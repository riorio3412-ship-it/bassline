using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BL23.Game
{
    /// <summary>Probe: vistas of the court's well from fixed verification cameras (court_scale_*). Each is rendered by a temporary
    /// "CourtVistaCam" (same far clip / post as the mansion cameras; the court pins its culling to it) into its own render
    /// texture and saved directly, so the trial camera's own probe shots are never captured through it and no UI covers it.</summary>
    public sealed partial class AutoProbe
    {
        IEnumerator CourtVista(string name, Vector3 pos, Vector3 look, float fov)
        {
            int w = Screen.width, h = Screen.height;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "CourtVista" }; rt.Create();
            var go = new GameObject("CourtVistaCam"); var cam = go.AddComponent<Camera>(); go.AddComponent<UniversalAdditionalCameraData>();
            BL23.Game.Mansion.MansionAtmosphere.SetupCamera(cam);
            cam.depth = 60; cam.fieldOfView = fov; cam.targetTexture = rt;
            go.transform.position = pos;
            var fwd = look - pos; var upv = Mathf.Abs(Vector3.Dot(fwd.normalized, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            go.transform.rotation = Quaternion.LookRotation(fwd, upv);
            var mv = Ses.World?.Mansion; if (mv != null) { mv.ViewCamera = cam; mv.Cull(pos); }
            yield return Wait(0.6f);
            if (mv != null) mv.Cull(pos);
            yield return Wait(0.25f);
            yield return new WaitForEndOfFrame();
            try
            {
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(false); RenderTexture.active = prev;
                var file = Path.Combine(_dir, $"{++_shots:00}_{name}.png"); File.WriteAllBytes(file, tex.EncodeToPNG()); Destroy(tex);
                var court = Ses.World?.Court;
                Log($"shot {Path.GetFileName(file)}  (court vista) cam {pos:F1} fov {fov} recentMs {(_ft.Count > 0 ? _ft[_ft.Count - 1] * 1000f : 0f):0}" + (court != null ? " | " + court.WellState() + " | " + court.JuryState() : ""));
            }
            catch (System.Exception e) { Log("court vista failed " + name + ": " + e.Message); }
            cam.targetTexture = null; Destroy(go); rt.Release(); Destroy(rt);
            yield return null;
        }

        /// <summary>The well verification set (spec V1..V10) right after the trial opens.</summary>
        IEnumerator CourtScale()
        {
            var court = Ses.World?.Court; if (court == null || court.JudgeAnchor == null) { Log("court_scale: no court"); yield break; }
            Vector3 c = court.Center, up = Vector3.up; var jd = court.JudgeAnchor.position - c; jd.y = 0; jd = jd.sqrMagnitude > 1e-4f ? jd.normalized : Vector3.forward;
            var rt = Vector3.Cross(up, jd);
            Vector3 D(float th) { float a = th * Mathf.Deg2Rad; return jd * Mathf.Cos(a) + rt * Mathf.Sin(a); }
            var judge = court.JudgeAnchor.position; var head = judge + up * 1.25f;
            yield return CourtVista("court_scale_1", c - jd * 6.1f - rt * 0.6f + up * 2.4f, judge + up * 1.3f, 52f);          // V1 establish
            yield return CourtVista("court_scale_2", c - jd * 8.3f + rt * 1.1f + up * 3.1f, judge + up * 1.4f, 22f);           // V2 throne telephoto (over the heads)
            yield return CourtVista("court_scale_3", c + D(-110f) * 5.4f + up * 3.4f, c + D(-110f) * 40f + up * 20f, 40f);     // V5 out between two podiums, over the gallery
            court.WellShot("well_up", out var upPos, out var upLook, out var upFov);
            yield return CourtVista("court_scale_4", upPos, upLook, upFov);                                                     // V6 up to the Sun (the canonical up-shot)
            yield return CourtVista("court_scale_5", c + D(130f) * 9.5f + up * 8.5f, court.AbyssPoint, 55f);                   // V7 down past the parapet
            yield return CourtVista("court_scale_6", c - jd * 3.4f + rt * 1.8f + up * 30f, c + jd * 0.5f, 52f);                // V10 high in the well
            yield return CourtVista("court_scale_7", c - rt * 4.8f + up * 1.5f, c + rt * 6.2f + up * 1.55f, 28f);             // V4 speaker background
            yield return CourtVista("court_scale_8", c + jd * 6f + up * 2.5f, court.LiftTop, 50f);                             // V8 the lift tower
            yield return CourtVista("court_scale_9", head - jd * 3.0f - up * 0.6f, head, 31f);                                 // V3 the judge
            court.WellShot("reveal_start", out var rvPos, out var rvLook, out var rvFov);
            yield return CourtVista("court_scale_reveal", rvPos, rvLook, rvFov);                                                 // the opening reveal the lens is offered
            court.WellShot("judge_wide", out var jwPos, out var jwLook, out var jwFov);
            yield return CourtVista("court_scale_judge_wide", jwPos, jwLook, jwFov);                                             // lancet, piers and chains above the throne
            yield return CourtVista("court_scale_speaker_arcade", c + D(-60f) * 2.4f + up * 1.5f, c + D(-60f) * 6.9f + D(30f) * 0.4f + up * 1.55f, 30f);   // a podium with the open arcade behind it
            // the bell in its pier bay, from the far side of the floor
            if (court.BellShot(out var bPos, out var bLook, out var bFov)) yield return CourtVista("court_scale_bell", bPos, bLook, bFov);
            // the court's hourglass on the judge's bench
            if (court.HourglassShot(out var hPos, out var hLook, out var hFov)) yield return CourtVista("court_scale_hourglass", hPos, hLook, hFov);
            // the lamp on the stair: across the well from the floor (a long lens), then close from the air
            if (court.WalkerShot(false, out var wPos, out var wLook, out var wFov)) yield return CourtVista("court_scale_walker", wPos, wLook, wFov);
            if (court.WalkerShot(true, out var wcPos, out var wcLook, out var wcFov)) yield return CourtVista("court_scale_walker_close", wcPos, wcLook, wcFov);
            // the stone jury on the piers, still gazing up at the Sun
            if (court.JuryShot(out var jPos, out var jLook, out var jFov)) yield return CourtVista("court_scale_jury", jPos, jLook, jFov);
        }

        /// <summary>V9: the look-up again once the verdict has been spoken (eye bands lit, pupil contracted, gallery hushed).</summary>
        IEnumerator CourtVerdictUp()
        {
            var court = Ses.World?.Court; if (court == null || court.JudgeAnchor == null) yield break;
            float w0 = Time.unscaledTime; while (court.VerdictAt < 0f && Time.unscaledTime - w0 < 40f) yield return Wait(0.2f);
            if (court.VerdictAt < 0f) Log("court_scale_verdict: no verdict beat seen");
            // the wake climbing (the vista captures ~0.85 s after it is set up: about 1.8 s into the wave, half the rings open)
            while (court.VerdictAt > 0f && Time.unscaledTime - court.VerdictAt < 0.9f) yield return Wait(0.05f);
            if (court.VerdictAt > 0f && court.WellShot("verdict_up", out var clPos, out var clLook, out var clFov)) yield return CourtVista("court_scale_verdict_climb", clPos, clLook, clFov);
            while (court.VerdictAt > 0f && Time.unscaledTime - court.VerdictAt < 6.5f) yield return Wait(0.1f);
            Vector3 c = court.Center; var jd = court.JudgeAnchor.position - c; jd.y = 0; jd = jd.sqrMagnitude > 1e-4f ? jd.normalized : Vector3.forward;
            court.WellShot("verdict_up", out var vPos, out var vLook, out var vFov);
            yield return CourtVista("court_scale_verdict_up", vPos, vLook, vFov);
            if (court.JuryShot(out var vjPos, out var vjLook, out var vjFov)) yield return CourtVista("court_scale_verdict_jury", vjPos, vjLook, vjFov);   // the jury, turned to the court
            yield return CourtVista("court_scale_verdict_wide", c - jd * 6.1f + Vector3.up * 2.4f, court.JudgeAnchor.position + Vector3.up * 6f, 60f);
            var rtv = Vector3.Cross(Vector3.up, jd);
            yield return CourtVista("court_scale_verdict_throne", c - jd * 8.3f + rtv * 1.1f + Vector3.up * 3.1f, court.JudgeAnchor.position + Vector3.up * 1.4f, 22f);   // V2 again: the window has gathered
            var vhead = court.JudgeAnchor.position + Vector3.up * 1.25f;
            yield return CourtVista("court_scale_verdict_judge", vhead - jd * 3.0f - Vector3.up * 0.6f, vhead, 31f);   // V3 again: what stands behind the lancet
            if (court.WalkerShot(true, out var wcPos, out var wcLook, out var wcFov)) yield return CourtVista("court_scale_verdict_walker", wcPos, wcLook, wcFov);
        }
    }
}
