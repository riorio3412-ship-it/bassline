using System;

namespace BL23.Game.Audio
{
    /// <summary>Deterministic xorshift RNG for procedural audio (independent of BL23.Sim.Rng so audio never consumes sim randomness).</summary>
    public struct SynthRng
    {
        uint _s;
        public SynthRng(uint seed) { _s = seed == 0 ? 0x9E3779B9u : seed; Next(); Next(); }
        public static SynthRng FromString(string s, int salt = 0)
        {
            uint h = 2166136261u;
            if (s != null) for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619u; }
            h ^= (uint)salt * 0x85EBCA6Bu; h ^= h >> 13; h *= 0xC2B2AE35u; h ^= h >> 16;
            return new SynthRng(h);
        }
        public uint Next() { uint x = _s; x ^= x << 13; x ^= x >> 17; x ^= x << 5; _s = x; return x; }
        /// <summary>[0,1)</summary>
        public float F() => (Next() >> 8) * (1f / 16777216f);
        /// <summary>[-1,1)</summary>
        public float N() => F() * 2f - 1f;
        public float Range(float a, float b) => a + (b - a) * F();
        public int R(int n) => n <= 1 ? 0 : (int)(Next() % (uint)n);
        public bool Chance(float p) => F() < p;
        /// <summary>Exponentially distributed value with the given mean.</summary>
        public float Exp(float mean) => -mean * (float)Math.Log(1.0 - F() * 0.999999);
    }

    /// <summary>Topology-preserving state-variable filter (Zavalishin). Stable for any cutoff below Nyquist; cheap per-sample retune.</summary>
    public sealed class Svf
    {
        float _ic1, _ic2, _a1, _a2, _a3, _k;
        readonly float _fs;
        public float Low, Band, High;
        public Svf(float sampleRate, float cutoff, float q) { _fs = sampleRate; Set(cutoff, q); }
        public void Set(float cutoff, float q)
        {
            float fc = Math.Max(10f, Math.Min(cutoff, _fs * 0.49f));
            float g = (float)Math.Tan(Math.PI * fc / _fs);
            _k = 1f / Math.Max(0.05f, q);
            _a1 = 1f / (1f + g * (g + _k)); _a2 = g * _a1; _a3 = g * _a2;
        }
        public float Process(float x)
        {
            float v3 = x - _ic2;
            float v1 = _a1 * _ic1 + _a2 * v3;
            float v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
            _ic1 = 2f * v1 - _ic1; _ic2 = 2f * v2 - _ic2;
            Low = v2; Band = v1; High = x - _k * v1 - v2;
            return v2;
        }
        public float LP(float x) { Process(x); return Low; }
        public float BP(float x) { Process(x); return Band; }
        public float HP(float x) { Process(x); return High; }
        public void Reset() { _ic1 = _ic2 = 0; }
    }

    /// <summary>Two-pole resonator (damped sinusoid). Excite with an impulse or noise; decay given as T60 seconds.</summary>
    public struct Mode
    {
        float _b1, _b2, _y1, _y2, _g;
        public Mode(float freq, float t60, float gain, float fs)
        {
            float f = Math.Min(freq, fs * 0.45f);
            double r = Math.Exp(-6.907755 / (Math.Max(0.002f, t60) * fs));
            double w = 2 * Math.PI * f / fs;
            _b1 = (float)(2 * r * Math.Cos(w)); _b2 = (float)(-r * r);
            _g = gain * (float)Math.Sin(w); // normalise so an impulse gives ~gain amplitude
            _y1 = _y2 = 0;
        }
        public float Tick(float x)
        {
            float y = _g * x + _b1 * _y1 + _b2 * _y2;
            _y2 = _y1; _y1 = y; return y;
        }
    }

    /// <summary>Small mono Schroeder reverb (4 combs + 2 allpasses) for stingers and room tails.</summary>
    public sealed class MiniReverb
    {
        readonly float[][] _comb; readonly int[] _ci; readonly float[] _cf; readonly float[] _lp;
        readonly float[][] _ap; readonly int[] _ai;
        readonly float _damp;
        public MiniReverb(float fs, float size = 1f, float feedback = 0.78f, float damp = 0.3f)
        {
            int[] cl = { 1116, 1188, 1277, 1356 }; int[] al = { 556, 441 };
            float sc = fs / 44100f * Math.Max(0.2f, size);
            _comb = new float[4][]; _ci = new int[4]; _cf = new float[4]; _lp = new float[4];
            for (int i = 0; i < 4; i++) { _comb[i] = new float[Math.Max(8, (int)(cl[i] * sc))]; _cf[i] = feedback; }
            _ap = new float[2][]; _ai = new int[2];
            for (int i = 0; i < 2; i++) _ap[i] = new float[Math.Max(8, (int)(al[i] * fs / 44100f))];
            _damp = damp;
        }
        public float Process(float x)
        {
            float o = 0;
            for (int i = 0; i < 4; i++)
            {
                var b = _comb[i]; int k = _ci[i]; float y = b[k];
                _lp[i] = y * (1 - _damp) + _lp[i] * _damp;
                b[k] = x * 0.25f + _lp[i] * _cf[i];
                _ci[i] = (k + 1) % b.Length; o += y;
            }
            for (int i = 0; i < 2; i++)
            {
                var b = _ap[i]; int k = _ai[i]; float bo = b[k];
                float y = -o + bo; b[k] = o + bo * 0.5f; _ai[i] = (k + 1) % b.Length; o = y;
            }
            return o;
        }
    }

    public static class SynthDsp
    {
        public const float TwoPi = (float)(Math.PI * 2);
        public static int Len(float seconds, int fs) => Math.Max(1, (int)(seconds * fs));

        public static float Semi(float semitones) => (float)Math.Pow(2, semitones / 12.0);
        public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        public static float Smooth(float t) { t = Clamp01(t); return t * t * (3 - 2 * t); }

        /// <summary>Attack/exponential-decay envelope value at time t.</summary>
        public static float AD(float t, float attack, float decayTau)
        {
            if (t < 0) return 0;
            if (t < attack) return attack <= 0 ? 1 : t / attack;
            return (float)Math.Exp(-(t - attack) / Math.Max(1e-4f, decayTau));
        }

        /// <summary>Add src into dst at offset (samples) with gain; clipped to dst length.</summary>
        public static void Mix(float[] dst, float[] src, int offset, float gain = 1f)
        {
            for (int i = 0; i < src.Length; i++) { int j = i + offset; if (j < 0) continue; if (j >= dst.Length) break; dst[j] += src[i] * gain; }
        }

        public static float Peak(float[] x) { float p = 0; for (int i = 0; i < x.Length; i++) { float a = Math.Abs(x[i]); if (a > p) p = a; } return p; }

        /// <summary>Remove DC, normalise to peak, fade the edges (ms) to avoid clicks. NaN/Inf are zeroed.</summary>
        public static float[] Finish(float[] x, float peak = 0.9f, float fadeInMs = 0.5f, float fadeOutMs = 8f, int fs = 44100, bool removeDc = true)
        {
            for (int i = 0; i < x.Length; i++) if (float.IsNaN(x[i]) || float.IsInfinity(x[i])) x[i] = 0;
            if (removeDc)
            {
                // one-pole DC blocker
                float xm1 = 0, ym1 = 0; const float R = 0.9995f;
                for (int i = 0; i < x.Length; i++) { float y = x[i] - xm1 + R * ym1; xm1 = x[i]; ym1 = y; x[i] = y; }
            }
            int fi = Math.Min(x.Length / 2, (int)(fadeInMs * 0.001f * fs)), fo = Math.Min(x.Length / 2, (int)(fadeOutMs * 0.001f * fs));
            for (int i = 0; i < fi; i++) x[i] *= (float)i / fi;
            for (int i = 0; i < fo; i++) x[x.Length - 1 - i] *= (float)i / fo;
            float p = Peak(x);
            if (p > 1e-6f && peak > 0) { float g = peak / p; for (int i = 0; i < x.Length; i++) x[i] *= g; }
            return x;
        }

        /// <summary>Make a seamless loop: renders are generated with extra 'xfade' samples at the end which are cross-faded (equal power) into the start.</summary>
        public static float[] MakeLoop(float[] src, int loopLen)
        {
            int xf = src.Length - loopLen; if (xf <= 0) return src;
            var o = new float[loopLen];
            Array.Copy(src, o, loopLen);
            for (int i = 0; i < xf && i < loopLen; i++)
            {
                float t = (i + 0.5f) / xf;
                float a = (float)Math.Sin(t * Math.PI * 0.5), b = (float)Math.Cos(t * Math.PI * 0.5);
                o[i] = src[i] * a + src[loopLen + i] * b;
            }
            return o;
        }

        /// <summary>Normalise loop to peak without edge fades (keeps continuity).</summary>
        public static float[] FinishLoop(float[] x, float peak = 0.8f)
        {
            for (int i = 0; i < x.Length; i++) if (float.IsNaN(x[i]) || float.IsInfinity(x[i])) x[i] = 0;
            double m = 0; for (int i = 0; i < x.Length; i++) m += x[i]; m /= x.Length;
            for (int i = 0; i < x.Length; i++) x[i] -= (float)m;
            float p = Peak(x); if (p > 1e-6f) { float g = peak / p; for (int i = 0; i < x.Length; i++) x[i] *= g; }
            return x;
        }

        /// <summary>Soft clip (tanh-ish) for saturation.</summary>
        public static float Sat(float x) { if (x > 3) return 1; if (x < -3) return -1; float x2 = x * x; return x * (27 + x2) / (27 + 9 * x2); }

        /// <summary>Band-limited-ish sawtooth via PolyBLEP.</summary>
        public static float Saw(ref float phase, float freq, float fs)
        {
            float dt = freq / fs; phase += dt; if (phase >= 1) phase -= 1;
            float v = 2 * phase - 1;
            if (phase < dt) { float t = phase / dt; v -= t + t - t * t - 1; }
            else if (phase > 1 - dt) { float t = (phase - 1) / dt; v -= t * t + t + t + 1; }
            return v;
        }
    }
}
