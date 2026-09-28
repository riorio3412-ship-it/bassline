using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>Flattened tapered lock along a polyline (anime hair clump). Cross-section is squashed along the direction from the head center.</summary>
    public sealed class SLock : Sdf
    {
        readonly Vector3[] p; readonly float[] r; readonly Vector3 center; readonly float flat;
        public IList<Vector3> Points => p; public IList<float> Radii => r; public Vector3 Center => center; public float Flat => flat;
        public SLock(IList<Vector3> pts, IList<float> radii, Vector3 headCenter, float flatten)
        {
            p = pts.ToArray(); r = radii.ToArray(); center = headCenter; flat = Mathf.Clamp(flatten, 0.2f, 1f);
            Vector3 mn = p[0], mx = p[0];
            float rm = 0; foreach (var x in r) rm = Mathf.Max(rm, x);
            foreach (var q in p) { mn = Vector3.Min(mn, q); mx = Vector3.Max(mx, q); }
            mn -= Vector3.one * rm; mx += Vector3.one * rm;
            B = new Bounds((mn + mx) * 0.5f, mx - mn);
        }
        public override float D(Vector3 q)
        {
            float best = 1e9f;
            for (int i = 0; i + 1 < p.Length; i++)
            {
                Vector3 a = p[i], b = p[i + 1], ab = b - a;
                float l2 = ab.sqrMagnitude;
                float t = l2 > 1e-12f ? Mathf.Clamp01(Vector3.Dot(q - a, ab) / l2) : 0f;
                Vector3 c = a + ab * t;
                Vector3 v = q - c;
                Vector3 radial = (c - center).normalized;
                float vr = Vector3.Dot(v, radial);
                Vector3 vt = v - radial * vr;
                float d = Mathf.Sqrt(vt.sqrMagnitude + (vr / flat) * (vr / flat)) * flat;
                float rad = Mathf.Lerp(r[i], r[i + 1], t);
                best = Mathf.Min(best, d - rad);
            }
            return best;
        }
    }

    public sealed partial class ProcBuilder
    {
        Vector3 cc;     // cranium center
        Vector3 cr;     // cranium radii
        Part hairPart;
        int mHair;
        Color hairA, hairB;
        readonly List<Sdf> hairLocks = new List<Sdf>();
        System.Random rng;

        Vector3 Surf(float azDeg, float elDeg, float off)
        {
            float a = azDeg * Mathf.Deg2Rad, e = elDeg * Mathf.Deg2Rad;
            Vector3 u = new Vector3(Mathf.Cos(e) * Mathf.Sin(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Cos(a));
            Vector3 onE = new Vector3(u.x * cr.x, u.y * cr.y, u.z * cr.z);
            float len = onE.magnitude;
            return cc + onE * ((len + off) / len);
        }

        float Rnd(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>Lock that follows the scalp from (az0,el0) to (az1,el1) then continues freely.</summary>
        Sdf Strand(float az0, float el0, float az1, float el1, float off0, float off1, float rRoot, float rTip, IList<Vector3> ext = null, float flat = 0.55f, int n = 8)
        {
            var pts = new List<Vector3>();
            var rs = new List<float>();
            int total = n + (ext?.Count ?? 0);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                pts.Add(Surf(Mathf.Lerp(az0, az1, t), Mathf.Lerp(el0, el1, t), Mathf.Lerp(off0, off1, t)));
            }
            if (ext != null) foreach (var e in ext) pts.Add(pts[pts.Count - 1] + e);
            for (int i = 0; i < pts.Count; i++)
            {
                float t = i / (float)(pts.Count - 1);
                rs.Add(Mathf.Lerp(rRoot, rTip, Mathf.Pow(t, 1.6f)));
            }
            return new SLock(pts, rs, cc, flat);
        }

        Sdf Cap(float t, float frontEl, float sideEl, float backEl, float k = 0.01f)
        {
            var shell = Sdf.Offset(Sdf.Ellipsoid(cc, cr), t);
            Vector3 c = cc, r = cr;
            var mask = Sdf.Func(p =>
            {
                Vector3 q = p - c;
                q = new Vector3(q.x / r.x, q.y / r.y, q.z / r.z);
                float el = Mathf.Asin(Mathf.Clamp(q.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                float az = Mathf.Abs(Mathf.Atan2(q.x, q.z) * Mathf.Rad2Deg);
                float elMin = az < 60f ? Mathf.Lerp(frontEl, sideEl, Mathf.SmoothStep(0, 1, (az - 25f) / 35f)) : Mathf.Lerp(sideEl, backEl, Mathf.SmoothStep(0, 1, (az - 90f) / 60f));
                return (elMin - el) * Mathf.Deg2Rad * 0.1f;
            }, shell.B);
            return Sdf.Inter(shell, mask, k);
        }

        List<Vector3> Fall(Vector3 start, float toY, float outward, float azDeg, int n, float sway = 0f, float curlIn = 0f, float wave = 0f)
        {
            var l = new List<Vector3>();
            float a = azDeg * Mathf.Deg2Rad;
            Vector3 radial = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            Vector3 side = new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a));
            float dy = (toY - start.y) / n;
            Vector3 prev = Vector3.zero;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                float o = outward * Mathf.Sin(t * Mathf.PI * 0.5f) - curlIn * Mathf.Pow(t, 3f);
                Vector3 target = radial * o + side * (sway * t + wave * Mathf.Sin(t * Mathf.PI * 3f)) + Vector3.up * dy * i;
                l.Add(target - prev);
                prev = target;
            }
            return l;
        }

        void BuildHair()
        {
            rng = new System.Random(def.Id.GetHashCode());
            cc = hc + V(0, 0.07f * hh, -0.035f * hh);
            cr = V(0.385f, 0.43f, 0.44f) * hh;
            hairA = Hex(L.HairColor, new Color(0.1f, 0.08f, 0.08f));
            hairB = string.IsNullOrEmpty(L.HairColor2) ? Color.Lerp(hairA, Color.white, 0.12f) : Hex(L.HairColor2);
            if (L.Hair == HairStyle.None) return;
            float lum = hairA.grayscale;
            mHair = M.Mat(new MatSpec
            {
                Name = "Hair", Color = Color.white, HairRing = lum < 0.2f ? 0.55f : 0.35f, Rim = 0.35f,
                Shade = lum < 0.15f ? new Color(0.7f, 0.66f, 0.85f) : new Color(0.72f, 0.62f, 0.72f),
                Shade2 = lum < 0.15f ? new Color(0.5f, 0.47f, 0.66f) : new Color(0.52f, 0.44f, 0.55f),
                Cavity = 0.4f,
                Gloss = L.Hair == HairStyle.SlickedBack ? 0.35f : 0f, GlossThreshold = 0.97f
            });
            hairPart = new Part { Name = "Hair", Res = 0.0024f, Skin = SkinRule.RigidTo(Bi(HBone.Head)), AOScale = 0.7f, Keep = 0.09f, Tolerance = 0.0008f };
            float topY = P.HeadTop.y, chinY = P.ChinPos.y;
            Func<Vector3, Color> grad = p =>
            {
                float t = Mathf.Clamp01((topY - p.y) / (hh * 1.2f));
                return Color.Lerp(hairA, hairB, Mathf.SmoothStep(0.15f, 1f, t)).linear;
            };
            switch (L.Hair)
            {
                case HairStyle.Bob: HairBob(false); break;
                case HairStyle.BobBangs: HairBob(true); break;
                case HairStyle.SlickedBack: HairSlick(); break;
                case HairStyle.SidePart55: HairSidePart(); break;
                case HairStyle.Dreadlocks: HairDreads(); break;
                case HairStyle.ShaggyMid: HairShaggy(); break;
                case HairStyle.WavyChin: HairWavy(); break;
                case HairStyle.Cropped: HairCropped(); break;
                case HairStyle.BunMessy: HairBun(); break;
                case HairStyle.LongRibbon: HairLong(false); break;
                case HairStyle.HimeLong: HairLong(true); break;
                case HairStyle.ShortRedStreak: HairShortStreak(); break;
                case HairStyle.SideBraid: HairBraid(); break;
                case HairStyle.TwinTails: HairTwin(); break;
                case HairStyle.Buzz: HairBuzz(); break;
                default: HairMessy(); break;
            }
            // the scalp shell under the clumps is darker so the gaps between clumps read as depth
            if (hairLocks.Count > 0) hairPart.Add(Sdf.Union(0.009f, hairLocks), mHair, 1, UseClumps ? (Func<Vector3, Color>)(p => { var c = grad(p); return new Color(c.r * 0.62f, c.g * 0.6f, c.b * 0.66f, 1f); }) : grad);
            BuildClumps(grad);
            M.Parts.Add(hairPart);
        }

        void Lock(Sdf s) { if (UseClumps && s is SLock sl) clumps.Add(sl); else hairLocks.Add(s); }

        // Standard bangs: n locks from the crown fanning over the forehead down to 'endEl'.
        void Bangs(int n, float azSpread, float endEl, float sweep, float rRoot = 0.018f, float rootEl = 72f, float jitter = 4f, float outTip = 0.006f)
        {
            for (int i = 0; i < n; i++)
            {
                float u = n == 1 ? 0.5f : i / (float)(n - 1);
                float az1 = Mathf.Lerp(-azSpread, azSpread, u) + Rnd(-jitter, jitter) * 0.5f;
                float az0 = az1 * 0.4f - sweep * 0.6f;
                float e1 = endEl + Rnd(-jitter, jitter) + Mathf.Abs(u - 0.5f) * 6f;
                var tip = new List<Vector3> { Vector3.down * 0.012f + Dir(az1) * outTip + Side(az1) * sweep * 0.0004f };
                Lock(Strand(az0, rootEl, az1 + sweep * 0.25f, e1, 0.012f, 0.02f, rRoot * Rnd(0.9f, 1.15f), 0.0015f, tip, 0.5f));
            }
        }

        Vector3 Dir(float azDeg) { float a = azDeg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); }
        Vector3 Side(float azDeg) { float a = azDeg * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a)); }

        void HairBob(bool fullFringe)
        {
            hairLocks.Add(Cap(0.014f, 40f, -10f, -45f));
            float chinY = P.ChinPos.y + (fullFringe ? 0.0f : 0.012f);
            // outer bob shell made of overlapping locks
            for (float az = -168f; az <= 168f; az += 13f)
            {
                if (Mathf.Abs(az) < 58f) continue;
                Vector3 s = Surf(az, -18f, 0.024f);
                var ext = Fall(s, chinY + Rnd(-0.008f, 0.01f), 0.004f, az, 5, Rnd(-0.004f, 0.004f), 0.02f);
                Lock(Strand(az * 0.7f, 70f, az, -18f, 0.014f, 0.024f, 0.028f, 0.006f, ext, 0.6f));
            }
            // sidelocks framing the face
            for (int s = 0; s < 2; s++)
            {
                float az = SX(s) * 62f;
                Vector3 st = Surf(az, 5f, 0.02f);
                var ext = Fall(st, chinY - 0.01f, 0.004f, az, 5, 0f, 0.01f);
                Lock(Strand(SX(s) * 40f, 65f, az, 5f, 0.012f, 0.02f, 0.024f, 0.004f, ext, 0.55f));
            }
            if (fullFringe)
            {
                // straight blunt bangs
                for (int i = 0; i < 11; i++)
                {
                    float az = Mathf.Lerp(-52f, 52f, i / 10f);
                    Lock(Strand(az * 0.3f, 74f, az, -4f + Mathf.Abs(az) * 0.08f, 0.012f, 0.019f, 0.02f, 0.006f, new List<Vector3> { Vector3.down * 0.006f }, 0.5f));
                }
            }
            else
            {
                // side-swept bangs, one longer lock touching the brow
                Bangs(8, 48f, 11f, 22f, 0.019f);
                Lock(Strand(-10f, 75f, 22f, 6f, 0.012f, 0.022f, 0.02f, 0.002f, new List<Vector3> { V(0.004f, -0.01f, 0.004f) }, 0.5f));
            }
        }

        void HairSlick()
        {
            hairLocks.Add(Cap(0.012f, 50f, 6f, -40f));
            // combed-back grooves: meridians from the front hairline over the crown to the nape
            for (float beta = -52f; beta <= 52f; beta += 8.5f)
            {
                var pts = new List<Vector3>();
                var rs = new List<float>();
                for (int i = 0; i <= 12; i++)
                {
                    float th = Mathf.Lerp(38f + Mathf.Abs(beta) * 0.25f, 158f, i / 12f) * Mathf.Deg2Rad;
                    Vector3 d = Quaternion.AngleAxis(beta * (1f - 0.35f * i / 12f), Vector3.forward) * new Vector3(0, Mathf.Sin(th), Mathf.Cos(th));
                    float lift = 0.016f + 0.012f * Mathf.Exp(-Mathf.Pow((i - 2.5f) / 2.2f, 2f)) * (1f - Mathf.Abs(beta) / 70f);
                    pts.Add(OnHead(d, lift));
                    rs.Add(Mathf.Lerp(0.013f, 0.008f, i / 12f));
                }
                Lock(new SLock(pts, rs, cc, 0.45f));
            }
            // sides combed back above the ears
            for (int s = 0; s < 2; s++)
                for (float el = 10f; el <= 40f; el += 10f)
                {
                    var pts = new List<Vector3>();
                    for (int i = 0; i <= 8; i++) pts.Add(Surf(SX(s) * Mathf.Lerp(70f, 165f, i / 8f), Mathf.Lerp(el, el - 25f, i / 8f), 0.011f));
                    Lock(new SLock(pts, pts.Select((p, i) => Mathf.Lerp(0.012f, 0.007f, i / 8f)).ToList(), cc, 0.45f));
                }
            // one loose strand falling on the forehead
            Lock(Strand(10f, 52f, 20f, 30f, 0.024f, 0.03f, 0.0045f, 0.0012f, new List<Vector3> { V(0.006f, -0.018f, 0.012f) }, 0.8f));
        }

        Vector3 OnHead(Vector3 dir, float off)
        {
            Vector3 onE = new Vector3(dir.x * cr.x, dir.y * cr.y, dir.z * cr.z);
            float len = onE.magnitude;
            return cc + onE * ((len + off) / len);
        }

        void HairSidePart()
        {
            hairLocks.Add(Cap(0.013f, 45f, -12f, -40f));
            // 5:5 curtain bangs
            for (int s = 0; s < 2; s++)
                for (int i = 0; i < 5; i++)
                {
                    float az1 = SX(s) * (26f + i * 9f);
                    float e1 = Mathf.Lerp(14f, -16f, i / 4f);
                    Lock(Strand(SX(s) * 3f, 80f, az1, e1, 0.013f, 0.02f, 0.02f, 0.003f, new List<Vector3> { Vector3.down * 0.012f + Dir(az1) * 0.004f }, 0.5f));
                }
            for (float az = -165f; az <= 165f; az += 16f)
            {
                if (Mathf.Abs(az) < 70f) continue;
                Vector3 s = Surf(az, -30f, 0.018f);
                var ext = Fall(s, P.ChinPos.y + 0.03f + (Mathf.Abs(az) > 130 ? -0.02f : 0.02f), 0.006f, az, 3, 0f, 0.01f);
                Lock(Strand(az * 0.6f, 70f, az, -30f, 0.013f, 0.018f, 0.024f, 0.004f, ext, 0.55f));
            }
        }

        void HairDreads()
        {
            hairLocks.Add(Cap(0.012f, 44f, -8f, -38f));
            float endY = shY + 0.02f;
            for (float el = 76f; el > -30f; el -= 14f)
                for (float az = -180f; az < 180f; az += 360f / Mathf.Max(6f, 22f * Mathf.Cos(el * Mathf.Deg2Rad)))
                {
                    float a = az + Rnd(-5f, 5f);
                    if (Mathf.Abs(Mathf.DeltaAngle(a, 0)) < 55f && el < 45f) continue; // forehead / face stays clear
                    float sgn = Mathf.Sign(Mathf.DeltaAngle(0, a) + 0.001f);
                    float absA = Mathf.Abs(Mathf.DeltaAngle(0, a));
                    // swept away from the face: front roots travel to the sides first
                    float at = absA < 78f ? sgn * (78f + absA * 0.35f) : a;
                    float elEnd = Mathf.Min(el, absA < 100f ? 0f : el);
                    var pts = new List<Vector3>();
                    for (int i = 0; i <= 4; i++) pts.Add(Surf(Mathf.Lerp(a, at, i / 4f), Mathf.Lerp(el, elEnd, i / 4f), 0.013f));
                    Vector3 start = pts[pts.Count - 1];
                    Vector3 outDir = start - cc; outDir.y = 0; outDir = outDir.normalized;
                    float front = Vector3.Dot(outDir, Vector3.forward);
                    float y1 = endY + Rnd(-0.05f, 0.03f) + (front > 0.2f ? 0.05f : 0f);
                    Vector3 cur = start;
                    int n = 6;
                    for (int i = 1; i <= n; i++)
                    {
                        float t = i / (float)n;
                        Vector3 nxt = new Vector3(start.x, Mathf.Lerp(start.y, y1, t), start.z) + outDir * (0.02f * Mathf.Sin(t * Mathf.PI * 0.5f)) + new Vector3(Rnd(-0.006f, 0.006f), 0, Rnd(-0.006f, 0.006f));
                        Vector3 q = nxt - cc; Vector3 qn = new Vector3(q.x / (cr.x + 0.028f), q.y / (cr.y + 0.028f), q.z / (cr.z + 0.028f));
                        if (qn.magnitude < 1f) nxt = cc + Vector3.Scale(qn.normalized, cr + Vector3.one * 0.028f);
                        // never in front of the face
                        if (nxt.y < cc.y + 0.02f && nxt.z > cc.z + 0.02f && Mathf.Abs(nxt.x) < cr.x + 0.02f) nxt.x = Mathf.Sign(nxt.x + 0.0001f) * (cr.x + 0.02f);
                        pts.Add(nxt); cur = nxt;
                    }
                    var rs = pts.Select((p, i) => i < 5 ? 0.0105f : 0.0105f - 0.0025f * (i - 4) / (pts.Count - 4)).ToList();
                    var dl = new SLock(pts, rs, cc, 1f);
                    Lock(Sdf.Displace(dl, p => Mathf.Sin(p.y * 260f + p.x * 40f) * 0.6f + Mathf.Sin(p.z * 190f - p.y * 70f) * 0.4f, 0.0022f));
                }
        }

        void HairShaggy()
        {
            hairLocks.Add(Cap(0.015f, 38f, -15f, -48f));
            float endY = neckBaseY + 0.02f;
            for (float az = -170f; az <= 170f; az += 12f)
            {
                if (Mathf.Abs(az) < 55f) continue;
                Vector3 s = Surf(az, -22f, 0.022f);
                var ext = Fall(s, endY + Rnd(-0.02f, 0.03f), 0.02f + Rnd(0, 0.01f), az, 4, Rnd(-0.01f, 0.01f), -0.01f);
                Lock(Strand(az * 0.7f, 68f, az, -22f, 0.015f, 0.022f, 0.024f, 0.002f, ext, 0.55f));
            }
            // flipped outer layer
            for (float az = -150f; az <= 150f; az += 22f)
            {
                if (Mathf.Abs(az) < 70f) continue;
                Vector3 s = Surf(az, 10f, 0.03f);
                var ext = Fall(s, P.ChinPos.y + Rnd(-0.01f, 0.02f), 0.035f, az, 3, Rnd(-0.01f, 0.01f), -0.02f);
                Lock(Strand(az * 0.8f, 55f, az, 10f, 0.02f, 0.03f, 0.02f, 0.002f, ext, 0.55f));
            }
            Bangs(9, 50f, -4f, 8f, 0.018f, 70f, 7f, 0.01f);
            for (int s = 0; s < 2; s++)
                Lock(Strand(SX(s) * 45f, 60f, SX(s) * 66f, -5f, 0.013f, 0.02f, 0.02f, 0.002f, Fall(Surf(SX(s) * 66f, -5f, 0.02f), P.ChinPos.y + 0.01f, 0.01f, SX(s) * 66f, 3), 0.55f));
        }

        void HairWavy()
        {
            hairLocks.Add(Cap(0.014f, 40f, -10f, -42f));
            float endY = P.ChinPos.y - 0.005f;
            for (float az = -166f; az <= 166f; az += 14f)
            {
                if (Mathf.Abs(az) < 56f) continue;
                Vector3 s = Surf(az, -12f, 0.024f);
                var ext = Fall(s, endY + Rnd(-0.015f, 0.01f), 0.02f, az, 8, 0f, 0.0f, 0.009f);
                Lock(Strand(az * 0.6f - 12f, 72f, az, -12f, 0.014f, 0.024f, 0.025f, 0.003f, ext, 0.6f));
            }
            // swept bangs with a wave, parted on the left
            for (int i = 0; i < 7; i++)
            {
                float az1 = Mathf.Lerp(-25f, 58f, i / 6f);
                float e1 = Mathf.Lerp(8f, -18f, i / 6f);
                var ext = new List<Vector3> { V(0.006f, -0.012f, 0.004f), V(-0.004f, -0.01f, 0.002f) };
                Lock(Strand(-30f, 78f, az1, e1, 0.013f, 0.024f, 0.021f, 0.002f, ext, 0.55f));
            }
            Lock(Strand(-40f, 60f, -64f, 0f, 0.012f, 0.022f, 0.02f, 0.002f, Fall(Surf(-64f, 0f, 0.022f), endY, 0.01f, -64f, 5, 0f, 0f, 0.008f), 0.55f));
        }

        void HairCropped()
        {
            hairLocks.Add(Cap(0.016f, 46f, -2f, -35f));
            for (int i = 0; i < 26; i++)
            {
                float az = Rnd(-70f, 70f), el = Rnd(40f, 82f);
                Vector3 root = Surf(az, el, 0.012f);
                Vector3 n = (root - cc).normalized;
                Vector3 tip = root + n * Rnd(0.012f, 0.02f) + Vector3.forward * 0.01f + Vector3.up * 0.004f;
                Lock(new SLock(new[] { root, tip }, new[] { 0.011f, 0.0015f }, cc, 0.7f));
            }
            Bangs(6, 34f, 22f, 4f, 0.016f, 64f, 5f, 0.012f);
        }

        void HairBun()
        {
            hairLocks.Add(Cap(0.011f, 42f, -6f, -38f));
            Vector3 bunC = Surf(180f, 52f, 0.05f);
            // combed-up grooves towards the bun
            for (float az = -150f; az <= 150f; az += 18f)
            {
                float el0 = Mathf.Abs(az) < 60f ? 44f : (Mathf.Abs(az) < 120f ? -2f : -32f);
                Lock(Strand(az, el0, Mathf.Lerp(az, 180f * Mathf.Sign(az + 0.01f), 0.8f), 48f, 0.012f, 0.02f, 0.012f, 0.009f, null, 0.45f));
            }
            // bun: lumpy sphere + wraps
            var bun = Sdf.Displace(Sdf.Ellipsoid(bunC, V(0.058f, 0.05f, 0.052f) * (hh / 0.24f)), p => Noise.Fbm(p * 70f) - 0.4f, 0.012f);
            Lock(bun);
            Lock(Sdf.Torus(bunC + V(0, -0.012f, 0), Quaternion.Euler(-35f, 0, 0), 0.045f, 0.012f));
            // flyaways
            for (int i = 0; i < 6; i++)
            {
                Vector3 d = new Vector3(Rnd(-1, 1), Rnd(0.2f, 1f), Rnd(-1f, 0.3f)).normalized;
                Vector3 a = bunC + d * 0.045f;
                Lock(new SLock(new[] { a, a + d * 0.03f + V(Rnd(-0.01f, 0.01f), -0.01f, 0), a + d * 0.05f + V(0, -0.02f, 0) }, new[] { 0.007f, 0.004f, 0.001f }, bunC, 0.6f));
            }
            // loose face-framing strands
            for (int s = 0; s < 2; s++)
                Lock(Strand(SX(s) * 38f, 55f, SX(s) * 62f, -8f, 0.013f, 0.02f, 0.009f, 0.002f, Fall(Surf(SX(s) * 62f, -8f, 0.02f), P.ChinPos.y + 0.01f, 0.008f, SX(s) * 62f, 5, 0f, 0f, 0.006f), 0.6f));
            Bangs(5, 30f, 30f, -8f, 0.015f, 66f, 6f, 0.014f);
            // pencil through the bun (P11 PenEar)
            if (Acc(Accessory.PenEar))
            {
                Vector3 a = bunC + V(-0.09f, 0.035f, 0.02f), b = bunC + V(0.08f, -0.02f, -0.015f);
                PencilProp(a, b);
            }
        }

        void PencilProp(Vector3 a, Vector3 b)
        {
            var part = new Part { Name = "Pencil", Res = 0.0014f, Skin = SkinRule.RigidTo(Bi(HBone.Head)) };
            Vector3 d = (b - a).normalized;
            part.Add(Sdf.Cyl(a + d * 0.012f, b, 0.0042f, 0.0005f), Cloth(new Color(0.98f, 0.78f, 0.15f), name: "PencilBody"), 1);
            part.Add(Sdf.Cone(a + d * 0.012f, a, 0.0042f, 0.0008f), Cloth(new Color(0.9f, 0.75f, 0.55f), name: "PencilWood"), 2);
            part.Add(Sdf.Cyl(b - d * 0.004f, b + d * 0.008f, 0.0044f, 0.001f), Cloth(new Color(0.95f, 0.55f, 0.6f), name: "Eraser"), 2);
            M.Parts.Add(part);
        }

        void HairLong(bool hime)
        {
            hairLocks.Add(Cap(0.014f, 40f, -10f, -45f));
            float endY = hime ? kneeY + 0.04f : waistY - 0.06f;
            // back chain bones for the long sheet
            Vector3 top = Surf(180f, 10f, 0.03f);
            float backZ = BackZ(shY - 0.05f) - 0.03f;
            var chainPts = new List<Vector3> { top };
            int segs = hime ? 4 : 3;
            for (int i = 1; i <= segs; i++)
            {
                float t = i / (float)segs;
                float y = Mathf.Lerp(top.y, endY, t);
                float z = y > hipsY ? Mathf.Min(BackZ(Mathf.Clamp(y, hipsY, shY)) - 0.035f, top.z) : BackZ(hipsY) - 0.04f;
                chainPts.Add(V(0, y, z));
            }
            int[] chain = AddChain("HairBack", "Head", chainPts);
            // sheet made of vertical locks around the back
            var sheet = new List<Sdf>();
            for (float a = 76f; a <= 284f; a += 7f)
            {
                float az = a > 180f ? a - 360f : a;
                float rootEl = 30f;
                Vector3 s = Surf(az, -10f, 0.022f);
                var pts = new List<Vector3>();
                for (int i = 0; i <= 6; i++) pts.Add(Surf(az, 70f - i * 13f, 0.016f + i * 0.001f));
                pts.Add(s);
                // follow the back down, staying behind the body
                float x0 = s.x, z0 = s.z;
                int n = hime ? 10 : 7;
                for (int i = 1; i <= n; i++)
                {
                    float t = i / (float)n;
                    float y = Mathf.Lerp(s.y, endY + Mathf.Abs(Mathf.Sin(az * 7f)) * 0.02f, t);
                    float bz = y > hipsY ? BackZ(Mathf.Clamp(y, hipsY, shY + 0.1f)) : BackZ(hipsY);
                    float spread = Mathf.Clamp01((s.y - y) / 0.2f);
                    float x = x0 * (1f + 0.35f * spread) * (y < shY ? 0.95f : 1f);
                    float wantZ = Mathf.Min(z0, bz - 0.03f) - 0.01f * spread;
                    // side locks drift forward over the shoulders only for the ribbon style
                    pts.Add(V(x, y, Mathf.Lerp(z0, wantZ, spread)));
                }
                var rs = pts.Select((p, i) => i < 8 ? 0.016f : Mathf.Lerp(0.018f, 0.003f, Mathf.Pow((i - 8) / (float)(pts.Count - 8), 2.2f))).ToList();
                sheet.Add(new SLock(pts, rs, cc, 0.5f));
            }
            Part longPart = null;
            if (UseClumps)
            {
                foreach (var sd in sheet)
                {
                    var sl = (SLock)sd;
                    var pts = sl.Points.Select(q => hime ? new Vector3(q.x, Mathf.Max(q.y, endY + 0.005f), q.z) : q).ToList();
                    clumps.Add(new SLock(pts, sl.Radii.Select((r, i) => hime ? Mathf.Max(r, 0.009f) : r).ToList(), sl.Center, sl.Flat));
                }
            }
            else
            {
                var sheetSdf = Sdf.Union(0.006f, sheet);
                if (hime) sheetSdf = KeepAbove(sheetSdf, endY + 0.005f); // blunt straight cut
                longPart = new Part { Name = "HairLong", Res = hime ? 0.0036f : 0.0032f, Keep = 0.08f, AOScale = 0.7f, Tolerance = 0.001f };
                longPart.Add(sheetSdf, mHair, 1, p => Color.Lerp(hairA, hairB, Mathf.Clamp01((P.HeadTop.y - p.y) / 0.9f)).linear);
                M.Parts.Add(longPart);
            }
            // side locks in front of the shoulders
            for (int s = 0; s < 2; s++)
            {
                float az = SX(s) * 68f;
                Vector3 st = Surf(az, -5f, 0.022f);
                float sy = hime ? P.ChinPos.y - 0.005f : J(HBone.Chest).y + 0.02f;
                var ext = new List<Vector3>();
                Vector3 prev = st;
                int n = hime ? 3 : 6;
                for (int i = 1; i <= n; i++)
                {
                    float t = i / (float)n;
                    float y = Mathf.Lerp(st.y, sy, t);
                    Vector3 target = V(st.x + SX(s) * (hime ? 0.004f : 0.035f) * t, y, st.z + (hime ? 0.005f : 0.05f) * t);
                    if (!hime && y < shY + 0.02f) target.z = Mathf.Max(target.z, FrontZ(Mathf.Clamp(y, waistY, shY)) * 0.4f);
                    ext.Add(target - prev); prev = target;
                }
                var lk = Strand(SX(s) * 40f, 62f, az, -5f, 0.013f, 0.022f, hime ? 0.026f : 0.02f, hime ? 0.02f : 0.003f, ext, 0.5f);
                if (hime) lk = KeepAbove(lk, sy);
                Lock(lk);
            }
            if (hime)
            {
                // blunt bangs: flat cut just above the eyes
                float cutY = P.EyeCenter.y + 0.125f * hh;
                var bangs = new List<Sdf>();
                for (int i = 0; i < 13; i++)
                {
                    float az = Mathf.Lerp(-56f, 56f, i / 12f);
                    bangs.Add(Strand(az * 0.3f, 76f, az, -12f, 0.012f, 0.02f, 0.02f, 0.016f, new List<Vector3> { Vector3.down * 0.01f }, 0.5f));
                }
                if (UseClumps) foreach (var b in bangs) { var sl = (SLock)b; clumps.Add(new SLock(sl.Points.Select(q => new Vector3(q.x, Mathf.Max(q.y, cutY), q.z)).ToList(), sl.Radii.Select(r => Mathf.Max(r, 0.008f)).ToList(), sl.Center, sl.Flat)); }
                else Lock(KeepAbove(Sdf.Union(0.004f, bangs), cutY));
            }
            else Bangs(9, 50f, -2f, 4f, 0.019f, 72f, 3f, 0.006f);
            // weights: blend head -> chain by height for the whole hair part
            int head = Bi(HBone.Head);
            float y0 = top.y;
            hairPart.Skin = new SkinRule
            {
                Custom = p =>
                {
                    var l = new List<KeyValuePair<int, float>>();
                    if (p.y > y0 - 0.02f || p.z > cc.z - 0.02f && p.y > shY) { l.Add(new KeyValuePair<int, float>(head, 1f)); return l; }
                    float headW = Mathf.Clamp01((p.y - (y0 - 0.12f)) / 0.1f);
                    l.Add(new KeyValuePair<int, float>(head, headW));
                    for (int i = 0; i < chain.Length; i++)
                    {
                        var bd = M.Bones[chain[i]];
                        float d = CharMesher.SegDist(p, bd.Pos, bd.Tail);
                        l.Add(new KeyValuePair<int, float>(chain[i], (1f - headW) / Mathf.Pow(d + 0.02f, 3f) * 0.0001f));
                    }
                    return l;
                }
            };
            if (longPart != null) longPart.Skin = hairPart.Skin;
        }

        void HairShortStreak()
        {
            hairLocks.Add(Cap(0.014f, 42f, -12f, -42f));
            for (float az = -165f; az <= 165f; az += 15f)
            {
                if (Mathf.Abs(az) < 58f) continue;
                Vector3 s = Surf(az, -20f, 0.02f);
                var ext = Fall(s, P.ChinPos.y + 0.035f + Rnd(-0.01f, 0.01f), 0.014f, az, 3, Rnd(-0.006f, 0.006f), -0.004f);
                Lock(Strand(az * 0.7f, 70f, az, -20f, 0.014f, 0.02f, 0.022f, 0.002f, ext, 0.55f));
            }
            Bangs(8, 46f, 0f, -10f, 0.018f, 72f, 6f, 0.01f);
            // red streak locks on the left side
            Color red = hairB;
            var streak = new List<Sdf>();
            streak.Add(Strand(-20f, 76f, -34f, -4f, 0.016f, 0.024f, 0.017f, 0.002f, new List<Vector3> { V(-0.004f, -0.014f, 0.006f) }, 0.5f));
            streak.Add(Strand(-35f, 70f, -52f, -8f, 0.016f, 0.024f, 0.016f, 0.002f, new List<Vector3> { V(-0.004f, -0.016f, 0.004f) }, 0.5f));
            hairPart.Add(Sdf.Union(0.003f, streak), mHair, 3, p => red.linear);
        }

        void HairBraid()
        {
            hairLocks.Add(Cap(0.014f, 40f, -12f, -44f));
            // hair swept to the left side into a thick braid over the left shoulder
            for (float az = -170f; az <= 170f; az += 15f)
            {
                if (Mathf.Abs(az) < 55f) continue;
                Lock(Strand(az, 60f, Mathf.Lerp(az, az > 0f ? 245f : -115f, 0.6f), -25f, 0.014f, 0.022f, 0.02f, 0.014f, null, 0.5f));
            }
            Vector3 start = Surf(-112f, -30f, 0.03f);
            Vector3 over = V(-P.ShoulderHalfW * 0.55f, shY + 0.03f, 0.03f);
            Vector3 end = V(-P.ShoulderHalfW * 0.45f, J(HBone.Chest).y - 0.06f, FrontZ(J(HBone.Chest).y - 0.06f) + 0.045f);
            var chainPts = new List<Vector3> { start, over, Vector3.Lerp(over, end, 0.5f), end };
            int[] chain = AddChain("HairBraid", "Head", chainPts);
            // braid segments: alternating tilted ellipsoids along a smooth curve
            var braid = new List<Sdf>();
            int nSeg = 16;
            for (int i = 0; i < nSeg; i++)
            {
                float t = i / (float)(nSeg - 1);
                Vector3 p = Bezier(start, over + V(-0.04f, 0.05f, -0.03f), end, t);
                Vector3 p2 = Bezier(start, over + V(-0.04f, 0.05f, -0.03f), end, Mathf.Min(1f, t + 0.05f));
                Vector3 d = (p2 - p).normalized;
                float w = Mathf.Lerp(0.036f, 0.022f, t);
                Quaternion q = Quaternion.LookRotation(d, Vector3.forward) * Quaternion.Euler(0, 0, (i % 2 == 0 ? 35f : -35f));
                braid.Add(Sdf.Ellipsoid(p, V(w * 0.95f, w * 0.55f, w * 1.1f), q));
            }
            Vector3 tie = end + (end - Bezier(start, over, end, 0.95f)).normalized * 0.01f;
            var braidSdf = Sdf.Union(0.004f, braid);
            // tuft below the tie
            var tuft = new SLock(new[] { tie, tie + V(0, -0.03f, 0.005f), tie + V(0.004f, -0.065f, 0.008f) }, new[] { 0.014f, 0.016f, 0.002f }, tie + V(0, 0.1f, 0), 0.7f);
            hairPart.Add(braidSdf, mHair, 1, p => Color.Lerp(hairA, hairB, 0.5f).linear);
            hairPart.Add(tuft, mHair, 1, p => hairB.linear);
            // tie
            var tieP = new Part { Name = "BraidTie", Res = 0.0016f, Skin = SkinRule.RigidTo(chain[2]) };
            tieP.Add(Sdf.Torus(tie, Quaternion.FromToRotation(Vector3.up, (end - over).normalized), 0.012f, 0.005f), Metal(Hex(L.AccColor, new Color(0.8f, 0.8f, 0.85f)), "BraidTie"), 1);
            M.Parts.Add(tieP);
            // side-swept bangs
            Bangs(7, 44f, 14f, -12f, 0.018f, 74f, 5f, 0.008f);
            Lock(Strand(40f, 58f, 64f, -6f, 0.013f, 0.02f, 0.016f, 0.002f, Fall(Surf(64f, -6f, 0.02f), P.ChinPos.y, 0.008f, 64f, 4), 0.55f));
            int head = Bi(HBone.Head);
            hairPart.Skin = new SkinRule
            {
                Custom = p =>
                {
                    var l = new List<KeyValuePair<int, float>>();
                    float dHead = (p - cc).magnitude - cr.y - 0.03f;
                    if (p.y > start.y + 0.01f || dHead < 0.0f && p.y > shY + 0.06f) { l.Add(new KeyValuePair<int, float>(head, 1f)); return l; }
                    l.Add(new KeyValuePair<int, float>(head, 0.05f / Mathf.Pow(Mathf.Max(dHead, 0.005f) + 0.02f, 2f) * 0.01f));
                    foreach (int c in chain)
                    {
                        var bd = M.Bones[c];
                        float d = CharMesher.SegDist(p, bd.Pos, bd.Tail);
                        l.Add(new KeyValuePair<int, float>(c, 0.0001f / Mathf.Pow(d + 0.015f, 3f)));
                    }
                    return l;
                }
            };
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t) => (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c;

        void HairTwin()
        {
            hairLocks.Add(Cap(0.014f, 40f, -12f, -44f));
            // short back layer
            for (float az = -165f; az <= 165f; az += 16f)
            {
                if (Mathf.Abs(az) < 60f) continue;
                Vector3 s = Surf(az, -22f, 0.018f);
                Lock(Strand(az * 0.7f, 68f, az, -22f, 0.014f, 0.018f, 0.022f, 0.004f, Fall(s, neckBaseY + 0.04f, 0.008f, az, 3, 0f, 0.006f), 0.55f));
            }
            // cute rounded bangs + sidelocks
            Bangs(9, 48f, 2f, 0f, 0.02f, 72f, 4f, 0.01f);
            for (int s = 0; s < 2; s++)
                Lock(Strand(SX(s) * 45f, 58f, SX(s) * 66f, -5f, 0.012f, 0.02f, 0.017f, 0.002f, Fall(Surf(SX(s) * 66f, -5f, 0.02f), P.ChinPos.y - 0.02f, 0.006f, SX(s) * 66f, 4, 0f, -0.004f), 0.55f));
            // twin tails with chains
            int head = Bi(HBone.Head);
            var chains = new int[2][];
            var tailSdf = new Sdf[2];
            var tiePos = new Vector3[2];
            for (int s = 0; s < 2; s++)
            {
                Vector3 tie = Surf(SX(s) * 108f, 34f, 0.02f);
                tiePos[s] = tie;
                Vector3 outD = V(SX(s), 0, -0.25f).normalized;
                float endY = hipsY - 0.02f;
                // tails flare out over the capelet / shoulders and hang behind to the hips
                var cpts = new List<Vector3> { tie, tie + outD * 0.08f + V(0, -0.04f, 0), V(tie.x + SX(s) * 0.135f, Mathf.Lerp(tie.y, endY, 0.45f), tie.z - 0.1f), V(tie.x + SX(s) * 0.12f, endY, tie.z - 0.12f) };
                chains[s] = AddChain("HairTail" + (s == 0 ? "L" : "R"), "Head", cpts);
                var locks = new List<Sdf>();
                for (int k = 0; k < 7; k++)
                {
                    float ang = k / 7f * Mathf.PI * 2f;
                    Vector3 off = new Vector3(Mathf.Cos(ang) * 0.012f, Mathf.Sin(ang) * 0.01f, Mathf.Sin(ang) * 0.012f);
                    var pts = new List<Vector3>();
                    for (int i = 0; i <= 8; i++)
                    {
                        float t = i / 8f;
                        Vector3 c = CatmullChain(cpts, t);
                        float spread = Mathf.Sin(Mathf.Clamp01(t * 1.3f) * Mathf.PI) * 2.2f + 0.6f;
                        Vector3 curl = t > 0.7f ? V(-SX(s) * 0.03f * (t - 0.7f) / 0.3f, 0, 0.02f * (t - 0.7f) / 0.3f) : Vector3.zero;
                        pts.Add(c + off * spread + curl + V(Rnd(-0.004f, 0.004f), 0, Rnd(-0.004f, 0.004f)));
                    }
                    var rs = pts.Select((p, i) => Mathf.Lerp(0.021f, 0.003f, Mathf.Pow(i / 8f, 1.6f)) * (i == 0 ? 0.8f : 1f)).ToList();
                    locks.Add(new SLock(pts, rs, CatmullChain(cpts, 0.4f), 0.8f));
                }
                tailSdf[s] = Sdf.Union(0.006f, locks);
                hairPart.Add(tailSdf[s], mHair, 1, p => Color.Lerp(hairA, hairB, Mathf.Clamp01((tie.y - p.y) / 0.5f)).linear);
                // hair tie (scrunchie)
                var tieP = new Part { Name = "TwinTie" + s, Res = 0.0018f, Skin = SkinRule.RigidTo(head) };
                tieP.Add(Sdf.Displace(Sdf.Torus(tie + outD * 0.012f, Quaternion.FromToRotation(Vector3.up, outD), 0.02f, 0.01f), p => Mathf.Sin(Mathf.Atan2(p.y - tie.y, p.z - tie.z) * 10f), 0.002f), Cloth(Hex(L.AccColor, new Color(0.95f, 0.6f, 0.72f)), name: "Scrunchie"), 1);
                M.Parts.Add(tieP);
            }
            hairPart.Skin = new SkinRule
            {
                Custom = p =>
                {
                    var l = new List<KeyValuePair<int, float>>();
                    int s = p.x < 0 ? 0 : 1;
                    float dt = (p - tiePos[s]).magnitude;
                    float dHead = (new Vector3((p.x - cc.x) / cr.x, (p.y - cc.y) / cr.y, (p.z - cc.z) / cr.z).magnitude - 1f) * cr.x;
                    if (dHead < 0.035f && dt > 0.05f || dt < 0.03f) { l.Add(new KeyValuePair<int, float>(head, 1f)); return l; }
                    l.Add(new KeyValuePair<int, float>(head, Mathf.Clamp01(1f - (dt - 0.03f) / 0.05f)));
                    foreach (int c in chains[s])
                    {
                        var bd = M.Bones[c];
                        float d = CharMesher.SegDist(p, bd.Pos, bd.Tail);
                        l.Add(new KeyValuePair<int, float>(c, 0.0001f / Mathf.Pow(d + 0.015f, 3f)));
                    }
                    return l;
                }
            };
        }

        static Vector3 CatmullChain(List<Vector3> pts, float t)
        {
            int n = pts.Count - 1;
            float f = Mathf.Clamp01(t) * n;
            int i = Mathf.Min(n - 1, Mathf.FloorToInt(f));
            float u = f - i;
            Vector3 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(n, i + 2)];
            return 0.5f * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u * u * u);
        }

        void HairBuzz()
        {
            var cap = Cap(0.0055f, 50f, 0f, -36f, 0.006f);
            hairPart.Add(Sdf.Displace(cap, p => Noise.Value(p * 400f) - 0.5f, 0.0006f), mHair, 1, p => hairA.linear);
        }

        void HairMessy()
        {
            hairLocks.Add(Cap(0.015f, 40f, -10f, -42f));
            for (float az = -165f; az <= 165f; az += 14f)
            {
                if (Mathf.Abs(az) < 58f) continue;
                Vector3 s = Surf(az, -18f, 0.02f);
                Lock(Strand(az * 0.7f, 70f, az, -18f, 0.014f, 0.02f, 0.022f, 0.002f, Fall(s, P.ChinPos.y + 0.03f, 0.016f, az, 3, Rnd(-0.01f, 0.01f)), 0.55f));
            }
            Bangs(8, 46f, -6f, 6f, 0.019f, 72f, 7f, 0.01f);
        }
    }
}
