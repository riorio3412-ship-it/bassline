using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// What 민혁 can do on the floor (TrialReforge §6.3, v0): 맞대기 (lay a plate against a theory), 캐묻기 (ask how they know),
    /// 더 듣기 (let the floor pass — someone with something to say asks for it), 그렇다면… (the inference), 지목. Resolution
    /// (§6.5): a true plate (or a turned one) that breaks a theory collapses it and turns the fake it leaned on; a plate that
    /// bears out a truth-side theory seals it; an unturned fake laid as proof is turned at once by whoever can, or earns the
    /// plain refusal; anything else is a miss with a written reason. NPCs lay plates through the same code.
    /// </summary>
    public static partial class TrialSystem
    {
        // ================================================================== plates as the court's cards
        internal static List<TrialGames.Bullet> DebateArsenal(Simulation sim)
        {
            var S = sim.S; var D = S.Trial.Debate; var list = new List<TrialGames.Bullet>();
            foreach (var p in D.Deck.Plates.OrderBy(p => p.N))
            {
                var ev = PlateEvidence(S, D, p);
                list.Add(new TrialGames.Bullet { Id = ev.Id, Title = ev.Title, Desc = ev.Desc, Line = ev.Line, Group = "은판", Kind = ev.Kind, Key = p.State == PlateState.Flipped || p.Kind == PlateKind.Body, InCase = true });
            }
            return list;
        }

        internal static Evidence DebatePlateEvidence(GameState S, string id)
        {
            var D = S.Trial?.Debate; if (D == null || id == null || !id.StartsWith("plate:")) return null;
            var p = DPlate(D, id.Substring(6)); return p == null ? null : PlateEvidence(S, D, p);
        }

        /// <summary>A plate as an evidence card the trial screen already knows how to draw (title, one line, the bullets).</summary>
        static Evidence PlateEvidence(GameState S, DebateState D, Plate p)
        {
            bool turned = p.State == PlateState.Flipped, sealedP = p.State == PlateState.Sealed;
            var kind = p.Kind == PlateKind.Body ? EvKind.Body : p.Kind == PlateKind.Trace || p.Kind == PlateKind.Fixture ? EvKind.Trace : p.Kind == PlateKind.Witness ? EvKind.Testimony : p.Kind == PlateKind.Object ? EvKind.ObjectState : EvKind.Record;
            string line = turned ? "✕ 뒤집힘 — " + p.Back : p.Face;
            var ev = new Evidence
            {
                Id = "plate:" + p.Id, Owner = Cast.Player, Kind = kind, Title = $"{p.N} · {p.Title}", Line = line, Desc = line + (sealedP && p.Back != null ? "\n" + p.Back : "") + (turned ? "\n(처음 보인 것: " + p.Face + ")" : ""),
                Source = "은판", Direct = true, Root = "plate:" + p.Id, T0 = p.T0, T1 = p.T1, Room = p.Room, Loop = S.Loop, Chapter = S.Chapter, Subject = p.Kind == PlateKind.Body ? D.Victim : null, Important = p.Kind == PlateKind.Body || turned
            };
            foreach (var pr in p.Props) ev.Props.Add(pr.Clone());
            return ev;
        }

        static Plate PlateOfCard(DebateState D, string cardId) => cardId != null && cardId.StartsWith("plate:") ? DPlate(D, cardId.Substring(6)) : null;
        static Theory TheoryOfClaim(DebateState D, string claimId) => claimId == null ? null : D.Theories.FirstOrDefault(t => t.TrialClaim == claimId);

        // ================================================================== resolution (pure)
        internal sealed class ShowEval { public string Outcome; public LogicResult R = LogicResult.Irrelevant; public string Why; public Theory Fallen, Sealed; public Plate Flip; public string Flipper; }

        /// <summary>A plate against one theory: Logic over the plate's props, a pack lie's BrokenBy, and the fake the theory leaned on.</summary>
        static (LogicResult r, string why) Weigh(GameState S, DebateState D, Theory th, Prop claim, Plate P)
        {
            if (claim == null || P == null) return (LogicResult.Irrelevant, null);
            if (P.State == PlateState.Flipped) return WeighTurned(S, D, th, claim, P);
            var (r, why) = Debate.Judge(S, claim, P, true);
            if (r != LogicResult.Contradict && th?.Lie != null && LieBrokenBy(D, th.Lie, P)) { r = LogicResult.Contradict; why = LineBank.FixParticles($"{P.Title} — {Given(th.Holder)}의 말과 맞지 않는다"); }
            // through its pin: this plate undoes the reading the theory leaned on
            if (r != LogicResult.Contradict && th?.Pin != null && th.Pin != P.Id)
            {
                var F = DPlate(D, th.Pin);
                if (F != null && !F.True && F.State != PlateState.Flipped && F.Routes.Contains("pl:" + P.Id))
                {
                    if (th.Leans) { r = LogicResult.Contradict; why = F.Back; }
                    else if (r == LogicResult.Irrelevant || r == LogicResult.Support) { r = LogicResult.LimitScope; why = F.Back; }
                }
            }
            return (r, why);
        }

        /// <summary>A turned plate is a settled fact: it undoes whatever leaned on its false first reading.</summary>
        static (LogicResult r, string why) WeighTurned(GameState S, DebateState D, Theory th, Prop claim, Plate P)
        {
            if (th != null && th.Pin == P.Id) return (th.Leans ? LogicResult.Contradict : LogicResult.LimitScope, P.Back);
            if (P.Role == PlateRole.FalseAlibi && claim.Kind == PropKind.AtPlace && claim.A == P.Seen && claim.Value == "window-cover") return (LogicResult.LimitScope, P.Back);
            if ((P.Role == PlateRole.Frame || P.Role == PlateRole.Coincidence || P.Role == PlateRole.Mistaken) && claim.Kind == PropKind.Culprit && claim.A == P.Points) return (LogicResult.Contradict, P.Back);
            if (P.Role == PlateRole.Frame && claim.Kind == PropKind.TraceAt && claim.Value != null && claim.Value.StartsWith("bloodwriting")) return (LogicResult.Contradict, P.Back);
            if (P.Role == PlateRole.Staged && claim.Kind == PropKind.WeaponType) return (LogicResult.Contradict, P.Back);
            return (LogicResult.Irrelevant, null);
        }

        /// <summary>The culprit's pack lie is broken by this thing (witness:/trace:/item:/furniture: in its BrokenBy).</summary>
        static bool LieBrokenBy(DebateState D, string lieId, Plate P)
        {
            // only what the court can see ties the lie to its speaker: a witness who saw them. A trace or an object on the pack's
            // list is where the truth hides, not something a plate says out loud (the pack reads the ledger; the court may not).
            var lie = D.Lies.FirstOrDefault(l => l.Id == lieId); if (lie == null || !P.True) return false;
            foreach (var b in lie.BrokenBy)
            {
                if (b.StartsWith("witness:") && P.Kind == PlateKind.Witness && P.Witness == b.Substring(8))
                {
                    if (P.Seen == D.Target) return true;
                    // the culprit's false sighting of the scapegoat: this witness had the scapegoat somewhere else around then
                    if (lie.Topic == "saw" && P.Seen != null && P.Seen == D.Scapegoat && P.Room >= 0 && P.Room != D.KillRoom && P.T0 >= 0 && Math.Abs(P.T0 - D.KillClock) < 20) return true;
                }
                // a preparation someone saw (the deck's beat plates carry the beat as their root)
                if (b.StartsWith("beat:") && P.Root == b) return true;
            }
            return false;
        }

        static Theory AnsweredTheory(DebateState D, Theory th) => th?.Answers != null && th.Answers.StartsWith("not:") ? D.Theories.FirstOrDefault(t => t.Id == th.Answers.Substring(4)) : null;

        /// <summary>Who present can turn an unturned fake at once: the person it wrongly points at (an ask: route), the honest
        /// witness of an alibi, or the court itself when a plate on its route is already public.</summary>
        static string Flipper(GameState S, TrialState T, Plate F)
        {
            var D = T.Debate; var jur = Jurors(S, T);
            foreach (var r in F.Routes.Where(r => r.StartsWith("ask:"))) { var x = r.Substring(4); if (jur.Contains(x)) return x; }
            if (F.Role == PlateRole.FalseAlibi && F.Witness != null && jur.Contains(F.Witness) && Honest(S, F.Witness)) return F.Witness;
            if (F.Routes.Any(r => r.StartsWith("pl:") && DPlate(D, r.Substring(3)) is Plate q && (q.State == PlateState.Sealed || q.State == PlateState.Flipped))) return "court";
            return null;
        }

        internal static ShowEval Evaluate(GameState S, TrialState T, Theory th, Plate P)
        {
            var D = T.Debate; var ev = new ShowEval();
            if (th == null || P == null) { ev.Outcome = "miss"; ev.Why = "이 은판으로는 가릴 수 없다"; return ev; }
            if (th.State != "standing") { ev.Outcome = "settled"; ev.Why = "그 말은 이미 정리됐다"; return ev; }
            bool proof = P.True || P.State == PlateState.Flipped;
            var own = Weigh(S, D, th, th.Claim, P);
            if (!proof)
            {
                // an unturned fake laid as proof: whoever can, turns it at once; otherwise the plain refusal (the same words a
                // true plate short of its partner would get — not a fake detector)
                var tm = T.Debate.Mysteries.FirstOrDefault(x => x.Id == th.Mystery);
                bool bears = own.r != LogicResult.Irrelevant || th.Pin == P.Id || (tm?.Claim != null && P.Supports.Contains(tm.Claim)) || (th.Target != null && P.Points == th.Target);
                if (!bears) { ev.Outcome = "miss"; ev.Why = MissWhy(D, th, P); return ev; }
                var flipper = Flipper(S, T, P);
                if (flipper != null) { ev.Outcome = "flip-laid"; ev.Flip = P; ev.Flipper = flipper; ev.Why = P.Back; if (th.Pin == P.Id && th.Leans) ev.Fallen = th; return ev; }
                ev.Outcome = "refuse"; ev.Why = "지금은 이 은판만으로는 가릴 수 없다."; return ev;
            }
            if (!th.True)
            {
                ev.R = own.r; ev.Why = own.why;
                if (own.r == LogicResult.Contradict) { ev.Outcome = "collapse"; ev.Fallen = th; ev.Flip = PinToTurn(D, th, P); return ev; }
                if (own.r == LogicResult.LimitScope || own.r == LogicResult.Conditional) { ev.Outcome = "limit"; return ev; }
                if (own.r == LogicResult.Support) { ev.Outcome = "support-wrong"; ev.Why = "오히려 그 말을 뒷받침한다 — " + own.why; return ev; }
                ev.Outcome = "miss"; ev.Why = MissWhy(D, th, P); return ev;
            }
            // a truth-side slide: borne out by the plate, or by the plate breaking the false reading it answers
            var fal = AnsweredTheory(D, th); if (fal != null && fal.State != "standing") fal = null;
            var other = fal != null ? Weigh(S, D, fal, fal.Claim, P) : (LogicResult.Irrelevant, (string)null);
            if (own.r == LogicResult.Support || other.Item1 == LogicResult.Contradict)
            {
                ev.Outcome = "seal"; ev.Sealed = th; ev.R = LogicResult.Support; ev.Why = other.Item1 == LogicResult.Contradict ? other.Item2 : own.why;
                if (fal != null && (other.Item1 == LogicResult.Contradict || AlibiAnswers(S, D, th, fal))) { ev.Fallen = fal; ev.Flip = PinToTurn(D, fal, P); }
                return ev;
            }
            ev.Outcome = "miss"; ev.Why = MissWhy(D, th, P); return ev;
        }

        /// <summary>A sealed alibi answers a theory that names the same person, when it covers the attack.</summary>
        static bool AlibiAnswers(GameState S, DebateState D, Theory alibi, Theory fal)
            => alibi.Claim != null && alibi.Claim.Kind == PropKind.AtPlace && alibi.Claim.Value == "window-cover" && fal.Target == alibi.Claim.A && alibi.Claim.T0 <= D.KillClock + 3 && alibi.Claim.T1 >= D.KillClock - 3;

        static Plate PinToTurn(DebateState D, Theory fallen, Plate P)
        {
            var F = fallen?.Pin != null ? DPlate(D, fallen.Pin) : null;
            if (F == null || F.True || F.State == PlateState.Flipped) return null;
            return F.Routes.Contains("pl:" + P.Id) || fallen.Leans ? F : null;
        }

        /// <summary>"맞지 않는다" with a plain reason: what the plate speaks to, next to what the theory is about.</summary>
        static string MissWhy(DebateState D, Theory th, Plate P)
        {
            string about = th.Claim == null ? "그 말" : th.Claim.Kind == PropKind.AtPlace ? "'어디에 있었나'" : th.Claim.Kind == PropKind.DeathPlace ? "'어디에서'" : th.Claim.Kind == PropKind.DeathWindow ? "'언제'" : th.Claim.Kind == PropKind.WeaponType ? "'무엇으로'" : th.Claim.Kind == PropKind.Culprit ? "'누가'" : "그 말";
            string says = P.Kind == PlateKind.Witness ? "누가 무엇을 봤는지" : P.Kind == PlateKind.Body ? "시신에 남은 것" : P.Kind == PlateKind.Object ? "그 물건이 어땠는지" : "그 자리에 남은 흔적";
            return LineBank.FixParticles($"이 은판은 {says}을(를) 말할 뿐, {about}에는 닿지 않는다");
        }

        // ================================================================== resolution (applied)
        /// <summary>Lay plate P against theory th, by the player or a resident. Emits the beats, moves the room, may settle the riddle.</summary>
        internal static ShowEval ApplyShow(Simulation sim, TrialState T, Theory th, Plate P, string by)
        {
            var S = sim.S; var D = T.Debate; var ev = Evaluate(S, T, th, P);
            var m = th != null ? D.Mysteries.FirstOrDefault(x => x.Id == th.Mystery) : null;
            bool player = by == Cast.Player;
            // the one laying it says so
            var slots = new Dictionary<string, string> { { "holder", "@" + th?.Holder }, { "plate", $"은판 {P?.N}, 「{P?.Title}」" } };
            bool lands = ev.Outcome == "collapse" || ev.Outcome == "seal";
            if (player) DSay(sim, T, Cast.Player, "p_show", slots, BeatKind.Line, lands && ev.Outcome == "collapse" ? "p_object" : "p_show", th?.TrialClaim, lands ? Emotion.Angry : Emotion.Neutral, Anim.Present, lands ? 0.7f : 0.5f, th?.Id, P?.Id);
            else DSay(sim, T, by, th != null && th.Holder == by ? "npc_show_own" : "npc_show", slots, BeatKind.Line, "object", th?.TrialClaim, Emotion.Angry, Anim.Present, 0.6f, th?.Id, P?.Id);
            if (P != null && P.State != PlateState.Flipped && P.State != PlateState.Sealed) P.State = PlateState.Shown;
            if (P != null) P.History.Add($"{by}>{th?.Id}:{ev.Outcome}");
            switch (ev.Outcome)
            {
                case "collapse":
                case "seal":
                    {
                        if (player) { D.Hits++; T.Valid++; T.Influence = MathX.Clamp01(T.Influence + (ev.Outcome == "collapse" ? 0.1f : 0.07f)); }
                        if (P != null && P.True) P.State = PlateState.Sealed;
                        if (player) Ally(sim, T, false);
                        if (ev.Fallen != null) MarkCollapsed(sim, T, ev.Fallen, by, ev.Why);
                        if (ev.Flip != null) FlipPlate(sim, T, ev.Flip, by);
                        if (ev.Sealed != null) SealTheory(sim, T, ev.Sealed, by, ev.Why);
                        if (ev.Fallen != null) CollapseReactions(sim, T, ev.Fallen);
                        break;
                    }
                case "flip-laid":
                    {
                        if (player) { T.Influence = MathX.Clamp01(T.Influence - 0.03f); T.Invalid++; }
                        FlipPlate(sim, T, ev.Flip, ev.Flipper);
                        if (ev.Fallen != null && ev.Fallen.State == "standing") CollapseTheory(sim, T, ev.Fallen, ev.Flipper, ev.Why, quiet: true);
                        break;
                    }
                case "limit":
                    {
                        if (player) { T.Valid++; T.Influence = MathX.Clamp01(T.Influence + 0.04f); D.Hits++; }
                        DResult(T, BeatKind.Result, by, "흔들린다 — " + ev.Why, "LimitScope", th.TrialClaim, intensity: 0.55f, theory: th.Id, plate: P?.Id);
                        var tc = T.Claims.First(c => c.Id == th.TrialClaim); tc.Status = "limited";
                        th.Conviction = Math.Max(0, th.Conviction - 0.35f);
                        int peeled = 0; foreach (var s in th.Supporters.Where(x => x != D.Target).ToList()) { if (peeled >= 2) break; th.Supporters.Remove(s); if (D.Reading[s] == th.Target) D.Reading[s] = "?"; peeled++; }
                        if (th.Holder == D.Target) CulpritShaken(sim, T, 0.25f);
                        else DSay(sim, T, th.Holder, "res_holder_stubborn", null, BeatKind.Line, "stubborn", th.TrialClaim, Emotion.Angry, Anim.CrossArms, 0.5f, th.Id);
                        break;
                    }
                case "refuse":
                    DResult(T, BeatKind.Refusal, null, ev.Why, "Irrelevant", th?.TrialClaim, intensity: 0.35f, theory: th?.Id, plate: P?.Id);
                    break;
                case "settled":
                    DResult(T, BeatKind.Refusal, null, ev.Why, "Irrelevant", th?.TrialClaim, intensity: 0.3f);
                    break;
                default:   // miss / support-wrong
                    {
                        if (player) D.Misses++;
                        if (th != null) th.Misses++;
                        if (m != null) m.Misses++;
                        bool free = m != null && m.Misses <= 1;
                        if (player && !free) { T.Invalid++; T.Influence = MathX.Clamp01(T.Influence - 0.04f); }
                        DResult(T, BeatKind.Result, null, (ev.Outcome == "support-wrong" ? "" : "맞지 않는다 — ") + ev.Why, ev.Outcome == "support-wrong" ? "Support" : "Irrelevant", th?.TrialClaim, intensity: 0.4f, theory: th?.Id, plate: P?.Id);
                        if (th != null && !free)
                        {
                            // someone gets a free line — the holder, a supporter, now and then the culprit behind them; someone leans back
                            var voices = new List<string> { th.Holder }; voices.AddRange(th.Supporters.Where(x => Jurors(S, T).Contains(x)));
                            string who = voices.Where(x => x != Cast.Player && Jurors(S, T).Contains(x)).OrderBy(x => DH(S, "scoff:" + th.Id + ":" + m?.Misses + ":" + x)).FirstOrDefault() ?? th.Holder;
                            if (Jurors(S, T).Contains(who))
                                DSay(sim, T, who, "re_scoff", null, BeatKind.Line, "scoff", th.TrialClaim, (Cast.Get(who)?.P.Pride ?? 0.5f) >= 0.6f ? Emotion.Smirk : Emotion.Neutral, Anim.CrossArms, 0.45f, th.Id);
                            if (player) Ally(sim, T, true);
                            var back = Jurors(S, T).Where(x => x != th.Holder && !th.Supporters.Contains(x) && D.Reading[x] == "?").OrderBy(x => DH(S, "lean:" + th.Id + ":" + th.Misses + ":" + x)).FirstOrDefault();
                            if (back != null) { th.Supporters.Add(back); if (th.Target != null) D.Reading[back] = th.Target; }
                        }
                        break;
                    }
            }
            SettleCheck(sim, T, m, by, ev);
            if (player && m != null && m.State == "lit" && (ev.Outcome == "miss" || ev.Outcome == "support-wrong" || ev.Outcome == "refuse")) Stuck(sim, T, m);
            return ev;
        }

        /// <summary>서윤 looks after 민혁 (Owner traits): after a miss she steadies him, now and then she marks a hit. Only while
        /// she is at a stand, not the one on trial, and not on the wrong end of his last plate.</summary>
        static void Ally(Simulation sim, TrialState T, bool miss)
        {
            var S = sim.S; var D = T.Debate; const string who = "P03";
            if (!Jurors(S, T).Contains(who) || who == D.Target || who == D.Accused) return;
            string tag = "ally:" + (D.Active ?? "-") + ":" + miss;
            if (D.Seen.Contains(tag) || D.Seen.Count(x => x.StartsWith("ally:")) >= 4) return;
            if (DH(S, "ally:" + D.Decisions.Count + ":" + miss) >= (miss ? 0.6 : 0.3)) return;
            D.Seen.Add(tag);
            DSay(sim, T, who, miss ? "ally_encourage" : "ally_praise", null, BeatKind.Line, "ally", null, Emotion.Smile, Anim.Talk, 0.35f);
        }

        /// <summary>A floor that keeps missing does not idle (§6.11): a hint after two misses, the plate after four, and at six
        /// the resident best placed lays it themselves. The culprit's own story nobody lays for 민혁 (NpcResolves): after eight
        /// misses on it the court moves on and the riddle stays open, as when he lets the floor pass.</summary>
        static void Stuck(Simulation sim, TrialState T, Mystery m)
        {
            if (m.Misses == 2) Hint(sim, T, m, 1);
            else if (m.Misses == 4) Hint(sim, T, m, 2);
            else if (m.Misses >= 6)
            {
                bool acted = FloorRequest(sim, T, m);
                if (!acted || m.Misses >= 7) acted = NpcResolves(sim, T, m) || acted;
                if (!acted && m.Misses >= 8 && m.State == "lit") SettleByRoom(sim, T, m);
            }
        }

        static void CollapseTheory(Simulation sim, TrialState T, Theory th, string by, string why, bool quiet = false)
        {
            MarkCollapsed(sim, T, th, by, why, quiet);
            if (quiet) { Cascade(sim, T, th, th.Target); return; }
            CollapseReactions(sim, T, th);
        }

        /// <summary>The theory falls (state, court record, the break on screen).</summary>
        static void MarkCollapsed(Simulation sim, TrialState T, Theory th, string by, string why, bool quiet = false)
        {
            var D = T.Debate;
            th.State = "collapsed";
            var tc = T.Claims.FirstOrDefault(c => c.Id == th.TrialClaim); if (tc != null) { tc.Status = "refuted"; tc.RefutedBy = by; tc.RefuteWhy = CaseBoard.Plain(why); }
            if (!quiet) DResult(T, BeatKind.Break, by, $"{Given(th.Holder)}의 말이 무너졌다 — {why}", "Contradict", th.TrialClaim, "break", 0.8f, th.Id);
            if (th.Lie != null && th.Holder == D.Target) { var lie = D.Lies.FirstOrDefault(l => l.Id == th.Lie); if (lie != null) lie.Broken = true; D.Mind.Pressure++; Publish(T, th, null); }
        }

        /// <summary>After a fall: the room turns, the named one breathes, the holder takes it (the honest concede, the proud dig in).</summary>
        static void CollapseReactions(Simulation sim, TrialState T, Theory th)
        {
            var S = sim.S; var D = T.Debate;
            Cascade(sim, T, th, th.Target);
            if (th.Target != null && th.Target != Cast.Player && Jurors(S, T).Contains(th.Target) && th.Target != D.Target)
                DSay(sim, T, th.Target, "res_accused_relief", null, BeatKind.Line, "relief", null, Emotion.Smile, Anim.Talk, 0.35f);
            // the holder takes it: honest ones concede (or reason on aloud), the proud dig in, the culprit covers
            if (th.Holder == D.Target) CulpritShaken(sim, T, 0.4f);
            else if (Jurors(S, T).Contains(th.Holder))
            {
                bool proud = (Cast.Get(th.Holder)?.P.Pride ?? 0.5f) >= 0.7f && th.Basis != Basis.Guess;
                if (proud || !Evolve(sim, T, th))
                    DSay(sim, T, th.Holder, proud ? "res_holder_stubborn" : "res_holder_concede", null, BeatKind.Line, proud ? "stubborn" : "recant", th.TrialClaim, proud ? Emotion.Angry : Emotion.Sad, proud ? Anim.CrossArms : Anim.Shrug, 0.45f, th.Id);
            }
            if (th.Target != null && th.Target != D.Target && D.Reversals + D.Hits > 0) Bystander(sim, T, D.Reversals % 2 == 0 ? "re_realize" : "re_fear", th.Id, th.Holder, th.Target);
            // the culprit, who backed it, goes quiet — a flicker only
            if (th.Supporters.Contains(D.Target ?? "") && th.Holder != D.Target) CulpritShaken(sim, T, 0.2f);
        }

        static void SealTheory(Simulation sim, TrialState T, Theory th, string by, string why)
        {
            var S = sim.S; var D = T.Debate;
            th.State = "sealed";
            var tc = T.Claims.FirstOrDefault(c => c.Id == th.TrialClaim); if (tc != null) tc.Status = "supported";
            if (th.Claim != null && th.Claim.Value == "window-cover") T.Public.Add(th.Claim.Clone());
            DResult(T, BeatKind.Result, by, $"{Given(th.Holder)}의 말이 맞았다 — {why}", "Support", th.TrialClaim, intensity: 0.6f, theory: th.Id);
            if (Jurors(S, T).Contains(th.Holder) && th.Holder != D.Target) DSay(sim, T, th.Holder, "res_vindicated", null, BeatKind.Line, "vindicated", th.TrialClaim, (Cast.Get(th.Holder)?.P.Pride ?? 0.5f) >= 0.6f ? Emotion.Smirk : Emotion.Smile, Anim.Talk, 0.4f, th.Id);
        }

        /// <summary>A fake turns: the photograph was real, its first reading was not. Everything that leaned on it goes too.</summary>
        internal static void FlipPlate(Simulation sim, TrialState T, Plate F, string by)
        {
            var S = sim.S; var D = T.Debate; if (F == null || F.State == PlateState.Flipped) return;
            F.State = PlateState.Flipped; F.History.Add("flipped:" + by);
            string who = by == "court" ? null : by;
            // an owner explaining themselves speaks first (a wrongly named person, or the honest witness of an alibi)
            if (who != null && who != Cast.Player && who != D.Target && (F.Role == PlateRole.Coincidence || F.Role == PlateRole.Mistaken || F.Role == PlateRole.FalseAlibi))
                DSay(sim, T, who, F.Role == PlateRole.FalseAlibi ? "ask_with_time" : "ask_owner_explains", new Dictionary<string, string> { { "reason", OwnerReason(sim, D, F, who) }, { "target", "@" + F.Seen }, { "time", ClockFmt.Vague(F.T0) } }, BeatKind.Line, "explain", null, Emotion.Neutral, Anim.Talk, 0.45f, null, F.Id);
            DBeat(T, BeatKind.Result, who ?? Cast.Player, $"은판 {F.N} 「{F.Title}」 — {F.Origin ?? "잘못 읽힘"}. {F.Back}", "twist", "flip", null, "Flip", Emotion.Surprised, Anim.None, true, 0.8f, null, F.Id);
            D.Reversals++;
            if (F.Role == PlateRole.Frame && F.Points != null && F.Points != Cast.Player && F.Points != D.Target && Jurors(S, T).Contains(F.Points))
                DSay(sim, T, F.Points, "re_framed", null, BeatKind.Line, "object", null, Emotion.Angry, Anim.Slam, 0.6f, null, F.Id);
            foreach (var th in D.Theories.Where(t => t.State == "standing" && t.Pin == F.Id && t.Leans).ToList()) CollapseTheory(sim, T, th, by, F.Back, quiet: true);
            // the person it pointed at is off the hook for the room
            if (F.Points != null && S.A(F.Points) != null) foreach (var j in Jurors(S, T)) if (D.Reading[j] == F.Points && j != D.Target) D.Reading[j] = "?";
            if (F.Users.Contains("story") && D.Target != null) { D.Mind.Pressure++; CulpritShaken(sim, T, 0.3f); }
        }

        /// <summary>The first-person reason the person a fake wrongly points at gives for it.</summary>
        static string OwnerReason(Simulation sim, DebateState D, Plate F, string who)
        {
            var S = sim.S;
            if (F.Role == PlateRole.FalseAlibi) return ClockFmt.Vague(F.T0);
            var held = F.Props.FirstOrDefault(p => p.Kind == PropKind.Held);
            if (held != null) return LineBank.FixParticles($"{ItemCatalog.Get(held.Item)?.Kor ?? "그건"}은(는) 쓸 데가 있어서 잠깐 들고 있었던 거예요. {ClockFmt.Vague(F.T0)}의 일이고요.");
            if (F.Root.StartsWith("furnsaw:")) return "가구는 제가 옮긴 게 맞아요. 치우느라고요. 그땐 아무 일도 없었어요.";
            return LineBank.FixParticles($"{S.RoomName(F.Room)}에는 {ClockFmt.Vague(F.T0)}에 잠깐 들렀을 뿐이에요. 그땐 아무 일도 없었어요.");
        }

        /// <summary>A riddle is answered when the theory voicing its first impression (or the culprit's alibi) has fallen, or when
        /// no false theory stands and a truth-side one is sealed.</summary>
        static void SettleCheck(Simulation sim, TrialState T, Mystery m, string by, ShowEval ev)
        {
            var D = T.Debate; if (m == null || m.State != "lit") return;
            var all = m.Theories.Select(id => DTheory(D, id)).Where(t => t != null).ToList();
            var core = CoreOf(D, m, all);
            bool answered = core != null ? core.State == "collapsed" : all.Any(t => t.State == "sealed") && !all.Any(t => t.State == "standing" && !t.True);
            if (!answered) return;
            string how = ev?.Outcome == "seal" ? "seal" : ev?.Outcome == "flip-laid" ? "flip" : ev?.Outcome == "withdrawn" ? "withdrawn" : "collapse";
            Settle(sim, T, m, by, how);
        }

        static Theory CoreOf(DebateState D, Mystery m, List<Theory> all)
        {
            if (m.Trick == "Who") return all.FirstOrDefault(t => t.Holder == D.Target && t.Lie != null);
            if (m.Trick == "Wound") return all.FirstOrDefault(t => !t.True);
            var k = KClaim(D, m.Claim);
            return all.FirstOrDefault(t => !t.True && t.Claim != null && k?.Presented != null && t.Claim.Kind == k.Presented.Kind && t.Claim.Value == k.Presented.Value && t.Claim.Room == k.Presented.Room)
                ?? all.FirstOrDefault(t => !t.True && t.Leans);
        }

        internal static void Settle(Simulation sim, TrialState T, Mystery m, string by, string how)
        {
            var S = sim.S; var D = T.Debate; if (m.State == "settled") return;
            m.State = "settled"; m.SettledBy = (by == Cast.Player ? "player" : by == null ? "room" : "npc") + ":" + how;
            // what stands now is engraved; wrong slides still up fall quietly
            foreach (var t in m.Theories.Select(id => DTheory(D, id)).Where(t => t != null && t.State == "standing" && !t.True))
            { t.State = "collapsed"; var tc = T.Claims.FirstOrDefault(c => c.Id == t.TrialClaim); if (tc != null) tc.Status = "refuted"; }
            foreach (var t in m.Theories.Select(id => DTheory(D, id)).Where(t => t != null && t.State == "standing" && t.True && t.Answers != null))
            { t.State = "sealed"; var tc = T.Claims.FirstOrDefault(c => c.Id == t.TrialClaim); if (tc != null) tc.Status = "supported"; }
            if (how == "withdrawn") AddPlaque(sim, T, m, SoftPlaque(sim, D, m), null, by ?? "room");
            else AddPlaque(sim, T, m, PlaqueText(sim, D, m), PlaqueProp(D, m), by ?? "room");
            D.FloorOpen = false; D.FloorTheory = null;
            if (T.PendingPrompt != null && T.PendingPrompt.StartsWith("chain:")) T.PendingPrompt = null;
            D.Step = "settle";
            S.Log("DebateSettle", by, data: $"{m.Id}:{m.Trick}:{m.SettledBy}");
        }

        /// <summary>No theory left standing and nothing proven: the room moves on without a plaque (the riddle is set aside).</summary>
        static void SettleByRoom(Simulation sim, TrialState T, Mystery m)
        {
            var D = T.Debate;
            m.State = "deferred"; m.SettledBy = "room:deferred";
            DNarrate(T, "이 수수께끼는 끝내 가려지지 않은 채 남는다.", BeatKind.Result);
            D.FloorOpen = false; D.Active = null; D.Step = "lit";
        }

        static void AfterSettle(Simulation sim, TrialState T, Mystery m)
        {
            var S = sim.S; var D = T.Debate;
            string reason = ReframeText(sim, D, m);
            if (reason != null && m.State == "settled" && m.Trick != "Who" && PlayerIn(S, T))
                DSay(sim, T, Cast.Player, "p_reframe", new Dictionary<string, string> { { "reason", reason } }, BeatKind.Line, "reframe", null, Emotion.Neutral, Anim.Think, 0.5f);
            if (m.Trick == "Who" && m.State == "settled") { D.Step = "infer"; return; }
        }

        // ================================================================== the player's calls (intercepted from TrialSystem)
        internal static Result DebateShow(Simulation sim, string claimId, string cardId)
        {
            var S = sim.S; var T = S.Trial; var D = T.Debate;
            var th = TheoryOfClaim(D, claimId); var P = PlateOfCard(D, cardId);
            if (th == null || P == null) return new Result { R = LogicResult.Irrelevant, Text = P == null ? "이 심판에서는 은판만 내밀 수 있다" : "그 말은 지금 가릴 수 있는 가설이 아니다" };
            Decide(D, ActionKind.Show, th.Id + "<" + P.Id, true);
            if (D.Act == "act3" && th.Id == D.Mind.Counter) { var dv = DuelShow(sim, T, th, P); return dv; }
            var ev = ApplyShow(sim, T, th, P, Cast.Player);
            Decided(D, ev.Outcome);
            AfterPlayerMove(sim, T);
            return new Result { R = ev.Outcome == "collapse" ? LogicResult.Contradict : ev.Outcome == "seal" ? LogicResult.Support : ev.Outcome == "limit" ? LogicResult.LimitScope : LogicResult.Irrelevant, Text = CaseBoard.Plain(ev.Why ?? ""), Valid = ev.Outcome == "collapse" || ev.Outcome == "seal" || ev.Outcome == "limit" || ev.Outcome == "flip-laid", Break = ev.Outcome == "collapse" };
        }

        internal static Result DebatePresentCard(Simulation sim, string cardId)
        {
            var T = sim.S.Trial; var D = T.Debate;
            var th = DTheory(D, D.FloorTheory ?? D.Mind.Counter); if (th == null) { var tc = D.Theories.LastOrDefault(t => t.State == "standing"); th = tc; }
            return th == null ? new Result { Text = "지금은 내밀 곳이 없다" } : DebateShow(sim, th.TrialClaim, cardId);
        }

        /// <summary>캐묻기 — "그걸 어떻게 아셨어요?": exposes the basis; the culprit must spend a lie; a wrongly named person explains.</summary>
        internal static void DebateAsk(Simulation sim, string claimId)
        {
            var S = sim.S; var T = S.Trial; var D = T.Debate;
            var th = TheoryOfClaim(D, claimId); if (th == null || th.State != "standing" || th.Holder == Cast.Player) return;
            Decide(D, ActionKind.Ask, th.Id, true); D.Asks++;
            string outcome = D.Act == "act3" && th.Id == D.Mind.Counter ? DuelAsk(sim, T, th) : AskTheory(sim, T, th, Cast.Player);
            Decided(D, outcome);
            AfterPlayerMove(sim, T);
        }

        internal static string AskTheory(Simulation sim, TrialState T, Theory th, string by)
        {
            var S = sim.S; var D = T.Debate; var m = D.Mysteries.FirstOrDefault(x => x.Id == th.Mystery); var jur = Jurors(S, T);
            DSay(sim, T, by, "p_ask", new Dictionary<string, string> { { "holder", "@" + th.Holder } }, BeatKind.Line, "p_ask", th.TrialClaim, Emotion.Neutral, Anim.Talk, 0.45f, th.Id);
            if (th.Asked >= 2) { DSay(sim, T, th.Holder, "ask_again", null, BeatKind.Line, "again", th.TrialClaim, Emotion.Angry, Anim.CrossArms, 0.4f, th.Id); return "again"; }
            th.Asked++;
            var pin = th.Pin != null ? DPlate(D, th.Pin) : null;
            // the culprit's own story: they must hold it together — and the honest witness they named gives the hour they really saw them
            if (th.Holder == D.Target && th.Lie != null)
            {
                string with = D.ClaimWith.FirstOrDefault(x => jur.Contains(x)) ?? pin?.Witness;
                // the answer fits the lie: a room (with whoever can vouch, or alone), a weapon they never saw, a sighting they
                // stand by — not a room line with its blanks empty
                string key = th.Claim?.Kind == PropKind.Held ? "duel_ask_weapon" : th.Claim?.Kind == PropKind.AtPlace && th.Claim.Room >= 0 ? (with != null ? "ask_culprit_detail" : "duel_ask_where") : th.Claim?.Value == "push" ? "duel_ask_push" : "duel_ask_final";
                DSay(sim, T, th.Holder, key, new Dictionary<string, string> { { "place", th.Claim?.Room >= 0 ? PlaceWord(S, th.Holder, th.Claim.Room) : "" }, { "with", with != null ? "@" + with : "" }, { "target", "@" + (th.Claim?.A ?? D.Scapegoat) }, { "victim", "@" + D.Victim } }, BeatKind.Line, "counter", th.TrialClaim, Emotion.Neutral, Anim.CrossArms, 0.5f, th.Id);
                var lie = D.Lies.FirstOrDefault(l => l.Id == th.Lie); D.Mind.Spent += lie?.Cost ?? 1; D.Mind.Pressure++;
                if (pin != null && pin.Role == PlateRole.FalseAlibi && pin.State != PlateState.Flipped && pin.Witness != null && jur.Contains(pin.Witness) && Honest(S, pin.Witness))
                {
                    FlipPlate(sim, T, pin, pin.Witness);
                    th.Conviction = Math.Max(0, th.Conviction - 0.4f);
                    var tc = T.Claims.First(c => c.Id == th.TrialClaim); tc.Status = "limited";
                    foreach (var s in th.Supporters.Where(x => x != D.Target).ToList()) th.Supporters.Remove(s);
                    CulpritShaken(sim, T, 0.35f);
                    return "shaken";
                }
                CulpritShaken(sim, T, 0.2f);
                return "held";
            }
            // a wrongly named person can explain what the witness saw
            var ask = pin != null && !pin.True && pin.State != PlateState.Flipped ? pin.Routes.FirstOrDefault(r => r.StartsWith("ask:") && jur.Contains(r.Substring(4)) && r.Substring(4) != th.Holder) : null;
            if (ask != null)
            {
                DSay(sim, T, th.Holder, "ask_saw", new Dictionary<string, string> { { "fragment", Frag(th) } }, BeatKind.Line, "answer", th.TrialClaim, Emotion.Neutral, Anim.Talk, 0.4f, th.Id);
                FlipPlate(sim, T, pin, ask.Substring(4));
                if (th.State == "standing" && th.Leans) CollapseTheory(sim, T, th, by, pin.Back, quiet: true);
                if (th.State == "collapsed" && Jurors(S, T).Contains(th.Holder)) DSay(sim, T, th.Holder, "res_holder_concede", null, BeatKind.Line, "recant", th.TrialClaim, Emotion.Sad, Anim.Shrug, 0.4f, th.Id);
                SettleCheck(sim, T, m, by, new ShowEval { Outcome = "flip-laid" });
                return "turned";
            }
            switch (th.Basis)
            {
                case Basis.Guess:
                    DSay(sim, T, th.Holder, "ask_guess", null, BeatKind.Line, "recant", th.TrialClaim, Emotion.Sad, Anim.Shrug, 0.4f, th.Id);
                    th.Conviction = 0.1f; foreach (var s in th.Supporters.Where(x => x != D.Target).ToList()) { th.Supporters.Remove(s); if (th.Target != null && D.Reading[s] == th.Target) D.Reading[s] = "?"; }
                    if (!th.True) { th.State = "collapsed"; var tc = T.Claims.First(c => c.Id == th.TrialClaim); tc.Status = "retracted"; DResult(T, BeatKind.Result, by, $"{Given(th.Holder)}이(가) 가설을 거둔다 — 본 것이 아니라 짐작이었다", "Conditional", th.TrialClaim, intensity: 0.5f, theory: th.Id); SettleCheck(sim, T, m, by, new ShowEval { Outcome = "withdrawn" }); return "withdrawn"; }
                    return "guess";
                case Basis.Hearsay:
                    DSay(sim, T, th.Holder, "ask_hearsay", new Dictionary<string, string> { { "with", "@" + (th.From ?? D.Target) } }, BeatKind.Line, "answer", th.TrialClaim, Emotion.Neutral, Anim.Talk, 0.4f, th.Id);
                    th.Conviction = Math.Max(0, th.Conviction - 0.2f); T.Claims.First(c => c.Id == th.TrialClaim).Status = "conditional";
                    return "hearsay";
                case Basis.Heard:
                    DSay(sim, T, th.Holder, "ask_heard", new Dictionary<string, string> { { "fragment", Frag(th) } }, BeatKind.Line, "answer", th.TrialClaim, Emotion.Neutral, Anim.Talk, 0.4f, th.Id);
                    if (th.True) Firm(sim, T, th);
                    return "firm";
                case Basis.Plate:
                    DSay(sim, T, th.Holder, "ask_plate", new Dictionary<string, string> { { "plate", pin != null ? $"「{pin.Title}」" : "은판" } }, BeatKind.Line, "answer", th.TrialClaim, Emotion.Neutral, Anim.Talk, 0.4f, th.Id);
                    return "plate";
                default:
                    DSay(sim, T, th.Holder, "ask_saw", new Dictionary<string, string> { { "fragment", Frag(th) } }, BeatKind.Line, "answer", th.TrialClaim, Emotion.Neutral, Anim.Talk, 0.4f, th.Id);
                    if (th.True) Firm(sim, T, th);
                    return "firm";
            }
        }

        static string Frag(Theory th) => string.IsNullOrEmpty(th.Fragment) ? "" : th.Fragment.Split('—')[0].Trim().TrimEnd('.') + ".";

        /// <summary>An honest first-hand answer: someone who was on the fence moves toward it.</summary>
        static void Firm(Simulation sim, TrialState T, Theory th)
        {
            var S = sim.S; var D = T.Debate;
            var who = Jurors(S, T).Where(x => x != th.Holder && !th.Supporters.Contains(x) && x != D.Target).OrderBy(x => DH(S, "firm:" + th.Id + ":" + x)).FirstOrDefault();
            if (who != null) th.Supporters.Add(who);
            th.Conviction = Math.Min(1, th.Conviction + 0.1f);
        }

        internal static void DebateAccuse(Simulation sim, string target)
        {
            var S = sim.S; var T = S.Trial; var D = T.Debate;
            if (target == null || D.Act != "act3") return;
            Decide(D, ActionKind.Accuse, target, true);
            T.PlayerAccused = target; T.Accused = target; D.Accused = target;
            var c = new TrialClaim { Id = "c" + (T.Claims.Count + 1), Speaker = Cast.Player, Prop = new Prop { Kind = PropKind.Culprit, A = target, B = D.Victim }, Key = "p_accuse", Topic = "debate", Player = true, Accused = target, Beat = T.Beats.Count };
            T.Claims.Add(c);
            DSay(sim, T, Cast.Player, "p_accuse", new Dictionary<string, string> { { "target", "@" + target } }, BeatKind.Line, "p_name", c.Id, Emotion.Angry, Anim.Point, 0.8f);
            foreach (var j in Jurors(S, T)) if (j != target && !Protects(S, j, target) && D.Reading[j] == "?" && DH(S, "follow:" + j) < 0.5 + T.Influence * 0.4) D.Reading[j] = target;
            Decided(D, target == D.Target ? "culprit" : "innocent");
            D.Step = "duel-open";
        }

        static void AfterPlayerMove(Simulation sim, TrialState T)
        {
            var D = T.Debate; D.ActionAt = T.Beats.Count;
            if (D.Step == "await") D.Step = DActive(D)?.State == "lit" ? "floor" : DActive(D)?.State == "settled" ? "settle" : "lit";
        }

        static void Decide(DebateState D, ActionKind kind, string detail, bool counted)
        {
            D.Decisions.Add(new DebateDecision { Beat = D.Beats.Count, Kind = kind, Counted = counted, Detail = detail, Options = D.FloorTheory != null ? 1 + D.Chips.Count : 1 });
            D.ReadableSinceDecision = 0;
        }
        static void Decided(DebateState D, string outcome) { var d = D.Decisions.LastOrDefault(); if (d != null) d.Outcome = outcome; }

        // ================================================================== the floor let pass (더 듣기)
        static void FloorPassed(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate; var m = DActive(D);
            if (m == null || m.State != "lit") { D.Step = m?.State == "settled" ? "settle" : "lit"; return; }
            Decide(D, ActionKind.Listen, m.Id, false); Decided(D, "pass");
            D.Passes++; m.Passes++;
            if (PlayerIn(S, T) && m.Passes == 1) DSay(sim, T, Cast.Player, "p_listen", null, BeatKind.Line, "p_listen", null, Emotion.Neutral, Anim.Listen, 0.3f);
            if (m.Passes == 1 && FloorRequest(sim, T, m)) { D.Step = "floor"; return; }
            if (m.Passes <= 2 && Hint(sim, T, m, m.Passes)) { D.Step = "floor"; return; }
            // the room will not idle: the resident best placed lays the plate themselves
            if (NpcResolves(sim, T, m)) { if (m.State == "lit") D.Step = "floor"; return; }
            SettleByRoom(sim, T, m);
        }

        /// <summary>민혁's inner voice (tier 1: where to look; tier 2: which plate). Never names the answer.</summary>
        static bool Hint(Simulation sim, TrialState T, Mystery m, int tier)
        {
            var S = sim.S; var D = T.Debate; if (!PlayerIn(S, T)) return false;
            var best = BestMove(sim, T, m); if (best.th == null) return false;
            D.Hints++;
            if (tier <= 1) DNarrate(T, $"({Given(best.th.Holder)}의 말… 어딘가 걸린다. {(best.p.Kind == PlateKind.Body ? "시신이 말해 주는 것" : best.p.Kind == PlateKind.Witness ? "누가 무엇을 봤는지" : "그 자리에 남은 흔적")}을(를) 다시 떠올려 보자.)", BeatKind.Inner);
            else DNarrate(T, $"(은판 {best.p.N}, 「{best.p.Title}」. 거기 찍힌 게 {Given(best.th.Holder)}의 말과 맞나?)", BeatKind.Inner);
            return true;
        }

        /// <summary>The move that would answer the riddle now (theory × plate), for hints, NPCs and the smart test player.</summary>
        internal static (Theory th, Plate p, string outcome) BestMove(Simulation sim, TrialState T, Mystery m, Func<Theory, bool> allow = null)
        {
            var D = T.Debate;
            var standing = m.Theories.Select(id => DTheory(D, id)).Where(t => t != null && t.State == "standing").ToList();
            foreach (var want in new[] { "collapse", "seal" })
                foreach (var th in standing.Where(t => allow == null || allow(t)).OrderByDescending(t => t.Id == D.FloorTheory ? 1 : 0).ThenBy(t => D.Theories.IndexOf(t)))
                    foreach (var p in D.Deck.Plates.OrderBy(p => p.N))
                    {
                        var ev = Evaluate(sim.S, T, th, p);
                        if (ev.Outcome == want && (ev.Fallen != null && CoreOf(D, m, standing.Concat(m.Theories.Select(id => DTheory(D, id))).Distinct().ToList()) == ev.Fallen || want == "seal" || ev.Outcome == "collapse")) return (th, p, ev.Outcome);
                    }
            return (null, null, null);
        }

        static bool NpcResolves(Simulation sim, TrialState T, Mystery m)
        {
            var S = sim.S; var D = T.Debate;
            // the room steps in on first impressions and on each other's mistakes, but the culprit's own story (where they were,
            // whom they saw) is 민혁's to break: residents who could break it speak up (FloorRequest) — laying the plate is his.
            // With nobody at his stand (a spectator run) there is no one else to do it.
            bool player = PlayerIn(S, T);
            bool Crux(Theory t) => D.Target != null && ((t.Holder == D.Target && t.Lie != null)
                || (m.Trick == "Who" && ((t.Target != null && t.Target == D.Target) || (t.Claim != null && t.Claim.A == D.Target && t.Holder != D.Target)
                                         || (AnsweredTheory(D, t) is Theory a && a.Holder == D.Target && a.Lie != null))));
            var best = BestMove(sim, T, m, player ? (Func<Theory, bool>)(t => !Crux(t)) : null); if (best.th == null) return false;
            var jur = Jurors(S, T);
            string who = best.p.FoundBy != null && jur.Contains(best.p.FoundBy) && best.p.FoundBy != D.Target ? best.p.FoundBy
                       : PickHolder(sim, T, jur, x => Obs(x) + Arg(x), "npcres:" + m.Id, D.Target, best.th.Holder);
            if (who == null) return false;
            if (PlayerIn(S, T)) T.Influence = MathX.Clamp01(T.Influence - 0.05f);
            DSay(sim, T, who, "npc_takes_floor", null, BeatKind.Interrupt, "object", best.th.TrialClaim, Emotion.Angry, Anim.Slam, 0.55f, best.th.Id);
            ApplyShow(sim, T, best.th, best.p, who);
            return true;
        }

        // ================================================================== 그렇다면… (the inference, on the engraved-question screen)
        static void StartInfer(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate;
            var opts = InferOptions(sim, D); if (opts == null) { D.Step = "r2"; return; }
            if (!PlayerIn(S, T)) { InferSettled(sim, T, opts.Value.right, Jurors(S, T).OrderByDescending(x => Obs(x)).First()); D.Step = "r2"; return; }
            var G = TrialGames.NewGame(T, "question"); G.Title = "그렇다면…"; G.Subtitle = "추론"; G.Why = "debate";
            G.Question = opts.Value.question; G.Word = opts.Value.right;
            G.Pool = new[] { opts.Value.right, opts.Value.wrong1, opts.Value.wrong2 }.Where(x => x != null).OrderBy(x => DH(S, "infer:" + x)).ToList();
            G.TimeLimit = 60;
            TrialGames.BeginGame(T, G);
            D.Step = "infer-wait";
        }

        /// <summary>Three inferences from the public facts; one follows. Principles, never a name: the name is the 지목's job.</summary>
        static (string question, string right, string wrong1, string wrong2)? InferOptions(Simulation sim, DebateState D)
        {
            var S = sim.S; string V = Given(D.Victim);
            var who = D.Mysteries.FirstOrDefault(m => m.Trick == "Who");
            var alibi = who == null ? null : D.Theories.FirstOrDefault(t => t.Mystery == who.Id && t.Holder == D.Target && t.Lie != null);
            string kill = S.RoomName(D.KillRoom);
            string right = alibi != null && alibi.State == "collapsed"
                ? $"{V}을(를) 친 사람은 그 무렵 {kill} 가까이에 있으면서, 다른 곳에 있었다고 말한 사람이다"
                : $"{V}을(를) 친 사람은 그 무렵 {kill} 가까이에 있었던 사람이다";
            string w1 = D.Trick == "Message" ? "피 글씨가 가리킨 사람이 범인이다 — 글씨는 거짓말을 하지 않는다"
                      : D.Trick == "Swap" ? "시신 옆의 흉기를 마지막에 만진 사람이 범인이다"
                      : D.Trick == "Seal" ? "문을 안에서 잠근 건 피해자 자신이다 — 범인은 없다"
                      : "처음 시신을 발견한 사람이 범인이다";
            var coin = D.Deck.Plates.FirstOrDefault(p => p.Role == PlateRole.Coincidence);
            string w2 = coin != null ? "사건이 일어나기 전에 근처에서 흉기가 될 만한 걸 들고 있던 사람이 범인이다" : $"{V}은(는) 발견되기 직전에 공격당했다 — 발견한 사람이 가장 수상하다";
            return ("그렇다면, " + V + "을(를) 친 사람은 어떤 사람인가?", LineBank.FixParticles(right), LineBank.FixParticles(w1), LineBank.FixParticles(w2));
        }

        static void InferSettled(Simulation sim, TrialState T, string right, string by)
        {
            var S = sim.S; var D = T.Debate;
            if (by == Cast.Player) DSay(sim, T, Cast.Player, "p_infer", new Dictionary<string, string> { { "reason", right } }, BeatKind.Line, "reveal", null, Emotion.Angry, Anim.Point, 0.8f);
            else DText(sim, T, by, right + (Cast.Get(by)?.Speech.PoliteDefault ?? true ? " …그런 거죠?" : " …그런 거지?"), BeatKind.Line, "reveal", null, Emotion.Neutral, Anim.Point, 0.7f);
            var who = D.Mysteries.FirstOrDefault(m => m.Trick == "Who");
            D.Plaques.Add(new Plaque { Id = "q" + (D.Plaques.Count + 1), Mystery = who?.Id, Text = right, By = by });
            DBeat(T, BeatKind.Plaque, by, right, "twist", "infer", null, "Infer", Emotion.Neutral, Anim.None, true, 0.85f);
            D.Reversals++;
        }

        internal static int DebateInferPick(Simulation sim, string choice)
        {
            var S = sim.S; var T = S.Trial; var D = T.Debate; var G = T.Game;
            if (G == null || G.Status != "open") return 0;
            Decide(D, ActionKind.Infer, choice, true);
            if (choice == G.Word)
            {
                Decided(D, "right"); D.Hits++; T.Valid++; T.Influence = MathX.Clamp01(T.Influence + 0.08f);
                InferSettled(sim, T, G.Word, Cast.Player);
                TrialGames.EndGame(T, G, "won"); D.Step = "r2";
                return 2;
            }
            Decided(D, "wrong"); G.Misses++; D.Misses++; T.Invalid++; T.Influence = MathX.Clamp01(T.Influence - 0.04f);
            DResult(T, BeatKind.Result, null, "맞지 않는다 — 지금까지 드러난 사실로는 그렇게 말할 수 없다", "Irrelevant", intensity: 0.4f);
            if (G.Misses >= 2)
            {
                var by = Jurors(S, T).Where(x => x != D.Target).OrderByDescending(x => Obs(x) + Arg(x)).ThenBy(x => x, StringComparer.Ordinal).First();
                InferSettled(sim, T, G.Word, by); T.Influence = MathX.Clamp01(T.Influence - 0.04f);
                TrialGames.EndGame(T, G, "lost"); D.Step = "r2";
                return -2;
            }
            return -1;
        }

        internal static void DebateInferTimeout(Simulation sim)
        {
            var S = sim.S; var T = S.Trial; var D = T.Debate; var G = T.Game; if (G == null || G.Status != "open") return;
            var by = Jurors(S, T).Where(x => x != D.Target).OrderByDescending(x => Obs(x) + Arg(x)).ThenBy(x => x, StringComparer.Ordinal).First();
            InferSettled(sim, T, G.Word, by);
            TrialGames.EndGame(T, G, "timeout"); D.Step = "r2";
        }

        // ================================================================== automation (headless tests, the probe) — never drives play
        internal static bool DebateProbe(GameState S, string claimId, string cardId)
        {
            var T = S.Trial; var D = T?.Debate; if (D == null) return false;
            var th = TheoryOfClaim(D, claimId); var P = PlateOfCard(D, cardId); if (th == null || P == null) return false;
            if (D.Act == "act3" && th.Id == D.Mind.Counter) return DuelAnswers(S, D, th, P) != null && !Stale(S, D, th, P);
            var ev = Evaluate(S, T, th, P); return ev.Outcome == "collapse" || ev.Outcome == "seal";
        }

        internal static void DebateAutoResolve(Simulation sim, bool smart)
        {
            var S = sim.S; var T = S.Trial; var D = T.Debate; var p = T.PendingPrompt;
            if (p == "accuse") { T.PendingPrompt = null; var who = AutoAccuse(sim, smart); Trace(T, "ACCUSE " + Given(who)); DebateAccuse(sim, who); return; }
            if (p == "game:question" && T.Game != null && T.Game.Status == "open")
            {
                if (smart) { Trace(T, "INFER " + T.Game.Word); DebateInferPick(sim, T.Game.Word); }
                else { var w = T.Game.Pool.OrderBy(x => DH(S, "naive-infer:" + x)).First(); Trace(T, "INFER " + w); DebateInferPick(sim, w); }
                if (T.Game != null && T.Game.Status == "open") DebateInferTimeout(sim);
                return;
            }
            if (p != null && p.StartsWith("game:")) T.PendingPrompt = null;
        }

        static string AutoAccuse(Simulation sim, bool smart)
        {
            var S = sim.S; var T = S.Trial; var D = T.Debate;
            if (smart)
            {
                // the one whose own story fell in court
                var liar = D.Theories.Where(t => t.State == "collapsed" && t.Lie != null).Select(t => t.Holder).FirstOrDefault();
                if (liar != null) return liar;
            }
            return Jurors(S, T).GroupBy(j => D.Reading[j]).Where(g => g.Key != "?").OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key).FirstOrDefault()
                ?? Jurors(S, T).First();
        }

        /// <summary>The headless 심판 (tests, spectator runs). Policies: smart (plays the best move the court's plates allow),
        /// naive (lays whatever plate is first to hand, asks now and then), passive (always listens).</summary>
        /// <summary>Tests only: when set, the headless player writes what it did ("&lt;beat index&gt;|&lt;action&gt;").</summary>
        internal static List<string> DebateTraceLog;
        static void Trace(TrialState T, string what) => DebateTraceLog?.Add(T.Beats.Count + "|" + what);

        internal static void DebateHeadless(Simulation sim, string policy)
        {
            var S = sim.S; int guard = 0;
            while (S.Trial != null && !S.Trial.Finished && guard++ < 3000)
            {
                var T = S.Trial; var D = T.Debate;
                if (T.PendingPrompt != null)
                {
                    var p = T.PendingPrompt;
                    if (p == "vote") { var v = D.Accused ?? AutoAccuse(sim, policy == "smart"); Trace(T, "VOTE " + v); PlayerVote(sim, v); T.PendingPrompt = null; continue; }
                    if (p == "accuse" || p.StartsWith("game:")) { Trace(T, "PROMPT " + p); TrialGames.AutoResolve(sim, policy == "smart"); if (T.PendingPrompt == p) T.PendingPrompt = null; continue; }
                    if (p.StartsWith("chain:")) { T.PendingPrompt = null; HeadlessFloor(sim, T, policy, p.Substring(6)); continue; }
                    T.PendingPrompt = null; continue;
                }
                var b = Next(sim); if (b == null && T.PendingPrompt == null && !T.Finished) { DebateDirect(sim, T); if (T.Cursor >= T.Beats.Count && T.PendingPrompt == null && guard > 2900) break; }
            }
        }

        static void HeadlessFloor(Simulation sim, TrialState T, string policy, string focusClaim)
        {
            var S = sim.S; var D = T.Debate;
            if (policy == "passive") { Trace(T, "PASS"); return; }
            var focus = TheoryOfClaim(D, focusClaim); if (focus == null) { Trace(T, "PASS (no focus)"); return; }
            var on = new List<Theory> { focus }; on.AddRange(T.Claims.First(c => c.Id == focusClaim).Premises.Select(id => TheoryOfClaim(D, id)).Where(t => t != null));
            if (policy == "smart")
            {
                foreach (var th in on.Where(t => t.State == "standing"))
                    foreach (var p in D.Deck.Plates.OrderBy(p => p.N))
                        if (DebateProbe(S, th.TrialClaim, "plate:" + p.Id)) { Trace(T, $"SHOW #{p.N} → {th.Id}({Given(th.Holder)})"); DebateShow(sim, th.TrialClaim, "plate:" + p.Id); return; }
                var ask = on.FirstOrDefault(t => t.State == "standing" && t.Asked == 0 && (t.Basis == Basis.Guess || t.Basis == Basis.Hearsay || (t.Holder == D.Target && t.Lie != null) || (t.Pin != null && DPlate(D, t.Pin) is Plate q && !q.True && q.Routes.Any(r => r.StartsWith("ask:")))));
                if (ask != null) { Trace(T, $"ASK {ask.Id}({Given(ask.Holder)})"); DebateAsk(sim, ask.TrialClaim); return; }
                Trace(T, "PASS");
                return;   // listen
            }
            // naive: the first plate that seems to speak to the same thing; now and then a question
            var tgt = on.OrderBy(t => DH(S, "naive-t:" + t.Id + ":" + D.Decisions.Count)).First();
            if (DH(S, "naive-ask:" + tgt.Id + ":" + D.Decisions.Count) < 0.25 && tgt.Asked == 0) { Trace(T, $"ASK {tgt.Id}({Given(tgt.Holder)})"); DebateAsk(sim, tgt.TrialClaim); return; }
            var plate = D.Deck.Plates.OrderBy(p => DH(S, "naive-p:" + p.Id + ":" + D.Decisions.Count)).First();
            Trace(T, $"SHOW #{plate.N} → {tgt.Id}({Given(tgt.Holder)})");
            DebateShow(sim, tgt.TrialClaim, "plate:" + plate.Id);
        }
    }
}
