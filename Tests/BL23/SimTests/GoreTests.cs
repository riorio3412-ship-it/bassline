using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using Newtonsoft.Json;

/// <summary>
/// `gore [seed] [kinds]` — kernel gore (Sim/Systems/Gore.cs). Stages each killing (stab, blunt, strangle, dismember) with
/// Gore.DebugStage on day 1 at 10:00 and prints what it left: marks, scene traces, pieces, wounds. Checks: 0 &lt; marks ≤ 48,
/// at most one BloodSpray/Struggle trace, save roundtrip IDENTICAL, two fresh runs identical, the dismembered pieces (4, the
/// asked parts, Sev-5 postmortem cuts) and their discovery by a third person, a non-lethal fight that spends no ids on gore,
/// and toppled furniture set right after the chapter turns.
/// </summary>
public static class GoreTests
{
    static int _fails;
    static void Check(bool ok, string what) { Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}"); if (!ok) _fails++; }

    public static int Run(string[] args)
    {
        _fails = 0;
        if (args.Length > 1 && args[1] == "scan") return Scan(args);
        ulong seed = args.Length > 1 && ulong.TryParse(args[1], out var s0) ? s0 : 20260926UL;
        var kinds = args.Length > 2 ? args[2].Split(',') : new[] { "stab", "blunt", "strangle", "dismember" };
        foreach (var kind in kinds) Case(seed, kind);
        AtBody(seed);
        NonLethal(seed);
        Console.WriteLine($"GORE {(_fails == 0 ? "PASS" : "FAIL")} fails={_fails}");
        return _fails == 0 ? 0 : 1;
    }

    static Simulation Fresh(ulong seed)
    {
        var sim = Simulation.NewCampaign(seed); sim.Headless = true; sim.S.Phase = Phase.Daily;
        while (sim.S.Clock < 10 * 60) sim.Step();   // day 1, 10:00
        sim.S.Out.Clear();
        return sim;
    }

    static string Fmt(GoreMark m, GameState S)
    {
        string extra = m.Furniture >= 0 ? $" furn={S.Layout.Furniture[m.Furniture].Type}#{m.Furniture}" : "";
        if (m.Door >= 0) extra += $" door={m.Door}";
        if (m.Item != null) extra += $" item={m.Item}";
        if (m.Trace != null) extra += $" trace={m.Trace}";
        return $"{m.Kind,-11} pos={m.Pos} r={m.Room} H={m.H:0.00} dir={m.Dir:0} pitch={m.Pitch:0} pow={m.Power:0.00} {m.Region}/{m.Dmg}/{m.Sev}{(m.Postmortem ? " PM" : "")} seed={m.Seed:X8}{extra}";
    }

    static void Case(ulong seed, string kind)
    {
        Console.WriteLine($"== gore {kind} (seed {seed}, day 1 10:00) ==");
        var sim = Fresh(seed); var S = sim.S;
        var trIds0 = new HashSet<string>(S.Traces.Select(t => t.Id));
        string vid = Gore.DebugStage(sim, kind);
        Check(vid != null, "DebugStage returned a victim");
        if (vid == null) return;
        var v = S.A(vid); var marks = Gore.MarksOf(v);
        var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == vid && i.Loop == S.Loop);
        Console.WriteLine($"  victim {vid} ({Cast.GivenOf(vid)}) status={v.Status} pose={v.Pose} cause={v.Body.DeathCause} by={v.Body.DeathBy} room={S.RoomName(v.Room)} pos={v.Pos} yaw={v.Yaw:0} bloodLoss={v.Body.BloodLoss:0.00}");
        Console.WriteLine($"  incident {inc?.Id} murder={inc?.Murder} weapon={inc?.WeaponType} mutilated={inc?.Mutilated} notes=[{string.Join("; ", inc?.Notes ?? new List<string>())}]");
        Console.WriteLine($"  struggle score={Gore.StruggleScore(sim, v)} marks={marks.Count} rev={v.Body.GoreRev}");
        foreach (var m in marks) Console.WriteLine("    " + Fmt(m, S));
        foreach (var w in v.Body.Wounds) Console.WriteLine($"    wound: {WoundText.Describe(w)} [{w.Region}/{w.Type}/{w.Sev}{(w.Postmortem ? " PM" : "")}]");
        var newTr = S.Traces.Where(t => !trIds0.Contains(t.Id)).ToList();
        foreach (var t in newTr) Console.WriteLine($"    trace {t.Id} {t.Type} vis={t.Visibility} at {t.Pos} r={t.Room} size={t.Size:0.00} dir={t.Dir:0} \"{t.Desc}\" / {t.Know} / {t.Unknown}");
        var pieces = Gore.PiecesOf(S, vid).ToList();
        foreach (var p in pieces) Console.WriteLine($"    piece {p.Id} {p.Note} ({(Gore.TryPart(p, out var sp) ? Gore.Kor(sp) : "?")}) at {p.Pos} r={S.RoomName(p.Room)} yaw={p.Yaw:0} surface={string.Join("+", p.Surface)} hidden={p.Hidden}");
        foreach (var f in S.Layout.Furniture.Where(f => f.Marks.Any(x => x.StartsWith("넘어져")))) Console.WriteLine($"    toppled furniture {f.Type}#{f.Id} marks=[{string.Join(", ", f.Marks)}] spots={string.Join(",", S.Layout.Spots.Where(sp => sp.Furniture == f.Id).Select(sp => sp.Occupant ?? "-"))}");
        foreach (var l in S.DevLog.Where(l => l.Contains("GORE STAGE") || l.Contains("DEATH") || l.Contains("EXC")).TakeLast(4)) Console.WriteLine("    dev " + l);

