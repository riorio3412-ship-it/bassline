using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BL23.Sim;

public static partial class Program
{
    static int Extra(string mode, string[] args)
    {
        if (mode == "campaign") return Campaign(args);
        if (mode == "census") return Census(args);
        if (mode == "rescue") return RescueTest(args);
        if (mode == "activities") return ActivitiesTest(args);
        if (mode == "mystery") return Mystery(args);
        if (mode == "clues") return CluesAudit(args);
        if (mode == "trialdump") return TrialDump(args);   // TrialDump.cs: trial transcript + boredom metrics per player policy
        if (mode == "textdump") return TextDump(args);     // TextDump.cs: every distinct rendered player-facing string → BL23Lab/textdump
        if (mode == "timeflow") return TimeFlowTest(args); // TimeFlowTests.cs: an on-demand player (time passes only in skips)
        if (mode == "together") return TogetherTest(args); // TimeFlowTests.cs: every person × every kind of time together
        if (mode == "tickcost") return TickCost(args);     // TickCost.cs: kernel ms/tick, allocations/tick, cost per stage of Step
        if (mode == "voice") return VoiceTest(args);       // VoiceTests.cs: bark repetition / shared-template rate / naming (DailyLifeDesign §11)
        if (mode == "witness") return WitnessTest(args);   // WitnessTest.cs: the three-witness rule before any death announcement
        if (mode == "conceal") return ConcealTest(args);   // ConcealTest.cs: hiding things on the body / in furniture, witnesses, NPC searches
        if (mode == "violence") return ViolenceTest(args); // ViolenceTest.cs: prolonged kills, restraint, firearms, crossbow, dragging (violence track)
        if (mode == "initiative") return InitiativeTest(args); // Initiative.cs: the proactive culprit mind — variety, preparation, rule shields, trial packs
        if (mode == "life") return LifeTest(args);         // LifeTests.cs: daily life — pair scenes, table, hearts, hangouts, festivals, rumours, aftermath (DailyLifeDesign §11)
        if (mode == "furnknow") return FurnitureKnowledgeTest(args); // FurnitureKnowledgeTest.cs: one furniture change → fact / sight / hearing / later notice, save/load
        if (mode == "deckdump") return DeckDump(args);     // DebateDump.cs: the 은판 deck of the first 심판
        if (mode == "deckgate") return DeckGate(args);     // DebateDump.cs: decks over several seeds
        if (mode == "debate") return DebateRun(args);      // DebateDump.cs: the debate 심판 played headless, as a transcript
        Console.WriteLine("unknown mode " + mode); return 1;
    }

