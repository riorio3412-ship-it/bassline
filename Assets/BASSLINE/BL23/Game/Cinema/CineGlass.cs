using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BL23.Game.Cinema
{
    /// <summary>Convex polygon helpers for the leaded-glass panes (all in local UI units, counter-clockwise).</summary>
    public static class Poly
    {
        public static Vector2 Centroid(IList<Vector2> p) { Vector2 c = Vector2.zero; foreach (var v in p) c += v; return p.Count > 0 ? c / p.Count : c; }
        public static Rect Bounds(IList<Vector2> p)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var v in p) { x0 = Mathf.Min(x0, v.x); y0 = Mathf.Min(y0, v.y); x1 = Mathf.Max(x1, v.x); y1 = Mathf.Max(y1, v.y); }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }
        public static float Area(IList<Vector2> p) { float a = 0; for (int i = 0; i < p.Count; i++) { var u = p[i]; var v = p[(i + 1) % p.Count]; a += u.x * v.y - v.x * u.y; } return a * 0.5f; }
        public static List<Vector2> CCW(List<Vector2> p) { if (Area(p) < 0) p.Reverse(); return p; }

        /// <summary>Shrink a convex polygon by d (each edge moves inward; corners are re-intersected).</summary>
        public static List<Vector2> Inset(IList<Vector2> p, float d)
        {
            int n = p.Count; var res = new List<Vector2>(n); if (n < 3) return new List<Vector2>(p);
            for (int i = 0; i < n; i++)
            {
                var a0 = p[(i - 1 + n) % n]; var a1 = p[i]; var b1 = p[(i + 1) % n];
                var na = Normal(a0, a1); var nb = Normal(a1, b1);   // outward normals (ccw)
                var pa = a0 - na * d; var da = a1 - a0; var pb = a1 - nb * d; var db = b1 - a1;
                res.Add(Intersect(pa, da, pb, db, a1 - (na + nb).normalized * d));
            }
            return res;
        }
        public static Vector2 Normal(Vector2 a, Vector2 b) { var e = (b - a).normalized; return new Vector2(e.y, -e.x); }
        static Vector2 Intersect(Vector2 p, Vector2 r, Vector2 q, Vector2 s, Vector2 fallback)
        {
            float den = r.x * s.y - r.y * s.x; if (Mathf.Abs(den) < 1e-5f) return fallback;
            float t = ((q.x - p.x) * s.y - (q.y - p.y) * s.x) / den; return p + r * t;
        }

        /// <summary>Clip a convex polygon to the half-plane dot(v, n) &lt;= k.</summary>
        public static List<Vector2> Clip(IList<Vector2> p, Vector2 n, float k)
        {
            var res = new List<Vector2>(p.Count + 2); int m = p.Count;
            for (int i = 0; i < m; i++)
            {
                var a = p[i]; var b = p[(i + 1) % m]; float da = Vector2.Dot(a, n) - k, db = Vector2.Dot(b, n) - k;
                if (da <= 0) res.Add(a);
                if ((da < 0 && db > 0) || (da > 0 && db < 0)) res.Add(Vector2.Lerp(a, b, da / (da - db)));
            }
            return res;
        }

        /// <summary>A pointed (lancet) arch window inside rect r: straight sides, two arcs meeting at the apex.</summary>
        public static List<Vector2> Lancet(Rect r, float archFrac = 0.42f, int seg = 12)
        {
            // a true pointed arch needs the rise to exceed half the span; keep it a lancet (never a flattened segment with a dip)
            float h = Mathf.Min(Mathf.Max(r.height * archFrac, r.width * 0.66f), r.height * 0.72f); h = Mathf.Max(h, r.width * 0.52f); float spring = r.yMax - h;
            var pts = new List<Vector2> { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, spring) };
            // equilateral-ish arch: each side is an arc centred on the opposite springing line
            float w = r.width; float R = (h * h + w * w * 0.25f) / Mathf.Max(1f, w);   // radius so the two arcs meet at the apex
            var cR = new Vector2(r.xMax - R, spring); var cL = new Vector2(r.xMin + R, spring);
            float aR0 = 0f, aR1 = Mathf.Acos(Mathf.Clamp((R - w * 0.5f) / R, -1f, 1f));
            for (int i = 1; i < seg; i++) { float a = Mathf.Lerp(aR0, aR1, i / (float)seg); pts.Add(cR + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R); }
            pts.Add(new Vector2(r.center.x, r.yMax));
            for (int i = seg - 1; i >= 1; i--) { float a = Mathf.Lerp(aR0, aR1, i / (float)seg); pts.Add(cL + new Vector2(-Mathf.Cos(a), Mathf.Sin(a)) * R); }
            pts.Add(new Vector2(r.xMin, spring));
            return pts;
        }
        public static List<Vector2> Circle(Vector2 c, float rad, int seg = 40)
        {
            var pts = new List<Vector2>(seg); for (int i = 0; i < seg; i++) { float a = i / (float)seg * Mathf.PI * 2f; pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad); } return pts;
        }
        public static List<Vector2> Scaled(IList<Vector2> p, Vector2 about, float k) { var res = new List<Vector2>(p.Count); foreach (var v in p) res.Add(about + (v - about) * k); return res; }
    }

    /// <summary>
    /// A pane of the window: a live camera image (RenderTexture) mapped onto a convex polygon. Soft inner vignette through
    /// vertex colours; 'Wipe' reveals it along a direction (light sweeping across glass) with a gilt leading edge.
    /// </summary>
    public sealed class PaneGraphic : MaskableGraphic
    {
        Texture _tex;
        /// <summary>The live image (a RenderTexture). Changing it rebinds the material.</summary>
        public Texture Tex { get => _tex; set { if (_tex == value) return; _tex = value; SetMaterialDirty(); } }
        public List<Vector2> Shape = new List<Vector2>();   // local units
        public Rect Uv = new Rect(0, 0, 1, 1); public float Wipe = 1f; public Vector2 WipeDir = new Vector2(1f, 0.35f);
        public float Vignette = 0.3f, Dim = 0f; public Color Edge = new Color(1f, 0.86f, 0.55f, 1f);
        public override Texture mainTexture => _tex != null ? _tex : s_WhiteTexture;
        public void Refresh() { SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (Shape == null || Shape.Count < 3) return;
            var bb = Poly.Bounds(Shape); var poly = Shape;
            var n = WipeDir.normalized; float lo = float.MaxValue, hi = float.MinValue;
            foreach (var v in Shape) { float d = Vector2.Dot(v, n); lo = Mathf.Min(lo, d); hi = Mathf.Max(hi, d); }
            float k = Mathf.Lerp(lo - 1f, hi + 1f, Mathf.Clamp01(Wipe));
            if (Wipe < 0.999f) poly = Poly.Clip(Shape, n, k);
            if (poly.Count < 3) return;
            var c = Poly.Centroid(poly); float lum = 1f - Dim;
            Color full = color * new Color(lum, lum, lum, 1f); Color edge = color * new Color(lum * (1f - Vignette), lum * (1f - Vignette), lum * (1f - Vignette), 1f);
            Vector2 UV(Vector2 p) => new Vector2(Uv.xMin + (p.x - bb.xMin) / Mathf.Max(1f, bb.width) * Uv.width, Uv.yMin + (p.y - bb.yMin) / Mathf.Max(1f, bb.height) * Uv.height);
            int i0 = vh.currentVertCount; vh.AddVert(c, full, UV(c));
            int m = poly.Count;
            for (int i = 0; i < m; i++) { var inner = Vector2.Lerp(c, poly[i], 0.62f); vh.AddVert(inner, full, UV(inner)); }
            for (int i = 0; i < m; i++) vh.AddVert(poly[i], edge, UV(poly[i]));
            for (int i = 0; i < m; i++)
            {
                int a = i, b = (i + 1) % m;
                vh.AddTriangle(i0, i0 + 1 + a, i0 + 1 + b);
                vh.AddTriangle(i0 + 1 + a, i0 + 1 + m + a, i0 + 1 + m + b); vh.AddTriangle(i0 + 1 + a, i0 + 1 + m + b, i0 + 1 + b);
            }
        }
        public float WipeK()
        {
            var n = WipeDir.normalized; float lo = float.MaxValue, hi = float.MinValue;
            foreach (var v in Shape) { float d = Vector2.Dot(v, n); lo = Mathf.Min(lo, d); hi = Mathf.Max(hi, d); }
            return Mathf.Lerp(lo - 1f, hi + 1f, Mathf.Clamp01(Wipe));
        }
    }

    /// <summary>The wipe's leading edge on a pane: a thin band of warm light moving across the glass.</summary>
    public sealed class WipeGlow : MaskableGraphic
    {
        public PaneGraphic Pane; public float BandW = 26f;
        public void Refresh() { SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (Pane == null || Pane.Shape == null || Pane.Shape.Count < 3 || Pane.Wipe <= 0.001f || Pane.Wipe >= 0.999f) return;
            var n = Pane.WipeDir.normalized; float k = Pane.WipeK();
            var e = Poly.Clip(Poly.Clip(Pane.Shape, n, k), -n, -(k - BandW)); if (e.Count < 3) return;
            int j0 = vh.currentVertCount;
            foreach (var v in e) { float a = Mathf.Clamp01(1f - (k - Vector2.Dot(v, n)) / BandW); vh.AddVert(v, new Color(color.r, color.g, color.b, a * a * color.a), Vector2.zero); }
            for (int i = 1; i + 1 < e.Count; i++) vh.AddTriangle(j0, j0 + i, j0 + i + 1);
        }
    }

    /// <summary>
    /// The lead came around a pane: a dark iron band with a gilt inner hairline and bosses at the corners. 'Draw' (0..1)
    /// traces it on around the perimeter.
    /// </summary>
    public sealed class LeadFrame : MaskableGraphic
    {
        public List<Vector2> Shape = new List<Vector2>(); public float Width = 10f, Draw = 1f; public bool Bosses = true;
        public Color Lead = new Color(0.035f, 0.03f, 0.028f, 1f), Gilt = new Color(0.86f, 0.68f, 0.36f, 1f), Sheen = new Color(0.3f, 0.26f, 0.22f, 1f);
        public void Refresh() { SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); int n = Shape?.Count ?? 0; if (n < 2) return;
            float per = 0; for (int i = 0; i < n; i++) per += (Shape[(i + 1) % n] - Shape[i]).magnitude;
            float left = per * Mathf.Clamp01(Draw); float w = Width * 0.5f;
            var lead = Lead * color; var gilt = Gilt * color; var sheen = Sheen * color;
            for (int i = 0; i < n && left > 0.01f; i++)
            {
                var a = Shape[i]; var b = Shape[(i + 1) % n]; float len = (b - a).magnitude; if (len < 0.01f) continue;
                float useLen = Mathf.Min(len, left); left -= useLen; var e = (b - a) / len; var bb = a + e * useLen;
                var no = new Vector2(e.y, -e.x);   // outward for ccw
                var ea = a - e * w * 0.4f; var eb = bb + e * w * 0.4f;
                Band(vh, ea, eb, no, -w, w, lead);
                Band(vh, ea, eb, no, w - 2.2f, w - 0.8f, sheen);                         // outer sheen
                Band(vh, a, bb, no, -w - 0.2f, -w + 1.6f, gilt);                           // gilt hairline on the glass side
            }
            if (!Bosses) return;
            float walked = 0;
            for (int i = 0; i < n; i++)
            {
                var prev = Shape[(i - 1 + n) % n]; var cur = Shape[i]; var next = Shape[(i + 1) % n];
                float turn = Vector2.Angle(cur - prev, next - cur);
                if (turn > 28f && walked <= per * Draw + 0.5f) Boss(vh, cur, w * 1.05f, lead, gilt);
                walked += (next - cur).magnitude;
            }
        }
        static void Band(VertexHelper vh, Vector2 a, Vector2 b, Vector2 n, float o0, float o1, Color c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a + n * o0, c, Vector2.zero); vh.AddVert(b + n * o0, c, Vector2.zero); vh.AddVert(b + n * o1, c, Vector2.zero); vh.AddVert(a + n * o1, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
        /// <summary>A small quatrefoil boss where the cames meet.</summary>
        static void Boss(VertexHelper vh, Vector2 c, float r, Color lead, Color gilt)
        {
            Disc(vh, c, r * 1.25f, lead, 14);
            for (int k = 0; k < 4; k++) { float a = k * Mathf.PI * 0.5f + Mathf.PI * 0.25f; Disc(vh, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.45f, r * 0.42f, gilt, 10); }
            Disc(vh, c, r * 0.3f, lead, 8);
        }
        static void Disc(VertexHelper vh, Vector2 c, float r, Color col, int seg)
        {
            int i0 = vh.currentVertCount; vh.AddVert(c, col, Vector2.zero);
            for (int i = 0; i < seg; i++) { float a = i / (float)seg * Mathf.PI * 2f; vh.AddVert(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, col, Vector2.zero); }
            for (int i = 0; i < seg; i++) vh.AddTriangle(i0, i0 + 1 + i, i0 + 1 + (i + 1) % seg);
        }
    }

    /// <summary>
    /// The window's border: a band of small, deep-coloured glass quarries in lead (ruby, lapis, amber, bottle green), lit from
    /// behind so they glow faintly; quatrefoil squares at the corners. Muted, never bright.
    /// </summary>
    public sealed class GlassBorder : MaskableGraphic
    {
        public float Band = 26f; public float Glow = 1f; public int Seed = 3;
        static readonly Color[] Jewels = { new Color(0.42f, 0.05f, 0.07f), new Color(0.07f, 0.12f, 0.34f), new Color(0.5f, 0.3f, 0.07f), new Color(0.06f, 0.24f, 0.14f), new Color(0.26f, 0.09f, 0.3f) };
        public void Refresh() { SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; float b = Band; var lead = new Color(0.03f, 0.025f, 0.022f, color.a);
            Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMin + b), new Vector2(r.xMin, r.yMin + b), lead, lead);
            Quad(vh, new Vector2(r.xMin, r.yMax - b), new Vector2(r.xMax, r.yMax - b), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), lead, lead);
            Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin + b, r.yMin), new Vector2(r.xMin + b, r.yMax), new Vector2(r.xMin, r.yMax), lead, lead);
            Quad(vh, new Vector2(r.xMax - b, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax - b, r.yMax), lead, lead);
            var rng = new System.Random(Seed); float g = 3f;
            void Run(Vector2 from, Vector2 to, Vector2 across)
            {
                float len = (to - from).magnitude; var e = (to - from) / len; int count = Mathf.Max(1, Mathf.RoundToInt(len / (b * 1.6f))); float step = len / count;
                for (int i = 0; i < count; i++)
                {
                    var a = from + e * (i * step + g); var z = from + e * ((i + 1) * step - g);
                    var j = Jewels[(i + rng.Next(2)) % Jewels.Length]; float lum = (0.28f + 0.14f * (float)rng.NextDouble()) * Glow; var col = Color.Lerp(j, new Color(0.2f, 0.16f, 0.13f), 0.35f) * lum; col.a = color.a;
                    var hi = Color.Lerp(col, new Color(0.55f, 0.42f, 0.28f, color.a), 0.22f); hi.a = color.a;
                    Quad(vh, a + across * g, z + across * g, z + across * (b - g), a + across * (b - g), col, hi);
                }
            }
            Run(new Vector2(r.xMin + b, r.yMin), new Vector2(r.xMax - b, r.yMin), Vector2.up);
            Run(new Vector2(r.xMin + b, r.yMax - b), new Vector2(r.xMax - b, r.yMax - b), Vector2.up);
            Run(new Vector2(r.xMin, r.yMin + b), new Vector2(r.xMin, r.yMax - b), Vector2.right);
            Run(new Vector2(r.xMax - b, r.yMin + b), new Vector2(r.xMax - b, r.yMax - b), Vector2.right);
            var gilt = new Color(0.8f, 0.62f, 0.32f, color.a);
            foreach (var c in new[] { new Vector2(r.xMin + b * 0.5f, r.yMin + b * 0.5f), new Vector2(r.xMax - b * 0.5f, r.yMin + b * 0.5f), new Vector2(r.xMax - b * 0.5f, r.yMax - b * 0.5f), new Vector2(r.xMin + b * 0.5f, r.yMax - b * 0.5f) })
                for (int k = 0; k < 4; k++) { float a = k * Mathf.PI * 0.5f; Disc(vh, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * b * 0.2f, b * 0.2f, gilt); }
            // inner gilt rule
            var ir = new Rect(r.xMin + b, r.yMin + b, r.width - 2 * b, r.height - 2 * b); float t = 1.4f;
            Quad(vh, new Vector2(ir.xMin, ir.yMin - t), new Vector2(ir.xMax, ir.yMin - t), new Vector2(ir.xMax, ir.yMin), new Vector2(ir.xMin, ir.yMin), gilt, gilt);
            Quad(vh, new Vector2(ir.xMin, ir.yMax), new Vector2(ir.xMax, ir.yMax), new Vector2(ir.xMax, ir.yMax + t), new Vector2(ir.xMin, ir.yMax + t), gilt, gilt);
            Quad(vh, new Vector2(ir.xMin - t, ir.yMin), new Vector2(ir.xMin, ir.yMin), new Vector2(ir.xMin, ir.yMax), new Vector2(ir.xMin - t, ir.yMax), gilt, gilt);
            Quad(vh, new Vector2(ir.xMax, ir.yMin), new Vector2(ir.xMax + t, ir.yMin), new Vector2(ir.xMax + t, ir.yMax), new Vector2(ir.xMax, ir.yMax), gilt, gilt);
        }
        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color edge, Color mid)
        {
            int i = vh.currentVertCount; var m = (a + b + c + d) * 0.25f;
            vh.AddVert(m, mid, Vector2.zero); vh.AddVert(a, edge, Vector2.zero); vh.AddVert(b, edge, Vector2.zero); vh.AddVert(c, edge, Vector2.zero); vh.AddVert(d, edge, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3); vh.AddTriangle(i, i + 3, i + 4); vh.AddTriangle(i, i + 4, i + 1);
        }
        static void Disc(VertexHelper vh, Vector2 c, float r, Color col)
        {
            int i0 = vh.currentVertCount; vh.AddVert(c, col, Vector2.zero); int seg = 10;
            for (int i = 0; i < seg; i++) { float a = i / (float)seg * Mathf.PI * 2f; vh.AddVert(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, col, Vector2.zero); }
            for (int i = 0; i < seg; i++) vh.AddTriangle(i0, i0 + 1 + i, i0 + 1 + (i + 1) % seg);
        }
    }
}
