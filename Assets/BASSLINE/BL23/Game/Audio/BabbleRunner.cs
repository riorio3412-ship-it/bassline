using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Audio
{
    /// <summary>Hidden host for <see cref="VoiceBabble"/>: plays the scheduled blips round-robin on three 2D sources.</summary>
    [DisallowMultipleComponent]
    internal sealed class BabbleRunner : MonoBehaviour
    {
        AudioSource[] _src;
        int _next;
        List<BabbleEvent> _plan;
        AudioClip[] _bank;
        int _idx; float _t0;
        public bool Active; public string Actor;

        void Awake()
        {
            _src = new AudioSource[3];
            for (int i = 0; i < _src.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0f; s.priority = 32; s.dopplerLevel = 0f; s.bypassReverbZones = true;
                _src[i] = s;
            }
        }

        public void Begin(string actor, AudioClip[] bank, List<BabbleEvent> plan)
        {
            Actor = actor; _bank = bank; _plan = plan; _idx = 0; _t0 = Time.unscaledTime; Active = plan.Count > 0;
        }

        public void End()
        {
            Active = false; _plan = null;
            if (VoiceBabble.AutoDuckMusic && MusicDirector.I != null) MusicDirector.I.Duck(false);
        }

        public void PlayBlip(AudioClip[] bank, BabbleEvent e)
        {
            if (bank == null || e.Blip < 0 || e.Blip >= bank.Length || bank[e.Blip] == null) return;
            var s = _src[_next]; _next = (_next + 1) % _src.Length;
            s.clip = bank[e.Blip]; s.pitch = Mathf.Clamp(e.Pitch, 0.5f, 2f);
            s.volume = Mathf.Clamp01(VoiceBabble.Volume * AudioVolumes.VoiceGain * e.Gain);
            s.Play();
        }

        void Update()
        {
            if (!Active || _plan == null) return;
            float el = Time.unscaledTime - _t0;
            while (_idx < _plan.Count && _plan[_idx].Time <= el) { PlayBlip(_bank, _plan[_idx]); _idx++; }
            if (_idx >= _plan.Count && el > BabbleSynth.Duration(_plan)) End();
        }
    }
}
