using System.Reflection;
using BL23.Game.Physicality;
using UnityEngine;
using static BL23.Game.Characters.MotionKit;

namespace BL23.Game.Characters
{
    /// <summary>Everyday body actions (motion track): body posture around the owner's PhysicalActionController (squat vs stoop by
    /// reach height, the free hand on the thigh, handover lean, carried weight, brace lean), dragging bodies, leaning on tables and
    /// generic hand targets. The controller keeps its hand IK and contact events; this layer only shapes the rest of the body.</summary>
    public partial class ActorAnimator
    {
        // ================================================================ controller probe (read-only view of the owner's controller)
        sealed class ControllerView
        {
            static FieldInfo _fPoint, _fLeft, _fClock, _fDur, _fWeight, _fCarried, _fMass, _fReceiver;
            static bool _bound;
            public PhysicalActionController C;
            public float[] HandWeight, CarryMass; public Transform[] Carried;
            public static void Bind()
            {
                if (_bound) return; _bound = true;
                var t = typeof(PhysicalActionController); var f = BindingFlags.NonPublic | BindingFlags.Instance;
                _fPoint = t.GetField("_point", f); _fLeft = t.GetField("_left", f); _fClock = t.GetField("_clock", f); _fDur = t.GetField("_duration", f);
                _fWeight = t.GetField("_handWeight", f); _fCarried = t.GetField("_carried", f); _fMass = t.GetField("_carryMass", f); _fReceiver = t.GetField("_receiver", f);
            }
            public ControllerView(PhysicalActionController c)
            {
                Bind(); C = c;
                HandWeight = _fWeight?.GetValue(c) as float[]; Carried = _fCarried?.GetValue(c) as Transform[]; CarryMass = _fMass?.GetValue(c) as float[];
            }
            public bool Target(out Vector3 point, out bool left, out float phase)
            {
                point = default; left = false; phase = 0f;
                if (_fPoint == null || _fLeft == null) return false;
                point = (Vector3)_fPoint.GetValue(C); left = (bool)_fLeft.GetValue(C);
                float clock = _fClock != null ? (float)_fClock.GetValue(C) : 0f, dur = _fDur != null ? (float)_fDur.GetValue(C) : 1f;
                phase = clock / Mathf.Max(0.05f, dur);
                return true;
            }
            public PhysicalActionController Receiver => _fReceiver?.GetValue(C) as PhysicalActionController;
        }
        ControllerView _ctl;
        /// <summary>QA: the controller-refinement state (body weight, reach target in root space, kind).</summary>
        public string DebugReach => $"ctlW {_ctlBodyW:F2} reach {_reachLocal.ToString("F2")} kind {(_physicalActions != null ? _physicalActions.CurrentAction.ToString() : "-")} contact {(_physicalActions != null && _physicalActions.ContactMade)} ctl {(_ctl != null)}";
        readonly ActorPose _preCtl = new ActorPose(), _act = new ActorPose();
        float _ctlBodyW, _reachLow, _reachHigh; Vector3 _reachLocal; bool _reachLeft;
        Vector3? _ctlLook;

        /// <summary>Called right after PhysicalActionController.PreparePose: re-shapes the body for the controller's reach
        /// (squat for the floor, stoop for low tables with the free hand on the thigh, tiptoe for high shelves), the handover lean,
        /// the brace lean and the posture under a carried load. The controller's hand IK then lands the hand.</summary>
        void RefineControllerPose(float dt, ActorPoses.Dims d)
        {
            if (_physicalActions == null || !CanApplyPhysicalActions) { _ctlBodyW = Mathf.MoveTowards(_ctlBodyW, 0f, dt * 4f); _ctlLook = null; return; }
            if (_ctl == null || _ctl.C != _physicalActions) _ctl = new ControllerView(_physicalActions);
            var kind = _physicalActions.CurrentAction;
            float sc = Height / 1.75f;
            _ctlLook = null;
            // ---- carried load (the controller holds the hand at its carry point; the body takes the weight)
            float mass = 0f; int hands = 0; bool leftOnly = false;
            if (_ctl.Carried != null && _ctl.CarryMass != null)
                for (int s = 0; s < 2; s++) if (_ctl.Carried[s] != null) { mass += _ctl.CarryMass[s]; hands++; leftOnly = s == 0; }
            if (_loadKg > 0f) { mass = Mathf.Max(mass, _loadKg); hands = _loadTwoHands ? 2 : Mathf.Max(hands, 1); }
            if (mass > 2.5f && _posture == Posture.Stand) LoadPosture(_final, mass, hands >= 2 || mass >= 5f, leftOnly, sc);
            bool reachKind = kind == PhysicalActionKind.PickUp || kind == PhysicalActionKind.Place || kind == PhysicalActionKind.Reach;
            bool handKind = kind == PhysicalActionKind.Give || kind == PhysicalActionKind.Receive;
            if ((reachKind || handKind || kind == PhysicalActionKind.Brace) && _ctl.Target(out var pt, out var left, out var phase))
            {
                _reachLocal = transform.InverseTransformPoint(pt); _reachLeft = left;
                _ctlLook = pt;
                float w = _ctl.HandWeight != null ? _ctl.HandWeight[left ? 0 : 1] : 1f;
                // after contact on a pick-up the hand returns to the carry point: the body rises with it
                float back = kind == PhysicalActionKind.PickUp && _physicalActions.ContactMade ? Win(phase, 0.6f, 1f) : kind == PhysicalActionKind.Place && _physicalActions.ContactMade ? Win(phase, 0.62f, 0.95f) : 0f;
                _ctlBodyW = Mathf.MoveTowards(_ctlBodyW, w * (1f - back), dt * 6f);
                if (kind == PhysicalActionKind.Brace) BraceBody(_final, d, sc);
                else if (handKind) HandoverBody(_final, d, sc, kind == PhysicalActionKind.Receive);
                else ReachBody(_final, d, sc);
            }
            else _ctlBodyW = Mathf.MoveTowards(_ctlBodyW, 0f, dt * 4f);
        }

