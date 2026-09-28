using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BL23.Sim;

/// <summary>
/// The debate 심판 (Sim/Trial/Debate, TrialReforge.md). Modes:
///   deckdump &lt;seed&gt;            — run to the first 심판 (investigating like TrialDump's thorough player), build the 은판 deck, print it
///   deckgate &lt;n&gt;               — the deck over seeds 20260926, 777 + n more: sizes, fakes, routes, users, validation log
///   debate &lt;seed&gt; [smart|naive|passive] — play the debate 심판 headless and print it as the screen would show it, with
///                                    the test player's moves inline, then the room, the knots and the verdict
/// </summary>
public static partial class Program
{
    /// <summary>Run a campaign to its first 심판 (Phase.Trial), investigating each confirmed case the way the audits do.</summary>
    static Simulation ToFirstTrial(ulong seed, out string why, bool useDebate = false)
    {
        why = null;
        var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
        long ticks = 0; string invKey = null;
        bool debate = TrialSystem.DebateEnabled; TrialSystem.DebateEnabled = useDebate;   // Begin runs inside Step: the court opens with the engine asked for
        try
        {
            while (S.Phase != Phase.Trial && ticks < 30_000_000)
            {
                if (S.Phase == Phase.Investigation && invKey != S.Loop + ":" + S.Chapter) { invKey = S.Loop + ":" + S.Chapter; AuditInvestigate(sim); }
                sim.Step(); ticks++;
                if (S.Out.Count > 2000) S.Out.Clear();
            }
        }
        finally { TrialSystem.DebateEnabled = debate; }
        if (S.Phase != Phase.Trial || S.Trial == null) { why = "no trial reached"; return null; }
        return sim;
    }

