using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BL23.Sim;

/// <summary>
/// Murder-content verification (BL23 second wave, Methods*.cs):
///   mystery &lt;seeds,comma,separated | N&gt; &lt;days&gt;            — unforced campaigns: variety of methods / weapons / tricks / rooms, per-case clue & trial report
///   mystery forced &lt;days&gt; [Kind,Kind,…]                    — each kind forced (SetPieces.Force) until it is executed once: steps, traces, clues, claim broken?
/// A "thorough" test player examines the body, every trace and every out-of-place object in the found room and the room where the attack began,
/// sweeps those rooms, and asks everyone where they were and what they saw — then the headless trial runs with the smart player.
/// </summary>
public static partial class Program
{
    static readonly string[] MyEvents = { "Garrote", "Scratched", "Shove", "ButtonTorn", "ShoveFailed", "Sedate", "Drowsy", "Doze", "Smother", "FakeNote", "KeySlide", "PoisonPlant", "PoisonTaken", "ShockRigged", "ShockFired", "BreakerTrip", "BreakerReset", "Burn", "DumpWater", "Bury", "NoiseMask", "HeldUnder", "ColdHide",
                                          "SealedRoom", "TodShift", "FakeMessage", "PlantWeapon", "Dose", "TrapFired", "Dismember", "PartHidden", "PieceSeen" };
    // a forced kind counts as proven only when its signature ledger event happened (not merely planned)
    static readonly Dictionary<string, string> Sig = new Dictionary<string, string> { ["Strangle"] = "Garrote", ["Push"] = "Shove", ["Shock"] = "ShockFired", ["Smother"] = "Smother", ["Bedtime"] = "PoisonTaken", ["FakeNote"] = "FakeNote", ["KeySlide"] = "KeySlide", ["Burn"] = "Burn", ["Dump"] = "DumpWater", ["Bury"] = "Bury", ["Noise"] = "NoiseMask", ["ColdHide"] = "ColdHide", ["Dismember"] = "Dismember", ["Drown"] = "HeldUnder", ["Seal"] = "SealedRoom", ["Tod"] = "TodShift", ["Message"] = "FakeMessage", ["Swap"] = "PlantWeapon", ["Poison"] = "Dose" };
    static bool Proves(string line, string wanted)
    {
        if (!Sig.TryGetValue(wanted, out var ev)) return line.Contains(wanted);
        var evs = line.Split(" | ").FirstOrDefault(p => p.StartsWith("events:"));
        return evs != null && evs.Substring(7).Split(',').Select(x => x.Trim()).Contains(ev);
    }
    static readonly string[] KeyProps = { "ligature", "nail-scrape", "smother-marks", "sedated", "push-bruise", "fall-injuries", "electrocuted", "temp-cold", "held-under", "key-slid", "scorch", "soil-dug", "wet-trail", "ash-fresh", "edge-scuff", "powder",
                                          "cord-stretched", "torn-button", "burnt-remnant", "pillow-pressed", "sedative-residue", "sedative-used", "poisoned-personal", "farewell-note", "handwriting-mismatch", "key-on-floor", "burnt", "waterlogged", "buried", "insulation-shavings",
                                          "cable-stripped", "poisoned", "thread-under-door", "temp-warm", "instant-death", "smeared", "poison-residue", "postmortem-cut", "saw-marks", "drain-blood", "bone-dust", "part-hidden", "burnt-bone" };

    sealed class MysteryStats
    {
        public Dictionary<string, int> Formed = new Dictionary<string, int>(), Executed = new Dictionary<string, int>(), Weapons = new Dictionary<string, int>(), Dmg = new Dictionary<string, int>(), Layers = new Dictionary<string, int>(), Rooms = new Dictionary<string, int>(), Claims = new Dictionary<string, int>(), Broken = new Dictionary<string, int>();
        public int Incidents, Trials, Faults, Correct; public List<string> Fails = new List<string>(); public List<int> CluesPerCase = new List<int>(), CardsPerCase = new List<int>();
        public void Add(Dictionary<string, int> d, string k) { if (k == null) return; d[k] = (d.TryGetValue(k, out var n) ? n : 0) + 1; }
    }