        /// <summary>Squat vs stoop vs tiptoe for a hand target (root space) — the free hand rests on the thigh when stooping.</summary>
        void ReachBody(ActorPose p, ActorPoses.Dims d, float sc)
        {
            float w = Ease(_ctlBodyW);
            if (w <= 0.001f || _posture != Posture.Stand) return;
            Vector3 t = _reachLocal;
            float h = t.y / sc;                                   // target height for a 1.75 m body
            float fwd = Mathf.Max(0f, new Vector2(t.x, t.z).magnitude - 0.35f * sc);
            bool left = _reachLeft;
            float side = left ? -1f : 1f;
            float yaw = Mathf.Clamp(Mathf.Atan2(t.x, Mathf.Max(0.05f, t.z)) * Mathf.Rad2Deg, -50f, 50f);
            var q = _act; q.CopyFrom(p);
            if (h < 0.42f)
            {
                // floor: a real squat (knees forward, heels may lift, back fairly straight), the reaching side a little lower;
                // very low targets drop one knee to the floor (half kneel)
                float deep = Clamp01((0.42f - h) / 0.32f);
                float hipsDrop = Mathf.Lerp(0.28f, 0.44f, deep) * sc;
                q.HipsOffset = new Vector3(side * 0.03f * sc, -hipsDrop, -0.06f * sc + fwd * 0.25f);
                q.R[0] = ActorPose.Body(28f + 10f * deep, yaw * 0.3f, 0f);
                q.R[(int)HBone.Spine] = ActorPose.Body(18f + 8f * deep, yaw * 0.25f, -side * 4f);
                q.R[(int)HBone.Chest] = ActorPose.Body(10f + 14f * fwd, yaw * 0.2f, 0f);
                q.R[(int)HBone.Neck] = ActorPose.Body(-8f);
                q.LegIK = 1f;
                // staggered feet: the foot opposite the reaching hand steps forward, the other heel lifts
                q.FootL = d.FootL + new Vector3(-0.05f * sc, 0f, (left ? -0.06f : 0.12f) * sc);
                q.FootR = d.FootR + new Vector3(0.05f * sc, 0f, (left ? 0.12f : -0.06f) * sc);
                if (left) q.FootRotL = ActorPose.E(24f * deep, -10f, 0f); else q.FootRotR = ActorPose.E(24f * deep, 10f, 0f);
                // the free hand rests on the knee
                q.SetArm(!left, 36f, ArmIdleOut + 2f, 30f, 46f, 40f, -20f); q.SoftHand(!left);
            }
            else if (h < 0.98f)
            {
                // low table / chair / drawer: hip hinge, soft knees, hips pushed back, the free hand braced on the thigh
                float k = Clamp01((0.98f - h) / 0.5f);
                q.HipsOffset = new Vector3(0f, -(0.05f + 0.1f * k) * sc, -(0.07f + 0.08f * k) * sc);
                q.R[0] = ActorPose.Body(30f + 32f * k + 10f * Clamp01(fwd * 2f), yaw * 0.35f, 0f);
                q.R[(int)HBone.Spine] = ActorPose.Body(10f + 8f * k, yaw * 0.3f, -side * 3f);
                q.R[(int)HBone.Chest] = ActorPose.Body(4f + 6f * k, yaw * 0.25f, 0f);
                q.R[(int)HBone.Neck] = ActorPose.Body(-14f * k);
                q.LegIK = 1f;
                q.FootL = d.FootL + new Vector3(-0.02f * sc, 0f, (left ? 0.02f : 0.06f) * sc);
                q.FootR = d.FootR + new Vector3(0.02f * sc, 0f, (left ? 0.06f : 0.02f) * sc);
                q.SetArm(!left, 30f, ArmIdleOut - 4f, 30f, 22f, 40f, -30f); q.SoftHand(!left);
            }
            else if (h > 1.5f)
            {
                // high shelf: onto the toes, reach up, the chest follows
                float k = Clamp01((h - 1.5f) / 0.35f);
                q.HipsOffset += new Vector3(0f, 0.05f * k * sc, 0.02f * k * sc);
                q.FootRotL = ActorPose.E(22f * k, -6f, 0f); q.FootRotR = ActorPose.E(22f * k, 6f, 0f);
                q.FootL += new Vector3(0f, 0.05f * k * sc, 0f); q.FootR += new Vector3(0f, 0.05f * k * sc, 0f);
                AddBody(q, HBone.Chest, -6f * k, yaw * 0.3f, side * 5f * k);
                AddBody(q, HBone.Spine, -3f * k, yaw * 0.2f, side * 3f * k);
                AddBody(q, HBone.Neck, -12f * k);
            }
            else
            {
                // chest height: lean toward it and turn the shoulders
                AddBody(q, HBone.Spine, 6f * Clamp01(fwd * 3f), yaw * 0.3f, 0f);
                AddBody(q, HBone.Chest, 4f * Clamp01(fwd * 3f), yaw * 0.3f, 0f);
                q.HipsOffset += new Vector3(0f, 0f, 0.03f * Clamp01(fwd * 3f) * sc);
            }
            p.Overlay(q, w, null, true);
        }

