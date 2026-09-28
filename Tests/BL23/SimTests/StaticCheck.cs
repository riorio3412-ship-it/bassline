using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BL23.Sim;

/// <summary>
/// staticcheck &lt;seedA&gt; &lt;seedB&gt; [days] — does a game depend on what ran before it in the same process? Runs seed B alone, then
/// seed A followed by seed B, and compares B's two runs minute by minute (ledger sequence, everyone's room and position).
/// Prints the first minute they part and the ledger around it in both runs: kernel state kept in a static field survives
/// into the next game (a second game in one session, a save loaded after another game) and would break determinism.
/// </summary>
public static partial class Program
{
    static int StaticCheck(string[] args)
    {
        ulong a = args.Length > 1 ? ulong.Parse(args[1]) : 1, b = args.Length > 2 ? ulong.Parse(args[2]) : 2;
        int days = args.Length > 3 ? int.Parse(args[3]) : 3;
        var alone = Trace(b, days, out var simAlone);
        Trace(a, days, out _);
        var after = Trace(b, days, out var simAfter);
        int n = Math.Min(alone.Count, after.Count);
        for (int i = 0; i < n; i++)
        {
            if (alone[i].sig == after[i].sig) continue;
            Console.WriteLine($"seed {b}: parts at {ClockFmt.DayHM(alone[i].clock)} (minute {i}) after running seed {a} first");
            Console.WriteLine("  alone: " + alone[i].sig);
            Console.WriteLine("  after: " + after[i].sig);
            long from = Math.Max(0, Math.Min(alone[i].seq, after[i].seq) - 25);
            void Dump(string title, Simulation sim, long seq0, long seq1)
            {
                Console.WriteLine($"  -- {title}: ledger {seq0}..{seq1}");
                foreach (var e in sim.S.Ledger.Where(e => e.Seq >= seq0 && e.Seq <= seq1).OrderBy(e => e.Seq)) Console.WriteLine($"     {e.Seq} {ClockFmt.DayHM(e.Clock)} {e.Type} {e.Actor} {e.Target} r{e.Room} {e.Data}");
            }
            Dump("alone", simAlone, from, alone[i].seq + 3); Dump("after", simAfter, from, after[i].seq + 3);
            return 1;
        }
        Console.WriteLine($"seed {b}: identical over {n} minutes whether or not seed {a} ran first");
        return 0;
    }

    static List<(double clock, long seq, string sig)> Trace(ulong seed, int days, out Simulation sim)
    {
        sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
        var res = new List<(double, long, string)>(); int last = -1; long ticks = 0;
        while (S.Day <= days && ticks < 30_000_000)
        {
            sim.Step(); ticks++;
            if (S.Out.Count > 2000) S.Out.Clear();
            int m = (int)Math.Floor(S.Clock); if (m == last) continue; last = m;
            var sb = new StringBuilder(); sb.Append(S.Seq).Append('|');
            foreach (var x in S.Actors.Values.OrderBy(x => x.Id, StringComparer.Ordinal)) sb.Append(x.Id).Append(':').Append(x.Room).Append('@').Append(Math.Round(x.Pos.x, 1)).Append(',').Append(Math.Round(x.Pos.z, 1)).Append(' ');
            res.Add((S.Clock, S.Seq, sb.ToString()));
        }
        return res;
    }
}
