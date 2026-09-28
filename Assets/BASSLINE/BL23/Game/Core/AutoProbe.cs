using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Automated play-through for verification (command line: -bl23probe [full|rooms] -bl23out DIR -bl23seed N).
    /// Drives the real game through title → prologue → daily → dialogue → notebook → rooms → murder → investigation →
    /// trial → verdict → reveal → next chapter, capturing screenshots and a log (errors, frame times, phase timeline).
    /// </summary>
    public sealed partial class AutoProbe : MonoBehaviour
    {
        // per-feature probe extensions live in partial files (AutoProbe.Clues.cs, AutoProbe.Qol.cs); each adds coroutines to run at a named point
        partial void CluesHook(string at, List<IEnumerator> run);   // AutoProbe.Clues.cs
        partial void QolHook(string at, List<IEnumerator> run);     // AutoProbe.Qol.cs
        partial void TimeHook(string at, List<IEnumerator> run);    // AutoProbe.Time.cs (time-on-demand)
        partial void ConcealHook(string at, List<IEnumerator> run);  // AutoProbe.Conceal.cs (hiding things on the body and in furniture)
        partial void ViolenceHook(string at, List<IEnumerator> run); // AutoProbe.Violence.cs (violence track: holds, restraint, guns, reactions)
        IEnumerator Hooks(string at) { var l = new List<IEnumerator>(); CluesHook(at, l); QolHook(at, l); TimeHook(at, l); ConcealHook(at, l); ViolenceHook(at, l); foreach (var e in l) yield return e; }

        public static bool Active; static AutoProbe _i;
        string _dir; readonly StringBuilder _log = new StringBuilder(); int _errors, _shots; readonly List<string> _errFirst = new List<string>();
        readonly List<float> _ft = new List<float>(); float _t0;
        readonly HashSet<string> _pendingShots = new HashSet<string>();

        public void Run(string mode)
        {
            Active = true; _i = this; _t0 = Time.realtimeSinceStartup;
            _dir = GameBoot.I.ArgValue("-bl23out") ?? Path.Combine(Application.persistentDataPath, "probe");
            Directory.CreateDirectory(_dir);
            Settings.SaveDirOverride = Path.Combine(_dir, "saves"); Directory.CreateDirectory(Settings.SaveDirOverride);
            Application.logMessageReceived += OnLog;
            int w = 1920, h = 1080; var res = GameBoot.I.ArgValue("-bl23res"); if (res != null && res.Contains("x")) { int.TryParse(res.Split('x')[0], out w); int.TryParse(res.Split('x')[1], out h); }
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
            // the probe must not leave its settings behind in the player's preferences
            _prevUi = PlayerPrefs.HasKey("bl23.uiscale") ? Settings.UIScale : -1f;
            Application.quitting += RestorePrefs;
            // who ends the run? (three probes stopped early with a clean exit) — log the managed stack when a quit is requested
            Application.wantsToQuit += () => { Log("quit requested at phase " + (Session.I?.S?.Phase.ToString() ?? "-") + "\n" + Environment.StackTrace); Flush(); return true; };
            Application.quitting += () => { Log("quitting (focus " + Application.isFocused + ")"); Flush(); };
            if (float.TryParse(GameBoot.I.ArgValue("-bl23uiscale") ?? "", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ui)) { Settings.UIScale = ui; Settings.Apply(); }
            // measure the real frame cost: no vsync / frame cap while probing (vsync quantises times to 8.3/16.7/33 ms)
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            // ---- [discovery probe: AutoProbe.Discovery.cs] ----
            StartCoroutine(mode != null && mode.StartsWith("disc") ? DiscoveryTour(mode) : mode == "rooms" ? Rooms() : mode == "ui" ? UiTour() : mode == "trial" ? TrialTour() : mode == "perf" ? PerfTour() /* AutoProbe.Perf.cs */ : Full());
        }

        void OnLog(string msg, string stack, LogType t)
        {
            if (t == LogType.Exception || t == LogType.Error || t == LogType.Assert)
            {
                _errors++; if (_errFirst.Count < 60) _errFirst.Add($"[{t}] {msg}\n{stack?.Split('\n').Take(6).Aggregate("", (a, b) => a + "    " + b + "\n")}");
            }
        }

        void Log(string s) { var line = $"[{Time.realtimeSinceStartup - _t0,7:0.0}s] {s}"; _log.AppendLine(line); Debug.Log("[PROBE] " + s); Flush(); }
        void Flush()
        {
            try
            {
                var sb = new StringBuilder(_log.ToString());
                sb.AppendLine().AppendLine($"errors: {_errors}"); foreach (var e in _errFirst) sb.AppendLine(e);
                if (_ft.Count > 0) { var s = _ft.OrderBy(x => x).ToList(); sb.AppendLine($"frame ms: avg {s.Average() * 1000:0.0} p50 {s[s.Count / 2] * 1000:0.0} p95 {s[(int)(s.Count * 0.95f)] * 1000:0.0} max {s.Last() * 1000:0.0} (n={s.Count})"); }
                File.WriteAllText(Path.Combine(_dir, "probe_log.txt"), sb.ToString());
            }
            catch (Exception) { }
        }

        /// <summary>Request a screenshot from anywhere (captured at the end of the current frame).</summary>
        public static void Shot(string name) { if (_i != null) _i.StartCoroutine(_i.ShotCo(name)); }
        IEnumerator ShotCo(string name)
        {
            yield return new WaitForEndOfFrame();
            try
            {
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                var png = tex.EncodeToPNG(); Destroy(tex);
                var file = Path.Combine(_dir, $"{++_shots:00}_{name}.png"); File.WriteAllBytes(file, png);
                { var recent = _ft.Skip(Math.Max(0, _ft.Count - 30)).ToList(); int lights = FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled); int shadowL = FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled && l.shadows != LightShadows.None); int rends = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r => r.isVisible); Log($"shot {Path.GetFileName(file)}  phase={Session.I?.S?.Phase} clock={(Session.I != null ? ClockFmt.DayHM(Session.I.S.Clock) : "-")} recentMs={(recent.Count > 0 ? recent.Average() * 1000 : 0):0} lights={lights} shadowLights={shadowL} visibleRenderers={rends}"); }
                var mc = Session.I?.Player?.Cam; if (mc != null && mc.isActiveAndEnabled && Session.I.World?.Mansion != null) { var cp = mc.transform.position; var near = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.isVisible && r.bounds.SqrDistance(cp) < 0.45f * 0.45f).Select(r => r.name).Distinct().Take(6); Log($"   cam {mc.name} pos {cp:F2} fwd {mc.transform.forward:F2} near {mc.nearClipPlane:F2} room {Session.I.World.Mansion.RoomAtWorld(cp)} close [{string.Join(", ", near)}]"); }
            }
            catch (Exception e) { Log("shot failed " + name + ": " + e.Message); }
        }

        /// <summary>A place about `dist` from `at` inside the same room (never through a wall): tries eight directions.</summary>
        P3 NearIn(P3 at, int room, float dist)
        {
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f + 0.35f; var p = new P3(at.f, at.x + Mathf.Cos(a) * dist, at.z + Mathf.Sin(a) * dist);
                if (S.Layout.RoomAt(p) == room && S.Layout.RoomAt(new P3(at.f, (at.x + p.x) * 0.5f, (at.z + p.z) * 0.5f)) == room) return p;
            }
            return dist > 0.6f ? NearIn(at, room, dist * 0.6f) : at;
        }

        IEnumerator Wait(float secs) { float t = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t < secs) { _ft.Add(Time.unscaledDeltaTime); yield return null; } }
        IEnumerator Until(Func<bool> cond, float timeout, string what)
        {
            float t = Time.realtimeSinceStartup;
            while (!cond()) { if (Time.realtimeSinceStartup - t > timeout) { Log("TIMEOUT waiting for " + what); yield break; } yield return null; }
        }

        Session Ses => Session.I; GameState S => Session.I.S;

        IEnumerator Full()
        {
            yield return Wait(1.5f); yield return ShotCo("title_early"); yield return Wait(2.5f); yield return ShotCo("title");
            foreach (var cam in Camera.allCameras) Log($"camera {cam.name} depth {cam.depth} pos {cam.transform.position} clear {cam.clearFlags} bg {cam.backgroundColor}");
            foreach (var cv in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Log($"canvas {cv.name} order {cv.sortingOrder} enabled {cv.enabled}");
            yield return Wait(0.5f);
            ulong seed = ulong.TryParse(GameBoot.I.ArgValue("-bl23seed") ?? "", out var sd) ? sd : 20260926UL;
            GameBoot.I.NewGame(seed); Log($"new game seed {seed}");
            yield return Wait(4.5f); yield return ShotCo("prologue_wide");
            yield return Wait(3.5f); yield return ShotCo("prologue_2");
            Ses.Headless = true;
            yield return Until(() => S.Phase == Phase.Daily && !Ses.Cine.Busy, 60, "daily");
            Ses.Headless = false;
            yield return Wait(1.5f); yield return ShotCo("daily_firstperson");
            Ses.Player.Pitch = 55f; yield return Wait(1.0f); yield return ShotCo("daily_lookdown"); Ses.Player.Pitch = 0f;   // first person: your own body below you
            // running in first person: the body must not flail through the view
            Ses.Player.ProbeRunUntil = Time.time + 2.6f; yield return Wait(1.3f); yield return ShotCo("daily_run"); Ses.Player.Pitch = 38f; yield return Wait(0.7f); yield return ShotCo("daily_run_down"); Ses.Player.Pitch = 0f; yield return Wait(0.8f);
            // dialogue with the nearest NPC
            var npc = S.LivingNpcs.OrderBy(a => a.Pos.Dist(S.Player.Pos)).FirstOrDefault();
            if (npc != null)
            {
                Ses.Player.Teleport(new P3(npc.Pos.f, npc.Pos.x + 1.2f, npc.Pos.z), MathX.AngleDeg(-1.2f, 0));
                yield return Wait(0.5f);
                Ses.Dialogue.Open(npc); yield return Wait(2.5f); yield return ShotCo("dialogue_" + npc.Id);
                yield return Wait(2f); yield return ShotCo("dialogue_options");
                Ses.Dialogue.Close(); yield return Wait(0.5f);
                // an invitation, in person: they say it, 민혁 accepts, the appointment card appears
                if (Ses.Sim.ProbeOffer(npc) != null)
                {
                    Ses.Dialogue.Open(npc); yield return Wait(4.5f); yield return ShotCo("dialogue_request");
                    Ses.Sim.Choose(npc, "req_accept"); Ses.Dialogue.Close(); yield return Wait(1.2f); yield return ShotCo("request_card");
                }
            }
            // character QA: a few of the cast framed head to toe where they stand (arms, hands, clothes), one GLB-rigged per shot
            foreach (var id in new[] { "P02", "P01", "P04", "P03" })
            {
                var cv = Ses.World.ViewOf(id); if (cv == null || !(S.A(id)?.Alive ?? false)) continue;
                var cgo = new GameObject("ProbeCastCam"); var ccam = cgo.AddComponent<Camera>(); ccam.depth = 60; ccam.fieldOfView = 30; ccam.nearClipPlane = 0.05f;
                var cfwd = cv.transform.forward; var cc = cv.transform.position + Vector3.up * 0.88f;
                ccam.transform.position = cc + cfwd * 3.1f + cv.transform.right * 0.5f + Vector3.up * 0.2f; ccam.transform.LookAt(cc);
                cv.ForceVisible = true; Ses.World.Mansion?.Cull(ccam.transform.position);
                yield return Wait(0.35f); yield return ShotCo("cast_" + id);
                cv.ForceVisible = false; Destroy(cgo);
            }
            Ses.Note.OpenTab("people"); yield return Wait(1f); yield return ShotCo("note_people");
            Ses.Note.OpenTab("map"); yield return Wait(1f); yield return ShotCo("note_map");
            Ses.Note.OpenTab("evidence"); yield return Wait(0.8f); yield return ShotCo("note_evidence");
            Ses.Note.Close(); yield return Wait(0.5f);
            yield return Hooks("daily");
            yield return RoomTour(10);
            // a shared breakfast: walk into the dining room while people eat
            {
                var din = S.Layout.Rooms.FirstOrDefault(r => r.Type == RoomType.Dining);
                if (din != null && S.Phase == Phase.Daily)   // --- time-on-demand: a case already open (a death in passed time) is not run on at Speed 12
                {
                    // breakfast is 08:00–09:00: let the house run until people have sat down to eat
                    { float tw = Time.realtimeSinceStartup; Ses.Speed = 12f; while (S.LivingNpcs.Count(x => x.Room == din.Id && (x.Anim == Anim.Eat || x.Pose == BL23.Sim.Pose.Sit)) < 3 && S.Minute < 9 * 60 && Time.realtimeSinceStartup - tw < 60f) yield return null; Ses.Speed = 1f; }
                    Ses.Player.Teleport(Ses.Sim.RandomPointIn(din, S.R(BL23.Sim.Stream.Presentation)), 0f);
                    float tt = Time.realtimeSinceStartup; while (!Ses.Cine.Busy && Time.realtimeSinceStartup - tt < 25f) yield return null;
                    if (Ses.Cine.Busy) { yield return Wait(1.5f); yield return ShotCo("tabletalk_1"); yield return Wait(3.2f); yield return ShotCo("tabletalk_2"); yield return Until(() => !Ses.Cine.Busy, 40, "table talk"); }
                    else Log("no table talk (diners " + S.LivingNpcs.Count(x => x.Room == din.Id) + ")");
                }
            }
            Ses.Player.FirstPerson = true;
            // first-person activities: sit down at a chair (the chair is pulled out), then read at a bookshelf
            foreach (var kind in new[] { "Chair", "Bookshelf" })
            {
                if (S.Phase != Phase.Daily) break;   // --- time-on-demand: a pastime would spend the investigation's minutes
                var fur = S.Layout.Furniture.Where(x => x.Type == kind && S.Layout.Room(x.Room) != null && !RoomInfo.IsPassage(S.Layout.Room(x.Room).Type) && x.Pos.f == 0).OrderBy(x => x.Id).FirstOrDefault();
                if (fur == null) continue; var acts = Ses.Sim.FurnitureActions(fur); if (acts.Count == 0) continue;
                var sp = S.Layout.Spots.FirstOrDefault(x => x.Furniture == fur.Id); var near = sp != null ? sp.Approach : fur.Pos;
                // stand a step away from it, toward the middle of its room (never through a wall)
                var fr = S.Layout.Room(fur.Room); float cx = fr != null ? fr.Rect.CX : near.x, cz = fr != null ? fr.Rect.CZ : near.z; float dl = Mathf.Max(0.01f, Mathf.Sqrt((cx - near.x) * (cx - near.x) + (cz - near.z) * (cz - near.z))); float st = Mathf.Min(1.3f, dl * 0.8f);
                var from = new P3(near.f, near.x + (cx - near.x) / dl * st, near.z + (cz - near.z) / dl * st);
                Ses.Player.Teleport(from, MathX.AngleDeg(near.x - from.x, near.z - from.z));
                yield return Wait(0.8f); Ses.Cine.Activity(fur, acts[acts.Count - 1]);
                yield return Wait(1.4f); yield return ShotCo("act_" + kind + "_1"); yield return Wait(1.4f); yield return ShotCo("act_" + kind + "_2");
                yield return Until(() => !Ses.Cine.Busy, 20, "activity");
            }
            Ses.Player.FirstPerson = true;
            // fast forward to the first investigation
            Log("fast-forward to investigation");
            Ses.Speed = 40f;
            float ffStart = Time.realtimeSinceStartup;
            while (S.Phase != Phase.Investigation && S.Phase != Phase.Trial && Time.realtimeSinceStartup - ffStart < 900)
            {
                if (Ses.Speed < 40) Ses.Speed = 40; // alarms reset speed
                if (Ses.Dialogue.Active) Ses.Dialogue.Close();
                if (Ses.Menu.Open) Ses.Menu.Close();
                if (!S.Player.Alive) { Log("player died: " + S.Player.Status); break; }
                yield return null;
            }
            Ses.Speed = 1f;
            Log($"phase {S.Phase} at {ClockFmt.DayHM(S.Clock)}; incidents {S.Incidents.Count}");
            if (S.Phase == Phase.Investigation)
            {
                var inc = S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter).OrderBy(i => i.ResultSeq).FirstOrDefault();
                var body = inc != null ? S.A(inc.Victim) : null;
                yield return Wait(1f); yield return ShotCo("investigation_start");
                if (body != null)
                {
                    { var stand = NearIn(body.Pos, body.Room, 1.5f); Ses.Player.Teleport(stand, MathX.AngleDeg(body.Pos.x - stand.x, body.Pos.z - stand.z), 35); }
                    yield return Wait(1.5f); yield return ShotCo("body_" + body.Id);
                    // the discovery cinematic (normally triggered when the player first sees the body)
                    Ses.Cine.Discovery(body.Id); yield return Wait(0.7f); yield return ShotCo("discovery_push"); yield return Wait(1.3f); yield return ShotCo("discovery_close"); yield return Wait(1.6f); yield return ShotCo("discovery_face");
                    yield return Until(() => !Ses.Cine.Busy, 10, "discovery end");
                    var ev = Ses.Sim.PlayerExamine(body); yield return Wait(0.3f);
                    if (ev != null) { Ses.Note.ShowBody(body, ev); yield return Wait(1f); yield return ShotCo("autopsy_card"); Ses.Note.Close(); }
                }
                Ses.Note.OpenTab("timeline"); yield return Wait(1f); yield return ShotCo("note_timeline"); Ses.Note.Close();
                yield return Hooks("investigation");
                Ses.Speed = 30f;
                { float tw = Time.realtimeSinceStartup, lastLog = tw;
                  while (S.Phase != Phase.Trial && Time.realtimeSinceStartup - tw < 600)
                  {
                      if (Ses.Speed < 30) Ses.Speed = 30; if (Ses.Dialogue.Active) Ses.Dialogue.Close(); if (Ses.Menu.Open) Ses.Menu.Close();
                      if (Time.realtimeSinceStartup - lastLog > 60) { lastLog = Time.realtimeSinceStartup; Log($"  waiting for trial: {S.Phase} {ClockFmt.DayHM(S.Clock)} cine={Ses.Cine.Busy} player={S.Player?.Status}"); }
                      yield return null;
                  }
                  if (S.Phase != Phase.Trial) Log("TIMEOUT waiting for trial"); }
                Ses.Speed = 1f;
            }
            if (S.Phase == Phase.Trial)
            {
                yield return Wait(4f); yield return ShotCo("trial_open");
                yield return Wait(5f); yield return ShotCo("trial_debate");
                yield return Until(() => S.Phase != Phase.Trial || (CluePicker.AnyHeard(S.Trial) && Ses.Trial.ProbeReady), 20, "first statement heard");   // F opens nothing before that
                Ses.Trial.ProbeFocus(); yield return Wait(1f); yield return ShotCo("trial_focus"); Ses.Trial.ProbeCloseFocus();
                yield return Hooks("trial");
                TrialDirectorUI.ProbeMomentOnce = true;   // the next witness / chain / theory moment is shown (and photographed) even in headless mode
                Ses.Headless = true;
                float ts = Time.realtimeSinceStartup; int lastBeats = 0;
                while (S.Phase == Phase.Trial && Time.realtimeSinceStartup - ts < 400)
                {
                    int n = S.Trial?.Beats.Count ?? 0;
                    if (n / 25 != lastBeats / 25) { Ses.Headless = false; yield return Wait(1.2f); yield return ShotCo($"trial_beat{n}"); Ses.Headless = true; }
                    lastBeats = n; yield return null;
                }
                Ses.Headless = false;
                yield return Wait(2.5f); yield return ShotCo("verdict");
                yield return Until(() => Ses.Reveal.Active, 60, "reveal");
                // 그날 밤의 재구성 photographs itself (cine_recap_*); the directed film follows (reveal_cine_*)
                yield return Until(() => !Ses.Reveal.RecapActive, 300, "recap");
                yield return Wait(3f); yield return ShotCo("reveal_1");
                yield return Wait(4f); yield return ShotCo("reveal_2");
                yield return Wait(6f);
                Ses.Headless = true;
                yield return Until(() => !Ses.Reveal.Active && !Ses.Trial.Active, 300, "reveal end");
                Ses.Headless = false;
                yield return Wait(3f); yield return ShotCo("after_chapter");
                yield return Hooks("after");
                Log($"after chapter: loop {S.Loop} ch {S.Chapter} phase {S.Phase} living {S.Living.Count()} settlements {S.Settlements.Count}");
                foreach (var st in S.Settlements) Log($"  settlement L{st.Loop}C{st.Chapter} accused {st.Accused} correct {st.Correct} exec {st.Executed} esc {st.Escaped} exc {st.Exception}");
            }
            // save/load roundtrip through the real slot files
            try
            {
                Ses.Save(6, "probe");
                var st2 = Session.LoadState(6, out var note);
                Log(st2 != null ? $"save/load ok (tick {st2.Tick}, actors {st2.Actors.Count}) {note}" : "save/load FAILED " + note);
            }
            catch (Exception e) { Log("save/load exception " + e); }
            Log("probe done");
            Flush();
            yield return Wait(1f);
            Application.Quit();
        }

        IEnumerator RoomTour(int max)
        {
            var seen = new HashSet<RoomType>();
            var rooms = S.Layout.Rooms.Where(r => r.Type != RoomType.Corridor && r.Type != RoomType.Courtroom && r.Type != RoomType.Elevator)
                .OrderBy(r => RoomInfo.IsMystery(r.Type) ? 0 : 1).ThenBy(r => r.Id).ToList();
            int n = 0;
            foreach (var r in rooms)
            {
                if (n >= max) break; if (!seen.Add(r.Type)) continue;
                // from a corner looking across the room: of the four corners, the one with nobody standing in the way
                var nav = S.Layout.Nav(r.Floor);
                P3 spot = new P3(r.Floor, r.Rect.CX, r.Rect.CZ); float bestScore = float.MinValue;
                foreach (var (cx, cz) in new[] { (r.Rect.x0, r.Rect.z0), (r.Rect.x1, r.Rect.z0), (r.Rect.x0, r.Rect.z1), (r.Rect.x1, r.Rect.z1) })
                {
                    int best = -1; float bd = float.MaxValue;
                    for (int k = 0; k < nav.Room.Length; k++) if (nav.Room[k] == r.Id && nav.Walkable(k) && nav.InMain(k)) { var c = nav.Center(k); float d = Math.Abs(c.x - cx) + Math.Abs(c.z - cz); if (d < bd) { bd = d; best = k; } }
                    if (best < 0) continue;
                    var cand = nav.Center(best);
                    // people between this corner and the room centre spoil the shot
                    float clear = 99f;
                    foreach (var a in S.Actors.Values)
                    {
                        if (a.IsPlayer || a.Pos.f != r.Floor) continue;
                        float dx = a.Pos.x - cand.x, dz = a.Pos.z - cand.z, vx = r.Rect.CX - cand.x, vz = r.Rect.CZ - cand.z;
                        float along = (dx * vx + dz * vz) / Math.Max(0.01f, (float)Math.Sqrt(vx * vx + vz * vz));
                        if (along < -0.5f) continue;
                        clear = Math.Min(clear, (float)Math.Sqrt(dx * dx + dz * dz));
                    }
                    float score = clear - bd * 0.2f;
                    if (score > bestScore) { bestScore = score; spot = cand; }
                }
                Ses.Player.Teleport(spot, MathX.AngleDeg(r.Rect.CX - spot.x, r.Rect.CZ - spot.z), 8);
                yield return Wait(1.2f); yield return ShotCo("room_" + r.Type); n++;
            }
        }

        IEnumerator UiTour()
        {
            yield return Wait(3f); yield return ShotCo("ui_title");
            GameBoot.I.NewGame(20260926UL); Ses.Headless = true;
            yield return Until(() => S.Phase == Phase.Daily && !Ses.Cine.Busy, 60, "daily");
            Ses.Headless = false; yield return Wait(1.2f); yield return ShotCo("ui_hud");
            var npc = S.LivingNpcs.OrderBy(a => a.Pos.Dist(S.Player.Pos)).FirstOrDefault();
            if (npc != null) { Ses.Dialogue.Open(npc); yield return Wait(4f); yield return ShotCo("ui_dialogue"); Ses.Dialogue.Close(); yield return Wait(0.5f); }
            foreach (var tab in new[] { "evidence", "timeline", "people", "map", "theory", "rules", "goals", "inventory" }) { Ses.Note.OpenTab(tab); yield return Wait(0.6f); yield return ShotCo("ui_note_" + tab); }
            Ses.Note.Close(); Ses.Menu.OpenSettings(); yield return Wait(0.6f); yield return ShotCo("ui_settings"); Ses.Menu.Close();
            Log("ui done"); Flush(); yield return Wait(1f); Application.Quit();
        }

        IEnumerator Rooms()
        {
            yield return Wait(2f);
            GameBoot.I.NewGame(20260926UL); Ses.Headless = true;
            yield return Until(() => S.Phase == Phase.Daily && !Ses.Cine.Busy, 60, "daily");
            Ses.Headless = false;
            yield return RoomTour(60);
            Log("rooms done"); Flush(); yield return Wait(1f); Application.Quit();
        }

        /// <summary>-bl23probe trial: step the kernel straight to the first class trial, investigate like the headless test player,
        /// then let every trial minigame demonstrate itself (each captures its own screenshots).</summary>
        IEnumerator TrialTour()
        {
            yield return Wait(2f);
            ulong seed = ulong.TryParse(GameBoot.I.ArgValue("-bl23seed") ?? "", out var sd) ? sd : 20260926UL;
            GameBoot.I.NewGame(seed); Log($"new game seed {seed}");
            Ses.Headless = true;
            yield return Until(() => S.Phase == Phase.Daily && !Ses.Cine.Busy, 90, "daily");
            Log("stepping the kernel to the first investigation");
            var sw = new System.Diagnostics.Stopwatch(); float ff = Time.realtimeSinceStartup;
            while (S.Phase != Phase.Investigation && S.Phase != Phase.Trial && Time.realtimeSinceStartup - ff < 900)
            {
                sw.Restart();
                while (sw.ElapsedMilliseconds < 40 && S.Phase == Phase.Daily && S.Player.Alive) Ses.Sim.Step();
                if (Ses.Dialogue.Active) Ses.Dialogue.Close(); if (Ses.Menu.Open) Ses.Menu.Close();
                if (!S.Player.Alive) { Log("player died: " + S.Player.Status); break; }
                _ft.Add(Time.unscaledDeltaTime); yield return null;
            }
            Log($"phase {S.Phase} at {ClockFmt.DayHM(S.Clock)}; incidents {S.Incidents.Count}");
            if (S.Phase == Phase.Investigation)
            {
                yield return Wait(0.5f);
                foreach (var inc in S.Incidents.Values.Where(i => i.Confirmed && i.Chapter == S.Chapter && i.Loop == S.Loop).ToList())
                {
                    var body = S.A(inc.Victim); if (body == null) continue;
                    Evidences.ExamineBody(Ses.Sim, S.Player, body, true);
                    foreach (var t in S.Traces.Where(t => t.Room == body.Room || t.Room == inc.CauseRoom).ToList()) Evidences.ExamineTrace(Ses.Sim, S.Player, t);
                }
                foreach (var npc in S.LivingNpcs.ToList())
                {
                    var w = Testimony.Where(Ses.Sim, npc, Cast.Player); if (w.Prop != null) Ses.Sim.Learn(Cast.Player, npc.Id, w.Prop, "(probe)", w.Lie, false);
                    foreach (var a in Testimony.Saw(Ses.Sim, npc, Cast.Player, 2)) if (a.Prop != null) Ses.Sim.Learn(Cast.Player, npc.Id, a.Prop, "(probe)", a.Lie, false);
                }
                Log($"player evidence {S.K(Cast.Player).Evidence.Count(e => e.Chapter == S.Chapter && e.Loop == S.Loop)}");
                float f2 = Time.realtimeSinceStartup;
                while (S.Phase != Phase.Trial && Time.realtimeSinceStartup - f2 < 600)
                {
                    if (S.Phase == Phase.Assembly) Ses.Headless = false;   // the trial opens with real presentation timing
                    sw.Restart();
                    while (sw.ElapsedMilliseconds < 40 && (S.Phase == Phase.Investigation || S.Phase == Phase.Assembly) && S.Player.Alive) Ses.Sim.Step();
                    if (Ses.Dialogue.Active) Ses.Dialogue.Close();
                    if (S.Phase == Phase.Trial) Ses.Headless = false;
                    _ft.Add(Time.unscaledDeltaTime); yield return null;
                }
            }
            if (S.Phase == Phase.Trial)
            {
                Ses.Headless = false; TrialDirectorUI.ProbeFast = true;
                yield return Until(() => Ses.Trial.Active, 30, "trial ui");
                yield return Wait(1.2f); yield return ShotCo("trial_open");
                yield return CourtScale();   // AutoProbe.Court.cs: the well from the verification cameras
                yield return Hooks("trialtour");
                DumpUiNear(new Rect(Screen.width * 0.5f, Screen.height - 40, Screen.width * 0.35f, 40));
                float ts = Time.realtimeSinceStartup; int lastBeats = 0; List<string> gameLog = null; string lastPrompt = null; bool probedBreak = false;
                while (S.Phase == Phase.Trial && Time.realtimeSinceStartup - ts < 1200)
                {
                    var T = S.Trial; int n = T?.Beats.Count ?? 0;
                    if (T != null) { gameLog = T.GameLog.Where(x => !x.StartsWith("shown:")).ToList(); if (T.PendingPrompt != lastPrompt) { lastPrompt = T.PendingPrompt; if (lastPrompt != null) Log($"prompt {lastPrompt} (beat {n}, influence {T.Influence:0.00})"); } }
                    if (n / 30 != lastBeats / 30) yield return ShotCo($"trial_talk{n}");
                    if (n >= 16 && !probedBreak && T != null && Ses.Trial.CamIdle && T.PendingPrompt == null) { probedBreak = true; Ses.Trial.ProbeBreak(); }   // the contradiction hit (camera), photographed once
                    lastBeats = n; _ft.Add(Time.unscaledDeltaTime); yield return null;
                }
                Log("trial rounds: " + string.Join(" | ", gameLog ?? new List<string>()));
                yield return Wait(2.5f); yield return ShotCo("verdict");
                yield return Wait(3.5f); yield return CourtVerdictUp();
                // the verdict plays out, then 그날 밤의 재구성 (it photographs itself: cine_recap_*) and the first stretch of the directed film (reveal_cine_*)
                yield return Until(() => Ses.Reveal.Active || (!Ses.Trial.Active && S.Phase != Phase.Trial && S.Phase != Phase.Verdict), 150, "reveal");
                if (Ses.Reveal.Active) { yield return Until(() => !Ses.Reveal.RecapActive, 300, "recap"); yield return Wait(14f); yield return ShotCo("reveal_film"); }
                Ses.Headless = true;
                yield return Until(() => !Ses.Reveal.Active && !Ses.Trial.Active, 400, "reveal end");
                Ses.Headless = false; TrialDirectorUI.ProbeFast = false;
                foreach (var st in S.Settlements) Log($"  settlement L{st.Loop}C{st.Chapter} accused {st.Accused} correct {st.Correct} exec {st.Executed} esc {st.Escaped} exc {st.Exception} note {st.Note}");
            }
            Log("trial probe done"); Flush();
            yield return Wait(1f);
            Application.Quit();
        }

        /// <summary>Probe diagnostics: log every visible UI graphic overlapping a screen rectangle (pixels, origin bottom-left).</summary>
        void DumpUiNear(Rect screen)
        {
            var corners = new Vector3[4];
            foreach (var g in FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsInactive.Exclude))
            {
                if (!g.isActiveAndEnabled || g.color.a < 0.05f || g.canvas == null || !g.canvas.enabled) continue;
                g.rectTransform.GetWorldCorners(corners); var cam = g.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : g.canvas.worldCamera;
                var a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]); var b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
                var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                if (!r.Overlaps(screen) || r.width > Screen.width * 0.95f) continue;
                string path = g.name; var p = g.transform.parent; for (int i = 0; i < 6 && p != null; i++, p = p.parent) path = p.name + "/" + path;
                Log($"  ui@top {path} [{g.GetType().Name}] canvas={g.canvas.name} rect=({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})");
            }
        }

        float _prevUi = -1f;
        void RestorePrefs() { if (_prevUi < 0) PlayerPrefs.DeleteKey("bl23.uiscale"); else Settings.UIScale = _prevUi; PlayerPrefs.Save(); }
        void OnDestroy() { Application.logMessageReceived -= OnLog; Active = false; }
    }
}
