using BL23.Game.Characters;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Cloth folds for the procedural garments: diagonal creases at the inside of the elbows / behind the knees and soft
    /// bunching toward the cuffs and hems, carved into the sleeve / trouser shells as a displacement.
    /// </summary>
    public sealed partial class ProcBuilder
    {
        public static bool ClothFolds = true;

        static float Ridge(float x) { float f = x - Mathf.Floor(x); return 1f - Mathf.Abs(f * 2f - 1f); } // triangle 0..1

        /// <summary>Fold height (-1..1) on a limb shell. a = top joint, e = middle joint, w = end joint, front = direction of the crease side.</summary>
        static float LimbFolds(Vector3 p, Vector3 a, Vector3 e, Vector3 w, Vector3 front, float seed)
        {
            Vector3 d = (w - a); float len = d.magnitude; if (len < 1e-4f) return 0f; d /= len;
            float t = Vector3.Dot(p - a, d) / len;
            if (t < 0.15f || t > 1.05f) return 0f;
            Vector3 c = a + d * (t * len);
            Vector3 fwd = Vector3.ProjectOnPlane(front, d).normalized, side = Vector3.Cross(d, fwd);
            Vector3 r = p - c;
            float ang = Mathf.Atan2(Vector3.Dot(r, side), Vector3.Dot(r, fwd));   // 0 on the crease side
            float te = Vector3.Dot(e - a, d) / len;
            float along = t * len;
            // joint creases: diagonal, strongest on the crease side, fading around the limb
            float joint = Mathf.Exp(-Mathf.Pow((t - te) / 0.11f, 2f)) * (0.35f + 0.65f * Mathf.Pow(0.5f + 0.5f * Mathf.Cos(ang), 1.5f));
            float diag = Ridge(along / 0.024f + ang * 0.22f + seed) ;
            float crease = (diag - 0.5f) * 2f;
            // bunching toward the end (cuff / hem): rings with a slight wobble
            float bunch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.98f, t));
            float ring = (Ridge(along / 0.018f + 0.15f * Mathf.Sin(ang * 2f + seed * 3f)) - 0.5f) * 2f;
            return joint * crease + bunch * ring * 0.55f;
        }

        Sdf SleeveFolds(int s, Sdf shell, float amp = 0.0022f)
        {
            if (!ClothFolds) return shell;
            Vector3 a = ArmA(s), e = ArmE(s), w = ArmW(s);
            float seed = s * 0.37f + (def.Id.GetHashCode() & 7) * 0.11f;
            return Sdf.Displace(shell, p => LimbFolds(p, a, e, w, Vector3.forward, seed), amp);
        }

        Sdf LegFolds(int s, Sdf shell, float amp = 0.0025f)
        {
            if (!ClothFolds) return shell;
            HBone ul = s == 0 ? HBone.UpperLegL : HBone.UpperLegR;
            Vector3 a = J(ul), e = J(ul + 1), w = J(ul + 2);
            float seed = s * 0.53f + (def.Id.GetHashCode() & 7) * 0.07f;
            // behind the knee is the crease side
            return Sdf.Displace(shell, p => LimbFolds(p, a, e, w, Vector3.back, seed), amp);
        }
    }
}
