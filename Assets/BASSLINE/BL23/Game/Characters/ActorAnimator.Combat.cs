using BL23.Game.Physicality;
using UnityEngine;
using static BL23.Game.Characters.MotionKit;

namespace BL23.Game.Characters
{
    /// <summary>Combat presentation (motion track): attack beats by weapon class (anticipation, strike, follow-through, recovery),
    /// the weapon-ready and firearm aim stances with recoil, stagger with world-planted recovery steps, and the victim's defences.
    /// Presentation only: the kernel decides who is hit and what happens.</summary>
    public partial class ActorAnimator
    {
        struct AttackState
        {
            public bool Active;
            public ActionAnim Kind;
            public WeaponClass Weapon;
            public float T, Dur, Impact, Strength;
            public bool HasTarget; public Vector3 Target;   // root-space target point
            public float W, I, F;                            // phase marks (fractions): wind-up end, impact, follow-through end
            public bool TwoHands;
        }
        AttackState _atk;

        WeaponClass _heldClass = WeaponClass.None; float _heldMass;
        bool _combatReady; WeaponClass _readyClass; float _readyW;
        Vector3? _aimWorld;
        ActorRig _partner;
        readonly ActorPose _cb = new ActorPose();

        public bool IsAttacking => _atk.Active;

        /// <summary>One attack beat: anticipation, strike, follow-through, recovery, sized by the weapon class and the held
        /// mass (SetHeldWeapon), aimed at targetWorld (null = straight ahead). kind: Stab, StabUnder, StabOver, Slash, Overhead,
        /// SwingSide, SwingHeavy, Shove, Kick, Throw, Shoot, Strangle / Garrote / Smother (a lunge into the hold). strength 0..1.
        /// Returns the seconds until the impact frame (the victim's reaction, the hit sound and the wound decal land then).</summary>
        public float PlayAttack(ActionAnim kind, WeaponClass weapon, Vector3? targetWorld = null, float strength = 1f)
        {
            Init();
            if (!_init || _dead >= 0 || _beingCarried) return 0f;
            if (weapon == WeaponClass.None) weapon = _heldClass;
            float mass = _heldMass > 0f ? _heldMass : DefaultMass(weapon);
            float slow = Mathf.Clamp(1f + (mass - 1f) * 0.06f, 0.9f, 1.45f);
            _atk.Active = true; _atk.Kind = kind; _atk.Weapon = weapon; _atk.T = 0f; _atk.Strength = Clamp01(strength);
            _atk.TwoHands = weapon == WeaponClass.Heavy || weapon == WeaponClass.Long || weapon == WeaponClass.Rifle || weapon == WeaponClass.Crossbow || kind == ActionAnim.SwingHeavy || kind == ActionAnim.Shove || (kind == ActionAnim.Overhead && mass > 2.5f);
            float dur;
            switch (kind)
            {
                case ActionAnim.Stab: dur = 0.95f; _atk.W = 0.32f; _atk.I = 0.46f; _atk.F = 0.62f; break;
                case ActionAnim.StabUnder: dur = 0.9f; _atk.W = 0.3f; _atk.I = 0.44f; _atk.F = 0.62f; break;
                case ActionAnim.StabOver: dur = 1.0f; _atk.W = 0.36f; _atk.I = 0.5f; _atk.F = 0.64f; break;
                case ActionAnim.Slash: dur = 0.85f; _atk.W = 0.34f; _atk.I = 0.47f; _atk.F = 0.66f; break;
                case ActionAnim.Overhead: dur = 1.15f; _atk.W = 0.4f; _atk.I = 0.52f; _atk.F = 0.66f; break;
                case ActionAnim.SwingSide: dur = 1.0f; _atk.W = 0.36f; _atk.I = 0.5f; _atk.F = 0.68f; break;
                case ActionAnim.SwingHeavy: dur = 1.5f; _atk.W = 0.44f; _atk.I = 0.56f; _atk.F = 0.72f; break;
                case ActionAnim.Shove: dur = 0.8f; _atk.W = 0.3f; _atk.I = 0.42f; _atk.F = 0.56f; break;
                case ActionAnim.Kick: dur = 0.9f; _atk.W = 0.3f; _atk.I = 0.44f; _atk.F = 0.6f; break;
                case ActionAnim.Throw: dur = 1.1f; _atk.W = 0.4f; _atk.I = 0.5f; _atk.F = 0.68f; break;
                case ActionAnim.Shoot: dur = weapon == WeaponClass.Rifle ? 0.75f : weapon == WeaponClass.Crossbow ? 0.45f : 0.55f; _atk.W = 0.02f; _atk.I = 0.04f; _atk.F = 0.18f; slow = 1f; break;
                case ActionAnim.Strangle: case ActionAnim.Garrote: case ActionAnim.LigatureFront: case ActionAnim.Smother: case ActionAnim.Drown:
                    dur = 0.8f; _atk.W = 0.25f; _atk.I = 0.55f; _atk.F = 0.8f; break;
                default: dur = 0.9f; _atk.W = 0.33f; _atk.I = 0.46f; _atk.F = 0.62f; break;
            }
            // heavier weapons: longer wind-up and recovery, same strike speed
            _atk.Dur = dur * slow;
            float wind = _atk.W * dur * slow, strike = (_atk.I - _atk.W) * dur;
            _atk.W = wind / _atk.Dur; _atk.I = (wind + strike) / _atk.Dur; _atk.F = Mathf.Min(0.92f, _atk.I + (_atk.F - _atk.I) * dur / _atk.Dur);
            _atk.Impact = _atk.I * _atk.Dur;
            _atk.HasTarget = targetWorld.HasValue;
            if (targetWorld.HasValue) _atk.Target = transform.InverseTransformPoint(targetWorld.Value);
            // the action clock (CurrentAction, first-person hands) follows the beat; its pose comes from this layer
            _action = kind; _aT = 0f; _aDur = _atk.Dur; _aActive = true; _afterAction = null; _actClip = null;
            _gActive = false;
            return _atk.Impact;
        }