        Check(!v.Alive, "victim is dead");
        Check(marks.Count > 0 && marks.Count <= Gore.MaxMarks, $"0 < marks ({marks.Count}) <= {Gore.MaxMarks}");
        int scene = newTr.Count(t => t.Type == "BloodSpray" || t.Type == "Struggle");
        Check(scene <= 1, $"scene traces BloodSpray/Struggle = {scene} (<= 1)");
        Check(sim.Faults == 0, $"faults = {sim.Faults}");
        if (kind == "stab") Check(marks.Any(m => m.Kind == GoreKind.Arterial) && marks.Any(m => m.Kind == GoreKind.CastOff) && marks.Any(m => m.Kind == GoreKind.Pool), "stab: arterial + cast-off + pool");
        if (kind == "blunt") Check(marks.Any(m => m.Kind == GoreKind.Impact) && marks.Any(m => m.Kind == GoreKind.Pool), "blunt: impact + pool");
        if (kind == "strangle") Check(marks.Count(m => m.Kind == GoreKind.NailScratch) >= 2 && !marks.Any(m => m.Kind == GoreKind.Pool), "strangle: nail scratches, no pool");

        // save roundtrip
        var json = SaveStore.Serialize(S); var json2 = SaveStore.Serialize(SaveStore.Deserialize(json));
        Check(json == json2, $"save roundtrip {(json == json2 ? "IDENTICAL" : "DIFF")}");

        // determinism: a second fresh run gives the same marks and pieces
        var sim2 = Fresh(seed); var vid2 = Gore.DebugStage(sim2, kind);
        string a1 = JsonConvert.SerializeObject(new { m = v.Body.GoreMarks, p = pieces, id = S.NextId });
        string a2 = JsonConvert.SerializeObject(new { m = sim2.S.A(vid2)?.Body.GoreMarks, p = Gore.PiecesOf(sim2.S, vid2).ToList(), id = sim2.S.NextId });
        Check(vid == vid2 && a1 == a2, "two fresh runs identical (marks, pieces, id counter)");

