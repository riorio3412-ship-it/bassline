using System;
using System.Collections.Generic;

namespace BL23.Sim
{
    // =====================================================================================================================
    // The 심판 as a debate (Documentation/BL23/TrialReforge.md §2 deck, §6 심판). Kernel data only: serialisable, deterministic,
    // saved inside TrialState.Debate. Enums are append-only once shipped.
    //
    // v0 scope (2026-09-28 cloud session): the deck comes from the case as the kernel recorded it (legacy + pack feeders),
    // riddles from the presented story, theories from each resident's own knowledge, the culprit from TrialPack, the room's
    // lean from public facts. The Game plays DebateBeats; nothing here reads Unity.
    // =====================================================================================================================

    public enum PlateKind { Body, Object, Trace, Fixture, Door, Record, Witness, Memory }
    public enum PlateRole
    {
        Body, Hinge, Confirm, Seam, Link, Clear,                                   // true
        Staged, Frame, FalseAlibi, Cover, Coincidence, Secret, Mistaken             // fake: a true photograph with a false first reading
    }
    public enum PlateState { Unfound, Found, Borrowed, Late, Shown, Sealed, Flipped }

    /// <summary>A case's photographs (은판), frozen when the House confirms the death.</summary>
    [Serializable]
    public sealed class CaseDeck
    {
        public string Incident; public int Loop, Chapter; public double Frozen; public long FrozenSeq;
        public string Source;                                    // feeders that contributed ("pack,legacy")
        public int FakeTarget;                                   // 4 | 5 | 6 (MurderHash), before filling
        public List<Plate> Plates = new List<Plate>();           // by N
        public List<DeckClaim> Claims = new List<DeckClaim>();   // the presented story ("c1"…)
        public List<DeckMystery> Mysteries = new List<DeckMystery>();
        public int TrueCount, FakeCount;
        public List<string> Log = new List<string>();            // why each slot was filled or left empty
    }

    [Serializable]
    public sealed class Plate
    {
        public string Id;            // "pl:<incident>:<k>"
        public int N;                // display number (body = 1; the rest by MurderHash order, never by truth)
        public string Root;          // body:/item:/trace:/furn:/door:/talk:<witness>:<key>/rec:<ledger seq>
        public PlateKind Kind; public PlateRole Role;
        public bool True;            // HIDDEN: resolution, votes, tests only — never shown, never read by Game/
        public string Origin;        // fakes, shown after the flip: 위장 | 감싸기 | 착각 | 우연 | 비밀 | 피해자의 계획
        public string Title;         // ≤12 chars, names the thing (§2.8)
        public string Face;          // one plain sentence of what can be seen (§2.8)
        public string Back;          // the settled meaning, shown once Sealed/Flipped
        public string Corrected;     // Witness plates: the witness's own correction after 캐묻기
        public string Points;        // what the face reading points at: actor id | accident | time | place | null
        public List<string> Alibi = new List<string>();
        public Axis Axis; public Channel Channel;
        public List<string> Breaks = new List<string>();     // claim ids it contradicts (true plates)
        public List<string> Supports = new List<string>();   // claim ids its face reading props up
        public List<string> Routes = new List<string>();     // fakes: "pl:<id>" | "pl:<a>+pl:<b>" | "ask:<actor>" | "stage:<axis>"
        public List<string> Needs = new List<string>();      // true plates whose force needs a partner ("pl:<id>")
        public List<string> Users = new List<string>();      // who leans on it: "theory:<actor>" | "lie:<id>" | "story"
        public string Owner;         // who defends it in the 심판
        public string Witness, Seen; // Witness plates
        public int Room = -1; public double T0 = -1, T1 = -1;
        public string FoundBy; public double FoundAt = -1;
        public bool HouseSealed;
        public List<Prop> Props = new List<Prop>();           // Logic.Check reads the face reading (fakes) or the fact (true)
        public List<Prop> BackProps = new List<Prop>();       // what it proves once turned (fakes' 드러난 사실)
        public PlateState State; public List<string> History = new List<string>();
    }

    [Serializable]
    public sealed class DeckClaim
    {
        public string Id; public int Layer;          // 1 first impression, 2 mid reversal, 3 truth-side
        public Axis Axis; public string Text, Truth, Holder, Trick;
        public Prop Presented, Actual;
    }

    [Serializable]
    public sealed class DeckMystery { public string Id; public Axis Kind; public string Text, Claim; public List<string> Plates = new List<string>(); }

    // ------------------------------------------------------------------ the debate
    public enum BeatKind
    {
        Line, Slide, Floor, Result, Refusal, Cascade, Plaque, Bell, Headcount, Recall, Stage, Counter, Tell, Break, Film, Summary, Vote,
        Verdict, Confession, Execution, Develop, System, Interrupt, Inner
    }

    /// <summary>What a theory stands on, shown as its chip: 봤다 · 들었다 · 짐작 · ○○에게 들었다 · 규칙 · 은판.</summary>
    public enum Basis { Saw, Heard, Guess, Hearsay, Rule, Plate }

