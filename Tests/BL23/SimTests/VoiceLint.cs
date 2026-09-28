using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BL23.Sim;

/// <summary>
/// "voicelint [outFile]" — authoring checks over every LineBank template (VoicePackGuide.md §Rules):
///   · a subtitle page over 60 characters (rendered with 2-3 syllable names / places / items)
///   · a signature tic or laugh said by someone who does not own it (CastTraits.TicOwner / LaughOwner, bible §2)
///   · banned words (재판, 학급재판, 학교, 반장; 도윤's 당신; 세나's 확인)
///   · pool sizes of the most-heard keys per resident (small_talk, busy, doing_act, routines, grammar, requests)
/// Prints a summary; writes every hit to the out file (default: BL23Lab/voicelint.txt).
/// </summary>
public static partial class Program
{
    /// <summary>Sim/Content/Voice next to this test project (Tests/BL23/SimTests → Assets/BASSLINE/BL23/Sim/Content/Voice).</summary>
    static string VoiceDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            var p = Path.Combine(dir.FullName, "Assets", "BASSLINE", "BL23", "Sim", "Content", "Voice");
            if (Directory.Exists(p)) return p;
        }
        var fixedPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Assets", "BASSLINE", "BL23", "Sim", "Content", "Voice");
        if (Directory.Exists(fixedPath)) return fixedPath;
        return Directory.Exists(@"C:\Users\리오\OneDrive\Desktop\BASSLINE\Assets\BASSLINE\BL23\Sim\Content\Voice") ? @"C:\Users\리오\OneDrive\Desktop\BASSLINE\Assets\BASSLINE\BL23\Sim\Content\Voice" : null;
    }

    static int VoiceLintTest(string[] args)
    {
        string outFile = args.Length > 1 ? args[1] : @"C:\Users\리오\BL23Lab\voicelint.txt";
        int n0 = LineBank.Count;
        var fld = typeof(LineBank).GetField("_d", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var d = fld?.GetValue(null) as Dictionary<string, Dictionary<string, LineBank.LineSet>>;
        if (d == null) { Console.WriteLine("no LineBank"); return 1; }
        var slots = new Dictionary<string, string> { ["t"] = "은결 씨", ["you"] = "민혁 씨", ["victim"] = "서윤 씨", ["place"] = "대현관 홀", ["time"] = "오전 9시 조금 넘어", ["item"] = "은 촛대", ["act"] = "종이 공예", ["topic"] = "체스", ["reason"] = "그 시간에 현장 근처에 있어서", ["sound"] = "비명", ["u"] = "태겸 씨", ["when"] = "오후 3시 20분", ["name"] = "공동 점검", ["detail"] = "상세", ["weapon"] = "은 촛대", ["room"] = "도서실", ["who"] = "은결", ["t2"] = "태겸" };
        var hits = new List<string>(); int longPages = 0, ticHits = 0, banHits = 0, templates = 0, voiceHits = 0;
        // lines authored in the voice packs (Sim/Content/Voice) are flagged [VOICE] so a pack author sees only their own problems
        string voiceSrc = ""; try { string vd = VoiceDir(); if (vd != null) voiceSrc = string.Join("\n", Directory.GetFiles(vd, "*.cs").Select(File.ReadAllText)); } catch { }
        string Tag(string raw) { bool v = raw != null && voiceSrc.Contains(raw); if (v) voiceHits++; return v ? "[VOICE] " : ""; }

        // tic regexes: the owner's word as a word (not inside another word)
        Regex W(string tic) => new Regex(@"(?<![가-힣A-Za-z])" + Regex.Escape(tic).Replace(@"\ ", " ") + @"(?![가-힣])");
        var tics = new List<(string tic, string owner, Regex rx)>();
        foreach (var kv in CastTraits.TicOwner)
        {
            string t = kv.Key.TrimEnd(',', '.', '?', '!', '…');
            Regex rx;
            if (t == "있잖아") rx = new Regex(@"(^|[.!?…,] ?|\| ?)있잖아(요)?[,.…]");        // the filler, not the grammar ("극장 있잖아.")
            else if (t == "그러니까") rx = new Regex(@"(^|[.!?…] |\|)그러니까[,.… ]");      // sentence-initial connective
            else if (t == "세상에") rx = new Regex(@"(?<![가-힣])세상에(?![가-힣서])");
            else if (t == "별로") rx = new Regex(@"(^|[ .…|])별로\.(\s|$|\|)");                 // the one-word verdict "별로."
            else if (t == "끝났습니다") rx = new Regex(@"(^|[.!?…] |\|)끝났습니다\.");          // 민서's one-line report, not the verb
            else if (kv.Key.EndsWith("!")) rx = new Regex(@"(?<![가-힣])" + Regex.Escape(t) + "!");   // "됐다!" (해린) ≠ "라임 됐다"
            else rx = W(t);
            tics.Add((t, kv.Value, rx));
        }
        // a laugh is a laugh only when it stands as a word followed by punctuation ("흠." — not 도윤's "흠 하나 없이")
        foreach (var kv in CastTraits.LaughOwner) tics.Add((kv.Key, kv.Value, new Regex(@"(?<![가-힣])" + kv.Key + @"(?=[.…!?,~]|$|\|)")));
        var bans = new (string who, Regex rx, string why)[]
        {
            (null, new Regex("재판|학급재판|학교|반장"), "banned word"),
            ("P04", new Regex(@"(?<![가-힣])당신"), "도윤 never says 당신"),
            ("P13", new Regex("확인"), "세나 never says 확인"),
        };
        var byActorKeyCount = new Dictionary<string, int>();
        foreach (var actor in d)
        {
            foreach (var key in actor.Value)
            {
                foreach (var (arr, reg) in new[] { (key.Value.P, "P"), (key.Value.C, "C") })
                {
                    for (int i = 0; i < arr.Length; i++)
                    {
                        templates++;
                        string raw = arr[i]; string where = $"{actor.Key}/{key.Key}/{reg}{i}";
                        string text = LineBank.Render(raw, slots, reg == "C");
                        foreach (var pg in LineBank.Pages(text)) if (pg.Length > 60) { longPages++; hits.Add($"{Tag(raw)}LONG {pg.Length} {where} | {pg}"); }
                        if (actor.Key != "ANY" && actor.Key != LineBank.House && actor.Key != Cast.Player && actor.Key != Cast.Butler)
                            foreach (var (tic, owner, rx) in tics)
                                if (owner != actor.Key && rx.IsMatch(raw)) { ticHits++; hits.Add($"{Tag(raw)}TIC '{tic}' (owner {owner}) {where} | {raw}"); }
                        foreach (var (who, rx, why) in bans)
                            if ((who == null || who == actor.Key) && rx.IsMatch(raw)) { banHits++; hits.Add($"{Tag(raw)}BAN {why} {where} | {raw}"); }
                    }
                }
            }
        }
        // pool sizes of the keys heard most
        string[] watch = { "small_talk", "busy", "doing_act", "meal", "comfort", "comforted", "applause", "confront", "confront_reply", "offer_tea", "thanks_tea", "stroll_invite", "stroll_accept", "borrow_ask", "borrow_yes", "borrow_no", "lend_give", "gathering_chat", "saw_handover", "hurt_react", "startle", "req_invite", "req_find", "req_deliver", "req_thanks_find" };
        var sb = new StringBuilder();
        sb.AppendLine($"VOICELINT templates={templates} long-pages={longPages} tic-collisions={ticHits} banned={banHits} — of these in voice packs: {voiceHits} (voice source {voiceSrc.Length} chars)");
        sb.AppendLine("pool sizes (own P+C; '-' = falls back to ANY):");
        sb.AppendLine("      " + string.Join(" ", watch.Select(w => w.Length > 6 ? w.Substring(0, 6) : w.PadRight(6))));
        foreach (var c in Cast.Participants.Where(c => !c.IsPlayer))
            sb.AppendLine($"  {c.Id} " + string.Join(" ", watch.Select(w => { int v = LineBank.Variants(c.Id, w); return (v == 0 ? "-" : v.ToString()).PadLeft(6); })));
        // suffixed variants per resident (pair @, about #, situation ~)
        foreach (var c in Cast.All.Where(c => !c.IsPlayer))
        {
            if (!d.TryGetValue(c.Id, out var m)) continue;
            int pair = m.Keys.Count(k => k.Contains('@')), about = m.Keys.Count(k => k.Contains('#')), ctx = m.Keys.Count(k => k.Contains('~')), intent = m.Keys.Count(k => k.StartsWith("intent_")), req = m.Keys.Count(k => k.StartsWith("req_"));
            int lines = m.Values.Sum(s => s.P.Length + s.C.Length);
            sb.AppendLine($"  {c.Id} {c.Given}: lines={lines} pair-keys={pair} about-keys={about} situation-keys={ctx} intent-keys={intent} request-keys={req}");
        }
        // keys a resident still takes from the shared ANY bank (what a voice pack should cover next)
        if (d.TryGetValue("ANY", out var anyKeys))
            foreach (var c in Cast.Participants.Where(c => !c.IsPlayer))
            {
                var missing = anyKeys.Keys.Where(k => !k.Contains('~') && LineBank.Variants(c.Id, k) == 0).OrderBy(k => k, StringComparer.Ordinal).ToList();
                if (missing.Count > 0) sb.AppendLine($"  {c.Id} still shared ({missing.Count}): " + string.Join(", ", missing));
            }
        Console.Write(sb.ToString());
        try { Directory.CreateDirectory(Path.GetDirectoryName(outFile)); File.WriteAllText(outFile, sb.ToString() + string.Join("\n", hits) + "\n", new UTF8Encoding(false)); Console.WriteLine("hits → " + outFile); } catch (Exception e) { Console.WriteLine("write failed: " + e.Message); }
        foreach (var h in hits.Where(h => h.StartsWith("[VOICE]")).Take(60)) Console.WriteLine("  " + h);
        return 0;
    }
}
