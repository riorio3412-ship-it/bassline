using System;
using System.Collections.Generic;

namespace BL23.Game.Audio
{
    public enum MusicState
    {
        Silence, Title, Prologue, DailyMorning, DailyDay, DailyEvening, Night, Tension, Mystery, BodyDiscovery,
        Investigation, InvestigationLate, TrialOpening, TrialDebate, TrialPressure, TrialClimax, Vote, VerdictCorrect,
        VerdictWrong, Execution, Escape, Reveal, Aftermath, LoopReset, Menu,
        Bond, Gathering, Assembly,
        // owner's folder layout (2026-09-27 22:40, "상황에 맞는 BGM,OST들")
        InvestigationGrim,   // 훼손도와 참혹성, 잔혹성, 트릭의 복잡함이 클 때
        Closing,             // 범인의 윤곽이 보일 때 (investigation and 심판)
        TrialPanic,          // 재판 때 패닉
        TrialComic,          // 재판 때 npc들 웃긴 추리 할 때
        Surprise             // 저택에서 예상치 못한 걸 발견했을 때 (one piece, then back)
    }

    /// <summary>Rotate = least-recently-played track next (never the same twice in a row). Sequence = listed order, then wrap.
    /// Hold = keep looping the chosen track until the state changes.</summary>
    public enum MusicPlayMode { Rotate, Sequence, Hold }

    /// <summary>One music file in Resources/Music. All times in seconds. Numbers were measured offline (BL23Lab/MusicAnalysis).</summary>
    public sealed class MusicTrack
    {
        public readonly string Name;      // Resources path "Music/" + Name
        public readonly string Source;    // original file name in the composer's archive (아카이브)
        public readonly float GainDb;     // loudness normalisation towards -16 LUFS (attenuate only)
        public readonly float Start;      // skip leading silence
        public readonly float End;        // end of audible content (before trailing silence)
        public readonly float Tail;       // length of the natural fade/decay that ends at End
        public readonly bool SeamlessLoop;// file was composed to loop
        public readonly float Lufs;       // measured integrated loudness (BS.1770-style)
        public readonly float Duration;   // file duration
        public string Layer;              // optional sample-synchronous layer track (vertical remix)
        public float LayerGainDb;         // absolute gain of the layer when fully in

        public MusicTrack(string name, string source, float gainDb, float start, float end, float tail, bool seamless, float lufs, float duration)
        { Name = name; Source = source; GainDb = gainDb; Start = start; End = end; Tail = tail; SeamlessLoop = seamless; Lufs = lufs; Duration = duration; }

        public float Gain => (float)Math.Pow(10.0, GainDb / 20.0);
        public string ResourcePath => "Music/" + Name;
    }

    public sealed class MusicStateDef
    {
        public MusicState State;
        public string[] Tracks = new string[0];
        public MusicPlayMode Mode = MusicPlayMode.Rotate;
        public float Volume = 1f;        // state mix level (linear)
        public float FadeIn = 2f;        // new music fade-in
        public float FadeOut = 2f;       // how fast the previous music leaves when this state is entered
        public float Delay = 0f;         // silence between old music leaving and new music starting
        public float Crossfade = 3f;     // minimum overlap between consecutive tracks inside the state
        public float Intensity = -1f;    // layer intensity applied on entry (-1 = keep current)
        public bool Resume = true;       // resume a remembered position when coming back to a track soon
        public float DuckDb = -9f;       // dialogue ducking depth
        public float Gap = 0f;           // free-roam states: seconds of room tone between two tracks (music breathes instead of wall-to-wall)
        public float GapJitter = 0f;     // random extra seconds on top of Gap
        public string Note = "";
    }

    /// <summary>
    /// Static music data: which of the composer's tracks plays in which game situation.
    /// Rationale per track and state: Documentation/BL23/MusicCueSheet.md (older notes: MusicMap.md).
    /// </summary>
    public static class MusicLibrary
    {
        public const float TargetLufs = -16f;
        public const float ResumeWindowSeconds = 150f;

        static Dictionary<string, MusicTrack> _tracks;
        static Dictionary<MusicState, MusicStateDef> _states;
        static Dictionary<string, string> _stingerFiles;

