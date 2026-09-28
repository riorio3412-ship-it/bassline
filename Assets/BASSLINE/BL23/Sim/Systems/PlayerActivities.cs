using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>What the player can do with the furniture of a room: sit, read, play, cook, garden, perform, swim,
    /// listen, look... Simple things (opening, sitting, looking, pouring tea, winding a clock) are instant; pastimes
    /// (Minutes > 0) pass real world time (interrupted by screams, knocks, announcements), change needs, may produce
    /// something (a dish, a flower, a paper model) and may draw nearby people in — which is how bonds grow.</summary>
    public sealed class PlayerAction
    {
        public string Id; public string Label; public string Activity; public double Minutes;
        public bool Pastime => Minutes > 0;   // --- time-on-demand: an instant action has Minutes = 0
    }

    public sealed class PlayerActivityResult
    {
        public string Text; public string Joined; public string JoinLine; public string ItemMade; public string LoreTitle; public string LoreText;
        public string Music;      // presentation hint: play a track here ("any", or a specific key)
        public double Minutes; public bool Interrupted;
    }

    public sealed partial class Simulation
    {
        // --- time-on-demand (begin): seats (+ottoman, day bed, rocking chair); the rest of this file is restructured so that
        // simple actions are instant (no mate roll, no waiting) and a pastime is Prepare → time → Finish (the time may be a
        // headless Wait or a presented skip: Systems/TimeFlow.cs).
        static readonly HashSet<string> Seats = new HashSet<string> { "Chair", "Armchair", "Sofa", "Bench", "BarStool", "Pew", "Lounger", "AudSeat", "WaitBench", "InfirmaryBed", "Ottoman", "DayBed", "RockingChair" };
        static readonly HashSet<string> InvestigationUse = new HashSet<string> { "view", "read", "sit", "search", "inventory", "wind", "log", "wash" };

        public List<PlayerAction> FurnitureActions(Furniture f)
        {
            var list = new List<PlayerAction>(); if (f == null) return list;
            void A(string id, string label, string act, double min) => list.Add(new PlayerAction { Id = id, Label = label, Activity = act, Minutes = min });
            var room = S.Layout.Room(f.Room);
            // things in a room you can open (doors, drawers, lids — or just rummage through), first
            switch (f.Type)
            {
                case "Wardrobe": case "Chest": case "Cabinet": case "Sideboard": case "FileCabinet": case "Nightstand": case "Desk": case "Crates": case "Shelves": case "MedCabinet": case "DollShelf": case "Barrel": case "CostumeRack":
                case "Fridge": case "ColdLocker": case "ButlerDesk": case "CardCatalog": case "DisplayCase": case "VanityDesk":
                    A("search", room?.Owner != null && room.Owner != Cast.Player ? "몰래 열어 보기 (남의 방)" : "열어 보기", "browse", 0); break;
                case "Counter": if (room?.Type == RoomType.Kitchen) A("search", "열어 보기", "browse", 0); break;
            }
            switch (f.Type)
            {
                case "Bookshelf": case "ReadingTable": case "CardCatalog": A("read", "책 읽기", "read", 30); break;
                case "Piano": case "Piano_Upright": case "Organ": case "Harp": case "Drums": A("play_music", "연주하기", "music", 25); break;
                case "Gramophone": case "Jukebox": A("listen", "음반 걸기", "listen", 0); A("listen_long", "음악 감상", "listen", 25); break;
                case "PoolTable": case "GameTable": case "Arcade": case "Dartboard": A("game", "한 판 하기", "game", 30); break;
                case "ChessTable": A("chess", "체스 두기", "chess", 35); break;
                case "Stove": case "Counter": case "Island": A("cook", "요리하기", "cook", 35); break;
                case "Planter": A("garden", "화초 돌보기", "garden", 25); break;
                case "Pew": case "Altar": A("pray", "추모하기", "pray", 15); break;
                case "Stage": case "Lectern": A("perform", room?.Type == RoomType.Chapel ? "낭독하기" : "무대에 서기", "perform", 25); break;
                case "PoolWater": case "DivingBoard": A("swim", "수영하기", "swim", 30); break;
                case "Workbench": case "SewingTable": case "Easel": A("craft", "뭔가 만들어 보기", "craft", 35); break;
                case "Telescope": case "Globe": case "BirdCage": case "DisplayCase": case "Aquarium": case "Fountain": case "FrameWall": A("view", "들여다보기", "browse", 0); break;
                case "BarCounter": A("drink", "한잔하기", "bar", 0); break;
                case "FilmProjector": A("film", "영사기 돌리기", "film", 20); break;
                case "PunchingBag": A("exercise", "운동하기", "exercise", 25); break;
                case "VanityDesk": case "Mirror": A("groom", "몸단장하기", "style", 0); break;
            }
            // things you count, set right or tend (spec §06: 관찰하기·주고받기 around objects)
            switch (f.Type)
            {
                case "KnifeRack": case "ToolWall": case "WineRack": A("inventory", "빠진 것이 없는지 세어 보기", "browse", 0); break;
                case "Clock": case "ClockCase": A("wind", "태엽 감고 시각 맞추기", "browse", 0); break;
                case "Fireplace": A("fire", "불 지피기", "rest", 0); break;
                case "Sink": A("wash", "손 씻기", "laundry", 0); break;
                case "Washer": A("wash", "빨래하기", "laundry", 20); break;
                case "TeaCart": A("tea", "차 따르기", "tea", 0); break;
                case "DoorLogger": case "Terminal": A("log", "출입 기록 확인", "investigate", 0); break;
            }
            if (f.Type == "Aquarium" || f.Type == "BirdCage") A("feed", "먹이 주기", "browse", 0);
            if (Seats.Contains(f.Type)) A("sit", "앉기", "rest", 0);
            // a dining chair at a meal: eat with whoever is at the table (until the meal is over)
            if (f.Type == "Chair" && room?.Type == RoomType.Dining && S.Phase == Phase.Daily && MealTime(out int slot))
                A("eat", "함께 식사하기", "eat", Math.Max(10, Math.Min(45, MealStart[slot] + MealLen - S.Minute)));
            if (f.Type == "Bed" && room?.Owner == Cast.Player) A("nap", "잠깐 눈 붙이기", "rest", 45);
            // instant things first, then the ones that take time
            return list.Where(a => a.Minutes <= 0).Concat(list.Where(a => a.Minutes > 0)).ToList();
        }

        /// <summary>A use in progress: validated and prepared (company may have joined), finished with the minutes really spent.</summary>
        internal sealed class UseCtx
        {
            public Furniture F; public PlayerAction Act; public ActivityDef Def; public Actor Mate; public string Fail; public double Minutes;
            public PlayerActivityResult Res = new PlayerActivityResult(); public bool KernelSeated;
        }

        internal UseCtx PrepareUse(Furniture f, string actionId, bool forPlan)
        {
            var ctx = new UseCtx { F = f };
            var act = f == null ? null : FurnitureActions(f).FirstOrDefault(x => x.Id == actionId);
            if (act == null || S.Phase != Phase.Daily && S.Phase != Phase.Investigation) { ctx.Fail = "지금은 할 수 없다"; return ctx; }
            if (S.Phase == Phase.Investigation && !InvestigationUse.Contains(actionId) && act.Minutes > 0) { ctx.Fail = "수사 중이다. 한가하게 있을 때가 아니다"; return ctx; }
            var me = P; var room = S.Layout.Room(f.Room);
            if (actionId == "cook" && !(room?.Type == RoomType.Kitchen)) { ctx.Fail = "여기서는 요리할 수 없다"; return ctx; }
            ctx.Act = act; ctx.Def = Activities.Get(act.Activity); ctx.Minutes = act.Minutes;
            if (act.Minutes <= 0) return ctx;   // instant: no company, no waiting
            // company: people nearby who like doing this, or like me, may come and join
            var rng = S.R(Stream.Life); var def = ctx.Def;
            var cands = S.LivingNpcs.Where(x => x.Room == me.Room && x.Status == ActorStatus.Active && x.TalkingTo == null && x.PlanId == null && x.Pose != Pose.Sleep
                                                && (x.Act == null || x.Act.Interruptible) && x.Pos.Dist(me.Pos) < 9f).ToList();
            if (actionId == "eat" || actionId == "nap") cands.Clear();   // the table is company already; nobody joins a nap
            Actor mate = null;
            foreach (var x in cands.OrderBy(_ => rng.F()))
            {
                var r = S.R(x.Id, Cast.Player);
                bool likes = x.Def.Hobbies.Any(h => Activities.HobbyToActivity.TryGetValue(h, out var aid) && aid == act.Activity);
                double p = (likes ? 0.55 : 0.1) + Math.Max(0, r.Like) * 0.5 + x.Def.P.Sociability * 0.15 - (r.Grudge > 0.3f ? 0.5 : 0);
                if (def?.Group == true || actionId == "chess" || actionId == "game" || actionId == "perform" || actionId == "play_music") p += 0.15;
                if (rng.Chance(MathX.Clamp01((float)p))) { mate = x; break; }
            }
            if (mate != null)
            {
                var join = Simple(mate, act.Activity, me.Room);
                if (join != null) { join.Id = "social:join:" + Cast.Player; join.Label = "민혁과 " + (def?.Kor ?? act.Label); Assign(mate, join); }
                ctx.Mate = mate; ctx.Res.Joined = mate.Id;
                ctx.Res.JoinLine = Speak(mate, "invite_yes", Cast.Player);
            }
            return ctx;
        }

        /// <summary>The body goes into the pastime: a nap lies down on the bed; a seat the presentation already took is kept.</summary>
        internal void BeginUse(UseCtx ctx, SkipPlan plan)
        {
            if (ctx == null || ctx.Fail != null) return;
            var me = P;
            if (ctx.Act.Id == "nap" && !(me.Spot >= 0 && me.Pose == Pose.Sleep)) { if (PlayerSit(ctx.F, me.Pos, true) != null) ctx.KernelSeated = true; else me.Pose = Pose.Sleep; }
            me.Anim = ctx.Def?.Anim ?? Anim.Idle;
            if (ctx.Act.Id == "nap") me.Anim = Anim.Sleep;
        }

        public PlayerActivityResult PlayerUse(Furniture f, string actionId)
        {
            var ctx = PrepareUse(f, actionId, false);
            if (ctx.Fail != null) return new PlayerActivityResult { Text = ctx.Fail };
            if (ctx.Act.Minutes <= 0) return FinishUse(ctx, 0, null);
            // the time itself (headless: the world keeps going; alarms cut it short)
            BeginUse(ctx, null);
            if (P.Spot < 0 && (actionId == "read" || actionId == "listen_long")) { var sp = PlayerSit(f, P.Pos); if (sp != null) ctx.KernelSeated = true; }
            double start = S.Clock;
            Wait(ctx.Minutes, () => S.Phase != Phase.Daily && S.Phase != Phase.Investigation);
            return FinishUse(ctx, S.Clock - start, null);
        }

        /// <summary>Needs, outcomes and company for a use that took spentMinutes (0 for an instant action).</summary>
        internal PlayerActivityResult FinishUse(UseCtx ctx, double spentMinutes, SkipPlan plan)
        {
            if (ctx == null) return new PlayerActivityResult { Text = "지금은 할 수 없다" };
            var res = ctx.Res; if (ctx.Fail != null) { res.Text = ctx.Fail; return res; }
            var f = ctx.F; var act = ctx.Act; var def = ctx.Def; var mate = ctx.Mate; string actionId = act.Id;
            var rng = S.R(Stream.Life); var me = P; var room = S.Layout.Room(f.Room);
            bool instant = act.Minutes <= 0;
            float frac = 1f;
            if (instant)
            {
                res.Minutes = 0;
                // every instant thing but sitting gets a seated player up first
                if (actionId == "sit")
                {
                    var sp = PlayerSit(f, me.Pos);
                    S.Log("PlayerActivity", Cast.Player, room: me.Room, data: actionId);
                    res.Text = sp != null ? (FurnitureCatalog.Get(f.Type)?.Kor ?? "의자") + "에 앉았다" : "비어 있는 자리가 없다";
                    res.Text = LineBank.FixParticles(res.Text);
                    return res;
                }
                if (me.Spot >= 0) PlayerStand();
            }
            else
            {
                res.Minutes = spentMinutes; res.Interrupted = spentMinutes < ctx.Minutes * 0.8;
                frac = (float)Math.Min(1, spentMinutes / Math.Max(1, ctx.Minutes));
                if (ctx.KernelSeated && me.Spot >= 0) PlayerStand();
                else if (me.Spot < 0 && me.Pose != Pose.Crouch) me.Pose = Pose.Stand;
                me.Anim = Anim.Idle;
            }
            if (def != null)
            {
                var n = me.Needs;
                n.Fun = MathX.Clamp01(n.Fun + def.Fun * 2 * frac); n.Stress = MathX.Clamp01(n.Stress + def.Stress * frac);
                n.Energy = MathX.Clamp01(n.Energy + (actionId == "nap" ? 0.35f : def.Energy) * frac); n.Social = MathX.Clamp01(n.Social + (mate != null ? 0.25f : 0));
            }
            S.Log("PlayerActivity", Cast.Player, mate?.Id, room: me.Room, data: actionId);
            if (res.Interrupted) { res.Text = "하던 걸 도중에 그만뒀다"; return res; }
            // outcomes
            var k = S.K(Cast.Player);
            switch (actionId)
            {
                case "read":
                    {
                        var e = Pick(Lore.Books, k, rng); if (e != null) { res.LoreTitle = e.Title; res.LoreText = S.Loop > 1 && e.AltText != null ? e.AltText : e.Text; }
                        res.Text = e != null ? "책을 읽었다: " + e.Title : "책장을 뒤적였지만 전부 읽은 책이다";
                        break;
                    }
                case "view":
                    {
                        var e = f.Type == "FrameWall" ? Lore.Views.FirstOrDefault(x => x.Id == "v_portrait") : f.Type == "Telescope" ? PickWhere(Lore.Views, k, rng, x => x.Id.StartsWith("v_moon") || x.Id.StartsWith("v_window") || x.Id.StartsWith("v_stars"))
                              : Lore.Views.FirstOrDefault(x => x.Id == (f.Type == "Globe" ? "v_globe" : f.Type == "BirdCage" ? "v_cage" : f.Type == "DisplayCase" ? "v_case" : f.Type == "Aquarium" ? "v_aquarium" : f.Type == "Fountain" ? "v_fountain" : "v_pool"));
                        if (e != null) { res.LoreTitle = e.Title; res.LoreText = e.Text; k.Facts.Add("lore:" + e.Id + ":" + S.Loop); }
                        // the clockwork bird repeats what it last heard in this room
                        if (f.Type == "BirdCage")
                        {
                            var said = S.Ledger.LastOrDefault(x => x.Type == "Speech" && x.Room == f.Room && x.Actor != Cast.Player && S.Clock - x.Clock < 180);
                            if (said != null) res.LoreText += $"\n새가 흉내 낸 말: \"{(said.Data ?? "").Split('|').Last()}\"";
                        }
                        res.Text = e?.Title ?? "들여다보았다";
                        break;
                    }
                case "film": { var e = Pick(Lore.Films, k, rng); if (e != null) { res.LoreTitle = e.Title; res.LoreText = e.Text; } res.Text = "영사기를 돌렸다"; break; }
                case "cook":
                    {
                        int mnow = S.Minute; string type = mnow < 11 * 60 ? "Bread" : mnow < 15 * 60 ? rng.Pick(new[] { "Snack", "Bread" }) : mnow < 18 * 60 ? rng.Pick(new[] { "Tea", "Chocolate" }) : rng.Pick(Lore.CookResults);
                        var it = MakeItem(type, me);
                        res.ItemMade = it.Id; res.Text = $"{it.Kor}을(를) 만들었다. 누군가에게 줘도 좋겠다";
                        if (MealStart.Any(m => m - S.Minute > 0 && m - S.Minute <= 60)) { S.Flags[$"cooked:{S.Day}:{MealStart.First(m => m - S.Minute > 0 && m - S.Minute <= 60)}"] = 1; foreach (var x in S.LivingNpcs.Where(x => x.Def.Hobbies.Contains("cook"))) Relations.Change(S, x.Id, Cast.Player, like: 0.03f, memory: "식사 준비를 거들어 줬다"); }
                        break;
                    }
                case "garden":
                    {
                        string key = "garden:" + f.Id; int n0 = S.Flags.TryGetValue(key, out var gv) ? (int)gv : 0; S.Flags[key] = n0 + 1;
                        if ((n0 + 1) % 2 == 0) { var it = MakeItem("Flower", me); res.ItemMade = it.Id; res.Text = "꽃이 피어 한 송이를 꺾었다"; }
                        else res.Text = "흙을 고르고 물을 줬다. 다음엔 꽃이 필 것 같다";
                        break;
                    }
                case "craft": { var it = MakeItem(f.Type == "SewingTable" ? rng.Pick(new[] { "Button", "Sticker" }) : f.Type == "Easel" ? rng.Pick(new[] { "Sticker", "PaperModel" }) : rng.Pick(new[] { "PaperModel", "WindupToy" }), me); res.ItemMade = it.Id; res.Text = $"{it.Kor}을(를) 만들었다"; break; }
                case "drink": { var it = MakeItem(rng.Pick(new[] { "Tea", "Soda", "Beer" }), me); res.ItemMade = it.Id; res.Text = $"{it.Kor} 한 잔을 챙겼다"; break; }
                case "play_music": case "listen": res.Music = "any"; res.Text = actionId == "listen" ? "음반을 걸었다" : "한 곡 연주했다"; break;
                case "listen_long": me.Needs.Stress = MathX.Clamp01(me.Needs.Stress - 0.08f); res.Text = "음반 한 면을 끝까지 들었다. 마음이 조금 가라앉았다"; break;
                case "eat":
                    {
                        me.Needs.Hunger = 0.05f; me.Needs.LastMeal = S.Clock;
                        if (MealTime(out int ms)) S.Flags[$"ate:{Cast.Player}:{S.Day}:{ms}"] = 1;
                        var diners = S.LivingNpcs.Where(x => x.Room == f.Room && x.Pos.DistXZ(me.Pos) < 2.6f && (x.Pose == Pose.Sit || x.Anim == Anim.Eat)).OrderBy(x => x.Id).ToList();
                        foreach (var x in diners) { float mul = TogetherDiminish(x.Id, false); Relations.Change(S, x.Id, Cast.Player, like: 0.02f * mul, memory: mul >= 1 ? "민혁과 같은 식탁에서 밥을 먹었다" : null); }
                        res.Text = diners.Count > 0 ? $"{string.Join(", ", diners.Take(3).Select(x => Cast.GivenOf(x.Id)))}와(과) 함께 식사했다" : "조용히 식사를 마쳤다";
                        break;
                    }
                case "perform":
                    {
                        var audience = S.LivingNpcs.Where(x => x.Room == me.Room && x.Pose != Pose.Sleep && x != mate).ToList();
                        foreach (var x in audience) Relations.Change(S, x.Id, Cast.Player, like: x.Def.P.Sociability > 0.5f ? 0.03f : 0.01f, memory: "민혁의 무대를 봤다");
                        res.Text = audience.Count > 0 ? $"관객 {audience.Count}명이 지켜봤다" : "빈 객석을 향해 섰다";
                        break;
                    }
                case "pray": me.Needs.Grief = MathX.Clamp01(me.Needs.Grief * 0.6f); res.Text = "잠시 눈을 감았다"; break;
                case "swim": me.Wet = true; me.WetUntil = S.Clock + 40; res.Text = "물살을 가르고 나왔다"; break;
                case "groom": if (me.BloodOnClothes > 0) { me.BloodOnClothes *= 0.5f; } res.Text = "옷매무새를 다듬었다"; break;
                case "nap": res.Text = "잠깐 잠들었다"; break;
                case "search":
                    {
                        string fk = FurnitureCatalog.Get(f.Type)?.Kor ?? "가구";
                        float reach = Math.Max(f.W, f.D) * 0.6f + 0.9f;
                        // --- concealment: a thing stashed in another piece nearby is not in this one; 민혁's own stash stays put away
                        var found = S.Items.Values.Where(i => i.Holder == null && (i.StashF >= 0 ? i.StashF == f.Id : i.Room == f.Room && i.Pos.DistXZ(f.Pos) < reach) && !(i.StashF >= 0 && k.Facts.Contains($"stash:{i.Id}:{i.StashF}"))).OrderBy(i => i.Hidden ? 0 : 1).ThenBy(i => i.Id).Take(3).ToList();
                        foreach (var i in found.Where(i => i.Hidden)) { i.Hidden = false; i.StashF = -1; S.Log("ItemFound", Cast.Player, item: i.Id, room: f.Room, data: "search"); S.Emit(GameEventType.ItemMoved, Cast.Player, data: i.Id, text: "revealed", pos: i.Pos, room: i.Room); }
                        foreach (var i in found.Where(i => i.Bloody || i.Washed || i.Def?.IsWeapon == true)) Evidences.ExamineItem(this, me, i);
                        res.Text = found.Count == 0 ? $"{fk} 안에는 별것 없다" : $"{fk}에서 찾았다: " + string.Join(", ", found.Select(i => i.Kor + (i.Bloody ? " (붉은 얼룩)" : "")));
                        // prying in someone else's room is noticed: the owner minds a lot, anyone else a little
                        if (room?.Owner != null && room.Owner != Cast.Player)
                        {
                            var seen = S.LivingNpcs.Where(x => x.Room == me.Room && x.Pose != Pose.Sleep && x.Status == ActorStatus.Active).ToList();
                            foreach (var w in seen) Relations.Change(S, w.Id, Cast.Player, like: w.Id == room.Owner ? -0.08f : -0.02f, trust: w.Id == room.Owner ? -0.07f : -0.03f, memory: w.Id == room.Owner ? "민혁이 내 방 물건을 뒤졌다" : "민혁이 남의 방을 뒤지는 걸 봤다");
                            if (seen.Count > 0) res.Text += $". {Cast.GivenOf(seen[0].Id)}이(가) 그걸 봤다";
                        }
                        break;
                    }
                case "inventory":
                    {
                        var miss = S.Items.Values.Where(i => i.HomeRoom == f.Room && i.Def != null && i.Def.IsWeapon && !(i.Room == f.Room && i.Holder == null)).ToList();
                        Evidences.ExamineRoomQuick(this, me, f.Room);
                        res.Text = miss.Count == 0 ? "빠진 것은 없다. 모두 제자리에 있다" : "빈자리가 있다: " + string.Join(", ", miss.Select(i => i.Def.Kor).Distinct());
                        break;
                    }
                case "wind":
                    {
                        if (S.ClockOffset.TryGetValue(f.Id, out var off) && Math.Abs(off) >= 1 && (int)off != 0)
                        {
                            int offMin = (int)Math.Round(off, MidpointRounding.AwayFromZero);   // the card's words and its prop say the same minutes
                            res.Text = $"시계가 {Math.Abs(offMin)}분 {(off > 0 ? "빨랐다" : "늦었다")}. 바로 맞춰 두었다";
                            // one card per clock (winding it again later updates the same card)
                            Evidences.File(this, Cast.Player, EvKind.ObjectState, $"{S.RoomName(f.Room)} 시계", $"{S.RoomName(f.Room)} 시계가 {Math.Abs(offMin)}분 {(off > 0 ? "빨랐다" : "늦었다")}\n이 시계를 보고 시각을 말한 사람은 그만큼 틀리게 말했을 것이다", "직접 확인",
                                "clockoff:" + f.Id, S.Clock, S.Clock, f.Room, "이 시계가 틀리게 가고 있었다는 것", "언제부터, 누가 바꿨는지", true, true, $"{S.RoomName(f.Room)} 시계가 {Math.Abs(offMin)}분 {(off > 0 ? "빨랐다" : "늦었다")}.", false,
                                new Prop { Kind = PropKind.ClockOffset, Room = f.Room, Value = offMin.ToString(), T0 = S.Clock, T1 = S.Clock });
                            S.ClockOffset[f.Id] = 0; S.Log("ClockAdjust", Cast.Player, room: f.Room, data: $"{f.Id}:{off:0}:player");
                        }
                        else res.Text = "태엽을 감았다. 시계는 맞게 가고 있다";
                        break;
                    }
                case "fire": S.Flags["fire:" + f.Id] = S.Clock; me.Needs.Stress = MathX.Clamp01(me.Needs.Stress - 0.1f); res.Text = "벽난로에 불을 지폈다. 방이 조금 따뜻해졌다"; break;
                case "wash":
                    if (f.Type == "Washer") { me.BloodOnClothes = 0; res.Text = "빨래를 돌렸다. 옷이 깨끗해졌다"; }
                    else { me.BloodOnClothes *= 0.8f; res.Text = "찬물에 손을 씻었다"; }
                    break;
                case "tea": { var it = MakeItem("Tea", me); res.ItemMade = it.Id; res.Text = "차를 한 잔 따랐다. 누군가에게 건네도 좋겠다"; break; }
                case "feed": me.Needs.Fun = MathX.Clamp01(me.Needs.Fun + 0.05f); res.Text = f.Type == "Aquarium" ? "먹이를 뿌렸다. 물고기들이 한꺼번에 몰려든다" : "모이를 넣었다. 기계새가 태엽 소리를 내며 고개를 갸웃한다"; break;
                case "log":
                    {
                        var recs = S.DeviceLog.Where(d => f.Type == "Terminal" || d.Room == f.Room).OrderByDescending(d => d.Clock).Take(10).ToList();
                        if (recs.Count == 0) { res.Text = "적힌 출입 기록이 없다"; break; }
                        res.LoreTitle = (f.Type == "Terminal" ? "관리 단말" : "출입 기록기") + " — 최근 기록";
                        res.LoreText = string.Join("\n", recs.Select(d => $"{ClockFmt.Vague(d.Clock, S.Clock)} · {S.RoomName(d.Room)} · {(d.Actor != null ? Cast.GivenOf(d.Actor) : "알 수 없음")} {(d.Entering ? "들어감" : "나감")}"));
                        res.Text = $"출입 기록 {recs.Count}건을 확인했다";
                        {
                            // the notebook keeps only what matters: during a case the entries inside the public window (at most 6),
                            // otherwise the last three hours — one card per device, updated when read again
                            List<DeviceRecord> keep; var all = S.DeviceLog.Where(d => f.Type == "Terminal" || d.Room == f.Room);
                            var inc = CaseProgress.Current(S);
                            if (inc != null && (S.Phase == Phase.Investigation || S.Phase == Phase.Assembly))
                            {
                                var known = CaseBoard.KnownWindow(this); double found = Testimony.CaseWindow(S).t1;
                                double w0 = Math.Min(found - 210, known.t0 - 60), w1 = inc.ConfirmClock + 10;
                                keep = all.Where(d => d.Clock >= w0 && d.Clock <= w1).OrderBy(d => d.Clock).TakeLast(6).ToList();
                            }
                            else keep = all.Where(d => S.Clock - d.Clock <= 180).OrderBy(d => d.Clock).TakeLast(10).ToList();
                            if (keep.Count > 0)
                                Evidences.File(this, Cast.Player, EvKind.Record, $"{S.RoomName(f.Room)} 출입 기록", string.Join("\n", keep.Select(d => $"{ClockFmt.Anchor(d.Clock, S.Clock)} {(d.Actor != null ? Cast.GivenOf(d.Actor) : "누군가")} {(d.Entering ? "들어감" : "나감")} — {S.RoomName(d.Room)}")), "기록 장치", "devlog:" + f.Id, keep.Min(d => d.Clock), keep.Max(d => d.Clock), f.Room,
                                    "기록된 시각에 그 문을 지나간 사람", "기록되지 않은 다른 문으로 다닌 사람", true, true, $"출입 기록 {keep.Count}건", false,
                                    keep.Where(d => d.Actor != null).Select(d => new Prop { Kind = PropKind.AtPlace, A = d.Actor, Room = d.Room, T0 = d.Clock, T1 = d.Clock }).ToArray());
                        }
                        break;
                    }
                default: res.Text = (def?.Kor ?? act.Label) + "(으)로 시간을 보냈다"; break;
            }
            // time spent together is how bonds grow; now and then it opens up something more personal
            if (mate != null && mate.Alive)
            {
                Relations.Change(S, mate.Id, Cast.Player, like: 0.05f, attach: 0.04f, trust: 0.02f, memory: "민혁과 함께한 " + (def?.Kor ?? act.Label) + " 시간이 즐거웠다");
                Relations.Change(S, Cast.Player, mate.Id, like: 0.04f, attach: 0.03f);
                string bondKey = "bond:" + mate.Id; int b0 = S.Flags.TryGetValue(bondKey, out var bv) ? (int)bv : 0; S.Flags[bondKey] = b0 + 1;
                var r = S.R(mate.Id, Cast.Player);
                string line = r.Trust > 0.5f && r.Like > 0.45f && (b0 + 1) % 3 == 0 ? Render(mate.Id, Cast.Player, "secret_share") : Render(mate.Id, Cast.Player, "small_talk");
                if (line != null) { res.JoinLine = line; S.Emit(GameEventType.Speech, mate.Id, Cast.Player, text: line, room: mate.Room, pos: mate.Pos, key: "bond"); }
                if (r.Trust > 0.5f && r.Like > 0.45f && (b0 + 1) % 3 == 0) k.Facts.Add("secret:" + mate.Id);
                res.Text += $". {Cast.GivenOf(mate.Id)}와(과) 함께했다";
                // the one who joined goes back to their own day
                if (mate.Act != null && mate.Act.Id == "social:join:" + Cast.Player && plan != null) Interrupt(mate, 0.5);
            }
            res.Text = LineBank.FixParticles(res.Text);
            return res;
        }
        // --- time-on-demand (end)

        Lore.Entry Pick(List<Lore.Entry> pool, Knowledge k, Rng rng) => PickWhere(pool, k, rng, _ => true);
        Lore.Entry PickWhere(List<Lore.Entry> pool, Knowledge k, Rng rng, Func<Lore.Entry, bool> pred)
        {
            var fresh = pool.Where(pred).Where(e => !k.Facts.Contains("lore:" + e.Id + ":" + S.Loop)).ToList();
            if (fresh.Count == 0) return null;
            var e0 = fresh[rng.R(fresh.Count)]; k.Facts.Add("lore:" + e0.Id + ":" + S.Loop); return e0;
        }

        Item MakeItem(string type, Actor holder)
        {
            var it = new Item { Id = S.NewId("it"), Type = type, Pos = holder.Pos, Room = holder.Room, Owner = holder.Id, HomeRoom = holder.Room, HomePos = holder.Pos };
            S.Items[it.Id] = it;
            PickUp(holder, it);
            S.Emit(GameEventType.ItemMoved, holder.Id, data: it.Id, text: "new");
            return it;
        }
    }
}
