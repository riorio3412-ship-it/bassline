using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BL23.Sim;

public static partial class Program
{
    /// <summary>Clue audit (clues v2-lite): "clues seed cases [v] [nocasual]". Plays cases the way a thorough player would —
    /// examine the body, the visible marks in the found room, R-look ten plain pieces of furniture, then ask every living
    /// resident about the case (q_case) and who they suspect (q_suspect) through the real dialogue path (Choose → Spoken) —
    /// and prints what the notebook ends up holding, per case:
    /// incase / key / other / sheets-per-witness / loose-filed / latin / unknownValues / maxTitle / josaFallbacks /
    /// firmAfterExam / firmAfterAll / top6hit / reachable / hiddenInArsenal, plus a hearsay unit check on the first case.</summary>
    static int CluesAudit(string[] args)
    {
        ulong seed = args.Length > 1 ? ulong.Parse(args[1]) : 20260926UL;
        int want = args.Length > 2 ? int.Parse(args[2]) : 2;
        bool verbose = args.Contains("v"); bool casual = !args.Contains("nocasual");
        var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
        int cases = 0; long ticks = 0; bool casualDone = false; string invKey = null; var inv = new InvStats(); int fails = 0; bool hearsayDone = false;
        while (cases < want && ticks < 30_000_000)
        {
            if (casual && !casualDone && S.Phase == Phase.Daily && S.Day == 1 && S.Minute >= 10 * 60) { casualDone = true; CasualExamines(sim); }
            if (S.Phase == Phase.Investigation && invKey != S.Loop + ":" + S.Chapter)
            {
                invKey = S.Loop + ":" + S.Chapter;
                if (!hearsayDone) { hearsayDone = true; if (!HearsayCheck(sim)) fails++; }
                inv = AuditInvestigate(sim);
            }
            if (S.Phase == Phase.Trial)
            {
                cases++;
                TrialSystem.RunHeadless(sim, true);
                if (!AuditReport(sim, inv, verbose)) fails++;
                Replay.BuildSegments(sim); Settlements.AfterReveal(sim);
                if (S.Phase == Phase.LoopEpilogue) { Settlements.NextLoop(sim); S.Phase = Phase.Daily; }
                continue;
            }
            sim.Step(); ticks++;
            if (S.Out.Count > 2000) S.Out.Clear();
        }
        Console.WriteLine($"clues audit done cases={cases} faults={sim.Faults} targetFails={fails}");
        return 0;
    }

    sealed class InvStats { public int FirmAfterExam = -1, FirmAfterAll = -1, PlainLooks, LooseFiled, Talked; public List<string> Steps = new List<string>(); }

    static List<Evidence> ChapterCards(GameState S) => S.K(Cast.Player).Evidence.Where(e => e.Loop == S.Loop && e.Chapter == S.Chapter).ToList();

    static int CountEvidenceEvents(GameState S) => S.Out.Count(e => e.Type == GameEventType.Evidence);

    static void CasualExamines(Simulation sim)
    {
        var S = sim.S; int before = ChapterCards(S).Count; int ev0 = CountEvidenceEvents(S);
        var furn = S.Layout.Furniture.Where(f => { var r = S.Layout.Room(f.Room); return r != null && r.Type != RoomType.Courtroom && (r.Type != RoomType.Bedroom || r.Owner == Cast.Player) && sim.FurnitureActions(f).Count > 0; })
                    .GroupBy(f => f.Type).Select(g => g.OrderBy(f => f.Id).First()).OrderBy(f => f.Id).Take(5).ToList();
        var doors = S.Layout.Doors.OrderBy(d => d.Id).Take(2).ToList();
        var items = S.Items.Values.Where(i => i.Holder == null && i.KeyFor == null && i.Room >= 0).OrderBy(i => i.Id, StringComparer.Ordinal).Take(3).ToList();
        int loose = 0;
        foreach (var f in furn) if (sim.PlayerExamine(f)?.Loose == true) loose++;
        foreach (var d in doors) if (sim.PlayerExamine(d)?.Loose == true) loose++;
        foreach (var i in items) if (sim.PlayerExamine(i)?.Loose == true) loose++;
        sim.RunTicks(600);   // later the same day, a second look at the same things
        foreach (var f in furn.Take(2)) sim.PlayerExamine(f);
        foreach (var d in doors.Take(1)) sim.PlayerExamine(d);
        var after = ChapterCards(S);
        Console.WriteLine($"CASUAL day-1 looks: {furn.Count} furniture + {doors.Count} doors + {items.Count} items ({loose} plain captions), then 2+1 second looks -> {after.Count - before} cards filed, {CountEvidenceEvents(S) - ev0} evidence toasts");
        foreach (var e in after.Skip(before)) Console.WriteLine($"   [{e.Kind}] {CaseBoard.Describe(sim, e).Title} | props={e.Props.Count} | {Flat(e.Desc, 70)}");
    }

