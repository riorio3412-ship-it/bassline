using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>Execution of the second-wave methods: plan steps → activities → contact → traces. See Methods.cs.</summary>
    public static partial class Methods
    {
        public static bool Handles(string kind) => kind == "X_Sedate" || kind == "X_WaitDrowsy" || kind == "X_PlantPoison" || kind == "X_WaitBedtime" || kind == "X_KeySlide"
            || kind == "X_FakeNote" || kind == "X_Burn" || kind == "X_DumpWater" || kind == "X_Bury" || kind == "X_Noise" || DismemberKind(kind)
            || Violence.Handles(kind);   // --- violence track: X_Load (a gun), X_Bind / X_Unbind (a sleeper tied before the kill)
        /// <summary>Plan steps that only wait for the world (the watchdog must not call them "stuck").</summary>
        public static bool Waiting(string kind) => kind == "X_Sedate" || kind == "X_WaitDrowsy" || kind == "X_PlantPoison" || kind == "X_WaitBedtime" || kind == "X_Dismember";

        static P3 InsideOf(Door d, Room room, float off = 0.8f) => d.AlongX ? new P3(d.Pos.f, d.Pos.x, d.Pos.z + (room.Rect.CZ > d.Pos.z ? off : -off)) : new P3(d.Pos.f, d.Pos.x + (room.Rect.CX > d.Pos.x ? off : -off), d.Pos.z);

        // ================================================================== plan step → activity
        public static Activity PlanThink(Simulation sim, Actor a, MurderPlan plan, PlanStep st, Activity act)
        {
            if (Violence.Handles(st.Kind)) return Violence.PlanThink(sim, a, plan, st, act);   // --- violence track
            var S = sim.S; var rng = S.R(Stream.PlanTie); var t = S.A(plan.Target);
            switch (st.Kind)
            {
                case "X_Sedate": return SedateThink(sim, a, plan, st, act);
                case "X_WaitDrowsy":
                    {
                        if (t == null || !t.Alive) { Crime.Abort(sim, a, plan, "target gone"); return null; }
                        if (Dozing(t)) { Crime.Advance(sim, a, plan); return null; }
                        bool pending = S.Flags.ContainsKey("sedate:" + t.Id) || S.Flags.ContainsKey("drowsy:" + t.Id);
                        if (!pending) { Crime.Abort(sim, a, plan, "약이 듣지 않아서"); return null; }
                        return null;   // life goes on at the table; the draught does the waiting
                    }
                case "X_PlantPoison":
                    {
                        if (t == null || !t.Alive) { Crime.Abort(sim, a, plan, "target gone"); return null; }
                        var bed = S.Layout.Room(st.Room); if (bed == null || bed.Doors.Count == 0) { Crime.Abort(sim, a, plan, "no room"); return null; }
                        // only by day, only while the owner is out, only through an unlocked door
                        if (S.IsNight || S.Minute < 8 * 60 || S.Minute > 20 * 60 + 30 || t.Room == bed.Id) return null;
                        var door = S.Layout.Doors[bed.Doors[0]]; if (door.Locked) return null;
                        if (S.Actors.Values.Any(x => x != a && x.Alive && x.Room == bed.Id)) return null;
                        var desk = bed.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(f => f.Type == "Desk" || f.Type == "Nightstand");
                        var p = desk != null ? sim.SnapPublic(new P3(desk.Pos.f, desk.Pos.x + (bed.Rect.CX - desk.Pos.x) * 0.3f, desk.Pos.z + (bed.Rect.CZ - desk.Pos.z) * 0.3f)) : sim.RandomPointIn(bed, rng);
                        act.Steps.Add(Simulation.GoTo(sim.SnapPublic(InsideOf(door, bed))));
                        act.Steps.Add(Simulation.GoTo(p)); act.Steps.Add(new ActionStep { Kind = "X_MPlant", Actor = t.Id, Item = st.Item, Data = st.Note, Room = bed.Id, Duration = 2 });
                        act.Label = "볼일"; act.Interruptible = false; return act;
                    }
                case "X_WaitBedtime":
                    {
                        var trap = S.Traps.FirstOrDefault(x => x.Plan == plan.Id && x.Kind == "Bedtime");
                        if (trap == null) { Crime.Abort(sim, a, plan, "준비해 둔 독이 사라져서"); return null; }
                        if (!trap.Active && trap.FiredAt >= 0) { Crime.Advance(sim, a, plan); return null; }
                        if (!trap.Active) { Crime.Abort(sim, a, plan, "누가 독을 치워 버려서"); return null; }
                        return null;   // live normally — with people around, far from that room
                    }
                case "X_KeySlide":
                    {
                        var bed = t != null ? S.Layout.BedroomOf(t.Id) : null;
                        var key = t?.Pocket.Select(S.I).FirstOrDefault(i => i?.KeyFor != null && i.Owner == t.Id);
                        if (t == null || bed == null || t.Room != bed.Id || key == null || bed.Doors.Count == 0) { Crime.Advance(sim, a, plan); return null; }
                        var d = S.Layout.Doors[bed.Doors[0]];
                        act.Steps.Add(Simulation.GoTo(sim.SnapPublic(InsideOf(d, bed))));
                        act.Steps.Add(new ActionStep { Kind = "X_MKeySlide", Actor = t.Id, Item = key.Id, Door = d.Id, Duration = 3 });
                        act.Interruptible = false; return act;
                    }
                case "X_FakeNote":
                    {
                        if (t == null || t.Status == ActorStatus.Active) { Crime.Advance(sim, a, plan); return null; }
                        act.Steps.Add(Simulation.GoTo(sim.SnapPublic(new P3(t.Pos.f, t.Pos.x + 0.7f, t.Pos.z + 0.2f))));
                        act.Steps.Add(new ActionStep { Kind = "X_MNote", Actor = t.Id, Duration = 2 });
                        act.Interruptible = false; return act;
                    }
                case "X_Burn":
                    {
                        var room = KnownRoom(sim, a, RoomType.Incinerator);
                        var f = room?.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(x => x.Type == "Incinerator");
                        if (room == null || f == null || (sim.Carried(a).All(i => i.Id != plan.Weapon) && a.BloodOnClothes < 0.2f)) { Crime.Advance(sim, a, plan); return null; }
                        var sp = room.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Furniture == f.Id);
                        act.Steps.Add(Simulation.GoTo(sp != null ? sp.Approach : sim.RandomPointIn(room, rng)));
                        act.Steps.Add(new ActionStep { Kind = "X_MBurn", Furniture = f.Id, Duration = 3 });
                        act.Label = "볼일"; act.Interruptible = false; return act;
                    }
                case "X_DumpWater":
                    {
                        var room = KnownRoom(sim, a, RoomType.Pool); var w = S.I(plan.Weapon);
                        if (room == null || w == null || w.Holder != a.Id || (S.IsNight && RoomInfo.NightLocked(room.Type))) { st.Kind = "HideWeapon"; return null; }
                        var water = room.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(x => x.Type == "PoolWater");
                        var edge = water != null ? sim.SnapPublic(new P3(water.Pos.f, water.Pos.x + water.W / 2 + 0.6f, water.Pos.z)) : sim.RandomPointIn(room, rng);
                        act.Steps.Add(Simulation.GoTo(edge)); act.Steps.Add(new ActionStep { Kind = "X_MDump", Item = w.Id, Room = room.Id, Duration = 1 });
                        act.Label = "볼일"; act.Interruptible = false; return act;
                    }
                case "X_Bury":
                    {
                        var room = KnownRoom(sim, a, RoomType.Greenhouse); var w = S.I(plan.Weapon);
                        var pl = room?.Furniture.Select(i => S.Layout.Furniture[i]).Where(x => x.Type == "Planter").OrderBy(x => x.Id).FirstOrDefault();
                        if (room == null || pl == null || w == null || w.Holder != a.Id) { st.Kind = "HideWeapon"; return null; }
                        act.Steps.Add(Simulation.GoTo(sim.SnapPublic(new P3(pl.Pos.f, pl.Pos.x + (room.Rect.CX > pl.Pos.x ? 1.1f : -1.1f), pl.Pos.z))));
                        act.Steps.Add(new ActionStep { Kind = "X_MBury", Item = w.Id, Furniture = pl.Id, Duration = 3 });
                        act.Label = "볼일"; act.Interruptible = false; return act;
                    }
                case "X_Noise":
                    {
                        var room = S.Layout.Room(st.Room);
                        var f = room?.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(x => x.Type == "Boiler" || x.Type == "PressConsole" || x.Type == "Pipes" || x.Type == "Generator");
                        if (room == null) { Crime.Advance(sim, a, plan); return null; }
                        var p = f != null ? sim.SnapPublic(new P3(f.Pos.f, f.Pos.x + (room.Rect.CX - f.Pos.x) * 0.25f, f.Pos.z + (room.Rect.CZ - f.Pos.z) * 0.25f)) : sim.RandomPointIn(room, rng);
                        act.Steps.Add(Simulation.GoTo(p)); act.Steps.Add(new ActionStep { Kind = "X_MNoise", Furniture = f?.Id ?? -1, Room = room.Id, Duration = 1 });
                        act.Interruptible = false; return act;
                    }
            }
            if (DismemberKind(st.Kind)) return DismemberThink(sim, a, plan, st, act);
            Crime.Advance(sim, a, plan); return null;
        }

        // ================================================================== sleeping draught (수면제)
        static Activity SedateThink(Simulation sim, Actor a, MurderPlan plan, PlanStep st, Activity act)
        {
            var S = sim.S; var t = S.A(plan.Target);
            if (t == null || !t.Alive) { Crime.Abort(sim, a, plan, "target gone"); return null; }
            var sed = S.I(st.Item); if (sed == null || sed.Holder != a.Id) { Crime.Abort(sim, a, plan, "item taken"); return null; }
            if (t.Room < 0 || !sim.RoomUsable(a, S.Layout.Room(t.Room))) return null;
            if (!SetPieces.IsConsuming(t))
            {
                bool heading = t.Act != null && t.Act.Id != null && (t.Act.Id == "life:eat" || t.Act.Id == "life:tea" || t.Act.Id == "life:bar");
                var room = S.Layout.Room(t.Room);
                bool atTable = room != null && (room.Type == RoomType.Dining || room.Type == RoomType.TeaRoom) && sim.MealTime(out _);
                if (!heading && !atTable) return null;
                var dest = heading && t.Act.Steps.Count > 0 && t.Act.Steps[0].HasTarget ? t.Act.Steps[0].Target : t.Pos;
                act.Interruptible = true; act.Label = "식사"; act.Priority = 25;
                act.Steps.Add(Simulation.GoTo(sim.SnapPublic(new P3(dest.f, dest.x - 1.0f, dest.z + 0.7f))));
                act.Steps.Add(Simulation.Do("eat", 6, Anim.Eat));
                return act;
            }
            act.Interruptible = false; act.Label = "식사"; act.Priority = 30;
            act.Steps.Add(Simulation.GoTo(sim.SnapPublic(new P3(t.Pos.f, t.Pos.x - 0.9f, t.Pos.z + 0.3f))));
            act.Steps.Add(new ActionStep { Kind = "X_MSedate", Actor = t.Id, Item = st.Item, Duration = 0.8 });
            return act;
        }

        public static bool Dozing(Actor t) => t != null && t.Alive && (t.Pose == Pose.Sleep || (t.Status == ActorStatus.Unconscious && t.Body.DeathAt < 0 && t.Body.Bleed <= 0.01f));

        // ================================================================== action steps
        public static bool Exec(Simulation sim, Actor a, ActionStep st)
        {
            if (Violence.Exec(sim, a, st)) return true;   // --- violence track: X_MLoad / X_MBind / X_MUnbind / X_Bound / X_Untie
            var S = sim.S; MurderPlan plan = a.PlanId != null && S.Plans.TryGetValue(a.PlanId, out var pp) ? pp : null; var rng = S.R(Stream.PlanTie);
            if (DismemberAct(st.Kind)) return DismemberExec(sim, a, st, plan);
            switch (st.Kind)
            {
                case "X_MSedate":
                    {
                        a.Speed = 0; a.Anim = Anim.Use;
                        if (S.Clock < a.Act.StepEnd) return true;
                        var t = S.A(st.Actor); var sed = S.I(st.Item);
                        if (t == null || !t.Alive || !SetPieces.IsConsuming(t) || t.Pos.Dist(a.Pos) > 2.6f || sed == null || sed.Holder != a.Id) { sim.NextStepPublic(a); return true; }
                        foreach (var x in S.Living.Where(x => x != a && x != t && x.Room == a.Room && x.Pose != Pose.Sleep && x.Pos.Dist(a.Pos) < 5f).OrderBy(x => x.Id))
                        {
                            double p = x.Def.Obs / 100.0 * (1 - a.Def.Deceit / 130.0) * 0.45;
                            if (!rng.Chance(p)) continue;
                            var kx = S.K(x.Id); kx.Facts.Add($"sawdose:{a.Id}:{t.Id}:{(int)S.Clock}");
                            kx.Suspicion[a.Id] = (kx.Suspicion.TryGetValue(a.Id, out var sv) ? sv : 0) + 0.2f;
                            S.Log("SawNearCup", x.Id, a.Id, room: a.Room, data: t.Id, secret: true);
                        }
                        S.Flags["sedate:" + t.Id] = S.Clock + rng.Range(12, 20);
                        S.Flags["sedated:" + t.Id] = S.Clock;
                        if (!sed.Surface.Contains("used")) sed.Surface.Add("used");
                        var cup = new Item { Id = S.NewId("it"), Type = "Cup", Pos = t.Pos, Room = t.Room, HomeRoom = t.Room, HomePos = t.Pos }; cup.Surface.Add("residue:sedative");
                        S.Items[cup.Id] = cup; S.Emit(GameEventType.ItemMoved, null, data: cup.Id, text: "new", pos: cup.Pos);
                        S.Log("Sedate", a.Id, t.Id, sed.Id, t.Room, t.Pos, null, plan?.Id, true);
                        if (plan != null) Crime.Advance(sim, a, plan);
                        sim.NextStepPublic(a); return true;
                    }
                case "X_MDoze":   // the sedated victim's own last step: drops off (door unlocked, sleeping deeply)
                    {
                        a.Speed = 0;
                        var bed = S.Layout.BedroomOf(a.Id); var sp = bed?.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Tag == "sleep" && (s.Occupant == null || s.Occupant == a.Id));
                        if (sp != null && a.Room == bed.Id && sp.Pos.DistXZ(a.Pos) < 2.5f) { sp.Occupant = a.Id; a.Spot = sp.Id; a.Pos = sp.Pos; a.Yaw = sp.Yaw; }
                        a.Pose = a.Spot >= 0 ? Pose.Sleep : Pose.LieSide; a.Anim = Anim.Sleep;
                        a.Status = ActorStatus.Unconscious; a.Body.UnconsciousUntil = S.Clock + rng.Range(140, 170);
                        S.Flags.Remove("drowsy:" + a.Id);
                        S.Log("Doze", a.Id, room: a.Room, pos: a.Pos, secret: true);
                        S.Emit(GameEventType.Pose, a.Id, data: "doze");
                        a.Act = null; a.NextThink = S.Clock + 1; return true;
                    }
                case "X_MPlant":
                    {
                        a.Speed = 0; a.Anim = Anim.Use;
                        bool watched = S.Living.Any(x => x != a && x.Room == a.Room && x.Pose != Pose.Sleep);
                        if (watched) { if (plan != null) Crime.Replan(sim, a, plan, "누가 방에 들어와서"); sim.Interrupt(a, 10); return true; }
                        if (S.Clock < a.Act.StepEnd) return true;
                        var t = S.A(st.Actor); var vial = S.I(st.Item); var parts = (st.Data ?? "").Split('|'); var dose = S.I(parts[0]);
                        bool gift = parts.Contains("gift"), note = parts.Contains("note");
                        if (t == null || !t.Alive || vial == null || vial.Holder != a.Id || dose == null) { if (plan != null) Crime.Abort(sim, a, plan, "item taken"); sim.NextStepPublic(a); return true; }
                        if (plan != null && !Crime.Reserve(sim, a, t, plan)) { Crime.Abort(sim, a, plan, "때가 맞지 않아서"); return true; }
                        var desk = S.Layout.Room(st.Room)?.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(f => f.Type == "Nightstand" || f.Type == "Desk");
                        var at = desk != null ? new P3(desk.Pos.f, desk.Pos.x, desk.Pos.z) : a.Pos;
                        if (gift)
                        {
                            if (dose.Holder == a.Id) sim.DropItem(a, dose, at); else { dose.Pos = at; dose.Room = st.Room; }
                            dose.Owner = t.Id; dose.Name = "리본으로 묶은 " + (dose.Def?.Kor ?? "선물"); dose.Note = "고마웠어. 푹 자.";
                        }
                        if (!dose.Surface.Contains("poisoned")) dose.Surface.Add("poisoned");
                        dose.ArmedBy = a.Id;
                        if (!vial.Surface.Contains("used")) vial.Surface.Add("used");
                        if (note)
                        {
                            var n = FakeNoteItem(sim, a, t, new P3(at.f, at.x + 0.25f, at.z + 0.1f), st.Room);
                            n.Surface.Add("planted");
                        }
                        var trap = new Trap { Id = S.NewId("trap"), Kind = "Bedtime", Owner = a.Id, Plan = plan?.Id, Target = t.Id, ArmedAt = S.Clock, Active = true, Room = st.Room };
                        S.Traps.Add(trap);
                        sim.AddTrace("PowderSpill", at, st.Room, a.Id, t.Id, 0.08f, 2, "책상 위에 흘린 녹색 가루 몇 알", "이 자리에서 누군가 가루를 덜었다", "누가, 언제, 무엇에 썼는지");
                        S.Log("PoisonPlant", a.Id, t.Id, dose.Id, st.Room, a.Pos, (dose.Def?.Kor ?? dose.Type) + (gift ? "|gift" : "") + (note ? "|note" : ""), plan?.Id, true);
                        if (plan != null) { plan.Stage = "Armed"; plan.Log.Add($"{ClockFmt.Vague(S.Clock)} 독을 넣어 둠: {S.RoomName(st.Room)}"); Crime.Advance(sim, a, plan); }
                        sim.NextStepPublic(a); return true;
                    }
                case "X_MKeySlide":
                    {
                        a.Speed = 0; a.Pose = Pose.Crouch; a.Anim = Anim.Use;
                        if (S.Clock < a.Act.StepEnd) return true;
                        var t = S.A(st.Actor); var key = S.I(st.Item); var d = S.Layout.Doors.ElementAtOrDefault(st.Door);
                        if (t != null && key != null && d != null && t.Pocket.Contains(key.Id))
                        {
                            var bed = S.Layout.Room(t.Room); int outsideId = d.RoomA == bed.Id ? d.RoomB : d.RoomA; var outside = S.Layout.Room(outsideId);
                            t.Pocket.Remove(key.Id);
                            a.Pos = sim.SnapPublic(InsideOf(d, outside, 0.9f)); a.Room = outsideId;
                            sim.SetDoor(a, d, false, true, "밖에서 잠금");
                            // crouch, and push the key back through the gap under the door
                            var inside = InsideOf(d, bed, 0.55f + rng.F() * 0.3f);
                            key.Holder = null; key.Room = bed.Id; key.Pos = new P3(inside.f, inside.x + rng.Range(-0.25f, 0.25f), inside.z + rng.Range(-0.25f, 0.25f)); key.Hidden = false;
                            if (!key.Surface.Contains("slid")) key.Surface.Add("slid");
                            S.Emit(GameEventType.ItemMoved, a.Id, data: key.Id, text: "drop", pos: key.Pos);
                            sim.AddTrace("KeySlide", InsideOf(d, bed, 0.25f), bed.Id, a.Id, t.Id, 0.12f, 1, "문턱 안쪽 바닥에 난 가늘고 긴 긁힌 자국", "무언가가 문 아래 틈으로 밀려 들어왔다", "누가, 언제");
                            S.Flags["sealed:" + t.Id] = d.Id; S.Flags["sealedat:" + t.Id] = S.Clock;
                            S.Log("KeySlide", a.Id, t.Id, key.Id, bed.Id, d.Pos, "key", plan?.Id, true);
                        }
                        a.Pose = Pose.Stand; if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
                case "X_MNote":
                    {
                        a.Speed = 0; a.Pose = Pose.Crouch; a.Anim = Anim.Write;
                        if (S.Clock < a.Act.StepEnd) return true;
                        var t = S.A(st.Actor);
                        if (t != null)
                        {
                            FakeNoteItem(sim, a, t, new P3(t.Pos.f, t.Pos.x + 0.45f, t.Pos.z + 0.2f), t.Room);
                            // the "overdose": the emptied packet of the draught left by the bed
                            var sed = sim.Carried(a).FirstOrDefault(i => i.Type == "Sedative");
                            if (sed != null) { sim.DropItem(a, sed, new P3(t.Pos.f, t.Pos.x + 0.3f, t.Pos.z - 0.2f)); if (!sed.Surface.Contains("empty")) sed.Surface.Add("empty"); }
                        }
                        a.Pose = Pose.Stand; if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
                case "X_MBurn":
                    {
                        a.Speed = 0; a.Anim = Anim.Use;
                        if (S.Clock < a.Act.StepEnd) return true;
                        var f = S.Layout.Furniture.ElementAtOrDefault(st.Furniture); var burned = new List<string>();
                        if (f != null)
                        {
                            var fp = new P3(f.Pos.f, f.Pos.x, f.Pos.z);
                            var w = S.I(plan?.Weapon);
                            if (w != null && w.Holder == a.Id) { Burn(sim, a, w, fp, f.Room); burned.Add(w.Kor); }
                            if (a.BloodOnClothes > 0.15f)
                            {
                                var btn = new Item { Id = S.NewId("it"), Type = "Button", Name = Cast.GivenOf(a.Id) + "의 타다 남은 셔츠 단추", Owner = a.Id, Pos = new P3(fp.f, fp.x + 0.2f, fp.z + 0.1f), Room = f.Room, HomeRoom = f.Room, HomePos = fp, Hidden = true };
                                btn.Surface.Add("burnt"); S.Items[btn.Id] = btn; S.Emit(GameEventType.ItemMoved, a.Id, data: btn.Id, text: "spawn", pos: btn.Pos);
                                S.Log("ChangeClothes", a.Id, room: a.Room, data: $"blood={a.BloodOnClothes:0.00} burned", secret: true);
                                a.BloodOnClothes = 0; burned.Add("피 묻은 옷");
                            }
                            S.Flags["furnace:" + f.Id] = S.Clock; if (!f.Marks.Contains("burned")) f.Marks.Add("burned");
                            sim.AddTrace("Ash", new P3(fp.f, fp.x + 0.5f, fp.z + 0.5f), f.Room, a.Id, plan?.Target, 0.35f, 1, "소각로 앞에 새로 떨어진 재", "최근 누군가 소각로에 무언가를 태웠다", "누가 무엇을 태웠는지");
                            sim.Sound(SoundKind.Machine, fp, 0.5f, a.Id);
                            S.Emit(GameEventType.Furniture, a.Id, text: "stoked", id: f.Id);
                            S.Log("Burn", a.Id, plan?.Target, room: f.Room, pos: fp, data: string.Join("·", burned), plan: plan?.Id, secret: true);
                        }
                        if (plan != null) { Crime.Advance(sim, a, plan); if (plan.Step < plan.Steps.Count && plan.Steps[plan.Step].Kind == "CleanUp") Crime.Advance(sim, a, plan); }
                        sim.NextStepPublic(a); return true;
                    }
                case "X_MDump":
                    {
                        a.Speed = 0; a.Anim = Anim.PutDown;
                        if (S.Clock < a.Act.StepEnd) return true;
                        var w = S.I(st.Item); var pool = S.Layout.Room(st.Room);
                        if (w != null && w.Holder == a.Id && pool != null)
                        {
                            // it sinks, and the circulation carries it into the filter basket of the water room
                            var wr = S.Layout.First(RoomType.WaterRoom); var pump = wr?.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(x => x.Type == "PumpUnit");
                            var dest = pump != null ? new P3(pump.Pos.f, pump.Pos.x + (wr.Rect.CX - pump.Pos.x) * 0.2f, pump.Pos.z + (wr.Rect.CZ - pump.Pos.z) * 0.2f) : new P3(pool.Floor, pool.Rect.CX, pool.Rect.CZ);
                            sim.DropItem(a, w, dest, true);
                            w.Bloody = false; w.Surface.Remove("blood"); if (!w.Surface.Contains("waterlogged")) w.Surface.Add("waterlogged"); w.Wet = true;
                            S.Emit(GameEventType.ItemState, a.Id, data: w.Id, text: "washed");
                            sim.Sound(SoundKind.Splash, a.Pos, 0.35f, a.Id);
                            sim.AddTrace("Water", a.Pos, a.Room, a.Id, plan?.Target, 0.4f, 1, "수영장 가장자리에 크게 튄 물 자국", "누군가 물속에 무언가를 떨어뜨렸다", "누가 무엇을 떨어뜨렸는지");
                            S.Log("DumpWater", a.Id, plan?.Target, w.Id, pool.Id, a.Pos, w.Type, plan?.Id, true);
                        }
                        if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
                case "X_MBury":
                    {
                        a.Speed = 0; a.Pose = Pose.Crouch; a.Anim = Anim.Garden;
                        if (S.Clock < a.Act.StepEnd) return true;
                        var w = S.I(st.Item); var pl = S.Layout.Furniture.ElementAtOrDefault(st.Furniture);
                        if (w != null && w.Holder == a.Id && pl != null)
                        {
                            sim.DropItem(a, w, new P3(pl.Pos.f, pl.Pos.x, pl.Pos.z), true);
                            if (!w.Surface.Contains("soil")) w.Surface.Add("soil");
                            if (!pl.Marks.Contains("dug")) pl.Marks.Add("dug");
                            sim.AddTrace("Soil", new P3(pl.Pos.f, pl.Pos.x, pl.Pos.z), pl.Room, a.Id, plan?.Target, 0.3f, 1, "화분대 흙을 새로 팠다가 다시 덮은 자국", "누군가 이 흙을 팠다가 다시 덮었다", "누가 무엇을 묻었는지");
                            S.Log("Bury", a.Id, plan?.Target, w.Id, pl.Room, pl.Pos, w.Type, plan?.Id, true);
                        }
                        a.Pose = Pose.Stand; if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
                case "X_MNoise":
                    {
                        a.Speed = 0; a.Anim = Anim.Operate;
                        if (S.Clock < a.Act.StepEnd) return true;
                        var room = S.Layout.Room(st.Room); var f = S.Layout.Furniture.ElementAtOrDefault(st.Furniture);
                        if (room != null)
                        {
                            var p = f != null ? new P3(f.Pos.f, f.Pos.x, f.Pos.z) : a.Pos;
                            sim.Sound(SoundKind.Machine, p, 0.95f, a.Id);   // the roar itself is heard across the floor (a time anchor)…
                            S.Flags["silence:" + room.Id] = S.Clock + 30; S.Flags["roar:" + room.Id] = S.Clock;   // …and then drowns everything made inside
                            if (f != null) { f.Marks.RemoveAll(m => m.StartsWith("출력 다이얼")); f.Marks.Add($"출력 다이얼이 끝까지 돌아가 있다 — {ClockFmt.Vague(S.Clock)}에 누가 돌린 듯하다"); }
                            S.Log("NoiseMask", a.Id, plan?.Target, room: room.Id, pos: p, data: f?.Type, plan: plan?.Id, secret: true);
                        }
                        if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
            }
            return false;
        }

        static void Burn(Simulation sim, Actor a, Item w, P3 fp, int room)
        {
            var S = sim.S;
            sim.DropItem(a, w, new P3(fp.f, fp.x + 0.1f, fp.z), true);
            bool survives = w.Def != null && (w.Def.Mat == Mat.Metal || w.Def.Mat == Mat.Glass || w.Def.Mat == Mat.Stone || w.Def.Mat == Mat.Ceramic);
            w.Bloody = false; w.Surface.Remove("blood"); if (!w.Surface.Contains("burnt")) w.Surface.Add("burnt");
            if (!survives) { w.Damage = 3; w.Name = "타다 남은 " + (w.Def?.Kor ?? w.Type) + " 조각"; }
            S.Emit(GameEventType.ItemState, a.Id, data: w.Id, text: "burnt");
        }

        static Item FakeNoteItem(Simulation sim, Actor a, Actor t, P3 at, int room)
        {
            var S = sim.S; var rng = S.R(Stream.PlanTie);
            string[] texts = { "미안해. 이제 그만 쉬고 싶어.", "더는 버틸 수가 없어. 찾지 마.", "다들 미안해. 이게 내가 고른 끝이야.", "누구 탓도 아니야. 그냥 너무 지쳤어." };
            var n = new Item { Id = S.NewId("it"), Type = "Document", Name = "접힌 쪽지 — 유서", Note = texts[rng.R(texts.Length)], NoteFrom = "forged:" + a.Id + ":" + t.Id, Pos = at, Room = room, HomeRoom = room, HomePos = at };
            S.Items[n.Id] = n; S.Emit(GameEventType.ItemMoved, a.Id, data: n.Id, text: "spawn", pos: at);
            S.Log("FakeNote", a.Id, t.Id, n.Id, room, at, n.Note, a.PlanId, true);
            return n;
        }

        // ================================================================== contact: the three new attack modes (called from Crime.Attack)
        public static void Attack(Simulation sim, Actor a, Actor t, ActionStep st, MurderPlan plan, Item weapon, Rng crng)
        {
            // --- violence track: strangling / smothering / drowning run as prolonged, time-true holds; guns fire (Sim/Violence)
            if (Violence.Attack(sim, a, t, st, plan, weapon, crng)) return;
            var S = sim.S; string mode = st.Tag; float d = a.Pos.Dist(t.Pos);
            float reach = mode == "push" ? 1.15f : mode == "smother" ? 1.1f : 0.95f;
            if (mode == "push" && !AtEdge(sim, t, out _, out _))
            {
                // the moment passed (the target walked on): back to waiting at the edge
                if (plan != null) { int si = plan.Steps.FindIndex(x => x.Kind == "Stalk" && x.Note == "edge"); if (si >= 0 && si < plan.Step) { plan.Step = si; plan.Steps[si].Done = false; plan.Log.Add($"{ClockFmt.DayHM(S.Clock)} 수정: 상대가 계단 끝에서 멀어짐"); } }
                sim.Interrupt(a, 0.5); return;
            }
            if (mode == "smother" && !Dozing(t) && t.Status == ActorStatus.Active)
            {
                if (plan != null) { plan.Log.Add($"{ClockFmt.DayHM(S.Clock)} 수정: 상대가 깨어 있음"); Crime.Replan(sim, a, plan, "target woke"); }
                sim.Interrupt(a, 3); return;
            }
            if (d > reach)
            {
                if (d > 25f || S.Clock - a.Act.StepStart > 6) { if (plan != null) Crime.Replan(sim, a, plan, "lost target"); sim.Interrupt(a); return; }
                P3 goal = t.Pos;
                if (mode == "strangle" && t.Speed < 0.2f) { double yr = t.Yaw * Math.PI / 180; goal = sim.SnapPublic(new P3(t.Pos.f, t.Pos.x - (float)Math.Sin(yr) * 0.55f, t.Pos.z - (float)Math.Cos(yr) * 0.55f)); }   // from behind
                if (a.Act.Path == null || S.Tick % 6 == 0)
                {
                    var pr = Pathfinder.Find(S.Layout, a.Pos, goal, sim.DoorCostFor(a)); if (!pr.Ok) { sim.Interrupt(a); return; }
                    a.Act.Path = pr.Points; a.Act.PathDoors = pr.DoorAtPoint; a.Act.PathStairs = pr.StairAtPoint; a.Act.PathIdx = 1;
                }
                sim.MoveAlongPublic(a, mode != "smother"); return;
            }
            a.Speed = 0; a.Yaw = MathX.AngleDeg(t.Pos.x - a.Pos.x, t.Pos.z - a.Pos.z);
            long nextStrike = S.Flags.TryGetValue("strike:" + a.Id, out var ns) ? (long)ns : 0;
            if (S.Tick < nextStrike) return;
            S.Flags["strike:" + a.Id] = S.Tick + (mode == "push" ? 4 : 9);
            string cntKey = "blows:" + a.Id + ":" + t.Id; double blows = S.Flags.TryGetValue(cntKey, out var bc) ? bc : 0; S.Flags[cntKey] = blows + 1;
            bool first = !S.Flags.ContainsKey("attackstart:" + a.Id + ":" + t.Id);
            if (first) { S.Flags["attackstart:" + a.Id + ":" + t.Id] = S.Clock; Incidents.OnAttackBegin(sim, a, t, plan); }
            float face = Math.Abs(MathX.DeltaAngle(t.Yaw, MathX.AngleDeg(a.Pos.x - t.Pos.x, a.Pos.z - t.Pos.z)));
            bool aware = face < 100 && t.Status == ActorStatus.Active && t.Pose != Pose.Sleep;
            void Done() { S.Flags.Remove(cntKey); S.Flags.Remove("attackstart:" + a.Id + ":" + t.Id); if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); }
            switch (mode)
            {
                case "strangle":
                    {
                        var cord = weapon != null && IsCord(weapon.Def) ? weapon : sim.Carried(a).FirstOrDefault(i => IsCord(i.Def));
                        if (cord == null) { st.Tag = "kill"; return; }   // lost the cord: whatever is at hand
                        if (first) S.Log("Garrote", a.Id, t.Id, cord.Id, t.Room, t.Pos, cord.Type, plan?.Id, true);
                        a.Anim = Anim.Strangle; a.BusyUntil = Math.Max(a.BusyUntil, S.Tick + 8);
                        S.Emit(GameEventType.Strike, a.Id, t.Id, pos: t.Pos, data: Anim.Strangle.ToString());
                        double hit = aware ? 0.55 - t.Body.Resistance * 0.2 + (a.Def.HeightCm - t.Def.HeightCm) / 200.0 : 0.92;
                        if (t.Status != ActorStatus.Active) hit = 1;
                        if (crng.Chance(hit))
                        {
                            bool wasConscious = t.Status == ActorStatus.Active;
                            int sev = Math.Max(2, Math.Min(4, (cord.Def?.Sev ?? 2) + (aware ? 0 : 1)));
                            if (wasConscious && (aware || crng.Chance(0.55))) Scratch(sim, t, a);   // the victim claws at the hands on the cord
                            sim.Strike(a.Id, t, BodyRegion.Neck, DamageType.Choke, sev, cord.Id, plan?.Id ?? "attack");
                            if (!cord.Surface.Contains("stretched")) cord.Surface.Add("stretched");
                        }
                        else { sim.Sound(SoundKind.Struggle, t.Pos, 0.35f, a.Id); if (aware && t.Status == ActorStatus.Active) { Scratch(sim, t, a); Crime.VictimReact(sim, t, a); } }
                        if (!t.Alive || (t.Status == ActorStatus.Unconscious && t.Body.DeathAt >= 0) || blows >= 14) Done();
                        return;
                    }
                case "push":
                    {
                        AtEdge(sim, t, out var kind, out var id);
                        a.Anim = Anim.Struggle; a.BusyUntil = Math.Max(a.BusyUntil, S.Tick + 10);
                        S.Emit(GameEventType.Strike, a.Id, t.Id, pos: t.Pos, data: Anim.Shove.ToString());
                        double p = aware ? 0.7 - t.Body.Resistance * 0.15 + (a.Def.HeightCm - t.Def.HeightCm) / 150.0 : 0.95;
                        if (crng.Chance(p)) { Fall(sim, a, t, kind, id, aware, plan); Done(); return; }
                        // braced, grabbed the rail — and knows who did it
                        sim.Sound(SoundKind.Struggle, t.Pos, 0.45f, a.Id);
                        S.K(t.Id).Facts.Add("attacked-by:" + a.Id);
                        Relations.Change(S, t.Id, a.Id, fear: 0.6f, grudge: 0.5f, trust: -0.8f, like: -0.6f, memory: "나를 계단 끝에서 밀려고 했다", tag: "enemy");
                        S.Log("ShoveFailed", a.Id, t.Id, room: t.Room, pos: t.Pos, plan: plan?.Id, secret: true);
                        Crime.VictimReact(sim, t, a);
                        if (plan != null) Crime.Abort(sim, a, plan, "상대에게 얼굴을 들켜서");
                        return;
                    }
                case "smother":
                    {
                        if (first)
                        {
                            var room = S.Layout.Room(t.Room); bool bedroom = room?.Type == RoomType.Bedroom && room.Owner == t.Id;
                            var pillow = new Item { Id = S.NewId("it"), Type = "Pillow", Name = bedroom ? Cast.GivenOf(t.Id) + " 침대의 베개" : "소파 쿠션", Pos = new P3(t.Pos.f, t.Pos.x + 0.35f, t.Pos.z + 0.15f), Room = t.Room, HomeRoom = t.Room, HomePos = t.Pos };
                            pillow.Surface.Add("pressed"); S.Items[pillow.Id] = pillow; S.Emit(GameEventType.ItemMoved, a.Id, data: pillow.Id, text: "spawn", pos: pillow.Pos);
                            S.Flags["pillow:" + t.Id] = S.Clock;
                            S.Log("Smother", a.Id, t.Id, pillow.Id, t.Room, t.Pos, bedroom ? "bed" : "cushion", plan?.Id, true);
                            if (t.Pose == Pose.Sleep && t.Status == ActorStatus.Active && crng.Chance(0.25)) { Scratch(sim, t, a); sim.Sound(SoundKind.Struggle, t.Pos, 0.25f, a.Id); }
                        }
                        a.Anim = Anim.Strangle; a.BusyUntil = Math.Max(a.BusyUntil, S.Tick + 12);
                        S.Emit(GameEventType.Strike, a.Id, t.Id, pos: t.Pos, data: Anim.Strangle.ToString());
                        var pil = S.Items.Values.Where(i => i.Type == "Pillow" && i.Room == t.Room && i.Surface.Contains("pressed")).OrderByDescending(i => i.Id.Length).ThenByDescending(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
                        sim.Strike(a.Id, t, BodyRegion.Head, DamageType.Choke, 4, pil?.Id, plan?.Id ?? "attack");
                        if (!t.Alive || (t.Body.DeathAt >= 0) || blows >= 6) Done();
                        return;
                    }
            }
        }

        /// <summary>The victim's nails catch the attacker's hand: a small fresh wound on the culprit (visible), skin under the victim's nails.</summary>
        static void Scratch(Simulation sim, Actor victim, Actor culprit)
        {
            var S = sim.S; if (S.Flags.ContainsKey("scratched:" + culprit.Id + ":" + victim.Id)) return;
            var rng = S.R(Stream.Combat); var region = rng.Chance(0.6) ? BodyRegion.HandL : BodyRegion.ArmR;
            var w = new Wound { Region = region, Type = DamageType.Cut, Sev = 1, Tick = S.Tick, Clock = S.Clock, By = victim.Id, CauseEvent = "scratch" };
            culprit.Body.Wounds.Add(w);
            if (region == BodyRegion.HandL) culprit.Body.HandL = MathX.Clamp01(culprit.Body.HandL - 0.1f); else culprit.Body.HandR = MathX.Clamp01(culprit.Body.HandR - 0.05f);
            S.Flags["scratched:" + culprit.Id + ":" + victim.Id] = S.Clock; S.Flags["scratch:" + culprit.Id] = S.Clock; S.Flags["nails:" + victim.Id] = S.Clock;
            S.Emit(GameEventType.Wound, victim.Id, culprit.Id, text: "손등의 긁힌 상처", pos: culprit.Pos, value: 1, data: $"{region}|{DamageType.Cut}|1|0|0,0,0");
            S.Log("Scratched", culprit.Id, victim.Id, room: culprit.Room, pos: culprit.Pos, data: region.ToString(), secret: true);
        }

        /// <summary>A body goes over the edge: down the stair to its foot, or over the gallery rail into the hall below.</summary>
        static void Fall(Simulation sim, Actor a, Actor t, string kind, int id, bool aware, MurderPlan plan)
        {
            var S = sim.S; var rng = S.R(Stream.Combat);
            P3 from = t.Pos; int fromRoom = t.Room; P3 land; int landRoom;
            if (kind == "stair") { var s = S.Layout.Stairs[id]; var b = Bottom(s); land = sim.SnapPublic(new P3(b.f, b.x + rng.Range(-0.4f, 0.4f), b.z + rng.Range(-0.4f, 0.4f))); }
            else
            {
                var v = S.Layout.Room(id); float cx = v.Rect.CX, cz = v.Rect.CZ; float dx = cx - from.x, dz = cz - from.z; float dl = (float)Math.Sqrt(dx * dx + dz * dz) + 1e-3f;
                var below = new P3(v.Floor - 1, from.x + dx / dl * 2.2f, from.z + dz / dl * 2.2f);
                land = sim.SnapPublic(below);
            }
            landRoom = S.Layout.RoomAt(land);
            sim.Sound(SoundKind.Scream, from, 0.85f, t.Id, t.Id);
            if (t.Act != null) sim.Interrupt(t, 3); t.TalkingTo = null; t.Following = null;
            t.StairId = -1; t.StairUntil = 0; t.Pos = land; t.Room = landRoom; t.Speed = 0;
            S.Log("Shove", a.Id, t.Id, room: fromRoom, pos: from, data: kind + "|" + S.RoomName(landRoom), plan: plan?.Id, secret: true);
            S.Flags["pushed:" + t.Id] = S.Clock;
            sim.Sound(SoundKind.Fall, land, 0.95f, t.Id);
            int sev = kind == "rail" ? 4 : rng.Chance(0.55) ? 4 : 3;
            sim.Strike(a.Id, t, BodyRegion.Head, DamageType.Fall, sev, null, "push");
            if (t.Alive) sim.Strike(a.Id, t, rng.Chance(0.5) ? BodyRegion.LegL : BodyRegion.ArmR, DamageType.Fall, 2, null, "push");
            else { var w = new Wound { Region = rng.Chance(0.5) ? BodyRegion.LegL : BodyRegion.ArmR, Type = DamageType.Fall, Sev = 2, Tick = S.Tick, Clock = S.Clock, By = a.Id, CauseEvent = "push" }; t.Body.Wounds.Add(w); }
            var tr = sim.AddTrace("Scuff", from, fromRoom, a.Id, t.Id, 0.4f, 1, kind == "rail" ? "난간 앞 바닥에 신발이 미끄러진 자국" : "계단 맨 윗단에 신발이 미끄러진 자국", "누군가 이곳에서 뒤로 균형을 잃었다", "혼자 미끄러졌는지, 누가 밀었는지");
            if (tr != null) tr.Note = "edge=" + kind;
            // in the grab, a button tears off the pusher's sleeve and stays where it fell
            if (rng.Chance(aware ? 0.85 : 0.55))
            {
                var btn = new Item { Id = S.NewId("it"), Type = "Button", Name = "뜯겨 나간 소매 단추", Owner = a.Id, Pos = new P3(from.f, from.x + rng.Range(-0.5f, 0.5f), from.z + rng.Range(-0.5f, 0.5f)), Room = fromRoom, HomeRoom = fromRoom };
                btn.HomePos = btn.Pos; btn.Surface.Add("torn"); S.Items[btn.Id] = btn; S.Emit(GameEventType.ItemMoved, a.Id, data: btn.Id, text: "spawn", pos: btn.Pos);
                S.Log("ButtonTorn", a.Id, t.Id, btn.Id, fromRoom, btn.Pos, secret: true);
            }
        }

        // ================================================================== remote mechanisms (traps with new kinds; called from Tricks)
        public static bool ArmTrap(Simulation sim, Actor a, Trap trap, ActionStep st)
        {
            var S = sim.S;
            if (trap.Kind != "Shock") return false;
            var f = S.Layout.Furniture[st.Furniture]; trap.Furniture = st.Furniture;
            var parts = st.Tag.Split(':'); trap.Spot = parts.Length > 1 && int.TryParse(parts[1], out var sp) ? sp : -1;
            f.Marks.Add("전선 피복이 칼로 반듯하게 벗겨져 있다");
            var spot = S.Layout.Spots.ElementAtOrDefault(trap.Spot);
            var wet = spot != null ? spot.Pos : new P3(f.Pos.f, f.Pos.x, f.Pos.z);
            sim.AddTrace("Water", wet, f.Room, a.Id, st.Actor, 0.7f, 0, "기계 앞 바닥에 넓게 고인 물", "누군가 이 자리에 물을 부었거나 흘렸다", "누가, 왜 그랬는지");
            var tool = sim.Carried(a).FirstOrDefault(i => i.Type == "Pliers" || i.Type == "Scissors" || i.Type == "Chisel" || i.Type == "PaletteKnife" || i.Type == "KitchenKnife" || i.Type == "Scalpel");
            if (tool != null && !tool.Surface.Contains("shavings")) tool.Surface.Add("shavings");
            S.Log("ShockRigged", a.Id, st.Actor, room: f.Room, pos: f.Pos, data: f.Type, plan: trap.Plan, secret: true);
            return true;
        }

        public static P3? TrapAt(GameState S, Trap t)
        {
            if (t.Kind == "Shock") { var f = S.Layout.Furniture.ElementAtOrDefault(t.Furniture); return f != null ? new P3(f.Pos.f, f.Pos.x, f.Pos.z) : (P3?)null; }
            if (t.Kind == "Bedtime") { var r = S.Layout.Room(t.Room); return r != null ? new P3(r.Floor, r.Rect.CX, r.Rect.CZ) : (P3?)null; }
            return null;
        }

        public static bool TrapCheck(Simulation sim, Trap t)
        {
            var S = sim.S;
            if (t.Kind == "Shock")
            {
                // the stripped cable is on the power strip that feeds the whole row of these machines in this room
                var f0 = S.Layout.Furniture.ElementAtOrDefault(t.Furniture); var room = f0 != null ? S.Layout.Room(f0.Room) : null;
                if (room == null) { t.Active = false; return true; }
                foreach (var si in room.Spots)
                {
                    var sp = S.Layout.Spots[si]; if (sp.Furniture < 0 || S.Layout.Furniture[sp.Furniture].Type != f0.Type) continue;
                    var v = S.Actors.Values.Where(x => x.Id != t.Owner && x.Status == ActorStatus.Active && x.CarriedBy == null && x.Pos.f == sp.Pos.f && (x.Spot == sp.Id || (x.IsPlayer && x.Pos.DistXZ(sp.Pos) < 0.5f))).OrderBy(x => x.Id).FirstOrDefault();
                    if (v != null) { FireShock(sim, t, v); break; }
                }
                return true;
            }
            if (t.Kind == "Bedtime")
            {
                var v = S.A(t.Target);
                if (v == null || !v.Alive) { t.Active = false; return true; }
                if (v.Room == t.Room && v.Pose == Pose.Sleep) FireBedtime(sim, t, v);
                return true;
            }
            return false;
        }

        static void FireShock(Simulation sim, Trap t, Actor v)
        {
            var S = sim.S; var f = S.Layout.Furniture[t.Furniture]; var room = S.Layout.Room(f.Room);
            t.Active = false; t.FiredAt = S.Clock; t.Victim = v.Id;
            S.Flags["attackstart:" + t.Owner + ":" + v.Id] = S.Clock;
            S.Log("TrapFired", t.Owner, v.Id, room: v.Room, pos: v.Pos, data: $"{t.Id} {t.Kind}", plan: t.Plan, secret: true);
            S.Log("ShockFired", t.Owner, v.Id, room: v.Room, pos: v.Pos, data: f.Type, plan: t.Plan, secret: true);
            sim.Sound(SoundKind.Switch, f.Pos, 0.9f, null); sim.Sound(SoundKind.Scream, v.Pos, 0.7f, v.Id, v.Id);
            f.Damage = Math.Max(f.Damage, 1); f.Marks.Add($"{ClockFmt.Vague(S.Clock)}에 불꽃이 튀면서 멈췄다");
            S.Emit(GameEventType.Furniture, t.Owner, v.Id, id: f.Id, pos: f.Pos, data: "sparks");
            sim.Strike(t.Owner, v, BodyRegion.HandR, DamageType.Shock, 4, null, "trap:" + t.Id);
            sim.AddTrace("Scorch", new P3(f.Pos.f, f.Pos.x, f.Pos.z), f.Room, t.Owner, v.Id, 0.3f, 0, "콘센트와 전선 끝이 까맣게 그을렸다", "이곳에서 강한 전기 불꽃이 튀었다", "우연인지, 누가 손을 댔는지");
            // the breaker trips: that circuit goes dark and the switchboard keeps the moment
            int circuit = room?.Circuit ?? -1;
            if (circuit >= 0 && S.CircuitOn(circuit))
            {
                sim.SetCircuit(circuit, false, null);
                S.Flags["breaker:" + circuit] = S.Clock; S.Flags["breakerreset:" + circuit] = S.Clock + 12;
                S.Log("BreakerTrip", null, v.Id, room: f.Room, data: circuit.ToString(), secret: true);
            }
            AfterFire(sim, t, v, "감전 함정");
        }

        static void FireBedtime(Simulation sim, Trap t, Actor v)
        {
            var S = sim.S; var rng = S.R(Stream.Combat);
            var dose = S.Items.Values.Where(i => i.Holder == null && i.Room == t.Room && i.Surface.Contains("poisoned") && !i.Surface.Contains("consumed")).OrderBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
            if (dose == null) { t.Active = false; t.DisarmedAt = S.Clock; return; }
            t.Active = false; t.FiredAt = S.Clock; t.Victim = v.Id;
            dose.Surface.Add("consumed");
            S.Flags["attackstart:" + t.Owner + ":" + v.Id] = S.Clock;
            double delay = rng.Range(35, 60);
            v.Body.PoisonBy = t.Owner; v.Body.PoisonedAt = S.Clock; v.Body.DeathAt = v.Body.DeathAt < 0 ? S.Clock + delay : Math.Min(v.Body.DeathAt, S.Clock + delay);
            S.Log("PoisonTaken", t.Owner, v.Id, dose.Id, t.Room, v.Pos, dose.Def?.Kor ?? dose.Type, t.Plan, true);
            S.Log("TrapFired", t.Owner, v.Id, room: v.Room, pos: v.Pos, data: $"{t.Id} {t.Kind}", plan: t.Plan, secret: true);
            // the victim locked their own door for the night: the room will be found sealed from the inside
            var bed = S.Layout.Room(t.Room); var d = bed != null && bed.Doors.Count > 0 ? S.Layout.Doors[bed.Doors[0]] : null;
            if (d != null && d.Locked) { S.Flags["sealed:" + v.Id] = d.Id; S.Flags["sealedat:" + v.Id] = S.Clock; }
            AfterFire(sim, t, v, "시간차 독");
        }

        static void AfterFire(Simulation sim, Trap t, Actor v, string what)
        {
            var S = sim.S;
            if (t.Plan == null || !S.Plans.TryGetValue(t.Plan, out var plan) || plan.Stage == "Aborted" || plan.Stage == "Done") return;
            var owner = S.A(t.Owner);
            if (v.Id == plan.Target) { plan.Log.Add($"{ClockFmt.Vague(S.Clock)} {what} 작동"); plan.Stage = "Concealing"; if (owner != null && plan.Step < plan.Steps.Count && (plan.Steps[plan.Step].Kind == "X_WaitTrap" || plan.Steps[plan.Step].Kind == "X_WaitBedtime")) Crime.Advance(sim, owner, plan); }
            else if (owner != null) { Crime.Abort(sim, owner, plan, "엉뚱한 사람이 함정에 걸려서"); owner.Needs.Stress = MathX.Clamp01(owner.Needs.Stress + 0.4f); owner.Needs.Fear = MathX.Clamp01(owner.Needs.Fear + 0.3f); }
        }

        /// <summary>Disarming the new kinds: the cable is taped and the floor mopped; the poisoned bedtime drink is taken away.</summary>
        public static bool Disarm(Simulation sim, Trap t, string by, string how)
        {
            var S = sim.S;
            if (t.Kind == "Shock") { var f = S.Layout.Furniture.ElementAtOrDefault(t.Furniture); f?.Marks.Add($"{ClockFmt.Vague(S.Clock)}에 누군가 벗겨진 전선을 감고 바닥의 물을 닦았다"); return true; }
            if (t.Kind == "Bedtime")
            {
                foreach (var it in S.Items.Values.Where(i => i.Holder == null && i.Room == t.Room && i.Surface.Contains("poisoned") && !i.Surface.Contains("consumed")).OrderBy(i => i.Id, StringComparer.Ordinal).ToList())
                { it.Surface.Remove("poisoned"); it.Surface.Add("discarded"); if (how == "owner") { it.Room = -1; it.Holder = by; S.A(by)?.Pocket.Add(it.Id); } }
                return true;
            }
            return false;
        }

        // ================================================================== ticking: the draught, the breaker, what people notice
        public static void Tick(Simulation sim)
        {
            var S = sim.S;
            if (S.Tick % 10 != 3) return;
            // a sleeping draught taking hold: the victim heads for bed without locking the door, and drops off
            foreach (var v in S.LivingNpcs.OrderBy(x => x.Id).ToList())
            {
                if (!S.Flags.TryGetValue("sedate:" + v.Id, out var at) || S.Clock < at) continue;
                S.Flags.Remove("sedate:" + v.Id);
                if (v.Status != ActorStatus.Active || v.CarriedBy != null) continue;
                S.Flags["drowsy:" + v.Id] = S.Clock;
                var bed = S.Layout.BedroomOf(v.Id);
                var act = new Activity { Id = "drowsy", Label = "잠자리로 이동", Priority = 250, Interruptible = false };
                var sp = bed?.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Tag == "sleep");
                if (bed != null) act.Steps.Add(Simulation.GoTo(sp != null ? sp.Approach : new P3(bed.Floor, bed.Rect.CX, bed.Rect.CZ)));
                act.Steps.Add(new ActionStep { Kind = "X_MDoze" });
                v.TalkingTo = null; sim.Assign(v, act);
                S.Log("Drowsy", v.Id, room: v.Room, pos: v.Pos, secret: true);
            }
            // a doze that never reached the bed (stuck on the way): the body gives in wherever it is
            foreach (var v in S.LivingNpcs.OrderBy(x => x.Id).ToList())
            {
                if (!S.Flags.TryGetValue("drowsy:" + v.Id, out var since) || S.Clock - since < 25 || v.Status != ActorStatus.Active) continue;
                if (v.Act == null || v.Act.Id != "drowsy") { var act = new Activity { Id = "drowsy", Label = "잠자리로 이동", Priority = 250, Interruptible = false }; act.Steps.Add(new ActionStep { Kind = "X_MDoze" }); sim.Assign(v, act); }
            }
            // a tripped breaker is reset by the house's relay after a while (the trip time stays on the switchboard)
            foreach (var key in S.Flags.Keys.Where(k => k.StartsWith("breakerreset:")).OrderBy(k => k, StringComparer.Ordinal).ToList())
            {
                if (S.Clock < S.Flags[key]) continue;
                S.Flags.Remove(key); int c = int.Parse(key.Substring(13));
                if (!S.CircuitOn(c)) { sim.SetCircuit(c, true, null); S.Log("BreakerReset", null, data: c.ToString(), secret: true); }
            }
            // a fresh scratch on the back of a hand, wet sleeves after the pool: noticed by whoever looks closely
            if (S.Tick % 30 != 3) return;
            foreach (var x in S.Actors.Values.Where(x => x.Alive && !x.IsButler).OrderBy(x => x.Id).ToList())
            {
                bool scratch = S.Flags.TryGetValue("scratch:" + x.Id, out var sAt) && S.Clock - sAt < 20 * 60;
                bool wet = S.Flags.TryGetValue("wetsleeve:" + x.Id, out var wAt) && S.Clock - wAt < 40;
                if (!scratch && !wet) continue;
                foreach (var o in S.Actors.Values.Where(o => o != x && o.Alive && o.Status == ActorStatus.Active && o.Pose != Pose.Sleep && !o.IsButler && o.Room == x.Room && o.Pos.f == x.Pos.f).OrderBy(o => o.Id).ToList())
                {
                    float dist = o.Pos.DistXZ(x.Pos); if (dist > (wet ? 4f : 2.6f) || sim.RoomLight(x.Room) < 0.3f) continue;
                    float face = Math.Abs(MathX.DeltaAngle(o.Yaw, MathX.AngleDeg(x.Pos.x - o.Pos.x, x.Pos.z - o.Pos.z))); if (face > 70) continue;
                    if (scratch && S.K(o.Id).Facts.Add("saw-scratch:" + x.Id + ":" + (int)(sAt / 60)))
                        Evidences.Add(sim, o.Id, EvKind.Sighting, $"{Cast.GivenOf(x.Id)}의 손등에 난 새 긁힌 자국", $"{ClockFmt.Vague(S.Clock)}, {S.RoomName(x.Room)}에서 {Cast.GivenOf(x.Id)}의 손등에 손톱에 긁힌 듯한 붉은 자국이 보였다.", "직접 목격", "scratch:" + x.Id + ":" + o.Id, S.Clock, S.Clock, x.Room,
                            "그 무렵 손에 새 상처가 있었다", "어떻게 다쳤는지", true, new Prop { Kind = PropKind.Injured, A = x.Id, Room = x.Room, T0 = S.Clock, T1 = S.Clock, Value = "손등 긁힘" });
                    if (wet && S.K(o.Id).Facts.Add("saw-wet:" + x.Id + ":" + (int)(wAt / 60)))
                        Evidences.Add(sim, o.Id, EvKind.Sighting, $"{Cast.GivenOf(x.Id)}의 흠뻑 젖은 소매", $"{ClockFmt.Vague(S.Clock)}, {S.RoomName(x.Room)}에서 {Cast.GivenOf(x.Id)}의 소매와 옷자락이 흠뻑 젖어 있었다.", "직접 목격", "wet:" + x.Id + ":" + o.Id, S.Clock, S.Clock, x.Room,
                            "그 무렵 옷이 젖어 있었다", "어디서 왜 젖었는지", true, new Prop { Kind = PropKind.Wet, A = x.Id, Room = x.Room, T0 = S.Clock, T1 = S.Clock });
                }
            }
            // where the wet footprints from the pool stop (one mark, a few minutes after the drowning)
            foreach (var x in S.Actors.Values.Where(x => x.Alive).OrderBy(x => x.Id).ToList())
            {
                if (!S.Flags.TryGetValue("wettrail:" + x.Id, out var tAt) || S.Clock - tAt < 5) continue;
                S.Flags.Remove("wettrail:" + x.Id);
                var room = S.Layout.Room(x.Room); if (room == null || room.Type == RoomType.Pool) continue;
                var wt = sim.AddTrace("FootprintWet", x.Pos, x.Room, x.Id, null, 0.3f, 1, "수영장 쪽에서 이어진 젖은 발자국이 여기서 끊긴다", "물에서 나온 누군가가 여기까지 걸어왔다", "누구의 발자국인지");
                if (wt != null) wt.Note = "drown";
            }
        }

        /// <summary>Drowning in the pool: the victim is soaked, the one who held them under has wet sleeves and leaves wet footprints.</summary>
        public static void OnDrown(Simulation sim, Actor a, Actor t)
        {
            var S = sim.S; t.Wet = true; t.WetUntil = S.Clock + 9999;
            if (!S.Flags.ContainsKey("drownclothed:" + t.Id)) S.Flags["drownclothed:" + t.Id] = t.Act != null && t.Act.Id == "life:swim" ? 0 : 1;   // pulled in dressed, not swimming
            if (t.Status == ActorStatus.Active && S.R(Stream.Combat).Chance(0.6)) Scratch(sim, t, a);   // clawing at the hands that hold them under
            if (S.Flags.ContainsKey("drownlog:" + a.Id + ":" + t.Id)) return;
            S.Flags["drownlog:" + a.Id + ":" + t.Id] = 1;
            a.Wet = true; a.WetUntil = S.Clock + 40; S.Flags["wetsleeve:" + a.Id] = S.Clock; S.Flags["wettrail:" + a.Id] = S.Clock;
            S.Log("HeldUnder", a.Id, t.Id, room: t.Room, pos: t.Pos, secret: true);
            var wt = sim.AddTrace("FootprintWet", a.Pos, a.Room, a.Id, t.Id, 0.35f, 1, "수영장 가장자리에서 문 쪽으로 이어진 젖은 발자국", "물에서 나온 누군가가 이쪽으로 걸어 나갔다", "누구의 발자국인지");
            if (wt != null) wt.Note = "drown";
        }

        /// <summary>A body carried into the cold store: it cools fast (reads as an earlier death) and frost settles on it.</summary>
        public static void OnColdHide(Simulation sim, Actor carrier, Actor t)
        {
            var S = sim.S; var rng = S.R(Stream.PlanTie);
            double shift = -rng.Range(70, 120); t.Body.TodShift = Math.Min(t.Body.TodShift, shift);
            S.Flags["frost:" + t.Id] = S.Clock;
            sim.AddTrace("Water", t.Pos, t.Room, carrier.Id, t.Id, 0.5f, 1, "시신 밑에 서리가 녹아 고인 물기", "시신이 몹시 차가운 곳에 놓여 있었다", "얼마나 오래 있었는지");
            S.Log("ColdHide", carrier.Id, t.Id, room: t.Room, pos: t.Pos, data: ((int)shift).ToString(), plan: carrier.PlanId, secret: true);
        }
    }
}
