using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Violence track probe (full mode). At "daily" a non-lethal showcase so the chapter still plays out: a cord from behind
    /// and a drowning taken to unconsciousness and then let go, a restraint, a revolver shot into a wall and one that wounds,
    /// a crossbow bolt in a wall and in a leg, a body dragged by someone too weak to lift it, and the first-person hands
    /// (reload, aim, recoil, the crossbow crank, a stab). At "after" (the next chapter) the lethal ones and the body card.
    /// Every step is guarded: a failure is logged and the tour goes on.
    /// </summary>
    public sealed partial class AutoProbe
    {
        partial void ViolenceHook(string at, List<IEnumerator> run)
        {
            if (at == "daily") run.Add(Guarded(ViolenceDaily(), "violence daily"));
            else if (at == "after") run.Add(Guarded(ViolenceAfter(), "violence after"));
        }

        IEnumerator Guarded(IEnumerator inner, string name)
        {
            while (true)
            {
                object cur;
                try { if (!inner.MoveNext()) break; cur = inner.Current; }
                catch (Exception e) { Log($"{name} step failed: {e.GetType().Name} {e.Message}\n{e.StackTrace?.Split('\n').FirstOrDefault()}"); break; }
                yield return cur;
            }
            VCamOff(); ViolUnfreeze(); FirstPersonHands.ProbeAim = false; Ses.Speed = 1f;
        }

        // ================================================================== staging helpers
        Camera _vcam; readonly List<Actor> _vFrozen = new List<Actor>(); readonly List<ActorView> _vShown = new List<ActorView>();
        void VCam(Vector3 from, Vector3 look)
        {
            if (_vcam == null) { var go = new GameObject("ProbeViolenceCam"); _vcam = go.AddComponent<Camera>(); _vcam.depth = 60; _vcam.fieldOfView = 44; _vcam.nearClipPlane = 0.04f; MansionAtmosphereSetup(_vcam); }
            _vcam.enabled = true; _vcam.transform.position = from; _vcam.transform.LookAt(look);
            Ses.World.Mansion?.Cull(from);
        }
        static void MansionAtmosphereSetup(Camera c) { try { BL23.Game.Mansion.MansionAtmosphere.SetupCamera(c); } catch (Exception) { } }
        void VCamOff() { if (_vcam != null) _vcam.enabled = false; foreach (var v in _vShown) if (v != null) v.ForceVisible = false; _vShown.Clear(); }
        /// <summary>A side-on view of two people (or one), at the given height, the given distance away.</summary>
        void ViewPair(Actor a, Actor b, float height = 1.35f, float dist = 2.7f, float side = 1f)
        {
            var va = Ses.World.ViewOf(a.Id); var vb = b != null ? Ses.World.ViewOf(b.Id) : null; if (va == null) return;
            var mid = vb != null ? (va.transform.position + vb.transform.position) * 0.5f : va.transform.position;
            var axis = vb != null ? vb.transform.position - va.transform.position : va.transform.forward; axis.y = 0f; if (axis.sqrMagnitude < 1e-4f) axis = Vector3.forward;
            var perp = Vector3.Cross(Vector3.up, axis.normalized) * side;
            var from = mid + perp * dist + Vector3.up * height;
            // keep the camera inside the room: pull it in if a wall is in the way
            if (Physics.Raycast(mid + Vector3.up * height, (from - (mid + Vector3.up * height)).normalized, out var hit, dist, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<ActorView>() == null)
                from = hit.point - (from - (mid + Vector3.up * height)).normalized * 0.25f;
            VCam(from, mid + Vector3.up * 0.9f);
            foreach (var v in new[] { va, vb }) if (v != null && !_vShown.Contains(v)) { v.ForceVisible = true; _vShown.Add(v); }
        }
        void ViolFreeze(Actor a) { a.Act = null; a.NextThink = S.Clock + 99999; a.Speed = 0; a.TalkingTo = null; a.Following = null; if (!_vFrozen.Contains(a)) _vFrozen.Add(a); S.K(a.Id).Open?.Clear(); }
        void ViolUnfreeze() { foreach (var a in _vFrozen) if (a.Alive) a.NextThink = S.Clock + 0.3; _vFrozen.Clear(); }
        void ViolPut(Actor a, P3 p, float yaw) { a.Pos = p; a.Room = S.Layout.RoomAt(p); a.Yaw = yaw; if (a.Status == ActorStatus.Active) a.Pose = BL23.Sim.Pose.Stand; Ses.World.ViewOf(a.Id)?.Snap(); }
        Room ViolRoom(params RoomType[] types) { foreach (var t in types) { var r = S.Layout.Rooms.Where(x => x.Type == t && !x.Void && x.Floor == 0).OrderBy(x => x.Id).FirstOrDefault() ?? S.Layout.Rooms.Where(x => x.Type == t && !x.Void).OrderBy(x => x.Id).FirstOrDefault(); if (r != null) return r; } return S.Layout.Rooms.First(x => x.Type == RoomType.GrandHall); }
        P3 ViolCell(Room r, int k)
        {
            var g = S.Layout.Nav(r.Floor); var mid = new P3(r.Floor, r.Rect.CX, r.Rect.CZ); var cells = new List<int>();
            for (int c = 0; c < g.Room.Length; c++) if (g.Room[c] == r.Id && g.Walkable(c) && g.InMain(c)) cells.Add(c);
            if (cells.Count == 0) return mid;
            cells = cells.OrderBy(c => g.Center(c).DistXZ(mid)).ToList(); return g.Center(cells[Math.Min(k, cells.Count - 1)]);
        }
        /// <summary>Nobody else in the room (they wait in the hall for a minute), the cast held still.</summary>
        void ViolStage(Room room, params Actor[] cast)
        {
            var hall = S.Layout.Rooms.First(x => x.Type == RoomType.GrandHall && x.Floor == 0);
            foreach (var x in S.LivingNpcs.ToList())
            {
                if (cast.Contains(x)) continue;
                if (x.Room == room.Id || x.Pos.f == room.Floor && x.Pos.DistXZ(new P3(room.Floor, room.Rect.CX, room.Rect.CZ)) < 14f) { ViolPut(x, Ses.Sim.RandomPointIn(hall, S.R(BL23.Sim.Stream.Presentation)), 0f); ViolFreeze(x); }
            }
            foreach (var c in cast) ViolFreeze(c);
            // the player is a witness too: wait in the hall (the probe camera does the looking)
            var me = S.Player;
            if (me != null && Ses.Player != null && !cast.Contains(me) && (me.Room == room.Id || me.Pos.f == room.Floor && me.Pos.DistXZ(new P3(room.Floor, room.Rect.CX, room.Rect.CZ)) < 14f))
                Ses.Player.Teleport(Ses.Sim.RandomPointIn(hall, S.R(BL23.Sim.Stream.Presentation)), 0f, 0f);
        }
        Item ViolGive(Actor a, string type)
        {
            var it = new Item { Id = S.NewId("it"), Type = type, Holder = a.Id, Room = -1 };
            S.Items[it.Id] = it;
            if (ItemCatalog.Get(type).Size <= 0.12f) a.Pocket.Add(it.Id); else { if (a.HandR != null) Ses.Sim.DropItem(a, S.I(a.HandR), a.Pos); a.HandR = it.Id; }
            S.Emit(GameEventType.ItemMoved, a.Id, data: it.Id, text: "pickup", pos: a.Pos);
            return it;
        }
        void ViolAttack(Actor a, Actor t, string tag) => Ses.Sim.Assign(a, new Activity { Id = "probe:attack", Label = "probe", Priority = 100, Interruptible = false, Steps = { new ActionStep { Kind = "Attack", Actor = t.Id, Tag = tag } } });
        static P3 Behind(P3 p, float yaw, float d) { double r = yaw * Math.PI / 180; return new P3(p.f, p.x - (float)Math.Sin(r) * d, p.z - (float)Math.Cos(r) * d); }

        /// <summary>Stage a hold in `room` and photograph its phases; let go at unconsciousness unless `lethal`.</summary>
        IEnumerator ViolHold(Actor a, Actor t, Room room, string tag, string name, bool lethal, P3? at = null, float yaw = 0f)
        {
            ViolStage(room, a, t);
            var p = at ?? ViolCell(room, 3); ViolPut(t, p, yaw);
            var ap = Ses.Sim.SnapPublic(Behind(p, yaw, tag == "drown" ? 0.9f : 0.5f)); ViolPut(a, ap, yaw);
            if (tag == "strangle") ViolGive(a, "CurtainCord");
            ViolAttack(a, t, tag);
            yield return Until(() => Violence.HeldIn(S, t.Id) != null, 8, name + " seize");
            var x = Violence.HeldIn(S, t.Id); if (x == null) { Log($"{name}: no hold (attacker act {a.Act?.Id}/{a.Act?.Cur?.Kind})"); yield break; }
            ViewPair(a, t, 1.3f, 2.6f); yield return Wait(0.9f); yield return ShotCo(name + "_seize");
            Ses.Speed = 2f; yield return Until(() => x.Phase == AssaultPhase.Struggle && x.Intensity > 0.72f || !x.Active, 12, name + " struggle"); Ses.Speed = 1f;
            ViewPair(a, t, 1.25f, 2.4f, -1f); yield return Wait(0.35f); yield return ShotCo(name + "_struggle");
            yield return Wait(1.1f); ViewPair(a, t, 1.7f, 2.2f); yield return ShotCo(name + "_struggle2");
            Ses.Speed = 3f; yield return Until(() => x.Phase >= AssaultPhase.Weaken || !x.Active, 50, name + " weaken"); Ses.Speed = 1f;
            ViewPair(a, t, 1.1f, 2.3f); yield return Wait(1.2f); yield return ShotCo(name + "_weaken");
            Ses.Speed = 3f; yield return Until(() => x.Phase >= AssaultPhase.Unconscious || !x.Active, 30, name + " unconscious"); Ses.Speed = 1f;
            yield return Wait(1.6f); ViewPair(a, t, 0.9f, 2.2f); yield return ShotCo(name + "_limp");
            if (!lethal) { if (x.Active) Violence.Release(Ses.Sim, x, "probe: let go"); }
            else { Ses.Speed = 3f; yield return Until(() => !x.Active, 60, name + " death"); Ses.Speed = 1f; yield return Wait(2.2f); ViewPair(t, null, 1.1f, 2f); yield return ShotCo(name + "_dead"); }
            Log($"{name}: {x.Kind} {x.Phase} ({x.Outcome}) | {string.Join(" | ", x.Log)}");
        }

        // ================================================================== daily: everyone survives
        IEnumerator ViolenceDaily()
        {
            var npcs = S.LivingNpcs.Where(a => a.Status == ActorStatus.Active && !a.IsButler).OrderBy(a => a.Id).ToList();
            if (npcs.Count < 9) { Log("violence probe: too few people"); yield break; }
            Log("violence probe (daily): holds, restraint, guns, drag, first-person hands");
            var strong = npcs.OrderByDescending(Violence.Strength).ToList();
            Actor A(int i) => strong[Math.Min(i, strong.Count - 1)];
            Actor V(int i) => strong[Math.Max(0, strong.Count - 1 - i)];
            // 1. a cord from behind, in the lounge, to unconsciousness
            var lounge = ViolRoom(RoomType.Lounge, RoomType.Parlor, RoomType.Library);
            yield return ViolHold(A(0), V(0), lounge, "strangle", "viol_garrote", false);
            ViolUnfreeze();
            // 2. drowning at the pool by the hair
            var pool = S.Layout.Rooms.FirstOrDefault(r => r.Type == RoomType.Pool);
            var water = pool?.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(f => f.Type == "PoolWater");
            if (water != null)
            {
                var at = Ses.Sim.SnapPublic(new P3(water.Pos.f, water.Pos.x + water.W / 2 + 1.3f, water.Pos.z));
                yield return ViolHold(A(1), V(1), pool, "drown", "viol_drown", false, at, -90f);
                ViolUnfreeze();
            }
            // 3. a restraint: knocked out, tied, wakes and strains at the rope
            {
                var a = A(2); var t = V(2); var room = ViolRoom(RoomType.Storage, RoomType.Study, RoomType.GuestRoom);
                ViolStage(room, a, t); var p = ViolCell(room, 1); ViolPut(t, p, 0f); ViolPut(a, Ses.Sim.SnapPublic(new P3(p.f, p.x + 0.7f, p.z)), -90f);
                Ses.Sim.Collapse(t, "probe"); t.Body.UnconsciousUntil = S.Clock + 1.2;
                var rope = ViolGive(a, "Rope");
                var b = Violence.CanBind(S, a, t, null, out var why) ? Violence.Bind(Ses.Sim, a, t, rope, true, true, true, "probe") : null;
                Log($"restraint: {(b != null ? b.Material + " tight " + b.Tight.ToString("0.00") : "refused " + why)}");
                ViewPair(t, a, 1.2f, 1.8f); yield return Wait(1.2f); yield return ShotCo("viol_bound_down");
                yield return Until(() => t.Status == ActorStatus.Active, 12, "bound wakes"); yield return Wait(2.5f);
                ViewPair(t, null, 1.3f, 1.7f); yield return ShotCo("viol_bound_struggle");
                ViolUnfreeze();
            }
            // 4. the revolver: into the wall (flash, the hole), then a wounding shot
            {
                var a = A(3); var t = V(3); var room = ViolRoom(RoomType.Study, RoomType.Library, RoomType.Lounge);
                ViolStage(room, a, t);
                var p = ViolCell(room, 0); ViolPut(a, p, 0f);
                var gun = ViolGive(a, "Revolver"); ViolGive(a, "Cartridges"); Violence.Load(Ses.Sim, a, gun);
                // the nearest wall ahead
                float bestYaw = 0f, bestD = 99f; var g = S.Layout.Nav(p.f);
                for (int k = 0; k < 8; k++) { float yaw = k * 45f; double r = yaw * Math.PI / 180; for (float d = 0.5f; d < 9f; d += 0.25f) { int c = g.CellOf(p.x + (float)Math.Sin(r) * d, p.z + (float)Math.Cos(r) * d); if (c < 0 || g.Room[c] != room.Id) { if (d < bestD && d > 2f) { bestD = d; bestYaw = yaw; } break; } } }
                a.Yaw = bestYaw; Ses.World.ViewOf(a.Id)?.Snap();
                var side = Quaternion.Euler(0, bestYaw + 90f, 0) * Vector3.forward; var av = Ses.World.ViewOf(a.Id);
                VCam(av.transform.position + side * 1.8f + Vector3.up * 1.5f + Quaternion.Euler(0, bestYaw, 0) * Vector3.forward * 0.8f, av.transform.position + Vector3.up * 1.3f + Quaternion.Euler(0, bestYaw, 0) * Vector3.forward * 0.6f);
                _vShown.Add(av); av.ForceVisible = true;
                yield return Wait(0.8f);
                a.Anim = BL23.Sim.Anim.Point; S.Emit(GameEventType.Anim, a.Id, null, text: "Revolver", data: "aim|" + gun.Id, pos: a.Pos);
                yield return Wait(1.0f); yield return ShotCo("viol_revolver_aim");
                var shot = Violence.Fire(Ses.Sim, a, gun, Violence.MuzzleY(a), bestYaw, 0f, null);
                yield return null; yield return ShotCo("viol_revolver_flash");
                Log("revolver shot: " + string.Join(" ; ", shot?.Impacts.Select(i => i.Kind + (i.Furniture >= 0 ? ":" + S.Layout.Furniture[i.Furniture].Type : "") + $" @{i.Dist:0.0}m") ?? new string[0]));
                yield return Wait(0.8f);
                var hole = S.Traces.LastOrDefault(tr => tr.Type == "BulletHole");
                if (hole != null) { var hp = Ses.World.ToWorld(hole.Pos) + Vector3.up * 1.2f; var back = Quaternion.Euler(0, hole.Dir, 0) * Vector3.back; VCam(hp + back * 0.9f + Vector3.up * 0.1f, hp); yield return Wait(0.4f); yield return ShotCo("viol_bullet_hole"); }
                // a wounding shot: the right shoulder at four metres
                var q = Ses.Sim.SnapPublic(new P3(p.f, p.x + (float)Math.Sin(bestYaw * Math.PI / 180) * 3.5f, p.z + (float)Math.Cos(bestYaw * Math.PI / 180) * 3.5f));
                ViolPut(t, q, bestYaw + 180f); yield return Wait(0.6f);
                float ty = 1.1f; var rgt = Quaternion.Euler(0, bestYaw + 90f, 0) * Vector3.forward * 0.12f;
                float aimYaw = MathX.AngleDeg(t.Pos.x + rgt.x - a.Pos.x, t.Pos.z + rgt.z - a.Pos.z), dist = a.Pos.DistXZ(t.Pos);
                ViewPair(a, t, 1.4f, 3.2f);
                var shot2 = Violence.Fire(Ses.Sim, a, gun, Violence.MuzzleY(a), aimYaw, (float)(Math.Atan2(ty - Violence.MuzzleY(a), dist) * 180 / Math.PI), t.Id);
                yield return null; yield return ShotCo("viol_revolver_hit_flash");
                yield return Wait(0.5f); yield return ShotCo("viol_revolver_hit");
                Log("wounding shot: " + string.Join(" ; ", shot2?.Impacts.Select(i => i.Kind + (i.Actor != null ? ":" + i.Actor + ":" + i.Region : "")) ?? new string[0]) + $" | victim {t.Status} bleed {t.Body.Bleed:0.00}");
                if (t.Alive) { Ses.Sim.FirstAid(a, t); t.Body.Stabilized = true; }
                ViolUnfreeze();
            }
            // 5. the crossbow: a bolt into the wall, one into a leg
            {
                var a = A(4); var t = V(4); var room = ViolRoom(RoomType.TrophyRoom, RoomType.Gallery, RoomType.Lounge);
                ViolStage(room, a, t);
                var p = ViolCell(room, 0); var q = ViolCell(room, 30); ViolPut(a, p, MathX.AngleDeg(q.x - p.x, q.z - p.z)); ViolPut(t, q, 0f);
                var bow = ViolGive(a, "Crossbow"); ViolGive(a, "BoltQuiver"); Violence.Load(Ses.Sim, a, bow);
                S.Emit(GameEventType.Anim, a.Id, null, text: "Crossbow", data: "reload|" + bow.Id, pos: a.Pos);
                ViewPair(a, null, 1.4f, 2.2f); yield return Wait(1.4f); yield return ShotCo("viol_crossbow_cock");
                float yawLeg = MathX.AngleDeg(t.Pos.x - a.Pos.x, t.Pos.z - a.Pos.z), dist = a.Pos.DistXZ(t.Pos);
                var shot = Violence.Fire(Ses.Sim, a, bow, Violence.MuzzleY(a), yawLeg, (float)(Math.Atan2(0.45f - Violence.MuzzleY(a), dist) * 180 / Math.PI), t.Id);
                yield return Wait(0.6f);
                Log("bolt: " + string.Join(" ; ", shot?.Impacts.Select(i => i.Kind + (i.Actor != null ? ":" + i.Actor + ":" + i.Region : "") + (i.Item != null ? " [" + i.Item + "]" : "")) ?? new string[0]));
                var bi = shot?.Impacts.FirstOrDefault(i => i.Item != null);
                if (bi != null) { var bp = Ses.World.ToWorld(bi.At) + Vector3.up * bi.Y; if (bi.Actor != null) ViewPair(t, null, 0.7f, 1.3f); else VCam(bp + Vector3.up * 0.3f + (Ses.World.ViewOf(a.Id).transform.position - bp).normalized * 1.1f, bp); yield return Wait(0.5f); yield return ShotCo("viol_bolt"); }
                if (t.Alive) { Ses.Sim.FirstAid(a, t); t.Body.Stabilized = true; }
                ViolUnfreeze();
            }
            // 6. a body too heavy to lift: dragged by the armpits
            {
                var body = S.Actors.Values.Where(x => x.Alive && x.Status == ActorStatus.Unconscious && x.CarriedBy == null).OrderByDescending(Violence.BodyMass).FirstOrDefault();
                var weak = npcs.Where(x => x.Alive && x.Status == ActorStatus.Active && x.Carrying == null).OrderBy(Violence.LiftCapacity).FirstOrDefault();
                if (body != null && weak != null)
                {
                    var room = S.Layout.Room(body.Room); if (room != null) ViolStage(room, weak);
                    ViolPut(weak, Ses.Sim.SnapPublic(body.Pos), 0f);
                    weak.Carrying = body.Id; body.CarriedBy = weak.Id; S.Emit(GameEventType.Carry, weak.Id, body.Id, value: 1);
                    var dest = room != null ? Ses.Sim.RandomPointIn(room, S.R(BL23.Sim.Stream.Presentation)) : weak.Pos;
                    Ses.Sim.Assign(weak, new Activity { Id = "probe:drag", Priority = 100, Interruptible = false, Steps = { new ActionStep { Kind = "GoTo", Target = dest, HasTarget = true } } });
                    yield return Wait(2.2f); ViewPair(weak, body, 1.5f, 2.6f); yield return Wait(0.3f); yield return ShotCo("viol_drag");
                    Log($"drag: {weak.Id} ({Violence.LiftCapacity(weak):0}kg) with {body.Id} ({Violence.BodyMass(body):0}kg) dragging={Violence.Dragging(S, weak)} anim={weak.Anim} traces={S.Traces.Count(tr => tr.Note == "drag")}");
                    yield return Wait(1.5f);
                    Ses.Sim.Interrupt(weak); weak.Carrying = null; body.CarriedBy = null; body.Pos = weak.Pos; body.Room = weak.Room; S.Emit(GameEventType.Carry, weak.Id, body.Id, value: 0);
                }
                ViolUnfreeze();
            }
            VCamOff();
            // 7. the first-person hands
            yield return ViolFirstPerson();
            Log("violence probe (daily) done");
        }

        IEnumerator ViolFirstPerson()
        {
            var me = S.Player; if (me == null || Ses.Player == null) yield break;
            var room = ViolRoom(RoomType.Library, RoomType.Lounge, RoomType.Study);
            var p = ViolCell(room, 2); Ses.Player.Teleport(p, 0f, 0f); Ses.Player.FirstPerson = true; yield return Wait(0.6f);
            FirstPersonHands.Ensure(Ses.Player);
            var gun = ViolGive(me, "Revolver"); ViolGive(me, "Cartridges"); yield return Wait(0.4f);
            FirstPersonHands.ProbeAim = true;
            var msg = Ses.Sim.PlayerLoad(); FirstPersonHands.PlayReload("Revolver", 5.5f); Log("player load: " + msg);
            yield return Wait(1.6f); yield return ShotCo("fp_revolver_reload");
            yield return Until(() => S.Tick >= Violence.Gun(S, gun.Id).ReadyAt, 10, "reload done"); yield return Wait(0.6f);
            yield return ShotCo("fp_revolver_aim");
            var cam = Ses.Player.Cam.transform; var f = cam.forward;
            var shot = Ses.Sim.PlayerFire(cam.position.y - Ses.World.ToWorld(me.Pos).y, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg);
            yield return null; yield return ShotCo("fp_revolver_recoil");
            Log("player shot: " + string.Join(" ; ", shot?.Impacts.Select(i => i.Kind + $" @{i.Dist:0.0}m") ?? new string[0]));
            yield return Wait(0.8f);
            // the crossbow: cranked slowly
            Ses.Sim.PlayerDrop(gun, me.Pos);
            var bow = ViolGive(me, "Crossbow"); ViolGive(me, "BoltQuiver"); yield return Wait(0.4f);
            Ses.Sim.PlayerLoad(); FirstPersonHands.PlayReload("Crossbow", 11f);
            yield return Wait(2.2f); yield return ShotCo("fp_crossbow_crank");
            FirstPersonHands.ProbeAim = false; yield return Wait(0.5f);
            Ses.Sim.PlayerDrop(bow, me.Pos);
            // a knife: wind-up, then the strike
            var knife = ViolGive(me, "KitchenKnife"); yield return Wait(0.5f);
            FirstPersonHands.PlaySwing(ActionAnim.Stab); yield return Wait(0.12f); yield return ShotCo("fp_knife_windup");
            yield return Wait(0.16f); yield return ShotCo("fp_knife_strike");
            yield return Wait(0.6f);
            Ses.Sim.PlayerDrop(knife, me.Pos);
        }

        // ================================================================== after the first chapter: the lethal ones
        IEnumerator ViolenceAfter()
        {
            var npcs = S.LivingNpcs.Where(a => a.Status == ActorStatus.Active && !a.IsButler).OrderBy(a => a.Id).ToList();
            if (npcs.Count < 5 || S.Phase != Phase.Daily) { Log($"violence probe (after): skipped (people {npcs.Count}, phase {S.Phase})"); yield break; }
            Log("violence probe (after): a drowning, a garrote and a shot to the end");
            var strong = npcs.OrderByDescending(Violence.Strength).ToList();
            var pool = S.Layout.Rooms.FirstOrDefault(r => r.Type == RoomType.Pool);
            var water = pool?.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(f => f.Type == "PoolWater");
            if (water != null)
            {
                var at = Ses.Sim.SnapPublic(new P3(water.Pos.f, water.Pos.x + water.W / 2 + 1.3f, water.Pos.z));
                yield return ViolHold(strong[0], strong[strong.Count - 1], pool, "drown", "viol_after_drown", true, at, -90f);
                ViolUnfreeze();
            }
            if (strong.Count >= 4 && strong[1].Alive && strong[strong.Count - 2].Alive)
            {
                yield return ViolHold(strong[1], strong[strong.Count - 2], ViolRoom(RoomType.Library, RoomType.Lounge), "strangle", "viol_after_garrote", true);
                ViolUnfreeze();
            }
            // a revolver shot to the chest at three metres, and the body card
            var a = npcs.FirstOrDefault(x => x.Alive && x.Status == ActorStatus.Active); var t = npcs.LastOrDefault(x => x.Alive && x.Status == ActorStatus.Active && x != a);
            if (a != null && t != null)
            {
                var room = ViolRoom(RoomType.Study, RoomType.GameRoom, RoomType.Lounge); ViolStage(room, a, t);
                var p = ViolCell(room, 0); var q = ViolCell(room, 12); ViolPut(a, p, MathX.AngleDeg(q.x - p.x, q.z - p.z)); ViolPut(t, q, MathX.AngleDeg(p.x - q.x, p.z - q.z));
                var gun = ViolGive(a, "Revolver"); ViolGive(a, "Cartridges"); Violence.Load(Ses.Sim, a, gun);
                ViewPair(a, t, 1.4f, 3f); yield return Wait(0.8f);
                float dist = a.Pos.DistXZ(t.Pos); var shot = Violence.Fire(Ses.Sim, a, gun, Violence.MuzzleY(a), MathX.AngleDeg(t.Pos.x - a.Pos.x, t.Pos.z - a.Pos.z), (float)(Math.Atan2(1.25f - Violence.MuzzleY(a), dist) * 180 / Math.PI), t.Id);
                yield return null; yield return ShotCo("viol_after_shot_flash");
                yield return Wait(1.2f); ViewPair(t, null, 1.2f, 2.2f); yield return ShotCo("viol_after_shot_fall");
                Log("killing shot: " + string.Join(" ; ", shot?.Impacts.Select(i => i.Kind + (i.Actor != null ? ":" + i.Actor + ":" + i.Region + ":sev" + i.Sev : "")) ?? new string[0]) + $" | {t.Status}");
                Ses.Speed = 3f; yield return Until(() => !t.Alive, 30, "shot death"); Ses.Speed = 1f; yield return Wait(1.5f);
                ViewPair(t, null, 1.0f, 1.8f); yield return ShotCo("viol_after_shot_dead");
                if (!t.Alive)
                {
                    VCamOff(); Ses.Player.Teleport(NearIn(t.Pos, t.Room, 1.2f), MathX.AngleDeg(t.Pos.x - Ses.Player.FeetPosition.x, t.Pos.z - Ses.Player.FeetPosition.z), 40f);
                    var ev = Ses.Sim.PlayerExamine(t); yield return Wait(0.3f);
                    if (ev != null) { Ses.Note.ShowBody(t, ev); yield return Wait(1f); yield return ShotCo("viol_after_body_card"); Ses.Note.Close(); Log("body card: " + (ev.Desc ?? "").Replace("\n", " / ")); }
                }
                ViolUnfreeze();
            }
            Log("violence probe (after) done");
        }
    }
}
