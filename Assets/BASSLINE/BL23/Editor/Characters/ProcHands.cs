using System.Collections.Generic;
using System.Linq;
using BL23.Game.Characters;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Finger rig of the procedural hands: the finger polylines the hand SDF is built from (knuckle, two middle joints,
    /// tip) become the three bones of each digit, and the hand mesh is weighted along each digit (same skeleton as the
    /// scanned actors, so the hand shapes of gestures and actions - fists, pointing, grips - work on everyone).
    /// </summary>
    public sealed partial class ProcBuilder
    {
        readonly List<Vector3>[] fingerLines = new List<Vector3>[10];   // side * 5 + digit (0 thumb .. 4 little)
        readonly float[] fingerRad = new float[10];

        void RecordFinger(int side, int digit, IList<Vector3> pts, float radius)
        {
            fingerLines[side * 5 + digit] = pts.ToList();
            fingerRad[side * 5 + digit] = radius;
        }

        /// <summary>Moves the finger bones onto the modelled fingers (called after the hands are built).</summary>
        void PlaceFingerBones()
        {
            for (int s = 0; s < 2; s++)
                for (int d = 0; d < 5; d++)
                {
                    var l = fingerLines[s * 5 + d]; if (l == null || l.Count < 4) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int b = (int)ActorSkeleton.Digit(s == 0, d, k);
                        P.Joint[b] = l[k];
                        if (b < M.Bones.Count) M.Bones[b].Pos = l[k];
                    }
                    P.FingerTip[s * 5 + d] = l[l.Count - 1];
                }
            CharacterBaker.ExtendedTails(M.Bones, P);
        }

        /// <summary>Distance to a digit polyline and the chain parameter (0..1 first segment, 1..2 second, 2..3 third; negative before the knuckle).</summary>
        static float DigitDist(Vector3 p, List<Vector3> l, out float par)
        {
            float best = float.MaxValue; par = 0f;
            for (int i = 0; i + 1 < l.Count && i < 3; i++)
            {
                Vector3 ab = l[i + 1] - l[i]; float l2 = Mathf.Max(1e-10f, ab.sqrMagnitude);
                float t = Vector3.Dot(p - l[i], ab) / l2;
                float tc = i == 0 ? Mathf.Min(t, 1f) : i == 2 ? Mathf.Max(t, 0f) : Mathf.Clamp01(t);
                float d = (p - (l[i] + ab * Mathf.Clamp01(t))).magnitude;
                if (d < best) { best = d; par = i + tc; }
            }
            return best;
        }

        /// <summary>Skin rule of a procedural hand: forearm / hand blend at the wrist, every digit along its own joints.</summary>
        SkinRule HandRule(int s)
        {
            int la = s == 0 ? Bi(HBone.LowerArmL) : Bi(HBone.LowerArmR), ha = s == 0 ? Bi(HBone.HandL) : Bi(HBone.HandR);
            bool left = s == 0;
            return new SkinRule
            {
                Custom = p =>
                {
                    var l = new List<KeyValuePair<int, float>>();
                    var bl = M.Bones[la]; var bh = M.Bones[ha];
                    float dl = CharMesher.SegDist(p, bl.Pos, bl.Tail), dh = CharMesher.SegDist(p, bh.Pos, bh.Tail);
                    float wl = 1f / Mathf.Pow(dl + 0.004f, 7f), wh = 1f / Mathf.Pow(dh + 0.004f, 7f);
                    float kl = wl / (wl + wh);
                    int bestD = -1; float bestDist = float.MaxValue, bestPar = 0f;
                    for (int d = 0; d < 5; d++)
                    {
                        var line = fingerLines[s * 5 + d]; if (line == null) continue;
                        float dist = DigitDist(p, line, out float par) - fingerRad[s * 5 + d];
                        if (dist < bestDist) { bestDist = dist; bestD = d; bestPar = par; }
                    }
                    float fk = 0f; var seg = new float[3];
                    if (bestD >= 0 && bestDist < 0.006f)
                    {
                        fk = bestD == 0 ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.1f, 0.35f, bestPar)) : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.3f, 0.1f, bestPar));
                        fk *= Mathf.Clamp01(1.5f - bestDist / 0.006f);
                        float b1 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1.15f, bestPar));
                        float b2 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.85f, 2.12f, bestPar));
                        seg[0] = 1f - b1; seg[1] = b1 * (1f - b2); seg[2] = b1 * b2;
                    }
                    float rest = 1f - fk;
                    l.Add(new KeyValuePair<int, float>(la, rest * kl));
                    l.Add(new KeyValuePair<int, float>(ha, rest * (1f - kl)));
                    if (fk > 0f) for (int k = 0; k < 3; k++) if (seg[k] > 0f) l.Add(new KeyValuePair<int, float>((int)ActorSkeleton.Digit(left, bestD, k), fk * seg[k]));
                    return l;
                }
            };
        }
    }
}