        static float DefaultMass(WeaponClass w)
        {
            switch (w)
            {
                case WeaponClass.Knife: return 0.25f;
                case WeaponClass.Blade: return 0.5f;
                case WeaponClass.Club: return 1.2f;
                case WeaponClass.Heavy: return 5f;
                case WeaponClass.Long: return 2f;
                case WeaponClass.Pistol: return 1.1f;
                case WeaponClass.Rifle: return 3.6f;
                case WeaponClass.Crossbow: return 4f;
                default: return 0.5f;
            }
        }

        static bool IsAttackKind(ActionAnim a)
        {
            switch (a)
            {
                case ActionAnim.Stab: case ActionAnim.StabUnder: case ActionAnim.StabOver: case ActionAnim.Slash: case ActionAnim.Overhead:
                case ActionAnim.SwingSide: case ActionAnim.SwingHeavy: case ActionAnim.Shove: case ActionAnim.Kick: case ActionAnim.Throw: case ActionAnim.Shoot:
                    return true;
            }
            return false;
        }

        /// <summary>Seconds until the impact frame of the attack playing now (0 = no attack, or the impact has passed).</summary>
        public float TimeToImpact => _atk.Active ? Mathf.Max(0f, _atk.Impact - _atk.T) : 0f;

        /// <summary>What the right hand holds, for grips and attack timing (ActorView sets it when the held item changes).</summary>
        public void SetHeldWeapon(WeaponClass weapon, float massKg) { _heldClass = weapon; _heldMass = Mathf.Max(0f, massKg); }

        /// <summary>Weapon raised and ready (a guard with a blade or club; an aim stance with Pistol / Rifle / Crossbow); false = idle.</summary>
        public void SetCombatReady(bool on, WeaponClass weapon) { _combatReady = on; if (weapon != WeaponClass.None) _readyClass = weapon; else if (on) _readyClass = _heldClass; }

        /// <summary>The other actor of a paired act (attacker / victim of a strike, a grab...); null clears it.</summary>
        public void SetPartner(ActorRig partner) { _partner = partner; }
        public ActorRig Partner => _partner;

        /// <summary>Aim point for a raised firearm / crossbow (with SetCombatReady(true, Pistol|Rifle|Crossbow)); null = straight ahead.</summary>
        public void SetAim(Vector3? targetWorld) { _aimWorld = targetWorld; }

        static bool Firearm(WeaponClass w) => w == WeaponClass.Pistol || w == WeaponClass.Rifle || w == WeaponClass.Crossbow;

        // ---------------------------------------------------------------- defence
        struct DefenseState { public bool Active; public ActionAnim Kind; public Vector3 From; public float T, Dur; }
        DefenseState _def;
        float _grabWristW;

        /// <summary>Victim defence against an attacker at attackerWorld: Defend (forearms up), GrabWrist (catch the weapon arm),
        /// TurnAway (turn the body away, shield the head), Dodge (quick step back), Guard (hands up). Returns the seconds it lasts.</summary>
        public float PlayDefense(ActionAnim kind, Vector3 attackerWorld)
        {
            Init();
            if (!_init || _dead >= 0 || _beingCarried || !_conscious) return 0f;
            Vector3 f = transform.InverseTransformPoint(attackerWorld); f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            _def.Active = true; _def.Kind = kind; _def.From = f.normalized; _def.T = 0f;
            _def.Dur = kind == ActionAnim.TurnAway ? 1.3f : kind == ActionAnim.Dodge ? 0.75f : kind == ActionAnim.GrabWrist ? 1.1f : 0.95f;
            Glance(attackerWorld + Vector3.up * 1.4f, _def.Dur);
            if (_rig != null && _rig.Face != null) _rig.Face.PushTransient(Expr.Fear, 1f, _def.Dur);
            return _def.Dur;
        }

        // ---------------------------------------------------------------- stagger (world-planted steps)
        struct StaggerState
        {
            public bool Active; public Vector3 Dir, DirW, Root0; public float T, Dur, Str, Dist; public int Steps; public bool FirstLeft;
            public Vector3 AnchorL, AnchorR, FromL, FromR; public int StepL, StepR, LandL, LandR; public int FinalLeft;
        }
        StaggerState _stag;

        /// <summary>Directional loss of balance with recovery steps: the body is pushed along worldDir (strength 0..1). Planted
        /// feet stay where they are in the world (no sliding) whether or not physics moves the root; never changes the posture
        /// (a fall happens only when the kernel says so).</summary>
        public void PlayStagger(Vector3 worldDir, float strength)
        {
            Init();
            if (!_init || _dead >= 0 || _beingCarried || _posture != Posture.Stand || _pairedRef != null) return;
            float s = Clamp01(strength);
            Vector3 dw = worldDir; dw.y = 0f;
            if (dw.sqrMagnitude < 1e-4f) dw = -transform.forward;
            dw.Normalize();
            Vector3 d = transform.InverseTransformDirection(dw);
            _stag.Active = true; _stag.Dir = d; _stag.DirW = dw; _stag.T = 0f; _stag.Str = s;
            _stag.Root0 = transform.position;
            _stag.Steps = 1 + (s > 0.4f ? 1 : 0) + (s > 0.75f ? 1 : 0);
            _stag.Dur = 0.55f + 0.3f * _stag.Steps;
            _stag.Dist = (0.12f + 0.42f * s) * (Height / 1.75f);
            // the foot on the side the body is pushed toward steps first; straight pushes alternate by actor
            _stag.FirstLeft = Mathf.Abs(d.x) > 0.25f ? d.x < 0f : Mathf.Repeat(_seed * 3.1f + _time, 1f) < 0.5f;
            _stag.AnchorL = _b[(int)HBone.FootL].position; _stag.AnchorR = _b[(int)HBone.FootR].position;
            _stag.FromL = _stag.AnchorL; _stag.FromR = _stag.AnchorR; _stag.StepL = _stag.StepR = _stag.LandL = _stag.LandR = -1; _stag.FinalLeft = -1;
            if (_rig != null && _rig.Face != null) _rig.Face.PushTransient(Expr.Surprised, 1f, 0.8f);
        }

        void TickCombatClocks(float dt)
        {
            if (_atk.Active) { _atk.T += dt; if (_atk.T >= _atk.Dur) _atk.Active = false; }
            if (_stag.Active) { _stag.T += dt; if (_stag.T >= _stag.Dur) _stag.Active = false; }
            if (_def.Active) { _def.T += dt; if (_def.T >= _def.Dur) _def.Active = false; }
        }

