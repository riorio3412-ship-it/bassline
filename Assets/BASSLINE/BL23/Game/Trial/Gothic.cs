using System.Collections;
using System.Collections.Generic;
using BL23.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BL23.Game
{
    /// <summary>Trial palette: black lacquer, carved oak, candlelight, tarnished brass, oxblood velvet, parchment and wax,
    /// muted stained-glass jewel accents. (The mansion's neon belongs to the dream outside, not to the court.)</summary>
    public static class GPal
    {
        public static readonly Color Lacquer = new Color(0.04f, 0.03f, 0.025f, 1f);
        public static readonly Color Oak = new Color(0.24f, 0.15f, 0.08f, 1f);
        public static readonly Color OakDark = new Color(0.11f, 0.065f, 0.035f, 1f);
        public static readonly Color Brass = new Color(0.78f, 0.62f, 0.34f, 1f);
        public static readonly Color BrassDim = new Color(0.42f, 0.32f, 0.16f, 1f);
        public static readonly Color Gilt = new Color(0.96f, 0.84f, 0.52f, 1f);
        public static readonly Color Candle = new Color(1f, 0.72f, 0.38f, 1f);
        public static readonly Color Flame = new Color(1f, 0.93f, 0.72f, 1f);
        public static readonly Color Oxblood = new Color(0.33f, 0.05f, 0.07f, 1f);
        public static readonly Color Velvet = new Color(0.2f, 0.03f, 0.05f, 1f);
        public static readonly Color Wax = new Color(0.55f, 0.07f, 0.09f, 1f);
        public static readonly Color BlackWax = new Color(0.09f, 0.08f, 0.09f, 1f);
        public static readonly Color Parchment = new Color(0.9f, 0.84f, 0.7f, 1f);
        public static readonly Color ParchDark = new Color(0.68f, 0.58f, 0.42f, 1f);
        public static readonly Color Ink = new Color(0.14f, 0.09f, 0.06f, 1f);
        public static readonly Color InkRed = new Color(0.5f, 0.06f, 0.06f, 1f);
        public static readonly Color Emerald = new Color(0.2f, 0.45f, 0.33f, 1f);
        public static readonly Color Sapphire = new Color(0.2f, 0.33f, 0.55f, 1f);
        public static readonly Color Amethyst = new Color(0.4f, 0.26f, 0.48f, 1f);
        public static readonly Color Bone = new Color(0.93f, 0.89f, 0.8f, 1f);
        public static readonly Color Smoke = new Color(0.6f, 0.56f, 0.52f, 1f);
        public static Color A(Color c, float a) { c.a = a; return c; }
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    /// <summary>Serif faces for the court (GowunBatang). BlackHanSans is kept for rare emphasis only.</summary>
    public static class GFont
    {
        static TMP_FontAsset _reg;
        public static TMP_FontAsset Serif => Fonts.Serif;
        public static TMP_FontAsset SerifReg { get { if (_reg == null) { _reg = Resources.Load<TMP_FontAsset>("FontAssets/GowunBatang-Regular SDF") ?? Fonts.Serif; if (_reg != Fonts.Body && _reg.fallbackFontAssetTable == null) _reg.fallbackFontAssetTable = new List<TMP_FontAsset> { Fonts.Body }; } return _reg; } }
    }

    /// <summary>Procedural textures (made once): parchment and a soft radial glow. (No grain, no vignette: the court is seen clean.)</summary>
    public static class GTex
    {
        static Texture2D _parch, _glow;
        static float Hash(int x, int y, int s) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041); h = (h ^ (h >> 13)) * 1274126177u; return ((h ^ (h >> 16)) & 0xffffff) / 16777215f; } }
        static float Noise(float x, float y, int s)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(xi, yi, s), b = Hash(xi + 1, yi, s), c = Hash(xi, yi + 1, s), d = Hash(xi + 1, yi + 1, s);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
        static float Fbm(float x, float y, int s) { float v = 0, amp = 0.5f, f = 1; for (int o = 0; o < 5; o++) { v += Noise(x * f, y * f, s + o * 17) * amp; f *= 2.03f; amp *= 0.5f; } return v; }

        public static Texture2D Parchment
        {
            get
            {
                if (_parch != null) return _parch;
                int n = 512; var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "gothic_parchment", wrapMode = TextureWrapMode.Clamp }; var px = new Color32[n * n];
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                    {
                        float u = x / (float)n, v = y / (float)n;
                        float cloud = Fbm(u * 5, v * 5, 3), fib = Noise(u * 180, v * 22, 9) * 0.5f + Noise(u * 23, v * 190, 11) * 0.5f;
                        float stain = Mathf.Clamp01(Fbm(u * 2.2f + 7, v * 2.2f + 3, 21) * 1.9f - 0.95f);
                        float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v)); float burn = Mathf.Clamp01(1 - edge * 9f + (Fbm(u * 9, v * 9, 5) - 0.5f) * 0.6f);
                        var c = Color.Lerp(GPal.Parchment, GPal.ParchDark, cloud * 0.55f + fib * 0.12f);
                        c = Color.Lerp(c, new Color(0.55f, 0.4f, 0.22f), stain * 0.3f);
                        c = Color.Lerp(c, new Color(0.3f, 0.18f, 0.08f), burn * 0.5f);
                        c.a = 1; px[y * n + x] = c;
                    }
                t.SetPixels32(px); t.Apply(true); _parch = t; return t;
            }
        }

        public static Texture2D Glow
        {
            get
            {
                if (_glow != null) return _glow;
                int n = 128; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "gothic_glow", wrapMode = TextureWrapMode.Clamp }; var px = new Color32[n * n];
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1; float r = Mathf.Sqrt(dx * dx + dy * dy); float a = Mathf.Clamp01(1 - r); a = a * a * (3 - 2 * a); px[y * n + x] = new Color(1, 1, 1, a); }
                t.SetPixels32(px); t.Apply(false); _glow = t; return t;
            }
        }

    }

    /// <summary>A carved frame: optional fill, double brass rule, corner lozenges; optional lancet-arch top.</summary>
    public sealed class GFrame : MaskableGraphic
    {
        public Color Fill = GPal.A(GPal.Lacquer, 0.92f), FillBottom = GPal.A(GPal.OakDark, 0.92f), Border = GPal.Brass; public float Width = 1.5f, Gap = 4f, CornerSize = 4.5f; public bool Arch, Corners = true, NoFill, NoBorder;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            if (!NoFill)
            {
                // linear colour space: a few % of leak over bright stained glass reads as "see-through" after sRGB encoding, so near-opaque fills are made fully opaque
                var fT = Fill; if (fT.a >= 0.88f) fT.a = 1f; var fB = FillBottom; if (fB.a >= 0.88f) fB.a = 1f;
                if (Arch) ArchFill(vh, r, fT * color, fB * color);
                else Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), fB * color, fB * color, fT * color, fT * color);
            }
            if (NoBorder) return;
            var b = Border * color;
            Outline(vh, r, Width, b);
            if (Gap > 0) { var inner = new Rect(r.xMin + Gap, r.yMin + Gap, r.width - Gap * 2, r.height - Gap * 2); Outline(vh, inner, Mathf.Max(1f, Width * 0.5f), GPal.A(b, b.a * 0.45f)); }
            if (Corners) foreach (var c in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) }) Diamond(vh, c, CornerSize, b);
        }
        void Outline(VertexHelper vh, Rect r, float w, Color c)
        {
            if (Arch)
            {
                float h = Mathf.Min(r.width * 0.5f, r.height * 0.35f); var pts = ArchPts(r, h);
                for (int i = 0; i < pts.Count; i++) Line(vh, pts[i], pts[(i + 1) % pts.Count], w, c);
                return;
            }
            Line(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), w, c); Line(vh, new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), w, c);
            Line(vh, new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), w, c); Line(vh, new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), w, c);
        }
        static List<Vector2> ArchPts(Rect r, float h)
        {
            var pts = new List<Vector2> { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax - h) };
            // pointed (lancet) arch: two circle arcs meeting at the apex
            int seg = 14; float R = r.width * 0.85f; var cR = new Vector2(r.xMax - R, r.yMax - h); var cL = new Vector2(r.xMin + R, r.yMax - h);
            float apexY = r.yMax;
            for (int i = 1; i <= seg; i++) { float t = i / (float)seg; var p = Vector2.Lerp(new Vector2(r.xMax, r.yMax - h), new Vector2(r.center.x, apexY), t); float bow = Mathf.Sin(t * Mathf.PI * 0.5f); p.x = Mathf.Lerp(r.xMax, r.center.x, 1 - Mathf.Cos(t * Mathf.PI * 0.5f)); p.y = Mathf.Lerp(r.yMax - h, apexY, bow); pts.Add(p); }
            for (int i = 1; i < seg; i++) { float t = 1 - i / (float)seg; float bow = Mathf.Sin(t * Mathf.PI * 0.5f); var p = new Vector2(Mathf.Lerp(r.xMin, r.center.x, 1 - Mathf.Cos(t * Mathf.PI * 0.5f)), Mathf.Lerp(r.yMax - h, apexY, bow)); pts.Add(p); }
            pts.Add(new Vector2(r.xMin, r.yMax - h));
            return pts;
        }
        static void ArchFill(VertexHelper vh, Rect r, Color top, Color bottom)
        {
            float h = Mathf.Min(r.width * 0.5f, r.height * 0.35f); var pts = ArchPts(r, h); var c = new Vector2(r.center.x, r.yMin + (r.height - h) * 0.5f);
            int i0 = vh.currentVertCount; vh.AddVert(c, Color.Lerp(bottom, top, 0.5f), Vector2.zero);
            foreach (var p in pts) vh.AddVert(p, Color.Lerp(bottom, top, Mathf.InverseLerp(r.yMin, r.yMax, p.y)), Vector2.zero);
            for (int i = 0; i < pts.Count; i++) vh.AddTriangle(i0, i0 + 1 + i, i0 + 1 + (i + 1) % pts.Count);
        }
        static void Diamond(VertexHelper vh, Vector2 c, float s, Color col) { int i = vh.currentVertCount; vh.AddVert(c + new Vector2(0, s), col, Vector2.zero); vh.AddVert(c + new Vector2(s, 0), col, Vector2.zero); vh.AddVert(c + new Vector2(0, -s), col, Vector2.zero); vh.AddVert(c + new Vector2(-s, 0), col, Vector2.zero); vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3); }
        internal static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd) { int i = vh.currentVertCount; vh.AddVert(a, ca, Vector2.zero); vh.AddVert(b, cb, Vector2.zero); vh.AddVert(c, cc, Vector2.zero); vh.AddVert(d, cd, Vector2.zero); vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3); }
        internal static void Line(VertexHelper vh, Vector2 a, Vector2 b, float w, Color c) { var n = (b - a).normalized; var p = new Vector2(-n.y, n.x) * (w * 0.5f); Quad(vh, a - p, b - p, b + p, a + p, c, c, c, c); }
        public void Refresh() => SetVerticesDirty();
    }

    /// <summary>A candle flame: teardrop with a bright core, flickering.</summary>
    public sealed class FlameGraphic : MaskableGraphic
    {
        public float Life = 1f; float _seed; void Awake() { _seed = Random.value * 100; }
        void Update() { SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (Life <= 0.001f) return; var r = rectTransform.rect; float t = Time.unscaledTime * 9f + _seed;
            float sway = (Mathf.PerlinNoise(t * 0.3f, _seed) - 0.5f) * r.width * 0.5f, hk = 0.8f + Mathf.PerlinNoise(_seed, t * 0.4f) * 0.35f;
            var baseC = new Vector2(r.center.x, r.yMin + r.height * 0.18f); float w = r.width * 0.5f * Mathf.Sqrt(Life), h = r.height * hk * Mathf.Sqrt(Life);
            int seg = 16; var outer = GPal.A(GPal.Candle, 0.85f) * color; var core = GPal.A(GPal.Flame, 1f) * color; var tip = GPal.A(new Color(1f, 0.45f, 0.15f), 0f) * color;
            int c0 = vh.currentVertCount; vh.AddVert(baseC + new Vector2(0, h * 0.1f), core, Vector2.zero);
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2; float y = Mathf.Sin(a); float x = Mathf.Cos(a);
                Vector2 p = y > 0 ? new Vector2(x * w * (1 - y * 0.85f) + sway * y, y * h) : new Vector2(x * w, y * w * 0.9f);
                vh.AddVert(baseC + p, y > 0.8f ? tip : outer, Vector2.zero);
            }
            for (int i = 0; i < seg; i++) vh.AddTriangle(c0, c0 + 1 + i, c0 + 2 + i);
        }
    }

    /// <summary>A wax seal: an irregular disc with a darker rim and an inner ring; its letter is a child text.</summary>
    public sealed class SealGraphic : MaskableGraphic
    {
        public Color Wax = GPal.Wax; public int Seed = 1; public float Crack;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; var c = r.center; float R = Mathf.Min(r.width, r.height) * 0.5f; int seg = 40;
            var rim = Color.Lerp(Wax, Color.black, 0.35f) * color; var mid = Wax * color; var hi = Color.Lerp(Wax, Color.white, 0.18f) * color;
            var rng = new System.Random(Seed);
            var rad = new float[seg]; for (int i = 0; i < seg; i++) rad[i] = R * (0.86f + (float)rng.NextDouble() * 0.14f);
            int c0 = vh.currentVertCount; vh.AddVert(c + new Vector2(-R * 0.15f, R * 0.15f), hi, Vector2.zero);
            for (int i = 0; i < seg; i++) { float a = i / (float)seg * Mathf.PI * 2; vh.AddVert(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad[i], rim, Vector2.zero); }
            for (int i = 0; i < seg; i++) vh.AddTriangle(c0, c0 + 1 + i, c0 + 1 + (i + 1) % seg);
            // inner embossed ring
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2, a1 = (i + 1) / (float)seg * Mathf.PI * 2; float r0 = R * 0.62f, r1 = R * 0.68f;
                GFrame.Quad(vh, c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r0, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r0, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r1, c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r1, rim, rim, rim, rim);
            }
            if (Crack > 0)
            {
                var crk = GPal.A(new Color(0.05f, 0.02f, 0.02f), Mathf.Clamp01(Crack)) * color; var cr = new System.Random(Seed * 7 + 3);
                for (int k = 0; k < 5; k++) { var p = c; float ang = (float)cr.NextDouble() * Mathf.PI * 2; for (int s = 0; s < 4; s++) { ang += ((float)cr.NextDouble() - 0.5f) * 1.2f; var q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * R * 0.28f * Mathf.Clamp01(Crack * 1.5f); GFrame.Line(vh, p, q, 2.2f, crk); p = q; } }
            }
        }
        public void Refresh() => SetVerticesDirty();
    }

    /// <summary>A sagging red thread between two points (a quadratic curve).</summary>
    public sealed class ThreadGraphic : MaskableGraphic
    {
        public Vector2 A, B; public float Sag = 40f, Width = 3.5f, Snap = 0f;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); int seg = 24; var mid = (A + B) * 0.5f + Vector2.down * Sag; var col = color;
            Vector2 P(float t) { var a = Vector2.Lerp(A, mid, t); var b = Vector2.Lerp(mid, B, t); return Vector2.Lerp(a, b, t); }
            for (int i = 0; i < seg; i++)
            {
                float t0 = i / (float)seg, t1 = (i + 1) / (float)seg;
                if (Snap > 0 && t0 > 0.5f - Snap * 0.5f && t1 < 0.5f + Snap * 0.5f) continue;
                GFrame.Line(vh, P(t0), P(t1), Width, col);
            }
        }
        public void Set(Vector2 a, Vector2 b) { A = a; B = b; SetVerticesDirty(); }
    }

    /// <summary>A bronze urn silhouette (the vote): foot, swelling body, neck and lip, lit from one side.</summary>
    public sealed class UrnGraphic : MaskableGraphic
    {
        static readonly float[] Profile = { 0.34f, 0.36f, 0.26f, 0.3f, 0.46f, 0.5f, 0.48f, 0.4f, 0.26f, 0.22f, 0.3f, 0.34f };   // half-width at heights 0..1
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; int n = Profile.Length;
            Color dark = new Color(0.16f, 0.1f, 0.05f, 1f) * color, lit = new Color(0.55f, 0.38f, 0.18f, 1f) * color, rim = new Color(0.85f, 0.66f, 0.34f, 1f) * color;
            for (int i = 0; i < n - 1; i++)
            {
                float y0 = r.yMin + r.height * i / (n - 1), y1 = r.yMin + r.height * (i + 1) / (n - 1);
                float w0 = Profile[i] * r.width, w1 = Profile[i + 1] * r.width; float cx = r.center.x;
                // left half lit, right half in shadow
                GFrame.Quad(vh, new Vector2(cx - w0, y0), new Vector2(cx, y0), new Vector2(cx, y1), new Vector2(cx - w1, y1), lit, Color.Lerp(lit, dark, 0.5f), Color.Lerp(lit, dark, 0.5f), lit);
                GFrame.Quad(vh, new Vector2(cx, y0), new Vector2(cx + w0, y0), new Vector2(cx + w1, y1), new Vector2(cx, y1), Color.Lerp(lit, dark, 0.5f), dark, dark, Color.Lerp(lit, dark, 0.5f));
            }
            float top = r.yMax, lip = Profile[n - 1] * r.width; GFrame.Line(vh, new Vector2(r.center.x - lip - 4, top), new Vector2(r.center.x + lip + 4, top), 5f, rim);
            GFrame.Line(vh, new Vector2(r.center.x - Profile[0] * r.width - 4, r.yMin + 2), new Vector2(r.center.x + Profile[0] * r.width + 4, r.yMin + 2), 5f, rim);
        }
    }

    /// <summary>Brass balance: the beam tilts with the jury's trust in the player (left pan 신뢰, right pan 의혹).</summary>
    public sealed class ScalesGraphic : MaskableGraphic
    {
        public float Value = 0.5f; float _shown = 0.5f;
        void Update() { float t = Mathf.MoveTowards(_shown, Value, Time.unscaledDeltaTime * 0.6f); if (Mathf.Abs(t - _shown) > 0.0005f) { _shown = t; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; var br = GPal.Brass * color; var dim = GPal.BrassDim * color;
            var top = new Vector2(r.center.x, r.yMax - r.height * 0.18f); float half = r.width * 0.42f; float ang = (_shown - 0.5f) * 36f * Mathf.Deg2Rad;   // trust high → left (신뢰) pan heavier
            var d = new Vector2(Mathf.Cos(ang), -Mathf.Sin(ang)); var L = top - d * half; var R = top + d * half;
            GFrame.Line(vh, new Vector2(r.center.x, r.yMin + 4), top, 4f, dim);                                    // column
            GFrame.Line(vh, new Vector2(r.center.x - 26, r.yMin + 3), new Vector2(r.center.x + 26, r.yMin + 3), 6f, br); // foot
            GFrame.Line(vh, L, R, 4f, br);                                                                          // beam
            foreach (var p in new[] { L, R })
            {
                var pan = p + Vector2.down * r.height * 0.42f;
                GFrame.Line(vh, p, pan + new Vector2(-18, 0), 1.5f, dim); GFrame.Line(vh, p, pan + new Vector2(18, 0), 1.5f, dim);
                int i = vh.currentVertCount; int seg = 10;
                for (int k = 0; k <= seg; k++) { float a = Mathf.PI + k / (float)seg * Mathf.PI; vh.AddVert(pan + new Vector2(Mathf.Cos(a) * 22, Mathf.Sin(a) * 9), br, Vector2.zero); }
                for (int k = 0; k < seg - 1; k++) vh.AddTriangle(i, i + k + 1, i + k + 2);
            }
            // finial
            int f = vh.currentVertCount; vh.AddVert(top + new Vector2(0, 12), br, Vector2.zero); vh.AddVert(top + new Vector2(6, 0), br, Vector2.zero); vh.AddVert(top + new Vector2(-6, 0), br, Vector2.zero); vh.AddTriangle(f, f + 1, f + 2);
        }
        public (Vector2 left, Vector2 right) Pans()
        {
            var r = rectTransform.rect; var top = new Vector2(r.center.x, r.yMax - r.height * 0.18f); float half = r.width * 0.42f; float ang = (_shown - 0.5f) * 36f * Mathf.Deg2Rad; var d = new Vector2(Mathf.Cos(ang), -Mathf.Sin(ang));
            return (top - d * half + Vector2.down * r.height * 0.42f, top + d * half + Vector2.down * r.height * 0.42f);
        }
    }

    /// <summary>Builders for the court's gothic UI pieces.</summary>
    public static class Goth
    {
        public static GFrame Frame(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 offMin = default, Vector2 offMax = default, bool arch = false, bool fill = true)
        {
            var rt = UIKit.Rect(parent, name, aMin, aMax, offMin, offMax); var f = rt.gameObject.AddComponent<GFrame>(); f.raycastTarget = false; f.Arch = arch; f.NoFill = !fill; return f;
        }

        public static RawImage Parchment(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 offMin = default, Vector2 offMax = default)
        {
            var rt = UIKit.Rect(parent, name, aMin, aMax, offMin, offMax); var im = rt.gameObject.AddComponent<RawImage>(); im.texture = GTex.Parchment; im.raycastTarget = false; return im;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color c, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool bold = true, Vector2? aMin = null, Vector2? aMax = null, Vector2 offMin = default, Vector2 offMax = default)
        {
            var t = UIKit.Text(parent, name, text, size, c, align, bold ? GFont.Serif : GFont.SerifReg, aMin, aMax, offMin, offMax); return t;
        }

        /// <summary>Engraved title: gilt serif with a dark outline.</summary>
        public static TextMeshProUGUI Engraved(Transform parent, string name, string text, float size, Color? c = null)
        {
            var t = UIKit.Text(parent, name, text, size, c ?? GPal.Gilt, TextAlignmentOptions.Center, GFont.Serif); t.textWrappingMode = TextWrappingModes.NoWrap; t.outlineWidth = 0.18f; t.outlineColor = new Color32(20, 10, 4, 255); t.characterSpacing = 6; return t;
        }

        public static RawImage Glow(Transform parent, string name, Color c, Vector2 size, Vector2 pos)
        {
            var rt = TrialFx.Centered(parent, name, size, pos); var im = rt.gameObject.AddComponent<RawImage>(); im.texture = GTex.Glow; im.color = c; im.raycastTarget = false; return im;
        }

        /// <summary>A speaker's name: parchment letters on dark lacquer, a gold rule beneath (high contrast, quiet).</summary>
        public static RectTransform Plate(Transform parent, string name, string text, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax, float size = 26)
        {
            var f = Frame(parent, name, aMin, aMax, offMin, offMax); f.Fill = GPal.A(GPal.Lacquer, 1f); f.FillBottom = GPal.A(new Color(0.07f, 0.045f, 0.03f), 1f); f.NoBorder = true;
            UIKit.Img(f.transform, "Rule", GPal.A(GPal.Gilt, 0.9f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 0), new Vector2(-10, 2));
            UIKit.Img(f.transform, "RuleTop", GPal.A(GPal.BrassDim, 0.7f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -1), new Vector2(-10, 0));
            var t = Text(f.transform, "T", text, size, GPal.Parchment, TextAlignmentOptions.Center, true, Vector2.zero, Vector2.one, new Vector2(14, 2), new Vector2(-14, 0)); t.characterSpacing = 4; t.textWrappingMode = TextWrappingModes.NoWrap;
            return f.rectTransform;
        }

        /// <summary>The house panel: black lacquer with a single gilt hairline and small corner lozenges, over a soft shadow.</summary>
        public static GFrame Panel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 offMin = default, Vector2 offMax = default)
        {
            var sh = UIKit.Rect(parent, name + "Shadow", aMin, aMax, offMin + new Vector2(-18, -26), offMax + new Vector2(18, 10)); var si = sh.gameObject.AddComponent<RawImage>(); si.texture = GTex.Glow; si.color = new Color(0, 0, 0, 0.55f); si.raycastTarget = false;
            var f = Frame(parent, name, aMin, aMax, offMin, offMax); f.Fill = GPal.A(new Color(0.055f, 0.04f, 0.03f), 1f); f.FillBottom = GPal.A(new Color(0.03f, 0.02f, 0.016f), 1f); f.Border = GPal.A(GPal.Brass, 0.85f); f.Width = 1.5f; f.Gap = 5f;
            return f;
        }

        /// <summary>Key prompts at the bottom right: small lacquer key caps with gilt letters, each followed by what it does.
        /// Input "key|what · key|what"; a segment without '|' is shown as plain text.</summary>
        public static RectTransform KeyBar(Transform parent, string spec, float size = 19)
        {
            var bar = UIKit.Rect(parent, "Keys", new Vector2(1, 0), new Vector2(1, 0)); bar.pivot = new Vector2(1, 0); bar.sizeDelta = new Vector2(10, 44); bar.anchoredPosition = new Vector2(-34, 22);
            var parts = (spec ?? "").Split(new[] { " · " }, System.StringSplitOptions.RemoveEmptyEntries); float x = 0;
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                var seg = parts[i]; int bar1 = seg.IndexOf('|'); string key = bar1 >= 0 ? seg.Substring(0, bar1) : null, what = bar1 >= 0 ? seg.Substring(bar1 + 1) : seg;
                var wt = Text(bar, "W" + i, what, size, GPal.A(GPal.Bone, 0.85f), TextAlignmentOptions.MidlineLeft, false, new Vector2(1, 0), new Vector2(1, 1)); wt.textWrappingMode = TextWrappingModes.NoWrap;
                float ww = wt.GetPreferredValues(what, 2000, 40).x; wt.rectTransform.offsetMin = new Vector2(-x - ww, 0); wt.rectTransform.offsetMax = new Vector2(-x, 0); x += ww + 10;
                if (key != null)
                {
                    var kt = Text(bar, "K" + i, key, size * 0.86f, GPal.Gilt, TextAlignmentOptions.Center, true, new Vector2(1, 0.5f), new Vector2(1, 0.5f)); kt.textWrappingMode = TextWrappingModes.NoWrap;
                    float kw = Mathf.Max(32, kt.GetPreferredValues(key, 2000, 40).x + 20);
                    var cap = Frame(bar, "Cap" + i, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-x - kw, -16), new Vector2(-x, 16)); cap.Fill = GPal.A(new Color(0.08f, 0.055f, 0.035f), 1f); cap.FillBottom = GPal.A(GPal.Lacquer, 1f); cap.Border = GPal.A(GPal.Brass, 0.8f); cap.Width = 1.2f; cap.Gap = 0; cap.Corners = false;
                    kt.transform.SetAsLastSibling(); kt.rectTransform.offsetMin = new Vector2(-x - kw, -16); kt.rectTransform.offsetMax = new Vector2(-x, 16);
                    x += kw + 8;
                }
                x += i > 0 ? 26 : 0;
            }
            // a soft dark bed so the prompts read over any part of the courtroom
            var bed = UIKit.Rect(bar, "Bed", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-x - 60, -18), new Vector2(40, 18)); var bi = bed.gameObject.AddComponent<RawImage>(); bi.texture = GTex.Glow; bi.color = new Color(0, 0, 0, 0.55f); bi.raycastTarget = false; bed.SetAsFirstSibling();
            bar.sizeDelta = new Vector2(x, 44);
            return bar;
        }

        /// <summary>The round's clock: one taper laid on its side, burning from the right with the flame riding its end, divided into
        /// candle-lengths by gilt ticks. It reads at a glance and deepens to oxblood in its last quarter.</summary>
        public sealed class CandleRow
        {
            readonly RectTransform _root, _fill, _flameRt; readonly Image _fillImg, _core; readonly FlameGraphic _flame; readonly RawImage _glow; readonly int _n; float _shown = 1; int _lastSeg;
            public float Frac => _shown;
            /// <summary>Called with the number of candle-lengths left whenever one burns out.</summary>
            public System.Action<int> OnSegmentOut;
            public CandleRow(Transform parent, int n, Vector2 center, float width = 440)
            {
                _n = Mathf.Max(1, n);
                _root = TrialFx.Centered(parent, "Taper", new Vector2(width, 40), center);
                UIKit.Img(_root, "Holder", GPal.A(GPal.BrassDim, 0.9f), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(-4, -7), new Vector2(4, 7));
                UIKit.Img(_root, "Track", new Color(0.09f, 0.06f, 0.04f, 1f), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(-2, -5), new Vector2(2, 5));
                _fill = UIKit.Rect(_root, "Wax", new Vector2(0, 0.5f), new Vector2(0, 0.5f)); _fill.pivot = new Vector2(0, 0.5f); _fill.sizeDelta = new Vector2(width, 10);
                _fillImg = _fill.gameObject.AddComponent<Image>(); _fillImg.color = GPal.Bone; _fillImg.raycastTarget = false;
                _core = UIKit.Img(_fill, "Sheen", new Color(1, 1, 1, 0.35f), new Vector2(0, 0.62f), new Vector2(1, 0.8f));
                for (int i = 1; i < _n; i++) UIKit.Img(_root, "Tick" + i, GPal.A(GPal.Gilt, 0.85f), new Vector2(i / (float)_n, 0.5f), new Vector2(i / (float)_n, 0.5f), new Vector2(-1, -10), new Vector2(1, 10));
                _glow = Goth.Glow(_root, "Glow", GPal.A(GPal.Candle, 0.35f), new Vector2(150, 150), Vector2.zero);
                _flameRt = UIKit.Rect(_root, "Flame", new Vector2(0, 0.5f), new Vector2(0, 0.5f)); _flameRt.sizeDelta = new Vector2(18, 38);
                _flame = _flameRt.gameObject.AddComponent<FlameGraphic>(); _flame.raycastTarget = false;
                _lastSeg = _n;
            }
            public void Set(float frac)
            {
                _shown = Mathf.Clamp01(frac); float w = _root.rect.width, x = w * _shown;
                _fill.sizeDelta = new Vector2(x, 10);
                _fillImg.color = _shown < 0.25f ? Color.Lerp(GPal.Wax, GPal.Bone, Mathf.Clamp01(_shown / 0.25f) * 0.6f) : GPal.Bone;
                _flameRt.anchoredPosition = new Vector2(x, 14); _glow.rectTransform.anchoredPosition = new Vector2(x - w * 0.5f, 8);
                bool lit = _shown > 0.001f; _flame.Life = lit ? 1 : 0;
                _glow.color = GPal.A(GPal.Candle, lit ? 0.3f + Mathf.PerlinNoise(Time.unscaledTime * (_shown < 0.25f ? 7f : 3f), 1.7f) * 0.14f : 0f);
                int seg = Mathf.CeilToInt(_shown * _n - 0.0001f);
                if (seg < _lastSeg) { _lastSeg = seg; OnSegmentOut?.Invoke(seg); } else if (seg > _lastSeg) _lastSeg = seg;
            }
            public void Flare() { if (_shown > 0) _flameRt.localScale = Vector3.one * 1.8f; }
            public void Relax(float dt) { _flameRt.localScale = Vector3.Lerp(_flameRt.localScale, Vector3.one, dt * 4); }
        }

        /// <summary>A court button: lacquer with a brass hairline; hovering warms it to oxblood with a gilt edge and a brass tick,
        /// a click presses it in with a soft stamp. (The mansion's slanted neon buttons never appear in the court.)</summary>
        public static GBtn Button(Transform parent, string label, System.Action onClick, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax, float size = 24, bool primary = false)
        {
            var f = Frame(parent, "Btn", aMin, aMax, offMin, offMax); f.raycastTarget = true; f.Gap = 0; f.Corners = primary; f.Width = 1.4f;
            var t = Text(f.transform, "T", label, size, GPal.Bone, TextAlignmentOptions.Center, primary, Vector2.zero, Vector2.one, new Vector2(16, 4), new Vector2(-16, -4)); t.enableAutoSizing = true; t.fontSizeMin = Mathf.Min(14, size); t.fontSizeMax = size; t.richText = true;
            var b = f.gameObject.AddComponent<GBtn>(); b.Frame = f; b.Label = t; b.OnClick = onClick; b.Primary = primary; b.Paint();
            return b;
        }

        public static string Initial(string id) { var g = string.IsNullOrEmpty(id) ? null : Cast.GivenOf(id); return string.IsNullOrEmpty(g) ? "" : g.Substring(0, 1); }

        /// <summary>Chip tones: a plain kind word, a gold one (중요), a smoky one (전해 들음).</summary>
        public enum ChipTone { Plain, Gold, Smoke }

        /// <summary>A small framed word — a clue's kind (시신 · 현장 · 물건 · 증언 · 기록), 중요, 전해 들음, a claim's status.
        /// Anchored at the left-middle of its parent, starting at x; returns the chip (its width is sizeDelta.x).</summary>
        public static RectTransform KindChip(Transform parent, string text, ChipTone tone, float x, float size = 16, float h = 26)
        {
            var f = Frame(parent, "Chip", new Vector2(0, 0.5f), new Vector2(0, 0.5f)); f.Gap = 0; f.Corners = false; f.Width = 1.1f;
            f.Fill = tone == ChipTone.Gold ? GPal.A(new Color(0.2f, 0.12f, 0.04f), 1f) : GPal.A(new Color(0.07f, 0.05f, 0.035f), 1f); f.FillBottom = GPal.A(GPal.Lacquer, 1f);
            f.Border = tone == ChipTone.Gold ? GPal.A(GPal.Gilt, 0.95f) : tone == ChipTone.Smoke ? GPal.A(GPal.Smoke, 0.7f) : GPal.A(GPal.Brass, 0.55f);
            var t = Text(f.transform, "T", text, size, tone == ChipTone.Gold ? GPal.Gilt : tone == ChipTone.Smoke ? GPal.Smoke : GPal.A(GPal.Bone, 0.92f), TextAlignmentOptions.Center, true, Vector2.zero, Vector2.one);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            float w = Mathf.Ceil(t.GetPreferredValues(text, 2000, h).x) + 18;
            var rt = f.rectTransform; rt.pivot = new Vector2(0, 0.5f); rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, 0);
            return rt;
        }

        /// <summary>The claim status words used in the court: 반박됨 · 흔들림 · 뒷받침됨 (open claims carry none).</summary>
        public static string StatusWord(string status) => status == "refuted" ? "반박됨" : status == "limited" || status == "conditional" ? "흔들림" : status == "supported" ? "뒷받침됨" : null;

        /// <summary>An oval gilt medallion with a portrait (or the initial when no portrait exists).</summary>
        public static RectTransform Medallion(Transform parent, string name, string id, Vector2 size, Vector2 pos)
        {
            var rt = TrialFx.Centered(parent, name, size, pos);
            var ring = rt.gameObject.AddComponent<RingGraphic>(); ring.Thickness = 7; ring.color = GPal.Brass; ring.raycastTarget = false;
            var inner = TrialFx.Centered(rt, "Face", size - new Vector2(14, 14)); var mask = inner.gameObject.AddComponent<RingGraphic>(); mask.Thickness = size.x; mask.color = Color.white; mask.raycastTarget = false;
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true; mask.color = GPal.Lacquer;
            var face = UIKit.Rect(inner, "P", Vector2.zero, Vector2.one, new Vector2(-10, -20), new Vector2(10, 10)); var raw = face.gameObject.AddComponent<RawImage>(); raw.raycastTarget = false;
            var tex = TrialPortraits.Get(id); raw.texture = tex; raw.color = tex != null ? new Color(1, 0.94f, 0.86f) : GPal.Lacquer; raw.uvRect = new Rect(0.05f, 0.1f, 0.9f, 0.85f);
            if (tex == null) { var t = Goth.Text(inner, "I", Goth.Initial(id), size.y * 0.4f, GPal.Brass, TextAlignmentOptions.Center); }
            return rt;
        }
    }
}

