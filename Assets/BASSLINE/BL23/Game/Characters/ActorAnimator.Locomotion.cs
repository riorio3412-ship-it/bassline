using UnityEngine;
using static BL23.Game.Characters.MotionKit;

namespace BL23.Game.Characters
{
    /// <summary>Locomotion: captured walk / jog / sprint (Quaternius UAL, retargeted) plus the procedural cycle, postures,
    /// posture transitions, stairs and seats.</summary>
    public partial class ActorAnimator
    {
        // ---------------------------------------------------------------- captured clips (Resources/Actors/Anim)
        // Retargeted motion capture (Quaternius Universal Animation Library, CC0) drives locomotion, posture transitions,
        // hit reactions and most hand actions; the procedural layers (idle styles, look-at, IK, fingers, injuries,
        // BREAK) stay on top.
        public bool UseClips = true;
        ActorClipSet _set;
        ActorClipSet.Clip _cWalk, _cJog, _cSprint, _cSit, _cSitTalk;
        float _clipPhase, _idleRate = 1f;
        readonly ActorPose _tmp3 = new ActorPose(), _tmp4 = new ActorPose();
        static readonly bool[] ArmsOnlyMask = MakeArmsOnly();
        public bool HasClips => _set != null;
        public string DebugState => $"posture {_posture} postureT {_postureT:F2} trans {(_trans != null ? _trans.Name + " " + _transT.ToString("F2") : "-")} walkW {_walkW:F2} speed {_speed:F2} set {(_set != null ? _set.Clips.Count.ToString() : "null")} walk {(_cWalk != null ? _cWalk.Name + " cyc " + _cWalk.CycleDist.ToString("F2") : "-")} action {(_aActive ? _action + " " + _aT.ToString("F2") + "/" + _aDur.ToString("F2") + (_actClip != null ? " clip " + _actClip.Name : "") : "-")} hips {(_b != null ? transform.InverseTransformPoint(_b[0].position).ToString("F3") : "")}";
        ActorClipSet.Clip _trans; float _transT, _transRate = 1f;   // posture transition clip (sit down, stand up, get up...)
        ActorClipSet.Clip _actClip; float _actRate = 1f, _actEnd = -1f; bool _actLoop, _actFull, _actPing;
        ActorClipSet.Clip _deathClip; float _deathT;

        static bool[] MakeArmsOnly()
        {
            var m = new bool[ActorSkeleton.Count];
            for (int i = 0; i < m.Length; i++)
            {
                var b = (HBone)i;
                m[i] = b == HBone.ShoulderL || b == HBone.UpperArmL || b == HBone.LowerArmL || b == HBone.HandL ||
                       b == HBone.ShoulderR || b == HBone.UpperArmR || b == HBone.LowerArmR || b == HBone.HandR || ActorSkeleton.IsFinger(b);
            }
            return m;
        }

        /// <summary>Idle styles whose arms stay in place while walking (hands behind the back, in pockets, holding a prop...).</summary>
        static bool StyleKeepsArms(string style)
        {
            switch (style ?? "")
            {
                case "pockets": case "behind": case "crossed": case "clasp": case "notebook": case "clipboard": case "lily": case "thermos": case "briefcase": case "politician": return true;
            }
            return false;
        }

        ActorClipSet.Clip Clip(string name) => _set?.Get(name);

        void BindClips()
        {
            _set = null; _cWalk = _cJog = _cSprint = _cSit = _cSitTalk = null;
            if (!UseClips || _rig == null) return;
            _set = ActorClipSet.Load(!string.IsNullOrEmpty(_rig.AnimSet) ? _rig.AnimSet : (_rig.IsGlb ? _rig.ActorId : "PROC"));
            if (_set == null) return;
            string st = _rig.IdleStyle ?? "";
            bool formal = st == "stiff" || st == "behind" || st == "politician" || st == "clasp" || st == "calm" || st == "notebook" || st == "briefcase" || _rig.IsGlb;
            _cWalk = (formal ? Clip("Walk_Formal_Loop") : null) ?? Clip("Walk_Loop");
            _cJog = Clip("Jog_Fwd_Loop"); _cSprint = Clip("Sprint_Loop");
            _cSit = Clip("Sitting_Idle_Loop"); _cSitTalk = Clip("Sitting_Talking_Loop");
            _idleRate = 0.88f + 0.24f * Mathf.Repeat(_seed * 7.31f, 1f);
        }

        void StartTransition(ActorClipSet.Clip c, float rate = 1f)
        {
            if (c == null) { _trans = null; return; }
            _trans = c; _transT = 0f; _transRate = rate;
        }

        void InitPersona() { }
        void OnPostureChanged(Posture from, Posture to) { }

