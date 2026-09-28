using System;

namespace BL23.Sim.Physicality
{
    /// <summary>Pure deterministic contact response, regional function, reaction arbitration and recovery.</summary>
    public static class BodySolver
    {
        static readonly PhysicalityTuning Defaults = new PhysicalityTuning();
        const float MaxStep = 1f / 120f;

        public static ImpactResult ApplyImpact(PhysicalBody body, ImpactInput impact, PhysicalityTuning tuning = null)
        {
            Require(body); var t = tuning ?? Defaults;
            var region = body.Region(impact.Region);
            float energy = Positive(impact.EnergyJoules), impulse = Positive(impact.Impulse);
            float dx = Signed(impact.DirectionX), dy = Signed(impact.DirectionY), dz = Signed(impact.DirectionZ);
            bool directional = Normalize(ref dx, ref dy, ref dz);
            float nx = Signed(impact.NormalX), ny = Signed(impact.NormalY), nz = Signed(impact.NormalZ);
            if (directional && Normalize(ref nx, ref ny, ref nz))
            {
                float normalFraction = Clamp01(Math.Abs(dx * nx + dy * ny + dz * nz));
                // Punctures require alignment; tangential cutting keeps an edge effective while a
                // glancing blunt strike transfers much less normal energy. These are game tunings.
                float transfer = impact.Type == DamageType.Stab ? normalFraction * normalFraction :
                    impact.Type == DamageType.Cut ? 0.45f + 0.55f * (float)Math.Sqrt(1f - normalFraction * normalFraction) :
                    0.15f + 0.85f * normalFraction * normalFraction;
                energy *= transfer;
            }
            int severity = Math.Max(0, Math.Min(4, impact.SeverityHint));
            if (severity == 0 && energy > 0.5f) severity = energy < 18f ? 1 : energy < 65f ? 2 : energy < 150f ? 3 : 4;
            if (energy == 0f && impulse == 0f && severity == 0) return Result(body, 0, 0, 0, 0, t);

            float dose = Math.Min(1f, energy / 240f);
            if (severity > 0) dose = Math.Max(dose, 0.04f + 0.09f * severity);
            float tissue = 0f;
            if (!impact.TransientOnly)
            {
                float old = region.Integrity;
                ApplyTissue(region, impact.Type, dose);
                tissue = Math.Max(0, old - region.Integrity);
            }
            // A corpse keeps damage channels, but does not experience pain/startle or recover.
            if (body.Dead) { RefreshFunctions(body); return Result(body, severity, tissue, 0, 0, t); }

            float pain = Clamp01((dose * 0.55f + Math.Min(0.12f, impulse / 600f)) * Positive(t.PainSensitivity));
            float oldPain = body.Pain, oldBalance = body.Balance;
            body.Pain = Clamp01(body.Pain + pain);
            body.Alarm = Math.Max(body.Alarm, Clamp01(0.15f + pain));
            float leverage = impact.Leverage > 0f && Finite(impact.Leverage) ? Math.Min(3f, impact.Leverage) : 1f;
            if (directional)
            {
                // Side and rear contacts are harder to catch with an ordinary forward stance.
                leverage *= 1f + Math.Abs(dx) * 0.15f + Math.Max(0f, dz) * 0.2f;
                if (impact.HasContact)
                {
                    // Horizontal off-centre contact supplies yaw moment; do not conflate height
                    // with radial distance (the adapter already supplies the height leverage).
                    float yawMomentArm = Math.Abs(Signed(impact.ContactX) * dz - Signed(impact.ContactZ) * dx);
                    leverage *= 1f + Math.Min(0.65f, yawMomentArm);
                }
            }
            float loss = impulse * leverage / (AtLeast(t.BodyMassKg, 1f) * AtLeast(t.BalanceImpulseScale, 0.1f));
            if (IsLeg(impact.Region)) loss *= 1.4f;
            if (impact.Supported && body.CanBrace) loss *= 0.55f;
            body.Balance = Clamp01(body.Balance - loss);
            RefreshFunctions(body);

            if (body.Posture == PhysicalPosture.Incapacitated) return Result(body, severity, tissue, oldBalance - body.Balance, body.Pain - oldPain, t);
            bool accepted;
            if (body.Posture == PhysicalPosture.Prone || body.Posture == PhysicalPosture.Falling)
                accepted = RequestReaction(body, body.Posture == PhysicalPosture.Prone ? PhysicalReaction.Downed : PhysicalReaction.Fall, impact.Region, Math.Max(pain, loss), 0.7f);
            else if (body.Balance <= Clamp01(t.FallThreshold))
            {
                SetPosture(body, PhysicalPosture.Falling);
                accepted = RequestReaction(body, PhysicalReaction.Fall, impact.Region, Math.Max(pain, loss), 0.7f);
            }
            else if (body.Balance < Clamp01(t.StaggerThreshold))
            {
                SetPosture(body, PhysicalPosture.Staggering);
                accepted = RequestReaction(body, impact.Supported && body.CanBrace ? PhysicalReaction.Brace : PhysicalReaction.Stagger,
                    impact.Region, Math.Max(pain, loss), 0.35f + 0.5f * Math.Min(1f, loss));
            }
            else accepted = RequestReaction(body, pain > 0.22f ? PhysicalReaction.Guard : PhysicalReaction.Flinch, impact.Region, Math.Max(0.1f, pain), 0.25f + pain * 0.6f);
            if (accepted)
            {
                body.LastSourceId = impact.SourceId;
                body.ReactionDirectionX = dx; body.ReactionDirectionY = dy; body.ReactionDirectionZ = dz;
                body.ContactX = impact.HasContact ? Signed(impact.ContactX) : 0f;
                body.ContactY = impact.HasContact ? Signed(impact.ContactY) : 0f;
                body.ContactZ = impact.HasContact ? Signed(impact.ContactZ) : 0f;
            }

            var result = Result(body, severity, tissue, oldBalance - body.Balance, body.Pain - oldPain, t);
            result.EnergyTransferred = energy;
            return result;
        }

