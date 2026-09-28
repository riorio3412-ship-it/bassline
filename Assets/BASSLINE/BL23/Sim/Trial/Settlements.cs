using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>Verdict → settlement (once) → reveal → continuation. Chapter increments only when the next daily life starts.</summary>
    public static class Settlements
    {
        public static void Resolve(Simulation sim, TrialState T)
        {
            var S = sim.S; string sid = $"L{S.Loop}C{S.Chapter}";
            if (S.Settlements.Any(x => x.Id == sid && x.Applied)) return; // idempotent
            var inc = TrialSystem.TargetIncident(S);
            string culprit = S.Ch.TargetCulprit;
            var set = new Settlement { Id = sid, Loop = S.Loop, Chapter = S.Chapter, Accused = T.Accused, Culprit = culprit, Votes = new Dictionary<string, string>(T.Votes) };
            var rng = S.R(Stream.VoteDraw);
            if (T.NoMurder || culprit == null)
            {
                set.Exception = true; set.Note = "살인이 아니었다 — 사실만 확인하고 심판을 마친다";
            }
            else if (!(S.A(culprit)?.Alive ?? false))
            {
                // the judged culprit died before the verdict: responsibility confirmation hearing (BL23 §2.4)
                set.Exception = true; set.Correct = T.Accused == culprit; set.Note = "책임만 가리는 심판 — 범인은 이미 숨졌다";
            }
            else if (T.Accused == culprit)
            {
                set.Correct = true; set.Executed = culprit;
            }
            else
            {
                set.Correct = false; set.Escaped = culprit;
                var pool = S.Living.Where(a => a.Id != culprit).Select(a => a.Id).OrderBy(x => x).ToList();
                if (pool.Count > 0) { set.Drawn = pool[rng.R(pool.Count)]; set.Executed = set.Drawn; }
            }
            // apply
            if (set.Executed != null) { var e = S.A(set.Executed); e.Status = ActorStatus.Executed; e.ExecutedChapter = S.Chapter; S.Log("Executed", Cast.Butler, e.Id, data: set.Correct ? "correct" : "draw"); }
            if (set.Escaped != null) { var e = S.A(set.Escaped); e.Status = ActorStatus.Escaped; e.EscapedChapter = S.Chapter; S.Log("Escaped", e.Id, data: "wish granted"); }
            foreach (var i in S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter)) { i.ProcedureClosed = true; i.SettlementApplied = true; i.RevealEligible = true; }
            set.Survivors = S.Survivors; set.Applied = true;
            Grade(sim, T, set);
            S.Settlements.Add(set);
            S.LastVerdictSummary = Summary(S, set);
            S.Dev($"SETTLE {sid} accused={set.Accused} culprit={culprit} correct={set.Correct} exec={set.Executed} esc={set.Escaped} survivors={set.Survivors}");
            S.Emit(GameEventType.Verdict, set.Executed, set.Escaped, text: S.LastVerdictSummary, data: set.Correct ? "correct" : set.Exception ? "exception" : "wrong");
            // everyone alive remembers the verdict; relationships shift (accusers vs accused etc.)
            foreach (var a in S.Living)
            {
                // witnessing an execution is a real, shaking event: fear of dying before the wish, stress, grief
                a.Needs.Stress = MathX.Clamp01(a.Needs.Stress + 0.25f + a.Def.P.Fearfulness * 0.2f); a.Needs.Fear = MathX.Clamp01(a.Needs.Fear + 0.3f);
                if (set.Executed != null && S.HasRel(a.Id, set.Executed)) { a.Needs.Grief = MathX.Clamp01(a.Needs.Grief + S.R(a.Id, set.Executed).Attach); }
                foreach (var kv in T.Votes.Where(kv => kv.Value == a.Id && kv.Key != a.Id)) if (!(set.Correct && a.Id == culprit)) Relations.Change(S, a.Id, kv.Key, grudge: 0.12f, like: -0.08f, memory: "심판에서 나에게 표를 던졌다");
            }
            if (set.Executed == Cast.Player || set.Escaped == Cast.Player) S.Flags["player_out"] = S.Clock;
            Conscience.OnSettlement(sim, set);
            sim.SetPhase(Phase.Verdict);
        }

        static string Summary(GameState S, Settlement s)
        {
            if (s.Exception) return s.Note + (s.Accused != null ? $" (지목: {Cast.NameOf(s.Accused)})" : "");
            if (s.Correct) return $"지목이 맞았다 — {Cast.NameOf(s.Executed)}을(를) 처형한다";
            return $"지목이 빗나갔다 — 진범 {Cast.NameOf(s.Escaped)}은(는) 빠져나가고, 추첨으로 뽑힌 {Cast.NameOf(s.Drawn)}이(가) 처형된다";
        }

        /// <summary>Evaluation (03 부록 I): duties met, normalized to 1000. Separate from the group verdict.</summary>
        static void Grade(Simulation sim, TrialState T, Settlement set)
        {
            var S = sim.S; var k = S.K(Cast.Player); var inc = TrialSystem.TargetIncident(S);
            if (inc == null) return;
            double inv = Math.Min(1, k.Evidence.Count(e => e.Chapter == S.Chapter && (e.Kind == EvKind.Body || e.Kind == EvKind.Trace || e.Kind == EvKind.ObjectState)) / 5.0);
            double logic = T.Valid + T.Invalid == 0 ? 0 : T.Valid / (double)(T.Valid + T.Invalid);
            double disc = k.Examined.Contains("body:" + inc.Victim) ? 1 : 0.3;
            double ver = Math.Min(1, Logic.IndependentRoots(k.Evidence.Where(e => e.Chapter == S.Chapter)) / 6.0);
            double court = Math.Min(1, T.Influence);
            double inter = S.Incidents.Values.Any(i => i.Rescued) ? 1 : 0.5;
            double recon = T.ReconstructMax > 0 ? T.ReconstructScore / (double)T.ReconstructMax : 0;
            double score = inv * 200 + logic * 200 + disc * 150 + ver * 150 + (court * 0.5 + recon * 0.5) * 200 + inter * 100;
            set.Grade = (int)Math.Round(score);
            bool rightCall = T.Votes.TryGetValue(Cast.Player, out var pv) && pv == set.Culprit;
            string letter = score >= 900 && rightCall ? "S" : score >= 800 ? "A" : score >= 650 ? "B" : score >= 500 ? "C" : "D";
            double mult = letter == "S" ? 1.5 : letter == "A" ? 1.25 : letter == "B" ? 1 : letter == "C" ? 0.8 : 0.5;
            int exp = (int)(500 * mult * (set.Correct ? 1 : 0.6));
            set.Exp = exp; S.Profile.Exp += exp;
            while (S.Profile.Level < 20 && S.Profile.Exp >= 300 + 100 * (S.Profile.Level - 1)) { S.Profile.Exp -= 300 + 100 * (S.Profile.Level - 1); S.Profile.Level++; S.Profile.Points++; }
            set.Note = (set.Note ?? "") + $" 평가 {letter} ({set.Grade})";
            S.Flags["grade:" + set.Id] = set.Grade;
        }

        /// <summary>Called by the presentation when the verdict/execution/reveal have been shown (or immediately headless).</summary>
        public static void AfterReveal(Simulation sim)
        {
            var S = sim.S;
            if (S.Phase == Phase.Daily) return;
            Rules.ExpireChapter(sim);
            S.Trial = null;
            // survivors return to the mansion; the trial took about ninety minutes
            S.Clock += 90;
            var hall = S.Layout.Rooms.First(r => r.Type == RoomType.GrandHall && r.Floor == 0);
            var rng = S.R(Stream.Life);
            foreach (var a in S.Actors.Values.Where(a => a.Alive || a.IsButler)) { a.Pos = sim.SnapPublic(sim.RandomPointIn(hall, rng)); a.Room = hall.Id; a.Act = null; a.NextThink = S.Clock + rng.Range(0.5f, 3); }
            // executed/escaped leave the mansion; the dead stay where they were found (their rooms are sealed memories)
            Continuation(sim);
        }

        public static void Continuation(Simulation sim)
        {
            var S = sim.S;
            bool unfinished = S.Incidents.Values.Any(i => i.Loop == S.Loop && !i.ProcedureClosed && i.Confirmed);
            if (unfinished) { sim.SetPhase(Phase.Investigation); return; }
            if (S.Survivors <= S.FloorLocked)
            {
                S.Log("LoopEnd", null, data: $"survivors={S.Survivors} floor={S.FloorLocked}");
                S.Dev($"LOOP END survivors {S.Survivors} <= floor {S.FloorLocked}");
                sim.SetPhase(Phase.LoopEpilogue);
                return;
            }
            sim.BeginChapter(false);
            // --- time-on-demand (begin): with a still clock (OnDemand) the words must match the hour — "아침입니다" only in the
            // morning; at night everyone is sent to bed (the presentation passes the night to the 7:00 bell), by day to their day.
            // The continuous flow (probes, headless campaigns) is unchanged.
            if (sim.OnDemand)
            {
                int m = S.Minute;
                sim.Announce(m >= 5 * 60 && m < 11 * 60 ? "y_morning" : m >= 20 * 60 || m < 5 * 60 ? "y_trial_night" : "y_trial_day", null);
                return;
            }
            // --- time-on-demand (end)
            sim.Announce("y_morning", null);
        }

        public static void NextLoop(Simulation sim)
        {
            var S = sim.S;
            // archive (ArchiveMeta — never copied into NPC knowledge)
            foreach (var i in S.Incidents.Values) S.Archive.Add($"L{i.Loop}C{i.Chapter} {Cast.NameOf(i.Victim)} ← {Cast.NameOf(i.Culprit) ?? "사고"} ({i.Method}, {S.RoomName(i.DeathRoom)})");
            S.Profile.Runs++;
            sim.StartLoop(S.Loop + 1);
            sim.Announce("y_loop_reset", null);
        }
    }
}
