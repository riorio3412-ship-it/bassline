using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BL23.Sim;

/// <summary>
/// Rendered-text dump ("textdump [outDir] [days] [seed,seed,…]"): plays headless campaigns (default seeds 20260926, 777, 4242
/// for 3 days each, smart trial player) and writes every DISTINCT player-facing string the kernel produced to
/// outDir/&lt;category&gt;.txt, deduplicated, with a count and one example source key per string. Many awkward sentences are
/// generated (templates + slots + particle fixing), so this is what the player actually reads, not the raw templates.
///   · world events (S.Out: speech / barks / notices / announcements …) drained every tick
///   · dialogue: each resident probed through the real conversation API (Options → Choose) on save/load clones every few
///     clock hours (so the campaign itself is not disturbed), investigation questions asked for real on the main run
///   · evidence cards (title / headline / description / can / cannot / source / fact sentences), statements heard
///   · trial: every beat of the smart run (main) plus three clones (probe: presses, wrong seals, sources, support;
///     naive and passive via TrialDump's TdPlay), minigame titles / questions / plates / slots / narration / lines,
///     reconstruction questions + options, verdict summary
///   · reveal script (Replay.Script) + PlanProse for every grammar, activity labels, requests, goals, clue labels,
///     furniture actions + activity captions, static phrase tables (wounds, sounds, topics, tricks)
/// Game-side strings (IntentLines, trial UI buttons) are not reachable from the kernel and are not included.
/// </summary>
public static partial class Program
{
    sealed class TxHit { public int N; public string Src; public string Seed; }
    static readonly Dictionary<string, Dictionary<string, TxHit>> _tx = new Dictionary<string, Dictionary<string, TxHit>>();
    static string _txSeed = "";
    static readonly HashSet<string> _txEvSeen = new HashSet<string>();

    static bool TxHangul(string s) { foreach (var ch in s) if (ch >= 0xAC00 && ch <= 0xD7A3) return true; return false; }

    static void Tx(string cat, string src, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        text = LineBank.FixParticles(text.Trim());
        if (!TxHangul(text)) return;
        if (!_tx.TryGetValue(cat, out var d)) _tx[cat] = d = new Dictionary<string, TxHit>(StringComparer.Ordinal);
        if (!d.TryGetValue(text, out var h)) d[text] = h = new TxHit { Src = src ?? "-", Seed = _txSeed };
        h.N++;
    }

    /// <summary>What the trial UI does to kernel text before showing it (CluePicker.Plain).</summary>
    static string TxPlain(string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        s = Regex.Replace(s, @"\s*\[LR\d+\]", "");
        return s.Replace(" (전해 들은 내용이라 조건부)", " (전해 들은 말이라 확정할 수 없다)").Replace("자료", "단서").Replace("조건부", "확인 필요").Replace("범위 제한", "흔들림");
    }

