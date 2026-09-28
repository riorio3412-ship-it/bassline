using System;

namespace BL23.Game.Audio
{
    /// <summary>
    /// Command-line switches for audio A/B checks (performance probes, debugging):
    /// -bl23noamb (no room ambience / reverb), -bl23nosamples (synth recipes only), -bl23nopreload (load samples on first use),
    /// -bl23audioquiet (no ambience change log).
    /// </summary>
    public static class AudioFlags
    {
        static bool _read, _noAmb, _noSamples, _noPreload, _quiet;
        public static bool NoAmbience { get { Read(); return _noAmb; } }
        public static bool NoSamples { get { Read(); return _noSamples; } }
        public static bool NoPreload { get { Read(); return _noPreload; } }
        public static bool Quiet { get { Read(); return _quiet; } }

        static void Read()
        {
            if (_read) return; _read = true;
            try
            {
                foreach (var a in Environment.GetCommandLineArgs())
                {
                    var s = a.ToLowerInvariant();
                    if (s == "-bl23noamb") _noAmb = true;
                    else if (s == "-bl23nosamples") _noSamples = true;
                    else if (s == "-bl23nopreload") _noPreload = true;
                    else if (s == "-bl23audioquiet") _quiet = true;
                }
            }
            catch (Exception) { }
        }
    }
}
