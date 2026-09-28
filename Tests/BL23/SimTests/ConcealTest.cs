using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;

public static partial class Program
{
    /// <summary>
    /// Concealment and stashing (Sim/Systems/Concealment.cs). Usage: conceal [seed] [days]
    ///  1. body: a knife goes inside 민혁's jacket, a key into a pocket, a third medium thing into the cross-bag; a candlestick
    ///     and a bust cannot be hidden (greyed reason); drawing takes it back into a free hand.
    ///  2. a witness: 진우 two steps away sees a bloody knife tucked away → a sighting (held knife, blood), suspicion, a memory,
    ///     a stain on 민혁's jacket; and the other way round: 민혁 sees 진우 hide a knife → a notice and one card.
    ///  3. stash: under a mattress (hidden, remembered, retrieved), in a nightstand drawer (opening it does not "discover" his own
    ///     stash), too big for a drawer (reason).
    ///  4. an NPC search: an observant investigator goes through the wardrobe with a bloody knife in it and finds it (revealed,
    ///     ItemFound, their own card, a notice to the player nearby).
    ///  5. save round trip with things concealed and stashed.
    ///  6. a campaign (default 777 × 3 days): what the kernel's own people did with the same acts (tucks, stashes, searches).
    /// </summary>
    static int ConcealTest(string[] args)
    {
        ulong seed = args.Length > 1 && ulong.TryParse(args[1], out var sd) ? sd : 20260926UL;
        int days = args.Length > 2 && int.TryParse(args[2], out var dd) ? dd : 3;
        int ok = 0, fail = 0;
        void Check(bool cond, string what) { if (cond) ok++; else fail++; Console.WriteLine((cond ? "  ok   " : "  FAIL ") + what); }

        var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; sim.EndPrologue();
        sim.RunTicks(600);
        var me = S.Player;
        // stand 민혁 in his own room, in daylight, with nobody looking (people go about their day elsewhere)
        var bed = S.Layout.BedroomOf(Cast.Player);
        var bedF = bed.Furniture.Select(i => S.Layout.Furniture[i]).First(f => f.Type == "Bed");
        foreach (var x in S.LivingNpcs.ToList()) { x.Act = null; x.NextThink = S.Clock + 9999; if (x.Room == bed.Id) { x.Pos = sim.SnapPublic(new P3(0, S.Layout.Rooms.First(r => r.Type == RoomType.GrandHall && r.Floor == 0).Rect.CX, S.Layout.Rooms.First(r => r.Type == RoomType.GrandHall && r.Floor == 0).Rect.CZ)); x.Room = S.Layout.RoomAt(x.Pos); } }
        me.Pos = sim.FrontOf(bedF); me.Room = bedF.Room; me.Yaw = MathX.AngleDeg(bedF.Pos.x - me.Pos.x, bedF.Pos.z - me.Pos.z);
        Item Spawn(string type, bool bloody = false)
        {
            var it = new Item { Id = S.NewId("it"), Type = type, Pos = me.Pos, Room = me.Room, HomeRoom = me.Room, HomePos = me.Pos, Bloody = bloody };
            if (bloody) it.Surface.Add("blood");
            S.Items[it.Id] = it; return it;
        }
        void Hold(Item it) { it.Pos = me.Pos; it.Room = me.Room; it.Holder = null; if (!sim.PickUp(me, it)) Console.WriteLine("   (pickup failed " + it.Type + ")"); }
        void EmptyHands() { foreach (var id in new[] { me.HandR, me.HandL }) { var x = S.I(id); if (x != null) sim.DropItem(me, x, me.Pos); } }

        { // a new "it" id never lands on a loop-start item (it used to overwrite one — a key in a pocket turned into the new thing)
            var probe = Simulation.NewCampaign(seed, 4); int n0 = probe.S.Items.Count; var ids = new HashSet<string>();
            for (int i = 0; i < 400; i++) { var x = new Item { Id = probe.S.NewId("it"), Type = "Candy" }; ids.Add(x.Id); probe.S.Items[x.Id] = x; }
            Check(probe.S.Items.Count == n0 + 400 && ids.Count == 400, $"new item ids never overwrite loop-start items ({n0} + 400 = {probe.S.Items.Count})");
        }
        Console.WriteLine($"== conceal seed {seed}: 민혁 in {S.RoomName(me.Room)} (light {sim.RoomLight(me.Room):0.00}); outfit coat={Concealment.OutfitOf(Cast.Player).Coat} bag={Concealment.OutfitOf(Cast.Player).Bag}");
        // ---- 1. on the body
        Console.WriteLine("-- 1. on the body");
        var knife = Spawn("KitchenKnife"); Hold(knife);
        var pr = sim.ConcealPrompt(knife); Check(pr.can && pr.label == "품에 숨기기", $"prompt for a knife: \"Q {pr.label}\"");
        var r1 = sim.PlayerConceal(knife);
        Check(r1.Ok && r1.Slot == BodySlot.Coat && me.HandR != knife.Id && me.HandL != knife.Id && me.Pocket.Contains(knife.Id) && knife.Worn == "coat", $"knife → {Concealment.SlotWord(Cast.Player, r1.Slot)}: \"{r1.Text}\" ({r1.Seconds:0.0}s)");
        Check(sim.Held(me) == null, "nothing shows in the hands");
        var key = me.Pocket.Select(S.I).FirstOrDefault(i => i?.KeyFor != null);
        Check(key != null && Concealment.SlotOf(S, key) == BodySlot.Pocket, $"own room key is in a pocket ({key?.Kor})");
        var pick = Spawn("IcePick"); Hold(pick); var r2 = sim.PlayerConceal(pick);
        Check(r2.Ok && r2.Slot == BodySlot.Coat, $"ice pick → {Concealment.SlotWord(Cast.Player, r2.Slot)} (coat {Concealment.Count(S, me, BodySlot.Coat)}/{Concealment.Capacity(Cast.Player, BodySlot.Coat)})");
        var chisel = Spawn("Chisel"); Hold(chisel); var r3 = sim.PlayerConceal(chisel);
        Check(r3.Ok && r3.Slot == BodySlot.Bag, $"a third medium thing (chisel) goes to the {Concealment.SlotWord(Cast.Player, r3.Slot)}: \"{r3.Text}\"");
        var candle = Spawn("Candlestick"); Hold(candle); var pc = sim.ConcealPrompt(candle); var r4 = sim.PlayerConceal(candle);
        Check(!pc.can && !r4.Ok && sim.Held(me) == candle, $"candlestick cannot be hidden: \"{pc.label}\" / \"{r4.Why}\"");
        EmptyHands();
        var bust = Spawn("Statuette"); Hold(bust); var r5 = sim.PlayerConceal(bust);
        Check(!r5.Ok, $"bronze bust cannot be hidden: \"{r5.Why}\""); EmptyHands();
        var poker = Spawn("FirePoker"); Hold(poker); var r6 = sim.PlayerConceal(poker);
        Check(!r6.Ok && r6.Why.Contains("길어서"), $"poker: \"{r6.Why}\""); EmptyHands();
        var letter = Spawn("LetterOpener"); Hold(letter); var r7 = sim.PlayerConceal(letter);
        Check(r7.Ok && r7.Slot == BodySlot.Pocket, $"letter opener → {Concealment.SlotWord(Cast.Player, r7.Slot)}");
        var rd = sim.PlayerDraw(knife);
        Check(rd.Ok && me.HandR == knife.Id && !me.Pocket.Contains(knife.Id) && knife.Worn == null, $"draw: \"{rd.Text}\"");
        var rc = sim.PlayerConceal(knife); Check(rc.Ok && rc.Slot == BodySlot.Coat, "back into the jacket");
        // a costume that has no coat: 해린 (tank top, tool belt) can hang a chisel on her belt but not hide a kitchen knife inside clothes
        var hae = S.A("P11");
        { var k2 = new Item { Id = S.NewId("it"), Type = "KitchenKnife", Pos = hae.Pos, Room = hae.Room }; S.Items[k2.Id] = k2; sim.PickUp(hae, k2); var s2 = Concealment.BestSlot(S, hae, k2, out var why2);
          Check(s2 == BodySlot.Bag, $"해린 (no coat): a knife goes on the {Concealment.SlotWord("P11", s2)} {(why2 ?? "")}"); sim.DropItem(hae, k2, hae.Pos); }
        { var k3 = new Item { Id = S.NewId("it"), Type = "KitchenKnife", Pos = S.A("P14").Pos, Room = S.A("P14").Room }; S.Items[k3.Id] = k3; var eun = S.A("P14"); sim.PickUp(eun, k3); var s3 = Concealment.BestSlot(S, eun, k3, out var why3);
          Check(s3 == BodySlot.None && why3 != null, $"은결 (mourning dress, no coat, no bag): a knife can't be hidden — \"{why3}\""); sim.DropItem(eun, k3, eun.Pos); }

        // ---- 2. witnesses
        Console.WriteLine("-- 2. seen doing it");
        var jin = S.A("P02");
        var dark = S.Layout.Rooms.Where(r => !RoomInfo.IsPassage(r.Type) && r.Id != me.Room && r.Floor == me.Pos.f).OrderBy(r => sim.RoomLight(r.Id)).ThenBy(r => r.Id).First();
        {
            // 진우 two steps in front of 민혁, looking at him
            var at = sim.SnapPublic(new P3(me.Pos.f, me.Pos.x + (float)Math.Sin(me.Yaw * Math.PI / 180) * 1.8f, me.Pos.z + (float)Math.Cos(me.Yaw * Math.PI / 180) * 1.8f));
            if (S.Layout.RoomAt(at) != me.Room) at = sim.SnapPublic(new P3(me.Pos.f, me.Pos.x + 1.2f, me.Pos.z));
            jin.Pos = at; jin.Room = S.Layout.RoomAt(at); jin.Yaw = MathX.AngleDeg(me.Pos.x - jin.Pos.x, me.Pos.z - jin.Pos.z); jin.Pose = Pose.Stand; jin.Act = null; jin.TalkingTo = null;
            me.Yaw = MathX.AngleDeg(jin.Pos.x - me.Pos.x, jin.Pos.z - me.Pos.z);
            Console.WriteLine($"   진우 at {jin.Pos.DistXZ(me.Pos):0.0} m, room {S.RoomName(jin.Room)}");
            EmptyHands();
            foreach (var x in new[] { pick, chisel }) { if (sim.PlayerDraw(x).Ok) EmptyHands(); }   // room inside the jacket again
            var bloody = Spawn("SkinningKnife", true); Hold(bloody);
            float sus0 = S.K("P02").Suspicion.TryGetValue(Cast.Player, out var sv) ? sv : 0; int sight0 = S.K("P02").Sightings.Count; float blood0 = me.BloodOnClothes;
            var rw = sim.PlayerConceal(bloody);
            Check(rw.Ok && rw.Seen.Contains("P02") && rw.SeenBy == "P02", $"진우 saw it: \"{rw.Text}\" (seen by {string.Join(",", rw.Seen)})");
            var s = S.K("P02").Sightings.Skip(sight0).LastOrDefault(x => x.Target == Cast.Player);
            Check(s != null && s.Held == "SkinningKnife" && s.Bloody, $"진우's sighting: held {s?.Held} bloody={s?.Bloody}");
            float sus1 = S.K("P02").Suspicion.TryGetValue(Cast.Player, out var sv1) ? sv1 : 0;
            Check(sus1 > sus0, $"진우's suspicion of 민혁 {sus0:0.00} → {sus1:0.00}");
            var mem = S.R("P02", Cast.Player).Memory.LastOrDefault();
            Check(mem != null && mem.Contains("넣는 걸 봤다"), $"진우 remembers: \"{mem}\"");
            Check(me.BloodOnClothes >= 0.3f && blood0 < 0.3f && rw.Stained, $"the jacket lining is stained ({blood0:0.00} → {me.BloodOnClothes:0.00}): \"{rw.Text}\"");
            var bark = S.Ledger.LastOrDefault(e => e.Type == "Speech" && e.Actor == "P02");
            Check(bark != null && bark.Data != null && bark.Data.StartsWith("conceal_notice"), $"진우 says: \"{bark?.Data?.Split('|').LastOrDefault()}\"");
            me.BloodOnClothes = 0;
            // the other way round: 진우 hides a knife while 민혁 watches
            me.Yaw = MathX.AngleDeg(jin.Pos.x - me.Pos.x, jin.Pos.z - me.Pos.z);
            var jk = new Item { Id = S.NewId("it"), Type = "KitchenKnife", Pos = jin.Pos, Room = jin.Room }; S.Items[jk.Id] = jk; sim.PickUp(jin, jk);
            int ev0 = S.K(Cast.Player).Evidence.Count; S.Out.Clear();
            var rj = Concealment.Conceal(sim, jin, jk);
            var note = S.Out.FirstOrDefault(e => e.Type == GameEventType.Notice && e.Key == "conceal");
            Check(rj.Ok && rj.Seen.Contains(Cast.Player) && note != null, $"민혁 sees 진우: \"{note?.Text}\"");
            var card = S.K(Cast.Player).Evidence.Skip(ev0).FirstOrDefault(e => e.Root == "conceal:P02:" + jk.Id);
            Check(card != null && card.Props.Any(p => p.Kind == PropKind.Held && p.A == "P02" && p.Item == "KitchenKnife"), $"one card: \"{card?.Title}\" — {card?.Line}");
            var rj2 = Concealment.Draw(sim, jin, jk); Concealment.Conceal(sim, jin, jk);
            Check(S.K(Cast.Player).Evidence.Count(e => e.Root == "conceal:P02:" + jk.Id) == 1, "seeing it again does not add a second card");
            var anim = S.Out.LastOrDefault(e => e.Type == GameEventType.Anim && e.Actor == "P02");
            Check(anim != null && (anim.Key == "tuck" || anim.Key == "draw") && jin.BusyUntil > S.Tick, $"진우's hands are busy for the motion ({anim?.Key}, {anim?.Value:0.0}s)");
            // far away and in the dark: unseen
            jin.Pos = sim.SnapPublic(new P3(dark.Floor, dark.Rect.CX, dark.Rect.CZ)); jin.Room = S.Layout.RoomAt(jin.Pos);
            EmptyHands(); var hid = Spawn("Scalpel"); Hold(hid); var ru = sim.PlayerConceal(hid);
            Check(ru.Ok && !ru.Seen.Contains("P02"), $"from another room nobody sees ({(ru.Seen.Count == 0 ? "unseen" : string.Join(",", ru.Seen))})");
        }

        // ---- 3. stashing
        Console.WriteLine("-- 3. stashing");
        foreach (var x in S.LivingNpcs.Where(x => x.Room == me.Room).ToList()) { x.Pos = sim.SnapPublic(new P3(dark.Floor, dark.Rect.CX, dark.Rect.CZ)); x.Room = S.Layout.RoomAt(x.Pos); }
        me.Pos = sim.FrontOf(bedF); me.Room = bedF.Room;
        EmptyHands(); sim.PlayerDraw(knife);
        var sp = sim.StashPrompt(bedF, knife); Check(sp.can && sp.label.Contains("매트리스 밑"), $"prompt at the bed: \"E {sp.label}\"");
        var st = sim.PlayerStash(knife, bedF);
        Check(st.Ok && knife.Hidden && knife.Holder == null && knife.StashF == bedF.Id && sim.Held(me) == null, $"stash: \"{st.Text}\" ({st.Seconds:0.0}s)");
        var rem = Concealment.Remembered(S);
        Check(rem.Any(x => x.it == knife && x.f == bedF), $"remembered: {string.Join(", ", rem.Select(x => x.it.Kor + " — " + Concealment.PlaceName(S, x.f)))}");
        Check(Concealment.StashedIn(S, bedF).Contains(knife) && sim.PlayerStashesAt(bedF).Contains(knife), "the bed keeps it");
        var hideLog = S.Ledger.LastOrDefault(e => e.Type == "HideItem" && e.Item == knife.Id);
        Check(hideLog != null && hideLog.Data == "KitchenKnife", "ledger HideItem (the reveal and the trial read it)");
        var back = sim.PlayerRetrieve(knife);
        Check(back.Ok && knife.Holder == Cast.Player && knife.StashF < 0 && !knife.Hidden && !Concealment.Remembered(S).Any(x => x.it == knife), $"retrieve: \"{back.Text}\"");
        // a drawer: the container shows it when opened; opening it does not "discover" what 민혁 put there himself
        var drawer = S.Layout.Furniture.Where(f => f.Type == "Nightstand" && f.Pos.f == me.Pos.f).OrderBy(f => f.Room == me.Room ? 0 : 1).ThenBy(f => f.Id).FirstOrDefault();
        if (drawer != null)
        {
            me.Pos = sim.FrontOf(drawer); me.Room = drawer.Room;
            var vial = Spawn("PoisonVial"); Hold(vial);   // (small: it goes to the pocket at pick-up)
            var sd2 = sim.PlayerStash(vial, drawer);
            Check(sd2.Ok && sim.ContainedItems(drawer).Contains(vial), $"nightstand: \"{sd2.Text}\" — the drawer holds it");
            var open = sim.PlayerOpen(drawer, null, true);
            Check(vial.Hidden && !open.Revealed.Contains(vial), "opening the drawer shows it but does not count as finding it");
            var pin = Spawn("RollingPin"); var tooBig = sim.StashPrompt(drawer, pin);
            Check(!tooBig.can && tooBig.label.Contains("크다"), $"a rolling pin and the drawer: \"{tooBig.label}\""); S.Items.Remove(pin.Id);
            var takeVial = sim.PlayerRetrieve(vial); Check(takeVial.Ok && me.Pocket.Contains(vial.Id), $"small things come back to the pocket: \"{takeVial.Text}\"");
        }
        else Console.WriteLine("   (no nightstand on this floor)");
        var planter = S.Layout.Furniture.Where(f => f.Type == "Planter").OrderBy(f => f.Pos.f == me.Pos.f ? 0 : 1).ThenBy(f => f.Id).FirstOrDefault();
        if (planter != null)
        {
            me.Pos = sim.FrontOf(planter); me.Room = planter.Room;
            var ps = sim.PlayerStash(knife, planter);
            Check(ps.Ok && knife.Surface.Contains("흙"), $"planter: \"{ps.Text}\" (soil on it)");
            var own = sim.PlayerSearchStash(planter);
            Check(own.Found.Count == 0 && knife.Hidden && knife.StashF == planter.Id, "a look at his own hiding place leaves his stash put away");
            // someone else buries a vial there: 민혁's close look turns it up
            var other = S.LivingNpcs.First(x => x.Id != "P02"); var vial2 = new Item { Id = S.NewId("it"), Type = "Sedative", Pos = planter.Pos, Room = planter.Room };
            S.Items[vial2.Id] = vial2; other.Pos = sim.FrontOf(planter); other.Room = planter.Room; sim.PickUp(other, vial2); Concealment.Stash(sim, other, vial2, planter);
            other.Pos = sim.SnapPublic(new P3(dark.Floor, dark.Rect.CX, dark.Rect.CZ)); other.Room = S.Layout.RoomAt(other.Pos);
            var look = sim.PlayerSearchStash(planter);
            Check(look.Ok && look.Found.Contains(vial2) && !look.Found.Contains(knife) && !vial2.Hidden && vial2.StashF < 0, $"a close look turns up what {Cast.GivenOf(other.Id)} buried: \"{look.Text}\"");
            var back3 = sim.PlayerRetrieve(knife); Check(back3.Ok, $"and his own comes back out: \"{back3.Text}\"");
        }

        // ---- 4. an investigator's search
        Console.WriteLine("-- 4. an NPC search");
        var ward = S.Layout.Furniture.Where(f => f.Type == "Wardrobe" && S.Layout.Room(f.Room)?.Owner != null && S.Layout.Room(f.Room).Owner != "P02" && f.Pos.f == 0).OrderBy(f => f.Id).FirstOrDefault()
                   ?? S.Layout.Furniture.First(f => f.Type == "Wardrobe");
        var owner = S.Layout.Room(ward.Room)?.Owner;
        var evid = Spawn("KitchenKnife", true); evid.Pos = ward.Pos; evid.Room = ward.Room;
        var ownerA = S.A(owner) ?? S.A("P05");
        { evid.Holder = null; ownerA.Pos = sim.FrontOf(ward); ownerA.Room = ward.Room; sim.PickUp(ownerA, evid); var so = Concealment.Stash(sim, ownerA, evid, ward); Check(so.Ok && evid.StashF == ward.Id, $"{Cast.GivenOf(ownerA.Id)} puts a bloody knife in the {Concealment.PlaceName(S, ward)}"); }
        ownerA.Pos = sim.SnapPublic(new P3(dark.Floor, dark.Rect.CX, dark.Rect.CZ)); ownerA.Room = S.Layout.RoomAt(ownerA.Pos);
        jin.Pos = sim.SnapPublic(new P3(ward.Pos.f, S.Layout.Room(ward.Room).Rect.CX, S.Layout.Room(ward.Room).Rect.CZ)); jin.Room = S.Layout.RoomAt(jin.Pos); jin.NextThink = S.Clock + 9999;
        me.Pos = sim.SnapPublic(new P3(ward.Pos.f, S.Layout.Room(ward.Room).Rect.CX + 0.8f, S.Layout.Room(ward.Room).Rect.CZ)); me.Room = S.Layout.RoomAt(me.Pos);
        var act = new Activity { Id = "inv:csearch:test", Label = "옷장 뒤지는 중", Priority = 10 };
        act.Steps.Add(Simulation.GoTo(sim.FrontOf(ward))); act.Steps.Add(new ActionStep { Kind = "C_Search", Furniture = ward.Id, Duration = 1.2 });
        sim.Assign(jin, act); S.Out.Clear();
        for (int i = 0; i < 3000 && jin.Act != null; i++) { sim.Step(); if (jin.Act == null) break; }
        var found = S.Ledger.LastOrDefault(e => e.Type == "ItemFound" && e.Item == evid.Id);
        Check(found != null && found.Actor == "P02" && !evid.Hidden && evid.StashF < 0, $"진우 found it ({(found != null ? ClockFmt.HM(found.Clock) : "-")}); it lies in plain view now");
        Check(S.K("P02").Evidence.Any(e => e.Root != null && e.Root.StartsWith("item:" + evid.Id)), "진우 has his own card about it");
        var fn = S.Out.FirstOrDefault(e => e.Type == GameEventType.Notice && e.Key == "stash_found");
        Check(fn != null, $"the player nearby hears of it: \"{fn?.Text}\"");
        var said = S.Ledger.LastOrDefault(e => e.Type == "Speech" && e.Actor == "P02" && e.Data != null && e.Data.StartsWith("stash_found"));
        Check(said != null, $"진우: \"{said?.Data?.Split('|').LastOrDefault()}\"");
        if (owner != null && owner != "P02") Check(S.K("P02").Suspicion.TryGetValue(owner, out var so2) && so2 > 0.1f, $"진우 now suspects the room's owner {Cast.GivenOf(owner)} ({so2:0.00})");

        // ---- 5. save round trip
        Console.WriteLine("-- 5. save");
        me.Pos = sim.FrontOf(bedF); me.Room = bedF.Room; EmptyHands();
        { if (knife.Holder != Cast.Player) { knife.Holder = null; Hold(knife); } var k5 = sim.PlayerConceal(knife); var c2 = Spawn("Rope"); Hold(c2); var s5 = sim.PlayerStash(c2, bedF);
          Console.WriteLine($"   knife: {k5.Text ?? k5.Why} ({knife.Worn}); rope: {s5.Text ?? s5.Why}"); }
        var json = SaveStore.Serialize(S); var back2 = SaveStore.Deserialize(json); var json2 = SaveStore.Serialize(back2);
        Check(json == json2, "round trip " + (json == json2 ? "IDENTICAL" : "DIFF"));
        Check(back2.I(knife.Id).Worn == "coat" && back2.Items.Values.Any(i => i.StashF == bedF.Id && i.Hidden), "worn and stashed survive the save");

        // ---- 6. a campaign: the people of the house use the same acts
        ulong cseed = args.Length > 3 && ulong.TryParse(args[3], out var cs) ? cs : 777UL;
        Console.WriteLine($"-- 6. campaign {cseed} × {days} days");
        var sim2 = Simulation.NewCampaign(cseed, 4); sim2.Headless = true; var S2 = sim2.S; S2.Phase = Phase.Daily;
        long ticks = 0; int trials = 0;
        while (S2.Clock < days * 1440 && ticks < 5_000_000)
        {
            if (S2.Phase == Phase.Trial) { trials++; TrialSystem.RunHeadless(sim2, true); Replay.BuildSegments(sim2); Settlements.AfterReveal(sim2); if (S2.Phase == Phase.LoopEpilogue) break; continue; }
            sim2.Step(); ticks++; if (S2.Out.Count > 2000) S2.Out.Clear();
        }
        string[] kinds = { "Conceal", "Draw", "Stash", "Retrieve", "ConcealStain", "SearchStart", "SearchPlace", "HideItem" };
        Console.WriteLine("   ledger: " + string.Join(" ", kinds.Select(k => k + "=" + S2.Ledger.Count(e => e.Type == k))) + $" found-in-stash={S2.Ledger.Count(e => e.Type == "ItemFound" && e.Data != null && e.Data.StartsWith("stash:"))} trials={trials} faults={sim2.Faults}");
        foreach (var e in S2.Ledger.Where(e => e.Type == "Conceal" || e.Type == "Stash" || e.Type == "ConcealStain" || e.Type == "ItemFound" && e.Data != null && e.Data.StartsWith("stash:")).Take(14))
            Console.WriteLine($"     {ClockFmt.DayHM(e.Clock)} {e.Type} {Cast.GivenOf(e.Actor)} {S2.I(e.Item)?.Kor} {S2.RoomName(e.Room)} {e.Data}");
        foreach (var e in S2.Ledger.Where(e => e.Type == "HideItem").Take(8))
        { var it2 = S2.I(e.Item); var pf = it2 != null && it2.StashF >= 0 ? S2.Layout.Furniture[it2.StashF] : null; Console.WriteLine($"     {ClockFmt.DayHM(e.Clock)} HideItem {Cast.GivenOf(e.Actor)} {it2?.Kor} {S2.RoomName(e.Room)} → {(pf != null ? Concealment.PlaceName(S2, pf) : it2?.Hidden == true ? "(loose, hidden)" : "(found / moved)")}"); }
        foreach (var pl in S2.Plans.Values.Where(p => p.Steps.Any(s => s.Kind == "HideWeapon" || s.Kind == "WashWeapon")).Take(4)) Console.WriteLine($"     plan {pl.Actor} {pl.Grammar} {pl.Stage}: {string.Join(",", pl.Steps.Select(s => s.Kind + (s.Done ? "*" : "")))}");
        var sawCards = S2.K(Cast.Player).Evidence.Count(e => e.Root != null && e.Root.StartsWith("conceal:"));
        int sightings = S2.Know.Values.Sum(k => k.Sightings.Count(s => s.Root != null && s.Root.StartsWith("conceal:")));
        Console.WriteLine($"   witnessed: NPC sightings {sightings}, player cards {sawCards}");
        Check(sim2.Faults == 0, "campaign faults=0");
        var j1 = SaveStore.Serialize(S2); var j2 = SaveStore.Serialize(SaveStore.Deserialize(j1));
        Check(j1 == j2, "campaign round trip " + (j1 == j2 ? "IDENTICAL" : "DIFF"));

        Console.WriteLine($"conceal ok={ok} fail={fail} faults={sim.Faults}");
        return fail == 0 && sim.Faults == 0 ? 0 : 1;
    }
}
