using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BL23.Game
{
    /// <summary>
    /// The discovery film's grade (exploration only, never in the court): every colour but blood-red drains toward grey, a little
    /// more contrast, a wine-dark vignette and a fine luminance grain. A second tiny profile carries the 120 ms lens spike at the
    /// sting. Both live on their own volume layer (26); only cameras the film attaches see it, and their masks are handed back
    /// bit-exact once the tail has faded. A kill switch drops it the moment a trial or the reveal is on screen.
    /// </summary>
    [DefaultExecutionOrder(9000)]
    public sealed class DiscoveryGrade : MonoBehaviour
    {
        public const int Layer = 26;
        static DiscoveryGrade _i;
        public static DiscoveryGrade I => _i;

        Volume _vol, _spikeVol; VolumeProfile _p, _sp;
        ColorCurves _cc; TextureCurve _hue, _lum; ChromaticAberration _ca, _sca; LensDistortion _sld;
        readonly Dictionary<Camera, bool> _had = new Dictionary<Camera, bool>();   // camera → did it see layer 26 before we added it
        float _w, _wFrom, _wTo, _wT0 = -1f, _wDur = 1f;
        float _drainK, _drainFrom, _drainTo, _drainT0 = -1f, _drainDur = 0.4f, _drainApplied = -1f;
        float _spikeUntil = -1f, _spikeLen = 0.12f;
        float _tailAt = -1f, _tailFade = 4f; bool _tail;

        public float Weight => _vol != null ? _vol.weight : 0f;
        public bool Attached => _had.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _i = null; }

        public static DiscoveryGrade Ensure()
        {
            if (_i != null) { _i.gameObject.SetActive(true); return _i; }
            var go = new GameObject("DiscoveryGrade"); go.layer = Layer; DontDestroyOnLoad(go);
            _i = go.AddComponent<DiscoveryGrade>(); _i.Build();
            return _i;
        }

        // hue x∈[0,1): reds (0 / 1 ± 0.035) keep a touch more than their saturation, everything else falls to ~24 %
        static readonly float[] HueX = { 0f, 0.035f, 0.105f, 0.5f, 0.895f, 0.965f };
        static readonly bool[] HueRed = { true, true, false, false, false, true };
        const float RedSat = 0.55f, OtherSat = 0.12f;

        void Build()
        {
            _p = ScriptableObject.CreateInstance<VolumeProfile>(); _p.name = "DiscoveryGrade";
            _cc = _p.Add<ColorCurves>(false); _cc.active = true;
            var keys = new Keyframe[HueX.Length]; for (int k = 0; k < keys.Length; k++) keys[k] = new Keyframe(HueX[k], 0.5f, 0f, 0f);
            _hue = new TextureCurve(keys, 0.5f, true, new Vector2(0f, 1f));
            _cc.hueVsSat.Override(_hue);
            // luminance vs saturation: the highlights (candle flames, lit plaster) lose their colour first
            _lum = new TextureCurve(new[] { new Keyframe(0f, 0.5f, 0f, 0f), new Keyframe(0.55f, 0.5f, 0f, 0f), new Keyframe(1f, 0.3f, 0f, 0f) }, 0.5f, false, new Vector2(0f, 1f));
            _cc.lumVsSat.Override(_lum);
            var adj = _p.Add<ColorAdjustments>(false); adj.active = true; adj.contrast.Override(15f);
            var vig = _p.Add<Vignette>(false); vig.active = true; vig.color.Override(new Color(0.06f, 0f, 0.01f)); vig.intensity.Override(0.42f); vig.smoothness.Override(0.5f);
            var fg = _p.Add<FilmGrain>(false); fg.active = true; fg.type.Override(FilmGrainLookup.Thin1); fg.intensity.Override(0.15f); fg.response.Override(0.8f);
            _ca = _p.Add<ChromaticAberration>(false); _ca.active = true; _ca.intensity.Override(0f);
            var ld = _p.Add<LensDistortion>(false); ld.active = true; ld.intensity.Override(0f);
            _vol = gameObject.AddComponent<Volume>(); _vol.isGlobal = true; _vol.priority = 150f; _vol.weight = 0f; _vol.sharedProfile = _p;

            _sp = ScriptableObject.CreateInstance<VolumeProfile>(); _sp.name = "DiscoverySpike";
            _sca = _sp.Add<ChromaticAberration>(false); _sca.active = true; _sca.intensity.Override(0.3f);
            _sld = _sp.Add<LensDistortion>(false); _sld.active = true; _sld.intensity.Override(-0.18f); _sld.scale.Override(1.02f);
            var sgo = new GameObject("DiscoverySpike"); sgo.layer = Layer; sgo.transform.SetParent(transform, false);
            _spikeVol = sgo.AddComponent<Volume>(); _spikeVol.isGlobal = true; _spikeVol.priority = 151f; _spikeVol.weight = 0f; _spikeVol.sharedProfile = _sp;
        }

        // ------------------------------------------------------------------ cameras
        public void Attach(Camera cam)
        {
            if (cam == null || _had.ContainsKey(cam)) return;
            var d = cam.GetUniversalAdditionalCameraData(); if (d == null) return;
            _had[cam] = (d.volumeLayerMask.value & (1 << Layer)) != 0;
            d.volumeLayerMask = d.volumeLayerMask.value | (1 << Layer);
        }
        /// <summary>Give the camera's layer-26 bit back exactly as it was (other bits are left as they are now).</summary>
        public void Detach(Camera cam)
        {
            if (cam == null || !_had.TryGetValue(cam, out bool had)) return;
            _had.Remove(cam);
            var d = cam.GetUniversalAdditionalCameraData(); if (d == null) return;
            int m = d.volumeLayerMask.value & ~(1 << Layer); if (had) m |= 1 << Layer;
            d.volumeLayerMask = m;
        }
        public void DetachAll()
        {
            var cams = new List<Camera>(_had.Keys);
            foreach (var c in cams) Detach(c);
            _had.Clear();
        }

        // ------------------------------------------------------------------ controls (unscaled time)
        public void WeightTo(float w, float secs) { _tail = false; _tailAt = -1f; _wFrom = _w; _wTo = Mathf.Clamp01(w); _wT0 = Time.unscaledTime; _wDur = Mathf.Max(0.01f, secs); }
        /// <summary>Colour drain 0..1 (1 = non-red hues at ~24 % saturation), eased over `secs`.</summary>
        public void Drain(float k, float secs) { _drainFrom = _drainK; _drainTo = Mathf.Clamp01(k); _drainT0 = Time.unscaledTime; _drainDur = Mathf.Max(0.01f, secs); }
        public void Spike(float secs = 0.12f) { _spikeLen = Mathf.Max(0.02f, secs); _spikeUntil = Time.unscaledTime + _spikeLen; }
        /// <summary>After control returns: hold the current weight briefly, then fade to 0 over `fade` seconds; then hand the
        /// camera masks back and switch off.</summary>
        public void Tail(float fade = 4f) { _tail = true; _tailAt = Time.unscaledTime; _tailFade = Mathf.Max(0.05f, fade); _wFrom = _w; }
        /// <summary>At once: weight 0, masks restored, off.</summary>
        public void Kill()
        {
            _w = 0f; _wT0 = -1f; _tail = false; _drainK = 0f; _drainT0 = -1f; _spikeUntil = -1f;
            if (_vol != null) _vol.weight = 0f; if (_spikeVol != null) _spikeVol.weight = 0f;
            ApplyDrain(0f);
            DetachAll();
            gameObject.SetActive(false);
        }

        static bool CourtOnScreen()
        {
            var s = Session.I; if (s == null) return false;
            if ((s.Trial != null && s.Trial.Active) || (s.Reveal != null && s.Reveal.Active)) return true;
            return s.Sim != null && s.S != null && s.S.Phase == Phase.Trial;
        }

        void LateUpdate()
        {
            // never in the court: no grade, no grain, no tint
            if (CourtOnScreen()) { Kill(); return; }
            float now = Time.unscaledTime;
            if (_tail)
            {
                float u = Mathf.Clamp01((now - _tailAt) / _tailFade);
                _w = Mathf.Lerp(_wFrom, 0f, u * u * (3f - 2f * u));
                if (u >= 1f) { Kill(); return; }
            }
            else if (_wT0 >= 0f)
            {
                float u = Mathf.Clamp01((now - _wT0) / _wDur); _w = Mathf.Lerp(_wFrom, _wTo, u * u * (3f - 2f * u));
            }
            if (_drainT0 >= 0f) { float u = Mathf.Clamp01((now - _drainT0) / _drainDur); _drainK = Mathf.Lerp(_drainFrom, _drainTo, u * u * (3f - 2f * u)); }
            if (_vol != null) _vol.weight = _w;
            // a curve does not blend with the volume weight (it switches), so on the way down the drain itself follows the
            // weight (colour seeps back with the return); on the way up it keeps its own 0.4 s ease from the sting
            bool falling = _tail || (_wT0 >= 0f && _wTo < _wFrom);
            ApplyDrain(_drainK * (falling ? Mathf.Clamp01(_w / 0.6f) : 1f));
            if (_spikeVol != null)
            {
                float left = _spikeUntil - now;
                _spikeVol.weight = left > 0f ? Mathf.Sin(Mathf.Clamp01(1f - left / _spikeLen) * Mathf.PI) : 0f;
            }
        }

        void ApplyDrain(float k)
        {
            if (_hue == null || Mathf.Abs(k - _drainApplied) < 0.004f) return;
            _drainApplied = k;
            for (int i = 0; i < HueX.Length; i++)
            {
                float v = Mathf.Lerp(0.5f, HueRed[i] ? RedSat : OtherSat, k);
                _hue.MoveKey(i, new Keyframe(HueX[i], v, 0f, 0f));
            }
            _hue.SetDirty();
        }

        void OnDestroy()
        {
            DetachAll();
            _hue?.Release(); _lum?.Release();
            if (_p != null) Destroy(_p); if (_sp != null) Destroy(_sp);
            if (_i == this) _i = null;
        }
    }
}
