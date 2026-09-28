using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Texture editing for the scanned models in 3D: every texel of the base colour map knows the surface point it
    /// paints (bind pose position + normal + the region label of its triangle), so fixes can be described on the body
    /// ("recolour the jacket", "put a mole under the left eye", "hair coloured texels only on the head") and applied in
    /// UV space. Also: texel-space inpainting for removing blotches and skin patches baked into hair.
    /// </summary>
    public sealed class GlbTexPaint
    {
        public int W, H;
        public Color[] Px;              // sRGB colours
        public int[] Tri;               // texel -> triangle (-1 = unused texel)
        public Vector3[] Pos;           // texel -> bind-pose position
        public Vector3[] Nrm;
        public bool[] Edited;

        public bool Has(int k) => Tri[k] >= 0;

        public static GlbTexPaint Build(Texture2D src, List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<int> T, int[] triMat, int mat)
        {
            var tp = new GlbTexPaint { W = src.width, H = src.height };
            int W = tp.W, H = tp.H;
            tp.Px = src.GetPixels();
            tp.Tri = new int[W * H]; for (int i = 0; i < tp.Tri.Length; i++) tp.Tri[i] = -1;
            tp.Pos = new Vector3[W * H]; tp.Nrm = new Vector3[W * H]; tp.Edited = new bool[W * H];
            int nt = T.Count / 3;
            for (int t = 0; t < nt; t++)
            {
                if (triMat != null && triMat[t] != mat) continue;
                int i0 = T[t * 3], i1 = T[t * 3 + 1], i2 = T[t * 3 + 2];
                Vector2 a = new Vector2(UV[i0].x * W, UV[i0].y * H), b = new Vector2(UV[i1].x * W, UV[i1].y * H), c = new Vector2(UV[i2].x * W, UV[i2].y * H);
                float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                if (Mathf.Abs(area) < 1e-9f) continue;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))) - 1), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))) + 1);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))) - 1), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))) + 1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float w0 = ((b.x - px) * (c.y - py) - (b.y - py) * (c.x - px)) / area;
                        float w1 = ((c.x - px) * (a.y - py) - (c.y - py) * (a.x - px)) / area;
                        float w2 = 1f - w0 - w1;
                        // include a half-texel margin so seams are covered
                        const float m = -0.02f;
                        if (w0 < m || w1 < m || w2 < m) continue;
                        int k = y * W + x;
                        if (tp.Tri[k] >= 0 && Mathf.Min(w0, Mathf.Min(w1, w2)) < 0f) continue;
                        tp.Tri[k] = t;
                        tp.Pos[k] = V[i0] * w0 + V[i1] * w1 + V[i2] * w2;
                        tp.Nrm[k] = (N[i0] * w0 + N[i1] * w1 + N[i2] * w2).normalized;
                    }
            }
            return tp;
        }

        public Texture2D ToTexture()
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true);
            t.SetPixels(Px); t.Apply(true);
            return t;
        }

        // ------------------------------------------------------------------ colour helpers
        public static float Lum(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        public static void HSV(Color c, out float h, out float s, out float v) => Color.RGBToHSV(c, out h, out s, out v);

        /// <summary>Recolour: keep the painted light / dark structure (luminance relative to a reference), take a new tint.</summary>
        public static Color Retint(Color c, Color tint, float refLum, float keepDetail = 1f)
        {
            float l = Lum(c);
            float k = Mathf.Clamp(l / Mathf.Max(0.02f, refLum), 0.05f, 3f);
            k = Mathf.Lerp(1f, k, keepDetail);
            var o = new Color(tint.r * k, tint.g * k, tint.b * k, c.a);
            // highlights desaturate toward white like painted fabric
            float over = Mathf.Max(0f, Mathf.Max(o.r, Mathf.Max(o.g, o.b)) - 1f);
            o = Color.Lerp(o, Color.white, Mathf.Clamp01(over * 0.8f));
            o.r = Mathf.Clamp01(o.r); o.g = Mathf.Clamp01(o.g); o.b = Mathf.Clamp01(o.b);
            return o;
        }

        public int Apply(Func<int, bool> mask, Func<int, Color, Color> map)
        {
            int n = 0;
            for (int k = 0; k < Px.Length; k++)
            {
                if (Tri[k] < 0 || !mask(k)) continue;
                Px[k] = map(k, Px[k]); Edited[k] = true; n++;
            }
            return n;
        }

        /// <summary>Soft dot painted around a surface point (radius in metres), only on texels facing like the surface there.</summary>
        public int Dot(Vector3 center, Vector3 normal, float radius, Color col, float strength = 1f)
        {
            int n = 0;
            for (int k = 0; k < Px.Length; k++)
            {
                if (Tri[k] < 0) continue;
                float d = (Pos[k] - center).magnitude;
                if (d > radius * 1.6f || Vector3.Dot(Nrm[k], normal) < 0.3f) continue;
                float a = strength * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((radius * 1.6f - d) / (radius * 0.9f)));
                Px[k] = Color.Lerp(Px[k], new Color(col.r, col.g, col.b, Px[k].a), a); Edited[k] = true; n++;
            }
            return n;
        }

        /// <summary>
        /// Fills the masked texels from the surrounding unmasked texels of the same UV island (repeated neighbour
        /// averaging, then a light blur of the filled area). Returns the number of texels filled.
        /// </summary>
        public int Inpaint(bool[] mask, int blurPasses = 2)
        {
            var known = new bool[Px.Length];
            int todo = 0;
            for (int k = 0; k < Px.Length; k++) { known[k] = Tri[k] >= 0 && !mask[k]; if (Tri[k] >= 0 && mask[k]) todo++; }
            int filled = 0;
            for (int it = 0; it < 400 && filled < todo; it++)
            {
                var add = new List<int>(); var cols = new List<Color>();
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int k = y * W + x;
                        if (known[k] || Tri[k] < 0) continue;
                        Color acc = Color.clear; int c = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                                int kk = yy * W + xx;
                                if (!known[kk]) continue;
                                acc += Px[kk]; c++;
                            }
                        if (c >= 2) { add.Add(k); cols.Add(acc / c); }
                    }
                if (add.Count == 0) break;
                for (int i = 0; i < add.Count; i++) { Px[add[i]] = cols[i]; known[add[i]] = true; Edited[add[i]] = true; }
                filled += add.Count;
            }
            for (int pass = 0; pass < blurPasses; pass++)
            {
                var src = (Color[])Px.Clone();
                for (int y = 1; y < H - 1; y++)
                    for (int x = 1; x < W - 1; x++)
                    {
                        int k = y * W + x; if (!mask[k] || Tri[k] < 0) continue;
                        Color acc = Color.clear; int c = 0;
                        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { int kk = (y + dy) * W + x + dx; if (Tri[kk] < 0) continue; acc += src[kk]; c++; }
                        Px[k] = acc / Mathf.Max(1, c);
                    }
            }
            return filled;
        }

        /// <summary>Grows the edits into the unused gutter texels around UV islands (bilinear / mip sampling reads them).</summary>
        public void PadEdits(int rings)
        {
            var done = (bool[])Edited.Clone();
            for (int r = 0; r < rings; r++)
            {
                var add = new List<int>(); var cols = new List<Color>();
                for (int y = 1; y < H - 1; y++)
                    for (int x = 1; x < W - 1; x++)
                    {
                        int k = y * W + x;
                        if (done[k] || Tri[k] >= 0) continue;
                        Color acc = Color.clear; int c = 0;
                        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { int kk = k + dy * W + dx; if (done[kk]) { acc += Px[kk]; c++; } }
                        if (c > 0) { add.Add(k); cols.Add(acc / c); }
                    }
                for (int i = 0; i < add.Count; i++) { Px[add[i]] = cols[i]; done[add[i]] = true; }
            }
        }

        /// <summary>Median-like cleanup of isolated blotches: texels that differ strongly from the median of their
        /// neighbourhood (same region) are replaced by it. Returns the number of texels changed.</summary>
        public int Despeckle(Func<int, bool> region, int r, float threshold)
        {
            var src = (Color[])Px.Clone();
            int n = 0;
            var buf = new List<float>(64);
            for (int y = r; y < H - r; y++)
                for (int x = r; x < W - r; x++)
                {
                    int k = y * W + x;
                    if (Tri[k] < 0 || !region(k)) continue;
                    Color med = MedianAround(src, x, y, r, region);
                    if (med.a < 0) continue;
                    float d = Mathf.Abs(Lum(src[k]) - Lum(med)) + (Mathf.Abs(src[k].r - med.r) + Mathf.Abs(src[k].g - med.g) + Mathf.Abs(src[k].b - med.b)) * 0.3f;
                    if (d > threshold) { Px[k] = new Color(med.r, med.g, med.b, src[k].a); Edited[k] = true; n++; }
                }
            return n;
        }

        Color MedianAround(Color[] src, int x, int y, int r, Func<int, bool> region)
        {
            var rs = new List<float>(); var gs = new List<float>(); var bs = new List<float>();
            for (int dy = -r; dy <= r; dy += 1)
                for (int dx = -r; dx <= r; dx += 1)
                {
                    int kk = (y + dy) * W + x + dx;
                    if (Tri[kk] < 0 || !region(kk)) continue;
                    rs.Add(src[kk].r); gs.Add(src[kk].g); bs.Add(src[kk].b);
                }
            if (rs.Count < 5) return new Color(0, 0, 0, -1);
            rs.Sort(); gs.Sort(); bs.Sort();
            return new Color(rs[rs.Count / 2], gs[gs.Count / 2], bs[bs.Count / 2], 1f);
        }
    }
}
