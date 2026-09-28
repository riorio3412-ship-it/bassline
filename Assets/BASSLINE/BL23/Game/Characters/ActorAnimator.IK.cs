using BL23.Game.Physicality;
using UnityEngine;
using static BL23.Game.Characters.MotionKit;

namespace BL23.Game.Characters
{
    /// <summary>Pose post-processing: additive hit offsets (before IK), fingers, and the world-space IK passes that run on the
    /// written transforms (finger to lips, bound wrists, aim, hands on targets, grips on partners, the ragdoll overlay).</summary>
    public partial class ActorAnimator
    {
        // ---------------------------------------------------------------- hit offsets (ActorPhysics hit springs)
        readonly Quaternion[] _hitOff = new Quaternion[ActorSkeleton.Count];
        readonly float[] _hitStamp = new float[ActorSkeleton.Count];
        bool _anyHit;

        /// <summary>Additive local rotation for one bone (hit springs), written every frame by ActorPhysics and applied by the
        /// animator BEFORE hand and foot IK (so grips, the rail and planted feet stay put). Canonical local frame of the bone
        /// (x right, y along the bone / up the spine, z forward); applied as R[b] = R[b] * localOffset. An offset that is not
        /// written again fades out within ~0.1 s.</summary>
        public void SetHitOffset(HBone bone, Quaternion localOffset)
        {
            int i = (int)bone;
            if (i < 0 || i >= ActorSkeleton.Count) return;
            _hitOff[i] = localOffset; _hitStamp[i] = _time; _anyHit = true;
        }

        void ApplyHitOffsets(ActorPose p)
        {
            if (!_anyHit) return;
            bool any = false;
            float dt = Mathf.Max(1e-3f, Time.deltaTime);
            for (int i = 0; i < ActorSkeleton.Count; i++)
            {
                var q = _hitOff[i];
                if (q.w == 0f && q.x == 0f && q.y == 0f && q.z == 0f) continue;   // never set
                if (_time - _hitStamp[i] > 0.05f) { q = Quaternion.Slerp(q, Quaternion.identity, 1f - Mathf.Exp(-25f * dt)); _hitOff[i] = q; }
                if (Quaternion.Angle(q, Quaternion.identity) < 0.05f) { _hitOff[i] = new Quaternion(0, 0, 0, 0); continue; }
                any = true;
                p.R[i] = p.R[i] * q;
            }
            _anyHit = any;
        }

        // ---------------------------------------------------------------- fingers
        void PoseFingers(ActorPose p)
        {
            for (int s = 0; s < 2; s++)
            {
                int b0 = s == 0 ? (int)HBone.ThumbL1 : (int)HBone.ThumbR1;
                float thumb = p.Finger[s * 3], index = p.Finger[s * 3 + 1], rest = p.Finger[s * 3 + 2];
                // thumb: CMC swings across the palm (opposition), MCP and IP bend
                Vector3 ta = _fingerAxis[s * 5], fa = _fingerAxis[s * 5 + 1];
                _FR[b0] = Quaternion.AngleAxis(thumb * 30f, ta) * Quaternion.AngleAxis(thumb * 18f, fa);
                _FR[b0 + 1] = Quaternion.AngleAxis(thumb * 38f, ta);
                _FR[b0 + 2] = Quaternion.AngleAxis(thumb * 55f, ta);
                for (int d = 1; d < 5; d++)
                {
                    float c = d == 1 ? index : Mathf.Clamp01(rest + (d - 2) * 0.05f);
                    Vector3 ax = _fingerAxis[s * 5 + d];
                    int b = b0 + d * 3;
                    _FR[b] = _spreadFix[s * 5 + d] * Quaternion.AngleAxis(c * 72f, ax);
                    _FR[b + 1] = Quaternion.AngleAxis(c * 95f, ax);
                    _FR[b + 2] = Quaternion.AngleAxis(c * 62f, ax);
                }
            }
        }

        // ---------------------------------------------------------------- companions (partner IK after every animator; ragdoll overlay)
        bool _companions;
        void InitIK() { }
        void EnsureCompanions()
        {
            if (_companions) return;
            _companions = true;
            var late = GetComponent<ActorLateIK>(); if (late == null) late = gameObject.AddComponent<ActorLateIK>(); late.Anim = this;
            var post = GetComponent<ActorPostRagdoll>(); if (post == null) post = gameObject.AddComponent<ActorPostRagdoll>(); post.Anim = this;
        }

        void ResetFeet() { _plantL.Valid = _plantR.Valid = false; _plantL.Stepping = _plantR.Stepping = false; }

        /// <summary>The ragdoll took the bones: transient motions end (they must not resume when the body gets up).</summary>
        void OnPhysicsDriven()
        {
            _fall.Active = _hit.Active = _stag.Active = _startle.Active = _atk.Active = _getUp.Active = _def.Active = false;
            _pass.Active = _bump.Active = false;
            ResetFeet();
        }

        // ---------------------------------------------------------------- self IK right after the pose is written
        void PostIK(float dt)
        {
            if (_gActive && _gesture == Gesture.FingerToLips && _dead < 0) FingerToLipsIK(_gW);
            SolveRestraintIK();
            AimArm();
        }

