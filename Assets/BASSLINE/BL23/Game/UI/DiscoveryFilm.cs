using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BL23.Game.Audio;
using BL23.Game.Characters;
using BL23.Game.Cinema;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Pose = BL23.Sim.Pose;
using BodyRegion = BL23.Sim.BodyRegion;
using MansionView = BL23.Game.Mansion.MansionView;

namespace BL23.Game
{
    /// <summary>
    /// Finding a body, filmed through 민혁's eyes. Driven by the audio clock of its own composed sting:
    /// D0 the threshold (first person: the gaze snaps to it, the room stretches away in a slow dolly-zoom, the hands come up),
    /// D1 the sting (the candles gutter outward from the body, every colour but blood drains, a 120 ms lens jolt, the bars),
    /// then 2–4 still, dark close-ups of what his eyes go to (a hand, the pool, the spray on the wall, the toppled chair, the
    /// weapon, a severed piece — the worst image last and longest), deafened under a thin ringing,
    /// D9 the return (back in his own eyes, a step back, breath, the lights come back) and the title card.
    /// Everything is read from the scene (kernel marks and traces, the gore renderer's hotspots and pieces), so it works for
    /// every case; any missing piece (a clip, the gore renderer, a clear angle) degrades quietly. Play never throws;
    /// Restore puts every camera, light, mask, volume and pause back and is safe to call any number of times.
    /// </summary>
    public sealed partial class DiscoveryFilm
    {
        // ================================================================== public surface
        public static DiscoveryFilm Active;
        public static float LastStartedAt = -999f;
        /// <summary>Fired at 45 % of each beat and insert (d0_threshold, d0_hands, d1_sting, i_*, d9_return) and when the title is up ("title").</summary>
        public static event Action<string> OnShot;
        /// <summary>Probe: acts like a skip key press (accepted after 2 s).</summary>
        public static bool ProbeSkip;
        public static string LastPlanLog = "", LastTimings = "";
        public static IReadOnlyDictionary<Light, float> LightBases => FilmLights.Bases;

        // ================================================================== audio (clips, cue, trims)
        const string ClipDir = "Sfx/Film/";
        static readonly string[] ClipNames = { "film_sting", "disc_hit_1", "disc_hit_2", "disc_hit_3", "film_heartbeat", "film_breath_in", "film_breath_out", "film_tinnitus" };
        /// <summary>Per-file level trims (dB) on top of the player's music / effects volume; the composer masters the files, so 0 by default.</summary>
        static readonly Dictionary<string, float> TrimDb = new Dictionary<string, float>
        {
            { "film_sting", 0f }, { "film_tinnitus", 0f }, { "disc_hit_1", 0f }, { "disc_hit_2", 0f }, { "disc_hit_3", 0f },
            { "film_heartbeat", 0f }, { "film_breath_in", 0f }, { "film_breath_out", 0f },
        };
        static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        static bool _warm; static float _cueHit = 0.30f; static FilmWatchdog _dog;
        static object _fullSim; static int _fullLoop = -1, _fullChapter = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = null; LastStartedAt = -999f; OnShot = null; ProbeSkip = false; LastPlanLog = ""; LastTimings = "";
            _clips.Clear(); _warm = false; _cueHit = 0.30f; _dog = null; _fullSim = null; _fullLoop = _fullChapter = -1; _hudCanvas = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Dog();
            // the first mark of blood anywhere warms the film's clips (gore-render never references the film)
            LinkFirstGore(Warm);
        }

        static FilmWatchdog Dog()
        {
            if (_dog != null) return _dog;
            var go = new GameObject("DiscoveryFilm"); UnityEngine.Object.DontDestroyOnLoad(go);
            _dog = go.AddComponent<FilmWatchdog>();
            return _dog;
        }

