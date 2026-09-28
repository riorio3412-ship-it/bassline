using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    public sealed class ActivityDef
    {
        public string Id; public string Kor; public RoomType[] Rooms; public string[] Spots; public double Min, Max; public Anim Anim;
        public float Hunger, Energy, Social, Fun, Stress; public bool Group; public bool Noisy; public string Tool;
    }

    public static class Activities
    {
        static Dictionary<string, ActivityDef> _d;
        public static ActivityDef Get(string id) { Ensure(); return id != null && _d.TryGetValue(id, out var d) ? d : null; }
        public static IEnumerable<ActivityDef> All { get { Ensure(); return _d.Values; } }
        static void A(string id, string kor, RoomType[] rooms, string[] spots, double min, double max, Anim anim, float hunger = 0, float energy = -0.02f, float social = 0, float fun = 0.1f, float stress = -0.05f, bool group = false, bool noisy = false)
            => _d[id] = new ActivityDef { Id = id, Kor = kor, Rooms = rooms, Spots = spots, Min = min, Max = max, Anim = anim, Hunger = hunger, Energy = energy, Social = social, Fun = fun, Stress = stress, Group = group, Noisy = noisy };
        static RoomType[] R(params RoomType[] r) => r; static string[] S(params string[] s) => s;
        static void Ensure()
        {
            if (_d != null) return; _d = new Dictionary<string, ActivityDef>();
            A("eat", "식사", R(RoomType.Dining), S("sit"), 18, 30, Anim.Eat, -0.75f, 0.02f, 0.15f, 0.05f, -0.05f, true);
            A("snack", "군것질", R(RoomType.Kitchen, RoomType.TeaRoom), S("stand", "sit", "cook"), 6, 12, Anim.Eat, -0.35f, 0, 0.02f, 0.05f);
            A("cook", "요리", R(RoomType.Kitchen), S("cook"), 25, 45, Anim.Cook, 0.05f, -0.05f, 0, 0.2f, -0.08f);
            A("read", "독서", R(RoomType.Library, RoomType.Study, RoomType.Lounge, RoomType.SecretStacks), S("read", "sit"), 25, 60, Anim.Read, 0.05f, -0.03f, 0, 0.18f, -0.1f);
            A("organize", "정리", R(RoomType.Archive, RoomType.Storage, RoomType.Library, RoomType.Pantry), S("stand", "work"), 20, 40, Anim.Use, 0.05f, -0.06f, 0, 0.12f, -0.06f);
            A("restore", "복원 작업", R(RoomType.Gallery, RoomType.Workshop), S("work"), 30, 70, Anim.Craft, 0.06f, -0.05f, 0, 0.25f, -0.12f);
            A("tea", "티타임", R(RoomType.TeaRoom, RoomType.Lounge, RoomType.Kitchen), S("sit"), 15, 30, Anim.Drink, -0.08f, 0.02f, 0.06f, 0.12f, -0.12f);
            A("walk", "산책", R(RoomType.Greenhouse, RoomType.Courtyard, RoomType.GrandHall, RoomType.RainCorridor, RoomType.Gallery), S("walk", "view", "stand", "sit"), 12, 30, Anim.Idle, 0.04f, -0.04f, 0, 0.12f, -0.1f);
            A("swim", "수영", R(RoomType.Pool), S("swim", "rest"), 20, 40, Anim.Swim, 0.12f, -0.12f, 0, 0.25f, -0.12f);
            A("game", "게임", R(RoomType.GameRoom), S("play"), 25, 60, Anim.Play, 0.06f, -0.04f, 0.1f, 0.35f, -0.1f, true, true);
            A("music", "연주", R(RoomType.MusicRoom), S("play"), 25, 60, Anim.Play, 0.05f, -0.04f, 0.05f, 0.35f, -0.12f, true, true);
            A("perform", "공연 연습", R(RoomType.Theater), S("perform"), 25, 50, Anim.Talk, 0.06f, -0.06f, 0.1f, 0.3f, -0.08f, true, true);
            A("garden", "화초 손질", R(RoomType.Greenhouse, RoomType.Courtyard), S("garden"), 20, 40, Anim.Garden, 0.05f, -0.05f, 0, 0.2f, -0.12f);
            A("repair", "수리", R(RoomType.Workshop), S("work"), 30, 60, Anim.Craft, 0.06f, -0.05f, 0, 0.3f, -0.08f);
            A("inspect", "설비 점검", R(RoomType.PowerRoom, RoomType.MachineRoom, RoomType.WaterRoom, RoomType.BoilerRoom), S("operate", "stand"), 15, 35, Anim.Operate, 0.04f, -0.04f, 0, 0.18f, -0.04f);
            A("craft", "종이 공예", R(RoomType.Lounge, RoomType.TeaRoom, RoomType.DollRoom, RoomType.Library), S("sit", "read"), 20, 45, Anim.Craft, 0.04f, -0.02f, 0, 0.22f, -0.12f);
            A("film", "영상 촬영", R(RoomType.Greenhouse, RoomType.GrandHall, RoomType.Lounge, RoomType.Theater, RoomType.WhiteDoors, RoomType.MirrorWater, RoomType.ClockMuseum), S("stand", "view"), 15, 30, Anim.Photo, 0.05f, -0.04f, 0.05f, 0.3f, -0.06f);
            A("party", "모임", R(RoomType.Lounge, RoomType.TeaRoom, RoomType.GameRoom), S("sit"), 25, 60, Anim.Laugh, 0.05f, -0.04f, 0.4f, 0.3f, -0.15f, true, true);
            A("socialize", "이야기", R(RoomType.Lounge, RoomType.TeaRoom, RoomType.Dining, RoomType.Parlor), S("sit"), 15, 35, Anim.Talk, 0.03f, -0.02f, 0.35f, 0.15f, -0.1f, true);
            A("rest", "휴식", R(RoomType.Bedroom, RoomType.Lounge, RoomType.Parlor, RoomType.GuestRoom, RoomType.DreamRoom, RoomType.PhoneRoom), S("sit", "rest", "sleep"), 20, 50, Anim.Idle, 0.03f, 0.18f, -0.02f, 0.03f, -0.15f);
            A("sleep", "수면", R(RoomType.Bedroom), S("sleep"), 300, 480, Anim.Sleep, 0.08f, 1f, 0, 0, -0.3f);
            A("exercise", "운동", R(RoomType.Gym, RoomType.GameRoom, RoomType.Courtyard, RoomType.Pool, RoomType.Landing), S("stand", "play"), 20, 40, Anim.Exercise, 0.12f, -0.12f, 0, 0.2f, -0.18f);
            A("puzzle", "숫자 퍼즐", R(RoomType.Library, RoomType.Study, RoomType.Lounge), S("read", "sit"), 20, 45, Anim.Think, 0.03f, -0.03f, 0, 0.2f, -0.1f);
            A("carry", "짐 운반", R(RoomType.Storage, RoomType.Laundry, RoomType.Kitchen, RoomType.Pantry), S("stand", "work"), 15, 35, Anim.Carry, 0.1f, -0.1f, 0, 0.1f, -0.08f);
            A("trade", "물건 교환", R(RoomType.Lounge, RoomType.Dining, RoomType.ContractRoom), S("sit"), 10, 25, Anim.Talk, 0.02f, -0.02f, 0.2f, 0.12f, -0.02f, true);
            A("style", "의상 정리", R(RoomType.Wardrobe), S("style", "stand"), 20, 45, Anim.Craft, 0.04f, -0.03f, 0, 0.25f, -0.1f);
            A("exhibit", "전시 준비", R(RoomType.Gallery, RoomType.TrophyRoom), S("stand"), 20, 45, Anim.Use, 0.05f, -0.05f, 0, 0.22f, -0.08f);
            A("investigate", "기록 조사", R(RoomType.Archive, RoomType.Library, RoomType.Study, RoomType.SecretStacks, RoomType.DreamRoom, RoomType.Lab), S("read", "stand"), 20, 45, Anim.Read, 0.04f, -0.03f, 0, 0.2f, -0.02f);
            A("observe", "사람 구경", R(RoomType.GrandHall, RoomType.Landing, RoomType.Lounge, RoomType.Dining), S("stand", "view", "sit"), 10, 25, Anim.Idle, 0.02f, -0.02f, 0.05f, 0.15f, -0.04f);
            A("speech", "발표 연습", R(RoomType.Theater, RoomType.Lounge, RoomType.EmptyAuditorium), S("perform", "stand"), 15, 30, Anim.Talk, 0.04f, -0.04f, 0.05f, 0.2f, -0.05f, false, true);
            A("pray", "추모", R(RoomType.Chapel, RoomType.Oracle), S("pray", "sit"), 12, 30, Anim.Pray, 0.02f, -0.02f, 0, 0.02f, -0.2f);
            A("laundry", "빨래", R(RoomType.Laundry), S("laundry", "stand"), 15, 30, Anim.Wash, 0.04f, -0.04f, 0, 0.05f, -0.04f);
            A("cleanup", "뒷정리", R(RoomType.Dining, RoomType.Kitchen), S("stand", "cook"), 10, 20, Anim.Clean, 0.03f, -0.04f, 0.02f, 0.02f, -0.04f);
            A("explore", "저택 탐방", R(), S(), 5, 12, Anim.Search, 0.04f, -0.04f, 0, 0.2f, -0.02f);
            A("tour", "낯선 방 구경", R(RoomType.RainCorridor, RoomType.EmptyAuditorium, RoomType.WaitingRoom, RoomType.WhiteDoors, RoomType.MirrorWater, RoomType.ClockMuseum), S("stand", "sit", "view"), 8, 20, Anim.Search, 0.03f, -0.03f, 0, 0.3f, 0.02f);
            A("mourn", "애도", R(RoomType.Chapel), S("pray", "sit"), 15, 30, Anim.Pray, 0.02f, -0.02f, 0.05f, 0, -0.2f);
            A("listen", "음악 감상", R(RoomType.Lounge, RoomType.MusicRoom, RoomType.GameRoom, RoomType.TeaRoom, RoomType.Study, RoomType.Parlor, RoomType.Dining, RoomType.DreamRoom), S("listen", "sit"), 20, 45, Anim.Idle, 0.02f, -0.01f, 0.05f, 0.25f, -0.15f, true, true);
            A("chess", "체스", R(RoomType.Lounge, RoomType.Library, RoomType.Study, RoomType.GameRoom, RoomType.TeaRoom, RoomType.Parlor), S("play"), 25, 60, Anim.Think, 0.03f, -0.03f, 0.15f, 0.3f, -0.08f, true);
            A("browse", "구경", R(RoomType.Study, RoomType.Library, RoomType.TrophyRoom, RoomType.DollRoom, RoomType.Gallery, RoomType.Courtyard, RoomType.Greenhouse, RoomType.Pool, RoomType.Observatory, RoomType.Armory, RoomType.Oracle), S("view"), 10, 25, Anim.Idle, 0.02f, -0.02f, 0, 0.18f, -0.06f);
            A("bar", "한잔", R(RoomType.Lounge, RoomType.Dining, RoomType.Pool, RoomType.Parlor, RoomType.GameRoom), S("sit"), 15, 35, Anim.Drink, -0.05f, 0.0f, 0.2f, 0.18f, -0.14f, true);
        }

        public static readonly Dictionary<string, string> HobbyToActivity = new Dictionary<string, string>
        {
            {"walk","walk"},{"game","game"},{"read","read"},{"observe","observe"},{"organize","organize"},{"cleanup","cleanup"},{"restore","restore"},{"tea","tea"},
            {"swim","swim"},{"speech","speech"},{"socialize","socialize"},{"trade","trade"},{"party","party"},{"music","music"},{"rest","rest"},{"perform","perform"},
            {"cook","cook"},{"garden","garden"},{"repair","repair"},{"inspect","inspect"},{"exercise","exercise"},{"craft","craft"},{"investigate","investigate"},
            {"style","style"},{"exhibit","exhibit"},{"film","film"},{"puzzle","puzzle"},{"carry","carry"}
        };
    }
}
