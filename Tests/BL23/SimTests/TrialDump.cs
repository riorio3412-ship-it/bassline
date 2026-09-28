using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BL23.Sim;

/// <summary>
/// Trial boredom audit ("trialdump seed [smart|naive|passive|all] [outDir]"): runs a campaign seed to its first class trial
/// (investigating the way CluesAudit's thorough player does), snapshots the state, then plays the SAME trial once per player
/// policy on a save/load clone and writes the full beat transcript with a marker at every player decision:
///   smart   — exactly TrialSystem.RunHeadless(smartPlayer=true): free objections (the F review) at every breakable claim line,
///             the kernel's own AutoResolve(smart) inside every round, accuses/votes its top suspect, passes the decision moments.
///   naive   — clicks the first thing offered: first line + first card, first plate, first portrait, all-first reconstruction.
///   passive — lets every candle burn out / passes every moment, goes with the court's favourite when a pick is forced.
/// Each transcript ends with the metrics used for the audit (beats, decisions, gaps, reading time, mechanics, NPC exchanges,
/// culprit counterplay, failure costs, vote pivotality, how early the outcome was set). A reference RunHeadless(smart) run on a
/// third clone checks that the instrumented smart driver reproduces the kernel's own headless player beat for beat.
/// </summary>
public static partial class Program
{
    sealed class TdDecision
    {
        public int At, Trigger = -1, End; public string Kind; public bool Prompted = true; public string Options = ""; public string Choice = ""; public string Outcome = "";
        public float Inf0, Inf1; public string Fav;
    }

    sealed class TdRound { public int Start, End = -1; public string Kind, Why, Topic, Title, Status; public float Limit; public List<string> Show = new List<string>(); public TrialGame G; }

    sealed class TdRun
    {
        public string Policy; public List<TdDecision> Dec = new List<TdDecision>(); public List<TdRound> Rounds = new List<TdRound>(); public HashSet<int> Played = new HashSet<int>();
        public string StartFav, FinalFav; public List<string> InfTrail = new List<string>(); public string Verdict; public Settlement Set; public TrialState T; public string Transcript; public string Metrics;
    }

    static int TrialDump(string[] args)
    {
        ulong seed = args.Length > 1 ? ulong.Parse(args[1]) : 20260926UL;
        string which = args.Length > 2 ? args[2] : "all";
        string outDir = args.Length > 3 ? args[3] : Path.Combine(Path.GetTempPath(), "bl23trialdump");
        Directory.CreateDirectory(outDir);
        var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
        var Lay = S.Layout;
        string mapLine = $"map seed={seed} skeleton={Lay.Skeleton} rooms={Lay.Rooms.Count} (non-passage {Lay.Rooms.Count(r => !RoomInfo.IsPassage(r.Type))}) doors={Lay.Doors.Count} furniture={Lay.Furniture.Count} hash={Lay.Hash} mystery={string.Join(",", Lay.MysteryTypes)}";
        Console.WriteLine("TRIALDUMP " + mapLine);
        long ticks = 0; string invKey = null;
        while (S.Phase != Phase.Trial && ticks < 30_000_000)
        {
            if (S.Phase == Phase.Investigation && invKey != S.Loop + ":" + S.Chapter) { invKey = S.Loop + ":" + S.Chapter; AuditInvestigate(sim); }
            sim.Step(); ticks++;
            if (S.Out.Count > 2000) S.Out.Clear();
        }
        if (S.Phase != Phase.Trial || S.Trial == null) { Console.WriteLine("no trial reached"); return 1; }
        var inc = TrialSystem.TargetIncident(S);
        string culprit = S.Ch.TargetCulprit ?? inc?.Culprit;
        string trick = inc != null ? TrialSystem.TrickOf(S, inc) : null;
        string caseLine = $"case L{S.Loop}C{S.Chapter} {ClockFmt.DayHM(S.Clock)}: victim {Cast.NameOf(inc?.Victim)} culprit {Cast.NameOf(culprit)} method {inc?.Method} dmg {inc?.Dmg} found {S.RoomName(inc?.FoundRoom ?? -1)} cause-room {S.RoomName(inc?.CauseRoom ?? -1)} moved={inc?.BodyMoved} trick={trick ?? "none"} grammar={(inc?.PlanId != null && S.Plans.TryGetValue(inc.PlanId, out var pl) ? pl.Grammar : "-")}";
        var cards = TrialGames.Arsenal(sim);
        string cardLine = $"player cards {cards.Count}: " + string.Join(" | ", cards.Select(c => c.Group + ":" + c.Title));
        Console.WriteLine(caseLine); Console.WriteLine(cardLine);
        string json = SaveStore.Serialize(S);

        var policies = which == "all" ? new[] { "smart", "naive", "passive" } : new[] { which };
        var runs = new List<TdRun>();
        foreach (var pol in policies)
        {
            var s2 = Simulation.FromState(SaveStore.Deserialize(json)); s2.Headless = true;
            var run = TdPlay(s2, pol, culprit);
            run.Transcript = TdTranscript(s2, run, culprit, mapLine, caseLine, cardLine);
            File.WriteAllText(Path.Combine(outDir, $"trial_{seed}_{pol}.txt"), run.Transcript + "\n" + run.Metrics, new UTF8Encoding(false));
            Console.WriteLine($"\n===== {pol.ToUpper()} → {run.Verdict}\n{run.Metrics}");
            runs.Add(run);
        }
        // reference: the kernel's own headless smart player on a third clone must match the instrumented smart driver
        var smartRun = runs.FirstOrDefault(r => r.Policy == "smart");
        if (smartRun != null)
        {
            var s3 = Simulation.FromState(SaveStore.Deserialize(json)); s3.Headless = true;
            TrialSystem.RunHeadless(s3, true);
            var a = string.Join("\n", smartRun.T.Beats.Select(b => b.Kind + b.Speaker + b.Text)); var b3 = string.Join("\n", s3.S.Trial.Beats.Select(b => b.Kind + b.Speaker + b.Text));
            Console.WriteLine($"\nREFERENCE RunHeadless(smart): beats={s3.S.Trial.Beats.Count} verdict={s3.S.LastVerdictSummary} → instrumented smart driver {(a == b3 ? "IDENTICAL" : "DIFFERS")}");
        }
        if (runs.Count > 1)
        {
            Console.WriteLine("\nPOLICY COMPARISON");
            foreach (var r in runs) Console.WriteLine($"  {r.Policy,-8} accused={Cast.GivenOf(r.T.Accused)} correct={r.Set?.Correct} votes[{TdTally(r.T.Votes)}] influence={r.T.Influence:0.00} valid={r.T.Valid} invalid={r.T.Invalid} grade={r.Set?.Grade}");
            bool same = runs.Select(r => r.T.Accused).Distinct().Count() == 1;
            Console.WriteLine($"  verdict independent of the player's play: {(same ? "YES — every policy convicts " + Cast.GivenOf(runs[0].T.Accused) : "no")}");
        }
        Console.WriteLine($"transcripts → {outDir}  faults={sim.Faults}");
        return 0;
    }

