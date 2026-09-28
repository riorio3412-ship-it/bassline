using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;

/// <summary>
/// "voice [days] [seed,seed,…]"  ("voice lint [outFile]" = VoiceLint.cs) — how varied and how personal the spoken barks are (DailyLifeDesign §11).
/// Plays headless campaigns (default seeds 20260926, 777, 4242 for 3 days each) and reads every Speech event the kernel emits.
///   REPEAT  per resident per day, the most times one exact ambient line was said (small_talk, talk_like, busy, doing_act,
///           meal, greet*, gathering_chat, talk_mansion). Target ≤ 3.
///   SHARED  share of NPC speech rendered from a shared (ANY) template instead of the speaker's own voice. Target &lt; 5%.
///   NAMED   lines per day that name another resident.
///   TIERS   which resolver tier produced each line (pair @, about #, situation ~, own, shared) — when LineContext is present.
/// Measurement only: it never changes the campaign.
/// </summary>
public static partial class Program
{
    static readonly HashSet<string> VoTestimony = new HashSet<string> { "alibi_where", "alibi_with", "saw_person", "saw_person_unsure", "saw_item", "saw_nothing", "heard_sound", "heard_voice", "saw_handover", "gossip_saw", "suspect", "share_find", "keep_find", "refuse_answer", "grief", "grief_close", "discover_shock", "scream_discover", "claim_alibi", "claim_saw", "claim_heard", "courier_confess" };
    static readonly HashSet<string> VoAmbient = new HashSet<string> { "small_talk", "talk_like", "busy", "doing_act", "meal", "greet", "greet_morning", "greet_night", "greet_close", "greet_cold", "gathering_chat", "talk_mansion" };

