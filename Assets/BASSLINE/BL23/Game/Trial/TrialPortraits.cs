using System.Collections;
using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BL23.Game
{
    /// <summary>
    /// Face portraits rendered from the real 3D actors (one offscreen camera, one frame per face, rendered at 448×560 with
    /// mips: supersampled wherever they are shown smaller) — used by medallions, the portrait galleries and the urns.
    /// Each face is taken in a "studio": the person's renderers move to a layer nothing else uses for that one frame, so the
    /// picture never depends on room culling, on who stands in front, or on whether they have reached their podium yet.
    /// </summary>
    public static class TrialPortraits
    {
        const int Studio = 29;
        static readonly Dictionary<string, RenderTexture> _rt = new Dictionary<string, RenderTexture>();
        public static Texture Get(string id) => id != null && _rt.TryGetValue(id, out var t) && t != null ? t : null;
        public static bool Busy { get; private set; }

        public static void Clear() { foreach (var t in _rt.Values) if (t != null) { t.Release(); Object.Destroy(t); } _rt.Clear(); }

        public static IEnumerator Capture(Session s, IEnumerable<string> ids, Vector3? center = null)
        {
            Busy = true;
            var go = new GameObject("TrialPortraitCam"); var cam = go.AddComponent<Camera>(); cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.07f, 0.045f, 0.03f, 1f); cam.fieldOfView = 22; cam.nearClipPlane = 0.05f; cam.farClipPlane = 5f; cam.depth = -50;
            cam.cullingMask = 1 << Studio;
            var data = go.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.renderShadows = false; data.antialiasing = AntialiasingMode.None;
            var key = new GameObject("TrialPortraitKey").AddComponent<Light>(); key.type = LightType.Point; key.range = 2.6f; key.intensity = 2.2f; key.color = new Color(1f, 0.9f, 0.8f); key.cullingMask = 1 << Studio;
            var rim = new GameObject("TrialPortraitRim").AddComponent<Light>(); rim.type = LightType.Point; rim.range = 2.2f; rim.intensity = 2.6f; rim.color = new Color(1f, 0.66f, 0.36f); rim.cullingMask = 1 << Studio;
            var fill = new GameObject("TrialPortraitFill").AddComponent<Light>(); fill.type = LightType.Point; fill.range = 2.4f; fill.intensity = 0.7f; fill.color = new Color(0.75f, 0.8f, 1f); fill.cullingMask = 1 << Studio;
            foreach (var id in ids)
            {
                var v = s.World?.ViewOf(id); if (v == null || !v.gameObject.activeInHierarchy) continue;
                // make sure the body is drawn (it may stand in a room the mansion has culled a moment ago)
                bool fv = v.ForceVisible; v.ForceVisible = true; v.Tick(0f);
                var rends = v.GetComponentsInChildren<Renderer>(true); var layers = new int[rends.Length];
                for (int i = 0; i < rends.Length; i++) { layers[i] = rends[i].gameObject.layer; rends[i].gameObject.layer = Studio; }
                var head = v.HeadPos;
                // at the podium the face turns to the middle of the court; anywhere else, to wherever the person faces
                Vector3 fwd = center.HasValue && (center.Value - head).sqrMagnitude < 15f * 15f ? center.Value - head : v.transform.forward;
                fwd.y = 0; if (fwd.sqrMagnitude < 0.01f) fwd = v.transform.forward; fwd.y = 0; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward; fwd.Normalize();
                if (!_rt.TryGetValue(id, out var rt) || rt == null) { rt = new RenderTexture(672, 840, 24, RenderTextureFormat.ARGB32) { name = "portrait_" + id, useMipMap = true, autoGenerateMips = true, filterMode = FilterMode.Trilinear, anisoLevel = 2, antiAliasing = 4 }; rt.Create(); _rt[id] = rt; }   // 1.5x + 4x MSAA: no jagged hair/outlines on portraits
                cam.targetTexture = rt;
                cam.transform.position = head + fwd * 0.95f + Vector3.up * 0.03f; cam.transform.LookAt(head + Vector3.down * 0.1f);
                var right = Vector3.Cross(Vector3.up, fwd);
                key.transform.position = head + fwd * 0.7f + right * 0.45f + Vector3.up * 0.35f;
                rim.transform.position = head - fwd * 0.5f - right * 0.4f + Vector3.up * 0.3f;
                fill.transform.position = head + fwd * 0.8f - right * 0.6f - Vector3.up * 0.1f;
                cam.enabled = true;
                yield return new WaitForEndOfFrame();
                cam.enabled = false;
                for (int i = 0; i < rends.Length; i++) if (rends[i] != null) rends[i].gameObject.layer = layers[i];
                v.ForceVisible = fv;
                yield return null;
            }
            cam.targetTexture = null;
            Object.Destroy(go); Object.Destroy(key.gameObject); Object.Destroy(rim.gameObject); Object.Destroy(fill.gameObject);
            Busy = false;
        }
    }
}