        // ---- charpolish step 0 contract: stairs / seats (owner: implementer 3)
        /// <summary>Seat height of the furniture being sat on (m above the feet plane; &lt;= 0 = default 0.45).</summary>
        public void SetSeatHeight(float metres) { }
        /// <summary>0..1 while a sit-down / stand-up clip moves the hips between the approach spot and the seat (ActorView
        /// blends the root with it); 0 when no seat transition plays.</summary>
        public float SeatRootBlend => 0f;

        void UpdateLocomotionInput(float dt)
        {
            Vector3 lv = transform.InverseTransformDirection(_vel); lv.y = 0f;
            _hopSpeed = Mathf.MoveTowards(_hopSpeed, AnklesBound ? lv.magnitude : 0f, dt * 6f);
            if (lv.sqrMagnitude > 0.0025f) _hopDir = lv.normalized;
            // an armpit drag walks backward, facing the body (the pose is turned round in EvaluateDragger)
            if (_dragBody != null && _dragGrip == DragGrip.Armpits) lv = -lv;
            // a stagger or a fall moves the feet itself (the root may be moved by physics meanwhile)
            if (_stag.Active || _fall.Active || _getUp.Active || _pairedRef != null || AnklesBound) lv *= 0f;
            float targetSpeed = lv.magnitude;
            _speed = Mathf.MoveTowards(_speed, targetSpeed, dt * 6f);
            if (targetSpeed > 0.05f) _moveDirL = Vector3.Slerp(_moveDirL, lv.normalized, dt * 8f);
            bool canMove = _dead < 0 && !_beingCarried && _posture == Posture.Stand && _conscious;
            float moveSpeed = canMove ? _speed : 0f;
            _walkW = Mathf.MoveTowards(_walkW, moveSpeed > 0.08f ? 1f : 0f, dt * 4.5f);
            _runW = Mathf.MoveTowards(_runW, (_running && moveSpeed > 2.0f) ? 1f : 0f, dt * 3f);
        }

        float MoveSpeed => (_dead < 0 && !_beingCarried && _posture == Posture.Stand && _conscious) ? _speed : 0f;

        void EvaluateStanding(float dt, ActorPoses.Dims dims)
        {
            float limp = (_limpL || _limpR) ? 1f : 0f;
            float cycleSpeed = MoveSpeed;
            ActorPoses.Idle(_base, dims, _time, _rig.IdleStyle, _mobility);
            _idleFootL = _base.FootL; _idleFootR = _base.FootR;
            if (_walkW <= 0.001f) { PlantFeet(dt, dims); StairFeet(dt); return; }
            float stepLen = ActorPoses.StepLength(dims, cycleSpeed, _runW);
            _phase += dt * cycleSpeed / Mathf.Max(0.2f, 2f * stepLen) * (limp > 0 ? 0.85f : 1f);
            _phase -= Mathf.Floor(_phase);
            ActorPoses.Walk(_tmp, dims, _phase, cycleSpeed, _moveDirL, _runW, _limpL, _limpR, _mobility, _time);
            if (_dragBody != null) { _tmp.HipsOffset *= 0.6f; }
            // captured walk / jog / sprint when moving forward (the procedural cycle handles strafing, backing up, limping)
            float fwd = Mathf.Clamp01((_moveDirL.z - 0.6f) / 0.3f) * (limp > 0 ? 0f : 1f) * (_mobility > 0.6f ? 1f : 0f);
            if (fwd > 0.001f && _cWalk != null && _cWalk.CycleDist > 0.05f)
            {
                float speedNow = Mathf.Max(0.05f, cycleSpeed);
                var runClip = speedNow > 3.6f && _cSprint != null ? _cSprint : _cJog;
                float wantRun = _runW > 0.001f && runClip != null && runClip.CycleDist > 0.05f ? _runW : 0f;
                // one shared phase; the cycle length follows whichever gait dominates so the feet stay planted
                float cycW = Mathf.Max(0.3f, _cWalk.CycleDist * LegLen), cycR = wantRun > 0f ? Mathf.Max(0.3f, runClip.CycleDist * LegLen) : cycW;
                float cyc = Mathf.Lerp(cycW, cycR, wantRun);
                _clipPhase += dt * speedNow / cyc;
                _clipPhase -= Mathf.Floor(_clipPhase);
                _tmp2.CopyFrom(_tmp);
                ActorClipSet.Sample(_cWalk, _clipPhase * _cWalk.Length, _tmp2, LegLen);
                if (wantRun > 0f)
                {
                    _tmp3.CopyFrom(_tmp);
                    ActorClipSet.Sample(runClip, _clipPhase * runClip.Length, _tmp3, LegLen);
                    _tmp2.Blend(_tmp2, _tmp3, wantRun);
                }
                // hands behind the back / in pockets / holding a prop stay that way while walking
                if (StyleKeepsArms(_rig.IdleStyle) && wantRun < 0.5f) _tmp2.Overlay(_base, 1f - wantRun * 2f, ArmsOnlyMask, false);
                _tmp.Blend(_tmp, _tmp2, fwd);
            }
            _base.Blend(_base, _tmp, _walkW);
            PlantFeet(dt, dims);
            StairFeet(dt);
        }

