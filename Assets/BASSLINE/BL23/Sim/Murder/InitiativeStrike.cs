using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    // =====================================================================================================================
    // STRIKE — the moment has come: the design becomes a MurderPlan the old machinery runs (Stalk, Attack, X_Dose, the
    // concealment layers…), with the scheme's own steps "S_*":
    //   S_WaitMoment  live normally until the hour              S_Errand   send someone to fetch something from a room
    //   S_Excuse      "잠깐 다녀올게요" (leave a table)            S_Retrieve take the weapon back out of its hiding place
    //   S_Near        stay by the victim until the lights go     S_DarkStrike close in in the dark (the mark, the last place)
    //   S_Mark        a small gift that finds them in the dark   S_Pour     pour the victim's cup (the dose follows: X_Dose)
    //   S_Note        a note in someone else's name              S_Summon   the scapegoat asked to wait near the scene
    //   S_Stash       the weapon into a hiding place right here  S_Plant    the scapegoat's thing left by the body
    //   S_Return      back to the room of the alibi              S_Resume   back to one's seat as the lights come on
    // Every lethal act stays the registered one (Attack / Drown / X_Dose / the methods) — MurderFoundation H5.
    // =====================================================================================================================
    public static partial class Initiative
    {
        static MurderPlan BuildStrike(Simulation sim, Scheme sc)
        {
            var S = sim.S; var a = S.A(sc.Culprit); var v = S.A(sc.Victim); if (a == null || v == null) return null;
            var plan = new MurderPlan { Id = S.NewId("plan"), Actor = a.Id, Target = v.Id, Motive = sc.Motive, Formed = S.Clock, StartTick = S.Tick, Reason = "scheme:" + sc.Id };
            plan.Deadline = Math.Max(sc.MomentEnd, S.Clock + 40) + 15;
            plan.Grammar = sc.Head ?? "Ambush";
            var w = S.I(sc.Weapon);
            if (w != null && w.Holder != null && w.Holder != a.Id) { w = null; }
            if (w != null) { plan.Weapon = w.Id; plan.WeaponType = w.Type; }
            bool carried = w != null && w.Holder == a.Id;
            bool stashed = w != null && w.Holder == null && sc.WeaponStashF >= 0 && w.StashF == sc.WeaponStashF;
            bool cord = w != null && Methods.IsCord(w.Def);
            string gid = sc.EventId; var g = gid != null ? S.Gatherings.FirstOrDefault(x => x.Id == gid) : null;
            if (g != null && (g.Done || g.Cancelled)) { gid = null; g = null; }
            void Wait() { if (g != null) plan.Steps.Add(new PlanStep { Kind = "X_WaitGathering", Note = gid }); else plan.Steps.Add(new PlanStep { Kind = "S_WaitMoment", Until = sc.MomentAt, Room = sc.MomentRoom }); }
            void Leave() { plan.Steps.Add(g != null ? new PlanStep { Kind = "X_SlipOut", Note = gid } : new PlanStep { Kind = "S_Excuse" }); }
            void Back(int room) { if (g != null) plan.Steps.Add(new PlanStep { Kind = "X_Rejoin", Note = gid }); else if (room >= 0) plan.Steps.Add(new PlanStep { Kind = "S_Return", Room = room }); }
            void Weapon() { if (w != null && !carried && !stashed) plan.Steps.Add(new PlanStep { Kind = "GetWeapon", Item = w.Id }); }
            void Garb() { if (sc.Garb != null && S.I(sc.Garb)?.Holder == a.Id) { plan.Disguise = sc.Garb; plan.Steps.Add(new PlanStep { Kind = "PutOn", Item = sc.Garb }); } }
            void Ungarb() { if (plan.Disguise != null) plan.Steps.Add(new PlanStep { Kind = "TakeOff", Room = -1 }); }
            int alibiRoom = sc.AlibiRoom >= 0 ? sc.AlibiRoom : sc.MomentRoom;
            bool method = sc.Approach != null && sc.Approach.StartsWith("method:");
            switch (sc.Approach)
            {
                case "errand":
                    {
                        if (sc.KillRoom < 0) return null;
                        Weapon(); Wait();
                        if (sc.Frame == "summon" && sc.Scapegoat != null)
                        {
                            // the scapegoat first, somewhere next door: they will have been "near the scene, alone" at the hour
                            int near = S.Layout.Neighbors(sc.KillRoom).Select(S.Layout.Room).Where(r => Plain(r) && r.Id != sc.MomentRoom && sim.RoomUsable(S.A(sc.Scapegoat) ?? a, r)).OrderBy(r => r.Id).Select(r => r.Id).DefaultIfEmpty(-1).First();
                            if (near < 0) near = sc.KillRoom;
                            plan.Steps.Add(new PlanStep { Kind = "S_Errand", Target = sc.Scapegoat, Room = near, Note = PretextFor(S.Layout.Room(near)?.Type ?? RoomType.Storage), Item = "decoy" });
                        }
                        plan.Steps.Add(new PlanStep { Kind = "S_Errand", Target = v.Id, Room = sc.KillRoom, Note = sc.Moment == "investigation" ? "확인할 것" : sc.Pretext });
                        Leave();
                        plan.Steps.Add(new PlanStep { Kind = "GoRoom", Room = sc.KillRoom });
                        if (stashed) plan.Steps.Add(new PlanStep { Kind = "S_Retrieve", Item = w.Id });
                        Garb();
                        plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = v.Id, Note = "reach", Room = sc.KillRoom });
                        plan.Steps.Add(new PlanStep { Kind = "Attack", Target = v.Id, Note = cord ? "strangle" : null });
                        Ungarb();
                        plan.Steps.Add(new PlanStep { Kind = "S_Stash", Room = sc.KillRoom, Note = sc.Frame == "weapon" ? "leave" : null });
                        FrameSteps(plan, sc);
                        Back(sc.MomentRoom);
                        plan.KillRoom = sc.KillRoom; plan.AlibiRoom = sc.MomentRoom;
                        break;
                    }
                case "slip-out":
                    {
                        Weapon(); Wait(); Leave();
                        if (sc.KillRoom >= 0) plan.Steps.Add(new PlanStep { Kind = "GoRoom", Room = sc.KillRoom });
                        Garb();
                        plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = v.Id, Note = "alone" });
                        plan.Steps.Add(new PlanStep { Kind = "Attack", Target = v.Id, Note = cord ? "strangle" : null });
                        Ungarb();
                        plan.Steps.Add(new PlanStep { Kind = "S_Stash", Room = -1, Note = sc.Frame == "weapon" ? "leave" : null });
                        FrameSteps(plan, sc);
                        Back(sc.MomentRoom);
                        plan.AlibiRoom = sc.MomentRoom;
                        break;
                    }
                case "dark-strike":
                    {
                        Weapon();
                        if (g != null) plan.Steps.Add(new PlanStep { Kind = "X_WaitGathering", Note = gid });
                        else plan.Steps.Add(new PlanStep { Kind = "S_WaitMoment", Until = sc.MomentAt - 22, Room = sc.MomentRoom });
                        if (sc.MarkItem != null && S.I(sc.MarkItem)?.Holder == a.Id) plan.Steps.Add(new PlanStep { Kind = "S_Mark", Target = v.Id, Item = sc.MarkItem });
                        if (sc.DarkBy == "helper" && !W(S).Favours.Any(f => f.Scheme == sc.Id && !f.Failed && !f.Done)) sc.DarkBy = "self";   // the helper said no, or was never asked
                        if (sc.DarkBy == "self")
                        {
                            var pr = S.Layout.First(RoomType.PowerRoom);
                            Leave();
                            plan.Steps.Add(new PlanStep { Kind = "CutPower", Room = pr?.Id ?? -1, Target = v.Id });
                            plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = v.Id, Note = "reach" });
                            plan.Steps.Add(new PlanStep { Kind = "Attack", Target = v.Id, Note = cord ? "strangle" : null });
                            plan.Steps.Add(new PlanStep { Kind = "S_Stash", Room = -1 });
                            plan.Steps.Add(new PlanStep { Kind = "RestorePower", Room = pr?.Id ?? -1 });
                            FrameSteps(plan, sc);
                            Back(sc.MomentRoom);
                        }
                        else
                        {
                            plan.Steps.Add(new PlanStep { Kind = "S_Near", Target = v.Id, Until = sc.MomentEnd });
                            plan.Steps.Add(new PlanStep { Kind = "S_DarkStrike", Target = v.Id, Until = sc.MomentEnd });
                            plan.Steps.Add(new PlanStep { Kind = "Attack", Target = v.Id, Note = cord ? "strangle" : null });
                            plan.Steps.Add(new PlanStep { Kind = "S_Stash", Room = -1, Note = "here" });
                            FrameSteps(plan, sc);
                            plan.Steps.Add(new PlanStep { Kind = "S_Resume" });
                        }
                        plan.AlibiRoom = sc.MomentRoom >= 0 ? sc.MomentRoom : -1;
                        break;
                    }
                case "serve":
                    {
                        var vial = S.I(sc.Poison); if (vial == null || (vial.Holder != null && vial.Holder != a.Id)) return null;
                        if (vial.Holder != a.Id) plan.Steps.Add(new PlanStep { Kind = "GetItem", Item = vial.Id });
                        Wait();
                        if (g != null) plan.Steps.Add(new PlanStep { Kind = "S_Pour", Target = v.Id });
                        plan.Steps.Add(new PlanStep { Kind = "X_Dose", Target = v.Id, Item = vial.Id });
                        if (sc.Frame == "vial" && sc.Scapegoat != null) plan.Steps.Add(new PlanStep { Kind = "S_Plant", Item = vial.Id, Target = sc.Scapegoat, Note = "near" });
                        else plan.Steps.Add(new PlanStep { Kind = "S_Stash", Room = -1, Item = vial.Id });
                        plan.Weapon = vial.Id; plan.WeaponType = vial.Type; plan.Grammar = "Poison";
                        plan.AlibiRoom = sc.MomentRoom;
                        break;
                    }
                case "rendezvous":
                    {
                        if (sc.KillRoom < 0) return null;
                        Weapon();
                        plan.Steps.Add(new PlanStep { Kind = "S_WaitMoment", Until = sc.MomentAt - 14, Room = sc.KillRoom });   // the appointment was made in the daytime (prep "appoint" / "note")
                        plan.Steps.Add(new PlanStep { Kind = "GoRoom", Room = sc.KillRoom });
                        Garb();
                        plan.Steps.Add(new PlanStep { Kind = "WaitVictim", Target = v.Id, Room = sc.KillRoom });
                        plan.Steps.Add(new PlanStep { Kind = "Attack", Target = v.Id, Note = cord ? "strangle" : null });
                        Ungarb();
                        plan.Steps.Add(new PlanStep { Kind = sc.Frame == "weapon" ? "S_Stash" : (U(S, plan.Id + ":hw") < 0.5 ? "HideWeapon" : "WashWeapon"), Room = sc.KillRoom, Note = sc.Frame == "weapon" ? "leave" : null });
                        FrameSteps(plan, sc);
                        plan.Steps.Add(new PlanStep { Kind = "CleanUp" });
                        if (sc.Alibi == "witness" || sc.Alibi == "clock") plan.Steps.Add(new PlanStep { Kind = "S_Return", Room = sc.AlibiRoom }); else plan.Steps.Add(new PlanStep { Kind = "Alibi" });
                        plan.KillRoom = sc.KillRoom; plan.AlibiRoom = alibiRoom;
                        break;
                    }
                case "ambush":
                    {
                        Weapon();
                        plan.Steps.Add(new PlanStep { Kind = "S_WaitMoment", Until = sc.MomentAt, Room = sc.KillRoom });
                        if (sc.KillRoom >= 0) plan.Steps.Add(new PlanStep { Kind = "GoRoom", Room = sc.KillRoom });
                        Garb();
                        bool dark = sc.Moment == "house-dark" || sc.Moment == "long-dark";
                        plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = v.Id, Note = dark ? "reach" : "alone" });
                        plan.Steps.Add(new PlanStep { Kind = "Attack", Target = v.Id, Note = cord ? "strangle" : null });
                        Ungarb();
                        plan.Steps.Add(new PlanStep { Kind = sc.Frame == "weapon" ? "S_Stash" : (U(S, plan.Id + ":hw") < 0.55 ? "HideWeapon" : "WashWeapon"), Room = -1, Note = sc.Frame == "weapon" ? "leave" : null });
                        FrameSteps(plan, sc);
                        plan.Steps.Add(new PlanStep { Kind = "CleanUp" });
                        if (sc.Alibi == "witness" || sc.Alibi == "clock") plan.Steps.Add(new PlanStep { Kind = "S_Return", Room = sc.AlibiRoom }); else plan.Steps.Add(new PlanStep { Kind = "Alibi" });
                        plan.KillRoom = sc.KillRoom; plan.AlibiRoom = alibiRoom;
                        if (sc.Moment == "investigation") plan.Deadline = S.Ch.InvestigationEnd - 4;
                        break;
                    }
                case "visit":
                    {
                        Weapon();
                        plan.Steps.Add(new PlanStep { Kind = "WaitNight" });
                        plan.Steps.Add(new PlanStep { Kind = "VisitRoom", Target = v.Id });
                        plan.Steps.Add(new PlanStep { Kind = "Attack", Target = v.Id, Note = cord ? "strangle" : null });
                        plan.Steps.Add(new PlanStep { Kind = "HideWeapon" });
                        FrameSteps(plan, sc);
                        plan.Steps.Add(new PlanStep { Kind = "CleanUp" });
                        plan.KillRoom = S.Layout.BedroomOf(v.Id)?.Id ?? -1;
                        plan.Deadline = sc.MomentEnd + 30;
                        break;
                    }
                default:
                    {
                        if (!method) return null;
                        string m = sc.Approach.Substring(7); plan.Grammar = m; plan.Weapon = null; plan.WeaponType = null;
                        if (m == "Drown")
                        {
                            var pool = S.Layout.First(RoomType.Pool); if (pool == null) return null; plan.KillRoom = pool.Id;
                            plan.Steps.Add(new PlanStep { Kind = "Invite", Target = v.Id, Room = pool.Id });
                            plan.Steps.Add(new PlanStep { Kind = "GoRoom", Room = pool.Id });
                            plan.Steps.Add(new PlanStep { Kind = "WaitVictim", Target = v.Id, Room = pool.Id });
                            plan.Steps.Add(new PlanStep { Kind = "Drown", Target = v.Id });
                        }
                        else if (m == "Trap")
                        {
                            var tc = Tricks.ChooseTrap(sim, a, v.Id); if (tc == null) return null;
                            Tricks.FillTrap(sim, plan, a, tc);
                        }
                        else if (m == "Shoot") Violence.Fill(sim, plan, a, Local(S, plan.Id + ":fill"));   // the violence track's gun / crossbow
                        else Methods.Fill(sim, plan, a, Local(S, plan.Id + ":fill"));
                        if (plan.Grammar != m) return null;   // the method fell back to a plain attack: not what was designed
                        FrameSteps(plan, sc);
                        plan.Steps.Add(new PlanStep { Kind = "CleanUp" });
                        if ((sc.Alibi == "witness" || sc.Alibi == "clock") && sc.AlibiRoom >= 0) plan.Steps.Add(new PlanStep { Kind = "S_Return", Room = sc.AlibiRoom });
                        plan.Deadline = Math.Max(plan.Deadline, sc.MomentEnd + 20);
                        break;
                    }
            }
            // the old concealment and trick layers for the quiet approaches (recorder, sealed doors, time shifts, the pool's filter…)
            if (sc.Moment != "investigation" && (sc.Approach == "rendezvous" || sc.Approach == "ambush" || sc.Approach == "visit" || method))
            {
                var lr = Local(S, plan.Id + ":aug");
                if (sc.Alibi != "witness" && sc.Alibi != "clock" && (sc.Approach == "ambush" || sc.Approach == "visit")) Tricks.Augment(sim, plan, a, lr);   // not for a meeting with an hour (a courier or a recording takes time)
                SetPieces.Augment(sim, plan, a, lr);
                Methods.Augment(sim, plan, a, lr);
            }
            else if (sc.Approach == "errand" && sc.Moment != "investigation" && a.Def.Infer + a.Def.Composure > 160) SetPieces.Augment(sim, plan, a, Local(S, plan.Id + ":aug"));
            // the scheme's own layers, for the reveal and the reconstruction ("Errand+Hosted+Framed+ClockAlibi")
            if (sc.Has("hosted")) plan.Grammar += "+Hosted";
            if (sc.Scapegoat != null) plan.Grammar += "+Framed";
            if (sc.Alibi == "witness") plan.Grammar += "+Witness"; else if (sc.Alibi == "clock") plan.Grammar += "+ClockAlibi";
            if (sc.Helper != null) plan.Grammar += "+Helper";
            if (sc.MarkItem != null) plan.Grammar += "+Marked";
            if (plan.Disguise != null) plan.Grammar += "+Garb";
            if (sc.Has("copycat")) plan.Grammar += "+Copycat";
            if (sc.Has("turnabout")) plan.Grammar += "+Turnabout";
            plan.Log.Add($"{ClockFmt.DayHM(S.Clock)} 계획: {plan.Grammar} 대상 {Cast.NameOf(v.Id)} ({sc.Motive}) — 준비된 기회: {sc.MomentText}");
            return plan;
        }

        static void FrameSteps(MurderPlan plan, Scheme sc)
        {
            if (sc.Scapegoat == null) return;
            if ((sc.Frame == "token" || sc.Frame2 == "token") && sc.FrameItem != null && sc.Frame != "weapon") plan.Steps.Add(new PlanStep { Kind = "S_Plant", Item = sc.FrameItem, Target = plan.Target });
        }

        // ================================================================== hook: Crime.Think for "S_" plan steps
        public static Activity PlanThink(Simulation sim, Actor a, MurderPlan plan, PlanStep st, bool beforeAttack)
        {
            var S = sim.S; var t = S.A(plan.Target); var sc = OfPlan(S, plan);
            if (a.Act != null && a.Act.Id == "murder:" + plan.Id + ":" + plan.Step) return a.Act;
            if (beforeAttack && (t == null || !t.Alive)) { Crime.Abort(sim, a, plan, "target gone"); return null; }
            if (beforeAttack && S.Clock > plan.Deadline) { Crime.Abort(sim, a, plan, "deadline"); return null; }
            if (beforeAttack && S.Phase != Phase.Daily && !PhaseOk(S, plan)) { Crime.Abort(sim, a, plan, "case opened"); return null; }
            var act = new Activity { Id = "murder:" + plan.Id + ":" + plan.Step, Label = "볼일", Priority = 30, Secret = true };
            var rng = Local(S, plan.Id + ":" + plan.Step + ":" + (long)S.Clock);
            switch (st.Kind)
            {
                case "S_WaitMoment":
                    if (S.Clock >= st.Until) { Crime.Advance(sim, a, plan); return null; }
                    return null;   // life goes on until the hour
                case "S_Errand":
                    {
                        var x = S.A(st.Target); if (x == null || !x.Alive || x.Status != ActorStatus.Active) { if (st.Item == "decoy") { Crime.Advance(sim, a, plan); return null; } Crime.Abort(sim, a, plan, "심부름 보낼 사람이 없어서"); return null; }
                        if (st.Item == "decoy" && (x.Room != a.Room || a.Pos.Dist(x.Pos) > 12f)) { Crime.Advance(sim, a, plan); return null; }   // the scapegoat is not at hand: no decoy tonight
                        act.Steps.Add(new ActionStep { Kind = "S_Reach", Actor = x.Id });
                        act.Steps.Add(new ActionStep { Kind = "S_Errand", Actor = x.Id, Room = st.Room, Data = st.Note, Tag = st.Item });
                        act.Label = "잠깐 부탁"; act.Interruptible = false; act.Priority = 40; return act;
                    }
                case "S_Excuse":
                    act.Steps.Add(new ActionStep { Kind = "S_Excuse" }); act.Label = "잠깐 볼일"; act.Interruptible = false; return act;
                case "S_Retrieve":
                    {
                        var it = S.I(st.Item);
                        if (it == null || it.Holder == a.Id) { Crime.Advance(sim, a, plan); return null; }
                        if (it.Holder != null || it.StashF < 0) { Crime.Replan(sim, a, plan, "숨겨 둔 흉기가 없어져서"); return null; }
                        var f = S.Layout.Furniture[it.StashF];
                        act.Steps.Add(Simulation.GoTo(sim.FrontOf(f))); act.Steps.Add(new ActionStep { Kind = "C_Retrieve", Item = it.Id }); act.Steps.Add(new ActionStep { Kind = "S_Advance" });
                        act.Label = "물건 챙기기"; act.Interruptible = false; return act;
                    }
                case "S_Near":
                    act.Steps.Add(new ActionStep { Kind = "S_Near", Actor = st.Target, Data = st.Until.ToString(System.Globalization.CultureInfo.InvariantCulture) });
                    act.Label = sc?.EventLabel ?? "사람들 곁에서 휴식"; act.Interruptible = false; act.Priority = 60; return act;
                case "S_DarkStrike":
                    act.Steps.Add(new ActionStep { Kind = "S_DarkStrike", Actor = st.Target, Data = st.Until.ToString(System.Globalization.CultureInfo.InvariantCulture) });
                    act.Label = "어둠 속"; act.Interruptible = false; act.Priority = 95; return act;
                case "S_Mark":
                    {
                        var it = S.I(st.Item); if (it == null || it.Holder != a.Id || t == null) { Crime.Advance(sim, a, plan); return null; }
                        act.Steps.Add(new ActionStep { Kind = "S_Reach", Actor = t.Id }); act.Steps.Add(new ActionStep { Kind = "S_Mark", Actor = t.Id, Item = it.Id }); act.Steps.Add(new ActionStep { Kind = "S_Advance" });
                        act.Label = "작은 선물"; act.Interruptible = false; return act;
                    }
                case "S_Pour":
                    {
                        if (t == null) { Crime.Advance(sim, a, plan); return null; }
                        act.Steps.Add(new ActionStep { Kind = "S_Reach", Actor = t.Id }); act.Steps.Add(new ActionStep { Kind = "S_Pour", Actor = t.Id }); act.Steps.Add(new ActionStep { Kind = "S_Advance" });
                        act.Label = "차 따르기"; act.Interruptible = false; return act;
                    }
                case "S_Note":
                    {
                        var bed = S.Layout.BedroomOf(a.Id); var vb = S.Layout.BedroomOf(plan.Target);
                        if (vb == null || vb.Doors.Count == 0) { st.Kind = "Invite"; return null; }
                        if (bed != null) { act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(bed, rng))); act.Steps.Add(Simulation.Do("write", 3, Anim.Write)); }
                        var d = S.Layout.Doors[vb.Doors[0]];
                        act.Steps.Add(Simulation.GoTo(sim.SnapPublic(d.Pos)));
                        act.Steps.Add(new ActionStep { Kind = "S_Note", Actor = plan.Target, Room = st.Room, Item = st.Item, Door = d.Id, Data = st.Until.ToString(System.Globalization.CultureInfo.InvariantCulture) });
                        act.Steps.Add(new ActionStep { Kind = "S_Advance" });
                        act.Label = "편지 쓰기"; act.Interruptible = false; return act;
                    }
                case "S_Summon":
                    {
                        var sgA = S.A(st.Target);
                        if (sgA != null && sgA.Alive && !sgA.IsPlayer)
                        {
                            int near = S.Layout.Neighbors(st.Room).Select(S.Layout.Room).Where(r => Plain(r) && sim.RoomUsable(sgA, r)).OrderBy(r => r.Id).Select(r => r.Id).DefaultIfEmpty(-1).First();
                            if (near < 0) near = st.Room;
                            S.Flags["meet:" + sgA.Id] = near; S.Flags["meetat:" + sgA.Id] = Math.Max(S.Clock + 8, st.Until); S.Flags[$"meetwith:{sgA.Id}:{plan.Target}"] = 1;
                            S.K(sgA.Id).Facts.Add($"invited:{plan.Target}:{near}:{(int)st.Until}");
                            S.Log("SchemeSummon", a.Id, sgA.Id, room: near, data: $"as {plan.Target} @{ClockFmt.HM(st.Until)}", secret: true);
                            if (sc != null) sc.Log.Add(K($"{ClockFmt.DayHM(S.Clock)} {Name(sgA.Id)}에게 {Name(plan.Target)}의 이름으로 {S.RoomName(near)}에서 보자는 쪽지를 남겼다."));
                        }
                        Crime.Advance(sim, a, plan); return null;
                    }
                case "S_Stash":
                    {
                        var it = S.I(st.Item ?? plan.Weapon);
                        if (it == null || it.Holder != a.Id) { Crime.Advance(sim, a, plan); return null; }
                        var room = S.Layout.Room(st.Room >= 0 ? st.Room : a.Room);
                        if (st.Note == "leave" || room == null) { act.Steps.Add(new ActionStep { Kind = "S_Leave", Item = it.Id }); act.Steps.Add(new ActionStep { Kind = "S_Advance" }); act.Interruptible = false; return act; }
                        var f = Concealment.PlaceIn(S, room, it, a.Pos);
                        if (f != null && (st.Note != "here" || a.Pos.DistXZ(f.Pos) < 6f)) { act.Steps.Add(Simulation.GoTo(sim.FrontOf(f))); act.Steps.Add(new ActionStep { Kind = "C_Stash", Item = it.Id, Furniture = f.Id }); }
                        else if (Concealment.BestSlot(S, a, it, out _) != BodySlot.None && !it.Bloody) act.Steps.Add(new ActionStep { Kind = "C_Tuck", Item = it.Id });
                        else act.Steps.Add(new ActionStep { Kind = "S_Leave", Item = it.Id, Tag = "hidden" });
                        act.Steps.Add(new ActionStep { Kind = "S_Advance" });
                        act.Label = "정리"; act.Interruptible = false; return act;
                    }
                case "S_Plant":
                    {
                        var it = S.I(st.Item);
                        if (it == null || it.Holder != a.Id) { Crime.Advance(sim, a, plan); return null; }
                        P3 at;
                        if (st.Note == "near") { var sg = S.A(st.Target); if (sg == null || sg.Room != a.Room) { act.Steps.Add(new ActionStep { Kind = "S_Leave", Item = it.Id, Tag = "hidden" }); act.Steps.Add(new ActionStep { Kind = "S_Advance" }); act.Interruptible = false; return act; } at = sg.Pos; }
                        else { if (t == null) { Crime.Advance(sim, a, plan); return null; } at = t.Pos; }
                        act.Steps.Add(Simulation.GoTo(sim.SnapPublic(new P3(at.f, at.x + 0.6f, at.z + 0.3f))));
                        act.Steps.Add(new ActionStep { Kind = "S_Plant", Item = it.Id, Actor = st.Target, Tag = st.Note });
                        act.Steps.Add(new ActionStep { Kind = "S_Advance" });
                        act.Label = "정리"; act.Interruptible = false; return act;
                    }
                case "S_Return":
                    {
                        var r = S.Layout.Room(st.Room); if (r == null) { Crime.Advance(sim, a, plan); return null; }
                        act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(r, rng))); act.Steps.Add(new ActionStep { Kind = "S_Advance" });
                        act.Label = "사람들 곁으로"; return act;
                    }
                case "S_Resume":
                    act.Steps.Add(Simulation.WaitStep(2)); act.Steps.Add(new ActionStep { Kind = "S_Advance" }); act.Label = sc?.EventLabel ?? "휴식"; return act;
            }
            Crime.Advance(sim, a, plan); return null;
        }

        static MurderPlan PlanOf(GameState S, Actor a) => a.PlanId != null && S.Plans.TryGetValue(a.PlanId, out var p) ? p : null;

        // ================================================================== hook: Crime.Exec for "S_" action steps
        public static bool Exec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S;
            switch (st.Kind)
            {
                case "S_Advance": { var plan = PlanOf(S, a); if (plan != null && a.Act != null && a.Act.Id != null && a.Act.Id.StartsWith("murder:" + plan.Id)) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true; }
                case "S_Reach": Reach(sim, a, st); return true;
                case "S_Errand": ErrandExec(sim, a, st); return true;
                case "S_Excuse":
                    {
                        sim.Speak(a, "gathering_leave", null);
                        foreach (var x in S.Living.Where(x => x != a && x.Room == a.Room && x.Pose != Pose.Sleep)) S.K(x.Id).Facts.Add($"left-table:{a.Id}:{a.Room}:{(int)S.Clock}");
                        S.Log("Excuse", a.Id, room: a.Room, data: "left the table");
                        var plan = PlanOf(S, a); if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true;
                    }
                case "S_Near": NearExec(sim, a, st); return true;
                case "S_DarkStrike": DarkStrikeExec(sim, a, st); return true;
                case "S_Mark":
                    {
                        var t = S.A(st.Actor); var it = S.I(st.Item);
                        if (t != null && it != null && it.Holder == a.Id && a.Pos.Dist(t.Pos) < 3f)
                        {
                            sim.Speak(a, "scheme_gift", t.Id, new Dictionary<string, string> { { "item", it.Def?.Kor ?? it.Kor } });
                            Grammars.Hand(sim, a, t, it);
                            S.K(a.Id).Facts.Add("mark:" + t.Id + ":" + it.Id);
                            var sc = OfPlan(S, PlanOf(S, a)); if (sc != null) Beat(sim, sc, a, "mark", it.Id, a.Room, K($"{Name(a.Id)}이(가) {Name(t.Id)}에게 {it.Def?.Kor ?? it.Kor}을(를) 건넸다"), "모임 자리의 작은 선물인 줄 알았다", "어둠 속에서 표적을 찾아낼 표식");
                        }
                        sim.NextStepPublic(a); return true;
                    }
                case "S_Pour":
                    {
                        var t = S.A(st.Actor);
                        if (t != null && t.Alive && a.Pos.Dist(t.Pos) < 3f && !t.IsPlayer)
                        {
                            sim.Speak(a, "scheme_pour", t.Id);
                            var tea = new Activity { Id = "life:tea", Label = "차 마시기", Priority = 3.5 }; tea.Steps.Add(Simulation.Do("tea", 9, Anim.Drink));
                            sim.Assign(t, tea);
                            var sc = OfPlan(S, PlanOf(S, a)); if (sc != null) Beat(sim, sc, a, "serve", null, a.Room, K($"{Name(a.Id)}이(가) {Name(t.Id)}의 잔을 손수 채웠다"), "주최자다운 친절인 줄 알았다", "독을 탈 잔을 정하는 순간");
                        }
                        sim.NextStepPublic(a); return true;
                    }
                case "S_Note": NoteExec(sim, a, st); return true;
                case "S_Leave":
                    {
                        var it = S.I(st.Item);
                        if (it != null && it.Holder == a.Id) { sim.DropItem(a, it, a.Pos, st.Tag == "hidden"); S.Log("SchemeLeave", a.Id, item: it.Id, room: a.Room, pos: a.Pos, data: st.Tag ?? "left", secret: true); }
                        sim.NextStepPublic(a); return true;
                    }
                case "S_Plant":
                    {
                        var it = S.I(st.Item);
                        if (it != null && it.Holder == a.Id)
                        {
                            sim.DropItem(a, it, a.Pos, st.Tag == "near");
                            S.Log("PlantToken", a.Id, st.Actor, item: it.Id, room: a.Room, pos: a.Pos, data: it.Owner ?? "", secret: true);
                            var sc = OfPlan(S, PlanOf(S, a)); if (sc != null) { sc.Planted.Add(it.Id); sc.Log.Add(K($"{ClockFmt.DayHM(S.Clock)} {S.RoomName(a.Room)}에 {(it.Owner != null ? Name(it.Owner) + "의 " : "")}{it.Def?.Kor ?? it.Kor}을(를) 남겨 두었다.")); }
                        }
                        a.Anim = Anim.PutDown; sim.NextStepPublic(a); return true;
                    }
                case "S_Prep": PrepExec(sim, a, st); return true;
                case "S_Favour": FavourExec(sim, a, st); return true;
                case "S_Cover": CoverExec(sim, a, st); return true;
            }
            return false;
        }

        /// <summary>Walk up to someone (they may be moving). Gives up after 25 minutes.</summary>
        static void Reach(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S; var t = S.A(st.Actor);
            if (t == null || !t.Alive) { sim.Interrupt(a, 1); return; }
            if (a.Pos.Dist(t.Pos) <= 2.0f) { a.Speed = 0; a.Act.Path = null; a.Yaw = MathX.AngleDeg(t.Pos.x - a.Pos.x, t.Pos.z - a.Pos.z); sim.NextStepPublic(a); return; }
            if (S.Clock - a.Act.StepStart > 25) { sim.Interrupt(a, 1); return; }
            if (a.Act.Path == null || S.Tick % 15 == 0)
            {
                var pr = Pathfinder.Find(S.Layout, a.Pos, t.Pos, sim.DoorCostFor(a));
                if (!pr.Ok) { sim.Interrupt(a, 2); return; }
                a.Act.Path = pr.Points; a.Act.PathDoors = pr.DoorAtPoint; a.Act.PathStairs = pr.StairAtPoint; a.Act.PathIdx = 1;
            }
            sim.MoveAlongPublic(a);
        }

        /// <summary>"Could you bring the wine from the cellar?" — the one sent goes, looks, and comes back if nothing happens.</summary>
        static void ErrandExec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S; var x = S.A(st.Actor); var plan = PlanOf(S, a); var sc = OfPlan(S, plan); var room = S.Layout.Room(st.Room);
            bool decoy = st.Tag == "decoy";
            if (x == null || room == null || !x.Alive) { if (plan != null) { if (decoy) Crime.Advance(sim, a, plan); else Crime.Abort(sim, a, plan, "심부름 보낼 사람이 없어서"); } sim.NextStepPublic(a); return; }
            var slots = new Dictionary<string, string> { { "item", st.Data ?? "물건" }, { "place", room.Name } };
            bool inv = S.Phase == Phase.Investigation;
            sim.Speak(a, inv ? "scheme_errand_inv" : "scheme_errand_ask", x.Id, slots);
            var r = S.R(x.Id, a.Id);
            float fearOf = r.Fear; float susp = S.K(x.Id).Suspicion.TryGetValue(a.Id, out var su0) ? su0 : 0f;
            bool yes = !x.IsPlayer && x.Status == ActorStatus.Active && r.Trust + r.Like > -0.15f && fearOf < 0.45f && susp < 0.45f && (inv || x.Needs.Fear < 0.85f) && (x.Act == null || x.Act.Interruptible || (x.Act.Id != null && (x.Act.Id.StartsWith("g:") || x.Act.Id.StartsWith("life:") || x.Act.Id.StartsWith("inv:"))));
            sim.Speak(x, yes ? "scheme_errand_yes" : "scheme_errand_no", a.Id, slots);
            foreach (var o in S.Living.Where(o => o != a && o != x && o.Room == a.Room && o.Pose != Pose.Sleep)) S.K(o.Id).Facts.Add($"errand:{a.Id}:{x.Id}:{room.Id}:{(int)S.Clock}");
            S.Log("Errand", a.Id, x.Id, room: room.Id, data: (st.Data ?? "") + (yes ? " yes" : " no") + (decoy ? " decoy" : ""));
            if (yes)
            {
                var back = S.Layout.Room(x.Room);
                var job = new Activity { Id = "errand:" + (sc?.Id ?? "x") + ":" + x.Id, Label = $"심부름 ({st.Data})", Priority = 25, Interruptible = false };
                var rng = Local(S, "err:" + x.Id + ":" + (long)S.Clock);
                job.Steps.Add(Simulation.GoTo(sim.RandomPointIn(room, rng)));
                job.Steps.Add(Simulation.Do("search", decoy ? 16 : 14, Anim.Search));   // looking properly takes a while — long enough for someone to follow
                if (back != null && !RoomInfo.IsPassage(back.Type)) job.Steps.Add(Simulation.GoTo(sim.RandomPointIn(back, rng)));
                sim.Assign(x, job);
                if (sc != null) sc.Log.Add(K($"{ClockFmt.DayHM(S.Clock)} {Name(x.Id)}을(를) {room.Name}(으)로 보냈다 — \"{st.Data}\" 심부름" + (decoy ? " (희생양을 현장 근처로)" : "") + "."));
                if (plan != null) Crime.Advance(sim, a, plan);
            }
            else if (plan != null) { if (decoy) Crime.Advance(sim, a, plan); else Crime.Abort(sim, a, plan, "심부름을 거절당해서"); }
            sim.NextStepPublic(a);
        }

        static bool DarkAt(Simulation sim, Actor who)
        {
            var S = sim.S; float l = sim.RoomLight(who.Room);
            return l <= 0.25f;
        }

        /// <summary>Stay close to the victim (as anyone at an evening would), until the lights go.</summary>
        static void NearExec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S; var t = S.A(st.Actor); var plan = PlanOf(S, a); var sc = OfPlan(S, plan);
            if (t == null || !t.Alive) { sim.Interrupt(a, 1); return; }
            double until = double.TryParse(st.Data, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var u) ? u : S.Clock + 30;
            if (DarkAt(sim, t) && a.Pos.Dist(t.Pos) < 9f) { if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return; }
            if (S.Clock > until) { if (plan != null) Crime.Abort(sim, a, plan, "끝내 불이 꺼지지 않아서"); sim.Interrupt(a, 1); return; }
            // at an evening: stay in its room and wait for them to come to it (never chase them round the house)
            int room = sc != null && sc.DarkBy != "house" ? (sc.EventRoom >= 0 ? sc.EventRoom : sc.MomentRoom) : -1;
            var goal = t.Pos; bool here = room < 0 || t.Room == room;
            if (!here)
            {
                var r = S.Layout.Room(room); if (r == null) { a.Speed = 0; return; }
                if (a.Room == room) { a.Speed = 0; a.Act.Path = null; a.Anim = Anim.Talk; return; }
                goal = new P3(r.Floor, r.Rect.CX, r.Rect.CZ);
            }
            float d = a.Pos.Dist(goal);
            if (d > 2.4f)
            {
                if (a.Act.Path == null || S.Tick % 20 == 0)
                {
                    var pr = Pathfinder.Find(S.Layout, a.Pos, goal, sim.DoorCostFor(a)); if (!pr.Ok) { a.Speed = 0; return; }
                    a.Act.Path = pr.Points; a.Act.PathDoors = pr.DoorAtPoint; a.Act.PathStairs = pr.StairAtPoint; a.Act.PathIdx = 1;
                }
                sim.MoveAlongPublic(a);
                return;
            }
            a.Speed = 0; a.Act.Path = null; a.Anim = Anim.Listen; a.Yaw = MathX.AngleDeg(t.Pos.x - a.Pos.x, t.Pos.z - a.Pos.z);
            // beside them at last: the sign for the lights (a hand bell everyone in the room hears — and will remember)
            if (here && room >= 0 && sc != null && sc.DarkBy == "helper" && !S.Flags.ContainsKey("cue:" + sc.Id))
            {
                S.Flags["cue:" + sc.Id] = S.Clock;
                sim.Speak(a, "scheme_cue", null, null, loud: true);
                sim.Sound(SoundKind.Bell, a.Pos, 0.55f, a.Id);
                foreach (var x in S.Living.Where(x => x != a && x.Room == a.Room && x.Pose != Pose.Sleep)) S.K(x.Id).Facts.Add($"cue:{a.Id}:{a.Room}:{(int)S.Clock}");
                S.Log("SchemeCue", a.Id, t.Id, room: a.Room, data: sc.Id, secret: true);
                Beat(sim, sc, a, "cue", null, a.Room, K($"{Name(a.Id)}이(가) 작은 종을 울리며 분위기를 띄웠다"), "모임의 순서인 줄 알았다", "불을 끄라는 조력자에게 보내는 신호");
            }
        }

        /// <summary>The lights are out: close in on the victim (the mark finds them; so does the last place they stood) and hand over to the attack.</summary>
        static void DarkStrikeExec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S; var t = S.A(st.Actor); var plan = PlanOf(S, a);
            if (t == null || !t.Alive) { sim.Interrupt(a, 1); return; }
            if (!DarkAt(sim, t))
            {
                if (plan != null) { plan.Log.Add($"{ClockFmt.DayHM(S.Clock)} 불이 너무 빨리 들어옴"); plan.Step = Math.Max(0, plan.Steps.FindIndex(x => x.Kind == "S_Near")); }
                sim.Interrupt(a, 0.5); return;
            }
            bool marked = S.K(a.Id).Facts.Contains("mark:" + t.Id + ":" + (OfPlan(S, plan)?.MarkItem ?? "-"));
            if (!marked && a.Pos.Dist(t.Pos) > 7f) { if (plan != null) Crime.Abort(sim, a, plan, "어둠 속에서 표적을 놓쳐서"); sim.Interrupt(a, 1); return; }
            if (a.Pos.Dist(t.Pos) <= 1.1f)
            {
                a.Speed = 0;
                if (plan != null && !Crime.Reserve(sim, a, t, plan)) { Crime.Abort(sim, a, plan, "때가 맞지 않아서"); sim.Interrupt(a, 1); return; }
                S.Log("DarkStrike", a.Id, t.Id, room: t.Room, pos: t.Pos, data: marked ? "marked" : "remembered", secret: true);
                if (plan != null) Crime.Advance(sim, a, plan);
                sim.NextStepPublic(a); return;
            }
            if (a.Act.Path == null || S.Tick % 6 == 0)
            {
                var pr = Pathfinder.Find(S.Layout, a.Pos, t.Pos, sim.DoorCostFor(a)); if (!pr.Ok) { sim.Interrupt(a, 1); return; }
                a.Act.Path = pr.Points; a.Act.PathDoors = pr.DoorAtPoint; a.Act.PathStairs = pr.StairAtPoint; a.Act.PathIdx = 1;
            }
            sim.MoveAlongPublic(a);
        }

        /// <summary>A note slipped under the victim's door in someone else's name: "at nine, in the chapel — 채령".</summary>
        static void NoteExec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S; var v = S.A(st.Actor); var room = S.Layout.Room(st.Room); var plan = PlanOf(S, a); var sc = OfPlan(S, plan);
            double at = double.TryParse(st.Data, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var u) ? u : S.Clock + 30;
            at = Math.Max(at, S.Clock + 15);
            if (v != null && room != null && v.Alive && !v.IsPlayer)
            {
                var vb = S.Layout.BedroomOf(v.Id); var d = st.Door >= 0 ? S.Layout.Doors[st.Door] : null;
                var inside = vb != null && d != null ? sim.SnapPublic(new P3(d.Pos.f, d.Pos.x + (d.AlongX ? 0 : (vb.Rect.CX > d.Pos.x ? 0.6f : -0.6f)), d.Pos.z + (d.AlongX ? (vb.Rect.CZ > d.Pos.z ? 0.6f : -0.6f) : 0))) : v.Pos;
                string signer = st.Item;
                var note = new Item { Id = S.NewId("it"), Type = "Document", Name = "접힌 쪽지", Owner = v.Id, Pos = inside, Room = vb?.Id ?? a.Room,
                    Note = K($"{ClockFmt.Vague(at)}, {room.Name}에서 기다릴게. 둘이서만 할 얘기가 있어. — {Name(signer)}"), NoteFrom = "framed:" + a.Id + ":" + signer };
                S.Items[note.Id] = note; S.Emit(GameEventType.ItemMoved, a.Id, data: note.Id, text: "spawn", pos: note.Pos);
                S.Flags["meet:" + v.Id] = room.Id; S.Flags["meetat:" + v.Id] = at; S.Flags[$"meetwith:{v.Id}:{signer}"] = 1;
                S.K(v.Id).Facts.Add($"invited:{signer}:{room.Id}:{(int)at}");
                S.Log("SchemeNote", a.Id, v.Id, item: note.Id, room: room.Id, data: $"signed {signer} @{ClockFmt.HM(at)}", secret: true);
                if (sc != null) { sc.Planted.Add(note.Id); sc.Log.Add(K($"{ClockFmt.DayHM(S.Clock)} {Name(v.Id)}의 방문 아래로 {Name(signer)}의 이름을 쓴 쪽지를 밀어 넣었다 — {ClockFmt.Vague(at)}, {room.Name}.")); Beat(sim, sc, a, "note", note.Id, a.Room, K($"{Name(a.Id)}이(가) {Name(v.Id)}의 방문 앞에서 허리를 굽혔다"), "떨어진 걸 줍는 줄 알았다", "남의 이름으로 쓴 호출 쪽지"); }
            }
            sim.NextStepPublic(a);
        }
    }
}