        void HandoverBody(ActorPose p, ActorPoses.Dims d, float sc, bool receiving)
        {
            float w = Ease(_ctlBodyW);
            if (w <= 0.001f) return;
            Vector3 t = _reachLocal;
            float yaw = Mathf.Clamp(Mathf.Atan2(t.x, Mathf.Max(0.05f, t.z)) * Mathf.Rad2Deg, -45f, 45f);
            float lean = Clamp01((new Vector2(t.x, t.z).magnitude - 0.3f * sc) / (0.4f * sc));
            var q = _act; q.CopyFrom(p);
            AddBody(q, HBone.Spine, 5f * lean, yaw * 0.25f, 0f);
            AddBody(q, HBone.Chest, 6f * lean, yaw * 0.3f, 0f);
            AddBody(q, HBone.Head, 10f, 0f, receiving ? 4f : -4f);                 // a small courteous nod over the hands
            q.HipsOffset += new Vector3(0f, -0.01f, 0.04f * lean) * sc;
            BlendShoulder(q, _reachLeft, 1f, 4f, 10f);
            p.Overlay(q, w, null, true);
        }

        void BraceBody(ActorPose p, ActorPoses.Dims d, float sc)
        {
            float w = Ease(_ctlBodyW);
            if (w <= 0.001f || _posture != Posture.Stand) return;
            Vector3 t = _reachLocal;
            Vector3 dir = new Vector3(t.x, 0f, t.z); float dist = dir.magnitude; if (dist > 1e-3f) dir /= dist;
            bool left = _reachLeft;
            var q = _act; q.CopyFrom(p);
            // the weight goes onto the braced arm: lean toward the support (the controller's reach squat is undone: bracing is a
            // lean, not a crouch), the other arm swings out, the knees give a little
            float k = Clamp01(dist / (0.55f * sc));
            q.HipsOffset = new Vector3(q.HipsOffset.x + dir.x * 0.12f * k * sc, Mathf.Max(q.HipsOffset.y, -0.06f * sc), q.HipsOffset.z + dir.z * 0.1f * k * sc);
            q.R[0] = Quaternion.Slerp(q.R[0], ActorPose.Body(dir.z * 14f * k), 0.7f);
            AddBody(q, HBone.Spine, dir.z * 12f * k, Mathf.Atan2(dir.x, Mathf.Max(0.1f, dir.z)) * Mathf.Rad2Deg * 0.2f, -dir.x * 10f * k);
            AddBody(q, HBone.Chest, dir.z * 8f * k, 0f, -dir.x * 8f * k);
            q.SetArm(!left, 30f, ArmIdleOut + 34f, 10f, 40f, 20f, -10f); q.OpenHand(!left);
            AddBody(q, HBone.Head, 8f, 0f, 0f);
            p.Overlay(q, w, null, true);
        }