    // ------------------------------------------------------------------ the instrumented player
    static TdRun TdPlay(Simulation sim, string pol, string culprit)
    {
        var S = sim.S; var run = new TdRun { Policy = pol };
        run.StartFav = TdFav(S, S.Trial);
        int guard = 0;
        while (S.Trial != null && !S.Trial.Finished && guard++ < 800)
        {
            var T = S.Trial;
            if (T.PendingPrompt != null) { TdPrompt(sim, pol, run); continue; }
            int cur = T.Cursor;
            var b = TrialSystem.Next(sim); if (b == null) { if (T.PendingPrompt != null) continue; break; }
            run.Played.Add(b.N);
            if (pol == "smart" && b.ClaimId != null && b.Kind == "line")
            {
                // RunHeadless(smart): an unprompted objection through the F review whenever any card contradicts an open claim
                var c = T.Claims.FirstOrDefault(x => x.Id == b.ClaimId);
                if (c != null && c.Speaker != Cast.Player && c.Status == "open")
                    foreach (var ev in S.K(Cast.Player).Evidence.ToList())
                        if (ev.Props.Any(p => Logic.Check(S, c.Prop, p, ev.Direct, ev.Root).Result == LogicResult.Contradict))
                        {
                            var d = new TdDecision { At = T.Beats.Count, Trigger = b.N, Kind = "F-review objection", Prompted = false, Inf0 = T.Influence, Fav = TdFav(S, T), Options = $"any claim × {S.K(Cast.Player).Evidence.Count} records" };
                            var r = TrialSystem.PlayerContradict(sim, c.Id, ev.Id);
                            d.Choice = $"{c.Id} ({Cast.GivenOf(c.Speaker)}) ← {ev.Title}"; d.Outcome = r.R + (r.Valid ? " valid" : " miss"); d.Inf1 = T.Influence; d.End = T.Beats.Count; run.Dec.Add(d);
                            break;
                        }
            }
        }
        var TT = S.Trial; run.T = TT; run.FinalFav = TdFav(S, TT);
        run.Set = S.Settlements.LastOrDefault(); run.Verdict = S.LastVerdictSummary;
        foreach (var rd in run.Rounds) if (rd.End < 0) rd.End = TT.Beats.Count;
        run.Metrics = TdMetrics(sim, run, culprit);
        return run;
    }