    static int Mystery(string[] args)
    {
        var sw = Stopwatch.StartNew();
        if (args.Length > 1 && args[1] == "forced")
        {
            int days = args.Length > 2 ? int.Parse(args[2]) : 8;
            var kinds = args.Length > 3 ? args[3].Split(',') : new[] { "Strangle", "Push", "Shock", "Smother", "Bedtime", "FakeNote", "KeySlide", "Burn", "Dump", "Bury", "Noise", "ColdHide", "Dismember", "Drown", "Poison", "Seal", "Tod", "Message", "Swap" };
            ulong[] seeds = { 20260926UL, 777UL, 1234UL, 90210UL, 4242UL, 31337UL };
            int ok = 0, faults = 0; var summary = new List<string>();
            foreach (var kind in kinds)
            {
                bool done = false;
                foreach (var seed in seeds)
                {
                    var st = new MysteryStats();
                    var hit = RunMystery(seed, days, kind, st, true, kind);
                    faults += st.Faults;
                    if (hit != null) { summary.Add($"  {kind,-9} seed {seed,-9} OK  {hit}"); ok++; done = true; break; }
                    Console.WriteLine($"   ({kind}: not executed on seed {seed}; formed={string.Join(",", st.Formed.Select(kv => kv.Key + ":" + kv.Value))})");
                }
                if (!done) summary.Add($"  {kind,-9} NOT EXECUTED on {seeds.Length} seeds");
            }
            SetPieces.Force = null;
            Console.WriteLine("\n==== FORCED SUMMARY ====");
            foreach (var l in summary) Console.WriteLine(l);
            Console.WriteLine($"forced ok={ok}/{kinds.Length} faults={faults} time={sw.ElapsedMilliseconds}ms");
            return faults == 0 ? 0 : 1;
        }
        {
            var seeds = args.Length > 1 && args[1].Contains(",") ? args[1].Split(',').Select(ulong.Parse).ToArray() : Enumerable.Range(0, args.Length > 1 ? int.Parse(args[1]) : 4).Select(i => 5000UL + (ulong)i * 7919UL).ToArray();
            int days = args.Length > 2 ? int.Parse(args[2]) : 8;
            var st = new MysteryStats();
            foreach (var seed in seeds) RunMystery(seed, days, null, st, true, null);
            Console.WriteLine("\n==== VARIETY ====");
            Console.WriteLine($"seeds={seeds.Length} days={days} incidents={st.Incidents} trials={st.Trials} correctVerdicts={st.Correct} faults={st.Faults}");
            void Dump(string name, Dictionary<string, int> d) => Console.WriteLine($"{name,-10} ({d.Count}): " + string.Join(", ", d.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value)));
            Dump("formed", st.Formed); Dump("executed", st.Executed); Dump("weapons", st.Weapons); Dump("death", st.Dmg); Dump("layers", st.Layers); Dump("rooms", st.Rooms); Dump("claims", st.Claims); Dump("broken", st.Broken);
            if (st.CluesPerCase.Count > 0) Console.WriteLine($"clue props per case (key facts the player found): avg {st.CluesPerCase.Average():0.0} max {st.CluesPerCase.Max()} | case cards avg {st.CardsPerCase.Average():0.0} max {st.CardsPerCase.Max()}");
            foreach (var f in st.Fails) Console.WriteLine("  FAIL " + f);
            Console.WriteLine($"time={sw.ElapsedMilliseconds}ms");
            return st.Faults == 0 ? 0 : 1;
        }
    }

    /// <summary>One campaign. Returns a one-line proof when <paramref name="wanted"/> (a grammar head or layer) was executed.</summary>
    static string RunMystery(ulong seed, int maxDays, string force, MysteryStats st, bool verbose, string wanted)
    {
        SetPieces.Force = force;
        var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
        Console.WriteLine($"\n#### seed {seed}{(force != null ? " force " + force : "")} layout {S.Layout.Hash} [{S.Layout.GenLog.LastOrDefault(l => l.StartsWith("items settled")) ?? "no settle log"}] rooms:{string.Join(",", S.Layout.Rooms.Where(r => r.Type == RoomType.Incinerator || r.Type == RoomType.ColdStorage || r.Type == RoomType.Darkroom).Select(r => r.Name))} armory={S.Items.Keys.Count(k => k.StartsWith("it_m"))}");
        var seenPlans = new HashSet<string>(); string proof = null; long ticks = 0; int loops = 1; var sweep2 = new Dictionary<string, double>();
        double endClock = maxDays * 1440;
        while (S.Clock < endClock && ticks < 20_000_000)
        {
            foreach (var p in S.Plans.Values) if (seenPlans.Add(p.Id)) st.Add(st.Formed, p.Grammar.Split('+')[0].Split('→')[0]);
            if (S.Phase == Phase.Investigation && !S.Flags.ContainsKey("myinv:" + S.Chapter + ":" + S.Loop))
            {
                S.Flags["myinv:" + S.Chapter + ":" + S.Loop] = 1;
                foreach (var inc in S.Incidents.Values.Where(i => i.Confirmed && i.Chapter == S.Chapter && i.Loop == S.Loop).ToList()) Investigate(sim, inc);
                foreach (var npc in S.LivingNpcs.ToList()) { var w = Testimony.Where(sim, npc, Cast.Player); if (w.Prop != null) sim.Learn(Cast.Player, npc.Id, w.Prop, "(test)", w.Lie, false); foreach (var s in Testimony.Saw(sim, npc, Cast.Player, 2)) if (s.Prop != null) sim.Learn(Cast.Player, npc.Id, s.Prop, "(test)", s.Lie, false); }
            }
            // a second, later sweep: things disposed of after the first look (a furnace, the pump, fresh soil) are still there to find
            if (S.Phase == Phase.Investigation && S.Flags.TryGetValue("myinv:" + S.Chapter + ":" + S.Loop, out var inv1) && inv1 < 2)
            {
                string k2 = "inv2at:" + S.Chapter + ":" + S.Loop;
                if (!sweep2.TryGetValue(k2, out var at2)) sweep2[k2] = S.Clock + 45;
                else if (S.Clock >= at2) { S.Flags["myinv:" + S.Chapter + ":" + S.Loop] = 2; foreach (var inc in S.Incidents.Values.Where(i => i.Confirmed && i.Chapter == S.Chapter && i.Loop == S.Loop).ToList()) Investigate(sim, inc); }
            }
            if (S.Phase == Phase.Trial)
            {
                st.Trials++;
                TrialSystem.RunHeadless(sim, true);
                var T = S.Trial;
                var inc = TrialSystem.TargetIncident(S);
                foreach (var i in S.Incidents.Values.Where(x => x.Loop == S.Loop && x.Chapter == S.Chapter).OrderBy(x => x.ResultSeq))
                {
                    var r = Report(sim, i, T, st, verbose, i == inc);
                    if (wanted != null && proof == null && Proves(r.line, wanted)) proof = r.line;
                }
                var set = S.Settlements.LastOrDefault(); if (set != null && set.Correct) st.Correct++;
                Console.WriteLine($"  VERDICT seed {seed} L{S.Loop}C{S.Chapter} correct={(set != null && set.Correct ? "Y" : "n")} valid={T?.Valid} invalid={T?.Invalid} :: {S.LastVerdictSummary}");
                Replay.BuildSegments(sim);
                Settlements.AfterReveal(sim);
                if (S.Phase == Phase.LoopEpilogue) { Settlements.NextLoop(sim); S.Phase = Phase.Daily; loops++; endClock = S.Clock + maxDays * 1440; if (loops > 2) break; }
                if (proof != null && wanted != null) break;
                continue;
            }
            sim.Step(); ticks++;
            if (S.Out.Count > 2000) S.Out.Clear();
        }
        st.Faults += sim.Faults;
        foreach (var l in S.DevLog.Where(x => x.Contains("EXC")).Take(5)) Console.WriteLine("   " + l);
        // save/load must survive the new state
        var json = SaveStore.Serialize(S); var back = SaveStore.Deserialize(json);
        Console.WriteLine($"  faults={sim.Faults} roundtrip={(json == SaveStore.Serialize(back) ? "IDENTICAL" : "DIFF")} clock={ClockFmt.DayHM(S.Clock)}");
        if (json != SaveStore.Serialize(back)) st.Faults++;
        SetPieces.Force = null;
        return proof;
    }

    /// <summary>The thorough test player's look at a confirmed case.</summary>
    static void Investigate(Simulation sim, Incident inc)
    {
        var S = sim.S; var P = S.Player; var body = S.A(inc.Victim); if (body == null) return;
        Evidences.ExamineBody(sim, P, body, true);
        var rooms = new[] { body.Room, inc.CauseRoom, inc.FoundRoom }.Where(r => r >= 0).Distinct().ToList();
        foreach (var t in S.Traces.Where(t => rooms.Contains(t.Room) || t.Victim == inc.Victim).ToList()) Evidences.ExamineTrace(sim, P, t);
        foreach (var r in rooms) Evidences.ExamineRoomQuick(sim, P, r);
        foreach (var it in S.Items.Values.Where(i => i.Holder == null && !i.Hidden && rooms.Contains(i.Room) && (i.Surface.Count > 0 || i.Note != null || i.Bloody)).ToList()) Evidences.ExamineItem(sim, P, it);
        foreach (var f in S.Layout.Furniture.Where(f => rooms.Contains(f.Room) && (f.Marks.Count > 0 || f.Type == "Switchboard")).ToList()) { string d = f.Type; var props = new List<Prop>(); Grammars.ExamineFurniture(sim, P, f, ref d, props); if (props.Count > 0) Evidences.Add(sim, P.Id, EvKind.ObjectState, f.Type, d, "직접 조사", "tf:" + f.Id, S.Clock, S.Clock, f.Room, "", "", true, props.ToArray()); }
        Evidences.ExamineRoomQuick(sim, P, S.Layout.First(RoomType.Kitchen).Id);
        // a machine that died, a switchboard, the furnace: the kind of place a thorough player checks when the scene points there
        var pr = S.Layout.First(RoomType.PowerRoom); if (pr != null && S.Flags.Keys.Any(k => k.StartsWith("breaker:"))) Evidences.ExamineRoomQuick(sim, P, pr.Id);
        // a thorough player also sweeps the rooms that help a crime (water room filter, greenhouse soil, cold store)
        foreach (var rt in new[] { RoomType.WaterRoom, RoomType.Greenhouse, RoomType.ColdStorage }) { var ur = S.Layout.First(rt); if (ur != null) Evidences.ExamineRoomQuick(sim, P, ur.Id); }
        var ir = S.Layout.First(RoomType.Incinerator); if (ir != null && S.Traces.Any(t => t.Room == ir.Id && t.Type == "Ash")) { Evidences.ExamineRoomQuick(sim, P, ir.Id); foreach (var f in ir.Furniture.Select(i => S.Layout.Furniture[i]).Where(f => f.Type == "Incinerator")) { string d = f.Type; var props = new List<Prop>(); Grammars.ExamineFurniture(sim, P, f, ref d, props); Evidences.Add(sim, P.Id, EvKind.ObjectState, "소각로", d, "직접 조사", "tfi:" + f.Id, S.Clock, S.Clock, f.Room, "", "", true, props.ToArray()); } foreach (var it in S.Items.Values.Where(i => i.Room == ir.Id && i.Holder == null && !i.Hidden).ToList()) Evidences.ExamineItem(sim, P, it); }
        // a thorough player opens the crates and lockers of the store rooms (the in-game "search" action): pieces of a body hidden there
        foreach (var it in S.Items.Values.Where(i => i.Holder == null && i.Hidden && i.Def?.Tag == "part" && i.Owner == inc.Victim).OrderBy(i => i.Id, StringComparer.Ordinal).ToList())
        {
            var rt = S.Layout.Room(it.Room)?.Type;
            if (rt == RoomType.Storage || rt == RoomType.ColdStorage || rt == RoomType.WineCellar || rt == RoomType.BoilerRoom || rt == RoomType.Closet) { it.Hidden = false; S.Log("ItemFound", P.Id, item: it.Id, room: it.Room, data: "search"); Evidences.ExamineItem(sim, P, it); }
        }
        foreach (var rt in new[] { RoomType.Pool }) { var ur = S.Layout.First(rt); if (ur != null) Evidences.ExamineRoomQuick(sim, P, ur.Id); }
    }

    static (string executed, string line) Report(Simulation sim, Incident inc, TrialState T, MysteryStats st, bool verbose, bool tried = true)
    {
        var S = sim.S; st.Incidents++;
        var plan = inc.PlanId != null && S.Plans.TryGetValue(inc.PlanId, out var p) ? p : null;
        string g = plan?.Grammar ?? inc.Method ?? "-"; var parts = g.Split('+'); string head = parts[0].Split('→').Last();
        st.Add(st.Executed, head); foreach (var l in parts.Skip(1)) st.Add(st.Layers, l);
        st.Add(st.Weapons, inc.WeaponType ?? plan?.WeaponType ?? "(none)"); st.Add(st.Dmg, inc.Dmg.ToString());
        var found = S.Layout.Room(inc.FoundRoom); st.Add(st.Rooms, found?.Type.ToString());
        // clue props the player holds for this case
        var myEv = S.K(Cast.Player).Evidence.Where(e => e.Loop == S.Loop && e.Chapter == S.Chapter).ToList();
        var caseRooms = new[] { inc.FoundRoom, inc.CauseRoom, inc.DeathRoom }.Where(r => r >= 0).ToList();
        var props = myEv.SelectMany(e => e.Props).Where(pp => pp.Value != null && KeyProps.Contains(pp.Value.Split(':')[0]) && (pp.A == inc.Victim || pp.A == inc.Culprit || caseRooms.Contains(pp.Room) || pp.Room < 0 && pp.A == null || (pp.Item != null && pp.Item == (inc.WeaponType ?? plan?.WeaponType)))).Select(pp => pp.Value.Split(':')[0]).Distinct().ToList();
        st.CluesPerCase.Add(props.Count); st.CardsPerCase.Add(myEv.Count);
        var mine = S.Ledger.Where(e => MyEvents.Contains(e.Type) && (e.Target == inc.Victim || e.Actor == inc.Culprit || e.Actor == inc.Victim) && e.Clock >= inc.DeathClock - 60 * 30 && e.Clock <= inc.DeathClock + 180).Select(e => e.Type).Distinct().ToList();
        string trick = TrialSystem.TrickOf(S, inc);
        var claims = T?.Claims.Where(c => c.Prop != null && ((c.Prop.Kind == PropKind.Culprit && c.Prop.A == null && c.Prop.Value != null) || (c.Prop.Kind == PropKind.DoorLocked && c.Prop.Value == "sealed") || (c.Prop.Kind == PropKind.WeaponType && c.Prop.Item != null) || (c.Prop.Kind == PropKind.TraceAt && c.Prop.Value != null && c.Prop.Value.StartsWith("bloodwriting")) || (c.Prop.Kind == PropKind.DeathWindow && c.Prop.Value == "exam"))).ToList() ?? new List<TrialClaim>();
        foreach (var c in claims) { string key = c.Prop.Kind == PropKind.Culprit ? c.Prop.Value : c.Prop.Kind.ToString(); st.Add(st.Claims, key); if (c.Status == "refuted" || c.Status == "limited" || c.Status == "conditional") st.Add(st.Broken, key); }
        var rq = tried ? T?.RQ.FirstOrDefault(q => q.Id == "trick") : null;
        string line = $"{Cast.GivenOf(inc.Culprit ?? "-")}→{Cast.GivenOf(inc.Victim)} [{g}] weapon={inc.WeaponType ?? "-"} dmg={inc.Dmg} cause='{S.A(inc.Victim)?.Body.DeathCause}' found={found?.Name} | events: {string.Join(",", mine)} | clues: {string.Join(",", props)} | claims: {string.Join(" ; ", claims.Select(c => (c.Prop.Kind == PropKind.Culprit ? c.Prop.Value : c.Prop.Kind.ToString()) + "→" + c.Status + (c.RefuteWhy != null ? $"({c.RefuteWhy})" : "")))} | trick={trick ?? "-"} answer={(rq != null && rq.Answer >= 0 ? rq.Options[rq.Answer] : "-")}";
        Console.WriteLine("  CASE " + line);
        if (verbose && plan != null) Console.WriteLine("     PLAN " + string.Join(",", plan.Steps.Select(s => s.Kind + (s.Done ? "*" : ""))) + " | " + string.Join(" / ", plan.Log));
        if (verbose)
        {
            var seg = Replay.BuildSegments(sim).FirstOrDefault(s => s.Incident == inc.Id);
            if (seg != null) foreach (var c in Replay.Script(S, seg).Where(c => c.text != null && !c.text.StartsWith("  └ 목에") ).Take(28)) Console.WriteLine($"     {ClockFmt.HM(c.clock)} {c.text}");
        }
        string exec = head + "|" + string.Join("|", parts.Skip(1)) + "|" + string.Join("|", mine);
        return (exec, line);
    }
}