    static int TextDump(string[] args)
    {
        string outDir = args.Length > 1 ? args[1] : @"C:\Users\리오\BL23Lab\textdump";
        int days = args.Length > 2 ? int.Parse(args[2]) : 3;
        var seeds = args.Length > 3 ? args[3].Split(',').Select(ulong.Parse).ToArray() : new[] { 20260926UL, 777UL, 4242UL };
        Directory.CreateDirectory(outDir);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var seed in seeds)
        {
            _txSeed = seed.ToString(); _txEvSeen.Clear();
            try { TxCampaign(seed, days); }
            catch (Exception e) { Console.WriteLine($"seed {seed} FAILED: {e}"); }
            Console.WriteLine($"seed {seed} done t={sw.ElapsedMilliseconds}ms categories={_tx.Count} strings={_tx.Values.Sum(d => d.Count)}");
        }
        TxStatic();
        TxLineBankAll();
        // write
        foreach (var f in Directory.GetFiles(outDir, "*.txt")) File.Delete(f);
        var index = new StringBuilder();
        index.AppendLine($"TEXT DUMP seeds={string.Join(",", seeds)} days={days} — {DateTime.Now:yyyy-MM-dd HH:mm}");
        foreach (var kv in _tx.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {kv.Key} — {kv.Value.Count} distinct strings, {kv.Value.Values.Sum(h => h.N)} occurrences. Format: count | source | text   (⏎ = newline, '|' kept as page break)");
            foreach (var e in kv.Value.OrderBy(x => x.Value.Src, StringComparer.Ordinal).ThenBy(x => x.Key, StringComparer.Ordinal))
                sb.AppendLine($"{e.Value.N,5} | {e.Value.Src} | {e.Key.Replace("\r", "").Replace("\n", "⏎")}");
            File.WriteAllText(Path.Combine(outDir, kv.Key + ".txt"), sb.ToString(), new UTF8Encoding(false));
            index.AppendLine($"  {kv.Key,-28} {kv.Value.Count,6} distinct {kv.Value.Values.Sum(h => h.N),8} total");
        }
        File.WriteAllText(Path.Combine(outDir, "_index.txt"), index.ToString(), new UTF8Encoding(false));
        TxLint(outDir);
        Console.Write(index.ToString());
        Console.WriteLine($"textdump → {outDir}  time={sw.ElapsedMilliseconds}ms");
        return 0;
    }

    // ------------------------------------------------------------------ mechanical lint over everything dumped
    static (string name, Regex rx)[] TxLintRules => new (string name, Regex rx)[]
    {
        ("unresolved particle pair", new Regex(@"이\(가\)|가\(이\)|을\(를\)|를\(을\)|은\(는\)|와\(과\)|\(으\)로|\(을\)를|\(이\)")),
        ("empty slot (…)", new Regex(@"…(에서|에|을|를|이|가|은|는|의)\b|[,\.] …\.|\s…[에을를]")),
        ("raw id / code word", new Regex(@"@P\d\d|\bP\d\d\b|\b(HandR|HandL|Head|Neck|Chest|ArmL|ArmR|LegL|LegR|Rain|Static|Clock|Announcement|Scream|Talk)\b")),
        ("doubled time hedge", new Regex(@"쯤 무렵|전 무렵|넘어 무렵|쯤쯤|넘어서이|넘어이|까지에 |넘어서엔|전 사이|쯤 사이|넘어 사이")),
        ("label + 중", new Regex(@"(감|음|중|로|다|함|림|짐) 중(이|\b)")),
        ("honorific slot", new Regex(@"씨 씨|님 씨|범인 씨|범인께서")),
        ("self-owned name (X이 X의)", new Regex(@"([가-힣]{2})[이가은는], ?\1의|([가-힣]{2}), \2의|(\b[가-힣]{2})[이가] \3의")),
        ("stacked 의", new Regex(@"[가-힣]+의 [가-힣]+의 [가-힣]+의|[가-힣]+의 [가-힣 ]+ 아래의 ")),
        ("spacing: 발 소리/말 소리", new Regex(@"발 소리|말 소리|울음 소리|웃음 소리")),
        ("noun + 하(spacing)", new Regex(@"(수영|요리|독서|정리|산책|게임|이야기|수다|연습 공연|운동|모임|청소) (하|할|했|해)")),
    };
    static Regex TxParticle => new Regex(@"([가-힣])(을|를|가|와|으로|로|은)(?=$|[\s\.,!?…'’」\)\|—~])");

    static void TxLint(string outDir)
    {
        var sb = new StringBuilder(); var hits = new Dictionary<string, List<string>>(); var rules = TxLintRules; var prx = TxParticle;
        void Hit(string rule, string line) { if (!hits.TryGetValue(rule, out var l)) hits[rule] = l = new List<string>(); if (l.Count < 400) l.Add(line); }
        foreach (var cat in _tx)
            foreach (var kv in cat.Value)
            {
                string t = kv.Key, where = $"[{cat.Key}] {kv.Value.Src} | {t.Replace("\n", "⏎")}";
                foreach (var (name, rx) in rules) if (rx.IsMatch(t)) Hit(name, where);
                foreach (Match m in prx.Matches(t))
                {
                    char c = m.Groups[1].Value[0]; string p = m.Groups[2].Value;
                    int jong = (c - 0xAC00) % 28; bool b = jong != 0, rieul = jong == 8;
                    bool bad = (p == "를" || p == "가" || p == "와") && b || (p == "을" || p == "은") && !b || p == "으로" && (!b || rieul) || p == "로" && b && !rieul;
                    // skip common false positives (verbs/nouns that merely end in these syllables)
                    if (bad && Regex.IsMatch(t.Substring(Math.Max(0, m.Index - 2), Math.Min(t.Length - Math.Max(0, m.Index - 2), m.Length + 2)), "마을|가을|노을|고을|누가|어디가|새로|대로|바로|서로|멋대로|따로|저절로|스스로|홀로|제대로|마음대로|이대로|그대로|정말로|진짜로|실제로|억지로|함부로|절대로|거꾸로|반대로|새로|말로|오히려|날로|결코|주로|하도록|도록|으로|처음으로")) bad = false;
                    if (bad) Hit("particle mismatch (" + p + ")", where);
                }
            }
        foreach (var kv in hits.OrderBy(k => k.Key, StringComparer.Ordinal)) { sb.AppendLine($"## {kv.Key} — {kv.Value.Count}"); foreach (var l in kv.Value) sb.AppendLine("  " + l); }
        File.WriteAllText(Path.Combine(outDir, "_lint.txt"), sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("lint: " + string.Join(", ", hits.Select(k => k.Key + "=" + k.Value.Count)));
    }

    static Simulation TxClone(Simulation sim) { var s2 = Simulation.FromState(SaveStore.Deserialize(SaveStore.Serialize(sim.S))); s2.Headless = true; if (s2.S.Out == null) s2.S.Out = new List<GameEvent>(); s2.S.Out.Clear(); return s2; }

    // ------------------------------------------------------------------ the campaign
    static void TxCampaign(ulong seed, int days)
    {
        var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
        double endClock = days * 1440; long ticks = 0; string invKey = null; int annIdx = 0; double nextProbe = 9 * 60; bool actProbe = false; int trials = 0;
        while (S.Clock < endClock && ticks < 20_000_000)
        {
            if (S.Phase == Phase.Trial && S.Trial != null)
            {
                trials++;
                TxCards(sim); TxTrial(sim);
                TxDrain(sim, "trial");
                Replay.BuildSegments(sim);
                foreach (var seg in S.Replays.Where(r => S.Incidents.TryGetValue(r.Incident, out var inc) && inc.Chapter == S.Chapter && inc.Loop == S.Loop))
                    foreach (var line in Replay.Script(S, seg)) Tx("replay_script", "Replay.Script", line.text);
                Settlements.AfterReveal(sim);
                TxDrain(sim, "after");
                if (S.Phase == Phase.LoopEpilogue) { Settlements.NextLoop(sim); S.Phase = Phase.Daily; }
                if (S.Phase == Phase.Trial) break;   // stuck: do not spin
                continue;
            }
            if (S.Phase == Phase.Investigation && invKey != S.Loop + ":" + S.Chapter)
            {
                invKey = S.Loop + ":" + S.Chapter;
                TxProbeTalk(sim, true);          // clone: every option, investigation register
                TxInvestigate(sim);              // main: examine + ask everyone for real (the trial needs it)
            }
            sim.Step(); ticks++;
            TxDrain(sim, null);
            while (annIdx < S.Announcements.Count) { var an = S.Announcements[annIdx++]; Tx("announce", "Announcement:" + (an.Key ?? an.Rule ?? "-"), an.Text); }
            if (ticks % 40 == 0)
                foreach (var a in S.LivingNpcs) if (a.Act != null && !string.IsNullOrEmpty(a.Act.Label)) Tx("activity_labels", "Act:" + TxActKey(a.Act.Id), a.Act.Label);
            if (S.Phase == Phase.Daily && S.Minute >= 9 * 60 && S.Minute <= 21 * 60 && S.Clock >= nextProbe)
            {
                nextProbe = S.Clock + 150;
                TxProbeTalk(sim, false);
                if (!actProbe && S.Minute >= 13 * 60) { actProbe = true; TxProbeActivities(sim); }
            }
        }
        // leftovers: cards of an open chapter, requests, goals, archive
        TxCards(sim);
        foreach (var r in S.Requests ?? new List<Request>()) Tx("requests", "Request:" + r.Kind, r.Text);
        foreach (var g in S.Goals.Values) Tx("goals", "Goal:" + g.Kind, g.Label);
        foreach (var a in S.Archive) Tx("archive", "Archive", a);
        foreach (var st in S.Settlements) Tx("verdict", "Settlement.Note", st.Note);
        Console.WriteLine($"  seed {seed}: ticks={ticks} trials={trials} clock={ClockFmt.DayHM(S.Clock)} faults={sim.Faults}");
    }

    static string TxActKey(string id)
    {
        if (id == null) return "-";
        var p = id.Split(':');
        if (p.Length >= 2 && (p[0] == "social" || p[0] == "req" || p[0] == "murder" || p[0] == "goal")) return p[0] + (p[0] == "social" && p.Length > 2 ? ":" + p[1] : "");
        return p.Length >= 2 ? p[0] + ":" + p[1] : p[0];
    }

    static void TxDrain(Simulation sim, string tag)
    {
        var S = sim.S; if (S.Out == null) return;
        foreach (var e in S.Out)
        {
            if (string.IsNullOrEmpty(e.Text)) continue;
            string cat;
            switch (e.Type)
            {
                case GameEventType.Speech: cat = e.Key == "bond" ? "dialogue" : "speech_barks"; break;
                case GameEventType.Notice: cat = "notices"; break;
                case GameEventType.Announcement: cat = "announce"; break;
                case GameEventType.Subtitle: cat = "subtitles"; break;
                default: cat = "events_other"; break;
            }
            string who = e.Actor == null ? "" : e.Actor == Cast.Butler ? "@Yusti" : "@" + Cast.GivenOf(e.Actor);
            Tx(cat, $"{e.Type}:{e.Key ?? "-"}{who}", e.Text);
        }
        S.Out.Clear();
        if (S.Trial != null && tag == "trial") { }
    }

    // ------------------------------------------------------------------ conversations
    static readonly HashSet<string> TxInvOpts = new HashSet<string> { "q_where", "q_saw", "q_heard", "q_suspect", "q_share", "show" };

    static void TxUtter(Simulation sim, string opt, Utterance u)
    {
        if (u == null) return;
        string cat = u.Speaker == Cast.Player ? "player_lines" : TxInvOpts.Contains(opt) ? "testimony" : "dialogue";
        string who = u.Speaker == Cast.Butler ? "Yusti" : Cast.GivenOf(u.Speaker);
        Tx(cat, $"{u.Key ?? "-"}@{who} ({opt})", u.Text);
    }

    /// <summary>On a clone: put a few loose things in 민혁's pockets, then go through every conversation option with every resident.</summary>
    static void TxProbeTalk(Simulation main, bool inv)
    {
        Simulation sim;
        try { sim = TxClone(main); } catch (Exception e) { Console.WriteLine("  clone failed: " + e.Message); return; }
        var S = sim.S; var P = S.Player; if (P == null || !P.Alive) return;
        var loose = S.Items.Values.Where(i => i.Holder == null && i.Def != null && !i.Def.Key && i.KeyFor == null && i.Type != "Invitation" && !i.Hidden && i.Room >= 0)
                                  .GroupBy(i => i.Type).Select(g => g.OrderBy(i => i.Id, StringComparer.Ordinal).First()).OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
        var rng = new Random((int)(S.Clock * 7) ^ S.Loop);
        foreach (var it in loose.OrderBy(_ => rng.Next()).Take(6)) { it.Holder = Cast.Player; it.Room = -1; P.Pocket.Add(it.Id); }
        // an owned thing of a random resident, so "giveback" has something to return
        var owned = S.Items.Values.Where(i => i.Holder == null && i.Owner != null && i.Owner != Cast.Player && i.Def != null && !i.Def.Key && i.KeyFor == null && i.Type != "Invitation").OrderBy(i => i.Id, StringComparer.Ordinal).Take(3).ToList();
        foreach (var it in owned) { it.Holder = Cast.Player; it.Room = -1; P.Pocket.Add(it.Id); }
        foreach (var b in new[] { S.Butler }) if (b != null) TxTalkAll(sim, b, inv);
        foreach (var npc in S.LivingNpcs.OrderBy(x => x.Id).ToList()) TxTalkAll(sim, npc, inv);
        // invitations / favours: every resident offers one, each answered one way
        int n = 0;
        foreach (var npc in S.LivingNpcs.OrderBy(x => x.Id).ToList())
        {
            try
            {
                var mk = typeof(Simulation).GetMethod(n % 3 == 0 ? "MakeFind" : n % 3 == 1 ? "MakeDeliver" : "MakeInvite", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                object req = mk.GetParameters().Length == 1 ? mk.Invoke(sim, new object[] { npc }) : mk.Invoke(sim, new object[] { npc, S.R(BL23.Sim.Stream.Presentation) });
                if (req == null) req = sim.ProbeOffer(npc); else S.Requests.Add((Request)req);
                if (req == null) continue;
                Tx("requests", "Request:" + ((Request)req).Kind, ((Request)req).Text);
                sim.BeginTalk(npc);
                foreach (var o in sim.Options(npc).Where(o => o.Id.StartsWith("req_"))) Tx("ui_options", "DialogueOption:" + o.Id, o.Label);
                string ans = n % 3 == 0 ? "req_accept" : n % 3 == 1 ? "req_refuse" : "req_later";
                foreach (var u in sim.Choose(npc, ans)) TxUtter(sim, ans, u);
                var rq = (Request)req;
                if (rq.State == "accepted" && (rq.Kind == "find" || rq.Kind == "deliver"))
                {
                    var it = S.I(rq.Item); var to = rq.Kind == "find" ? npc : S.A(rq.To);
                    if (it != null && to != null && to.Alive) { if (it.Holder != Cast.Player) { it.Holder = Cast.Player; it.Room = -1; if (!P.Pocket.Contains(it.Id)) P.Pocket.Add(it.Id); } var u = sim.RequestHandover(to, it); TxUtter(sim, "handover", u); }
                }
                sim.EndTalk(npc);
            }
            catch (Exception e) { Tx("_errors", "request", e.GetType().Name + " " + e.Message); }
            n++;
        }
        TxDrain(sim, null);
    }

    static void TxTalkAll(Simulation sim, Actor npc, bool inv)
    {
        var S = sim.S;
        if (!sim.CanTalk(npc, out var why)) { Tx("ui_misc", "CanTalk", why); return; }
        try
        {
            sim.BeginTalk(npc);
            var opts = sim.Options(npc);
            foreach (var o in opts) { Tx("ui_options", "DialogueOption:" + o.Id, o.Label); if (!o.Enabled) Tx("ui_options", "DialogueOption.Why:" + o.Id, o.Why); }
            foreach (var o in opts.ToList())
            {
                if (o.Id == "bye" || o.Id.StartsWith("req_")) continue;
                if (!o.Enabled) continue;
                if (o.Id == "bond")
                {
                    foreach (var u in sim.Choose(npc, "bond")) TxUtter(sim, "bond", u);
                    var pend = sim.BondPending(npc); if (pend != null) foreach (var u in sim.Choose(npc, "bondpick", "0")) TxUtter(sim, "bondpick", u);
                    continue;
                }
                int reps = o.Id == "chat" || o.Id == "likes" || o.Id == "compliment" || o.Id == "tease" || o.Id == "gossip" ? 2 : 1;
                var args = o.Sub != null && o.Sub.Count > 0 ? o.Sub.Select(s => s.id).Take(o.Id == "gift" ? 4 : o.Id == "warn" ? 2 : 1).ToList() : new List<string> { null };
                foreach (var a in args)
                    for (int k = 0; k < reps; k++)
                        foreach (var u in sim.Choose(npc, o.Id, a)) TxUtter(sim, o.Id, u);
            }
            foreach (var u in sim.Choose(npc, "bye")) TxUtter(sim, "bye", u);
            sim.EndTalk(npc);
        }
        catch (Exception e) { Tx("_errors", "talk:" + npc.Id, e.GetType().Name + " " + e.Message + " " + e.StackTrace?.Split('\n').FirstOrDefault()); }
    }

    /// <summary>Main run: examine the scene like a thorough player, then ask every resident where/saw/heard/suspect/share (Choose → Spoken).</summary>
    static void TxInvestigate(Simulation sim)
    {
        var S = sim.S;
        foreach (var inc in S.Incidents.Values.Where(i => i.Confirmed && i.Chapter == S.Chapter && i.Loop == S.Loop).ToList())
        {
            var body = S.A(inc.Victim); if (body != null) TxExam(sim, body, "body");
            foreach (var t in S.Traces.Where(t => !t.Cleaned && t.Visibility <= 1 && (t.Room == inc.FoundRoom || t.Room == inc.CauseRoom || t.Victim == inc.Victim)).ToList()) TxExam(sim, t, "trace");
            if (inc.FoundRoom >= 0) Evidences.ExamineRoomQuick(sim, S.Player, inc.FoundRoom);
            var wid = inc.Weapon ?? (inc.PlanId != null && S.Plans.TryGetValue(inc.PlanId, out var pl) ? pl.Weapon : null);
            var w = wid != null && S.Items.ContainsKey(wid) ? S.I(wid) : null; if (w != null && w.Holder == null) TxExam(sim, w, "item");
            foreach (var d in S.Layout.Doors.Where(d => d.RoomA == inc.FoundRoom || d.RoomB == inc.FoundRoom).Take(2).ToList()) TxExam(sim, d, "door");
            foreach (var f in S.Layout.Furniture.Where(f => f.Room == inc.FoundRoom).Take(4).ToList()) TxExam(sim, f, "furniture");
        }
        foreach (var it in S.Items.Values.Where(i => i.Holder == null && i.Room >= 0 && i.Def != null).OrderBy(i => i.Id, StringComparer.Ordinal).Take(6).ToList()) TxExam(sim, it, "item");
        foreach (var npc in S.LivingNpcs.OrderBy(x => x.Id).ToList())
        {
            if (!sim.CanTalk(npc, out var why)) { Tx("ui_misc", "CanTalk", why); continue; }
            sim.BeginTalk(npc);
            foreach (var q in new[] { "q_where", "q_saw", "q_heard", "q_suspect", "q_share" })
                foreach (var u in sim.Choose(npc, q)) { TxUtter(sim, q, u); sim.Spoken(u); }
            var mine = S.K(Cast.Player).Evidence.Where(e => e.Chapter == S.Chapter && e.Loop == S.Loop && !e.Hidden).OrderByDescending(e => e.Acquired).FirstOrDefault();
            if (mine != null) foreach (var u in sim.Choose(npc, "show", mine.Id)) { TxUtter(sim, "show", u); sim.Spoken(u); }
            sim.EndTalk(npc);
        }
        foreach (var c in CaseProgress.Clues(sim)) Tx("clue_labels", "CaseProgress.Clue:" + c.Key.Split(':')[0], c.Label);
    }

    static void TxExam(Simulation sim, object target, string what)
    {
        try
        {
            var ev = sim.PlayerExamine(target);
            if (ev != null && ev.Loose) { Tx("examine_captions", "Examine(loose):" + what, ev.Title); Tx("examine_captions", "Examine(loose).Desc:" + what, ev.Desc); Tx("examine_captions", "Examine(loose).Line:" + what, ev.Line); }
        }
        catch (Exception e) { Tx("_errors", "examine:" + what, e.GetType().Name + " " + e.Message); }
    }

    /// <summary>On a clone: 민혁 spends time with each kind of furniture (captions, company lines, lore).</summary>
    static void TxProbeActivities(Simulation main)
    {
        Simulation sim; try { sim = TxClone(main); } catch { return; }
        var S = sim.S; var done = new HashSet<string>(); int n = 0;
        foreach (var f in S.Layout.Furniture.OrderBy(f => f.Id).ToList())
        {
            var acts = sim.FurnitureActions(f); if (acts.Count == 0) continue;
            var room = S.Layout.Room(f.Room); if (room == null || room.Type == RoomType.Courtroom || room.Type == RoomType.Bedroom && room.Owner != Cast.Player) continue;
            foreach (var a in acts)
            {
                Tx("ui_options", "FurnitureAction:" + a.Id, a.Label);
                if (!done.Add(a.Id + ":" + f.Type) || n > 90) continue;
                if (S.Phase != Phase.Daily) break;
                var p = S.Player; p.Pos = sim.SnapPublic(new P3(f.Pos.f, f.Pos.x + 0.9f, f.Pos.z + 0.4f)); p.Room = f.Room;
                if (S.Minute > 21 * 60 || S.Minute < 8 * 60) sim.Wait(Math.Max(10, (1440 - S.Minute + 9 * 60) % 1440));
                try
                {
                    var r = sim.PlayerUse(f, a.Id); n++;
                    Tx("activity_captions", "PlayerUse:" + a.Id + "/" + f.Type, r.Text);
                    Tx("activity_captions", "PlayerUse.JoinLine:" + a.Id + (r.Joined != null ? "@" + Cast.GivenOf(r.Joined) : ""), r.JoinLine);
                    if (r.LoreTitle != null) Tx("lore", "Lore:" + r.LoreTitle, r.LoreTitle + " — " + r.LoreText);
                }
                catch (Exception e) { Tx("_errors", "use:" + a.Id, e.GetType().Name + " " + e.Message); }
                TxDrain(sim, null);
            }
        }
        foreach (var d in S.Layout.Doors.Take(3)) foreach (var act in new[] { "open", "lock", "knock" }) { try { Tx("ui_misc", "PlayerDoor:" + act, sim.PlayerDoor(d, act)); } catch { } }
        TxDrain(sim, null);
    }

    // ------------------------------------------------------------------ evidence
    static void TxCards(Simulation sim)
    {
        var S = sim.S;
        foreach (var kv in S.Know)
        {
            bool mine = kv.Key == Cast.Player;
            foreach (var e in kv.Value.Evidence)
            {
                if (!_txEvSeen.Add(kv.Key + ":" + e.Id)) continue;
                string src = $"Evidence:{e.Kind}:{(e.Root ?? "-").Split(':')[0]}{(mine ? "" : " (npc)")}";
                Tx("evidence_title", src, e.Title);
                Tx("evidence_line", src, e.Line);
                Tx("evidence_desc", src, e.Desc);
                Tx("evidence_can_cannot", src + " can", e.CanKnow);
                Tx("evidence_can_cannot", src + " cannot", e.CannotKnow);
                Tx("evidence_source", src, e.Source);
                foreach (var p in e.Props) { try { Tx("evidence_facts", $"PropText:{p.Kind}", Evidences.PropText(sim, p)); } catch { } }
            }
            if (mine) foreach (var st in kv.Value.Statements.Skip(Math.Max(0, kv.Value.Statements.Count - 400))) if (_txEvSeen.Add("st:" + st.Id)) Tx("statements_heard", $"Statement:{st.Prop?.Kind}{(st.Hearsay ? " (overheard)" : "")}@{Cast.GivenOf(st.Speaker)}", st.Text);
        }
    }

    // ------------------------------------------------------------------ trial
    static void TxTrial(Simulation sim)
    {
        var S = sim.S; string json = SaveStore.Serialize(S);
        var inc = TrialSystem.TargetIncident(S); string culprit = S.Ch.TargetCulprit ?? inc?.Culprit;
        foreach (var b in TrialGames.Arsenal(sim)) { Tx("trial_cards", "Arsenal:" + b.Group, b.Title); Tx("trial_cards", "Arsenal.Desc:" + b.Group, b.Desc); }
        // clones first (they start from the same snapshot)
        foreach (var pol in new[] { "probe", "naive", "passive" })
        {
            try
            {
                var s2 = Simulation.FromState(SaveStore.Deserialize(json)); s2.Headless = true; s2.S.Out?.Clear();
                if (pol == "probe") TxTrialDrive(s2, false, true);
                else { var run = TdPlay(s2, pol, culprit); foreach (var rd in run.Rounds) TxGame(s2.S, rd.G); }
                TxBeats(s2, pol);
                TxDrain(s2, "trial");
            }
            catch (Exception e) { Tx("_errors", "trial:" + pol, e.GetType().Name + " " + e.Message + " " + e.StackTrace?.Split('\n').FirstOrDefault()); }
        }
        TxTrialDrive(sim, true, false);
        TxBeats(sim, "smart");
    }

    /// <summary>RunHeadless(smart) with the round contents captured; probe=true also presses, misses, asks sources and supports.</summary>
    static void TxTrialDrive(Simulation sim, bool smart, bool probe)
    {
        var S = sim.S; int guard = 0; var seenGames = new HashSet<TrialGame>();
        while (S.Trial != null && !S.Trial.Finished && guard++ < 700)
        {
            var T = S.Trial;
            if (T.PendingPrompt != null)
            {
                string p = T.PendingPrompt;
                if (p == "vote") { TrialSystem.PlayerVote(sim, TrialSystem.AutoVote(sim, smart)); T.PendingPrompt = null; continue; }
                if (p.StartsWith("game:") || p == "accuse")
                {
                    var G = T.Game; if (G != null && seenGames.Add(G)) TxGame(S, G);
                    if (probe && G != null && G.Status == "open") TxProbeRound(sim, G);
                    if (T.PendingPrompt == p) TrialGames.AutoResolve(sim, smart || !probe);
                    if (G != null) TxGame(S, G);
                    continue;
                }
                if (probe)
                {
                    var arsenal = TrialGames.Arsenal(sim).Select(b => b.Id).ToList();
                    if (p.StartsWith("witness:") || p.StartsWith("chain:") || p == "theory")
                    {
                        var ids = new List<string>();
                        if (p.StartsWith("witness:")) ids.Add(p.Substring(8));
                        else if (p.StartsWith("chain:")) { var c0 = T.Claims.FirstOrDefault(c => c.Id == p.Substring(6)); if (c0 != null) { ids.AddRange(c0.Premises); ids.Add(c0.Id); } }
                        else ids.AddRange(T.Claims.Where(c => c.Accused != null && c.Status == "open").Select(c => c.Id).Take(2));
                        T.PendingPrompt = null;
                        if (ids.Count > 0) TrialSystem.PlayerAskSource(sim, ids[0]);
                        if (ids.Count > 1 && arsenal.Count > 0) TrialSystem.PlayerSupport(sim, ids[1], arsenal[0]);
                        continue;
                    }
                    if (p == "rebut" && arsenal.Count > 0) { T.PendingPrompt = null; TrialSystem.PlayerPresent(sim, arsenal[arsenal.Count - 1]); continue; }
                    if (p == "defense") { TrialSystem.PlayerDefense(sim, arsenal.FirstOrDefault()); continue; }
                }
                T.PendingPrompt = null; continue;
            }
            var b = TrialSystem.Next(sim); if (b == null) { if (T.PendingPrompt != null) continue; break; }
            if (b.Options != null) foreach (var o in b.Options) Tx("trial_options", $"Beat.Options:{b.Kind}/{b.Key ?? "-"}", TxPlain(o));
            if (smart && b.ClaimId != null && b.Kind == "line")
            {
                var c = T.Claims.FirstOrDefault(x => x.Id == b.ClaimId);
                if (c != null && c.Speaker != Cast.Player && c.Status == "open")
                    foreach (var ev in S.K(Cast.Player).Evidence.ToList())
                        if (ev.Props.Any(pp => Logic.Check(S, c.Prop, pp, ev.Direct, ev.Root).Result == LogicResult.Contradict)) { TrialSystem.PlayerContradict(sim, c.Id, ev.Id); break; }
            }
        }
        var TT = S.Trial;
        if (TT != null)
        {
            foreach (var q in TT.RQ) { Tx("trial_reconstruct", "RQ:" + q.Id, q.Question); for (int i = 0; i < q.Options.Count; i++) { string lab; try { lab = TrialGames.OptionLabel(S, q, i); } catch { lab = q.Options[i]; } Tx("trial_reconstruct", "RQ.Option:" + q.Id, lab); } }
            if (TT.Game != null) TxGame(S, TT.Game);
        }
        Tx("verdict", "LastVerdictSummary", S.LastVerdictSummary);
    }

    /// <summary>Probe clone: press every line, stamp a wrong card, pick a wrong plate/pair — the misses, retorts and lapses get played.</summary>
    static void TxProbeRound(Simulation sim, TrialGame G)
    {
        var S = sim.S; var arsenal = TrialGames.Arsenal(sim).Select(b => b.Id).ToList();
        try
        {
            switch (G.Kind)
            {
                case "inquiry":
                    for (int i = 0; i < G.Lines.Count && G.Status == "open"; i++) foreach (var (who, text) in TrialGames.Press(sim, i)) Tx("trial_press", "Press@" + (who == null ? "court" : who == Cast.Butler ? "Yusti" : Cast.GivenOf(who)), TxPlain(text));
                    for (int i = 0; i < G.Lines.Count && G.Status == "open"; i++)
                    {
                        if (G.Lines[i].ClaimId == null) continue;
                        var wrong = arsenal.FirstOrDefault(id => !TrialGames.ProbeWorks(S, G.Lines[i].ClaimId, false, id));
                        if (wrong == null) continue;
                        var r = TrialGames.Seal(sim, i, wrong, false); TxShot(r, "Seal(miss)");
                        if (G.Misses >= 2) break;
                    }
                    break;
                case "ledger": if (G.Lines.Count > 0 && G.Lines2.Count > 0 && !TrialGames.ProbeLedger(S, 0, 0)) TxShot(TrialGames.LedgerPick(sim, 0, 0), "LedgerPick(miss)"); break;
                case "question": { var w = G.Pool.FirstOrDefault(x => x != G.Word); if (w != null) TrialGames.QuestionPick(sim, w); break; }
                case "board":
                    if (G.Why != "final" && arsenal.Count > 0)
                    {
                        var L = G.Final; var wrong = L?.ClaimId != null ? arsenal.FirstOrDefault(id => !TrialGames.ProbeWorks(S, L.ClaimId, false, id)) : null;
                        if (wrong != null) TxShot(TrialGames.BoardTie(sim, -1, wrong), "BoardTie(miss)");
                    }
                    break;
            }
        }
        catch (Exception e) { Tx("_errors", "probe:" + G.Kind, e.GetType().Name + " " + e.Message); }
    }

    static void TxShot(TrialGames.ShotResult r, string src)
    {
        if (r == null) return;
        Tx("trial_shots", src + ".Text", TxPlain(r.Text)); Tx("trial_shots", src + ".Retort", TxPlain(r.Retort)); Tx("trial_shots", src + ".Steer", TxPlain(r.Steer)); Tx("trial_shots", src + ".Seal", r.Seal);
    }

    static void TxGame(GameState S, TrialGame G)
    {
        if (G == null) return;
        string src = "Game:" + G.Kind + "/" + (G.Kind == "board" || G.Kind == "inquiry" ? G.Why ?? "-" : G.Topic ?? "-");
        Tx("trial_game", src + " Title", G.Title); Tx("trial_game", src + " Subtitle", G.Subtitle); Tx("trial_game", src + " Question", G.Question);
        Tx("trial_game", src + " Outcome", G.Outcome);
        foreach (var w in G.Pool) Tx("trial_game", src + " Plate", w);
        foreach (var n in G.Narration) Tx("trial_game", src + " Narration", TxPlain(n));
        foreach (var sl in G.Slots) { Tx("trial_game", src + " Slot.Label", sl.Label); Tx("trial_game", src + " Slot.Hint", sl.Hint); Tx("trial_game", src + " Slot.Why", TxPlain(sl.Why)); Tx("trial_game", src + " Slot.Result", sl.Result); foreach (var pc in sl.Pieces) Tx("trial_game", src + " Slot.Piece", TrialGames.BulletEvidence(S, pc)?.Title ?? pc); }
        foreach (var l in G.Lines.Concat(G.Lines2).Concat(G.Final != null ? new[] { G.Final } : new GameLine[0]))
        {
            Tx("trial_game_lines", $"{src} Line:{l.Kind ?? "talk"}@{Cast.GivenOf(l.Speaker)}", TxPlain(l.Text));
            if (l.WeakAt >= 0 && l.Text != null && l.WeakAt + l.WeakLen <= l.Text.Length) Tx("trial_game_weak", src + " Weak phrase", l.Text.Substring(l.WeakAt, l.WeakLen));
        }
    }

    static void TxBeats(Simulation sim, string pol)
    {
        var T = sim.S.Trial; if (T == null) return;
        foreach (var b in T.Beats)
        {
            string text = TxPlain(b.Text);
            string who = b.Speaker == null ? "court" : b.Speaker == Cast.Butler ? "Yusti" : b.Speaker == Cast.Player ? "Minhyuk" : Cast.GivenOf(b.Speaker);
            string cat = b.Kind == "mode" || b.Kind == "topic" || b.Kind == "game" ? "trial_banners"
                       : b.Speaker == Cast.Butler ? "trial_yusti"
                       : b.Kind == "result" || b.Kind == "break" ? "trial_results"
                       : b.Kind == "line" && b.Speaker == Cast.Player ? "trial_player"
                       : b.Kind == "line" ? "trial_npc"
                       : "trial_other";
            Tx(cat, $"{b.Kind}/{b.Key ?? "-"}@{who}{(pol == "smart" ? "" : " [" + pol + "]")}", text);
            if (b.Options != null) foreach (var o in b.Options) Tx("trial_options", $"Beat.Options:{b.Kind}/{b.Key ?? "-"}", TxPlain(o));
        }
        foreach (var c in T.Claims) if (!string.IsNullOrEmpty(c.Text)) Tx("trial_claims", $"Claim:{c.Topic}/{c.Key ?? "-"}/{c.Prop?.Kind}", TxPlain(c.Text));
    }

    /// <summary>Every LineBank template rendered twice with stress-test slot values (a name/time/place with and without a final
    /// consonant), so the lint sees what hard-coded particles after slots turn into. Category "zz_linebank_render".</summary>
    static void TxLineBankAll()
    {
        _txSeed = "linebank"; int n0 = LineBank.Count;
        var fld = typeof(LineBank).GetField("_d", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var d = fld?.GetValue(null) as Dictionary<string, Dictionary<string, LineBank.LineSet>>; if (d == null) return;
        var slotsA = new Dictionary<string, string> { ["t"] = "은결", ["you"] = "민혁", ["victim"] = "서윤", ["place"] = "대현관 홀", ["time"] = "오전 9시 조금 넘어", ["item"] = "은 촛대", ["act"] = "독서", ["topic"] = "체스", ["name"] = "공동 점검", ["desc"] = "설명", ["reason"] = "그 시간에 현장 근처에 있어서", ["sound"] = "비명 소리", ["list"] = "가온 → 은결", ["rel"] = "동생", ["detail"] = "상세", ["weapon"] = "은 촛대", ["room"] = "도서실", ["who"] = "은결", ["t2"] = "태겸", ["what"] = "쪽지", ["when"] = "오후 3시 20분", ["u"] = "태겸" };   // when/u: request and handover slots (voice packs)
        var slotsB = new Dictionary<string, string>(slotsA) { ["t"] = "수아", ["you"] = "재하", ["victim"] = "예담", ["place"] = "식당", ["time"] = "오후 3시쯤", ["item"] = "트로피", ["act"] = "수영", ["topic"] = "커피", ["who"] = "수아", ["t2"] = "세나", ["room"] = "게임실", ["weapon"] = "연장 전선" };
        foreach (var actor in d)
            foreach (var key in actor.Value)
            {
                for (int i = 0; i < key.Value.P.Length; i++) { Tx("zz_linebank_render", $"{actor.Key}/{key.Key}/P{i}", LineBank.Render(key.Value.P[i], slotsA, false)); Tx("zz_linebank_render", $"{actor.Key}/{key.Key}/P{i}", LineBank.Render(key.Value.P[i], slotsB, false)); }
                for (int i = 0; i < key.Value.C.Length; i++) { Tx("zz_linebank_render", $"{actor.Key}/{key.Key}/C{i}", LineBank.Render(key.Value.C[i], slotsA, true)); Tx("zz_linebank_render", $"{actor.Key}/{key.Key}/C{i}", LineBank.Render(key.Value.C[i], slotsB, true)); }
            }
    }

    // ------------------------------------------------------------------ static phrase tables
    static void TxStatic()
    {
        _txSeed = "static";
        var grams = new[] { "Ambush", "Lure", "NightVisit", "Blackout", "Press", "Drown", "Disguise", "Trap", "Gathering", "Recorder", "Courier", "Silence", "Guise", "Seal", "Tod", "Message", "Swap", "Poison", "Blur", "Echo", "Fix", "Weight",
                            "Strangle", "Push", "Shock", "Smother", "Bedtime", "KeySlide", "FakeNote", "Burn", "Dump", "Bury", "Noise", "ColdHide", "Dismember" };
        foreach (var g in grams) { Tx("replay_planprose", "Replay.PlanProse:" + g, Replay.PlanProse(g)); Tx("replay_planprose", "Replay.GrammarKor:" + g, Replay.GrammarKor(g)); }
        foreach (var g in new[] { "Lure+Tod", "Ambush+Seal+Swap", "Lure→Ambush+Recorder", "NightVisit+Message", "Gathering+Poison" }) Tx("replay_planprose", "Replay.PlanProse:" + g, Replay.PlanProse(g));
        foreach (var m in new[] { "wish", "grudge", "fear", "jealousy" }) Tx("replay_planprose", "Replay.MotiveKor", Replay.MotiveKor(m));
        foreach (var w in new[] { "interrupted", "stuck", "locked door", "item taken", "victim didn't come", "weapon missing", "no path x", "zzz" }) Tx("replay_planprose", "Replay.WhyKor", Replay.WhyKor(w));
        foreach (BodyRegion r in Enum.GetValues(typeof(BodyRegion))) foreach (DamageType d in Enum.GetValues(typeof(DamageType))) { if (d == DamageType.None) continue; Tx("static_wounds", "WoundText.Describe", WoundText.Describe(new Wound { Region = r, Type = d, Sev = 2 })); }
        foreach (DamageType d in Enum.GetValues(typeof(DamageType))) { try { Tx("static_tables", "TrialGames.DamageWord", TrialGames.DamageWord(d)); } catch { } }
        foreach (SoundKind k in Enum.GetValues(typeof(SoundKind))) Tx("static_tables", "Simulation.SoundText(+ 소리가 들렸다)", Simulation.SoundText(k) + " 소리가 들렸다");
        foreach (var t in new[] { "alibi", "cause", "time", "place", "suspicious", "culprit" }) Tx("static_tables", "TrialGames.TopicLabel", TrialGames.TopicLabel(t));
        foreach (var t in TrialSystem.TrickOptions) Tx("static_tables", "TrialSystem.TrickOptions", t);
        foreach (var k in new[] { "Seal", "Tod", "Message", "Swap", "Poison" }) Tx("static_tables", "SetPieces.Kor", SetPieces.Kor(k));
        foreach (var a in Activities.All) Tx("static_tables", "ActivityDef.Kor", a.Kor);
    }
}
