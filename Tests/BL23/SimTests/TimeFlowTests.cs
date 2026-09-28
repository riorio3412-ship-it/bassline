using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BL23.Sim;

/// <summary>
/// Time on demand (kernel): a scripted player who never lets the clock run by itself in daily life. Time passes only in
/// skips — the smart row ("다음 일까지"), plain waits, time together with someone, pastimes, sleep, meals — and in real ticks
/// while investigating. Checks that the world still happens: murders, bells, meals, appointments, the trial.
///   timeflow &lt;seed&gt; &lt;days&gt;   — the on-demand campaign
///   together &lt;seed&gt;          — every person × every kind of time together
/// </summary>
public static partial class Program
{
    sealed class TfStats
    {
        public int Skips, Guard, Natural, Stopped, Deferred, HotAtEnd, Settles, Meals, Approach, Appointment, MealStops, Knock, Woken, Critical, Moved, PhaseStops;
        public double MaxSettle, MaxMeal, MaxDefer, MaxOvershoot;
        public Dictionary<string, int> ByKind = new Dictionary<string, int>();
        public Dictionary<string, int> ByStop = new Dictionary<string, int>();
        public int SkipsToFirstCase = -1; public double FirstDeathClock = -1, FirstCaseClock = -1;
        public int AcceptedInvites, MetInvites, MissedInvites;
        public int Trials; public long SkipTicks; public double SkipMs;
        public int TogetherScenes, TogetherYes, Pastimes, Sleeps, Eats;
        public int Cancels, CancelDeferred; public double MaxCancelDefer;
        public int SettleRuns; public double MinSettle = double.MaxValue, MaxSettleRun;
    }