        // ---------------------------------------------------------------- idle foot planting (turn in place, stop, small nudges)
        struct FootPlant { public bool Valid, Stepping; public Vector3 W, From; public float Yaw, FromYaw, T; }
        FootPlant _plantL, _plantR;

        /// <summary>While standing, feet stay where they are in the world; when the root turns or drifts away from them (turning on
        /// the spot, a nudge, the end of a walk) they step back under the body one at a time instead of sliding.</summary>
        void PlantFeet(float dt, ActorPoses.Dims d)
        {
            bool can = _posture == Posture.Stand && _base.LegIK > 0.5f && !_stag.Active && !_atk.Active && !_fall.Active && !_getUp.Active && _dragBody == null &&
                       !_dragged && _pairedRef == null && !AnklesBound && !(_aActive && _action != ActionAnim.None) && _dead < 0 && _conscious && !_beingCarried && _lod == 0;
            if (!can) { ResetFeet(); return; }
            float rootYaw = transform.eulerAngles.y;
            for (int k = 0; k < 2; k++)
            {
                bool left = k == 0;
                ref FootPlant f = ref (left ? ref _plantL : ref _plantR);
                Vector3 local = left ? _base.FootL : _base.FootR;
                Quaternion lrot = left ? _base.FootRotL : _base.FootRotR;
                Vector3 want = transform.TransformPoint(local);
                Vector3 lf = lrot * Vector3.forward;
                float wantYaw = rootYaw + Mathf.Atan2(lf.x, lf.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Asin(Mathf.Clamp(lf.y, -1f, 1f)) * Mathf.Rad2Deg;
                if (_walkW > 0.02f || !f.Valid) { f.W = want; f.Yaw = wantYaw; f.Valid = true; f.Stepping = false; continue; }
                Vector3 pos = f.W; float yaw = f.Yaw; float lift = 0f;
                if (f.Stepping)
                {
                    f.T += dt;
                    float u = Clamp01(f.T / 0.32f);
                    pos = Vector3.Lerp(f.From, want, Ease(u));
                    yaw = Mathf.LerpAngle(f.FromYaw, wantYaw, Ease(u));
                    lift = Mathf.Sin(u * Mathf.PI) * 0.045f * (Height / 1.75f);
                    if (u >= 1f) { f.Stepping = false; f.W = want; f.Yaw = wantYaw; }
                }
                Vector3 lp = transform.InverseTransformPoint(pos);
                lp.y = local.y + lift;
                Quaternion r = Quaternion.Euler(0f, Mathf.DeltaAngle(rootYaw, yaw), 0f) * Quaternion.AngleAxis(pitch, Vector3.right);
                if (left) { _base.FootL = lp; _base.FootRotL = r; } else { _base.FootR = lp; _base.FootRotR = r; }
            }
            // start a step for the foot that is furthest off (one at a time)
            if (!_plantL.Stepping && !_plantR.Stepping)
            {
                float eL = FootError(ref _plantL, _base.FootL, true), eR = FootError(ref _plantR, _base.FootR, false);
                if (Mathf.Max(eL, eR) > 1f)
                {
                    ref FootPlant f = ref (eL >= eR ? ref _plantL : ref _plantR);
                    f.Stepping = true; f.T = 0f; f.From = f.W; f.FromYaw = f.Yaw;
                }
            }
        }

        /// <summary>How far a planted foot is from where the idle pose wants it (1 = time to step): 9 cm or 28 degrees.</summary>
        float FootError(ref FootPlant f, Vector3 shownLocal, bool left)
        {
            if (!f.Valid) return 0f;
            Vector3 restLocal = left ? _idleFootL : _idleFootR;
            Vector3 a = transform.InverseTransformPoint(f.W); a.y = 0f; restLocal.y = 0f;
            float yawErr = Mathf.Abs(Mathf.DeltaAngle(f.Yaw, transform.eulerAngles.y + (left ? -7f : 9f)));
            return Mathf.Max((a - restLocal).magnitude / (0.09f * Height / 1.75f), yawErr / 28f);
        }

        // ---- stairs: feet on the treads, hips down to the lower foot (the root follows the kernel's smooth slope)
        Vector3 _stBottom, _stTop; int _stSteps; float _stairW;

        /// <summary>On a staircase: bottom/top of the flight in world space and its number of steps (steps &lt;= 0 = off). Feet land on
        /// the treads and the hips drop to the lower foot, so nobody walks on an invisible ramp.</summary>
        public void SetStairs(Vector3 bottomWorld, Vector3 topWorld, int steps) { _stBottom = bottomWorld; _stTop = topWorld; _stSteps = steps; }

        float TreadY(Vector3 wp)
        {
            Vector3 run = _stTop - _stBottom; float rise = run.y; run.y = 0f; float len = run.magnitude;
            if (len < 0.1f || _stSteps <= 0) return transform.position.y;
            float s = Vector3.Dot(wp - _stBottom, run / len) / len;
            int k = Mathf.Clamp(Mathf.CeilToInt(s * _stSteps - 1e-3f), 0, _stSteps);
            return _stBottom.y + rise * k / _stSteps;
        }

        void StairFeet(float dt)
        {
            _stairW = Mathf.MoveTowards(_stairW, _stSteps > 0 ? 1f : 0f, dt * 4f);
            if (_stairW < 0.001f || _posture != Posture.Stand || _base.LegIK < 0.5f || _dead >= 0) return;
            float rootY = transform.position.y;
            // the foot rests on the highest tread under its length (heel to ball), so toes never sink into the next riser
            Vector3 fw = transform.forward * 0.13f * (Height / 1.75f), bw = transform.forward * 0.05f * (Height / 1.75f);
            Vector3 wl = transform.TransformPoint(_base.FootL), wr = transform.TransformPoint(_base.FootR);
            float dL = Mathf.Max(TreadY(wl + fw), TreadY(wl - bw)) - rootY, dR = Mathf.Max(TreadY(wr + fw), TreadY(wr - bw)) - rootY;
            dL = Mathf.Clamp(dL, -0.35f, 0.35f) * _stairW; dR = Mathf.Clamp(dR, -0.35f, 0.35f) * _stairW;
            _base.FootL += new Vector3(0f, dL, 0f); _base.FootR += new Vector3(0f, dR, 0f);
            _base.HipsOffset += new Vector3(0f, Mathf.Min(0f, Mathf.Min(dL, dR)) * 0.9f + 0.25f * Mathf.Max(0f, Mathf.Min(dL, dR)), 0f);
        }

        Vector3 _idleFootL, _idleFootR;
        /// <summary>QA: planted-feet state.</summary>
        public string DebugFeet => $"L valid {_plantL.Valid} step {_plantL.Stepping} yaw {_plantL.Yaw:F0} | R valid {_plantR.Valid} step {_plantR.Stepping} yaw {_plantR.Yaw:F0} | root {transform.eulerAngles.y:F0} walkW {_walkW:F2}";
        float _hopSpeed; Vector3 _hopDir = Vector3.forward;

        void EvaluatePosture(float dt, ActorPoses.Dims dims, Posture p)
        {
            ActorPoses.PostureBase(_base, dims, p, _time, _rig.IdleStyle);
            if (p == Posture.Sit && _cSit != null)
            {
                // seated: legs / hips stay fitted to the chair, the upper body breathes and shifts like a person
                _tmp2.CopyFrom(_base);
                ActorClipSet.Sample(_gActive && (_gesture == Gesture.Talk || _gesture == Gesture.TalkEmphatic) && _cSitTalk != null ? _cSitTalk : _cSit, _time * _idleRate, _tmp2, LegLen);
                _base.Overlay(_tmp2, 0.85f, ArmsHeadMask, false);
            }
            else if (p == Posture.Crouch && Clip("Crouch_Idle_Loop") != null)
            {
                _tmp2.CopyFrom(_base);
                ActorClipSet.Sample(Clip("Crouch_Idle_Loop"), _time * _idleRate, _tmp2, LegLen);
                _base.Overlay(_tmp2, 1f, null, true);
            }
        }

        void EvaluateTransitions(float dt)
        {
            if (_trans != null)
            {
                // captured transition (sit down / stand up / get up): starts from the last pose, lands in the new posture
                float L = _trans.Length;
                _transT += dt * _transRate;
                _tmp.CopyFrom(_base);
                ActorClipSet.Sample(_trans, Mathf.Min(_transT, L), _tmp, LegLen);
                float wIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_transT / 0.18f));
                float wOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_transT - L * 0.72f) / (L * 0.28f)));
                _tmp2.Blend(_from, _tmp, wIn);
                _tmp3.CopyFrom(_base);
                _base.Blend(_tmp2, _tmp3, wOut);
                _postureT = 1f;
                if (_transT >= L) _trans = null;
            }
            else if (_postureT < 1f)
            {
                _postureT = Mathf.Min(1f, _postureT + dt / 0.5f);
                _tmp.CopyFrom(_base);
                _base.Blend(_from, _tmp, Mathf.SmoothStep(0f, 1f, _postureT));
            }
        }
    }
}