        void LoadPosture(ActorPose p, float mass, bool front, bool leftOnly, float sc)
        {
            float k = Clamp01((mass - 2.5f) / 15f);
            if (front)
            {
                // a heavy load held in front: lean back from the hips, elbows bent, shoulders pulled down
                AddBody(p, HBone.Spine, -7f * k);
                AddBody(p, HBone.Chest, -5f * k);
                AddBody(p, HBone.Head, 4f * k);
                p.HipsOffset += new Vector3(0f, -0.025f * k, 0.035f * k) * sc;
                BlendShoulder(p, true, k, -4f, 6f); BlendShoulder(p, false, k, -4f, 6f);
            }
            else
            {
                // one-handed (bucket, bag): lean away from the load, that shoulder dropped, the free arm out for balance
                float side = leftOnly ? 1f : -1f;
                AddBody(p, HBone.Spine, 0f, 0f, side * 7f * k);
                AddBody(p, HBone.Chest, 0f, 0f, side * 5f * k);
                p.HipsOffset += new Vector3(-side * 0.02f * k, 0f, 0f) * sc;
                BlendShoulder(p, leftOnly, k, -7f, 0f);
                BlendArm(p, !leftOnly, k * 0.7f, 8f, ArmIdleOut + 22f * k, 0f, 18f);
            }
        }

        // ================================================================ explicit carry load / lean / hand targets
        float _loadKg; bool _loadTwoHands;

        /// <summary>Weight of what the hands carry (kg; 0 = nothing) and whether both hands hold it: the body leans against the
        /// load, shoulders and elbows take the strain, the gait shortens. (PhysicalActionController.SetCarriedObject loads are
        /// picked up automatically; this is for loads the controller does not know.)</summary>
        public void SetCarryLoad(float massKg, bool twoHanded) { _loadKg = Mathf.Max(0f, massKg); _loadTwoHands = twoHanded; }

        struct HandTarget { public Transform T; public Vector3 Offset; public Vector3? Point; public Vector3 Normal; public float Want, W; public bool Lean; }
        readonly HandTarget[] _hand = new HandTarget[2];

        /// <summary>Lean one hand on a surface (table, chair back, rail) at surfacePointWorld; null releases.</summary>
        public void LeanOn(Vector3? surfacePointWorld, Vector3 surfaceNormalWorld, bool left = false)
        {
            int s = left ? 0 : 1;
            if (!surfacePointWorld.HasValue) { _hand[s].Want = 0f; return; }
            _hand[s].T = null; _hand[s].Point = surfacePointWorld; _hand[s].Normal = surfaceNormalWorld.sqrMagnitude > 0.01f ? surfaceNormalWorld.normalized : Vector3.up;
            _hand[s].Want = 1f; _hand[s].Lean = true;
        }

        /// <summary>Generic hand IK: the hand follows target (+localOffset in the target's space) with weight 0..1; null releases.</summary>
        public void SetHandTarget(bool left, Transform target, Vector3 localOffset, float weight = 1f)
        {
            int s = left ? 0 : 1;
            if (target == null) { _hand[s].Want = 0f; return; }
            _hand[s].T = target; _hand[s].Offset = localOffset; _hand[s].Point = null; _hand[s].Want = Clamp01(weight); _hand[s].Lean = false;
        }

        bool HandTargetBusy(bool left) => _hand[left ? 0 : 1].Want > 0.01f || _hand[left ? 0 : 1].W > 0.05f;

        void EvaluateHandTargetBodies(float dt)
        {
            for (int s = 0; s < 2; s++)
            {
                _hand[s].W = Mathf.MoveTowards(_hand[s].W, _hand[s].Want, dt * 3.5f);
                if (!_hand[s].Lean || _hand[s].W <= 0.001f || !_hand[s].Point.HasValue || _posture != Posture.Stand) continue;
                // leaning on a hand: shift the weight toward it, the other hip drops, the shoulder rises a little
                Vector3 t = transform.InverseTransformPoint(_hand[s].Point.Value);
                float w = Ease(_hand[s].W);
                float side = s == 0 ? -1f : 1f;
                float reach = Clamp01(new Vector2(t.x, t.z).magnitude / (0.6f * Height / 1.75f));
                AddBody(_base, HBone.Hips, 0f, 0f, side * -5f * w);
                AddBody(_base, HBone.Spine, 8f * w * reach, t.x * 12f * w, side * 4f * w);
                AddBody(_base, HBone.Chest, 5f * w * reach, t.x * 10f * w, side * 3f * w);
                _base.HipsOffset += new Vector3(side * 0.03f * w, -0.01f * w, 0.03f * w * reach) * (Height / 1.75f);
                BlendShoulder(_base, s == 0, w, 7f, 4f);
            }
        }

