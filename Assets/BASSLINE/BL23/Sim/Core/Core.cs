using System;
using System.Collections.Generic;

namespace BL23.Sim
{
    /// <summary>Floor-plane position. x = east, z = north, meters. Floor index: -1 basement, 0 ground, 1 upper.</summary>
    [Serializable]
    public struct P3 : IEquatable<P3>
    {
        public int f; public float x; public float z;
        public P3(int floor, float x, float z) { f = floor; this.x = x; this.z = z; }
        public float DistXZ(P3 o) { float dx = o.x - x, dz = o.z - z; return (float)Math.Sqrt(dx * dx + dz * dz); }
        public float Dist(P3 o) { float d = DistXZ(o); return o.f == f ? d : d + 40f * Math.Abs(o.f - f); }
        public bool Equals(P3 o) => f == o.f && x == o.x && z == o.z;
        public override bool Equals(object obj) => obj is P3 o && Equals(o);
        public override int GetHashCode() => f * 7919 + (int)(x * 131) + (int)(z * 17);
        public override string ToString() => $"({f}:{x:0.0},{z:0.0})";
        public static P3 Lerp(P3 a, P3 b, float t) => new P3(t < 0.5f ? a.f : b.f, a.x + (b.x - a.x) * t, a.z + (b.z - a.z) * t);
    }

    [Serializable]
    public struct RectF
    {
        public float x0, z0, x1, z1;
        public RectF(float x0, float z0, float x1, float z1) { this.x0 = Math.Min(x0, x1); this.z0 = Math.Min(z0, z1); this.x1 = Math.Max(x0, x1); this.z1 = Math.Max(z0, z1); }
        public float W => x1 - x0; public float D => z1 - z0; public float CX => (x0 + x1) * 0.5f; public float CZ => (z0 + z1) * 0.5f;
        public float Area => W * D;
        public bool Contains(float x, float z) => x >= x0 && x <= x1 && z >= z0 && z <= z1;
        public bool Overlaps(RectF o) => x0 < o.x1 && o.x0 < x1 && z0 < o.z1 && o.z0 < z1;
        public RectF Inset(float d) => new RectF(x0 + d, z0 + d, x1 - d, z1 - d);
        public override string ToString() => $"[{x0:0.#},{z0:0.#}-{x1:0.#},{z1:0.#}]";
    }

    /// <summary>PCG32 deterministic random stream. Serializable state; never uses GetHashCode for persistence.</summary>
    [Serializable]
    public sealed class Rng
    {
        public ulong S; public ulong I; public long Consumed;
        public Rng() { }
        public Rng(ulong seed, ulong stream) { S = 0; I = (stream << 1) | 1u; NextU(); S += seed; NextU(); Consumed = 0; }
        public uint NextU()
        {
            ulong old = S; S = old * 6364136223846793005UL + I; Consumed++;
            uint xs = (uint)(((old >> 18) ^ old) >> 27); int rot = (int)(old >> 59);
            return (xs >> rot) | (xs << ((-rot) & 31));
        }
        public float F() => (NextU() >> 8) * (1f / 16777216f);
        public double D() => NextU() * (1.0 / 4294967296.0);
        public int R(int maxExclusive) { if (maxExclusive <= 1) { NextU(); return 0; } return (int)(NextU() % (uint)maxExclusive); }
        public int R(int min, int maxExclusive) => min + R(maxExclusive - min);
        public float Range(float a, float b) => a + (b - a) * F();
        public bool Chance(double p) => D() < p;
        public T Pick<T>(IList<T> list) => list[R(list.Count)];
        public void Shuffle<T>(IList<T> list) { for (int i = list.Count - 1; i > 0; i--) { int j = R(i + 1); T t = list[i]; list[i] = list[j]; list[j] = t; } }
        public T Weighted<T>(IList<T> items, Func<T, double> w)
        {
            double total = 0; foreach (var it in items) total += Math.Max(0, w(it));
            if (total <= 0) return items.Count > 0 ? items[R(items.Count)] : default;
            double r = D() * total;
            foreach (var it in items) { r -= Math.Max(0, w(it)); if (r <= 0) return it; }
            return items[items.Count - 1];
        }
        public static ulong Hash(string s) { ulong h = 1469598103934665603UL; foreach (char c in s) { h ^= c; h *= 1099511628211UL; } return h; }
    }

    public enum Stream { Layout, Props, Life, PlanTie, ChapterRule, AbilityAssign, VoteDraw, Presentation, Combat, Perception, Dialogue, Trial,
        Conceal   // (appended) Systems/Concealment.cs: NPC searches of hiding places — never renumber the members above
    }

