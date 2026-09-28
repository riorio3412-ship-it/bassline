using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// The content of the debate (TrialReforge §6.4): for each riddle, 2–4 residents put up a theory — one checkable claim, in
    /// their own words, on their own perception (what they examined, heard, saw) — and the room answers them (the named one
    /// protests, the culprit leans in, someone echoes or doubts). Holders come from who actually knows what; the family only
    /// gives the sentence its shape. Nothing here reads the culprit's plan except the culprit's own lines.
    /// </summary>
    public static partial class TrialSystem
    {
        // ================================================================== helpers
        static DeckClaim KClaim(DebateState D, string id) => D.Deck.Claims.FirstOrDefault(c => c.Id == id);
        static bool FoundPlate(GameState S, string who, Plate p) => p != null && who != null && (p.FoundBy == who || p.Witness == who || S.K(who).Evidence.Any(e => e.Root != null && (e.Root == p.Root || e.Root.EndsWith(p.Root))));
        static double Arg(string x) => (Cast.Get(x)?.Argue ?? 50) / 100.0;
        static double Obs(string x) => (Cast.Get(x)?.Obs ?? 50) / 100.0;
        static double Fearful(GameState S, string x) => S.A(x)?.Def.P.Fearfulness ?? 0.5;
        static bool Honest(GameState S, string x) => (S.A(x)?.Def.P.Honesty ?? 0.5f) >= 0.4f;
        static bool BodyNote(GameState S, string x, string victim, string value) => S.K(x).Evidence.Any(e => !e.Hidden && (e.Kind == EvKind.Body || (e.Root != null && e.Root.StartsWith("bodyexam:"))) && e.Props.Any(p => p.Kind == PropKind.TraceAt && p.Value == value && (p.A == null || p.A == victim)));
        static bool Examined(GameState S, string x, string victim) => S.K(x).Examined.Contains("body:" + victim) || S.K(x).Evidence.Any(e => e.Root != null && (e.Root.StartsWith("bodyexam:" + victim) || e.Root.StartsWith("body:" + victim)));
        static string Glyph(string face) { if (face == null) return "?"; int a = face.IndexOf('「'), b = face.IndexOf('」'); return a >= 0 && b > a ? face.Substring(a + 1, b - a - 1) : "?"; }
        static string Given(string id) => Cast.GivenOf(id) ?? id;
        /// <summary>The same words in the speaker's register (DebateLines picks the register the same way).</summary>
        internal static string Reg(string who, string polite, string casual) => who == Cast.Player || who == Cast.Butler || (Cast.Get(who)?.Speech.PoliteDefault ?? true) ? polite : casual;

        /// <summary>The resident a theory is given to: best score, not someone who has already led, never an excluded person.</summary>
        static string PickHolder(Simulation sim, TrialState T, IEnumerable<string> cands, Func<string, double> score, string salt, params string[] exclude)
        {
            var S = sim.S; var D = T.Debate;
            return cands.Where(x => x != null && x != Cast.Player && !exclude.Contains(x) && T.Participants.Contains(x) && S.A(x)?.Alive == true)
                .OrderByDescending(x => score(x) - (D.Seen.Contains("lead:" + x) ? 0.8 : 0))
                .ThenBy(x => DH(S, salt + ":" + x)).FirstOrDefault();
        }

        /// <summary>A theory put up in court: the slide (a readable line) and its court record (a TrialClaim the screen can target).</summary>
        static Theory Voice(Simulation sim, TrialState T, Mystery m, string holder, Family fam, Basis basis, Prop claim, string lineKey, Dictionary<string, string> slots,
            string fragment, string pin, bool truth, string target = null, string lie = null, string answers = null, bool leans = false, string key = "claim",
            Emotion emo = Emotion.Neutral, Anim gesture = Anim.Talk, string rawText = null, string from = null)
        {
            var S = sim.S; var D = T.Debate;
            var th = new Theory { Id = "t" + (D.Theories.Count + 1), Mystery = m.Id, Holder = holder, Family = fam, Basis = basis, Claim = claim?.Clone(), Fragment = fragment, Pin = pin, True = truth, Target = target, Lie = lie, Answers = answers, Leans = leans, From = from };
            th.Conviction = basis == Basis.Saw ? 0.9f : basis == Basis.Heard ? 0.75f : basis == Basis.Plate ? 0.6f : basis == Basis.Rule ? 1f : basis == Basis.Hearsay ? 0.45f : 0.35f;
            D.Theories.Add(th); m.Theories.Add(th.Id);
            var c = new TrialClaim { Id = "c" + (T.Claims.Count + 1), Speaker = holder, Prop = claim?.Clone(), Key = "debate:" + fam, Lie = lie != null, Topic = "debate", Beat = T.Beats.Count, Accused = target };
            T.Claims.Add(c); th.TrialClaim = c.Id;
            var b = rawText != null ? DText(sim, T, holder, rawText, BeatKind.Slide, key, c.Id, emo, gesture, 0.45f, th.Id, pin)
                                    : DSay(sim, T, holder, lineKey, slots, BeatKind.Slide, key, c.Id, emo, gesture, 0.45f, th.Id, pin);
            th.Text = b.Text; c.Text = b.Text;
            if (target != null && D.Reading.ContainsKey(holder) && holder != D.Target) D.Reading[holder] = target;
            if (pin != null) { var p = DPlate(D, pin); if (p != null && p.State != PlateState.Flipped && p.State != PlateState.Sealed) p.State = PlateState.Shown; }
            if (!D.Seen.Contains("lead:" + holder)) D.Seen.Add("lead:" + holder);
            if (!D.Seen.Contains("theory:" + fam)) D.Seen.Add("theory:" + fam);
            return th;
        }

        /// <summary>Someone backs a theory out loud (they join its supporters; the room sees it).</summary>
        static void Back(Simulation sim, TrialState T, Theory th, string who, string lineKey, Dictionary<string, string> slots, Emotion emo = Emotion.Neutral, Anim gesture = Anim.Talk, string key = "claim")
        {
            var D = T.Debate; if (who == null || th == null) return;
            DSay(sim, T, who, lineKey, slots, BeatKind.Line, key, th.TrialClaim, emo, gesture, 0.4f, th.Id);
            if (!th.Supporters.Contains(who)) th.Supporters.Add(who);
            if (th.Target != null && D.Reading.ContainsKey(who) && who != D.Target) D.Reading[who] = th.Target;
        }

        /// <summary>An echo from someone who already leaned that way (at most one per theory: the cascade shows the rest).</summary>
        static void PileOn(Simulation sim, TrialState T, Theory th)
        {
            var S = sim.S; var D = T.Debate; if (th == null) return;
            var who = Jurors(S, T).Where(x => x != th.Holder && x != th.Target && x != D.Target && !th.Supporters.Contains(x) && !Protects(S, x, th.Target))
                .OrderByDescending(x => (th.Target != null && D.Reading.TryGetValue(x, out var r) && r == th.Target ? 2 : 0) + (th.Target != null && Resents(S, x, th.Target) ? 1.5 : 0) + (1 - Arg(x)))
                .ThenBy(x => DH(S, "pile:" + th.Id + ":" + x)).FirstOrDefault();
            if (who == null) return;
            Back(sim, T, th, who, "re_pileon", null, Emotion.Neutral, Anim.CrossArms);
            // a couple more quietly turn with them (bodies, not lines)
            foreach (var x in Jurors(S, T).Where(x => x != th.Holder && x != who && x != th.Target && x != D.Target && D.Reading[x] == "?" && !Protects(S, x, th.Target)).OrderBy(x => DH(S, "pile2:" + th.Id + ":" + x)).Take(2))
            { th.Supporters.Add(x); if (th.Target != null) D.Reading[x] = th.Target; }
        }

        /// <summary>A sharp resident questions a theory's basis out loud (a doubt, not a counter-theory).</summary>
        static void Doubt(Simulation sim, TrialState T, Theory th, params string[] exclude)
        {
            var S = sim.S; var D = T.Debate; if (th == null) return;
            var ex = exclude.Concat(new[] { th.Holder, th.Target, D.Target }).ToArray();
            var who = PickHolder(sim, T, Jurors(S, T), x => Arg(x) + Obs(x) * 0.5, "doubt:" + th.Id, ex);
            if (who == null) return;
            DSay(sim, T, who, "re_doubt", new Dictionary<string, string> { { "holder", "@" + th.Holder } }, BeatKind.Interrupt, "object", th.TrialClaim, Emotion.Surprised, Anim.Point, 0.5f, th.Id);
        }

        /// <summary>The one a theory names answers it (an innocent protests; the player is left to the floor).</summary>
        static void Protest(Simulation sim, TrialState T, string who)
        {
            var S = sim.S; var D = T.Debate;
            if (who == null || who == Cast.Player || !Jurors(S, T).Contains(who)) return;
            DSay(sim, T, who, "re_protest", new Dictionary<string, string> { { "victim", "@" + D.Victim } }, BeatKind.Line, "protest", null, who == D.Target ? Emotion.Neutral : Emotion.Surprised, Anim.Surprised, 0.5f);
        }

        /// <summary>At most one tangent per 심판 (1막): a dramatic resident's wild guess, laughed down. Never a decision.</summary>
        static void Tangent(Simulation sim, TrialState T, Mystery m)
        {
            var S = sim.S; var D = T.Debate;
            if (D.Seen.Contains("tangent") || DH(S, "tangent:" + D.Incident) > 0.6) return;
            var who = PickHolder(sim, T, Jurors(S, T), x => Fearful(S, x) + (1 - Obs(x)), "tangent", D.Target);
            if (who == null) return;
            D.Seen.Add("tangent");
            DSay(sim, T, who, "th_tangent", null, BeatKind.Line, "tangent", null, Emotion.Fear, Anim.Cower, 0.3f);
            var down = PickHolder(sim, T, Jurors(S, T), x => Arg(x) - Fearful(S, x), "tangent-down", who, D.Target);
            if (down != null) DSay(sim, T, down, "re_tangent_down", null, BeatKind.Line, "tangent_down", null, Emotion.Angry, Anim.CrossArms, 0.3f);
        }

        // ================================================================== per riddle
        static void RiddleTheories(Simulation sim, TrialState T, Mystery m)
        {
            switch (m.Trick)
            {
                case "Wound": ThWound(sim, T, m); break;
                case "Message": ThMessage(sim, T, m); break;
                case "Place": ThPlace(sim, T, m); break;
                case "Tod": ThTod(sim, T, m); break;
                case "Seal": ThSeal(sim, T, m); break;
                case "Swap": ThSwap(sim, T, m); break;
                case "Accident": case "Natural": case "Suicide": ThCause(sim, T, m); break;
                case "Who": ThWho(sim, T, m); break;
            }
            if (m.Act == "act1") Tangent(sim, T, m);
        }

        // ---- 「피로 쓴 글자」: the reader names the one the glyph points at; the named one protests; the culprit leans in
        static void ThMessage(Simulation sim, TrialState T, Mystery m)
        {
            var S = sim.S; var D = T.Debate; var k = KClaim(D, m.Claim); var jur = Jurors(S, T);
            var frame = D.Deck.Plates.FirstOrDefault(p => p.Role == PlateRole.Frame && p.Root.StartsWith("trace:"));
            string X = frame?.Points; string glyph = Glyph(frame?.Face);
            string reader = PickHolder(sim, T, jur, x => (frame != null && frame.Users.Contains("theory:" + x) ? 1.5 : 0) + (FoundPlate(S, x, frame) ? 1.2 : 0) + (X != null && Resents(S, x, X) ? 1.5 : 0) + Arg(x) * 0.6, "reader:" + m.Id, X, D.Target);
            if (reader == null || k?.Presented == null) return;
            var t1 = Voice(sim, T, m, reader, Family.Blame, FoundPlate(S, reader, frame) ? Basis.Saw : Basis.Plate, k.Presented, "th_writing",
                new Dictionary<string, string> { { "victim", "@" + D.Victim }, { "glyph", glyph }, { "target", "@" + X } }, frame?.Face, frame?.Id, false, X, leans: true, key: "accuse", emo: Emotion.Angry, gesture: Anim.Point);
            // the named one answers — with the truth of where they were, if anyone can bear it out
            if (X != null && X != Cast.Player && jur.Contains(X))
            {
                Protest(sim, T, X);
                var clear = D.Deck.Plates.FirstOrDefault(p => p.Role == PlateRole.Clear && p.Alibi.Contains(X));
                var ap = clear?.Props.FirstOrDefault(p => p.Kind == PropKind.AtPlace && p.A == X);
                if (ap != null)
                    Voice(sim, T, m, X, Family.Truth, Basis.Saw, new Prop { Kind = PropKind.AtPlace, A = X, Room = ap.Room, T0 = ap.T0, T1 = ap.T1, Value = "window-cover" }, "th_self_alibi",
                        new Dictionary<string, string> { { "time", ClockFmt.Vague(ap.T0) }, { "place", S.RoomName(ap.Room) } }, null, clear.Id, true, answers: "not:" + t1.Id, key: "claim", emo: Emotion.Angry, gesture: Anim.Shrug);
            }
            // the culprit leans in (it protects them)
            if (D.Target != null && D.Target != X && D.Target != reader && jur.Contains(D.Target))
                Back(sim, T, t1, D.Target, "th_culprit_backs", new Dictionary<string, string> { { "victim", "@" + D.Victim } }, Emotion.Neutral, Anim.CrossArms);
            PileOn(sim, T, t1);
            // the truth side, only from someone whose own look at the body saw an instant death
            var examiner = PickHolder(sim, T, jur.Where(x => BodyNote(S, x, D.Victim, "instant-death")), x => Obs(x) + Arg(x) * 0.3, "exam:" + m.Id, reader, X, D.Target);
            if (examiner != null && DH(S, "truthside:" + m.Id) < 0.7)
                Voice(sim, T, m, examiner, Family.Moved, Basis.Saw, new Prop { Kind = PropKind.TraceAt, A = D.Victim, Value = "not:" + t1.Id }, "th_truth_writing",
                    new Dictionary<string, string> { { "victim", "@" + D.Victim } }, "거의 즉사", null, true, answers: "not:" + t1.Id, key: "object", emo: Emotion.Neutral, gesture: Anim.Think);
        }

        // ---- 「발견된 곳」: the one who stood over the body vs the one who heard it elsewhere; the culprit keeps it simple
        static void ThPlace(Simulation sim, TrialState T, Mystery m)
        {
            var S = sim.S; var D = T.Debate; var k = KClaim(D, m.Claim); var jur = Jurors(S, T);
            var body = D.Deck.Plates.FirstOrDefault(p => p.Kind == PlateKind.Body);
            var pool = D.Deck.Plates.FirstOrDefault(p => p.Kind == PlateKind.Trace && p.Room == D.FoundRoom && p.True);
            string seer = PickHolder(sim, T, jur, x => (Examined(S, x, D.Victim) ? 1.5 : 0) + (body != null && body.FoundBy == x ? 1 : 0) + (FoundPlate(S, x, pool) ? 0.6 : 0) + (1 - Obs(x)) * 0.4, "seer:" + m.Id, D.Target);
            if (seer == null || k?.Presented == null) return;
            string frag = pool != null ? Reg(seer, $"{pool.Title}도 거기 있었어요.", $"{pool.Title}도 거기 있었어.") : Reg(seer, "쓰러진 자리 그대로였어요.", "쓰러진 자리 그대로였어.");
            var t1 = Voice(sim, T, m, seer, Family.Self, Examined(S, seer, D.Victim) ? Basis.Saw : Basis.Guess, k.Presented, "th_place_found",
                new Dictionary<string, string> { { "place", S.RoomName(D.FoundRoom) }, { "fragment", frag } }, frag, pool?.Id, false, key: "claim", emo: Emotion.Neutral, gesture: Anim.Talk);
            // heard it from somewhere else
            string hearer = null; HeardSound hs = null;
            foreach (var x in jur.Where(x => x != seer && x != D.Target).OrderBy(x => DH(S, "hear:" + m.Id + ":" + x)))
            {
                var h = S.K(x).Heard.Where(h2 => Debate.IsAlarm(h2.Kind) && Math.Abs(h2.Clock - D.KillClock) <= 25 && (h2.GuessRoom == D.KillRoom || h2.Room == D.KillRoom)).OrderByDescending(h2 => h2.Loud).FirstOrDefault();
                if (h != null) { hearer = x; hs = h; break; }
            }
            var truthProp = new Prop { Kind = PropKind.DeathPlace, A = D.Victim, Room = D.KillRoom, Value = "attack" };
            if (hearer != null)
            {
                var hp = D.Deck.Plates.FirstOrDefault(p => p.Witness == hearer && p.Role == PlateRole.Confirm);
                Voice(sim, T, m, hearer, Family.Moved, Basis.Heard, truthProp, "th_place_heard",
                    new Dictionary<string, string> { { "place", S.RoomName(D.KillRoom) }, { "time", ClockFmt.Vague(hs.Clock) }, { "sound", Simulation.SoundText(hs.Kind) }, { "fragment", Reg(hearer, $"{ClockFmt.Vague(hs.Clock)}에 {Simulation.SoundText(hs.Kind)} 소리가 났어요.", $"{ClockFmt.Vague(hs.Clock)}에 {Simulation.SoundText(hs.Kind)} 소리가 났어.") } },
                    Simulation.SoundText(hs.Kind), hp?.Id, true, answers: "not:" + t1.Id, key: "object", emo: Emotion.Neutral, gesture: Anim.Point);
            }
            else
            {
                // someone who noticed the marks where it really began
                var marks = D.Deck.Plates.FirstOrDefault(p => p.True && p.Room == D.KillRoom && (p.Kind == PlateKind.Trace || p.Kind == PlateKind.Fixture));
                string noticer = marks == null ? null : PickHolder(sim, T, jur, x => (FoundPlate(S, x, marks) ? 2 : 0) + Obs(x), "marks:" + m.Id, seer, D.Target);
                if (noticer != null && FoundPlate(S, noticer, marks))
                    Voice(sim, T, m, noticer, Family.Moved, Basis.Saw, truthProp, "th_place_marks",
                        new Dictionary<string, string> { { "place", S.RoomName(D.KillRoom) }, { "fragment", marks.Title } }, marks.Title, marks.Id, true, answers: "not:" + t1.Id, key: "object", emo: Emotion.Neutral, gesture: Anim.Point);
            }
            if (D.Target != null && D.Target != seer && jur.Contains(D.Target))
                Back(sim, T, t1, D.Target, "th_culprit_place", new Dictionary<string, string> { { "place", S.RoomName(D.FoundRoom) } }, Emotion.Neutral, Anim.CrossArms);
            if (hearer == null) Doubt(sim, T, t1);
        }

        // ---- 「그 시각의 죽음」: the body's warmth vs a sound at another hour
        static void ThTod(Simulation sim, TrialState T, Mystery m)
        {
            var S = sim.S; var D = T.Debate; var k = KClaim(D, m.Claim); var jur = Jurors(S, T);
            if (k?.Presented == null) return;
            string examiner = PickHolder(sim, T, jur, x => (Examined(S, x, D.Victim) ? 2 : 0) + (1 - Obs(x)) * 0.5, "tod:" + m.Id, D.Target);
            if (examiner == null) return;
            double shown = k.Presented.T0 + 15;
            var t1 = Voice(sim, T, m, examiner, Family.Self, Examined(S, examiner, D.Victim) ? Basis.Saw : Basis.Plate, k.Presented, "th_time_body",
                new Dictionary<string, string> { { "time", ClockFmt.Vague(shown) }, { "victim", "@" + D.Victim } }, "체온", null, false, key: "claim", emo: Emotion.Neutral, gesture: Anim.Talk);
            string hearer = null; HeardSound hs = null;
            foreach (var x in jur.Where(x => x != examiner && x != D.Target).OrderBy(x => DH(S, "todh:" + m.Id + ":" + x)))
            {
                var h = S.K(x).Heard.Where(h2 => Debate.IsAlarm(h2.Kind) && Math.Abs(h2.Clock - D.KillClock) <= 20).OrderByDescending(h2 => h2.Loud).FirstOrDefault();
                if (h != null) { hearer = x; hs = h; break; }
            }
            if (hearer != null)
                Voice(sim, T, m, hearer, Family.TimeSlip, Basis.Heard, new Prop { Kind = PropKind.DeathWindow, A = D.Victim, T0 = D.KillClock - 10, T1 = D.KillClock + 10 }, "th_time_heard",
                    new Dictionary<string, string> { { "time", ClockFmt.Vague(hs.Clock) }, { "sound", Simulation.SoundText(hs.Kind) } }, Simulation.SoundText(hs.Kind), null, true, answers: "not:" + t1.Id, key: "object", emo: Emotion.Neutral, gesture: Anim.Point);
            if (D.Target != null && D.Target != examiner && jur.Contains(D.Target))
                Back(sim, T, t1, D.Target, "th_culprit_time", null, Emotion.Neutral, Anim.CrossArms);
            else Doubt(sim, T, t1);
        }

        // ---- 「잠긴 방」: the one who broke in swears it was sealed
        static void ThSeal(Simulation sim, TrialState T, Mystery m)
        {
            var S = sim.S; var D = T.Debate; var k = KClaim(D, m.Claim); var jur = Jurors(S, T);
            if (k?.Presented == null) return;
            var body = D.Deck.Plates.FirstOrDefault(p => p.Kind == PlateKind.Body);
            string disc = PickHolder(sim, T, jur, x => (body != null && body.FoundBy == x ? 2 : 0) + (Examined(S, x, D.Victim) ? 1 : 0), "seal:" + m.Id, D.Target);
            if (disc == null) return;
            var t1 = Voice(sim, T, m, disc, Family.Self, Basis.Saw, k.Presented, "th_sealed", new Dictionary<string, string> { { "place", S.RoomName(D.FoundRoom) } }, "잠긴 문", null, false, key: "claim", emo: Emotion.Fear, gesture: Anim.Talk);
            var thread = D.Deck.Plates.FirstOrDefault(p => p.True && p.Props.Any(pp => pp.Kind == PropKind.TraceAt && (pp.Value == "thread-under-door" || pp.Value == "key-slid")));
            string noticer = thread == null ? null : PickHolder(sim, T, jur.Where(x => FoundPlate(S, x, thread)), x => Obs(x), "sealn:" + m.Id, disc, D.Target);
            if (noticer != null) Voice(sim, T, m, noticer, Family.HiddenWay, Basis.Saw, new Prop { Kind = PropKind.DoorLocked, A = D.Victim, Room = D.FoundRoom, Value = "not:" + t1.Id }, "th_sealed_thread", null, thread.Title, thread.Id, true, answers: "not:" + t1.Id, key: "object", emo: Emotion.Neutral, gesture: Anim.Think);
            if (D.Target != null && D.Target != disc && jur.Contains(D.Target)) Back(sim, T, t1, D.Target, "th_culprit_sealed", null, Emotion.Neutral, Anim.CrossArms);
            else Doubt(sim, T, t1);
        }

        // ---- 「시신 옆의 흉기」: the bloody thing by the body vs the wound itself
        static void ThSwap(Simulation sim, TrialState T, Mystery m)
        {
            var S = sim.S; var D = T.Debate; var k = KClaim(D, m.Claim); var jur = Jurors(S, T);
            if (k?.Presented == null) return;
            var decoy = D.Deck.Plates.FirstOrDefault(p => p.Role == PlateRole.Staged && p.Points == "means");
            string item = decoy?.Title?.Replace("피 묻은 ", "") ?? "그것";
            string reader = PickHolder(sim, T, jur, x => (FoundPlate(S, x, decoy) ? 2 : 0) + (1 - Obs(x)), "swap:" + m.Id, D.Target);
            if (reader == null) return;
            var t1 = Voice(sim, T, m, reader, Family.Self, FoundPlate(S, reader, decoy) ? Basis.Saw : Basis.Plate, k.Presented, "th_weapon_decoy", new Dictionary<string, string> { { "item", item } }, decoy?.Face, decoy?.Id, false, leans: true, key: "claim", emo: Emotion.Neutral, gesture: Anim.Point);
            var v = S.A(D.Victim); var w = v?.Body.Wounds.Where(x => !x.Postmortem).OrderByDescending(x => x.Sev).FirstOrDefault();
            string examiner = w == null ? null : PickHolder(sim, T, jur.Where(x => Examined(S, x, D.Victim)), x => Obs(x), "swapx:" + m.Id, reader, D.Target);
            if (examiner != null)
                Voice(sim, T, m, examiner, Family.Truth, Basis.Saw, new Prop { Kind = PropKind.WeaponType, A = D.Victim, Value = w.Type.ToString() }, "th_weapon_wound",
                    new Dictionary<string, string> { { "wound", WoundText.Region(w.Region) + "의 " + WoundText.Type(w.Type) } }, WoundText.Type(w.Type), null, true, answers: "not:" + t1.Id, key: "object", emo: Emotion.Neutral, gesture: Anim.Think);
            if (D.Target != null && D.Target != reader && jur.Contains(D.Target)) Back(sim, T, t1, D.Target, "th_culprit_weapon", new Dictionary<string, string> { { "item", item } }, Emotion.Neutral, Anim.CrossArms);
            PileOn(sim, T, t1);
        }

        // ---- 「아무도 손대지 않았다」: an accident, an illness, a choice — or a hand
        static void ThCause(Simulation sim, TrialState T, Mystery m)
        {
            var S = sim.S; var D = T.Debate; var k = KClaim(D, m.Claim); var jur = Jurors(S, T);
            if (k?.Presented == null) return;
            string key = m.Trick == "Natural" ? "th_natural" : m.Trick == "Suicide" ? "th_suicide" : "th_accident";
            string holder = PickHolder(sim, T, jur, x => Fearful(S, x) + (Examined(S, x, D.Victim) ? 0.5 : 0), "cause:" + m.Id, D.Target);
            if (holder == null) return;
            var t1 = Voice(sim, T, m, holder, Family.Accident, Examined(S, holder, D.Victim) ? Basis.Saw : Basis.Guess, k.Presented, key,
                new Dictionary<string, string> { { "victim", "@" + D.Victim }, { "place", S.RoomName(D.FoundRoom) } }, null, null, false, key: "claim", emo: Emotion.Sad, gesture: Anim.Talk);
            string[] hands = { "ligature", "smother-marks", "nail-scrape", "held-under", "push-bruise", "clothed-drowning", "poisoned" };
            string examiner = PickHolder(sim, T, jur.Where(x => hands.Any(h => BodyNote(S, x, D.Victim, h))), x => Obs(x), "causex:" + m.Id, holder, D.Target);
            if (examiner != null)
                Voice(sim, T, m, examiner, Family.Truth, Basis.Saw, new Prop { Kind = PropKind.Culprit, A = null, B = D.Victim, Value = "not:" + t1.Id }, "th_not_accident",
                    new Dictionary<string, string> { { "victim", "@" + D.Victim } }, null, null, true, answers: "not:" + t1.Id, key: "object", emo: Emotion.Neutral, gesture: Anim.Think);
            if (D.Target != null && D.Target != holder && jur.Contains(D.Target)) Back(sim, T, t1, D.Target, "th_culprit_accident", new Dictionary<string, string> { { "victim", "@" + D.Victim } }, Emotion.Sad, Anim.Talk);
            else PileOn(sim, T, t1);
        }

        // ---- 「누가」: the one the room was turned toward defends; someone's sighting names another; the culprit gives an alibi
        static void ThWho(Simulation sim, TrialState T, Mystery m)
        {
            var S = sim.S; var D = T.Debate; var jur = Jurors(S, T);
            // the pushed person's truthful answer
            if (D.Push != null && D.Push != Cast.Player && jur.Contains(D.Push) && !m.Theories.Select(id => DTheory(D, id)).Any(t => t.Holder == D.Push))
            {
                var clear = D.Deck.Plates.FirstOrDefault(p => p.Role == PlateRole.Clear && p.Alibi.Contains(D.Push));
                var ap = clear?.Props.FirstOrDefault(p => p.Kind == PropKind.AtPlace && p.A == D.Push);
                var accusing = m.Theories.Select(id => DTheory(D, id)).FirstOrDefault(t => t != null && t.Target == D.Push && t.State == "standing");
                if (ap != null)
                    Voice(sim, T, m, D.Push, Family.Truth, Basis.Saw, new Prop { Kind = PropKind.AtPlace, A = D.Push, Room = ap.Room, T0 = ap.T0, T1 = ap.T1, Value = "window-cover" }, "th_self_alibi",
                        new Dictionary<string, string> { { "time", ClockFmt.Vague(ap.T0) }, { "place", S.RoomName(ap.Room) } }, null, clear.Id, true, answers: accusing != null ? "not:" + accusing.Id : null, key: "claim", emo: Emotion.Angry, gesture: Anim.Shrug);
                else Protest(sim, T, D.Push);
            }
            // a sighting that names someone else (a coincidence the room has not turned yet)
            var coin = D.Deck.Plates.Where(p => p.Role == PlateRole.Coincidence && p.State != PlateState.Flipped && p.Points != null && p.Points != D.Push && jur.Contains(p.Witness ?? "") && p.Witness != D.Target && jur.Contains(p.Points)).OrderBy(p => p.N).FirstOrDefault();
            if (coin != null && !m.Theories.Select(id => DTheory(D, id)).Any(t => t.Pin == coin.Id))
            {
                var held = coin.Props.FirstOrDefault(p => p.Kind == PropKind.Held);
                string heldKor = held != null ? ItemCatalog.Get(held.Item)?.Kor ?? held.Item : "", nm = sim.CallName(coin.Witness, coin.Points);
                string spoken = held != null ? Reg(coin.Witness, $"{ClockFmt.Vague(coin.T0)} {S.RoomName(coin.Room)}에서 {nm}이(가) {heldKor}을(를) 들고 있었어요.", $"{ClockFmt.Vague(coin.T0)} {S.RoomName(coin.Room)}에서 {nm}이(가) {heldKor} 들고 있었어.")
                                             : Reg(coin.Witness, $"{ClockFmt.Vague(coin.T0)} {S.RoomName(coin.Room)}에서 {nm}을(를) 봤어요.", $"{ClockFmt.Vague(coin.T0)} {S.RoomName(coin.Room)}에서 {nm} 봤어.");
                var t = Voice(sim, T, m, coin.Witness, Family.Blame, Basis.Saw, new Prop { Kind = PropKind.Culprit, A = coin.Points, B = D.Victim, Value = "opportunity" }, held != null ? "th_held" : "th_near",
                    new Dictionary<string, string> { { "time", ClockFmt.Vague(coin.T0) }, { "place", S.RoomName(coin.Room) }, { "target", "@" + coin.Points }, { "item", heldKor }, { "fragment", LineBank.FixParticles(spoken) } },
                    coin.Face, coin.Id, false, coin.Points, leans: true, key: "claim", emo: Emotion.Neutral, gesture: Anim.Point);
                Protest(sim, T, coin.Points);
            }
            // "then where was everybody?" — the culprit's alibi, and the honest one who saw them (at another hour)
            if (D.Target != null && jur.Contains(D.Target) && !m.Theories.Select(id => DTheory(D, id)).Any(t => t.Holder == D.Target && t.Lie != null))
            {
                string asker = D.Push != null && jur.Contains(D.Push) && D.Push != D.Target ? D.Push : PickHolder(sim, T, jur, x => Arg(x), "where:" + m.Id, D.Target);
                if (asker != null) DSay(sim, T, asker, "re_where_all", null, BeatKind.Interrupt, "object", null, Emotion.Angry, Anim.Slam, 0.55f);
                var lie = D.Lies.FirstOrDefault(l => !l.Used && (l.Topic == "where" || l.Topic == "time" || l.Topic == "with"));
                var fa = D.Deck.Plates.FirstOrDefault(p => p.Role == PlateRole.FalseAlibi);
                int room = D.ClaimRoom >= 0 ? D.ClaimRoom : fa?.Room ?? -1;
                if (room >= 0)
                {
                    double t0 = D.ClaimFrom >= 0 ? D.ClaimFrom : D.KillClock - 30, t1 = D.ClaimTo >= 0 ? D.ClaimTo : D.KillClock + 30;
                    string with = D.ClaimWith.FirstOrDefault(x => jur.Contains(x)) ?? fa?.Witness;
                    string text = lie?.Text != null ? DebateLines.InRegister(D.Target, lie.Text) : DebateLines.Say(S, D.Target, "th_alibi_culprit", new Dictionary<string, string> { { "place", S.RoomName(room) }, { "time", ClockFmt.Vague(D.KillClock) }, { "with", with != null ? sim.CallName(D.Target, with) : "누구든" } }, D.SpokenKeys, D.Seq.ToString());
                    if (lie != null) lie.Used = true;
                    var alibi = Voice(sim, T, m, D.Target, Family.Self, Basis.Saw, new Prop { Kind = PropKind.AtPlace, A = D.Target, Room = room, T0 = t0, T1 = t1, Value = "window-cover" }, null, null,
                        null, fa?.Id, false, lie: lie?.Id ?? "story", key: "claim", emo: Emotion.Neutral, gesture: Anim.Talk, rawText: text);
                    D.Mind.LiesUsed.Add(lie?.Id ?? "story");
                    // the honest one who did see them there — at another hour
                    if (fa?.Witness != null && jur.Contains(fa.Witness) && fa.Witness != D.Target && Honest(S, fa.Witness))
                        Back(sim, T, alibi, fa.Witness, "re_confirm_alibi", new Dictionary<string, string> { { "target", "@" + D.Target }, { "place", S.RoomName(fa.Room) }, { "time", ClockFmt.Vague(fa.T0) } }, Emotion.Neutral, Anim.Talk);
                }
            }
        }

        // ================================================================== when the floor passes: someone asks for it
        /// <summary>A resident with something relevant, not yet said, speaks up (§6.3 K5). Returns false if nobody has anything.</summary>
        static bool FloorRequest(Simulation sim, TrialState T, Mystery m)
        {
            var S = sim.S; var D = T.Debate; var jur = Jurors(S, T);
            var standing = m.Theories.Select(id => DTheory(D, id)).Where(t => t != null && t.State == "standing").ToList();
            if (m.Trick == "Who" && D.Target != null)
            {
                // someone who saw the culprit near the scene at the hour
                var alibi = standing.FirstOrDefault(t => t.Holder == D.Target && t.Lie != null);
                var link = D.Deck.Plates.FirstOrDefault(p => p.True && p.Role == PlateRole.Link && p.Kind == PlateKind.Witness && p.Seen == D.Target && jur.Contains(p.Witness ?? "") && !m.Requests.Contains(p.Witness));
                if (link != null)
                {
                    m.Requests.Add(link.Witness);
                    DSay(sim, T, link.Witness, "re_request", new Dictionary<string, string> { { "place", S.RoomName(link.Room) } }, BeatKind.Interrupt, "request", null, Emotion.Fear, Anim.Talk, 0.5f);
                    var at = link.Props.FirstOrDefault(p => p.Kind == PropKind.AtPlace && p.A == D.Target);
                    var held = link.Props.FirstOrDefault(p => p.Kind == PropKind.Held && p.A == D.Target);
                    Voice(sim, T, m, link.Witness, Family.Truth, Basis.Saw, at ?? new Prop { Kind = PropKind.AtPlace, A = D.Target, Room = link.Room, T0 = link.T0, T1 = link.T1 }, held != null ? "th_link_witness_held" : "th_link_witness",
                        new Dictionary<string, string> { { "time", ClockFmt.Vague(link.T0) }, { "place", S.RoomName(link.Room) }, { "target", "@" + D.Target }, { "item", held != null ? ItemCatalog.Get(held.Item)?.Kor ?? held.Item : "" } },
                        link.Face, link.Id, true, answers: alibi != null ? "not:" + alibi.Id : null, key: "object", emo: Emotion.Fear, gesture: Anim.Point);
                    if (alibi != null) CulpritShaken(sim, T, 0.4f);
                    return true;
                }
            }
            // the truth side of an L1 riddle, if nobody put it up yet
            if (m.Trick == "Message" && !standing.Any(t => t.Answers != null))
            {
                var t1 = standing.FirstOrDefault(t => t.Leans);
                var examiner = t1 == null ? null : PickHolder(sim, T, jur.Where(x => BodyNote(S, x, D.Victim, "instant-death") && !m.Requests.Contains(x)), x => Obs(x), "req:" + m.Id, t1.Holder, t1.Target, D.Target);
                if (examiner != null)
                {
                    m.Requests.Add(examiner);
                    Voice(sim, T, m, examiner, Family.Moved, Basis.Saw, new Prop { Kind = PropKind.TraceAt, A = D.Victim, Value = "not:" + t1.Id }, "th_truth_writing",
                        new Dictionary<string, string> { { "victim", "@" + D.Victim } }, "거의 즉사", null, true, answers: "not:" + t1.Id, key: "object", emo: Emotion.Neutral, gesture: Anim.Think);
                    return true;
                }
            }
            if (m.Trick == "Place" && !standing.Any(t => t.Answers != null))
            {
                var t1 = standing.FirstOrDefault(t => !t.True);
                foreach (var x in jur.Where(x => t1 != null && x != t1.Holder && x != D.Target && !m.Requests.Contains(x)).OrderBy(x => DH(S, "reqh:" + m.Id + ":" + x)))
                {
                    var h = S.K(x).Heard.Where(h2 => Debate.IsAlarm(h2.Kind) && Math.Abs(h2.Clock - D.KillClock) <= 25 && (h2.GuessRoom == D.KillRoom || h2.Room == D.KillRoom)).OrderByDescending(h2 => h2.Loud).FirstOrDefault();
                    if (h == null) continue;
                    m.Requests.Add(x);
                    DSay(sim, T, x, "re_request", new Dictionary<string, string> { { "place", S.RoomName(D.KillRoom) } }, BeatKind.Interrupt, "request", null, Emotion.Neutral, Anim.Talk, 0.5f);
                    Voice(sim, T, m, x, Family.Moved, Basis.Heard, new Prop { Kind = PropKind.DeathPlace, A = D.Victim, Room = D.KillRoom, Value = "attack" }, "th_place_heard",
                        new Dictionary<string, string> { { "place", S.RoomName(D.KillRoom) }, { "time", ClockFmt.Vague(h.Clock) }, { "sound", Simulation.SoundText(h.Kind) }, { "fragment", Reg(x, $"{ClockFmt.Vague(h.Clock)}에 {Simulation.SoundText(h.Kind)} 소리가 났어요.", $"{ClockFmt.Vague(h.Clock)}에 {Simulation.SoundText(h.Kind)} 소리가 났어.") } },
                        Simulation.SoundText(h.Kind), null, true, answers: "not:" + t1.Id, key: "object", emo: Emotion.Neutral, gesture: Anim.Point);
                    return true;
                }
            }
            return false;
        }

        // ================================================================== plaques and what they open
        static string PlaqueText(Simulation sim, DebateState D, Mystery m)
        {
            var S = sim.S; string V = Given(D.Victim);
            switch (m.Trick)
            {
                case "Wound": return $"{V}은(는) {WeaponWord(WoundDmg(sim, D))}에 당했다" + (D.Record != null ? $" — {D.Record}." : ".");
                case "Message":
                    {
                        var frame = D.Deck.Plates.FirstOrDefault(p => p.Role == PlateRole.Frame && p.Root.StartsWith("trace:"));
                        return $"피로 쓴 글자는 {V}이(가) 남긴 것이 아니다" + (frame?.Points != null ? $" — 누군가 {Given(frame.Points)}에게 뒤집어씌우려 했다." : ".");
                    }
                case "Place": return D.Moved ? $"{V}은(는) {S.RoomName(D.KillRoom)}에서 공격당했다. 누군가 {S.RoomName(D.FoundRoom)}(으)로 옮겼다." : $"{V}은(는) {S.RoomName(D.KillRoom)}에서 공격당했다. 쓰러진 곳은 {S.RoomName(D.FoundRoom)}이다.";
                case "Tod": return $"{V}이(가) 공격당한 건 {ClockFmt.Vague(D.KillClock)}이다 — 시신의 온기는 꾸며진 것이다.";
                case "Seal": return $"{S.RoomName(D.FoundRoom)}은(는) 밀실이 아니었다 — 밖에서 잠글 수 있었다.";
                case "Swap": return "시신 옆의 물건은 흉기가 아니다 — 피는 겉에만 발려 있었다.";
                case "Accident": return $"{V}의 죽음은 사고가 아니다 — 누군가 손을 댔다.";
                case "Natural": return $"{V}은(는) 병으로 죽지 않았다 — 누군가 손을 댔다.";
                case "Suicide": return $"{V}은(는) 스스로 목숨을 끊지 않았다 — 누군가 꾸몄다.";
                case "Who":
                    {
                        var alibi = D.Theories.FirstOrDefault(t => t.Mystery == m.Id && t.Holder == D.Target && t.Lie != null);
                        return alibi != null && alibi.State == "collapsed"
                            ? $"{Given(D.Target)}의 말은 사실이 아니다 — {V}이(가) 공격당한 무렵, {Given(D.Target)}은(는) 현장 가까이에 있었다."
                            : $"{V}이(가) 공격당한 무렵, 현장 가까이에 누가 있었는지가 드러났다.";
                    }
            }
            return m.Title;
        }

        static Prop PlaqueProp(DebateState D, Mystery m)
        {
            switch (m.Trick)
            {
                case "Wound": return new Prop { Kind = PropKind.WeaponType, A = D.Victim, Value = D.Deck.Plates.FirstOrDefault(p => p.Kind == PlateKind.Body)?.Props.FirstOrDefault(p => p.Kind == PropKind.WeaponType)?.Value };
                case "Place": return new Prop { Kind = PropKind.DeathPlace, A = D.Victim, Room = D.KillRoom, Value = "attack" };
                case "Tod": return new Prop { Kind = PropKind.DeathWindow, A = D.Victim, T0 = D.KillClock - 10, T1 = D.KillClock + 10 };
                case "Who": return D.Target != null ? new Prop { Kind = PropKind.Lie, A = D.Target, Value = m.Id } : null;
            }
            return null;
        }

        /// <summary>A riddle closed only because its first reading was a guess the holder took back: engrave what is known, not more.</summary>
        static string SoftPlaque(Simulation sim, DebateState D, Mystery m)
        {
            var S = sim.S; var core = D.Theories.FirstOrDefault(t => t.Mystery == m.Id && !t.True && t.State == "collapsed");
            var truth = D.Theories.FirstOrDefault(t => t.Mystery == m.Id && t.True && t.State != "collapsed");
            string first = core != null ? $"{Given(core.Holder)}의 말은 짐작이었다" : "처음의 말은 짐작이었다";
            if (m.Trick == "Place" && truth != null) return $"{first} — {Given(truth.Holder)}은(는) {S.RoomName(D.KillRoom)} 쪽에서 소리를 들었다. 아직 확인되지는 않았다.";
            return first + ". 아직 확인된 것은 없다.";
        }

        static DamageType WoundDmg(Simulation sim, DebateState D) => P(D.Deck.Plates.FirstOrDefault(p => p.Kind == PlateKind.Body)?.Props.FirstOrDefault(p => p.Kind == PropKind.WeaponType)?.Value);

        /// <summary>민혁's summary of a settled riddle, spoken (4막): the plaque in his own words.</summary>
        static string SummaryLine(Simulation sim, DebateState D, Mystery m)
        {
            var S = sim.S; string V = sim.CallName(Cast.Player, D.Victim);
            string Call(string id) => sim.CallName(Cast.Player, id);
            switch (m.Trick)
            {
                case "Wound": return LineBank.FixParticles($"{V}은(는) {WeaponWord(WoundDmg(sim, D))}에 당했어요." + (D.Record != null ? $" {D.Record}이었어요." : ""));
                case "Message":
                    {
                        var frame = D.Deck.Plates.FirstOrDefault(p => p.Role == PlateRole.Frame && p.Root.StartsWith("trace:"));
                        return LineBank.FixParticles($"피로 쓴 글자는 {V}이(가) 남긴 게 아니었어요. 거의 즉사였으니까요." + (frame?.Points != null ? $" 누군가 {Call(frame.Points)}에게 뒤집어씌우려고 써 둔 거예요." : ""));
                    }
                case "Place": return LineBank.FixParticles(D.Moved ? $"{V}은(는) {S.RoomName(D.KillRoom)}에서 공격당했고, 누군가 {S.RoomName(D.FoundRoom)}(으)로 옮겼어요." : $"{V}은(는) {S.RoomName(D.KillRoom)}에서 공격당했고, {S.RoomName(D.FoundRoom)}까지 가서 쓰러졌어요.");
                case "Tod": return LineBank.FixParticles($"{V}이(가) 공격당한 건 {ClockFmt.Vague(D.KillClock)}이에요. 시신의 온기는 꾸며진 거였고요.");
                case "Seal": return LineBank.FixParticles($"{S.RoomName(D.FoundRoom)}은(는) 밀실이 아니었어요. 밖에서도 잠글 수 있었어요.");
                case "Swap": return "시신 옆의 물건은 흉기가 아니었어요. 피는 나중에 겉에만 발라 둔 거예요.";
                case "Accident": case "Natural": case "Suicide": return LineBank.FixParticles($"{V}의 죽음은 누군가 손을 댄 결과예요. {(m.Trick == "Accident" ? "사고" : m.Trick == "Natural" ? "병" : "스스로 택한 것")}처럼 보이게 꾸몄을 뿐이에요.");
                case "Who":
                    {
                        var alibi = D.Theories.FirstOrDefault(t => t.Mystery == m.Id && t.Holder == D.Target && t.Lie != null);
                        return alibi != null && alibi.State == "collapsed" ? LineBank.FixParticles($"그 무렵 {Call(D.Target)}은(는) 다른 곳에 있었다고 했지만, 사실은 현장 가까이에 있었어요.") : null;
                    }
            }
            return null;
        }

        /// <summary>The line that opens the next question (민혁's): 그렇다면 문제는…</summary>
        static string ReframeText(Simulation sim, DebateState D, Mystery m)
        {
            var S = sim.S; string V = sim.CallName(Cast.Player, D.Victim);
            switch (m.Trick)
            {
                case "Wound": return "그 흉기가 누구 손에 있었느냐예요.";
                case "Message": return "누가, 왜 그 글씨를 남겼느냐예요.";
                case "Place": return $"그 시각 {S.RoomName(D.KillRoom)} 가까이에 누가 있었느냐예요.";
                case "Tod": return "그 시각에 맞춰 둔 알리바이가 있다는 거예요.";
                case "Seal": return "누가 밖에서 문을 잠갔느냐예요.";
                case "Swap": return "진짜 흉기가 어디로 갔느냐예요.";
                case "Accident": case "Natural": case "Suicide": return $"누가 {V}에게 손을 댔느냐예요.";
            }
            return null;
        }
    }
}
