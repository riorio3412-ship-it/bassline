using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// F10 — group events and small festivals (DailyLifeDesign §3.4), and the memorial (§8.3). At most one per calm day: the
    /// host proposes, 유스티 announces ("y_event" / "y_memorial"), and it runs through the ordinary gathering system (Grammars:
    /// attendance, arrivals, slipping out, the player's attendance record) — so every festival is also a murder window.
    /// When 민혁 is there it is staged with its mini-game (LifeOffer → LifeSceneUI):
    ///   quiz (예담, residents' habits — spoken aloud to a known audience, §7.4) · cards (진우: bluffs, tells learned, F8) ·
    ///   concert (재하 and whoever performs) · cooking (준서 vs a challenger, "간 좀 봐 줄래요?") · bar (시온: the second glass) ·
    ///   memorial (은결, or 준서/서윤, after a death).
    /// Without him the host and guests still talk (overheard) and the knowledge effects happen.
    /// </summary>
    public sealed partial class Simulation
    {
        sealed class FestKind { public string Kind, Host, Title, Rooms; public int Start, Len; public string[] Alt; }

        static readonly FestKind[] FestKinds =
        {
            new FestKind { Kind = "quiz", Host = "P17", Title = "저택 퀴즈쇼", Rooms = "Lounge;TeaRoom;Parlor;GameRoom", Start = 15 * 60, Len = 80, Alt = new[] { "P03" } },
            new FestKind { Kind = "concert", Host = "P09", Title = "작은 공연의 밤", Rooms = "Theater;MusicRoom;Lounge", Start = 15 * 60 + 30, Len = 80, Alt = new[] { "P12", "P07" } },
            new FestKind { Kind = "cooking", Host = "P10", Title = "요리 품평회", Rooms = "Dining;Kitchen", Start = 16 * 60, Len = 70, Alt = new[] { "P03" } },
            new FestKind { Kind = "cards", Host = "P02", Title = "카드의 밤", Rooms = "Lounge;GameRoom;Parlor;TeaRoom", Start = 20 * 60, Len = 90, Alt = new[] { "P13", "P06" } },
            new FestKind { Kind = "bar", Host = "P07", Title = "바의 밤", Rooms = "Lounge;Parlor;Dining", Start = 20 * 60 + 30, Len = 80, Alt = new[] { "P08" } },
        };

        void FestMinute(int mod)
        {
            if (mod == 10 * 60) { HouseEventPlan(); FestPlan(); }
            HouseEventMinute(mod);
            if (mod == 17 * 60) MemorialPlan();
            // festivals running without 민혁: the host opens, the guests answer (overheard), the knowledge happens
            foreach (var g in (S.Gatherings ?? new List<Gathering>()).Where(g => g.Kind != null && (g.Kind.StartsWith("fest:") || g.Kind == "memorial" || HouseEvents.IsHouse(g)) && !g.Done && !g.Cancelled).ToList())
            {
                if (S.Clock < g.Cur.Start + 15 || S.Flags.ContainsKey("lfestnpc:" + g.Id) || S.Flags.ContainsKey("lfestseen:" + g.Id)) continue;
                S.Flags["lfestnpc:" + g.Id] = S.Clock;
                var present = FestPresent(g); if (!(present.Contains(g.Host) || g.Host == Cast.Butler) || present.Count < 2) continue;
                var run = FestBuild(g, present, false); if (run == null) continue;
                var lines = RunFrom(run);
                LifeSayLater(lines, 0.2, 1.0);
                FestFinished(run);
            }
        }

        List<string> FestPresent(Gathering g) => S.LivingNpcs.Where(x => x.Room == g.Cur.Room && x.Pose != Pose.Sleep && x.Status == ActorStatus.Active && g.Status.ContainsKey(x.Id)).OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => x.Id).ToList();

        void FestPlan()
        {
            if (S.Phase != Phase.Daily || LifeAfterDeath(24) || S.Flags.ContainsKey($"lfestday:{S.Day}")) return;
            if (S.Ch.InvestigationEnd >= 0 && S.Clock < S.Ch.InvestigationEnd) return;
            if (!LR.Chance(S.Day == 1 ? 0.6 : 0.75)) return;
            var order = FestKinds.OrderBy(k => LF("lfest:" + k.Kind, -99)).ThenBy(k => Array.IndexOf(FestKinds, k)).ToList();
            foreach (var k in order)
            {
                if (S.Flags.TryGetValue("lfest:" + k.Kind, out var ld) && S.Day - ld < 3) continue;
                string host = new[] { k.Host }.Concat(k.Alt ?? new string[0]).FirstOrDefault(h => { var a = S.A(h); return a != null && a.Alive && a.Status == ActorStatus.Active && a.PlanId == null && a.Needs.Fear < 0.5f; });
                if (host == null) continue;
                var room = S.Layout.Rooms.Where(r => RoomFits(k.Rooms, r) && RoomUsable(S.A(host), r)).OrderBy(r => Array.IndexOf(k.Rooms.Split(';'), r.Type.ToString())).ThenBy(r => r.Id).FirstOrDefault();
                if (room == null) continue;
                double start = Math.Floor(S.Clock / 1440) * 1440 + k.Start;
                if ((S.Gatherings ?? new List<Gathering>()).Any(g => !g.Done && !g.Cancelled && g.Revs.Count > 0 && Math.Abs(g.Cur.Start - start) < 120)) continue;
                FestCreate("fest:" + k.Kind, host, k.Title, room, start, start + k.Len, "y_event");
                S.Flags["lfest:" + k.Kind] = S.Day; S.Flags[$"lfestday:{S.Day}"] = 1;
                return;
            }
        }

        void MemorialPlan()
        {
            if (S.Phase != Phase.Daily) return;
            var inc = S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Confirmed && !S.Flags.ContainsKey("lmemorial:" + i.Victim)).OrderBy(i => i.ConfirmClock).FirstOrDefault();
            if (inc == null) return;
            string host = new[] { "P14", "P10", "P03", "P18" }.FirstOrDefault(h => { var a = S.A(h); return a != null && a.Alive && a.Status == ActorStatus.Active && a.PlanId == null && h != inc.Culprit; });
            if (host == null) return;
            var room = S.Layout.Rooms.Where(r => (r.Type == RoomType.Chapel || r.Type == RoomType.Lounge || r.Type == RoomType.Greenhouse) && RoomUsable(S.A(host), r)).OrderBy(r => r.Type == RoomType.Chapel ? 0 : r.Type == RoomType.Greenhouse ? 1 : 2).ThenBy(r => r.Id).FirstOrDefault();
            if (room == null) return;
            S.Flags["lmemorial:" + inc.Victim] = S.Day;
            double start = Math.Floor(S.Clock / 1440) * 1440 + 20 * 60 + 30;
            var g = FestCreate("memorial", host, "추모의 밤", room, start, start + 60, "y_memorial");
            if (g != null) S.Flags["lmemv:" + g.Id] = int.Parse(inc.Victim.Substring(1), CultureInfo.InvariantCulture);
        }

        Gathering FestCreate(string kind, string host, string title, Room room, double start, double end, string announceKey)
        {
            if (S.Gatherings == null) S.Gatherings = new List<Gathering>();
            var g = new Gathering { Id = S.NewId("gath"), Host = host, Kind = kind, Label = title };
            g.Revs.Add(new GatheringRev { Room = room.Id, Start = start, End = end, At = S.Clock, Why = "유스티의 공지" });
            g.Status[host] = "host"; g.KnownRev[host] = 0; g.Channel[host] = "voice";
            foreach (var x in S.Living.Where(x => x.Id != host).OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                bool loves = kind.StartsWith("fest:") && CastTraits.HangoutFav(x.Id, kind == "fest:quiz" ? "cards" : kind == "fest:concert" ? "piano" : kind == "fest:cooking" ? "cook" : kind == "fest:cards" ? "cards" : "bar") == CastTraits.Fav.Love;
                g.Status[x.Id] = x.IsPlayer ? "invited" : (kind == "memorial" ? (S.R(x.Id, host).Opinion > -0.3 && x.Needs.Fear < 0.7f ? "accepted" : "declined") : (loves || Grammars.WouldAccept(this, x, g) ? "accepted" : "declined"));
                g.KnownRev[x.Id] = 0; g.Channel[x.Id] = "voice";
            }
            S.Gatherings.Add(g);
            S.Log("GatheringPlanned", host, data: $"{g.Id} {title} @{room.Name} {ClockFmt.Vague(start)}-{ClockFmt.Vague(end)} guests={string.Join(",", g.Status.Where(kv => kv.Value == "accepted").Select(kv => kv.Key))}");
            S.Log("FestPlanned", host, room: room.Id, data: kind + ":" + g.Id);
            LFinc("lfest:total");
            // 유스티 announces it (host name as the house says it, the place, the hour)
            var slots = new Dictionary<string, string> { { "t", Cast.NameOf(host) }, { "place", room.Name }, { "when", ClockFmt.Mark(start, true) }, { "time", ClockFmt.Mark(start, true) }, { "act", title } };
            Announce(announceKey, slots);
            return g;
        }

        // ------------------------------------------------------------------ staged: 민혁 walked into a running festival
        LifeStage FestOffer()
        {
            var me = S.Player;
            foreach (var g in (S.Gatherings ?? new List<Gathering>()).Where(g => g.Kind != null && (g.Kind.StartsWith("fest:") || g.Kind == "memorial" || HouseEvents.IsHouse(g) || Grammars.IsResidentKind(g.Kind)) && !g.Done && !g.Cancelled))
            {
                if (g.Cur.Room != me.Room || S.Clock < g.Cur.Start - 5 || S.Clock > g.Cur.End - (HouseEvents.IsHouse(g) ? 2 : 10)) continue;
                if (S.Flags.ContainsKey("lfestseen:" + g.Id)) continue;
                var present = FestPresent(g); if (!(present.Contains(g.Host) || g.Host == Cast.Butler) || present.Count < 2) continue;
                var run = FestBuild(g, present, true); if (run == null) continue;
                S.Flags["lfestseen:" + g.Id] = S.Clock; LFinc("lfest:seen");
                if (g.Status.TryGetValue(Cast.Player, out var st) && (st == "invited" || st == "accepted")) g.Status[Cast.Player] = "attended";
                _stage = run;
                var lines = RunFrom(run);
                if (run.Over) { FestFinished(run); _stage = null; }
                S.Log("FestScene", Cast.Player, g.Host, room: g.Cur.Room, data: g.Kind);
                return ToStage(run, lines);
            }
            return null;
        }

        void FestFinished(SceneRun run)
        {
            if (!run.Ctx.TryGetValue("gid", out var gid)) return;
            var g = (S.Gatherings ?? new List<Gathering>()).FirstOrDefault(x => x.Id == gid); if (g == null) return;
            var present = run.Cast.Where(x => x != Cast.Player).ToList();
            foreach (var p in present) if (p != g.Host) Relations.Change(S, g.Host, p, like: 0.02f);
            if (g.Kind == "memorial")
            {
                foreach (var p in present) foreach (var q in present) if (p != q) Relations.Change(S, p, q, attach: 0.02f);
                var absent = S.LivingNpcs.Where(x => !present.Contains(x.Id)).Select(x => x.Id).ToList();
                foreach (var p in present) foreach (var a in absent) S.K(p).Facts.Add($"absent-memorial:{a}:{g.Id}");
                S.Flags["lmemorialheld:" + gid] = S.Clock;
            }
            if (run.Ctx.TryGetValue("habits", out var hs))
                foreach (var subj in hs.Split(',').Where(x => x.Length > 0)) Foreshadow.HabitShared(this, subj, "quiz", present.Concat(run.Cast.Contains(Cast.Player) ? new[] { Cast.Player } : new string[0]));
            S.Log("FestDone", g.Host, room: g.Cur.Room, data: g.Kind + ":" + run.Score);
        }

        // ------------------------------------------------------------------ building each festival's scene
        SceneRun FestBuild(Gathering g, List<string> present, bool withPlayer)
        {
            var sc = new GenScene { Id = "gen:fest:" + g.Id, Kind = "fest", Title = g.Label, Npc = g.Host };
            var run = new SceneRun { Scene = sc, Kind = "fest", Npc = g.Host, Room = g.Cur.Room };
            run.Ctx["gid"] = g.Id; run.Ctx["act"] = g.Label; run.Ctx["place"] = g.Cur.Room.ToString(CultureInfo.InvariantCulture); run.Ctx["t"] = g.Host;
            run.Cast.AddRange(present); if (withPlayer) run.Cast.Add(Cast.Player);
            var others = present.Where(x => x != g.Host).ToList();
            switch (g.Kind)
            {
                case "fest:quiz": FestQuiz(sc, run, g.Host, present, withPlayer); break;
                case "fest:cards": FestCards(sc, run, g.Host, others, withPlayer); break;
                case "fest:concert": FestConcert(sc, run, g.Host, present, withPlayer); break;
                case "fest:cooking": FestCooking(sc, run, g.Host, others, withPlayer); break;
                case "fest:bar": FestBar(sc, run, g.Host, others, withPlayer); break;
                case "house:banquet": case "house:masque": case "house:hunt": case "house:stars": case "house:vigil":
                    HouseEventScene(sc, run, g, present, withPlayer); break;
                case "tea": case "cards": case "music": case "reading": case "party": case "show": case "film":
                    GatherScene(sc, run, g, present, withPlayer); break;
                case "memorial":
                    {
                        string v = S.Flags.TryGetValue("lmemv:" + g.Id, out var vn) ? "P" + ((int)vn).ToString("00", CultureInfo.InvariantCulture) : LifeLastDeath()?.Victim;
                        if (v == null) return null;
                        run.Ctx["victim"] = v; FestMemorial(sc, run, g.Host, others, v, withPlayer); break;
                    }
                default: return null;
            }
            run.Beat = sc.First;
            return sc.First == null ? null : run;
        }

        static LL FL(string who, string text, Emotion e = Emotion.Neutral, Anim g = Anim.Talk) => new LL { Who = who, Text = text, Emo = e, Gest = g };
        static LL FK(string who, string key, string to = null, Emotion e = Emotion.Neutral) => new LL { Who = who, Text = "\u0001" + key + (to != null ? "\u0002" + to : ""), Emo = e };
        static LOpt FO(string label, params LL[] reply) { var o = new LOpt { Label = label }; o.Reply.AddRange(reply); return o; }
        static LFx FFx(string from, string to, float like = 0, float trust = 0, float attach = 0, float respect = 0, string mem = null) => new LFx { From = from, To = to, Like = like, Trust = trust, Attach = attach, Respect = respect, Memory = mem };

        /// <summary>A festival voice line: the resident's own key if they have one, else the authored fallback.</summary>
        LL FV(string who, string key, string fallback, Emotion e = Emotion.Neutral) => LineBank.Has(who, key) ? FK(who, key, null, e) : FL(who, fallback, e);

        // quiz — 예담 asks about residents' habits; every answer is heard by the whole room (who knows whose thermos …)
        void FestQuiz(GenScene sc, SceneRun run, string host, List<string> present, bool withPlayer)
        {
            var pool = LifeData.Quiz.Where(q => S.A(q.Subject)?.Alive == true).OrderBy(q => present.Contains(q.Subject) ? 0 : 1).ThenBy(q => LR.F()).Take(3).ToList();
            if (pool.Count == 0) return;
            run.Ctx["habits"] = string.Join(",", pool.Select(q => q.Subject));
            sc.Open(FV(host, "fest_quiz_open", host == "P17" ? "짜잔! 저택 퀴즈쇼 제1회! 문제는 전부 여기 사는 사람들 얘기야.|틀려도 벌칙은 없어. 아마도!" : "퀴즈를 하겠습니다. 문제는 모두 여기 계신 분들 이야기예요.", Emotion.Grin));
            for (int i = 0; i < pool.Count; i++)
            {
                var q = pool[i]; string id = "q" + i;
                var beat = i == 0 ? sc.First : null;
                if (beat == null) { sc.Then(id); beat = sc.Beat(id); }
                string ask = LineBank.FixParticles(LineBank.Render(q.Ask, new Dictionary<string, string> { { "s", q.Subject == host ? "내" : CallName(host, q.Subject) } }, false));
                beat.Lines.Add(FL(host, (host == "P17" ? "문제! " : "") + ask, Emotion.Smile));
                if (!withPlayer)
                {
                    // without 민혁 a guest answers — right or wrong, the answer is said aloud all the same
                    var guess = present.FirstOrDefault(x => x != host && x != q.Subject) ?? host;
                    beat.Lines.Add(FL(guess, q.Options[0] + "?"));
                    beat.Lines.Add(FL(host, host == "P17" ? "판정합니다! 정답!" : "정답입니다.", Emotion.Grin));
                    if (present.Contains(q.Subject) && q.React != null) beat.Lines.Add(FL(q.Subject, q.React));
                    continue;
                }
                var order = new List<int> { 0, 1, 2 }; LR.Shuffle(order);
                foreach (var k in order)
                {
                    var o = FO(q.Options[k]);
                    if (k == 0)
                    {
                        o.Right(); o.Reply.Add(FL(host, host == "P17" ? "딩동! 판정합니다, 정답!" : "정답입니다.", Emotion.Grin));
                        if (present.Contains(q.Subject) && q.React != null) o.Reply.Add(FL(q.Subject, q.React, Emotion.Smile));
                        o.Do(FFx(q.Subject, "me", like: 0.02f), FFx(host, "me", like: 0.01f));
                    }
                    else
                    {
                        o.Reply.Add(FL(host, (host == "P17" ? "땡! 판정합니다, 오답! " : "아쉽네요. ") + "정답은 " + q.Options[0] + "!", Emotion.Laugh));
                        if (present.Contains(q.Subject) && q.WrongReact != null) o.Reply.Add(FL(q.Subject, q.WrongReact, Emotion.Smirk));
                    }
                    o.FactList = "bondnote:" + q.Subject + ":" + q.Note;
                    beat.Opts.Add(o);
                }
            }
            sc.Then("end",
                If2("score>=2", FL(host, host == "P17" ? "우승은 민혁! 상품은… 짜잔, 종이 모형! 네 방 거야." : "오늘 우승은 민혁 씨예요.", Emotion.Grin)),
                If2("score<=1", FL(host, host == "P17" ? "다음 회에는 더 어렵게 낼 거야. 벌칙은… 없어! 히히." : "다음에 또 해요.", Emotion.Smile)));
            if (withPlayer)
            {
                var end = sc.Beat("end");
                var prize = FO("상품 고마워요. 다음 회도 불러 줘요.").Do(FFx(host, "me", like: 0.04f, attach: 0.02f, mem: "퀴즈쇼에서 우승했다")); prize.If = "score>=2"; if (host == "P17") prize.GiftType = "PaperModel";
                var again = FO("다음 회도 불러 주세요.").Do(FFx(host, "me", like: 0.04f, attach: 0.02f, mem: "퀴즈쇼에 끝까지 있었다")); again.If = "score<=1";
                end.Opts.Add(prize); end.Opts.Add(again); end.Opts.Add(FO("재밌었어요. 이만 갈게요.").Do(FFx(host, "me", like: 0.02f)));
            }
        }

        static LL If2(string cond, LL l) { l.If = cond; return l; }

        // cards — 진우 deals; each opponent bets honestly or bluffs (their tell shows in the bluff); calling or folding teaches it
        void FestCards(GenScene sc, SceneRun run, string host, List<string> guests, bool withPlayer)
        {
            var opp = guests.OrderByDescending(x => CastTraits.HangoutFav(x, "cards") == CastTraits.Fav.Love ? 1 : 0).ThenBy(x => LR.F()).Take(2).ToList();
            opp.Add(host);
            sc.Open(FV(host, "fest_cards_open", "카드의 밤이야. 규칙은 하나. 걸기 전에 한마디씩 해. 그게 다야.|흐응, 표정은 공짜로 보여 주는 거고.", Emotion.Smirk));
            for (int i = 0; i < opp.Count; i++)
            {
                string o = opp[i]; var who = S.A(o); if (who == null) continue;
                string id = "r" + i; if (i > 0) sc.Then(id);
                var beat = i == 0 ? sc.First : sc.Beat(id);
                double lie = MathX.Clamp((float)(0.25 + who.Def.Deceit / 200.0), 0.25f, 0.7f);
                bool bluff = LR.Chance(lie);
                beat.Lines.Add(bluff ? FK(o, "card_bluff", null, Emotion.Smirk) : FK(o, "card_true", null, Emotion.Neutral));
                if (!withPlayer) { beat.Lines.Add(FK(o, bluff ? "card_bluff_reveal" : "card_true_win", null, bluff ? Emotion.Laugh : Emotion.Smile)); continue; }
                var call = FO("콜."); var fold = FO("폴드.");
                if (bluff)
                {
                    call.Right(); call.Reply.Add(FK(o, "card_caught", null, Emotion.Surprised)); call.Know("tell"); call.Do(FFx(o, "me", respect: 0.03f));
                    fold.Reply.Add(FK(o, "card_bluff_reveal", null, Emotion.Laugh)); fold.Know("tell");
                }
                else
                {
                    call.Reply.Add(FK(o, "card_true_win", null, Emotion.Smile)); call.Do(FFx(o, "me", like: 0.02f));
                    fold.Right(); fold.Reply.Add(FK(o, "card_true_fold", null, Emotion.Smirk));
                }
                // the tell a bluff taught belongs to this opponent: the choice's facts are applied for them
                call.About = o; fold.About = o;
                beat.Opts.Add(call); beat.Opts.Add(fold);
            }
            sc.Then("end", If2("score>=2", FV(host, "fest_cards_lose", "…큭. 오늘은 네가 이겼네. 다음엔 사탕 없이 해 볼까?", Emotion.Smirk)), If2("score<=1", FV(host, "fest_cards_win", "흐응, 다들 표정이 솔직하네. 오늘 밤은 내가 가져갈게.", Emotion.Grin)));
        }

        // concert — 재하 hosts; whoever performs takes a turn; 민혁 may accompany on the piano (tempo)
        void FestConcert(GenScene sc, SceneRun run, string host, List<string> present, bool withPlayer)
        {
            sc.Open(FV(host, "fest_concert_open", "{nick:아}, 와 줬구나. 작은 무대야. 틀려도 돼, 박수는 내가 칠게.", Emotion.Smile));
            foreach (var p in new[] { "P12", "P07", "P08", "P14" }.Where(p => present.Contains(p) && p != host)) sc.First.Lines.Add(FK(p, "fest_perform", null, Emotion.Smile));
            if (!withPlayer) { sc.First.Lines.Add(FK(present.FirstOrDefault(x => x != host) ?? host, "applause", host)); return; }
            var slow = FO("피아노 앞에 앉아 천천히 반주한다").Act(); slow.Reply.Add(FL(host, "{nick:아}, 좋다. 숨 쉴 자리가 생겼어.", Emotion.Smile)); slow.Do(FFx(host, "me", like: 0.03f));
            var even = FO("피아노 앞에 앉아 박자대로 반주한다").Act(); even.Reply.Add(FL(host, "브라보. 너 박자 좋다.", Emotion.Grin)); if (present.Contains("P08")) even.Reply.Add(FL("P08", "크. 박자 안 끌고 가네. 인정.")); even.Do(FFx(host, "me", like: 0.03f), FFx("P08", "me", respect: 0.04f));
            var fast = FO("피아노 앞에 앉아 후렴에서 속도를 올린다").Act(); fast.Reply.Add(FL(host, "어, 빨라졌다. …누가 늘 여기서 빨라졌거든. 아니야, 계속하자.", Emotion.Sad)); if (present.Contains("P08")) fast.Reply.Add(FL("P08", "빨라. 반 박자. 둘 다.")); fast.Do(FFx(host, "me", attach: 0.03f)); fast.Know("note:후렴에서 박자가 빨라지는 사람이 있었다고 한다");
            sc.Choice(slow, even, fast);
            sc.Then("end", FL(host, "고마워, 다들. 3열 7번은… 오늘도 비워 뒀어. 그냥 그래야 할 것 같아서.", Emotion.Smile));
        }

        // cooking — 준서 against a challenger; 민혁 tastes first and judges
        void FestCooking(GenScene sc, SceneRun run, string host, List<string> guests, bool withPlayer)
        {
            string ch = guests.OrderByDescending(x => CastTraits.HangoutFav(x, "cook") == CastTraits.Fav.Will ? 1 : 0).ThenBy(x => x == "P07" ? 0 : x == "P03" ? 1 : 2).FirstOrDefault();
            if (ch != null) run.Ctx["b"] = ch;
            sc.Open(FV(host, "fest_cook_open", "요리 품평회예요. 우선 앉아요. 오늘은 제 거랑 {b} 거랑, 두 그릇이에요.", Emotion.Smile));
            if (ch != null) sc.First.Lines.Add(FK(ch, "fest_cook_brag", null, Emotion.Grin));
            if (!withPlayer) { sc.First.Lines.Add(FK(guests.FirstOrDefault(x => x != ch) ?? host, "meal", host)); return; }
            sc.Then("taste", FL(host, "간 좀 봐 줄래요? 제 혀가 요즘 좀 게을러서요. 허허."));
            var bland = FO("좀 싱거워요."); bland.Reply.Add(FL(host, "…역시. 짠맛이 제일 먼저 도망갔거든요.", Emotion.Sad)); bland.Do(FFx(host, "me", trust: 0.05f, mem: "간을 솔직하게 봐 줬다")); bland.Know("note:짠맛을 잘 못 느끼는 것 같다"); bland.Mem("bland_honest");
            var good = FO("딱 좋아요."); good.Reply.Add(FL(host, "다행이다. 허허.", Emotion.Smile)); good.Do(FFx(host, "me", like: 0.02f)); good.Mem("bland_praised");
            var self = FO("직접 맛보시면 되잖아요."); self.Reply.Add(FL(host, "…그러게요. 허허.", Emotion.Blank)); self.Do(FFx(host, "me", like: -0.03f));
            sc.Choice(bland, good, self);
            if (ch != null)
            {
                sc.Then("judge", FL(host, "자, 이제 판정이에요. 솔직하게요."));
                var j1 = FO("준서 씨 요리가 이겼어요."); j1.Reply.Add(FL(host, "어이쿠. 고마워요. …{b}도 잘했어요, 진짜로.", Emotion.Smile)); j1.Reply.Add(FK(ch, "fest_cook_lost", null, Emotion.Sad)); j1.Do(FFx(host, "me", like: 0.03f), FFx(ch, "me", like: -0.02f));
                var j2 = FO("오늘은 {b} 쪽이에요."); j2.Reply.Add(FK(ch, "fest_cook_won", null, Emotion.Grin)); j2.Reply.Add(FL(host, "허허. 인정해요. 다음엔 제가 이길게요.", Emotion.Smile)); j2.Do(FFx(ch, "me", like: 0.05f), FFx(host, "me", respect: 0.02f));
                var j3 = FO("둘 다 맛있어요. 무승부로 해요."); j3.Reply.Add(FL(host, "무승부면… 둘 다 한 그릇씩 더 먹는 거예요.", Emotion.Laugh)); j3.Do(FFx(host, "me", like: 0.02f), FFx(ch, "me", like: 0.02f));
                sc.Choice(j1, j2, j3);
            }
        }

        // bar — 시온 pours; the second glass loosens a secret hint
        void FestBar(GenScene sc, SceneRun run, string host, List<string> guests, bool withPlayer)
        {
            sc.Open(FV(host, "fest_bar_open", "야! 오늘은 바의 밤이다. 첫 잔은 내가 쏜다! 크하하!", Emotion.Grin));
            foreach (var p in guests.OrderBy(x => LR.F()).Take(3)) sc.First.Lines.Add(FK(p, "fest_bar_banter", host, Emotion.Smirk));
            if (!withPlayer) return;
            var more = FO("한 잔 더 주세요."); more.Reply.Add(FL(host, "크하하, 브로 좀 마시네! 둘째 잔부터는 진심만 나온다, 인정?", Emotion.Grin)); more.Do(FFx(host, "me", like: 0.04f));
            var water = FO("저는 물로 바꿀게요."); water.Reply.Add(FL(host, "물? …그래, 그것도 의리지. 대신 끝까지 있어.", Emotion.Smile)); water.Do(FFx(host, "me", like: 0.01f));
            sc.Choice(more, water);
            sc.Then("second", FL(host, "자, 둘째 잔. 규칙은 하나. 한 명한테 하나만 물어볼 수 있다. 대답은 자유!", Emotion.Grin));
            foreach (var p in guests.Where(x => S.R(x, Cast.Player).Trust > -0.1f).OrderByDescending(x => S.R(x, Cast.Player).Like).ThenBy(x => x, StringComparer.Ordinal).Take(3))
            {
                var o = FO(CallName(Cast.Player, p) + "한테 묻는다"); o.Silent = true;
                o.Reply.Add(FK(p, "secret_hint", null, Emotion.Sad)); o.Do(FFx(p, "me", trust: 0.03f, attach: 0.02f)); o.Know("hint"); o.About = p;
                sc.Beat("second").Opts.Add(o);
            }
            sc.Then("end", FL(host, "오늘의 단어는 '잔'. 비우면 채우고, 채우면 또 비우고.|…라임 됐다, 인정?", Emotion.Grin));
        }

        // memorial — each attendee says one thing about the dead; 민혁 lays a lily and chooses whom to sit beside
        void FestMemorial(GenScene sc, SceneRun run, string host, List<string> others, string victim, bool withPlayer)
        {
            sc.Open(FV(host, "memorial_open", "와 주셔서 고맙습니다. 이제 아무도 {victim:을} 재촉하지 않겠네요.|…백합은 한 송이면 충분합니다.", Emotion.Sad));
            foreach (var p in others.OrderByDescending(x => S.R(x, victim).Like + S.R(x, victim).Attach).ThenBy(x => x, StringComparer.Ordinal).Take(4)) sc.First.Lines.Add(FK(p, "memorial_word", null, Emotion.Sad));
            if (withPlayer)
            {
                foreach (var p in others.OrderByDescending(x => S.R(x, Cast.Player).Like).ThenBy(x => x, StringComparer.Ordinal).Take(3))
                {
                    var o = FO("백합을 놓고 " + CallName(Cast.Player, p) + " 옆에 앉는다").Act();
                    o.Reply.Add(FK(p, "grief_share_silent", null, Emotion.Sad)); o.Do(FFx(p, "me", attach: 0.05f, like: 0.02f, mem: "추모 자리에서 곁에 앉아 줬다"));
                    sc.First.Opts.Add(o);
                }
            }
            sc.Then("end", FL(host, "오늘은 여기까지 하겠습니다. …불은 제가 끄고 가겠습니다.", Emotion.Sad));
        }
    }
}