    /// <summary>Run a full headless campaign: daily life → autonomous crime → discovery → investigation → trial → settlement → next chapter/loop.</summary>
    static int Campaign(string[] args)
    {
        ulong seed = args.Length > 1 ? ulong.Parse(args[1]) : 20260926UL;
        int maxDays = args.Length > 2 ? int.Parse(args[2]) : 20;
        bool verbose = args.Contains("v");
        var sw = Stopwatch.StartNew();
        var fri = Array.IndexOf(args, "force"); if (fri >= 0 && fri + 1 < args.Length) Rules.ForceRule = args[fri + 1];
        var spi = Array.IndexOf(args, "trick"); if (spi >= 0 && spi + 1 < args.Length) SetPieces.Force = args[spi + 1];
        var sim = Simulation.NewCampaign(seed, args.Contains("floor5") ? 5 : 4);
        sim.Headless = true;
        var S = sim.S;
        S.Phase = Phase.Daily;
        Console.WriteLine("owned: " + string.Join(",", S.Items.Values.Where(i => i.Owner != null && i.KeyFor == null).Select(i => i.Owner + ":" + i.Type + ":" + (i.Holder ?? "-")))); 
        Console.WriteLine($"seed {seed} layout {S.Layout.Hash} skel {S.Layout.Skeleton} mystery {string.Join(",", S.Layout.MysteryTypes)} abilities {string.Join(" ", S.Abilities.Select(a => a.Actor + ":" + a.Ability))}");
        var gram = new Dictionary<string, int>(); var seenSeq = new HashSet<long>(); var grammars = new Dictionary<string, int>();
        string[] G = { "Lend", "Handover", "LeaveLoan", "ItemMissingNoticed", "ItemFound", "Return", "LendCoat", "GatheringPlanned", "GatheringRevised", "NoteDelivered", "InviteAnswer", "GatheringArrive", "GatheringLeave", "GatheringReturn", "GatheringStoodUp", "GatheringEnd", "GatheringExplained", "TrapArmed", "TrapFired", "TrapFound", "TrapDisarmed", "RecorderArmed", "RecorderPlay", "EchoRecord", "EchoPlay", "GuiseAs", "CourierAsk", "NoteRead", "CourierRealised", "FaultFound", "RepairWork", "ClockAdjust", "PostmortemDamage", "PublicInquiry", "TableSeat", "NoticeDelivered", "StatementSealed", "RuleStart", "KeyShift", "SealedRoom", "SealedFound", "FakeMessage", "PlantWeapon", "Stoke", "TodShift", "Dose", "SawNearCup", "Comfort", "TeaFor", "Confront", "HouseToll" };
        void Scan() { foreach (var e in S.Ledger) if (seenSeq.Add(e.Seq) && G.Contains(e.Type)) gram[e.Type] = (gram.TryGetValue(e.Type, out var n0) ? n0 : 0) + 1; foreach (var pl in S.Plans.Values) if (seenSeq.Add(-1000000 - long.Parse(pl.Id.Substring(4)))) grammars[pl.Grammar] = (grammars.TryGetValue(pl.Grammar, out var n1) ? n1 : 0) + 1; }
        long ticks = 0; int lastDay = 0; int trials = 0; int loops = 1; int devIdx = 0;
        double endClock = maxDays * 1440;
        while (S.Clock < endClock + S.Loop * 0 && ticks < 30_000_000)
        {
            if (S.Phase == Phase.Trial)
            {
                trials++; Scan();
                // the test player auto-investigates only what it could plausibly have examined: bodies + scene traces
                TrialSystem.RunHeadless(sim, true); TrialDebug(sim);
                var T = S.Trial;
                { var cl = CaseProgress.Clues(sim); Console.WriteLine($"  CLUES {cl.Count}: " + string.Join(", ", cl.Select(c => c.Key.Split(':')[0] + (c.Found ? "*" : "")))); }
                var st = S.Settlements.LastOrDefault();
                Console.WriteLine($"  TRIAL L{S.Loop}C{S.Chapter}: beats={T?.Beats.Count} claims={T?.Claims.Count} refuted={T?.Claims.Count(c => c.Status == "refuted")} modes={string.Join(",", T?.ModesSeen ?? new List<string>())} → {S.LastVerdictSummary}");
                if (verbose && T != null) foreach (var b in T.Beats.Take(80)) Console.WriteLine($"     [{b.Kind}] {Cast.GivenOf(b.Speaker) ?? ""}: {b.Text}");
                Replay.BuildSegments(sim);
                foreach (var seg in S.Replays.Where(r => S.Incidents.TryGetValue(r.Incident, out var inc) && inc.Chapter == S.Chapter && inc.Loop == S.Loop))
                {
                    var script = Replay.Script(S, seg);
                    Console.WriteLine($"  REVEAL {seg.Incident}: frames={seg.Frames.Count} events={seg.Events.Count}");
                    { var inc0 = S.Incidents[seg.Incident]; var pl0 = inc0.PlanId != null && S.Plans.TryGetValue(inc0.PlanId, out var pz) ? pz : null; if (pl0 != null) Console.WriteLine("     PLAN " + pl0.Grammar + " steps=" + string.Join(",", pl0.Steps.Select(s => s.Kind + (s.Done ? "*" : ""))) + " | " + string.Join(" / ", pl0.Log)); }
                    foreach (var line in script.Take(verbose ? 60 : 14)) Console.WriteLine($"     {ClockFmt.HM(line.clock)} {line.text}");
                }
                Settlements.AfterReveal(sim);
                {
                    int bad = 0;
                    foreach (var x in S.LivingNpcs)
                    {
                        var bed = S.Layout.BedroomOf(x.Id); if (bed == null) continue;
                        var pr = Pathfinder.Find(S.Layout, x.Pos, sim.RandomPointIn(bed, S.R(Stream.Presentation)), sim.DoorCostFor(x));
                        if (pr.Ok) continue;
                        bad++; var g = S.Layout.Nav(x.Pos.f); int kc = g.CellOf(x.Pos.x, x.Pos.z);
                        if (bad <= 3) Console.WriteLine($"  NOPATH {x.Id} at {x.Pos} room {S.RoomName(x.Room)} walkable={g.Walkable(kc)} cellRoom={(kc >= 0 ? g.Room[kc] : -9)} bed={bed.Name}");
                    }
                    Console.WriteLine($"  after-reveal path check: {bad} actors cannot reach their bedroom");
                }
                if (S.Phase == Phase.LoopEpilogue)
                {
                    Console.WriteLine($"== LOOP {S.Loop} END (survivors {S.Survivors}) ==");
                    Settlements.NextLoop(sim); S.Phase = Phase.Daily; loops++;
                    endClock = S.Clock + maxDays * 1440; // continue in the new loop
                    Console.WriteLine($"== LOOP {S.Loop} START layout {S.Layout.Hash} skel {S.Layout.Skeleton} mystery {string.Join(",", S.Layout.MysteryTypes)}");
                    if (loops > 3) break;
                }
                continue;
            }
            if (S.Phase == Phase.Investigation && !S.Flags.ContainsKey("testinv:" + S.Chapter + ":" + S.Loop))
            {
                S.Flags["testinv:" + S.Chapter + ":" + S.Loop] = 1;
                foreach (var inc in S.Incidents.Values.Where(i => i.Confirmed && i.Chapter == S.Chapter && i.Loop == S.Loop))
                {
                    var body = S.A(inc.Victim); Evidences.ExamineBody(sim, S.Player, body, true);
                    foreach (var t in S.Traces.Where(t => t.Room == body.Room || t.Room == inc.CauseRoom)) Evidences.ExamineTrace(sim, S.Player, t);
                    Evidences.ExamineRoomQuick(sim, S.Player, S.Layout.First(RoomType.Kitchen).Id);
                }
                foreach (var npc in S.LivingNpcs.ToList()) { var w = Testimony.Where(sim, npc, Cast.Player); if (w.Prop != null) sim.Learn(Cast.Player, npc.Id, w.Prop, "(test)", w.Lie, false); foreach (var s in Testimony.Saw(sim, npc, Cast.Player, 2)) if (s.Prop != null) sim.Learn(Cast.Player, npc.Id, s.Prop, "(test)", s.Lie, false); }
            }
            sim.Step(); ticks++; if (ticks % 20000 == 0) Scan();
            if (ticks % 10 == 0 && S.Phase == Phase.Daily && S.Minute >= 9 * 60 && S.Minute < 21 * 60) Wander(S);
            if (S.Day != lastDay) { lastDay = S.Day; PlanReport(S); PressureReport(sim); Console.WriteLine($"-- day {S.Day} L{S.Loop}C{S.Chapter} phase {S.Phase} alive {S.Survivors} plans {S.Plans.Values.Count(p => p.Stage != "Done" && p.Stage != "Aborted")} rules {string.Join(",", S.Ch.Rules.Select(r => r.Rule))} t={sw.ElapsedMilliseconds}ms"); }
            if (devIdx > S.DevLog.Count) devIdx = Math.Max(0, S.DevLog.Count - 500);
            while (devIdx < S.DevLog.Count) { var l = S.DevLog[devIdx++]; if (verbose || l.Contains("EXC") || l.Contains("PLAN") || l.Contains("DEATH") || l.Contains("abort") || l.Contains("SETTLE") || l.Contains("HUNGER") || l.Contains("LOOP") || l.Contains("chapter")) Console.WriteLine("   " + l); }
            if (S.Out.Count > 2000) S.Out.Clear();
        }
        Scan();
        Console.WriteLine("GRAMMAR EVENTS: " + string.Join(", ", G.Select(g => g + "=" + (gram.TryGetValue(g, out var n) ? n : 0))));
        Console.WriteLine("PLAN GRAMMARS: " + string.Join(", ", grammars.OrderByDescending(x => x.Value).Select(x => x.Key + "=" + x.Value)));
        WanderReport();
        foreach (var x in S.LivingNpcs)
        {
            var room = S.Layout.Room(x.Room); if (room == null) continue;
            var to = sim.RandomPointIn(room, S.R(Stream.Presentation));
            var pr = Pathfinder.Find(S.Layout, x.Pos, to, sim.DoorCostFor(x));
            if (pr.Ok) continue;
            var g = S.Layout.Nav(x.Pos.f); int kc = g.CellOf(x.Pos.x, x.Pos.z); int sn = Pathfinder.Snap(g, x.Pos); int gn = Pathfinder.Snap(S.Layout.Nav(to.f), to);
            Console.WriteLine($"  STUCK {x.Id} pos {x.Pos} room {room.Name} cell {kc} walk={g.Walkable(kc)} snap={sn} goal={gn} goalWalk={(gn >= 0 && S.Layout.Nav(to.f).Walkable(gn))} carriedBy={x.CarriedBy} carrying={x.Carrying} stair={x.StairId} pose={x.Pose}");
        }
        foreach (var pl in S.Plans.Values.Where(p => p.Stage == "Aborted").Take(8)) Console.WriteLine($"  ABORTED {pl.Actor}->{pl.Target} {pl.Grammar} at step {pl.Step}:{(pl.Step < pl.Steps.Count ? pl.Steps[pl.Step].Kind : "-")} | {string.Join(" / ", pl.Log.Skip(1))}");
        Console.WriteLine($"faults={sim.Faults}"); foreach (var l in S.DevLog.Where(x => x.Contains("EXC")).Take(5)) Console.WriteLine("   " + l);
        Console.WriteLine($"done ticks={ticks} trials={trials} loops={loops} time={sw.ElapsedMilliseconds}ms clock={ClockFmt.DayHM(S.Clock)}");
        // save/load roundtrip
        StrikeReport(S); SizeReport(S); var json = SaveStore.Serialize(S); var back = SaveStore.Deserialize(json);
        var json2 = SaveStore.Serialize(back);
        Console.WriteLine($"save json={json.Length / 1024}KB roundtrip={(json == json2 ? "IDENTICAL" : "DIFF")}");
        var tmpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bl23savetest"); System.IO.Directory.CreateDirectory(tmpDir); var sp = System.IO.Path.Combine(tmpDir, "slot1.sav");
        SaveStore.Write(sp, S, "test"); SaveStore.Write(sp, S, "test2"); var fileKB = new System.IO.FileInfo(sp).Length / 1024; var rd = SaveStore.Read(sp, out var note1); Console.WriteLine($"file save={fileKB}KB read={(rd != null && SaveStore.Serialize(rd) == json ? "IDENTICAL" : "DIFF")} info={SaveStore.Info(sp).Label}");
        var bytes = System.IO.File.ReadAllBytes(sp); bytes[bytes.Length / 2] ^= 0x5A; System.IO.File.WriteAllBytes(sp, bytes); var rd2 = SaveStore.Read(sp, out var note2); Console.WriteLine($"corrupt → fallback={(rd2 != null ? "previous OK" : "none")} note={note2}");
        return 0;
    }
}

