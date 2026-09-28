using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game
{
    /// <summary>
    /// Perf probe A/B levers (part of -bl23probe perf): after the hall, dining and dialogue windows the kernel is paused (the
    /// scene holds still), a reference window is measured, then one runtime change at a time is measured the same way and
    /// undone — so the owners' recommendations come with a counter delta instead of a guess. The levers only emulate a
    /// recommendation (e.g. "characters cast no shadow" is the upper bound of a low-poly shadow proxy); nothing here ships.
    /// Levers: ~noreflect (MansionReflection off) · ~charshadowoff (people cast no shadows) · ~lodbias1 (QualitySettings.lodBias
    /// 2 → 1: people switch to LOD1 at about half the distance) · ~shadows0 (no point-light shadows at all) · ~hallcull (hall
    /// only: rooms on other floors that are not hall/landing/stairwell hidden) · ~far25 (dialogue only: camera far plane 25 m).
    /// Every change registers its undo, so a fault inside a window still restores the scene (PerfSafe calls PerfLeverUndo).
    /// </summary>
    public sealed partial class AutoProbe
    {
        readonly List<Action> _leverUndo = new List<Action>();

        void PerfLeverUndo()
        {
            for (int i = _leverUndo.Count - 1; i >= 0; i--) { try { _leverUndo[i](); } catch (Exception e) { Log("perf: lever undo threw " + e.Message); } }
            _leverUndo.Clear();
        }

        IEnumerator PerfLevers(string baseName, int room, bool hallCull, bool farClip)
        {
            Ses.Pause("perflever");
            _leverUndo.Add(() => Ses.Resume("perflever"));
            string where = $"levers of {baseName} (kernel paused), {S.RoomName(room)}";
            yield return PerfMeasure(baseName + "~ref", where + ": reference", room, 1.5f, 3f);

            yield return PerfLever(baseName + "~noreflect", where + ": planar reflection off", room, () =>
            {
                var rs = FindObjectsByType<BL23.Game.Mansion.MansionReflection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(r => r.enabled).ToList();
                foreach (var r in rs) r.enabled = false;
                return () => { foreach (var r in rs) if (r != null) r.enabled = true; };
            });

            yield return PerfLever(baseName + "~charshadowoff", where + ": people cast no shadows", room, () =>
            {
                var saved = new List<(Renderer r, ShadowCastingMode m)>();
                foreach (var v in Ses.World.Actors.Values)
                    if (v != null && v.Rig != null)
                        foreach (var r in v.Rig.GetComponentsInChildren<Renderer>(true)) { saved.Add((r, r.shadowCastingMode)); if (r.shadowCastingMode != ShadowCastingMode.ShadowsOnly) r.shadowCastingMode = ShadowCastingMode.Off; }
                return () => { foreach (var (r, m) in saved) if (r != null) r.shadowCastingMode = m; };
            });

            yield return PerfLever(baseName + "~lodbias1", where + ": QualitySettings.lodBias 1", room, () =>
            {
                float old = QualitySettings.lodBias; QualitySettings.lodBias = 1f;
                return () => QualitySettings.lodBias = old;
            });

            yield return PerfLever(baseName + "~shadows0", where + ": no point/spot light shadows", room, () =>
            {
                var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(l => l.type != LightType.Directional).ToList();
                var saved = lights.Select(l => (l, l.shadows)).ToList();
                var enf = gameObject.AddComponent<PerfShadowsOff>(); enf.Lights = lights;
                return () => { if (enf != null) Destroy(enf); foreach (var (l, s) in saved) if (l != null) l.shadows = s; };
            });

            if (hallCull)
                yield return PerfLever(baseName + "~hallcull", where + ": other-floor rooms except hall/landing/stairwell hidden", room, () =>
                {
                    var off = new List<Renderer>(); var mv = Ses.World.Mansion; int f = S.Layout.Rooms[room].Floor;
                    if (mv != null && mv.Rooms != null)
                        foreach (var rv in mv.Rooms)
                        {
                            if (rv == null || !rv.Visible || rv.Room.Floor == f) continue;
                            var t = rv.Room.Type; if (t == RoomType.GrandHall || t == RoomType.Landing || t == RoomType.Stairwell) continue;
                            foreach (var r in rv.Renderers) if (r != null && r.enabled) { r.enabled = false; off.Add(r); }
                        }
                    Log($"perf: hallcull hid {off.Count} renderers");
                    return () => { foreach (var r in off) if (r != null) r.enabled = true; };
                });

            if (farClip)
                yield return PerfLever(baseName + "~far25", where + ": camera far plane 25 m", room, () =>
                {
                    Camera best = null; var cams = new Camera[16]; int n = Camera.GetAllCameras(cams);
                    for (int i = 0; i < n; i++) { var c = cams[i]; if (c != null && c.isActiveAndEnabled && c.targetTexture == null && (best == null || c.depth > best.depth)) best = c; }
                    if (best == null) return () => { };
                    float old = best.farClipPlane; best.farClipPlane = Mathf.Min(old, 25f);
                    Log($"perf: far25 on {best.name} (was {old:0} m)");
                    return () => { if (best != null) best.farClipPlane = old; };
                });

            PerfLeverUndo();   // resumes the kernel
            PerfLeverSummary(baseName);
        }

        IEnumerator PerfLever(string name, string where, int room, Func<Action> apply)
        {
            Action undo = null;
            try { undo = apply(); } catch (Exception e) { Log($"perf: lever {name} failed: {e.Message}"); }
            if (undo == null) yield break;
            _leverUndo.Add(undo);
            yield return PerfMeasure(name, where, room, 1f, 3f);
            _leverUndo.Remove(undo);
            try { undo(); } catch (Exception e) { Log($"perf: lever {name} undo threw {e.Message}"); }
            yield return Wait(0.4f);
        }

        /// <summary>One PERFAB line per lever: the change of each machine-independent counter against the ~ref window.</summary>
        void PerfLeverSummary(string baseName)
        {
            var refRow = _perfRows.LastOrDefault(r => r.Name == baseName + "~ref"); if (refRow == null) return;
            string[] keys = { "drawCalls", "setPassCalls", "triangles", "vertices", "shadowCasters", "vbUploadBytes", "visibleSkinnedMeshes" };
            double V(PerfRow r, string k) => r.C.TryGetValue(k, out var v) && v.ok ? v.avg : double.NaN;
            var sb = new StringBuilder();
            foreach (var row in _perfRows.Where(r => r.Name.StartsWith(baseName + "~") && r != refRow))
            {
                sb.Clear().Append($"PERFAB {row.Name}:");
                foreach (var k in keys)
                {
                    double a = V(refRow, k), b = V(row, k); if (double.IsNaN(a) || double.IsNaN(b)) continue;
                    sb.Append($" {k} {a:0}→{b:0} ({(a > 0 ? (b - a) / a * 100 : 0):+0;-0;0}%)");
                }
                sb.Append($" | renderers vis {(refRow.N.TryGetValue("renderersVisible", out var rv0) ? rv0 : 0):0}→{(row.N.TryGetValue("renderersVisible", out var rv1) ? rv1 : 0):0}");
                Log(sb.ToString());
            }
        }
    }

    /// <summary>Probe only: keeps the listed lights' shadows off every frame (MansionView's culling re-enables them 5x a second).</summary>
    public sealed class PerfShadowsOff : MonoBehaviour
    {
        public List<Light> Lights;
        void LateUpdate() { if (Lights == null) return; foreach (var l in Lights) if (l != null && l.shadows != LightShadows.None) l.shadows = LightShadows.None; }
    }
}
