using System;
using System.Collections.Generic;

namespace BL23.Game.Audio
{
    /// <summary>How far a positional sound carries (maps to AudioSource min/max distance in <see cref="Sfx"/>).</summary>
    public enum SfxRange { UI, Near, Medium, Far, Huge }

    public sealed class SfxInfo
    {
        public string Id;
        public int Variants = 1;
        public bool Loop;
        public float Volume = 1f;         // default mix gain
        public SfxRange Range = SfxRange.Medium;
        public float PitchJitter = 0.04f; // random +- pitch per play (fraction)
        internal Func<SfxGen, float[]> Render;
    }

    /// <summary>Generator context: sample rate, deterministic RNG and helpers shared by all recipes.</summary>
    public sealed class SfxGen
    {
        public readonly int Fs; public SynthRng R; public readonly int Variant;
        public SfxGen(int fs, SynthRng r, int variant) { Fs = fs; R = r; Variant = variant; }
        public float[] New(float seconds) => new float[SynthDsp.Len(seconds, Fs)];
        public int S(float seconds) => (int)(seconds * Fs);

        /// <summary>Damped sinusoid added directly (cheap modal partial).</summary>
        public void Partial(float[] o, float at, float freq, float t60, float amp, float phase = 0f)
        {
            if (freq <= 0 || freq >= Fs * 0.47f || amp == 0) return;
            int s0 = S(at); int n = Math.Min(o.Length - s0, S(Math.Min(t60 * 1.1f, 8f)) + 1);
            double w = SynthDsp.TwoPi * freq / Fs, k = -6.907755 / (t60 * Fs);
            double c = Math.Cos(w), s = Math.Sin(w), re = Math.Cos(phase), im = Math.Sin(phase), dec = Math.Exp(k), g = 1;
            for (int i = 0; i < n; i++)
            {
                int j = s0 + i; if (j >= 0) o[j] += (float)(amp * im * g);
                double nr = re * c - im * s; im = re * s + im * c; re = nr; g *= dec;
            }
        }

        /// <summary>Sine with exponential pitch glide from f0 to f1 (time constant glideTau) and AD envelope.</summary>
        public void Thump(float[] o, float at, float f0, float f1, float glideTau, float attack, float decay, float amp)
        {
            int s0 = S(at); float ph = 0; int n = Math.Min(o.Length - s0, S(attack + decay * 7f));
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Fs; float f = f1 + (f0 - f1) * (float)Math.Exp(-t / Math.Max(1e-4f, glideTau));
                ph += SynthDsp.TwoPi * f / Fs; if (ph > SynthDsp.TwoPi) ph -= SynthDsp.TwoPi;
                int j = s0 + i; if (j >= 0) o[j] += amp * (float)Math.Sin(ph) * SynthDsp.AD(t, attack, decay);
            }
        }

