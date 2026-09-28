using UnityEngine;
using BL23.Game.Physicality;

namespace BL23.Game.Characters
{
    /// <summary>
    /// Fully code-driven procedural animation on the common skeleton: locomotion (walk/run/limp),
    /// postures, additive upper-body gestures, keyframed actions, dead poses, look-at, injuries,
    /// carrying and the BREAK effect. Poses are authored in canonical space (see <see cref="ActorPose"/>)
    /// and retargeted to each actor's rest skeleton.
    /// charpolish M0: split into partial files by concern —
    ///   ActorAnimator.cs            core: state, public API, init, the layer stack (Evaluate), pose application
    ///   ActorAnimator.Locomotion.cs gait (captured walk/jog/sprint + procedural), stairs, seats
    ///   ActorAnimator.Upper.cs      gestures, actions, first-person calm, look-at
    ///   ActorAnimator.Combat.cs     attacks, weapon stance, stagger, victim reactions
    ///   ActorAnimator.IK.cs         world-space post passes (hit offsets, hand/finger IK)
    /// </summary>
    [DefaultExecutionOrder(50)]
    public partial class ActorAnimator : MonoBehaviour
    {
        // ---------------------------------------------------------------- setup data
        ActorRig _rig;
        PhysicalActionController _physicalActions;   // the owner's contact layer (main Game/Physicality): PreparePose before, ApplyIK after the pose
        /// <summary>Ragdoll owns bone transforms while true. Animation state can still be updated by the simulation.</summary>
        public bool PhysicsDriven;
        bool _init;
        Transform[] _b;
        Quaternion[] _O, _OInv, _restG;          // canonical->rest offsets, rest global rotations (root space)
        Vector3[] _restLocalPos;
        Vector3[] _restPos;                        // rest joint positions (root space)
        Quaternion _armatureInv = Quaternion.identity;
        public float LegLen { get; private set; }
        public float UpperLegLen { get; private set; }
        public float LowerLegLen { get; private set; }
        public float AnkleH { get; private set; }
        public Vector3 RestHips { get; private set; }
        public float ArmIdleOut = 9f;              // abduction (deg) that clears the body/clothes in idle
        public bool AutoFidget = true;             // occasional idle gestures (look around, nod, stretch) when nothing else plays
        public float Height => _rig != null ? _rig.Height : 1.7f;

        // ---------------------------------------------------------------- state
        Vector3 _vel; bool _running; float _speed; Vector3 _moveDirL = Vector3.forward;
        float _phase; float _walkW, _runW;
        Posture _posture = Posture.Stand; float _postureT = 1f;
        readonly ActorPose _from = new ActorPose();
        readonly ActorPose _last = new ActorPose();
        int _dead = -1; float _deadT;
        Gesture _gesture; float _gT, _gDur; bool _gActive;
        ActionAnim _action; float _aT, _aDur; bool _aActive;
        Vector3? _look; Vector2 _lookYP;
        float _mobility = 1f; bool _armL, _armR, _limpL, _limpR, _conscious = true;
        bool _autoArmL, _autoArmR;
        ActorRig _carrying; ActorRig _carrier; bool _beingCarried;
        float _break; float _freezeT; float _nextFreeze; readonly ActorPose _frozen = new ActorPose();
        float _time; float _seed;
        float _idleClock, _nextFidget = 10f;
        Posture? _afterAction;

        // scratch
        readonly ActorPose _base = new ActorPose(), _tmp = new ActorPose(), _tmp2 = new ActorPose(), _final = new ActorPose();
        readonly Quaternion[] _G = new Quaternion[ActorSkeleton.Count];
        readonly Quaternion[] _W = new Quaternion[ActorSkeleton.Count];
        readonly Quaternion[] _Gread = new Quaternion[ActorSkeleton.Count];
        static readonly bool[] UpperMask = MakeMask(true);
        static readonly bool[] ArmsHeadMask = MakeArmsHead();

        public Posture CurrentPosture => _posture;
        public bool IsDead => _dead >= 0;
        public bool IsCarrying => _carrying != null;
        public float BreakAmount => _break;
        public ActionAnim CurrentAction => _aActive ? _action : ActionAnim.None;
        public Gesture CurrentGesture => _gActive ? _gesture : Gesture.None;
        public bool CanApplyPhysicalActions => !PhysicsDriven && _dead < 0 && _conscious && !_beingCarried && _pairedRef == null && !_dragged && !WristsBound;
        /// <summary>An arm the physical action layer may use: not injured, not holding a hand target, a body or a raised firearm.</summary>
        public bool ArmAvailable(bool left) => (left ? !(_armL || _autoArmL) : !(_armR || _autoArmR)) && !HandTargetBusy(left) && _dragBody == null &&
            !(_readyW > 0.5f && Firearm(_readyClass) && (!left || _readyClass != WeaponClass.Pistol));

