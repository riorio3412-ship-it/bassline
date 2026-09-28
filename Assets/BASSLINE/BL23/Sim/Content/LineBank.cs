using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace BL23.Sim
{
    /// <summary>Per-character line variants keyed by situation. Content lives in Lines_*.cs (static partial Init_* methods).</summary>
    public static partial class LineBank
    {
        public sealed class LineSet { public string[] P = new string[0]; public string[] C = new string[0]; }
        static Dictionary<string, Dictionary<string, LineSet>> _d;

        static string[] P(params string[] s) => s;
        static string[] C(params string[] s) => s;

        static void Add(string actor, string key, string[] polite, string[] casual = null)
        {
            if (!_d.TryGetValue(actor, out var m)) _d[actor] = m = new Dictionary<string, LineSet>();
            if (!m.TryGetValue(key, out var set)) m[key] = set = new LineSet();
            if (polite != null && polite.Length > 0) set.P = set.P.Concat(polite).ToArray();
            if (casual != null && casual.Length > 0) set.C = set.C.Concat(casual).ToArray();
        }
        // convenience for casual-only characters
        static void AddC(string actor, string key, params string[] casual) => Add(actor, key, null, casual);

        static void Ensure()
        {
            if (_d != null) return;
            _d = new Dictionary<string, Dictionary<string, LineSet>>();
            foreach (var m in typeof(LineBank).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Where(m => m.Name.StartsWith("Init_") && m.GetParameters().Length == 0).OrderBy(m => m.Name))
            {
                try { m.Invoke(null, null); } catch (Exception) { /* a broken content file must not break the game */ }
            }
        }

        public static bool Has(string actor, string key) { Ensure(); return _d.TryGetValue(actor, out var m) && m.ContainsKey(key); }
        public static IEnumerable<string> Keys(string actor) { Ensure(); return _d.TryGetValue(actor, out var m) ? m.Keys : Enumerable.Empty<string>(); }
        public static int Count { get { Ensure(); return _d.Values.Sum(m => m.Values.Sum(s => s.P.Length + s.C.Length)); } }

        /// <summary>Picks a raw template. casual=true prefers C variants. Falls back: other register → ANY → key itself.</summary>
        public static string Raw(string actor, string key, bool casual, Rng rng)
        {
            Ensure();
            string[] pick = null;
            if (_d.TryGetValue(actor, out var m) && m.TryGetValue(key, out var set))
                pick = casual ? (set.C.Length > 0 ? set.C : set.P) : (set.P.Length > 0 ? set.P : set.C);
            if ((pick == null || pick.Length == 0) && _d.TryGetValue("ANY", out var any) && any.TryGetValue(key, out var aset))
                pick = casual ? (aset.C.Length > 0 ? aset.C : aset.P) : (aset.P.Length > 0 ? aset.P : aset.C);
            if (pick == null || pick.Length == 0) return null;
            return pick.Length == 1 || rng == null ? pick[0] : pick[rng.R(pick.Length)];
        }

        /// <summary>Renders a template with slot values and Korean particle selection.</summary>
        public static string Render(string template, IDictionary<string, string> slots, bool casualToAddressee)
        {
            if (template == null) return "";
            var s = RenderRaw(template, slots, casualToAddressee);
            // tidy honorific/noun duplication from mixed authoring conventions ("진우 씨 씨", "소리 소리")
            s = s.Replace("씨 씨", "씨").Replace("님 씨", "님").Replace("씨 님", "님").Replace("님 님", "님").Replace("소리 소리", "소리");
            return TimeJoins(s);
        }

        static string RenderRaw(string template, IDictionary<string, string> slots, bool casualToAddressee)
        {
            return Regex.Replace(template, @"\{([a-z!]+)(?::([가-힣]+))?\}", m =>
            {
                string name = m.Groups[1].Value, particle = m.Groups[2].Success ? m.Groups[2].Value : null;
                string val = slots != null && slots.TryGetValue(name, out var v) ? v : "";
                if (string.IsNullOrEmpty(val)) val = name == "you" ? "" : "…";
                if (particle == null) return val;
                if (particle == "아") { if (!casualToAddressee || val.EndsWith("씨") || val.EndsWith("님")) return val; return val + (Batchim(val) ? "아" : "야"); }
                return val + Josa(val, particle);
            });
        }

        public static bool Batchim(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            char c = word[word.Length - 1];
            if (c >= 0xAC00 && c <= 0xD7A3) return (c - 0xAC00) % 28 != 0;
            if (char.IsDigit(c)) return "013678".IndexOf(c) >= 0;
            return false;
        }
        static bool RieulBatchim(string word) { char c = word[word.Length - 1]; return c >= 0xAC00 && c <= 0xD7A3 && (c - 0xAC00) % 28 == 8; }

        static readonly System.Text.RegularExpressions.Regex _pair = new System.Text.RegularExpressions.Regex(@"([가-힣A-Za-z0-9]+)(은\(는\)|는\(은\)|이\(가\)|가\(이\)|을\(를\)|를\(을\)|와\(과\)|과\(와\)|\(으\)로|이\(야\))");
        /// <summary>Spoken times already carry their own "around" (쯤 / 조금 넘어 / 조금 전): templates that add "쯤" or "에" after
        /// {time} would double it ("4시쯤쯤", "4시 조금 넘어에", "4시 조금 넘어서이었고요") — smooth those joins. Also joins sound nouns ("발소리").</summary>
        public static string TimeJoins(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.IndexOf("쯤쯤", StringComparison.Ordinal) >= 0) s = s.Replace("쯤쯤", "쯤");
            if (s.IndexOf("조금 전쯤", StringComparison.Ordinal) >= 0) s = s.Replace("조금 전쯤", "조금 전");
            if (s.IndexOf("전후쯤", StringComparison.Ordinal) >= 0) s = s.Replace("전후쯤", "전후");
            if (s.IndexOf("조금 넘어", StringComparison.Ordinal) >= 0)
                s = s.Replace("조금 넘어쯤", "조금 넘어서").Replace("조금 넘어에는", "조금 넘어서는").Replace("조금 넘어엔", "조금 넘어서는").Replace("조금 넘어에", "조금 넘어서")
                     .Replace("조금 넘어까지", "조금 넘어서까지").Replace("조금 넘어부터", "조금 넘어서부터").Replace("조금 넘어였", "조금 넘어서였").Replace("조금 넘어예요", "조금 넘어서예요")
                     .Replace("조금 넘어야", "조금 넘어서야").Replace("조금 넘어라", "조금 넘어서라").Replace("조금 넘어고", "조금 넘어서고").Replace("조금 넘어 무렵", "조금 넘어서").Replace("조금 넘어 사이", "조금 넘어서 사이");
            if (s.IndexOf("조금 지나서", StringComparison.Ordinal) >= 0) s = s.Replace("조금 지나서쯤", "조금 지나서");
            // "넘어서/지나서" + a join the template added: "넘어서에" → "넘어서", "넘어서이었고요" → "넘어서였고요", "넘어서이요" → "넘어서요", "넘어서이면" → "넘어서라면"
            if (s.IndexOf("서에", StringComparison.Ordinal) >= 0 || s.IndexOf("서엔", StringComparison.Ordinal) >= 0) s = s.Replace("넘어서에는", "넘어서는").Replace("넘어서엔", "넘어서는").Replace("넘어서에", "넘어서").Replace("지나서에는", "지나서는").Replace("지나서엔", "지나서는").Replace("지나서에", "지나서");
            if (s.IndexOf("서이", StringComparison.Ordinal) >= 0) s = _seoCopula.Replace(s, m => m.Groups[1].Value + (m.Groups[2].Value == "었" ? "였" : m.Groups[2].Value == "에요" ? "예요" : m.Groups[2].Value == "면" ? "라면" : m.Groups[2].Value));
            if (s.IndexOf("조금 넘어", StringComparison.Ordinal) >= 0) s = _nwEnd.Replace(s, "조금 넘어서");
            if (s.IndexOf("까지쯤", StringComparison.Ordinal) >= 0) s = s.Replace("까지쯤", "까지");
            if (s.IndexOf(" 사이", StringComparison.Ordinal) >= 0)
            {
                s = s.Replace(" 사이 사이", " 사이").Replace(" 사이까지", " 사이");
                // "A부터 B까지" said as one estimate ("숨진 건 A부터 B까지 사이예요") → "A부터 B 사이예요"
                s = _rangeAsPoint.Replace(s, "$1 사이");
                // one time + "사이" (a range that collapsed to a single time): "4시쯤 사이예요" → "4시쯤이에요"; a real range ("A부터/에서 B쯤 사이") is kept
                s = _singleSai.Replace(s, m =>
                {
                    string tail = m.Groups[1].Value == "조금 넘어" ? "조금 넘어서" : m.Groups[1].Value, suf = m.Groups[2].Value;
                    return tail + (suf == "예요" ? Josa(tail, "이에요") : suf == "야" ? Josa(tail, "이야") : suf);
                });
                s = s.Replace("까지 사이", "까지");
            }
            else if (s.IndexOf("까지", StringComparison.Ordinal) >= 0) s = _rangeAsPoint.Replace(s, "$1 사이");
            if (s.IndexOf("무렵", StringComparison.Ordinal) >= 0) s = s.Replace("무렵쯤", "무렵").Replace("쯤 무렵", "쯤").Replace("조금 전 무렵", "조금 전").Replace("넘어서 무렵", "넘어서").Replace("지나서 무렵", "지나서").Replace("전후 무렵", "전후");
            if (s.IndexOf("직전쯤", StringComparison.Ordinal) >= 0) s = s.Replace("직전쯤", "직전");
            // sound nouns are one word — "발소리", "말소리", "웃음소리", "울음소리" (SoundText gives "발", "말"… and templates add " 소리"); English fallbacks get a Korean word
            if (s.IndexOf(" 소리", StringComparison.Ordinal) >= 0)
                s = _soundWord.Replace(s, "$1소리").Replace("Rain 소리", "빗소리").Replace("Static 소리", "지직거리는 잡음").Replace("Clock 소리", "시계 종소리").Replace("Announcement 소리", "안내 방송 소리");
            return s;
        }

        // one spoken time as ClockFmt.Vague says it: "오후 4시쯤", "오후 4시 반쯤", "오후 4시 조금 넘어(서)", "오후 5시 조금 전" (also "조금 지나서", "전후")
        const string SpokenTime = @"(?:(?:어제|그저께|\d+일째) )?(?:(?:새벽|아침|오전|낮|오후|저녁|밤) )?\d{1,2}시(?: 반쯤| 반|쯤| 조금 넘어서| 조금 넘어| 조금 지나서| 조금 전| 전후)?";
        static readonly Regex _seoCopula = new Regex(@"(넘어서|지나서)이(었|에요|요|면|야|고|라|죠|니|던)");
        static readonly Regex _nwEnd = new Regex(@"조금 넘어(?=[.?!|]|$)");
        static readonly Regex _rangeAsPoint = new Regex(@"(?<=\d시(?: 반쯤| 반|쯤| 조금 넘어서| 조금 지나서| 조금 전| 전후)?)(부터 " + SpokenTime + @")까지(?: 사이)?(?=예요|이에요|야|이야|입니다|였|이었|에(?!서)|요|이다|다[ .,!?…|]|다$)");
        static readonly Regex _singleSai = new Regex(@"(?<!(?:부터|에서) (?:(?:어제|그저께|\d+일째) )?(?:(?:새벽|아침|오전|낮|오후|저녁|밤) )?\d{1,2}시(?: 반)? ?)(쯤|조금 전|조금 넘어서|조금 넘어|조금 지나서|전후) 사이(예요|야|입니다)?");
        static readonly Regex _soundWord = new Regex(@"(?<![가-힣])(발|말|웃음|울음|숨) 소리");

        /// <summary>Resolves code-built "은(는)"-style pairs to the right particle for the preceding word.</summary>
        public static string FixParticles(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = TimeJoins(s);
            if (s.IndexOf('(') < 0) return s;
            return _pair.Replace(s, m => { string w = m.Groups[1].Value, p = m.Groups[2].Value; string first = p.StartsWith("(") ? "로" : p.Substring(0, p.IndexOf('(')); return w + Josa(w, first); });
        }

        public static string Josa(string word, string p)
        {
            bool b = Batchim(word);
            switch (p)
            {
                case "이": case "가": return b ? "이" : "가";
                case "을": case "를": return b ? "을" : "를";
                case "은": case "는": return b ? "은" : "는";
                case "와": case "과": return b ? "과" : "와";
                case "로": case "으로": return b && !RieulBatchim(word) ? "으로" : "로";
                case "이랑": case "랑": return b ? "이랑" : "랑";
                case "이나": case "나": return b ? "이나" : "나";
                case "이야": case "야": return b ? "이야" : "야";
                case "이에요": case "예요": return b ? "이에요" : "예요";
                case "이라": case "라": return b ? "이라" : "라";
                case "이었": case "였": return b ? "이었" : "였";
                case "이고": case "고": return b ? "이고" : "고";
                case "으로서": return b && !RieulBatchim(word) ? "으로서" : "로서";
                case "아": return b ? "아" : "야";
            }
            return p;
        }

        /// <summary>Split a rendered line into subtitle pages.</summary>
        public static string[] Pages(string line) => (line ?? "").Split('|').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
    }
}
