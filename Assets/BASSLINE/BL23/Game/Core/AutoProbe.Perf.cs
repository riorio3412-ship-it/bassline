using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BL23.Sim;
using Unity.Profiling;
using UnityEngine;
using UProfiler = UnityEngine.Profiling.Profiler;

namespace BL23.Game
{
    /// <summary>
    /// -bl23probe perf [-bl23seed N (default 777)] [-perfGore stab|blunt|strangle|dismember] — the same fixed scenarios every
    /// run, measured with machine-independent counters so two builds can be compared on a busy machine:
    /// grand hall with the breakfast crowd · dining room at breakfast (people seated) · the longest corridor · the most
    /// furnished library/study · the greenhouse · the most furnished basement room · a dialogue close-up · the body-discovery
    /// film and the crime scene with gore after a staged murder (Gore.DebugStage) · the courtroom during the trial.
    /// The kernel is stepped deterministically between scenarios (session paused) and runs at 1x inside each window.
    /// Each window: 3 s warm-up, 5 s of ProfilerRecorder counters (render / memory / main thread; per frame avg/max) plus a
    /// census (lights on / shadowed, rigidbodies awake, renderers visible, skinned meshes, animators, particles, Update
    /// scripts by type, mesh and texture memory). Writes perf_&lt;scenario&gt;.json, perf_summary.md,
    /// perf_counters_available.txt (which counters this player exposes) and a PERF line per scenario in probe_log.txt.
    /// Wall-clock frame ms are recorded but are noisy on a shared machine — compare the counters.
    /// </summary>
    public sealed partial class AutoProbe
    {
        static readonly CultureInfo PerfInv = CultureInfo.InvariantCulture;

        /// <summary>(category, counter name, json key). Counters a player does not expose are reported as null. Unity 6.6 release
        /// players split draw calls by path (Standard / SRP Batcher / Instanced / Indirect / BRG / Null Geometry) and expose no
        /// "Draw Calls Count", "Batches Count", "GC Allocated In Frame" or "Main Thread": "drawCalls" and "instances" are then the
        /// per-frame sums of the split counters, GC allocation is estimated from mono used-size increases, and the CPU/GPU frame
        /// times come from the Render category (FrameTimingManager).</summary>
        static readonly (ProfilerCategory cat, string name, string key)[] PerfCounterDefs =
        {
            (ProfilerCategory.Render, "Draw Calls Count", "drawCallsLegacy"),
            (ProfilerCategory.Render, "Standard Draw Calls Count", "drawStandard"),
            (ProfilerCategory.Render, "SRP Batcher Draw Calls Count", "drawSrpBatcher"),
            (ProfilerCategory.Render, "Standard Instanced Draw Calls Count", "drawInstanced"),
            (ProfilerCategory.Render, "Standard Indirect Draw Calls Count", "drawIndirect"),
            (ProfilerCategory.Render, "BRG Draw Calls Count", "drawBrg"),
            (ProfilerCategory.Render, "BRG Indirect Draw Calls Count", "drawBrgIndirect"),
            (ProfilerCategory.Render, "Null Geometry Draw Calls Count", "drawNullGeometry"),
            (ProfilerCategory.Render, "Null Geometry Indirect Draw Calls Count", "drawNullGeometryIndirect"),
            (ProfilerCategory.Render, "Standard Instances Count", "instStandard"),
            (ProfilerCategory.Render, "SRP Batcher Instances Count", "instSrpBatcher"),
            (ProfilerCategory.Render, "Standard Instanced Instances Count", "instInstanced"),
            (ProfilerCategory.Render, "Standard Indirect Instances Count", "instIndirect"),
            (ProfilerCategory.Render, "BRG Instances Count", "instBrg"),
            (ProfilerCategory.Render, "BRG Indirect Instances Count", "instBrgIndirect"),
            (ProfilerCategory.Render, "SetPass Calls Count", "setPassCalls"),
            (ProfilerCategory.Render, "Batches Count", "batches"),
            (ProfilerCategory.Render, "Triangles Count", "triangles"),
            (ProfilerCategory.Render, "Vertices Count", "vertices"),
            (ProfilerCategory.Render, "Shadow Casters Count", "shadowCasters"),
            (ProfilerCategory.Render, "Visible Skinned Meshes Count", "visibleSkinnedMeshes"),
            (ProfilerCategory.Render, "Render Textures Count", "renderTextures"),
            (ProfilerCategory.Render, "Render Textures Bytes", "renderTexturesBytes"),
            (ProfilerCategory.Render, "Render Textures Changes Count", "renderTextureChanges"),
            (ProfilerCategory.Render, "Used Textures Bytes", "usedTexturesBytes"),
            (ProfilerCategory.Render, "Used Buffers Bytes", "usedBuffersBytes"),
            (ProfilerCategory.Render, "Used Buffers Count", "usedBuffers"),
            (ProfilerCategory.Render, "Vertex Buffer Upload In Frame Bytes", "vbUploadBytes"),
            (ProfilerCategory.Render, "Index Buffer Upload In Frame Bytes", "ibUploadBytes"),
            (ProfilerCategory.Render, "Video Memory Bytes", "videoMemoryBytes"),
            (ProfilerCategory.Render, "CPU Main Thread Frame Time", "cpuMainNs"),
            (ProfilerCategory.Render, "CPU Render Thread Frame Time", "cpuRenderNs"),
            (ProfilerCategory.Render, "GPU Frame Time", "gpuNs"),
            (ProfilerCategory.Memory, "GC Allocated In Frame", "gcAllocatedInFrame"),
            (ProfilerCategory.Memory, "GC Allocation In Frame Count", "gcAllocationsInFrame"),
            (ProfilerCategory.Memory, "Total Used Memory", "totalUsedMemory"),
            (ProfilerCategory.Memory, "Total Reserved Memory", "totalReservedMemory"),
            (ProfilerCategory.Memory, "System Used Memory", "systemUsedMemory"),
            (ProfilerCategory.Memory, "App Resident Memory", "appResidentMemory"),
            (ProfilerCategory.Memory, "GC Used Memory", "gcUsedMemory"),
            (ProfilerCategory.Memory, "Gfx Used Memory", "gfxUsedMemory"),
            (ProfilerCategory.Memory, "Texture Memory", "textureMemory"),
            (ProfilerCategory.Memory, "Mesh Memory", "meshMemory"),
            (ProfilerCategory.Memory, "Texture Count", "textureCount"),
            (ProfilerCategory.Memory, "Mesh Count", "meshCount"),
            (ProfilerCategory.Memory, "Material Count", "materialCount"),
            (ProfilerCategory.Internal, "Main Thread", "mainThreadNs"),
        };
        static readonly string[] PerfDrawKeys = { "drawStandard", "drawSrpBatcher", "drawInstanced", "drawIndirect", "drawBrg", "drawBrgIndirect", "drawNullGeometry", "drawNullGeometryIndirect" };
        static readonly string[] PerfInstKeys = { "instStandard", "instSrpBatcher", "instInstanced", "instIndirect", "instBrg", "instBrgIndirect" };