        void OnDeathStarted() { _atk.Active = false; _stag.Active = false; _combatReady = false; _def.Active = false; _hit.Active = false; _startle.Active = false; _getUp.Active = false; }

        void EvaluateCombat(float dt, ActorPoses.Dims dims)
        {
            bool can = _dead < 0 && !_beingCarried && _conscious && _posture == Posture.Stand;
            _grabWristW = 0f;
            _readyW = Mathf.MoveTowards(_readyW, can && _combatReady && _restraint == RestraintFlags.None ? 1f : 0f, dt * 5f);
            if (_readyW > 0.001f) EvaluateReady(dims, _readyW);
            if (_atk.Active) { _atk.T += dt; if (_atk.T >= _atk.Dur || !can) _atk.Active = false; else EvaluateAttack(dims); }
            if (_def.Active) { _def.T += dt; if (_def.T >= _def.Dur || !can) _def.Active = false; else EvaluateDefense(dims); }
            if (_stag.Active && can) EvaluateStagger(dt, dims);
            else _stag.Active = false;
        }

        // ---------------------------------------------------------------- ready / aim stances
        void EvaluateReady(ActorPoses.Dims d, float w)
        {
            var p = _cb; p.CopyFrom(_base);
            float ao = ArmIdleOut;
            var cls = _readyClass;
            // aim direction (root space), for chest turn and pitch
            float yaw = 0f, pitch = 0f;
            if (_aimWorld.HasValue)
            {
                Vector3 a = transform.InverseTransformPoint(_aimWorld.Value) - new Vector3(0f, Height * 0.8f, 0f);
                yaw = Mathf.Clamp(Mathf.Atan2(a.x, a.z) * Mathf.Rad2Deg, -70f, 70f);
                pitch = Mathf.Clamp(-Mathf.Atan2(a.y, new Vector2(a.x, a.z).magnitude) * Mathf.Rad2Deg, -40f, 50f);
            }
            float breath = Mathf.Sin(_time * 1.7f);
            bool still = _walkW < 0.1f && p.LegIK > 0.5f;
            if (cls == WeaponClass.Pistol)
            {
                // duellist: side on, arm straight at the target, the free hand in the small of the back
                AddBody(p, HBone.Hips, 0f, -20f + yaw * 0.25f, 0f);
                AddBody(p, HBone.Spine, 0f, -14f + yaw * 0.25f, 0f);
                AddBody(p, HBone.Chest, -3f, -22f + yaw * 0.2f, 0f);
                p.SetArm(false, 88f - pitch * 0.9f + breath * 0.4f, 56f, 8f, 3f, 70f, -4f);
                p.SetArm(true, -22f, ao + 4f, 88f, 80f, 20f, 0f);
                p.Grip(false); p.SoftHand(true);
                if (still) { p.FootR += new Vector3(0.03f, 0f, 0.1f); p.FootL += new Vector3(-0.05f, 0f, -0.08f); p.FootRotL = ActorPose.E(0f, -48f, 0f); p.FootRotR = ActorPose.E(0f, -12f, 0f); }
            }
            else if (cls == WeaponClass.Rifle || cls == WeaponClass.Crossbow)
            {
                bool xb = cls == WeaponClass.Crossbow;
                AddBody(p, HBone.Hips, 0f, -14f + yaw * 0.25f, 0f);
                AddBody(p, HBone.Spine, 2f + pitch * 0.25f, -10f + yaw * 0.25f, 0f);
                AddBody(p, HBone.Chest, 3f + pitch * 0.35f, -8f + yaw * 0.25f, 0f);
                AddBody(p, HBone.Neck, 6f, 10f, -6f);
                AddBody(p, HBone.Head, 4f, 8f, -8f);                                  // cheek to the stock
                p.SetArm(false, (xb ? 70f : 76f) + breath * 0.3f, 52f, 62f, xb ? 100f : 112f, 30f, 10f);    // trigger hand by the shoulder
                p.SetArm(true, (xb ? 72f : 80f) + breath * 0.3f, -6f, 22f, xb ? 42f : 34f, 70f, -10f);      // support hand under the barrel
                p.Grip(false); p.Grip(true);
                if (still) { p.FootL += new Vector3(-0.02f, 0f, 0.12f); p.FootR += new Vector3(0.04f, 0f, -0.06f); p.FootRotR = ActorPose.E(0f, 35f, 0f); p.HipsOffset += new Vector3(0f, -0.02f, 0f); }
            }
            else
            {
                // melee guard: weapon hand forward at the hip, free hand up, knees soft
                bool two = cls == WeaponClass.Heavy || cls == WeaponClass.Long;
                AddBody(p, HBone.Chest, 4f, -10f + yaw * 0.4f, 0f);
                AddBody(p, HBone.Spine, 3f, yaw * 0.3f, 0f);
                if (two) { p.SetArm(false, 34f, ao + 10f, 20f, 70f, 40f, 0f); p.SetArm(true, 40f, -8f, 40f, 72f, 40f, 0f); p.Grip(true); }
                else { p.SetArm(false, 26f, ao + 8f, 20f, 78f, 40f, -10f); p.SetArm(true, 30f, ao + 2f, 30f, 86f, 20f, 0f); p.SoftHand(true); }
                p.Grip(false);
                if (still) { p.FootL += new Vector3(-0.02f, 0f, 0.1f); p.FootR += new Vector3(0.03f, 0f, -0.07f); p.HipsOffset += new Vector3(0f, -0.035f, 0f); }
            }
            _base.Overlay(p, w, _walkW > 0.3f ? UpperMask : null, _walkW <= 0.3f);
        }

        // ---------------------------------------------------------------- attack beats
        /// <summary>One key of an attack beat (right-handed attacker): arm angles, torso turns, weight and the lead step.</summary>
        struct AtkKey
        {
            public float RF, RO, RT, RE, RET, RW;       // right arm: fwd, out, twist, elbow, elbow twist, wrist
            public float LF, LO, LT, LE, LET, LW;       // left arm
            public float CP, CY, CL, SP, SY, HP, HY;    // chest pitch / yaw / lean, spine pitch / yaw, hips pitch / yaw
            public Vector3 Hips; public float Step;      // hips offset (m, 1.75 m body), lead-foot step forward (m)
            public float HeadP;
        }

