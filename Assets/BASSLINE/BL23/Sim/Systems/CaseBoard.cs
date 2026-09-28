using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BL23.Sim
{
    /// <summary>
    /// The notebook's view of the player's clue cards (clues v2-lite). Everything here is a pure read over what the player
    /// knows plus public incident facts (victim, found room, discovery and confirmation clocks) — never the true time of
    /// death, the culprit, or the ledger. Relevance ("사건 단서" vs "기타") and importance (중요, gold ◆) are decided at display
    /// time; nothing about them is stored. Ties break ordinally; no Dictionary enumeration order, no randomness.
    /// </summary>
    public static class CaseBoard
    {
        public enum Cat { Body, Scene, Object, Witness, Record }            // 시신 · 현장 · 물건 · 증언 · 기록
        public sealed class View
        {
            public Evidence Ev; public string Id, Title, Line, Caution, When, KindLabel;
            public Cat Cat; public bool Key, InCase, Hearsay; public List<string> Bullets = new List<string>();
        }
        public sealed class Question { public int Index; public string Ask, Answer, Hint; public bool Firm; public List<string> CardIds = new List<string>(); }
        public sealed class Conflict { public string Who, A, B; public double T0, T1; }

        static readonly string[] CatLabel = { "시신", "현장", "물건", "증언", "기록" };
        public const int TitleMax = 16;

        // ================================================================== context (built once per call)
        internal sealed class Ctx
        {
            public Simulation Sim; public GameState S; public Knowledge K; public Incident Inc; public string Victim;
            public HashSet<string> Victims = new HashSet<string>();
            public List<Evidence> Mine = new List<Evidence>();      // the player's cards this chapter, not hidden
            public HashSet<int> Scene = new HashSet<int>();
            public double W0, W1, Found, Confirm; public (double t0, double t1, bool examined) Known;
            public Evidence Primary;
            public HashSet<string> SheetsInCase;
            public Dictionary<string, string> Tellers = new Dictionary<string, string>();
            public bool Case => Inc != null;
        }

        internal static Ctx Context(Simulation sim)
        {
            var S = sim.S; var c = new Ctx { Sim = sim, S = S, K = S.K(Cast.Player) };
            c.Inc = CaseProgress.Current(S); c.Victim = c.Inc?.Victim;
            foreach (var i in S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Confirmed)) { if (i.Victim != null) c.Victims.Add(i.Victim); if (i.FoundRoom >= 0) c.Scene.Add(i.FoundRoom); }
            c.Mine = c.K.Evidence.Where(e => e.Loop == S.Loop && e.Chapter == S.Chapter && !e.Hidden).ToList();
            var w = Testimony.CaseWindow(S); c.W0 = w.t0; c.W1 = w.t1; c.Found = w.t1; c.Confirm = c.Inc != null ? c.Inc.ConfirmClock : S.Clock;
            c.Primary = c.Victim == null ? null : c.Mine.FirstOrDefault(e => e.Important && e.Subject == c.Victim && e.Direct && !e.Copy);
            var dw = c.Primary?.Props.LastOrDefault(p => p.Kind == PropKind.DeathWindow && p.Value == "exam");
            c.Known = dw != null ? (dw.T0, dw.T1, true) : (c.Found - 150, c.Found, false);
            if (c.Case)
            {
                foreach (var e in c.Mine.Where(e => e.Kind == EvKind.Trace && e.Room >= 0 && MarkNear(e, c.Found - 240, c.Confirm + 10))) c.Scene.Add(e.Room);
                foreach (var p in c.Mine.SelectMany(e => e.Props).Where(p => p.Kind == PropKind.DeathPlace && p.Room >= 0)) c.Scene.Add(p.Room);
            }
            return c;
        }

        /// <summary>The scene: the found room, rooms of the player's marks near the case, and any known place of death.</summary>
        public static HashSet<int> SceneRooms(Simulation sim) => Context(sim).Scene;

        public static (double t0, double t1, bool examined) KnownWindow(Simulation sim) => Context(sim).Known;

        // ================================================================== facts
        public static string FactKey(Prop p)
        {
            if (p == null) return "";
            if (p.Kind == PropKind.Heard) return $"Heard|{p.Value}|{p.Room}|{(long)Math.Floor(p.T0 / 10)}";
            int room = p.Kind == PropKind.Held || p.Kind == PropKind.SawActor ? -9 : p.Room;
            return $"{p.Kind}|{p.A}|{p.B}|{p.Item}|{room}|{(long)Math.Floor(p.T0 / 20)}";
        }
        /// <summary>The same fact at any time (used to widen a witness's repeated account).</summary>
        internal static string FactKeyNoTime(Prop p)
        {
            if (p == null) return "";
            if (p.Kind == PropKind.Heard) return $"Heard|{p.Value}|{p.Room}";
            int room = p.Kind == PropKind.Held || p.Kind == PropKind.SawActor ? -9 : p.Room;
            return $"{p.Kind}|{p.A}|{p.B}|{p.Item}|{room}";
        }

        static bool NamesVictim(Ctx c, Prop p) => p != null && ((p.A != null && c.Victims.Contains(p.A)) || (p.B != null && c.Victims.Contains(p.B)));
        static bool Overlaps(Prop p, double a, double b) => p != null && !(p.T0 <= 0 && p.T1 <= 0) && p.T1 >= a && p.T0 <= b;
        static bool IsSheet(Evidence e) => e.Kind == EvKind.Testimony && e.Root != null && e.Root.StartsWith("wit:", StringComparison.Ordinal);
        static bool Starts(Evidence e, string pre) => e.Root != null && e.Root.StartsWith(pre, StringComparison.Ordinal);

        static readonly SoundKind[] Violent = { SoundKind.Scream, SoundKind.Strike, SoundKind.Struggle, SoundKind.Crash, SoundKind.GlassBreak, SoundKind.Fall, SoundKind.Press, SoundKind.Splash };
        static bool ViolentHeard(Prop p) => p.Kind == PropKind.Heard && p.Value != null && Enum.TryParse(p.Value, out SoundKind k) && Array.IndexOf(Violent, k) >= 0;
        static bool WeaponItem(string type) => ItemCatalog.Get(type)?.IsWeapon == true;

        /// <summary>A witness bullet worth a toast: anything but the speaker's own whereabouts away from the scene.</summary>
        internal static bool NotableBullet(Ctx c, Prop p, string speaker)
        {
            if (p == null) return false;
            if (NamesVictim(c, p)) return true;
            if ((p.Kind == PropKind.AtPlace || p.Kind == PropKind.WithPerson) && p.A == speaker && !c.Scene.Contains(p.Room)) return false;
            return true;
        }

        internal static int BulletScore(Ctx c, Prop p, string speaker)
        {
            if (p == null) return -99;
            int s = 0;
            if (NamesVictim(c, p)) s += 6;
            switch (p.Kind)
            {
                case PropKind.Culprit: s += 8; break;
                case PropKind.Bloodied: case PropKind.Disguised: s += 7; break;
                case PropKind.Injured: case PropKind.ItemMissing: s += 5; break;
                case PropKind.Held: s += WeaponItem(p.Item) ? 6 : 2; break;
                case PropKind.Heard: s += ViolentHeard(p) ? 5 : p.Value != null && p.Value.StartsWith("voice:") ? 3 : 1; break;
                case PropKind.SawActor: s += 3; break;
                case PropKind.Loaned: s += p.Value != null && p.Value.StartsWith("courier:") ? 5 : 2; break;
                case PropKind.AtPlace: case PropKind.WithPerson: s += p.A == speaker ? 0 : 2; break;
                default: s += 1; break;
            }
            if (p.Room >= 0 && c.Scene.Contains(p.Room)) s += 3;
            if (c.Case && Overlaps(p, c.W0, c.W1)) s += 1;
            return s;
        }

        // ================================================================== relevance & importance
        public static bool InCase(Simulation sim, Evidence e) => InCase(Context(sim), e);
        internal static bool InCase(Ctx c, Evidence e)
        {
            var S = c.S;
            if (e == null || !c.Case || e.Hidden || e.Loop != S.Loop || e.Chapter != S.Chapter) return false;
            if (IsKey(c, e)) return true;
            if (IsSheet(e) && e.Props.Count > 0 && e.Props.All(p => (p.Kind == PropKind.AtPlace || p.Kind == PropKind.WithPerson) && p.A == e.Subject && !c.Scene.Contains(p.Room) && !NamesVictim(c, p))) return false;
            if (IsSheet(e)) return CaseSheets(c).Contains(e.Id);
            // a mark older than the case (a day-old footprint) is not part of it, wherever it lies
            if (e.Kind == EvKind.Trace && !MarkNear(e, c.W0 - 240, c.Confirm + 10)) return false;
            if ((Starts(e, "door:") || Starts(e, "furn:")) && !e.Props.Any(p => p.Kind != PropKind.DoorState) && !c.Scene.Contains(e.Room)) return false;
            if ((Starts(e, "invite:") || Starts(e, "hand:") || Starts(e, "gath:") || Starts(e, "envelope:")) && !e.Props.Any(p => NamesVictim(c, p))) return false;
            if (e.Subject != null && c.Victims.Contains(e.Subject)) return true;
            if (e.Props.Any(p => NamesVictim(c, p))) return true;
            if (e.Room >= 0 && c.Scene.Contains(e.Room)) return true;
            if (e.Props.Any(p => p.Room >= 0 && c.Scene.Contains(p.Room))) return true;
            double a = c.W0 - 60, b = c.Confirm + 10;
            return e.Props.Any(p => Overlaps(p, a, b));
        }

        public static bool IsKey(Simulation sim, Evidence e) => IsKey(Context(sim), e);
        internal static bool IsKey(Ctx c, Evidence e)
        {
            if (e == null || e.Hidden) return false;
            if (e.Important) return true;
            foreach (var p in e.Props)
            {
                switch (p.Kind)
                {
                    case PropKind.Staged: case PropKind.TrapSet: case PropKind.Lie: case PropKind.Bloodied: case PropKind.Disguised: case PropKind.Injured: case PropKind.ItemMissing: case PropKind.Culprit:
                        return true;
                    // a wrong clock is gold when it was read around this chapter's case (an alibi can hang on it) — not a clock set
                    // right two days ago. Decided by the reading time against the case, so it only ever turns on once a case exists.
                    case PropKind.ClockOffset: if (p.Value != "0" && p.Value != "-0" && p.Value != null && c.Case && p.T0 >= c.W0 - 720) return true; break;
                    case PropKind.ItemState: if (p.Value == "피 묻음" || p.Value == "세척 흔적" || p.Value == "숨겨짐" || p.Value == "피 묻었다 씻김") return true; break;
                    case PropKind.DeviceRecord: if (c.Case && p.Value != "coverage-partial" && Overlaps(p, c.W0 - 30, c.W1 + 30)) return true; break;
                }
            }
            if (Starts(e, "sealed:") || Starts(e, "ch04:") || Starts(e, "scratch:") || Starts(e, "wet:") || Starts(e, "afterglow:")) return true;
            // a device log read inside the case window is a record of who went where
            if (c.Case && Starts(e, "devlog:") && e.Props.Any(p => Overlaps(p, c.W0 - 30, c.W1 + 30))) return true;
            if (e.Kind == EvKind.Trace)
            {
                string type = TraceType(c, e);
                if (type != null && (type.Contains("Blood") || type.StartsWith("Drag") || type.StartsWith("Struggle") || type.Contains("Fiber"))) return true;   // blood (pools, drops, bloody footprints), drag marks, struggle, thread
                if (type == "Scuff" && e.Props.Any(p => p.Kind == PropKind.TraceAt && p.Value == "몸싸움 흔적")) return true;
                // a mark in the found room — left around the time (old footprints in a busy hall are not the case)
                if (c.Case && e.Room >= 0 && c.Inc.FoundRoom == e.Room && MarkNear(e, c.W0 - 60, c.Confirm + 10)) return true;
            }
            // Witness sheets are 중요 through the hard facts above (blood on someone's clothes, a disguise, an injury, an
            // accusation, a changed story) and through one narrow rule: after the body exam, a witness who saw someone else holding
            // a weapon of the wound's kind around the time of death — only the first sheet to tell it (a second witness of the same
            // sighting adds no second ◆). The broad rules ("someone else at the scene", "a violent sound") stay dropped: at breakfast
            // in the dining room, or in the grand hall everyone crosses, every witness says them — 15 gold sheets a case.
            if (IsSheet(e) && WeaponSeen(c, e) != null) return true;
            return false;
        }

        /// <summary>The wound's family from the body exam (0 when not examined, poisoned or unmarked).</summary>
        static int ExamFamily(Ctx c)
        {
            if (!c.Case || !c.Known.examined || c.Primary == null) return 0;
            var wt = c.Primary.Props.FirstOrDefault(p => p.Kind == PropKind.WeaponType && p.Value != null && p.Value != "None");
            return wt != null && Enum.TryParse(wt.Value, out DamageType d) ? DamageFamily(d) : 0;
        }

        /// <summary>A sheet's "{누군가}이 {흉기}를 들고 있었다" that matches the body: someone other than the speaker (and the victim)
        /// holding a weapon of the wound's family (blunt, blade or cord) within the estimated time of death ±30 min — and this sheet
        /// is the first to have told it (by when the player heard it, so the ◆ never moves once given).</summary>
        internal static Prop WeaponSeen(Ctx c, Evidence e)
        {
            if (e == null || !IsSheet(e)) return null;
            int fam = ExamFamily(c); if (fam < 1 || fam > 3) return null;
            double a = c.Known.t0 - 30, b = c.Known.t1 + 30;
            foreach (var p in e.Props.OrderBy(p => p.T0).ThenBy(p => FactKey(p), StringComparer.Ordinal))
            {
                if (p.Kind != PropKind.Held || p.A == null || p.A == e.Subject || c.Victims.Contains(p.A) || !Overlaps(p, a, b)) continue;
                var def = ItemCatalog.Get(p.Item); if (def == null || !def.IsWeapon || DamageFamily(def.Dmg) != fam) continue;
                if (FirstTeller(c, p, a, b) != e.Id) continue;
                return p;
            }
            return null;
        }

        /// <summary>Of the sheets holding the same weapon sighting, the one the player heard it from first (ties: card id).</summary>
        static string FirstTeller(Ctx c, Prop p, double a, double b)
        {
            string key = "held|" + p.A + "|" + p.Item;
            if (c.Tellers.TryGetValue(key, out var id)) return id;
            string best = null; double bestAt = double.MaxValue;
            foreach (var e in c.Mine.Where(IsSheet).OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                if (!e.Props.Any(x => x.Kind == PropKind.Held && x.A == p.A && x.Item == p.Item && x.A != e.Subject && Overlaps(x, a, b))) continue;
                double at = double.MaxValue;
                foreach (var st in c.K.Statements)
                    if (st.Speaker == e.Subject && st.Prop != null && st.Prop.Kind == PropKind.Held && st.Prop.A == p.A && st.Prop.Item == p.Item && st.Clock < at) at = st.Clock;
                if (at == double.MaxValue) at = e.Acquired;
                if (at < bestAt) { bestAt = at; best = e.Id; }
            }
            c.Tellers[key] = best; return best;
        }

        /// <summary>A fact on a witness sheet that bears on the case (not merely where someone was, away from the scene).</summary>
        static bool CaseFact(Ctx c, Prop p, string speaker)
        {
            if (!NotableBullet(c, p, speaker)) return false;
            if (NamesVictim(c, p) || (p.Room >= 0 && c.Scene.Contains(p.Room) && p.Kind != PropKind.Heard)) return true;
            if (!Overlaps(p, c.W0 - 60, c.Confirm + 10)) return false;
            switch (p.Kind)
            {
                case PropKind.Held: return WeaponItem(p.Item);
                case PropKind.Heard: return ViolentHeard(p) || (p.Value != null && p.Value.StartsWith("voice:"));
                case PropKind.Bloodied: case PropKind.Disguised: case PropKind.Injured: case PropKind.Culprit: case PropKind.ItemMissing: return true;
                case PropKind.Loaned: return p.Value != null && p.Value.StartsWith("courier:");
                case PropKind.SawActor: return p.Item == "unsure";
            }
            return false;
        }
        /// <summary>The same fact told by several witnesses (the bang half the house heard) counts once.</summary>
        static string NoveltyKey(Prop p)
        {
            if (p.Kind == PropKind.Heard) return $"Heard|{p.Value}|{(long)Math.Floor(p.T0 / 10)}";
            if (p.Kind == PropKind.Held) return $"Held|{p.A}|{p.Item}";
            return FactKeyNoTime(p);
        }
        /// <summary>Witness sheets that belong in 사건 단서: in the order they were first filed, each must add a case fact no
        /// earlier sheet already gave (the others stay under 기타 — still there, still usable in court).</summary>
        static HashSet<string> CaseSheets(Ctx c)
        {
            if (c.SheetsInCase != null) return c.SheetsInCase;
            var res = new HashSet<string>(); var told = new HashSet<string>();
            if (c.Case)
                foreach (var e in c.Mine.Where(IsSheet).OrderBy(e => e.Acquired).ThenBy(e => e.Id, StringComparer.Ordinal))
                {
                    var facts = e.Props.Where(p => CaseFact(c, p, e.Subject)).Select(NoveltyKey).Distinct().ToList();
                    bool novel = facts.Any(f => !told.Contains(f));
                    if (novel || IsKey(c, e)) { res.Add(e.Id); foreach (var f in facts) told.Add(f); }
                }
            c.SheetsInCase = res; return res;
        }

        /// <summary>Was the mark itself (its own estimated window — not a set-piece note stamped "until now") left in [a, b]?</summary>
        static bool MarkNear(Evidence e, double a, double b)
        {
            var own = e.Props.Where(p => p.Kind == PropKind.TraceAt && KnownPlain(p.Value)).ToList();
            if (own.Count == 0) return e.T1 >= a && e.T0 <= b;
            return own.Any(p => Overlaps(p, a, b));
        }

        static string TraceType(Ctx c, Evidence e)
        {
            var tp = e.Props.FirstOrDefault(p => p.Kind == PropKind.TraceAt && p.Item != null)?.Item;
            if (tp != null) return tp;
            var t = e.TraceId != null ? c.S.Traces.FirstOrDefault(x => x.Id == e.TraceId) : null; return t?.Type;
        }

        // ================================================================== one card, as the notebook shows it
        public static View Describe(Simulation sim, Evidence e) => Describe(Context(sim), e);
        internal static View Describe(Ctx c, Evidence e)
        {
            var v = new View { Ev = e, Id = e.Id };
            v.Cat = CatOf(c, e); v.KindLabel = CatLabel[(int)v.Cat];
            v.Title = TitleOf(c, e);
            v.Key = IsKey(c, e); v.InCase = InCase(c, e);
            if (IsSheet(e))
            {
                var facts = SheetFacts(c, e);
                bool allHearsay = facts.Count > 0 && facts.All(f => f.hearsay);
                v.Hearsay = e.Copy || allHearsay;
                string Text(string s, bool h) => h && !allHearsay ? s + " (전해 들음)" : s;
                if (facts.Count > 0) v.Line = Text(facts[0].text, facts[0].hearsay);
                foreach (var f in facts.Skip(1).Take(4)) v.Bullets.Add(Text(f.text, f.hearsay));
                if (v.Line == null) v.Line = FirstLine(e.Desc);
            }
            else
            {
                v.Hearsay = e.Copy;
                var lines = SplitLines(e.Desc);
                v.Line = !string.IsNullOrEmpty(e.Line) ? e.Line : lines.FirstOrDefault() ?? "";
                string lk = Norm(v.Line);
                foreach (var l in lines) { if (v.Bullets.Count >= 4) break; if (Norm(l) == lk || v.Bullets.Any(b => Norm(b) == Norm(l))) continue; v.Bullets.Add(l); }
            }
            v.Caution = CautionOf(c, e, v.Cat);
            v.When = WhenOf(c, e, v.Cat);
            return v;
        }

        static string Norm(string s) => (s ?? "").Trim().TrimEnd('.', ' ');
        static string StripDot(string s) { s = (s ?? "").Trim(); if (s.StartsWith("·")) s = s.Substring(1).TrimStart(); return s; }
        static List<string> SplitLines(string desc) => (desc ?? "").Replace("|", "\n").Split('\n').Select(StripDot).Where(x => x.Length > 0).ToList();
        static string FirstLine(string desc) => SplitLines(desc).FirstOrDefault() ?? "";

        /// <summary>A witness sheet's facts, one per fact, most notable first, each with its own time.</summary>
        static List<(string text, bool hearsay, int score)> SheetFacts(Ctx c, Evidence e)
        {
            var res = new List<(string, bool, int)>(); var seenKey = new HashSet<string>(); var seenText = new HashSet<string>();
            foreach (var p in e.Props.OrderByDescending(p => BulletScore(c, p, e.Subject)).ThenBy(p => p.T0).ThenBy(p => FactKey(p), StringComparer.Ordinal))
            {
                if (!seenKey.Add(FactKey(p))) continue;
                var s = Sentence(c, p, e.Subject); if (s == null || !seenText.Add(s)) continue;
                res.Add((s, HeardOnlyOverheard(c, e.Subject, p), BulletScore(c, p, e.Subject)));
            }
            return res;
        }

        /// <summary>Was this fact only ever overheard (never said to the player)?</summary>
        static bool HeardOnlyOverheard(Ctx c, string speaker, Prop p)
        {
            string fk = FactKeyNoTime(p); bool any = false;
            foreach (var st in c.K.Statements)
            {
                if (st.Speaker != speaker || st.Prop == null || FactKeyNoTime(st.Prop) != fk || st.Prop.T1 < p.T0 - 45 || st.Prop.T0 > p.T1 + 45) continue;
                if (!st.Hearsay) return false; any = true;
            }
            return any;
        }

        static string VictimOf(Evidence e)
        {
            if (e.Subject != null) return e.Subject;
            if (e.Root != null && (e.Root.StartsWith("body:", StringComparison.Ordinal) || e.Root.StartsWith("bodyexam:", StringComparison.Ordinal))) { var p = e.Root.Split(':'); if (p.Length > 1) return p[1]; }
            return e.Props.FirstOrDefault(p => (p.Kind == PropKind.FoundPlace || p.Kind == PropKind.DeathWindow) && p.A != null)?.A;
        }

        static bool IsBodyCard(Evidence e) => e.Kind == EvKind.Body || (e.Important && e.Subject != null) || (e.Root != null && e.Root.StartsWith("official:", StringComparison.Ordinal));

        static Cat CatOf(Ctx c, Evidence e)
        {
            if (IsBodyCard(e)) return Cat.Body;
            if (e.Kind == EvKind.Trace || Starts(e, "sealed:")) return Cat.Scene;
            if (Starts(e, "missing:") || e.Props.Any(p => p.Kind == PropKind.ItemMissing)) return Cat.Object;
            if (e.Kind == EvKind.ObjectState) return Starts(e, "item:") ? Cat.Object : Cat.Scene;
            if (e.Kind == EvKind.Testimony || e.Kind == EvKind.Heard) return Cat.Witness;
            if (e.Kind == EvKind.Sighting && e.Direct && !e.Copy) return Cat.Witness;
            return Cat.Record;
        }

        static string Given(string id) => id == null ? "누군가" : Cast.GivenOf(id) ?? id;
        static string Room(GameState S, int r) => r >= 0 ? S.RoomName(r) : null;
        public static string Fit(string s, int max = TitleMax)
        {
            s = (s ?? "").Trim(); if (s.Length <= max) return s;
            return s.Substring(0, max - 1).TrimEnd() + "…";
        }
        static string Cut(string title)
        {
            string t = title ?? "";
            int i = t.IndexOf(" (", StringComparison.Ordinal); if (i > 0) t = t.Substring(0, i);
            i = t.IndexOf(" — ", StringComparison.Ordinal); if (i > 0) t = t.Substring(0, i);
            return Fit(t);
        }
        static int Tail(string root, int idx) { var p = (root ?? "").Split(':'); return p.Length > idx && int.TryParse(p[idx], out var n) ? n : -1; }
        static string Seg(string root, int idx) { var p = (root ?? "").Split(':'); return p.Length > idx ? p[idx] : null; }

        static string TitleOf(Ctx c, Evidence e)
        {
            var S = c.S; string r = e.Root ?? "";
            if (IsSheet(e)) return Fit($"{Given(e.Subject ?? Seg(r, 1))}의 증언");
            if (IsBodyCard(e)) { var v = VictimOf(e); if (v != null) return Fit(S.A(v)?.Alive == true ? $"{Given(v)}의 상태" : $"{Given(v)}의 시신"); }
            if (r.StartsWith("trace:"))
            {
                var t = S.Traces.FirstOrDefault(x => x.Id == (e.TraceId ?? r.Substring(6)));
                string mark = t?.Desc ?? e.Props.FirstOrDefault(p => p.Kind == PropKind.TraceAt)?.Value;
                if (mark != null && !KnownPlain(mark)) mark = ValueText(mark);
                if (mark == null) return Cut(e.Title);
                // a mark in the room where the body lay needs no room name; elsewhere "{방}의 {흔적}" when it fits (never a parenthesis)
                int tr = t?.Room ?? e.Room;
                if (tr < 0 || (c.Inc != null && tr == c.Inc.FoundRoom)) return Fit(mark);
                string s = $"{Room(S, tr)}의 {mark}";
                return s.Length <= TitleMax ? s : Fit(mark);
            }
            if (r.StartsWith("furn:") || r.StartsWith("tf:") || r.StartsWith("tfi:"))
            {
                int fid = Tail(r, 1); var f = fid >= 0 && fid < S.Layout.Furniture.Count ? S.Layout.Furniture[fid] : null;
                return f != null ? Fit(FurnitureCatalog.Get(f.Type)?.Kor ?? Cut(e.Title)) : Cut(e.Title);
            }
            if (r.StartsWith("door:")) { var d = S.Layout.Door(Tail(r, 1)); return d != null ? Fit($"{S.RoomName(Evidences.DoorRoom(S, d))} 문") : Cut(e.Title); }
            if (r.StartsWith("item:")) { var it = S.I(Seg(r, 1)); return Fit(it?.Kor ?? Cut(e.Title)); }
            if (r.StartsWith("missing:") || e.Props.Any(p => p.Kind == PropKind.ItemMissing))
            {
                var p = e.Props.FirstOrDefault(x => x.Kind == PropKind.ItemMissing); var kor = ItemCatalog.Get(p?.Item)?.Kor ?? S.I(Seg(r, 1))?.Def?.Kor;
                return kor != null ? Fit($"없어진 {kor}") : Cut(e.Title);
            }
            if (r.StartsWith("dev:") || r.StartsWith("mdev:"))
            {
                int fid = Tail(r, 1); var f = fid >= 0 && fid < S.Layout.Furniture.Count ? S.Layout.Furniture[fid] : null;
                string dev = f != null ? FurnitureCatalog.Get(f.Type)?.Kor : null; if (dev == null) return Cut(e.Title);
                string s = $"{S.RoomName(f.Room)} {dev}"; return s.Length <= TitleMax ? s : Fit(dev);
            }
            if (r.StartsWith("devlog:")) return Fit($"{S.RoomName(e.Room)} 출입 기록");
            if (r.StartsWith("clockoff:")) return Fit($"{S.RoomName(e.Room)} 시계");
            if (r.StartsWith("scratch:")) return Fit($"{Given(Seg(r, 1))} 손등의 긁힌 자국");
            if (r.StartsWith("wet:")) return Fit($"{Given(Seg(r, 1))}의 젖은 소매");
            if (r.StartsWith("sealed:")) return "밀실 상태";
            if (r.StartsWith("afterglow:")) { var it = S.I(Seg(r, 1)); return it != null ? Fit($"씻긴 {it.Kor}") : "씻긴 흔적"; }
            if (r.StartsWith("ch04:")) return Fit($"{Given(Seg(r, 1))}의 바뀐 진술");
            if (r.StartsWith("envelope:"))
            {
                var it = S.I(Seg(r, 1)); var sec = it?.Surface.FirstOrDefault(s => s.StartsWith("secret:"));
                return sec != null ? Fit($"{Given(sec.Substring(7))}에 관한 봉투") : Cut(e.Title);
            }
            if (r.StartsWith("invite:")) { var p = e.Props.FirstOrDefault(x => x.Kind == PropKind.Invited); return p?.B != null ? Fit($"{Given(p.B)}의 초대") : Cut(e.Title); }
            if (r.StartsWith("hand:")) return "물건을 건네는 장면";
            if (r.StartsWith("gath:")) { var g = S.Gatherings.FirstOrDefault(x => x.Id == Seg(r, 1)); return g?.Label != null ? Fit($"{g.Label} 참석자") : Cut(e.Title); }
            if (r.StartsWith("reverb:")) { var p = e.Props.FirstOrDefault(x => x.Kind == PropKind.Heard); return p != null ? Fit($"{Room(S, p.Room) ?? "방"}의 소리 잔향") : Cut(e.Title); }
            return Cut(e.Title);
        }

        static string CautionOf(Ctx c, Evidence e, Cat cat)
        {
            if (Starts(e, "door:")) return "지금 상태일 뿐, 그때도 그랬는지는 모른다";
            if (cat == Cat.Body)
            {
                var v = VictimOf(e);
                bool moved = e.Props.Any(p => p.Kind == PropKind.BodyMoved && p.Value == "likely");
                bool drag = c.Mine.Any(x => x.Kind == EvKind.Trace && x.Props.Any(p => p.Kind == PropKind.TraceAt && (p.Item == "DragMark" || p.Item == "BloodSmear") && (p.A == v || (c.Inc != null && x.Room == c.Inc.FoundRoom))));
                if (moved || drag) return "발견된 곳이 숨진 곳이라는 보장은 없다";
            }
            return null;
        }

        static string WhenOf(Ctx c, Evidence e, Cat cat)
        {
            var S = c.S; double now = S.Clock;
            if (IsSheet(e)) return null;
            if (cat == Cat.Body)
            {
                var v = VictimOf(e); var inc = S.Incidents.Values.Where(i => i.Victim == v && i.Loop == S.Loop).OrderBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
                double t = inc != null && inc.DiscoverClock > 0 ? inc.DiscoverClock : e.Acquired; return "발견 " + ClockFmt.Anchor(t, now);   // not to be read as the time of death
            }
            if (Starts(e, "missing:") || e.Props.Any(p => p.Kind == PropKind.ItemMissing)) return null;
            if (Starts(e, "official:")) return null;
            if (Starts(e, "invite:")) { var p = e.Props.FirstOrDefault(x => x.Kind == PropKind.Invited); return p != null ? Span(p.T0, p.T1, now) : null; }
            if (e.Kind == EvKind.Trace)
            {
                string type = TraceType(c, e); if (type != null && type.StartsWith("Blood")) return null;   // its age is already said in words
                if (e.T1 - e.T0 > 180) return null;   // an old, dry mark: a half-day guess narrows nothing
                return Span(e.T0, e.T1, now);
            }
            if (e.T0 <= 0 && e.T1 <= 0) return null;
            return Span(e.T0, e.T1, now);
        }

        static string Span(double a, double b, double now) => a <= 0 && b <= 0 ? null : Math.Abs(b - a) < 12 ? ClockFmt.Anchor((a + b) / 2, now) : ClockFmt.AnchorRange(a, b, now);

        // ================================================================== the notebook's list
        public static List<View> Cards(Simulation sim) => Cards(Context(sim));
        internal static List<View> Cards(Ctx c)
        {
            var views = c.Mine.Select(e => Describe(c, e)).ToList();
            if (!c.Case) return views.OrderByDescending(v => v.Ev.Acquired).ThenBy(v => v.Id, StringComparer.Ordinal).ToList();
            var inCase = views.Where(v => v.InCase).OrderByDescending(v => v.Key).ThenBy(v => (int)v.Cat).ThenBy(v => v.Ev.Acquired).ThenBy(v => v.Id, StringComparer.Ordinal);
            var other = views.Where(v => !v.InCase).OrderByDescending(v => v.Ev.Acquired).ThenBy(v => v.Id, StringComparer.Ordinal);
            return inCase.Concat(other).ToList();
        }

        // ================================================================== 사건 개요: four questions
        public static List<Question> Questions(Simulation sim) => Questions(Context(sim));
        internal static List<Question> Questions(Ctx c)
        {
            var res = new List<Question>(); if (!c.Case) return res;
            var S = c.S; string V = c.Victim; string found = S.RoomName(c.Inc.FoundRoom);
            var inCase = c.Mine.Where(e => InCase(c, e)).ToList();
            const string preExam = "시신을 아직 자세히 살펴보지 않았다 — 가까이 가서 R을 길게 누르자";

            // ---- Q1 언제 죽었나
            var q1 = new Question { Index = 0, Ask = "언제 죽었나" };
            if (c.Primary != null) q1.CardIds.Add(c.Primary.Id);
            if (c.Known.examined)
            {
                q1.Answer = ClockFmt.AnchorRange(c.Known.t0, c.Known.t1, S.Clock);
                double a = c.Known.t0 - 30, b = c.Known.t1 + 30;
                foreach (var e in c.Mine.Where(e => e != c.Primary))
                {
                    bool hit = e.Props.Any(p => Overlaps(p, a, b) && ((p.Kind == PropKind.AliveAt && p.A == V) || ((p.Kind == PropKind.AtPlace || p.Kind == PropKind.WithPerson || p.Kind == PropKind.Held) && (p.A == V || p.B == V)) || (p.Kind == PropKind.SawActor && p.B == V) || ViolentHeard(p)))
                        || (e.Kind == EvKind.Trace && (e.T0 + e.T1) / 2 >= a && (e.T0 + e.T1) / 2 <= b);
                    if (hit) { q1.Firm = true; q1.CardIds.Add(e.Id); }
                }
                if (c.K.Sightings.Any(s => s.Target == V && !s.Dead && s.T1 >= a && s.T0 <= b)) q1.Firm = true;
                if (c.K.Heard.Any(h => h.Clock >= a && h.Clock <= b && Array.IndexOf(Violent, h.Kind) >= 0)) q1.Firm = true;
                q1.Hint = "피해자를 마지막으로 본 사람이나, 그 무렵 소리를 들은 사람을 찾아보자";
            }
            else q1.Hint = preExam;
            res.Add(q1);

            // ---- Q2 어디서 벌어졌나
            var q2 = new Question { Index = 1, Ask = "어디서 벌어졌나" };
            double k0 = c.Known.t0 - 30, k1 = c.Known.t1 + 30;
            var traces = inCase.Where(e => e.Kind == EvKind.Trace && (e.Room == c.Inc.FoundRoom || (e.T1 >= k0 && e.T0 <= k1))).ToList();
            var elsewhere = traces.Where(e => e.Room >= 0 && e.Room != c.Inc.FoundRoom).GroupBy(e => e.Room).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).FirstOrDefault();
            bool moved = c.Primary != null && c.Primary.Props.Any(p => p.Kind == PropKind.BodyMoved && p.Value == "likely");
            var sealedCard = c.Mine.FirstOrDefault(e => Starts(e, "sealed:"));
            // a drag mark (or a body that may have been moved) means "found here" is not "happened here": the place is settled
            // only by marks elsewhere, or by a struggle and a pool of blood where it lay — the same doubt the body card voices
            bool doubt = moved || traces.Any(e => { var ty = TraceType(c, e); return ty == "DragMark" || ty == "Drag" || ty == "BloodSmear"; });
            var atFound = traces.Where(e => e.Room == c.Inc.FoundRoom).ToList();
            bool struggle = atFound.Any(e => (TraceType(c, e) ?? "").StartsWith("Struggle") || e.Props.Any(p => p.Kind == PropKind.TraceAt && p.Value == "몸싸움 흔적"));
            bool pool = atFound.Any(e => { var ty = TraceType(c, e); return ty == "BloodPool" || ty == "Blood" || ty == "BloodSpray"; });
            bool struggleAndPool = struggle && pool;
            int place = -1;
            if (sealedCard != null) place = c.Inc.FoundRoom;
            else if (doubt) { if (elsewhere != null) place = elsewhere.Key; else if (struggleAndPool) place = c.Inc.FoundRoom; }
            else { var two = traces.Where(e => e.Room >= 0).GroupBy(e => e.Room).Where(g => g.Count() >= 2).OrderBy(g => g.Key == c.Inc.FoundRoom ? 0 : 1).ThenBy(g => g.Key).FirstOrDefault(); if (two != null) place = two.Key; }
            q2.Firm = place >= 0;
            q2.Answer = q2.Firm ? $"벌어진 곳: {S.RoomName(place)}" + (place != c.Inc.FoundRoom ? $" · 발견: {found}" : "")
                                : $"발견된 곳: {found}" + (elsewhere != null ? $" · 흔적: {S.RoomName(elsewhere.Key)}" : "");
            q2.CardIds.AddRange(traces.Select(e => e.Id)); if (sealedCard != null) q2.CardIds.Add(sealedCard.Id);
            bool unseen = S.Traces.Any(t => t.Room == c.Inc.FoundRoom && t.Visibility <= 1 && !t.Cleaned && !c.K.Examined.Contains("trace:" + t.Id));
            q2.Hint = unseen ? $"현장({found})에 아직 살펴보지 않은 흔적이 보인다" : "시신이 옮겨졌는지, 다른 방에 흔적이 있는지 살펴보자";
            res.Add(q2);

            // ---- Q3 무엇으로
            var q3 = new Question { Index = 2, Ask = "무엇으로" };
            if (!c.Known.examined) q3.Hint = preExam;
            else
            {
                var pr = c.Primary.Props;
                bool poison = pr.Any(p => p.Kind == PropKind.TraceAt && p.A == V && (p.Value == "poisoned" || p.Value == "dose-window"));
                var wt = pr.FirstOrDefault(p => p.Kind == PropKind.WeaponType && p.Value != null && p.Value != "None");
                var objs = c.Mine.Where(e => e != c.Primary && !IsBodyCard(e)).ToList();
                if (poison)
                {
                    q3.Answer = "독으로 보인다"; q3.Hint = "마실 것이나 약병을 살펴보자";
                    foreach (var e in objs.Where(e => e.Props.Any(p => p.Kind == PropKind.ItemState && (p.Value == "poison-residue" || p.Value == "vial-used" || p.Value == "poisoned-personal")))) { q3.Firm = true; q3.CardIds.Add(e.Id); }
                }
                else if (wt != null && Enum.TryParse(wt.Value, out DamageType dt))
                {
                    int fam = DamageFamily(dt);
                    q3.Answer = WeaponAnswer(dt); q3.Hint = MethodHint(fam);
                    foreach (var e in objs)
                    {
                        bool bloody = e.Props.Any(p => p.Kind == PropKind.ItemState && (p.Value == "피 묻음" || p.Value == "세척 흔적" || p.Value == "피 묻었다 씻김"));
                        bool anomalous = bloody || e.Props.Any(p => (p.Kind == PropKind.ItemState && p.Value == "숨겨짐") || p.Kind == PropKind.ItemMissing);
                        string type = e.Props.FirstOrDefault(p => (p.Kind == PropKind.ItemState || p.Kind == PropKind.ItemMissing) && p.Item != null)?.Item;
                        var def = ItemCatalog.Get(type);
                        bool family = def != null && def.IsWeapon && DamageFamily(def.Dmg) == fam;
                        bool method = (fam >= 4) && (e.Props.Any(p => p.Kind == PropKind.TrapSet || p.Kind == PropKind.Staged) || e.Props.Any(p => (p.Kind == PropKind.TraceAt || p.Kind == PropKind.ItemState) && MethodMark(p.Value)));
                        if (bloody || (anomalous && family) || method) { q3.Firm = true; q3.CardIds.Add(e.Id); }
                    }
                    // a witness saw a weapon of this kind in someone's hand around the time: the next step names it
                    var seen = c.Mine.Where(IsSheet).Select(e => (e, p: WeaponSeen(c, e))).Where(x => x.p != null).OrderBy(x => x.p.T0).ThenBy(x => x.e.Id, StringComparer.Ordinal).FirstOrDefault();
                    if (seen.p != null)
                    {
                        q3.CardIds.Add(seen.e.Id);
                        if (!q3.Firm) q3.Hint = $"{J(Given(seen.e.Subject), "이")} 본 {J(ItemKor(seen.p.Item), "이")} 지금 어디 있는지 확인해 보자";
                    }
                }
                else
                {
                    q3.Answer = "겉으로 보이는 상처는 없다"; q3.Hint = "방 안의 장치나 전선을 살펴보자";
                    foreach (var e in objs.Where(e => e.Props.Any(p => p.Kind == PropKind.TrapSet || p.Kind == PropKind.Staged))) { q3.Firm = true; q3.CardIds.Add(e.Id); }
                }
                if (c.Primary != null) q3.CardIds.Insert(0, c.Primary.Id);
            }
            res.Add(q3);

            // ---- Q4 그때 누가 근처에 있었나
            var q4 = new Question { Index = 3, Ask = "그때 누가 근처에 있었나" };
            var near = Nearby(c, k0, Math.Min(k1, c.Found));   // after discovery everyone gathers there: not "near at the time"
            var names = near.Select(x => x.who).Distinct().ToList();
            if (names.Count > 0) q4.Answer = string.Join("·", names.Take(names.Count > 3 ? 2 : 3).Select(Given)) + (names.Count > 3 ? $" 외 {names.Count - 2}명" : "");
            var srcs = near.Select(x => x.src).Distinct().ToList();
            q4.Firm = srcs.Count >= 2 && srcs.Any(s => s != "self" && s != "device");
            foreach (var id in near.Where(x => x.card != null).Select(x => x.card).Distinct()) q4.CardIds.Add(id);
            q4.Hint = WhoHint(c, near, k0, k1);
            res.Add(q4);
            foreach (var q in res) { var d = q.CardIds.Distinct().ToList(); q.CardIds.Clear(); q.CardIds.AddRange(d); }
            return res;
        }

        static int DamageFamily(DamageType d)
        {
            switch (d) { case DamageType.Blunt: case DamageType.Crush: return 1; case DamageType.Cut: case DamageType.Stab: return 2; case DamageType.Choke: return 3; case DamageType.Drown: return 4; case DamageType.Shock: return 5; case DamageType.Fall: return 6; case DamageType.Burn: return 7; }
            return 0;
        }
        /// <summary>Where to look next for "무엇으로", by the kind of death (a fall has no weapon to put back).</summary>
        static string MethodHint(int fam)
        {
            switch (fam)
            {
                case 4: return "물이 있는 곳(욕실·수영장·분수)을 살펴보자";
                case 5: return "방 안의 장치나 전선을 살펴보자";
                case 6: return "어디서 떨어졌는지, 위층 난간이나 계단을 살펴보자";
                case 7: return "불을 낼 만한 곳(벽난로·소각실)을 살펴보자";
            }
            return "흉기가 될 만한 물건이 제자리에 있는지 확인해 보자";
        }
        static string WeaponAnswer(DamageType d)
        {
            switch (d)
            {
                case DamageType.Blunt: return "둔기로 보인다"; case DamageType.Crush: return "무거운 것으로 보인다"; case DamageType.Cut: case DamageType.Stab: return "날붙이로 보인다";
                case DamageType.Choke: return "끈 같은 것으로 보인다"; case DamageType.Drown: return "물에 빠진 것으로 보인다"; case DamageType.Shock: return "감전으로 보인다";
                case DamageType.Fall: return "높은 데서 떨어진 것 같다"; case DamageType.Burn: return "불로 보인다";
            }
            return "아직 모른다";
        }
        static readonly string[] MethodMarks = { "cable-stripped", "scorch", "edge-scuff", "wet-trail", "thread-under-door", "key-slid", "insulation-shavings", "torn-button", "line-cut", "cord-stretched", "pillow-pressed", "sedative-residue", "sedative-used", "smeared" };
        static bool MethodMark(string v) => v != null && Array.IndexOf(MethodMarks, v) >= 0;

        /// <summary>Everyone the player's cards place in a scene room in the window: (who, source, card id, room).</summary>
        static List<(string who, string src, string card, int room)> Nearby(Ctx c, double a, double b)
        {
            var res = new List<(string, string, string, int)>(); var S = c.S;
            void Add(string who, string src, string card, int room) { if (who == null || who == Cast.Player || c.Victims.Contains(who) || !c.Scene.Contains(room)) return; res.Add((who, src, card, room)); }
            foreach (var e in c.Mine.Where(e => !e.Copy))
            {
                string src = IsSheet(e) ? e.Subject : Starts(e, "devlog:") || Starts(e, "dev:") || Starts(e, "mdev:") || e.Props.Any(p => p.Kind == PropKind.DeviceRecord) ? "device" : e.Kind == EvKind.Sighting && e.Direct ? "self" : null;
                if (src == null) continue;
                foreach (var p in e.Props.Where(p => Overlaps(p, a, b)))
                {
                    switch (p.Kind)
                    {
                        case PropKind.AtPlace: case PropKind.Held: Add(p.A, src, e.Id, p.Room); break;
                        case PropKind.WithPerson: Add(p.A, src, e.Id, p.Room); Add(p.B, src, e.Id, p.Room); break;
                        case PropKind.SawActor: Add(p.B, src, e.Id, p.Room); break;
                        case PropKind.DeviceRecord: if (p.Value != "coverage-partial") Add(p.A, "device", e.Id, p.Room); break;
                    }
                }
            }
            foreach (var s in c.K.Sightings.Where(s => !s.Dead && s.IdConf > 0.6f && s.T1 >= a && s.T0 <= b)) Add(s.Target, "self", null, s.Room);
            return res.OrderBy(x => x.Item1, StringComparer.Ordinal).ThenBy(x => x.Item2, StringComparer.Ordinal).ToList();
        }

        static string WhoHint(Ctx c, List<(string who, string src, string card, int room)> near, double a, double b)
        {
            var S = c.S;
            bool Ask(string id) { var x = S.A(id); return x != null && x.Alive && !x.IsPlayer && !x.IsButler && !c.Victims.Contains(id) && !Asked(c, id); }
            // the victim's last companion, as far as the player's cards say
            var comp = c.Mine.SelectMany(e => e.Props).Where(p => p.Kind == PropKind.WithPerson && (c.Victims.Contains(p.A) || c.Victims.Contains(p.B)))
                .OrderByDescending(p => p.T1).Select(p => c.Victims.Contains(p.A) ? p.B : p.A).FirstOrDefault(Ask);
            if (comp != null) return $"{Given(comp)}에게 아직 묻지 않았다 — 피해자와 마지막으로 함께 있었다";
            var cand = near.Where(x => Ask(x.who)).ToList();
            if (cand.Count > 0)
            {
                var ids = cand.Select(x => x.who).Distinct().Take(2).ToList();
                string names = string.Join("·", ids.Select(Given));
                return $"{names}에게 아직 묻지 않았다 — 그 무렵 {S.RoomName(cand[0].room)} 근처에 있었다고 한다";
            }
            return "피해자를 마지막으로 본 사람을 찾아보자";
        }

        public static int FirmCount(Simulation sim) => Questions(sim).Count(q => q.Firm);

        public static List<string> NextSteps(Simulation sim, int max = 3)
        {
            var c = Context(sim); var res = new List<string>();
            foreach (var q in Questions(c)) if (!q.Firm && !string.IsNullOrEmpty(q.Hint) && !res.Contains(q.Hint)) res.Add(q.Hint);
            if (c.Case && Conflicts(c).Count > 0) res.Insert(Math.Min(1, res.Count), "두 사람의 말이 어긋난다 — 동선 탭에서 확인하자");
            return res.Take(Math.Max(0, max)).ToList();
        }

        // ================================================================== accounts that cannot both be true
        public static List<Conflict> Conflicts(Simulation sim) => Conflicts(Context(sim));
        internal static List<Conflict> Conflicts(Ctx c)
        {
            var res = new List<Conflict>(); if (!c.Case) return res; var S = c.S;
            double a = c.W0 - 60, b = c.Confirm + 10;
            var pl = new List<(string who, int room, double t0, double t1, string src, string label)>();
            void Add(string who, int room, double t0, double t1, string src, string label)
            {
                if (who == null || who == Cast.Player || room < 0) return;
                var r = S.Layout.Room(room); if (r == null || RoomInfo.IsPassage(r.Type)) return;
                if (t1 - t0 < 6) { double m = (t0 + t1) / 2; t0 = m - 3; t1 = m + 3; }
                if (t1 < a || t0 > b) return;
                pl.Add((who, room, t0, t1, src, label));
            }
            foreach (var e in c.Mine.Where(e => !e.Copy))
            {
                string src, label;
                if (IsSheet(e)) { src = e.Subject; label = $"{Given(e.Subject)}의 말"; }
                else if (Starts(e, "devlog:") || e.Props.Any(p => p.Kind == PropKind.DeviceRecord)) { src = "device"; label = "출입 기록"; }
                else if (e.Kind == EvKind.Sighting && e.Direct) { src = "self"; label = "직접 봄"; }
                else continue;
                foreach (var p in e.Props)
                    switch (p.Kind)
                    {
                        case PropKind.AtPlace: case PropKind.WithPerson:
                            {
                                // people round their own whereabouts to ten minutes: don't call the rounding a contradiction
                                double s0 = p.T0, s1 = p.T1; if (IsSheet(e) && p.A == e.Subject && s1 - s0 > 20) { s0 += 5; s1 -= 5; }
                                Add(p.A, p.Room, s0, s1, src, label); if (p.Kind == PropKind.WithPerson) Add(p.B, p.Room, s0, s1, src, label);
                                break;
                            }
                        case PropKind.SawActor: if (p.Item != "unsure") Add(p.B, p.Room, p.T0, p.T1, src, label); break;
                        case PropKind.DeviceRecord: if (p.Value != "coverage-partial") Add(p.A, p.Room, p.T0, p.T1, "device", "출입 기록"); break;
                    }
            }
            foreach (var s in c.K.Sightings.Where(s => !s.Dead && s.IdConf > 0.6f)) Add(s.Target, s.Room, s.T0, s.T1, "self", "직접 봄");
            var done = new HashSet<string>();
            foreach (var g in pl.GroupBy(x => x.who).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var xs = g.OrderBy(x => x.t0).ThenBy(x => x.src, StringComparer.Ordinal).ToList();
                for (int i = 0; i < xs.Count; i++)
                    for (int j = i + 1; j < xs.Count; j++)
                    {
                        var x = xs[i]; var y = xs[j];
                        if (x.room == y.room || x.src == y.src) continue;
                        double o0 = Math.Max(x.t0, y.t0), o1 = Math.Min(x.t1, y.t1); if (o1 - o0 < 5) continue;
                        string key = g.Key + "|" + Math.Min(x.room, y.room) + "|" + Math.Max(x.room, y.room);
                        if (!done.Add(key)) continue;
                        res.Add(new Conflict { Who = Given(g.Key), T0 = o0, T1 = o1,
                            A = $"{x.label}: {S.RoomName(x.room)} ({ClockFmt.Anchor((x.t0 + x.t1) / 2, S.Clock)})",
                            B = $"{y.label}: {S.RoomName(y.room)} ({ClockFmt.Anchor((y.t0 + y.t1) / 2, S.Clock)})" });
                    }
            }
            return res;
        }

        // ================================================================== court: which cards to offer first
        public static List<(string id, double score)> RankScored(Simulation sim, IList<TrialClaim> claims)
        {
            var c = Context(sim); var res = new List<(string id, double score, double acq)>();
            foreach (var v in Cards(c).Where(v => v.Ev.Props.Count > 0))
            {
                double sc = 0;
                if (claims != null)
                    foreach (var cl in claims)
                    {
                        if (cl?.Prop == null) continue;
                        double best = 0;
                        foreach (var p in v.Ev.Props)
                        {
                            double s = 0;
                            if (p.A != null && (p.A == cl.Prop.A || p.A == cl.Prop.B)) s += 2;
                            if (p.B != null && (p.B == cl.Prop.A || p.B == cl.Prop.B)) s += 1.5;
                            if (p.Room >= 0 && p.Room == cl.Prop.Room) s += 1.5;
                            if (p.Kind == cl.Prop.Kind) s += 1;
                            if (cl.Prop.T1 > 0 && p.T1 > 0 && p.T0 <= cl.Prop.T1 + 20 && p.T1 >= cl.Prop.T0 - 20) s += 1;
                            if (s > best) best = s;
                        }
                        sc += best;
                        if (IsSheet(v.Ev) && v.Ev.Subject == cl.Speaker) sc += 1;
                    }
                if (v.Key) sc += 2; if (v.Ev.Pinned) sc += 1; if (!v.InCase) sc -= 2;
                // a clock read long before the case (set right days ago) says nothing about that night's times
                if (c.Case && v.Ev.Props.Any(p => p.Kind == PropKind.ClockOffset) && v.Ev.Props.Where(p => p.Kind == PropKind.ClockOffset).All(p => p.T0 < c.W0 - 720)) sc -= 3;
                res.Add((v.Id, sc, v.Ev.Acquired));
            }
            return res.OrderByDescending(x => x.score).ThenBy(x => x.acq).ThenBy(x => x.id, StringComparer.Ordinal).Select(x => (x.id, x.score)).ToList();
        }
        /// <summary>Every card the court can use, best guess first — never truncated.</summary>
        public static List<string> Rank(Simulation sim, IList<TrialClaim> claims) => RankScored(sim, claims).Select(x => x.id).ToList();

        // ================================================================== sentences
        public static string Sentence(Simulation sim, Prop p, string speaker = null) => p == null ? null : Sentence(sim.S, p, speaker);
        internal static string Sentence(Ctx c, Prop p, string speaker) => Sentence(c.S, p, speaker);

        static string J(string w, string p) => string.IsNullOrEmpty(w) ? w : w + LineBank.Josa(w, p);
        static string Join(params string[] parts) => string.Join(" ", parts.Where(x => !string.IsNullOrEmpty(x)));
        static string ItemKor(string type) => ItemCatalog.Get(type)?.Kor ?? (type != null && !HasLatin(type) ? type : "물건");
        static bool HasLatin(string s) => s != null && s.Any(ch => (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'));

        static string Sentence(GameState S, Prop p, string speaker)
        {
            if (p == null) return null;
            double now = S.Clock;
            string T = p.T0 <= 0 && p.T1 <= 0 ? null : Span(p.T0, p.T1, now);
            string R = p.Room >= 0 ? S.RoomName(p.Room) : null;
            string A = p.A != null ? Given(p.A) : "누군가"; string B = p.B != null ? Given(p.B) : null;
            switch (p.Kind)
            {
                case PropKind.AtPlace:
                    if (speaker != null && p.A == speaker) return Join(T, R != null ? R + "에 있었다고 한다" : "그곳에 있었다고 한다");
                    return Join(J(A, "이"), T, R != null ? R + "에 있었다" : "그곳에 있었다");
                case PropKind.NotAtPlace: return Join(J(A, "은"), T, (R ?? "그곳") + "에 없었다");
                case PropKind.WithPerson:
                    return Join(speaker != null && p.A == speaker ? null : J(A, "은"), T, R != null ? R + "에서" : null, J(B ?? "누군가", "와"), "함께 있었다");
                case PropKind.SawActor:
                    return Join(speaker != null && p.A == speaker ? null : J(A, "이"), T, R != null ? R + "에서" : null, p.Item == "unsure" ? (B ?? "누군가") + "인 것 같은 사람을 봤다" : J(B ?? "누군가", "을") + " 봤다");
                case PropKind.Held: return Join(J(A, "이"), J(ItemKor(p.Item), "을"), "들고 있었다") + (T != null ? $" ({T})" : "");
                case PropKind.Heard:
                    {
                        string where = R != null ? R + " 쪽에서" : null;
                        if (p.Value != null && p.Value.StartsWith("voice:")) return Join(T, where, Given(p.Value.Substring(6)) + "의 목소리를 들었다 — 모습은 못 봤다");
                        string snd = p.Value != null && Enum.TryParse(p.Value, out SoundKind sk) ? Simulation.SoundText(sk) : null;
                        return Join(T, where, snd != null ? snd + " 소리를 들었다" : "이상한 소리를 들었다");
                    }
                case PropKind.DeathWindow: return p.Value == "exam" ? $"{ClockFmt.AnchorRange(p.T0, p.T1, now)}에 숨진 것으로 보인다" : null;
                case PropKind.AliveAt: return Join(J(A, "은"), T != null ? T + "에는" : null, "살아 있었다");
                case PropKind.FoundPlace: return R != null ? R + "에서 발견됐다" : null;
                case PropKind.DeathPlace: return R != null ? R + "에서 숨졌다" : null;
                case PropKind.Wound: return null;
                case PropKind.WeaponType:
                    {
                        if (!Enum.TryParse(p.Value ?? "None", out DamageType d)) return null;
                        switch (d)
                        {
                            case DamageType.Blunt: return "흉기: 둔기로 보인다"; case DamageType.Crush: return "흉기: 무거운 것으로 보인다";
                            case DamageType.Cut: case DamageType.Stab: return "흉기: 날붙이로 보인다"; case DamageType.Choke: return "흉기: 끈 같은 것으로 보인다";
                            case DamageType.Drown: return "물에 빠져 숨진 것으로 보인다"; case DamageType.Shock: return "감전되어 숨진 것으로 보인다";
                            case DamageType.Fall: return "떨어져 숨진 것으로 보인다"; case DamageType.Burn: return "불에 타 숨진 것으로 보인다";
                        }
                        return null;
                    }
                case PropKind.BodyMoved: return p.Value == "likely" ? "시신이 옮겨졌을 수 있다" : null;
                case PropKind.DoorLocked: return $"{R ?? "그 방"} 문은 그때 잠겨 {(p.Value == "no" ? "있지 않았다" : "있었다")}";
                case PropKind.DoorState:
                    {
                        string room = R; if (int.TryParse(p.Item, out var did)) { var d = S.Layout.Door(did); if (d != null) room = S.RoomName(Evidences.DoorRoom(S, d)); }
                        return $"{room ?? "그 방"} 문: 지금 {(p.Value == "locked" ? "잠겨 있다" : "열려 있다")}";
                    }
                case PropKind.Injured:
                    if (p.Value == "attacked") return Join(J(A, "이"), "공격당해 다쳤다");
                    if (p.Value != null && p.Value.Contains("긁힘")) return $"{A}의 {p.Value.Replace(" 긁힘", "").Replace("긁힘", "").Trim()}에 긁힌 자국이 있었다".Replace("의 에", "에");
                    return p.Value != null && !HasLatin(p.Value) ? Join(J(A, "이"), J(p.Value, "을"), "다쳤다") : Join(J(A, "이"), "다쳤다");
                case PropKind.Bloodied: return $"{A}의 옷에 피가 묻어 있었다" + (T != null ? $" ({T})" : "");
                case PropKind.Wet: return Join(J(A, "이"), "젖어 있었다") + (T != null ? $" ({T})" : "");
                case PropKind.Disguised: return Join(T, R != null ? R + "에서" : null, "얼굴을 가린 누군가가 보였다");
                case PropKind.Culprit: return p.A == null || p.B == null ? null : Join(J(A, "이"), J(B, "을"), "죽였다고 한다");
                case PropKind.Motive: return B != null ? $"{A}에게는 {J(B, "을")} 미워할 이유가 있었다" : $"{A}에게는 이유가 있었다";
                case PropKind.Ability: return $"{A}에게는 특별한 힘이 있다";
                case PropKind.Key: return Join(J(A, "이"), J(ItemKor(p.Item), "을"), "갖고 있었다");
                case PropKind.ItemAt: return p.Value == "moved" ? null : Join(J(ItemKor(p.Item), "이"), R != null ? R + "에 있었다" : "그곳에 있었다");
                case PropKind.Lie:
                    {
                        string v = p.Value ?? "";
                        int i = v.IndexOf("진술 정정 ", StringComparison.Ordinal);
                        if (i >= 0) return $"{A}의 말이 바뀌었다 — {v.Substring(i + 6)}";
                        return $"{A}의 말은 사실과 달랐다";
                    }
                case PropKind.TraceAt:
                    {
                        string t = ValueText(p.Value);
                        if (t == null) return R != null ? R + "에서 이상한 점이 보였다" : "이상한 점이 보였다";
                        return R != null ? $"{R}: {t}" : t;
                    }
                case PropKind.ItemState:
                    {
                        string t = ValueText(p.Value);
                        string owner = p.A != null ? $" ({Given(p.A)}의 것)" : "";
                        if (t == null) return J(ItemKor(p.Item), "에") + " 이상한 점이 있다";
                        return $"{ItemKor(p.Item)}: {t}{owner}";
                    }
                case PropKind.ItemMissing: return Join(R != null ? R + "에서" : null, J(ItemKor(p.Item), "이"), "없어졌다");
                case PropKind.LightsOut: return Join(T, (R ?? "그 방") + "의 불이 꺼져 있었다");
                case PropKind.LightsChanged:
                    {
                        string v = p.Value ?? "";
                        if (v.StartsWith("trip:")) return Join(T, v.Substring(5) + " 회로가 내려갔다");
                        return Join(T, HasLatin(v) || v.Length == 0 ? "누군가 조명을 바꿨다" : "누군가 " + J(v, "을") + " 건드렸다");
                    }
                case PropKind.MachineUsed:
                    {
                        string v = p.Value ?? "";
                        if (v.Contains("녹음")) return Join(T, R != null ? R + "에서" : null, "녹음이 재생됐다");
                        if (v.StartsWith("수리")) return Join(p.A != null ? J(A, "이") : null, "손본 기록이 있다", v.Contains("미완료") ? "(끝나지 않았다)" : null);
                        if (v == "소각로 가동") return Join(T, "소각로가 돌아갔다");
                        if (v == "보일러 최대 출력") return Join(T, "보일러가 최대로 돌아갔다");
                        if (v == "프레스") return Join(T, "프레스가 움직였다");
                        return Join(T, HasLatin(v) || v.Length == 0 ? "장치가 움직였다" : J(v, "이") + " 있었다");
                    }
                case PropKind.Invited: return Join(J(B ?? "누군가", "이"), J(A, "을"), R != null ? R + "에" : null, "불렀다") + (T != null ? $" ({T})" : "");
                case PropKind.Loaned:
                    if (p.Value != null && p.Value.StartsWith("courier:")) return $"{B ?? "누군가"}의 부탁으로 {J(A, "이")} 쪽지를 전했다";
                    return Join(J(B ?? "누군가", "이"), A + "에게", J(ItemKor(p.Item), "을"), "건넸다") + (T != null ? $" ({T})" : "");
                case PropKind.ClockOffset:
                    {
                        if (!int.TryParse(p.Value, out var n) || n == 0) return null;
                        return $"{R ?? "그 방"} 시계가 {Math.Abs(n)}분 {(n > 0 ? "빠르다" : "느리다")}";
                    }
                case PropKind.DeviceRecord:
                    if (p.Value == "coverage-partial") return $"{R ?? "그 방"} 출입 기록기는 문 하나만 기록한다";
                    if (p.Value == "playback") return Join(T, R != null ? R + "에서" : null, "녹음이 재생됐다 (기록)");
                    return Join(T, J(A, "이"), R != null ? R + "에" : null, p.Value == "out" ? "나왔다" : "들어갔다") + " (출입 기록)";
                case PropKind.TrapSet:
                    {
                        string what = p.Value == "Shock" ? "전기 장치" : p.Value == "Topple" ? "넘어뜨리는 장치" : p.Value == "Tripwire" ? "걸려 넘어지는 줄" : "장치";
                        return $"{R ?? "그 방"}에 {J(what, "이")} 있었다";
                    }
                case PropKind.Staged:
                    switch (p.Value)
                    {
                        case "seal": return "밖에서 잠가 밀실로 꾸몄다";
                        case "tod-heat": return "시신을 데워 숨진 시각을 늦춰 보이게 했다";
                        case "tod-cold": return "시신을 식혀 숨진 시각을 앞당겨 보이게 했다";
                        case "message": return "피해자가 남긴 글씨처럼 꾸몄다";
                        case "swap": return "흉기를 바꿔치기했다";
                    }
                    return "현장이 꾸며져 있었다";
            }
            return null;
        }

        // ================================================================== value codes → words
        static readonly Dictionary<string, string> Codes = new Dictionary<string, string>
        {
            ["locked"] = "잠김", ["unlocked"] = "열림",
            ["poisoned"] = "독 반응", ["dose-window"] = "독을 먹은 시간대를 짐작할 수 있다", ["temp-warm"] = "시신이 데워져 있다", ["temp-cold"] = "시신이 식혀져 있다",
            ["instant-death"] = "즉사로 보인다", ["thread-under-door"] = "문 밑으로 지나간 실 자국", ["fire-stoked"] = "누군가 불을 키웠다",
            ["cable-stripped"] = "피복이 벗겨진 전선", ["poison-residue"] = "독 찌꺼기", ["vial-used"] = "쓴 흔적이 있는 약병", ["smeared"] = "문질러 번진 자국", ["line-cut"] = "잘린 줄",
            ["ligature"] = "목을 두른 가는 끈 자국", ["nail-scrape"] = "손톱 밑에 낀 남의 살갗", ["smother-marks"] = "얼굴을 짓누른 자국", ["sedated"] = "잠드는 약을 먹은 흔적",
            ["push-bruise"] = "손바닥에 세게 밀린 멍", ["fall-injuries"] = "떨어지며 생긴 상처", ["electrocuted"] = "감전된 자국", ["held-under"] = "물속에서 짓눌린 손가락 자국",
            ["key-slid"] = "문 밑으로 밀어 넣은 자국", ["scorch"] = "전선이 탄 자국", ["soil-dug"] = "새로 파헤친 흙", ["wet-trail"] = "이어진 젖은 발자국", ["ash-fresh"] = "아직 따뜻한 재",
            ["edge-scuff"] = "밀려난 듯 미끄러진 자국", ["powder"] = "흘린 약 가루", ["cord-stretched"] = "세게 조였던 끈", ["torn-button"] = "뜯겨 나간 단추", ["burnt-remnant"] = "타다 남은 단추",
            ["burnt-bone"] = "타다 남은 뼛조각", ["pillow-pressed"] = "얼굴이 눌렸던 베개", ["sedative-residue"] = "잠드는 약을 탄 잔", ["sedative-used"] = "줄어든 수면제", ["poisoned-personal"] = "미리 독을 넣어 둔 물건",
            ["farewell-note"] = "유서", ["handwriting-mismatch"] = "본인 것이 아닌 필체", ["key-on-floor"] = "바닥에 떨어져 있던 열쇠", ["burnt"] = "불에 탄 흔적", ["waterlogged"] = "물에 잠겼던 흔적",
            ["buried"] = "흙에 묻혀 있었다", ["insulation-shavings"] = "전선 피복 부스러기", ["postmortem-cut"] = "숨진 뒤에 잘린 자국", ["saw-marks"] = "톱 자국", ["bone-dust"] = "뼛가루",
            ["drain-blood"] = "배수구에 남은 핏물", ["clothed-drowning"] = "옷을 입은 채 물에 빠졌다", ["cold-water"] = "차가운 물기", ["part-hidden"] = "숨겨진 시신의 일부",
            ["피 묻음"] = "피가 묻어 있다", ["세척 흔적"] = "씻은 흔적이 있다", ["숨겨짐"] = "숨겨져 있었다", ["피 묻었다 씻김"] = "피가 묻었다가 씻겼다",
        };

        /// <summary>A known code (or plain Korean text) → words; null for an unknown code (the caller says something generic).</summary>
        static string ValueText(string v)
        {
            if (string.IsNullOrEmpty(v)) return null;
            if (Codes.TryGetValue(v, out var t)) return t;
            if (v.StartsWith("bloodwriting:")) return $"피로 쓴 글자 「{v.Substring(13)}」";
            if (v.StartsWith("writing-hand:"))
            {
                var p = v.Split(':'); string hand = p.Length > 1 && p[1] == "L" ? "왼손" : "오른손";
                string who = p.Length > 2 ? (p[2] == "left-handed" ? " (피해자는 왼손잡이)" : p[2] == "right-handed" ? " (피해자는 오른손잡이)" : "") : "";
                return $"글씨를 쓴 손: {hand}{who}";
            }
            if (v.StartsWith("sedated:")) return Codes["sedated"];
            if (v.EndsWith("(세척됨)")) { var w = v.Substring(0, v.Length - 5).Trim(); return w.Length > 0 && !HasLatin(w) ? w + " — 씻어 낸 흔적" : "씻어 낸 흔적"; }
            if (!HasLatin(v)) return v;   // already plain Korean (a trace's own description)
            return null;
        }
        static bool KnownPlain(string v) => v != null && !HasLatin(v);

        /// <summary>Does the dictionary (or plain text) cover this value? Unknown codes render generically and are counted by the audit.</summary>
        public static bool KnownValue(string value) => value == null || ValueText(value) != null || value == "moved" || value == "likely" || value == "unknown" || value == "exam" || value == "official"
            || value == "in" || value == "out" || value == "coverage-partial" || value == "playback" || value == "sealed" || value == "yes" || value == "no" || value == "attacked"
            || value.StartsWith("root:") || value.StartsWith("voice:") || value.StartsWith("courier:") || value == "handover" || value.StartsWith("rev") || value.StartsWith("trip:")
            || Enum.TryParse(value, out SoundKind _) || Enum.TryParse(value, out DamageType _) || value == "Shock" || value == "Topple" || value == "Tripwire" || int.TryParse(value, out _)
            || value == "seal" || value == "tod-heat" || value == "tod-cold" || value == "message" || value == "swap" || value == "window-cover";

        // ================================================================== court text
        static readonly Regex LR = new Regex(@"\s*\[LR\d+\]", RegexOptions.CultureInvariant);
        /// <summary>Court text for the screen: no rule codes, the player's word for a card is 단서. Idempotent.</summary>
        public static string Plain(string courtText)
        {
            if (string.IsNullOrEmpty(courtText)) return courtText;
            string s = LR.Replace(courtText, "");
            s = s.Replace(" (전해 들은 내용이라 조건부)", " (전해 들은 말이라 확정할 수 없다)");
            s = Material.Replace(s, "단서"); s = Conditional.Replace(s, "확인 필요"); s = s.Replace("범위 제한", "흔들림");
            // the court's clock rule speaks in signed minutes ("시계가 -13분 틀어진 방이 있다"): say it the way a person would
            s = ClockOff.Replace(s, m => int.TryParse(m.Groups[1].Value, out var n) && n != 0 ? $"{Math.Abs(n)}분 {(n < 0 ? "늦은" : "빠른")} 시계가 있다" : "시계가 틀어진 방이 있다");
            return s;
        }
        static readonly Regex ClockOff = new Regex(@"시계가 ([+-]?\d+)분 틀어진 방이 있다", RegexOptions.CultureInvariant);
        static readonly Regex Material = new Regex("자료(?! 보관)(?!실)", RegexOptions.CultureInvariant);
        static readonly Regex Conditional = new Regex("조건부(?!터)", RegexOptions.CultureInvariant);

        public static bool Asked(Simulation sim, string npc) => Asked(Context(sim), npc);
        static bool Asked(Ctx c, string npc) => c.Inc != null && c.K.Facts.Contains($"asked:{npc}:case:{c.Inc.Id}");
    }
}
