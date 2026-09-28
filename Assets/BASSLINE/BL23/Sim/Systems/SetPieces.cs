using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Signature tricks (the "class-trial" kind): a staged locked room, a shifted time of death, a fake dying message,
    /// a planted wrong weapon. Each is chosen by a planner who knows the means, executed as real steps that can be
    /// seen or interrupted, leaves physical traces, and plants a false first impression the trial has to break.
    /// Nothing here reads hidden truth for anyone: examiners only learn what their own look at the scene gives them.
    /// </summary>
    public static class SetPieces
    {
        /// <summary>Test hook (headless campaigns only): always stage this kind when its means exist, with relaxed trait thresholds.</summary>
        public static string Force;
        public static bool Handles(string kind) => kind == "X_Seal" || kind == "X_Tod" || kind == "X_Message" || kind == "X_Swap" || kind == "X_Dose";

        // ================================================================== planning
        public static void Augment(Simulation sim, MurderPlan plan, Actor a, Rng rng)
        {
            var S = sim.S; var c = a.Def; var k = S.K(a.Id);
            int attack = plan.Steps.FindIndex(s => s.Kind == "Attack" || s.Kind == "Drown");
            if (attack < 0 || plan.Grammar.StartsWith("Press") || plan.Grammar.StartsWith("Trap") || Methods.Staged(plan.Grammar)) return;   // an "accident" or a "natural death" carries no planted message or weapon
            var t = S.A(plan.Target); if (t == null) return;
            bool Visited(RoomType rt) => S.Layout.Rooms.Any(r => r.Type == rt && k.Facts.Contains("visited:" + r.Id));
            var opts = new List<(string kind, double score)>();
            var line = sim.Carried(a).FirstOrDefault(i => i.Type == "Tripwire" || i.Type == "Thread") ?? (Force != null ? S.Items.Values.AsEnumerable() : k.ItemSeen.Keys.Select(S.I)).Where(i => i != null && i.Holder == null && (i.Type == "Tripwire" || i.Type == "Thread")).OrderBy(i => i.Pos.Dist(a.Pos)).FirstOrDefault();
            if (c.Infer >= 68 && line != null && plan.Steps.Any(s => s.Kind == "VisitRoom" || s.Kind == "Stalk" || s.Kind == "GoRoom")) opts.Add(("Seal", (plan.Steps.Any(s => s.Kind == "VisitRoom") ? 1.1 : 0.45) + c.Infer / 200.0));
            if (c.Composure >= 62 && c.Infer >= 62 && plan.Grammar != "Drown" && (Visited(RoomType.WineCellar) || Visited(RoomType.Pool) || S.Layout.Furniture.Any(f => f.Type == "Fireplace"))) opts.Add(("Tod", 0.85 + c.Composure / 250.0));
            if (c.Deceit >= 62 && c.P.Morality < 0.6f && plan.Grammar != "Drown") opts.Add(("Message", 0.75 + c.Deceit / 250.0));
            if (c.Deceit >= 52 && plan.Grammar != "Drown") opts.Add(("Swap", 0.6 + c.Deceit / 300.0));
            if (Force != null)
            {
                opts.Clear();
                if (Force == "Seal" && line != null && plan.Steps.Any(s => s.Kind == "VisitRoom" || s.Kind == "Stalk" || s.Kind == "GoRoom")) opts.Add(("Seal", 9));
                if (Force == "Tod" && plan.Grammar != "Drown") opts.Add(("Tod", 9));
                if (Force == "Message" && plan.Grammar != "Drown") opts.Add(("Message", 9));
                if (Force == "Swap" && plan.Grammar != "Drown") opts.Add(("Swap", 9));
            }
            if (opts.Count == 0) return;
            // the more capable the planner, the more layers — but one clean trick is the norm
            double any = 0.35 + (c.Infer + c.Deceit + c.Composure - 180) / 250.0;
            if (Force == null && !rng.Chance(MathX.Clamp01((float)any))) return;
            int layers = c.Infer + c.Deceit + c.Composure > 225 && rng.Chance(0.4) ? 2 : 1;
            var chosen = new List<string>();
            foreach (var o in opts.OrderByDescending(o => o.score + rng.F() * 0.4))
            {
                if (chosen.Count >= layers) break;
                if (o.kind == "Seal" && chosen.Contains("Tod") || o.kind == "Tod" && chosen.Contains("Seal")) continue;
                chosen.Add(o.kind);
            }
            int after = attack + 1;
            var post = new List<PlanStep>();
            if (chosen.Contains("Message")) post.Add(new PlanStep { Kind = "X_Message", Target = plan.Target });
            if (chosen.Contains("Swap")) post.Add(new PlanStep { Kind = "X_Swap", Target = plan.Target });
            if (chosen.Contains("Tod")) post.Add(new PlanStep { Kind = "X_Tod", Target = plan.Target });
            if (chosen.Contains("Seal"))
            {
                post.Add(new PlanStep { Kind = "X_Seal", Target = plan.Target, Item = line.Id });
                // a sealed room keeps the body where it fell and the key where it belongs
                plan.Steps.RemoveAll(s => s.Kind == "LockRoom" || s.Kind == "MoveBody");
                attack = plan.Steps.FindIndex(s => s.Kind == "Attack" || s.Kind == "Drown"); after = attack + 1;
                int first = plan.Steps.FindIndex(s => s.Kind == "VisitRoom" || s.Kind == "Stalk" || s.Kind == "Invite");
                if (line.Holder != a.Id && first >= 0) { plan.Steps.Insert(first, new PlanStep { Kind = "GetItem", Item = line.Id }); after++; }
            }
            if (chosen.Contains("Tod")) plan.Steps.RemoveAll(s => s.Kind == "MoveBody");
            after = plan.Steps.FindIndex(s => s.Kind == "Attack" || s.Kind == "Drown") + 1;
            plan.Steps.InsertRange(after, post);
            foreach (var ch in chosen) plan.Grammar += "+" + ch;
            S.Dev($"setpiece {a.Id} {string.Join("+", chosen)}");
        }

        // ================================================================== plan step → activity
        public static Activity PlanThink(Simulation sim, Actor a, MurderPlan plan, PlanStep st, Activity act)
        {
            var S = sim.S; var rng = S.R(Stream.PlanTie); var t = S.A(plan.Target);
            if (st.Kind == "X_Dose") return DoseThink(sim, a, plan, st, act);
            if (t == null || t.Status == ActorStatus.Active) { Crime.Advance(sim, a, plan); return null; }
            act.Interruptible = false; act.Label = "볼일";
            switch (st.Kind)
            {
                case "X_Message":
                    {
                        act.Steps.Add(Simulation.GoTo(Near(sim, t.Pos))); act.Steps.Add(new ActionStep { Kind = "SP_Message", Actor = t.Id, Duration = 1.5 });
                        return act;
                    }
                case "X_Swap":
                    {
                        // something heavy and at hand in this room, of a different kind of harm than the real wound
                        var real = t.Body.Wounds.Where(w => !w.Postmortem).OrderByDescending(w => w.Sev).FirstOrDefault();
                        var decoy = S.Items.Values.Where(i => i.Holder == null && i.Room == t.Room && i.Def != null && i.Def.IsWeapon && i.Id != plan.Weapon && i.Def.Dmg != (real?.Type ?? DamageType.None) && !i.Bloody && i.Pos.Dist(t.Pos) < 12)
                            .OrderBy(i => i.Pos.Dist(t.Pos)).FirstOrDefault();
                        if (decoy == null) { Crime.Advance(sim, a, plan); return null; }
                        act.Steps.Add(Simulation.GoTo(decoy.Pos)); act.Steps.Add(new ActionStep { Kind = "SP_TakeDecoy", Item = decoy.Id });
                        act.Steps.Add(Simulation.GoTo(Near(sim, t.Pos))); act.Steps.Add(new ActionStep { Kind = "SP_Plant", Item = decoy.Id, Actor = t.Id, Duration = 1 });
                        return act;
                    }
                case "X_Tod":
                    {
                        // heat where the body lies (a fireplace in this room) or cold nearby (the pool on this floor / the cellar)
                        var here = S.Layout.Room(t.Room); var kk = S.K(a.Id);
                        var fire = here?.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(f => f.Type == "Fireplace")
                                   // or a known fireplace close by on this floor (the body is carried there)
                                   ?? S.Layout.Furniture.Where(f => f.Type == "Fireplace" && f.Pos.f == t.Pos.f && (kk.Facts.Contains("visited:" + f.Room) || Force != null) && sim.RoomUsable(a, S.Layout.Room(f.Room)) && f.Pos.DistXZ(t.Pos) < 24)
                                        .OrderBy(f => f.Pos.DistXZ(t.Pos)).FirstOrDefault();
                        if (fire != null)
                        {
                            act.Steps.Add(Simulation.GoTo(sim.SnapPublic(new P3(fire.Pos.f, fire.Pos.x + (float)Math.Sin(fire.Yaw * Math.PI / 180) * 1.0f, fire.Pos.z + (float)Math.Cos(fire.Yaw * Math.PI / 180) * 1.0f))));
                            act.Steps.Add(new ActionStep { Kind = "SP_Stoke", Furniture = fire.Id, Actor = t.Id, Duration = 4 });
                            act.Steps.Add(Simulation.GoTo(Near(sim, t.Pos))); act.Steps.Add(new ActionStep { Kind = "SP_Grab", Actor = t.Id });
                            act.Steps.Add(Simulation.GoTo(sim.SnapPublic(new P3(fire.Pos.f, fire.Pos.x + (float)Math.Sin(fire.Yaw * Math.PI / 180) * 1.3f, fire.Pos.z + (float)Math.Cos(fire.Yaw * Math.PI / 180) * 1.3f))));
                            act.Steps.Add(new ActionStep { Kind = "SP_Release", Actor = t.Id, Tag = "heat" });
                            return act;
                        }
                        var k = S.K(a.Id);
                        var cold = S.Layout.Rooms.Where(r => (r.Type == RoomType.WineCellar || r.Type == RoomType.Pool || r.Type == RoomType.ColdStorage) && (k.Facts.Contains("visited:" + r.Id) || Force != null) && sim.RoomUsable(a, r))
                            .OrderBy(r => new P3(r.Floor, r.Rect.CX, r.Rect.CZ).Dist(t.Pos) + (r.Floor != t.Pos.f ? 25 : 0) - (r.Type == RoomType.ColdStorage ? 15 : 0)).ThenBy(r => r.Id).FirstOrDefault();   // the cold store is the coldest place in the house
                        if (cold == null || new P3(cold.Floor, cold.Rect.CX, cold.Rect.CZ).Dist(t.Pos) > 45) { Crime.Advance(sim, a, plan); return null; }
                        act.Steps.Add(Simulation.GoTo(Near(sim, t.Pos))); act.Steps.Add(new ActionStep { Kind = "SP_Grab", Actor = t.Id });
                        act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(cold, rng))); act.Steps.Add(new ActionStep { Kind = "SP_Release", Actor = t.Id, Tag = "cold", Duration = 2 });
                        return act;
                    }
                case "X_Seal":
                    {
                        // any room whose every door locks: the other doors are latched from inside, the last one is locked with the line from outside
                        var bed = S.Layout.Room(t.Room);
                        if (bed == null || bed.Doors.Count == 0 || RoomInfo.IsPassage(bed.Type) || bed.Doors.Any(id => !S.Layout.Doors[id].Lockable)) { Crime.Advance(sim, a, plan); return null; }
                        var d = S.Layout.Doors[bed.Doors.OrderBy(id => S.Layout.Doors[id].Pos.Dist(a.Pos)).First()];
                        foreach (var od in bed.Doors.Where(id => id != d.Id)) { var o = S.Layout.Doors[od]; act.Steps.Add(Simulation.GoTo(sim.SnapPublic(o.AlongX ? new P3(o.Pos.f, o.Pos.x, o.Pos.z + (bed.Rect.CZ > o.Pos.z ? 0.8f : -0.8f)) : new P3(o.Pos.f, o.Pos.x + (bed.Rect.CX > o.Pos.x ? 0.8f : -0.8f), o.Pos.z)))); act.Steps.Add(new ActionStep { Kind = "Door", Door = od, Tag = "latch", Data = "안쪽 잠금쇠" }); }
                        var inside = d.AlongX ? new P3(d.Pos.f, d.Pos.x, d.Pos.z + (bed.Rect.CZ > d.Pos.z ? 0.8f : -0.8f)) : new P3(d.Pos.f, d.Pos.x + (bed.Rect.CX > d.Pos.x ? 0.8f : -0.8f), d.Pos.z);
                        act.Steps.Add(Simulation.GoTo(sim.SnapPublic(inside))); act.Steps.Add(new ActionStep { Kind = "SP_Seal", Door = d.Id, Actor = t.Id, Item = st.Item, Duration = 4 });
                        return act;
                    }
            }
            Crime.Advance(sim, a, plan); return null;
        }

        static P3 Near(Simulation sim, P3 p) => sim.SnapPublic(new P3(p.f, p.x + 0.6f, p.z + 0.3f));

        // ================================================================== step execution
        public static bool Exec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S; MurderPlan plan = a.PlanId != null && S.Plans.TryGetValue(a.PlanId, out var pp) ? pp : null; var rng = S.R(Stream.PlanTie);
            switch (st.Kind)
            {
                case "SP_Dose": return DoseExec(sim, a, st, plan, rng);
                case "SP_Message":
                    {
                        a.Speed = 0; a.Pose = Pose.Crouch; a.Anim = Anim.Write;
                        if (S.Clock < a.Act.StepEnd) return true;
                        var t = S.A(st.Actor);
                        if (t != null && t.Status != ActorStatus.Active)
                        {
                            // a scapegoat: someone the culprit resents, or who would be believed — never themselves
                            var scape = S.Living.Where(x => x != a && !x.IsButler).OrderByDescending(x => S.R(a.Id, x.Id).Grudge * 2 - S.R(a.Id, x.Id).Like + (S.K(t.Id).Suspicion.TryGetValue(x.Id, out var sv) ? sv : 0) + rng.F() * 0.3).FirstOrDefault();
                            if (scape != null)
                            {
                                string glyph = Cast.GivenOf(scape.Id).Substring(0, 1);
                                // the victim's writing hand is their dominant hand; the culprit uses the hand they can reach
                                bool vLeft = Cast.LeftHanded(t.Id); string used = rng.Chance(0.7) ? (vLeft ? "R" : "L") : (vLeft ? "L" : "R");
                                var pos = new P3(t.Pos.f, t.Pos.x + (used == "R" ? 0.35f : -0.35f), t.Pos.z + 0.25f);
                                var tr = sim.AddTrace("BloodWriting", pos, t.Room, a.Id, t.Id, 0.35f, 0, $"피로 쓴 글자 「{glyph}」",
                                    $"시신 옆 바닥에 피로 쓴 「{glyph}」 한 글자가 남아 있다", "누가, 언제 썼는지");
                                tr.Note = $"target={scape.Id};hand={used};glyph={glyph}";
                                S.Log("FakeMessage", a.Id, t.Id, room: t.Room, pos: pos, data: scape.Id + "|" + glyph, plan: plan?.Id, secret: true);
                                a.BloodOnClothes = Math.Max(a.BloodOnClothes, 0.15f);
                            }
                        }
                        a.Pose = Pose.Stand; if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
                case "SP_TakeDecoy":
                    {
                        var it = S.I(st.Item);
                        if (it == null || it.Holder != null) { if (plan != null) Crime.Advance(sim, a, plan); sim.Interrupt(a); return true; }
                        sim.PickUp(a, it); sim.NextStepPublic(a); return true;
                    }
                case "SP_Plant":
                    {
                        a.Speed = 0; a.Anim = Anim.PutDown;
                        if (S.Clock < a.Act.StepEnd) return true;
                        var it = S.I(st.Item); var t = S.A(st.Actor);
                        if (it != null && it.Holder == a.Id && t != null)
                        {
                            it.Bloody = true; if (!it.Surface.Contains("blood")) it.Surface.Add("blood"); it.Surface.Add("smeared");
                            sim.DropItem(a, it, new P3(t.Pos.f, t.Pos.x - 0.45f, t.Pos.z + 0.2f));
                            S.Log("PlantWeapon", a.Id, t.Id, it.Id, t.Room, it.Pos, it.Type, plan?.Id, true);
                        }
                        if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
                case "SP_Stoke":
                    {
                        a.Speed = 0; a.Pose = Pose.Crouch; a.Anim = Anim.Use;
                        if (S.Clock < a.Act.StepEnd) return true;
                        var f = S.Layout.Furniture.ElementAtOrDefault(st.Furniture);
                        if (f != null)
                        {
                            if (!f.Marks.Contains("stoked")) f.Marks.Add("stoked");
                            S.Flags["stoked:" + f.Id] = S.Clock;
                            sim.AddTrace("Ash", new P3(f.Pos.f, f.Pos.x, f.Pos.z + 0.2f), f.Room, a.Id, st.Actor, 0.5f, 1, "벽난로 앞에 새로 떨어진 재",
                                "누군가 최근 장작을 잔뜩 넣어 불을 세게 키웠다", "누가, 왜 그랬는지");
                            S.Emit(GameEventType.Furniture, a.Id, text: "stoked", id: f.Id);
                            S.Log("Stoke", a.Id, st.Actor, room: f.Room, data: "fireplace", plan: plan?.Id, secret: true);
                        }
                        a.Pose = Pose.Stand; sim.NextStepPublic(a); return true;
                    }
                case "SP_Grab":
                    {
                        var t = S.A(st.Actor);
                        if (t == null || t.Pos.Dist(a.Pos) > 2.4f || t.Status == ActorStatus.Active) { if (plan != null) Crime.Advance(sim, a, plan); sim.Interrupt(a); return true; }
                        a.Carrying = t.Id; t.CarriedBy = a.Id; a.Anim = Anim.Carry;
                        S.Log("CarryStart", a.Id, t.Id, room: a.Room, pos: a.Pos, secret: true); S.Emit(GameEventType.Carry, a.Id, t.Id, value: 1);
                        sim.AddTrace("DragMark", a.Pos, a.Room, a.Id, t.Id, 0.5f, 1, "무언가를 끈 자국", "무거운 것이 옮겨졌다", "누가 무엇을 끌었는지");
                        var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == t.Id && i.Loop == S.Loop); if (inc != null) inc.BodyMoved = true;
                        sim.NextStepPublic(a); return true;
                    }
                case "SP_Release":
                    {
                        var t = S.A(a.Carrying);
                        if (t != null)
                        {
                            if (S.Clock < a.Act.StepEnd && st.Tag == "cold") { a.Speed = 0; return true; }
                            t.CarriedBy = null; a.Carrying = null; t.Pos = a.Pos; t.Room = a.Room; t.Pose = st.Tag == "heat" ? Pose.LieSide : Pose.LieBack;
                            S.Log("CarryEnd", a.Id, t.Id, room: a.Room, pos: a.Pos, data: "tod-" + st.Tag, secret: true); S.Emit(GameEventType.Carry, a.Id, t.Id, value: 0);
                            // heat keeps the body warm (reads as a later death), cold drains it (reads as earlier)
                            double shift = st.Tag == "heat" ? rng.Range(55, 95) : -rng.Range(60, 110);
                            t.Body.TodShift = shift;
                            var room = S.Layout.Room(a.Room);
                            if (st.Tag == "cold")
                            {
                                if (room?.Type == RoomType.Pool) { t.Wet = true; t.WetUntil = S.Clock + 9999; sim.Sound(SoundKind.Splash, a.Pos, 0.45f, a.Id); }
                                sim.AddTrace("Water", t.Pos, a.Room, a.Id, t.Id, 0.7f, 1, "시신 밑에 고인 차가운 물기", "시신이 한동안 차가운 곳에 놓여 있었다", "얼마나 오래 있었는지");
                            }
                            S.Log("TodShift", a.Id, t.Id, room: a.Room, data: st.Tag + "|" + (int)shift, plan: plan?.Id, secret: true);
                        }
                        if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
                case "SP_Seal":
                    {
                        a.Speed = 0; a.Anim = Anim.Operate;
                        if (S.Clock < a.Act.StepEnd) return true;
                        var d = S.Layout.Doors.ElementAtOrDefault(st.Door); var t = S.A(st.Actor); var line = S.I(st.Item);
                        if (d != null && t != null && line != null && line.Holder == a.Id && d.Lockable)
                        {
                            var bed = S.Layout.Room(d.RoomA == t.Room ? d.RoomA : d.RoomB);
                            var outsideId = d.RoomA == bed.Id ? d.RoomB : d.RoomA; var outside = S.Layout.Room(outsideId);
                            // the victim's own key stays on the body; a line looped round the thumb-turn and pulled through the gap locks it from outside
                            a.Pos = sim.SnapPublic(d.AlongX ? new P3(d.Pos.f, d.Pos.x, d.Pos.z + (outside.Rect.CZ > d.Pos.z ? 0.9f : -0.9f)) : new P3(d.Pos.f, d.Pos.x + (outside.Rect.CX > d.Pos.x ? 0.9f : -0.9f), d.Pos.z)); a.Room = outsideId;
                            d.Open = false; d.Locked = true;
                            S.Emit(GameEventType.Door, a.Id, id: d.Id, text: "lock", value: 1);
                            sim.Sound(SoundKind.Door, d.Pos, 0.15f, a.Id);
                            sim.AddTrace("ThreadFiber", d.Pos, bed.Id, a.Id, t.Id, 0.08f, 2, "문 아래 틈에 걸린 투명한 실 조각",
                                "가는 줄을 문 아래 틈으로 넣어 안쪽 잠금쇠에 걸었던 흔적", "누가, 언제");
                            var inner = d.AlongX ? new P3(d.Pos.f, d.Pos.x + 0.4f, d.Pos.z + (bed.Rect.CZ > d.Pos.z ? 0.08f : -0.08f)) : new P3(d.Pos.f, d.Pos.x + (bed.Rect.CX > d.Pos.x ? 0.08f : -0.08f), d.Pos.z + 0.4f);
                            sim.AddTrace("Scratch", inner, bed.Id, a.Id, t.Id, 0.06f, 2, "안쪽 잠금쇠 손잡이에 난 긁힌 자국", "누군가 손이 아닌 무언가를 걸어 잠금쇠를 돌렸다", "누가, 언제");
                            if (!line.Surface.Contains("cut")) line.Surface.Add("cut");
                            S.Flags["sealed:" + t.Id] = d.Id;
                            S.Log("SealedRoom", a.Id, t.Id, line.Id, bed.Id, d.Pos, "line", plan?.Id, true);
                        }
                        if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
            }
            return false;
        }

        // ================================================================== poison (a dose in food or drink)
        /// <summary>A poison the planner knows where to get: carried, or seen lying somewhere they can go.</summary>
        public static Item PoisonSource(Simulation sim, Actor a)
        {
            var S = sim.S; var k = S.K(a.Id);
            // any poison source: the greenhouse's foxglove extract, the darkroom's chemicals, leaves picked by hand
            var held = sim.Carried(a).FirstOrDefault(i => i.Def?.Tag == "poison" && !i.Surface.Contains("empty"));
            if (held != null) return held;
            return (Force != null && (Force == "Poison" || Force == "Bedtime" || Force.Contains("Note")) ? S.Items.Values.AsEnumerable() : k.ItemSeen.Keys.Select(S.I)).Where(i => i != null && i.Def?.Tag == "poison" && i.Holder == null && !i.Surface.Contains("empty") && sim.RoomUsable(a, S.Layout.Room(i.Room) ?? S.RoomOf(a)))
                     .OrderBy(i => i.Pos.Dist(a.Pos)).ThenBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
        }

        public static void FillPoison(Simulation sim, MurderPlan plan, Actor a)
        {
            var S = sim.S; var vial = PoisonSource(sim, a); if (vial == null) return;
            if (vial.Holder != a.Id) plan.Steps.Add(new PlanStep { Kind = "GetItem", Item = vial.Id });
            plan.Steps.Add(new PlanStep { Kind = "X_Dose", Target = plan.Target, Item = vial.Id });
            plan.Weapon = vial.Id; plan.WeaponType = vial.Type;
            plan.Deadline = S.Clock + 60 * 30;   // it waits for the right meal or tea
        }

        static bool Consuming(Actor t) => t.Act != null && t.Act.Id != null && (t.Act.Id == "life:eat" || t.Act.Id == "life:tea" || t.Act.Id == "life:bar" || t.Act.Id == "life:snack" || t.Act.Id.StartsWith("social:join")) && t.Act.Cur?.Kind == "Activity";
        /// <summary>Is this person eating or drinking right now (a cup within reach)? Used by the dose and the sleeping draught.</summary>
        public static bool IsConsuming(Actor t) => t != null && Consuming(t);

        static Activity DoseThink(Simulation sim, Actor a, MurderPlan plan, PlanStep st, Activity act)
        {
            var S = sim.S; var t = S.A(plan.Target);
            if (t == null || !t.Alive) { Crime.Abort(sim, a, plan, "target gone"); return null; }
            // only while the target is eating or drinking — otherwise life goes on until the next meal or tea
            if (t.Room < 0 || !sim.RoomUsable(a, S.Layout.Room(t.Room))) return null;
            if (!Consuming(t))
            {
                // the target is on the way to a meal or tea: sit down at the same table and wait for the moment
                bool heading = t.Act != null && t.Act.Id != null && (t.Act.Id == "life:eat" || t.Act.Id == "life:tea" || t.Act.Id == "life:bar");
                var room = S.Layout.Room(t.Room);
                bool atTable = room != null && (room.Type == RoomType.Dining || room.Type == RoomType.TeaRoom) && sim.MealTime(out _);
                if (!heading && !atTable) return null;
                var dest = heading && t.Act.Steps.Count > 0 && t.Act.Steps[0].HasTarget ? t.Act.Steps[0].Target : t.Pos;
                act.Interruptible = true; act.Label = "식사"; act.Priority = 25;
                act.Steps.Add(Simulation.GoTo(sim.SnapPublic(new P3(dest.f, dest.x + 1.1f, dest.z + 0.6f))));
                act.Steps.Add(Simulation.Do("eat", 6, Anim.Eat));
                return act;
            }
            S.Dev($"dose-chance {a.Id}->{t.Id} in {S.RoomName(t.Room)}");
            act.Interruptible = false; act.Label = "식사"; act.Priority = 30;
            act.Steps.Add(Simulation.GoTo(sim.SnapPublic(new P3(t.Pos.f, t.Pos.x + 0.9f, t.Pos.z + 0.3f))));
            act.Steps.Add(new ActionStep { Kind = "SP_Dose", Actor = t.Id, Item = st.Item, Duration = 0.8 });
            return act;
        }

        static bool DoseExec(Simulation sim, Actor a, ActionStep st, MurderPlan plan, Rng rng)
        {
            var S = sim.S; a.Speed = 0; a.Anim = Anim.Use;
            if (S.Clock < a.Act.StepEnd) return true;
            var t = S.A(st.Actor); var vial = S.I(st.Item);
            if (t == null || !t.Alive || !Consuming(t) || t.Pos.Dist(a.Pos) > 2.6f || vial == null || vial.Holder != a.Id) { sim.NextStepPublic(a); return true; }   // moment passed: try at the next meal
            // a quick hand over someone's cup; sharp eyes at the table may catch it
            foreach (var x in S.Living.Where(x => x != a && x != t && x.Room == a.Room && x.Pose != Pose.Sleep && x.Pos.Dist(a.Pos) < 5f))
            {
                double p = x.Def.Obs / 100.0 * (1 - a.Def.Deceit / 130.0) * 0.45;
                if (!rng.Chance(p)) continue;
                var kx = S.K(x.Id); kx.Facts.Add($"sawdose:{a.Id}:{t.Id}:{(int)S.Clock}");
                kx.Suspicion[a.Id] = (kx.Suspicion.TryGetValue(a.Id, out var sv) ? sv : 0) + 0.2f;
                S.Log("SawNearCup", x.Id, a.Id, room: a.Room, data: t.Id, secret: true);
            }
            double delay = rng.Range(28, 50);
            t.Body.PoisonBy = a.Id; t.Body.PoisonedAt = S.Clock; t.Body.DeathAt = t.Body.DeathAt < 0 ? S.Clock + delay : Math.Min(t.Body.DeathAt, S.Clock + delay);
            // the cup stays where it was drunk from; the vial is lighter now
            var cup = new Item { Id = S.NewId("it"), Type = "Cup", Pos = t.Pos, Room = t.Room, HomeRoom = t.Room, HomePos = t.Pos }; cup.Surface.Add("residue:bitter");
            S.Items[cup.Id] = cup; S.Emit(GameEventType.ItemMoved, null, data: cup.Id, text: "new", pos: cup.Pos);
            if (!vial.Surface.Contains("used")) vial.Surface.Add("used");
            S.Log("Dose", a.Id, t.Id, vial.Id, t.Room, t.Pos, ((int)delay).ToString(), plan?.Id, true);
            if (plan != null) Crime.Advance(sim, a, plan);
            sim.NextStepPublic(a); return true;
        }

        // ================================================================== discovery & examination
        /// <summary>When the butler opens a room and confirms a body: a sealed room is announced as such (the false first impression).</summary>
        public static void OnConfirm(Simulation sim, Actor body, int room)
        {
            var S = sim.S;
            if (!S.Flags.TryGetValue("sealed:" + body.Id, out var did)) return;
            var d = S.Layout.Doors.ElementAtOrDefault((int)did); if (d == null) return;
            S.Flags.Remove("sealed:" + body.Id);
            // someone other than the butler unlocked it in the meantime: the "sealed" impression is gone
            double sealedAt = S.Ledger.LastOrDefault(e => e.Type == "SealedRoom" && e.Target == body.Id)?.Clock ?? -1;
            if (S.Flags.TryGetValue("sealedat:" + body.Id, out var sAt)) { sealedAt = sAt; S.Flags.Remove("sealedat:" + body.Id); }
            if (d.LockLog.Any(l => l.Contains(" unlock ") && !l.Contains(Cast.Butler)) && S.Ledger.Any(e => e.Type == "Unlock" && e.Data == "door" + d.Id && e.Actor != Cast.Butler && e.Clock > sealedAt)) return;
            var roomObj = S.Layout.Room(room);
            var key = body.Pocket.Select(S.I).FirstOrDefault(i => i?.KeyFor != null && i.Owner == body.Id);
            // the key slid back under the door (Methods): lying on the floor just inside
            var floorKey = key == null ? S.Items.Values.Where(i => i.KeyFor != null && i.Owner == body.Id && i.Holder == null && i.Room == room).OrderBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault() : null;
            bool bedroomKey = roomObj?.Type == RoomType.Bedroom && roomObj.Owner == body.Id && key != null;
            string detail = bedroomKey ? $"방 열쇠는 {Cast.GivenOf(body.Id)}의 주머니 안에 그대로 있었다."
                          : floorKey != null ? $"{Cast.GivenOf(body.Id)}의 방 열쇠는 방 안쪽, 문 바로 앞 바닥에 떨어져 있었다."
                                       : $"{S.RoomName(room)}의 문은 모두 안쪽에서 잠금쇠가 걸려 있었다. 밖에서 열 수 있는 열쇠는 없다.";
            string text = LineBank.Render(LineBank.Raw(LineBank.House, "y_sealed", false, S.R(Stream.Presentation)) ?? "{detail}", new Dictionary<string, string> { { "detail", detail } }, false);
            S.Log("SealedFound", null, body.Id, room: room, data: bedroomKey ? "key-inside" : floorKey != null ? "key-floor" : "latched-inside");
            foreach (var a in S.Actors.Values.Where(x => x.Alive))
            {
                S.K(a.Id).Facts.Add("sealedroom:" + body.Id);
                Evidences.Add(sim, a.Id, EvKind.Announcement, "밀실 상태", text.Replace("|", " "), "발견 당시", "sealed:" + body.Id, S.Clock, S.Clock, room, "문이 잠겨 있었고 열쇠가 안에 있었다", "어떻게 잠겼는지", true,
                    new Prop { Kind = PropKind.DoorLocked, A = body.Id, Room = room, T0 = S.Clock, T1 = S.Clock, Value = "sealed" });
            }
            S.Emit(GameEventType.Announcement, LineBank.House, text: text, key: "y_sealed");
        }

        /// <summary>Extra lines for a body examination (temperature anomaly, the hand that wrote, instant death).</summary>
        public static void BodyNotes(Simulation sim, Actor ex, Actor body, float skill, Rng rng, List<string> lines, List<Prop> props)
        {
            var S = sim.S;
            if (body.Body.PoisonBy != null && body.Body.Wounds.Count == 0)
            {
                lines.Add("· 입술과 손끝이 푸르스름하고 토한 흔적이 있다 — 무언가에 중독된 것으로 보인다");
                props.Add(new Prop { Kind = PropKind.TraceAt, A = body.Id, Room = body.Room, T0 = S.Clock, T1 = S.Clock, Value = "poisoned" });
                if (skill > 0.55f && body.Body.PoisonedAt >= 0)
                {
                    double b0 = body.Body.DeathClock - 55, b1 = body.Body.DeathClock - 25;
                    lines.Add($"· 이런 독이라면, 먹거나 마신 때는 {ClockFmt.VagueRange(b0, b1, S.Clock)}(으)로 짐작된다");
                    props.Add(new Prop { Kind = PropKind.TraceAt, A = body.Id, T0 = b0, T1 = b1, Value = "dose-window" });
                }
            }
            if (Math.Abs(body.Body.TodShift) > 1 && skill + rng.F() * 0.35f > 0.6f)
            {
                bool warm = body.Body.TodShift > 0;
                lines.Add(warm ? "· 방 온도에 비해 피부가 이상하게 따뜻하다 — 숨진 시각이 실제보다 늦게 짐작됐을 수 있다"
                               : "· 몸이 이상하게 차갑고 축축하다 — 차가운 곳에 있었다면 숨진 시각이 실제보다 이르게 짐작됐을 수 있다");
                props.Add(new Prop { Kind = PropKind.TraceAt, A = body.Id, Room = body.Room, T0 = S.Clock, T1 = S.Clock, Value = warm ? "temp-warm" : "temp-cold" });
            }
            var main = body.Body.Wounds.Where(w => !w.Postmortem).OrderByDescending(w => w.Sev).FirstOrDefault();
            bool instant = main != null && ((main.Region == BodyRegion.Neck && main.Type == DamageType.Cut && main.Sev >= 3) || (main.Region == BodyRegion.Head && main.Sev >= 4));
            if (instant && skill > 0.45f) { lines.Add("· 거의 즉사였을 치명상 — 쓰러진 뒤 무언가를 할 시간은 없었을 것이다"); props.Add(new Prop { Kind = PropKind.TraceAt, A = body.Id, Value = "instant-death" }); }
            var msg = S.Traces.FirstOrDefault(t => t.Type == "BloodWriting" && t.Victim == body.Id && !t.Cleaned);
            if (msg != null)
            {
                string hand = Note(msg.Note, "hand");
                bool bloodyFinger = skill + rng.F() * 0.3f > 0.5f;
                if (bloodyFinger) lines.Add($"· {(hand == "L" ? "왼손" : "오른손")} 검지 끝에만 피가 묻어 있다 ({Cast.GivenOf(body.Id)}은(는) {(Cast.LeftHanded(body.Id) ? "왼손잡이" : "오른손잡이")})");
                if (bloodyFinger) props.Add(new Prop { Kind = PropKind.TraceAt, A = body.Id, Value = "writing-hand:" + hand + (Cast.LeftHanded(body.Id) ? ":left-handed" : ":right-handed") });
            }
            Methods.BodyNotes(sim, ex, body, skill, rng, lines, props);
        }

        /// <summary>A trace examined: the dying message names someone (and a careful eye sees how it was written).</summary>
        public static void TraceNotes(Simulation sim, Actor ex, Trace t, List<string> lines, List<Prop> props)
        {
            var S = sim.S;
            if (t.Type == "BloodWriting")
            {
                string target = Note(t.Note, "target"), glyph = Note(t.Note, "glyph");
                props.Add(new Prop { Kind = PropKind.TraceAt, A = t.Victim, B = target, Room = t.Room, T0 = t.Clock, T1 = S.Clock, Value = "bloodwriting:" + glyph });
                float skill = ex.Def.Obs / 100f + (ex.Ability == "StateSense" ? 0.3f : 0);
                if (skill > 0.72f) lines.Add("· 획의 번짐과 방향이 쓰러진 자세에서 쓴 것 같지 않다 — 위에서 내려다보며 쓴 듯하다");
                // most readers take the message at face value: it points at someone
                if (!ex.IsPlayer && target != null && skill < 0.8f) { var k = S.K(ex.Id); k.Suspicion[target] = (k.Suspicion.TryGetValue(target, out var v) ? v : 0) + 0.35f; }
            }
            if (t.Type == "ThreadFiber") props.Add(new Prop { Kind = PropKind.TraceAt, A = t.Victim, Room = t.Room, T0 = t.Clock, T1 = S.Clock, Value = "thread-under-door" });
            Methods.TraceNotes(sim, ex, t, lines, props);
        }

        public static void ItemNotes(Simulation sim, Actor ex, Item it, List<string> lines, List<Prop> props)
        {
            if (it.Surface.Contains("residue:bitter")) { lines.Add("· 잔 바닥에 쓴 풀 냄새가 나는 녹색 잔여물이 말라붙어 있다"); props.Add(new Prop { Kind = PropKind.ItemState, Item = it.Type, Value = "poison-residue", Room = it.Room }); }
            if (it.Type == "PoisonVial" && it.Surface.Contains("used")) { lines.Add("· 병 속 내용물이 눈에 띄게 줄었다 — 최근 누군가 덜어 썼다"); props.Add(new Prop { Kind = PropKind.ItemState, Item = it.Type, Value = "vial-used", Room = it.Room }); }
            if (it.Surface.Contains("smeared")) { lines.Add("· 피가 표면에만 얇게 발려 있다 — 무언가를 내리쳐 묻은 모양이 아니다"); props.Add(new Prop { Kind = PropKind.ItemState, Item = it.Type, Value = "smeared", Room = it.Room }); }
            if ((it.Type == "Tripwire" || it.Type == "Thread") && it.Surface.Contains("cut")) { lines.Add(it.Type == "Thread" ? "· 실 끝이 새로 잘려 있고, 한 번 길게 풀어 쓴 흔적이 있다" : "· 낚싯줄 끝이 새로 잘려 있고 한 뼘쯤 모자란다"); props.Add(new Prop { Kind = PropKind.ItemState, Item = it.Type, Value = "line-cut", Room = it.Room }); }
            Methods.ItemNotes(sim, ex, it, lines, props);
        }

        public static void FurnitureNotes(Simulation sim, Furniture f, ref string desc, List<Prop> props)
        {
            if (f.Type == "Fireplace" && f.Marks.Contains("stoked"))
            {
                desc += ". 장작이 한가득 들어가 있고 불이 지나치게 세다 — 누군가 최근에 크게 키웠다";
                props.Add(new Prop { Kind = PropKind.TraceAt, Room = f.Room, Value = "fire-stoked" });
            }
            Methods.FurnitureNotes(sim, f, ref desc, props);
        }

        static string Note(string note, string key)
        {
            if (note == null) return null;
            foreach (var p in note.Split(';')) { var kv = p.Split('='); if (kv.Length == 2 && kv[0] == key) return kv[1]; }
            return null;
        }

        /// <summary>For the reveal/trial: how this murder was staged, in plain words.</summary>
        public static string Kor(string kind)
        {
            switch (kind)
            {
                case "Seal": return "낚싯줄로 밖에서 잠근 밀실";
                case "Tod": return "시신을 데우거나 식혀 숨진 시각 속이기";
                case "Message": return "가짜 다잉 메시지";
                case "Swap": return "흉기 바꿔치기";
                case "Poison": return "독이 든 잔";
            }
            return null;
        }
    }
}
