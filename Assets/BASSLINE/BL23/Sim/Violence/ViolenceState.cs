using System;
using System.Collections.Generic;

namespace BL23.Sim
{
    // ======================================================================================================================
    // Violence & physicality track (Documentation/BL23/ViolenceMotionContract.md): the kernel state of prolonged kills,
    // restraints and firearms. Everything here is plain serializable data (GameState.Violence); the systems that drive it
    // live in Sim/Violence/*.cs. Deterministic: time is counted in kernel ticks (10 Hz), randomness comes from a seeded Rng
    // stored with each assault, or from S.R(Stream.Combat).
    // ======================================================================================================================

    /// <summary>How a prolonged kill is done. StrangleManual = both hands on the throat, face to face; StrangleRear = a cord
    /// from behind; StrangleFront = a cord from the front; Smother = something pressed over the face of someone lying down;
    /// Drown = a fist in the hair forcing the head into water (pool, sink, shallow water, bath, well).</summary>
    public enum AssaultKind { StrangleManual, StrangleRear, StrangleFront, Smother, Drown }

    /// <summary>Approach → Seize → Struggle → Weaken → Unconscious → Dead; Released / Escaped end it early (the victim lives).</summary>
    public enum AssaultPhase { Approach, Seize, Struggle, Weaken, Unconscious, Dead, Released, Escaped }

    /// <summary>One prolonged, time-true assault. The Game reads <see cref="Intensity"/> (how hard the victim fights this
    /// tick, 0..1) and <see cref="Phase"/> every frame; the kernel alone decides durations and the outcome.</summary>
    [Serializable]
    public sealed class Assault
    {
        public string Id; public AssaultKind Kind; public AssaultPhase Phase;
        public string Attacker, Victim, Plan, Tool;          // Tool: the cord / pillow item (null = bare hands)
        public int Water = -1; public string WaterType;      // drowning: the water furniture
        public long Approach = -1, Begun, PhaseAt, Ended = -1; // ticks
        public int SeizeT, StruggleT, WeakenT, HoldT;         // planned phase lengths (ticks)
        public float Air = 1f, Fight = 1f, Grip = 1f, Intensity;
        public float VStr, AStr;
        public bool Surprise, Asleep, Sedated, Bound, Seated;
        public int Scratches, Bites, Knocks, Splashes, Noises, Resists;
        public P3 From, At, AttAt; public float Yaw, AttYaw;   // victim dragged From → At (drowning), held there; attacker at AttAt
        public string Outcome;
        public Rng R;
        public List<string> Log = new List<string>();
        public bool Active => Phase <= AssaultPhase.Unconscious;
        public float Seconds(long tick) => (tick - Begun) / (float)SimTime.PerSecond;
    }

    /// <summary>Wrists / ankles / a gag on one person. The binding item is on the body (Item.Holder = the bound person, in no
    /// hand or pocket) until it comes off, when it drops at their feet (evidence either way).</summary>
    [Serializable]
    public sealed class Binding
    {
        public string Actor, By, Item, Material; public bool Wrists, Ankles, Gag;
        public long Tick; public double Clock; public float Tight = 0.8f, Loose;
        public float HandLWas = 1f, HandRWas = 1f, MobilityWas = 1f, SpeechWas = 1f;
        public bool Off, Marked; public double OffClock = -1; public string OffBy, Why;
        public double LastCry = -1;
    }

    /// <summary>The mechanical state of one firearm / crossbow item.</summary>
    [Serializable]
    public sealed class GunState
    {
        public string Item; public int Loaded; public int Spent; public int Shots; public long ReadyAt; public double LastShot = -1; public string LastBy;
    }

    /// <summary>One shot (or one shotgun blast): where it came from, where it went, what it hit. Kept for presentation (tracer,
    /// muzzle flash, impact decals, recoil) and for the reveal.</summary>
    [Serializable]
    public sealed class ShotRecord
    {
        public string Id, By, Weapon, WeaponType, Intended;
        public P3 From; public float Y0, Yaw, Pitch; public double Clock; public long Tick; public int Room = -1;
        public List<ShotImpact> Impacts = new List<ShotImpact>();
        public bool Silent;
    }

    [Serializable]
    public sealed class ShotImpact
    {
        public string Kind;          // body / exit / wall / door / furniture / floor / ceiling / void / miss
        public string Actor; public BodyRegion Region; public int Sev; public int Furniture = -1, Door = -1;
        public P3 At; public float Y, Dist; public bool Through; public string Item;   // Item: the bolt that stuck there
    }

    /// <summary>A crossbow bolt stuck in a body, a wall or a piece of furniture (the Game shows it where the kernel says).</summary>
    [Serializable]
    public sealed class Embedded
    {
        public string Item, Actor; public BodyRegion Region; public int Furniture = -1, Door = -1;
        public P3 At; public float Y, Yaw, Pitch;
    }

    [Serializable]
    public sealed class ViolenceState
    {
        public List<Assault> Assaults = new List<Assault>();
        public List<Binding> Bindings = new List<Binding>();
        public List<GunState> Guns = new List<GunState>();
        public List<ShotRecord> Shots = new List<ShotRecord>();
        public List<Embedded> Embedded = new List<Embedded>();
        public int Next;
    }
}
