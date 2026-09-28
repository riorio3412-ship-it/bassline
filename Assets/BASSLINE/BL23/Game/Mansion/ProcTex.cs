using System;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Procedural textures (wallpaper masks, decal atlas, portraits, light cookies). The editor baker writes these to
    /// Resources/Mansion/Proc as PNG; at runtime they are loaded from there, or generated (smaller) as a fallback.
    /// </summary>
    public static class ProcTex
    {
        // ------------------------------------------------------------------ helpers
        static float Frac(float x) => x - Mathf.Floor(x);
        static float Hash(float x, float y) { float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f; return h - Mathf.Floor(h); }
        static float Hash(int x, int y, int s) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263 + s * 2147483647); h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16; return (h & 0xFFFFFF) / 16777216f; } }
        static float Noise(float x, float y, int period, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y); float fx = x - ix, fy = y - iy;
            int x0 = ((ix % period) + period) % period, y0 = ((iy % period) + period) % period, x1 = (x0 + 1) % period, y1 = (y0 + 1) % period;
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            float ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }
        /// <summary>Tileable fbm over [0,1)^2 with base period p.</summary>
        static float Fbm(float u, float v, int p, int seed, int oct = 4)
        {
            float s = 0, a = 0.5f, tot = 0;
            for (int o = 0; o < oct; o++) { s += a * Noise(u * p, v * p, p, seed + o * 17); tot += a; a *= 0.5f; p *= 2; }
            return s / tot;
        }
        static float Smooth(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }

        /// <summary>
        /// A hand-knotted Persian-style rug (sRGB, clamp; u across the width, v along the length): guard stripes, a
        /// running-vine main border, a field of small herati motifs, a lobed central medallion with pendants and corner
        /// spandrels, all worn by traffic toward the middle. Four colourways.
        /// </summary>
        public static Texture2D PersianRug(int w, int h, int variant)
        {
            Color field, medal, border, ivory, gold, ink, accent;
            switch (variant & 3)
            {
                case 0: field = new Color(0.42f, 0.06f, 0.07f); medal = new Color(0.08f, 0.1f, 0.22f); border = new Color(0.08f, 0.1f, 0.22f); ivory = new Color(0.82f, 0.74f, 0.58f); gold = new Color(0.68f, 0.48f, 0.2f); ink = new Color(0.05f, 0.03f, 0.03f); accent = new Color(0.2f, 0.32f, 0.3f); break;
                case 1: field = new Color(0.07f, 0.09f, 0.2f); medal = new Color(0.45f, 0.07f, 0.08f); border = new Color(0.4f, 0.06f, 0.07f); ivory = new Color(0.8f, 0.72f, 0.56f); gold = new Color(0.7f, 0.5f, 0.2f); ink = new Color(0.03f, 0.03f, 0.05f); accent = new Color(0.55f, 0.3f, 0.12f); break;
                case 2: field = new Color(0.05f, 0.18f, 0.16f); medal = new Color(0.78f, 0.68f, 0.5f); border = new Color(0.25f, 0.07f, 0.16f); ivory = new Color(0.82f, 0.74f, 0.6f); gold = new Color(0.66f, 0.46f, 0.18f); ink = new Color(0.03f, 0.05f, 0.04f); accent = new Color(0.5f, 0.12f, 0.12f); break;
                default: field = new Color(0.2f, 0.06f, 0.17f); medal = new Color(0.75f, 0.62f, 0.52f); border = new Color(0.06f, 0.16f, 0.12f); ivory = new Color(0.84f, 0.76f, 0.62f); gold = new Color(0.68f, 0.5f, 0.24f); ink = new Color(0.04f, 0.02f, 0.03f); accent = new Color(0.45f, 0.12f, 0.1f); break;
            }
            var px = new Color32[w * h];
            float aspect = h / (float)w;       // v runs along the long side
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    float u = (i + 0.5f) / w, v = (j + 0.5f) / h;
                    // distance to the nearest edge in width units
                    float du = Mathf.Min(u, 1 - u), dv = Mathf.Min(v, 1 - v) * aspect, de = Mathf.Min(du, dv);
                    Color c;
                    if (de < 0.018f) c = ink;
                    else if (de < 0.034f) c = Frac((u + v * aspect) * 40f) < 0.5f ? gold : ivory;        // outer guard: barber-pole
                    else if (de < 0.042f) c = ink;
                    else if (de < 0.13f)
                    {
                        // main border: a running vine with rosettes
                        float bt = du < dv ? v * aspect : u;             // coordinate along the border
                        float n = (de - 0.042f) / 0.088f;              // 0..1 across the band
                        float wave = 0.5f + 0.32f * Mathf.Sin(bt * Mathf.PI * 2f * 7f);
                        float rosT = Frac(bt * 7f + 0.25f), rosD = Mathf.Sqrt((rosT - 0.5f) * (rosT - 0.5f) * 0.18f + (n - 0.5f) * (n - 0.5f));
                        c = border;
                        if (Mathf.Abs(n - wave) < 0.07f) c = gold;
                        if (rosD < 0.2f) c = rosD < 0.1f ? accent : (rosD < 0.14f ? ivory : ink);
                    }
                    else if (de < 0.138f) c = ink;
                    else if (de < 0.156f) c = Frac((u - v * aspect) * 36f) < 0.5f ? accent : ivory;   // inner guard
                    else if (de < 0.164f) c = ink;
                    else
                    {
                        // field: herati-like lattice of small diamonds + rosettes
                        float fu = u * 9f, fv = v * aspect * 9f;
                        float lu = Frac(fu) - 0.5f, lv = Frac(fv) - 0.5f;
                        float dia = Mathf.Abs(lu) + Mathf.Abs(lv);
                        c = field;
                        if (Mathf.Abs(dia - 0.38f) < 0.035f) c = Color.Lerp(field, gold, 0.55f);
                        if (dia < 0.09f) c = ivory;
                        else if (dia < 0.14f) c = accent;
                        // central medallion (lobed lozenge) with pendants, and corner spandrels
                        float cu = (u - 0.5f) * 2f, cv = (v - 0.5f) * 2f;                              // -1..1
                        float lobes = 1f + 0.08f * Mathf.Cos(Mathf.Atan2(cv, cu) * 16f);
                        float md = (Mathf.Abs(cu) / 0.58f + Mathf.Abs(cv) / 0.42f) / lobes;
                        float pend = Mathf.Abs(cu) / 0.12f + Mathf.Abs(Mathf.Abs(cv) - 0.52f) / 0.1f;
                        if (md < 1f || pend < 1f)
                        {
                            float mdd = Mathf.Min(md, pend);
                            c = mdd > 0.9f ? ink : mdd > 0.8f ? gold : medal;
                            if (md < 0.55f) c = md > 0.47f ? ink : md > 0.4f ? ivory : (Frac(md * 6f) < 0.5f ? field : accent);
                            if (md < 0.16f) c = gold;
                        }
                        // corner spandrels: quarter medallions in the medallion colour
                        float sx = 1f - Mathf.Abs(cu), sy = 1f - Mathf.Abs(cv);
                        float sd = (sx / 0.42f + sy / 0.3f);
                        if (sd < 1f) c = sd > 0.9f ? ink : sd > 0.8f ? gold : Color.Lerp(medal, field, 0.25f);
                    }
                    // wool: abrash colour banding along the weave, knot grain, traffic wear toward the middle
                    float abrash = (Noise(u * 3f, v * 40f, 999, 71 + variant) - 0.5f) * 0.16f;
                    float grain = (Hash(i, j, 5 + variant) - 0.5f) * 0.12f;
                    float wear = Smooth(0.35f, 0.0f, Mathf.Abs(u - 0.5f) * 0.7f + Mathf.Abs(v - 0.5f) * 0.5f) * 0.18f * Fbm(u, v, 6, 17 + variant, 3);
                    float k = 1f + abrash + grain;
                    c = new Color(c.r * k, c.g * k, c.b * k);
                    c = Color.Lerp(c, new Color(0.55f, 0.48f, 0.4f), wear);
                    { float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f; c = Color.Lerp(c, new Color(lum, lum, lum), 0.22f) * 0.8f; }   // aged wool: muted, never candy
                    px[i + j * w] = c;
                }
            var t = Make(w, h, px, false, false, "PersianRug" + variant);
            return t;
        }

        public static Texture2D Make(int w, int h, Color32[] px, bool linear, bool repeat, string name, bool mips = true)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, mips, linear) { name = name, wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            t.SetPixels32(px); t.Apply(mips, false);
            return t;
        }

        // ------------------------------------------------------------------ wallpaper masks (R motif, G gilt, B spare, A grime)
        public enum Paper { Damask, Stripe, Moonflower, FleshVein, Damask2 }

        struct Ell { public float x, y, rx, ry, rot; }

        static float SdEll(float x, float y, float cx, float cy, float rx, float ry, float rot)
        {
            float dx = x - cx, dy = y - cy; float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            float lx = dx * cs + dy * sn, ly = -dx * sn + dy * cs;
            float k = Mathf.Sqrt(lx * lx / (rx * rx) + ly * ly / (ry * ry));
            return (k - 1f) * Mathf.Min(rx, ry);
        }
        static float SdSeg(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float px = x - ax, py = y - ay, vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp01((px * vx + py * vy) / (vx * vx + vy * vy + 1e-9f));
            float dx = px - vx * t, dy = py - vy * t; return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }
        static float SdArc(float x, float y, float cx, float cy, float R, float w, float a0, float a1)
        {
            float dx = x - cx, dy = y - cy; float ang = Mathf.Atan2(dy, dx); if (ang < a0) ang += Mathf.PI * 2f;
            if (ang >= a0 && ang <= a1) return Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - R) - w;
            float ex0 = cx + Mathf.Cos(a0) * R, ey0 = cy + Mathf.Sin(a0) * R, ex1 = cx + Mathf.Cos(a1) * R, ey1 = cy + Mathf.Sin(a1) * R;
            return Mathf.Min(Mathf.Sqrt((x - ex0) * (x - ex0) + (y - ey0) * (y - ey0)), Mathf.Sqrt((x - ex1) * (x - ex1) + (y - ey1) * (y - ey1))) - w;
        }

        /// <summary>Signed distance of a symmetric damask ornament (x mirrored >= 0, y in -0.5..0.5). vein = distance to inner detail lines.</summary>
        static float DamaskSdf(float x, float y, int variant, out float vein)
        {
            float s = variant == 0 ? 1f : 1.1f;
            x /= s; y /= s;
            float d = SdSeg(x, y, 0, -0.26f, 0, 0.2f, 0.014f);                                   // stem
            d = Mathf.Min(d, SdEll(x, y, 0, 0.27f, 0.032f, 0.075f, 0));                          // centre petal
            d = Mathf.Min(d, SdEll(x, y, 0.055f, 0.215f, 0.022f, 0.06f, -0.65f));               // side petals
            d = Mathf.Min(d, SdEll(x, y, 0.0f, 0.13f, 0.045f, 0.03f, 0));                        // bud
            float leaf = SdEll(x, y, 0.125f, 0.02f, 0.045f, 0.13f, -0.75f);                      // big leaf
            d = Mathf.Min(d, leaf);
            d = Mathf.Min(d, SdArc(x, y, 0.17f, -0.17f, 0.065f, 0.012f, -0.3f, 4.0f));           // scroll
            d = Mathf.Min(d, SdEll(x, y, 0.235f, -0.17f, 0.018f, 0.018f, 0));                    // scroll end
            d = Mathf.Min(d, SdEll(x, y, 0.0f, -0.285f, 0.07f, 0.028f, 0));                      // base
            d = Mathf.Min(d, SdEll(x, y, 0.2f, 0.19f, 0.02f, 0.02f, 0));                         // berries
            d = Mathf.Min(d, SdEll(x, y, 0.245f, 0.1f, 0.016f, 0.016f, 0));
            if (variant == 1)
            {
                d = Mathf.Min(d, SdEll(x, y, 0.0f, 0.0f, 0.08f, 0.1f, 0));                       // pomegranate
                d = Mathf.Min(d, SdArc(x, y, 0.06f, 0.33f, 0.05f, 0.01f, 3.2f, 6.0f));
            }
            // veins: leaf midrib + pomegranate seeds
            float vx = x - 0.125f, vy = y - 0.02f; float cs = Mathf.Cos(-0.75f), sn = Mathf.Sin(-0.75f);
            float lx = vx * cs + vy * sn, ly = -vx * sn + vy * cs;
            vein = Mathf.Abs(lx) + (Mathf.Abs(ly) > 0.1f ? 1 : 0);
            vein = Mathf.Min(vein, Mathf.Abs(Mathf.Sqrt(x * x + (y - 0.27f) * (y - 0.27f) * 0.3f) - 0.015f) + (y < 0.2f ? 1 : 0));
            if (variant == 1) vein = Mathf.Min(vein, Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.05f));
            return d * s;
        }

        public static Texture2D Wallpaper(Paper kind, int size)
        {
            var px = new Color32[size * size];
            var rnd = new System.Random(1000 + (int)kind * 77);
            Ell[] ells = null;
            if (kind == Paper.Damask || kind == Paper.Damask2)
            {
                int n = kind == Paper.Damask ? 9 : 11;
                ells = new Ell[n];
                // a spine + mirrored leaves/curls, all inside |q| < 0.48 of a motif cell (cell = 1 tile, half-drop)
                ells[0] = new Ell { x = 0, y = 0.02f, rx = 0.05f, ry = 0.2f, rot = 0 };
                ells[1] = new Ell { x = 0, y = 0.26f, rx = 0.035f, ry = 0.07f, rot = 0 };
                ells[2] = new Ell { x = 0, y = -0.24f, rx = 0.07f, ry = 0.05f, rot = 0 };
                for (int i = 3; i < n; i++)
                {
                    float ang = (float)(rnd.NextDouble() * 1.6 - 0.8);
                    float r = 0.08f + (float)rnd.NextDouble() * 0.22f;
                    ells[i] = new Ell
                    {
                        x = 0.05f + (float)rnd.NextDouble() * 0.2f,
                        y = (float)(rnd.NextDouble() * 0.56 - 0.28),
                        rx = 0.02f + (float)rnd.NextDouble() * 0.05f,
                        ry = 0.05f + (float)rnd.NextDouble() * 0.1f,
                        rot = ang
                    };
                    _ = r;
                }
            }
            float inv = 1f / size;
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                {
                    float u = (i + 0.5f) * inv, v = (j + 0.5f) * inv;
                    float motif = 0, gilt = 0;
                    switch (kind)
                    {
                        case Paper.Damask:
                        case Paper.Damask2:
                            {
                                // two motif centres per tile (half-drop); motif = mirrored SDF ornament
                                float best = 9, vein = 9;
                                for (int c = 0; c < 2; c++)
                                {
                                    float cx = c == 0 ? 0.5f : 0f, cy = c == 0 ? 0.5f : 0f;
                                    float qx = u - cx, qy = v - cy;
                                    qx -= Mathf.Round(qx); qy -= Mathf.Round(qy);
                                    float dd2 = DamaskSdf(Mathf.Abs(qx), qy, kind == Paper.Damask2 ? 1 : 0, out float vn);
                                    best = Mathf.Min(best, dd2); vein = Mathf.Min(vein, vn);
                                }
                                motif = Smooth(0.003f, -0.003f, best);
                                gilt = Smooth(0.011f, 0.007f, best) * Smooth(0.001f, 0.004f, best);      // outline just outside the fill
                                gilt = Mathf.Max(gilt, Smooth(0.004f, 0.0015f, vein) * motif);          // veins inside
                                // tiny gilt dots in the ground (diamond lattice)
                                float gx = u * 10f, gy = v * 10f + (Mathf.Floor(u * 10f) % 2) * 0.5f;
                                float ddot = Mathf.Sqrt(Mathf.Pow(Frac(gx) - 0.5f, 2) + Mathf.Pow(Frac(gy) - 0.5f, 2));
                                gilt = Mathf.Max(gilt, Smooth(0.06f, 0.035f, ddot) * (1 - Smooth(0.03f, 0.0f, best)) * 0.7f);
                                break;
                            }
                        case Paper.Stripe:
                            {
                                float x = Frac(u * 4f);
                                motif = Smooth(0.02f, 0.03f, x) * Smooth(0.48f, 0.47f, x);
                                gilt = Smooth(0.03f, 0.0f, Mathf.Abs(x - 0.62f)) + Smooth(0.012f, 0.0f, Mathf.Abs(x - 0.74f));
                                // little diamonds in the plain stripe
                                float dy = Frac(v * 8f) - 0.5f, dx = x - 0.74f;
                                float dia = Mathf.Abs(dx) * 6f + Mathf.Abs(dy) * 1.2f;
                                gilt = Mathf.Max(gilt, Smooth(0.12f, 0.08f, dia));
                                break;
                            }
                        case Paper.Moonflower:
                            {
                                // grid of crescents with jitter + stars
                                float best = 0, g2 = 0;
                                for (int k = 0; k < 2; k++)
                                {
                                    float gx = u * 3f + k * 0.5f, gy = v * 3f + k * 0.5f;
                                    int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy);
                                    float fx = gx - ix - 0.5f, fy = gy - iy - 0.5f;
                                    float r1 = Mathf.Sqrt(fx * fx + fy * fy);
                                    float ox = fx - 0.09f, oy = fy + 0.06f;
                                    float r2 = Mathf.Sqrt(ox * ox + oy * oy);
                                    float moon = Smooth(0.25f, 0.23f, r1) * Smooth(0.2f, 0.22f, r2);
                                    best = Mathf.Max(best, moon);
                                    g2 = Mathf.Max(g2, Smooth(0.27f, 0.255f, r1) * Smooth(0.235f, 0.25f, r1) * Smooth(0.2f, 0.22f, r2));
                                    // four-point star offset
                                    float sx = fx + 0.33f, sy = fy - 0.3f;
                                    float st = Mathf.Abs(sx) * Mathf.Abs(sy) * 90f + (sx * sx + sy * sy) * 60f;
                                    g2 = Mathf.Max(g2, Smooth(0.6f, 0.3f, st));
                                }
                                // vine curls
                                float vine = Mathf.Abs(Mathf.Sin((u * 6.2831f * 3f) + Mathf.Sin(v * 6.2831f * 2f) * 1.5f) - (v * 2f % 1f - 0.5f) * 0.0f);
                                motif = Mathf.Max(best, Smooth(0.06f, 0.02f, Mathf.Abs(Frac(u * 3f + Mathf.Sin(v * 6.2831f * 3f) * 0.08f) - 0.5f)) * 0.5f);
                                gilt = g2; _ = vine;
                                break;
                            }
                        case Paper.FleshVein:
                            {
                                // warped voronoi edges -> veins
                                float wu = u + (Fbm(u, v, 4, 11) - 0.5f) * 0.12f, wv = v + (Fbm(u, v, 4, 23) - 0.5f) * 0.12f;
                                int P = 6; float gx = wu * P, gy = wv * P; int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy);
                                float f1 = 9, f2 = 9;
                                for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
                                    {
                                        int cx = ix + x, cy = iy + y; int wx = ((cx % P) + P) % P, wy = ((cy % P) + P) % P;
                                        float ox = Hash(wx, wy, 5), oy = Hash(wx, wy, 9);
                                        float dx = cx + ox - gx, dy = cy + oy - gy; float d = Mathf.Sqrt(dx * dx + dy * dy);
                                        if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
                                    }
                                float edge = f2 - f1;
                                motif = Smooth(0.09f, 0.0f, edge) * 0.9f + Fbm(u, v, 8, 3) * 0.15f;
                                gilt = Smooth(0.03f, 0.0f, edge) * 0.4f;
                                break;
                            }
                    }
                    // grime: low-frequency blotches + vertical drips from the top
                    float grime = Mathf.Pow(Fbm(u, v, 3, 71 + (int)kind), 2.2f) * 1.3f;
                    float dripCol = Hash(Mathf.FloorToInt(u * 24f), 0, 91);
                    float drip = dripCol > 0.8f ? Smooth(0.35f, 0.9f, v) * Smooth(0.5f, 0.2f, Mathf.Abs(Frac(u * 24f) - 0.5f)) : 0;
                    grime = Mathf.Clamp01(grime + drip * 0.5f);
                    px[j * size + i] = new Color32((byte)(Mathf.Clamp01(motif) * 255), (byte)(Mathf.Clamp01(gilt) * 255), 0, (byte)(grime * 255));
                }
            return Make(size, size, px, true, true, "Wallpaper_" + kind);
        }

        // ------------------------------------------------------------------ canvas for atlases
        sealed class Canvas
        {
            public int W, H; public Color[] P;
            public Canvas(int w, int h, Color fill) { W = w; H = h; P = new Color[w * h]; for (int i = 0; i < P.Length; i++) P[i] = fill; }
            public void Blend(int x, int y, Color c, float a)
            {
                if (x < 0 || y < 0 || x >= W || y >= H || a <= 0) return;
                int k = y * W + x; P[k] = Color.Lerp(P[k], new Color(c.r, c.g, c.b, P[k].a), Mathf.Clamp01(a));
            }
            public void Max(int x, int y, float r, float a)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                int k = y * W + x; var p = P[k]; p.r = Mathf.Max(p.r, r); p.a = Mathf.Max(p.a, a); P[k] = p;
            }
            public void Ellipse(float cx, float cy, float rx, float ry, Color c, float soft = 1.5f, float alpha = 1f, float rot = 0)
            {
                int x0 = Mathf.FloorToInt(cx - Mathf.Max(rx, ry) - 2), x1 = Mathf.CeilToInt(cx + Mathf.Max(rx, ry) + 2);
                int y0 = Mathf.FloorToInt(cy - Mathf.Max(rx, ry) - 2), y1 = Mathf.CeilToInt(cy + Mathf.Max(rx, ry) + 2);
                float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                    {
                        float dx = x - cx, dy = y - cy; float lx = dx * cs + dy * sn, ly = -dx * sn + dy * cs;
                        float d = Mathf.Sqrt(lx * lx / (rx * rx) + ly * ly / (ry * ry));
                        float a = Mathf.Clamp01((1 - d) * Mathf.Min(rx, ry) / soft);
                        Blend(x, y, c, a * alpha);
                    }
            }
            public void Line(float ax, float ay, float bx, float by, float w, Color c, float alpha = 1f)
            {
                int x0 = Mathf.FloorToInt(Mathf.Min(ax, bx) - w - 2), x1 = Mathf.CeilToInt(Mathf.Max(ax, bx) + w + 2);
                int y0 = Mathf.FloorToInt(Mathf.Min(ay, by) - w - 2), y1 = Mathf.CeilToInt(Mathf.Max(ay, by) + w + 2);
                float vx = bx - ax, vy = by - ay; float l2 = vx * vx + vy * vy + 1e-6f;
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                    {
                        float t = Mathf.Clamp01(((x - ax) * vx + (y - ay) * vy) / l2);
                        float dx = x - (ax + vx * t), dy = y - (ay + vy * t);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        Blend(x, y, c, Mathf.Clamp01(w - d + 0.5f) * alpha);
                    }
            }
            public Color32[] ToColor32()
            {
                var o = new Color32[P.Length];
                for (int i = 0; i < P.Length; i++) o[i] = P[i];
                return o;
            }
        }

        // ------------------------------------------------------------------ decal atlas (4x4). R = height, A = coverage
        public const int DecalCells = 4;
        public enum Decal { BloodPool, BloodSplat, BloodDrip, BloodSmear, DragMark, FootShoe, FootBare, Water, Scratch, Crack, Dent, Fragments, Soil, Ash, PaintDrip, Handprint }

        public static Rect DecalRect(Decal d)
        {
            int k = (int)d; int cx = k % DecalCells, cy = k / DecalCells; float s = 1f / DecalCells;
            float pad = s * 0.01f;
            return new Rect(cx * s + pad, cy * s + pad, s - 2 * pad, s - 2 * pad);
        }

        public static Texture2D DecalAtlas(int size)
        {
            int cell = size / DecalCells;
            var h = new float[size * size]; var a = new float[size * size];
            var rnd = new System.Random(4242);
            float R() => (float)rnd.NextDouble();
            void Put(int x, int y, float hv, float av)
            {
                if (x < 0 || y < 0 || x >= size || y >= size) return;
                int k = y * size + x; h[k] = Mathf.Max(h[k], hv); a[k] = Mathf.Max(a[k], av);
            }
            void Blob(int ox, int oy, float cx, float cy, float r, float warp, int seed, float hmul = 1f, float edgeH = 0.4f)
            {
                int x0 = (int)(cx - r * 1.6f), x1 = (int)(cx + r * 1.6f), y0 = (int)(cy - r * 1.6f), y1 = (int)(cy + r * 1.6f);
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                    {
                        if (x < 0 || y < 0 || x >= cell || y >= cell) continue;
                        float dx = (x - cx) / r, dy = (y - cy) / r;
                        float ang = Mathf.Atan2(dy, dx);
                        float n = Noise(ang * 1.6f + 10, seed * 0.37f, 1000, seed) * warp + Noise(ang * 5f + 3, seed, 1000, seed + 1) * warp * 0.4f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / (1 - warp * 0.5f + n);
                        float cov = Mathf.Clamp01((1 - d) * r * 0.25f);
                        float hv = (0.55f + edgeH * Smooth(0.6f, 0.95f, d)) * hmul;
                        if (cov > 0) Put(ox + x, oy + y, hv * cov, cov);
                    }
            }
            void Stroke(int ox, int oy, float ax, float ay, float bx, float by, float w, float hv, float av = 1f)
            {
                float vx = bx - ax, vy = by - ay; float l2 = vx * vx + vy * vy + 1e-6f;
                int x0 = (int)(Mathf.Min(ax, bx) - w - 2), x1 = (int)(Mathf.Max(ax, bx) + w + 2), y0 = (int)(Mathf.Min(ay, by) - w - 2), y1 = (int)(Mathf.Max(ay, by) + w + 2);
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                    {
                        if (x < 0 || y < 0 || x >= cell || y >= cell) continue;
                        float t = Mathf.Clamp01(((x - ax) * vx + (y - ay) * vy) / l2);
                        float dx = x - (ax + vx * t), dy = y - (ay + vy * t); float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float cov = Mathf.Clamp01(w - d + 0.5f);
                        if (cov > 0) Put(ox + x, oy + y, hv * cov * (1 - d / (w + 1)) + 0.1f * cov, cov * av);
                    }
            }
            for (int k = 0; k < DecalCells * DecalCells; k++)
            {
                int ox = (k % DecalCells) * cell, oy = (k / DecalCells) * cell;
                float c = cell;
                switch ((Decal)k)
                {
                    case Decal.BloodPool: Blob(ox, oy, c * 0.5f, c * 0.5f, c * 0.36f, 0.35f, 3, 1f, 0.5f); for (int i = 0; i < 6; i++) Blob(ox, oy, c * (0.2f + 0.6f * R()), c * (0.2f + 0.6f * R()), c * (0.03f + 0.05f * R()), 0.2f, 10 + i); break;
                    case Decal.BloodSplat:
                        Blob(ox, oy, c * 0.5f, c * 0.5f, c * 0.18f, 0.5f, 7, 0.9f);
                        for (int i = 0; i < 26; i++) { float ang = R() * 6.283f, dist = c * (0.18f + R() * 0.28f); float x = c * 0.5f + Mathf.Cos(ang) * dist, y = c * 0.5f + Mathf.Sin(ang) * dist; Blob(ox, oy, x, y, c * (0.008f + R() * 0.03f), 0.2f, 30 + i); if (R() > 0.5f) Stroke(ox, oy, c * 0.5f + Mathf.Cos(ang) * c * 0.15f, c * 0.5f + Mathf.Sin(ang) * c * 0.15f, x, y, c * 0.006f, 0.5f); }
                        break;
                    case Decal.BloodDrip:
                    case Decal.PaintDrip:
                        {
                            bool paint = (Decal)k == Decal.PaintDrip;
                            // band at the top, drips running down (texture v=1 top)
                            for (int x = 0; x < cell; x++)
                            {
                                float top = c * (0.9f - Noise(x * 0.05f, 1, 1000, k) * 0.12f);
                                for (int y = (int)top; y < cell; y++) Put(ox + x, oy + y, 0.5f, 1);
                            }
                            int n = paint ? 16 : 11;
                            for (int i = 0; i < n; i++)
                            {
                                float x = c * (0.04f + 0.92f * R()); float len = c * (0.15f + R() * (paint ? 0.72f : 0.55f)); float w = c * (0.006f + R() * (paint ? 0.02f : 0.012f));
                                float y0 = c * 0.9f, y1 = y0 - len;
                                Stroke(ox, oy, x, y0, x + (R() - 0.5f) * c * 0.01f, y1, w, 0.8f);
                                Blob(ox, oy, x, y1, w * 1.6f, 0.1f, 50 + i, 1f);
                            }
                            break;
                        }
                    case Decal.BloodSmear:
                        for (int i = 0; i < 40; i++) { float y = c * (0.3f + R() * 0.4f); Stroke(ox, oy, c * (0.08f + R() * 0.1f), y, c * (0.6f + R() * 0.32f), y + (R() - 0.5f) * c * 0.1f, c * (0.01f + R() * 0.03f), 0.4f + R() * 0.3f, 0.5f + R() * 0.5f); }
                        break;
                    case Decal.DragMark:
                        for (int i = 0; i < 30; i++) { float x = c * (0.3f + R() * 0.4f); Stroke(ox, oy, x, c * 0.04f, x + (R() - 0.5f) * c * 0.06f, c * (0.7f + R() * 0.26f), c * (0.004f + R() * 0.02f), 0.3f, 0.25f + R() * 0.6f); }
                        break;
                    case Decal.FootShoe:
                    case Decal.FootBare:
                        {
                            bool bare = (Decal)k == Decal.FootBare;
                            Blob(ox, oy, c * 0.5f, c * 0.64f, c * (bare ? 0.13f : 0.15f), 0.08f, 71, 0.8f, 0.1f);   // ball
                            Blob(ox, oy, c * 0.49f, c * 0.3f, c * (bare ? 0.1f : 0.12f), 0.05f, 72, 0.8f, 0.1f);   // heel
                            if (bare) for (int t = 0; t < 5; t++) Blob(ox, oy, c * (0.4f + t * 0.05f), c * (0.83f - Mathf.Abs(t - 1) * 0.015f), c * (0.035f - t * 0.003f), 0.05f, 80 + t);
                            else
                            {
                                Stroke(ox, oy, c * 0.47f, c * 0.35f, c * 0.49f, c * 0.6f, c * 0.07f, 0.5f, 0.6f);
                                // tread lines cut out
                                for (int y = 0; y < cell; y += 7) for (int x = 0; x < cell; x++) { int kk = (oy + y) * size + ox + x; a[kk] *= 0.35f; }
                            }
                            break;
                        }
                    case Decal.Water:
                        Blob(ox, oy, c * 0.5f, c * 0.5f, c * 0.38f, 0.45f, 91, 0.4f, 0.8f); Blob(ox, oy, c * 0.72f, c * 0.35f, c * 0.14f, 0.3f, 92, 0.4f, 0.8f);
                        break;
                    case Decal.Scratch:
                        for (int i = 0; i < 4; i++) { float x = c * (0.3f + i * 0.1f); Stroke(ox, oy, x, c * 0.15f, x + c * 0.12f, c * 0.85f, c * 0.008f, 0.1f); }
                        for (int i = 0; i < 12; i++) { float x = c * R(), y = c * R(); Stroke(ox, oy, x, y, x + (R() - 0.5f) * c * 0.4f, y + (R() - 0.5f) * c * 0.4f, c * 0.003f, 0.1f, 0.6f); }
                        break;
                    case Decal.Crack:
                        for (int b = 0; b < 7; b++)
                        {
                            float x = c * 0.5f, y = c * 0.5f; float ang = b / 7f * 6.283f + R() * 0.5f; float w = c * 0.012f;
                            for (int s = 0; s < 14; s++)
                            {
                                float nx = x + Mathf.Cos(ang) * c * 0.03f, ny = y + Mathf.Sin(ang) * c * 0.03f; Stroke(ox, oy, x, y, nx, ny, w, 0.05f);
                                x = nx; y = ny; ang += (R() - 0.5f) * 0.9f; w *= 0.9f;
                                if (R() > 0.8f) { float bx = x, by = y, ba = ang + (R() > 0.5f ? 0.9f : -0.9f); for (int q = 0; q < 5; q++) { float nbx = bx + Mathf.Cos(ba) * c * 0.025f, nby = by + Mathf.Sin(ba) * c * 0.025f; Stroke(ox, oy, bx, by, nbx, nby, w * 0.6f, 0.05f); bx = nbx; by = nby; ba += (R() - 0.5f); } }
                            }
                        }
                        break;
                    case Decal.Dent:
                        for (int y = 0; y < cell; y++) for (int x = 0; x < cell; x++)
                            {
                                float dx = (x - c * 0.5f) / (c * 0.4f), dy = (y - c * 0.5f) / (c * 0.4f); float d = Mathf.Sqrt(dx * dx + dy * dy);
                                if (d < 1) Put(ox + x, oy + y, 0.5f - 0.5f * Mathf.Cos(d * Mathf.PI), Smooth(1f, 0.6f, d) * 0.7f);
                            }
                        break;
                    case Decal.Fragments:
                        for (int i = 0; i < 30; i++)
                        {
                            float x = c * (0.1f + 0.8f * R()), y = c * (0.1f + 0.8f * R()), s = c * (0.01f + 0.04f * R());
                            float a0 = R() * 6.28f; var p0 = new Vector2(x + Mathf.Cos(a0) * s, y + Mathf.Sin(a0) * s); var p1 = new Vector2(x + Mathf.Cos(a0 + 2.1f) * s, y + Mathf.Sin(a0 + 2.1f) * s); var p2 = new Vector2(x + Mathf.Cos(a0 + 4.0f) * s * 0.6f, y + Mathf.Sin(a0 + 4.0f) * s * 0.6f);
                            Stroke(ox, oy, p0.x, p0.y, p1.x, p1.y, s * 0.35f, 0.9f); Stroke(ox, oy, p1.x, p1.y, p2.x, p2.y, s * 0.35f, 0.9f);
                        }
                        break;
                    case Decal.Soil:
                    case Decal.Ash:
                        for (int y = 0; y < cell; y++) for (int x = 0; x < cell; x++)
                            {
                                float dx = (x - c * 0.5f) / (c * 0.45f), dy = (y - c * 0.5f) / (c * 0.45f); float d = Mathf.Sqrt(dx * dx + dy * dy);
                                float n = Noise(x * 0.08f, y * 0.08f, 1000, k) * 0.6f + Noise(x * 0.3f, y * 0.3f, 1000, k + 1) * 0.4f;
                                float cov = Smooth(1f, 0.3f, d + (n - 0.5f) * 0.6f) * (0.5f + n * 0.7f);
                                if ((Decal)k == Decal.Soil && Hash(x, y, 7) > 0.985f) cov = 1;
                                Put(ox + x, oy + y, n, Mathf.Clamp01(cov));
                            }
                        break;
                    case Decal.Handprint:
                        Blob(ox, oy, c * 0.5f, c * 0.4f, c * 0.16f, 0.1f, 111, 0.8f);
                        for (int f = 0; f < 4; f++) { float fx = c * (0.36f + f * 0.09f); Stroke(ox, oy, fx, c * 0.52f, fx + (f - 1.5f) * c * 0.02f, c * (0.78f + (f == 1 || f == 2 ? 0.06f : 0f)), c * 0.035f, 0.8f); }
                        Stroke(ox, oy, c * 0.34f, c * 0.4f, c * 0.2f, c * 0.55f, c * 0.035f, 0.8f);
                        for (int i = 0; i < 5; i++) { float x = c * (0.35f + R() * 0.3f); Stroke(ox, oy, x, c * 0.26f, x, c * (0.1f - R() * 0.06f), c * 0.01f, 0.7f); }
                        break;
                }
            }
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) { byte hv = (byte)(Mathf.Clamp01(h[i]) * 255); px[i] = new Color32(hv, hv, hv, (byte)(Mathf.Clamp01(a[i]) * 255)); }
            return Make(size, size, px, true, false, "DecalAtlas");
        }

        // ------------------------------------------------------------------ portraits (4 x 3 cells): 0..7 scratched faces, 8..11 dream landscapes
        public const int PortraitCols = 4, PortraitRows = 3;
        public static Rect PortraitRect(int idx)
        {
            idx = ((idx % 12) + 12) % 12; int cx = idx % PortraitCols, cy = idx / PortraitCols;
            return new Rect(cx / (float)PortraitCols, cy / (float)PortraitRows, 1f / PortraitCols, 1f / PortraitRows);
        }

        public static Texture2D Portraits(int size)
        {
            int cw = size / PortraitCols, ch = size / PortraitRows;
            var cv = new Canvas(size, size, new Color(0.05f, 0.04f, 0.05f, 1));
            var rnd = new System.Random(9001);
            float R() => (float)rnd.NextDouble();
            Color[] bgs = { new Color(0.22f, 0.05f, 0.12f), new Color(0.05f, 0.14f, 0.16f), new Color(0.16f, 0.12f, 0.04f), new Color(0.1f, 0.06f, 0.2f), new Color(0.2f, 0.09f, 0.07f), new Color(0.04f, 0.06f, 0.14f), new Color(0.12f, 0.16f, 0.06f), new Color(0.18f, 0.1f, 0.16f) };
            Color[] cloth = { new Color(0.08f, 0.06f, 0.1f), new Color(0.35f, 0.05f, 0.08f), new Color(0.05f, 0.2f, 0.22f), new Color(0.3f, 0.25f, 0.1f), new Color(0.15f, 0.1f, 0.3f), new Color(0.6f, 0.55f, 0.5f), new Color(0.05f, 0.05f, 0.05f), new Color(0.4f, 0.1f, 0.3f) };
            Color[] hair = { new Color(0.05f, 0.03f, 0.03f), new Color(0.25f, 0.12f, 0.05f), new Color(0.7f, 0.6f, 0.45f), new Color(0.08f, 0.08f, 0.12f), new Color(0.35f, 0.05f, 0.1f) };
            for (int idx = 0; idx < PortraitCols * PortraitRows; idx++)
            {
                int ox = (idx % PortraitCols) * cw, oy = (idx / PortraitCols) * ch;
                Color bg = bgs[idx % bgs.Length];
                // painterly background gradient + vignette
                for (int y = 0; y < ch; y++) for (int x = 0; x < cw; x++)
                    {
                        float u = x / (float)cw, v = y / (float)ch;
                        float vig = 1 - Mathf.Pow(Mathf.Abs(u - 0.5f) * 1.6f, 2) - Mathf.Pow(Mathf.Abs(v - 0.55f) * 1.3f, 2);
                        float n = Noise(u * 9, v * 9, 1000, idx) * 0.5f + Noise(u * 30, v * 30, 1000, idx + 7) * 0.25f;
                        Color c = bg * (0.5f + 0.8f * Mathf.Clamp01(vig) + n * 0.4f);
                        if (idx >= 8) c = Color.Lerp(new Color(0.02f, 0.03f, 0.08f), bg * 2.2f, Mathf.Pow(v, 1.4f)) * (0.7f + n * 0.5f);
                        cv.P[(oy + y) * size + ox + x] = new Color(c.r, c.g, c.b, 1);
                    }
                float cx = ox + cw * 0.5f;
                if (idx < 8)
                {
                    Color cl = cloth[idx % cloth.Length], hr = hair[idx % hair.Length];
                    Color skin = Color.Lerp(new Color(0.85f, 0.7f, 0.6f), new Color(0.55f, 0.4f, 0.32f), R() * 0.6f);
                    float headY = oy + ch * 0.62f, headR = cw * 0.17f;
                    // shoulders / torso
                    cv.Ellipse(cx, oy + ch * 0.12f, cw * 0.42f, ch * 0.28f, cl * 1.2f, 3);
                    cv.Ellipse(cx, oy + ch * 0.2f, cw * 0.3f, ch * 0.2f, cl * 0.9f, 6, 0.6f);
                    // collar / ruff
                    if (idx % 3 == 0) for (int r = 0; r < 12; r++) cv.Ellipse(cx + (r - 5.5f) * cw * 0.03f, oy + ch * 0.38f, cw * 0.035f, ch * 0.03f, new Color(0.9f, 0.88f, 0.8f), 2);
                    cv.Ellipse(cx, headY - headR * 1.1f, headR * 0.45f, headR * 0.6f, skin * 0.85f, 2);         // neck
                    cv.Ellipse(cx, headY + headR * 0.35f, headR * 1.15f, headR * 1.3f, hr, 3);                  // hair back
                    cv.Ellipse(cx, headY, headR * 0.92f, headR * 1.18f, skin, 2);                              // face
                    cv.Ellipse(cx, headY + headR * 0.9f, headR * 1.0f, headR * 0.5f, hr, 2);                    // fringe
                    cv.Ellipse(cx - headR * 0.35f, headY + headR * 0.15f, headR * 0.13f, headR * 0.08f, new Color(0.1f, 0.06f, 0.06f), 1.5f);
                    cv.Ellipse(cx + headR * 0.35f, headY + headR * 0.15f, headR * 0.13f, headR * 0.08f, new Color(0.1f, 0.06f, 0.06f), 1.5f);
                    cv.Ellipse(cx, headY - headR * 0.55f, headR * 0.25f, headR * 0.06f, new Color(0.5f, 0.15f, 0.15f), 1.5f);
                    // the face is scratched away: frantic strokes exposing pale ground and red underpaint
                    int strokes = 40 + (int)(R() * 40);
                    for (int s = 0; s < strokes; s++)
                    {
                        float ax = cx + (R() - 0.5f) * headR * 1.8f, ay = headY + (R() - 0.5f) * headR * 2.0f;
                        float ang = (R() - 0.5f) * 1.2f + (s % 2 == 0 ? 0.6f : -0.6f);
                        float len = headR * (0.3f + R() * 0.9f);
                        Color sc = R() > 0.7f ? new Color(0.55f, 0.05f, 0.08f) : new Color(0.85f, 0.82f, 0.75f);
                        cv.Line(ax, ay, ax + Mathf.Cos(ang) * len, ay + Mathf.Sin(ang) * len, 0.8f + R() * 1.4f, sc, 0.8f);
                    }
                    if (idx % 4 == 1)
                    {
                        // but the eyes remain, staring
                        cv.Ellipse(cx - headR * 0.35f, headY + headR * 0.15f, headR * 0.16f, headR * 0.1f, new Color(0.95f, 0.93f, 0.88f), 1.2f);
                        cv.Ellipse(cx + headR * 0.35f, headY + headR * 0.15f, headR * 0.16f, headR * 0.1f, new Color(0.95f, 0.93f, 0.88f), 1.2f);
                        cv.Ellipse(cx - headR * 0.35f, headY + headR * 0.15f, headR * 0.06f, headR * 0.06f, new Color(0.02f, 0.02f, 0.02f), 1f);
                        cv.Ellipse(cx + headR * 0.35f, headY + headR * 0.15f, headR * 0.06f, headR * 0.06f, new Color(0.02f, 0.02f, 0.02f), 1f);
                    }
                }
                else
                {
                    // dream landscapes: a huge moon over black water / trees / a fish in the sky / the mansion
                    float mY = oy + ch * (0.62f + R() * 0.12f), mX = ox + cw * (0.3f + R() * 0.4f), mR = cw * (0.14f + R() * 0.08f);
                    Color mc = idx == 9 ? new Color(1f, 0.4f, 0.55f) : new Color(1f, 0.95f, 0.8f);
                    cv.Ellipse(mX, mY, mR * 1.8f, mR * 1.8f, mc * 0.25f, mR * 0.8f, 0.5f);
                    cv.Ellipse(mX, mY, mR, mR, mc, 2);
                    for (int x = 0; x < cw; x++)
                    {
                        float horizon = ch * (0.28f + Noise(x * 0.03f, idx, 1000, idx) * (idx == 10 ? 0.25f : 0.06f));
                        for (int y = 0; y < (int)horizon; y++) cv.Blend(ox + x, oy + y, new Color(0.02f, 0.02f, 0.04f), 1);
                        if (idx != 10) for (int y = 0; y < (int)(ch * 0.22f); y++) { float refl = Mathf.Exp(-Mathf.Abs(ox + x - mX) / (mR * 0.6f)) * (0.5f + 0.5f * Mathf.Sin(y * 1.3f)); cv.Blend(ox + x, oy + y, mc, refl * 0.35f); }
                    }
                    if (idx == 11)
                    {
                        // goldfish drifting in the sky
                        cv.Ellipse(ox + cw * 0.3f, oy + ch * 0.45f, cw * 0.12f, cw * 0.06f, new Color(1f, 0.45f, 0.1f), 2);
                        cv.Ellipse(ox + cw * 0.46f, oy + ch * 0.45f, cw * 0.06f, cw * 0.07f, new Color(1f, 0.6f, 0.3f), 2, 0.9f, 0.5f);
                    }
                }
                // frame shadow inside the cell edge
                for (int y = 0; y < ch; y++) for (int x = 0; x < cw; x++)
                    {
                        float e = Mathf.Min(Mathf.Min(x, cw - 1 - x), Mathf.Min(y, ch - 1 - y));
                        if (e < 10) cv.Blend(ox + x, oy + y, Color.black, (10 - e) / 14f);
                    }
            }
            // canvas crackle overall
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float n = Noise(x * 0.35f, y * 0.35f, 100000, 5);
                    var p = cv.P[y * size + x]; float k = 0.9f + n * 0.2f; cv.P[y * size + x] = new Color(p.r * k, p.g * k, p.b * k, 1);
                }
            return Make(size, size, cv.ToColor32(), false, false, "Portraits");
        }

        // ------------------------------------------------------------------ light cookies
        /// <summary>Spot cookie: arched window with leaded panes (white = light).</summary>
        public static Texture2D WindowCookie(int size)
        {
            var px = new Color32[size * size];
            for (int j = 0; j < size; j++) for (int i = 0; i < size; i++)
                {
                    float u = (i + 0.5f) / size * 2 - 1, v = (j + 0.5f) / size * 2 - 1;
                    // window occupies |u|<0.55, v in -0.8..0.8 with an arch on top
                    float inside = (Mathf.Abs(u) < 0.55f && v > -0.8f && (v < 0.35f || (u * u + (v - 0.35f) * (v - 0.35f)) < 0.55f * 0.55f)) ? 1 : 0;
                    float pu = Mathf.Abs(Frac((u + 0.55f) / 0.55f) - 0.5f), pv = Mathf.Abs(Frac((v + 0.8f) / 0.4f) - 0.5f);
                    float bar = (pu > 0.46f || pv > 0.46f) ? 1 : 0;
                    float e = Smooth(1.0f, 0.75f, Mathf.Sqrt(u * u + v * v));
                    float val = inside * (1 - bar * 0.9f) * e;
                    byte b = (byte)(Mathf.Clamp01(val) * 255);
                    px[j * size + i] = new Color32(b, b, b, b);
                }
            var t = Make(size, size, px, false, false, "WindowCookie");
            return t;
        }

        /// <summary>Coloured stained-glass cookie (moon rosette).</summary>
        public static Texture2D StainedCookie(int size, Color a, Color b, Color c, Color d)
        {
            var px = new Color32[size * size];
            Color[] cols = { a, b, c, d };
            for (int j = 0; j < size; j++) for (int i = 0; i < size; i++)
                {
                    float u = (i + 0.5f) / size, v = (j + 0.5f) / size;
                    float du = u - 0.5f, dv = v - 0.5f; float r = Mathf.Sqrt(du * du + dv * dv);
                    // voronoi cells
                    float gx = u * 7, gy = v * 7; int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy);
                    float f1 = 9, f2 = 9; float id = 0;
                    for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
                        {
                            float ox = Hash(ix + x, iy + y, 3), oy = Hash(ix + x, iy + y, 8);
                            float dx = ix + x + ox - gx, dy = iy + y + oy - gy; float dd = Mathf.Sqrt(dx * dx + dy * dy);
                            if (dd < f1) { f2 = f1; f1 = dd; id = Hash(ix + x, iy + y, 21); } else if (dd < f2) f2 = dd;
                        }
                    Color col = cols[Mathf.Min(3, (int)(id * 4))] * (0.6f + 0.6f * Frac(id * 13));
                    if (r < 0.17f) col = new Color(1f, 0.95f, 0.8f);
                    float lead = (f2 - f1) < 0.06f ? 1 : 0;
                    if (Mathf.Abs(r - 0.17f) < 0.012f || Mathf.Abs(r - 0.26f) < 0.01f) lead = 1;
                    float e = Smooth(0.5f, 0.38f, r);
                    col = col * (1 - lead * 0.95f) * e;
                    px[j * size + i] = new Color32((byte)(Mathf.Clamp01(col.r) * 255), (byte)(Mathf.Clamp01(col.g) * 255), (byte)(Mathf.Clamp01(col.b) * 255), 255);
                }
            return Make(size, size, px, false, false, "StainedCookie");
        }

        /// <summary>Small soft dot texture for halos / particles.</summary>
        public static Texture2D SoftDot(int size)
        {
            var px = new Color32[size * size];
            for (int j = 0; j < size; j++) for (int i = 0; i < size; i++)
                {
                    float u = (i + 0.5f) / size * 2 - 1, v = (j + 0.5f) / size * 2 - 1; float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01(1 - r); a = a * a;
                    byte b = (byte)(a * 255); px[j * size + i] = new Color32(255, 255, 255, b);
                }
            return Make(size, size, px, false, false, "SoftDot");
        }

        /// <summary>Rain streak for the particle system.</summary>
        public static Texture2D RainStreak(int w, int h)
        {
            var px = new Color32[w * h];
            for (int j = 0; j < h; j++) for (int i = 0; i < w; i++)
                {
                    float u = (i + 0.5f) / w * 2 - 1, v = (j + 0.5f) / h;
                    float a = Mathf.Clamp01(1 - Mathf.Abs(u) * 1.2f) * Mathf.Sin(v * Mathf.PI) * (0.4f + 0.6f * v);
                    px[j * w + i] = new Color32(210, 230, 255, (byte)(a * 255));
                }
            return Make(w, h, px, false, false, "RainStreak");
        }
    }
}