        static AtkKey K(float rf, float ro, float rt, float re, float ret, float rw, float lf, float lo, float lt, float le, float let, float lw,
            float cp, float cy, float cl, float sp, float sy, float hp, float hy, float hx, float hyy, float hz, float step, float headP = 0f)
            => new AtkKey { RF = rf, RO = ro, RT = rt, RE = re, RET = ret, RW = rw, LF = lf, LO = lo, LT = lt, LE = le, LET = let, LW = lw, CP = cp, CY = cy, CL = cl, SP = sp, SY = sy, HP = hp, HY = hy, Hips = new Vector3(hx, hyy, hz), Step = step, HeadP = headP };

        static AtkKey Lerp(AtkKey a, AtkKey b, float t)
        {
            return new AtkKey
            {
                RF = Mathf.Lerp(a.RF, b.RF, t), RO = Mathf.Lerp(a.RO, b.RO, t), RT = Mathf.Lerp(a.RT, b.RT, t), RE = Mathf.Lerp(a.RE, b.RE, t), RET = Mathf.Lerp(a.RET, b.RET, t), RW = Mathf.Lerp(a.RW, b.RW, t),
                LF = Mathf.Lerp(a.LF, b.LF, t), LO = Mathf.Lerp(a.LO, b.LO, t), LT = Mathf.Lerp(a.LT, b.LT, t), LE = Mathf.Lerp(a.LE, b.LE, t), LET = Mathf.Lerp(a.LET, b.LET, t), LW = Mathf.Lerp(a.LW, b.LW, t),
                CP = Mathf.Lerp(a.CP, b.CP, t), CY = Mathf.Lerp(a.CY, b.CY, t), CL = Mathf.Lerp(a.CL, b.CL, t), SP = Mathf.Lerp(a.SP, b.SP, t), SY = Mathf.Lerp(a.SY, b.SY, t),
                HP = Mathf.Lerp(a.HP, b.HP, t), HY = Mathf.Lerp(a.HY, b.HY, t), Hips = Vector3.Lerp(a.Hips, b.Hips, t), Step = Mathf.Lerp(a.Step, b.Step, t), HeadP = Mathf.Lerp(a.HeadP, b.HeadP, t)
            };
        }

