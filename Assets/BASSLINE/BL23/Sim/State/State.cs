using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    [Serializable]
    public sealed class Wound
    {
        public BodyRegion Region; public DamageType Type; public int Sev; public float lx, ly, lz;
        // World-space incoming momentum and surface normal, optional for old/scripted wounds.
        public bool HasContact, ResolvedByPhysics; public float dx, dy, dz, nx, ny, nz, ContactImpulse, ContactEnergy;
        public bool Postmortem; public long Tick; public double Clock; public string By; public string Weapon; public string CauseEvent; public bool Treated;
        public string Kor => WoundText.Describe(this);
    }

    /// <summary>Functional channels are separate — there is no single HP. See WoundProfiles for the combination rule.</summary>
    [Serializable]
    public sealed class Body
    {
        public List<Wound> Wounds = new List<Wound>();
        public float Mobility = 1, Resistance = 1, HandL = 1, HandR = 1, Conscious = 1, Speech = 1;
        public float Bleed;             // blood loss per clock minute (0..)
        public float BloodLoss;         // 0..1, death at 1
        public double DeathAt = -1;     // scheduled death (clock), -1 none
        public double UnconsciousUntil = -1;
        public bool Stabilized; public bool Dead; public double DeathClock = -1; public string DeathCause; public string DeathBy;
        public int DeathRoom = -1; public long DeathSeq;
        public double TodShift;         // minutes the body reads as dying later (+) or earlier (-) than it did: heat / cold manipulation
        public string PoisonBy; public double PoisonedAt = -1;   // a dose in food or drink: who gave it, when
        // ---- gore (Sim/Systems/Gore.cs): blood / struggle marks on and around this body; null until used (old saves load)
        public List<GoreMark> GoreMarks; public int GoreRev;
        public bool Critical => !Dead && (DeathAt >= 0 || Bleed > 0.02f);
    }

    [Serializable]
    public sealed class Needs
    {
        public float Hunger = 0.2f, Energy = 0.9f, Social = 0.5f, Fun = 0.5f, Stress = 0.15f, Fear = 0f, Grief = 0f, Anger = 0f;
        public double LastMeal, LastSleep, LastTalk;
    }

    [Serializable]
    public sealed class ActionStep
    {
        public string Kind;         // GoTo, Wait, Activity, PickUp, Drop, Place, Talk, Open, Lock, Unlock, Attack, Carry, Release, Wash, Change, Operate, Examine, Ring, Follow, Flee, Hide, Say
        public P3 Target; public bool HasTarget; public int Room = -1; public int Spot = -1; public int Door = -1; public int Furniture = -1;
        public string Actor; public string Item; public string Tag; public string Data;
        public double Duration;     // clock minutes
        public bool Run; public Anim Anim;
    }

    [Serializable]
    public sealed class Activity
    {
        public string Id;           // intent id: eat, sleep, hobby:read, social:P05, goal:G03, murder:plan1, investigate, flee...
        public string Label;        // Korean label shown when asked "what are you doing"
        public List<ActionStep> Steps = new List<ActionStep>();
        public int Index; public double StepStart = -1; public double StepEnd = -1;
        public List<P3> Path; public int PathIdx; public List<int> PathDoors; public List<int> PathStairs;
        public double Priority; public bool Interruptible = true; public bool Secret;
        public int Failures;
        public ActionStep Cur => Index >= 0 && Index < Steps.Count ? Steps[Index] : null;
    }

    [Serializable]
    public sealed class Actor
    {
        public string Id; public ActorStatus Status = ActorStatus.Active;
        public P3 Pos; public float Yaw; public int Room = -1;
        public Pose Pose = Pose.Stand; public int Spot = -1; public Anim Anim = Anim.Idle; public Emotion Emotion = Emotion.Neutral;
        public float Speed; public bool Running;
        public string HandR, HandL; public List<string> Pocket = new List<string>();
        public string Disguise;         // worn item id (mask/cloak/raincoat)
        public string BorrowedOutfitOf; // IG06 borrowed coat look
        public float BloodOnClothes; public bool Wet; public double WetUntil;
        public Body Body = new Body(); public Needs Needs = new Needs();
        // Optional in older saves. Narrative Body remains the authority for life/death and evidence.
        public Physicality.PhysicalBody Physical;
        [NonSerialized, Newtonsoft.Json.JsonIgnore] public bool PhysicsDriven;
        public Activity Act; public string Following; public string FollowedBy; public string Carrying; public string CarriedBy;
        public string TalkingTo; public double BusyUntil;
        public double StairUntil; public int StairId = -1; public P3 StairFrom, StairTo;
        public string Ability; public int AbilityCharges; public double AbilityCooldownUntil;
        public List<string> GoalIds = new List<string>();
        public string PlanId;           // restricted: active murder plan
        public double NextThink; public double NextPerceive; public double NextSocial;
        public double Awake = 1;        // asleep when in Sleep pose
        public string LastSaid; public double LastSaidAt;
        public int ExecutedChapter = -1; public int EscapedChapter = -1;
        public bool Alive => Status == ActorStatus.Active || Status == ActorStatus.Unconscious;
        public bool Present => Alive;
        public bool IsPlayer => Id == Cast.Player; public bool IsButler => Id == Cast.Butler;
        public CastDef Def => Cast.Get(Id);
    }

    [Serializable]
    public sealed class Rel
    {
        public float Like, Trust, Respect, Attach, Romance, Fear, Depend, Jealous, Grudge;
        public bool Casual; public int Talks; public double LastTalk = -999;
        public HashSet<string> Tags = new HashSet<string>();   // friend, colleague, lover, rival, enemy, grudge, family, rejected, promise:*
        public List<string> Memory = new List<string>();       // short experience log (Korean), newest last
        public double Opinion => Like * 0.45 + Trust * 0.35 + Respect * 0.2 + Attach * 0.3 - Grudge * 0.6 - Fear * 0.2;
    }

    [Serializable]
    public sealed class Sighting
    {
        public string Target; public int Room; public double T0, T1; public float IdConf; public bool Dead; public bool Unconscious;
        public string Held; public bool Bloody; public bool Carrying; public string Disguise; public bool Running; public bool Attacking; public string Victim;
        public bool Direct = true; public string Root;
    }

    [Serializable]
    public sealed class HeardSound
    {
        public SoundKind Kind; public int Room; public int GuessRoom; public double Clock; public float Loud; public string Voice; public float VoiceConf; public string Root;
    }

    [Serializable]
    public sealed class Statement
    {
        public string Id; public string Speaker; public string Listener; public double Clock; public Prop Prop; public bool Hearsay; public string OrigSource; public string Root; public string Text; public bool Lie;
    }

    [Serializable]
    public sealed class Prop
    {
        public PropKind Kind; public string A; public string B; public int Room = -1; public string Item; public double T0, T1; public string Value;
        public Prop Clone() => (Prop)MemberwiseClone();
        public override string ToString() => $"{Kind}({A},{B},r{Room},{Item},{ClockFmt.HM(T0)}-{ClockFmt.HM(T1)},{Value})";
    }

    [Serializable]
    public sealed class Evidence
    {
        public string Id; public string Owner; public EvKind Kind; public string Title; public string Desc; public string Source; public bool Direct = true;
        public string Root; public double T0, T1; public int Room = -1; public List<Prop> Props = new List<Prop>();
        public string CanKnow; public string CannotKnow; public double Acquired; public int Loop; public int Chapter; public bool Copy; public string TraceId;
        public string Subject; public bool Important; public List<string> SharedWith = new List<string>(); public bool Pinned;
        /// <summary>Folded into another card (a body's seen/official halves) or superseded by a direct look (a hearsay copy):
        /// kept for provenance, never listed, presented or used by the autopilot.</summary>
        public bool Hidden;
        /// <summary>A look that turned up nothing notable: returned by examine calls for the caption, never stored.</summary>
        public bool Loose;
        /// <summary>One-sentence headline shown under the title.</summary>
        public string Line;
        /// <summary>Witness sheets only: what the witness said, oldest first (at most 8).</summary>
        public List<string> Quotes = new List<string>();
    }

    [Serializable]
    public sealed class Knowledge
    {
        public List<Sighting> Sightings = new List<Sighting>();
        public List<HeardSound> Heard = new List<HeardSound>();
        public List<Statement> Statements = new List<Statement>();
        public List<Evidence> Evidence = new List<Evidence>();
        public Dictionary<int, bool> DoorLocked = new Dictionary<int, bool>();
        public Dictionary<string, (int room, double t)> ItemSeen = new Dictionary<string, (int, double)>();
        public HashSet<string> Facts = new HashSet<string>();
        public HashSet<string> KnownDead = new HashSet<string>();
        public HashSet<string> Examined = new HashSet<string>();
        public Dictionary<string, float> Suspicion = new Dictionary<string, float>();
        public Dictionary<string, (int room, double t)> LastSeen = new Dictionary<string, (int, double)>();
        // ---- furniture (Sim/Systems/FurnitureChanges.cs): what this person saw done to the furniture, or noticed later.
        // Never the hidden cause: Who is only ever someone this person actually saw doing it.
        public List<FurnitureNote> FurnitureNotes = new List<FurnitureNote>();
        public Dictionary<int, FurnitureMemory> FurnitureKnown = new Dictionary<int, FurnitureMemory>(); // changed pieces only: how this person last knew them
        public Dictionary<int, double> RoomSeenAt = new Dictionary<int, double>();    // room id → the last time this person stood in it (own memory)
        [NonSerialized] public Dictionary<string, Sighting> Open;   // rebuilt from Sightings on load
    }

    public enum FurnitureNoteKind { Did, Saw, Noticed }

    /// <summary>How one person last knew a piece of furniture that has changed at least once (Sim/Systems/FurnitureChanges.cs).</summary>
    [Serializable]
    public sealed class FurnitureMemory
    {
        public int Rev;             // the change they have taken in
        public P3 Pos; public float Yaw; public int Damage;
        public string Disorder;     // null / toppled / rumpled
        public double Seen;         // the last time they were in its room with it like this
    }

    /// <summary>One person's knowledge of one change to a piece of furniture: did it themselves, saw it happen, or noticed it later.</summary>
    [Serializable]
    public sealed class FurnitureNote
    {
        public int Furniture; public int Room = -1; public FurnitureNoteKind Kind;
        public string How;          // moved / damaged / toppled / rug / rearranged (what was done, as far as it shows)
        public string Who;          // Saw: the person seen doing it (may be a misread coat); null when nobody was seen. Noticed: always null
        public float WhoConf;
        public double Clock;        // when this person took it in
        public double Since = -1;   // Noticed: the last time this person had seen it before (-1 = did not know the room)
        public int Rev;             // the change this note is about
        public string Root;
    }

    [Serializable]
    public sealed class Trace
    {
        public string Id; public string Type; public P3 Pos; public int Room; public double Clock; public long Seq; public float Size = 0.4f;
        public string Source;    // restricted cause actor id
        public string Victim; public string Item; public float Dir;
        public int Visibility = 1;   // 0 obvious, 1 near look, 2 detailed exam, 3 ability only
        public bool Cleaned; public double CleanedAt; public bool Blurred; public bool Preserved;
        public string Desc;      // public description when examined
        public string Know, Unknown;
        public string Note;      // trick bookkeeping (e.g. fake dying message: "target=P12;hand=L"), never shown directly
    }

    [Serializable]
    public sealed class LedgerEvent
    {
        public long Seq; public long Tick; public double Clock; public string Type; public string Actor; public string Target; public string Item; public int Room = -1; public P3 Pos; public string Data;
        public string Plan; public int Chapter; public bool Secret;
    }

    [Serializable]
    public sealed class GameEvent
    {
        public GameEventType Type; public string Actor; public string Target; public string Text; public string Key; public P3 Pos; public int Room = -1; public int Id = -1;
        public float Value; public string Data; public bool Audible; public long Tick; public double Clock;
    }

    [Serializable]
    public sealed class Incident
    {
        public string Id; public int Loop; public int Chapter; public string Victim; public string Culprit; public string PlanId; public string Method;
        public string Weapon; public string WeaponType; public DamageType Dmg; public BodyRegion Region;
        public int CauseRoom = -1, DeathRoom = -1, FoundRoom = -1; public double CauseClock = -1, DeathClock = -1, DiscoverClock = -1, ConfirmClock = -1;
        public long ResultSeq; public bool Discovered, Confirmed, Murder, Accident, BodyMoved, Mutilated, Rescued;
        public List<string> Discoverers = new List<string>(); public List<string> Notes = new List<string>();
        public long SegmentStartTick, SegmentEndTick; public bool ProcedureClosed, SettlementApplied, RevealEligible;
    }

    [Serializable]
    public sealed class PlanStep { public string Kind; public string Item; public int Room = -1; public int Door = -1; public int Furniture = -1; public string Target; public double Until; public string Note; public bool Done; }

    [Serializable]
    public sealed class MurderPlan
    {
        public string Id; public string Actor; public string Target; public string Motive; public string Method; public string Grammar;
        public List<PlanStep> Steps = new List<PlanStep>(); public int Step; public string Stage = "Forming";
        public string Weapon; public string WeaponType; public int KillRoom = -1; public int AlibiRoom = -1; public int DumpRoom = -1; public string Disguise; public string Accomplice;
        public double Formed, Deadline; public int Tries; public string Reservation; public List<string> Log = new List<string>();
        public List<string> Alternatives = new List<string>(); public string Reason; public long StartTick;
        public string AlibiClaimRoomName; public int AlibiClaimRoom = -1; public string LieTarget;
    }

    [Serializable]
    public sealed class RuleInstance
    {
        public string Id; public string Rule; public int Chapter; public string Name; public string Desc; public bool Major; public string Kind;
        public List<string> Targets = new List<string>(); public List<double> Times = new List<double>(); public int Param; public bool Active = true; public bool Announced;
        public HashSet<string> HeardBy = new HashSet<string>(); public string Data;
    }

    [Serializable]
    public sealed class Reservation { public string Id; public string Actor; public string Target; public string Plan; public bool Released; public bool Consumed; }

    [Serializable]
    public sealed class ChapterState
    {
        public int StartN; public int VictimCap; public List<RuleInstance> Rules = new List<RuleInstance>();
        public List<Reservation> Reservations = new List<Reservation>(); public List<string> Incidents = new List<string>();
        public double InvestigationEnd = -1; public int Extensions; public double FirstAnnounce = -1; public string TargetIncident; public string TargetCulprit;
        public bool UnexpectedCasualty; public double ChapterStartClock; public int DaysElapsed;
        public List<string> PublicVotes = new List<string>();
    }

    [Serializable]
    public sealed class AbilityAssignment { public string Actor; public string Ability; public int Loop; public int Charges; public double CooldownUntil; public List<string> Uses = new List<string>(); }

    [Serializable]
    public sealed class Settlement
    {
        public string Id; public int Loop; public int Chapter; public string Accused; public bool Correct; public string Culprit; public string Executed; public string Escaped; public string Drawn;
        public bool Exception; public bool Applied; public Dictionary<string, string> Votes = new Dictionary<string, string>(); public int Survivors; public string Note; public int Grade; public int Exp;
    }

    [Serializable]
    public sealed class Announcement { public double Clock; public string Key; public string Text; public string Rule; public List<string> Heard = new List<string>(); }

    [Serializable]
    public sealed class Goal
    {
        public string Id; public string Owner; public string Kind; public string Label; public int Stage; public int Stages; public float Progress; public List<string> Helpers = new List<string>();
        public string Room; public bool Done; public bool Abandoned; public bool PlayerAccepted; public string Handover;
    }

    [Serializable]
    public sealed class Profile
    {
        public int Level = 1; public int Exp; public int Points; public HashSet<string> Nodes = new HashSet<string>(); public int Runs; public List<string> Archive = new List<string>();
        public int FloorPreset = 4; public int Gore = 2; public int Assist = 1;
    }

    [Serializable]
    public sealed class ReplayFrame
    {
        // per actor: x,z,f,yaw,pose,anim,alive,carry — packed as Int16 (x/z in 2cm) to keep saves small
        public long Tick; public double Clock; public string P;
        [NonSerialized, Newtonsoft.Json.JsonIgnore] float[] _d;
        [Newtonsoft.Json.JsonIgnore]
        public float[] Data
        {
            get
            {
                if (_d == null && P != null)
                {
                    var b = Convert.FromBase64String(P); _d = new float[b.Length / 2];
                    for (int i = 0; i < _d.Length; i++) { short v = (short)(b[2 * i] | (b[2 * i + 1] << 8)); _d[i] = (i % Replay.Stride) < 2 ? v / 50f : v; }
                }
                return _d;
            }
            set
            {
                _d = value; var b = new byte[value.Length * 2];
                for (int i = 0; i < value.Length; i++) { float f = (i % Replay.Stride) < 2 ? value[i] * 50f : value[i]; short v = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Math.Round(f))); b[2 * i] = (byte)(v & 0xff); b[2 * i + 1] = (byte)((v >> 8) & 0xff); }
                P = Convert.ToBase64String(b);
            }
        }
    }
    [Serializable]
    public sealed class ReplaySegment { public string Incident; public long T0, T1; public List<ReplayFrame> Frames = new List<ReplayFrame>(); public List<LedgerEvent> Events = new List<LedgerEvent>(); public List<string> Actors = new List<string>(); public string LayoutHash; }
}
