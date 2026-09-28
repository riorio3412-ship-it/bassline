using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// The discovery film's candle gutter: the room's lights dip toward a fraction of their brightness, the dip travelling
    /// outward from what 민혁 is looking at, and come back afterwards. Other code (MansionView's flicker, culling and daylight)
    /// keeps writing absolute intensities every frame; whenever a light holds a value we did not write, that value is adopted
    /// as its new base and the film's factor is applied on top of it. Runs after everything else (LateUpdate, order 10000).
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class FilmLights : MonoBehaviour
    {
        sealed class Entry { public Light L; public float Base, Written = float.NaN, Delay, K = 1f, KFrom = 1f; }

        static FilmLights _i;
        readonly List<Entry> _e = new List<Entry>();
        static readonly Dictionary<Light, float> _bases = new Dictionary<Light, float>();
        /// <summary>Each managed light's base intensity as last recorded (kept after the film ends, for the probe's invariant).</summary>
        public static IReadOnlyDictionary<Light, float> Bases => _bases;
        public static bool Running => _i != null && _i._e.Count > 0;

        float _dipAt = -1f, _dipTo = 1f, _ramp = 0.25f, _retAt = -1f, _retDur = 1.2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _i = null; _bases.Clear(); }

        public static FilmLights Ensure()
        {
            if (_i != null) return _i;
            var go = new GameObject("DiscoveryFilmLights"); DontDestroyOnLoad(go);
            _i = go.AddComponent<FilmLights>();
            return _i;
        }

        /// <summary>Take over these lights (delay = when the dip reaches each one, seconds after the dip starts).</summary>
        public void Begin(List<(Light light, float delay)> lights)
        {
            End();
            _bases.Clear(); _dipAt = -1f; _retAt = -1f;
            foreach (var (l, d) in lights)
            {
                if (l == null) continue;
                _e.Add(new Entry { L = l, Base = l.intensity, Delay = Mathf.Max(0f, d) });
                _bases[l] = l.intensity;
            }
        }

        /// <summary>Start the dip now (unscaled time): each light falls to `to` of its base over `ramp` seconds after its delay.</summary>
        public void Dip(float to, float ramp) { _dipAt = Time.unscaledTime; _dipTo = Mathf.Clamp01(to); _ramp = Mathf.Max(0.01f, ramp); _retAt = -1f; }

        /// <summary>Bring every light back to its base over `secs`.</summary>
        public void Return(float secs)
        {
            _retAt = Time.unscaledTime; _retDur = Mathf.Max(0.01f, secs);
            foreach (var e in _e) e.KFrom = e.K;
        }

        /// <summary>Hand the lights back (exactly their bases, unless someone else has written them since).</summary>
        public void End()
        {
            foreach (var e in _e)
            {
                if (e.L == null) continue;
                if (float.IsNaN(e.Written) || Mathf.Abs(e.L.intensity - e.Written) < 1e-4f) e.L.intensity = e.Base;
                _bases[e.L] = e.Base;
            }
            _e.Clear(); _dipAt = -1f; _retAt = -1f;
        }

        float KOf(Entry e, float now)
        {
            if (_retAt >= 0f) { float u = Mathf.Clamp01((now - _retAt) / _retDur); u = u * u * (3f - 2f * u); return Mathf.Lerp(e.KFrom, 1f, u); }
            if (_dipAt < 0f) return 1f;
            float k = Mathf.Clamp01((now - _dipAt - e.Delay) / _ramp);
            return Mathf.Lerp(1f, _dipTo, k);
        }

        void LateUpdate()
        {
            if (_e.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = 0; i < _e.Count; i++)
            {
                var e = _e[i]; if (e.L == null) continue;
                float cur = e.L.intensity;
                // someone else (flicker, culling, daylight, a power cut) wrote an absolute value: that is the new base
                if (float.IsNaN(e.Written) || Mathf.Abs(cur - e.Written) > 1e-4f) e.Base = cur;
                e.K = KOf(e, now);
                float v = e.Base * e.K;
                e.L.intensity = v; e.Written = v;
                _bases[e.L] = e.Base;
            }
        }

        void OnDisable() { End(); }
    }
}