        sealed class PerfCtr { public string Key, Name; public ProfilerRecorder R; public string Unit; public readonly List<double> V = new List<double>(256); }
        sealed class PerfRow
        {
            public string Name, Where; public int Npcs = -1; public readonly Dictionary<string, (double avg, double p95, bool ok)> C = new Dictionary<string, (double, double, bool)>();   // p95, not max: at high frame rates a render counter sometimes lands a frame late (a 0, then a double frame)
            public readonly Dictionary<string, double> N = new Dictionary<string, double>(); public double FtAvg, FtP95, GcKbFrame, GcKbSec; public bool GcFromCounter; public string Flags = "";
        }
        readonly List<PerfRow> _perfRows = new List<PerfRow>();
        ulong _perfSeed;

        IEnumerator PerfTour()
        {
            yield return Wait(2f);
            _perfSeed = ulong.TryParse(GameBoot.I.ArgValue("-bl23seed") ?? "", out var sd) ? sd : 777UL;
            string goreKind = (GameBoot.I.ArgValue("-perfGore") ?? "stab").Trim().ToLowerInvariant();
            PerfDumpAvailable();
            GameBoot.I.NewGame(_perfSeed); Log($"perf: new game seed {_perfSeed}");
            Ses.Headless = true;   // no table talk / banners / long cards over the windows (the discovery film and the court run normally)
            yield return Until(() => S.Phase == Phase.Daily && !Ses.Cine.Busy, 90, "daily");
            Ses.Player.FirstPerson = true;
            yield return Wait(1f);
            Log($"perf: daily at {ClockFmt.DayHM(S.Clock)} tick {S.Tick}, living {S.LivingNpcs.Count()}");

            // 1) the grand hall with the breakfast crowd: the first moment before 08:10 with six or more people in it
            var hall = S.Layout.Rooms.Where(r => r.Type == RoomType.GrandHall && !r.Void).OrderBy(r => r.Floor == 0 ? 0 : 1).ThenBy(r => r.Id).FirstOrDefault();
            if (hall != null)
            {
                yield return PerfStepUntil(() => PerfNpcsIn(hall.Id) >= 6, 8 * 60 + 10);
                yield return PerfSafe(PerfAtCorner("hall_breakfast", hall), "hall_breakfast");
                yield return PerfSafe(PerfLevers("hall_breakfast", hall.Id, true, false), "hall_levers");
            }
            else Log("perf: no grand hall — hall_breakfast skipped");

            // 2) the dining room at breakfast, people seated (the meal bell is at 08:00)
            var din = S.Layout.Rooms.Where(r => r.Type == RoomType.Dining).OrderBy(r => r.Id).FirstOrDefault();
            if (din != null)
            {
                int living = S.LivingNpcs.Count(); int want = Math.Max(3, (int)Math.Ceiling(living * 0.8));
                yield return PerfStepUntil(() => PerfSeatedIn(din.Id) >= want, 8 * 60 + 45);
                Log($"perf: dining seated {PerfSeatedIn(din.Id)}/{living} at {ClockFmt.DayHM(S.Clock)} (wanted {want})");
                yield return PerfSafe(PerfAtCorner("dining_meal", din), "dining_meal");
                yield return PerfSafe(PerfLevers("dining_meal", din.Id, false, false), "dining_levers");
            }
            else Log("perf: no dining room — dining_meal skipped");

            // 3) the longest corridor, from one end
            var cor = S.Layout.Rooms.Where(r => r.Type == RoomType.Corridor && !r.Void).OrderByDescending(r => Math.Max(r.Rect.W, r.Rect.D)).ThenBy(r => r.Id).FirstOrDefault();
            if (cor != null) yield return PerfSafe(PerfAtCorridorEnd("corridor_long", cor), "corridor_long");
            else Log("perf: no corridor — corridor_long skipped");

            // 4) the densest library / study
            var lib = S.Layout.Rooms.Where(r => (r.Type == RoomType.Library || r.Type == RoomType.Study) && !r.Void).OrderByDescending(r => r.Furniture.Count).ThenBy(r => r.Id).FirstOrDefault();
            if (lib != null) yield return PerfSafe(PerfAtCorner("library_dense", lib), "library_dense");
            else Log("perf: no library/study — library_dense skipped");

            // 5) the greenhouse
            var gh = S.Layout.Rooms.Where(r => r.Type == RoomType.Greenhouse && !r.Void).OrderBy(r => r.Id).FirstOrDefault();
            if (gh != null) yield return PerfSafe(PerfAtCorner("greenhouse", gh), "greenhouse");
            else Log("perf: no greenhouse in this layout — skipped");

            // 6) the most furnished basement room
            var bas = S.Layout.Rooms.Where(r => r.Floor == -1 && !r.Void && !RoomInfo.IsPassage(r.Type) && r.Type != RoomType.Courtroom && r.Type != RoomType.Elevator)
                .OrderByDescending(r => r.Furniture.Count).ThenBy(r => r.Id).FirstOrDefault();
            if (bas != null) yield return PerfSafe(PerfAtCorner("basement", bas), "basement");
            else Log("perf: no basement room — skipped");

            // 7) a dialogue close-up (the first free person by id)
            var npc = S.LivingNpcs.Where(a => a.Status == ActorStatus.Active && a.TalkingTo == null && a.Pose != BL23.Sim.Pose.Sleep && a.StairId < 0 && a.CarriedBy == null)
                .OrderBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault();
            if (npc != null)
            {
                var stand = NearIn(npc.Pos, npc.Room, 1.2f);
                Ses.Player.Teleport(stand, MathX.AngleDeg(npc.Pos.x - stand.x, npc.Pos.z - stand.z));
                yield return Wait(0.5f);
                Ses.Dialogue.Open(npc);
                yield return PerfSafe(PerfMeasure("dialogue_closeup", $"{npc.Id} {Cast.GivenOf(npc.Id)} in {S.RoomName(npc.Room)}", npc.Room, 3f, 5f), "dialogue_closeup");
                if (Ses.Dialogue.Active) yield return PerfSafe(PerfLevers("dialogue_closeup", npc.Room, false, true), "dialogue_levers");
                if (Ses.Dialogue.Active) Ses.Dialogue.Close();
                yield return Wait(0.5f);
            }
            else Log("perf: nobody free to talk to — dialogue_closeup skipped");

            // 8) a staged murder: the discovery film, then the crime scene with its gore
            yield return PerfSafe(PerfCrime(goreKind), "crime");

            // 9) the courtroom during the trial (the kernel is stepped through the investigation)
            yield return PerfSafe(PerfCourt(), "court");

            PerfWriteSummary();
            Log("perf probe done"); Flush();
            yield return Wait(1f);
            Application.Quit();
        }

