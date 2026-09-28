using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    [Serializable]
    public sealed class TrialClaim
    {
        public string Id; public string Speaker; public Prop Prop; public string Text; public string Key; public bool Lie; public string Topic;
        public string Status = "open";   // open, refuted, limited, supported, retracted, conditional
        public int Beat; public bool Player; public List<string> Premises = new List<string>(); public string Accused; public List<string> Supporters = new List<string>();
        public string RefutedBy; public string RefuteWhy;
    }

    [Serializable]
    public sealed class TrialBeat
    {
        public int N; public string Kind;   // line, mode, prompt, result, vote, verdict, system, break
        public string Speaker; public string Text; public string Key; public string ClaimId; public TrialMode Mode; public string Data; public Emotion Emotion; public Anim Gesture;
        public List<string> Options = new List<string>();
    }

    [Serializable]
    public sealed class TrialStance { public string Actor; public string Accusing; public string Supporting; public float Confidence; public string VoteIntent; public List<string> Log = new List<string>(); }

    [Serializable]
    public sealed class TrialState
    {
        public string Id; public int Loop, Chapter; public string Stage = "Opening"; public TrialMode Mode = TrialMode.TM01_Debate; public string Topic = "opening";
        public List<string> Participants = new List<string>(); public List<string> Dead = new List<string>(); public Dictionary<string, int> Seat = new Dictionary<string, int>();
        public List<TrialClaim> Claims = new List<TrialClaim>(); public List<TrialBeat> Beats = new List<TrialBeat>(); public int Cursor;
        public Dictionary<string, TrialStance> Stances = new Dictionary<string, TrialStance>();
        public List<Prop> Public = new List<Prop>(); public List<string> PublicRoots = new List<string>();
        public Dictionary<string, string> Votes = new Dictionary<string, string>(); public int VoteRound; public string Accused; public string PlayerAccused;
        public float Influence = 0.5f; public int Valid, Invalid, Beat; public List<string> SpokeOpening = new List<string>(); public List<string> TopicsDone = new List<string>();
        public bool Finished; public string DefenseDone; public int ReconstructScore, ReconstructMax; public List<string> Reconstruct = new List<string>();
        public string PendingPrompt; public List<string> ModesSeen = new List<string>(); public double ClockAtStart; public bool NoMurder; public int Limit = 70;
        // class-trial minigames (TrialGames): the current/last round, a log of rounds played, and the final-phase step machine
        public TrialGame Game; public List<string> GameLog = new List<string>(); public int GameBeats; public int Inquiries, Ledgers, Boards, Questions;
        public string FinalStep; public int PanicScore, PanicMax; public List<TrialSystem.RQ> RQ = new List<TrialSystem.RQ>();
        public int Streak, Hourglass; public List<string> Withheld = new List<string>();
        // the debate 심판 (Sim/Trial/Debate, TrialReforge.md): null = this trial runs the old engine (and old saves stay byte-identical)
        public DebateState Debate;
    }

    public static partial class TrialSystem
    {
        /// <summary>Start new 심판s as a debate (Sim/Trial/Debate). Read only in Begin: a trial keeps the engine it began with.</summary>
        public static bool DebateEnabled;

        // ------------------------------------------------------------------ start
        public static void Begin(Simulation sim)
        {
            var S = sim.S;
            var T = new TrialState { Id = S.NewId("trial"), Loop = S.Loop, Chapter = S.Chapter, ClockAtStart = S.Clock };
            T.Participants = S.Living.Select(a => a.Id).OrderBy(id => id).ToList();
            T.Dead = S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Confirmed).Select(i => i.Victim).ToList();
            T.NoMurder = !S.Incidents.Values.Any(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Murder);
            int seat = 0; foreach (var c in Cast.Participants) { T.Seat[c.Id] = seat++; }
            S.Trial = T;
            // elevator ride: everyone living descends to their stand (a real ride, not a teleport into a hidden scene)
            var court = S.Layout.Rooms.First(r => r.Type == RoomType.Courtroom);
            var stands = S.Layout.Furniture.Where(f => f.Type == "TrialStand").OrderBy(f => f.Variant).ToList();
            foreach (var a in S.Actors.Values)
            {
                if (a.IsButler) { a.Pos = new P3(-2, court.Rect.CX, court.Rect.z1 - 1.2f); a.Room = court.Id; a.Yaw = 180; continue; }
                if (!a.Alive) continue;
                if (a.Act != null) { a.Act = null; } a.Speed = 0; a.Pose = Pose.Stand; a.Following = null; a.TalkingTo = null;
                var st = stands.Count > T.Seat[a.Id] ? stands[T.Seat[a.Id]] : null;
                if (st != null) { a.Pos = st.Pos; a.Yaw = st.Yaw; } a.Room = court.Id;
            }
            foreach (var id in T.Participants.Where(x => x != Cast.Player)) { Testimony.UpdateSuspicion(sim, S.A(id)); T.Stances[id] = new TrialStance { Actor = id }; }
            sim.SetPhase(Phase.Trial);
            S.Log("TrialBegin", null, data: $"participants={T.Participants.Count} dead={string.Join(",", T.Dead)}");
            if (DebateEligible(sim, T, out var dinc)) { DebateBegin(sim, T, dinc); return; }   // the debate 심판 (Sim/Trial/Debate)
            // Yusti, from the judge's seat, runs only the procedure: opening, reminders of the chapter rule, the vote and the verdict
            Yusti(sim, T, "y_trial_open", "open");
            if (S.RuleActive("CH19")) Yusti(sim, T, "y_trial_rule", "rule", new Dictionary<string, string> { { "name", "시간차 공지" }, { "desc", "같은 공지가 두 번, 서로 다른 시각에 전해졌습니다. 언제 들으셨는지도 함께 말씀해 주십시오." } });
            if (S.RuleActive("CH21")) Yusti(sim, T, "y_trial_rule", "rule", new Dictionary<string, string> { { "name", "세 번째 빈자리" }, { "desc", "사건마다 책임은 따로 묻겠지만, 이번 심판에서 가려낼 범인은 한 명입니다." } });
            T.Mode = TrialMode.TM01_Debate; Mode(T, TrialMode.TM01_Debate, "첫 진술");
        }

        /// <summary>A procedural line from Yusti on the judge's seat (kind "system", so rounds and the UI treat it as procedure, not debate).</summary>
        static TrialBeat Yusti(Simulation sim, TrialState T, string lineKey, string key, Dictionary<string, string> slots = null)
            => Say(T, "system", Cast.Butler, sim.Render(Cast.Butler, null, lineKey, slots) ?? "…", key: key);

        internal static TrialBeat Say(TrialState T, string kind, string speaker, string text, string claimId = null, string data = null, Emotion emo = Emotion.Neutral, Anim gesture = Anim.Talk, string key = null)
        {
            var b = new TrialBeat { N = T.Beats.Count, Kind = kind, Speaker = speaker, Text = CaseBoard.Plain(text), ClaimId = claimId, Data = data, Mode = T.Mode, Emotion = emo, Gesture = gesture, Key = key };
            T.Beats.Add(b); return b;
        }

        internal static TrialBeat Line(TrialState T, Simulation sim, string speaker, string key, string listener, Dictionary<string, string> slots, TrialClaim claim = null, string kind = "line", Emotion emo = Emotion.Neutral, Anim gesture = Anim.Talk)
        {
            string text = sim.Render(speaker, listener, key, slots) ?? "…";
            var b = new TrialBeat { N = T.Beats.Count, Kind = kind, Speaker = speaker, Text = text, Key = key, ClaimId = claim?.Id, Mode = T.Mode, Emotion = emo, Gesture = gesture };
            T.Beats.Add(b);
            if (claim != null) claim.Text = text;
            sim.S.Log("TrialLine", speaker, listener, data: key + "|" + text);
            return b;
        }

        internal static void Mode(TrialState T, TrialMode m, string label)
        {
            T.Mode = m; if (!T.ModesSeen.Contains(m.ToString())) T.ModesSeen.Add(m.ToString());
            T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "mode", Mode = m, Text = label, Data = m.ToString() });
        }

        static TrialClaim Claim(TrialState T, Simulation sim, string speaker, Prop p, string key, Dictionary<string, string> slots, bool lie, string topic, string listener = null, Emotion emo = Emotion.Neutral)
        {
            var c = new TrialClaim { Id = "c" + (T.Claims.Count + 1), Speaker = speaker, Prop = p, Key = key, Lie = lie, Topic = topic, Beat = T.Beats.Count };
            T.Claims.Add(c);
            Line(T, sim, speaker, key, listener, slots, c, emo: emo, gesture: key == "accuse" ? Anim.Point : Anim.Talk);
            return c;
        }

        // ------------------------------------------------------------------ the debate director (non-linear)
        /// <summary>Produce beats until at least one new one exists (or the trial ends). UI calls this whenever it needs the next beat.</summary>
        public static TrialBeat Next(Simulation sim)
        {
            var S = sim.S; var T = S.Trial; if (T == null) return null;
            int guard = 0;
            while (T.Cursor >= T.Beats.Count && !T.Finished && T.PendingPrompt == null && guard++ < 10) Direct(sim, T);
            if (T.Cursor < T.Beats.Count) { var nb = T.Beats[T.Cursor++]; nb.Text = CaseBoard.Plain(nb.Text); return nb; }   // no rule codes on screen
            return null;
        }

        static void Direct(Simulation sim, TrialState T)
        {
            if (T.Debate != null) { T.Beat++; DebateDirect(sim, T); return; }
            var S = sim.S; var rng = S.R(Stream.Trial); T.Beat++;
            if (T.Stage == "Vote") { RunVote(sim, T); return; }
            if (T.Stage == "Done") { T.Finished = true; return; }
            var npcs = T.Participants.Where(x => x != Cast.Player).Select(S.A).Where(a => a != null && a.Alive).ToList();
            // hard budget: the trial does not drag on forever (beats spent inside minigame rounds don't count)
            if (T.Beats.Count - T.GameBeats > T.Limit && T.Stage != "Final") { StartFinal(sim, T); return; }
            // each topic opens with its header beat — emitted lazily so a minigame on the previous topic comes first
            if (Array.IndexOf(TopicStages, T.Stage) >= 0 && !T.TopicsDone.Contains(T.Stage)) { Topic(T, sim, T.Stage); return; }
            switch (T.Stage)
            {
                case "Opening":
                    {
                        var next = npcs.Where(a => !T.SpokeOpening.Contains(a.Id)).OrderBy(a => rng.F()).FirstOrDefault();
                        if (next == null || T.SpokeOpening.Count >= Math.Min(6, npcs.Count)) { T.Stage = "Cause"; TrialGames.AfterTopic(sim, T, "alibi"); return; }
                        T.SpokeOpening.Add(next.Id);
                        var w = Testimony.Where(sim, next, null);
                        if (w.Prop != null) { var c = Claim(T, sim, next.Id, w.Prop, w.Key == "alibi_with" ? "alibi_with" : "claim_alibi", w.Slots, w.Lie, "alibi"); NpcReactions(sim, T, c); }
                        else Line(T, sim, next.Id, "trial_first", null, null);
                        return;
                    }
                case "Cause":
                    {
                        // someone who examined the body states the cause; someone who didn't may guess wrong
                        var inc = TargetIncident(S);
                        if (inc == null) { T.Stage = "Culprit"; return; }
                        var examiner = npcs.Where(a => S.K(a.Id).Examined.Contains("body:" + inc.Victim)).OrderBy(a => rng.F()).FirstOrDefault();
                        var guesser = npcs.Where(a => !S.K(a.Id).Examined.Contains("body:" + inc.Victim) && a.Def.P.Honesty < 0.8f).OrderBy(a => rng.F()).FirstOrDefault();
                        if (guesser != null && rng.Chance(0.55))
                        {
                            var wrong = inc.Dmg == DamageType.Blunt ? DamageType.Stab : DamageType.Blunt;
                            var p = new Prop { Kind = PropKind.WeaponType, A = inc.Victim, Value = wrong.ToString() };
                            var c = Claim(T, sim, guesser.Id, p, "claim_theory", new Dictionary<string, string> { { "t", "범인" }, { "place", S.RoomName(inc.FoundRoom) }, { "item", WeaponWord(wrong) } }, false, "cause");
                            NpcReactions(sim, T, c);
                        }
                        if (examiner != null)
                        {
                            var ev = S.K(examiner.Id).Evidence.FirstOrDefault(e => e.Subject == inc.Victim && e.Kind == EvKind.Body);
                            var wp = ev?.Props.FirstOrDefault(p => p.Kind == PropKind.WeaponType);
                            if (wp != null) { var c = Claim(T, sim, examiner.Id, wp.Clone(), "claim_theory", new Dictionary<string, string> { { "t", "범인" }, { "place", S.RoomName(inc.FoundRoom) }, { "item", WeaponWord(P(wp.Value)) } }, false, "cause"); c.Status = "supported"; }
                        }
                        ImpressionSwap(sim, T, npcs, inc, rng);
                        ImpressionMethods(sim, T, npcs, inc, rng, "cause");   // staged accident / natural death (TrialMethods.cs)
                        T.Stage = "Time"; TrialGames.AfterTopic(sim, T, "cause"); return;
                    }
                case "Time":
                    {
                        var inc = TargetIncident(S); if (inc == null) { T.Stage = "Culprit"; return; }
                        // someone claims they saw the victim alive late / heard a scream
                        foreach (var a in npcs.OrderBy(_ => rng.F()))
                        {
                            var k = S.K(a.Id);
                            var alive = k.Sightings.Where(s => s.Target == inc.Victim && !s.Dead && s.IdConf > 0.5f).OrderByDescending(s => s.T1).FirstOrDefault();
                            if (alive != null && S.Clock - alive.T1 < 600)
                            {
                                var p = new Prop { Kind = PropKind.AliveAt, A = inc.Victim, Room = alive.Room, T0 = alive.T1, T1 = alive.T1, Value = "root:" + alive.Root };
                                var c = Claim(T, sim, a.Id, p, "claim_saw", new Dictionary<string, string> { { "t", "@" + inc.Victim }, { "place", S.RoomName(alive.Room) }, { "time", ClockFmt.Vague(alive.T1) } }, false, "time");
                                NpcReactions(sim, T, c); break;
                            }
                        }
                        var hearer = npcs.Select(a => (a, h: Testimony.Heard(sim, a, null))).FirstOrDefault(x => x.h.Prop != null);
                        if (hearer.a != null) { var c = Claim(T, sim, hearer.a.Id, hearer.h.Prop, "claim_heard", hearer.h.Slots, false, "time"); NpcReactions(sim, T, c); }
                        ImpressionTod(sim, T, npcs, inc, rng);
                        T.Stage = "Place"; TrialGames.AfterTopic(sim, T, "time"); return;
                    }
                case "Place":
                    {
                        var inc = TargetIncident(S); if (inc == null) { T.Stage = "Culprit"; return; }
                        var a = npcs.OrderBy(_ => rng.F()).First();
                        var p = new Prop { Kind = PropKind.DeathPlace, A = inc.Victim, Room = inc.FoundRoom };
                        var c = ClaimText(T, sim, a.Id, p, Polite(a.Id) ? $"{Cast.GivenOf(inc.Victim)} 씨는 발견된 그 자리, {S.RoomName(inc.FoundRoom)}에서 숨진 거예요." : $"{J(Cast.GivenOf(inc.Victim), "는")} 발견된 그 자리, {S.RoomName(inc.FoundRoom)}에서 죽은 거야.", "place");
                        NpcReactions(sim, T, c);
                        // a locked room claim if the scene door was locked on discovery
                        var room = S.Layout.Room(inc.FoundRoom);
                        if (ImpressionSeal(sim, T, npcs, inc, rng)) { }
                        else if (room != null && room.Doors.Any(d => S.Layout.Doors[d].Locked))
                        {
                            var b = npcs.Where(x => x != a).OrderBy(_ => rng.F()).FirstOrDefault();
                            if (b != null) { var dc = ClaimText(T, sim, b.Id, new Prop { Kind = PropKind.DoorState, Room = room.Id, Value = "locked", T0 = inc.DiscoverClock, T1 = inc.DiscoverClock }, Polite(b.Id) ? $"{room.Name} 문은 잠겨 있었어요. 밀실이었다고요." : $"{room.Name} 문은 잠겨 있었어. 밀실이었다고.", "place"); NpcReactions(sim, T, dc); }
                        }
                        ImpressionMethods(sim, T, npcs, inc, rng, "place");   // a forged farewell note (TrialMethods.cs)
                        T.Stage = "Suspicious"; TrialGames.AfterTopic(sim, T, "place"); return;
                    }
                case "Suspicious":
                    {
                        int said = 0;
                        foreach (var a in npcs.OrderBy(_ => rng.F()))
                        {
                            foreach (var s in Testimony.Saw(sim, a, null, 1))
                            {
                                if (s.Prop == null || T.Claims.Any(c => c.Speaker == a.Id && c.Prop != null && c.Prop.Kind == s.Prop.Kind && c.Prop.A == s.Prop.A)) continue;
                                var c = Claim(T, sim, a.Id, s.Prop, s.Key == "saw_item" ? "claim_saw" : s.Key == "saw_person_unsure" ? "claim_saw" : "claim_saw", WithTime(s.Slots.ContainsKey("t") ? s.Slots : Merge(s.Slots, "t", "@" + (s.Prop.A ?? a.Id)), s.Prop), s.Lie, "suspicious");
                                if (s.Prop.Kind == PropKind.Disguised || s.Prop.Item == "unsure") { Mode(T, TrialMode.TM03_Witness, "집중 심문: " + Cast.GivenOf(a.Id)); T.PendingPrompt = "witness:" + c.Id; }
                                NpcReactions(sim, T, c); said++;
                            }
                            if (said >= 3) break;
                        }
                        { var incM = TargetIncident(S); if (incM != null) ImpressionMessage(sim, T, npcs, incM, rng); }
                        T.Stage = "Culprit"; TrialGames.AfterTopic(sim, T, "suspicious"); return;
                    }
                case "Culprit":
                    {
                        // accusations from those with a clear suspect; the accused defends; others take sides
                        var acc = npcs.Select(a => { Testimony.UpdateSuspicion(sim, a); var k = S.K(a.Id); var top = k.Suspicion.Where(kv => T.Participants.Contains(kv.Key) && kv.Key != a.Id).OrderByDescending(kv => kv.Value).FirstOrDefault(); return (a, top); })
                            .Where(x => x.top.Key != null && x.top.Value > 0.45f).OrderByDescending(x => x.top.Value).ToList();
                        // culprits push their scapegoat
                        foreach (var a in npcs.Where(a => IsCulprit(S, a.Id)))
                        {
                            var sc = Testimony.Scapegoat(sim, a);
                            if (sc != null && T.Participants.Contains(sc) && !acc.Any(x => x.a == a)) acc.Add((a, new KeyValuePair<string, float>(sc, 0.6f)));
                        }
                        int said0 = T.Claims.Count(c => c.Topic == "culprit" && c.Accused != null && c.Speaker != Cast.Player);
                        var fresh = said0 >= 4 ? new List<(Actor a, KeyValuePair<string, float> top)>() : acc.Where(x => !T.Claims.Any(c => c.Speaker == x.a.Id && c.Accused == x.top.Key)).Take(2).ToList();
                        if (fresh.Count == 0) { StartFinal(sim, T); return; }
                        foreach (var (a, top) in fresh)
                        {
                            var p = new Prop { Kind = PropKind.Culprit, A = top.Key, B = TargetIncident(S)?.Victim };
                            var c = Claim(T, sim, a.Id, p, top.Key == Cast.Player ? "accuse_player" : "accuse", new Dictionary<string, string> { { "t", "@" + top.Key } }, IsCulprit(S, a.Id), "culprit", top.Key == Cast.Player ? Cast.Player : null, Emotion.Angry);
                            c.Accused = top.Key; c.Premises = PremisesFor(sim, a, top.Key);
                            if (c.Premises.Count >= 2) { Mode(T, TrialMode.TM04_Chain, Cast.GivenOf(a.Id) + "의 논리"); T.PendingPrompt = "chain:" + c.Id; }
                            T.Stances[a.Id].Accusing = top.Key; T.Stances[a.Id].Confidence = top.Value;
                            var accused = S.A(top.Key);
                            if (S.RuleActive("CH18")) { Yusti(sim, T, "y_trial_rule", "rule", new Dictionary<string, string> { { "name", "반론 우선권" }, { "desc", Cast.NameOf(top.Key) + " 님의 반론을 먼저 듣겠습니다." } }); if (accused != null && accused.IsPlayer) T.PendingPrompt = "rebut"; }
                            if (accused != null && !accused.IsPlayer) { var w = Testimony.Where(sim, accused, a.Id); var dc = Claim(T, sim, accused.Id, w.Prop, "defend_self", w.Slots, w.Lie, "culprit", a.Id, Emotion.Surprised); NpcReactions(sim, T, dc); }
                            Sides(sim, T, c);
                        }
                        // competing theories → TM05
                        var theories = T.Claims.Where(c => c.Accused != null && c.Status != "refuted" && c.Status != "retracted").GroupBy(c => c.Accused).Where(g => g.Count() + g.Sum(x => x.Supporters.Count) >= 2).ToList();
                        if (theories.Count >= 2 && !T.ModesSeen.Contains(TrialMode.TM05_Theory.ToString())) { Mode(T, TrialMode.TM05_Theory, "가설 대립 — " + string.Join(" 대 ", theories.Select(g => Cast.GivenOf(g.Key)))); T.PendingPrompt = "theory"; }
                        Reveals(sim, T);
                        TrialGames.AfterTopic(sim, T, "culprit");
                        return;
                    }
                case "Final":
                    {
                        if (T.PendingPrompt != null) return;   // waiting for the player (accusation / showdown / closing argument)
                        bool pl = PlayerIn(S, T);
                        switch (T.FinalStep ?? "accuse")
                        {
                            case "accuse":
                                T.FinalStep = "defense";
                                if (pl && T.PlayerAccused == null)
                                {
                                    Yusti(sim, T, "y_accuse_call", "accuse_call");
                                    T.PendingPrompt = "accuse";
                                }
                                return;
                            case "defense":
                                T.FinalStep = "panic";
                                if (T.DefenseDone == null && T.Accused != null) FinalDefense(sim, T);
                                return;
                            case "panic":
                                T.FinalStep = "closing";
                                TrialGames.StartFinalBoard(sim, T);
                                return;
                            case "closing":
                                T.FinalStep = "vote";
                                Mode(T, TrialMode.TM07_Reconstruct, "사건을 처음부터 되짚는다");
                                BuildReconstruction(sim, T);
                                if (pl) TrialGames.StartReconstruct(sim, T); else T.ReconstructScore = 0;
                                return;
                        }
                        T.Stage = "Vote"; Yusti(sim, T, "y_vote_call", "vote_call"); Mode(T, TrialMode.Vote, "투표");
                        return;
                    }
            }
        }

        static readonly string[] TopicStages = { "Cause", "Time", "Place", "Suspicious", "Culprit" };

        internal static bool PlayerIn(GameState S, TrialState T) => T.Participants.Contains(Cast.Player) && S.A(Cast.Player)?.Alive == true;

        static Dictionary<string, string> Merge(Dictionary<string, string> d, string k, string v) { var n = new Dictionary<string, string>(d); n[k] = v; return n; }
        static Dictionary<string, string> WithTime(Dictionary<string, string> d, Prop p) { if (p == null || (d.TryGetValue("time", out var t) && !string.IsNullOrEmpty(t)) || p.T0 <= 0) return d; return Merge(d, "time", ClockFmt.Vague(p.T0)); }

        static void Topic(TrialState T, Simulation sim, string topic)
        {
            T.Topic = topic; T.TopicsDone.Add(topic);
            string label = topic == "Cause" ? "논점: 사인과 흉기" : topic == "Time" ? "논점: 숨진 시각" : topic == "Place" ? "논점: 숨진 곳" : topic == "Suspicious" ? "논점: 수상한 행동" : topic == "Culprit" ? "논점: 범인은 누구인가" : topic;
            T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "topic", Text = label, Data = topic, Mode = T.Mode });
            if (T.Mode != TrialMode.TM01_Debate) Mode(T, TrialMode.TM01_Debate, "자유 논의");
        }

        // a noun (the templates say "{item:을} 썼다면"), so the theory reads "범인이 로비에서 둔기를 썼다면 설명이 돼요"
        internal static string WeaponWord(DamageType d) => d == DamageType.Stab ? "날붙이" : d == DamageType.Cut ? "칼날" : d == DamageType.Blunt ? "둔기" : d == DamageType.Crush ? "무거운 물건" : d == DamageType.Drown ? "물" : d == DamageType.Choke ? "끈" : "흉기";
        internal static string J(string w, string p) => w + LineBank.Josa(w, p);
        static bool Polite(string id) => Cast.Get(id)?.Speech.PoliteDefault ?? true;

        /// <summary>A claim whose sentence has no fitting line template (e.g. "X died where they were found"): plain text in the speaker's register.</summary>
        static TrialClaim ClaimText(TrialState T, Simulation sim, string speaker, Prop p, string text, string topic, Emotion emo = Emotion.Neutral)
        {
            var c = new TrialClaim { Id = "c" + (T.Claims.Count + 1), Speaker = speaker, Prop = p, Key = "claim_text", Topic = topic, Beat = T.Beats.Count, Text = text };
            T.Claims.Add(c);
            Say(T, "line", speaker, text, c.Id, emo: emo);
            sim.S.Log("TrialLine", speaker, null, data: "claim_text|" + text);
            return c;
        }
        internal static DamageType P(string s) { Enum.TryParse(s ?? "None", out DamageType d); return d; }

        public static Incident TargetIncident(GameState S)
        {
            if (S.Ch.TargetIncident != null && S.Incidents.TryGetValue(S.Ch.TargetIncident, out var i)) return i;
            return S.Incidents.Values.Where(x => x.Loop == S.Loop && x.Chapter == S.Chapter && x.Confirmed).OrderBy(x => x.ResultSeq).FirstOrDefault();
        }

        internal static bool IsCulprit(GameState S, string id) => S.Incidents.Values.Any(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Culprit == id);

        static List<string> PremisesFor(Simulation sim, Actor a, string target)
        {
            var S = sim.S; var T = S.Trial; var res = new List<string>(); var k = S.K(a.Id);
            var (t0, t1, room) = Testimony.CaseWindow(S);
            foreach (var s in k.Sightings.Where(s => s.Target == target && s.T1 >= t0 - 20 && s.T0 <= t1 && s.IdConf > 0.4f).OrderByDescending(s => (s.Bloody ? 3 : 0) + (s.Held != null ? 2 : 0) + (s.Room == room ? 1 : 0)).Take(2))
            {
                var p = s.Held != null ? new Prop { Kind = PropKind.Held, A = target, Item = s.Held, Room = s.Room, T0 = s.T0, T1 = s.T1 } : new Prop { Kind = PropKind.AtPlace, A = target, Room = s.Room, T0 = s.T0, T1 = s.T1 };
                var c = new TrialClaim { Id = "c" + (T.Claims.Count + 1), Speaker = a.Id, Prop = p, Key = "claim_saw", Topic = "premise", Beat = T.Beats.Count };
                c.Text = sim.Render(a.Id, null, s.Held != null ? "saw_item" : "saw_person", new Dictionary<string, string> { { "t", "@" + target }, { "place", S.RoomName(s.Room) }, { "time", ClockFmt.Vague(s.T0) }, { "item", ItemCatalog.Get(s.Held)?.Kor ?? "" } });
                T.Claims.Add(c); res.Add(c.Id);
            }
            if (k.Facts.Any(f => f.StartsWith("caught-lie:" + target))) { var c = new TrialClaim { Id = "c" + (T.Claims.Count + 1), Speaker = a.Id, Prop = new Prop { Kind = PropKind.Lie, A = target }, Key = "counter", Topic = "premise", Text = Cast.GivenOf(target) + "의 말은 내가 본 것과 달라.", Beat = T.Beats.Count }; T.Claims.Add(c); res.Add(c.Id); }
            if (IsCulprit(S, a.Id) && res.Count == 0)
            {
                var (tt0, tt1, rr) = Testimony.CaseWindow(S);
                var c = new TrialClaim { Id = "c" + (T.Claims.Count + 1), Speaker = a.Id, Prop = new Prop { Kind = PropKind.AtPlace, A = target, Room = rr, T0 = tt1 - 45, T1 = tt1 - 35 }, Key = "claim_saw", Topic = "premise", Lie = true, Beat = T.Beats.Count };
                c.Text = sim.Render(a.Id, null, "saw_person", new Dictionary<string, string> { { "t", "@" + target }, { "place", S.RoomName(rr) }, { "time", ClockFmt.Vague(tt1 - 40) } }); T.Claims.Add(c); res.Add(c.Id);
            }
            return res;
        }

        /// <summary>After a claim, other NPCs who hold contradicting evidence may object (if their strategy allows).</summary>
        static void NpcReactions(Simulation sim, TrialState T, TrialClaim c)
        {
            var S = sim.S; var rng = S.R(Stream.Trial);
            if (c.Prop == null) return;
            foreach (var id in T.Participants.Where(x => x != Cast.Player && x != c.Speaker).OrderBy(_ => rng.F()))
            {
                var a = S.A(id); var k = S.K(id);
                var evid = k.Evidence.Concat(SightingsAsEvidence(sim, a));
                var (v, ev) = Logic.Best(S, c.Prop, evid);
                if (v == null) continue;
                bool protects = S.R(id, c.Speaker).Attach > 0.55f || S.R(id, c.Speaker).Tags.Contains("family") || S.R(id, c.Speaker).Tags.Contains("lover");
                bool culprit = IsCulprit(S, id);
                if (v.Result == LogicResult.Contradict || v.Result == LogicResult.Conditional || v.Result == LogicResult.LimitScope)
                {
                    if (protects && rng.Chance(0.7)) { S.Log("TrialWithhold", id, c.Speaker, data: "protect", secret: true); if (!T.Withheld.Any(w => w.StartsWith(id + "|" + c.Speaker + "|"))) T.Withheld.Add(id + "|" + c.Speaker + "|" + c.Id); continue; }
                    if (culprit && !(c.Accused == id)) continue; // the culprit won't help clarify unless defending self
                    if (a.Def.P.Fearfulness > 0.7f && S.R(id, c.Speaker).Fear > 0.3f && rng.Chance(0.6)) continue;
                    Line(T, sim, id, "object", c.Speaker, new Dictionary<string, string> { { "t", "@" + c.Speaker } }, emo: Emotion.Angry, gesture: Anim.Point);
                    T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "result", Speaker = id, Text = v.Why, ClaimId = c.Id, Data = v.Result.ToString() });
                    ApplyVerdict(sim, T, c, v, id, ev);
                    return;
                }
                if (v.Result == LogicResult.Support && rng.Chance(0.35) && S.R(id, c.Speaker).Opinion > 0)
                {
                    Line(T, sim, id, "agree", c.Speaker, new Dictionary<string, string> { { "t", "@" + c.Speaker } });
                    if (!c.Supporters.Contains(id)) c.Supporters.Add(id);
                    if (c.Status == "open") c.Status = "supported";
                    return;
                }
            }
        }

        static IEnumerable<Evidence> SightingsAsEvidence(Simulation sim, Actor a)
        {
            var S = sim.S; var k = S.K(a.Id); var (t0, t1, room) = Testimony.CaseWindow(S);
            foreach (var s in k.Sightings.Where(s => s.T1 >= t0 - 30 && s.T0 <= t1 + 5 && s.IdConf > 0.55f && !s.Dead))
                yield return new Evidence { Id = "mem:" + s.Root, Owner = a.Id, Kind = EvKind.Sighting, Direct = true, Root = s.Root, Title = "기억", Props = { new Prop { Kind = PropKind.AtPlace, A = s.Target, Room = s.Room, T0 = s.T0, T1 = s.T1 } } };
        }

        /// <summary>Fallback switch (clues v2-lite): publish every prop of the player's non-sheet cards (still keyed per prop)
        /// instead of only the decisive ones. Off unless the mystery gate needs it.</summary>
        internal static bool PublishAllPlayerProps = false;

        static bool IsPlayerCard(GameState S, Evidence ev) => ev != null && ev.Owner == Cast.Player && ev.Id != null && !ev.Id.StartsWith("claim:", StringComparison.Ordinal) && S.K(Cast.Player).Evidence.Contains(ev);

        /// <summary>What a card makes public. An NPC card (or a court statement) publishes everything under its root, as always;
        /// a player's card publishes only the props that bear on the claim(s) it was used against — keyed per prop, so a bullet
        /// a witness adds later can still be published by a later presentation.</summary>
        static void Publish(Simulation sim, TrialState T, Evidence ev, IList<TrialClaim> against)
        {
            var S = sim.S; if (ev == null) return;
            if (!IsPlayerCard(S, ev) || against == null)
            {
                if (!T.PublicRoots.Contains(ev.Root)) { T.PublicRoots.Add(ev.Root); T.Public.AddRange(ev.Props.Select(p => p.Clone())); }
                return;
            }
            bool sheet = ev.Kind == EvKind.Testimony && ev.Root != null && ev.Root.StartsWith("wit:", StringComparison.Ordinal);
            foreach (var p in ev.Props)
            {
                bool decisive = (PublishAllPlayerProps && !sheet) || against.Any(c => c?.Prop != null && Logic.Check(S, c.Prop, p, ev.Direct, ev.Root).Result != LogicResult.Irrelevant);
                if (!decisive) continue;
                string key = ev.Root + "#" + CaseBoard.FactKey(p);
                if (T.PublicRoots.Contains(key)) continue;
                T.PublicRoots.Add(key); T.Public.Add(p.Clone());
            }
        }

        internal static void ApplyVerdict(Simulation sim, TrialState T, TrialClaim c, LogicVerdict v, string by, Evidence ev, bool legacyPublish = false)
        {
            var S = sim.S; var speaker = S.A(c.Speaker);
            Publish(sim, T, ev, legacyPublish ? null : new[] { c });
            c.RefutedBy = by; c.RefuteWhy = CaseBoard.Plain(v.Why);
            switch (v.Result)
            {
                case LogicResult.Contradict:
                    c.Status = "refuted";
                    if (speaker != null && !speaker.IsPlayer)
                    {
                        // the speaker corrects, retracts, or panics — a lie caught is public knowledge, not proof of murder
                        if (c.Lie) { Line(T, sim, speaker.Id, speaker.Def.Composure > 80 ? "counter" : "panic", by, new Dictionary<string, string> { { "t", "@" + by } }, emo: Emotion.Fear, gesture: Anim.Cower); T.Public.Add(new Prop { Kind = PropKind.Lie, A = speaker.Id, Value = c.Id }); if (!T.ModesSeen.Contains(TrialMode.TM02_Crossfire.ToString())) { Mode(T, TrialMode.TM02_Crossfire, "교차 논쟁"); } }
                        else Line(T, sim, speaker.Id, "retract", by, null, emo: Emotion.Sad);
                        Relations.Change(S, speaker.Id, by, like: -0.05f, grudge: c.Lie ? 0.15f : 0.03f, memory: "심판에서 모두가 보는 앞에서 반박당했다");
                    }
                    foreach (var id in T.Participants.Where(x => x != Cast.Player)) { var k = S.K(id); if (c.Lie) k.Facts.Add($"caught-lie:{c.Speaker}:{c.Id}"); }
                    // those who had backed the statement reconsider out loud (some stand by it)
                    foreach (var sup in c.Supporters.Where(x => x != Cast.Player && x != by && S.A(x)?.Alive == true).ToList())
                    {
                        if (!S.R(Stream.Trial).Chance(0.6)) continue;
                        Say(T, "line", sup, Polite(sup) ? "…저도 다시 생각해 볼게요. 방금 그 말에 맞장구친 건 거둘게요." : "…나도 다시 생각해 볼게. 방금 그 말에 맞장구친 거, 취소할게.", c.Id, emo: Emotion.Sad, gesture: Anim.Shrug, key: "recant");
                        c.Supporters.Remove(sup); if (T.Stances.TryGetValue(sup, out var st0) && st0.Supporting == c.Accused) st0.Supporting = null;
                    }
                    break;
                case LogicResult.Conditional: c.Status = "conditional"; break;
                case LogicResult.LimitScope: c.Status = "limited"; if (speaker != null && !speaker.IsPlayer) Line(T, sim, speaker.Id, "concede", by, null); break;
                case LogicResult.Support: c.Status = "supported"; break;
            }
            // everyone hears the argument: public props feed their suspicion models
            foreach (var id in T.Participants.Where(x => x != Cast.Player)) { foreach (var p in T.Public) if (!S.K(id).Statements.Any(s => s.Root == "trial:" + p.Kind + p.A + p.Value)) S.K(id).Statements.Add(new Statement { Id = S.NewId("st"), Speaker = by, Listener = id, Clock = S.Clock, Prop = p.Clone(), Root = "trial:" + p.Kind + p.A + p.Value }); Testimony.UpdateSuspicion(sim, S.A(id)); }
        }

        static void Sides(Simulation sim, TrialState T, TrialClaim acc)
        {
            var S = sim.S; var rng = S.R(Stream.Trial);
            foreach (var id in T.Participants.Where(x => x != Cast.Player && x != acc.Speaker && x != acc.Accused).OrderBy(_ => rng.F()).Take(3))
            {
                var r1 = S.R(id, acc.Speaker); var r2 = S.R(id, acc.Accused);
                var k = S.K(id); float sus = k.Suspicion.TryGetValue(acc.Accused, out var s) ? s : 0;
                double support = sus + r1.Opinion * 0.4 - r2.Opinion * 0.5 - (r2.Tags.Contains("family") || r2.Tags.Contains("lover") ? 1 : 0) + (IsCulprit(S, id) && acc.Accused != id ? 0.6 : 0);
                if (support > 0.45) { Line(T, sim, id, "agree", acc.Speaker, new Dictionary<string, string> { { "t", "@" + acc.Speaker } }); acc.Supporters.Add(id); T.Stances[id].Supporting = acc.Accused; }
                else if (support < -0.2) Line(T, sim, id, "defend_other", acc.Speaker, new Dictionary<string, string> { { "t", "@" + acc.Accused } });
            }
        }

        static void StartFinal(Simulation sim, TrialState T)
        {
            if (T.Stage == "Final") return;
            T.Stage = "Final"; T.FinalStep = "accuse";
            // the leading accusation (or the player's); the final phase then runs accuse → defense → panic talk → closing argument → vote
            string lead = T.PlayerAccused ?? T.Claims.Where(c => c.Accused != null && c.Status != "refuted" && c.Status != "retracted").GroupBy(c => c.Accused).OrderByDescending(g => g.Count() + g.Sum(c => c.Supporters.Count)).Select(g => g.Key).FirstOrDefault();
            T.Accused = lead;
        }

        static void FinalDefense(Simulation sim, TrialState T)
        {
            var S = sim.S; T.DefenseDone = T.Accused ?? "-";
            var a = S.A(T.Accused); if (a == null || !a.Alive) return;
            Mode(T, TrialMode.TM08_FinalDefense, "최종 변론: " + Cast.GivenOf(a.Id));
            if (a.IsPlayer) { if (!TrialGames.StartDefenseBoard(sim, T)) T.PendingPrompt = "defense"; return; }
            Line(T, sim, a.Id, "final_defense", null, null, emo: Emotion.Fear);
            // an innocent accused may bring an independent alibi (valid rebuttals are adopted)
            var k = S.K(a.Id); var (t0, t1, room) = Testimony.CaseWindow(S);
            var witness = T.Participants.Where(x => x != a.Id && x != Cast.Player).Select(S.A).FirstOrDefault(w => S.K(w.Id).Sightings.Any(s => s.Target == a.Id && s.IdConf > 0.6f && s.Room != room && s.T0 <= t1 && s.T1 >= t1 - 40));
            if (witness != null && !IsCulprit(S, a.Id))
            {
                var s = S.K(witness.Id).Sightings.First(x => x.Target == a.Id && x.IdConf > 0.6f && x.Room != room && x.T0 <= t1 && x.T1 >= t1 - 40);
                var c = Claim(T, sim, witness.Id, new Prop { Kind = PropKind.AtPlace, A = a.Id, Room = s.Room, T0 = s.T0, T1 = s.T1, Value = "window-cover" }, "claim_saw", new Dictionary<string, string> { { "t", "@" + a.Id }, { "place", S.RoomName(s.Room) }, { "time", ClockFmt.Vague(s.T0) } }, false, "defense");
                c.Status = "supported";
                foreach (var id in T.Participants.Where(x => x != Cast.Player)) { var kk = S.K(id); if (kk.Suspicion.ContainsKey(a.Id)) kk.Suspicion[a.Id] *= 0.5f; }
                T.Public.Add(c.Prop.Clone());
            }
        }

        // ------------------------------------------------------------------ TM07 reconstruction (player)
        [Serializable] public sealed class RQ { public string Id; public string Question; public List<string> Options = new List<string>(); public int Answer = -1; public int Chosen = -1; }
        public static List<RQ> Questions(Simulation sim) => sim.S.Trial?.RQ ?? new List<RQ>();

        static void BuildReconstruction(Simulation sim, TrialState T)
        {
            var S = sim.S; var inc = TargetIncident(S); var _rq = T.RQ = new List<RQ>(); var rng = S.R(Stream.Trial);
            if (inc == null) return;
            var parts = T.Participants.Concat(T.Dead).Distinct().ToList();
            // options are built from what's publicly known + noise; the "answer" index is only used for scoring after the vote
            var q1 = new RQ { Id = "who", Question = "범인은 누구인가?" }; foreach (var p in parts.Where(p => p != inc.Victim)) q1.Options.Add(p); q1.Answer = q1.Options.IndexOf(inc.Culprit ?? "");
            var q2 = new RQ { Id = "weapon", Question = "시신에 남은 흔적은?" }; foreach (var d in new[] { DamageType.Stab, DamageType.Cut, DamageType.Blunt, DamageType.Crush, DamageType.Drown, DamageType.Choke, DamageType.Fall, DamageType.Shock, DamageType.None }) q2.Options.Add(d.ToString()); q2.Answer = q2.Options.IndexOf(inc.Dmg.ToString());
            // the whole floor plan is the answer space for "where" (the player places the figures on the map)
            var q3 = new RQ { Id = "where", Question = "실제로 공격이 벌어진 곳은?" }; foreach (var r in S.Layout.Rooms.Where(r => r.Type != RoomType.Courtroom && r.Type != RoomType.Elevator).OrderBy(r => r.Floor).ThenBy(r => r.Id)) q3.Options.Add(r.Id.ToString()); q3.Answer = q3.Options.IndexOf(inc.CauseRoom.ToString());
            var q4 = new RQ { Id = "moved", Question = "시신은 옮겨졌나?", Options = { "옮겨지지 않았다", "옮겨졌다" } }; q4.Answer = inc.BodyMoved || Methods.Dismembered(S, inc.Victim) ? 1 : 0;
            var q5 = new RQ { Id = "conceal", Question = "범인은 흉기를 어떻게 처리했나?", Options = { "현장에 두고 갔다", "씻었다", "다른 곳에 숨겼다", "모르겠다", "소각로에 태웠다", "물속에 버렸다", "흙 속에 묻었다" } };
            var wLog = S.Ledger.Where(e => e.Actor == inc.Culprit && (e.Type == "Wash" || e.Type == "HideItem") && e.Clock >= inc.DeathClock).Select(e => e.Type).FirstOrDefault();
            q5.Answer = wLog == "Wash" ? 1 : wLog == "HideItem" ? 2 : 0;
            { int mc = MethodConcealAnswer(S, inc, q5.Options); if (mc >= 0) q5.Answer = mc; }   // the incinerator, the pool, the soil (TrialMethods.cs)
            // the clock: ten-minute marks across the case window (the hands snap to them)
            var (w0, w1, _) = Testimony.CaseWindow(S);
            var q6 = new RQ { Id = "when", Question = "공격이 벌어진 시각은?" };
            for (double t = Math.Floor(w0 / 10) * 10; t <= w1 + 0.1; t += 10) q6.Options.Add(t.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
            double cause = inc.CauseClock >= 0 ? inc.CauseClock : inc.DeathClock;
            q6.Answer = q6.Options.Count == 0 ? -1 : Enumerable.Range(0, q6.Options.Count).OrderBy(i => Math.Abs(double.Parse(q6.Options[i], System.Globalization.CultureInfo.InvariantCulture) - cause)).First();
            // how the scene was staged (the plan's signature trick is the hidden truth; "none" is a real answer too)
            var q7 = new RQ { Id = "trick", Question = "범인이 꾸민 속임수는?" }; q7.Options.AddRange(TrickOptions);
            q7.Answer = Array.IndexOf(TrickCodes, TrickOf(S, inc));
            _rq.AddRange(new[] { q1, q6, q3, q2, q4, q7, q5 });
            T.ReconstructMax = _rq.Count;
        }

        public static readonly string[] TrickOptions = { "아무것도 꾸미지 않았다", "밖에서 잠가 만든 밀실", "숨진 시각 속이기", "가짜 피 글씨", "흉기 바꿔치기", "사고로 꾸미기", "자연사로 꾸미기", "자살로 꾸미기(가짜 유서)", "열쇠를 되돌려 놓은 밀실", "미리 타 둔 독" };
        static readonly string[] TrickCodes = { null, "Seal", "Tod", "Message", "Swap", "Accident", "Natural", "Suicide", "KeySlide", "Delayed" };

        /// <summary>The signature trick of an incident's plan (hidden truth — scoring and reveal only).</summary>
        internal static string TrickOf(GameState S, Incident inc)
        {
            var g = inc?.PlanId != null && S.Plans.TryGetValue(inc.PlanId, out var p) ? p.Grammar ?? "" : "";
            return ExecutedTrick(S, inc);   // what was actually done (ledger), not what was planned — see TrialMethods.cs
        }

        public static List<RQ> ReconstructionQuestions(GameState S) => S?.Trial?.RQ ?? new List<RQ>();

        public static void PlayerReconstruct(Simulation sim, Dictionary<string, int> answers)
        {
            var S = sim.S; var T = S.Trial; if (T == null) return;
            int score = 0; foreach (var q in T.RQ) if (answers.TryGetValue(q.Id, out var a)) { q.Chosen = a; if (a == q.Answer) score++; }
            T.ReconstructScore = score;
            var who = T.RQ.FirstOrDefault(q => q.Id == "who");
            if (who != null && who.Chosen >= 0) { T.PlayerAccused = who.Options[who.Chosen]; T.Accused = T.PlayerAccused; }
            // the reconstruction is a public explanation: its persuasiveness depends on how well it fits PUBLIC facts (not the truth directly)
            float fit = PublicFit(sim, T, T.Accused);
            foreach (var id in T.Participants.Where(x => x != Cast.Player && T.Accused != null))
            {
                var k = S.K(id); float trust = MathX.Clamp01(0.3f + S.R(id, Cast.Player).Trust + S.R(id, Cast.Player).Respect);
                k.Suspicion[T.Accused] = (k.Suspicion.TryGetValue(T.Accused, out var v) ? v : 0) + fit * (0.4f + T.Influence * 0.6f) * (0.5f + trust * 0.5f);
            }
            T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "result", Speaker = Cast.Player, Text = $"재구성을 내놓았다 — 밝혀진 사실과 {(int)(fit * 100)}% 맞아떨어진다", Data = "reconstruct" });
            T.PendingPrompt = null;
        }

        internal static float PublicFit(Simulation sim, TrialState T, string accused)
        {
            if (accused == null) return 0;
            var S = sim.S; float f = 0.3f;
            foreach (var p in T.Public)
            {
                if (p.Kind == PropKind.Lie && p.A == accused) f += 0.15f;
                if ((p.Kind == PropKind.Held || p.Kind == PropKind.Bloodied) && p.A == accused) f += 0.2f;
                if (p.Kind == PropKind.AtPlace && p.A == accused && p.Value == "window-cover") f -= 0.5f;
            }
            foreach (var c in T.Claims.Where(c => c.Accused == accused && c.Status == "supported")) f += 0.1f;
            return MathX.Clamp01(f);
        }

        // ------------------------------------------------------------------ player actions
        public sealed class Result { public LogicResult R; public string Text; public bool Break; public bool Valid; }

        public static Result PlayerContradict(Simulation sim, string claimId, string evidenceId)
        {
            if (sim.S.Trial?.Debate != null) return DebateShow(sim, claimId, evidenceId);
            var S = sim.S; var T = S.Trial; var c = T?.Claims.FirstOrDefault(x => x.Id == claimId); var ev = TrialGames.BulletEvidence(S, evidenceId);
            if (c == null || ev == null) return new Result { R = LogicResult.Irrelevant, Text = "잘못 고른 것 같다" };
            LogicVerdict best = null; foreach (var p in ev.Props) { var v = Logic.Check(S, c.Prop, p, ev.Direct, ev.Root); if (best == null || (int)Rank(v.Result) > (int)Rank(best.Result)) best = v; }
            best = best ?? new LogicVerdict { Result = LogicResult.Irrelevant, Why = "이 단서는 그 주장과 직접 관련이 없다" };
            Line(T, sim, Cast.Player, "p_object", c.Speaker, new Dictionary<string, string> { { "t", "@" + c.Speaker } }, emo: Emotion.Angry, gesture: Anim.Point);
            var res = new Result { R = best.Result, Text = CaseBoard.Plain(best.Why) };
            if (best.Result == LogicResult.Contradict || best.Result == LogicResult.Conditional || best.Result == LogicResult.LimitScope)
            {
                T.Valid++; T.Influence = MathX.Clamp01(T.Influence + (best.Result == LogicResult.Contradict ? 0.12f : 0.05f));
                T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = best.Result == LogicResult.Contradict ? "break" : "result", Speaker = Cast.Player, Text = best.Why, ClaimId = c.Id, Data = best.Result.ToString() });
                ApplyVerdict(sim, T, c, best, Cast.Player, ev);
                res.Valid = true; res.Break = best.Result == LogicResult.Contradict;
                foreach (var id in T.Participants.Where(x => x != Cast.Player)) Relations.Change(S, id, Cast.Player, respect: 0.03f);
            }
            else
            {
                T.Invalid++; T.Influence = MathX.Clamp01(T.Influence - 0.06f);
                T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "result", Speaker = Cast.Player, Text = (best.Result == LogicResult.Support ? "오히려 그 주장을 뒷받침한다 — " : "") + best.Why, ClaimId = c.Id, Data = best.Result.ToString() });
                var sp = S.A(c.Speaker); if (sp != null && !sp.IsPlayer) Line(T, sim, sp.Id, "counter", Cast.Player, new Dictionary<string, string> { { "t", "@" + Cast.Player } });
            }
            return res;
        }
        static int Rank(LogicResult r) => r == LogicResult.Contradict ? 5 : r == LogicResult.Conditional ? 4 : r == LogicResult.LimitScope ? 3 : r == LogicResult.Support ? 2 : r == LogicResult.NeedPremise ? 1 : 0;

        public static Result PlayerSupport(Simulation sim, string claimId, string evidenceId)
        {
            if (sim.S.Trial?.Debate != null) return DebateShow(sim, claimId, evidenceId);
            var S = sim.S; var T = S.Trial; var c = T?.Claims.FirstOrDefault(x => x.Id == claimId); var ev = TrialGames.BulletEvidence(S, evidenceId);
            if (c == null || ev == null) return new Result();
            var v = ev.Props.Select(p => Logic.Check(S, c.Prop, p, ev.Direct, ev.Root)).FirstOrDefault(x => x.Result == LogicResult.Support);
            Line(T, sim, Cast.Player, "p_agree", c.Speaker, new Dictionary<string, string> { { "t", "@" + c.Speaker } });
            if (v != null) { c.Status = "supported"; if (!c.Supporters.Contains(Cast.Player)) c.Supporters.Add(Cast.Player); T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "result", Speaker = Cast.Player, Text = v.Why, ClaimId = c.Id, Data = "Support" }); T.Influence = MathX.Clamp01(T.Influence + 0.04f); ApplyVerdict(sim, T, c, v, Cast.Player, ev); Relations.Change(S, c.Speaker, Cast.Player, like: 0.04f, trust: 0.05f, memory: "심판에서 내 편을 들어 줬다"); return new Result { R = LogicResult.Support, Text = CaseBoard.Plain(v.Why), Valid = true }; }
            T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "result", Speaker = Cast.Player, Text = "그 단서로는 이 주장을 뒷받침할 수 없다", ClaimId = c.Id, Data = "Irrelevant" }); T.Influence = MathX.Clamp01(T.Influence - 0.03f);
            return new Result { R = LogicResult.Irrelevant, Text = "상관없는 단서다" };
        }

        public static void PlayerAskSource(Simulation sim, string claimId)
        {
            if (sim.S.Trial?.Debate != null) { DebateAsk(sim, claimId); return; }
            var S = sim.S; var T = S.Trial; var c = T?.Claims.FirstOrDefault(x => x.Id == claimId); if (c == null) return;
            var sp = S.A(c.Speaker); if (sp == null || sp.IsPlayer) return;
            if (T.Mode != TrialMode.TM03_Witness) Mode(T, TrialMode.TM03_Witness, "집중 심문: " + Cast.GivenOf(sp.Id));
            Line(T, sim, Cast.Player, "p_ask_source", sp.Id, new Dictionary<string, string> { { "t", "@" + sp.Id } });
            var st = S.K(sp.Id).Statements.FirstOrDefault(s => c.Prop != null && s.Prop.Kind == c.Prop.Kind && s.Prop.A == c.Prop.A && Math.Abs(s.Prop.T0 - c.Prop.T0) < 5);
            if (st != null) { Line(T, sim, sp.Id, "source_hearsay", Cast.Player, new Dictionary<string, string> { { "t", "@" + st.Speaker } }); c.Status = c.Status == "open" ? "conditional" : c.Status; T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "result", Text = "직접 본 것이 아니라 전해 들은 말이다", ClaimId = c.Id, Data = "Conditional" }); return; }
            if (c.Lie)
            {
                // under questioning, a liar's details get shaky — composure decides
                bool crack = S.R(Stream.Trial).F() * 100 > sp.Def.Composure;
                Line(T, sim, sp.Id, crack ? "panic" : "source_direct", Cast.Player, null, emo: crack ? Emotion.Fear : Emotion.Neutral);
                if (crack) { T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "result", Text = Cast.GivenOf(sp.Id) + "의 설명이 흔들린다 — 자세히 캐물으니 앞뒤가 안 맞는다", ClaimId = c.Id, Data = "LimitScope" }); c.Status = "limited"; foreach (var id in T.Participants.Where(x => x != Cast.Player && x != sp.Id)) { var k = S.K(id); k.Suspicion[sp.Id] = (k.Suspicion.TryGetValue(sp.Id, out var v) ? v : 0) + 0.12f; } }
                return;
            }
            // honest witness explains conditions (distance/light) — may limit the scope of their own claim
            var sight = S.K(sp.Id).Sightings.FirstOrDefault(s => c.Prop != null && (s.Target == c.Prop.A || s.Target == c.Prop.B) && Math.Abs(s.T0 - c.Prop.T0) < 5);
            Line(T, sim, sp.Id, "source_direct", Cast.Player, null);
            if (sight != null && sight.IdConf < 0.6f) { c.Status = "limited"; T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "result", Text = "어둡거나 멀어서 얼굴을 확실히 보지는 못했다", ClaimId = c.Id, Data = "LimitScope" }); }
        }

        /// <summary>Present a card to the court. <paramref name="target"/>: a claim id → judged against that claim only;
        /// "@player" → against the open claims accusing the player and their premises; null → every open claim (legacy/autopilot).</summary>
        public static Result PlayerPresent(Simulation sim, string evidenceId, string target = null)
        {
            if (sim.S.Trial?.Debate != null) return DebatePresentCard(sim, evidenceId);
            var S = sim.S; var T = S.Trial; var ev = S.K(Cast.Player).Evidence.FirstOrDefault(e => e.Id == evidenceId && !e.Hidden); if (T == null || ev == null) return new Result();
            var view = CaseBoard.Describe(sim, ev);
            Line(T, sim, Cast.Player, "p_present", null, new Dictionary<string, string> { { "item", view.Title } });
            T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "evidence", Speaker = Cast.Player, Text = CaseBoard.Plain(view.Title + (string.IsNullOrEmpty(view.Line) ? "" : " — " + view.Line)), Data = ev.Id });
            List<TrialClaim> targets;
            if (target == null) targets = T.Claims.Where(c => c.Status == "open" || c.Status == "supported").ToList();
            else if (target == "@player")
            {
                var acc = T.Claims.Where(c => c.Accused == Cast.Player && !c.Player && (c.Status == "open" || c.Status == "supported")).ToList();
                var prem = acc.SelectMany(c => c.Premises).Distinct().Select(id => T.Claims.FirstOrDefault(x => x.Id == id)).Where(c => c != null && (c.Status == "open" || c.Status == "supported")).ToList();
                targets = acc.Concat(prem).Distinct().ToList();
            }
            else targets = T.Claims.Where(c => c.Id == target && (c.Status == "open" || c.Status == "supported")).ToList();
            // presenting makes it public; check it against the targeted claims for an automatic contradiction
            Result best = new Result { R = LogicResult.Irrelevant, Text = "모두가 그 단서를 살펴봤다" };
            foreach (var c in targets)
            {
                foreach (var p in ev.Props)
                {
                    var v = Logic.Check(S, c.Prop, p, ev.Direct, ev.Root);
                    if (v.Result == LogicResult.Contradict) { T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "break", Speaker = Cast.Player, Text = CaseBoard.Plain($"{Cast.GivenOf(c.Speaker)}의 말과 어긋난다 — {v.Why}"), ClaimId = c.Id, Data = "Contradict" }); ApplyVerdict(sim, T, c, v, Cast.Player, ev, target == null); T.Valid++; T.Influence = MathX.Clamp01(T.Influence + 0.1f); best = new Result { R = v.Result, Text = CaseBoard.Plain(v.Why), Valid = true, Break = true }; break; }
                }
            }
            Publish(sim, T, ev, target == null ? null : targets);
            foreach (var id in T.Participants.Where(x => x != Cast.Player)) Testimony.UpdateSuspicion(sim, S.A(id));
            // TM06: an ally who holds evidence from the same investigation (or the witness standing by their own word) joins in
            string rk = Evidences.RootKey(ev.Root);
            var ally = T.Participants.Where(x => x != Cast.Player).Select(S.A).FirstOrDefault(a => a != null && (ev.SharedWith.Contains(a.Id) || S.K(a.Id).Evidence.Any(e => e.Root == ev.Root || Evidences.RootKey(e.Root) == rk) || (ev.Kind == EvKind.Testimony && ev.Subject == a.Id)) && S.R(a.Id, Cast.Player).Trust > 0.15f);
            if (ally != null && !T.ModesSeen.Contains(TrialMode.TM06_Joint.ToString())) { Mode(T, TrialMode.TM06_Joint, "합동 논증: " + Cast.GivenOf(ally.Id)); Line(T, sim, ally.Id, "agree", Cast.Player, new Dictionary<string, string> { { "t", "@" + Cast.Player } }); T.Influence = MathX.Clamp01(T.Influence + 0.08f); }
            return best;
        }

        public static void PlayerAccuse(Simulation sim, string target)
        {
            if (sim.S.Trial?.Debate != null) { DebateAccuse(sim, target); return; }
            var S = sim.S; var T = S.Trial; if (T == null) return;
            T.PlayerAccused = target;
            var c = new TrialClaim { Id = "c" + (T.Claims.Count + 1), Speaker = Cast.Player, Prop = new Prop { Kind = PropKind.Culprit, A = target, B = TargetIncident(S)?.Victim }, Key = "p_accuse", Topic = "culprit", Player = true, Accused = target, Beat = T.Beats.Count };
            T.Claims.Add(c);
            Line(T, sim, Cast.Player, "p_accuse", target, new Dictionary<string, string> { { "t", "@" + target } }, c, emo: Emotion.Angry, gesture: Anim.Point);
            var a = S.A(target); if (a != null && !a.IsPlayer) { var w = Testimony.Where(sim, a, Cast.Player); var dc = Claim(T, sim, a.Id, w.Prop, "defend_self", w.Slots, w.Lie, "culprit", Cast.Player, Emotion.Surprised); NpcReactions(sim, T, dc); }
            Sides(sim, T, c);
            if (T.Stage != "Final" && T.Stage != "Vote") StartFinal(sim, T);
            T.Accused = target;
        }

        public static void PlayerDefense(Simulation sim, string evidenceId)
        {
            var S = sim.S; var T = S.Trial; if (T == null) return;
            if (evidenceId != null) PlayerPresent(sim, evidenceId);
            T.PendingPrompt = null;
        }

        public static void SkipPrompt(Simulation sim) { var T = sim.S.Trial; if (T != null) T.PendingPrompt = null; }

        // ------------------------------------------------------------------ vote & verdict
        public static List<string> VoteCandidates(GameState S) => S.Trial.Participants.ToList();
        public static List<string> DeadCandidates(GameState S) => S.Trial.Dead.ToList();

        public static void PlayerVote(Simulation sim, string target) { sim.S.Trial.Votes[Cast.Player] = target; }

        static void RunVote(Simulation sim, TrialState T)
        {
            var S = sim.S; var rng = S.R(Stream.VoteDraw);
            bool playerVoter = T.Participants.Contains(Cast.Player) && S.Player.Alive;
            if (playerVoter && !T.Votes.ContainsKey(Cast.Player)) { T.PendingPrompt = "vote"; return; }
            foreach (var id in T.Participants.Where(x => x != Cast.Player)) if (!T.Votes.ContainsKey(id)) T.Votes[id] = NpcVote(sim, T, S.A(id));
            var tally = T.Votes.GroupBy(v => v.Value).Select(g => (who: g.Key, n: g.Count())).OrderByDescending(x => x.n).ToList();
            T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "vote", Text = string.Join(", ", tally.Select(x => Cast.GivenOf(x.who) + " " + x.n + "표")), Data = string.Join(";", T.Votes.Select(kv => kv.Key + ">" + kv.Value)) });
            bool tie = tally.Count > 1 && tally[0].n == tally[1].n;
            if (tie && T.VoteRound == 0)
            {
                T.VoteRound = 1; T.Votes.Clear(); Yusti(sim, T, "y_vote_tie", "vote_tie"); return;
            }
            string accused;
            if (tie) { var tops = tally.Where(x => x.n == tally[0].n).Select(x => x.who).OrderBy(x => x).ToList(); accused = tops[rng.R(tops.Count)]; Yusti(sim, T, "y_vote_draw", "vote_draw", new Dictionary<string, string> { { "t", Cast.NameOf(accused) } }); }
            else accused = tally[0].who;
            T.Accused = accused;
            if (S.RuleActive("CH01")) Yusti(sim, T, "y_vote_open", "vote_open", new Dictionary<string, string> { { "list", string.Join(", ", T.Votes.Select(kv => Cast.GivenOf(kv.Key) + " → " + Cast.GivenOf(kv.Value))) } });
            // vote lines
            foreach (var kv in T.Votes.Where(kv => kv.Key != Cast.Player).Take(4)) Line(T, sim, kv.Key, "vote_line", null, new Dictionary<string, string> { { "t", "@" + kv.Value } });
            Yusti(sim, T, "y_vote_result", "vote_result", new Dictionary<string, string> { { "t", Cast.NameOf(accused) } });
            T.Stage = "Done"; T.Finished = true;
            Settlements.Resolve(sim, T);
        }

        /// <summary>Private belief vs public vote: evidence-based suspicion, then self-protection, loyalty, fear and alliances.</summary>
        static string NpcVote(Simulation sim, TrialState T, Actor a)
        {
            var S = sim.S; var rng = S.R(Stream.VoteDraw); Testimony.UpdateSuspicion(sim, a); var k = S.K(a.Id);
            var cands = T.Participants.Concat(T.Dead).Distinct().Where(x => x != a.Id).ToList();
            var util = new Dictionary<string, double>();
            foreach (var x in cands)
            {
                double sus = k.Suspicion.TryGetValue(x, out var s) ? s : 0;
                if (T.Dead.Contains(x)) sus *= 0.25; // dead can be named only via the separate tab
                double u = sus * 2.0;
                var r = S.R(a.Id, x); u -= Math.Max(0, r.Attach) * 0.9 + (r.Tags.Contains("lover") || r.Tags.Contains("family") ? 1.2 : 0);
                u += Math.Max(0, -r.Opinion) * 0.4;
                if (x == T.Accused) u += 0.3 + T.Influence * (S.R(a.Id, Cast.Player).Trust + 0.2) * 0.8;
                if (T.Claims.Any(c => c.Accused == x && c.Supporters.Contains(a.Id))) u += 0.5; // consistency with public stance
                if (T.Public.Any(p => p.Kind == PropKind.AtPlace && p.A == x && p.Value == "window-cover")) u -= 1.2;
                u += rng.F() * 0.25;
                util[x] = u;
            }
            // the real culprit votes for whoever keeps them safest (the current favourite that isn't them)
            if (IsCulprit(S, a.Id)) { var fav = util.Where(kv => kv.Key != a.Id).OrderByDescending(kv => kv.Value).First().Key; if (T.Stances.TryGetValue(a.Id, out var st1)) st1.VoteIntent = fav; return fav; }
            var best = util.OrderByDescending(kv => kv.Value).First().Key;
            if (T.Stances.TryGetValue(a.Id, out var st)) st.VoteIntent = best;
            return best;
        }

        // ------------------------------------------------------------------ headless (tests / observe mode)
        public static void RunHeadless(Simulation sim, bool smartPlayer)
        {
            if (sim.S.Trial?.Debate != null) { DebateHeadless(sim, smartPlayer ? "smart" : "naive"); return; }
            var S = sim.S; int guard = 0;
            while (S.Trial != null && !S.Trial.Finished && guard++ < 600)
            {
                var T = S.Trial;
                if (T.PendingPrompt != null)
                {
                    if (T.PendingPrompt == "vote") { PlayerVote(sim, AutoVote(sim, smartPlayer)); T.PendingPrompt = null; continue; }
                    if (T.PendingPrompt.StartsWith("game:") || T.PendingPrompt == "accuse") { TrialGames.AutoResolve(sim, smartPlayer); continue; }
                    T.PendingPrompt = null; continue;
                }
                var b = Next(sim); if (b == null) { if (T.PendingPrompt != null) continue; break; }
                if (smartPlayer && b.ClaimId != null && b.Kind == "line")
                {
                    var c = T.Claims.FirstOrDefault(x => x.Id == b.ClaimId);
                    if (c != null && c.Speaker != Cast.Player && c.Status == "open")
                        foreach (var ev in S.K(Cast.Player).Evidence.Where(e => !e.Hidden).ToList())
                            if (ev.Props.Any(p => Logic.Check(S, c.Prop, p, ev.Direct, ev.Root).Result == LogicResult.Contradict)) { PlayerContradict(sim, c.Id, ev.Id); break; }
                }
            }
        }

        internal static string AutoVote(Simulation sim, bool smart)
        {
            var S = sim.S; var T = S.Trial;
            if (smart) { Testimony.UpdateSuspicion(sim, S.Player); var k = S.K(Cast.Player); var top = k.Suspicion.Where(kv => T.Participants.Contains(kv.Key) && kv.Key != Cast.Player).OrderByDescending(kv => kv.Value).FirstOrDefault(); if (top.Key != null) return top.Key; }
            return T.Accused ?? T.Participants.First(x => x != Cast.Player);
        }
    }
}
