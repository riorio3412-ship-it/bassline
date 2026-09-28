using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    public sealed class GoalDef { public string Id; public string Owner; public string Label; public string Activity; public RoomType Room; public int Steps; public string Partner; public bool Player; public string Desc; }

    /// <summary>Two long-term goals per participant (36): 34 autonomous NPC goals + 2 player-chosen goals.</summary>
    public static class Goals
    {
        public static readonly List<GoalDef> Catalog = new List<GoalDef>
        {
            new GoalDef { Id = "G01a", Owner = "P01", Label = "모두와 한 번씩 이야기하기", Player = true, Steps = 17, Desc = "살아 있는 사람 모두와 한 번씩 이야기해 본다" },
            new GoalDef { Id = "G01b", Owner = "P01", Label = "달라진 저택 지도 완성하기", Player = true, Steps = 30, Desc = "이번 루프에 새로 지어진 저택의 방을 직접 다 둘러본다" },
            new GoalDef { Id = "G02a", Owner = "P02", Label = "사람들 반응 기록하기", Activity = "observe", Room = RoomType.Lounge, Steps = 4 },
            new GoalDef { Id = "G02b", Owner = "P02", Label = "심리학 서가 정리", Activity = "read", Room = RoomType.Library, Steps = 3 },
            new GoalDef { Id = "G03a", Owner = "P03", Label = "당번표 만들기", Activity = "organize", Room = RoomType.Archive, Steps = 4 },
            new GoalDef { Id = "G03b", Owner = "P03", Label = "식당 뒷정리 당번 맡기", Activity = "cleanup", Room = RoomType.Dining, Steps = 3 },
            new GoalDef { Id = "G04a", Owner = "P04", Label = "갤러리 액자 복원", Activity = "restore", Room = RoomType.Gallery, Steps = 4 },
            new GoalDef { Id = "G04b", Owner = "P04", Label = "조용히 차 마시는 시간", Activity = "tea", Room = RoomType.TeaRoom, Steps = 3 },
            new GoalDef { Id = "G05a", Owner = "P05", Label = "지지자 모임 만들기", Activity = "socialize", Room = RoomType.Lounge, Steps = 4 },
            new GoalDef { Id = "G05b", Owner = "P05", Label = "매일 수영", Activity = "swim", Room = RoomType.Pool, Steps = 3 },
            new GoalDef { Id = "G06a", Owner = "P06", Label = "공용품 재고 장부 쓰기", Activity = "organize", Room = RoomType.Storage, Steps = 4 },
            new GoalDef { Id = "G06b", Owner = "P06", Label = "교환 장터 준비", Activity = "trade", Room = RoomType.Lounge, Steps = 2 },
            new GoalDef { Id = "G07a", Owner = "P07", Label = "다 같이 파티 열기", Activity = "party", Room = RoomType.Lounge, Steps = 3 },
            new GoalDef { Id = "G07b", Owner = "P07", Label = "혼자만의 독서", Activity = "read", Room = RoomType.Library, Steps = 3 },
            new GoalDef { Id = "G08a", Owner = "P08", Label = "합주 세션", Activity = "music", Room = RoomType.MusicRoom, Steps = 3 },
            new GoalDef { Id = "G08b", Owner = "P08", Label = "리듬 게임 최고점 갈아 치우기", Activity = "game", Room = RoomType.GameRoom, Steps = 3 },
            new GoalDef { Id = "G09a", Owner = "P09", Label = "소극장 공연 준비", Activity = "perform", Room = RoomType.Theater, Steps = 4 },
            new GoalDef { Id = "G09b", Owner = "P09", Label = "부치지 못할 편지", Activity = "read", Room = RoomType.Study, Steps = 2 },
            new GoalDef { Id = "G10a", Owner = "P10", Label = "끼니마다 식사 준비", Activity = "cook", Room = RoomType.Kitchen, Steps = 6 },
            new GoalDef { Id = "G10b", Owner = "P10", Label = "온실 허브 기르기", Activity = "garden", Room = RoomType.Greenhouse, Steps = 3 },
            new GoalDef { Id = "G11a", Owner = "P11", Label = "저택 설비 점검", Activity = "inspect", Room = RoomType.PowerRoom, Steps = 3 },
            new GoalDef { Id = "G11b", Owner = "P11", Label = "의수 조정 장치 만들기", Activity = "repair", Room = RoomType.Workshop, Steps = 4, Partner = "P13" },
            new GoalDef { Id = "G12a", Owner = "P12", Label = "작은 무대 공연", Activity = "perform", Room = RoomType.Theater, Steps = 3 },
            new GoalDef { Id = "G12b", Owner = "P12", Label = "스티커 굿즈 정리", Activity = "craft", Room = RoomType.TeaRoom, Steps = 2 },
            new GoalDef { Id = "G13a", Owner = "P13", Label = "게임 대회 열기", Activity = "game", Room = RoomType.GameRoom, Steps = 4 },
            new GoalDef { Id = "G13b", Owner = "P13", Label = "지난 경기 기록 다시 보기", Activity = "investigate", Room = RoomType.Library, Steps = 2 },
            new GoalDef { Id = "G14a", Owner = "P14", Label = "추모 예배실 정돈", Activity = "pray", Room = RoomType.Chapel, Steps = 3 },
            new GoalDef { Id = "G14b", Owner = "P14", Label = "종이 백합 접기", Activity = "craft", Room = RoomType.Lounge, Steps = 3 },
            new GoalDef { Id = "G15a", Owner = "P15", Label = "저택 기록 지도 만들기", Activity = "investigate", Room = RoomType.Archive, Steps = 4 },
            new GoalDef { Id = "G15b", Owner = "P15", Label = "특종 수첩 채우기", Activity = "observe", Room = RoomType.GrandHall, Steps = 3 },
            new GoalDef { Id = "G16a", Owner = "P16", Label = "의상 전시", Activity = "style", Room = RoomType.Wardrobe, Steps = 3 },
            new GoalDef { Id = "G16b", Owner = "P16", Label = "사업 구상 노트 쓰기", Activity = "read", Room = RoomType.Study, Steps = 2 },
            new GoalDef { Id = "G17a", Owner = "P17", Label = "초대장 돌려 파티 열기", Activity = "party", Room = RoomType.TeaRoom, Steps = 3 },
            new GoalDef { Id = "G17b", Owner = "P17", Label = "종이 모형 전시", Activity = "craft", Room = RoomType.DollRoom, Steps = 3 },
            new GoalDef { Id = "G18a", Owner = "P18", Label = "짐 나르는 동선 다듬기", Activity = "carry", Room = RoomType.Storage, Steps = 3 },
            new GoalDef { Id = "G18b", Owner = "P18", Label = "숫자 퍼즐 끝까지 풀기", Activity = "puzzle", Room = RoomType.Library, Steps = 3 },
        };

        public static void InitLoop(GameState S)
        {
            S.Goals.Clear();
            foreach (var d in Catalog) S.Goals[d.Id] = new Goal { Id = d.Id, Owner = d.Owner, Kind = d.Activity, Label = d.Label, Stages = d.Steps, PlayerAccepted = false };
        }

        public static Activity NextStep(Simulation sim, Actor a)
        {
            var S = sim.S;
            var g = S.Goals.Values.Where(x => x.Owner == a.Id && !x.Done && !x.Abandoned).OrderBy(x => x.Stage).FirstOrDefault();
            if (g == null) return null;
            var def = Catalog.First(x => x.Id == g.Id); if (def.Player) return null;
            if (S.Flags.TryGetValue("goalcd:" + g.Id, out var cd) && S.Clock < cd) return null;
            var room = S.Layout.Rooms.Where(r => r.Type == def.Room && sim.RoomUsable(a, r)).FirstOrDefault()
                       ?? S.Layout.Rooms.Where(r => Activities.Get(def.Activity)?.Rooms.Contains(r.Type) == true && sim.RoomUsable(a, r)).FirstOrDefault();
            if (room == null) return null;
            var act = sim.Simple(a, def.Activity, room.Id); if (act == null) return null;
            act.Id = "goal:" + g.Id; act.Label = g.Label + $" ({g.Stage + 1}/{g.Stages})"; act.Priority = 1.1 + 0.3 * (1 - a.Needs.Fun);
            return act;
        }

        public static void OnStepDone(Simulation sim, Actor a, string actId)
        {
            var S = sim.S; string gid = actId.Substring(5);
            if (!S.Goals.TryGetValue(gid, out var g)) return;
            g.Stage++; g.Progress = g.Stage / (float)g.Stages; S.Flags["goalcd:" + gid] = S.Clock + 120;
            // shared work with people present forms colleague bonds
            foreach (var x in S.Living.Where(x => x != a && x.Room == a.Room && x.Pose != Pose.Sleep)) { Relations.Change(S, a.Id, x.Id, like: 0.02f, attach: 0.02f); if (!g.Helpers.Contains(x.Id)) g.Helpers.Add(x.Id); }
            if (g.Stage >= g.Stages) { g.Done = true; S.Log("GoalDone", a.Id, data: g.Label); a.Needs.Fun = 1; a.Needs.Stress = MathX.Clamp01(a.Needs.Stress - 0.3f); }
        }

        public static void OnEnterRoom(Simulation sim, Actor a, Room r)
        {
            var S = sim.S; if (!a.IsPlayer || r == null) return;
            if (S.Goals.TryGetValue("G01b", out var g) && g.PlayerAccepted && !g.Done)
            {
                int visited = S.K(a.Id).Facts.Count(f => f.StartsWith("visited:"));
                g.Stage = Math.Min(g.Stages, visited); g.Progress = g.Stage / (float)g.Stages; if (g.Stage >= g.Stages) { g.Done = true; S.Emit(GameEventType.Notice, a.Id, text: "목표를 이뤘다 — " + g.Label, key: "goal"); }
            }
        }

        /// <summary>When an owner dies or leaves, their goals stay recorded; helpers may take a reduced version over.</summary>
        public static void OnOwnerGone(Simulation sim, string owner)
        {
            var S = sim.S;
            foreach (var g in S.Goals.Values.Where(x => x.Owner == owner && !x.Done))
            {
                g.Abandoned = true;
                var heir = g.Helpers.Select(S.A).FirstOrDefault(x => x != null && x.Alive && !x.IsPlayer);
                if (heir != null) { g.Handover = heir.Id; S.Log("GoalHandover", heir.Id, owner, data: g.Label); }
            }
        }
    }
}
