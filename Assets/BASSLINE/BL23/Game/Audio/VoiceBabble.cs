using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Audio
{
    /// <summary>
    /// Per-character "babble" while subtitles type out (Danganronpa / Animal Crossing style, kept subtle).
    /// Pitch, speed and timbre come from BL23.Sim.Cast (Gender, Speech.VoicePitch, Speech.VoiceRate).
    /// Two ways to use it:
    ///  1) <c>VoiceBabble.Speak("P03", line)</c> — schedules one blip per Hangul syllable on its own clock;
    ///     type the subtitle at <see cref="SyllablesPerSecond"/> (or use <see cref="EstimateDuration"/>).
    ///  2) typewriter-driven: call <c>VoiceBabble.Blip("P03", ch)</c> for every revealed character (rate-limited).
    /// </summary>
    public static class VoiceBabble
    {
        public static bool Enabled = true;
        /// <summary>Base level (multiplied by master*voice volume). Low on purpose: babble should sit under the music.</summary>
        public static float Volume = 0.3f;
        /// <summary>If true, Speak() ducks the music until the line has finished.</summary>
        public static bool AutoDuckMusic = false;

        static BabbleRunner _runner;
        static readonly Dictionary<string, AudioClip[]> _banks = new Dictionary<string, AudioClip[]>();
        static readonly Dictionary<string, float> _lastBlip = new Dictionary<string, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _runner = null; _banks.Clear(); _lastBlip.Clear(); }

        public static bool IsSpeaking => _runner != null && _runner.Active;
        public static string CurrentActor => _runner != null && _runner.Active ? _runner.Actor : null;

        public static void Speak(string actorId, string text) => Speak(actorId, text, 0f, 0f);

        /// <param name="pitch">&gt;0 overrides Speech.VoicePitch</param>
        /// <param name="rate">&gt;0 overrides Speech.VoiceRate</param>
        public static void Speak(string actorId, string text, float pitch, float rate)
        {
            if (!Enabled || string.IsNullOrEmpty(text)) { Stop(); return; }
            Ensure();
            var bank = Bank(actorId, pitch, out var voice);
            float r = rate > 0f ? rate : voice.Rate;
            var plan = BabbleSynth.Plan(text, r, (uint)(text.GetHashCode() ^ (actorId ?? "").GetHashCode()));
            _runner.Begin(actorId, bank, plan);
            if (AutoDuckMusic && MusicDirector.I != null) MusicDirector.I.Duck(true);
        }

        public static void Stop()
        {
            if (_runner != null && _runner.Active) { _runner.End(); }
        }

        /// <summary>Syllables per second this actor speaks at (use it as the subtitle typing speed for Hangul).</summary>
        public static float SyllablesPerSecond(string actorId)
        {
            var c = BL23.Sim.Cast.Get(actorId);
            float rate = c != null && c.Speech != null && c.Speech.VoiceRate > 0 ? c.Speech.VoiceRate : 1f;
            return BabbleSynth.BaseSyllablesPerSecond * rate;
        }

        public static float EstimateDuration(string actorId, string text)
        {
            var c = BL23.Sim.Cast.Get(actorId);
            float rate = c != null && c.Speech != null && c.Speech.VoiceRate > 0 ? c.Speech.VoiceRate : 1f;
            return BabbleSynth.Duration(BabbleSynth.Plan(text, rate, 1u));
        }

        /// <summary>Typewriter-driven mode: one call per revealed character. Blips are rate-limited so fast typing stays pleasant.</summary>
        public static void Blip(string actorId, char ch)
        {
            if (!Enabled) return;
            Ensure();
            var plan = BabbleSynth.Plan(ch.ToString(), 1f, (uint)(ch * 2654435761u));
            if (plan.Count == 0) return;
            var bank = Bank(actorId, 0f, out var voice);
            float now = Time.unscaledTime, minGap = 0.8f / (BabbleSynth.BaseSyllablesPerSecond * voice.Rate);
            string key = actorId ?? "";
            if (_lastBlip.TryGetValue(key, out var last) && now - last < minGap) return;
            _lastBlip[key] = now;
            _runner.PlayBlip(bank, plan[0]);
        }

        /// <summary>Synthesize an actor's blip bank ahead of time (e.g. while a scene loads).</summary>
        public static void Prewarm(string actorId) { Ensure(); Bank(actorId, 0f, out _); }

        static void Ensure()
        {
            if (_runner != null) return;
            var go = new GameObject("BL23_VoiceBabble");
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<BabbleRunner>();
        }

        static AudioClip[] Bank(string actorId, float pitchOverride, out BabbleVoice voice)
        {
            var c = BL23.Sim.Cast.Get(actorId);
            bool female = c != null && c.Gender == BL23.Sim.Gender.F;
            float vp = pitchOverride > 0f ? pitchOverride : (c != null && c.Speech != null ? c.Speech.VoicePitch : 1f);
            float vr = c != null && c.Speech != null ? c.Speech.VoiceRate : 1f;
            voice = BabbleSynth.VoiceFor(actorId ?? "?", female, vp, vr, c != null && c.IsButler);
            string key = (actorId ?? "?") + "|" + Mathf.RoundToInt(vp * 100f);
            if (_banks.TryGetValue(key, out var bank)) return bank;
            var data = BabbleSynth.RenderBank(voice);
            bank = new AudioClip[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                bank[i] = AudioClip.Create($"babble_{actorId}_{i}", data[i].Length, 1, BabbleSynth.Rate, false);
                bank[i].SetData(data[i], 0);
            }
            _banks[key] = bank;
            return bank;
        }
    }
}