        /// <summary>The ready, wind-up, impact and follow-through keys of an attack kind.</summary>
        static void AttackKeys(ActionAnim kind, bool two, float ao, out AtkKey ready, out AtkKey wind, out AtkKey hit, out AtkKey follow)
        {
            // ready: weapon hand at the hip, free hand loosely forward
            ready = K(24f, ao + 6f, 20f, 70f, 30f, -8f, 20f, ao + 4f, 25f, 60f, 20f, 0f, 3f, -6f, 0f, 2f, 0f, 0f, 0f, 0f, -0.03f, 0f, 0f);
            switch (kind)
            {
                case ActionAnim.StabUnder:
                    wind = K(-12f, ao + 10f, -20f, 96f, 90f, 22f, 44f, 2f, 30f, 58f, 20f, 0f, 10f, -18f, 0f, 6f, -8f, 4f, -8f, 0f, -0.09f, -0.02f, 0f);
                    hit = K(60f, ao, -10f, 38f, 90f, 26f, 22f, ao + 8f, 20f, 88f, 20f, 0f, 5f, 16f, 0f, 6f, 8f, 6f, 10f, 0f, -0.07f, 0.1f, 0.26f);
                    follow = K(70f, ao, -8f, 30f, 90f, 36f, 18f, ao + 8f, 20f, 92f, 20f, 0f, -2f, 18f, 0f, 3f, 8f, 4f, 10f, 0f, -0.05f, 0.11f, 0.26f);
                    break;
                case ActionAnim.StabOver:
                    wind = K(168f, 20f, 0f, 102f, 0f, 30f, 60f, 10f, 20f, 40f, 30f, 0f, -12f, -12f, 0f, -5f, -4f, -3f, -6f, 0f, 0f, -0.02f, 0f, -6f);
                    hit = K(55f, 6f, 0f, 30f, 10f, 40f, 42f, 10f, 20f, 50f, 30f, 0f, 20f, 10f, 0f, 10f, 4f, 6f, 6f, 0f, -0.07f, 0.08f, 0.22f, 6f);
                    follow = K(30f, 4f, 0f, 26f, 10f, 46f, 36f, 10f, 20f, 56f, 30f, 0f, 26f, 12f, 0f, 13f, 4f, 8f, 6f, 0f, -0.09f, 0.1f, 0.22f, 8f);
                    break;
                case ActionAnim.Slash:
                    wind = K(42f, 72f, -22f, 64f, 20f, 10f, 40f, ao + 2f, 30f, 60f, 20f, 0f, 2f, -32f, 0f, 2f, -10f, 0f, -8f, 0.02f, -0.04f, -0.02f, 0f);
                    hit = K(84f, 10f, 0f, 22f, 40f, 0f, 30f, ao + 10f, 20f, 70f, 20f, 0f, 8f, 0f, 0f, 4f, 0f, 2f, 2f, 0f, -0.06f, 0.06f, 0.2f);
                    follow = K(72f, -42f, 40f, 32f, 60f, -10f, 24f, ao + 16f, 20f, 76f, 20f, 0f, 10f, 32f, 0f, 5f, 12f, 3f, 12f, -0.01f, -0.06f, 0.07f, 0.2f);
                    break;
                case ActionAnim.Overhead:
                    wind = two ? K(176f, 14f, 20f, 116f, 30f, 10f, 172f, -6f, 30f, 120f, 30f, 10f, -14f, -4f, 0f, -6f, 0f, -4f, 0f, 0f, 0.01f, -0.03f, 0f, -8f)
                               : K(176f, 16f, 20f, 112f, 30f, 10f, 40f, ao + 12f, 20f, 50f, 20f, 0f, -12f, -10f, 0f, -5f, -4f, -3f, -4f, 0f, 0.01f, -0.03f, 0f, -8f);
                    hit = two ? K(70f, 6f, 10f, 14f, 30f, 0f, 68f, -10f, 30f, 18f, 30f, 0f, 22f, 0f, 0f, 14f, 0f, 8f, 0f, 0f, -0.08f, 0.08f, 0.22f, 8f)
                              : K(72f, 6f, 10f, 14f, 30f, 0f, 30f, ao + 14f, 20f, 60f, 20f, 0f, 20f, 6f, 0f, 12f, 2f, 7f, 4f, 0f, -0.08f, 0.08f, 0.22f, 8f);
                    follow = two ? K(32f, 4f, 10f, 10f, 30f, 0f, 30f, -12f, 30f, 14f, 30f, 0f, 30f, 0f, 0f, 18f, 0f, 10f, 0f, 0f, -0.11f, 0.1f, 0.22f, 10f)
                                 : K(30f, 4f, 10f, 10f, 30f, 0f, 26f, ao + 16f, 20f, 60f, 20f, 0f, 28f, 6f, 0f, 16f, 2f, 9f, 4f, 0f, -0.1f, 0.1f, 0.22f, 10f);
                    break;
                case ActionAnim.SwingSide:
                    wind = K(50f, 86f, -30f, 72f, 20f, 0f, 44f, ao, 30f, 70f, 20f, 0f, 0f, -46f, 0f, 0f, -16f, 0f, -14f, 0.02f, -0.03f, -0.03f, 0f);
                    hit = K(90f, 16f, 0f, 10f, 30f, 0f, 30f, ao + 12f, 20f, 70f, 20f, 0f, 4f, 4f, 0f, 2f, 5f, 2f, 10f, 0f, -0.06f, 0.06f, 0.22f);
                    follow = K(80f, -50f, 30f, 26f, 40f, 0f, 20f, ao + 24f, 20f, 60f, 20f, 0f, 6f, 40f, 0f, 3f, 15f, 3f, 20f, -0.02f, -0.06f, 0.07f, 0.22f);
                    break;
                case ActionAnim.SwingHeavy:
                    wind = K(150f, 56f, -10f, 110f, 30f, 0f, 142f, -12f, 42f, 122f, 30f, 0f, -8f, -50f, 0f, -3f, -20f, -2f, -20f, 0.03f, -0.02f, -0.05f, 0f, -4f);
                    hit = K(86f, 6f, 0f, 12f, 30f, 0f, 86f, -20f, 30f, 20f, 30f, 0f, 18f, 10f, 0f, 10f, 5f, 6f, 15f, 0f, -0.09f, 0.08f, 0.28f, 6f);
                    follow = K(40f, -30f, 20f, 20f, 30f, 0f, 42f, -44f, 30f, 30f, 30f, 0f, 30f, 44f, 0f, 15f, 20f, 9f, 25f, -0.02f, -0.12f, 0.08f, 0.28f, 10f);
                    break;
                case ActionAnim.Shove:
                    wind = K(40f, -4f, 30f, 112f, 20f, -30f, 40f, -4f, 30f, 112f, 20f, -30f, -4f, 0f, 0f, -2f, 0f, 0f, 0f, 0f, -0.04f, -0.04f, 0f);
                    hit = K(86f, 0f, 20f, 10f, 20f, -50f, 86f, 0f, 20f, 10f, 20f, -50f, 12f, 0f, 0f, 6f, 0f, 4f, 0f, 0f, -0.05f, 0.12f, 0.3f);
                    follow = K(88f, 0f, 20f, 6f, 20f, -50f, 88f, 0f, 20f, 6f, 20f, -50f, 14f, 0f, 0f, 7f, 0f, 5f, 0f, 0f, -0.05f, 0.14f, 0.3f);
                    break;
                case ActionAnim.Throw:
                    wind = K(152f, 42f, -40f, 112f, 0f, 20f, 64f, ao + 8f, 10f, 26f, 0f, 0f, -6f, -36f, 0f, -3f, -14f, -2f, -15f, 0.02f, -0.02f, -0.04f, 0f);
                    hit = K(112f, 12f, 0f, 40f, 30f, -10f, 30f, ao + 14f, 10f, 50f, 0f, 0f, 12f, 10f, 0f, 6f, 6f, 4f, 8f, 0f, -0.05f, 0.08f, 0.26f);
                    follow = K(40f, -30f, 30f, 22f, 40f, -10f, 14f, ao + 16f, 10f, 40f, 0f, 0f, 22f, 30f, 0f, 10f, 12f, 6f, 16f, -0.02f, -0.07f, 0.1f, 0.26f);
                    break;
                case ActionAnim.Strangle: case ActionAnim.Garrote: case ActionAnim.LigatureFront: case ActionAnim.Smother: case ActionAnim.Drown:
                    wind = K(40f, ao, 30f, 90f, 30f, 0f, 40f, ao, 30f, 90f, 30f, 0f, 6f, 0f, 0f, 3f, 0f, 0f, 0f, 0f, -0.04f, -0.02f, 0f);
                    hit = K(84f, -8f, 45f, 40f, 30f, -20f, 84f, -8f, 45f, 40f, 30f, -20f, 14f, 0f, 0f, 6f, 0f, 4f, 0f, 0f, -0.06f, 0.1f, 0.25f);
                    follow = hit;
                    break;
                default:    // Stab: straight thrust
                    wind = K(-44f, ao + 14f, 10f, 96f, 60f, -10f, 50f, 0f, 30f, 55f, 20f, 0f, -3f, -26f, 0f, -1f, -10f, 0f, -10f, 0f, -0.04f, -0.04f, 0f);
                    hit = K(78f, 2f, 5f, 12f, 70f, -12f, 20f, ao + 10f, 20f, 90f, 20f, 0f, 10f, 20f, 0f, 6f, 8f, 4f, 10f, 0f, -0.06f, 0.1f, 0.26f);
                    follow = K(82f, 0f, 5f, 5f, 70f, -12f, 16f, ao + 12f, 20f, 94f, 20f, 0f, 14f, 22f, 0f, 8f, 8f, 5f, 10f, 0f, -0.07f, 0.12f, 0.26f);
                    break;
            }
        }

