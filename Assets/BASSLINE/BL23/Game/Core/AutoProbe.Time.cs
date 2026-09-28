using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Probe steps for time-on-demand. Both time flows: simple interactions are instant (a container opened — its hidden
    /// item unseen until then —, a seat taken, a clock wound, a look, a cup of tea: no fade, no cinematic, under 0.6 s) and no
    /// fish swims through the air. OnDemand only (-bl23timeflow ondemand): the still world (clock unchanged, people alive in
    /// place), the minutes a conversation took, a 30-minute time-lapse, an interruption and 계속 기다리기, time spent with
    /// someone, an emergency running the clock, a still person stepping aside, an appointment kept, a night's sleep, and in
    /// the investigation the remaining time, a 10-minute wait and ending it from the T menu.
    /// </summary>
    public sealed partial class AutoProbe
    {
        partial void TimeHook(string at, List<IEnumerator> run)
        {
            switch (at)
            {
                case "daily": run.Add(Safe(TimeDaily(), "daily")); break;
                // before the convenience pass (which runs the investigation to its end)
                case "investigation": if (Ses.TimeFlow == TimeFlowMode.OnDemand) run.Insert(0, Safe(TimeInvestigation(), "investigation")); break;
                case "trial": run.Add(Safe(TimeTrial(), "trial")); break;
            }
        }

        /// <summary>A step that throws is logged and skipped; it never ends the whole probe (other workflows run these too).</summary>
        IEnumerator Safe(IEnumerator e, string name)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(e);
            while (stack.Count > 0)
            {
                object cur; var top = stack.Peek();
                try { if (!top.MoveNext()) { stack.Pop(); continue; } cur = top.Current; }
                catch (Exception ex) { Log($"time: step {name} threw {ex.GetType().Name}: {ex.Message} @ {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}"); TimeClean(); yield break; }
                if (cur is IEnumerator nested) { stack.Push(nested); continue; }   // nested steps run here too, so their faults are caught
                yield return cur;
            }
        }

        TimeDirector TD => Ses.TimeDir;
        void TimeClean() { if (Ses.Note.Open) Ses.Note.Close(); if (Ses.Menu.Open) Ses.Menu.Close(); if (BacklogUI.Open) BacklogUI.Close(); if (Ses.Dialogue.Active) Ses.Dialogue.Close(); }

        /// <summary>The still-life probe counters (FrozenLife / AmbientChatter).</summary>
        static string StaticVal(string type, string prop)
        {
            switch (type + "." + prop)
            {
                case "FrozenLife.Started": return FrozenLife.Started.ToString();
                case "FrozenLife.Clashes": return FrozenLife.Clashes.ToString();
                case "FrozenLife.AvgMs": return FrozenLife.AvgMs.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
                case "AmbientChatter.Heard": return AmbientChatter.Heard.ToString();
                case "AmbientChatter.Pairs": return AmbientChatter.Pairs.ToString();
                case "AmbientChatter.Lines": return AmbientChatter.Lines.ToString();
            }
            return "-";
        }

        /// <summary>Waits until no skip runs and no cinematic holds the screen. A skip still running at the timeout is a fault: logged as
        /// STUCK (what it was, how far from its target) and cancelled, so the steps after it start clean.</summary>
        IEnumerator WaitSkipIdle(float timeout)
        {
            float t = Time.realtimeSinceStartup, lastProg = t; int lastTicks = -1; SkipPlan lastPlan = null;
            while (TD.Active || Ses.Cine.Busy)
            {
                float now = Time.realtimeSinceStartup;
                var cp = TD.Current; if (cp != lastPlan || cp != null && cp.Ticks != lastTicks) { lastPlan = cp; lastTicks = cp?.Ticks ?? -1; lastProg = now; }
                // past the timeout a skip that still steps is only slow (a costly house): wait on, up to three times as long; one whose
                // ticks stood still for two seconds is stuck
                if (now - t >= timeout && (!TD.Active || now - lastProg > 2f || now - t >= timeout * 3f)) break;
                _ft.Add(Time.unscaledDeltaTime); yield return null;
            }
            float waited = Time.realtimeSinceStartup - t;
            if (TD.Active)
            {
                var p = TD.Current;
                Log($"time: STUCK plan={p?.Kind} '{p?.Label}' ticks={p?.Ticks} left={(p != null ? p.Target - S.Clock : 0):0.0}min style={TD.CurrentStyle} cancel-held={TD.CancelHeld} stalls={TD.Stalls} after {waited:0}s (no tick for {Time.realtimeSinceStartup - lastProg:0.0}s) — cancelling");
                TD.Cancel();
                float t2 = Time.realtimeSinceStartup; while (TD.Active && Time.realtimeSinceStartup - t2 < 8f) { _ft.Add(Time.unscaledDeltaTime); yield return null; }
                if (TD.Active) Log("time: STUCK — the skip did not end after cancelling");
            }
            else if (Ses.Cine.Busy) Log($"time: note — a cinematic still holds the screen after {timeout:0}s");
            else if (waited > timeout) Log($"time: note — a slow skip: {waited:0.0}s (timeout {timeout:0}s) [{TD.LastLog}]");
        }

        /// <summary>Lapse frame times of the last visible time-lapse (p95 must stay under 33 ms).</summary>
        string LapseP95()
        {
            var l = TD.LapseFrames; if (l == null || l.Count < 5) return "-";
            var s = l.OrderBy(x => x).ToList(); return $"{s[(int)(s.Count * 0.95f)] * 1000:0.0}ms (n={s.Count})";
        }

        /// <summary>Turn the view to a point (probe framing).</summary>
        void LookAtWorld(Vector3 w)
        {
            var cam = Ses.Player.Cam; if (cam == null) return;
            var d = w - cam.transform.position; if (d.sqrMagnitude < 1e-4f) return;
            var e = Quaternion.LookRotation(d).eulerAngles; Ses.Player.Yaw = e.y; Ses.Player.Pitch = Mathf.Clamp(e.x > 180f ? e.x - 360f : e.x, -80f, 80f);
        }

        /// <summary>Stand 'dist' metres in front of a piece of furniture, looking at it (inside its room).</summary>
        void FaceFurniture(Furniture f, float dist, float pitch = 12f)
        {
            var sp = S.Layout.Spots.FirstOrDefault(x => x.Furniture == f.Id); var near = sp != null ? sp.Approach : f.Pos;
            var fr = S.Layout.Room(f.Room); float cx = fr != null ? fr.Rect.CX : near.x, cz = fr != null ? fr.Rect.CZ : near.z;
            float dl = Mathf.Max(0.01f, Mathf.Sqrt((cx - near.x) * (cx - near.x) + (cz - near.z) * (cz - near.z))); float st = Mathf.Min(dist, dl * 0.8f);
            var from = new P3(near.f, near.x + (cx - near.x) / dl * st, near.z + (cz - near.z) / dl * st);
            if (S.Layout.RoomAt(from) != f.Room) from = near;
            Ses.Player.Teleport(from, MathX.AngleDeg(f.Pos.x - from.x, f.Pos.z - from.z), pitch);
        }

        bool Livable(int room) { var r = S.Layout.Room(room); return r != null && !RoomInfo.IsPassage(r.Type) && r.Type != RoomType.Courtroom && r.Type != RoomType.Elevator; }

        // ================================================================== daily
        IEnumerator TimeDaily()
        {
            Log($"time: flow {Ses.TimeFlow} (kernel OnDemand={Ses.Sim.OnDemand})");
            TimeClean(); Ses.Speed = 1f;
            yield return WaitSkipIdle(10f);
            yield return Safe(TimeContainer(), "Container");
            yield return Safe(TimeSeat(), "Seat");
            yield return Safe(TimeInstant(), "Instant");
            yield return Safe(TimeFish(), "Fish");
            if (Ses.TimeFlow != TimeFlowMode.OnDemand) { Log("time: continuous flow — the still-world steps are skipped"); yield break; }
            yield return Safe(TimeFrozenRoom(), "FrozenRoom");
            yield return Safe(TimeFrozenLife(), "FrozenLife");
            yield return Safe(TimeTalk(), "Talk");
            yield return Safe(TimeWait30(), "Wait30");
            yield return Safe(TimeInterrupt(), "Interrupt");
            yield return Safe(TimeTogether(), "Together");
            yield return Safe(TimeYield(), "Yield");
            yield return Safe(TimeAppointment(), "Appointment");
            // early in the morning most people are at breakfast or on their way: if the first look found no lively room, look again now
            if (!_frozenLifeOk) { TimeClean(); yield return WaitSkipIdle(10f); yield return Safe(TimeFrozenLife("_b"), "FrozenLife again"); }
            yield return Safe(TimeEmergency(), "Emergency");
            yield return Safe(TimeLongSkips(), "LongSkips");
            yield return Safe(TimeSleep(), "Sleep");
            yield return Safe(TimeSmartDays(), "SmartDays");
            TimeClean(); yield return WaitSkipIdle(15f);
            Log($"time: daily steps done at {ClockFmt.DayHM(S.Clock)} phase {S.Phase}; skips {TD.SkipCount}, watchdog stalls {TD.Stalls}");
        }

        // ---- 1. a container: the hidden item is unseen until its part is opened, then it can be taken
        IEnumerator TimeContainer()
        {
            string[] types = { "Wardrobe", "Sideboard", "Nightstand", "Cabinet", "FileCabinet", "Chest" };
            Furniture fur = null; BL23.Game.Mansion.OpenableParts op = null;
            foreach (var f in S.Layout.Furniture.Where(x => types.Contains(x.Type) && x.Pos.f == S.Player.Pos.f && Livable(x.Room)).OrderBy(x => Array.IndexOf(types, x.Type)).ThenBy(x => x.Id))
            {
                var go = Ses.World.Mansion?.FurnitureObject(f.Id); var o = go != null ? go.GetComponentInChildren<BL23.Game.Mansion.OpenableParts>() : null;
                if (o != null && o.Count > 0) { fur = f; op = o; break; }
            }
            if (fur == null) { Log("time: container — no furniture with openable parts on this floor"); yield break; }
            var it = S.Items.Values.Where(i => i.Holder == null && !i.Hidden && i.KeyFor == null && i.Def != null && !i.Def.IsWeapon && i.Room == fur.Room).OrderBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault()
                  ?? S.Items.Values.Where(i => i.Holder == null && !i.Hidden && i.KeyFor == null && i.Def != null && !i.Def.IsWeapon && i.Pos.f == fur.Pos.f).OrderBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
            if (it == null) { Log("time: container — no loose item to hide"); yield break; }
            bool hid = false; try { hid = Ses.Sim.ProbeHide(it, fur); } catch (Exception e) { Log("time: ProbeHide threw " + e.Message); }
            Log($"time: container {fur.Type}#{fur.Id} ({S.RoomName(fur.Room)}) parts {op.Count} glass={op.GlassFront}; hid {it.Id} {it.Kor}: {hid}");
            FaceFurniture(fur, 1.3f, 18f);
            yield return Wait(0.3f);
            // frame the part that holds it (not the top of the piece)
            var slotting = ContainerView.SlotOf(it); int part0 = slotting != null && slotting.Part >= 0 ? slotting.Part : 0;
            var pr = op.Parts[part0].Pivot != null ? op.Parts[part0].Pivot.GetComponentInChildren<Renderer>() : null;
            if (pr != null) LookAtWorld(pr.bounds.center);
            yield return Wait(0.5f);
            ItemVisibility(it, out bool seen, out bool pickable);
            Log($"time: closed — item seen={seen} pickable={pickable} (want pickable=false{(op.GlassFront ? ", seen through glass" : ", seen=false")}); kept in part {part0} ({op.Parts[part0].Name})");
            yield return ShotCo("container_closed");
            double clock0 = S.Clock; long tick0 = S.Tick; float t0 = Time.realtimeSinceStartup; bool busy = false;
            bool ok = Ses.Player.Interact.ProbeOpen(fur.Id, part0); busy |= Ses.Cine.Busy;
            float tCall = Time.realtimeSinceStartup - t0, tOpen = -1f; bool handShot = false;
            while (Time.realtimeSinceStartup - t0 < 1.2f)
            {
                if (tOpen < 0f && op.Parts[part0].T >= 0.95f) tOpen = Time.realtimeSinceStartup - t0; busy |= Ses.Cine.Busy || TD.Active;
                // the hand still reaching for it, from 민혁's own eyes (as the part comes fully open; after the timing, the shot costs a frame)
                if (!handShot && (tOpen >= 0f || Time.realtimeSinceStartup - t0 >= 0.3f)) { handShot = true; yield return ShotCo("open_hand"); continue; }
                if (tOpen >= 0f && Time.realtimeSinceStartup - t0 > 0.65f) break;
                yield return null;
            }
            ItemVisibility(it, out seen, out pickable);
            string label = Clean(Ses.Player.Interact.TargetLabel), hint = Clean(Ses.Player.Interact.Hint);
            yield return ShotCo("open_drawer_1");
            Log($"time: opened part {part0} ({op.Parts[part0].Name}) ok={ok} — call {tCall * 1000:0}ms, fully open at {tOpen:0.00}s (want ≤ 0.5); item seen={seen} pickable={pickable}; crosshair '{label}' / '{hint}' (want the item: E 줍기)");
            // X: everything opens, one line for all of it
            bool all = Ses.Player.Interact.ProbeOpenAll(fur.Id);
            yield return Wait(0.6f); yield return ShotCo("open_drawer_2");
            ItemVisibility(it, out seen, out pickable);
            var bounce = op.transform.Find("InteriorBounce")?.GetComponent<Light>();
            Log($"time: open all={all} — item hidden={it.Hidden} seen={seen} pickable={pickable}; busy={busy} clock+{(S.Clock - clock0):0.00} ticks+{S.Tick - tick0}; interior light range {(bounce != null ? bounce.range.ToString("0.00") : "-")} m (piece depth {fur.D:0.00})");
            bool took = false; try { took = Ses.Player.Interact.ProbePickUp(); } catch (Exception e) { Log("time: ProbePickUp threw " + e.Message); }
            Log($"time: picked up: {took} (holder {S.I(it.Id)?.Holder ?? "-"})");
            yield return Wait(0.4f);
        }

        void ItemVisibility(Item it, out bool seen, out bool pickable)
        {
            seen = false; pickable = false;
            if (it == null || !Ses.World.Items.TryGetValue(it.Id, out var iv) || iv == null) return;
            seen = iv.GetComponentsInChildren<Renderer>(false).Any(r => r.enabled);
            pickable = iv.GetComponentsInChildren<Collider>(false).Any(c => c.enabled);
        }

        // ---- 2. a seat: sitting is instant (no fade, no cinematic), getting up too
        IEnumerator TimeSeat()
        {
            // an armchair, a sofa and a chair: seated at once, the view down at seated height (≤ 1.25 m) within 0.6 s, the seated hint
            int n = 0;
            foreach (var type in new[] { "Armchair", "Sofa", "Chair" })
            {
                var chair = S.Layout.Furniture.Where(x => x.Type == type && Livable(x.Room) && S.Layout.Spots.Any(sp => sp.Furniture == x.Id && sp.Occupant == null && sp.OnFurniture))
                                              .OrderBy(x => x.Pos.f == S.Player.Pos.f ? 0 : 1).ThenBy(x => x.Pos.DistXZ(S.Player.Pos)).FirstOrDefault();
                if (chair == null) { Log($"time: seat — no free {type}"); continue; }
                FaceFurniture(chair, 1.1f, 10f); yield return Wait(0.5f);
                double clock0 = S.Clock; float t0 = Time.realtimeSinceStartup; float eye0 = Ses.Player.EyeHeight;
                bool sat = false; try { sat = Ses.Player.Interact.ProbeSit(chair.Id); } catch (Exception e) { Log("time: ProbeSit threw " + e.Message); }
                float took = Time.realtimeSinceStartup - t0; bool busy = Ses.Cine.Busy;
                float eyeAt = -1f; while (Time.realtimeSinceStartup - t0 < 0.6f) { busy |= Ses.Cine.Busy; yield return null; }
                eyeAt = Ses.Player.EyeHeight; string hint = Clean(Ses.Player.Interact.Hint), label = Clean(Ses.Player.Interact.TargetLabel);
                if (n++ == 0) yield return ShotCo("sit_fp"); else yield return ShotCo("sit_fp_" + type.ToLowerInvariant());
                bool ok = sat && eyeAt <= 1.25f && hint != null && hint.Contains("일어나기") && !(label ?? "").Contains("앉기") && !busy;
                Log($"time: sat on {type}#{chair.Id}: {sat} in {took:0.00}s; eye {eye0:0.00} → {eyeAt:0.00} m at 0.6 s (want ≤ 1.25); hint '{hint}' label '{label}'; kernel spot {S.Player.Spot} pose {S.Player.Pose}; busy={busy} fade={TD.UI.FadeAlpha:0.00} clock+{(S.Clock - clock0):0.00} → {(ok ? "OK" : "FAIL")}");
                bool stood = false; try { stood = Ses.Player.Interact.ProbeStand(); } catch (Exception e) { Log("time: ProbeStand threw " + e.Message); }
                yield return Wait(0.6f);
                Log($"time: stood up: {stood} (pose {S.Player.Pose}, spot {S.Player.Spot}, eye {Ses.Player.EyeHeight:0.00} m)");
            }
        }

        // ---- 3. instant actions: wind a clock, look at something, pour tea — no fade, no cinematic, no time
        IEnumerator TimeInstant()
        {
            foreach (var (type, act) in new[] { ("Clock", "wind"), ("ClockCase", "wind"), ("Telescope", "view"), ("Globe", "view"), ("Aquarium", "view"), ("FrameWall", "view"), ("TeaCart", "tea") })
            {
                var f = S.Layout.Furniture.Where(x => x.Type == type && x.Pos.f == S.Player.Pos.f && Livable(x.Room)).OrderBy(x => x.Id).FirstOrDefault(); if (f == null) continue;
                var a = Ses.Sim.FurnitureActions(f).FirstOrDefault(x => x.Id == act); if (a == null) continue;
                FaceFurniture(f, 1.2f, 6f); yield return Wait(0.4f);
                double c0 = S.Clock; float t0 = Time.realtimeSinceStartup; float maxFade = 0f; bool busy = false; bool early = false;
                Ses.Cine.Activity(f, a);
                for (float t = 0; t < 0.6f; t += Time.unscaledDeltaTime)
                {
                    maxFade = Mathf.Max(maxFade, TD.UI.FadeAlpha); busy |= Ses.Cine.Busy || TD.Active;
                    // the hand reaching, from 민혁's own eyes (a quarter second after the key)
                    if (!early && t >= 0.25f && (act == "tea" || act == "wind" && type == "Clock")) { early = true; yield return ShotCo("instant_" + act + "_hand"); }
                    yield return null;
                }
                Log($"time: instant {act} at {type}#{f.Id} ({a.Minutes:0} min): busy={busy} fade={maxFade:0.00} clock+{(S.Clock - c0):0.00} ({(Time.realtimeSinceStartup - t0):0.00}s)");
                if (act == "wind" && type == "Clock") yield return ShotCo("instant_wind");
                if (act == "tea") yield return ShotCo("instant_tea");
            }
            yield return WaitSkipIdle(20f);   // before the phase-0 change a "view" still took minutes
        }

        // ---- 4. no fish in the air: every school stays inside its tank
        IEnumerator TimeFish()
        {
            int schools = 0, fish = 0, outside = 0; var where = new List<string>();
            for (int k = 0; k < 3; k++)
            {
                foreach (var fs in FindObjectsByType<BL23.Game.Mansion.FishSchool>(FindObjectsSortMode.None))
                {
                    var tank = fs.transform.parent; if (tank == null) continue;
                    var rends = tank.GetComponentsInChildren<Renderer>(true).Where(r => !r.transform.IsChildOf(fs.transform)).ToList(); if (rends.Count == 0) continue;
                    var b = rends[0].bounds; foreach (var r in rends.Skip(1)) b.Encapsulate(r.bounds); b.Expand(0.1f);   // ±5 cm
                    if (k == 0) schools++;
                    foreach (Transform f in fs.transform) { if (k == 0) fish++; if (!b.Contains(f.position)) { outside++; if (where.Count < 5) where.Add(tank.name); } }
                }
                yield return Wait(0.5f);
            }
            int air = FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name.IndexOf("AirFish", StringComparison.OrdinalIgnoreCase) >= 0);
            Log($"time: fish schools {schools}, fish {fish}, fish outside tanks: {outside}{(where.Count > 0 ? " (" + string.Join(", ", where.Distinct()) + ")" : "")}; air-fish objects {air}");
        }

        // ---- OnDemand 1. the still world: the clock does not move while nothing is spent
        IEnumerator TimeFrozenRoom()
        {
            TimeClean(); yield return WaitSkipIdle(15f); yield return Wait(0.5f); yield return WaitSkipIdle(10f);
            long tick0 = S.Tick; double c0 = S.Clock;
            yield return Wait(3f);
            Log($"time: frozen_room — ticks +{S.Tick - tick0}, clock +{(S.Clock - c0):0.000} (want 0; still={Ses.WorldStill}, chip='{Ses.ClockChip()}')");
            yield return ShotCo("frozen_room");
        }

        // ---- OnDemand 2. people alive in place: gestures, no two alike at once, babble within 30 s
        bool _frozenLifeOk;
        IEnumerator TimeFrozenLife() => TimeFrozenLife("");
        IEnumerator TimeFrozenLife(string pass)
        {
            // the liveliest room on this floor: people standing idle or already talking close together (a lounge, a parlour, the dining
            // room after a meal), scored by the pairs the chatter can make (within 2.4 m) — never a room with one lone player
            Room room = null; float bestScore = 0f;
            foreach (var r in S.Layout.Rooms.Where(r => Livable(r.Id) && r.Floor == S.Player.Pos.f))
            {
                var here = S.LivingNpcs.Where(a => a.Room == r.Id && a.Status == ActorStatus.Active && a.Pose != BL23.Sim.Pose.Sleep && !a.IsButler && a.StairId < 0).ToList();
                if (here.Count < 2) continue;
                // (not the dining room at a meal: walking in starts the table talk, a cinematic, and the still life steps aside for it)
                int mm = S.Minute; bool mealTalk = r.Type == RoomType.Dining && (mm >= 7 * 60 && mm < 10 * 60 || mm >= 12 * 60 && mm < 14 * 60 || mm >= 18 * 60 && mm < 21 * 60);
                if (mealTalk) continue;
                int pairs = 0; for (int i = 0; i < here.Count; i++) for (int j = i + 1; j < here.Count; j++) if (here[i].Pos.DistXZ(here[j].Pos) < 2.4f || here[i].TalkingTo == here[j].Id) pairs++;
                float score = pairs * 2f + here.Count;
                if (score > bestScore) { bestScore = score; room = r; }
            }
            if (room == null) room = S.Layout.Rooms.Where(r => Livable(r.Id) && r.Type != RoomType.Dining).OrderByDescending(r => S.LivingNpcs.Count(a => a.Room == r.Id)).FirstOrDefault();
            if (room == null) { Log("time: frozen_life — no room with people"); yield break; }
            var ppl = S.LivingNpcs.Where(a => a.Room == room.Id).ToList();
            // stand about 3 m from the middle of them, looking at them
            var c = new P3(room.Floor, room.Rect.CX, room.Rect.CZ);
            if (ppl.Count > 0) c = new P3(room.Floor, ppl.Average(a => a.Pos.x), ppl.Average(a => a.Pos.z));
            var stand = NearIn(c, room.Id, 3.0f);
            Ses.Player.Teleport(stand, MathX.AngleDeg(c.x - stand.x, c.z - stand.z), 6);
            string s0 = StaticVal("FrozenLife", "Started"), k0 = StaticVal("FrozenLife", "Clashes"), h0 = StaticVal("AmbientChatter", "Heard"), l0 = StaticVal("AmbientChatter", "Lines");
            long tick0 = S.Tick; bool babble = false; float t0 = Time.realtimeSinceStartup; int maxPairs = 0; bool shot1 = false, shot2 = false, lineShot = false;
            while (Time.realtimeSinceStartup - t0 < 30f)
            {
                babble |= BL23.Game.Audio.VoiceBabble.IsSpeaking;
                maxPairs = Math.Max(maxPairs, AmbientChatter.Pairs);
                float e = Time.realtimeSinceStartup - t0;
                if (!shot1 && e > 5f) { shot1 = true; yield return ShotCo("frozen_life_1" + pass); continue; }
                if (!shot2 && e > 15f) { shot2 = true; yield return ShotCo("frozen_life_2" + pass); continue; }
                if (!lineShot && StaticVal("AmbientChatter", "Lines") != l0) { lineShot = true; yield return ShotCo("frozen_overheard" + pass); continue; }   // a line caught over someone's head
                _ft.Add(Time.unscaledDeltaTime); yield return null;
            }
            int started = int.TryParse(StaticVal("FrozenLife", "Started"), out var s1) && int.TryParse(s0, out var s00) ? s1 - s00 : -1;
            int clashes = int.TryParse(StaticVal("FrozenLife", "Clashes"), out var k1) && int.TryParse(k0, out var k00) ? k1 - k00 : -1;
            bool ok = started >= 2 && clashes == 0 && babble && maxPairs >= 1 && S.Tick == tick0;
            _frozenLifeOk = ok;
            Log($"time: frozen_life{(pass.Length > 0 ? " (again, " + ClockFmt.HM(S.Clock) + ")" : "")} in {room.Name} ({ppl.Count} people: {string.Join(",", ppl.Select(a => a.Id + ":" + a.Anim))}) — gestures started +{started} (want ≥ 2), clashes +{clashes} (want 0), heard {h0}→{StaticVal("AmbientChatter", "Heard")}, whole lines caught {l0}→{StaticVal("AmbientChatter", "Lines")}, pairs max {maxPairs} (want ≥ 1), babble seen={babble}, ticks +{S.Tick - tick0}; still-life avg {StaticVal("FrozenLife", "AvgMs")} ms/frame → {(ok ? "OK" : "FAIL")}");
            // the people standing there: what each is shown doing (a player reads these labels)
            foreach (var a in ppl.Take(6)) { string lbl = FrozenLife.UnplacedLabel(S, a); if (lbl != null) Log($"time:   {a.Id} {a.Act?.Id} {a.Anim} — '{lbl}'"); }
        }

        Actor Talkable(float maxDist = 60f) => S.LivingNpcs.Where(a => a.Status == ActorStatus.Active && a.TalkingTo == null && !a.IsButler && a.Pos.f == S.Player.Pos.f && Livable(a.Room) && Ses.Sim.CanTalk(a, out _) && a.Pos.DistXZ(S.Player.Pos) < maxDist)
                                                    .OrderBy(a => a.Pos.DistXZ(S.Player.Pos)).FirstOrDefault();

        // ---- OnDemand 3. a conversation passes the minutes it took, after it; a second chat within the hour gives nothing
        IEnumerator TimeTalk()
        {
            TimeClean(); yield return WaitSkipIdle(10f);
            var npc = Talkable(200f); if (npc == null) { Log("time: talk — nobody to talk to"); yield break; }
            var stand = NearIn(npc.Pos, npc.Room, 1.2f); Ses.Player.Teleport(stand, MathX.AngleDeg(npc.Pos.x - stand.x, npc.Pos.z - stand.z)); yield return Wait(0.4f);
            double c0 = S.Clock; float like0 = S.R(npc.Id, Cast.Player).Like, like1 = like0, like2 = like0; long tick0 = S.Tick;
            Ses.Dialogue.Open(npc); yield return Wait(1.0f);
            if (Ses.Dialogue.Active)
            {
                Ses.Dialogue.ProbeFinishLines();
                if (Ses.Dialogue.ProbeChoose("chat")) { yield return Wait(0.3f); Ses.Dialogue.ProbeFinishLines(); }
                like1 = S.R(npc.Id, Cast.Player).Like;
                if (Ses.Dialogue.Active && Ses.Dialogue.ProbeChoose("chat")) { yield return Wait(0.3f); Ses.Dialogue.ProbeFinishLines(); }
                like2 = S.R(npc.Id, Cast.Player).Like;
            }
            double during = S.Clock - c0; double pending = Ses.Sim.PendingTalk;
            Ses.Dialogue.Close();
            yield return Until(() => TD.Active, 3f, "talk lapse start");
            bool lapsed = TD.Active; yield return WaitSkipIdle(8f); yield return Wait(0.3f);
            Log($"time: talk with {npc.Id} — clock during dialogue +{during:0.00}, pending {pending:0.00} min, after close +{(S.Clock - c0):0.00} (want +1..+4) lapse={lapsed} [{TD.LastLog}]; like {like0:0.000}→{like1:0.000}→{like2:0.000} (second chat: no gain)");
        }

        // ---- OnDemand 4. 30 minutes from the T menu: the wait menu, a time-lapse with the dial
        IEnumerator TimeWait30()
        {
            TimeClean(); yield return WaitSkipIdle(10f);
            Ses.Menu.OpenWait(); yield return Wait(0.8f); yield return ShotCo("wait_menu_od");
            Log($"time: wait menu top row '{Clean(Ses.Menu.ProbeRowLabel(1))}'");
            Ses.Menu.Close(); yield return Wait(0.3f);
            double c0 = S.Clock; Ses.Cine.Wait(30);
            yield return Wait(0.9f); yield return ShotCo("lapse_mid");
            yield return WaitSkipIdle(20f);
            Log($"time: wait 30 — clock +{(S.Clock - c0):0.0} (want 30..35 or a stop) [{TD.LastLog}] stop '{TD.LastResult?.Stop?.Title}'; visible lapse frame p95 {LapseP95()} (want < 33 ms)");
            yield return Wait(0.5f);
        }

        static string Clean(string s) => s == null ? null : System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "");

        // ---- OnDemand 5. an interruption: someone comes to talk during an hour's wait; T offers 계속 기다리기, which ends on time
        IEnumerator TimeInterrupt()
        {
            TimeClean(); yield return WaitSkipIdle(10f);
            var npc = Talkable(40f);
            double c0 = S.Clock; Ses.Cine.Wait(60); var plan = TD.Current; double target = plan != null ? plan.Target : c0 + 60;
            bool asked = false; if (npc != null) { try { asked = Ses.Sim.ProbeApproach(npc); } catch (Exception e) { Log("time: ProbeApproach threw " + e.Message); } }
            yield return WaitSkipIdle(25f);
            var st = TD.LastResult?.Stop;
            Log($"time: interrupt — approach by {npc?.Id ?? "-"} given={asked}; stopped by {st?.Kind.ToString() ?? "-"} '{st?.Title}' after {(S.Clock - c0):0.0} min (target {ClockFmt.HM(target)}) banner={TD.UI.BannerShowing} '{TD.UI.BannerText}'");
            yield return Wait(0.3f); yield return ShotCo("interrupt_banner");
            Ses.Menu.OpenWait(); yield return Wait(0.7f);
            string top = Clean(Ses.Menu.ProbeRowLabel(1));
            yield return ShotCo("wait_menu_resume");
            Log($"time: T menu top row '{top}' (want 계속 기다리기 when stopped short)");
            if (top != null && top.Contains("계속 기다리기"))
            {
                Ses.Menu.ProbeRow(1); yield return WaitSkipIdle(30f);
                Log($"time: resumed — ended {ClockFmt.HM(S.Clock)} vs target {ClockFmt.HM(target)} (±5) stop {TD.LastResult?.Stop?.Kind.ToString() ?? "-"}");
            }
            else Ses.Menu.Close();
            yield return Wait(0.4f);
        }

        // ---- OnDemand 6. time with someone: tea or a long talk — the clock moves by it, their liking grows
        IEnumerator TimeTogether()
        {
            TimeClean(); yield return WaitSkipIdle(10f);
            Actor npc = null; TogetherOption opt = null; double c0 = S.Clock; float like0 = 0f; int asked = 0;
            // someone away from the dining room (walking in there at a meal starts the table talk, which holds the screen for a while)
            var din = S.Layout.First(RoomType.Dining);
            foreach (var a in S.LivingNpcs.Where(a => a.Status == ActorStatus.Active && !a.IsButler && a.Pos.f == S.Player.Pos.f && Livable(a.Room) && (din == null || a.Room != din.Id)).OrderBy(a => a.Pos.DistXZ(S.Player.Pos)).Take(10).ToList())
            {
                List<TogetherOption> opts = null; try { opts = Ses.Sim.TogetherOptions(a); } catch (Exception e) { Log("time: TogetherOptions threw " + e.Message); }
                var o = opts?.FirstOrDefault(x => x.Enabled && (x.Id == "tea" || x.Id == "talk_long")) ?? opts?.FirstOrDefault(x => x.Enabled);
                if (o == null) continue;
                var stand = NearIn(a.Pos, a.Room, 1.2f); Ses.Player.Teleport(stand, MathX.AngleDeg(a.Pos.x - stand.x, a.Pos.z - stand.z)); yield return Wait(0.3f);
                c0 = S.Clock; like0 = S.R(a.Id, Cast.Player).Like; asked++;
                List<Utterance> said = null; try { said = Ses.Sim.Choose(a, "together", o.Id); } catch (Exception e) { Log("time: Choose together threw " + e.Message); }
                if (Ses.Sim.PendingTogether != null) { npc = a; opt = o; break; }
                Log($"time: together — {a.Id} said no to {o.Id}: '{said?.LastOrDefault()?.Text}'");
                if (asked >= 5) break;
            }
            if (npc == null) { Log("time: together — no one to spend time with"); yield break; }
            // the scene starts once nothing else holds the screen (the conversation, a table talk, a lapse): up to 25 s, as a player waits
            float tw = Time.realtimeSinceStartup; bool sawBusy = false, sawDialogue = false, sawActive = false;
            while (!(TD.Active && TD.Current?.Kind == SkipKind.Together) && Ses.Sim.PendingTogether != null && Time.realtimeSinceStartup - tw < 25f)
            { sawBusy |= Ses.Cine.Busy; sawDialogue |= Ses.Dialogue.Active; sawActive |= TD.Active; _ft.Add(Time.unscaledDeltaTime); yield return null; }
            Log($"time: together — scene {(TD.Active && TD.Current?.Kind == SkipKind.Together ? "started" : "not started")} after {Time.realtimeSinceStartup - tw:0.0}s (screen held: cine={sawBusy} dialogue={sawDialogue} skip={sawActive})");
            if (!(TD.Active && TD.Current?.Kind == SkipKind.Together) && Ses.Sim.PendingTogether != null)
            {
                // nothing picked it up: say exactly why the director would refuse, then try it here
                Log($"time: together — TogetherScene did not start it; director refusal now: '{(TD.CanStart(TimeDirector.Style.Scene) ? "none" : "busy/active/staging/critical")}', last '{TD.LastRefusal ?? "-"}', cine={Ses.Cine.Busy} dialogue={Ses.Dialogue.Active} pendingStop={Ses.Sim.PendingStop?.Kind.ToString() ?? "-"}");
                if (TD.CanStart(TimeDirector.Style.Scene))
                {
                    var tp = Ses.Sim.PendingTogether; Ses.Sim.PendingTogether = null; SkipPlan plan = null; string why = null;
                    try { plan = Ses.Sim.PlanTogether(tp, out why); } catch (Exception e) { Log("time: PlanTogether threw " + e.Message); }
                    if (plan == null || !TD.Start(plan, TimeDirector.Style.Scene)) { Log($"time: together — could not start (plan: {why ?? "ok"}; director: {TD.LastRefusal ?? "-"})"); yield break; }
                    Log("time: together started by the probe (no scene picked it up)");
                }
                else yield break;
            }
            if (!(TD.Active && TD.Current?.Kind == SkipKind.Together)) { Log("time: together — the scene was over before it could be photographed"); yield break; }
            // the staging (the partner walks over, 민혁 sits or takes the last step), a line at half way, the result card
            yield return Wait(0.6f); yield return ShotCo("together_1");
            yield return Until(() => !TD.Active || TD.Progress > 0.52f, 12f, "together 50%"); yield return Wait(0.3f); yield return ShotCo("together_2");
            yield return WaitSkipIdle(15f); yield return Wait(0.5f); yield return ShotCo("together_result");
            var r = TD.LastResult;
            Log($"time: together {opt.Id} with {npc.Id} — clock +{(S.Clock - c0):0.0} (want ~{opt.Minutes}), like {like0:0.000}→{S.R(npc.Id, Cast.Player).Like:0.000}, stop {r?.Stop?.Kind.ToString() ?? "-"}, lines '{string.Join(" / ", r?.Lines ?? new List<string>())}' [{TD.LastLog}]");
            yield return Wait(0.8f); TimeClean();
        }

        // ---- OnDemand 7. a still person in a doorway steps aside
        IEnumerator TimeYield()
        {
            TimeClean(); yield return WaitSkipIdle(10f);
            var npc = Talkable(80f); if (npc == null) { Log("time: yield — nobody"); yield break; }
            var before = npc.Pos; bool moved = false;
            try { moved = Ses.Sim.YieldTo(npc, S.Player.Pos); } catch (Exception e) { Log("time: YieldTo threw " + e.Message); }
            yield return Wait(0.5f);
            Log($"time: yield {npc.Id} — moved={moved} by {before.DistXZ(npc.Pos):0.00} m (want 0.8–1.2)");
        }

        // ---- OnDemand 8. an appointment kept: accepted, wait in its room until they come
        IEnumerator TimeAppointment()
        {
            TimeClean(); yield return WaitSkipIdle(10f);
            var host = S.LivingNpcs.Where(a => a.Status == ActorStatus.Active && !a.IsButler && a.PlanId == null && a.TalkingTo == null).OrderBy(a => a.Pos.DistXZ(S.Player.Pos)).FirstOrDefault();
            Request r = null; try { r = host != null ? Ses.Sim.ProbeOffer(host) : null; } catch (Exception e) { Log("time: ProbeOffer threw " + e.Message); }
            if (r == null) { Log("time: appointment — no invitation could be made"); yield break; }
            try { Ses.Sim.Choose(host, "req_accept"); } catch (Exception e) { Log("time: req_accept threw " + e.Message); }
            var room = S.Layout.Room(r.Room);
            Log($"time: appointment with {host.Id} — {r.State} at {ClockFmt.HM(r.At)} in {room?.Name ?? "-"} (now {ClockFmt.HM(S.Clock)})");
            if (r.State != "accepted" || room == null) yield break;
            var at = Ses.Sim.SnapPublic(new P3(room.Floor, room.Rect.CX, room.Rect.CZ)); Ses.Player.Teleport(at, 0f); yield return Wait(0.5f);
            Ses.Menu.OpenWait(); yield return Wait(0.6f);
            for (int k = 1; k <= 9; k++) { var l = Clean(Ses.Menu.ProbeRowLabel(k)); if (l != null && l.Length > 0) Log($"time:   wait row {k}: {l}"); }
            yield return ShotCo("wait_menu_appointment"); Ses.Menu.Close();
            for (int attempt = 0; attempt < 4 && r.State == "accepted" && S.Clock < r.At + 25; attempt++)
            {
                SkipPlan p = null; try { p = Ses.Sim.PlanUntil(r.At + 25, "약속", out _); } catch (Exception e) { Log("time: PlanUntil threw " + e.Message); }
                if (p == null || !TD.Start(p)) break;
                yield return WaitSkipIdle(30f);
                Log($"time:   appointment wait ended {ClockFmt.HM(S.Clock)} stop {TD.LastResult?.Stop?.Kind.ToString() ?? "-"} '{TD.LastResult?.Stop?.Title}' request {r.State}");
                if (Ses.Dialogue.Active) Ses.Dialogue.Close();
                yield return Wait(0.5f);
            }
            yield return ShotCo("appointment_kept");
            Log($"time: appointment {r.State} (want met) at {ClockFmt.HM(S.Clock)}; host in room: {S.A(host.Id)?.Room == r.Room}");
        }

        // ---- OnDemand 9. an emergency: a scream next door stops a wait and the clock runs on its own
        IEnumerator TimeEmergency()
        {
            TimeClean(); yield return WaitSkipIdle(10f);
            // nobody on the way to talk (their arrival would end the wait before the scream), no stop left over
            foreach (var ap in S.LivingNpcs.Where(a => S.Flags.ContainsKey("approach:" + a.Id)).ToList()) { try { Ses.Sim.DismissApproach(ap); } catch (Exception e) { Log("time: DismissApproach threw " + e.Message); } }
            Ses.Sim.TakeStop();
            // someone in a room on this floor; 민혁 a few metres away in the same room — near enough to hear it for certain
            var who = S.LivingNpcs.Where(a => a.Status == ActorStatus.Active && !a.IsButler && a.Pose != BL23.Sim.Pose.Sleep && a.Pos.f == S.Player.Pos.f && Livable(a.Room)).OrderBy(a => a.Pos.DistXZ(S.Player.Pos)).FirstOrDefault()
                   ?? S.LivingNpcs.Where(a => a.Status == ActorStatus.Active && !a.IsButler && a.Pose != BL23.Sim.Pose.Sleep && Livable(a.Room)).OrderBy(a => a.Id).FirstOrDefault();
            if (who == null) { Log("time: emergency — nobody awake to scream"); yield break; }
            int next = who.Room;
            var stand = NearIn(who.Pos, next, 5f); Ses.Player.Teleport(stand, MathX.AngleDeg(who.Pos.x - stand.x, who.Pos.z - stand.z)); yield return Wait(0.4f);
            double c0 = S.Clock; Ses.Cine.Wait(60);
            yield return Wait(0.6f);   // the lapse is running
            bool running = TD.Active; double atScream = S.Clock;
            bool screamed = false; try { screamed = Ses.Sim.ProbeScream(next); } catch (Exception e) { Log("time: ProbeScream threw " + e.Message); }
            yield return WaitSkipIdle(20f);
            var st = TD.LastResult?.Stop; yield return Wait(0.3f);
            long tick0 = S.Tick; yield return Wait(2f);
            bool ok = st != null && st.Kind == StopKind.Heard && Ses.EmergencyWhy != null && S.Tick > tick0;
            Log($"time: emergency — scream in {S.RoomName(next)} given={screamed} (wait running={running}, {atScream - c0:0.0} min in); wait stopped by {st?.Kind.ToString() ?? "-"} '{st?.Title}' after {(S.Clock - c0):0.0} min; emergency='{Ses.EmergencyWhy}' tail={Ses.EmergencyTail}; ticks over 2 s +{S.Tick - tick0} (want > 0) → {(ok ? "OK" : "FAIL")}");
            yield return ShotCo("emergency_chip");
            // let it calm down before sleeping
            float t0 = Time.realtimeSinceStartup; while ((Ses.EmergencyWhy != null || Ses.EmergencyTail) && Time.realtimeSinceStartup - t0 < 60f) { Ses.Speed = 8f; _ft.Add(Time.unscaledDeltaTime); yield return null; }
            Ses.Speed = 1f;
        }

        // ---- OnDemand 10. a night's sleep: to the evening, then bed until the morning bell (a fade, the dial, waking)
        IEnumerator TimeSleep()
        {
            TimeClean(); yield return WaitSkipIdle(10f);
            var bedroom = S.Layout.BedroomOf(Cast.Player); var bed = bedroom?.Furniture.Select(id => S.Layout.Furniture[id]).FirstOrDefault(x => x.Type == "Bed");
            if (bed == null) { Log("time: sleep — no own bed"); yield break; }
            FaceFurniture(bed, 1.2f, 10f); yield return Wait(0.4f);
            double day0 = Math.Floor(S.Clock / 1440.0) * 1440.0, evening = day0 + 20 * 60 + 5;
            for (int attempt = 0; attempt < 6 && S.Clock < evening && S.Phase == Phase.Daily; attempt++)
            {
                SkipPlan p = null; try { p = Ses.Sim.PlanUntil(evening, "저녁까지", out _); } catch (Exception e) { Log("time: PlanUntil threw " + e.Message); }
                if (p == null) break; p.Quiet = true;
                if (!TD.Start(p)) { yield return Wait(0.5f); continue; }
                yield return WaitSkipIdle(40f); if (Ses.Dialogue.Active) Ses.Dialogue.Close(); yield return Wait(0.3f);
                Log($"time:   to the evening: {ClockFmt.DayHM(S.Clock)} stop {TD.LastResult?.Stop?.Kind.ToString() ?? "-"} '{TD.LastResult?.Stop?.Title}' [{TD.LastLog}]");
            }
            if (S.Phase != Phase.Daily) { Log($"time: sleep — phase is {S.Phase} (a case began first)"); yield break; }
            Ses.Menu.OpenWait(); yield return Wait(0.6f); Log($"time: night T menu top row '{Clean(Ses.Menu.ProbeRowLabel(1))}'"); yield return ShotCo("wait_menu_night"); Ses.Menu.Close();
            FaceFurniture(bed, 1.2f, 10f); yield return Wait(0.3f);
            double c0 = S.Clock; float e0 = S.Player.Needs.Energy;
            TD.StartSleep(false);
            yield return Wait(1.2f); yield return ShotCo("sleep_fade");
            yield return WaitSkipIdle(30f); yield return Wait(0.2f); yield return ShotCo("wake");
            var r = TD.LastResult; var lp = TD.LastPlan;
            bool slept = lp != null && lp.Kind == SkipKind.Sleep, morning = slept && r?.Stop?.Kind == StopKind.Target && S.Minute >= 7 * 60 && S.Clock > c0 + 60;
            Log($"time: sleep — {ClockFmt.DayHM(c0)} → {ClockFmt.DayHM(S.Clock)} (want ≥ 07:00), stop {r?.Stop?.Kind.ToString() ?? "-"} '{r?.Stop?.Title}', skip real {TD.LastRealSecs:0.00}s (want ≤ 6), energy {e0:0.00}→{S.Player.Needs.Energy:0.00} [{TD.LastLog}] → "
                + (!slept ? "FAIL (no sleep skip ran)" : !morning ? $"stopped early ({r?.Stop?.Kind})" : TD.LastRealSecs <= 6f ? "OK" : $"SLOW ({TD.LastRealSecs:0.0}s for {lp.Ticks} ticks)"));
        }

        // ---- OnDemand 11. long skips: the 3시간 chip, the top row and a far "~까지" row each reach their target (or a logged stop) in
        // real seconds — never a dial standing still under the veil (a veiled skip once stood 10.8 clock minutes short of its target)
        IEnumerator TimeLongSkips()
        {
            TimeClean(); yield return WaitSkipIdle(10f);
            if (S.Phase != Phase.Daily) { Log($"time: long skips — phase {S.Phase}, not run"); yield break; }
            int stalls0 = TD.Stalls, n = 0, ok = 0;
            // a. the 3시간 chip, from the menu (keys 2–5 are the chips: 5 = 3시간)
            Ses.Menu.OpenWait(); yield return Wait(0.5f);
            string chip = Clean(Ses.Menu.ProbeRowLabel(5));
            if (chip != null && chip.Contains("3시간")) { n++; bool good = false; yield return LongSkip("chip " + chip.Trim(), () => Ses.Menu.ProbeRow(5), true, g => good = g); if (good) ok++; }
            else { Log($"time: long skips — row 5 is '{chip}', not the 3시간 chip"); Ses.Menu.Close(); }
            yield return Wait(0.4f);
            // b. the top row when it waits for more than an hour (not the night's bed, not a due appointment elsewhere)
            if (S.Phase == Phase.Daily)
            {
                Ses.Menu.OpenWait(); yield return Wait(0.5f);
                string top = Clean(Ses.Menu.ProbeRowLabel(1)) ?? "";
                bool usable = top.Length > 0 && !top.Contains("잠자리") && !top.Contains("방으로 돌아가") && !top.Contains("약속 시간이다") && (top.Contains("시간") && top.Contains("뒤") || top.Contains("계속 기다리기"));
                if (usable) { n++; bool good = false; yield return LongSkip("top row '" + top.Trim() + "'", () => Ses.Menu.ProbeRow(1), true, g => good = g); if (good) ok++; }
                else { Log($"time: long skips — top row '{top}' is not a long wait here"); Ses.Menu.Close(); }
                yield return Wait(0.4f);
            }
            // c. the farthest "~까지" target within eight hours (lunch, the evening, the night), run as the menu runs it
            if (S.Phase == Phase.Daily)
            {
                List<UpcomingEvent> targets = null; try { targets = Ses.Sim.UntilTargets(); } catch (Exception e) { Log("time: UntilTargets threw " + e.Message); }
                var far = targets?.Where(e => e.Kind != "appointment" && e.Kind != "gathering" && e.At - S.Clock > 60 && e.At - S.Clock < 8 * 60).OrderByDescending(e => e.At).FirstOrDefault();
                if (far != null)
                {
                    n++; bool good = false; var fe = far;
                    yield return LongSkip($"until '{fe.Text}' {ClockFmt.HM(fe.At)} ({fe.At - S.Clock:0} min)", () => { var p = Ses.Sim.PlanUntil(fe.At, fe.Text, out _); if (p == null) return false; p.Quiet = TimeDirector.QuietPref; return TD.Start(p); }, false, g => good = g);
                    if (good) ok++;
                }
                else Log("time: long skips — no '~까지' target between one and eight hours away");
            }
            Log($"time: long skips — {ok}/{n} reached their target or a stop in time; watchdog stalls {stalls0}→{TD.Stalls} (want no change) → {(ok == n && TD.Stalls == stalls0 ? "OK" : "FAIL")}");
        }

        /// <summary>One long skip, started by 'start': it must end within 30 real seconds — at its target (never short of it) or at a
        /// stop that says why. Photographs the veiled middle once.</summary>
        IEnumerator LongSkip(string what, Func<bool> start, bool fromMenu, Action<bool> result)
        {
            if (S.Phase != Phase.Daily) { result(false); yield break; }
            double c0 = S.Clock; float t0 = Time.realtimeSinceStartup; int skips0 = TD.SkipCount;
            bool started = false; try { started = start(); } catch (Exception e) { Log($"time: long skip {what} threw {e.Message}"); }
            if (Ses.Menu.Open) Ses.Menu.Close();
            yield return null;
            if (!TD.Active && TD.SkipCount == skips0) { Log($"time: long skip {what} — did not start (started={started}, refusal '{TD.LastRefusal ?? "-"}')"); result(false); yield break; }
            var p = TD.Current; double target = p != null ? p.Target : c0;
            bool veilShot = false; int lastTicks = -1; float lastProg = t0;
            // wanted within 30 s; a skip that still steps past that is slow (logged), one whose ticks stand still for 3 s is stuck
            while (TD.Active && Time.realtimeSinceStartup - t0 < 60f)
            {
                var cp0 = TD.Current; if (cp0 != null && cp0.Ticks != lastTicks) { lastTicks = cp0.Ticks; lastProg = Time.realtimeSinceStartup; }
                if (Time.realtimeSinceStartup - lastProg > 3f) break;
                if (!veilShot && TD.Veiled && Time.realtimeSinceStartup - t0 > 1.2f) { veilShot = true; yield return ShotCo("lapse_veil"); continue; }
                _ft.Add(Time.unscaledDeltaTime); yield return null;
            }
            float real = Time.realtimeSinceStartup - t0;
            if (TD.Active)
            {
                var cp = TD.Current;
                Log($"time: long skip {what} — STUCK after {real:0.0}s (no tick for {Time.realtimeSinceStartup - lastProg:0.0}s): ticks {cp?.Ticks} left {(cp != null ? cp.Target - S.Clock : 0):0.0} min veiled={TD.Veiled} stalls={TD.Stalls} — cancelling");
                TD.Cancel(); float tc = Time.realtimeSinceStartup; while (TD.Active && Time.realtimeSinceStartup - tc < 8f) yield return null;
                result(false); yield break;
            }
            var r = TD.LastResult; var st = r?.Stop; double over = S.Clock - target;
            bool natural = st != null && st.Kind == StopKind.Target;
            bool good = natural ? over >= -0.5 : st != null && st.Kind != StopKind.Guard && !string.IsNullOrEmpty(st.Title);
            Log($"time: long skip {what} — {ClockFmt.DayHM(c0)} → {ClockFmt.DayHM(S.Clock)} in {real:0.0}s{(real > 30f ? " (SLOW: over 30 s)" : "")}, stop {st?.Kind.ToString() ?? "-"} '{st?.Title}', over {over:0.0} min (natural ends: ≥ -0.5) [{TD.LastLog}] visible p95 {LapseP95()} → {(good ? "OK" : "FAIL")}");
            yield return Wait(0.4f);
            if (st != null && st.Kind != StopKind.Target && !veilShot) yield return ShotCo("long_skip_stop");
            if (Ses.Dialogue.Active) Ses.Dialogue.Close();
            // an emergency (a scream) runs the clock by itself: let it calm down before the next skip
            float te = Time.realtimeSinceStartup; while ((Ses.EmergencyWhy != null || Ses.EmergencyTail || Ses.Cine.Busy || TD.Active) && Time.realtimeSinceStartup - te < 40f) { _ft.Add(Time.unscaledDeltaTime); yield return null; }
            result(good);
        }

        // ---- OnDemand 12. a day passed only through the T menu's top row and a night's sleep (no Speed): bells, people coming to talk,
        // meals, the night — and, when the house is ready for it, a murder in time the player passed
        IEnumerator TimeSmartDays()
        {
            TimeClean(); yield return WaitSkipIdle(10f);
            if (S.Phase != Phase.Daily) { Log($"time: smart days — phase {S.Phase}, not run"); yield break; }
            double c0 = S.Clock, until = c0 + 26 * 60; float t0 = Time.realtimeSinceStartup;
            int presses = 0, interrupts = 0, talks = 0, sleeps = 0; bool bannerShot = false;
            int inc0 = S.Incidents.Values.Count(i => i.Loop == S.Loop);
            var kinds = new Dictionary<string, int>();
            Log($"time: smart days — from {ClockFmt.DayHM(c0)}, only the top row and sleep (incidents so far {inc0})");
            while (S.Phase == Phase.Daily && S.Clock < until && Time.realtimeSinceStartup - t0 < 150f && S.Player.Alive)
            {
                if (S.Incidents.Values.Count(i => i.Loop == S.Loop) > inc0) break;
                TimeClean();
                if (Ses.Cine.Busy || TD.Active || Ses.EmergencyWhy != null || Ses.EmergencyTail) { float tb = Time.realtimeSinceStartup; while ((Ses.Cine.Busy || TD.Active || Ses.EmergencyWhy != null || Ses.EmergencyTail) && Time.realtimeSinceStartup - tb < 40f) { _ft.Add(Time.unscaledDeltaTime); yield return null; } continue; }
                // an appointment that is due elsewhere: go there (a player walks over), then the top row waits there until they come
                var due = Ses.Sim.DueAppointment();
                if (due != null && due.Room >= 0)
                {
                    var rm = S.Layout.Room(due.Room);
                    if (rm != null) { var at = Ses.Sim.SnapPublic(new P3(rm.Floor, rm.Rect.CX, rm.Rect.CZ)); Ses.Player.Teleport(at, 0f); Log($"time:   to the appointment in {rm.Name} ({Cast.GivenOf(due.Actor)})"); yield return Wait(0.5f); }
                }
                Ses.Menu.OpenWait(); yield return Wait(0.35f);
                string top = Clean(Ses.Menu.ProbeRowLabel(1)) ?? "";
                if (top.Length == 0) { Ses.Menu.Close(); Log("time:   the T menu had no top row"); break; }
                double before = S.Clock; int skips0 = TD.SkipCount;
                Ses.Menu.ProbeRow(1); presses++;
                if (Ses.Menu.Open) Ses.Menu.Close();
                if (top.Contains("잠자리") || top.Contains("방으로 돌아가")) sleeps++;
                // until it (and whatever it led to) is over
                float ts = Time.realtimeSinceStartup;
                yield return null; yield return null;
                yield return WaitSkipIdle(45f);   // (logs STUCK and cancels when the ticks stop; a slow but stepping skip is waited for)
                if (TD.SkipCount == skips0) { Log($"time:   press {presses}: '{top}' started nothing ({TD.LastRefusal ?? "-"})"); if (presses > 40) break; yield return Wait(0.5f); continue; }
                var r = TD.LastResult; var st = r?.Stop; string sk = st?.Kind.ToString() ?? "-";
                kinds[sk] = (kinds.TryGetValue(sk, out var kc) ? kc : 0) + 1;
                bool stopped = st != null && st.Kind != StopKind.Target && st.Kind != StopKind.Cancelled;
                if (stopped) interrupts++;
                Log($"time:   press {presses}: '{top}' → {ClockFmt.DayHM(before)}–{ClockFmt.DayHM(S.Clock)} ({S.Clock - before:0} min, {Time.realtimeSinceStartup - ts:0.0}s) stop {sk} '{st?.Title}'{(st?.Sub != null ? " · " + st.Sub : "")}{(TD.LastPlan?.Passed?.Count > 0 ? " | 그 사이: " + string.Join(" / ", TD.LastPlan.Passed.Take(3)) : "")}");
                if (stopped && !bannerShot && TD.UI.BannerShowing) { bannerShot = true; yield return ShotCo("smartday_interrupt"); }
                // someone came to talk: now and then answer them (the conversation's minutes pass after it), else wait on
                if (st != null && st.Kind == StopKind.Approach && st.Actor != null && talks < 3)
                {
                    var who = S.A(st.Actor);
                    if (who != null && who.Alive && Ses.Sim.CanTalk(who, out _))
                    {
                        if (who.Pos.f != S.Player.Pos.f || who.Pos.DistXZ(S.Player.Pos) > 2.5f) { var near = NearIn(who.Pos, who.Room, 1.2f); Ses.Player.Teleport(near, MathX.AngleDeg(who.Pos.x - near.x, who.Pos.z - near.z)); yield return Wait(0.3f); }
                        talks++; Ses.Dialogue.Open(who); yield return Wait(0.8f);
                        if (Ses.Dialogue.Active) { Ses.Dialogue.ProbeFinishLines(); if (Ses.Dialogue.ProbeChoose("req_accept") || Ses.Dialogue.ProbeChoose("chat")) { yield return Wait(0.3f); Ses.Dialogue.ProbeFinishLines(); } }
                        Ses.Dialogue.Close(); yield return Wait(0.3f); yield return WaitSkipIdle(10f);
                        Log($"time:   talked with {who.Id} (now {ClockFmt.DayHM(S.Clock)})");
                    }
                }
                if (Ses.Dialogue.Active) Ses.Dialogue.Close();
                if (!S.Player.Alive) break;
            }
            int inc1 = S.Incidents.Values.Count(i => i.Loop == S.Loop);
            var first = S.Incidents.Values.Where(i => i.Loop == S.Loop).OrderBy(i => i.DeathClock).FirstOrDefault();
            Log($"time: smart days — {presses} presses ({sleeps} to bed), {interrupts} stopped by something ({string.Join(", ", kinds.Select(k => k.Key + "=" + k.Value))}), {talks} answered; {ClockFmt.DayHM(c0)} → {ClockFmt.DayHM(S.Clock)} in {Time.realtimeSinceStartup - t0:0}s real; phase {S.Phase}; "
                + (inc1 > inc0 && first != null ? $"a death in passed time: {Cast.GivenOf(first.Victim)} at {ClockFmt.DayHM(first.DeathClock)} (discovered={first.Discovered}, confirmed={first.Confirmed})" : "no death yet")
                + $"; skips {TD.SkipCount}, stalls {TD.Stalls}");
            if (inc1 > inc0) yield return ShotCo("smartday_after_death");
        }

        // ================================================================== investigation (OnDemand)
        IEnumerator TimeInvestigation()
        {
            TimeClean(); yield return WaitSkipIdle(10f); yield return Wait(0.8f);
            Log("time: investigation — " + (Ses.Hud.ProbeInfo ?? "").Replace("\n", " | "));
            yield return ShotCo("invest_budget");
            double c0 = S.Clock; Ses.Cine.Wait(10); yield return WaitSkipIdle(20f);
            Log($"time: investigation wait 10 — clock +{(S.Clock - c0):0.0} (want 10) [{TD.LastLog}]");
            // after 20 minutes of investigation: end it from the T menu (confirm) → assembly → trial
            float t0 = Time.realtimeSinceStartup; Ses.Speed = 30f;
            while (S.Phase == Phase.Investigation && S.Clock - S.PhaseStart < 21 && Time.realtimeSinceStartup - t0 < 120f) { if (Ses.Speed < 30) Ses.Speed = 30; if (Ses.Dialogue.Active) Ses.Dialogue.Close(); _ft.Add(Time.unscaledDeltaTime); yield return null; }
            Ses.Speed = 1f;
            if (S.Phase != Phase.Investigation) { Log("time: investigation ended before the T-menu step (" + S.Phase + ")"); yield break; }
            Ses.Menu.OpenWait(); yield return Wait(0.7f); yield return ShotCo("wait_menu_investigation_od");
            bool row = Ses.Menu.ProbeRow(2); yield return Wait(0.5f); yield return ShotCo("end_investigation_confirm");
            bool yes = row && Ses.Menu.ProbeConfirm(); yield return Wait(0.8f);
            Log($"time: end investigation via T menu: row={row} confirm={yes} → phase {S.Phase}");
            if (Ses.Menu.Open) Ses.Menu.Close();
        }

        // ================================================================== trial
        IEnumerator TimeTrial()
        {
            bool lev = GameObject.Find("Leviathan") != null;
            Log($"time: courtroom leviathan present: {(lev ? "yes" : "no")} (owner: courtroom)");
            yield break;
        }
    }
}
