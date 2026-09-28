using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>One reason a resident could come to kill one person (before personality decides whether they will).</summary>
    public sealed class MotiveCand { public string Target, Motive, Trigger, Protects, CopyOf, Base, Variant; public float P; }

    // =====================================================================================================================
    // MOTIVES — pressure from what the resident knows and has lived through (never from elapsed time alone):
    //   the old everyday pressures (Relations.Pressure: the wish, grudges, fear, jealousy) and the event-born ones the old
    //   planner had no eyes for — a secret about to come out (CH06 envelopes), someone threatening the person you would die
    //   for, a rival in love, noticing someone is plotting against YOU, a witness to your own first crime, grief that has
    //   found a culprit (right or wrong), and the copycat's cold arithmetic: "the first killing is the one they judge".
    // =====================================================================================================================
    public static partial class Initiative
    {
        static readonly string[] Legacy = { "wish", "escape", "grudge", "fear", "jealousy" };

        /// <summary>Restraint plus the wall (Conscience) — the same inhibition the old planner's pressure subtracts.</summary>
        static float Inhibit(GameState S, Actor a) => Conscience.Inhibit(S, a);
        /// <summary>Strong bonds block (the old planner's rule): love, attachment, family.</summary>
        static float Block(GameState S, string a, string t)
        {
            if (!S.HasRel(a, t)) return 0; var r = S.R(a, t);
            return r.Attach * 0.9f + Math.Max(0, r.Like) * 0.6f + (r.Tags.Contains("lover") ? 0.6f : 0) + (r.Tags.Contains("family") ? 0.8f : 0);
        }
        static bool Valid(GameState S, Actor a, string t)
        {
            var x = S.A(t); return x != null && x.Alive && !x.IsButler && x.Id != a.Id && x.Status != ActorStatus.Escaped && x.Status != ActorStatus.Executed;
        }

        internal static float Threshold(Actor a, string motive)
        {
            var st = SchemeStyles.Of(a.Def); float th = 0.35f;
            if (st.Style == "Impulsive") th -= 0.05f; else if (st.Style == "Meticulous") th += 0.03f;
            if (motive == "defense" || motive == "silence" || motive == "avenge") th -= 0.05f;
            if (motive == "copycat") th += 0.05f;
            return th;
        }

        /// <summary>The strongest reason this resident has right now, from their own knowledge. calm: the chapter's quiet stretch
        /// (only the resolved plan early — pressure ≥ 0.5); inv: the investigation (only the event-born motives).</summary>
        static MotiveCand BestMotive(Simulation sim, Actor a, bool inv, bool calm)
        {
            var S = sim.S; var list = new List<MotiveCand>();
            EventMotives(sim, a, inv, list);
            if (calm && !inv)
            {
                var (p, t, m) = Relations.Pressure(sim, a);
                if (t != null && p >= 0.5f) { var c = new MotiveCand { Target = t, Motive = m, P = p }; Refine(sim, a, c); list.Add(c); }
            }
            MotiveCand best = null;
            foreach (var c in list)
            {
                if (!Valid(S, a, c.Target)) continue;
                if (best == null || c.P > best.P + 1e-4f || Math.Abs(c.P - best.P) <= 1e-4f && string.CompareOrdinal(c.Target, best.Target) < 0) best = c;
            }
            return best;
        }

        /// <summary>The old planner's motive, read closer: a wish after executions, a deadline or with fear rising is a wish to get OUT.</summary>
        static void Refine(Simulation sim, Actor a, MotiveCand c)
        {
            var S = sim.S;
            if (c.Motive == "wish")
            {
                bool deadline = S.RuleActive("CH09") && S.Rule("CH09").Targets.Contains(a.Id);
                bool executions = S.Settlements.Any(x => x.Loop == S.Loop && x.Executed != null);
                if (deadline || executions || a.Needs.Fear > 0.55f) { c.Motive = "escape"; c.Trigger = deadline ? "정산 기한 통지를 받고, 이번이 아니면 소원이 사라진다는 생각에" : executions ? "처형을 지켜본 뒤로, 살아서 이 저택을 나가야 한다는 생각에" : "하루하루 커지는 두려움 속에서, 먼저 나가야 한다는 생각에"; }
            }
            if (c.Trigger == null) c.Trigger = TriggerText(sim, a, c);
        }

        static string TriggerText(Simulation sim, Actor a, MotiveCand c)
        {
            var S = sim.S; string t = Name(c.Target);
            string mem = S.HasRel(a.Id, c.Target) && S.R(a.Id, c.Target).Memory.Count > 0 ? S.R(a.Id, c.Target).Memory[S.R(a.Id, c.Target).Memory.Count - 1] : null;
            if (mem != null) { int sp = mem.IndexOf(' '); int sp2 = sp >= 0 ? mem.IndexOf(' ', sp + 1) : -1; if (sp2 > 0) mem = mem.Substring(sp2 + 1); }   // strip "N일차 HH:MM"
            switch (c.Motive)
            {
                case "wish": return "계약한 소원을 이루려면 누군가 죽어야 한다는 셈을 끝내고";
                case "escape": return "살아서 이 저택을 나가야 한다는 생각에";
                case "grudge": return K(mem != null ? $"{t}에게 쌓인 원한 때문에 — \"{mem}\"" : $"{t}에게 쌓인 원한 때문에");
                case "fear": return K($"{t}이(가) 자기를 해칠 거라는 두려움에");
                case "jealousy": return K($"{t}을(를) 향한 질투를 더는 삼킬 수 없어서");
            }
            return K($"{t}을(를) 둘러싼 일 때문에");
        }

        /// <summary>Motives born from events (what the old planner could not see).</summary>
        static void EventMotives(Simulation sim, Actor a, bool inv, List<MotiveCand> list)
        {
            var S = sim.S; var k = S.K(a.Id); var c = a.Def; float inh = Inhibit(S, a);
            var facts = k.Facts.ToList(); facts.Sort(StringComparer.Ordinal);
            if (!inv)
            {
                // 1) secret — someone holds my past (CH06 envelopes; bible knots)
                foreach (var f in facts)
                {
                    if (!f.StartsWith("knows-my-secret:")) continue; var t = f.Substring(16); if (!Valid(S, a, t)) continue;
                    float p = 0.42f + (1 - c.P.Honesty) * 0.35f + c.P.Pride * 0.12f + (S.HasRel(a.Id, t) ? S.R(a.Id, t).Fear * 0.3f : 0) - Block(S, a.Id, t) - inh;
                    list.Add(new MotiveCand { Target = t, Motive = "secret", P = p, Trigger = K(k.Facts.Contains("letter:secret:" + t) ? $"저택의 편지가 {Name(t)}이(가) 자기 과거를 알고 있다고 알려 온 뒤로" : k.Facts.Contains("envelope-about-me") ? $"자기 과거가 적힌 봉투가 {Name(t)}에게 갔다는 걸 알고" : $"{Name(t)}이(가) 자기 비밀을 안다는 걸 알고") });
                }
                // 2) protect — the person I would die for is threatened (their secret in someone's hands, or they were attacked)
                foreach (var x in S.Living.OrderBy(q => q.Id, StringComparer.Ordinal))
                {
                    if (x == a || x.IsPlayer || !S.HasRel(a.Id, x.Id)) continue; var rx = S.R(a.Id, x.Id);
                    if (rx.Attach < 0.5f && !rx.Tags.Contains("family") && !rx.Tags.Contains("lover")) continue;
                    var kx = S.K(x.Id);
                    foreach (var f in kx.Facts.Where(f => f.StartsWith("knows-my-secret:") || f.StartsWith("attacked-by:")).OrderBy(f => f, StringComparer.Ordinal))
                    {
                        var t = f.Substring(f.IndexOf(':') + 1); if (!Valid(S, a, t) || t == x.Id) continue;
                        float p = 0.3f + rx.Attach * 0.45f + c.P.Loyalty * 0.2f + (f.StartsWith("attacked") ? 0.15f : 0) - Block(S, a.Id, t) - inh * 0.8f;
                        list.Add(new MotiveCand { Target = t, Motive = "protect", Protects = x.Id, P = p, Trigger = K(f.StartsWith("attacked") ? $"{Name(t)}이(가) {Name(x.Id)}을(를) 해치려 했다는 걸 알고, {Name(x.Id)}을(를) 지키려고" : $"{Name(t)}이(가) {Name(x.Id)}의 비밀을 쥐고 있다는 걸 알고, {Name(x.Id)}을(를) 지키려고") });
                    }
                }
                // 3) love — a rival beside the one I love
                foreach (var x in S.Living.OrderBy(q => q.Id, StringComparer.Ordinal))
                {
                    if (x == a || !S.HasRel(a.Id, x.Id) || S.R(a.Id, x.Id).Romance < 0.3f) continue;
                    foreach (var t in S.Living.OrderBy(q => q.Id, StringComparer.Ordinal))
                    {
                        if (t == a || t == x || !S.HasRel(a.Id, t.Id) || !S.HasRel(x.Id, t.Id)) continue;
                        var rxt = S.R(x.Id, t.Id); var rat = S.R(a.Id, t.Id);
                        if (rat.Jealous < 0.2f || (rxt.Like < 0.35f && rxt.Romance < 0.25f)) continue;
                        float p = 0.3f + rat.Jealous * 0.6f + c.P.Jealousy * 0.3f + S.R(a.Id, x.Id).Romance * 0.3f - Block(S, a.Id, t.Id) - inh;
                        list.Add(new MotiveCand { Target = t.Id, Motive = "love", Protects = x.Id, P = p, Trigger = K($"{Name(x.Id)} 곁에 있는 {Name(t.Id)}을(를) 더는 견딜 수 없어서") });
                    }
                }
            }
            // 4) defense — I noticed someone preparing against me (their tell became my fear)
            foreach (var f in facts)
            {
                if (!f.StartsWith("threat:")) continue; var t = f.Substring(7); if (!Valid(S, a, t)) continue;
                float p = 0.35f + (S.HasRel(a.Id, t) ? S.R(a.Id, t).Fear : 0) * 0.6f + (1 - c.P.Morality) * 0.25f + c.P.Aggression * 0.2f - Block(S, a.Id, t) - inh * 0.7f;
                var theirs = OpenOf(S, t);
                list.Add(new MotiveCand { Target = t, Motive = "defense", P = p, Variant = theirs != null && theirs.Victim == a.Id ? "turnabout" : null, Trigger = K(k.Facts.Contains("letter:threat:" + t) ? $"저택의 편지가 {Name(t)}이(가) 자기를 지켜보고 있다고 알려 온 뒤로, 먼저 움직이려고" : $"{Name(t)}이(가) 자기를 노리고 뭔가 준비한다는 걸 눈치채고, 먼저 움직이려고") });
            }
            // 5) silence — someone saw me around my own first crime
            foreach (var inc in S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Culprit == a.Id && i.Murder).OrderBy(i => i.Id, StringComparer.Ordinal))
            {
                double t0 = inc.CauseClock - 20, t1 = inc.CauseClock + 12;
                var near = new HashSet<int> { inc.CauseRoom, inc.DeathRoom }; foreach (var nb in S.Layout.Neighbors(inc.CauseRoom)) near.Add(nb);
                foreach (var s in k.Sightings)
                {
                    if (s.T1 < t0 || s.T0 > t1 || !near.Contains(s.Room) || s.Target == inc.Victim || s.Dead) continue;
                    if (!Valid(S, a, s.Target)) continue;
                    if (list.Any(q => q.Motive == "silence" && q.Target == s.Target)) continue;
                    float p = 0.42f + (1 - c.P.Morality) * 0.4f + (s.Room == inc.CauseRoom ? 0.12f : 0) - Block(S, a.Id, s.Target) - inh * 0.6f;
                    list.Add(new MotiveCand { Target = s.Target, Motive = "silence", P = p, Variant = "silencer", Trigger = K($"{Name(s.Target)}이(가) {When(s.T0)} {S.RoomName(s.Room)} 근처에서 자기를 봤다는 걸 알고") });
                }
            }
            // 6) avenge — the one I loved is dead, and I believe I know who did it (maybe wrongly)
            if (inv || S.Incidents.Values.Any(i => i.Loop == S.Loop && i.Confirmed && S.Clock - i.ConfirmClock < 36 * 60))
            {
                foreach (var inc in S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Confirmed && i.Victim != a.Id).OrderBy(i => i.Id, StringComparer.Ordinal))
                {
                    if (!S.HasRel(a.Id, inc.Victim)) continue; var rv = S.R(a.Id, inc.Victim);
                    if (rv.Attach < 0.45f && !rv.Tags.Contains("friend") && !rv.Tags.Contains("lover") && !rv.Tags.Contains("family")) continue;
                    var top = k.Suspicion.Where(kv => kv.Value >= 0.4f && Valid(S, a, kv.Key)).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).FirstOrDefault();
                    if (top.Key == null) continue;
                    float p = 0.3f + a.Needs.Grief * 0.4f + c.P.Aggression * 0.3f + c.P.Grudge * 0.2f + top.Value * 0.2f - Block(S, a.Id, top.Key) - inh;
                    list.Add(new MotiveCand { Target = top.Key, Motive = "avenge", Protects = inc.Victim, P = p, Variant = "avenger", Trigger = K($"{Name(inc.Victim)}을(를) 죽인 게 {Name(top.Key)}이라고 믿고") });
                }
            }
            // 7) copycat — a first killing is on the house's hands; the one they judge is the FIRST (rule 여섯)
            var first = S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Murder && i.Confirmed && i.Culprit != a.Id).OrderBy(i => i.ResultSeq).FirstOrDefault();
            if (first != null && c.Infer >= 60 && c.P.Morality < 0.55f)
            {
                var (p0, t0, m0) = Relations.Pressure(sim, a);
                if (t0 != null && t0 != first.Victim && p0 >= 0.12f && Valid(S, a, t0))
                    list.Add(new MotiveCand { Target = t0, Motive = "copycat", Base = m0, CopyOf = first.Id, P = p0 + 0.22f, Variant = "copycat",
                        Trigger = K($"{Name(first.Victim)}의 죽음으로 저택이 어수선한 지금이라면, 심판은 첫 사건만 가린다는 규칙 여섯이 자기를 가려 줄 거라 믿고") });
            }
        }

        /// <summary>Pressure now against the scheme's own target (the conscience check while preparing).</summary>
        static float CurrentPressure(Simulation sim, Actor a, Scheme sc)
        {
            var S = sim.S;
            if (Array.IndexOf(Legacy, sc.Motive) >= 0)
            {
                var (p, t, m) = Relations.Pressure(sim, a);
                return t == sc.Victim ? p : p * 0.7f;
            }
            var list = new List<MotiveCand>(); EventMotives(sim, a, S.Phase == Phase.Investigation, list);
            float best = -1; foreach (var c in list) if (c.Target == sc.Victim && c.P > best) best = c.P;
            return best < 0 ? Math.Max(0.15f, sc.Pressure - 0.05f) : best;   // an event motive does not simply fade
        }

        /// <summary>The target of this resident's prepared tells: when the victim of a scheme notices, fear turns into a counter-plan.</summary>
        internal static void Noticed(Simulation sim, Scheme sc, SchemeBeat b)
        {
            var S = sim.S; var v = S.A(sc.Victim);
            if (v == null || v.IsPlayer || !b.Observers.Contains(v.Id)) return;
            if (b.Kind != "shadow" && b.Kind != "obtain" && b.Kind != "scout") return;
            double chance = v.Def.Obs / 100.0 * 0.5 + v.Def.Infer / 100.0 * 0.3 - S.A(sc.Culprit).Def.Deceit / 100.0 * 0.35;
            if (U(S, b.Id + ":notice") >= chance) return;
            S.K(v.Id).Facts.Add("threat:" + sc.Culprit);
            Relations.Change(S, v.Id, sc.Culprit, fear: 0.2f, trust: -0.1f, memory: "나를 노리고 뭔가 꾸미는 것 같다");
            S.Log("SchemeNoticed", v.Id, sc.Culprit, data: b.Id, secret: true);
        }
    }
}