    static int TimeFlowTest(string[] args)
    {
        ulong seed = args.Length > 1 ? ulong.Parse(args[1]) : 20260926UL;
        int days = args.Length > 2 ? int.Parse(args[2]) : 4;
        bool verbose = args.Contains("v");
        var sw = Stopwatch.StartNew();
        var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; sim.OnDemand = true; var S = sim.S;
        sim.EndPrologue();
        var lr = new Rng(seed ^ 0x5eedUL, 7);   // the test player's own dice (never the world's)
        var st = new TfStats(); int fails = 0; var failMsgs = new List<string>();
        void Fail(string m) { fails++; if (failMsgs.Count < 20) failMsgs.Add(m); Console.WriteLine("  FAIL " + m); }
        Console.WriteLine($"timeflow seed {seed} layout {S.Layout.Hash} days {days}");

        // ---------------------------------------------------------------- zero-time checks
        {
            long tick0 = S.Tick; double clock0 = S.Clock;
            var streams0 = S.Rng.Streams.ToDictionary(kv => kv.Key, kv => kv.Value.Consumed);
            for (int i = 0; i < 50; i++) sim.PlayerPerceive();
            bool rngSame = S.Rng.Streams.Count == streams0.Count && S.Rng.Streams.All(kv => streams0.TryGetValue(kv.Key, out var c) && c == kv.Value.Consumed);
            Console.WriteLine($"perceive x50: tick {(S.Tick == tick0 ? "same" : "MOVED")} clock {(S.Clock == clock0 ? "same" : "MOVED")} rng {(rngSame ? "unchanged" : "CHANGED")}");
            if (S.Tick != tick0 || S.Clock != clock0 || !rngSame) Fail("PlayerPerceive moved time or drew RNG");
            // instant things take no time: open, sit, stand, look
            var chair = S.Layout.Furniture.FirstOrDefault(f => f.Type == "Chair" && S.Layout.Spots.Any(sp => sp.Furniture == f.Id && sp.Occupant == null && sp.OnFurniture));
            var box = S.Layout.Furniture.FirstOrDefault(f => Simulation.IsContainer(f) && S.Layout.Room(f.Room)?.Owner == null);
            var loose = S.Items.Values.FirstOrDefault(i => i.Holder == null && !i.Hidden && i.Def != null && !i.Def.Key && i.Room >= 0);
            if (chair != null)
            {
                MoveNear(sim, chair.Pos);
                var sp = sim.PlayerSit(chair, S.Player.Pos); bool seated = sim.PlayerSeated; sim.PlayerStand();
                Console.WriteLine($"sit/stand: seated={seated} spot={(sp != null ? sp.Id : -1)} after={S.Player.Spot} pose={S.Player.Pose}");
                if (sp == null || !seated || S.Player.Spot >= 0) Fail("PlayerSit/PlayerStand");
            }
            if (box != null && loose != null)
            {
                bool hid = sim.ProbeHide(loose, box); var inside = sim.ContainedItems(box);
                MoveNear(sim, box.Pos);
                var or = sim.PlayerOpen(box, inside.Select(i => i.Id).ToList(), true);
                Console.WriteLine($"container {box.Type}: hide={hid} inside={inside.Count} open → \"{or.Text}\" revealed={or.Revealed.Count} hiddenAfter={loose.Hidden}");
                if (!hid || inside.Count == 0 || or.Revealed.Count == 0 || loose.Hidden) Fail("container hide/open");
            }
            var clockF = S.Layout.Furniture.FirstOrDefault(f => f.Type == "Clock" || f.Type == "ClockCase");
            if (clockF != null) { MoveNear(sim, clockF.Pos); var r = sim.PlayerUse(clockF, "wind"); Console.WriteLine($"wind: {r.Minutes}분 → {r.Text}"); }
            if (S.Tick != tick0 || Math.Abs(S.Clock - clock0) > 1e-9) Fail($"instant actions moved time ({S.Tick - tick0} ticks)");
            else Console.WriteLine("instant actions: clock and tick unchanged");
        }

        // ---------------------------------------------------------------- the scripted on-demand player
        int cycle = 0; int lastDay = S.Day; double endClock = days * 1440; int loops = 1; int talkCount = 0;
        bool loadBellChecked = false; bool emergencyChecked = false; bool yieldChecked = false; bool resumeChecked = false; bool bodyFirstChecked = false;
        var annCount = new Dictionary<string, int>();
        int annSeen = 0;
        void CountAnn() { while (annSeen < S.Announcements.Count) { var k = S.Announcements[annSeen++].Key ?? "?"; annCount[k] = (annCount.TryGetValue(k, out var n) ? n : 0) + 1; } if (annSeen > S.Announcements.Count) annSeen = S.Announcements.Count; }
        SkipResult Run(SkipPlan p, string why, int cancelAfter = -1)
        {
            if (p == null) return null;
            var t0 = sw.Elapsed.TotalMilliseconds; long tk0 = S.Tick; double target = p.Target;
            SkipResult res;
            if (cancelAfter < 0) res = sim.RunSkip(p);
            else
            {
                // the player stops the wait part-way (Esc): at once — unless a murder is in a hot moment, then on until it cools
                sim.BeginSkip(p); sim.StepSkip(p, cancelAfter);
                if (!p.Done)
                {
                    st.Cancels++; sim.CancelSkip(p);
                    if (!p.Done)
                    {
                        st.CancelDeferred++; double cc = S.Clock; int g = 0;
                        while (!sim.StepSkip(p, 50) && g++ < 4000) { }
                        st.MaxCancelDefer = Math.Max(st.MaxCancelDefer, S.Clock - cc);
                        Console.WriteLine($"  cancel deferred at {ClockFmt.DayHM(cc)}: ran on {S.Clock - cc:0.0} min until the hot moment passed → {p.Stop?.Kind}");
                    }
                }
                res = sim.EndSkip(p);
            }
            st.SkipMs += sw.Elapsed.TotalMilliseconds - t0; st.SkipTicks += S.Tick - tk0;
            st.Skips++; string kk = p.Kind.ToString(); st.ByKind[kk] = (st.ByKind.TryGetValue(kk, out var a) ? a : 0) + 1;
            string sk = p.Stop?.Kind.ToString() ?? "null"; st.ByStop[sk] = (st.ByStop.TryGetValue(sk, out var b) ? b : 0) + 1;
            if (p.Stop == null) Fail("skip ended without a stop");
            else
            {
                switch (p.Stop.Kind)
                {
                    case StopKind.Guard: st.Guard++; Fail($"GUARD {p.Kind} {p.Label} target {ClockFmt.DayHM(target)}"); break;
                    case StopKind.Target: st.Natural++; break;
                    default: st.Stopped++; break;
                }
                if (p.Stop.Kind == StopKind.Approach) st.Approach++;
                if (p.Stop.Kind == StopKind.Appointment) st.Appointment++;
                if (p.Stop.Kind == StopKind.Meal) st.MealStops++;
                if (p.Stop.Kind == StopKind.Knock) st.Knock++;
                if (p.Stop.Kind == StopKind.Woken) st.Woken++;
                if (p.Stop.Kind == StopKind.Moved) st.Moved++;
                if (p.Stop.Kind == StopKind.Phase) st.PhaseStops++;
                if (p.Stop.Class == StopClass.Critical) st.Critical++;
            }
            if (p.Deferred > 0) { st.Deferred++; st.MaxDefer = Math.Max(st.MaxDefer, p.DeferredMinutes); }
            if (p.SettledMinutes > 0) { st.Settles++; st.MaxSettle = Math.Max(st.MaxSettle, p.SettledMinutes); }
            if (p.MealWaitMinutes > 0) { st.Meals++; st.MaxMeal = Math.Max(st.MaxMeal, p.MealWaitMinutes); }
            if (p.Stop?.Kind == StopKind.Target && p.Kind != SkipKind.Toll && p.Kind != SkipKind.Settle) st.MaxOvershoot = Math.Max(st.MaxOvershoot, S.Clock - target);
            if (S.Phase == Phase.Daily && sim.AnyPlanHot()) st.HotAtEnd++;
            if (verbose || p.Stop != null && p.Stop.Kind != StopKind.Target && p.Stop.Kind != StopKind.Cancelled)
                Console.WriteLine($"  {ClockFmt.DayHM(p.Start)}→{ClockFmt.DayHM(S.Clock)} {p.Kind,-8} {why,-10} {p.Label} → {p.Stop?.Kind} {p.Stop?.Title}{(p.Stop?.Sub != null ? " · " + p.Stop.Sub : "")}" + (res.Lines.Count > 0 ? $" | 그 사이: {string.Join(" / ", res.Lines.Take(3))}" : "") + (res.Activity?.Text != null ? $" | {res.Activity.Text}" : ""));
            sim.PlayerPerceive();
            CountAnn();
            return res;
        }
        void Talk(Actor npc)
        {
            // an ordinary conversation: an offer is accepted, then a line of small talk; the minutes are passed after it
            sim.BeginTalk(npc);
            var opts = sim.Options(npc);
            var lines = new List<Utterance>();
            if (opts.Any(o => o.Id == "req_accept")) { lines.AddRange(sim.Choose(npc, "req_accept")); st.AcceptedInvites++; }
            else if (opts.Any(o => o.Id == "chat")) lines.AddRange(sim.Choose(npc, "chat"));
            foreach (var u in lines) sim.Spoken(u);
            sim.EndTalk(npc); talkCount++;
            if (sim.PendingTalk > 0) { var p = sim.PlanTalk(sim.PendingTalk); sim.PendingTalk = 0; Run(p, "talk"); }
        }
        void Answer()
        {
            // someone came to talk: answer them (a real player would; the approach is a stop)
            foreach (var a in S.LivingNpcs.OrderBy(x => x.Id).ToList())
                if (S.Flags.ContainsKey("approach:" + a.Id) && a.Pos.f == S.Player.Pos.f && a.Pos.DistXZ(S.Player.Pos) < 4f && sim.CanTalk(a, out _)) { Talk(a); break; }
        }

        // a new day starts with people going about their morning (the presentation's day-start settle): at least 4 clock minutes,
        // until everyone has set off and left the crowd the prologue / a trial left in one room
        void Settle(string why)
        {
            var sp = sim.PlanSettle(30); var r = Run(sp, "settle");
            if (r == null) return;
            st.SettleRuns++; st.MinSettle = Math.Min(st.MinSettle, r.Minutes); st.MaxSettleRun = Math.Max(st.MaxSettleRun, r.Minutes);
            Console.WriteLine($"  day-start settle ({why}) at {ClockFmt.DayHM(sp.Start)}: {r.Minutes:0.0} min → {sp.Stop?.Kind}");
            if (r.Minutes < 3.9 && sp.Stop?.Kind == StopKind.Target && sp.Target - sp.Start >= 4) Fail($"day-start settle ended after {r.Minutes:0.0} min");
        }
        Settle("after the prologue");

        long ticksInv = 0; int guardLoops = 0;
        while (S.Clock < endClock && guardLoops++ < 200000)
        {
            if (S.Day != lastDay)
            {
                lastDay = S.Day;
                var json = SaveStore.Serialize(S); var back = SaveStore.Deserialize(json); bool same = SaveStore.Serialize(back) == json;
                Console.WriteLine($"-- day {S.Day} L{S.Loop}C{S.Chapter} phase {S.Phase} alive {S.Survivors} skips {st.Skips} plans {S.Plans.Values.Count(p => p.Stage != "Done" && p.Stage != "Aborted")} roundtrip={(same ? "IDENTICAL" : "DIFF")} faults={sim.Faults} t={sw.ElapsedMilliseconds}ms");
                if (!same) Fail("roundtrip DIFF on day " + S.Day);
            }
            if (st.FirstDeathClock < 0) { var d = S.Incidents.Values.Where(i => i.Loop == 1).OrderBy(i => i.DeathClock).FirstOrDefault(); if (d != null) st.FirstDeathClock = d.DeathClock; }
            if (S.Phase == Phase.Trial)
            {
                st.Trials++;
                TrialSystem.RunHeadless(sim, true);
                Console.WriteLine($"  TRIAL L{S.Loop}C{S.Chapter} at {ClockFmt.DayHM(S.Clock)} → {S.LastVerdictSummary}");
                Replay.BuildSegments(sim); Settlements.AfterReveal(sim);
                if (S.Phase == Phase.LoopEpilogue) { Settlements.NextLoop(sim); sim.EndPrologue(); if (S.Phase != Phase.Daily) S.Phase = Phase.Daily; loops++; endClock = S.Clock + days * 1440; if (loops > 2) break; }
                // (at night the presentation passes the night instead: the test player goes to bed in the daily loop)
                if (S.Phase == Phase.Daily) { int mm = S.Minute; if (!(mm >= 20 * 60 || mm < 5 * 60)) Settle("after the trial"); }
                continue;
            }
            if (S.Phase == Phase.Investigation || S.Phase == Phase.Assembly)
            {
                if (st.FirstCaseClock < 0) { st.FirstCaseClock = S.Clock; st.SkipsToFirstCase = st.Skips; Console.WriteLine($"  CASE opened at {ClockFmt.DayHM(S.Clock)} after {st.Skips} skips (first death {ClockFmt.DayHM(st.FirstDeathClock)})"); }
                // the investigation runs in real time; the test player looks at the scene and asks around, then ends it after 20 minutes
                if (S.Phase == Phase.Investigation && !S.Flags.ContainsKey("tftest:inv:" + S.Chapter + ":" + S.Loop))
                {
                    S.Flags["tftest:inv:" + S.Chapter + ":" + S.Loop] = 1;
                    foreach (var inc in S.Incidents.Values.Where(i => i.Confirmed && i.Chapter == S.Chapter && i.Loop == S.Loop))
                    {
                        var body = S.A(inc.Victim); Evidences.ExamineBody(sim, S.Player, body, true);
                        foreach (var t in S.Traces.Where(t => t.Room == body.Room || t.Room == inc.CauseRoom)) Evidences.ExamineTrace(sim, S.Player, t);
                    }
                    foreach (var npc in S.LivingNpcs.ToList()) { var w = Testimony.Where(sim, npc, Cast.Player); if (w.Prop != null) sim.Learn(Cast.Player, npc.Id, w.Prop, "(test)", w.Lie, false); }
                    // a short wait inside the investigation (critical-only mask)
                    var wp = sim.PlanWait(10, out var why); if (wp == null) Fail("investigation wait: " + why); else { double c0 = S.Clock; Run(wp, "inv-wait"); if (S.Phase == Phase.Investigation && (wp.Stop == null || wp.Stop.Kind == StopKind.Target) && Math.Abs(S.Clock - c0 - 10) > 1.5) Fail($"investigation wait passed {S.Clock - c0:0.0} min"); }   // (a scream may stop it early: that is a stop, not a fault)
                }
                if (S.Phase == Phase.Investigation && sim.CanEndInvestigation(out _, out _)) sim.PlayerEndInvestigation();
                for (int i = 0; i < 60; i++) { sim.Step(); ticksInv++; if (i % 2 == 0) sim.PlayerPerceive(); if (S.Phase == Phase.Trial) break; }
                CountAnn();
                continue;
            }
            if (S.Phase != Phase.Daily) { sim.Step(); continue; }

            // ---------------- daily life, frozen between skips
            var me = S.Player;
            if (!me.Alive) { Console.WriteLine("  player died"); break; }
            Answer();
            // the player finds a body before anyone else (a still world): people are called to it and come running while the clock
            // runs by itself (an emergency), the house tolls, the investigation opens — without pressing T
            if (!bodyFirstChecked)
            {
                var inc0 = S.Incidents.Values.Where(i => i.Loop == S.Loop && !i.Discovered && !i.Confirmed).OrderBy(i => i.DeathClock).FirstOrDefault();
                var body0 = inc0 != null ? S.A(inc0.Victim) : null;
                if (body0 != null && body0.Status == ActorStatus.Dead && body0.CarriedBy == null && body0.StairId < 0 && S.Layout.Room(body0.Room) != null)
                {
                    bodyFirstChecked = true;
                    if (sim.PlayerSeated) sim.PlayerStand();
                    P3 at = body0.Pos; bool placed = false;
                    foreach (var (dx, dz) in new[] { (1.2f, 0f), (-1.2f, 0f), (0f, 1.2f), (0f, -1.2f), (0.8f, 0.8f), (-0.8f, -0.8f) })
                    {
                        var q = sim.SnapPublic(new P3(body0.Pos.f, body0.Pos.x + dx, body0.Pos.z + dz));
                        if (S.Layout.RoomAt(q) != body0.Room || q.DistXZ(body0.Pos) > 2.5f) continue;
                        at = q; placed = true; break;
                    }
                    if (placed)
                    {
                        sim.SetPlayerPose(at, MathX.AngleDeg(body0.Pos.x - at.x, body0.Pos.z - at.z), false, false);
                        double c0 = S.Clock; long tk0 = S.Tick;
                        var streamsB = S.Rng.Streams.ToDictionary(kv => kv.Key, kv => kv.Value.Consumed);
                        sim.PlayerPerceive();
                        bool seen = inc0.Discoverers.Contains(Cast.Player); string why0 = sim.EmergencyWhy();
                        // the zero-time look that found it drew no dice: the call for people waits for the next tick
                        bool rngSameB = S.Rng.Streams.Count == streamsB.Count && S.Rng.Streams.All(kv => streamsB.TryGetValue(kv.Key, out var cB) && cB == kv.Value.Consumed);
                        int soon = sim.GatherSoonCount;
                        if (!rngSameB) Fail("finding a body in a still world drew RNG (zero-time look)");
                        if (seen && !inc0.Confirmed && !Cases.Enough(S, inc0) && soon > 0) { sim.Step(); if (!S.Flags.ContainsKey("gather:" + inc0.Id) || sim.GatherSoonCount != 0) Fail("the queued call for people did not run on the next tick"); }
                        // what the session does in a still world: real ticks while something urgent is going on (then until calm, at
                        // most 30 clock minutes), a pending toll as a lapse
                        int n = 0; double tailFrom = -1, enoughAt = -1, tollAt = -1;
                        while (S.Phase == Phase.Daily && n++ < 20 * 120)
                        {
                            if (enoughAt < 0 && Cases.Enough(S, inc0)) enoughAt = S.Clock - c0;
                            if (tollAt < 0 && sim.TollPending) tollAt = S.Clock - c0;
                            if (sim.EmergencyWhy() != null) { tailFrom = -1; sim.Step(); if (n % 2 == 0) sim.PlayerPerceive(); continue; }
                            if (tailFrom < 0) tailFrom = S.Clock;
                            if (!sim.CalmEnough() && S.Clock - tailFrom < 30) { sim.Step(); if (n % 2 == 0) sim.PlayerPerceive(); continue; }
                            if (sim.TollPending) { Run(sim.PlanToll(), "toll"); continue; }
                            break;
                        }
                        sim.TakeStop();
                        Console.WriteLine($"  body-first: {body0.Id} in {S.RoomName(body0.Room)} seen={seen} look rng={(rngSameB ? "unchanged" : "CHANGED")} queued calls {soon} emergency='{why0}' witnesses {Cases.Witnesses(S, inc0)} (enough after {enoughAt:0.0} min, toll called after {tollAt:0.0} min) → phase {S.Phase} after {S.Clock - c0:0.0} min, {S.Tick - tk0} ticks (no T)");
                        if (seen && why0 == null) Fail("body found first: no emergency while people are called");
                        if (seen && S.Phase == Phase.Daily) Fail("body found first: the investigation did not open without T");
                        continue;
                    }
                }
            }
            // an emergency (a scream nearby): the clock runs in real time until calm
            if (sim.EmergencyWhy() != null)
            {
                int n = 0; while ((sim.EmergencyWhy() != null || !sim.CalmEnough()) && n++ < 20 * 50 && S.Phase == Phase.Daily) { sim.Step(); if (n % 2 == 0) sim.PlayerPerceive(); }
                sim.TakeStop(); continue;
            }
            // a pending toll (the player found someone): the toll lapse
            if (sim.TollPending) { Run(sim.PlanToll(), "toll"); continue; }
            // appointment: go there shortly before and wait until they come
            var appt = (S.Requests ?? new List<Request>()).Where(r => r.Kind == "invite" && r.State == "accepted").OrderBy(r => r.At).FirstOrDefault();
            if (appt != null && appt.At - S.Clock < 90)
            {
                if (me.Room != appt.Room) MoveToRoom(sim, appt.Room, lr);
                var tgt = sim.UntilTargets().FirstOrDefault(e => e.Kind == "appointment" && e.Room == appt.Room);
                double until = Math.Max(appt.At, S.Clock + 1) ; if (S.Clock >= appt.At) until = appt.At + 25;
                var p = sim.PlanUntil(Math.Min(until, appt.At + 25), "약속까지", out var why);
                if (p != null) Run(p, "appt"); else Run(sim.PlanWait(5, out _), "wait5");   // the window already closed (e.g. the trial took the time)
                if (appt.State == "met") { st.MetInvites++; var host = S.A(appt.From); if (host != null && host.Alive) { var yes = sim.PlayerTogether(host, "join"); if (yes != null && !yes.Interrupted && yes.Minutes > 0) st.TogetherYes++; st.TogetherScenes++; } }
                else if (appt.State == "missed") st.MissedInvites++;
                continue;
            }
            int m = S.Minute;
            // night: to bed
            if (m >= 22 * 60 || m < 6 * 60 + 30)
            {
                var bed = S.Layout.BedroomOf(Cast.Player);
                if (bed != null && me.Room != bed.Id) MoveToRoom(sim, bed.Id, lr);
                var sp = sim.PlanSleep(out var why);
                if (sp == null) { Fail("PlanSleep: " + why); Run(sim.PlanWait(30, out _), "wait"); continue; }
                var r0 = Run(sp, "sleep"); st.Sleeps++;
                if (r0 != null && r0.Stop != null && r0.Stop.Kind == StopKind.Target && S.Minute < 7 * 60) Fail("woke before 7:00");
                if (!resumeChecked && r0 != null && r0.Interrupted && S.Minute < 6 * 60 + 30) { resumeChecked = true; var rp = sim.ResumePlan(sp); Console.WriteLine($"  resume after {r0.Stop?.Kind}: {(rp != null ? rp.Kind + " to " + ClockFmt.HM(rp.Target) : "none")}"); if (rp != null) Run(rp, "resume"); }
                continue;
            }
            // the breakfast bell once in the run: a save made on the bell's minute does not ring it again on load
            if (!loadBellChecked && m == 8 * 60 && S.Announcements.Count > 0 && S.Announcements[S.Announcements.Count - 1].Key == "y_meal_breakfast")
            {
                loadBellChecked = true;
                var json = SaveStore.Serialize(S); var back = SaveStore.Deserialize(json); var sim2 = Simulation.FromState(back); sim2.Headless = true; sim2.OnDemand = true;
                int n0 = sim2.S.Announcements.Count; for (int i = 0; i < 20; i++) sim2.Step();
                bool ok = sim2.S.Announcements.Count == n0;
                Console.WriteLine($"  load-at-bell: announcements {n0} → {sim2.S.Announcements.Count} ({(ok ? "no replay" : "REPLAYED")})");
                if (!ok) Fail("bell replayed after load");
            }
            // one scream next door during a wait: an emergency that runs the clock until calm
            if (!emergencyChecked && S.Day >= 1 && m > 10 * 60 && m < 17 * 60)
            {
                var nb = S.Layout.Rooms.Where(r => r.Id != me.Room && r.Floor == me.Pos.f && S.LivingNpcs.Any(x => x.Room == r.Id && x.Status == ActorStatus.Active)).OrderBy(r => Math.Abs(r.Rect.CX - me.Pos.x) + Math.Abs(r.Rect.CZ - me.Pos.z)).FirstOrDefault();
                if (nb != null)
                {
                    emergencyChecked = true;
                    var wp = sim.PlanWait(60, out _); sim.BeginSkip(wp); sim.StepSkip(wp, 5); bool screamed = sim.ProbeScream(nb.Id); sim.StepSkip(wp, 3);
                    var res = sim.EndSkip(wp); string emerg = sim.EmergencyWhy();
                    Console.WriteLine($"  scream probe in {nb.Name}: screamed={screamed} stop={wp.Stop?.Kind} '{wp.Stop?.Title}' emergency='{emerg}'");
                    if (screamed && (wp.Stop == null || wp.Stop.Kind != StopKind.Heard)) Console.WriteLine("  note: the scream was not heard from here (too far) — no stop expected");
                    else if (screamed && emerg == null) Fail("heard a scream but no emergency");
                    continue;
                }
            }
            // a still NPC steps aside
            if (!yieldChecked)
            {
                // (the first standing person for whom there is room to step aside; a Game-only call, zero time)
                int tried = 0, ok = 0; string how = "";
                foreach (var y in S.LivingNpcs.Where(x => x.Status == ActorStatus.Active && x.StairId < 0 && x.Spot < 0 && x.Pos.f == me.Pos.f && x.TalkingTo == null && x.Carrying == null).OrderBy(x => x.Id).Take(8))
                {
                    tried++; var from = new P3(y.Pos.f, y.Pos.x + 1.2f, y.Pos.z); var p0 = y.Pos; long tk = S.Tick;
                    if (!sim.YieldTo(y, from)) continue;
                    ok++; float d = p0.DistXZ(y.Pos); how = $"{y.Id} moved {d:0.00} m in {S.RoomName(y.Room)}";
                    if (d < 0.75f || d > 1.25f || S.Tick != tk) Fail("yield distance/time " + how);
                    break;
                }
                if (tried > 0) { yieldChecked = true; Console.WriteLine($"  yield: {ok}/{tried} {how}"); if (ok == 0) Fail("nobody could step aside"); }
            }
            // ---------------- choose what to do (the cycle a player might follow)
            int c = cycle++ % 6;
            var mealSlot = -1; bool mealNow = sim.MealTime(out mealSlot) && S.Minute - Simulation.MealStart[mealSlot] < 40 && !S.Flags.ContainsKey($"ate:{Cast.Player}:{S.Day}:{mealSlot}");
            if (mealNow)
            {
                // eat at the table (or with someone who is eating)
                var din = S.Layout.First(RoomType.Dining);
                if (din != null)
                {
                    if (me.Room != din.Id) MoveToRoom(sim, din.Id, lr);
                    var diner = S.LivingNpcs.Where(x => x.Room == din.Id && x.Act?.Id == "life:eat" && x.Status == ActorStatus.Active).OrderBy(x => x.Pos.DistXZ(me.Pos)).FirstOrDefault();
                    if (diner != null && lr.Chance(0.5)) { var r = sim.PlayerTogether(diner, "meal"); st.TogetherScenes++; if (r != null && r.Minutes > 0) { st.TogetherYes++; st.Eats++; if (verbose || r.Stop?.Kind != StopKind.Target) Console.WriteLine($"  meal with {diner.Id}: {r.Minutes:0}min {r.Stop?.Kind} {r.Activity?.Text}"); } else S.Flags[$"ate:{Cast.Player}:{S.Day}:{mealSlot}"] = 1; continue; }
                    var chair = din.Furniture.Select(i => S.Layout.Furniture[i]).Where(f => f.Type == "Chair" && sim.FurnitureActions(f).Any(a => a.Id == "eat")).OrderBy(f => f.Id).FirstOrDefault(f => S.Layout.Spots.Any(sp => sp.Furniture == f.Id && sp.Occupant == null && sp.OnFurniture));
                    if (chair != null)
                    {
                        MoveNear(sim, chair.Pos); sim.PlayerSit(chair, S.Player.Pos);
                        var pp = sim.PlanPastime(chair, "eat", out var why);
                        if (pp != null) { var r = Run(pp, "eat"); st.Eats++; st.Pastimes++; }
                        else { S.Flags[$"ate:{Cast.Player}:{S.Day}:{mealSlot}"] = 1; }
                        if (sim.PlayerSeated) sim.PlayerStand();
                        continue;
                    }
                }
            }
            if (c == 0 || c == 3)
            {
                // the smart row: the next thing worth waiting for
                var e = sim.NextEvent(S.Clock);
                if (e != null && e.At - S.Clock < 8 * 60) { var p = sim.PlanUntil(e.At, e.Text + "까지", out var why); if (p != null) { if (lr.Chance(0.2)) Run(p, "next-esc:" + e.Kind, 20 * (10 + lr.R(30))); else Run(p, "next:" + e.Kind); } else Fail("smart row: " + why); }
                else Run(sim.PlanWait(60, out _), "wait60");
            }
            else if (c == 1)
            {
                // now and then the player stops a wait part-way (Esc)
                var wp = sim.PlanWait(lr.Chance(0.5) ? 30 : 60, out _);
                if (lr.Chance(0.5)) Run(wp, "wait-esc", 20 * (5 + lr.R(15))); else Run(wp, "wait");
            }
            else if (c == 2)
            {
                // time together with someone nearby (or in the nearest busy room)
                var who = S.LivingNpcs.Where(x => x.Status == ActorStatus.Active && x.Pose != Pose.Sleep && x.StairId < 0 && (x.Act == null || x.Act.Interruptible) && x.TalkingTo == null)
                    .OrderBy(x => x.Pos.Dist(me.Pos)).ThenBy(x => x.Id).FirstOrDefault();
                if (who == null) { Run(sim.PlanWait(30, out _), "wait"); continue; }
                if (who.Room != me.Room || who.Pos.DistXZ(me.Pos) > 4f) MoveNear(sim, who.Pos);
                Talk(who);
                if (!sim.CanTalk(who, out _)) continue;
                var kinds = sim.TogetherOptions(who).Where(o => o.Enabled).Select(o => o.Id).ToList();
                if (kinds.Count == 0) continue;
                string kind = kinds[lr.R(kinds.Count)];
                var r = sim.PlayerTogether(who, kind); st.TogetherScenes++;
                if (r != null && r.Minutes > 0) st.TogetherYes++;
                if (verbose && r != null) Console.WriteLine($"  together {who.Id} {kind}: {r.Minutes:0}min {r.Stop?.Kind} {r.Title} | {r.Activity?.Text}");
            }
            else if (c == 4)
            {
                // a pastime in a room that has one
                var f = S.Layout.Furniture.Where(x => { var rr = S.Layout.Room(x.Room); return rr != null && (rr.Owner == null || rr.Owner == Cast.Player) && rr.Type != RoomType.Courtroom && sim.RoomUsable(S.Player, rr) && sim.FurnitureActions(x).Any(a => a.Pastime && a.Id != "eat" && a.Id != "nap"); })
                    .OrderBy(x => x.Room == me.Room ? 0 : 1).ThenBy(x => lr.F()).FirstOrDefault();
                if (f == null) { Run(sim.PlanWait(30, out _), "wait"); continue; }
                MoveNear(sim, f.Pos);
                var act = sim.FurnitureActions(f).First(a => a.Pastime && a.Id != "eat" && a.Id != "nap");
                var pp = sim.PlanPastime(f, act.Id, out var why);
                if (pp == null) { if (verbose) Console.WriteLine($"  pastime {f.Type}/{act.Id}: {why}"); Run(sim.PlanWait(30, out _), "wait"); continue; }
                var r = Run(pp, "pastime"); st.Pastimes++;
                if (verbose && r?.Activity != null) Console.WriteLine($"     → {r.Activity.Text}");
                if (sim.PlayerSeated) sim.PlayerStand();
            }
            else
            {
                // wander to another room and look around (zero time), then a short wait
                var rooms = S.Layout.Rooms.Where(r => !RoomInfo.IsPassage(r.Type) && r.Type != RoomType.Courtroom && sim.RoomUsable(S.Player, r) && r.Type != RoomType.Bedroom).OrderBy(r => r.Id).ToList();
                if (rooms.Count > 0) MoveToRoom(sim, rooms[lr.R(rooms.Count)].Id, lr);
                Run(sim.PlanWait(15, out _), "wait15");
            }
        }
        CountAnn();
        Console.WriteLine($"loop exit: clock {ClockFmt.DayHM(S.Clock)} phase {S.Phase} iterations {guardLoops}");
        if (guardLoops >= 200000) Fail("the test player stopped passing time");
        int metTotal = (S.Requests ?? new List<Request>()).Count(r => r.State == "met" || r.State == "done");
        Console.WriteLine();
        Console.WriteLine($"SKIPS {st.Skips}: " + string.Join(", ", st.ByKind.OrderByDescending(x => x.Value).Select(x => x.Key + "=" + x.Value)));
        Console.WriteLine($"STOPS: " + string.Join(", ", st.ByStop.OrderByDescending(x => x.Value).Select(x => x.Key + "=" + x.Value)));
        Console.WriteLine($"natural={st.Natural} stopped={st.Stopped} guard={st.Guard} critical={st.Critical} phase={st.PhaseStops} moved={st.Moved}");
        Console.WriteLine($"OVERSHOOT settle n={st.Settles} max={st.MaxSettle:0.0}min (≤5) · meal n={st.Meals} max={st.MaxMeal:0.0}min (≤10) · deferral n={st.Deferred} max={st.MaxDefer:0.0}min (≤30) · natural-end overshoot max={st.MaxOvershoot:0.0}min");
        Console.WriteLine($"HOT at stop delivery: {st.HotAtEnd}/{st.Skips} ({100.0 * st.HotAtEnd / Math.Max(1, st.Skips):0.0}%)");
        Console.WriteLine($"APPOINTMENTS accepted={st.AcceptedInvites} met(seen)={st.MetInvites} met/done(total)={metTotal} missed={(S.Requests ?? new List<Request>()).Count(r => r.State == "missed")} stops={st.Appointment}");
        Console.WriteLine($"TOGETHER scenes={st.TogetherScenes} yes={st.TogetherYes} · pastimes={st.Pastimes} · eats={st.Eats} · sleeps={st.Sleeps} · talks={talkCount}");
        Console.WriteLine("BELLS " + string.Join(", ", annCount.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value)));
        Console.WriteLine($"CASE first death {(st.FirstDeathClock >= 0 ? ClockFmt.DayHM(st.FirstDeathClock) : "none")} · case opened {(st.FirstCaseClock >= 0 ? ClockFmt.DayHM(st.FirstCaseClock) : "never")} after {st.SkipsToFirstCase} skips · trials={st.Trials}");
        Console.WriteLine($"PERF skip ticks={st.SkipTicks} ms={st.SkipMs:0} ms/tick={st.SkipMs / Math.Max(1, st.SkipTicks):0.000} · investigation ticks={ticksInv}");
        Console.WriteLine($"CANCEL (Esc part-way) n={st.Cancels} deferred while a plan was hot={st.CancelDeferred} max ran on={st.MaxCancelDefer:0.0}min (≤30)");
        Console.WriteLine($"DAY-START SETTLE n={st.SettleRuns} min={(st.SettleRuns > 0 ? st.MinSettle : 0):0.0}min max={st.MaxSettleRun:0.0}min (≥4, ≤30)");
        if (st.MaxCancelDefer > 30.2) Fail($"cancel deferral ran {st.MaxCancelDefer:0.0} min");
        if (st.MaxSettle > 5.2) Fail($"settle overshoot {st.MaxSettle:0.0}");
        if (st.MaxMeal > 10.2) Fail($"meal overshoot {st.MaxMeal:0.0}");
        if (st.MaxDefer > 30.2) Fail($"deferral {st.MaxDefer:0.0}");
        if (st.Approach + st.MealStops == 0) Fail("no Approach or Meal stop seen");
        if (!annCount.ContainsKey("y_meal_breakfast") || !annCount.ContainsKey("y_meal_dinner") || !annCount.ContainsKey("y_morning") || !annCount.ContainsKey("y_night")) Fail("a daily bell never rang");
        if (st.FirstCaseClock < 0 || st.FirstCaseClock > 4 * 1440)
        {
            Fail("no case within 4 days");
            // for the murder owner: what the planners tried while the player passed time (plans made, aborted and why)
            foreach (var l in S.DevLog.Where(x => x.Contains("PLAN ") || x.Contains("abort ")).Take(16)) Console.WriteLine("   " + l);
            foreach (var pl in S.Plans.Values.OrderBy(x => x.Id).Take(8)) Console.WriteLine($"   plan {pl.Id} {pl.Actor}->{pl.Target} stage {pl.Stage} step {pl.Step}/{pl.Steps.Count}");
        }
        if (st.Trials == 0 && days >= 4) Fail("no trial reached");
        if (!loadBellChecked) Console.WriteLine("  note: load-at-bell check did not run");
        if (sim.Faults > 0) Fail($"faults={sim.Faults}");
        { var json = SaveStore.Serialize(S); var back = SaveStore.Deserialize(json); Console.WriteLine($"save roundtrip={(SaveStore.Serialize(back) == json ? "IDENTICAL" : "DIFF")}"); }
        Console.WriteLine($"faults={sim.Faults}"); foreach (var l in S.DevLog.Where(x => x.Contains("EXC") || x.Contains("GUARD")).Take(8)) Console.WriteLine("   " + l);
        Console.WriteLine($"timeflow done fails={fails} clock={ClockFmt.DayHM(S.Clock)} time={sw.ElapsedMilliseconds}ms");
        return fails == 0 ? 0 : 1;
    }

    static void MoveNear(Simulation sim, P3 p)
    {
        var S = sim.S; if (sim.PlayerSeated) sim.PlayerStand();
        var q = sim.SnapPublic(new P3(p.f, p.x + 0.9f, p.z + 0.4f));
        sim.SetPlayerPose(q, 0, false, false);
    }

    static void MoveToRoom(Simulation sim, int room, Rng lr)
    {
        var S = sim.S; var r = S.Layout.Room(room); if (r == null) return; if (sim.PlayerSeated) sim.PlayerStand();
        var q = sim.SnapPublic(sim.RandomPointIn(r, lr));
        sim.SetPlayerPose(q, 0, false, false);
        sim.PlayerPerceive();
    }

    /// <summary>Every person × every kind of time together, each from the same saved moment (a weekday lunchtime).</summary>
    static int TogetherTest(string[] args)
    {
        ulong seed = args.Length > 1 ? ulong.Parse(args[1]) : 20260926UL;
        var sim0 = Simulation.NewCampaign(seed, 4); sim0.Headless = true; sim0.OnDemand = true; sim0.EndPrologue();
        var S0 = sim0.S;
        // to lunch on day 1 (12:35): a meal window for "meal"
        while (S0.Minute < 12 * 60 + 35) sim0.Step();
        string baseJson = SaveStore.Serialize(S0);
        int fails = 0, cases = 0, yes = 0, no = 0, partnerLeft = 0;
        void Fail(string m) { fails++; Console.WriteLine("  FAIL " + m); }
        Simulation Fresh() { var s = Simulation.FromState(SaveStore.Deserialize(baseJson)); s.Headless = true; s.OnDemand = true; return s; }
        Room RoomFor(Simulation sim, string kind, Actor npc)
        {
            var L = sim.S.Layout;
            if (kind == "tea") return L.Rooms.Where(r => TogetherActs.TeaRooms.Contains(r.Type) && r.Type != RoomType.Dining).OrderBy(r => r.Id).FirstOrDefault() ?? L.First(RoomType.Dining);
            if (kind == "meal") return L.First(RoomType.Dining);
            return L.Room(npc.Room);
        }
        var ids = S0.LivingNpcs.Select(a => a.Id).OrderBy(x => x).ToList();
        Console.WriteLine($"together seed {seed} at {ClockFmt.DayHM(S0.Clock)} people {ids.Count}");
        foreach (var id in ids)
        {
            var line = new List<string>();
            foreach (var kind in new[] { "talk_long", "tea", "meal", "join" })
            {
                var sim = Fresh(); var S = sim.S; var npc = S.A(id); var me = S.Player;
                if (npc.Status != ActorStatus.Active) continue;
                // the test places the actors: both in a fitting room, standing close; the NPC not busy, fairly friendly
                var room = RoomFor(sim, kind, npc);
                if (room == null) { line.Add(kind + ":noroom"); continue; }
                if (kind == "join")
                {
                    var hob = npc.Def.Hobbies.Select(h => Activities.HobbyToActivity.TryGetValue(h, out var x) ? x : null).FirstOrDefault(x => x != null && !TogetherActs.NotJoinable.Contains(x) && x != "cook") ?? "read";
                    var act = sim.Simple(npc, hob); if (act == null) act = sim.Simple(npc, "read");
                    if (act != null) sim.Assign(npc, act);
                    room = S.Layout.Room(npc.Room);
                }
                else
                {
                    var pt = sim.SnapPublic(sim.RandomPointIn(room, new Rng(7, 7)));
                    npc.Pos = pt; npc.Room = room.Id; npc.Act = null; npc.TalkingTo = null; if (npc.Spot >= 0) { S.Layout.Spots[npc.Spot].Occupant = null; npc.Spot = -1; } npc.Pose = Pose.Stand;
                }
                sim.SetPlayerPose(sim.SnapPublic(new P3(npc.Pos.f, npc.Pos.x + 1.0f, npc.Pos.z)), 0, false, false);
                var rel = S.R(id, Cast.Player); rel.Like = Math.Max(rel.Like, 0.3f); rel.Grudge = 0; rel.Fear = 0; npc.Needs.Energy = 0.8f; npc.Needs.Stress = 0.2f;
                // as a player would: open a conversation (which makes them put down what they were doing), pick the option there
                sim.BeginTalk(npc);
                var dlg = sim.Options(npc).FirstOrDefault(o => o.Id == "together");
                bool inDialogue = dlg != null && dlg.Sub != null && dlg.Sub.Any(s => s.id == kind);
                sim.EndTalk(npc);
                var opt = sim.TogetherOptions(npc).FirstOrDefault(o => o.Id == kind);
                if (opt != null && opt.Enabled && !inDialogue) Fail($"{id} {kind}: enabled but missing from the dialogue");
                if (opt == null || !opt.Enabled) { line.Add(kind + ":off(" + opt?.Why + ")"); continue; }
                cases++;
                float like0 = rel.Like, trust0 = rel.Trust;
                double c0 = S.Clock;
                var res = sim.PlayerTogether(npc, kind);
                double mins = S.Clock - c0;
                if (res == null) { Fail($"{id} {kind}: null result"); continue; }
                bool said = res.Lines.Any(l => l.StartsWith("bye|"));
                if (!said) { no++; line.Add($"{kind}:NO({res.Title})"); Fail($"{id} {kind}: refused though friendly ({res.Title})"); continue; }
                yes++;
                float dl = S.R(id, Cast.Player).Like - like0;
                if (res.Stop?.Kind == StopKind.PartnerLeft) partnerLeft++;
                bool fullRun = res.Stop?.Kind == StopKind.Target;
                if (fullRun && Math.Abs(mins - opt.Minutes) > 1.0 + 5.0 + 0.2 && mins < opt.Minutes - 1) Fail($"{id} {kind}: {mins:0.0} min for {opt.Minutes}");
                if (fullRun && dl <= 0) Fail($"{id} {kind}: like did not rise ({dl:0.000})");
                line.Add($"{kind}:{mins:0}m {res.Stop?.Kind} +{dl:0.000}");
            }
            Console.WriteLine($"  {id} {Cast.GivenOf(id)}: " + string.Join("  ", line));
        }
        // refusals: ordinary causes, the same for everyone (a plan changes nothing)
        {
            var sim = Fresh(); var S = sim.S; var npc = S.A(ids[0]); var me = S.Player;
            sim.SetPlayerPose(sim.SnapPublic(new P3(npc.Pos.f, npc.Pos.x + 1.0f, npc.Pos.z)), 0, false, false);
            npc.Act = null; npc.TalkingTo = null;
            var rel = S.R(npc.Id, Cast.Player); rel.Like = 0; rel.Trust = 0;
            var r1 = sim.PlayerTogether(npc, "talk_long");
            bool noLine = r1.Lines.Any(l => l.StartsWith("invite_no|"));
            npc.PlanId = "plan-fake";
            var r2 = sim.PlayerTogether(npc, "talk_long");
            npc.PlanId = null;
            bool same = r2.Lines.Any(l => l.StartsWith("invite_no|"));
            rel.Like = 0.4f; npc.Needs.Energy = 0.1f;
            var r3 = sim.PlayerTogether(npc, "talk_long"); bool sleepy = r3.Lines.Any(l => l.StartsWith("sleepy|"));
            npc.Needs.Energy = 0.8f;
            var r4 = sim.PlayerTogether(npc, "talk_long"); bool ok4 = r4.Lines.Any(l => l.StartsWith("bye|"));
            var r5 = sim.PlayerTogether(npc, "talk_long"); bool recent = r5.Lines.Any(l => l.StartsWith("talked_recently|"));
            Console.WriteLine($"refusals: not-close → invite_no {noLine} · with a plan the same {same} · tired → sleepy {sleepy} · friendly → yes {ok4} · again at once → talked_recently {recent}");
            if (!noLine || !same || !sleepy || !ok4 || !recent) Fail("refusal lines");
        }
        // per-day diminishing: 1, .5, .25 (the "just spent time together" hour is waived for the test)
        {
            var sim = Fresh(); var S = sim.S; var npc = S.A(ids[1]);
            var tea = S.Layout.Rooms.Where(r => TogetherActs.TeaRooms.Contains(r.Type)).OrderBy(r => r.Id).First();
            var deltas = new List<float>();
            for (int i = 0; i < 3; i++)
            {
                npc.Pos = sim.SnapPublic(sim.RandomPointIn(tea, new Rng(3, 3))); npc.Room = tea.Id; npc.Act = null; npc.TalkingTo = null; npc.Needs.Energy = 0.8f; npc.Needs.Stress = 0.1f;
                sim.SetPlayerPose(sim.SnapPublic(new P3(npc.Pos.f, npc.Pos.x + 1.0f, npc.Pos.z)), 0, false, false);
                var rel = S.R(npc.Id, Cast.Player); rel.Like = 0.3f; rel.Grudge = 0; float l0 = rel.Like;
                S.Flags.Remove("togat:" + npc.Id);
                var r = sim.PlayerTogether(npc, "tea");
                deltas.Add(S.R(npc.Id, Cast.Player).Like - l0);
                Console.WriteLine($"  tea #{i + 1}: {r.Minutes:0} min stop {r.Stop?.Kind} like +{deltas[i]:0.0000}");
            }
            bool dim = deltas[0] > 0 && Math.Abs(deltas[1] / deltas[0] - 0.5f) < 0.12f && Math.Abs(deltas[2] / deltas[0] - 0.25f) < 0.08f;
            Console.WriteLine($"diminishing: {string.Join(" / ", deltas.Select(d => (d / Math.Max(1e-6f, deltas[0])).ToString("0.00")))} {(dim ? "OK" : "WRONG")}");
            if (!dim) Fail("diminishing");
        }
        // the partner is called away mid-scene → PartnerLeft
        {
            var sim = Fresh(); var S = sim.S; var npc = S.A(ids[2]);
            npc.Act = null; npc.TalkingTo = null; npc.Needs.Energy = 0.8f; S.R(npc.Id, Cast.Player).Like = 0.3f;
            sim.SetPlayerPose(sim.SnapPublic(new P3(npc.Pos.f, npc.Pos.x + 1.0f, npc.Pos.z)), 0, false, false);
            var tp = new TogetherPlan { Npc = npc.Id, Id = "talk_long", Activity = "socialize", Label = "천천히 이야기를 나눈다", Minutes = 30 };
            var p = sim.PlanTogether(tp, out var why);
            if (p == null) Fail("PlanTogether: " + why);
            else
            {
                sim.BeginSkip(p); sim.StepSkip(p, 100);
                var other = sim.Simple(npc, "walk"); if (other != null) sim.Assign(npc, other); else sim.Interrupt(npc, 0);
                sim.StepSkip(p, 400);
                var r = sim.EndSkip(p);
                Console.WriteLine($"partner called away: stop {p.Stop?.Kind} '{p.Stop?.Title}' after {r.Minutes:0.0} min · {r.Activity?.Text}");
                if (p.Stop == null || p.Stop.Kind != StopKind.PartnerLeft) Fail("PartnerLeft not detected");
            }
        }
        Console.WriteLine($"together done cases={cases} yes={yes} no={no} partnerLeft(in cases)={partnerLeft} fails={fails}");
        return fails == 0 ? 0 : 1;
    }
}
