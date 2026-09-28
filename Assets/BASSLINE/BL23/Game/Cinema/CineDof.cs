using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BL23.Game.Cinema
{
    /// <summary>
    /// Per-camera depth of field for the cinematic cameras (trial close-ups, the reveal, every live pane of a montage).
    /// One global Volume on its own layer carries a Bokeh DoF override; only cameras that opt in (Register) see that layer,
    /// and the override's focus/aperture are rewritten just before each of them renders (URP updates the volume stack per
    /// camera after beginCameraRendering). Nothing else about the image changes: no grain, no tint.
    /// </summary>
    public static class CineDof
    {
        public const int Layer = 25;
        struct Setting { public bool On; public float Focus, Aperture, Focal; }
        static Volume _vol; static DepthOfField _dof; static bool _hooked;
        static readonly Dictionary<Camera, Setting> _set = new Dictionary<Camera, Setting>();

        static void Ensure()
        {
            if (_vol != null) return;
            var go = new GameObject("CineDofVolume"); go.layer = Layer; Object.DontDestroyOnLoad(go);
            var p = ScriptableObject.CreateInstance<VolumeProfile>(); p.name = "CineDof";
            _dof = p.Add<DepthOfField>(false); _dof.active = false;
            _dof.mode.Override(DepthOfFieldMode.Bokeh);
            _dof.focusDistance.Override(2f); _dof.aperture.Override(4f); _dof.focalLength.Override(50f);
            _dof.bladeCount.Override(6); _dof.bladeCurvature.Override(0.9f); _dof.bladeRotation.Override(0f);
            _vol = go.AddComponent<Volume>(); _vol.isGlobal = true; _vol.priority = 200; _vol.weight = 1f; _vol.sharedProfile = p;
            if (!_hooked) { _hooked = true; RenderPipelineManager.beginCameraRendering += OnBegin; }
        }

        /// <summary>Let this camera see the DoF volume (its normal volumes stay as they are).</summary>
        public static void Register(Camera cam)
        {
            if (cam == null) return; Ensure();
            var d = cam.GetUniversalAdditionalCameraData(); if (d == null) return;
            d.volumeLayerMask = d.volumeLayerMask | (1 << Layer);
            cam.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.EveryFrame);
            if (!_set.ContainsKey(cam)) _set[cam] = new Setting();
        }

        /// <summary>Focus at 'focus' metres. Smaller aperture numbers and longer focal lengths blur harder.</summary>
        public static void Set(Camera cam, bool on, float focus = 2f, float aperture = 4f, float focal = 50f)
        {
            if (cam == null) return;
            _set[cam] = new Setting { On = on, Focus = Mathf.Max(0.1f, focus), Aperture = Mathf.Clamp(aperture, 1f, 32f), Focal = Mathf.Clamp(focal, 1f, 300f) };
        }
        public static void Off(Camera cam) { if (cam != null && _set.ContainsKey(cam)) _set[cam] = new Setting(); }
        public static void Forget(Camera cam) { if (cam != null) _set.Remove(cam); }

        static void OnBegin(ScriptableRenderContext ctx, Camera cam)
        {
            if (_dof == null) return;
            if (cam != null && _set.TryGetValue(cam, out var s) && s.On)
            {
                _dof.active = true;
                _dof.focusDistance.value = s.Focus; _dof.aperture.value = s.Aperture; _dof.focalLength.value = s.Focal;
            }
            else _dof.active = false;
        }
    }
}
