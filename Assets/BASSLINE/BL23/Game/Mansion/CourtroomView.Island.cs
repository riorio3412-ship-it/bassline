using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// The island's edge: the stone parapet behind the ring gallery with iron candle trees on its coping, the judge's breach
    /// (no wall behind the throne: a broken promontory over the drop, two tall iron candelabra, a toppled one on the lip) and
    /// the iron chandelier hanging on a chain that rises out of sight. Everything here is lit geometry near the court.
    /// Frame convention: θ is measured from the judge's direction toward rt = Cross(up, judgeDir); D(θ) = jd·cosθ + rt·sinθ.
    /// </summary>
    public sealed partial class CourtroomView
    {
        Vector3 _c, _jd, _rt, _liftDir; float _rad, _liftTh; ulong _seed;

        // the court's candle flames are upright billboards: seen from high above they would flatten into floating pills, so
        // they fade out as the lens pitches down past ~50° (the candles' light stays)
        Material _mFlame; float _flameK = -1f;
        void CourtFlame(MeshRenderer mr)
        {
            if (mr == null) return;
            if (_mFlame == null) _mFlame = new Material(MansionMats.Get(S.Flame)) { name = "CourtFlame" };
            var sm = mr.sharedMaterials; for (int i = 0; i < sm.Length; i++) if (sm[i] == MansionMats.Get(S.Flame)) sm[i] = _mFlame; mr.sharedMaterials = sm;
        }
        void UpdateFlames()
        {
            if (_mFlame == null || _view == null) return;
            var cam = _view.ViewCamera; if (cam == null) return;
            float pitch = Mathf.Asin(Mathf.Clamp(-cam.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(48f, 70f, pitch));
            if (!Mathf.Approximately(k, _flameK)) { _flameK = k; _mFlame.SetFloat("_Intensity", k); }
        }
        Transform _chandPivot; float _chandAmp = 1f;

        void SetFrame(MansionView v, MansionView.RoomView rv, Vector3 c, float rad, Vector3 jd, Vector3 liftDir)
        {
            _c = c; _rad = rad; _jd = jd; _rt = Vector3.Cross(Vector3.up, jd).normalized; _liftDir = liftDir;
            _liftTh = Mathf.Atan2(Vector3.Dot(liftDir, _rt), Vector3.Dot(liftDir, _jd)) * Mathf.Rad2Deg;
            _seed = v.Layout.Seed;
        }

        Vector3 Dir(float thDeg) { float a = thDeg * Mathf.Deg2Rad; return _jd * Mathf.Cos(a) + _rt * Mathf.Sin(a); }
        /// <summary>World point at angle θ (deg), radius r, height h above the court floor.</summary>
        Vector3 P(float thDeg, float r, float h) => _c + Dir(thDeg) * r + Vector3.up * h;

        /// <summary>Surface of revolution in the court frame over the θ-arc [th0, th1] (profile: (r, h)). Face orientation: see the
        /// Lathe notes (a profile running up faces outward, down faces inward, inward along a level faces up).</summary>
        void LatheArc(MeshBuilder mb, IList<Vector2> prof, float th0, float th1, int seg)
        {
            mb.Push(Matrix4x4.TRS(_c, Quaternion.LookRotation(_jd), Vector3.one));
            mb.Lathe(prof, Mathf.Max(1, seg), false, false, 90f - th1, 90f - th0);
            mb.Pop();
        }
        void LatheFace(MeshBuilder mb, float r0, float h0, float r1, float h1, float th0, float th1, int seg) => LatheArc(mb, new[] { new Vector2(r0, h0), new Vector2(r1, h1) }, th0, th1, seg);
        /// <summary>The same face as LatheFace but as flat quads, so lit materials get world-planar uvs (no stretch along the arc).</summary>
        void ArcQuads(MeshBuilder mb, float r0, float h0, float r1, float h1, float th0, float th1, int seg)
        {
            seg = Mathf.Max(1, seg); float dr = r1 - r0, dh = h1 - h0;
            for (int s = 0; s < seg; s++)
            {
                float t0 = Mathf.Lerp(th0, th1, s / (float)seg), t1 = Mathf.Lerp(th0, th1, (s + 1f) / seg), tm = (t0 + t1) * 0.5f;
                var want = Dir(tm) * dh - Vector3.up * dr;
                mb.QuadAuto(P(t0, r0, h0), P(t0, r1, h1), P(t1, r1, h1), P(t1, r0, h0), want);
            }
        }

        /// <summary>Rim bays (10° each, from the judge): the breach, the solid oak bays beside it and around the lift, and
        /// everywhere else an open arcade onto the well, so a speaker at a podium has the dark depth of the well behind them.</summary>
        bool RimBreach(float thc) => Mathf.Abs(Mathf.DeltaAngle(thc, 0f)) < 20f;
        // (the oak stays solid only beside the breach and right around the lift cage, so a speaker near the lift has the well behind them too)
        bool RimOpen(float thc) => !RimBreach(thc) && Mathf.Abs(Mathf.DeltaAngle(thc, 0f)) > 32f && Mathf.Abs(Mathf.DeltaAngle(thc, _liftTh)) > 13f;

        /// <summary>The island's rim: carved oak wainscot in the solid bays; elsewhere a carved oak screen of paired lancet
        /// openings on colonnettes above a low sill, the gallery resting on it, the void beyond.</summary>
        void Rim(MansionView v, MeshBuilder mb, float panelTop)
        {
            Color gilt = new Color(0.78f, 0.6f, 0.3f), oak = new Color(0.34f, 0.22f, 0.15f), oakDark = new Color(0.2f, 0.13f, 0.09f);
            const float Sill = 0.78f, Spring = 1.95f, Apex = 2.42f, RIn = 9.85f, ROut = 10.05f;
            float rad = _rad; var arch = new List<Vector2>();
            for (int i = 0; i < 36; i++)
            {
                float t0 = i * 10f, t1 = t0 + 10f, tc = t0 + 5f;
                if (RimBreach(tc)) continue;
                var dm = Dir(tc);
                if (!RimOpen(tc))
                {
                    // wainscot: back board, raised field panel, plinth, rail
                    Vector3 p0 = P(t0, rad, 0f), p1 = P(t1, rad, 0f);
                    mb.Set(S.WoodDark, oakDark); mb.QuadAuto(p0, p0 + Vector3.up * panelTop, p1 + Vector3.up * panelTop, p1, -dm);
                    mb.Set(S.WoodDark, oak);
                    var f0 = Vector3.Lerp(p0, p1, 0.1f) - dm * 0.04f; var f1 = Vector3.Lerp(p0, p1, 0.9f) - dm * 0.04f;
                    mb.QuadAuto(f0 + Vector3.up * 0.5f, f0 + Vector3.up * (panelTop - 0.35f), f1 + Vector3.up * (panelTop - 0.35f), f1 + Vector3.up * 0.5f, -dm);
                    mb.Set(S.WoodDark, oakDark); mb.Bar(p0 - dm * 0.06f + Vector3.up * 0.12f, p1 - dm * 0.06f + Vector3.up * 0.12f, 0.12f, 0.24f);
                    mb.Set(S.Gold, gilt); mb.Bar(p0 - dm * 0.07f + Vector3.up * panelTop, p1 - dm * 0.07f + Vector3.up * panelTop, 0.07f, 0.06f);
                    continue;
                }
                // the sill: panelled inside, plain outside, a gilt fillet on its edge
                mb.Set(S.WoodDark, oakDark);
                ArcQuads(mb, RIn, Sill, RIn, 0f, t0, t1, 2);
                ArcQuads(mb, ROut, 0f, ROut, Sill, t0, t1, 2);
                ArcQuads(mb, ROut, Sill, RIn, Sill, t0, t1, 2);
                mb.Set(S.WoodDark, oak);
                for (int k = 0; k < 2; k++)
                {
                    float a0 = t0 + k * 5f + 0.9f, a1 = t0 + k * 5f + 4.1f;
                    mb.QuadAuto(P(a0, RIn - 0.03f, 0.14f), P(a0, RIn - 0.03f, Sill - 0.14f), P(a1, RIn - 0.03f, Sill - 0.14f), P(a1, RIn - 0.03f, 0.14f), -dm);
                }
                mb.Set(S.Gold, gilt); mb.Bar(P(t0, RIn - 0.02f, Sill), P(t1, RIn - 0.02f, Sill), 0.05f, 0.04f);
                // colonnettes on the 5° marks (the last one only where the arcade ends)
                for (int k = 0; k <= 2; k++)
                {
                    if (k == 2 && RimOpen(tc + 10f)) continue;
                    float tk = t0 + k * 5f; var cp = P(tk, (RIn + ROut) * 0.5f, 0f);
                    mb.Set(S.WoodDark, oak);
                    mb.Push(cp, 0); mb.Lathe(new[] { new Vector2(0.12f, Sill), new Vector2(0.12f, Sill + 0.06f), new Vector2(0.085f, Sill + 0.1f), new Vector2(0.066f, Sill + 0.14f), new Vector2(0.066f, Spring - 0.17f), new Vector2(0.09f, Spring - 0.11f), new Vector2(0.12f, Spring - 0.05f), new Vector2(0.12f, Spring) }, 8, false, true); mb.Pop();
                    mb.Set(S.Gold, gilt); mb.Push(cp + Vector3.up * (Spring - 0.14f), 0); mb.Torus(Vector3.zero, 0.075f, 0.014f, 12, 4); mb.Pop();
                }
                // two pointed lancet heads per bay, the spandrels and the band under the gallery
                for (int k = 0; k < 2; k++)
                {
                    float ta = t0 + k * 5f, tb = ta + 5f, tm = ta + 2.5f; float W = RIn * 5f * Mathf.Deg2Rad, a = W * 0.5f - 0.075f;
                    ArchPts(arch, a, Spring, Apex, 5);
                    Vector3 Q(float x, float r, float h) => P(tm + x / r * Mathf.Rad2Deg, r, h);
                    foreach (var (r, outward) in new[] { (RIn, false), (ROut, true) })
                    {
                        var n = outward ? Dir(tm) : -Dir(tm);
                        mb.Set(S.WoodDark, outward ? oakDark : Color.Lerp(oakDark, oak, 0.45f));   // the candles sit close under these: keep the oak deep, not orange
                        mb.QuadAuto(Q(-W / 2, r, Spring), Q(-W / 2, r, panelTop), Q(-a, r, panelTop), Q(-a, r, Spring), n);
                        mb.QuadAuto(Q(a, r, Spring), Q(a, r, panelTop), Q(W / 2, r, panelTop), Q(W / 2, r, Spring), n);
                        for (int j = 0; j < arch.Count - 1; j++)
                            mb.QuadAuto(Q(arch[j].x, r, arch[j].y), Q(arch[j].x, r, panelTop), Q(arch[j + 1].x, r, panelTop), Q(arch[j + 1].x, r, arch[j + 1].y), n);
                    }
                    mb.Set(S.WoodDark, oakDark);
                    for (int j = 0; j < arch.Count - 1; j++)
                    {
                        var p = arch[j]; var q2 = arch[j + 1];
                        var ctr = Q(0f, RIn, Spring); var mid = Q((p.x + q2.x) * 0.5f, RIn, (p.y + q2.y) * 0.5f);
                        mb.QuadAuto(Q(p.x, RIn, p.y), Q(q2.x, RIn, q2.y), Q(q2.x, ROut, q2.y), Q(p.x, ROut, p.y), ctr - mid);
                    }
                }
                mb.Set(S.Gold, gilt); mb.Bar(P(t0, RIn - 0.07f, panelTop), P(t1, RIn - 0.07f, panelTop), 0.07f, 0.06f);
                // outside: a band under the parapet, closed underneath
                mb.Set(S.StoneWall, new Color(0.42f, 0.39f, 0.37f) * 0.45f);
                ArcQuads(mb, 10.35f, 2.45f, 10.35f, 2.8f, t0, t1, 2);
                ArcQuads(mb, ROut, 2.45f, 10.35f, 2.45f, t0, t1, 2);
            }
        }

        static float Hash(float x) { x = Mathf.Sin(x * 12.9898f + 78.233f) * 43758.5453f; return x - Mathf.Floor(x); }
        static float Hash(int i, int s) { uint x = (uint)i * 747796405u + (uint)s * 2891336453u; x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return (x & 0xffffff) / 16777216f; }

        /// <summary>θ-arcs (degrees, judge-relative, within [0, 360)) that stay clear of the given exclusions (centre, half-width).</summary>
        static List<(float a0, float a1)> ArcsClear(params (float c, float hw)[] ex)
        {
            var ok = new bool[360];
            for (int d = 0; d < 360; d++)
            {
                ok[d] = true;
                foreach (var e in ex) if (Mathf.Abs(Mathf.DeltaAngle(d + 0.5f, e.c)) < e.hw) { ok[d] = false; break; }
            }
            var res = new List<(float, float)>();
            int start = -1; for (int i = 0; i < 360; i++) if (!ok[i]) { start = i; break; }
            if (start < 0) { res.Add((0f, 360f)); return res; }
            int run = -1;
            for (int k = 1; k <= 360; k++)
            {
                int i = (start + k) % 360; bool on = ok[i];
                if (on && run < 0) run = start + k;
                if ((!on || k == 360) && run >= 0) { int end = on ? start + k + 1 : start + k; if (end - run >= 3) res.Add((run, end)); run = -1; }
            }
            return res;
        }

        void BuildIsland(MansionView v, MansionView.RoomView rv, Transform root, MeshBuilder mb, MeshBuilder fx)
        {
            var rnd = new System.Random((int)(_seed % 1000003UL) * 17 + 5);
            float R() => (float)rnd.NextDouble();
            Color stone = new Color(0.42f, 0.39f, 0.37f) * 0.55f, coping = new Color(0.42f, 0.39f, 0.37f) * 0.3f, iron = new Color(0.1f, 0.09f, 0.08f), gilt = new Color(0.78f, 0.6f, 0.3f);
            var gallery = ArcsClear((0f, 28f), (_liftTh, 20f));

            // ---- parapet behind the gallery (inner face, top, outer face, coping) with a band of blind roundels outside
            foreach (var (a0, a1) in gallery)
            {
                int seg = Mathf.Max(3, Mathf.CeilToInt((a1 - a0) / 4f));
                mb.Set(S.StoneWall, stone);
                ArcQuads(mb, 9.85f, 4.30f, 9.85f, 3.07f, a0, a1, seg);
                ArcQuads(mb, 10.35f, 2.80f, 10.35f, 4.30f, a0, a1, seg);
                mb.Set(S.StoneWall, coping);
                ArcQuads(mb, 10.45f, 4.30f, 10.45f, 4.42f, a0, a1, seg);
                ArcQuads(mb, 10.45f, 4.42f, 9.80f, 4.42f, a0, a1, seg);
                ArcQuads(mb, 9.80f, 4.42f, 9.80f, 4.30f, a0, a1, seg);
                ArcQuads(mb, 10.35f, 4.30f, 10.45f, 4.30f, a0, a1, seg);
                mb.Set(S.StoneWall, stone * 0.8f);
                for (float th = a0 + 3.75f; th < a1 - 2f; th += 7.5f)
                {
                    mb.Push(Matrix4x4.TRS(P(th, 10.39f, 3.6f), Quaternion.FromToRotation(Vector3.up, Dir(th)), Vector3.one));
                    mb.Torus(Vector3.zero, 0.32f, 0.05f, 12, 4); mb.Pop();
                }
            }

            // ---- the judge's breach: two broken stub piers where the wall stops, a promontory of dark marble over the drop
            mb.Set(S.StoneWall, stone * 0.9f);
            foreach (float s in new[] { -1f, 1f })
            {
                float th = 22f * s; var bp = P(th, 10.32f, 0f); var q = Quaternion.LookRotation(-Dir(th));
                float h = Mathf.Lerp(3.0f, 3.9f, R());
                mb.Push(Matrix4x4.TRS(bp, q, Vector3.one)); mb.Box(new Vector3(0, h / 2, 0), new Vector3(0.6f, h, 0.6f)); mb.Pop();
                for (int k = 0; k < 2; k++)
                {
                    var tq = q * Quaternion.Euler(Mathf.Lerp(8f, 25f, R()) * (R() < 0.5f ? -1 : 1), R() * 60f, Mathf.Lerp(8f, 25f, R()));
                    mb.Push(Matrix4x4.TRS(bp + Vector3.up * (h + 0.12f + k * 0.3f) + Dir(th + 90f) * (R() - 0.5f) * 0.2f, tq, Vector3.one));
                    mb.Box(Vector3.zero, new Vector3(0.5f, 0.35f, 0.4f)); mb.Pop();
                }
            }
            {
                const int N = 14; var lip = new float[N + 1]; var jag = new float[N + 1];
                for (int i = 0; i <= N; i++)
                {
                    float th = Mathf.Lerp(-24f, 24f, i / (float)N);
                    float sm = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(24f, 10f, Mathf.Abs(th)));
                    lip[i] = 10.05f + (1.6f + 1.4f * Hash(th * 0.37f + 1.3f)) * sm;
                    jag[i] = -1.2f + (Hash(th * 1.7f + 4.1f) - 0.5f) * 0.6f;
                }
                for (int i = 0; i < N; i++)
                {
                    float t0 = Mathf.Lerp(-24f, 24f, i / (float)N), t1 = Mathf.Lerp(-24f, 24f, (i + 1) / (float)N);
                    // top (marble), just under the disc so the two never fight
                    mb.Set(S.MarbleDark, Color.white, new Vector4(0.25f, 0.05f, 0.2f, 0));
                    mb.QuadAuto(P(t0, 10.0f, -0.002f), P(t0, lip[i], -0.002f), P(t1, lip[i + 1], -0.002f), P(t1, 10.0f, -0.002f), Vector3.up);
                    // the broken lip face and the underside
                    mb.Set(S.StoneWall, stone * 0.7f);
                    var o0 = P(t0, lip[i], -0.002f); var o1 = P(t1, lip[i + 1], -0.002f);
                    var u0 = P(t0, lip[i] - 0.15f, jag[i]); var u1 = P(t1, lip[i + 1] - 0.15f, jag[i + 1]);
                    mb.QuadAuto(o0, u0, u1, o1, Dir((t0 + t1) * 0.5f));
                    mb.QuadAuto(u0, P(t0, 10.0f, -1.3f), P(t1, 10.0f, -1.3f), u1, Vector3.down);
                }
                // rubble hanging under the lip
                for (int k = 0; k < 5; k++)
                {
                    float th = Mathf.Lerp(-18f, 18f, (k + R() * 0.6f) / 5f); float s = Mathf.Lerp(1.5f, 3f, R());
                    var p = P(th, Mathf.Lerp(10.4f, 11.8f, R()), -1.2f - s * 0.5f - R() * 2.5f);
                    mb.Push(Matrix4x4.TRS(p, Quaternion.Euler(R() * 40f, R() * 360f, R() * 40f), Vector3.one));
                    if (k % 2 == 0) mb.Ellipsoid(Vector3.zero, new Vector3(s * 0.5f, s * 0.6f, s * 0.45f), 7, 5); else mb.Box(Vector3.zero, new Vector3(s * 0.7f, s, s * 0.6f));
                    mb.Pop();
                }
                // a brass candelabrum toppled on the lip, one candle still burning
                {
                    var p = P(9f, 11.1f, 0f); var q = Quaternion.LookRotation(Dir(9f + 70f)) * Quaternion.Euler(0, 0, 88f);
                    mb.Set(S.Brass, gilt * 0.8f);
                    mb.Push(Matrix4x4.TRS(p + Vector3.up * 0.08f, q, Vector3.one));
                    mb.Lathe(new[] { new Vector2(0.14f, 0), new Vector2(0.05f, 0.08f), new Vector2(0.03f, 1.1f), new Vector2(0.07f, 1.16f) }, 8);
                    mb.Pop();
                    var tip = p + Vector3.up * 0.1f + (q * Vector3.up) * 1.1f;
                    var wick = new List<Vector3>();
                    FurnitureFactory.Candle(mb, tip + Vector3.up * 0.02f, 0.14f, 0.02f, -2, tip + Vector3.up * 0.17f, wick);
                    foreach (var w in wick) MansionView.FlameQuad(fx, w, 0.07f, -2);
                }
                // two tall iron candelabra flanking the throne
                var benchC = _c + _jd * (_rad - 1.3f);
                foreach (float s in new[] { -1f, 1f })
                {
                    var b = benchC + _rt * (3.95f * s);   // clear of the bench stairs, inside the wainscot
                    mb.Set(S.Iron, iron);
                    mb.Push(b, 0);
                    mb.Lathe(new[] { new Vector2(0.001f, 0f), new Vector2(0.32f, 0.02f), new Vector2(0.26f, 0.12f), new Vector2(0.06f, 0.3f), new Vector2(0.045f, 5.0f), new Vector2(0.09f, 5.1f), new Vector2(0.06f, 5.2f) }, 10);
                    mb.Pop();
                    var wicks = new List<Vector3>();
                    for (int k = 0; k < 5; k++)
                    {
                        float a = k / 5f * Mathf.PI * 2f + 0.3f; var arm = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.34f;
                        mb.Set(S.Iron, iron);
                        mb.Tube(new List<Vector3> { b + Vector3.up * 5.05f, b + Vector3.up * 5.12f + arm * 0.6f, b + Vector3.up * 5.25f + arm }, 0.02f, 5);
                        mb.Push(b + Vector3.up * 5.25f + arm, 0); mb.Lathe(new[] { new Vector2(0.001f, -0.02f), new Vector2(0.07f, 0f), new Vector2(0.06f, 0.02f) }, 8); mb.Pop();
                        float ch = 0.2f + (k % 3) * 0.05f; var cp = b + Vector3.up * 5.26f + arm;
                        FurnitureFactory.Candle(mb, cp, ch, 0.022f, -2, cp + Vector3.up * (ch + 0.01f), wicks);
                    }
                    foreach (var w in wicks) MansionView.FlameQuad(fx, w, 0.08f, -2);
                }
            }

            // ---- iron candle trees on the coping (pier-gap centres); every other one throws light back over the gallery
            int lit = 0;
            foreach (float th in new[] { 30f, 60f, 90f, 120f, 150f, -30f, -60f, -90f, -120f, -150f })
            {
                var b = P(th, 10.1f, 4.42f); var inward = -Dir(th); var side = Dir(th + 90f);
                mb.Set(S.Iron, iron);
                mb.Push(b, 0); mb.Lathe(new[] { new Vector2(0.001f, 0f), new Vector2(0.14f, 0.01f), new Vector2(0.1f, 0.06f), new Vector2(0.025f, 0.14f), new Vector2(0.02f, 1.2f), new Vector2(0.05f, 1.24f) }, 8); mb.Pop();
                var wicks = new List<Vector3>();
                for (int k = 0; k < 3; k++)
                {
                    float a = (k - 1) * 0.9f; var arm = (inward * Mathf.Cos(a) + side * Mathf.Sin(a)) * 0.25f;
                    var tip = b + Vector3.up * (1.0f + (k == 1 ? 0.22f : 0f)) + arm;
                    mb.Set(S.Iron, iron);
                    mb.Tube(new List<Vector3> { b + Vector3.up * 0.95f, b + Vector3.up * 1.0f + arm * 0.5f, tip }, 0.015f, 5);
                    mb.Push(tip, 0); mb.Lathe(new[] { new Vector2(0.001f, -0.02f), new Vector2(0.06f, 0f), new Vector2(0.05f, 0.02f) }, 8); mb.Pop();
                    float ch = 0.18f + (k * 7 % 3) * 0.03f;
                    FurnitureFactory.Candle(mb, tip, ch, 0.018f, -2, tip + Vector3.up * (ch + 0.01f), wicks);
                }
                foreach (var w in wicks) MansionView.FlameQuad(fx, w, 0.07f, -2);
                if (lit++ % 2 == 0)
                {
                    var l = v.AddLight(rv, b + Vector3.up * 1.5f + inward * 0.3f, Candle, 2.0f, 6.5f, LightType.Point, false, 0f, fire: true);
                    AnimLight(l, lit * 0.77f, Candle, true, true);
                }
            }

            // ---- the iron ring chandelier: same ring over the clock, but its chain rises out of sight (a 21 s pendulum)
            {
                var pivotPos = _c + Vector3.up * 118f;
                _chandPivot = new GameObject("ChandelierPivot").transform; _chandPivot.SetParent(root, false); _chandPivot.position = pivotPos;
                var cm = new MeshBuilder(); var cf = new MeshBuilder();
                cm.Push(Matrix4x4.Translate(-pivotPos)); cf.Push(Matrix4x4.Translate(-pivotPos));
                float cy = 6.4f; var cc = _c + Vector3.up * cy;
                cm.Set(S.Iron, new Color(0.09f, 0.08f, 0.08f));
                cm.Push(cc, 0); cm.Torus(Vector3.zero, 2.4f, 0.05f, 48, 5); cm.Torus(Vector3.up * 0.25f, 1.6f, 0.035f, 36, 4); cm.Pop();
                var crown = _c + Vector3.up * 13.5f;
                for (int k = 0; k < 4; k++) { float a = k / 4f * Mathf.PI * 2 + 0.4f; cm.Rod(cc + new Vector3(Mathf.Cos(a) * 2.4f, 0, Mathf.Sin(a) * 2.4f), crown + new Vector3(Mathf.Cos(a) * 0.4f, 0, Mathf.Sin(a) * 0.4f), 0.012f, 4, false); }
                // the crown is blackened iron, not gilt: seen from the floor against the Sun it must not read as a glowing ring
                cm.Set(S.Iron, new Color(0.11f, 0.09f, 0.07f)); cm.Push(crown, 0); cm.Torus(Vector3.zero, 0.45f, 0.06f, 20, 5); cm.Pop();
                cm.Set(S.Iron, new Color(0.07f, 0.065f, 0.06f));
                int nLinks = Mathf.FloorToInt((30f - 13.5f) / 0.26f);
                for (int k = 0; k < nLinks; k++)
                {
                    var p = crown + Vector3.up * (0.13f + k * 0.26f);
                    var q = Quaternion.Euler(0, k % 2 == 0 ? 0f : 90f, 0) * Quaternion.Euler(90f, 0, 0);
                    cm.Push(Matrix4x4.TRS(p, q, new Vector3(1f, 1f, 1.25f))); cm.Torus(Vector3.zero, 0.16f, 0.035f, 8, 4); cm.Pop();
                }
                // above 30 m the chain is a black thread into the black Sun (built with the well, unlit: ChandelierThread)
                var wicks = new List<Vector3>();
                for (int k = 0; k < 18; k++)
                {
                    float a = k / 18f * Mathf.PI * 2; var p = cc + new Vector3(Mathf.Cos(a) * 2.4f, 0.05f, Mathf.Sin(a) * 2.4f);
                    float h = 0.18f + (k * 7 % 5) * 0.04f;
                    FurnitureFactory.Candle(cm, p, h, 0.02f, -2, p + Vector3.up * (h + 0.01f), wicks);
                }
                // flames are built pivot-relative too: FlameQuad takes world points, so shift them into the pivot's space
                foreach (var w in wicks) MansionView.FlameQuad(cf, w, 0.08f, -2);
                v.Emit(rv, cm, "CourtChandelier", _chandPivot, ShadowCastingMode.On);
                CourtFlame(v.Emit(rv, cf, "CourtChandelierFlames", _chandPivot, ShadowCastingMode.Off));
                var cl = v.AddLight(rv, cc + Vector3.down * 0.3f, Candle, 6f, 13f, LightType.Point, false, 0f, fire: true);
                if (cl?.Light != null) cl.Light.transform.SetParent(_chandPivot, true);
                AnimLight(cl, 0.3f, Candle, true, true);
            }
        }
    }
}
