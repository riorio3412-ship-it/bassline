using System;
using System.Collections.Generic;

namespace BL23.Game.Audio
{
    public enum BabbleOnset { Soft = 0, Plosive = 1, Sibilant = 2, Breathy = 3 }

    /// <summary>One scheduled blip of a line (time from line start, seconds).</summary>
    public struct BabbleEvent
    {
        public float Time; public int Blip; public float Pitch; public float Gain; public int CharIndex;
    }

    /// <summary>Per-actor voice parameters for the babble synthesizer.</summary>
    public sealed class BabbleVoice
    {
        public string ActorId;
        public float F0 = 150f;          // base pitch (Hz)
        public float FormantScale = 1f;  // vocal tract size (female/child > 1)
        public int Timbre;               // 0 soft, 1 reed, 2 breathy, 3 aquarium (the butler's fishbowl head)
        public float Rate = 1f;          // syllables speed multiplier
        public float Breath = 0.08f;
        public float Tilt = 1600f;
    }

    /// <summary>
    /// Danganronpa / Animal-Crossing style babble: one short formant blip per Hangul syllable, vowel taken from the
    /// syllable's medial vowel (ㅏ ㅓ ㅗ ㅜ ㅡ ㅣ ㅐ/ㅔ), onset colour from the initial consonant, shorter blip when there is
    /// a final consonant. Pure C#; <see cref="VoiceBabble"/> plays it in Unity.
    /// </summary>
    public static class BabbleSynth
    {
        public const int Rate = 22050;            // babble needs no more bandwidth; halves memory
        public const int VowelCount = 7;
        public const int BlipCount = VowelCount * 4 * 2;
        public const float BaseSyllablesPerSecond = 13f;

        public static int BlipIndex(int vowel, BabbleOnset onset, bool coda) => ((vowel * 4) + (int)onset) * 2 + (coda ? 1 : 0);

        // medial vowel (jungseong 0..20) -> vowel class (0 a, 1 eo, 2 o, 3 u, 4 eu, 5 i, 6 e)
        static readonly int[] JungToVowel = { 0, 6, 0, 6, 1, 6, 1, 6, 2, 0, 6, 6, 2, 3, 1, 6, 5, 3, 4, 5, 5 };

        public static bool IsHangulSyllable(char c) => c >= '가' && c <= '힣';

        public static void Decompose(char c, out int cho, out int jung, out int jong)
        {
            int idx = c - 0xAC00; cho = idx / 588; jung = (idx % 588) / 28; jong = idx % 28;
        }

        public static BabbleOnset OnsetOfCho(int cho)
        {
            switch (cho)
            {
                case 0: case 1: case 3: case 4: case 7: case 8: case 15: case 16: case 17: return BabbleOnset.Plosive;
                case 9: case 10: case 12: case 13: case 14: return BabbleOnset.Sibilant;
                case 18: return BabbleOnset.Breathy;
                default: return BabbleOnset.Soft; // ㄴ ㄹ ㅁ ㅇ
            }
        }

        public static int VowelOfJung(int jung) => jung >= 0 && jung < JungToVowel.Length ? JungToVowel[jung] : 0;

        /// <summary>Voice from cast data (gender + SpeechStyle.VoicePitch/VoiceRate). pitchOverride/rateOverride &gt; 0 replace the cast values.</summary>
        public static BabbleVoice VoiceFor(string actorId, bool female, float voicePitch, float voiceRate, bool isButler)
        {
            voicePitch = voicePitch > 0 ? voicePitch : 1f; voiceRate = voiceRate > 0 ? voiceRate : 1f;
            var h = SynthRng.FromString(actorId ?? "?", 3);
            var v = new BabbleVoice { ActorId = actorId, Rate = voiceRate };
            v.F0 = (female ? 205f : 112f) * voicePitch;
            v.FormantScale = (female ? 1.14f : 1f) * (1f + (voicePitch - 1f) * 0.35f);
            v.Timbre = isButler ? 3 : h.R(3);
            v.Breath = v.Timbre == 2 ? 0.35f : v.Timbre == 1 ? 0.03f : 0.1f;
            v.Tilt = v.Timbre == 1 ? 3200f : v.Timbre == 2 ? 1200f : 1700f;
            return v;
        }

