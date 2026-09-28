using System.Collections.Generic;

namespace BL23.Sim
{
    /// <summary>
    /// Bond stories: short personal scenes each participant shares with 민혁 as they grow closer (shared activities,
    /// gifts, conversations). Stage 1–3 can happen in any loop; stage 4 opens only in a later loop, after the player has
    /// already heard stages 1–3 from that person in some earlier loop (the person does not remember — the player does).
    /// A scene is a few lines and one choice; the choice changes the relationship and may reveal something real
    /// (their wish/contract, their secret, a fear, what they noticed about someone else).
    /// </summary>
    public sealed class BondLine { public string Who; public string Text; public Emotion Emo; public BondLine(string who, string text, Emotion emo = Emotion.Neutral) { Who = who; Text = text; Emo = emo; } }

    public sealed class BondChoice
    {
        public string Label;                 // what 민혁 says (menu label)
        public List<BondLine> Reply = new List<BondLine>();   // how the scene ends after that choice
        public float Like, Trust, Attach;    // relationship change toward 민혁
        public string Reveal;                // fact unlocked for the player: "contract:<id>", "secret:<id>", "fear:<id>", "hint:<id>", "note:<text>"
    }

    public sealed class BondScene
    {
        public string Id; public string Npc; public int Stage; public string Title;
        public List<BondLine> Lines = new List<BondLine>();
        public List<BondChoice> Choices = new List<BondChoice>();
    }

    public static partial class BondScenes
    {
        public const string Me = "P01";
        static BondLine N(string npc, string text, Emotion e = Emotion.Neutral) => new BondLine(npc, text, e);
        static BondLine M(string text, Emotion e = Emotion.Neutral) => new BondLine(Me, text, e);

        static readonly List<BondScene> _all = new List<BondScene>();
        static bool _init;
        public static IReadOnlyList<BondScene> All { get { Ensure(); return _all; } }
        static void Add(BondScene s) => _all.Add(s);

        static partial void Init_P02(); static partial void Init_P03(); static partial void Init_P04(); static partial void Init_P05();
        static partial void Init_P06(); static partial void Init_P07(); static partial void Init_P08(); static partial void Init_P09();
        static partial void Init_P10(); static partial void Init_P11(); static partial void Init_P12(); static partial void Init_P13();
        static partial void Init_P14(); static partial void Init_P15(); static partial void Init_P16(); static partial void Init_P17();
        static partial void Init_P18();

        static void Ensure()
        {
            if (_init) return; _init = true;
            // per-character scene sets live in BondScenes_Pxx.cs (partial methods: a missing file simply adds nothing)
            Init_P02(); Init_P03(); Init_P04(); Init_P05(); Init_P06(); Init_P07(); Init_P08(); Init_P09(); Init_P10();
            Init_P11(); Init_P12(); Init_P13(); Init_P14(); Init_P15(); Init_P16(); Init_P17(); Init_P18();
            _all.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }
    }
}