    /// <summary>Relay an NPC's body card to the player, then let the player examine the body: the copy must be superseded and
    /// no direct card may carry what only the copy had.</summary>
    static bool HearsayCheck(Simulation sim)
    {
        var S = sim.S; var inc = CaseProgress.Current(S); if (inc == null) return true;
        var body = S.A(inc.Victim); if (body == null) return true;
        var npc = S.LivingNpcs.OrderBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault(x => S.K(x.Id).Evidence.Any(e => e.Kind == EvKind.Body && (e.Subject == body.Id || (e.Root ?? "").StartsWith("bodyexam:" + body.Id))))
                  ?? S.LivingNpcs.OrderBy(x => x.Id, StringComparer.Ordinal).First();
        var src = S.K(npc.Id).Evidence.FirstOrDefault(e => (e.Root ?? "").StartsWith("bodyexam:" + body.Id + ":")) ?? Evidences.ExamineBody(sim, npc, body, true);
        var copy = Evidences.Relay(sim, src, npc.Id);
        bool wasVisible = copy != null && copy.Copy && !copy.Hidden;
        var copyProps = new HashSet<Prop>(copy?.Props ?? new List<Prop>());
        var copyWin = copy?.Props.FirstOrDefault(p => p.Kind == PropKind.DeathWindow && p.Value == "exam");
        sim.PlayerExamine(body);
        var direct = S.K(Cast.Player).Evidence.Where(e => e.Direct && !e.Copy && e.Loop == S.Loop && e.Chapter == S.Chapter).ToList();
        bool leakedRef = direct.Any(e => e.Props.Any(p => copyProps.Contains(p)));
        bool leakedWin = copyWin != null && direct.Any(e => e.Props.Any(p => p.Kind == PropKind.DeathWindow && p.Value == "exam" && p.T0 == copyWin.T0 && p.T1 == copyWin.T1 && (e.Root ?? "").Contains(":" + Cast.Player) == false));
        bool ok = copy != null && wasVisible && copy.Hidden && !leakedRef && !leakedWin;
        Console.WriteLine($"HEARSAY CHECK relay {Cast.GivenOf(npc.Id)}'s body card → copy visible={wasVisible} → after the player's exam copy hidden={copy?.Hidden} leakedRef={leakedRef} leakedWindow={leakedWin} => {(ok ? "PASS" : "FAIL")}");
        return ok;
    }

