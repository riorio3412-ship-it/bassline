using System.Diagnostics;

namespace BL23.Game.Audio
{
    /// <summary>
    /// Main-thread cost of the audio scripts (music director, ambience director, Sfx.PlayEx), logged once a minute for the
    /// first few minutes: "[BL23 AudioPerf] ... ms/frame". Lets probes separate script cost from machine noise.
    /// </summary>
    public static class AudioPerf
    {
        static readonly Stopwatch _music = new Stopwatch(), _amb = new Stopwatch(), _sfx = new Stopwatch();
        static int _frames, _logs; static float _nextLog = 60f; static int _lastFrame = -1;

        public static void BeginMusic() => _music.Start();
        public static void EndMusic() => _music.Stop();
        public static void BeginAmb() => _amb.Start();
        public static void EndAmb() { _amb.Stop(); Tick(); }
        public static void BeginSfx() => _sfx.Start();
        public static void EndSfx() => _sfx.Stop();

        static void Tick()
        {
            int f = UnityEngine.Time.frameCount; if (f != _lastFrame) { _lastFrame = f; _frames++; }
            float now = UnityEngine.Time.unscaledTime;
            if (now < _nextLog || _logs >= 6 || AudioFlags.Quiet) return;
            _nextLog = now + 60f; _logs++;
            double k = 1000.0 / Stopwatch.Frequency / System.Math.Max(1, _frames);
            UnityEngine.Debug.Log($"[BL23 AudioPerf] {_frames} frames: music {_music.ElapsedTicks * k:0.000} ms/frame, ambience {_amb.ElapsedTicks * k:0.000}, sfx {_sfx.ElapsedTicks * k:0.000}");
            _music.Reset(); _amb.Reset(); _sfx.Reset(); _frames = 0;
        }
    }
}
