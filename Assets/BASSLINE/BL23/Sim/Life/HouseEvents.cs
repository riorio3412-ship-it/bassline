using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// The house's events (SocialEventsDesign §4; owner, 2026-09-28: "1챕터가 길어져도 교류와 이벤트가 공백을 채우면 좋다. 이벤트에서
    /// 살인의 기회도 생긴다"). On a quiet day from the second on, 유스티 announces one evening the house holds — the invitation is
    /// the house's, the answer each resident's own (their fear, their mood, the event's appeal to them, whether their faction's
    /// leader goes). Each runs through the ordinary gathering system (arrivals, slipping out, the attendance record) and is
    /// staged like a festival when 민혁 is there. And each is a stage the murder mind reads:
    ///   연회        the toast: the lights go out for four minutes in a full hall (joined + dark)
    ///   가면의 밤    everyone masked: what the witnesses see is a mask, not a name (Perception: TheaterMask)
    ///   보물찾기     the house posts who searches where, alone — the victim's hour and room are public (moment "hunt")
    ///   별 보는 밤   the lights off for the whole of it, out under the glass (joined + dark)
    ///   밤의 기도    candles only, eyes closed (joined + dark)
    /// No random stream is drawn: the kind, the toast's minute and the search zones are hashes.
    /// </summary>
    public static class HouseEvents
    {
        public static bool Enabled = true;

        internal sealed class Kind { public string Id, Title, Appeal, Dark; public RoomType[] Rooms; public int Start, Len; public bool Masks, Hunt; }

        internal static readonly Kind[] Kinds =
        {
            new Kind { Id = "banquet", Title = "저택의 연회", Rooms = new[] { RoomType.GrandHall, RoomType.Dining }, Start = 19 * 60 + 40, Len = 90, Dark = "toast", Appeal = "party" },
            new Kind { Id = "masque", Title = "가면의 밤", Rooms = new[] { RoomType.GrandHall, RoomType.Theater, RoomType.Lounge }, Start = 20 * 60 + 30, Len = 80, Masks = true, Appeal = "party" },
            new Kind { Id = "hunt", Title = "저택 보물찾기", Rooms = new[] { RoomType.GrandHall, RoomType.Lounge }, Start = 14 * 60, Len = 100, Hunt = true, Appeal = "game" },
            new Kind { Id = "stars", Title = "별 보는 밤", Rooms = new[] { RoomType.Greenhouse, RoomType.Courtyard }, Start = 21 * 60, Len = 70, Dark = "whole", Appeal = "read" },
            new Kind { Id = "vigil", Title = "밤의 기도", Rooms = new[] { RoomType.Chapel }, Start = 21 * 60, Len = 50, Dark = "whole" },
        };
        internal static Kind KindOf(Gathering g) => g?.Kind != null && g.Kind.StartsWith("house:", StringComparison.Ordinal) ? Kinds.FirstOrDefault(k => "house:" + k.Id == g.Kind) : null;
        public static bool IsHouse(Gathering g) => g?.Kind != null && g.Kind.StartsWith("house:", StringComparison.Ordinal);

        /// <summary>Does the house put the lights out during this gathering's window (the toast, a dark evening)?</summary>
        public static bool Dark(GameState S, Gathering g)
        {
            var k = KindOf(g); if (k == null || k.Dark == null) return false;
            return k.Dark == "whole" || S.Flags.ContainsKey($"hevdark:{g.Id}");
        }

        /// <summary>The treasure hunt: the room this resident was posted to, and the hours (public — the house read the chart out).</summary>
        public static bool HuntZone(GameState S, string who, out int room, out double at, out double end)
        {
            room = -1; at = end = -1;
            foreach (var g in S.Gatherings.Where(g => g.Kind == "house:hunt" && !g.Cancelled).OrderBy(g => g.Id, StringComparer.Ordinal))
            {
                if (!S.Flags.TryGetValue($"hevzone:{g.Id}:{who}", out var r)) continue;
                room = (int)r; at = g.Cur.Start + 10; end = g.Cur.Start + (KindOf(g)?.Len ?? 90);
                if (end < S.Clock) continue;
                return true;
            }
            return false;
        }

        /// <summary>Would this resident come to the house's evening? Their own mood and taste, the evening's nature, who else has
        /// said they are going (a friend pulls, someone they resent or fear pushes away), and where their faction's leader goes.
        /// The reason for a refusal is kept (fear · someone there · the crowd · the leader) — the table voices it.</summary>
        internal static bool WouldAttend(Simulation sim, Actor x, Kind k, Gathering g)
        {
            var S = sim.S; if (x.IsPlayer) return true;
            var p = x.Def.P;
            double fear = x.Needs.Fear * 0.9 + x.Needs.Stress * 0.3, crowd = (0.55 - p.Sociability) * 0.8;
            double s = 0.3 + (k.Appeal != null && x.Def.Hobbies.Contains(k.Appeal) ? 0.3 : 0) - fear - Math.Max(0, crowd) + (S.Chapter > 1 ? -0.1 : 0);
            switch (k.Id)
            {
                case "vigil": s += p.Morality * 0.3 + x.Needs.Grief * 0.5 - 0.25; break;
                case "stars": s += p.Romance * 0.3 + p.Curiosity * 0.2 - 0.1; break;
                case "masque": s += p.Pride * 0.2 - p.Fearfulness * 0.3; break;
                case "banquet": s += 0.1; break;
                case "hunt": s += p.Curiosity * 0.25 - p.Fearfulness * 0.2; break;
            }
            // who has already said yes
            string shun = null; double pull = 0;
            foreach (var kv in g.Status.Where(kv => kv.Key != x.Id && kv.Value == "accepted").OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (!S.HasRel(x.Id, kv.Key)) continue; var r = S.R(x.Id, kv.Key);
                pull += Math.Max(0, r.Like) * 0.08;
                if ((r.Grudge > 0.3f || r.Fear > 0.3f || r.Tags.Contains("enemy")) && shun == null) shun = kv.Key;
            }
            s += Math.Min(0.3, pull) - (shun != null ? 0.25 : 0);
            double lead = Factions.InviteBias(S, x.Id, g); s += lead;
            if (x.PlanId != null && S.Plans.TryGetValue(x.PlanId, out var pl) && pl.Grammar == "Gathering") s += 1;
            bool yes = s > 0.2;
            if (!yes)
            {
                string why = fear >= 0.35 ? "fear" : shun != null ? "enemy" : lead <= -0.1 ? "lead" : crowd > 0.05 ? "crowd" : "rest";
                S.K(x.Id).Facts.Add($"hevno:{g.Id}:{why}");
            }
            return yes;
        }
        /// <summary>Why this resident said no to that evening ("fear", "enemy", "lead", "crowd", "rest"), or null.</summary>
        public static string NoReason(GameState S, string who, string gid)
        {
            string pre = $"hevno:{gid}:";
            var f = S.K(who).Facts.Where(x => x.StartsWith(pre, StringComparison.Ordinal)).OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault();
            return f?.Substring(pre.Length);
        }
    }

    public sealed partial class Simulation
    {
        /// <summary>10:00 (before the residents' own festivals are planned): the house's evening, on a quiet day from the second on.</summary>
        void HouseEventPlan()
        {
            if (!HouseEvents.Enabled || S.Phase != Phase.Daily || S.Day < 2 || LifeAfterDeath(24)) return;
            if (S.Ch.InvestigationEnd >= 0 && S.Clock < S.Ch.InvestigationEnd) return;
            if (S.Incidents.Values.Any(i => i.Loop == S.Loop && i.Chapter == S.Chapter)) return;   // the house hosts while nothing has happened
            if (S.Flags.ContainsKey($"hevday:{S.Loop}:{S.Day}") || S.Survivors <= S.FloorLocked + 1) return;
            var kinds = HouseEvents.Kinds.Where(k => S.Layout.Rooms.Any(r => k.Rooms.Contains(r.Type) && !S.Flags.ContainsKey("swallowed:" + r.Id))).ToList();
            var fresh = kinds.Where(k => !S.Flags.ContainsKey($"hev:{S.Loop}:{k.Id}")).ToList(); if (fresh.Count > 0) kinds = fresh;
            if (kinds.Count == 0) return;
            var kind = kinds[(int)(MurderHash.U01(S, $"hevkind:{S.Loop}:{S.Day}") * kinds.Count) % kinds.Count];
            var room = S.Layout.Rooms.Where(r => kind.Rooms.Contains(r.Type) && !S.Flags.ContainsKey("swallowed:" + r.Id)).OrderBy(r => Array.IndexOf(kind.Rooms, r.Type)).ThenBy(r => r.Id).FirstOrDefault();
            if (room == null) return;
            double start = Math.Floor(S.Clock / 1440) * 1440 + kind.Start; if (start < S.Clock + 60) return;
            if ((S.Gatherings ?? new List<Gathering>()).Any(g => !g.Done && !g.Cancelled && g.Revs.Count > 0 && Math.Abs(g.Cur.Start - start) < 120)) return;
            HouseEventCreate(kind, room, start);
        }

        internal Gathering HouseEventCreate(HouseEvents.Kind k, Room room, double start)
        {
            if (S.Gatherings == null) S.Gatherings = new List<Gathering>();
            // the treasure hunt gathers everyone for the briefing only; then they spread out to their posted rooms
            double end = start + (k.Hunt ? 12 : k.Len);
            var g = new Gathering { Id = S.NewId("gath"), Host = Cast.Butler, Kind = "house:" + k.Id, Label = k.Title };
            g.Revs.Add(new GatheringRev { Room = room.Id, Start = start, End = end, At = S.Clock, Why = "저택의 초대" });
            g.Status[Cast.Butler] = "host"; g.KnownRev[Cast.Butler] = 0; g.Channel[Cast.Butler] = "voice";
            foreach (var x in S.Living.Where(x => !x.IsButler).OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                g.Status[x.Id] = x.IsPlayer ? "invited" : HouseEvents.WouldAttend(this, x, k, g) ? "accepted" : "declined";
                g.KnownRev[x.Id] = 0; g.Channel[x.Id] = "voice";
            }
            S.Gatherings.Add(g);
            S.Flags[$"hev:{S.Loop}:{k.Id}"] = S.Day; S.Flags[$"hevday:{S.Loop}:{S.Day}"] = 1;
            if (k.Dark == "toast") S.Flags[$"hevdark:{g.Id}"] = start + 20 + Math.Floor(MurderHash.U01(S, "hevdark:" + g.Id) * 4) * 10;   // the toast
            var slots = new Dictionary<string, string> { { "place", room.Name }, { "time", ClockFmt.Mark(start, true) }, { "when", ClockFmt.Mark(start, true) }, { "act", k.Title } };
            if (k.Hunt)
            {
                // the zone chart, read out in the morning: who searches where, alone
                var zones = S.Layout.Rooms.Where(r => HuntRoom(r.Type) && !S.Flags.ContainsKey("swallowed:" + r.Id)).OrderBy(r => r.Id).ToList();
                var hunters = g.Status.Where(kv => kv.Value == "accepted" || kv.Value == "invited").Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal).ToList();
                var chart = new List<string>();
                if (zones.Count > 0)
                    for (int i = 0; i < hunters.Count; i++)
                    {
                        int off = (int)(MurderHash.U01(S, "hevzone:" + g.Id) * zones.Count);
                        var z = zones[(off + i * 3) % zones.Count];
                        S.Flags[$"hevzone:{g.Id}:{hunters[i]}"] = z.Id;
                        chart.Add($"{Cast.GivenOf(hunters[i])} 님 — {z.Name}");
                        foreach (var w in S.Living) S.K(w.Id).Facts.Add($"huntzone:{g.Id}:{hunters[i]}:{z.Id}");
                    }
                // three to a page, as 유스티 reads it out
                slots["list"] = string.Join("|", chart.Select((c, i) => (c, i)).GroupBy(x => x.i / 3).Select(gr => string.Join(", ", gr.Select(x => x.c))));
            }
            S.Log("GatheringPlanned", Cast.Butler, data: $"{g.Id} {k.Title} @{room.Name} {ClockFmt.Vague(start)}-{ClockFmt.Vague(end)} guests={string.Join(",", g.Status.Where(kv => kv.Value == "accepted").Select(kv => kv.Key))}");
            S.Log("HouseEvent", Cast.Butler, room: room.Id, data: k.Id + ":" + g.Id);
            LFinc("lhev:total");
            Announce("y_hev_" + k.Id, slots, "hev:" + k.Id);
            return g;
        }

        /// <summary>The house's evening, staged (LifeFest.FestBuild): 유스티 opens, the guests speak in their own voices, 민혁 may
        /// say one thing — and what he says can hold people together (Conscience.Mend) or just warm a tie.</summary>
        void HouseEventScene(GenScene sc, SceneRun run, Gathering g, List<string> present, bool withPlayer)
        {
            var k = HouseEvents.KindOf(g); if (k == null) return;
            string Y = Cast.Butler;
            var voiced = present.Where(x => x != Y && LineBank.Has(x, "hev_" + k.Id)).OrderByDescending(x => (Factions.Of(S, x)?.Leader == x ? 0.5 : 0) + MurderHash.U01(S, "hevseat:" + g.Id + ":" + x)).ThenBy(x => x, StringComparer.Ordinal).Take(4).ToList();
            // at most one guest with no words of their own (they say the shared line)
            var guests = voiced.Concat(present.Where(x => x != Y && !voiced.Contains(x)).OrderBy(x => MurderHash.U01(S, "hevseat:" + g.Id + ":" + x)).ThenBy(x => x, StringComparer.Ordinal).Take(voiced.Count >= 3 ? 0 : 1)).ToList();
            string near = guests.OrderByDescending(x => S.R(Cast.Player, x).Like + S.R(x, Cast.Player).Like + S.R(x, Cast.Player).Romance).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault();
            if (near != null) run.Ctx["t"] = near;
            string pact = string.Join(",", guests.Where(x => Conscience.TookPact(S, x)).OrderBy(x => x, StringComparer.Ordinal));
            sc.Open(FV(Y, "hev_open_" + k.Id, "알려 드린 대로, " + k.Title + "입니다.", Emotion.Neutral));
            for (int i = 0; i < guests.Count; i++)
            {
                string to = withPlayer ? Cast.Player : guests.Count > 1 ? guests[(i + 1) % guests.Count] : null;
                sc.First.Lines.Add(FK(guests[i], "hev_" + k.Id, to, k.Id == "vigil" ? Emotion.Sad : k.Id == "hunt" || k.Id == "banquet" ? Emotion.Smile : Emotion.Neutral));
            }
            if (!withPlayer || guests.Count == 0) return;
            var beat = sc.First; string first = guests[0];
            switch (k.Id)
            {
                case "banquet":
                    beat.Opts.Add(FO("건배 전에 제가 한마디 할게요.", FV(first, "hev_banquet_toast", "좋아요. 짧게 해요.", Emotion.Smile)).Do(guests.Select(x => FFx(x, "me", like: 0.01f, respect: 0.02f)).ToArray()));
                    beat.Opts.Add(FO("잔은 받지 않을게요. 조심해서요.", FV(first, "hev_banquet_wary", "…그것도 맞는 말이네요.", Emotion.Neutral)).Do(guests.Select(x => FFx(x, "me", respect: S.A(x).Def.Obs >= 60 ? 0.03f : 0f, like: S.A(x).Def.P.Pride > 0.7f ? -0.01f : 0f)).ToArray()));
                    beat.Opts.Add(FO("말없이 잔을 든다").Act());
                    break;
                case "masque":
                    beat.Opts.Add(FO("가면 쓰니까 누가 누군지 하나도 모르겠네요.", FV(first, "hev_masque_guess", "그게 재밌는 거잖아요.", Emotion.Smile)).Do(guests.Select(x => FFx(x, "me", like: 0.01f)).ToArray()));
                    beat.Opts.Add(FO("저는 가면 안 쓸래요. 얼굴 보고 얘기하고 싶어서요.", FV(first, "hev_masque_bare", "…그럼 표정 다 보이겠네요.", Emotion.Neutral)).Do(guests.Select(x => FFx(x, "me", respect: 0.02f)).ToArray()));
                    beat.Opts.Add(FO("말없이 가면을 쓴다").Act());
                    break;
                case "hunt":
                    if (near != null) beat.Opts.Add(FO("{t} 씨, 같이 찾을래요?", FV(near, "hev_hunt_pair", "…구역이 다르잖아요. 그래도, 좋아요.", Emotion.Smile)).Do(FFx(near, "me", like: 0.04f, attach: 0.02f, mem: "보물찾기를 같이 하자고 했다")));
                    beat.Opts.Add(FO("혼자 찾을게요. 구역은 지켜야죠.").Do(guests.Select(x => FFx(x, "me", respect: 0.01f)).ToArray()));
                    beat.Opts.Add(FO("말없이 구역표를 다시 본다").Act());
                    break;
                case "stars":
                    if (near != null) beat.Opts.Add(FO("{t} 씨, 저 별 이름 알아요?", FV(near, "hev_stars_name", "…몰라요. 그래도 같이 보니까 좋네요.", Emotion.Smile)).Do(FFx(near, "me", like: 0.04f, attach: 0.03f, mem: "별 이름을 같이 찾았다")));
                    { var o = FO("다들 소원 하나씩 빌어요. 저택 말고, 별한테요.", FV(first, "hev_stars_wish", "…별한테라면, 빌어도 되겠네요.", Emotion.Smile)).Do(guests.Select(x => FFx(x, "me", like: 0.02f)).ToArray()); if (pact.Length > 0) o.Know("mend:" + pact); beat.Opts.Add(o); }
                    beat.Opts.Add(FO("말없이 하늘을 본다").Act());
                    break;
                case "vigil":
                    { var o = FO("…다들 무사하게 해 주세요.", FV(first, "hev_vigil_amen", "…네. 다들요.", Emotion.Sad)).Do(guests.Select(x => FFx(x, "me", like: 0.01f)).ToArray()); if (pact.Length > 0) o.Know("mend:" + pact); beat.Opts.Add(o); }
                    beat.Opts.Add(FO("눈은 감지 않을게요. 누군가는 깨어 있어야죠.").Do(guests.Select(x => FFx(x, "me", respect: 0.03f)).ToArray()));
                    beat.Opts.Add(FO("말없이 눈을 감는다").Act());
                    break;
            }
        }

        /// <summary>An evening a resident thought up (Grammars): the host opens, the guests talk, 민혁 may say one thing.</summary>
        void GatherScene(GenScene sc, SceneRun run, Gathering g, List<string> present, bool withPlayer)
        {
            string host = g.Host; var guests = present.Where(x => x != host).OrderBy(x => MurderHash.U01(S, "gseat:" + g.Id + ":" + x)).ThenBy(x => x, StringComparer.Ordinal).Take(3).ToList();
            sc.Open(FK(host, "gath_open_" + g.Kind, withPlayer ? Cast.Player : guests.FirstOrDefault(), Emotion.Smile));
            for (int i = 0; i < guests.Count; i++) sc.First.Lines.Add(FK(guests[i], "gathering_chat", i == 0 ? host : guests[i - 1], Emotion.Smile));
            if (!withPlayer) return;
            // the one standing off to the side (lowest ties to the rest)
            string lone = guests.OrderBy(x => guests.Where(y => y != x).Select(y => Factions.Affinity(S, x, y)).DefaultIfEmpty(0).Average()).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault();
            if (lone != null) run.Ctx["t"] = lone;
            var beat = sc.First;
            beat.Opts.Add(FO("초대해 줘서 고마워요.", FK(host, "gathering_yes", Cast.Player, Emotion.Smile)).Do(FFx(host, "me", like: 0.04f, attach: 0.01f, mem: "모임에 와 줬다")));
            if (lone != null) beat.Opts.Add(FO("{t} 씨, 이쪽으로 와서 같이 해요.", FK(lone, "gathering_yes", Cast.Player, Emotion.Smile)).Do(FFx(lone, "me", like: 0.04f, trust: 0.02f, mem: "모임에서 나를 끌어 줬다"), FFx(host, "me", like: 0.01f)));
            beat.Opts.Add(FO("조용히 구석에 앉는다").Act());
        }

        static bool HuntRoom(RoomType t) => t == RoomType.Library || t == RoomType.Archive || t == RoomType.Storage || t == RoomType.Gallery || t == RoomType.ClockMuseum
            || t == RoomType.WineCellar || t == RoomType.Study || t == RoomType.Workshop || t == RoomType.TrophyRoom || t == RoomType.DollRoom || t == RoomType.MirrorWater
            || t == RoomType.MusicRoom || t == RoomType.Wardrobe || t == RoomType.Greenhouse;

        /// <summary>Each minute: the toast's darkness, a dark evening's lights, the masks, the search.</summary>
        void HouseEventMinute(int mod)
        {
            foreach (var g in (S.Gatherings ?? new List<Gathering>()).Where(g => HouseEvents.IsHouse(g) && !g.Cancelled).ToList())
            {
                var k = HouseEvents.KindOf(g); if (k == null) continue;
                var room = S.Layout.Room(g.Cur.Room); if (room == null) continue;
                double start = g.Cur.Start, end = start + k.Len;
                // the lights: the toast (four minutes) or the whole evening
                double off = k.Dark == "toast" && S.Flags.TryGetValue($"hevdark:{g.Id}", out var t) ? t : k.Dark == "whole" ? start : -1;
                double on = k.Dark == "toast" ? off + 4 : end;
                if (off >= 0 && S.Clock >= off && S.Clock < on && !S.Flags.ContainsKey($"hevoff:{g.Id}") && S.Phase == Phase.Daily)
                {
                    S.Flags[$"hevoff:{g.Id}"] = S.Clock;
                    if (room.Circuit > 0) SetCircuit(room.Circuit, false, null);
                    S.Emit(GameEventType.Notice, text: k.Dark == "toast" ? "건배하는 순간, 연회장의 불이 꺼졌다" : k.Id == "vigil" ? "촛불만 남기고 불이 꺼졌다" : "별을 보려고 불을 껐다", key: "house_dark");
                    S.Log("HouseDark", Cast.Butler, room: room.Id, data: g.Id);
                }
                if (off >= 0 && S.Clock >= on && S.Flags.ContainsKey($"hevoff:{g.Id}") && !S.Flags.ContainsKey($"hevon:{g.Id}"))
                {
                    S.Flags[$"hevon:{g.Id}"] = S.Clock;
                    if (room.Circuit > 0) SetCircuit(room.Circuit, true, null);
                    S.Log("HouseLight", Cast.Butler, room: room.Id, data: g.Id);
                }
                // the masks: handed out at the door, taken back at the end (the masks stay in the hall)
                if (k.Masks && S.Clock >= start - 5 && S.Clock < end && !S.Flags.ContainsKey($"hevmask:{g.Id}"))
                {
                    S.Flags[$"hevmask:{g.Id}"] = S.Clock;
                    foreach (var x in S.LivingNpcs.Where(x => g.Status.TryGetValue(x.Id, out var st) && (st == "accepted" || st == "attended" || st == "late") && x.Disguise == null).OrderBy(x => x.Id, StringComparer.Ordinal))
                    {
                        var m = new Item { Id = S.NewId("it"), Type = "TheaterMask", Name = "가면의 밤 가면", Owner = Cast.Butler, Holder = x.Id, Pos = x.Pos, Room = x.Room, HomeRoom = room.Id };
                        S.Items[m.Id] = m; x.Disguise = m.Id; S.Flags[$"hevmaskof:{x.Id}"] = 1;
                        S.Emit(GameEventType.Disguise, x.Id, data: m.Type);
                    }
                    S.Log("HouseMasks", Cast.Butler, room: room.Id, data: g.Id);
                }
                if (k.Masks && S.Clock >= end && S.Flags.ContainsKey($"hevmask:{g.Id}") && !S.Flags.ContainsKey($"hevunmask:{g.Id}"))
                {
                    S.Flags[$"hevunmask:{g.Id}"] = S.Clock;
                    foreach (var x in S.Actors.Values.Where(x => S.Flags.ContainsKey($"hevmaskof:{x.Id}")).OrderBy(x => x.Id, StringComparer.Ordinal))
                    {
                        S.Flags.Remove($"hevmaskof:{x.Id}");
                        var m = x.Disguise != null ? S.I(x.Disguise) : null;
                        if (m == null || m.Name != "가면의 밤 가면") continue;
                        x.Disguise = null; m.Holder = null; m.Room = x.Room; m.Pos = x.Pos;
                        S.Emit(GameEventType.Disguise, x.Id, data: null); S.Emit(GameEventType.ItemMoved, null, data: m.Id, text: "drop", pos: m.Pos);
                    }
                }
                // the search: after the briefing, the hunters go to their posted rooms and look (alone)
                if (k.Hunt && S.Clock >= start + 10 && !S.Flags.ContainsKey($"hevgo:{g.Id}") && S.Phase == Phase.Daily)
                {
                    S.Flags[$"hevgo:{g.Id}"] = S.Clock;
                    foreach (var x in S.LivingNpcs.OrderBy(x => x.Id, StringComparer.Ordinal))
                    {
                        if (!S.Flags.TryGetValue($"hevzone:{g.Id}:{x.Id}", out var zr) || x.PlanId != null || x.TalkingTo != null || x.Status != ActorStatus.Active) continue;
                        var z = S.Layout.Room((int)zr); if (z == null || !RoomUsable(x, z)) continue;
                        var act = new Activity { Id = "hev:hunt:" + g.Id, Label = "보물찾기", Priority = 2.3, Interruptible = true };
                        act.Steps.Add(GoTo(RandomPointIn(z, S.R(Stream.Life))));
                        act.Steps.Add(Do("search", Math.Max(20, k.Len - 20), Anim.Search));
                        Assign(x, act);
                    }
                    S.Log("HouseHunt", Cast.Butler, data: g.Id);
                }
            }
        }
    }
}