        /// <summary>A strike in progress keeps the animated arm even while PhysicalActionController runs its Swing contact window
        /// (the window and its events are untouched; only the drawn arm is the attack beat's).</summary>
        void KeepStrikeArm()
        {
            if (!_atk.Active || _physicalActions == null || _physicalActions.CurrentAction != PhysicalActionKind.Swing) return;
            for (int s = 0; s < 2; s++)
            {
                int ua = s == 0 ? (int)HBone.UpperArmL : (int)HBone.UpperArmR;
                for (int k = 0; k < 3; k++) { int i = ua + k; if (_has[i]) _b[i].localRotation = Quaternion.Inverse(_W[_par[i]]) * _W[i]; }
            }
        }

        /// <summary>Pistol aim: the extended arm points exactly at the aim point.</summary>
        void AimArm()
        {
            if (_readyW < 0.05f || _readyClass != WeaponClass.Pistol || !_aimWorld.HasValue || _dead >= 0) return;
            Transform ua = _rig.Bone(HBone.UpperArmR), hd = _rig.Bone(HBone.HandR);
            if (ua == null || hd == null) return;
            Vector3 cur = hd.position - ua.position, want = _aimWorld.Value - ua.position;
            if (cur.sqrMagnitude < 1e-6f || want.sqrMagnitude < 1e-4f) return;
            float w = Ease(_readyW) * (_atk.Active && _atk.Kind == ActionAnim.Shoot ? 0.6f : 1f);
            ua.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, want), w) * ua.rotation;
        }

        /// <summary>Partner-dependent IK after every animator has posed (ActorLateIK, order 55): hand targets on other bodies,
        /// the dragger's grip, the paired contact points, a victim catching the attacker's wrist.</summary>
        internal void LateIK(float dt)
        {
            if (!_init || PhysicsDriven || _lod >= 2) return;
            SolveHandTargets();
            SolveDragGrip();
            SolvePairedContacts();
            if (_grabWristW > 0.01f && _partner != null)
            {
                var wr = _partner.Bone(HBone.HandR);
                if (wr != null) HandIK(true, wr.position, null, _grabWristW);
            }
        }

        /// <summary>
        /// Finger-to-lips: two-bone IK on the right arm (after the pose is written) so the index fingertip lands on the
        /// lips of this rig whatever its proportions; the hand keeps the gesture's orientation.
        /// </summary>
        void FingerToLipsIK(float w)
        {
            if (w <= 0.001f || !_rig.HasBone(HBone.UpperArmR) || !_rig.HasBone(HBone.LowerArmR)) return;
            Transform ua = _rig.Bone(HBone.UpperArmR), la = _rig.Bone(HBone.LowerArmR), hd = _rig.Bone(HBone.HandR), head = _rig.Bone(HBone.Head);
            var tipT = _rig.FingerTipAnchor(false, 1);
            float s = Height / 1.75f;
            Vector3 eye = _rig.EyeAnchor != null ? _rig.EyeAnchor.position : head.position + transform.up * 0.08f * s;
            Vector3 lips = eye - head.up * (0.058f * s) + head.forward * (0.05f * s);
            Vector3 pole = (-transform.up + transform.right * 0.7f + transform.forward * 0.15f).normalized;
            for (int it = 0; it < 2; it++)
            {
                Vector3 tip = tipT != null && tipT != hd ? tipT.position : hd.position + (hd.position - la.position).normalized * 0.09f * s;
                Vector3 wristTarget = Vector3.Lerp(hd.position, lips - (tip - hd.position), w);
                TwoBoneIK(ua, la, hd, wristTarget, pole);
            }
        }

        static void TwoBoneIK(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole)
        {
            Vector3 pa = a.position, pb = b.position, pc = c.position;
            float la = (pb - pa).magnitude, lb = (pc - pb).magnitude;
            if (la < 1e-4f || lb < 1e-4f) return;
            float dist = Mathf.Clamp((target - pa).magnitude, Mathf.Abs(la - lb) + 1e-3f, la + lb - 1e-3f);
            float cur = Vector3.Angle(pb - pa, pc - pb);
            float want = 180f - Mathf.Acos(Mathf.Clamp((la * la + lb * lb - dist * dist) / (2f * la * lb), -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 axis = Vector3.Cross(pb - pa, pc - pb);
            if (axis.sqrMagnitude < 1e-10f) axis = Vector3.Cross(pb - pa, pole);
            axis.Normalize();
            b.rotation = Quaternion.AngleAxis(want - cur, axis) * b.rotation;
            pc = c.position;
            a.rotation = Quaternion.FromToRotation(pc - pa, target - pa) * a.rotation;
            pb = b.position;
            Vector3 n = (target - pa).normalized;
            Vector3 e = Vector3.ProjectOnPlane(pb - pa, n), p = Vector3.ProjectOnPlane(pole, n);
            if (e.sqrMagnitude > 1e-8f && p.sqrMagnitude > 1e-8f) a.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(e, p, n), n) * a.rotation;
        }

        static bool Holding(Transform t)
        {
            if (t == null) return false;
            for (int i = 0; i < t.childCount; i++) if (t.GetChild(i).gameObject.activeSelf) return true;
            return false;
        }
    }

}
