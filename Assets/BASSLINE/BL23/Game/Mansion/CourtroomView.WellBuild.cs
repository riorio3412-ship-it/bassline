using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// The Well of Witnesses: everything beyond the island's parapet. Colossal clustered piers on a 17.5 m ring, the well wall
    /// at 38 m with galleries every 5.4 m that compress into a fine lattice high up, the Judge's Lancet, hooded far watchers,
    /// amber doorways, chains, the lift tower, bridges, a stair that ends in the air, the Black Sun at the zenith and the haze.
    /// All of it is unlit (MansionGlow SOLID, fogged) with its light baked once into the vertices ("VoidStone"): value in the
    /// float channel (TEXCOORD2.x), hue in the colour. Additive layers (corona, haze, eye bands) are driven at runtime.
    /// Nothing here collides; everything is registered with the court room so the mansion's cull owns its visibility.
    /// </summary>
    public sealed partial class CourtroomView
    {
        // ------------------------------------------------------------------ public anchors / dimensions (additive API)
        public float HallRadius => 38f;
        public float PierRadius => 17.5f;
        public float ShaftTop => 118f;
        public float AbyssDepth => 70f;
        /// <summary>The pupil of the Black Sun (the look-up target).</summary>
        public Vector3 Zenith => _c + Vector3.up * 117.9f;
        /// <summary>The visible lower part of the Judge's Lancet (a telephoto background target).</summary>
        public Vector3 LancetPoint => _c + _jd * 39.5f + Vector3.up * 18f;
        /// <summary>Between the two piers that flank the throne.</summary>
        public Vector3 ThronePortal => _c + _jd * 17.5f + Vector3.up * 8f;
        /// <summary>High on the lift tower (tilt-up target).</summary>
        public Vector3 LiftTop => _c + _liftDir * 10.8f + Vector3.up * (LiftH - 6f);
        /// <summary>Down into the abyss past the parapet.</summary>
        public Vector3 AbyssPoint => _c + Dir(130f) * 24f - Vector3.up * 26f;
        /// <summary>Tuning: overall value of the baked void stone.</summary>
        public float VoidExposure = 1.6f;

        const float WallR = 38f, BackR = 39.4f, PierR = 17.5f, Top = 118f, Abyss = -70f;
        const int Sectors = 6;
        /// <summary>Near stone (h -14..34 above the floor) carries the full-contrast ashlar; beyond it a low-contrast copy, so the
        /// joints fade into the air instead of patterning it.</summary>
        const float NearLo = -14f, NearHi = 34f;

        /// <summary>The player build strips every fog variant (the only scene ships with fog off and fog stripping is automatic),
        /// so RenderSettings fog never reaches a shader. The well then bakes its own aerial perspective into the vertices: a slate
        /// air, lighter than the far stone. Decided once, at the first court build (before the atmosphere turns fog on).</summary>
        public static bool FogStripped { get; private set; } = true;
        static bool _fogProbed;
        public float AerialDensity = 0.017f;
        public Color AerialAir = new Color(0.020f, 0.022f, 0.028f);   // linear

        Transform _wellRoot;
        Material _mStone, _mStoneFar, _mVoid, _mBackdrop, _mDoorsEarly, _mDoorsLate, _mCorona, _mPupil, _mIris, _mHaze, _mShafts, _mPuffs;
        readonly Material[] _mBand = new Material[4];
        MeshBuilder[] _stone, _stoneFar, _iron, _wUp, _wCourt;
        /// <summary>The stone builder for a piece at angle th and height h (near band textured, the rest plain).</summary>
        MeshBuilder SB(float th, float h) => h >= NearLo && h <= NearHi ? _stone[Sec(th)] : _stoneFar[Sec(th)];
        MeshBuilder _glass, _closure, _rose;
        VQ _doorsE, _doorsL;
        readonly VQ[] _doorsV = new VQ[SpiralSteps];
        readonly Material[] _mDoorsV = new Material[SpiralSteps];
        readonly MeshBuilder[] _band = new MeshBuilder[4];
        readonly MeshRenderer[] _watchR = new MeshRenderer[Sectors];
        readonly Mesh[] _watchCourtMesh = new Mesh[Sectors];
        readonly Transform[] _chainPivot = new Transform[4];
        Transform _backdrop, _corona, _iris, _puffs, _counter;
        float _counterH = 6f; int _tris, _verts, _rends;
        System.Random _rnd;
        float Rn() => (float)_rnd.NextDouble();

        static int Sec(float th) => Mathf.Clamp((int)(Mathf.Repeat(th, 360f) / 60f), 0, Sectors - 1);
        static float TierH(int k) { if (k <= 9) return 5.4f * k; float h = 48.6f; for (int j = 10; j <= k; j++) h += 5.4f * Mathf.Pow(0.93f, j - 9); return h; }
        static float TierF(int k) => k <= 9 ? 1f : Mathf.Pow(0.93f, k - 9);
        const int TierMin = -12, TierMax = 33;
        /// <summary>Wall height where tier k's wall panel ends (under the next slab).</summary>
        static float TierTop(int k) => k >= TierMax ? 108f : TierH(k + 1) - 0.9f * TierF(k + 1);
        static bool InLancet(float th, int k) => Mathf.Abs(Mathf.DeltaAngle(th, 0f)) < 11.25f && k >= -1 && k <= 15;
        static float[] _tierTab;
        static void EnsureTierTable() { if (_tierTab != null) return; _tierTab = new float[TierMax - TierMin + 2]; for (int k = TierMin; k <= TierMax + 1; k++) _tierTab[k - TierMin] = TierH(k); }
        /// <summary>Wall value factor at height h: full at a tier's floor, falling into the shadow under the next ledge.</summary>
        static float UnderLedge(float h)
        {
            if (_tierTab == null || h < _tierTab[0] || h >= 108f) return 1f;
            int i = 0; while (i < _tierTab.Length - 2 && h >= _tierTab[i + 1]) i++;
            float h0 = _tierTab[i], h1 = _tierTab[i + 1], u = Mathf.InverseLerp(h0, h1, h);
            return Mathf.Lerp(1f, 0.45f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, u)));
        }

        /// <summary>Material of baked void geometry: (albedo, object noise, mode 0 = bake light / 1 = explicit value, group -1).</summary>
        static void Stone(MeshBuilder mb, float albedo, float noise, Color hue) => mb.Set(S.Glow, hue, new Vector4(albedo, noise, 0f, 9f));
        static void Lum(MeshBuilder mb, float value, Color hue) => mb.Set(S.Glow, hue, new Vector4(value, 1f, 1f, 9f));
        static readonly Color StoneHue = new Color(0.93f, 0.96f, 1f), IronHue = new Color(0.9f, 0.92f, 1f), Bone = new Color(0.9f, 0.86f, 0.8f), DoorWarm = new Color(1f, 0.62f, 0.32f);
        const float AStone = 0.35f, AIron = 0.12f, AIronDark = 0.045f, ABridge = 0.30f;

        // ================================================================== build
        void BuildWell(MansionView v, MansionView.RoomView rv, Transform root)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (!_fogProbed) { _fogProbed = true; FogStripped = !RenderSettings.fog && !Application.isEditor; }
            _wellRoot = new GameObject("CourtWell").transform; _wellRoot.SetParent(root, false);
            _rnd = new System.Random((int)(_seed % 1000003UL) * 7 + 911);
            MakeMaterials();
            _stone = new MeshBuilder[Sectors]; _stoneFar = new MeshBuilder[Sectors]; _iron = new MeshBuilder[Sectors]; _wUp = new MeshBuilder[Sectors]; _wCourt = new MeshBuilder[Sectors];
            for (int i = 0; i < Sectors; i++) { _stone[i] = new MeshBuilder(); _stoneFar[i] = new MeshBuilder(); _iron[i] = new MeshBuilder(); _wUp[i] = new MeshBuilder(); _wCourt[i] = new MeshBuilder(); }
            _doorsE = new VQ { Mode = 1f }; _doorsL = new VQ { Mode = 1f }; _glass = new MeshBuilder(); _closure = new MeshBuilder(); _rose = new MeshBuilder();
            for (int i = 0; i < SpiralSteps; i++) _doorsV[i] = new VQ { Mode = 1f };
            _spiral0 = 40f + Rn() * 50f;
            for (int i = 0; i < 4; i++) _band[i] = new MeshBuilder();

            // each piece on its own: one failing never costs the others
            void Try(System.Action a, string what) { try { a(); } catch (System.Exception e) { Debug.LogError("[CourtWell] " + what + ": " + e); } }
            Try(Backdrop, "backdrop");
            Try(Drum, "drum");
            Try(Piers, "piers");
            Try(WallAndTiers, "wall");
            Try(Lancet, "lancet");
            Try(LancetWatchers, "lancet watchers");
            Try(Closure, "closure");
            Try(Rulers, "rulers");
            Try(Chains, "chains");
            Try(LiftTower, "lift tower");
            Try(Zenith3, "zenith");
            Try(Air, "air");
            // (no Leviathan: the owner asked for no fish flying through the air — "물고기가 허공을 날아다니는 것들 없애주고")
            Try(TrialBelow, "trial below");
            Try(ChandelierThread, "chandelier thread");
            Try(LancetPresence, "lancet presence");
            Try(StairWalker, "stair walker");
            Try(Bell, "bell");
            Try(Hourglass, "hourglass");   // lit, on the bench (CourtroomView.Omens.cs)
            Try(Jury, "stone jury");        // CourtroomView.Jury.cs (its corbels go into the far stone, emitted below)
            Try(WakeRings, "wake rings");

            // ---- emit (sector meshes: frustum culling keeps 1..3 of them in a close shot)
            for (int s = 0; s < Sectors; s++)
            {
                Put(_stone[s], "CourtWell_S" + s, _wellRoot, _mStone, true, Vector3.zero, true);
                Put(_stoneFar[s], "CourtWellFar_S" + s, _wellRoot, _mStoneFar, true, Vector3.zero, true);
                Put(_iron[s], "CourtIron_S" + s, _wellRoot, _mVoid, true, Vector3.zero);
                if (!_wCourt[s].Empty) { var cm = _wCourt[s].ToMesh("CourtWatchersC_S" + s, out _, false); BakeVoid(cm, Vector3.zero); _watchCourtMesh[s] = cm; }
                _watchR[s] = Put(_wUp[s], "CourtWatchers_S" + s, _wellRoot, _mVoid, true, Vector3.zero);
                if (_watchR[s] != null) _watchUpMesh[s] = _watchR[s].GetComponent<MeshFilter>().sharedMesh;
            }
            Put(_closure, "CourtClosure", _wellRoot, _mStoneFar, true, Vector3.zero, true);
            Put(_rose, "CourtRose", _wellRoot, _mStoneFar, true, Vector3.zero, true);
            Put(_glass, "CourtLancetGlass", _wellRoot, _mVoid, true, Vector3.zero);
            PutBaked(_doorsE, "CourtDoorsEarly", _mDoorsEarly);
            PutBaked(_doorsL, "CourtDoorsLate", _mDoorsLate);
            for (int i = 0; i < SpiralSteps; i++) PutBaked(_doorsV[i], "CourtDoorsVerdict" + i, _mDoorsV[i]);
            for (int b = 0; b < 4; b++) Put(_band[b], "CourtEyeBand" + (char)('A' + b), _wellRoot, _mBand[b], false, Vector3.zero);
            Debug.Log($"[CourtWell] tris {_tris} verts {_verts} renderers {_rends} eyes {_eyes} watchers {_watchers} build {sw.Elapsed.TotalMilliseconds:0} ms (ashlar {_tAshlar:0} uv {_tUV:0} bake {_tBake:0})");
            _built = true;
        }

        void MakeMaterials()
        {
            Material Clone(int slot, string n) => new Material(MansionMats.Get(slot)) { name = n };
            // dressed ashlar in world metres (CourtroomView.Stone.cs); the far stone a low-contrast copy so its joints fade with the air
            { var ta = System.Diagnostics.Stopwatch.StartNew(); EnsureAshlar(); _tAshlar = ta.Elapsed.TotalMilliseconds; }
            _mStone = Clone(S.Glow, "CourtColossus");
            _mStone.SetTexture("_MainTex", _ashlarNear); _mStone.SetTextureScale("_MainTex", new Vector2(1f / AshW, 1f / AshH)); _mStone.SetTextureOffset("_MainTex", Vector2.zero);
            _mStone.SetColor("_Color", Color.white); _mStone.SetFloat("_Intensity", 1f);
            _mVoid = Clone(S.Glow, "CourtVoidLight"); _mVoid.SetTexture("_MainTex", Texture2D.whiteTexture); _mVoid.SetColor("_Color", Color.white); _mVoid.SetFloat("_Intensity", 1f);
            _mStoneFar = new Material(_mStone) { name = "CourtColossusFar" }; _mStoneFar.SetTexture("_MainTex", _ashlarFar);
            // the backdrop is pure fog colour where fog renders; where it does not, it carries the baked air instead of the clear black
            _mBackdrop = new Material(_mVoid) { name = "CourtBackdrop" }; _mBackdrop.SetColor("_Color", FogStripped ? Color.white : Color.black); _mBackdrop.renderQueue = 2450;
            _mDoorsEarly = new Material(_mVoid) { name = "CourtDoorsEarly" };
            _mDoorsLate = new Material(_mVoid) { name = "CourtDoorsLate" }; _mDoorsLate.SetFloat("_Intensity", 0f);
            for (int i = 0; i < SpiralSteps; i++) { _mDoorsV[i] = new Material(_mVoid) { name = "CourtDoorsVerdict" + i }; _mDoorsV[i].SetFloat("_Intensity", 0f); }
            _mCorona = Clone(S.GlowAdd, "ZenithCorona"); _mCorona.SetTexture("_MainTex", Texture2D.whiteTexture);
            _mPupil = Clone(S.GlowAdd, "ZenithPupil"); _mPupil.SetTexture("_MainTex", Texture2D.whiteTexture);
            _mPupil.SetFloat("_SrcBlend", (float)BlendMode.Zero); _mPupil.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcColor);
            _mPupil.SetColor("_Color", new Color(0.985f, 0.985f, 0.985f, 1f)); _mPupil.renderQueue = _mCorona.renderQueue + 1;
            _mIris = Clone(S.GlowAdd, "ZenithIris"); _mIris.SetTexture("_MainTex", Texture2D.whiteTexture); _mIris.renderQueue = _mPupil.renderQueue + 1; _mIris.SetFloat("_Intensity", 0f);
            _mHaze = Clone(S.Ray, "CourtHaze");
            _mShafts = Clone(S.Ray, "CourtLancetShafts");
            _mPuffs = Clone(S.Halo, "CourtPuffs");
            for (int b = 0; b < 4; b++) { _mBand[b] = Clone(S.Halo, "CourtEyeBand" + b); _mBand[b].SetFloat("_Intensity", 0f); }
        }

        MeshRenderer Put(MeshBuilder mb, string name, Transform parent, Material mat, bool bake, Vector3 offset, bool textured = false)
        {
            if (mb == null || mb.Empty) return null;
            var mesh = mb.ToMesh(name, out _, false);
            if (textured) { var tu = System.Diagnostics.Stopwatch.StartNew(); WorldUV(mesh); _tUV += tu.Elapsed.TotalMilliseconds; }
            if (bake) BakeVoid(mesh, offset, textured ? _ashlarAvg : 1f);
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mats = new Material[mesh.subMeshCount]; for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _rv.Renderers.Add(mr); _wellRends.Add(mr);
            for (int s = 0; s < mesh.subMeshCount; s++) _tris += (int)(mesh.GetIndexCount(s) / 3);
            _verts += mesh.vertexCount; _rends++;
            return mr;
        }

        /// <summary>A VQ of explicit values (lights): bake the air in, then emit it like any other well piece.</summary>
        void PutBaked(VQ q, string name, Material mat)
        {
            if (q == null) return;
            var m = q.ToMesh(name); if (m.vertexCount == 0) return;
            BakeVoid(m, Vector3.zero); PutMesh(m, name, _wellRoot, mat);
        }

        Transform Pivot(string name, Vector3 pos)
        {
            var t = new GameObject(name).transform; t.SetParent(_wellRoot, false); t.position = pos; return t;
        }

        // ================================================================== the VoidStone bake
        static float H3(Vector3 p) { int x = Mathf.FloorToInt(p.x * 0.6f), y = Mathf.FloorToInt(p.y * 0.6f), z = Mathf.FloorToInt(p.z * 0.6f); return Hash(x * 73856093 ^ y * 19349663 ^ z * 83492791, 11); }

        /// <summary>Bake light into void geometry: rgb = hue (max component 1), custom.x = value. Mode 1 vertices keep their value.
        /// Where fog does not render, the aerial perspective is baked in too (toward AerialAir, seen from just above the floor);
        /// matAvg is what the material multiplies on top (the stone texture's average), so the air lands on the right value.</summary>
        double _tAshlar, _tUV, _tBake;
        void BakeVoid(Mesh m, Vector3 offset, float matAvg = 1f)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            BakeVoidCore(m, offset, matAvg);
            _tBake += sw.Elapsed.TotalMilliseconds;
        }
        void BakeVoidCore(Mesh m, Vector3 offset, float matAvg)
        {
            var vs = m.vertices; var ns = m.normals; var cs = m.colors32; var xs = new List<Vector4>(vs.Length); m.GetUVs(2, xs);
            if (xs.Count != vs.Length || cs.Length != vs.Length) return;
            var up = Vector3.up; var zen = Zenith; var lanC = _c + _jd * 39.5f + up * 35f; var warmC = _c + up * 3f;
            var warmHue = new Color(1f, 0.78f, 0.55f);
            var eye = _c + up * 3f; var air = AerialAir / Mathf.Max(0.05f, matAvg); bool aerial = FogStripped && AerialDensity > 0f;
            EnsureTierTable();
            for (int i = 0; i < vs.Length; i++)
            {
                var x = xs[i]; Color hue = cs[i];
                float value; bool light = false, noAir = false;
                if (x.z > 0.5f)
                {
                    value = x.x; light = value >= 0.15f;
                    noAir = x.y > 2.5f;   // a true black that no haze greys (the thread into the Sun)
                    if (x.y > 1.5f && !noAir)
                    {
                        // a far watcher: black, with a faint cold rim where its outline turns away from the court
                        var p = vs[i] + offset; var n = ns[i]; var toC = (_c + up * 2f - p).normalized;
                        float rim = Mathf.Pow(1f - Mathf.Abs(Vector3.Dot(n, toC)), 3f);
                        value += 0.07f * rim; hue = Color.Lerp(hue, new Color(0.75f, 0.82f, 1f), Mathf.Clamp01(rim * 1.5f));
                    }
                }
                else
                {
                    var p = vs[i] + offset; var n = ns[i]; float h = p.y - _c.y;
                    float zt = 0.20f * Mathf.Max(0f, Vector3.Dot(n, (zen - p).normalized));
                    var tl = lanC - p; float dl = tl.magnitude;
                    float lt = 0.18f * Mathf.Max(0f, Vector3.Dot(n, tl / Mathf.Max(dl, 1e-3f))) / (1f + (dl / 35f) * (dl / 35f));
                    float ab = 0.10f * Mathf.Max(0f, -n.y) * Mathf.Lerp(1f, 0.3f, Mathf.Clamp01((h + 60f) / 80f));
                    var tw = warmC - p; float dw = tw.magnitude;
                    float wt = 0.80f * Mathf.Max(0f, Vector3.Dot(n, tw / Mathf.Max(dw, 1e-3f))) * Mathf.Exp(-dw / 10f);
                    var ax = new Vector3(_c.x - p.x, 0f, _c.z - p.z); float axm = ax.magnitude;
                    float rim = axm > 1e-3f ? 0.12f * Mathf.Pow(1f - Mathf.Abs(Vector3.Dot(n, ax / axm)), 3f) : 0f;
                    float ledge = 0.10f * Mathf.Max(0f, n.y);
                    float lit = 0.030f + zt + lt + ab + wt + rim + ledge;
                    float noise = (x.y > 0f ? x.y : 1f) * (0.8f + 0.4f * H3(p));
                    // the wall darkens under each gallery's ledge (the soffit's shadow, the grime running off it)
                    float rw = new Vector2(p.x - _c.x, p.z - _c.z).magnitude;
                    if (rw > 37.8f && Mathf.Abs(n.y) < 0.35f) noise *= UnderLedge(h);
                    value = Mathf.Max(0.0005f, VoidExposure * x.x * lit * noise);
                    hue = Color.Lerp(hue, warmHue, Mathf.Clamp01(wt / Mathf.Max(lit, 1e-4f)) * 0.85f);
                }
                float mx = Mathf.Max(hue.r, Mathf.Max(hue.g, hue.b)); if (mx > 1e-4f) { hue.r /= mx; hue.g /= mx; hue.b /= mx; }
                if (aerial && !noAir)
                {
                    float d = Vector3.Distance(vs[i] + offset, eye), T = Mathf.Exp(-(AerialDensity * d) * (AerialDensity * d));
                    if (light) T = Mathf.Pow(T, 0.4f);   // a lamp carries through haze that swallows the stone around it
                    var rgb = new Color(hue.r * value * T + air.r * (1f - T), hue.g * value * T + air.g * (1f - T), hue.b * value * T + air.b * (1f - T));
                    value = Mathf.Max(0.0005f, Mathf.Max(rgb.r, Mathf.Max(rgb.g, rgb.b)));
                    hue = new Color(rgb.r / value, rgb.g / value, rgb.b / value);
                }
                hue.a = 1f;
                cs[i] = hue; xs[i] = new Vector4(value, 0f, 0f, 9f);
            }
            m.colors32 = cs; m.SetUVs(2, xs);
        }

        // ================================================================== pieces
        void Backdrop()
        {
            // an inward sphere that follows the lens: wherever nothing covers the view it is pure fog colour (never the clear black)
            var mb = new MeshBuilder();
            Lum(mb, 0.0005f, Color.white);
            var prof = new List<Vector2>();
            for (int i = 12; i >= 0; i--) { float a = -Mathf.PI / 2 + Mathf.PI * i / 12f; prof.Add(new Vector2(Mathf.Max(0.01f, Mathf.Cos(a) * 130f), Mathf.Sin(a) * 130f)); }
            mb.Lathe(prof, 24);
            _backdrop = Pivot("CourtBackdrop", _c);
            Put(mb, "CourtBackdrop", _backdrop, _mBackdrop, true, _c);
        }

        void Drum()
        {
            // the island's stem: a cornice under the parapet, corbels, and a tapering drum that falls into the abyss
            var deep = new[] { new Vector2(6.6f, Abyss), new Vector2(6.6f, -22f), new Vector2(7.0f, -12f) };
            var low = new[] { new Vector2(7.0f, -12f), new Vector2(8.2f, -7f), new Vector2(9.8f, -3f), new Vector2(10.6f, -1.0f) };
            for (int s = 0; s < Sectors; s++)
            {
                float a0 = s * 60f, a1 = a0 + 60f;
                Stone(_stoneFar[s], AStone * 0.8f, 0.95f, StoneHue); LatheArc(_stoneFar[s], deep, a0, a1, 8);
                Stone(_stone[s], AStone * 0.8f, 0.95f, StoneHue); LatheArc(_stone[s], low, a0, a1, 8);
            }
            // cornice (not in the judge's breach): tall, up to the parapet, behind the oak bays; under the open arcade only a
            // ledge at floor level, so the openings look straight out into the well
            for (int i = 0; i < 36; i++)
            {
                float t0 = i * 10f, t1 = t0 + 10f, tc = t0 + 5f;
                if (RimBreach(tc)) continue;
                if (t0 < 22f && t1 > 22f) t0 = 22f; if (t0 < 338f && t1 > 338f) t1 = 338f;
                var mb = _stone[Sec(tc)];
                Stone(mb, AStone, 1f, StoneHue);
                if (RimOpen(tc))
                {
                    LatheFace(mb, 10.6f, -1.0f, 10.6f, -0.35f, t0, t1, 2);
                    LatheFace(mb, 10.6f, -0.35f, 11.0f, -0.2f, t0, t1, 2);
                    LatheFace(mb, 11.0f, -0.2f, 11.0f, 0.0f, t0, t1, 2);
                    Stone(mb, AStone * 1.1f, 1f, StoneHue); LatheFace(mb, 11.0f, 0.0f, 10.05f, 0.0f, t0, t1, 2);
                }
                else
                {
                    LatheFace(mb, 10.6f, -1.0f, 10.6f, 1.2f, t0, t1, 2);
                    LatheFace(mb, 10.6f, 1.2f, 11.2f, 1.6f, t0, t1, 2);
                    LatheFace(mb, 11.2f, 1.6f, 11.2f, 2.4f, t0, t1, 2);
                    LatheFace(mb, 11.2f, 2.4f, 10.35f, 2.8f, t0, t1, 2);
                }
            }
            for (int k = 0; k < 48; k++)
            {
                float th = k * 7.5f + 3.75f; if (Mathf.Abs(Mathf.DeltaAngle(th, 0f)) < 24f) continue;
                var mb = _stone[Sec(th)]; Stone(mb, AStone * 0.9f, 0.9f + Rn() * 0.2f, StoneHue);
                mb.Push(Matrix4x4.TRS(P(th, 11.2f, -0.82f), Quaternion.LookRotation(Dir(th)), Vector3.one));   // a corbel table under the ledge
                mb.Box(Vector3.zero, new Vector3(0.6f, 1.4f, 1.1f)); mb.Pop();
            }
        }

        // ---- piers -------------------------------------------------------------------------------------------------
        // first-order rows: far below the near band, near (textured) in it, far above it
        static readonly float[] RowsDeep = { -70f, -58f, -46f, -34f, -22f, NearLo };
        static readonly float[] RowsNear = { NearLo, -10f, -6f, -2f, 2f, 6f, 10f, 14f, 18f, 24f, 30f, NearHi };
        static readonly float[] RowsHigh = { NearHi, 36f };
        void Piers()
        {
            for (int i = 0; i < 12; i++)
            {
                float th = 15f + 30f * i; var mn = _stone[Sec(th)]; var mf = _stoneFar[Sec(th)];
                float noise = 0.88f + Rn() * 0.24f;
                float topH = Mathf.Lerp(92f, 104f, Rn());
                var frame = Matrix4x4.TRS(P(th, PierR, 0f), Quaternion.LookRotation(-Dir(th)), Vector3.one);
                mn.Push(frame); mf.Push(frame);
                // first order: core, engaged + corner shafts, annulets (the counting rhythm), capital
                Stone(mn, AStone, noise, StoneHue); Stone(mf, AStone, noise, StoneHue);
                TallBox(mf, 1.6f, 1.6f, new List<float>(RowsDeep), false);
                TallBox(mn, 1.6f, 1.6f, new List<float>(RowsNear), false);
                TallBox(mf, 1.6f, 1.6f, new List<float> { NearHi, 36f, 46f }, true);
                foreach (var (x, z, r) in new[] { (1.6f, 0f, 0.55f), (-1.6f, 0f, 0.55f), (0f, 1.6f, 0.55f), (0f, -1.6f, 0.55f), (1.6f, 1.6f, 0.28f), (-1.6f, 1.6f, 0.28f), (1.6f, -1.6f, 0.28f), (-1.6f, -1.6f, 0.28f) })
                {
                    foreach (var (b, rows) in new[] { (mf, RowsDeep), (mn, RowsNear), (mf, RowsHigh) })
                    {
                        var prof = new Vector2[rows.Length]; for (int k = 0; k < rows.Length; k++) prof[k] = new Vector2(r, rows[k]);
                        b.Push(new Vector3(x, 0, z), 0); b.Lathe(prof, 6); b.Pop();
                    }
                }
                Stone(mn, AStone * 1.1f, noise, StoneHue); Stone(mf, AStone * 1.1f, noise, StoneHue);
                for (float h = -60f; h < 36f; h += 12f) (h >= NearLo && h <= NearHi ? mn : mf).Box(new Vector3(0, h + 0.25f, 0), new Vector3(3.7f, 0.5f, 3.7f));
                mf.Box(new Vector3(0, 36.6f, 0), new Vector3(4.2f, 1.2f, 4.2f));
                // second order
                Stone(mf, AStone, noise, StoneHue);
                var rows2 = new List<float> { 47.2f, 56f, 66f, 76f, 84f, 91f };
                if (topH > 91.5f) rows2.Add(topH);
                TallBox(mf, 1.3f, 1.3f, rows2, true);
                foreach (var (x, z) in new[] { (1.3f, 0f), (-1.3f, 0f), (0f, 1.3f), (0f, -1.3f) })
                {
                    mf.Push(new Vector3(x, 0, z), 0); mf.Lathe(new[] { new Vector2(0.45f, 47.2f), new Vector2(0.45f, 60f), new Vector2(0.45f, 72f), new Vector2(0.45f, 83f) }, 6); mf.Pop();
                }
                Stone(mf, AStone * 1.1f, noise, StoneHue);
                for (float h = 58f; h < 83f; h += 12f) mf.Box(new Vector3(0, h + 0.2f, 0), new Vector3(3.1f, 0.4f, 3.1f));
                mf.Box(new Vector3(0, 83.5f, 0), new Vector3(3.4f, 1.0f, 3.4f));
                // broken tops: stepped blocks, tilted, reaching for nothing
                float bh = topH;
                for (int k = 0; k < 3; k++)
                {
                    float w = 2.4f - k * 0.55f, hgt = 2.6f - k * 0.5f;
                    var q = Quaternion.Euler((Rn() - 0.5f) * 14f * (k + 1), Rn() * 30f, (Rn() - 0.5f) * 14f * (k + 1));
                    mf.Push(Matrix4x4.TRS(new Vector3((Rn() - 0.5f) * 0.6f, bh + hgt * 0.5f, (Rn() - 0.5f) * 0.6f), q, Vector3.one));
                    mf.Box(Vector3.zero, new Vector3(w, hgt, w * (0.8f + Rn() * 0.3f))); mf.Pop();
                    bh += hgt * 0.85f;
                }
                mn.Pop(); mf.Pop();
                // the arcades to the next pier (both orders)
                Arcade(th + 15f, 37.2f, 46f, 5.9f, 1.6f, noise);
                Arcade(th + 15f, 84f, 91f, 6.3f, 1.3f, noise);
            }
            // string courses: a full ring over the first arcade, a broken one over the second
            for (int s = 0; s < Sectors; s++)
            {
                var mb = _stoneFar[s]; float a0 = s * 60f, a1 = a0 + 60f;
                Stone(mb, AStone * 1.15f, 1f, StoneHue);
                RingFaces(mb, 15.2f, 19.2f, 46f, 47.2f, a0, a1, 8);
            }
            var gaps = new List<(float, float)>();
            for (int k = 0; k < 4; k++) { float g = 20f + k * 90f + Rn() * 50f; gaps.Add((g, 3f + Rn() * 2f)); }
            foreach (var (a0, a1) in ArcsClear(gaps.ToArray()))
                for (float t0 = a0; t0 < a1 - 0.01f; t0 += 30f)
                {
                    float t1 = Mathf.Min(a1, t0 + 30f); var mb = _stoneFar[Sec((t0 + t1) * 0.5f)];
                    Stone(mb, AStone * 1.15f, 1f, StoneHue);
                    RingFaces(mb, 15.6f, 18.9f, 91f, 92.2f, t0, t1, Mathf.Max(1, Mathf.CeilToInt((t1 - t0) / 7.5f)));
                }
        }

        /// <summary>A box's four sides (half extents hx, hz in the pushed frame) split into rows at the given heights (optional top).</summary>
        static void TallBox(MeshBuilder mb, float hx, float hz, List<float> rows, bool top = true)
        {
            var c = new[] { new Vector2(-hx, -hz), new Vector2(hx, -hz), new Vector2(hx, hz), new Vector2(-hx, hz) };
            for (int s = 0; s < 4; s++)
            {
                var a = c[s]; var b = c[(s + 1) % 4]; var mid = (a + b) * 0.5f; var want = new Vector3(mid.x, 0, mid.y);
                for (int i = 0; i < rows.Count - 1; i++)
                    mb.QuadAuto(new Vector3(a.x, rows[i], a.y), new Vector3(a.x, rows[i + 1], a.y), new Vector3(b.x, rows[i + 1], b.y), new Vector3(b.x, rows[i], b.y), want);
            }
            if (top) mb.QuadAuto(new Vector3(-hx, rows[rows.Count - 1], -hz), new Vector3(-hx, rows[rows.Count - 1], hz), new Vector3(hx, rows[rows.Count - 1], hz), new Vector3(hx, rows[rows.Count - 1], -hz), Vector3.up);
        }

        /// <summary>A ring slab (outer face, top, inner face, underside) over the θ-arc.</summary>
        void RingFaces(MeshBuilder mb, float rIn, float rOut, float h0, float h1, float a0, float a1, int seg)
        {
            LatheFace(mb, rOut, h0, rOut, h1, a0, a1, seg);
            LatheFace(mb, rOut, h1, rIn, h1, a0, a1, seg);
            LatheFace(mb, rIn, h1, rIn, h0, a0, a1, seg);
            LatheFace(mb, rIn, h0, rOut, h0, a0, a1, seg);
        }

        /// <summary>An equilateral pointed arch between two piers (chord midpoint at θm), springing at hs, spandrel to htop, depth ±hd.</summary>
        void Arcade(float thm, float hs, float htop, float span, float hd, float noise)
        {
            var mb = _stoneFar[Sec(thm)];
            float rm = PierR * Mathf.Cos(15f * Mathf.Deg2Rad);
            mb.Push(Matrix4x4.TRS(P(thm, rm, 0f), Quaternion.LookRotation(-Dir(thm)), Vector3.one));
            float s = span; const int n = 6; var I = new List<Vector3>(); var E = new List<Vector3>();
            for (int j = 0; j <= n; j++) { float a = (180f - 60f * j / n) * Mathf.Deg2Rad; var cc = new Vector2(s / 2, hs); var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); I.Add(new Vector3(cc.x + d.x * s, cc.y + d.y * s, 0)); E.Add(new Vector3(cc.x + d.x * (s + 0.9f), cc.y + d.y * (s + 0.9f), 0)); }
            for (int j = 1; j <= n; j++) { float a = (60f - 60f * j / n) * Mathf.Deg2Rad; var cc = new Vector2(-s / 2, hs); var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); I.Add(new Vector3(cc.x + d.x * s, cc.y + d.y * s, 0)); E.Add(new Vector3(cc.x + d.x * (s + 0.9f), cc.y + d.y * (s + 0.9f), 0)); }
            var zf = new Vector3(0, 0, hd); var ctr = new Vector3(0, hs, 0);
            Stone(mb, AStone * 0.9f, noise, StoneHue);
            for (int j = 0; j < I.Count - 1; j++)
            {
                var a = I[j]; var b = I[j + 1]; var want = ctr - (a + b) * 0.5f;
                mb.QuadAuto(a - zf, a + zf, b + zf, b - zf, want);                                                      // soffit
                var ta = new Vector3(a.x, htop, 0); var tb = new Vector3(b.x, htop, 0);
                mb.QuadAuto(a + zf, ta + zf, tb + zf, b + zf, Vector3.forward);                                         // spandrel, court side
                mb.QuadAuto(a - zf, ta - zf, tb - zf, b - zf, Vector3.back);                                            // spandrel, wall side
            }
            Stone(mb, AStone * 1.2f, noise, StoneHue);
            var zr = new Vector3(0, 0, hd + 0.12f);
            for (int j = 0; j < I.Count - 1; j++) mb.QuadAuto(I[j] + zr, E[j] + zr, E[j + 1] + zr, I[j + 1] + zr, Vector3.forward);   // voussoir ring
            mb.Pop();
        }

        // ---- the well wall and its galleries ---------------------------------------------------------------------------
        readonly List<(float th, int k, float f, int grp)> _openings = new List<(float, int, float, int)>();
        readonly List<(float th, int k)> _rails = new List<(float, int)>();
        void WallAndTiers()
        {
            // plain wall below the lowest gallery
            WallBand(Abyss, TierH(TierMin) - 0.9f, 0f, 360f);
            int torn = 0;
            var tornBays = new HashSet<int>(); while (tornBays.Count < 2) tornBays.Add(_rnd.Next(48) + 1000 * (_rnd.Next(12) - 2));
            bool prevLit = false;
            for (int k = TierMin; k <= TierMax; k++)
            {
                float h = TierH(k), f = TierF(k), top = TierTop(k);
                bool detailed = k >= -3 && k <= 14;
                int bays = f < 0.5f ? 96 : 48; float bw = 360f / bays;
                // slab + balustrade, bay by bay (6% lose their balustrade; tier 7 lost a whole arc)
                for (int b = 0; b < 48; b++)
                {
                    float th = b * 7.5f, t0 = th - 3.75f, t1 = th + 3.75f;
                    if (InLancet(th, k)) continue;
                    if (k == 7 && Mathf.Repeat(th, 360f) > 258f && Mathf.Repeat(th, 360f) < 302f) continue;
                    var mb = SB(th, h);
                    // a moulded ledge: pale top (the line the eye follows round the well), a nosing, a fascia, a bed mould and a
                    // soffit in shadow; every bay's stone a little different
                    float bn = 0.82f + Rn() * 0.36f;
                    Stone(mb, AStone * 0.8f, bn, StoneHue); LatheFace(mb, WallR, h, 36.2f, h, t0, t1, 1);
                    Stone(mb, AStone * 0.85f, bn, StoneHue); LatheFace(mb, 36.2f, h, 36.0f, h - 0.1f * f, t0, t1, 1);
                    Stone(mb, AStone * 0.62f, bn, StoneHue); LatheFace(mb, 36.0f, h - 0.1f * f, 36.0f, h - 0.36f * f, t0, t1, 1);
                    Stone(mb, AStone * 0.42f, bn, StoneHue); LatheFace(mb, 36.0f, h - 0.36f * f, 36.2f, h - 0.5f * f, t0, t1, 1);
                    Stone(mb, AStone * 0.28f, bn, StoneHue); LatheFace(mb, 36.2f, h - 0.5f * f, 36.55f, h - 0.64f * f, t0, t1, 1);
                    Stone(mb, AStone * 0.16f, bn, StoneHue); LatheFace(mb, 36.55f, h - 0.64f * f, WallR, h - 0.9f * f, t0, t1, 1);
                    bool inverted = k == 11 && th >= 120f && th <= 150f;
                    bool rail = k <= 24 && Rn() > 0.06f;
                    if (rail && !inverted) _rails.Add((th, k));
                    if (rail)
                    {
                        if (inverted)
                        {
                            float hb = h - 0.9f * f;
                            Stone(mb, AStone * 0.5f, bn, StoneHue); LatheFace(mb, 36.35f, hb, 36.35f, hb - 1.1f * f, t0, t1, 1);
                            Stone(mb, AStone * 0.7f, bn, StoneHue); LatheFace(mb, 36.35f, hb - 1.1f * f, 36.6f, hb - 1.1f * f, t0, t1, 1);
                        }
                        else
                        {
                            // a solid balustrade with a projecting coping
                            Stone(mb, AStone * 0.9f, bn, StoneHue); LatheFace(mb, 36.72f, h + 1.12f * f, 36.24f, h + 1.12f * f, t0, t1, 1);
                            Stone(mb, AStone * 0.6f, bn, StoneHue); LatheFace(mb, 36.24f, h + 1.12f * f, 36.24f, h + 1.0f * f, t0, t1, 1);
                            Stone(mb, AStone * 0.3f, bn, StoneHue); LatheFace(mb, 36.24f, h + 1.0f * f, 36.35f, h + 1.0f * f, t0, t1, 1);
                            Stone(mb, AStone * 0.5f, bn, StoneHue); LatheFace(mb, 36.35f, h + 1.0f * f, 36.35f, h, t0, t1, 1);
                        }
                    }
                }
                // the wall panel with its openings
                for (int b = 0; b < bays; b++)
                {
                    float th = b * bw; if (InLancet(th, k)) continue;
                    var mb = SB(th, h);
                    float w = Mathf.Max(1.2f, 3.1f * f), apex = Mathf.Min(h + 4.3f * f, top - 0.15f * f);
                    if (bays == 96) w = Mathf.Max(0.9f, 3.1f * f * 0.85f);
                    float spring = Mathf.Min(h + 3.2f * f, apex - 1.1f * w * 0.5f);   // a pointed head needs rise > half-span
                    float bn = 0.8f + Rn() * 0.4f;
                    if (detailed)
                    {
                        int key = b + 1000 * k; bool deep = tornBays.Contains(key) && torn < 2; if (deep) torn++;
                        OpeningBay(mb, th, bw, h, top, w, spring, apex, deep ? BackR + 2f : BackR, bn);
                        // lit doorways: a few from the start, more as the trial deepens, and at the verdict a spiral of them
                        // climbing the well (one turn every 60 m, in six steps)
                        int grp = -1; bool odd = k == 11 && th >= 118f && th <= 152f;
                        float sp = SpiralTh(h); float dd = Mathf.Abs(Mathf.DeltaAngle(th, sp));
                        if (!odd && dd < 8f && Rn() < 0.85f) grp = 2 + Mathf.Clamp(Mathf.FloorToInt((h + 16.5f) / 15f), 0, SpiralSteps - 1);
                        // the lowest galleries are what a speaker has behind them through the arcade: more of them are lit, and from the start
                        else if (!odd && !prevLit && Rn() < (k >= -1 && k <= 1 ? 0.27f : 0.13f)) grp = Rn() < (k >= -1 && k <= 1 ? 0.72f : 0.45f) ? 0 : 1;
                        prevLit = grp >= 0;
                        _openings.Add((th, k, f, grp));
                    }
                    else FlatBay(mb, th, bw, h, top, w, spring, apex, bn);
                }
            }
            // the neck under the cap
            WallBand(108f, Top, 0f, 360f);
            // amber doorways (the lit ones), most with a hooded watcher standing black against the light
            foreach (var o in _openings)
            {
                if (o.grp < 0) continue;
                var db = o.grp == 0 ? _doorsE : o.grp == 1 ? _doorsL : _doorsV[o.grp - 2]; float h = TierH(o.k);
                // a pointed doorway of warm light, hottest low in its middle and dimming toward its frame (a lamp somewhere
                // inside), never a flat card; dim enough to stay amber rather than blow out to cream
                float V = o.grp >= 2 ? Mathf.Lerp(0.5f, 0.72f, Rn()) : Mathf.Lerp(0.36f, 0.58f, Rn());
                var hot = new Color(1f, 0.7f, 0.42f); var warm = new Color(1f, 0.56f, 0.24f);
                var n = -Dir(o.th); var t = Dir(o.th + 90f); var up = Vector3.up;
                var o0 = P(o.th, BackR - 0.05f, h);
                Vector3 D2(float x, float y) => o0 + t * x + up * y;
                _arch.Clear(); ArchPts(_arch, 0.5f, 1.45f, 2.2f, 4);
                var ctr = D2(0f, 0.75f);
                var outline = new List<Vector2> { new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f) };
                for (int j = _arch.Count - 1; j >= 0; j--) outline.Add(_arch[j]);
                for (int j = 0; j < outline.Count; j++)
                {
                    var a = outline[j]; var b = outline[(j + 1) % outline.Count];
                    float va = V * (a.y < 0.05f ? 0.72f : Mathf.Lerp(0.55f, 0.3f, a.y / 2.2f)), vb = V * (b.y < 0.05f ? 0.72f : Mathf.Lerp(0.55f, 0.3f, b.y / 2.2f));
                    db.Tri(ctr, D2(a.x, a.y), D2(b.x, b.y), hot, warm, warm, V, va, vb, n);
                }
                // the light spills a little onto the floor of the recess, fading toward the gallery
                db.Quad(P(o.th, BackR - 0.06f, h + 0.01f) - t * 0.55f, P(o.th, WallR + 0.1f, h + 0.01f) - t * 0.9f, P(o.th, WallR + 0.1f, h + 0.01f) + t * 0.9f, P(o.th, BackR - 0.06f, h + 0.01f) + t * 0.55f,
                        warm, warm, warm, warm, V * 0.3f, 0.02f, 0.02f, V * 0.3f, Vector3.up);
                if (Rn() < (o.grp >= 2 ? 0.4f : 0.65f)) Watcher(o.th + (Rn() - 0.5f) * 0.6f, 38.9f, h, new[] { 0, 1, 4, 0, 3 }[_rnd.Next(5)], 0);
            }
        }

        const int SpiralSteps = 6;
        float _spiral0;
        /// <summary>The verdict's spiral of lit doorways: its angle at height h (one turn every 60 m, clear of the lancet).</summary>
        float SpiralTh(float h) => _spiral0 + h * 6f;

        /// <summary>Plain wall (vertical, facing the axis) over a θ range, in rows.</summary>
        void WallBand(float h0, float h1, float a0, float a1)
        {
            for (float t0 = a0; t0 < a1 - 0.01f; t0 += 7.5f)
            {
                float t1 = Mathf.Min(a1, t0 + 7.5f); var mb = SB((t0 + t1) * 0.5f, (h0 + h1) * 0.5f);
                Stone(mb, AStone, 1f, StoneHue);
                int rows = Mathf.Max(1, Mathf.CeilToInt((h1 - h0) / 8f));
                for (int r = 0; r < rows; r++) LatheFace(mb, WallR, Mathf.Lerp(h0, h1, (r + 1f) / rows), WallR, Mathf.Lerp(h0, h1, r / (float)rows), t0, t1, 1);
            }
        }

        /// <summary>Point on the wall cylinder of radius r at arc offset x (m, at r 38) from θc.</summary>
        Vector3 WP(float thc, float x, float r, float h) => P(thc + x / WallR * Mathf.Rad2Deg, r, h);

        static void ArchPts(List<Vector2> pts, float a, float spring, float apex, int nSide)
        {
            // a pointed arch from (-a, spring) through (0, apex) to (a, spring): two circular arcs
            float rise = Mathf.Max(a * 1.001f, apex - spring); float cx = (rise * rise - a * a) / (2f * a); float rho = cx + a;
            float angApex = Mathf.Atan2(rise, -cx);
            pts.Clear();
            for (int j = 0; j <= nSide; j++) { float ang = Mathf.Lerp(Mathf.PI, angApex, j / (float)nSide); pts.Add(new Vector2(cx + rho * Mathf.Cos(ang), spring + rho * Mathf.Sin(ang))); }
            for (int j = nSide - 1; j >= 0; j--) { var q = pts[j]; pts.Add(new Vector2(-q.x, q.y)); }
        }
        readonly List<Vector2> _arch = new List<Vector2>();

        /// <summary>Clip a polygon to a convex, counter-clockwise region (Sutherland-Hodgman); the result replaces poly.</summary>
        static void ClipConvex(List<Vector2> poly, List<Vector2> region, List<Vector2> tmp)
        {
            for (int e = 0; e < region.Count && poly.Count > 0; e++)
            {
                var a = region[e]; var ab = region[(e + 1) % region.Count] - a;
                if (ab.sqrMagnitude < 1e-10f) continue;
                tmp.Clear();
                for (int i = 0; i < poly.Count; i++)
                {
                    var p = poly[i]; var q = poly[(i + 1) % poly.Count];
                    float dp = ab.x * (p.y - a.y) - ab.y * (p.x - a.x), dq = ab.x * (q.y - a.y) - ab.y * (q.x - a.x);
                    if (dp >= 0f) tmp.Add(p);
                    if ((dp >= 0f) != (dq >= 0f)) tmp.Add(p + (q - p) * (dp / (dp - dq)));
                }
                poly.Clear(); poly.AddRange(tmp);
            }
        }

        void OpeningBay(MeshBuilder mb, float th, float bw, float h, float top, float w, float spring, float apex, float back, float bn = 1f)
        {
            float W = WallR * bw * Mathf.Deg2Rad, a = w * 0.5f; var inward = -Dir(th);
            ArchPts(_arch, a, spring, apex, 2);
            Stone(mb, AStone, bn, StoneHue);
            // wall face: two jambs' piers and the strips above the arch
            mb.QuadAuto(WP(th, -W / 2, WallR, h), WP(th, -W / 2, WallR, top), WP(th, -a, WallR, top), WP(th, -a, WallR, h), inward);
            mb.QuadAuto(WP(th, a, WallR, h), WP(th, a, WallR, top), WP(th, W / 2, WallR, top), WP(th, W / 2, WallR, h), inward);
            for (int j = 0; j < _arch.Count - 1; j++)
            {
                var p = _arch[j]; var q = _arch[j + 1];
                mb.QuadAuto(WP(th, p.x, WallR, p.y), WP(th, p.x, WallR, top), WP(th, q.x, WallR, top), WP(th, q.x, WallR, q.y), inward);
            }
            // the recess: back, jambs, soffit, floor
            Stone(mb, AStone * 0.1f, bn, StoneHue);
            mb.QuadAuto(WP(th, -a, back, h), WP(th, -a, back, spring), WP(th, a, back, spring), WP(th, a, back, h), inward);
            for (int j = 0; j < _arch.Count - 1; j++) mb.TriAuto(WP(th, 0, back, spring), WP(th, _arch[j].x, back, _arch[j].y), WP(th, _arch[j + 1].x, back, _arch[j + 1].y), inward);
            Stone(mb, AStone * 0.2f, bn, StoneHue);
            var tg = Dir(th + 90f);
            mb.QuadAuto(WP(th, -a, WallR, h), WP(th, -a, WallR, spring), WP(th, -a, back, spring), WP(th, -a, back, h), tg);
            mb.QuadAuto(WP(th, a, WallR, h), WP(th, a, WallR, spring), WP(th, a, back, spring), WP(th, a, back, h), -tg);
            for (int j = 0; j < _arch.Count - 1; j++)
            {
                var p = _arch[j]; var q = _arch[j + 1]; var mid = WP(th, (p.x + q.x) * 0.5f, WallR, (p.y + q.y) * 0.5f);
                mb.QuadAuto(WP(th, p.x, WallR, p.y), WP(th, q.x, WallR, q.y), WP(th, q.x, back, q.y), WP(th, p.x, back, p.y), WP(th, 0, WallR, spring) - mid);
            }
            Stone(mb, AStone * 0.5f, bn, StoneHue);
            mb.QuadAuto(WP(th, -a, WallR, h), WP(th, -a, back, h), WP(th, a, back, h), WP(th, a, WallR, h), Vector3.up);
        }

        void FlatBay(MeshBuilder mb, float th, float bw, float h, float top, float w, float spring, float apex, float bn = 1f)
        {
            float W = WallR * bw * Mathf.Deg2Rad, a = w * 0.5f; var inward = -Dir(th);
            Stone(mb, AStone, bn, StoneHue);
            mb.QuadAuto(WP(th, -W / 2, WallR, h), WP(th, -W / 2, WallR, top), WP(th, W / 2, WallR, top), WP(th, W / 2, WallR, h), inward);
            Stone(mb, AStone * 0.1f, bn, StoneHue);
            float r = WallR - 0.04f;
            mb.QuadAuto(WP(th, -a, r, h), WP(th, -a, r, spring), WP(th, a, r, spring), WP(th, a, r, h), inward);
            mb.TriAuto(WP(th, -a, r, spring), WP(th, 0, r, apex), WP(th, a, r, spring), inward);
        }

        // ---- the Judge's Lancet ------------------------------------------------------------------------------------------
        void Lancet()
        {
            var mb = _stoneFar[0];
            float hBot = TierH(-1) - 0.9f, hTop = TierTop(15), spring = 62f;
            float[] rr = { WallR, 38.5f, 39.0f, 39.5f };
            float[] half = { 7.0f, 6.4f, 5.8f, 5.8f };        // arc half-widths at r 38 (m)
            float[] apexes = { 74.9f, 73.6f, 72.4f, 72.4f };
            var outl = new List<Vector2>[4];
            for (int i = 0; i < 4; i++) { var l = new List<Vector2>(); ArchPts(l, half[i], spring, apexes[i], 5); outl[i] = l; }
            // the wall face around the first order
            float Z = 11.25f * Mathf.Deg2Rad * WallR; var inward = -_jd;
            Stone(mb, AStone, 1f, StoneHue);
            int rows = 8;
            for (int r = 0; r < rows; r++)
            {
                float y0 = Mathf.Lerp(hBot, hTop, r / (float)rows), y1 = Mathf.Lerp(hBot, hTop, (r + 1f) / rows);
                if (y0 < spring) { float yy1 = Mathf.Min(y1, spring); mb.QuadAuto(WP(0, -Z, WallR, y0), WP(0, -Z, WallR, yy1), WP(0, -half[0], WallR, yy1), WP(0, -half[0], WallR, y0), inward); mb.QuadAuto(WP(0, half[0], WallR, y0), WP(0, half[0], WallR, yy1), WP(0, Z, WallR, yy1), WP(0, Z, WallR, y0), inward); }
            }
            {
                var o = outl[0];
                mb.QuadAuto(WP(0, -Z, WallR, spring), WP(0, -Z, WallR, hTop), WP(0, o[0].x, WallR, hTop), WP(0, o[0].x, WallR, spring), inward);
                mb.QuadAuto(WP(0, o[o.Count - 1].x, WallR, spring), WP(0, o[o.Count - 1].x, WallR, hTop), WP(0, Z, WallR, hTop), WP(0, Z, WallR, spring), inward);
                for (int j = 0; j < o.Count - 1; j++) mb.QuadAuto(WP(0, o[j].x, WallR, o[j].y), WP(0, o[j].x, WallR, hTop), WP(0, o[j + 1].x, WallR, hTop), WP(0, o[j + 1].x, WallR, o[j + 1].y), inward);
            }
            // three stepped orders: reveal of order i (r_i -> r_{i+1}) and the step face at r_{i+1} between outline i and i+1
            for (int i = 0; i < 3; i++)
            {
                var o = outl[i]; var q = outl[i + 1];
                Stone(mb, AStone * 0.35f, 1f, StoneHue);
                // jambs
                foreach (int s in new[] { -1, 1 })
                {
                    float x = s * half[i]; var tg = Dir(90f) * -s;
                    for (int r = 0; r < 4; r++)
                    {
                        float y0 = Mathf.Lerp(hBot, spring, r / 4f), y1 = Mathf.Lerp(hBot, spring, (r + 1) / 4f);
                        mb.QuadAuto(WP(0, x, rr[i], y0), WP(0, x, rr[i], y1), WP(0, x, rr[i + 1], y1), WP(0, x, rr[i + 1], y0), tg);
                    }
                }
                for (int j = 0; j < o.Count - 1; j++)
                {
                    var a = o[j]; var b = o[j + 1]; var mid = WP(0, (a.x + b.x) * 0.5f, rr[i], (a.y + b.y) * 0.5f);
                    mb.QuadAuto(WP(0, a.x, rr[i], a.y), WP(0, b.x, rr[i], b.y), WP(0, b.x, rr[i + 1], b.y), WP(0, a.x, rr[i + 1], a.y), WP(0, 0, rr[i], spring) - mid);
                }
                if (i < 2)
                {
                    Stone(mb, AStone * 0.5f, 1f, StoneHue);
                    foreach (int s in new[] { -1, 1 })
                        for (int r = 0; r < 4; r++)
                        {
                            float y0 = Mathf.Lerp(hBot, spring, r / 4f), y1 = Mathf.Lerp(hBot, spring, (r + 1) / 4f);
                            mb.QuadAuto(WP(0, s * half[i], rr[i + 1], y0), WP(0, s * half[i], rr[i + 1], y1), WP(0, s * half[i + 1], rr[i + 1], y1), WP(0, s * half[i + 1], rr[i + 1], y0), inward);
                        }
                    for (int j = 0; j < o.Count - 1; j++)
                        mb.QuadAuto(WP(0, o[j].x, rr[i + 1], o[j].y), WP(0, o[j + 1].x, rr[i + 1], o[j + 1].y), WP(0, q[j + 1].x, rr[i + 1], q[j + 1].y), WP(0, q[j].x, rr[i + 1], q[j].y), inward);
                }
            }
            // sill
            Stone(mb, AStone * 1.2f, 1f, StoneHue);
            mb.QuadAuto(WP(0, -half[0], WallR, hBot), WP(0, -half[0], 39.5f, hBot), WP(0, half[0], 39.5f, hBot), WP(0, half[0], WallR, hBot), Vector3.up);
            // the glass: cold grisaille in diamond quarries (the lead is the gap between them), no colour. Four lights between
            // the mullions, cut by transoms into tiers, each tier ending under its transom in a cusped pointed head. Brighter
            // high, darker low; shadowed along the mullions and under each transom; every quarry a little different, a few dark.
            var fr = Quaternion.LookRotation(-_jd);
            float gw = half[3]; const float MW = 0.5f, TH = 0.4f;
            var lightsX = new[] { (-gw, -3f - MW / 2), (-3f + MW / 2, -MW / 2), (MW / 2, 3f - MW / 2), (3f + MW / 2, gw) };
            var transoms = new List<float>(); for (float y = hBot + 13.5f; y < spring - 6f; y += 13.5f) transoms.Add(y);
            var gq = new VQ { Mode = 1f };
            var grey = new Color(0.80f, 0.83f, 0.86f); var green = new Color(0.78f, 0.84f, 0.80f); var blue = new Color(0.77f, 0.81f, 0.90f);
            var poly = new List<Vector2>(); var clip = new List<Vector2>(); var tmp = new List<Vector2>(); var arch = new List<Vector2>();
            float GlassV(float x, float y, float yTop, bool underTransom)
            {
                float v = Mathf.Lerp(0.062f, 0.13f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-6f, 55f, y)));
                float ds = Mathf.Min(Mathf.Abs(Mathf.Abs(x) - gw), Mathf.Min(Mathf.Abs(Mathf.Abs(x) - (3f + MW / 2)), Mathf.Min(Mathf.Abs(Mathf.Abs(x) - (3f - MW / 2)), Mathf.Abs(Mathf.Abs(x) - MW / 2))));
                v *= Mathf.Lerp(0.62f, 1f, Mathf.SmoothStep(0f, 1f, ds / 0.55f));                                   // the mullions' shadow
                if (underTransom) v *= Mathf.Lerp(0.68f, 1f, Mathf.SmoothStep(0f, 1f, (yTop - y) / 1.4f));         // and the transom's
                return v;
            }
            void Fill(List<Vector2> region, float yTop, bool underTransom, int seed)
            {
                // diamond quarries 0.95 x 1.45 m clipped to the (convex) region
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (var p in region) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
                const float DW = 0.95f, DH = 1.45f, Shrink = 0.93f;
                int j0 = Mathf.FloorToInt((minY - hBot) / (DH * 0.5f)) - 1, j1 = Mathf.CeilToInt((maxY - hBot) / (DH * 0.5f)) + 1;
                for (int j = j0; j <= j1; j++)
                {
                    float cy = hBot + j * DH * 0.5f, off = (j & 1) == 0 ? 0f : DW * 0.5f;
                    int i0 = Mathf.FloorToInt((minX - off) / DW) - 1, i1 = Mathf.CeilToInt((maxX - off) / DW) + 1;
                    for (int i = i0; i <= i1; i++)
                    {
                        float cx = off + i * DW;
                        poly.Clear();
                        poly.Add(new Vector2(cx, cy - DH * 0.5f * Shrink)); poly.Add(new Vector2(cx + DW * 0.5f * Shrink, cy));
                        poly.Add(new Vector2(cx, cy + DH * 0.5f * Shrink)); poly.Add(new Vector2(cx - DW * 0.5f * Shrink, cy));
                        ClipConvex(poly, region, tmp); if (poly.Count < 3) continue;
                        float hsh = Hash(i * 131 + j * 7919 + seed * 31, 5), tint = Hash(i * 17 + j * 311 + seed, 9), odd = Hash(i * 53 + j * 97 + seed * 7, 13);
                        float k = odd < 0.06f ? 0.18f : odd > 0.97f ? 1.35f : 0.78f + 0.44f * hsh;
                        var col = Color.Lerp(grey, tint < 0.5f ? green : blue, Mathf.Abs(tint - 0.5f) * 0.9f);
                        var p0 = poly[0]; float v0 = GlassV(p0.x, p0.y, yTop, underTransom) * k;
                        for (int t = 1; t + 1 < poly.Count; t++)
                        {
                            var p1 = poly[t]; var p2 = poly[t + 1];
                            gq.Tri(WP(0, p0.x, 39.5f, p0.y), WP(0, p1.x, 39.5f, p1.y), WP(0, p2.x, 39.5f, p2.y), col, col, col,
                                  v0, GlassV(p1.x, p1.y, yTop, underTransom) * k, GlassV(p2.x, p2.y, yTop, underTransom) * k, inward);
                        }
                    }
                }
            }
            Stone(mb, AStone * 0.1f, 1f, StoneHue);
            for (int li = 0; li < 4; li++)
            {
                var (xL, xR) = lightsX[li]; float xc = (xL + xR) * 0.5f, a = (xR - xL) * 0.5f;
                for (int ti = 0; ti <= transoms.Count; ti++)
                {
                    float yLow = ti == 0 ? hBot : transoms[ti - 1] + TH * 0.5f;
                    bool top = ti == transoms.Count;
                    float yHigh = top ? spring : transoms[ti] - TH * 0.5f;
                    clip.Clear();
                    if (top) { clip.Add(new Vector2(xL, yLow)); clip.Add(new Vector2(xR, yLow)); clip.Add(new Vector2(xR, yHigh)); clip.Add(new Vector2(xL, yHigh)); Fill(clip, yHigh, false, li * 10 + ti); continue; }
                    // a cusped pointed head under the transom
                    float apex = yHigh - 0.1f, sp = apex - Mathf.Max(a * 1.25f, 1.9f);
                    ArchPts(arch, a, sp, apex, 7);
                    clip.Add(new Vector2(xL, yLow)); clip.Add(new Vector2(xR, yLow));
                    for (int j = arch.Count - 1; j >= 0; j--) clip.Add(new Vector2(xc + arch[j].x, arch[j].y));
                    Fill(clip, yHigh, true, li * 10 + ti);
                    // stone spandrels between the head and the transom, and two cusps on the intrados (a trefoil head)
                    int mid = arch.Count / 2;
                    for (int j = 0; j < mid; j++) mb.TriAuto(WP(0, xL, 39.36f, yHigh), WP(0, xc + arch[j].x, 39.36f, arch[j].y), WP(0, xc + arch[j + 1].x, 39.36f, arch[j + 1].y), inward);
                    for (int j = mid; j < arch.Count - 1; j++) mb.TriAuto(WP(0, xR, 39.36f, yHigh), WP(0, xc + arch[j].x, 39.36f, arch[j].y), WP(0, xc + arch[j + 1].x, 39.36f, arch[j + 1].y), inward);
                    mb.TriAuto(WP(0, xL, 39.36f, yHigh), WP(0, xc, 39.36f, apex), WP(0, xR, 39.36f, yHigh), inward);
                    foreach (int side in new[] { 2, arch.Count - 5 })
                    {
                        var e0 = arch[side]; var e1 = arch[side + 2]; var m = (e0 + e1) * 0.5f;
                        var toIn = (new Vector2(0f, sp + (apex - sp) * 0.25f) - m).normalized;
                        var tip = m + toIn * 0.34f;
                        mb.TriAuto(WP(0, xc + e0.x, 39.34f, e0.y), WP(0, xc + tip.x, 39.34f, tip.y), WP(0, xc + e1.x, 39.34f, e1.y), inward);
                    }
                }
            }
            {
                // the great head over the four lights
                clip.Clear(); var head = outl[3];
                for (int j = head.Count - 1; j >= 0; j--) clip.Add(head[j]);   // right -> apex -> left: counter-clockwise with the springing line
                Fill(clip, 80f, false, 99);
            }
            { var gm = gq.ToMesh("CourtLancetGlass"); BakeVoid(gm, Vector3.zero); PutMesh(gm, "CourtLancetGlass", _wellRoot, _mVoid); }
            // tracery: dark stone silhouettes against the glass (deep mullions, transoms, two pointed sub-arches and a roundel)
            Stone(mb, AStone * 0.12f, 1f, StoneHue);
            foreach (float x in new[] { -3f, 0f, 3f })
            {
                mb.Push(Matrix4x4.TRS(WP(0, x, 39.15f, 0f), fr, Vector3.one));
                mb.Box(new Vector3(0, (hBot + spring) * 0.5f, 0), new Vector3(MW, spring - hBot, 0.7f), MeshBuilder.Faces.PZ | MeshBuilder.Faces.PX | MeshBuilder.Faces.NX);
                mb.Box(new Vector3(0, (hBot + spring) * 0.5f, 0.4f), new Vector3(MW * 0.45f, spring - hBot, 0.12f), MeshBuilder.Faces.PZ | MeshBuilder.Faces.PX | MeshBuilder.Faces.NX);   // a fillet down the face
                mb.Pop();
            }
            foreach (float y in transoms)
            {
                mb.Push(Matrix4x4.TRS(WP(0, 0, 39.2f, y), fr, Vector3.one));
                mb.Box(Vector3.zero, new Vector3(half[3] * 2f, TH, 0.6f), MeshBuilder.Faces.PZ | MeshBuilder.Faces.PY | MeshBuilder.Faces.NY); mb.Pop();
            }
            {
                var sub = new List<Vector2>();
                foreach (float s in new[] { -1f, 1f })
                {
                    ArchPts(sub, half[3] * 0.5f, spring, spring + 4.5f, 5);
                    var pts = new List<Vector3>();
                    foreach (var p in sub) pts.Add(WP(0, p.x + s * half[3] * 0.5f, 39.3f, p.y));
                    mb.Tube(pts, 0.35f, 5);
                }
                mb.Push(Matrix4x4.TRS(WP(0, 0, 39.3f, 69.0f), fr * Quaternion.Euler(90f, 0, 0), Vector3.one)); mb.Torus(Vector3.zero, 1.9f, 0.35f, 24, 5); mb.Pop();
            }
        }

        void Closure()
        {
            var mb = _closure;
            Stone(mb, AStone * 0.6f, 1f, StoneHue);
            mb.Push(_c + Vector3.up * Abyss, 0); mb.Disc(Vector3.zero, WallR + 0.5f, 48, true); mb.Pop();
            Stone(mb, AStone * 0.7f, 1f, StoneHue);
            LatheArc(mb, new[] { new Vector2(29.5f, Top), new Vector2(WallR + 0.5f, Top) }, 0f, 360f, 48);    // cap underside (faces down)
            Stone(mb, AStone * 0.9f, 1f, StoneHue);
            LatheArc(mb, new[] { new Vector2(28.5f, 120f), new Vector2(27f, 119.6f), new Vector2(26.6f, 118.4f), new Vector2(27f, 117.2f), new Vector2(28f, 116.8f), new Vector2(29.5f, Top) }, 0f, 360f, 48);
            // the rose tracery under the Sun: dark stone lattice (fogged at this height); the band r 8..12.5 stays open
            var rb = _rose;
            Stone(rb, AStone * 0.5f, 1f, StoneHue);
            rb.Push(_c + Vector3.up * 117.4f, 0); rb.Torus(Vector3.zero, 12.5f, 0.7f, 48, 6); rb.Torus(Vector3.zero, 20f, 0.8f, 64, 6); rb.Pop();
            for (int k = 0; k < 24; k++)
            {
                float th = 15f * k; var q = Quaternion.LookRotation(Dir(th));
                rb.Push(Matrix4x4.TRS(P(th, (12.5f + 26.8f) * 0.5f, 117.5f), q, Vector3.one)); rb.Box(Vector3.zero, new Vector3(1.1f, 1.4f, 26.8f - 12.5f)); rb.Pop();
                // cusped foils in every cell: a quatrefoil between the rings, a trefoil (one lobe outward) in the outer band
                Foil(rb, th + 7.5f, 16.25f, 1.75f, 4, 0.28f, 117.3f);
                Foil(rb, th + 7.5f, 23.4f, 2.35f, 3, 0.3f, 117.2f);
            }
        }

        /// <summary>A cusped foil (n lobes) lying flat at height h: r(φ) = R (0.62 + 0.38 |cos(nφ/2)|), the cusps pointing in.</summary>
        void Foil(MeshBuilder mb, float th, float rc, float R, int n, float tube, float h)
        {
            var c = P(th, rc, h); var e1 = Dir(th); var e2 = Dir(th + 90f);
            var pts = new List<Vector3>(); const int N = 24;
            for (int i = 0; i <= N; i++)
            {
                float ph = i / (float)N * Mathf.PI * 2f; float r = R * (0.62f + 0.38f * Mathf.Abs(Mathf.Cos(n * ph * 0.5f)));
                pts.Add(c + (e1 * Mathf.Cos(ph) + e2 * Mathf.Sin(ph)) * r);
            }
            mb.Tube(pts, tube, 4);
        }

        // ---- watchers (the rulers of scale) ----------------------------------------------------------------------------
        int _watchers;
        /// <summary>A hooded far watcher at (θ, r, h) (feet). pose 0 standing, 1 kneeling, 2 seated on the rail, 3 leaning, 4 bowed.
        /// Two variants: gazing up at the Sun (faceless) and turned to the court (a pale face). inverted = hanging head-down.</summary>
        void Watcher(float th, float r, float h, int pose, int band, bool inverted = false, float scale = 1f)
        {
            if (_watchers >= 300) return; _watchers++;
            int s = Sec(th); var feet = P(th, r, h);
            var toC = _c - feet; toC.y = 0; var yaw = Quaternion.LookRotation(toC.normalized);
            var faceTarget = _c + Vector3.up * 1.5f;
            float noise = 0.8f + Rn() * 0.4f;
            for (int variant = 0; variant < 2; variant++)
            {
                var mb = variant == 0 ? _wUp[s] : _wCourt[s];
                var root = Matrix4x4.TRS(feet, inverted ? yaw * Quaternion.Euler(0, 0, 180f) : yaw, Vector3.one * scale);
                mb.Push(root);
                mb.Set(S.Glow, Color.white, new Vector4(0.004f * noise, 2f, 1f, 9f));   // black, rim-lit in the bake
                float lean = pose == 3 ? 20f : pose == 4 ? 12f : 0f; float baseY = pose == 2 ? 1.1f : 0f;
                mb.Push(Matrix4x4.TRS(new Vector3(0, baseY, 0), Quaternion.Euler(lean, 0, 0), Vector3.one));
                Vector3 bodyC, bodyR, hood;
                switch (pose)
                {
                    case 1: bodyC = new Vector3(0, 0.42f, 0); bodyR = new Vector3(0.26f, 0.42f, 0.22f); hood = new Vector3(0, 0.9f, 0.05f); break;
                    case 2: bodyC = new Vector3(0, 0.36f, -0.04f); bodyR = new Vector3(0.24f, 0.4f, 0.2f); hood = new Vector3(0, 0.84f, 0f); break;
                    case 4: bodyC = new Vector3(0, 0.6f, 0); bodyR = new Vector3(0.24f, 0.58f, 0.19f); hood = new Vector3(0, 1.08f, 0.2f); break;
                    default: bodyC = new Vector3(0, 0.62f, 0); bodyR = new Vector3(0.24f, 0.6f, 0.18f); hood = new Vector3(0, 1.22f, 0.02f); break;
                }
                mb.Ellipsoid(bodyC, bodyR, 6, 4);
                Quaternion hq;
                if (variant == 0) hq = Quaternion.Euler(-35f, 0, 0);
                else
                {
                    // look at the court floor: pitch toward the target
                    var hw = mb.M.MultiplyPoint3x4(hood); var d = faceTarget - hw; float pitch = Mathf.Atan2(-d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                    hq = Quaternion.Euler(Mathf.Clamp(pitch - lean, -40f, 60f) * (inverted ? -1f : 1f), 0, 0);
                }
                mb.Push(Matrix4x4.TRS(hood, hq, Vector3.one));
                mb.Ellipsoid(Vector3.zero, new Vector3(0.15f, 0.19f, 0.16f), 6, 4);
                if (variant == 1)
                {
                    Lum(mb, 0.14f, Bone);   // pale enough to be found in a wide (a few specks that were not there before), never a lamp
                    var fz = new Vector3(0, -0.02f, 0.158f);
                    mb.QuadAuto(fz + new Vector3(-0.08f, -0.11f, 0), fz + new Vector3(-0.08f, 0.11f, 0), fz + new Vector3(0.08f, 0.11f, 0), fz + new Vector3(0.08f, -0.11f, 0), Vector3.forward);
                    // the eye pair for the wave bands lives on this face
                    var e = mb.M.MultiplyPoint3x4(fz + new Vector3(0, 0.03f, 0.01f)); var side = mb.M.MultiplyVector(Vector3.right).normalized;
                    EyePair(band, e, side, 1f, 0.3f * scale);
                }
                mb.Pop(); mb.Pop(); mb.Pop();
            }
        }

        /// <summary>The eye band a height belongs to: the wave climbs A (below 50 m), B, C, D (to 97 m).</summary>
        static int BandOf(float h) => h < 50f ? 0 : h < 68f ? 1 : h < 82f ? 2 : h < 97f ? 3 : -1;

        /// <param name="headW">width of the head the eyes sit in (0 = no head: far eyes, spaced wide enough to read as a pair)</param>
        void EyePair(int band, Vector3 at, Vector3 side, float f, float headW = 0f)
        {
            if (band < 0 || band > 3) return;
            var mb = _band[band]; float d = Vector3.Distance(at, _c + Vector3.up * 2f);
            // sized by distance (a core of about 3 px even at 90 m) and set far enough apart to read as a pair, never a lamp
            float s = headW > 0f ? Mathf.Max(0.12f, 0.0055f * d) : Mathf.Max(0.16f, 0.0085f * d);
            float val = (0.55f + 0.6f * Mathf.Exp(-(d / 90f) * (d / 90f))) * (0.8f + 0.4f * Rn());
            var col = Color.Lerp(new Color(1f, 0.8f, 0.52f), new Color(0.96f, 0.9f, 0.8f), Rn() * 0.6f);
            mb.Set(S.Halo, col, new Vector4(val, 0.05f, 0f, 9f));
            float sp = Mathf.Max(0.095f * f, 0.0095f * d) * 0.5f; if (headW > 0f) sp = Mathf.Min(sp, headW * 0.31f);   // on a head they stay inside its outline
            MansionView.HaloQuad(mb, at - side * sp, s); MansionView.HaloQuad(mb, at + side * sp, s);
            _eyes++;
        }
        int _eyes;

        /// <summary>Something peering over a gallery's balustrade: a black hood just above the coping (near galleries only) and a
        /// pair of eyes in the wave band of that height. Unseen until the well wakes.</summary>
        void Peer(float th, int k)
        {
            float h = TierH(k), f = TierF(k); var top = P(th, 36.45f, h + 1.12f * f);
            var toC = _c - top; toC.y = 0f; toC.Normalize(); var side = Vector3.Cross(Vector3.up, toC);
            float hs = Mathf.Lerp(0.9f, 1.15f, Rn()) * Mathf.Max(0.55f, f);
            var head = top + Vector3.up * (0.1f * hs);
            if (k <= 9)
            {
                var mb = _iron[Sec(th)];
                mb.Set(S.Glow, Color.white, new Vector4(0.004f, 2f, 1f, 9f));
                mb.Push(Matrix4x4.TRS(head, Quaternion.LookRotation(toC) * Quaternion.Euler(Rn() * 16f - 4f, (Rn() - 0.5f) * 30f, (Rn() - 0.5f) * 14f), Vector3.one * hs));
                mb.Ellipsoid(Vector3.zero, new Vector3(0.17f, 0.21f, 0.18f), 6, 4); mb.Pop();
            }
            EyePair(BandOf(h), head + toC * 0.19f * hs + Vector3.up * 0.02f, side, f, k <= 9 ? 0.34f * hs : 0f);
        }

        void Rulers()
        {
            // balustrade watchers on the near galleries (tiers -6..9): 5 poses, never all alike
            for (int k = -6; k <= 9; k++)
            {
                float h = TierH(k);
                for (int b = 0; b < 48; b++)
                {
                    float th = b * 7.5f; if (InLancet(th, k)) continue;
                    if (k == 7 && Mathf.Repeat(th, 360f) > 258f && Mathf.Repeat(th, 360f) < 302f) continue;
                    if (Rn() > 0.14f) continue;
                    int n = Rn() < 0.35f ? 2 : 1;
                    for (int j = 0; j < n; j++)
                    {
                        float tj = th + (Rn() - 0.5f) * 5f; int pose = Rn() < 0.35f ? 2 : _rnd.Next(5);
                        float r = pose == 2 ? 36.48f : 36.7f + Rn() * 0.12f;   // hard against the rail: from the floor the rail hides anything further back
                        Watcher(tj, r, h, pose, k >= -3 ? 0 : -1, false, pose == 2 ? 1.1f : 1.3f);
                    }
                }
            }
            // the inverted balcony (tier 11, θ 120..150): four watchers hanging head-down under the slab
            {
                float hb = TierH(11) - 0.9f * TierF(11);
                for (int j = 0; j < 4; j++) Watcher(123f + j * 7.5f + (Rn() - 0.5f) * 3f, 37.0f, hb, j % 2 == 0 ? 0 : 4, 2, true);
            }
            // eyes over the balustrades, every gallery up to 96 m, in uneven clusters (denser low, where the watchers are)
            foreach (var (th, k) in _rails)
            {
                if (k < -4) continue;
                float p = k <= 9 ? 0.42f : 0.3f; if (Rn() > p) continue;
                int n = Rn() < 0.4f ? 2 : 1;
                for (int j = 0; j < n; j++) Peer(th + (Rn() - 0.5f) * 6.2f, k);
            }
            // bridges: low (with a lantern post and a watcher), mid (two watchers), high (broken at 60 %)
            Bridge(45f, 7.5f, 19.1f, 36.3f, 1);
            Bridge(-105f, 31f, 19.1f, 36.3f, 2);
            Bridge(165f, 57f, 19.1f, 19.1f + 0.6f * 17.2f, 0, true);
            Bridge(165f, 57f, 33.2f, 36.3f, 0, true);
            // the stair to nowhere: from θ 70 (h 5.4) up and around through the lift side to θ 230 (h 65); the last tread is broken
            {
                const int N = 331; float r0 = 35.0f, r1 = 36.2f, rm = (r0 + r1) * 0.5f;
                for (int i = 0; i < N; i++)
                {
                    float th = Mathf.Lerp(70f, 230f, i / (N - 1f)), h = Mathf.Lerp(5.4f, 65f, i / (N - 1f));
                    var mb = SB(th, h);
                    Stone(mb, ABridge, 1f, StoneHue);
                    mb.Push(Matrix4x4.TRS(P(th, rm, h), Quaternion.LookRotation(Dir(th)), Vector3.one));
                    if (i == N - 1) { mb.Push(Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 9f), Vector3.one)); mb.Box(new Vector3(0, 0, -0.25f), new Vector3(0.3f, 0.05f, 0.7f)); mb.Pop(); }
                    else mb.Box(Vector3.zero, new Vector3(0.3f, 0.05f, r1 - r0));
                    Stone(mb, ABridge * 0.5f, 1f, StoneHue);
                    mb.QuadAuto(new Vector3(-0.15f, -0.18f, -0.6f), new Vector3(-0.15f, 0f, -0.6f), new Vector3(-0.15f, 0f, 0.6f), new Vector3(-0.15f, -0.18f, 0.6f), Vector3.left);
                    mb.Pop();
                    if (i % 10 == 0 && i + 10 < N)
                    {
                        float th2 = Mathf.Lerp(70f, 230f, (i + 10) / (N - 1f)), h2 = Mathf.Lerp(5.4f, 65f, (i + 10) / (N - 1f));
                        Stone(mb, ABridge * 0.7f, 1f, StoneHue);
                        mb.QuadAuto(P(th, r0 - 0.02f, h - 0.4f), P(th, r0 - 0.02f, h + 0.05f), P(th2, r0 - 0.02f, h2 + 0.05f), P(th2, r0 - 0.02f, h2 - 0.4f), -Dir((th + th2) * 0.5f));
                        Stone(mb, AIron, 1f, IronHue);
                        mb.Bar(P(th, r0 + 0.05f, h + 0.95f), P(th2, r0 + 0.05f, h2 + 0.95f), 0.1f);
                    }
                    if (i % 13 == 0)
                    {
                        Stone(mb, ABridge * 0.8f, 1f, StoneHue);
                        mb.Push(Matrix4x4.TRS(P(th, 36.9f, h - 0.5f), Quaternion.LookRotation(Dir(th)), Vector3.one)); mb.Box(Vector3.zero, new Vector3(0.6f, 0.9f, 1.6f)); mb.Pop();
                    }
                }
                // three fallen treads hang in the air below the end, perfectly still
                for (int j = 0; j < 3; j++)
                {
                    float th = 230f - j * 0.6f, h = 65f - 1.1f - j * 0.8f; var mb = SB(th, h);
                    Stone(mb, ABridge, 1f, StoneHue);
                    mb.Push(Matrix4x4.TRS(P(th + 0.4f, rm + (Rn() - 0.5f) * 0.4f, h), Quaternion.LookRotation(Dir(th)) * Quaternion.Euler((Rn() - 0.5f) * 30f, Rn() * 40f, (Rn() - 0.5f) * 30f), Vector3.one));
                    mb.Box(Vector3.zero, new Vector3(0.3f, 0.05f, 1.2f)); mb.Pop();
                }
            }
        }

        void Bridge(float th, float h, float rA, float rB, int watchers, bool broken = false)
        {
            var mb = SB(th, h); var q = Quaternion.LookRotation(Dir(th));
            mb.Push(Matrix4x4.TRS(P(th, 0f, h), q, Vector3.one));
            float len = rB - rA, zc = (rA + rB) * 0.5f;
            Stone(mb, ABridge, 1f, StoneHue);
            mb.Box(new Vector3(0, -0.3f, zc), new Vector3(2.2f, 0.6f, len));
            Stone(mb, ABridge * 0.8f, 1f, StoneHue);
            foreach (float x in new[] { -1.0f, 1.0f }) mb.Box(new Vector3(x, 0.5f, zc), new Vector3(0.18f, 1.0f, len));
            // a shallow arch under the span
            Stone(mb, ABridge * 0.6f, 1f, StoneHue);
            for (int j = 0; j < 8; j++)
            {
                float z0 = Mathf.Lerp(rA, rB, j / 8f), z1 = Mathf.Lerp(rA, rB, (j + 1) / 8f);
                float d0 = 0.6f + Mathf.Sin(Mathf.InverseLerp(rA, rB, z0) * Mathf.PI) * (broken ? 0.4f : 1.4f), d1 = 0.6f + Mathf.Sin(Mathf.InverseLerp(rA, rB, z1) * Mathf.PI) * (broken ? 0.4f : 1.4f);
                foreach (float x in new[] { -1.1f, 1.1f }) mb.QuadAuto(new Vector3(x, -0.6f, z0), new Vector3(x, -d0 - 0.6f, z0), new Vector3(x, -d1 - 0.6f, z1), new Vector3(x, -0.6f, z1), new Vector3(x, 0, 0));
            }
            if (broken)
            {
                float zEnd = rA > 30f ? rA : rB; float dir = rA > 30f ? -1f : 1f;
                Stone(mb, ABridge * 0.9f, 1f, StoneHue);
                for (int j = 0; j < 4; j++) { mb.Push(Matrix4x4.TRS(new Vector3((j - 1.5f) * 0.55f, -0.3f + (Rn() - 0.5f) * 0.3f, zEnd + dir * (0.2f + Rn() * 0.6f)), Quaternion.Euler(Rn() * 30f, Rn() * 30f, Rn() * 30f), Vector3.one)); mb.Box(Vector3.zero, new Vector3(0.5f, 0.5f, 0.6f)); mb.Pop(); }
            }
            mb.Pop();
            if (watchers == 1 && !broken)
            {
                // a lantern post at mid-span and one watcher beside it
                var lp = P(th, (rA + rB) * 0.5f, h) + Dir(th + 90f) * 0.8f;
                var im = _iron[Sec(th)]; Stone(im, AIronDark, 1f, IronHue);
                im.Rod(lp, lp + Vector3.up * 1.8f, 0.05f, 6, false);
                im.Push(lp + Vector3.up * 1.95f, 0); im.Box(Vector3.zero, new Vector3(0.28f, 0.34f, 0.28f)); im.Pop();
                Lum(im, 0.5f, DoorWarm); im.Push(lp + Vector3.up * 1.95f, 0); im.Box(Vector3.zero, new Vector3(0.2f, 0.26f, 0.3f)); im.Box(Vector3.zero, new Vector3(0.3f, 0.26f, 0.2f)); im.Pop();
                Watcher(th - 1.2f, (rA + rB) * 0.5f + 1.5f, h, 0, 0);
            }
            else if (watchers >= 2) { Watcher(th + 1.5f, rA + len * 0.3f, h, 4, 0); Watcher(th - 1.4f, rA + len * 0.62f, h, 1, 0); }
        }

        // ---- chains --------------------------------------------------------------------------------------------------
        void Chains()
        {
            float[] ths = { 30f, -30f, 150f, -150f };
            for (int i = 0; i < 4; i++)
            {
                float th = ths[i]; var pivotPos = P(th, 22f, Top);
                _chainPivot[i] = Pivot("CourtChain" + i, pivotPos);
                var mb = new MeshBuilder(); mb.Push(Matrix4x4.Translate(-pivotPos));
                Stone(mb, AIronDark, 0.9f + Rn() * 0.2f, IronHue);
                bool broken = th == -150f; float lowest = broken ? 24f : -40f;
                var axis = P(th, 22f, 0f);
                int n = 0;
                for (float h = 76f; h >= lowest; h -= 3.0f, n++)
                {
                    var q = Quaternion.Euler(0, n % 2 == 0 ? 0f : 90f, 0) * Quaternion.Euler(90f, 0, 0);
                    if (broken && h - 3f < lowest) q = q * Quaternion.Euler(0, 0, 25f);
                    mb.Push(Matrix4x4.TRS(axis + Vector3.up * h, q, new Vector3(1f, 1f, 1.6f)));
                    if (broken && h - 3f < lowest) mb.Torus(Vector3.zero, 1.15f, 0.34f, 10, 5, 40f, 330f);   // the broken open link
                    else mb.Torus(Vector3.zero, 1.15f, 0.34f, 10, 5);
                    mb.Pop();
                }
                mb.Rod(axis + Vector3.up * 78f, axis + Vector3.up * (Top + 0.5f), 0.5f, 6, false);
                if (!broken) mb.Rod(axis + Vector3.up * Abyss, axis + Vector3.up * (lowest - 1.5f), 0.5f, 6, false);
                Put(mb, "CourtChain" + i, _chainPivot[i], _mVoid, true, pivotPos);
            }
        }

        // ---- the lift tower ------------------------------------------------------------------------------------------
        /// <summary>Height of the lift tower's head above the court floor.</summary>
        const float LiftH = 46f;
        void LiftTower()
        {
            float th = _liftTh; var mb = _iron[Sec(th)];
            Stone(mb, AIronDark, 1f, IronHue);
            // the tower stops well below the upper haze (a sheave wheel on its head): the eclipse stays a clean silhouette
            float[] rs = { 10.0f, 11.6f }; float[] xs = { -1.4f, 1.4f }; float h0 = 3.3f, h1 = LiftH;
            Vector3 U(float r, float x, float h) => P(th, r, h) + Dir(th + 90f) * x;
            foreach (var r in rs) foreach (var x in xs) mb.Bar(U(r, x, h0), U(r, x, h1), 0.24f);
            int panel = 0;
            for (float h = h0 + 3.2f; h < h1; h += 3.2f, panel++)
            {
                mb.Bar(U(rs[0], -1.4f, h), U(rs[0], 1.4f, h), 0.12f); mb.Bar(U(rs[1], -1.4f, h), U(rs[1], 1.4f, h), 0.12f);
                mb.Bar(U(rs[0], -1.4f, h), U(rs[1], -1.4f, h), 0.12f); mb.Bar(U(rs[0], 1.4f, h), U(rs[1], 1.4f, h), 0.12f);
                if (panel % 2 == 0)
                    foreach (var x in xs) { mb.Bar(U(rs[0], x, h - 3.2f), U(rs[1], x, h), 0.1f); mb.Bar(U(rs[1], x, h - 3.2f), U(rs[0], x, h), 0.1f); }
            }
            // the head: a cross-beam and a great sheave wheel that the ropes run over
            {
                var hc = U(10.8f, 0f, h1 + 0.9f);
                mb.Bar(U(rs[0], 0f, h1), U(rs[1], 0f, h1), 0.3f); mb.Bar(U(rs[0], -1.4f, h1), U(rs[0], 1.4f, h1), 0.2f); mb.Bar(U(rs[1], -1.4f, h1), U(rs[1], 1.4f, h1), 0.2f);
                mb.Push(Matrix4x4.TRS(hc, Quaternion.LookRotation(Dir(th + 90f)) * Quaternion.Euler(90f, 0f, 0f), Vector3.one)); mb.Torus(Vector3.zero, 1.25f, 0.11f, 20, 4); mb.Pop();
                for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI; var d = Vector3.up * Mathf.Cos(a) + Dir(th) * Mathf.Sin(a); mb.Bar(hc - d * 1.2f, hc + d * 1.2f, 0.07f); }
            }
            // a second cage parked high up, a lantern burning inside
            {
                float ch = LiftH - 9.5f; var cc = U(10.8f, 0f, ch);
                Stone(mb, AIronDark, 1f, IronHue);
                foreach (var dx in new[] { -1.2f, 1.2f }) foreach (var dz in new[] { -0.8f, 0.8f }) mb.Bar(U(10.8f + dz, dx, ch), U(10.8f + dz, dx, ch + 3f), 0.1f);
                foreach (var y in new[] { ch, ch + 3f }) { mb.Bar(U(10.0f, -1.2f, y), U(10.0f, 1.2f, y), 0.1f); mb.Bar(U(11.6f, -1.2f, y), U(11.6f, 1.2f, y), 0.1f); mb.Bar(U(10.0f, -1.2f, y), U(11.6f, -1.2f, y), 0.1f); mb.Bar(U(10.0f, 1.2f, y), U(11.6f, 1.2f, y), 0.1f); }
                for (int i = 1; i < 6; i++) mb.Bar(U(10.0f, -1.2f + i * 0.4f, ch), U(10.0f, -1.2f + i * 0.4f, ch + 3f), 0.1f);
                Lum(mb, 0.6f, DoorWarm); mb.Push(cc + Vector3.up * 1.3f, 0); mb.Box(Vector3.zero, new Vector3(0.3f, 0.42f, 0.3f)); mb.Pop();
                Lum(mb, 0.12f, DoorWarm); mb.Push(cc + Vector3.up * 0.02f, 0); mb.Box(Vector3.zero, new Vector3(2.2f, 0.04f, 1.4f)); mb.Pop();
            }
            // the counterweight rides on two rods under its own pivot
            {
                var cp = U(10.8f, 0f, 0f);
                _counter = Pivot("CourtCounterweight", cp);
                var cw = new MeshBuilder(); cw.Push(Matrix4x4.Translate(-cp));
                Stone(cw, AIronDark, 1f, IronHue);
                cw.Push(Matrix4x4.TRS(cp, Quaternion.LookRotation(Dir(th + 90f)), Vector3.one)); cw.Box(new Vector3(0, 1.4f, 0), new Vector3(2.2f, 2.8f, 0.8f)); cw.Pop();
                foreach (var x in new[] { -0.7f, 0.7f }) cw.Rod(cp + Dir(th + 90f) * x + Vector3.up * 2.8f, cp + Dir(th + 90f) * x + Vector3.up * 16f, 0.05f, 5, false);
                Put(cw, "CourtCounterweight", _counter, _mVoid, true, cp);
                _counter.position = cp + Vector3.up * _counterH;
            }
        }

        // ---- the Black Sun ---------------------------------------------------------------------------------------------
        static float Fib(float b) => 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(23f * b + 3f * Mathf.Sin(7f * b)));
        static Vector3 Polar(float r, float b) => new Vector3(Mathf.Cos(b) * r, 0f, Mathf.Sin(b) * r);
        void Zenith3()
        {
            const int SEG = 96;
            // corona: the limb crosses the bloom threshold, everything else stays low; fog-independent (the one impossible light)
            {
                var zc = _c + Vector3.up * 118.4f; _corona = Pivot("ZenithCorona", zc);
                var q = new VQ();
                float[] rr = { 8f, 8.25f, 8.6f, 9.1f, 9.8f, 10.8f, 12.2f, 14f, 16.5f, 19f, 22f, 25f, 28f, 30f };
                float V(float r, float b) => 1.25f * Mathf.Exp(-(r - 8f) / 0.72f) + 0.22f * Fib(b) * Mathf.Exp(-(r - 8f) / 5.2f) + 0.04f * Mathf.Exp(-(r - 8f) / 25f);
                Color Hc(float r) { var c = Color.Lerp(new Color(0.9f, 0.93f, 1f), new Color(0.68f, 0.75f, 0.95f), Mathf.InverseLerp(8f, 12f, r)); c.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(26f, 30f, r)); return c; }
                for (int ri = 0; ri < rr.Length - 1; ri++)
                    for (int s = 0; s < SEG; s++)
                    {
                        float b0 = s / (float)SEG * Mathf.PI * 2f, b1 = (s + 1) / (float)SEG * Mathf.PI * 2f; float r0 = rr[ri], r1 = rr[ri + 1];
                        q.Quad(Polar(r0, b0), Polar(r0, b1), Polar(r1, b1), Polar(r1, b0), Hc(r0), Hc(r0), Hc(r1), Hc(r1), V(r0, b0), V(r0, b1), V(r1, b1), V(r1, b0), Vector3.down);
                    }
                PutMesh(q.ToMesh("ZenithCorona"), "ZenithCorona", _corona, _mCorona);
            }
            // pupil: multiplies what is behind it by ~0.015 (a true black that survives the fog), soft outer edge
            {
                var pp = _c + Vector3.up * 117.9f; var pv = Pivot("ZenithPupil", pp);
                var q = new VQ(); var w = Color.white; var clear = new Color(1, 1, 1, 0);
                for (int s = 0; s < SEG; s++)
                {
                    float b0 = s / (float)SEG * Mathf.PI * 2f, b1 = (s + 1) / (float)SEG * Mathf.PI * 2f;
                    q.Tri(Vector3.zero, Polar(7.9f, b0), Polar(7.9f, b1), w, w, w, 1f, 1f, 1f, Vector3.down);
                    q.Quad(Polar(7.9f, b0), Polar(7.9f, b1), Polar(8.1f, b1), Polar(8.1f, b0), w, w, clear, clear, 1f, 1f, 1f, 1f, Vector3.down);
                }
                PutMesh(q.ToMesh("ZenithPupil"), "ZenithPupil", pv, _mPupil);
            }
            // iris band: off at rest; when it rises the black visibly contracts from r 8 to r 6.4
            {
                var ip = _c + Vector3.up * 117.7f; _iris = Pivot("ZenithIris", ip);
                var q = new VQ(); float[] rr = { 6.4f, 6.8f, 7.2f, 7.6f, 8.0f }; var hue = new Color(0.82f, 0.86f, 1f);
                float V(float r, float b) => 1.1f * Mathf.Lerp(0.25f, 1f, (r - 6.4f) / 1.6f) * Fib(b + 0.3f);
                for (int ri = 0; ri < rr.Length - 1; ri++)
                    for (int s = 0; s < SEG; s++)
                    {
                        float b0 = s / (float)SEG * Mathf.PI * 2f, b1 = (s + 1) / (float)SEG * Mathf.PI * 2f; float r0 = rr[ri], r1 = rr[ri + 1];
                        q.Quad(Polar(r0, b0), Polar(r0, b1), Polar(r1, b1), Polar(r1, b0), hue, hue, hue, hue, V(r0, b0), V(r0, b1), V(r1, b1), V(r1, b0), Vector3.down);
                    }
                PutMesh(q.ToMesh("ZenithIris"), "ZenithIris", _iris, _mIris);
            }
        }

        // ---- the air: haze, abyss pools, slow puffs, the lancet's shafts ---------------------------------------------
        void Air()
        {
            var hue = new Color(0.72f, 0.78f, 0.9f);
            // a luminous cylinder of haze (lighter than the stone), bands at the arch springing and under the Sun
            {
                var q = new VQ();
                float[] hs = { -60f, -42f, -20f, 0f, 15f, 30f, 36f, 48f, 56f, 66f, 80f, 90f, 101f, 108f };
                float[] vs = { 0f, 0.02f, 0.02f, 0.02f, 0.02f, 0.035f, 0.045f, 0.045f, 0.028f, 0.02f, 0.035f, 0.05f, 0.05f, 0f };
                const int SEG = 40; float r = 27f; var uv = new Vector2(0.5f, 0.1f);
                for (int i = 0; i < hs.Length - 1; i++)
                    for (int s = 0; s < SEG; s++)
                    {
                        float t0 = s * 360f / SEG, t1 = (s + 1) * 360f / SEG;
                        q.Quad(P(t0, r, hs[i]), P(t0, r, hs[i + 1]), P(t1, r, hs[i + 1]), P(t1, r, hs[i]), hue, hue, hue, hue, vs[i], vs[i + 1], vs[i + 1], vs[i], -Dir((t0 + t1) * 0.5f), uv, uv, uv, uv);
                    }
                // pale pools of light far down (normals up)
                PoolRing(q, -14f, 11.2f, 37f, 0.035f, hue);
                PoolRing(q, -38f, 0.5f, 37f, 0.05f, hue);
                // and two veils high up: looking up, the galleries dissolve into light long before the Sun (kept off the axis)
                VeilRing(q, 70f, 10f, 37f, 0.016f, hue);
                VeilRing(q, 98f, 9f, 37f, 0.026f, hue);
                // a thin sheet of lit air right under the Sun (the pupil, drawn after it, keeps its black): whatever crosses
                // beneath it (the leviathan) shows as one whole silhouette instead of fragments against the dark tracery
                VeilDisc(q, 99.5f, 27f, 0.03f, hue);
                PutMesh(q.ToMesh("CourtHaze"), "CourtHaze", _wellRoot, _mHaze);
            }
            // slow puffs of brighter air, clear of the piers; three low ones lift the air behind the throne portal
            {
                var pc = _c; _puffs = Pivot("CourtPuffs", pc);
                var mb = new MeshBuilder(); mb.Push(Matrix4x4.Translate(-pc));
                for (int i = 0; i < 10; i++)
                {
                    float th = i < 3 ? (i - 1) * 20f + (Rn() - 0.5f) * 6f : Rn() * 360f; float h = i < 3 ? 8f + Rn() * 7f : 15f + Rn() * 55f;
                    float rr = 26f + Rn() * 6f; float hs = 14f + Rn() * 6f;
                    mb.Set(S.Halo, hue, new Vector4(0.03f + Rn() * 0.02f, 0f, 0f, 9f));
                    MansionView.HaloQuad(mb, P(th, rr, h), hs);
                }
                Put(mb, "CourtPuffs", _puffs, _mPuffs, false, pc);
            }
            // three pale shafts from the lancet toward the court, cut off high above the floor (never inside the ring)
            {
                var q = new VQ(); var sh = new Color(0.62f, 0.68f, 0.82f);
                for (int i = 0; i < 3; i++)
                {
                    float x = (i - 1) * 3.2f, ys = 44f + i * 8f;
                    var src = WP(0, x, 39.4f, ys); var dst = _c + Vector3.up * 6f; var end = Vector3.Lerp(src, dst, 0.55f);
                    var along = (end - src).normalized; var side = Vector3.Cross(along, Vector3.up).normalized; if (side.sqrMagnitude < 0.1f) side = _rt;
                    float w0 = 1.6f, w1 = 2.6f; var nrm = Vector3.Cross(side, along).normalized;
                    q.Quad(src - side * w0, end - side * w1, end + side * w1, src + side * w0, sh, sh, sh, sh, 0.035f, 0.035f, 0.035f, 0.035f, nrm,
                           new Vector2(0, 0), new Vector2(0, 0.9f), new Vector2(1, 0.9f), new Vector2(1, 0));
                }
                PutMesh(q.ToMesh("CourtLancetShafts"), "CourtLancetShafts", _wellRoot, _mShafts);
            }
        }

        void PoolRing(VQ q, float h, float r0, float r1, float v, Color hue)
        {
            const int SEG = 40; float[] rr = { r0, Mathf.Lerp(r0, r1, 0.35f), Mathf.Lerp(r0, r1, 0.7f), r1 };
            float[] vv = { v * 0.4f, v, v, 0f }; var uv = new Vector2(0.5f, 0.1f);
            for (int i = 0; i < rr.Length - 1; i++)
                for (int s = 0; s < SEG; s++)
                {
                    float t0 = s * 360f / SEG, t1 = (s + 1) * 360f / SEG;
                    q.Quad(P(t0, rr[i], h), P(t0, rr[i + 1], h), P(t1, rr[i + 1], h), P(t1, rr[i], h), hue, hue, hue, hue, vv[i], vv[i + 1], vv[i + 1], vv[i], Vector3.up, uv, uv, uv, uv);
                }
        }

        void VeilDisc(VQ q, float h, float r1, float v, Color hue)
        {
            const int SEG = 40; float[] rr = { 0.3f, r1 * 0.4f, r1 * 0.8f, r1 }; float[] vv = { v, v, v * 0.75f, 0f }; var uv = new Vector2(0.5f, 0.1f);   // (the ray shader needs uv.y 0.1)
            for (int i = 0; i < rr.Length - 1; i++)
                for (int s = 0; s < SEG; s++)
                {
                    float t0 = s * 360f / SEG, t1 = (s + 1) * 360f / SEG;
                    q.Quad(P(t0, rr[i], h), P(t0, rr[i + 1], h), P(t1, rr[i + 1], h), P(t1, rr[i], h), hue, hue, hue, hue, vv[i], vv[i + 1], vv[i + 1], vv[i], Vector3.down, uv, uv, uv, uv);
                }
        }

        void VeilRing(VQ q, float h, float r0, float r1, float v, Color hue)
        {
            const int SEG = 40; float[] rr = { r0, r0 + 3f, Mathf.Lerp(r0, r1, 0.55f), r1 };
            float[] vv = { 0f, v * 0.7f, v, v * 0.25f }; var uv = new Vector2(0.5f, 0.1f);
            for (int i = 0; i < rr.Length - 1; i++)
                for (int s = 0; s < SEG; s++)
                {
                    float t0 = s * 360f / SEG, t1 = (s + 1) * 360f / SEG;
                    q.Quad(P(t0, rr[i], h), P(t0, rr[i + 1], h), P(t1, rr[i + 1], h), P(t1, rr[i], h), hue, hue, hue, hue, vv[i], vv[i + 1], vv[i + 1], vv[i], Vector3.down, uv, uv, uv, uv);
                }
        }

        MeshRenderer PutMesh(Mesh mesh, string name, Transform parent, Material mat)
        {
            if (mesh == null || mesh.vertexCount == 0) return null;
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _rv.Renderers.Add(mr); _wellRends.Add(mr);
            _tris += (int)(mesh.GetIndexCount(0) / 3); _verts += mesh.vertexCount; _rends++;
            return mr;
        }

        /// <summary>A tiny mesh accumulator for quads whose corners carry their own colour and value (custom.x): the corona,
        /// the pupil's soft edge, the iris and the haze. Everything else goes through MeshBuilder.</summary>
        sealed class VQ
        {
            readonly List<Vector3> _p = new List<Vector3>(); readonly List<Vector3> _n = new List<Vector3>(); readonly List<Vector2> _uv = new List<Vector2>();
            readonly List<Color32> _c = new List<Color32>(); readonly List<Vector4> _x = new List<Vector4>(); readonly List<int> _t = new List<int>();
            /// <summary>1 = explicit values (a light or a glass: the bake keeps them and only adds the air), 0 = values to bake.</summary>
            public float Mode;
            int Add(Vector3 p, Vector3 n, Vector2 uv, Color c, float v) { _p.Add(p); _n.Add(n); _uv.Add(uv); _c.Add(c); _x.Add(new Vector4(v, 0f, Mode, 9f)); return _p.Count - 1; }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color ca, Color cb, Color cc, Color cd, float va, float vb, float vc, float vd, Vector3 want,
                             Vector2 ua = default, Vector2 ub = default, Vector2 uc = default, Vector2 ud = default)
            {
                var n = Vector3.Cross(b - a, c - a); if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(c - a, d - a);
                bool flip = Vector3.Dot(n, want) < 0f; n = (flip ? -n : n).normalized;
                int i0 = Add(a, n, ua, ca, va), i1 = Add(b, n, ub, cb, vb), i2 = Add(c, n, uc, cc, vc), i3 = Add(d, n, ud, cd, vd);
                if (!flip) { _t.Add(i0); _t.Add(i1); _t.Add(i2); _t.Add(i0); _t.Add(i2); _t.Add(i3); }
                else { _t.Add(i0); _t.Add(i2); _t.Add(i1); _t.Add(i0); _t.Add(i3); _t.Add(i2); }
            }
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc, float va, float vb, float vc, Vector3 want)
            {
                var n = Vector3.Cross(b - a, c - a); bool flip = Vector3.Dot(n, want) < 0f; n = (flip ? -n : n).normalized;
                int i0 = Add(a, n, Vector2.zero, ca, va), i1 = Add(b, n, Vector2.zero, cb, vb), i2 = Add(c, n, Vector2.zero, cc, vc);
                if (!flip) { _t.Add(i0); _t.Add(i1); _t.Add(i2); } else { _t.Add(i0); _t.Add(i2); _t.Add(i1); }
            }
            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name }; if (_p.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(_p); m.SetNormals(_n); m.SetUVs(0, _uv); m.SetColors(_c); m.SetUVs(2, _x); m.SetTriangles(_t, 0); m.RecalculateBounds();
                return m;
            }
        }
    }
}