    [Serializable]
    public sealed class RngSet
    {
        public ulong CampaignSeed;
        public Dictionary<string, Rng> Streams = new Dictionary<string, Rng>();
        public Rng Get(Stream s, int loop)
        {
            string key = s + "#" + loop;
            if (!Streams.TryGetValue(key, out var r)) { r = new Rng(CampaignSeed ^ Rng.Hash(key), (ulong)((int)s + 1) * 2654435761UL + (ulong)loop); Streams[key] = r; }
            return r;
        }
        public Rng Get(Stream s) => Get(s, 0);
    }

    public static class SimTime
    {
        public const int PerSecond = 10;
        public const float Dt = 1f / PerSecond;
    }

    public static class ClockFmt
    {
        public static string HM(double absMin) { int m = (int)Math.Floor(absMin % 1440); if (m < 0) m += 1440; return $"{m / 60:00}:{m % 60:00}"; }
        public static int Day(double absMin) => (int)Math.Floor(absMin / 1440) + 1;
        public static string DayHM(double absMin) => $"{Day(absMin)}일차 {HM(absMin)}";
        public static string Range(double a, double b) => Math.Abs(b - a) < 0.5 ? HM(a) : HM(a) + "~" + HM(b);

        // ---- how people in the house tell time: plainly, the way anyone would say it ("오후 4시 반쯤", "밤 11시 조금 전").
        // Resolution ~10-15 minutes — enough to reason with, never a stopwatch. (The earlier hour-bell counting — "네 번째 종과
        // 다섯 번째 종 사이" — read as a puzzle of its own; the user found it hard to follow.)
        /// <summary>The part of the day for the HUD clock: 새벽, 아침, 아침 식사 때, 오전, 점심 식사 때, 오후, 저녁 식사 때, 저녁, 밤.</summary>
        public static string Period(double absMin)
        {
            int m = (int)Math.Floor(absMin % 1440); if (m < 0) m += 1440;
            if (m < 6 * 60) return "새벽"; if (m < 8 * 60) return "아침"; if (m < 9 * 60) return "아침 식사 때"; if (m < 12 * 60 + 30) return "오전";
            if (m < 13 * 60 + 30) return "점심 식사 때"; if (m < 18 * 60 + 30) return "오후"; if (m < 19 * 60 + 30) return "저녁 식사 때";
            if (m < 21 * 60) return "저녁"; return "밤";
        }
        /// <summary>새벽 / 아침 / 오전 / 낮 / 오후 / 저녁 / 밤 for an hour 0..23 (used in spoken times).</summary>
        static string Part(int h24)
        {
            int h = ((h24 % 24) + 24) % 24;
            return h < 6 ? "새벽" : h < 9 ? "오전" : h < 12 ? "오전" : h < 13 ? "낮" : h < 18 ? "오후" : h < 21 ? "저녁" : "밤";
        }
        static string Hour(int h24) { int h = ((h24 % 24) + 24) % 24; int h12 = h % 12 == 0 ? 12 : h % 12; return h12 + "시"; }
        /// <summary>"오후 4시쯤", "오후 4시 조금 넘어", "오후 4시 반쯤", "오후 5시 조금 전". Prefixed with the day when it isn't today.</summary>
        public static string Vague(double absMin, double now = double.NaN)
        {
            int m = (int)Math.Floor(absMin % 1440); if (m < 0) m += 1440;
            int h = m / 60, mm = m % 60; string when;
            if (mm <= 7) when = $"{Part(h)} {Hour(h)}쯤";
            else if (mm < 22) when = $"{Part(h)} {Hour(h)} 조금 넘어";
            else if (mm <= 38) when = $"{Part(h)} {Hour(h)} 반쯤";
            else if (mm < 53) when = $"{Part(h + 1)} {Hour(h + 1)} 조금 전";
            else when = $"{Part(h + 1)} {Hour(h + 1)}쯤";
            string day = "";
            if (!double.IsNaN(now)) { int d = Day(now) - Day(absMin); day = d == 1 ? "어제 " : d == 2 ? "그저께 " : d > 2 ? $"{Day(absMin)}일째 " : ""; }
            return day + when;
        }
        /// <summary>The clock itself, for the HUD: "7:40".</summary>
        public static string BellShort(double absMin)
        {
            int m = (int)Math.Floor(absMin % 1440); if (m < 0) m += 1440; int h = m / 60, mm = m % 60; int h12 = h % 12 == 0 ? 12 : h % 12;
            return $"{h12}:{mm:00}";
        }
        /// <summary>"오후 4시쯤부터 5시 반쯤까지" (the part of the day said once when it doesn't change).</summary>
        public static string VagueRange(double a, double b, double now = double.NaN)
        {
            if (b < a) { var t = a; a = b; b = t; }
            if (b - a < 12) return Vague((a + b) / 2, now);
            // the day word is said once: both ends on the same day → the second half carries no "어제"
            string va = Vague(a, now), vb = Day(a) == Day(b) ? Vague(b) : Vague(b, now);
            int ha = (int)Math.Floor(a % 1440) / 60, hb = (int)Math.Floor(b % 1440) / 60;
            string pa = Part(ha), pb = Part(hb);
            if (pa == pb && Day(a) == Day(b) && vb.StartsWith(pa + " ")) vb = vb.Substring(pa.Length + 1);
            return va + "부터 " + vb + "까지";
        }