        public static IEnumerable<MusicTrack> Tracks { get { Ensure(); return _tracks.Values; } }
        public static MusicTrack Track(string name) { Ensure(); return name != null && _tracks.TryGetValue(name, out var t) ? t : null; }
        public static MusicStateDef State(MusicState s) { Ensure(); return _states.TryGetValue(s, out var d) ? d : _states[MusicState.Silence]; }
        /// <summary>Stinger ids that play a composed music file instead of a synthesized sting.</summary>
        public static string StingerFile(string id) { Ensure(); return id != null && _stingerFiles.TryGetValue(id, out var f) ? f : null; }
        public static IEnumerable<KeyValuePair<string, string>> StingerFiles { get { Ensure(); return _stingerFiles; } }

        /// <summary>Tracks that must stay sample-aligned with a partner (loaded in memory rather than streamed).</summary>
        public static bool NeedsInMemory(string name)
        {
            Ensure();
            foreach (var t in _tracks.Values) if (t.Layer != null && (t.Name == name || t.Layer == name)) return true;
            return false;
        }

        static void Ensure()
        {
            if (_tracks != null) return;
            _tracks = new Dictionary<string, MusicTrack>();
            foreach (var t in BuildTracks()) _tracks[t.Name] = t;
            // vertical remix: "murder kick ver" is a sample-aligned stem of "murder" (same length, onset xcorr 0.95 at lag 0)
            _tracks["murder"].Layer = "murder_kick_ver"; _tracks["murder"].LayerGainDb = -10f;

            _states = new Dictionary<MusicState, MusicStateDef>();
            foreach (var d in BuildStates()) _states[d.State] = d;
            foreach (MusicState s in Enum.GetValues(typeof(MusicState))) if (!_states.ContainsKey(s)) _states[s] = new MusicStateDef { State = s };
            ApplyCueOverrides();

            _stingerFiles = new Dictionary<string, string>();   // (Cong now belongs to the body discovery, per the owner's folders)
        }

        /// <summary>
        /// The owner's own cue sheet: BASSLINE_Data/StreamingAssets/music_cues.txt (next to the game) can reassign which
        /// tracks play in which situation without touching code — one line per situation: "DailyDay: happy, ceo, korean".
        /// Unknown track names are ignored (and logged); an empty list silences that situation.
        /// </summary>
        static void ApplyCueOverrides()
        {
            try
            {
                var path = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "music_cues.txt");
                if (!System.IO.File.Exists(path)) return;
                foreach (var raw in System.IO.File.ReadAllLines(path))
                {
                    var line = raw.Trim(); if (line.Length == 0 || line.StartsWith("#")) continue;
                    int c = line.IndexOf(':'); if (c <= 0) continue;
                    if (!Enum.TryParse(line.Substring(0, c).Trim(), true, out MusicState st)) continue;
                    var names = new List<string>(); int listed = 0;
                    foreach (var n0 in line.Substring(c + 1).Split(',')) { var n = n0.Trim(); if (n.Length == 0) continue; listed++; if (_tracks.ContainsKey(n)) names.Add(n); else UnityEngine.Debug.LogWarning("[BL23 Music] music_cues.txt: unknown track '" + n + "'"); }
                    if (listed > 0 && names.Count == 0) continue;   // only unknown names (e.g. a track not installed yet): keep the default
                    if (_states.TryGetValue(st, out var def)) def.Tracks = names.ToArray();
                }
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[BL23 Music] music_cues.txt: " + e.Message); }
        }

        static MusicTrack T(string name, string src, float gainDb, float start, float end, float tail, bool loop, float lufs, float dur)
            => new MusicTrack(name, src, gainDb, start, end, tail, loop, lufs, dur);