        /// <summary>No survival decisions here: import current legacy values, including recovery after rescue.</summary>
        public static void ApplyLegacyLimits(PhysicalBody body, float mobility, float gripL, float gripR, float consciousness, bool dead)
        {
            Require(body);
            body.LegacyMobility = Clamp01(mobility); body.LegacyGripLeft = Clamp01(gripL); body.LegacyGripRight = Clamp01(gripR);
            body.Consciousness = Clamp01(consciousness); body.Dead = dead;
            RefreshFunctions(body);
            if (dead || body.Consciousness <= 0.1f)
            {
                SetPosture(body, PhysicalPosture.Incapacitated);
                if (body.Reaction != PhysicalReaction.Incapacitated)
                    RequestReaction(body, PhysicalReaction.Incapacitated, BodyRegion.Head, 1f, 1f);
            }
            else if (body.Posture == PhysicalPosture.Incapacitated)
            {
                SetPosture(body, PhysicalPosture.Prone);
                ClearReaction(body);
                RequestReaction(body, PhysicalReaction.Downed, BodyRegion.Chest, 0.6f, 0.5f);
            }
        }

        /// <summary>Advance with actual simulated seconds. Paused dt=0 is a no-op; invalid time is rejected.</summary>
        public static void Step(PhysicalBody body, float dtSeconds, bool grounded, bool supportAvailable, PhysicalityTuning tuning = null)
        {
            Require(body);
            if (!Finite(dtSeconds) || dtSeconds < 0f || dtSeconds > 60f) throw new ArgumentOutOfRangeException(nameof(dtSeconds), "Use finite simulation seconds in [0,60].");
            if (dtSeconds == 0f) return;
            var t = tuning ?? Defaults;
            // Small bounded integration preserves transitions at low frame rates without losing elapsed time.
            int steps = Math.Max(1, (int)Math.Ceiling(dtSeconds / MaxStep));
            float dt = dtSeconds / steps;
            for (int i = 0; i < steps; ++i) Advance(body, dt, grounded, supportAvailable, t);
        }

        public static bool RequestStartle(PhysicalBody body, float intensity, string sourceId = null, PhysicalityTuning tuning = null)
        {
            Require(body); var t = tuning ?? Defaults;
            float amount = Clamp01(Positive(intensity) * Positive(t.StartleSensitivity));
            if (body.Dead || body.Consciousness <= 0.1f || amount <= 0f) return false;
            body.Alarm = Math.Max(body.Alarm, amount);
            bool accepted = RequestReaction(body, PhysicalReaction.Startle, BodyRegion.Head, amount, 0.25f + 0.45f * amount);
            if (accepted) body.LastSourceId = sourceId;
            return accepted;
        }

        public static bool RequestReaction(PhysicalBody body, PhysicalReaction reaction, BodyRegion region, float strength, float seconds)
        {
            Require(body);
            if ((int)reaction < 0 || reaction > PhysicalReaction.Incapacitated) throw new ArgumentOutOfRangeException(nameof(reaction));
            if (reaction == PhysicalReaction.None || !Finite(seconds) || seconds <= 0f) return false;
            if (body.Dead && reaction != PhysicalReaction.Incapacitated) return false;
            if (body.ReactionRemaining > 0f && reaction < body.Reaction) return false;
            float amount = Clamp01(strength);
            if (body.ReactionRemaining > 0f && reaction == body.Reaction && amount < body.ReactionStrength) return false;
            body.Reaction = reaction; body.ReactionRegion = region;
            body.ReactionStrength = amount; body.ReactionRemaining = Math.Min(seconds, 60f);
            ++body.ReactionRevision;
            return true;
        }

