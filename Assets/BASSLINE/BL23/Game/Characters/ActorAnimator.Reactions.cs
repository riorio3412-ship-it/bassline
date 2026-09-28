using UnityEngine;
using static BL23.Game.Characters.MotionKit;

namespace BL23.Game.Characters
{
    /// <summary>Reactions (motion track): region flinches, protective falls, get-up, startle, loud noises, pain and held wounds.
    /// Presentation only; the kernel / physicality layer decides what happens and calls these.</summary>
    public partial class ActorAnimator
    {
        // ================================================================ state
        struct HitState { public bool Active; public BodyRegion Region; public Vector3 Dir; public float T, Dur, Str; public int Seed; }
        HitState _hit;
        struct StartleState { public bool Active; public Vector3 Src; public float T, Dur, Str; public bool Duck; public bool StepLeft; }
        StartleState _startle;
        struct FallState { public bool Active; public Vector3 Dir; public float T, Str; public bool Back; }
        FallState _fall;
        struct GetUpState { public bool Active; public float T, Dur; public int Kind; }   // 0 face up, 1 face down, 2 from sitting / side
        GetUpState _getUp;
        float _pain, _painShown;
        BodyRegion? _woundHold; float _woundHoldW;
        readonly ActorPose _rx = new ActorPose(), _rx2 = new ActorPose(), _getUpFrom = new ActorPose();

        public bool Reacting => _hit.Active || _startle.Active || _fall.Active || _getUp.Active;

        // ================================================================ API
        /// <summary>A blow landed on 'region' travelling along worldDir (strength 0..1): head snap, recoil, doubled over after a gut
        /// blow, a hand flying to the wound; strong hits add a stagger with recovery steps.</summary>
        public void PlayHit(BodyRegion region, Vector3 worldDir, float strength)
        {
            Init();
            if (!_init || _dead >= 0 || _beingCarried || _pairedRef != null) return;
            float s = Clamp01(strength);
            Vector3 d = transform.InverseTransformDirection(worldDir); d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) d = Vector3.back;
            d.Normalize();
            _hit.Active = true; _hit.Region = region; _hit.Dir = d; _hit.T = 0f; _hit.Str = s; _hit.Seed++;
            _hit.Dur = HitDuration(region, s);
            if (_rig != null && _rig.Face != null) _rig.Face.PushTransient(s > 0.5f ? Expr.Pain : Expr.Surprised, 1f, Mathf.Min(_hit.Dur, 1.6f));
            // look toward where the blow came from
            Glance(transform.position - worldDir.normalized * 1.5f + Vector3.up * Height * 0.9f, 0.9f);
            if (s > 0.55f && _posture == Posture.Stand && _conscious && region != BodyRegion.HandL && region != BodyRegion.HandR)
                PlayStagger(worldDir, Mathf.Clamp01((s - 0.4f) * (region == BodyRegion.Abdomen ? 0.7f : 1.4f)));
        }

        static float HitDuration(BodyRegion r, float s)
        {
            switch (r)
            {
                case BodyRegion.Abdomen: return 1.3f + 1.1f * s;
                case BodyRegion.Head: case BodyRegion.Neck: return 0.9f + 0.9f * s;
                case BodyRegion.LegL: case BodyRegion.LegR: case BodyRegion.FootL: case BodyRegion.FootR: return 0.9f + 0.7f * s;
                default: return 0.8f + 0.8f * s;
            }
        }

