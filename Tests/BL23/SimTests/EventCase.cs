using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;

/// <summary>
/// eventcase &lt;from&gt; &lt;to&gt; [days] [kind] — the 심판 of a murder staged at one of the house's evenings (HouseEvents), played
/// headless the way "debate" plays the first one. Seeds run with 민혁 active (the premise lab's player); every court opens
/// as a debate, and the ones for other murders are settled headless. The first case whose scheme used a house evening
/// (kind: masque · banquet · hunt · stars · vigil · puppet · aquarium · contract · phone, or any) is printed — the deck, then the transcript and the verdict.
/// </summary>
public static partial class Program
{
    static int EventCase(string[] args)
    {
        ulong from = args.Length > 1 ? ulong.Parse(args[1]) : 1, to = args.Length > 2 ? ulong.Parse(args[2]) : 40;
        int days = args.Length > 3 && int.TryParse(args[3], out var dd) ? dd : 9;
        string want = args.Length > 4 ? args[4] : "any";
        bool debate = TrialSystem.DebateEnabled; TrialSystem.DebateEnabled = true;
        try
        {
            for (ulong seed = from; seed <= to; seed++)
            {
                var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
                var prng = new Rng(seed ^ 0x9E3779B9UL, 11); long ticks = 0; int lastMin = -1; string invKey = null;
                while (S.Day <= days && ticks < 30_000_000)
                {
                    if (S.Phase == Phase.Trial && S.Trial != null)
                    {
                        var T = S.Trial; var D = T.Debate;
                        var inc = D != null && S.Incidents.TryGetValue(D.Incident, out var ii) ? ii : null;
                        var sc = inc?.PlanId != null ? Initiative.ByPlan(S, inc.PlanId) : null;
                        string kind = HouseKindOf(S, sc, inc);
                        if (D != null && kind != null && (want == "any" || kind == want))
                        {
                            Console.WriteLine($"==== seed {seed} · {kind} · {ClockFmt.DayHM(S.Clock)} L{S.Loop}C{S.Chapter}");
                            Console.WriteLine(DeckText(sim, Debate.Facts(sim, inc), D.Deck));
                            var trace = new List<string>(); TrialSystem.DebateTraceLog = trace;
                            try { TrialSystem.DebateHeadless(sim, "smart"); } finally { TrialSystem.DebateTraceLog = null; }
                            var at = trace.Select(x => { int i = x.IndexOf('|'); return (n: int.Parse(x.Substring(0, i)), what: x.Substring(i + 1)); }).ToList();
                            for (int i = 0; i <= T.Beats.Count; i++)
                            {
                                foreach (var a in at.Where(a => a.n == i)) Console.WriteLine($"          ▶ {a.what}");
                                if (i == T.Beats.Count) break;
                                var b = T.Beats[i]; string who = b.Speaker == null ? "" : Cast.GivenOf(b.Speaker) ?? b.Speaker;
                                string tag = b.Kind == "line" ? "" : $"[{b.Kind}{(b.Data != null && b.Kind != "vote" ? ":" + b.Data : "")}] ";
                                if (!string.IsNullOrEmpty(b.Text)) Console.WriteLine($"  {i,3} {tag}{(who.Length > 0 ? who + ": " : "")}{b.Text}");
                            }
                            Console.WriteLine($"== verdict: accused {Cast.GivenOf(T.Accused)} culprit {Cast.GivenOf(D.Target)} correct {D.Correct}");
                            return 0;
                        }
                        if (D != null) TrialSystem.DebateHeadless(sim, "smart"); else TrialSystem.RunHeadless(sim, true);
                        Replay.BuildSegments(sim); Settlements.AfterReveal(sim);
                        if (S.Phase == Phase.LoopEpilogue) { Settlements.NextLoop(sim); S.Phase = Phase.Daily; }
                        if (S.Phase == Phase.Trial) break;
                        continue;
                    }
                    if (S.Phase == Phase.Investigation && invKey != S.Loop + ":" + S.Chapter) { invKey = S.Loop + ":" + S.Chapter; AuditInvestigate(sim); }
                    sim.Step(); ticks++;
                    int mm = (int)Math.Floor(S.Clock); if (mm != lastMin) { lastMin = mm; PremisePlayer(sim, prng, _ => { }, S.Minute); }
                    if (S.Out.Count > 2000) S.Out.Clear();
                }
                Console.WriteLine($"seed {seed}: no {want} case by day {days}");
            }
        }
        finally { TrialSystem.DebateEnabled = debate; }
        return 1;
    }

    /// <summary>The house evening a murder's scheme used (masque · banquet · hunt · stars · vigil · puppet · aquarium · contract ·
    /// phone), or null.</summary>
    static string HouseKindOf(GameState S, Scheme sc, Incident inc)
    {
        if (sc == null) return null;
        if (sc.Moment == "hunt") return "hunt";
        if (sc.Moment == "call") return "phone";
        string ek = sc.EventKind ?? (sc.MomentRef != null ? S.Gatherings.FirstOrDefault(g => g.Id == sc.MomentRef)?.Kind : null);
        return ek != null && ek.StartsWith("house:", StringComparison.Ordinal) ? ek.Substring(6) : null;
    }
}