        if (kind == "dismember")
        {
            var want = new[] { "HandR", "ArmL", "LegR", "Head" };
            Check(pieces.Count == 4 && want.All(n => pieces.Any(p => p.Note == n)), $"pieces = {pieces.Count} [{string.Join(",", pieces.Select(p => p.Note))}]");
            var cuts = v.Body.Wounds.Where(w => w.CauseEvent == "dismember").ToList();
            Check(cuts.Count == 4 && cuts.All(w => w.Sev == 5 && w.Postmortem), $"dismember wounds: {cuts.Count}, all Sev 5 postmortem");
            Check(pieces.All(p => S.Layout.RoomAt(p.Pos) == v.Room && p.Room == v.Room), "pieces lie in the victim's room");
            Check(marks.Count(m => m.Kind == GoreKind.SawMark) == 4, "one saw mark per cut");
            Check(Gore.IsDismembered(S, vid), "IsDismembered");
            // a third person walks up to a piece: that is finding the body
            var culprit = v.Body.DeathBy;
            var o = S.LivingNpcs.Where(x => x.Status == ActorStatus.Active && x.Id != vid && x.Id != culprit && x.CarriedBy == null && x.Carrying == null && x.StairId < 0).OrderBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
            var piece = pieces.OrderBy(p => p.Pos.DistXZ(v.Pos)).Last();
            if (o != null && inc != null)
            {
                bool before = inc.Discovered;
                var g = S.Layout.Nav(piece.Pos.f); P3 at = piece.Pos; bool found = false;
                for (int b = 0; b < 16 && !found; b++)
                {
                    double r = b * Math.PI / 8; var q = new P3(piece.Pos.f, piece.Pos.x + (float)Math.Sin(r) * 1.5f, piece.Pos.z + (float)Math.Cos(r) * 1.5f);
                    int k = g.CellOf(q.x, q.z); if (k >= 0 && g.Walkable(k) && g.Room[k] == piece.Room) { at = q; found = true; }
                }
                sim.Interrupt(o, 5); o.Pos = at; o.Room = S.Layout.RoomAt(at); o.Yaw = MathX.AngleDeg(piece.Pos.x - at.x, piece.Pos.z - at.z); o.NextPerceive = S.Clock; o.Speed = 0;
                int ticks = 0; while (ticks < 20 && !inc.Discovered) { sim.Step(); ticks++; }
                bool pieceSeen = S.Ledger.Any(e => e.Type == "PieceSeen" && e.Actor == o.Id);
                Console.WriteLine($"    discovery: {o.Id} placed {at.DistXZ(piece.Pos):0.0} m from {piece.Note} → discovered={inc.Discovered} after {ticks} ticks (was {before}) pieceSeen={pieceSeen} foundRoom={S.RoomName(inc.FoundRoom)} discoverers={string.Join(",", inc.Discoverers)}");
                Check(inc.Discovered, "a third person finds the body within 20 ticks");
            }
            else Check(false, "no third person / incident for the discovery test");
        }

