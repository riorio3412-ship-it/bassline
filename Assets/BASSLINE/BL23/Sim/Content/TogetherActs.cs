using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>Ways to spend time with someone (time-on-demand §5.1): what it is called, how long it takes, what it does to how
    /// they feel about 민혁 (NPC→민혁 base; 민혁→NPC gets 0.6× like and 0.5× attach), and how the bodies move.</summary>
    public sealed class TogetherAct
    {
        public string Id; public string Label; public int Minutes; public string Activity; public Anim Anim;
        public float Like, Attach, Trust, Respect;
    }

    public static class TogetherActs
    {
        public static readonly List<TogetherAct> All = new List<TogetherAct>
        {
            new TogetherAct { Id = "talk_long", Label = "천천히 이야기를 나눈다", Minutes = 30, Activity = "socialize", Anim = Anim.Talk, Like = 0.03f, Attach = 0.03f, Trust = 0.04f },
            new TogetherAct { Id = "tea", Label = "차 한잔", Minutes = 30, Activity = "tea", Anim = Anim.Drink, Like = 0.04f, Attach = 0.03f },
            new TogetherAct { Id = "meal", Label = "함께 식사하기", Minutes = 30, Activity = "eat", Anim = Anim.Eat, Like = 0.03f, Attach = 0.03f },
            new TogetherAct { Id = "join", Label = "하는 일을 함께 한다", Minutes = 30, Activity = null, Anim = Anim.Use, Like = 0.04f, Attach = 0.02f, Trust = 0.02f },
        };
        public static TogetherAct Get(string id) => All.FirstOrDefault(a => a.Id == id);

        /// <summary>What someone may be joined in (their everyday "life:" activity). Sleep, rest, meals, chores and wandering are not.</summary>
        public static readonly HashSet<string> NotJoinable = new HashSet<string> { "sleep", "rest", "eat", "snack", "carry", "cleanup", "laundry", "explore", "tour", "inspect", "walk" };
        /// <summary>Work-like activities: joining them earns a little respect too.</summary>
        public static readonly HashSet<string> WorkLike = new HashSet<string> { "repair", "restore", "organize", "investigate", "exhibit" };
        /// <summary>Rooms where a cup of tea is at hand (besides a room with a tea cart).</summary>
        public static readonly HashSet<RoomType> TeaRooms = new HashSet<RoomType> { RoomType.TeaRoom, RoomType.Lounge, RoomType.Kitchen, RoomType.Parlor, RoomType.Dining };
    }
}
