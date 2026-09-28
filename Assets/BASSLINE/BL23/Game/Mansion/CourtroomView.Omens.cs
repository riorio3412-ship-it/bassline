using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Things in and around the well that show in the frames the trial lens actually uses (and the bell and the hourglass below):
    /// · the thread: above the chandelier's links its chain is a black thread rising into the black Sun. No haze greys it, so
    ///   it vanishes into the pupil instead of drawing a pale line across it.
    /// · the one at the window: at the verdict something colossal rises behind the Judge's Lancet, a hooded silhouette pressed
    ///   against the grisaille behind Yusti's head. The cold key light that comes through the lancet halves as it does.
    ///   It is a darkening of the glass, never a figure you can see clearly: no face, no eyes, no edge.
    /// · the lamp on the stair: a single hooded figure carries a lantern up the stair to nowhere as the trial deepens, a warm
    ///   speck crawling up the far wall in the wides. At the verdict it reaches the broken last tread and the light goes out.
    /// </summary>
    public sealed partial class CourtroomView
    {
        // ------------------------------------------------------------------ the thread
        void ChandelierThread()
        {
            if (_chandPivot == null) return;
            var pv = _chandPivot.position;
            var mb = new MeshBuilder(); mb.Push(Matrix4x4.Translate(-pv));
            mb.Set(S.Glow, Color.white, new Vector4(0.0008f, 3f, 1f, 9f));   // explicit near-black, kept out of the air
            mb.Rod(_c + Vector3.up * 29.9f, _c + Vector3.up * 70f, 0.09f, 6, false);
            mb.Rod(_c + Vector3.up * 70f, _c + Vector3.up * 118.5f, 0.13f, 6, false);   // a little heavier high up: never thinner than two pixels
            Put(mb, "ChandelierThread", _chandPivot, _mVoid, true, pv);
        }

        // ------------------------------------------------------------------ the one at the window
        Transform _presence; Material _mPresence, _mPresenceRim; float _presence01;
        const float PresenceDrop = 48f;
        /// <summary>0..1: how far the thing behind the lancet has risen (probe / diagnostics).</summary>
        public float Presence => _presence01;

        /// <summary>How dark the glass is at (x across the lancet, y height) with the thing fully risen: a pointed hood leaning a
        /// little toward the court's right, broad shoulders below it that fill the lower glass, all softened by the frosted glass.</summary>
        const float PresHx = 0.7f, PresHy = 14.5f;   // the hood's crown reaches ~21 m: in the judge's close-up it rises just behind and above his head
        /// <summary>Signed distance-like field of the silhouette (m, positive inside).</summary>
        static float PresenceDist(float x, float y)
        {
            const float hx = PresHx, hy = PresHy;
            float up = Mathf.Clamp01((y - hy) / 6.3f);
            float rx = 3.7f * Mathf.Lerp(1f, 0.28f, up * up), ry = y > hy ? 6.3f : 4.6f;
            float ex = (x - hx - Mathf.Max(0f, y - hy) * 0.14f) / rx, ey = (y - hy) / ry;
            float dHood = (1f - Mathf.Sqrt(ex * ex + ey * ey)) * Mathf.Min(rx, ry);
            const float sx = 0.3f, sy = -3.0f, srx = 9.8f, sry = 12.4f;
            float ux = (x - sx) / srx, uy = (y - sy) / sry;
            float dBody = y < sy ? srx - Mathf.Abs(x - sx) : (1f - Mathf.Sqrt(ux * ux + uy * uy)) * sry * 0.8f;
            return Mathf.Max(dHood, dBody);
        }
        static float PresenceShade(float x, float y)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.6f, 0.9f, PresenceDist(x, y)));
            // not a flat cut-out: the mass is a little less dense where the hood's fold would catch the glass's light
            float fold = Mathf.Exp(-((x - PresHx - 1.6f) * (x - PresHx - 1.6f)) / 1.2f - ((y - PresHy + 0.5f) * (y - PresHy + 0.5f)) / 10f);
            return k * (0.93f - 0.18f * fold);
        }
        /// <summary>The glass just outside the outline is a little brighter (the light it blocks bends round its edge): the shape reads
        /// as a thing standing behind the window, not as grime on it.</summary>
        static float PresenceRim(float x, float y)
        {
            if (y < 3f) return 0f;
            float d = PresenceDist(x, y) + 0.55f;
            return 0.05f * Mathf.Exp(-(d / 0.5f) * (d / 0.5f)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 8f, y));
        }

        void LancetPresence()
        {
            _mPresence = new Material(MansionMats.Get(S.GlowAdd)) { name = "CourtPresence" };
            _mPresence.SetTexture("_MainTex", Texture2D.whiteTexture);
            _mPresence.SetFloat("_SrcBlend", (float)BlendMode.Zero); _mPresence.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcColor);
            _mPresence.SetColor("_Color", new Color(0.985f, 0.985f, 0.985f, 1f));
            _mPresence.renderQueue = 2990;   // after the glass, before every additive layer (the shafts in front of the glass stay bright)
            _mPresence.SetFloat(PInt, 0f);
            _presence = Pivot("CourtPresence", _c - Vector3.up * PresenceDrop);
            // a sheet just in front of the glass (r 39.46: behind the tracery, the orders and the wall), much taller than the window
            // so that while it rises the window only ever shows its outline climbing
            var q = new VQ { Mode = 1f }; var qr = new VQ { Mode = 1f }; var rimCol = new Color(0.8f, 0.84f, 0.92f);
            var ys = new System.Collections.Generic.List<float> { -100f, -70f, -40f, -20f };
            for (float y = -12f; y <= 24.01f; y += 0.5f) ys.Add(y);
            ys.Add(32f); ys.Add(40f);
            const int NX = 36; const float X0 = -9f, X1 = 9f;
            var w = Color.white; var inward = -_jd; var pv = _c;   // built at rest height 0 relative to a pivot at the court centre
            for (int j = 0; j + 1 < ys.Count; j++)
                for (int i = 0; i < NX; i++)
                {
                    float x0 = Mathf.Lerp(X0, X1, i / (float)NX), x1 = Mathf.Lerp(X0, X1, (i + 1f) / NX), y0 = ys[j], y1 = ys[j + 1];
                    Vector3 a = WP(0f, x0, 39.46f, y0) - pv, b = WP(0f, x0, 39.46f, y1) - pv, c = WP(0f, x1, 39.46f, y1) - pv, d = WP(0f, x1, 39.46f, y0) - pv;
                    float v00 = PresenceShade(x0, y0) / 0.985f, v01 = PresenceShade(x0, y1) / 0.985f, v11 = PresenceShade(x1, y1) / 0.985f, v10 = PresenceShade(x1, y0) / 0.985f;
                    // never exactly 0: the glow shader reads a value of 0 as "unset" (full strength), which would cut solid triangles
                    if (v00 >= 1e-3f || v01 >= 1e-3f || v11 >= 1e-3f || v10 >= 1e-3f)
                        q.Quad(a, b, c, d, w, w, w, w, Mathf.Max(v00, 1e-4f), Mathf.Max(v01, 1e-4f), Mathf.Max(v11, 1e-4f), Mathf.Max(v10, 1e-4f), inward);
                    float r00 = PresenceRim(x0, y0), r01 = PresenceRim(x0, y1), r11 = PresenceRim(x1, y1), r10 = PresenceRim(x1, y0);
                    if (r00 >= 1e-3f || r01 >= 1e-3f || r11 >= 1e-3f || r10 >= 1e-3f)
                        qr.Quad(a + inward * 0.01f, b + inward * 0.01f, c + inward * 0.01f, d + inward * 0.01f, rimCol, rimCol, rimCol, rimCol, Mathf.Max(r00, 1e-5f), Mathf.Max(r01, 1e-5f), Mathf.Max(r11, 1e-5f), Mathf.Max(r10, 1e-5f), inward);
                }
            PutMesh(q.ToMesh("CourtPresence"), "CourtPresence", _presence, _mPresence);
            _mPresenceRim = new Material(MansionMats.Get(S.GlowAdd)) { name = "CourtPresenceRim" };
            _mPresenceRim.SetTexture("_MainTex", Texture2D.whiteTexture); _mPresenceRim.SetColor("_Color", Color.white);
            _mPresenceRim.renderQueue = 2991; _mPresenceRim.SetFloat(PInt, 0f);
            PutMesh(qr.ToMesh("CourtPresenceRim"), "CourtPresenceRim", _presence, _mPresenceRim);
        }

        /// <summary>Per frame: the rise begins a moment after the verdict is spoken and is complete within six seconds.</summary>
        void UpdatePresence(float t)
        {
            if (_presence == null) return;
            float target = _verdictT0 > 0f ? S01((t - _verdictT0 - 0.8f) / 5.2f) : 0f;
            _presence01 = _verdictT0 > 0f ? target : Mathf.MoveTowards(_presence01, 0f, 0.02f);
            float rise = _presence01 * _presence01 * (3f - 2f * _presence01);
            // once risen it does not hold still like stone: it breathes, very slowly
            float breath = rise * 0.4f * Mathf.Sin(t * Mathf.PI * 2f / 7.5f);
            _presence.position = _c - Vector3.up * (PresenceDrop * (1f - rise) - breath);
            if (_mPresence != null) _mPresence.SetFloat(PInt, Mathf.Clamp01(_presence01 * 12f));
            if (_mPresenceRim != null) _mPresenceRim.SetFloat(PInt, Mathf.Clamp01(_presence01 * 12f));
        }

        // ------------------------------------------------------------------ the lamp on the stair
        Transform _walker; Material _mWalkLamp, _mWalkHalo; float _walkU = 0.03f, _walkLamp = 1f;
        const float StairTh0 = 70f, StairTh1 = 230f, StairH0 = 5.4f, StairH1 = 65f, StairRm = 35.6f, StairLen = 116f;
        Vector3 StairFeet(float u) => P(Mathf.Lerp(StairTh0, StairTh1, u), StairRm, Mathf.Lerp(StairH0, StairH1, u) + 0.03f);
        Quaternion StairFacing(float u) => Quaternion.LookRotation(Dir(Mathf.Lerp(StairTh0, StairTh1, u) + 90f), Vector3.up);
        static readonly Vector3 LampLocal = new Vector3(0.3f, 0.93f, 0.36f);

        void StairWalker()
        {
            var home = StairFeet(0.5f);
            _walker = Pivot("CourtStairWalker", home);
            const float sc = 1.3f;
            var body = new MeshBuilder();
            body.Set(S.Glow, Color.white, new Vector4(0.004f, 2f, 1f, 9f));   // black, rim-lit in the bake like the other watchers
            body.Push(Matrix4x4.Scale(Vector3.one * sc));
            body.Ellipsoid(new Vector3(0f, 0.62f, 0f), new Vector3(0.24f, 0.6f, 0.19f), 8, 5);
            body.Push(new Vector3(0f, 1.2f, 0.05f), Quaternion.Euler(14f, 0f, 0f), Vector3.one); body.Ellipsoid(Vector3.zero, new Vector3(0.15f, 0.19f, 0.16f), 8, 5); body.Pop();
            // the arm held out with the lantern, and its short iron bail
            body.Push(new Vector3(0.2f, 0.98f, 0.18f), Quaternion.Euler(55f, 0f, -10f), Vector3.one); body.Ellipsoid(Vector3.zero, new Vector3(0.06f, 0.2f, 0.06f), 6, 4); body.Pop();
            body.Rod(LampLocal + new Vector3(0f, 0.14f, 0f), LampLocal + new Vector3(-0.04f, 0.26f, -0.1f), 0.012f, 4, false);
            body.Pop();
            Put(body, "CourtStairWalker", _walker, _mVoid, true, home);
            // the lantern: a small warm box (its own material, so it can go out) and a soft glow around it
            _mWalkLamp = new Material(_mVoid) { name = "CourtStairLamp" };
            var lamp = new MeshBuilder(); Lum(lamp, 0.95f, DoorWarm);
            lamp.Push(Matrix4x4.Scale(Vector3.one * sc)); lamp.Box(LampLocal, new Vector3(0.12f, 0.17f, 0.12f)); lamp.Pop();
            Put(lamp, "CourtStairLamp", _walker, _mWalkLamp, true, home);
            _mWalkHalo = new Material(MansionMats.Get(S.Halo)) { name = "CourtStairLampGlow" };
            var halo = new MeshBuilder(); halo.Set(S.Halo, new Color(1f, 0.72f, 0.45f), new Vector4(0.42f, 0.12f, 0f, 9f));
            MansionView.HaloQuad(halo, LampLocal * sc, 1.5f);
            Put(halo, "CourtStairLampGlow", _walker, _mWalkHalo, false, home);
            _walker.SetPositionAndRotation(StairFeet(_walkU), StairFacing(_walkU));
        }

        /// <summary>Per frame: the climb follows the trial's depth at a slow walk (it catches up in stretches, then waits); after the
        /// verdict it goes on to the broken last tread, where the lantern goes out.</summary>
        void UpdateWalker(Camera cam, float dt)
        {
            if (_walker == null) return;
            bool verdict = _verdictT0 > 0f;
            float target = verdict ? 1f : Mathf.Clamp(0.03f + 0.93f * _depth, 0f, 0.96f);
            _walkU = Mathf.MoveTowards(_walkU, target, dt * (verdict ? 1.4f : 0.8f) / StairLen);
            float u = Mathf.Min(_walkU, 0.997f);
            _walker.SetPositionAndRotation(StairFeet(u), StairFacing(u));
            bool dark = verdict && _walkU >= 0.999f;
            _walkLamp = Mathf.MoveTowards(_walkLamp, dark ? 0f : 1f, dt / (dark ? 1.6f : 3f));
            if (_mWalkLamp != null) _mWalkLamp.SetFloat(PInt, Mathf.Lerp(0.02f, 1f, _walkLamp));
            if (_mWalkHalo != null)
            {
                // the glow is additive (no fog reaches it): carry it through the same air the stone is seen through
                float rho = FogStripped ? AerialDensity : RenderSettings.fog ? RenderSettings.fogDensity : 0f;
                float d = cam != null ? Vector3.Distance(cam.transform.position, _walker.position) : 50f;
                float T = Mathf.Exp(-(rho * d) * (rho * d));
                _mWalkHalo.SetFloat(PInt, _walkLamp * Mathf.Pow(T, 0.45f));
            }
        }

        // ------------------------------------------------------------------ the bell
        // A bronze bell eight metres across its mouth hangs from an iron beam slung between two of the second-order piers, above the
        // lift tower. It never rings on its own: when the court tolls (the room's great stares come with the toll) it swings, slowly,
        // at a pendulum's true pace, and settles again.
        Transform _bell; float _bellAmp, _bellAt = -99f, _bellT, _bellTh;
        const float BellPeriod = 5.2f;

        void Bell()
        {
            // two bays round from the lift, so it never stacks on the lift tower's head as seen from the floor
            float g = Mathf.Repeat(Mathf.Round((_liftTh + 60f) / 30f) * 30f, 360f); if (Mathf.Abs(Mathf.DeltaAngle(g, 0f)) < 75f) g = Mathf.Repeat(Mathf.Round((_liftTh - 60f) / 30f) * 30f, 360f);
            _bellTh = g;
            float rm = PierR * Mathf.Cos(15f * Mathf.Deg2Rad), hb = 67f;
            var pivot = P(g, rm, hb - 0.8f);
            _bell = Pivot("CourtBell", pivot);
            var mb = new MeshBuilder(); mb.Push(Matrix4x4.Translate(-pivot));
            // the beam from pier to pier, its two iron straps and the yoke
            var a = P(g - 15f, PierR, hb); var b = P(g + 15f, PierR, hb);
            var beam = new MeshBuilder();
            Stone(beam, AIronDark, 1f, IronHue);
            beam.Push(Matrix4x4.TRS((a + b) * 0.5f, Quaternion.LookRotation(Dir(g)), Vector3.one)); beam.Box(Vector3.zero, new Vector3((a - b).magnitude, 1.5f, 1.3f)); beam.Pop();
            foreach (var s in new[] { -1f, 1f })
            {
                // iron corbels where the beam leaves each pier's face
                var from = s < 0 ? a : b; var to = s < 0 ? b : a; var e = from + (to - from).normalized * 1.85f;
                beam.Push(Matrix4x4.TRS(e, Quaternion.LookRotation(Dir(g)), Vector3.one)); beam.Box(new Vector3(0f, -1.25f, 0f), new Vector3(1.1f, 1.0f, 1.5f)); beam.Pop();
            }
            Put(beam, "CourtBellBeam", _wellRoot, _mVoid, true, Vector3.zero);
            // the bell itself (hanging from the pivot): headstock, crown, waist, sound bow, a thick lip; hollow, so from the floor
            // you look up into its dark mouth
            Stone(mb, 0.2f, 1f, new Color(0.86f, 0.9f, 1f));   // old bronze: its shoulder and sound bow catch the Sun, the rest stays a silhouette
            mb.Push(Matrix4x4.TRS(pivot, Quaternion.LookRotation(Dir(g)), Vector3.one));
            mb.Box(new Vector3(0f, -0.35f, 0f), new Vector3(5.2f, 1.0f, 1.3f));                     // headstock
            foreach (var s in new[] { -1.6f, 1.6f }) mb.Box(new Vector3(s, -1.2f, 0f), new Vector3(0.5f, 1.3f, 0.9f));   // canons
            var outer = new[] { new Vector2(4.05f, -10.6f), new Vector2(4.1f, -10.2f), new Vector2(3.85f, -9.6f), new Vector2(3.1f, -8.4f), new Vector2(2.55f, -6.6f), new Vector2(2.35f, -4.6f), new Vector2(2.3f, -3.2f), new Vector2(2.05f, -2.3f), new Vector2(1.3f, -1.85f), new Vector2(0.001f, -1.8f) };
            mb.Lathe(outer, 28);
            // inside (facing in) and the lip's underside
            var inner = new[] { new Vector2(0.001f, -2.3f), new Vector2(1.7f, -2.5f), new Vector2(1.95f, -3.4f), new Vector2(2.0f, -4.8f), new Vector2(2.2f, -6.8f), new Vector2(2.75f, -8.6f), new Vector2(3.45f, -9.8f), new Vector2(3.7f, -10.6f) };
            Stone(mb, AIronDark * 0.35f, 1f, IronHue);
            mb.Lathe(inner, 28);
            Stone(mb, 0.16f, 1f, IronHue);
            mb.Lathe(new[] { new Vector2(3.7f, -10.6f), new Vector2(4.05f, -10.6f) }, 28);
            // two raised bands on the waist, where the inscription would be
            Stone(mb, 0.26f, 1f, new Color(0.86f, 0.9f, 1f));
            foreach (var y in new[] { -3.0f, -8.0f }) { float r = y > -5f ? 2.33f : 3.2f; mb.Push(new Vector3(0f, y, 0f), 0); mb.Torus(Vector3.zero, r, 0.09f, 36, 4); mb.Pop(); }
            mb.Pop();
            Put(mb, "CourtBell", _bell, _mVoid, true, pivot);
        }

        /// <summary>The bell's waist (a look-up target), or the Sun if there is no bell.</summary>
        public Vector3 BellPoint => _bell != null ? _bell.position - Vector3.up * 5.5f : Zenith;
        /// <summary>A framing of the bell from the far side of the clock floor (past the chandelier's links).</summary>
        public bool BellShot(out Vector3 pos, out Vector3 look, out float fov)
        {
            pos = _c - Dir(_bellTh) * 4f + Dir(_bellTh + 90f) * 1.8f + Vector3.up * 1.6f; look = BellPoint; fov = 34f;
            return _bell != null;
        }

        void BellToll(float t)
        {
            if (_bell == null || t - _bellAt < 20f) return;
            if (_bellAmp <= 0f) _bellT = 0f;   // from rest, the swing starts at the bottom of its arc
            _bellAt = t; _bellAmp = Mathf.Min(_bellAmp + 4.5f, 7f);
        }

        void UpdateBell(float dt)
        {
            if (_bell == null) return;
            if (_bellAmp <= 0f) { _bell.localRotation = Quaternion.identity; return; }
            _bellAmp = Mathf.MoveTowards(_bellAmp, 0f, dt * Mathf.Max(0.06f, _bellAmp * 0.085f));
            _bellT += dt;
            float ang = _bellAmp * Mathf.Sin(_bellT * Mathf.PI * 2f / BellPeriod);
            _bell.localRotation = Quaternion.AngleAxis(ang, Dir(_bellTh + 90f));
        }

        // ------------------------------------------------------------------ the court's hourglass
        // The trial's rules speak of "the court's hourglass" turning over (a streak of true answers earns one; a guttered candle
        // spends one). It stands on Yusti's bench: brass and black oak, pale sand running. Whenever the trial's hourglass count
        // changes, it turns over by itself.
        Transform _hg, _hgTop, _hgBot, _hgStream; float _hgRem = 1f, _hgFlipT0 = -1f; int _hgLast = -1;
        const float HgScale = 1.3f, HgDrain = 240f, HgFlip = 1.4f;

        void Hourglass()
        {
            if (_view == null || _rv == null) return;
            var benchC = _c + _jd * (_rad - 1.3f); var fwd = -_jd; var lx = Vector3.Cross(Vector3.up, fwd);
            var centre = benchC + lx * -0.8f + fwd * 0.28f + Vector3.up * (1.9f + 0.045f + 0.28f * HgScale);
            _hg = new GameObject("CourtHourglass").transform; _hg.SetParent(transform, false); _hg.SetPositionAndRotation(centre, Quaternion.LookRotation(fwd));
            var S3 = Matrix4x4.Scale(Vector3.one * HgScale);
            // frame: two black-oak plates, three brass posts
            var fr = new MeshBuilder(); fr.Push(S3);
            fr.Set(S.WoodDark, new Color(0.16f, 0.1f, 0.07f));
            fr.Cyl(new Vector3(0f, -0.28f, 0f), 0.125f, 0.028f, 20, true); fr.Cyl(new Vector3(0f, 0.252f, 0f), 0.125f, 0.028f, 20, true);
            fr.Set(S.Brass, new Color(0.5f, 0.38f, 0.2f));
            for (int k = 0; k < 3; k++) { float a = k / 3f * Mathf.PI * 2f + 0.5f; var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.105f; fr.Rod(o + Vector3.up * -0.252f, o + Vector3.up * 0.252f, 0.011f, 8, false); }
            fr.Pop();
            _view.Emit(_rv, fr, "CourtHourglassFrame", _hg, ShadowCastingMode.On);
            var gl = new MeshBuilder(); gl.Push(S3); gl.Set(S.Glass, Color.white);
            gl.Lathe(new[] { new Vector2(0.028f, -0.252f), new Vector2(0.07f, -0.21f), new Vector2(0.088f, -0.14f), new Vector2(0.07f, -0.06f), new Vector2(0.012f, -0.012f), new Vector2(0.006f, 0f), new Vector2(0.012f, 0.012f), new Vector2(0.07f, 0.06f), new Vector2(0.088f, 0.14f), new Vector2(0.07f, 0.21f), new Vector2(0.028f, 0.252f) }, 18);
            gl.Pop();
            _view.Emit(_rv, gl, "CourtHourglassGlass", _hg, ShadowCastingMode.Off);
            var sand = new Color(0.66f, 0.54f, 0.38f);
            // the sand above: a cone standing on its point at the neck (it shrinks toward the neck as it runs)
            _hgTop = new GameObject("Sand").transform; _hgTop.SetParent(_hg, false); _hgTop.localPosition = Vector3.up * (0.012f * HgScale);
            var st = new MeshBuilder(); st.Push(S3); st.Set(S.Plaster, sand);
            st.Lathe(new[] { new Vector2(0.001f, 0f), new Vector2(0.068f, 0.095f) }, 16); st.Disc(new Vector3(0f, 0.095f, 0f), 0.068f, 16, true); st.Pop();
            _view.Emit(_rv, st, "CourtHourglassSandTop", _hgTop, ShadowCastingMode.Off);
            // the sand below: the filled foot of the bulb and a heap growing on it
            _hgBot = new GameObject("SandBelow").transform; _hgBot.SetParent(_hg, false); _hgBot.localPosition = Vector3.up * (-0.215f * HgScale);
            var sb = new MeshBuilder(); sb.Push(S3); sb.Set(S.Plaster, sand);
            sb.Lathe(new[] { new Vector2(0.072f, 0f), new Vector2(0.001f, 0.085f) }, 16); sb.Lathe(new[] { new Vector2(0.03f, -0.035f), new Vector2(0.072f, 0f) }, 16); sb.Pop();
            _view.Emit(_rv, sb, "CourtHourglassSandBelow", _hgBot, ShadowCastingMode.Off);
            // the thread of falling sand
            _hgStream = new GameObject("SandStream").transform; _hgStream.SetParent(_hg, false); _hgStream.localPosition = Vector3.zero;
            var ss = new MeshBuilder(); ss.Push(S3); ss.Set(S.Plaster, sand * 1.1f); ss.Rod(Vector3.zero, Vector3.up * -0.2f, 0.0025f, 5, false); ss.Pop();
            _view.Emit(_rv, ss, "CourtHourglassStream", _hgStream, ShadowCastingMode.Off);
            ApplyHourglass();
        }

        void ApplyHourglass()
        {
            if (_hg == null) return;
            float up = Mathf.Pow(Mathf.Clamp01(_hgRem), 1f / 3f), down = Mathf.Pow(Mathf.Clamp01(1f - _hgRem), 1f / 3f);
            _hgTop.localScale = Vector3.one * Mathf.Max(0.001f, up);
            _hgBot.localScale = Vector3.one * Mathf.Max(0.001f, down);
            float s = _hgRem > 0.002f && _hgFlipT0 < 0f ? 1f : 0.001f;
            _hgStream.localScale = new Vector3(s, 1f, s);
        }

        /// <summary>Per frame: the sand runs; when the trial's hourglass count changes, the glass turns over (1.4 s) and runs again.</summary>
        void UpdateHourglass(float t, float dt)
        {
            if (_hg == null) return;
            int now = -1;
            try { var tr = Session.I?.S?.Trial; now = tr != null ? tr.Hourglass : -1; } catch (System.Exception) { }
            if (now >= 0 && _hgLast >= 0 && now != _hgLast && _hgFlipT0 < 0f) _hgFlipT0 = t;
            _hgLast = now;
            if (_hgFlipT0 >= 0f)
            {
                float u = (t - _hgFlipT0) / HgFlip;
                if (u >= 1f) { _hgFlipT0 = -1f; _hg.localRotation = Quaternion.identity; _hg.rotation = Quaternion.LookRotation(-_jd); _hgRem = 1f - _hgRem; }
                else _hg.rotation = Quaternion.LookRotation(-_jd) * Quaternion.AngleAxis(180f * S01(u), Vector3.right);
            }
            else _hgRem = Mathf.Max(0f, _hgRem - dt / HgDrain);
            ApplyHourglass();
        }

        /// <summary>A framing of the hourglass on the bench (probe): from in front of the bench, a little above the bench top.</summary>
        public bool HourglassShot(out Vector3 pos, out Vector3 look, out float fov)
        {
            pos = look = Vector3.zero; fov = 0f; if (_hg == null) return false;
            look = _hg.position; pos = look - _jd * 1.5f + _rt * 1.3f + Vector3.up * 0.5f; fov = 30f; return true;   // beside the front podium, not over it
        }

        /// <summary>A new trial: the court's hourglass stands full.</summary>
        void ResetHourglass() { _hgRem = 1f; _hgFlipT0 = -1f; _hgLast = -1; if (_hg != null) { _hg.rotation = Quaternion.LookRotation(-_jd); ApplyHourglass(); } }

        /// <summary>Probe framings of the lamp on the stair: from the court floor (a long lens across the well) or close, from the air.</summary>
        public bool WalkerShot(bool close, out Vector3 pos, out Vector3 look, out float fov)
        {
            pos = look = Vector3.zero; fov = 0f; if (_walker == null) return false;
            var wp = _walker.position; float th = Mathf.Lerp(StairTh0, StairTh1, _walkU);
            look = wp + Vector3.up * 1.1f;
            if (close) { pos = P(th - 14f, 29.5f, wp.y - _c.y + 2.5f); fov = 34f; }
            else { float gap = Mathf.Round(th / 30f) * 30f; pos = P(gap + 4f, 7.6f, 4.8f); fov = 12f; }   // over the gallery (beside its candle tree), through the pier gap nearest the lamp
            return true;
        }

        void ResetOmens()
        {
            _walkU = 0.03f; _walkLamp = 1f; _presence01 = 0f; ResetHourglass();
            if (_presence != null) _presence.position = _c - Vector3.up * PresenceDrop;
            if (_mPresence != null) _mPresence.SetFloat(PInt, 0f);
            if (_mPresenceRim != null) _mPresenceRim.SetFloat(PInt, 0f);
        }
    }
}
