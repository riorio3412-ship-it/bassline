using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BL23.Game.Mansion
{
    public enum MansionMood { Normal, Grand, Mystery, WhiteOut, Rain, Water, Machine, Chapel, Courtroom }

    /// <summary>
    /// Post-processing + fog + reflections for the mansion, created in code. A base volume carries the house look
    /// (ACES, bloom, vignette, split toning magenta/cyan, grain, slight CA); a crossfading mood volume follows the room
    /// the camera is in; separate volumes for the darkness chapter, noise chapter and trial tension.
    /// </summary>
    public sealed class MansionAtmosphere : MonoBehaviour
    {
        MansionView _view;
        Volume _base, _moodA, _moodB, _dark, _noise, _tension;
        readonly Dictionary<MansionMood, VolumeProfile> _moods = new Dictionary<MansionMood, VolumeProfile>();
        MansionMood _mood = (MansionMood)(-1);
        float _fade = 1f;
        bool _aIsCurrent = true;
        Color _fogTarget = new Color(0.06f, 0.05f, 0.05f); float _fogDensityTarget = 0.018f;
        public MansionReflection Reflection { get; private set; }
        Cubemap _envCube;

        public static MansionAtmosphere Create(MansionView view)
        {
            var go = new GameObject("MansionAtmosphere");
            go.transform.SetParent(view.transform, false);
            var a = go.AddComponent<MansionAtmosphere>();
            a._view = view;
            a.Init();
            return a;
        }

        Volume NewVolume(string name, int priority, VolumeProfile p, float weight)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var v = go.AddComponent<Volume>(); v.isGlobal = true; v.priority = priority; v.sharedProfile = p; v.weight = weight;
            return v;
        }

        static T Add<T>(VolumeProfile p) where T : VolumeComponent { var c = p.Add<T>(false); c.active = true; return c; }

        void Init()
        {
            // ---- base look
            var bp = ScriptableObject.CreateInstance<VolumeProfile>(); bp.name = "MansionBase";
            var tm = Add<Tonemapping>(bp); tm.mode.Override(TonemappingMode.ACES);
            // candle-lit oil-painting look: warm highlights, cool-neutral deep shadows, restrained bloom, no colour fringing
            var bloom = Add<Bloom>(bp); bloom.threshold.Override(1.25f); bloom.intensity.Override(0.3f); bloom.scatter.Override(0.6f); bloom.tint.Override(new Color(1f, 0.9f, 0.8f)); bloom.highQualityFiltering.Override(true);
            var vig = Add<Vignette>(bp); vig.intensity.Override(0.3f); vig.smoothness.Override(0.5f); vig.color.Override(new Color(0.03f, 0.02f, 0.02f));
            var ca = Add<ChromaticAberration>(bp); ca.intensity.Override(0.015f);
            var fg = Add<FilmGrain>(bp); fg.type.Override(FilmGrainLookup.Medium3); fg.intensity.Override(0.08f); fg.response.Override(0.8f);
            var cadj = Add<ColorAdjustments>(bp); cadj.postExposure.Override(0.2f); cadj.contrast.Override(14f); cadj.saturation.Override(-4f);
            var st = Add<SplitToning>(bp); st.shadows.Override(new Color(0.44f, 0.47f, 0.52f)); st.highlights.Override(new Color(0.58f, 0.52f, 0.44f)); st.balance.Override(-5f);
            var smh = Add<ShadowsMidtonesHighlights>(bp); smh.shadows.Override(new Vector4(0.97f, 0.98f, 1.03f, -0.03f)); smh.midtones.Override(new Vector4(1.02f, 1.0f, 0.97f, 0f)); smh.highlights.Override(new Vector4(1.02f, 1f, 0.96f, 0.02f));
            _base = NewVolume("Base", 10, bp, 1f);

            // ---- moods
            foreach (MansionMood m in System.Enum.GetValues(typeof(MansionMood))) _moods[m] = MakeMood(m);
            _moodA = NewVolume("MoodA", 20, _moods[MansionMood.Normal], 1f);
            _moodB = NewVolume("MoodB", 21, _moods[MansionMood.Normal], 0f);

            // ---- darkness chapter
            var dp = ScriptableObject.CreateInstance<VolumeProfile>(); dp.name = "Darkness";
            var dca = Add<ColorAdjustments>(dp); dca.postExposure.Override(-0.55f); dca.saturation.Override(-35f); dca.contrast.Override(22f);
            var dv = Add<Vignette>(dp); dv.intensity.Override(0.55f); dv.color.Override(Color.black); dv.smoothness.Override(0.6f);
            var dg = Add<FilmGrain>(dp); dg.intensity.Override(0.45f);
            _dark = NewVolume("Darkness", 40, dp, 0f);

            // ---- noise chapter
            var np = ScriptableObject.CreateInstance<VolumeProfile>(); np.name = "Noise";
            var nca = Add<ChromaticAberration>(np); nca.intensity.Override(0.75f);
            var ng = Add<FilmGrain>(np); ng.type.Override(FilmGrainLookup.Large02); ng.intensity.Override(0.8f);
            var nld = Add<LensDistortion>(np); nld.intensity.Override(-0.18f);
            _noise = NewVolume("Noise", 41, np, 0f);

            // ---- trial tension (courtroom pulses)
            var tp = ScriptableObject.CreateInstance<VolumeProfile>(); tp.name = "Tension";
            // tension only narrows the frame and deepens contrast a little: no colour cast, no fringing, no bloom blow-out
            var tv = Add<Vignette>(tp); tv.intensity.Override(0.42f); tv.smoothness.Override(0.5f); tv.color.Override(new Color(0.05f, 0.01f, 0.01f));
            var tcad = Add<ColorAdjustments>(tp); tcad.saturation.Override(-6f); tcad.contrast.Override(10f);
            _tension = NewVolume("Tension", 42, tp, 0f);

            // ---- fog + ambient fallbacks (characters use SH ambient)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.fogColor = _fogTarget;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.15f, 0.14f, 0.17f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.09f, 0.08f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.04f, 0.035f);
            _envCube = MakeEnvCube(32);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
#if UNITY_2022_1_OR_NEWER
            RenderSettings.customReflectionTexture = _envCube;
#endif
            RenderSettings.reflectionIntensity = 1f;

            Reflection = gameObject.AddComponent<MansionReflection>();
            Reflection.View = _view;
            SetRoom(null);
        }

        VolumeProfile MakeMood(MansionMood m)
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>(); p.name = "Mood_" + m;
            var ca = Add<ColorAdjustments>(p);
            var bloom = Add<Bloom>(p);
            var cab = Add<ChromaticAberration>(p);
            var wb = Add<WhiteBalance>(p);
            switch (m)
            {
                case MansionMood.Normal: ca.postExposure.Override(0.2f); bloom.intensity.Override(0.28f); cab.intensity.Override(0.015f); wb.temperature.Override(0f); break;
                case MansionMood.Grand: ca.postExposure.Override(0.25f); ca.saturation.Override(4f); bloom.intensity.Override(0.34f); cab.intensity.Override(0.02f); wb.temperature.Override(5f); break;
                case MansionMood.Mystery: ca.postExposure.Override(0.28f); ca.saturation.Override(6f); bloom.intensity.Override(0.4f); cab.intensity.Override(0.025f); wb.tint.Override(6f); break;
                case MansionMood.WhiteOut: ca.postExposure.Override(1.0f); ca.saturation.Override(-30f); ca.contrast.Override(-4f); bloom.intensity.Override(1.2f); bloom.threshold.Override(0.85f); cab.intensity.Override(0.05f); break;
                case MansionMood.Rain: ca.postExposure.Override(0.25f); ca.saturation.Override(0f); bloom.intensity.Override(0.34f); cab.intensity.Override(0.02f); wb.temperature.Override(-18f); break;
                case MansionMood.Water: ca.postExposure.Override(0.28f); ca.saturation.Override(4f); bloom.intensity.Override(0.34f); cab.intensity.Override(0.02f); wb.temperature.Override(-8f); break;
                case MansionMood.Machine: ca.postExposure.Override(0.25f); ca.saturation.Override(-10f); ca.contrast.Override(18f); bloom.intensity.Override(0.3f); cab.intensity.Override(0.02f); wb.temperature.Override(6f); break;
                case MansionMood.Chapel: ca.postExposure.Override(0.28f); ca.saturation.Override(2f); bloom.intensity.Override(0.4f); cab.intensity.Override(0.02f); wb.temperature.Override(4f); break;
                case MansionMood.Courtroom: ca.postExposure.Override(0.35f); ca.saturation.Override(6f); ca.contrast.Override(16f); bloom.intensity.Override(0.45f); cab.intensity.Override(0.0f); break;   // the trial: no colour cast, no fringing
            }
            return p;
        }

        public static MansionMood MoodFor(Room r)
        {
            if (r == null) return MansionMood.Normal;
            switch (r.Type)
            {
                case RoomType.GrandHall: case RoomType.Landing: case RoomType.Dining: case RoomType.Theater: case RoomType.Gallery: return MansionMood.Grand;
                case RoomType.WhiteDoors: return MansionMood.WhiteOut;
                case RoomType.RainCorridor: return MansionMood.Rain;
                case RoomType.Pool: case RoomType.WaterRoom: case RoomType.MirrorWater: return MansionMood.Water;
                case RoomType.MachineRoom: case RoomType.PowerRoom: case RoomType.BoilerRoom: case RoomType.Laundry: case RoomType.Storage: case RoomType.Incinerator: case RoomType.ColdStorage: return MansionMood.Machine;
                case RoomType.Darkroom: return MansionMood.Mystery;
                case RoomType.Chapel: return MansionMood.Chapel;
                case RoomType.Courtroom: return MansionMood.Courtroom;
            }
            return RoomInfo.IsMystery(r.Type) ? MansionMood.Mystery : MansionMood.Normal;
        }

        public void SetRoom(Room r)
        {
            SetMood(MoodFor(r));
            if (r != null && _view != null)
            {
                var pal = MansionPalette.Get(r.Palette);
                { float fl = pal.Fog.grayscale; _fogTarget = Color.Lerp(new Color(fl, fl, fl), pal.Fog, 0.45f) * 0.7f; }
                _fogDensityTarget = r.Type == RoomType.RainCorridor ? 0.05f : r.Type == RoomType.WhiteDoors ? 0.03f : r.Type == RoomType.Greenhouse || r.Type == RoomType.Pool ? 0.035f : r.Type == RoomType.Courtroom ? 0.012f : 0.02f;
                // big volumes hold a little more haze: depth between the candle pools
                if (r.Type == RoomType.GrandHall || r.Type == RoomType.Theater || r.Type == RoomType.Chapel || r.Type == RoomType.Landing || (r.Rect.W * r.Rect.D > 110f && _fogDensityTarget < 0.024f)) _fogDensityTarget = 0.026f;
                // by day, the haze under glass or by the windows is pale, not smoke
                if (_view.Sun > 0f && r.Floor >= 0 && (r.Exterior || r.Type == RoomType.Greenhouse || r.Type == RoomType.Courtyard || r.Type == RoomType.Pool))
                    _fogTarget = Color.Lerp(_fogTarget, new Color(0.36f, 0.38f, 0.41f), _view.Sun * (r.Type == RoomType.Greenhouse || r.Type == RoomType.Courtyard || r.Type == RoomType.Pool ? 0.75f : 0.35f));
                if (r.Type == RoomType.WhiteDoors) _fogTarget = new Color(0.9f, 0.9f, 0.92f);
            }
        }

        public void SetMood(MansionMood m, bool instant = false)
        {
            if (m == _mood) return;
            _mood = m;
            var next = _aIsCurrent ? _moodB : _moodA;
            next.sharedProfile = _moods[m];
            _aIsCurrent = !_aIsCurrent;
            _fade = instant ? 1f : 0f;
            ApplyFade();
        }

        void ApplyFade()
        {
            var cur = _aIsCurrent ? _moodA : _moodB; var prev = _aIsCurrent ? _moodB : _moodA;
            cur.weight = Mathf.SmoothStep(0, 1, _fade); prev.weight = 1f - cur.weight;
        }

        public void SetDarkness(float t) { if (_dark != null) _dark.weight = t; }
        public void SetNoise(float t) { if (_noise != null) _noise.weight = t; }
        public void SetTension(float t) { if (_tension != null) _tension.weight = Mathf.Clamp01(t); }

        /// <summary>Snap transitions (offline renders / scene cuts).</summary>
        public void Snap()
        {
            _fade = 1f; ApplyFade();
            RenderSettings.fogColor = _fogTarget; RenderSettings.fogDensity = _fogDensityTarget;
        }

        void Update()
        {
            if (_fade < 1f) { _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / 0.9f); ApplyFade(); }
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2f);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, _fogTarget, k);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, _fogDensityTarget, k);
        }

        /// <summary>Recommended camera settings for the mansion (post, HDR, SMAA, depth).</summary>
        public static void SetupCamera(Camera cam)
        {
            if (cam == null) return;
            cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 140f;
            var d = cam.GetUniversalAdditionalCameraData();
            d.renderPostProcessing = true;
            d.stopNaN = true;          // a single NaN pixel (degenerate glass normals) must never bloom into a white blob
            d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            d.antialiasingQuality = AntialiasingQuality.High;
            d.renderShadows = true;
            d.requiresDepthOption = CameraOverrideOption.On;
            d.requiresColorOption = CameraOverrideOption.On;
        }

        static Cubemap MakeEnvCube(int size)
        {
            var cube = new Cubemap(size, TextureFormat.RGBAHalf, true) { name = "MansionEnv" };
            var px = new Color[size * size];
            for (int f = 0; f < 6; f++)
            {
                for (int j = 0; j < size; j++)
                    for (int i = 0; i < size; i++)
                    {
                        float u = (i + 0.5f) / size * 2 - 1, v = (j + 0.5f) / size * 2 - 1;
                        Vector3 d;
                        switch ((CubemapFace)f)
                        {
                            case CubemapFace.PositiveX: d = new Vector3(1, -v, -u); break;
                            case CubemapFace.NegativeX: d = new Vector3(-1, -v, u); break;
                            case CubemapFace.PositiveY: d = new Vector3(u, 1, v); break;
                            case CubemapFace.NegativeY: d = new Vector3(u, -1, -v); break;
                            case CubemapFace.PositiveZ: d = new Vector3(u, -v, 1); break;
                            default: d = new Vector3(-u, -v, -1); break;
                        }
                        d.Normalize();
                        // dim interior: warm candle glow blobs near the horizon, a faint cold window band, dark floor
                        float up = d.y;
                        Color c = Color.Lerp(new Color(0.03f, 0.025f, 0.025f), new Color(0.08f, 0.065f, 0.06f), Mathf.Clamp01(up * 0.5f + 0.5f));
                        float ang = Mathf.Atan2(d.z, d.x);
                        float blobs = Mathf.Pow(Mathf.Max(0, Mathf.Sin(ang * 3f)), 16f) * Mathf.Exp(-Mathf.Abs(up - 0.25f) * 6f);
                        c += new Color(1.2f, 0.72f, 0.38f) * blobs * 0.9f;
                        float win = Mathf.Exp(-Mathf.Abs(up - 0.3f) * 12f) * Mathf.Pow(Mathf.Max(0, Mathf.Cos(ang * 2f + 0.7f)), 12f);
                        c += new Color(0.35f, 0.45f, 0.7f) * win * 0.25f;
                        px[j * size + i] = c;
                    }
                cube.SetPixels(px, (CubemapFace)f);
            }
            cube.Apply(true, false);
            return cube;
        }
    }

    /// <summary>
    /// Single planar reflection for the reflective surface of the room the camera is in (checker marble, wet stone,
    /// pool / mirror water). Renders a mirrored camera at half resolution and publishes _BL_PlanarTex.
    /// </summary>
    public sealed class MansionReflection : MonoBehaviour
    {
        public MansionView View;
        public float ResolutionScale = 0.5f;
        Camera _cam; RenderTexture _rt;
        public RenderTexture Texture => _rt;
        float _planeY; bool _active;
        static readonly int PlanarTex = Shader.PropertyToID("_BL_PlanarTex"), PlanarParams = Shader.PropertyToID("_BL_PlanarParams");

        void OnDisable() { Shader.SetGlobalVector(PlanarParams, Vector4.zero); }
        void OnDestroy() { if (_rt != null) _rt.Release(); }

        /// <summary>Which plane (if any) the camera's room reflects.</summary>
        public bool PlaneFor(int roomId, out float y, out float strength)
        {
            y = 0; strength = 0;
            if (View == null || roomId < 0) return false;
            var r = View.Layout.Rooms[roomId];
            float fy = View.Layout.FloorY(r.Floor);
            switch (r.Type)
            {
                case RoomType.GrandHall: case RoomType.ClockMuseum:
                    y = fy; strength = 0.6f; return true;
                case RoomType.Elevator: case RoomType.Gallery: case RoomType.WhiteDoors: case RoomType.Kitchen: case RoomType.ButlerRoom: case RoomType.TeaRoom: case RoomType.DollRoom: case RoomType.WaitingRoom:
                    y = fy; strength = 0.8f; return true;
                case RoomType.RainCorridor: y = fy; strength = 1.2f; return true;
                case RoomType.Pool: y = fy - 0.12f; strength = 1f; return true;
                case RoomType.MirrorWater: y = fy + 0.12f; strength = 1.3f; return true;
                case RoomType.Dining: case RoomType.Lounge: case RoomType.Library: case RoomType.Corridor: case RoomType.Landing: case RoomType.MusicRoom: case RoomType.Parlor: case RoomType.Study: case RoomType.TrophyRoom: case RoomType.Chapel:
                    y = fy; strength = 0.8f; return true;
                case RoomType.Courtroom: y = fy; strength = 1f; return true;
            }
            return false;
        }

        void LateUpdate()
        {
            var main = View != null && View.ViewCamera != null ? View.ViewCamera : Camera.main;
            if (main != null) Render(main);
        }

        /// <summary>Render the reflection for the given camera now (also used by offline screenshot tools).</summary>
        public void Render(Camera main)
        {
            if (View == null || main == null) return;
            int room = View.RoomAtWorld(main.transform.position);
            if (!PlaneFor(room, out float y, out float strength) || main.transform.position.y < y)
            {
                _active = false; Shader.SetGlobalVector(PlanarParams, Vector4.zero); return;
            }
            _planeY = y; _active = true;
            int w = Mathf.Max(64, (int)(main.pixelWidth * ResolutionScale)), h = Mathf.Max(64, (int)(main.pixelHeight * ResolutionScale));
            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                if (_rt != null) _rt.Release();
                _rt = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { name = "MansionPlanar", useMipMap = true, autoGenerateMips = true, wrapMode = TextureWrapMode.Clamp };
                _rt.Create();
            }
            if (_cam == null)
            {
                var go = new GameObject("PlanarCam") { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(transform, false);
                _cam = go.AddComponent<Camera>(); _cam.enabled = false;
                var d = _cam.GetUniversalAdditionalCameraData(); d.renderPostProcessing = false; d.renderShadows = false; d.requiresDepthOption = CameraOverrideOption.Off; d.requiresColorOption = CameraOverrideOption.Off; d.antialiasing = AntialiasingMode.None;
            }
            _cam.CopyFrom(main);
            _cam.ResetCullingMatrix();
            _cam.enabled = false;
            _cam.targetTexture = _rt;
            _cam.clearFlags = CameraClearFlags.SolidColor; var bgc = RenderSettings.fogColor * 0.6f; bgc.a = 0f; _cam.backgroundColor = bgc;   // alpha 0 = nothing reflected there
            // mirror: a proper (non-mirrored) camera at the reflected pose renders the mirror image flipped left/right;
            // the shaders sample it with u -> 1-u. No culling inversion needed (render-graph safe).
            var plane = new Vector4(0, 1, 0, -y);
            var R = Reflect(plane);
            _cam.ResetWorldToCameraMatrix(); _cam.ResetProjectionMatrix();
            var pos = R.MultiplyPoint(main.transform.position);
            var fwd = R.MultiplyVector(main.transform.forward); var up = R.MultiplyVector(main.transform.up);
            _cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, up));
            _cam.farClipPlane = 60f;
            var cp = CameraSpacePlane(_cam.worldToCameraMatrix, new Vector3(0, y, 0), Vector3.up, 0.004f);
            _cam.projectionMatrix = _cam.CalculateObliqueMatrix(cp);
            Shader.SetGlobalVector(PlanarParams, new Vector4(-1000, 0, 0, 0));
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = _rt };
            if (RenderPipeline.SupportsRenderRequest(_cam, req)) RenderPipeline.SubmitRenderRequest(_cam, req);
            Shader.SetGlobalTexture(PlanarTex, _rt);
            Shader.SetGlobalVector(PlanarParams, new Vector4(y, 1, strength, 0.02f));
        }

        static Matrix4x4 Reflect(Vector4 p)
        {
            var m = Matrix4x4.identity;
            m.m00 = 1 - 2 * p.x * p.x; m.m01 = -2 * p.x * p.y; m.m02 = -2 * p.x * p.z; m.m03 = -2 * p.w * p.x;
            m.m10 = -2 * p.y * p.x; m.m11 = 1 - 2 * p.y * p.y; m.m12 = -2 * p.y * p.z; m.m13 = -2 * p.w * p.y;
            m.m20 = -2 * p.z * p.x; m.m21 = -2 * p.z * p.y; m.m22 = 1 - 2 * p.z * p.z; m.m23 = -2 * p.w * p.z;
            m.m30 = 0; m.m31 = 0; m.m32 = 0; m.m33 = 1;
            return m;
        }

        static Vector4 CameraSpacePlane(Matrix4x4 w2c, Vector3 pos, Vector3 normal, float offset)
        {
            Vector3 op = pos + normal * offset;
            Vector3 cpos = w2c.MultiplyPoint(op);
            Vector3 cn = w2c.MultiplyVector(normal).normalized;
            return new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cpos, cn));
        }
    }
}
