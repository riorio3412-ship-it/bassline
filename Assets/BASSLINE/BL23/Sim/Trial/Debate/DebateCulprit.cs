using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// The culprit fights (TrialReforge §6.6, §6.9). Before 지목 they are one voice among many: they lean on the wrong theory,
    /// push someone at R1, give their alibi. After 지목 their composure candles light and they counter from their own
    /// TrialPack — the alibi, the weapon, the scapegoat, the fallbacks that concede a little and keep the rest — and every
    /// counter answered by a real plate snuffs a candle and fills a knot (기회 · 수단 · 거짓). The last one is always
    /// "증거 대 봐". At a third of the candles their tell shows; at none they break, quietly. Innocents never get candles.
    /// </summary>
    public static partial class TrialSystem
    {
        // ================================================================== before 지목
        /// <summary>R1: the room is turned toward someone — an unturned coincidence, or the scapegoat. An honest resident with a
        /// reason goes first as often as the culprit does.</summary>
        static void CulpritPush(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate; var jur = Jurors(S, T);
            var who = D.Mysteries.FirstOrDefault(m => m.Trick == "Who");
            bool Cleared(string x) => D.Plaques.Any(q => q.Prop != null && q.Prop.Kind == PropKind.AtPlace && q.Prop.A == x && q.Prop.Value == "window-cover")
                                   || D.Theories.Any(t => t.State == "sealed" && t.Claim != null && t.Claim.Kind == PropKind.AtPlace && t.Claim.A == x && t.Claim.Value == "window-cover");
            var coins = D.Deck.Plates.Where(p => p.Role == PlateRole.Coincidence && p.State != PlateState.Flipped && p.Points != null && jur.Contains(p.Points) && p.Points != D.Target && !Cleared(p.Points)).OrderBy(p => p.N).ToList();
            // whom: the room's own leading suspicion when it has weight (the obvious suspect), else an unturned coincidence, else
            // the scapegoat the culprit prepared
            var lead = jur.GroupBy(j => D.Reading[j]).Where(g => g.Key != "?" && g.Key != D.Target && jur.Contains(g.Key) && !Cleared(g.Key)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).FirstOrDefault();
            bool goatOk = D.Scapegoat != null && jur.Contains(D.Scapegoat) && !Cleared(D.Scapegoat);
            string P = lead != null && lead.Count() >= 3 ? lead.Key : coins.FirstOrDefault()?.Points ?? (goatOk ? D.Scapegoat : null);
            if (P == null || who == null) { DNarrate(T, "좌중은 아직 누구에게도 기울지 않는다.", BeatKind.Cascade); return; }
            var coin = coins.FirstOrDefault(p => p.Points == P);
            string honest = PickHolder(sim, T, jur.Where(x => x != P && x != D.Target && ((coin != null && coin.Witness == x) || Resents(S, x, P))), x => (coin?.Witness == x ? 1.5 : 0) + (Resents(S, x, P) ? 1 : 0) + Arg(x), "push:" + P, P, D.Target);
            bool culpritFirst = honest == null || DH(S, "pushfirst:" + D.Incident) < 0.35;
            string pusher = culpritFirst && jur.Contains(D.Target ?? "") ? D.Target : honest ?? D.Target;
            if (pusher == null) return;
            string frag, pin = null, lie = null; Basis basis = Basis.Guess;
            var sawLie = D.Lies.FirstOrDefault(l => l.Topic == "saw" && !l.Used);
            if (pusher == D.Target && P == D.Scapegoat && sawLie != null) { frag = DebateLines.InRegister(pusher, sawLie.Text); lie = sawLie.Id; sawLie.Used = true; basis = Basis.Saw; D.Mind.LiesUsed.Add(sawLie.Id); }
            else if (coin != null && coin.Points == P && (pusher == coin.Witness || pusher == D.Target)) { frag = pusher == coin.Witness ? coin.Face.Split('—')[0].Trim() + "." : $"{Given(coin.Witness)}이(가) 봤대요. {coin.Face.Split('—')[0].Trim()}."; pin = coin.Id; basis = pusher == coin.Witness ? Basis.Saw : Basis.Hearsay; }
            else frag = RelFragment(sim, D, pusher, P);
            var th = Voice(sim, T, who, pusher, Family.Blame, basis, new Prop { Kind = PropKind.Culprit, A = P, B = D.Victim, Value = "push" }, "push_scapegoat",
                new Dictionary<string, string> { { "target", "@" + P }, { "fragment", frag } }, frag, pin, false, P, lie, leans: pin != null, key: "steer", emo: Emotion.Angry, gesture: Anim.Point,
                from: basis == Basis.Hearsay ? coin?.Witness : null);
            string echo = pusher == D.Target ? honest : (jur.Contains(D.Target ?? "") ? D.Target : null);
            if (echo != null) Back(sim, T, th, echo, "push_echo", null, Emotion.Neutral, Anim.CrossArms);
            Protest(sim, T, P);
            int n = 0;
            foreach (var j in jur.Where(j => j != P && j != pusher && j != D.Target && !Protects(S, j, P) && D.Reading[j] != P).OrderBy(j => DH(S, "turn:" + j)))
            {
                if (D.Reading[j] != "?" && DH(S, "turn2:" + j) >= 0.5) continue;   // some keep their own suspicion
                D.Reading[j] = P; if (!th.Supporters.Contains(j)) th.Supporters.Add(j); n++;
            }
            if (D.Target != null && D.Reading.ContainsKey(D.Target)) D.Reading[D.Target] = P;
            if (n > 0) DNarrate(T, $"좌중의 시선이 {Given(P)}에게 쏠린다.", BeatKind.Cascade, intensity: 0.6f);
            D.Push = P;
        }

        /// <summary>Why someone would name P without having seen anything: bad blood the room knows about.</summary>
        static string RelFragment(Simulation sim, DebateState D, string who, string P)
        {
            var S = sim.S; var r = RelOf(S, P, D.Victim);
            bool polite = Cast.Get(who)?.Speech.PoliteDefault ?? true;
            if (D.Said.TryGetValue(P, out var room) && D.Alone.Contains(P))
                return LineBank.FixParticles(polite ? $"{Given(P)}, {S.RoomName(room)}에 있었다면서요? 본 사람이 아무도 없잖아요." : $"{Given(P)}, {S.RoomName(room)}에 있었다며? 본 사람이 아무도 없잖아.");
            if (r != null && (r.Grudge > 0.2f || r.Like < -0.2f)) return polite ? $"{Given(P)}, {Given(D.Victim)}하고 사이 안 좋았잖아요." : $"{Given(P)}, {Given(D.Victim)}하고 사이 안 좋았잖아.";
            return polite ? "그 시간에 어디 있었는지, 아직 아무도 못 들었잖아요." : "그 시간에 어디 있었는지 아직 아무도 못 들었잖아.";
        }

        /// <summary>Pressure shows on the face, never as a confession: a small tell the first time it crosses a threshold.</summary>
        static void CulpritShaken(Simulation sim, TrialState T, float amount)
        {
            var S = sim.S; var D = T.Debate; var M = D.Mind; if (D.Target == null || !Jurors(S, T).Contains(D.Target)) return;
            M.Pressure++;
            if (M.Pressure == 2 && !D.Seen.Contains("tell:subtle"))
            {
                D.Seen.Add("tell:subtle");
                DNarrate(T, Tell(D.Target, false), BeatKind.Tell, intensity: 0.4f);
            }
        }

        static readonly Dictionary<string, (string subtle, string full)> Tells = new Dictionary<string, (string, string)>
        {
            ["P02"] = ("진우가 사탕을 입에 문다.", "진우가 사탕을 와작 깨문다. 그러고는 되묻는다. \"맞혀 볼까?\""),
            ["P03"] = ("서윤이 펜 끝을 가지런히 한다.", "\"괜찮아요. …괜찮아요.\" 서윤이 펜을 책상 모서리에 맞춰 놓는다."),
            ["P04"] = ("도윤이 찻잔 손잡이를 돌려 놓는다.", "도윤이 찻잔을 정확히 제자리에 내려놓는다. 받침의 무늬까지 맞춰서."),
            ["P05"] = ("이현이 재킷 단추를 만진다.", "\"말씀하신 건….\" 이현이 말을 고르며 재킷 단추를 움켜쥔다."),
            ["P06"] = ("태겸이 회중시계를 힐끗 본다.", "태겸이 회중시계를 꺼내 연다. 닫는다. 다시 연다."),
            ["P07"] = ("시온의 말이 빨라진다.", "시온이 선글라스를 코끝으로 끌어내린다. 목소리가 한 톤 커진다."),
            ["P08"] = ("라온이 한쪽 귀를 만진다.", "라온이 비어 있는 귀, 이어폰이 없는 자리를 손끝으로 더듬는다."),
            ["P09"] = ("재하가 손을 등 뒤로 감춘다.", "재하가 이름을 부르지 않고 말을 시작한다. 목소리가 너무 매끄럽다."),
            ["P10"] = ("준서가 괜히 앞치마를 턴다.", "\"잠깐 나갔을 뿐이야.\" 준서가 주머니에서 먹을 것을 꺼내 내민다."),
            ["P11"] = ("해린이 고글을 만지작거린다.", "\"문제없어. 다 봤어.\" 해린이 고글을 눈 위로 끌어내린다."),
            ["P12"] = ("수아의 웃음이 한층 밝아진다.", "수아가 가장 환하게 웃는다. 눈길만 방 구석을 훑는다."),
            ["P13"] = ("세나가 한 손을 주머니에 넣는다.", "\"괜찮다고.\" 세나가 한 손을 주머니 깊이 밀어 넣는다."),
            ["P14"] = ("은결의 대답이 반 박자 빨라진다.", "은결이 늘 두던 침묵 없이, 곧바로 대답한다."),
            ["P15"] = ("가온이 수첩을 반쯤 덮는다.", "가온이 수첩을 탁 덮는다. 한 번도 닫힌 적 없던 수첩이다."),
            ["P16"] = ("채령이 주머니 속 무언가를 쥔다.", "\"기억 안 나.\" 채령의 주머니 속에서 무언가가 찌그러진다."),
            ["P17"] = ("예담의 말끝이 뚝 끊긴다.", "예담의 노래 같던 말이 멈춘다. \"…판정합니다.\" 심판 목소리가 튀어나온다."),
            ["P18"] = ("민서가 전자시계를 본다.", "민서가 전자시계를 들여다본다. \"힘들지 않습니다.\""),
        };
        static string Tell(string id, bool full) => Tells.TryGetValue(id ?? "", out var t) ? (full ? t.full : t.subtle) : $"{Given(id)}의 눈길이 잠깐 흔들린다.";

        // ================================================================== 3막: 지목 and the duel
        static void DuelStep(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate;
            switch (D.Step)
            {
                case "accuse":
                    DYusti(T, "지목할 분의 이름을 말씀해 주십시오.", "accuse_call", 0.7f);
                    if (PlayerIn(S, T)) { T.PendingPrompt = "accuse"; D.Step = "accuse-wait"; }
                    else { D.Accused = AutoAccuse(sim, false); T.Accused = D.Accused; D.Step = "duel-open"; }
                    return;
                case "accuse-wait":
                    if (T.PendingPrompt != null) return;
                    if (D.Accused == null) { D.Accused = AutoAccuse(sim, false); T.Accused = D.Accused; }   // no name given: the room's leading reading
                    D.Step = "duel-open"; return;
                case "duel-open": DuelOpen(sim, T); return;
                case "counter": DuelCounter(sim, T); return;
                case "counter-floor": DuelFloor(sim, T); return;
                case "counter-wait": if (T.PendingPrompt != null) return; DuelPassed(sim, T); return;
                case "break": DuelBreak(sim, T); return;
                case "defend": InnocentDefends(sim, T); return;
                case "end": D.Act = "act4"; D.Step = "summary"; D.Active = null; return;
            }
        }

        static void DuelOpen(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate; var M = D.Mind;
            if (D.Accused != D.Target || D.Target == null || !Jurors(S, T).Contains(D.Target)) { D.Step = "defend"; return; }
            M.Actor = D.Target;
            var duel = new Mystery { Id = "m" + (D.Mysteries.Count + 1), Axis = Axis.Identity, Trick = "Duel", Act = "act3", State = "lit", Title = $"「{Given(D.Target)}의 반격」", Issue = "기회 · 수단 · 거짓" };
            D.Mysteries.Add(duel); D.Active = duel.Id;
            PlanCounters(sim, T);
            M.Candles = M.Plan.Count + (M.Plan.Contains("final") ? 0 : 1); if (!M.Plan.Contains("final")) M.Plan.Add("final");
            M.Candles = M.Plan.Count; M.CandlesLit = M.Candles;
            DNarrate(T, $"{Given(D.Target)}의 자리에 촛불 {Kor(M.Candles)} 개가 켜진다.", BeatKind.Stage, intensity: 0.7f);
            DSay(sim, T, D.Target, "duel_open", new Dictionary<string, string> { { "victim", "@" + D.Victim } }, BeatKind.Counter, "counter", null, (Cast.Get(D.Target)?.P.Pride ?? 0.5f) >= 0.6f ? Emotion.Smirk : Emotion.Neutral, Anim.CrossArms, 0.7f);
            D.Step = "counter";
        }

        /// <summary>What the accused will throw back, aimed at the empty knots: their alibi, the weapon, the scapegoat, then the
        /// fallbacks (each concedes a little) — answerable ones first, 2 in Chapter 1 and 3 after, then the final demand.</summary>
        static void PlanCounters(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate; var M = D.Mind; M.Plan.Clear();
            var cands = new List<string>();
            foreach (var l in D.Lies.Where(l => !l.Broken))
            {
                if (l.Topic == "weapon" || l.Topic == "item") cands.Add("weapon:" + l.Id);
                else if ((l.Topic == "where" || l.Topic == "time" || l.Topic == "with") && !l.Used) cands.Add("alibi:" + l.Id);
                else if (l.Topic == "saw" && !l.Used) cands.Add("scapegoat:" + l.Id);
            }
            foreach (var f in D.Fallbacks.Where(f => !f.Used && !(f.Concedes ?? "").Contains("침묵"))) cands.Add("fallback:" + f.Order);
            int cap = S.Chapter <= 1 ? 3 : 4;
            foreach (var c in cands.OrderBy(c => CanAnswer(sim, T, c) ? 0 : 1).ThenBy(c => c.StartsWith("weapon") ? 0 : c.StartsWith("alibi") ? 1 : c.StartsWith("scapegoat") ? 2 : 3).ThenBy(c => c, StringComparer.Ordinal))
            { if (M.Plan.Count >= cap) break; if (!M.Plan.Any(p => KnotOf(D, p) == KnotOf(D, c) && !c.StartsWith("fallback")) || cands.Count <= cap) M.Plan.Add(c); }
            if (M.Plan.Count < 2) foreach (var c in cands.Where(c => !M.Plan.Contains(c))) { if (M.Plan.Count >= 2) break; M.Plan.Add(c); }
            M.Plan.Add("final");
        }

        /// <summary>The knot a counter aims at.</summary>
        static string KnotOf(DebateState D, string plan)
        {
            if (plan.StartsWith("weapon")) return "means";
            if (plan.StartsWith("alibi")) return "chance";
            if (plan.StartsWith("scapegoat")) return "deceit";
            if (plan.StartsWith("fallback:"))
            {
                var f = D.Fallbacks.FirstOrDefault(x => "fallback:" + x.Order == plan); string tr = (f?.Trigger ?? "") + (f?.Concedes ?? "");
                return tr.Contains("흉기") ? "means" : tr.Contains("현장") || tr.Contains("근처") ? "chance" : "deceit";
            }
            return "any";
        }

        static bool CanAnswer(Simulation sim, TrialState T, string plan)
        {
            var S = sim.S; var D = T.Debate; var probe = CounterClaim(sim, D, plan, out _, out _);
            var th = new Theory { Holder = D.Target, Claim = probe, Lie = plan.Contains(":") && !plan.StartsWith("fallback") ? plan.Substring(plan.IndexOf(':') + 1) : null, Family = Family.Self };
            return D.Deck.Plates.Any(p => DuelAnswers(S, D, th, p, plan) != null);
        }

        /// <summary>The counter as a checkable claim, and the words the accused says (their pack's own, in their register).</summary>
        static Prop CounterClaim(Simulation sim, DebateState D, string plan, out string text, out string lieId)
        {
            var S = sim.S; text = null; lieId = null;
            if (plan.StartsWith("weapon:") || plan.StartsWith("alibi:") || plan.StartsWith("scapegoat:"))
            {
                string lid = plan.Substring(plan.IndexOf(':') + 1); lieId = lid; var lie = D.Lies.FirstOrDefault(l => l.Id == lid); text = lie?.Text;
                if (plan.StartsWith("weapon:"))
                {
                    var inc = S.Incidents.TryGetValue(D.Incident, out var i) ? i : null; var it = inc?.Weapon != null ? S.I(inc.Weapon) : null;
                    return new Prop { Kind = PropKind.Held, A = D.Target, Item = it?.Type, Value = "denied" };
                }
                if (plan.StartsWith("alibi:")) return new Prop { Kind = PropKind.AtPlace, A = D.Target, Room = D.ClaimRoom, T0 = D.ClaimFrom >= 0 ? D.ClaimFrom : D.KillClock - 30, T1 = D.ClaimTo >= 0 ? D.ClaimTo : D.KillClock + 30, Value = "window-cover" };
                return new Prop { Kind = PropKind.Culprit, A = D.Scapegoat, B = D.Victim, Value = "push" };
            }
            if (plan.StartsWith("fallback:"))
            {
                var f = D.Fallbacks.FirstOrDefault(x => "fallback:" + x.Order == plan); text = f?.Story;
                return new Prop { Kind = PropKind.Culprit, A = D.Target, B = D.Victim, Value = "denied" };
            }
            return new Prop { Kind = PropKind.Culprit, A = D.Target, B = D.Victim, Value = "demand" };
        }

        /// <summary>The plate that answers this counter — a fresh one before one already laid in this duel.</summary>
        internal static Plate DuelAnswer(GameState S, DebateState D, Theory th, string plan = null)
            => D.Deck.Plates.Where(p => DuelAnswers(S, D, th, p, plan) != null).OrderBy(p => D.Mind.Used.Contains(p.Id) ? 1 : 0).ThenBy(p => p.N).FirstOrDefault();

        /// <summary>A plate already laid in this duel while a fresh one also answers: the court wants the other one.</summary>
        internal static bool Stale(GameState S, DebateState D, Theory th, Plate P, string plan = null)
            => D.Mind.Used.Contains(P.Id) && D.Deck.Plates.Any(o => o.Id != P.Id && !D.Mind.Used.Contains(o.Id) && DuelAnswers(S, D, th, o, plan) != null);

        /// <summary>Does plate P answer counter th? Returns the written reason, or null. Only proof answers (a true plate or a
        /// turned one); a plate already laid may answer again only when no fresh one does (<see cref="Stale"/>).</summary>
        internal static string DuelAnswers(GameState S, DebateState D, Theory th, Plate P, string plan = null)
        {
            if (th?.Claim == null || P == null) return null;
            plan = plan ?? D.Mind.Counters.LastOrDefault();
            // any proof may answer (the same photograph can undo a lie and the fallback behind it); a turned fake counts as proof
            bool proof = P.True || P.State == PlateState.Flipped; if (!proof) return null;
            var c = th.Claim; string X = D.Target;
            string Implicates()
            {
                if (P.State == PlateState.Flipped) return P.Users.Contains("story") ? P.Back : null;
                foreach (var p in P.Props.Where(p => p.A == X))
                {
                    if ((p.Kind == PropKind.AtPlace || p.Kind == PropKind.Held) && (p.Room == D.KillRoom || p.Room == D.FoundRoom) && p.T1 >= D.KillClock - 40 && p.T0 <= D.KillClock + 20)
                        return LineBank.FixParticles($"{ClockFmt.Vague(p.T0)}, {Given(X)}은(는) {S.RoomName(p.Room)}에 있었다");
                    if (p.Kind == PropKind.Held && ItemCatalog.Get(p.Item)?.IsWeapon == true) return LineBank.FixParticles($"{Given(X)}의 손에 {ItemCatalog.Get(p.Item)?.Kor}이(가) 있었다");
                    if (p.Kind == PropKind.Bloodied) return LineBank.FixParticles($"{Given(X)}의 옷에 피가 묻어 있었다");
                }
                return null;
            }
            switch (c.Kind)
            {
                case PropKind.Held:
                    {
                        var h = P.True ? P.Props.FirstOrDefault(p => p.Kind == PropKind.Held && p.A == X && (p.Item == c.Item || ItemCatalog.Get(p.Item)?.IsWeapon == true)) : null;
                        if (h != null) return LineBank.FixParticles($"{ClockFmt.Vague(h.T0)}, {Given(X)}의 손에 {ItemCatalog.Get(h.Item)?.Kor ?? "그것"}이(가) 있었다");
                        if (th.Lie != null && LieBrokenBy(D, th.Lie, P)) return LineBank.FixParticles($"{P.Title} — {Given(X)}의 말과 맞지 않는다");
                        return null;
                    }
                case PropKind.AtPlace:
                    {
                        var (r, why) = P.State == PlateState.Flipped ? WeighTurned(S, D, th, c, P) : Debate.Judge(S, c, P, true);
                        if (r == LogicResult.Contradict || (P.State == PlateState.Flipped && r == LogicResult.LimitScope)) return why;
                        if (th.Lie != null && LieBrokenBy(D, th.Lie, P)) return LineBank.FixParticles($"{P.Title} — {Given(X)}의 말과 맞지 않는다");
                        return c.A == X ? Implicates() : null;
                    }
                case PropKind.Culprit:
                    {
                        if (c.Value == "push")
                        {
                            if (c.A == null) return null;
                            if (P.State == PlateState.Flipped && P.Points == c.A) return P.Back;
                            var (r, why) = Debate.Judge(S, c, P, true);
                            return r == LogicResult.Contradict ? why : null;
                        }
                        return Implicates();
                    }
            }
            return null;
        }

        static void DuelCounter(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate; var M = D.Mind; var duel = DActive(D);
            string next = M.Plan.FirstOrDefault(p => !M.Counters.Contains(p));
            if (next == null || M.CandlesLit <= 0) { D.Step = "break"; return; }
            M.Counters.Add(next); M.Passes = 0;
            var claim = CounterClaim(sim, D, next, out var text, out var lieId);
            if (lieId != null) { var lie = D.Lies.FirstOrDefault(l => l.Id == lieId); if (lie != null) lie.Used = true; M.LiesUsed.Add(lieId); }
            if (next.StartsWith("fallback:")) { var f = D.Fallbacks.FirstOrDefault(x => "fallback:" + x.Order == next); if (f != null) f.Used = true; M.Fallback++; }
            bool final = next == "final";
            float lit = M.Candles == 0 ? 0 : M.CandlesLit / (float)M.Candles;
            bool retreat = next.StartsWith("fallback:");
            var emo = final ? Emotion.Angry : retreat ? (lit > 0.5f ? Emotion.Angry : Emotion.Fear)
                    : lit > 0.66f ? ((Cast.Get(D.Target)?.P.Pride ?? 0.5f) >= 0.6f ? Emotion.Smirk : Emotion.Neutral) : lit > 0.34f ? Emotion.Angry : Emotion.Fear;
            Theory th;
            if (final || text == null)
                th = Voice(sim, T, duel, D.Target, Family.Self, Basis.Saw, claim, "duel_demand", new Dictionary<string, string> { { "victim", "@" + D.Victim } }, null, null, false, key: "counter", emo: emo, gesture: final ? Anim.Slam : Anim.CrossArms);
            else
                th = Voice(sim, T, duel, D.Target, Family.Self, Basis.Saw, claim, null, null, null, null, false, lie: lieId, key: "counter", emo: emo, gesture: retreat ? Anim.Shrug : Anim.CrossArms, rawText: next.StartsWith("fallback:") ? RetreatText(sim, T, next, text) : DebateLines.InRegister(D.Target, text));
            M.Counter = th.Id;
            D.Step = "counter-floor";
        }

        /// <summary>A fallback opens by conceding out loud ("…알았어요, 그건 인정해요.") before the story that keeps the rest.</summary>
        static string RetreatText(Simulation sim, TrialState T, string plan, string story)
        {
            bool polite = Cast.Get(T.Debate.Target)?.Speech.PoliteDefault ?? true;
            return (polite ? "…알았어요, 그건 인정할게요. " : "…알았어, 그건 인정할게. ") + DebateLines.InRegister(T.Debate.Target, story);
        }

        static void DuelFloor(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate; var M = D.Mind;
            var th = DTheory(D, M.Counter); if (th == null || th.State != "standing") { D.Step = "counter"; return; }
            D.FloorTheory = th.Id; D.FloorOpen = true; D.FloorKind = "counter";
            T.Claims.First(c => c.Id == th.TrialClaim).Premises = new List<string>();
            if (!PlayerIn(S, T)) { D.Step = "counter-wait"; return; }
            if (!D.Seen.Contains("focus:" + th.Id)) { D.Seen.Add("focus:" + th.Id); Mode(T, TrialMode.TM08_FinalDefense, $"{Given(D.Target)}의 반격 — 남은 촛불 {M.CandlesLit}"); }
            DMark(D, BeatKind.Floor, th.Id);
            T.PendingPrompt = "chain:" + th.TrialClaim;
            D.Step = "counter-wait"; D.FloorAt = T.Beats.Count;
        }

        /// <summary>A plate laid against the accused's counter (player or a resident who takes the floor).</summary>
        internal static Result DuelShow(Simulation sim, TrialState T, Theory th, Plate P, string by = null)
        {
            var S = sim.S; var D = T.Debate; var M = D.Mind; by = by ?? Cast.Player; bool player = by == Cast.Player;
            string plan = M.Counters.LastOrDefault();
            string why = DuelAnswers(S, D, th, P, plan);
            var slots = new Dictionary<string, string> { { "holder", "@" + th.Holder }, { "plate", $"은판 {P.N}, 「{P.Title}」" } };
            if (why != null && Stale(S, D, th, P, plan))
            {
                // the same photograph twice: no penalty, but the accused shrugs it off and the floor stays open
                if (player) DSay(sim, T, Cast.Player, "p_show", slots, BeatKind.Line, "p_show", th.TrialClaim, Emotion.Neutral, Anim.Present, 0.55f, th.Id, P.Id);
                DResult(T, BeatKind.Result, null, "그 은판은 이미 한 번 내밀었다 — 같은 은판으로 두 번 몰 수는 없다. 다른 은판이 있을 것이다.", "Irrelevant", th.TrialClaim, intensity: 0.4f, theory: th.Id, plate: P.Id);
                DSay(sim, T, D.Target, "duel_stale", null, BeatKind.Line, "scoff", th.TrialClaim, Emotion.Smirk, Anim.CrossArms, 0.5f, th.Id);
                if (player) Decided(D, "stale");
                D.Step = "counter-floor";
                return new Result { R = LogicResult.Irrelevant, Text = "그 은판은 이미 한 번 내밀었다 — 다른 은판이 있을 것이다" };
            }
            if (player) DSay(sim, T, Cast.Player, "p_show", slots, BeatKind.Line, why != null ? "p_object" : "p_show", th.TrialClaim, why != null ? Emotion.Angry : Emotion.Neutral, Anim.Present, why != null ? 0.85f : 0.6f, th.Id, P.Id);
            else DSay(sim, T, by, "npc_show", slots, BeatKind.Line, "object", th.TrialClaim, Emotion.Angry, Anim.Present, 0.7f, th.Id, P.Id);
            if (why == null)
            {
                if (player) { D.Misses++; T.Invalid++; T.Influence = MathX.Clamp01(T.Influence - 0.04f); Decided(D, "miss"); }
                th.Misses++;
                bool fake = !P.True && P.State != PlateState.Flipped;
                DResult(T, BeatKind.Result, null, fake ? "지금은 이 은판만으로는 가릴 수 없다." : "맞지 않는다 — " + CounterMissWhy(D, th, P), "Irrelevant", th.TrialClaim, intensity: 0.45f, theory: th.Id, plate: P.Id);
                DSay(sim, T, D.Target, "duel_scoff", null, BeatKind.Line, "scoff", th.TrialClaim, Emotion.Smirk, Anim.CrossArms, 0.55f, th.Id);
                if (th.Misses == 1) DuelAlly(sim, T, th, plan);
                if (th.Misses >= 3) { StandCounter(sim, T, th); D.Step = "counter"; }
                else D.Step = "counter-floor";
                return new Result { R = LogicResult.Irrelevant, Text = CaseBoard.Plain(fake ? "지금은 이 은판만으로는 가릴 수 없다." : CounterMissWhy(D, th, P)) };
            }
            if (player) { D.Hits++; T.Valid++; T.Influence = MathX.Clamp01(T.Influence + 0.1f); Decided(D, "answered"); }
            else if (PlayerIn(S, T)) T.Influence = MathX.Clamp01(T.Influence - 0.05f);
            M.Used.Add(P.Id); if (P.True) P.State = PlateState.Sealed;
            th.State = "collapsed"; var tc = T.Claims.First(c => c.Id == th.TrialClaim); tc.Status = "refuted"; tc.RefutedBy = by; tc.RefuteWhy = CaseBoard.Plain(why);
            if (th.Lie != null) { var lie = D.Lies.FirstOrDefault(l => l.Id == th.Lie); if (lie != null) lie.Broken = true; }
            Publish(T, th, P);
            DResult(T, BeatKind.Break, by, $"{Given(D.Target)}의 말이 무너졌다 — {why}", "Contradict", th.TrialClaim, "break", 0.85f, th.Id, P.Id);
            FillKnot(D, plan, P);
            M.CandlesLit = Math.Max(0, M.CandlesLit - 1); M.Pressure++;
            DNarrate(T, M.CandlesLit > 0 ? $"촛불 하나가 꺼진다. 남은 촛불 {Kor(M.CandlesLit)} 개." : "마지막 촛불이 꺼진다.", BeatKind.Stage, intensity: 0.75f);
            if (plan == "final" || M.CandlesLit <= 0) { D.Step = "break"; return new Result { R = LogicResult.Contradict, Text = CaseBoard.Plain(why), Valid = true, Break = true }; }
            // at a third of the candles, the tell
            if (!M.Told && M.CandlesLit <= Math.Max(1, M.Candles / 3))
            {
                M.Told = true; DNarrate(T, Tell(D.Target, true), BeatKind.Tell, intensity: 0.8f);
                DSay(sim, T, D.Target, "res_culprit_cornered", null, BeatKind.Line, "panic", th.TrialClaim, Emotion.Fear, Anim.Cower, 0.85f, th.Id);
            }
            else DSay(sim, T, D.Target, "res_culprit_shaken", null, BeatKind.Line, "counter", th.TrialClaim, Emotion.Angry, Anim.CrossArms, 0.7f, th.Id);
            if (M.Counters.Count == 1 || M.CandlesLit == 1) Bystander(sim, T, "re_duel_gasp", "duel" + M.CandlesLit, by);
            D.FloorOpen = false; D.Step = "counter";
            return new Result { R = LogicResult.Contradict, Text = CaseBoard.Plain(why), Valid = true, Break = true };
        }

        /// <summary>What the duel proved goes on the public record (the clock-and-floor-plan round and its fit read it): the
        /// answering plate's own facts once, and the accused's broken words as a lie.</summary>
        internal static void Publish(TrialState T, Theory th, Plate P)
        {
            if (P != null && P.True && !T.PublicRoots.Contains("plate:" + P.Id)) { T.PublicRoots.Add("plate:" + P.Id); T.Public.AddRange(P.Props.Select(p => p.Clone())); }
            if (th?.Lie != null && th.Holder != null && !T.Public.Any(p => p.Kind == PropKind.Lie && p.A == th.Holder && p.Value == th.TrialClaim))
                T.Public.Add(new Prop { Kind = PropKind.Lie, A = th.Holder, Value = th.TrialClaim });
        }

        static string CounterMissWhy(DebateState D, Theory th, Plate P)
        {
            var c = th.Claim;
            if (c.Kind == PropKind.Held) return "이 은판은 흉기를 누가 쥐었는지 말하지 않는다";
            if (c.Kind == PropKind.AtPlace) return "이 은판은 그 시각 그 사람이 어디 있었는지 말하지 않는다";
            if (D.Mind.Used.Contains(P.Id)) return "그 은판은 이미 내밀었다 — 같은 은판으로 두 번 몰 수는 없다";
            return "이 은판은 그 사람을 그 자리에 세우지 못한다";
        }

        /// <summary>The knot the counter aimed at, plus whatever the answering plate itself proves (at the scene → 기회, a weapon in
        /// hand → 수단, a turned plate their story leaned on → 거짓).</summary>
        static void FillKnot(DebateState D, string plan, Plate P = null)
        {
            var M = D.Mind; string k = plan == null ? "any" : KnotOf(D, plan);
            if (k == "any") k = !M.Chance ? "chance" : !M.Means ? "means" : "deceit";
            if (k == "chance") M.Chance = true; else if (k == "means") M.Means = true; else M.Deceit = true;
            if (P == null) return;
            if (P.State == PlateState.Flipped && P.Users.Contains("story")) M.Deceit = true;
            if (!P.True) return;
            foreach (var p in P.Props.Where(p => p.A == D.Target))
            {
                if ((p.Kind == PropKind.AtPlace || p.Kind == PropKind.Held) && (p.Room == D.KillRoom || p.Room == D.FoundRoom) && p.T1 >= D.KillClock - 40 && p.T0 <= D.KillClock + 20) M.Chance = true;
                if (p.Kind == PropKind.Held && ItemCatalog.Get(p.Item)?.IsWeapon == true) M.Means = true;
            }
        }

        /// <summary>Someone in the room who holds the answer to this counter says so (without laying it for 민혁).</summary>
        static bool DuelAlly(Simulation sim, TrialState T, Theory th, string plan)
        {
            var S = sim.S; var D = T.Debate; var jur = Jurors(S, T);
            var answer = DuelAnswer(S, D, th, plan);
            if (answer == null) return false;
            string holder = answer.FoundBy != null && jur.Contains(answer.FoundBy) && answer.FoundBy != D.Target ? answer.FoundBy : answer.Witness != null && jur.Contains(answer.Witness) && answer.Witness != D.Target ? answer.Witness : null;
            if (holder == null) { if (PlayerIn(S, T)) DNarrate(T, $"(은판 {answer.N}, 「{answer.Title}」…?)", BeatKind.Inner); return true; }
            DSay(sim, T, holder, "re_request_plate", new Dictionary<string, string> { { "plate", $"은판 {answer.N}, 「{answer.Title}」" } }, BeatKind.Interrupt, "request", th.TrialClaim, Emotion.Fear, Anim.Talk, 0.6f, th.Id, answer.Id);
            return true;
        }

        /// <summary>A counter nobody answered: the candle stays lit, the accused's words stand.</summary>
        static void StandCounter(Simulation sim, TrialState T, Theory th)
        {
            var D = T.Debate; th.State = "standing-final";
            DNarrate(T, "촛불은 꺼지지 않는다. 그 말은 그대로 남는다.", BeatKind.Result, intensity: 0.5f);
            D.FloorOpen = false;
        }

        /// <summary>캐묻기 on a counter: a lie costs them; over budget, they fall back and concede something — out of their own mouth.</summary>
        static string DuelAsk(Simulation sim, TrialState T, Theory th)
        {
            var S = sim.S; var D = T.Debate; var M = D.Mind;
            DSay(sim, T, Cast.Player, "p_ask", new Dictionary<string, string> { { "holder", "@" + th.Holder } }, BeatKind.Line, "p_ask", th.TrialClaim, Emotion.Neutral, Anim.Talk, 0.55f, th.Id);
            th.Asked++;
            var lie = th.Lie != null ? D.Lies.FirstOrDefault(l => l.Id == th.Lie) : null;
            if (lie != null && th.Asked <= 1)
            {
                M.Spent += lie.Cost; M.Pressure++;
                if (M.Spent > Math.Max(1, M.Budget))
                {
                    // out of lies: a concession fills the knot this counter aimed at, and a candle goes
                    var f = D.Fallbacks.FirstOrDefault(x => !x.Used && !(x.Concedes ?? "").Contains("침묵"));
                    if (f != null)
                    {
                        f.Used = true; M.Fallback++;
                        DText(sim, T, D.Target, RetreatText(sim, T, "fallback:" + f.Order, f.Story), BeatKind.Counter, "counter", th.TrialClaim, Emotion.Fear, Anim.Shrug, 0.8f, th.Id);
                        th.State = "collapsed"; var tc = T.Claims.First(c => c.Id == th.TrialClaim); tc.Status = "retracted";
                        DResult(T, BeatKind.Break, Cast.Player, $"{Given(D.Target)}이(가) 스스로 물러선다 — {f.Concedes}", "Contradict", th.TrialClaim, "break", 0.8f, th.Id);
                        FillKnot(D, M.Counters.LastOrDefault()); M.CandlesLit = Math.Max(0, M.CandlesLit - 1);
                        DNarrate(T, M.CandlesLit > 0 ? $"촛불 하나가 꺼진다. 남은 촛불 {Kor(M.CandlesLit)} 개." : "마지막 촛불이 꺼진다.", BeatKind.Stage, intensity: 0.75f);
                        D.Hits++; T.Valid++; T.Influence = MathX.Clamp01(T.Influence + 0.06f);
                        D.FloorOpen = false; D.Step = M.CandlesLit <= 0 ? "break" : "counter";
                        return "conceded";
                    }
                }
                string key = th.Claim?.Kind == PropKind.Held ? "duel_ask_weapon" : th.Claim?.Kind == PropKind.AtPlace && th.Claim.Room >= 0 ? (D.ClaimWith.Count > 0 ? "ask_culprit_detail" : "duel_ask_where") : th.Claim?.Value == "push" ? "duel_ask_push" : "duel_ask_final";
                DSay(sim, T, D.Target, key, new Dictionary<string, string> { { "place", th.Claim?.Room >= 0 ? S.RoomName(th.Claim.Room) : "" }, { "with", D.ClaimWith.FirstOrDefault() != null ? "@" + D.ClaimWith.First() : "" }, { "target", "@" + (th.Claim?.A ?? D.Scapegoat) }, { "victim", "@" + D.Victim } }, BeatKind.Counter, "counter", th.TrialClaim, Emotion.Angry, Anim.CrossArms, 0.65f, th.Id);
                CulpritShaken(sim, T, 0.3f);
                D.Step = "counter-floor";
                return "pressed";
            }
            DSay(sim, T, D.Target, "ask_again", null, BeatKind.Line, "again", th.TrialClaim, Emotion.Angry, Anim.CrossArms, 0.5f, th.Id);
            D.Step = "counter-floor";
            return "again";
        }

        /// <summary>The floor let pass on a counter: the accused presses; then someone who holds the answer speaks up; then they
        /// lay it themselves; if nobody can, the counter stands.</summary>
        static void DuelPassed(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate; var M = D.Mind;
            var th = DTheory(D, M.Counter); if (th == null || th.State != "standing") { D.Step = "counter"; return; }
            Decide(D, ActionKind.Listen, th.Id, false); Decided(D, "pass");
            M.Passes++; D.Passes++;
            string plan = M.Counters.LastOrDefault();
            var answer = DuelAnswer(S, D, th, plan);
            var jur = Jurors(S, T);
            string holder = answer == null ? null : answer.FoundBy != null && jur.Contains(answer.FoundBy) && answer.FoundBy != D.Target ? answer.FoundBy : answer.Witness != null && jur.Contains(answer.Witness) ? answer.Witness : null;
            if (M.Passes == 1)
            {
                DSay(sim, T, D.Target, "duel_press", null, BeatKind.Counter, "counter", th.TrialClaim, Emotion.Smirk, Anim.CrossArms, 0.6f, th.Id);
                if (holder != null && answer != null)
                {
                    DSay(sim, T, holder, "re_request_plate", new Dictionary<string, string> { { "plate", $"은판 {answer.N}, 「{answer.Title}」" } }, BeatKind.Interrupt, "request", th.TrialClaim, Emotion.Fear, Anim.Talk, 0.6f, th.Id, answer.Id);
                }
                else if (answer != null && PlayerIn(S, T)) DNarrate(T, $"(은판 {answer.N}, 「{answer.Title}」…?)", BeatKind.Inner);
                D.Step = "counter-floor"; return;
            }
            if (answer != null && holder != null) { DSay(sim, T, holder, "npc_takes_floor", null, BeatKind.Interrupt, "object", th.TrialClaim, Emotion.Angry, Anim.Slam, 0.7f, th.Id); DuelShow(sim, T, th, answer, holder); return; }
            StandCounter(sim, T, th); D.Step = "counter";
        }

        /// <summary>The quiet break: the line stops mid-sentence, the last candle gutters, a partial admission.</summary>
        static void DuelBreak(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate; var M = D.Mind;
            int knots = (M.Chance ? 1 : 0) + (M.Means ? 1 : 0) + (M.Deceit ? 1 : 0);
            var final = D.Theories.LastOrDefault(t => t.Holder == D.Target && t.Claim != null && t.Claim.Value == "demand");
            bool answered = final != null && final.State == "collapsed";
            if (answered || knots >= 3)
            {
                DSay(sim, T, D.Target, "duel_cut", null, BeatKind.Break, "panic", null, Emotion.Fear, Anim.Cower, 0.95f);
                DNarrate(T, "말이 거기서 끊긴다. 법정의 촛불이 하나씩 잦아들고, 그 자리의 촛불만 남아 흔들린다.", BeatKind.Break, intensity: 1f);
                if (!M.Told) { M.Told = true; DNarrate(T, Tell(D.Target, true), BeatKind.Tell, intensity: 0.9f); }
                DSay(sim, T, D.Target, "break_quiet", null, BeatKind.Break, "reveal", null, Emotion.Break, Anim.Cower, 1f);
                M.Broken = true;
                // the room: the one they tried to blame, then the one who loved the victim most
                var jur = Jurors(S, T);
                string wronged = D.Deck.Plates.Where(p => p.Role == PlateRole.Frame && p.Points != null && jur.Contains(p.Points)).Select(p => p.Points).FirstOrDefault() ?? (D.Push != null && jur.Contains(D.Push) ? D.Push : null);
                if (wronged != null && wronged != D.Target) DSay(sim, T, wronged, "re_betrayed", new Dictionary<string, string> { { "target", "@" + D.Target } }, BeatKind.Line, "object", null, Emotion.Angry, Anim.Point, 0.8f);
                string mourner = jur.Where(x => x != D.Target && x != wronged && S.HasRel(x, D.Victim)).OrderByDescending(x => S.R(x, D.Victim).Attach).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault();
                if (mourner != null && S.R(mourner, D.Victim).Attach > 0.2f) DSay(sim, T, mourner, "re_grief", new Dictionary<string, string> { { "victim", "@" + D.Victim } }, BeatKind.Line, "claim", null, Emotion.Crying, Anim.Cry, 0.7f);
            }
            else
            {
                DSay(sim, T, D.Target, "duel_hold", null, BeatKind.Counter, "counter", null, Emotion.Smirk, Anim.CrossArms, 0.7f);
                DNarrate(T, $"{Given(D.Target)}의 촛불은 아직 꺼지지 않았다.", BeatKind.Result, intensity: 0.6f);
            }
            D.FloorOpen = false; D.Step = "end";
        }

        /// <summary>A wrong 지목: the accused answers with the truth, someone stands by them, the culprit quietly agrees with the
        /// room — and Yusti allows one more name.</summary>
        static void InnocentDefends(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate; var X = D.Accused; var jur = Jurors(S, T);
            if (X != null && jur.Contains(X))
            {
                var clear = D.Deck.Plates.FirstOrDefault(p => p.Role == PlateRole.Clear && p.Alibi.Contains(X));
                var sealedAlibi = D.Theories.FirstOrDefault(t => t.State == "sealed" && t.Claim != null && t.Claim.Kind == PropKind.AtPlace && t.Claim.A == X);
                var ap = sealedAlibi?.Claim ?? clear?.Props.FirstOrDefault(p => p.Kind == PropKind.AtPlace && p.A == X);
                string frag = ap != null ? LineBank.FixParticles($"{ClockFmt.Vague(ap.T0)}엔 {S.RoomName(ap.Room)}에 있었어요.") : "그 시간에 전 현장 근처에도 안 갔어요.";
                DSay(sim, T, X, "innocent_defend", new Dictionary<string, string> { { "fragment", frag } }, BeatKind.Line, "defend", null, Emotion.Fear, Anim.Cower, 0.7f);
                var friend = jur.Where(x => x != X && x != D.Target && (Protects(S, x, X) || (clear != null && clear.Witness == x))).OrderBy(x => DH(S, "friend:" + x)).FirstOrDefault();
                if (friend != null) DSay(sim, T, friend, "re_defend_other", new Dictionary<string, string> { { "target", "@" + X } }, BeatKind.Interrupt, "object", null, Emotion.Angry, Anim.Point, 0.6f);
                if (D.Target != null && jur.Contains(D.Target) && D.Target != X) DSay(sim, T, D.Target, "push_echo", null, BeatKind.Line, "claim", null, Emotion.Smirk, Anim.CrossArms, 0.4f);
                foreach (var j in jur) if (D.Reading[j] == X && (Protects(S, j, X) || ap != null)) D.Reading[j] = "?";
            }
            if (D.AccuseTries == 0 && PlayerIn(S, T))
            {
                D.AccuseTries++;
                DYusti(T, "지목을 다시 하시겠습니까? 한 번에 한하여 허락하겠습니다.", "accuse_again", 0.6f);
                D.Accused = null; T.PlayerAccused = null; T.Accused = null;
                D.Step = "accuse";
                return;
            }
            D.Step = "end";
        }
    }

    public static partial class Debate
    {
        /// <summary>The culprit's plan, frozen when the court opens (CaseApi computes the pack on demand from a world the court moves).</summary>
        internal static void SnapshotPack(CaseFacts C, DebateState D)
        {
            var pk = C.Pack; if (pk == null) return;
            var st = pk.Story;
            if (st != null)
            {
                D.ClaimRoom = st.ClaimRoom; D.ClaimFrom = st.ClaimFrom; D.ClaimTo = st.ClaimTo;
                D.ClaimWith = (st.ClaimWith ?? new List<string>()).Where(x => x != null && x != C.Culprit).ToList();
                D.StoryText = st.ClaimText; D.StoryTheory = st.Theory;
            }
            D.Budget = pk.LieBudget; D.Mind.Budget = pk.LieBudget; D.Mind.Scapegoat = D.Scapegoat;
            foreach (var l in pk.Lies) D.Lies.Add(new DuelLie { Id = l.Id, Topic = l.Topic, Text = l.Text, BrokenBy = (l.BrokenBy ?? new List<string>()).ToList(), Cost = l.Cost });
            foreach (var f in pk.Fallbacks.OrderBy(f => f.Order)) D.Fallbacks.Add(new DuelFallback { Order = f.Order, Trigger = f.Trigger, Story = f.Story, Concedes = f.Concedes, Keeps = f.Keeps });
        }
    }
}