        /// <summary>World-space hand IK for hand targets and leans (runs after every animator has posed: partner bones are current).</summary>
        void SolveHandTargets()
        {
            for (int s = 0; s < 2; s++)
            {
                var h = _hand[s];
                if (h.W <= 0.001f) continue;
                Vector3 target;
                if (h.T != null) target = h.T.TransformPoint(h.Offset);
                else if (h.Point.HasValue) target = h.Point.Value;
                else continue;
                HandIK(s == 0, target, h.Lean ? h.Normal : (Vector3?)null, Ease(h.W));
            }
        }

        /// <summary>Two-bone arm IK so the hand anchor (palm) lands on 'target'; with a surface normal the palm lies flat on it.</summary>
        void HandIK(bool left, Vector3 target, Vector3? surfaceNormal, float w)
        {
            if (w <= 0.001f) return;
            HBone ua = left ? HBone.UpperArmL : HBone.UpperArmR, la = left ? HBone.LowerArmL : HBone.LowerArmR, hb = left ? HBone.HandL : HBone.HandR;
            if (!_rig.HasBone(ua) || !_rig.HasBone(la) || !_rig.HasBone(hb)) return;
            Transform a = _rig.Bone(ua), b = _rig.Bone(la), c = _rig.Bone(hb);
            Transform anchor = (left ? _rig.HandAnchorL : _rig.HandAnchorR) ?? c;
            if (surfaceNormal.HasValue)
            {
                // palm onto the surface: rotate the hand so its palm normal opposes the surface normal, fingers pointing forward
                Vector3 palm = c.rotation * (Quaternion.Inverse(RestRot(hb)) * transform.rotation * (left ? Vector3.right : Vector3.left));
                Quaternion flat = Quaternion.FromToRotation(palm, -surfaceNormal.Value) * c.rotation;
                c.rotation = Quaternion.Slerp(c.rotation, flat, w);
            }
            Vector3 pole = -transform.up * 0.6f + transform.right * (left ? -1f : 1f) - transform.forward * 0.25f;
            for (int it = 0; it < 2; it++)
            {
                Vector3 off = anchor.position - c.position;
                TwoBone(a, b, c, Vector3.Lerp(anchor.position, target, w) - off, pole, 1f);
            }
        }

        // ================================================================ dragging bodies
        ActorRig _dragBody; DragGrip _dragGrip; ActorAnimator _dragger; bool _dragged; Transform _dragParentBefore;
        float _dragW; Vector3 _dragHeadVel; Vector2 _dragHead; Vector3 _dragPrevRoot; Vector3 _dragVel;

        /// <summary>Drag 'body' (dead / unconscious) by the armpits (the dragger walks backward, bent over, the heels trailing) or
        /// the ankles / wrists (the dragger walks forward pulling, the body's arms trailing overhead). null stops dragging.
        /// The body is attached to the dragger's root (like a shoulder carry) and posed with trailing, lolling limbs.</summary>
        public void SetDragging(ActorRig body, DragGrip grip)
        {
            Init();
            if (_dragBody == body && _dragGrip == grip) return;
            if (_dragBody != null && _dragBody.Anim != null) _dragBody.Anim.ReleaseFromDragger(this);
            _dragBody = body; _dragGrip = grip;
            if (body != null) { body.Init(); if (body.Anim != null) body.Anim.AttachToDragger(this, grip); }
            if (body != null && _aActive && _action == ActionAnim.Drag) { }   // the Drag action keeps playing; the grip decides the pose
        }

        /// <summary>True while someone drags this body.</summary>
        public bool IsBeingDragged => _dragged;
        public bool IsDragging => _dragBody != null;

        internal void AttachToDragger(ActorAnimator dragger, DragGrip grip)
        {
            Init();
            if (dragger == null) return;
            BlendFromCurrentBones(0.35f);
            _dragger = dragger; _dragged = true; _dragGrip = grip;
            _dragParentBefore = transform.parent;
            transform.SetParent(dragger.transform, true);
            _dragW = 0f; _dragHead = Vector2.zero; _dragHeadVel = Vector3.zero; _dragPrevRoot = dragger.transform.position;
        }

