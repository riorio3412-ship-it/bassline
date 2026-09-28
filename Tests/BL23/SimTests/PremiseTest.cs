using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;

/// <summary>
/// The premise in the lab (owner, 2026-09-28): the invitation, the first table's pact, the wall (Conscience) and the
/// house's pushes (HousePush). One campaign, 민혁 at every breakfast and dinner:
///   premise &lt;seed&gt; &lt;days&gt; [off]   — the table scenes as a transcript (pact, the token, the second wish, the broken pact,
///                                     rules, hunger, deaths), the house's announcements, what wore the wall each day, the
///                                     wall of every resident at the morning bell, the first murder's clock; save round-trip.
///                                     off: the old mind (no wall, no pushes) for comparison.
/// </summary>
public static partial class Program
{
    static int PremiseTest(string[] args)
    {
        ulong seed = args.Length > 1 ? ulong.Parse(args[1]) : 20260926UL;
        int days = args.Length > 2 ? int.Parse(args[2]) : 7;
        bool off = args.Contains("off");
        Conscience.Enabled = !off; HousePush.Enabled = !off;
        var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
        var rng = new Rng(seed ^ 0x9E3779B9UL, 11);
        Console.WriteLine($"PREMISE seed={seed} days={days} {(off ? "(off: no wall, no pushes)" : "")}");
        double end = days * 1440; long ticks = 0; int lastMin = -1; long seenSeq = -1; double firstMurder = -1;
        string Who(string id) => id == null ? "—" : Cast.GivenOf(id) ?? id;
        double g0(Gathering g) => g.Cur.Start;
        void Print(LifeStage st)
        {
            int g = 0;
            while (st != null && g++ < 6)
            {
                foreach (var u in st.Lines) { Console.WriteLine($"      {Who(u.Speaker)}{(u.Emotion != Emotion.Neutral ? " (" + u.Emotion + ")" : "")}: {u.Text}"); sim.Spoken(u); }
                if (st.Options == null || st.Options.Count == 0 || st.Done) break;
                int pick = rng.R(st.Options.Count);
                Console.WriteLine($"      ▶ 민혁 고름: {st.Options[pick]}");
                st = sim.LifePick(pick);
            }
        }
        while (S.Clock < end && ticks < 30_000_000)
        {
            if (S.Phase == Phase.Trial)
            {
                TrialSystem.RunHeadless(sim, true); Replay.BuildSegments(sim); Settlements.AfterReveal(sim);
                Console.WriteLine($"   [{ClockFmt.DayHM(S.Clock)}] VERDICT {S.LastVerdictSummary}");
                if (S.Phase == Phase.LoopEpilogue) { Settlements.NextLoop(sim); S.Phase = Phase.Daily; }
                if (S.Phase == Phase.Trial) break;
                continue;
            }
            if (S.Phase == Phase.Investigation && !S.Flags.ContainsKey("testinv:" + S.Chapter + ":" + S.Loop))
            {
                S.Flags["testinv:" + S.Chapter + ":" + S.Loop] = 1;
                foreach (var inc in S.Incidents.Values.Where(i => i.Confirmed && i.Chapter == S.Chapter && i.Loop == S.Loop))
                { var body = S.A(inc.Victim); Evidences.ExamineBody(sim, S.Player, body, true); }
            }
            sim.Step(); ticks++;
            int min = (int)Math.Floor(S.Clock);
            if (min == lastMin) continue; lastMin = min;
            // the ledger: the house's words, the wall wearing, deaths
            foreach (var e in S.Ledger.Where(x => x.Seq > seenSeq).ToList())
            {
                seenSeq = Math.Max(seenSeq, e.Seq);
                if (e.Type == "Announce")
                {
                    var p = (e.Data ?? "").Split(new[] { '|' }, 2); string key = p[0];
                    if (key.StartsWith("y_push") || key.StartsWith("y_rule_") || key.StartsWith("y_hev") || key == "y_hunger" || key == "y_body")
                        Console.WriteLine($"   [{ClockFmt.DayHM(e.Clock)}] 유스티 «{key}» {(p.Length > 1 ? p[1].Replace("|", " / ") : "")}");
                }
                else if (e.Type == "HousePush" || e.Type == "HousePushSkip" || e.Type == "RuleImposed" || e.Type == "HouseEvent" || e.Type == "HouseDark" || e.Type == "HouseMasks" || e.Type == "HouseHunt" || e.Type.StartsWith("Faction")) Console.WriteLine($"   [{ClockFmt.DayHM(e.Clock)}] {e.Type} {Who(e.Actor)} {e.Data}");
                else if (e.Type == "WallErode" || e.Type == "WallMend") Console.WriteLine($"   [{ClockFmt.DayHM(e.Clock)}] {e.Type} {Who(e.Actor)} {e.Data}");
            }
            foreach (var inc in S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Murder && !S.Flags.ContainsKey("ptdeath:" + i.Id)).ToList())
            {
                S.Flags["ptdeath:" + inc.Id] = 1; if (firstMurder < 0) firstMurder = inc.DeathClock;
                Console.WriteLine($"   [{ClockFmt.DayHM(inc.DeathClock)}] ✝ MURDER {Who(inc.Victim)} by {Who(inc.Culprit)} (L{inc.Loop}C{inc.Chapter})");
            }
            int m = S.Minute;
            if (S.Phase == Phase.Daily && m == 7 * 60 + 1)
            {
                var walls = S.LivingNpcs.OrderBy(a => a.Id, StringComparer.Ordinal).Select(a => $"{Who(a.Id)} {Conscience.Wall(S, a.Id):0.00}/{Conscience.Inhibit(S, a):0.00}");
                Console.WriteLine($"-- {ClockFmt.DayHM(S.Clock)} L{S.Loop}C{S.Chapter} alive {S.Living.Count()} hunger {Hunger.Level(S)} push {HousePush.Next(S)} pact {(Conscience.PactBroken(S) ? "broken" : "kept")}");
                Console.WriteLine("   wall/inhibit: " + string.Join(", ", walls));
                Console.WriteLine("   factions: " + Factions.Describe(S) + " · left out: " + string.Join(",", S.LivingNpcs.Where(x => Factions.LeftOut(S, x.Id)).Select(x => Who(x.Id))));
                {
                    var ids = S.LivingNpcs.Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal).ToList(); var all = new List<(string, float)>();
                    for (int i = 0; i < ids.Count; i++) for (int j = i + 1; j < ids.Count; j++) all.Add((Who(ids[i]) + "-" + Who(ids[j]), Factions.Affinity(S, ids[i], ids[j])));
                    var sorted = all.OrderByDescending(x => x.Item2).ToList();
                    Console.WriteLine("   affinity top: " + string.Join(", ", sorted.Take(10).Select(x => $"{x.Item1} {x.Item2:0.00}")) + $" · median {sorted[sorted.Count / 2].Item2:0.00} · bottom {sorted.Last().Item2:0.00}");
                }
            }
            // 민혁 goes to the house's evening (HouseEvents) a few minutes in, and the festival stage plays
            if (S.Phase == Phase.Daily && S.Player != null && S.Player.Alive)
            {
                var hg = S.Gatherings.FirstOrDefault(g => (HouseEvents.IsHouse(g) || Grammars.IsResidentKind(g.Kind)) && !g.Cancelled && S.Clock >= g.Cur.Start + 3 && S.Clock <= g.Cur.Start + 20 && !S.Flags.ContainsKey("ptsaw:" + g.Id));
                int need = HouseEvents.IsHouse(hg ?? new Gathering()) ? 4 : 2;
                if (hg != null && (S.Clock >= g0(hg) + 19 || (hg.Kind == "house:hunt" && S.Clock >= g0(hg) + 4) || S.LivingNpcs.Count(x => x.Room == hg.Cur.Room && hg.Status.ContainsKey(x.Id)) >= need))
                {
                    S.Flags["ptsaw:" + hg.Id] = 1;
                    var room = S.Layout.Room(hg.Cur.Room); var pos = sim.RandomPointIn(room, S.R(Stream.Presentation)); sim.SetPlayerPose(pos, 0, false, false);
                    var going = hg.Status.Where(kv => kv.Value == "accepted" || kv.Value == "attended" || kv.Value == "late" || kv.Value == "host").Select(kv => Who(kv.Key));
                    var no = hg.Status.Where(kv => kv.Value == "declined").Select(kv => Who(kv.Key));
                    Console.WriteLine($"   [{ClockFmt.DayHM(S.Clock)}] {(HouseEvents.IsHouse(hg) ? "저택 행사" : Who(hg.Host) + "의 모임")} 「{hg.Label}」 @{room.Name} · 온다: {string.Join(",", going)} · 안 온다: {string.Join(",", no)}");
                    var st = sim.LifeOffer(); if (st != null) Print(st); else Console.WriteLine("      (장면 없음 — 아직 사람이 덜 모였다)");
                }
            }
            // someone comes to confide (HousePush → LifeDialogue "confide"): 민혁 hears them out and answers
            if (S.Phase == Phase.Daily && S.Player != null && S.Player.Alive && m >= 8 * 60 && m <= 22 * 60 && m % 5 == 0)
            {
                var c = S.LivingNpcs.OrderBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault(x => sim.LifeGoKind(x.Id) == "confide" && sim.CanTalk(x, out _));
                if (c != null)
                {
                    var pos = sim.Snap(new P3(c.Pos.f, c.Pos.x + 0.9f, c.Pos.z)); sim.SetPlayerPose(pos, 0, false, false);
                    Console.WriteLine($"   [{ClockFmt.DayHM(S.Clock)}] 속내 — {Who(c.Id)} (벽 {Conscience.Wall(S, c.Id):0.00})");
                    sim.BeginTalk(c);
                    try
                    {
                        foreach (var u in sim.Choose(c, "lifego", null)) { Console.WriteLine($"      {Who(u.Speaker)}: {u.Text}"); sim.Spoken(u); }
                        for (int g = 0; g < 4; g++)
                        {
                            var picks = sim.Options(c).Where(x => x.Id == "lifepick").ToList(); if (picks.Count == 0) break;
                            var pk = picks[rng.R(picks.Count)];
                            foreach (var u in sim.Choose(c, "lifepick", pk.Arg)) { Console.WriteLine($"      {Who(u.Speaker)}: {u.Text}"); sim.Spoken(u); }
                        }
                    }
                    finally { sim.EndTalk(c); }
                    Console.WriteLine($"      → 벽 {Conscience.Wall(S, c.Id):0.00}");
                }
            }
            // 민혁 sits at breakfast and dinner, the way Session.CheckTableTalk stages it: once three are seated or eating
            string meal = m >= 7 * 60 && m < 10 * 60 ? "breakfast" : m >= 18 * 60 && m < 21 * 60 ? "dinner" : null;
            if (S.Phase == Phase.Daily && S.Player != null && S.Player.Alive && meal != null && m >= (meal == "breakfast" ? Simulation.MealStart[0] : Simulation.MealStart[2]) && !S.Flags.ContainsKey($"pttable:{S.Day}:{meal}"))
            {
                var din = S.Layout.First(RoomType.Dining); if (din == null) continue;
                if (S.Player.Room != din.Id) { var p = sim.RandomPointIn(din, S.R(Stream.Presentation)); sim.SetPlayerPose(p, 0, false, false); }
                var diners = S.LivingNpcs.Where(x => x.Room == din.Id && x.Status == ActorStatus.Active && (x.Anim == Anim.Eat || x.Pose == Pose.Sit) && x.TalkingTo == null).OrderBy(x => x.Id).Select(x => x.Id).ToList();
                if (diners.Count < 3 || (diners.Count < 4 && m < (meal == "breakfast" ? Simulation.MealStart[0] : Simulation.MealStart[2]) + 20)) continue;
                S.Flags[$"pttable:{S.Day}:{meal}"] = S.Clock;
                var st = sim.LifeTable(diners.Take(7).ToList(), meal);
                if (st != null) { Console.WriteLine($"   [{ClockFmt.DayHM(S.Clock)}] 식탁 「{st.Title}」 ({string.Join(",", diners.Select(Who))}) lines={st.Lines.Count} opts={st.Options?.Count ?? 0} done={st.Done}"); Print(st); }
            }
            if (S.Out.Count > 2000) S.Out.Clear();
        }
        var json = SaveStore.Serialize(S); bool same = SaveStore.Serialize(SaveStore.Deserialize(json)) == json;
        Console.WriteLine($"== first murder {(firstMurder < 0 ? "none" : ClockFmt.DayHM(firstMurder))} · clock {ClockFmt.DayHM(S.Clock)} L{S.Loop}C{S.Chapter} · faults={sim.Faults} · roundtrip={(same ? "IDENTICAL" : "DIFF")}");
        Conscience.Enabled = true; HousePush.Enabled = true;
        return sim.Faults == 0 && same ? 0 : 1;
    }
}
