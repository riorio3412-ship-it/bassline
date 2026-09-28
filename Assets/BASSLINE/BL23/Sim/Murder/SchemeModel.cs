using System;
using System.Collections.Generic;

namespace BL23.Sim
{
    // =====================================================================================================================
    // The proactive culprit mind (Initiative*.cs) — saved state. One Scheme per resident who has decided to kill:
    //   motive (who, why, what pushed them) → the opportunity they found or MADE (a hosted evening, the house's scheduled
    //   darkness, a meal, the victim's habit, the investigation itself…) → the preparation, done as visible daily-life acts
    //   that leave tells (a knife taken, a clock set, a raincoat borrowed, a rumour planted) → the strike (a MurderPlan the
    //   existing kill machinery runs) → the cover (first to "find" the body, the story kept straight).
    // CaseApi reads it back as the 심판's "truth vs presented story" record (Documentation/BL23/CaseTruthApi.md).
    // Every list has one owner; strings are ids or vocabulary words (append-only, see CaseTruthApi.md §3).
    // =====================================================================================================================

    [Serializable]
    public sealed class Scheme
    {
        public string Id, Culprit, Victim;
        /// <summary>Motive (CaseTruthApi §3.1), the event that pushed them (plain Korean), the person they act for
        /// (protect/love/avenge), the incident they copy (copycat), the underlying everyday motive of a copycat.</summary>
        public string Motive, Trigger, Protects, CopyOf, BaseMotive;
        public float Pressure;
        /// <summary>Meticulous | Theatrical | Practical | Impulsive | Technical (Catalog/SchemeStyles.cs).</summary>
        public string Style;
        /// <summary>Designing → Preparing → Striking → Covering → Done | Abandoned.</summary>
        public string State = "Designing";
        public int Loop, Chapter;
        public double Formed = -1, Designed = -1, StrikeAt = -1, Struck = -1, Killed = -1, Ended = -1, NextPrep = -1, NextCheck = -1;
        public string EndWhy;

        // ---- the design (Initiative.Design)
        public string Moment, MomentRef, MomentText, Approach, Head;
        public int MomentRoom = -1, KillRoom = -1, EventRoom = -1;
        public double MomentAt = -1, MomentEnd = -1;
        public string Weapon, WeaponType; public int WeaponStashF = -1;
        /// <summary>The culprit's own event (a Gathering they host) or the one they joined.</summary>
        public string EventId, EventKind, EventLabel;
        /// <summary>What the victim is sent to fetch (errand) — "와인 한 병", "새 카드 한 벌".</summary>
        public string Pretext;
        /// <summary>crowd | witness | clock | recorder | helper | none — and the arranged witness, where and when.</summary>
        public string Alibi, AlibiWitness; public int AlibiRoom = -1; public double AlibiAt = -1;
        public int ClockF = -1; public double ClockShift;
        /// <summary>Who takes the blame, how (token | weapon | summon | rumor | note | dead), and the planted thing.</summary>
        public string Scapegoat, Frame, FrameItem, Frame2;
        /// <summary>An unwitting helper (a favour: dim the lights at nine, fetch something at the same time as the victim).</summary>
        public string Helper, HelperTask;
        public string Garb, MarkItem, Poison;
        /// <summary>Darkness for a dark strike: house (CH03/CH23) | helper | self.</summary>
        public string DarkBy;
        /// <summary>Turnabout: the victim's own scheme against the culprit, and the victim's stashed weapon the culprit turns.</summary>
        public string VictimScheme;
        public List<string> Variants = new List<string>();
        /// <summary>Rule shields chosen at planning time: "rule:tactic" (CaseTruthApi §3.5).</summary>
        public List<string> Shields = new List<string>();
        public List<PrepTask> Prep = new List<PrepTask>();
        public List<string> Beats = new List<string>();
        public List<string> Planted = new List<string>();
        public List<string> Guests = new List<string>();
        public string Plan; public List<string> Plans = new List<string>();
        public int Redesigns, Strikes;
        public int LieBudget;
        public string Shape;
        public List<string> Log = new List<string>();

        public bool Has(string v) => Variants.Contains(v);
        public void Var(string v) { if (v != null && !Variants.Contains(v)) Variants.Add(v); }
        public void Shield(string rule, string tactic) { var k = rule + ":" + tactic; if (!Shields.Contains(k)) Shields.Add(k); }
        public bool Open => State != "Done" && State != "Abandoned";
        public bool PreStrike => State == "Designing" || State == "Preparing";
    }

    /// <summary>One preparation act (CaseTruthApi §3.4): done in daily life, before the moment, with a motion.</summary>
    [Serializable]
    public sealed class PrepTask
    {
        public string Kind, Item, Target, Note; public int Room = -1, Furniture = -1;
        public double NotBefore = -1, Due = -1, DoneAt = -1;
        public bool Done, Failed, Essential; public int Tries; public string Beat;
    }

    /// <summary>A favour an unwitting helper agreed to do at a time (the helper never knows what it was for).</summary>
    [Serializable]
    public sealed class Favour
    {
        public string Id, Scheme, Helper, Kind, Item, Target, Text; public int Room = -1, Circuit = -1;
        public double At = -1, Until = -1, DoneAt = -1; public bool Done, Failed, Started;
    }
}