        if (kind == "stab")
        {
            var topples = marks.Where(m => m.Kind == GoreKind.Topple).ToList();
            if (topples.Count == 0) Console.WriteLine("    (no toppled furniture in this staging — righting not exercised)");
            else
            {
                S.Chapter++; sim.RunTicks(50);
                bool spotsClear = topples.All(m => !S.Layout.Spots.Any(sp => sp.Furniture == m.Furniture && sp.Occupant == "~toppled"));
                bool marksClear = topples.All(m => !S.Layout.Furniture[m.Furniture].Marks.Any(x => x.StartsWith("넘어져")));
                Check(spotsClear && marksClear && topples.All(m => m.Righted), $"righting: {topples.Count} toppled piece(s) set upright after the chapter turned");
            }
        }
    }

    /// <summary>`gore scan &lt;seeds,comma&gt; [days] [Force]` — unstaged murders: run each campaign up to its first trial (or the day
    /// limit; SetPieces.Force optional) and print the marks every death left. Checks faults, the mark cap and the roundtrip.</summary>
    static int Scan(string[] args)
    {
        var seeds = (args.Length > 2 ? args[2] : "20260926,777").Split(',').Select(ulong.Parse).ToList();
        int days = args.Length > 3 ? int.Parse(args[3]) : 6;
        if (args.Length > 4) SetPieces.Force = args[4];
        var kinds = new Dictionary<GoreKind, int>(); int deaths = 0;
        foreach (var seed in seeds)
        {
            var sim = Simulation.NewCampaign(seed); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
            while (S.Phase != Phase.Trial && S.Day <= days && S.Tick < 3_000_000) { sim.Step(); if (S.Out.Count > 2000) S.Out.Clear(); }
            Console.WriteLine($"== scan seed {seed}: stopped {ClockFmt.DayHM(S.Clock)} phase {S.Phase} faults={sim.Faults}");
            foreach (var v in S.Actors.Values.Where(a => !a.Alive && a.Body.Dead))
            {
                deaths++; var ms = Gore.MarksOf(v);
                var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == v.Id);
                Console.WriteLine($"  {v.Id} {v.Body.DeathCause} by {v.Body.DeathBy ?? "-"} in {S.RoomName(v.Room)} pose={v.Pose} method={inc?.Method} struggle={Gore.StruggleScore(sim, v)} marks={ms.Count}: {string.Join(", ", ms.GroupBy(m => m.Kind).Select(gk => gk.Key + "×" + gk.Count()))} pieces={Gore.PiecesOf(S, v.Id).Count()}");
                foreach (var m in ms) kinds[m.Kind] = (kinds.TryGetValue(m.Kind, out var n) ? n : 0) + 1;
                Check(ms.Count <= Gore.MaxMarks, $"{v.Id}: marks {ms.Count} <= {Gore.MaxMarks}");
            }
            foreach (var a in S.Actors.Values.Where(a => a.Alive && Gore.MarksOf(a).Count > 0)) Console.WriteLine($"  (alive) {a.Id} marks: {string.Join(", ", Gore.MarksOf(a).GroupBy(m => m.Kind).Select(gk => gk.Key + "×" + gk.Count()))}");
            Check(sim.Faults == 0, $"seed {seed}: faults = {sim.Faults}");
            var json = SaveStore.Serialize(S); Check(json == SaveStore.Serialize(SaveStore.Deserialize(json)), $"seed {seed}: roundtrip");
        }
        SetPieces.Force = null;
        Console.WriteLine($"SCAN deaths={deaths} marks by kind: {string.Join(", ", kinds.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value))}");
        Console.WriteLine($"GORE SCAN {(_fails == 0 ? "PASS" : "FAIL")} fails={_fails}");
        return _fails == 0 ? 0 : 1;
    }

    /// <summary>The CutUp drop-in: default parts (2–4 of the limbs) laid beside their joints; a second call never re-cuts a part;
    /// a blunt tool cannot cut (empty list).</summary>
    static void AtBody(ulong seed)
    {
        Console.WriteLine($"== gore Dismember AtBody (CutUp drop-in, seed {seed}) ==");
        var sim = Fresh(seed); var S = sim.S;
        var vid = Gore.DebugStage(sim, "stab"); var v = S.A(vid); var c = S.A(v.Body.DeathBy);
        var candle = new Item { Id = "gt_candle", Type = "Candlestick", Pos = v.Pos, Room = v.Room };
        Check(Gore.Dismember(sim, v, c, candle, SeverPlacement.AtBody).Count == 0, "a candlestick cannot cut: empty list");
        var saw = new Item { Id = "gt_saw", Type = "BoneSaw", Pos = v.Pos, Room = v.Room };
        var ids = Gore.Dismember(sim, v, c, saw, SeverPlacement.AtBody);
        var ps = ids.Select(S.I).ToList();
        foreach (var p in ps) Console.WriteLine($"    {p.Id} {p.Note} at {p.Pos} ({p.Pos.DistXZ(v.Pos):0.00} m from the body, pose {v.Pose}, yaw {v.Yaw:0}) piece-yaw={p.Yaw:0} room={S.RoomName(p.Room)}");
        Check(ids.Count >= 2 && ids.Count <= 4 && ps.All(p => new[] { "ArmL", "ArmR", "LegL", "LegR" }.Contains(p.Note)), $"default parts: {ids.Count} limbs [{string.Join(",", ps.Select(p => p.Note))}]");
        Check(ps.All(p => p.Room == v.Room && p.Pos.DistXZ(v.Pos) < 1.8f), "pieces lie beside the body, in its room");
        var again = Gore.Dismember(sim, v, c, saw, SeverPlacement.AtBody, new[] { SeverPart.ArmL, SeverPart.ArmR, SeverPart.LegL, SeverPart.LegR, SeverPart.Head });
        var notes = Gore.PiecesOf(S, vid).Select(p => p.Note).ToList();
        Check(notes.Distinct().Count() == notes.Count && again.Count == 5 - ids.Count, $"second call cuts only what is left (+{again.Count}: {string.Join(",", notes)})");
        Check(v.Body.Wounds.Count(w => w.CauseEvent == "dismember") == notes.Count && Gore.MarksOf(v).Count <= Gore.MaxMarks, "one Sev-5 cut per piece; marks within the cap");
    }

    /// <summary>Two light blows, nobody dies: the only ids spent are the kernel's own blood-drip/scuff traces; gore adds marks, no trace.</summary>
    static void NonLethal(ulong seed)
    {
        Console.WriteLine($"== gore non-lethal fight (seed {seed}) ==");
        var sim = Fresh(seed); var S = sim.S;
        var ppl = S.LivingNpcs.Where(x => x.Status == ActorStatus.Active && x.StairId < 0 && x.CarriedBy == null).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        var a = ppl[2]; var b = ppl[3];
        b.Pos = sim.SnapPublic(new P3(a.Pos.f, a.Pos.x + 0.5f, a.Pos.z)); b.Room = S.Layout.RoomAt(b.Pos);
        var trIds0 = new HashSet<string>(S.Traces.Select(t => t.Id)); int id0 = S.NextId; int m0 = Gore.MarksOf(a).Count; int items0 = S.Items.Count;
        sim.Strike(b.Id, a, BodyRegion.ArmL, DamageType.Stab, 1, null, "attack");
        sim.Strike(b.Id, a, BodyRegion.Chest, DamageType.Cut, 1, null, "attack");
        var newTr = S.Traces.Where(t => !trIds0.Contains(t.Id)).ToList();
        int spent = S.NextId - id0;
        Console.WriteLine($"  {b.Id} → {a.Id}: alive={a.Alive} marks {m0}→{Gore.MarksOf(a).Count} ids spent={spent} new traces={string.Join(",", newTr.Select(t => t.Type))}");
        foreach (var m in Gore.MarksOf(a)) Console.WriteLine("    " + Fmt(m, S));
        Check(a.Alive, "nobody died");
        Check(Gore.MarksOf(a).Count > m0, "the blows left marks");
        Check(spent == newTr.Count && S.Items.Count == items0, $"ids spent ({spent}) == the kernel's own traces ({newTr.Count}); no items");
        Check(newTr.All(t => t.Type == "BloodDrip" || t.Type == "Scuff"), "gore added no trace");

        // bleeding past a door leaves a handprint on its frame; bleeding out beside a wall leaves a handprint and a smear
        Console.WriteLine("== gore bleeding: door handprint, dying handprint ==");
        var c = ppl[4]; var door = S.Layout.Doors.Where(d => d.Pos.f == 0).OrderBy(d => d.Id).First();
        var near = S.Layout.Room(door.RoomA);
        var p0 = new P3(door.Pos.f, door.Pos.x + (door.AlongX ? 0f : (near.Rect.CX < door.Pos.x ? -0.45f : 0.45f)), door.Pos.z + (door.AlongX ? (near.Rect.CZ < door.Pos.z ? -0.45f : 0.45f) : 0f));
        sim.Interrupt(c, 5); c.Pos = p0; c.Room = S.Layout.RoomAt(p0);
        sim.Strike(ppl[5].Id, c, BodyRegion.Abdomen, DamageType.Stab, 2, null, "attack");
        c.Pos = p0; c.Room = S.Layout.RoomAt(p0); c.Speed = 0; if (c.Act != null) sim.Interrupt(c, 5);
        int dId = S.NextId; sim.RunTicks(10);
        var dp = Gore.MarksOf(c).Where(m => m.Kind == GoreKind.Handprint && m.Door >= 0).ToList();
        Console.WriteLine($"  {c.Id} bleed={c.Body.Bleed:0.000} at {c.Pos} door {door.Id} {door.Pos} dist={c.Pos.DistXZ(door.Pos):0.00} → door handprints: {string.Join("; ", dp.Select(m => Fmt(m, S)))}");
        Check(dp.Count >= 1 && dp.All(m => m.Door >= 0), "a bleeding person leaves a handprint on a door they pass");
        c.Body.BloodLoss = 0.5f; sim.Die(c, ppl[5].Id, "과다 출혈", c.Body.Wounds.LastOrDefault());
        var hp = Gore.MarksOf(c).Where(m => m.Door < 0 && (m.Kind == GoreKind.Handprint || m.Kind == GoreKind.Smear)).ToList();
        foreach (var m in Gore.MarksOf(c)) Console.WriteLine("    " + Fmt(m, S));
        Check(hp.Count(m => m.Kind == GoreKind.Handprint) == 1 && hp.Count(m => m.Kind == GoreKind.Smear) >= 1, "bleeding out by a wall: handprint + smear");
        Check(sim.Faults == 0, $"faults = {sim.Faults}");
    }
}