        /// <summary>Import an explicit severing decision from the existing authoritative Gore system.</summary>
        public static void MarkSevered(PhysicalBody body, BodyRegion region)
        {
            Require(body); body.Region(region).Severed = true; RefreshFunctions(body);
        }

        /// <summary>For loop reset/respawn. Does not touch the authoritative Actor.Body.</summary>
        public static void Reset(PhysicalBody body, bool clearInjuries = true)
        {
            Require(body);
            if (clearInjuries) body.Regions = new PhysicalBody().Regions;
            body.Pain = body.Alarm = body.PostureSeconds = body.StableGroundSeconds = 0f;
            body.Balance = body.Consciousness = body.LegacyMobility = body.LegacyGripLeft = body.LegacyGripRight = 1f;
            body.Dead = false; body.Posture = PhysicalPosture.Standing; body.LastSourceId = null;
            body.ReactionDirectionX = body.ReactionDirectionY = body.ReactionDirectionZ = 0f;
            body.ContactX = body.ContactY = body.ContactZ = 0f;
            ClearReaction(body); RefreshFunctions(body);
        }

        static void Advance(PhysicalBody b, float dt, bool grounded, bool support, PhysicalityTuning t)
        {
            if (b.Dead || b.Consciousness <= 0.1f) return;
            float recovery = Positive(t.RecoverySpeed);
            b.Pain = Math.Max(0f, b.Pain - Positive(t.PainRecoveryPerSecond) * recovery * dt);
            b.Alarm *= (float)Math.Exp(-0.69314718056 * dt / AtLeast(t.AlarmHalfLifeSeconds, 0.01f));
            b.ReactionRemaining = Math.Max(0f, b.ReactionRemaining - dt);
            if (b.ReactionRemaining <= 0f) ClearReaction(b);
            b.PostureSeconds += dt;
            b.StableGroundSeconds = grounded ? b.StableGroundSeconds + dt : 0f;
            RefreshFunctions(b);
            if (grounded && b.Posture != PhysicalPosture.Falling)
                b.Balance = Clamp01(b.Balance + Positive(t.BalanceRecoveryPerSecond) * recovery * dt *
                    Math.Max(0.15f, b.Mobility) * (support && b.CanBrace ? 1.4f : 1f));

            switch (b.Posture)
            {
                case PhysicalPosture.Standing:
                    break;
                case PhysicalPosture.Staggering:
                    if (!grounded) BeginFall(b);
                    else if (b.Balance >= Clamp01(t.StaggerThreshold) && b.ReactionRemaining <= 0f) SetPosture(b, PhysicalPosture.Standing);
                    else if (support && b.CanBrace && b.Reaction < PhysicalReaction.Brace)
                        RequestReaction(b, PhysicalReaction.Brace, BodyRegion.Chest, 1f - b.Balance, 0.25f);
                    break;
                case PhysicalPosture.Falling:
                    if (grounded && b.PostureSeconds >= Positive(t.MinimumFallSeconds) && b.StableGroundSeconds >= 0.1f)
                    {
                        SetPosture(b, PhysicalPosture.Prone);
                        ClearReaction(b); RequestReaction(b, PhysicalReaction.Downed, b.ReactionRegion, 0.7f, Positive(t.MinimumProneSeconds));
                    }
                    break;
                case PhysicalPosture.Prone:
                    if (grounded && b.PostureSeconds >= Positive(t.MinimumProneSeconds) && b.Balance >= 0.6f &&
                        b.Mobility >= 0.2f && b.Pain < 0.85f && recovery > 0f)
                    { SetPosture(b, PhysicalPosture.Recovering); ClearReaction(b); }
                    break;
                case PhysicalPosture.Recovering:
                    if (!grounded) BeginFall(b);
                    else if (b.Mobility < 0.2f) SetPosture(b, PhysicalPosture.Prone);
                    else if (b.PostureSeconds * recovery >= AtLeast(t.GetUpSeconds, 0.1f)) SetPosture(b, PhysicalPosture.Standing);
                    break;
            }
        }

        static void BeginFall(PhysicalBody b)
        {
            SetPosture(b, PhysicalPosture.Falling);
            RequestReaction(b, PhysicalReaction.Fall, BodyRegion.Chest, 1f, 0.7f);
        }

