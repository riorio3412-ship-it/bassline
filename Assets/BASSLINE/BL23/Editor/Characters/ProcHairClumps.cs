using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Characters;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Anime hair as separate clump meshes: every lock of a hairstyle becomes its own swept, flattened, pointed mesh
    /// (lens-shaped cross-section lying along the scalp), so the outline pass draws the line art between clumps and the
    /// fringe separates into strands instead of melting into one helmet. Wide locks are split into layered sub-clumps.
    /// </summary>
    public sealed partial class ProcBuilder
    {
        readonly List<SLock> clumps = new List<SLock>();
        public bool UseClumps = true;

        static Vector3 CatmullP(IList<Vector3> p, int i, float u)
        {
            int n = p.Count - 1;
            Vector3 p0 = p[Mathf.Max(0, i - 1)], p1 = p[i], p2 = p[Mathf.Min(n, i + 1)], p3 = p[Mathf.Min(n, i + 2)];
            return 0.5f * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u * u * u);
        }

        /// <summary>Resamples a lock's polyline (smooth Catmull-Rom) into n points with interpolated radii.</summary>
        static void Resample(IList<Vector3> pts, IList<float> rs, int n, List<Vector3> op, List<float> orad)
        {
            var dense = new List<Vector3>(); var dr = new List<float>();
            for (int i = 0; i + 1 < pts.Count; i++)
                for (int k = 0; k < 6; k++) { float u = k / 6f; dense.Add(CatmullP(pts, i, u)); dr.Add(Mathf.Lerp(rs[i], rs[i + 1], u)); }
            dense.Add(pts[pts.Count - 1]); dr.Add(rs[rs.Count - 1]);
            var acc = new float[dense.Count];
            for (int i = 1; i < dense.Count; i++) acc[i] = acc[i - 1] + (dense[i] - dense[i - 1]).magnitude;
            float total = acc[acc.Length - 1];
            int j = 0;
            for (int k = 0; k < n; k++)
            {
                float s = total * k / (n - 1);
                while (j < acc.Length - 2 && acc[j + 1] < s) j++;
                float seg = Mathf.Max(1e-6f, acc[j + 1] - acc[j]);
                float u = Mathf.Clamp01((s - acc[j]) / seg);
                op.Add(Vector3.Lerp(dense[j], dense[j + 1], u)); orad.Add(Mathf.Lerp(dr[j], dr[j + 1], u));
            }
        }

        /// <summary>Clump mesh: lens cross-section (width along the scalp, thin radially), pointed tip, root sunk into the scalp.</summary>
        RawGeo ClumpGeo(IList<Vector3> pts, IList<float> rs, Vector3 center, float flat, int mat, Func<Vector3, Color> tint, float thickK = 0.62f)
        {
            var g = new RawGeo { Mat = mat };
            float len = 0f; for (int i = 1; i < pts.Count; i++) len += (pts[i] - pts[i - 1]).magnitude;
            int n = Mathf.Clamp(Mathf.CeilToInt(len / 0.006f) + 2, 5, 40);
            var P = new List<Vector3>(); var R = new List<float>();
            Resample(pts, rs, n, P, R);
            const int ring = 8;
            Vector3 prevB = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector3 T = (P[Mathf.Min(n - 1, i + 1)] - P[Mathf.Max(0, i - 1)]).normalized;
                Vector3 radial = Vector3.ProjectOnPlane(P[i] - center, T);
                if (radial.sqrMagnitude < 1e-8f) radial = Vector3.ProjectOnPlane(Vector3.up, T);
                radial.Normalize();
                Vector3 B = Vector3.Cross(T, radial).normalized;
                if (i > 0 && Vector3.Dot(B, prevB) < 0) B = -B;
                prevB = B;
                float w = R[i] / Mathf.Max(0.25f, flat);
                float th = R[i] * thickK;
                // pointed tip, slightly pinched root
                float tipK = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) / 0.22f));
                w *= Mathf.Lerp(0.04f, 1f, tipK); th *= Mathf.Lerp(0.15f, 1f, tipK);
                if (t < 0.08f) { w *= Mathf.Lerp(0.75f, 1f, t / 0.08f); }
                for (int k = 0; k < ring; k++)
                {
                    float a = k / (float)ring * Mathf.PI * 2f;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    // lens: sharpened side edges, the outer (visible) face a little fuller than the inner one
                    float x = Mathf.Sign(ca) * Mathf.Pow(Mathf.Abs(ca), 0.8f);
                    float y = Mathf.Sign(sa) * Mathf.Pow(Mathf.Abs(sa), 1.25f) * (sa > 0 ? 1f : 0.7f);
                    Vector3 v = P[i] + B * (w * x) + radial * (th * y);
                    g.V.Add(v);
                    g.C.Add(tint != null ? tint(v) : Color.white);
                }
            }
            // tip vertex
            Vector3 tipDir = (P[n - 1] - P[n - 2]).normalized;
            int tip = g.V.Count; g.V.Add(P[n - 1] + tipDir * 0.002f); g.C.Add(tint != null ? tint(P[n - 1]) : Color.white);
            // root cap vertex (sunk into the scalp)
            int root = g.V.Count; Vector3 rootP = P[0] - (P[1] - P[0]).normalized * 0.004f; g.V.Add(rootP); g.C.Add(tint != null ? tint(rootP) : Color.white);
            for (int i = 0; i + 1 < n; i++)
                for (int k = 0; k < ring; k++)
                {
                    int a = i * ring + k, b = i * ring + (k + 1) % ring, c = (i + 1) * ring + k, d = (i + 1) * ring + (k + 1) % ring;
                    g.T.Add(a); g.T.Add(c); g.T.Add(b);
                    g.T.Add(b); g.T.Add(c); g.T.Add(d);
                }
            for (int k = 0; k < ring; k++)
            {
                int a = (n - 1) * ring + k, b = (n - 1) * ring + (k + 1) % ring;
                g.T.Add(a); g.T.Add(tip); g.T.Add(b);
                int c = k, d = (k + 1) % ring;
                g.T.Add(c); g.T.Add(d); g.T.Add(root);
            }
            // orient triangles outward (away from the clump axis) and build smooth normals
            var nrm = new Vector3[g.V.Count];
            for (int t = 0; t < g.T.Count; t += 3)
            {
                int a = g.T[t], b = g.T[t + 1], c = g.T[t + 2];
                Vector3 fn = Vector3.Cross(g.V[b] - g.V[a], g.V[c] - g.V[a]);
                Vector3 ctr = (g.V[a] + g.V[b] + g.V[c]) / 3f;
                int seg = Mathf.Clamp(Mathf.Min(a, Mathf.Min(b, c)) / ring, 0, n - 1);
                if (Vector3.Dot(fn, ctr - P[seg]) < 0) { g.T[t + 1] = c; g.T[t + 2] = b; fn = -fn; }
                nrm[a] += fn; nrm[b] += fn; nrm[c] += fn;
            }
            for (int i = 0; i < nrm.Length; i++) g.N.Add(nrm[i].sqrMagnitude > 1e-14f ? nrm[i].normalized : Vector3.up);
            return g;
        }

        /// <summary>Converts the style's locks into layered clump meshes (wide locks split into 2-3 strands).</summary>
        void BuildClumps(Func<Vector3, Color> grad)
        {
            if (clumps.Count == 0) return;
            var part = new Part { Name = "HairClumps", Skin = hairPart.Skin, AOScale = 0.8f };
            var rnd = new System.Random(def.Id.GetHashCode() ^ 0x5eed);
            float R01() => (float)rnd.NextDouble();
            foreach (var L in clumps)
            {
                var pts = L.Points; var rs = L.Radii;
                float wmax = rs.Max() / Mathf.Max(0.25f, L.Flat);
                int split = wmax > 0.03f ? 3 : wmax > 0.018f ? 2 : 1;
                if (split == 1) { part.AddRaw(ClumpGeo(pts, rs, L.Center, L.Flat, mHair, grad)); continue; }
                for (int s = 0; s < split; s++)
                {
                    float off = (s - (split - 1) * 0.5f) / split; // -0.33..0.33
                    float lenK = 0.82f + 0.3f * R01();
                    var np = new List<Vector3>(); var nr = new List<float>();
                    for (int i = 0; i < pts.Count; i++)
                    {
                        float t = i / (float)(pts.Count - 1);
                        Vector3 T = (pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]).normalized;
                        Vector3 radial = Vector3.ProjectOnPlane(pts[i] - L.Center, T).normalized;
                        Vector3 B = Vector3.Cross(T, radial).normalized;
                        float w = rs[i] / Mathf.Max(0.25f, L.Flat);
                        // strands fan apart toward the tips, alternate layers sit slightly above / below
                        Vector3 p = pts[i] + B * (off * w * (1.1f + 0.5f * t)) + radial * ((s % 2 == 0 ? 0.0012f : -0.0008f) * t);
                        np.Add(p); nr.Add(rs[i] * (1.15f / split + 0.12f));
                    }
                    // shorten / lengthen by trimming or extending the last segment
                    if (lenK < 1f && np.Count > 2)
                    {
                        int keep = Mathf.Max(2, Mathf.RoundToInt((np.Count - 1) * lenK) + 1);
                        Vector3 last = Vector3.Lerp(np[keep - 1], np[Mathf.Min(np.Count - 1, keep)], 0.5f);
                        np = np.Take(keep).ToList(); nr = nr.Take(keep).ToList();
                        np[np.Count - 1] = last;
                    }
                    else if (lenK > 1f && np.Count > 1)
                    {
                        Vector3 d = np[np.Count - 1] - np[np.Count - 2];
                        np.Add(np[np.Count - 1] + d * (lenK - 1f) * 2f); nr.Add(nr[nr.Count - 1] * 0.5f);
                    }
                    part.AddRaw(ClumpGeo(np, nr, L.Center, L.Flat, mHair, grad));
                }
            }
            M.Parts.Add(part);
            M.Log += $"hair clumps: {clumps.Count} locks -> {part.Raw.Count} clump meshes\n";
        }
    }
}
