using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// The court's audience: a ring gallery above the rim (velvet benches, a gilt balustrade, oak corbels) where hooded figures
    /// sit, spaced unevenly, never all alike, the same hooded silhouettes that stand in the galleries of the well far above, so
    /// near and far read as one congregation. No faces and no eyeballs: under each hood only a dark hollow and two dim pinpoints
    /// of light. The heads turn together to whoever is speaking (Watch); the pinpoints narrow and burn brighter when the room
    /// tightens (Stare); they go out one by one in blinks; a few turn to the lens; some, when nobody holds them, lift their heads
    /// to the Sun. Hush(s): for a while they bow and dim, so the far eyes own the moment. Each watcher is one head (hood) and one
    /// pair of pinpoints: two renderers, about forty watchers.
    /// </summary>
    public sealed class CourtroomEyes : MonoBehaviour
    {
        sealed class Eye   // one hooded watcher
        {
            public Transform Head, Glint;
            public Vector3 Fwd;
            public float Speed, Seed, Cone, NextBlink, BlinkStart = -9f, NextSaccade, Size;
            public bool Watcher, Devout, Tremor; public Vector3 Jitter;
        }

        readonly List<Eye> _eyes = new List<Eye>();
        Vector3 _center, _target, _zenith; bool _hasZenith; float _targetAt = -99f, _stare, _stareShown, _stareAt = -99f;
        float _hushUntil = -1f, _hush;
        MansionView _view; MansionView.RoomView _rv;

        /// <summary>How many watchers sit in the gallery.</summary>
        public int Count => _eyes.Count;
        /// <summary>How hard the gallery is staring right now (0..1, eased).</summary>
        public float StareLevel => _stareShown;
        /// <summary>For s seconds the gallery bows and its pinpoints sink to a fifth, no one blinks and the heads barely move:
        /// another layer of eyes (the far watchers) owns the moment.</summary>
        internal void Hush(float s) { _hushUntil = Mathf.Max(_hushUntil, Time.time + Mathf.Max(0f, s)); }

        /// <summary>All heads turn toward a world point (a speaker's head). Holds for a few seconds, then they drift back to what the camera frames.</summary>
        public void Watch(Vector3 worldPos) { _target = worldPos; _targetAt = Time.time; }
        /// <summary>0..1: the pinpoints narrow and burn brighter (contradiction, accusation, vote, verdict). Decays by itself.</summary>
        public void Stare(float intensity) { _stare = Mathf.Clamp01(intensity); _stareAt = Time.time; }
        /// <summary>Everyone blinks at once (a ripple over ~0.3 s).</summary>
        public void Blink() { float t = Time.time; foreach (var e in _eyes) e.BlinkStart = t + Random.Range(0f, 0.3f); }

        static readonly Color GlintCol = new Color(1f, 0.8f, 0.55f);
        static readonly float[] GlintLevel = { 0.38f, 0.85f, 1.6f };
        static Material[] _glintMats; static Mesh _hoodMesh, _glintMesh;

        void LateUpdate()
        {
            if (_eyes.Count == 0 || _rv == null || !_rv.Visible) return;
            float t = Time.time, dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (t - _stareAt > 2.5f) _stare = Mathf.MoveTowards(_stare, 0f, dt * 0.35f);
            _stareShown = Mathf.MoveTowards(_stareShown, _stare, dt * 2.5f);
            bool hushed = t < _hushUntil;
            _hush = Mathf.MoveTowards(_hush, hushed ? 1f : 0f, dt / 0.6f);
            var cam = _view != null && _view.ViewCamera != null ? _view.ViewCamera : Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : _center + Vector3.up * 1.6f;
            bool held = t - _targetAt < 4f;
            Vector3 focus = held ? _target : cam != null ? cam.transform.position + cam.transform.forward * 7f : _center + Vector3.up * 1.4f;
            float speedK = Mathf.Lerp(1f, 0.3f, _hush);
            if (_glintMats != null)
                for (int i = 0; i < _glintMats.Length; i++)
                    if (_glintMats[i] != null) { var c = GlintCol * GlintLevel[i] * (1f + 0.9f * _stareShown) * Mathf.Lerp(1f, 0.22f, _hush); c.a = 1f; _glintMats[i].SetColor(PBase, c); }
            for (int i = 0; i < _eyes.Count; i++)
            {
                var e = _eyes[i];
                if (t >= e.NextSaccade)
                {
                    e.NextSaccade = t + Random.Range(0.8f, 3.6f) / speedK;
                    e.Jitter = Random.insideUnitSphere * 0.4f;
                    if (Random.value < 0.03f) e.Watcher = !e.Watcher && Random.value < 0.5f;
                }
                var look = (e.Watcher ? camPos : e.Devout && !held && _hasZenith ? _zenith : focus) + e.Jitter;
                var dir = look - e.Head.position; if (dir.sqrMagnitude < 1e-4f) continue; dir.Normalize();
                // hushed: the heads sink a little, toward the floor
                if (_hush > 0f) dir = Vector3.Slerp(dir, (e.Fwd + Vector3.down * 0.6f).normalized, _hush * 0.5f);
                float ang = Vector3.Angle(e.Fwd, dir); if (ang > e.Cone) dir = Vector3.Slerp(e.Fwd, dir, e.Cone / ang);
                e.Head.rotation = Quaternion.Slerp(e.Head.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-e.Speed * speedK * dt));
                if (e.Tremor && !hushed) e.Head.rotation *= Quaternion.Euler(Random.Range(-0.8f, 0.8f), Random.Range(-0.8f, 0.8f), 0f);
                if (t >= e.NextBlink) { e.NextBlink = t + Random.Range(2.5f, 9f); if (!hushed) e.BlinkStart = t; }
                float bp = (t - e.BlinkStart) / 0.2f, shut = 0f;
                if (bp >= 0f && bp <= 1f) shut = bp < 0.4f ? bp / 0.4f : 1f - (bp - 0.4f) / 0.6f;
                // the pinpoints are only seen from in front of the hood: turned away they go out rather than show edge-on
                var toCam = camPos - e.Glint.position; float facing = Vector3.Dot(e.Head.forward, toCam.normalized);
                float vis = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.5f, facing));
                float open = (1f - shut) * Mathf.Lerp(1f, 0.7f, _stareShown) * Mathf.Lerp(1f, 0.35f, _hush);
                float flick = 0.8f + 0.3f * Mathf.PerlinNoise(t * 0.12f + e.Seed * 17f, e.Seed * 5f);   // now and then one catches the candlelight
                float s = e.Size * vis * flick;
                e.Glint.localScale = new Vector3(s, s * Mathf.Max(0.02f, open), 1f);
            }
        }
        static readonly int PBase = Shader.PropertyToID("_BaseColor");

        // ================================================================== construction

        /// <param name="judgeDir">direction from the centre toward the judge's bench (kept clear)</param>
        /// <param name="liftDir">direction from the centre toward the lift cage (kept clear)</param>
        internal static CourtroomEyes Build(MansionView v, MansionView.RoomView rv, Transform parent, Vector3 c, float rad, float panelTop, float domeBase, Vector3 judgeDir, Vector3 liftDir, Vector3? zenith = null)
        {
            var go = new GameObject("EyeGallery"); go.transform.SetParent(parent, false);
            var ce = go.AddComponent<CourtroomEyes>(); ce._center = c; ce._view = v; ce._rv = rv;
            if (zenith.HasValue) { ce._zenith = zenith.Value; ce._hasZenith = true; }
            var rnd = new System.Random((int)(v.Layout.Seed % 1000003UL) * 31 + 7);
            float R() => (float)rnd.NextDouble();

            // allowed arc (lathe convention: angle from +X toward +Z, degrees); gaps at the bench and at the lift
            float aJ = Mathf.Atan2(judgeDir.z, judgeDir.x) * Mathf.Rad2Deg, aL = Mathf.Atan2(liftDir.z, liftDir.x) * Mathf.Rad2Deg;
            var allowed = new bool[360];
            for (int d = 0; d < 360; d++) allowed[d] = Mathf.Abs(Mathf.DeltaAngle(d, aJ)) > 28f && (liftDir == Vector3.zero || Mathf.Abs(Mathf.DeltaAngle(d, aL)) > 20f);
            var arcs = Runs(allowed);

            float yB = panelTop + 0.15f, slab = 0.22f, depth = 1.75f, rI = rad - depth;
            Color gilt = new Color(0.78f, 0.6f, 0.3f), oakDark = new Color(0.2f, 0.13f, 0.09f), velvet = new Color(0.34f, 0.05f, 0.08f);
            Color shroud = new Color(0.02f, 0.017f, 0.021f);
            var mb = new MeshBuilder();
            float tier1R = rI + 0.5f, tier2R = rI + 1.2f, step = 0.32f, seatH = 0.42f;
            foreach (var (a0, a1) in arcs)
            {
                int seg = Mathf.Max(4, Mathf.CeilToInt((a1 - a0) / 3f));
                mb.Push(c, 0);
                // slab: top, inner face, underside (separate faces keep hard edges)
                mb.Set(S.MarbleDark, Color.white);
                mb.Lathe(new[] { new Vector2(rad, yB), new Vector2(rI, yB) }, seg, false, false, a0, a1);
                mb.Set(S.WoodDark, oakDark);
                mb.Lathe(new[] { new Vector2(rI, yB), new Vector2(rI, yB - slab) }, seg, false, false, a0, a1);
                mb.Lathe(new[] { new Vector2(rI, yB - slab), new Vector2(rad, yB - slab) }, seg, false, false, a0, a1);
                // gilt fascia bands
                mb.Set(S.Gold, gilt);
                mb.Push(Vector3.up * (yB - 0.04f), 0); mb.Torus(Vector3.zero, rI - 0.012f, 0.025f, seg, 4, a0, a1); mb.Pop();
                mb.Push(Vector3.up * (yB - slab + 0.03f), 0); mb.Torus(Vector3.zero, rI - 0.01f, 0.02f, seg, 4, a0, a1); mb.Pop();
                // raised back tier
                mb.Set(S.WoodDark, oakDark);
                mb.Lathe(new[] { new Vector2(rad, yB + step), new Vector2(tier2R - 0.35f, yB + step) }, seg, false, false, a0, a1);
                mb.Lathe(new[] { new Vector2(tier2R - 0.35f, yB + step), new Vector2(tier2R - 0.35f, yB) }, seg, false, false, a0, a1);
                // velvet benches (seat top + front) with low backs
                foreach (var (br, by) in new[] { (tier1R, yB), (tier2R, yB + step) })
                {
                    mb.Set(S.Velvet, velvet);
                    mb.Lathe(new[] { new Vector2(br + 0.22f, by + seatH), new Vector2(br - 0.22f, by + seatH) }, seg, false, false, a0, a1);
                    mb.Lathe(new[] { new Vector2(br - 0.22f, by + seatH), new Vector2(br - 0.22f, by) }, seg, false, false, a0, a1);
                    mb.Set(S.WoodDark, oakDark);
                    mb.Lathe(new[] { new Vector2(br + 0.26f, by + seatH + 0.5f), new Vector2(br + 0.2f, by + seatH + 0.5f), new Vector2(br + 0.2f, by + seatH) }, seg, false, false, a0, a1);
                }
                // balustrade: rail + balusters
                mb.Set(S.Gold, gilt);
                mb.Push(Vector3.up * (yB + 0.95f), 0); mb.Torus(Vector3.zero, rI + 0.06f, 0.045f, seg, 5, a0, a1); mb.Pop();
                mb.Push(Vector3.up * (yB + 0.08f), 0); mb.Torus(Vector3.zero, rI + 0.06f, 0.035f, seg, 4, a0, a1); mb.Pop();
                mb.Pop();
                mb.Set(S.WoodDark, oakDark);
                float arcLen = (a1 - a0) * Mathf.Deg2Rad * (rI + 0.06f); int nb = Mathf.Max(2, Mathf.FloorToInt(arcLen / 0.28f));
                for (int k = 0; k <= nb; k++)
                {
                    float a = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, k / (float)nb); var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    var bp = c + d * (rI + 0.06f) + Vector3.up * yB;
                    mb.Push(bp, 0); mb.Lathe(new[] { new Vector2(0.035f, 0.08f), new Vector2(0.05f, 0.25f), new Vector2(0.022f, 0.45f), new Vector2(0.045f, 0.7f), new Vector2(0.03f, 0.92f) }, 6); mb.Pop();
                }
                // oak corbels under the slab, one over every tenth-degree colonnette of the rim's arcade (the rim's bays run in
                // 10° steps from the judge: lathe angle = judge angle - θ), so the gallery reads as carried by the arcade
                for (int k = 0; k < 36; k++)
                {
                    float ak = Mathf.Repeat(aJ - k * 10f, 360f); if (ak < a0 + 1.5f) ak += 360f; if (ak < a0 + 1.5f || ak > a1 - 1.5f) continue;
                    float a = Mathf.Deg2Rad * ak; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    var q = Quaternion.LookRotation(-d);
                    // a short bracket tucked under the slab, above the colonnette capitals (it must not hang into the arches)
                    mb.Push(Matrix4x4.TRS(c + d * (rad - 0.28f) + Vector3.up * (yB - slab - 0.13f), q, Vector3.one));
                    mb.BevelBox(new Vector3(0, 0f, -0.02f), new Vector3(0.17f, 0.26f, 0.5f), 0.03f);
                    mb.BevelBox(new Vector3(0, -0.17f, 0.12f), new Vector3(0.14f, 0.1f, 0.22f), 0.02f);
                    mb.Pop();
                }
            }

            // ---- the watchers: fewer than the seats, spaced unevenly; a few stand at the very back
            EnsureAssets();
            var holder = new GameObject("Watchers").transform; holder.SetParent(go.transform, false);
            var look0 = c + Vector3.up * 1.3f;
            foreach (var (a0, a1) in arcs)
                foreach (var (br, by, fill, stand) in new[] { (tier1R, yB, 0.42f, false), (tier2R, yB + step, 0.5f, false), (rad - 0.2f, yB + step, 0.1f, true) })
                {
                    float arcLen = (a1 - a0) * Mathf.Deg2Rad * br; int n = Mathf.FloorToInt(arcLen / 0.85f);
                    for (int k = 0; k < n; k++)
                    {
                        if (R() > fill) continue;
                        float a = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, (k + 0.5f + (R() - 0.5f) * 0.6f) / n);
                        var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var side = Vector3.Cross(Vector3.up, d);
                        var seat = c + d * (br + (R() - 0.5f) * 0.12f) + Vector3.up * (by + (stand ? 0f : seatH));
                        // body (static): hunched shoulders under a mantle, or a standing figure; d points outward, faces turn to -d
                        float hunch = Mathf.Lerp(0f, 0.12f, R()), h = stand ? Mathf.Lerp(1.3f, 1.5f, R()) : Mathf.Lerp(0.5f, 0.7f, R());
                        mb.Set(S.Velvet, shroud * Mathf.Lerp(0.75f, 1.25f, R()));
                        // a mantle falling from the neck to the bench (or the floor): one flowing hooded silhouette with the cowl
                        mb.Push(seat - d * hunch * 0.4f, 0);
                        mb.Lathe(new[] { new Vector2(stand ? 0.3f : 0.28f, 0f), new Vector2(0.27f, h * 0.3f), new Vector2(0.23f, h * 0.62f), new Vector2(0.17f, h * 0.86f), new Vector2(0.11f, h * 0.98f), new Vector2(0.001f, h) }, 12);
                        mb.Pop();
                        var headPos = seat + Vector3.up * (h + 0.02f) - d * (0.02f + hunch);
                        ce.AddWatcher(holder, headPos, look0, Mathf.Lerp(0.92f, 1.1f, R()), rnd, rv);
                    }
                }
            // a fifth of the audience are devout: when nothing holds their attention they lift their heads to the Sun
            foreach (var e in ce._eyes) e.Devout = !e.Watcher && R() < 0.2f;

            v.Emit(rv, mb, "CourtGallery", go.transform, ShadowCastingMode.On);
            // a few low candles along the gallery so the audience is always half-lit
            for (int k = 0; k < 4; k++)
            {
                float adeg = (k + 0.25f) / 4f * 360f; if (Mathf.Abs(Mathf.DeltaAngle(adeg, aJ)) < 30f) adeg += 40f;
                float a = adeg * Mathf.Deg2Rad; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.AddLight(rv, c + d * (rad - 0.7f) + Vector3.up * (yB + 1.5f), new Color(1f, 0.78f, 0.55f), 1.3f, 5.5f, LightType.Point, false, 0.3f, fire: true);
            }
            return ce;
        }

        void AddWatcher(Transform holder, Vector3 pos, Vector3 look, float scale, System.Random rnd, MansionView.RoomView rv)
        {
            var fwd = (look - pos).normalized;
            var head = new GameObject("Watcher").transform; head.SetParent(holder, false); head.position = pos; head.rotation = Quaternion.LookRotation(fwd, Vector3.up); head.localScale = Vector3.one * scale;
            var hr = Part(head, "Hood", _hoodMesh, MansionMats.Get(S.Velvet), true);
            float r = (float)rnd.NextDouble(); int lvl = r < 0.6f ? 0 : r < 0.9f ? 1 : 2;   // most stay in shadow; a few catch the light
            var glint = new GameObject("Glint").transform; glint.SetParent(head, false); glint.localPosition = new Vector3(0f, 0.004f, 0.128f);
            var gr = Part(glint, "Pinpoints", _glintMesh, _glintMats[lvl], false);
            rv?.Renderers.Add(hr); rv?.Renderers.Add(gr);
            float seed = (float)rnd.NextDouble();
            _eyes.Add(new Eye
            {
                Head = head, Glint = glint, Fwd = fwd, Cone = 58f, Seed = seed,
                Speed = Mathf.Lerp(1.6f, 5.5f, seed * seed), Size = Mathf.Lerp(0.85f, 1.15f, (float)rnd.NextDouble()),
                NextBlink = Time.time + (float)rnd.NextDouble() * 6f, Watcher = rnd.NextDouble() < 0.08, Tremor = rnd.NextDouble() < 0.12,
            });
        }

        static Renderer Part(Transform parent, string name, Mesh mesh, Material mat, bool lit)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = lit;
            mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return mr;
        }

        // ================================================================== shared hood / pinpoint assets

        static void EnsureAssets()
        {
            if (_hoodMesh != null && _glintMesh != null && _glintMats != null && _glintMats[0] != null) return;
            // the hood (local: +z the face, +y up): a cowl rising to a soft point, drawn slightly back, open at the front in a
            // tall lens-shaped slit; inside it only black (the pinpoints sit in there, so from the side the cowl hides them)
            var mb = new MeshBuilder();
            var shroud = new Color(0.02f, 0.017f, 0.021f);
            mb.Set(S.Velvet, shroud);
            mb.Push(Vector3.zero, Quaternion.Euler(-9f, 0f, 0f), new Vector3(1f, 1f, 1.12f));
            // lathe angles run from +x toward +z: leave a wedge around +z (90°) open for the face
            // the lower cowl is open at the front (the face slit), the crown above the brow is whole (no notch at the peak)
            var cowl = new[] { new Vector2(0.001f, -0.24f), new Vector2(0.12f, -0.22f), new Vector2(0.162f, -0.12f), new Vector2(0.172f, 0.0f), new Vector2(0.158f, 0.1f) };
            var crown = new[] { new Vector2(0.158f, 0.1f), new Vector2(0.11f, 0.22f), new Vector2(0.05f, 0.3f), new Vector2(0.001f, 0.345f) };
            mb.Lathe(cowl, 18, false, false, 90f + 34f, 90f + 360f - 34f);
            mb.Lathe(crown, 18);
            // its black lining (the same shell a hair inside, facing in: nothing shows through the slit)
            var lining = new Vector2[cowl.Length]; for (int i = 0; i < cowl.Length; i++) lining[i] = new Vector2(cowl[cowl.Length - 1 - i].x * 0.965f, cowl[cowl.Length - 1 - i].y * 0.985f);
            mb.Set(S.Velvet, new Color(0.003f, 0.0025f, 0.003f));
            mb.Lathe(lining, 18, false, false, 90f + 34f, 90f + 360f - 34f);
            // the underside of the brow, closing the slit's top
            mb.Lathe(new[] { new Vector2(0.001f, 0.098f), new Vector2(0.152f, 0.098f) }, 18, false, false, 90f - 34f, 90f + 34f);
            mb.Pop();
            mb.Ellipsoid(new Vector3(0f, -0.01f, 0.0f), new Vector3(0.14f, 0.2f, 0.125f), 14, 8);   // the hollow, where the face should be
            // the drape over the shoulders
            mb.Set(S.Velvet, shroud);
            mb.Lathe(new[] { new Vector2(0.245f, -0.34f), new Vector2(0.2f, -0.24f), new Vector2(0.14f, -0.16f) }, 16);
            _hoodMesh = mb.ToMesh("WatcherHood", out _, true);
            // two almond pinpoints, 6.8 cm apart
            var m = new Mesh { name = "WatcherPinpoints" };
            var vs = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
            foreach (float x in new[] { -0.034f, 0.034f })
            {
                int b = vs.Count; float w = 0.016f, h = 0.009f;
                vs.Add(new Vector3(x - w, -h, 0)); vs.Add(new Vector3(x - w, h, 0)); vs.Add(new Vector3(x + w, h, 0)); vs.Add(new Vector3(x + w, -h, 0));
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
                tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });   // facing +z, out of the hood
            }
            m.SetVertices(vs); m.SetUVs(0, uv); m.SetNormals(new List<Vector3> { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward });
            m.SetTriangles(tris, 0); m.RecalculateBounds(); _glintMesh = m;
            var tex = PinpointTexture();
            _glintMats = new Material[GlintLevel.Length];
            for (int i = 0; i < _glintMats.Length; i++)
            {
                var gm = new Material(MansionMats.Particle("WatcherPinpointBase", tex, true)) { name = "WatcherPinpoint" + i };
                var col = GlintCol * GlintLevel[i]; col.a = 1f; gm.SetColor("_BaseColor", col); _glintMats[i] = gm;
            }
        }

        /// <summary>A soft almond of light, hotter at its core (no disc, no iris, no highlight).</summary>
        static Texture2D PinpointTexture()
        {
            const int W = 64, H = 32; var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "watcher_pinpoint", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H * 2f - 1f;
                    float lid = Mathf.Max(0.05f, 1f - u * u);                        // the almond narrows to points at its ends
                    float a = Mathf.Exp(-3.2f * u * u - 5.5f * (v / lid) * (v / lid));
                    a = Mathf.Clamp01(a * 1.15f) * Mathf.Clamp01(1f - Mathf.Abs(u) * 0.9f + 0.1f);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            tex.SetPixels32(px); tex.Apply(true, true); return tex;
        }

        static List<(float a0, float a1)> Runs(bool[] allowed)
        {
            var res = new List<(float, float)>(); int n = allowed.Length;
            int start = -1; for (int i = 0; i < n; i++) if (!allowed[i]) { start = i; break; }
            if (start < 0) { res.Add((0f, 360f)); return res; }
            int runStart = -1;
            for (int k = 1; k <= n; k++)
            {
                int i = (start + k) % n; bool on = allowed[i];
                if (on && runStart < 0) runStart = start + k;
                if ((!on || k == n) && runStart >= 0) { int end = on ? start + k + 1 : start + k; if (end - runStart >= 6) res.Add((runStart, end)); runStart = -1; }
            }
            return res;
        }
    }
}
