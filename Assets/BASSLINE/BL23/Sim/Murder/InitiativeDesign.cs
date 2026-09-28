using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    // =====================================================================================================================
    // DESIGN — pressure meets opportunity. The culprit lists the moments THEY know of (their own evening they could host,
    // an evening they were invited to, the house's announced blackout, a meal, the victim's habit, the night lock, a
    // private meeting, the small hours, the investigation itself), the ways each could be used (send the victim on an errand,
    // slip out, strike in the dark, pour the cup, a rendezvous, an ambush, the old methods), scores every pair by
    // personality (style, skills, audacity vs caution, the motive's urgency) and by novelty (a returning player should not
    // meet the same shape twice), then fills in the frame, the alibi, the helpers and the preparation list.
    // =====================================================================================================================
    public static partial class Initiative
    {
        // ------------------------------------------------------------------ the evenings a culprit can host
        sealed class HostKind { public string Id, Label; public RoomType[] Rooms; public bool Dark, Serve; }
        static readonly HostKind[] HostKinds =
        {
            new HostKind { Id = "tea",        Label = "차 모임",          Rooms = new[] { RoomType.TeaRoom, RoomType.Lounge, RoomType.Greenhouse, RoomType.Parlor }, Serve = true },
            new HostKind { Id = "cards",      Label = "카드 게임의 밤",    Rooms = new[] { RoomType.GameRoom, RoomType.Lounge, RoomType.Parlor } },
            new HostKind { Id = "music",      Label = "작은 연주회",       Rooms = new[] { RoomType.MusicRoom, RoomType.Theater, RoomType.Parlor }, Dark = true },
            new HostKind { Id = "reading",    Label = "촛불 낭독회",       Rooms = new[] { RoomType.Library, RoomType.Study, RoomType.Chapel, RoomType.SecretStacks }, Dark = true },
            new HostKind { Id = "film",       Label = "상영회",           Rooms = new[] { RoomType.Theater, RoomType.Lounge }, Dark = true },
            new HostKind { Id = "show",       Label = "무대 발표회",       Rooms = new[] { RoomType.Theater, RoomType.MusicRoom }, Dark = true },
            new HostKind { Id = "rehearsal",  Label = "낭독극 리허설",     Rooms = new[] { RoomType.Theater, RoomType.MusicRoom, RoomType.Chapel }, Dark = true },
            new HostKind { Id = "cooking",    Label = "요리 모임",         Rooms = new[] { RoomType.Kitchen, RoomType.Dining }, Serve = true },
            new HostKind { Id = "wine",       Label = "와인 시음회",       Rooms = new[] { RoomType.WineCellar, RoomType.Dining, RoomType.Lounge, RoomType.Parlor }, Serve = true },
            new HostKind { Id = "birthday",   Label = "생일 축하 자리",    Rooms = new[] { RoomType.Dining, RoomType.Lounge, RoomType.TeaRoom }, Serve = true },
            new HostKind { Id = "inspection", Label = "지하 점검",         Rooms = new[] { RoomType.BoilerRoom, RoomType.WineCellar, RoomType.Storage, RoomType.MachineRoom, RoomType.WaterRoom }, Dark = true },
            new HostKind { Id = "memorial",   Label = "추모 모임",         Rooms = new[] { RoomType.Chapel, RoomType.Oracle, RoomType.Lounge } , Dark = true },
            new HostKind { Id = "photo",      Label = "단체 사진 촬영",    Rooms = new[] { RoomType.Gallery, RoomType.Lounge, RoomType.Greenhouse } },
        };
        public static string HostLabel(string id) { foreach (var h in HostKinds) if (h.Id == id) return h.Label; return "모임"; }

        sealed class MomentCand
        {
            public string Kind, Ref, Label, EventKind, DarkBy; public int Room = -1, HabitRoom = -1;
            public double At = -1, End = -1, Strike = -1; public bool Crowd, Dark, Serve, VictimIn, Hosted;
        }
        sealed class DesignCand { public MomentCand M; public string Approach, Method, Weapon, Poison; public int KillRoom = -1; public double Score; }

        static double Day0(double t) => Math.Floor(t / 1440) * 1440;
        static int MinuteOf(double t) { int m = (int)Math.Floor(t % 1440); return m < 0 ? m + 1440 : m; }
        static P3 Center(Room r) => new P3(r.Floor, r.Rect.CX, r.Rect.CZ);
        static bool Knows(GameState S, Actor a, Room r) => r != null && S.K(a.Id).Facts.Contains("visited:" + r.Id);
        static bool Plain(Room r) => r != null && !RoomInfo.IsPassage(r.Type) && r.Type != RoomType.Bedroom && r.Type != RoomType.Elevator && r.Type != RoomType.ButlerRoom && r.Type != RoomType.Courtroom && !r.Void;

        /// <summary>Tests only: every (moment, approach) a design weighed, with its score.</summary>
        public static Action<string> DesignTrace;

        // ================================================================== design
        static bool Design(Simulation sim, Scheme sc)
        {
            var S = sim.S; var a = S.A(sc.Culprit); var v = S.A(sc.Victim); if (a == null || v == null) return false;
            var st = SchemeStyles.Of(a.Def);
            var weapons = WeaponsKnown(sim, a);
            var moments = Moments(sim, sc, a, v, st);
            DesignCand best = null;
            foreach (var m in moments)
                foreach (var d in Approaches(sim, sc, a, v, st, m, weapons))
                {
                    d.Score = ScoreDesign(sim, sc, a, v, st, d);
                    DesignTrace?.Invoke($"{sc.Id} {a.Id}->{v.Id} {m.Kind}{(m.EventKind != null ? "(" + m.EventKind + ")" : "")} {d.Approach} {d.Score:0.00}{(m.VictimIn ? " vin" : "")}");
                    if (best == null || d.Score > best.Score + 1e-9) best = d;
                }
            if (best == null) { S.Dev($"scheme {sc.Id} no design ({moments.Count} moments, {weapons.Count} weapons)"); return false; }
            Apply(sim, sc, a, v, st, best);
            return true;
        }

        /// <summary>Weapons the culprit knows of and can reach (carried first; seen, still free, in rooms they may enter).</summary>
        static List<Item> WeaponsKnown(Simulation sim, Actor a)
        {
            var S = sim.S; var res = new List<Item>(); var seen = new HashSet<string>();
            foreach (var (id, _) in Crime.KnownWeapons(sim, a))
            {
                if (!seen.Add(id)) continue; var it = S.I(id);
                if (it == null || it.Def == null || (it.Holder != null && it.Holder != a.Id) || it.Surface.Contains("burnt")) continue;
                res.Add(it);
            }
            res.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));
            return res;
        }

        // ------------------------------------------------------------------ moments the culprit knows about
        static List<MomentCand> Moments(Simulation sim, Scheme sc, Actor a, Actor v, SchemeStyle st)
        {
            var S = sim.S; var list = new List<MomentCand>(); double now = S.Clock;
            if (S.Phase == Phase.Investigation)
            {
                if (S.Ch.InvestigationEnd - now > 22) list.Add(new MomentCand { Kind = "investigation", At = now + 1, End = S.Ch.InvestigationEnd - 6, Strike = now + 1, Label = "수사가 한창일 때" });
                return list;
            }
            // nothing is struck in a chapter's opening quiet: the moment is chosen after it, the preparation fills it
            double from = Math.Max(now, CalmEnd(S) + 5);
            // 1) an evening they host themselves
            if (!S.Gatherings.Any(g => !g.Done && !g.Cancelled && g.Host == a.Id))
                foreach (var hk in HostKinds)
                {
                    if (hk.Id == "memorial" && !S.Incidents.Values.Any(i => i.Loop == S.Loop && i.Confirmed)) continue;
                    var rooms = S.Layout.Rooms.Where(r => hk.Rooms.Contains(r.Type) && Knows(S, a, r) && sim.RoomUsable(a, r) && Plain(r)).OrderBy(r => r.Id).ToList();
                    if (rooms.Count == 0) continue;
                    var room = rooms[(int)(U(S, sc.Id + ":hr:" + hk.Id + ":" + sc.Redesigns) * rooms.Count) % rooms.Count];
                    double start = EventStart(S, sc, hk.Id, from); if (start < 0) continue;
                    double end = start + 70 + Math.Floor(U(S, sc.Id + ":hd:" + hk.Id) * 4) * 10;
                    list.Add(new MomentCand { Kind = "hosted", EventKind = hk.Id, Label = hk.Label, Room = room.Id, At = start, End = end, Strike = start - 5, Crowd = true, Dark = hk.Dark, Serve = hk.Serve, VictimIn = !v.IsPlayer && WouldCome(S, v, a), Hosted = true });
                }
            // 2) an evening they were invited to (someone else's, a festival, an exchange)
            foreach (var g in S.Gatherings.Where(g => !g.Done && !g.Cancelled && g.Host != a.Id && g.Kind != "house:hunt" && g.Kind != "house:phone").OrderBy(g => g.Id, StringComparer.Ordinal))   // the hunt's briefing and the telephone night's lounge are not the moment; the search and the call are (2b, 2c)
            {
                if (!g.Status.TryGetValue(a.Id, out var s0) || !(s0 == "accepted" || s0 == "attended" || s0 == "invited")) continue;
                var R = g.Cur; if (R.Start < from - 5 || R.Start > now + (HouseEvents.IsHouse(g) ? 720 : 360)) continue;   // the house announces its evening in the morning
                bool vin = g.Status.TryGetValue(v.Id, out var vs) && (vs == "accepted" || vs == "attended" || vs == "host");
                bool dark = S.RuleActive("CH03") && S.Rule("CH03").Times.Any(t => t > R.Start + 10 && t < R.End - 10) || HouseEvents.Dark(S, g);   // the house's toast, a dark evening
                var hk = HouseEvents.KindOf(g);
                list.Add(new MomentCand { Kind = "joined", Ref = g.Id, Label = g.Label, EventKind = hk != null ? "house:" + hk.Id : null, Room = R.Room, At = R.Start, End = hk != null ? R.Start + hk.Len : R.End, Strike = Math.Max(now + 2, R.Start - 5), Crowd = true, VictimIn = vin, Dark = dark || hk?.Dark != null, DarkBy = dark || hk?.Dark != null ? "house" : null, Serve = hk != null && (hk.Id == "banquet" || hk.Serve) });
            }
            // 2b) the house's treasure hunt: where the victim searches, alone, is public (the chart read out in the morning)
            if (HouseEvents.HuntZone(S, v.Id, out var hz, out var hAt, out var hEnd) && hAt > from - 5 && hEnd - 20 > now)
                list.Add(new MomentCand { Kind = "hunt", Room = hz, HabitRoom = hz, At = Math.Max(hAt, now + 2), End = hEnd, Strike = Math.Max(hAt + 5, now + 2), Label = "보물찾기 — " + S.RoomName(hz) });
            // 2c) the house's telephone night: the victim's call — alone in the telephone room, at an hour everyone heard
            if (HouseEvents.NextCall(S, v.Id, out var pr, out var cAt, out var cg) && cAt > from - 5 && cAt - 4 > now)
                list.Add(new MomentCand { Kind = "call", Ref = cg.Id, Room = pr, HabitRoom = pr, At = cAt, End = cAt + HouseEvents.CallLen, Strike = Math.Max(cAt - 6, now + 2), Label = "전화의 밤 — " + Cast.GivenOf(v.Id) + "의 통화 차례" });
            // 3) the house's own darkness (CH03: announced times; CH23: the long dark)
            if (S.RuleActive("CH03"))
                foreach (var t in S.Rule("CH03").Times.OrderBy(x => x)) if (t > from + 40 && t < now + 1440)
                    list.Add(new MomentCand { Kind = "house-dark", At = t, End = t + 24, Strike = t - 30, Dark = true, DarkBy = "house", Label = "저택의 정기 소등" });
            if (S.RuleActive("CH23"))
            { double t = SafeTime(S, from + 60 + Math.Floor(U(S, sc.Id + ":ld" + sc.Redesigns) * 9) * 10); list.Add(new MomentCand { Kind = "long-dark", At = t, End = t + 150, Strike = t, Dark = true, DarkBy = "house", Label = "긴 암흑" }); }
            // 4) a meal: everyone at one table, a victim who can be sent away from it
            var dining = S.Layout.First(RoomType.Dining);
            if (dining != null && sim.RoomUsable(a, dining))
            {
                double meal = NextMeal(Math.Max(now + 70, from));
                list.Add(new MomentCand { Kind = "meal", Room = dining.Id, At = meal + 6, End = meal + 48, Strike = meal + 6, Crowd = true, VictimIn = !v.IsPlayer, Label = MinuteOf(meal) < 10 * 60 ? "아침 식사 자리" : "저녁 식사 자리" });
            }
            // 5) the victim's habit (only what the culprit has seen: two or more sightings in one room)
            var habit = HabitOf(S, a, v);
            if (habit.room >= 0)
            {
                var (at, end) = NextBlock(Math.Max(now + 50, from), habit.block);
                list.Add(new MomentCand { Kind = S.RuleActive("CH22") ? "noise" : "habit", HabitRoom = habit.room, Room = habit.room, At = at, End = end, Strike = at, Label = S.RoomName(habit.room) + "에 늘 머무는 시간" });
            }
            // 6) just before the night lock (the house locks an empty night room for them); the appointment is made in the daytime
            {
                double at = Day0(from) + 21 * 60 + 10; if (Math.Max(now + 90, from) > at - 20) at += 1440;
                foreach (var r in S.Layout.Rooms.Where(r => RoomInfo.NightLocked(r.Type) && Knows(S, a, r) && Plain(r)).OrderBy(r => r.Id).Take(2))
                    list.Add(new MomentCand { Kind = "night-lock", Room = r.Id, At = at, End = at + 44, Strike = at - 12, Label = "밤 10시 잠금 직전의 " + r.Name });
            }
            // 7) a private meeting at a secluded place (arranged hours ahead)
            {
                double at = SafeTime(S, Math.Max(now + 120, from + 30) + Math.Floor(U(S, sc.Id + ":rv" + sc.Redesigns) * 16) * 10);
                list.Add(new MomentCand { Kind = "rendezvous", At = at, End = at + 50, Strike = at - 12, Label = "단둘이 만나자는 약속" });
            }
            // 8) the small hours
            if (!v.IsPlayer)
            { double at = Day0(from) + 1440 + 90; if (from < Day0(from) + 60) at -= 1440; list.Add(new MomentCand { Kind = "night-visit", At = at, End = at + 180, Strike = at - 50, Label = "모두 잠든 새벽" }); }
            return list;
        }

        static bool WouldCome(GameState S, Actor v, Actor host)
        {
            var r = S.R(v.Id, host.Id);
            double score = r.Opinion * 1.4 + v.Def.P.Sociability * 0.5 - v.Needs.Fear * 0.8 - v.Needs.Stress * 0.3 + (S.Chapter > 1 ? -0.1 : 0);
            return score > 0.15;
        }

        /// <summary>A start time for the culprit's own event: a few hours ahead, in the daytime or early evening, off the meal hours.</summary>
        static double EventStart(GameState S, Scheme sc, string kind, double from)
        {
            double lead = 160 + Math.Floor(U(S, sc.Id + ":lead:" + kind + sc.Redesigns) * 12) * 10;   // time to invite, fetch, stash, arrange
            double t = Math.Ceiling(Math.Max(S.Clock + lead, from + 20) / 10) * 10;
            int m = MinuteOf(t);
            if (m < 10 * 60) t = Day0(t) + 10 * 60 + Math.Floor(U(S, sc.Id + ":am:" + kind) * 10) * 10;
            else if (m > 20 * 60 + 40) t = Day0(t) + 1440 + 14 * 60 + Math.Floor(U(S, sc.Id + ":pm:" + kind) * 30) * 10;
            return SafeTime(S, t);
        }
        /// <summary>Move a time off the meal hours and out of the night.</summary>
        static double SafeTime(GameState S, double t)
        {
            int m = MinuteOf(t); double d0 = Day0(t);
            if (m >= 12 * 60 + 10 && m < 13 * 60 + 50) t = d0 + 14 * 60;
            else if (m >= 18 * 60 + 10 && m < 19 * 60 + 50) t = d0 + 20 * 60;
            else if (m >= 7 * 60 + 30 && m < 9 * 60 + 20) t = d0 + 9 * 60 + 30;
            else if (m >= 21 * 60 + 30) t = d0 + 1440 + 10 * 60;
            else if (m < 7 * 60) t = d0 + 10 * 60;
            return t;
        }
        static double NextMeal(double after)
        {
            double d0 = Day0(after);
            foreach (var cand in new[] { d0 + 8 * 60, d0 + 18 * 60 + 30, d0 + 1440 + 8 * 60, d0 + 1440 + 18 * 60 + 30 }) if (cand >= after) return cand;
            return d0 + 1440 + 8 * 60;
        }
        static (double at, double end) NextBlock(double after, int block)
        {
            double d0 = Day0(after);
            (double s, double e) W(int b) => b == 0 ? (9 * 60 + 30, 11 * 60 + 50) : b == 1 ? (14 * 60, 17 * 60 + 50) : (19 * 60 + 50, 21 * 60 + 40);
            var (bs, be) = W(block);
            for (int day = 0; day < 3; day++)
            {
                double s = d0 + day * 1440 + bs, e = d0 + day * 1440 + be;
                if (e - 30 > after) return (Math.Max(s, after), e);
            }
            return (d0 + 1440 + bs, d0 + 1440 + be);
        }
        /// <summary>Where and when the culprit has seen the victim more than once (their own sightings — the culprit's knowledge).</summary>
        static (int room, int block, int count) HabitOf(GameState S, Actor a, Actor v)
        {
            var k = S.K(a.Id); var by = new Dictionary<int, int>(); var blocks = new Dictionary<int, int[]>();
            foreach (var s in k.Sightings)
            {
                if (s.Target != v.Id || s.IdConf < 0.5f || s.Dead) continue;
                var r = S.Layout.Room(s.Room); if (!Plain(r)) continue;
                by[s.Room] = (by.TryGetValue(s.Room, out var n) ? n : 0) + 1;
                if (!blocks.TryGetValue(s.Room, out var bl)) blocks[s.Room] = bl = new int[3];
                int m = MinuteOf(s.T0); bl[m < 12 * 60 ? 0 : m < 18 * 60 ? 1 : 2]++;
            }
            int best = -1, bc = 0;
            foreach (var kv in by.OrderBy(x => x.Key)) if (kv.Value >= 2 && kv.Value > bc) { bc = kv.Value; best = kv.Key; }
            if (best < 0) return (-1, 0, 0);
            var b3 = blocks[best]; int blk = b3[2] >= b3[1] && b3[2] >= b3[0] ? 2 : b3[1] >= b3[0] ? 1 : 0;
            return (best, blk, bc);
        }

        // ------------------------------------------------------------------ the ways a moment can be used
        static IEnumerable<DesignCand> Approaches(Simulation sim, Scheme sc, Actor a, Actor v, SchemeStyle st, MomentCand m, List<Item> weapons)
        {
            var S = sim.S; var res = new List<DesignCand>(); bool pv = v.IsPlayer;
            void Add(string ap, string weapon = null, int kill = -1, string method = null, string poison = null)
            { if (st.Refuses(ap)) return; res.Add(new DesignCand { M = m, Approach = ap, Weapon = weapon, KillRoom = kill, Method = method, Poison = poison }); }
            string w = BestWeapon(sim, sc, a, weapons, "any"), wq = BestWeapon(sim, sc, a, weapons, "quiet"), wc = BestWeapon(sim, sc, a, weapons, "conceal");
            var poison = SetPieces.PoisonSource(sim, a);
            float trust = S.R(v.Id, a.Id).Trust + S.R(v.Id, a.Id).Like;
            switch (m.Kind)
            {
                case "hosted":
                    if (m.VictimIn)
                    {
                        if (w != null) { int kr = ErrandRoom(sim, sc, a, m.Room); if (kr >= 0) Add("errand", w, kr); }
                        if (m.Dark && wq != null && m.EventKind != "birthday" && S.Layout.Room(m.Room)?.Circuit > 0) Add("dark-strike", wq);
                        if (m.Serve && poison != null) Add("serve", poison: poison.Id);
                    }
                    else if (wc != null) { var hb0 = HabitOf(S, a, v); if (hb0.room >= 0 && ErrandPref(S.Layout.Room(hb0.room).Type) >= 0.6) Add("slip-out", wc, hb0.room); }   // they will be alone where they always are
                    break;
                case "joined":
                    if (m.VictimIn)
                    {
                        if (w != null && !pv) { int kr = ErrandRoom(sim, sc, a, m.Room); if (kr >= 0) Add("errand", w, kr); }
                        if (m.Dark && wq != null) Add("dark-strike", wq);
                        if (m.Serve && poison != null && !pv) Add("serve", poison: poison.Id);   // the house's banquet: a glass raised for the toast
                    }
                    else if (wc != null) { var hb = HabitOf(S, a, v); if (hb.room >= 0 && ErrandPref(S.Layout.Room(hb.room).Type) >= 0.6) Add("slip-out", wc, hb.room); }
                    break;
                case "hunt": case "call":
                    if (w != null) Add("ambush", w, m.HabitRoom);
                    break;
                case "house-dark": case "long-dark":
                    if (wq != null) Add("dark-strike", wq);
                    if (w != null) Add("ambush", w);
                    break;
                case "meal":
                    if (!pv && w != null) { int kr = ErrandRoom(sim, sc, a, m.Room); if (kr >= 0) Add("errand", w, kr); }
                    if (!pv && poison != null) Add("serve", poison: poison.Id);
                    break;
                case "habit": case "noise":
                    if (w != null) Add("ambush", w, m.HabitRoom);
                    foreach (var (g, s) in Methods.Options(sim, a, v, trust, Local(S, sc.Id + ":mo")))
                        if (s > 0 && s < 5) Add("method:" + g, method: g);
                    if (!pv && Tricks.ChooseTrap(sim, a, v.Id) != null) Add("method:Trap", method: "Trap");
                    break;
                case "night-lock":
                    if (!pv && w != null && trust > 0.05f) Add("rendezvous", w, m.Room);
                    break;
                case "rendezvous":
                    if (!pv && w != null && trust > -0.05f) { int kr = SecludedKnown(sim, sc, a); if (kr >= 0) Add("rendezvous", w, kr); }
                    { var pool = S.Layout.First(RoomType.Pool); if (!pv && pool != null && Knows(S, a, pool) && trust > 0.05f && a.Def.HeightCm + 8 >= v.Def.HeightCm) Add("method:Drown", method: "Drown", kill: pool.Id); }
                    break;
                case "night-visit":
                    if (w != null) Add("visit", w);
                    if (Methods.Sedative(sim, a) != null && !pv) Add("method:Smother", method: "Smother");
                    break;
                case "investigation":
                    if (w != null && !pv) { int kr = ErrandRoom(sim, sc, a, v.Room >= 0 ? v.Room : a.Room); if (kr >= 0) Add("errand", w, kr); }
                    if (w != null) Add("ambush", w);
                    break;
            }
            return res;
        }

        static readonly Dictionary<string, string[]> SkillWeapons = new Dictionary<string, string[]>
        {
            ["Cooking"] = new[] { "KitchenKnife", "Cleaver", "RollingPin", "FryingPan", "IcePick" }, ["Strength"] = new[] { "Crowbar", "PipeSection", "Hammer", "Wrench" },
            ["Bodies"] = new[] { "Scalpel", "SkinningKnife" }, ["Outfits"] = new[] { "Scissors", "Scarf", "CurtainCord" }, ["Craft"] = new[] { "Chisel", "PaletteKnife", "Hammer" },
            ["Theatre"] = new[] { "CurtainCord", "Candlestick", "LetterOpener" }, ["Sound"] = new[] { "PianoWire", "ExtensionCord" }, ["Tech"] = new[] { "Wrench", "Hammer", "ExtensionCord" },
            ["Electric"] = new[] { "ExtensionCord", "Wrench" }, ["Records"] = new[] { "LetterOpener", "Bookend" }, ["Tools"] = new[] { "Crowbar", "Hammer", "Wrench", "PipeSection" },
            ["Cold"] = new[] { "IcePick" }, ["Chem"] = new[] { "Scalpel" }, ["Social"] = new[] { "Decanter", "Candlestick", "LetterOpener" },
        };
        static double WeaponAffinity(SchemeStyle st, string type)
        {
            double s = 0; foreach (var sk in st.Skills) if (SkillWeapons.TryGetValue(sk, out var list) && Array.IndexOf(list, type) >= 0) s += 0.6;
            return s;
        }

        /// <summary>The weapon for an approach: "quiet" = a blade or a cord that goes under the clothes (a strike in a crowd);
        /// "conceal" = anything that fits under the clothes (walking out of an evening with it).</summary>
        static string BestWeapon(Simulation sim, Scheme sc, Actor a, List<Item> weapons, string need)
        {
            var S = sim.S; var st = SchemeStyles.Of(a.Def); string best = null; double bs = double.MinValue;
            Incident copy = sc.CopyOf != null && S.Incidents.TryGetValue(sc.CopyOf, out var ci) ? ci : null;
            foreach (var it in weapons)
            {
                var d = it.Def; if (d == null || !d.IsWeapon) continue;
                bool onBody = Concealment.BodyClass(d) != BodySlot.None;
                if (need == "quiet" && (!onBody || !(d.Dmg == DamageType.Stab || d.Dmg == DamageType.Cut || d.Dmg == DamageType.Choke))) continue;
                if (need == "conceal" && !onBody) continue;
                if (Methods.IsCord(d) && a.Def.P.Aggression >= 0.62f && need != "quiet") continue;
                double dist = it.Holder == a.Id ? 0 : a.Pos.Dist(it.Pos);
                double s = d.Sev * 1.1 - dist / 30.0 + (onBody ? 0.5 : 0) + WeaponAffinity(st, it.Type) + U(S, sc.Id + ":w:" + it.Id + ":" + sc.Redesigns) * 1.1;
                if (copy != null) s += copy.WeaponType == it.Type ? 2.5 : copy.Dmg == d.Dmg ? 1.2 : 0;
                if (sc.Has("turnabout") && it.Owner == sc.Victim) s += 2;
                if (s > bs) { bs = s; best = it.Id; }
            }
            return best;
        }

        /// <summary>Where to send the victim on an errand from a room: a quiet room the culprit knows, close by.</summary>
        static int ErrandRoom(Simulation sim, Scheme sc, Actor a, int fromRoom)
        {
            var S = sim.S; var from = S.Layout.Room(fromRoom); if (from == null) return -1; var k = S.K(a.Id);
            int best = -1; double bs = double.MinValue;
            foreach (var r in S.Layout.Rooms)
            {
                if (r.Id == fromRoom || !Plain(r) || !Knows(S, a, r) || !sim.RoomUsable(a, r)) continue;
                double pref = ErrandPref(r.Type); if (pref <= 0) continue;
                if (r.Floor != from.Floor) continue;   // an errand is down the corridor, not two flights away
                double dist = Center(from).DistXZ(Center(r)); if (dist > 36) continue;
                int people = 0; foreach (var s in k.Sightings) if (s.Room == r.Id && S.Clock - s.T1 < 600) people++;
                double s1 = pref - dist / 35.0 + 1.0 / (1 + people * 0.4) + U(S, sc.Id + ":er:" + r.Id + ":" + sc.Redesigns) * 0.6;
                if (s1 > bs) { bs = s1; best = r.Id; }
            }
            return best;
        }
        static double ErrandPref(RoomType t)
        {
            switch (t)
            {
                case RoomType.WineCellar: case RoomType.Storage: case RoomType.Closet: case RoomType.ColdStorage: case RoomType.Archive: case RoomType.Darkroom:
                case RoomType.Wardrobe: case RoomType.Laundry: case RoomType.BoilerRoom: case RoomType.Greenhouse: case RoomType.Chapel: case RoomType.Study: case RoomType.TrophyRoom: return 1.2;
                case RoomType.Library: case RoomType.Kitchen: case RoomType.Workshop: case RoomType.MusicRoom: case RoomType.Gallery: case RoomType.WaterRoom: case RoomType.MachineRoom: case RoomType.DollRoom: return 0.6;
                case RoomType.Lounge: case RoomType.Dining: case RoomType.TeaRoom: case RoomType.GameRoom: case RoomType.Infirmary: return 0;
            }
            return RoomInfo.IsMystery(t) ? 1.0 : 0.3;
        }
        static string PretextFor(RoomType t)
        {
            switch (t)
            {
                case RoomType.WineCellar: return "와인 한 병"; case RoomType.Storage: return "여분 의자"; case RoomType.Closet: return "담요";
                case RoomType.ColdStorage: return "얼음"; case RoomType.Archive: case RoomType.Library: case RoomType.Study: return "두고 온 책";
                case RoomType.Darkroom: return "사진 필름"; case RoomType.Wardrobe: return "무대 의상"; case RoomType.Laundry: return "수건";
                case RoomType.BoilerRoom: case RoomType.MachineRoom: case RoomType.WaterRoom: case RoomType.Workshop: return "공구 상자";
                case RoomType.Greenhouse: return "꽃 몇 송이"; case RoomType.Chapel: return "초 한 묶음"; case RoomType.Kitchen: return "찻잎 통";
                case RoomType.MusicRoom: return "악보 묶음"; case RoomType.Gallery: return "작은 액자"; case RoomType.TrophyRoom: return "트로피 받침";
            }
            return "등불";
        }
        static int SecludedKnown(Simulation sim, Scheme sc, Actor a)
        {
            var S = sim.S; int best = -1; double bs = double.MinValue;
            foreach (var r in S.Layout.Rooms)
            {
                if (!Plain(r) || !Knows(S, a, r) || !sim.RoomUsable(a, r) || RoomInfo.NightLocked(r.Type)) continue;
                double pref = ErrandPref(r.Type); if (pref <= 0) continue;
                double s1 = pref + (r.Floor == -1 || RoomInfo.IsMystery(r.Type) ? 0.4 : 0) + U(S, sc.Id + ":sq:" + r.Id + ":" + sc.Redesigns) * 0.9;
                if (s1 > bs) { bs = s1; best = r.Id; }
            }
            return best;
        }

        // ------------------------------------------------------------------ scoring: who this person is, and what the player has already seen
        static double ScoreDesign(Simulation sim, Scheme sc, Actor a, Actor v, SchemeStyle st, DesignCand d)
        {
            var S = sim.S; var c = a.Def; var m = d.M; string ap = d.Approach; bool method = ap.StartsWith("method:");
            string apk = method ? "method" : ap;
            double s = apk == "errand" ? 0.9 : apk == "slip-out" ? 0.75 : apk == "dark-strike" ? 0.85 : apk == "serve" ? 0.8 : apk == "rendezvous" ? 0.7 : apk == "ambush" ? 0.6 : apk == "visit" ? 0.45 : 0.72;
            s += m.Kind == "hosted" ? 0.3 : m.Kind == "joined" ? 0.2 : m.Kind == "house-dark" ? 0.45 : m.Kind == "long-dark" ? 0.35 : m.Kind == "noise" ? 0.25 : m.Kind == "meal" ? 0.2
               : m.Kind == "habit" ? 0.1 : m.Kind == "night-lock" ? 0.25 : m.Kind == "rendezvous" ? 0.05 : m.Kind == "investigation" ? 0.35 : 0;
            // the house's own evenings are stages it set for exactly this (SocialEventsDesign §4): a chart that says who is alone
            // where, masks that turn every witness's "I saw X" into "I saw a mask", a toast in the dark, a whole evening unlit
            if (m.Kind == "hunt") s += 0.75 + (st.Style == "Practical" || st.Style == "Impulsive" ? 0.15 : 0) + (st.Style == "Meticulous" ? 0.1 : 0);   // alone, for an hour and a half, where everyone was told
            if (m.Kind == "call") s += 0.7 + (st.Style == "Meticulous" ? 0.2 : 0) + (st.Skill("Timing") || st.Skill("Schedules") ? 0.2 : 0);   // eight minutes, to the minute, read out in the morning
            if (m.EventKind != null && m.EventKind.StartsWith("house:", StringComparison.Ordinal))
            {
                s += 0.45;
                if (m.EventKind == "house:masque" && (ap == "slip-out" || ap == "errand")) s += 0.35 + (st.Style == "Theatrical" ? 0.2 : 0);
                if (m.EventKind == "house:banquet" && (ap == "dark-strike" || ap == "serve")) s += 0.25;
                if ((m.EventKind == "house:stars" || m.EventKind == "house:vigil") && ap == "dark-strike") s += 0.25;
            }
            // style
            switch (st.Style)
            {
                case "Theatrical": if (m.Hosted) s += 0.35; if (ap == "dark-strike") s += 0.3; if (ap == "errand") s += 0.15; if (m.Kind == "house-dark") s += 0.2; break;
                case "Meticulous": if (ap == "serve") s += 0.35; if (m.Kind == "habit") s += 0.15; if (m.Kind == "night-lock") s += 0.25; if (ap == "rendezvous") s += 0.15; if (method) s += 0.2; if (m.Hosted) s += 0.1; break;
                case "Practical": if (ap == "errand") s += 0.25; if (m.Kind == "meal") s += 0.2; if (ap == "ambush") s += 0.15; if (ap == "slip-out") s += 0.15; break;
                case "Impulsive": if (ap == "dark-strike") s += 0.3; if (ap == "ambush") s += 0.25; if (m.Kind == "investigation") s += 0.25; if (ap == "slip-out") s += 0.15; if (m.Hosted) s -= 0.2; if (ap == "serve") s -= 0.3; break;
                case "Technical": if (m.Kind == "house-dark") s += 0.3; if (ap == "dark-strike") s += 0.2; if (ap == "method:Shock") s += 0.4; if (m.Kind == "noise") s += 0.2; break;
            }
            if (st.Leans(ap) || (method && st.Leans(ap))) s += 0.35;
            if (m.Hosted && st.Hosting(m.EventKind)) s += 0.35;
            if (st.Skill("Cooking") && (ap == "serve" || m.EventKind == "cooking")) s += 0.3;
            if (st.Skill("Electric") && (m.Dark || m.Kind == "house-dark")) s += 0.25;
            if (st.Skill("Swim") && ap == "method:Drown") s += 0.3;
            if (st.Skill("Theatre") && (ap == "dark-strike" || m.EventKind == "show" || m.EventKind == "rehearsal")) s += 0.2;
            if ((st.Skill("Social") || st.Skill("Charm")) && m.Hosted) s += 0.2;
            if ((st.Skill("Records") || st.Skill("Writing")) && ap == "rendezvous") s += 0.2;
            if (st.Skill("Keys") && m.Kind == "night-lock") s += 0.3;
            if ((st.Skill("Bodies") || st.Skill("Cold") || st.Skill("Chem")) && (ap == "serve" || ap == "method:Smother")) s += 0.15;
            if (st.Skill("Strength") && (ap == "errand" || ap == "ambush")) s += 0.15;
            if (st.Skill("Sound") && (m.Kind == "noise" || m.EventKind == "music")) s += 0.2;
            if (st.Skill("Photo") && (m.EventKind == "film" || m.EventKind == "photo")) s += 0.2;
            if (st.Skill("Timing") && (ap == "slip-out" || ap == "ambush")) s += 0.15;
            if (ap == "method:Shoot") { if (st.Style == "Impulsive" || st.Style == "Technical") s += 0.25; if (m.Kind == "noise") s += 0.45; if (st.Style == "Meticulous") s -= 0.2; }   // a shot is heard house-wide unless the house is loud
            // audacity vs caution
            double bold = Math.Max(0, Math.Min(1.2, (1 - c.P.Fearfulness) * 0.45 + c.P.Pride * 0.25 + c.P.Aggression * 0.2 + st.Bold + (st.Style == "Theatrical" ? 0.15 : 0)));
            double aud = (m.Hosted ? 0.35 : 0) + (ap == "dark-strike" ? 0.45 : 0) + (m.Kind == "investigation" ? 0.45 : 0) + (m.Kind == "house-dark" ? 0.3 : 0) + (m.Kind == "meal" && ap == "errand" ? 0.2 : 0) + (m.Kind == "joined" ? 0.15 : 0);
            double caution = (c.Infer + c.Composure) / 200.0;
            double safe = ap == "serve" ? 0.3 : m.Kind == "night-lock" ? 0.3 : ap == "rendezvous" ? 0.2 : method ? 0.25 : m.Kind == "habit" ? 0.15 : ap == "errand" ? 0.1 : ap == "slip-out" ? 0.1 : ap == "visit" ? 0.1 : 0.05;
            s += bold * aud + caution * safe;
            // the motive's urgency and shape
            switch (sc.Motive)
            {
                case "silence": if (m.Kind == "investigation") s += 0.6; if (ap == "ambush") s += 0.2; if (m.Hosted) s -= 0.3; break;
                case "avenge": if (m.Kind == "investigation") s += 0.4; if (ap == "ambush") s += 0.2; if (m.Hosted) s -= 0.3; break;
                case "defense": if (ap == "rendezvous" || m.Kind == "joined") s += 0.25; break;
                case "protect": if (ap == "serve" || ap == "rendezvous") s += 0.2; break;
                case "love": if (m.Hosted) s += 0.2; break;
                case "secret": if (ap == "rendezvous") s += 0.2; if (m.Kind == "night-lock") s += 0.15; break;
                case "copycat":
                    { if (m.Kind == "investigation") s += 0.3; var ci = sc.CopyOf != null && S.Incidents.TryGetValue(sc.CopyOf, out var x) ? x : null; if (ci != null && ci.Method != null && method && ci.Method.StartsWith(d.Method ?? "#")) s += 0.5; break; }
            }
            if (sc.Has("turnabout")) { var vs = OpenOf(S, v.Id); if (vs != null && vs.Victim == a.Id && m.Kind == "joined" && m.Ref == vs.EventId) s += 0.6; }
            // novelty: the returning player — the same shape again is the worst sin
            var nov = W(S).Novelty; string shape = m.Kind + "/" + apk;
            s -= 0.4 * Math.Min(4, nov.Count("shape2:" + shape));
            int sameMoment = 0, sameAp = 0;
            foreach (var x in W(S).Schemes) if (x.Loop == S.Loop && x != sc && x.Struck >= 0) { if (x.Moment == m.Kind) sameMoment++; if (x.Approach == ap) sameAp++; }
            s -= 0.3 * sameMoment + 0.2 * sameAp;
            // a moment too close to prepare for loses a little; one that is far away loses a little
            double lead = m.Strike - S.Clock; if (lead > 18 * 60) s -= 0.25; if (lead < 30 && m.Kind != "investigation" && m.Kind != "joined") s -= 0.2;
            s += U(S, sc.Id + ":j:" + m.Kind + ":" + ap + ":" + (m.EventKind ?? m.Ref ?? "") + ":" + sc.Redesigns) * 0.6;
            return s;
        }

        // ------------------------------------------------------------------ fill in the design
        static void Apply(Simulation sim, Scheme sc, Actor a, Actor v, SchemeStyle st, DesignCand d)
        {
            var S = sim.S; var m = d.M;
            sc.Moment = m.Kind; sc.MomentRef = m.Ref; sc.MomentAt = m.At; sc.MomentEnd = m.End; sc.StrikeAt = m.Strike; sc.MomentRoom = m.Room; sc.MomentText = m.Label;
            sc.Approach = d.Approach; sc.Weapon = d.Weapon; sc.WeaponType = S.I(d.Weapon)?.Type; sc.Poison = d.Poison;
            sc.KillRoom = d.KillRoom >= 0 ? d.KillRoom : m.HabitRoom >= 0 ? m.HabitRoom : (d.Approach == "dark-strike" ? m.Room : -1);
            sc.Head = d.Approach == "errand" ? "Errand" : d.Approach == "slip-out" ? "Gathering" : d.Approach == "dark-strike" ? "DarkStrike" : d.Approach == "serve" ? "Poison"
                    : d.Approach == "rendezvous" ? "Rendezvous" : d.Approach == "ambush" ? "Ambush" : d.Approach == "visit" ? "NightVisit" : d.Method;
            if (m.Hosted) { sc.EventKind = m.EventKind; sc.EventLabel = m.Label; sc.EventRoom = m.Room; sc.Var("hosted"); }
            if (m.Kind == "joined") { sc.EventId = m.Ref; sc.EventLabel = m.Label; sc.EventRoom = m.Room; if (m.EventKind != null) sc.EventKind = m.EventKind; }
            if (m.Kind == "investigation") sc.Var("during-investigation");
            if (d.Approach == "dark-strike") { sc.DarkBy = m.DarkBy ?? (m.Hosted ? "helper" : "self"); if (m.Crowd || m.Kind == "house-dark") sc.Var("group-moment"); }
            if (d.Approach == "errand") sc.Pretext = PretextFor(S.Layout.Room(sc.KillRoom)?.Type ?? RoomType.Storage);
            if (m.Kind == "house-dark" && d.Approach == "dark-strike") sc.KillRoom = -1;   // wherever the victim is when the lights go
            ChooseFrame(sim, sc, a, v, st);
            ChooseAlibi(sim, sc, a, v, st);
            ChooseGarbAndMark(sim, sc, a, v, st);
            ChooseShields(sim, sc, a, v);
            sc.LieBudget = 2 + (a.Def.Deceit >= 60 ? 1 : 0) + (a.Def.Deceit >= 80 ? 1 : 0) + (a.Def.Composure >= 80 ? 1 : 0) + (a.Def.Argue >= 85 ? 1 : 0);
            string apk = d.Approach.StartsWith("method:") ? "method" : d.Approach;
            sc.Shape = $"{sc.Moment}/{apk}/{sc.Frame ?? "-"}/{sc.Alibi ?? "-"}";
            W(S).Novelty.Add("shape2:" + sc.Moment + "/" + apk);
            if (m.Hosted) CreateEvent(sim, sc, a, v, m);
            BuildPrep(sim, sc, a, v, st);
            sc.Log.Add(K($"{ClockFmt.DayHM(S.Clock)} 설계: {MomentText(sc)} — {ApproachText(sc)}" + (sc.Scapegoat != null ? $", 의심은 {Name(sc.Scapegoat)}에게" : "") + "."));
            S.Log("SchemeDesign", a.Id, v.Id, data: $"{sc.Id} {sc.Shape} strike@{ClockFmt.HM(sc.StrikeAt)} prep={sc.Prep.Count}{(sc.EventKind != null && sc.EventKind.StartsWith("house:") ? " ev=" + sc.EventKind.Substring(6) : sc.Moment == "hunt" || sc.Moment == "call" ? " ev=" + sc.Moment : "")}", secret: true);
            S.Dev($"SCHEME-DESIGN {a.Id}->{v.Id} {sc.Shape} at {ClockFmt.DayHM(sc.StrikeAt)} prep={string.Join(",", sc.Prep.Select(p => p.Kind))}");
        }

        // ------------------------------------------------------------------ the frame: who should take the blame, and how
        static void ChooseFrame(Simulation sim, Scheme sc, Actor a, Actor v, SchemeStyle st)
        {
            var S = sim.S; var c = a.Def;
            double want = 0.25 + c.Deceit / 160.0 + (st.Style == "Theatrical" || st.Style == "Meticulous" ? 0.1 : 0) - (sc.Moment == "investigation" ? 0.25 : 0);
            if (U(S, sc.Id + ":fr" + sc.Redesigns) >= want) return;
            string best = null; double bs = double.MinValue;
            foreach (var x in S.Living.OrderBy(q => q.Id, StringComparer.Ordinal))
            {
                if (x.Id == a.Id || x.Id == v.Id) continue;
                if (S.HasRel(a.Id, x.Id) && (S.R(a.Id, x.Id).Attach > 0.45f || S.R(a.Id, x.Id).Tags.Contains("family") || S.R(a.Id, x.Id).Tags.Contains("lover"))) continue;
                double s = 0.2;
                if (S.HasRel(a.Id, x.Id)) { var r = S.R(a.Id, x.Id); s += r.Grudge * 1.0 + Math.Max(0, -r.Like) * 0.8 + r.Jealous * 0.4 - r.Like * 0.3; }
                if (S.HasRel(x.Id, v.Id)) { var r = S.R(x.Id, v.Id); s += r.Grudge * 0.9 + r.Jealous * 0.4 + (r.Tags.Contains("feud") || r.Tags.Contains("grudge") || r.Tags.Contains("enemy") ? 0.5 : 0); }   // a visible motive
                if (x.IsPlayer) s += 0.25;   // "계약 없는 놈이 제일 수상하지"
                s += U(S, sc.Id + ":sg:" + x.Id) * 0.5;
                if (s > bs) { bs = s; best = x.Id; }
            }
            if (best == null) return;
            sc.Scapegoat = best; sc.Var("framed");
            // how: their token at the scene, their weapon, sent near the scene at the hour, a rumour, a note in their name
            var kinds = new List<(string k, double w)>();
            var token = S.Items.Values.Where(i => i.Owner == best && i.KeyFor == null && i.Holder == null && !i.Hidden && i.Def != null && !i.Def.IsWeapon && i.Type != "Envelope" && i.Type != "Invitation"
                                              && S.K(a.Id).ItemSeen.ContainsKey(i.Id) && sim.RoomUsable(a, S.Layout.Room(i.Room) ?? S.RoomOf(a)) && Concealment.BodyClass(i.Def) == BodySlot.Pocket).OrderBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
            if (token != null && sc.Approach != "serve") kinds.Add(("token", 1.0));
            var tw = TheirWeapon(sim, sc, a, best);
            if (tw != null && sc.Approach != "serve" && !sc.Approach.StartsWith("method:")) kinds.Add(("weapon", 0.9));
            if ((sc.Approach == "errand") && sc.Moment == "hosted" && !S.A(best).IsPlayer) kinds.Add(("summon", 1.1));
            if (sc.Approach == "rendezvous" && (c.Deceit >= 70 || st.Skill("Writing") || st.Skill("Records"))) kinds.Add(("note", 1.2));
            if (sc.Approach == "serve") kinds.Add(("vial", 1.0));
            if (c.P.Sociability >= 0.45f && !S.A(best).IsPlayer) kinds.Add(("rumor", 0.7));
            if (kinds.Count == 0) kinds.Add(("rumor", 0.5));
            var pick = Local(S, sc.Id + ":fk" + sc.Redesigns).Weighted(kinds, q => q.w).k;
            sc.Frame = pick;
            if (pick == "token") sc.FrameItem = token.Id;
            if (pick == "weapon") { sc.Weapon = tw.Id; sc.WeaponType = tw.Type; sc.FrameItem = tw.Id; }
            if (kinds.Count > 1 && c.Deceit >= 80) { var second = kinds.Where(q => q.k != pick && q.k != "weapon" && q.k != "summon" && q.k != "note").Select(q => q.k).FirstOrDefault(); if (second != null) { sc.Frame2 = second; if (second == "token" && token != null) sc.FrameItem = sc.FrameItem ?? token.Id; } }
        }
        /// <summary>A weapon that points at the scapegoat: from their trade (the cook's knife, the restorer's palette knife) and in reach.</summary>
        static Item TheirWeapon(Simulation sim, Scheme sc, Actor a, string sg)
        {
            var S = sim.S; var st = SchemeStyles.Of(Cast.Get(sg)); var list = WeaponsKnown(sim, a);
            foreach (var it in list) if (it.Owner == sg) return it;
            foreach (var it in list) if (WeaponAffinity(st, it.Type) > 0 && it.Holder == null) return it;
            return null;
        }

        // ------------------------------------------------------------------ the alibi
        static void ChooseAlibi(Simulation sim, Scheme sc, Actor a, Actor v, SchemeStyle st)
        {
            var S = sim.S; var c = a.Def;
            // the treasure hunt: the chart posted everyone alone to a room, so "I was in my zone" is the alibi the house hands out —
            // go there first, be back there after (what breaks it: anyone who saw them anywhere else while the search ran)
            if (sc.Moment != "hosted" && sc.Moment != "joined" && sc.Moment != "meal" && sc.Moment != "investigation"
                && HouseEvents.HuntZoneAt(S, a.Id, sc.StrikeAt, out var hz, out var hAt, out _) && hz != sc.KillRoom
                && !(HouseEvents.HuntZoneAt(S, v.Id, sc.StrikeAt, out var vz, out _, out _) && vz == hz))
            { sc.Alibi = "zone"; sc.AlibiRoom = hz; sc.AlibiAt = hAt; sc.Var("zone-alibi"); return; }
            if (sc.Moment == "call")
            {
                // "I was in the lounge, waiting for my call, with everyone" — broken by whoever saw them leave it
                var cg = S.Gatherings.FirstOrDefault(x => x.Id == sc.MomentRef);
                sc.Alibi = "crowd"; sc.AlibiRoom = cg != null && cg.Revs.Count > 0 ? cg.Cur.Room : -1; sc.EventRoom = sc.AlibiRoom; return;
            }
            if (sc.Moment == "hosted" || sc.Moment == "joined" || sc.Moment == "meal" || sc.Approach == "dark-strike" || sc.Moment == "house-dark") { sc.Alibi = "crowd"; sc.AlibiRoom = sc.MomentRoom; }
            if (sc.Moment == "investigation") { sc.Alibi = "crowd"; return; }
            if (sc.Alibi == "crowd" && sc.Approach != "errand" && sc.Approach != "slip-out") return;
            if (sc.Approach == "serve" || sc.Approach.StartsWith("method:Bedtime") || sc.Approach.StartsWith("method:Shock") || sc.Approach.StartsWith("method:Trap")) { if (sc.Alibi == null) sc.Alibi = "remote"; return; }
            // an arranged witness right after the deed: "at half past nine I was already in the library with 민서"
            double want = 0.2 + c.Infer / 200.0 + (st.Style == "Meticulous" ? 0.2 : 0) + (st.Skill("Clocks") || st.Skill("Schedules") ? 0.2 : 0);
            if (U(S, sc.Id + ":al" + sc.Redesigns) >= want) { if (sc.Alibi == null) sc.Alibi = "none"; return; }
            string w = null; double ws = double.MinValue;
            foreach (var x in S.LivingNpcs.OrderBy(q => q.Id, StringComparer.Ordinal))
            {
                if (x.Id == a.Id || x.Id == v.Id || x.Id == sc.Scapegoat || !S.HasRel(x.Id, a.Id)) continue;
                var r = S.R(x.Id, a.Id); if (r.Trust + r.Like < 0.08f) continue;
                double s = r.Trust + r.Like + (Grammars.HasWatch(x.Id) ? 0 : 0.35) + U(S, sc.Id + ":aw:" + x.Id) * 0.3;   // no watch: they read the room clock
                if (s > ws) { ws = s; w = x.Id; }
            }
            if (w == null) { if (sc.Alibi == null) sc.Alibi = "none"; return; }
            var room = S.Layout.Rooms.Where(r => (r.Type == RoomType.Library || r.Type == RoomType.Lounge || r.Type == RoomType.TeaRoom || r.Type == RoomType.Study || r.Type == RoomType.GameRoom) && Knows(S, a, r) && sim.RoomUsable(a, r) && r.Id != sc.KillRoom && r.Id != sc.MomentRoom)
                          .OrderBy(r => U(S, sc.Id + ":ar:" + r.Id)).FirstOrDefault();
            if (room == null) { if (sc.Alibi == null) sc.Alibi = "none"; return; }
            sc.Alibi = "witness"; sc.AlibiWitness = w; sc.AlibiRoom = room.Id;
            sc.AlibiAt = Math.Ceiling((sc.MomentAt + 25) / 10) * 10;
            sc.Var("arranged-witness");
            // a clock put wrong by a quarter of an hour in the room of the meeting (the witness has no watch and reads the wall)
            var clock = room.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(f => f.Type == "Clock");
            if (clock != null && !Grammars.HasWatch(w) && (st.Skill("Clocks") || c.Infer >= 80) && U(S, sc.Id + ":ck") < 0.75)
            { sc.ClockF = clock.Id; sc.ClockShift = -(10 + Math.Floor(U(S, sc.Id + ":cks") * 3) * 5); sc.Alibi = "clock"; sc.Var("clock-alibi"); }
        }

        static void ChooseGarbAndMark(Simulation sim, Scheme sc, Actor a, Actor v, SchemeStyle st)
        {
            var S = sim.S; var c = a.Def; var k = S.K(a.Id);
            var wdef = ItemCatalog.Get(sc.WeaponType);
            bool bloody = wdef != null && (wdef.Dmg == DamageType.Stab || wdef.Dmg == DamageType.Cut || wdef.Dmg == DamageType.Blunt);
            if (bloody && (sc.Approach == "errand" || sc.Approach == "slip-out" || sc.Approach == "ambush" || sc.Approach == "rendezvous") && (c.Infer + c.Composure > 155 || st.Skill("Outfits")))
            {
                string pref = sc.EventKind == "cooking" ? "SpareApron" : sc.EventKind == "show" || sc.EventKind == "rehearsal" ? "Cloak" : null;
                var g = k.ItemSeen.Keys.Select(S.I).Where(i => i != null && i.Holder == null && (i.Type == "SpareApron" || i.Type == "Raincoat" || i.Type == "Cloak") && sim.RoomUsable(a, S.Layout.Room(i.Room) ?? S.RoomOf(a)))
                          .OrderBy(i => i.Type == pref ? 0 : 1).ThenBy(i => i.Pos.Dist(a.Pos)).ThenBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
                if (g != null) { sc.Garb = g.Id; sc.Var("garb"); }
            }
            if (sc.Approach == "dark-strike")
            {
                // a mark to find them in the dark: something small and scented, pale, or that rings — a gift (never a mark on the weapon)
                var gift = k.ItemSeen.Keys.Select(S.I).Where(i => i != null && i.Holder == null && (i.Type == "Flower" || i.Type == "HandWarmer" || i.Type == "Sticker") && sim.RoomUsable(a, S.Layout.Room(i.Room) ?? S.RoomOf(a)))
                             .OrderBy(i => i.Pos.Dist(a.Pos)).ThenBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault()
                        ?? sim.Carried(a).FirstOrDefault(i => i.Type == "Flower" || i.Type == "HandWarmer" || i.Type == "Candy");
                if (gift != null && !v.IsPlayer) { sc.MarkItem = gift.Id; sc.Var("marked-in-dark"); }
                if (sc.DarkBy == "helper")
                {
                    string h = null; double hs = double.MinValue;
                    foreach (var x in S.LivingNpcs.OrderBy(q => q.Id, StringComparer.Ordinal))
                    {
                        if (x.Id == a.Id || x.Id == v.Id || x.Id == sc.Scapegoat || !S.HasRel(x.Id, a.Id)) continue;
                        var r = S.R(x.Id, a.Id); double s = r.Trust + r.Like + x.Def.P.Loyalty * 0.2 + U(S, sc.Id + ":hp:" + x.Id) * 0.3;
                        if (r.Trust + r.Like > 0.04f && s > hs) { hs = s; h = x.Id; }
                    }
                    if (h == null) sc.DarkBy = "self"; else { sc.Helper = h; sc.HelperTask = "lights"; sc.Var("helper"); }
                }
            }
        }

        // ------------------------------------------------------------------ the culprit's own evening
        static void CreateEvent(Simulation sim, Scheme sc, Actor a, Actor v, MomentCand m)
        {
            var S = sim.S; var room = S.Layout.Room(m.Room);
            var g = new Gathering { Id = S.NewId("gath"), Host = a.Id, Kind = m.EventKind, Label = m.Label };
            g.Revs.Add(new GatheringRev { Room = m.Room, Start = m.At, End = m.End, At = S.Clock, Why = "처음 초대" });
            var guests = new List<string>();
            bool needVictim = sc.Approach == "errand" || sc.Approach == "dark-strike" || sc.Approach == "serve";
            if (needVictim) guests.Add(v.Id);
            if (sc.Helper != null) guests.Add(sc.Helper);
            if (sc.Scapegoat != null && sc.Frame == "summon") guests.Add(sc.Scapegoat);
            int want = 3 + (int)(U(S, sc.Id + ":gn") * 4);
            foreach (var x in S.Living.Where(x => x != a && !x.IsButler && !guests.Contains(x.Id) && x.Id != v.Id && (sc.Approach != "slip-out" || x.Id != sc.Scapegoat))
                                      .OrderByDescending(x => S.R(a.Id, x.Id).Opinion + U(S, sc.Id + ":gs:" + x.Id) * 0.3).ThenBy(x => x.Id, StringComparer.Ordinal))
            { if (guests.Count >= want) break; guests.Add(x.Id); }
            foreach (var id in guests) { g.Status[id] = "unaware"; g.KnownRev[id] = -1; g.Channel[id] = id == Cast.Player ? "note" : "voice"; }
            g.Status[a.Id] = "host"; g.KnownRev[a.Id] = 0;
            S.Gatherings.Add(g);
            S.Flags["grevcheck:" + g.Id] = 1;   // the host keeps their own evening where and when it was set
            sc.EventId = g.Id; sc.Guests.Clear(); sc.Guests.AddRange(guests);
            S.Log("GatheringPlanned", a.Id, data: $"{g.Id} {g.Label} @{room?.Name} {ClockFmt.Vague(m.At)}-{ClockFmt.Vague(m.End)} guests={string.Join(",", guests)} scheme");
        }

        // ------------------------------------------------------------------ the preparation list (each a visible act with a motion)
        static void BuildPrep(Simulation sim, Scheme sc, Actor a, Actor v, SchemeStyle st)
        {
            var S = sim.S; double now = S.Clock; var c = a.Def;
            PrepTask T(string kind, bool essential, double due, string item = null, int room = -1, string target = null, double notBefore = -1, int furn = -1, string note = null)
            {
                foreach (var old in sc.Prep) if (old.Done && old.Kind == kind && old.Item == item && item != null) return old;   // already done in an earlier design
                var t = new PrepTask { Kind = kind, Essential = essential, Due = due, Item = item, Room = room, Target = target, NotBefore = notBefore < 0 ? now : notBefore, Furniture = furn, Note = note };
                sc.Prep.Add(t); return t;
            }
            bool inv = sc.Moment == "investigation";
            double strike = sc.StrikeAt;
            if (sc.Moment == "hosted") T("host", true, sc.MomentAt - 25, room: sc.EventRoom);
            if (inv) { return; }   // no time: the weapon is fetched by the plan itself
            var w = S.I(sc.Weapon);
            if (w != null && w.Holder != a.Id) T("obtain", true, strike - 5, w.Id, w.Room);
            var p = S.I(sc.Poison);
            if (p != null && p.Holder != a.Id) T("poison", true, strike - 5, p.Id, p.Room);
            if (sc.Garb != null) T("garb", false, strike - 5, sc.Garb);
            if (sc.Frame == "token" || sc.Frame2 == "token") { var tk = S.I(sc.FrameItem); if (tk != null && tk.Owner == sc.Scapegoat) T("token", false, strike - 5, tk.Id, tk.Room); }
            if (sc.Approach == "errand" && w != null && sc.KillRoom >= 0)
            {
                var kr = S.Layout.Room(sc.KillRoom); var f = kr != null ? Concealment.PlaceIn(S, kr, w, Center(kr)) : null;
                if (f != null) T("stash", false, sc.MomentAt - 5, w.Id, kr.Id, furn: f.Id);
            }
            bool meticulous = sc.Style == "Meticulous" || c.Infer >= 85;
            if (sc.KillRoom >= 0 && (meticulous ? U(S, sc.Id + ":sc") < 0.85 : U(S, sc.Id + ":sc") < 0.4)) T("scout", false, strike - 20, room: sc.KillRoom);
            if (sc.Moment == "habit" || sc.Moment == "noise" || sc.Moment == "house-dark" || sc.Moment == "long-dark" || sc.Moment == "night-visit" || sc.Approach.StartsWith("method:") || U(S, sc.Id + ":sh") < 0.3)
                T("shadow", sc.Moment == "house-dark", Math.Max(now + 30, strike - 15), target: v.Id);
            if ((sc.Approach == "errand" || sc.Approach == "slip-out" || sc.Approach == "rendezvous") && (meticulous || SchemeStyles.Of(a.Def).Skill("Timing")) && U(S, sc.Id + ":rh") < 0.7)
                T("rehearse", false, strike - 15, room: sc.KillRoom >= 0 ? sc.KillRoom : sc.MomentRoom, note: (sc.EventRoom >= 0 ? sc.EventRoom : sc.MomentRoom).ToString());
            if (sc.AlibiWitness != null) T("witness", false, sc.AlibiAt - 35, room: sc.AlibiRoom, target: sc.AlibiWitness);
            if (sc.ClockF >= 0) { double nine = Day0(sc.AlibiAt) + 9 * 60 + 10; T("clock", false, sc.AlibiAt - 10, room: sc.AlibiRoom, furn: sc.ClockF, notBefore: Math.Max(now, nine)); }
            if (sc.Helper != null) T("helper", false, sc.MomentAt - 10, target: sc.Helper, room: sc.EventRoom);
            if (sc.Approach == "rendezvous") { if (sc.Frame == "note" && sc.Scapegoat != null) T("note", true, sc.MomentAt - 30, room: sc.KillRoom, target: v.Id); else T("appoint", true, sc.MomentAt - 30, room: sc.KillRoom, target: v.Id); }
            if (sc.Frame == "summon" && sc.Scapegoat != null && sc.Approach != "errand") T("summon", false, sc.MomentAt - 20, room: sc.KillRoom, target: sc.Scapegoat);
            if (sc.Frame == "rumor" || sc.Frame2 == "rumor") T("rumor", false, strike - 20, target: sc.Scapegoat);
            if (sc.MarkItem != null) { var mk = S.I(sc.MarkItem); if (mk != null && mk.Holder != a.Id) T("mark-get", false, strike - 10, mk.Id, mk.Room); }
            if (sc.Moment == "hosted" && U(S, sc.Id + ":dr") < 0.7) T("dress", false, sc.MomentAt - 3, room: sc.EventRoom, notBefore: sc.MomentAt - 90);
            // drop what no longer fits in the time left (a task due in the past is not a task)
            sc.Prep.RemoveAll(t => !t.Done && !t.Essential && t.Due < now + 8);
            foreach (var t in sc.Prep) if (!t.Done && t.Due < now + 8) t.Due = now + 8;
        }
    }
}