        internal void ReleaseFromDragger(ActorAnimator dragger)
        {
            if (!_dragged) return;
            BlendFromCurrentBones(0.3f);
            _dragged = false; _dragger = null;
            if (_dragParentBefore != null)
            {
                Vector3 p = transform.position;
                transform.SetParent(_dragParentBefore, true);
                transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity;
            }
            else transform.SetParent(null, true);
            _from.CopyFrom(_last); _postureT = 0f;
        }

        /// <summary>The dragger's own pose while dragging (walking or standing still).</summary>
        void EvaluateDragger(float dt, ActorPoses.Dims d)
        {
            if (_dragBody == null || _dead >= 0 || !_conscious) return;
            float sc = Height / 1.75f;
            var q = _act; q.CopyFrom(_base);
            float move = Clamp01(_walkW);
            if (_dragGrip == DragGrip.Armpits)
            {
                // facing the body, walking backward: deep bend, hips low, both hands under the armpits in front
                ShiftRootFlip(q);
                AddBody(q, HBone.Hips, 18f, 0f, 0f);
                AddBody(q, HBone.Spine, 12f, 0f, 0f);
                AddBody(q, HBone.Chest, 8f, 0f, 0f);
                AddBody(q, HBone.Neck, -14f);
                q.HipsOffset += new Vector3(0f, -0.09f * sc, -0.05f * sc);
                q.SetArm(false, 36f, 8f, 20f, 12f, 40f, -10f); q.SetArm(true, 36f, 8f, 20f, 12f, 40f, -10f);
                q.Grip(false); q.Grip(true);
                // effort: a slow heave with every step
                float heave = Mathf.Sin(_phase * Mathf.PI * 4f) * move;
                AddBody(q, HBone.Chest, -3f * heave, 0f, 0f);
                q.HipsOffset += new Vector3(0f, 0.012f * heave * sc, 0f);
            }
            else
            {
                // facing away, pulling the ankles / wrists behind: leaning forward into the load, arms back and low
                AddBody(q, HBone.Spine, 18f + 4f * move, 0f, 0f);
                AddBody(q, HBone.Chest, 8f, 0f, 0f);
                AddBody(q, HBone.Neck, -10f);
                q.HipsOffset += new Vector3(0f, -0.06f * sc, 0.03f * sc);
                q.SetArm(false, -34f, ArmIdleOut + 8f, 30f, 14f, 30f, 10f); q.SetArm(true, -34f, ArmIdleOut + 8f, 30f, 14f, 30f, 10f);
                q.Grip(false); q.Grip(true);
            }
            _base.Overlay(q, 1f, null, true);
        }

        /// <summary>Turns the dragger's body to face backward (the armpit drag walks backward) around its own root.</summary>
        static void ShiftRootFlip(ActorPose q) { MotionKit.ShiftRoot(q, Vector3.zero, Quaternion.Euler(0f, 180f, 0f)); }