        /// <summary>Balance lost along worldDir: arms reach out to break the fall (or toward a brace point given by
        /// PhysicalActionController.Brace); the physical ragdoll (PhysicalRagdoll) then takes the body. Without a ragdoll the body
        /// goes down procedurally and lands in LieFront / LieBack.</summary>
        public void PlayFall(Vector3 worldDir, float strength)
        {
            Init();
            if (!_init || _dead >= 0 || _beingCarried) return;
            Vector3 d = transform.InverseTransformDirection(worldDir); d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) d = Vector3.forward;
            d.Normalize();
            _fall.Active = true; _fall.Dir = d; _fall.T = 0f; _fall.Str = Clamp01(strength); _fall.Back = d.z < -0.3f;
            _stag.Active = false; _hit.Active = false; _startle.Active = false;
            _aActive = false; _gActive = false;
            _from.CopyFrom(_last);
            if (_rig != null && _rig.Face != null) _rig.Face.PushTransient(Expr.Fear, 1f, 1.4f);
            _ragdollOverlay.Begin(d, _fall.Back);
        }

        /// <summary>From the current lying posture to standing: roll, push up, kneel, rise. pain01 slows it. Returns the seconds.</summary>
        public float PlayGetUp(float pain01 = 0f)
        {
            Init();
            if (!_init || _dead >= 0 || _beingCarried) return 0f;
            int kind = LyingKind();
            _getUpFrom.CopyFrom(_last);
            ReadCanonicalIfLive(_getUpFrom);
            _getUp.Active = true; _getUp.T = 0f; _getUp.Kind = kind;
            _getUp.Dur = (kind == 2 ? 1.5f : 2.4f) * (1f + 0.7f * Clamp01(pain01));
            _trans = null; _fall.Active = false;
            _posture = Posture.Stand; _postureT = 1f;
            _aActive = false; _gActive = false;
            if (_rig != null && _rig.Face != null && pain01 > 0.3f) _rig.Face.PushTransient(Expr.Pain, 0.7f, _getUp.Dur);
            return _getUp.Dur;
        }

        /// <summary>Startle toward sourceWorld (strength 0..1): a jolt, shoulders up, a half step back, the head then the body turn.</summary>
        public void PlayStartle(Vector3 sourceWorld, float strength)
        {
            Init();
            if (!_init || _dead >= 0 || _beingCarried || !_conscious || _pairedRef != null) return;
            float s = Clamp01(strength);
            Vector3 src = transform.InverseTransformPoint(sourceWorld); src.y = 0f;
            if (src.sqrMagnitude < 1e-4f) src = Vector3.forward;
            _startle.Active = true; _startle.Src = src.normalized; _startle.T = 0f; _startle.Str = s;
            _startle.Dur = 1.1f + 0.6f * s; _startle.Duck = false;
            _startle.StepLeft = src.x > 0f;   // the foot away from the source steps back
            Glance(sourceWorld + Vector3.up * 0.2f, _startle.Dur + 0.8f);
            if (_rig != null && _rig.Face != null) _rig.Face.PushTransient(s > 0.6f ? Expr.Fear : Expr.Surprised, 1f, _startle.Dur);
        }

        /// <summary>A loud sound at sourceWorld (loudness 0..1): gunshot / crash = duck and cover, slam = flinch, thud = glance.</summary>
        public void HearNoise(Vector3 sourceWorld, float loudness01)
        {
            Init();
            if (!_init || _dead >= 0 || !_conscious || _beingCarried) return;
            float l = Clamp01(loudness01);
            if (l >= 0.85f && _posture == Posture.Stand && _pairedRef == null)
            {
                PlayStartle(sourceWorld, 1f);
                _startle.Duck = true; _startle.Dur = 2.2f;
            }
            else if (l >= 0.45f) PlayStartle(sourceWorld, Mathf.Lerp(0.35f, 0.8f, (l - 0.45f) / 0.4f));
            else if (l >= 0.15f) Glance(sourceWorld, 1.2f + l * 2f);
        }

        /// <summary>Continuous pain 0..1 (e.g. PhysicalBody.Pain): guarded posture, shallow breathing, trembling.</summary>
        public void SetPain(float pain01) { _pain = Clamp01(pain01); }

        /// <summary>A hand pressed on a wound while the actor keeps going (null = off).</summary>
        public void SetWoundHold(BodyRegion? region) { _woundHold = region; }

        // ================================================================ evaluation (called from Evaluate)
        void EvaluateReactions(float dt, ActorPoses.Dims dims)
        {
            _painShown = Mathf.MoveTowards(_painShown, _dead < 0 && _conscious ? _pain : 0f, dt * 0.8f);
            if (_dead >= 0 || _beingCarried) { _hit.Active = _startle.Active = _fall.Active = _getUp.Active = false; return; }
            if (_getUp.Active) EvaluateGetUp(dt, dims);
            if (_fall.Active) EvaluateFall(dt, dims);
            if (!_conscious) { _hit.Active = false; _startle.Active = false; return; }
            if (_painShown > 0.02f && _posture == Posture.Stand) PainPosture(dims);
            if (_startle.Active) EvaluateStartle(dt, dims);
            if (_hit.Active) EvaluateHit(dt, dims);
        }

        // ---------------------------------------------------------------- hit
        void EvaluateHit(float dt, ActorPoses.Dims dims)
        {
            _hit.T += dt;
            float t = _hit.T, u = t / _hit.Dur, s = _hit.Str;
            if (u >= 1f) { _hit.Active = false; return; }
            float sc = Height / 1.75f;
            float env = Win(t, 0f, 0.03f) * (1f - Win(u, 0.72f, 1f));
            float jolt = Impulse(t, 0.075f);
            float jolt2 = Impulse(t - 0.05f, 0.12f);                 // lagging secondary (head whip, arms)
            float hold = Win(t, 0.05f, 0.3f) * (1f - Win(u, 0.62f, 0.98f));   // the protective hold, overlapping the jolt
            Vector3 d = _hit.Dir;
            var p = _rx; p.CopyFrom(_base);
            float k = 0.45f + 0.75f * s;
            bool standing = _posture == Posture.Stand;
            switch (_hit.Region)
            {
                case BodyRegion.Head:
                case BodyRegion.Neck:
                {
                    bool neck = _hit.Region == BodyRegion.Neck;
                    AddBody(p, HBone.Head, d.z * 30f * k * jolt - 8f * hold, d.x * 34f * k * jolt, -d.x * 20f * k * jolt);
                    AddBody(p, HBone.Neck, d.z * 16f * k * jolt, d.x * 14f * k * jolt, -d.x * 10f * k * jolt);
                    AddBody(p, HBone.Chest, d.z * 8f * k * jolt2 + 6f * hold, -d.x * 6f * k * jolt2, 0f);
                    AddBody(p, HBone.Spine, 5f * hold);
                    if (standing) p.HipsOffset += new Vector3(d.x * 0.02f, -0.035f * k * jolt - 0.02f * hold, d.z * 0.03f * k * jolt2) * sc;
                    // hands fly to the face / throat: the near hand always, both on hard hits
                    float h = hold * Mathf.Clamp01(s * 1.6f);
                    bool nearLeft = d.x > 0.2f;                        // pushed right = struck on the left side
                    float face = neck ? 0f : 1f;
                    BlendArm(p, nearLeft, h, 38f + 14f * face, -12f, 42f, 128f + 10f * face, 30f, 18f);
                    BlendHand(p, nearLeft, h, 0.25f, 0.3f, 0.4f);
                    if (s > 0.55f || neck) { BlendArm(p, !nearLeft, h * 0.9f, 36f + 12f * face, -14f, 40f, 124f + 10f * face, 30f, 18f); BlendHand(p, !nearLeft, h, 0.25f, 0.3f, 0.4f); }
                    break;
                }
                case BodyRegion.Abdomen:
                {
                    float fold = Mathf.Max(jolt, hold) * k;
                    AddBody(p, HBone.Spine, 30f * fold, -d.x * 6f * jolt, 0f);
                    AddBody(p, HBone.Chest, 22f * fold, 0f, 0f);
                    AddBody(p, HBone.Neck, -10f * fold);
                    AddBody(p, HBone.Head, 4f * fold);
                    AddBody(p, HBone.Hips, 12f * fold);
                    if (standing) p.HipsOffset += new Vector3(0f, -0.09f * fold, -0.08f * fold + d.z * 0.03f * jolt) * sc;
                    float h = Mathf.Max(jolt * 0.8f, hold);
                    BlendArm(p, false, h, 34f, -18f, 52f, 96f, 25f, -10f);
                    BlendArm(p, true, h * 0.95f, 30f, -16f, 50f, 102f, 25f, -10f);
                    BlendHand(p, false, h, 0.35f, 0.45f, 0.55f); BlendHand(p, true, h, 0.35f, 0.45f, 0.55f);
                    BlendShoulder(p, false, h, 8f, 10f); BlendShoulder(p, true, h, 8f, 10f);
                    break;
                }
                case BodyRegion.Back:
                {
                    AddBody(p, HBone.Chest, -20f * k * jolt + 12f * hold, 0f, -d.x * 8f * jolt);
                    AddBody(p, HBone.Spine, -10f * k * jolt + 6f * hold);
                    AddBody(p, HBone.Head, -16f * k * jolt2 + 6f * hold);
                    if (standing) p.HipsOffset += new Vector3(0f, -0.02f * jolt, 0.05f * k * jolt) * sc;
                    float f = jolt2 * k;
                    BlendArm(p, false, f, 20f, ArmIdleOut + 30f, 0f, 30f);
                    BlendArm(p, true, f, 20f, ArmIdleOut + 30f, 0f, 30f);
                    BlendShoulder(p, false, Mathf.Max(f, hold), -4f, -10f); BlendShoulder(p, true, Mathf.Max(f, hold), -4f, -10f);
                    // a hand goes back toward the wound
                    BlendArm(p, false, hold * 0.9f, -34f, ArmIdleOut + 6f, 85f, 95f, 20f, 0f);
                    BlendHand(p, false, hold, 0.2f, 0.25f, 0.3f);
                    break;
                }
                case BodyRegion.LegL: case BodyRegion.LegR: case BodyRegion.FootL: case BodyRegion.FootR:
                {
                    bool left = _hit.Region == BodyRegion.LegL || _hit.Region == BodyRegion.FootL;
                    float buckle = Mathf.Max(jolt, hold * 0.8f) * k;
                    float side = left ? 1f : -1f;
                    AddBody(p, HBone.Hips, 6f * buckle, 0f, side * 7f * buckle);
                    AddBody(p, HBone.Spine, 14f * buckle, 0f, -side * 5f * buckle);
                    AddBody(p, HBone.Chest, 10f * buckle);
                    if (standing)
                    {
                        p.HipsOffset += new Vector3(-side * 0.03f * buckle, -0.08f * buckle, -0.02f * buckle) * sc;
                        if (p.LegIK > 0.5f)
                        {
                            // weight off the struck leg: its foot lifts and slides a little
                            Vector3 lift = new Vector3(0f, 0.04f * jolt * k, -0.03f * jolt) * sc;
                            if (left) p.FootL += lift; else p.FootR += lift;
                        }
                    }
                    // the hand on the struck side reaches down to the thigh / knee, the other arm balances
                    BlendArm(p, left, hold, 32f, 2f, 20f, 26f, 30f, 10f);
                    BlendHand(p, left, hold, 0.3f, 0.35f, 0.4f);
                    BlendArm(p, !left, jolt * k, 15f, ArmIdleOut + 28f, 0f, 35f);
                    break;
                }
                default:
                {
                    // chest, shoulders, arms, hands
                    bool armHit = _hit.Region != BodyRegion.Chest;
                    bool left = _hit.Region == BodyRegion.ShoulderL || _hit.Region == BodyRegion.ArmL || _hit.Region == BodyRegion.HandL;
                    AddBody(p, HBone.Chest, d.z * 20f * k * jolt + 10f * hold, (armHit ? (left ? -1f : 1f) * 12f * jolt : -d.x * 10f * jolt) * k, -d.x * 8f * k * jolt);
                    AddBody(p, HBone.Spine, d.z * 9f * k * jolt + 5f * hold);
                    AddBody(p, HBone.Head, -d.z * 12f * k * jolt2, 0f, d.x * 6f * jolt2);
                    if (standing) p.HipsOffset += new Vector3(d.x * 0.03f * jolt, -0.025f * k * jolt, d.z * 0.04f * k * jolt) * sc;
                    if (!armHit)
                    {
                        // hand flat on the sternum, shoulders rounded
                        BlendArm(p, false, hold, 22f, -20f, 55f, 118f, 30f, 10f);
                        BlendHand(p, false, hold, 0.15f, 0.18f, 0.22f);
                        BlendShoulder(p, false, hold, 6f, 12f); BlendShoulder(p, true, hold, 4f, 10f);
                        BlendArm(p, true, jolt2 * k, 18f, ArmIdleOut + 22f, 0f, 40f);
                    }
                    else
                    {
                        // the struck arm jerks away and is then held close; the other hand clutches it
                        float jerk = jolt * k;
                        BlendArm(p, left, jerk, 12f + d.z * 25f, ArmIdleOut + 18f + (left ? -d.x : d.x) * 25f, 0f, 30f);
                        BlendShoulder(p, left, Mathf.Max(jerk, hold), 12f, 6f);
                        BlendArm(p, left, hold, 18f, ArmIdleOut - 4f, 30f, 72f, 20f, 0f);
                        BlendArm(p, !left, hold, 40f, -26f, 62f, 92f, 20f, 0f);
                        BlendHand(p, !left, hold, 0.55f, 0.6f, 0.68f);
                        AddBody(p, HBone.Head, 14f * hold, (left ? -1f : 1f) * 18f * hold, 0f);
                        AddBody(p, HBone.Chest, 6f * hold, (left ? -1f : 1f) * 10f * hold, 0f);
                    }
                    break;
                }
            }
            // eyes squeezed, the whole body trembles a little after hard blows
            if (s > 0.5f)
            {
                float tr = (1f - u) * 1.6f * s;
                AddBody(p, HBone.Chest, Noise(_time * 13f, 3f) * tr, Noise(_time * 11f, 5f) * tr, 0f);
            }
            _base.Overlay(p, env, null, true);
        }

        // ---------------------------------------------------------------- pain (continuous)
        void PainPosture(ActorPoses.Dims dims)
        {
            float a = _painShown;
            float breath = Mathf.Sin(_time * (2.2f + 2.5f * a));
            AddBody(_base, HBone.Spine, 7f * a);
            AddBody(_base, HBone.Chest, 5f * a + breath * 1.6f * a);
            AddBody(_base, HBone.Head, -4f * a);
            BlendShoulder(_base, true, a * 0.6f, 5f + breath * 2f, 6f);
            BlendShoulder(_base, false, a * 0.6f, 5f + breath * 2f, 6f);
            if (a > 0.55f)
            {
                float tr = (a - 0.55f) * 2.5f;
                AddBody(_base, HBone.Head, Noise(_time * 9f, 1f) * tr, Noise(_time * 8f, 2f) * tr, 0f);
                int ua = (int)HBone.LowerArmR, la = (int)HBone.LowerArmL;
                _base.R[ua] = _base.R[ua] * Quaternion.AngleAxis(Noise(_time * 12f, 4f) * 2.5f * tr, Vector3.right);
                _base.R[la] = _base.R[la] * Quaternion.AngleAxis(Noise(_time * 12f, 6f) * 2.5f * tr, Vector3.right);
            }
        }

        // ---------------------------------------------------------------- startle / noise
        void EvaluateStartle(float dt, ActorPoses.Dims dims)
        {
            _startle.T += dt;
            float t = _startle.T, u = t / _startle.Dur, s = _startle.Str;
            if (u >= 1f) { _startle.Active = false; return; }
            float sc = Height / 1.75f;
            float env = Win(t, 0f, 0.035f) * (1f - Win(u, 0.7f, 1f));
            float jolt = Impulse(t, 0.08f);
            float hold = Bell(u, 0.05f, 0.18f, 0.5f, 0.95f);
            Vector3 src = _startle.Src;
            var p = _rx; p.CopyFrom(_base);
            float away = Mathf.Clamp(-src.z, -1f, 1f);
            // shoulders jump up, arms pull in, a small hop then a crouch away from the source
            float up = Mathf.Max(jolt, hold * 0.6f) * (0.5f + 0.7f * s);
            BlendShoulder(p, true, up, 16f, 8f); BlendShoulder(p, false, up, 16f, 8f);
            AddBody(p, HBone.Chest, -src.z * 9f * s * jolt - 4f * hold, 0f, src.x * 6f * s * jolt);
            AddBody(p, HBone.Neck, 6f * jolt * s);
            AddBody(p, HBone.Head, 8f * jolt * s);
            if (_posture == Posture.Stand)
            {
                p.HipsOffset += new Vector3(-src.x * 0.03f * s * hold, 0.022f * s * Impulse(t, 0.06f) - 0.04f * s * hold, -src.z * 0.05f * s * hold) * sc;
                if (p.LegIK > 0.5f)
                {
                    // the far foot steps back and returns
                    float step = Bell(u, 0.06f, 0.2f, 0.62f, 0.85f);
                    float lift = Mathf.Sin(Win(u, 0.06f, 0.2f) * Mathf.PI) * 0.05f + Mathf.Sin(Win(u, 0.62f, 0.85f) * Mathf.PI) * 0.035f;
                    Vector3 back = new Vector3(-src.x * 0.06f, lift, -src.z * 0.16f - 0.04f) * s * sc;
                    if (_startle.StepLeft) p.FootL += back * (step > 0f ? 1f : 0f) * Mathf.Max(step, lift > 0.001f ? step : 0f);
                    else p.FootR += back * Mathf.Max(step, 0f);
                }
            }
            // hands come up (open) in front of the chest; strong startles guard the face
            float h = Mathf.Max(jolt * 0.9f, hold) * s;
            float guard = s > 0.7f ? 1f : 0f;
            BlendArm(p, false, h, 34f + 26f * guard, -4f, 40f, 96f + 26f * guard, 40f, -20f);
            BlendArm(p, true, h, 30f + 24f * guard, -2f, 40f, 92f + 26f * guard, 40f, -20f);
            BlendHand(p, false, h, 0.08f, 0.05f, 0.1f); BlendHand(p, true, h, 0.08f, 0.05f, 0.1f);
            // the body turns toward the source after the head (the look-at chain turns the head)
            float yaw = Mathf.Clamp(Mathf.Atan2(src.x, src.z) * Mathf.Rad2Deg, -80f, 80f);
            float turn = Win(u, 0.15f, 0.45f) * (1f - Win(u, 0.75f, 1f));
            AddBody(p, HBone.Spine, 0f, yaw * 0.18f * turn, 0f);
            AddBody(p, HBone.Chest, 0f, yaw * 0.22f * turn, 0f);
            if (_startle.Duck)
            {
                // gunshot / crash: duck, hands over the head, then slowly straighten
                float dk = Bell(u, 0.04f, 0.14f, 0.55f, 0.95f);
                AddBody(p, HBone.Spine, 22f * dk); AddBody(p, HBone.Chest, 16f * dk); AddBody(p, HBone.Neck, 14f * dk); AddBody(p, HBone.Head, 10f * dk);
                if (_posture == Posture.Stand) p.HipsOffset += new Vector3(0f, -0.2f * dk, -0.05f * dk) * sc;
                BlendArm(p, false, dk, 148f, 30f, 60f, 128f, 20f, 30f);
                BlendArm(p, true, dk, 146f, 32f, 60f, 130f, 20f, 30f);
                BlendHand(p, false, dk, 0.2f, 0.25f, 0.3f); BlendHand(p, true, dk, 0.2f, 0.25f, 0.3f);
            }
            _base.Overlay(p, env, null, true);
        }

        // ---------------------------------------------------------------- fall
        void EvaluateFall(float dt, ActorPoses.Dims dims)
        {
            _fall.T += dt;
            float t = _fall.T;
            Vector3 d = _fall.Dir;
            float sc = Height / 1.75f;
            // 0 .. 0.32 s: balance goes, a catching step, arms reach along the fall; later (no ragdoll took over): down to the floor
            float go = EaseIn(t / 0.34f);
            float down = Ease((t - 0.3f) / 0.5f);
            var p = _rx; p.CopyFrom(_base);
            bool back = _fall.Back;
            float side = Mathf.Clamp(d.x, -1f, 1f);
            float fwd = back ? -1f : 1f;
            // torso pitches along the fall, knees give
            AddBody(p, HBone.Hips, fwd * 14f * go, 0f, -side * 10f * go);
            AddBody(p, HBone.Spine, fwd * 12f * go, 0f, -side * 6f * go);
            AddBody(p, HBone.Chest, fwd * 8f * go);
            AddBody(p, HBone.Neck, back ? 18f * go : -16f * go);          // chin tucked going backward, head up going forward
            AddBody(p, HBone.Head, back ? 12f * go : -10f * go);
            p.HipsOffset += new Vector3(side * 0.08f * go, -0.14f * go, d.z * 0.12f * go) * sc;
            if (p.LegIK > 0.5f)
            {
                // a late catching step along the fall
                Vector3 step = new Vector3(d.x * 0.22f, Mathf.Sin(Clamp01(t / 0.3f) * Mathf.PI) * 0.06f, d.z * 0.3f) * sc * Win(t, 0.02f, 0.28f);
                if (d.x > 0.3f || (Mathf.Abs(d.x) <= 0.3f && ((_seed * 10f) % 2f) < 1f)) p.FootR += step; else p.FootL += step;
            }
            // arms reach to break the fall: forward = palms toward the floor ahead, backward = hands behind, sideways = that side
            float reach = Win(t, 0.02f, 0.22f);
            if (!back)
            {
                BlendArm(p, false, reach, 78f, ArmIdleOut + 14f + side * 18f, 0f, 14f, 60f, -38f);
                BlendArm(p, true, reach, 74f, ArmIdleOut + 14f - side * 18f, 0f, 16f, 60f, -38f);
            }
            else
            {
                BlendArm(p, false, reach, -42f, ArmIdleOut + 22f, -20f, 18f, 0f, -40f);
                BlendArm(p, true, reach, -40f, ArmIdleOut + 24f, -20f, 20f, 0f, -40f);
            }
            BlendHand(p, false, reach, 0.05f, 0.02f, 0.05f); BlendHand(p, true, reach, 0.05f, 0.02f, 0.05f);
            if (down > 0f)
            {
                // no ragdoll: finish the fall procedurally (hands take the weight going forward, the seat going backward)
                var lie = _rx2; lie.Reset();
                ActorPoses.PostureBase(lie, dims, back ? Posture.LieBack : Posture.LieFront, _time, "");
                if (!back) { lie.SetArm(false, 95f, 30f, -10f, 70f, 60f, -20f); lie.SetArm(true, 92f, 30f, -10f, 75f, 60f, -20f); }
                MotionKit.ShiftRoot(lie, new Vector3(d.x, 0f, d.z) * 0.35f * sc, Quaternion.identity);
                p.Blend(p, lie, down);
            }
            _base.CopyFrom(p);
            if (t > 0.85f)
            {
                _fall.Active = false;
                _from.CopyFrom(p);
                _posture = back ? Posture.LieBack : Posture.LieFront; _postureT = 0.99f;
                _last.CopyFrom(p);
            }
        }

        // ---------------------------------------------------------------- get up
        int LyingKind()
        {
            // read the real body (after a ragdoll the bones may lie any way)
            if (_b == null || _b[0] == null) return _posture == Posture.LieFront ? 1 : 0;
            Transform hips = _b[0], chest = _b[(int)HBone.Chest];
            Vector3 upAlongSpine = (chest.position - hips.position).normalized;
            if (upAlongSpine.y > 0.55f) return 2;                   // already sitting up / slumped
            Vector3 front = transform.InverseTransformDirection(hips.rotation * Quaternion.Inverse(_restG[0]) * Vector3.forward);
            // the hips' canonical forward: up = face up, down = face down
            Vector3 fw = hips.rotation * Quaternion.Inverse(transform.rotation * _restG[0]) * transform.forward;
            float faceUp = fw.y;
            if (Mathf.Abs(faceUp) < 0.2f) return _posture == Posture.LieFront ? 1 : 0;
            return faceUp > 0f ? 0 : 1;
        }

        void ReadCanonicalIfLive(ActorPose into)
        {
            if (_b == null || _b[0] == null) return;
            ReadCanonicalInto(into);
            // keep the hand shapes of the last pose
            for (int k = 0; k < 6; k++) into.Finger[k] = _last.Finger[k];
        }

        void EvaluateGetUp(float dt, ActorPoses.Dims dims)
        {
            _getUp.T += dt;
            float u = _getUp.T / _getUp.Dur;
            if (u >= 1f) { _getUp.Active = false; _from.CopyFrom(_base); _postureT = 1f; return; }
            float sc = Height / 1.75f;
            var k0 = _getUpFrom;
            var a = _rx; var b = _rx2;
            // keyframes: 0 lying (the real pose), then per kind; each segment eases
            int kind = _getUp.Kind;
            float[] times = kind == 0 ? GetUpTimesUp : kind == 1 ? GetUpTimesDown : GetUpTimesSit;
            int seg = 0;
            while (seg < times.Length - 2 && u > times[seg + 1]) seg++;
            float lu = Ease((u - times[seg]) / Mathf.Max(1e-3f, times[seg + 1] - times[seg]));
            if (seg == 0) a.CopyFrom(k0); else GetUpKey(a, dims, kind, seg, sc);
            GetUpKey(b, dims, kind, seg + 1, sc);
            var p = _tmp4; p.Blend(a, b, lu);
            // the last key is the live standing pose (idle / walk already in _base)
            if (seg + 1 == times.Length - 1) p.Blend(a, _base, lu);
            _base.CopyFrom(p);
        }

        static readonly float[] GetUpTimesUp = { 0f, 0.2f, 0.42f, 0.66f, 0.84f, 1f };
        static readonly float[] GetUpTimesDown = { 0f, 0.22f, 0.44f, 0.66f, 0.84f, 1f };
        static readonly float[] GetUpTimesSit = { 0f, 0.45f, 0.75f, 1f };

        void GetUpKey(ActorPose p, ActorPoses.Dims d, int kind, int key, float sc)
        {
            p.Reset();
            int last = (kind == 2 ? GetUpTimesSit.Length : GetUpTimesUp.Length) - 1;
            if (key >= last) { p.CopyFrom(_base); return; }
            float ao = ArmIdleOut;
            if (kind == 2) key += 2;                                   // sitting: skip to the kneel keys
            else if (kind == 1 && key <= 2)
            {
                if (key == 1)
                {
                    // face down: hands under the shoulders, press the chest up
                    ActorPoses.PostureBase(p, d, Posture.LieFront, _time, "");
                    p.R[(int)HBone.Spine] = ActorPose.Body(-22f);
                    p.R[(int)HBone.Chest] = ActorPose.Body(-12f);
                    p.R[(int)HBone.Neck] = ActorPose.Body(-14f);
                    p.R[(int)HBone.Head] = ActorPose.Body(-10f);
                    p.SetArm(true, 62f, ao + 12f, -30f, 70f, 40f, -60f);
                    p.SetArm(false, 62f, ao + 12f, -30f, 70f, 40f, -60f);
                    p.OpenHand(true); p.OpenHand(false);
                    p.HipsOffset += new Vector3(0f, 0.04f * sc, 0f);
                    return;
                }
                // all fours: hips up over the knees, arms straight down
                p.LegIK = 0f;
                p.R[0] = ActorPose.Body(95f);
                float hipH = 0.085f * sc + d.UpperLeg * 0.98f;
                p.HipsOffset = new Vector3(0f, hipH + d.HipsAboveJoint * 0.1f - d.RestHips.y, -0.05f * sc);
                p.R[(int)HBone.Spine] = ActorPose.Body(-6f);
                p.R[(int)HBone.Chest] = ActorPose.Body(-4f);
                p.R[(int)HBone.Neck] = ActorPose.Body(-30f);
                p.R[(int)HBone.Head] = ActorPose.Body(-10f);
                p.SetArm(true, 88f, ao + 4f, 0f, 8f, 0f, -70f);
                p.SetArm(false, 88f, ao + 4f, 0f, 8f, 0f, -70f);
                p.SetLegFK(true, 88f, 6f, 92f, 45f);
                p.SetLegFK(false, 88f, 6f, 92f, 45f);
                p.OpenHand(true); p.OpenHand(false);
                return;
            }
            else if (kind == 0 && key <= 2)
            {
                if (key == 1)
                {
                    // face up: roll toward the right side, the top arm reaching across, knees drawn up
                    ActorPoses.PostureBase(p, d, Posture.LieBack, _time, "");
                    p.R[0] = p.R[0] * Quaternion.AngleAxis(-38f, Vector3.up);
                    p.R[(int)HBone.Spine] = ActorPose.Body(12f, -10f, 0f);
                    p.R[(int)HBone.Chest] = ActorPose.Body(12f, -12f, 0f);
                    p.R[(int)HBone.Neck] = ActorPose.Body(24f);
                    p.SetArm(true, 78f, -10f, 20f, 40f, 20f, 0f);
                    p.SetArm(false, 30f, ao + 30f, 0f, 30f, 20f, -40f);
                    p.SetLegFK(true, 30f, 4f, 60f, -10f);
                    p.SetLegFK(false, 18f, 8f, 40f, -10f);
                    return;
                }
                // propped on one hand, sitting sideways, legs folded
                p.LegIK = 0f;
                p.R[0] = ActorPose.Body(-35f, -25f, 0f);
                p.HipsOffset = new Vector3(0.03f * sc, 0.12f * sc - d.RestHips.y, -0.02f);
                p.R[(int)HBone.Spine] = ActorPose.Body(18f, 8f, -6f);
                p.R[(int)HBone.Chest] = ActorPose.Body(12f, 6f, -4f);
                p.R[(int)HBone.Neck] = ActorPose.Body(8f);
                p.R[(int)HBone.Head] = ActorPose.Body(4f, 10f, 0f);
                p.SetArm(false, -10f, ao + 34f, -40f, 6f, 0f, -70f);         // right hand planted behind-side
                p.SetArm(true, 40f, ao + 2f, 20f, 60f, 20f, 0f);
                p.SetLegFK(true, 70f, -8f, 120f, 10f, 20f);
                p.SetLegFK(false, 60f, 20f, 110f, 10f, -10f);
                p.OpenHand(false);
                return;
            }
            // shared: kneel on the left knee, right foot planted, hand on the right knee; then rising with the hand pushing
            bool rising = kind == 2 ? key >= 4 : key >= 4;
            if (!rising)
            {
                p.LegIK = 0f;
                float hipH = 0.07f * sc + d.UpperLeg * 0.95f;
                p.HipsOffset = new Vector3(0f, hipH + d.HipsAboveJoint - d.RestHips.y, -0.04f * sc);
                p.R[0] = ActorPose.Body(18f);
                p.SetLegFK(true, 5f, 4f, 95f, 50f);                          // left knee on the floor
                p.SetLegFK(false, 88f, 6f, 92f, -2f);                        // right foot forward, knee up
                p.R[(int)HBone.Spine] = ActorPose.Body(14f);
                p.R[(int)HBone.Chest] = ActorPose.Body(8f);
                p.R[(int)HBone.Neck] = ActorPose.Body(-4f);
                p.SetArm(false, 42f, ao - 2f, 30f, 38f, 30f, -30f);          // right hand on the right knee
                p.SetArm(true, 18f, ao + 8f, 10f, 30f, 20f, 0f);
                p.SoftHand(false);
                return;
            }
            // rising: weight over the front foot, hands push on the thigh
            p.LegIK = 1f;
            p.FootL = d.FootL + new Vector3(0f, 0f, -0.16f * sc);
            p.FootR = d.FootR + new Vector3(0f, 0f, 0.14f * sc);
            p.FootRotL = ActorPose.E(28f, -6f, 0f);
            p.HipsOffset = new Vector3(0f, -0.26f * sc, 0.02f * sc);
            p.R[0] = ActorPose.Body(30f);
            p.R[(int)HBone.Spine] = ActorPose.Body(16f);
            p.R[(int)HBone.Chest] = ActorPose.Body(6f);
            p.R[(int)HBone.Neck] = ActorPose.Body(-8f);
            p.SetArm(false, 34f, ao - 4f, 30f, 30f, 30f, -30f);
            p.SetArm(true, 30f, ao - 2f, 30f, 36f, 30f, -30f);
            p.SoftHand(false); p.SoftHand(true);
        }
    }
}
