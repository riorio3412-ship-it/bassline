using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Audio
{
    /// <summary>
    /// Room ambience: looping beds per room type (Resources/Ambience/*, CC0 recordings), random one-shot "sprinkles" around the
    /// listener (floor creaks, drips, wind gusts, distant thunder on rain nights, cutlery at a full table, pages in the library,
    /// the hour chime in rooms with clocks) and the listener's room reverb (one AudioReverbZone that follows the ear).
    /// Everything cross-fades by the player's room and the time of day, is quieter at night, dips under dialogue, broadcasts
    /// and cinematics, rises a little while the music rests between pieces, and goes silent in the courtroom (the trial runs
    /// its own court ambience). Self-starting; reads Session/GameState only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbienceDirector : MonoBehaviour
    {
        public static AmbienceDirector I;
        /// <summary>Overall ambience trim (dB) on top of the SFX volume.</summary>
        public float MasterDb = 0f;
        /// <summary>Beds rise this much while the music is silent between pieces.</summary>
        public float MusicRestBoostDb = 3f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { Ensure(); }

        public static AmbienceDirector Ensure()
        {
            if (I != null) return I;
            var go = new GameObject("BL23_Ambience"); DontDestroyOnLoad(go);
            I = go.AddComponent<AmbienceDirector>();
            return I;
        }

        // ------------------------------------------------------------------ beds
        sealed class Layer
        {
            public string Bed; public float Pitch = 1f; public AudioSource Src; public ResourceRequest Req; public bool Failed;
            public float Level, Target, Applied = -1f; public bool Paused; // linear
        }
        readonly Dictionary<string, Layer> _layers = new Dictionary<string, Layer>();
        readonly Dictionary<string, float> _want = new Dictionary<string, float>();   // key -> dB (LUFS-ish at 100% volume)
        const float BedRefLufs = -30f;                                                  // files are normalised to this

        Layer GetLayer(string key)
        {
            if (_layers.TryGetValue(key, out var l)) return l;
            string bed = key; float pitch = 1f; int at = key.IndexOf('@');
            if (at > 0) { bed = key.Substring(0, at); float.TryParse(key.Substring(at + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out pitch); }
            l = new Layer { Bed = bed, Pitch = pitch <= 0 ? 1f : pitch };
            var go = new GameObject("Amb_" + key); go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = true; s.spatialBlend = 0f; s.priority = 64; s.volume = 0f; s.dopplerLevel = 0f;
            s.bypassReverbZones = true; s.bypassEffects = true; s.pitch = l.Pitch;
            l.Src = s;
            try { l.Req = Resources.LoadAsync<AudioClip>("Ambience/" + bed); } catch (System.Exception) { l.Failed = true; }
            _layers[key] = l;
            return l;
        }

        void Want(string key, float lufs) { if (!_want.TryGetValue(key, out var v) || lufs > v) _want[key] = lufs; }

        // ------------------------------------------------------------------ state
        Transform _ear; float _nextEarLookup;
        int _room = -1; RoomType _type; bool _exterior, _hasRoom; float _nextSlow; int _npcsHere; int _lastHour = -1;
        AudioReverbZone _zone; AudioReverbPreset _preset = AudioReverbPreset.Off;
        readonly Dictionary<string, float> _nextSprinkle = new Dictionary<string, float>();
        SynthRng _rng;

        void Awake()
        {
            if (I != null && I != this) { Destroy(this); return; }
            I = this;
            _rng = new SynthRng((uint)System.Environment.TickCount | 3u);
            var z = new GameObject("RoomReverb"); z.transform.SetParent(transform, false);
            _zone = z.AddComponent<AudioReverbZone>(); _zone.minDistance = 5000f; _zone.maxDistance = 6000f; _zone.reverbPreset = AudioReverbPreset.Off;
        }
        void OnDestroy() { if (I == this) I = null; }

        void Update() { AudioPerf.BeginAmb(); try { UpdateImpl(); } finally { AudioPerf.EndAmb(); } }
        void UpdateImpl()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f), now = Time.unscaledTime;
            if (now >= _nextEarLookup) { _nextEarLookup = now + 1f; _ear = FindEar(); }

            // targets, ducking and sprinkles at 5 Hz; the fades below run every frame
            if (now >= _nextTargets)
            {
                _nextTargets = now + 0.2f;
                _want.Clear(); _duckDb = 0f; _world = BuildTargets(now, ref _duckDb);
                foreach (var kv in _want) GetLayer(kv.Key);
                foreach (var l in _layers.Values) l.Target = 0f;
                foreach (var kv in _want) if (_layers.TryGetValue(kv.Key, out var w)) w.Target = AudioVolumes.DbToLin(kv.Value - BedRefLufs);
                if (_world) Sprinkles(now);
                LogChange(_world);
            }
            bool world = _world;

            // music resting between pieces: the room breathes
            var md = MusicDirector.I; float rest = world && md != null && md.IsSilent ? MusicRestBoostDb : 0f;
            float bus = AudioVolumes.SfxGain * AudioVolumes.DbToLin(MasterDb + _duckDb + rest);

            foreach (var l in _layers.Values)
            {
                if (l.Src.clip == null && !l.Failed && l.Req != null && l.Req.isDone)
                {
                    l.Src.clip = l.Req.asset as AudioClip; l.Failed = l.Src.clip == null;
                    if (l.Failed) Debug.LogWarning("[BL23 Amb] missing bed Resources/Ambience/" + l.Bed);
                    if (l.Src.clip != null) { l.Src.time = _rng.F() * l.Src.clip.length * 0.9f; }
                }
                // ~2.5 s fades in linear gain (slower out than in)
                float rate = (l.Target > l.Level ? 0.5f : 0.35f) * Mathf.Max(0.05f, Mathf.Max(l.Target, l.Level));
                l.Level = Mathf.MoveTowards(l.Level, l.Target, rate * dt * 1.6f);
                if (l.Src.clip == null) continue;
                float v = Mathf.Clamp01(l.Level * bus);
                if (Mathf.Abs(v - l.Applied) > 0.0002f) { l.Src.volume = v; l.Applied = v; }
                if (v > 0.0005f) { if (l.Paused) { l.Src.UnPause(); l.Paused = false; } else if (!l.Src.isPlaying) l.Src.Play(); }
                else if (l.Src.isPlaying && l.Target <= 0f) { l.Src.Pause(); l.Paused = true; }
            }
        }
        float _nextTargets, _duckDb; bool _world;

        string _lastLog; int _logs;
        void LogChange(bool world)
        {
            if (_logs >= 40 || AudioFlags.Quiet) return;
            var keys = new List<string>(); foreach (var kv in _want) keys.Add(kv.Key + " " + kv.Value.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
            keys.Sort(System.StringComparer.Ordinal);
            string s = world ? $"{_type} rev={_preset} beds=[{string.Join(", ", keys)}]" : "off";
            if (s == _lastLog) return;
            _lastLog = s; _logs++;
            int loaded = 0; foreach (var l in _layers.Values) if (l.Src.clip != null) loaded++;
            Debug.Log($"[BL23 Amb] {s} loaded {loaded}/{_layers.Count}");
        }

        static Transform FindEar()
        {
            var ls = Object.FindObjectsByType<AudioListener>();
            foreach (var l in ls) if (l != null && l.isActiveAndEnabled) return l.transform;
            return ls.Length > 0 ? ls[0].transform : null;
        }

        // ------------------------------------------------------------------ targets per room / time
        bool BuildTargets(float now, ref float duckDb)
        {
            var ses = Session.I;
            if (AudioFlags.NoAmbience || ses == null || ses.Sim == null || ses.S == null || ses.S.Layout == null) { SetReverb(AudioReverbPreset.Off); return false; }
            var S = ses.S;
            bool trial = (ses.Trial != null && ses.Trial.Active) || S.Phase == Phase.Trial || S.Phase == Phase.Verdict || S.Phase == Phase.Execution;
            if (trial) { SetReverb(AudioReverbPreset.Off); return false; }                 // the courtroom has its own staging

            // slow refresh: room, occupants
            if (now >= _nextSlow)
            {
                _nextSlow = now + 0.5f;
                var p = S.Player; var r = p != null ? S.Layout.Room(p.Room) : null;
                _hasRoom = r != null;
                if (r != null) { _room = r.Id; _type = r.Type; _exterior = r.Exterior; }
                int id = _room; _npcsHere = _hasRoom ? S.LivingNpcs.Count(a => a.Room == id) : 0;
            }
            if (!_hasRoom) { SetReverb(AudioReverbPreset.Off); Want("roomtone_house", -46f); return true; }
            var room = S.Layout.Room(_room);

            int m = S.Minute; bool night = m >= 22 * 60 || m < 6 * 60, evening = m >= 18 * 60 && m < 22 * 60, deep = m >= 1 * 60 && m < 5 * 60;
            bool rain = RainNight(S) && (m >= 19 * 60 || m < 5 * 60);
            bool power = room == null || S.CircuitOn(room.Circuit);
            float nightDb = night ? -3f : 0f;

            // ducking: dialogue, cinematics, menus, broadcasts
            if (ses.Dialogue != null && ses.Dialogue.Active) duckDb -= 6f;
            if (ses.Cine != null && ses.Cine.Busy) duckDb -= 8f;
            if ((ses.Menu != null && ses.Menu.Open) || (ses.Note != null && ses.Note.Open)) duckDb -= 6f;
            if (VoiceBabble.IsSpeaking) duckDb -= 3f;
            if (ses.Reveal != null && ses.Reveal.Active) duckDb -= 8f;

            // the air of the house everywhere
            Want("roomtone_house", -46f + nightDb);

            switch (_type)
            {
                case RoomType.Corridor: case RoomType.Landing: case RoomType.Stairwell:
                    Want("hall_tone", -48f + nightDb);
                    if (night) Want("wind_interior", deep ? -44f : -47f);
                    break;
                case RoomType.GrandHall:
                    Want("hall_tone", -40f + nightDb); Want("clock_ticking", -44f);
                    if (night) Want("wind_interior", -48f);
                    break;
                case RoomType.Chapel: case RoomType.Theater: case RoomType.MusicRoom:
                    Want("hall_tone", -42f + nightDb); break;
                case RoomType.Gallery: case RoomType.TrophyRoom: case RoomType.DollRoom:
                    Want("hall_tone", -44f + nightDb); if (_type == RoomType.DollRoom) Want("clock_ticking", -46f); break;
                case RoomType.Lounge: case RoomType.Parlor: case RoomType.TeaRoom:
                    Want("fireplace", evening || night ? -35f : -39f); break;
                case RoomType.Study: case RoomType.Library: case RoomType.Archive:
                    if (_type != RoomType.Archive) Want("fireplace", evening || night ? -37f : -41f);
                    Want("clock_ticking", _type == RoomType.Study ? -40f : -44f); break;
                case RoomType.Bedroom: case RoomType.GuestRoom: case RoomType.ButlerRoom:
                    Want("clock_ticking", -46f); break;
                case RoomType.Dining:
                    Want("hall_tone", -48f); break;
                case RoomType.Kitchen:
                    if (!night) Want("kitchen_simmer", MealTime(m) ? -35f : -41f);
                    if (power) Want("ventilation", -48f);
                    break;
                case RoomType.Greenhouse:
                    if (night) Want("night_insects", -38f); else Want("greenhouse", -37f);
                    break;
                case RoomType.Courtyard:
                    if (night) Want("night_insects", -34f); else Want("greenhouse", -44f);
                    Want("wind_interior", -44f);
                    break;
                case RoomType.Pool:
                    Want("pool_water", -35f); break;
                case RoomType.WaterRoom:
                    Want("pool_water", -42f); if (power) Want("machine_hum", -40f); break;
                case RoomType.MirrorWater:
                    Want("pool_water", -44f); Want("hall_tone", -44f); break;
                case RoomType.MachineRoom:
                    if (power) Want("machine_hum", -35f); break;
                case RoomType.PowerRoom:
                    if (power) Want("machine_hum", -38f); Want("ventilation", -44f); break;
                case RoomType.Laundry:
                    if (power) Want("ventilation", -42f); break;
                case RoomType.Infirmary:
                    if (power) Want("ventilation", -47f); break;
                case RoomType.BoilerRoom:
                    Want("boiler", -35f); Want("cellar", -46f); break;
                case RoomType.WineCellar:
                    Want("cellar", -36f); break;
                case RoomType.Storage: case RoomType.Closet: case RoomType.Wardrobe: case RoomType.Workshop:
                    Want("roomtone_house", -44f); break;
                case RoomType.Elevator:
                    Want("machine_hum", -44f); break;
                case RoomType.RainCorridor:
                    Want("rain_heavy", -33f); Want("hall_tone", -46f); break;
                case RoomType.EmptyAuditorium:
                    Want("crowd_far", -40f); Want("hall_tone", -42f); break;
                case RoomType.WaitingRoom:
                    Want("clock_ticking", -40f); Want("hall_tone", -48f); break;
                case RoomType.WhiteDoors:
                    Want("hall_tone", -43f); break;
                case RoomType.ClockMuseum:
                    Want("clock_ticking@0.94", -40f); Want("clock_ticking", -41f); Want("clock_ticking@1.07", -42f); Want("hall_tone", -46f); break;
                case RoomType.GameRoom:
                    break;
            }
            // weather: some nights it rains (deterministic per day), heard on the windows
            if (rain && _type != RoomType.RainCorridor)
            {
                bool wet = _exterior || _type == RoomType.Greenhouse || _type == RoomType.Courtyard;
                Want("rain_window", wet ? -36f : (RoomInfo.IsPassage(_type) ? -44f : -48f));
                if (wet) Want("wind_interior", -45f);
            }
            SetReverb(ReverbFor(_type));
            return true;
        }

        static bool MealTime(int m) => (m >= 6 * 60 + 30 && m < 9 * 60) || (m >= 11 * 60 + 30 && m < 13 * 60 + 30) || (m >= 17 * 60 + 30 && m < 20 * 60);

        /// <summary>About a third of the nights rain, the same nights in every playthrough of a loop.</summary>
        static bool RainNight(GameState S)
        {
            int day = S.Minute < 6 * 60 ? S.Day - 1 : S.Day;
            uint h = (uint)(day * 73856093) ^ (uint)(S.Loop * 19349663) ^ 0x9E3779B9u; h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            return h % 100u < 34u;
        }

        // ------------------------------------------------------------------ sprinkles
        bool Due(string key, float now, float min, float max)
        {
            if (!_nextSprinkle.TryGetValue(key, out var t)) { _nextSprinkle[key] = now + _rng.Range(min * 0.4f, max); return false; }
            if (now < t) return false;
            _nextSprinkle[key] = now + _rng.Range(min, max);
            return true;
        }

        Vector3 Around(float rMin, float rMax, float y = 0.5f)
        {
            var c = _ear != null ? _ear.position : Vector3.zero;
            float a = _rng.Range(0f, Mathf.PI * 2f), r = _rng.Range(rMin, rMax);
            return c + new Vector3(Mathf.Cos(a) * r, _rng.Range(-0.3f, 1.2f) + y - 0.5f, Mathf.Sin(a) * r);
        }

        void Sprinkles(float now)
        {
            var ses = Session.I; if (ses == null || ses.S == null || !_hasRoom || _ear == null) return;
            if (ses.Cine != null && ses.Cine.Busy) return;
            var S = ses.S; int m = S.Minute; bool night = m >= 22 * 60 || m < 6 * 60;
            bool quietRoom = !(_type == RoomType.MachineRoom || _type == RoomType.PowerRoom || _type == RoomType.Pool || _type == RoomType.BoilerRoom);

            // the old house settles: creaks (more at night, in corridors and wooden rooms)
            bool creaky = RoomInfo.IsPassage(_type) || _type == RoomType.Library || _type == RoomType.Study || _type == RoomType.Gallery || _type == RoomType.Bedroom || _type == RoomType.GuestRoom || _type == RoomType.Archive || _type == RoomType.Stairwell;
            if (quietRoom && Due("creak", now, night ? 14f : 28f, night ? 40f : 75f) && (creaky || _rng.F() < 0.4f))
                Sfx.PlayEx("amb_creak", Around(4f, 11f), night ? 0.9f : 0.7f, 1f, -1, _rng.F() < 0.5f ? 2500f : 0f);

            // water drips in damp rooms
            if ((_type == RoomType.WineCellar || _type == RoomType.Greenhouse || _type == RoomType.BoilerRoom || _type == RoomType.MirrorWater || _type == RoomType.WaterRoom || _type == RoomType.Pool) && Due("drip", now, 2.5f, 9f))
                Sfx.PlayEx("amb_drip", Around(1.5f, 6f, 1.5f), _rng.Range(0.5f, 1f), 1f);

            // rain nights: distant thunder
            bool rain = RainNight(S) && (m >= 19 * 60 || m < 5 * 60);
            if ((rain || _type == RoomType.RainCorridor) && Due("thunder", now, 35f, 95f))
                Sfx.PlayEx("amb_thunder", null, _exterior || _type == RoomType.RainCorridor ? _rng.Range(0.7f, 1f) : _rng.Range(0.35f, 0.6f), 1f, -1, _exterior ? 0f : 1200f);

            // night wind finds the gaps in the windows
            if (night && (RoomInfo.IsPassage(_type) || _exterior || _type == RoomType.Courtyard) && Due("gust", now, 30f, 80f))
                Sfx.PlayEx("amb_wind_gust", null, rain ? 0.9f : 0.6f, 1f);

            // a full table / tea room / kitchen: cutlery, cups
            if ((_type == RoomType.Dining || _type == RoomType.TeaRoom || _type == RoomType.Kitchen) && _npcsHere >= 2 && Due("cutlery", now, 2.5f, 7f))
                Sfx.PlayEx("amb_cutlery", Around(2f, 6f, 0.9f), _rng.Range(0.6f, 1f), 1f);

            // someone reading
            if ((_type == RoomType.Library || _type == RoomType.Archive || _type == RoomType.Study) && _npcsHere >= 1 && Due("page", now, 8f, 20f))
                Sfx.PlayEx("amb_page", Around(2f, 6f, 0.9f), _rng.Range(0.6f, 1f), 1f);

            // the hour, in rooms with a clock
            int hour = m / 60;
            if (_lastHour >= 0 && hour != _lastHour && (m % 60) < 3)
            {
                bool clockRoom = _type == RoomType.Study || _type == RoomType.GrandHall || _type == RoomType.ClockMuseum || _type == RoomType.Library || _type == RoomType.Parlor || _type == RoomType.Lounge || _type == RoomType.WaitingRoom;
                if (clockRoom) Sfx.PlayEx("amb_clock_chime", Around(3f, 7f, 1.8f), 0.9f, _type == RoomType.ClockMuseum ? _rng.Range(0.9f, 1.1f) : 1f);
            }
            _lastHour = hour;
        }

        // ------------------------------------------------------------------ reverb
        static AudioReverbPreset ReverbFor(RoomType t)
        {
            switch (t)
            {
                case RoomType.GrandHall: case RoomType.Chapel: case RoomType.EmptyAuditorium: case RoomType.Theater: return AudioReverbPreset.Auditorium;
                case RoomType.Corridor: case RoomType.Landing: return AudioReverbPreset.Hallway;
                case RoomType.Stairwell: case RoomType.RainCorridor: return AudioReverbPreset.StoneCorridor;
                case RoomType.Gallery: case RoomType.TrophyRoom: case RoomType.DollRoom: case RoomType.ClockMuseum: case RoomType.WhiteDoors:
                case RoomType.MirrorWater: case RoomType.Pool: case RoomType.WaterRoom: case RoomType.BoilerRoom: case RoomType.MachineRoom: case RoomType.PowerRoom:
                    return AudioReverbPreset.Stoneroom;
                case RoomType.WineCellar: return AudioReverbPreset.Cave;
                case RoomType.Lounge: case RoomType.Library: case RoomType.Study: case RoomType.Bedroom: case RoomType.GuestRoom: case RoomType.Archive:
                case RoomType.ButlerRoom: case RoomType.Wardrobe: return AudioReverbPreset.Livingroom;
                case RoomType.Courtyard: return AudioReverbPreset.Plain;
                case RoomType.Elevator: case RoomType.Closet: return AudioReverbPreset.PaddedCell;
                default: return AudioReverbPreset.Room;
            }
        }

        void SetReverb(AudioReverbPreset p)
        {
            if (_zone == null || p == _preset) return;
            _preset = p;
            if (p == AudioReverbPreset.Off) { _zone.reverbPreset = AudioReverbPreset.Off; return; }
            // take the preset, then soften its wet level (the rooms should feel real, not like a cathedral)
            _zone.reverbPreset = p;
            int room = _zone.room, roomHF = _zone.roomHF, roomLF = _zone.roomLF, refl = _zone.reflections, rev = _zone.reverb;
            float decay = _zone.decayTime, ratio = _zone.decayHFRatio, reflDelay = _zone.reflectionsDelay, revDelay = _zone.reverbDelay, hfRef = _zone.HFReference, lfRef = _zone.LFReference, diff = _zone.diffusion, dens = _zone.density;
            _zone.reverbPreset = AudioReverbPreset.User;
            _zone.room = Mathf.Max(-10000, room - 500); _zone.roomHF = roomHF; _zone.roomLF = roomLF; _zone.reflections = refl; _zone.reverb = rev - 200;
            _zone.decayTime = decay; _zone.decayHFRatio = ratio; _zone.reflectionsDelay = reflDelay; _zone.reverbDelay = revDelay;
            _zone.HFReference = hfRef; _zone.LFReference = lfRef; _zone.diffusion = diff; _zone.density = dens;
        }
    }
}
