using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BL23.Sim;

/// <summary>
/// The proactive culprit mind (Sim/Murder/Initiative*.cs) in the lab:
///   initiative &lt;seeds,csv | N&gt; &lt;days&gt; [off] [v]  — headless campaigns; per seed writes BL23Lab/initiative/stats_&lt;seed&gt;.tsv and
///                                                   cases_&lt;seed&gt;.md (CaseApi.Narrative of every murder); save round-trip checked
///   initiative report                              — aggregates every stats_*.tsv: variety of motives, moments (opportunities),
///                                                   hosted evenings, preparation kinds, framing, during-investigation murders, rule
///                                                   shields; share of cases with visible preparation; abort rates; the largest
///                                                   single pattern; writes metrics.md and cases.md (10 varied narratives)
/// </summary>
public static partial class Program
{
    static readonly string InitDir = "C:/Users/리오/BL23Lab/initiative";

    static int InitiativeTest(string[] args)
    {
        if (args.Length > 1 && args[1] == "report") return InitiativeReport();
        if (args.Length > 1 && args[1] == "trace") return InitiativeTrace(ulong.Parse(args[2]), int.Parse(args[3]));
        var seeds = args.Length > 1 && (args[1].Contains(",") || args[1].Length >= 4) ? args[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(ulong.Parse).ToArray()
                  : Enumerable.Range(0, args.Length > 1 ? int.Parse(args[1]) : 6).Select(i => 9100UL + (ulong)i * 7717UL).ToArray();
        int days = args.Length > 2 ? int.Parse(args[2]) : 6;
        bool off = args.Contains("off"), verbose = args.Contains("v");
        Initiative.Enabled = !off;
        Directory.CreateDirectory(InitDir);
        int faults = 0; bool ident = true;
        foreach (var seed in seeds) { var r = RunInitiativeSeed(seed, days, verbose, off); faults += r.faults; ident &= r.identical; }
        Console.WriteLine($"initiative seeds={seeds.Length} days={days} {(off ? "(legacy planner)" : "")} faults={faults} roundtrip={(ident ? "IDENTICAL" : "DIFF")}");
        return faults == 0 && ident ? 0 : 1;
    }

    static (int faults, bool identical) RunInitiativeSeed(ulong seed, int days, bool verbose, bool off)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
        double end = days * 1440; long ticks = 0; int loops = 1, trials = 0;
        var packs = new Dictionary<string, (TrialPack pack, string text)>();
        while (S.Clock < end && ticks < 40_000_000)
        {
            if (S.Phase == Phase.Trial)
            {
                trials++;
                foreach (var inc in S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Murder).OrderBy(i => i.ResultSeq))
                    if (!packs.ContainsKey(inc.Id)) packs[inc.Id] = (CaseApi.TrialPack(S, inc.Id), CaseApi.Narrative(S, inc.Id));
                TrialSystem.RunHeadless(sim, true);
                Settlements.AfterReveal(sim);
                if (S.Phase == Phase.LoopEpilogue) { Settlements.NextLoop(sim); S.Phase = Phase.Daily; loops++; end = S.Clock + days * 1440; if (loops > 2) break; }
                continue;
            }
            if (S.Phase == Phase.Investigation && !S.Flags.ContainsKey("testinv:" + S.Chapter + ":" + S.Loop))
            {
                S.Flags["testinv:" + S.Chapter + ":" + S.Loop] = 1;
                foreach (var inc in S.Incidents.Values.Where(i => i.Confirmed && i.Chapter == S.Chapter && i.Loop == S.Loop).ToList())
                {
                    var body = S.A(inc.Victim); Evidences.ExamineBody(sim, S.Player, body, true);
                    foreach (var t in S.Traces.Where(t => t.Room == body.Room || t.Room == inc.CauseRoom).ToList()) Evidences.ExamineTrace(sim, S.Player, t);
                }
                foreach (var npc in S.LivingNpcs.ToList()) { var w = Testimony.Where(sim, npc, Cast.Player); if (w.Prop != null) sim.Learn(Cast.Player, npc.Id, w.Prop, "(test)", w.Lie, false); }
            }
            sim.Step(); ticks++;
            if (S.Out.Count > 2000) S.Out.Clear();
        }
        foreach (var inc in S.Incidents.Values.Where(i => i.Murder).OrderBy(i => i.ResultSeq)) if (!packs.ContainsKey(inc.Id)) packs[inc.Id] = (CaseApi.TrialPack(S, inc.Id), CaseApi.Narrative(S, inc.Id));
        var json = SaveStore.Serialize(S); var back = SaveStore.Deserialize(json); bool identical = SaveStore.Serialize(back) == json;