namespace BL23.Game
{
    /// <summary>Behaviour of <see cref="Goth.Button"/>.</summary>
    public sealed class GBtn : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler, UnityEngine.EventSystems.IPointerClickHandler
    {
        public GFrame Frame; public TMPro.TextMeshProUGUI Label; public System.Action OnClick; public bool Primary, Selected, Interactable = true, Hover;
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { Hover = true; Paint(); if (Interactable) TrialFx.Sound("plate_hover", 0.35f); }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { Hover = false; Paint(); }
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e) { Click(); }
        public void Click() { if (!Interactable) { TrialFx.Sound("ui_cancel", 0.4f); return; } TrialFx.Sound(Primary ? "wax_stamp" : "quill", Primary ? 0.6f : 0.5f); transform.localScale = Vector3.one * 0.97f; OnClick?.Invoke(); }
        void Update() { transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * (Hover && Interactable ? 1.02f : 1f), Time.unscaledDeltaTime * 12); }
        public void Paint()
        {
            if (Frame == null) return;
            bool warm = Interactable && (Hover || Selected);
            Frame.Fill = !Interactable ? GPal.A(new Color(0.05f, 0.04f, 0.035f), 1f) : warm ? GPal.A(Primary ? GPal.Wax : GPal.Oxblood, 1f) : GPal.A(Primary ? GPal.Oxblood : new Color(0.08f, 0.055f, 0.035f), 1f);
            Frame.FillBottom = GPal.A(Primary ? new Color(0.1f, 0.015f, 0.025f) : GPal.Lacquer, 1f);
            Frame.Border = !Interactable ? GPal.A(GPal.Smoke, 0.25f) : warm ? GPal.Gilt : GPal.A(GPal.Brass, 0.75f);
            Frame.Refresh();
            if (Label != null) Label.color = !Interactable ? GPal.A(GPal.Smoke, 0.6f) : warm || Primary ? GPal.Gilt : GPal.Bone;
        }
    }
}