        static bool[] MakeMask(bool upper)
        {
            var m = new bool[ActorSkeleton.Count];
            for (int i = 0; i < m.Length; i++)
            {
                var b = (HBone)i;
                bool up = b == HBone.Spine || b == HBone.Chest || b == HBone.Neck || b == HBone.Head || b == HBone.ShoulderL || b == HBone.ShoulderR ||
                          b == HBone.UpperArmL || b == HBone.LowerArmL || b == HBone.HandL || b == HBone.UpperArmR || b == HBone.LowerArmR || b == HBone.HandR ||
                          b == HBone.UpperChest || ActorSkeleton.IsFinger(b);
                m[i] = upper ? up : !up;
            }
            return m;
        }

        static bool[] MakeArmsHead()
        {
            var m = MakeMask(true);
            m[(int)HBone.Spine] = false;
            return m;
        }

        // ---------------------------------------------------------------- public API (contract)
        public void SetMove(Vector3 worldVelocity, bool running) { _vel = worldVelocity; _running = running; }

        public void SetPosture(Posture p)
        {
            Init();
            if (p == _posture && _postureT >= 1f) return;
            var prev = _posture;
            _from.CopyFrom(_last);
            _posture = p; _postureT = 0f;
            _trans = null;
            _getUp.Active = false;
            bool lying = prev == Posture.LieBack || prev == Posture.LieFront || prev == Posture.LieSide || prev == Posture.Slumped;
            if (_dead < 0 && !_beingCarried && _conscious && _pairedRef == null && !_dragged)
            {
                if (_set != null && prev == Posture.Stand && p == Posture.Sit) StartTransition(Clip("Sitting_Enter"), 1.1f);
                else if (_set != null && prev == Posture.Sit && p == Posture.Stand) StartTransition(Clip("Sitting_Exit"), 1.05f);
                else if (lying && p == Posture.Stand)
                {
                    // getting up from where the body really lies (after a ragdoll it may be face down): the captured clip rises from
                    // the back, the procedural sequence covers face-down and half-sitting starts
                    int kind = LyingKind();
                    if (kind == 0 && Clip("LayToIdle") != null) StartTransition(Clip("LayToIdle"), 0.9f);
                    else PlayGetUp(0f);
                }
            }
            OnPostureChanged(prev, p);
        }

        public void SetDeadPose(int variant)
        {
            Init();
            if (variant < 0)
            {
                if (_dead >= 0) { _from.CopyFrom(_last); _postureT = 0f; }
                _dead = -1; _deathClip = null; _hasDeadOverride = false; return;
            }
            if (_dead < 0)
            {
                _from.CopyFrom(_last); _deadT = 0f;
                // dying on one's feet: a real collapse (captured fall) before settling into the chosen dead pose
                bool standing = _posture == Posture.Stand && _conscious && !_beingCarried;
                int v = Mathf.Clamp(variant, 0, 5);
                _deathClip = standing && (v == 0 || v == 4) && !_hasDeadOverride ? Clip("Death01") : null;
                _deathT = 0f;
            }
            _dead = Mathf.Clamp(variant, 0, 5);
            _gActive = false; _aActive = false; _trans = null;
            OnDeathStarted();
        }

        public void SetLookAt(Vector3? worldPoint) { _look = worldPoint; }

        public void SetInjury(float mobility, bool leftArm, bool rightArm, bool limpL, bool limpR, bool conscious)
        {
            Init();
            _mobility = Mathf.Clamp01(mobility); _armL = leftArm; _armR = rightArm; _limpL = limpL; _limpR = limpR;
            if (_conscious != conscious)
            {
                _conscious = conscious;
                _from.CopyFrom(_last); _postureT = 0f;
            }
        }

        /// <summary>Called by ActorWounds for severe wounds (arm hangs automatically).</summary>
        public void AddAutoInjury(bool leftArm, bool rightArm) { _autoArmL |= leftArm; _autoArmR |= rightArm; }
        public void ClearAutoInjury() { _autoArmL = _autoArmR = false; }

        // ---- charpolish step 0 contract (owner: implementer 3, motion)
        /// <summary>While true, LateUpdate does not write the pose (the ragdoll / physics layer owns the bones).</summary>
        public bool Suspended { get => PhysicsDriven; set => PhysicsDriven = value; }

        readonly ActorPose _inertia = new ActorPose();
        float _inertT = 1f, _inertDur = 0f;
        /// <summary>Start an inertial blend from whatever the bones show now (after a ragdoll, a cut, a teleport) into
        /// the animated pose over 'seconds'.</summary>
        public void BlendFromCurrentBones(float seconds)
        {
            Init();
            if (!_init) return;
            ReadCanonicalInto(_inertia);
            _inertia.LegIK = 0f;
            _inertDur = Mathf.Max(0.01f, seconds); _inertT = 0f;
            _last.CopyFrom(_inertia); _from.CopyFrom(_inertia);
        }