        /// <summary>Runs a scenario (and everything it nests); a fault is logged and the tour moves on to the next scenario.</summary>
        IEnumerator PerfSafe(IEnumerator e, string name)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(e);
            while (stack.Count > 0)
            {
                object cur; var top = stack.Peek();
                try { if (!top.MoveNext()) { stack.Pop(); continue; } cur = top.Current; }
                catch (Exception ex)
                {
                    Log($"perf: scenario {name} threw {ex.GetType().Name}: {ex.Message} @ {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
                    PerfLeverUndo();
                    if (Ses.IsPaused("perfstep")) Ses.Resume("perfstep");
                    if (Ses.Dialogue.Active) Ses.Dialogue.Close(); if (Ses.Note.Open) Ses.Note.Close(); if (Ses.Menu.Open) Ses.Menu.Close();
                    yield break;
                }
                if (cur is IEnumerator nested) { stack.Push(nested); continue; }
                yield return cur;
            }
        }

        int PerfNpcsIn(int room) => S.LivingNpcs.Count(a => a.Room == room);
        int PerfSeatedIn(int room) => S.LivingNpcs.Count(a => a.Room == room && (a.Pose == BL23.Sim.Pose.Sit || a.Anim == Anim.Eat));

        /// <summary>Steps the kernel (session paused, so the number of ticks does not depend on the frame rate) until done()
        /// or the clock minute of day reaches maxMinute. Daily only.</summary>
        IEnumerator PerfStepUntil(Func<bool> done, int maxMinute)
        {
            Ses.Pause("perfstep"); var sw = new System.Diagnostics.Stopwatch(); long t0 = S.Tick;
            try
            {
                while (!done() && S.Minute < maxMinute && S.Phase == Phase.Daily && S.Player.Alive)
                {
                    sw.Restart();
                    while (sw.ElapsedMilliseconds < 30 && !done() && S.Minute < maxMinute && S.Phase == Phase.Daily) Ses.Sim.Step();
                    if (Ses.Dialogue.Active) Ses.Dialogue.Close(); if (Ses.Menu.Open) Ses.Menu.Close();
                    _ft.Add(Time.unscaledDeltaTime); yield return null;
                }
            }
            finally { Ses.Resume("perfstep"); }
            Log($"perf: stepped {S.Tick - t0} ticks → {ClockFmt.DayHM(S.Clock)}");
        }

        /// <summary>A corner of the room (layout only, so the view is the same every run), looking across to its middle.</summary>
        IEnumerator PerfAtCorner(string name, Room r)
        {
            var nav = S.Layout.Nav(r.Floor);
            P3 spot = new P3(r.Floor, r.Rect.CX, r.Rect.CZ); float bd = float.MaxValue;
            foreach (var (cx, cz) in new[] { (r.Rect.x0, r.Rect.z0), (r.Rect.x1, r.Rect.z0), (r.Rect.x0, r.Rect.z1), (r.Rect.x1, r.Rect.z1) })
            {
                for (int k = 0; k < nav.Room.Length; k++)
                {
                    if (nav.Room[k] != r.Id || !nav.Walkable(k) || !nav.InMain(k)) continue;
                    var c = nav.Center(k); float d = Math.Abs(c.x - cx) + Math.Abs(c.z - cz);
                    if (d < bd - 1e-4f) { bd = d; spot = c; }
                }
            }
            if (Ses.Note.Open) Ses.Note.Close(); if (Ses.Menu.Open) Ses.Menu.Close(); if (Ses.Dialogue.Active) Ses.Dialogue.Close();
            Ses.Player.Teleport(spot, MathX.AngleDeg(r.Rect.CX - spot.x, r.Rect.CZ - spot.z), 8f);
            yield return PerfMeasure(name, $"{S.RoomName(r.Id)} ({r.Type}, floor {r.Floor}, {r.Rect.W:0.0}x{r.Rect.D:0.0} m, furniture {r.Furniture.Count})", r.Id, 3f, 5f);
        }