        /// <summary>The dragged body's pose: laid out behind the dragger, torso raised at the armpits or legs raised at the ankles,
        /// the free limbs hanging and trailing, the head lolling with the dragger's steps.</summary>
        void EvaluateDragged(float dt, ActorPoses.Dims d)
        {
            if (_dragger == null) { _dragged = false; return; }
            _dragW = Mathf.MoveTowards(_dragW, 1f, dt * 2.5f);
            float sc = Height / 1.75f;
            var dr = _dragger;
            float dsc = dr.Height / 1.75f;
            // dragger velocity (for trailing and head lolling)
            Vector3 rootNow = dr.transform.position;
            Vector3 v = dt > 1e-4f ? (rootNow - _dragPrevRoot) / dt : Vector3.zero; _dragPrevRoot = rootNow;
            _dragVel = Vector3.Lerp(_dragVel, v, 1f - Mathf.Exp(-dt * 6f));
            Vector3 accLocal = transform.InverseTransformDirection(v - _dragVel);
            // head: a damped spring pushed by the dragger's accelerations and steps
            float step = Mathf.Sin(dr._phase * Mathf.PI * 4f) * Clamp01(dr._walkW);
            Vector2 force = new Vector2(accLocal.z * 25f + step * 10f, accLocal.x * 30f);
            _dragHeadVel.x += (force.x - _dragHead.x * 60f - _dragHeadVel.x * 7f) * dt;
            _dragHeadVel.y += (force.y - _dragHead.y * 60f - _dragHeadVel.y * 7f) * dt;
            _dragHead += new Vector2(_dragHeadVel.x, _dragHeadVel.y) * dt;
            var p = _base;
            p.Reset();
            ActorPoses.PostureBase(p, d, Posture.LieBack, 0f, "");
            if (_dragGrip == DragGrip.Armpits)
            {
                // the root sits under the hips, 0.9 m in front of the backward-walking dragger; the head toward the dragger
                transform.localPosition = new Vector3(0f, 0f, -(0.3f * dsc + 0.36f * sc));
                transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                float incline = 45f;
                p.R[0] = ActorPose.Body(-90f + incline);
                p.HipsOffset = new Vector3(0f, 0.1f * sc - d.RestHips.y, 0f);
                p.SetLegFK(true, -incline + 4f, 6f, 6f, -40f, -10f);            // legs on the floor, heels dragging, toes up
                p.SetLegFK(false, -incline + 6f, 4f, 10f, -40f, 12f);
                p.R[(int)HBone.Spine] = ActorPose.Body(6f);
                p.R[(int)HBone.Chest] = ActorPose.Body(4f);
                // head hangs back between the dragger's arms and rolls with the steps
                p.R[(int)HBone.Neck] = ActorPose.Body(-26f + _dragHead.x * 0.5f, 0f, _dragHead.y * 0.6f + 8f);
                p.R[(int)HBone.Head] = ActorPose.Body(-18f + _dragHead.x * 0.6f, 12f, _dragHead.y * 0.7f + 6f);
                // arms lifted at the armpits, forearms dangling to the floor
                p.SetArm(true, 18f, ArmIdleOut + 34f, 10f, 18f + step * 4f, 20f, 20f);
                p.SetArm(false, 16f, ArmIdleOut + 36f, 10f, 22f - step * 4f, 20f, 20f);
                p.SetShoulder(true, 18f, -6f); p.SetShoulder(false, 18f, -6f);
            }
            else
            {
                // ankles (or wrists): the body lies behind the dragger feet first, legs lifted to the hands, arms trailing overhead
                bool wrists = _dragGrip == DragGrip.Wrists;
                float lift = 24f;
                if (wrists)
                {
                    transform.localPosition = new Vector3(0f, 0f, -(0.35f * dsc + 0.62f * sc));
                    transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    p.R[0] = ActorPose.Body(-78f);
                    p.SetArm(true, 160f, 10f, 0f, 6f, 0f, 0f); p.SetArm(false, 160f, 10f, 0f, 6f, 0f, 0f);
                    p.SetLegFK(true, -8f, 5f, 8f, -35f, -10f); p.SetLegFK(false, -6f, 7f, 14f, -35f, 10f);
                    p.R[(int)HBone.Neck] = ActorPose.Body(8f + _dragHead.x * 0.5f, 0f, _dragHead.y * 0.6f);
                    p.R[(int)HBone.Head] = ActorPose.Body(10f + _dragHead.x * 0.5f, 25f, _dragHead.y * 0.6f);
                }
                else
                {
                    transform.localPosition = new Vector3(0f, 0f, -(0.3f * dsc + 0.85f * sc * Mathf.Cos(lift * Mathf.Deg2Rad)));
                    transform.localRotation = Quaternion.identity;
                    p.SetLegFK(true, lift + 2f, 4f, 10f, 20f, -6f); p.SetLegFK(false, lift, 6f, 12f, 20f, 6f);
                    p.SetArm(true, 165f + step * 3f, 22f, 0f, 14f, 30f, 10f);
                    p.SetArm(false, 170f - step * 3f, 18f, 0f, 20f, 30f, 10f);
                    p.R[(int)HBone.Neck] = ActorPose.Body(12f + _dragHead.x * 0.4f, 0f, _dragHead.y * 0.6f);
                    p.R[(int)HBone.Head] = ActorPose.Body(4f + _dragHead.x * 0.5f, 35f + _dragHead.y * 0.4f, 0f);
                }
            }
            p.SetHand(true, 0.35f, 0.3f, 0.4f); p.SetHand(false, 0.35f, 0.3f, 0.4f);
            if (_dead < 0 && !_conscious) { }  // unconscious bodies look the same; the dead keep their face from the kernel
        }

