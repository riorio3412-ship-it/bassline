using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>Per-model texture fixes (art direction + cleanup), painted in 3D on the source surface.</summary>
    public static partial class GlbFix
    {
        static GlbFix()
        {
            Fixes["P06"] = FixP06;
            Fixes["P10"] = FixP10;
            Fixes["P05"] = FixP05;
            Fixes["P04"] = FixP04;
            Fixes["P01"] = FixP01;
            Fixes["P02"] = FixP02;
        }

        // ================================================================== helpers
        /// <summary>Iris centre of eye s (0 = character's right eye, +X side; 1 = character's left eye, -X side).</summary>
        public static Vector3 Eye(Ctx c, int s)
        {
            var cal = GlbFace.Get(c.Def.Id); var e = c.LM.EyeCenter;
            return new Vector3(e.x + (s == 0 ? cal.EyeDX : -cal.EyeDX), e.y + cal.EyeDY, e.z);
        }

        /// <summary>Front-most surface point (and its normal) at (x, y).</summary>
        public static Vector3 FrontSurface(Ctx c, float x, float y, out Vector3 n, float tol = 0.0015f)
        {
            var tp = c.TP; float bz = float.MinValue; Vector3 bp = new Vector3(x, y, 0); n = Vector3.forward;
            for (int k = 0; k < tp.Pos.Length; k++)
            {
                if (tp.Tri[k] < 0) continue;
                var p = tp.Pos[k];
                if (Mathf.Abs(p.x - x) > tol || Mathf.Abs(p.y - y) > tol || p.z <= bz) continue;
                bz = p.z; bp = p; n = tp.Nrm[k];
            }
            return bp;
        }

        public static float Sat(Color col) { Color.RGBToHSV(col, out _, out float s, out _); return s; }
        public static float Hue(Color col) { Color.RGBToHSV(col, out float h, out _, out _); return h; }
        public static bool Warm(Color col) => col.r > col.g && col.g > col.b * 0.95f && Sat(col) > 0.12f;

        /// <summary>Skin tone test (light warm, not saturated red / orange / yellow fabric).</summary>
        public static bool SkinLike(Color col)
        {
            Color.RGBToHSV(col, out float h, out float s, out float v);
            return v > 0.45f && s > 0.08f && s < 0.5f && (h < 0.11f || h > 0.97f) && col.r > col.b + 0.08f;
        }

        /// <summary>
        /// Checkerboard detector in texture space: a texel is on a checker pattern when, along both its row and its
        /// column (±half texels), dark and bright classes alternate at least 'minFlips' times (a single garment edge
        /// flips once).
        /// </summary>
        public static bool[] Checker(GlbTexPaint tp, Func<int, bool> zone, int half, int minFlips, float dark = 0.3f, float bright = 0.52f)
        {
            int W = tp.W, H = tp.H;
            var cls = new sbyte[W * H];
            for (int k = 0; k < cls.Length; k++)
            {
                if (tp.Tri[k] < 0) { cls[k] = -1; continue; }
                float l = Lum(tp.Px[k]);
                cls[k] = (sbyte)(l < dark ? 1 : l > bright && Sat(tp.Px[k]) < 0.25f ? 2 : 0);
            }
            int Flips(int x, int y, int dx, int dy)
            {
                int last = 0, n = 0;
                for (int i = -half; i <= half; i++)
                {
                    int xx = x + dx * i, yy = y + dy * i;
                    if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                    int c = cls[yy * W + xx];
                    if (c <= 0) continue;
                    if (last != 0 && c != last) n++;
                    last = c;
                }
                return n;
            }
            var m = new bool[W * H];
            System.Threading.Tasks.Parallel.For(0, H, y =>
            {
                for (int x = 0; x < W; x++)
                {
                    int k = y * W + x;
                    if (cls[k] < 0 || !zone(k)) continue;
                    if (Flips(x, y, 1, 0) >= minFlips && Flips(x, y, 0, 1) >= minFlips) m[k] = true;
                }
            });
            return m;
        }

        public static bool[] Dilate(GlbTexPaint tp, bool[] m, int r, Func<int, bool> zone = null)
        {
            int W = tp.W, H = tp.H;
            var o = (bool[])m.Clone();
            for (int it = 0; it < r; it++)
            {
                var src = (bool[])o.Clone();
                for (int y = 1; y < H - 1; y++)
                    for (int x = 1; x < W - 1; x++)
                    {
                        int k = y * W + x;
                        if (src[k] || tp.Tri[k] < 0 || (zone != null && !zone(k))) continue;
                        if (src[k - 1] || src[k + 1] || src[k - W] || src[k + W]) o[k] = true;
                    }
            }
            return o;
        }

        /// <summary>UV islands of the atlas (4-connected used texels); returns the island id per texel (-1 unused).</summary>
        public static int[] Islands(GlbTexPaint tp, out int count)
        {
            int W = tp.W, H = tp.H;
            var lab = new int[W * H]; for (int i = 0; i < lab.Length; i++) lab[i] = -1;
            var st = new Stack<int>();
            count = 0;
            for (int s = 0; s < lab.Length; s++)
            {
                if (tp.Tri[s] < 0 || lab[s] >= 0) continue;
                lab[s] = count; st.Push(s);
                while (st.Count > 0)
                {
                    int k = st.Pop(); int x = k % W, y = k / W;
                    if (x > 0 && tp.Tri[k - 1] >= 0 && lab[k - 1] < 0) { lab[k - 1] = count; st.Push(k - 1); }
                    if (x < W - 1 && tp.Tri[k + 1] >= 0 && lab[k + 1] < 0) { lab[k + 1] = count; st.Push(k + 1); }
                    if (y > 0 && tp.Tri[k - W] >= 0 && lab[k - W] < 0) { lab[k - W] = count; st.Push(k - W); }
                    if (y < H - 1 && tp.Tri[k + W] >= 0 && lab[k + W] < 0) { lab[k + W] = count; st.Push(k + W); }
                }
                count++;
            }
            return lab;
        }

        /// <summary>Grows a partial detection to whole UV islands: islands where at least 'frac' of the texels are
        /// flagged (and 'minFlagged' texels) become fully flagged.</summary>
        public static bool[] IslandVote(GlbTexPaint tp, bool[] flagged, float frac, int minFlagged)
        {
            var lab = Islands(tp, out int n);
            var tot = new int[n]; var hit = new int[n];
            for (int k = 0; k < lab.Length; k++) { if (lab[k] < 0) continue; tot[lab[k]]++; if (flagged[k]) hit[lab[k]]++; }
            var o = new bool[lab.Length];
            for (int k = 0; k < lab.Length; k++) { int l = lab[k]; if (l >= 0 && (flagged[k] || (hit[l] >= minFlagged && hit[l] >= frac * tot[l]))) o[k] = true; }
            return o;
        }

        /// <summary>Median luminance of the texels passing 'mask' (reference level for <see cref="GlbTexPaint.Retint"/>).</summary>
        public static float MedianLum(GlbTexPaint tp, Func<int, bool> mask)
        {
            var l = new List<float>();
            for (int k = 0; k < tp.Px.Length; k += 3) if (tp.Tri[k] >= 0 && mask(k)) l.Add(Lum(tp.Px[k]));
            if (l.Count == 0) return 0.5f;
            l.Sort(); return l[l.Count / 2];
        }

        /// <summary>Softens the scan's baked-in high-frequency hair noise (woven / cross-hatched strands): texels of the
        /// mask are pulled toward a blurred version of themselves.</summary>
        public static void Smooth(GlbTexPaint tp, Func<int, bool> mask, int r, float amount)
        {
            int W = tp.W, H = tp.H;
            var src = (Color[])tp.Px.Clone();
            // separable box blur restricted to masked texels
            var tmp = new Color[W * H];
            System.Threading.Tasks.Parallel.For(0, H, y =>
            {
                for (int x = 0; x < W; x++)
                {
                    int k = y * W + x; if (tp.Tri[k] < 0 || !mask(k)) continue;
                    Color acc = Color.clear; int c = 0;
                    for (int i = -r; i <= r; i++) { int xx = x + i; if (xx < 0 || xx >= W) continue; int kk = y * W + xx; if (tp.Tri[kk] < 0 || !mask(kk)) continue; acc += src[kk]; c++; }
                    tmp[k] = acc / Mathf.Max(1, c);
                }
            });
            var outp = new Color[W * H];
            System.Threading.Tasks.Parallel.For(0, H, y =>
            {
                for (int x = 0; x < W; x++)
                {
                    int k = y * W + x; if (tp.Tri[k] < 0 || !mask(k)) continue;
                    Color acc = Color.clear; int c = 0;
                    for (int i = -r; i <= r; i++) { int yy = y + i; if (yy < 0 || yy >= H) continue; int kk = yy * W + x; if (tp.Tri[kk] < 0 || !mask(kk)) continue; acc += tmp[kk]; c++; }
                    outp[k] = acc / Mathf.Max(1, c);
                }
            });
            for (int k = 0; k < tp.Px.Length; k++)
            {
                if (tp.Tri[k] < 0 || !mask(k)) continue;
                var b = outp[k];
                tp.Px[k] = new Color(Mathf.Lerp(src[k].r, b.r, amount), Mathf.Lerp(src[k].g, b.g, amount), Mathf.Lerp(src[k].b, b.b, amount), src[k].a);
                tp.Edited[k] = true;
            }
        }

        static bool InHead(Ctx c, Vector3 p, float below = 0f) => p.y > c.LM.ChinY - below && (p - c.LM.HeadCenter).magnitude < 0.2f * c.Scan.H / 1.75f;

        /// <summary>Front-most texel near (x, y) whose colour passes 'ok' (face skin / eye white: bangs hang in front).</summary>
        static float FaceZ(Ctx c, float x, float y, float tol, Func<Color, bool> ok)
        {
            var tp = c.TP; float bz = float.MinValue;
            for (int k = 0; k < tp.Pos.Length; k++)
            {
                if (tp.Tri[k] < 0) continue;
                var p = tp.Pos[k];
                if (Mathf.Abs(p.x - x) > tol || Mathf.Abs(p.y - y) > tol || p.z <= bz || !ok(tp.Px[k])) continue;
                bz = p.z;
            }
            return bz;
        }

        static bool FaceColour(Color col) => SkinLike(col) || (Lum(col) > 0.72f && Sat(col) < 0.2f);

        /// <summary>Iris / pupil texels of both eyes: inside the eye opening, on the eye surface (not the fringe in front).</summary>
        static Func<int, bool> IrisMask(Ctx c, float halfW = 0.013f, float halfH = 0.011f)
        {
            var tp = c.TP;
            var e = new[] { Eye(c, 0), Eye(c, 1) };
            var zf = new float[2];
            for (int s = 0; s < 2; s++) { zf[s] = FaceZ(c, e[s].x, e[s].y, 0.006f, FaceColour); if (zf[s] < -1f) zf[s] = e[s].z + 0.012f; }
            c.Note($"eye surface z {zf[0]:F4} / {zf[1]:F4}");
            return k =>
            {
                var p = tp.Pos[k];
                for (int s = 0; s < 2; s++)
                    if (Mathf.Abs(p.x - e[s].x) < halfW && Mathf.Abs(p.y - e[s].y) < halfH && p.z > zf[s] - 0.007f && p.z < zf[s] + 0.004f && tp.Nrm[k].z > 0.15f) return true;
                return false;
            };
        }

        // ================================================================== P06 권태겸: away from the source look
        // navy double-breasted jacket with brass buttons, solid camel scarf, cream shirt + burgundy tie in the V, chestnut
        // hair, amber eyes, fringe lifted off the eyes, dark brown mantle, charcoal trousers, brown shoes (no glasses).
        static void FixP06(Ctx c)
        {
            var tp = c.TP; var lm = c.LM; float S = c.Scan.H / 1.75f;
            Vector3 hc = lm.HeadCenter;
            // --- scarf: everything wrapped around the neck (checks) -> solid camel wool
            bool Scarf(Vector3 p) => p.y > lm.ShoulderY - 0.05f * S && p.y < lm.ChinY + 0.015f && new Vector2(p.x - hc.x, p.z - hc.z).magnitude < 0.105f * S;
            Func<int, bool> neckZone = k => { var p = tp.Pos[k]; return p.y > lm.ShoulderY - 0.1f * S && p.y < lm.ChinY + 0.03f && new Vector2(p.x - hc.x, p.z - hc.z).magnitude < 0.2f * S; };
            var chkI = IslandVote(tp, Dilate(tp, Checker(tp, neckZone, 12, 2), 4, neckZone), 0.2f, 40);
            var chk = new bool[chkI.Length];
            float vTip0 = lm.ChestY + 0.045f * S, vTop0 = lm.ShoulderY + 0.03f * S;
            bool InV(Vector3 p) { if (p.z < 0.02f || p.y < vTip0 || p.y > vTop0 - 0.035f * S) return false; float t = (p.y - vTip0) / (vTop0 - vTip0); return Mathf.Abs(p.x) < 0.012f + 0.066f * S * t; }
            bool Knot(Vector3 p) => p.y > lm.ShoulderY - 0.03f * S && p.y < lm.ChinY && Mathf.Abs(p.x - hc.x) < 0.06f * S && p.z > hc.z;
            for (int k = 0; k < chk.Length; k++) chk[k] = (chkI[k] && neckZone(k) && !InV(tp.Pos[k])) || (Knot(tp.Pos[k]) && !InV(tp.Pos[k]) && (Lum(tp.Px[k]) < 0.3f || (Lum(tp.Px[k]) > 0.45f && Sat(tp.Px[k]) < 0.22f)));
            var camel = new Color(0.66f, 0.52f, 0.36f);
            int nScarf = tp.Apply(k =>
            {
                var p = tp.Pos[k]; var col = tp.Px[k];
                if (chk[k] && !SkinLike(col)) return true;
                if (!Scarf(p) || SkinLike(col) || InV(p)) return false;
                if (p.y > lm.ChinY - 0.03f && p.z < hc.z - 0.02f && Lum(col) < 0.3f && Hue(col) > 0.6f) return false;   // hair tips at the nape
                return Lum(col) < 0.3f || (Lum(col) > 0.45f && Sat(col) < 0.22f);
            }, (k, col) =>
            {
                float sh = 0.9f + 0.12f * tp.Nrm[k].y + 0.04f * Mathf.Sin((tp.Pos[k].x + tp.Pos[k].y * 0.5f) * 900f);   // fine knit rib
                return new Color(camel.r * sh, camel.g * sh, camel.b * sh, 1f);
            });
            c.Note($"checkered scarf -> solid camel: {nScarf}");
            // leftover check squares at the scarf edges (missed by the pattern test): islands mostly camel now -> all camel
            {
                var cm = new bool[tp.Px.Length];
                for (int k = 0; k < cm.Length; k++) cm[k] = tp.Edited[k] && neckZone(k);
                var grown = IslandVote(tp, cm, 0.3f, 30);
                int nLeft = tp.Apply(k => grown[k] && !tp.Edited[k] && neckZone(k) && !InV(tp.Pos[k]) && !SkinLike(tp.Px[k]) && (Lum(tp.Px[k]) < 0.3f || (Lum(tp.Px[k]) > 0.45f && Sat(tp.Px[k]) < 0.22f)),
                    (k, col) => { float sh = 0.9f + 0.12f * tp.Nrm[k].y; return new Color(camel.r * sh, camel.g * sh, camel.b * sh, 1f); });
                c.Note($"scarf leftovers -> camel: {nLeft}");
            }
            // --- the checkered V on the chest -> cream shirt front with a burgundy tie
            float vTip = lm.ChestY + 0.045f * S, vTop = lm.ShoulderY + 0.03f * S;
            int nBib = tp.Apply(k =>
            {
                var p = tp.Pos[k]; var col = tp.Px[k];
                if (tp.Edited[k] || p.z < 0.02f || p.y < vTip || p.y > vTop) return false;
                float t = (p.y - vTip) / (vTop - vTip);
                if (Mathf.Abs(p.x) > 0.004f + 0.066f * S * t) return false;
                if (Sat(col) > 0.4f) return false;    // buttons
                return Lum(col) < 0.3f || (Lum(col) > 0.45f && Sat(col) < 0.22f);
            }, (k, col) =>
            {
                var p = tp.Pos[k];
                float t = (p.y - vTip) / (vTop - vTip);
                float sh = 0.92f + 0.1f * tp.Nrm[k].y;
                bool tie = Mathf.Abs(p.x) < 0.0105f * S + 0.004f * (1f - t);
                if (tie)
                {
                    float stripe = Mathf.Repeat((p.x * 0.8f + p.y) * 160f, 1f) < 0.18f ? 1.18f : 1f;
                    return new Color(0.36f * sh * stripe, 0.12f * sh * stripe, 0.12f * sh * stripe, 1f);
                }
                return new Color(0.9f * sh, 0.87f * sh, 0.8f * sh, 1f);
            });
            c.Note($"chest V -> cream shirt + tie: {nBib}");

            // --- eyes: grey iris -> warm amber (pupil keeps its depth)
            var iris = IrisMask(c);
            int nEye = tp.Apply(k => iris(k) && Lum(tp.Px[k]) < 0.72f && Sat(tp.Px[k]) < 0.25f && !SkinLike(tp.Px[k]),
                (k, col) => GlbTexPaint.Retint(col, new Color(0.64f, 0.4f, 0.17f), 0.42f, 1f));
            c.Note($"iris -> amber: {nEye}");

            // --- hair (and brows) -> chestnut
            float hairRef = MedianLum(tp, k => InHead(c, tp.Pos[k]) && Lum(tp.Px[k]) < 0.3f && !iris(k));
            var eyeA = Eye(c, 0); var eyeB = Eye(c, 1);
            float zEyeSurf = Mathf.Max(FaceZ(c, eyeA.x, eyeA.y, 0.006f, FaceColour), FaceZ(c, eyeB.x, eyeB.y, 0.006f, FaceColour));
            // lashes / eyeliner stay dark: texels on the eye surface around each opening are not hair
            bool Liner(Vector3 p) { foreach (var q in new[] { eyeA, eyeB }) if (Mathf.Abs(p.x - q.x) < 0.022f && p.y - q.y > -0.013f && p.y - q.y < 0.012f && p.z > zEyeSurf - 0.012f && p.z < zEyeSurf + 0.002f) return true; return false; }
            int nHair = tp.Apply(k => !tp.Edited[k] && InHead(c, tp.Pos[k], 0.03f) && !iris(k) && !Liner(tp.Pos[k]) && Lum(tp.Px[k]) < 0.36f && !SkinLike(tp.Px[k]),
                (k, col) => GlbTexPaint.Retint(col, new Color(0.34f, 0.23f, 0.15f), hairRef, 0.85f));
            c.Note($"hair -> chestnut (ref {hairRef:F2}): {nHair}");

            // --- body garments
            bool Body(Vector3 p) => p.y < lm.ChinY - 0.005f;
            bool Hand(Vector3 p) => Mathf.Abs(p.x) > lm.ShoulderX + 0.42f * S;
            bool Leg(Vector3 p) => p.y < lm.CrotchY + 0.03f && new Vector2(Mathf.Abs(p.x) - lm.HipX, p.z).magnitude < 0.105f * S;
            int nBtn = tp.Apply(k => !tp.Edited[k] && Body(tp.Pos[k]) && Sat(tp.Px[k]) > 0.4f && (Hue(tp.Px[k]) < 0.06f || Hue(tp.Px[k]) > 0.92f) && !SkinLike(tp.Px[k]),
                (k, col) => GlbTexPaint.Retint(col, new Color(0.72f, 0.56f, 0.3f), 0.3f, 0.8f));
            c.Note($"red buttons -> brass: {nBtn}");
            float whiteRef = MedianLum(tp, k => Body(tp.Pos[k]) && !Hand(tp.Pos[k]) && Lum(tp.Px[k]) > 0.55f && Sat(tp.Px[k]) < 0.15f);
            int nJk = tp.Apply(k => !tp.Edited[k] && Body(tp.Pos[k]) && !Hand(tp.Pos[k]) && Lum(tp.Px[k]) > 0.3f && Sat(tp.Px[k]) < 0.2f && !SkinLike(tp.Px[k]),
                (k, col) => GlbTexPaint.Retint(col, new Color(0.14f, 0.18f, 0.31f), whiteRef, 1f));
            c.Note($"white jacket -> navy (ref {whiteRef:F2}): {nJk}");
            float darkRef = MedianLum(tp, k => Body(tp.Pos[k]) && Lum(tp.Px[k]) < 0.3f);
            int nD = tp.Apply(k => !tp.Edited[k] && Body(tp.Pos[k]) && Lum(tp.Px[k]) <= 0.3f && !SkinLike(tp.Px[k]),
                (k, col) =>
                {
                    var p = tp.Pos[k];
                    Color tint = p.y < 0.11f * S ? new Color(0.24f, 0.14f, 0.09f) : Leg(p) ? new Color(0.17f, 0.17f, 0.19f) : new Color(0.2f, 0.15f, 0.12f);
                    return GlbTexPaint.Retint(col, tint, Mathf.Max(0.05f, darkRef), 0.9f);
                });
            c.Note($"dark garments (ref {darkRef:F2}): {nD}");
        }

        // ================================================================== P10 강준서: bottle-green coat + mole under the left eye
        static void FixP10(Ctx c)
        {
            var tp = c.TP; var lm = c.LM; float S = c.Scan.H / 1.75f;
            bool Blond(Color col) => Hue(col) > 0.07f && Hue(col) < 0.18f && Sat(col) > 0.28f && Lum(col) > 0.28f;
            // skin-coloured patch baked into the fringe above the forehead -> hair
            var e = lm.EyeCenter;
            Color hairMed; { var l = new List<Color>(); for (int k = 0; k < tp.Px.Length; k += 5) if (tp.Tri[k] >= 0 && InHead(c, tp.Pos[k]) && Blond(tp.Px[k])) l.Add(tp.Px[k]); hairMed = l.Count > 0 ? l.OrderBy(Lum).ElementAt(l.Count / 2) : new Color(0.8f, 0.62f, 0.3f); }
            int nPatch = tp.Apply(k =>
            {
                var p = tp.Pos[k];
                return InHead(c, p) && p.y > e.y + 0.045f * S && p.z > lm.HeadCenter.z + 0.01f && SkinLike(tp.Px[k]) && !Blond(tp.Px[k]);
            }, (k, col) => { float sh = 0.85f + 0.25f * Mathf.Clamp01(tp.Nrm[k].y * 0.5f + 0.5f); return new Color(hairMed.r * sh, hairMed.g * sh, hairMed.b * sh, 1f); });
            c.Note($"skin patch in the fringe -> hair: {nPatch}");
            bool Body(Vector3 p) => p.y < lm.ChinY + 0.045f;
            float greyRef = MedianLum(tp, k => tp.Pos[k].y < lm.ChinY && Lum(tp.Px[k]) > 0.28f && Sat(tp.Px[k]) < 0.14f);
            int n = tp.Apply(k => !tp.Edited[k] && Body(tp.Pos[k]) && Lum(tp.Px[k]) > 0.2f && Sat(tp.Px[k]) < 0.2f && !SkinLike(tp.Px[k]) && !Blond(tp.Px[k]) && !(InHead(c, tp.Pos[k]) && tp.Pos[k].z > lm.HeadCenter.z),
                (k, col) => GlbTexPaint.Retint(col, new Color(0.13f, 0.27f, 0.18f), greyRef, 1f));
            c.Note($"grey coat -> bottle green (ref {greyRef:F2}): {n}");
            // cape-free scan (glb_fixed): the shoulders and upper back were under the cape and carry its grey / brownish shading;
            // everything left there that is not hair, skin or a dark button / glove becomes coat green too
            if (c.Def.Id == "P10")
            {
                int nb = tp.Apply(k => !tp.Edited[k] && Body(tp.Pos[k]) && tp.Pos[k].y > lm.ChinY - 0.32f * S && Lum(tp.Px[k]) > 0.1f && Sat(tp.Px[k]) < 0.5f && !SkinLike(tp.Px[k]) && !Blond(tp.Px[k]),
                    (k, col) => GlbTexPaint.Retint(col, new Color(0.13f, 0.27f, 0.18f), greyRef, 1f));
                c.Note($"uncovered shoulders -> bottle green: {nb}");
            }
            // hair: soften the woven cross-hatch noise, keep the strand shading
            Smooth(tp, k => InHead(c, tp.Pos[k], 0.02f) && Blond(tp.Px[k]), 3, 0.75f);
            Mole(c, 1);
        }

        /// <summary>Small beauty mark just below the lower lash line of eye s, slightly toward the outer corner.</summary>
        static void Mole(Ctx c, int s)
        {
            var cal = GlbFace.Get(c.Def.Id);
            var e = Eye(c, s);
            float x = e.x + (s == 0 ? 1f : -1f) * 0.0045f;
            float y = e.y + cal.EyeBot.y * 0.001f - 0.0045f;
            float z = FaceZ(c, x, y, 0.0012f, SkinLike);
            var p = new Vector3(x, y, z > -1f ? z : e.z + 0.012f);
            int k = c.TP.Dot(p, Vector3.forward, 0.0012f, new Color(0.22f, 0.13f, 0.1f), 0.95f);
            c.Note($"mole at {p.ToString("F4")} ({k} texels)");
        }

        // ================================================================== cleanup of the older scans
        static void FixP05(Ctx c)
        {
            var tp = c.TP; var lm = c.LM; float S = c.Scan.H / 1.75f;
            // left glove (character's left = -X): gold-ish smear on the fingers -> glove black
            int n = tp.Apply(k =>
            {
                var p = tp.Pos[k];
                if (!(p.x < -(lm.ShoulderX + 0.1f) && p.y < lm.WristL.y + 0.01f && p.y > lm.TipL.y - 0.04f)) return false;
                return Lum(tp.Px[k]) > 0.2f && !(Hue(tp.Px[k]) > 0.93f || Hue(tp.Px[k]) < 0.03f) || Sat(tp.Px[k]) > 0.25f && Hue(tp.Px[k]) > 0.05f && Hue(tp.Px[k]) < 0.2f;
            }, (k, col) => new Color(0.07f, 0.065f, 0.07f, 1f) * (0.9f + 0.2f * Mathf.Clamp01(tp.Nrm[k].y + 0.5f)));
            c.Note($"left glove smear -> black leather: {n}");
            // red specks in the black hair
            int h = tp.Apply(k => InHead(c, tp.Pos[k]) && tp.Pos[k].y > lm.EyeCenter.y + 0.02f && Sat(tp.Px[k]) > 0.3f && (Hue(tp.Px[k]) > 0.9f || Hue(tp.Px[k]) < 0.05f) && Lum(tp.Px[k]) < 0.4f,
                (k, col) => { float l = Lum(col); return new Color(l * 0.8f, l * 0.85f, l * 0.9f, 1f); });
            c.Note($"red specks in hair: {h}");
            // yellow smear at the left mouth corner -> skin (inpainted), then a clean gold tooth in the grin
            var e = lm.EyeCenter;
            var smear = new Vector2(e.x - 0.027f, e.y - 0.05f);
            var mask = new bool[tp.Px.Length];
            int ns = 0;
            for (int k = 0; k < mask.Length; k++)
            {
                if (tp.Tri[k] < 0) continue;
                var p = tp.Pos[k];
                if (p.z < lm.HeadCenter.z || (new Vector2(p.x, p.y) - smear).magnitude > 0.011f) continue;
                var col = tp.Px[k];
                if (Sat(col) > 0.18f && Hue(col) > 0.07f && Hue(col) < 0.2f || (Lum(col) < 0.55f && Sat(col) < 0.12f && Lum(col) > 0.2f)) { mask[k] = true; ns++; }
            }
            mask = Dilate(tp, mask, 2);
            int nf = tp.Inpaint(mask, 3);
            c.Note($"mouth-corner smear -> skin: {ns} ({nf} filled)");
            var gt = new Vector2(e.x - 0.013f, e.y - 0.0565f);
            float gz = FaceZ(c, gt.x, gt.y, 0.002f, col => Lum(col) > 0.7f);
            int ng = 0;
            if (gz > -1f)
                for (int k = 0; k < tp.Px.Length; k++)
                {
                    if (tp.Tri[k] < 0) continue;
                    var p = tp.Pos[k];
                    if (Mathf.Abs(p.x - gt.x) > 0.0017f || Mathf.Abs(p.y - gt.y) > 0.0022f || p.z < gz - 0.004f || Lum(tp.Px[k]) < 0.6f) continue;
                    float l = Lum(tp.Px[k]);
                    tp.Px[k] = new Color(0.86f * l + 0.1f, 0.66f * l + 0.06f, 0.24f * l, 1f); tp.Edited[k] = true; ng++;
                }
            c.Note($"gold tooth: {ng}");
        }

        // P04: narrow pale eyes read as slits in close-ups -> a deeper iris with a clear pupil and catch light;
        // the white turtleneck is toned to off-white (it burned into bloom under the dialogue key light)
        static void FixP04(Ctx c)
        {
            var tp = c.TP; var lm = c.LM; float S = c.Scan.H / 1.75f;
            CleanPaintedStrands(c, 44f, false, 5.5f);
            EyeBoost(c, new Color(0.3f, 0.36f, 0.5f), 0.011f, 0.0065f);
            int n = tp.Apply(k =>
            {
                var p = tp.Pos[k];
                return p.y < lm.ChinY + 0.01f && p.y > lm.ShoulderY - 0.12f * S && Mathf.Abs(p.x) < 0.13f * S && Lum(tp.Px[k]) > 0.7f && Sat(tp.Px[k]) < 0.12f && !SkinLike(tp.Px[k]);
            }, (k, col) => new Color(col.r * 0.87f, col.g * 0.855f, col.b * 0.83f, col.a));
            c.Note($"turtleneck -> off-white: {n}");
        }

        /// <summary>
        /// Dark hair strokes painted ON the facial skin around the eyes (generated-texture artifacts that read as hair
        /// shards slicing the eye): dark texels lying on the skin surface (not real strands in front of it), outside the
        /// eye opening and the brows, are inpainted from the surrounding skin.
        /// </summary>
        static void CleanPaintedStrands(Ctx c, float reachMM = 34f, bool keepBrows = true, float irisR = 5f)
        {
            var tp = c.TP; var lm = c.LM; var cal = GlbFace.Get(c.Def.Id);
            var e = new[] { Eye(c, 0), Eye(c, 1) };
            const float cs = 0.002f;
            var zmap = new Dictionary<(int, int), float>();
            for (int k = 0; k < tp.Px.Length; k += 2)
            {
                if (tp.Tri[k] < 0 || !SkinLike(tp.Px[k])) continue;
                var p = tp.Pos[k];
                if (p.z < lm.HeadCenter.z || Mathf.Abs(p.y - e[0].y) > 0.06f || Mathf.Abs(p.x - lm.EyeCenter.x) > 0.1f) continue;
                var key = (Mathf.FloorToInt(p.x / cs), Mathf.FloorToInt(p.y / cs));
                if (!zmap.TryGetValue(key, out float z) || p.z < z) zmap[key] = p.z;   // rear-most skin (painted undersides float in front)
            }
            float SkinZ(Vector3 p)
            {
                int cx = Mathf.FloorToInt(p.x / cs), cy = Mathf.FloorToInt(p.y / cs);
                for (int r = 0; r <= 6; r++)
                {
                    float best = float.MinValue;
                    for (int dx = -r; dx <= r; dx++) for (int dy = -r; dy <= r; dy++)
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) == r && zmap.TryGetValue((cx + dx, cy + dy), out float z) && z > best) best = z;
                    if (best > float.MinValue) return best;
                }
                return float.MinValue;
            }
            var mask = new bool[tp.Px.Length];
            Color skinMed; { var l = new List<Color>(); for (int k = 0; k < tp.Px.Length; k += 3) { if (tp.Tri[k] < 0 || !SkinLike(tp.Px[k])) continue; var p = tp.Pos[k]; if (p.z > lm.HeadCenter.z && Mathf.Abs(p.y - (e[0].y - 0.03f)) < 0.02f && Mathf.Abs(p.x - lm.EyeCenter.x) < 0.05f) l.Add(tp.Px[k]); } skinMed = l.Count > 0 ? l.OrderBy(Lum).ElementAt(l.Count / 2) : new Color(0.95f, 0.86f, 0.8f); }
            int n = 0;
            for (int k = 0; k < tp.Px.Length; k++)
            {
                if (tp.Tri[k] < 0) continue;
                var col = tp.Px[k];
                if (SkinLike(col)) continue;
                bool dark = Lum(col) <= 0.3f;
                if (!dark && keepBrows) continue;
                var p = tp.Pos[k];
                if (p.z < lm.HeadCenter.z) continue;
                int s = Mathf.Abs(p.x - e[0].x) < Mathf.Abs(p.x - e[1].x) ? 0 : 1;
                float xmm = (s == 0 ? e[0].x - p.x : p.x - e[1].x) * 1000f;     // toward the nose
                float ymm = (p.y - e[s].y) * 1000f;
                if (Mathf.Abs(xmm) > reachMM || ymm < -26f || ymm > 30f) continue;
                // eye opening (+ lashes / liner margin) and the brow band stay
                GlbFaceMask.EyeTopBot(cal, Mathf.Clamp(xmm, cal.EyeOut.x, cal.EyeIn.x), out float top, out float bot);
                bool inOpening = xmm > cal.EyeOut.x && xmm < cal.EyeIn.x && ymm > bot && ymm < top;
                if (inOpening)
                {
                    // strands drawn across the eye white: dark texels below the lash line, off the iris -> sclera
                    if (dark && ymm < top - 1.3f && ymm > bot + 0.8f && new Vector2(xmm, ymm).magnitude > irisR && Mathf.Abs(p.z - (SkinZ(p) > -1f ? SkinZ(p) : p.z)) < 0.004f)
                    { tp.Px[k] = new Color(0.92f, 0.92f, 0.95f, 1f); tp.Edited[k] = true; n++; }
                    continue;
                }
                if (keepBrows && xmm > cal.EyeOut.x - 3.5f && xmm < cal.EyeIn.x + 2.5f && ymm > bot - 2.5f && ymm < top + 3f) continue;
                if (!keepBrows && xmm > cal.EyeOut.x - 1f && xmm < cal.EyeIn.x + 1f && ymm > bot - 1f && ymm < top + 1.2f) continue;
                if (!keepBrows)
                {
                    // wipe the eye surround (cheekbone .. above the brow): no hair fragments may stay painted there
                    float x0 = cal.EyeOut.x - 10f, x1 = cal.EyeIn.x + 5f, y0 = cal.EyeBot.y - 9f, y1 = cal.EyeTop.y + 17f;
                    float uu = (xmm - (x0 + x1) * 0.5f) / ((x1 - x0) * 0.5f), vv = (ymm - (y0 + y1) * 0.5f) / ((y1 - y0) * 0.5f);
                    if (uu * uu + vv * vv < 1f) { float sh = 0.94f + 0.08f * Mathf.Clamp01(tp.Nrm[k].y * 0.5f + 0.5f); tp.Px[k] = new Color(skinMed.r * sh, skinMed.g * sh, skinMed.b * sh, 1f); tp.Edited[k] = true; n++; continue; }
                }
                if (keepBrows && xmm > cal.EyeOut.x - 7f && xmm < cal.EyeIn.x + 5f && ymm >= top + 3f && ymm < top + 17f) continue;
                if (!dark) continue;
                float sz = SkinZ(p);
                if (sz < -1f || Mathf.Abs(p.z - sz) > 0.0014f) continue;    // real strands float in front of the skin
                mask[k] = true; n++;
            }
            mask = Dilate(tp, mask, 1);
            int f = tp.Inpaint(mask, 2);
            c.Note($"painted strands on the face -> skin: {n} texels ({f} filled)");
        }

        /// <summary>Clearer anime eyes on a scan: iris retinted deeper (structure kept), a dark pupil and a small catch light.</summary>
        static void EyeBoost(Ctx c, Color irisTint, float halfW, float halfH)
        {
            var tp = c.TP;
            var iris = IrisMask(c, halfW, halfH);
            // iris texels per eye (their centroid places the pupil: robust to small calibration offsets)
            var acc = new Vector3[2]; var cnt = new int[2];
            var e0 = Eye(c, 0);
            for (int k = 0; k < tp.Px.Length; k++)
            {
                if (tp.Tri[k] < 0 || !iris(k) || Lum(tp.Px[k]) >= 0.8f || SkinLike(tp.Px[k]) || Lum(tp.Px[k]) <= 0.28f) continue;
                int s = Mathf.Abs(tp.Pos[k].x - e0.x) < 0.02f ? 0 : 1;
                acc[s] += tp.Pos[k]; cnt[s]++;
            }
            int n = tp.Apply(k => iris(k) && Lum(tp.Px[k]) < 0.8f && !SkinLike(tp.Px[k]) && Lum(tp.Px[k]) > 0.28f,
                (k, col) => GlbTexPaint.Retint(col, irisTint, 0.55f, 0.8f));
            int np = 0, nh = 0;
            for (int s = 0; s < 2; s++)
            {
                if (cnt[s] < 10) continue;
                var ce = acc[s] / cnt[s];
                var e = new Vector3(Mathf.Lerp(Eye(c, s).x, ce.x, 0.5f), Mathf.Lerp(Eye(c, s).y, ce.y, 0.5f), ce.z);
                float z = ce.z + 0.0015f;
                var pc = new Vector3(e.x, e.y - 0.0003f, z);
                np += tp.Dot(pc, Vector3.forward, 0.0011f, new Color(0.05f, 0.05f, 0.08f), 0.9f);
                var hl = new Vector3(e.x + (s == 0 ? 0.0012f : -0.0012f), e.y + 0.0016f, z);
                nh += tp.Dot(hl, Vector3.forward, 0.00065f, new Color(1f, 1f, 1f), 0.95f);
            }
            c.Note($"eyes: iris {n}, pupil {np}, catch light {nh}");
        }

        /// <summary>
        /// Skin-coloured paint on hair geometry (the undersides / tips of fringe strands baked with the face colour -
        /// "skin stuck to the hair"): skin-coloured texels floating clearly in front of the facial skin surface are
        /// repainted in the hair colour. The face surface per 2 mm cell is the rear-most skin texel there.
        /// </summary>
        static void HairSkinPatches(Ctx c)
        {
            var tp = c.TP; var lm = c.LM; var e = lm.EyeCenter;
            const float cs = 0.002f;
            bool Region(Vector3 p) => p.z > lm.HeadCenter.z + 0.01f && Mathf.Abs(p.x - e.x) < 0.085f && p.y > e.y - 0.035f && p.y < e.y + 0.08f;
            var zmin = new Dictionary<(int, int), float>();
            var hair = new List<Color>();
            for (int k = 0; k < tp.Px.Length; k++)
            {
                if (tp.Tri[k] < 0) continue;
                var p = tp.Pos[k];
                if (!Region(p)) { if (InHead(c, p) && p.y > e.y + 0.05f && !SkinLike(tp.Px[k]) && (k % 7) == 0) hair.Add(tp.Px[k]); continue; }
                if (!SkinLike(tp.Px[k])) continue;
                var key = (Mathf.FloorToInt(p.x / cs), Mathf.FloorToInt(p.y / cs));
                if (!zmin.TryGetValue(key, out float z) || p.z < z) zmin[key] = p.z;
            }
            if (hair.Count < 50) return;
            var hm = hair.OrderBy(Lum).ElementAt(hair.Count / 2);
            float FaceZAt(Vector3 p)
            {
                int cx = Mathf.FloorToInt(p.x / cs), cy = Mathf.FloorToInt(p.y / cs);
                float best = float.MaxValue;
                for (int dx = -2; dx <= 2; dx++) for (int dy = -2; dy <= 2; dy++) if (zmin.TryGetValue((cx + dx, cy + dy), out float z) && z < best) best = z;
                return best;
            }
            int n = tp.Apply(k =>
            {
                var p = tp.Pos[k];
                if (!Region(p) || !SkinLike(tp.Px[k])) return false;
                float fz = FaceZAt(p);
                return fz < 1f && p.z > fz + 0.0035f;
            }, (k, col) => { float sh = 0.8f + 0.35f * Mathf.Clamp01(tp.Nrm[k].y * 0.5f + 0.5f); return new Color(hm.r * sh, hm.g * sh, hm.b * sh, 1f); });
            c.Note($"skin-coloured paint on hair strands -> hair: {n} texels");
        }

        static void FixP01(Ctx c) { }
        static void FixP02(Ctx c) { }
    }
}
