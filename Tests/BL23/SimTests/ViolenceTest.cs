using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;

/// <summary>
/// violence [seed]: forces every prolonged kill (rear / front ligature, bare hands, smother, drowning in the pool and in a
/// sink), an intervention and an escape attempt, a restraint that is worked free, a revolver shot, a crossbow shot, a shotgun
/// blast into a wall, a body dragged by someone too weak to lift it, and the "Shoot" / "Bind" plans chosen by NPC culprits.
/// Each scenario runs in a fresh house. Prints the phase timelines, what was heard, traces and wounds; checks save round trips.
/// </summary>
public static partial class Program
{
    static int ViolenceTest(string[] args)
    {
        ulong seed = args.Length > 1 && ulong.TryParse(args[1], out var sd) ? sd : 20260926UL;
        Simulation sim = null; GameState S = null; List<Actor> npcs = null; int fails = 0, diffs = 0, faults = 0;
        var used = new HashSet<string>();
        void Fresh()
        {
            if (sim != null)
            {
                var j = SaveStore.Serialize(S); if (SaveStore.Serialize(SaveStore.Deserialize(j)) != j) { diffs++; Console.WriteLine("     ROUNDTRIP DIFF"); }
                faults += sim.Faults; foreach (var l in S.DevLog.Where(x => x.Contains("EXC")).Take(3)) Console.WriteLine("   " + l);
            }
            sim = Simulation.NewCampaign(seed); sim.Headless = true; S = sim.S; S.Phase = Phase.Daily; sim.RunTicks(20);
            npcs = S.LivingNpcs.OrderBy(a => a.Id).ToList(); used.Clear();
        }
        Fresh();
        Console.WriteLine($"violence seed {seed} layout {S.Layout.Hash}");
        Actor Pick(string id) => S.A(id) ?? npcs.First();

        // ---------------------------------------------------------------- helpers
        Room RoomOf(params RoomType[] types) { foreach (var t in types) { var r = S.Layout.Rooms.Where(x => x.Type == t && !x.Void).OrderBy(x => x.Id).FirstOrDefault(); if (r != null) return r; } return S.Layout.Rooms.First(x => x.Type == RoomType.Lounge); }
        P3 Free(Room r, int k)
        {
            var g = S.Layout.Nav(r.Floor); var cells = new List<int>();
            for (int c = 0; c < g.Room.Length; c++) if (g.Room[c] == r.Id && g.Walkable(c) && g.InMain(c)) cells.Add(c);
            if (cells.Count == 0) return new P3(r.Floor, r.Rect.CX, r.Rect.CZ);
            var mid = new P3(r.Floor, r.Rect.CX, r.Rect.CZ);
            cells = cells.OrderBy(c => g.Center(c).DistXZ(mid)).ToList();
            return g.Center(cells[Math.Min(k, cells.Count - 1)]);
        }
        void Freeze(Actor a) { a.Act = null; a.NextThink = S.Clock + 99999; a.Speed = 0; a.TalkingTo = null; a.Following = null; }
        void Isolate(params Actor[] keep)
        {
            // everyone else goes to (and stays in) their own room: no accidental witnesses, but still ears in the house
            foreach (var x in S.LivingNpcs.OrderBy(x => x.Id).ToList())
            {
                if (keep.Contains(x)) continue;
                var bed = S.Layout.BedroomOf(x.Id); if (bed != null && x.Room != bed.Id) { x.Pos = Free(bed, 0); x.Room = bed.Id; }
                Freeze(x);
            }
            foreach (var k in keep) { Freeze(k); S.K(k.Id).Open?.Clear(); }
        }
        void Put(Actor a, P3 p, float yaw) { a.Pos = p; a.Room = S.Layout.RoomAt(p); a.Yaw = yaw; a.Pose = Pose.Stand; }
        P3 Behind(P3 p, float yaw, float d) { double r = yaw * Math.PI / 180; return new P3(p.f, p.x - (float)Math.Sin(r) * d, p.z - (float)Math.Cos(r) * d); }
        Item Give(Actor a, string type)
        {
            var it = new Item { Id = S.NewId("it"), Type = type, Holder = a.Id, Room = -1 };
            S.Items[it.Id] = it; if (ItemCatalog.Get(type).Size <= 0.12f) a.Pocket.Add(it.Id); else if (a.HandR == null) a.HandR = it.Id; else a.HandL = it.Id;
            return it;
        }
        void Attack(Actor a, Actor t, string tag) => sim.Assign(a, new Activity { Id = "test:attack", Label = "test", Priority = 100, Interruptible = false, Steps = { new ActionStep { Kind = "Attack", Actor = t.Id, Tag = tag } } });
        long tickFrom = 0; int traceFrom = 0; int ledgerFrom = 0;
        void Mark0() { tickFrom = S.Tick; traceFrom = S.Traces.Count; ledgerFrom = S.Ledger.Count; }
        void Report(string title, Assault x, Actor a, Actor t)
        {
            Console.WriteLine($"== {title}: {Cast.GivenOf(a.Id)}({a.Id}) → {Cast.GivenOf(t.Id)}({t.Id})  kind={x?.Kind}  outcome={x?.Phase} ({x?.Outcome})  {(S.Tick - tickFrom) / 10.0:0.0}s");
            if (x != null)
            {
                foreach (var l in x.Log) Console.WriteLine("     " + l);
                Console.WriteLine($"     planned: seize {x.SeizeT / 10.0:0.0}s struggle {x.StruggleT / 10.0:0.0}s weaken {x.WeakenT / 10.0:0.0}s hold {x.HoldT / 10.0:0.0}s | strength v={x.VStr:0.00} a={x.AStr:0.00} | scratches {x.Scratches} bites {x.Bites} knocks {x.Knocks} splashes {x.Splashes} noises {x.Noises} resists {x.Resists}");
            }
            double since = S.Clock - (S.Tick - tickFrom) / 10.0 * S.ClockRate - 0.01;
            var heard = S.Actors.Values.SelectMany(p => S.K(p.Id).Heard.Where(h => h.Clock >= since).Select(h => (p.Id, h))).ToList();
            foreach (var g in heard.GroupBy(h => h.h.Kind).OrderBy(g => g.Key)) Console.WriteLine($"     heard {g.Key}: {g.Count()} times by {g.Select(h => h.Id).Distinct().Count()} people (max level {g.Max(h => h.h.Loud):0.00})");
            foreach (var tr in S.Traces.Skip(traceFrom)) Console.WriteLine($"     trace {tr.Type}: {tr.Desc} @ {S.RoomName(tr.Room)}{(tr.Note != null ? "  [" + tr.Note + "]" : "")}");
            foreach (var w in t.Body.Wounds.Where(w => w.Tick >= tickFrom)) Console.WriteLine($"     victim wound: {w.Kor}");
            foreach (var w in a.Body.Wounds.Where(w => w.Tick >= tickFrom)) Console.WriteLine($"     attacker wound: {w.Kor}");
            var types = S.Ledger.Skip(ledgerFrom).Select(e => e.Type).Where(ty => ty != "Sound" && ty != "Enter" && ty != "PlanStep" && ty != "Speech").Distinct();
            Console.WriteLine($"     ledger: {string.Join(", ", types)}");
            Console.WriteLine($"     victim now: {t.Status} pose={t.Pose} alive={t.Alive}{(t.Alive ? "" : " cause=" + t.Body.DeathCause)}");
            if (!t.Alive) { var ev = Evidences.ExamineBody(sim, S.Player, t, true); if (ev != null) Console.WriteLine("     examined: " + (ev.Desc ?? "").Replace("\n", " / ")); }
        }
        Assault RunAssault(Actor a, Actor t, int cap = 1500)
        {
            Assault x = null;
            for (int i = 0; i < cap; i++) { sim.Step(); x = x ?? S.Violence.Assaults.LastOrDefault(q => q.Attacker == a.Id && q.Victim == t.Id && q.Begun >= tickFrom); if (x != null && !x.Active) break; if (S.Out.Count > 2000) S.Out.Clear(); }
            for (int i = 0; i < 20; i++) sim.Step();
            return x;
        }
        (Actor a, Actor t) Pair(string ai, string ti) { var a = Pick(ai); var t = Pick(ti); if (!a.Alive || used.Contains(a.Id)) a = npcs.First(n => n.Alive && !used.Contains(n.Id) && n != t); if (!t.Alive || used.Contains(t.Id)) t = npcs.First(n => n.Alive && !used.Contains(n.Id) && n != a); used.Add(a.Id); used.Add(t.Id); return (a, t); }

        // ---------------------------------------------------------------- 1. a cord from behind
        {
            var (a, t) = Pair("P18", "P09"); var room = RoomOf(RoomType.Lounge, RoomType.Parlor); Isolate(a, t);
            var p = Free(room, 3); Put(t, p, 0f); Put(a, sim.SnapPublic(Behind(p, 0f, 0.5f)), 0f);
            Give(a, "Rope"); Mark0(); Attack(a, t, "strangle");
            var x = RunAssault(a, t); Report("STRANGLE (rear ligature)", x, a, t); if (x == null || t.Alive) fails++;
        }
        // ---------------------------------------------------------------- 2. bare hands, face to face
        Fresh();
        {
            var (a, t) = Pair("P06", "P12"); var room = RoomOf(RoomType.Library, RoomType.Study); Isolate(a, t);
            var p = Free(room, 2); Put(t, p, 180f); Put(a, sim.SnapPublic(Behind(p, 180f, -0.6f)), 0f);
            Mark0(); Attack(a, t, "throttle");
            var x = RunAssault(a, t); Report("STRANGLE (manual, front)", x, a, t); if (x == null) fails++;
        }
        // ---------------------------------------------------------------- 3. a scarf from the front
        Fresh();
        {
            var (a, t) = Pair("P16", "P15"); var room = RoomOf(RoomType.Wardrobe, RoomType.MusicRoom); Isolate(a, t);
            var p = Free(room, 1); Put(t, p, 90f); Put(a, sim.SnapPublic(new P3(p.f, p.x + 0.6f, p.z)), -90f);
            Give(a, "Scarf"); Mark0(); Attack(a, t, "strangle");
            var x = RunAssault(a, t); Report("STRANGLE (front ligature)", x, a, t); if (x == null) fails++;
        }
        // ---------------------------------------------------------------- 4. a pillow on a sleeper (wakes and fights)
        Fresh();
        {
            var (a, t) = Pair("P14", "P02"); var bed = S.Layout.BedroomOf(t.Id) ?? RoomOf(RoomType.GuestRoom); Isolate(a, t);
            var sp = bed.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Tag == "sleep");
            var p = sp != null ? sp.Pos : Free(bed, 0); Put(t, p, 0f); t.Pose = Pose.Sleep; if (sp != null) { sp.Occupant = t.Id; t.Spot = sp.Id; }
            Put(a, sp != null ? sp.Approach : Free(bed, 1), 0f); a.Yaw = MathX.AngleDeg(p.x - a.Pos.x, p.z - a.Pos.z);
            Mark0(); Attack(a, t, "smother");
            var x = RunAssault(a, t); Report("SMOTHER (natural sleep)", x, a, t); if (x == null) fails++;
        }
        // ---------------------------------------------------------------- 5. a pillow on a drugged sleeper
        Fresh();
        {
            var (a, t) = Pair("P10", "P12"); var bed = S.Layout.BedroomOf(t.Id) ?? RoomOf(RoomType.GuestRoom); Isolate(a, t);
            var sp = bed.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Tag == "sleep");
            var p = sp != null ? sp.Pos : Free(bed, 0); Put(t, p, 0f); if (sp != null) { sp.Occupant = t.Id; t.Spot = sp.Id; }
            t.Pose = Pose.Sleep; t.Status = ActorStatus.Unconscious; t.Body.UnconsciousUntil = S.Clock + 150; S.Flags["sedated:" + t.Id] = S.Clock;
            Put(a, sp != null ? sp.Approach : Free(bed, 1), 0f); a.Yaw = MathX.AngleDeg(p.x - a.Pos.x, p.z - a.Pos.z);
            Mark0(); Attack(a, t, "smother");
            var x = RunAssault(a, t); Report("SMOTHER (sedated)", x, a, t); if (x == null) fails++;
        }
        // ---------------------------------------------------------------- 6. drowned in the pool by the hair
        Fresh();
        {
            var (a, t) = Pair("P10", "P17"); var pool = RoomOf(RoomType.Pool); Isolate(a, t);
            var water = pool.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(f => f.Type == "PoolWater");
            var p = water != null ? sim.SnapPublic(new P3(water.Pos.f, water.Pos.x + water.W / 2 + 1.4f, water.Pos.z)) : Free(pool, 0);
            Put(t, p, -90f); Put(a, sim.SnapPublic(new P3(p.f, p.x + 0.9f, p.z)), -90f);
            Mark0(); Attack(a, t, "drown");
            var x = RunAssault(a, t); Report($"DROWN ({x?.WaterType ?? "no water"})", x, a, t); if (x == null || x.Kind != AssaultKind.Drown) fails++;
        }
        // ---------------------------------------------------------------- 7. drowned in a kitchen sink
        Fresh();
        {
            var (a, t) = Pair("P07", "P13"); var kit = RoomOf(RoomType.Kitchen, RoomType.Laundry); Isolate(a, t);
            var sink = kit.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(f => f.Type == "Sink");
            var p = sink != null ? sim.FrontOf(sink, 0.6f) : Free(kit, 0);
            Put(t, p, 0f); Put(a, sim.SnapPublic(Behind(p, 0f, 0.6f)), 0f);
            Mark0(); Attack(a, t, "drown");
            var x = RunAssault(a, t); Report($"DROWN ({x?.WaterType ?? "no water"})", x, a, t); if (x == null) fails++;
        }
        // ---------------------------------------------------------------- 8. an intervention (someone pulls the attacker off)
        Fresh();
        {
            var (a, t) = Pair("P08", "P11"); var room = RoomOf(RoomType.GameRoom, RoomType.Gallery); var o = npcs.First(n => n.Alive && !used.Contains(n.Id)); used.Add(o.Id); Isolate(a, t, o);
            var p = Free(room, 2); Put(t, p, 0f); Put(a, sim.SnapPublic(Behind(p, 0f, 0.5f)), 0f);
            var hall = RoomOf(RoomType.GrandHall); Put(o, Free(hall, 0), 0f);   // out of sight when it starts: comes running at the noise
            Give(a, "CurtainCord"); Mark0(); Attack(a, t, "strangle");
            for (int i = 0; i < 40; i++) sim.Step();
            sim.Assign(o, new Activity { Id = "case:intervene", Label = "싸움 제지", Priority = 150, Interruptible = false, Steps = { new ActionStep { Kind = "GoTo", Target = a.Pos, HasTarget = true, Run = true }, new ActionStep { Kind = "Wait", Duration = 5 } } });
            var x = RunAssault(a, t); Report($"INTERVENTION by {Cast.GivenOf(o.Id)}", x, a, t); if (x == null || !t.Alive) fails++;
        }
        // ---------------------------------------------------------------- 9. escape (a strong victim, a weakened attacker, face to face)
        Fresh();
        {
            var strongest = npcs.Where(n => n.Alive).OrderByDescending(Violence.Strength).First(); used.Add(strongest.Id);
            var weakest = npcs.Where(n => n.Alive && !used.Contains(n.Id)).OrderBy(Violence.Strength).First(); used.Add(weakest.Id);
            var room = RoomOf(RoomType.Chapel, RoomType.TeaRoom, RoomType.Lounge); Isolate(weakest, strongest);
            weakest.Body.HandL = weakest.Body.HandR = 0.45f;
            var p = Free(room, 4); Put(strongest, p, 0f); Put(weakest, sim.SnapPublic(new P3(p.f, p.x, p.z + 0.6f)), 180f);
            Mark0(); Attack(weakest, strongest, "throttle");
            var x = RunAssault(weakest, strongest); Report("ESCAPE ATTEMPT (weak attacker)", x, weakest, strongest);
            for (int i = 0; i < 150; i++) sim.Step();
            Console.WriteLine($"     afterwards the victim: act={strongest.Act?.Id} knows attacker={S.K(strongest.Id).Facts.Contains("attacked-by:" + weakest.Id)}");
        }
        // ---------------------------------------------------------------- 10. restraint: knocked out, tied, works free
        Fresh();
        {
            var (a, t) = Pair("P05", "P03"); var room = RoomOf(RoomType.Storage, RoomType.Closet, RoomType.Study); Isolate(a, t);
            var p = Free(room, 1); Put(t, p, 0f); Put(a, sim.SnapPublic(new P3(p.f, p.x + 0.7f, p.z)), -90f);
            sim.Collapse(t, "test knock-out"); t.Body.UnconsciousUntil = S.Clock + 4;
            var rope = Give(a, "Rope"); Mark0();
            bool can = Violence.CanBind(S, a, t, null, out var why);
            var b = can ? Violence.Bind(sim, a, t, rope, true, true, true, "test") : null;
            Console.WriteLine($"== RESTRAINT: {Cast.GivenOf(a.Id)} ties {Cast.GivenOf(t.Id)} ({t.Status}) can={can} {why} → {(b != null ? $"{b.Material} wrists={b.Wrists} ankles={b.Ankles} gag={b.Gag} tight={b.Tight:0.00}" : "none")}");
            a.Pos = Free(RoomOf(RoomType.GrandHall), 0); a.Room = S.Layout.RoomAt(a.Pos);   // the binder leaves
            double t0 = S.Clock; long start = S.Tick;
            for (int i = 0; i < 6000 && b != null && !b.Off; i++)
            {
                sim.Step(); if (S.Out.Count > 2000) S.Out.Clear();
                if ((S.Tick - start) % 300 == 0) Console.WriteLine($"     {(S.Tick - start) / 10.0,6:0.0}s  status={t.Status} act={t.Act?.Id ?? "-"} anim={t.Anim} loose={b.Loose:0.00} speech={t.Body.Speech:0.00} hands={t.Body.HandR:0.00} mob={t.Body.Mobility:0.00}");
            }
            Console.WriteLine($"     freed after {(S.Tick - start) / 10.0:0.0}s ({S.Clock - t0:0} clock min): {b?.Why} (by {b?.OffBy}); marks: {string.Join(" / ", t.Body.Wounds.Where(w => w.CauseEvent == "bound").Select(w => w.Kor))}; the rope: {string.Join(",", S.I(rope.Id).Surface)} in {S.RoomName(S.I(rope.Id).Room)}");
            Console.WriteLine($"     ledger: {string.Join(", ", S.Ledger.Skip(ledgerFrom).Select(e => e.Type).Where(ty => ty == "Bind" || ty == "Unbind" || ty == "WorkedFree" || ty == "AttackSurvived" || ty == "UntieStart" || ty == "WakeUp").Distinct())}");
            var ev = Evidences.ExamineItem(sim, S.Player, S.I(rope.Id)); if (ev != null) Console.WriteLine("     rope examined: " + (ev.Desc ?? "").Replace("\n", " / "));
            if (b == null || !b.Off) fails++;
        }
        // ---------------------------------------------------------------- 11. a revolver shot
        Fresh();
        {
            var (a, t) = Pair("P04", "P16");
            var room = RoomOf(RoomType.Study, RoomType.Library, RoomType.Lounge); Isolate(a, t);
            var p = Free(room, 0); var q = Free(room, 25); Put(a, p, MathX.AngleDeg(q.x - p.x, q.z - p.z)); Put(t, q, MathX.AngleDeg(p.x - q.x, p.z - q.z));
            var gun = Give(a, "Revolver"); Give(a, "Cartridges"); Mark0();
            Console.WriteLine($"== REVOLVER: load → {Violence.Load(sim, a, gun) ?? "ok"} rounds={Violence.Gun(S, gun.Id).Loaded}  dist={a.Pos.DistXZ(t.Pos):0.0}m line of fire={Violence.LineOfFire(sim, a, t)}");
            Attack(a, t, "shoot");
            for (int i = 0; i < 300 && t.Alive && t.Status == ActorStatus.Active; i++) { sim.Step(); if (S.Out.Count > 2000) S.Out.Clear(); }
            for (int i = 0; i < 200; i++) sim.Step();
            foreach (var sh in S.Violence.Shots.Where(s => s.Tick >= tickFrom && s.By == a.Id)) Console.WriteLine($"     shot {sh.WeaponType} yaw {sh.Yaw:0} pitch {sh.Pitch:0.0} → {string.Join(" ; ", sh.Impacts.Select(i => i.Kind + (i.Actor != null ? " " + i.Actor + " " + i.Region + " sev" + i.Sev : i.Furniture >= 0 ? " " + S.Layout.Furniture[i.Furniture].Type : "") + $" @{i.Dist:0.0}m y{i.Y:0.00}" + (i.Through ? " (through)" : "")))}");
            Report("REVOLVER", null, a, t);
            var ears = S.Actors.Values.Where(x => x.Alive && x != a && S.K(x.Id).Heard.Any(h => h.Kind == SoundKind.Gunshot)).ToList();
            Console.WriteLine($"     gunshot heard by {ears.Count}/{S.Actors.Values.Count(x => x.Alive && x != a)} living people; gsr on shooter={S.Flags.ContainsKey("gsr:" + a.Id)}; gun loaded={Violence.Gun(S, gun.Id).Loaded} spent={Violence.Gun(S, gun.Id).Spent}");
            var gev = Evidences.ExamineItem(sim, S.Player, gun); if (gev != null) Console.WriteLine("     gun examined: " + (gev.Desc ?? "").Replace("\n", " / "));
            if (!S.Violence.Shots.Any(s => s.Tick >= tickFrom)) fails++;
        }
        // ---------------------------------------------------------------- 12. a crossbow bolt (almost silent)
        Fresh();
        {
            var (a, t) = Pair("P11", "P18");
            var room = RoomOf(RoomType.TrophyRoom, RoomType.Gallery, RoomType.Lounge); Isolate(a, t);
            var p = Free(room, 0); var q = Free(room, 40); Put(a, p, MathX.AngleDeg(q.x - p.x, q.z - p.z)); Put(t, q, 0f);
            var bow = Give(a, "Crossbow"); Give(a, "BoltQuiver"); Mark0();
            Console.WriteLine($"== CROSSBOW: cock → {Violence.Load(sim, a, bow) ?? "ok"}  dist={a.Pos.DistXZ(t.Pos):0.0}m");
            float yaw = MathX.AngleDeg(t.Pos.x - a.Pos.x, t.Pos.z - a.Pos.z); float y0 = Violence.MuzzleY(a); float dist = a.Pos.DistXZ(t.Pos);
            var shot = Violence.Fire(sim, a, bow, y0, yaw, (float)(Math.Atan2(1.2f - y0, dist) * 180 / Math.PI), t.Id);
            for (int i = 0; i < 200; i++) sim.Step();
            Console.WriteLine($"     bolt → {string.Join(" ; ", shot?.Impacts.Select(i => i.Kind + (i.Actor != null ? " " + i.Actor + " " + i.Region : "") + $" @{i.Dist:0.0}m" + (i.Item != null ? " [" + i.Item + " " + string.Join(",", S.I(i.Item)?.Surface ?? new List<string>()) + "]" : "")) ?? new string[0])}");
            Report("CROSSBOW", null, a, t);
            Console.WriteLine($"     embedded records: {S.Violence.Embedded.Count}; gunshot alarms: {S.Actors.Values.Count(x => S.K(x.Id).Heard.Any(h => h.Kind == SoundKind.Gunshot))}");
            if (shot == null) fails++;
        }
        // ---------------------------------------------------------------- 13. a shotgun blast into the wall
        Fresh();
        {
            var a = npcs.First(n => n.Alive); used.Add(a.Id);
            var room = RoomOf(RoomType.GrandHall); Isolate(a);
            var p = Free(room, 0); Put(a, p, 90f);
            var sg = Give(a, "HuntingShotgun"); Give(a, "ShotShells"); Violence.Load(sim, a, sg); Mark0();
            var shot = Violence.Fire(sim, a, sg, Violence.MuzzleY(a), 90f, 0f, null);
            Console.WriteLine($"== SHOTGUN into the wall: {string.Join(" ; ", shot?.Impacts.Select(i => i.Kind + (i.Furniture >= 0 ? " " + S.Layout.Furniture[i.Furniture].Type : "") + $" @{i.Dist:0.0}m") ?? new string[0])}");
            Violence.Load(sim, a, sg);
            Console.WriteLine($"     traces: {string.Join(" | ", S.Traces.Skip(traceFrom).Select(tr => tr.Type + ":" + tr.Desc))}; shells on the floor: {S.Items.Values.Count(i => i.Type == "SpentShell")}; heard by {S.Actors.Values.Count(x => S.K(x.Id).Heard.Any(h => h.Kind == SoundKind.Gunshot))}");
        }
        // ---------------------------------------------------------------- 14. carrying vs dragging a body
        Fresh();
        {
            var weak = npcs.Where(n => n.Alive).OrderBy(Violence.LiftCapacity).First();
            var body = npcs.Where(n => n.Alive && n != weak).OrderByDescending(Violence.BodyMass).First();
            var strong = npcs.Where(n => n.Alive).OrderByDescending(Violence.LiftCapacity).First();
            Isolate(weak, body); sim.Strike(weak.Id, body, BodyRegion.Chest, DamageType.Stab, 4, null, "test"); for (int i = 0; i < 300 && body.Alive; i++) sim.Step();
            S.K(weak.Id).KnownDead.Add(body.Id); S.Flags["seenbody:" + weak.Id + ":" + body.Id] = S.Clock;   // the killer does not "discover" the body in their own hands
            Console.WriteLine($"== CARRY/DRAG: body {body.Id} {Violence.BodyMass(body):0}kg (alive={body.Alive}) | weakest {weak.Id} lifts {Violence.LiftCapacity(weak):0}kg | strongest {strong.Id} lifts {Violence.LiftCapacity(strong):0}kg");
            Put(weak, sim.SnapPublic(body.Pos), 0f); weak.Carrying = body.Id; body.CarriedBy = weak.Id; Mark0();
            var dest = RoomOf(RoomType.GrandHall); sim.Assign(weak, new Activity { Id = "test:drag", Priority = 100, Interruptible = false, Steps = { new ActionStep { Kind = "GoTo", Target = Free(dest, 5), HasTarget = true } } });
            var p0 = weak.Pos; int n = 0; float maxSpeed = 0; int dev0 = S.DevLog.Count;
            for (; n < 1500 && weak.Act != null; n++) { sim.Step(); maxSpeed = Math.Max(maxSpeed, weak.Speed); }
            foreach (var l in S.DevLog.Skip(dev0).Where(l => l.Contains(weak.Id)).Take(3)) Console.WriteLine("     dev: " + l);
            Console.WriteLine($"     dragging={Violence.Dragging(S, weak)} anim={weak.Anim} speed {maxSpeed:0.00} m/s; moved {p0.Dist(weak.Pos):0.0}m in {n / 10.0:0.0}s; traces: {string.Join(" | ", S.Traces.Skip(traceFrom).GroupBy(tr => tr.Type).Select(g => g.Key + " x" + g.Count()))}");
            var one = S.Traces.Skip(traceFrom).FirstOrDefault(tr => tr.Note == "drag"); if (one != null) { var ev = Evidences.ExamineTrace(sim, S.Player, one); Console.WriteLine("     drag mark examined: " + (ev?.Desc ?? "").Replace("\n", " / ")); }
            weak.Carrying = null; body.CarriedBy = null; body.Pos = weak.Pos; body.Room = weak.Room;
        }
        // ---------------------------------------------------------------- 15. the planners: "Shoot" (gun / crossbow) and "Bind" chosen by NPC culprits
        foreach (var force in new[] { "Shoot", "Crossbow", "Bind" })
        {
            Fresh();
            SetPieces.Force = force;
            var s2 = Simulation.NewCampaign(seed ^ 0x51UL); s2.Headless = true; s2.S.Phase = Phase.Daily;
            bool Done() => s2.S.Ledger.Any(e => (force == "Bind" ? e.Type == "Bind" : e.Type == "Gunshot" || e.Type == "CrossbowShot")) && s2.S.Actors.Values.Any(x => !x.Alive);
            for (int i = 0; i < 4 * 14400 && !Done(); i++) { s2.Step(); if (s2.S.Out.Count > 2000) s2.S.Out.Clear(); if (s2.S.Phase == Phase.Trial) break; }
            for (int i = 0; i < 600; i++) s2.Step();
            SetPieces.Force = null;
            var plans = s2.S.Plans.Values.Where(p => p.Grammar != null && (p.Grammar.StartsWith("Shoot") || p.Grammar.Contains("+Bind"))).ToList();
            var ev = s2.S.Ledger.Where(e => e.Type == "Gunshot" || e.Type == "CrossbowShot" || e.Type == "Bind" || e.Type == "Unbind" || e.Type == "Reload" || e.Type == "ShotHit" || e.Type == "AssaultBegin" || e.Type == "AssaultEnd" || e.Type == "Death" && e.Actor != null).Select(e => $"{ClockFmt.DayHM(e.Clock)} {e.Type} {e.Actor}->{e.Target} {e.Data}").Take(10).ToList();
            Console.WriteLine($"== PLANNER force={force}: {plans.Count} plans: {string.Join(" / ", plans.Take(3).Select(p => p.Actor + "->" + p.Target + " " + p.Grammar + " [" + string.Join(",", p.Steps.Select(st => st.Kind + (st.Done ? "*" : ""))) + "] " + p.Stage + " | " + string.Join(" / ", p.Log.Skip(1).Take(3))))}");
            foreach (var l in ev) Console.WriteLine("     " + l);
            var inc = s2.S.Incidents.Values.FirstOrDefault(); if (inc != null) { Console.WriteLine($"     incident: {inc.Victim} by {inc.Culprit} method={inc.Method} weapon={inc.WeaponType} dmg={inc.Dmg}"); foreach (var line in Replay.Script(s2.S, new ReplaySegment { Incident = inc.Id, Events = s2.S.Ledger.Where(e => e.Plan == inc.PlanId || e.Target == inc.Victim).ToList() }).Take(12)) Console.WriteLine($"        {ClockFmt.HM(line.clock)} {line.text}"); }
            Console.WriteLine($"     faults={s2.Faults}"); faults += s2.Faults;
            foreach (var l in s2.S.DevLog.Where(x => x.Contains("EXC")).Take(3)) Console.WriteLine("     " + l);
            var j2 = SaveStore.Serialize(s2.S); if (SaveStore.Serialize(SaveStore.Deserialize(j2)) != j2) { diffs++; Console.WriteLine("     ROUNDTRIP DIFF"); }
        }
        // ---------------------------------------------------------------- the save round trip (every scenario's house)
        Fresh();
        Console.WriteLine($"faults={faults} fails={fails} roundtrip={(diffs == 0 ? "IDENTICAL" : "DIFF x" + diffs)}");
        return faults == 0 && fails == 0 && diffs == 0 ? 0 : 1;
    }
}
