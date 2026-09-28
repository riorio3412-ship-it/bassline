using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    public sealed class RuleDef { public string Id; public string Name; public string Kind; public bool Major; public int MinN; public int MinChapter = 1; public string Desc; }

    /// <summary>Special chapter rules. Selection reads only population/facilities/history — never hidden plans or culprits.</summary>
    public static class Rules
    {
        public static readonly List<RuleDef> Catalog = new List<RuleDef>
        {
            new RuleDef { Id = "CH01", Name = "공개 표결", Kind = "절차", MinN = 5, Desc = "이번 심판에서는 투표가 끝난 뒤 누가 누구를 찍었는지 공개합니다." },
            new RuleDef { Id = "CH02", Name = "양익 생활", Kind = "환경", Major = true, MinN = 8, Desc = "두 조로 나뉘어 저택의 동쪽 날개와 서쪽 날개에서 따로 지냅니다. 식당과 구급실, 심판 소집과 수사 때는 예외입니다." },
            new RuleDef { Id = "CH03", Name = "정기 소등", Kind = "환경", MinN = 7, Desc = "미리 알린 두 시각에 저택 조명이 1분씩 꺼집니다. 비상등은 켜 둡니다." },
            new RuleDef { Id = "CH04", Name = "최초 진술 봉인", Kind = "정보", MinN = 5, Desc = "각자 처음 한 진술은 한 글자도 바뀌지 않고 남습니다. 말을 고치면 고친 진술이 따로 기록됩니다." },
            new RuleDef { Id = "CH05", Name = "두 계약 공개", Kind = "정보", MinN = 5, Desc = "두 사람이 계약으로 약속받은 보상이 한 번 공개됩니다." },
            new RuleDef { Id = "CH06", Name = "과거의 봉투", Kind = "관계", Major = true, MinN = 6, Desc = "누군가의 과거가 적힌 봉투가 몇 사람의 방에 배달됩니다. 열어 보기 전에는 무슨 내용인지 모릅니다." },
            new RuleDef { Id = "CH07", Name = "제한 대여", Kind = "관계", MinN = 6, Desc = "공용 장비 두 가지를 한 번에 20분까지 빌릴 수 있습니다." },
            new RuleDef { Id = "CH08", Name = "공동 점검", Kind = "환경", MinN = 5, Desc = "알려 드린 시설을 두 사람씩 함께 점검해 보시길 권합니다. 빠지셔도 불이익은 없습니다." },
            new RuleDef { Id = "CH09", Name = "정산의 기한", Kind = "계약", Major = true, MinN = 8, MinChapter = 3, Desc = "뽑힌 계약자 한 사람에게만, 이번 정산이 지나면 보상이 사라진다는 통지가 갑니다." },
            new RuleDef { Id = "CH10", Name = "공개 질의", Kind = "절차", MinN = 5, Desc = "수사가 끝나 갈 무렵, 모두 모여 각자 어디에 있었는지 밝히는 자리가 한 번 열립니다." },
            new RuleDef { Id = "CH11", Name = "공용품 교환회", Kind = "관계", MinN = 6, Desc = "미리 알린 시각과 장소에서 물건 교환회가 한 번 열립니다." },
            new RuleDef { Id = "CH12", Name = "이동식 전시", Kind = "환경", Major = true, MinN = 6, Desc = "전시품과 받침대의 자리가 한 번 바뀝니다." },
            new RuleDef { Id = "CH13", Name = "교대 열쇠", Kind = "관계", MinN = 6, Desc = "공용실 한 곳의 열쇠를 담당자 두 사람이 번갈아 맡습니다." },
            new RuleDef { Id = "CH14", Name = "분산 열람", Kind = "정보", Major = true, MinN = 7, Desc = "공용 기록을 볼 수 있는 창구가 두 곳으로 나뉩니다." },
            new RuleDef { Id = "CH15", Name = "공동 식탁", Kind = "관계", MinN = 6, Desc = "다음 식사 때 함께 앉을 사람을 짝지어 권해 드립니다. 거절하셔도 됩니다." },
            new RuleDef { Id = "CH16", Name = "공개 정비 시간", Kind = "환경", MinN = 6, Desc = "꼭 필요하지 않은 시설 몇 곳이 정해진 시간 동안 정비로 문을 닫습니다." },
            new RuleDef { Id = "CH17", Name = "자료 보관 담당", Kind = "정보", MinN = 6, Desc = "생활 기록과 대여표를 정리할 담당자 두 명이 정해집니다." },
            new RuleDef { Id = "CH18", Name = "반론 우선권", Kind = "절차", MinN = 5, Desc = "누군가 새로 지목되면, 지목된 사람의 반론부터 한 번 먼저 듣습니다." },
            new RuleDef { Id = "CH19", Name = "전해 들은 말 가리기", Kind = "절차", MinN = 5, Desc = "심판에서는 중요한 말을 처음 누가 했는지, 직접 본 건지 전해 들은 건지부터 따집니다." },
            new RuleDef { Id = "CH20", Name = "시간차 공지", Kind = "정보", Major = true, MinN = 7, Desc = "행사 안내 하나가 저택 두 곳에 시간 차를 두고 전해집니다." },
            new RuleDef { Id = "CH21", Name = "세 번째 빈자리", Kind = "사건", Major = true, MinN = 8, MinChapter = 2, Desc = "이번에는 희생자가 두 명이 아니라 세 명까지 나올 수 있습니다. 세 명을 죽이라는 명령은 아닙니다." },
            new RuleDef { Id = "CH22", Name = "잔향의 저택", Kind = "환경", Major = true, MinN = 7, Desc = "저택 곳곳에서 정체 모를 소음이 이어집니다. 소리가 어디서 나는지 가늠하기 어려워집니다." },
            new RuleDef { Id = "CH23", Name = "긴 암흑", Kind = "환경", Major = true, MinN = 7, Desc = "이번 사건이 끝날 때까지 저택의 불이 대부분 꺼집니다. 모두에게 손전등을 나눠 드리며, 그 불빛은 다른 사람 눈에도 띕니다." },
        };
        static readonly string[][] Forbidden = { new[] { "CH02", "CH03" }, new[] { "CH05", "CH06" }, new[] { "CH06", "CH09" }, new[] { "CH03", "CH23" } };

        public static string ForceRule;
        public static void SelectForChapter(Simulation sim)
        {
            var S = sim.S; var rng = S.R(Stream.ChapterRule);
            int n = S.Ch.StartN; int c = S.Chapter;
            int max = c == 1 ? 0 : c == 2 ? 1 : 2;
            // rest chapters exist: sometimes nothing extra
            if (max > 0 && rng.Chance(0.2)) max = 0;
            var recent = S.Flags.Where(kv => kv.Key.StartsWith("rulehist:") && kv.Value >= c - 1).Select(kv => kv.Key.Split(':')[1]).ToHashSet();
            var chosen = new List<RuleDef>();
            // test hook: force a rule (headless verification only)
            if (ForceRule != null && c >= 2) { var fr = Catalog.FirstOrDefault(x => x.Id == ForceRule); if (fr != null) chosen.Add(fr); }
            var pool = Catalog.Where(r => n >= r.MinN && c >= r.MinChapter && !recent.Contains(r.Id)).ToList();
            rng.Shuffle(pool);
            foreach (var r in pool)
            {
                if (chosen.Count >= max) break;
                if (r.Major && chosen.Any(x => x.Major)) continue;
                if (chosen.Any(x => x.Kind == r.Kind)) continue;
                if (chosen.Any(x => Forbidden.Any(f => f.Contains(x.Id) && f.Contains(r.Id)))) continue;
                if ((r.Id == "CH22" || r.Id == "CH23") && chosen.Any(x => x.Kind == "환경")) continue;
                // weights: new special rules are rarer so they feel special
                double w = r.Id == "CH21" ? 0.35 : r.Id == "CH22" || r.Id == "CH23" ? 0.5 : (r.Id == "CH05" || r.Id == "CH06" || r.Id == "CH09") ? 1.0 : 0.75;
                if (!rng.Chance(w)) continue;
                chosen.Add(r);
            }
            foreach (var r in chosen)
            {
                var inst = new RuleInstance { Id = S.NewId("rule"), Rule = r.Id, Chapter = c, Name = r.Name, Desc = r.Desc, Major = r.Major, Kind = r.Kind };
                S.Ch.Rules.Add(inst); S.Flags["rulehist:" + r.Id] = c;
                Setup(sim, inst, rng);
            }
            // announce at chapter start (heard by everyone present)
            foreach (var inst in S.Ch.Rules) { sim.Announce("y_rule_" + inst.Rule, RuleSlots(S, inst), inst.Rule); inst.Announced = true; S.Emit(GameEventType.RuleStart, data: inst.Rule, text: inst.Name + " — " + inst.Desc); }
        }

        static Dictionary<string, string> RuleSlots(GameState S, RuleInstance r)
        {
            var d = new Dictionary<string, string>();
            if (r.Times.Count > 0) d["time"] = string.Join(", ", r.Times.Select(t => ClockFmt.Vague(t)));
            if (r.Targets.Count > 0) d["t"] = string.Join("·", r.Targets.Select(Cast.GivenOf));
            if (r.Data != null) d["place"] = r.Data;
            d["n"] = r.Param.ToString();
            return d;
        }

        static void Setup(Simulation sim, RuleInstance r, Rng rng)
        {
            var S = sim.S; var living = S.Living.Select(a => a.Id).ToList();
            double day0 = Math.Floor(S.Clock / 1440) * 1440;
            switch (r.Rule)
            {
                case "CH02":
                    {
                        var hall = S.Layout.Rooms.First(x => x.Type == RoomType.GrandHall && x.Floor == 0);
                        foreach (var id in living) { var bed = S.Layout.BedroomOf(id); r.Targets.Add(id + (bed != null && bed.Rect.CX < hall.Rect.CX ? ":W" : ":E")); }
                        break;
                    }
                case "CH03": r.Times.Add(day0 + 14 * 60 + rng.R(0, 60)); r.Times.Add(day0 + 20 * 60 + rng.R(0, 90)); break;
                case "CH05":
                    { var two = living.Where(x => x != Cast.Player).OrderBy(_ => rng.F()).Take(2).ToList(); r.Targets.AddRange(two); foreach (var a in S.Actors.Values.Where(a => a.Alive)) foreach (var t in two) S.K(a.Id).Facts.Add("contract:" + t); break; }
                case "CH06":
                    {
                        // envelopes: a true past fact about one participant, physically delivered to another's room
                        var subjects = living.Where(x => x != Cast.Player && !string.IsNullOrEmpty(Cast.Get(x).Secret)).OrderBy(_ => rng.F()).Take(3).ToList();
                        foreach (var subj in subjects)
                        {
                            var recipient = living.Where(x => x != subj).OrderBy(_ => rng.F()).First();
                            var bed = S.Layout.BedroomOf(recipient); if (bed == null) continue;
                            var env = new Item { Id = S.NewId("it"), Type = "Envelope", Name = Cast.GivenOf(recipient) + " 앞 봉투", Owner = recipient, Pos = sim.RandomPointIn(bed, rng), Room = bed.Id, KeyFor = null };
                            env.Surface.Add("secret:" + subj); S.Items[env.Id] = env; S.Emit(GameEventType.ItemMoved, null, data: env.Id, text: "spawn", pos: env.Pos);
                            r.Targets.Add(recipient + ">" + subj);
                            // NPC recipients read their envelope when they next rest in their room
                            if (recipient != Cast.Player) { S.K(recipient).Facts.Add("secret:" + subj); S.K(recipient).Facts.Add("knows-secret-of:" + subj); S.K(subj).Facts.Add("envelope-about-me"); Relations.Change(S, recipient, subj, trust: -0.1f, respect: -0.05f, memory: "봉투를 읽고 그 사람의 과거를 알게 됐다"); Relations.Change(S, subj, recipient, fear: 0.15f); S.K(subj).Facts.Add("knows-my-secret:" + recipient); }
                        }
                        break;
                    }
                case "CH07":
                    {
                        // two pieces of public equipment at the butler's desk, 20 minutes each, with a written lending sheet
                        r.Data = "집사 진행대"; var hall = S.Layout.Rooms.First(x => x.Type == RoomType.GrandHall && x.Floor == 0);
                        foreach (var type in new[] { "Camera", "Recorder" })
                        {
                            var it = new Item { Id = S.NewId("it"), Type = type, Name = "공용 " + ItemCatalog.Get(type).Kor, Owner = Cast.Butler, Pos = sim.RandomPointIn(hall, rng), Room = hall.Id, HomeRoom = hall.Id };
                            it.HomePos = it.Pos; S.Items[it.Id] = it; S.Emit(GameEventType.ItemMoved, null, data: it.Id, text: "spawn", pos: it.Pos); r.Targets.Add(it.Id);
                        }
                        var sheet = new Item { Id = S.NewId("it"), Type = "Document", Name = "대여표", Owner = Cast.Butler, Pos = sim.RandomPointIn(hall, rng), Room = hall.Id, HomeRoom = hall.Id, Note = "대여표 (공용 장비 · 20분)" };
                        S.Items[sheet.Id] = sheet; S.Emit(GameEventType.ItemMoved, null, data: sheet.Id, text: "spawn", pos: sheet.Pos); r.Targets.Add("sheet:" + sheet.Id);
                        break;
                    }
                case "CH09":
                    { var t = living.Where(x => x != Cast.Player).OrderBy(_ => rng.F()).First(); r.Targets.Add(t); S.K(t).Facts.Add("deadline:" + S.Chapter); break; }
                case "CH11": r.Times.Add(day0 + 15 * 60); r.Data = S.RoomName(S.Layout.First(RoomType.Lounge)?.Id ?? 0); break;
                case "CH12":
                    {
                        var ped = S.Layout.Furniture.Where(f => f.Type == "Pedestal").OrderBy(_ => rng.F()).Take(3).ToList();
                        foreach (var f in ped) { var room = S.Layout.Room(f.Room); var np = sim.RandomPointIn(room, rng); f.Pos = new P3(f.Pos.f, np.x, np.z); f.Moved = true; f.Marks.Add("옮기다 긁힌 자국"); S.Emit(GameEventType.Furniture, Cast.Butler, id: f.Id, pos: f.Pos); }
                        S.Layout.InvalidateNav(); break;
                    }
                case "CH13":
                    {
                        var room = S.Layout.First(RoomType.Archive) ?? S.Layout.First(RoomType.Library); var two = living.Where(x => x != Cast.Player).OrderBy(_ => rng.F()).Take(2).ToList();
                        r.Targets.AddRange(two); r.Data = room?.Name;
                        if (room != null && room.Doors.Count > 0)
                        {
                            var d = S.Layout.Doors[room.Doors[0]]; d.Lockable = true; d.Locked = true; d.KeyId = "key_shift" + S.Chapter;
                            var key = new Item { Id = S.NewId("it"), Type = "RoomKey", Name = room.Name + " 교대 열쇠", KeyFor = d.KeyId, Holder = two[0] }; S.Items[key.Id] = key; S.A(two[0]).Pocket.Add(key.Id);
                        }
                        break;
                    }
                case "CH15":
                    {
                        r.Times.Add(day0 + 18 * 60 + 30);
                        var pool = living.Where(x => x != Cast.Butler).OrderBy(_ => rng.F()).ToList();
                        for (int i = 0; i + 1 < pool.Count; i += 2) r.Targets.Add(pool[i] + "+" + pool[i + 1]);
                        break;
                    }
                case "CH16": r.Times.Add(day0 + 13 * 60); r.Data = "기계실·수질 관리실"; break;
                case "CH17": { var two = living.Where(x => x != Cast.Player).OrderBy(_ => rng.F()).Take(2).ToList(); r.Targets.AddRange(two); break; }
                case "CH20":
                    {
                        // one non-essential event, announced on the west side first and on the east side 40 minutes later
                        r.Times.Add(day0 + 12 * 60); r.Times.Add(day0 + 12 * 60 + 40);
                        var room = S.Layout.First(RoomType.Greenhouse) ?? S.Layout.First(RoomType.Lounge);
                        if (room != null)
                        {
                            var g = new Gathering { Id = S.NewId("gath"), Host = Cast.Butler, Kind = "notice", Label = "정원 음악회" };
                            g.Revs.Add(new GatheringRev { Room = room.Id, Start = day0 + 15 * 60, End = day0 + 16 * 60, At = S.Clock, Why = "CH20 공지" });
                            foreach (var id in living.Where(x => x != Cast.Butler)) { g.Status[id] = "unaware"; g.KnownRev[id] = -1; g.Channel[id] = "voice"; }
                            g.Status[Cast.Butler] = "host"; g.KnownRev[Cast.Butler] = 0; S.Flags["grevcheck:" + g.Id] = 1;
                            S.Gatherings.Add(g); r.Data = g.Id;
                        }
                        break;
                    }
                case "CH21": break;
                case "CH22": S.Noise = 0.55f; break;
                case "CH23":
                    {
                        S.Darkness = 0.88f;
                        foreach (var id in living)
                        {
                            var fl = new Item { Id = S.NewId("it"), Type = "Flashlight", Name = Cast.GivenOf(id) + "의 손전등", Holder = id, Owner = id };
                            S.Items[fl.Id] = fl; var a = S.A(id); if (a.HandL == null) a.HandL = fl.Id; else a.Pocket.Add(fl.Id);
                            S.Flags["light:" + fl.Id] = id == Cast.Player ? 0 : 1;
                        }
                        break;
                    }
            }
        }

        /// <summary>Per clock minute rule effects.</summary>
        public static void Tick(Simulation sim, int minuteOfDay)
        {
            var S = sim.S;
            foreach (var r in S.Ch.Rules.Where(r => r.Active).ToList())
            {
                switch (r.Rule)
                {
                    case "CH03":
                        foreach (var t in r.Times)
                        {
                            if (S.Phase != Phase.Daily) break;
                            if (Math.Abs(S.Clock - t) < 0.6 && !S.Flags.ContainsKey("ch03on:" + t)) { S.Flags["ch03on:" + t] = 1; for (int c = 1; c < 8; c++) sim.SetCircuit(c, false, null); S.Emit(GameEventType.Notice, text: "정기 소등으로 불이 꺼졌다", key: "blackout"); }
                            if (S.Clock >= t + 30 && S.Flags.ContainsKey("ch03on:" + t) && !S.Flags.ContainsKey("ch03off:" + t)) { S.Flags["ch03off:" + t] = 1; for (int c = 1; c < 8; c++) sim.SetCircuit(c, true, null); }
                        }
                        break;
                    case "CH13":
                        // shift change at 14:00 and 22:00: the key physically passes from one keeper to the other
                        if ((minuteOfDay == 14 * 60 || minuteOfDay == 22 * 60) && r.Targets.Count >= 2 && S.Phase == Phase.Daily)
                        {
                            var key = S.Items.Values.FirstOrDefault(i => i.KeyFor == "key_shift" + r.Chapter); var holder = key != null ? S.A(key.Holder) : null;
                            var next = holder != null ? S.A(r.Targets.First(x => x != holder.Id)) : null;
                            if (key != null && holder != null && next != null && holder.Alive && next.Alive && !holder.IsPlayer)
                            {
                                var act = new Activity { Id = "rule:CH13", Label = "열쇠 교대", Priority = 2.5 };
                                act.Steps.Add(new ActionStep { Kind = "Talk", Actor = next.Id }); act.Steps.Add(new ActionStep { Kind = "X_Hand", Item = key.Id, Actor = next.Id, Tag = "shiftkey" });
                                sim.Assign(holder, act); S.Log("KeyShift", holder.Id, next.Id, item: key.Id, data: "CH13");
                            }
                        }
                        break;
                    case "CH10":
                        if (S.Phase == Phase.Investigation && S.Ch.InvestigationEnd > 0 && S.Clock >= S.Ch.InvestigationEnd - 40 && !S.Flags.ContainsKey("ch10done:" + S.Chapter))
                        {
                            S.Flags["ch10done:" + S.Chapter] = 1;
                            sim.Announce("y_rule_CH10_now", null, "CH10");
                            // everyone states where they were, in front of everyone (the statements, lies included, become shared hearsay)
                            foreach (var npc in S.LivingNpcs.OrderBy(x => x.Id).ToList())
                            {
                                var w = Testimony.Where(sim, npc, null); if (w.Prop == null) continue;
                                string text = sim.Render(npc.Id, null, w.Key, w.Slots) ?? "";
                                foreach (var o in S.Living.Where(o => o != npc)) sim.Learn(o.Id, npc.Id, w.Prop, text, w.Lie, false);
                            }
                            S.Log("PublicInquiry", Cast.Butler, data: "CH10");
                        }
                        break;
                    case "CH20":
                        {
                            var g = S.Gatherings.FirstOrDefault(x => x.Id == r.Data); if (g == null) break;
                            var hall = S.Layout.Rooms.First(x => x.Type == RoomType.GrandHall && x.Floor == 0);
                            for (int i = 0; i < r.Times.Count; i++)
                            {
                                if (S.Clock < r.Times[i] || S.Flags.ContainsKey($"ch20:{S.Chapter}:{i}")) continue;
                                S.Flags[$"ch20:{S.Chapter}:{i}"] = 1; bool west = i == 0;
                                // the notice is heard only by those currently on that side of the house
                                foreach (var a in S.Living.Where(a => a.Id != Cast.Butler && a.Pose != Pose.Sleep))
                                {
                                    var ar = S.Layout.Room(a.Room); if (ar == null) continue;
                                    if ((ar.Rect.CX < hall.Rect.CX) == west) Grammars.Tell(sim, g, a, "voice");
                                }
                                S.Log("NoticeDelivered", Cast.Butler, data: $"{g.Id} {(west ? "west" : "east")}");
                                S.Emit(GameEventType.Notice, null, text: $"{(west ? "서쪽" : "동쪽")} 스피커에서 안내 방송 — {g.Label}, {S.RoomName(g.Cur.Room)}, {ClockFmt.Vague(g.Cur.Start)}부터", key: "notice");
                            }
                            break;
                        }
                    case "CH22":
                        // periodic swells: the noise isn't timed to anything hidden
                        S.Noise = 0.35f + 0.3f * (float)Math.Abs(Math.Sin(S.Clock / 7.3)) ;
                        if (minuteOfDay % 9 == 0) S.Emit(GameEventType.Sound, text: "Noise", value: S.Noise);
                        break;
                }
            }
        }

        public static void ExpireChapter(Simulation sim)
        {
            var S = sim.S;
            foreach (var r in S.Ch.Rules.Where(r => r.Active))
            {
                r.Active = false;
                if (r.Rule == "CH22") S.Noise = 0;
                if (r.Rule == "CH23") S.Darkness = 0;
                if (r.Rule == "CH03") for (int c = 1; c < 8; c++) sim.SetCircuit(c, true, null);
                S.Emit(GameEventType.RuleEnd, data: r.Rule, text: r.Name);
            }
            // physical consequences stay: broken bulbs, moved exhibits, notes, relationships, knowledge
        }

        /// <summary>CH14: the official confirmation is readable only at two separate windows (half each). NPCs get the half
        /// of the window nearer to them; the player has to go and read both documents.</summary>
        public static void SplitOfficialFile(Simulation sim, Actor a, Incident inc)
        {
            var S = sim.S; var rA = S.Layout.First(RoomType.Library) ?? S.Layout.First(RoomType.Study); var rB = S.Layout.First(RoomType.Archive) ?? S.Layout.First(RoomType.Lounge);
            if (rA == null || rB == null) return;
            if (!S.Flags.ContainsKey("ch14doc:" + inc.Id))
            {
                S.Flags["ch14doc:" + inc.Id] = 1;
                foreach (var (room, half) in new[] { (rA, "A"), (rB, "B") })
                {
                    var doc = new Item { Id = S.NewId("it"), Type = "Document", Name = $"공식 기록 열람본 {half}", Owner = Cast.Butler, Pos = sim.RandomPointIn(room, S.R(Stream.Layout)), Room = room.Id, HomeRoom = room.Id };
                    doc.HomePos = doc.Pos; doc.Surface.Add($"official:{inc.Id}:{half}");
                    doc.Note = half == "A" ? $"{Cast.NameOf(inc.Victim)} — 발견된 곳: {S.RoomName(inc.FoundRoom)}, 죽음이 확인된 시각: {ClockFmt.Vague(inc.ConfirmClock)}" : $"{Cast.NameOf(inc.Victim)} — 공식 기록상 숨졌을 수 있는 시간: 생활이 시작된 {ClockFmt.Vague(S.Ch.ChapterStartClock)}부터 죽음이 확인될 때까지";
                    S.Items[doc.Id] = doc; S.Emit(GameEventType.ItemMoved, null, data: doc.Id, text: "spawn", pos: doc.Pos);
                }
            }
            if (a.IsPlayer) return;
            bool nearA = a.Pos.Dist(new P3(rA.Floor, rA.Rect.CX, rA.Rect.CZ)) <= a.Pos.Dist(new P3(rB.Floor, rB.Rect.CX, rB.Rect.CZ));
            OfficialHalf(sim, a, inc, nearA ? "A" : "B");
        }

        public static void OfficialHalf(Simulation sim, Actor a, Incident inc, string half)
        {
            var S = sim.S;
            if (half == "A") Evidences.Add(sim, a.Id, EvKind.Document, $"공식 기록 열람본 A — {Cast.NameOf(inc.Victim)}", $"발견된 곳은 {S.RoomName(inc.FoundRoom)}, 죽음이 확인된 건 {ClockFmt.Vague(inc.ConfirmClock)}.", "열람 창구 A", "official:" + inc.Id + ":A", inc.ConfirmClock, inc.ConfirmClock, inc.FoundRoom, "발견된 곳과 죽음이 확인된 시각", "숨졌을 수 있는 시간(다른 창구에 있다)", true,
                new Prop { Kind = PropKind.FoundPlace, A = inc.Victim, Room = inc.FoundRoom, T0 = inc.ConfirmClock, T1 = inc.ConfirmClock });
            else Evidences.Add(sim, a.Id, EvKind.Document, $"공식 기록 열람본 B — {Cast.NameOf(inc.Victim)}", $"공식 기록상 숨졌을 수 있는 시간: {ClockFmt.Vague(S.Ch.ChapterStartClock)} ~ {ClockFmt.Vague(inc.ConfirmClock)}", "열람 창구 B", "official:" + inc.Id + ":B", S.Ch.ChapterStartClock, inc.ConfirmClock, inc.FoundRoom, "공식 기록상 숨졌을 수 있는 시간", "발견된 곳(다른 창구에 있다)", true,
                new Prop { Kind = PropKind.DeathWindow, A = inc.Victim, T0 = S.Ch.ChapterStartClock, T1 = inc.ConfirmClock, Value = "official" });
        }

        /// <summary>CH15: suggested dinner partner (null if none or already refused).</summary>
        public static string TablePartner(GameState S, string id)
        {
            var r = S.Rule("CH15"); if (r == null) return null;
            var pair = r.Targets.FirstOrDefault(x => x.Split('+').Contains(id)); if (pair == null) return null;
            return pair.Split('+').First(x => x != id);
        }

        public static bool RoomBlocked(Simulation sim, Actor a, Room r)
        {
            var S = sim.S;
            var ch02 = S.Rule("CH02");
            if (ch02 != null && S.Phase == Phase.Daily && !RoomInfo.IsPassage(r.Type) && r.Type != RoomType.Dining && r.Type != RoomType.Infirmary && r.Type != RoomType.Kitchen)
            {
                var hall = S.Layout.Rooms.First(x => x.Type == RoomType.GrandHall && x.Floor == 0);
                var mine = ch02.Targets.FirstOrDefault(t => t.StartsWith(a.Id + ":"));
                if (mine != null) { bool west = mine.EndsWith(":W"); bool roomWest = r.Rect.CX < hall.Rect.CX; if (west != roomWest) return true; }
            }
            if (S.RuleActive("CH16") && S.Minute >= 13 * 60 && S.Minute < 14 * 60 && (r.Type == RoomType.MachineRoom || r.Type == RoomType.WaterRoom)) return true;
            return false;
        }

        public static Activity LifeActivity(Simulation sim, Actor a)
        {
            var S = sim.S; var rng = S.R(Stream.Life);
            foreach (var r in S.Ch.Rules.Where(r => r.Active))
            {
                if (r.Rule == "CH11" && r.Times.Count > 0 && Math.Abs(S.Clock - r.Times[0]) < 20 && !S.Flags.ContainsKey($"ch11:{a.Id}"))
                {
                    S.Flags[$"ch11:{a.Id}"] = 1; var lounge = S.Layout.First(RoomType.Lounge); if (lounge == null) continue;
                    var act = new Activity { Id = "rule:CH11", Label = "교환회 참석", Priority = 1.2 + a.Def.P.Sociability };
                    act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(lounge, rng))); act.Steps.Add(Simulation.Do("trade", 20, Anim.Talk)); return act;
                }
                if (r.Rule == "CH08" && S.Minute > 10 * 60 && S.Minute < 17 * 60 && a.Def.P.Loyalty > 0.5f && !S.Flags.ContainsKey($"ch08:{a.Id}"))
                {
                    S.Flags[$"ch08:{a.Id}"] = 1; var room = S.Layout.First(rng.Chance(0.5) ? RoomType.PowerRoom : RoomType.WaterRoom); if (room == null) continue;
                    var act = new Activity { Id = "rule:CH08", Label = "공동 점검", Priority = 1.4 };
                    act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(room, rng))); act.Steps.Add(Simulation.Do("inspect", 20, Anim.Operate)); return act;
                }
                if (r.Rule == "CH07" && S.Phase == Phase.Daily && a.Def.P.Curiosity > 0.55f && !S.Loans.Any(l => l.Borrower == a.Id && l.State == "lent") && !S.Flags.ContainsKey($"ch07:{a.Id}:{S.Day}") && rng.Chance(0.25))
                {
                    var it = r.Targets.Where(x => !x.StartsWith("sheet:")).Select(S.I).FirstOrDefault(i => i != null && i.Holder == null && i.Room == i.HomeRoom);
                    if (it == null) continue;
                    S.Flags[$"ch07:{a.Id}:{S.Day}"] = 1;
                    var act = new Activity { Id = "rule:CH07", Label = "공용 장비 대여", Priority = 1.5 };
                    act.Steps.Add(Simulation.GoTo(it.Pos)); act.Steps.Add(new ActionStep { Kind = "X_TakeOwn", Item = it.Id }); act.Steps.Add(new ActionStep { Kind = "X_LogLoan", Item = it.Id, Data = r.Targets.First(x => x.StartsWith("sheet:")).Substring(6) });
                    return act;
                }
                if (r.Rule == "CH17" && r.Targets.Contains(a.Id) && !S.Flags.ContainsKey($"ch17:{a.Id}:{S.Day}"))
                {
                    S.Flags[$"ch17:{a.Id}:{S.Day}"] = 1; var room = S.Layout.First(RoomType.Archive) ?? S.Layout.First(RoomType.Library); if (room == null) continue;
                    var act = new Activity { Id = "rule:CH17", Label = "기록 정리", Priority = 1.6 };
                    act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(room, rng))); act.Steps.Add(Simulation.Do("organize", 25, Anim.Write)); return act;
                }
            }
            return null;
        }

        public static void OnEnterRoom(Simulation sim, Actor a, Room r)
        {
            var S = sim.S;
            // CH17 record keepers log who passed the archive (a document others can read later)
            if (r == null) return;
            if (S.RuleActive("CH17") && (r.Type == RoomType.Archive || r.Type == RoomType.Storage))
            {
                var keepers = S.Rule("CH17").Targets; var present = S.Actors.Values.FirstOrDefault(x => keepers.Contains(x.Id) && x.Room == r.Id && x.Alive && x != a);
                if (present != null) S.K(present.Id).Sightings.Add(new Sighting { Target = a.Id, Room = r.Id, T0 = S.Clock, T1 = S.Clock, IdConf = 0.95f, Root = "log:" + r.Id + ":" + S.Seq });
            }
        }
    }
}