        static IEnumerable<MusicTrack> BuildTracks()
        {
            return new[]
            {
            //  resource name                 original file                                   gainDb  start    end     tail  seamless lufs   duration
            T("open", "open.wav", -3.5f, 0.00f, 30.00f, 0.0f, true, -12.5f, 30.00f),
            T("game", "Game.mp3", -2.2f, 0.00f, 102.25f, 1.2f, false, -13.8f, 102.79f),
            T("cloud", "Cloud.mp3", -0.7f, 0.00f, 36.75f, 0.2f, false, -15.3f, 36.96f),
            T("echoes_of_the_marble_hall", "Echoes of the Marble Hall.mp3", -4.1f, 0.00f, 110.75f, 4.2f, false, -11.9f, 110.97f),
            T("meeting_the_king", "Meeting the King.wav", -6.0f, 0.17f, 61.25f, 2.8f, false, -10.0f, 61.71f),
            T("beneath_the_iron_sky", "Beneath the Iron Sky.mp3", -3.8f, 0.55f, 163.25f, 2.2f, false, -12.2f, 163.85f),
            T("beneath_the_iron_sky_v2", "Beneath the Iron Sky ver 2.mp3", -4.6f, 0.41f, 173.25f, 0.8f, false, -11.4f, 173.45f),
            T("happy", "happy.mp3", -0.6f, 0.00f, 214.25f, 1.8f, false, -15.4f, 214.85f),
            T("an_ordinary_life", "An ordinary life.mp3", -3.4f, 0.90f, 162.75f, 5.2f, false, -12.6f, 163.92f),
            T("an_ordinary_life_2", "An ordinary life (2).mp3", -3.3f, 0.63f, 210.25f, 2.8f, false, -12.7f, 210.48f),
            T("my_apple", "My Apple.mp3", -0.5f, 0.07f, 138.25f, 1.8f, false, -15.5f, 138.43f),
            T("ceo", "CEO.mp3", -0.2f, 0.00f, 192.17f, 0.7f, false, -15.8f, 192.17f),
            T("korean", "korean.mp3", -1.7f, 0.00f, 66.25f, 1.8f, false, -14.3f, 67.66f),
            T("boomba", "boomba.mp3", 0.0f, 0.38f, 107.25f, 3.2f, false, -16.0f, 109.40f),
            T("drift_bawl", "drift bawl.wav", -5.6f, 0.15f, 144.75f, 0.2f, false, -10.4f, 145.50f),
            T("faded_ink", "Faded Ink.mp3", -3.0f, 0.26f, 138.67f, 1.7f, false, -13.0f, 138.67f),
            T("faded_ink_1", "Faded Ink (1).mp3", -3.0f, 0.97f, 182.66f, 3.7f, false, -13.0f, 182.66f),
            T("piano_pl", "piano PL.mp3", -0.5f, 0.00f, 201.75f, 4.2f, false, -15.5f, 203.06f),
            T("efefef", "efefef.mp3", -3.3f, 0.97f, 224.71f, 3.2f, false, -12.7f, 224.71f),
            T("glow", "Glow.wav", 0.0f, 0.00f, 151.25f, 1.8f, false, -16.9f, 154.37f),
            T("midnight_in_the_ruins", "Midnight in the Ruins.mp3", -3.7f, 0.64f, 149.21f, 0.7f, false, -12.3f, 149.21f),
            T("midnight_in_the_ruins_2", "Midnight in the Ruins (2).mp3", -1.9f, 0.46f, 198.62f, 1.6f, false, -14.1f, 198.62f),
            T("tired", "Tired.mp3", 0.0f, 0.12f, 186.75f, 3.2f, false, -16.6f, 187.54f),
            T("conspiracy_in_the_dark", "conspiracy in the dark.mp3", -3.6f, 0.66f, 114.50f, 1.5f, false, -12.4f, 114.50f),
            T("murder", "murder.mp3", 0.0f, 0.18f, 76.75f, 2.8f, false, -17.4f, 77.61f),
            T("murder_kick_ver", "murder kick ver.mp3", -8.2f, 0.18f, 74.75f, 0.8f, false, -7.8f, 77.61f),
            T("deep", "deep.mp3", 0.0f, 0.00f, 94.25f, 0.8f, false, -18.2f, 98.14f),
            T("ghost", "ghost.mp3", 0.0f, 0.44f, 212.25f, 3.8f, false, -16.4f, 212.79f),
            T("bat", "bat.mp3", -4.4f, 0.18f, 64.05f, 0.1f, true, -11.6f, 64.05f),
            T("people", "people.mp3", 0.0f, 0.00f, 96.25f, 2.8f, false, -16.2f, 96.31f),
            T("yeat", "yeat.wav", 0.0f, 0.03f, 72.18f, 0.2f, true, -17.9f, 72.18f),
            T("geugeol_bwasseo", "그걸 봤어_.mp3", -2.8f, 0.00f, 82.03f, 3.0f, false, -13.2f, 82.03f),
            T("inspection", "inspection.mp3", -3.2f, 0.00f, 162.75f, 2.2f, false, -12.8f, 164.06f),
            T("mystery", "Mystery.mp3", -5.3f, 0.13f, 136.75f, 4.2f, false, -10.7f, 140.07f),
            T("wolf", "Wolf.mp3", -2.4f, 0.00f, 116.25f, 0.8f, false, -13.6f, 116.33f),
            T("points_of_doubt", "Points of doubt.mp3", -8.3f, 0.20f, 61.57f, 1.1f, false, -7.7f, 61.57f),
            T("cyber", "cyber.mp3", -7.6f, 0.17f, 118.25f, 6.8f, false, -8.4f, 120.06f),
            T("track_p", "-P.mp3", -6.2f, 0.18f, 68.08f, 3.1f, false, -9.8f, 68.08f),
            T("judgment", "judgment.mp3", -2.8f, 0.00f, 104.57f, 2.6f, false, -13.2f, 104.57f),
            T("judgment_guitar", "judgment guitar.mp3", 0.0f, 0.09f, 194.25f, 0.8f, false, -16.3f, 194.71f),
            T("judgment_2", "judgment 2.mp3", -2.3f, 0.37f, 178.75f, 4.8f, false, -13.7f, 178.80f),
            T("judgment_2_guitar", "judgment 2 guitar.mp3", -0.1f, 0.07f, 208.75f, 2.8f, false, -15.9f, 209.59f),
            T("saul_theme", "Saul theme.mp3", -0.4f, 0.06f, 142.25f, 1.8f, false, -15.6f, 142.39f),
            T("debate", "debate.mp3", -9.2f, 0.17f, 99.25f, 1.8f, false, -6.8f, 99.37f),
            T("jinjja", "진짜_.mp3", -7.0f, 0.13f, 150.25f, 9.2f, false, -9.0f, 158.88f),
            T("ai", "ai.mp3", -8.6f, 0.13f, 99.75f, 0.2f, false, -7.4f, 102.27f),
            T("female", "female.mp3", -8.1f, 0.18f, 132.75f, 1.2f, false, -7.9f, 135.34f),
            T("boss", "BOSS.mp3", -7.5f, 0.13f, 126.67f, 3.2f, false, -8.5f, 126.67f),
            T("final", "final.mp3", -8.4f, 0.18f, 103.75f, 7.2f, false, -7.6f, 109.77f),
            T("eege", "eege.mp3", -7.1f, 0.18f, 182.75f, 10.2f, false, -8.9f, 186.28f),
            T("gaze", "Gaze.mp3", -9.3f, 0.26f, 128.75f, 0.2f, false, -6.7f, 131.27f),
            T("i_dont_regret_it", "I don't regret it.mp3", -8.8f, 0.18f, 168.75f, 1.2f, false, -7.2f, 172.85f),
            T("symphonic_suite_aot", "SymphonicSuite [AoT] Part1-4th7-b@$ (1).mp3", -2.4f, 0.54f, 103.75f, 2.2f, false, -13.6f, 104.57f),
            T("kill_your_self", "Kill your self.mp3", -6.1f, 0.13f, 151.25f, 8.2f, false, -9.9f, 153.65f),
            T("gore", "gore.mp3", -10.8f, 0.17f, 51.25f, 0.8f, false, -5.2f, 52.06f),
            T("amu_geokjeong_eopseo", "아무 걱정이 필요 없어.mp3", -10.0f, 0.18f, 101.75f, 1.2f, false, -6.0f, 103.18f),
            T("jugeo", "죽어.mp3", -6.5f, 0.18f, 121.57f, 3.1f, false, -9.5f, 121.57f),
            T("wah", "WAH.mp3", -7.7f, 0.13f, 115.75f, 1.2f, false, -8.3f, 116.98f),
            T("scru", "SCRU_.mp3", -9.4f, 0.40f, 110.75f, 2.2f, false, -6.6f, 110.84f),
            T("bandit_stroy", "Bandit stroy.mp3", -4.8f, 1.31f, 154.63f, 0.6f, false, -11.2f, 154.63f),
            T("pedal", "pedal.mp3", -8.6f, 0.17f, 158.75f, 2.8f, false, -7.4f, 158.76f),
            T("fu", "FU.mp3", -6.5f, 0.18f, 106.75f, 2.8f, false, -9.5f, 119.01f),
            T("raid", "raid.mp3", -8.0f, 0.09f, 96.05f, 0.1f, true, -8.0f, 96.05f),
            T("give_me", "Give me.mp3", -8.0f, 0.18f, 207.75f, 1.8f, false, -8.0f, 212.64f),
            T("geugeoyeotgun", "그거였군.mp3", -2.0f, 0.00f, 96.25f, 1.8f, false, -14.0f, 99.37f),
            T("modeun_geol_ara", "모든 걸 알아.mp3", 0.0f, 0.08f, 175.75f, 2.2f, false, -16.4f, 175.99f),
            T("tell_me_my_name", "Tell me my name.mp3", -1.8f, 0.46f, 104.71f, 0.7f, false, -14.2f, 104.71f),
            T("tell_me_my_name_2", "Tell me my name (2).mp3", -1.3f, 0.00f, 94.75f, 0.2f, false, -14.7f, 94.75f),
            T("tell_me_my_name_3", "Tell me my name (3).mp3", -3.1f, 1.19f, 100.75f, 2.8f, false, -12.9f, 104.04f),
            T("pianop", "pianop (1).mp3", -1.1f, 0.00f, 147.60f, 16.1f, false, -14.9f, 147.60f),
            T("hope", "hope.wav", -6.4f, 0.16f, 136.25f, 2.8f, false, -9.6f, 140.31f),
            T("future", "future.mp3", -5.8f, 0.17f, 169.75f, 4.8f, false, -10.2f, 178.34f),
            T("i_cant_feel_it", "I can't feel it.mp3", -2.1f, 0.00f, 58.75f, 0.2f, true, -13.9f, 58.99f),
            T("cong", "Cong.mp3", -7.5f, 0.27f, 21.75f, 1.8f, false, -8.5f, 22.75f),
            T("hey", "Hey.mp3", -8.0f, 0.13f, 31.75f, 1.8f, false, -8.0f, 33.31f),   // owner pack 2026-09-27 (재판장 앞에서 대기할 때)
            };
        }

