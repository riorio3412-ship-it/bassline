using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    // =====================================================================================================================
    // Murder foundation — the case record (Documentation/BL23/MurderFoundation.md §B/§C/§D, skeleton v1 "S" section).
    // One CaseFile per composed murder plan: the design the culprit chose (act + tricks + moment + archetype), the tricks as
    // they really ran, and — once the House confirms the death — the record the 심판 reads: the presented story in layers,
    // the exposing paths (fair play), a small key-clue set, open questions with residents' theories, and what each witness
    // perceived. Saved in GameState.Mur. Every list has one owner (no shared references: Newtonsoft does not preserve them).
    // =====================================================================================================================

    /// <summary>The fact a trick falsifies (append-only: saved as ints).</summary>
    public enum Axis { Time, Place, Identity, Cause, Means, Access, Sequence }

    /// <summary>Where an exposing observation comes from (B body · S scene · O object · R record · W witness · X experiment · P power).</summary>
    public enum Channel { Body, Scene, Object, Record, Witness, Experiment, Power }

    /// <summary>A trick's life: chosen → its setup step ran → it is in force (the false fact stands) → broken / failed / dropped.</summary>
    public enum RunState { Planned, SetUp, Active, Exposed, Failed, Dropped }

    [Serializable]
    public sealed class MurderWorld
    {
        public List<CaseFile> Cases = new List<CaseFile>();
        /// <summary>Preparation moments in daily life that someone saw (or could have): the culprit's tells (Initiative*.cs).</summary>
        public List<SchemeBeat> Beats = new List<SchemeBeat>();
        /// <summary>The proactive culprit minds (Initiative*.cs): motive → opportunity → preparation → strike → cover.</summary>
        public List<Scheme> Schemes = new List<Scheme>();
        /// <summary>Favours an unwitting helper promised to do at a given time (dim the lights for the film, fetch the wine…).</summary>
        public List<Favour> Favours = new List<Favour>();
        /// <summary>What the player has already been shown across loops (and, when the Game carries it, across runs).</summary>
        public NoveltyMemory Novelty = new NoveltyMemory();
        /// <summary>Lab bias (tests and probes only): a catalogue id the composer uses whenever it is feasible. Saved, so a
        /// forced run continues identically after a save/load. Never lifts the planner's knowledge limits.</summary>
        public string LabForce;
        /// <summary>Ledger sequence the case tracker has already read.</summary>
        public long Cursor;
        /// <summary>Composer evaluations so far (a jitter key — candidate order never depends on stream state).</summary>
        public int Evals;
        public int NextId = 1;

        public CaseFile ByPlan(string plan) { if (plan == null) return null; for (int i = Cases.Count - 1; i >= 0; i--) if (Cases[i].Plan == plan) return Cases[i]; return null; }
        public CaseFile ByIncident(string inc) { if (inc == null) return null; for (int i = Cases.Count - 1; i >= 0; i--) if (Cases[i].Incident == inc) return Cases[i]; return null; }
        public CaseFile ById(string id) { if (id == null) return null; for (int i = Cases.Count - 1; i >= 0; i--) if (Cases[i].Id == id) return Cases[i]; return null; }
    }

    [Serializable]
    public sealed class CaseFile
    {
        public string Id, Plan, Incident; public int Loop, Chapter;
        public string Culprit, Victim, IntendedVictim, Motive;
        // ---- the design (chosen by Composer.Plan)
        public string Archetype, Question, Moment, MomentRef, Scheme, Style;
        public string Approach, Subdue, Mechanism, Agent, AgentItem;
        public List<string> Disposals = new List<string>(), Stagings = new List<string>();
        public int Tier; public string Aha;
        public List<TrickRun> Runs = new List<TrickRun>();
        public string Grammar;                 // the legacy display label at formation (never parsed by new code)
        public string State = "Planned";       // Planned → Executing → Killed → Found → Confirmed | Aborted
        public bool Improvised, Forced;
        public double Formed = -1, Killed = -1, Found = -1, Confirmed = -1;
        public int KillRoom = -1, FoundRoom = -1;
        // ---- finalised when the House confirms the death (what the 심판 and the reveal read; CaseApi)
        public List<StoryLayer> Layers = new List<StoryLayer>();
        public List<ExposingPath> Paths = new List<ExposingPath>();
        public List<KeyClue> KeyClues = new List<KeyClue>();
        public List<OpenQuestion> Questions = new List<OpenQuestion>();
        public List<Fragment> Fragments = new List<Fragment>();
        public List<string> BeatIds = new List<string>();
        public string Logline, Signature, Hinge, HingeCover, Reframe, Scapegoat;
        public List<string> Log = new List<string>();

        public TrickRun Run(string trick) { foreach (var r in Runs) if (r.Trick == trick) return r; return null; }
        /// <summary>Tricks that actually ran (their setup happened and the false fact was put in place).</summary>
        public int Executed { get { int n = 0; foreach (var r in Runs) if (r.State == RunState.SetUp || r.State == RunState.Active || r.State == RunState.Exposed) n++; return n; } }
    }

    [Serializable]
    public sealed class TrickRun
    {
        public string Trick; public string Role;       // aha | support | inherent | improv | decoy
        public Axis Axis; public RunState State; public string Exec;
        public string Presented, Truth;                 // plain Korean claims (the false story and what really happened)
        public long Seq = -1; public double Clock = -1; public int Room = -1;
        public List<string> Seams = new List<string>(); // seam keys that were really left ("trace:tr12", "item:it40:stopped", "ledger:44")
        public string Fail;
    }

    [Serializable]
    public sealed class StoryLayer
    {
        public int N; public string Title, Story, Suspect, BrokenBy;
        public List<string> Claims = new List<string>();
    }

    [Serializable]
    public sealed class ExposingPath
    {
        public string Trick, Claim, Channels; public bool NpcIndependent, Found;
        public List<string> Roots = new List<string>();
    }

    [Serializable]
    public sealed class KeyClue { public string Key, Label, Meaning, Breaks, Origin; public bool Fake; public int Order; }

    [Serializable]
    public sealed class OpenQuestion
    {
        public string Id, Kind, Text, Answer, Reframe; public int Order;
        public List<TheorySeed> Theories = new List<TheorySeed>();
    }

    /// <summary>A hypothesis a resident would voice about an open question — built from what they perceived, believe and fear,
    /// and from their personality. The culprit's seed is a lie that protects the presented story.</summary>
    [Serializable]
    public sealed class TheorySeed { public string Holder, Kind, Text, Basis; public bool True, Lie, Culprit; public float Conviction; }

    /// <summary>One thing one witness perceived around the crime (a sound, a brush of cloth, a glint, a smell, a glimpse):
    /// the raw material of testimony and of the theories. Truth names what really caused it (never shown directly).</summary>
    [Serializable]
    public sealed class Fragment { public string Holder, Kind, Detail, Source, Truth; public double Clock; public int Room = -1; public float Conf; }

    /// <summary>An innocent-looking preparation moment in daily life that someone saw (borrowing thread at breakfast…).
    /// (Named SchemeBeat so the daily-life track can own the name "Foreshadow" for its API.)</summary>
    [Serializable]
    public sealed class SchemeBeat
    {
        public string Id, Plan, Actor, Beat, Item, Text; public int Room = -1; public double Clock;
        public List<string> Observers = new List<string>();
        /// <summary>Initiative: the scheme it belongs to, the prep kind, how it read at the time, what it really was.</summary>
        public string Scheme, Kind, Innocent, Meaning; public int Loop, Chapter;
    }

    [Serializable]
    public sealed class NoveltyMemory
    {
        /// <summary>How often the player has met each thing: "arch:X", "trick:Y", "mech:Z", "moment:M", "q:K", "shape:…".</summary>
        public Dictionary<string, int> Seen = new Dictionary<string, int>();
        /// <summary>Case shape signatures in the order the player met them (at most 120).</summary>
        public List<string> History = new List<string>();
        public int Cases, Runs;
        public int Count(string key) => key != null && Seen.TryGetValue(key, out var n) ? n : 0;
        public void Add(string key) { if (key == null) return; Seen[key] = Count(key) + 1; }
    }

    /// <summary>Deterministic, stream-free jitter: the same (campaign, loop, key) always gives the same number, whatever else
    /// drew from the RNG streams. Composer candidates use it so a catalogue addition never reshuffles every other seed.</summary>
    public static class MurderHash
    {
        public static double U01(GameState S, string key)
        {
            ulong h = Rng.Hash(key) ^ (S.Rng.CampaignSeed * 0x9E3779B97F4A7C15UL) ^ ((ulong)S.Loop * 0xC2B2AE3D27D4EB4FUL);
            h ^= h >> 33; h *= 0xff51afd7ed558ccdUL; h ^= h >> 33; h *= 0xc4ceb9fe1a85ec53UL; h ^= h >> 33;
            return (h >> 11) * (1.0 / 9007199254740992.0);
        }
    }
}
