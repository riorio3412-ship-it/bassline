using BL23.Game.Characters;
using UnityEngine;

namespace BL23.Game.Physicality
{
    /// <summary>Bone-axis independent IK. Changes rotations only; never stretches bones or assumes a humanoid Avatar.</summary>
    public static class ProceduralContactIK
    {
        public static Vector3 ElbowPosition(Vector3 root, Vector3 target, Vector3 pole,
            float upperLength, float lowerLength)
        {
            upperLength = Mathf.Max(0.0001f, upperLength);
            lowerLength = Mathf.Max(0.0001f, lowerLength);
            Vector3 axis = target - root;
            if (axis.sqrMagnitude < 1e-10f) axis = Vector3.forward;
            float distance = Mathf.Clamp(axis.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.00001f,
                upperLength + lowerLength - 0.00001f);
            axis.Normalize();
            Vector3 bend = Vector3.ProjectOnPlane(pole, axis);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(Vector3.up, axis);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(Vector3.right, axis);
            bend.Normalize();
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float away = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            return root + axis * along + bend * away;
        }

        public static bool SolveTwoBone(Transform upper, Transform lower, Transform end,
            Vector3 target, Vector3 pole, float weight = 1f)
        {
            if (upper == null || lower == null || end == null || weight <= 0f) return false;
            Vector3 root = upper.position;
            float a = Vector3.Distance(root, lower.position), b = Vector3.Distance(lower.position, end.position);
            if (a < 0.0001f || b < 0.0001f) return false;
            target = Vector3.Lerp(end.position, target, Mathf.Clamp01(weight));
            Vector3 direction = target - root;
            if (direction.sqrMagnitude < 1e-10f) direction = end.position - root;
            if (direction.sqrMagnitude < 1e-10f) direction = Vector3.forward;
            float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(a - b) + 0.00001f, a + b - 0.00001f);
            Vector3 reachable = root + direction.normalized * distance;
            Vector3 elbow = ElbowPosition(root, reachable, pole, a, b);
            Quaternion handRotation = end.rotation;
            upper.rotation = Quaternion.FromToRotation(lower.position - root, elbow - root) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, reachable - lower.position) * lower.rotation;
            end.rotation = handRotation;
            return (end.position - target).sqrMagnitude < 0.0025f;
        }

        public static float SolveHand(ActorRig rig, bool left, Vector3 contact, Vector3 normal,
            float weight, float surfaceRotationWeight = 0f)
        {
            HBone upperBone = left ? HBone.UpperArmL : HBone.UpperArmR;
            HBone lowerBone = left ? HBone.LowerArmL : HBone.LowerArmR;
            HBone handBone = left ? HBone.HandL : HBone.HandR;
            if (rig == null || !rig.HasBone(upperBone) || !rig.HasBone(lowerBone) || !rig.HasBone(handBone)) return float.PositiveInfinity;
            Transform upper = rig.Bone(upperBone), lower = rig.Bone(lowerBone), hand = rig.Bone(handBone);
            Transform anchor = (left ? rig.HandAnchorL : rig.HandAnchorR) ?? hand;
            Vector3 blendedContact = Vector3.Lerp(anchor.position, contact, Mathf.Clamp01(weight));
            Vector3 pole = -rig.transform.up + rig.transform.right * (left ? -0.8f : 0.8f) - rig.transform.forward * 0.1f;
            if (surfaceRotationWeight > 0f && normal.sqrMagnitude > 0.1f && rig.Anim != null)
            {
                Vector3 localPalm = Quaternion.Inverse(rig.Anim.RestRot(handBone)) * rig.transform.right * (left ? 1f : -1f);
                Quaternion surface = Quaternion.FromToRotation(hand.rotation * localPalm, -normal.normalized) * hand.rotation;
                hand.rotation = Quaternion.Slerp(hand.rotation, surface, surfaceRotationWeight * weight);
            }
            // HandAnchor may be offset from the wrist. Re-evaluate after rotating the arm, while preserving the wrist orientation.
            for (int pass = 0; pass < 2; pass++)
                SolveTwoBone(upper, lower, hand, blendedContact - (anchor.position - hand.position), pole);
            return Vector3.Distance(anchor.position, contact);
        }
    }
}