        static MusicStateDef S(MusicState s, string note, params string[] tracks) => new MusicStateDef { State = s, Tracks = tracks, Note = note };

        /// <summary>States heard while the player walks the mansion freely (music breathes, never whiplashes).</summary>
        public static bool IsFreeRoam(MusicState s) =>
            s == MusicState.DailyMorning || s == MusicState.DailyDay || s == MusicState.DailyEvening || s == MusicState.Night ||
            s == MusicState.Mystery || s == MusicState.Aftermath || s == MusicState.Gathering;
        /// <summary>Time-of-day states: switching between them waits for the current piece to end.</summary>
        public static bool IsTimeOfDay(MusicState s) =>
            s == MusicState.DailyMorning || s == MusicState.DailyDay || s == MusicState.DailyEvening || s == MusicState.Night;
        public static bool IsTrial(MusicState s) =>
            s == MusicState.TrialOpening || s == MusicState.TrialDebate || s == MusicState.TrialPressure || s == MusicState.TrialClimax || s == MusicState.Vote ||
            s == MusicState.TrialPanic || s == MusicState.TrialComic || s == MusicState.Closing;
        public static bool IsInvestigation(MusicState s) =>
            s == MusicState.Investigation || s == MusicState.InvestigationLate || s == MusicState.InvestigationGrim;