        /// <summary>After both bodies are posed: the dragger's hands on the grip points of the body.</summary>
        void SolveDragGrip()
        {
            if (_dragBody == null || _dragBody.Anim == null || !_dragBody.Anim._dragged) return;
            var b = _dragBody;
            if (_dragGrip == DragGrip.Armpits)
            {
                Transform ch = b.Bone(HBone.Chest), ul = b.Bone(HBone.UpperArmL), ur = b.Bone(HBone.UpperArmR);
                if (ch == null || ul == null || ur == null) return;
                Vector3 down = -ch.up;
                HandIK(false, Vector3.Lerp(ur.position, ch.position, 0.25f) + down * 0.05f - ch.forward * 0.04f, null, 1f);
                HandIK(true, Vector3.Lerp(ul.position, ch.position, 0.25f) + down * 0.05f - ch.forward * 0.04f, null, 1f);
            }
            else
            {
                bool wr = _dragGrip == DragGrip.Wrists;
                Transform l = b.Bone(wr ? HBone.HandL : HBone.FootL), r = b.Bone(wr ? HBone.HandR : HBone.FootR);
                if (l == null || r == null) return;
                // the body's right limb is on the dragger's right when both face the same way (ankles); mirrored for wrists
                HandIK(false, (wr ? l : r).position, null, 1f);
                HandIK(true, (wr ? r : l).position, null, 1f);
            }
        }

        // ================================================================ ragdoll presentation overlay (after PhysicalRagdoll writes the bones)
        sealed class RagdollOverlayState
        {
            public bool Armed; public Vector3 Dir; public bool Back; public float T = -1f; public Vector3 PrevHips; public float Settle;
            public void Begin(Vector3 dirLocal, bool back) { Armed = true; Dir = dirLocal; Back = back; T = -1f; Settle = 0f; }
        }
        readonly RagdollOverlayState _ragdollOverlay = new RagdollOverlayState();

        /// <summary>Called after the ragdoll has written the bones (ActorLateIK, order 310): during the first moments of a
        /// physical fall the arms visibly reach to break it and the hands splay; it fades out as the body comes to rest.</summary>
        internal void RagdollOverlay(float dt)
        {
            var o = _ragdollOverlay;
            if (!PhysicsDriven) { if (o.T >= 0f) { o.T = -1f; o.Armed = false; } return; }
            if (o.T < 0f) { o.T = 0f; if (!o.Armed) { o.Dir = transform.InverseTransformDirection(Vector3.ProjectOnPlane(_vel, Vector3.up)); if (o.Dir.sqrMagnitude < 0.01f) o.Dir = Vector3.forward; o.Dir.Normalize(); o.Back = o.Dir.z < -0.3f; } o.PrevHips = _b[0].position; }
            o.T += dt;
            Vector3 hips = _b[0].position;
            float speed = dt > 1e-4f ? (hips - o.PrevHips).magnitude / dt : 0f; o.PrevHips = hips;
            if (o.T > 0.25f && speed < 0.25f) o.Settle += dt; else o.Settle = Mathf.Max(0f, o.Settle - dt);
            float w = Win(o.T, 0f, 0.08f) * (1f - Win(o.Settle, 0f, 0.35f)) * (1f - Win(o.T, 1.2f, 1.8f));
            if (w <= 0.001f || _rig == null) return;
            // world direction of the fall; the hands reach for the floor ahead of the chest
            Vector3 dirW = transform.TransformDirection(o.Dir);
            Transform chest = _b[(int)HBone.Chest];
            for (int s = 0; s < 2; s++)
            {
                bool left = s == 0;
                Transform ua = _rig.Bone(left ? HBone.UpperArmL : HBone.UpperArmR);
                if (ua == null) continue;
                Vector3 side = Vector3.Cross(Vector3.up, dirW).normalized * (left ? 1f : -1f);
                Vector3 target = o.Back ? chest.position - dirW * 0.35f + side * 0.25f + Vector3.down * 0.55f
                                        : chest.position + dirW * 0.55f + side * 0.2f + Vector3.down * 0.5f;
                target.y = Mathf.Max(target.y, transform.position.y + 0.05f);
                HandIK(left, target, null, w * 0.85f);
            }
            // hands splay open while reaching (fingers are not simulated)
            SplayHands(w);
        }

        void SplayHands(float w)
        {
            if (!HasFingers || w <= 0.001f) return;
            var p = _rx2; p.CopyFrom(_last);
            p.OpenHand(true); p.OpenHand(false);
            PoseFingers(p);
            for (int s = 0; s < 2; s++)
            {
                int b0 = s == 0 ? (int)HBone.ThumbL1 : (int)HBone.ThumbR1;
                for (int k = 0; k < ActorSkeleton.FingersPerHand; k++)
                {
                    int i = b0 + k; if (!_has[i]) continue;
                    // finger local rotation from the canonical hand frame (rest orientation relative to the hand)
                    Quaternion open = Quaternion.Inverse(_restG[_par[i]]) * _O[_par[i]] * _FR[i] * _OInv[i] * _restG[i];
                    _b[i].localRotation = Quaternion.Slerp(_b[i].localRotation, open, w);
                }
            }
        }
    }
}