        readonly ActorPose _deadOverride = new ActorPose();
        bool _hasDeadOverride;
        /// <summary>Dead pose override (a settled ragdoll snapshot, canonical space); null = back to ActorPoses.Dead(variant).
        /// The override wins over the variant while the actor is dead (SetDeadPose(v) keeps it; SetDeadPose(-1) clears it).</summary>
        public void SetDeadOverride(ActorPose pose)
        {
            Init();
            if (pose == null) { _hasDeadOverride = false; return; }
            _deadOverride.CopyFrom(pose);
            _hasDeadOverride = true;
            _deathClip = null;   // no captured collapse over a settled body
        }
        /// <summary>True while a dead-pose override (ragdoll snapshot) is in use.</summary>
        public bool HasDeadOverride => _hasDeadOverride;

        /// <summary>True while this actor is carried by someone (the carried body dangles).</summary>
        public bool IsBeingCarried => _beingCarried;

        /// <summary>True while the hands visibly do something (an action, carrying, weapon ready, an attack): first person
        /// keeps the arms drawn.</summary>
        public bool HandsBusy => _aActive || _carrying != null || _combatReady || _atk.Active;

        int _lod;
        /// <summary>Detail tier: 0 = A (full), 1 = B (on screen, far), 2 = C (hidden/culled: clocks only).</summary>
        public void SetLod(int tier) { _lod = Mathf.Clamp(tier, 0, 2); }
        public int Lod => _lod;

        public void SetCarrying(ActorRig carried)
        {
            Init();
            if (_carrying == carried) return;
            if (_carrying != null && _carrying.Anim != null) _carrying.Anim.ReleaseFromCarrier(this);
            _carrying = carried;
            if (carried != null)
            {
                carried.Init();
                carried.Anim.AttachToCarrier(this);
            }
        }

        public void SetBreak(float t)
        {
            Init();
            if (_rig != null && !_rig.CanBreak) t = 0f;
            _break = Mathf.Clamp01(t);
            if (_rig != null) _rig.SetBreakVisual(_break);
        }

        // ---------------------------------------------------------------- carry plumbing
        Transform _carryParentBefore;
        internal void AttachToCarrier(ActorAnimator carrier)
        {
            _carrier = carrier != null ? carrier._rig : null;
            _beingCarried = carrier != null;
            if (!_beingCarried) return;
            _carryParentBefore = transform.parent;
            var chest = carrier._b[(int)HBone.Chest];
            transform.SetParent(chest, false);
            transform.localRotation = Quaternion.identity;
            transform.localPosition = Vector3.zero;
            // carried hips rest on top of the carrier's right shoulder (chest-local rest offset)
            _carryHips = carrier._restPos[(int)HBone.UpperArmR] - carrier._restPos[(int)HBone.Chest] + new Vector3(-0.03f, 0.13f, -0.05f) * (carrier.Height / 1.75f);
            _placeOnCarrier = true;
        }

        bool _placeOnCarrier;
        Vector3 _carryHips;

