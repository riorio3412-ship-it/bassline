using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace BL23.Sim
{
    /// <summary>
    /// DAILY LIFE kernel (DailyLifeDesign §3–§8). One entry per clock minute (LifeAI.Schedule → LifeMinute), the scene runner
    /// shared by every kind of daily scene, and the two doors to the presentation:
    ///   · dialogue (PlayerApi.Options / Choose → LifeOptions / LifeChoose): hearts, hangout moments, the purposes residents
    ///     come to 민혁 with (a gift, jealousy, a rumour, a callback), gifts, rumour tracing/correction;
    ///   · staged scenes (Game/UI/LifeSceneUI → LifeOffer / LifePick / LifeTable): pair scenes, the table, festivals, the memorial.
    /// Determinism: randomness only from S.R(Stream.Dialogue); iteration over ordinal-sorted ids; state only in S.Flags,
    /// Knowledge.Facts and Rel (the save round-trip stays IDENTICAL). Runtime-only: the scene being played and the queue of
    /// lines still to be said aloud (like conversations, they are not saved).
    /// </summary>
    public sealed partial class Simulation
    {
        // ================================================================== state helpers
        Rng LR => S.R(Stream.Dialogue);
        Knowledge PK => S.K(Cast.Player);
        double LF(string key, double def = 0) => S.Flags.TryGetValue(key, out var v) ? v : def;
        void LFinc(string key, double by = 1) => S.Flags[key] = LF(key) + by;

        /// <summary>A resident who can take part in a scene right now (awake, free, not scheming, not busy with someone else).</summary>
        internal bool LifeFree(Actor a, bool allowTalk = false)
        {
            if (a == null || a.IsPlayer || a.IsButler || !a.Alive || a.Status != ActorStatus.Active) return false;
            if (a.Pose == Pose.Sleep || a.StairId >= 0 || a.CarriedBy != null || a.Carrying != null || a.PlanId != null) return false;
            if (!allowTalk && a.TalkingTo != null) return false;
            if (a.Act != null && (!a.Act.Interruptible || (a.Act.Id != null && (a.Act.Id.StartsWith("murder") || a.Act.Id.StartsWith("case") || a.Act.Id.StartsWith("social:together"))))) return false;
            if (a.Body.Critical || a.Needs.Fear > 0.8f) return false;
            return true;
        }

        /// <summary>민혁 is up and about in daily life (not asleep, not in a skip-with-someone, not in the 심판).</summary>
        internal bool LifePlayerAround()
        {
            var me = S.Player; if (me == null || !me.Alive || me.Status != ActorStatus.Active || me.Pose == Pose.Sleep) return false;
            return S.Phase == Phase.Daily;
        }

        internal bool PlayerInTogether() => S.LivingNpcs.Any(x => x.Act != null && x.Act.Id == "social:together:" + Cast.Player);

        /// <summary>The nickname a resident uses for 민혁 ({nick}); their plain address when none was given yet.</summary>
        public string LifeNick(string npc)
        {
            if (npc == null || npc == Cast.Player) return "민혁";
            string pre = "life:nick:" + npc + ":";
            foreach (var f in PK.Facts) if (f.StartsWith(pre, StringComparison.Ordinal)) return f.Substring(pre.Length);
            return CallName(npc, Cast.Player);
        }
        void SetNick(string npc, string nick)
        {
            string pre = "life:nick:" + npc + ":";
            foreach (var f in PK.Facts.Where(f => f.StartsWith(pre, StringComparison.Ordinal)).ToList()) PK.Facts.Remove(f);
            PK.Facts.Add(pre + nick);
        }

        public bool LifeMem(string npc, string key) => PK.Facts.Contains("life:mem:" + npc + ":" + key);
        void Remember(string npc, string key) { if (npc == null || key == null) return; PK.Facts.Add("life:mem:" + npc + ":" + key); S.Flags["lmem:" + npc + ":" + key] = S.Day; }

        /// <summary>Heart events done with this resident this loop (0..4).</summary>
        public int LifeHearts(string npc) => (int)LF("lh:" + npc);

        /// <summary>A confirmed death this loop that the house knows of, and when.</summary>
        internal Incident LifeLastDeath() => S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Confirmed && i.ConfirmClock >= 0).OrderByDescending(i => i.ConfirmClock).ThenBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
        internal bool LifeAfterDeath(double hours = 24) { var d = LifeLastDeath(); return d != null && S.Clock - d.ConfirmClock < hours * 60; }

        static string TimeTag(int m) => m >= 5 * 60 && m < 11 * 60 ? "morning" : m >= 11 * 60 && m < 17 * 60 ? "day" : m >= 17 * 60 && m < 21 * 60 ? "evening" : "night";

        bool TimeFits(string when)
        {
            if (string.IsNullOrEmpty(when)) return S.Minute >= 7 * 60 && S.Minute < 23 * 60;
            string tag = TimeTag(S.Minute);
            foreach (var w in when.Split('|', ';', ','))
            {
                if (w == tag) return true;
                if (w == "meal" && MealTime(out _)) return true;
                if (w == "any") return true;
            }
            return false;
        }

        bool RoomFits(string where, Room r)
        {
            if (r == null || r.Void) return false;
            if (string.IsNullOrEmpty(where)) return !RoomInfo.IsPassage(r.Type) && r.Type != RoomType.Elevator && r.Type != RoomType.Courtroom;
            foreach (var w in where.Split(';', '|', ',')) if (w == r.Type.ToString()) return true;
            return false;
        }

        // ================================================================== conditions
        static readonly Regex _cmp = new Regex(@"^(\w+):?([A-Za-z0-9_]*):?([A-Za-z0-9_]*)(<=|>=|==|<|>)(-?[0-9.]+)$");

        /// <summary>Evaluates a scene condition: ','-joined AND, '!' negates. See LL for the forms (mem:, fact:, casual:, day&gt;=, …).</summary>
        public bool LifeIf(string cond, SceneRun run = null)
        {
            if (string.IsNullOrEmpty(cond)) return true;
            foreach (var raw in cond.Split(','))
            {
                var c = raw.Trim(); if (c.Length == 0) continue;
                bool neg = c.StartsWith("!"); if (neg) c = c.Substring(1);
                bool v = LifeIf1(c, run);
                if (v == neg) return false;
            }
            return true;
        }

        string Who(string id, SceneRun run)
        {
            if (id == "$" || id == "npc") return run?.Npc;
            if (id == "me") return Cast.Player;
            if (run != null && run.Ctx.TryGetValue(id, out var v) && v != null && (v.StartsWith("P") || v == Cast.Butler)) return v;
            return id;
        }

        bool LifeIf1(string c, SceneRun run)
        {
            var parts = c.Split(':');
            switch (parts[0])
            {
                case "mem": return parts.Length >= 3 && LifeMem(Who(parts[1], run), string.Join(":", parts.Skip(2)));
                case "fact": { if (parts.Length < 3) return false; var who = Who(parts[1], run); return S.K(who).Facts.Contains(string.Join(":", parts.Skip(2))); }
                case "casual": { var n = Who(parts.Length > 1 ? parts[1] : "$", run); return n != null && S.HasRel(n, Cast.Player) && S.R(n, Cast.Player).Casual; }
                case "dead": return LifeLastDeath() != null;
                case "afterdeath": return LifeAfterDeath();
                case "deadid": { var id = Who(parts[1], run); var a = S.A(id); return a != null && !a.Alive; }
                case "alive": { var id = Who(parts[1], run); var a = S.A(id); return a != null && a.Alive; }
                case "present": { var id = Who(parts[1], run); var a = S.A(id); return a != null && a.Alive && run != null && a.Room == run.Room && a.Pose != Pose.Sleep; }
                case "rule": return S.Ch.Rules.Any(r => r.Rule == parts[1] && r.Active && r.Announced);
                case "hunger": return S.Flags.TryGetValue($"hunger:{S.Loop}:{S.Chapter}", out var hv) && hv >= 1;
                case "morning": case "day": case "evening": case "night": return TimeTag(S.Minute) == parts[0];
                case "meal": return MealTime(out _);
                case "lover": { var n = Who(parts[1], run); return n != null && S.R(n, Cast.Player).Tags.Contains("lover"); }
                case "tag": { if (parts.Length < 4) return false; return S.R(Who(parts[1], run), Who(parts[2], run)).Tags.Contains(parts[3]); }
                case "flag": return S.Flags.ContainsKey(string.Join(":", parts.Skip(1)));
            }
            // comparisons: day>=2, loop>=2, bond:P03>=2, heart:P03>=1, like:P07:P12>0.3 (also trust, attach, grudge, romance, jealous)
            var m = _cmp.Match(c);
            if (!m.Success) return false;
            string k = m.Groups[1].Value, x = m.Groups[2].Value, y = m.Groups[3].Value, op = m.Groups[4].Value;
            double rhs = double.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture), lhs;
            switch (k)
            {
                case "day": lhs = S.Day; break;
                case "score": lhs = run?.Score ?? 0; break;
                case "picks": lhs = run?.Picks ?? 0; break;
                case "loop": lhs = S.Loop; break;
                case "chapter": lhs = S.Chapter; break;
                case "hour": lhs = S.Minute / 60.0; break;
                case "bond": lhs = BondStage(Who(x, run)); break;
                case "heart": lhs = LifeHearts(Who(x, run)); break;
                case "dead": lhs = S.Incidents.Values.Count(i => i.Loop == S.Loop && i.Confirmed); break;
                case "like": case "trust": case "attach": case "grudge": case "romance": case "jealous": case "respect":
                    {
                        string from = Who(x, run), to = Who(string.IsNullOrEmpty(y) ? "me" : y, run); if (from == null || to == null) return false;
                        var r = S.R(from, to); lhs = k == "like" ? r.Like : k == "trust" ? r.Trust : k == "attach" ? r.Attach : k == "grudge" ? r.Grudge : k == "romance" ? r.Romance : k == "jealous" ? r.Jealous : r.Respect; break;
                    }
                default: return false;
            }
            switch (op) { case "<=": return lhs <= rhs; case ">=": return lhs >= rhs; case "==": return Math.Abs(lhs - rhs) < 1e-6; case "<": return lhs < rhs; case ">": return lhs > rhs; }
            return false;
        }

        // ================================================================== rendering
        /// <summary>Who a line is said to: 민혁's lines go to the scene's resident; in a pair scene A and B talk to each other.</summary>
        string ListenerOf(string speaker, SceneRun run)
        {
            if (speaker == Cast.Player) return run.Npc ?? run.Cast.FirstOrDefault(x => x != Cast.Player);
            if (run.Kind == "pair" && run.Ctx.TryGetValue("a", out var a) && run.Ctx.TryGetValue("b", out var b))
            {
                if (speaker == a) return b; if (speaker == b) return a;
            }
            if (run.Ctx.TryGetValue("to:" + speaker, out var to)) return to;
            return Cast.Player;
        }

        bool CasualTo(string speaker, string listener)
        {
            if (speaker == null || listener == null || speaker == Cast.Butler) return false;
            if (S.HasRel(speaker, listener)) return S.R(speaker, listener).Casual;
            return !(Cast.Get(speaker)?.Speech.PoliteDefault ?? true);
        }

        /// <summary>Authored text → spoken text for this speaker and listener (slots, particles, time joins).</summary>
        internal string LifeRenderText(string template, string speaker, string listener, SceneRun run)
        {
            if (template == null) return null;
            bool casual = CasualTo(speaker, listener);
            var d = new Dictionary<string, string>();
            d["me"] = speaker == Cast.Player ? "나" : CallName(speaker, Cast.Player);
            d["nick"] = LifeNick(speaker);
            d["you"] = listener != null ? CallName(speaker, listener) : "";
            if (run != null)
                foreach (var kv in run.Ctx)
                {
                    if (kv.Key.StartsWith("to:")) continue;
                    string v = kv.Value;
                    if (kv.Key == "place" && int.TryParse(v, out var rid)) v = S.RoomName(rid);
                    else if (kv.Key == "time" && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var tm)) v = ClockFmt.Mark(tm, true);
                    else if (v != null && v.Length >= 3 && v[0] == 'P' && char.IsDigit(v[1]) && Cast.Get(v) != null) v = speaker == v ? (speaker == Cast.Player ? "나" : "저") : CallName(speaker, v);
                    d[kv.Key] = v;
                }
            if (!d.ContainsKey("place")) d["place"] = S.RoomName(run != null && run.Room >= 0 ? run.Room : (S.A(speaker)?.Room ?? -1));
            return LineBank.FixParticles(LineBank.Render(template, d, casual));
        }

        internal Utterance LifeU(LL l, SceneRun run)
        {
            string who = l.Who == "me" ? Cast.Player : Who(l.Who, run) ?? l.Who;
            if (l.Text != null && l.Text.Length > 1 && l.Text[0] == '\u0001')
            {
                // a line from the speaker's voice pack (table topics, festival voices): "\u0001key[\u0002listener]"
                string body = l.Text.Substring(1), to = null; int k = body.IndexOf('\u0002');
                if (k >= 0) { to = body.Substring(k + 1); body = body.Substring(0, k); }
                if (to == null && run.Kind != "table" && run.Kind != "fest") to = ListenerOf(who, run);
                var ku = LifeKeyU(who, to, body, run, l.Emo, l.Gest);
                if (ku != null && run.Kind == "heart") ku.Key = "bond";
                return ku;
            }
            string li = ListenerOf(who, run);
            bool casual = CasualTo(who, li);
            string raw = casual && l.TextC != null ? l.TextC : l.Text;
            string text = LifeRenderText(raw, who, li, run);
            return new Utterance { Speaker = who, Listener = li, Key = run.Kind == "heart" ? "bond" : "life", Text = text, Emotion = l.Emo, Gesture = l.Gest, Seconds = Math.Max(2.5, (text?.Length ?? 0) * 0.08) };
        }

        /// <summary>A line said by someone from a key in their voice pack (Content/Life_*.cs keys), as an utterance in a run.</summary>
        internal Utterance LifeKeyU(string speaker, string listener, string key, SceneRun run, Emotion e = Emotion.Neutral, Anim g = Anim.Talk)
        {
            var slots = new Dictionary<string, string>();
            if (run != null)
                foreach (var kv in run.Ctx)
                {
                    if (kv.Key.StartsWith("to:")) continue;
                    string v = kv.Value;
                    if (kv.Key == "place" && int.TryParse(v, out var rid)) v = S.RoomName(rid);
                    else if (kv.Key == "time" && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var tm)) v = ClockFmt.Mark(tm, true);
                    else if (v != null && v.Length >= 3 && v[0] == 'P' && char.IsDigit(v[1]) && Cast.Get(v) != null) v = "@" + v;
                    slots[kv.Key] = v;
                }
            slots["nick"] = LifeNick(speaker);
            string used = key, text = null;
            // a key this speaker lacks: the nearest everyday key in their OWN voice beats the shared line
            if (!LineBank.Has(speaker, key) && !LineBank.HasSuffixed(speaker, key, '@') && KeyFallback.TryGetValue(key, out var alts))
                foreach (var alt in alts) { if (!LineBank.Has(speaker, alt)) continue; text = Render(speaker, listener, alt, slots); if (!string.IsNullOrEmpty(text)) { used = alt; break; } }
            if (string.IsNullOrEmpty(text)) { text = Render(speaker, listener, key, slots); used = key; }
            if (string.IsNullOrEmpty(text)) return null;
            return new Utterance { Speaker = speaker, Listener = listener, Key = used, Text = text, Emotion = e, Gesture = g, Seconds = Math.Max(2.5, text.Length * 0.08) };
        }

        /// <summary>When a daily-life key has no line for this speaker: the closest everyday key their voice pack does have.</summary>
        static readonly Dictionary<string, string[]> KeyFallback = new Dictionary<string, string[]>
        {
            ["tt_conflict"] = new[] { "argue_open" }, ["tt_verdict"] = new[] { "after_trial_guilt", "after_trial_relief" }, ["tt_verdict_re"] = new[] { "after_trial_relief", "after_trial_guilt" },
            ["tt_food"] = new[] { "meal" }, ["tt_food_re"] = new[] { "meal" }, ["tt_hunger_re"] = new[] { "meal" }, ["tt_death"] = new[] { "grief_close", "grief" }, ["tt_death_re"] = new[] { "grief" },
            ["tt_event"] = new[] { "gathering_invite" }, ["tt_event_re"] = new[] { "gathering_yes" }, ["tt_rule"] = new[] { "react_rule" }, ["tt_rule_re"] = new[] { "react_rule" },
            ["tt_envelope_re"] = new[] { "env_open" }, ["rumour_deny"] = new[] { "argue_reply" }, ["tt_rumour"] = new[] { "rumour_tell" },
            ["memorial_word"] = new[] { "grief" }, ["grief_act"] = new[] { "grief" }, ["grief_share_silent"] = new[] { "comforted" },
        };

        // ================================================================== the scene runner
        List<LOpt> VisibleOpts(SceneRun run) => run.Beat == null ? new List<LOpt>() : run.Beat.Opts.Where(o => LifeIf(o.If, run)).ToList();

        internal string OptLabel(LOpt o, SceneRun run)
        {
            bool casual = run.Npc != null && S.HasRel(Cast.Player, run.Npc) && S.R(Cast.Player, run.Npc).Casual;
            string raw = casual && o.LabelC != null ? o.LabelC : o.Label;
            return LifeRenderText(raw, Cast.Player, run.Npc ?? ListenerOf(Cast.Player, run), run);
        }

        /// <summary>Plays from the current beat until a choice (or the end). Returns the lines.</summary>
        internal List<Utterance> RunFrom(SceneRun run)
        {
            var res = new List<Utterance>(); int guard = 0;
            while (run.Beat != null && guard++ < 20)
            {
                foreach (var l in run.Beat.Lines) if (LifeIf(l.If, run)) { var u = LifeU(l, run); if (u != null && !string.IsNullOrEmpty(u.Text)) res.Add(u); }
                if (VisibleOpts(run).Count > 0) return res;
                var next = run.Beat.Next != null ? run.Scene.Beat(run.Beat.Next) : null;
                run.Beat = next;
            }
            run.Over = true; run.Beat = null;
            return res;
        }

        /// <summary>민혁 picks option i of the current beat: his line, the reply, the effects, then on to the next beat.</summary>
        internal List<Utterance> RunPick(SceneRun run, int i)
        {
            var res = new List<Utterance>();
            var opts = VisibleOpts(run); if (opts.Count == 0) { run.Over = true; return res; }
            if (i < 0 || i >= opts.Count) i = 0;
            var o = opts[i];
            if (!o.Silent) res.Add(new Utterance { Speaker = Cast.Player, Listener = run.Npc ?? ListenerOf(Cast.Player, run), Key = run.Kind == "heart" ? "bond" : "life", Text = OptLabel(o, run), Emotion = Emotion.Neutral, Gesture = Anim.Talk });
            foreach (var l in o.Reply) if (LifeIf(l.If, run)) { var u = LifeU(l, run); if (u != null && !string.IsNullOrEmpty(u.Text)) res.Add(u); }
            ApplyOpt(run, o);
            run.Picks++;
            string nx = o.NextBeat ?? run.Beat.Next;
            if (nx == "!end" || nx == null) { run.Beat = null; run.Over = true; return res; }
            run.Beat = run.Scene.Beat(nx);
            if (run.Beat == null) { run.Over = true; return res; }
            res.AddRange(RunFrom(run));
            return res;
        }

        void ApplyOpt(SceneRun run, LOpt o)
        {
            string npc = o.About ?? run.Npc;
            foreach (var fx in o.Fx) ApplyFx(fx, run);
            run.Score += o.Score;
            if (o.MemKey != null)
            {
                if (o.About == null && (run.Kind == "pair" || run.Kind == "table" || run.Kind == "fest")) { foreach (var id in run.Cast.Where(x => x != Cast.Player)) Remember(id, o.MemKey); }
                else Remember(npc, o.MemKey);
            }
            if (o.CallbackLine != null && npc != null) PK.Facts.Add($"life:cb:{npc}:{S.Day}:{o.CallbackLine}");
            if (o.FactList != null) foreach (var f in o.FactList.Split(';')) LifeFact(npc, f.Trim(), run);
            if (o.NickName != null && npc != null) SetNick(npc, o.NickName);
            if (o.MakeCasual && npc != null)
            {
                S.R(npc, Cast.Player).Casual = true; S.R(Cast.Player, npc).Casual = true;
                Relations.Change(S, npc, Cast.Player, like: 0.02f, memory: "말을 편하게 하기로 했다");
            }
            if (o.GiftType != null && npc != null) LifeGiveItem(npc, o.GiftType);
            if (o.RumourKind != null) SeedRumour(o.RumourKind, run.Ctx.TryGetValue("a", out var ra) ? ra : npc, run.Ctx.TryGetValue("b", out var rb) ? rb : null, npc ?? Cast.Player, run.Room, true, run.Scene?.Id);
            if (o.TieState != null) LifeTie(o.TieState);
            if (o.NoteText != null) run.Note = LifeRenderText(o.NoteText, Cast.Player, npc, run);
            if (npc != null && (run.Kind == "heart" || run.Kind == "hang")) LFinc($"lfav:{S.Day}:{npc}");
        }

        void ApplyFx(LFx fx, SceneRun run)
        {
            string from = Who(fx.From, run), to = Who(fx.To, run);
            if (from == null || to == null || from == to || S.A(from) == null || S.A(to) == null) return;
            if (!(S.A(from).Alive) && from != Cast.Player) return;
            string mem = fx.Memory != null ? LifeRenderText(fx.Memory, from, to, run) : null;
            Relations.Change(S, from, to, like: fx.Like, trust: fx.Trust, respect: fx.Respect, attach: fx.Attach, romance: fx.Romance, fear: fx.Fear, grudge: fx.Grudge, jealous: fx.Jealous, memory: mem, tag: fx.Tag, untag: fx.Untag);
        }

        /// <summary>A notebook fact from a choice (see LOpt.Know).</summary>
        void LifeFact(string npc, string f, SceneRun run)
        {
            if (string.IsNullOrEmpty(f)) return;
            string Body(string pre) => LifeRenderText(f.Substring(pre.Length), Cast.Player, npc, run);
            if (f.StartsWith("note:")) { if (npc != null) PK.Facts.Add("bondnote:" + npc + ":" + Body("note:")); return; }
            if (f.StartsWith("likes:")) { if (npc != null) PK.Facts.Add("likes:" + npc + ":" + f.Substring(6)); return; }
            if (f.StartsWith("habit:")) { if (npc != null) { PK.Facts.Add("bondnote:" + npc + ":" + Body("habit:")); PK.Facts.Add("habit:" + npc); Foreshadow.HabitShared(this, npc, "life", new[] { Cast.Player }); } return; }
            if (f == "tell") { if (npc != null) LearnTell(npc, run); return; }
            if (f == "hint" || f == "contract" || f == "secret") { if (npc != null) PK.Facts.Add(f + ":" + npc); return; }
            if (f.StartsWith("knot:")) { CastWeb.RevealKnot(S, f.Substring(5), Cast.Player); return; }
            if (f.StartsWith("flag:")) { S.Flags[f.Substring(5)] = S.Day; return; }
            if (f.StartsWith("mend:")) { foreach (var id in f.Substring(5).Split(',')) Conscience.Mend(S, id, 0.03f, "pact-remind"); return; }
            PK.Facts.Add(f);
        }

        /// <summary>F8: 민혁 has seen through a bluff: the resident's tell goes into the notebook (performed whether or not it was
        /// learned — the notebook only makes it legible). 진우 fakes one until bond III.</summary>
        internal void LearnTell(string npc, SceneRun run)
        {
            var t = CastTraits.Get(npc); if (t == null || t.TellGesture == null) return;
            if (npc == "P02" && BondStage("P02") < 3) { if (PK.Facts.Add("tell-fake:P02")) { PK.Facts.Add("bondnote:P02:거짓말할 때 사탕 껍질을 만지작거린다 — 본인 말로는 가짜 버릇이라고 한다"); if (run != null) run.Note = "수첩에 적었다 — 진우의 버릇(…진짜일까?)"; } return; }
            if (!PK.Facts.Add("tell:" + npc)) return;
            string text = t.TellGesture + (t.TellPhrase != null ? $" ('{t.TellPhrase}')" : "");
            PK.Facts.Add("bondnote:" + npc + ":거짓말할 때 — " + text);
            if (run != null) run.Note = LineBank.FixParticles($"수첩에 적었다 — {Cast.GivenOf(npc)}은(는) 거짓말할 때 {t.TellGesture}");
            S.Log("TellLearned", Cast.Player, npc, data: text);
        }

        void LifeTie(string state)
        {
            S.Flags["ltie:" + state] = S.Day;
            S.Log("TieShift", null, data: state);
        }

        /// <summary>A small thing given to 민혁 (a crafted gift, bread, a hot pack): a real item in his pocket.</summary>
        internal Item LifeGiveItem(string from, string type)
        {
            var me = S.Player; var def = ItemCatalog.Get(type); if (me == null || def == null) return null;
            var it = new Item { Id = S.NewId("it"), Type = type, Name = $"{Cast.GivenOf(from)}에게 받은 {def.Kor}", Owner = Cast.Player, Pos = me.Pos, Room = me.Room, HomeRoom = me.Room, HomePos = me.Pos };
            S.Items[it.Id] = it;
            S.Emit(GameEventType.ItemMoved, Cast.Player, data: it.Id, text: "spawn", pos: me.Pos);
            PickUp(me, it);
            S.Log("LifeGift", from, Cast.Player, item: it.Id, data: type);
            return it;
        }

        // ================================================================== runtime: the scene being staged, lines said aloud over time
        SceneRun _stage;                                   // the staged scene in progress (LifeSceneUI); not saved
        readonly Dictionary<string, SceneRun> _dlg = new Dictionary<string, SceneRun>();   // dialogue scenes by resident; not saved
        readonly List<(double at, string who, string to, string text)> _sayQ = new List<(double, string, string, string)>();

        /// <summary>Queue lines to be said aloud one after another (every ~1.5 clock minutes) — NPC-only scenes are overheard.</summary>
        internal void LifeSayLater(IEnumerable<Utterance> lines, double start = 0.5, double gap = 1.2)
        {
            double t = Math.Max(S.Clock + start, _sayQ.Count > 0 ? _sayQ[_sayQ.Count - 1].at + gap : 0);
            foreach (var u in lines) { if (u == null || string.IsNullOrEmpty(u.Text) || u.Speaker == Cast.Player) continue; _sayQ.Add((t, u.Speaker, u.Listener, u.Text)); t += gap; }
            if (_sayQ.Count > 60) _sayQ.RemoveRange(0, _sayQ.Count - 60);
        }

        void FlushSay()
        {
            while (_sayQ.Count > 0 && _sayQ[0].at <= S.Clock)
            {
                var (at, who, to, text) = _sayQ[0]; _sayQ.RemoveAt(0);
                var a = S.A(who); if (a == null || !a.Alive || a.Pose == Pose.Sleep || a.Status != ActorStatus.Active) continue;
                LifeSayNow(a, to, text, "life");
            }
        }

        /// <summary>Say an already-rendered line aloud (the same record and sound as Speak, without a key lookup).</summary>
        internal void LifeSayNow(Actor a, string listener, string text, string key)
        {
            if (a == null || string.IsNullOrEmpty(text) || a.Body.Speech < 0.2f) return;
            a.LastSaid = text; a.LastSaidAt = S.Clock;
            S.Log("Speech", a.Id, listener, room: a.Room, pos: a.Pos, data: key + "|" + text);
            S.Emit(GameEventType.Speech, a.Id, listener, text: text, room: a.Room, pos: a.Pos, key: key);
            Sound(SoundKind.Talk, a.Pos, 0.22f, a.Id, a.Id);
            LifeLineCount(a.Id, text);
        }

        /// <summary>Test hook (SimTests "life"): every daily-life line said or staged (speaker, text).</summary>
        public static Action<string, string> LifeLineTrace;
        void LifeLineCount(string who, string text) { LifeLineTrace?.Invoke(who, text); }

        // ================================================================== the minute
        int _lifeLastMinute = -1;
        /// <summary>Hook: LifeAI.Schedule, once per clock minute (all daily-life systems; each guarded).</summary>
        internal void LifeMinute(int mod)
        {
            int m = (int)Math.Floor(S.Clock); if (m == _lifeLastMinute) return; _lifeLastMinute = m;
            LifeGuard("say", FlushSay);
            LifeGuard("aftermath", () => AftermathMinute(mod));
            if (S.Phase != Phase.Daily) return;
            if (mod % 5 == 0) LifeGuard("pairs", () => PairTick(mod));
            LifeGuard("table", () => TableTick(mod));
            if (mod % 10 == 0) LifeGuard("hearts", () => HeartTick(mod));
            LifeGuard("fest", () => FestMinute(mod));
            if (mod % 10 == 5) LifeGuard("rumours", () => RumourTick(mod));
            if (mod % 15 == 0) LifeGuard("seek", () => SeekTick(mod));
            if (mod == 20 * 60) LifeGuard("jealous", JealousyTick);
            LifeGuard("foreshadow", () => Foreshadow.Tick(this));
        }

        /// <summary>Ledger events after a sequence number (the ledger is in Seq order), without scanning all of it.</summary>
        internal IEnumerable<LedgerEvent> LedgerSince(double seq)
        {
            int i = S.Ledger.Count - 1; while (i >= 0 && S.Ledger[i].Seq > seq) i--;
            for (int j = i + 1; j < S.Ledger.Count; j++) yield return S.Ledger[j];
        }

        void LifeGuard(string what, Action f)
        {
            try { f(); }
            catch (Exception e) { Fault("life:" + what, e); }
        }
    }
}