        void EvaluateAttack(ActorPoses.Dims d)
        {
            float u = Clamp01(_atk.T / _atk.Dur);
            float ao = ArmIdleOut;
            float sc = Height / 1.75f;
            if (_atk.Kind == ActionAnim.Shoot) { EvaluateShot(u); return; }
            if (_atk.Kind == ActionAnim.Kick) { EvaluateKick(d, u); return; }
            var p = _cb; p.CopyFrom(_base);
            AttackKeys(_atk.Kind, _atk.TwoHands, ao, out var ready, out var wind, out var hit, out var follow);
            // ready -> wind-up (anticipation, eased) -> impact (accelerating) -> follow-through (decelerating) -> ready -> base
            float R = Mathf.Min(0.9f, _atk.F + 0.2f);
            AtkKey k;
            if (u < _atk.W) k = Lerp(ready, wind, Ease(u / _atk.W));
            else if (u < _atk.I) { float x = (u - _atk.W) / (_atk.I - _atk.W); k = Lerp(wind, hit, EaseIn(x) * 0.85f + Ease(x) * 0.15f); }
            else if (u < _atk.F) k = Lerp(hit, follow, EaseOut((u - _atk.I) / (_atk.F - _atk.I)));
            else k = Lerp(follow, ready, Ease((u - _atk.F) / Mathf.Max(0.01f, R - _atk.F)));
            float env = Win(u, 0f, 0.1f) * (1f - Win(u, R, 1f));
            // aim at the target: turn the torso toward it, raise / lower the strike to its height, lunge to its distance
            float tyaw = 0f, tpitch = 0f, reach = 0f;
            if (_atk.HasTarget)
            {
                Vector3 tl = _atk.Target;
                tyaw = Mathf.Clamp(Mathf.Atan2(tl.x, tl.z) * Mathf.Rad2Deg, -60f, 60f);
                float h = tl.y - Height * 0.62f;
                tpitch = Mathf.Clamp(h / Mathf.Max(0.3f, new Vector2(tl.x, tl.z).magnitude) * 45f, -35f, 35f);
                reach = Mathf.Clamp(new Vector2(tl.x, tl.z).magnitude - 0.55f, -0.2f, 0.45f);
            }
            float aimW = Win(u, 0f, _atk.W) * (1f - Win(u, _atk.F, R));
            float str = 0.75f + 0.25f * _atk.Strength;
            p.SetArm(false, k.RF + tpitch * aimW, k.RO, k.RT, k.RE, k.RET, k.RW);
            if (_atk.TwoHands || _atk.Kind == ActionAnim.Shove) p.SetArm(true, k.LF + tpitch * aimW, k.LO, k.LT, k.LE, k.LET, k.LW);
            else p.SetArm(true, k.LF, k.LO, k.LT, k.LE, k.LET, k.LW);
            AddBody(p, HBone.Chest, k.CP * str, k.CY * str + tyaw * 0.35f * aimW, k.CL);
            AddBody(p, HBone.Spine, k.SP * str, k.SY * str + tyaw * 0.25f * aimW, 0f);
            AddBody(p, HBone.Hips, k.HP, k.HY + tyaw * 0.2f * aimW, 0f);
            AddBody(p, HBone.Head, k.HeadP);
            float stepK = Clamp01(k.Step / 0.3f);
            p.HipsOffset += new Vector3(k.Hips.x, k.Hips.y, k.Hips.z * (1f + reach)) * sc;
            if (p.LegIK > 0.5f && _walkW < 0.5f)
            {
                // the lead (left) foot steps toward the target on the strike, the back heel lifts and pivots
                float stepLen = k.Step * (1f + reach * 1.5f) * sc;
                float lift = Mathf.Sin(Clamp01((u - _atk.W * 0.7f) / Mathf.Max(0.05f, _atk.I - _atk.W * 0.7f)) * Mathf.PI) * 0.05f * stepK * sc;
                p.FootL += new Vector3(Mathf.Sin(tyaw * Mathf.Deg2Rad) * stepLen * 0.5f, lift, stepLen);
                p.FootRotR = p.FootRotR * ActorPose.E(18f * stepK, 12f * stepK, 0f);
            }
            if (_atk.Weapon == WeaponClass.None && _atk.Kind != ActionAnim.Shove) { p.Fist(false); p.Fist(true); }
            else { p.Grip(false); if (_atk.TwoHands) p.Grip(true); else if (_atk.Kind != ActionAnim.Shove) p.SoftHand(true); }
            if (_atk.Kind == ActionAnim.Shove) { p.OpenHand(true); p.OpenHand(false); }
            if (_atk.Kind == ActionAnim.Throw && u > _atk.I) p.OpenHand(false);
            _base.Overlay(p, env, null, true);
        }

        void EvaluateKick(ActorPoses.Dims d, float u)
        {
            var p = _cb; p.CopyFrom(_base);
            float sc = Height / 1.75f;
            float chamber = Win(u, 0.05f, _atk.W), ext = Win(u, _atk.W, _atk.I) * (1f - Win(u, _atk.F, 0.82f)), back = Win(u, _atk.F, 0.82f);
            float lift = chamber * (1f - Win(u, 0.8f, 0.95f));
            float env = Win(u, 0f, 0.08f) * (1f - Win(u, 0.88f, 1f));
            AddBody(p, HBone.Hips, -8f * lift - 8f * ext, 0f, 4f * lift);
            AddBody(p, HBone.Spine, -6f * ext, 0f, 0f);
            AddBody(p, HBone.Chest, -8f * ext + 4f * chamber, 0f, 0f);
            p.HipsOffset += new Vector3(-0.03f * lift, -0.02f * lift, -0.03f * ext) * sc;
            p.LegIK = 1f;
            Vector3 knee = new Vector3(0f, d.LegLen * 0.55f, d.UpperLeg * 0.65f);
            Vector3 kick = new Vector3(0f, d.LegLen * 0.45f, d.LegLen * 0.85f);
            Vector3 foot = Vector3.Lerp(d.FootR, d.FootR + knee, lift);
            foot = Vector3.Lerp(foot, d.FootR + kick, ext * (1f - back));
            p.FootR = foot;
            p.FootRotR = ActorPose.E(-30f * ext + 30f * chamber * (1f - ext), 0f, 0f);
            float bal = Mathf.Max(lift, ext);
            BlendArm(p, false, bal, 30f, ArmIdleOut + 26f, 10f, 70f, 20f, 0f);
            BlendArm(p, true, bal, 36f, ArmIdleOut + 22f, 10f, 64f, 20f, 0f);
            p.Fist(true); p.Fist(false);
            _base.Overlay(p, env, null, true);
        }