    static int VoiceTest(string[] args)
    {
        if (args.Length > 1 && args[1] == "lint") return VoiceLintTest(args.Skip(1).ToArray());   // "voice lint [outFile]"
        int days = args.Length > 1 ? int.Parse(args[1]) : 3;
        var seeds = args.Length > 2 ? args[2].Split(',').Select(ulong.Parse).ToArray() : new[] { 20260926UL, 777UL, 4242UL };
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // resolver trace (present only once LineContext exists): speaker, key, resolved key, tier
        var traces = new List<(string sp, string key, string tier)>();
        var lc = typeof(LineBank).Assembly.GetType("BL23.Sim.LineContext");
        var traceField = lc?.GetField("Trace", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (traceField != null)
        {
            Action<string, string, string, int> tr = (sp, key, resolved, tier) => traces.Add((sp, key, tier == 0 ? "pair" : tier == 1 ? "about" : tier == 2 ? "situation" : tier == 3 ? "own" : "shared"));
            traceField.SetValue(null, tr);
        }

        long total = 0, shared = 0, named = 0, namedSocial = 0, social = 0; int dayCount = 0;
        int maxFlags = 0, maxLr = 0;
        var perRun = new Dictionary<string, Dictionary<string, int>>();   // seed|speaker → ambient text → n over the whole run
        var byKey = new Dictionary<string, int>(); var sharedByKey = new Dictionary<string, int>(); var tiers = new Dictionary<string, int>();
        var perDay = new Dictionary<string, Dictionary<string, int>>();   // seed|day|speaker → text → n (ambient only)
        var stDistinct = new Dictionary<string, HashSet<string>>(); var stTotal = new Dictionary<string, int>();
        var givens = Cast.Participants.Select(c => (c.Id, c.Given)).ToList();

        foreach (var seed in seeds)
        {
            var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
            double endClock = days * 1440; long ticks = 0; int loops = 1;
            dayCount += days;
            while (S.Clock < endClock && ticks < 20_000_000)
            {
                if (S.Phase == Phase.Trial)
                {
                    TrialSystem.RunHeadless(sim, true); Replay.BuildSegments(sim); Settlements.AfterReveal(sim);
                    if (S.Phase == Phase.LoopEpilogue) { Settlements.NextLoop(sim); S.Phase = Phase.Daily; loops++; }
                    if (S.Phase == Phase.Trial) break;
                    S.Out?.Clear(); continue;
                }
                traces.Clear();
                if (ticks % 3000 == 0) { maxFlags = Math.Max(maxFlags, S.Flags.Count); maxLr = Math.Max(maxLr, S.Flags.Keys.Count(k => k.StartsWith("lr:"))); }
                sim.Step(); ticks++;
                if (S.Out == null) continue;
                foreach (var e in S.Out)
                {
                    if (e.Type != GameEventType.Speech || string.IsNullOrEmpty(e.Text) || e.Actor == null || e.Actor == Cast.Butler || e.Actor == Cast.Player || e.Key == "bond") continue;
                    string key = e.Key ?? "-"; total++;
                    byKey[key] = (byKey.TryGetValue(key, out var bk) ? bk : 0) + 1;
                    bool isShared; int ti = traces.FindIndex(x => x.sp == e.Actor && x.key == key);
                    if (ti >= 0) { var tier = traces[ti].tier; traces.RemoveAt(ti); isShared = tier == "shared"; tiers[tier] = (tiers.TryGetValue(tier, out var tn) ? tn : 0) + 1; }
                    else isShared = !LineBank.Has(e.Actor, key) && LineBank.Has("ANY", key);
                    if (isShared) { shared++; sharedByKey[key] = (sharedByKey.TryGetValue(key, out var sk) ? sk : 0) + 1; }
                    string given = Cast.GivenOf(e.Actor);
                    bool nm = givens.Any(g => g.Id != e.Actor && g.Id != Cast.Player && e.Text.Contains(g.Given)); if (nm) named++;
                    if (!VoTestimony.Contains(key)) { social++; if (nm) namedSocial++; }
                    if (VoAmbient.Contains(key))
                    {
                        string dk = seed + "|" + ClockFmt.Day(e.Clock) + "|" + given;
                        if (!perDay.TryGetValue(dk, out var m)) perDay[dk] = m = new Dictionary<string, int>(StringComparer.Ordinal);
                        m[e.Text] = (m.TryGetValue(e.Text, out var c0) ? c0 : 0) + 1;
                        string rk = seed + "|" + given; if (!perRun.TryGetValue(rk, out var mr)) perRun[rk] = mr = new Dictionary<string, int>(StringComparer.Ordinal); mr[e.Text] = (mr.TryGetValue(e.Text, out var c1) ? c1 : 0) + 1;
                    }
                    if (key == "small_talk")
                    {
                        if (!stDistinct.TryGetValue(given, out var hs)) stDistinct[given] = hs = new HashSet<string>(StringComparer.Ordinal);
                        hs.Add(e.Text); stTotal[given] = (stTotal.TryGetValue(given, out var t0) ? t0 : 0) + 1;
                    }
                }
                S.Out.Clear();
            }
            Console.WriteLine($"  seed {seed}: ticks={ticks} loops={loops} clock={ClockFmt.DayHM(S.Clock)} faults={sim.Faults} flags(max)={maxFlags} of which lr:={maxLr} t={sw.ElapsedMilliseconds}ms");
        }
        if (traceField != null) traceField.SetValue(null, null);

        var maxes = perDay.Select(kv => (kv.Key, max: kv.Value.Values.Max(), top: kv.Value.OrderByDescending(x => x.Value).First().Key)).ToList();
        int over = maxes.Count(x => x.max > 3);
        Console.WriteLine($"VOICE seeds={string.Join(",", seeds)} days={days}  speech={total}");
        Console.WriteLine($"  REPEAT max={maxes.Select(x => x.max).DefaultIfEmpty(0).Max()} mean-of-daily-max={maxes.Select(x => (double)x.max).DefaultIfEmpty(0).Average():0.00} resident-days>3={over}/{maxes.Count}");
        foreach (var w in maxes.OrderByDescending(x => x.max).Take(5)) Console.WriteLine($"     {w.Key} ×{w.max}: {w.top}");
        Console.WriteLine($"  SHARED {shared}/{total} = {100.0 * shared / Math.Max(1, total):0.0}%   top: " + string.Join(", ", sharedByKey.OrderByDescending(x => x.Value).Take(10).Select(x => x.Key + "=" + x.Value)));
        Console.WriteLine($"  NAMED {named} lines ({named / (double)Math.Max(1, dayCount):0.0}/day); in daily-life talk (no testimony keys) {namedSocial}/{social} = {100.0 * namedSocial / Math.Max(1, social):0.0}% ({namedSocial / (double)Math.Max(1, dayCount):0.0}/day)");
        { var runMax = perRun.Select(kv => kv.Value.Values.Max()).DefaultIfEmpty(0).ToList(); Console.WriteLine($"  REPEAT-3DAY per resident per seed: max={runMax.Max()} mean={runMax.Select(x => (double)x).Average():0.00}"); }
        if (tiers.Count > 0) Console.WriteLine("  TIERS " + string.Join(", ", tiers.OrderByDescending(x => x.Value).Select(x => $"{x.Key}={x.Value} ({100.0 * x.Value / Math.Max(1, total):0.0}%)")));
        Console.WriteLine("  small_talk distinct/total: " + string.Join(", ", stTotal.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key} {stDistinct[x.Key].Count}/{x.Value}")));
        Console.WriteLine("  keys: " + string.Join(", ", byKey.OrderByDescending(x => x.Value).Take(24).Select(x => x.Key + "=" + x.Value)));
        Console.WriteLine($"voice done t={sw.ElapsedMilliseconds}ms");
        return 0;
    }
}