        /// <summary>One end of a corridor, looking down its length.</summary>
        IEnumerator PerfAtCorridorEnd(string name, Room r)
        {
            var nav = S.Layout.Nav(r.Floor); bool alongX = r.Rect.W >= r.Rect.D;
            float ex = alongX ? r.Rect.x0 + 0.6f : r.Rect.CX, ez = alongX ? r.Rect.CZ : r.Rect.z0 + 0.6f;
            float fx = alongX ? r.Rect.x1 : r.Rect.CX, fz = alongX ? r.Rect.CZ : r.Rect.z1;
            P3 spot = new P3(r.Floor, ex, ez); float bd = float.MaxValue;
            for (int k = 0; k < nav.Room.Length; k++)
            {
                if (nav.Room[k] != r.Id || !nav.Walkable(k) || !nav.InMain(k)) continue;
                var c = nav.Center(k); float d = Math.Abs(c.x - ex) + Math.Abs(c.z - ez);
                if (d < bd - 1e-4f) { bd = d; spot = c; }
            }
            Ses.Player.Teleport(spot, MathX.AngleDeg(fx - spot.x, fz - spot.z), 3f);
            yield return PerfMeasure(name, $"{S.RoomName(r.Id)} (floor {r.Floor}, {Math.Max(r.Rect.W, r.Rect.D):0.0} m long)", r.Id, 3f, 5f);
        }

        IEnumerator PerfCrime(string kind)
        {
            Ses.Headless = false;   // the discovery film plays as for a player
            string vid = null;
            try { vid = BL23.Sim.Gore.DebugStage(Ses.Sim, kind); } catch (Exception e) { Log("perf: DebugStage threw " + e.Message); }
            var v = vid != null ? S.A(vid) : null;
            if (v == null) { Log("perf: no staged murder — discovery_film and crime_scene skipped"); Ses.Headless = true; yield break; }
            Log($"perf: staged {kind}: victim {vid} in {S.RoomName(v.Room)}");
            float gw = Time.realtimeSinceStartup; while (!DiscoveryFilm.GoreReady() && Time.realtimeSinceStartup - gw < 15f) yield return null;
            var inside = NearIn(v.Pos, v.Room, 1.6f);
            Ses.Player.Teleport(inside, MathX.AngleDeg(v.Pos.x - inside.x, v.Pos.z - inside.z), 20f);
            float t0 = Time.realtimeSinceStartup; while (!Ses.Cine.Busy && Time.realtimeSinceStartup - t0 < 6f) yield return null;
            if (!Ses.Cine.Busy) { Ses.Cine.Discovery(vid); yield return null; }
            if (Ses.Cine.Busy) yield return PerfMeasure("discovery_film", $"{kind} film, victim {vid} in {S.RoomName(v.Room)} (whole film, no warm-up)", v.Room, 0f, 14f, () => !Ses.Cine.Busy);
            else Log("perf: the discovery film did not start");
            yield return Until(() => !Ses.Cine.Busy, 20, "film end");
            Ses.Headless = true;
            yield return Wait(6.5f);   // the film's grade fades out
            yield return Until(() => !Ses.Cine.Busy, 20, "cards after the film");
            v = S.A(vid);
            var stand = NearIn(v.Pos, v.Room, 1.8f);
            Ses.Player.Teleport(stand, MathX.AngleDeg(v.Pos.x - stand.x, v.Pos.z - stand.z), 28f);
            yield return PerfMeasure("crime_scene", $"{kind} scene, 1.8 m from {vid} in {S.RoomName(v.Room)}, gore on", v.Room, 3f, 5f);
        }

        IEnumerator PerfCourt()
        {
            Ses.Headless = true;
            var sw = new System.Diagnostics.Stopwatch(); float ff = Time.realtimeSinceStartup; long t0 = S.Tick;
            while (S.Phase != Phase.Trial && Time.realtimeSinceStartup - ff < 600)
            {
                if (S.Phase == Phase.Assembly) Ses.Headless = false;   // the trial opens with its real presentation
                sw.Restart();
                while (sw.ElapsedMilliseconds < 40 && (S.Phase == Phase.Daily || S.Phase == Phase.Investigation || S.Phase == Phase.Assembly) && S.Player.Alive) Ses.Sim.Step();
                if (Ses.Dialogue.Active) Ses.Dialogue.Close(); if (Ses.Menu.Open) Ses.Menu.Close();
                if (!S.Player.Alive) { Log("perf: player died: " + S.Player.Status); break; }
                if (S.Phase != Phase.Daily && S.Phase != Phase.Investigation && S.Phase != Phase.Assembly && S.Phase != Phase.Trial) { Log("perf: unexpected phase " + S.Phase); break; }
                _ft.Add(Time.unscaledDeltaTime); yield return null;
            }
            Log($"perf: stepped {S.Tick - t0} ticks to {S.Phase} at {ClockFmt.DayHM(S.Clock)} ({Time.realtimeSinceStartup - ff:0}s)");
            if (S.Phase != Phase.Trial) { Log("perf: no trial reached — courtroom skipped"); yield break; }
            Ses.Headless = false;
            yield return Until(() => Ses.Trial.Active, 60, "trial ui");
            yield return Wait(6f);   // the opening (portraits, card)
            { float tb = Time.realtimeSinceStartup; while ((S.Trial?.Beats.Count ?? 0) < 6 && S.Phase == Phase.Trial && Time.realtimeSinceStartup - tb < 30f) yield return Wait(0.2f); }   // into the debate
            var court = S.Layout.Rooms.FirstOrDefault(r => r.Type == RoomType.Courtroom);
            yield return PerfMeasure("courtroom_trial", $"trial L{S.Loop}C{S.Chapter}, beats {S.Trial?.Beats.Count ?? 0}", court?.Id ?? -1, 3f, 5f);
            Log($"perf: trial beats after the window {S.Trial?.Beats.Count ?? 0}, prompt {S.Trial?.PendingPrompt ?? "-"}");
        }

