using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>Upper-body layers: gestures, keyframed / captured actions, idle fidgets, first-person calm, look-at.</summary>
    public partial class ActorAnimator
    {
        public void PlayGesture(Gesture g, float duration = 0f)
        {
            if (g == Gesture.WeightShift) { _nextShift = 0f; return; }
            if (g == Gesture.None) { if (_gActive) _gDur = Mathf.Min(_gDur, _gT + 0.3f); return; }
            _gesture = g; _gT = 0f; _gDur = duration > 0f ? duration : ActorPoses.DefaultGestureDuration(g); _gActive = true;
        }

        public void PlayAction(ActionAnim a, float duration)
        {
            if (a == ActionAnim.None) { if (_aActive) _aDur = Mathf.Min(_aDur, _aT + 0.25f); return; }
            // motion track: kinds with their own systems (attack beats, reactions, defences)
            if (IsAttackKind(a)) { if (!(_atk.Active && _atk.Kind == a)) PlayAttack(a, _heldClass); return; }
            switch (a)
            {
                case ActionAnim.GetUp: PlayGetUp(0f); return;
                case ActionAnim.Startle: PlayStartle(transform.position + transform.forward * 2f + Vector3.up, 0.7f); return;
                case ActionAnim.HitHead: PlayHit(BodyRegion.Head, -transform.forward, 0.6f); return;
                case ActionAnim.HitGut: PlayHit(BodyRegion.Abdomen, -transform.forward, 0.7f); return;
                case ActionAnim.ClutchWound: PlayHit(BodyRegion.Chest, -transform.forward, 0.35f); return;
                case ActionAnim.Defend: case ActionAnim.GrabWrist: case ActionAnim.TurnAway: case ActionAnim.Dodge: case ActionAnim.Guard:
                    PlayDefense(a, (_partner != null ? _partner.transform.position : transform.position + transform.forward)); return;
                case ActionAnim.Aim: SetCombatReady(true, Firearm(_heldClass) ? _heldClass : WeaponClass.Pistol); return;
            }
            // an attack in progress ignores the kernel's Struggle for the attacker (Struggle is for victims)
            if (a == ActionAnim.Struggle && _atk.Active) return;
            _action = a; _aT = 0f; _aDur = duration > 0f ? duration : ActorPoses.DefaultActionDuration(a); _aActive = true;
            _afterAction = null;
            if (a == ActionAnim.Fall) _afterAction = Random.value < 0.5f ? Posture.LieBack : Posture.LieFront;
            if (a == ActionAnim.Sleep) _afterAction = null;
            BindActionClip(a, _aDur);
            // a collapse onto the back uses the captured fall; give it the time it needs
            if (a == ActionAnim.Fall && _afterAction == Posture.LieBack && _set != null && Clip("Death01") != null)
            {
                _actClip = Clip("Death01"); _actFull = true; _actLoop = false; _actPing = false;
                _aDur = Mathf.Max(_aDur, 1.5f); _actRate = _actClip.Length * 0.85f / _aDur;
            }
        }

        ActorClipSet.Clip GestureClip(Gesture g)
        {
            switch (g)
            {
                case Gesture.Talk: return Clip("Idle_Talking_Loop");
                case Gesture.CrossArms: return Clip("Idle_FoldArms_Loop");
                case Gesture.ShakeHead: return Clip("Idle_No_Loop");
            }
            return null;
        }

        /// <summary>Picks the captured clip for an action (null = procedural only) and how to play it.</summary>
        void BindActionClip(ActionAnim a, float dur)
        {
            _actClip = null; _actLoop = false; _actFull = false; _actPing = false; _actRate = 1f; _actEnd = -1f;
            if (_set == null) return;
            bool longAct = dur > 4f;
            ActorClipSet.Clip c = null;
            switch (a)
            {
                case ActionAnim.PickUp: case ActionAnim.PutDown: c = Clip("PickUp_Table"); _actFull = true; break;
                case ActionAnim.Use: if (longAct) { c = Clip("Interact"); _actPing = true; } break;
                case ActionAnim.Operate: c = Clip("Interact"); _actPing = true; break;
                case ActionAnim.Craft: c = Clip("Interact"); _actPing = true; break;
                case ActionAnim.FirstAid: c = Clip("Fixing_Kneeling"); _actFull = true; _actLoop = true; break;
                case ActionAnim.Garden: c = Clip(Random.value < 0.5f ? "Farm_Watering" : "Farm_PlantSeed"); _actFull = true; _actLoop = true; break;
                case ActionAnim.Search: c = Clip("Chest_Open"); _actFull = true; _actPing = true; break;
                case ActionAnim.Examine: c = Clip("Farm_Harvest"); _actFull = true; break;
                case ActionAnim.Hurt: c = Clip(Random.value < 0.5f ? "Hit_Chest" : "Hit_Head"); break;
                case ActionAnim.Stab: c = Clip("Punch_Cross"); break;
                case ActionAnim.Slash: c = Clip("Melee_Hook"); break;
                case ActionAnim.Overhead: c = Clip("TreeChopping_Loop"); break;
                case ActionAnim.Throw: c = Clip("OverhandThrow"); _actFull = true; break;
                case ActionAnim.Swim: c = Clip("Swim_Idle_Loop"); _actFull = true; _actLoop = true; break;
            }
            if (c == null) return;
            _actClip = c;
            if (c.Loop && a != ActionAnim.Overhead) _actLoop = true;
            if (a == ActionAnim.Overhead) { _actEnd = c.Length * 0.5f; _actRate = _actEnd / Mathf.Max(0.2f, dur * 0.6f); return; }
            if (!_actLoop && !_actPing)
                _actRate = Mathf.Clamp(c.Length / Mathf.Max(0.2f, dur), 0.75f, 1.6f); // one-shots fit the requested time within reason
        }

        float ActionClipTime(float t)
        {
            var c = _actClip;
            if (_actLoop) return t * _idleRate;
            if (_actPing)
            {
                // work loops from one-shot clips: play, then sway back and forth through the busy middle part
                float L = c.Length;
                if (t < L * 0.55f) return t;
                float u = (t - L * 0.55f) / Mathf.Max(0.2f, L * 0.35f);
                float tri = 1f - Mathf.Abs(Mathf.Repeat(u, 2f) - 1f);
                return L * (0.55f - 0.2f * tri);
            }
            return _actEnd > 0f ? Mathf.Min(t * _actRate, _actEnd) : t * _actRate;
        }

        void EvaluateActionLayer(float dt, ActorPoses.Dims dims)
        {
            if (!_aActive || _dead >= 0 || _beingCarried) return;
            _aT += dt;
            if (_atk.Active && _atk.Kind == _action) { if (_aT >= _aDur) _aActive = false; return; }   // posed by the attack beat
            float w = Mathf.Clamp01(_aT / 0.2f) * Mathf.Clamp01((_aDur - _aT) / 0.25f);
            bool fullBody;
            _tmp.CopyFrom(_base);
            ActorPosesExt.Weapon = _heldClass;
            ActorPoses.ActionPose(_tmp, dims, _action, _aT, _aDur, _time, out fullBody);
            if (_actClip != null)
            {
                // captured motion for the body, the procedural hand shape stays (grip / open)
                ActorClipSet.Sample(_actClip, ActionClipTime(_aT), _tmp, LegLen);
                fullBody = _actFull;
            }
            if (_action == ActionAnim.Fall) w = Mathf.Clamp01(_aT / 0.12f);
            _base.Overlay(_tmp, w, fullBody ? null : UpperMask, fullBody);
            if (_aT >= _aDur)
            {
                _aActive = false; _actClip = null;
                if (_afterAction.HasValue)
                {
                    _from.CopyFrom(_base);
                    _posture = _afterAction.Value; _postureT = 0.99f; _afterAction = null;
                    _last.CopyFrom(_base);
                }
            }
        }

        void EvaluateFidgets(float dt)
        {
            if (AutoFidget && !_gActive && !_aActive && !_atk.Active && _dead < 0 && !_beingCarried && _conscious && _posture == Posture.Stand && _walkW < 0.01f && _break < 0.05f)
            {
                _idleClock += dt;
                if (_idleClock > _nextFidget)
                {
                    _idleClock = 0f; _nextFidget = Random.Range(9f, 18f);
                    var pool = FidgetPoolFor(_rig.IdleStyle);
                    PlayGesture(pool[Random.Range(0, pool.Length)], Random.Range(1.6f, 3.2f));
                }
            }
            else _idleClock = 0f;
        }
        static readonly Gesture[] FidgetPool = { Gesture.LookAround, Gesture.Listen, Gesture.Nod, Gesture.Think, Gesture.Shrug };

        void EvaluateGestureLayer(float dt, ActorPoses.Dims dims)
        {
            if (!_gActive || _dead >= 0 || _beingCarried || !_conscious || _pairedRef != null) return;
            if (WristsBound && _gesture != Gesture.Cry && _gesture != Gesture.ShakeHead && _gesture != Gesture.Nod && _gesture != Gesture.Shiver) { _gActive = false; return; }
            _gT += dt;
            float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_gT / 0.28f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_gDur - _gT) / 0.35f));
            _gW = w;
            _tmp.CopyFrom(_base);
            ActorPoses.GesturePose(_tmp, dims, _gesture, _gT, _gDur, _time);
            var gc = GestureClip(_gesture);
            if (gc != null) ActorClipSet.Sample(gc, _gT * (gc.Loop ? _idleRate : Mathf.Max(0.6f, gc.Length / Mathf.Max(0.3f, _gDur))), _tmp, LegLen);
            _base.Overlay(_tmp, w, _aActive ? ArmsHeadMask : UpperMask, false);
            if (_gT >= _gDur) _gActive = false;
        }

        /// <summary>
        /// First-person body mode for the player (set while the head is hidden): while moving / crouching / carrying the
        /// arms hang low beside the hips (swing ≤ 8°, elbows ≤ 18°), the torso barely leans or twists and the bob is
        /// halved, so the forearms and sleeves never cross the lower part of the view; hand props sit low by the hips.
        /// Scripted actions (drinking, reading, pulling a chair...) still show the hands.
        /// </summary>
        public bool FirstPersonCalm;
        float _fpW;
        float _gW;

        void ApplyFirstPersonCalm(float dt)
        {
            bool actionShowsHands = (_aActive && _action != ActionAnim.Hurt && _action != ActionAnim.Stagger && _action != ActionAnim.Carry) || _atk.Active || _combatReady;
            bool moving = _walkW > 0.02f;
            bool crouch = _posture == Posture.Crouch;
            bool carrying = _carrying != null || (_aActive && _action == ActionAnim.Carry);
            float want = actionShowsHands ? 0f : (moving || crouch || carrying ? 1f : 0.6f);
            _fpW = Mathf.MoveTowards(_fpW, want, dt * 4f);
            if (_fpW <= 0.001f) return;
            float ph = _phase * Mathf.PI * 2f;
            float swing = 8f * Mathf.Clamp01(_walkW);
            _tmp4.CopyFrom(_base);
            float ao = ArmIdleOut + 2f;
            if (carrying)
            {
                // the carried body rests on the right shoulder: the right hand holds it there, tucked back out of view
                _tmp4.SetArm(false, -12f, 68f, 0f, 118f, 0f, 0f);
                _tmp4.SetArm(true, -Mathf.Cos(ph) * swing, ao, 0f, 14f, 8f, 4f);
            }
            else
            {
                _tmp4.SetArm(true, -Mathf.Cos(ph) * swing - 2f, ao, 0f, 12f + 4f * Mathf.Max(0f, -Mathf.Cos(ph)), 8f, 4f);
                _tmp4.SetArm(false, Mathf.Cos(ph) * swing - 2f, ao, 0f, 12f + 4f * Mathf.Max(0f, Mathf.Cos(ph)), 8f, 4f);
            }
            _tmp4.SetShoulder(true, 0f); _tmp4.SetShoulder(false, 0f);
            if (!carrying) { _tmp4.SoftHand(true); _tmp4.SoftHand(false); }
            _base.Overlay(_tmp4, _fpW, ArmsOnlyMask, false);
            // calmer torso: most of the lean / twist / bob goes
            float k = _fpW * (moving ? 0.7f : 0.4f);
            _base.R[(int)HBone.Spine] = Quaternion.Slerp(_base.R[(int)HBone.Spine], Quaternion.identity, k);
            _base.R[(int)HBone.Chest] = Quaternion.Slerp(_base.R[(int)HBone.Chest], Quaternion.identity, k);
            _base.R[(int)HBone.UpperChest] = Quaternion.Slerp(_base.R[(int)HBone.UpperChest], Quaternion.identity, k);
            _base.R[0] = Quaternion.Slerp(_base.R[0], Quaternion.identity, k * 0.8f);
            if (moving) _base.HipsOffset = new Vector3(_base.HipsOffset.x * 0.5f, _base.HipsOffset.y * (1f - 0.5f * _fpW), _base.HipsOffset.z * 0.5f);
        }

        // ---------------------------------------------------------------- look-at
        Vector3 _glancePt; float _glanceUntil = -1f;
        /// <summary>Look at worldPoint for 'seconds', overriding SetLookAt (hit reactions: look toward the attacker).</summary>
        public void Glance(Vector3 worldPoint, float seconds) { _glancePt = worldPoint; _glanceUntil = _time + Mathf.Max(0f, seconds); }

        /// <summary>Trial stand: hands rest on / gesture above the rail at railPointWorld (null = off).</summary>
        public void SetLectern(Vector3? railPointWorld, Vector3 railForwardWorld) { }

        void EvaluateLook(float dt)
        {
            Vector2 wantYP = Vector2.zero;
            Vector3? look = _time < _glanceUntil ? _glancePt : _look;
            if (_ctlLook.HasValue && !(_time < _glanceUntil)) look = _ctlLook;
            bool noLook = _dragged || _dragBody != null || (_pairedRef != null && _pairedRef.V == this);
            if (look.HasValue && _dead < 0 && _conscious && !_beingCarried && !noLook)
            {
                Vector3 eye = _rig.EyeAnchor != null ? _rig.EyeAnchor.position : transform.position + Vector3.up * Height * 0.93f;
                Vector3 d = transform.InverseTransformDirection(look.Value - eye);
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                if (Mathf.Abs(yaw) < 120f) wantYP = new Vector2(Mathf.Clamp(yaw, -75f, 75f), Mathf.Clamp(pitch, -35f, 45f));
            }
            _lookYP = Vector2.Lerp(_lookYP, wantYP, 1f - Mathf.Exp(-dt * 7f));
            if (_lookYP.sqrMagnitude > 0.01f)
            {
                ApplyLook(_base, HBone.Chest, _lookYP * 0.15f);
                ApplyLook(_base, HBone.Neck, _lookYP * 0.35f);
                ApplyLook(_base, HBone.Head, _lookYP * 0.5f);
            }
        }

        static void ApplyLook(ActorPose p, HBone b, Vector2 yawPitch)
        {
            p.R[(int)b] = Quaternion.AngleAxis(yawPitch.x, Vector3.up) * p.R[(int)b] * Quaternion.AngleAxis(yawPitch.y, Vector3.right);
        }
    }
}
