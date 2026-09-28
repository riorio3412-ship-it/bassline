using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// What the second-wave methods leave for an examiner, where their weapons live, and how the reveal narrates them.
    /// Clue budget per method (readability — the user found evidence floods confusing): two or three strong, specific facts.
    ///   교살: the ligature line on the neck · skin under the victim's nails ↔ a fresh scratch on someone's hand · the stretched cord
    ///   추락: palm bruises from the front · a torn sleeve button at the top · the slip mark at the edge
    ///   감전: a cable stripped with a blade · the breaker's trip time on the switchboard · the burn on the palm
    ///   수면제+질식: pressure marks on the face · the pressed pillow · the draught in the dinner cup (+ a forged note / a key by the door)
    ///   시간차 독: the dosed bedtime drink in the victim's own room · poisoning on the body · a glimpse of who went into that room by day
    ///   방 은폐: the incinerator still hot + a burnt remnant · the weapon in the pool filter · freshly turned soil · frost on the body
    /// </summary>
    public static partial class Methods
    {
        // ================================================================== weapons that belong to rooms (placed every loop, deterministic)
        static readonly (RoomType room, string item, string[] on)[] Armory =
        {
            (RoomType.Study, "LetterOpener", new[] { "Desk" }), (RoomType.Archive, "LetterOpener", new[] { "ReadingTable", "Desk" }),
            (RoomType.Library, "Bookend", new[] { "Bookshelf", "ReadingTable" }), (RoomType.Study, "Bookend", new[] { "Bookshelf" }),
            (RoomType.Lounge, "Statuette", new[] { "SideTable", "Console", "Sideboard" }), (RoomType.Parlor, "Statuette", new[] { "SideTable", "Console", "RoundTable" }),
            (RoomType.Gallery, "Statuette", new[] { "Pedestal" }), (RoomType.TrophyRoom, "Statuette", new[] { "Pedestal", "DisplayCase" }),
            (RoomType.Lounge, "Decanter", new[] { "BarCounter", "Sideboard", "Console" }), (RoomType.Dining, "Decanter", new[] { "Sideboard", "LongTable" }), (RoomType.WineCellar, "Decanter", new[] { "Barrel", "WineRack" }),
            (RoomType.Lounge, "IcePick", new[] { "BarCounter", "Sideboard" }), (RoomType.Kitchen, "IcePick", new[] { "Counter", "Island" }),
            (RoomType.Greenhouse, "GardenShears", new[] { "Planter", "Bench" }), (RoomType.Greenhouse, "Foxglove", new[] { "Planter" }),
            (RoomType.Storage, "Crowbar", new[] { "Crates", "Shelves" }), (RoomType.BoilerRoom, "Crowbar", new[] { "Crates", "Pipes" }), (RoomType.Incinerator, "Crowbar", new[] { "Crates", "Incinerator" }),
            (RoomType.Laundry, "Iron", new[] { "DryRack", "Shelves", "Washer" }),
            (RoomType.Infirmary, "Scalpel", new[] { "MedCabinet", "Desk" }),
            (RoomType.GameRoom, "CueStick", new[] { "PoolTable" }), (RoomType.TrophyRoom, "SkinningKnife", new[] { "StuffedBeast", "Pedestal" }),
            (RoomType.Lounge, "CurtainCord", new[] { "Sofa", "Armchair", "Plant" }), (RoomType.Parlor, "CurtainCord", new[] { "Armchair", "Sofa" }), (RoomType.Theater, "CurtainCord", new[] { "Stage", "Seats" }), (RoomType.GuestRoom, "CurtainCord", new[] { "Bed", "Wardrobe" }),
            (RoomType.MusicRoom, "PianoWire", new[] { "Piano", "Piano_Upright" }),
            (RoomType.Workshop, "ExtensionCord", new[] { "Workbench", "ToolWall" }), (RoomType.Theater, "ExtensionCord", new[] { "Stage", "FilmProjector" }),
            (RoomType.Workshop, "Pliers", new[] { "Workbench", "ToolWall" }), (RoomType.PowerRoom, "Pliers", new[] { "Switchboard", "Generator" }),
            (RoomType.Workshop, "Hacksaw", new[] { "ToolWall", "Workbench" }), (RoomType.MachineRoom, "Hacksaw", new[] { "Crates", "Shelves" }), (RoomType.ColdStorage, "BoneSaw", new[] { "ColdLocker", "Crates" }), (RoomType.Kitchen, "BoneSaw", new[] { "Counter", "Island" }),
        };

        /// <summary>After the loop's items are spawned: the new weapons and tools on the furniture they belong to (one per entry, if the room exists).</summary>
        public static void SpawnLoop(Simulation sim)
        {
            var S = sim.S; var rng = new Rng(S.Rng.CampaignSeed ^ Rng.Hash("armory#" + S.Loop), 0x6D757264UL);
            int n = 0;
            // surfaces already holding the loop's items (same slot scheme as the layout's settle pass, so nothing lands on top of another)
            var used = new Dictionary<int, int>();
            foreach (var it0 in S.Items.Values.Where(i => i.Holder == null && i.Room >= 0).OrderBy(i => i.Id, StringComparer.Ordinal))
                foreach (var fid in S.Layout.Room(it0.Room)?.Furniture ?? new List<int>())
                { var f0 = S.Layout.Furniture[fid]; NavGrid.GetFootprint(f0, 0f, out var fr); if (fr.Contains(it0.Pos.x, it0.Pos.z)) { used[fid] = (used.TryGetValue(fid, out var u) ? u : 0) + 1; break; } }
            foreach (var (rt, type, on) in Armory)
            {
                var room = S.Layout.Rooms.Where(r => r.Type == rt && !r.Void).OrderBy(r => r.Id).FirstOrDefault(); if (room == null || ItemCatalog.Get(type) == null) continue;
                var near = new P3(room.Floor, room.Rect.CX, room.Rect.CZ);
                P3? p = type == "Crowbar" ? null : ItemPlacement.OnSurface(S.Layout, room, on, near, used, rng, out _) ?? ItemPlacement.OnSurface(S.Layout, room, new[] { "Workbench", "Console", "SideTable", "Desk", "Sideboard", "Counter", "Chest", "Crates", "Pedestal", "CoffeeTable", "RoundTable" }, near, used, rng, out _);
                if (p == null) { var host = room.Furniture.Select(i => S.Layout.Furniture[i]).Where(x => on.Contains(x.Type)).OrderBy(x => x.Id).FirstOrDefault(); if (ItemPlacement.AgainstWall(S.Layout, room, host != null ? new P3(host.Pos.f, host.Pos.x, host.Pos.z) : near, rng, out var wp)) p = wp; }
                if (p == null) continue;
                var it = new Item { Id = "it_m" + (++n), Type = type, Pos = p.Value, Room = room.Id, Yaw = rng.Range(0, 360), HomeRoom = room.Id, HomePos = p.Value };
                S.Items[it.Id] = it;
            }
            Violence.SpawnLoop(sim, used);   // --- violence track: guns, the crossbow, their ammunition (elsewhere), rope / tape / a scarf
        }

        // ================================================================== the body
        public static void BodyNotes(Simulation sim, Actor ex, Actor body, float skill, Rng rng, List<string> lines, List<Prop> props)
        {
            var S = sim.S; var W = body.Body.Wounds.Where(w => !w.Postmortem).ToList(); string V = Cast.GivenOf(body.Id);
            void P(string v, double t0 = double.NaN) => props.Add(new Prop { Kind = PropKind.TraceAt, A = body.Id, Room = body.Room, T0 = double.IsNaN(t0) ? S.Clock : t0, T1 = S.Clock, Value = v });
            if (W.Any(w => w.Region == BodyRegion.Neck && w.Type == DamageType.Choke && !(w.CauseEvent != null && w.CauseEvent.StartsWith("manual"))))   // (violence track: bare hands leave finger bruises instead)
            { lines.Add("· 목을 가로로 두른 가는 끈 자국 — 뒤에서 조른 흔적이다(스스로 목을 맸다면 자국의 방향이 다르다)"); P("ligature"); }
            if (S.Flags.ContainsKey("nails:" + body.Id) && (skill > 0.45f || ex.IsPlayer))
            { lines.Add("· 손톱 밑에 다른 사람의 살갗과 핏자국이 끼어 있다 — 범인의 손이나 팔에 긁힌 상처가 남았을 것이다"); P("nail-scrape"); }
            if (W.Any(w => w.Region == BodyRegion.Head && w.Type == DamageType.Choke))
            { lines.Add("· 코와 입 주변이 짓눌렸고 눈 흰자에 작은 붉은 점이 흩어져 있다 — 무언가로 얼굴을 눌러 숨을 막았다"); P("smother-marks"); }
            if (S.Flags.TryGetValue("sedated:" + body.Id, out var sedAt) && (skill + rng.F() * 0.3f > 0.5f || ex.IsPlayer))
            { lines.Add("· 입가와 숨결에서 달큰한 약 냄새가 난다 — 죽기 전에 수면제를 먹었다"); P("sedated", sedAt); }
            if (S.Flags.ContainsKey("pushed:" + body.Id) && (skill > 0.4f || ex.IsPlayer))
            { lines.Add("· 가슴과 어깨에 손바닥만 한 멍이 두 개 있다 — 떨어지기 직전, 앞에서 누가 세게 밀친 자국이다"); P("push-bruise"); }
            if (W.Any(w => w.Type == DamageType.Fall))
            { lines.Add("· 머리와 팔다리의 멍이 한쪽으로 몰려 있다 — 높은 곳에서 떨어지며 생긴 상처다"); P("fall-injuries"); }
            if (W.Any(w => w.Type == DamageType.Shock))
            { lines.Add("· 오른손바닥에 작은 화상과 붉은 줄무늬가 있다 — 전기가 몸을 타고 지나갔다"); P("electrocuted"); }
            if (S.Flags.ContainsKey("frost:" + body.Id))
            { lines.Add("· 옷깃과 머리카락에 서리가 앉아 있다 — 한동안 몹시 차가운 곳에 놓여 있었다"); P("temp-cold"); }
            if (W.Any(w => w.Type == DamageType.Drown) && S.Flags.TryGetValue("drownclothed:" + body.Id, out var clothed) && clothed > 0)
            { lines.Add("· 겉옷과 신발을 그대로 입은 채 물에 빠졌다 — 수영하러 들어간 차림이 아니다"); P("clothed-drowning"); }
            if (body.Wet && W.Any(w => w.Type == DamageType.Drown) && S.Flags.ContainsKey("nails:" + body.Id))
            { lines.Add("· 물을 들이켜고 숨졌는데, 목덜미와 어깨에 손가락 자국이 있다 — 누군가 물속으로 짓눌렀다"); P("held-under"); }
            if (S.Flags.ContainsKey("dismembered:" + body.Id))
            {
                int cut = body.Body.Wounds.Count(w => w.Postmortem && w.CauseEvent == "dismember");
                lines.Add($"· 팔다리 {cut}곳이 잘려 나갔다 — 절단면에 피가 번진 멍도, 오그라든 가장자리도 없다. 숨이 멎은 뒤에 잘린 것이다"); P("postmortem-cut");
                if (skill > 0.4f || ex.IsPlayer) { lines.Add("· 절단면의 뼈에 톱니 자국이 일정한 간격으로 나 있다 — 칼이 아니라 톱을 썼다"); P("saw-marks"); }
            }
            Violence.BodyNotes(sim, ex, body, skill, rng, lines, props);   // --- violence track: gunshot / bolt / hands / hair / bindings
        }

        // ================================================================== traces
        public static void TraceNotes(Simulation sim, Actor ex, Trace t, List<string> lines, List<Prop> props)
        {
            var S = sim.S;
            void P(string v) => props.Add(new Prop { Kind = PropKind.TraceAt, A = t.Victim, Room = t.Room, T0 = t.Clock, T1 = S.Clock, Value = v, Item = t.Type });
            switch (t.Type)
            {
                case "KeySlide": lines.Add("· 긁힘은 문 아래 틈에서 방 안쪽으로 곧게 이어진다 — 밖에서 무언가를 밀어 넣은 방향이다"); P("key-slid"); break;
                case "Scorch": lines.Add("· 전선이 닳아서 생긴 그을음이 아니다 — 피복을 벗긴 구리선이 물에 닿은 자리부터 탔다"); P("scorch"); break;
                case "Soil": lines.Add("· 흙 속에 단단한 것이 묻혀 있는 감촉이 있다"); P("soil-dug"); break;
                case "PowderSpill": P("powder"); break;
                case "DrainBlood": lines.Add("· 씻겨 내려간 핏물이 배수구 틈에 고여 있다 — 누군가 이 방에서 많은 피를 씻어 냈다"); P("drain-blood"); break;
                case "FootprintWet": if (t.Note == "drown") P("wet-trail"); break;   // only the trail of someone who held a victim under, not every swimmer
                case "Water": if (t.Victim != null && t.Desc != null && t.Desc.Contains("시신")) P("cold-water"); break;   // water under a body: it lay somewhere cold/wet
                case "Ash": if (S.Layout.Room(t.Room)?.Type == RoomType.Incinerator) { lines.Add("· 재가 아직 따뜻하다"); P("ash-fresh"); } break;
                case "Scuff": if (t.Note != null && t.Note.StartsWith("edge=")) P("edge-scuff"); break;
            }
            Violence.TraceNotes(sim, ex, t, lines, props);   // --- violence track: bullet holes, drag marks
        }

        // ================================================================== objects
        public static void ItemNotes(Simulation sim, Actor ex, Item it, List<string> lines, List<Prop> props)
        {
            var S = sim.S; string owner = it.Owner != null ? Cast.GivenOf(it.Owner) : null;
            void P(string v, string a = null) => props.Add(new Prop { Kind = PropKind.ItemState, A = a, Item = it.Type, Value = v, Room = it.Room, T0 = S.Clock, T1 = S.Clock });
            if (IsCord(it.Def) && it.Surface.Contains("stretched")) { lines.Add("· 가운데가 늘어나고 꼬여 있다 — 무언가를 세게 조였던 흔적이다. 섬유 사이에 살갗 조각이 끼어 있다"); P("cord-stretched"); }
            if (it.Type == "Button" && it.Surface.Contains("torn") && owner != null) { lines.Add($"· 실밥째 뜯겨 나간 단추다 — {owner}의 재킷 소매 단추와 모양이 같다"); P("torn-button", it.Owner); }
            if (it.Type == "Button" && it.Surface.Contains("burnt") && owner != null) { lines.Add($"· 재 속에서 나온 셔츠 단추다 — {owner}의 셔츠 단추와 모양이 같다"); P("burnt-remnant", it.Owner); }
            if (it.Type == "Pillow" && it.Surface.Contains("pressed")) { lines.Add("· 한가운데에 침과 립밤이 번진 자국이 있다 — 누군가의 얼굴에 세게 눌렸던 흔적이다"); P("pillow-pressed"); }
            if (it.Surface.Contains("residue:sedative")) { lines.Add("· 잔 바닥에 하얀 가루가 덜 녹은 채 남아 있다 — 수면제를 탄 잔이다"); P("sedative-residue"); }
            if (it.Type == "Sedative" && (it.Surface.Contains("used") || it.Surface.Contains("empty"))) { lines.Add(it.Surface.Contains("empty") ? "· 약 봉지가 비어 있다" : "· 봉지 안의 약이 눈에 띄게 줄었다"); P("sedative-used"); }
            if (it.Surface.Contains("poisoned"))
            { lines.Add($"· 안에 쓴 풀 냄새가 나는 녹색 가루가 섞여 있다 — 누군가 미리 독을 넣었다{(owner != null ? $" ({owner}의 것)" : "")}"); P("poisoned-personal", it.Owner); }
            if (it.Note != null && it.NoteFrom != null && it.NoteFrom.StartsWith("forged:"))
            {
                var p = it.NoteFrom.Split(':'); string victim = p.Length > 2 ? p[2] : null; string V = victim != null ? Cast.GivenOf(victim) : "고인";
                lines.Add($"· 유서처럼 보이는 글 — “{it.Note}”");
                P("farewell-note", victim);
                bool keen = ex != null && (ex.IsPlayer || ex.Def.Obs >= 80 || (victim != null && S.K(ex.Id).Examined.Any(e => e.StartsWith("item:") && S.I(e.Substring(5))?.Owner == victim && (S.I(e.Substring(5))?.Type == "Notebook" || S.I(e.Substring(5))?.Type == "Document"))));
                if (keen) { lines.Add($"· {V}의 글씨가 아니다 — 받침을 쓰는 버릇도, 획이 기우는 방향도 다르다. 누군가 흉내 낸 글씨다"); P("handwriting-mismatch", victim); }
                else lines.Add($"· {V}의 글씨처럼 보인다");
            }
            if (it.KeyFor != null && it.Surface.Contains("slid")) { lines.Add("· 주머니나 열쇠걸이가 아니라 문 바로 안쪽 바닥에 떨어져 있었다"); P("key-on-floor", it.Owner); }
            if (it.Surface.Contains("burnt")) { lines.Add(it.Damage >= 3 ? "· 거의 다 타고 남은 조각이다 — 소각로 안에서 나왔다" : "· 불에 그을렸다 — 소각로에 들어갔다 나온 흔적이다"); P("burnt"); }
            if (it.Surface.Contains("waterlogged")) { lines.Add("· 물에 오래 잠겨 있던 듯 불어 있다 — 피는 씻겨 나갔지만 이음새에 붉은 얼룩이 남았다"); P("waterlogged"); }
            if (it.Surface.Contains("soil")) { lines.Add("· 흙투성이다 — 화분대 흙 속에 묻혀 있었다"); P("buried"); }
            if (it.Surface.Contains("shavings")) { lines.Add("· 날 사이에 전선 피복 부스러기가 끼어 있다"); P("insulation-shavings"); }
            if ((it.Def?.Tag == "poison") && it.Surface.Contains("used") && it.Type != "PoisonVial") { lines.Add("· 병 속 내용물이 눈에 띄게 줄었다 — 최근 누군가 덜어 썼다"); P("vial-used"); }
            if (IsSaw(it) && it.Surface.Contains("bonedust")) { lines.Add("· 씻어서 제자리에 돌려놓았지만, 톱니 사이에 하얀 뼛가루와 굳은 핏물이 끼어 있다"); P("bone-dust"); }
            Violence.ItemNotes(sim, ex, it, lines, props);   // --- violence track: guns, bolts, shells, wadding, hair, bindings
            if (it.Def?.Tag == "part")
            {
                string V = owner ?? "누군가";
                if (it.Surface.Contains("burnt")) { lines.Add($"· 재 속에서 나온 뼛조각 — 사람의 {PartKor(it.Note)} 뼈다. 타다 남은 옷감으로 보아 {V}의 것이다"); P("burnt-bone", it.Owner); }
                else { lines.Add($"· 피가 밴 천을 풀자 사람의 {PartKor(it.Note)}이(가) 나왔다 — 톱으로 자른 거친 절단면. 옷자락으로 보아 {V}의 것이다"); P("part-hidden", it.Owner); }
            }
        }

        // ================================================================== furniture and devices
        public static void FurnitureNotes(Simulation sim, Furniture f, ref string desc, List<Prop> props)
        {
            var S = sim.S;
            var shock = S.Traps.FirstOrDefault(t => t.Kind == "Shock" && t.Furniture == f.Id);
            if (shock != null)
            {
                desc += shock.Active ? ". 뒤쪽 전선 피복이 칼로 반듯하게 벗겨져 있고 앞 바닥은 흥건하다 — 손을 대면 위험하다"
                      : shock.FiredAt >= 0 ? ". 전선 피복이 칼로 반듯하게 벗겨져 있었다 — 닳아서 생긴 누전이 아니다" : ". 벗겨진 전선을 누군가 감아 두었다 — 그 전에 피복이 칼로 벗겨져 있었다";
                props.Add(new Prop { Kind = PropKind.TraceAt, Room = f.Room, Item = f.Type, T0 = shock.ArmedAt - 60, T1 = shock.FiredAt >= 0 ? shock.FiredAt : S.Clock, Value = "cable-stripped" });
                props.Add(new Prop { Kind = PropKind.TrapSet, Room = f.Room, Item = f.Type, T0 = S.Ch.ChapterStartClock, T1 = shock.FiredAt >= 0 ? shock.FiredAt : S.Clock, Value = "Shock" });
            }
            if (f.Type == "Incinerator" && S.Flags.TryGetValue("furnace:" + f.Id, out var fired))
            {
                double age = S.Clock - fired;
                desc += age < 120 ? $". 소각로 안쪽이 아직 뜨겁다 — {ClockFmt.Vague(fired, S.Clock)}에 누군가 불을 지폈다" : $". 식은 재가 쌓여 있다 — {ClockFmt.Vague(fired, S.Clock)}에 무언가를 태운 흔적이다";
                props.Add(new Prop { Kind = PropKind.MachineUsed, Room = f.Room, Item = f.Type, T0 = fired - 8, T1 = fired + 8, Value = "소각로 가동" });
                foreach (var it in S.Items.Values.Where(i => i.Room == f.Room && i.Hidden && i.Surface.Contains("burnt")).OrderBy(i => i.Id, StringComparer.Ordinal).ToList())
                { it.Hidden = false; desc += $"\n재 속에서 {it.Kor}이(가) 나왔다"; }
            }
            if (f.Type == "Switchboard")
            {
                var trips = S.Flags.Where(kv => kv.Key.StartsWith("breaker:")).OrderBy(kv => kv.Value).ToList();
                foreach (var kv in trips)
                {
                    int c = int.Parse(kv.Key.Substring(8)); string name = c >= 0 && c < S.Layout.Circuits.Count ? S.Layout.Circuits[c].Name : c.ToString();
                    desc += $"\n차단기 기록 — {ClockFmt.Vague(kv.Value, S.Clock)}에 {name} 회로가 내려갔다";
                    props.Add(new Prop { Kind = PropKind.LightsChanged, Room = f.Room, T0 = kv.Value, T1 = kv.Value, Value = "trip:" + name });
                }
            }
            var dial = f.Marks.LastOrDefault(m => m.StartsWith("출력 다이얼"));
            if (dial != null) { desc += ". " + dial; props.Add(new Prop { Kind = PropKind.MachineUsed, Room = f.Room, Item = f.Type, T0 = S.Clock - 240, T1 = S.Clock, Value = "보일러 최대 출력" }); }
            if (f.Type == "Planter" && f.Marks.Contains("dug")) desc += ". 흙 한쪽을 새로 파헤쳤다가 다시 덮은 자국이 있다";
        }

        /// <summary>A quick sweep of a room also picks up the second-wave objects that are plainly out of place there.</summary>
        public static void RoomQuick(Simulation sim, Actor ex, int room)
        {
            var S = sim.S;
            foreach (var it in S.Items.Values.Where(i => i.Room == room && i.Holder == null && !i.Hidden && (i.Surface.Contains("torn") || i.Surface.Contains("pressed") || i.Surface.Contains("slid") || i.Surface.Contains("residue:sedative") || (i.NoteFrom != null && i.NoteFrom.StartsWith("forged:")) || i.Surface.Contains("stretched"))).OrderBy(i => i.Id, StringComparer.Ordinal).ToList())
                Evidences.ExamineItem(sim, ex, it);
            // what a room sweep finds when something was disposed of there: the filter basket, freshly turned soil, the furnace
            var rr = S.Layout.Room(room);
            if (rr != null && (rr.Type == RoomType.WaterRoom || rr.Type == RoomType.Pool))
                foreach (var it in S.Items.Values.Where(i => i.Room == room && i.Holder == null && i.Surface.Contains("waterlogged")).OrderBy(i => i.Id, StringComparer.Ordinal).ToList()) { it.Hidden = false; Evidences.ExamineItem(sim, ex, it); }
            if (rr != null && S.Traces.Any(t => t.Room == room && t.Type == "Soil" && !t.Cleaned))
                foreach (var it in S.Items.Values.Where(i => i.Room == room && i.Holder == null && i.Surface.Contains("soil")).OrderBy(i => i.Id, StringComparer.Ordinal).ToList()) { it.Hidden = false; Evidences.ExamineItem(sim, ex, it); }
            foreach (var fid in S.Layout.Room(room)?.Furniture ?? new List<int>())
            {
                var f = S.Layout.Furniture[fid];
                bool device = f.Type == "Incinerator" && S.Flags.ContainsKey("furnace:" + f.Id) || S.Traps.Any(t => t.Kind == "Shock" && t.Furniture == fid && (t.FiredAt >= 0 || t.DisarmedAt >= 0)) || f.Type == "Switchboard" && S.Flags.Keys.Any(k => k.StartsWith("breaker:"));
                if (!device) continue;
                string desc = FurnitureCatalog.Get(f.Type)?.Kor ?? f.Type; var props = new List<Prop>();
                FurnitureNotes(sim, f, ref desc, props);
                Evidences.Add(sim, ex.Id, EvKind.Record, $"{FurnitureCatalog.Get(f.Type)?.Kor ?? f.Type} ({S.RoomName(room)})", desc, "직접 조사", "mdev:" + fid + ":" + (int)(S.Clock / 30), S.Clock, S.Clock, room, "장치에 남은 기록", "장치에 남지 않은 일", true, props.ToArray());
            }
            if (rr != null && rr.Type == RoomType.Incinerator)   // the furnace, once opened, gives up what did not burn
                foreach (var it in S.Items.Values.Where(i => i.Room == room && i.Holder == null && !i.Hidden && i.Surface.Contains("burnt")).OrderBy(i => i.Id, StringComparer.Ordinal).ToList()) Evidences.ExamineItem(sim, ex, it);
        }

        // ================================================================== the reveal: captions and names
        public static string Caption(GameState S, Incident inc, LedgerEvent e)
        {
            string A = Cast.GivenOf(e.Actor), T = e.Target != null ? Cast.GivenOf(e.Target) : "", R = S.RoomName(e.Room), I = S.I(e.Item)?.Kor ?? ItemCatalog.Get(e.Data)?.Kor ?? "물건";
            switch (e.Type)
            {
                case "Garrote": return $"{A}, {R}에서 {T}의 등 뒤로 다가가 {I}을(를) 목에 감는다";
                case "Scratched": return $"  └ {T}의 손톱이 {A}의 손등을 할퀸다";
                case "Shove": { var p = (e.Data ?? "").Split('|'); return p[0] == "rail" ? $"{A}, 회랑 난간에 기댄 {T}을(를) 힘껏 민다 — {T}은(는) 난간 너머 {(p.Length > 1 ? p[1] : "아래")}(으)로 떨어진다" : $"{A}, 계단 맨 윗단에 선 {T}을(를) 떠민다 — {T}은(는) 계단 아래로 굴러떨어진다"; }
                case "ButtonTorn": return $"  └ 실랑이하다 {A}의 소매 단추 하나가 뜯겨 바닥에 구른다";
                case "ShoveFailed": return $"{A}, {T}을(를) 밀어 떨어뜨리려다 실패한다 — {T}은(는) 난간을 붙잡고 버티며 상대의 얼굴을 똑똑히 본다";
                case "Sedate": return $"{A}, {R}의 식탁에서 {T}의 잔에 수면제를 몰래 녹인다";
                case "Drowsy": return $"{A}, 쏟아지는 졸음에 비틀거리며 방으로 향한다";
                case "Doze": return $"{A}, 방문도 잠그지 못한 채 쓰러지듯 잠든다";
                case "Smother": return e.Data == "bed" ? $"{A}, 잠든 {T}의 얼굴을 베개로 짓누른다" : $"{A}, 잠든 {T}의 얼굴을 쿠션으로 짓누른다";
                case "FakeNote": return $"{A}, {T}의 글씨를 흉내 낸 유서를 곁에 남긴다 — “{e.Data}”";
                case "KeySlide": return $"{A}, {T}의 열쇠로 밖에서 문을 잠그고 — 열쇠를 문 아래 틈으로 밀어 방 안에 되돌려 놓는다";
                case "PoisonPlant": { var p = (e.Data ?? "").Split('|'); return p.Contains("gift") ? $"{A}, 아무도 없는 {R}에 들어가 독을 섞은 {p[0]}을(를) 선물처럼 책상에 두고 나온다" : $"{A}, 아무도 없는 {R}에 들어가 {T}의 {p[0]}에 독을 섞는다"; }
                case "PoisonTaken": return $"{T}, 스스로 잠근 방 안에서 잠들기 전 {e.Data}을(를) 마신다 — 독이 든 줄도 모른 채";
                case "ShockRigged": return $"{A}, {R}의 {FurnitureCatalog.Get(e.Data)?.Kor ?? "기계"} 뒤 전선 피복을 벗기고 앞 바닥에 물을 붓는다";
                case "ShockFired": return $"{T}, {FurnitureCatalog.Get(e.Data)?.Kor ?? "기계"}에 손을 대는 순간 감전된다 — 차단기가 내려가고 방이 어두워진다";
                case "BreakerTrip": return null;
                case "Burn": return $"{A}, 소각실 소각로에 {(string.IsNullOrEmpty(e.Data) ? "증거" : e.Data)}을(를) 던져 넣는다 — 불길이 치솟는다";
                case "DumpWater": return $"{A}, {I}을(를) 수영장 물속에 떨어뜨린다 — 물살에 휩쓸려 여과기 쪽으로 끌려간다";
                case "Bury": return $"{A}, 온실 화분대의 흙을 파고 {I}을(를) 묻는다";
                case "NoiseMask": return $"{A}, 보일러 출력을 끝까지 올린다 — 요란한 굉음이 이 방의 모든 소리를 덮는다";
                case "HeldUnder": return $"  └ {A}의 소매가 흠뻑 젖는다";
                case "ColdHide": return $"{A}, {T}의 시신을 저온 보관실에 눕혀 둔다 — 몸은 빠르게 식는다";
                case "Dismember": return $"{A}, {R}에서 {T}의 시신을 톱으로 해체한다 — 절단면에서는 피가 거의 흐르지 않는다";
                case "PartHidden": return e.Data == "burn" ? $"{A}, 천에 싼 꾸러미 하나를 소각로에 밀어 넣는다" : e.Data == "water" ? $"{A}, 천에 싼 꾸러미 하나를 수영장 물속에 가라앉힌다" : e.Data == "soil" ? $"{A}, 천에 싼 꾸러미 하나를 {R} 화분대 흙 깊이 묻는다" : $"{A}, 천에 싼 꾸러미 하나를 {R} 상자 안쪽 깊숙이 밀어 넣는다";
                case "PieceSeen": return $"{A}, {R}에서 피가 밴 천 꾸러미를 발견한다";
            }
            return Violence.Caption(S, inc, e);   // --- violence track: holds, shots, bindings (null when none)
        }

        public static string GrammarKor(string g)
        {
            switch (g)
            {
                case "Strangle": return "등 뒤에서 끈으로 목 조르기"; case "Push": return "높은 곳에서 밀어 떨어뜨리기(사고처럼 꾸밈)"; case "Shock": return "기계에 걸어 둔 감전 함정(사고처럼 꾸밈)";
                case "Smother": return "수면제로 재운 뒤 베개로 숨 막기(자연사처럼 꾸밈)"; case "Bedtime": return "잠들기 전 마실 것에 미리 넣어 둔 독";
                case "KeySlide": return "열쇠를 문틈으로 밀어 넣어 만든 밀실"; case "FakeNote": return "가짜 유서(자살처럼 꾸밈)"; case "Burn": return "소각로에 증거 태우기";
                case "Dismember": return "시신을 톱으로 잘라 여러 곳에 나눠 숨기기";
                case "Dump": return "흉기를 수영장 물속에 버리기"; case "Bury": return "흉기를 온실 흙에 묻기"; case "Noise": return "보일러 소음으로 소리 덮기"; case "ColdHide": return "시신을 저온 보관실에 숨기기";
            }
            return Violence.GrammarKor(g) ?? Initiative.GrammarKor(g);   // --- violence track: Shoot / Bind · initiative: scheme heads and layers (Sim/Murder)
        }

        public static (string conn, string fin)? Verb(string g)
        {
            switch (g)
            {
                case "Strangle": return ("등 뒤에서 끈으로 목을 조르고", "등 뒤에서 끈으로 목을 조른다");
                case "Push": return ("높은 곳 가장자리에 선 순간을 노려 밀어 떨어뜨리고", "높은 곳 가장자리에 선 순간을 노려 밀어 떨어뜨린다");
                case "Shock": return ("상대가 늘 쓰는 기계에 감전 함정을 걸어 두고", "상대가 늘 쓰는 기계에 감전 함정을 걸어 둔다");
                case "Smother": return ("수면제로 잠재운 뒤 베개로 숨을 막고", "수면제로 잠재운 뒤 베개로 숨을 막는다");
                case "Bedtime": return ("잠들기 전 마실 것에 미리 독을 넣어 두고", "잠들기 전 마실 것에 미리 독을 넣어 둔다");
                case "KeySlide": return ("열쇠를 문틈으로 되돌려 방을 밀실로 만들고", "열쇠를 문틈으로 되돌려 방을 밀실로 만든다");
                case "FakeNote": return ("가짜 유서로 자살처럼 꾸미고", "가짜 유서로 자살처럼 꾸민다");
                case "Burn": return ("흉기와 옷을 소각로에 태우고", "흉기와 옷을 소각로에 태운다");
                case "Dump": return ("흉기를 수영장 물속에 버리고", "흉기를 수영장 물속에 버린다");
                case "Bury": return ("흉기를 온실 흙 속에 묻고", "흉기를 온실 흙 속에 묻는다");
                case "Noise": return ("보일러 굉음으로 소리를 덮고", "보일러 굉음으로 소리를 덮는다");
                case "ColdHide": return ("시신을 저온 보관실에 숨기고", "시신을 저온 보관실에 숨긴다");
                case "Dismember": return ("시신을 톱으로 잘라 여러 곳에 나눠 숨기고", "시신을 톱으로 잘라 여러 곳에 나눠 숨긴다");
            }
            return Violence.Verb(g) ?? Initiative.Verb(g);   // --- violence track: Shoot / Bind · initiative: scheme heads and layers (Sim/Murder)
        }
    }
}