    static InvStats AuditInvestigate(Simulation sim)
    {
        var S = sim.S; var k = S.K(Cast.Player); var st = new InvStats();
        var start = ChapterCards(S);
        Console.WriteLine($"\n==== INVESTIGATION L{S.Loop}C{S.Chapter} {ClockFmt.DayHM(S.Clock)} — cards already in the notebook: {start.Count(e => !e.Hidden)} visible ({start.Count} stored)");
        foreach (var e in start.Where(e => !e.Hidden)) Console.WriteLine($"   pre [{e.Kind}] {CaseBoard.Describe(sim, e).Title}");
        var inc = CaseProgress.Current(S);
        // 1. the body
        var body = inc != null ? S.A(inc.Victim) : null; if (body != null) sim.PlayerExamine(body);
        st.FirmAfterExam = CaseBoard.FirmCount(sim);
        // 2. the visible marks in the found room
        if (inc != null) foreach (var t in S.Traces.Where(t => !t.Cleaned && t.Visibility <= 1 && t.Room == inc.FoundRoom).OrderBy(t => t.Id, StringComparer.Ordinal).ToList()) sim.PlayerExamine(t);
        // 3. ten plain pieces of furniture (R-look): none of them may be filed
        var plain = S.Layout.Furniture.Where(f => { var r = S.Layout.Room(f.Room); return r != null && r.Type != RoomType.Courtroom && (r.Type != RoomType.Bedroom || r.Owner == Cast.Player) && f.Marks.Count == 0 && !f.Moved && f.Type != "Switchboard" && f.Type != "ClockCase" && f.Type != "Clock" && f.Type != "DoorLogger" && f.Type != "Incinerator" && !S.ClockOffset.ContainsKey(f.Id); })
                      .GroupBy(f => f.Type).Select(g => g.OrderBy(f => f.Id).First()).OrderBy(f => f.Id).Take(10).ToList();
        foreach (var f in plain) { int n0 = k.Evidence.Count; var ev = sim.PlayerExamine(f); st.PlainLooks++; if (ev != null && !ev.Loose && k.Evidence.Count > n0) st.LooseFiled++; }
        // 4. every living resident: the case, and who they suspect
        foreach (var npc in S.LivingNpcs.OrderBy(x => x.Id).ToList())
        {
            if (!sim.CanTalk(npc, out _)) continue;
            sim.BeginTalk(npc); st.Talked++;
            foreach (var q in new[] { "q_case", "q_suspect" }) foreach (var u in sim.Choose(npc, q)) sim.Spoken(u);
            sim.EndTalk(npc);
        }
        st.FirmAfterAll = CaseBoard.FirmCount(sim);
        foreach (var q in CaseBoard.Questions(sim)) Console.WriteLine($"   {(q.Firm ? "●" : "○")} {q.Ask} · {q.Answer ?? "아직 모른다"}{(q.Firm ? "" : " · 다음 — " + q.Hint)} [{q.CardIds.Count} cards]");
        { var sw = System.Diagnostics.Stopwatch.StartNew(); for (int i = 0; i < 20; i++) CaseBoard.NextSteps(sim, 3); double ms1 = sw.Elapsed.TotalMilliseconds / 20; sw.Restart(); for (int i = 0; i < 20; i++) CaseBoard.Cards(sim); double ms2 = sw.Elapsed.TotalMilliseconds / 20; sw.Restart(); for (int i = 0; i < 20; i++) TrialGames.Arsenal(sim); Console.WriteLine($"   cost: NextSteps {ms1:0.00}ms  Cards {ms2:0.00}ms  Arsenal {sw.Elapsed.TotalMilliseconds / 20:0.00}ms  (sightings {k.Sightings.Count}, statements {k.Statements.Count}, cards {k.Evidence.Count})"); }
        st.Steps = CaseBoard.NextSteps(sim, 3);
        Console.WriteLine($"   next: {string.Join(" / ", st.Steps)}");
        foreach (var c in CaseBoard.Conflicts(sim).Take(4)) Console.WriteLine($"   ! {c.Who}: {c.A} ↔ {c.B}");
        return st;
    }

    static string Flat(string s, int n) { s = (s ?? "").Replace("\n", " / "); return s.Length > n ? s.Substring(0, n) + "…" : s; }