        // ------------------------------------------------------------------ one measured window
        IEnumerator PerfMeasure(string name, string where, int room, float warm, float rec, Func<bool> stop = null)
        {
            if (warm > 0f) yield return Wait(warm);
            var ctrs = new List<PerfCtr>();
            foreach (var d in PerfCounterDefs)
            {
                var c = new PerfCtr { Key = d.key, Name = d.cat.Name + "/" + d.name };
                try { c.R = ProfilerRecorder.StartNew(d.cat, d.name, 1); c.Unit = c.R.Valid ? c.R.UnitType.ToString() : null; } catch (Exception) { }
                ctrs.Add(c);
            }
            // per-frame sums: all draw calls (the legacy counter when the player has it, else the split counters) and instances
            var byKey = ctrs.ToDictionary(c => c.Key);
            var dcSum = new PerfCtr { Key = "drawCalls", Name = "sum/Draw Calls Count or Standard+SRP Batcher+Instanced+Indirect+BRG+Null Geometry", Unit = "Count" };
            var inSum = new PerfCtr { Key = "instances", Name = "sum/Standard+SRP Batcher+Instanced+Indirect+BRG Instances Count", Unit = "Count" };
            ctrs.Add(dcSum); ctrs.Add(inSum);
            void Sums()
            {
                double d = 0; bool any = false; var leg = byKey["drawCallsLegacy"];
                if (leg.R.Valid) { d = leg.R.LastValueAsDouble; any = true; }
                else foreach (var k in PerfDrawKeys) { var c = byKey[k]; if (c.R.Valid) { d += c.R.LastValueAsDouble; any = true; } }
                if (any) dcSum.V.Add(d);
                double n = 0; any = false; foreach (var k in PerfInstKeys) { var c = byKey[k]; if (c.R.Valid) { n += c.R.LastValueAsDouble; any = true; } }
                if (any) inSum.V.Add(n);
            }
            var ft = new List<float>(); long tick0 = S.Tick; int cine = 0, frames = 0, gc0 = GC.CollectionCount(0);
            long mono0 = UProfiler.GetMonoUsedSizeLong(), lastMono = mono0, monoUp = 0; string ph0 = S.Phase.ToString(), clock0 = ClockFmt.DayHM(S.Clock);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < rec)
            {
                yield return null;
                if (stop != null && stop()) break;
                frames++; ft.Add(Time.unscaledDeltaTime); _ft.Add(Time.unscaledDeltaTime);
                if (frames == 1) continue;   // the frame the recorders started in
                foreach (var c in ctrs) if (c.R.Valid) c.V.Add(c.R.LastValueAsDouble);
                Sums();
                long m =UProfiler.GetMonoUsedSizeLong(); if (m > lastMono) monoUp += m - lastMono; lastMono = m;
                if (Ses.Cine.Busy) cine++;
            }
            float secs = Time.realtimeSinceStartup - t0; long ticks = S.Tick - tick0; int gcs = GC.CollectionCount(0) - gc0;
            foreach (var c in ctrs) { try { if (c.R.Valid) c.R.Dispose(); } catch (Exception) { } }

            var row = new PerfRow { Name = name, Where = where, Npcs = room >= 0 ? PerfNpcsIn(room) : -1 };
            foreach (var c in ctrs) { if (c.V.Count == 0) { row.C[c.Key] = (0, 0, false); continue; } var s = c.V.OrderBy(x => x).ToList(); row.C[c.Key] = (s.Average(), s[Math.Min(s.Count - 1, (int)(s.Count * 0.95))], true); }
            var fs = ft.Skip(1).OrderBy(x => x).ToList();
            row.FtAvg = fs.Count > 0 ? fs.Average() * 1000 : 0; row.FtP95 = fs.Count > 0 ? fs[Math.Min(fs.Count - 1, (int)(fs.Count * 0.95f))] * 1000 : 0;
            var gcC = row.C["gcAllocatedInFrame"]; row.GcFromCounter = gcC.ok;
            row.GcKbFrame = gcC.ok ? gcC.avg / 1024.0 : monoUp / 1024.0 / Math.Max(1, frames - 1);
            row.GcKbSec = row.GcKbFrame * Math.Max(1, frames - 1) / Math.Max(0.001, secs);   // allocation rate: per frame depends on the frame rate, per second does not
            if (cine > 0) row.Flags += $"cinematic {cine}/{frames - 1} frames; ";
            if (S.Phase.ToString() != ph0) row.Flags += $"phase {ph0}→{S.Phase}; ";

            var census = PerfCensus(row);
            PerfWriteJson(row, ctrs, census, secs, frames, ticks, gcs, mono0, lastMono, monoUp, ph0, clock0, room, warm, rec);
            _perfRows.Add(row);
            Log($"PERF {name}: draws {PerfFmt(row, "drawCalls")} setpass {PerfFmt(row, "setPassCalls")} srpBatcher {PerfFmt(row, "drawSrpBatcher")} instances {PerfFmt(row, "instances")} tris {PerfFmt(row, "triangles", 1e-3, "k")} " +
                $"shadowCasters {PerfFmt(row, "shadowCasters")} skinnedVis {PerfFmt(row, "visibleSkinnedMeshes")} | lights {row.N["lightsOn"]}/{row.N["lightsShadowed"]} shadowed | rb awake {row.N["rbAwake"]}/{row.N["rbDynamic"]} | " +
                $"renderers {row.N["renderersVisible"]}/{row.N["renderersActive"]} | skinned {row.N["skinnedVisible"]}/{row.N["skinnedActive"]} | GC {row.GcKbFrame:0.0} KB/frame {row.GcKbSec:0} KB/s{(row.GcFromCounter ? "" : " (est.)")} | " +
                $"frame {row.FtAvg:0.0}/{row.FtP95:0.0} ms (noisy) | npcs {row.Npcs} | ticks {ticks} | {row.Flags}{where}");
            yield return ShotCo("perf_" + name);
        }

        static string PerfFmt(PerfRow r, string key, double scale = 1, string suffix = "")
        {
            if (!r.C.TryGetValue(key, out var v) || !v.ok) return "n/a";
            return $"{v.avg * scale:0.#}{suffix}/{v.p95 * scale:0.#}{suffix}";
        }