public static partial class Program
{
    static readonly System.Collections.Generic.Dictionary<string, int> _wRoom = new System.Collections.Generic.Dictionary<string, int>();
    static readonly System.Collections.Generic.Dictionary<string, int> _wWhy = new System.Collections.Generic.Dictionary<string, int>();
    static long _wSamples, _wChanges, _wPassage, _wMoving, _wHaunt, _cPairs, _cOverlap, _cTight;
    /// <summary>Daytime (09-21) wandering metric: room changes per NPC-hour, share of time in passages / walking / at the haunt.</summary>
    static void Wander(GameState S)
    {
        foreach (var a in S.LivingNpcs)
        {
            if (a.Status != ActorStatus.Active) continue;
            _wSamples++;
            if (_wRoom.TryGetValue(a.Id, out var r) && r != a.Room) { _wChanges++; var room0 = S.Layout.Room(a.Room); if (room0 != null && !RoomInfo.IsPassage(room0.Type)) { string id = a.Act?.Id ?? "(none)"; int c = id.IndexOfAny(new[] { ':' }, id.StartsWith("life:") || id.StartsWith("goal:") || id.StartsWith("social:") || id.StartsWith("case:") ? id.IndexOf(':') + 1 : 0); if (id.StartsWith("social:") || id.StartsWith("alt:") || id.StartsWith("inv:") || id.StartsWith("X_")) c = id.IndexOf(':'); string k0 = id.StartsWith("social:join") ? "social:join" : id.StartsWith("social:") ? "social:talk" : c > 0 ? id.Substring(0, c) : id; _wWhy[k0] = (_wWhy.TryGetValue(k0, out var n0) ? n0 : 0) + 1; } }
            _wRoom[a.Id] = a.Room;
            var room = S.Layout.Room(a.Room); if (room != null && RoomInfo.IsPassage(room.Type)) _wPassage++;
            if (a.Speed > 0.1f) _wMoving++;
            int m = S.Minute; int block = m < 12 * 60 ? 0 : m < 18 * 60 ? 1 : 2;
            if (S.Flags.TryGetValue($"haunt:{a.Id}:{S.Day}:{block}", out var h) && (int)h == a.Room) _wHaunt++;
        }
        // crowding: pairs of standing people closer than 0.45 m (bodies overlapping) or 0.7 m (uncomfortably tight)
        var st = S.Actors.Values.Where(x => x.Alive && x.Status == ActorStatus.Active && x.StairId < 0 && x.CarriedBy == null && x.Pose == Pose.Stand).ToList();
        for (int i = 0; i < st.Count; i++) for (int j = i + 1; j < st.Count; j++) { if (st[i].Pos.f != st[j].Pos.f) continue; float d = st[i].Pos.DistXZ(st[j].Pos); if (d > 3f) continue; _cPairs++; if (d < 0.45f) _cOverlap++; else if (d < 0.7f) _cTight++; }
    }
    static void WanderReport()
    {
        if (_wSamples == 0) return;
        // one sample per NPC every 10 ticks = 1 sim second = 0.5 clock minutes
        double npcHours = _wSamples * 0.5 / 60.0;
        Console.WriteLine($"WANDER roomChanges/NPC-hour={_wChanges / npcHours:0.00} passage={100.0 * _wPassage / _wSamples:0.0}% moving={100.0 * _wMoving / _wSamples:0.0}% atHaunt={100.0 * _wHaunt / _wSamples:0.0}%");
        Console.WriteLine($"CROWD near-pairs(<3m)={_cPairs} overlapping(<0.45m)={_cOverlap} ({100.0 * _cOverlap / Math.Max(1, _cPairs):0.0}%) tight(<0.7m)={_cTight} ({100.0 * _cTight / Math.Max(1, _cPairs):0.0}%)");
        Console.WriteLine("  entries by activity: " + string.Join(", ", _wWhy.OrderByDescending(x => x.Value).Take(16).Select(x => x.Key + "=" + x.Value)));
    }
}
