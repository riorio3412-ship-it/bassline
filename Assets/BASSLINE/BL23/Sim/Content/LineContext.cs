using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // LINE RESOLVER (DailyLifeDesign §9.2, feature F2) + the pool helpers the voice packs use (F1).
    //
    // Every kernel line goes through Simulation.Render → LineContext.Resolve. For (speaker, key, listener, slots) it tries,
    // in order, and each tier only when the speaker actually has lines for it:
    //   0  key@Pxx   pair line: said TO this listener                      (p .70)
    //   1  key#Pxx   about line: said ABOUT the person in {t}/{victim}/{t2} (p .70)
    //   2  key~tag   situation line: time of day, mood, a recent death, the room, who else is here, rules, hunger, the
    //                relationship stage with 민혁 … (first matching tag in priority order, each with its own chance)
    //   3  key       the speaker's own generic lines
    //   4  key       the shared ANY lines (ANY~tag first) — the last resort a voice pack is meant to replace
    // and never repeats the speaker's last line for that pool: a per-speaker "deck" (every variant once before any comes
    // back, and never the same line twice in a row) kept in S.Flags["lr:<speaker>:<pool>"]. Randomness is S.R(Stream.Dialogue)
    // only, so the kernel stays deterministic and the memory round-trips with the save.
    //
    // Key suffix conventions (authoring guide: Documentation/BL23/VoicePackGuide.md):
    //   "small_talk@P03"  pair      "grief#P10"  about      "small_talk~afterdeath"  situation
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    public static class LineContext
    {
        /// <summary>Test hook (SimTests "voice"): speaker, key, the resolved pool key, tier (0 pair, 1 about, 2 situation, 3 own, 4 shared).</summary>
        public static Action<string, string, string, int> Trace;

        public const double PPair = 0.7, PAbout = 0.7;

        /// <summary>Keys whose {t} is the person spoken TO (routines walk up to someone): with no listener, {t} becomes the listener
        /// (for the pair tier and for 반말/존댓말). Everywhere else {t} is someone talked ABOUT.</summary>
        public static readonly HashSet<string> AddressedKeys = new HashSet<string>
        {
            "comfort", "offer_tea", "confront", "applause", "stroll_invite", "stroll_accept", "courier_deliver", "argue_open", "argue_reply", "argue_makeup", "argue_stormoff",
        };

        /// <summary>Situation tags in priority order, with the chance that a matching ~tag pool is used over the generic one.
        /// Unknown tags (e.g. rule_CH05, near_P05, at_Kitchen, stage2) fall back to their family's chance.</summary>
        static readonly (string tag, double p)[] TagChance =
        {
            ("afterdeath", 0.85), ("aftertrial", 0.8), ("grief", 0.7), ("afraid", 0.65), ("angry", 0.6), ("stressed", 0.5), ("tired", 0.5),
            ("hunger", 0.55), ("quiet", 0.45), ("rule_", 0.55), ("near_", 0.6), ("stage", 0.6), ("loop", 0.6), ("at_", 0.45),
            ("morning", 0.4), ("day", 0.35), ("evening", 0.4), ("night", 0.5),
        };
        static double ChanceOf(string tag)
        {
            foreach (var (t, p) in TagChance) if (tag == t || (t.EndsWith("_") && tag.StartsWith(t)) || (t == "stage" && tag.StartsWith("stage")) || (t == "loop" && tag.StartsWith("loop"))) return p;
            return 0.5;
        }

        // ------------------------------------------------------------------ the kernel path
        /// <summary>
        /// Picks the raw template for this line. <paramref name="listener"/> and <paramref name="casual"/> may be updated:
        /// an addressed routine key with no listener talks to its {t}, in the register the speaker uses with that person.
        /// Returns null when no bank has the key (the caller shows "…").
        /// </summary>
        public static string Resolve(Simulation sim, string speaker, ref string listener, string key, IDictionary<string, string> slots, ref bool casual)
        {
            var S = sim.S;
            if (key == null) return null;
            if (listener == null && slots != null && AddressedKeys.Contains(key) && slots.TryGetValue("t", out var tv) && tv != null && tv.StartsWith("@"))
            {
                listener = tv.Substring(1);
                if (speaker != Cast.Butler && S.HasRel(speaker, listener)) casual = S.R(speaker, listener).Casual;
            }
            var rng = S.R(Stream.Dialogue);
            string about = About(slots, listener);
            string[] fallback = null; string fallbackKey = null; int fallbackTier = -1; bool fallbackCasual = casual;

            // 0 pair · 1 about  (special pools are consumable: once every variant was heard they rest a while — see Resting)
            if (listener != null && TryPool(speaker, key + "@" + listener, casual, out var pool, out var pc))
            {
                bool rest = Resting(S, speaker, key + "@" + listener, pool.Length, pc, RestPairMinutes);
                if (!rest && rng.Chance(PPair)) return Pick(S, rng, speaker, key, key + "@" + listener, pool, pc, 0, listener);
                fallback = pool; fallbackKey = key + "@" + listener; fallbackTier = 0; fallbackCasual = pc;
            }
            if (about != null && TryPool(speaker, key + "#" + about, casual, out pool, out pc))
            {
                bool rest = Resting(S, speaker, key + "#" + about, pool.Length, pc, RestPairMinutes);
                if (!rest && rng.Chance(PAbout)) return Pick(S, rng, speaker, key, key + "#" + about, pool, pc, 1, listener);
                if (fallback == null) { fallback = pool; fallbackKey = key + "#" + about; fallbackTier = 1; fallbackCasual = pc; }
            }
            // 2 situation — only the tags this speaker has lines for are even computed
            if (LineBank.HasSuffixed(speaker, key, '~'))
            {
                foreach (var tag in Situations(sim, speaker, listener))
                {
                    if (!TryPool(speaker, key + "~" + tag, casual, out pool, out pc)) continue;
                    bool rest = Resting(S, speaker, key + "~" + tag, pool.Length, pc, RestSituationMinutes);
                    if (!rest && rng.Chance(ChanceOf(tag))) return Pick(S, rng, speaker, key, key + "~" + tag, pool, pc, 2, listener);
                    if (fallback == null) { fallback = pool; fallbackKey = key + "~" + tag; fallbackTier = 2; fallbackCasual = pc; }
                }
            }
            // 3 own
            if (TryPool(speaker, key, casual, out pool, out pc)) return Pick(S, rng, speaker, key, key, pool, pc, 3, listener);
            // a pair/about/situation pool that lost its roll still beats the shared line
            if (fallback != null) return Pick(S, rng, speaker, key, fallbackKey, fallback, fallbackCasual, fallbackTier, listener);
            // 4 shared (ANY~tag, then ANY)
            if (LineBank.HasSuffixed(LineBank.Shared, key, '~'))
                foreach (var tag in Situations(sim, speaker, listener))
                    if (TryPool(LineBank.Shared, key + "~" + tag, casual, out pool, out pc) && rng.Chance(ChanceOf(tag))) return Pick(S, rng, speaker, key, "*" + key + "~" + tag, pool, pc, 4, listener);
            if (TryPool(LineBank.Shared, key, casual, out pool, out pc)) return Pick(S, rng, speaker, key, "*" + key, pool, pc, 4, listener);
            return null;
        }

        /// <summary>The person a line is about: the first of {t}, {victim}, {t2} given as "@Pxx" (never the listener).</summary>
        static readonly string[] AboutSlots = { "t", "victim", "t2" };
        static string About(IDictionary<string, string> slots, string listener)
        {
            if (slots == null) return null;
            foreach (var s in AboutSlots)
                if (slots.TryGetValue(s, out var v) && v != null && v.StartsWith("@") && v.Length > 1) { var id = v.Substring(1); if (id != listener) return id; }
            return null;
        }

        static bool TryPool(string actor, string key, bool casual, out string[] pool, out bool usedCasual)
        {
            pool = LineBank.Pool(actor, key, casual, out usedCasual);
            return pool != null && pool.Length > 0;
        }

        /// <summary>Deck pick: every variant once before any repeats, never the same one twice in a row. Memory per speaker and pool.
        /// Outside the pair tier, a variant that names the listener in the third person ("재하는…" said TO 재하) is skipped while
        /// another variant is available.</summary>
        static string Pick(GameState S, Rng rng, string speaker, string key, string poolKey, string[] pool, bool usedCasual, int tier, string listener = null)
        {
            Trace?.Invoke(speaker, key, poolKey, tier);
            int n = pool.Length;
            bool special = tier <= 2;   // pair / about / situation pools keep memory even with one line, so they can rest
            if (n == 1 && !special) return pool[0];
            string avoid = tier != 0 && listener != null && listener != Cast.Player && listener != Cast.Butler ? Cast.Get(listener)?.Given : null;
            string fk = "lr:" + speaker + ":" + poolKey + (usedCasual ? ":C" : ":P");
            S.Flags.TryGetValue(fk, out var mem);
            int pick;
            if (n <= DeckMax)
            {
                // mem = (last + 1) * 2^46 + usedMask  (exact in a double: < 2^53)
                long m = (long)mem; int last = (int)(m >> DeckMax) - 1; long used = m & ((1L << DeckMax) - 1); long all = (1L << n) - 1;
                long bad = 0; if (avoid != null) for (int i = 0; i < n; i++) if (pool[i] != null && pool[i].Contains(avoid)) bad |= 1L << i;
                if (bad == all) bad = 0;
                used &= all; if ((used | bad) == all) used = 0;
                // relax in order: the deck, then "not twice in a row", then the listener-name filter
                pick = DeckChoose(rng, n, used | bad, last);
                if (pick < 0) pick = DeckChoose(rng, n, bad, last);
                if (pick < 0) pick = DeckChoose(rng, n, bad, -1);
                if (pick < 0) pick = DeckChoose(rng, n, 0, -1);
                used |= 1L << pick;
                S.Flags[fk] = (double)(((long)(pick + 1) << DeckMax) | (used & all));
                // a special pool just heard in full: note when, so it rests before coming back (Resting)
                if (special && (used & all) == all) S.Flags["lrt:" + speaker + ":" + poolKey + (usedCasual ? ":C" : ":P")] = S.Clock;
            }
            else
            {
                int last = mem < 0 ? (int)(-mem) - 1 : -1;
                pick = rng.R(n - (last >= 0 && last < n ? 1 : 0)); if (last >= 0 && last < n && pick >= last) pick++;
                S.Flags[fk] = -(pick + 1);
            }
            return pool[pick];
        }
        const int DeckMax = 46;

        /// <summary>Clock minutes a fully-heard special pool rests: pair/about lines (a line said TO or ABOUT someone) and situation lines.</summary>
        public const double RestPairMinutes = 12 * 60, RestSituationMinutes = 3 * 60;

        /// <summary>A pair / about / situation pool whose every variant this speaker has already said, and not long ago: skip it for now
        /// (it stays a fallback before the shared ANY line). Keeps a one-line pair variant from being said six times in a day.</summary>
        static bool Resting(GameState S, string speaker, string poolKey, int n, bool usedCasual, double restMinutes)
        {
            if (n > DeckMax) return false;
            string sfx = usedCasual ? ":C" : ":P";
            if (!S.Flags.TryGetValue("lr:" + speaker + ":" + poolKey + sfx, out var mem)) return false;
            long used = (long)mem & ((1L << DeckMax) - 1), all = (1L << n) - 1;
            if ((used & all) != all) return false;
            return S.Flags.TryGetValue("lrt:" + speaker + ":" + poolKey + sfx, out var t) && S.Clock - t < restMinutes;
        }

        /// <summary>Uniform choice among indices not in <paramref name="blocked"/> and not <paramref name="last"/>; -1 when none.</summary>
        static int DeckChoose(Rng rng, int n, long blocked, int last)
        {
            int free = 0; for (int i = 0; i < n; i++) if ((blocked & (1L << i)) == 0 && i != last) free++;
            if (free == 0) return -1;
            int r = rng.R(free);
            for (int i = 0; i < n; i++) { if ((blocked & (1L << i)) != 0 || i == last) continue; if (r-- == 0) return i; }
            return -1;
        }

        // ------------------------------------------------------------------ situations
        /// <summary>
        /// The situation tags true for this speaker right now, most specific first:
        /// afterdeath · aftertrial · grief · afraid · angry · stressed · tired · hunger · quiet · rule_CHxx · near_Pxx · stage1-4 (with 민혁) ·
        /// loop2 / loop3 · at_&lt;RoomType&gt; · morning / day / evening / night.
        /// </summary>
        public static List<string> Situations(Simulation sim, string speaker, string listener)
        {
            var S = sim.S; var res = new List<string>(); var a = S.A(speaker);
            // a death this loop that the speaker knows about, confirmed within a day
            var k = S.Know.TryGetValue(speaker, out var kk) ? kk : null;
            if (S.Incidents.Values.Any(i => i.Loop == S.Loop && i.Confirmed && i.ConfirmClock >= 0 && S.Clock - i.ConfirmClock < 24 * 60 && (k == null || k.KnownDead.Contains(i.Victim) || speaker == Cast.Butler))) res.Add("afterdeath");
            if (S.Chapter > 1 && S.Settlements.Any(s => s.Loop == S.Loop) && S.Clock - S.Ch.ChapterStartClock < 24 * 60) res.Add("aftertrial");
            if (a != null)
            {
                var n = a.Needs;
                if (n.Grief > 0.35f) res.Add("grief");
                if (n.Fear > 0.5f) res.Add("afraid");
                if (n.Anger > 0.5f) res.Add("angry");
                if (n.Stress > 0.6f) res.Add("stressed");
                if (n.Energy < 0.25f) res.Add("tired");
            }
            if (S.Flags.TryGetValue($"hunger:{S.Loop}:{S.Chapter}", out var hv) && hv >= 1) res.Add("hunger");
            else if (S.Phase == Phase.Daily && S.Clock - QuietSince(S) > 24 * 60) res.Add("quiet");
            foreach (var r in S.Ch.Rules) if (r.Active && r.Announced && r.Rule != null) res.Add("rule_" + r.Rule);
            if (a != null && a.Room >= 0)
                foreach (var o in S.Actors.Values.Where(o => o.Id != speaker && o.Id != listener && o.Alive && !o.IsButler && o.Room == a.Room && o.Pose != Pose.Sleep).Select(o => o.Id).OrderBy(x => x, StringComparer.Ordinal))
                    res.Add("near_" + o);
            if (listener == Cast.Player && speaker != Cast.Player) { int st = sim.BondStage(speaker); if (st > 0) res.Add("stage" + Math.Min(4, st)); }
            if (S.Loop >= 3) res.Add("loop3"); if (S.Loop >= 2) res.Add("loop2");
            if (a != null && a.Room >= 0) { var room = S.Layout?.Room(a.Room); if (room != null) res.Add("at_" + room.Type); }
            int m = S.Minute;
            res.Add(m >= 5 * 60 && m < 11 * 60 ? "morning" : m >= 11 * 60 && m < 17 * 60 ? "day" : m >= 17 * 60 && m < 21 * 60 ? "evening" : "night");
            return res;
        }

        static double QuietSince(GameState S)
        {
            double t = S.Ch.ChapterStartClock;
            foreach (var i in S.Incidents.Values) if (i.Loop == S.Loop && i.Confirmed && i.ConfirmClock > t) t = i.ConfirmClock;
            return t;
        }

        // ------------------------------------------------------------------ the presentation path (Game side)
        static readonly Dictionary<string, (int last, long used)> _presMem = new Dictionary<string, (int, long)>();
        static readonly Rng _presRng = new Rng(0xB123C0FFEEUL, 23);

        /// <summary>
        /// A per-character line for presentation-only barks (IntentLines, idle barks): the speaker's OWN pool for key
        /// (no ANY fallback, so the caller keeps its own table), non-repeating, WITHOUT touching kernel state (no Flags, no
        /// kernel RNG) — safe to call from Unity at any frame. Returns the rendered text, or null.
        /// </summary>
        public static string Presentation(string speaker, string key, bool? casual = null, IDictionary<string, string> slots = null)
        {
            if (speaker == null || key == null) return null;
            bool cas = casual ?? !(Cast.Get(speaker)?.Speech.PoliteDefault ?? true);
            var pool = LineBank.Pool(speaker, key, cas, out var used);
            if (pool == null || pool.Length == 0) return null;
            int pick = 0;
            if (pool.Length > 1)
            {
                string mk = speaker + ":" + key + (used ? ":C" : ":P");
                _presMem.TryGetValue(mk, out var mem); int n = Math.Min(pool.Length, 62); long all = n >= 63 ? -1L : (1L << n) - 1; long u = mem.used & all; if (u == all) u = 0;
                var free = Enumerable.Range(0, n).Where(i => (u & (1L << i)) == 0 && i != mem.last - 1).ToList(); if (free.Count == 0) free = Enumerable.Range(0, n).Where(i => i != mem.last - 1).ToList();
                pick = free[_presRng.R(free.Count)]; _presMem[mk] = (pick + 1, u | (1L << pick));
            }
            return LineBank.Render(pool[pick], slots ?? new Dictionary<string, string>(), cas);
        }

        /// <summary>IntentLines hook: "intent_&lt;tag&gt;" from the speaker's voice pack ("차나 한잔해야겠다" in their own words), or null.</summary>
        public static string Intent(string speaker, string activityTag) => activityTag == null ? null : Presentation(speaker, "intent_" + activityTag);
    }

    public static partial class LineBank
    {
        public const string Shared = "ANY";

        /// <summary>The register-resolved pool an actor has for a key (no ANY fallback): casual prefers C, polite prefers P, else the other.</summary>
        public static string[] Pool(string actor, string key, bool casual, out bool usedCasual)
        {
            Ensure(); usedCasual = casual;
            if (actor == null || key == null || !_d.TryGetValue(actor, out var m) || !m.TryGetValue(key, out var set)) return null;
            if (casual) { if (set.C.Length > 0) return set.C; usedCasual = false; return set.P; }
            if (set.P.Length > 0) return set.P; usedCasual = true; return set.C;
        }

        static Dictionary<string, HashSet<string>> _suffixed;
        /// <summary>Does the actor have any "key&lt;sep&gt;…" variants (sep '@', '#', '~')? Cheap pre-check so situations are computed only when used.</summary>
        public static bool HasSuffixed(string actor, string key, char sep)
        {
            Ensure();
            if (_suffixed == null)
            {
                var d = new Dictionary<string, HashSet<string>>();
                foreach (var kv in _d)
                {
                    var hs = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var k in kv.Value.Keys) { int i = k.IndexOfAny(new[] { '@', '#', '~' }); if (i > 0) hs.Add(k.Substring(0, i) + k[i]); }
                    d[kv.Key] = hs;
                }
                _suffixed = d;
            }
            return actor != null && _suffixed.TryGetValue(actor, out var h) && h.Contains(key + sep);
        }

        /// <summary>All variants (both registers) an actor has for a key, own bank only. Used by the authoring/voice tests.</summary>
        public static int Variants(string actor, string key) { Ensure(); return actor != null && _d.TryGetValue(actor, out var m) && m.TryGetValue(key, out var s) ? s.P.Length + s.C.Length : 0; }

        // ---- voice-pack authoring helpers (Sim/Content/Voice/Voice_*.cs). Same storage as Add: they simply build the suffixed keys.
        /// <summary>Generic lines for a key (both registers). Same as Add.</summary>
        static void V(string who, string key, string[] polite, string[] casual = null) => Add(who, key, polite, casual);
        /// <summary>Casual-only generic lines.</summary>
        static void VC(string who, string key, params string[] casual) => Add(who, key, null, casual);
        /// <summary>Polite-only generic lines.</summary>
        static void VP(string who, string key, params string[] polite) => Add(who, key, polite, null);
        /// <summary>Lines said TO one resident (key@to).</summary>
        static void Pair(string who, string key, string to, string[] polite, string[] casual = null) => Add(who, key + "@" + to, polite, casual);
        static void PairC(string who, string key, string to, params string[] casual) => Add(who, key + "@" + to, null, casual);
        static void PairP(string who, string key, string to, params string[] polite) => Add(who, key + "@" + to, polite, null);
        /// <summary>Lines said ABOUT one resident given in {t}/{victim}/{t2} (key#subject).</summary>
        static void About(string who, string key, string subject, string[] polite, string[] casual = null) => Add(who, key + "#" + subject, polite, casual);
        static void AboutC(string who, string key, string subject, params string[] casual) => Add(who, key + "#" + subject, null, casual);
        static void AboutP(string who, string key, string subject, params string[] polite) => Add(who, key + "#" + subject, polite, null);
        /// <summary>Lines for a situation tag (key~tag): see LineContext.Situations for the tag list.</summary>
        static void Ctx(string who, string key, string tag, string[] polite, string[] casual = null) => Add(who, key + "~" + tag, polite, casual);
        static void CtxC(string who, string key, string tag, params string[] casual) => Add(who, key + "~" + tag, null, casual);
        static void CtxP(string who, string key, string tag, params string[] polite) => Add(who, key + "~" + tag, polite, null);
    }
}
