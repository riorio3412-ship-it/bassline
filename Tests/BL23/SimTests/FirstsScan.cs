using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;

/// <summary>
/// firsts &lt;from&gt; &lt;to&gt; [days] [active] [all] — how varied the loop's first murder is across games (owner: "사건의 다양성").
/// Each seed runs a headless campaign up to its first murder (or the day limit) and prints one line: day and hour, culprit →
/// victim, the scheme's moment and approach, the motive, whom the house chose (HousePush.Chosen), and the house's evenings.
/// active: 민혁 plays the way the premise lab drives him (tables, confidences, the house's evenings) — closer to a real game
/// than an idle 민혁, who never takes the first table's pact. all: run every day (trials included) and tally every murder's
/// moment. The tail counts culprits, moments, approaches, motives, the designs weighed and why plans were revised.
/// Seeds run one after another: the kernel keeps a few lazily filled static tables that are not thread-safe (run several
/// processes over seed ranges in parallel for speed).
/// </summary>
public static partial class Program
{
    static int FirstsScan(string[] args)
    {
        ulong from = args.Length > 1 ? ulong.Parse(args[1]) : 1, to = args.Length > 2 ? ulong.Parse(args[2]) : 40;
        int days = args.Length > 3 && int.TryParse(args[3], out var dd) ? dd : 9; bool active = args.Contains("active"), all = args.Contains("all");
        var seeds = new List<ulong>(); for (ulong s = from; s <= to; s++) seeds.Add(s);
        var rows = new (ulong seed, string line, string culprit, string moment, string approach, string motive)[seeds.Count];
        var designs = new List<string>[seeds.Count]; var revises = new List<string>[seeds.Count]; var allMoments = new List<string>[seeds.Count];
        for (int i = 0; i < seeds.Count; i++)
        {
            ulong seed = seeds[i];
            var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
            long ticks = 0; Incident first = null; var prng = new Rng(seed ^ 0x9E3779B9UL, 11); int lastMin = -1;
            while (ticks < 30_000_000 && S.Day <= days)
            {
                if (all && S.Phase == Phase.Trial)
                {
                    TrialSystem.RunHeadless(sim, true); Replay.BuildSegments(sim); Settlements.AfterReveal(sim);
                    if (S.Phase == Phase.LoopEpilogue) { Settlements.NextLoop(sim); S.Phase = Phase.Daily; }
                    if (S.Phase == Phase.Trial) break;
                    continue;
                }
                sim.Step(); ticks++;
                if (active) { int mm = (int)Math.Floor(S.Clock); if (mm != lastMin) { lastMin = mm; PremisePlayer(sim, prng, _ => { }, S.Minute); } }
                if (S.Out.Count > 2000) S.Out.Clear();
                first = S.Incidents.Values.Where(x => x.Loop == 1 && x.Murder && x.Culprit != null).OrderBy(x => x.ResultSeq).FirstOrDefault();
                if (first != null && !all) break;
            }
            allMoments[i] = S.Incidents.Values.Where(x => x.Murder && x.Culprit != null).OrderBy(x => x.ResultSeq).Select(x => { var q = x.PlanId != null ? Initiative.ByPlan(S, x.PlanId) : null; string ek = q?.EventKind ?? (q?.MomentRef != null ? S.Gatherings.FirstOrDefault(g => g.Id == q.MomentRef)?.Kind : null); return (q?.Moment ?? "improvised") + (ek != null && ek.StartsWith("house:") ? "(" + ek + ")" : ""); }).ToList();
            designs[i] = S.Ledger.Where(e => e.Type == "SchemeDesign").Select(e => { var p = (e.Data ?? "").Split(' '); return p.Length > 1 ? string.Join("/", p[1].Split('/').Take(2)) : "?"; }).ToList();
            revises[i] = S.Ledger.Where(e => e.Type == "SchemeRevise").Select(e => { var d = e.Data ?? ""; int k = d.IndexOf(' '); d = k >= 0 ? d.Substring(k + 1) : d; int c = d.IndexOf(":"); if (d.Contains("중단")) d = d.Substring(d.IndexOf("중단")); return d.Length > 24 ? d.Substring(0, 24) : d; }).ToList();
            string W(string id) => id == null ? "-" : Cast.GivenOf(id) ?? id;
            string evs = string.Join(",", S.Gatherings.Where(g => HouseEvents.IsHouse(g) && g.Revs.Count > 0).Select(g => (HouseEvents.KindOf(g)?.Id ?? g.Kind) + "@" + (int)(g.Cur.Start / 1440 + 1)));
            if (first == null) { rows[i] = (seed, $"{seed,6} | no murder by day {days} · chosen {W(HousePush.Chosen(S))} · events {evs}", "-", "-", "-", "-"); continue; }
            var sc = first.PlanId != null && S.Plans.TryGetValue(first.PlanId, out var pl) ? Initiative.ByPlan(S, pl.Id) : null;
            string moment = sc?.Moment ?? "improvised", ap = sc?.Approach ?? (pl0(S, first)?.Grammar ?? first.Method ?? "-"), motive = sc?.Motive ?? pl0(S, first)?.Motive ?? "-";
            string ev = sc?.EventKind ?? (sc?.MomentRef != null ? S.Gatherings.FirstOrDefault(g => g.Id == sc.MomentRef)?.Kind : null);
            rows[i] = (seed, $"{seed,6} | {ClockFmt.DayHM(first.CauseClock >= 0 ? first.CauseClock : first.DeathClock),-12} {W(first.Culprit)}→{W(first.Victim)} | {moment}{(ev != null ? "(" + ev + ")" : "")} / {ap} | {motive} | chosen {W(HousePush.Chosen(S))} | events {evs}",
                W(first.Culprit), moment, ap, motive);
        }
        foreach (var r in rows) Console.WriteLine(r.line);
        if (all) Console.WriteLine("all murders: " + string.Join(", ", allMoments.Where(x => x != null).SelectMany(x => x).GroupBy(x => x).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")) + $" · total {allMoments.Where(x => x != null).Sum(x => x.Count)}");
        Console.WriteLine("designs: " + string.Join(", ", designs.Where(x => x != null).SelectMany(x => x).GroupBy(x => x).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")));
        Console.WriteLine("revises: " + string.Join(", ", revises.Where(x => x != null).SelectMany(x => x).GroupBy(x => x).OrderByDescending(g => g.Count()).Take(14).Select(g => $"{g.Key} {g.Count()}")));
        void Count(string title, Func<(ulong seed, string line, string culprit, string moment, string approach, string motive), string> key)
            => Console.WriteLine($"{title}: " + string.Join(", ", rows.GroupBy(key).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}")));
        Console.WriteLine();
        Count("culprits", r => r.culprit); Count("moments", r => r.moment); Count("approaches", r => r.approach); Count("motives", r => r.motive);
        var top = rows.Where(r => r.culprit != "-").GroupBy(r => r.culprit).OrderByDescending(g => g.Count()).FirstOrDefault();
        int n = rows.Count(r => r.culprit != "-");
        Console.WriteLine($"top first killer: {top?.Key} {top?.Count() ?? 0}/{n} ({(n == 0 ? 0 : 100.0 * (top?.Count() ?? 0) / n):0}%) · distinct culprits {rows.Where(r => r.culprit != "-").Select(r => r.culprit).Distinct().Count()} · distinct shapes {rows.Where(r => r.culprit != "-").Select(r => r.moment + "/" + r.approach).Distinct().Count()}");
        return 0;
    }
    static MurderPlan pl0(GameState S, Incident i) => i.PlanId != null && S.Plans.TryGetValue(i.PlanId, out var p) ? p : null;
}
