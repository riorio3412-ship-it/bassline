using System; using System.Linq; using BL23.Sim; using Newtonsoft.Json;
public static partial class Program {
  public static void TrialDebug(Simulation sim) {
    var T = sim.S.Trial; if (T == null) { Console.WriteLine("   (trial null)"); return; }
    Console.WriteLine($"   stage={T.Stage} prompt={T.PendingPrompt} finished={T.Finished} cursor={T.Cursor}/{T.Beats.Count} votes={T.Votes.Count} accused={T.Accused}");
  }
  public static void SizeReport(GameState S) {
    Console.WriteLine("  size ledger=" + JsonConvert.SerializeObject(S.Ledger).Length/1024 + "KB replaybuf=" + JsonConvert.SerializeObject(S.ReplayBuf).Length/1024 + "KB replays=" + JsonConvert.SerializeObject(S.Replays).Length/1024 + "KB know=" + JsonConvert.SerializeObject(S.Know).Length/1024 + "KB flags=" + JsonConvert.SerializeObject(S.Flags).Length/1024 + "KB layout=" + JsonConvert.SerializeObject(S.Layout).Length/1024 + "KB rels=" + JsonConvert.SerializeObject(S.Rels).Length/1024 + "KB traces=" + S.Traces.Count + " ledgerN=" + S.Ledger.Count);
    Console.WriteLine("  know parts: ev=" + JsonConvert.SerializeObject(S.Know.Values.SelectMany(x => x.Evidence)).Length/1024 + "KB sight=" + JsonConvert.SerializeObject(S.Know.Values.SelectMany(x => x.Sightings)).Length/1024 + "KB heard=" + JsonConvert.SerializeObject(S.Know.Values.SelectMany(x => x.Heard)).Length/1024 + "KB stmt=" + JsonConvert.SerializeObject(S.Know.Values.SelectMany(x => x.Statements)).Length/1024 + "KB facts=" + JsonConvert.SerializeObject(S.Know.Values.SelectMany(x => x.Facts)).Length/1024 + "KB evN=" + S.Know.Values.Sum(x => x.Evidence.Count) + " top ev kinds: " + string.Join(",", S.Know.Values.SelectMany(x => x.Evidence).GroupBy(e => e.Source).OrderByDescending(g => g.Count()).Take(6).Select(g => g.Key + ":" + g.Count())));
    var k = S.Know.Values.First(); Console.WriteLine($"  know sample: sight={k.Sightings.Count} heard={k.Heard.Count} stmt={k.Statements.Count} ev={k.Evidence.Count} facts={k.Facts.Count} itemseen={k.ItemSeen.Count}");
    foreach (var g in S.Ledger.GroupBy(e => e.Type).OrderByDescending(g => g.Count()).Take(8)) Console.Write($" {g.Key}:{g.Count()}"); Console.WriteLine();
  }
  public static void PlanReport(GameState S) {
    foreach (var p in S.Plans.Values.Where(p => p.Stage != "Done" && p.Stage != "Aborted")) Console.WriteLine($"  plan {p.Id} {p.Actor}->{p.Target} {p.Grammar} step={p.Step}/{p.Steps.Count} {(p.Step < p.Steps.Count ? p.Steps[p.Step].Kind : "-")} tries={p.Tries} log: {string.Join(" | ", p.Log.TakeLast(4))} act={S.A(p.Actor).Act?.Id}/{S.A(p.Actor).Act?.Cur?.Kind}");
  }
}
public static partial class Program {
  public static void PressureReport(Simulation sim) {
    var list = sim.S.LivingNpcs.Select(a => (a, p: Relations.Pressure(sim, a))).OrderByDescending(x => x.p.pressure).Take(5);
    Console.WriteLine("  pressure: " + string.Join("  ", list.Select(x => $"{x.a.Id}:{x.p.pressure:0.00}->{x.p.target}({x.p.motive}) st={x.a.Needs.Stress:0.0} an={x.a.Needs.Anger:0.0}")));
  }
}
public static partial class Program {
  public static void StrikeReport(GameState S) {
    foreach (var g in S.Ledger.Where(e => e.Type == "Strike").GroupBy(e => e.Actor + "->" + e.Target + " " + (e.Data ?? "").Split('/').Last()).OrderByDescending(g => g.Count()).Take(6))
      { var f = g.First(); var v = S.A(f.Target); var at = S.A(f.Actor); Console.WriteLine($"  strike {g.Key} x{g.Count()} first={ClockFmt.DayHM(g.Min(e => e.Clock))} last={ClockFmt.DayHM(g.Max(e => e.Clock))} data={f.Data} last={g.Last().Data} victim={v?.Status} pose={v?.Pose} carriedBy={v?.CarriedBy} mob={v?.Body.Mobility:0.00} cons={v?.Body.Conscious:0.00} wounds={v?.Body.Wounds.Count} vroom={v?.Room} aroom={at?.Room} act={at?.Act?.Id}/{at?.Act?.Cur?.Kind} d={(v != null && at != null ? v.Pos.Dist(at.Pos) : -1):0.0}"); }
  }
}
