using System;
using UnityEngine;

namespace BL23.Game.Audio
{
    /// <summary>Player volume settings shared by MusicDirector, Sfx and VoiceBabble. Persisted in PlayerPrefs.</summary>
    public static class AudioVolumes
    {
        const string KMaster = "BL23.Vol.Master", KMusic = "BL23.Vol.Music", KSfx = "BL23.Vol.Sfx", KVoice = "BL23.Vol.Voice";
        static bool _loaded;
        static float _master = 1f, _music = 0.8f, _sfx = 1f, _voice = 0.9f;

        public static event Action Changed;

        public static float Master { get { Load(); return _master; } }
        public static float Music { get { Load(); return _music; } }
        public static float SfxVolume { get { Load(); return _sfx; } }
        public static float Voice { get { Load(); return _voice; } }

        /// <summary>Effective linear gains (master already multiplied in).</summary>
        public static float MusicGain => Master * Music;
        public static float SfxGain => Master * SfxVolume;
        public static float VoiceGain => Master * Voice;

        public static void Set(float master, float music, float sfx)
        {
            Load();
            _master = Mathf.Clamp01(master); _music = Mathf.Clamp01(music); _sfx = Mathf.Clamp01(sfx);
            Save(); Changed?.Invoke();
        }
        public static void SetVoice(float voice) { Load(); _voice = Mathf.Clamp01(voice); Save(); Changed?.Invoke(); }

        public static void Load()
        {
            if (_loaded) return; _loaded = true;
            try
            {
                _master = PlayerPrefs.GetFloat(KMaster, _master); _music = PlayerPrefs.GetFloat(KMusic, _music);
                _sfx = PlayerPrefs.GetFloat(KSfx, _sfx); _voice = PlayerPrefs.GetFloat(KVoice, _voice);
            }
            catch (Exception) { /* PlayerPrefs unavailable (e.g. called off the main thread) -> defaults */ }
        }
        static void Save()
        {
            try
            {
                PlayerPrefs.SetFloat(KMaster, _master); PlayerPrefs.SetFloat(KMusic, _music);
                PlayerPrefs.SetFloat(KSfx, _sfx); PlayerPrefs.SetFloat(KVoice, _voice);
            }
            catch (Exception) { }
        }

        public static float DbToLin(float db) => Mathf.Pow(10f, db / 20f);
    }
}
