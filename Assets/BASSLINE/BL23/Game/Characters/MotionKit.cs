using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>Timing curves and pose helpers shared by the motion layers (reactions, attacks, paired acts, ambient).</summary>
    public static class MotionKit
    {
        public static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
        /// <summary>Smoothstep of x (0..1).</summary>
        public static float Ease(float x) { x = Clamp01(x); return x * x * (3f - 2f * x); }
        /// <summary>Fast start, soft landing (strike follow-through, recoils settling).</summary>
        public static float EaseOut(float x) { x = Clamp01(x); float i = 1f - x; return 1f - i * i * i; }
        /// <summary>Slow start, fast end (a strike accelerating into the target).</summary>
        public static float EaseIn(float x) { x = Clamp01(x); return x * x * x; }
        /// <summary>0 before a, 1 after b, eased between.</summary>
        public static float Win(float u, float a, float b) => Ease((u - a) / Mathf.Max(1e-4f, b - a));
        /// <summary>Rises over [a,b], holds, falls over [c,d].</summary>
        public static float Bell(float u, float a, float b, float c, float d) => Win(u, a, b) * (1f - Win(u, c, d));
        /// <summary>Impulse response: 0 at t=0, peak 1 at t=peak, decays after (a blow's jolt).</summary>
        public static float Impulse(float t, float peak)
        {
            if (t <= 0f) return 0f;
            float x = t / Mathf.Max(1e-3f, peak);
            return x * Mathf.Exp(1f - x);
        }
        /// <summary>Damped oscillation around 0 starting at 1 (settle wobble after an impact).</summary>
        public static float Wobble(float t, float freq, float damp) => Mathf.Exp(-damp * t) * Mathf.Cos(t * freq * 2f * Mathf.PI);
        public static float Noise(float t, float seed) => Mathf.PerlinNoise(t, seed) * 2f - 1f;

        /// <summary>p.R[b] = p.R[b] * Body(pitch, yaw, lean) (local additive on a spine-type bone).</summary>
        public static void AddBody(ActorPose p, HBone b, float pitchFwd, float yawRight = 0f, float leanLeft = 0f)
        {
            p.R[(int)b] = p.R[(int)b] * ActorPose.Body(pitchFwd, yawRight, leanLeft);
        }

        /// <summary>Blend an arm toward an absolute SetArm target with weight w.</summary>
        public static void BlendArm(ActorPose p, bool left, float w, float fwd, float outward, float twist, float elbow, float elbowTwist = 0f, float wristFlex = 0f)
        {
            if (w <= 0f) return;
            int ua = left ? (int)HBone.UpperArmL : (int)HBone.UpperArmR;
            w = Clamp01(w);
            p.R[ua] = Quaternion.Slerp(p.R[ua], ActorPose.Arm(left, fwd, outward, twist), w);
            p.R[ua + 1] = Quaternion.Slerp(p.R[ua + 1], ActorPose.Elbow(left, elbow, elbowTwist), w);
            p.R[ua + 2] = Quaternion.Slerp(p.R[ua + 2], ActorPose.Wrist(left, wristFlex), w);
        }

        public static void BlendHand(ActorPose p, bool left, float w, float thumb, float index, float rest)
        {
            int o = left ? 0 : 3; w = Clamp01(w);
            p.Finger[o] = Mathf.Lerp(p.Finger[o], thumb, w);
            p.Finger[o + 1] = Mathf.Lerp(p.Finger[o + 1], index, w);
            p.Finger[o + 2] = Mathf.Lerp(p.Finger[o + 2], rest, w);
        }

        public static void BlendShoulder(ActorPose p, bool left, float w, float up, float fwd = 0f)
        {
            int s = left ? (int)HBone.ShoulderL : (int)HBone.ShoulderR;
            var q = Quaternion.AngleAxis(left ? -up : up, Vector3.forward) * Quaternion.AngleAxis(left ? fwd : -fwd, Vector3.up);
            p.R[s] = Quaternion.Slerp(p.R[s], q, Clamp01(w));
        }

        /// <summary>Moves / turns the whole body inside the root frame (paired alignment, drag offsets) keeping feet targets
        /// consistent: offset and yaw are root-space.</summary>
        public static void ShiftRoot(ActorPose p, Vector3 offset, Quaternion rot)
        {
            p.RootRot = rot * p.RootRot;
            p.RootOffset = offset + rot * p.RootOffset;
            p.FootL = offset + rot * p.FootL;
            p.FootR = offset + rot * p.FootR;
        }

        /// <summary>A leg IK stance in root space: both feet planted at rest (with offsets) and the hips moved.</summary>
        public static void Stance(ActorPose p, ActorPoses.Dims d, Vector3 footL, Vector3 footR, Vector3 hipsOffset)
        {
            p.LegIK = 1f;
            p.FootL = d.FootL + footL; p.FootR = d.FootR + footR;
            p.HipsOffset = hipsOffset;
        }

        /// <summary>Two-bone IK on transforms (rotations only). Keeps the end bone's world rotation.</summary>
        public static void TwoBone(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole, float weight)
        {
            if (a == null || b == null || c == null || weight <= 0.0005f) return;
            Quaternion endRot = c.rotation;
            Vector3 pa = a.position, pb = b.position, pc = c.position;
            float la = (pb - pa).magnitude, lb = (pc - pb).magnitude;
            if (la < 1e-4f || lb < 1e-4f) return;
            target = Vector3.Lerp(pc, target, Clamp01(weight));
            Vector3 to = target - pa;
            float dist = Mathf.Clamp(to.magnitude, Mathf.Abs(la - lb) + 1e-3f, la + lb - 1e-3f);
            Vector3 dir = to.sqrMagnitude > 1e-8f ? to.normalized : (pc - pa).normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(pb - pa, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(Vector3.up, dir);
            bend.Normalize();
            float along = (la * la - lb * lb + dist * dist) / (2f * dist);
            float away = Mathf.Sqrt(Mathf.Max(0f, la * la - along * along));
            Vector3 elbow = pa + dir * along + bend * away;
            Vector3 hand = pa + dir * dist;
            a.rotation = Quaternion.FromToRotation(pb - pa, elbow - pa) * a.rotation;
            pb = b.position; pc = c.position;
            b.rotation = Quaternion.FromToRotation(pc - pb, hand - pb) * b.rotation;
            c.rotation = endRot;
        }
    }
}