        /// <summary>Render the full bank (BlipCount clips) for one voice.</summary>
        public static float[][] RenderBank(BabbleVoice v)
        {
            var bank = new float[BlipCount][];
            for (int vw = 0; vw < VowelCount; vw++)
                for (int on = 0; on < 4; on++)
                    for (int cd = 0; cd < 2; cd++)
                        bank[BlipIndex(vw, (BabbleOnset)on, cd == 1)] = RenderBlip(v, vw, (BabbleOnset)on, cd == 1);
            return bank;
        }

        public static float[] RenderBlip(BabbleVoice v, int vowel, BabbleOnset onset, bool coda)
        {
            int fs = Rate;
            float len = coda ? 0.058f : 0.078f; float tail = 0.02f;
            var o = new float[SynthDsp.Len(len + tail, fs)];
            var g = new SfxGen(fs, SynthRng.FromString(v.ActorId ?? "?", vowel * 31 + (int)onset * 7 + (coda ? 1 : 0)), 0);
            float voiceStart = onset == BabbleOnset.Sibilant ? 0.012f : onset == BabbleOnset.Breathy ? 0.008f : onset == BabbleOnset.Plosive ? 0.004f : 0f;
            float attack = onset == BabbleOnset.Soft ? 0.012f : 0.005f;
            // consonant colour
            if (onset == BabbleOnset.Plosive) g.Noise(o, 0f, 0.0005f, 0.004f, 0.35f, 1, 1800f * v.FormantScale, 1.2f);
            else if (onset == BabbleOnset.Sibilant) g.Noise(o, 0f, 0.004f, 0.006f, 0.22f, 2, 4200f * v.FormantScale);
            else if (onset == BabbleOnset.Breathy) g.Noise(o, 0f, 0.003f, 0.006f, 0.25f, 1, 1300f * v.FormantScale, 1f);
            // voiced vowel
            var form = SfxSynth.VowelFormants[vowel];
            float vlen = len - voiceStart; float f0 = v.F0;
            int s0 = SynthDsp.Len(voiceStart, fs), n = SynthDsp.Len(vlen + tail, fs);
            SfxSynth.Voice(g, o, s0, n,
                t => f0 * (1f - 0.05f * t / vlen),
                t => SynthDsp.Clamp01(t / attack) * (t < vlen ? 1f : (float)Math.Exp(-(t - vlen) / 0.006f)) * (coda && t > vlen * 0.7f ? 0.6f : 1f),
                form, form, v.FormantScale, v.Breath, v.Tilt);
            if (v.Timbre == 1) g.Saturate(o, 1.8f);
            if (v.Timbre == 3)
            {
                // the butler's aquarium head: muffled, slightly watery wobble
                g.LowPass(o, 1400f, 0.9f);
                for (int i = 0; i < o.Length; i++) o[i] *= 0.85f + 0.15f * (float)Math.Sin(SynthDsp.TwoPi * 23f * i / fs);
            }
            return SynthDsp.Finish(o, 0.5f, 0.3f, 6f, fs);
        }

