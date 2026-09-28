using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BL23.Sim;

/// <summary>
/// tickcost [days=2] [seed,seed,…] [nostage] — what one kernel tick costs (compare before/after back to back on the same
/// machine; allocations and GC counts are machine-independent). For each seed a fresh campaign runs `days` clock days the
/// way the Unity session drives it (Headless off so sounds are emitted, S.Out drained after every tick, investigation and
/// trial handled like the campaign test) and every Simulation.Step is timed, its allocations counted
/// (GC.GetAllocatedBytesForCurrentThread) and ticks during which a GC ran are marked. A second pass runs the same seed
/// through StepStaged (a stopwatch + allocation lap per stage of Step) — its final save must be IDENTICAL to the first
/// pass (this also proves two fresh runs are deterministic and that the staged mirror still matches Simulation.Step).
/// Note: .NET RyuJIT here; Unity's Mono JIT is slower (expect roughly 1.5–3x the ms) and its Boehm GC is non-generational,
/// so the allocation volume matters more there than the gen0 count suggests.
/// </summary>
public static partial class Program
{
    static readonly string[] TcStages = { "schedule", "bodies", "facilities", "grammars", "think", "execute", "crowd", "follow", "perception", "convos", "crime", "cases", "replay" };

    sealed class TcRun
    {
        public ulong Seed; public bool Staged; public string Json; public int Faults, Trials; public long Frozen;
        public readonly List<double> Ms = new List<double>(64000);
        public readonly List<long> Alloc = new List<long>(64000);
        public readonly List<Phase> Ph = new List<Phase>(64000);
        public readonly List<double> Clock = new List<double>(64000);
        public readonly List<byte> Gc = new List<byte>(64000);           // 0 none, else 1 + the highest generation collected during the tick
        // staged pass: [0,N) time, [N,2N) bytes (running totals); per-stage worst tick; the stage behind every big allocation
        public readonly long[] Acc = new long[TcStages.Length * 2];
        public readonly long[] StageMax = new long[TcStages.Length]; public readonly double[] StageMaxAt = new double[TcStages.Length];
        public readonly List<(double clock, int stage, long bytes)> BigAllocs = new List<(double, int, long)>();
        public int Gc0, Gc1, Gc2; public double WallMs;
    }

