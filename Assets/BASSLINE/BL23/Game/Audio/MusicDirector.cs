using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Audio
{
    /// <summary>
    /// Adaptive music player. Usage: <c>MusicDirector.I.SetState(MusicState.TrialDebate);</c>
    /// - crossfades between states with per-state fade/delay times (MusicLibrary);
    /// - inside a state rotates tracks least-recently-played first, never the same track twice in a row;
    /// - keeps the current track if it also belongs to the new state (e.g. Tension -> BodyDiscovery with "murder");
    /// - resumes a track where it left off if the state is re-entered within MusicLibrary.ResumeWindowSeconds (e.g. after Menu);
    /// - sample-synchronous layers (murder + kick stem) driven by SetIntensity();
    /// - stingers (synth or composed file) with automatic ducking; Duck(true/false) for dialogue;
    /// - runs on unscaled time and ignores AudioListener.pause, so it keeps working in pause menus;
    /// - null-safe: with zero music files in Resources/Music it simply stays silent.
    /// </summary>
    [DisallowMultipleComponent]
    public class MusicDirector : MonoBehaviour
    {
        public static MusicDirector I;

        public static MusicDirector Ensure()
        {
            if (I != null) return I;
            var go = new GameObject("BL23_MusicDirector");
            DontDestroyOnLoad(go);
            I = go.AddComponent<MusicDirector>();
            return I;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { Ensure(); }

        // ------------------------------------------------------------------ public API
        /// <summary>The state the game last asked for (callers compare against this).</summary>
        public MusicState Current => _requested;
        /// <summary>The state actually playing: requests are debounced in free roam, the summons phase gets its own cue and the
        /// dread after a body discovery is held until the investigation starts.</summary>
        public MusicState Playing => _state;
        public MusicState Previous => _prevState;
        /// <summary>True while no music is audible (between free-roam pieces, before a delayed start, or silent states).</summary>
        public bool IsSilent => _main == null || _main.Track == null || _main.Leaving || (_main.Fade < 0.08f && !_main.WaitingLoad);
        /// <summary>Extra ducking while any babble voice line plays (broadcasts, table talk) — dB, 0 = off.</summary>
        public float VoiceDuckDb = -5f;
        /// <summary>After a body discovery the music stays in dread (instead of drifting back to daily pieces) until the
        /// investigation starts, for at most this many seconds.</summary>
        public float DiscoveryHoldSeconds = 180f;
        public string NowPlaying => _main != null && _main.Track != null ? _main.Track.Name : null;
        public float NowPlayingTime => _main != null && _main.Src != null ? _main.Src.time : 0f;
        public float Intensity => _intensity;
        public bool IsDucked => _duck;
        /// <summary>Linear gain applied to synthesized stingers relative to SFX volume.</summary>
        public float StingerVolume = 0.9f;

        public void SetState(MusicState s, bool restart = false)
        {
            _requested = s;
            float now = Time.unscaledTime;
            if (s == MusicState.BodyDiscovery) _discoveryAt = now;
            else if (!MusicLibrary.IsFreeRoam(s) && s != MusicState.Tension && s != MusicState.Bond) _discoveryAt = -9999f; // investigation / trial / cinematics end the hold
            var eff = Resolve(s);
            if (restart) { ClearPending(); Apply(eff, true); return; }
            if (eff == _state) { ClearPending(); return; }
            float hold = HoldTime(_state, eff, out bool soft);
            if (hold <= 0f) { ClearPending(); Apply(eff, false); return; }
            if (_pendingApplyAt >= 0f && _pendingEff == eff) return; // already waiting for exactly this
            _pendingEff = eff; _pendingApplyAt = now + hold; _pendingSoft = soft;
            _softDeadline = now + (soft ? Mathf.Clamp(RemainingInTrack(), hold, 95f) : hold);
        }

        MusicState Resolve(MusicState s)
        {
            var ses = BL23.Game.Session.I; float now = Time.unscaledTime;
            if (s == MusicState.InvestigationLate && ses != null && ses.Sim != null && ses.S.Phase == BL23.Sim.Phase.Assembly) return MusicState.Assembly;
            if (MusicLibrary.IsFreeRoam(s) && now - _discoveryAt < DiscoveryHoldSeconds) return MusicState.BodyDiscovery;
            // --- owner's folders (2026-09-27): moments and moods chosen from what is happening
            if (now < _momentUntil && (MusicLibrary.IsFreeRoam(s) || s == MusicState.Tension)) return MusicState.Surprise;
            try
            {
                if (MusicLibrary.IsInvestigation(s) && ses != null && ses.Sim != null) return InvestigationMood(ses, s, now);
                if (ses != null && ses.Trial != null && ses.Trial.Active && (s == MusicState.TrialDebate || s == MusicState.TrialPressure || s == MusicState.TrialOpening || s == MusicState.TrialClimax))
                    return TrialMood(ses, s, now);
            }
            catch (System.Exception e) { if (Debug.isDebugBuild) Debug.LogWarning("[BL23 Music] mood: " + e.Message); }
            return s;
        }

        // ------------------------------------------------------------------ moods (owner's folders)
        float _momentUntil = -1f, _momentCooldownUntil = -1f, _restUntil = -1f;
        float _panicUntil = -1f, _comicUntil = -1f, _trialCueUntil = -1f; MusicState _trialCue = MusicState.TrialDebate;
        float _closingCheckAt = -1f; bool _closing; string _grimFor; bool _grim; int _beatSeen = -1;

        /// <summary>저택에서 예상치 못한 걸 발견했을 때: one piece from the surprise folder over free roam, then back (after a rest).
        /// At most one every few minutes so it keeps its punch.</summary>
        public void Surprise()
        {
            float now = Time.unscaledTime;
            if (now < _momentCooldownUntil || !MusicLibrary.IsFreeRoam(_requested) && _requested != MusicState.Tension) return;
            _momentUntil = now + 150f; _momentCooldownUntil = now + 360f; _restUntil = -1f;
            SetState(_requested);
        }

        /// <summary>
        /// The 심판's own cues, for trial code that knows better than the heuristics: "panic" (재판 때 패닉), "comic" (npc들 웃긴 추리),
        /// "closing" (범인의 윤곽), "clash" (범인과 결정적으로 말싸움), "modes" (재판 다른 모드들), "calm" (back to 재판 일반).
        /// Without calls the director reads the trial itself: the culprit breaking → panic, a beat whose Key/Data/Kind mentions
        /// comic/joke/silly/tangent/gag/absurd/banter → comic, the suspect and culprit stages → closing, the final stage → clash.
        /// </summary>
        public void TrialCue(string cue, float seconds = 30f)
        {
            float now = Time.unscaledTime;
            switch (cue)
            {
                case "panic": _panicUntil = now + seconds; break;
                case "comic": _comicUntil = now + seconds; break;
                case "closing": _trialCue = MusicState.Closing; _trialCueUntil = now + seconds; break;
                case "clash": _trialCue = MusicState.TrialClimax; _trialCueUntil = now + seconds; break;
                case "modes": _trialCue = MusicState.TrialPressure; _trialCueUntil = now + seconds; break;
                case "calm": _panicUntil = _comicUntil = _trialCueUntil = -1f; break;
            }
            SetState(_requested);
        }

        void EndMoment()
        {
            _momentUntil = -1f; ClearPending();
            var eff = Resolve(_requested);
            if (eff == MusicState.Surprise) eff = MusicState.Silence;
            Apply(eff, false);   // free roam: Apply rests first (prev state was the surprise)
        }

        static bool ComicBeat(BL23.Sim.TrialBeat b)
        {
            string t = ((b.Kind ?? "") + "|" + (b.Key ?? "") + "|" + (b.Data ?? "")).ToLowerInvariant();
            return t.Contains("comic") || t.Contains("joke") || t.Contains("silly") || t.Contains("tangent") || t.Contains("gag") || t.Contains("absurd") || t.Contains("banter") || t.Contains("derail");
        }

        MusicState TrialMood(BL23.Game.Session ses, MusicState s, float now)
        {
            var T = ses.S != null ? ses.S.Trial : null;
            if (!string.IsNullOrEmpty(ses.Trial.BreakFor)) _panicUntil = Mathf.Max(_panicUntil, now + 24f);   // the culprit's mask slips
            if (T != null && T.Beats != null && T.Cursor != _beatSeen)
            {
                int to = Mathf.Min(T.Cursor, T.Beats.Count);
                for (int i = Mathf.Max(0, Mathf.Max(_beatSeen, to - 4)); i < to; i++)
                {
                    var b = T.Beats[i]; if (b == null) continue;
                    if (b.Kind == "break" || b.Emotion == BL23.Sim.Emotion.Break) _panicUntil = Mathf.Max(_panicUntil, now + 24f);
                    else if (ComicBeat(b)) _comicUntil = Mathf.Max(_comicUntil, now + 30f);
                }
                _beatSeen = to;
            }
            if (now < _panicUntil) return MusicState.TrialPanic;
            if (now < _trialCueUntil) return _trialCue;
            if (now < _comicUntil && s == MusicState.TrialDebate) return MusicState.TrialComic;
            if (s == MusicState.TrialDebate && T != null)
            {
                if (T.Stage == "Suspicious" || T.Stage == "Culprit") return MusicState.Closing;
                if (T.Stage == "Final") return MusicState.TrialClimax;
            }
            return s;
        }

        MusicState InvestigationMood(BL23.Game.Session ses, MusicState s, float now)
        {
            var S = ses.S;
            if (now >= _closingCheckAt)
            {
                _closingCheckAt = now + 5f;
                // the culprit's outline: most of the case's open questions have a firm answer on the board
                var qs = BL23.Sim.CaseBoard.Questions(ses.Sim); int firm = 0; foreach (var q in qs) if (q.Firm) firm++;
                _closing = qs.Count >= 3 && firm >= Mathf.Max(3, qs.Count - 1);
            }
            if (_closing) return MusicState.Closing;
            if (s == MusicState.InvestigationLate) return s;
            string key = S.Ch != null ? S.Ch.TargetIncident : null;
            if (key != _grimFor) { _grimFor = key; _grim = Grim(S, key); }
            return _grim ? MusicState.InvestigationGrim : MusicState.Investigation;
        }

        /// <summary>훼손도·참혹성·잔혹성·트릭의 복잡함이 클 때: a mutilated or dismembered body, a body torn by many wounds, or a
        /// plan with three or more trick steps.</summary>
        static bool Grim(BL23.Sim.GameState S, string incId)
        {
            if (incId == null || !S.Incidents.TryGetValue(incId, out var inc) || inc == null) return false;
            if (inc.Mutilated) return true;
            var v = S.A(inc.Victim); if (v != null && v.Body != null && v.Body.Wounds.Count >= 5) return true;
            if (inc.PlanId != null && S.Plans.TryGetValue(inc.PlanId, out var p) && p != null)
            {
                int tricks = 0;
                foreach (var st in p.Steps)
                {
                    if (st == null || st.Kind == null || !st.Kind.StartsWith("X_", System.StringComparison.Ordinal)) continue;
                    if (st.Kind == "X_Dismember" || st.Kind == "X_Mutilate" || st.Kind == "X_ScatterParts") return true;
                    tricks++;
                }
                if (tricks >= 3) return true;
            }
            return false;
        }

        /// <summary>How long a free-roam request must stay stable before the music follows it (0 = at once).</summary>
        static float HoldTime(MusicState from, MusicState to, out bool soft)
        {
            soft = false;
            bool fFree = MusicLibrary.IsFreeRoam(from) || from == MusicState.Tension, tFree = MusicLibrary.IsFreeRoam(to) || to == MusicState.Tension;
            if ((from == MusicState.Tension && to == MusicState.BodyDiscovery) || (from == MusicState.BodyDiscovery && to == MusicState.Tension)) return 6f; // dread pools share tracks: no flip-flop
            if (!fFree || !tFree) return 0f;                                              // cinematic, case and trial cues are immediate
            if (MusicLibrary.IsTimeOfDay(from) && MusicLibrary.IsTimeOfDay(to)) { soft = true; return 2f; } // let the piece end (max 95 s)
            if (to == MusicState.Tension) return 2f;
            if (from == MusicState.Tension) return 10f;
            if (to == MusicState.Gathering) return 4f;
            if (from == MusicState.Gathering) return 8f;
            if (to == MusicState.Mystery) return 3f;
            if (from == MusicState.Mystery) return 5f;
            return 3f;
        }

        float RemainingInTrack()
        {
            if (_main == null || _main.Track == null || _main.Src == null || _main.Src.clip == null) return 0f;
            return Mathf.Max(0f, Mathf.Min(_main.Track.End, _main.Src.clip.length) - _main.Src.time);
        }

        void ClearPending() { _pendingApplyAt = -1f; _pendingSoft = false; }

        void UpdatePending(float now)
        {
            // requests stay valid while the world moves on: re-resolve (summons phase, discovery hold expiring)
            if (_pendingApplyAt < 0f && now >= _nextResolveAt)
            {
                _nextResolveAt = now + 0.5f;
                var eff = Resolve(_requested);
                if (eff != _state)
                {
                    float hold = HoldTime(_state, eff, out bool soft);
                    if (hold <= 0f) { Apply(eff, false); return; }
                    _pendingEff = eff; _pendingApplyAt = now + hold; _pendingSoft = soft; _softDeadline = now + (soft ? Mathf.Clamp(RemainingInTrack(), hold, 95f) : hold);
                }
            }
            if (_pendingApplyAt < 0f || now < _pendingApplyAt) return;
            if (Resolve(_requested) != _pendingEff) { ClearPending(); return; }            // the request changed back meanwhile
            if (_pendingSoft && now < _softDeadline && _main != null && !_main.Leaving && RemainingInTrack() > 6f) return; // finish the phrase first
            var e2 = _pendingEff; ClearPending(); Apply(e2, false);
        }

        void Apply(MusicState s, bool restart)
        {
            var def = MusicLibrary.State(s);
            if (s == _state && !restart) return;
            bool wasFree = MusicLibrary.IsFreeRoam(_state) && _main != null && !_main.Leaving && _main.Track != null;
            _prevState = _state; _state = s; _def = def;
            _pendingStartAt = -1f;
            if (def.Intensity >= 0f) SetIntensity(def.Intensity, def.State == MusicState.BodyDiscovery ? 0.3f : 1.5f);

            if (def.Tracks.Length == 0) { FadeOutAll(def.FadeOut, true); _main = null; return; }

            // keep the current track if it is also part of the new state
            if (!restart && _main != null && !_main.Leaving && _main.Track != null && Contains(def.Tracks, _main.Track.Name))
            {
                _main.StateVolTarget = def.Volume;
                _lastInState[s] = _main.Track.Name;
                if (def.Mode == MusicPlayMode.Sequence) _seqPos[s] = IndexOf(def.Tracks, _main.Track.Name);
                return;
            }

            if (def.Mode == MusicPlayMode.Sequence) _seqPos[s] = -1;
            FadeOutAll(def.FadeOut, true);
            _main = null;
            // daily music never runs back to back (owner, 2026-09-27): a rest between pieces carries across room and
            // time-of-day changes, walking into another room lets the house breathe first, and a surprise ends in quiet
            if (MusicLibrary.IsFreeRoam(s) && !restart)
            {
                float now = Time.unscaledTime;
                if (_prevState == MusicState.Surprise) _restUntil = Mathf.Max(_restUntil, now + 35f + _rng.F() * 25f);
                else if (wasFree) _restUntil = Mathf.Max(_restUntil, now + 20f + _rng.F() * 25f);
                if (now < _restUntil) { _pendingStartAt = _restUntil; _pendingRestart = false; return; }
            }
            if (def.Delay > 0f) { _pendingStartAt = Time.unscaledTime + def.Delay; _pendingRestart = restart; }
            else StartStateTrack(def, restart, def.FadeIn);
        }

        /// <summary>"discovery","break","objection","verdict","gavel","chime","evidence","loop_reset","chapter_clear" (or any Sfx id).</summary>
        public void Stinger(string id) => Stinger(id, 1f);

        public void Stinger(string id, float volume)
        {
            if (string.IsNullOrEmpty(id)) return;
            AudioClip clip = null; float gain; bool musical = false;
            string file = MusicLibrary.StingerFile(id);
            if (file != null) { clip = Clip(file); if (clip != null) { musical = true; } }
            if (clip != null) gain = MusicLibrary.Track(file).Gain * AudioVolumes.MusicGain;
            else
            {
                clip = Sfx.GetClip(id);
                gain = Sfx.VolumeOf(id) * AudioVolumes.SfxGain * StingerVolume;
            }
            if (clip == null) return;
            var src = _stingerSrc[_nextStinger]; _nextStinger = (_nextStinger + 1) % _stingerSrc.Length;
            src.Stop(); src.clip = clip; src.volume = Mathf.Clamp01(gain * volume); src.pitch = 1f; src.Play();
            float duckFor = musical ? clip.length : Mathf.Min(clip.length, 2.5f);
            _stingerDuck = AudioVolumes.DbToLin(musical ? -24f : -10f);
            _stingerDuckUntil = Mathf.Max(_stingerDuckUntil, Time.unscaledTime + duckFor);
        }

        public void SetVolume(float master, float music, float sfx) => AudioVolumes.Set(master, music, sfx);

        /// <summary>Lower the music under dialogue (depth per state, default -9 dB).</summary>
        public void Duck(bool on) { _duck = on; }

        /// <summary>0..1: fades sample-synchronous layers in/out (Tension: the murder kick stem).</summary>
        public void SetIntensity(float t, float seconds = 2f)
        {
            _intensityTarget = Mathf.Clamp01(t);
            _intensityRate = Mathf.Abs(_intensityTarget - _intensity) / Mathf.Max(0.05f, seconds);
            if (seconds <= 0f) _intensity = _intensityTarget;
        }

        /// <summary>Crossfade to another track of the current state (debug / "next song").</summary>
        public void Skip()
        {
            if (_def == null || _def.Tracks.Length == 0) return;
            Apply(_state, true);
        }

        public void StopAll(float fade = 1f) { _pendingStartAt = -1f; FadeOutAll(fade, false); _main = null; }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            foreach (var v in _voices) { if (v.Track == null) continue; if (paused) v.Src.Pause(); else v.Src.UnPause(); }
        }

        public string Describe()
        {
            string st = _requested == _state ? _state.ToString() : $"{_state} (asked {_requested})";
            if (_main == null || _main.Track == null) return $"{st}: (silence)";
            return $"{st}: {_main.Track.Name} {_main.Src.time:0.0}/{_main.Track.End:0.0}s int {_intensity:0.00} duck {_duckGain:0.00} voices {ActiveVoices()}";
        }

        // ------------------------------------------------------------------ internals
        sealed class Voice
        {
            public AudioSource Src;
            public MusicTrack Track;
            public bool IsLayer;
            public Voice Layer;          // main -> its layer
            public float Fade, FadeTarget, FadeRate;
            public float StateVol = 1f, StateVolTarget = 1f;
            public bool Leaving, NextScheduled, NativeLoop, WaitingLoad;
            public float WaitSince, IdleFor;
            public float StartPos;
            public double ScheduledDsp;
            public void Clear() { Track = null; IsLayer = false; Layer = null; Fade = 0; FadeTarget = 0; FadeRate = 1; Leaving = NextScheduled = NativeLoop = WaitingLoad = false; ScheduledDsp = 0; IdleFor = 0; WaitSince = 0; }
        }

        const int MaxVoices = 6;
        readonly List<Voice> _voices = new List<Voice>();
        Voice _main;
        MusicState _state = MusicState.Silence, _prevState = MusicState.Silence, _requested = MusicState.Silence, _pendingEff;
        float _pendingApplyAt = -1f, _softDeadline, _nextResolveAt, _discoveryAt = -9999f; bool _pendingSoft;
        MusicStateDef _def;
        float _intensity, _intensityTarget, _intensityRate = 0.5f;
        bool _duck, _paused; float _duckGain = 1f, _stingerDuck = 1f, _stingerDuckUntil;
        AudioSource[] _stingerSrc;
        int _nextStinger;
        float _pendingStartAt = -1f; bool _pendingRestart;
        readonly Dictionary<MusicState, int> _seqPos = new Dictionary<MusicState, int>();
        readonly Dictionary<MusicState, string> _lastInState = new Dictionary<MusicState, string>();
        readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        readonly Dictionary<string, KeyValuePair<float, float>> _resume = new Dictionary<string, KeyValuePair<float, float>>(); // name -> (pos, realtime)
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly HashSet<string> _missing = new HashSet<string>();
        SynthRng _rng = new SynthRng(0xB1A5u);

        void Awake()
        {
            if (I != null && I != this) { Destroy(this); return; } // the auto-created director wins; never destroy a host object
            I = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            AudioVolumes.Load();
            _def = MusicLibrary.State(MusicState.Silence);
            _stingerSrc = new AudioSource[2];
            for (int i = 0; i < _stingerSrc.Length; i++) _stingerSrc[i] = NewSource("Stinger" + i, 1);
            _rng = new SynthRng((uint)System.Environment.TickCount | 1u);
            Sfx.Init();
        }

        void OnDestroy() { if (I == this) I = null; }

        AudioSource NewSource(string name, int priority)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = false; s.spatialBlend = 0f; s.priority = priority; s.dopplerLevel = 0f;
            s.ignoreListenerPause = true; s.bypassReverbZones = true; s.volume = 0f;
            return s;
        }

        AudioClip Clip(string name)
        {
            if (string.IsNullOrEmpty(name) || _missing.Contains(name)) return null;
            if (_clips.TryGetValue(name, out var c) && c != null) return c;
            var tr = MusicLibrary.Track(name);
            c = tr != null ? Resources.Load<AudioClip>(tr.ResourcePath) : null;
            if (c == null) { _missing.Add(name); if (Debug.isDebugBuild) Debug.Log($"[BL23 Music] missing clip Resources/Music/{name} (continuing without it)"); return null; }
            _clips[name] = c; return c;
        }

        static bool Contains(string[] a, string s) { for (int i = 0; i < a.Length; i++) if (a[i] == s) return true; return false; }
        static int IndexOf(string[] a, string s) { for (int i = 0; i < a.Length; i++) if (a[i] == s) return i; return -1; }
        int ActiveVoices() { int n = 0; foreach (var v in _voices) if (v.Track != null) n++; return n; }

        void StartStateTrack(MusicStateDef def, bool restart, float fadeIn)
        {
            string name = PickTrack(def, restart, true, out float resumePos);
            if (name == null) return;
            var tr = MusicLibrary.Track(name);
            _main = StartVoice(tr, resumePos >= 0f ? resumePos : tr.Start, fadeIn, def);
        }

        string PickTrack(MusicStateDef def, bool restart, bool allowResume, out float resumePos)
        {
            resumePos = -1f;
            float now = Time.unscaledTime;
            // 1) resume a recently interrupted track of this state
            if (def.Resume && !restart && allowResume)
            {
                string best = null; float bestAt = -1f, bestPos = 0f;
                foreach (var n in def.Tracks)
                {
                    if (!_resume.TryGetValue(n, out var r)) continue;
                    var tr = MusicLibrary.Track(n);
                    if (tr == null || now - r.Value > MusicLibrary.ResumeWindowSeconds || r.Key > tr.End - 8f) continue;
                    if (r.Value > bestAt && Clip(n) != null) { best = n; bestAt = r.Value; bestPos = r.Key; }
                }
                if (best != null) { _resume.Remove(best); resumePos = bestPos; Played(def, best); return best; }
            }
            // 2) sequence
            if (def.Mode == MusicPlayMode.Sequence)
            {
                int start = _seqPos.TryGetValue(def.State, out var p) ? p : -1;
                for (int k = 1; k <= def.Tracks.Length; k++)
                {
                    int idx = (start + k) % def.Tracks.Length;
                    if (Clip(def.Tracks[idx]) != null) { _seqPos[def.State] = idx; Played(def, def.Tracks[idx]); return def.Tracks[idx]; }
                }
                return null;
            }
            // 3) rotation: least recently played, never the last one of this state / the one currently playing
            string current = _main != null && _main.Track != null ? _main.Track.Name : null;
            _lastInState.TryGetValue(def.State, out var last);
            string pick = null; float pickScore = float.MaxValue; int avail = 0;
            foreach (var n in def.Tracks) if (Clip(n) != null) avail++;
            if (avail == 0) return null;
            foreach (var n in def.Tracks)
            {
                if (Clip(n) == null) continue;
                if (avail > 1 && (n == last || (restart && n == current))) continue;
                float score = _lastPlayed.TryGetValue(n, out var t) ? t : -100000f + _rng.F() * 1000f; // never played: random order first
                if (score < pickScore) { pickScore = score; pick = n; }
            }
            if (pick == null) foreach (var n in def.Tracks) if (Clip(n) != null) { pick = n; break; }
            Played(def, pick);
            return pick;
        }

        void Played(MusicStateDef def, string name) { _lastInState[def.State] = name; _lastPlayed[name] = Time.unscaledTime; }

        Voice FreeVoice(Voice exclude = null)
        {
            foreach (var v in _voices) if (v.Track == null) return v;
            if (_voices.Count < MaxVoices)
            {
                var v = new Voice { Src = NewSource("MusicVoice" + _voices.Count, 0) }; _voices.Add(v); return v;
            }
            // steal the quietest leaving voice
            Voice victim = null;
            foreach (var v in _voices) if (v != exclude && !v.IsLayer && v.Leaving && (victim == null || v.Fade < victim.Fade)) victim = v;
            if (victim == null) foreach (var v in _voices) if (v != exclude && !v.IsLayer && v != _main) { victim = v; break; }
            if (victim == null) victim = _voices[0] != exclude ? _voices[0] : _voices[1];
            StopVoice(victim);
            return victim;
        }

        Voice StartVoice(MusicTrack tr, float pos, float fadeIn, MusicStateDef def)
        {
            var clip = Clip(tr.Name); if (clip == null) return null;
            var v = FreeVoice();
            v.Clear(); v.Track = tr; v.Src.clip = clip;
            v.StartPos = Mathf.Clamp(pos, 0f, Mathf.Max(0f, clip.length - 0.1f));
            v.Fade = fadeIn > 0.01f ? 0f : 1f; v.FadeTarget = 1f; v.FadeRate = 1f / Mathf.Max(0.01f, fadeIn);
            v.StateVol = v.StateVolTarget = def.Volume;
            bool single = def.Mode == MusicPlayMode.Hold || def.Tracks.Length == 1;
            v.NativeLoop = single && tr.SeamlessLoop && tr.Start <= 0.05f && tr.End >= tr.Duration - 0.05f;
            v.Src.loop = v.NativeLoop;

            if (tr.Layer != null)
            {
                var lclip = Clip(tr.Layer);
                if (lclip != null)
                {
                    var lv = FreeVoice(v);
                    lv.Clear(); lv.Track = MusicLibrary.Track(tr.Layer); lv.IsLayer = true; lv.Src.clip = lclip; lv.Src.loop = false; lv.Fade = 1f; lv.FadeTarget = 1f;
                    v.Layer = lv;
                }
            }
            v.WaitingLoad = true; v.WaitSince = Time.unscaledTime;
            TryBegin(v);
            ApplyVolume(v, AudioVolumes.MusicGain);
            return v;
        }

        /// <summary>Starts playback once the clip data (and the layer's) is ready; layers start on the same DSP tick.</summary>
        void TryBegin(Voice v)
        {
            if (!v.WaitingLoad) return;
            var c = v.Src.clip; var lc = v.Layer != null ? v.Layer.Src.clip : null;
            // streamed clips may never report Loaded before playing: give up waiting after 1.5 s and just play
            bool timedOut = Time.unscaledTime - v.WaitSince > 1.5f;
            if (!timedOut && (!Ready(c) || (lc != null && !Ready(lc)))) return;
            v.WaitingLoad = false;
            if (c.loadState == AudioDataLoadState.Failed) { StopVoice(v); if (_main == v) _main = null; return; }
            v.Src.time = Mathf.Min(v.StartPos, c.length - 0.05f);
            if (v.Layer != null && lc != null && lc.loadState != AudioDataLoadState.Failed)
            {
                v.Layer.Src.time = Mathf.Min(v.StartPos, lc.length - 0.05f);
                double at = AudioSettings.dspTime + 0.1;
                v.ScheduledDsp = at; v.Layer.ScheduledDsp = at;
                v.Src.PlayScheduled(at); v.Layer.Src.PlayScheduled(at);
            }
            else v.Src.Play();
            if (_paused) { v.Src.Pause(); if (v.Layer != null) v.Layer.Src.Pause(); }
        }

        static bool Ready(AudioClip c)
        {
            if (c == null) return true;
            var st = c.loadState;
            if (st == AudioDataLoadState.Unloaded) { c.LoadAudioData(); st = c.loadState; }
            return st == AudioDataLoadState.Loaded || st == AudioDataLoadState.Failed;
        }

        void StopVoice(Voice v)
        {
            if (v == null) return;
            if (v.Layer != null) { var l = v.Layer; v.Layer = null; StopVoice(l); }
            if (v.Src != null) { v.Src.Stop(); v.Src.clip = null; v.Src.volume = 0f; }
            v.Clear();
        }

        void BeginLeave(Voice v, float seconds, bool remember)
        {
            if (v == null || v.Track == null || v.IsLayer) return;
            if (remember && !v.WaitingLoad && v.Src.isPlaying) _resume[v.Track.Name] = new KeyValuePair<float, float>(v.Src.time, Time.unscaledTime);
            v.Leaving = true; v.FadeTarget = 0f;
            v.FadeRate = Mathf.Max(v.Fade, 0.01f) / Mathf.Max(0.02f, seconds);
            if (v.WaitingLoad) StopVoice(v);
        }

        void FadeOutAll(float seconds, bool remember)
        {
            foreach (var v in _voices) if (v.Track != null && !v.IsLayer && !v.Leaving) BeginLeave(v, seconds, remember);
        }

        string NextInState(MusicStateDef def)
        {
            if (_main == null || _main.Track == null) return null;
            if (def.Mode == MusicPlayMode.Hold) return _main.Track.Name;
            if (!Contains(def.Tracks, _main.Track.Name)) return PickTrack(def, false, false, out _);
            if (def.Mode == MusicPlayMode.Sequence) return PickTrack(def, false, false, out _);
            _lastInState[def.State] = _main.Track.Name;
            return PickTrack(def, true, false, out _);
        }

        void Update() { AudioPerf.BeginMusic(); try { UpdateImpl(); } finally { AudioPerf.EndMusic(); } }
        void UpdateImpl()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f), now = Time.unscaledTime;
            if (_def == null) _def = MusicLibrary.State(_state);

            UpdatePending(now);
            if (_pendingStartAt >= 0f && now >= _pendingStartAt) { _pendingStartAt = -1f; StartStateTrack(_def, _pendingRestart, _def.FadeIn); }

            _intensity = Mathf.MoveTowards(_intensity, _intensityTarget, _intensityRate * dt);
            float duckDb = _duck ? _def.DuckDb : 0f; if (VoiceBabble.IsSpeaking) duckDb = Mathf.Min(duckDb, VoiceDuckDb);
            float duckTarget = AudioVolumes.DbToLin(duckDb) * (now < _stingerDuckUntil ? _stingerDuck : 1f);
            float k = duckTarget < _duckGain ? 1f - Mathf.Exp(-dt / 0.08f) : 1f - Mathf.Exp(-dt / 0.45f);
            _duckGain += (duckTarget - _duckGain) * k;
            float bus = AudioVolumes.MusicGain;

            for (int i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (v.Track == null || v.IsLayer) continue;
                if (v.WaitingLoad) { TryBegin(v); if (v.Track == null || v.WaitingLoad) continue; } // fade starts with the audio
                v.Fade = Mathf.MoveTowards(v.Fade, v.FadeTarget, v.FadeRate * dt);
                v.StateVol = Mathf.MoveTowards(v.StateVol, v.StateVolTarget, dt * 0.6f);
                ApplyVolume(v, bus);
                bool scheduled = v.ScheduledDsp > 0 && AudioSettings.dspTime < v.ScheduledDsp + 0.05;
                if (v.Leaving && v.Fade <= 0.0005f) { StopVoice(v); continue; }
                if (!v.WaitingLoad && !scheduled && !_paused && !v.Src.isPlaying) v.IdleFor += dt; else v.IdleFor = 0f;
                if (v.IdleFor > 0.3f)
                {
                    // ended naturally (or audio was suspended past the crossfade point)
                    bool wasMain = v == _main;
                    StopVoice(v);
                    if (wasMain) { _main = null; if (_state == MusicState.Surprise) EndMoment(); else if (_def.Tracks.Length > 0) StartStateTrack(_def, false, 0.5f); }
                }
            }

            // schedule the successor inside the current state
            if (_main != null && _main.Track != null && !_main.Leaving && !_main.NextScheduled && !_main.NativeLoop && !_main.WaitingLoad && _main.Src.isPlaying && _def.Tracks.Length > 0)
            {
                var tr = _main.Track; float len = _main.Src.clip != null ? _main.Src.clip.length : tr.Duration;
                float end = Mathf.Min(tr.End, len);
                float overlap = Mathf.Clamp(Mathf.Max(_def.Crossfade, tr.Tail * 0.7f), _def.Crossfade, 8f);
                if (_def.Mode == MusicPlayMode.Hold && tr.SeamlessLoop) overlap = 0.3f;
                float pos = _main.Src.time;
                if (pos >= end - overlap)
                {
                    // a surprise is one piece: let it finish, then back to what was asked (after a quiet moment)
                    if (_state == MusicState.Surprise)
                    {
                        var leaving = _main; leaving.NextScheduled = true; BeginLeave(leaving, Mathf.Max(0.05f, end - pos), false);
                        _main = null; EndMoment(); return;
                    }
                    // free-roam states breathe: let the track finish on its own, then some room tone before the next one
                    if (_def.Gap > 0f && _def.Mode == MusicPlayMode.Rotate)
                    {
                        var leaving = _main; leaving.NextScheduled = true; BeginLeave(leaving, Mathf.Max(0.05f, end - pos), false);
                        _main = null; _pendingRestart = false; _pendingStartAt = now + Mathf.Max(0f, end - pos) + _def.Gap + UnityEngine.Random.value * _def.GapJitter;
                        if (MusicLibrary.IsFreeRoam(_state)) _restUntil = _pendingStartAt;   // the rest survives walking into another room
                        return;
                    }
                    string next = NextInState(_def);
                    var ntr = MusicLibrary.Track(next);
                    if (ntr != null)
                    {
                        bool self = ntr == tr;
                        float xf = self && tr.SeamlessLoop ? 0.3f : overlap;
                        var old = _main; old.NextScheduled = true;
                        BeginLeave(old, Mathf.Max(0.05f, end - pos), false);
                        _main = StartVoice(ntr, ntr.Start, self && tr.SeamlessLoop ? 0.3f : Mathf.Min(xf, _def.Crossfade), _def);
                    }
                }
            }
        }

        void ApplyVolume(Voice v, float bus)
        {
            float fade = Mathf.Sin(v.Fade * Mathf.PI * 0.5f);
            float g = bus * v.StateVol * v.Track.Gain * fade * _duckGain;
            v.Src.volume = Mathf.Clamp01(g);
            if (v.Layer != null && v.Layer.Track != null)
            {
                float mix = _intensity * _intensity * (3f - 2f * _intensity);
                v.Layer.Src.volume = Mathf.Clamp01(bus * v.StateVol * AudioVolumes.DbToLin(v.Track.LayerGainDb) * fade * _duckGain * mix);
            }
        }
    }
}
