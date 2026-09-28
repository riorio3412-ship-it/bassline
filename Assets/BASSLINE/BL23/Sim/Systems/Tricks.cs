using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// BL23 trick grammars layered on the crime planner. Each is built from small real actions with their own traces,
    /// observations, failure modes and independent investigation paths:
    ///  • 등록 장치/함정 — a heavy piece of furniture rigged over a habitual seat, or a line across the top of a stair.
    ///    Fires on whoever physically triggers it (not necessarily the target). Found/disarmed by close inspection.
    ///  • 녹음/소리 오인 — a recorder plays the planner's voice from their own room while they are elsewhere.
    ///  • 모임 안의 사건 — slipping out of a gathering for a short window and coming back.
    ///  • 권능 개입 — Silence, Guise, Blur and Echo used exactly by their public rules (signs and residues remain).
    ///  • 대리 전달(IG10) — a helper unknowingly delivers the luring note, then keeps quiet out of fear.
    ///  • 사후 훼손 — additional postmortem damage that muddies cause/identity but never erases other sources.
    /// None of this describes real-world methods; it is game state (actions → state → traces → knowledge).
    /// </summary>
    public static class Tricks
    {
        public sealed class TrapChoice { public string Kind; public int Furniture = -1, Spot = -1, Stair = -1, Room = -1; public string Tool; public double Score; }

        // ================================================================== planning
        public static TrapChoice ChooseTrap(Simulation sim, Actor a, string target)
        {
            var S = sim.S; var k = S.K(a.Id); var c = a.Def;
            if (c.Infer < 68 || c.Composure < 55) return null;
            // a tool the planner knows about
            var tool = sim.Carried(a).FirstOrDefault(i => i.Type == "Wrench" || i.Type == "Hammer" || i.Type == "Chisel")?.Id
                    ?? k.ItemSeen.Keys.Select(S.I).Where(i => i != null && i.Holder == null && (i.Type == "Wrench" || i.Type == "Hammer" || i.Type == "Chisel")).OrderBy(i => i.Pos.Dist(a.Pos)).Select(i => i.Id).FirstOrDefault();
            TrapChoice best = null;
            if (tool != null)
            {
                // habits: rooms where the planner has seen the target more than once
                var habit = k.Sightings.Where(s => s.Target == target && s.IdConf > 0.5f).GroupBy(s => s.Room).Where(g => g.Count() >= 2).OrderByDescending(g => g.Count()).Select(g => g.Key).Take(3).ToList();
                foreach (var rid in habit)
                {
                    var room = S.Layout.Room(rid); if (room == null || RoomInfo.IsPassage(room.Type) || room.Type == RoomType.GrandHall) continue;
                    foreach (var fid in room.Furniture)
                    {
                        var f = S.Layout.Furniture[fid]; var def = FurnitureCatalog.Get(f.Type);
                        if (def == null || def.H < 1.8f || def.Mass < 50 || f.Damage > 0 || S.Traps.Any(t => t.Furniture == fid)) continue;
                        var sp = room.Spots.Select(i => S.Layout.Spots[i]).Where(s => s.Furniture != fid && (s.Tag == "sit" || s.Tag == "read" || s.Tag == "play" || s.Tag == "rest") && s.Pos.DistXZ(f.Pos) < 1.9f).OrderBy(s => s.Pos.DistXZ(f.Pos)).FirstOrDefault();
                        if (sp == null) continue;
                        double score = 0.75 + c.Infer / 200.0 + habit.Count * 0.05;
                        if (best == null || score > best.Score) best = new TrapChoice { Kind = "Topple", Furniture = fid, Spot = sp.Id, Room = rid, Tool = tool, Score = score };
                    }
                }
            }
            // a line across the top step of the stair nearest the target's bedroom (whoever comes down first...)
            var line = sim.Carried(a).FirstOrDefault(i => i.Type == "Tripwire")?.Id ?? k.ItemSeen.Keys.Select(S.I).Where(i => i != null && i.Holder == null && i.Type == "Tripwire").Select(i => i.Id).FirstOrDefault();
            var bed = S.Layout.BedroomOf(target);
            if (line != null && bed != null && c.P.Morality < 0.45f)
            {
                var st = S.Layout.Stairs.Where(s => Math.Max(s.A.f, s.B.f) == bed.Floor).OrderBy(s => Top(s).DistXZ(new P3(bed.Floor, bed.Rect.CX, bed.Rect.CZ))).FirstOrDefault();
                if (st != null)
                {
                    double score = 0.55 + c.Infer / 250.0;
                    if (best == null || score > best.Score) best = new TrapChoice { Kind = "Tripwire", Stair = st.Id, Room = RoomOfStairTop(S, st), Tool = line, Score = score };
                }
            }
            return best;
        }

        static P3 Top(Stair s) => s.A.f > s.B.f ? s.A : s.B;
        static P3 Bottom(Stair s) => s.A.f > s.B.f ? s.B : s.A;
        static int RoomOfStairTop(GameState S, Stair s) => s.A.f > s.B.f ? s.RoomA : s.RoomB;

        public static void FillTrap(Simulation sim, MurderPlan plan, Actor a, TrapChoice tc)
        {
            var S = sim.S;
            plan.Weapon = tc.Tool; plan.WeaponType = S.I(tc.Tool)?.Type; plan.KillRoom = tc.Room;
            if (S.I(tc.Tool)?.Holder != a.Id) plan.Steps.Add(new PlanStep { Kind = "GetItem", Item = tc.Tool });
            plan.Steps.Add(new PlanStep { Kind = "X_ArmTrap", Room = tc.Room, Furniture = tc.Kind == "Tripwire" ? tc.Stair : tc.Furniture, Note = tc.Kind + ":" + tc.Spot, Target = plan.Target });
            plan.Steps.Add(new PlanStep { Kind = "X_WaitTrap", Target = plan.Target });
            plan.Deadline = S.Clock + 60 * 30;
        }

        /// <summary>Gathering alibi: the planner has accepted an invitation the target is not part of.</summary>
        public static Gathering GatheringFor(Simulation sim, Actor a, string target)
        {
            var S = sim.S; if (a.Def.Deceit < 55) return null;
            return S.Gatherings.FirstOrDefault(g => !g.Done && !g.Cancelled && g.Status.TryGetValue(a.Id, out var st) && (st == "accepted" || st == "host") && g.Cur.Start - S.Clock < 300 && g.Cur.Start > S.Clock - 10
                && !(g.Status.TryGetValue(target, out var ts) && (ts == "accepted" || ts == "attended" || ts == "host")));
        }

        public static void FillGathering(Simulation sim, MurderPlan plan, Actor a, Gathering g)
        {
            plan.Steps.Add(new PlanStep { Kind = "X_WaitGathering", Note = g.Id });
            plan.Steps.Add(new PlanStep { Kind = "X_SlipOut", Note = g.Id });
            plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = plan.Target, Note = "alone" });
            plan.Steps.Add(new PlanStep { Kind = "Attack", Target = plan.Target });
            plan.Steps.Add(new PlanStep { Kind = "X_Rejoin", Note = g.Id });
            plan.Deadline = g.Cur.End;
            plan.AlibiRoom = g.Cur.Room;
        }

        /// <summary>Optional layers on an already-built plan: recorder alibi, powers, courier, postmortem damage.</summary>
        public static void Augment(Simulation sim, MurderPlan plan, Actor a, Rng rng)
        {
            var S = sim.S; var c = a.Def; var k = S.K(a.Id);
            int firstApproach = plan.Steps.FindIndex(s => s.Kind == "Stalk" || s.Kind == "Invite" || s.Kind == "VisitRoom" || s.Kind == "X_ArmTrap");
            int attack = plan.Steps.FindIndex(s => s.Kind == "Attack" || s.Kind == "KnockOut" || s.Kind == "Drown");
            var bed = S.Layout.BedroomOf(a.Id);
            // 녹음 알리바이: the planner's voice plays in their own room during the act
            if (plan.Grammar != "Gathering" && plan.Grammar != "NightVisit" && plan.Grammar != "Trap" && !Methods.Remote(plan.Grammar) && c.Deceit >= 55 && bed != null && firstApproach >= 0 && rng.Chance(0.5))
            {
                var rec = sim.Carried(a).FirstOrDefault(i => i.Type == "Recorder") ?? S.Items.Values.FirstOrDefault(i => i.Type == "Recorder" && i.Owner == a.Id && i.Holder == null)
                       ?? k.ItemSeen.Keys.Select(S.I).Where(i => i != null && i.Type == "Recorder" && i.Holder == null).OrderBy(i => i.Owner == null ? 0 : 1).ThenBy(i => i.Pos.Dist(a.Pos)).FirstOrDefault();
                if (rec != null)
                {
                    var ins = new List<PlanStep>();
                    if (rec.Holder != a.Id) ins.Add(new PlanStep { Kind = "GetItem", Item = rec.Id });
                    ins.Add(new PlanStep { Kind = "X_Record", Item = rec.Id, Room = bed.Id });
                    plan.Steps.InsertRange(firstApproach, ins);
                    int alibi = plan.Steps.FindIndex(s => s.Kind == "Alibi"); if (alibi >= 0) plan.Steps[alibi] = new PlanStep { Kind = "X_ReturnRoom", Item = rec.Id, Room = bed.Id };
                    plan.AlibiRoom = bed.Id; plan.Grammar += "+Recorder";
                    firstApproach += ins.Count; attack += ins.Count;
                }
            }
            // 권능 개입 — only the power this person holds this loop, only by its public rule
            var ab = a.Ability; bool charged = a.AbilityCharges > 0;
            if (charged && attack >= 0)
            {
                if (ab == "Silence") { plan.Steps.Insert(attack, new PlanStep { Kind = "X_Power", Note = "Silence" }); plan.Grammar += "+Silence"; attack++; }
                else if (ab == "Guise" && firstApproach >= 0)
                {
                    var scape = S.Living.Where(x => x != a && x.Id != plan.Target && !x.IsButler && !x.IsPlayer).OrderByDescending(x => S.R(a.Id, x.Id).Grudge - S.R(a.Id, x.Id).Like + (Math.Abs(x.Def.HeightCm - a.Def.HeightCm) < 6 ? 0.5 : 0)).FirstOrDefault();
                    if (scape != null) { plan.Steps.Insert(attack, new PlanStep { Kind = "X_Power", Note = "Guise", Target = scape.Id }); plan.Grammar += "+Guise"; attack++; }
                }
                else if (ab == "Blur") { plan.Steps.Insert(attack + 1, new PlanStep { Kind = "X_Power", Note = "Blur" }); plan.Grammar += "+Blur"; }
                else if (ab == "Fix") { plan.Steps.Insert(attack + 1, new PlanStep { Kind = "X_Power", Note = "Fix" }); plan.Grammar += "+Fix"; }
                else if (ab == "Weight" && plan.Steps.Any(s => s.Kind == "MoveBody")) { plan.Steps.Insert(plan.Steps.FindIndex(s => s.Kind == "MoveBody"), new PlanStep { Kind = "X_Power", Note = "Weight" }); plan.Grammar += "+Weight"; }
                else if (ab == "Echo" && bed != null && firstApproach >= 0)
                {
                    plan.Steps.Insert(firstApproach, new PlanStep { Kind = "X_EchoRecord", Room = bed.Id }); attack++;
                    plan.Steps.Insert(attack, new PlanStep { Kind = "X_Power", Note = "EchoPlay" }); attack++;
                    plan.AlibiRoom = bed.Id; plan.Grammar += "+Echo";
                }
            }
            // 대리 전달 (IG10): someone else carries the luring note
            int inv = plan.Steps.FindIndex(s => s.Kind == "Invite");
            if (inv >= 0 && c.Deceit >= 58 && rng.Chance(0.6))
            {
                var t = S.A(plan.Target);
                var helper = S.LivingNpcs.Where(x => x != a && x != t && x.Status == ActorStatus.Active && S.R(x.Id, a.Id).Trust + S.R(x.Id, a.Id).Like > 0.15f && S.R(t.Id, x.Id).Opinion > -0.05 && x.PlanId == null).OrderByDescending(x => S.R(x.Id, a.Id).Trust + S.R(t.Id, x.Id).Opinion).FirstOrDefault();
                if (helper != null) { plan.Steps[inv] = new PlanStep { Kind = "X_Courier", Target = plan.Target, Room = plan.Steps[inv].Room, Note = helper.Id }; plan.Accomplice = null; plan.Grammar += "+Courier"; }
            }
            // 사후 훼손: cold grudges only (never on a scene staged as an accident or a natural death)
            if (attack >= 0 && !Methods.Staged(plan.Grammar) && ((plan.Motive == "grudge" || plan.Motive == "jealousy") && c.P.Morality < 0.35f && rng.Chance(0.4) || c.Deceit >= 80 && c.Composure >= 75 && c.P.Morality < 0.5f && rng.Chance(0.2))) plan.Steps.Insert(plan.Steps.FindIndex(s => s.Kind == "Attack" || s.Kind == "KnockOut" || s.Kind == "Drown") + 1, new PlanStep { Kind = "X_Mutilate", Target = plan.Target });
        }

        // ================================================================== plan step → activity
        public static Activity PlanThink(Simulation sim, Actor a, MurderPlan plan, PlanStep st, Activity act)
        {
            var S = sim.S; var rng = S.R(Stream.PlanTie); var t = S.A(plan.Target);
            switch (st.Kind)
            {
                case "X_ArmTrap":
                    {
                        var kind = st.Note.Split(':')[0];
                        if (kind == "Topple")
                        {
                            var f = S.Layout.Furniture.ElementAtOrDefault(st.Furniture); var room = S.Layout.Room(st.Room);
                            if (f != null && room != null && S.IsNight && RoomInfo.NightLocked(room.Type)) return null;   // locked for the night: set it at first light (the deadline still applies)
                            if (f == null || room == null || !sim.RoomUsable(a, room)) { Crime.Abort(sim, a, plan, "함정을 놓을 곳에 갈 수 없어서"); return null; }
                            var p = sim.SnapPublic(new P3(f.Pos.f, f.Pos.x + (room.Rect.CX - f.Pos.x) * 0.25f, f.Pos.z + (room.Rect.CZ - f.Pos.z) * 0.25f));
                            act.Steps.Add(Simulation.GoTo(p)); act.Steps.Add(new ActionStep { Kind = "X_Arm", Furniture = st.Furniture, Tag = st.Note, Actor = plan.Target, Duration = 6 });
                        }
                        else if (kind == "Shock")
                        {
                            // 감전 함정: at the machine's own use-spot (strip the cable behind it, wet the floor in front)
                            var f = S.Layout.Furniture.ElementAtOrDefault(st.Furniture); var room = S.Layout.Room(st.Room);
                            if (f != null && room != null && S.IsNight && RoomInfo.NightLocked(room.Type)) return null;   // locked for the night: set it at first light (the deadline still applies)
                            if (f == null || room == null || !sim.RoomUsable(a, room)) { Crime.Abort(sim, a, plan, "함정을 놓을 곳에 갈 수 없어서"); return null; }
                            var sp = int.TryParse(st.Note.Split(':').ElementAtOrDefault(1), out var spi) ? S.Layout.Spots.ElementAtOrDefault(spi) : null;
                            act.Steps.Add(Simulation.GoTo(sim.SnapPublic(sp != null ? sp.Approach : new P3(f.Pos.f, f.Pos.x + (room.Rect.CX - f.Pos.x) * 0.25f, f.Pos.z + (room.Rect.CZ - f.Pos.z) * 0.25f))));
                            act.Steps.Add(new ActionStep { Kind = "X_Arm", Furniture = st.Furniture, Tag = st.Note, Actor = plan.Target, Duration = 5 });
                        }
                        else
                        {
                            var stair = S.Layout.Stairs.ElementAtOrDefault(st.Furniture); if (stair == null) { Crime.Abort(sim, a, plan, "쓸 만한 계단이 없어서"); return null; }
                            act.Steps.Add(Simulation.GoTo(sim.SnapPublic(Top(stair)))); act.Steps.Add(new ActionStep { Kind = "X_Arm", Furniture = st.Furniture, Tag = st.Note, Actor = plan.Target, Duration = 3 });
                        }
                        act.Label = "볼일"; act.Interruptible = false; return act;
                    }
                case "X_WaitTrap":
                    {
                        var trap = S.Traps.FirstOrDefault(x => x.Plan == plan.Id);
                        if (trap == null) { Crime.Abort(sim, a, plan, "설치한 함정이 사라져서"); return null; }
                        if (!trap.Active && trap.FiredAt >= 0) { Crime.Advance(sim, a, plan); return null; }
                        if (!trap.Active) { Crime.Abort(sim, a, plan, "누가 함정을 풀어 버려서"); return null; }
                        return null; // live normally; the trap does the waiting
                    }
                case "X_WaitGathering":
                    {
                        var g = S.Gatherings.FirstOrDefault(x => x.Id == st.Note);
                        if (g == null || g.Cancelled || g.Done || S.Clock > g.Cur.End - 20) { Crime.Replan(sim, a, plan, "모임이 취소돼서"); Crime.SwitchToStalkPublic(plan); plan.Steps.RemoveAll(s => s.Kind == "X_SlipOut" || s.Kind == "X_Rejoin" || s.Kind == "X_WaitGathering"); plan.Step = Math.Min(plan.Step, plan.Steps.Count - 1); return null; }
                        if (g.Arrived.TryGetValue(a.Id, out var arr) && S.Clock - arr >= 12 && a.Room == g.Revs[g.KnownRev.TryGetValue(a.Id, out var kr) && kr >= 0 ? kr : g.Rev].Room) { Crime.Advance(sim, a, plan); return null; }
                        return null; // attending happens through normal life (LifeCandidates)
                    }
                case "X_SlipOut":
                    act.Steps.Add(new ActionStep { Kind = "X_Excuse", Data = st.Note }); act.Label = "잠깐 볼일"; act.Interruptible = false; return act;
                case "X_Rejoin":
                    {
                        var g = S.Gatherings.FirstOrDefault(x => x.Id == st.Note);
                        if (g == null || g.Done || g.Cancelled) { Crime.Advance(sim, a, plan); return null; }
                        var room = S.Layout.Room(g.Cur.Room);
                        act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(room, rng))); act.Steps.Add(new ActionStep { Kind = "X_Back", Data = g.Id });
                        act.Label = "모임 복귀"; return act;
                    }
                case "X_Record":
                    {
                        var bed = S.Layout.Room(st.Room); var rec = S.I(st.Item);
                        if (bed == null || rec == null || rec.Holder != a.Id) { Crime.Advance(sim, a, plan); return null; }
                        if (bed.Doors.Count > 0)
                        {
                            var dd = S.Layout.Doors[bed.Doors[0]];
                            act.Steps.Add(Simulation.GoTo(sim.SnapPublic(dd.AlongX ? new P3(dd.Pos.f, dd.Pos.x, dd.Pos.z + (bed.Rect.CZ > dd.Pos.z ? 0.8f : -0.8f)) : new P3(dd.Pos.f, dd.Pos.x + (bed.Rect.CX > dd.Pos.x ? 0.8f : -0.8f), dd.Pos.z))));
                            act.Steps.Add(new ActionStep { Kind = "Door", Door = dd.Id, Tag = "close" });
                        }
                        act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(bed, rng)));
                        act.Steps.Add(new ActionStep { Kind = "X_RecordSelf", Item = rec.Id, Duration = 3 });
                        act.Label = "방에서 휴식"; act.Interruptible = false; return act;
                    }
                case "X_ReturnRoom":
                    {
                        var bed = S.Layout.Room(st.Room); if (bed == null) { Crime.Advance(sim, a, plan); return null; }
                        act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(bed, rng)));
                        act.Steps.Add(new ActionStep { Kind = "X_Retrieve", Item = st.Item });
                        act.Label = "방으로 이동"; return act;
                    }
                case "X_EchoRecord":
                    {
                        var bed = S.Layout.Room(st.Room); if (bed == null) { Crime.Advance(sim, a, plan); return null; }
                        act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(bed, rng))); act.Steps.Add(new ActionStep { Kind = "X_EchoRec" });
                        act.Label = "방에서 휴식"; return act;
                    }
                case "X_Power":
                    act.Steps.Add(new ActionStep { Kind = "X_UsePower", Tag = st.Note, Actor = st.Target }); act.Interruptible = false; return act;
                case "X_Courier":
                    {
                        var helper = S.A(st.Note); if (helper == null || !helper.Alive) { st.Kind = "Invite"; return null; }
                        if (helper.Pose == Pose.Sleep || S.IsNight) return null;   // ask in the morning
                        act.Steps.Add(new ActionStep { Kind = "Talk", Actor = helper.Id });
                        act.Steps.Add(new ActionStep { Kind = "X_GiveNote", Actor = helper.Id, Tag = plan.Target, Room = st.Room });
                        act.Label = "심부름 부탁"; return act;
                    }
                case "X_Mutilate":
                    {
                        if (t == null || t.Alive) { Crime.Advance(sim, a, plan); return null; }
                        act.Steps.Add(Simulation.GoTo(t.Pos)); act.Steps.Add(new ActionStep { Kind = "X_Deface", Actor = t.Id });
                        act.Interruptible = false; return act;
                    }
            }
            if (Methods.Handles(st.Kind)) return Methods.PlanThink(sim, a, plan, st, act);
            if (SetPieces.Handles(st.Kind)) return SetPieces.PlanThink(sim, a, plan, st, act);
            Crime.Advance(sim, a, plan); return null;
        }

        // ================================================================== step execution
        public static bool Exec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S; MurderPlan plan = a.PlanId != null && S.Plans.TryGetValue(a.PlanId, out var pp) ? pp : null; var rng = S.R(Stream.PlanTie);
            if (Methods.Exec(sim, a, st)) return true;   // second-wave action steps (X_M…)
            switch (st.Kind)
            {
                case "X_Arm":
                    {
                        a.Speed = 0; a.Anim = Anim.Operate;
                        // needs privacy: anyone awake in the room postpones the work
                        bool watched = S.Living.Any(x => x != a && x.Room == a.Room && x.Pose != Pose.Sleep && x.Pos.Dist(a.Pos) < 12);
                        if (watched) { if (S.Clock - a.Act.StepStart > 30) { if (plan != null) Crime.Replan(sim, a, plan, "사람들이 자리를 뜨지 않아서"); sim.Interrupt(a, 8); } else a.Act.StepEnd = S.Clock + st.Duration; return true; }
                        if (S.Clock < a.Act.StepEnd) return true;
                        var t = S.A(st.Actor);
                        if (plan != null && t != null && !Crime.Reserve(sim, a, t, plan)) { Crime.Abort(sim, a, plan, "때가 맞지 않아서"); return true; }
                        var parts = st.Tag.Split(':'); var trap = new Trap { Id = S.NewId("trap"), Kind = parts[0], Owner = a.Id, Plan = plan?.Id, Target = st.Actor, ArmedAt = S.Clock, Active = true, Room = a.Room };
                        if (trap.Kind == "Topple")
                        {
                            trap.Furniture = st.Furniture; trap.Spot = int.Parse(parts[1]); var f = S.Layout.Furniture[st.Furniture];
                            f.Marks.Add("받침 고정부에 새로 긁힌 자국이 있다");
                            sim.AddTrace("Scratch", f.Pos, f.Room, a.Id, st.Actor, 0.25f, 2, "가구 받침에 새로 긁힌 자국", "최근 누군가 이 가구의 받침과 고정부를 건드렸다", "누가, 언제, 왜 그랬는지");
                        }
                        else if (Methods.ArmTrap(sim, a, trap, st)) { }   // 감전 함정 (Methods)
                        else
                        {
                            trap.Stair = st.Furniture; var stair = S.Layout.Stairs[st.Furniture]; trap.TopFloor = Top(stair).f;
                            var line = sim.Carried(a).FirstOrDefault(i => i.Type == "Tripwire");
                            if (line != null) { sim.DropItem(a, line, Top(stair), true); line.Surface.Add("팽팽하게 묶였던 자국"); }
                        }
                        S.Traps.Add(trap);
                        S.Log("TrapArmed", a.Id, st.Actor, room: a.Room, pos: a.Pos, data: $"{trap.Id} {trap.Kind}", plan: plan?.Id, secret: true);
                        if (plan != null) { plan.Stage = "Armed"; plan.Log.Add($"{ClockFmt.Vague(S.Clock)} 함정 설치: {trap.Kind} ({S.RoomName(a.Room)})"); Crime.Advance(sim, a, plan); }
                        sim.NextStepPublic(a); return true;
                    }
                case "X_Excuse":
                    {
                        var g = S.Gatherings.FirstOrDefault(x => x.Id == st.Data);
                        sim.Speak(a, "gathering_leave", null, new Dictionary<string, string> { { "act", g?.Label ?? "모임" } });
                        if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
                case "X_Back":
                    {
                        var g = S.Gatherings.FirstOrDefault(x => x.Id == st.Data);
                        sim.Speak(a, "gathering_back", null, new Dictionary<string, string> { { "act", g?.Label ?? "모임" } });
                        if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
                case "X_RecordSelf":
                    {
                        a.Speed = 0; a.Anim = Anim.Talk;
                        if (S.Clock < a.Act.StepEnd) return true;
                        var rec = S.I(st.Item);
                        if (rec != null && rec.Holder == a.Id)
                        {
                            rec.RecVoice = a.Id; rec.RecAt = S.Clock; rec.RecText = sim.Render(a.Id, null, "rehearse") ?? "…";
                            // leave it on a shelf in the room with a delayed playback set to the expected time of the act
                            sim.DropItem(a, rec, a.Pos, true);
                            rec.PlayAt = S.Clock + rng.Range(22, 40); rec.ArmedBy = a.Id; rec.PlayedAt = -1;
                            S.Log("RecorderArmed", a.Id, item: rec.Id, room: a.Room, data: $"rec@{ClockFmt.Vague(rec.RecAt)} play@{ClockFmt.Vague(rec.PlayAt)}", plan: plan?.Id, secret: true);
                        }
                        if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
                case "X_Retrieve":
                    {
                        var rec = S.I(st.Item);
                        if (rec != null && rec.Holder == null && rec.Room == a.Room && rec.PlayAt < 0 && a.Def.Composure >= 70) { rec.Holder = a.Id; a.Pocket.Add(rec.Id); rec.Room = -1; S.Log("PickUp", a.Id, item: rec.Id, room: a.Room, data: "Recorder retrieve", secret: true); S.Emit(GameEventType.ItemMoved, a.Id, data: rec.Id, text: "pickup"); }
                        if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
                case "X_EchoRec":
                    {
                        // Echo: remember a short sound made here (the planner hums/talks in their room)
                        sim.Speak(a, "rehearse", null);
                        S.Flags["echo:" + a.Id] = S.Clock; S.Flags["echopos:" + a.Id] = a.Room;
                        S.Flags["echox:" + a.Id] = a.Pos.x; S.Flags["echoz:" + a.Id] = a.Pos.z; S.Flags["echof:" + a.Id] = a.Pos.f;
                        S.Log("EchoRecord", a.Id, room: a.Room, pos: a.Pos, plan: plan?.Id, secret: true);
                        if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
                case "X_UsePower":
                    {
                        string p = st.Tag;
                        if (p == "EchoPlay")
                        {
                            if (Abilities.Use(sim, a) && S.Flags.TryGetValue("echox:" + a.Id, out var ex))
                            {
                                var pos = new P3((int)S.Flags["echof:" + a.Id], (float)ex, (float)S.Flags["echoz:" + a.Id]);
                                sim.Sound(SoundKind.Talk, pos, 0.45f, null, a.Id);
                                sim.AddTrace("PowerResidue", pos, S.Layout.RoomAt(pos), a.Id, null, 0.4f, 3, "쇠붙이가 울리는 듯한 희미한 여운", "이곳에서 담아 둔 소리가 다시 울렸다 — 권능 '메아리'의 흔적", "원래 그 소리를 낸 사람이 누구인지");
                                S.Log("EchoPlay", a.Id, room: S.Layout.RoomAt(pos), pos: pos, data: "recorded@" + ClockFmt.Vague(S.Flags["echo:" + a.Id]), plan: plan?.Id, secret: true);
                            }
                        }
                        else if (p == "Fix")
                        {
                            // 고정: hold the scene's door shut for a while (delays discovery; frost marks stay on the frame)
                            var room = S.Layout.Room(a.Room); var door = room?.Doors.Select(d => S.Layout.Doors[d]).OrderBy(d => d.Pos.DistXZ(a.Pos)).FirstOrDefault();
                            if (door != null && door.Pos.DistXZ(a.Pos) < 6f && Abilities.Use(sim, a, null, door.Id)) { sim.SetDoor(a, door, false, null, "권능 고정"); sim.AddTrace("PowerResidue", door.Pos, a.Room, a.Id, null, 0.4f, 2, "문틀에 낀 서리 무늬", "누군가 권능으로 이 문을 붙들어 둔 적이 있다", "누가 그랬는지"); }
                        }
                        else Abilities.Use(sim, a, st.Actor);
                        if (p == "Guise" && st.Actor != null) S.Log("GuiseAs", a.Id, st.Actor, room: a.Room, plan: plan?.Id, secret: true);
                        if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
                case "X_GiveNote":
                    {
                        var helper = S.A(st.Actor); var t = S.A(st.Tag); var room = S.Layout.Room(st.Room);
                        if (helper == null || t == null || room == null || a.Pos.Dist(helper.Pos) > 3f) { if (plan != null) { var ps = plan.Steps[plan.Step]; ps.Kind = "Invite"; } sim.Interrupt(a, 1); return true; }
                        double meetAt = S.Clock + 25 + rng.Range(0, 15);
                        var note = new Item { Id = S.NewId("it"), Type = "Invitation", Name = "접힌 쪽지", Owner = t.Id, Holder = helper.Id, Note = $"{ClockFmt.Vague(meetAt)}에 {room.Name}에서 기다릴게. 할 이야기가 있어.", NoteFrom = "courier:" + a.Id + ":" + room.Id + ":" + (int)meetAt };
                        S.Items[note.Id] = note; helper.Pocket.Add(note.Id);
                        sim.Speak(a, "courier_ask", helper.Id, new Dictionary<string, string> { { "t", "@" + t.Id } });
                        S.Log("CourierAsk", a.Id, helper.Id, item: note.Id, room: a.Room, data: t.Id, plan: plan?.Id, secret: true);
                        S.K(helper.Id).Facts.Add($"courier:{a.Id}:{t.Id}:{(int)S.Clock}");
                        var dv = new Activity { Id = "g:deliver:" + note.Id, Label = "쪽지 전달", Priority = 2.6 };
                        dv.Steps.Add(new ActionStep { Kind = "Talk", Actor = t.Id }); dv.Steps.Add(new ActionStep { Kind = "X_Hand", Item = note.Id, Actor = t.Id, Tag = "courier" });
                        sim.Assign(helper, dv);
                        S.Deliveries.Add($"{a.Id}|{helper.Id}|{t.Id}|{S.Clock:0}|{note.Id}");
                        if (plan != null) { plan.KillRoom = room.Id; Crime.Advance(sim, a, plan); }
                        sim.NextStepPublic(a); return true;
                    }
                case "X_Deface":
                    {
                        var t = S.A(st.Actor);
                        if (t != null && !t.Alive && t.Pos.Dist(a.Pos) < 2.2f)
                        {
                            var w = sim.Held(a, d => d != null && d.IsWeapon); var dt = w?.Def?.Dmg ?? DamageType.Blunt;
                            sim.Strike(a.Id, t, BodyRegion.Head, dt == DamageType.None ? DamageType.Blunt : dt, 3, w?.Id, "postmortem");
                            sim.Strike(a.Id, t, BodyRegion.HandR, dt == DamageType.None ? DamageType.Blunt : dt, 2, w?.Id, "postmortem");
                            a.Anim = Anim.Overhead;
                        }
                        if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
                case "X_Disarm":
                    {
                        a.Speed = 0; a.Anim = Anim.Operate;
                        if (S.Clock < a.Act.StepEnd) return true;
                        var trap = S.Traps.FirstOrDefault(x => x.Id == st.Data);
                        if (trap != null && trap.Active) Disarm(sim, trap, a.Id, trap.Owner == a.Id ? "owner" : "found");
                        sim.NextStepPublic(a); return true;
                    }
            }
            sim.NextStepPublic(a); return true;
        }

        // ================================================================== ticking devices
        public static void Tick(Simulation sim)
        {
            var S = sim.S;
            if (S.Traps.Count > 0 && S.Tick % 3 == 0) foreach (var t in S.Traps.Where(x => x.Active).ToList()) TrapCheck(sim, t);
            if (S.Tick % 50 == 25 && S.Traps.Any(x => x.Active)) TrapDiscovery(sim);
            if (S.Tick % 5 == 0) foreach (var it in S.Items.Values.Where(i => i.PlayAt >= 0 && S.Clock >= i.PlayAt).ToList()) Playback(sim, it);
            Methods.Tick(sim);   // sleeping draughts, breaker relays, scratches and wet sleeves noticed
        }

        static void TrapCheck(Simulation sim, Trap t)
        {
            var S = sim.S;
            if (Methods.TrapCheck(sim, t)) return;   // 감전 함정 · 시간차 독
            if (t.Kind == "Topple")
            {
                var sp = S.Layout.Spots.ElementAtOrDefault(t.Spot); if (sp == null) { t.Active = false; return; }
                var v = S.Actors.Values.FirstOrDefault(x => x.Id != t.Owner && x.Status == ActorStatus.Active && x.CarriedBy == null && x.Pos.f == sp.Pos.f && (x.Spot == t.Spot || (x.Pos.DistXZ(sp.Pos) < 0.5f && (x.Pose == Pose.Sit || x.IsPlayer))));
                if (v != null) Fire(sim, t, v);
            }
            else
            {
                var st = S.Layout.Stairs.ElementAtOrDefault(t.Stair); if (st == null) { t.Active = false; return; }
                var top = Top(st);
                var v = S.Actors.Values.FirstOrDefault(x => x.Id != t.Owner && x.Status == ActorStatus.Active && x.CarriedBy == null &&
                    ((x.StairId == t.Stair && x.StairFrom.f == top.f && x.StairUntil < 0.35) || (x.IsPlayer && x.Pos.f == top.f && x.Pos.DistXZ(top) < 0.7f)));
                if (v != null) Fire(sim, t, v);
            }
        }

        static void Fire(Simulation sim, Trap t, Actor v)
        {
            var S = sim.S; var rng = S.R(Stream.Combat);
            t.Active = false; t.FiredAt = S.Clock; t.Victim = v.Id;
            S.Log("TrapFired", t.Owner, v.Id, room: v.Room, pos: v.Pos, data: $"{t.Id} {t.Kind}", plan: t.Plan, secret: true);
            if (t.Kind == "Topple")
            {
                var f = S.Layout.Furniture[t.Furniture]; var sp = S.Layout.Spots[t.Spot];
                f.Origin = f.Pos; f.Moved = true; f.Damage = 3; f.Pos = new P3(f.Pos.f, (f.Pos.x + sp.Pos.x) / 2, (f.Pos.z + sp.Pos.z) / 2);
                S.Emit(GameEventType.Furniture, t.Owner, v.Id, id: f.Id, pos: f.Pos, data: "topple");
                sim.Sound(SoundKind.Crash, f.Pos, 1f, null);
                sim.Strike(t.Owner, v, v.Pose == Pose.Sit ? BodyRegion.Head : BodyRegion.Chest, DamageType.Crush, 3 + (rng.Chance(0.5) ? 1 : 0), null, "trap:" + t.Id);
                if (v.Alive && v.Status == ActorStatus.Active) sim.Strike(t.Owner, v, BodyRegion.ShoulderL, DamageType.Crush, 2, null, "trap:" + t.Id);
                sim.AddTrace("Debris", f.Pos, f.Room, t.Owner, v.Id, 1.2f, 0, "쓰러진 가구와 흩어진 파편", "무거운 가구가 넘어졌다", "스스로 넘어졌는지, 누군가 손을 댔는지");
            }
            else
            {
                var st = S.Layout.Stairs[t.Stair]; var bottom = Bottom(st);
                if (v.StairId >= 0) { v.StairId = -1; v.StairUntil = 0; }
                v.Pos = bottom; v.Room = S.Layout.RoomAt(bottom); if (v.Act != null) sim.Interrupt(v, 2);
                sim.Sound(SoundKind.Fall, bottom, 0.9f, null);
                sim.Strike(t.Owner, v, BodyRegion.Head, DamageType.Blunt, 3, null, "trap:" + t.Id);
                sim.Strike(t.Owner, v, rng.Chance(0.5) ? BodyRegion.LegL : BodyRegion.FootR, DamageType.Blunt, 2, null, "trap:" + t.Id);
                var line = S.Items.Values.FirstOrDefault(i => i.Type == "Tripwire" && i.Holder == null && i.Pos.f == Top(st).f && i.Pos.DistXZ(Top(st)) < 1.5f);
                if (line != null) { line.Damage = 2; line.Surface.Add("끊어짐"); S.Emit(GameEventType.ItemState, null, data: line.Id, text: "broken"); }
                sim.AddTrace("Scuff", Top(st), RoomOfStairTop(S, st), t.Owner, v.Id, 0.4f, 1, "계단 맨 윗단에 발이 미끄러진 자국", "누군가 이곳에서 발을 헛디뎠다", "혼자 넘어졌는지, 누가 그렇게 만들었는지");
            }
            // the planner's reaction: success, or the wrong person
            if (t.Plan != null && S.Plans.TryGetValue(t.Plan, out var plan) && plan.Stage != "Aborted" && plan.Stage != "Done")
            {
                var owner = S.A(t.Owner);
                if (v.Id == plan.Target) { plan.Log.Add($"{ClockFmt.Vague(S.Clock)} 함정 작동"); plan.Stage = "Concealing"; if (owner != null && plan.Step < plan.Steps.Count && plan.Steps[plan.Step].Kind == "X_WaitTrap") Crime.Advance(sim, owner, plan); }
                else if (owner != null) { Crime.Abort(sim, owner, plan, "엉뚱한 사람이 함정에 걸려서"); owner.Needs.Stress = MathX.Clamp01(owner.Needs.Stress + 0.4f); owner.Needs.Fear = MathX.Clamp01(owner.Needs.Fear + 0.3f); }
            }
        }

        static void TrapDiscovery(Simulation sim)
        {
            var S = sim.S; var rng = S.R(Stream.Life);
            foreach (var t in S.Traps.Where(x => x.Active && x.Kind != "Bedtime").ToList())   // a dosed drink in a closed bedroom is nobody's find
            {
                P3 at = Methods.TrapAt(S, t) ?? (t.Kind == "Topple" ? S.Layout.Furniture[t.Furniture].Pos : Top(S.Layout.Stairs[t.Stair]));
                // a stripped cable sits behind the machine: only someone right at it, looking closely, spots it (a puddle alone is just a puddle)
                float reach = t.Kind == "Shock" ? 1.4f : 2.6f; double odds = t.Kind == "Shock" ? 0.15 : 1.0;
                foreach (var x in S.LivingNpcs.Where(x => x.Id != t.Owner && x.Status == ActorStatus.Active && x.Pose != Pose.Sleep && x.Pos.f == at.f && x.Pos.DistXZ(at) < reach).ToList())
                {
                    double p = Math.Max(0, (x.Def.Obs - 55) / 900.0) * (sim.RoomLight(x.Room) > 0.3f ? 1 : 0.3) * (x.IsButler ? 2 : 1) * odds;
                    if (!rng.Chance(p)) continue;
                    t.Found = true;
                    var act = DisarmActivity(sim, x, t); if (act != null) sim.Assign(x, act);
                    S.Log("TrapFound", x.Id, room: x.Room, pos: x.Pos, data: t.Id + " " + t.Kind);
                    sim.Speak(x, "trap_found", null, new Dictionary<string, string> { { "place", S.RoomName(x.Room) } }, new Prop { Kind = PropKind.TrapSet, Room = t.Room, T0 = t.ArmedAt - 60, T1 = S.Clock, Value = t.Kind }, loud: true);
                    foreach (var o in S.Living) S.K(o.Id).Facts.Add($"trap-found:{t.Room}:{(int)S.Clock}");
                    break;
                }
            }
        }

        public static Activity DisarmActivity(Simulation sim, Actor a, Trap t)
        {
            var S = sim.S; if (!t.Active) return null;
            P3 at = Methods.TrapAt(S, t) ?? (t.Kind == "Topple" ? S.Layout.Furniture[t.Furniture].Pos : Top(S.Layout.Stairs[t.Stair]));
            if (t.Kind == "Bedtime" && S.Actors.Values.Any(x => x.Alive && x.Id != a.Id && x.Room == t.Room)) return null;   // not while the owner is in there
            var act = new Activity { Id = "g:disarm:" + t.Id, Label = t.Owner == a.Id ? "볼일" : t.Kind == "Shock" ? "위험한 전선 정리" : "위험한 가구 고정", Priority = 4, Interruptible = false };
            act.Steps.Add(Simulation.GoTo(sim.SnapPublic(at))); act.Steps.Add(new ActionStep { Kind = "X_Disarm", Data = t.Id, Duration = 3 });
            return act;
        }

        public static void Disarm(Simulation sim, Trap t, string by, string how)
        {
            var S = sim.S; t.Active = false; t.DisarmedAt = S.Clock; t.DisarmedBy = by;
            S.Log("TrapDisarmed", by, t.Owner, room: t.Room, data: $"{t.Id} {how}", secret: how == "owner");
            if (t.Kind == "Topple") { var f = S.Layout.Furniture[t.Furniture]; f.Marks.Add($"{ClockFmt.Vague(S.Clock)}에 누군가 받침을 다시 고정했다"); }
            else Methods.Disarm(sim, t, by, how);
            if (how != "owner" && t.Plan != null && S.Plans.TryGetValue(t.Plan, out var plan) && plan.Stage != "Aborted" && plan.Stage != "Done") { var o = S.A(t.Owner); if (o != null) Crime.Abort(sim, o, plan, "누가 함정을 찾아내서"); }
            foreach (var r in S.Ch.Reservations.Where(r => r.Plan == t.Plan && !r.Consumed)) r.Released = true;
        }

        public static void OnPlanAbort(Simulation sim, Actor a, MurderPlan plan)
        {
            var S = sim.S;
            // a recorder still waiting to play is quietly cancelled by its owner only if they get to it; otherwise it plays
            foreach (var rec in S.Items.Values.Where(i => i.ArmedBy == a.Id && i.PlayAt >= 0)) { if (rec.Room == a.Room) rec.PlayAt = -1; }
        }

        static void Playback(Simulation sim, Item rec)
        {
            var S = sim.S; rec.PlayedAt = S.Clock; rec.PlayAt = -1;
            if (rec.Holder != null || rec.RecVoice == null) return;
            var pos = rec.Pos;
            sim.Sound(SoundKind.Talk, pos, 0.42f, null, rec.RecVoice);
            S.Log("RecorderPlay", rec.ArmedBy, rec.RecVoice, item: rec.Id, room: rec.Room, pos: pos, data: "recorded@" + ClockFmt.Vague(rec.RecAt), secret: true);
            S.Emit(GameEventType.Speech, rec.RecVoice, text: rec.RecText, pos: pos, room: rec.Room, key: "recording", data: rec.Id);
        }

        // ================================================================== courier (IG10)
        public static void OnCourierHandover(Simulation sim, Actor helper, Actor target, Item note)
        {
            var S = sim.S; if (note.NoteFrom == null || !note.NoteFrom.StartsWith("courier:")) return;
            var p = note.NoteFrom.Split(':'); string sender = p[1]; int room = int.Parse(p[2]); double meetAt = double.Parse(p[3]);
            sim.Speak(helper, "courier_deliver", target.Id, null);
            // the recipient reads an unsigned note: curiosity and trust in the go-between decide
            bool go = !target.IsPlayer && target.Needs.Fear < 0.6f && (target.Def.P.Curiosity > 0.4f || S.R(target.Id, helper.Id).Trust > 0.3f);
            S.K(target.Id).Facts.Add($"note:{note.Id}:{room}:{(int)meetAt}");
            if (go) { S.Flags[$"meet:{target.Id}"] = room; S.Flags[$"meetat:{target.Id}"] = meetAt; S.Flags[$"meetwith:{target.Id}:{sender}"] = 1; }
            S.Log("NoteRead", target.Id, helper.Id, item: note.Id, data: go ? "will go" : "ignored");
        }

        /// <summary>When the victim of a courier-lured plan is found dead, the go-between realises what they carried.</summary>
        public static void OnDeathKnown(Simulation sim, Actor who, string victim)
        {
            var S = sim.S; var k = S.K(who.Id);
            var f = k.Facts.FirstOrDefault(x => x.StartsWith("courier:") && x.Split(':')[2] == victim);
            if (f == null || k.Facts.Contains("courier-fear:" + victim)) return;
            k.Facts.Add("courier-fear:" + victim);
            who.Needs.Fear = MathX.Clamp01(who.Needs.Fear + 0.35f); who.Needs.Stress = MathX.Clamp01(who.Needs.Stress + 0.3f);
            S.Log("CourierRealised", who.Id, victim, data: f, secret: true);
        }

        // ================================================================== examination hooks
        public static void ExamineFurniture(Simulation sim, Actor who, Furniture f, ref string desc, List<Prop> props)
        {
            var S = sim.S;
            var t = S.Traps.FirstOrDefault(x => x.Furniture == f.Id && x.Kind == "Topple" && (x.Active || x.FiredAt >= 0 || x.DisarmedAt >= 0));
            if (t == null) return;
            if (t.Active)
            {
                desc += "\n받침 한쪽이 빠져 있고 고정 나사가 풀려 있다 — 앞자리에 무게가 실리면 이쪽으로 넘어오게 되어 있다.";
                if (who != null && !S.K(who.Id).Facts.Contains("knows-trap:" + t.Id)) { S.K(who.Id).Facts.Add("knows-trap:" + t.Id); t.Found = true; S.Log("TrapFound", who.Id, room: f.Room, data: t.Id); }
            }
            else if (t.FiredAt >= 0) desc += "\n넘어진 가구의 받침 고정부가 미리 풀려 있었다 — 부러진 게 아니라 빠져 있다.";
            else desc += "\n받침을 누군가 다시 고정했다. 그 전에 풀려 있던 흔적이 남아 있다.";
            props.Add(new Prop { Kind = PropKind.TrapSet, Room = f.Room, Item = f.Type, T0 = S.Ch.ChapterStartClock, T1 = t.FiredAt >= 0 ? t.FiredAt : S.Clock, Value = "Topple" });
        }

        public static void ExamineItem(Simulation sim, Actor ex, Item it, List<string> lines, List<Prop> props)
        {
            var S = sim.S;
            if (it.Type == "Recorder")
            {
                if (it.RecAt >= 0)
                {
                    lines.Add($"{ClockFmt.Vague(it.RecAt)}에 녹음한 짧은 음성이 하나 들어 있다" + (it.PlayedAt >= 0 ? $" — {ClockFmt.Vague(it.PlayedAt)}에 재생됐다" : it.PlayAt >= 0 ? $" — {ClockFmt.Vague(it.PlayAt)}에 저절로 재생되도록 맞춰져 있다" : ""));
                    if (ex != null && ex.IsPlayer) lines.Add(it.RecVoice != null ? "틀어 보니 " + Cast.GivenOf(it.RecVoice) + "의 목소리다" : "틀어 봐도 누구 목소리인지 모르겠다");
                    props.Add(new Prop { Kind = PropKind.MachineUsed, A = it.RecVoice, Room = it.Room, Item = it.Type, T0 = it.PlayedAt >= 0 ? it.PlayedAt - 1 : it.RecAt, T1 = it.PlayedAt >= 0 ? it.PlayedAt + 1 : it.RecAt, Value = it.PlayedAt >= 0 ? $"녹음 재생 {ClockFmt.Vague(it.PlayedAt)} (녹음 {ClockFmt.Vague(it.RecAt)})" : $"녹음 {ClockFmt.Vague(it.RecAt)}" });
                }
                else lines.Add("녹음된 게 없다");
            }
            if (it.Type == "Invitation" && it.Note != null)
            {
                lines.Add("적힌 내용: “" + it.Note + "”");
                if (it.NoteFrom != null && !it.NoteFrom.StartsWith("courier:")) { if (ex != null) Grammars.ReadInvitation(sim, ex, it); lines.Add(it.NoteRev > 0 ? "나중에 고쳐 쓴 초대장이다" : "초대장이다"); }
                else lines.Add("보낸 사람의 이름이 없다");
                props.Add(new Prop { Kind = PropKind.ItemAt, Item = it.Type, Room = it.Room, T0 = S.Clock, T1 = S.Clock, Value = it.Note });
            }
            if (it.Type == "Fragment" && it.ParentItem != null && ex != null)
            {
                var mine = sim.Carried(ex).Where(i => i.Type == "Fragment" && i.ParentItem == it.ParentItem && i.Id != it.Id).ToList();
                var parent = S.I(it.ParentItem);
                if (mine.Count > 0) { lines.Add($"들고 있는 조각과 깨진 면이 맞물린다 — 같은 {parent?.Kor ?? "물건"}에서 나온 조각이다"); props.Add(new Prop { Kind = PropKind.ItemState, Item = parent?.Type, Room = parent?.Room ?? it.Room, T0 = S.Clock, T1 = S.Clock, Value = "조각 일치: " + (parent?.Kor ?? "?") }); }
                if (parent != null) lines.Add($"떨어져 나온 원래 {parent.Kor}은(는) 지금 {S.RoomName(parent.Room)}에 있다");
            }
            if (it.Type == "Tripwire" && it.Surface.Contains("팽팽하게 묶였던 자국"))
            {
                lines.Add("양 끝에 어딘가에 팽팽하게 묶여 있던 자국이 있다" + (it.Damage >= 2 ? " — 한가운데가 끊어졌다" : ""));
                props.Add(new Prop { Kind = PropKind.TrapSet, Room = it.Room, Item = it.Type, T0 = S.Ch.ChapterStartClock, T1 = S.Clock, Value = "Tripwire" });
            }
            if (it.Owner != null && it.Type != "Invitation" && it.Type != "Button" && it.KeyFor == null && it.Owner != Cast.Butler) lines.Add($"{Cast.GivenOf(it.Owner)}의 이름표가 붙어 있다");
            if (it.Type == "Document" && it.Note != null && it.Type != "Invitation") lines.Add("적힌 내용: “" + it.Note + "”");
            var off = it.Surface.FirstOrDefault(s => s.StartsWith("official:"));
            if (off != null && ex != null) { var p = off.Split(':'); var inc = S.Incidents.TryGetValue(p[1], out var ii) ? ii : null; if (inc != null) Rules.OfficialHalf(sim, ex, inc, p[2]); }
        }

        /// <summary>NPC/player quick sweep of a room also reads its devices (clock, door logger, rigged furniture).</summary>
        public static void RoomQuick(Simulation sim, Actor ex, int room)
        {
            var S = sim.S; var r = S.Layout.Room(room); if (r == null) return;
            foreach (var fid in r.Furniture)
            {
                var f = S.Layout.Furniture[fid];
                bool device = f.Type == "Clock" || f.Type == "DoorLogger" || S.Traps.Any(t => t.Furniture == fid && (t.FiredAt >= 0 || t.DisarmedAt >= 0));
                if (!device) continue;
                string desc = FurnitureCatalog.Get(f.Type)?.Kor ?? f.Type; var props = new List<Prop>();
                Grammars.ExamineFurniture(sim, ex, f, ref desc, props);
                Evidences.Add(sim, ex.Id, EvKind.Record, $"{FurnitureCatalog.Get(f.Type)?.Kor ?? f.Type} ({S.RoomName(room)})", desc, "직접 조사", "dev:" + fid + ":" + (int)(S.Clock / 30), S.Clock, S.Clock, room, "장치에 남은 기록", "장치에 남지 않은 일", true, props.ToArray());
            }
            foreach (var it in S.Items.Values.Where(i => i.Room == room && i.Holder == null && (i.Type == "Recorder" && i.RecAt >= 0 || i.Type == "Tripwire" && i.Surface.Count > 0)).ToList())
                Evidences.ExamineItem(sim, ex, it);
            Methods.RoomQuick(sim, ex, room);
        }

        public static string PlayerOperate(Simulation sim, Furniture f)
        {
            var S = sim.S;
            var t = S.Traps.FirstOrDefault(x => x.Active && x.Kind == "Topple" && x.Furniture == f.Id && S.K(Cast.Player).Facts.Contains("knows-trap:" + x.Id));
            if (t == null) return null;
            Disarm(sim, t, Cast.Player, "player");
            return "받침을 다시 고정했다 — 이제 넘어지지 않는다";
        }
    }
}
