using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BL23.Sim;

/// <summary>
/// "life [days] [seed,seed,…]" — daily life (Sim/Life) with a scripted, sociable player, DailyLifeDesign §11:
///   PAIR    pair scenes per day (NPC-only + in front of 민혁; target 3–6, ≤ 2 in front of him)
///   TABLE   distinct table topics over the run (target ≥ 5)
///   TIES    distinct tie states that changed (target ≥ 8)
///   GRIEF   per death: a table that mourned it, a memorial held, grief acts, inherited goals voiced
///   REPEAT  per resident per day, the most times one exact daily-life line was said (target ≤ 3)
///   HEARTS  heart events played; INVITES heart invitations + residents who came to 민혁 (gift, jealousy, rumour …)
/// plus festivals, rumours (seeded / spread / distorted / traced / corrected), banter keys, and a content lint
/// (pages over 60 characters, 재판, other people's tics, the romance guardrail). Ends with the save round-trip.
/// </summary>
public static partial class Program
{
    static readonly string[] LifeKeys = { "life_give", "life_give_thanks", "life_give_tease", "life_give_refused", "life_jealous", "life_jealous_soothed", "life_jealous_teased", "life_jealous_hurt",
        "life_rival", "life_rival_ok", "life_rival_defied", "life_rival_seen", "rumour_tell", "rumour_unsure", "rumour_scolded", "rumour_deny", "rumour_correct", "rumour_from", "rumour_mine",
        "after_trial_walk", "after_trial_walk_yes", "after_trial_walk_no", "grief_share", "grief_share_back", "grief_share_silent", "grief_act", "memorial_word",
        "card_bluff", "card_true", "card_caught", "card_bluff_reveal", "card_true_win", "card_true_fold", "fest_bar_banter", "fest_cook_brag", "fest_cook_lost", "fest_cook_won",
        "fest_quiz_open", "fest_cards_open", "fest_cards_win", "fest_cards_lose", "fest_concert_open", "fest_bar_open", "fest_cook_open", "memorial_open", "fest_perform", "tt_spoons", "tt_lily", "gift_log" };

