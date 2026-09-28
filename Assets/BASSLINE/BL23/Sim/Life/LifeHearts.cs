using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// §6 relationship stages with visible payoffs, subculture style:
    ///   · HEARTS — each resident's own story chain (4 events, Content/Life_Hearts_*.cs), unlocked by closeness AND days, between
    ///     and around the bond scenes. Usually the resident seeks 민혁 out with an invitation (a real appointment card through
    ///     Requests: accept / an hour later / refuse; kept or broken for real); at the appointment the scene plays. Day 2 may
    ///     double-book two invitations on purpose (DailyLife §2.1): the one he stands up remembers it.
    ///   · nicknames and 반말 change inside the chains; choices are remembered and brought back (callbacks, "mem:" conditions);
    ///   · residents come to him: small gifts (준서's bread, 라온's hot pack …), jealousy when he favours someone, a rival's
    ///     warning, rumours brought first at stage 2, grief after a death, a walk after the 심판;
    ///   · HANGOUTS — a moment with a choice at the end of time spent together (Content/Life_Hang_*.cs), with a callback variant
    ///     the second time (LifeAfterTogether, called by the presentation when a "함께 시간을 보낸다" ends).
    /// </summary>
    public sealed partial class Simulation
    {
        // ================================================================== hearts
        /// <summary>The next heart event of this resident if it is unlocked now, else null (why says what is missing — tests only).</summary>
        public HeartDef HeartReady(Actor npc, out string why)
        {
            why = null;
            if (npc == null || npc.IsPlayer || npc.IsButler || !npc.Alive) { why = "no"; return null; }
            if (S.Phase != Phase.Daily) { why = "phase"; return null; }
            int next = LifeHearts(npc.Id) + 1; var h = LifeData.Heart(npc.Id, next);
            if (h == null) { why = "none"; return null; }
            var r = S.R(npc.Id, Cast.Player);
            if (r.Grudge > 0.35f || r.Fear > 0.45f) { why = "grudge"; return null; }
            if (next == 1 && !PK.Facts.Contains("met:" + npc.Id)) { why = "unmet"; return null; }
            if (r.Like < h.NeedLike || r.Trust < h.NeedTrust || r.Attach < h.NeedAttach) { why = $"rel like {r.Like:0.00}/{h.NeedLike} trust {r.Trust:0.00}/{h.NeedTrust} attach {r.Attach:0.00}/{h.NeedAttach}"; return null; }
            if (BondStage(npc.Id) < h.NeedBond) { why = "bond"; return null; }
            if (next > 1 && S.Day < LF($"lhd:{npc.Id}:{next - 1}") + h.Gap) { why = "days"; return null; }
            if (S.Flags.TryGetValue($"bondday:{npc.Id}", out var bd) && (int)bd == S.Day) { why = "bondtoday"; return null; }
            var probe = new SceneRun { Scene = h, Kind = "heart", Npc = npc.Id, Room = npc.Room };
            if (!LifeIf(h.If, probe)) { why = "cond"; return null; }
            return h;
        }

        /// <summary>"「…」 — 따로 할 얘기가 있어 보인다": in the right kind of room it is told now; elsewhere the resident names a time and place.</summary>
        List<Utterance> AskHeart(Actor npc)
        {
            var res = new List<Utterance>();
            var h = HeartReady(npc, out _); if (h == null) return res;
            var room = S.Layout.Room(npc.Room);
            if (RoomFits(h.Where, room) || string.IsNullOrEmpty(h.Where)) return BeginDlg(npc, h, "heart");
            var req = HeartRequest(npc, h, true, out string text);
            if (req == null) return BeginDlg(npc, h, "heart");
            res.Add(new Utterance { Speaker = npc.Id, Listener = Cast.Player, Key = "request", Text = text, Emotion = Emotion.Smile, Gesture = Anim.Talk });
            S.Log("HeartInvite", npc.Id, Cast.Player, room: req.Room, data: h.Id + ":asked");
            return res;
        }

        /// <summary>An invitation for a heart event: a real appointment (Requests) at a room that fits, an hour or two ahead.</summary>
        Request HeartRequest(Actor npc, HeartDef h, bool accepted, out string text)
        {
            text = null;
            var rooms = S.Layout.Rooms.Where(r => RoomFits(h.Where, r) && RoomUsable(npc, r)).OrderBy(r => r.Id).ToList();
            if (rooms.Count == 0) return null;
            var room = rooms.OrderBy(r => npc.Pos.Dist(new P3(r.Floor, r.Rect.CX, r.Rect.CZ))).First();
            double at = Math.Ceiling((S.Clock + (accepted ? 40 : 70) + LR.Range(0, 50)) / 10.0) * 10.0;
            int m = (int)Math.Floor(at % 1440); if (m > 21 * 60 + 30 || m < 8 * 60) return null;
            // day 2: one deliberate double booking (DailyLife §2.1) — the same hour as an appointment already made
            if (!accepted && S.Day == 2 && !S.Flags.ContainsKey($"lclash:{S.Loop}"))
            {
                var other = (S.Requests ?? new List<Request>()).Where(x => x.Kind == "invite" && x.State == "accepted" && x.At > S.Clock + 40 && x.From != npc.Id).OrderBy(x => x.At).FirstOrDefault();
                if (other != null) { at = other.At; S.Flags[$"lclash:{S.Loop}"] = S.Day; }
            }
            var run = new SceneRun { Scene = h, Kind = "heart", Npc = npc.Id, Room = room.Id };
            run.Ctx["place"] = room.Id.ToString(CultureInfo.InvariantCulture); run.Ctx["time"] = at.ToString(CultureInfo.InvariantCulture);
            bool cas = CasualTo(npc.Id, Cast.Player);
            string raw = cas && h.InviteC != null ? h.InviteC : h.Invite ?? (cas ? "{time}에 {place}에서 볼래? 할 얘기가 있어." : "{time}에 {place}에서 뵐 수 있을까요? 드릴 얘기가 있어요.");
            text = LifeRenderText(raw, npc.Id, Cast.Player, run);
            var req = new Request { Id = S.NewId("rq"), From = npc.Id, Kind = "invite", Activity = Activities.Get(h.Act) != null ? h.Act : "tea", Room = room.Id, At = at, Made = S.Clock, Due = at + 25, State = accepted ? "accepted" : "offered", Text = text };
            if (S.Requests == null) S.Requests = new List<Request>();
            S.Requests.Add(req);
            S.Flags["lreq:" + req.Id] = h.No;
            return req;
        }

        void HeartTick(int mod)
        {
            LifeCheckDue();
            if (mod < 9 * 60 || mod > 20 * 60) return;
            var me = S.Player; if (!LifePlayerAround() || PlayerInTogether()) return;
            if (S.Requests != null && S.Requests.Any(r => r.State == "offered")) return;
            if (LF($"lhinv:{S.Day}") >= 2) return;
            if (S.Flags.TryGetValue("req:lastoffer", out var lastOffer) && S.Clock - lastOffer < (OnDemand ? 90 : 45)) return;
            if (S.LivingNpcs.Any(x => x.Act != null && x.Act.Id != null && (x.Act.Id.StartsWith("req:") || x.Act.Id.StartsWith("life:seek")))) return;
            var cands = new List<(Actor a, HeartDef h)>();
            foreach (var a in S.LivingNpcs.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                if (!LifeFree(a) || a.Pos.f != me.Pos.f && a.Pos.Dist(me.Pos) > 60) continue;
                if ((S.Requests ?? new List<Request>()).Any(r => r.From == a.Id && (r.State == "offered" || r.State == "accepted"))) continue;
                if (LifeGoFor(a.Id) != null) continue;
                var h = HeartReady(a, out _); if (h != null) cands.Add((a, h));
            }
            if (cands.Count == 0 || !LR.Chance(0.55)) return;
            var pick = LR.Weighted(cands, c => 0.3 + S.R(c.a.Id, Cast.Player).Like + S.R(c.a.Id, Cast.Player).Attach);
            var req = HeartRequest(pick.a, pick.h, false, out _); if (req == null) return;
            S.Flags["req:lastoffer"] = S.Clock; LFinc($"lhinv:{S.Day}"); LFinc("lhinv:total");
            var ask = new Activity { Id = "req:" + req.Id, Label = "민혁 찾아가기", Priority = 30 };
            ask.Steps.Add(new ActionStep { Kind = "Talk", Actor = Cast.Player });
            Assign(pick.a, ask);
            S.Log("HeartInvite", pick.a.Id, Cast.Player, room: req.Room, data: pick.h.Id);
        }

        /// <summary>Appointments made for heart events: met → the scene is due (the presentation opens the conversation);
        /// missed → remembered (and whoever 민혁 was with instead is resented a little). Zero time; safe to call every frame.</summary>
        public void LifeCheckDue()
        {
            if (S.Requests == null) return;
            foreach (var r in S.Requests)
            {
                if (!S.Flags.TryGetValue("lreq:" + r.Id, out var no)) continue;
                if (r.State == "met")
                {
                    S.Flags.Remove("lreq:" + r.Id);
                    LifePurpose(r.From, "heart", ((int)no).ToString(CultureInfo.InvariantCulture));
                    S.Emit(GameEventType.Notice, r.From, Cast.Player, text: null, key: "life_due");
                }
                else if (r.State == "missed" || r.State == "expired" || r.State == "refused")
                {
                    S.Flags.Remove("lreq:" + r.Id);
                    if (r.State == "missed")
                    {
                        Remember(r.From, "missed_heart");
                        PK.Facts.Add($"life:cb:{r.From}:{S.Day}:{(CasualTo(r.From, Cast.Player) ? "어제 약속한 데서 한참 기다렸어. …됐어, 오늘은 온 거니까." : "어제 약속한 곳에서 한참 기다렸어요. …괜찮아요. 오늘은 오셨으니까요.")}");
                        var chosen = S.Requests.FirstOrDefault(x => x != r && x.State == "met" && Math.Abs(x.At - r.At) <= 30);
                        if (chosen != null) Relations.Change(S, r.From, chosen.From, jealous: 0.06f, memory: "민혁은 그 시간에 저 사람과 있었다");
                    }
                }
            }
        }

        // ================================================================== hangouts (F7)
        /// <summary>Presentation: a "함께 시간을 보낸다" just ended (full = it ran its course). When the resident has a moment for
        /// this activity (their ♥, first time or the callback variant), it is queued as what they have to say — true means
        /// "open the conversation now".</summary>
        public bool LifeAfterTogether(string npcId, string kind, string activity, bool full)
        {
            try
            {
                if (!full || S.Phase != Phase.Daily) return false;
                var npc = S.A(npcId); if (npc == null || !npc.Alive || npc.IsButler) return false;
                LFinc($"lfav:{S.Day}:{npcId}");
                if (S.Flags.ContainsKey($"lhang:{npcId}:{S.Day}")) return false;
                foreach (var act in HangActs(npc, kind, activity))
                {
                    var defs = LifeData.Hangs.Where(h => h.Npc == npcId && h.Act == act && LifeIf(h.If, new SceneRun { Scene = h, Kind = "hang", Npc = npcId, Room = npc.Room })).ToList();
                    if (defs.Count == 0) continue;
                    HangDef pick;
                    if (LifeMem(npcId, "hang:" + act)) pick = defs.FirstOrDefault(h => h.Again) ?? (LR.Chance(0.3) ? defs.FirstOrDefault(h => !h.Again) : null);
                    else pick = defs.FirstOrDefault(h => !h.Again);
                    if (pick == null) continue;
                    LifePurpose(npcId, "hang", pick.Id);
                    return true;
                }
            }
            catch (Exception e) { Fault("life:together", e); }
            return false;
        }

        /// <summary>The hangout activities (DailyLife §4.3/§4.4 columns) a time together can be, most specific first.</summary>
        static IEnumerable<string> HangActs(Actor npc, string kind, string activity)
        {
            bool Loves(string act) => CastTraits.HangoutFav(npc.Id, act) == CastTraits.Fav.Love;
            switch (kind)
            {
                case "tea": yield return "tea"; yield return "talk"; yield break;
                case "meal": yield return "meal"; yield return "cook"; yield return "talk"; yield break;
                case "talk_long": yield return "talk"; if (Loves("walk")) yield return "walk"; if (Loves("bar")) yield return "bar"; yield break;
            }
            switch (activity)
            {
                case "read": case "investigate": case "puzzle": yield return "read"; break;
                case "music": case "listen": yield return "piano"; break;
                case "game":
                    foreach (var g in new[] { "cards", "billiards", "darts" }.OrderByDescending(g => Loves(g) ? 1 : 0)) yield return g;
                    break;
                case "chess": yield return "chess"; break;
                case "garden": yield return "garden"; break;
                case "cook": case "snack": yield return "cook"; break;
                case "swim": case "exercise": yield return "swim"; break;
                case "craft": case "restore": case "style": yield return "craft"; break;
                case "repair": case "inspect": yield return "repair"; break;
                case "perform": case "speech": yield return "lines"; break;
                case "bar": case "party": yield return "bar"; break;
                case "film": case "exhibit": yield return "darkroom"; break;
                case "tea": yield return "tea"; break;
                case "walk": case "browse": case "observe": yield return "walk"; break;
            }
            yield return "talk";
        }

        // ================================================================== residents who come to 민혁
        /// <summary>What each resident gives when they come with a small gift (DailyLife §5: bread from 준서, a hot pack from 라온 …).</summary>
        static readonly Dictionary<string, string> GiveType = new Dictionary<string, string>
        {
            ["P02"] = "Candy", ["P03"] = "Sticker", ["P04"] = "Cup", ["P05"] = "Chocolate", ["P06"] = "Chocolate", ["P07"] = "Snack", ["P08"] = "HandWarmer", ["P09"] = "Flower",
            ["P10"] = "Bread", ["P11"] = "WindupToy", ["P12"] = "Sticker", ["P13"] = "Soda", ["P14"] = "Flower", ["P15"] = "Notebook", ["P16"] = "Button", ["P17"] = "PaperModel", ["P18"] = "Bread",
        };

        void SeekTick(int mod)
        {
            if (!LifePlayerAround() || PlayerInTogether()) return;
            var me = S.Player;
            // new purposes: small gifts from those who have grown close (early for 준서's bread and 라온's hot packs)
            if (mod >= 10 * 60 && mod <= 20 * 60 && mod % 60 == 30)
            {
                foreach (var a in S.LivingNpcs.OrderBy(x => x.Id, StringComparer.Ordinal))
                {
                    if (!GiveType.ContainsKey(a.Id) || S.Flags.TryGetValue("lgift:" + a.Id, out var gd) && S.Day - gd < 2) continue;
                    var r = S.R(a.Id, Cast.Player);
                    bool early = (a.Id == "P10" || a.Id == "P08") && r.Like >= 0.2f;
                    if (!(early || BondStage(a.Id) >= 2 || r.Like >= 0.38f) || r.Grudge > 0.2f) continue;
                    if (!LR.Chance(0.2)) continue;
                    S.Flags["lgift:" + a.Id] = S.Day;
                    LifePurpose(a.Id, "gift", GiveType[a.Id]);
                    break;
                }
            }
            // a resident drawn to 민혁 (stage 2+, an attraction) lets it show (anyone: the owner lifted the exclusions)
            if (mod >= 11 * 60 && mod <= 21 * 60 && mod % 60 == 45)
            {
                foreach (var a in S.LivingNpcs.OrderBy(x => x.Id, StringComparer.Ordinal))
                {
                    if (NoRomance(a.Id) || BondStage(a.Id) < 2 || S.Flags.TryGetValue("lflirt:" + a.Id, out var fd) && S.Day - fd < 2) continue;
                    var r = S.R(a.Id, Cast.Player);
                    if (!(r.Romance > 0.15f || r.Like > 0.45f && r.Attach > 0.15f) || r.Grudge > 0.15f) continue;
                    if (!(LineBank.Has(a.Id, "flirt") || LineBank.Has(a.Id, "love_hint") || LineBank.Has(a.Id, "flirt@" + Cast.Player))) continue;
                    if (!LR.Chance(0.15 + a.Def.P.Romance * 0.2)) continue;
                    S.Flags["lflirt:" + a.Id] = S.Day; LifePurpose(a.Id, "flirt", "-");
                    break;
                }
            }
            // someone with something to say walks over (one at a time, and not too often: each one stops a wait)
            if (S.Flags.TryGetValue("req:lastoffer", out var lastOffer) && S.Clock - lastOffer < (OnDemand ? 60 : 30)) return;
            if (S.LivingNpcs.Any(x => x.Act != null && x.Act.Id != null && (x.Act.Id.StartsWith("req:") || x.Act.Id.StartsWith("life:seek")))) return;
            foreach (var a in S.LivingNpcs.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                var go = LifeGoFor(a.Id); if (go == null) continue;
                var kind = go.Value.kind; if (kind == "heart" || kind == "hang" || kind == "callback") continue;
                if (!LifeFree(a)) continue;
                if (a.Pos.f != me.Pos.f && a.Pos.Dist(me.Pos) > 60) continue;
                var act = new Activity { Id = "life:seek:" + kind, Label = "민혁 찾아가기", Priority = 25 };
                act.Steps.Add(new ActionStep { Kind = "Talk", Actor = Cast.Player });
                Assign(a, act);
                S.Flags["req:lastoffer"] = S.Clock; LFinc("lseek:total"); LFinc($"lseek:{kind}");
                S.Log("LifeSeek", a.Id, Cast.Player, data: kind + ":" + go.Value.arg);
                break;
            }
        }

        /// <summary>20:00: who did 민혁 favour today? Those attached to him who got nothing notice; a crush or a sibling of the
        /// favoured one warns him off (CharacterBible ties #4, #6, #12).</summary>
        void JealousyTick()
        {
            var favs = S.LivingNpcs.Select(x => (id: x.Id, n: LF($"lfav:{S.Day}:{x.Id}"))).Where(x => x.n >= 2).OrderByDescending(x => x.n).ThenBy(x => x.id, StringComparer.Ordinal).ToList();
            if (favs.Count == 0) return;
            string top = favs[0].id; int made = 0;
            foreach (var y in S.LivingNpcs.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                if (y.Id == top || made >= 1) continue;
                var r = S.R(y.Id, Cast.Player);
                if (LF($"lfav:{S.Day}:{y.Id}") > 0) continue;
                if (r.Attach < 0.08f && BondStage(y.Id) < 2) continue;
                if (!LR.Chance(Math.Min(0.8, 0.25 + r.Attach * 2 + y.Def.P.Jealousy * 0.4))) continue;
                LifePurpose(y.Id, "jealous", top); Relations.Change(S, y.Id, top, jealous: 0.04f); made++;
                S.Log("LifeJealous", y.Id, top, data: "me");
            }
            foreach (var t in CastWeb.Ties.Where(t => t.B == top && (t.Type == "crush" || t.Type == "protect" || t.Type == "siblings" || t.Type == "fan") || t.Mutual && t.A == top && t.Type == "siblings"))
            {
                string from = t.B == top ? t.A : t.B; var fa = S.A(from); if (fa == null || !fa.Alive || from == Cast.Player) continue;
                if (LifeGoFor(from) != null) continue;
                LifePurpose(from, "rival", top); Relations.Change(S, from, Cast.Player, jealous: 0.03f);
                S.Log("LifeJealous", from, top, data: "rival");
                break;
            }
        }
    }
}
