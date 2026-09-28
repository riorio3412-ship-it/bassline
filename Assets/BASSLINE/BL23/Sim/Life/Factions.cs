using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>A group that holds together (SocialEventsDesign §2): who is in, who leads, who they are against.</summary>
    [Serializable]
    public sealed class Faction
    {
        public string Id; public int Loop; public string Name; public string Leader; public string Rival;
        public List<string> Members = new List<string>();
        public double Formed, Changed;
    }

    /// <summary>
    /// Factions (owner, 2026-09-28: "친밀도가 높은 그룹은 따로 파벌이 생기는 시스템"). Each morning at 06:00 and at a chapter's
    /// start the residents' ties are read as a graph: the closest pairs seed groups, whoever stands close enough to a group
    /// joins it (at most five, one group each), and a group of three or more is a faction. A faction keeps its name while
    /// most of it stays together, so joining, leaving and falling apart are events people talk about. It has a leader, may
    /// have a rival faction, and leaves some residents out. It shapes who is invited and who comes (Grammars), what the
    /// table talks about (LifeTable "faction"), how ties drift, who feels left out (strain, the wish's pull), and whom a juror
    /// will not vote for. No random stream is drawn: the order is the ties' own, ties broken by id.
    /// </summary>
    public static class Factions
    {
        // (the ties' scale: the closest pairs of a first day read about 0.15–0.22, the median about 0.05)
        const float PairMin = 0.12f, JoinMin = 0.09f, RivalMax = -0.05f, Alone = 0.08f; const int MaxSize = 5, MinSize = 3;

        /// <summary>How close two residents are: like, trust and attachment both ways, plus a tie the story gave them.</summary>
        public static float Affinity(GameState S, string a, string b)
        {
            float One(string x, string y) { if (!S.HasRel(x, y)) return 0; var r = S.R(x, y); return (r.Like + r.Trust + r.Attach) / 3f; }
            return (One(a, b) + One(b, a)) / 2f + (CastWeb.TieBetween(a, b) != null ? 0.05f : 0f);
        }

        public static Faction Of(GameState S, string id) => id == null ? null : S.Factions.FirstOrDefault(f => f.Loop == S.Loop && f.Members.Contains(id));
        public static bool Together(GameState S, string a, string b) { var f = Of(S, a); return f != null && f.Members.Contains(b); }
        public static bool Rivals(GameState S, string a, string b) { var fa = Of(S, a); var fb = Of(S, b); return fa != null && fb != null && (fa.Rival == fb.Id || fb.Rival == fa.Id); }
        /// <summary>A resident no faction holds and nobody stands close to.</summary>
        public static bool LeftOut(GameState S, string id) => S.Day >= 2 && Of(S, id) == null && S.LivingNpcs.Where(x => x.Id != id).Select(x => Affinity(S, id, x.Id)).DefaultIfEmpty(0).Max() < Alone;

        /// <summary>Read the graph again (06:00, a chapter's start).</summary>
        public static void Compute(Simulation sim)
        {
            var S = sim.S;
            S.Factions.RemoveAll(f => f.Loop != S.Loop);
            var people = S.LivingNpcs.Where(a => a.Status != ActorStatus.Escaped && a.Status != ActorStatus.Executed).Select(a => a.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var aff = new Dictionary<(string, string), float>();
            float A(string x, string y) { var k = string.CompareOrdinal(x, y) < 0 ? (x, y) : (y, x); if (!aff.TryGetValue(k, out var v)) aff[k] = v = Affinity(S, x, y); return v; }
            var pairs = new List<(string a, string b, float v)>();
            for (int i = 0; i < people.Count; i++) for (int j = i + 1; j < people.Count; j++) { float v = A(people[i], people[j]); if (v >= PairMin) pairs.Add((people[i], people[j], v)); }
            pairs.Sort((p, q) => q.v != p.v ? q.v.CompareTo(p.v) : string.CompareOrdinal(p.a + p.b, q.a + q.b));
            var taken = new HashSet<string>(); var groups = new List<List<string>>();
            foreach (var (a, b, _) in pairs)
            {
                if (taken.Contains(a) || taken.Contains(b)) continue;
                var g = new List<string> { a, b };
                while (g.Count < MaxSize)
                {
                    string best = null; float bv = JoinMin;
                    foreach (var c in people) { if (taken.Contains(c) || g.Contains(c)) continue; float m = g.Average(x => A(x, c)); if (m > bv + 1e-5f) { bv = m; best = c; } }
                    if (best == null) break; g.Add(best);
                }
                if (g.Count < MinSize) continue;   // a close pair is a friendship, not a faction (they stay free to join another)
                foreach (var x in g) taken.Add(x);
                groups.Add(g);
            }
            // match to yesterday's factions (most members in common), so a faction lives on through joins and departures
            var old = S.Factions.ToList(); var now = new List<Faction>();
            foreach (var g in groups)
            {
                var prev = old.Where(f => !now.Contains(f)).Select(f => (f, n: f.Members.Count(g.Contains))).Where(x => x.n >= 2).OrderByDescending(x => x.n).ThenBy(x => x.f.Id, StringComparer.Ordinal).Select(x => x.f).FirstOrDefault();
                var f = prev ?? new Faction { Id = S.NewId("fac"), Loop = S.Loop, Formed = S.Clock };
                var joined = g.Where(x => !f.Members.Contains(x)).ToList(); var left = f.Members.Where(x => !g.Contains(x)).ToList();
                f.Members = g.OrderBy(x => x, StringComparer.Ordinal).ToList();
                f.Leader = f.Members.OrderByDescending(x => f.Members.Where(y => y != x).Average(y => A(x, y)) + S.A(x).Def.P.Pride * 0.3f + S.A(x).Def.P.Sociability * 0.3f).ThenBy(x => x, StringComparer.Ordinal).First();
                f.Name = Cast.GivenOf(f.Leader) + "네";
                if (prev == null) { S.Log("FactionFormed", f.Leader, data: f.Id + ":" + string.Join(",", f.Members)); f.Changed = S.Clock; }
                else
                {
                    foreach (var x in joined) { S.Log("FactionJoined", x, f.Leader, data: f.Id); f.Changed = S.Clock; }
                    foreach (var x in left) { S.Log("FactionLeft", x, f.Leader, data: f.Id); f.Changed = S.Clock; if (S.A(x)?.Alive == true) foreach (var y in f.Members) Relations.Change(S, y, x, like: -0.02f, jealous: 0.03f, memory: "무리를 떠났다"); }
                }
                now.Add(f);
            }
            foreach (var f in old.Where(f => !now.Contains(f))) S.Log("FactionDissolved", f.Leader, data: f.Id);
            // rivals: the two factions that stand furthest apart
            foreach (var f in now) f.Rival = null;
            foreach (var f in now)
            {
                var r = now.Where(o => o != f).Select(o => (o, v: f.Members.SelectMany(x => o.Members.Select(y => A(x, y))).Average())).Where(x => x.v < RivalMax).OrderBy(x => x.v).ThenBy(x => x.o.Id, StringComparer.Ordinal).Select(x => x.o).FirstOrDefault();
                if (r != null) { f.Rival = r.Id; if (r.Rival == null) r.Rival = f.Id; }
            }
            S.Factions = now;
        }

        /// <summary>06:00: the graph is read again; inside a faction ties warm, between rivals they sour; the left-out feel it.</summary>
        public static void Morning(Simulation sim)
        {
            var S = sim.S; if (S.Phase != Phase.Daily && S.Phase != Phase.Investigation) return;
            Compute(sim);
            foreach (var f in S.Factions)
            {
                foreach (var a in f.Members) foreach (var b in f.Members) if (a != b) Relations.Change(S, a, b, like: 0.005f, trust: 0.004f);
                var rival = S.Factions.FirstOrDefault(o => o.Id == f.Rival);
                if (rival != null) foreach (var a in f.Members) Relations.Change(S, a, rival.Leader, grudge: 0.004f);
            }
            foreach (var a in S.LivingNpcs.OrderBy(x => x.Id, StringComparer.Ordinal))
                if (LeftOut(S, a.Id)) { a.Needs.Stress = MathX.Clamp01(a.Needs.Stress + 0.03f); S.K(a.Id).Facts.Add("left-out:" + S.Day); }
        }

        /// <summary>How a faction bends an answer to an invitation (Grammars.WouldAccept).</summary>
        public static double InviteBias(GameState S, string guest, Gathering g)
        {
            var fx = Of(S, guest); if (fx == null) return 0;
            double d = 0; var fh = Of(S, g.Host);
            if (fh != null && fh.Id == fx.Id) d += 0.35;
            else if (fh != null && (fx.Rival == fh.Id || fh.Rival == fx.Id) && g.Host == fh.Leader) d -= 0.3;
            // where the leader goes, the faction goes
            if (fx.Leader != guest && g.Status.TryGetValue(fx.Leader, out var ls)) { if (ls == "declined") d -= 0.15; else if (ls == "accepted" || ls == "host") d += 0.1; }
            return d;
        }

        /// <summary>For notes and the lab: one line per faction.</summary>
        public static string Describe(GameState S) => S.Factions.Count == 0 ? "없음" : string.Join(" / ", S.Factions.Select(f => $"{f.Name}({string.Join("·", f.Members.Select(Cast.GivenOf))}){(f.Rival != null ? " ↔ " + S.Factions.FirstOrDefault(o => o.Id == f.Rival)?.Name : "")}"));
    }
}
