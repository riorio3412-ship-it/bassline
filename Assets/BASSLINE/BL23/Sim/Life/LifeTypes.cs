using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // DAILY LIFE — authored scene types (DailyLifeDesign §3–§8, features F4–F12).
    //
    // Every scene in daily life is a list of BEATS. A beat is a few lines (LL) and, optionally, a choice (LOpt).
    // A choice carries its reply lines and its effects (LFx relationship changes, remembered choices, facts for the
    // notebook, a nickname, 반말, a gift, a rumour, a tie change) and may jump to another beat. Scenes without a
    // choice simply play their lines. The same runner serves every kind:
    //   pair      two residents bouncing off each other (F4) — NPC-only (overheard, becomes talk) or with 민혁 there
    //   heart     a resident's personal story chain with 민혁 (§6), 4 per resident, unlocked by closeness and days
    //   hang      a moment inside "함께 시간을 보낸다" (F7): the resident's ♥ activities, with callbacks the second time
    //   table     the morning / evening table (F5) — built at run time from topic keys (Content/Life_Table*.cs)
    //   fest      group events (F10), memorial (F6), gifts, jealousy, rumours, callbacks — built at run time
    // Authoring lives in Sim/Content/Life_*.cs (static partial class LifeData, methods named Init_*; auto-registered).
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>One line of an authored scene. Who: "Pxx" or "me" (민혁). '|' breaks subtitle pages (≤ 60 chars each).
    /// Slots: {me} how the speaker addresses 민혁 · {nick} the speaker's nickname for 민혁 · {a} {b} {t} {victim} (names, in
    /// the speaker's own address) · {place} {time} {item} {act} {rule} {n} {topic} {rumour}. Particles as in LineBank ({t:이}).</summary>
    public sealed class LL
    {
        public string Who, Text, TextC, If; public Emotion Emo; public Anim Gest = Anim.Talk;
    }

    /// <summary>A relationship change: From → To ("me" = 민혁). Memory is a short Korean note (shown as a toast when 민혁 is involved).</summary>
    public sealed class LFx
    {
        public string From, To; public float Like, Trust, Respect, Attach, Romance, Fear, Grudge, Jealous; public string Memory, Tag, Untag;
    }

    /// <summary>A choice. Label is 민혁's words (spoken after picking) unless Silent (an action, shown only in the menu).</summary>
    public sealed class LOpt
    {
        public string Label, LabelC, If;
        public readonly List<LL> Reply = new List<LL>();
        public readonly List<LFx> Fx = new List<LFx>();
        public string MemKey, FactList, NoteText, NickName, GiftType, NextBeat, RumourKind, TieState, CallbackLine;
        /// <summary>The resident this choice's facts, memory, nickname and gift belong to (default: the scene's resident).</summary>
        public string About;
        public bool MakeCasual, Silent;
        public int Score;   // mini-games: 1 = right answer / good read (festival quiz, cards)

        public LOpt For(string npc) { About = npc; return this; }
        public LOpt Do(params LFx[] fx) { Fx.AddRange(fx); return this; }
        /// <summary>Remember this choice: player fact life:mem:&lt;npc&gt;:&lt;key&gt; (conditions "mem:Pxx:key" bring it back later).</summary>
        public LOpt Mem(string key) { MemKey = key; return this; }
        /// <summary>';'-separated notebook facts: "note:text", "likes:topic", "habit:text", "tell", "hint", "contract", or a raw fact.</summary>
        public LOpt Know(string facts) { FactList = FactList == null ? facts : FactList + ";" + facts; return this; }
        /// <summary>A quiet system line after the choice ("수첩에 적었다 — …").</summary>
        public LOpt Note(string text) { NoteText = text; return this; }
        /// <summary>From now on the resident calls 민혁 this ({nick}).</summary>
        public LOpt Nick(string nick) { NickName = nick; return this; }
        public LOpt Give(string itemType) { GiftType = itemType; return this; }
        public LOpt Go(string beat) { NextBeat = beat; return this; }
        public LOpt End() { NextBeat = "!end"; return this; }
        /// <summary>Both sides switch to 반말 (Rel.Casual) — every later line from the voice packs follows.</summary>
        public LOpt Casual() { MakeCasual = true; return this; }
        public LOpt Rumour(string kind) { RumourKind = kind; return this; }
        /// <summary>A tie changes state ("P02:P03:truce") — counted by the life test (TIES) and remembered on both sides.</summary>
        public LOpt Tie(string state) { TieState = state; return this; }
        public LOpt When(string cond) { If = cond; return this; }
        /// <summary>A line the resident says to 민혁 on a LATER day because of this choice (a callback).</summary>
        public LOpt Later(string line) { CallbackLine = line; return this; }
        /// <summary>Menu-only action ("말없이 빵을 반으로 가른다"): not spoken as 민혁's line.</summary>
        public LOpt Act() { Silent = true; return this; }
        public LOpt Right() { Score = 1; return this; }
    }

    public sealed class LBeat
    {
        public string Id; public string Next;
        public readonly List<LL> Lines = new List<LL>();
        public readonly List<LOpt> Opts = new List<LOpt>();
    }

    /// <summary>Base of every authored or generated scene.</summary>
    public class LScene
    {
        public string Id, Title, Kind, Npc;
        /// <summary>RoomType names (';') where it can happen (empty = anywhere not a passage), time tags (morning|day|evening|night|meal), condition.</summary>
        public string Where, When, If;
        public readonly List<LBeat> Beats = new List<LBeat>();
        public LBeat Beat(string id) => Beats.FirstOrDefault(b => b.Id == id);
        public LBeat First => Beats.Count > 0 ? Beats[0] : null;
        internal LBeat Last => Beats.Count > 0 ? Beats[Beats.Count - 1] : null;
        public int LineCount => Beats.Sum(b => b.Lines.Count + b.Opts.Sum(o => o.Reply.Count));
        public IEnumerable<LL> AllLines() { foreach (var b in Beats) { foreach (var l in b.Lines) yield return l; foreach (var o in b.Opts) foreach (var l in o.Reply) yield return l; } }
        public IEnumerable<LOpt> AllOpts() => Beats.SelectMany(b => b.Opts);
    }

    /// <summary>Fluent authoring (CRTP so chains keep their concrete type).</summary>
    public abstract class LSceneB<T> : LScene where T : LSceneB<T>
    {
        T Self => (T)this;
        public T At(string rooms) { Where = rooms; return Self; }
        public T Time(string when) { When = when; return Self; }
        public T Only(string cond) { If = cond; return Self; }
        /// <summary>The opening lines (first beat).</summary>
        public T Open(params LL[] lines) { if (Beats.Count == 0) Beats.Add(new LBeat { Id = "start" }); Beats[0].Lines.AddRange(lines); return Self; }
        /// <summary>Choices for the latest beat.</summary>
        public T Choice(params LOpt[] opts) { if (Beats.Count == 0) Beats.Add(new LBeat { Id = "start" }); Last.Opts.AddRange(opts); return Self; }
        /// <summary>A new beat: reached after the previous beat's choice (unless that choice goes elsewhere), or only by Go(id)
        /// when Branch(id) is used instead.</summary>
        public T Then(string id, params LL[] lines) { var prev = Last; var b = new LBeat { Id = id }; b.Lines.AddRange(lines); if (prev != null && prev.Next == null && !_branchNext) prev.Next = id; _branchNext = false; Beats.Add(b); return Self; }
        /// <summary>A beat reached only through Go(id) from a choice.</summary>
        public T Branch(string id, params LL[] lines) { _branchNext = true; return Then(id, lines); }
        bool _branchNext;
    }

    /// <summary>F4 — two residents in one room, in character. NPC-only it leaves NpcFx, a rumour and overheard lines;
    /// with 민혁 present it ends in his choice.</summary>
    public sealed class PairDef : LSceneB<PairDef>
    {
        public string A, B;
        /// <summary>conflict · rival · tease · flirt · ally · comic · reconcile · warm — conflict/rival feed the table's tt_conflict.</summary>
        public string PairKind = "conflict";
        public int Cooldown = 2, Weight = 1;
        public readonly List<LFx> NpcFx = new List<LFx>();
        public string NpcRumour, NpcTie;
        public PairDef Kind_(string k) { PairKind = k; return this; }
        public PairDef Npc_(params LFx[] fx) { NpcFx.AddRange(fx); return this; }
        public PairDef Talk(string rumourKind) { NpcRumour = rumourKind; return this; }
        public PairDef TieNpc(string state) { NpcTie = state; return this; }
        public PairDef Every(int days) { Cooldown = days; return this; }
        public PairDef W(int w) { Weight = w; return this; }
    }

    /// <summary>§6 — a resident's personal story chain (No 1..4), unlocked by closeness and days. The resident usually seeks
    /// 민혁 out with an invitation (Invite, in their own words; {time} {place}); asked in person, it plays at once when the room fits.</summary>
    public sealed class HeartDef : LSceneB<HeartDef>
    {
        public int No; public float NeedLike, NeedTrust, NeedAttach; public int NeedBond, Gap = 1;
        public string Invite, InviteC, Act = "tea";
        public HeartDef Need(float like = 0, float trust = 0, float attach = 0, int bond = 0, int gap = 1) { NeedLike = like; NeedTrust = trust; NeedAttach = attach; NeedBond = bond; Gap = gap; return this; }
        /// <summary>The invitation (a Request): what they say when they come to 민혁, and the everyday activity it is ("tea", "walk", "music", …).</summary>
        public HeartDef Ask(string polite, string casual = null, string act = "tea") { Invite = polite; InviteC = casual; Act = act; return this; }
    }

    /// <summary>F7 — a moment inside time spent together, per resident and activity (DailyLife §4.4 ♥). Again = the second
    /// and later times (callbacks, the inside joke).</summary>
    public sealed class HangDef : LSceneB<HangDef>
    {
        public string Act; public bool Again;
        public HangDef Repeat() { Again = true; return this; }
    }

    /// <summary>A generated scene (table topic, festival, memorial, gift, jealousy, rumour, callback …).</summary>
    public sealed class GenScene : LSceneB<GenScene> { }

    /// <summary>What the presentation plays (LifeSceneUI): lines by any number of speakers, then an optional choice.</summary>
    public sealed class LifeStage
    {
        public string Id, Kind, Title; public int Room = -1;
        public List<string> Cast = new List<string>();
        public List<Utterance> Lines = new List<Utterance>();
        public List<string> Options = new List<string>();
        public string Note;          // a system note to show after these lines (a learned tell, a remembered habit)
        public bool Done;            // no more choices: the scene is over once the lines are shown
    }

    /// <summary>The kernel's running scene (not saved; dialogue scenes also keep their place in a player fact).</summary>
    public sealed class SceneRun
    {
        public LScene Scene; public string Kind; public string Npc; public LBeat Beat;
        public Dictionary<string, string> Ctx = new Dictionary<string, string>();
        public int Room = -1; public int Score; public int Picks;
        public List<string> Cast = new List<string>();
        public string Note;
        public bool Over;
    }
}
