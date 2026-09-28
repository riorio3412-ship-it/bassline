using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// F11 — rumours (DailyLifeDesign §3.5). A rumour has an origin, a chain and each knower's own version — never one global
    /// truth. Sources: pair scenes seen by others, odd sightings (late walks, a tool in hand, someone else's room), 민혁's own
    /// prying, and a wrong verdict. It spreads in ordinary conversations (two residents talking), escalates one step in the
    /// mouth of the dishonest ("만졌대 → 가져갔대 → 훔쳤대"), raises suspicion and lowers trust toward its subject, and makes
    /// the subject angry at the teller if they overhear it. Close residents bring a new one to 민혁 first. He can trace it
    /// ("누구한테 들었어?") and correct it when he knows better. It becomes an evidence card only through case recall.
    /// Each knower's copy: Knowledge.Facts "rumour:&lt;rid&gt;|kind|a|b|room|step|from|origin|src".
    /// </summary>
    public sealed partial class Simulation
    {
        public struct Rumour { public string rid, kind, a, b, from, origin, src; public int room, step; }

        /// <summary>Claim ladders: step 0 is what was seen; each step is one exaggeration further.</summary>
        static readonly Dictionary<string, string[]> Claims = new Dictionary<string, string[]>
        {
            ["argued"] = new[] { "{a:이} {b:와} {place}에서 말다툼을 했대.", "{a:이} {b:와} 크게 싸웠대. 소리까지 질렀대.", "{a:이} {b} 멱살까지 잡았대." },
            ["flirt"] = new[] { "{a:이} {b:와} 요즘 붙어 다닌대.", "{a:이} {b:와} 사귄대.", "{a:이} {b} 방에서 늦게 나오더래." },
            ["night"] = new[] { "{a:이} 밤늦게 복도를 걷더래.", "{a:이} 밤마다 남의 방 앞을 서성인대.", "{a:이} 밤에 남의 방문 손잡이를 돌려 보더래." },
            ["tool"] = new[] { "{a:이} {item:을} 들고 다니더래.", "{a:이} {item:을} 몰래 챙겼대.", "{a:이} {item:을} 어딘가에 숨겨 뒀대." },
            ["room"] = new[] { "{a:이} {b} 방 쪽에서 나오더래.", "{a:이} {b} 방에 들어갔다 나왔대.", "{a:이} {b} 방 서랍을 뒤졌대." },
            ["pry"] = new[] { "{a:이} 남의 방 서랍을 열어 봤대.", "{a:이} 남의 방을 뒤지고 다닌대.", "{a:이} 남의 물건을 가져갔대." },
            ["vote"] = new[] { "{a:이} 심판에서 표를 한쪽으로 몰았대.", "{a:이} 심판 전에 누굴 찍으라고 돌렸대.", "{a:이} 처음부터 표를 짜 놨대." },
            ["frames"] = new[] { "{a:이} 밤에 복도 액자를 하나씩 만지고 다니더래.", "{a:이} 밤마다 복도를 돌며 뭘 확인한대.", "{a:이} 밤에 누굴 따라다니는 것 같대." },
            ["key"] = new[] { "{a:이} 창고 열쇠를 갖고 있었대.", "{a:이} 창고 물건을 꺼내 간대.", "{a:이} 창고 물건을 빼돌린대." },
            ["secret"] = new[] { "{a:은} 여기 오기 전에 무슨 일이 있었대.", "{a:은} 옛날에 큰일을 저질렀대.", "{a:이} 사람을 망쳐 놓고 왔대." },
        };
        static readonly HashSet<string> Negative = new HashSet<string> { "night", "tool", "room", "pry", "vote", "frames", "key", "secret" };

        string RumPrefix(string rid) => "rumour:" + rid + "|";

        /// <summary>A rumour starts: its first knower (the origin) holds it at step 0. Returns the id, or null (a duplicate today).</summary>
        internal string SeedRumour(string kind, string a, string b, string origin, int room, bool fromChoice, string src = null)
        {
            if (kind == null || a == null || origin == null || !Claims.ContainsKey(kind)) return null;
            if (kind == "flirt" && (a == "P02" || a == "P17" || b == "P02" || b == "P17")) return null;   // no romance talk about the youngest-looking two
            string dk = $"lrumk:{kind}:{a}:{b}";
            if (S.Flags.TryGetValue(dk, out var d) && S.Day - d < 2) return null;
            S.Flags[dk] = S.Day;
            int n = (int)LF("lrumn") + 1; S.Flags["lrumn"] = n;
            var r = new Rumour { rid = "R" + n.ToString(CultureInfo.InvariantCulture), kind = kind, a = a, b = b ?? "-", room = room, step = 0, from = origin, origin = origin, src = src ?? "-" };
            RumourLearn(origin, r, origin);
            S.Log("RumourSeed", origin, a, room: room, data: $"{r.rid}:{kind}:{b}:{src}");
            LFinc("lrum:seeded");
            return r.rid;
        }

        internal Rumour? RumourOf(string knower, string rid)
        {
            if (knower == null || rid == null) return null;
            string pre = RumPrefix(rid);
            var f = S.K(knower).Facts.FirstOrDefault(x => x.StartsWith(pre, StringComparison.Ordinal));
            return f == null ? (Rumour?)null : ParseRumour(f);
        }

        static Rumour? ParseRumour(string f)
        {
            var p = f.Substring("rumour:".Length).Split('|'); if (p.Length < 9) return null;
            return new Rumour { rid = p[0], kind = p[1], a = p[2], b = p[3] == "-" ? null : p[3], room = int.TryParse(p[4], out var rm) ? rm : -1, step = int.TryParse(p[5], out var st) ? st : 0, from = p[6], origin = p[7], src = p[8] };
        }

        IEnumerable<Rumour> RumoursOf(string knower) => S.K(knower).Facts.Where(x => x.StartsWith("rumour:", StringComparison.Ordinal)).OrderBy(x => x, StringComparer.Ordinal).Select(ParseRumour).Where(r => r != null).Select(r => r.Value);

        /// <summary>The claim as this speaker would say it (names in their own address).</summary>
        /// <summary>The claim as this speaker would say it to this listener (names in the speaker's own address; "…했대요" when
        /// the speaker is on polite terms with the listener, "…했대" otherwise).</summary>
        internal string RumourText(Rumour r, string speaker = null, string listener = null)
        {
            if (!Claims.TryGetValue(r.kind, out var ladder)) return "";
            string sp = speaker ?? r.from ?? Cast.Player;
            string Nm(string id) => id == null || id == "-" ? "누군가" : CallName(sp, id);
            var slots = new Dictionary<string, string> { { "a", Nm(r.a) }, { "b", Nm(r.b) }, { "place", r.room >= 0 ? S.RoomName(r.room) : "어딘가" }, { "item", r.src != null && r.src.StartsWith("item=") ? r.src.Substring(5) : "뭔가" } };
            string s = LineBank.FixParticles(LineBank.Render(ladder[Math.Max(0, Math.Min(ladder.Length - 1, r.step))], slots, false));
            bool polite = sp != Cast.Player ? !CasualTo(sp, listener ?? Cast.Player) : listener != null && !CasualTo(sp, listener);
            if (polite && s.EndsWith(".") && s.Length > 2 && (s[s.Length - 2] == '대' || s[s.Length - 2] == '래')) s = s.Substring(0, s.Length - 1) + "요.";
            return s;
        }

        /// <summary>Someone hears a rumour (their own copy, with the step as told).</summary>
        internal void RumourLearn(string who, Rumour r, string from)
        {
            if (who == null) return;
            var k = S.K(who); string pre = RumPrefix(r.rid);
            if (k.Facts.Any(x => x.StartsWith(pre, StringComparison.Ordinal)) || k.Facts.Contains("rumour-corrected:" + r.rid)) return;
            k.Facts.Add($"rumour:{r.rid}|{r.kind}|{r.a}|{r.b ?? "-"}|{r.room}|{r.step}|{from}|{r.origin}|{r.src ?? "-"}");
            LFinc("lrum:heard");
            if (who == Cast.Player) { PK.Facts.Add($"bondnote:{r.a}:소문 — “{RumourText(r, from)}” ({Cast.GivenOf(from)}에게서)"); return; }
            if (Negative.Contains(r.kind) && r.a != who && S.A(r.a) != null)
            {
                k.Suspicion[r.a] = (k.Suspicion.TryGetValue(r.a, out var v) ? v : 0) + 0.03f * (r.step + 1);
                Relations.Change(S, who, r.a, trust: -0.02f * (r.step + 1));
            }
            // close residents bring it to 민혁 first (stage 2)
            if (who != r.a && S.A(who)?.Alive == true && (BondStage(who) >= 2 || S.R(who, Cast.Player).Like >= 0.32f) && RumourOf(Cast.Player, r.rid) == null && LifeGoFor(who) == null)
                LifePurpose(who, "rumour", r.rid);
        }

        void RumourTick(int mod)
        {
            // spread: two residents already talking
            var talk = S.LivingNpcs.Where(x => x.TalkingTo != null && string.CompareOrdinal(x.Id, x.TalkingTo) < 0 && S.A(x.TalkingTo)?.TalkingTo == x.Id).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
            foreach (var x in talk)
            {
                var y = S.A(x.TalkingTo); if (y == null || y.IsPlayer) continue;
                foreach (var (teller, hearer) in new[] { (x, y), (y, x) })
                {
                    var r = RumoursOf(teller.Id).FirstOrDefault(q => RumourOf(hearer.Id, q.rid) == null && q.a != hearer.Id && !S.Flags.ContainsKey("rumhush:" + teller.Id + ":" + q.rid) && !S.K(hearer.Id).Facts.Contains("rumour-corrected:" + q.rid));
                    if (r.rid == null || !LR.Chance(0.35)) continue;
                    var told = r;
                    if ((teller.Def.P.Honesty < 0.5f || teller.Def.Deceit > 70) && told.step < 2 && LR.Chance(0.5)) { told.step++; LFinc("lrum:distorted"); }
                    string claim = RumourText(told, teller.Id, hearer.Id);
                    var u = LifeKeyU(teller.Id, hearer.Id, "rumour_tell", new SceneRun { Kind = "life", Npc = teller.Id, Ctx = { ["rumour"] = claim, ["t"] = told.a } });
                    LifeSayNow(teller, hearer.Id, u?.Text ?? claim, "rumour_tell");
                    told.from = teller.Id;
                    RumourLearn(hearer.Id, told, teller.Id);
                    LFinc("lrum:spread");
                    // the subject overhears
                    var subj = S.A(told.a);
                    if (subj != null && subj.Alive && !subj.IsPlayer && subj.Room == teller.Room && subj.Pose != Pose.Sleep && subj.Pos.Dist(teller.Pos) < 9 && Negative.Contains(told.kind))
                    {
                        Relations.Change(S, subj.Id, teller.Id, grudge: 0.06f, like: -0.04f, memory: "내 얘기를 뒤에서 하는 걸 들었다");
                        subj.Needs.Anger = MathX.Clamp01(subj.Needs.Anger + 0.15f);
                        var du = LifeKeyU(subj.Id, teller.Id, "rumour_deny", new SceneRun { Kind = "life", Npc = subj.Id, Ctx = { ["t"] = teller.Id } });
                        if (du != null) LifeSayLater(new[] { du }, 0.4);
                        S.K(subj.Id).Facts.Add("rumour-about-me:" + told.rid);
                    }
                    // a hearer standing near 민혁 — he hears it too
                    var me = S.Player;
                    if (me != null && me.Room == teller.Room && me.Pose != Pose.Sleep && me.Pos.Dist(teller.Pos) < 9) RumourLearn(Cast.Player, told, teller.Id);
                    break;
                }
            }
            // seed: once an hour, someone's odd sighting in the last hour becomes talk
            if (mod % 60 == 35)
            {
                var obs = S.LivingNpcs.OrderBy(x => x.Id, StringComparer.Ordinal).ToList(); if (obs.Count == 0) return;
                var o = obs[(S.Day * 24 + mod / 60) % obs.Count];
                foreach (var s in S.K(o.Id).Sightings.Where(s => S.Clock - s.T1 < 60 && s.Target != o.Id && s.Target != Cast.Butler && !s.Dead && s.IdConf > 0.6f).OrderByDescending(s => s.T1).Take(12))
                {
                    var room = S.Layout.Room(s.Room); if (room == null) continue;
                    int m = (int)Math.Floor(s.T0 % 1440);
                    if (s.Held != null && ItemCatalog.Get(s.Held)?.IsWeapon == true && room.Type != RoomType.Kitchen && room.Type != RoomType.Workshop)
                    { if (SeedRumour("tool", s.Target, null, o.Id, s.Room, false, "item=" + (ItemCatalog.Get(s.Held)?.Kor ?? "칼")) != null) break; }
                    else if (room.Type == RoomType.Bedroom && room.Owner != null && room.Owner != s.Target)
                    { if (SeedRumour("room", s.Target, room.Owner, o.Id, s.Room, false) != null) break; }
                    else if ((m >= 23 * 60 || m < 5 * 60) && RoomInfo.IsPassage(room.Type))
                    { if (SeedRumour(s.Target == "P04" ? "frames" : "night", s.Target, null, o.Id, s.Room, false) != null) break; }
                }
                // 민혁 caught prying today
                foreach (var f in S.Flags.Keys.Where(k => k.StartsWith("pry:", StringComparison.Ordinal) && k.EndsWith(":" + S.Day, StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList())
                {
                    if (S.Flags.ContainsKey("l" + f)) continue; S.Flags["l" + f] = S.Clock;
                    var p = f.Split(':'); if (!int.TryParse(p[1], out var fid) || fid < 0 || fid >= S.Layout.Furniture.Count) continue;
                    var froom = S.Layout.Room(S.Layout.Furniture[fid].Room); var owner = froom?.Owner != null ? S.A(froom.Owner) : null;
                    string origin = owner != null && owner.Alive ? owner.Id : S.LivingNpcs.Where(x => x.Room == S.Player.Room).OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => x.Id).FirstOrDefault();
                    if (origin != null) SeedRumour("pry", Cast.Player, froom?.Owner, origin, froom?.Id ?? -1, false);
                }
            }
        }

        // ------------------------------------------------------------------ 민혁 and rumours
        /// <summary>Rumours this resident and 민혁 both know (for "누구한테 들었어?" and corrections).</summary>
        List<(string rid, string text)> RumoursShared(string npc)
        {
            var res = new List<(string, string)>();
            foreach (var r in RumoursOf(npc)) { var mine = RumourOf(Cast.Player, r.rid); if (mine != null) res.Add((r.rid, "“" + RumourText(r, Cast.Player) + "”")); }
            return res.Take(6).ToList();
        }

        /// <summary>Does 민혁 know better? It is about him, or he saw the scene it came from, or it grew in the telling about someone he knows well.</summary>
        bool RumourPlayerKnowsTruth(string rid)
        {
            var r = RumourOf(Cast.Player, rid); if (r == null) return false;
            var v = r.Value;
            if (v.a == Cast.Player) return v.step > 0 || v.kind != "pry";
            if (v.src != null && v.src.StartsWith("PS") && PK.Facts.Any(f => f.StartsWith("saw-scene:" + v.src + ":", StringComparison.Ordinal))) return true;
            return v.step >= 1 && BondStage(v.a) >= 2;
        }

        (string rid, string carrier)? RumoursAt(List<string> diners)
        {
            var counts = new Dictionary<string, List<string>>();
            foreach (var d in diners) foreach (var r in RumoursOf(d)) { if (!counts.TryGetValue(r.rid, out var l)) counts[r.rid] = l = new List<string>(); l.Add(d); }
            var best = counts.Where(kv => kv.Value.Count >= 2 && !S.Flags.ContainsKey("lttrum:" + kv.Key)).OrderByDescending(kv => kv.Value.Count).ThenBy(kv => kv.Key, StringComparer.Ordinal).FirstOrDefault();
            if (best.Key == null) return null;
            S.Flags["lttrum:" + best.Key] = S.Clock;
            return (best.Key, best.Value.OrderBy(x => x, StringComparer.Ordinal).First());
        }

        List<Utterance> RumourTrace(Actor npc, string rid)
        {
            var res = new List<Utterance>(); var r = RumourOf(npc.Id, rid); if (r == null) return res;
            var v = r.Value;
            res.Add(new Utterance { Speaker = Cast.Player, Listener = npc.Id, Key = "life", Text = PoliteToPlayer(npc.Id) ? "그 얘기, 누구한테 들었어요?" : "그 얘기 누구한테 들었어?" });
            string who = v.from == npc.Id ? (v.origin == npc.Id ? null : v.origin) : v.from;
            var run = new SceneRun { Kind = "life", Npc = npc.Id }; if (who != null) run.Ctx["t"] = who;
            var u = who == null ? LifeKeyU(npc.Id, Cast.Player, "rumour_mine", run) : LifeKeyU(npc.Id, Cast.Player, "rumour_from", run);
            if (u == null) u = new Utterance { Speaker = npc.Id, Listener = Cast.Player, Key = "life", Text = who == null ? "내가 직접 봤어." : LineBank.FixParticles($"{CallName(npc.Id, who)}한테 들었어.") };
            res.Add(u);
            PK.Facts.Add($"bondnote:{v.a}:소문의 길 — {Cast.GivenOf(npc.Id)} ← {(who != null ? Cast.GivenOf(who) : "직접 봤다고 함")}");
            PK.Facts.Add($"rumour-from:{rid}:{npc.Id}:{who ?? npc.Id}");
            Relations.Change(S, npc.Id, Cast.Player, trust: 0.01f);
            LFinc("lrum:traced");
            return res;
        }

        List<Utterance> RumourCorrect(Actor npc, string rid)
        {
            var res = new List<Utterance>(); var r = RumourOf(npc.Id, rid); if (r == null || !RumourPlayerKnowsTruth(rid)) return res;
            var v = r.Value;
            res.Add(new Utterance { Speaker = Cast.Player, Listener = npc.Id, Key = "life", Text = PoliteToPlayer(npc.Id) ? "그거 사실이 아니에요. 제가 봤어요." : "그거 사실 아니야. 내가 봤어." });
            var u = LifeKeyU(npc.Id, Cast.Player, "rumour_correct", new SceneRun { Kind = "life", Npc = npc.Id, Ctx = { ["t"] = v.a } })
                    ?? new Utterance { Speaker = npc.Id, Listener = Cast.Player, Key = "life", Text = "…그래? 내가 잘못 들었나 보네." };
            res.Add(u);
            var k = S.K(npc.Id);
            foreach (var f in k.Facts.Where(x => x.StartsWith(RumPrefix(rid), StringComparison.Ordinal)).ToList()) k.Facts.Remove(f);
            k.Facts.Add("rumour-corrected:" + rid);
            if (k.Suspicion.TryGetValue(v.a, out var s)) k.Suspicion[v.a] = Math.Max(0, s - 0.06f);
            Relations.Change(S, npc.Id, Cast.Player, trust: 0.03f);
            if (v.a != Cast.Player && S.A(v.a)?.Alive == true) Relations.Change(S, v.a, Cast.Player, trust: 0.04f, like: 0.02f, memory: "나에 대한 소문을 바로잡아 줬다");
            LFinc("lrum:corrected");
            return res;
        }
    }
}