        static void ApplyTissue(RegionState r, DamageType type, float dose)
        {
            switch (type)
            {
                case DamageType.Cut: r.Cut = Clamp01(r.Cut + dose); r.StructuralDamage = Clamp01(r.StructuralDamage + dose * 0.36f); break;
                case DamageType.Stab: r.Puncture = Clamp01(r.Puncture + dose); r.StructuralDamage = Clamp01(r.StructuralDamage + dose * 0.32f); break;
                case DamageType.Blunt: case DamageType.Fall:
                    r.Bruise = Clamp01(r.Bruise + dose); r.StructuralDamage = Clamp01(r.StructuralDamage + Math.Max(0f, dose - 0.1f) * 0.75f); break;
                case DamageType.Crush:
                    r.Bruise = Clamp01(r.Bruise + dose); r.StructuralDamage = Clamp01(r.StructuralDamage + dose * 0.9f); break;
                case DamageType.Burn: r.Cut = Clamp01(r.Cut + dose * 0.5f); break;
                // Choke, shock and drowning outcomes stay exclusively with Combat/WoundProfiles.
            }
        }

        static void RefreshFunctions(PhysicalBody b)
        {
            float leftArm = Math.Min(Function(b, BodyRegion.ShoulderL), Math.Min(Function(b, BodyRegion.ArmL), Function(b, BodyRegion.HandL)));
            float rightArm = Math.Min(Function(b, BodyRegion.ShoulderR), Math.Min(Function(b, BodyRegion.ArmR), Function(b, BodyRegion.HandR)));
            float leftLeg = Math.Min(Function(b, BodyRegion.LegL), Function(b, BodyRegion.FootL));
            float rightLeg = Math.Min(Function(b, BodyRegion.LegR), Function(b, BodyRegion.FootR));
            b.GripLeft = Math.Min(Clamp01(b.LegacyGripLeft), leftArm);
            b.GripRight = Math.Min(Clamp01(b.LegacyGripRight), rightArm);
            // A failed leg limits upright locomotion; both failed legs prevent automatic get-up.
            float legs = Math.Min(leftLeg, rightLeg) * 0.65f + Math.Max(leftLeg, rightLeg) * 0.35f;
            float torso = Math.Min(Function(b, BodyRegion.Chest), Math.Min(Function(b, BodyRegion.Abdomen), Function(b, BodyRegion.Back)));
            b.Mobility = Math.Min(Clamp01(b.LegacyMobility), Math.Min(legs, torso));
            if (b.Dead || b.Consciousness <= 0.1f) b.Mobility = b.GripLeft = b.GripRight = 0f;
        }

        static float Function(PhysicalBody body, BodyRegion part)
        {
            var r = body.Region(part);
            return Clamp01(r.Integrity - r.Bruise * 0.12f);
        }
        static ImpactResult Result(PhysicalBody b, int severity, float tissue, float balance, float pain, PhysicalityTuning t) => new ImpactResult
        {
            Reaction = b.Reaction, Severity = severity, TissueDamage = tissue, BalanceLost = balance, PainAdded = pain,
            ShouldDropLeft = b.GripLeft < Clamp01(t.GripDropThreshold), ShouldDropRight = b.GripRight < Clamp01(t.GripDropThreshold)
        };
        static bool IsLeg(BodyRegion r) => r == BodyRegion.LegL || r == BodyRegion.LegR || r == BodyRegion.FootL || r == BodyRegion.FootR;
        static void SetPosture(PhysicalBody b, PhysicalPosture p) { if (b.Posture == p) return; b.Posture = p; b.PostureSeconds = 0f; b.StableGroundSeconds = 0f; }
        static void ClearReaction(PhysicalBody b) { b.Reaction = PhysicalReaction.None; b.ReactionStrength = b.ReactionRemaining = 0f; }
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        static float Signed(float n) => Finite(n) ? n : 0f;
        static bool Normalize(ref float x, ref float y, ref float z)
        {
            double length = Math.Sqrt((double)x * x + (double)y * y + (double)z * z);
            if (length < 0.000001) return false;
            x = (float)(x / length); y = (float)(y / length); z = (float)(z / length); return true;
        }
        static float Positive(float n) => Finite(n) ? Math.Max(0f, n) : 0f;
        static float AtLeast(float n, float min) => Math.Max(min, Positive(n));
        static float Clamp01(float n) => Math.Min(1f, Positive(n));
        static void Require(PhysicalBody b) { if (b == null) throw new ArgumentNullException(nameof(b)); }
    }
}
