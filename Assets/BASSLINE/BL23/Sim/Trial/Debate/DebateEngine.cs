using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// The 심판 as a debate (TrialReforge §6), v0 director. The court argues over the 은판 deck (DeckBuild.cs): riddles light one
    /// at a time, residents put up theories in their own words, the floor turns to 민혁 (a chain: prompt over the standing
    /// theories), and every plate laid, question asked or floor let pass moves the room. Act shape: 서막 → 1막 (first
    /// impression, R1) → 2막 (the reversal, 그렇다면…, R2) → 3막 (지목 and the duel) → 4막 (summary, vote, verdict).
    ///
    /// The Game plays it through the existing trial screen: every DebateBeat is mirrored as a TrialBeat, every theory is a
    /// TrialClaim, plates are the cards (Arsenal / BulletEvidence), and the old player calls are intercepted while
    /// TrialState.Debate is set. Deterministic: no RNG stream of its own (MurderHash for choices), the vote draw only.
    /// </summary>
    public static partial class TrialSystem
    {
        // ================================================================== start
        /// <summary>A debate needs a murder with a living culprit at a stand, and a deck that holds together.</summary>
        static bool DebateEligible(Simulation sim, TrialState T, out Incident inc)
        {
            var S = sim.S; inc = TargetIncident(S);
            if (!DebateEnabled || T.NoMurder || inc == null || !inc.Murder || inc.Culprit == null) return false;
            if (!T.Participants.Contains(inc.Culprit) || S.A(inc.Culprit)?.Alive != true) return false;
            return T.Participants.Count(x => x != Cast.Player && S.A(x)?.Alive == true) >= 3;
        }

        static void DebateBegin(Simulation sim, TrialState T, Incident inc)
        {
            var S = sim.S;
            var C = Debate.Facts(sim, inc);
            var deck = Debate.BuildDeck(C);
            var D = new DebateState { Incident = inc.Id, Victim = inc.Victim, Target = inc.Culprit, Deck = deck };
            D.KillRoom = C.KillRoom; D.FoundRoom = C.FoundRoom; D.KillClock = C.KillClock; D.FoundClock = C.FoundClock;
            D.Trick = C.Trick; D.Scapegoat = C.Scapegoat != null && C.Scapegoat != C.Culprit && T.Participants.Contains(C.Scapegoat) && S.A(C.Scapegoat)?.Alive == true ? C.Scapegoat : null;
            Debate.SnapshotPack(C, D);
            T.Debate = D;
            // the riddles: what it was done with (unless the weapon itself is the trick), the first impressions in the deck's order
            // (1막 takes the first, 2막 the rest), then "who" (lit after R1)
            int n = 0;
            var bodyPlate = deck.Plates.FirstOrDefault(p => p.Kind == PlateKind.Body);
            if (C.Trick != "Swap" && bodyPlate != null && bodyPlate.Props.Any(p => p.Kind == PropKind.WeaponType))
            {
                var wm = new Mystery { Id = "m" + (++n), Axis = Axis.Means, Trick = "Wound", Act = "act1", Title = LineBank.FixParticles($"「{Cast.GivenOf(D.Victim)}의 상처」"), Issue = "무엇으로 당했나" };
                D.Mysteries.Add(wm); D.Queue.Add(wm.Id);
            }
            int firstL1 = n + 1;
            foreach (var dm in deck.Mysteries)
            {
                var k = deck.Claims.FirstOrDefault(c => c.Id == dm.Claim);
                if (k == null || k.Truth == "true") continue;   // a true first reading is not a riddle: it opens the 심판 as a plaque
                var m = new Mystery { Id = "m" + (++n), Axis = dm.Kind, Claim = dm.Claim, Trick = k.Trick ?? (k.Axis == Axis.Place ? "Place" : k.Axis.ToString()) };
                m.Act = n == firstL1 ? "act1" : "act2";
                int cut = (dm.Text ?? "").IndexOf(" — ", StringComparison.Ordinal);
                m.Title = LineBank.FixParticles(cut > 0 ? dm.Text.Substring(0, cut) : dm.Text);
                m.Issue = LineBank.FixParticles(cut > 0 ? dm.Text.Substring(cut + 3) : IssueText(D, m));
                D.Mysteries.Add(m); D.Queue.Add(m.Id);
            }
            var who = new Mystery { Id = "m" + (++n), Axis = Axis.Identity, Trick = "Who", Act = "act2", Key = true };
            who.Title = LineBank.FixParticles($"「{Cast.GivenOf(D.Victim)}을(를) 친 손」"); who.Issue = "그 시각, 현장에 있을 수 있던 사람은 누구인가";
            D.Mysteries.Add(who); D.Queue.Add(who.Id);
            if (D.Mysteries.Count == 1) who.Act = "act1";
            // the room's first readings (private suspicion seeds only the start) and the name plates
            foreach (var id in T.Participants.Where(x => x != Cast.Player && S.A(x)?.Alive == true))
            {
                var k = S.K(id); var top = k.Suspicion.Where(kv => T.Participants.Contains(kv.Key) && kv.Key != id).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).FirstOrDefault();
                string r = top.Key != null && top.Value > 0.45f ? top.Key : "?";
                if (id == D.Target) r = D.Scapegoat ?? "?";
                D.Reading[id] = r; D.Initial[id] = r;
            }
            D.Standing = T.Participants.Where(x => x != Cast.Player && S.A(x)?.Alive == true).OrderBy(x => x, StringComparer.Ordinal).ToList();
            // the House's record of the body (read at the 서막, like every record: exact, and silent about who)
            var vict = S.A(D.Victim); var main = vict?.Body.Wounds.Where(w => !w.Postmortem).OrderByDescending(w => w.Sev).ThenBy(w => w.Tick).FirstOrDefault();
            D.Record = main != null ? WoundText.Region(main.Region) + "에 " + (Violence.Phrase(main) ?? WoundText.Type(main.Type)) : null;
            D.Act = "prologue"; D.Step = "open";
            S.Log("DebateBegin", null, data: $"inc={inc.Id} plates={deck.Plates.Count} fakes={deck.FakeCount} riddles={D.Mysteries.Count} trick={D.Trick ?? "-"} lies={D.Lies.Count}");
        }

        static string IssueText(DebateState D, Mystery m)
        {
            switch (m.Trick)
            {
                case "Message": return "정말 " + Cast.GivenOf(D.Victim) + "이(가) 남긴 말인가";
                case "Place": return "어디에서 공격당했나";
                case "Tod": return "정말 그 시각에 숨졌나";
                case "Seal": return "누가, 어떻게 드나들었나";
                case "Swap": return "정말 그것으로 쳤나";
                case "Accident": case "Natural": case "Suicide": return "정말 아무도 손대지 않았나";
            }
            return m.Title;
        }

        // ================================================================== beats
        /// <summary>One beat, twice: the debate's own record and the trial screen's line.</summary>
        internal static DebateBeat DBeat(TrialState T, BeatKind kind, string speaker, string text, string tkind, string key = null, string claimId = null, string data = null,
            Emotion emo = Emotion.Neutral, Anim gesture = Anim.Talk, bool readable = true, float intensity = 0.3f, string theory = null, string plate = null)
        {
            var D = T.Debate; text = LineBank.FixParticles(text ?? "");
            var b = new DebateBeat { Seq = ++D.Seq, Kind = kind, Readable = readable, Speaker = speaker, Text = text, LineKey = key, Mystery = D.Active, Theory = theory, Plate = plate, Face = emo.ToString(), Gesture = gesture.ToString(), Intensity = intensity };
            D.Beats.Add(b);
            T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = tkind, Speaker = speaker, Text = CaseBoard.Plain(text), Key = key, ClaimId = claimId, Mode = T.Mode, Data = data, Emotion = emo, Gesture = gesture });
            if (readable) D.ReadableSinceDecision++;
            return b;
        }

        /// <summary>A resident's line from the debate bank (register, no repeats, the speaker's own names for people).</summary>
        internal static DebateBeat DSay(Simulation sim, TrialState T, string speaker, string lineKey, Dictionary<string, string> slots, BeatKind kind = BeatKind.Line, string key = null, string claimId = null,
            Emotion emo = Emotion.Neutral, Anim gesture = Anim.Talk, float intensity = 0.35f, string theory = null, string plate = null)
        {
            var D = T.Debate;
            var filled = new Dictionary<string, string>();
            if (slots != null) foreach (var kv in slots) filled[kv.Key] = kv.Value != null && kv.Value.StartsWith("@") ? sim.CallName(speaker, kv.Value.Substring(1)) : kv.Value;
            string text = DebateLines.Say(sim.S, speaker, lineKey, filled, D.SpokenKeys, D.Seq.ToString()) ?? "…";
            sim.S.Log("TrialLine", speaker, null, data: lineKey + "|" + text);
            return DBeat(T, kind, speaker, text, "line", key ?? lineKey, claimId, null, emo, gesture, true, intensity, theory, plate);
        }

        /// <summary>A plain sentence in someone's mouth (already in their register).</summary>
        internal static DebateBeat DText(Simulation sim, TrialState T, string speaker, string text, BeatKind kind = BeatKind.Line, string key = null, string claimId = null,
            Emotion emo = Emotion.Neutral, Anim gesture = Anim.Talk, float intensity = 0.35f, string theory = null, string plate = null)
        {
            sim.S.Log("TrialLine", speaker, null, data: (key ?? "text") + "|" + text);
            return DBeat(T, kind, speaker, text, "line", key, claimId, null, emo, gesture, true, intensity, theory, plate);
        }

        /// <summary>The room's narration (no speaker): what bodies do, the plates, 민혁's inner voice.</summary>
        internal static DebateBeat DNarrate(TrialState T, string text, BeatKind kind = BeatKind.Line, string data = null, float intensity = 0.3f, string plate = null, bool readable = true)
            => DBeat(T, kind, null, text, "line", null, null, data, Emotion.Neutral, Anim.None, readable, intensity, null, plate);

        /// <summary>Yusti on the judge's seat: procedure, rules, counts — 하십시오체, never an opinion.</summary>
        internal static DebateBeat DYusti(TrialState T, string text, string key = null, float intensity = 0.3f)
            => DBeat(T, BeatKind.System, Cast.Butler, text, "system", key, null, null, Emotion.Neutral, Anim.Talk, true, intensity);

        /// <summary>A result plate on the screen (collapse, flip, plaque, refusal, miss).</summary>
        internal static DebateBeat DResult(TrialState T, BeatKind kind, string speaker, string text, string data, string claimId = null, string tkind = "result", float intensity = 0.5f, string theory = null, string plate = null)
            => DBeat(T, kind, speaker, text, tkind, null, claimId, data, Emotion.Neutral, Anim.None, true, intensity, theory, plate);

        /// <summary>A mark in the debate's own record only (floors, stingers) — nothing on the trial screen.</summary>
        internal static void DMark(DebateState D, BeatKind kind, string theory = null, string text = null)
            => D.Beats.Add(new DebateBeat { Seq = ++D.Seq, Kind = kind, Readable = false, Theory = theory, Text = text, Mystery = D.Active });

        static void DMode(TrialState T, TrialMode m, string label)
        {
            T.Mode = m; if (!T.ModesSeen.Contains(m.ToString())) T.ModesSeen.Add(m.ToString());
            T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "mode", Mode = m, Text = label, Data = m.ToString() });
            var D = T.Debate; D.Beats.Add(new DebateBeat { Seq = ++D.Seq, Kind = BeatKind.Bell, Readable = false, Text = label, Stinger = "act" });
        }

        static void DTopic(TrialState T, string label, string data)
        {
            label = LineBank.FixParticles(label);
            T.Topic = data; if (!T.TopicsDone.Contains(data)) T.TopicsDone.Add(data);
            T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "topic", Text = label, Data = data, Mode = T.Mode });
            var D = T.Debate; D.Beats.Add(new DebateBeat { Seq = ++D.Seq, Kind = BeatKind.Slide, Readable = false, Text = label, Mystery = D.Active, Stinger = "riddle" });
        }

        internal static string Kor(int n)
        {
            string[] w = { "영", "한", "두", "세", "네", "다섯", "여섯", "일곱", "여덟", "아홉", "열", "열한", "열두", "열세", "열네", "열다섯", "열여섯", "열일곱", "열여덟" };
            return n >= 0 && n < w.Length ? w[n] : n.ToString();
        }
        static string Ordinal(int n) { string[] w = { "첫", "두", "세", "네", "다섯" }; return n >= 1 && n <= w.Length ? w[n - 1] + " 번째" : n + "번째"; }

        internal static Mystery DActive(DebateState D) => D.Mysteries.FirstOrDefault(m => m.Id == D.Active);
        internal static Theory DTheory(DebateState D, string id) => D.Theories.FirstOrDefault(t => t.Id == id);
        internal static Plate DPlate(DebateState D, string id) => D.Deck.Plates.FirstOrDefault(p => p.Id == id);
        internal static List<string> Jurors(GameState S, TrialState T) => T.Participants.Where(x => x != Cast.Player && S.A(x)?.Alive == true).OrderBy(x => x, StringComparer.Ordinal).ToList();
        internal static double DH(GameState S, string key) => MurderHash.U01(S, "debate:" + key);
        internal static Rel RelOf(GameState S, string a, string b) => a != null && b != null && S.HasRel(a, b) ? S.R(a, b) : null;
        internal static bool Protects(GameState S, string a, string b) { var r = RelOf(S, a, b); return r != null && (r.Attach > 0.55f || r.Tags.Contains("family") || r.Tags.Contains("lover")); }
        internal static bool Resents(GameState S, string a, string b) { var r = RelOf(S, a, b); return r != null && (r.Grudge > 0.25f || r.Fear > 0.3f || r.Tags.Contains("grudge") || r.Tags.Contains("enemy")); }

        // ================================================================== the director
        static void DebateDirect(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate;
            if (T.Stage == "Done" || D.Act == "done") { T.Finished = true; return; }
            switch (D.Act)
            {
                case "prologue": Prologue(sim, T); return;
                case "act1": case "act2": RiddleStep(sim, T); return;
                case "act3": DuelStep(sim, T); return;
                case "act4": VerdictStep(sim, T); return;
            }
        }

        // ---------------------------------------------------------------- 서막
        static void Prologue(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate; var deck = D.Deck;
            DMode(T, TrialMode.TM01_Debate, "서막 · 개정");
            DYusti(T, "알려 드립니다. 심판을 시작하겠습니다. 정해진 대로, 지목은 한 번입니다.", "open");
            DYusti(T, $"틀리면, 한 분이 제비로 대신 가십니다. {Cast.NameOf(Cast.Player)} 님도 그 제비 안에 계십니다.", "rule");
            if (S.RuleActive("CH19")) Yusti(sim, T, "y_trial_rule", "rule", new Dictionary<string, string> { { "name", "시간차 공지" }, { "desc", "같은 공지가 두 번, 서로 다른 시각에 전해졌습니다. 언제 들으셨는지도 함께 말씀해 주십시오." } });
            if (S.RuleActive("CH21")) Yusti(sim, T, "y_trial_rule", "rule", new Dictionary<string, string> { { "name", "세 번째 빈자리" }, { "desc", "사건마다 책임은 따로 묻겠지만, 이번 심판에서 가려낼 범인은 한 명입니다." } });
            DYusti(T, $"저택의 기록을 다시 읽겠습니다. {Cast.NameOf(D.Victim)} 님, {S.RoomName(D.FoundRoom)}에서 {ClockFmt.Vague(D.FoundClock)}에 발견되셨습니다." + (D.Record != null ? $" {D.Record}." : "") + " 그 밖의 것은 여러분께서 가려 주십시오.", "record");
            DYusti(T, $"은판은 {Kor(deck.Plates.Count)} 장입니다. 그중 {Kor(deck.FakeCount)} 장은 보이는 대로가 아닙니다.", "count");
            if (S.Settlements.Count == 0) DNarrate(T, $"(사진은 다 진짜다. {Kor(deck.FakeCount)} 장은 처음 떠오르는 뜻이 틀렸을 뿐.)", BeatKind.Inner);
            // the hand-over: plates the player did not find themselves
            var borrowed = deck.Plates.Where(p => p.State == PlateState.Borrowed && p.FoundBy != null).Select(p => p.FoundBy).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            int late = deck.Plates.Count(p => p.State == PlateState.Late);
            if (borrowed.Count > 0 || late > 0)
                DNarrate(T, (borrowed.Count > 0 ? $"빌린 은판 {deck.Plates.Count(p => p.State == PlateState.Borrowed)} — {string.Join(", ", borrowed.Select(Cast.GivenOf))}" : "") + (borrowed.Count > 0 && late > 0 ? " · " : "") + (late > 0 ? $"그날 저택이 찍어 둔 은판 {late}" : ""), BeatKind.Develop, readable: false);
            // a first reading nobody disputes opens the 심판 as a plaque (the time everyone heard, …)
            foreach (var k in deck.Claims.Where(c => c.Layer == 1 && c.Truth == "true"))
            {
                var basis = deck.Plates.FirstOrDefault(p => p.True && p.Role == PlateRole.Confirm);
                if (basis == null) continue;
                AddPlaque(sim, T, null, k.Text + $" — {Cast.GivenOf(basis.Witness)}이(가) 그 소리를 들었다.", k.Presented, "house");
            }
            D.Act = D.Mysteries.Any(m => m.Act == "act1") ? "act1" : "act2"; D.Step = "rollcall";
            DMode(T, TrialMode.TM01_Debate, "1막 「첫인상」");
        }

        internal static void AddPlaque(Simulation sim, TrialState T, Mystery m, string text, Prop prop, string by)
        {
            var D = T.Debate;
            var p = new Plaque { Id = "q" + (D.Plaques.Count + 1), Mystery = m?.Id, Text = LineBank.FixParticles(text), By = by, Prop = prop?.Clone() };
            D.Plaques.Add(p); if (m != null) m.Plaque = p.Id;
            if (prop != null) T.Public.Add(prop.Clone());
            DResult(T, BeatKind.Plaque, null, "정해진 것 — " + p.Text, "Named", intensity: 0.45f);
        }

        // ---------------------------------------------------------------- 1막 · 2막: one riddle at a time
        static void RiddleStep(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate;
            switch (D.Step)
            {
                case "rollcall": RollCall(sim, T); D.Step = "lit"; return;
                case "lit":
                    {
                        var m = D.Mysteries.FirstOrDefault(x => x.Act == D.Act && x.State == "dim");
                        if (m == null) { D.Step = D.Act == "act1" ? "r1" : "r2"; return; }
                        D.Active = m.Id; m.State = "lit";
                        int no = D.Mysteries.IndexOf(m) + 1;
                        DTopic(T, $"수수께끼 {no} · {m.Title} — {m.Issue}", "riddle:" + m.Id);
                        D.Step = "theories";
                        return;
                    }
                case "theories":
                    {
                        var m = DActive(D);
                        RiddleTheories(sim, T, m);
                        if (m.State == "settled") { D.Step = "settle"; return; }
                        if (!m.Theories.Select(id => DTheory(D, id)).Any(t => t != null && t.State == "standing")) { SettleByRoom(sim, T, m); return; }
                        D.Step = "floor";
                        return;
                    }
                case "floor": OpenFloor(sim, T); return;
                case "await":
                    {
                        // the prompt was cleared and nothing was done: the player let the floor pass
                        if (T.PendingPrompt != null) return;
                        FloorPassed(sim, T);
                        return;
                    }
                case "settle":
                    {
                        var m = DActive(D);
                        D.Step = "lit";
                        AfterSettle(sim, T, m);   // may send the flow to 그렇다면…
                        D.Active = null;
                        return;
                    }
                case "infer": StartInfer(sim, T); return;
                case "infer-wait": if (T.PendingPrompt == null && (T.Game == null || T.Game.Status != "open")) { D.Step = "r2"; } return;
                case "r1": RoundOne(sim, T); return;
                case "r2": RoundTwo(sim, T); return;
            }
        }

        /// <summary>The floor: the debate stops and turns to 민혁 — every standing theory of the riddle, the focused one last.</summary>
        static void OpenFloor(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate; var m = DActive(D);
            var standing = m.Theories.Select(id => DTheory(D, id)).Where(t => t != null && t.State == "standing").ToList();
            if (standing.Count == 0) { SettleByRoom(sim, T, m); return; }
            var focus = FocusOf(D, standing);
            D.FloorTheory = focus.Id; D.FloorOpen = true; D.FloorKind = "theory"; m.Floors++;
            var fc = T.Claims.First(c => c.Id == focus.TrialClaim);
            fc.Premises = standing.Where(t => t != focus).Select(t => t.TrialClaim).Take(3).ToList();
            D.Chips = fc.Premises.ToList();
            // no player at a stand (a spectator run): the floor passes by itself
            if (!PlayerIn(S, T)) { D.Step = "await"; D.FloorAt = T.Beats.Count; return; }
            if (m.Floors == 1 || D.Chips.Count == 0 || T.Mode != TrialMode.TM04_Chain || !D.Seen.Contains("focus:" + focus.Id)) Mode(T, TrialMode.TM04_Chain, (m.Floors == 1 ? "발언권 — " : "다시, ") + Cast.GivenOf(focus.Holder) + "의 가설");
            if (!D.Seen.Contains("focus:" + focus.Id)) D.Seen.Add("focus:" + focus.Id);
            DMark(D, BeatKind.Floor, focus.Id);
            T.PendingPrompt = "chain:" + focus.TrialClaim;
            D.Step = "await"; D.FloorAt = T.Beats.Count;
        }

        /// <summary>The slide whose collapse changes the most: most supporters, then a lie, then the newest.</summary>
        static Theory FocusOf(DebateState D, List<Theory> standing)
            => standing.OrderByDescending(t => t.Supporters.Count + (t.Target != null ? 2 : 0) + (t.True ? 0 : 1)).ThenByDescending(t => D.Theories.IndexOf(t)).First();

        // ---------------------------------------------------------------- R1 · R2
        static void RoundOne(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate;
            CulpritPush(sim, T);
            Bell(sim, T);
            D.Act = "act2"; D.Step = "lit";
            DMode(T, TrialMode.TM02_Crossfire, "2막 「뒤집힌 이야기」");
        }

        static void RoundTwo(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate;
            // the room turns: whoever the settled facts leave standing at the scene
            var who = D.Mysteries.FirstOrDefault(m => m.Trick == "Who");
            if (who != null && who.State == "settled" && D.Target != null)
            {
                var moved = new List<string>();
                foreach (var j in Jurors(S, T))
                {
                    if (j == D.Target || Protects(S, j, D.Target)) continue;
                    if (D.Reading[j] != D.Target) { D.Reading[j] = D.Target; moved.Add(j); }
                }
                if (moved.Count > 0) DNarrate(T, $"좌중의 눈이 하나둘 {Cast.GivenOf(D.Target)}에게로 옮겨 간다.", BeatKind.Cascade, intensity: 0.7f);
            }
            Bell(sim, T);
            D.Act = "act3"; D.Step = "accuse";
            DMode(T, TrialMode.TM08_FinalDefense, "3막 「반격」");
        }

        internal static void Bell(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate; D.Bells++;
            DYusti(T, $"{Ordinal(D.Bells)} 종입니다. 지금 어느 분을 향하고 계신지, 손을 들어 주십시오.", D.Bells >= 2 ? "vote_call" : "bell", 0.55f);
            Headcount(sim, T);
        }

        internal static void Headcount(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate;
            var tally = Jurors(S, T).GroupBy(j => D.Reading.TryGetValue(j, out var r) ? r : "?").Select(g => (who: g.Key, n: g.Count()))
                .OrderBy(x => x.who == "?" ? 1 : 0).ThenByDescending(x => x.n).ThenBy(x => x.who, StringComparer.Ordinal).ToList();
            var named = tally.Where(x => x.who != "?").ToList(); int unsure = tally.Where(x => x.who == "?").Sum(x => x.n);
            var parts = named.Take(3).Select(x => Cast.GivenOf(x.who) + " " + x.n).ToList();
            int rest = named.Skip(3).Sum(x => x.n); if (rest > 0) parts.Add("그 밖 " + rest); if (unsure > 0) parts.Add("모름 " + unsure);
            string text = "좌중 — " + string.Join(" · ", parts);
            DResult(T, BeatKind.Headcount, null, text, "Headcount", intensity: 0.5f);
        }

        /// <summary>Everyone whose reading rested on a fallen theory (or on the person it cleared) drops to undecided — one beat.</summary>
        internal static void Cascade(Simulation sim, TrialState T, Theory fallen, string cleared)
        {
            var S = sim.S; var D = T.Debate; int n = 0;
            foreach (var j in Jurors(S, T))
            {
                if (j == D.Target && D.Reading[j] != "?") continue;   // the culprit never lets go of a scapegoat on their own
                bool rested = fallen != null && fallen.Supporters.Contains(j) || fallen != null && fallen.Holder == j && fallen.Target != null;
                if ((rested || (cleared != null && D.Reading[j] == cleared)) && D.Reading[j] != "?") { D.Reading[j] = "?"; n++; }
            }
            if (fallen != null) fallen.Supporters.Clear();
            if (n >= 2) DNarrate(T, $"좌중이 술렁인다 — {Kor(n)} 사람이 고개를 돌린다.", BeatKind.Cascade, intensity: 0.6f);
        }

        // ================================================================== 4막
        static void VerdictStep(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate;
            switch (D.Step)
            {
                case "summary":
                    {
                        // 「그날 밤의 재구성」: the clock and the floor plan first (the existing round), then 민혁's closing argument
                        DMode(T, TrialMode.TM07_Reconstruct, "4막 「판결」");
                        BuildReconstruction(sim, T);
                        if (PlayerIn(S, T) && TrialGames.StartReconstruct(sim, T)) { D.Step = "closing"; return; }
                        D.Step = "closing";
                        return;
                    }
                case "closing":
                    {
                        if (T.PendingPrompt != null || (T.Game != null && T.Game.Status == "open" && T.Game.Kind == "reconstruct")) return;
                        ClosingArgument(sim, T);
                        // a fake the room's version still leans on
                        var stand = D.Deck.Plates.Where(p => !p.True && p.State != PlateState.Flipped && p.Points != null && p.Points == D.Accused).ToList();
                        if (stand.Count > 0) DYusti(T, "아직 확인되지 않은 은판이 있습니다. 이대로 투표하시겠습니까?", "warn");
                        Headcount(sim, T);
                        D.Step = "vote";
                        return;
                    }
                case "vote":
                    {
                        DYusti(T, "투표를 받겠습니다. 돌은 한 분에 하나입니다.", "vote_call", 0.6f);
                        Mode(T, TrialMode.Vote, "투표");
                        T.Stage = "Vote";
                        if (PlayerIn(S, T) && !T.Votes.ContainsKey(Cast.Player)) T.PendingPrompt = "vote";
                        D.Step = "tally";
                        return;
                    }
                case "tally":
                    {
                        if (T.PendingPrompt == "vote") return;
                        if (PlayerIn(S, T) && !T.Votes.ContainsKey(Cast.Player)) { T.PendingPrompt = "vote"; return; }
                        Tally(sim, T);
                        return;
                    }
            }
        }

        // ---------------------------------------------------------------- the vote (public facts only) and the verdict
        static void Tally(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate;
            foreach (var j in Jurors(S, T)) if (!T.Votes.ContainsKey(j)) T.Votes[j] = DebateVoteOf(sim, T, j);
            var tally = T.Votes.GroupBy(v => v.Value).Select(g => (who: g.Key, n: g.Count())).OrderByDescending(x => x.n).ThenBy(x => x.who, StringComparer.Ordinal).ToList();
            T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "vote", Text = string.Join(", ", tally.Select(x => Cast.GivenOf(x.who) + " " + x.n + "표")), Data = string.Join(";", T.Votes.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + ">" + kv.Value)) });
            D.Beats.Add(new DebateBeat { Seq = ++D.Seq, Kind = BeatKind.Vote, Readable = false, Text = string.Join(", ", tally.Select(x => x.who + ":" + x.n)) });
            bool tie = tally.Count > 1 && tally[0].n == tally[1].n;
            if (tie && T.VoteRound == 0) { T.VoteRound = 1; T.Votes.Clear(); Yusti(sim, T, "y_vote_tie", "vote_tie"); D.Step = "vote"; return; }
            string accused;
            if (tie) { var tops = tally.Where(x => x.n == tally[0].n).Select(x => x.who).OrderBy(x => x, StringComparer.Ordinal).ToList(); accused = tops[S.R(Stream.VoteDraw).R(tops.Count)]; Yusti(sim, T, "y_vote_draw", "vote_draw", new Dictionary<string, string> { { "t", Cast.NameOf(accused) } }); }
            else accused = tally[0].who;
            T.Accused = accused;
            if (S.RuleActive("CH01")) Yusti(sim, T, "y_vote_open", "vote_open", new Dictionary<string, string> { { "list", string.Join(", ", T.Votes.Select(kv => Cast.GivenOf(kv.Key) + " → " + Cast.GivenOf(kv.Value))) } });
            // at most five stances out loud: the loudest readings first
            foreach (var kv in T.Votes.Where(kv => kv.Key != Cast.Player).OrderBy(kv => DH(S, "stance:" + kv.Key)).Take(4))
                DSay(sim, T, kv.Key, kv.Value == D.Target && D.Target == accused ? "vote_sure" : "vote_line", new Dictionary<string, string> { { "target", "@" + kv.Value } }, BeatKind.Vote, key: "vote_line", emo: Emotion.Neutral);
            Yusti(sim, T, "y_vote_result", "vote_result", new Dictionary<string, string> { { "t", Cast.NameOf(accused) } });
            D.Verdict = accused; D.Correct = accused == D.Target; D.Act = "done";
            T.ReconstructMax = D.Mysteries.Count; T.ReconstructScore = D.Mysteries.Count(m => m.SettledBy != null && m.SettledBy.StartsWith("player"));
            T.Stage = "Done"; T.Finished = true;
            S.Log("DebateVerdict", null, accused, data: $"correct={D.Correct} hits={D.Hits} misses={D.Misses} passes={D.Passes} asks={D.Asks} decisions={D.Decisions.Count}");
            Settlements.Resolve(sim, T);
        }

        /// <summary>
        /// A juror's stone (§6.8): their public reading, weighed by what was proven in court — knots held on the accused,
        /// fakes shown that point at someone and were never turned, plaques clearing someone — plus a bounded lean toward
        /// 민혁's 지목 by trust. Family and lovers never vote their person; nobody votes for themselves; the culprit votes
        /// the likeliest other name.
        /// </summary>
        static string DebateVoteOf(Simulation sim, TrialState T, string j)
        {
            var S = sim.S; var D = T.Debate;
            var cands = T.Participants.Where(x => x != j && S.A(x)?.Alive == true).ToList();
            var score = new Dictionary<string, double>();
            var M = D.Mind; int knots = (M.Chance ? 1 : 0) + (M.Means ? 1 : 0) + (M.Deceit ? 1 : 0);
            foreach (var x in cands)
            {
                double u = 0;
                if (x == D.Accused && D.Accused == M.Actor) u += 3 * knots;
                u += D.Deck.Plates.Count(p => !p.True && p.State == PlateState.Shown && p.Points == x);
                u -= 3 * D.Plaques.Count(q => q.Prop != null && q.Prop.Kind == PropKind.AtPlace && q.Prop.Value == "window-cover" && q.Prop.A == x);
                if (x == D.Accused) { var r = RelOf(S, j, Cast.Player); u += Math.Min(1.5, 0.5 + (r != null ? (r.Trust + r.Respect) : 0) + T.Influence * 0.5); }
                if (D.Reading.TryGetValue(j, out var rd) && rd == x) u += 1.2;
                if (D.Initial.TryGetValue(j, out var ini) && ini == x) u += 0.2;
                if (Protects(S, j, x)) u -= 9;
                score[x] = u;
            }
            if (j == D.Target) return score.Where(kv => kv.Key != j).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;
            return score.OrderByDescending(kv => kv.Value).ThenBy(kv => DH(S, "vote:" + j + ":" + kv.Key)).First().Key;
        }
    }
}