    static int LifeTest(string[] args)
    {
        int days = args.Length > 1 ? int.Parse(args[1]) : 5;
        var seeds = args.Length > 2 ? args[2].Split(',').Select(ulong.Parse).ToArray() : new[] { 20260926UL, 777UL, 4242UL };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int lintFails = LifeLint();

        var perDayLine = new Dictionary<string, Dictionary<string, int>>();   // seed|day|who → text → n
        var tableKinds = new HashSet<string>(); var tieStates = new HashSet<string>();
        long pairTotal = 0, pairPlayer = 0, hearts = 0, invites = 0, seeks = 0, hangs = 0, fests = 0, festSeen = 0, deaths = 0, memorials = 0, griefActs = 0, inherit = 0, tableDeath = 0, gifts = 0;
        long rumSeed = 0, rumSpread = 0, rumDist = 0, rumTrace = 0, rumFix = 0, faults = 0; int dayCount = 0; var banter = new Dictionary<string, long>();
        var pairsPerDay = new List<int>(); var heartsPer = new Dictionary<string, int>(); bool roundtripOk = true;

        foreach (var seed in seeds)
        {
            var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
            var rng = new Rng(seed ^ 0xA11CEUL, 7);
            Simulation.LifeLineTrace = (who, text) =>
            {
                if (who == null || who == Cast.Player || text == null) return;
                string k = seed + "|" + S.Day + "|" + who;
                if (!perDayLine.TryGetValue(k, out var m)) perDayLine[k] = m = new Dictionary<string, int>(StringComparer.Ordinal);
                m[text] = (m.TryGetValue(text, out var c) ? c : 0) + 1;
            };
            double end = days * 1440; long ticks = 0; int lastMin = -1; dayCount += days;
            while (S.Clock < end && ticks < 20_000_000)
            {
                if (S.Phase == Phase.Trial)
                {
                    TrialSystem.RunHeadless(sim, true); Replay.BuildSegments(sim); Settlements.AfterReveal(sim);
                    if (S.Phase == Phase.LoopEpilogue) { Settlements.NextLoop(sim); S.Phase = Phase.Daily; }
                    if (S.Phase == Phase.Trial) break;
                    continue;
                }
                if (S.Phase == Phase.Investigation && !S.Flags.ContainsKey("testinv:" + S.Chapter + ":" + S.Loop))
                {
                    S.Flags["testinv:" + S.Chapter + ":" + S.Loop] = 1;
                    foreach (var inc in S.Incidents.Values.Where(i => i.Confirmed && i.Chapter == S.Chapter && i.Loop == S.Loop))
                    { var body = S.A(inc.Victim); Evidences.ExamineBody(sim, S.Player, body, true); }
                }
                sim.Step(); ticks++;
                int min = (int)Math.Floor(S.Clock);
                if (min != lastMin) { lastMin = min; try { LifePlayer(sim, rng, tableKinds); } catch (Exception e) { Console.WriteLine("  PLAYER EXC " + e.GetType().Name + " " + e.Message + " @ " + e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()); } }
                if (S.Out.Count > 2000) S.Out.Clear();
            }
            Simulation.LifeLineTrace = null;
            double F(string k) => S.Flags.TryGetValue(k, out var v) ? v : 0;
            for (int d = 1; d <= days; d++) pairsPerDay.Add((int)F("lps:day:" + d));
            pairTotal += (long)F("lps:total"); for (int d = 1; d <= days; d++) pairPlayer += (long)F("lps:seen:" + d);
            foreach (var k in S.Flags.Keys.Where(k => k.StartsWith("ltie:"))) tieStates.Add(k);
            foreach (var c in Cast.Npcs) { int h = (int)F("lh:" + c.Id); hearts += h; heartsPer[c.Id] = (heartsPer.TryGetValue(c.Id, out var o) ? o : 0) + h; }
            invites += (long)F("lhinv:total"); seeks += (long)F("lseek:total"); fests += (long)F("lfest:total"); festSeen += (long)F("lfest:seen");
            deaths += (long)F("lad:n"); memorials += S.Flags.Keys.Count(k => k.StartsWith("lmemorialheld:")); griefActs += (long)F("lgrief:acts"); inherit += (long)F("lgoal:inherit");
            tableDeath += S.Flags.Keys.Count(k => k.StartsWith("ltdeath:"));
            rumSeed += (long)F("lrum:seeded"); rumSpread += (long)F("lrum:spread"); rumDist += (long)F("lrum:distorted"); rumTrace += (long)F("lrum:traced"); rumFix += (long)F("lrum:corrected");
            hangs += S.Ledger.Count(e => e.Type == "HangScene"); gifts += S.Ledger.Count(e => e.Type == "GiftGiven" || e.Type == "LifeGift");
            foreach (var e in S.Ledger.Where(e => e.Type == "TableTopic")) { var p = (e.Data ?? "").Split(':'); if (p.Length > 1) tableKinds.Add(p[1] + (p.Length > 4 && p[4] == "player" ? "*" : "")); }
            foreach (var k in S.Flags.Keys.Where(k => k.StartsWith("lbanter:"))) banter[k.Substring(8)] = (banter.TryGetValue(k.Substring(8), out var b) ? b : 0) + (long)S.Flags[k];
            faults += sim.Faults;
            foreach (var l in S.DevLog.Where(x => x.Contains("EXC")).Take(6)) Console.WriteLine("   " + l);
            var json = SaveStore.Serialize(S); var back = SaveStore.Deserialize(json); bool same = SaveStore.Serialize(back) == json; roundtripOk &= same;
            Console.WriteLine($"  seed {seed}: clock={ClockFmt.DayHM(S.Clock)} loop={S.Loop} ch={S.Chapter} faults={sim.Faults} pairs={F("lps:total")} hearts={Cast.Npcs.Sum(c => F("lh:" + c.Id))} fest={F("lfest:total")} roundtrip={(same ? "IDENTICAL" : "DIFF")} t={sw.ElapsedMilliseconds}ms");
        }
        var maxes = perDayLine.Select(kv => (kv.Key, max: kv.Value.Values.Max(), top: kv.Value.OrderByDescending(x => x.Value).First().Key)).ToList();
        Console.WriteLine($"LIFE seeds={string.Join(",", seeds)} days={days}");
        Console.WriteLine($"  PAIR {pairTotal} total = {pairTotal / (double)Math.Max(1, dayCount):0.0}/day (per day: {string.Join(" ", pairsPerDay)}), in front of 민혁 {pairPlayer} = {pairPlayer / (double)Math.Max(1, dayCount):0.0}/day");
        Console.WriteLine($"  TABLE distinct topics {tableKinds.Count}: {string.Join(", ", tableKinds.OrderBy(x => x))}");
        Console.WriteLine($"  TIES distinct changed {tieStates.Count}: {string.Join(", ", tieStates.OrderBy(x => x).Take(24))}");
        Console.WriteLine($"  GRIEF deaths {deaths}: death tables {tableDeath}, memorials {memorials}, grief acts {griefActs}, inherited goals voiced {inherit}");
        Console.WriteLine($"  REPEAT max={maxes.Select(x => x.max).DefaultIfEmpty(0).Max()} resident-days>3={maxes.Count(x => x.max > 3)}/{maxes.Count}");
        foreach (var w in maxes.OrderByDescending(x => x.max).Take(4)) Console.WriteLine($"     {w.Key} ×{w.max}: {w.top}");
        Console.WriteLine($"  HEARTS {hearts} ({string.Join(" ", heartsPer.OrderBy(k => k.Key).Select(k => k.Key + ":" + k.Value))}) · INVITES heart {invites}, came to 민혁 {seeks} · HANGOUT moments {hangs} · gifts {gifts}");
        Console.WriteLine($"  FEST planned {fests}, staged for 민혁 {festSeen} · RUMOURS seeded {rumSeed} spread {rumSpread} distorted {rumDist} traced {rumTrace} corrected {rumFix}");
        Console.WriteLine($"  BANTER {string.Join(", ", banter.OrderByDescending(x => x.Value).Select(x => x.Key + "=" + x.Value))}");
        Console.WriteLine($"faults={faults} lint={lintFails} roundtrip={(roundtripOk ? "IDENTICAL" : "DIFF")} t={sw.ElapsedMilliseconds}ms");
        return faults == 0 && roundtripOk ? 0 : 1;
    }