        /// <summary>Build the blip timeline for a line. seed makes the jitter deterministic per line.</summary>
        public static List<BabbleEvent> Plan(string text, float rate, uint seed)
        {
            var list = new List<BabbleEvent>();
            if (string.IsNullOrEmpty(text)) return list;
            float sps = BaseSyllablesPerSecond * Math.Max(0.3f, rate), step = 1f / sps;
            var r = new SynthRng(seed | 1u);
            float t = 0f; int sentenceStart = 0; bool wordStart = true; char prev = ' ';
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (IsHangulSyllable(c))
                {
                    Decompose(c, out int cho, out int jung, out int jong);
                    bool skip = !wordStart && r.Chance(0.1f);
                    if (!skip) list.Add(new BabbleEvent { Time = t, Blip = BlipIndex(VowelOfJung(jung), OnsetOfCho(cho), jong != 0), Pitch = 1f + r.Range(-0.035f, 0.035f), Gain = r.Range(0.85f, 1f), CharIndex = i });
                    t += step * (jong != 0 ? 0.95f : 1f); wordStart = false;
                }
                else if (c >= 0x3131 && c <= 0x318E) // compatibility jamo: ㅋㅋ, ㅎㅎ, ㅠㅠ ...
                {
                    BabbleOnset on = c == 'ㅋ' || c == 'ㄱ' || c == 'ㅌ' || c == 'ㅍ' ? BabbleOnset.Plosive : c == 'ㅎ' ? BabbleOnset.Breathy : c == 'ㅅ' || c == 'ㅈ' ? BabbleOnset.Sibilant : BabbleOnset.Soft;
                    int vw = c == 'ㅠ' || c == 'ㅜ' ? 3 : c == 'ㅏ' ? 0 : 4;
                    list.Add(new BabbleEvent { Time = t, Blip = BlipIndex(vw, on, false), Pitch = 1f + r.Range(-0.03f, 0.03f), Gain = 0.8f, CharIndex = i });
                    t += step * 0.8f; wordStart = false;
                }
                else if (char.IsLetterOrDigit(c))
                {
                    // latin letters / digits: one blip per ~2 characters
                    if (wordStart || !char.IsLetterOrDigit(prev) || (i % 2 == 0))
                    {
                        int vw = VowelOfLatin(c, r);
                        list.Add(new BabbleEvent { Time = t, Blip = BlipIndex(vw, "ptkbdgc".IndexOf(char.ToLowerInvariant(c)) >= 0 ? BabbleOnset.Plosive : "sxzj".IndexOf(char.ToLowerInvariant(c)) >= 0 ? BabbleOnset.Sibilant : BabbleOnset.Soft, false), Pitch = 1f + r.Range(-0.03f, 0.03f), Gain = 0.9f, CharIndex = i });
                    }
                    t += step * 0.55f; wordStart = false;
                }
                else
                {
                    switch (c)
                    {
                        case ' ': case '\t': t += step * 0.4f; wordStart = true; break;
                        case '\n': t += 0.25f; wordStart = true; break;
                        case ',': case '、': case ';': t += 0.18f; wordStart = true; break;
                        case '.': case '。': case '!': case '?': case '！': case '？':
                            Intonate(list, sentenceStart, c == '?' || c == '？', c == '!' || c == '！');
                            sentenceStart = list.Count; t += prev == c ? 0.08f : 0.3f; wordStart = true; break;
                        case '…': t += 0.35f; wordStart = true; break;
                        case '~': case '～': t += 0.1f; break;
                        default: break; // quotes, brackets, symbols: silent
                    }
                }
                prev = c;
            }
            Intonate(list, sentenceStart, false, false);
            return list;
        }

        static int VowelOfLatin(char c, SynthRng r)
        {
            switch (char.ToLowerInvariant(c))
            {
                case 'a': return 0; case 'e': return 6; case 'i': case 'y': return 5; case 'o': return 2; case 'u': case 'w': return 3;
                default: return char.IsDigit(c) ? (c - '0') % VowelCount : 4;
            }
        }

        /// <summary>Sentence declination; question rises at the end; exclamation louder and a bit higher.</summary>
        static void Intonate(List<BabbleEvent> list, int from, bool question, bool exclaim)
        {
            int n = list.Count - from; if (n <= 0) return;
            for (int k = 0; k < n; k++)
            {
                var e = list[from + k]; float u = n > 1 ? (float)k / (n - 1) : 0f;
                e.Pitch *= 1f + 0.04f - 0.08f * u;
                if (exclaim) { e.Pitch *= 1.05f; e.Gain *= 1.15f; }
                if (question && k >= n - 2) e.Pitch *= k == n - 1 ? 1.18f : 1.08f;
                list[from + k] = e;
            }
        }

        public static float Duration(List<BabbleEvent> plan) => plan.Count == 0 ? 0f : plan[plan.Count - 1].Time + 0.1f;
    }
}
