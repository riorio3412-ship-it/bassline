using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>Geometry edits of the scans: head-bound accessories and the fringe lift over the eyes.</summary>
    public static partial class GlbFix
    {
        /// <summary>Texel (uv) painted with the accessory colour, per actor (accessory vertices sample it).</summary>
        public static readonly Dictionary<string, Vector2> AccUV = new Dictionary<string, Vector2>();
        /// <summary>Bump when a geometry edit changes (part of the decimation cache key).</summary>
        public const int GeometryVersion = 3;

        /// <summary>Adds modelled accessories to the scan's head-bound extra parts (Studs: weighted 100% to the head).</summary>
        public static void AddAccessories(CastDef def, GlbRigger.Scan scan, Texture2D tex, System.Text.StringBuilder log)
        {
            // (P06 glasses were removed at the user's request; Glasses() is kept for reference / other actors)
        }

        /// <summary>Paints a small solid block into an unused corner of the atlas and returns its uv.</summary>
        static Vector2 PaintAccessoryTexel(GlbTexPaint tp, Color col, string id)
        {
            int W = tp.W, H = tp.H, B = 10;
            for (int y = 4; y + B < H; y += B)
                for (int x = 4; x + B < W; x += B)
                {
                    bool free = true;
                    for (int yy = y - 3; yy < y + B + 3 && free; yy++) for (int xx = x - 3; xx < x + B + 3; xx++) if (tp.Tri[yy * W + xx] >= 0) { free = false; break; }
                    if (!free) continue;
                    for (int yy = y - 3; yy < y + B + 3; yy++) for (int xx = x - 3; xx < x + B + 3; xx++) { tp.Px[yy * W + xx] = col; tp.Edited[yy * W + xx] = true; }
                    var uv = new Vector2((x + B * 0.5f) / W, (y + B * 0.5f) / H);
                    AccUV[id] = uv;
                    return uv;
                }
            return Vector2.zero;
        }

        /// <summary>Front-most scan vertex near (x, y) whose texture colour passes 'ok'.</summary>
        static float SurfaceZ(GlbRigger.Scan scan, Texture2D tex, float x, float y, float r, System.Func<Color, bool> ok)
        {
            float best = float.MinValue;
            for (int i = 0; i < scan.V.Count; i++)
            {
                var p = scan.V[i];
                if (Mathf.Abs(p.x - x) > r || Mathf.Abs(p.y - y) > r || p.z <= best) continue;
                if (ok != null && tex != null && !ok(tex.GetPixelBilinear(scan.UV[i].x, scan.UV[i].y))) continue;
                best = p.z;
            }
            return best;
        }

        /// <summary>Largest |x| of the head surface at height y, depth z (for the temple arms).</summary>
        static float SideX(GlbRigger.Scan scan, float y, float z, float r, int sign)
        {
            float best = 0f;
            for (int i = 0; i < scan.V.Count; i++)
            {
                var p = scan.V[i];
                if (Mathf.Abs(p.y - y) > r || Mathf.Abs(p.z - z) > r) continue;
                float sx = p.x * sign;
                if (sx > best && sx < 0.15f) best = sx;
            }
            return best;
        }

        /// <summary>
        /// Thin rectangular half-rim glasses (dark tortoise frame) fitted to the scanned face: lenses around the
        /// calibrated iris centres, a bridge over the nose, temple arms following the side of the head to the ears.
        /// </summary>
        static void Glasses(CastDef def, GlbRigger.Scan scan, Texture2D tex, System.Text.StringBuilder log)
        {
            var lm = scan.LM; var cal = GlbFace.Get(def.Id);
            float S = scan.H / 1.75f;
            var e = lm.EyeCenter;
            float ey = e.y + cal.EyeDY - 0.0015f;
            // face surface in front of the eyes / nose bridge (skin or eye colours only: the fringe hangs in front)
            System.Func<Color, bool> face = col => SkinLike(col) || Lum(col) > 0.6f;
            float zEye = Mathf.Max(SurfaceZ(scan, tex, e.x + cal.EyeDX, ey, 0.004f, face), SurfaceZ(scan, tex, e.x - cal.EyeDX, ey, 0.004f, face));
            float zNose = SurfaceZ(scan, tex, e.x, ey - 0.004f, 0.004f, face);
            if (zEye < -1f) zEye = e.z + 0.012f;
            if (zNose < -1f) zNose = zEye + 0.01f;
            float lensZ = Mathf.Max(zEye + 0.011f, zNose + 0.002f);
            float hw = 0.0215f * Mathf.Max(0.9f, cal.EyeDX / 0.036f), hh = 0.0125f, rr = 0.0045f;   // lens half size, corner radius
            float rad = 0.0010f;
            var parts = new List<(Vector3[] v, Vector3[] n, int[] t)>();
            for (int s = -1; s <= 1; s += 2)
            {
                float cx = e.x + s * cal.EyeDX;
                // lens outline (rounded rectangle), wrapped: the outer side sits a little further back
                var ring = new List<Vector3>();
                int per = 10;
                foreach (int corner in new[] { 3, 0, 1, 2 })
                {
                    float qx = corner == 0 || corner == 3 ? 1f : -1f, qy = corner < 2 ? 1f : -1f;
                    Vector2 c0 = new Vector2(qx * (hw - rr), qy * (hh - rr));
                    float a0 = corner * Mathf.PI * 0.5f;
                    for (int k = 0; k <= per; k++)
                    {
                        float a = a0 + k * (Mathf.PI * 0.5f) / per;
                        Vector2 q = c0 + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                        // top edge a bit flatter and heavier (half-rim look): the lower edge is lifted slightly
                        if (q.y < 0) q.y *= 0.92f;
                        float outward = q.x * s;                       // + toward the temple
                        float wrap = Mathf.Max(0f, outward) / hw * 0.0045f;
                        ring.Add(new Vector3(cx + q.x, ey + q.y, lensZ - wrap - Mathf.Abs(q.y) * 0.05f));
                    }
                }
                // the lower edge is a fine wire: drawn as a thinner tube
                int i0 = ring.FindIndex(p => p.y >= ey - 0.004f), i1 = ring.FindLastIndex(p => p.y >= ey - 0.004f);
                parts.Add(Tube(ring.GetRange(i0, i1 - i0 + 1), rad * 1.15f, false));
                var lower = ring.GetRange(i1, ring.Count - i1).Concat(ring.GetRange(0, i0 + 1)).ToList();
                parts.Add(Tube(lower, rad * 0.6f, false));
                // temple arm: hinge at the outer top corner, back along the head side to the ear
                Vector3 hinge = new Vector3(cx + s * hw, ey + hh * 0.55f, lensZ - 0.0045f);
                var arm = new List<Vector3> { hinge };
                float zBack = lm.HeadCenter.z - 0.035f * S;
                for (int k = 1; k <= 6; k++)
                {
                    float t = k / 6f;
                    float z = Mathf.Lerp(hinge.z - 0.004f, zBack, t);
                    float y = hinge.y - t * 0.004f - t * t * 0.006f;
                    float side = SideX(scan, y, z, 0.006f, s);
                    float x = s * Mathf.Max(Mathf.Abs(hinge.x) * (1f - t) + (side + 0.0025f) * t, side * 0.97f + 0.001f);
                    arm.Add(new Vector3(x, y, z));
                }
                parts.Add(Tube(arm, rad, false));
            }
            // bridge: a shallow arch between the inner top corners
            {
                var br = new List<Vector3>();
                float xin = hw - 0.002f;
                for (int k = 0; k <= 8; k++)
                {
                    float t = k / 8f;
                    float x = Mathf.Lerp(e.x + cal.EyeDX - xin, e.x - cal.EyeDX + xin, t);
                    float y = ey + hh * 0.35f + Mathf.Sin(t * Mathf.PI) * 0.0035f;
                    br.Add(new Vector3(x, y, lensZ + 0.001f));
                }
                parts.Add(Tube(br, rad, false));
            }
            scan.Studs.AddRange(parts);
            log?.AppendLine($"[{def.Id}] glasses: lens z {lensZ:F3} (eye {zEye:F3}, nose {zNose:F3}), {parts.Sum(p => p.v.Length)} verts");
        }

        /// <summary>Actors whose fringe hangs over the eyes: strength of the lift (0..1).</summary>
        // the vertical squash of strands stretched their skin-coloured undersides into visible "combs": off for now;
        // strands crossing the eye openings are tucked behind the eyes instead (PushBehind)
        static readonly Dictionary<string, float> FringeLift = new Dictionary<string, float> { { "P01", 0f }, { "P06", 0f }, { "P10", 0f }, { "P04", 0f } };
        public static bool HasGeometryEdits(string id) => FringeLift.ContainsKey(id);
        static readonly HashSet<string> PushBehind = new HashSet<string> { "P06" };

        /// <summary>
        /// Lifts the fringe strands that hang in front of the eyes: hair-coloured vertices clearly in front of the facial
        /// skin surface, below a line just above the upper lids, are compressed up toward that line (the strands get
        /// shorter over the eyes, fading out sideways), so the eyes read in close-ups and no strand outline slices them.
        /// </summary>
        public static void LiftFringe(CastDef def, GlbRigger.Scan scan, List<Vector3> V, List<Vector2> UV, Texture2D tex, System.Text.StringBuilder log)
        {
            if (!FringeLift.TryGetValue(def.Id, out float k) || tex == null) return;
            var lm = scan.LM; var cal = GlbFace.Get(def.Id); var e = lm.EyeCenter;
            float ex = cal.EyeDX, eyTop = e.y + cal.EyeDY + cal.EyeTop.y * 0.001f;
            float line = eyTop + 0.0045f;
            // facial skin depth map (2 mm cells)
            const float cs = 0.002f;
            var zmap = new Dictionary<(int, int), float>();
            var zmin = new Dictionary<(int, int), float>();   // rear-most skin (strand undersides painted skin-colour float in front)
            var cols = new Color[V.Count];
            for (int i = 0; i < V.Count; i++)
            {
                var p = V[i];
                if (Mathf.Abs(p.x - e.x) > 0.09f || p.y < e.y - 0.06f || p.y > e.y + 0.06f || p.z < lm.HeadCenter.z) continue;
                cols[i] = tex.GetPixelBilinear(UV[i].x, UV[i].y);
                if (!(SkinLike(cols[i]) || (Lum(cols[i]) > 0.72f && Sat(cols[i]) < 0.2f))) continue;
                var key = (Mathf.FloorToInt(p.x / cs), Mathf.FloorToInt(p.y / cs));
                if (!zmap.TryGetValue(key, out float z) || p.z > z) zmap[key] = p.z;
                if (!zmin.TryGetValue(key, out float z2) || p.z < z2) zmin[key] = p.z;
            }
            float SkinZ(Vector3 p)
            {
                int cx = Mathf.FloorToInt(p.x / cs), cy = Mathf.FloorToInt(p.y / cs);
                float best = float.MinValue;
                // nearest ring of cells that has skin samples (a wide max would pick up the nose tip)
                for (int r = 0; r <= 6; r++)
                {
                    for (int dx = -r; dx <= r; dx++) for (int dy = -r; dy <= r; dy++)
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) == r && zmap.TryGetValue((cx + dx, cy + dy), out float z) && z > best) best = z;
                    if (best > float.MinValue) break;
                }
                return best;
            }
            float FaceZ(Vector3 p)
            {
                int cx = Mathf.FloorToInt(p.x / cs), cy = Mathf.FloorToInt(p.y / cs);
                for (int r = 0; r <= 4; r++)
                {
                    float best = float.MaxValue;
                    for (int dx = -r; dx <= r; dx++) for (int dy = -r; dy <= r; dy++)
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) == r && zmin.TryGetValue((cx + dx, cy + dy), out float z) && z < best) best = z;
                    if (best < float.MaxValue) return best;
                }
                return float.MinValue;
            }
            float halfBand = ex + 0.026f;
            int moved = 0, nCol = 0, nNoSkin = 0, nBehind = 0;
            for (int i = 0; i < V.Count; i++)
            {
                var p = V[i];
                if (p.y >= line || p.y < e.y - 0.065f || Mathf.Abs(p.x - e.x) > halfBand + 0.01f || p.z < lm.HeadCenter.z) continue;
                var col = cols[i];
                if (SkinLike(col) || (Lum(col) > 0.72f && Sat(col) < 0.2f)) { nCol++; continue; }   // hair colours only (not skin / eye white)
                float sz = SkinZ(p);
                if (sz < -1f) { nNoSkin++; continue; }
                if (p.z < sz + 0.0014f) { nBehind++; continue; }             // brows / lashes / eyeliner lie on the skin
                float wx = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfBand - 0.008f, halfBand + 0.01f, Mathf.Abs(p.x - e.x)));
                float w = wx * k;
                if (w <= 0f) continue;
                float dy = line - p.y;
                float ny = line - dy * 0.28f;
                V[i] = new Vector3(p.x, Mathf.Lerp(p.y, ny, w), p.z + 0.0015f * w);
                moved++;
            }
            log?.AppendLine($"[{def.Id}] fringe lifted over the eyes: {moved} vertices (line {line:F3}; skipped colour {nCol}, no skin {nNoSkin}, on skin {nBehind})");

            // strands still crossing an eye opening are tucked just behind the eye surface (the classic anime "eyes over
            // hair" read): seen from the front the eye is clear, the strand passes behind it
            if (PushBehind.Contains(def.Id))
            {
                int tucked = 0;
                var eyes = new[] { new Vector3(e.x + ex, e.y + cal.EyeDY, 0f), new Vector3(e.x - ex, e.y + cal.EyeDY, 0f) };
                for (int i = 0; i < V.Count; i++)
                {
                    var p = V[i];
                    if (p.z < lm.HeadCenter.z || Mathf.Abs(p.y - e.y) > 0.035f || Mathf.Abs(p.x - e.x) > ex + 0.035f) continue;
                    var col = cols[i];
                    if (SkinLike(col) || (Lum(col) > 0.72f && Sat(col) < 0.2f)) continue;
                    float sz = FaceZ(p);
                    if (sz < -1f || p.z < sz + 0.0003f) continue;
                    float best = 0f;
                    for (int s = 0; s < 2; s++)
                    {
                        float xmm = (s == 0 ? eyes[0].x - p.x : p.x - eyes[1].x) * 1000f, ymm = (p.y - eyes[s].y) * 1000f;
                        // soft window around the opening
                        float outer = def.Id == "P04" ? 18f : 8f, upper = def.Id == "P04" ? 16f : 10f;
                        float wx = Mathf.InverseLerp(cal.EyeOut.x - outer, cal.EyeOut.x - outer + 5f, xmm) * Mathf.InverseLerp(cal.EyeIn.x + 7f, cal.EyeIn.x + 2f, xmm);
                        float wy = Mathf.InverseLerp(cal.EyeBot.y - 8f, cal.EyeBot.y - 3f, ymm) * Mathf.InverseLerp(cal.EyeTop.y + upper, cal.EyeTop.y + upper - 5f, ymm);
                        best = Mathf.Max(best, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(wx)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(wy)));
                    }
                    if (best <= 0f) continue;
                    V[i] = new Vector3(p.x, p.y, Mathf.Lerp(p.z, sz - 0.007f, Mathf.Clamp01(best * 1.6f)));
                    tucked++;
                }
                log?.AppendLine($"[{def.Id}] strands tucked behind the eyes: {tucked} vertices");
            }
        }

        /// <summary>Actors whose fringe mass is fused in front of the eyes (cut away inside an eye volume).</summary>
        static readonly HashSet<string> CutEyes = new HashSet<string> { "P04" };
        public static bool CutsFringe(string id) => CutEyes.Contains(id);

        /// <summary>
        /// Removes the hair triangles inside an eye volume (an ellipse from the cheekbone to above the brow, around each
        /// eye opening) that float in front of the facial skin: dark strand cards and their skin-coloured undersides.
        /// The face surface is the rear-most skin-coloured geometry per 2 mm cell. Eye whites / irises are kept.
        /// </summary>
        public static int CutFringe(CastDef def, GlbRigger.Scan scan, List<Vector3> V, List<Vector2> UV, List<int> T, ref int[] triMat, Texture2D tex, System.Text.StringBuilder log)
        {
            if (!CutEyes.Contains(def.Id) || tex == null) return 0;
            var lm = scan.LM; var cal = GlbFace.Get(def.Id); var e = lm.EyeCenter;
            var eyes = new[] { new Vector3(e.x + cal.EyeDX, e.y + cal.EyeDY, 0f), new Vector3(e.x - cal.EyeDX, e.y + cal.EyeDY, 0f) };
            const float cs = 0.002f;
            var zmin = new Dictionary<(int, int), float>();
            var col = new Color[V.Count]; var has = new bool[V.Count];
            for (int i = 0; i < V.Count; i++)
            {
                var p = V[i];
                if (Mathf.Abs(p.x - e.x) > 0.1f || Mathf.Abs(p.y - e.y) > 0.07f || p.z < lm.HeadCenter.z) continue;
                col[i] = tex.GetPixelBilinear(UV[i].x, UV[i].y); has[i] = true;
                if (!SkinLike(col[i])) continue;
                var key = (Mathf.FloorToInt(p.x / cs), Mathf.FloorToInt(p.y / cs));
                if (!zmin.TryGetValue(key, out float z) || p.z < z) zmin[key] = p.z;
            }
            float FaceZ(Vector3 p)
            {
                int cx = Mathf.FloorToInt(p.x / cs), cy = Mathf.FloorToInt(p.y / cs);
                for (int r = 0; r <= 5; r++)
                {
                    float best = float.MaxValue;
                    for (int dx = -r; dx <= r; dx++) for (int dy = -r; dy <= r; dy++)
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) == r && zmin.TryGetValue((cx + dx, cy + dy), out float z) && z < best) best = z;
                    if (best < float.MaxValue) return best;
                }
                return float.MinValue;
            }
            bool NearOpening(Vector3 p)
            {
                for (int s = 0; s < 2; s++)
                {
                    float xmm = (s == 0 ? eyes[0].x - p.x : p.x - eyes[1].x) * 1000f, ymm = (p.y - eyes[s].y) * 1000f;
                    if (xmm < cal.EyeOut.x - 2f || xmm > cal.EyeIn.x + 2f) continue;
                    GlbFaceMask.EyeTopBot(cal, Mathf.Clamp(xmm, cal.EyeOut.x, cal.EyeIn.x), out float top, out float bot);
                    if (ymm > bot - 1.5f && ymm < top + 2.2f) return true;
                }
                return false;
            }
            bool InVolume(Vector3 p)
            {
                for (int s = 0; s < 2; s++)
                {
                    float xmm = (s == 0 ? eyes[0].x - p.x : p.x - eyes[1].x) * 1000f, ymm = (p.y - eyes[s].y) * 1000f;
                    float x0 = cal.EyeOut.x - 24f, x1 = cal.EyeIn.x + 5f, y0 = cal.EyeBot.y - 9f, y1 = cal.EyeTop.y + 24f;
                    float u = (xmm - (x0 + x1) * 0.5f) / ((x1 - x0) * 0.5f), v = (ymm - (y0 + y1) * 0.5f) / ((y1 - y0) * 0.5f);
                    if (u * u + v * v < 1f) return true;
                }
                return false;
            }
            // face front around each eye: a plane through the front-most skin of the brow / cheek / temple ring
            // (12..24 mm from the iris, the nose side excluded) - the eye, lids and lashes sit behind it, fringe in front
            var planes = new Vector3[2];   // z = a + b * dx + c * dy (dx, dy from the iris centre, metres)
            for (int s = 0; s < 2; s++)
            {
                var sector = new float[16]; var sx = new float[16]; var sy = new float[16];
                for (int q = 0; q < 16; q++) sector[q] = float.MinValue;
                for (int i = 0; i < V.Count; i++)
                {
                    if (!has[i] || !SkinLike(col[i])) continue;
                    var p = V[i];
                    float dx = p.x - eyes[s].x, dy = p.y - eyes[s].y;
                    float xn = (s == 0 ? -dx : dx) * 1000f;       // toward the nose
                    float r = new Vector2(dx, dy).magnitude * 1000f;
                    if (r < 12f || r > 24f || xn > 6f) continue;
                    int q = Mathf.Clamp(Mathf.FloorToInt((Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 0.5f) * 16f), 0, 15);
                    if (p.z > sector[q]) { sector[q] = p.z; sx[q] = dx; sy[q] = dy; }
                }
                // least squares over the filled sectors
                double n = 0, Sx = 0, Sy = 0, Sz = 0, Sxx = 0, Syy = 0, Sxy = 0, Sxz = 0, Syz = 0;
                for (int q = 0; q < 16; q++)
                {
                    if (sector[q] == float.MinValue) continue;
                    double x = sx[q], y = sy[q], z = sector[q];
                    n++; Sx += x; Sy += y; Sz += z; Sxx += x * x; Syy += y * y; Sxy += x * y; Sxz += x * z; Syz += y * z;
                }
                if (n < 4) { planes[s] = new Vector3(float.MaxValue, 0, 0); continue; }
                // solve [n Sx Sy; Sx Sxx Sxy; Sy Sxy Syy] [a b c] = [Sz Sxz Syz]
                var m = new double[3, 4] { { n, Sx, Sy, Sz }, { Sx, Sxx, Sxy, Sxz }, { Sy, Sxy, Syy, Syz } };
                for (int col0 = 0; col0 < 3; col0++)
                {
                    int piv = col0; for (int r2 = col0 + 1; r2 < 3; r2++) if (System.Math.Abs(m[r2, col0]) > System.Math.Abs(m[piv, col0])) piv = r2;
                    for (int k2 = 0; k2 < 4; k2++) { var tmp = m[col0, k2]; m[col0, k2] = m[piv, k2]; m[piv, k2] = tmp; }
                    if (System.Math.Abs(m[col0, col0]) < 1e-12) continue;
                    for (int r2 = 0; r2 < 3; r2++)
                    {
                        if (r2 == col0) continue;
                        double f2 = m[r2, col0] / m[col0, col0];
                        for (int k2 = 0; k2 < 4; k2++) m[r2, k2] -= f2 * m[col0, k2];
                    }
                }
                planes[s] = new Vector3((float)(m[0, 3] / m[0, 0]), (float)(m[1, 3] / m[1, 1]), (float)(m[2, 3] / m[2, 2]));
                log?.AppendLine($"[{def.Id}] eye {s} face plane z = {planes[s].x:F4} + {planes[s].y:F3} dx + {planes[s].z:F3} dy ({n} sectors)");
            }
            float PlaneGap(Vector3 p)
            {
                float best = float.MinValue;
                for (int s = 0; s < 2; s++)
                {
                    if (planes[s].x == float.MaxValue) continue;
                    float dx = p.x - eyes[s].x, dy = p.y - eyes[s].y;
                    if (Mathf.Abs(dx) > 0.04f) continue;
                    best = Mathf.Max(best, p.z - (planes[s].x + planes[s].y * dx + planes[s].z * dy));
                }
                return best;
            }
            var nt = new List<int>(T.Count); var nm = new List<int>(triMat.Length);
            var cutV = new bool[V.Count];
            int cut = 0;
            for (int t = 0; t < T.Count / 3; t++)
            {
                int a = T[t * 3], b = T[t * 3 + 1], c = T[t * 3 + 2];
                bool drop = false;
                if (has[a] && has[b] && has[c])
                {
                    var ctr = (V[a] + V[b] + V[c]) / 3f;
                    if (InVolume(ctr))
                    {
                        var cc = (col[a] + col[b] + col[c]) / 3f;
                        float fz = FaceZ(ctr);
                        bool eyeWhite = Lum(cc) > 0.72f && Sat(cc) < 0.2f;
                        bool iris = !SkinLike(cc) && Lum(cc) > 0.3f && Hue(cc) > 0.5f && Hue(cc) < 0.75f && Sat(cc) > 0.12f;
                        if (PlaneGap(ctr) > 0.0015f && !eyeWhite && !iris) drop = true;
                    }
                }
                if (drop) { cut++; cutV[a] = cutV[b] = cutV[c] = true; continue; }
                nt.Add(a); nt.Add(b); nt.Add(c); nm.Add(triMat[t]);
            }
            T.Clear(); T.AddRange(nt); triMat = nm.ToArray();
            log?.AppendLine($"[{def.Id}] fringe cut in front of the eyes: {cut} triangles");
            if (V.Count > 200000)   // debug view on the full-resolution source (bake + QA)
            {
                var used = new bool[V.Count]; foreach (int i in T) used[i] = true;
                GlbDebug.SplatRegion("splat_cut_" + def.Id, V, i =>
                {
                    if (cutV[i]) return new Color(1f, 0.15f, 0.1f);
                    if (!used[i]) return new Color(0.08f, 0.08f, 0.1f);
                    var p = V[i]; bool inV = has[i] && InVolume(p);
                    var cc = has[i] ? col[i] : Color.gray;
                    if (inV) return PlaneGap(p) > 0.0015f ? new Color(1f, 0.9f, 0.1f) : new Color(0.2f, 0.9f, 0.3f) * (0.5f + 0.5f * Lum(cc));
                    return cc * 0.8f;
                }, new Vector3(e.x, e.y + 0.01f, lm.HeadCenter.z), 0.08f, Vector3.right, Vector3.up, Vector3.forward);
            }
            return cut;
        }

        /// <summary>Round tube mesh along a polyline (6 sides, capped ends).</summary>
        public static (Vector3[] v, Vector3[] n, int[] t) Tube(List<Vector3> pts, float r, bool closed)
        {
            const int sides = 6;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            int m = pts.Count;
            Vector3 prevU = Vector3.zero;
            for (int i = 0; i < m; i++)
            {
                Vector3 d = (i + 1 < m ? pts[i + 1] : pts[i]) - (i > 0 ? pts[i - 1] : pts[i]);
                if (d.sqrMagnitude < 1e-12f) d = Vector3.right;
                d.Normalize();
                Vector3 u = i == 0 ? Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.forward).normalized : Vector3.ProjectOnPlane(prevU, d).normalized;
                if (u.sqrMagnitude < 0.5f) u = Vector3.Cross(d, Vector3.up).normalized;
                prevU = u;
                Vector3 w = Vector3.Cross(d, u);
                for (int k = 0; k < sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    Vector3 dir = u * Mathf.Cos(a) + w * Mathf.Sin(a);
                    v.Add(pts[i] + dir * r); n.Add(dir);
                }
            }
            for (int i = 0; i + 1 < m; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = i * sides + k, b = i * sides + (k + 1) % sides, c = (i + 1) * sides + k, dd = (i + 1) * sides + (k + 1) % sides;
                    t.Add(a); t.Add(c); t.Add(b);
                    t.Add(b); t.Add(c); t.Add(dd);
                }
            // caps
            for (int end = 0; end < 2; end++)
            {
                int i = end == 0 ? 0 : m - 1;
                Vector3 d = end == 0 ? (pts[0] - pts[Mathf.Min(1, m - 1)]) : (pts[m - 1] - pts[Mathf.Max(0, m - 2)]);
                d = d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.forward;
                int ci = v.Count; v.Add(pts[i] + d * r * 0.5f); n.Add(d);
                for (int k = 0; k < sides; k++)
                {
                    int a = i * sides + k, b = i * sides + (k + 1) % sides;
                    if (end == 0) { t.Add(ci); t.Add(b); t.Add(a); } else { t.Add(ci); t.Add(a); t.Add(b); }
                }
            }
            return (v.ToArray(), n.ToArray(), t.ToArray());
        }
    }
}
