using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BL23.Game.Audio;
using BL23.Game.Cinema;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BL23.Game
{
    /// <summary>
    /// -bl23probe disc:all | disc:stab | disc:blunt | disc:strangle | disc:dismember (or "-bl23probe disc -discCase stab,blunt";
    /// optional -bl23seed N = base seed, case i uses N+i) — the body-discovery film, case by case:
    /// a murder staged by the kernel (Gore.DebugStage), 민혁 runs in through the (opened) door, the film plays (every beat
    /// photographed), then every invariant of the restore is checked, the gore is photographed from close by (cameras pushed
    /// out of geometry, overlays hidden), every picture is scanned for pink, and (stab) the skip is tested from inside the
    /// room. Gates: the hands in frame at D0, nothing left of a severed head on the neck, a strangled neck's furrow, the piece
    /// bake and torso cut times, the gore mesh budget. Ends with one PASS/FAIL line per case.
    /// </summary>
    public sealed partial class AutoProbe
    {
        sealed class DiscResult
        {
            public string Kind; public readonly List<string> Fails = new List<string>(); public readonly List<string> Notes = new List<string>();
            public void Check(bool ok, string what) { if (!ok) Fails.Add(what); }
            public string Summary() => $"DISC {Kind}: {(Fails.Count == 0 ? "PASS" : "FAIL")}" + (Fails.Count > 0 ? " — " + string.Join("; ", Fails) : "") + (Notes.Count > 0 ? " | " + string.Join("; ", Notes) : "");
        }

        IEnumerator DiscoveryTour(string mode)
        {
            yield return Wait(2f);
            // cases: "-bl23probe disc:<k>[,<k>…]" or "-bl23probe disc -discCase <k>[,<k>…]" (the flag wins); k = stab|blunt|strangle|dismember|all
            string arg = (GameBoot.I.ArgValue("-discCase") ?? (mode.Contains(":") ? mode.Substring(mode.IndexOf(':') + 1) : "all")).Trim().ToLowerInvariant();
            var all = new[] { "stab", "blunt", "strangle", "dismember" };
            var kinds = arg == "all" || arg.Length == 0 ? all : arg.Split(new[] { ',', '|', '+', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).Where(k => k.Length > 0).ToArray();
            foreach (var k in kinds) if (!all.Contains(k)) Log($"unknown discovery case '{k}' (Gore.DebugStage knows {string.Join("|", all)})");
            ulong seed0 = ulong.TryParse(GameBoot.I.ArgValue("-bl23seed") ?? "", out var sd) ? sd : 20260926UL;
            Log($"discovery probe: cases [{string.Join(",", kinds)}] base seed {seed0}");
            var results = new List<DiscResult>();
            for (int i = 0; i < kinds.Length; i++)
            {
                var res = new DiscResult { Kind = kinds[i] }; results.Add(res);
                Log($"==== discovery case {kinds[i]} ({i + 1}/{kinds.Length})");
                yield return DiscCase(kinds[i], seed0 + (ulong)i, res);
                Log(res.Summary());
            }
            Log("==== discovery summary");
            foreach (var r in results) Log(r.Summary());
            Log("discovery probe done"); Flush();
            yield return Wait(1f);
            Application.Quit();
        }

        IEnumerator DiscCase(string kind, ulong seed, DiscResult res)
        {
            int err0 = _errors;
            GameBoot.I.NewGame(seed); Log($"new game seed {seed}");
            yield return Wait(1f);
            Ses.Headless = true;
            yield return Until(() => Ses != null && S.Phase == Phase.Daily && !Ses.Cine.Busy, 60, "daily");
            Ses.Headless = false;
            yield return Wait(1f);
            Log($"Settings.Gore = {Settings.Gore}");

            // ---- what the film must give back (recorded before anything happens: a film that starts early cannot taint it)
            var pc = Ses.Player; var pv = Ses.World.ViewOf(Cast.Player);
            var film = Ses.Cine.FilmCam;
            int pcMask0 = pc.Cam.GetUniversalAdditionalCameraData()?.volumeLayerMask.value ?? -1;
            int fcMask0 = film != null ? film.GetUniversalAdditionalCameraData()?.volumeLayerMask.value ?? -1 : -1;
            float fcFov0 = film != null ? film.fieldOfView : -1f;
            float amb0 = AmbienceDirector.I != null ? AmbienceDirector.I.MasterDb : 0f;
            float fov0 = pc.Cam.fieldOfView;

            // ---- 1) the kernel stages the murder
            int traces0 = S.Traces.Count; string vid = null;
            try { vid = Gore.DebugStage(Ses.Sim, kind); } catch (Exception e) { Log("DebugStage threw: " + e); }
            if (string.IsNullOrEmpty(vid) || S.A(vid) == null) { res.Fails.Add("Gore.DebugStage returned no victim (kernel stub?)"); yield break; }
            var v = S.A(vid); var inc = S.Incidents.Values.Where(x => x.Victim == vid).OrderByDescending(x => x.ResultSeq).FirstOrDefault();
            var newTraces = S.Traces.Skip(traces0).Select(t => t.Type).ToList();
            var pieces = Gore.PiecesOf(S, vid).ToList();
            int stageRoom = v.Room;
            Log($"staged {kind}: victim {vid} ({Cast.NameOf(vid)}) status {v.Status} culprit {inc?.Culprit} room {v.Room} ({S.RoomName(v.Room)}) weapon {inc?.Weapon} marks {Gore.MarksOf(v).Count} new traces [{string.Join(",", newTraces)}] pieces [{string.Join(",", pieces.Select(p => p.Id + ":" + p.Note))}]");
            float gw = Time.realtimeSinceStartup;
            while (!DiscoveryFilm.GoreReady() && Time.realtimeSinceStartup - gw < 15f) { _ft.Add(Time.unscaledDeltaTime); yield return null; }
            Log($"GoreScene.Ready={DiscoveryFilm.GoreReady()} after {Time.realtimeSinceStartup - gw:0.0}s");
            yield return Wait(1.0f);
            // the body must lie where the kernel killed it (a view left behind by the teleport would fall elsewhere)
            var bv = Ses.World.ViewOf(vid);
            if (bv != null)
            {
                var kw = Ses.World.ToWorld(v.Pos); var hips = CineAnchors.Hips(vid); float dh = new Vector2(hips.x - kw.x, hips.z - kw.z).magnitude;
                Log($"victim body: hips {hips:F2}, kernel {kw:F2} (Δ {dh:0.00} m), room now {v.Room} (staged {stageRoom})");
                res.Check(dh <= 1.6f && v.Room == stageRoom, $"victim's body is not where the kernel staged it (Δ {dh:0.00} m, room {v.Room} vs {stageRoom})");
            }
            // a film that started on its own before the walk-in (민혁 saw it from where he stood): let it play out, note it
            if (Ses.Cine.Busy)
            {
                Log("premature film: it started before the walk-in — waiting it out"); res.Notes.Add("premature film");
                float pt = Time.realtimeSinceStartup; while (Ses.Cine.Busy && Time.realtimeSinceStartup - pt < 20f) { _ft.Add(Time.unscaledDeltaTime); yield return null; }
                yield return Wait(6.5f);
            }
            bool vfv0 = bv != null && bv.ForceVisible;

            // ---- 2) walk in through the door (opened first)
            var room = S.Layout.Room(v.Room);
            P3? outside = DoorApproach(v, room, out var door);
            if (door != null && (!door.Open || door.Locked)) { try { Ses.Sim.SetDoor(null, door, true, false, "probe"); Log($"door {door.Id} opened for the walk-in"); } catch (Exception e) { Log("door: " + e.Message); } }
            string path = "none";
            var shotsSeen = new List<string>(); var p01Bad = new List<string>(); var ft = new List<float>();
            Action<string> onShot = n =>
            {
                shotsSeen.Add(n); Shot($"{kind}_{n}");
                if (n.StartsWith("i_"))
                {
                    int c = P01InFrame(film); if (c > 0) p01Bad.Add($"{n}:{c}");
                    Log($"   {n}: P01 renderers in frame {c}" + (film != null && film.enabled ? $" · lens {film.transform.position:F2} fwd {film.transform.forward:F2} fov {film.fieldOfView:0.0}" : ""));
                }
            };
            DiscoveryFilm.OnShot += onShot;
            try
            {
                if (outside.HasValue)
                {
                    var o = outside.Value;
                    pc.Teleport(o, MathX.AngleDeg(v.Pos.x - o.x, v.Pos.z - o.z), 8f);
                    yield return Wait(0.6f);
                    if (!Ses.Cine.Busy) yield return ShotCo($"{kind}_doorway_before");
                    pc.ProbeRunUntil = Time.time + 0.5f;
                    float t0 = Time.realtimeSinceStartup; while (!Ses.Cine.Busy && Time.realtimeSinceStartup - t0 < 8f) { _ft.Add(Time.unscaledDeltaTime); yield return null; }
                    if (Ses.Cine.Busy) path = "walk-in";
                }
                if (!Ses.Cine.Busy)
                {
                    var inside = NearIn(v.Pos, v.Room, 1.6f);
                    pc.Teleport(inside, MathX.AngleDeg(v.Pos.x - inside.x, v.Pos.z - inside.z), 20f);
                    float t0 = Time.realtimeSinceStartup; while (!Ses.Cine.Busy && Time.realtimeSinceStartup - t0 < 4f) { _ft.Add(Time.unscaledDeltaTime); yield return null; }
                    if (Ses.Cine.Busy) path = "inside";
                }
                if (!Ses.Cine.Busy) { Ses.Cine.Discovery(vid); yield return null; if (Ses.Cine.Busy) path = "by hand"; }
                Log($"discovery path: {path} (incident discovered {inc?.Discovered}, discoverers [{string.Join(",", inc?.Discoverers ?? new List<string>())}])");
                res.Notes.Add("path " + path);
                if (!Ses.Cine.Busy) { res.Fails.Add("the film never started"); yield break; }

                // ---- 3) during the film
                float fs = Time.realtimeSinceStartup; bool m0 = false, m9 = false;
                while (Ses.Cine.Busy && Time.realtimeSinceStartup - fs < 15f)
                {
                    float el = Time.realtimeSinceStartup - fs; ft.Add(Time.unscaledDeltaTime); _ft.Add(Time.unscaledDeltaTime);
                    if (!m0) { m0 = true; Log("   music +0: " + (MusicDirector.I?.Describe() ?? "-")); }
                    if (!m9 && el >= 9f) { m9 = true; Log("   music +9: " + (MusicDirector.I?.Describe() ?? "-")); }
                    yield return null;
                }
                float dur = Time.realtimeSinceStartup - fs;
                if (!m9) { yield return Wait(Mathf.Max(0f, 9f - dur)); Log("   music +9: " + (MusicDirector.I?.Describe() ?? "-")); }
                bool registered = MusicLibrary.Track("disc_theme") != null;
                if (registered) res.Check(MusicDirector.I != null && MusicDirector.I.NowPlaying == "disc_theme", "music not disc_theme: " + MusicDirector.I?.Describe());
                else res.Notes.Add("disc_theme not registered yet (music logged only)");
                var tm = DiscoveryFilm.LastTimings ?? "";
                bool full = tm.Contains("full=True");
                Log($"film duration {dur:0.00}s ({(full ? "full" : "short")}) shots [{string.Join(",", shotsSeen)}]");
                res.Check(full ? dur <= 10.5f : dur <= 7.0f, $"film too long {dur:0.00}s");
                Log("plan:\n" + DiscoveryFilm.LastPlanLog);
                Log("timings:" + tm);
                if (ft.Count > 0) { var s = ft.OrderBy(x => x).ToList(); float p95 = s[(int)(s.Count * 0.95f)] * 1000f; Log($"film frames avg {s.Average() * 1000f:0.0} ms p95 {p95:0.0} ms (n={s.Count})"); res.Notes.Add($"p95 {p95:0.0}ms"); }
                res.Check(p01Bad.Count == 0, "민혁 in frame: " + string.Join(",", p01Bad));
                float hitErr = DiscoveryFilm.LastHitDspErrMs; Log($"sting sync |dsp − frame| = {Mathf.Abs(hitErr):0} ms"); res.Check(Mathf.Abs(hitErr) <= 40f, $"sting sync {hitErr:0}ms");
                int inserts = shotsSeen.Count(x => x.StartsWith("i_"));
                res.Check(inserts > 0 || tm.Contains("fewer than 2 inserts"), "no inserts and no reason logged");
                if (inserts < 2) res.Notes.Add($"only {inserts} insert(s)");
                if (full && path != "by hand") res.Check(inserts >= 2, $"only {inserts} insert(s) in the full film");
                // D0: the raised hands were in the lower part of the frame (not the fallback, not a title-only film)
                if (!tm.Contains("fallback") && !tm.Contains("title only"))
                {
                    Log("hands at D0: " + DiscoveryFilm.LastHandsVp + (DiscoveryFilm.LastHandsOk ? " (ok)" : " (NOT in the lower frame)"));
                    res.Check(DiscoveryFilm.LastHandsOk, "hands not in the lower frame at D0 (" + DiscoveryFilm.LastHandsVp + ")");
                }

                // ---- 4) the restore
                yield return null; _snap = SnapLights(); yield return null;
                CheckRestore(res, "film", pc, pv, bv, vfv0, fov0, film, fcFov0, amb0);
                yield return Wait(6.2f);
                CheckGrade(res, "film", pc, film, pcMask0, fcMask0);

                // ---- 5) what the body itself shows: a severed head gone from the neck, a strangled neck's furrow, budgets
                BodyGates(kind, vid, res);

                // ---- 6) stills of the gore, the body marks and the doorway
                yield return DiscStills(kind, vid, door, res);

                // ---- 7) skip (stab only), from inside the room this time
                if (kind == "stab")
                {
                    var inside = NearIn(v.Pos, v.Room, 1.4f);
                    pc.Teleport(inside, MathX.AngleDeg(v.Pos.x - inside.x, v.Pos.z - inside.z), 25f);
                    yield return Wait(0.8f);
                    if (Ses.Cine.Busy) { float bt = Time.realtimeSinceStartup; while (Ses.Cine.Busy && Time.realtimeSinceStartup - bt < 15f) yield return null; yield return Wait(6.5f); }
                    _snap = null;
                    Ses.Cine.Discovery(vid); yield return null;
                    if (!Ses.Cine.Busy) res.Fails.Add("skip test: second film did not start");
                    else
                    {
                        yield return Wait(2.5f);
                        DiscoveryFilm.ProbeSkip = true; float sk = Time.realtimeSinceStartup;
                        while (Ses.Cine.Busy && Time.realtimeSinceStartup - sk < 5f) { _ft.Add(Time.unscaledDeltaTime); yield return null; }
                        float back = Time.realtimeSinceStartup - sk;
                        Log($"skip: control back after {back:0.00}s"); res.Check(back <= 1.5f, $"skip took {back:0.00}s");
                        Log("skip plan:\n" + DiscoveryFilm.LastPlanLog);
                        Log("skip timings:" + DiscoveryFilm.LastTimings);
                        yield return null; _snap = SnapLights(); yield return null;
                        CheckRestore(res, "skip", pc, pv, bv, vfv0, fov0, film, fcFov0, amb0);
                        yield return Wait(6.2f);
                        CheckGrade(res, "skip", pc, film, pcMask0, fcMask0);
                    }
                }
            }
            finally { DiscoveryFilm.OnShot -= onShot; FilmOverlays.Show(); }

            // ---- 8) pink scan over every picture of this case
            yield return null; yield return null;
            PinkScan(kind, res);
            // ---- 9) no errors
            int errs = _errors - err0; Log($"errors in case: {errs}"); res.Check(errs == 0, $"{errs} errors/exceptions");
        }

        /// <summary>1.2 m outside the room's door (on the other side), or null; the door it goes through.</summary>
        P3? DoorApproach(Actor v, Room room, out Door door)
        {
            door = null;
            if (room == null) return null;
            foreach (var d in S.Layout.Doors.Where(d => d.RoomA == room.Id || d.RoomB == room.Id).Where(d => !d.Sealed).OrderBy(d => d.Pos.DistXZ(v.Pos)))
            {
                var nav = S.Layout.Nav(d.Pos.f);
                float ox = d.AlongX ? 0f : Mathf.Sign(d.Pos.x - room.Rect.CX), oz = d.AlongX ? Mathf.Sign(d.Pos.z - room.Rect.CZ) : 0f;
                foreach (float dist in new[] { 1.2f, 0.9f, 1.5f })
                {
                    var p = new P3(d.Pos.f, d.Pos.x + ox * dist, d.Pos.z + oz * dist);
                    int k = nav.CellOf(p.x, p.z);
                    if (nav.Walkable(k) && S.Layout.RoomAt(p) != room.Id) { door = d; return p; }
                }
            }
            return null;
        }

        int P01InFrame(Camera cam)
        {
            var rig = Ses?.World?.ViewOf(Cast.Player)?.Rig; if (rig == null || cam == null || !cam.enabled) return 0;
            var planes = GeometryUtility.CalculateFrustumPlanes(cam); int n = 0;
            foreach (var r in rig.GetComponentsInChildren<Renderer>()) if (r != null && r.enabled && r.gameObject.activeInHierarchy && GeometryUtility.TestPlanesAABB(planes, r.bounds)) n++;
            return n;
        }

        Dictionary<Light, float> _snap;
        static Dictionary<Light, float> SnapLights() { var d = new Dictionary<Light, float>(); foreach (var kv in DiscoveryFilm.LightBases) if (kv.Key != null) d[kv.Key] = kv.Key.intensity; return d; }

        void CheckRestore(DiscResult res, string tag, PlayerController pc, ActorView pv, ActorView bv, bool vfv0, float fov0, Camera film, float fcFov0, float amb0)
        {
            var sb = new StringBuilder($"restore ({tag}):");
            void C(bool ok, string what) { sb.Append(ok ? " ok:" : " FAIL:").Append(what); res.Check(ok, tag + " " + what); }
            C(!pc.Scripted && pc.Controlling && pc.Cam.enabled, "control");
            C(Mathf.Abs(pc.Cam.fieldOfView - fov0) <= 1f || Mathf.Abs(pc.Cam.fieldOfView - Settings.Fov) <= 1f, $"fov {pc.Cam.fieldOfView:0.0} (before {fov0:0.0}, setting {Settings.Fov:0.0})");
            C(!Ses.IsPaused("cine"), "cine unpaused");
            C(pv == null || !pv.ForceHidden, "P01 not hidden");
            C(bv == null || bv.ForceVisible == vfv0, "victim ForceVisible restored");
            // lights: every managed light back at its recorded base (flickering ones move on their own and are skipped)
            var mv = Ses.World?.Mansion; var flick = new HashSet<Light>();
            if (_snap != null) foreach (var kv in _snap) if (kv.Key != null && Mathf.Abs(kv.Key.intensity - kv.Value) > 1e-4f) flick.Add(kv.Key);   // animated by someone else this very frame
            if (mv != null) foreach (var lr in mv.AllLights) if (lr.Light != null && (lr.Flicker > 0f || lr.Fire)) flick.Add(lr.Light);
            int bad = 0, n = 0;
            foreach (var kv in DiscoveryFilm.LightBases)
            {
                if (kv.Key == null || flick.Contains(kv.Key)) continue; n++;
                float b = kv.Value; if (Mathf.Abs(kv.Key.intensity - b) > Mathf.Max(0.01f * Mathf.Abs(b), 1e-4f)) bad++;
            }
            C(bad == 0, $"lights {n - bad}/{n} at base");
            var hud = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name == "HUD");
            C(hud == null || hud.enabled, "HUD enabled");
            C(!FilmOverlays.Hidden, "overlays back");
            C(AmbienceDirector.I == null || Mathf.Abs(AmbienceDirector.I.MasterDb - amb0) < 0.01f, $"ambience {AmbienceDirector.I?.MasterDb ?? 0:0.0} dB");
            C(film == null || (!film.enabled && Mathf.Abs(film.fieldOfView - fcFov0) < 0.01f), "film cam off, FOV back");
            C(DiscoveryFilm.Active == null, "no active film");
            C(GameObject.Find("FilmHandFill") == null && GameObject.Find("FilmGazeLight") == null, "film lights gone");
            Log(sb.ToString());
        }

        void CheckGrade(DiscResult res, string tag, PlayerController pc, Camera film, int pcMask0, int fcMask0)
        {
            var g = DiscoveryGrade.I;
            bool off = g == null || !g.gameObject.activeInHierarchy || g.Weight <= 0.001f;
            int pm = pc.Cam.GetUniversalAdditionalCameraData()?.volumeLayerMask.value ?? -1;
            int fm = film != null ? film.GetUniversalAdditionalCameraData()?.volumeLayerMask.value ?? -1 : -1;
            Log($"grade ({tag}) after 6 s: weight {(g != null ? g.Weight : 0f):0.000} active {(g != null && g.gameObject.activeInHierarchy)} masks pc {pm}/{pcMask0} film {fm}/{fcMask0}");
            res.Check(off, tag + " grade still on after 6 s");
            res.Check(pm == pcMask0 && fm == fcMask0, tag + " camera volume masks not restored");
        }

        /// <summary>The body itself: after a head cut nothing may be drawn where the head was (the cut skins must carry their
        /// cut with head triangles removed); a strangled neck carries its furrow; the piece bake, torso cut and mesh budgets.</summary>
        void BodyGates(string kind, string vid, DiscResult res)
        {
            var bv = Ses.World.ViewOf(vid); if (bv == null) { res.Fails.Add("victim view gone"); return; }
            var gt = bv.GetComponent<GoreTorso>();
            if (gt != null && gt.Mask != 0)
            {
                Log(gt.LastReport);
                Log($"torso cut {gt.LastApplyMs:0.0} ms · pieces: bake {GorePieces.LastBakeMs:0.0} ms (pose {GorePieces.LastPoseMs:0.0}, build {GorePieces.LastBuildMs:0.0}) · piece meshes {GorePieces.MeshBytes / 1048576f:0.00} MB");
                res.Check(gt.LastApplyMs <= 80f, $"torso cut took {gt.LastApplyMs:0.0} ms (a one-off hitch over 5 frames)");
                res.Check(GorePieces.LastBakeMs <= 8f, $"piece bake {GorePieces.LastBakeMs:0.0} ms > 8");
                res.Check(GorePieces.MeshBytes <= 4L * 1048576L, $"piece meshes {GorePieces.MeshBytes / 1048576f:0.00} MB > 4");
                if ((gt.Mask & GoreMeshCut.Bit(SeverPart.Head)) != 0)
                {
                    var left = gt.HeadLeftovers();
                    Log("head cut: " + (left.Count == 0 ? "the neck is clean" : "LEFT ON THE NECK: " + string.Join(", ", left)));
                    res.Check(left.Count == 0, "head geometry left on the neck: " + string.Join(", ", left));
                }
            }
            else if (kind == "dismember") res.Fails.Add("no torso cut on the dismembered body");
            var bm = bv.GetComponent<GoreBodyMarks>();
            if (bm != null) Log(bm.LastReport);
            if (kind == "strangle") res.Check(bm != null && bm.LastReport.Contains("ligature"), "no ligature furrow on the strangled neck");
        }

        // ------------------------------------------------------------------ stills
        IEnumerator DiscStills(string kind, string vid, Door door, DiscResult res)
        {
            var targets = new List<(string what, Vector3 p, Vector3 n)>();
            foreach (var h in DiscoveryFilm.Hotspots(vid)) targets.Add((h.Kind.ToString().ToLowerInvariant(), h.Pos, h.Normal));
            foreach (var p in DiscoveryFilm.PiecesOf(vid)) if (!p.Hidden) targets.Add(("piece" + p.Part, p.Center, Vector3.up));
            var body = Ses.World.ViewOf(vid);
            if (body != null)
            {
                var gt = body.GetComponent<GoreTorso>();
                if (gt != null && (gt.Mask & GoreMeshCut.Bit(SeverPart.Head)) != 0 && body.Rig != null && body.Rig.HasBone(HBone.Neck)) targets.Insert(0, ("neck", body.Rig.Bone(HBone.Neck).position, Vector3.up));
                var bm = body.GetComponent<GoreBodyMarks>();
                if (bm != null && bm.NeckPoint != Vector3.zero) targets.Insert(0, ("neck", bm.NeckPoint, Vector3.up));
                if (bm != null && body.Rig != null && body.Rig.HasBone(HBone.HandR)) targets.Insert(Math.Min(1, targets.Count), ("hand", body.Rig.Bone(HBone.HandR).position, Vector3.up));
                if (targets.Count == 0) targets.Add(("body", CineAnchors.Body(vid), Vector3.up));
            }
            // the weapon, where it lies (its blade should carry the blood: GoreItems)
            var wInc = S.Incidents.Values.Where(x => x.Victim == vid).OrderByDescending(x => x.ResultSeq).FirstOrDefault();
            var wIt = wInc != null ? S.I(wInc.Weapon) : null;
            if (wIt != null && wIt.Holder == null && !wIt.Hidden && Ses.World.Items.TryGetValue(wIt.Id, out var wiv) && wiv != null && wiv.Visual != null)
            {
                var rs = wiv.Visual.GetComponentsInChildren<Renderer>(); var wb = rs.Length > 0 ? rs[0].bounds : new Bounds(wiv.Visual.transform.position, Vector3.one * 0.1f); foreach (var r in rs) wb.Encapsulate(r.bounds);
                var bb = wiv.Visual.GetComponent<GoreBladeBlood>();
                Log($"weapon {wIt.Type} {wIt.Id}: bloody {wIt.Bloody} · blade blood {(bb != null ? (bb.Built ? "built" : "none (no flat steel blade)") : "not attached")}");
                targets.Insert(Math.Min(2, targets.Count), ("weapon", wb.center, Vector3.up));
            }
            var v = S.A(vid); var room = S.Layout.Room(v?.Room ?? -1); var mv = Ses.World.Mansion;
            FilmOverlays.Hide(true);
            int n = 0;
            try
            {
                foreach (var (what, p, nrm) in targets)
                {
                    if (n >= 9) break;
                    var go = new GameObject("ProbeDiscCam"); var cam = go.AddComponent<Camera>();
                    BL23.Game.Mansion.MansionAtmosphere.SetupCamera(cam); cam.depth = 60; cam.fieldOfView = 40; cam.nearClipPlane = 0.03f;
                    var eye = Ses.Player.Cam.transform.position;
                    bool ok = PlaceStill(p, nrm, eye, room, vid, n, out var pos, out string why);
                    cam.transform.position = pos; cam.transform.LookAt(p);
                    if (body != null) body.ForceVisible = true; mv?.Cull(pos);
                    Log($"   still {what}_{n}: cam {pos:F2} → {p:F2} ({Vector3.Distance(pos, p):0.00} m){(ok ? "" : " — best effort: " + why)}");
                    yield return Wait(0.35f); yield return ShotCo($"{kind}_still_{what}_{n}");
                    if (body != null) body.ForceVisible = false; Destroy(go); n++;
                }
                // the doorway: from just inside the threshold, at eye height, looking at the body
                if (door != null && body != null && room != null)
                {
                    var dp = Ses.World.ToWorld(door.Pos); var inward = door.AlongX ? new Vector3(0, 0, Mathf.Sign(room.Rect.CZ - door.Pos.z)) : new Vector3(Mathf.Sign(room.Rect.CX - door.Pos.x), 0, 0);
                    var pos = dp + inward * 0.35f + Vector3.up * 1.6f; var look = CineAnchors.Body(vid);
                    var go = new GameObject("ProbeDiscCam"); var cam = go.AddComponent<Camera>();
                    BL23.Game.Mansion.MansionAtmosphere.SetupCamera(cam); cam.depth = 60; cam.fieldOfView = 60; cam.nearClipPlane = 0.05f;
                    cam.transform.position = pos; cam.transform.LookAt(look);
                    body.ForceVisible = true; mv?.Cull(pos);
                    Log($"   still doorway_{n}: cam {pos:F2} → {look:F2}");
                    yield return Wait(0.5f); yield return ShotCo($"{kind}_still_doorway_{n}");
                    body.ForceVisible = false; Destroy(go); n++;
                }
                mv?.Cull(Ses.Player.Cam.transform.position);
            }
            finally { FilmOverlays.Show(); }
            int decals = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Count(m => m != null && m.GetType().Name == "GoreDecalView");
            int vis = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r => r.isVisible);
            int seeps = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r => r != null && r.name == "GoreSeep");
            int blades = FindObjectsByType<GoreBladeBlood>(FindObjectsSortMode.None).Count(b => b != null && b.Built);
            int moths = FindObjectsByType<GoreMoths>(FindObjectsSortMode.None).Sum(m => m != null ? m.Count : 0);
            Log($"stills {n} · GorePieces.LastBakeMs {DiscoveryFilm.GoreLastBakeMs():0.0} · decals {decals} · seeps under doors {seeps} · bloody blades {blades} · moths {moths} · visible renderers {vis}");
        }

        /// <summary>A still camera 0.75–1.2 m from p, clear of geometry: along a wall's normal (a little raised) or round a
        /// floor spot at 35° (from below for a ceiling), starting from 민혁's side; sphere-cast out from the spot so it stops
        /// short of anything behind, never inside a collider, inside the room, with a clear line to p.</summary>
        bool PlaceStill(Vector3 p, Vector3 n, Vector3 eye, Room room, string vid, int k, out Vector3 pos, out string why)
        {
            var mv = Ses.World.Mansion; int roomId = room != null ? room.Id : -1;
            var toEye = eye - p; toEye.y = 0; toEye = toEye.sqrMagnitude > 1e-4f ? toEye.normalized : Vector3.forward;
            var dirs = new List<Vector3>();
            if (n.sqrMagnitude > 0.1f && Mathf.Abs(n.y) < 0.7f)
            {
                var h = new Vector3(n.x, 0, n.z).normalized;
                foreach (float turn in new[] { 0f, 25f, -25f, 45f, -45f }) { var d = Quaternion.AngleAxis(turn, Vector3.up) * h; dirs.Add((d * Mathf.Cos(18f * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(18f * Mathf.Deg2Rad)).normalized); }
            }
            else
            {
                float el = n.y < -0.7f ? -40f : 35f;
                foreach (float turn in new[] { 0f, 40f, -40f, 80f, -80f, 120f, -120f, 180f })
                {
                    var d = Quaternion.AngleAxis(turn + (k % 2 == 0 ? 12f : -12f), Vector3.up) * toEye;
                    dirs.Add((d * Mathf.Cos(el * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(el * Mathf.Deg2Rad)).normalized);
                }
            }
            why = "no clear spot";
            Vector3 first = p + dirs[0] * 0.9f; bool haveFirst = false;
            foreach (var d in dirs)
                foreach (float want in new[] { 1.0f, 0.8f, 1.2f })
                {
                    var start = p + (n.sqrMagnitude > 0.1f ? n.normalized : Vector3.up) * 0.03f;
                    float dist = want;
                    foreach (var h in Physics.SphereCastAll(start, 0.1f, d, want, ~0, QueryTriggerInteraction.Ignore))
                        if (!StillIgnorable(h.collider, vid) && h.distance > 0f) dist = Mathf.Min(dist, h.distance - 0.12f);
                    if (dist < 0.45f) { why = "blocked right behind the spot"; continue; }
                    var c = start + d * dist;
                    if (!haveFirst) { first = c; haveFirst = true; }
                    if (mv != null && roomId >= 0 && mv.RoomAtWorld(c) != roomId) { why = "outside the room"; continue; }
                    bool inside = false; foreach (var col in Physics.OverlapSphere(c, 0.07f, ~0, QueryTriggerInteraction.Ignore)) if (!StillIgnorable(col, vid)) { inside = true; break; }
                    if (inside) { why = "inside geometry"; continue; }
                    if (!StillClear(c, p, vid)) { why = "line to the spot blocked"; continue; }
                    pos = c; return true;
                }
            pos = first; return false;
        }

        bool StillIgnorable(Collider c, string vid)
        {
            if (c == null) return true;
            var av = c.GetComponentInParent<ActorView>(); if (av != null && (av.Id == vid || av.Id == Cast.Player)) return true;
            if (c.GetComponentInParent<PlayerController>() != null) return true;
            var part = c.GetComponentInParent<BL23.Game.Physicality.PhysicalBodyPart>(); if (part != null) return true;
            if (c.GetComponentInParent<TraceView>() != null) return true;
            var tag = c.GetComponentInParent<ItemTag>(); if (tag != null) { var it = S.I(tag.ItemId); if (it != null && it.Owner == vid && it.Type == "SeveredPart") return true; }
            return false;
        }

        bool StillClear(Vector3 a, Vector3 b, string vid)
        {
            var d = b - a; float len = d.magnitude; if (len < 0.02f) return true; d /= len;
            foreach (var h in Physics.RaycastAll(a, d, len, ~0, QueryTriggerInteraction.Ignore))
            {
                if (len - h.distance < 0.12f) continue;   // the surface the spot lies on
                if (StillIgnorable(h.collider, vid)) continue;
                return false;
            }
            return true;
        }

        /// <summary>Fail when more than 0.2 % of a picture's pixels are pink (hue 300–345°, S &gt; 0.5, V &gt; 0.3).</summary>
        void PinkScan(string kind, DiscResult res)
        {
            int files = 0;
            foreach (var f in Directory.GetFiles(_dir, "*.png").Where(f => Path.GetFileName(f).Contains("_" + kind + "_")).OrderBy(f => f))
            {
                try
                {
                    var tex = new Texture2D(2, 2); if (!tex.LoadImage(File.ReadAllBytes(f))) { Destroy(tex); continue; }
                    var px = tex.GetPixels32(); int pink = 0;
                    for (int i = 0; i < px.Length; i += 2)
                    {
                        Color.RGBToHSV(px[i], out float hh, out float ss, out float vv);
                        float deg = hh * 360f; if (deg >= 300f && deg <= 345f && ss > 0.5f && vv > 0.3f) pink++;
                    }
                    float frac = pink / Mathf.Max(1f, px.Length / 2f); files++;
                    if (frac > 0.002f) { res.Fails.Add($"pink {frac * 100f:0.00}% in {Path.GetFileName(f)}"); Log($"   PINK {frac * 100f:0.00}% {Path.GetFileName(f)}"); }
                    Destroy(tex);
                }
                catch (Exception e) { Log("pink scan " + f + ": " + e.Message); }
            }
            Log($"pink scan: {files} pictures");
        }
    }
}
