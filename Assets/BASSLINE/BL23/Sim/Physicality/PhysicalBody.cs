using System;

namespace BL23.Sim.Physicality
{
    // Values are deliberately ordered: a minor reaction may never replace a protective response.
    public enum PhysicalReaction { None, Startle, Flinch, Guard, Stagger, Brace, Fall, Downed, Incapacitated }
    public enum PhysicalPosture { Standing, Staggering, Falling, Prone, Recovering, Incapacitated }

    /// <summary>Mechanical/animation injury channels, not a medical model or another death system.</summary>
    [Serializable]
    public sealed class RegionState
    {
        public BodyRegion Region;
        public float Bruise, Cut, Puncture, StructuralDamage;
        public bool Severed;
        public float Integrity => Severed ? 0f : 1f - Math.Max(StructuralDamage, Math.Max(Cut * 0.65f, Puncture * 0.45f));
    }

    /// <summary>
    /// Serializable optional state for Actor.Physical. All times are simulation SECONDS (not the
    /// existing story clock minutes). Existing Body/Combat remains authority for survival, wounds,
    /// bleeding and unconsciousness. Legacy limits are combined by minimum, never multiplied twice.
    /// </summary>
    [Serializable]
    public sealed class PhysicalBody
    {
        static readonly BodyRegion[] RegionValues = (BodyRegion[])Enum.GetValues(typeof(BodyRegion));
        static readonly int RegionCount = RegionValues.Length;
        public int Version = 1;
        public RegionState[] Regions = CreateRegions();
        public float Pain, Alarm;
        public float Balance = 1f;
        public float Consciousness = 1f;
        public float Mobility = 1f, GripLeft = 1f, GripRight = 1f;
        public float LegacyMobility = 1f, LegacyGripLeft = 1f, LegacyGripRight = 1f;
        public bool Dead;
        public PhysicalPosture Posture;
        public PhysicalReaction Reaction;
        public BodyRegion ReactionRegion = BodyRegion.Chest;
        public float ReactionStrength, ReactionRemaining;
        public float ReactionDirectionX, ReactionDirectionY, ReactionDirectionZ;
        public float ContactX, ContactY, ContactZ;
        public float PostureSeconds, StableGroundSeconds;
        public string LastSourceId;
        public long ReactionRevision;
        public bool CanAct => !Dead && Consciousness > 0.1f && Posture != PhysicalPosture.Incapacitated &&
                              Posture != PhysicalPosture.Falling && Posture != PhysicalPosture.Prone && Posture != PhysicalPosture.Recovering;
        public bool CanBrace => !Dead && Consciousness > 0.1f && Math.Max(GripLeft, GripRight) > 0.25f;

        static RegionState[] CreateRegions()
        {
            var result = new RegionState[RegionCount];
            for (int i = 0; i < result.Length; ++i) result[i] = new RegionState { Region = RegionValues[i] };
            return result;
        }

        public RegionState Region(BodyRegion region)
        {
            int index = (int)region;
            if (index < 0 || index >= RegionCount) throw new ArgumentOutOfRangeException(nameof(region));
            if (Regions == null || Regions.Length != RegionCount)
            {
                var old = Regions; Regions = CreateRegions();
                if (old != null) foreach (var part in old)
                    if (part != null && (int)part.Region >= 0 && (int)part.Region < Regions.Length) Regions[(int)part.Region] = part;
            }
            if (Regions[index] == null) Regions[index] = new RegionState { Region = region };
            return Regions[index];
        }
    }

    [Serializable]
    public sealed class PhysicalityTuning
    {
        public float BodyMassKg = 75f;
        public float BalanceImpulseScale = 1.8f; // loss = impulse / (mass * scale), before leverage
        public float BalanceRecoveryPerSecond = 0.7f;
        public float PainRecoveryPerSecond = 0.11f;
        public float AlarmHalfLifeSeconds = 2.5f;
        public float StaggerThreshold = 0.7f;
        public float FallThreshold = 0.22f;
        public float GripDropThreshold = 0.3f;
        public float MinimumFallSeconds = 0.35f;
        public float MinimumProneSeconds = 0.8f;
        public float GetUpSeconds = 1.25f;
        // Future personality/physique profiles may override these; shared defaults apply to everybody.
        public float PainSensitivity = 1f;
        public float StartleSensitivity = 1f;
        public float RecoverySpeed = 1f;
    }

    public struct ImpactInput
    {
        public BodyRegion Region;
        public DamageType Type;
        public float EnergyJoules;
        public float Impulse;             // magnitude in newton-seconds; direction belongs to the motor
        public float Leverage;            // 0 means default 1; height/stance multiplier
        public bool Supported;            // only true after a usable support has actually been found
        public bool TransientOnly;        // shove/legacy replay: no persistent tissue change
        public int SeverityHint;          // 0 derives from energy; existing Wound.Sev may be supplied
        public string SourceId;
        // Actor-local coordinates: +Z forward, +Y up. An incoming +Z impulse arrives from behind.
        // A zero direction or normal means unspecified and preserves the direction-neutral defaults.
        public float DirectionX, DirectionY, DirectionZ;
        public bool HasContact;
        public float ContactX, ContactY, ContactZ;
        public float NormalX, NormalY, NormalZ;
    }

    public struct ImpactResult
    {
        public PhysicalReaction Reaction;
        public int Severity;
        public float TissueDamage, BalanceLost, PainAdded;
        public float EnergyTransferred;
        public bool ShouldDropLeft, ShouldDropRight;
    }
}
