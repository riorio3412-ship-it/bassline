using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using Newtonsoft.Json.Linq;

public static partial class Program
{
    /// <summary>
    /// Furniture changes: one fact, different people, different knowledge (Sim/Systems/FurnitureChanges.cs). Usage: furnknow [seed] [days]
    ///  1. 민혁 drags a chair (the physics bridge): the ledger has the fact with his name; he knows he did it; a resident watching in
    ///     the lit room saw it and saw him; one in the same room facing away only heard it; one behind a closed door, one upstairs:
    ///     nothing to see. The same resting pose reported twice is one move.
    ///  2. afterwards: the one facing away turns round and notices (no name, "since" = just now); a resident who had been in the
    ///     room earlier comes back and notices (no name, "since" = their last visit); a newcomer who never knew the room does not.
    ///  3. plain disorder (a table on its side) is noticed even by someone who never knew the room.
    ///  4. seeing depends on light: the same witness at the same spot sees a move in the lit room and not in the dark one.
    ///  5. hearing depends on the door: the neighbour hears a drag louder through an open door than through a closed one.
    ///  6. save/load round trip, an old save without these fields, and a loaded copy that goes on with a different move.
    ///  7. a campaign (default 6 days, chapter rule CH12 "이동식 전시" forced from chapter 2 so the house itself moves things):
    ///     what the house's own people saw and noticed of struggles, traps, rules — never a name in a later notice.
    /// </summary>
    static int FurnitureKnowledgeTest(string[] args)
    {
        ulong seed = args.Length > 1 && ulong.TryParse(args[1], out var sd) ? sd : 20260926UL;
        int days = args.Length > 2 && int.TryParse(args[2], out var dd) ? dd : 6;
        int ok = 0, fail = 0;
        void Check(bool cond, string what) { if (cond) ok++; else fail++; Console.WriteLine((cond ? "  ok   " : "  FAIL ") + what); }

        var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; sim.EndPrologue();
        sim.RunTicks(600);
        double far = S.Clock + 99999;
        foreach (var x in S.Actors.Values) { if (x.IsPlayer || x.IsButler) continue; x.Act = null; x.NextThink = far; x.Speed = 0; x.TalkingTo = null; }

        // ---- the room: lit, ground floor, a small piece near the middle, a door to a neighbouring room
        Room R = null, R2 = null; Furniture F = null; Door D = null;
        foreach (var room in S.Layout.Rooms.Where(r => r.Floor == 0 && !RoomInfo.IsPassage(r.Type) && r.Rect.W >= 6 && r.Rect.D >= 6 && sim.RoomLight(r.Id) > 0.6f).OrderBy(r => r.Id))
        {
            var c = new P3(0, room.Rect.CX, room.Rect.CZ);
            var f = room.Furniture.Select(i => S.Layout.Furniture[i]).Where(x => x.W * x.D < 1.0f && x.Type != "Rug" && x.Pos.DistXZ(c) < Math.Min(room.Rect.W, room.Rect.D) * 0.35f).OrderBy(x => x.Id).FirstOrDefault();
            var d = room.Doors.Select(i => S.Layout.Doors[i]).Where(x => !x.Sealed && x.RoomA >= 0 && x.RoomB >= 0).OrderBy(x => x.Id).FirstOrDefault();
            if (f == null || d == null) continue;
            R = room; F = f; D = d; R2 = S.Layout.Room(d.RoomA == room.Id ? d.RoomB : d.RoomA); break;
        }
        if (R == null) { Console.WriteLine("FAIL no suitable room"); return 1; }
        var R3 = S.Layout.Rooms.Where(r => r.Floor == 0 && r.Id != R.Id && r.Id != R2.Id && !r.Doors.Any(i => S.Layout.Doors[i].RoomA == R.Id || S.Layout.Doors[i].RoomB == R.Id))
                               .OrderByDescending(r => Math.Abs(r.Rect.CX - R.Rect.CX) + Math.Abs(r.Rect.CZ - R.Rect.CZ)).First();
        var Up = S.Layout.Rooms.Where(r => r.Floor != 0 && !RoomInfo.IsPassage(r.Type)).OrderBy(r => r.Id).First();
        Console.WriteLine($"== furnknow seed {seed}: {S.RoomName(R.Id)} (light {sim.RoomLight(R.Id):0.00}) {FurnitureCatalog.Get(F.Type)?.Kor} #{F.Id}; next door {S.RoomName(R2.Id)}; away {S.RoomName(R3.Id)}; upstairs {S.RoomName(Up.Id)}");

        P3 Snap(P3 p) => sim.SnapPublic(p);
        bool Clear(P3 a, P3 b) => S.Layout.Nav(a.f).Ray(a.x, a.z, b.x, b.z, i => S.Layout.Doors[i].Open, false, out _);
        // a walkable point in <room> about <r> metres from <c> with a clear line to it
        P3 Around(Room room, P3 c, float r, float offsetDeg = 0f)
        {
            for (int i = 0; i < 36; i++)
            {
                double a = (offsetDeg + i * 10) * Math.PI / 180;
                var p = Snap(new P3(c.f, c.x + (float)Math.Sin(a) * r, c.z + (float)Math.Cos(a) * r));
                if (S.Layout.RoomAt(p) == room.Id && Math.Abs(p.DistXZ(c) - r) < 0.6f && Clear(p, c)) return p;
            }
            throw new Exception($"no spot {r}m from {c} in {S.RoomName(room.Id)}");
        }
        P3 Centre(Room room) => Snap(new P3(room.Floor, room.Rect.CX, room.Rect.CZ));
        void Put(Actor a, P3 at, P3 look, bool away = false)
        {
            a.Pos = at; a.Room = S.Layout.RoomAt(at); a.StairId = -1; a.Pose = Pose.Stand; a.Act = null; a.Speed = 0;
            if (!a.IsPlayer) a.NextThink = far;
            a.Yaw = MathX.AngleDeg(look.x - at.x, look.z - at.z) + (away ? 180f : 0f);
        }
        List<FurnitureNote> Notes(Actor a, Furniture f) => S.K(a.Id).FurnitureNotes.Where(n => n.Furniture == f.Id).ToList();
        HeardSound LastScrape(Actor a, double since) => S.K(a.Id).Heard.LastOrDefault(h => h.Kind == SoundKind.Scrape && h.Clock >= since - 1e-6);
        string Name(string id) => id == null ? "—" : Cast.GivenOf(id) ?? id;

        var me = S.Player; var W = S.A("P02"); var B = S.A("P03"); var N = S.A("P04"); var Fa = S.A("P05"); var L = S.A("P06"); var X = S.A("P07"); var Y = S.A("P08");
        var centre = Centre(R);
        var dir = F.Pos.DistXZ(centre) > 0.3f ? new P3(0, (centre.x - F.Pos.x) / F.Pos.DistXZ(centre), (centre.z - F.Pos.z) / F.Pos.DistXZ(centre)) : new P3(0, 1, 0);

        // ---- 1. the move
        Console.WriteLine("-- 1. the move: who knows what");
        Put(me, sim.FrontOf(F), F.Pos);
        Put(W, Around(R, F.Pos, 3.0f), F.Pos);
        Put(B, Around(R, F.Pos, 3.5f, 150f), F.Pos, away: true);
        var doorAt = D.Pos; D.Open = false; D.Locked = false;
        Put(N, Around(R2, doorAt, 2.0f), doorAt, away: true);
        Put(Fa, Centre(Up), Centre(Up));
        Put(L, Around(R, F.Pos, 2.5f, 250f), F.Pos);   // L was here earlier (before the change) …
        sim.RunTicks(10);
        double tL = S.K(L.Id).RoomSeenAt.TryGetValue(R.Id, out var tl) ? tl : -1;
        Put(L, Centre(R3), Centre(R3));                // … and is away when it happens
        Put(X, Centre(R3), Centre(R3)); S.K(X.Id).RoomSeenAt.Remove(R.Id);   // X has never been in this room
        Put(Y, Centre(R3), Centre(R3)); S.K(Y.Id).RoomSeenAt.Remove(R.Id);
        sim.RunTicks(10);
        Check(tL > 0, $"{Name(L.Id)} remembers being in the room ({ClockFmt.HM(tL)})");

        var from = F.Pos; float fromYaw = F.Yaw; int ledger0 = S.Ledger.Count; double t1 = S.Clock;
        var to = new P3(F.Pos.f, F.Pos.x + dir.x * 0.8f, F.Pos.z + dir.z * 0.8f);
        sim.PhysicsFurnitureMoved(F, to, F.Yaw + 30f, Cast.Player);
        var fact = S.Ledger.Skip(ledger0).LastOrDefault(e => e.Type == "FurnitureMoved");
        Check(F.Rev == 1 && F.Pos.DistXZ(to) < 1e-3 && F.Room == R.Id, $"committed: rev {F.Rev}, moved {from.DistXZ(F.Pos):0.00}m, still in {S.RoomName(F.Room)}");
        Check(fact != null && fact.Actor == Cast.Player && !fact.Secret, $"fact in the ledger: FurnitureMoved by {Name(fact?.Actor)}");
        var nMe = Notes(me, F); Check(nMe.Count == 1 && nMe[0].Kind == FurnitureNoteKind.Did, $"{Name(me.Id)} knows he did it ({string.Join(",", nMe.Select(n => n.Kind))})");
        var nW = Notes(W, F); Check(nW.Count == 1 && nW[0].Kind == FurnitureNoteKind.Saw && nW[0].Who == Cast.Player && nW[0].WhoConf >= 0.3f, $"{Name(W.Id)} ({W.Pos.DistXZ(F.Pos):0.0}m, facing) saw it done by {Name(nW.FirstOrDefault()?.Who)} (conf {nW.FirstOrDefault()?.WhoConf:0.00})");
        Check(Notes(B, F).Count == 0 && LastScrape(B, t1) != null, $"{Name(B.Id)} (same room, back turned): no sight, heard \"{Simulation.SoundText(SoundKind.Scrape)}\" (loud {LastScrape(B, t1)?.Loud:0.00})");
        var hN = LastScrape(N, t1);
        Check(Notes(N, F).Count == 0, $"{Name(N.Id)} (next door, closed): no sight; heard: {(hN != null ? $"yes {hN.Loud:0.00} from '{S.RoomName(hN.GuessRoom)}'" : "no")}");
        var hFa = LastScrape(Fa, t1);
        Check(Notes(Fa, F).Count == 0 && (hFa == null || hFa.Loud < (LastScrape(B, t1)?.Loud ?? 1f)), $"{Name(Fa.Id)} (upstairs): nothing seen; heard: {(hFa != null ? hFa.Loud.ToString("0.00") : "no")}");
        Check(Notes(L, F).Count == 0 && S.K(L.Id).FurnitureKnown.TryGetValue(F.Id, out var mL) && mL.Pos.DistXZ(from) < 1e-3 && Math.Abs(mL.Seen - tL) < 1.0, $"{Name(L.Id)} (away) still knows it where it stood (last seen {ClockFmt.HM(S.K(L.Id).FurnitureKnown.TryGetValue(F.Id, out var ml2) ? ml2.Seen : -1)})");
        Check(Notes(X, F).Count == 0 && !S.K(X.Id).FurnitureKnown.ContainsKey(F.Id), $"{Name(X.Id)} (away, never in the room): no note, no memory of it");
        {
            int rev = F.Rev, led = S.Ledger.Count, notes = S.Know.Values.Sum(k => k.FurnitureNotes.Count);
            sim.PhysicsFurnitureMoved(F, new P3(F.Pos.f, F.Pos.x + 0.004f, F.Pos.z), F.Yaw + 0.3f, Cast.Player);
            Check(F.Rev == rev && S.Ledger.Count == led && S.Know.Values.Sum(k => k.FurnitureNotes.Count) == notes, "the same resting pose reported again is not a second move (no rev, no ledger, no notes)");
        }

        // ---- 2. afterwards
        Console.WriteLine("-- 2. afterwards");
        sim.RunTicks(40);
        Put(B, B.Pos, F.Pos); sim.RunTicks(10);
        var nB = Notes(B, F);
        Check(nB.Count == 1 && nB[0].Kind == FurnitureNoteKind.Noticed && nB[0].Who == null && nB[0].Since >= t1 - 1.0 && nB[0].How == "moved",
            $"{Name(B.Id)} turns round and notices it moved ({nB.FirstOrDefault()?.How}), no name, since {ClockFmt.HM(nB.FirstOrDefault()?.Since ?? -1)}");
        Put(L, Around(R, F.Pos, 2.5f, 250f), F.Pos); sim.RunTicks(10);
        var nL = Notes(L, F);
        Check(nL.Count == 1 && nL[0].Kind == FurnitureNoteKind.Noticed && nL[0].Who == null && Math.Abs(nL[0].Since - tL) < 1.0 && nL[0].Since < t1,
            $"{Name(L.Id)} comes back and notices ({nL.FirstOrDefault()?.How}), no name, since their last visit {ClockFmt.HM(nL.FirstOrDefault()?.Since ?? -1)}");
        Put(X, Around(R, F.Pos, 2.5f, 100f), F.Pos); sim.RunTicks(10);
        Check(Notes(X, F).Count == 0, $"{Name(X.Id)} walks in for the first time: a chair a little off its place means nothing to them");
        sim.RunTicks(20);
        Check(Notes(W, F).Count == 1 && Notes(me, F).Count == 1, "those who saw or did it do not \"notice\" it again");

        // ---- 3. plain disorder
        Console.WriteLine("-- 3. plain disorder");
        var G = R.Furniture.Select(i => S.Layout.Furniture[i]).Where(x => x.Id != F.Id && x.Type != "Rug" && x.Pos.DistXZ(centre) < 6f && x.Pos.DistXZ(F.Pos) > 1.2f).OrderBy(x => x.Pos.DistXZ(centre)).ThenBy(x => x.Id).First();
        Put(Y, Centre(R3), Centre(R3)); Put(X, Centre(R3), Centre(R3)); Put(W, Centre(R3), Centre(R3)); Put(L, Centre(R3), Centre(R3)); Put(B, Centre(R3), Centre(R3)); Put(me, Centre(R3), Centre(R3));
        sim.RunTicks(10);
        { int d0 = G.Damage; G.Marks.Add(Simulation.ToppledMark); G.Damage = Math.Max(1, G.Damage); G.Moved = true; sim.CommitFurnitureChange(G, null, "toppled", G.Pos, G.Yaw, d0); }
        Put(Y, Around(R, G.Pos, 2.5f), G.Pos); sim.RunTicks(10);
        var nY = Notes(Y, G);
        Check(nY.Count == 1 && nY[0].Kind == FurnitureNoteKind.Noticed && nY[0].How == "toppled" && nY[0].Since < 0 && nY[0].Who == null,
            $"{Name(Y.Id)}, new to the room, notices the {FurnitureCatalog.Get(G.Type)?.Kor} on its side (since: unknown)");
        Check(Notes(Y, F).Count == 0, $"{Name(Y.Id)} does not \"notice\" the chair they never knew");
        Put(me, Around(R, G.Pos, 2.0f, 90f), G.Pos); sim.RunTicks(10);
        var toast = S.Out.LastOrDefault(e => e.Type == GameEventType.Notice && e.Key == "furniture" && e.Id == G.Id);
        Check(toast != null, $"the player gets it as a notice: \"{toast?.Text}\"");

        // ---- 4. light
        Console.WriteLine("-- 4. light");
        var H = R.Furniture.Select(i => S.Layout.Furniture[i]).Where(x => x.Id != F.Id && x.Id != G.Id && x.Type != "Rug" && x.W * x.D < 2f).OrderBy(x => x.Pos.DistXZ(centre)).ThenBy(x => x.Id).First();
        var mover = S.A("P09"); var W2 = S.A("P10");
        Put(me, Centre(R3), Centre(R3)); Put(Y, Centre(R3), Centre(R3));
        Put(mover, sim.FrontOf(H), H.Pos);
        P3 w2at; try { w2at = Around(R, H.Pos, 6.0f); } catch { w2at = Around(R, H.Pos, 5.0f); }
        Put(W2, w2at, H.Pos);
        sim.RunTicks(10);
        sim.PhysicsFurnitureMoved(H, new P3(H.Pos.f, H.Pos.x + dir.x * 0.6f, H.Pos.z + dir.z * 0.6f), H.Yaw, mover.Id);
        var lit = Notes(W2, H).LastOrDefault();
        Check(lit != null && lit.Kind == FurnitureNoteKind.Saw && lit.Who == mover.Id, $"lit: {Name(W2.Id)} at {W2.Pos.DistXZ(H.Pos):0.0}m saw {Name(lit?.Who)} move it (light {sim.RoomLight(R.Id):0.00})");
        S.Darkness = 1f;
        int before = Notes(W2, H).Count;
        sim.PhysicsFurnitureMoved(H, new P3(H.Pos.f, H.Pos.x - dir.x * 0.6f, H.Pos.z - dir.z * 0.6f), H.Yaw, mover.Id);
        Check(Notes(W2, H).Count == before, $"dark: the same {Name(W2.Id)} at the same spot sees nothing (light {sim.RoomLight(R.Id):0.00}); {Name(mover.Id)} still knows: {Notes(mover, H).Count(n => n.Kind == FurnitureNoteKind.Did)} own moves");
        Check(S.Ledger.Count(e => e.Type == "FurnitureMoved" && e.Actor == mover.Id) == 2, "the ledger has both moves, dark or not");
        S.Darkness = 0f;

        // ---- 5. the door
        Console.WriteLine("-- 5. the door");
        Put(mover, sim.FrontOf(H), H.Pos); Put(W2, Centre(R3), Centre(R3));
        Put(N, Around(R2, doorAt, 2.0f), doorAt, away: true);
        float Drag(bool open)
        {
            D.Open = open; double t = S.Clock;
            var p0 = H.Pos; var away = new P3(H.Pos.f, H.Pos.x + dir.x * 2.0f, H.Pos.z + dir.z * 2.0f);
            if (S.Layout.RoomAt(away) != R.Id) away = new P3(H.Pos.f, H.Pos.x - dir.x * 2.0f, H.Pos.z - dir.z * 2.0f);
            sim.PhysicsFurnitureMoved(H, away, H.Yaw, mover.Id);
            var h = LastScrape(N, t); sim.RunTicks(5);
            sim.PhysicsFurnitureMoved(H, p0, H.Yaw, mover.Id); sim.RunTicks(5);
            return h?.Loud ?? 0f;
        }
        float closed = Drag(false), opened = Drag(true); D.Open = false;
        Check(opened > 0f && opened > closed, $"{Name(N.Id)} next door hears a 2m drag: closed door {closed:0.00}, open door {opened:0.00}");
        Check(Notes(N, H).All(n => n.Kind != FurnitureNoteKind.Saw), $"{Name(N.Id)} (back to the door) never saw it either way");

        // ---- 6. save / load
        Console.WriteLine("-- 6. save / load");
        var json = SaveStore.Serialize(S); var back = SaveStore.Deserialize(json); var json2 = SaveStore.Serialize(back);
        Check(json == json2, $"round trip IDENTICAL ({json.Length / 1024}KB)");
        Check(back.Layout.Furniture[F.Id].Rev == F.Rev && back.K(W.Id).FurnitureNotes.Count == S.K(W.Id).FurnitureNotes.Count && back.K(L.Id).FurnitureKnown.Count == S.K(L.Id).FurnitureKnown.Count,
            $"loaded: rev {back.Layout.Furniture[F.Id].Rev}, {Name(W.Id)} notes {back.K(W.Id).FurnitureNotes.Count}, {Name(L.Id)} memories {back.K(L.Id).FurnitureKnown.Count}");
        {
            var jo = JObject.Parse(json);
            foreach (var kv in (JObject)jo["Know"]) { var k = (JObject)kv.Value; k.Remove("FurnitureNotes"); k.Remove("FurnitureKnown"); k.Remove("RoomSeenAt"); }
            foreach (var f in (JArray)jo["Layout"]["Furniture"]) ((JObject)f).Remove("Rev");
            var old = SaveStore.Deserialize(jo.ToString(Newtonsoft.Json.Formatting.None));
            var simOld = Simulation.FromState(old); simOld.Headless = true;
            simOld.RunTicks(50);
            var fo = old.Layout.Furniture[F.Id]; int revOld = fo.Rev;
            simOld.PhysicsFurnitureMoved(fo, new P3(fo.Pos.f, fo.Pos.x + dir.x * 0.5f, fo.Pos.z + dir.z * 0.5f), fo.Yaw, null);
            Check(old.Know.Values.All(k => k.FurnitureNotes != null && k.FurnitureKnown != null && k.RoomSeenAt != null) && revOld == 0 && fo.Rev == 1 && simOld.Faults == 0,
                $"an older save without these fields loads, runs 50 ticks and takes a new move (rev {revOld}→{fo.Rev}, faults {simOld.Faults})");
        }
        {
            // the loaded copy goes on with a different move; the original is untouched by it
            var sim2 = Simulation.FromState(back); sim2.Headless = true;
            var F2 = back.Layout.Furniture[F.Id]; var W_2 = back.A(W.Id); var me2 = back.Player;
            me2.Pos = sim2.FrontOf(F2); me2.Room = F2.Room; me2.Yaw = MathX.AngleDeg(F2.Pos.x - me2.Pos.x, F2.Pos.z - me2.Pos.z);
            W_2.Pos = Around(R, F2.Pos, 3.0f); W_2.Room = R.Id; W_2.Yaw = MathX.AngleDeg(F2.Pos.x - W_2.Pos.x, F2.Pos.z - W_2.Pos.z);
            sim2.PhysicsFurnitureMoved(F2, new P3(F2.Pos.f, F2.Pos.x - dir.x * 0.7f, F2.Pos.z - dir.z * 0.7f), F2.Yaw - 30f, Cast.Player);
            Check(F2.Rev == F.Rev + 1 && F.Rev == 1 && back.K(W.Id).FurnitureNotes.Count(n => n.Furniture == F.Id) == S.K(W.Id).FurnitureNotes.Count(n => n.Furniture == F.Id) + 1,
                $"after loading, a different move makes a different future (copy rev {F2.Rev}, original rev {F.Rev})");
        }
        Check(sim.Faults == 0, $"no faults in the scripted part ({sim.Faults})");

        // ---- 7. the house's own people
        Console.WriteLine($"-- 7. a campaign, {days} days (CH12 forced from chapter 2)");
        Rules.ForceRule = "CH12";
        var sc = Simulation.NewCampaign(seed, 4); sc.Headless = true; var SC = sc.S; SC.Phase = Phase.Daily;
        double end = SC.Clock + days * 1440; long ticks = 0;
        while (SC.Clock < end && ticks < 30_000_000)
        {
            if (SC.Phase == Phase.Trial)
            {
                // as the campaign regression does: the trial, then the verdict's aftermath and the next chapter (Extra.cs)
                TrialSystem.RunHeadless(sc, true); Settlements.AfterReveal(sc);
                if (SC.Phase == Phase.LoopEpilogue) break;
                continue;
            }
            sc.Step(); ticks++;
        }
        Rules.ForceRule = null;
        Console.WriteLine($"   reached {ClockFmt.DayHM(SC.Clock)} chapter {SC.Chapter} ({ticks} ticks), rules {string.Join(",", SC.Ch.Rules.Select(r => r.Rule))}");
        var all = SC.Know.SelectMany(kv => kv.Value.FurnitureNotes.Select(n => (who: kv.Key, n))).ToList();
        var changed = SC.Layout.Furniture.Where(f => f.Rev > 0).ToList();
        Console.WriteLine($"   changes {changed.Sum(f => f.Rev)} on {changed.Count} pieces: " + string.Join(", ", SC.Ledger.Where(e => e.Type == "FurnitureHit" || e.Type == "FurnitureMoved").GroupBy(e => e.Type + ":" + (e.Data?.Split(':').LastOrDefault() ?? "")).Select(g => g.Key + "=" + g.Count())));
        Console.WriteLine($"   notes: did {all.Count(x => x.n.Kind == FurnitureNoteKind.Did)}, saw {all.Count(x => x.n.Kind == FurnitureNoteKind.Saw)} (named {all.Count(x => x.n.Kind == FurnitureNoteKind.Saw && x.n.Who != null)}), noticed later {all.Count(x => x.n.Kind == FurnitureNoteKind.Noticed)}");
        foreach (var x in all.Where(x => x.n.Kind != FurnitureNoteKind.Did).OrderBy(x => x.n.Clock).Take(8))
            Console.WriteLine($"     {ClockFmt.DayHM(x.n.Clock)} {Name(x.who)} {x.n.Kind} {FurnitureCatalog.Get(SC.Layout.Furniture[x.n.Furniture].Type)?.Kor} {x.n.How} in {SC.RoomName(x.n.Room)}{(x.n.Who != null ? " — by " + Name(x.n.Who) : "")}{(x.n.Since >= 0 ? " — since " + ClockFmt.DayHM(x.n.Since) : "")}");
        Check(all.All(x => x.n.Kind != FurnitureNoteKind.Noticed || x.n.Who == null), "a later notice never carries a name");
        Check(all.All(x => x.n.Who == null || (SC.A(x.n.Who) != null && (x.n.Kind == FurnitureNoteKind.Did || x.n.WhoConf >= 0.3f))), "a name in a sighting is always someone they could make out");
        Check(changed.Count > 0 && all.Count > 0, "the house's own struggles, traps and rules reach people's knowledge");
        Check(sc.Faults == 0, $"campaign faults {sc.Faults}");
        var cj = SaveStore.Serialize(SC); Check(cj == SaveStore.Serialize(SaveStore.Deserialize(cj)), "campaign save round trip IDENTICAL");

        Console.WriteLine($"furnknow: ok={ok} fail={fail}");
        return fail == 0 ? 0 : 1;
    }
}