    static string DeckText(Simulation sim, CaseFacts C, CaseDeck d)
    {
        var S = sim.S; var sb = new StringBuilder();
        sb.AppendLine($"case {d.Incident} L{d.Loop}C{d.Chapter}: victim {Cast.GivenOf(C.VictimId)} culprit {Cast.GivenOf(C.Culprit)} trick {C.Trick ?? "-"} kill {S.RoomName(C.KillRoom)} {ClockFmt.HM(C.KillClock)} found {S.RoomName(C.FoundRoom)} {ClockFmt.HM(C.FoundClock)} scapegoat {Cast.GivenOf(C.Scapegoat) ?? "-"} pack {(C.Pack == null ? "none" : C.Pack.Improvised ? "improvised" : "scheme")}");
        sb.AppendLine("factions: " + Factions.Describe(S));
        foreach (var g in S.Gatherings.Where(g => HouseEvents.IsHouse(g) && g.Revs.Count > 0)) sb.AppendLine($"house event: {g.Kind} {g.Label} {ClockFmt.DayHM(g.Cur.Start)} @{S.RoomName(g.Cur.Room)}{(g.Cancelled ? " (cancelled)" : "")}");
        if (C.Pack != null) sb.AppendLine($"scheme: moment {C.Pack.Truth.Moment ?? "-"} ({C.Pack.Truth.MomentText}) approach {C.Pack.Truth.Approach ?? "-"} · story “{C.Pack.Story.ClaimText}” [{S.RoomName(C.Pack.Story.ClaimRoom)} {ClockFmt.HM(C.Pack.Story.ClaimFrom)}–{ClockFmt.HM(C.Pack.Story.ClaimTo)}] · lies {string.Join(", ", C.Pack.Lies.Select(l => l.Topic + "[" + string.Join(" ", l.BrokenBy.Take(4)) + (l.BrokenBy.Count > 4 ? " …" : "") + "]"))}");
        sb.AppendLine($"deck: {d.Plates.Count} plates, true {d.TrueCount}, fake {d.FakeCount} (target {d.FakeTarget}) source {d.Source}");
        foreach (var c in d.Claims) sb.AppendLine($"  claim {c.Id} L{c.Layer} {c.Axis} {(c.Truth == "true" ? "(true)" : "")} “{c.Text}”");
        foreach (var m in d.Mysteries) sb.AppendLine($"  riddle {m.Id} [{m.Kind}] {m.Text} ← {m.Claim}");
        foreach (var p in d.Plates)
            sb.AppendLine($"  {p.N,2} {(p.True ? "◉" : "✕")} {p.Role,-11} {p.Kind,-7} {p.State,-8} “{p.Title}” — {p.Face}\n       back: {p.Back}\n       breaks[{string.Join(",", p.Breaks)}] supports[{string.Join(",", p.Supports)}] routes[{string.Join(",", p.Routes.Select(r => r.StartsWith("pl:") ? "#" + (d.Plates.FirstOrDefault(x => "pl:" + x.Id == r)?.N.ToString() ?? "?") : r))}] users[{string.Join(",", p.Users.Select(u => u.StartsWith("theory:") ? Cast.GivenOf(u.Substring(7)) : u))}] points {(p.Points != null && S.A(p.Points) != null ? Cast.GivenOf(p.Points) : p.Points ?? "-")} found {(p.FoundBy == null ? "-" : Cast.GivenOf(p.FoundBy))}");
        foreach (var l in d.Log) sb.AppendLine("  log: " + l);
        return sb.ToString();
    }

    static int DeckDump(string[] args)
    {
        ulong seed = args.Length > 1 && ulong.TryParse(args[1], out var sd) ? sd : 20260926UL;
        var sim = ToFirstTrial(seed, out var why); if (sim == null) { Console.WriteLine(why); return 1; }
        var inc = TrialSystem.TargetIncident(sim.S);
        var C = Debate.Facts(sim, inc); var d = Debate.BuildDeck(C);
        Console.WriteLine(DeckText(sim, C, d));
        if (args.Contains("diag"))
        {
            var S = sim.S;
            Console.WriteLine($"-- incident: cause {S.RoomName(inc.CauseRoom)} {ClockFmt.HM(inc.CauseClock)} death {S.RoomName(inc.DeathRoom)} {ClockFmt.HM(inc.DeathClock)} found {S.RoomName(inc.FoundRoom)} moved {inc.BodyMoved} weapon {inc.Weapon} {inc.WeaponType} method {inc.Method}");
            foreach (var t in S.Traces.Where(t => t.Victim == inc.Victim || t.Room == inc.CauseRoom || t.Room == inc.FoundRoom || t.Room == inc.DeathRoom))
                Console.WriteLine($"   trace {t.Id} {t.Type} {S.RoomName(t.Room)} {ClockFmt.HM(t.Clock)} v={Cast.GivenOf(t.Victim)} src={Cast.GivenOf(t.Source)} vis={t.Visibility} cleaned={t.Cleaned} desc={t.Desc} note={t.Note}");
            var v = S.A(inc.Victim);
            if (v?.Body.GoreMarks != null) Console.WriteLine("   gore marks: " + string.Join(", ", v.Body.GoreMarks.Select(g => g.Kind + "@" + S.RoomName(g.Room))));
            foreach (var e in S.K(Cast.Player).Evidence.Where(e => !e.Hidden)) Console.WriteLine($"   P01 card {e.Kind} root={e.Root} “{e.Title}” props: {string.Join(" ", e.Props.Select(p => p.Kind + (p.Value != null ? ":" + p.Value : "") + (p.Item != null ? "/" + p.Item : "")))}");
            foreach (var id in C.Npcs) foreach (var e in S.K(id).Evidence.Where(e => e.Kind != EvKind.Testimony && e.Kind != EvKind.Announcement).Take(4)) Console.WriteLine($"   {Cast.GivenOf(id)} card {e.Kind} root={e.Root} “{e.Title}”");
            if (C.Pack != null) { Console.WriteLine($"   pack story: claim {S.RoomName(C.Pack.Story.ClaimRoom)} {ClockFmt.HM(C.Pack.Story.ClaimFrom)}-{ClockFmt.HM(C.Pack.Story.ClaimTo)} with {string.Join(",", C.Pack.Story.ClaimWith.Select(Cast.GivenOf))} text “{C.Pack.Story.ClaimText}” theory “{C.Pack.Story.Theory}” scapegoat {Cast.GivenOf(C.Pack.Story.Scapegoat)} planted {string.Join(",", C.Pack.Story.Planted)}");
              foreach (var l in C.Pack.Lies) Console.WriteLine($"   lie {l.Id} {l.Topic} cost {l.Cost} “{l.Text}” brokenBy {string.Join(",", l.BrokenBy)}");
              // what each listed witness of the "where" lie actually holds about the culprit around the claim
              var wl = C.Pack.Lies.FirstOrDefault(l => l.Topic == "where");
              if (wl != null) foreach (var w in wl.BrokenBy.Where(b => b.StartsWith("witness:")).Select(b => b.Substring(8)))
              {
                  foreach (var s in S.K(w).Sightings.Where(x => x.Target == C.Culprit && x.T1 >= C.Pack.Story.ClaimFrom - 30 && x.T0 <= C.Pack.Story.ClaimTo + 30))
                      Console.WriteLine($"   where-breaker {Cast.GivenOf(w)} saw {S.RoomName(s.Room)} {ClockFmt.HM(s.T0)}-{ClockFmt.HM(s.T1)} id {s.IdConf:0.00} disguise {s.Disguise ?? "-"} dead {s.Dead}");
                  foreach (var f in S.K(w).Facts.Where(f => f.StartsWith("left-gathering:") || f.StartsWith("left-table:"))) Console.WriteLine($"   where-breaker {Cast.GivenOf(w)} fact {f}");
              }
              foreach (var e in S.Ledger.Where(e => wl != null && wl.BrokenBy.Contains("ledger:" + e.Seq))) Console.WriteLine($"   where-breaker ledger {e.Seq} {e.Type} {Cast.GivenOf(e.Actor)} {S.RoomName(e.Room)} {ClockFmt.HM(e.Clock)} {e.Data}");
              foreach (var f in C.Pack.Fallbacks) Console.WriteLine($"   fallback {f.Order} [{f.Trigger}] “{f.Story}” concedes {f.Concedes} keeps {f.Keeps}");
              foreach (var sh in C.Pack.Shields) Console.WriteLine($"   shield {sh.Rule}:{sh.Tactic} “{sh.Argument}”"); }
        }
        return 0;
    }

    static int DeckGate(string[] args)
    {
        int n = args.Length > 1 && int.TryParse(args[1], out var nn) ? nn : 4;
        var seeds = new List<ulong> { 20260926UL, 777UL };
        for (int i = 0; i < n; i++) seeds.Add(1000UL + (ulong)i * 7919UL);
        int ok = 0, thin = 0, reached = 0;
        foreach (var seed in seeds)
        {
            var sim = ToFirstTrial(seed, out var why); if (sim == null) { Console.WriteLine($"seed {seed}: {why}"); continue; }
            reached++;
            var inc = TrialSystem.TargetIncident(sim.S);
            if (inc == null || !inc.Murder) { Console.WriteLine($"seed {seed}: no murder to judge"); continue; }
            var C = Debate.Facts(sim, inc); var d = Debate.BuildDeck(C);
            bool good = d.Plates.Count >= 8 && d.FakeCount >= 3 && d.Log.All(l => !l.StartsWith("claim unbreakable") && !l.StartsWith("fake without"));
            if (good) ok++; else thin++;
            Console.WriteLine($"seed {seed}: {C.Trick ?? "-",-8} plates {d.Plates.Count,2} fakes {d.FakeCount} riddles {d.Mysteries.Count} claims {d.Claims.Count} {(good ? "ok" : "THIN")} | " + string.Join("; ", d.Log.Where(l => l.StartsWith("claim") || l.StartsWith("thin") || l.StartsWith("no ")).Take(3)));
        }
        Console.WriteLine($"deckgate: reached {reached}/{seeds.Count}, full {ok}, thin {thin}");
        return 0;
    }

    static int DebateRun(string[] args)
    {
        ulong seed = args.Length > 1 && ulong.TryParse(args[1], out var sd) ? sd : 20260926UL;
        string policy = args.Length > 2 ? args[2] : "smart";
        var sim = ToFirstTrial(seed, out var why, useDebate: true); if (sim == null) { Console.WriteLine(why); return 1; }
        var S = sim.S; var T = S.Trial;
        if (T.Debate == null) { Console.WriteLine($"seed {seed}: the court opened with the old engine (no debate-eligible murder)"); return 1; }
        var D = T.Debate;
        var inc = S.Incidents[D.Incident];
        Console.WriteLine(DeckText(sim, Debate.Facts(sim, inc), D.Deck));
        var trace = new List<string>(); TrialSystem.DebateTraceLog = trace;
        try { TrialSystem.DebateHeadless(sim, policy); }
        finally { TrialSystem.DebateTraceLog = null; }
        // the transcript: every line the screen would show, the test player's moves where they happened
        var at = trace.Select(x => { int i = x.IndexOf('|'); return (n: int.Parse(x.Substring(0, i)), what: x.Substring(i + 1)); }).ToList();
        int readable = 0;
        for (int i = 0; i <= T.Beats.Count; i++)
        {
            foreach (var a in at.Where(a => a.n == i)) Console.WriteLine($"          ▶ {a.what}");
            if (i == T.Beats.Count) break;
            var b = T.Beats[i];
            string who = b.Speaker == null ? "" : Cast.GivenOf(b.Speaker) ?? b.Speaker;
            string tag = b.Kind == "line" ? "" : $"[{b.Kind}{(b.Data != null && b.Kind != "vote" ? ":" + b.Data : "")}] ";
            string face = b.Kind == "line" && b.Speaker != null && b.Emotion != Emotion.Neutral ? $" ({b.Emotion}{(b.Gesture != Anim.Talk && b.Gesture != Anim.None ? "/" + b.Gesture : "")})" : "";
            string key = b.Key != null && (b.Key == "accuse" || b.Key == "object" || b.Key == "counter" || b.Key == "panic" || b.Key == "recant" || b.Key == "steer" || b.Key == "p_object" || b.Key == "reveal") ? " «" + b.Key + "»" : "";
            if (b.Kind == "mode") { Console.WriteLine($"\n══ {b.Text} ══"); continue; }
            if (b.Kind == "topic") { Console.WriteLine($"\n── {b.Text}"); continue; }
            if (b.Kind == "line" && b.Speaker != null || b.Kind == "system") readable++;
            Console.WriteLine($"{i,4} {tag}{(who.Length > 0 ? who + face + key + ": " : "")}{b.Text}");
        }
        Console.WriteLine();
        Console.WriteLine($"== verdict: accused {Cast.GivenOf(T.Accused)} culprit {Cast.GivenOf(D.Target)} correct {D.Correct} · finished {T.Finished} stage {T.Stage} act {D.Act}/{D.Step}");
        Console.WriteLine($"   riddles: {string.Join(" · ", D.Mysteries.Select(m => $"{m.Id} {m.Trick} {m.State} {m.SettledBy ?? "-"} floors {m.Floors} passes {m.Passes} theories {m.Theories.Count}"))}");
        Console.WriteLine($"   plaques: {D.Plaques.Count} — " + string.Join(" / ", D.Plaques.Select(q => q.Text)));
        Console.WriteLine($"   knots: chance {D.Mind.Chance} means {D.Mind.Means} deceit {D.Mind.Deceit} · candles {D.Mind.CandlesLit}/{D.Mind.Candles} · plan {string.Join(",", D.Mind.Plan)} · broken {D.Mind.Broken} · pressure {D.Mind.Pressure}");
        Console.WriteLine($"   decisions {D.Decisions.Count} (counted {D.Decisions.Count(d => d.Counted)}) · hits {D.Hits} misses {D.Misses} passes {D.Passes} asks {D.Asks} hints {D.Hints} reversals {D.Reversals} · readable lines {readable} · beats {T.Beats.Count}");
        Console.WriteLine($"   decisions: {string.Join(" | ", D.Decisions.Select(d => d.Kind + ":" + d.Outcome))}");
        Console.WriteLine($"   votes: {string.Join(", ", T.Votes.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => Cast.GivenOf(kv.Key) + "→" + Cast.GivenOf(kv.Value)))}");
        Console.WriteLine($"   room: {string.Join(", ", D.Reading.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => Cast.GivenOf(kv.Key) + ":" + (kv.Value == "?" ? "?" : Cast.GivenOf(kv.Value))))}");
        // the state must survive a save in the middle of it (and after)
        var json = SaveStore.Serialize(S); var back = SaveStore.Serialize(SaveStore.Deserialize(json));
        Console.WriteLine($"   save roundtrip: {(json == back ? "IDENTICAL" : "DIFFERENT")} ({json.Length} chars)");
        return 0;
    }
}