        // ---- clue text: one anchor per card, plain words, 10-minute resolution ("저녁 8시 40분쯤")
        static string DayWord(double t, double now)
        {
            if (double.IsNaN(now)) return "";
            int d = Day(now) - Day(t); return d == 1 ? "어제 " : d == 2 ? "그저께 " : d > 2 ? $"{Math.Max(1, Day(t))}일째 " : "";
        }
        static int MinOfDay(double t) { int m = (int)Math.Floor(t % 1440); if (m < 0) m += 1440; return m; }
        static string HourMin(int m, bool withPart)
        {
            int h = (m / 60) % 24, mm = m % 60;
            string s = Hour(h) + (mm == 0 ? "" : mm == 30 ? " 반" : $" {mm}분");
            return withPart ? Part(h) + " " + s : s;
        }
        /// <summary>One moment, plainly, to the nearest 10 minutes: "저녁 8시 반쯤", "어제 밤 11시 10분쯤".</summary>
        public static string Anchor(double t, double now = double.NaN)
        {
            double r = Math.Round(t / 10) * 10;
            return DayWord(r, now) + HourMin(MinOfDay(r), true) + "쯤";
        }
        /// <summary>A span with the day word and the part of the day said once: "저녁 8시~8시 반쯤", "오후 5시~저녁 7시쯤".</summary>
        public static string AnchorRange(double a, double b, double now = double.NaN)
        {
            if (b < a) { var t = a; a = b; b = t; }
            double ra = Math.Round(a / 10) * 10, rb = Math.Round(b / 10) * 10;
            if (rb - ra < 10) return Anchor((a + b) / 2, now);
            int ma = MinOfDay(ra), mb = MinOfDay(rb);
            string left = DayWord(ra, now) + HourMin(ma, true), right;
            if (Day(ra) == Day(rb)) right = HourMin(mb, Part(ma / 60) != Part(mb / 60));
            else { string db = DayWord(rb, now); right = (db.Length > 0 ? db : double.IsNaN(now) ? "" : "오늘 ") + HourMin(mb, true); }
            return left + "~" + right + "쯤";
        }
        /// <summary>For save slots: "3일째 오후 1:02".</summary>
        public static string Stamp(double t)
        {
            int m = MinOfDay(t); int h = m / 60; int h12 = h % 12 == 0 ? 12 : h % 12;
            return $"{Day(t)}일째 {(h < 12 ? "오전" : "오후")} {h12}:{m % 60:00}";
        }
        /// <summary>A column label: "8시", "8시 반", "8시 10분"; with the part of the day: "저녁 8시".</summary>
        public static string Mark(double t, bool withPart) => HourMin(MinOfDay(Math.Round(t)), withPart);
        /// <summary>A duration, plainly: "잠깐", "15분쯤", "30분쯤", "한 시간쯤", "두세 시간", "반나절", "하루 가까이".</summary>
        public static string VagueSpan(double minutes)
        {
            if (minutes < 8) return "잠깐"; if (minutes < 22) return "15분쯤"; if (minutes < 45) return "30분쯤";
            if (minutes < 100) return "한 시간쯤"; if (minutes < 200) return "두세 시간"; if (minutes < 420) return "반나절"; return "하루 가까이";
        }
    }

    public static class MathX
    {
        public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static double Clamp(double v, double a, double b) => v < a ? a : v > b ? b : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float AngleDeg(float dx, float dz) => (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
        public static float DeltaAngle(float a, float b) { float d = (b - a) % 360f; if (d > 180) d -= 360; if (d < -180) d += 360; return d; }
    }
}