        internal void ReleaseFromCarrier(ActorAnimator carrier)
        {
            _beingCarried = false; _carrier = null;
            if (_carryParentBefore != null)
            {
                // back under the owning view (ActorView / holder), which places the body where the simulation dropped it
                transform.SetParent(_carryParentBefore, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            else
            {
                Vector3 p = transform.position;
                transform.SetParent(null, true);
                transform.position = new Vector3(p.x, carrier.transform.position.y, p.z) + carrier.transform.right * 0.45f;
                transform.rotation = Quaternion.Euler(0, carrier.transform.eulerAngles.y + 90f, 0);
            }
            _from.CopyFrom(_last); _postureT = 0f;
            ResetFeet();
        }

        // ---------------------------------------------------------------- init
        void Awake() { Init(); }

        bool[] _has;                               // bone transform present (older rigs lack the extended bones)
        int[] _par;                                // effective parent (skips missing bones)
        readonly Vector3[] _fingerAxis = new Vector3[10]; // canonical-space flexion axis per digit: thumb..little L, then R
        readonly Quaternion[] _spreadFix = new Quaternion[10];
        public bool HasFingers { get; private set; }
        public bool HasUpperChest { get; private set; }

        public void Init()
        {
            if (_init) return;
            _rig = GetComponent<ActorRig>();
            if (_rig == null || _rig.Bones == null || _rig.Bones.Length < ActorSkeleton.CoreCount || _rig.Bones[0] == null) return;
            _rig.Init();
            _init = true;
            _rig.Anim = this;
            _b = _rig.Bones;
            int n = ActorSkeleton.Count;
            _has = new bool[n]; _par = new int[n];
            for (int i = 0; i < n; i++) _has[i] = i < _b.Length && _b[i] != null;
            for (int i = 0; i < n; i++)
            {
                int p = ActorSkeleton.Parent[i];
                if (p >= 0 && !_has[p]) p = ActorSkeleton.CoreParent(i);
                _par[i] = p;
            }
            HasUpperChest = _has[(int)HBone.UpperChest];
            HasFingers = _has[(int)HBone.IndexL1] && _has[(int)HBone.IndexR1];
            _O = new Quaternion[n]; _OInv = new Quaternion[n]; _restG = new Quaternion[n];
            _restLocalPos = new Vector3[n]; _restPos = new Vector3[n];
            var rootInvRot = Quaternion.Inverse(transform.rotation);
            for (int i = 0; i < n; i++)
            {
                if (!_has[i]) { _restG[i] = Quaternion.identity; continue; }
                _restLocalPos[i] = _b[i].localPosition;
                _restPos[i] = transform.InverseTransformPoint(_b[i].position);
                _restG[i] = rootInvRot * _b[i].rotation;
            }
            var hipsParent = _b[0].parent;
            _armatureInv = Quaternion.Inverse(rootInvRot * hipsParent.rotation);
            for (int i = 0; i < n; i++)
            {
                var hb = (HBone)i;
                if (!_has[i] || ActorSkeleton.IsFinger(hb)) { _O[i] = Quaternion.identity; _OInv[i] = Quaternion.identity; continue; }
                Vector3 cdir = ActorSkeleton.CanonicalDir(hb);
                Vector3 rdir = RestDir(hb);
                bool useDir = hb != HBone.Hips && hb != HBone.FootL && hb != HBone.FootR && hb != HBone.ToeL && hb != HBone.ToeR;
                _O[i] = useDir && rdir.sqrMagnitude > 1e-8f ? Quaternion.FromToRotation(cdir, rdir.normalized) : Quaternion.identity;
                _OInv[i] = Quaternion.Inverse(_O[i]);
            }
            if (_rig.KeepArmRestBend)
            {
                // rigs baked in a relaxed rest pose (soft elbow, carrying angle, turned palms): forearm and hand keep their
                // rest orientation relative to the upper arm, so the canonical "arms down" pose is that relaxed pose
                for (int s = 0; s < 2; s++)
                {
                    int ua = s == 0 ? (int)HBone.UpperArmL : (int)HBone.UpperArmR;
                    _O[ua + 1] = _O[ua]; _O[ua + 2] = _O[ua];
                    _OInv[ua + 1] = _OInv[ua]; _OInv[ua + 2] = _OInv[ua];
                }
            }
            InitFingers();
            RestHips = _restPos[0];
            UpperLegLen = (_restPos[(int)HBone.LowerLegL] - _restPos[(int)HBone.UpperLegL]).magnitude;
            LowerLegLen = (_restPos[(int)HBone.FootL] - _restPos[(int)HBone.LowerLegL]).magnitude;
            AnkleH = _restPos[(int)HBone.FootL].y;
            LegLen = UpperLegLen + LowerLegLen;
            _seed = Mathf.Abs((_rig.ActorId ?? "x").GetHashCode() % 1000) * 0.137f;
            _time = _seed;
            _nextFreeze = 1f;
            InitPersona();
            BindClips();
            _last.Reset(); _last.LegIK = 1f; _last.FootL = _restPos[(int)HBone.FootL]; _last.FootR = _restPos[(int)HBone.FootR];
            _from.CopyFrom(_last);
            InitIK();
        }

        void InitFingers()
        {
            // finger bones keep their rest orientation relative to the hand; one flexion axis per digit, measured on the rest hand
            for (int s = 0; s < 2; s++)
            {
                bool left = s == 0;
                int hand = left ? (int)HBone.HandL : (int)HBone.HandR;
                int b0 = left ? (int)HBone.ThumbL1 : (int)HBone.ThumbR1;
                for (int k = 0; k < ActorSkeleton.FingersPerHand; k++) { _O[b0 + k] = _O[hand]; _OInv[b0 + k] = _OInv[hand]; }
                Vector3 w = _restPos[hand];
                Vector3 tip = left ? _rig.HandTipRestL : _rig.HandTipRestR;
                var tips = _rig.FingerTipRest != null && _rig.FingerTipRest.Length == 10 ? _rig.FingerTipRest : null;
                Vector3 handDir = (tip - w).sqrMagnitude > 1e-8f ? (tip - w).normalized : Vector3.down;
                Vector3 med = Vector3.ProjectOnPlane(new Vector3(left ? 1f : -1f, 0f, 0f), handDir).normalized;   // palm faces the body
                for (int d = 0; d < 5; d++)
                {
                    int b = b0 + d * 3;
                    Vector3 dd = handDir;
                    if (_has[b] && tips != null) { var v = tips[s * 5 + d] - _restPos[b]; if (v.sqrMagnitude > 1e-8f) dd = v.normalized; }
                    Vector3 axis = Vector3.Cross(dd, Vector3.ProjectOnPlane(med, dd).normalized);
                    if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(handDir, med);
                    _fingerAxis[s * 5 + d] = _OInv[hand] * axis.normalized;
                    // generated hands often rest with the fingers splayed (T-pose scans): pull each finger toward the hand axis
                    // around the palm normal (charpolish M6: 90% with a 60 deg limit), so relaxed / curled hands never read as
                    // claws; a small natural fan stays (index a little toward the thumb, little finger a little away)
                    _spreadFix[s * 5 + d] = Quaternion.identity;
                    if (d > 0 && _has[b] && tips != null)
                    {
                        Vector3 pn = Vector3.ProjectOnPlane(med, dd).normalized;
                        float spread = Vector3.SignedAngle(Vector3.ProjectOnPlane(handDir, pn), Vector3.ProjectOnPlane(dd, pn), pn);
                        // +angle about pn turns the finger backward on the left hand and forward (thumb side) on the right hand
                        float fanFwd = (2.2f - d) * 2.5f;
                        float keep = left ? -fanFwd : fanFwd;
                        if (Mathf.Abs(spread) < 60f) _spreadFix[s * 5 + d] = Quaternion.AngleAxis(-(spread - keep) * 0.9f, _OInv[hand] * pn);
                    }
                }
            }
        }

        Vector3 RestDir(HBone b)
        {
            Vector3 p = _restPos[(int)b];
            Vector3 P(HBone x) => _restPos[(int)x];
            switch (b)
            {
                case HBone.Hips: return P(HBone.Spine) - p;
                case HBone.Spine: return P(HBone.Chest) - p;
                case HBone.Chest: return (HasUpperChest ? P(HBone.UpperChest) : P(HBone.Neck)) - p;
                case HBone.UpperChest: return P(HBone.Neck) - p;
                case HBone.Neck: return P(HBone.Head) - p;
                case HBone.Head: return _rig.HeadTopAnchor != null ? transform.InverseTransformPoint(_rig.HeadTopAnchor.position) - p : Vector3.up;
                case HBone.ShoulderL: return P(HBone.UpperArmL) - p;
                case HBone.ShoulderR: return P(HBone.UpperArmR) - p;
                case HBone.UpperArmL: return P(HBone.LowerArmL) - p;
                case HBone.UpperArmR: return P(HBone.LowerArmR) - p;
                case HBone.LowerArmL: return P(HBone.HandL) - p;
                case HBone.LowerArmR: return P(HBone.HandR) - p;
                case HBone.HandL: return _rig.HandTipRestL - p;
                case HBone.HandR: return _rig.HandTipRestR - p;
                case HBone.UpperLegL: return P(HBone.LowerLegL) - p;
                case HBone.UpperLegR: return P(HBone.LowerLegR) - p;
                case HBone.LowerLegL: return P(HBone.FootL) - p;
                case HBone.LowerLegR: return P(HBone.FootR) - p;
            }
            return Vector3.zero;
        }

        public Vector3 RestPos(HBone b) { Init(); return _has != null && _has[(int)b] ? _restPos[(int)b] : _restPos[(int)ActorSkeleton.Core(b)]; }
        /// <summary>Rest (bind) rotation of a bone in world space for the current root transform.</summary>
        public Quaternion RestRot(HBone b) { Init(); int i = _has != null && _has[(int)b] ? (int)b : (int)ActorSkeleton.Core(b); return transform.rotation * _restG[i]; }
        /// <summary>World rotation of a bone's canonical frame now (x right, y along the bone / up the spine, z forward).</summary>
        public Quaternion CanonicalRotation(HBone b) { Init(); int i = _has != null && _has[(int)b] ? (int)b : (int)ActorSkeleton.Core(b); return _b[i].rotation * Quaternion.Inverse(_restG[i]) * _O[i]; }

        // ---------------------------------------------------------------- update
        void LateUpdate()
        {
            if (!_init) { Init(); if (!_init) return; }
            if (PhysicsDriven) { _inertT = _inertDur; return; }
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            Tick(dt);
        }

        /// <summary>Advances the animation by dt and applies the pose (also used by editor QA tools).</summary>
        public void Tick(float dt)
        {
            if (!_init) { Init(); if (!_init) return; }
            _time += dt;
            if (PhysicsDriven) { OnPhysicsDriven(); return; }
            // LOD C (hidden / culled): clocks only, no pose work (a promotion evaluates once and blends in)
            if (_lod >= 2) { TickClocksOnly(dt); _wasCulled = true; return; }
            if (_wasCulled) { _wasCulled = false; ResetFeet(); }
            // BREAK micro-freezes
            if (_break > 0.05f)
            {
                if (_freezeT > 0f) { _freezeT -= dt; ApplyPose(_frozen); return; }
                if (_time > _nextFreeze)
                {
                    _nextFreeze = _time + Random.Range(0.25f, 1.6f) / (0.4f + _break);
                    _freezeT = Random.Range(0.05f, 0.22f) * _break;
                    _frozen.CopyFrom(_last);
                }
            }
            Evaluate(dt, _final);
            // inertial blend from a snapshot (ragdoll end, cut, teleport)
            if (_inertT < _inertDur)
            {
                _inertT += dt;
                float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_inertT / _inertDur));
                _tmp.CopyFrom(_final);
                _final.Blend(_inertia, _tmp, w);
            }
            ApplyHitOffsets(_final);
            if (_physicalActions == null) _physicalActions = GetComponent<PhysicalActionController>();
            if (_physicalActions != null)
            {
                _physicalActions.PreparePose(dt, _final);
                RefineControllerPose(dt, new ActorPoses.Dims(this, _restPos, _rig));
            }
            _last.CopyFrom(_final);
            ApplyPose(_final);
            PostIK(dt);
            if (_physicalActions != null) { _physicalActions.ApplyIK(); KeepStrikeArm(); }
            EnsureCompanions();
        }

