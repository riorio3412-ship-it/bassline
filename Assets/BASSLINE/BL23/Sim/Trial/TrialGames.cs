using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>One testimony line inside a round. WeakAt/WeakLen mark the phrase ("구절") a seal can land on — the part a
    /// contradiction would attack. Kind: "claim" (an ordinary assertion), "defense" (someone asking to be believed), null (plain talk).</summary>
    [Serializable]
    public sealed class GameLine
    {
        public string ClaimId; public string Speaker; public string Text; public int WeakAt = -1; public int WeakLen; public string Kind;
        public Emotion Emo; public string Result; public bool Pressed;
    }

    [Serializable]
    public sealed class GameSlot { public string Id; public string Label; public string Hint; public string Chosen; public string Result; public string Why; public List<string> Pieces = new List<string>(); }

    /// <summary>
    /// A class-trial round built by the kernel from the live TrialState:
    ///   inquiry     — 촛불 심문: a ledger of the topic's testimonies; wax seals of evidence cards are stamped onto phrases.
    ///   ledger      — 증언 대조: two people's testimonies side by side; pick the pair of lines that cannot both be true.
    ///   board       — 붉은 실: an accusation and the premises holding it up (Why = accused/challenge/defense), or the final
    ///                 argument against the accused (Why = final: opportunity / means / deceit anchors).
    ///   question    — 새겨진 물음: the court itself poses what the evidence just established; answer from engraved plates.
    ///   reconstruct — 시계와 평면도: set the clock and place figures on the floor plan; answers the reconstruction questions.
    /// Every outcome is resolved through the trial logic (Logic.Check via PlayerContradict / PlayerSupport) or from facts the
    /// player's own evidence establishes — never by a pre-matched answer id.
    /// </summary>
    [Serializable]
    public sealed class TrialGame
    {
        public string Id; public string Kind; public string Topic; public string Title; public string Subtitle; public string Opponent; public string Second; public string Question; public string Why;
        public List<GameLine> Lines = new List<GameLine>(); public List<GameLine> Lines2 = new List<GameLine>(); public GameLine Final;
        public List<string> Bullets = new List<string>();
        public string Word; public List<string> Pool = new List<string>(); public Prop Fact;
        public List<GameSlot> Slots = new List<GameSlot>(); public List<string> Narration = new List<string>();
        public float TimeLimit = 120; public int Misses, Shots, BeatStart; public string Status = "open"; public string Outcome; public string WinClaim; public string WinBullet;
    }

    public static class TrialGames
    {
        public sealed class Bullet { public string Id, Title, Desc, Group; public EvKind Kind; public string Line; public bool Key, InCase; }
        public sealed class ShotResult { public bool Valid, Break, Ended, Lost, Hourglass; public LogicResult R = LogicResult.Irrelevant; public string Text, Retort, RetortBy, Seal, SteerBy, Steer; }
        /// <summary>Kernel text on its way to the screen: no rule codes, "단서" not "자료".</summary>
        static ShotResult Plain(ShotResult r) { if (r == null) return r; r.Text = CaseBoard.Plain(r.Text); r.Retort = CaseBoard.Plain(r.Retort); r.Steer = CaseBoard.Plain(r.Steer); return r; }

        /// <summary>Spend one of the court's hourglasses (earned by a streak): the UI relights the candles.</summary>
        public static bool UseHourglass(Simulation sim) { var T = sim.S.Trial; if (T == null || T.Hourglass <= 0) return false; T.Hourglass--; return true; }

        public static string TopicLabel(string topic)
        {
            switch (topic)
            {
                case "alibi": return "각자의 알리바이"; case "cause": return "사인과 흉기"; case "time": return "숨진 시각"; case "place": return "숨진 곳";
                case "suspicious": return "수상한 행동"; case "culprit": return "범인은 누구인가";
            }
            return topic;
        }

        // ================================================================== evidence cards
        /// <summary>The player's evidence cards of this chapter (bodies, traces, objects, records, testimonies heard in investigation).</summary>
        public static List<Bullet> Arsenal(Simulation sim)
        {
            // the notebook's order (사건 단서 first, 중요 first), its short titles and one-line headlines
            var list = new List<Bullet>();
            foreach (var v in CaseBoard.Cards(sim).Where(v => v.Ev.Props.Count > 0))
                list.Add(new Bullet { Id = v.Id, Title = v.Title, Desc = v.Line, Line = v.Line, Group = v.KindLabel, Kind = v.Ev.Kind, Key = v.Key, InCase = v.InCase });
            return list;
        }

        static IEnumerable<Evidence> PlayerCards(GameState S) => S.K(Cast.Player).Evidence.Where(e => e.Loop == S.Loop && e.Chapter == S.Chapter && e.Props.Count > 0 && !e.Hidden);

        /// <summary>Resolve a card id; "claim:cN" is a statement made in court (hearsay for the player: Direct=false).</summary>
        public static Evidence BulletEvidence(GameState S, string id)
        {
            if (id == null) return null;
            if (id.StartsWith("claim:"))
            {
                var c = S.Trial?.Claims.FirstOrDefault(x => x.Id == id.Substring(6)); if (c?.Prop == null) return null;
                return new Evidence { Id = id, Owner = Cast.Player, Kind = EvKind.Testimony, Title = Cast.GivenOf(c.Speaker) + "의 심판 발언", Desc = c.Text, Source = Cast.NameOf(c.Speaker), Direct = false, Root = "trial:" + c.Id, T0 = c.Prop.T0, T1 = c.Prop.T1, Room = c.Prop.Room, Loop = S.Loop, Chapter = S.Chapter, Props = { c.Prop.Clone() } };
            }
            var ev = S.K(Cast.Player).Evidence.FirstOrDefault(e => e.Id == id);
            return ev != null && ev.Hidden ? null : ev;   // folded / superseded cards are never presented
        }

        static int Rank(LogicResult r) => r == LogicResult.Contradict ? 5 : r == LogicResult.Conditional ? 4 : r == LogicResult.LimitScope ? 3 : r == LogicResult.Support ? 2 : r == LogicResult.NeedPremise ? 1 : 0;
        static bool Valid(string action, LogicResult r) => action == "agree" ? r == LogicResult.Support : (r == LogicResult.Contradict || r == LogicResult.Conditional || r == LogicResult.LimitScope);

        static LogicResult Eval(GameState S, TrialClaim c, Evidence ev, string action)
        {
            if (c?.Prop == null || ev == null || !Usable(S.Trial, c, ev.Id, action)) return LogicResult.Irrelevant;
            var best = LogicResult.Irrelevant;
            foreach (var p in ev.Props) { var r = Logic.Check(S, c.Prop, p, ev.Direct, ev.Root).Result; if (action == "agree" ? r == LogicResult.Support : Rank(r) > Rank(best)) best = r; }
            return best;
        }

        /// <summary>Independence: a statement can't back itself up, and a speaker can't be corroborated by their own words.</summary>
        static bool Usable(TrialState T, TrialClaim c, string id, string action)
        {
            if (id == null || id == "claim:" + c.Id) return false;
            if (action == "agree" && id.StartsWith("claim:")) { var o = T?.Claims.FirstOrDefault(x => x.Id == id.Substring(6)); if (o != null && o.Speaker == c.Speaker) return false; }
            return true;
        }

        static bool Breakable(GameState S, TrialClaim c, List<Evidence> cards) => cards.Any(ev => Valid("contra", Eval(S, c, ev, "contra")));
        static bool Backable(GameState S, TrialClaim c, List<Evidence> cards) => cards.Any(ev => Valid("agree", Eval(S, c, ev, "agree")));

        /// <summary>Automation only (headless test player / AutoProbe demo): would this card work on that statement? Never drives the game.</summary>
        public static bool ProbeWorks(GameState S, string claimId, bool support, string cardId)
        {
            var c = S.Trial?.Claims.FirstOrDefault(x => x.Id == claimId); var a = support ? "agree" : "contra"; return c != null && Valid(a, Eval(S, c, BulletEvidence(S, cardId), a));
        }
        public static bool ProbeSlot(Simulation sim, string slot, string cardId) { var T = sim.S.Trial; return T?.Game != null && SlotFits(sim, T, slot, T.Game.Opponent, BulletEvidence(sim.S, cardId), out _); }
        public static bool ProbeLedger(GameState S, int left, int right)
        {
            var G = S.Trial?.Game; if (G == null || left < 0 || right < 0 || left >= G.Lines.Count || right >= G.Lines2.Count) return false;
            return Conflict(S, S.Trial.Claims.FirstOrDefault(c => c.Id == G.Lines[left].ClaimId), S.Trial.Claims.FirstOrDefault(c => c.Id == G.Lines2[right].ClaimId)) != null;
        }

        static string LineKind(TrialClaim c) => c.Key == "defend_self" || c.Key == "defend_other" || c.Key == "final_defense" || c.Topic == "defense" || (c.Prop != null && c.Prop.Value == "window-cover") ? "defense" : "claim";

        // ================================================================== round lifecycle
        static TrialGame NewGame(TrialState T, string kind) => new TrialGame { Id = "g" + (T.GameLog.Count(x => x.StartsWith("#")) + 1), Kind = kind };

        static void Begin(TrialState T, TrialGame G)
        {
            T.Game = G; G.BeatStart = T.Beats.Count; T.GameLog.Add("#" + G.Kind);
            T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "game", Text = G.Title + (G.Subtitle != null ? " — " + G.Subtitle : ""), Data = G.Kind, Mode = T.Mode });
            T.PendingPrompt = "game:" + G.Kind;
        }

        static void End(TrialState T, TrialGame G, string status)
        {
            G.Status = status; T.GameLog.Add(G.Kind + ":" + (G.Topic ?? G.Opponent ?? "-") + ":" + status);
            T.GameBeats += Math.Max(0, T.Beats.Count - G.BeatStart);
            if (T.PendingPrompt != null && T.PendingPrompt.StartsWith("game:")) T.PendingPrompt = null;
        }

        static bool CheckLost(Simulation sim, TrialState T, TrialGame G)
        {
            if (T.Influence > 0.001f || G.Status != "open") return false;
            var S = sim.S;
            TrialSystem.Say(T, "result", null, "배심원들의 신뢰를 잃었다 — 이제 아무도 귀 기울이지 않는다", data: "Lost");
            foreach (var id in T.Participants.Where(x => x != Cast.Player)) Relations.Change(S, id, Cast.Player, respect: -0.04f);
            T.Influence = 0.15f;
            if (G.Kind == "board" && G.Why != "final") LoseBoard(sim, T, G);
            End(T, G, "lost");
            return true;
        }

        /// <summary>Called by the topic director after each topic: open the round the discussion calls for, if any.</summary>
        public static bool AfterTopic(Simulation sim, TrialState T, string topic)
        {
            var S = sim.S;
            if (!TrialSystem.PlayerIn(S, T)) return false;
            if (T.PendingPrompt != null && T.PendingPrompt.StartsWith("game:")) return true;
            if (topic == "culprit")
            {
                // someone points at the player: answer the chain of reasoning on the thread board
                var acc = T.Claims.LastOrDefault(c => c.Accused == Cast.Player && c.Speaker != Cast.Player && c.Status != "refuted" && !T.GameLog.Contains("board:" + c.Id));
                if (acc != null && T.Boards < 2 && StartBoard(sim, T, acc, "accused")) return true;
            }
            if (T.Inquiries < 4 && !T.GameLog.Contains("inquiry:" + topic) && StartInquiry(sim, T, topic)) return true;
            if (T.Ledgers < 2 && (topic == "alibi" || topic == "suspicious" || topic == "time") && !T.GameLog.Contains("ledger:" + topic) && StartLedger(sim, T, topic)) return true;
            if (topic == "cause") return StartCauseQuestion(sim, T);
            return false;
        }

        public static void AutoResolve(Simulation sim, bool smart)
        {
            var S = sim.S; var T = S.Trial; if (T == null) return;
            var p = T.PendingPrompt; var G = T.Game;
            if (p == "accuse") { T.PendingPrompt = null; if (smart) TrialSystem.PlayerAccuse(sim, TrialSystem.AutoVote(sim, true)); return; }
            if (G == null || G.Status != "open") { if (p != null && p.StartsWith("game:")) T.PendingPrompt = null; return; }
            var cards = Arsenal(sim).Select(b => b.Id).ToList();
            switch (G.Kind)
            {
                case "inquiry":
                    if (smart)
                        for (int i = 0; i < G.Lines.Count && G.Status == "open"; i++)
                        {
                            var L = G.Lines[i]; if (L.ClaimId == null) continue;
                            var b = cards.FirstOrDefault(id => ProbeWorks(S, L.ClaimId, false, id));
                            if (b != null) Seal(sim, i, b, false);
                            else if (L.Kind == "defense") { var s = cards.FirstOrDefault(id => ProbeWorks(S, L.ClaimId, true, id)); if (s != null) Seal(sim, i, s, true); }
                        }
                    if (G.Status == "open") Timeout(sim);
                    break;
                case "ledger":
                    if (smart) for (int i = 0; i < G.Lines.Count && G.Status == "open"; i++) for (int j = 0; j < G.Lines2.Count && G.Status == "open"; j++) if (ProbeLedger(S, i, j)) LedgerPick(sim, i, j);
                    if (G.Status == "open") Timeout(sim);
                    break;
                case "board":
                    if (G.Why == "final")
                    {
                        var pick = new List<string>();
                        if (smart) { var used = new HashSet<string>(); foreach (var slot in G.Slots) { var b = cards.FirstOrDefault(id => !used.Contains(id) && SlotFits(sim, T, slot.Id, G.Opponent, BulletEvidence(S, id), out _)); pick.Add(b); if (b != null) used.Add(b); } }
                        FinalBoard(sim, pick);
                    }
                    else
                    {
                        if (smart)
                            for (int i = -1; i < G.Lines.Count && G.Status == "open"; i++)
                            {
                                var L = i < 0 ? G.Final : G.Lines[i]; if (L?.ClaimId == null) continue;
                                var b = cards.FirstOrDefault(id => ProbeWorks(S, L.ClaimId, false, id)); if (b != null) BoardTie(sim, i, b);
                            }
                        if (G.Status == "open") BoardYield(sim);
                    }
                    break;
                case "question":
                    if (smart) QuestionPick(sim, G.Word);
                    if (G.Status == "open") Timeout(sim);
                    break;
                case "reconstruct":
                    if (smart) { var ans = new Dictionary<string, int>(); foreach (var q in T.RQ) ans[q.Id] = q.Answer >= 0 ? q.Answer : 0; ReconstructSubmit(sim, ans); }
                    else End(T, G, "skipped");
                    break;
                default: End(T, G, "skipped"); break;
            }
            if (T.PendingPrompt == p && p != null && p.StartsWith("game:") && (T.Game == null || T.Game.Status != "open")) T.PendingPrompt = null;
            if (T.Game == G && G.Status == "open") End(T, G, "skipped");
        }

        /// <summary>A lapse inside a round (a cracked seal, a snapped thread): trust cost, capped per hit. True if the round was lost.</summary>
        public static bool Damage(Simulation sim, float amount)
        {
            var T = sim.S.Trial; var G = T?.Game; if (G == null || G.Status != "open") return false;
            T.Influence = MathX.Clamp01(T.Influence - Math.Min(0.05f, Math.Max(0f, amount)));
            return CheckLost(sim, T, G);
        }

        /// <summary>The candles burned out (or the player let the round pass).</summary>
        public static void Timeout(Simulation sim)
        {
            var T = sim.S.Trial; var G = T?.Game; if (G == null || G.Status != "open") return;
            bool quiet = G.Kind == "inquiry" && G.Why == "quiet"; if (!quiet) T.Influence = MathX.Clamp01(T.Influence - 0.04f);
            string msg = quiet ? "심문을 마쳤다 — 무너뜨릴 만한 말은 끝내 나오지 않았다" : G.Kind == "inquiry" ? "촛불이 모두 꺼졌다 — 누구의 증언도 무너지지 않았다" : G.Kind == "question" ? "끝내 답하지 못했다" : G.Kind == "ledger" ? "두 증언에서 어긋난 곳을 짚어 내지 못했다" : "시간이 다 됐다";
            TrialSystem.Say(T, "result", null, msg, data: "Timeout");
            End(T, G, "timeout");
        }

        // ================================================================== 촛불 심문 (candle inquiry)
        static IEnumerable<TrialClaim> Pool(GameState S, TrialState T, string topic)
        {
            var topics = topic == "culprit" ? new[] { "culprit", "premise", "defense" } : new[] { topic };
            return T.Claims.Where(c => topics.Contains(c.Topic) && c.Speaker != Cast.Player && c.Prop != null && c.Text != null && S.A(c.Speaker)?.Alive == true
                && (c.Status == "open" || c.Status == "supported" || c.Status == "conditional"));
        }

        public static bool StartInquiry(Simulation sim, TrialState T, string topic)
        {
            var S = sim.S; var rng = S.R(Stream.Trial);
            var pool = Pool(S, T, topic).ToList(); if (pool.Count == 0) return false;
            var cards = PlayerCards(S).ToList();
            var scored = pool.Select(c => { var k = LineKind(c); return (c, k, ok: Breakable(S, c, cards) || (k == "defense" && Backable(S, c, cards))); }).ToList();
            bool live = scored.Any(x => x.ok);
            // nothing here the player's cards could break: still a round worth pressing witnesses in, but only a couple of those per trial
            if (!live && (pool.Count < 2 || T.GameLog.Count(x => x == "quiet") >= 2)) return false;
            var pick = scored.Where(x => x.ok).OrderByDescending(x => x.c.Beat).Take(3).ToList();
            foreach (var x in scored.Where(x => !x.ok).OrderByDescending(x => x.c.Beat)) { if (pick.Count >= 5) break; pick.Add(x); }
            rng.Shuffle(pick);
            var G = NewGame(T, "inquiry"); G.Topic = topic; G.Title = "촛불 심문"; G.Subtitle = TopicLabel(topic); G.Why = live ? "live" : "quiet"; if (!live) T.GameLog.Add("quiet");
            var seen = new HashSet<string>();
            foreach (var x in pick)
            {
                var L = MakeLine(sim, T, x.c, x.k); if (!seen.Add(L.Text)) continue;
                G.Lines.Add(L); if (!T.GameLog.Contains("shown:" + x.c.Id)) T.GameLog.Add("shown:" + x.c.Id);
            }
            if (G.Lines.Count < 3) AddChatter(T, G, 3 - G.Lines.Count, rng);
            G.Bullets = Suggest(sim, G.Lines.Select(l => T.Claims.FirstOrDefault(c => c.Id == l.ClaimId)).Where(c => c != null).ToList(), 6);
            G.TimeLimit = 80 + 14 * G.Lines.Count;
            T.Inquiries++; T.GameLog.Add("inquiry:" + topic);
            Begin(T, G);
            return true;
        }

        static void AddChatter(TrialState T, TrialGame G, int n, Rng rng)
        {
            int from = T.Beats.FindLastIndex(b => b.Kind == "topic"); if (from < 0) from = 0;
            var lines = T.Beats.Skip(from).Where(b => b.Kind == "line" && b.ClaimId == null && b.Speaker != null && b.Speaker != Cast.Player && b.Speaker != Cast.Butler && !string.IsNullOrEmpty(b.Text)).ToList();
            rng.Shuffle(lines);
            foreach (var b in lines.Take(n)) { var pg = LineBank.Pages(b.Text); G.Lines.Insert(rng.R(G.Lines.Count + 1), new GameLine { Speaker = b.Speaker, Text = pg.Length > 0 ? pg[pg.Length - 1] : b.Text, Emo = b.Emotion }); }
        }

        public static GameLine MakeLine(Simulation sim, TrialState T, TrialClaim c, string kind)
        {
            var emo = T.Beats.FirstOrDefault(b => b.ClaimId == c.Id && b.Kind == "line")?.Emotion ?? Emotion.Neutral;
            var L = new GameLine { ClaimId = c.Id, Speaker = c.Speaker, Kind = kind ?? LineKind(c), Emo = emo };
            Span(sim, c, L);
            return L;
        }

        static void Span(Simulation sim, TrialClaim c, GameLine L)
        {
            var pages = LineBank.Pages(c.Text); if (pages.Length == 0) pages = new[] { c.Text ?? "…" };
            var cands = WeakCandidates(sim, c);
            if (TryFind(pages, cands, L)) return;
            var re = Restate(sim, c);
            if (re != null && TryFind(LineBank.Pages(re), cands, L)) return;
            var t = pages.OrderByDescending(x => x.Length).First(); L.Text = t;
            int cut = t.LastIndexOf(". ", StringComparison.Ordinal); int start = cut > 0 && cut < t.Length - 4 ? cut + 2 : 0;
            L.WeakAt = start; L.WeakLen = Math.Max(1, t.TrimEnd('.', '!', '?', '…', ' ', '”').Length - start);
        }

        static bool TryFind(string[] pages, List<string> cands, GameLine L)
        {
            foreach (var cand in cands) foreach (var pg in pages) { int i = pg.IndexOf(cand, StringComparison.Ordinal); if (i >= 0) { L.Text = pg; L.WeakAt = i; L.WeakLen = cand.Length; return true; } }
            return false;
        }

        /// <summary>The phrase a contradiction would attack, per proposition kind (where / when / who / with what / how).</summary>
        static List<string> WeakCandidates(Simulation sim, TrialClaim c)
        {
            var S = sim.S; var p = c.Prop; var L = new List<string>(); if (p == null) return L;
            void Add(string s) { if (!string.IsNullOrEmpty(s) && !L.Contains(s)) L.Add(s); }
            string Room(int r) => r >= 0 ? S.RoomName(r) : null;
            string Name(string id) => id != null ? Cast.GivenOf(id) : null;
            string Hm(double t) => t > 0 ? ClockFmt.Vague(t) : null; string Hx(double t) => t > 0 ? ClockFmt.HM(t) : null;
            switch (p.Kind)
            {
                case PropKind.AtPlace: Add(Room(p.Room)); { var r = S.Layout.Room(p.Room); if (r != null && r.Type == RoomType.Bedroom && r.Owner == c.Speaker) Add("내 방"); } Add(Hm(p.T0)); Add(Hm(p.T1)); break;
                case PropKind.WithPerson: Add(Name(p.B)); Add(Room(p.Room)); Add(Hm(p.T0)); break;
                case PropKind.AliveAt: Add(Hm(p.T0)); Add(Hm(p.T1)); Add(Room(p.Room)); break;
                case PropKind.Held: Add(ItemCatalog.Get(p.Item)?.Kor); Add(Room(p.Room)); break;
                case PropKind.WeaponType: if (p.Item != null) Add(ItemCatalog.Get(p.Item)?.Kor); Add(TrialSystem.WeaponWord(TrialSystem.P(p.Value))); break;
                case PropKind.DeathPlace: Add(Room(p.Room)); break;
                case PropKind.DoorState: Add("잠긴 문"); Add("잠겨"); Add(Room(p.Room)); break;
                case PropKind.DoorLocked: Add("아무도 나갈 수 없었"); Add("밀실"); Add("잠겨"); break;
                case PropKind.DeathWindow: Add(Hm(p.T0)); Add(Hm(p.T1)); break;
                case PropKind.TraceAt: if (p.Value != null && p.Value.StartsWith("bloodwriting")) { Add("「" + p.Value.Substring(p.Value.IndexOf(':') + 1) + "」"); Add(Name(p.B)); } Add(Room(p.Room)); break;
                case PropKind.Culprit: Add(Name(p.A)); break;
                case PropKind.Heard: Add(Hm(p.T0 + 2)); Add(Hm(p.T0)); Add(Room(p.Room)); break;
                case PropKind.SawActor: Add(Name(p.B)); Add(Room(p.Room)); break;
                case PropKind.Disguised: Add(Room(p.Room)); Add(Hm(p.T0)); break;
                case PropKind.Loaned: Add(Name(p.B)); Add(Name(p.A)); break;
                default: Add(Name(p.A)); Add(Room(p.Room)); break;
            }
            Add(Hx(p.T0)); Add(Hx(p.T1)); Add(Hx(p.T0 + 2));
            return L;
        }

        /// <summary>A claim whose rendered line doesn't spell out its proposition ("I'm not the one!") is restated in the speaker's voice.</summary>
        static string Restate(Simulation sim, TrialClaim c)
        {
            var S = sim.S; var p = c.Prop; if (p == null || p.Room < 0) return null;
            var d = new Dictionary<string, string>(); string key;
            string place = S.RoomName(p.Room), time = ClockFmt.Vague(p.T0);
            switch (p.Kind)
            {
                case PropKind.AtPlace: if (p.A == c.Speaker) { key = "claim_alibi"; d["time"] = time; d["place"] = place; } else { key = "claim_saw"; d["t"] = "@" + p.A; d["place"] = place; d["time"] = time; } break;
                case PropKind.WithPerson: key = "alibi_with"; d["t"] = "@" + p.B; d["place"] = place; d["time"] = time; break;
                case PropKind.Held: key = "saw_item"; d["t"] = "@" + p.A; d["item"] = ItemCatalog.Get(p.Item)?.Kor ?? "무언가"; d["place"] = place; break;
                case PropKind.Heard: key = "claim_heard"; d["time"] = ClockFmt.Vague(p.T0 + 2); d["place"] = place; d["sound"] = "이상한"; break;
                case PropKind.AliveAt: key = "claim_saw"; d["t"] = "@" + p.A; d["place"] = place; d["time"] = time; break;
                default: return null;
            }
            return sim.Render(c.Speaker, null, key, d);
        }

        /// <summary>Card suggestion by topical relevance only (shared people / rooms / times) — deliberately blind to which card actually works.</summary>
        static List<string> Suggest(Simulation sim, List<TrialClaim> claims, int n) => CaseBoard.Rank(sim, claims).Take(n).ToList();

        /// <summary>Stamp the wax seal of an evidence card onto a testimony's phrase. Crimson wax refutes, black wax vouches.</summary>
        public static ShotResult Seal(Simulation sim, int lineIdx, string cardId, bool support) => Plain(SealRaw(sim, lineIdx, cardId, support));
        static ShotResult SealRaw(Simulation sim, int lineIdx, string cardId, bool support)
        {
            var T = sim.S.Trial; var G = T?.Game;
            if (G == null || G.Kind != "inquiry" || G.Status != "open" || lineIdx < 0 || lineIdx >= G.Lines.Count) return new ShotResult();
            var L = G.Lines[lineIdx]; var c = T.Claims.FirstOrDefault(x => x.Id == L.ClaimId);
            if (c == null || L.Result == "backed" || L.Result == "hit") return new ShotResult();
            return Fire(sim, T, G, c, support ? "agree" : "contra", cardId, L, !support || L.Kind == "defense");
        }

        /// <summary>Press a witness on a line (where did you see it? how sure?) — their answer can narrow or shake the statement.</summary>
        public static List<(string who, string text)> Press(Simulation sim, int lineIdx) => PressRaw(sim, lineIdx).Select(x => (x.who, CaseBoard.Plain(x.text))).ToList();
        static List<(string who, string text)> PressRaw(Simulation sim, int lineIdx)
        {
            var T = sim.S.Trial; var G = T?.Game; var res = new List<(string, string)>();
            if (G == null || G.Status != "open" || lineIdx < 0 || lineIdx >= G.Lines.Count) return res;
            var L = G.Lines[lineIdx]; if (L.ClaimId == null || L.Pressed) return res;
            L.Pressed = true; int before = T.Beats.Count;
            TrialSystem.PlayerAskSource(sim, L.ClaimId);
            foreach (var b in T.Beats.Skip(before)) if ((b.Kind == "line" || b.Kind == "result") && !string.IsNullOrEmpty(b.Text)) res.Add((b.Speaker, b.Text));
            if (T.Cursor == before) T.Cursor = T.Beats.Count;
            var c = T.Claims.FirstOrDefault(x => x.Id == L.ClaimId); if (c != null && (c.Status == "limited" || c.Status == "conditional")) L.Result = "shaken";
            return res;
        }

        static ShotResult Fire(Simulation sim, TrialState T, TrialGame G, TrialClaim c, string action, string cardId, GameLine L, bool endOnValid)
        {
            var S = sim.S; var res = new ShotResult(); int before = T.Beats.Count; G.Shots++;
            TrialSystem.Result r;
            if (!Usable(T, c, cardId, action))
            {
                T.Invalid++; T.Influence = MathX.Clamp01(T.Influence - 0.05f);
                TrialSystem.Say(T, "result", Cast.Player, action == "agree" ? "그 사람 말을 그 사람 말로 뒷받침할 수는 없다 — 다른 근거가 필요하다" : "그 말로 그 말 자체를 반박할 수는 없다", c.Id, "Irrelevant");
                TrialSystem.Line(T, sim, c.Speaker, "counter", Cast.Player, new Dictionary<string, string> { { "t", "@" + Cast.Player } });
                r = new TrialSystem.Result { R = LogicResult.Irrelevant, Text = "다른 근거가 필요하다" };
            }
            else r = action == "agree" ? TrialSystem.PlayerSupport(sim, c.Id, cardId) : TrialSystem.PlayerContradict(sim, c.Id, cardId);
            res.R = r.R; res.Text = r.Text; res.Valid = r.Valid; res.Break = r.Break;
            if (r.Valid)
            {
                res.Seal = r.R == LogicResult.Contradict ? "반증" : r.R == LogicResult.Support ? "뒷받침" : r.R == LogicResult.Conditional ? "흔들림" : "허점";
                res.Hourglass = TrialSystem.Reward(T);
                if (T.Cursor == before && T.Beats.Count > before + 1 && T.Beats[before].Speaker == Cast.Player && T.Beats[before].Kind == "line") T.Cursor = before + 1;
                if (!endOnValid)
                {
                    // a vouched line: sealed in black, the inquiry goes on (its record plays in the round itself)
                    if (L != null) L.Result = "backed"; if (T.Cursor <= before + 1) T.Cursor = T.Beats.Count;
                    return res;
                }
                if (L != null) L.Result = "hit";
                G.Outcome = r.R.ToString(); G.WinClaim = c.Id; G.WinBullet = cardId;
                if (G.Kind == "board" && G.Final != null && c.Id != G.Final.ClaimId) CutPremise(sim, T, G, c);
                End(T, G, "won"); res.Ended = true;
                FollowUp(sim, T, G, c, cardId);
                return res;
            }
            G.Misses++;
            var retort = T.Beats.Skip(before).LastOrDefault(b => b.Speaker == c.Speaker && b.Kind == "line");
            res.Retort = retort?.Text ?? T.Beats.Skip(before).LastOrDefault(b => b.Kind == "result")?.Text; res.RetortBy = retort != null ? c.Speaker : null;
            (res.SteerBy, res.Steer) = TrialSystem.Lapse(sim, T);
            if (T.Cursor == before) T.Cursor = T.Beats.Count;   // the lapse already played inside the round
            if (CheckLost(sim, T, G)) { res.Ended = true; res.Lost = true; }
            return res;
        }

        static void FollowUp(Simulation sim, TrialState T, TrialGame won, TrialClaim c, string cardId)
        {
            var S = sim.S; var rng = S.R(Stream.Trial);
            if (won.Kind != "inquiry") return;
            // the refutation established something that now needs a name → the engraved question
            if (T.Questions < 3 && TryQuestionFrom(sim, T, c, cardId)) return;
            // a proud or cornered speaker doubles down → their remaining reasoning goes on the thread board
            var sp = S.A(c.Speaker);
            if (sp == null || sp.IsPlayer || !sp.Alive || T.Boards >= 2) return;
            double will = sp.Def.P.Pride * 0.6 + sp.Def.Argue / 100.0 * 0.5 + (TrialSystem.IsCulprit(S, sp.Id) ? 0.35 : 0) - sp.Def.P.Fearfulness * 0.3;
            if (!rng.Chance(MathX.Clamp01((float)will) * 0.8)) return;
            var cards = PlayerCards(S).ToList();
            var other = T.Claims.Where(x => x.Speaker == sp.Id && x.Id != c.Id && x.Prop != null && x.Text != null && (x.Status == "open" || x.Status == "supported")).OrderByDescending(x => x.Beat).FirstOrDefault(x => Breakable(S, x, cards));
            if (other != null) StartBoard(sim, T, other, "challenge");
        }

        // ================================================================== 증언 대조 (cross-ledger)
        /// <summary>Two testimonies that cannot both be true (Logic, both directions, as hearsay for the court).</summary>
        static LogicVerdict Conflict(GameState S, TrialClaim a, TrialClaim b)
        {
            if (a?.Prop == null || b?.Prop == null || a.Speaker == b.Speaker) return null;
            var v1 = Logic.Check(S, a.Prop, b.Prop, false, "trial:" + b.Id); var v2 = Logic.Check(S, b.Prop, a.Prop, false, "trial:" + a.Id);
            var v = Rank(v1.Result) >= Rank(v2.Result) ? v1 : v2;
            return v.Result == LogicResult.Contradict || v.Result == LogicResult.Conditional ? v : null;
        }

        public static bool StartLedger(Simulation sim, TrialState T, string topic)
        {
            var S = sim.S; var rng = S.R(Stream.Trial);
            var said = T.Claims.Where(c => c.Speaker != Cast.Player && c.Speaker != Cast.Butler && c.Prop != null && c.Text != null && c.Status != "retracted" && c.Status != "refuted" && S.A(c.Speaker)?.Alive == true
                && (c.Topic != "premise" || T.GameLog.Contains("shown:" + c.Id)) && c.Prop.Kind != PropKind.Culprit).ToList();
            (TrialClaim a, TrialClaim b) best = (null, null); int bestScore = -1;
            for (int i = 0; i < said.Count; i++)
                for (int j = i + 1; j < said.Count; j++)
                {
                    var a = said[i]; var b = said[j]; if (Conflict(S, a, b) == null) continue;
                    if (T.GameLog.Contains("pair:" + a.Id + ":" + b.Id)) continue;
                    int sc = (a.Topic == topic ? 2 : 0) + (b.Topic == topic ? 2 : 0) + Math.Max(a.Beat, b.Beat) / 10;
                    if (sc > bestScore) { bestScore = sc; best = (a, b); }
                }
            if (best.a == null) return false;
            var G = NewGame(T, "ledger"); G.Title = "증언 대조"; G.Topic = topic; G.Opponent = best.a.Speaker; G.Second = best.b.Speaker;
            G.Subtitle = $"{Cast.NameOf(best.a.Speaker)} ↔ {Cast.NameOf(best.b.Speaker)}";
            foreach (var (who, key, list) in new[] { (best.a.Speaker, best.a, G.Lines), (best.b.Speaker, best.b, G.Lines2) })
            {
                var own = said.Where(c => c.Speaker == who && c.Id != key.Id).OrderByDescending(c => c.Beat).Take(3).ToList(); own.Add(key); rng.Shuffle(own);
                foreach (var c in own) { var L = MakeLine(sim, T, c, LineKind(c)); if (!list.Any(x => x.Text == L.Text)) list.Add(L); }
            }
            G.TimeLimit = 70 + 8 * (G.Lines.Count + G.Lines2.Count);
            T.Ledgers++; T.GameLog.Add("ledger:" + topic); T.GameLog.Add("pair:" + best.a.Id + ":" + best.b.Id);
            Begin(T, G);
            return true;
        }

        /// <summary>The player joins one line of the left testimony to one line of the right: can both be true?</summary>
        public static ShotResult LedgerPick(Simulation sim, int left, int right) => Plain(LedgerPickRaw(sim, left, right));
        static ShotResult LedgerPickRaw(Simulation sim, int left, int right)
        {
            var S = sim.S; var T = S.Trial; var G = T?.Game; var res = new ShotResult();
            if (G == null || G.Kind != "ledger" || G.Status != "open" || left < 0 || right < 0 || left >= G.Lines.Count || right >= G.Lines2.Count) return res;
            var a = T.Claims.FirstOrDefault(c => c.Id == G.Lines[left].ClaimId); var b = T.Claims.FirstOrDefault(c => c.Id == G.Lines2[right].ClaimId);
            G.Shots++; var v = Conflict(S, a, b);
            if (v != null)
            {
                foreach (var c in new[] { a, b }) { if (c.Status == "open" || c.Status == "supported") c.Status = "conditional"; if (!T.Public.Any(p => p.Kind == c.Prop.Kind && p.A == c.Prop.A && p.Room == c.Prop.Room && p.T0 == c.Prop.T0)) T.Public.Add(c.Prop.Clone()); }
                T.Valid++; T.Influence = MathX.Clamp01(T.Influence + 0.1f);
                TrialSystem.Say(T, "result", Cast.Player, $"{Cast.GivenOf(a.Speaker)}의 말과 {Cast.GivenOf(b.Speaker)}의 말은 둘 다 맞을 수는 없다 — {v.Why}", a.Id, "Ledger");
                foreach (var who in new[] { a.Speaker, b.Speaker })
                {
                    var actor = S.A(who); bool liar = (who == a.Speaker ? a : b).Lie;
                    // the one who lied shakes (composure decides how visibly); the honest one insists
                    TrialSystem.Line(T, sim, who, liar && S.R(Stream.Trial).F() * 100 > (actor?.Def.Composure ?? 50) ? "panic" : "counter", Cast.Player, new Dictionary<string, string> { { "t", "@" + (who == a.Speaker ? b.Speaker : a.Speaker) } }, emo: liar ? Emotion.Fear : Emotion.Angry);
                    foreach (var id in T.Participants.Where(x => x != Cast.Player && x != who)) { var k = S.K(id); k.Suspicion[who] = (k.Suspicion.TryGetValue(who, out var s0) ? s0 : 0) + 0.06f; }
                }
                foreach (var id in T.Participants.Where(x => x != Cast.Player)) Relations.Change(S, id, Cast.Player, respect: 0.03f);
                G.Outcome = v.Result.ToString(); G.Lines[left].Result = "hit"; G.Lines2[right].Result = "hit";
                res.Valid = true; res.R = v.Result; res.Text = v.Why; res.Seal = "모순"; res.Ended = true; res.Hourglass = TrialSystem.Reward(T);
                End(T, G, "won");
                return res;
            }
            G.Misses++; T.Invalid++; T.Influence = MathX.Clamp01(T.Influence - 0.05f);
            int before = T.Beats.Count;
            TrialSystem.Say(T, "result", Cast.Player, "두 말은 둘 다 맞을 수 있다 — 어긋나지 않는다", a?.Id, "Irrelevant");
            (res.SteerBy, res.Steer) = TrialSystem.Lapse(sim, T);
            if (T.Cursor == before) T.Cursor = T.Beats.Count;
            res.Text = "두 말은 둘 다 맞을 수 있다";
            if (CheckLost(sim, T, G)) { res.Ended = true; res.Lost = true; }
            else if (G.Misses >= 3) { TrialSystem.Say(T, "result", null, "대조는 아무 결론 없이 끝났다", data: "LedgerLost"); End(T, G, "lost"); res.Ended = true; res.Lost = true; }
            return res;
        }

        // ================================================================== 붉은 실 (thread board)
        public static bool StartBoard(Simulation sim, TrialState T, TrialClaim target, string why)
        {
            var S = sim.S; var op = S.A(target?.Speaker); if (op == null || !op.Alive || op.IsPlayer || target.Text == null || target.Prop == null) return false;
            var cards = PlayerCards(S).ToList();
            var prem = target.Premises.Select(id => T.Claims.FirstOrDefault(c => c.Id == id)).Where(c => c?.Prop != null && c.Text != null).ToList();
            if (prem.Count == 0) prem = T.Claims.Where(c => c.Speaker == target.Speaker && c.Id != target.Id && c.Prop != null && c.Text != null && c.Status != "refuted" && c.Status != "retracted").OrderByDescending(c => c.Beat).Take(3).ToList();
            bool any = Breakable(S, target, cards) || prem.Any(c => Breakable(S, c, cards));
            if (!any && why == "challenge") return false;    // a challenge only goes to the board if there is something to pull on
            var G = NewGame(T, "board"); G.Title = "붉은 실"; G.Opponent = op.Id; G.Why = why; G.Topic = target.Id;
            G.Subtitle = Cast.NameOf(op.Id) + (why == "challenge" ? "의 반론" : why == "defense" ? "의 지목 — 최종 변론" : "의 지목");
            G.Final = MakeLine(sim, T, target, "claim");
            foreach (var c in prem.Take(4)) { G.Lines.Add(MakeLine(sim, T, c, "claim")); if (!T.GameLog.Contains("shown:" + c.Id)) T.GameLog.Add("shown:" + c.Id); }
            G.Bullets = Suggest(sim, prem.Append(target).ToList(), 6);
            G.TimeLimit = 90;
            T.Boards++; T.GameLog.Add("board:" + target.Id); if (!T.GameLog.Contains("shown:" + target.Id)) T.GameLog.Add("shown:" + target.Id);
            if (T.PendingPrompt == "rebut") T.PendingPrompt = null;
            TrialSystem.Line(T, sim, op.Id, "counter", Cast.Player, new Dictionary<string, string> { { "t", "@" + Cast.Player } }, emo: Emotion.Angry, gesture: Anim.Point);
            Begin(T, G);
            return true;
        }

        public static bool StartDefenseBoard(Simulation sim, TrialState T)
        {
            var acc = T.Claims.Where(c => c.Accused == Cast.Player && c.Speaker != Cast.Player && c.Status != "refuted" && sim.S.A(c.Speaker)?.Alive == true).OrderByDescending(c => c.Supporters.Count).ThenByDescending(c => c.Beat).FirstOrDefault();
            return acc != null && StartBoard(sim, T, acc, "defense");
        }

        /// <summary>Tie a red thread from an evidence card to a pinned statement (idx -1 = the accusation itself). Two snapped threads lose the board.</summary>
        public static ShotResult BoardTie(Simulation sim, int idx, string cardId) => Plain(BoardTieRaw(sim, idx, cardId));
        static ShotResult BoardTieRaw(Simulation sim, int idx, string cardId)
        {
            var T = sim.S.Trial; var G = T?.Game; if (G == null || G.Kind != "board" || G.Why == "final" || G.Status != "open") return new ShotResult();
            var L = idx < 0 ? G.Final : idx < G.Lines.Count ? G.Lines[idx] : null; if (L == null) return new ShotResult();
            var c = T.Claims.FirstOrDefault(x => x.Id == L.ClaimId); if (c == null) return new ShotResult();
            var res = Fire(sim, T, G, c, "contra", cardId, L, true);
            if (!res.Ended && G.Misses >= 2) { LoseBoard(sim, T, G); End(T, G, "lost"); res.Ended = true; res.Lost = true; }
            return res;
        }

        /// <summary>A premise under an accusation was cut: the accusation stands on nothing.</summary>
        static void CutPremise(Simulation sim, TrialState T, TrialGame G, TrialClaim premise)
        {
            var acc = T.Claims.FirstOrDefault(x => x.Id == G.Final?.ClaimId); if (acc == null) return;
            if (acc.Status == "open" || acc.Status == "supported") acc.Status = "limited";
            TrialSystem.Say(T, "result", Cast.Player, $"{Cast.GivenOf(acc.Speaker)}의 지목을 떠받치던 실 한 가닥이 끊어졌다 — 그 지목은 근거를 잃는다", acc.Id, "LimitScope");
            foreach (var id in acc.Supporters.ToList()) { var st = T.Stances.TryGetValue(id, out var s) ? s : null; if (st != null && st.Supporting == acc.Accused) st.Supporting = null; }
            acc.Supporters.Clear();
            if (acc.Accused != null) foreach (var id in T.Participants.Where(x => x != Cast.Player)) { var k = sim.S.K(id); if (k.Suspicion.ContainsKey(acc.Accused)) k.Suspicion[acc.Accused] *= 0.7f; }
        }

        public static void BoardYield(Simulation sim)
        {
            var T = sim.S.Trial; var G = T?.Game; if (G == null || G.Kind != "board" || G.Status != "open") return;
            T.Influence = MathX.Clamp01(T.Influence - 0.03f);
            if (G.Why == "final") { FinalBoard(sim, new List<string>()); return; }
            LoseBoard(sim, T, G); End(T, G, "lost");
        }

        static void LoseBoard(Simulation sim, TrialState T, TrialGame G)
        {
            var S = sim.S;
            TrialSystem.Say(T, "result", null, $"실은 끝내 이어지지 않았다 — {Cast.GivenOf(G.Opponent)}의 주장이 힘을 얻는다", data: "BoardLost");
            if (G.Why == "accused" || G.Why == "defense")
                foreach (var id in T.Participants.Where(x => x != Cast.Player)) { var k = S.K(id); k.Suspicion[Cast.Player] = (k.Suspicion.TryGetValue(Cast.Player, out var v) ? v : 0) + 0.12f; }
            var fin = T.Claims.FirstOrDefault(x => x.Id == G.Final?.ClaimId); if (fin != null && fin.Status == "open") fin.Status = "supported";
        }

        // ---- the final board: the accused at the centre, opportunity / means / deceit
        public static bool StartFinalBoard(Simulation sim, TrialState T)
        {
            var S = sim.S; if (!TrialSystem.PlayerIn(S, T)) return false;
            var acc = S.A(T.Accused); if (acc == null || !acc.Alive || acc.IsPlayer) return false;
            string g = Cast.GivenOf(acc.Id);
            var G = NewGame(T, "board"); G.Title = "붉은 실 — 최후 논증"; G.Opponent = acc.Id; G.Why = "final"; G.Subtitle = Josa(Cast.NameOf(acc.Id), "을") + " 겨눈 세 매듭";
            G.Slots = new List<GameSlot>
            {
                new GameSlot { Id = "chance", Label = "기회", Hint = $"그 시각, {Josa(g, "은")} 현장 가까이에 있었다" },
                new GameSlot { Id = "means", Label = "수단", Hint = $"{Josa(g, "은")} 흉기가 될 만한 것을 손에 넣을 수 있었다" },
                new GameSlot { Id = "deceit", Label = "거짓", Hint = $"{Josa(g, "은")} 무언가를 감추려고 거짓말을 했다" },
            };
            G.Bullets = Suggest(sim, T.Claims.Where(c => c.Speaker == acc.Id || c.Prop?.A == acc.Id).ToList(), 8);
            G.TimeLimit = 100;
            Begin(T, G);
            return true;
        }

        static bool SlotFits(Simulation sim, TrialState T, string slot, string acc, Evidence ev, out string why)
        {
            why = null; if (ev == null) return false;
            var S = sim.S; var (t0, t1, scene) = Testimony.CaseWindow(S);
            var rooms = new HashSet<int>(); if (scene >= 0) rooms.Add(scene);
            var inc = TrialSystem.TargetIncident(S); if (inc != null && inc.FoundRoom >= 0) rooms.Add(inc.FoundRoom);
            foreach (var p in T.Public) if ((p.Kind == PropKind.TraceAt || p.Kind == PropKind.DeathPlace) && p.Room >= 0) rooms.Add(p.Room);
            bool Win(Prop p) => p.T1 <= 0 || (p.T0 <= t1 + 5 && p.T1 >= t0 - 20);
            foreach (var p in ev.Props)
            {
                switch (slot)
                {
                    case "chance":
                        if ((p.Kind == PropKind.AtPlace || p.Kind == PropKind.Held) && p.A == acc && rooms.Contains(p.Room) && Win(p)) { why = $"사건 무렵 {S.RoomName(p.Room)}에 있었다"; return true; }
                        if (p.Kind == PropKind.DeviceRecord && p.A == acc && p.Value == "in" && rooms.Contains(p.Room)) { why = "출입 기록기에 현장에 들어간 기록이 남았다"; return true; }
                        if (p.Kind == PropKind.SawActor && p.B == acc && p.Item != "unsure" && rooms.Contains(p.Room) && Win(p)) { why = "현장 근처에서 봤다는 사람이 있다"; return true; }
                        if (p.Kind == PropKind.Heard && p.Value == "voice:" + acc && rooms.Contains(p.Room)) { why = "현장 쪽에서 목소리가 들렸다 (녹음이 아니라면)"; return true; }
                        break;
                    case "means":
                        if (p.Kind == PropKind.Held && p.A == acc && ItemCatalog.Get(p.Item)?.IsWeapon == true) { why = Josa(ItemCatalog.Get(p.Item)?.Kor ?? "흉기", "을") + " 들고 있었다"; return true; }
                        if (p.Kind == PropKind.Bloodied && p.A == acc) { why = "옷에 피가 묻어 있었다"; return true; }
                        if (p.Kind == PropKind.Injured && p.A == acc) { why = "피해자가 저항하다 낸 것으로 보이는 상처가 있다"; return true; }
                        break;
                    case "deceit":
                        if (p.Kind == PropKind.Lie && p.A == acc) { why = "진술이 사실과 달랐다"; return true; }
                        if (p.Kind == PropKind.MachineUsed && p.A == acc && p.Value != null && p.Value.Contains("녹음")) { why = "녹음해 둔 목소리로 알리바이를 꾸몄다"; return true; }
                        if (p.Kind == PropKind.Loaned && p.B == acc && p.Value != null && p.Value.StartsWith("courier:")) { why = "다른 사람을 시켜 피해자를 불러냈다"; return true; }
                        if (p.Kind == PropKind.TraceAt && p.Value != null && p.Value.StartsWith("bloodwriting") && p.B != acc && T.Public.Any(x => x.Kind == PropKind.TraceAt && (x.Value == "instant-death" || (x.Value != null && x.Value.StartsWith("writing-hand:"))))) { why = "피해자가 쓴 척 꾸민 글씨로 다른 사람에게 누명을 씌웠다"; return true; }
                        break;
                }
            }
            // their own statement already fell apart in this court
            if (slot == "deceit" && T.Claims.Any(c => c.Speaker == acc && c.Status == "refuted" && c.Lie && ev.Props.Any(p => Logic.Check(S, c.Prop, p, ev.Direct, ev.Root).Result == LogicResult.Contradict))) { why = "이 증거로 그 진술은 이미 무너졌다"; return true; }
            return false;
        }

        /// <summary>The final argument: one evidence card tied to each anchor (opportunity / means / deceit), each from an independent source.</summary>
        public static List<string> FinalBoard(Simulation sim, IList<string> chosen)
        {
            var S = sim.S; var T = S.Trial; var G = T?.Game; var res = new List<string>();
            if (G == null || G.Kind != "board" || G.Why != "final" || G.Status != "open") return res;
            string acc = G.Opponent; var roots = new HashSet<string>(); int ok = 0, bad = 0;
            for (int i = 0; i < G.Slots.Count; i++)
            {
                var slot = G.Slots[i]; string id = chosen != null && i < chosen.Count ? chosen[i] : null; slot.Chosen = id;
                if (id == null) { slot.Result = "empty"; res.Add("empty"); continue; }
                var ev = BulletEvidence(S, id); string root = ev?.Root ?? id;
                if (ev != null && !roots.Contains(root) && SlotFits(sim, T, slot.Id, acc, ev, out var why)) { ok++; roots.Add(root); slot.Result = "ok"; slot.Why = CaseBoard.Plain(why); }
                else { bad++; slot.Result = "bad"; slot.Why = ev != null && roots.Contains(root) ? "같은 데서 나온 단서를 두 번 쓸 수는 없다" : "이 매듭에는 들어맞지 않는 단서다"; }
                res.Add(slot.Result);
            }
            T.PanicScore = ok; T.PanicMax = G.Slots.Count;
            float frac = G.Slots.Count > 0 ? ok / (float)G.Slots.Count : 0;
            foreach (var id in T.Participants.Where(x => x != Cast.Player && x != acc))
            {
                var k = S.K(id); float trust = MathX.Clamp01(0.3f + S.R(id, Cast.Player).Trust + S.R(id, Cast.Player).Respect);
                k.Suspicion[acc] = (k.Suspicion.TryGetValue(acc, out var v) ? v : 0) + frac * 0.6f * (0.5f + trust * 0.5f);
            }
            T.Influence = MathX.Clamp01(T.Influence + 0.05f * ok - 0.04f * bad);
            if (ok > 0) T.Valid += ok; if (bad > 0) T.Invalid += bad;
            TrialSystem.Say(T, "result", Cast.Player, $"실 {G.Slots.Count}가닥 중 {ok}가닥이 이어졌다" + (ok == G.Slots.Count ? " — 빈틈없는 논증이다" : ok == 0 ? " — 논증이 서지 않는다" : ""), data: "FinalBoard");
            if (ok >= 2) TrialSystem.Line(T, sim, acc, "panic", Cast.Player, new Dictionary<string, string> { { "t", "@" + Cast.Player } }, emo: Emotion.Fear, gesture: Anim.Cower);
            else TrialSystem.Line(T, sim, acc, "counter", Cast.Player, new Dictionary<string, string> { { "t", "@" + Cast.Player } }, emo: Emotion.Angry, gesture: Anim.Point);
            End(T, G, ok >= 2 ? "won" : "lost");
            return res;
        }

        // ================================================================== 새겨진 물음 (naming what the evidence established)
        static readonly string[] TrickNames = new[] { "낚싯줄로 밖에서 잠근 밀실", "숨진 시각을 속인 체온 조작", "가짜 다잉 메시지", "흉기 바꿔치기", "녹음기로 만든 목소리 알리바이", "시신 옮기기", "시계를 틀어 놓은 알리바이" }.Concat(TrialSystem.MethodTrickWords).ToArray();   // + second-wave stagings (TrialMethods.cs)

        public static string DamageWord(DamageType d)
        {
            switch (d)
            {
                case DamageType.Stab: return "찔린 상처"; case DamageType.Cut: return "베인 상처"; case DamageType.Blunt: return "둔기에 맞은 상처"; case DamageType.Crush: return "짓눌린 상처";
                case DamageType.Drown: return "익사"; case DamageType.Choke: return "목 졸림"; case DamageType.Fall: return "추락"; case DamageType.Burn: return "화상"; case DamageType.Shock: return "감전";
            }
            return null;
        }

        static string Josa(string w, string p) => w + LineBank.Josa(w, p);

        static bool TryQuestionFrom(Simulation sim, TrialState T, TrialClaim c, string cardId)
        {
            var S = sim.S; var ev = BulletEvidence(S, cardId); if (ev == null || c.Prop == null) return false;
            { var mq = TrialSystem.MethodQuestion(S, c.Prop, ev); if (mq.word != null) return StartQuestion(sim, T, mq.word, mq.question, "trick", mq.fact, "트릭"); }   // staged accident / natural death / suicide / slid key (TrialMethods.cs)
            var victim = TrialSystem.TargetIncident(S)?.Victim;
            switch (c.Prop.Kind)
            {
                case PropKind.WeaponType:
                    {
                        if (ev.Props.Any(p => p.Kind == PropKind.ItemState && p.Value == "smeared")) return StartQuestion(sim, T, "흉기 바꿔치기", "시신 옆의 피 묻은 물건 — 범인은 무슨 짓을 했나?", "trick", ev.Props.First(p => p.Kind == PropKind.ItemState && p.Value == "smeared").Clone(), "트릭");
                        var val = ev.Props.FirstOrDefault(p => p.Kind == PropKind.WeaponType)?.Value ?? ev.Props.FirstOrDefault(p => p.Kind == PropKind.Wound)?.Item;
                        if (val == null) return false; var d = TrialSystem.P(val);
                        return StartQuestion(sim, T, DamageWord(d), $"그렇다면 {Cast.GivenOf(c.Prop.A ?? victim)}의 진짜 사인은?", "cause", new Prop { Kind = PropKind.WeaponType, A = c.Prop.A ?? victim, Value = d.ToString() }, "사인");
                    }
                case PropKind.AtPlace: case PropKind.WithPerson: case PropKind.Held:
                    {
                        var who = c.Prop.A;
                        var where = ev.Props.FirstOrDefault(p => (p.Kind == PropKind.AtPlace || p.Kind == PropKind.Held || p.Kind == PropKind.DeviceRecord) && p.A == who && p.Room >= 0 && p.Room != c.Prop.Room)
                                    ?? ev.Props.FirstOrDefault(p => p.Kind == PropKind.SawActor && p.B == who && p.Room >= 0 && p.Room != c.Prop.Room);
                        if (where == null) return false;
                        return StartQuestion(sim, T, S.RoomName(where.Room), $"그 시각, {Josa(Cast.GivenOf(who), "이")} 실제로 있던 곳은?", "room", new Prop { Kind = PropKind.AtPlace, A = who, Room = where.Room, T0 = where.T0, T1 = where.T1 }, "장소");
                    }
                case PropKind.DoorLocked:
                    if (ev.Props.Any(p => (p.Kind == PropKind.TraceAt && (p.Value == "thread-under-door" || p.Item == "Scratch")) || (p.Kind == PropKind.ItemState && p.Value == "line-cut")))
                        return StartQuestion(sim, T, "낚싯줄로 밖에서 잠근 밀실", "문은 안에서 잠겨 있었다 — 범인은 어떻게 빠져나갔나?", "trick", ev.Props.First(p => p.Kind == PropKind.TraceAt || p.Kind == PropKind.ItemState).Clone(), "트릭");
                    return false;
                case PropKind.DeathWindow:
                    if (ev.Props.Any(p => p.Kind == PropKind.TraceAt && (p.Value == "temp-warm" || p.Value == "temp-cold" || p.Value == "fire-stoked" || p.Item == "Water")))
                        return StartQuestion(sim, T, "숨진 시각을 속인 체온 조작", "시신으로 짐작한 숨진 시각이 어긋난 이유 — 범인은 무엇을 꾸몄나?", "trick", ev.Props.First(p => p.Kind == PropKind.TraceAt).Clone(), "트릭");
                    return false;
                case PropKind.TraceAt:
                    if (c.Prop.Value != null && c.Prop.Value.StartsWith("bloodwriting"))
                        return StartQuestion(sim, T, "가짜 다잉 메시지", "그 피 글씨의 정체는?", "trick", ev.Props.First(p => p.Kind == PropKind.TraceAt).Clone(), "트릭");
                    return false;
                case PropKind.DeathPlace:
                    if (ev.Props.Any(p => (p.Kind == PropKind.BodyMoved && p.Value == "likely") || (p.Kind == PropKind.TraceAt && (p.Item == "DragMark" || p.Item == "BloodSmear"))))
                        return StartQuestion(sim, T, "시신 옮기기", "발견된 곳이 범행 현장이 아니라면 — 범인이 한 일은?", "trick", new Prop { Kind = PropKind.BodyMoved, A = victim, Value = "likely" }, "트릭");
                    return false;
                case PropKind.DoorState:
                    if (ev.Props.Any(p => p.Kind == PropKind.ItemAt && p.Value == "moved"))
                        return StartQuestion(sim, T, "열쇠", "밖에서 문을 잠글 수 있었던 이유 — 범인이 쓴 것은?", "trick", null, "트릭");
                    return false;
                case PropKind.Heard:
                    if (ev.Props.Any(p => p.Kind == PropKind.MachineUsed && p.Value != null && p.Value.Contains("녹음")))
                        return StartQuestion(sim, T, "녹음기로 만든 목소리 알리바이", "그 목소리의 정체는?", "trick", ev.Props.First(p => p.Kind == PropKind.MachineUsed).Clone(), "트릭");
                    if (ev.Props.Any(p => p.Kind == PropKind.ClockOffset && p.Value != "0"))
                        return StartQuestion(sim, T, "시계를 틀어 놓은 알리바이", "모두가 말한 시각이 어긋난 이유는?", "trick", ev.Props.First(p => p.Kind == PropKind.ClockOffset).Clone(), "트릭");
                    return false;
            }
            return false;
        }

        static bool StartCauseQuestion(Simulation sim, TrialState T)
        {
            var S = sim.S; var inc = TrialSystem.TargetIncident(S); if (inc == null || T.Questions >= 3) return false;
            if (T.Claims.Any(c => c.Topic == "cause" && c.Status == "supported")) return false;   // an examiner already established it
            var wp = PlayerCards(S).Where(e => e.Kind == EvKind.Body && e.Subject == inc.Victim).SelectMany(e => e.Props).FirstOrDefault(p => p.Kind == PropKind.WeaponType);
            if (wp == null) return false;
            return StartQuestion(sim, T, DamageWord(TrialSystem.P(wp.Value)), $"{Josa(Cast.GivenOf(inc.Victim), "은")} 무엇 때문에 숨졌나?", "cause", new Prop { Kind = PropKind.WeaponType, A = inc.Victim, Value = wp.Value }, "사인");
        }

        static IEnumerable<string> Decoys(GameState S, string category, string word)
        {
            if (category == "cause") return new[] { DamageType.Stab, DamageType.Cut, DamageType.Blunt, DamageType.Crush, DamageType.Drown, DamageType.Choke }.Select(DamageWord).Where(w => w != word);
            if (category == "room") return S.Layout.Rooms.Where(r => !RoomInfo.IsPassage(r.Type)).Select(r => S.RoomName(r.Id)).Where(w => w != word).Distinct().OrderBy(w => w, StringComparer.Ordinal);
            return TrickNames.Where(w => w != word);
        }

        public static bool StartQuestion(Simulation sim, TrialState T, string word, string question, string category, Prop fact, string label)
        {
            var S = sim.S; var rng = S.R(Stream.Trial);
            if (string.IsNullOrEmpty(word) || T.GameLog.Contains("named:" + word)) return false;
            var G = NewGame(T, "question"); G.Title = "새겨진 물음"; G.Subtitle = label; G.Question = question; G.Word = word; G.Fact = fact; G.Topic = category;
            var dec = Decoys(S, category, word).ToList(); rng.Shuffle(dec);
            G.Pool = dec.Take(category == "trick" ? 4 : 5).ToList(); G.Pool.Add(word); rng.Shuffle(G.Pool);
            G.TimeLimit = 45;
            T.Questions++; T.GameLog.Add("named:" + word);
            Begin(T, G);
            return true;
        }

        /// <summary>Choose an engraved plate. 2 = right (the fact goes public), -1 = wrong (trust cost), -2 = round lost, 0 = no round.</summary>
        public static int QuestionPick(Simulation sim, string choice)
        {
            var S = sim.S; var T = S.Trial; var G = T?.Game; if (G == null || G.Kind != "question" || G.Status != "open") return 0;
            if (choice == G.Word)
            {
                TrialSystem.Say(T, "line", Cast.Player, $"…그래. 답은 ‘{G.Word}’{LineBank.Josa(G.Word, "이야")}.", emo: Emotion.Angry, gesture: Anim.Point);
                TrialSystem.Say(T, "result", Cast.Player, $"명판이 제자리에 딸깍 맞물린다 — ‘{G.Word}’. 이제 모두가 아는 사실이 됐다.", data: "Named");
                if (G.Fact != null) Publish(sim, T, G.Fact, Cast.Player);
                TrialSystem.Reward(T);
                T.Influence = MathX.Clamp01(T.Influence + 0.08f); T.Valid++;
                if (G.Fact?.Kind == PropKind.WeaponType) foreach (var c in T.Claims.Where(c => c.Prop?.Kind == PropKind.WeaponType && c.Prop.Value != G.Fact.Value && c.Status == "open")) c.Status = "limited";
                var ally = T.Participants.Where(x => x != Cast.Player).Select(S.A).Where(a => a != null && a.Alive).OrderByDescending(a => S.R(a.Id, Cast.Player).Trust + S.R(a.Id, Cast.Player).Respect).ThenBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault();
                if (ally != null) TrialSystem.Line(T, sim, ally.Id, "agree", Cast.Player, new Dictionary<string, string> { { "t", "@" + Cast.Player } });
                End(T, G, "won");
                return 2;
            }
            G.Misses++; T.Influence = MathX.Clamp01(T.Influence - 0.04f); T.Invalid++; TrialSystem.Lapse(sim, T);
            if (CheckLost(sim, T, G)) return -2;
            if (G.Misses >= 2) { TrialSystem.Say(T, "result", null, "끝내 답을 맞히지 못했다", data: "QuestionLost"); End(T, G, "lost"); return -2; }
            return -1;
        }

        static void Publish(Simulation sim, TrialState T, Prop p, string by)
        {
            var S = sim.S; T.Public.Add(p.Clone());
            foreach (var id in T.Participants.Where(x => x != Cast.Player))
            {
                var k = S.K(id); string root = "trial:" + p.Kind + p.A + p.Value + p.Room;
                if (!k.Statements.Any(s => s.Root == root)) k.Statements.Add(new Statement { Id = S.NewId("st"), Speaker = by, Listener = id, Clock = S.Clock, Prop = p.Clone(), Root = root });
                var a = S.A(id); if (a != null) Testimony.UpdateSuspicion(sim, a);
            }
        }

        // ================================================================== 시계와 평면도 (clock & floor-plan reconstruction)
        public static bool StartReconstruct(Simulation sim, TrialState T)
        {
            if (T.RQ.Count == 0) return false;
            var G = NewGame(T, "reconstruct"); G.Title = "시계와 평면도"; G.Subtitle = "그날 있었던 일을 처음부터 되짚어라";
            foreach (var q in T.RQ) { var slot = new GameSlot { Id = q.Id, Label = q.Question }; for (int i = 0; i < q.Options.Count; i++) slot.Pieces.Add(i.ToString()); G.Slots.Add(slot); }
            G.TimeLimit = 300;
            Begin(T, G);
            return true;
        }

        public static string OptionLabel(GameState S, TrialSystem.RQ q, int i)
        {
            if (q == null || i < 0 || i >= q.Options.Count) return "?";
            var o = q.Options[i];
            if (q.Id == "who") return Cast.NameOf(o);
            if (q.Id == "weapon" && Enum.TryParse(o, out DamageType d)) return WoundText.Type(d);
            if (q.Id == "where" && int.TryParse(o, out int r)) return S.RoomName(r);
            if (q.Id == "when" && double.TryParse(o, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var t)) return ClockFmt.Vague(t);
            return o;
        }

        /// <summary>Check the reconstruction against what is PUBLIC (never against the hidden truth): "ok", "conflict", "open" or "empty" per question.</summary>
        public static Dictionary<string, string> ReconstructCheck(Simulation sim, Dictionary<string, int> answers)
        {
            var S = sim.S; var T = S.Trial; var G = T?.Game; var res = new Dictionary<string, string>(); if (T == null) return res;
            var victim = TrialSystem.TargetIncident(S)?.Victim; int conflicts = 0;
            foreach (var q in T.RQ)
            {
                if (!answers.TryGetValue(q.Id, out var a) || a < 0 || a >= q.Options.Count) { res[q.Id] = "empty"; continue; }
                var st = PublicFitOf(T, q, q.Options[a], victim); res[q.Id] = st; if (st == "conflict") conflicts++;
            }
            if (conflicts > 0 && G != null && G.Kind == "reconstruct" && G.Status == "open")
            {
                G.Misses++; T.Influence = Math.Max(0.05f, T.Influence - 0.03f * conflicts); T.Invalid++;
                int before = T.Beats.Count;
                TrialSystem.Say(T, "result", null, $"밝혀진 사실과 어긋나는 곳이 {conflicts}군데 있다", data: "ReconstructConflict");
                if (T.Cursor == before) T.Cursor = T.Beats.Count;
            }
            return res;
        }

        static bool AnyPublic(TrialState T, Func<Prop, bool> f) => T.Public.Any(f);

        static string PublicFitOf(TrialState T, TrialSystem.RQ q, string opt, string victim)
        {
            switch (q.Id)
            {
                case "weapon":
                    {
                        var pub = T.Public.Where(p => p.Kind == PropKind.WeaponType && p.A == victim && p.Item == null).Select(p => p.Value).Distinct().ToList();
                        if (pub.Count == 0) return "open"; return pub.Contains(opt) ? "ok" : "conflict";
                    }
                case "moved":
                    {
                        if (AnyPublic(T, p => p.A == victim && ((p.Kind == PropKind.TraceAt && p.Value == "postmortem-cut") || (p.Kind == PropKind.ItemState && (p.Value == "part-hidden" || p.Value == "burnt-bone"))))) return opt == "옮겨졌다" ? "ok" : "conflict";   // cut up in a work room and spread: it was moved
                        bool mv = AnyPublic(T, p => (p.Kind == PropKind.BodyMoved && p.A == victim && p.Value == "likely") || (p.Kind == PropKind.TraceAt && (p.Item == "DragMark" || p.Item == "BloodSmear")));
                        if (!mv) return "open"; return opt == "옮겨졌다" ? "ok" : "conflict";
                    }
                case "who":
                    if (opt == victim) return "conflict";
                    if (AnyPublic(T, p => p.Kind == PropKind.AtPlace && p.A == opt && p.Value == "window-cover")) return "conflict";
                    if (AnyPublic(T, p => (p.Kind == PropKind.Lie || p.Kind == PropKind.Held || p.Kind == PropKind.Bloodied) && p.A == opt)) return "ok";
                    return "open";
                case "where":
                    {
                        var rooms = T.Public.Where(p => p.Kind == PropKind.TraceAt && p.Room >= 0 && p.Item != null && (p.Item.Contains("Blood") || p.Item == "DragMark")).Select(p => p.Room.ToString()).ToList();
                        return rooms.Contains(opt) ? "ok" : "open";
                    }
                case "when":
                    {
                        if (!double.TryParse(opt, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var t)) return "open";
                        if (AnyPublic(T, p => p.Kind == PropKind.AliveAt && p.A == victim && p.T0 > t + 10)) return "conflict";   // seen alive after it
                        return "open";
                    }
                case "conceal":
                    {
                        { var mf = TrialSystem.MethodConcealFit(T, opt); if (mf != null) return mf; }   // burnt / waterlogged / buried (TrialMethods.cs)
                        var st = T.Public.Where(p => p.Kind == PropKind.ItemState).Select(p => p.Value).ToList();
                        if (st.Contains("세척 흔적")) return opt == "씻었다" ? "ok" : "conflict";
                        if (st.Contains("숨겨짐")) return opt == "다른 곳에 숨겼다" ? "ok" : "conflict";
                        return "open";
                    }
                case "trick":
                    {
                        bool seal = AnyPublic(T, p => (p.Kind == PropKind.TraceAt && (p.Value == "thread-under-door" || p.Item == "Scratch")) || (p.Kind == PropKind.ItemState && p.Value == "line-cut"));
                        bool tod = AnyPublic(T, p => p.Kind == PropKind.TraceAt && (p.Value == "temp-warm" || p.Value == "temp-cold" || p.Value == "fire-stoked"));
                        bool msg = AnyPublic(T, p => p.Kind == PropKind.TraceAt && (p.Value == "instant-death" || (p.Value != null && p.Value.StartsWith("writing-hand:")))) && AnyPublic(T, p => p.Kind == PropKind.TraceAt && p.Value != null && p.Value.StartsWith("bloodwriting"));
                        bool swap = AnyPublic(T, p => p.Kind == PropKind.ItemState && p.Value == "smeared");
                        string fits = seal ? TrialSystem.TrickOptions[1] : tod ? TrialSystem.TrickOptions[2] : msg ? TrialSystem.TrickOptions[3] : swap ? TrialSystem.TrickOptions[4] : null;
                        if (fits == null) { var mt = TrialSystem.MethodTrickFits(T); if (mt != null) fits = TrialSystem.TrickOptionFor(mt); }   // second-wave stagings
                        if (fits == null) return "open";
                        return opt == fits ? "ok" : opt == TrialSystem.TrickOptions[0] ? "conflict" : "open";
                    }
            }
            return "open";
        }

        /// <summary>Present the reconstruction to the court (persuasion by public fit) and return its narration.</summary>
        public static List<string> ReconstructSubmit(Simulation sim, Dictionary<string, int> answers)
        {
            var S = sim.S; var T = S.Trial; var G = T?.Game; if (T == null) return new List<string>();
            TrialSystem.PlayerReconstruct(sim, answers);
            var nar = Narrate(sim, T, answers);
            if (G != null && G.Kind == "reconstruct") { G.Narration = nar; if (G.Status == "open") End(T, G, "won"); }
            return nar;
        }

        static List<string> Narrate(Simulation sim, TrialState T, Dictionary<string, int> answers)
        {
            var S = sim.S; var victim = TrialSystem.TargetIncident(S)?.Victim; string V = victim != null ? Cast.GivenOf(victim) : "피해자";
            string Opt(string qid) { var q = T.RQ.FirstOrDefault(x => x.Id == qid); return q != null && answers.TryGetValue(qid, out var a) && a >= 0 && a < q.Options.Count ? OptionLabel(S, q, a) : null; }
            string Raw(string qid) { var q = T.RQ.FirstOrDefault(x => x.Id == qid); return q != null && answers.TryGetValue(qid, out var a) && a >= 0 && a < q.Options.Count ? q.Options[a] : null; }
            var list = new List<string>();
            var when = Opt("when"); var where = Opt("where");
            list.Add(when != null ? $"그날 {when}, " + (where != null ? $"{where}에서 {Josa(V, "은")} 범인과 마주쳤다." : $"{Josa(V, "은")} 범인과 마주쳤다.") : where != null ? $"{where}. {Josa(V, "은")} 그곳에서 범인과 마주쳤다." : $"그날, {Josa(V, "은")} 범인과 마주쳤다.");
            var weapon = Opt("weapon"); list.Add(weapon != null ? $"시신에 남은 것은 {weapon}. 범인은 그렇게 {V}의 숨을 끊었다." : "무엇으로 공격했는지는 밝혀지지 않았다.");
            var moved = Raw("moved"); list.Add(moved == "옮겨졌다" && Methods.Dismembered(S, victim) ? "범행 뒤, 범인은 시신을 톱이 있는 방으로 옮겨 해체하고, 조각을 천에 싸 여러 곳에 나눠 숨겼다." : moved == "옮겨졌다" ? "범행 뒤, 범인은 시신을 다른 곳으로 옮겨 놓았다." : moved != null ? "시신은 쓰러진 그 자리에 그대로 남겨졌다." : "시신이 옮겨졌는지는 알 수 없다.");
            var trick = Raw("trick"); list.Add(trick != null && trick != TrialSystem.TrickOptions[0] ? $"그리고 범인은 ‘{trick}’{LineBank.Josa(trick, "로")} 우리 눈을 가리려 했다." : trick != null ? "범인은 현장에 별다른 장치를 꾸미지 않았다." : "범인이 무엇을 꾸몄는지는 아직 알 수 없다.");
            var conceal = Raw("conceal"); list.Add(conceal == "씻었다" ? "범인은 흉기를 씻어 흔적을 지우려 했다." : conceal == "다른 곳에 숨겼다" ? "흉기는 다른 곳에 숨겨 두었다." : conceal == "현장에 두고 갔다" ? "흉기는 현장에 그대로 남겨 두었다." : conceal == "소각로에 태웠다" ? "흉기와 피 묻은 옷은 소각로에 던져 넣어 태워 없애려 했다." : conceal == "물속에 버렸다" ? "흉기는 수영장 물속에 떨어뜨려 버렸다." : conceal == "흙 속에 묻었다" ? "흉기는 온실 화분대 흙 속에 묻어 두었다." : "흉기의 행방은 아직 확실하지 않다.");
            var wq = T.RQ.FirstOrDefault(q => q.Id == "who"); string culp = wq != null && answers.TryGetValue("who", out var wi) && wi >= 0 && wi < wq.Options.Count ? wq.Options[wi] : null;
            list.Add(culp != null ? $"이 모든 일을 한 사람은 — {Cast.NameOf(culp)}." : "범인의 정체는 아직 어둠 속에 있다.");
            return list;
        }
    }
}