        var M = S.Mur ?? new MurderWorld(); var sb = new StringBuilder(); string tag = off ? "off" : "on";
        foreach (var sc in M.Schemes)
        {
            var beats = M.Beats.Where(b => b.Scheme == sc.Id).ToList();
            sb.AppendLine(string.Join("\t", "S", seed, sc.Id, sc.Culprit, sc.Victim, sc.Motive, sc.Moment ?? "-", sc.Approach ?? "-", sc.EventKind ?? "-", sc.Frame ?? "-", sc.Alibi ?? "-",
                string.Join(",", sc.Variants), string.Join(",", sc.Shields), string.Join(",", sc.Prep.Where(p => p.Done).Select(p => p.Kind)), sc.State, (sc.EndWhy ?? "").Replace('\t', ' '),
                sc.Struck >= 0 ? 1 : 0, sc.Killed >= 0 ? 1 : 0, beats.Count, beats.Count(b => b.Observers.Count > 0), beats.Any(b => b.Observers.Contains(Cast.Player)) ? 1 : 0, sc.Strikes, sc.Redesigns, sc.Shape ?? "-", sc.Loop, sc.Chapter, tag));
        }
        foreach (var kv in packs.OrderBy(k => S.Incidents[k.Key].ResultSeq))
        {
            var p = kv.Value.pack; if (p == null) continue; var inc = S.Incidents[kv.Key];
            sb.AppendLine(string.Join("\t", "I", seed, inc.Id, inc.Culprit, inc.Victim, p.Improvised ? 1 : 0, p.Order, p.Judged ? 1 : 0, p.Truth.Moment ?? "-", p.Truth.Approach ?? "-", (p.Truth.Method ?? "-").Replace('\t', ' '),
                p.Shields.Count, p.Lies.Count, p.Fallbacks.Count, p.Foreshadow.Count, p.Foreshadow.Count(b => b.Observers.Count > 0), p.Foreshadow.Any(b => b.PlayerSaw) ? 1 : 0, p.Story.Scapegoat ?? "-",
                string.Join(",", p.Truth.Variants), string.Join(",", p.Shields.Select(s => s.Rule + ":" + s.Tactic)), inc.Loop, inc.Chapter, tag, p.Lies.Count(l => l.BrokenBy.Count > 0)));
        }
        File.WriteAllText(Path.Combine(InitDir, $"stats_{seed}{(off ? "_off" : "")}.tsv"), sb.ToString());
        var cs = new StringBuilder();
        foreach (var kv in packs.OrderBy(k => S.Incidents[k.Key].ResultSeq))
        {
            var p = kv.Value.pack; if (p == null || kv.Value.text == null) continue;
            int richness = p.Truth.Preparations.Count(x => x.Done) + p.Shields.Count + (p.Story.Scapegoat != null ? 2 : 0) + p.Foreshadow.Count(b => b.Observers.Count > 0) * 2 + p.Truth.Variants.Count;
            cs.AppendLine($"<!-- CASE seed={seed} inc={kv.Key} moment={p.Truth.Moment} approach={p.Truth.Approach} planned={(p.Improvised ? 0 : 1)} rich={richness} -->");
            cs.AppendLine(kv.Value.text); cs.AppendLine();
        }
        File.WriteAllText(Path.Combine(InitDir, $"cases_{seed}{(off ? "_off" : "")}.md"), cs.ToString());
        int formed = M.Schemes.Count, struck = M.Schemes.Count(x => x.Struck >= 0), killed = M.Schemes.Count(x => x.Killed >= 0);
        Console.WriteLine($"seed {seed}: schemes={formed} struck={struck} killed={killed} abandoned={M.Schemes.Count(x => x.State == "Abandoned")} murders={packs.Count} (planned {packs.Values.Count(v => v.pack != null && !v.pack.Improvised)}) trials={trials} loops={loops} faults={sim.Faults} roundtrip={(identical ? "IDENTICAL" : "DIFF")} {sw.ElapsedMilliseconds}ms");
        if (verbose)
            foreach (var sc in M.Schemes)
            {
                Console.WriteLine($"  [{sc.Id}] {sc.Culprit}->{sc.Victim} {sc.Motive} {sc.Shape} {sc.State} ({sc.EndWhy}) strikes={sc.Strikes} redesigns={sc.Redesigns}");
                foreach (var l in sc.Log) Console.WriteLine("      " + l);
                foreach (var pid in sc.Plans) if (S.Plans.TryGetValue(pid, out var pl)) Console.WriteLine($"      plan {pl.Grammar} {pl.Stage} steps={string.Join(",", pl.Steps.Select(x => x.Kind + (x.Done ? "*" : "")))} | {string.Join(" / ", pl.Log.Skip(1))}");
            }
        foreach (var l in S.DevLog.Where(x => x.Contains("EXC")).Take(5)) Console.WriteLine("   " + l);
        return (sim.Faults, identical);
    }

    // ================================================================== aggregation
    static int InitiativeReport()
    {
        var S = new List<string[]>(); var I = new List<string[]>();
        foreach (var f in Directory.GetFiles(InitDir, "stats_*.tsv").OrderBy(x => x, StringComparer.Ordinal))
        {
            if (f.EndsWith("_off.tsv")) continue;
            foreach (var line in File.ReadAllLines(f)) { var c = line.Split('\t'); if (c[0] == "S") S.Add(c); else if (c[0] == "I") I.Add(c); }
        }
        var o = new StringBuilder();
        void W(string s) { Console.WriteLine(s); o.AppendLine(s); }
        void Dist(string name, IEnumerable<string> vals)
        {
            var list = vals.Where(v => !string.IsNullOrEmpty(v) && v != "-").ToList(); if (list.Count == 0) { W($"- {name}: (none)"); return; }
            var g = list.GroupBy(v => v).OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();
            W($"- {name} ({g.Count} kinds, n={list.Count}, top {100.0 * g[0].Count() / list.Count:0}%): " + string.Join(", ", g.Select(x => $"{x.Key}={x.Count()}")));
        }
        IEnumerable<string> Multi(IEnumerable<string[]> rows, int col) => rows.SelectMany(r => r[col].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        int seeds = S.Select(r => r[1]).Concat(I.Select(r => r[1])).Distinct().Count();
        var formed = S; var struck = S.Where(r => r[16] == "1").ToList(); var killed = S.Where(r => r[17] == "1").ToList();
        var abandonedPre = S.Where(r => r[14] == "Abandoned" && r[16] == "0").ToList(); var abandonedPost = S.Where(r => r[14] == "Abandoned" && r[16] == "1" && r[17] == "0").ToList();
        W($"# Initiative metrics — {seeds} seeds");
        W("");
        W($"- schemes formed {formed.Count}, struck {struck.Count}, killed {killed.Count}; abandoned before the strike {abandonedPre.Count} ({100.0 * abandonedPre.Count / Math.Max(1, formed.Count):0}%), strike failed {abandonedPost.Count} ({100.0 * abandonedPost.Count / Math.Max(1, struck.Count):0}% of strikes); still open at the end {S.Count(r => r[14] != "Done" && r[14] != "Abandoned")}");
        var murders = I; var planned = I.Where(r => r[5] == "0").ToList();
        W($"- murders {murders.Count}: planned by a scheme {planned.Count} ({100.0 * planned.Count / Math.Max(1, murders.Count):0}%), improvised (legacy impulse) {murders.Count - planned.Count}");
        W($"- planned murders with visible preparation (a tell someone saw) {planned.Count(r => int.Parse(r[15]) > 0)} ({100.0 * planned.Count(r => int.Parse(r[15]) > 0) / Math.Max(1, planned.Count):0}%); with a tell the player saw {planned.Count(r => r[16] == "1")}; mean tells per planned murder {(planned.Count > 0 ? planned.Average(r => int.Parse(r[14])) : 0):0.0} (seen {(planned.Count > 0 ? planned.Average(r => int.Parse(r[15])) : 0):0.0})");
        W($"- lies per murder {(murders.Count > 0 ? murders.Average(r => int.Parse(r[12])) : 0):0.0} (with a breaking seam {(murders.Count > 0 ? murders.Average(r => int.Parse(r[23])) : 0):0.0}), fallbacks {(murders.Count > 0 ? murders.Average(r => int.Parse(r[13])) : 0):0.0}, shields {(murders.Count > 0 ? murders.Average(r => int.Parse(r[11])) : 0):0.0}; with a scapegoat {murders.Count(r => r[17] != "-")}");
        W($"- second (not judged) murders {murders.Count(r => int.Parse(r[6]) >= 2)}; during the investigation {killed.Count(r => r[11].Contains("during-investigation"))}; copycat {killed.Count(r => r[11].Contains("copycat"))}; hosted {killed.Count(r => r[11].Contains("hosted"))}; group moment {killed.Count(r => r[11].Contains("group-moment"))}; turnabout {killed.Count(r => r[11].Contains("turnabout"))}; unwitting helper {killed.Count(r => r[11].Contains("helper"))}; framed {killed.Count(r => r[11].Contains("framed"))}");
        W("");
        W("## Variety (killed schemes)");
        Dist("motive", killed.Select(r => r[5])); Dist("moment", killed.Select(r => r[6])); Dist("approach", killed.Select(r => r[7])); Dist("hosted evening", killed.Select(r => r[8]));
        Dist("frame", killed.Select(r => r[9])); Dist("alibi", killed.Select(r => r[10])); Dist("variants", Multi(killed, 11)); Dist("rule shields (planned)", Multi(killed, 12)); Dist("preparation done", Multi(killed, 13));
        var pat = killed.Select(r => r[6] + "/" + r[7]).ToList();
        var allPat = murders.Select(r => r[5] == "1" ? "improvised/" + (r[10].Split('+')[0]) : r[8] + "/" + r[9]).ToList();
        Dist("pattern moment/approach (killed schemes)", pat);
        Dist("pattern (all murders)", allPat);
        Dist("case shape (moment/approach/frame/alibi)", killed.Select(r => r[23]));
        W("");
        W("## Variety (all schemes formed)");
        Dist("motive", formed.Select(r => r[5])); Dist("moment", formed.Select(r => r[6])); Dist("approach", formed.Select(r => r[7])); Dist("hosted evening", formed.Select(r => r[8]));
        Dist("frame", formed.Select(r => r[9])); Dist("variants", Multi(formed, 11)); Dist("preparation done", Multi(formed, 13));
        Dist("abandon reasons", S.Where(r => r[14] == "Abandoned").Select(r => Reason(r[15])));
        Dist("shields in trial packs (incl. detected)", Multi(I, 19));
        File.WriteAllText(Path.Combine(InitDir, "metrics.md"), o.ToString());
        // ten varied narratives
        var blocks = new List<(string head, string text, string key, int rich, bool plannedCase)>();
        foreach (var f in Directory.GetFiles(InitDir, "cases_*.md").OrderBy(x => x, StringComparer.Ordinal))
        {
            if (f.EndsWith("_off.md")) continue;
            var txt = File.ReadAllText(f); var parts = txt.Split(new[] { "<!-- CASE " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                int e = p.IndexOf("-->"); if (e < 0) continue; var head = p.Substring(0, e);
                string Get(string k) { int i = head.IndexOf(k + "="); if (i < 0) return ""; int j = head.IndexOf(' ', i); return j < 0 ? head.Substring(i + k.Length + 1).Trim() : head.Substring(i + k.Length + 1, j - i - k.Length - 1); }
                blocks.Add((head, p.Substring(e + 3).Trim(), Get("moment") + "/" + Get("approach"), int.TryParse(Get("rich"), out var rr) ? rr : 0, Get("planned") == "1"));
            }
        }
        var chosen = new List<(string head, string text, string key, int rich, bool plannedCase)>(); var used = new HashSet<string>();
        foreach (var b in blocks.Where(b => b.plannedCase).OrderByDescending(b => b.rich)) { if (chosen.Count >= 9) break; if (used.Add(b.key)) chosen.Add(b); }
        foreach (var b in blocks.Where(b => !b.plannedCase).OrderByDescending(b => b.rich).Take(1)) chosen.Add(b);
        foreach (var b in blocks.Where(b => b.plannedCase).OrderByDescending(b => b.rich)) { if (chosen.Count >= 10) break; if (!chosen.Contains(b)) chosen.Add(b); }
        var md = new StringBuilder();
        md.AppendLine("# BL23 — 능동적 범인: 사건 기록 10선 (initiative lab)"); md.AppendLine();
        md.AppendLine($"생성: SimTests `initiative` · {seeds}개 시드 · 사건별로 CaseApi.TrialPack을 사람이 읽을 수 있게 펼친 것 (진실 vs 내세울 이야기). 마지막 하나는 비교용 즉흥 범행."); md.AppendLine();
        foreach (var b in chosen) { md.AppendLine($"<!-- {b.head.Trim()} -->"); md.AppendLine(b.text); md.AppendLine(); md.AppendLine("---"); md.AppendLine(); }
        File.WriteAllText(Path.Combine(InitDir, "cases.md"), md.ToString());
        Console.WriteLine($"wrote {Path.Combine(InitDir, "metrics.md")} and cases.md ({chosen.Count} cases)");
        return 0;
    }

    static string Reason(string s) { if (string.IsNullOrEmpty(s)) return "-"; int i = s.IndexOf(" — "); return (i > 0 ? s.Substring(0, i) : s).Trim(); }

    /// <summary>initiative trace &lt;seed&gt; &lt;days&gt;: every 3 game minutes while a scheme is striking, where the culprit and the victim are and what they do.</summary>
    static int InitiativeTrace(ulong seed, int days)
    {
        var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
        double end = days * 1440, next = 0; long ticks = 0;
        while (S.Clock < end && ticks < 40_000_000)
        {
            if (S.Phase == Phase.Trial) { TrialSystem.RunHeadless(sim, true); Settlements.AfterReveal(sim); if (S.Phase == Phase.LoopEpilogue) break; continue; }
            sim.Step(); ticks++; if (S.Out.Count > 2000) S.Out.Clear();
            if (S.Clock < next) continue; next = S.Clock + 3;
            foreach (var sc in S.Mur.Schemes.Where(x => x.State == "Striking"))
            {
                var a = S.A(sc.Culprit); var v = S.A(sc.Victim); S.Plans.TryGetValue(sc.Plan ?? "", out var pl);
                string step = pl != null && pl.Step < pl.Steps.Count ? pl.Steps[pl.Step].Kind : "-";
                Console.WriteLine($"{ClockFmt.DayHM(S.Clock)} {S.Phase} {sc.Id} {sc.Approach} plan={pl?.Stage}:{step} | {a.Id} @{S.RoomName(a.Room)} act={a.Act?.Id}/{a.Act?.Cur?.Kind} think={a.NextThink - S.Clock:0.0} int={a.Act?.Interruptible} | {v.Id} @{S.RoomName(v.Room)} act={v.Act?.Id}/{v.Act?.Cur?.Kind} light={sim.RoomLight(v.Room):0.00} d={a.Pos.Dist(v.Pos):0.0}");
            }
        }
        foreach (var l in S.DevLog.Where(x => x.Contains("EXC")).Take(5)) Console.WriteLine("   " + l);
        return 0;
    }
}