        bool _wasCulled;
        void TickClocksOnly(float dt)
        {
            if (_aActive) { _aT += dt; if (_aT >= _aDur) { _aActive = false; _actClip = null; if (_afterAction.HasValue) { _posture = _afterAction.Value; _postureT = 1f; _afterAction = null; } } }
            if (_gActive) { _gT += dt; if (_gT >= _gDur) _gActive = false; }
            if (_postureT < 1f) _postureT = 1f;
            if (_trans != null) _trans = null;
            if (_dead >= 0) { _deadT += dt; _deathClip = null; }
            TickCombatClocks(dt);
        }

        void Evaluate(float dt, ActorPose outPose)
        {
            var dims = new ActorPoses.Dims(this, _restPos, _rig);
            // --- locomotion input (root space)
            UpdateLocomotionInput(dt);

            // --- base layer
            _base.Reset();
            if (_beingCarried)
            {
                ActorPoses.Carried(_base, dims, _time, _carryHips);
            }
            else if (_dragged)
            {
                EvaluateDragged(dt, dims);
            }
            else if (_dead >= 0)
            {
                _deadT += dt;
                if (_hasDeadOverride) _base.CopyFrom(_deadOverride); else ActorPoses.Dead(_base, dims, _dead);
                if (_deathClip != null)
                {
                    // captured collapse, then settle into the dead pose over its last third
                    float L = _deathClip.Length;
                    _deathT += dt;
                    _tmp.CopyFrom(_base);
                    ActorClipSet.Sample(_deathClip, Mathf.Min(_deathT, L), _tmp, LegLen);
                    float wIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_deathT / 0.15f));
                    float wOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_deathT - L * 0.7f) / (L * 0.4f)));
                    _tmp2.Blend(_from, _tmp, wIn);
                    _base.Blend(_tmp2, _base, wOut);
                    if (_deathT > L * 1.1f) _deathClip = null;
                }
                else
                {
                    float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_deadT / 0.55f));
                    if (w < 1f) { _tmp.CopyFrom(_base); _base.Blend(_from, _tmp, w); }
                }
            }
            else
            {
                Posture p = _conscious ? _posture : (_posture == Posture.Sit ? Posture.Slumped : (_posture == Posture.Stand || _posture == Posture.Crouch || _posture == Posture.Kneel ? Posture.LieSide : _posture));
                if (p == Posture.Stand) EvaluateStanding(dt, dims);
                else EvaluatePosture(dt, dims, p);
                if (!_conscious) ActorPoses.Unconscious(_base, dims, _time);
                EvaluateTransitions(dt);
            }

            // --- action layer
            EvaluateActionLayer(dt, dims);

            // --- combat: attacks, weapon stance, stagger, victim defences
            EvaluateCombat(dt, dims);

            // --- reactions: hits, startle, falls, get-up, pain
            EvaluateReactions(dt, dims);

            // --- carrying / dragging someone
            if (_carrying != null && _dead < 0) ActorPoses.CarryOverlay(_base, dims, _time);
            EvaluateDragger(dt, dims);

            // --- ambient life: weight shifts, passing people, bumps, cold, comforting; leaning on a hand
            EvaluateAmbient(dt, dims);
            EvaluateHandTargetBodies(dt);

            // --- idle fidgets + gesture layer (upper body)
            EvaluateFidgets(dt);
            EvaluateGestureLayer(dt, dims);

            // --- restraints (bound wrists / ankles override the arms and legs)
            EvaluateRestraint(dt, dims);

            // --- injuries
            if (_dead < 0 && _conscious && !_beingCarried)
            {
                if (_armL || _autoArmL) ActorPoses.LimpArm(_base, true, _time);
                if (_armR || _autoArmR) ActorPoses.LimpArm(_base, false, _time);
                if (_mobility < 0.6f && _posture == Posture.Stand) ActorPoses.Hunch(_base, 1f - _mobility / 0.6f);
            }

            // --- paired acts (strangling, smothering, drowning) override everything for both bodies
            EvaluatePaired(dt, dims);

            // --- first person: the player's own body seen from its eyes (ActorRig.HeadHidden). Arms stay low and close to
            // the body, no big swing or forward reach, a calmer torso, so nothing sweeps across the lower view.
            if (FirstPersonCalm && _dead < 0 && !_beingCarried) ApplyFirstPersonCalm(dt);

            // --- look-at chain (eyes, head, chest)
            EvaluateLook(dt);

            // --- BREAK: uncanny tilt + twitches
            if (_break > 0.001f && _dead < 0)
            {
                float t = _time;
                float tilt = 32f * _break * (0.8f + 0.2f * Mathf.Sin(t * 0.7f));
                float twitch = (Mathf.PerlinNoise(t * 9f, _seed) - 0.5f) * 18f * _break;
                _base.R[(int)HBone.Head] = _base.R[(int)HBone.Head] * ActorPose.Body(-6f * _break + twitch * 0.3f, twitch * 0.5f, -tilt);
                _base.R[(int)HBone.Neck] = _base.R[(int)HBone.Neck] * ActorPose.Body(4f * _break, 0f, -tilt * 0.25f);
                _base.R[(int)HBone.ShoulderL] = _base.R[(int)HBone.ShoulderL] * Quaternion.AngleAxis(-8f * _break * Mathf.PerlinNoise(t * 3f, 1f), Vector3.forward);
                float armT = (Mathf.PerlinNoise(t * 13f, 7f) - 0.5f) * 25f * _break;
                _base.R[(int)HBone.LowerArmR] = _base.R[(int)HBone.LowerArmR] * Quaternion.AngleAxis(armT, Vector3.right);
                _base.R[(int)HBone.HandL] = _base.R[(int)HBone.HandL] * Quaternion.AngleAxis(armT * 1.5f, Vector3.forward);
            }

            // hands that hold something grip it
            if (_dead < 0 && !_beingCarried)
            {
                if (Holding(_rig.HandAnchorR)) _base.Grip(false);
                if (Holding(_rig.HandAnchorL)) _base.Grip(true);
            }

            outPose.CopyFrom(_base);
        }

        // ---------------------------------------------------------------- solve & apply
        /// <summary>Poses the skeleton from a canonical pose (editor tools: T-pose construction, clip previews).</summary>
        public void ApplyCanonical(ActorPose p) { Init(); ApplyPose(p); }

        /// <summary>Inverse of ApplyPose: converts the current bone transforms into a canonical pose (clip retargeting).</summary>
        public void ReadCanonical(ActorPose p) { Init(); ReadCanonicalInto(p); }

        void ReadCanonicalInto(ActorPose p)
        {
            var rootInvRot = Quaternion.Inverse(transform.rotation);
            var G = _Gread;
            foreach (int i in ActorSkeleton.Order)
            {
                if (!_has[i]) { G[i] = _par[i] >= 0 ? G[_par[i]] : Quaternion.identity; continue; }
                G[i] = rootInvRot * _b[i].rotation * Quaternion.Inverse(_restG[i]) * _O[i];
            }
            p.Reset();
            foreach (int i in ActorSkeleton.Order)
            {
                if (!_has[i] || ActorSkeleton.IsFinger((HBone)i)) continue;
                p.R[i] = i == 0 ? G[0] : Quaternion.Inverse(G[_par[i]]) * G[i];
            }
            if (HasUpperChest)
            {
                Quaternion h = p.R[(int)HBone.Chest], u = p.R[(int)HBone.UpperChest];
                p.R[(int)HBone.Chest] = h * h;
                p.R[(int)HBone.UpperChest] = Quaternion.Inverse(h) * u;
            }
            p.HipsOffset = transform.InverseTransformPoint(_b[0].position) - _restPos[0];
            p.LegIK = 0f;
            p.FootL = transform.InverseTransformPoint(_b[(int)HBone.FootL].position);
            p.FootR = transform.InverseTransformPoint(_b[(int)HBone.FootR].position);
        }

        void ApplyPose(ActorPose p)
        {
            int n = ActorSkeleton.Count;
            // chest bend shared with the upper chest (smoother torso curve) when the rig has one
            Quaternion rChest = p.R[(int)HBone.Chest], rUpper = p.R[(int)HBone.UpperChest];
            if (HasUpperChest)
            {
                rChest = Quaternion.Slerp(Quaternion.identity, p.R[(int)HBone.Chest], 0.5f);
                rUpper = rChest * p.R[(int)HBone.UpperChest];
            }
            // fingers from the scalar hand channels (thumb / index / middle-ring-little), every segment curls
            if (HasFingers) PoseFingers(p);
            // canonical globals (parents first)
            foreach (int i in ActorSkeleton.Order)
            {
                if (!_has[i]) continue;
                Quaternion r = i == (int)HBone.Chest ? rChest : i == (int)HBone.UpperChest ? rUpper : ActorSkeleton.IsFinger((HBone)i) ? (HasFingers ? _FR[i] : Quaternion.identity) : p.R[i];
                _G[i] = i == 0 ? p.RootRot * r : _G[_par[i]] * r;
            }
            Vector3 hipsPos = p.RootOffset + p.RootRot * (_restPos[0] + p.HipsOffset);

            // leg IK
            if (p.LegIK > 0.001f)
            {
                SolveLeg(true, p, hipsPos);
                SolveLeg(false, p, hipsPos);
            }

            for (int i = 0; i < n; i++) if (_has[i]) _W[i] = _G[i] * _OInv[i] * _restG[i];
            // write transforms
            _b[0].localPosition = _armatureInv * hipsPos;
            _b[0].localRotation = _armatureInv * _W[0];
            for (int i = 1; i < n; i++)
            {
                if (!_has[i]) continue;
                _b[i].localRotation = Quaternion.Inverse(_W[_par[i]]) * _W[i];
            }
        }
        readonly Quaternion[] _FR = new Quaternion[ActorSkeleton.Count];

        void SolveLeg(bool left, ActorPose p, Vector3 hipsPos)
        {
            int ul = left ? (int)HBone.UpperLegL : (int)HBone.UpperLegR;
            // hip joint position from the hips transform
            Quaternion wHips = _G[0] * _OInv[0] * _restG[0];
            Vector3 hip = hipsPos + wHips * _restLocalPos[ul];
            Vector3 target = left ? p.FootL : p.FootR;
            float a = (_restPos[ul + 1] - _restPos[ul]).magnitude;
            float b = (_restPos[ul + 2] - _restPos[ul + 1]).magnitude;
            Vector3 d = target - hip;
            float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(a - b) + 0.01f, a + b - 0.0015f);
            Vector3 dn = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.down;
            // knee pole: forward of the hips yaw, slightly outward
            Quaternion footRot = left ? p.FootRotL : p.FootRotR;
            Vector3 fwd = footRot * Vector3.forward;
            Vector3 pole = (fwd + (left ? Vector3.left : Vector3.right) * 0.12f).normalized;
            Vector3 poleP = Vector3.ProjectOnPlane(pole, dn);
            if (poleP.sqrMagnitude < 1e-6f) poleP = Vector3.ProjectOnPlane(Vector3.forward, dn);
            poleP.Normalize();
            float cosA = Mathf.Clamp((a * a + dist * dist - b * b) / (2f * a * dist), -1f, 1f);
            float angA = Mathf.Acos(cosA);
            Vector3 knee = hip + (dn * Mathf.Cos(angA) + poleP * Mathf.Sin(angA)) * a;
            Vector3 foot = hip + dn * dist;
            Vector3 du = (knee - hip).normalized, dl = (foot - knee).normalized;
            Quaternion gU = Quaternion.LookRotation(Vector3.ProjectOnPlane(poleP, du).normalized, -du);
            Vector3 lowerFwd = Vector3.ProjectOnPlane(fwd, dl);
            if (lowerFwd.sqrMagnitude < 1e-6f) lowerFwd = Vector3.ProjectOnPlane(poleP, dl);
            Quaternion gL = Quaternion.LookRotation(lowerFwd.normalized, -dl);
            float w = p.LegIK;
            _G[ul] = Quaternion.Slerp(_G[ul], gU, w);
            _G[ul + 1] = Quaternion.Slerp(_G[ul + 1], gL, w);
            // foot keeps its canonical global orientation, toe follows
            Quaternion gFoot = Quaternion.Slerp(_G[ul + 2], p.RootRot * footRot, w);
            Quaternion toeLocal = p.R[ul + 3];
            _G[ul + 2] = gFoot;
            _G[ul + 3] = gFoot * toeLocal;
        }

        /// <summary>Current canonical-space rest joint position helper for pose authoring.</summary>
        internal Vector3 RestJoint(int i) => _restPos[i];
    }
}
