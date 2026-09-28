using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    public sealed partial class Simulation
    {
        // ------------------------------------------------------------------ activity helpers
        public static ActionStep GoTo(P3 p, bool run = false) => new ActionStep { Kind = "GoTo", Target = p, HasTarget = true, Run = run };
        public static ActionStep Do(string tag, double minutes, Anim anim, int spot = -1) => new ActionStep { Kind = "Activity", Tag = tag, Duration = minutes, Anim = anim, Spot = spot };
        public static ActionStep WaitStep(double minutes) => new ActionStep { Kind = "Wait", Duration = minutes };

        public void Assign(Actor a, Activity act)
        {
            if (a.Act != null) EndActivity(a, false);
            act.Index = 0; act.StepStart = -1; act.Path = null;
            a.Act = act;
            if (a.Spot >= 0 && a.Pose != Pose.Stand && a.Pose != Pose.Crouch) { ReleaseSpot(a); a.Pose = Pose.Stand; }
        }

        void EndActivity(Actor a, bool completed)
        {
            if (a.Act == null) return;
            if (a.Spot >= 0 && a.Pose != Pose.Stand) { ReleaseSpot(a); a.Pose = Pose.Stand; }
            a.Anim = Anim.Idle; a.Running = false; a.Speed = 0;
            var id = a.Act.Id; a.Act = null;
            if (!completed && id != null && id.StartsWith("murder")) Crime.OnStepFailed(this, a);
        }

        void ReleaseSpot(Actor a)
        {
            if (a.Spot >= 0 && a.Spot < S.Layout.Spots.Count && S.Layout.Spots[a.Spot].Occupant == a.Id) S.Layout.Spots[a.Spot].Occupant = null;
            a.Spot = -1;
        }

        public void Interrupt(Actor a, double thinkIn = 0)
        {
            EndActivity(a, false); a.NextThink = S.Clock + thinkIn;
        }

        void NextStep(Actor a)
        {
            var act = a.Act; if (act == null) return;
            act.Index++; act.StepStart = -1; act.Path = null; act.PathIdx = 0;
            if (act.Index >= act.Steps.Count) { var id = act.Id; EndActivity(a, true); OnActivityDone(a, id); a.NextThink = S.Clock; }
        }

        void FailStep(Actor a, string why)
        {
            if (a.Act == null) return;
            a.Act.Failures++;
            S.Dev($"{a.Id} step fail {a.Act.Id}/{a.Act.Cur?.Kind}: {why}");
            if (a.Act.Id != null && a.Act.Id.StartsWith("murder")) { Crime.OnStepFailed(this, a, why); return; }
            EndActivity(a, false); a.NextThink = S.Clock + 0.5;
        }

        // ------------------------------------------------------------------ execution
        void Execute(Actor a)
        {
            if (Violence.Holds(this, a)) return;   // --- violence track: held in a strangle / smother / drowning hold
            if (a.StairId >= 0) { StairMove(a); return; }
            if (a.CarriedBy != null) return;
            // in a conversation: stay put unless something urgent (non-interruptible) takes over
            if (a.TalkingTo != null && (a.Act == null || a.Act.Interruptible)) { a.Speed = 0; return; }
            var act = a.Act; if (act == null) { a.Speed = 0; if (a.Anim != Anim.Sleep) a.Anim = a.Pose == Pose.Sit ? Anim.Idle : Anim.Idle; return; }
            var st = act.Cur; if (st == null) { NextStep(a); return; }
            // a motion in progress finishes before the next thing starts (walking off mid-gesture looks wrong)
            if (a.BusyUntil > S.Tick && st.Kind != "Door") { a.Speed = 0; return; }
            if (act.StepStart < 0)
            {
                act.StepStart = S.Clock; if (st.Duration > 0) act.StepEnd = S.Clock + st.Duration;
                if (StepMotion.TryGetValue(st.Kind, out var mo)) { a.Anim = mo.anim; if (mo.hold > 0) a.BusyUntil = Math.Max(a.BusyUntil, S.Tick + mo.hold); }
            }
            switch (st.Kind)
            {
                case "GoTo": StepGoTo(a, st); break;
                case "Wait": a.Speed = 0; a.Anim = st.Anim == Anim.None ? Anim.Idle : st.Anim; if (S.Clock >= act.StepEnd) NextStep(a); break;
                case "Activity": StepActivity(a, st); break;
                case "Face": { var t = S.A(st.Actor); if (t != null) a.Yaw = MathX.AngleDeg(t.Pos.x - a.Pos.x, t.Pos.z - a.Pos.z); NextStep(a); break; }
                case "PickUp": StepPickUp(a, st); break;
                case "Drop": StepDrop(a, st); break;
                case "Door": StepDoor(a, st); break;
                case "Talk": StepTalk(a, st); break;
                case "Follow": StepFollow(a, st); break;
                case "Say": Say(a, st.Tag, st.Data, st.Actor); NextStep(a); break;
                case "Call": Crime.Exec(this, a, st); break;
                case "C_Tuck": case "C_Draw": case "C_Stash": case "C_Retrieve": case "C_Search": ConcealStep(a, st); break;   // --- concealment (Systems/Concealment.cs)
                default: Crime.Exec(this, a, st); break;
            }
        }

        /// <summary>Every action is a motion: what the body does while a step happens (steps that set their own animation keep it).
        /// Instant steps leave the body busy for a moment so the motion is seen before the next thing starts.</summary>
        static readonly Dictionary<string, (Anim anim, int hold)> StepMotion = new Dictionary<string, (Anim, int)>
        {
            ["PickUp"] = (Anim.PickUp, 6), ["GetItem"] = (Anim.PickUp, 6), ["PlanPick"] = (Anim.PickUp, 6), ["SP_TakeDecoy"] = (Anim.PickUp, 6), ["X_Retrieve"] = (Anim.PickUp, 6), ["X_TakeOwn"] = (Anim.PickUp, 6),
            ["Drop"] = (Anim.PutDown, 6), ["X_Drop"] = (Anim.PutDown, 6), ["X_DropNote"] = (Anim.PutDown, 6), ["HideWeapon"] = (Anim.PutDown, 8), ["PlanHideWeapon"] = (Anim.PutDown, 8), ["SP_Plant"] = (Anim.PutDown, 8), ["PlanReturnWeapon"] = (Anim.PutDown, 6), ["X_LeaveLoan"] = (Anim.PutDown, 6),
            ["X_Hand"] = (Anim.Present, 8), ["X_GiveNote"] = (Anim.Present, 8), ["X_Courier"] = (Anim.Present, 8),
            ["WashWeapon"] = (Anim.Wash, 0), ["PlanWash"] = (Anim.Wash, 0), ["CleanUp"] = (Anim.Clean, 0),
            ["PutOn"] = (Anim.Use, 10), ["TakeOff"] = (Anim.Use, 10), ["PlanPutOn"] = (Anim.Use, 10), ["PlanTakeOff"] = (Anim.Use, 10), ["Raincoat"] = (Anim.Use, 10), ["Scarf"] = (Anim.Use, 8),
            ["SP_Stoke"] = (Anim.Use, 10), ["SP_Dose"] = (Anim.Use, 6), ["Poison"] = (Anim.Use, 6), ["SP_Seal"] = (Anim.Use, 12), ["Seal"] = (Anim.Use, 12), ["X_Seal"] = (Anim.Use, 12),
            ["SP_Message"] = (Anim.Write, 12), ["X_Message"] = (Anim.Write, 12), ["Message"] = (Anim.Write, 12), ["Notebook"] = (Anim.Write, 0), ["Book"] = (Anim.Read, 0),
            ["X_ArmTrap"] = (Anim.Operate, 12), ["Trap"] = (Anim.Operate, 12), ["PlanArm"] = (Anim.Operate, 12), ["X_Arm"] = (Anim.Operate, 12), ["X_Disarm"] = (Anim.Operate, 12),
            ["CutPower"] = (Anim.Operate, 10), ["RestorePower"] = (Anim.Operate, 10), ["X_Power"] = (Anim.Operate, 10), ["PlanCut"] = (Anim.Operate, 10), ["PlanRestore"] = (Anim.Operate, 10), ["X_UsePower"] = (Anim.Operate, 8),
            ["ArmPress"] = (Anim.Operate, 12), ["Press"] = (Anim.Operate, 10), ["Recorder"] = (Anim.Operate, 10), ["X_Record"] = (Anim.Operate, 10), ["X_RecordSelf"] = (Anim.Talk, 10), ["X_EchoRec"] = (Anim.Operate, 8), ["X_EchoRecord"] = (Anim.Operate, 8),
            ["X_Grab"] = (Anim.Carry, 6), ["SP_Grab"] = (Anim.Carry, 6), ["PlanGrab"] = (Anim.Carry, 6), ["SP_Release"] = (Anim.PutDown, 8), ["PlanRelease"] = (Anim.PutDown, 8),
            ["X_Mutilate"] = (Anim.Stab, 10), ["X_Deface"] = (Anim.Stab, 10), ["X_Swap"] = (Anim.PutDown, 8), ["X_Tod"] = (Anim.Carry, 6),
            ["LockRoom"] = (Anim.Use, 6), ["PlanLockRoom"] = (Anim.Use, 6), ["Camera"] = (Anim.Photo, 8), ["Flashlight"] = (Anim.Use, 4), ["Thermos"] = (Anim.Drink, 10),
            ["X_Glance"] = (Anim.Search, 6), ["Examine"] = (Anim.Examine, 0), ["X_RepairStep"] = (Anim.Craft, 0),
            // second-wave methods (Methods*.cs): the draught, the planted dose, the key under the door, the forged note, the rooms that help
            ["X_MSedate"] = (Anim.Use, 6), ["X_MPlant"] = (Anim.Use, 10), ["X_MKeySlide"] = (Anim.Use, 12), ["X_MNote"] = (Anim.Write, 12),
            ["X_MBurn"] = (Anim.Use, 12), ["X_MDump"] = (Anim.PutDown, 8), ["X_MBury"] = (Anim.Garden, 12), ["X_MNoise"] = (Anim.Operate, 10), ["X_MDoze"] = (Anim.Sleep, 0),
            ["X_MSawPick"] = (Anim.PickUp, 4), ["X_MDismember"] = (Anim.Slash, 14), ["X_MPartPick"] = (Anim.PickUp, 4), ["X_MHidePart"] = (Anim.Hide, 8),
            // violence track (Sim/Violence): loading a gun, tying and untying someone, straining against bonds, freeing a bound person
            ["X_MLoad"] = (Anim.Use, 0), ["X_MBind"] = (Anim.Use, 0), ["X_MUnbind"] = (Anim.Use, 0), ["X_Bound"] = (Anim.Struggle, 0), ["X_Untie"] = (Anim.Use, 0), ["PlanPickRope"] = (Anim.PickUp, 6),
        };
        void StepActivity(Actor a, ActionStep st)
        {
            a.Speed = 0;
            if (st.Spot >= 0 && a.Spot != st.Spot)
            {
                var sp = S.Layout.Spots[st.Spot];
                if (sp.Occupant != null && sp.Occupant != a.Id) { FailStep(a, "spot taken"); return; }
                sp.Occupant = a.Id; a.Spot = st.Spot;
                if (sp.OnFurniture) { a.Pos = sp.Pos; }
                a.Yaw = sp.Yaw;
                a.Pose = sp.Tag == "sleep" ? Pose.Sleep : sp.Tag == "rest" ? Pose.LieBack : (sp.Tag == "sit" || sp.Tag == "eat" || sp.Tag == "read" && sp.OnFurniture || sp.Tag == "pray" && sp.OnFurniture) ? Pose.Sit : Pose.Stand;
                if (sp.Tag == "garden") a.Pose = Pose.Crouch;
            }
            a.Anim = st.Anim;
            LifeTick(a, st);
            if (S.Clock >= a.Act.StepEnd) NextStep(a);
        }

        // ------------------------------------------------------------------ path following
        public Func<int, float> DoorCostFor(Actor a)
        {
            var k = S.K(a.Id);
            return d =>
            {
                var door = S.Layout.Doors[d];
                if (door.Blocked || door.Sealed) return -1;
                bool believedLocked = k.DoorLocked.TryGetValue(d, out var l) ? l : false;
                if (a.IsButler) return 0.2f;
                if (believedLocked && !HasKey(a, door)) return -1;
                if (door.Locked && !believedLocked) return 0.5f; // doesn't know yet — will find out on arrival
                return 0.3f;
            };
        }

        public bool HasKey(Actor a, Door d)
        {
            if (d.KeyId == null) return false;
            // (perf) no Concat/array/Split: called from the path search's door cost for every believed-locked door it meets
            foreach (var id in a.Pocket) if (Opens(S.I(id), d.KeyId)) return true;
            return Opens(S.I(a.HandL), d.KeyId) || Opens(S.I(a.HandR), d.KeyId);
        }
        /// <summary>Whether an item's key list ("key_a;key_b") has the door's key (or the master key for a "key_" door). Same
        /// segments as KeyFor.Split(';').</summary>
        static bool Opens(Item it, string keyId)
        {
            var s = it?.KeyFor; if (s == null) return false;
            for (int i = 0; ; )
            {
                int j = s.IndexOf(';', i), len = (j < 0 ? s.Length : j) - i;
                if (KeySeg(s, i, len, keyId) || (KeySeg(s, i, len, "key_master") && keyId.StartsWith("key_"))) return true;
                if (j < 0) return false;
                i = j + 1;
            }
        }
        static bool KeySeg(string s, int i, int len, string v) => len == v.Length && string.CompareOrdinal(s, i, v, 0, len) == 0;

        void StepGoTo(Actor a, ActionStep st)
        {
            var act = a.Act;
            if (act.Path == null)
            {
                if (a.Pos.DistXZ(st.Target) < 0.25f && a.Pos.f == st.Target.f) { a.Speed = 0; NextStep(a); return; }
                var pr = Pathfinder.Find(S.Layout, a.Pos, st.Target, DoorCostFor(a));
                if (!pr.Ok) { FailStep(a, "no path from " + S.RoomName(a.Room) + " to " + S.RoomName(S.Layout.RoomAt(st.Target))); return; }
                act.Path = pr.Points; act.PathDoors = pr.DoorAtPoint; act.PathStairs = pr.StairAtPoint; act.PathIdx = 1;
                if (a.Pose != Pose.Stand && a.Pose != Pose.Crouch) { ReleaseSpot(a); a.Pose = Pose.Stand; }
            }
            if (act.PathIdx >= act.Path.Count) { a.Speed = 0; NextStep(a); return; }
            var target = act.Path[act.PathIdx];
            int stair = act.PathStairs[act.PathIdx];
            if (stair >= 0)
            {
                var s = S.Layout.Stairs[stair];
                a.StairId = stair; a.StairFrom = a.Pos; a.StairTo = target; a.StairUntil = 0; act.PathIdx++;
                return;
            }
            int door = act.PathDoors[act.PathIdx];
            if (door >= 0)
            {
                var d = S.Layout.Doors[door];
                if (a.Pos.DistXZ(d.Pos) < 1.0f)
                {
                    if (!DoorPass(a, d)) return; // waiting on door op or failed
                }
            }
            bool run = st.Run || a.Running;
            float mob = Math.Max(0.15f, a.Body.Mobility);
            float spd = (run ? RunSpeed : WalkSpeed) * mob * (a.Carrying != null ? Violence.CarrySpeed(S, a) : 1f);   // (violence track: carried 0.5, dragged 0.28)
            if (a.Status == ActorStatus.Unconscious) spd = 0;
            float step = spd * SimTime.Dt;
            float dx = target.x - a.Pos.x, dz = target.z - a.Pos.z; float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            a.Anim = a.Carrying != null ? Violence.CarryAnim(S, a) : Anim.None; a.Running = run; a.Speed = spd;   // (violence track: a body over the shoulder, or dragged)
            if (dist > 0.01f) a.Yaw = MathX.AngleDeg(dx, dz);
            if (dist <= step) { a.Pos = new P3(target.f, target.x, target.z); act.PathIdx++; }
            else a.Pos = new P3(a.Pos.f, a.Pos.x + dx / dist * step, a.Pos.z + dz / dist * step);
            UpdateRoom(a);
            if (a.Carrying != null) { var c = S.A(a.Carrying); if (c != null) { c.Pos = a.Pos; c.Room = a.Room; } }
            if (run && S.Tick % 4 == 0) Sound(SoundKind.Running, a.Pos, 0.35f, a.Id);
            if (act.PathIdx >= act.Path.Count) { a.Speed = 0; a.Running = false; NextStep(a); }
        }

        void StairMove(Actor a)
        {
            var s = S.Layout.Stairs[a.StairId];
            float mob = Math.Max(0.2f, a.Body.Mobility);
            a.StairUntil += SimTime.Dt / (s.Seconds / mob * (a.Carrying != null ? Violence.CarryStairs(S, a) : 1f));   // (violence track: dragging up/down a stair is slow)
            float t = (float)Math.Min(1, a.StairUntil);
            a.Pos = P3.Lerp(a.StairFrom, a.StairTo, t); a.Speed = WalkSpeed;
            a.Yaw = MathX.AngleDeg(a.StairTo.x - a.StairFrom.x, a.StairTo.z - a.StairFrom.z);
            if (t >= 1) { a.Pos = a.StairTo; a.StairId = -1; a.StairUntil = 0; UpdateRoom(a); }
            if (a.Carrying != null) { var c = S.A(a.Carrying); if (c != null) { c.Pos = a.Pos; c.Room = a.Room; } }
        }

        public void UpdateRoom(Actor a)
        {
            int r = S.Layout.RoomAt(a.Pos);
            if (r >= 0 && r != a.Room)
            {
                int old = a.Room; a.Room = r;
                S.Log("Enter", a.Id, room: r, pos: a.Pos, secret: true);
                OnEnterRoom(a, old, r);
            }
        }

        /// <summary>Handle a door on the path. Returns true when the actor may continue moving.</summary>
        bool DoorPass(Actor a, Door d)
        {
            var k = S.K(a.Id);
            if (a.BusyUntil > S.Tick) { a.Speed = 0; return false; }
            if (d.FixedByAbility && d.FixedUntilTick > S.Tick) { k.DoorLocked[d.Id] = true; FailStep(a, "door fixed"); return false; }
            if (d.Locked)
            {
                if (HasKey(a, d))
                {
                    d.Locked = false; d.Open = true; a.BusyUntil = S.Tick + 8; a.Anim = Anim.OpenDoor;
                    S.Log("Unlock", a.Id, room: d.RoomA, data: "door" + d.Id, secret: false); d.LockLog.Add($"{ClockFmt.HM(S.Clock)} unlock {a.Id}");
                    DoorChanged(d, a); Sound(SoundKind.Door, d.Pos, 0.3f, a.Id);
                    k.DoorLocked[d.Id] = false;
                    if (!a.IsPlayer) PendingRelock(a, d);
                    return false;
                }
                k.DoorLocked[d.Id] = true;
                Sound(SoundKind.Knock, d.Pos, 0.25f, a.Id); // rattle
                S.Log("DoorRattle", a.Id, data: "door" + d.Id);
                // --- time-on-demand: someone tries the locked door of the room the (awake) player is in
                { var pl = S.Player; if (!a.IsPlayer && pl != null && pl.Alive && pl.Pose != Pose.Sleep && (d.RoomA == pl.Room || d.RoomB == pl.Room)) RaiseStop(StopKind.Knock, StopClass.Social, "누군가 문을 열려고 한다", S.RoomName(pl.Room), a.Id, pl.Room); }
                FailStep(a, "locked door"); return false;
            }
            if (!d.Open && Violence.HandsBound(S, a)) { FailStep(a, "hands bound"); return false; }   // --- violence track: tied wrists cannot turn a handle
            if (!d.Open)
            {
                d.Open = true; a.BusyUntil = S.Tick + 5; a.Anim = Anim.OpenDoor;
                DoorChanged(d, a); Sound(SoundKind.Door, d.Pos, 0.25f, a.Id);
                if (!a.Running && !a.IsButler) AutoClose(d, a);
                return false;
            }
            return true;
        }

        // door auto-close (the one who opened closes it after passing, unless fleeing)
        readonly Dictionary<int, (string actor, double at)> _closeQueue = new Dictionary<int, (string, double)>();
        readonly Dictionary<int, (string actor, double at)> _relockQueue = new Dictionary<int, (string, double)>();
        void AutoClose(Door d, Actor a) { _closeQueue[d.Id] = (a.Id, S.Tick + 18); }
        void PendingRelock(Actor a, Door d) { if (a.IsButler || d.NightPolicy) return; _relockQueue[d.Id] = (a.Id, S.Tick + 25); }

        void Facilities()
        {
            if (_closeQueue.Count > 0)
            {
                foreach (var kv in _closeQueue.ToList())
                {
                    if (S.Tick < kv.Value.at) continue;
                    var d = S.Layout.Doors[kv.Key]; _closeQueue.Remove(kv.Key);
                    if (!d.Open) continue;
                    // don't close on someone standing in the doorway
                    if (S.Actors.Values.Any(x => x.Alive && x.Pos.f == d.Pos.f && x.Pos.DistXZ(d.Pos) < 0.7f)) { _closeQueue[kv.Key] = (kv.Value.actor, S.Tick + 10); continue; }
                    d.Open = false; DoorChanged(d, S.A(kv.Value.actor)); Sound(SoundKind.Door, d.Pos, 0.2f, kv.Value.actor);
                }
            }
            if (_relockQueue.Count > 0)
            {
                foreach (var kv in _relockQueue.ToList())
                {
                    if (S.Tick < kv.Value.at) continue; _relockQueue.Remove(kv.Key);
                    var d = S.Layout.Doors[kv.Key]; var a = S.A(kv.Value.actor);
                    if (a == null || !a.Alive || d.Locked) continue;
                    // only the owner relocks their own bedroom from inside or when leaving
                    var bed = S.Layout.BedroomOf(a.Id);
                    if (bed != null && (d.RoomA == bed.Id || d.RoomB == bed.Id) && a.Pos.DistXZ(d.Pos) < 3f && (S.IsNight || a.Needs.Fear > 0.4f))
                    { d.Open = false; d.Locked = true; d.LockLog.Add($"{ClockFmt.HM(S.Clock)} lock {a.Id}"); S.Log("Lock", a.Id, data: "door" + d.Id); DoorChanged(d, a); Sound(SoundKind.Door, d.Pos, 0.2f, a.Id); }
                }
            }
            Crime.Facilities(this);
            ConcealTick();   // --- concealment: investigators go through hiding places (Systems/Concealment.cs)
        }

        public void DoorChanged(Door d, Actor by)
        {
            S.Emit(GameEventType.Door, by?.Id, id: d.Id, value: (d.Open ? 1 : 0) + (d.Locked ? 2 : 0), pos: d.Pos);
        }

        public void SetDoor(Actor by, Door d, bool? open, bool? locked, string why = null)
        {
            if (open.HasValue) d.Open = open.Value;
            if (locked.HasValue) { d.Locked = locked.Value; if (d.Locked) d.Open = false; d.LockLog.Add($"{ClockFmt.HM(S.Clock)} {(d.Locked ? "lock" : "unlock")} {by?.Id} {why}"); S.Log(d.Locked ? "Lock" : "Unlock", by?.Id, data: "door" + d.Id + " " + why); }
            DoorChanged(d, by); Sound(SoundKind.Door, d.Pos, 0.25f, by?.Id);
        }

        // ------------------------------------------------------------------ items
        void StepPickUp(Actor a, ActionStep st)
        {
            var it = S.I(st.Item);
            if (it == null || it.Holder != null) { FailStep(a, "item gone"); return; }
            if (it.Pos.f != a.Pos.f || it.Pos.DistXZ(a.Pos) > 1.6f) { FailStep(a, "item not here"); return; }
            PickUp(a, it); a.Anim = Anim.PickUp; a.BusyUntil = S.Tick + 5; NextStep(a);
        }

        public bool PickUp(Actor a, Item it)
        {
            Violence.Unstick(S, it);   // --- violence track: a bolt pulled out of a wall / furniture is an ordinary item again
            var def = it.Def; bool small = def != null && def.Size <= 0.12f;
            if (small) { a.Pocket.Add(it.Id); }
            else
            {
                if (a.HandR == null && a.Body.HandR > 0.3f) a.HandR = it.Id;
                else if (a.HandL == null && a.Body.HandL > 0.3f) a.HandL = it.Id;
                else if (a.HandR != null) { DropItem(a, S.I(a.HandR), a.Pos); a.HandR = it.Id; }
                else return false;
            }
            it.Holder = a.Id; it.Room = -1; it.LastUser = a.Id; it.LastMovedTick = S.Tick;
            ConcealOnPickUp(a, it);   // --- concealment: out of its hiding place, not yet worn anywhere
            S.Log("PickUp", a.Id, item: it.Id, room: a.Room, pos: a.Pos, data: it.Type, secret: true);
            S.Emit(GameEventType.ItemMoved, a.Id, id: -1, data: it.Id, text: "pickup", pos: a.Pos);
            return true;
        }

        void StepDrop(Actor a, ActionStep st)
        {
            var it = S.I(st.Item);
            if (it == null || it.Holder != a.Id) { NextStep(a); return; }
            DropItem(a, it, st.HasTarget ? st.Target : a.Pos, st.Tag == "hide");
            a.Anim = Anim.PutDown; NextStep(a);
        }

        public void DropItem(Actor a, Item it, P3 at, bool hidden = false)
        {
            if (it == null) return;
            if (a.HandR == it.Id) a.HandR = null; if (a.HandL == it.Id) a.HandL = null; a.Pocket.Remove(it.Id);
            it.Holder = null; it.Pos = at; it.Room = S.Layout.RoomAt(at); it.Hidden = hidden; it.LastMovedTick = S.Tick;
            ConcealOnDrop(a, it, hidden);   // --- concealment: a thing hidden by a hiding place is kept in that place (Systems/Concealment.cs)
            S.Log(hidden ? "HideItem" : "Drop", a.Id, item: it.Id, room: it.Room, pos: at, data: it.Type, secret: true);
            S.Emit(GameEventType.ItemMoved, a.Id, data: it.Id, text: hidden ? "hide" : "drop", pos: at);
        }

        public Item Held(Actor a, Func<ItemDef, bool> pred = null)
        {
            // (perf) right hand, then left, without an array: perception asks this for every person it sees
            var it = S.I(a.HandR); if (it != null && (pred == null || pred(it.Def))) return it;
            it = S.I(a.HandL); if (it != null && (pred == null || pred(it.Def))) return it;
            return null;
        }

        public IEnumerable<Item> Carried(Actor a)
        {
            foreach (var id in new[] { a.HandR, a.HandL }) { var it = S.I(id); if (it != null) yield return it; }
            foreach (var id in a.Pocket) { var it = S.I(id); if (it != null) yield return it; }
        }

        // ------------------------------------------------------------------ doors as explicit steps (lock/unlock/knock)
        void StepDoor(Actor a, ActionStep st)
        {
            var d = S.Layout.Doors[st.Door];
            if (a.Pos.DistXZ(d.Pos) > 1.6f) { FailStep(a, "door far"); return; }
            switch (st.Tag)
            {
                case "lock": if (HasKey(a, d) || a.IsButler) SetDoor(a, d, false, true, st.Data); else { FailStep(a, "no key"); return; } break;
                case "latch": if (d.Lockable) SetDoor(a, d, false, true, st.Data); break;   // thumb-turn from the inside: no key needed
                case "unlock": if (HasKey(a, d) || a.IsButler) SetDoor(a, d, null, false, st.Data); else { FailStep(a, "no key"); return; } break;
                case "open": if (!d.Locked) SetDoor(a, d, true, null); break;
                case "close": SetDoor(a, d, false, null); break;
                case "knock": Sound(SoundKind.Knock, d.Pos, 0.45f, a.Id); S.Log("Knock", a.Id, data: "door" + d.Id); break;
            }
            a.Anim = st.Tag == "knock" ? Anim.Knock : Anim.OpenDoor; a.BusyUntil = S.Tick + 5;
            NextStep(a);
        }

        // ------------------------------------------------------------------ following (companion / stalking)
        void StepFollow(Actor a, ActionStep st)
        {
            var t = S.A(st.Actor);
            if (t == null || !t.Alive) { NextStep(a); return; }
            if (a.Act.StepEnd > 0 && S.Clock >= a.Act.StepEnd) { NextStep(a); return; }
            float keep = st.Tag == "stalk" ? 7f : 1.8f;
            float d = a.Pos.Dist(t.Pos);
            if (d > keep + 0.8f)
            {
                if (a.Act.Path == null || S.Tick % 20 == 0)
                {
                    var pr = Pathfinder.Find(S.Layout, a.Pos, t.Pos, DoorCostFor(a));
                    if (!pr.Ok) { FailStep(a, "follow lost"); return; }
                    a.Act.Path = pr.Points; a.Act.PathDoors = pr.DoorAtPoint; a.Act.PathStairs = pr.StairAtPoint; a.Act.PathIdx = 1;
                }
                // reuse goto mover
                var tmp = new ActionStep { Kind = "GoTo", Target = t.Pos, HasTarget = true, Run = d > 9 };
                MoveAlong(a, tmp);
            }
            else { a.Speed = 0; a.Act.Path = null; a.Yaw = MathX.AngleDeg(t.Pos.x - a.Pos.x, t.Pos.z - a.Pos.z); }
        }

        void MoveAlong(Actor a, ActionStep st)
        {
            if (a.PhysicsDriven) { a.Speed = 0; a.Running = false; return; }
            var act = a.Act; if (act.Path == null || act.PathIdx >= act.Path.Count) return;
            var target = act.Path[act.PathIdx];
            int stair = act.PathStairs[act.PathIdx];
            if (stair >= 0) { a.StairId = stair; a.StairFrom = a.Pos; a.StairTo = target; a.StairUntil = 0; act.PathIdx++; return; }
            int door = act.PathDoors[act.PathIdx];
            if (door >= 0) { var d = S.Layout.Doors[door]; if (a.Pos.DistXZ(d.Pos) < 1.0f && !DoorPass(a, d)) return; }
            float spd = (st.Run ? RunSpeed : WalkSpeed) * Math.Max(0.05f, Math.Min(a.Body.Mobility, a.Physical?.Mobility ?? 1f));
            float step = spd * SimTime.Dt; float dx = target.x - a.Pos.x, dz = target.z - a.Pos.z; float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            a.Speed = spd; a.Running = st.Run; if (dist > 0.01f) a.Yaw = MathX.AngleDeg(dx, dz);
            if (dist <= step) { a.Pos = target; act.PathIdx++; } else a.Pos = new P3(a.Pos.f, a.Pos.x + dx / dist * step, a.Pos.z + dz / dist * step);
            UpdateRoom(a);
        }

        void FollowUpdate()
        {
            // companions assigned by the player: keep them near unless they have urgent needs
            foreach (var a in S.Actors.Values)
            {
                if (a.Following == null || !a.Alive || a.IsPlayer) continue;
                var t = S.A(a.Following); if (t == null || !t.Alive) { a.Following = null; continue; }
                if (a.Act == null || a.Act.Id != "follow")
                {
                    if (a.Act != null && !a.Act.Interruptible) continue;
                    var act = new Activity { Id = "follow", Label = Cast.GivenOf(t.Id) + "하고 동행", Priority = 50 };
                    act.Steps.Add(new ActionStep { Kind = "Follow", Actor = t.Id, Tag = "companion" });
                    Assign(a, act);
                }
            }
        }
    }
}