        /// <summary>
        /// The cue sheet follows the OWNER'S OWN FOLDERS (2026-09-27 22:40, "상황에 맞는 BGM,OST들.zip"):
        ///   평상시 BGM (슬픈 상황에서도 가능) → daily life by time of day, meals, odd rooms, grief after a verdict
        ///   npc들이랑 놀 때 → Bond (time together, bond talks) · 시신이 발견되고 직후 → BodyDiscovery
        ///   수사 BGM → Investigation(/Late) · 훼손도·참혹성·잔혹성·트릭의 복잡함이 클 때 → InvestigationGrim
        ///   범인의 윤곽이 보일 때 → Closing · 재판장 앞에서 대기할 때 → Assembly · 재판 일반 → TrialOpening/TrialDebate/Vote
        ///   재판 다른 모드들 → TrialPressure (rule games, mini-modes) · 재판 때 패닉 → TrialPanic (+ Execution)
        ///   재판 때 npc들 웃긴 추리 할 때 → TrialComic · 범인과 결정적으로 말싸움을 할 때 → TrialClimax
        ///   저택에서 예상치 못한 걸 발견했을 때 → Surprise (one piece, then back to what was playing)
        /// Situations the owner's folders do not name (title, prologue, verdicts, execution, escape, reveal, loop reset) keep
        /// earlier choices. Daily music must not run back to back (owner): 1.5–3.5 min of room tone between daily pieces, and
        /// the rest carries across room and time-of-day changes (MusicDirector).
        /// Keep Assets/StreamingAssets/music_cues.txt in sync with the track lists here.
        /// </summary>
        static IEnumerable<MusicStateDef> BuildStates()
        {
            var L = new List<MusicStateDef>();
            MusicStateDef d;

            d = S(MusicState.Silence, "everything fades out"); d.FadeOut = 1.5f; L.Add(d);

            d = S(MusicState.Title, "30 s wide ambient pad composed to loop", "open");
            d.Mode = MusicPlayMode.Hold; d.Volume = 0.9f; d.FadeIn = 3f; d.FadeOut = 1.5f; L.Add(d);

            d = S(MusicState.Menu, "pause / options", "open");
            d.Mode = MusicPlayMode.Hold; d.Volume = 0.6f; d.FadeIn = 0.8f; d.FadeOut = 0.5f; L.Add(d);

            d = S(MusicState.Prologue, "arrival in the marble mansion, the butler presents the game, the statement",
                "echoes_of_the_marble_hall", "beneath_the_iron_sky", "glow");
            d.Mode = MusicPlayMode.Sequence; d.Volume = 0.82f; d.FadeIn = 3f; d.FadeOut = 2f; d.Crossfade = 3f; L.Add(d);

            // ---------------- 평상시 BGM: free roam with long rests between pieces (never back to back)
            d = S(MusicState.DailyMorning, "평상시: waking house", "an_ordinary_life_2", "piano_pl", "echoes_of_the_marble_hall", "glow", "people");
            d.Volume = 0.62f; d.FadeIn = 4f; d.FadeOut = 4f; d.Crossfade = 5f; d.Delay = 6f; d.Gap = 95f; d.GapJitter = 110f; L.Add(d);

            d = S(MusicState.DailyDay, "평상시: the long day", "people", "an_ordinary_life_2", "bat", "track_p", "tell_me_my_name_2", "piano_pl", "echoes_of_the_marble_hall");
            d.Volume = 0.6f; d.FadeIn = 4f; d.FadeOut = 4f; d.Crossfade = 5f; d.Delay = 6f; d.Gap = 95f; d.GapJitter = 110f; L.Add(d);

            d = S(MusicState.DailyEvening, "평상시: dusk", "pianop", "tell_me_my_name", "glow", "tell_me_my_name_3", "symphonic_suite_aot");
            d.Volume = 0.6f; d.FadeIn = 4.5f; d.FadeOut = 4f; d.Crossfade = 5f; d.Delay = 6f; d.Gap = 95f; d.GapJitter = 110f; L.Add(d);

            d = S(MusicState.Night, "평상시: sleeping house", "midnight_in_the_ruins", "midnight_in_the_ruins_2", "tired", "yeat");
            d.Volume = 0.55f; d.FadeIn = 5f; d.FadeOut = 4f; d.Crossfade = 6f; d.Delay = 8f; d.Gap = 110f; d.GapJitter = 120f; L.Add(d);

            d = S(MusicState.Mystery, "평상시: rooms with no purpose", "yeat", "bat", "track_p", "people");
            d.Volume = 0.6f; d.FadeIn = 3f; d.FadeOut = 2.5f; d.Crossfade = 4f; d.Delay = 4f; d.Gap = 80f; d.GapJitter = 90f; L.Add(d);

            d = S(MusicState.Gathering, "평상시: a shared meal, muffled under the table talk", "an_ordinary_life_2", "piano_pl", "people");
            d.Volume = 0.52f; d.FadeIn = 3f; d.FadeOut = 3f; d.Crossfade = 4f; d.DuckDb = -7f; d.Gap = 60f; d.GapJitter = 60f; L.Add(d);

            d = S(MusicState.Aftermath, "평상시 (슬픈 상황): the morning after a verdict", "tell_me_my_name", "tell_me_my_name_3", "symphonic_suite_aot", "pianop");
            d.Volume = 0.62f; d.FadeOut = 3f; d.Delay = 1f; d.FadeIn = 4f; d.Crossfade = 5f; d.Gap = 70f; d.GapJitter = 70f; L.Add(d);

            // ---------------- npc들이랑 놀 때
            d = S(MusicState.Bond, "npc들이랑 놀 때: time together, bond talks (composed to loop)", "i_cant_feel_it");
            d.Mode = MusicPlayMode.Hold; d.Volume = 0.58f; d.FadeIn = 2.5f; d.FadeOut = 3f; d.DuckDb = -6f; L.Add(d);

            d = S(MusicState.Tension, "fear / someone is planning", "deep", "mystery", "murder");
            d.Volume = 0.72f; d.FadeIn = 3f; d.FadeOut = 2.5f; d.Crossfade = 4f; d.Intensity = 0f; L.Add(d);

            // ---------------- 저택에서 예상치 못한 걸 발견했을 때 (a moment: one piece, then back)
            d = S(MusicState.Surprise, "예상치 못한 발견", "boomba", "cloud");
            d.Volume = 0.7f; d.FadeIn = 0.8f; d.FadeOut = 1.2f; d.Crossfade = 3f; d.Resume = false; L.Add(d);

            // ---------------- 시신이 발견되고 직후 (after the discovery film's own sting)
            d = S(MusicState.BodyDiscovery, "시신 발견 직후: the murder stems (kick held back like a pulse), the King, Cong",
                "murder", "meeting_the_king", "cong");
            d.Volume = 0.8f; d.FadeOut = 0.12f; d.Delay = 2.2f; d.FadeIn = 1.2f; d.Crossfade = 3f; d.Intensity = 0.35f; d.Resume = false; L.Add(d);

            // ---------------- 수사 BGM
            d = S(MusicState.Investigation, "수사", "inspection", "mystery", "ghost", "efefef", "hope", "deep", "future");
            d.Volume = 0.64f; d.FadeIn = 3f; d.FadeOut = 2.5f; d.Crossfade = 4f; d.Gap = 12f; d.GapJitter = 18f; d.DuckDb = -8f; L.Add(d);

            d = S(MusicState.InvestigationLate, "수사 막바지", "kill_your_self", "future", "inspection");
            d.Volume = 0.7f; d.FadeIn = 2f; d.FadeOut = 2f; d.Crossfade = 3f; d.Gap = 4f; d.GapJitter = 4f; L.Add(d);

            d = S(MusicState.InvestigationGrim, "수사: 훼손도·참혹성·잔혹성·트릭의 복잡함이 클 때", "eege", "gaze", "give_me");
            d.Volume = 0.72f; d.FadeIn = 2f; d.FadeOut = 2f; d.Crossfade = 3f; d.Gap = 8f; d.GapJitter = 10f; d.DuckDb = -8f; L.Add(d);

            // ---------------- 범인의 윤곽이 보일 때
            d = S(MusicState.Closing, "범인의 윤곽이 보일 때", "points_of_doubt", "final", "raid", "happy");
            d.Volume = 0.8f; d.FadeIn = 1.5f; d.FadeOut = 1.2f; d.Crossfade = 3f; d.DuckDb = -8f; L.Add(d);

            // ---------------- 재판장 앞에서 대기할 때
            d = S(MusicState.Assembly, "재판장 앞에서 대기: the summons and the walk to the court", "hey", "an_ordinary_life");
            d.Mode = MusicPlayMode.Sequence; d.Volume = 0.72f; d.FadeIn = 2f; d.FadeOut = 2f; d.Crossfade = 3f; L.Add(d);

            // ---------------- the 심판
            d = S(MusicState.TrialOpening, "재판 일반: the court opens", "saul_theme", "judgment", "korean");
            d.Volume = 0.88f; d.FadeIn = 1.5f; d.FadeOut = 1.5f; d.Crossfade = 3f; L.Add(d);

            d = S(MusicState.TrialDebate, "재판 일반: the debate",
                "judgment_2", "my_apple", "game", "fu", "korean", "geugeol_bwasseo", "judgment", "saul_theme", "geugeoyeotgun", "modeun_geol_ara");
            d.Volume = 0.85f; d.FadeIn = 1.2f; d.FadeOut = 1f; d.Crossfade = 3f; d.DuckDb = -8f; L.Add(d);

            d = S(MusicState.TrialPressure, "재판 다른 모드들: rule games and mini-modes", "cyber", "pedal", "i_dont_regret_it");
            d.Volume = 0.9f; d.FadeIn = 0.5f; d.FadeOut = 0.35f; d.Crossfade = 2f; d.DuckDb = -7f; L.Add(d);

            d = S(MusicState.TrialPanic, "재판 때 패닉: the culprit cracks, the room erupts",
                "judgment_guitar", "judgment_2_guitar", "drift_bawl", "jinjja", "jugeo", "wah", "scru", "gore", "amu_geokjeong_eopseo");
            d.Volume = 0.92f; d.FadeIn = 0.3f; d.FadeOut = 0.4f; d.Crossfade = 2f; d.DuckDb = -7f; d.Resume = false; L.Add(d);

            d = S(MusicState.TrialComic, "재판 때 npc들 웃긴 추리 할 때", "wolf");
            d.Mode = MusicPlayMode.Hold; d.Volume = 0.82f; d.FadeIn = 0.6f; d.FadeOut = 0.8f; d.DuckDb = -8f; L.Add(d);

            d = S(MusicState.TrialClimax, "범인과 결정적으로 말싸움을 할 때", "boss", "ceo", "female");
            d.Volume = 1f; d.FadeIn = 0.4f; d.FadeOut = 0.3f; d.Delay = 0.3f; d.Crossfade = 2f; d.DuckDb = -7f; L.Add(d);

            d = S(MusicState.Vote, "재판 일반: everyone's gaze, held until the ballots close", "geugeol_bwasseo");
            d.Mode = MusicPlayMode.Hold; d.Volume = 0.88f; d.FadeIn = 0.5f; d.FadeOut = 0.5f; d.Resume = false; L.Add(d);

            d = S(MusicState.VerdictCorrect, "the culprit's confession", "i_dont_regret_it", "glow");
            d.Volume = 0.9f; d.FadeOut = 0.2f; d.Delay = 1.5f; d.FadeIn = 1.5f; d.Resume = false; L.Add(d);

            d = S(MusicState.VerdictWrong, "wrong verdict: tragedy", "symphonic_suite_aot", "kill_your_self");
            d.Volume = 0.88f; d.FadeOut = 0.2f; d.Delay = 1.8f; d.FadeIn = 1.5f; d.Resume = false; L.Add(d);

            d = S(MusicState.Execution, "grotesque, ironic (from the panic folder)", "gore", "amu_geokjeong_eopseo", "jugeo", "wah", "scru");
            d.Volume = 1f; d.FadeOut = 0.3f; d.FadeIn = 0.2f; d.Crossfade = 1.5f; d.Resume = false; L.Add(d);

            d = S(MusicState.Escape, "the culprit gets away with the wish", "bandit_stroy", "pedal", "fu", "raid", "give_me");
            d.Volume = 1f; d.FadeOut = 0.3f; d.FadeIn = 0.3f; d.Crossfade = 1.5f; d.Resume = false; L.Add(d);

            d = S(MusicState.Reveal, "3D replay of the true events: 'so that was it' / 'I know everything'", "geugeoyeotgun", "modeun_geol_ara");
            d.Volume = 0.82f; d.FadeOut = 1f; d.FadeIn = 1.5f; d.Crossfade = 3f; d.Resume = false; L.Add(d);

            d = S(MusicState.LoopReset, "hard cut to silence, then the uncanny sequence", "future", "tell_me_my_name_3");
            d.Mode = MusicPlayMode.Sequence; d.Volume = 0.82f; d.FadeOut = 0.05f; d.Delay = 1.6f; d.FadeIn = 2.5f; d.Crossfade = 4f; d.Resume = false; L.Add(d);

            return L;
        }
    }
}