        /// <summary>Load the film's clips and cue file so the first discovery does not stall (safe to call again).</summary>
        public static void Warm()
        {
            if (_warm) return; _warm = true;
            foreach (var n in ClipNames)
            {
                try
                {
                    var c = Resources.Load<AudioClip>(ClipDir + n); _clips[n] = c;
                    if (c != null && c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData();
                }
                catch (Exception e) { Debug.LogWarning("[DiscoveryFilm] clip " + n + ": " + e.Message); }
            }
            try
            {
                var cues = Resources.Load<TextAsset>(ClipDir + "film_sting_cues");
                if (cues != null)
                    foreach (var raw in cues.text.Split('\n'))
                    {
                        var line = raw.Trim(); if (line.Length == 0 || line.StartsWith("#")) continue;
                        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && parts[1] == "hit" && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h) && h >= 0f && h < 3f) _cueHit = h;
                    }
            }
            catch (Exception e) { Debug.LogWarning("[DiscoveryFilm] cues: " + e.Message); }
        }

        static AudioClip Clip(string n) { Warm(); return _clips.TryGetValue(n, out var c) ? c : null; }
        static float Gain(string n)
        {
            float bus = n == "film_sting" || n == "film_tinnitus" ? AudioVolumes.MusicGain : AudioVolumes.SfxGain;
            return bus * AudioVolumes.DbToLin(TrimDb.TryGetValue(n, out var t) ? t : 0f);
        }

        /// <summary>A body was found while something else held the screen: the sting now, the film when the screen is free.</summary>
        public static void PlayStingNow(MonoBehaviour host)
        {
            try
            {
                var c = Clip("film_sting"); if (c == null) { MusicDirector.I?.Stinger("discovery"); return; }   // not composed into this build yet
                var src = Dog().Sting; src.Stop(); src.clip = c; src.loop = false; src.volume = Mathf.Clamp01(Gain("film_sting")); src.Play();
            }
            catch (Exception e) { Debug.LogWarning("[DiscoveryFilm] sting: " + e.Message); }
        }

        // ================================================================== instance
        readonly Session _s; readonly CinematicUI _ui; readonly string _victim; readonly bool _late;
        GameState S => _s.S;
        PlayerController _pc; Actor _a, _v; ActorView _me, _body; MansionView _mv; Camera _cam; Incident _inc;
        bool _full, _fallback, _titleOnly, _skip, _abort, _restored, _completed, _inInserts, _setupDone;
        readonly StringBuilder _tm = new StringBuilder();
        // what we change and give back
        float _pcFov0, _camFov0, _camNear0 = 0.05f, _camFar0 = 140f; AntialiasingMode _camAA0; int _camMask0; bool _camRec, _pcCamWas = true;
        bool _victimFV0; Camera _mvCam0; bool _mvCamRec; float _ambDb0 = float.NaN; Canvas _hud; bool _hudRec;
        // gaze / dolly
        Vector3 _eye0, _focus; int _room = -1, _floor; float _floorY; float _yaw0, _pitch0, _yawT, _pitchT;
        float _d0 = 2f, _pullMax, _pull, _dollyFov = 60f, _tremor; Vector3 _pullDir;
        // audio clock
        double _dspStart; float _u0, _off; bool _dsp; float _h = 0.30f; bool _hitDone; float _t;
        float _started;
        readonly List<(float at, Action act)> _later = new List<(float, Action)>();
        readonly HashSet<string> _fired = new HashSet<string>();
        float _tinTo, _tinFrom, _tinT0 = -1f, _tinDur = 0.5f;
        float _ambTo, _ambFrom, _ambT0 = -1f, _ambDur = 0.3f;
        DiscoveryTitle _title; FilmLetterbox _bars;

        public DiscoveryFilm(Session s, CinematicUI ui, string victim, bool late) { _s = s; _ui = ui; _victim = victim; _late = late; }

        /// <summary>The whole film. Never throws: each step is guarded and a failing step is skipped.</summary>
        public IEnumerator Play() => Guarded(Main());

        IEnumerator Main()
        {
            bool ok = false;
            try { ok = Setup(); } catch (Exception e) { Fail("setup", e); }
            if (!ok) { Stamp("not started"); Publish(); yield break; }
            if (_titleOnly)
            {
                // a queued film for a body that is already being investigated: the card only
                ShowTitle(Time.unscaledTime, 2.6f); FireShot("title");
                Stamp("title only (incident already confirmed)"); _completed = true; Publish();
                yield break;
            }
            yield return StartClock();
            yield return D0();
            if (!_abort && !_skip && !_fallback) yield return Inserts();
            if (!_abort) yield return D9();
            Publish();
        }

        // ------------------------------------------------------------------ setup
        bool Setup()
        {
            _pc = _s.Player; _a = S?.Player; _me = _s.World?.ViewOf(Cast.Player); _v = S?.A(_victim); _body = _s.World?.ViewOf(_victim);
            _mv = _s.World?.Mansion; _cam = _ui != null ? _ui.FilmCam : null;
            if (_pc == null || _pc.Cam == null || _a == null || _v == null || _s.Headless) return false;
            Active = this; LastStartedAt = Time.unscaledTime; _started = Time.unscaledTime; ProbeSkip = false;
            Warm();
            _inc = S.Incidents.Values.Where(i => i.Victim == _victim && i.Loop == S.Loop).OrderByDescending(i => i.ResultSeq).FirstOrDefault();
            // the first film of a chapter is the full one; later bodies (and queued films) get the short cut
            bool first = !ReferenceEquals(_fullSim, _s.Sim) || _fullLoop != S.Loop || _fullChapter != S.Chapter;
            _full = first && !_late;
            if (first) { _fullSim = _s.Sim; _fullLoop = S.Loop; _fullChapter = S.Chapter; }
            if (_late && _inc != null && _inc.Confirmed) _titleOnly = true;
            Stamp($"start victim={_victim} full={_full} late={_late} gore={Settings.Gore}");
            // HUD out of the way for the close-ups (its banner and toast return with control)
            _hud = HudCanvas(); if (_hud != null) { _hudRec = true; _hud.enabled = false; }
            // the stop banner ("비명이 들렸다"), speech bubbles and system notes step out of the frame too (FilmOverlays)
            try { FilmOverlays.Hide(); } catch (Exception e) { Fail("overlays", e); }
            if (_titleOnly) { _setupDone = true; return true; }
            // music: the dread takes over (never restarted if a body is already holding it)
            try { var md = MusicDirector.I; if (md != null && md.Playing != MusicState.BodyDiscovery) md.SetState(MusicState.BodyDiscovery, true); } catch (Exception e) { Fail("music", e); }
            _pcFov0 = _pc.Cam.fieldOfView; _pcCamWas = _pc.Cam.enabled;
            _eye0 = _pc.Cam.transform.position;
            ChooseFocus();
            if (!_fallback)
            {
                // everything the film will look at is decided now, before the first frame (no hitch at the cut)
                try { FindDropAnchor(); } catch (Exception e) { Fail("drop anchor", e); }
                try { Plan(); } catch (Exception e) { Fail("plan", e); _plan.Clear(); }
            }
            _setupDone = true;
            return true;
        }

        static Canvas _hudCanvas;
        static Canvas HudCanvas()
        {
            if (_hudCanvas != null) return _hudCanvas;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c != null && c.name == "HUD" && c.isRootCanvas) { _hudCanvas = c; break; }
            return _hudCanvas;
        }

        /// <summary>What the eyes go to: the body if it is in plain view within 14 m, else the nearest piece that is, else the body.</summary>
        void ChooseFocus()
        {
            Physics.SyncTransforms();   // every sight line below is cast against this frame's poses
            bool roomLoaded = _body != null && _mv != null && _mv.Rooms != null && _v.Room >= 0 && _v.Room < _mv.Rooms.Length && _mv.Rooms[_v.Room] != null;
            Vector3 torso = _body != null ? CineAnchors.Body(_victim) : (_s.World != null ? _s.World.ToWorld(_v.Pos) + Vector3.up * 0.3f : _eye0 + _pc.Cam.transform.forward * 2f);
            _focus = torso; string why = "torso";
            if (roomLoaded && !(Vector3.Distance(_eye0, torso) <= 14f && SeenFromEye(torso)))
            {
                float best = float.MaxValue; bool found = false;
                foreach (var pc in PiecesOf(_victim))
                {
                    if (pc.Hidden || pc.T == null) continue;
                    float dp = Vector3.Distance(_eye0, pc.Center); if (dp > 14f || dp >= best || !SeenFromEye(pc.Center, pc.ItemId)) continue;
                    best = dp; _focus = pc.Center; why = "piece " + pc.Part; found = true;
                }
                if (!found) why = "torso (not in plain view)";
            }
            if (!roomLoaded) { _fallback = true; why += " — room not loaded: short fallback"; }
            _room = _mv != null ? _mv.RoomAtWorld(_focus + Vector3.up * 0.3f) : -1;
            if (_room < 0) _room = _v.Room;
            var rm = S.Layout.Room(_room); _floor = rm != null ? rm.Floor : _v.Pos.f; _floorY = S.Layout.FloorY(_floor);
            _d0 = Mathf.Max(0.6f, Vector3.Distance(_eye0, _focus));
            var d = _focus - _eye0; var flat = new Vector3(d.x, 0, d.z);
            _yaw0 = _pc.Yaw; _pitch0 = _pc.Pitch;
            _yawT = flat.sqrMagnitude > 1e-4f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : _pc.Yaw;
            _pitchT = Mathf.Clamp(Mathf.Atan2(-d.y, Mathf.Max(0.05f, flat.magnitude)) * Mathf.Rad2Deg, -30f, 60f);
            // how far the lens may pull back for the dolly: a 0.15 m sphere stopping 0.1 m before anything behind
            _pullDir = -(Quaternion.Euler(_pitchT, _yawT, 0) * Vector3.forward);
            float free = 0.6f;
            foreach (var h in Physics.SphereCastAll(_eye0, 0.15f, _pullDir, 0.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider == null || h.distance <= 0f || OwnBody(h.collider)) continue;
                free = Mathf.Min(free, h.distance);
            }
            _pullMax = Mathf.Clamp(free - 0.1f, 0f, 0.5f);
            Stamp($"focus {why} room={_room} d0={_d0:0.00} pull={_pullMax:0.00}");
        }

        bool OwnBody(Collider c)
        {
            if (c.GetComponentInParent<PlayerController>() != null || c.GetComponentInParent<ActorView>()?.Id == Cast.Player) return true;
            var part = c.GetComponentInParent<BL23.Game.Physicality.PhysicalBodyPart>();   // ragdoll proxies live beside the view
            return part != null && part.Owner != null && part.Owner.GetComponent<ActorView>()?.Id == Cast.Player;
        }

        // ------------------------------------------------------------------ the audio clock
        IEnumerator StartClock()
        {
            _h = _cueHit;
            AudioClip sting = _late ? null : Clip("film_sting");
            if (sting != null && sting.loadState != AudioDataLoadState.Loaded)
            {
                if (sting.loadState == AudioDataLoadState.Unloaded) sting.LoadAudioData();
                float w0 = Time.unscaledTime;
                while (sting.loadState != AudioDataLoadState.Loaded && sting.loadState != AudioDataLoadState.Failed && Time.unscaledTime - w0 < 0.4f) yield return null;
            }
            _dspStart = AudioSettings.dspTime + 0.10; _u0 = Time.unscaledTime + 0.10f; _off = 0f;
            var dog = Dog();
            if (sting != null && sting.loadState == AudioDataLoadState.Loaded)
            {
                dog.Sting.Stop(); dog.Sting.clip = sting; dog.Sting.loop = false; dog.Sting.volume = Mathf.Clamp01(Gain("film_sting"));
                dog.Sting.PlayScheduled(_dspStart); _dsp = true;
                Stamp($"sting scheduled at dsp+0.10, hit cue {_h:0.000}s");
            }
            else if (_late)
            {
                // the sting already played when the body was found; the film's hit is a lighter one, on the same clock
                var h2 = Clip("disc_hit_2");
                if (h2 != null) { dog.Hit.Stop(); dog.Hit.clip = h2; dog.Hit.loop = false; dog.Hit.volume = Mathf.Clamp01(Gain("disc_hit_2")); dog.Hit.PlayScheduled(_dspStart + _h); }
                _dsp = true; Stamp("late film: disc_hit_2 at the hit");
            }
            else if (sting == null)
            {
                // the composed sting is not in the build yet: the house's old discovery stinger instead, on the same clock
                _dsp = true; try { MusicDirector.I?.Stinger("discovery"); } catch (Exception e) { Fail("fallback stinger", e); }
                Stamp("no film_sting clip: fallback stinger, audio clock only");
            }
            else
            {
                // still loading after 0.4 s: play it as soon as it can, and run the film on unscaled time
                _dsp = false; _u0 = Time.unscaledTime;
                dog.Sting.Stop(); dog.Sting.clip = sting; dog.Sting.loop = false; dog.Sting.volume = Mathf.Clamp01(Gain("film_sting")); dog.Sting.Play();
                Stamp("sting not loaded in 0.4 s: unsynced, unscaled clock");
            }
        }

        /// <summary>Film time: the audio clock, smoothed over its buffer steps (falls back to unscaled time if audio stalls).</summary>
        float T()
        {
            float tu = Time.unscaledTime - _u0;
            if (!_dsp) return tu;
            float td = (float)(AudioSettings.dspTime - _dspStart);
            if (Mathf.Abs(td - tu) > 0.3f) { _dsp = false; Stamp($"audio clock drifted {td - tu:+0.00;-0.00}s: unscaled from here"); return tu; }
            _off += (td - tu - _off) * 0.2f;
            return tu + _off;
        }

        /// <summary>Once per frame inside every phase: advance the clock, run timed actions, audio fades, skip and abort checks.
        /// Returns true when the current phase must stop (skip or abort).</summary>
        bool Frame()
        {
            _t = T();
            float now = Time.unscaledTime;
            for (int i = _later.Count - 1; i >= 0; i--)
                if (now >= _later[i].at) { var a = _later[i].act; _later.RemoveAt(i); try { a(); } catch (Exception e) { Fail("timed", e); } }
            FadeAudio(now);
            try { FxTick(); } catch (Exception e) { Fail("fx", e); }
            if (_s.Headless) { Stamp("headless mid-film: restored"); _abort = true; Restore(); return true; }
            if (!_skip && now - _started >= 2.0f && (ProbeSkip || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)))
            { _skip = true; ProbeSkip = false; Stamp($"skip at t={_t:0.00}"); }
            return _skip || _abort;
        }
        void Later(float secs, Action a) => _later.Add((Time.unscaledTime + secs, a));

        void FadeAudio(float now)
        {
            var dog = _dog; if (dog == null) return;
            if (_tinT0 >= 0f)
            {
                float u = Mathf.Clamp01((now - _tinT0) / _tinDur); float v = Mathf.Lerp(_tinFrom, _tinTo, u);
                dog.Tin.volume = Mathf.Clamp01(v * Gain("film_tinnitus"));
                if (u >= 1f) { _tinT0 = -1f; if (_tinTo <= 0f) dog.Tin.Stop(); }
            }
            if (_ambT0 >= 0f && AmbienceDirector.I != null)
            {
                float u = Mathf.Clamp01((now - _ambT0) / _ambDur); AmbienceDirector.I.MasterDb = Mathf.Lerp(_ambFrom, _ambTo, u * u * (3f - 2f * u));
                if (u >= 1f) _ambT0 = -1f;
            }
        }
        void Tinnitus(float to, float secs)
        {
            var dog = Dog(); var c = Clip("film_tinnitus"); if (c == null) return;
            if (!dog.Tin.isPlaying && to > 0f) { dog.Tin.clip = c; dog.Tin.loop = true; dog.Tin.volume = 0f; dog.Tin.Play(); }
            _tinFrom = dog.Tin.isPlaying ? dog.Tin.volume / Mathf.Max(1e-4f, Gain("film_tinnitus")) : 0f; _tinTo = to; _tinT0 = Time.unscaledTime; _tinDur = Mathf.Max(0.01f, secs);
        }
        void Ambience(float db, float secs)
        {
            var amb = AmbienceDirector.I; if (amb == null) return;
            if (float.IsNaN(_ambDb0)) _ambDb0 = amb.MasterDb;
            _ambFrom = amb.MasterDb; _ambTo = db; _ambT0 = Time.unscaledTime; _ambDur = Mathf.Max(0.01f, secs);
        }
        void PlayOn(AudioSource src, string clip, float vol = 1f)
        {
            var c = Clip(clip); if (c == null || src == null) return;
            src.Stop(); src.clip = c; src.loop = false; src.volume = Mathf.Clamp01(Gain(clip) * vol); src.Play();
        }

        void FireShot(string name)
        {
            if (!_fired.Add(name)) return;
            _tm.Append($" [{name}@{_t:0.00}]");
            try { OnShot?.Invoke(name); } catch (Exception e) { Fail("OnShot " + name, e); }
        }

        // ------------------------------------------------------------------ D0 the threshold / D1 the sting
        IEnumerator D0()
        {
            float len = _fallback ? 1.2f : 1.3f;
            _pc.BeginScript();
            _a.Yaw = _yawT;   // the body turns with the gaze, so the hands come up in front of the view
            var anim = _me?.Rig?.Anim; anim?.PlayGesture(Gesture.Surprised, 1.4f);
            try { HandsBegin(); } catch (Exception e) { Fail("hands", e); }
            var grade = DiscoveryGrade.Ensure(); grade.Attach(_pc.Cam); grade.WeightTo(0.6f, len);
            LightsBegin();
            _later.Add((_u0 + 0.05f, () => PlayOn(Dog().Body, "film_breath_in")));
            bool handsChecked = false;
            while (true)
            {
                if (Frame()) yield break;
                float t = _t;
                if (!_hitDone && t >= _h) Hit();
                // the gaze goes to it (0.35 s, eased out) — the camera itself is written below, after the player's own update
                float g = Mathf.Clamp01(t / 0.35f); g = 1f - (1f - g) * (1f - g) * (1f - g);
                float yaw = Mathf.LerpAngle(_yaw0, _yawT, g), pitch = Mathf.Lerp(_pitch0, _pitchT, g);
                _pc.Yaw = _pc.ScriptYaw = yaw; _pc.Pitch = _pc.ScriptPitch = pitch;
                // dolly zoom from 0.35 s: the lens pulls back while the field narrows, so the body grows and the room stretches away
                float k = Mathf.Clamp01((t - 0.35f) / Mathf.Max(0.1f, len - 0.35f)); k = k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) * 0.5f;
                float want = _pullMax * k, s = Mathf.Lerp(1f, 1.25f, k);
                // the lens leaves his eyes backwards only as far as his own shoulders stay out of the frame (first person)
                if (want > _pullCap) want = _pullCap;
                float safe = SafePull(_pc.Cam.transform.position, Quaternion.Euler(pitch, yaw, 0f), s, want);
                if (safe < want - 1e-4f) { _pullCap = safe; if (!_capLogged) { _capLogged = true; Stamp($"dolly pull capped at {safe:0.00} m (his shoulders)"); } }
                _pull = Mathf.Min(want, _pullCap);
                _dollyFov = DollyFov(_pull, s);
                _tremor = _hitDone ? Mathf.MoveTowards(_tremor, 0.4f, Time.unscaledDeltaTime * 2f) : 0.12f;
                float kick = _hitDone ? -0.8f * Mathf.Sin(Mathf.Clamp01((t - _h) / 0.12f) * Mathf.PI) : 0f;
                WriteFirstPerson(yaw, pitch + kick, _pull, _dollyFov, t);
                if (t >= len * 0.45f) FireShot("d0_threshold");
                if (!handsChecked && t >= 0.6f) { handsChecked = true; CheckHands(); }
                if (t >= 1.0f) FireShot("d0_hands");
                if (_hitDone && t >= _h + 0.06f) FireShot("d1_sting");
                if (t >= len) break;
                yield return null;
            }
            if (!_hitDone) Hit();
        }

        float _pullCap = 9f; bool _capLogged;
        readonly Plane[] _planes = new Plane[6];
        static readonly HBone[] TorsoBones = { HBone.Neck, HBone.UpperChest, HBone.Chest, HBone.ShoulderL, HBone.ShoulderR, HBone.UpperArmL, HBone.UpperArmR };

        /// <summary>fov(t) = 2·atan(tan(fov0/2) · d0/(d0+pull) / s): the body keeps (and slowly gains) its size while the room recedes.</summary>
        float DollyFov(float pull, float s) => 2f * Mathf.Atan(Mathf.Tan(_pcFov0 * 0.5f * Mathf.Deg2Rad) * _d0 / (_d0 + pull) / s) * Mathf.Rad2Deg;

        /// <summary>The largest pull ≤ want (4 cm steps) at which none of his own neck, chest, shoulders or upper arms (0.12 m
        /// around each bone) would be in the lens's frustum.</summary>
        float SafePull(Vector3 eye, Quaternion rot, float s, float want)
        {
            var rig = _me?.Rig; var cam = _pc.Cam; if (rig == null || cam == null || want <= 0f) return want;
            for (float p = want; p > 0.001f; p -= 0.04f)
            {
                var view = Matrix4x4.TRS(eye + _pullDir * p, rot, new Vector3(1, 1, -1)).inverse;
                GeometryUtility.CalculateFrustumPlanes(Matrix4x4.Perspective(DollyFov(p, s), cam.aspect, cam.nearClipPlane, 60f) * view, _planes);
                bool seen = false;
                foreach (var hb in TorsoBones)
                {
                    if (!rig.HasBone(hb)) continue; var b = rig.Bone(hb).position; bool inside = true;
                    for (int i = 0; i < 6; i++) if (_planes[i].GetDistanceToPoint(b) < -0.12f) { inside = false; break; }
                    if (inside) { seen = true; break; }
                }
                if (!seen) return p;
            }
            return 0f;
        }

        /// <summary>The first-person lens, after PlayerController's own scripted update has placed it this frame.</summary>
        void WriteFirstPerson(float yaw, float pitch, float pull, float fov, float t)
        {
            var cam = _pc.Cam; if (cam == null) return;
            float tx = (Mathf.PerlinNoise(t * 9f, 0.37f) - 0.5f) * 2f * _tremor, ty = (Mathf.PerlinNoise(0.71f, t * 9f) - 0.5f) * 2f * _tremor;
            var rot = Quaternion.Euler(pitch + tx, yaw + ty, 0f);
            cam.transform.rotation = rot;
            if (pull > 0f) cam.transform.position += _pullDir * pull;
            cam.fieldOfView = fov;
        }

        /// <summary>D1: the hit lands on the sting's cue.</summary>
        void Hit()
        {
            _hitDone = true;
            double err = (AudioSettings.dspTime - (_dspStart + _h)) * 1000.0;
            Stamp($"hit t={_t:0.000} dspErr={err:+0;-0}ms");
            LastHitDspErrMs = (float)err;
            try { FilmLights.Ensure().Dip(0.18f, 0.25f); } catch (Exception e) { Fail("gutter", e); }
            try { CandleSmoke(); } catch (Exception e) { Fail("smoke", e); }
            if (Sfx.Has("candle_snuff"))
            {
                var near = _candles.OrderBy(c => c.delay).Take(2).ToList();
                if (near.Count > 0) Sfx.PlayEx("candle_snuff", near[0].pos, 0.55f);
                var p2 = near.Count > 1 ? near[1].pos : (near.Count > 0 ? near[0].pos : _focus);
                Later(0.19f, () => Sfx.PlayEx("candle_snuff", p2, 0.4f));
            }
            var grade = DiscoveryGrade.I; if (grade != null) { grade.Drain(1f, 0.4f); grade.Spike(0.12f); }
            try { _bars = FilmLetterbox.Ensure(_ui.FilmCanvas); _bars?.Slide(true, 0.25f); } catch (Exception e) { Fail("bars", e); }
            try { MotesStart(); } catch (Exception e) { Fail("motes", e); }
            try { DropSpawn(); } catch (Exception e) { Fail("drop", e); }
            try { GazeStart(); } catch (Exception e) { Fail("gaze light", e); }
        }
        /// <summary>|audio clock − frame| at the hit (ms), for the probe.</summary>
        public static float LastHitDspErrMs;

        // ------------------------------------------------------------------ the gutter: which lights, how far
        readonly List<(Vector3 pos, float delay, bool candle)> _candles = new List<(Vector3, float, bool)>();
        void LightsBegin()
        {
            var list = new List<(Light, float)>(); _candles.Clear();
            if (_mv == null || _room < 0) { FilmLights.Ensure().Begin(list); return; }
            var b = _mv.RoomBounds(_room); b.Expand(1.0f);   // + 0.5 m each side
            var candleRecs = new HashSet<Light>();
            if (_mv.Rooms != null) foreach (var rv in _mv.Rooms) if (rv != null) foreach (var lr in rv.Lights) if (lr.Light != null && (lr.Fire || (lr.Flicker > 0f && lr.Range < 7f))) candleRecs.Add(lr.Light);
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == null || (l.type != LightType.Point && l.type != LightType.Spot)) continue;
                if (l == _pc.Flash || l.GetComponentInParent<FilmLights>() != null || l.name.StartsWith("Film")) continue;
                if (!b.Contains(l.transform.position)) continue;
                float delay = Vector3.Distance(l.transform.position, _focus) / 12f;   // the dip travels outward at 12 m/s
                list.Add((l, delay));
                bool candle = candleRecs.Contains(l) || (l.range < 4f && l.color.r > l.color.b * 1.4f);
                if (candle && Vector3.Distance(l.transform.position, _focus) < 6f) _candles.Add((l.transform.position, delay, true));
            }
            FilmLights.Ensure().Begin(list);
            Stamp($"gutter lights {list.Count}, candles {_candles.Count}");
        }

        // ------------------------------------------------------------------ inserts
        IEnumerator Inserts()
        {
            if (_plan.Count < 2) { Stamp("fewer than 2 inserts: D0 + D1 + D9 + title"); yield break; }
            EnterInserts();
            int hit = 0;
            for (int i = 0; i < _plan.Count; i++)
            {
                if (_abort || _skip) break;
                yield return InsertCo(_plan[i], hit++);
            }
        }

        CineShot _shot; Light _key, _rim; float _keyBase, _keyHz, _keySeed;
        void EnterInserts()
        {
            _inInserts = true;
            var cam = _cam; if (cam == null) return;
            if (!_camRec)
            {
                _camRec = true; _camFov0 = cam.fieldOfView; _camNear0 = cam.nearClipPlane; _camFar0 = cam.farClipPlane;
                var d0 = cam.GetUniversalAdditionalCameraData(); if (d0 != null) { _camAA0 = d0.antialiasing; _camMask0 = d0.volumeLayerMask.value; }
            }
            cam.nearClipPlane = 0.03f; cam.farClipPlane = 60f;
            var d = cam.GetUniversalAdditionalCameraData();
            var pd = _pc.Cam.GetUniversalAdditionalCameraData();
            if (d != null) { d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; d.volumeLayerMask = (pd != null ? pd.volumeLayerMask.value : 1) | (1 << DiscoveryGrade.Layer); }
            CineDof.Register(cam);   // (the grade's layer is already in the mask set above; Restore puts the recorded mask back whole)
            cam.enabled = true; _pc.Cam.enabled = false;
            if (_me != null) { _me.ForceHidden = true; _me.SetHidden(true); }   // now, not next frame: the first insert frame must not see him
            if (_hands != null) _hands.FillMax = 0f;   // his hands' warm spill is his, not the close-ups'
            if (_body != null) { _victimFV0 = _body.ForceVisible; _body.ForceVisible = true; _body.SetHidden(false); }
            GoreFilmMode(true);
            if (_mv != null) { _mvCam0 = _mv.ViewCamera; _mvCamRec = true; _mv.ViewCamera = cam; }
            Ambience((float.IsNaN(_ambDb0) ? (AmbienceDirector.I?.MasterDb ?? 0f) : _ambDb0) - 20f, 0.3f);
            Tinnitus(1f, 0.5f);
            MotesSpeed(0.03f);
            GazeTo(GazeMax * 0.5f, 0.3f);   // the close-ups carry their own key
        }

        IEnumerator InsertCo(Shot sh, int n)
        {
            var cam = _cam; if (cam == null) yield break;
            float t0 = _t, dur = sh.Dur;
            _shot = new CineShot
            {
                Spec = new ShotSpec(ShotKind.Thing, _victim) { Push = 1.12f, Rack = true },
                Base = sh.Lens, Follow = false, Target = sh.Target ?? (() => sh.P),
                O0 = Vector3.zero, O1 = (sh.P - sh.Lens) * (1f - 1f / 1.12f),
                L0 = sh.Look - sh.Lens, L1 = sh.Look - sh.Lens,
                Fov0 = 34f, Fov1 = 31f, FitW0 = sh.FitW0, FitW1 = sh.FitW1,
                Dof = true, Aperture = sh.Aperture, Focal = 60f, RackFrom = Vector3.Distance(sh.Lens, sh.P) + 0.2f,
                Roll0 = sh.Roll, Roll1 = sh.Roll * 1.15f,
            };
            try { _mv?.Cull(sh.Lens); } catch (Exception e) { Fail("cull", e); }
            try { KeyLight(sh); } catch (Exception e) { Fail("key", e); }
            // a witness recoils as the cut lands on them (a silent scream under the ringing)
            if (sh.Witness != null) { try { var wv = _s.World?.ViewOf(sh.Witness); wv?.Rig?.Anim?.PlayGesture(Gesture.Surprised, sh.Dur + 0.6f); wv?.Rig?.SetExpression(Expr.Fear); } catch (Exception e) { Fail("witness", e); } }
            PlayOn(Dog().Hit, "disc_hit_" + (n % 3 + 1));
            if (sh.Moth) { try { MothStart(sh); } catch (Exception e) { Fail("moth", e); } }
            _tm.Append($" {sh.Name}({dur:0.0}s)");
            float seed = n * 3.17f;
            while (true)
            {
                if (Frame()) break;
                float el = _t - t0; float u = Mathf.Clamp01(el / dur);
                _shot.Apply(cam, u, Time.unscaledDeltaTime);
                // a hand-held breath of drift, never a shake
                float dx = (Mathf.PerlinNoise(_t * 0.6f, seed) - 0.5f) * 0.3f, dy = (Mathf.PerlinNoise(seed + 5f, _t * 0.6f) - 0.5f) * 0.3f;
                cam.transform.rotation *= Quaternion.Euler(dx, dy, 0f);
                if (_key != null) _key.intensity = _keyBase * (1f + 0.08f * Mathf.Sin(_t * _keyHz * 2f * Mathf.PI + _keySeed) * (0.6f + 0.4f * Mathf.PerlinNoise(_t * 3f, _keySeed)));
                if (sh.Moth) MothTick(sh, u);
                DropTick(false);
                if (u >= 0.45f) FireShot(sh.Name);
                if (u >= 1f) break;
                yield return null;
            }
            if (_key != null) _key.enabled = false; if (_rim != null) _rim.enabled = false;
        }

        void KeyLight(Shot sh)
        {
            if (_key == null)
            {
                _key = new GameObject("FilmKey").AddComponent<Light>(); UnityEngine.Object.DontDestroyOnLoad(_key.gameObject);
                _key.type = LightType.Spot; _key.spotAngle = 58f; _key.innerSpotAngle = 22f; _key.range = 3f; _key.shadows = LightShadows.Soft; _key.shadowStrength = 0.9f;
                _key.color = new Color(1f, 0.62f, 0.32f);
            }
            var pos = sh.KeyPos; _key.transform.position = pos; _key.transform.rotation = Quaternion.LookRotation((sh.P - pos).normalized);
            _key.range = Mathf.Min(3f, Vector3.Distance(pos, sh.P) + 1.4f);
            _keyBase = 1.4f; _key.intensity = _keyBase; _key.enabled = true;
            _keyHz = 6f + (float)(_rng?.NextDouble() ?? 0.5) * 3f; _keySeed = (float)(_rng?.NextDouble() ?? 0.3) * 10f;
            if (sh.Rim)
            {
                if (_rim == null) { _rim = new GameObject("FilmRim").AddComponent<Light>(); UnityEngine.Object.DontDestroyOnLoad(_rim.gameObject); _rim.type = LightType.Point; _rim.shadows = LightShadows.None; _rim.color = new Color(0.78f, 0.82f, 0.9f); _rim.range = 1.4f; }
                var away = sh.P - sh.Lens; away.y = 0; away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
                _rim.transform.position = sh.P + away * 0.45f + Vector3.up * 0.25f; _rim.intensity = 0.25f; _rim.enabled = true;
            }
            else if (_rim != null) _rim.enabled = false;
        }

        // ------------------------------------------------------------------ D9 the return
        IEnumerator D9()
        {
            float len = _skip ? 0.8f : _fallback ? 1.8f : 2.4f;
            float t0 = _t;
            ExitInserts();
            _pc.Cam.enabled = true;
            PlayOn(Dog().Body, "film_breath_out");
            Later(_skip ? 0.15f : 0.35f, () => PlayOn(Dog().Body2, "film_heartbeat", 0.75f));
            _me?.Rig?.Anim?.PlayGesture(Gesture.HandOnChest, 1.2f);
            HandsEnd(_skip ? 0.3f : 0.6f);   // the raised hands come down as the breath goes out
            try { FilmLights.Ensure().Return(_skip ? 0.6f : 1.2f); } catch (Exception e) { Fail("lights return", e); }
            GazeTo(0f, _skip ? 0.5f : 1.1f);   // the last light on it goes as the candles come back
            Tinnitus(0f, _skip ? 0.5f : 1.0f);
            if (!float.IsNaN(_ambDb0)) Ambience(_ambDb0, _skip ? 0.6f : 1.2f);
            DiscoveryGrade.I?.WeightTo(0.25f, len);
            DropRelease();
            MotesFade(len);
            // the title rises as he steps back
            float titleAt = Time.unscaledTime + (_skip ? 0f : 0.2f);
            ShowTitle(titleAt, -1f);
            // a step back (the kernel pose moves; nav-checked)
            var p0 = _a.Pos; var back = -new Vector3(_focus.x - _eye0.x, 0, _focus.z - _eye0.z).normalized;
            var p1 = new P3(p0.f, p0.x + back.x * 0.45f, p0.z + back.z * 0.45f);
            var g = S.Layout.Nav(p0.f); int kc = g.CellOf(p1.x, p1.z); if (!g.Walkable(kc) || S.Layout.RoomAt(p1) != S.Layout.RoomAt(p0)) p1 = p0;
            // back in his eyes exactly as the threshold left them (the dolly's narrow field and pulled-back lens), then relaxing
            float fovFrom = _dollyFov > 1f ? _dollyFov : _pc.Cam.fieldOfView, pullFrom = _pull, trFrom = Mathf.Max(_tremor, 0.25f);
            float fovDur = Mathf.Min(1.6f, len), stepDur = Mathf.Min(0.5f, len * 0.6f);
            bool barsOut = false;
            while (true)
            {
                Frame(); if (_abort) yield break;
                float el = _t - t0;
                float ks = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(el / stepDur));
                _a.Pos = new P3(p0.f, Mathf.Lerp(p0.x, p1.x, ks), Mathf.Lerp(p0.z, p1.z, ks));
                float kf = Mathf.Clamp01(el / fovDur); kf = kf * kf * (3f - 2f * kf);
                _tremor = Mathf.Lerp(trFrom, 0f, Mathf.Clamp01(el / len));
                WriteFirstPerson(_pc.ScriptYaw, _pc.ScriptPitch + Mathf.Sin(Mathf.Clamp01(el / stepDur) * Mathf.PI) * 0.6f, Mathf.Lerp(pullFrom, 0f, ks), Mathf.Lerp(fovFrom, Settings.Fov, kf), _t);
                if (!barsOut && el >= len - 0.6f) { barsOut = true; _bars?.Slide(false, 0.55f); }
                DropTick(true);
                if (el >= len * 0.45f) FireShot("d9_return");
                if (el >= len) break;
                yield return null;
            }
            _a.Pos = p1;
            _pc.Cam.fieldOfView = Settings.Fov;
            _pc.EndScript(_a.Pos, _pc.ScriptYaw);
            // control is back
            if (_hud != null) _hud.enabled = true;
            FilmOverlays.Show();
            Hud.I?.Toast("R 길게: 살펴보기 — 곧 종이 울린다", Pal.Blood, 5f);
            _title?.HideAt(_skip ? titleAt + 1.2f : Time.unscaledTime + 1.2f);
            DiscoveryGrade.I?.Tail(4f);
            _completed = true;
            Stamp($"control returned t={_t:0.00} total={Time.unscaledTime - _started:0.00}s");
        }

        void ExitInserts()
        {
            if (!_inInserts) return; _inInserts = false;
            if (_cam != null) { _cam.enabled = false; CineDof.Off(_cam); }
            if (_pc?.Cam != null) _pc.Cam.enabled = true;
            if (_me != null) { _me.ForceHidden = false; _me.SetHidden(false); }
            if (_hands != null) _hands.FillMax = 0.7f;
            if (_body != null) _body.ForceVisible = _victimFV0;
            GoreFilmMode(false);
            if (_mv != null && _mvCamRec) { _mv.ViewCamera = _mvCam0; _mvCamRec = false; }
            try { if (_pc?.Cam != null) _mv?.Cull(_pc.Cam.transform.position); } catch (Exception e) { Fail("cull", e); }
            if (_key != null) _key.enabled = false; if (_rim != null) _rim.enabled = false;
            MotesSpeed(1f);
        }

        void ShowTitle(float at, float hideAfter)
        {
            try
            {
                _title = DiscoveryTitle.Ensure(_ui != null ? _ui.FilmCanvas : null); if (_title == null) return;
                string who = Cast.NameOf(_victim), where = S.RoomName(_room >= 0 ? _room : (_v?.Room ?? -1)), when = ClockFmt.HM(S.Clock);
                _title.Show($"{who} — {where} · {when}", at);
                if (hideAfter > 0f) _title.HideAt(at + hideAfter);
                Later(Mathf.Max(0f, at - Time.unscaledTime) + 0.4f, () => FireShot("title"));
            }
            catch (Exception e) { Fail("title", e); }
        }

        // ------------------------------------------------------------------ restore
        /// <summary>Put everything back (idempotent, null-safe): cameras, FOV, the player's script, who is hidden, the lights,
        /// the grade, the bars and title, the HUD, the ambience, the gore film mode, the mansion's culling camera.</summary>
        public void Restore()
        {
            if (_restored) return; _restored = true;
            void Do(string what, Action a) { try { a(); } catch (Exception e) { Fail("restore " + what, e); } }
            Do("pc cam", () => { if (_pc?.Cam != null) _pc.Cam.enabled = true; });
            Do("film cam", () =>
            {
                if (_cam == null) return;
                _cam.enabled = false; CineDof.Off(_cam);
                if (_camRec)
                {
                    _cam.fieldOfView = _camFov0; _cam.nearClipPlane = _camNear0; _cam.farClipPlane = _camFar0;
                    var d = _cam.GetUniversalAdditionalCameraData(); if (d != null) { d.antialiasing = _camAA0; d.volumeLayerMask = _camMask0; }
                }
            });
            Do("script", () => { if (_pc != null && _pc.Scripted && _a != null) _pc.EndScript(_a.Pos, _pc.ScriptYaw); });
            Do("fov", () => { if (_pc?.Cam != null && _setupDone && !_titleOnly) _pc.Cam.fieldOfView = Settings.Fov; });
            Do("hidden", () => { if (_me != null) _me.ForceHidden = false; if (_body != null && _inInserts) _body.ForceVisible = _victimFV0; });
            Do("hands", () => { if (!_completed) HandsKill(); else HandsEnd(0.4f); });
            _inInserts = false;
            Do("lights", () => { FilmLights.Ensure().End(); if (_key != null) UnityEngine.Object.Destroy(_key.gameObject); if (_rim != null) UnityEngine.Object.Destroy(_rim.gameObject); _key = _rim = null; });
            Do("fx", DestroyFx);
            Do("grade", () =>
            {
                var gr = DiscoveryGrade.I; if (gr == null) return;
                bool court = (_s.Trial != null && _s.Trial.Active) || (_s.Reveal != null && _s.Reveal.Active) || (S != null && S.Phase == Phase.Trial);
                if (_completed && !court) gr.Tail(4f); else gr.Kill();
            });
            Do("bars", () => { if (!_completed) _bars?.HideNow(); });
            Do("title", () => { if (!_completed) _title?.HideNow(); });
            Do("hud", () => { if (_hudRec && _hud != null) _hud.enabled = true; });
            Do("overlays", FilmOverlays.Show);
            Do("ambience", () => { if (!float.IsNaN(_ambDb0) && AmbienceDirector.I != null) AmbienceDirector.I.MasterDb = _ambDb0; _ambT0 = -1f; });
            Do("tinnitus", () => { if (_dog != null) _dog.Tin.Stop(); _tinT0 = -1f; });
            Do("gore", () => GoreFilmMode(false));
            Do("mansion", () => { if (_mv != null && _mvCamRec) { _mv.ViewCamera = _mvCam0; _mvCamRec = false; } if (_mv != null && _pc?.Cam != null) _mv.Cull(_pc.Cam.transform.position); });
            _later.Clear();
            if (Active == this) Active = null;
            Publish();
        }

        // ------------------------------------------------------------------ bookkeeping
        void Stamp(string s) { _tm.Append($"\n[{Time.unscaledTime - _started:0.00}s t={_t:0.00}] {s}"); }
        void Fail(string what, Exception e) { Debug.LogWarning($"[DiscoveryFilm] {what}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); _tm.Append($"\n  ! {what}: {e.Message}"); }
        void Publish() { LastTimings = _tm.ToString(); LastPlanLog = _plog.ToString(); }

        /// <summary>Runs a coroutine tree with every step guarded: an exception abandons that step (logged) and the film goes on.</summary>
        IEnumerator Guarded(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(root);
            while (stack.Count > 0)
            {
                var top = stack.Peek(); object cur = null; bool moved;
                try { moved = top.MoveNext(); if (moved) cur = top.Current; }
                catch (Exception e) { Fail("step", e); stack.Pop(); continue; }
                if (!moved) { stack.Pop(); continue; }
                if (cur is IEnumerator nested) { stack.Push(nested); continue; }
                yield return cur;
            }
        }
    }

    /// <summary>The film's own 2D sources, and a watchdog: a film that has held the screen for 20 s is restored by force.</summary>
    public sealed class FilmWatchdog : MonoBehaviour
    {
        internal AudioSource Sting, Tin, Hit, Body, Body2;
        void Awake() { Sting = Src(0); Tin = Src(8); Hit = Src(4); Body = Src(16); Body2 = Src(16); }
        AudioSource Src(int prio)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false; s.spatialBlend = 0f; s.ignoreListenerPause = true; s.bypassReverbZones = true; s.bypassListenerEffects = true; s.priority = prio; s.dopplerLevel = 0f;
            return s;
        }
        void Update()
        {
            var f = DiscoveryFilm.Active; if (f == null) return;
            if (Time.unscaledTime - DiscoveryFilm.LastStartedAt <= 20f) return;
            Debug.LogWarning("[DiscoveryFilm] watchdog: a film held the screen for 20 s — restored by force");
            try { f.Restore(); } catch (Exception e) { Debug.LogException(e); }
            DiscoveryFilm.Active = null;
            var s = Session.I; if (s != null) { s.Resume("cine"); if (s.Cine != null) s.Cine.Busy = false; }
        }
    }
}