    static string TdFav(GameState S, TrialState T)
    {
        // plurality of each NPC's current top suspect (read-only: suspicion as last updated by the kernel)
        var tops = T.Participants.Where(x => x != Cast.Player && S.A(x)?.Alive == true).Select(id => S.K(id).Suspicion.Where(kv => kv.Key != id && T.Participants.Contains(kv.Key)).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key).FirstOrDefault()).Where(x => x != null).ToList();
        if (tops.Count == 0) return "-";
        var g = tops.GroupBy(x => x).OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();
        return $"{Cast.GivenOf(g[0].Key)}({g[0].Count()}/{tops.Count})" + (g.Count > 1 ? $" next {Cast.GivenOf(g[1].Key)}({g[1].Count()})" : "");
    }

    static void TdPrompt(Simulation sim, string pol, TdRun run)
    {
        var S = sim.S; var T = S.Trial; string p = T.PendingPrompt;
        var d = new TdDecision { At = T.Beats.Count, Kind = p, Inf0 = T.Influence, Fav = TdFav(S, T) };
        var arsenal = TrialGames.Arsenal(sim).Select(b => b.Id).ToList();
        string first = arsenal.FirstOrDefault();
        if (p == "vote")
        {
            var cands = TrialSystem.VoteCandidates(S); d.Options = $"{cands.Count} portraits (+{TrialSystem.DeadCandidates(S).Count} dead) incl. yourself";
            string pick = pol == "smart" ? TrialSystem.AutoVote(sim, true) : pol == "naive" ? cands.First(x => x != Cast.Player) : TrialSystem.AutoVote(sim, false);
            TrialSystem.PlayerVote(sim, pick); T.PendingPrompt = null; d.Choice = Cast.GivenOf(pick);
        }
        else if (p == "accuse")
        {
            var cands = T.Participants.Where(x => x != Cast.Player).ToList(); d.Options = $"{cands.Count} portraits, no cancel";
            string pick = pol == "smart" ? TrialSystem.AutoVote(sim, true) : pol == "naive" ? cands.First() : TrialSystem.AutoVote(sim, false);
            T.PendingPrompt = null; TrialSystem.PlayerAccuse(sim, pick); d.Choice = Cast.GivenOf(pick);
        }
        else if (p.StartsWith("game:")) TdGame(sim, pol, d, run, arsenal);
        else if (p.StartsWith("witness:") || p.StartsWith("chain:") || p == "theory")
        {
            T.PendingPrompt = null;
            var ids = new List<string>();
            if (p.StartsWith("witness:")) ids.Add(p.Substring(8));
            else if (p.StartsWith("chain:")) { var c0 = T.Claims.FirstOrDefault(c => c.Id == p.Substring(6)); if (c0 != null) { ids.AddRange(c0.Premises); ids.Add(c0.Id); } }
            else ids.AddRange(T.Claims.Where(c => c.Accused != null && c.Status != "refuted" && c.Status != "retracted").GroupBy(c => c.Accused).Select(g => g.OrderByDescending(x => x.Supporters.Count).ThenBy(x => x.Beat).First().Id).Take(3));
            var claims = ids.Select(id => T.Claims.FirstOrDefault(c => c.Id == id)).Where(c => c != null && !string.IsNullOrEmpty(c.Text)).Distinct().Take(4).ToList();
            int breakable = claims.Count(c => arsenal.Any(a => TrialGames.ProbeWorks(S, c.Id, false, a)));
            d.Kind = "moment:" + (p.StartsWith("witness:") ? "witness" : p.StartsWith("chain:") ? "chain" : "theory");
            d.Options = $"{claims.Count} claims × (source/contra/support) + pass; {breakable} breakable with a card: " + string.Join(" / ", claims.Select(c => $"{c.Id} {Cast.GivenOf(c.Speaker)}“{TdShort(c.Text, 40)}”"));
            if (pol == "naive" && claims.Count > 0 && first != null)
            {
                var r = TrialSystem.PlayerContradict(sim, claims[0].Id, first); d.Choice = $"contra {claims[0].Id} ← {TrialGames.BulletEvidence(S, first)?.Title}"; d.Outcome = r.R + (r.Valid ? " valid" : " miss");
            }
            else d.Choice = pol == "smart" ? "pass (RunHeadless passes; it objects via F instead)" : "pass";
        }
        else if (p == "rebut")
        {
            d.Options = $"present one of {arsenal.Count} cards or skip";
            if (pol == "naive" && first != null) { T.PendingPrompt = null; var r = TrialSystem.PlayerPresent(sim, first); d.Choice = "present " + TrialGames.BulletEvidence(S, first)?.Title; d.Outcome = r.R + (r.Valid ? " valid" : ""); }
            else { T.PendingPrompt = null; d.Choice = "skip"; }
        }
        else if (p == "defense")
        {
            d.Options = $"present one of {arsenal.Count} cards or none";
            if (pol == "naive" && first != null) { TrialSystem.PlayerDefense(sim, first); d.Choice = "present " + TrialGames.BulletEvidence(S, first)?.Title; }
            else { TrialSystem.PlayerDefense(sim, null); d.Choice = "none"; }
        }
        else { T.PendingPrompt = null; d.Choice = "(cleared)"; }
        d.Inf1 = T.Influence; d.End = T.Beats.Count; run.Dec.Add(d);
        run.InfTrail.Add($"{d.At}:{d.Inf1:0.00}");
    }

    static void TdGame(Simulation sim, string pol, TdDecision d, TdRun run, List<string> arsenal)
    {
        var S = sim.S; var T = S.Trial; var G = T.Game; string p = T.PendingPrompt;
        if (G == null || G.Status != "open") { T.PendingPrompt = null; d.Choice = "(no open round)"; return; }
        var rd = new TdRound { Start = G.BeatStart, Kind = G.Kind, Why = G.Why, Topic = G.Topic, Title = G.Title + (G.Subtitle != null ? " — " + G.Subtitle : ""), Limit = G.TimeLimit, G = G };
        foreach (var r0 in run.Rounds) if (r0.End < 0) r0.End = G.BeatStart;
        run.Rounds.Add(rd);
        string W(GameLine l) => l.WeakAt >= 0 && l.WeakAt + l.WeakLen <= (l.Text ?? "").Length ? l.Text.Substring(0, l.WeakAt) + "«" + l.Text.Substring(l.WeakAt, l.WeakLen) + "»" + l.Text.Substring(l.WeakAt + l.WeakLen) : l.Text;
        string Tag(GameLine l) { var c = T.Claims.FirstOrDefault(x => x.Id == l.ClaimId); if (c == null) return "(chatter)"; bool br = arsenal.Any(a => TrialGames.ProbeWorks(S, c.Id, false, a)); bool bk = arsenal.Any(a => TrialGames.ProbeWorks(S, c.Id, true, a)); return $"({c.Id} {l.Kind}{(c.Lie ? " LIE" : "")}{(br ? " BREAKABLE" : "")}{(bk && l.Kind == "defense" ? " BACKABLE" : "")})"; }
        for (int i = 0; i < G.Lines.Count; i++) rd.Show.Add($"L{i} {Cast.GivenOf(G.Lines[i].Speaker)}: {W(G.Lines[i])} {Tag(G.Lines[i])}");
        for (int i = 0; i < G.Lines2.Count; i++) rd.Show.Add($"R{i} {Cast.GivenOf(G.Lines2[i].Speaker)}: {W(G.Lines2[i])} {Tag(G.Lines2[i])}");
        if (G.Final != null) rd.Show.Add($"ACCUSATION {Cast.GivenOf(G.Final.Speaker)}: {W(G.Final)} {Tag(G.Final)}");
        if (G.Question != null) rd.Show.Add($"Q {G.Question}  plates: {string.Join(" / ", G.Pool)}  (answer ‘{G.Word}’)");
        foreach (var sl in G.Slots) rd.Show.Add($"slot {sl.Id}: {sl.Label}{(sl.Hint != null ? " — " + sl.Hint : "")}{(sl.Pieces.Count > 0 ? $" [{sl.Pieces.Count} options]" : "")}");
        var shown = G.Bullets.Count > 0 ? G.Bullets.ToList() : arsenal;
        if (G.Kind != "question" && G.Kind != "reconstruct" && G.Kind != "ledger") rd.Show.Add("cards offered: " + string.Join(" | ", shown.Select(id => TrialGames.BulletEvidence(S, id)?.Title)));
        d.Kind = "round:" + G.Kind + (G.Kind == "board" ? "/" + G.Why : G.Kind == "question" ? "/" + G.Topic : G.Kind == "inquiry" ? "/" + G.Why : "");
        d.Options = G.Kind == "inquiry" ? $"{G.Lines.Count} lines × {shown.Count} cards (+press)" : G.Kind == "ledger" ? $"{G.Lines.Count}×{G.Lines2.Count} line pairs" : G.Kind == "question" ? $"{G.Pool.Count} plates" : G.Kind == "reconstruct" ? $"{T.RQ.Count} questions ({string.Join(",", T.RQ.Select(q => q.Id + ":" + q.Options.Count))} options)" : G.Why == "final" ? $"3 slots × {shown.Count} cards" : $"{G.Lines.Count + 1} pins × {shown.Count} cards";
        var acts = new List<string>();
        string Title(string id) => TrialGames.BulletEvidence(S, id)?.Title ?? id;
        switch (G.Kind)
        {
            case "inquiry":
                if (pol == "smart")
                {
                    for (int i = 0; i < G.Lines.Count && G.Status == "open"; i++)
                    {
                        var L = G.Lines[i]; if (L.ClaimId == null) continue;
                        var b = arsenal.FirstOrDefault(id => TrialGames.ProbeWorks(S, L.ClaimId, false, id));
                        if (b != null) { var r = TrialGames.Seal(sim, i, b, false); acts.Add($"seal L{i}←{Title(b)}:{r.Seal ?? r.R.ToString()}"); }
                        else if (L.Kind == "defense") { var s = arsenal.FirstOrDefault(id => TrialGames.ProbeWorks(S, L.ClaimId, true, id)); if (s != null) { var r = TrialGames.Seal(sim, i, s, true); acts.Add($"vouch L{i}←{Title(s)}:{r.Seal ?? r.R.ToString()}"); } }
                    }
                }
                else if (pol == "naive")
                {
                    int tries = 0;
                    for (int i = 0; i < G.Lines.Count && G.Status == "open" && tries < 3; i++)
                    {
                        if (G.Lines[i].ClaimId == null || shown.Count == 0) continue; tries++;
                        var r = TrialGames.Seal(sim, i, shown[0], false); acts.Add($"seal L{i}←{Title(shown[0])}:{(r.Valid ? r.Seal : "miss " + r.R)}");
                    }
                }
                if (G.Status == "open") { TrialGames.Timeout(sim); acts.Add("candles out"); }
                break;
            case "ledger":
                if (pol == "smart") { for (int i = 0; i < G.Lines.Count && G.Status == "open"; i++) for (int j = 0; j < G.Lines2.Count && G.Status == "open"; j++) if (TrialGames.ProbeLedger(S, i, j)) { var r = TrialGames.LedgerPick(sim, i, j); acts.Add($"pair L{i}-R{j}:{(r.Valid ? "won" : "miss")}"); } }
                else if (pol == "naive") { for (int k = 0; k < 3 && G.Status == "open"; k++) { int i = Math.Min(k, G.Lines.Count - 1), j = Math.Min(k, G.Lines2.Count - 1); if (i < 0 || j < 0) break; var r = TrialGames.LedgerPick(sim, i, j); acts.Add($"pair L{i}-R{j}:{(r.Valid ? "won" : "miss")}"); } }
                if (G.Status == "open") { TrialGames.Timeout(sim); acts.Add("time out"); }
                break;
            case "board":
                if (G.Why == "final")
                {
                    var pick = new List<string>();
                    if (pol == "smart") { var used = new HashSet<string>(); foreach (var slot in G.Slots) { var b = arsenal.FirstOrDefault(id => !used.Contains(id) && TrialGames.ProbeSlot(sim, slot.Id, id)); pick.Add(b); if (b != null) used.Add(b); } }
                    else if (pol == "naive") pick = shown.Take(3).ToList();
                    if (pol == "passive") { TrialGames.BoardYield(sim); acts.Add("yield (empty argument)"); }
                    else { var res = TrialGames.FinalBoard(sim, pick); acts.Add("tie " + string.Join(", ", G.Slots.Select((s, i) => s.Id + "←" + (i < pick.Count && pick[i] != null ? Title(pick[i]) : "∅") + ":" + (i < res.Count ? res[i] : "?")))); }
                }
                else
                {
                    if (pol == "smart")
                        for (int i = -1; i < G.Lines.Count && G.Status == "open"; i++)
                        {
                            var L = i < 0 ? G.Final : G.Lines[i]; if (L?.ClaimId == null) continue;
                            var b = arsenal.FirstOrDefault(id => TrialGames.ProbeWorks(S, L.ClaimId, false, id)); if (b != null) { var r = TrialGames.BoardTie(sim, i, b); acts.Add($"tie {(i < 0 ? "ACC" : "P" + i)}←{Title(b)}:{(r.Valid ? r.Seal : "snap")}"); }
                        }
                    else if (pol == "naive" && shown.Count > 0)
                        for (int i = -1; i < G.Lines.Count && G.Status == "open"; i++) { var r = TrialGames.BoardTie(sim, i, shown[0]); acts.Add($"tie {(i < 0 ? "ACC" : "P" + i)}←{Title(shown[0])}:{(r.Valid ? r.Seal : "snap")}"); }
                    if (G.Status == "open") { TrialGames.BoardYield(sim); acts.Add("yield"); }
                }
                break;
            case "question":
                if (pol == "smart") { var r = TrialGames.QuestionPick(sim, G.Word); acts.Add($"‘{G.Word}’:{r}"); }
                else if (pol == "naive") { foreach (var w in G.Pool.Take(2).ToList()) { if (G.Status != "open") break; var r = TrialGames.QuestionPick(sim, w); acts.Add($"‘{w}’:{r}"); } }
                if (G.Status == "open") { TrialGames.Timeout(sim); acts.Add("time out"); }
                break;
            case "reconstruct":
                if (pol == "passive") { TrialGames.AutoResolve(sim, false); acts.Add("left the clock untouched (skipped)"); }
                else
                {
                    var ans = new Dictionary<string, int>(); foreach (var q in T.RQ) ans[q.Id] = pol == "smart" ? (q.Answer >= 0 ? q.Answer : 0) : 0;
                    var chk = TrialGames.ReconstructCheck(sim, ans);
                    TrialGames.ReconstructSubmit(sim, ans);
                    acts.Add((pol == "smart" ? "HIDDEN-TRUTH answers " : "all-first answers ") + string.Join(", ", T.RQ.Select(q => q.Id + "=" + TrialGames.OptionLabel(S, q, ans[q.Id]) + "[" + (chk.TryGetValue(q.Id, out var st) ? st : "?") + "]")) + $" score {T.ReconstructScore}/{T.ReconstructMax}");
                }
                break;
            default: TrialGames.AutoResolve(sim, false); acts.Add("auto"); break;
        }
        if (T.PendingPrompt == p && p != null && p.StartsWith("game:") && (T.Game == null || T.Game.Status != "open")) T.PendingPrompt = null;
        if (T.Game == G && G.Status == "open") { TrialGames.AutoResolve(sim, false); acts.Add("(forced close)"); }
        rd.Status = G.Status; if (T.Game != G && T.Game != null) rd.End = T.Game.BeatStart; else rd.End = T.Beats.Count;
        d.Choice = string.Join("; ", acts); d.Outcome = G.Status + (G.Outcome != null ? " " + G.Outcome : "") + $" misses={G.Misses}";
    }

    // ------------------------------------------------------------------ transcript + metrics
    static readonly HashSet<string> TdReadKinds = new HashSet<string> { "line", "result", "system", "break", "vote", "evidence", "twist" };

    static string TdShort(string s, int n) { s = (s ?? "").Replace("|", " / ").Replace("\n", " "); return s.Length > n ? s.Substring(0, n) + "…" : s; }

    static string TdTally(Dictionary<string, string> votes) => string.Join(", ", votes.GroupBy(v => v.Value).OrderByDescending(g => g.Count()).Select(g => Cast.GivenOf(g.Key) + " " + g.Count()));

    static string[] TdPages(string text)
    {
        var res = new List<string>();
        foreach (var pg in LineBank.Pages(text))
        {
            if (pg.Length <= 46) { res.Add(pg); continue; }
            var parts = System.Text.RegularExpressions.Regex.Split(pg, @"(?<=[\.!?…])\s+"); string cur = "";
            foreach (var q in parts) { if (cur.Length > 0 && cur.Length + q.Length > 46) { res.Add(cur); cur = q; } else cur = cur.Length > 0 ? cur + " " + q : q; }
            if (cur.Length > 0) res.Add(cur);
        }
        return res.ToArray();
    }

    static string TdTranscript(Simulation sim, TdRun run, string culprit, string mapLine, string caseLine, string cardLine)
    {
        var S = sim.S; var T = run.T; var sb = new StringBuilder();
        string Who(string id) => id == null ? "(court)" : (id == culprit ? "★" : id == Cast.Player ? "◆" : "") + (id == Cast.Butler ? "Yusti" : Cast.GivenOf(id));
        sb.AppendLine($"TRIAL TRANSCRIPT — policy {run.Policy.ToUpper()}"); sb.AppendLine(mapLine); sb.AppendLine(caseLine); sb.AppendLine(cardLine);
        sb.AppendLine("legend: ★ culprit  ◆ player  ~ beat generated inside a round and never played in the dialogue box  ┆ inside a round's beat range  >>> player decision");
        var decAt = run.Dec.GroupBy(d => d.At).ToDictionary(g => g.Key, g => g.ToList());
        var roundAt = run.Rounds.GroupBy(r => r.Start).ToDictionary(g => g.Key, g => g.ToList());
        for (int i = 0; i <= T.Beats.Count; i++)
        {
            if (roundAt.TryGetValue(i, out var rs)) foreach (var r in rs) { sb.AppendLine($"      ╔═ ROUND {r.Title} [{r.Kind}{(r.Why != null ? "/" + r.Why : "")}] time {r.Limit:0}s → {r.Status}"); foreach (var line in r.Show) sb.AppendLine("      ║  " + line); }
            if (decAt.TryGetValue(i, out var ds)) foreach (var d in ds) sb.AppendLine($">>> DECISION {(d.Prompted ? "PROMPTED" : "UNPROMPTED")} {d.Kind}{(d.Trigger >= 0 ? " (on #" + d.Trigger + ")" : "")} | options: {d.Options} | choice: {d.Choice} | outcome: {d.Outcome} | influence {d.Inf0:0.00}→{d.Inf1:0.00} | favourite {d.Fav}");
            if (i == T.Beats.Count) break;
            var b = T.Beats[i]; bool inRound = run.Rounds.Any(r => i >= r.Start && i < r.End);
            string pre = (run.Played.Contains(i) ? " " : "~") + (inRound ? "┆" : " ");
            string tag = "";
            if (b.ClaimId != null) { var c = T.Claims.FirstOrDefault(x => x.Id == b.ClaimId); if (c != null) tag = $" [{c.Id} {c.Topic} {c.Prop?.Kind}{(c.Lie ? " LIE" : "")}{(c.Accused != null ? " →" + Cast.GivenOf(c.Accused) : "")} ⇒{c.Status}]"; }
            string body = b.Kind == "mode" ? $"== MODE {b.Data} «{b.Text}»" : b.Kind == "topic" ? $"== TOPIC «{b.Text}»" : b.Kind == "game" ? $"== ROUND BEGINS «{b.Text}»" : $"{Who(b.Speaker)}{(b.Key != null ? " <" + b.Key + ">" : "")}: {TdShort(LineBank.FixParticles(b.Text ?? ""), 400)}{(b.Data != null && b.Kind != "vote" ? " {" + b.Data + "}" : "")}";
            sb.AppendLine($"{pre}#{i:000} [{b.Kind}] {body}{tag}");
        }
        sb.AppendLine("\nCLAIMS");
        foreach (var c in T.Claims) sb.AppendLine($"  {c.Id} {Who(c.Speaker)} {c.Topic} {c.Prop?.Kind}{(c.Lie ? " LIE" : "")} ⇒{c.Status}{(c.RefutedBy != null ? " by " + Who(c.RefutedBy) : "")}{(c.Accused != null ? " accuses " + Who(c.Accused) : "")} sup[{string.Join(",", c.Supporters.Select(Who))}] “{TdShort(c.Text, 90)}”");
        sb.AppendLine("GAMELOG " + string.Join(" ", T.GameLog.Where(x => !x.StartsWith("shown:"))));
        return sb.ToString();
    }

    static string TdMetrics(Simulation sim, TdRun run, string culprit)
    {
        var S = sim.S; var T = run.T; var sb = new StringBuilder();
        var beats = T.Beats;
        var readable = beats.Where(b => run.Played.Contains(b.N) && TdReadKinds.Contains(b.Kind) && !string.IsNullOrEmpty(b.Text)).ToList();
        var hidden = beats.Where(b => !run.Played.Contains(b.N)).ToList();
        int chars = readable.Sum(b => LineBank.FixParticles(b.Text).Replace("|", "").Length);
        int pages = readable.Sum(b => TdPages(LineBank.FixParticles(b.Text)).Length);
        double autoSec = readable.SelectMany(b => TdPages(LineBank.FixParticles(b.Text))).Sum(pg => pg.Length / 48.0 + Math.Min(3.2, Math.Max(1.2, pg.Length * 0.045)));
        int banners = beats.Count(b => run.Played.Contains(b.N) && (b.Kind == "mode" || b.Kind == "topic"));
        autoSec += banners * 2.0;
        var prompted = run.Dec.Where(d => d.Prompted).OrderBy(d => d.At).ToList(); var free = run.Dec.Where(d => !d.Prompted).ToList();
        sb.AppendLine($"METRICS [{run.Policy}] verdict: {run.Verdict} (culprit {Cast.GivenOf(culprit)}, accused {Cast.GivenOf(T.Accused)})");
        sb.AppendLine($"  beats total={beats.Count} played-in-dialogue={run.Played.Count} readable={readable.Count} ({pages} text pages, {chars} chars) round-only/unplayed={hidden.Count} banners={banners}");
        sb.AppendLine("  by kind: " + string.Join(", ", beats.GroupBy(b => b.Kind).OrderByDescending(g => g.Count()).Select(g => g.Key + "=" + g.Count())));
        sb.AppendLine("  by speaker: " + string.Join(", ", readable.GroupBy(b => b.Speaker == null ? "(court)" : b.Speaker == Cast.Butler ? "Yusti" : Cast.GivenOf(b.Speaker)).OrderByDescending(g => g.Count()).Select(g => g.Key + "=" + g.Count())));
        sb.AppendLine($"  reading: {chars / 8.0 / 60.0:0.0} min at 8 chars/s; UI auto-advance pace ≈ {autoSec / 60.0:0.0} min; minigame clocks (upper bound) {run.Rounds.Sum(r => r.Limit) / 60.0:0.0} min over {run.Rounds.Count} rounds");
        sb.AppendLine($"  decisions: prompted={prompted.Count} unprompted(F review)={free.Count} → " + string.Join(", ", run.Dec.GroupBy(d => d.Kind.Split('(')[0].Trim()).Select(g => g.Key + "×" + g.Count())));
        // gaps: readable beats between consecutive prompted decisions
        var cuts = new List<int> { 0 }; cuts.AddRange(prompted.Select(d => d.At)); cuts.Add(beats.Count);
        var gaps = new List<(int from, int to, int n)>();
        for (int i = 0; i + 1 < cuts.Count; i++) { int a = cuts[i], z = cuts[i + 1]; gaps.Add((a, z, readable.Count(b => b.N >= a && b.N < z))); }
        var inner = gaps.Skip(1).Take(Math.Max(0, gaps.Count - 2)).ToList();
        sb.AppendLine($"  readable beats before 1st decision={gaps[0].n} (#{gaps[0].from}-#{gaps[0].to}); between decisions mean={(inner.Count > 0 ? inner.Average(g => g.n) : 0):0.0} max={(inner.Count > 0 ? inner.Max(g => g.n) : 0)}; after last={gaps[gaps.Count - 1].n}; longest stretch={gaps.Max(g => g.n)} (#{gaps.OrderByDescending(g => g.n).First().from}-#{gaps.OrderByDescending(g => g.n).First().to})");
        sb.AppendLine("  gap list: " + string.Join(" ", gaps.Select(g => g.n)));
        var allCuts = run.Dec.Select(d => d.At).Distinct().OrderBy(x => x).ToList(); allCuts.Insert(0, 0); allCuts.Add(beats.Count);
        int longestAny = 0; for (int i = 0; i + 1 < allCuts.Count; i++) longestAny = Math.Max(longestAny, readable.Count(b => b.N >= allCuts[i] && b.N < allCuts[i + 1]));
        sb.AppendLine($"  longest stretch with no decision of any kind (incl. F objections) = {longestAny} readable beats");
        var mech = new SortedSet<string>(run.Dec.Select(d => d.Kind.StartsWith("round:") || d.Kind.StartsWith("moment:") ? d.Kind : d.Kind.Split(':')[0]));
        sb.AppendLine($"  distinct mechanics used ({mech.Count}): {string.Join(", ", mech)}");
        sb.AppendLine("  rounds: " + string.Join(" | ", run.Rounds.Select(r => $"{r.Kind}{(r.Why != null ? "/" + r.Why : "")}{(r.Topic != null && r.Kind == "inquiry" ? "@" + r.Topic : "")}→{r.Status}")));
        // NPC vs NPC
        bool Npc(string id) => id != null && id != Cast.Player && id != Cast.Butler;
        var cl = T.Claims.ToDictionary(c => c.Id);
        int npcObj = beats.Count(b => b.Kind == "result" && Npc(b.Speaker) && b.ClaimId != null && cl.TryGetValue(b.ClaimId, out var c0) && Npc(c0.Speaker));
        var keys = beats.Where(b => b.Kind == "line" && Npc(b.Speaker) && b.Key != null).GroupBy(b => b.Key).ToDictionary(g => g.Key, g => g.Count());
        int K(string k) => keys.TryGetValue(k, out var n) ? n : 0;
        int npcAcc = T.Claims.Count(c => Npc(c.Speaker) && c.Accused != null && c.Accused != Cast.Player && c.Key != "p_accuse");
        sb.AppendLine($"  NPC↔NPC: objections that refuted/limited an NPC claim={npcObj}, NPC→NPC accusations={npcAcc}, accusations of the player={T.Claims.Count(c => Npc(c.Speaker) && c.Accused == Cast.Player)}, agree={K("agree")} defend_other={K("defend_other")} defend_self={K("defend_self")} recant={K("recant")} counter={K("counter")} panic={K("panic")} retract={K("retract")} concede={K("concede")} steer={K("steer")} twist/reveal={beats.Count(b => b.Kind == "twist")}");
        sb.AppendLine("  NPC line keys: " + string.Join(", ", keys.OrderByDescending(x => x.Value).Select(x => x.Key + "=" + x.Value)));
        // the culprit
        var cb = beats.Where(b => b.Speaker == culprit).ToList();
        var cc = T.Claims.Where(c => c.Speaker == culprit).ToList();
        sb.AppendLine($"  culprit {Cast.GivenOf(culprit)}: {cb.Count} beats ({string.Join(",", cb.GroupBy(b => b.Key ?? b.Kind).Select(g => g.Key + "×" + g.Count()))}); claims {cc.Count} (lies {cc.Count(c => c.Lie)}, refuted {cc.Count(c => c.Status == "refuted")}{(cc.Any(c => c.RefutedBy != null) ? " by " + string.Join(",", cc.Where(c => c.RefutedBy != null).Select(c => Cast.GivenOf(c.RefutedBy))) : "")}); accused others: {string.Join(",", cc.Where(c => c.Accused != null).Select(c => Cast.GivenOf(c.Accused)))}; steer lines {cb.Count(b => b.Key == "steer")}; accused BY: {string.Join(",", T.Claims.Where(c => c.Accused == culprit).Select(c => Cast.GivenOf(c.Speaker)))}");
        // failure opportunities & costs
        var misses = run.Dec.Where(d => d.Outcome.Contains("miss") && !d.Outcome.Contains("misses=0") || d.Outcome.StartsWith("lost") || d.Outcome.StartsWith("timeout")).ToList();
        sb.AppendLine($"  fail points: {prompted.Count(d => d.Kind.StartsWith("round:") || d.Kind.StartsWith("moment:"))} rounds/moments; actual lapses={misses.Count}; influence trail: {string.Join(" ", run.Dec.Select(d => $"#{d.At}:{d.Inf0:0.00}→{d.Inf1:0.00}"))}; final influence {T.Influence:0.00} valid={T.Valid} invalid={T.Invalid}");
        sb.AppendLine($"  player suspicion at end (NPC mean) = {T.Participants.Where(x => x != Cast.Player).Select(id => S.K(id).Suspicion.TryGetValue(Cast.Player, out var v) ? v : 0).DefaultIfEmpty(0).Average():0.00}; settlement: correct={run.Set?.Correct} executed={Cast.GivenOf(run.Set?.Executed)} escaped={Cast.GivenOf(run.Set?.Escaped)} drawn={Cast.GivenOf(run.Set?.Drawn)} grade={run.Set?.Grade}");
        // the vote
        var npcVotes = T.Votes.Where(kv => kv.Key != Cast.Player).GroupBy(kv => kv.Value).Select(g => (who: g.Key, n: g.Count())).OrderByDescending(x => x.n).ToList();
        int margin = npcVotes.Count > 1 ? npcVotes[0].n - npcVotes[1].n : npcVotes.Count == 1 ? npcVotes[0].n : 0;
        string pv = T.Votes.TryGetValue(Cast.Player, out var pvx) ? pvx : null;
        sb.AppendLine($"  vote: round {T.VoteRound + 1}; all [{TdTally(T.Votes)}]; NPC-only [{string.Join(", ", npcVotes.Select(x => Cast.GivenOf(x.who) + " " + x.n))}] margin {margin} → player's vote ({Cast.GivenOf(pv)}) {(margin >= 2 ? "could NOT change the result" : margin == 1 ? "could at most force a revote/tie" : "was decisive")}");
        sb.AppendLine($"  NPC votes for the court's final target {Cast.GivenOf(T.Accused)}: {T.Votes.Count(kv => kv.Key != Cast.Player && kv.Value == T.Accused)}/{T.Votes.Count(kv => kv.Key != Cast.Player)}; culprit voted {Cast.GivenOf(T.Votes.TryGetValue(culprit ?? "", out var cv) ? cv : null)}");
        sb.AppendLine($"  obviousness: NPC favourite at the first beat = {run.StartFav}; at the end = {run.FinalFav}; decisions' favourites: {string.Join(" → ", run.Dec.Where(d => d.Prompted).Select(d => d.Fav.Split('(')[0]).Distinct())}");
        return sb.ToString();
    }
}