    /// <summary>Theory families (§6.4): the shape of a guess, filled with the holder's own perception.</summary>
    public enum Family { Self, Accident, Moved, Blame, TimeSlip, HiddenWay, Tangent, Truth }

    public enum ActionKind { Show, Ask, Stage, Listen, Accept, Infer, Accuse, Defer, Vote, Pass }

    [Serializable]
    public sealed class Theory
    {
        public string Id; public string Mystery; public string Holder; public Family Family; public Basis Basis; public string From;   // Hearsay: whose words
        public Prop Claim;           // exactly one checkable claim
        public string Text;          // what the holder says (their words, their perception)
        public string Fragment;      // the concrete perception it cites ("11시 종 치고 바로 비명…")
        public string Pin;           // a plate it leans on (id) — a fake's user, or a true plate's reading
        public bool True;            // HIDDEN: does the claim hold in the truth (director and metrics only)
        public string Lie;           // the culprit's pack lie id when this slide is a lie
        public string State = "standing";   // standing · collapsed · sealed · evolved · refused
        public string EvolvedFrom;
        public List<string> Supporters = new List<string>();
        public float Conviction; public int Asked; public int Misses;
    }

    [Serializable]
    public sealed class Mystery
    {
        public string Id; public Axis Axis; public string Title; public string Issue; public string Claim;   // DeckClaim id
        public string State = "dim";   // dim · lit · settled · deferred
        public List<string> Theories = new List<string>(); public int Misses; public string Reframe; public string Plaque;
        public bool Key;               // the aha: settles through 그렇다면…
    }

    [Serializable]
    public sealed class Plaque { public string Id, Mystery, Text, By; public Prop Prop; }

    /// <summary>The culprit's fight (§6.6), and the same selector's state for anyone accused.</summary>
    [Serializable]
    public sealed class CulpritMind
    {
        public string Actor; public int Budget, Spent; public List<string> LiesUsed = new List<string>();
        public int Fallback; public string Scapegoat; public bool Slipped; public bool Broken;
        public int Candles, CandlesLit;                     // lit only at 지목, on the accused
        public bool Chance, Means, Deceit;                  // knots (기회 · 수단 · 거짓) filled by proof
        public List<string> Counters = new List<string>();  // counter kinds used, in order
    }

    [Serializable]
    public sealed class LeanDelta { public string Actor, From, To; }

    [Serializable]
    public sealed class DebateBeat
    {
        public long Seq; public BeatKind Kind; public bool Readable, Counted; public float Secs;
        public string Speaker, Text, LineKey, Mystery, Theory, Plate, Plate2, Knot, Stinger, Target;
        public string Face;          // Emotion name for the speaker's face ("Angry", "Fear", …) or a dark face ("CorneredStare")
        public string Gesture;       // Anim name for the body ("Point", "CrossArms", "Slam", …)
        public float Intensity;      // 0..1 tension hint for music and camera
        public List<LeanDelta> Lean;
    }

    [Serializable]
    public sealed class DebateOption
    {
        public ActionKind Kind; public string Label; public string Theory; public string Plate; public string Plate2; public string Actor; public string Inference;
        public bool Enabled = true; public string Why;
    }

    [Serializable]
    public sealed class DebateInput { public ActionKind Kind; public string Theory, Plate, Plate2, Actor, Inference; }

    [Serializable]
    public sealed class DebateResult { public string Outcome; public string Text; public bool Progress; }

    [Serializable]
    public sealed class DebateDecision { public int Beat; public ActionKind Kind; public bool Counted; public string Detail, Outcome; public int Options; }

    [Serializable]
    public sealed class DebateState
    {
        public string Incident; public string Victim; public string Target;        // the case judged; the culprit (HIDDEN)
        public CaseDeck Deck;
        public List<Mystery> Mysteries = new List<Mystery>();
        public List<Theory> Theories = new List<Theory>();
        public List<Plaque> Plaques = new List<Plaque>();
        public CulpritMind Mind = new CulpritMind();
        public Dictionary<string, string> Reading = new Dictionary<string, string>();   // juror → whom they lean toward ("?" unsure)
        public List<string> Standing = new List<string>();                              // name plates still standing
        public List<DebateBeat> Beats = new List<DebateBeat>(); public int Cursor;
        public string Act = "prologue";   // prologue · act1 · act2 · act3 · act4 · done
        public string Step;               // director's step inside the act
        public string Active;             // lit mystery id
        public string Focus;              // focused theory id when a floor is open
        public List<string> Chips = new List<string>();
        public bool FloorOpen; public string FloorKind;   // "theory" · "infer" · "accuse" · "counter" · "vote"
        public string Accused; public string Verdict; public bool Correct;
        public List<string> SpokenKeys = new List<string>();    // speaker|key|text hashes already said (no line twice per speaker)
        public List<DebateDecision> Decisions = new List<DebateDecision>();
        public int ReadableSinceDecision; public int Agreements; public int Reversals;
        public List<string> Seen = new List<string>();          // novelty tags used this 심판 (counter:, theory:, axis:)
        public long Seq;
    }
}