        void EvaluateShot(float u)
        {
            // recoil from the aim stance (or from the current pose when not aiming)
            var p = _cb; p.CopyFrom(_base);
            float t = _atk.T;
            var w = _atk.Weapon == WeaponClass.None ? _readyClass : _atk.Weapon;
            float kick = Impulse(t, w == WeaponClass.Rifle ? 0.045f : 0.035f);
            float settle = Wobble(t, 3.2f, 7f) * Clamp01(t / 0.05f);
            float s = 0.6f + 0.4f * _atk.Strength;
            if (w == WeaponClass.Rifle)
            {
                AddBody(p, HBone.Chest, -6f * kick * s, 2f * kick, 0f);
                AddBody(p, HBone.Spine, -3f * kick * s);
                BlendShoulder(p, false, kick, 3f, -10f * s);
                p.HipsOffset += new Vector3(0f, 0f, -0.025f * kick * s);
                p.R[(int)HBone.UpperArmR] = p.R[(int)HBone.UpperArmR] * Quaternion.AngleAxis(-6f * kick, Vector3.right);
                p.R[(int)HBone.UpperArmL] = p.R[(int)HBone.UpperArmL] * Quaternion.AngleAxis(-6f * kick, Vector3.right);
            }
            else if (w == WeaponClass.Crossbow)
            {
                AddBody(p, HBone.Chest, -2f * kick);
                p.R[(int)HBone.UpperArmR] = p.R[(int)HBone.UpperArmR] * Quaternion.AngleAxis(-3f * kick, Vector3.right);
            }
            else
            {
                // pistol: the forearm and wrist flip up, the shoulder takes it, the head blinks back
                int la = (int)HBone.LowerArmR, hd = (int)HBone.HandR, ua = (int)HBone.UpperArmR;
                p.R[ua] = p.R[ua] * Quaternion.AngleAxis(-10f * kick * s + 2f * settle * kick, Vector3.right);
                p.R[la] = p.R[la] * Quaternion.AngleAxis(-16f * kick * s, Vector3.right);
                p.R[hd] = p.R[hd] * Quaternion.AngleAxis(-22f * kick * s, Vector3.right);
                BlendShoulder(p, false, kick, 4f, -6f);
                AddBody(p, HBone.Chest, -2.5f * kick, 0f, 0f);
                AddBody(p, HBone.Head, -3f * kick, 0f, 0f);
            }
            _base.CopyFrom(p);
        }

        // ---------------------------------------------------------------- defence poses
        void EvaluateDefense(ActorPoses.Dims d)
        {
            float u = _def.T / _def.Dur;
            float sc = Height / 1.75f;
            float ao = ArmIdleOut;
            var p = _cb; p.CopyFrom(_base);
            Vector3 f = _def.From;
            float side = f.x >= 0f ? 1f : -1f;
            float env = Win(u, 0f, 0.1f) * (1f - Win(u, 0.75f, 1f));
            float yaw = Mathf.Clamp(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, -80f, 80f);
            float tremble = Noise(_time * 14f, 2f) * 1.5f;
            switch (_def.Kind)
            {
                case ActionAnim.TurnAway:
                {
                    float turn = Win(u, 0.02f, 0.22f);
                    AddBody(p, HBone.Hips, 6f * turn, -side * 55f * turn, 0f);
                    AddBody(p, HBone.Spine, 16f * turn, -side * 20f * turn, 0f);
                    AddBody(p, HBone.Chest, 14f * turn, -side * 15f * turn, 0f);
                    AddBody(p, HBone.Neck, 18f * turn);
                    AddBody(p, HBone.Head, 10f * turn);
                    p.HipsOffset += new Vector3(-side * 0.06f, -0.08f, -0.05f) * turn * sc;
                    p.SetArm(false, 150f, 32f, 60f, 132f + tremble, 20f, 30f);
                    p.SetArm(true, 148f, 34f, 60f, 134f - tremble, 20f, 30f);
                    p.SoftHand(false); p.SoftHand(true);
                    if (p.LegIK > 0.5f) { Vector3 away = new Vector3(-f.x, 0f, -f.z) * 0.18f * sc * turn; p.FootL += away; p.FootR += away * 0.5f; }
                    break;
                }
                case ActionAnim.Dodge:
                {
                    float dk = Bell(u, 0.02f, 0.14f, 0.5f, 0.9f);
                    AddBody(p, HBone.Spine, -8f * dk, 0f, side * 8f * dk);
                    AddBody(p, HBone.Chest, -10f * dk, -yaw * 0.2f * dk, side * 6f * dk);
                    p.HipsOffset += new Vector3(-f.x * 0.2f, -0.06f, -f.z * 0.22f) * dk * sc;
                    if (p.LegIK > 0.5f) { Vector3 back = new Vector3(-f.x, 0f, -f.z) * 0.32f * sc * dk; p.FootL += back; p.FootR += back * 0.6f; }
                    p.SetArm(false, 60f, ao + 6f, 30f, 100f, 40f, -20f); p.SetArm(true, 58f, ao + 6f, 30f, 102f, 40f, -20f);
                    p.OpenHand(false); p.OpenHand(true);
                    break;
                }
                case ActionAnim.GrabWrist:
                {
                    float g = Win(u, 0.04f, 0.2f);
                    AddBody(p, HBone.Chest, -8f * g + tremble * 0.5f, yaw * 0.3f, 0f);
                    AddBody(p, HBone.Spine, -4f * g);
                    p.HipsOffset += new Vector3(0f, -0.04f, -0.06f) * g * sc;
                    p.SetArm(true, 88f + tremble, 12f, 20f, 26f, 60f, -10f);     // left hand catches the weapon wrist
                    p.SetArm(false, 78f - tremble, -12f, 40f, 48f, 60f, -30f);   // right hand pushes the arm away
                    p.SetHand(true, 0.7f, 0.75f, 0.85f); p.OpenHand(false);
                    _grabWristW = g * env;
                    break;
                }
                default:    // Defend / Guard: forearms up
                {
                    bool guard = _def.Kind == ActionAnim.Guard;
                    float up = Win(u, 0.02f, 0.14f);
                    AddBody(p, HBone.Chest, -6f * up, yaw * 0.2f, 0f);
                    AddBody(p, HBone.Neck, 12f * up);
                    AddBody(p, HBone.Head, 10f * up, -side * 16f * up, 0f);
                    p.HipsOffset += new Vector3(0f, -0.05f, -0.05f) * up * sc;
                    BlendShoulder(p, false, up, 10f, 8f); BlendShoulder(p, true, up, 10f, 8f);
                    if (guard) { p.SetArm(false, 62f, 6f, 40f, 104f + tremble, 40f, -10f); p.SetArm(true, 60f, 6f, 40f, 106f - tremble, 40f, -10f); p.OpenHand(false); p.OpenHand(true); }
                    else { p.SetArm(false, 96f, -12f, 62f, 124f + tremble, 20f, 10f); p.SetArm(true, 94f, -10f, 62f, 126f - tremble, 20f, 10f); p.SoftHand(false); p.SoftHand(true); }
                    break;
                }
            }
            _base.Overlay(p, env, null, true);
        }