        static readonly Dictionary<Type, int> _perfUpd = new Dictionary<Type, int>();
        /// <summary>1 = Update, 2 = LateUpdate, 4 = FixedUpdate (declared anywhere below MonoBehaviour).</summary>
        static int PerfUpdFlags(Type t)
        {
            if (_perfUpd.TryGetValue(t, out var f)) return f;
            const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var x = t; x != null && x != typeof(MonoBehaviour); x = x.BaseType)
            {
                if (x.GetMethod("Update", bf, null, Type.EmptyTypes, null) != null) f |= 1;
                if (x.GetMethod("LateUpdate", bf, null, Type.EmptyTypes, null) != null) f |= 2;
                if (x.GetMethod("FixedUpdate", bf, null, Type.EmptyTypes, null) != null) f |= 4;
            }
            _perfUpd[t] = f; return f;
        }

        /// <summary>Object census at the end of a window; numbers go into row.N, the rest into the returned json fragment.</summary>
        string PerfCensus(PerfRow row)
        {
            var N = row.N; var sb = new StringBuilder();
            // lights
            int lOn = 0, lSh = 0, lPoint = 0, lSpot = 0, lDir = 0, lOther = 0;
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!l.isActiveAndEnabled) continue; lOn++; if (l.shadows != LightShadows.None) lSh++;
                switch (l.type) { case LightType.Point: lPoint++; break; case LightType.Spot: lSpot++; break; case LightType.Directional: lDir++; break; default: lOther++; break; }
            }
            N["lightsOn"] = lOn; N["lightsShadowed"] = lSh; N["lightsPoint"] = lPoint; N["lightsSpot"] = lSpot; N["lightsDirectional"] = lDir; N["lightsOther"] = lOther;
            var mv = Ses.World?.Mansion;
            if (mv != null)
            {
                int mOn = 0, mSh = 0; foreach (var lr in mv.AllLights) if (lr.Light != null && lr.Light.isActiveAndEnabled) { mOn++; if (lr.Light.shadows != LightShadows.None) mSh++; }
                int visRooms = 0, roomRends = 0; if (mv.Rooms != null) foreach (var rv in mv.Rooms) if (rv != null && rv.Visible) { visRooms++; roomRends += rv.Renderers.Count; }
                N["mansionLights"] = mv.AllLights.Count; N["mansionLightsOn"] = mOn; N["mansionLightsShadowed"] = mSh; N["roomsVisible"] = visRooms; N["roomsTotal"] = mv.Rooms?.Length ?? 0; N["roomRenderersVisible"] = roomRends;
            }
            // physics
            int rb = 0, rbDyn = 0, rbAwake = 0, rbKinAwake = 0;
            foreach (var b in FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                rb++; bool sleeping = b.IsSleeping();
                if (b.isKinematic) { if (!sleeping) rbKinAwake++; continue; }
                rbDyn++; if (!sleeping) rbAwake++;
            }
            N["rigidbodies"] = rb; N["rbDynamic"] = rbDyn; N["rbAwake"] = rbAwake; N["rbKinematicAwake"] = rbKinAwake;
            N["colliders"] = FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(c => c.enabled);
            // renderers
            int rAct = 0, rVis = 0, rVisShadow = 0, vMesh = 0, vSkin = 0, vPart = 0, vOther = 0;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled) continue; rAct++;
                if (!r.isVisible) continue; rVis++; if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) rVisShadow++;
                if (r is SkinnedMeshRenderer) vSkin++; else if (r is MeshRenderer) vMesh++; else if (r is ParticleSystemRenderer) vPart++; else vOther++;
            }
            N["renderersActive"] = rAct; N["renderersVisible"] = rVis; N["renderersVisibleCastingShadows"] = rVisShadow; N["visibleMeshRenderers"] = vMesh; N["visibleSkinnedRenderers"] = vSkin; N["visibleParticleRenderers"] = vPart; N["visibleOtherRenderers"] = vOther;
            int sAct = 0, sVis = 0, sOff = 0; long sVerts = 0, sBones = 0;
            foreach (var s in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!s.enabled) continue; sAct++; if (s.updateWhenOffscreen) sOff++;
                if (!s.isVisible) continue; sVis++; if (s.sharedMesh != null) sVerts += s.sharedMesh.vertexCount; sBones += s.bones?.Length ?? 0;
            }
            N["skinnedActive"] = sAct; N["skinnedVisible"] = sVis; N["skinnedUpdateWhenOffscreen"] = sOff; N["skinnedVisibleVertices"] = sVerts; N["skinnedVisibleBones"] = sBones;
            int an = 0, anAlways = 0; foreach (var a in FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (a.isActiveAndEnabled) { an++; if (a.cullingMode == AnimatorCullingMode.AlwaysAnimate) anAlways++; }
            N["animators"] = an; N["animatorsAlwaysAnimate"] = anAlways;
            int ps = 0, psPlaying = 0; long particles = 0; foreach (var p in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) { ps++; if (p.isPlaying) psPlaying++; particles += p.particleCount; }
            N["particleSystems"] = ps; N["particleSystemsPlaying"] = psPlaying; N["particlesAlive"] = particles;
            N["audioSourcesPlaying"] = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(a => a.isActiveAndEnabled && a.isPlaying);
            N["camerasEnabled"] = Camera.allCamerasCount;
            N["canvasesEnabled"] = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(c => c.isActiveAndEnabled);
            N["uiGraphicsActive"] = FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(g => g.isActiveAndEnabled);
            N["reflectionProbes"] = FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(p => p.isActiveAndEnabled);
            N["lodGroups"] = FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            N["gameObjectsActive"] = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            // actors seen
            int npcViews = 0, npcVisible = 0;
            if (Ses.World != null) foreach (var kv in Ses.World.Actors) { if (kv.Value == null || kv.Key == Cast.Player) continue; npcViews++; if (kv.Value.GetComponentsInChildren<Renderer>().Any(r => r.enabled && r.isVisible)) npcVisible++; }
            N["actorViews"] = npcViews; N["actorsVisible"] = npcVisible;
            // scripts with per-frame callbacks
            var byType = new Dictionary<string, int>(); int upd = 0, late = 0, fixd = 0, mbs = 0;
            foreach (var m in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (m == null || !m.isActiveAndEnabled) continue; mbs++;
                int f = PerfUpdFlags(m.GetType()); if (f == 0) continue;
                if ((f & 1) != 0) upd++; if ((f & 2) != 0) late++; if ((f & 4) != 0) fixd++;
                var n = m.GetType().Name; byType[n] = (byType.TryGetValue(n, out var c) ? c : 0) + 1;
            }
            N["monoBehavioursActive"] = mbs; N["scriptsUpdate"] = upd; N["scriptsLateUpdate"] = late; N["scriptsFixedUpdate"] = fixd;
            sb.Append("\"updateScriptsByType\": {").Append(string.Join(", ", byType.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{PJ(x.Key)}: {x.Value}"))).Append("},\n");
            // memory (release-player friendly estimates next to the profiler counters)
            long meshBytes = 0, meshVerts = 0; int meshN = 0;
            foreach (var m in Resources.FindObjectsOfTypeAll<Mesh>())
            {
                if (m == null) continue; meshN++; int vc = m.vertexCount; meshVerts += vc; int stride = 0;
                try { for (int s = 0; s < m.vertexBufferCount; s++) stride += m.GetVertexBufferStride(s); long idx = 0; for (int s = 0; s < m.subMeshCount; s++) idx += m.GetIndexCount(s); meshBytes += (long)vc * stride + idx * (m.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32 ? 4 : 2); } catch (Exception) { }
            }
            N["meshesLoaded"] = meshN; N["meshVerticesLoaded"] = meshVerts; N["meshBytesEstimate"] = meshBytes;
            N["texturesLoaded"] = Resources.FindObjectsOfTypeAll<Texture>().Length; N["materialsLoaded"] = Resources.FindObjectsOfTypeAll<Material>().Length;
            N["textureMemoryCurrent"] = (double)Texture.currentTextureMemory; N["textureMemoryTotal"] = (double)Texture.totalTextureMemory; N["textureMemoryNonStreaming"] = (double)Texture.nonStreamingTextureMemory;
            N["monoHeapBytes"] = UProfiler.GetMonoHeapSizeLong(); N["monoUsedBytes"] = UProfiler.GetMonoUsedSizeLong();
            N["totalAllocatedBytes"] = UProfiler.GetTotalAllocatedMemoryLong(); N["totalReservedBytes"] = UProfiler.GetTotalReservedMemoryLong();
            // cameras rendering now
            sb.Append("\"cameras\": [").Append(string.Join(", ", Camera.allCameras.OrderByDescending(c => c.depth).Select(c =>
                $"{{\"name\": {PJ(c.name)}, \"depth\": {PN(c.depth)}, \"fov\": {PN(c.fieldOfView)}, \"far\": {PN(c.farClipPlane)}, \"target\": {(c.targetTexture != null ? PJ(c.targetTexture.width + "x" + c.targetTexture.height) : "null")}, \"pos\": [{PN(c.transform.position.x)}, {PN(c.transform.position.y)}, {PN(c.transform.position.z)}], \"fwd\": [{PN(c.transform.forward.x)}, {PN(c.transform.forward.y)}, {PN(c.transform.forward.z)}]}}"))).Append("],\n");
            return sb.ToString();
        }

        static string PJ(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";
        static string PN(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "null" : v.ToString("0.###", PerfInv);

        void PerfWriteJson(PerfRow row, List<PerfCtr> ctrs, string census, float secs, int frames, long ticks, int gcs, long mono0, long mono1, long monoUp, string ph0, string clock0, int room, float warm, float rec)
        {
            try
            {
                var sb = new StringBuilder("{\n");
                var rm = room >= 0 ? S.Layout.Room(room) : null;
                sb.Append($"\"scenario\": {PJ(row.Name)},\n\"seed\": {_perfSeed},\n\"where\": {PJ(row.Where)},\n");
                sb.Append($"\"room\": {(rm != null ? $"{{\"id\": {rm.Id}, \"name\": {PJ(rm.Name)}, \"type\": {PJ(rm.Type.ToString())}, \"floor\": {rm.Floor}, \"furniture\": {rm.Furniture.Count}, \"npcs\": {row.Npcs}}}" : "null")},\n");
                sb.Append($"\"phase\": {PJ(ph0)}, \"phaseEnd\": {PJ(S.Phase.ToString())}, \"clock\": {PJ(clock0)}, \"clockEnd\": {PJ(ClockFmt.DayHM(S.Clock))},\n");
                sb.Append($"\"warmupSeconds\": {PN(warm)}, \"recordSeconds\": {PN(secs)}, \"recordLimitSeconds\": {PN(rec)}, \"frames\": {frames}, \"kernelTicks\": {ticks}, \"flags\": {PJ(row.Flags.Trim())},\n");
                sb.Append($"\"screen\": \"{Screen.width}x{Screen.height}\", \"quality\": {PJ(QualitySettings.names.Length > QualitySettings.GetQualityLevel() ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "?")}, \"gpu\": {PJ(SystemInfo.graphicsDeviceName)},\n");
                sb.Append("\"counters\": {\n");
                sb.Append(string.Join(",\n", ctrs.Select(c =>
                {
                    if (c.V.Count == 0) return $"  {PJ(c.Key)}: null";
                    var s = c.V.OrderBy(x => x).ToList();
                    return $"  {PJ(c.Key)}: {{\"counter\": {PJ(c.Name)}, \"unit\": {PJ(c.Unit)}, \"avg\": {PN(s.Average())}, \"max\": {PN(s.Last())}, \"min\": {PN(s[0])}, \"p95\": {PN(s[Math.Min(s.Count - 1, (int)(s.Count * 0.95))])}, \"samples\": {s.Count}}}";
                })));
                sb.Append("\n},\n");
                sb.Append($"\"frameMs\": {{\"avg\": {PN(row.FtAvg)}, \"p95\": {PN(row.FtP95)}, \"note\": \"wall clock, noisy on a shared machine\"}},\n");
                sb.Append($"\"gc\": {{\"allocKBPerFrame\": {PN(row.GcKbFrame)}, \"allocKBPerSecond\": {PN(row.GcKbSec)}, \"source\": {PJ(row.GcFromCounter ? "GC Allocated In Frame" : "mono used-size increases (estimate)")}, \"collections\": {gcs}, \"monoUsedStartMB\": {PN(mono0 / 1048576.0)}, \"monoUsedEndMB\": {PN(mono1 / 1048576.0)}, \"monoGrowthMB\": {PN(monoUp / 1048576.0)}}},\n");
                sb.Append("\"census\": {").Append(string.Join(", ", row.N.Select(kv => $"{PJ(kv.Key)}: {PN(kv.Value)}"))).Append("},\n");
                sb.Append(census);
                sb.Append($"\"probeVersion\": 1\n}}\n");
                File.WriteAllText(Path.Combine(_dir, $"perf_{row.Name}.json"), sb.ToString());
            }
            catch (Exception e) { Log("perf: json failed " + row.Name + ": " + e.Message); }
        }

        void PerfWriteSummary()
        {
            try
            {
                string A(PerfRow r, string k, double sc = 1) => r.C.TryGetValue(k, out var v) && v.ok ? (v.avg * sc).ToString("0.#", PerfInv) : "n/a";
                string AM(PerfRow r, string k, double sc = 1) => r.C.TryGetValue(k, out var v) && v.ok ? $"{(v.avg * sc).ToString("0.#", PerfInv)} / {(v.p95 * sc).ToString("0.#", PerfInv)}" : "n/a";
                string Nn(PerfRow r, string k, double sc = 1) => r.N.TryGetValue(k, out var v) ? (v * sc).ToString("0.#", PerfInv) : "-";
                var sb = new StringBuilder();
                sb.AppendLine($"# BL23 perf probe — seed {_perfSeed}, {Screen.width}x{Screen.height}, {SystemInfo.graphicsDeviceName}, {DateTime.Now:yyyy-MM-dd HH:mm}");
                sb.AppendLine();
                sb.AppendLine("Render counters: avg / p95 per frame over the window (p95 rather than max: at high frame rates a render counter sometimes lands one frame late). Census: at the end of the window. Frame ms is wall clock (noisy).");
                sb.AppendLine();
                sb.AppendLine("| scenario | npcs in room | draw calls | of which SRP batcher | setpass | instances | tris (k) | verts (k) | shadow casters | vis. skinned | lights on / shadowed | renderers vis / active | skinned vis / active (vis verts k) | rb dynamic / awake | Update / LateUpdate scripts | GC KB/frame · KB/s | frame ms avg / p95 | CPU main / render / GPU ms |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (var r in _perfRows)
                    sb.AppendLine($"| {r.Name} | {(r.Npcs >= 0 ? r.Npcs.ToString() : "-")} | {AM(r, "drawCalls")} | {A(r, "drawSrpBatcher")} | {AM(r, "setPassCalls")} | {A(r, "instances")} | {AM(r, "triangles", 1e-3)} | {A(r, "vertices", 1e-3)} | {AM(r, "shadowCasters")} | {A(r, "visibleSkinnedMeshes")} | {Nn(r, "lightsOn")} / {Nn(r, "lightsShadowed")} | {Nn(r, "renderersVisible")} / {Nn(r, "renderersActive")} | {Nn(r, "skinnedVisible")} / {Nn(r, "skinnedActive")} ({Nn(r, "skinnedVisibleVertices", 1e-3)}) | {Nn(r, "rbDynamic")} / {Nn(r, "rbAwake")} | {Nn(r, "scriptsUpdate")} / {Nn(r, "scriptsLateUpdate")} | {r.GcKbFrame.ToString("0.0", PerfInv)} · {r.GcKbSec.ToString("0", PerfInv)}{(r.GcFromCounter ? "" : "*")} | {r.FtAvg.ToString("0.0", PerfInv)} / {r.FtP95.ToString("0.0", PerfInv)} | {A(r, "cpuMainNs", 1e-6)} / {A(r, "cpuRenderNs", 1e-6)} / {A(r, "gpuNs", 1e-6)} |");
                sb.AppendLine();
                sb.AppendLine("| scenario | where | mem total used MB | video mem (render) MB | render-texture MB | tex current / non-streaming MB | mesh est. MB (loaded meshes) | mono used MB | GOs active | colliders | animators | particles alive | rooms visible | flags |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (var r in _perfRows)
                    sb.AppendLine($"| {r.Name} | {r.Where} | {A(r, "totalUsedMemory", 1.0 / 1048576)} | {A(r, "videoMemoryBytes", 1.0 / 1048576)} | {A(r, "renderTexturesBytes", 1.0 / 1048576)} | {Nn(r, "textureMemoryCurrent", 1.0 / 1048576)} / {Nn(r, "textureMemoryNonStreaming", 1.0 / 1048576)} | {Nn(r, "meshBytesEstimate", 1.0 / 1048576)} ({Nn(r, "meshesLoaded")}) | {Nn(r, "monoUsedBytes", 1.0 / 1048576)} | {Nn(r, "gameObjectsActive")} | {Nn(r, "colliders")} | {Nn(r, "animators")} | {Nn(r, "particlesAlive")} | {Nn(r, "roomsVisible")} | {r.Flags.Trim()} |");
                sb.AppendLine();
                sb.AppendLine("\\* GC KB/frame estimated from mono used-size increases (the GC Allocated In Frame counter is not exposed by this player).");
                File.WriteAllText(Path.Combine(_dir, "perf_summary.md"), sb.ToString());
                Log("perf summary:\n" + sb);
            }
            catch (Exception e) { Log("perf: summary failed: " + e.Message); }
        }

        /// <summary>Which profiler counters this player exposes (release players expose fewer than the editor).</summary>
        void PerfDumpAvailable()
        {
            try
            {
                var hs = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
                Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(hs);
                var lines = new List<string>();
                foreach (var h in hs)
                {
                    var d = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(h);
                    if ((d.Flags & Unity.Profiling.LowLevel.MarkerFlags.Counter) == 0) continue;
                    lines.Add($"{d.Category.Name}\t{d.Name}\t{d.UnitType}");
                }
                lines.Sort(StringComparer.Ordinal);
                File.WriteAllLines(Path.Combine(_dir, "perf_counters_available.txt"), lines);
                var wanted = PerfCounterDefs.Select(x => x.cat.Name + "\t" + x.name).ToList();
                Log($"perf: {lines.Count} counters exposed; wanted but missing: {string.Join(", ", wanted.Where(w => !lines.Any(l => l.StartsWith(w + "\t", StringComparison.Ordinal))).Select(w => w.Replace('\t', '/')))}");
            }
            catch (Exception e) { Log("perf: counter listing failed: " + e.Message); }
        }
    }
}
