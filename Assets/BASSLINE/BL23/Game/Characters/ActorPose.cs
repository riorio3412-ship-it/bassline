using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>
    /// A pose in canonical space: every bone rotation is expressed relative to the canonical "I-pose"
    /// (spine up, arms hanging straight down, legs straight, +Z forward). R[Hips] is the hips rotation
    /// in actor root space; every other entry is local to its parent. Retargeted per actor by ActorAnimator.
    /// </summary>
    public sealed class ActorPose
    {
        public const int N = ActorSkeleton.Count;
        public readonly Quaternion[] R = new Quaternion[N];
        public Vector3 HipsOffset;          // offset of the hips from their rest position (root space)
        public float LegIK;                 // 0 = FK legs, 1 = feet pinned to FootL/FootR
        public Vector3 FootL, FootR;        // ankle targets (root space, absolute)
        public Quaternion FootRotL = Quaternion.identity, FootRotR = Quaternion.identity; // canonical global foot rotations
        public Vector3 RootOffset;          // extra offset of the whole body (lying poses), root space
        public Quaternion RootRot = Quaternion.identity;
        /// <summary>Hand curls 0 (open) .. 1 (fist): thumb, index, middle-pinky group for the left hand, then the right hand.</summary>
        public readonly float[] Finger = new float[6];
        public static readonly float[] RelaxedHand = { 0.22f, 0.2f, 0.3f };

        public ActorPose() { Reset(); }

        public void Reset()
        {
            for (int i = 0; i < N; i++) R[i] = Quaternion.identity;
            HipsOffset = Vector3.zero; LegIK = 0f; FootL = FootR = Vector3.zero;
            FootRotL = FootRotR = Quaternion.identity; RootOffset = Vector3.zero; RootRot = Quaternion.identity;
            for (int s = 0; s < 2; s++) for (int k = 0; k < 3; k++) Finger[s * 3 + k] = RelaxedHand[k];
        }

        public void CopyFrom(ActorPose p)
        {
            for (int i = 0; i < N; i++) R[i] = p.R[i];
            HipsOffset = p.HipsOffset; LegIK = p.LegIK; FootL = p.FootL; FootR = p.FootR;
            FootRotL = p.FootRotL; FootRotR = p.FootRotR; RootOffset = p.RootOffset; RootRot = p.RootRot;
            for (int k = 0; k < 6; k++) Finger[k] = p.Finger[k];
        }

        /// <summary>this = lerp(a, b, t) over all channels.</summary>
        public void Blend(ActorPose a, ActorPose b, float t)
        {
            t = Mathf.Clamp01(t);
            for (int i = 0; i < N; i++) R[i] = Quaternion.Slerp(a.R[i], b.R[i], t);
            HipsOffset = Vector3.Lerp(a.HipsOffset, b.HipsOffset, t);
            LegIK = Mathf.Lerp(a.LegIK, b.LegIK, t);
            FootL = Vector3.Lerp(a.FootL, b.FootL, t);
            FootR = Vector3.Lerp(a.FootR, b.FootR, t);
            FootRotL = Quaternion.Slerp(a.FootRotL, b.FootRotL, t);
            FootRotR = Quaternion.Slerp(a.FootRotR, b.FootRotR, t);
            RootOffset = Vector3.Lerp(a.RootOffset, b.RootOffset, t);
            RootRot = Quaternion.Slerp(a.RootRot, b.RootRot, t);
            for (int k = 0; k < 6; k++) Finger[k] = Mathf.Lerp(a.Finger[k], b.Finger[k], t);
        }

        /// <summary>Blend only masked bones of 'b' over this pose.</summary>
        public void Overlay(ActorPose b, float t, bool[] mask, bool hipsAndLegs)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f) return;
            for (int i = 0; i < N; i++) if (mask == null || mask[i]) R[i] = Quaternion.Slerp(R[i], b.R[i], t);
            if (mask == null || mask[(int)HBone.HandL]) for (int k = 0; k < 3; k++) Finger[k] = Mathf.Lerp(Finger[k], b.Finger[k], t);
            if (mask == null || mask[(int)HBone.HandR]) for (int k = 3; k < 6; k++) Finger[k] = Mathf.Lerp(Finger[k], b.Finger[k], t);
            if (hipsAndLegs)
            {
                HipsOffset = Vector3.Lerp(HipsOffset, b.HipsOffset, t);
                LegIK = Mathf.Lerp(LegIK, b.LegIK, t);
                FootL = Vector3.Lerp(FootL, b.FootL, t);
                FootR = Vector3.Lerp(FootR, b.FootR, t);
                FootRotL = Quaternion.Slerp(FootRotL, b.FootRotL, t);
                FootRotR = Quaternion.Slerp(FootRotR, b.FootRotR, t);
                RootOffset = Vector3.Lerp(RootOffset, b.RootOffset, t);
                RootRot = Quaternion.Slerp(RootRot, b.RootRot, t);
            }
        }

        // ---------------------------------------------------------------- authoring helpers (degrees)
        public static Quaternion E(float x, float y, float z) => Quaternion.Euler(x, y, z);

        /// <summary>Arm (upper arm) rotation: fwd = swing forward, outward = abduction away from the body, twist = inward roll.</summary>
        public static Quaternion Arm(bool left, float fwd, float outward, float twist = 0f)
        {
            return Quaternion.AngleAxis(-fwd, Vector3.right) * Quaternion.AngleAxis(left ? -outward : outward, Vector3.forward) * Quaternion.AngleAxis(left ? twist : -twist, Vector3.up);
        }

        /// <summary>Elbow flexion (forearm forward/up) + pronation twist.</summary>
        public static Quaternion Elbow(bool left, float flex, float twist = 0f)
        {
            return Quaternion.AngleAxis(-flex, Vector3.right) * Quaternion.AngleAxis(left ? twist : -twist, Vector3.up);
        }

        /// <summary>Wrist: flex (palm toward forearm front), side deviation.</summary>
        public static Quaternion Wrist(bool left, float flex, float side = 0f, float twist = 0f)
        {
            return Quaternion.AngleAxis(-flex, Vector3.right) * Quaternion.AngleAxis(left ? -side : side, Vector3.forward) * Quaternion.AngleAxis(left ? twist : -twist, Vector3.up);
        }

        /// <summary>Thigh: fwd = hip flexion, outward = abduction.</summary>
        public static Quaternion Thigh(bool left, float fwd, float outward = 0f, float twist = 0f)
        {
            return Quaternion.AngleAxis(-fwd, Vector3.right) * Quaternion.AngleAxis(left ? -outward : outward, Vector3.forward) * Quaternion.AngleAxis(left ? -twist : twist, Vector3.up);
        }

        /// <summary>Knee flexion (shin goes back).</summary>
        public static Quaternion Knee(float flex) => Quaternion.AngleAxis(flex, Vector3.right);

        /// <summary>Spine/neck/head: pitch forward (+), yaw right (+), lean left (+).</summary>
        public static Quaternion Body(float pitchFwd, float yawRight = 0f, float leanLeft = 0f)
        {
            return Quaternion.AngleAxis(yawRight, Vector3.up) * Quaternion.AngleAxis(pitchFwd, Vector3.right) * Quaternion.AngleAxis(leanLeft, Vector3.forward);
        }

        public void SetArm(bool left, float fwd, float outward, float twist, float elbow, float elbowTwist = 0f, float wristFlex = 0f)
        {
            int ua = left ? (int)HBone.UpperArmL : (int)HBone.UpperArmR;
            R[ua] = Arm(left, fwd, outward, twist);
            R[ua + 1] = Elbow(left, elbow, elbowTwist);
            R[ua + 2] = Wrist(left, wristFlex);
        }

        public void SetShoulder(bool left, float up, float fwd = 0f)
        {
            // raise (shrug) = rotate the clavicle up
            R[left ? (int)HBone.ShoulderL : (int)HBone.ShoulderR] = Quaternion.AngleAxis(left ? -up : up, Vector3.forward) * Quaternion.AngleAxis(left ? fwd : -fwd, Vector3.up);
        }

        /// <summary>Hand shape: curls 0 (open, straight) .. 1 (closed) for thumb, index and the middle-pinky group.</summary>
        public void SetHand(bool left, float thumb, float index, float rest)
        {
            int o = left ? 0 : 3;
            Finger[o] = thumb; Finger[o + 1] = index; Finger[o + 2] = rest;
        }
        public void Fist(bool left) => SetHand(left, 0.75f, 0.95f, 1f);
        public void PointHand(bool left) => SetHand(left, 0.7f, 0.02f, 0.95f);
        public void OpenHand(bool left) => SetHand(left, 0.05f, 0.04f, 0.06f);
        public void Grip(bool left) => SetHand(left, 0.55f, 0.6f, 0.68f);
        public void SoftHand(bool left) => SetHand(left, 0.3f, 0.35f, 0.45f);

        public void SetLegFK(bool left, float thighFwd, float thighOut, float knee, float anklePitch = 0f, float thighTwist = 0f)
        {
            int ul = left ? (int)HBone.UpperLegL : (int)HBone.UpperLegR;
            R[ul] = Thigh(left, thighFwd, thighOut, thighTwist);
            R[ul + 1] = Knee(knee);
            R[ul + 2] = Quaternion.AngleAxis(anklePitch, Vector3.right);
        }
    }
}