    // ------------------------------------------------------------------ the scripted player (one decision per clock minute)
    static void LifePlayer(Simulation sim, Rng rng, HashSet<string> tableKinds)
    {
        var S = sim.S; var me = S.Player; if (me == null || !me.Alive || S.Phase != Phase.Daily) return;
        int m = S.Minute; if (m < 8 * 60 || m > 22 * 60) return;
        void Go(int room) { var r = S.Layout.Room(room); if (r == null) return; var p = sim.RandomPointIn(r, S.R(Stream.Presentation)); sim.SetPlayerPose(p, 0, false, false); }
        void GoTo(Actor a) { var p = sim.Snap(new P3(a.Pos.f, a.Pos.x + 0.9f, a.Pos.z)); sim.SetPlayerPose(p, 0, false, false); }
        void PlayStage(LifeStage st) { int g = 0; while (st != null && g++ < 12) { foreach (var u in st.Lines) sim.Spoken(u); if (st.Options == null || st.Options.Count == 0 || st.Done) break; st = sim.LifePick(rng.R(st.Options.Count)); } }
        void Talk(Actor a, string opt, string arg = null)
        {
            if (a == null || !a.Alive || !sim.CanTalk(a, out _)) return;
            S.K(Cast.Player).Facts.Add("met:" + a.Id); S.K(a.Id).Facts.Add("met:" + Cast.Player);   // (DialogueUI.Open does this on a first conversation)
            sim.BeginTalk(a);
            try
            {
                var lines = sim.Choose(a, opt, arg); foreach (var u in lines) sim.Spoken(u);
                for (int g = 0; g < 8; g++)
                {
                    var o = sim.Options(a); var picks = o.Where(x => x.Id == "lifepick").ToList(); var go = o.FirstOrDefault(x => x.Id == "lifego");
                    if (picks.Count > 0) { var pk = picks[rng.R(picks.Count)]; foreach (var u in sim.Choose(a, "lifepick", pk.Arg)) sim.Spoken(u); continue; }
                    if (go != null) { foreach (var u in sim.Choose(a, "lifego")) sim.Spoken(u); continue; }
                    break;
                }
            }
            finally { sim.EndTalk(a); }
        }
        // meals: sit with the diners (the table topic)
        if (m == Simulation.MealStart[0] + 6 || m == Simulation.MealStart[2] + 6)
        {
            var din = S.Layout.First(RoomType.Dining); if (din == null) return;
            Go(din.Id);
            var diners = S.LivingNpcs.Where(x => x.Room == din.Id && x.Status == ActorStatus.Active && x.Pose != Pose.Sleep).OrderBy(x => x.Id).Select(x => x.Id).Take(6).ToList();
            var st = diners.Count >= 2 ? sim.LifeTable(diners, m < 12 * 60 ? "breakfast" : "dinner") : null;
            if (st != null) PlayStage(st);
            return;
        }
        // appointments: be there on time (the heart plays when both are in the room)
        foreach (var r in (S.Requests ?? new List<Request>()).Where(r => r.Kind == "invite" && r.State == "accepted" && S.Clock >= r.At - 3 && S.Clock <= r.At + 15).OrderBy(r => r.At).Take(1))
        {
            var host = S.A(r.From); if (host == null || !host.Alive) continue;
            if (me.Room != r.Room) { if (host.Room == r.Room) GoTo(host); else Go(r.Room); }
            return;
        }
        sim.LifeCheckDue();
        // whoever has something to say (a heart at the appointment, a gift, jealousy, a rumour …)
        foreach (var a in S.LivingNpcs.OrderBy(x => x.Id).ToList())
        {
            if (sim.LifeGoKind(a.Id) == null || !sim.CanTalk(a, out _)) continue;
            if (a.Act != null && a.Act.Id != null && (a.Act.Id.StartsWith("life:seek") || a.Act.Id.StartsWith("req:")) || m % 10 == 0) { GoTo(a); Talk(a, "lifego"); return; }
        }
        // offered invitations: accept them
        foreach (var r in (S.Requests ?? new List<Request>()).Where(r => r.State == "offered").OrderBy(r => r.Id).Take(1))
        {
            var a = S.A(r.From); if (a == null) continue; GoTo(a); sim.BeginTalk(a); foreach (var u in sim.AnswerRequest(a, rng.Chance(0.85) ? "accept" : "refuse")) sim.Spoken(u); sim.EndTalk(a); return;
        }
        if (m % 20 != 0) return;
        // a festival running: go and join it
        var fest = (S.Gatherings ?? new List<Gathering>()).FirstOrDefault(g => g.Kind != null && (g.Kind.StartsWith("fest:") || g.Kind == "memorial") && !g.Done && !g.Cancelled && S.Clock >= g.Cur.Start && S.Clock < g.Cur.End - 15);
        if (fest != null && !S.Flags.ContainsKey("lfestseen:" + fest.Id)) { Go(fest.Cur.Room); var st = sim.LifeOffer(); if (st != null) PlayStage(st); return; }
        // walk into the busiest room: maybe a scene flares up there
        if (rng.Chance(0.5))
        {
            var busy = S.LivingNpcs.Where(x => x.Room >= 0 && x.Pose != Pose.Sleep).GroupBy(x => x.Room).Where(g => { var rr = S.Layout.Room(g.Key); return rr != null && !RoomInfo.IsPassage(rr.Type) && rr.Type != RoomType.Bedroom; }).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).FirstOrDefault();
            if (busy != null) { Go(busy.Key); var st = sim.LifeOffer(); if (st != null) { PlayStage(st); return; } }
        }
        // spend time with someone (a heart first if one is ready, else time together and its moment)
        var cands = S.LivingNpcs.Where(x => x.Status == ActorStatus.Active && x.Pose != Pose.Sleep && x.PlanId == null && sim.CanTalk(x, out _)).OrderBy(x => x.Id).ToList();
        if (cands.Count == 0) return;
        var npc = cands[rng.R(cands.Count)];
        GoTo(npc); S.K(Cast.Player).Facts.Add("met:" + npc.Id);
        if (sim.HeartReady(npc, out _) != null) { Talk(npc, "lifeheart"); return; }
        if (rng.Chance(0.2))
        {
            var pref = Gifts.Get(npc.Id);
            if (pref != null && pref.Loved.Length > 0 && ItemCatalog.Get(pref.Loved[0]) != null)
            {
                var it = new Item { Id = S.NewId("it"), Type = pref.Loved[0], Name = ItemCatalog.Get(pref.Loved[0]).Kor, Pos = me.Pos, Room = me.Room };
                S.Items[it.Id] = it; sim.PickUp(me, it); Talk(npc, "gift", it.Id); return;
            }
        }
        string kind = rng.Chance(0.5) ? "talk_long" : rng.Chance(0.5) ? "join" : "tea";
        var opts = sim.TogetherOptions(npc); var o = opts.FirstOrDefault(x => x.Id == kind && x.Enabled) ?? opts.FirstOrDefault(x => x.Enabled);
        if (o == null) { Talk(npc, "chat"); return; }
        var res = sim.PlayerTogether(npc, o.Id);
        if (res != null && !res.Interrupted) { var tp = res.Activity; if (sim.LifeAfterTogether(npc.Id, o.Id, npc.Act?.Id?.StartsWith("life:") == true ? npc.Act.Id.Split(':')[1] : null, true)) Talk(npc, "lifego"); }
    }

    // ------------------------------------------------------------------ content lint
    static readonly Regex _slot = new Regex(@"\{[a-z!]+(?::[가-힣]+)?\}");
    static int LifeLint()
    {
        LifeData.Ensure(); int fails = 0;
        foreach (var e in LifeData.Errors) { Console.WriteLine("  LIFE LOAD ERROR " + e); fails++; }
        Console.WriteLine($"  content: pairs={LifeData.Pairs.Count} hearts={LifeData.Hearts.Count} hangouts={LifeData.Hangs.Count} quiz={LifeData.Quiz.Count}");
        Console.WriteLine("  hearts per resident: " + string.Join(" ", Cast.Npcs.Select(c => c.Id + ":" + LifeData.Hearts.Count(h => h.Npc == c.Id))));
        Console.WriteLine("  hangouts per resident: " + string.Join(" ", Cast.Npcs.Select(c => c.Id + ":" + LifeData.Hangs.Count(h => h.Npc == c.Id))));
        var pairKinds = LifeData.Pairs.GroupBy(p => p.PairKind).Select(g => g.Key + "=" + g.Count()); Console.WriteLine("  pair kinds: " + string.Join(", ", pairKinds));
        void Check(string where, string who, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            string plain = _slot.Replace(text, "OOOO");
            foreach (var page in plain.Split('|')) if (page.Trim().Length > 60) { Console.WriteLine($"  LINT long page ({page.Trim().Length}) {where}: {page.Trim()}"); fails++; }
            if (text.Contains("재판")) { Console.WriteLine($"  LINT 재판 {where}: {text}"); fails++; }
            if (Regex.IsMatch(text, @"\([^)]*[가-힣][^)]*\)") && !text.Contains("(를)") && !text.Contains("(을)") && !text.Contains("(과)") && !text.Contains("(가)") && !text.Contains("(는)") && !text.Contains("(으)")) { Console.WriteLine($"  LINT brackets {where}: {text}"); fails++; }
            if (who != null && who != "me" && who != Cast.Player)
            {
                string bare = Regex.Replace(text, @"'[^']*'|‘[^’]*’|“[^”]*”", "");   // imitating someone in quotes is allowed
                foreach (var kv in CastTraits.TicOwner)
                {
                    if (kv.Value == who) continue; int i = bare.IndexOf(kv.Key, StringComparison.Ordinal); if (i < 0) continue;
                    if (kv.Key == "그러니까" && !bare.StartsWith("그러니까") && !bare.Contains(". 그러니까") && !bare.Contains("|그러니까")) continue;
                    if (kv.Key.Length == 1 || kv.Key == "헐") { int j = i + kv.Key.Length; if (j < bare.Length && bare[j] >= 0xAC00 && bare[j] <= 0xD7A3) continue; }
                    Console.WriteLine($"  LINT tic '{kv.Key}' (owner {kv.Value}) said by {who} {where}: {text}"); fails++;
                }
            }
        }
        foreach (var sc in LifeData.Pairs.Cast<LScene>().Concat(LifeData.Hearts).Concat(LifeData.Hangs))
        {
            foreach (var l in sc.AllLines()) { Check(sc.Id, l.Who, l.Text); Check(sc.Id, l.Who, l.TextC); }
            foreach (var o in sc.AllOpts()) { Check(sc.Id + " opt", "me", o.Label); Check(sc.Id + " opt", "me", o.LabelC); Check(sc.Id + " later", sc.Npc, o.CallbackLine); }
            if (sc is HeartDef h) { Check(sc.Id + " ask", h.Npc, h.Invite); Check(sc.Id + " ask", h.Npc, h.InviteC); }
            if (sc.First == null) { Console.WriteLine("  LINT empty scene " + sc.Id); fails++; }
            foreach (var b in sc.Beats) if (b.Next != null && sc.Beat(b.Next) == null) { Console.WriteLine($"  LINT {sc.Id} beat {b.Id} → missing {b.Next}"); fails++; }
            foreach (var o in sc.AllOpts()) if (o.NextBeat != null && o.NextBeat != "!end" && sc.Beat(o.NextBeat) == null) { Console.WriteLine($"  LINT {sc.Id} option → missing beat {o.NextBeat}"); fails++; }
            // romance guardrail in authored scenes
            if (sc is PairDef pd && pd.PairKind == "flirt" && (Simulation.NoRomance(pd.A) || Simulation.NoRomance(pd.B))) { Console.WriteLine("  LINT guardrail flirt scene with P02/P12/P17: " + sc.Id); fails++; }
        }
        foreach (var c in Cast.Participants.Select(c => c.Id).Concat(new[] { "ANY" }))
            foreach (var key in LifeKeys)
                foreach (var cas in new[] { false, true })
                {
                    var pool = LineBank.Pool(c, key, cas, out var used); if (pool == null || used != cas) continue;
                    foreach (var t in pool) Check(c + ":" + key, c == "ANY" ? null : c, t);
                }
        foreach (var c in Cast.Npcs.Select(x => x.Id)) foreach (var rk in new[] { "dirty_joke", "flirt", "love_hint", "flirt_react", "react_dirty" })
            if (Simulation.NoRomance(c) && (LineBank.Variants(c, rk) > 0)) Console.WriteLine($"  NOTE {c} has {rk} lines — the code never fires them (guardrail)");
        Console.WriteLine($"  lint fails={fails}");
        return fails;
    }
}
