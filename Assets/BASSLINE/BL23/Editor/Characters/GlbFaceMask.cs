using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Per-pixel "real facial skin" mask for the scanned GLB heads, in the face-overlay frame (uv1.xy).
    /// The head is rasterised orthographically from the front with a z-buffer, so every mask texel describes the
    /// frontmost surface there: fringe strands (in front of the skin, or painted onto it in hair colour) come out 0,
    /// facial skin / eyes / mouth come out 1. The shader multiplies every overlay element by this mask, so nothing
    /// can ever be drawn on hair. The frontal "photo" is also used to measure the scanned eyes / mouth and their colours.
    /// </summary>
    public static class GlbFaceMask
    {
        public const int R = 768;

        public sealed class Img
        {
            public int N; public float Scale;
            public Color[] Col;     // base colour of the frontmost surface (sRGB)
            public float[] Z;       // bind-pose z of the frontmost surface (bigger = closer to the viewer), NaN = empty
            public float[] Nz;      // normal.z of that surface
            public float[] Skin;    // 0..1 skin-likeness from colour
            public float[] Strand;  // 0..1 "sticks out in front of the face surface"
            public float[] Mask;    // final feathered mask
            public float[] Prior;   // face-region prior
            public bool Separable;  // hair and skin colours differ enough to classify by colour
            public Color SkinRef, HairRef, LashCol, LidCol, LipCol;
            public int Idx(int x, int y) => y * N + x;
        }

        public static Vector2 ToUV(Vector3 p, GlbFace.Frame f) => new Vector2(0.5f - (p.x - f.Center.x) / f.Scale, 0.5f + (p.y - f.Center.y) / f.Scale);

        /// <summary>Front-projected z-buffer render of the textured head in face-frame coordinates.</summary>
        public static Img Rasterize(List<Vector3> V, List<Vector2> UV, List<int> T, int[] triMat, int texMat, Texture2D tex, GlbFace.Frame f, float minY, int size = R)
        {
            var img = new Img { N = size, Scale = f.Scale, Col = new Color[size * size], Z = new float[size * size], Nz = new float[size * size] };
            var uvAt = new Vector2[size * size];
            for (int i = 0; i < img.Z.Length; i++) img.Z[i] = float.NaN;
            for (int t = 0; t < T.Count / 3; t++)
            {
                if (triMat != null && triMat[t] != texMat) continue;
                int i0 = T[t * 3], i1 = T[t * 3 + 1], i2 = T[t * 3 + 2];
                Vector3 p0 = V[i0], p1 = V[i1], p2 = V[i2];
                if (p0.y < minY && p1.y < minY && p2.y < minY) continue;
                Vector2 a = ToUV(p0, f) * size, b = ToUV(p1, f) * size, c = ToUV(p2, f) * size;
                float x0 = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), x1 = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                float y0 = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), y1 = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                if (x1 < 0 || y1 < 0 || x0 >= size || y0 >= size) continue;
                float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                if (Mathf.Abs(area) < 1e-9f) continue;
                Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
                float nz = n.sqrMagnitude > 1e-20f ? n.normalized.z : 0f;
                int ix0 = Mathf.Max(0, Mathf.FloorToInt(x0)), ix1 = Mathf.Min(size - 1, Mathf.CeilToInt(x1));
                int iy0 = Mathf.Max(0, Mathf.FloorToInt(y0)), iy1 = Mathf.Min(size - 1, Mathf.CeilToInt(y1));
                for (int y = iy0; y <= iy1; y++)
                    for (int x = ix0; x <= ix1; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float w0 = ((b.x - px) * (c.y - py) - (b.y - py) * (c.x - px)) / area;
                        float w1 = ((c.x - px) * (a.y - py) - (c.y - py) * (a.x - px)) / area;
                        float w2 = 1f - w0 - w1;
                        if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                        float z = w0 * p0.z + w1 * p1.z + w2 * p2.z;
                        int k = y * size + x;
                        if (!float.IsNaN(img.Z[k]) && img.Z[k] >= z) continue;
                        img.Z[k] = z;
                        // the mesh is double sided: the visible side faces the viewer
                        img.Nz[k] = Mathf.Abs(nz);
                        uvAt[k] = UV[i0] * w0 + UV[i1] * w1 + UV[i2] * w2;
                    }
            }
            var px32 = tex.GetPixels32();
            int tw = tex.width, th = tex.height;
            for (int k = 0; k < img.Col.Length; k++)
            {
                if (float.IsNaN(img.Z[k])) { img.Col[k] = Color.clear; continue; }
                img.Col[k] = Bilinear(px32, tw, th, uvAt[k]);
            }
            return img;
        }

        static Color Bilinear(Color32[] p, int w, int h, Vector2 uv)
        {
            float x = Mathf.Repeat(uv.x, 1f) * w - 0.5f, y = Mathf.Repeat(uv.y, 1f) * h - 0.5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, w - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, h - 1);
            int x1 = Mathf.Min(x0 + 1, w - 1), y1 = Mathf.Min(y0 + 1, h - 1);
            float fx = Mathf.Clamp01(x - x0), fy = Mathf.Clamp01(y - y0);
            Color a = p[y0 * w + x0], b = p[y0 * w + x1], c = p[y1 * w + x0], d = p[y1 * w + x1];
            return Color.Lerp(Color.Lerp(a, b, fx), Color.Lerp(c, d, fx), fy);
        }

        // ------------------------------------------------------------------ colour helpers
        public static Vector3 Lab(Color c)
        {
            float Lin(float v) => v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            float r = Lin(c.r), g = Lin(c.g), b = Lin(c.b);
            float X = (0.4124f * r + 0.3576f * g + 0.1805f * b) / 0.95047f;
            float Y = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            float Zc = (0.0193f * r + 0.1192f * g + 0.9505f * b) / 1.08883f;
            float F(float t) => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;
            float fx = F(X), fy = F(Y), fz = F(Zc);
            return new Vector3(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
        }

        static float DE(Vector3 a, Vector3 b) { var d = a - b; return Mathf.Sqrt(d.x * d.x * 0.35f + d.y * d.y + d.z * d.z); }

        static List<Vector3> Palette(List<Vector3> s, int max)
        {
            if (s.Count <= max) return s;
            // farthest-point sampling keeps the palette diverse (highlights, shadows)
            var outp = new List<Vector3>();
            var med = new Vector3(Median(s.Select(v => v.x)), Median(s.Select(v => v.y)), Median(s.Select(v => v.z)));
            outp.Add(med);
            var dist = s.Select(v => DE(v, med)).ToArray();
            while (outp.Count < max)
            {
                int bi = 0; float bd = -1;
                for (int i = 0; i < s.Count; i++) if (dist[i] > bd) { bd = dist[i]; bi = i; }
                if (bd < 2f) break;
                outp.Add(s[bi]);
                for (int i = 0; i < s.Count; i++) dist[i] = Mathf.Min(dist[i], DE(s[i], s[bi]));
            }
            return outp;
        }

        static float Median(IEnumerable<float> v) { var a = v.OrderBy(x => x).ToArray(); return a.Length == 0 ? 0 : a[a.Length / 2]; }

        // ------------------------------------------------------------------ mask
        /// <summary>Builds Skin / Strand / Mask for the photo, using the feature layout as a prior.</summary>
        public static void Classify(Img img, GlbFace.Layout L)
        {
            int N = img.N;
            float mm = 0.001f / img.Scale; // one millimetre in face uv
            Vector3[] lab = new Vector3[N * N];
            for (int k = 0; k < lab.Length; k++) if (!float.IsNaN(img.Z[k])) lab[k] = Lab(img.Col[k]);
            bool Has(int k) => !float.IsNaN(img.Z[k]);
            var c = L.C;
            // skin samples: cheeks under both eyes + beside the mouth, robustly filtered around their median
            var skinS = new List<Vector3>();
            void SampleBox(List<Vector3> dst, float u0, float u1, float v0, float v1)
            {
                for (int y = Mathf.Max(0, (int)(v0 * N)); y < Mathf.Min(N, (int)(v1 * N)); y++)
                    for (int x = Mathf.Max(0, (int)(u0 * N)); x < Mathf.Min(N, (int)(u1 * N)); x++)
                    {
                        int k = y * N + x;
                        if (Has(k) && img.Nz[k] > 0.35f) dst.Add(lab[k]);
                    }
            }
            float eyeBot = -c.EyeBot.y * mm;
            foreach (var e in new[] { L.EyeL, L.EyeR })
                SampleBox(skinS, e.x - 8f * mm, e.x + 8f * mm, e.y - eyeBot - 14f * mm, e.y - eyeBot - 4f * mm);
            SampleBox(skinS, L.Mouth.x - 16f * mm, L.Mouth.x - 9f * mm, L.Mouth.y - 3f * mm, L.Mouth.y + 8f * mm);
            SampleBox(skinS, L.Mouth.x + 9f * mm, L.Mouth.x + 16f * mm, L.Mouth.y - 3f * mm, L.Mouth.y + 8f * mm);
            var med = new Vector3(Median(skinS.Select(v => v.x)), Median(skinS.Select(v => v.y)), Median(skinS.Select(v => v.z)));
            skinS = skinS.Where(v => DE(v, med) < 12f).ToList();
            var hairS = new List<Vector3>();
            SampleBox(hairS, 0.2f, 0.8f, 0.9f, 0.995f);
            var hairMed = new Vector3(Median(hairS.Select(v => v.x)), Median(hairS.Select(v => v.y)), Median(hairS.Select(v => v.z)));
            img.Separable = DE(hairMed, med) > 16f;
            hairS = hairS.Where(v => DE(v, med) > 16f).ToList();
            var skinP = Palette(skinS, 24);
            var hairP = Palette(hairS, 48);
            img.SkinRef = LabToApproxColor(img, lab, med);
            img.Skin = new float[N * N];
            for (int k = 0; k < lab.Length; k++)
            {
                if (!Has(k)) continue;
                float ds = skinP.Count > 0 ? skinP.Min(p => DE(lab[k], p)) : 50f;
                float dh = hairP.Count > 0 ? hairP.Min(p => DE(lab[k], p)) : 50f;
                float s = dh / (dh + ds + 1e-3f);
                if (ds < 7f) s = Mathf.Max(s, 0.9f);
                img.Skin[k] = img.Separable ? s : 1f;
            }
            // facing from the smoothed height field (scan triangles are noisy at the millimetre scale)
            {
                int sr = Mathf.Max(1, Mathf.RoundToInt(0.0012f / PixelSize(img)));
                var zf = new float[N * N]; var wf = new float[N * N];
                for (int k = 0; k < zf.Length; k++) { bool h = Has(k); zf[k] = h ? img.Z[k] : 0f; wf[k] = h ? 1f : 0f; }
                zf = Box(Box(zf, N, sr), N, sr); wf = Box(Box(wf, N, sr), N, sr);
                float ps = PixelSize(img);
                float Zs(int x, int y) { x = Mathf.Clamp(x, 0, N - 1); y = Mathf.Clamp(y, 0, N - 1); int k = y * N + x; return wf[k] > 1e-4f ? zf[k] / wf[k] : float.NaN; }
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        int k = y * N + x;
                        if (!Has(k)) continue;
                        float zx0 = Zs(x - 2, y), zx1 = Zs(x + 2, y), zy0 = Zs(x, y - 2), zy1 = Zs(x, y + 2);
                        if (float.IsNaN(zx0) || float.IsNaN(zx1) || float.IsNaN(zy0) || float.IsNaN(zy1)) continue;
                        float gx = (zx1 - zx0) / (4f * ps), gy = (zy1 - zy0) / (4f * ps);
                        img.Nz[k] = 1f / Mathf.Sqrt(1f + gx * gx + gy * gy);
                    }
            }
            // strand detection: grey-scale opening of the height field removes narrow things that stick out
            int r = Mathf.RoundToInt(0.0045f / PixelSize(img));
            var zmin = MinMaxFilter(img.Z, N, r, true);
            var zopen = MinMaxFilter(zmin, N, r, false);
            img.Strand = new float[N * N];
            for (int k = 0; k < lab.Length; k++)
            {
                if (!Has(k)) continue;
                float dz = float.IsNaN(zopen[k]) ? 0f : img.Z[k] - zopen[k];
                img.Strand[k] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.0012f, 0.0028f, dz));
            }
            // conservative face region prior: from just above the eyes to the chin, out to the outer eye corners
            float ox = (L.EyeR.x - L.EyeL.x) * 0.5f + (-c.EyeOut.x + 4f) * mm;
            float top = L.EyeL.y + (c.EyeTop.y + 4f) * mm;
            float bottom = L.Mouth.y - 26f * mm;
            float vc = (top + bottom) * 0.5f, oy = (top - bottom) * 0.5f;
            var face = new float[N * N];
            img.Prior = new float[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int k = y * N + x;
                    float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                    // rounded box (superellipse); a bit narrower toward the chin
                    float wx = ox * (v < vc ? Mathf.Lerp(0.72f, 1f, Mathf.Clamp01((v - bottom) / (vc - bottom))) : 1f);
                    float pe = v > vc ? 6f : 3f; // flat across the eyes, rounded toward the chin
                    float q = Mathf.Pow(Mathf.Pow(Mathf.Abs(u - 0.5f) / wx, pe) + Mathf.Pow(Mathf.Abs(v - vc) / oy, pe), 1f / pe);
                    float prior = Mathf.Clamp01((1f - q) / 0.04f);
                    img.Prior[k] = prior;
                    if (!Has(k)) continue;
                    float inEye = 0f;
                    for (int s = 0; s < 2; s++)
                    {
                        Vector2 e = s == 0 ? L.EyeL : L.EyeR;
                        float dx = (u - e.x) * (s == 0 ? 1f : -1f) / mm, dy = (v - e.y) / mm; // mm, x toward the nose
                        EyeTopBot(c, Mathf.Clamp(dx, c.EyeOut.x, c.EyeIn.x), out float et, out float eb);
                        float outside = Mathf.Max(Mathf.Max(dy - (et + 1.2f), (eb - 1.2f) - dy), Mathf.Max(c.EyeOut.x - 1.5f - dx, dx - (c.EyeIn.x + 1.5f)));
                        inEye = Mathf.Max(inEye, Mathf.Clamp01(-outside / 0.6f));
                    }
                    float inMouth = Mathf.Clamp01((1.2f - Mathf.Sqrt(Sq((u - L.Mouth.x) / (c.MouthHalf / img.Scale * 1.3f)) + Sq((v - L.Mouth.y) / (3.5f * mm)))) / 0.3f);
                    float colour = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.36f, 0.52f, img.Skin[k]));
                    // eye / mouth paint is not skin coloured: there only the geometry decides
                    float cc = Mathf.Max(colour, Mathf.Max(inEye, inMouth));
                    float facing = Mathf.Clamp01((img.Nz[k] - 0.2f) / 0.2f);
                    face[k] = cc * (1f - img.Strand[k]) * facing * prior;
                }
            // binarise, close pinholes (~0.6 mm), drop islands, erode ~1 mm away from every hair edge, feather ~0.8 mm
            var bin = new bool[N * N];
            for (int k = 0; k < bin.Length; k++) bin[k] = face[k] > 0.5f;
            int cr = Mathf.Max(1, Mathf.RoundToInt(0.0006f / PixelSize(img)));
            bin = Erode(Dilate(bin, N, cr), N, cr);
            for (int k = 0; k < bin.Length; k++) if (!Has(k) || img.Strand[k] > 0.5f || img.Prior[k] <= 0f) bin[k] = false;
            bin = KeepLargeComponents(bin, N, (int)(N * N * 0.002f));
            int er = Mathf.Max(1, Mathf.RoundToInt(0.0010f / PixelSize(img)));
            var er1 = Erode(bin, N, er);
            var m = new float[N * N];
            for (int k = 0; k < m.Length; k++) m[k] = er1[k] ? 1f : 0f;
            int fr = Mathf.Max(1, Mathf.RoundToInt(0.0006f / PixelSize(img)));
            m = Box(m, N, fr); m = Box(m, N, fr);
            // hard cuts: never outside the eroded-then-feathered region's parent, never on strands / hair colour
            for (int k = 0; k < m.Length; k++)
            {
                if (!bin[k] || !Has(k) || img.Strand[k] > 0.35f) m[k] = 0f;
                else if (img.Separable && img.Skin[k] < 0.3f && face[k] < 0.5f) m[k] = 0f;
            }
            for (int i = 0; i < N; i++) { m[i] = 0; m[(N - 1) * N + i] = 0; m[i * N] = 0; m[i * N + N - 1] = 0; }
            img.Mask = m;
        }

        static bool[] Dilate(bool[] src, int N, int r)
        {
            var inv = new bool[src.Length];
            for (int k = 0; k < src.Length; k++) inv[k] = !src[k];
            var e = Erode(inv, N, r);
            for (int k = 0; k < e.Length; k++) e[k] = !e[k];
            return e;
        }

        static Color LabToApproxColor(Img img, Vector3[] lab, Vector3 target)
        {
            // pick the photo texel closest to the target Lab (exact inverse not needed)
            float best = float.MaxValue; Color c = Color.gray;
            for (int k = 0; k < lab.Length; k += 3)
            {
                if (float.IsNaN(img.Z[k])) continue;
                float d = DE(lab[k], target);
                if (d < best) { best = d; c = img.Col[k]; }
            }
            return c;
        }

        static float Sq(float x) => x * x;
        public static float PixelSize(Img img) => img.Scale / img.N; // metres per texel

        static float[] MinMaxFilter(float[] src, int N, int r, bool min)
        {
            var tmp = new float[N * N]; var dst = new float[N * N];
            for (int pass = 0; pass < 2; pass++)
            {
                var a = pass == 0 ? src : tmp; var b = pass == 0 ? tmp : dst;
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float best = float.NaN;
                        for (int d = -r; d <= r; d++)
                        {
                            int xx = pass == 0 ? x + d : x, yy = pass == 0 ? y : y + d;
                            if (xx < 0 || yy < 0 || xx >= N || yy >= N) continue;
                            float v = a[yy * N + xx];
                            if (float.IsNaN(v)) continue;
                            if (float.IsNaN(best) || (min ? v < best : v > best)) best = v;
                        }
                        b[y * N + x] = best;
                    }
            }
            return dst;
        }

        static bool[] Erode(bool[] src, int N, int r)
        {
            var a = new bool[N * N]; var b = new bool[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    bool ok = true;
                    for (int d = -r; d <= r && ok; d++) { int xx = x + d; if (xx < 0 || xx >= N || !src[y * N + xx]) ok = false; }
                    a[y * N + x] = ok;
                }
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    bool ok = true;
                    for (int d = -r; d <= r && ok; d++) { int yy = y + d; if (yy < 0 || yy >= N || !a[yy * N + x]) ok = false; }
                    b[y * N + x] = ok;
                }
            return b;
        }

        static float[] Box(float[] src, int N, int r)
        {
            var a = new float[N * N]; var b = new float[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float s = 0; int n = 0;
                    for (int d = -r; d <= r; d++) { int xx = x + d; if (xx < 0 || xx >= N) continue; s += src[y * N + xx]; n++; }
                    a[y * N + x] = s / n;
                }
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float s = 0; int n = 0;
                    for (int d = -r; d <= r; d++) { int yy = y + d; if (yy < 0 || yy >= N) continue; s += a[yy * N + x]; n++; }
                    b[y * N + x] = s / n;
                }
            return b;
        }

        static bool[] KeepLargeComponents(bool[] m, int N, int minSize)
        {
            var lab = new int[N * N]; var outp = new bool[N * N];
            int id = 0; var q = new Queue<int>(); var comp = new List<int>();
            for (int s = 0; s < m.Length; s++)
            {
                if (!m[s] || lab[s] != 0) continue;
                id++; comp.Clear(); q.Enqueue(s); lab[s] = id;
                while (q.Count > 0)
                {
                    int k = q.Dequeue(); comp.Add(k);
                    int x = k % N, y = k / N;
                    for (int n = 0; n < 4; n++)
                    {
                        int xx = x + (n == 0 ? 1 : n == 1 ? -1 : 0), yy = y + (n == 2 ? 1 : n == 3 ? -1 : 0);
                        if (xx < 0 || yy < 0 || xx >= N || yy >= N) continue;
                        int kk = yy * N + xx;
                        if (!m[kk] || lab[kk] != 0) continue;
                        lab[kk] = id; q.Enqueue(kk);
                    }
                }
                if (comp.Count >= minSize) foreach (int k in comp) outp[k] = true;
            }
            return outp;
        }

        public static float[] Feather(float[] m, int N, int r) => Box(Box(m, N, Mathf.Max(1, r / 2)), N, Mathf.Max(1, r / 2));

        public static float SampleBilinear(float[] a, int N, Vector2 uv)
        {
            if (uv.x < 0 || uv.y < 0 || uv.x > 1 || uv.y > 1) return 0f;
            float x = uv.x * N - 0.5f, y = uv.y * N - 0.5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, N - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, N - 1);
            int x1 = Mathf.Min(x0 + 1, N - 1), y1 = Mathf.Min(y0 + 1, N - 1);
            float fx = Mathf.Clamp01(x - x0), fy = Mathf.Clamp01(y - y0);
            return Mathf.Lerp(Mathf.Lerp(a[y0 * N + x0], a[y0 * N + x1], fx), Mathf.Lerp(a[y1 * N + x0], a[y1 * N + x1], fx), fy);
        }

        public static Texture2D MaskTexture(Img img)
        {
            var t = new Texture2D(img.N, img.N, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp };
            var c = new Color32[img.N * img.N];
            for (int k = 0; k < c.Length; k++) { byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(img.Mask[k]) * 255f); c[k] = new Color32(b, b, b, 255); }
            t.SetPixels32(c); t.Apply(false);
            return t;
        }


        // ------------------------------------------------------------------ eye shape (mm, x toward the nose, y up, origin = iris centre)
        static float Arc(Vector2 a, Vector2 apex, Vector2 b, float x)
        {
            if (x <= apex.x) { float t = Mathf.Clamp01(Mathf.InverseLerp(a.x, apex.x, x)); return Mathf.Lerp(a.y, apex.y, Mathf.Sin(t * Mathf.PI * 0.5f)); }
            float s = Mathf.Clamp01(Mathf.InverseLerp(b.x, apex.x, x)); return Mathf.Lerp(b.y, apex.y, Mathf.Sin(s * Mathf.PI * 0.5f));
        }

        public static bool EyeTopBot(GlbFace.Calib c, float x, out float top, out float bot)
        {
            top = Arc(c.EyeOut, c.EyeTop, c.EyeIn, x); bot = Arc(c.EyeOut, c.EyeBot, c.EyeIn, x);
            return x >= c.EyeOut.x && x <= c.EyeIn.x;
        }

        // ------------------------------------------------------------------ colours for the overlay art
        public sealed class GlbArt
        {
            public const int EyeRes = 256;
            public GlbFace.Layout L;
            public float EhMM;                                // eye cell half size (mm)
            public Color[] LidTop = new Color[EyeRes], LidBot = new Color[EyeRes];
            public Color Lash, Lip, Skin;
            public Color[] MouthCover;                        // 256 x 128, skin with the scanned mouth line removed
        }

        static bool Sample(Img img, Vector2 uv, out Color c, out int k)
        {
            int x = Mathf.FloorToInt(uv.x * img.N), y = Mathf.FloorToInt(uv.y * img.N);
            c = Color.clear; k = -1;
            if (x < 0 || y < 0 || x >= img.N || y >= img.N) return false;
            k = y * img.N + x;
            if (float.IsNaN(img.Z[k])) return false;
            c = img.Col[k];
            return true;
        }

        static Color Avg(List<Color> l, Color fallback) { if (l.Count == 0) return fallback; Color s = Color.clear; foreach (var c in l) s += c; s /= l.Count; s.a = 1; return s; }

        public static GlbArt Analyze(Img img, GlbFace.Layout L)
        {
            var c = L.C; var art = new GlbArt { L = L, EhMM = L.EyeHalfM * 1000f, Skin = img.SkinRef };
            int n = GlbArt.EyeRes;
            var okTop = new bool[n]; var okBot = new bool[n];
            var skinLab = Lab(img.SkinRef);
            bool SkinOk(int k) => k >= 0 && img.Mask[k] > 0.6f && img.Skin[k] > 0.5f && img.Strand[k] < 0.2f && DE(Lab(img.Col[k]), skinLab) < 13f;
            for (int i = 0; i < n; i++)
            {
                float x = ((i + 0.5f) / n - 0.5f) * 2f * art.EhMM;
                float xc = Mathf.Clamp(x, c.EyeOut.x + 1f, c.EyeIn.x - 1f);
                EyeTopBot(c, xc, out float top, out float bot);
                var up = new List<Color>(); var dn = new List<Color>();
                for (int s = 0; s < 2; s++)
                    for (float d = 1.6f; d <= 5.0f; d += 0.6f)
                    {
                        if (Sample(img, L.EyeUV(s, new Vector2(xc, top + d)), out var cu, out int ku) && SkinOk(ku)) up.Add(cu);
                        if (Sample(img, L.EyeUV(s, new Vector2(xc, bot - d)), out var cd, out int kd) && SkinOk(kd)) dn.Add(cd);
                    }
                okTop[i] = up.Count > 0; okBot[i] = dn.Count > 0;
                art.LidTop[i] = Color.Lerp(img.SkinRef * new Color(0.97f, 0.95f, 0.96f, 1f), Avg(up, img.SkinRef), 0.45f); art.LidBot[i] = Color.Lerp(img.SkinRef, Avg(dn, img.SkinRef), 0.45f);
            }
            FillMissing(art.LidTop, okTop, okBot.Any(b => b) ? art.LidBot : null, img.SkinRef);
            FillMissing(art.LidBot, okBot, null, img.SkinRef);
            SmoothCols(art.LidTop, 10); SmoothCols(art.LidBot, 10);
            // lash colour: darkest texels of the scanned upper lash line
            var lashL = new List<Color>();
            for (int s = 0; s < 2; s++)
                for (float x = c.EyeOut.x * 0.6f; x <= c.EyeIn.x * 0.6f; x += 0.4f)
                {
                    EyeTopBot(c, x, out float top, out _);
                    for (float d = 0.2f; d <= c.LashW + 0.4f; d += 0.3f)
                        if (Sample(img, L.EyeUV(s, new Vector2(x, top - d)), out var cl, out int kl) && img.Strand[kl] < 0.2f && img.Mask[kl] > 0.3f) lashL.Add(cl);
                }
            lashL = lashL.OrderBy(x => x.grayscale).Take(Mathf.Max(1, lashL.Count / 5)).ToList();
            art.Lash = Avg(lashL, new Color(0.12f, 0.08f, 0.08f));
            // lip line colour
            var lipL = new List<Color>();
            float mmU = 0.001f / L.F.Scale;
            for (float x = -c.MouthHalf * 0.6f; x <= c.MouthHalf * 0.6f; x += 0.0004f)
                for (float y = -1.5f; y <= 1.5f; y += 0.3f)
                    if (Sample(img, new Vector2(L.Mouth.x + x / L.F.Scale, L.Mouth.y + y * mmU), out var cm, out int km) && img.Mask[km] > 0.3f) lipL.Add(cm);
            lipL = lipL.OrderBy(x => x.grayscale).Take(Mathf.Max(1, lipL.Count / 4)).ToList();
            art.Lip = Avg(lipL, new Color(0.4f, 0.2f, 0.2f));
            // mouth cover: per column, interpolate the skin just above and below the scanned line
            art.MouthCover = new Color[256 * 128];
            float hw = L.MouthHalfM.x;
            float ry = 3.2f;
            for (int i = 0; i < 256; i++)
            {
                float px = (i + 0.5f) / 256f;
                float xm = (px - 0.5f) * 2f * hw;
                var up = new List<Color>(); var dn = new List<Color>();
                for (float dx = -0.6f; dx <= 0.6f; dx += 0.3f)
                    for (float d = 0.8f; d <= 2.4f; d += 0.4f)
                    {
                        Vector2 uu = new Vector2(L.Mouth.x + (xm + dx * 0.001f) / L.F.Scale, L.Mouth.y + (ry + d) * mmU);
                        Vector2 ud = new Vector2(L.Mouth.x + (xm + dx * 0.001f) / L.F.Scale, L.Mouth.y - (ry + d) * mmU);
                        if (Sample(img, uu, out var cu, out int ku) && SkinOk(ku)) up.Add(cu);
                        if (Sample(img, ud, out var cd, out int kd) && SkinOk(kd)) dn.Add(cd);
                    }
                Color cu2 = Avg(up, Avg(dn, img.SkinRef)), cd2 = Avg(dn, cu2);
                for (int j = 0; j < 128; j++)
                {
                    float py = (j + 0.5f) / 128f * 0.5f;
                    float ym = (py - 0.25f) * 2f * hw * 1000f;
                    float t = Mathf.Clamp01((ym + ry) / (2f * ry));
                    art.MouthCover[j * 256 + i] = Color.Lerp(cd2, cu2, t);
                }
            }
            return art;
        }

        static void FillMissing(Color[] a, bool[] ok, Color[] alt, Color fallback)
        {
            int n = a.Length;
            if (!ok.Any(b => b)) { for (int i = 0; i < n; i++) a[i] = alt != null ? alt[i] : fallback; return; }
            for (int i = 0; i < n; i++)
            {
                if (ok[i]) continue;
                int l = i, r = i;
                while (l >= 0 && !ok[l]) l--;
                while (r < n && !ok[r]) r++;
                if (l < 0) a[i] = a[r]; else if (r >= n) a[i] = a[l]; else a[i] = Color.Lerp(a[l], a[r], (i - l) / (float)(r - l));
            }
        }

        static void SmoothCols(Color[] a, int r)
        {
            var src = (Color[])a.Clone();
            for (int i = 0; i < a.Length; i++)
            {
                Color s = Color.clear; int n = 0;
                for (int d = -r; d <= r; d++) { int j = i + d; if (j < 0 || j >= a.Length) continue; s += src[j]; n++; }
                a[i] = s / n; a[i].a = 1;
            }
        }

        // ------------------------------------------------------------------ debug output
        static string ShotDir { get { string d = Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots")); Directory.CreateDirectory(d); return d; } }

        /// <summary>Photo (+ eye outline / mouth rect) | skin-likeness | strands | mask, to Shots/glbface_ID.png.</summary>
        public static void SaveDebug(Img img, string id, GlbFace.Layout L)
        {
            int N = img.N;
            var t = new Texture2D(N * 4, N, TextureFormat.RGB24, false);
            var px = new Color[N * 4 * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int k = y * N + x;
                    bool has = !float.IsNaN(img.Z[k]);
                    Color bg = new Color(0.1f, 0.3f, 0.1f);
                    px[y * N * 4 + x] = has ? img.Col[k] : bg;
                    px[y * N * 4 + N + x] = has && img.Skin != null ? Color.Lerp(Color.black, new Color(1f, 0.8f, 0.7f), img.Skin[k]) : bg;
                    px[y * N * 4 + 2 * N + x] = has && img.Strand != null ? Color.Lerp(img.Col[k] * 0.5f, Color.red, img.Strand[k]) : bg;
                    px[y * N * 4 + 3 * N + x] = has && img.Mask != null ? Color.Lerp(img.Col[k] * 0.3f, img.Col[k], img.Mask[k]) + new Color(0, 0.3f * img.Mask[k], 0) : bg;
                    if (img.Prior != null && img.Prior[k] > 0f && img.Prior[k] < 0.5f) px[y * N * 4 + 3 * N + x] = Color.yellow;
                }
            void Dot(Vector2 uv, Color col, int panel)
            {
                int x = Mathf.RoundToInt(uv.x * N), y = Mathf.RoundToInt(uv.y * N);
                if (x >= 0 && x < N && y >= 0 && y < N) px[y * N * 4 + panel * N + x] = col;
            }
            var c = L.C;
            for (int s = 0; s < 2; s++)
            {
                for (float x = c.EyeOut.x; x <= c.EyeIn.x; x += 0.1f)
                {
                    EyeTopBot(c, x, out float top, out float bot);
                    Dot(L.EyeUV(s, new Vector2(x, top)), Color.cyan, 0); Dot(L.EyeUV(s, new Vector2(x, bot)), Color.cyan, 0);
                    Dot(L.EyeUV(s, new Vector2(x, top - c.LashW)), new Color(0, 0.6f, 1f), 0);
                }
                for (int d = -6; d <= 6; d++) { Dot(L.EyeUV(s, new Vector2(d * 0.25f, 0)), Color.magenta, 0); Dot(L.EyeUV(s, new Vector2(0, d * 0.25f)), Color.magenta, 0); }
            }
            for (float x = -c.MouthHalf; x <= c.MouthHalf; x += 0.0001f) Dot(new Vector2(L.Mouth.x + x / L.F.Scale, L.Mouth.y), Color.cyan, 0);
            t.SetPixels(px); t.Apply();
            File.WriteAllBytes(Path.Combine(ShotDir, "glbface_" + id + ".png"), t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
        }

        /// <summary>CPU composite of the overlay exactly as the shader combines it (cell + mask), for eye / mouth states.</summary>
        public static void SavePreview(Img img, GlbFace.Layout L, Texture2D atlas, string id, Texture2D fx = null)
        {
            int[] eyes = { 0, 1, 2, 3, 5, 6, 8, 10, 11, 13, 14, 15 };
            int[] mouths = { 0, 4, 5, 1, 2, 6, 3, 8 };
            int tiles = eyes.Length + mouths.Length;
            // crop: eyes + mouth region
            float u0 = L.EyeL.x - L.EyeHalf * 1.3f, u1 = L.EyeR.x + L.EyeHalf * 1.3f;
            float v1 = L.EyeL.y + L.EyeHalf * 0.75f, v0 = L.Mouth.y - 0.05f;
            int TW = 480; int TH = Mathf.RoundToInt(TW * (v1 - v0) / (u1 - u0));
            int cols = 5, rows = Mathf.CeilToInt(tiles / (float)cols);
            var outp = new Color[TW * cols * TH * rows];
            for (int ti = 0; ti < tiles; ti++)
            {
                int eIdx = ti < eyes.Length ? eyes[ti] : 0, mIdx = ti < eyes.Length ? 0 : mouths[ti - eyes.Length];
                int ox = (ti % cols) * TW, oy = (rows - 1 - ti / cols) * TH;
                for (int y = 0; y < TH; y++)
                    for (int x = 0; x < TW; x++)
                    {
                        float u = Mathf.Lerp(u0, u1, (x + 0.5f) / TW), v = Mathf.Lerp(v0, v1, (y + 0.5f) / TH);
                        int k = Mathf.Clamp((int)(v * img.N), 0, img.N - 1) * img.N + Mathf.Clamp((int)(u * img.N), 0, img.N - 1);
                        Color col = float.IsNaN(img.Z[k]) ? new Color(0.1f, 0.3f, 0.1f) : img.Col[k];
                        float m = img.Mask[k];
                        // eyes
                        if (eIdx != 0)
                            for (int s = 0; s < 2; s++)
                            {
                                Vector2 e = s == 0 ? L.EyeL : L.EyeR;
                                float lx = (u - (e.x - L.EyeHalf)) / (2f * L.EyeHalf), ly = (v - (e.y - L.EyeHalf)) / (2f * L.EyeHalf);
                                if (s == 1) lx = 1f - lx;
                                if (lx < 0 || lx > 1 || ly < 0 || ly > 1) continue;
                                var cc = atlas.GetPixelBilinear((eIdx % 4) * 0.25f + lx * 0.25f, 1f - (eIdx / 4 + 1) * 0.125f + ly * 0.125f);
                                col = Color.Lerp(col, cc, cc.a * m);
                            }
                        if (mIdx != 0)
                        {
                            float lx = (u - (L.Mouth.x - L.MouthHalf.x)) / (2f * L.MouthHalf.x), ly = (v - (L.Mouth.y - L.MouthHalf.y)) / (2f * L.MouthHalf.y);
                            if (lx >= 0 && lx <= 1 && ly >= 0 && ly <= 1)
                            {
                                var cc = atlas.GetPixelBilinear((mIdx % 4) * 0.25f + lx * 0.25f, 0.5f - (mIdx / 4 + 1) * 0.0625f + ly * 0.0625f);
                                col = Color.Lerp(col, cc, cc.a * m);
                            }
                        }
                        col.a = 1;
                        outp[(oy + y) * TW * cols + ox + x] = col;
                    }
            }
            var t = new Texture2D(TW * cols, TH * rows, TextureFormat.RGB24, false);
            t.SetPixels(outp); t.Apply();
            File.WriteAllBytes(Path.Combine(ShotDir, "glbface_preview_" + id + ".png"), t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
        }
    }
}