    static int TickCost(string[] args)
    {
        int days = args.Length > 1 && int.TryParse(args[1], out var dd) ? dd : 2;
        var seeds = args.Length > 2 && args[2].Split(',').All(x => ulong.TryParse(x, out _))
            ? args[2].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(ulong.Parse).ToArray()
            : new[] { 20260926UL, 777UL, 4242UL };
        bool staged = !args.Contains("nostage");
        Console.WriteLine($"tickcost: {seeds.Length} seeds x {days} days, staged pass {(staged ? "on" : "off")}, runtime {Environment.Version}, " +
                          $"stopwatch {Stopwatch.Frequency / 1e6:0.#} MHz, server GC {System.Runtime.GCSettings.IsServerGC}, 1 tick = {SimTime.Dt:0.0} sim s");
        // JIT / tiering warm-up (untimed): a whole day of another campaign, plain and staged, so every daily code path is hot
        { var w = TcRunOne(1UL, 1, false, true); var w2 = TcRunOne(2UL, 1, true, true); Console.WriteLine($"warm-up done ({(w.WallMs + w2.WallMs) / 1000:0.0}s)"); }

        var rows = new List<string>(); var all = new List<TcRun>();
        foreach (var seed in seeds)
        {
            var real = TcRunOne(seed, days, false); all.Add(real);
            TcRun stg = null; if (staged) { stg = TcRunOne(seed, days, true); }
            TcReport(real, stg);
            rows.Add(TcRow(real, stg));
        }
        // the pooled line (all seeds)
        {
            var ms = all.SelectMany(r => r.Ms).OrderBy(x => x).ToList(); var al = all.SelectMany(r => r.Alloc).OrderBy(x => x).ToList();
            var noGc = all.SelectMany(r => Enumerable.Range(0, r.Ms.Count).Where(i => r.Gc[i] == 0).Select(i => r.Ms[i])).ToList();
            Console.WriteLine();
            Console.WriteLine("| seed | live ticks | ms/tick avg | p50 | p95 | p99 | max | max w/o GC | alloc B/tick avg | p50 | p95 | MB total | ticks with GC (gen0/1/2) | faults | staged save |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in rows) Console.WriteLine(r);
            Console.WriteLine($"| all | {ms.Count} | {ms.Average():0.000} | {Pct(ms, 0.5):0.000} | {Pct(ms, 0.95):0.000} | {Pct(ms, 0.99):0.000} | {ms.Last():0.00} | {(noGc.Count > 0 ? noGc.Max() : 0):0.00} | {al.Average():0} | {Pct(al, 0.5)} | {Pct(al, 0.95)} | {al.Sum() / 1048576.0:0} | {all.Sum(r => r.Gc0)}/{all.Sum(r => r.Gc1)}/{all.Sum(r => r.Gc2)} | {all.Sum(r => r.Faults)} | |");
            double avg = ms.Average();
            Console.WriteLine($"budget: {25.0 / avg:0} ticks fit a 25 ms frame budget → a 1-hour skip (1200 ticks) costs {avg * 1200:0} ms of kernel time and allocates {al.Average() * 1200 / 1048576.0:0.0} MB here (x1.5–3 the ms under Mono)");
        }
        return 0;
    }

    static double Pct(List<double> s, double p) => s.Count == 0 ? 0 : s[Math.Min(s.Count - 1, (int)(s.Count * p))];
    static long Pct(List<long> s, double p) => s.Count == 0 ? 0 : s[Math.Min(s.Count - 1, (int)(s.Count * p))];

    static TcRun TcRunOne(ulong seed, int days, bool staged, bool warm = false)
    {
        var r = new TcRun { Seed = seed, Staged = staged };
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var sim = Simulation.NewCampaign(seed, 4);
        sim.Headless = false;     // the Unity session: sounds are emitted (and drained below)
        sim.OnDemand = false;     // continuous house (the probe's flow; OnDemand steps the same ticks in bursts)
        var S = sim.S; S.Phase = Phase.Daily;
        double end = S.Clock + days * 1440.0; long guard = 0; int N = TcStages.Length; var prev = new long[N * 2];
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
        var wall = Stopwatch.StartNew(); double toMs = 1000.0 / Stopwatch.Frequency;
        while (S.Clock < end && guard++ < 2_000_000)
        {
            if (S.Phase == Phase.Trial)
            {
                r.Trials++;
                TrialSystem.RunHeadless(sim, true);
                Replay.BuildSegments(sim);
                Settlements.AfterReveal(sim);
                if (S.Phase == Phase.LoopEpilogue) { Settlements.NextLoop(sim); S.Phase = Phase.Daily; }
                continue;
            }
            if (S.Phase == Phase.Investigation && !S.Flags.ContainsKey("testinv:" + S.Chapter + ":" + S.Loop))
            {
                // the test player investigates like the campaign test (bodies, scene traces, everyone's whereabouts)
                S.Flags["testinv:" + S.Chapter + ":" + S.Loop] = 1;
                foreach (var inc in S.Incidents.Values.Where(i => i.Confirmed && i.Chapter == S.Chapter && i.Loop == S.Loop).ToList())
                {
                    var body = S.A(inc.Victim); if (body == null) continue; Evidences.ExamineBody(sim, S.Player, body, true);
                    foreach (var t in S.Traces.Where(t => t.Room == body.Room || t.Room == inc.CauseRoom).ToList()) Evidences.ExamineTrace(sim, S.Player, t);
                }
                foreach (var npc in S.LivingNpcs.ToList()) { var w = Testimony.Where(sim, npc, Cast.Player); if (w.Prop != null) sim.Learn(Cast.Player, npc.Id, w.Prop, "(test)", w.Lie, false); foreach (var s in Testimony.Saw(sim, npc, Cast.Player, 2)) if (s.Prop != null) sim.Learn(Cast.Player, npc.Id, s.Prop, "(test)", s.Lie, false); }
            }
            var ph = S.Phase; double clk = S.Clock;
            bool frozen = ph == Phase.Verdict || ph == Phase.Execution || ph == Phase.Reveal || ph == Phase.Settlement || ph == Phase.LoopEpilogue || ph == Phase.Boot;
            if (staged) Array.Copy(r.Acc, prev, prev.Length);
            int c0 = GC.CollectionCount(0), c1 = GC.CollectionCount(1), c2 = GC.CollectionCount(2);
            long a0 = GC.GetAllocatedBytesForCurrentThread(); long t0 = Stopwatch.GetTimestamp();
            if (staged) sim.StepStaged(r.Acc); else sim.Step();
            long t1 = Stopwatch.GetTimestamp(); long a1 = GC.GetAllocatedBytesForCurrentThread();
            byte gc = (byte)(GC.CollectionCount(2) != c2 ? 3 : GC.CollectionCount(1) != c1 ? 2 : GC.CollectionCount(0) != c0 ? 1 : 0);
            S.Out.Clear();   // the presenter drains the events every frame
            if (frozen) { r.Frozen++; continue; }
            r.Ms.Add((t1 - t0) * toMs); r.Alloc.Add(a1 - a0); r.Ph.Add(ph); r.Clock.Add(clk); r.Gc.Add(gc);
            if (staged)
            {
                int bigK = -1; long bigB = 0;
                for (int k = 0; k < N; k++)
                {
                    long dt = r.Acc[k] - prev[k]; if (dt > r.StageMax[k]) { r.StageMax[k] = dt; r.StageMaxAt[k] = clk; }
                    long db = r.Acc[N + k] - prev[N + k]; if (db > bigB) { bigB = db; bigK = k; }
                }
                if (bigB >= 256 * 1024) r.BigAllocs.Add((clk, bigK, bigB));
            }
        }
        r.WallMs = wall.Elapsed.TotalMilliseconds;
        r.Gc0 = GC.CollectionCount(0) - g0; r.Gc1 = GC.CollectionCount(1) - g1; r.Gc2 = GC.CollectionCount(2) - g2;
        r.Faults = sim.Faults; if (!warm) r.Json = SaveStore.Serialize(S);
        return r;
    }

    static string TcRow(TcRun r, TcRun stg)
    {
        var s = r.Ms.OrderBy(x => x).ToList(); var a = r.Alloc.OrderBy(x => x).ToList();
        var noGc = Enumerable.Range(0, r.Ms.Count).Where(i => r.Gc[i] == 0).Select(i => r.Ms[i]).ToList();
        string same = stg == null ? "-" : r.Json == stg.Json ? "IDENTICAL" : "DIFF";
        int k0 = r.Gc.Count(g => g == 1), k1 = r.Gc.Count(g => g == 2), k2 = r.Gc.Count(g => g == 3);
        return $"| {r.Seed} | {s.Count} | {s.Average():0.000} | {Pct(s, 0.5):0.000} | {Pct(s, 0.95):0.000} | {Pct(s, 0.99):0.000} | {s.Last():0.00} | {(noGc.Count > 0 ? noGc.Max() : 0):0.00} | {a.Average():0} | {Pct(a, 0.5)} | {Pct(a, 0.95)} | {a.Sum() / 1048576.0:0} | {k0}/{k1}/{k2} | {r.Faults} | {same} |";
    }

    static void TcReport(TcRun r, TcRun stg)
    {
        var s = r.Ms.OrderBy(x => x).ToList(); var al = r.Alloc.OrderBy(x => x).ToList();
        var gcT = Enumerable.Range(0, r.Ms.Count).Where(i => r.Gc[i] != 0).ToList();
        Console.WriteLine();
        Console.WriteLine($"== seed {r.Seed}: {s.Count} live ticks (+{r.Frozen} frozen), trials {r.Trials}, faults {r.Faults}, wall {r.WallMs / 1000:0.0}s (incl. trials)");
        Console.WriteLine($"   ms/tick avg {s.Average():0.000} p50 {Pct(s, 0.5):0.000} p95 {Pct(s, 0.95):0.000} p99 {Pct(s, 0.99):0.000} max {s.Last():0.00} | total step time {s.Sum() / 1000:0.00}s");
        Console.WriteLine($"   alloc/tick avg {al.Average():0} B p50 {Pct(al, 0.5)} p95 {Pct(al, 0.95)} max {al.Last()} | total {al.Sum() / 1048576.0:0.0} MB | GCs gen0 {r.Gc0} gen1 {r.Gc1} gen2 {r.Gc2} (whole loop)");
        if (gcT.Count > 0) Console.WriteLine($"   ticks with a GC: {gcT.Count} (gen2 {r.Gc.Count(g => g == 3)}), their avg {gcT.Average(i => r.Ms[i]):0.00} ms max {gcT.Max(i => r.Ms[i]):0.00} ms | without: max {Enumerable.Range(0, r.Ms.Count).Where(i => r.Gc[i] == 0).Select(i => r.Ms[i]).DefaultIfEmpty(0).Max():0.00} ms");
        foreach (var g in Enumerable.Range(0, r.Ms.Count).GroupBy(i => r.Ph[i]).OrderBy(g => g.Key))
        {
            var m = g.Select(i => r.Ms[i]).OrderBy(x => x).ToList(); var b = g.Select(i => r.Alloc[i]).ToList();
            Console.WriteLine($"   phase {g.Key,-13} ticks {m.Count,6}  avg {m.Average():0.000} p95 {Pct(m, 0.95):0.000} max {m.Last():0.00} ms  alloc avg {b.Average():0} B");
        }
        // by clock hour of day (daily only): where the house is busy
        var hours = new StringBuilder("   daily ms/tick by hour:");
        foreach (var g in Enumerable.Range(0, r.Ms.Count).Where(i => r.Ph[i] == Phase.Daily).GroupBy(i => (int)(r.Clock[i] % 1440) / 60).OrderBy(g => g.Key))
            hours.Append($" {g.Key:00}h={g.Average(i => r.Ms[i]):0.00}");
        Console.WriteLine(hours.ToString());
        Console.WriteLine("   slowest ticks: " + string.Join(", ", Enumerable.Range(0, r.Ms.Count).OrderByDescending(i => r.Ms[i]).Take(6).Select(i => $"{r.Ms[i]:0.0}ms@{ClockFmt.DayHM(r.Clock[i])}/{r.Ph[i]}{(r.Gc[i] != 0 ? "/GC" + (r.Gc[i] - 1) : "")}")));
        Console.WriteLine("   biggest allocating ticks: " + string.Join(", ", Enumerable.Range(0, r.Ms.Count).OrderByDescending(i => r.Alloc[i]).Take(5).Select(i => $"{r.Alloc[i] / 1024}KB@{ClockFmt.DayHM(r.Clock[i])}")));
        if (stg != null)
        {
            double toMs = 1000.0 / Stopwatch.Frequency; int N = TcStages.Length; double tot = 0; long totB = 0; for (int k = 0; k < N; k++) { tot += stg.Acc[k] * toMs; totB += stg.Acc[N + k]; }
            int n = Math.Max(1, stg.Ms.Count);
            Console.WriteLine($"   staged pass: avg {stg.Ms.Average():0.000} ms/tick, alloc avg {stg.Alloc.Average():0} B/tick; final save vs plain pass: {(stg.Json == r.Json ? "IDENTICAL" : "DIFF (StepStaged no longer mirrors Simulation.Step, or the kernel is nondeterministic)")}");
            Console.WriteLine("   stage          µs/tick  time%   B/tick  alloc%   worst tick");
            foreach (var k in Enumerable.Range(0, N).OrderByDescending(k => stg.Acc[k]))
                Console.WriteLine($"   {TcStages[k],-12} {stg.Acc[k] * toMs * 1000 / n,8:0.0} {100 * stg.Acc[k] * toMs / Math.Max(1e-9, tot),6:0.0}% {stg.Acc[N + k] / (double)n,8:0} {100.0 * stg.Acc[N + k] / Math.Max(1, totB),6:0.0}%   {stg.StageMax[k] * toMs:0.00}ms@{ClockFmt.DayHM(stg.StageMaxAt[k])}");
            if (stg.BigAllocs.Count > 0)
                Console.WriteLine($"   ticks allocating ≥256 KB in one stage: {stg.BigAllocs.Count} — " + string.Join(", ", stg.BigAllocs.GroupBy(x => x.stage).OrderByDescending(g => g.Sum(x => x.bytes)).Select(g => $"{TcStages[g.Key]} x{g.Count()} ({g.Sum(x => x.bytes) / 1048576.0:0.0} MB, e.g. {ClockFmt.DayHM(g.OrderByDescending(x => x.bytes).First().clock)} {g.Max(x => x.bytes) / 1024} KB)")));
        }
    }
}

namespace BL23.Sim
{
    public sealed partial class Simulation
    {
        /// <summary>Tests only (SimTests/TickCost.cs): Simulation.Step with a stopwatch + allocation lap per stage into
        /// acc[0..N) (ticks) and acc[N..2N) (bytes), in the order of Program.TcStages. Must stay a faithful copy of Step —
        /// TickCost compares the final save against a plain run and reports DIFF when it drifts.</summary>
        internal void StepStaged(long[] acc)
        {
            var S = this.S;
            if (S.Phase == Phase.Trial || S.Phase == Phase.Verdict || S.Phase == Phase.Execution || S.Phase == Phase.Reveal || S.Phase == Phase.Settlement || S.Phase == Phase.LoopEpilogue || S.Phase == Phase.Boot)
            {
                S.Tick++; return;
            }
            S.Tick++;
            S.Clock += S.ClockRate * SimTime.Dt;
            int N = acc.Length / 2;
            long t = System.Diagnostics.Stopwatch.GetTimestamp(), b = GC.GetAllocatedBytesForCurrentThread();
            void Lap(int k) { long n = System.Diagnostics.Stopwatch.GetTimestamp(), m = GC.GetAllocatedBytesForCurrentThread(); acc[k] += n - t; acc[N + k] += m - b; t = n; b = m; }
            Guard("schedule", Schedule); Lap(0);
            Guard("bodies", Bodies); Lap(1);
            Guard("facilities", Facilities); Lap(2);
            Guard("grammars", () => Grammars.Tick(this)); Lap(3);
            foreach (var a in S.Actors.Values)
            {
                if (a.IsPlayer || !a.Alive) continue;
                if (a.Status == ActorStatus.Unconscious) continue;
                try
                {
                    if (S.Clock >= a.NextThink && (a.Act == null || a.Act.Interruptible || a.Act.Cur == null)) Think(a);
                    Lap(4);
                    Execute(a);
                    Lap(5);
                }
                catch (Exception e) { Fault("actor " + a.Id + " " + (a.Act?.Id ?? "-") + "/" + (a.Act?.Cur?.Kind ?? "-"), e); a.Act = null; a.Speed = 0; a.NextThink = S.Clock + 1; Lap(5); }
            }
            Guard("crowd", Crowd); Lap(6);
            Guard("follow", FollowUpdate); Lap(7);
            Guard("perception", Perception); Lap(8);
            Guard("convos", Conversations); Lap(9);
            Guard("crime", () => Crime.Update(this)); Lap(10);
            Guard("cases", () => Cases.Update(this)); Lap(11);
            if (S.Tick % 5 == 0) Guard("replay", () => Replay.Record(this));
            Lap(12);
        }
    }
}