    static bool AuditReport(Simulation sim, InvStats inv, bool verbose)
    {
        var S = sim.S; var T = S.Trial; var k = S.K(Cast.Player);
        var Latin = new Regex("[A-Za-z]{3,}"); var JosaLiterals = new[] { "이(가)", "을(를)", "은(는)", "와(과)", "(으)로" };   // locals: keep Program's static initializer out of it
        var inc = S.Incidents.Values.Where(i => i.Confirmed && i.Chapter == S.Chapter && i.Loop == S.Loop).OrderBy(i => i.ConfirmClock).FirstOrDefault();
        var views = CaseBoard.Cards(sim);
        int incase = views.Count(v => v.InCase), key = views.Count(v => v.Key), other = views.Count - incase;
        var sheets = views.Where(v => v.Ev.Kind == EvKind.Testimony && (v.Ev.Root ?? "").StartsWith("wit:")).GroupBy(v => v.Ev.Subject).Select(g => g.Count()).ToList();
        int maxSheets = sheets.Count == 0 ? 0 : sheets.Max();
        var texts = views.SelectMany(v => new[] { v.Title, v.Line, v.Caution, v.When, v.KindLabel }.Concat(v.Bullets)).Where(x => !string.IsNullOrEmpty(x)).ToList();
        var sentences = views.SelectMany(v => v.Ev.Props).Select(p => CaseBoard.Sentence(sim, p, null)).Where(x => x != null).ToList();
        var latin = texts.Concat(sentences).Where(x => Latin.IsMatch(x)).Distinct().ToList();
        var unknown = views.SelectMany(v => v.Ev.Props).Select(p => p.Value).Where(v => v != null && !CaseBoard.KnownValue(v)).Distinct().ToList();
        int maxTitle = views.Count == 0 ? 0 : views.Max(v => v.Title.Length);
        var josa = texts.Concat(sentences).Where(x => JosaLiterals.Any(j => x.Contains(j))).Distinct().ToList();
        // court: does the ranking put a working card in the first six? is every card reachable? anything hidden offered?
        var ars = TrialGames.Arsenal(sim); var arsIds = ars.Select(b => b.Id).ToList();
        var claims = T?.Claims.Where(c => c.Speaker != Cast.Player && c.Prop != null).ToList() ?? new List<TrialClaim>();
        int workable = 0, top6 = 0;
        foreach (var c in claims)
        {
            var works = arsIds.Where(id => TrialGames.ProbeWorks(S, c.Id, false, id) || TrialGames.ProbeWorks(S, c.Id, true, id)).ToList(); if (works.Count == 0) continue;
            workable++; var r6 = CaseBoard.Rank(sim, new List<TrialClaim> { c }).Take(6).ToList(); if (works.Any(r6.Contains)) top6++;
        }
        var rank = CaseBoard.Rank(sim, claims); bool reachable = arsIds.All(rank.Contains) && rank.Count == arsIds.Count;
        int hiddenIn = arsIds.Count(id => k.Evidence.FirstOrDefault(e => e.Id == id)?.Hidden == true);
        Console.WriteLine($"\n==== CARDS L{S.Loop}C{S.Chapter}: victim {Cast.NameOf(inc?.Victim)} culprit {Cast.NameOf(inc?.Culprit)} method {inc?.Method} — verdict: {S.LastVerdictSummary}");
        foreach (var v in views)
        {
            Console.WriteLine($"   {(v.InCase ? (v.Key ? "◆" : "·") : " ")} {v.KindLabel} {v.Title}{(v.When != null ? "  (" + v.When + ")" : "")}{(v.Hearsay ? " [전해 들음]" : "")} — {v.Line}");
            if (verbose) { foreach (var b in v.Bullets) Console.WriteLine($"         · {b}"); if (v.Caution != null) Console.WriteLine($"         주의 — {v.Caution}"); }
        }
        Console.WriteLine($"   METRICS incase={incase} key={key} other={other} sheets-per-witness={maxSheets} loose-filed={inv.LooseFiled}/{inv.PlainLooks} latin={latin.Count} unknownValues={unknown.Count} maxTitle={maxTitle} josaFallbacks={josa.Count} firmAfterExam={inv.FirmAfterExam} firmAfterAll={inv.FirmAfterAll} top6hit={top6}/{workable} reachable={(reachable ? "100%" : "NO")} hiddenInArsenal={hiddenIn} talked={inv.Talked}");
        if (latin.Count > 0) Console.WriteLine("   latin: " + string.Join(" | ", latin.Take(6)));
        if (unknown.Count > 0) Console.WriteLine("   unknown values: " + string.Join(", ", unknown));
        if (josa.Count > 0) Console.WriteLine("   josa: " + string.Join(" | ", josa.Take(6)));
        var fail = new List<string>();
        if (incase > 15) fail.Add($"사건 단서 {incase} > 15");
        if (key < 4 || key > 10) fail.Add($"중요 {key} not in 4..10");
        if (maxSheets > 1) fail.Add("more than one sheet per witness");
        if (inv.LooseFiled > 0) fail.Add("plain looks filed");
        if (latin.Count > 0) fail.Add("latin text");
        if (maxTitle > 18) fail.Add("title > 18");
        if (josa.Count > 0) fail.Add("josa fallbacks");
        if (inv.FirmAfterExam > 2) fail.Add("overview fills itself");
        if (!reachable) fail.Add("unreachable cards");
        if (hiddenIn > 0) fail.Add("hidden cards offered");
        Console.WriteLine("   TARGETS " + (fail.Count == 0 ? "all met" : "MISSED: " + string.Join("; ", fail)));
        return fail.Count == 0;
    }
}
