using System;
using System.Collections.Generic;
using BL23.Game.Characters;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Procedurally paints the anime face atlas (1024x2048: 16 eyes, 16 mouths, 8 brows) and the face FX sheet (1024x1024: base, blush, gloom, tears).
    /// All art is drawn with anti-aliased 2D signed distance shapes.
    /// </summary>
    public sealed partial class FacePainter
    {
        public Color Iris = new Color(0.25f, 0.2f, 0.18f);
        public Color Hair = new Color(0.1f, 0.08f, 0.08f);
        public Color Skin = new Color(0.95f, 0.84f, 0.77f);
        public float Sharp = 0.5f;      // 0 round .. 1 sharp
        public bool Female;
        public bool GoldTooth;
        public bool LashesHeavy;
        public float CuteBlush;          // constant base blush 0..1
        public float EyeLidTilt;         // +: outer corner up
        public float BrowThick = 1f;
        public float NosePos = 0.3f;     // v of nose mark in face uv

        // ------------------------------------------------------------------ canvas
        sealed class Canvas
        {
            public int W, H;
            public Color[] P;
            public Canvas(int w, int h) { W = w; H = h; P = new Color[w * h]; }
            // premultiplied "over"
            public void Blend(int x, int y, Color c, float a)
            {
                if (a <= 0f || x < 0 || y < 0 || x >= W || y >= H) return;
                a = Mathf.Clamp01(a * c.a);
                int i = y * W + x;
                Color d = P[i];
                float oa = a + d.a * (1f - a);
                if (oa <= 1e-6f) return;
                Color o = (c * a + d * d.a * (1f - a)) / oa;
                o.a = oa;
                P[i] = o;
            }
            public void Erase(int x, int y, float a)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                int i = y * W + x; var d = P[i]; d.a *= 1f - Mathf.Clamp01(a); P[i] = d;
            }
        }

        /// <summary>Region of the canvas being painted, with local coords (0..1, y up).</summary>
        struct Cell
        {
            public Canvas C; public int X0, Y0, W, H;
            public float Px => 1f / W; // one pixel in local x units
        }

        delegate float Sd(Vector2 p);
        delegate Color Col(Vector2 p);

        static void Fill(Cell c, Sd sd, Col col, float soft = 1.2f, Rect? bounds = null)
        {
            Rect r = bounds ?? new Rect(0, 0, 1, 1);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(r.xMin * c.W) - 2), x1 = Mathf.Min(c.W - 1, Mathf.CeilToInt(r.xMax * c.W) + 2);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(r.yMin * c.H) - 2), y1 = Mathf.Min(c.H - 1, Mathf.CeilToInt(r.yMax * c.H) + 2);
            float px = 1f / c.W;
            float aspect = (float)c.H / c.W; // shapes are defined in "x units" so circles stay round
            const int T = 8;
            float tileR = T * 0.75f * px + px * soft * 2f;
            for (int ty = y0; ty <= y1; ty += T)
                for (int tx = x0; tx <= x1; tx += T)
                {
                    var tc = new Vector2((tx + T * 0.5f) / c.W, (ty + T * 0.5f) / c.H * aspect);
                    if (sd(tc) > tileR) continue;
                    for (int y = ty; y < ty + T && y <= y1; y++)
                        for (int x = tx; x < tx + T && x <= x1; x++)
                        {
                            var p = new Vector2((x + 0.5f) / c.W, (y + 0.5f) / c.H * aspect);
                            float d = sd(p);
                            float a = Mathf.Clamp01(0.5f - d / (px * soft));
                            if (a <= 0f) continue;
                            c.C.Blend(c.X0 + x, c.Y0 + y, col(p), a);
                        }
                }
        }

        static void FillC(Cell c, Sd sd, Color color, float soft = 1.2f) => Fill(c, sd, _ => color, soft);
        static void Fill(Cell c, Sd sd, Color color, float soft = 1.2f) => Fill(c, sd, _ => color, soft);

        // ------------------------------------------------------------------ 2D SDF helpers (x units)
        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        static float Ellipse(Vector2 p, Vector2 c, Vector2 r, float rotDeg = 0f)
        {
            Vector2 q = p - c;
            if (rotDeg != 0f) { float a = -rotDeg * Mathf.Deg2Rad; q = new Vector2(q.x * Mathf.Cos(a) - q.y * Mathf.Sin(a), q.x * Mathf.Sin(a) + q.y * Mathf.Cos(a)); }
            float k0 = new Vector2(q.x / r.x, q.y / r.y).magnitude;
            float k1 = new Vector2(q.x / (r.x * r.x), q.y / (r.y * r.y)).magnitude;
            if (k1 < 1e-9f) return -Mathf.Min(r.x, r.y);
            return k0 * (k0 - 1f) / k1;
        }
        static float Seg(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            Vector2 ab = b - a; float l2 = ab.sqrMagnitude;
            t = l2 > 1e-12f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2) : 0f;
            return (p - (a + ab * t)).magnitude;
        }
        static Vector2 Bez(Vector2 a, Vector2 b, Vector2 c, float t) => (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c;
        static Vector2 Bez3(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1 - t; return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
        }
        static List<Vector2> Curve(Vector2 a, Vector2 b, Vector2 c, int n = 24)
        {
            var l = new List<Vector2>(n + 1); for (int i = 0; i <= n; i++) l.Add(Bez(a, b, c, i / (float)n)); return l;
        }
        static List<Vector2> Curve3(Vector2 a, Vector2 b, Vector2 c, Vector2 d, int n = 28)
        {
            var l = new List<Vector2>(n + 1); for (int i = 0; i <= n; i++) l.Add(Bez3(a, b, c, d, i / (float)n)); return l;
        }
        /// <summary>Distance to a polyline stroke whose half width varies along its length.</summary>
        static float Stroke(Vector2 p, List<Vector2> pts, Func<float, float> width)
        {
            float best = 1e9f; int n = pts.Count - 1;
            for (int i = 0; i < n; i++)
            {
                float d = Seg(p, pts[i], pts[i + 1], out float t);
                float u = (i + t) / n;
                float w = width(u);
                best = Mathf.Min(best, d - w);
            }
            return best;
        }
        /// <summary>Signed distance to a closed polygon (negative inside).</summary>
        static float Poly(Vector2 p, List<Vector2> v)
        {
            float d = (p - v[0]).sqrMagnitude; float s = 1f;
            for (int i = 0, j = v.Count - 1; i < v.Count; j = i, i++)
            {
                Vector2 e = v[j] - v[i], w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, b.sqrMagnitude);
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }
        static float SMin(float a, float b, float k) { float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k); return Mathf.Lerp(b, a, h) - k * h * (1 - h); }

        // ------------------------------------------------------------------ public API
        public Texture2D PaintAtlas()
        {
            var cv = new Canvas(1024, 2048);
            for (int i = 0; i < 16; i++)
            {
                var cell = new Cell { C = cv, X0 = (i % 4) * 256, Y0 = 2048 - (i / 4 + 1) * 256, W = 256, H = 256 };
                if (ScanEyes != null && ScanEyePx != null) { if (_g == null) _g = SyntheticArt(); PaintScanEye(cell, i); } else PaintEye(cell, i);
            }
            for (int i = 0; i < 16; i++)
            {
                var cell = new Cell { C = cv, X0 = (i % 4) * 256, Y0 = 1024 - (i / 4 + 1) * 128, W = 256, H = 128 };
                PaintMouth(cell, i);
            }
            for (int i = 0; i < 8; i++)
            {
                var cell = new Cell { C = cv, X0 = (i % 4) * 256, Y0 = 512 - (i / 4 + 1) * 128, W = 256, H = 128 };
                PaintBrow(cell, i);
            }
            return ToTex(cv, "FaceAtlas");
        }

        public Texture2D PaintFx()
        {
            var cv = new Canvas(1024, 1024);
            PaintBase(new Cell { C = cv, X0 = 0, Y0 = 512, W = 512, H = 512 });
            PaintBlush(new Cell { C = cv, X0 = 512, Y0 = 512, W = 512, H = 512 });
            PaintGloom(new Cell { C = cv, X0 = 512, Y0 = 0, W = 512, H = 512 });
            PaintTears(new Cell { C = cv, X0 = 0, Y0 = 0, W = 512, H = 512 });
            return ToTex(cv, "FaceFx");
        }

        static Texture2D ToTex(Canvas cv, string name)
        {
            var t = new Texture2D(cv.W, cv.H, TextureFormat.RGBA32, true, false) { name = name };
            // un-premultiplied colors with alpha; bleed color into transparent texels to avoid dark fringes
            var px = cv.P;
            var outp = new Color[px.Length];
            for (int i = 0; i < px.Length; i++) outp[i] = px[i];
            Dilate(outp, cv.W, cv.H, 3);
            t.SetPixels(outp);
            t.Apply(true);
            return t;
        }

        static void Dilate(Color[] p, int w, int h, int iters)
        {
            for (int it = 0; it < iters; it++)
            {
                var src = (Color[])p.Clone();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (src[i].a > 0.01f) continue;
                        Color acc = Color.clear; float n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                                var s = src[yy * w + xx];
                                if (s.a > 0.01f) { acc += new Color(s.r, s.g, s.b, 0); n++; }
                            }
                        if (n > 0) { var c = acc / n; c.a = 0; p[i] = c; }
                    }
            }
        }

        // ------------------------------------------------------------------ eyes
        Color LashCol => Color.Lerp(new Color(0.07f, 0.05f, 0.07f), Hair * 0.5f, 0.35f);

        struct EyeShape
        {
            public Vector2 Outer, Inner;      // corners
            public Vector2 TopC1, TopC2;       // upper lid bezier controls
            public Vector2 BotC1, BotC2;       // lower lid bezier controls
            public Vector2 IrisC; public Vector2 IrisR;
            public float Pupil; public bool Highlights; public bool Dull; public float LidW;
        }

        EyeShape BaseShape()
        {
            float s = Sharp;
            var e = new EyeShape();
            float tilt = EyeLidTilt + s * 0.05f;
            e.Outer = new Vector2(0.10f, 0.50f + tilt);
            e.Inner = new Vector2(0.90f, 0.47f);
            float topH = Mathf.Lerp(0.80f, 0.70f, s);
            e.TopC1 = new Vector2(0.22f, Mathf.Lerp(topH + 0.04f, topH + 0.02f, s));
            e.TopC2 = new Vector2(0.70f, Mathf.Lerp(topH + 0.03f, topH - 0.02f, s));
            float botH = Mathf.Lerp(0.20f, 0.27f, s);
            e.BotC1 = new Vector2(0.30f, botH);
            e.BotC2 = new Vector2(0.72f, botH + 0.01f);
            e.IrisC = new Vector2(0.50f, Mathf.Lerp(0.47f, 0.48f, s));
            e.IrisR = new Vector2(Mathf.Lerp(0.235f, 0.205f, s), Mathf.Lerp(0.31f, 0.27f, s));
            e.Pupil = 0.42f; e.Highlights = true; e.LidW = LashesHeavy || Female ? 0.03f : 0.024f;
            return e;
        }

        void PaintEye(Cell c, int idx)
        {
            var e = BaseShape();
            switch (idx)
            {
                case FaceCells.EyeOpen: DrawOpenEye(c, e, 0f); break;
                case FaceCells.EyeHalf: DrawOpenEye(c, e, 0.55f); break;
                case FaceCells.EyeClosed: DrawClosedEye(c, e, false); break;
                case FaceCells.EyeHappy: DrawHappyEye(c, e, 1f); break;
                case FaceCells.EyeWide:
                    e.TopC1.y += 0.05f; e.TopC2.y += 0.05f; e.BotC1.y -= 0.04f; e.BotC2.y -= 0.04f;
                    e.IrisR *= 0.82f; e.Pupil = 0.32f; e.IrisC.y += 0.02f;
                    DrawOpenEye(c, e, 0f); break;
                case FaceCells.EyeAngry:
                    e.Inner.y -= 0.08f; e.TopC2.y -= 0.14f; e.TopC1.y -= 0.02f; e.IrisR *= 0.92f; e.Pupil = 0.36f;
                    DrawOpenEye(c, e, 0f, angry: true); break;
                case FaceCells.EyeSad:
                    e.Outer.y -= 0.08f; e.TopC1.y -= 0.12f; e.TopC2.y -= 0.02f; e.IrisC.y -= 0.03f;
                    DrawOpenEye(c, e, 0.1f); break;
                case FaceCells.EyeFear:
                    e.TopC1.y += 0.05f; e.TopC2.y += 0.05f; e.BotC1.y -= 0.03f; e.BotC2.y -= 0.03f;
                    e.IrisR *= 0.62f; e.Pupil = 0.25f; e.Highlights = false;
                    DrawOpenEye(c, e, 0f, fear: true); break;
                case FaceCells.EyeDead:
                    e.Highlights = false; e.Dull = true; e.Pupil = 0.62f; e.TopC1.y -= 0.06f; e.TopC2.y -= 0.06f;
                    DrawOpenEye(c, e, 0.2f); break;
                case FaceCells.EyeBlank:
                    e.Highlights = false; e.IrisR *= 0.9f;
                    DrawOpenEye(c, e, 0f, blank: true); break;
                case FaceCells.EyeTeary:
                    e.TopC1.y -= 0.04f; e.Outer.y -= 0.05f;
                    DrawOpenEye(c, e, 0.05f, teary: true); break;
                case FaceCells.EyePain: DrawPainEye(c, e); break;
                case FaceCells.EyeBreak:
                    e.TopC1.y += 0.03f; e.TopC2.y += 0.03f; e.Highlights = false;
                    DrawOpenEye(c, e, 0f, brk: true); break;
                case FaceCells.EyeHalfLid: DrawOpenEye(c, e, 0.38f); break;
                case FaceCells.EyeNarrow:
                    e.BotC1.y += 0.1f; e.BotC2.y += 0.1f;
                    DrawOpenEye(c, e, 0.42f); break;
                case FaceCells.EyeLaugh: DrawHappyEye(c, e, 1.5f); break;
            }
        }

        /// <summary>lidDrop: 0 open .. 1 closed (upper lid moves down).</summary>
        void DrawOpenEye(Cell c, EyeShape e, float lidDrop, bool angry = false, bool fear = false, bool blank = false, bool teary = false, bool brk = false)
        {
            // effective upper lid after the drop
            Vector2 tc1 = Vector2.Lerp(e.TopC1, new Vector2(e.TopC1.x, e.BotC1.y + 0.1f), lidDrop);
            Vector2 tc2 = Vector2.Lerp(e.TopC2, new Vector2(e.TopC2.x, e.BotC2.y + 0.1f), lidDrop);
            var top = Curve3(e.Outer, tc1, tc2, e.Inner);
            var bot = Curve3(e.Outer, e.BotC1, e.BotC2, e.Inner);
            var poly = new List<Vector2>(top);
            for (int i = bot.Count - 2; i >= 1; i--) poly.Add(bot[i]);
            Sd eyeIn = p => Poly(p, poly);

            // sclera
            Color scl = blank ? new Color(0.93f, 0.93f, 0.97f) : new Color(1f, 0.99f, 0.97f);
            Fill(c, eyeIn, p =>
            {
                // lid shadow on the sclera top
                float yTop = TopY(top, p.x);
                float sh = Mathf.Clamp01((yTop - p.y) / 0.12f);
                return Color.Lerp(new Color(0.78f, 0.74f, 0.82f), scl, sh);
            });

            // iris (clipped to the eye opening)
            if (!blank)
            {
                Color iris = Iris;
                if (e.Dull) iris = Color.Lerp(iris, new Color(0.35f, 0.33f, 0.36f), 0.55f);
                if (brk) iris = Color.Lerp(iris, new Color(0.62f, 0.05f, 0.16f), 0.55f);
                Color dark = iris * 0.35f; dark.a = 1;
                Color light = Color.Lerp(iris, Color.white, 0.45f);
                Vector2 ic = e.IrisC, ir = e.IrisR;
                Fill(c, p => Mathf.Max(Ellipse(p, ic, ir), eyeIn(p)), p =>
                {
                    float t = Mathf.Clamp01((p.y - (ic.y - ir.y)) / (2 * ir.y));
                    Color col = Color.Lerp(light, iris, Mathf.SmoothStep(0.05f, 0.6f, t));
                    col = Color.Lerp(col, dark, Mathf.SmoothStep(0.55f, 0.95f, t));
                    // radial streaks
                    float ang = Mathf.Atan2(p.y - ic.y, p.x - ic.x);
                    float st = Mathf.Sin(ang * 14f) * 0.5f + 0.5f;
                    col = Color.Lerp(col, col * 0.82f, st * 0.25f);
                    // limbal ring
                    float ring = Mathf.Clamp01(1f + Ellipse(p, ic, ir) / 0.03f);
                    col = Color.Lerp(col, dark, Mathf.SmoothStep(0.55f, 1f, ring) * 0.85f);
                    col.a = 1; return col;
                });
                // lower iris glow crescent
                if (!e.Dull && !brk)
                    Fill(c, p => Mathf.Max(Mathf.Max(Ellipse(p, ic + new Vector2(0, -ir.y * 0.45f), new Vector2(ir.x * 0.62f, ir.y * 0.32f)), -Ellipse(p, ic + new Vector2(0, -ir.y * 0.25f), new Vector2(ir.x * 0.6f, ir.y * 0.3f))), eyeIn(p)),
                        p => new Color(Mathf.Lerp(Iris.r, 1, 0.6f), Mathf.Lerp(Iris.g, 1, 0.6f), Mathf.Lerp(Iris.b, 1, 0.6f), 0.55f));
                // pupil
                Vector2 pr = new Vector2(ir.x * e.Pupil, ir.y * e.Pupil * 1.05f);
                Color pc = e.Dull ? new Color(0.08f, 0.07f, 0.09f) : new Color(0.04f, 0.03f, 0.05f);
                if (brk)
                {
                    // concentric break rings
                    Fill(c, p => Mathf.Max(Mathf.Abs(Ellipse(p, ic, ir * 0.62f)) - 0.018f, eyeIn(p)), new Color(0.78f, 0.08f, 0.22f), 1.2f);
                    Fill(c, p => Mathf.Max(Mathf.Abs(Ellipse(p, ic, ir * 0.35f)) - 0.014f, eyeIn(p)), new Color(0.86f, 0.8f, 1f), 1.2f);
                    FillC(c, p => Mathf.Max(Circle(p, ic, 0.03f), eyeIn(p)), new Color(0.02f, 0f, 0.02f));
                }
                else if (fear)
                {
                    FillC(c, p => Mathf.Max(Circle(p, ic, 0.028f), eyeIn(p)), pc);
                }
                else
                {
                    FillC(c, p => Mathf.Max(Ellipse(p, ic + new Vector2(0, 0.01f), pr), eyeIn(p)), new Color(pc.r, pc.g, pc.b, 0.92f));
                }
                // highlights
                if (e.Highlights)
                {
                    FillC(c, p => Mathf.Max(Ellipse(p, ic + new Vector2(-ir.x * 0.38f, ir.y * 0.38f), new Vector2(ir.x * 0.36f, ir.y * 0.26f), -25f), eyeIn(p)), Color.white);
                    FillC(c, p => Mathf.Max(Circle(p, ic + new Vector2(ir.x * 0.42f, -ir.y * 0.38f), ir.x * 0.14f), eyeIn(p)), Color.white);
                    if (teary)
                    {
                        FillC(c, p => Mathf.Max(Circle(p, ic + new Vector2(ir.x * 0.1f, ir.y * 0.05f), ir.x * 0.12f), eyeIn(p)), Color.white);
                        // welling water line
                        Fill(c, p => Mathf.Max(Stroke(p, bot, u => 0.035f * Mathf.Sin(u * Mathf.PI)), eyeIn(p) - 0.03f), _ => new Color(0.75f, 0.92f, 1f, 0.85f));
                    }
                }
            }
            else
            {
                // blank: faint grey ring, no pupil
                Vector2 ic = e.IrisC, ir = e.IrisR;
                Fill(c, p => Mathf.Max(Mathf.Abs(Ellipse(p, ic, ir)) - 0.006f, eyeIn(p)), new Color(0.55f, 0.55f, 0.6f, 0.5f));
            }

            if (fear)
            {
                // trembling lines around the iris
                for (int k = 0; k < 3; k++)
                {
                    float yy = 0.26f + k * 0.04f;
                    var l = Curve(new Vector2(0.2f, yy), new Vector2(0.5f, yy - 0.03f), new Vector2(0.8f, yy));
                    Fill(c, p => Stroke(p, l, u => 0.004f), new Color(0.3f, 0.3f, 0.45f, 0.35f));
                }
            }

            // upper lid (thick lash line)
            float lw = e.LidW;
            var lid = Curve3(e.Outer + new Vector2(-0.04f, -0.01f), tc1 + new Vector2(0, 0.012f), tc2 + new Vector2(0, 0.01f), e.Inner + new Vector2(0.01f, 0.005f), 32);
            Color lash = LashCol;
            Fill(c, p => Stroke(p, lid, u => lw * (0.35f + 1.1f * Mathf.Sin(Mathf.Clamp01(u * 1.15f) * Mathf.PI) * (1.15f - u * 0.6f)) + (angry ? 0.004f : 0f)), lash);
            // outer lashes flick
            var fl1 = Curve(e.Outer + new Vector2(0.0f, 0.0f), e.Outer + new Vector2(-0.07f, 0.0f), e.Outer + new Vector2(-0.09f, -0.06f), 10);
            Fill(c, p => Stroke(p, fl1, u => 0.016f * (1f - u) + 0.002f), lash);
            if (Female || LashesHeavy)
            {
                var fl2 = Curve(e.Outer + new Vector2(0.03f, 0.03f), e.Outer + new Vector2(-0.04f, 0.07f), e.Outer + new Vector2(-0.08f, 0.06f), 10);
                Fill(c, p => Stroke(p, fl2, u => 0.011f * (1f - u) + 0.002f), lash);
            }
            // double eyelid crease
            var crease = Curve(tc1 + new Vector2(0.02f, 0.08f), (tc1 + tc2) * 0.5f + new Vector2(0, 0.11f), tc2 + new Vector2(0.03f, 0.05f), 16);
            Fill(c, p => Stroke(p, crease, u => 0.0045f * Mathf.Sin(u * Mathf.PI)), new Color(lash.r, lash.g, lash.b, 0.45f));
            // lower lash line (partial, soft)
            Fill(c, p => Stroke(p, bot, u => (u > 0.12f && u < 0.8f ? 0.007f : 0.0f) * Mathf.Sin(Mathf.Clamp01((u - 0.12f) / 0.68f) * Mathf.PI)), new Color(lash.r, lash.g, lash.b, 0.7f));
            if (angry)
            {
                // tension line under the eye
                var tl = Curve(new Vector2(0.35f, 0.14f), new Vector2(0.55f, 0.12f), new Vector2(0.75f, 0.15f), 12);
                Fill(c, p => Stroke(p, tl, u => 0.004f * Mathf.Sin(u * Mathf.PI)), new Color(0.35f, 0.2f, 0.2f, 0.35f));
            }
        }

        static float TopY(List<Vector2> top, float x)
        {
            for (int i = 0; i + 1 < top.Count; i++)
                if ((top[i].x <= x && top[i + 1].x >= x) || (top[i].x >= x && top[i + 1].x <= x))
                {
                    float t = Mathf.InverseLerp(top[i].x, top[i + 1].x, x);
                    return Mathf.Lerp(top[i].y, top[i + 1].y, t);
                }
            return 0.5f;
        }

        void DrawClosedEye(Cell c, EyeShape e, bool happy)
        {
            Color lash = LashCol;
            var l = Curve3(e.Outer + new Vector2(-0.03f, 0f), new Vector2(0.3f, 0.30f), new Vector2(0.68f, 0.30f), e.Inner, 28);
            Fill(c, p => Stroke(p, l, u => 0.022f * (0.4f + Mathf.Sin(u * Mathf.PI) * 0.8f) * (1.1f - 0.5f * u)), lash);
            var fl = Curve(e.Outer + new Vector2(-0.02f, 0f), e.Outer + new Vector2(-0.07f, -0.02f), e.Outer + new Vector2(-0.08f, -0.07f), 10);
            Fill(c, p => Stroke(p, fl, u => 0.012f * (1f - u) + 0.002f), lash);
        }

        void DrawHappyEye(Cell c, EyeShape e, float thick)
        {
            Color lash = LashCol;
            var l = Curve3(new Vector2(0.14f, 0.36f), new Vector2(0.3f, 0.66f), new Vector2(0.68f, 0.66f), new Vector2(0.86f, 0.38f), 28);
            Fill(c, p => Stroke(p, l, u => 0.02f * thick * (0.45f + Mathf.Sin(u * Mathf.PI) * 0.7f)), lash);
            var fl = Curve(new Vector2(0.14f, 0.36f), new Vector2(0.08f, 0.34f), new Vector2(0.05f, 0.28f), 8);
            Fill(c, p => Stroke(p, fl, u => 0.01f * (1f - u) + 0.002f), lash);
        }

        void DrawPainEye(Cell c, EyeShape e)
        {
            Color lash = LashCol;
            // ">" chevron pointing to the inner corner (image right)
            var a = Curve(new Vector2(0.18f, 0.72f), new Vector2(0.5f, 0.62f), new Vector2(0.84f, 0.48f), 16);
            var b = Curve(new Vector2(0.84f, 0.48f), new Vector2(0.5f, 0.34f), new Vector2(0.2f, 0.26f), 16);
            Fill(c, p => Mathf.Min(Stroke(p, a, u => 0.022f * (0.5f + 0.5f * u)), Stroke(p, b, u => 0.02f * (1f - 0.5f * u))), lash);
        }

        // ------------------------------------------------------------------ brows
        void PaintBrow(Cell c, int idx)
        {
            // local x 0..1 (outer -> inner), y in x units (cell is 2:1, so y range 0..0.5)
            Color bc = Color.Lerp(Hair, new Color(0.05f, 0.04f, 0.05f), 0.35f);
            Vector2 outer = new Vector2(0.10f, 0.24f), mid = new Vector2(0.45f, 0.32f), inner = new Vector2(0.88f, 0.25f);
            switch (idx)
            {
                case FaceCells.BrowRaised: outer.y += 0.05f; mid.y += 0.09f; inner.y += 0.07f; break;
                case FaceCells.BrowAngry: outer.y += 0.06f; mid.y += 0.02f; inner.y -= 0.1f; break;
                case FaceCells.BrowSad: outer.y -= 0.07f; mid.y += 0.02f; inner.y += 0.09f; break;
                case FaceCells.BrowFurrow: outer.y += 0.02f; mid.y -= 0.01f; inner.y -= 0.07f; break;
                case FaceCells.BrowRelaxed: outer.y -= 0.02f; mid.y -= 0.03f; inner.y -= 0.02f; break;
                case FaceCells.BrowSkeptic: outer.y += 0.04f; mid.y += 0.11f; inner.y += 0.03f; break;
                case FaceCells.BrowBreak: outer.y += 0.1f; mid.y -= 0.02f; inner.y -= 0.12f; break;
            }
            var l = Curve(outer, mid, inner, 24);
            float th = 0.018f * BrowThick * (Female ? 0.85f : 1.1f);
            Fill(c, p => Stroke(p, l, u => th * (0.35f + 0.9f * u) * (u > 0.93f ? Mathf.Lerp(1f, 0.6f, (u - 0.93f) / 0.07f) : 1f)), bc);
            if (idx == FaceCells.BrowFurrow || idx == FaceCells.BrowAngry || idx == FaceCells.BrowBreak)
            {
                var w = Curve(inner + new Vector2(0.03f, -0.06f), inner + new Vector2(0.07f, -0.02f), inner + new Vector2(0.09f, 0.05f), 10);
                Fill(c, p => Stroke(p, w, u => 0.004f * Mathf.Sin(u * Mathf.PI)), new Color(bc.r, bc.g, bc.b, 0.5f));
            }
        }

        // ------------------------------------------------------------------ mouths
        public Color? LipOverride;
        Color LipLine => LipOverride ?? new Color(0.36f, 0.16f, 0.16f);
        Color MouthIn => new Color(0.36f, 0.08f, 0.1f);
        Color Tongue => new Color(0.88f, 0.42f, 0.45f);
        Color Teeth => new Color(1f, 0.99f, 0.97f);
        static readonly Color Gold = new Color(0.95f, 0.72f, 0.2f);

        void PaintMouth(Cell c, int idx)
        {
            // local coords: x 0..1, y 0..0.5 (2:1 cell); center (0.5, 0.25)
            Vector2 C = new Vector2(0.5f, 0.25f);
            Color line = LipLine;
            switch (idx)
            {
                case FaceCells.MouthNeutral:
                {
                    var l = Curve(C + new Vector2(-0.09f, 0.004f), C + new Vector2(0, -0.006f), C + new Vector2(0.09f, 0.004f), 12);
                    Fill(c, p => Stroke(p, l, u => 0.008f * (0.5f + 0.5f * Mathf.Sin(u * Mathf.PI))), line);
                    break;
                }
                case FaceCells.MouthSmile:
                {
                    var l = Curve(C + new Vector2(-0.13f, 0.03f), C + new Vector2(0, -0.045f), C + new Vector2(0.13f, 0.03f), 16);
                    Fill(c, p => Stroke(p, l, u => 0.009f * (0.45f + 0.55f * Mathf.Sin(u * Mathf.PI))), line);
                    break;
                }
                case FaceCells.MouthFrown:
                {
                    var l = Curve(C + new Vector2(-0.11f, -0.025f), C + new Vector2(0, 0.03f), C + new Vector2(0.11f, -0.025f), 16);
                    Fill(c, p => Stroke(p, l, u => 0.009f * (0.45f + 0.55f * Mathf.Sin(u * Mathf.PI))), line);
                    break;
                }
                case FaceCells.MouthSmirk:
                {
                    var l = Curve3(C + new Vector2(-0.1f, -0.004f), C + new Vector2(-0.02f, -0.012f), C + new Vector2(0.07f, -0.01f), C + new Vector2(0.13f, 0.04f), 16);
                    Fill(c, p => Stroke(p, l, u => 0.009f * (0.4f + 0.6f * Mathf.Sin(u * Mathf.PI))), line);
                    break;
                }
                case FaceCells.MouthWavy:
                {
                    var pts = new List<Vector2>();
                    for (int i = 0; i <= 32; i++) { float t = i / 32f; pts.Add(C + new Vector2(-0.12f + 0.24f * t, Mathf.Sin(t * Mathf.PI * 3f) * 0.014f)); }
                    Fill(c, p => Stroke(p, pts, u => 0.008f), line);
                    break;
                }
                case FaceCells.MouthGrin: OpenMouth(c, C, 0.16f, 0.07f, 0.02f, teethTop: true, smile: 1f, tongue: 0.4f); break;
                case FaceCells.MouthTalkA: OpenMouth(c, C, 0.06f, 0.035f, 0.0f, teethTop: false, smile: 0.2f, tongue: 0.5f); break;
                case FaceCells.MouthTalkO: OpenMouth(c, C, 0.075f, 0.065f, 0.0f, teethTop: true, smile: 0f, tongue: 0.5f); break;
                case FaceCells.MouthOpenWide: OpenMouth(c, C, 0.1f, 0.11f, 0f, teethTop: true, smile: -0.2f, tongue: 0.6f); break;
                case FaceCells.MouthLaugh: OpenMouth(c, C, 0.17f, 0.1f, 0.03f, teethTop: true, smile: 1.2f, tongue: 0.7f); break;
                case FaceCells.MouthSadOpen: OpenMouth(c, C, 0.09f, 0.05f, 0f, teethTop: false, smile: -1f, tongue: 0.3f); break;
                case FaceCells.MouthDead:
                {
                    OpenMouth(c, C + new Vector2(0.01f, 0), 0.06f, 0.022f, 0f, teethTop: false, smile: -0.3f, tongue: 0f, tilt: -6f);
                    break;
                }
                case FaceCells.MouthGritted: Gritted(c, C, 0.12f, 0.035f, 0f); break;
                case FaceCells.MouthPain: Gritted(c, C, 0.14f, 0.05f, -0.03f); break;
                case FaceCells.MouthDisgust:
                {
                    var pts = new List<Vector2> { C + new Vector2(-0.12f, -0.02f), C + new Vector2(-0.03f, 0.0f), C + new Vector2(0.06f, 0.035f), C + new Vector2(0.12f, 0.02f), C + new Vector2(0.05f, -0.015f), C + new Vector2(-0.04f, -0.03f) };
                    FillC(c, p => Poly(p, pts), MouthIn);
                    Fill(c, p => Mathf.Max(Poly(p, pts), -(p.y - (C.y + 0.005f))), _ => Teeth);
                    Fill(c, p => Mathf.Abs(Poly(p, pts)) - 0.005f, line);
                    break;
                }
                case FaceCells.MouthBreak:
                {
                    // unnaturally wide grin
                    var up = Curve(C + new Vector2(-0.3f, 0.06f), C + new Vector2(0f, 0.0f), C + new Vector2(0.3f, 0.06f), 24);
                    var dn = Curve(C + new Vector2(0.3f, 0.06f), C + new Vector2(0f, -0.13f), C + new Vector2(-0.3f, 0.06f), 24);
                    var poly = new List<Vector2>(up); poly.AddRange(dn);
                    FillC(c, p => Poly(p, poly), new Color(0.2f, 0.02f, 0.05f));
                    Fill(c, p => Mathf.Max(Poly(p, poly), -(p.y - (C.y - 0.005f))), p =>
                    {
                        float tx = Mathf.Repeat(p.x * 28f, 1f);
                        var col = Teeth; if (tx < 0.12f) col = new Color(0.7f, 0.65f, 0.7f); return col;
                    });
                    if (GoldTooth) FillC(c, p => Mathf.Max(Poly(p, poly), Mathf.Max(-(p.y - (C.y - 0.005f)), Mathf.Abs(p.x - (C.x + 0.07f)) - 0.018f)), Gold);
                    Fill(c, p => Mathf.Abs(Poly(p, poly)) - 0.006f, new Color(0.15f, 0.02f, 0.05f));
                    break;
                }
            }
        }

        /// <summary>Soft interior: dark red near the lips fading to a deep shadow at the back / bottom.</summary>
        Color MouthDepth(Vector2 p, Vector2 C, float hh, float lift)
        {
            float t = Mathf.Clamp01((C.y + hh * 0.5f + lift - p.y) / Mathf.Max(0.01f, hh * 2f));
            var a = MouthIn; var b = new Color(MouthIn.r * 0.42f, MouthIn.g * 0.35f, MouthIn.b * 0.45f, 1f);
            return Color.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t));
        }

        /// <summary>Hint of the upper lip (soft lip-coloured band hugging the top edge) and a faint lower-lip light.</summary>
        void LipHints(Cell c, List<Vector2> upper, Vector2 C, float hw, float hh)
        {
            var lipCol = Color.Lerp(LipLine, Skin, 0.45f);
            Fill(c, p => Stroke(p, upper, u => 0.0035f + 0.004f * Mathf.Sin(u * Mathf.PI)), p => new Color(lipCol.r, lipCol.g, lipCol.b, 0.32f), 3f);
            var lo = Curve(C + new Vector2(-hw * 0.45f, -hh * 2.05f), C + new Vector2(0, -hh * 2.35f), C + new Vector2(hw * 0.45f, -hh * 2.05f), 12);
            Fill(c, p => Stroke(p, lo, u => 0.004f * Mathf.Sin(u * Mathf.PI)), p => new Color(1f, 0.93f, 0.9f, 0.14f), 3f);
        }

        void OpenMouth(Cell c, Vector2 C, float hw, float hh, float lift, bool teethTop, float smile, float tongue, float tilt = 0f)
        {
            // D-shape: upper edge flatter, lower edge rounder; smile curves corners up
            var up = Curve(C + new Vector2(-hw, lift + smile * 0.02f), C + new Vector2(0, hh * 0.5f - smile * 0.01f + lift), C + new Vector2(hw, lift + smile * 0.02f), 20);
            var dn = Curve(C + new Vector2(hw, lift + smile * 0.02f), C + new Vector2(0, -hh * (1.6f + smile * 0.25f) + lift), C + new Vector2(-hw, lift + smile * 0.02f), 20);
            var poly = new List<Vector2>(up); poly.AddRange(dn);
            if (tilt != 0)
            {
                float a = tilt * Mathf.Deg2Rad;
                for (int i = 0; i < poly.Count; i++) { var q = poly[i] - C; poly[i] = C + new Vector2(q.x * Mathf.Cos(a) - q.y * Mathf.Sin(a), q.x * Mathf.Sin(a) + q.y * Mathf.Cos(a)); }
            }
            Sd inside = p => Poly(p, poly);
            Fill(c, inside, p => MouthDepth(p, C, hh, lift));
            if (tongue > 0)
            {
                Vector2 tc = C + new Vector2(0, -hh * 1.25f + lift);
                var tr = new Vector2(hw * 0.62f, hh * 0.7f * tongue + 0.01f);
                Fill(c, p => Mathf.Max(Ellipse(p, tc, tr), inside(p)), p => Color.Lerp(Tongue, new Color(Tongue.r * 0.72f, Tongue.g * 0.72f, Tongue.b * 0.72f, 1f), Mathf.Clamp01((tc.y + tr.y - p.y) / (tr.y * 2f))));
            }
            if (teethTop)
            {
                // one upper teeth band that follows the lip curve (no cells), softly shaded toward the gum line
                float band = Mathf.Max(0.012f, hh * 0.35f);
                Fill(c, p =>
                {
                    float x = Mathf.Clamp01((p.x - (C.x - hw)) / (2f * hw));
                    float topY = C.y + lift + (hh * 0.5f - smile * 0.01f) * 4f * x * (1f - x) + smile * 0.02f * (1f - 4f * x * (1f - x));
                    return Mathf.Max(inside(p) + 0.002f, -(p.y - (topY - band)));
                }, p => new Color(Teeth.r * 0.97f, Teeth.g * 0.95f, Teeth.b * 0.95f, 1f));
                if (GoldTooth && hw > 0.09f)
                    Fill(c, p => Mathf.Max(inside(p) + 0.002f, Mathf.Max(-(p.y - (C.y + lift - band * 0.2f)), Mathf.Abs(p.x - (C.x + hw * 0.42f)) - 0.016f)), Gold);
            }
            Fill(c, p => Mathf.Abs(inside(p)) - 0.0034f, p => new Color(LipLine.r, LipLine.g, LipLine.b, 0.78f), 1.6f);
            LipHints(c, Curve(C + new Vector2(-hw * 0.85f, lift + smile * 0.02f + 0.006f), C + new Vector2(0, hh * 0.5f - smile * 0.01f + lift + 0.008f), C + new Vector2(hw * 0.85f, lift + smile * 0.02f + 0.006f), 16), C, hw, hh);
        }

        void Gritted(Cell c, Vector2 C, float hw, float hh, float corner)
        {
            // clenched teeth, anime style: a rounded lens-shaped mouth, dark soft interior, one upper teeth band that
            // follows the lip curve and a thin lower band behind it - no tooth cells, no box outline
            var up = Curve(C + new Vector2(-hw, corner), C + new Vector2(0, hh * 1.35f), C + new Vector2(hw, corner), 20);
            var dn = Curve(C + new Vector2(hw, corner), C + new Vector2(0, -hh * 1.3f), C + new Vector2(-hw, corner), 20);
            var poly = new List<Vector2>(up); poly.AddRange(dn);
            Sd inside = p => Poly(p, poly);
            Fill(c, inside, p => MouthDepth(p, C, hh, 0f));
            float Top(float px) { float x = Mathf.Clamp01((px - (C.x - hw)) / (2f * hw)); return Mathf.Lerp(C.y + corner, C.y + hh * 0.675f, 4f * x * (1f - x)); }
            float Bot(float px) { float x = Mathf.Clamp01((px - (C.x - hw)) / (2f * hw)); return Mathf.Lerp(C.y + corner, C.y - hh * 0.65f, 4f * x * (1f - x)); }
            // upper band down to just below the centre line, lower band a sliver above the bottom edge
            Fill(c, p => Mathf.Max(inside(p) + 0.003f, -(p.y - (C.y - hh * 0.08f))), p =>
            {
                float v = 0.9f + 0.08f * Mathf.Clamp01((p.y - (C.y - hh * 0.08f)) / Mathf.Max(0.004f, Top(p.x) - C.y));
                return new Color(Teeth.r * v, Teeth.g * v, Teeth.b * v, 1f);
            });
            Fill(c, p => Mathf.Max(inside(p) + 0.004f, p.y - (Bot(p.x) + hh * 0.42f)), new Color(Teeth.r * 0.78f, Teeth.g * 0.76f, Teeth.b * 0.8f));
            if (GoldTooth) FillC(c, p => Mathf.Max(Mathf.Max(inside(p) + 0.003f, -(p.y - (C.y - hh * 0.08f))), Mathf.Abs(p.x - (C.x + hw * 0.33f)) - 0.018f), Gold);
            Fill(c, p => Mathf.Abs(inside(p)) - 0.003f, p => new Color(LipLine.r, LipLine.g, LipLine.b, 0.7f), 1.6f);
            LipHints(c, Curve(C + new Vector2(-hw * 0.85f, corner + 0.006f), C + new Vector2(0, hh * 1.35f + 0.008f), C + new Vector2(hw * 0.85f, corner + 0.006f), 16), C, hw, hh * 0.6f);
        }

        // ------------------------------------------------------------------ fx sheet (face uv 0..1 over the whole cell)
        public float EyeV = 0.43f, MouthV = 0.1f, EyeDx = 0.2f;   // eye line / mouth line / eye offset in face uv (fx placement)

        void PaintBase(Cell c)
        {
            Color shade = new Color(Skin.r * 0.78f, Skin.g * 0.6f, Skin.b * 0.62f, 1f);
            // nose: soft shadow down one side of the bridge, a small shadow under the tip, a tiny highlight on it
            var bridge = Curve(new Vector2(0.513f, EyeV - 0.035f), new Vector2(0.522f, (EyeV + NosePos) * 0.5f - 0.01f), new Vector2(0.516f, NosePos + 0.012f), 16);
            Fill(c, p => Stroke(p, bridge, u => 0.0035f + 0.006f * Mathf.Sin(u * Mathf.PI)), p => new Color(shade.r, shade.g, shade.b, 0.22f), 3f);
            var under = Curve(new Vector2(0.478f, NosePos - 0.004f), new Vector2(0.5f, NosePos - 0.014f), new Vector2(0.524f, NosePos - 0.003f), 12);
            Fill(c, p => Stroke(p, under, u => 0.0035f * Mathf.Sin(u * Mathf.PI) + 0.001f), new Color(shade.r * 0.9f, shade.g * 0.85f, shade.b * 0.9f, 0.45f), 2f);
            FillC(c, p => Circle(p, new Vector2(0.5f, NosePos + 0.004f), 0.006f), new Color(1f, 0.97f, 0.95f, 0.22f));
            // lips: a faint lower-lip tint and the philtrum shadow
            var lip = new Vector2(0.5f, MouthV - 0.017f);
            Fill(c, p => Ellipse(p, lip, new Vector2(0.034f, 0.009f)), p => new Color(0.86f, 0.5f, 0.52f, 0.28f * Mathf.Clamp01(-Ellipse(p, lip, new Vector2(0.034f, 0.009f)) / 0.006f)), 3f);
            var phil = new List<Vector2> { new Vector2(0.5f, NosePos - 0.02f), new Vector2(0.5f, MouthV + 0.012f) };
            Fill(c, p => Stroke(p, phil, u => 0.004f), new Color(shade.r, shade.g, shade.b, 0.1f), 4f);
            // natural warmth on the cheeks for everyone (stronger "cute blush" where the character asks for it)
            float cb = 0.1f + 0.28f * CuteBlush;
            for (int s = 0; s < 2; s++)
            {
                Vector2 bc = new Vector2(s == 0 ? 0.3f : 0.7f, (EyeV + NosePos) * 0.5f - 0.03f);
                var r = new Vector2(0.075f, 0.03f);
                Fill(c, p => Ellipse(p, bc, r), p => new Color(1f, 0.56f, 0.58f, cb * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-Ellipse(p, bc, r) / 0.025f))), 3f);
            }
        }

        void PaintBlush(Cell c)
        {
            // soft watercolour flush on the cheeks (no hatching)
            for (int s = 0; s < 2; s++)
            {
                Vector2 bc = new Vector2(s == 0 ? 0.5f - EyeDx * 1.15f : 0.5f + EyeDx * 1.15f, NosePos + 0.045f);
                var r = new Vector2(0.1f, 0.042f);
                Fill(c, p => Ellipse(p, bc, r), p => new Color(0.98f, 0.45f, 0.52f, 0.42f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-Ellipse(p, bc, r) / 0.035f))), 2.5f);
            }
        }

        void PaintGloom(Cell c)
        {
            // a cold, darkening veil that settles over the upper face (gothic mood rather than comic shock lines)
            Fill(c, p => -(p.y - 0.5f), p =>
            {
                float t = Mathf.Clamp01((p.y - 0.5f) / 0.42f);
                float side = 1f - Mathf.Clamp01(Mathf.Abs(p.x - 0.5f) / 0.5f) * 0.3f;
                return new Color(0.2f, 0.17f, 0.34f, 0.5f * Mathf.SmoothStep(0f, 1f, t) * side);
            }, 2f);
            // faint under-eye shadows
            for (int s = 0; s < 2; s++)
            {
                Vector2 e = new Vector2(s == 0 ? 0.5f - EyeDx : 0.5f + EyeDx, EyeV - 0.06f);
                var r = new Vector2(0.09f, 0.03f);
                Fill(c, p => Ellipse(p, e, r), p => new Color(0.3f, 0.24f, 0.42f, 0.35f * Mathf.Clamp01(-Ellipse(p, e, r) / 0.025f)), 2f);
            }
        }

        void PaintTears(Cell c)
        {
            float eyeY = EyeV - 0.035f;
            for (int s = 0; s < 2; s++)
            {
                float x = s == 0 ? 0.5f - EyeDx * 0.62f : 0.5f + EyeDx * 0.62f;
                var ln = Curve(new Vector2(x, eyeY), new Vector2(x + (s == 0 ? -0.03f : 0.03f), eyeY - 0.12f), new Vector2(x + (s == 0 ? -0.02f : 0.02f), eyeY - 0.25f), 16);
                Fill(c, p => Stroke(p, ln, u => 0.012f * (0.6f + 0.4f * Mathf.Sin(u * Mathf.PI))), new Color(0.72f, 0.9f, 1f, 0.85f));
                Fill(c, p => Stroke(p, ln, u => 0.004f), new Color(1f, 1f, 1f, 0.9f));
                FillC(c, p => Circle(p, ln[ln.Count - 1] + new Vector2(0, -0.03f), 0.014f), new Color(0.72f, 0.9f, 1f, 0.9f));
            }
        }
    }
}
