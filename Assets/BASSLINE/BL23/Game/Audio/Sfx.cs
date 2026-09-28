using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Audio
{
    /// <summary>Handle for a looping sound started with <see cref="Sfx.Loop"/>.</summary>
    public sealed class SfxHandle
    {
        internal AudioSource Src; internal SfxInfo Info; internal float BaseVol = 1f;
        internal float Fade, FadeTarget = 1f, FadeRate = 10f; internal Transform FollowT; internal bool Stopping;
        public string Id => Info != null ? Info.Id : null;
        public bool IsPlaying => Src != null && !Stopping;
        public void Stop(float fade = 0.25f)
        {
            if (Src == null || Stopping) return;
            Stopping = true; FadeTarget = 0f; FadeRate = Mathf.Max(Fade, 0.01f) / Mathf.Max(0.01f, fade);
        }
        public void SetPosition(Vector3 p) { if (Src != null) Src.transform.position = p; }
        public void SetVolume(float v) { BaseVol = Mathf.Max(0f, v); }
        public void Follow(Transform t) { FollowT = t; }
        public void SetPitch(float p) { if (Src != null) Src.pitch = Mathf.Clamp(p, 0.1f, 3f); }
    }

    /// <summary>
    /// Sound effects. Two layers per id:
    ///  1) recorded samples (CC0; Resources/Sfx/&lt;id&gt;_&lt;n&gt; + Resources/Sfx/_manifest.txt with per-id volume, range, pitch and level
    ///     jitter, loop flag — built by BL23Lab/AudioLab/Forge, credits in Art/ThirdParty/CREDITS_AUDIO.md). Preloaded in the
    ///     background at startup; a variant requested before its turn is loaded on the spot.
    ///  2) the procedural recipe in <see cref="SfxSynth"/> for every id without samples (synthesized on a worker thread).
    /// Variants rotate randomly without immediate repeats; the same id never stacks more than a few voices at once.
    /// 3D playback uses a pool of AudioSources with a custom rolloff that reaches silence at the max distance, and passes
    /// through the listener's room reverb (see AmbienceDirector); UI sounds stay dry.
    /// <code>Sfx.Play("door_open", doorPos); var rain = Sfx.Loop("rain_loop", corridorPos); rain.Stop(1f);</code>
    /// </summary>
    public static class Sfx
    {
        public const int PoolSize = 28;
        const int Rate = SfxSynth.DefaultRate;

        static SfxRunner _runner;
        static readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        static readonly List<AudioSource> _pool = new List<AudioSource>();
        static readonly List<string> _poolId = new List<string>();
        static readonly List<SfxHandle> _loops = new List<SfxHandle>();
        static readonly HashSet<string> _warned = new HashSet<string>();
        static readonly Dictionary<SfxRange, AnimationCurve> _curves = new Dictionary<SfxRange, AnimationCurve>();
        static SynthRng _rng = new SynthRng(0x5F3759DFu);
        static int _next;
        static readonly Dictionary<string, int> _lastVariant = new Dictionary<string, int>();
        static readonly Dictionary<string, int> _prevVariant = new Dictionary<string, int>();

        /// <summary>True once every synth clip has been made and every sample preloaded.</summary>
        public static bool Ready { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _runner = null; _clips.Clear(); _pool.Clear(); _poolId.Clear(); _loops.Clear(); _warned.Clear(); _curves.Clear(); _lastVariant.Clear(); _prevVariant.Clear();
            _sets = null; _probed.Clear(); Ready = false; _next = 0;
        }

        /// <summary>Create the host object and start background loading/synthesis. Safe to call repeatedly.</summary>
        public static void Init()
        {
            if (_runner != null) return;
            var go = new GameObject("BL23_Sfx");
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<SfxRunner>();
            for (int i = 0; i < PoolSize; i++) { _pool.Add(NewSource(go.transform, "Sfx" + i)); _poolId.Add(null); }
            LoadManifest();
            _runner.StartCoroutine(GenerateAll());
        }

        static AudioSource NewSource(Transform parent, string name)
        {
            // built inactive: an enabled AudioSource with a filter and no clip logs "Only custom filters can be played" on enable
            var g = new GameObject(name); g.SetActive(false); g.transform.SetParent(parent, false);
            var s = g.AddComponent<AudioSource>();
            s.playOnAwake = false; s.dopplerLevel = 0f; s.spread = 0f; s.priority = 128;
            var lp = g.AddComponent<AudioLowPassFilter>(); lp.enabled = false; lp.cutoffFrequency = 22000f;
            g.SetActive(true);
            return s;
        }

        // ------------------------------------------------------------------ recorded sample sets
        sealed class SampleSet
        {
            public string Id; public int Count; public float Vol = 1f, Jitter = 0.04f, VolJitterDb = 1f; public SfxRange Range = SfxRange.Medium; public bool Loop;
            public AudioClip[] Clips; public bool[] Tried;
            public SfxInfo Info;
        }
        static Dictionary<string, SampleSet> _sets;

        static void LoadManifest()
        {
            _sets = new Dictionary<string, SampleSet>();
            TextAsset ta = null;
            if (AudioFlags.NoSamples) return;   // A/B check: synth recipes only
            try { ta = Resources.Load<TextAsset>("Sfx/_manifest"); } catch (System.Exception) { }
            if (ta == null) return;
            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.Trim(); if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split('|'); if (p.Length < 7) continue;
                var ss = new SampleSet { Id = p[0], Count = Mathf.Max(1, ParseI(p[1])), Vol = ParseF(p[2], 1f), Jitter = ParseF(p[4], 0.04f), VolJitterDb = ParseF(p[5], 1f), Loop = p[6] == "1" };
                if (!System.Enum.TryParse(p[3], out ss.Range)) ss.Range = SfxRange.Medium;
                ss.Clips = new AudioClip[ss.Count]; ss.Tried = new bool[ss.Count];
                var synth = SfxSynth.Get(ss.Id);
                ss.Info = new SfxInfo { Id = ss.Id, Variants = ss.Count, Loop = ss.Loop || (synth != null && synth.Loop), Volume = ss.Vol, Range = ss.Range, PitchJitter = ss.Jitter };
                _sets[ss.Id] = ss;
            }
            // ---- BEGIN physical pack (audio track, 2026-09-27): second manifest; ids are played through PhysicalSounds.cs ----
            LoadExtraManifest("Sfx/_manifest_phys");
            // ---- END physical pack ----
        }

        // ---- BEGIN physical pack (audio track, 2026-09-27) ----
        /// <summary>A separately built sample manifest (same line format as _manifest.txt), e.g. the physical / violence pack
        /// written by BL23Lab/AudioLab/Forge `phys` into Resources/Sfx/_manifest_phys.txt. Read after the main manifest (whose
        /// entries win on a clash), so the main layer is preloaded first.</summary>
        static void LoadExtraManifest(string resourcePath)
        {
            TextAsset ta = null;
            try { ta = Resources.Load<TextAsset>(resourcePath); } catch (System.Exception) { }
            if (ta == null) return;
            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.Trim(); if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split('|'); if (p.Length < 7 || _sets.ContainsKey(p[0])) continue;
                var ss = new SampleSet { Id = p[0], Count = Mathf.Max(1, ParseI(p[1])), Vol = ParseF(p[2], 1f), Jitter = ParseF(p[4], 0.04f), VolJitterDb = ParseF(p[5], 1f), Loop = p[6] == "1" };
                if (!System.Enum.TryParse(p[3], out ss.Range)) ss.Range = SfxRange.Medium;
                ss.Clips = new AudioClip[ss.Count]; ss.Tried = new bool[ss.Count];
                var synth = SfxSynth.Get(ss.Id);
                ss.Info = new SfxInfo { Id = ss.Id, Variants = ss.Count, Loop = ss.Loop || (synth != null && synth.Loop), Volume = ss.Vol, Range = ss.Range, PitchJitter = ss.Jitter };
                _sets[ss.Id] = ss;
            }
        }
        // ---- END physical pack ----
        static int ParseI(string s) { int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v); return v; }
        static float ParseF(string s, float d) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;

        static readonly HashSet<string> _probed = new HashSet<string>();
        static SampleSet Set(string id)
        {
            if (id == null) return null;
            if (_sets == null) LoadManifest();
            if (_sets.TryGetValue(id, out var s)) return s;
            if (AudioFlags.NoSamples || !_probed.Add(id)) return null;
            // files added outside the manifest (other tools drop Resources/Sfx/<id>_0..n): discover them by name, once
            AudioClip c0 = null;
            try { c0 = Resources.Load<AudioClip>("Sfx/" + id + "_0"); } catch (System.Exception) { }
            if (c0 == null) return null;
            var clips = new List<AudioClip> { c0 };
            for (int i = 1; i < 32; i++)
            {
                AudioClip c = null; try { c = Resources.Load<AudioClip>("Sfx/" + id + "_" + i); } catch (System.Exception) { }
                if (c == null) break; clips.Add(c);
            }
            var synth = SfxSynth.Get(id);
            s = new SampleSet { Id = id, Count = clips.Count, Vol = 1f, Range = synth != null ? synth.Range : SfxRange.UI, Jitter = synth != null ? synth.PitchJitter : 0.02f, VolJitterDb = 0.5f, Loop = synth != null && synth.Loop };
            s.Clips = clips.ToArray(); s.Tried = new bool[s.Count]; for (int i = 0; i < s.Count; i++) s.Tried[i] = true;
            s.Info = new SfxInfo { Id = id, Variants = s.Count, Loop = s.Loop, Volume = s.Vol, Range = s.Range, PitchJitter = s.Jitter };
            _sets[id] = s;
            return s;
        }

        static AudioClip SampleClip(SampleSet s, int i)
        {
            i = ((i % s.Count) + s.Count) % s.Count;
            if (s.Clips[i] == null && !s.Tried[i])
            {
                s.Tried[i] = true;
                try { s.Clips[i] = Resources.Load<AudioClip>("Sfx/" + s.Id + "_" + i); } catch (System.Exception) { }
            }
            if (s.Clips[i] != null) return s.Clips[i];
            for (int k = 0; k < s.Count; k++) if (s.Clips[k] != null) return s.Clips[k]; // a missing file: any loaded sibling
            return null;
        }

        /// <summary>True if the id has recorded samples or a synth recipe.</summary>
        public static bool Has(string id) => Set(id) != null || SfxSynth.Get(id) != null;
        /// <summary>Mix gain of an id (sample manifest volume, else the recipe's volume).</summary>
        public static float VolumeOf(string id) { var s = Set(id); if (s != null) return s.Vol; var i = SfxSynth.Get(id); return i != null ? i.Volume : 1f; }
        static SfxInfo InfoOf(string id) { var s = Set(id); return s != null ? s.Info : SfxSynth.Get(id); }

        static IEnumerator GenerateAll()
        {
            // 1) recorded samples, most urgent first, a few async loads in flight
            if (_sets != null && _sets.Count > 0 && !AudioFlags.NoPreload)
            {
                var urgent = new[] { "ui_hover", "ui_confirm", "ui_cancel", "ui_page", "step_marble", "step_wood", "step_carpet", "door_open", "door_close", "chime", "bell_toll" };
                var order = new List<SampleSet>();
                foreach (var u in urgent) if (_sets.TryGetValue(u, out var s0)) order.Add(s0);
                foreach (var s1 in _sets.Values) if (!order.Contains(s1)) order.Add(s1);
                var inflight = new List<KeyValuePair<ResourceRequest, KeyValuePair<SampleSet, int>>>();
                foreach (var s in order)
                    for (int i = 0; i < s.Count; i++)
                    {
                        if (s.Clips[i] != null || s.Tried[i]) continue;
                        s.Tried[i] = true;
                        ResourceRequest rq = null;
                        try { rq = Resources.LoadAsync<AudioClip>("Sfx/" + s.Id + "_" + i); } catch (System.Exception) { }
                        if (rq != null) inflight.Add(new KeyValuePair<ResourceRequest, KeyValuePair<SampleSet, int>>(rq, new KeyValuePair<SampleSet, int>(s, i)));
                        while (inflight.Count >= 4)
                        {
                            yield return null;
                            for (int k = inflight.Count - 1; k >= 0; k--) if (inflight[k].Key.isDone) { var kv = inflight[k].Value; if (kv.Key.Clips[kv.Value] == null) kv.Key.Clips[kv.Value] = inflight[k].Key.asset as AudioClip; inflight.RemoveAt(k); }
                        }
                    }
                while (inflight.Count > 0)
                {
                    yield return null;
                    for (int k = inflight.Count - 1; k >= 0; k--) if (inflight[k].Key.isDone) { var kv = inflight[k].Value; if (kv.Key.Clips[kv.Value] == null) kv.Key.Clips[kv.Value] = inflight[k].Key.asset as AudioClip; inflight.RemoveAt(k); }
                }
            }

            // 2) synth recipes for ids without samples (pure C#: worker thread; AudioClips are created here on the main thread)
            var jobs = new List<KeyValuePair<string, int>>();
            foreach (var id in SfxSynth.Ids)
            {
                if (Set(id) != null) continue;
                var info = SfxSynth.Get(id); for (int v = 0; v < info.Variants; v++) jobs.Add(new KeyValuePair<string, int>(id, v));
            }
            if (Application.platform != RuntimePlatform.WebGLPlayer && jobs.Count > 0)
            {
                var done = new System.Collections.Concurrent.ConcurrentQueue<KeyValuePair<KeyValuePair<string, int>, float[]>>();
                var task = System.Threading.Tasks.Task.Run(() =>
                {
                    foreach (var j in jobs) done.Enqueue(new KeyValuePair<KeyValuePair<string, int>, float[]>(j, SfxSynth.Render(j.Key, j.Value, Rate)));
                });
                while (!task.IsCompleted || !done.IsEmpty)
                {
                    for (int n = 0; n < 12 && done.TryDequeue(out var r); n++) Store(r.Key.Key, r.Key.Value, r.Value);
                    yield return null;
                }
                if (task.IsFaulted && Debug.isDebugBuild) Debug.LogWarning("[BL23 Sfx] background synthesis failed, finishing on the main thread: " + task.Exception);
            }
            var sw = new System.Diagnostics.Stopwatch();
            foreach (var j in jobs)
            {
                sw.Restart();
                SynthClip(j.Key, j.Value);
                if (sw.Elapsed.TotalMilliseconds > 2.0) yield return null;
            }
            Ready = true;
        }

        static void Store(string id, int variant, float[] data)
        {
            var info = SfxSynth.Get(id); if (info == null || data == null || data.Length == 0) return;
            if (!_clips.TryGetValue(id, out var arr)) { arr = new AudioClip[info.Variants]; _clips[id] = arr; }
            if (arr[variant] != null) return; // already made on demand
            var clip = AudioClip.Create($"sfx_{id}_{variant}", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            arr[variant] = clip;
        }

        static AudioClip SynthClip(string id, int variant)
        {
            var info = SfxSynth.Get(id); if (info == null) return null;
            if (!_clips.TryGetValue(id, out var arr)) { arr = new AudioClip[info.Variants]; _clips[id] = arr; }
            int vi = ((variant % info.Variants) + info.Variants) % info.Variants;
            if (arr[vi] == null) Store(id, vi, SfxSynth.Render(id, vi, Rate));
            return arr[vi];
        }

        /// <summary>The clip for an id (recorded sample if there is one, else the synthesized recipe; variant wraps).
        /// Returns null for unknown ids (warned once).</summary>
        public static AudioClip GetClip(string id, int variant = 0)
        {
            var s = Set(id);
            if (s != null) { var c = SampleClip(s, variant); if (c != null) return c; }
            if (SfxSynth.Get(id) == null) { Warn(id); return null; }
            return SynthClip(id, variant);
        }

        static void Warn(string id)
        {
            if (id != null && _warned.Add(id)) Debug.LogWarning($"[BL23 Sfx] unknown sound id '{id}'");
        }

        static int PickVariant(string id, int count)
        {
            if (count <= 1) return 0;
            int v = _rng.R(count);
            _lastVariant.TryGetValue(id, out var last); _prevVariant.TryGetValue(id, out var prev);
            for (int tries = 0; tries < 4 && (v == last || (count >= 4 && v == prev)); tries++) v = (v + 1 + _rng.R(count - 1)) % count;
            _prevVariant[id] = last; _lastVariant[id] = v;
            return v;
        }

        public static void Play(string id, Vector3? pos = null, float vol = 1f) { PlayEx(id, pos, vol); }

        /// <param name="variant">-1 = random (no immediate repeats)</param>
        /// <param name="lowpassHz">0 = off; e.g. 900 for a sound heard through a wall</param>
        public static AudioSource PlayEx(string id, Vector3? pos = null, float vol = 1f, float pitch = 1f, int variant = -1, float lowpassHz = 0f)
        { AudioPerf.BeginSfx(); try { return PlayExImpl(id, pos, vol, pitch, variant, lowpassHz); } finally { AudioPerf.EndSfx(); } }

        static AudioSource PlayExImpl(string id, Vector3? pos, float vol, float pitch, int variant, float lowpassHz)
        {
            if (_runner == null) Init();
            var set = Set(id);
            SfxInfo info; AudioClip clip = null; float volJit = 0f;
            if (set != null)
            {
                info = set.Info;
                clip = SampleClip(set, variant >= 0 ? variant : PickVariant(id, set.Count));
                volJit = set.VolJitterDb;
            }
            else
            {
                info = SfxSynth.Get(id);
                if (info == null) { Warn(id); return null; }
                clip = SynthClip(id, variant >= 0 ? variant : PickVariant(id, info.Variants));
            }
            if (clip == null) return null;
            if (Busy(id) >= MaxVoices(id)) return null;           // e.g. a crowd of footsteps in one frame
            int si; var src = TakeSource(out si); if (src == null) return null;
            _poolId[si] = id;
            Configure(src, info, pos, lowpassHz);
            src.clip = clip; src.loop = false;
            src.pitch = pitch * (1f + _rng.Range(-info.PitchJitter, info.PitchJitter));
            float jit = volJit > 0f ? AudioVolumes.DbToLin(_rng.Range(-volJit, volJit)) : 1f;
            src.volume = Mathf.Clamp01(vol * info.Volume * jit * AudioVolumes.SfxGain);
            src.Play();
            return src;
        }

        public static SfxHandle Loop(string id, Vector3? pos = null, float vol = 1f, float fadeIn = 0.3f)
        {
            if (_runner == null) Init();
            var info = InfoOf(id); var clip = GetClip(id, 0);
            if (info == null || clip == null) return new SfxHandle { Stopping = true };
            var src = NewSource(_runner.transform, "SfxLoop_" + id);
            Configure(src, info, pos, 0f);
            src.clip = clip; src.loop = true; src.pitch = 1f; src.volume = 0f;
            src.time = _rng.F() * clip.length * 0.9f; // desynchronise identical loops
            src.Play();
            var h = new SfxHandle { Src = src, Info = info, BaseVol = vol, Fade = fadeIn > 0.01f ? 0f : 1f, FadeTarget = 1f, FadeRate = 1f / Mathf.Max(0.01f, fadeIn) };
            _loops.Add(h);
            return h;
        }

        public static void StopAllLoops(float fade = 0.3f) { foreach (var h in _loops) h.Stop(fade); }

        /// <summary>Footstep by floor material name ("Marble", "Wood", "Carpet", "Stone", "Water"/"Wet", ...).</summary>
        public static void Footstep(string surface, Vector3 pos, float vol = 1f) => Play(SurfaceToId(surface), pos, vol);

        public static string SurfaceToId(string surface)
        {
            if (string.IsNullOrEmpty(surface)) return "step_marble";
            string s = surface.ToLowerInvariant();
            if (s.Contains("water") || s.Contains("wet") || s.Contains("puddle") || s.Contains("bath") || s.Contains("pool") || s.Contains("blood")) return "step_water";
            if (s.Contains("carpet") || s.Contains("rug") || s.Contains("fabric") || s.Contains("cloth") || s.Contains("grass") || s.Contains("soil")) return "step_carpet";
            if (s.Contains("wood") || s.Contains("parquet") || s.Contains("plank") || s.Contains("board") || s.Contains("stage")) return "step_wood";
            if ((s.Contains("stone") || s.Contains("gravel") || s.Contains("cellar") || s.Contains("concrete")) && Has("step_stone")) return "step_stone";
            return "step_marble";
        }

        // ------------------------------------------------------------------ internals
        static int MaxVoices(string id)
        {
            if (id.StartsWith("step_")) return 6;
            if (id.StartsWith("amb_") || id.StartsWith("ui_")) return 3;
            if (id == "bell_toll" || id == "death_bell" || id == "morning_bell" || id == "chime") return 2;
            return 4;
        }
        static int Busy(string id)
        {
            int n = 0;
            for (int i = 0; i < _pool.Count; i++) if (_poolId[i] == id && _pool[i] != null && _pool[i].isPlaying) n++;
            return n;
        }

        static AudioSource TakeSource(out int index)
        {
            for (int k = 0; k < _pool.Count; k++)
            {
                int i = (_next + k) % _pool.Count; var s = _pool[i];
                if (s != null && !s.isPlaying) { _next = (i + 1) % _pool.Count; index = i; return s; }
            }
            // steal the one closest to finishing
            AudioSource best = null; float bestLeft = float.MaxValue; index = -1;
            for (int i = 0; i < _pool.Count; i++)
            {
                var s = _pool[i];
                if (s == null || s.clip == null) continue;
                float left = (s.clip.length - s.time) / Mathf.Max(0.1f, Mathf.Abs(s.pitch));
                if (left < bestLeft) { bestLeft = left; best = s; index = i; }
            }
            if (best != null) best.Stop();
            return best;
        }

        static void Configure(AudioSource s, SfxInfo info, Vector3? pos, float lowpassHz)
        {
            var lp = s.GetComponent<AudioLowPassFilter>();
            if (lp != null) { lp.enabled = lowpassHz > 0f; if (lowpassHz > 0f) lp.cutoffFrequency = lowpassHz; }
            if (pos.HasValue && info.Range != SfxRange.UI)
            {
                s.transform.position = pos.Value;
                s.spatialBlend = 1f;
                RangeOf(info.Range, out float min, out float max);
                s.minDistance = min; s.maxDistance = max;
                s.rolloffMode = AudioRolloffMode.Custom;
                s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Curve(info.Range, min, max));
                s.ignoreListenerPause = false;
                s.bypassReverbZones = false;   // positional sounds take the listener's room reverb
            }
            else
            {
                s.spatialBlend = 0f; s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = 1f; s.maxDistance = 500f;
                s.ignoreListenerPause = info.Range == SfxRange.UI; // UI sounds keep working in pause menus
                s.bypassReverbZones = true;
            }
        }

        static void RangeOf(SfxRange r, out float min, out float max)
        {
            switch (r)
            {
                case SfxRange.Near: min = 0.7f; max = 14f; break;
                case SfxRange.Medium: min = 1.2f; max = 24f; break;
                case SfxRange.Far: min = 2f; max = 45f; break;
                case SfxRange.Huge: min = 4f; max = 80f; break;
                default: min = 1f; max = 20f; break;
            }
        }

        /// <summary>Inverse-distance law from min to ~70% of max, then a smooth fade to silence at max.</summary>
        static AnimationCurve Curve(SfxRange r, float min, float max)
        {
            if (_curves.TryGetValue(r, out var c)) return c;
            float r0 = min / max; var keys = new List<Keyframe> { new Keyframe(0f, 1f), new Keyframe(r0, 1f) };
            for (int i = 1; i <= 8; i++)
            {
                float x = r0 + (1f - r0) * i / 8f;
                float y = r0 / x;
                if (x > 0.7f) y *= Mathf.SmoothStep(1f, 0f, (x - 0.7f) / 0.3f);
                keys.Add(new Keyframe(x, i == 8 ? 0f : y));
            }
            c = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            _curves[r] = c;
            return c;
        }

        internal static void UpdateLoops(float dt)
        {
            float bus = AudioVolumes.SfxGain;
            for (int i = _loops.Count - 1; i >= 0; i--)
            {
                var h = _loops[i];
                if (h.Src == null) { _loops.RemoveAt(i); continue; }
                h.Fade = Mathf.MoveTowards(h.Fade, h.FadeTarget, h.FadeRate * dt);
                if (h.FollowT != null) h.Src.transform.position = h.FollowT.position;
                h.Src.volume = Mathf.Clamp01(h.BaseVol * h.Info.Volume * bus * h.Fade);
                if (h.Stopping && h.Fade <= 0.001f)
                {
                    Object.Destroy(h.Src.gameObject); h.Src = null; _loops.RemoveAt(i);
                }
            }
        }
    }
}
