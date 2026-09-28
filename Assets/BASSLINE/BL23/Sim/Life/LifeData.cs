using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BL23.Sim
{
    /// <summary>
    /// Registry of authored daily-life scenes. Content files (Sim/Content/Life_*.cs) add to it from static methods named
    /// Init_* (found by reflection, run in ordinal name order, each guarded) — a new file needs no registration.
    /// Lists are sorted by Id (ordinal) after loading so every lookup is deterministic.
    /// </summary>
    public static partial class LifeData
    {
        public static readonly List<PairDef> Pairs = new List<PairDef>();
        public static readonly List<HeartDef> Hearts = new List<HeartDef>();
        public static readonly List<HangDef> Hangs = new List<HangDef>();
        /// <summary>The mansion quiz show's questions (예담): one habit of one resident each. Options[0] is the right answer.</summary>
        public static readonly List<QuizQ> Quiz = new List<QuizQ>();
        public sealed class QuizQ { public string Subject, Ask, Note, React, WrongReact; public string[] Options; }
        static void Q(string subject, string ask, string right, string wrong1, string wrong2, string note, string react = null, string wrongReact = null)
            => Quiz.Add(new QuizQ { Subject = subject, Ask = ask, Options = new[] { right, wrong1, wrong2 }, Note = note, React = react, WrongReact = wrongReact });
        /// <summary>Content that failed to load (the life test prints these).</summary>
        public static readonly List<string> Errors = new List<string>();
        static bool _init;
        static Dictionary<string, LScene> _byId;

        public static void Ensure()
        {
            if (_init) return; _init = true;
            foreach (var m in typeof(LifeData).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Where(m => m.Name.StartsWith("Init_") && m.GetParameters().Length == 0).OrderBy(m => m.Name, StringComparer.Ordinal))
            {
                try { m.Invoke(null, null); }
                catch (Exception e) { Errors.Add(m.Name + ": " + (e.InnerException?.Message ?? e.Message)); }
            }
            Pairs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            Hearts.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            Hangs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            _byId = new Dictionary<string, LScene>(StringComparer.Ordinal);
            foreach (var s in Pairs.Cast<LScene>().Concat(Hearts).Concat(Hangs))
            {
                if (s.Id == null) { Errors.Add("scene without id: " + s.Title); continue; }
                if (_byId.ContainsKey(s.Id)) { Errors.Add("duplicate scene id " + s.Id); continue; }
                _byId[s.Id] = s;
            }
        }

        public static LScene Get(string id) { Ensure(); return id != null && _byId.TryGetValue(id, out var s) ? s : null; }
        public static HeartDef Heart(string npc, int no) { Ensure(); return Hearts.FirstOrDefault(h => h.Npc == npc && h.No == no); }
        public static int HeartCount(string npc) { Ensure(); return Hearts.Count(h => h.Npc == npc); }

        // ------------------------------------------------------------------ authoring helpers (used by Content/Life_*.cs)
        static PairDef Pair(string id, string a, string b, string title) { var p = new PairDef { Id = id, A = a, B = b, Title = title, Kind = "pair", Npc = a }; Pairs.Add(p); return p; }
        static HeartDef Heart(string npc, int no, string title) { var h = new HeartDef { Id = $"H_{npc}_{no}", Npc = npc, No = no, Title = title, Kind = "heart" }; Hearts.Add(h); return h; }
        static HangDef Hang(string npc, string act, string title) { var h = new HangDef { Id = $"G_{npc}_{act}_{Hangs.Count(x => x.Npc == npc && x.Act == act)}", Npc = npc, Act = act, Title = title, Kind = "hang" }; Hangs.Add(h); return h; }

        /// <summary>A line.</summary>
        static LL L(string who, string text, Emotion e = Emotion.Neutral, Anim g = Anim.Talk) => new LL { Who = who, Text = text, Emo = e, Gest = g };
        /// <summary>A line with a 반말 variant (used when the speaker is on casual terms with the listener).</summary>
        static LL LC(string who, string polite, string casual, Emotion e = Emotion.Neutral, Anim g = Anim.Talk) => new LL { Who = who, Text = polite, TextC = casual, Emo = e, Gest = g };
        /// <summary>민혁's line (optionally with a 반말 variant).</summary>
        static LL Me(string text, string casual = null, Emotion e = Emotion.Neutral) => new LL { Who = "me", Text = text, TextC = casual, Emo = e };
        /// <summary>A line said only when the condition holds (callbacks: "mem:P10:bland", "casual:P03", "day>=3" …).</summary>
        static LL If(string cond, LL line) { line.If = cond; return line; }
        static LOpt O(string label, params LL[] reply) { var o = new LOpt { Label = label }; o.Reply.AddRange(reply); return o; }
        static LOpt OC(string polite, string casual, params LL[] reply) { var o = new LOpt { Label = polite, LabelC = casual }; o.Reply.AddRange(reply); return o; }

        /// <summary>How someone feels about 민혁 changes.</summary>
        static LFx ToMe(string who, float like = 0, float trust = 0, float attach = 0, float grudge = 0, float respect = 0, float jealous = 0, float romance = 0, string mem = null)
            => new LFx { From = who, To = "me", Like = like, Trust = trust, Attach = attach, Grudge = grudge, Respect = respect, Jealous = jealous, Romance = romance, Memory = mem };
        /// <summary>How one resident feels about another changes.</summary>
        static LFx Rel(string from, string to, float like = 0, float trust = 0, float grudge = 0, float respect = 0, float attach = 0, float jealous = 0, float romance = 0, float fear = 0, string mem = null, string tag = null, string untag = null)
            => new LFx { From = from, To = to, Like = like, Trust = trust, Grudge = grudge, Respect = respect, Attach = attach, Jealous = jealous, Romance = romance, Fear = fear, Memory = mem, Tag = tag, Untag = untag };
    }
}
