using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    // =====================================================================================================================
    // THE PROACTIVE CULPRIT MIND — core (hooks, lifecycle, helpers).
    //
    //   pressure (Motives)  ─┐
    //                        ├─ a decision to kill, per personality ─→ Design: the opportunity found or MADE (Moments),
    //   opportunity (world) ─┘                                         the approach, the weapon, the frame, the alibi,
    //                                                                  the rule shields (Design / Story)
    //   → Preparing: visible daily-life acts that leave tells (Prep) → Strike: a MurderPlan the old kill machinery runs,
    //     with scheme steps "S_*" (Strike) → Covering: first to find the body, the story kept straight → Done.
    //
    // Hooks (all small and delimited, in Crime.cs): Update (Crime.Update), TakeOver (Crime.Evaluate, before BuildPlan),
    // Think (Crime.Think when no plan), PlanThink/Exec ("S_" steps), PhaseOk (plans that live in the investigation),
    // Waiting (the watchdog). Display: GrammarKor/Verb (MethodsClues).
    //
    // Determinism: the mind draws NO RNG stream. Every choice is a MurderHash of (campaign, loop, key) — MurderFoundation
    // H2 "counter hash" — so a quiet mind leaves the rest of the world bit-identical, and a save/load continues identically.
    // Forced method tests (SetPieces.Force) run the legacy planner untouched: the mind stands down.
    // =====================================================================================================================
    public static partial class Initiative
    {
        /// <summary>Lab switch (SimTests "initiative off" compares against the legacy planner).</summary>
        public static bool Enabled = true;
        public static bool Active(GameState S) => Enabled && SetPieces.Force == null && S != null;

        // ------------------------------------------------------------------ deterministic helpers (no stream is drawn)
        internal static double U(GameState S, string key) => MurderHash.U01(S, key);
        internal static Rng Local(GameState S, string key)
        {
            ulong h = Rng.Hash(key) ^ (S.Rng.CampaignSeed * 0x9E3779B97F4A7C15UL) ^ ((ulong)S.Loop << 40);
            return new Rng(h, 0x5C4E3EUL);
        }
        internal static MurderWorld W(GameState S) { if (S.Mur == null) S.Mur = new MurderWorld(); return S.Mur; }
        internal static string Name(string id) => Cast.GivenOf(id);
        internal static string K(string s) => LineBank.FixParticles(s);
        internal static string When(double t) => ClockFmt.Vague(t);

        public static Scheme SchemeById(GameState S, string id) { if (id == null || S.Mur == null) return null; foreach (var x in S.Mur.Schemes) if (x.Id == id) return x; return null; }
        /// <summary>The resident's scheme that is still running this loop (null if none).</summary>
        public static Scheme OpenOf(GameState S, string actor)
        {
            if (S.Mur == null) return null;
            for (int i = S.Mur.Schemes.Count - 1; i >= 0; i--) { var x = S.Mur.Schemes[i]; if (x.Culprit == actor && x.Open && x.Loop == S.Loop) return x; }
            return null;
        }
        public static Scheme ByPlan(GameState S, string plan)
        {
            if (plan == null || S.Mur == null) return null;
            for (int i = S.Mur.Schemes.Count - 1; i >= 0; i--) { var x = S.Mur.Schemes[i]; if (x.Plan == plan || x.Plans.Contains(plan)) return x; }
            return null;
        }
        static Scheme OfPlan(GameState S, MurderPlan plan) => plan?.Reason != null && plan.Reason.StartsWith("scheme:") ? SchemeById(S, plan.Reason.Substring(7)) : null;

        static double CalmEnd(GameState S) => S.Ch.ChapterStartClock + (S.Loop == 1 && S.Chapter == 1 ? 30 * 60 : 20 * 60);
        static int DeadThisChapter(GameState S) => S.Incidents.Values.Count(i => i.Chapter == S.Chapter && i.Loop == S.Loop && !i.ProcedureClosed);
        /// <summary>The same budget the old planner respects: the chapter's victim cap, at most two live plans, the survivor floor.</summary>
        static bool Budget(GameState S)
            => S.Ch.Reservations.Count(r => !r.Released && !r.Consumed) + DeadThisChapter(S) < S.Ch.VictimCap
            && S.Plans.Values.Count(p => p.Stage != "Done" && p.Stage != "Aborted") < 2
            && S.Survivors > S.FloorLocked;

        // ================================================================== hook: Crime.Update (every 50 ticks)
        public static void Update(Simulation sim)
        {
            var S = sim.S; if (!Active(S)) return;
            if (S.Phase != Phase.Daily && S.Phase != Phase.Investigation) return;
            var M = W(S);
            for (int i = 0; i < M.Schemes.Count; i++)
            {
                var sc = M.Schemes[i]; if (!sc.Open) continue;
                if (sc.Loop != S.Loop) { End(sim, sc, "Abandoned", "저택의 시간이 처음으로 돌아가서"); continue; }
                Step(sim, sc);
            }
            Scan(sim);
        }

        // ================================================================== hook: Crime.Evaluate (lethal intent, alternatives already tried)
        /// <summary>A resident has decided to kill (the old planner's intent). The proactive mind takes the intent over: it looks for,
        /// or makes, the right moment and prepares. Returns false when the old immediate plan should run (the impulsive few).</summary>
        public static bool TakeOver(Simulation sim, Actor a, string target, string motive, float p)
        {
            var S = sim.S; if (!Active(S) || a == null || target == null) return false;
            if (OpenOf(S, a.Id) != null) return true;                       // the scheme already owns this person's intent
            if (W(S).Schemes.Any(x => x.Open && x.Victim == target && x.Loop == S.Loop && x.State != "Covering")) return true;   // someone is already after them: this intent waits (no second plan on the same person)
            var st = SchemeStyles.Of(a.Def);
            double imp = st.Style == "Impulsive" ? 0.3 : 0.06;
            if (p > 0.95f && a.Def.P.Aggression > 0.55f) imp += 0.2;
            if (U(S, $"imp:{a.Id}:{S.Chapter}:{(int)(S.Clock / 90)}") < imp) return false;   // acts at once, prepares nothing
            if (W(S).Schemes.Count(x => x.Open && x.PreStrike && x.Loop == S.Loop) >= 3) return false;
            var c = new MotiveCand { Target = target, Motive = motive, P = p };
            Refine(sim, a, c);
            return Form(sim, a, c) != null;
        }

        // ================================================================== hook: Crime.Think (no plan in hand)
        public static Activity Think(Simulation sim, Actor a)
        {
            var S = sim.S; if (!Active(S) || a == null || a.IsPlayer || a.IsButler || a.Status != ActorStatus.Active) return null;
            if (S.Phase != Phase.Daily && S.Phase != Phase.Investigation) return null;
            var fav = FavourActivity(sim, a); if (fav != null) return fav;
            var sc = OpenOf(S, a.Id); if (sc == null) return null;
            if (sc.State == "Preparing") return PrepActivity(sim, a, sc);
            if (sc.State == "Covering") return CoverActivity(sim, a, sc);
            return null;
        }

        // ================================================================== hook: plans that may run during the investigation
        public static bool PhaseOk(GameState S, MurderPlan plan)
        {
            if (S.Phase != Phase.Investigation) return false;
            var sc = OfPlan(S, plan); return sc != null && sc.Moment == "investigation";
        }
        /// <summary>A scheme's plan: the old conscience check (Relations.Pressure against the same target) does not apply — the
        /// scheme weighed its own motive while preparing, and a copycat's or a silencer's target is not the everyday one.</summary>
        public static bool OwnsConscience(GameState S, MurderPlan plan) => OfPlan(S, plan) != null;
        /// <summary>Scheme plan steps that only wait for the world (the watchdog must not call them "stuck").</summary>
        public static bool Waiting(string kind) => kind == "S_WaitMoment" || kind == "S_Near" || kind == "S_DarkStrike" || kind == "S_WaitEvent";

        // ================================================================== lifecycle
        static Scheme Form(Simulation sim, Actor a, MotiveCand c)
        {
            var S = sim.S; var M = W(S);
            var sc = new Scheme
            {
                Id = "sch" + (M.NextId++), Culprit = a.Id, Victim = c.Target, Motive = c.Motive, Trigger = c.Trigger ?? TriggerText(sim, a, c),
                Protects = c.Protects, CopyOf = c.CopyOf, BaseMotive = c.Base, Pressure = c.P, Style = SchemeStyles.Of(a.Def).Style,
                Loop = S.Loop, Chapter = S.Chapter, Formed = S.Clock
            };
            if (c.Variant != null) sc.Var(c.Variant);
            M.Schemes.Add(sc);
            sc.Log.Add(K($"{ClockFmt.DayHM(S.Clock)} {Name(a.Id)}은(는) {Name(c.Target)}을(를) 죽이기로 마음먹었다 — {sc.Trigger}."));
            S.Log("SchemeFormed", a.Id, c.Target, data: $"{sc.Id} {c.Motive} p={c.P:0.00}", secret: true);
            S.Dev($"SCHEME {a.Id} -> {c.Target} {c.Motive} p={c.P:0.00} ({sc.Trigger})");
            Step(sim, sc);
            return sc;
        }

        static void Step(Simulation sim, Scheme sc)
        {
            var S = sim.S; var a = S.A(sc.Culprit); var v = S.A(sc.Victim);
            if (a == null || !a.Alive || a.Status == ActorStatus.Escaped || a.Status == ActorStatus.Executed) { End(sim, sc, "Abandoned", "스스로 움직일 수 없게 돼서"); return; }
            if (sc.PreStrike && (v == null || !v.Alive || v.Status == ActorStatus.Escaped || v.Status == ActorStatus.Executed)) { End(sim, sc, "Abandoned", "표적이 먼저 사라져서"); return; }
            switch (sc.State)
            {
                case "Designing":
                    if (S.Clock < sc.NextCheck) return;
                    if (!Design(sim, sc))
                    {
                        sc.Redesigns++; sc.NextCheck = S.Clock + 40 + U(S, sc.Id + ":rd" + sc.Redesigns) * 40;
                        if (sc.Redesigns > 4) End(sim, sc, "Abandoned", "쓸 만한 기회를 찾지 못해서");
                        return;
                    }
                    sc.State = "Preparing"; sc.Designed = S.Clock; sc.NextCheck = S.Clock + 60;
                    return;
                case "Preparing": Preparing(sim, sc); return;
                case "Striking": Striking(sim, sc); return;
                case "Covering": Covering(sim, sc); return;
            }
        }

        static void Preparing(Simulation sim, Scheme sc)
        {
            var S = sim.S; var a = S.A(sc.Culprit);
            if (a.Status != ActorStatus.Active) return;
            // conscience: bonds made since, a saved friend, a wish that fades — people back out before the act
            if (S.Clock >= sc.NextCheck)
            {
                sc.NextCheck = S.Clock + 60;
                float p = CurrentPressure(sim, a, sc);
                if (p < 0.1f && U(S, sc.Id + ":cons:" + (int)(S.Clock / 60)) < 0.7) { End(sim, sc, "Abandoned", "마음이 바뀌어서"); return; }
                sc.Pressure = Math.Max(p, 0f);
            }
            if (sc.EventId != null)
            {
                var g = S.Gatherings.FirstOrDefault(x => x.Id == sc.EventId);
                if (g == null || g.Cancelled) { Redesign(sim, sc, "모임이 틀어져서"); return; }
            }
            // the house is suddenly distracted: a body was found. A patient schemer sees it — and rule 여섯 as a shield.
            if (S.Phase == Phase.Investigation && sc.Moment != "investigation") { InvestigationSwitch(sim, sc); return; }
            if (S.Phase == Phase.Daily && sc.Moment == "investigation") { Redesign(sim, sc, "수사가 끝나 버려서"); return; }
            if (S.Clock > sc.MomentEnd) { Redesign(sim, sc, "때를 놓쳐서"); return; }
            if (sc.Prep.Any(t => t.Essential && t.Failed)) { Redesign(sim, sc, "준비가 어긋나서"); return; }
            bool ready = EssentialsDone(sc);
            if (S.Clock >= sc.StrikeAt && ready) Strike(sim, sc);
            else if (!ready && S.Clock >= sc.StrikeAt + 20) Redesign(sim, sc, "준비를 다 하지 못해서");
        }

        static bool EssentialsDone(Scheme sc) { foreach (var t in sc.Prep) if (t.Essential && !t.Done) return false; return true; }

        static void Strike(Simulation sim, Scheme sc)
        {
            var S = sim.S; var a = S.A(sc.Culprit);
            if (a.PlanId != null) return;
            if (S.Phase == Phase.Daily && S.Clock < CalmEnd(S)) return;          // every chapter opens with ordinary life
            if (!Budget(S)) { if (S.Clock > sc.MomentEnd - 15) Redesign(sim, sc, "이번 챕터의 희생자 한도가 차서"); return; }
            if (a.Act != null && a.Act.Id != null && a.Act.Id.StartsWith("scheme:")) sim.Interrupt(a, 0.1);
            var plan = BuildStrike(sim, sc);
            if (plan == null) { Redesign(sim, sc, "실행 순서를 짜지 못해서"); return; }
            S.Plans[plan.Id] = plan; a.PlanId = plan.Id;
            sc.Plan = plan.Id; sc.Plans.Add(plan.Id); sc.State = "Striking"; sc.Struck = S.Clock; sc.Strikes++;
            S.Log("PlanFormed", a.Id, sc.Victim, plan: plan.Id, data: $"{plan.Grammar} motive={sc.Motive} scheme={sc.Id} moment={sc.Moment}", secret: true);
            S.Dev($"PLAN {a.Id} -> {sc.Victim} {plan.Grammar} ({sc.Motive}) scheme={sc.Id} moment={sc.Moment} approach={sc.Approach} weapon={plan.WeaponType}");
            sc.Log.Add(K($"{ClockFmt.DayHM(S.Clock)} 실행에 들어갔다 — {MomentText(sc)}, {ApproachText(sc)}."));
            Replay.MarkPlanStart(sim, plan);
            var nov = W(S).Novelty; nov.Add("shape:" + sc.Shape); nov.Add("moment:" + sc.Moment); nov.Add("approach:" + sc.Approach);
        }

        static void Striking(Simulation sim, Scheme sc)
        {
            var S = sim.S; var v = S.A(sc.Victim);
            S.Plans.TryGetValue(sc.Plan ?? "", out var plan);
            var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == sc.Victim && i.Loop == S.Loop);
            if (inc != null)
            {
                if (inc.Culprit == sc.Culprit) { sc.State = "Covering"; sc.Killed = inc.DeathClock; OnKilled(sim, sc, inc); return; }
                if (plan == null || plan.Stage == "Done" || plan.Stage == "Aborted") { End(sim, sc, "Abandoned", "다른 손에 먼저 죽어서"); return; }
                return;
            }
            if (plan == null) { Redesign(sim, sc, "계획이 사라져서"); return; }
            if (plan.Stage == "Aborted")
            {
                string why = plan.Log.Count > 0 ? plan.Log[plan.Log.Count - 1] : "중단";
                if (sc.Strikes >= 2) End(sim, sc, "Abandoned", "두 번 틀어져서 — " + why); else Redesign(sim, sc, "실행이 틀어져서 — " + why);
                return;
            }
            if (plan.Stage == "Done" && v != null && v.Alive && v.Body.DeathAt < 0 && v.Status == ActorStatus.Active)
            { if (sc.Strikes >= 2) End(sim, sc, "Abandoned", "표적이 살아남아서"); else Redesign(sim, sc, "표적이 살아남아서"); }
        }

        static void Redesign(Simulation sim, Scheme sc, string why)
        {
            var S = sim.S;
            sc.Log.Add(K($"{ClockFmt.DayHM(S.Clock)} 계획을 고쳤다 — {why}."));
            S.Log("SchemeRevise", sc.Culprit, sc.Victim, data: sc.Id + " " + why, secret: true);
            if (sc.Redesigns >= 3) { End(sim, sc, "Abandoned", why); return; }
            sc.Redesigns++;
            // our own evening that has not begun yet goes on without a purpose (the guests were invited) — nothing to undo
            foreach (var f in W(S).Favours) if (f.Scheme == sc.Id && !f.Done) f.Failed = true;
            ClearDesign(sc);
            sc.State = "Designing"; sc.NextCheck = S.Clock + 20 + U(S, sc.Id + ":rdw" + sc.Redesigns) * 30;
        }

        /// <summary>The house has announced an evening (HouseEvents): a schemer still preparing weighs it against their own design —
        /// a toast in the dark, a hall of masks, a chart that says who searches alone where are stages nobody could set alone.
        /// This is not a failure (no redesign is spent) and whatever is prepared and still useful is kept; the design scoring
        /// decides whether the house's evening wins.</summary>
        public static void OnHouseEvent(Simulation sim, Gathering g)
        {
            var S = sim.S; if (!Active(S) || g == null) return;
            foreach (var sc in W(S).Schemes.Where(x => x.Open && x.Loop == S.Loop && x.State == "Preparing" && (x.StrikeAt < 0 || x.StrikeAt > S.Clock + 90)).ToList())
            {
                bool going = g.Status.TryGetValue(sc.Culprit, out var st) && (st == "accepted" || st == "invited");
                bool posted = g.Kind == "house:hunt" && S.Flags.ContainsKey($"hevzone:{g.Id}:{sc.Victim}");
                if (!going && !posted) continue;
                sc.Log.Add(K($"{ClockFmt.DayHM(S.Clock)} 저택의 공지({g.Label})를 듣고 계획을 다시 저울질했다."));
                S.Log("SchemeRevise", sc.Culprit, sc.Victim, data: sc.Id + " 저택의 공지를 듣고", secret: true);
                foreach (var f in W(S).Favours) if (f.Scheme == sc.Id && !f.Done) f.Failed = true;
                ClearDesign(sc); sc.State = "Designing"; sc.NextCheck = S.Clock + 5 + U(S, sc.Id + ":hev:" + g.Id) * 20;
            }
        }

        static void ClearDesign(Scheme sc)
        {
            sc.Moment = sc.MomentRef = sc.MomentText = sc.Approach = sc.Head = null; sc.MomentRoom = sc.KillRoom = sc.EventRoom = -1; sc.MomentAt = sc.MomentEnd = sc.StrikeAt = -1;
            sc.EventId = sc.EventKind = sc.EventLabel = sc.Pretext = null; sc.Alibi = sc.AlibiWitness = null; sc.AlibiRoom = -1; sc.AlibiAt = -1; sc.ClockF = -1; sc.ClockShift = 0;
            sc.Scapegoat = sc.Frame = sc.FrameItem = sc.Frame2 = null; sc.Helper = sc.HelperTask = null; sc.MarkItem = null; sc.DarkBy = null; sc.Shields.Clear(); sc.Guests.Clear();
            // keep what was already prepared and still useful (the weapon in hand, the garb, the poison): the prep list is rebuilt
            sc.Prep.RemoveAll(t => !t.Done || (t.Kind != "obtain" && t.Kind != "garb" && t.Kind != "poison" && t.Kind != "token"));
            var keep = new List<string>(); foreach (var v in sc.Variants) if (v == "copycat" || v == "turnabout" || v == "avenger" || v == "silencer") keep.Add(v);
            sc.Variants.Clear(); sc.Variants.AddRange(keep);
        }

        static void End(Simulation sim, Scheme sc, string state, string why)
        {
            var S = sim.S;
            sc.State = state; sc.Ended = S.Clock; sc.EndWhy = why;
            sc.Log.Add(K($"{ClockFmt.DayHM(S.Clock)} {(state == "Done" ? "끝냈다" : "그만두었다")} — {why}."));
            S.Log(state == "Done" ? "SchemeDone" : "SchemeAbandon", sc.Culprit, sc.Victim, data: sc.Id + " " + why, secret: true);
            S.Dev($"SCHEME-{(state == "Done" ? "DONE" : "ABANDON")} {sc.Culprit}->{sc.Victim} {sc.Id}: {why}");
            foreach (var f in W(S).Favours) if (f.Scheme == sc.Id && !f.Done) f.Failed = true;
        }

        static void OnKilled(Simulation sim, Scheme sc, Incident inc)
        {
            var S = sim.S;
            sc.Log.Add(K($"{ClockFmt.DayHM(inc.DeathClock)} {Name(sc.Victim)}이(가) {S.RoomName(inc.DeathRoom)}에서 숨졌다."));
            int order = S.Incidents.Values.Count(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Murder && i.ResultSeq < inc.ResultSeq) + 1;
            if (order >= 2) { sc.Var("second-killer"); sc.Shield("y6", "second-killer"); }
            S.Log("SchemeKill", sc.Culprit, sc.Victim, data: $"{sc.Id} order={order} moment={sc.Moment} approach={sc.Approach}", secret: true);
        }

        static void Covering(Simulation sim, Scheme sc)
        {
            var S = sim.S;
            var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == sc.Victim && i.Loop == S.Loop);
            if (inc == null) { End(sim, sc, "Done", "끝까지 들키지 않았다"); return; }
            bool coverLeft = sc.Prep.Any(t => t.Kind == "firstin" && !t.Done && !t.Failed);
            if (inc.Confirmed && !coverLeft) { End(sim, sc, "Done", "시신이 발견되고 수사가 시작됐다"); return; }
            if (S.Clock - sc.Killed > 20 * 60) End(sim, sc, "Done", "하루가 지났다");
        }

        // ================================================================== the scan: intent born from events (not only from the old planner)
        static void Scan(Simulation sim)
        {
            var S = sim.S; var M = W(S);
            bool inv = S.Phase == Phase.Investigation;
            double calmEnd = CalmEnd(S); bool calm = S.Clock < calmEnd;
            if (!inv && calm && S.Clock < calmEnd - (calmEnd - S.Ch.ChapterStartClock) * 0.45) return;   // the first stretch of a chapter is ordinary life
            int open = M.Schemes.Count(x => x.Open && x.PreStrike && x.Loop == S.Loop);
            foreach (var a in S.LivingNpcs.OrderBy(x => x.Id, StringComparer.Ordinal).ToList())
            {
                if (a.Status != ActorStatus.Active || a.PlanId != null) continue;
                if (OpenOf(S, a.Id) != null) continue;
                string key = "isc:" + a.Id;
                if (S.Flags.TryGetValue(key, out var nx) && S.Clock < nx) continue;
                S.Flags[key] = S.Clock + 30 + Math.Floor(U(S, key + ":" + (int)S.Clock) * 30);
                if (!inv && open >= 2) continue;
                if (inv && M.Schemes.Count(x => x.Open && x.Moment == "investigation" && x.Loop == S.Loop && x.Chapter == S.Chapter) >= 1) continue;
                var cand = BestMotive(sim, a, inv, calm);
                if (cand != null && M.Schemes.Any(x => x.Open && x.Victim == cand.Target && x.Loop == S.Loop && x.State != "Covering")) continue;
                if (cand == null || cand.P < Threshold(a, cand.Motive)) continue;
                if (Form(sim, a, cand) != null) open++;
            }
        }
    }
}
