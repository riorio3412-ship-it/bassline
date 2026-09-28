using System;
using System.Linq;
using BL23.Sim;

public static partial class Program
{
    /// <summary>
    /// The three-witness rule (user 2026-09-27: "시신은 세명 이상이 봐야지만 수사가 시작돼"): for every announced death, list who saw
    /// the body before the house tolled, and fail if an announcement came with fewer than the required witnesses (unless it was
    /// the rare "unfound for a full day" fallback). Usage: witness &lt;seed&gt; [days]
    /// </summary>
    static int WitnessTest(string[] args)
    {
        ulong seed = args.Length > 1 ? ulong.Parse(args[1]) : 20260926UL;
        int days = args.Length > 2 ? int.Parse(args[2]) : 4;
        var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
        double end = S.Clock + days * 1440; long ticks = 0; int fails = 0, tolls = 0;
        while (S.Clock < end && ticks < 30_000_000)
        {
            if (S.Phase == Phase.Trial) { TrialSystem.RunHeadless(sim, true); continue; }
            sim.Step(); ticks++;
        }
        foreach (var toll in S.Ledger.Where(e => e.Type == "HouseToll"))
        {
            var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == toll.Target && i.Loop == S.Loop) ?? S.Incidents.Values.FirstOrDefault(i => i.Victim == toll.Target);
            if (inc == null) continue;
            var seenBefore = S.Ledger.Where(e => e.Type == "BodySeen" && e.Target == toll.Target && e.Seq < toll.Seq && e.Data == "dead" && S.A(e.Actor) is Actor x && !x.IsButler)
                                     .Select(e => e.Actor).Distinct().ToList();
            bool unfound = toll.Data == "unfound";
            if (unfound) continue;   // the Minute() log line that precedes a forced toll; the forced HouseConfirm logs its own toll
            tolls++;
            bool forced = S.Ledger.Any(e => e.Type == "HouseToll" && e.Target == toll.Target && e.Data == "unfound" && e.Seq < toll.Seq);
            string mark = seenBefore.Count >= Cases.WitnessRule || forced ? "ok" : "FAIL";
            if (mark == "FAIL") fails++;
            Console.WriteLine($"toll {Cast.NameOf(toll.Target)} at {ClockFmt.Mark(toll.Clock, true)}: witnesses {seenBefore.Count} [{string.Join(",", seenBefore.Select(Cast.GivenOf))}]{(forced ? " (unfound fallback)" : "")} → {mark}");
        }
        var gathers = S.Ledger.Count(e => e.Type == "Gather");
        Console.WriteLine($"tolls={tolls} gathers={gathers} fails={fails}");
        return fails == 0 ? 0 : 1;
    }
}