        // ---------------------------------------------------------------- stagger
        void EvaluateStagger(float dt, ActorPoses.Dims dims)
        {
            _stag.T += dt;
            float u = Clamp01(_stag.T / _stag.Dur);
            if (_stag.T >= _stag.Dur) { _stag.Active = false; return; }
            float w = Win(_stag.T, 0f, 0.05f) * (1f - Win(u, 0.84f, 1f));
            Vector3 d = _stag.Dir; float s = _stag.Str;
            float sc = Height / 1.75f;
            float jolt = Impulse(_stag.T, 0.1f);                                  // the upper body is thrown first
            float travel = EaseOut(u / 0.72f);                                     // then the steps catch it
            float balance = Bell(u, 0.04f, 0.2f, 0.55f, 0.9f);                    // arms out for balance
            // how far the body should be from where it started, minus what the root (physics / kernel) already moved
            float rootMoved = Vector3.Dot(transform.position - _stag.Root0, _stag.DirW);
            float remain = Mathf.Max(0f, _stag.Dist * travel - Mathf.Max(0f, rootMoved)) * (1f - Win(u, 0.7f, 0.97f));
            // braced on a table / rail: the body cannot go through the support (hips stay ~0.38 m short of the brace point)
            if (_physicalActions != null && _physicalActions.CurrentAction == PhysicalActionKind.Brace)
                remain = Mathf.Min(remain, Mathf.Max(0f, Vector3.Dot(new Vector3(_reachLocal.x, 0f, _reachLocal.z), d) - 0.38f * sc));
            var p = _tmp4; p.CopyFrom(_base);
            p.HipsOffset += d * remain + Vector3.down * (0.035f * sc * balance * (0.5f + s));
            // feet: planted in the world; each scheduled step lifts one foot from where it stands to where the body is now
            int n = _stag.Steps;
            Vector3 wantL = transform.TransformPoint(dims.FootL + d * remain), wantR = transform.TransformPoint(dims.FootR + d * remain);
            if (_stag.FinalLeft < 0 && u >= 0.76f)
                _stag.FinalLeft = Vector3.Distance(_stag.AnchorL, wantL) > Vector3.Distance(_stag.AnchorR, wantR) ? 1 : 0;
            for (int k = 0; k < 2; k++)
            {
                bool left = k == 0;
                Vector3 want = left ? wantL : wantR;
                int active = -1; float a0 = 0f, b0 = 1f;
                for (int st = 0; st <= n; st++)
                {
                    bool final = st == n;
                    bool stepLeft = final ? _stag.FinalLeft == 1 : ((st % 2 == 0) == _stag.FirstLeft);
                    if (final && _stag.FinalLeft < 0) continue;
                    if (stepLeft != left) continue;
                    float a = final ? 0.76f : 0.05f + st * (0.64f / n), b = final ? 0.96f : a + 0.58f / n;
                    if (u >= a) { active = st; a0 = a; b0 = b; }
                }
                ref Vector3 anchor = ref (left ? ref _stag.AnchorL : ref _stag.AnchorR);
                ref Vector3 from = ref (left ? ref _stag.FromL : ref _stag.FromR);
                ref int step = ref (left ? ref _stag.StepL : ref _stag.StepR);
                ref int land = ref (left ? ref _stag.LandL : ref _stag.LandR);
                Vector3 world = anchor; float lift = 0f;
                if (active >= 0 && land != active)
                {
                    if (step != active) { step = active; from = anchor; }
                    float su = Clamp01((u - a0) / Mathf.Max(0.01f, b0 - a0));
                    world = Vector3.Lerp(from, want, Ease(su));
                    lift = Mathf.Sin(su * Mathf.PI) * 0.055f * sc;
                    if (su >= 1f) { anchor = want; land = active; world = want; }
                }
                Vector3 local = transform.InverseTransformPoint(world);
                local.y = (left ? dims.FootL.y : dims.FootR.y) + lift;
                if (left) p.FootL = local; else p.FootR = local;
            }
            p.LegIK = 1f;
            // torso thrown along the push (pushed back -> leans back), head lags the other way
            float pitch = d.z * 18f * jolt * (0.5f + s), lean = -d.x * 15f * jolt * (0.5f + s);
            AddBody(p, HBone.Spine, pitch * 0.5f, 0f, lean * 0.5f);
            AddBody(p, HBone.Chest, pitch * 0.6f, 6f * jolt * (d.x >= 0 ? 1f : -1f), lean * 0.6f);
            AddBody(p, HBone.Neck, -pitch * 0.5f, 0f, -lean * 0.4f);
            AddBody(p, HBone.Head, -pitch * 0.4f, -8f * jolt, -lean * 0.4f);
            float ao = ArmIdleOut;
            BlendArm(p, true, balance, 16f + 14f * s - d.z * 10f, ao + 34f + 24f * s, 10f, 30f + 20f * balance, 10f, -10f);
            BlendArm(p, false, balance, 16f + 14f * s - d.z * 10f, ao + 34f + 24f * s, 10f, 30f + 20f * balance, 10f, -10f);
            BlendHand(p, true, balance, 0.1f, 0.06f, 0.12f); BlendHand(p, false, balance, 0.1f, 0.06f, 0.12f);
            _base.Overlay(p, w, null, true);
        }
    }
}