        /// <summary>Filtered noise burst. mode: 0 LP, 1 BP(normalised), 2 HP.</summary>
        public void Noise(float[] o, float at, float attack, float decay, float amp, int mode, float fc, float q = 0.7f, float maxLen = 2f)
        {
            int s0 = S(at); int n = Math.Min(o.Length - s0, S(Math.Min(maxLen, attack + decay * 7f)));
            var f = new Svf(Fs, fc, q);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Fs; f.Process(R.N());
                float v = mode == 0 ? f.Low : mode == 1 ? f.Band / Math.Max(0.05f, q) : f.High; // Band/Q = unity-peak band-pass
                int j = s0 + i; if (j >= 0) o[j] += amp * v * SynthDsp.AD(t, attack, decay);
            }
        }

        /// <summary>Impulse excitation of a bank of modes (freq, t60, gain) — wood/metal/ceramic strikes.</summary>
        public void Strike(float[] o, float at, float amp, float[] freqs, float[] t60s, float[] gains, float noiseMs = 1.5f, float noiseLp = 5000f)
        {
            for (int k = 0; k < freqs.Length; k++) Partial(o, at, freqs[k], t60s[k], amp * gains[k], R.F() * 0.5f);
            if (noiseMs > 0) Noise(o, at, 0.0003f, noiseMs * 0.001f, amp * 0.5f, 0, noiseLp);
        }

        public float[] Reverb(float[] x, float mix, float size = 1f, float feedback = 0.78f, float damp = 0.35f)
        {
            var rv = new MiniReverb(Fs, size, feedback, damp);
            for (int i = 0; i < x.Length; i++) x[i] = x[i] * (1 - mix * 0.5f) + rv.Process(x[i]) * mix;
            return x;
        }
        public float[] LowPass(float[] x, float fc, float q = 0.707f) { var f = new Svf(Fs, fc, q); for (int i = 0; i < x.Length; i++) x[i] = f.LP(x[i]); return x; }
        public float[] HighPass(float[] x, float fc, float q = 0.707f) { var f = new Svf(Fs, fc, q); for (int i = 0; i < x.Length; i++) x[i] = f.HP(x[i]); return x; }
        public float[] Saturate(float[] x, float drive) { for (int i = 0; i < x.Length; i++) x[i] = SynthDsp.Sat(x[i] * drive); return x; }
    }

    /// <summary>
    /// Procedural sound-effect recipes. Everything is synthesised from noise, modal resonators and oscillators —
    /// no sample files. Pure C# (no UnityEngine) so it can be unit-tested; <see cref="Sfx"/> turns the buffers into AudioClips.
    /// </summary>
    public static class SfxSynth
    {
        public const int DefaultRate = 44100;
        static readonly Dictionary<string, SfxInfo> _db = new Dictionary<string, SfxInfo>();
        static readonly List<string> _ids = new List<string>();
        public static IReadOnlyList<string> Ids { get { Build(); return _ids; } }
        public static SfxInfo Get(string id) { Build(); return id != null && _db.TryGetValue(id, out var d) ? d : null; }

        /// <summary>Render one variant of a sound (mono float PCM, peak ≤ 1).</summary>
        public static float[] Render(string id, int variant, int fs = DefaultRate)
        {
            var d = Get(id); if (d == null) return null;
            var g = new SfxGen(fs, SynthRng.FromString(id, variant * 7919 + 17), variant % Math.Max(1, d.Variants));
            var x = d.Render(g);
            for (int i = 0; i < x.Length; i++) { float v = x[i]; if (float.IsNaN(v) || float.IsInfinity(v)) x[i] = 0; else if (v > 1f) x[i] = 1f; else if (v < -1f) x[i] = -1f; }
            return x;
        }

        static void Add(string id, int variants, SfxRange range, float vol, Func<SfxGen, float[]> fn, bool loop = false, float jitter = 0.04f)
        {
            var d = new SfxInfo { Id = id, Variants = variants, Range = range, Volume = vol, Render = fn, Loop = loop, PitchJitter = jitter };
            _db[id] = d; _ids.Add(id);
        }

        static bool _built;
        static void Build()
        {
            if (_built) return; _built = true;
            // footsteps
            Add("step_marble", 6, SfxRange.Near, 0.55f, StepMarble);
            Add("step_wood", 6, SfxRange.Near, 0.6f, StepWood);
            Add("step_carpet", 6, SfxRange.Near, 0.45f, StepCarpet);
            Add("step_water", 6, SfxRange.Near, 0.55f, StepWater);
            // doors
            Add("door_open", 2, SfxRange.Medium, 0.9f, DoorOpen);
            Add("door_close", 2, SfxRange.Medium, 0.8f, DoorClose);
            Add("door_lock", 2, SfxRange.Medium, 0.6f, g => LockClicks(g, false));
            Add("door_unlock", 2, SfxRange.Medium, 0.6f, g => LockClicks(g, true));
            Add("door_knock", 2, SfxRange.Medium, 0.8f, DoorKnock);
            Add("door_rattle", 2, SfxRange.Medium, 0.7f, DoorRattle);
            // breakage / impacts
            Add("glass_shatter", 3, SfxRange.Far, 0.85f, GlassShatter);
            Add("ceramic_break", 3, SfxRange.Far, 0.8f, CeramicBreak);
            Add("wood_crack", 2, SfxRange.Medium, 0.8f, WoodCrack);
            Add("metal_clang", 3, SfxRange.Far, 0.75f, MetalClang);
            Add("metal_dent", 2, SfxRange.Medium, 0.75f, MetalDent);
            Add("body_fall", 2, SfxRange.Medium, 0.9f, BodyFall);
            Add("blade_whoosh", 3, SfxRange.Near, 0.6f, BladeWhoosh);
            Add("stab", 2, SfxRange.Medium, 0.85f, Stab);
            Add("slash", 2, SfxRange.Medium, 0.8f, Slash);
            Add("blunt_hit", 3, SfxRange.Medium, 0.9f, BluntHit);
            // voice-ish
            Add("scream_muffled", 3, SfxRange.Far, 0.8f, Scream, jitter: 0.06f);
            Add("gasp", 2, SfxRange.Near, 0.6f, Gasp, jitter: 0.05f);
            Add("heartbeat", 1, SfxRange.UI, 0.8f, g => Heartbeat(g, 0.8f), jitter: 0f);
            Add("heartbeat_loop", 1, SfxRange.UI, 0.8f, g => Heartbeat(g, 60f / 72f), loop: true, jitter: 0f);
            // trial / announcements / stingers (2D)
            Add("chime", 1, SfxRange.UI, 0.8f, Chime, jitter: 0f);
            Add("gavel", 2, SfxRange.UI, 0.9f, Gavel, jitter: 0.02f);
            Add("break", 1, SfxRange.UI, 0.95f, StingerBreak, jitter: 0f);
            Add("objection", 1, SfxRange.UI, 0.9f, StingerObjection, jitter: 0f);
            Add("discovery", 1, SfxRange.UI, 0.9f, StingerDiscovery, jitter: 0f);
            Add("verdict", 1, SfxRange.UI, 0.9f, StingerVerdict, jitter: 0f);
            Add("evidence", 1, SfxRange.UI, 0.7f, StingerEvidence, jitter: 0f);
            Add("loop_reset", 1, SfxRange.UI, 0.9f, StingerLoopReset, jitter: 0f);
            Add("chapter_clear", 1, SfxRange.UI, 0.8f, StingerChapterClear, jitter: 0f);
            // the trial (심판): small mechanical cues
            Add("trial_fire", 2, SfxRange.UI, 0.8f, TrialFire, jitter: 0.03f);
            Add("trial_lock", 1, SfxRange.UI, 0.6f, TrialLock, jitter: 0f);
            Add("trial_tick", 1, SfxRange.UI, 0.45f, TrialTick, jitter: 0.02f);
            Add("trial_wrong", 1, SfxRange.UI, 0.8f, TrialWrong, jitter: 0f);
            Add("trial_impact", 1, SfxRange.UI, 1f, TrialImpact, jitter: 0f);
            Add("trial_coin", 3, SfxRange.UI, 0.5f, TrialCoin, jitter: 0.05f);
            // the trial's gothic palette: bell, candles, quill, parchment, wax stamp, choir and organ, the stone hall
            Add("bell_toll", 2, SfxRange.UI, 0.85f, BellToll, jitter: 0f);
            Add("candle_snuff", 2, SfxRange.UI, 0.55f, CandleSnuff, jitter: 0.05f);
            Add("candle_flare", 1, SfxRange.UI, 0.6f, CandleFlare, jitter: 0.02f);
            Add("quill", 3, SfxRange.UI, 0.45f, Quill, jitter: 0.06f);
            Add("parchment", 2, SfxRange.UI, 0.5f, Parchment, jitter: 0.05f);
            Add("wax_stamp", 2, SfxRange.UI, 0.85f, WaxStamp, jitter: 0.03f);
            Add("choir_swell", 1, SfxRange.UI, 0.75f, ChoirSwell, jitter: 0f);
            Add("organ_sting", 1, SfxRange.UI, 0.7f, OrganSting, jitter: 0f);
            Add("plate_hover", 2, SfxRange.UI, 0.3f, PlateHover, jitter: 0.04f);
            Add("hourglass", 1, SfxRange.UI, 0.55f, Hourglass, jitter: 0f);
            Add("court_ambience", 1, SfxRange.UI, 0.35f, CourtAmbience, loop: true, jitter: 0f);
            // UI
            Add("ui_hover", 1, SfxRange.UI, 0.35f, UiHover, jitter: 0.02f);
            Add("ui_confirm", 1, SfxRange.UI, 0.5f, g => UiTwoTone(g, 880f, 1320f), jitter: 0f);
            Add("ui_cancel", 1, SfxRange.UI, 0.45f, g => UiTwoTone(g, 660f, 440f), jitter: 0f);
            Add("ui_page", 2, SfxRange.UI, 0.45f, UiPage, jitter: 0.05f);
            // environment
            Add("clock_tick", 2, SfxRange.Near, 0.45f, ClockTick, jitter: 0.01f);
            Add("clock_loop", 1, SfxRange.Near, 0.45f, ClockLoop, loop: true, jitter: 0f);
            Add("rain_loop", 1, SfxRange.Far, 0.5f, RainLoop, loop: true, jitter: 0f);
            Add("water_splash", 2, SfxRange.Medium, 0.75f, WaterSplash);
            Add("hydraulic_press", 1, SfxRange.Huge, 1f, HydraulicPress, jitter: 0f);
            Add("breaker", 2, SfxRange.Far, 0.8f, Breaker, jitter: 0.02f);
            Add("light_switch", 2, SfxRange.Near, 0.4f, LightSwitch);
            Add("fluorescent_buzz", 1, SfxRange.Near, 0.35f, FluorescentBuzz, loop: true, jitter: 0f);
            Add("static_noise", 1, SfxRange.Medium, 0.4f, StaticNoise, loop: true, jitter: 0f);
            Add("elevator_rumble", 1, SfxRange.Far, 0.6f, ElevatorRumble, loop: true, jitter: 0f);
            Add("crowd_murmur", 1, SfxRange.Far, 0.5f, CrowdMurmur, loop: true, jitter: 0f);
        }

        // ------------------------------------------------------------------ footsteps
        static void HardTap(SfxGen g, float[] o, float at, float amp, float f1, float f2)
        {
            g.Noise(o, at, 0.0002f, 0.0018f, amp * 0.9f, 2, 1500f);
            g.Partial(o, at, f1, g.R.Range(0.035f, 0.07f), amp * 0.35f, g.R.F());
            g.Partial(o, at, f2, g.R.Range(0.025f, 0.05f), amp * 0.22f, g.R.F());
            g.Partial(o, at, f1 * 1.83f, 0.02f, amp * 0.1f);
        }
        static float[] StepMarble(SfxGen g)
        {
            var o = g.New(0.3f); float heel = 0.003f, toe = g.R.Range(0.045f, 0.075f);
            HardTap(g, o, heel, 1f, g.R.Range(2500f, 3500f), g.R.Range(4200f, 5600f));
            HardTap(g, o, toe, g.R.Range(0.35f, 0.55f), g.R.Range(3000f, 4200f), g.R.Range(5000f, 6500f));
            g.Thump(o, heel, g.R.Range(120f, 150f), g.R.Range(90f, 110f), 0.02f, 0.001f, 0.022f, 0.35f);
            g.Noise(o, toe, 0.01f, 0.03f, 0.05f, 0, 1800f);
            return g.Reverb(SynthDsp.Finish(o, 0.85f, 0.2f, 20f, g.Fs), 0.08f, 0.6f, 0.6f);
        }
        static float[] StepWood(SfxGen g)
        {
            var o = g.New(0.38f); float toe = g.R.Range(0.05f, 0.08f);
            float[] f = { g.R.Range(165f, 210f), g.R.Range(370f, 460f), g.R.Range(690f, 850f), g.R.Range(1250f, 1600f) };
            float[] t = { 0.12f, 0.09f, 0.07f, 0.05f }; float[] gn = { 0.7f, 0.45f, 0.3f, 0.18f };
            g.Thump(o, 0.002f, g.R.Range(95f, 120f), g.R.Range(70f, 90f), 0.03f, 0.0015f, 0.045f, 0.7f);
            g.Strike(o, 0.002f, 1f, f, t, gn, 2f, 3000f);
            g.Strike(o, toe, 0.5f, f, t, gn, 1.5f, 3500f);
            if (g.Variant % 3 == 0) // floorboard creak
            {
                int s0 = g.S(0.08f), n = g.S(0.16f); var b1 = new Svf(g.Fs, g.R.Range(800f, 1000f), 12f); var b2 = new Svf(g.Fs, g.R.Range(1400f, 1700f), 14f);
                float ph = 0; for (int i = 0; i < n && s0 + i < o.Length; i++)
                {
                    float u = (float)i / n; float rate = 55f - 22f * u; ph += rate / g.Fs; float x = 0; if (ph >= 1) { ph -= 1; x = g.R.Range(0.6f, 1f); }
                    b1.Process(x); b2.Process(x); o[s0 + i] += 0.5f * (b1.Band * 0.08f + b2.Band * 0.06f) * (float)Math.Sin(Math.PI * u);
                }
            }
            return SynthDsp.Finish(o, 0.9f, 0.2f, 20f, g.Fs);
        }
        static float[] StepCarpet(SfxGen g)
        {
            var o = g.New(0.26f); float toe = g.R.Range(0.05f, 0.075f);
            g.Noise(o, 0, 0.006f, 0.045f, 0.9f, 0, g.R.Range(550f, 850f));
            g.Thump(o, 0.002f, 80f, 62f, 0.03f, 0.004f, 0.04f, 0.45f);
            g.Noise(o, toe, 0.006f, 0.035f, 0.45f, 0, g.R.Range(650f, 950f));
            g.Noise(o, 0.01f, 0.01f, 0.05f, 0.07f, 1, 2600f, 0.8f);
            return SynthDsp.Finish(o, 0.6f, 1f, 20f, g.Fs);
        }
        static void Bubble(SfxGen g, float[] o, float at, float f0, float rise, float dur, float amp)
        {
            int s0 = g.S(at), n = Math.Min(o.Length - s0, g.S(dur)); float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n; float f = f0 * (1 + rise * u); ph += SynthDsp.TwoPi * f / g.Fs;
                o[s0 + i] += amp * (float)Math.Sin(ph) * SynthDsp.AD(u * dur, 0.001f, dur * 0.3f);
            }
        }
        static float[] StepWater(SfxGen g)
        {
            var o = g.New(0.38f);
            g.Noise(o, 0, 0.004f, 0.06f, 0.8f, 1, g.R.Range(1400f, 2500f), 0.8f);
            g.Noise(o, 0, 0.002f, 0.03f, 0.3f, 2, 4000f);
            g.Thump(o, 0, 120f, 90f, 0.02f, 0.001f, 0.03f, 0.35f);
            int nb = 3 + g.R.R(4);
            for (int b = 0; b < nb; b++) Bubble(g, o, g.R.Range(0.01f, 0.13f), g.R.Range(500f, 1400f), g.R.Range(0.2f, 0.4f), g.R.Range(0.02f, 0.05f), g.R.Range(0.1f, 0.28f));
            return SynthDsp.Finish(o, 0.85f, 0.3f, 20f, g.Fs);
        }

        // ------------------------------------------------------------------ doors
        static void Creak(SfxGen g, float[] o, float at, float dur, float baseRate, float amp, float pitchMul)
        {
            int s0 = g.S(at), n = Math.Min(o.Length - s0, g.S(dur));
            var r1 = new Svf(g.Fs, g.R.Range(550f, 700f) * pitchMul, 9f); var r2 = new Svf(g.Fs, g.R.Range(1000f, 1300f) * pitchMul, 11f); var r3 = new Svf(g.Fs, g.R.Range(2000f, 2600f) * pitchMul, 13f);
            float ph = 0, jit = 1f, burstAmp = 0f; int burst = 0, burstLen = Math.Max(1, g.S(0.0015f));
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n; float rate = baseRate * (0.6f + 0.9f * (float)Math.Sin(Math.PI * Math.Min(1, u * 1.15f))) * jit;
                ph += rate / g.Fs;
                if (ph >= 1) { ph -= 1; burst = burstLen; burstAmp = g.R.Range(0.5f, 1f); jit = g.R.Range(0.85f, 1.15f); }
                // each stick-slip event is a short noise burst (softer than a single-sample click) over a faint friction hiss
                float x = 0.04f * g.R.N();
                if (burst > 0) { x += burstAmp * g.R.N() * burst / burstLen * 0.6f; burst--; }
                r1.Process(x); r2.Process(x); r3.Process(x);
                float env = (float)Math.Pow(Math.Sin(Math.PI * u), 0.7);
                o[s0 + i] += amp * env * (r1.Band * 0.11f + r2.Band * 0.09f + r3.Band * 0.05f);
            }
        }
        static void Latch(SfxGen g, float[] o, float at, float amp, float pitch)
        {
            g.Strike(o, at, amp, new[] { 2200f * pitch, 3500f * pitch, 5100f * pitch }, new[] { 0.05f, 0.04f, 0.03f }, new[] { 0.5f, 0.35f, 0.2f }, 1f, 6000f);
        }
        static float[] DoorOpen(SfxGen g)
        {
            var o = g.New(1.15f); float pm = g.Variant == 0 ? 1f : 1.35f;
            Latch(g, o, 0.02f, 0.3f, g.R.Range(0.9f, 1.1f));
            Creak(g, o, 0.12f, g.R.Range(0.75f, 0.9f), g.Variant == 0 ? 38f : 55f, 2.6f, pm);
            g.Noise(o, 0.15f, 0.3f, 0.25f, 0.06f, 0, 280f);
            return SynthDsp.Finish(o, 0.8f, 0.3f, 40f, g.Fs);
        }
        static float[] DoorClose(SfxGen g)
        {
            var o = g.New(1.0f); float slam = 0.22f;
            Creak(g, o, 0.0f, 0.2f, 50f, 0.5f, 1.1f);
            g.Thump(o, slam, 66f, 46f, 0.06f, 0.002f, 0.12f, 1f);
            g.Noise(o, slam, 0.0005f, 0.015f, 0.6f, 0, 2500f);
            g.Strike(o, slam, 1.1f, new[] { g.R.Range(110f, 130f), g.R.Range(220f, 250f), g.R.Range(370f, 410f), g.R.Range(590f, 650f) }, new[] { 0.25f, 0.2f, 0.15f, 0.1f }, new[] { 1f, 0.7f, 0.45f, 0.3f }, 0f);
            g.Noise(o, slam, 0.001f, 0.03f, 0.35f, 1, 750f, 0.9f);
            Latch(g, o, slam + 0.03f, 0.35f, 0.85f);
            var x = SynthDsp.Finish(o, 0.9f, 0.3f, 60f, g.Fs);
            return SynthDsp.Finish(g.Reverb(x, 0.18f, 0.9f), 0.9f, 0.3f, 60f, g.Fs);
        }
        static float[] LockClicks(SfxGen g, bool unlock)
        {
            var o = g.New(0.4f); float p = g.Variant == 0 ? 1f : 1.15f;
            float a = unlock ? 0.0f : 0.11f, b = unlock ? 0.12f : 0.0f;
            // light click (key / cylinder)
            g.Strike(o, b, 0.45f, new[] { 1800f * p, 3100f * p, 4700f * p }, new[] { 0.03f, 0.025f, 0.02f }, new[] { 0.5f, 0.35f, 0.25f }, 0.8f, 7000f);
            // bolt slide
            g.Noise(o, Math.Min(a, b) + 0.02f, 0.02f, 0.03f, 0.12f, 1, 2000f * p, 2f);
            // bolt clack
            g.Strike(o, a, 1f, new[] { 900f * p, 1600f * p, 2900f * p }, new[] { 0.06f, 0.05f, 0.03f }, new[] { 0.6f, 0.4f, 0.25f }, 1.5f, 5000f);
            g.Thump(o, a, 200f, 170f, 0.01f, 0.0005f, 0.02f, 0.3f);
            return SynthDsp.Finish(o, 0.8f, 0.2f, 20f, g.Fs);
        }
        static float[] DoorKnock(SfxGen g)
        {
            var o = g.New(0.95f); bool pound = g.Variant == 1;
            float[] times = pound ? new[] { 0f, 0.28f + g.R.Range(-0.02f, 0.02f) } : new[] { 0f, 0.19f + g.R.Range(-0.015f, 0.015f), 0.37f + g.R.Range(-0.02f, 0.02f) };
            for (int k = 0; k < times.Length; k++)
            {
                float amp = (k == 1 ? 0.88f : 1f) * g.R.Range(0.9f, 1f); float fm = pound ? 0.8f : 1f;
                g.Strike(o, times[k], amp, new[] { g.R.Range(180f, 210f) * fm, g.R.Range(420f, 480f) * fm, g.R.Range(850f, 950f) * fm, g.R.Range(1450f, 1600f) * fm },
                    new[] { 0.12f, 0.08f, 0.06f, 0.04f }, new[] { 1f, 0.6f, 0.35f, 0.2f }, pound ? 3f : 1.5f, pound ? 2500f : 4000f);
                g.Thump(o, times[k], 105f * fm, 90f * fm, 0.02f, 0.001f, pound ? 0.08f : 0.05f, pound ? 0.8f : 0.4f);
            }
            var x = SynthDsp.Finish(o, 0.9f, 0.2f, 60f, g.Fs);
            return SynthDsp.Finish(g.Reverb(x, 0.15f, 1f), 0.9f, 0.2f, 60f, g.Fs);
        }
        static float[] DoorRattle(SfxGen g)
        {
            var o = g.New(0.8f); float t = 0; int n = 7 + g.R.R(4);
            for (int k = 0; k < n && t < 0.62f; k++)
            {
                float a = g.R.Range(0.4f, 1f);
                g.Strike(o, t, a * 0.7f, new[] { g.R.Range(1100f, 1400f), g.R.Range(2500f, 3100f) }, new[] { 0.03f, 0.025f }, new[] { 0.6f, 0.4f }, 0.6f, 6000f);
                g.Strike(o, t + 0.004f, a * 0.35f, new[] { g.R.Range(140f, 170f), g.R.Range(300f, 340f) }, new[] { 0.06f, 0.05f }, new[] { 1f, 0.6f }, 0f);
                t += g.R.Range(0.04f, 0.09f);
            }
            Latch(g, o, t, 0.7f, 0.8f);
            return SynthDsp.Finish(o, 0.8f, 0.2f, 30f, g.Fs);
        }

        // ------------------------------------------------------------------ breakage
        static float[] GlassShatter(SfxGen g) => GlassCore(g, 1.5f, 70 + g.Variant * 20, 1f);
        static float[] GlassCore(SfxGen g, float len, int shards, float bright)
        {
            var o = g.New(len);
            g.Noise(o, 0, 0.0003f, 0.005f, 1f, 2, 2500f);
            g.Thump(o, 0, 200f, 170f, 0.02f, 0.001f, 0.03f, 0.3f);
            for (int k = 0; k < shards; k++)
            {
                float t0 = 0.002f + Math.Min(len * 0.6f, g.R.Exp(0.09f));
                float f = 2000f * (float)Math.Pow(4.5, g.R.F()) * bright;
                float t60 = g.R.Range(0.03f, 0.18f) * (float)Math.Pow(4000.0 / f, 0.3);
                float amp = g.R.Range(0.05f, 0.35f) * (float)Math.Exp(-t0 * 2.5f);
                g.Partial(o, t0, f, t60, amp, g.R.F() * 6f);
                g.Partial(o, t0, f * g.R.Range(1.5f, 2.3f), t60 * 0.7f, amp * 0.5f, g.R.F() * 6f);
            }
            for (int k = 0; k < 12; k++) g.Partial(o, g.R.Range(0.35f, len - 0.2f), g.R.Range(3000f, 8000f), g.R.Range(0.02f, 0.08f), g.R.Range(0.03f, 0.1f));
            g.Noise(o, 0, 0.001f, 0.2f, 0.15f, 2, 3000f, 0.7f, len);
            return SynthDsp.Finish(g.HighPass(o, 250f), 0.9f, 0.1f, 80f, g.Fs);
        }
        static float[] CeramicBreak(SfxGen g)
        {
            var o = g.New(1.05f);
            g.Strike(o, 0, 0.6f, new[] { g.R.Range(280f, 340f), g.R.Range(520f, 600f), g.R.Range(900f, 1050f) }, new[] { 0.06f, 0.05f, 0.04f }, new[] { 1f, 0.6f, 0.4f }, 0f);
            g.Noise(o, 0, 0.0005f, 0.012f, 0.8f, 1, 2000f, 1.2f);
            int n = 20 + g.R.R(12);
            for (int k = 0; k < n; k++)
            {
                float t0 = 0.003f + Math.Min(0.6f, g.R.Exp(0.07f)); float f = 1000f * (float)Math.Pow(4.5, g.R.F());
                g.Partial(o, t0, f, g.R.Range(0.015f, 0.06f), g.R.Range(0.1f, 0.4f) * (float)Math.Exp(-t0 * 3f), g.R.F() * 6f);
            }
            for (int k = 0; k < 6; k++) g.Strike(o, g.R.Range(0.15f, 0.75f), g.R.Range(0.1f, 0.22f), new[] { g.R.Range(600f, 1500f), g.R.Range(1800f, 3000f) }, new[] { 0.03f, 0.02f }, new[] { 1f, 0.5f }, 0.4f, 4000f);
            return SynthDsp.Finish(g.HighPass(o, 150f), 0.9f, 0.1f, 60f, g.Fs);
        }
        static float[] WoodCrack(SfxGen g)
        {
            var o = g.New(0.65f);
            for (int i = 0; i < g.S(0.003f); i++) o[i] += g.R.N() * (1f - (float)i / g.S(0.003f));
            var crackle = g.New(0.65f); int n = 15 + g.R.R(12);
            for (int k = 0; k < n; k++)
            {
                int s0 = g.S(0.005f + Math.Min(0.25f, g.R.Exp(0.05f))), len = g.S(g.R.Range(0.0005f, 0.0015f)); float a = g.R.Range(0.1f, 0.5f);
                for (int i = 0; i < len && s0 + i < crackle.Length; i++) crackle[s0 + i] += g.R.N() * a;
            }
            var bp = new Svf(g.Fs, 1800f, 1.5f); for (int i = 0; i < crackle.Length; i++) o[i] += 1.5f * bp.BP(crackle[i]);
            g.Strike(o, 0, 0.28f, new[] { g.R.Range(100f, 130f), g.R.Range(240f, 290f), g.R.Range(480f, 560f), g.R.Range(1100f, 1300f) }, new[] { 0.15f, 0.12f, 0.08f, 0.05f }, new[] { 0.7f, 0.8f, 0.6f, 0.5f }, 0f);
            g.Noise(o, 0.01f, 0.01f, 0.08f, 0.25f, 0, 900f);
            return SynthDsp.Finish(o, 0.9f, 0.05f, 40f, g.Fs);
        }
        static float[] MetalClang(SfxGen g)
        {
            var o = g.New(2.5f);
            float f0 = g.R.Range(260f, 480f) * (g.Variant == 2 ? 0.6f : 1f);
            float[] ratios = { 1.0f, 1.59f, 2.14f, 2.30f, 2.65f, 2.92f, 3.60f, 4.06f, 5.40f, 6.8f };
            for (int k = 0; k < ratios.Length; k++)
            {
                float f = f0 * ratios[k] * g.R.Range(0.99f, 1.01f);
                float t60 = Math.Max(0.08f, Math.Min(2.4f, 2.2f * (float)Math.Pow(f0 / f, 0.6)));
                float amp = (float)(1.0 / Math.Pow(k + 1, 0.7)) * g.R.Range(0.7f, 1.3f) * 0.4f;
                g.Partial(o, 0, f, t60, amp, g.R.F() * 6f);
                g.Partial(o, 0, f * 1.003f, t60 * 0.9f, amp * 0.5f, g.R.F() * 6f); // beating twin
            }
            g.Noise(o, 0, 0.0003f, 0.01f, 0.4f, 2, 2000f);
            return SynthDsp.Finish(o, 0.85f, 0.05f, 100f, g.Fs);
        }
        static float[] MetalDent(SfxGen g)
        {
            var o = g.New(0.6f);
            g.Thump(o, 0, 105f, 70f, 0.03f, 0.001f, 0.06f, 1f);
            var crunch = g.New(0.08f); var f = new Svf(g.Fs, 1200f, 1f);
            for (int i = 0; i < crunch.Length; i++) { float gate = ((i / (g.Fs / 1000)) % 2 == 0) ? g.R.Range(0.2f, 1f) : g.R.Range(0f, 0.4f); crunch[i] = f.BP(g.R.N()) * gate * (float)Math.Exp(-(float)i / g.S(0.03f)); }
            SynthDsp.Mix(o, crunch, 0, 0.5f);
            g.Strike(o, 0.002f, 0.6f, new[] { g.R.Range(650f, 780f), g.R.Range(1800f, 2100f), g.R.Range(3100f, 3500f) }, new[] { 0.25f, 0.18f, 0.12f }, new[] { 0.5f, 0.35f, 0.2f }, 0f);
            return SynthDsp.Finish(o, 0.9f, 0.05f, 40f, g.Fs);
        }

        // ------------------------------------------------------------------ body / combat (impactful, not gory)
        static float[] BodyFall(SfxGen g)
        {
            var o = g.New(0.95f); float hit = 0.12f;
            // cloth rustle leading in
            var f = new Svf(g.Fs, 2000f, 0.7f);
            for (int i = 0; i < g.S(hit); i++) { float u = (float)i / g.S(hit); o[i] += 0.12f * u * u * f.BP(g.R.N()); }
            g.Thump(o, hit, 62f, 44f, 0.1f, 0.002f, 0.16f, 1f);
            g.Noise(o, hit, 0.001f, 0.05f, 0.5f, 0, 400f);
            g.Noise(o, hit, 0.001f, 0.03f, 0.5f, 1, 650f, 0.9f); // mid "slap" so it reads on small speakers
            g.Thump(o, hit, 130f, 95f, 0.05f, 0.002f, 0.06f, 0.35f);
            g.Strike(o, hit, 0.3f, new[] { g.R.Range(140f, 170f), g.R.Range(300f, 340f) }, new[] { 0.15f, 0.1f }, new[] { 1f, 0.6f }, 0f);
            float limb = hit + g.R.Range(0.08f, 0.14f);
            g.Thump(o, limb, 82f, 60f, 0.05f, 0.002f, 0.07f, 0.45f); g.Noise(o, limb, 0.001f, 0.03f, 0.2f, 0, 600f);
            g.Thump(o, hit + g.R.Range(0.2f, 0.3f), 92f, 80f, 0.05f, 0.002f, 0.05f, 0.22f);
            g.Noise(o, hit + 0.25f, 0.03f, 0.12f, 0.05f, 1, 1500f, 0.8f);
            return SynthDsp.Finish(o, 0.95f, 0.5f, 60f, g.Fs);
        }
        static void Whoosh(SfxGen g, float[] o, float at, float dur, float lo, float hi, float amp)
        {
            int s0 = g.S(at), n = Math.Min(o.Length - s0, g.S(dur)); var f = new Svf(g.Fs, lo, 1.2f); float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n; float fc = lo + (hi - lo) * (float)Math.Pow(Math.Sin(Math.PI * u), 1.5); f.Set(fc, 1.2f);
                float env = (float)Math.Pow(Math.Sin(Math.PI * u), 2);
                f.Process(g.R.N()); ph += SynthDsp.TwoPi * fc * 1.3f / g.Fs;
                o[s0 + i] += amp * env * (f.Band * 0.9f + 0.05f * (float)Math.Sin(ph));
            }
        }
        static float[] BladeWhoosh(SfxGen g)
        {
            var o = g.New(0.38f); float d = g.R.Range(0.22f, 0.32f);
            Whoosh(g, o, 0, d, 450f + g.Variant * 80f, 2600f + g.Variant * 300f, 1f);
            return SynthDsp.Finish(o, 0.8f, 1f, 20f, g.Fs);
        }
        static void ClothRip(SfxGen g, float[] o, float at, float dur, float amp)
        {
            int s0 = g.S(at), n = Math.Min(o.Length - s0, g.S(dur)); var f = new Svf(g.Fs, 2600f, 0.9f); float gate = 0; int left = 0;
            for (int i = 0; i < n; i++)
            {
                if (left-- <= 0) { left = g.S(g.R.Range(0.002f, 0.004f)); gate = g.R.Chance(0.7f) ? g.R.Range(0.3f, 1f) : 0f; }
                float t = (float)i / g.Fs; o[s0 + i] += amp * gate * f.BP(g.R.N()) * SynthDsp.AD(t, 0.002f, dur * 0.45f);
            }
        }
        static float[] Stab(SfxGen g)
        {
            var o = g.New(0.45f); float hit = 0.1f;
            Whoosh(g, o, 0, 0.12f, 700f, 2200f, 0.5f);
            g.Thump(o, hit, 95f, 50f, 0.04f, 0.001f, 0.07f, 1f);
            g.Noise(o, hit, 0.0005f, 0.02f, 0.55f, 1, 1400f, 1.2f);
            g.Noise(o, hit, 0.001f, 0.035f, 0.5f, 0, 700f);
            ClothRip(g, o, hit + 0.005f, 0.04f, 0.15f);
            return SynthDsp.Finish(g.Saturate(o, 1.4f), 0.9f, 0.5f, 30f, g.Fs);
        }
        static float[] Slash(SfxGen g)
        {
            var o = g.New(0.45f); float hit = 0.17f;
            Whoosh(g, o, 0, 0.2f, 900f, 3200f, 0.7f);
            ClothRip(g, o, hit, 0.1f, 0.6f);
            g.Thump(o, hit, 115f, 70f, 0.03f, 0.001f, 0.04f, 0.5f);
            return SynthDsp.Finish(o, 0.85f, 0.5f, 30f, g.Fs);
        }
        static float[] BluntHit(SfxGen g)
        {
            var o = g.New(0.45f); float m = 1f + g.Variant * 0.12f;
            g.Thump(o, 0, 78f * m, 45f * m, 0.05f, 0.001f, 0.12f, 1f);
            g.Strike(o, 0, 0.5f, new[] { g.R.Range(200f, 240f) * m, g.R.Range(450f, 520f) * m, g.R.Range(900f, 1000f) * m }, new[] { 0.08f, 0.06f, 0.04f }, new[] { 1.6f, 1.1f, 0.6f }, 2f, 3000f);
            g.Noise(o, 0, 0.0008f, 0.025f, 0.45f, 1, 1100f, 0.9f);
            return SynthDsp.Finish(g.Saturate(o, 1.6f), 0.95f, 0.1f, 30f, g.Fs);
        }

        // ------------------------------------------------------------------ vocal-ish (formant synthesis)
        internal static readonly float[][] VowelFormants =
        {
            new[] { 800f, 1250f, 2600f },  // 0 a  ㅏ
            new[] { 600f, 1000f, 2500f },  // 1 eo ㅓ
            new[] { 450f, 800f, 2500f },   // 2 o  ㅗ
            new[] { 330f, 800f, 2300f },   // 3 u  ㅜ
            new[] { 350f, 1400f, 2400f },  // 4 eu ㅡ
            new[] { 300f, 2250f, 3000f },  // 5 i  ㅣ
            new[] { 500f, 1850f, 2600f },  // 6 e  ㅐ/ㅔ
        };

        /// <summary>Voiced formant synthesis with per-sample f0 and vowel interpolation. Used by screams, gasps, crowd and (via BabbleSynth) babble.</summary>
        internal static void Voice(SfxGen g, float[] o, int s0, int n, Func<float, float> f0At, Func<float, float> ampAt, float[] formA, float[] formB, float formantScale, float breath, float tilt = 1800f)
        {
            var src = new Svf(g.Fs, tilt, 0.6f);
            var f1 = new Svf(g.Fs, formA[0], 8f); var f2 = new Svf(g.Fs, formA[1], 10f); var f3 = new Svf(g.Fs, formA[2], 12f);
            var asp = new Svf(g.Fs, 2500f, 0.6f);
            float ph = 0;
            for (int i = 0; i < n && s0 + i < o.Length; i++)
            {
                float u = (float)i / Math.Max(1, n - 1); float t = (float)i / g.Fs;
                if ((i & 31) == 0)
                {
                    float a = SynthDsp.Smooth(u);
                    float F1 = (formA[0] + (formB[0] - formA[0]) * a) * formantScale, F2 = (formA[1] + (formB[1] - formA[1]) * a) * formantScale, F3 = (formA[2] + (formB[2] - formA[2]) * a) * formantScale;
                    f1.Set(F1, F1 / 90f); f2.Set(F2, F2 / 120f); f3.Set(F3, F3 / 180f);
                }
                float saw = SynthDsp.Saw(ref ph, f0At(t), g.Fs);
                float s = src.LP(saw) + breath * asp.HP(g.R.N());
                f1.Process(s); f2.Process(s); f3.Process(s);
                // Band has peak gain Q (~9..14 here); these weights give roughly 1 : 0.7 : 0.45 formant levels
                float y = f1.Band * 0.11f + f2.Band * 0.07f + f3.Band * 0.035f;
                o[s0 + i] += y * ampAt(t);
            }
        }

        static float[] Scream(SfxGen g)
        {
            bool male = g.Variant == 1, yelp = g.Variant == 2;
            float len = yelp ? 0.55f : male ? 1.1f : 1.25f; var o = g.New(len + 0.4f);
            float fStart = male ? 230f : 420f, fPeak = male ? 390f : 640f, fEnd = male ? 190f : 350f; if (yelp) { fStart = 480f; fPeak = 760f; fEnd = 520f; }
            float vibRate = g.R.Range(5.5f, 7f);
            Func<float, float> f0 = t =>
            {
                float u = t / len; float f = u < 0.12f ? fStart + (fPeak - fStart) * SynthDsp.Smooth(u / 0.12f) : u < 0.6f ? fPeak * (1f + 0.04f * (u - 0.12f)) : fPeak + (fEnd - fPeak) * SynthDsp.Smooth((u - 0.6f) / 0.4f);
                return f * (1f + 0.025f * (float)Math.Sin(SynthDsp.TwoPi * vibRate * t));
            };
            Func<float, float> amp = t => { float u = t / len; return SynthDsp.Clamp01(u / 0.05f) * (u > 0.75f ? SynthDsp.Clamp01((1f - u) / 0.25f) : 1f) * (0.85f + 0.15f * (float)Math.Sin(SynthDsp.TwoPi * 11f * t)); };
            Voice(g, o, 0, g.S(len), f0, amp, VowelFormants[0], VowelFormants[6], male ? 0.9f : 1.12f, 0.25f, 2600f);
            g.Saturate(o, 2.2f);
            g.LowPass(o, 1300f, 0.7f); g.LowPass(o, 1800f, 0.6f); // muffled (through a wall / hand)
            return SynthDsp.Finish(g.Reverb(o, 0.25f, 0.8f), 0.85f, 1f, 80f, g.Fs);
        }
        static float[] Gasp(SfxGen g)
        {
            bool male = g.Variant == 1; var o = g.New(0.5f);
            // breathy inhale: noise through "ha" formants with fast attack
            var n1 = new Svf(g.Fs, 700f, 5f); var n2 = new Svf(g.Fs, 1250f, 6f); var n3 = new Svf(g.Fs, 2600f, 7f);
            for (int i = 0; i < g.S(0.42f); i++)
            {
                float t = (float)i / g.Fs; float x = g.R.N(); n1.Process(x); n2.Process(x); n3.Process(x);
                float env = SynthDsp.AD(t, 0.03f, 0.1f) * (t < 0.15f ? 1f : 1f);
                o[i] += env * (n1.Band * 0.2f + n2.Band * 0.16f + n3.Band * 0.1f);
            }
            float fv = male ? 150f : 260f;
            Voice(g, o, 0, g.S(0.06f), t => fv * (1 + 0.3f * t / 0.06f), t => SynthDsp.AD(t, 0.005f, 0.02f) * 0.35f, VowelFormants[1], VowelFormants[0], male ? 0.9f : 1.1f, 0.6f);
            return SynthDsp.Finish(o, 0.7f, 1f, 40f, g.Fs);
        }
        static float[] Heartbeat(SfxGen g, float period)
        {
            var o = g.New(period);
            g.Thump(o, 0.005f, 68f, 46f, 0.04f, 0.008f, 0.06f, 1f);
            g.Thump(o, 0.005f, 136f, 100f, 0.03f, 0.006f, 0.04f, 0.4f);
            g.Noise(o, 0.005f, 0.005f, 0.04f, 0.2f, 0, 150f);
            g.Thump(o, 0.3f, 74f, 55f, 0.04f, 0.006f, 0.05f, 0.7f);
            g.Thump(o, 0.3f, 148f, 115f, 0.03f, 0.005f, 0.035f, 0.25f);
            g.LowPass(o, 260f);
            return SynthDsp.Finish(o, 0.9f, 0.5f, 5f, g.Fs);
        }

        // ------------------------------------------------------------------ bells, gavel, stingers
        static void Bell(SfxGen g, float[] o, float at, float f, float amp, float lenScale = 1f)
        {
            float[] r = { 1f, 2f, 2.4f, 3f, 4.2f, 5.4f }; float[] a = { 1f, 0.5f, 0.3f, 0.25f, 0.15f, 0.08f }; float[] d = { 2.4f, 1.6f, 1.2f, 1.0f, 0.7f, 0.5f };
            for (int k = 0; k < r.Length; k++) g.Partial(o, at, f * r[k], d[k] * lenScale, amp * a[k] * 0.4f, g.R.F());
            g.Partial(o, at, f * 7f, 0.05f, amp * 0.08f);
        }
        static float Note(int midi) => 440f * (float)Math.Pow(2, (midi - 69) / 12.0);
        static float[] Chime(SfxGen g)
        {
            var o = g.New(3.4f);
            int[] notes = { 84, 79, 81, 76 }; // C6 G5 A5 E5 — the butler's four-tone chime
            for (int k = 0; k < notes.Length; k++) Bell(g, o, 0.02f + k * 0.34f, Note(notes[k]), k == 3 ? 1f : 0.85f, k == 3 ? 1.3f : 0.8f);
            return SynthDsp.Finish(g.Reverb(o, 0.3f, 1.2f), 0.8f, 0.5f, 200f, g.Fs);
        }
        static float[] Gavel(SfxGen g)
        {
            var o = g.New(1.4f); float[] hits = g.Variant == 1 ? new[] { 0.0f, 0.24f } : new[] { 0.0f };
            foreach (var h in hits)
            {
                g.Strike(o, h, 1f, new[] { g.R.Range(210f, 240f), g.R.Range(500f, 560f), g.R.Range(1100f, 1250f), g.R.Range(2300f, 2600f) }, new[] { 0.18f, 0.13f, 0.08f, 0.05f }, new[] { 1f, 0.6f, 0.4f, 0.25f }, 1f, 7000f);
                g.Thump(o, h, 110f, 90f, 0.02f, 0.001f, 0.04f, 0.6f);
            }
            var x = SynthDsp.Finish(o, 0.9f, 0.05f, 100f, g.Fs);
            return SynthDsp.Finish(g.Reverb(x, 0.35f, 1.3f), 0.9f, 0.05f, 100f, g.Fs);
        }
        static void SawChord(SfxGen g, float[] o, float at, float dur, float[] freqs, float amp, float cutA, float cutPeak, float cutEnd, float attack, float decay)
        {
            int s0 = g.S(at), n = Math.Min(o.Length - s0, g.S(dur)); var ph = new float[freqs.Length * 2]; var lp = new Svf(g.Fs, cutA, 0.9f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / g.Fs;
                if ((i & 15) == 0) { float cut = t < attack ? cutA + (cutPeak - cutA) * (t / attack) : cutEnd + (cutPeak - cutEnd) * (float)Math.Exp(-(t - attack) / Math.Max(0.01f, decay)); lp.Set(cut, 0.9f); }
                float s = 0;
                for (int k = 0; k < freqs.Length; k++) { s += SynthDsp.Saw(ref ph[2 * k], freqs[k] * 1.004f, g.Fs); s += SynthDsp.Saw(ref ph[2 * k + 1], freqs[k] * 0.996f, g.Fs); }
                o[s0 + i] += amp * lp.LP(s / (freqs.Length * 2)) * SynthDsp.AD(t, attack * 0.5f, decay);
            }
        }
        static float[] StingerBreak(SfxGen g)
        {
            var o = g.New(1.9f); float hit = 0.15f;
            for (int i = 0; i < g.S(hit); i++) { float u = (float)i / g.S(hit); o[i] += 0.35f * u * u * u * g.R.N(); }
            SynthDsp.Mix(o, GlassCore(g, 1.4f, 90, 1.2f), g.S(hit), 0.8f);
            g.Thump(o, hit, 72f, 40f, 0.12f, 0.002f, 0.35f, 1f);
            int s0 = g.S(hit), n = g.S(0.45f); float ph = 0; var lp = new Svf(g.Fs, 3000f, 0.8f);
            for (int i = 0; i < n; i++) { float t = (float)i / g.Fs; float f = 120f + 1280f * (float)Math.Exp(-t / 0.1f); o[s0 + i] += 0.35f * lp.LP(SynthDsp.Saw(ref ph, f, g.Fs)) * SynthDsp.AD(t, 0.001f, 0.2f); }
            g.Partial(o, hit, 3100f, 0.9f, 0.15f); g.Partial(o, hit, 4700f, 0.7f, 0.1f);
            return SynthDsp.Finish(g.Reverb(g.Saturate(o, 1.3f), 0.25f, 1.1f), 0.95f, 0.5f, 150f, g.Fs);
        }
        // ------------------------------------------------------------------ class-trial minigames
        static float[] TrialFire(SfxGen g)
        {
            var o = g.New(0.35f); int n = g.S(0.16f); float ph = 0; float top = g.Variant == 0 ? 2600f : 2300f;
            for (int i = 0; i < n; i++) { float t = (float)i / g.Fs; float f = 480f + (top - 480f) * (float)Math.Exp(-t / 0.04f); ph += SynthDsp.TwoPi * f / g.Fs; o[i] += 0.5f * (float)Math.Sin(ph) * SynthDsp.AD(t, 0.001f, 0.05f); }
            g.Noise(o, 0, 0.0005f, 0.03f, 0.7f, 2, 3000f);
            g.Thump(o, 0, 180f, 70f, 0.02f, 0.001f, 0.05f, 0.7f);
            return SynthDsp.Finish(g.Reverb(o, 0.15f, 0.6f), 0.9f, 0.2f, 20f, g.Fs);
        }
        static float[] TrialLock(SfxGen g)
        {
            var o = g.New(0.25f); g.Partial(o, 0, 1760f, 0.08f, 0.6f); g.Partial(o, 0.06f, 2640f, 0.1f, 0.6f); g.Noise(o, 0, 0.0002f, 0.002f, 0.3f, 2, 4000f);
            return SynthDsp.Finish(o, 0.85f, 0.1f, 10f, g.Fs);
        }
        static float[] TrialTick(SfxGen g)
        {
            var o = g.New(0.06f); g.Strike(o, 0, 1f, new[] { 3200f, 4700f }, new[] { 0.015f, 0.01f }, new[] { 0.6f, 0.4f }, 0.4f, 8000f);
            return SynthDsp.Finish(o, 0.7f, 0.05f, 5f, g.Fs);
        }
        static float[] TrialWrong(SfxGen g)
        {
            var o = g.New(0.45f); float p1 = 0, p2 = 0; int n = g.S(0.4f); var lp = new Svf(g.Fs, 1400f, 0.8f);
            for (int i = 0; i < n; i++) { float t = (float)i / g.Fs; float s = SynthDsp.Saw(ref p1, 98f, g.Fs) + SynthDsp.Saw(ref p2, 104f, g.Fs); o[i] += 0.5f * lp.LP(s) * SynthDsp.AD(t, 0.005f, 0.18f); }
            g.Thump(o, 0, 120f, 60f, 0.05f, 0.002f, 0.1f, 0.5f);
            return SynthDsp.Finish(o, 0.85f, 0.2f, 30f, g.Fs);
        }
        static float[] TrialImpact(SfxGen g)
        {
            var o = g.New(1.6f); g.Thump(o, 0, 110f, 38f, 0.08f, 0.002f, 0.35f, 1f); g.Noise(o, 0, 0.001f, 0.12f, 0.8f, 0, 900f); g.Noise(o, 0, 0.0005f, 0.02f, 0.5f, 2, 3500f);
            return SynthDsp.Finish(g.Reverb(g.Saturate(o, 1.6f), 0.35f, 1.3f), 0.95f, 0.1f, 200f, g.Fs);
        }
        static float[] TrialCoin(SfxGen g)
        {
            var o = g.New(0.6f); float b = g.R.Range(1900f, 2300f);
            g.Strike(o, 0, 1f, new[] { b, b * 1.51f, b * 2.23f }, new[] { 0.35f, 0.25f, 0.18f }, new[] { 0.6f, 0.4f, 0.3f }, 0.5f, 9000f);
            return SynthDsp.Finish(o, 0.7f, 0.05f, 40f, g.Fs);
        }

        // ------------------------------------------------------------------ the trial's gothic palette
        /// <summary>A great bronze bell, struck once: hum, prime, minor tierce, quint and nominal partials with long, beating decays.</summary>
        static float[] BellToll(SfxGen g)
        {
            var o = g.New(6.5f); float f = g.Variant == 0 ? 110f : 146.8f;
            float[] ratio = { 0.5f, 0.503f, 1f, 1.19f, 1.5f, 2f, 2.01f, 2.52f, 2.67f, 3.01f, 4.07f, 5.2f };
            float[] t60 = { 6.2f, 6.0f, 4.6f, 3.8f, 3.0f, 2.6f, 2.5f, 1.9f, 1.7f, 1.4f, 0.9f, 0.6f };
            float[] amp = { 0.55f, 0.35f, 0.9f, 0.7f, 0.35f, 0.6f, 0.3f, 0.3f, 0.22f, 0.18f, 0.12f, 0.08f };
            for (int k = 0; k < ratio.Length; k++) g.Partial(o, 0.01f, f * ratio[k], t60[k], amp[k] * 0.35f, g.R.F() * 6.28f);
            g.Noise(o, 0.005f, 0.0005f, 0.012f, 0.35f, 1, 2400f, 1.2f);    // clapper
            g.Thump(o, 0.005f, 70f, 55f, 0.2f, 0.002f, 0.25f, 0.25f);
            return SynthDsp.Finish(g.Reverb(o, 0.42f, 1.7f, 0.84f, 0.45f), 0.85f, 0.2f, 600f, g.Fs);
        }
        /// <summary>A candle pinched out: a short breath, a soft puff, a hiss of wick.</summary>
        static float[] CandleSnuff(SfxGen g)
        {
            var o = g.New(0.6f);
            g.Noise(o, 0f, 0.025f, 0.09f, 0.55f, 1, g.Variant == 0 ? 1300f : 1700f, 0.8f);
            g.Thump(o, 0.02f, 95f, 70f, 0.03f, 0.004f, 0.05f, 0.35f);
            g.Noise(o, 0.07f, 0.01f, 0.12f, 0.12f, 2, 5500f);
            return SynthDsp.Finish(g.Reverb(o, 0.18f, 0.8f), 0.8f, 0.3f, 60f, g.Fs);
        }
        /// <summary>Candles catch again: a rising breath of air and a few crackles.</summary>
        static float[] CandleFlare(SfxGen g)
        {
            var o = g.New(1.0f); int n = g.S(0.55f); var bp = new Svf(g.Fs, 300f, 1.1f);
            for (int i = 0; i < n; i++) { float t = (float)i / g.Fs; if ((i & 15) == 0) bp.Set(300f + 2600f * SynthDsp.Smooth(t / 0.5f), 1.1f); o[i] += 0.5f * bp.BP(g.R.N()) * (float)Math.Sin(Math.PI * SynthDsp.Clamp01(t / 0.55f)); }
            for (int k = 0; k < 7; k++) g.Noise(o, g.R.Range(0.25f, 0.8f), 0.0002f, g.R.Range(0.0005f, 0.002f), g.R.Range(0.2f, 0.5f), 2, 3000f);
            return SynthDsp.Finish(g.Reverb(o, 0.2f, 0.9f), 0.75f, 1f, 80f, g.Fs);
        }
        /// <summary>A quill scratching a line into the record.</summary>
        static float[] Quill(SfxGen g)
        {
            var o = g.New(0.32f); int grains = 4 + g.Variant; float t = 0.005f;
            for (int k = 0; k < grains; k++) { g.Noise(o, t, 0.002f, g.R.Range(0.008f, 0.018f), g.R.Range(0.5f, 1f), 1, g.R.Range(3200f, 5200f), 2.2f); t += g.R.Range(0.03f, 0.05f); }
            return SynthDsp.Finish(g.Reverb(o, 0.12f, 0.6f), 0.7f, 0.2f, 30f, g.Fs);
        }
        /// <summary>A sheet of parchment slid across oak.</summary>
        static float[] Parchment(SfxGen g)
        {
            var o = g.New(0.55f); int n = g.S(0.42f); var f = new Svf(g.Fs, g.Variant == 0 ? 2200f : 2800f, 0.7f); float gate = 1; int left = 0;
            for (int i = 0; i < n; i++) { if (left-- <= 0) { left = g.S(g.R.Range(0.003f, 0.009f)); gate = g.R.Range(0.5f, 1.2f); } float u = (float)i / n; o[i] += f.BP(g.R.N()) * (float)Math.Pow(Math.Sin(Math.PI * u), 0.7) * gate * 0.8f; }
            g.Noise(o, 0.38f, 0.004f, 0.04f, 0.25f, 0, 260f);
            return SynthDsp.Finish(o, 0.6f, 2f, 40f, g.Fs);
        }
        /// <summary>A heavy wooden stamp pressed into warm wax.</summary>
        static float[] WaxStamp(SfxGen g)
        {
            var o = g.New(0.9f);
            g.Strike(o, 0, 1f, new[] { g.R.Range(170f, 190f), g.R.Range(390f, 430f), g.R.Range(880f, 960f), 1750f }, new[] { 0.16f, 0.1f, 0.06f, 0.03f }, new[] { 1f, 0.55f, 0.3f, 0.12f }, 1.2f, 3000f);
            g.Thump(o, 0, 130f, 62f, 0.03f, 0.001f, 0.07f, 0.9f);
            g.Noise(o, 0.012f, 0.004f, 0.06f, 0.25f, 0, 700f);             // the wax gives
            return SynthDsp.Finish(g.Reverb(o, 0.22f, 1f), 0.9f, 0.05f, 120f, g.Fs);
        }
        /// <summary>A soft choir swell (ah → oh) on an open D chord: a truth has been named.</summary>
        static float[] ChoirSwell(SfxGen g)
        {
            float len = 2.4f; var o = g.New(len + 1.2f); int[] notes = { 50, 57, 62, 66, 69 };
            foreach (var nt in notes)
                for (int v = 0; v < 2; v++)
                {
                    float f = Note(nt) * (v == 0 ? 1.003f : 0.997f); float ph = g.R.F() * 6f; float rate = g.R.Range(4.6f, 5.6f);
                    float fs = nt < 60 ? 0.85f : 1.05f; float a = nt < 60 ? 0.8f : 0.6f;
                    Voice(g, o, g.S(g.R.Range(0f, 0.05f)), g.S(len), t => f * (1f + 0.004f * (float)Math.Sin(6.283f * rate * t + ph)),
                        t => a * SynthDsp.Smooth(t / 0.7f) * (t > len - 0.9f ? SynthDsp.Clamp01((len - t) / 0.9f) : 1f), VowelFormants[0], VowelFormants[2], fs, 0.2f, 2400f);
                }
            Bell(g, o, 0.02f, Note(86), 0.35f, 0.9f);
            return SynthDsp.Finish(g.Reverb(o, 0.45f, 1.6f, 0.82f, 0.4f), 0.8f, 20f, 400f, g.Fs);
        }
        /// <summary>A low organ chord with a dissonant upper note: a mistake before the whole court.</summary>
        static float[] OrganSting(SfxGen g)
        {
            var o = g.New(1.9f); int[] notes = { 38, 45, 50, 51 }; float[] draw = { 1f, 0.55f, 0.4f, 0.28f, 0.16f, 0.1f }; float[] mult = { 1f, 2f, 3f, 4f, 6f, 8f };
            int n = g.S(1.3f);
            foreach (var nt in notes)
            {
                float f = Note(nt); var ph = new double[draw.Length];
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / g.Fs; float env = t < 0.03f ? t / 0.03f : t < 0.9f ? 1f : (float)Math.Exp(-(t - 0.9f) / 0.15f);
                    float s = 0; for (int h = 0; h < draw.Length; h++) { ph[h] += 6.283185307 * f * mult[h] / g.Fs; s += draw[h] * (float)Math.Sin(ph[h]); }
                    o[i] += 0.16f * s * env * (nt == 51 ? 0.7f : 1f);
                }
            }
            g.Thump(o, 0, 80f, 45f, 0.08f, 0.003f, 0.2f, 0.5f);
            return SynthDsp.Finish(g.Reverb(g.LowPass(o, 2600f), 0.38f, 1.5f, 0.8f), 0.85f, 1f, 300f, g.Fs);
        }
        /// <summary>A small brass plate touched: a dry metallic tick.</summary>
        static float[] PlateHover(SfxGen g)
        {
            var o = g.New(0.16f); float b = g.Variant == 0 ? 2600f : 2950f;
            g.Strike(o, 0, 0.6f, new[] { b, b * 1.42f, b * 2.03f }, new[] { 0.05f, 0.035f, 0.02f }, new[] { 0.6f, 0.35f, 0.2f }, 0.3f, 9000f);
            return SynthDsp.Finish(o, 0.55f, 0.05f, 20f, g.Fs);
        }
        /// <summary>An hourglass turned over: a wooden knock, then sand pouring.</summary>
        static float[] Hourglass(SfxGen g)
        {
            var o = g.New(1.6f);
            g.Strike(o, 0, 0.8f, new[] { 240f, 560f, 1300f }, new[] { 0.1f, 0.07f, 0.04f }, new[] { 1f, 0.5f, 0.25f }, 1f, 4000f);
            int s0 = g.S(0.12f), n = g.S(1.3f); var bp = new Svf(g.Fs, 5200f, 1.2f);
            for (int i = 0; i < n && s0 + i < o.Length; i++) { float t = (float)i / g.Fs; float env = SynthDsp.Smooth(t / 0.15f) * (t > 0.9f ? SynthDsp.Clamp01((1.3f - t) / 0.4f) : 1f); float grain = g.R.F() < 0.25f ? g.R.N() : 0f; o[s0 + i] += 0.35f * bp.BP(grain) * env; }
            return SynthDsp.Finish(g.Reverb(o, 0.18f, 0.8f), 0.7f, 0.1f, 80f, g.Fs);
        }
        /// <summary>The stone hall under the music: air, a low hum, far creaks of old wood and the odd candle crackle.</summary>
        static float[] CourtAmbience(SfxGen g)
        {
            float loop = 9f, xf = 1f; var o = g.New(loop + xf); var lp = new Svf(g.Fs, 240f, 0.7f); var hp = new Svf(g.Fs, 3000f, 0.7f); float brown = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = (float)i / g.Fs; brown = brown * 0.996f + g.R.N() * 0.04f;
                float swell = 0.75f + 0.25f * (float)Math.Sin(6.283f * t / 4.5f);
                o[i] = 1.4f * lp.LP(brown) * swell + 0.012f * hp.HP(g.R.N()) + 0.05f * (float)Math.Sin(6.283f * 55f * t) * (0.6f + 0.4f * (float)Math.Sin(6.283f * 0.21f * t));
            }
            for (int k = 0; k < 3; k++) { float at = g.R.Range(0.4f, loop - 0.6f); g.Strike(o, at, 0.12f, new[] { g.R.Range(140f, 220f), g.R.Range(380f, 520f), g.R.Range(900f, 1300f) }, new[] { 0.2f, 0.12f, 0.06f }, new[] { 1f, 0.6f, 0.3f }, 3f, 1500f); }
            float tt = 0; while ((tt += g.R.Exp(1f / 1.4f)) < loop + xf - 0.05f) g.Noise(o, tt, 0.0002f, g.R.Range(0.0004f, 0.0012f), g.R.Range(0.03f, 0.09f), 2, 2800f);
            g.Reverb(o, 0.35f, 1.6f);
            return SynthDsp.FinishLoop(SynthDsp.MakeLoop(o, g.S(loop)), 0.5f);
        }

        static float[] StingerObjection(SfxGen g)
        {
            var o = g.New(1.4f);
            g.Noise(o, 0, 0.0003f, 0.004f, 1f, 2, 1500f);
            SawChord(g, o, 0.01f, 1.2f, new[] { Note(45), Note(52), Note(57), Note(60), Note(64) }, 1.2f, 300f, 3500f, 800f, 0.02f, 0.25f);
            g.Thump(o, 0.01f, 60f, 45f, 0.1f, 0.002f, 0.2f, 0.6f);
            return SynthDsp.Finish(g.Reverb(o, 0.3f, 1.1f), 0.95f, 0.2f, 150f, g.Fs);
        }
        static float[] StingerDiscovery(SfxGen g)
        {
            var o = g.New(2.8f);
            float[] cluster = { Note(49), Note(50), Note(55), Note(56), Note(62) };
            SawChord(g, o, 0f, 0.6f, cluster, 1f, 1500f, 5000f, 1200f, 0.006f, 0.25f);
            SawChord(g, o, 0.42f, 2.2f, cluster, 1.3f, 1500f, 5000f, 900f, 0.006f, 0.8f);
            foreach (var h in new[] { 0f, 0.42f }) { g.Partial(o, h, 1180f, 0.6f, 0.25f); g.Partial(o, h, 1730f, 0.5f, 0.18f); g.Partial(o, h, 2640f, 0.4f, 0.12f); }
            g.Thump(o, 0.42f, 60f, 42f, 0.15f, 0.003f, 0.5f, 1f);
            return SynthDsp.Finish(g.Reverb(g.Saturate(o, 1.5f), 0.3f, 1.2f), 0.95f, 0.2f, 250f, g.Fs);
        }
        static float[] StingerVerdict(SfxGen g)
        {
            var o = g.New(3.2f);
            g.Thump(o, 0, 95f, 70f, 0.3f, 0.002f, 0.3f, 1f); g.Noise(o, 0, 0.001f, 0.04f, 0.4f, 0, 600f);
            // chord swell (D minor), opening filter
            int n = g.S(2.9f); var ph = new float[8]; var lp = new Svf(g.Fs, 400f, 0.8f); float[] fr = { Note(50), Note(53), Note(57), Note(62) };
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / g.Fs; if ((i & 15) == 0) lp.Set(400f + 2100f * SynthDsp.Smooth(t / 1.2f), 0.8f);
                float s = 0; for (int k = 0; k < 4; k++) { s += SynthDsp.Saw(ref ph[2 * k], fr[k] * 1.003f, g.Fs) + SynthDsp.Saw(ref ph[2 * k + 1], fr[k] * 0.997f, g.Fs); }
                float env = t < 1.4f ? SynthDsp.Smooth(t / 0.8f) : (float)Math.Exp(-(t - 1.4f) / 0.6f);
                o[i] += 0.5f * lp.LP(s / 8f) * env;
            }
            g.Thump(o, 1.4f, 70f, 42f, 0.2f, 0.002f, 0.5f, 1.1f); g.Noise(o, 1.4f, 0.002f, 0.4f, 0.25f, 2, 3000f, 0.7f, 1.6f);
            return SynthDsp.Finish(g.Reverb(g.Saturate(o, 1.3f), 0.3f, 1.3f), 0.95f, 0.5f, 250f, g.Fs);
        }
        static float[] StingerEvidence(SfxGen g)
        {
            var o = g.New(1.4f); int[] notes = { 84, 88, 91, 96 };
            for (int k = 0; k < notes.Length; k++) Bell(g, o, k * 0.07f, Note(notes[k]), 0.8f, 0.3f);
            for (int k = 0; k < 12; k++) g.Partial(o, g.R.Range(0.2f, 0.75f), g.R.Range(4000f, 9000f), g.R.Range(0.05f, 0.15f), 0.08f);
            g.Thump(o, 0, Note(72), Note(72), 1f, 0.02f, 0.4f, 0.12f); g.Thump(o, 0, Note(79), Note(79), 1f, 0.02f, 0.4f, 0.1f);
            return SynthDsp.Finish(g.Reverb(o, 0.25f, 1f), 0.8f, 0.2f, 150f, g.Fs);
        }
        static float[] StingerLoopReset(SfxGen g)
        {
            var o = g.New(2.7f); float swell = 1.6f, stop = 0.5f;
            float[] fr = { Note(45), Note(52), Note(58), Note(65) }; var ph = new float[4]; var hp = new Svf(g.Fs, 200f, 0.7f); float speed = 1f;
            for (int i = 0; i < g.S(swell + stop); i++)
            {
                float t = (float)i / g.Fs; float env;
                if (t < swell) env = (float)(Math.Exp(3.5 * t / swell) - 1) / (float)(Math.Exp(3.5) - 1); else { float u = (t - swell) / stop; speed = Math.Max(0f, 1f - u); env = (1f - u) * (1f - u); }
                float s = 0; for (int k = 0; k < 4; k++) { ph[k] += SynthDsp.TwoPi * fr[k] * speed / g.Fs; s += (float)Math.Sin(ph[k]) + 0.3f * (float)Math.Sin(2 * ph[k]); }
                if ((i & 31) == 0) hp.Set(200f + 5800f * SynthDsp.Clamp01(t / swell) * speed + 20f, 0.7f);
                o[i] += env * (0.18f * s + 0.25f * hp.HP(g.R.N()) * (t < swell ? 1f : speed));
            }
            g.Thump(o, swell + stop + 0.02f, 55f, 38f, 0.1f, 0.002f, 0.25f, 0.8f);
            return SynthDsp.Finish(g.Reverb(o, 0.3f, 1.4f), 0.9f, 1f, 150f, g.Fs);
        }
        static float[] StingerChapterClear(SfxGen g)
        {
            var o = g.New(2.0f); int[] run = { 67, 72, 76, 79 };
            for (int k = 0; k < run.Length; k++) SawChord(g, o, k * 0.11f, 0.3f, new[] { Note(run[k]) }, 0.8f, 800f, 4000f, 1500f, 0.01f, 0.12f);
            SawChord(g, o, 0.45f, 1.5f, new[] { Note(60), Note(64), Note(67), Note(72) }, 1f, 600f, 3500f, 1200f, 0.02f, 0.6f);
            Bell(g, o, 0.45f, Note(84), 0.5f, 0.6f);
            return SynthDsp.Finish(g.Reverb(o, 0.25f, 1.1f), 0.85f, 0.2f, 200f, g.Fs);
        }

        // ------------------------------------------------------------------ UI
        static float[] UiHover(SfxGen g) { var o = g.New(0.05f); g.Partial(o, 0, 2400f, 0.03f, 0.8f); g.Partial(o, 0, 4800f, 0.015f, 0.2f); return SynthDsp.Finish(o, 0.6f, 0.3f, 10f, g.Fs); }
        static float[] UiTwoTone(SfxGen g, float a, float b)
        {
            var o = g.New(0.24f);
            foreach (var (f, at) in new[] { (a, 0f), (b, 0.06f) })
            {
                int s0 = g.S(at), n = g.S(0.16f);
                for (int i = 0; i < n && s0 + i < o.Length; i++) { float t = (float)i / g.Fs; float w = SynthDsp.TwoPi * f * t; o[s0 + i] += ((float)Math.Sin(w) + 0.25f * (float)Math.Sin(3 * w)) * SynthDsp.AD(t, 0.002f, 0.05f); }
            }
            return SynthDsp.Finish(o, 0.7f, 0.2f, 20f, g.Fs);
        }
        static float[] UiPage(SfxGen g)
        {
            var o = g.New(0.24f); var f = new Svf(g.Fs, g.Variant == 0 ? 3500f : 4200f, 0.8f); int n = g.S(0.15f); float gate = 1; int left = 0;
            for (int i = 0; i < n; i++) { if (left-- <= 0) { left = g.S(0.002f); gate = g.R.Range(0.7f, 1.3f); } float u = (float)i / n; o[i] += f.BP(g.R.N()) * (float)Math.Sin(Math.PI * u) * gate; }
            g.Noise(o, 0.12f, 0.005f, 0.03f, 0.3f, 0, 300f);
            return SynthDsp.Finish(o, 0.6f, 1f, 20f, g.Fs);
        }

        // ------------------------------------------------------------------ environment
        static void Tick(SfxGen g, float[] o, float at, bool tock, float amp)
        {
            float m = tock ? 0.75f : 1f;
            g.Strike(o, at, amp, new[] { 3200f * m, 4900f * m, 7100f * m, 1200f * m }, new[] { 0.02f, 0.015f, 0.01f, 0.03f }, new[] { 0.5f, 0.35f, 0.2f, 0.35f }, 0.4f, 8000f);
        }
        static float[] ClockTick(SfxGen g) { var o = g.New(0.09f); Tick(g, o, 0.001f, g.Variant == 1, 1f); return SynthDsp.Finish(o, 0.6f, 0.05f, 10f, g.Fs); }
        static float[] ClockLoop(SfxGen g)
        {
            var o = g.New(2f); Tick(g, o, 0.002f, false, 1f); Tick(g, o, 1.002f, true, 0.9f);
            g.Noise(o, 0.0f, 0.3f, 0.5f, 0.01f, 1, 900f, 1f, 2f);
            return SynthDsp.FinishLoop(o, 0.6f);
        }
        static float[] RainLoop(SfxGen g)
        {
            float loop = 6f, xf = 0.5f; var o = g.New(loop + xf); int n = o.Length;
            var lp = new Svf(g.Fs, 2800f, 0.6f); var hp = new Svf(g.Fs, 300f, 0.6f);
            for (int i = 0; i < n; i++) { float t = (float)i / g.Fs; float am = 1f + 0.15f * (float)Math.Sin(SynthDsp.TwoPi * 0.13f * t) + 0.1f * (float)Math.Sin(SynthDsp.TwoPi * 0.31f * t + 1f); o[i] = 0.25f * am * hp.HP(lp.LP(g.R.N())); }
            float tt = 0; while ((tt += g.R.Exp(1f / 30f)) < loop + xf - 0.05f) { float a = g.R.Range(0.05f, 0.3f); g.Noise(o, tt, 0.0001f, 0.0008f, a, 2, 3000f); g.Partial(o, tt, g.R.Range(1500f, 4500f), g.R.Range(0.01f, 0.03f), a * 0.5f, g.R.F() * 6); }
            tt = 0; while ((tt += g.R.Exp(1f / 3f)) < loop + xf - 0.1f) { float a = g.R.Range(0.2f, 0.45f); g.Noise(o, tt, 0.0005f, 0.02f, a, 1, 1400f, 0.8f); g.Partial(o, tt, g.R.Range(600f, 1200f), 0.04f, a * 0.4f); }
            return SynthDsp.FinishLoop(SynthDsp.MakeLoop(o, g.S(loop)), 0.7f);
        }
        static float[] WaterSplash(SfxGen g)
        {
            var o = g.New(0.95f);
            g.Noise(o, 0, 0.003f, 0.12f, 1f, 1, g.R.Range(1200f, 2000f), 0.7f);
            g.Noise(o, 0, 0.002f, 0.05f, 0.3f, 2, 4000f);
            g.Thump(o, 0, 110f, 55f, 0.05f, 0.002f, 0.08f, 0.6f);
            for (int b = 0; b < 15; b++) { float t0 = 0.05f + Math.Min(0.65f, g.R.Exp(0.12f)); Bubble(g, o, t0, g.R.Range(400f, 1500f), 0.35f, g.R.Range(0.02f, 0.06f), g.R.Range(0.1f, 0.35f)); }
            for (int b = 0; b < 5; b++) Bubble(g, o, g.R.Range(0.3f, 0.8f), g.R.Range(900f, 1800f), 0.5f, 0.02f, g.R.Range(0.05f, 0.12f));
            return SynthDsp.Finish(o, 0.9f, 0.3f, 60f, g.Fs);
        }
        static float[] HydraulicPress(SfxGen g)
        {
            var o = g.New(3.0f); float slam = 2.05f; int nm = g.S(slam + 0.6f);
            float p1 = 0, p2 = 0; var lp = new Svf(g.Fs, 600f, 0.8f); var hs = new Svf(g.Fs, 2500f, 0.7f);
            for (int i = 0; i < nm; i++)
            {
                float t = (float)i / g.Fs; float f = 45f + 15f * SynthDsp.Smooth(t / 0.6f);
                float motor = SynthDsp.Saw(ref p1, f, g.Fs) + 0.6f * SynthDsp.Saw(ref p2, f * 2.01f, g.Fs);
                float env = SynthDsp.Clamp01(t / 0.4f) * (t > slam + 0.05f ? (float)Math.Exp(-(t - slam - 0.05f) / 0.15f) : 1f);
                float hiss = (0.05f + 0.25f * SynthDsp.Smooth((t - 0.3f) / 1.5f)) * (t > slam ? (float)Math.Exp(-(t - slam) / 0.05f) : 1f);
                o[i] += 0.45f * env * lp.LP(motor) + hiss * hs.HP(g.R.N());
            }
            for (int k = 0; k < 3; k++) Creak(g, o, g.R.Range(0.4f, 1.6f), 0.3f, g.R.Range(20f, 40f), 0.35f, 0.55f);
            g.Thump(o, slam, 50f, 34f, 0.15f, 0.002f, 0.3f, 1.2f);
            g.Noise(o, slam, 0.001f, 0.05f, 0.8f, 0, 1500f);
            for (int k = 0; k < 6; k++) g.Partial(o, slam, 180f * new[] { 1f, 1.59f, 2.14f, 2.92f, 4.06f, 5.4f }[k], 0.8f / (k + 1), 0.2f / (k + 1));
            g.Noise(o, slam + 0.15f, 0.05f, 0.3f, 0.3f, 2, 3000f, 0.7f, 0.9f);
            return SynthDsp.Finish(g.Saturate(o, 1.2f), 0.92f, 1f, 100f, g.Fs);
        }
        static float[] Breaker(SfxGen g)
        {
            var o = g.New(0.65f); bool off = g.Variant == 1;
            g.Strike(o, 0, 1f, new[] { g.R.Range(380f, 450f), g.R.Range(900f, 1000f), g.R.Range(2100f, 2400f) }, new[] { 0.06f, 0.05f, 0.03f }, new[] { 1f, 0.6f, 0.4f }, 1.5f, 6000f);
            g.Thump(o, 0, 130f, 110f, 0.02f, 0.001f, 0.03f, 0.6f);
            var hp = new Svf(g.Fs, 1500f, 0.7f); int s0 = g.S(0.01f), n = g.S(off ? 0.14f : 0.08f); bool on = true; int left = 0;
            for (int i = 0; i < n; i++) { if (left-- <= 0) { left = g.S(0.0005f); on = g.R.Chance(0.5f); } float t = (float)i / g.Fs; o[s0 + i] += (on ? 0.4f : 0f) * hp.HP(g.R.N()) * (float)Math.Exp(-t / 0.03f); }
            if (!off) { int h0 = g.S(0.02f), hn = g.S(0.5f); for (int i = 0; i < hn && h0 + i < o.Length; i++) { float t = (float)i / g.Fs; float w = SynthDsp.TwoPi * 50f * t; o[h0 + i] += 0.2f * ((float)Math.Sin(w) + 0.5f * (float)Math.Sin(2 * w) + 0.3f * (float)Math.Sin(3 * w)) * SynthDsp.AD(t, 0.01f, 0.25f); } }
            return SynthDsp.Finish(o, 0.9f, 0.05f, 40f, g.Fs);
        }
        static float[] LightSwitch(SfxGen g)
        {
            var o = g.New(0.08f); float p = g.Variant == 0 ? 1f : 0.85f;
            g.Strike(o, 0, 1f, new[] { 2200f * p, 3400f * p }, new[] { 0.02f, 0.015f }, new[] { 0.6f, 0.4f }, 0.5f, 7000f);
            g.Thump(o, 0, 210f, 190f, 0.01f, 0.0005f, 0.01f, 0.4f);
            return SynthDsp.Finish(o, 0.6f, 0.05f, 10f, g.Fs);
        }
        static float[] FluorescentBuzz(SfxGen g)
        {
            // exactly 3 s: 120 Hz * 3 = 360 whole cycles, 2400 Hz * 3 = 7200 cycles, 1/3 Hz AM = 1 cycle -> seamless
            var o = g.New(3f); int n = o.Length;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / g.Fs; double s = 0;
                for (int h = 1; h <= 8; h++) s += Math.Sin(2 * Math.PI * 120 * h * t + h * 0.7) / Math.Pow(h, 1.2);
                s = s * (1 + 0.08 * Math.Sin(2 * Math.PI * t / 3.0)) + 0.04 * Math.Sin(2 * Math.PI * 2400 * t);
                o[i] = (float)(0.3 * s);
            }
            for (int k = 0; k < 4; k++) g.Noise(o, g.R.Range(0.2f, 2.6f), 0.0005f, g.R.Range(0.004f, 0.015f), 0.25f, 2, 2000f);
            return SynthDsp.FinishLoop(o, 0.5f);
        }
        static float[] StaticNoise(SfxGen g)
        {
            float loop = 3f, xf = 0.3f; var o = g.New(loop + xf); var bp = new Svf(g.Fs, 1800f, 0.5f); float wp = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = (float)i / g.Fs; float swirl = 0.75f + 0.25f * (float)Math.Sin(SynthDsp.TwoPi * 0.7f * t);
                wp += SynthDsp.TwoPi * (1100f + 200f * (float)Math.Sin(SynthDsp.TwoPi * 0.23f * t)) / g.Fs;
                o[i] = 0.3f * swirl * bp.BP(g.R.N()) * 2f + 0.03f * (float)Math.Sin(wp);
            }
            float tt = 0; while ((tt += g.R.Exp(1f / 20f)) < loop + xf - 0.01f) g.Noise(o, tt, 0.0001f, g.R.Range(0.0002f, 0.001f), g.R.Range(0.2f, 1f), 2, 2500f);
            return SynthDsp.FinishLoop(SynthDsp.MakeLoop(o, g.S(loop)), 0.6f);
        }
        static float[] ElevatorRumble(SfxGen g)
        {
            float loop = 4f, xf = 0.5f; var o = g.New(loop + xf); var lp = new Svf(g.Fs, 180f, 0.7f); float brown = 0, ph = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = (float)i / g.Fs; brown = brown * 0.995f + g.R.N() * 0.05f;
                ph += SynthDsp.TwoPi * 220f * (1f + 0.01f * (float)Math.Sin(SynthDsp.TwoPi * 3f * t)) / g.Fs;
                o[i] = 0.8f * lp.LP(brown) * 4f + 0.3f * (float)Math.Sin(SynthDsp.TwoPi * 42f * t) * (1f + 0.2f * (float)Math.Sin(SynthDsp.TwoPi * 0.5f * t)) + 0.05f * ((float)Math.Sin(ph) + 0.4f * (float)Math.Sin(2 * ph));
            }
            for (int k = 0; k < 2; k++) { float at = g.R.Range(0.3f, loop - 0.4f); g.Strike(o, at, 0.3f, new[] { 90f, 210f, 1800f }, new[] { 0.12f, 0.1f, 0.03f }, new[] { 1f, 0.6f, 0.15f }, 0f); }
            return SynthDsp.FinishLoop(SynthDsp.MakeLoop(o, g.S(loop)), 0.7f);
        }
        static float[] CrowdMurmur(SfxGen g)
        {
            float loop = 6f, xf = 0.6f; var o = g.New(loop + xf); int total = o.Length;
            for (int v = 0; v < 9; v++)
            {
                float f0 = g.R.Range(95f, 240f); float fs = f0 < 160f ? 1f : 1.15f; float vAmp = g.R.Range(0.3f, 1f); var buf = new float[total];
                float t = g.R.Range(0f, 0.4f);
                while (t < loop + xf)
                {
                    if (g.R.Chance(0.2f)) { t += g.R.Range(0.2f, 0.6f); continue; }
                    float dur = g.R.Range(0.12f, 0.25f); var va = VowelFormants[g.R.R(7)]; var vb = VowelFormants[g.R.R(7)];
                    float fS = f0 * g.R.Range(0.9f, 1.1f), a = g.R.Range(0.4f, 1f);
                    Voice(g, buf, g.S(t), g.S(dur), tt => fS * (1f - 0.1f * tt / dur), tt => a * (float)Math.Pow(Math.Sin(Math.PI * SynthDsp.Clamp01(tt / dur)), 0.8), va, vb, fs, 0.15f);
                    t += dur + g.R.Range(0.0f, 0.05f);
                }
                var d = new Svf(g.Fs, g.R.Range(1200f, 3000f), 0.7f);
                for (int i = 0; i < total; i++) o[i] += vAmp * d.LP(buf[i]);
            }
            g.LowPass(o, 2800f); g.Reverb(o, 0.4f, 1.2f);
            return SynthDsp.FinishLoop(SynthDsp.MakeLoop(o, g.S(loop)), 0.6f);
        }
    }
}
