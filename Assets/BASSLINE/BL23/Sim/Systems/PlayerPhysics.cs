using System;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// The presentation layer runs real rigid-body physics for loose objects and light furniture; this is where the
    /// results come back into the world state: things that broke, things that moved, people hit by thrown objects.
    /// Every consequence is ordinary: noise others can hear, fragments that become evidence, a bruise, a grudge.
    /// </summary>
    public sealed partial class Simulation
    {
        /// <summary>A loose item was damaged by an impact (by = who set it in motion, if anyone).</summary>
        public void PhysicsItemDamaged(Item it, int damage, P3 at, string by)
        {
            if (it == null || damage <= it.Damage) return;
            var def = it.Def; it.Damage = Math.Min(3, damage); it.Pos = at; it.Room = S.Layout.RoomAt(at);
            bool shatters = def != null && (def.Mat == Mat.Glass || def.Mat == Mat.Ceramic) && it.Damage >= 2;
            if (shatters && !S.Items.Values.Any(i => i.ParentItem == it.Id))
            {
                it.Damage = 3;
                var rng = S.R(Stream.Combat);
                for (int i = 0; i < 3; i++)
                {
                    var fr = new Item { Id = S.NewId("it"), Type = "Fragment", Name = (def?.Kor ?? "물건") + " 파편", ParentItem = it.Id, Pos = new P3(at.f, at.x + rng.Range(-0.5f, 0.5f), at.z + rng.Range(-0.5f, 0.5f)), Room = it.Room, HomeRoom = it.Room };
                    fr.HomePos = fr.Pos; S.Items[fr.Id] = fr;
                    S.Emit(GameEventType.ItemMoved, by, data: fr.Id, text: "new", pos: fr.Pos);
                }
                Sound(SoundKind.GlassBreak, at, 0.6f, by);
                AddTrace("Fragment", at, it.Room, by, null, 0.4f, 0, "흩어진 " + (def?.Kor ?? "물건") + " 파편", "무언가 떨어지거나 부딪혀 깨졌다", "누가 그랬는지, 일부러였는지");
                S.Log("Break", by, item: it.Id, room: it.Room, pos: at, data: "physics", secret: by == null);
            }
            else
            {
                if (!it.Surface.Contains(def?.Mat == Mat.Metal ? "dent" : "scratch")) it.Surface.Add(def?.Mat == Mat.Metal ? "dent" : "scratch");
                Sound(SoundKind.Crash, at, 0.3f, by);
                S.Log("ItemHit", by, item: it.Id, room: it.Room, pos: at, data: "d" + it.Damage, secret: true);
            }
            S.Emit(GameEventType.ItemState, by, data: it.Id, text: shatters ? "broken" : "damaged");
        }

        /// <summary>A piece of furniture was knocked or dragged somewhere new by physics (pushing, grabbing, a fall).</summary>
        public void PhysicsFurnitureMoved(Furniture f, P3 pos, float yaw, string by)
        {
            if (f == null || S?.Layout == null || S.Layout.Floor(pos.f) == null || float.IsNaN(pos.x) || float.IsInfinity(pos.x)
                || float.IsNaN(pos.z) || float.IsInfinity(pos.z) || float.IsNaN(yaw) || float.IsInfinity(yaw)) return;
            var oldPos = f.Pos; float oldYaw = f.Yaw; int oldRoom = f.Room;
            int newRoom = S.Layout.RoomAt(pos);
            if (!f.Moved) { f.Moved = true; if (f.Origin.Equals(default(P3))) f.Origin = f.Pos; }
            f.Pos = pos; f.Yaw = yaw; f.Room = newRoom;
            if (oldRoom != newRoom)
            {
                S.Layout.Room(oldRoom)?.Furniture.Remove(f.Id);
                var room = S.Layout.Room(newRoom);
                if (room != null && !room.Furniture.Contains(f.Id)) room.Furniture.Add(f.Id);
            }
            // Seating/work approach points belong to the piece of furniture, including after a rotation or floor change.
            double angle = (yaw - oldYaw) * Math.PI / 180.0;
            float cosine = (float)Math.Cos(angle), sine = (float)Math.Sin(angle);
            foreach (var spot in S.Layout.Spots)
            {
                if (f.Id < 0 || spot.Furniture != f.Id) continue;
                var oldSpotRoom = S.Layout.Room(spot.Room);
                float x = spot.Pos.x - oldPos.x, z = spot.Pos.z - oldPos.z;
                spot.Pos = new P3(pos.f, pos.x + cosine * x + sine * z, pos.z - sine * x + cosine * z);
                x = spot.Approach.x - oldPos.x; z = spot.Approach.z - oldPos.z;
                spot.Approach = new P3(pos.f, pos.x + cosine * x + sine * z, pos.z - sine * x + cosine * z);
                spot.Yaw += yaw - oldYaw; spot.Room = newRoom;
                if (oldRoom != newRoom)
                {
                    oldSpotRoom?.Spots.Remove(spot.Id);
                    var room = S.Layout.Room(newRoom);
                    if (room != null && !room.Spots.Contains(spot.Id)) room.Spots.Add(spot.Id);
                }
            }
            // Clear both caches: removing a blocker from upstairs must also make the old floor walkable.
            S.Layout.InvalidateNav(oldPos.f);
            if (pos.f != oldPos.f) S.Layout.InvalidateNav(pos.f);
            S.Log("FurnitureMoved", by, room: f.Room, pos: pos, data: f.Type, secret: by == null);
            if (by != null) Sound(SoundKind.Crash, pos, 0.15f, by);
        }

        /// <summary>Furniture damaged by an impact.</summary>
        public void PhysicsFurnitureDamaged(Furniture f, int damage, string by)
        {
            if (f == null || damage <= f.Damage) return;
            f.Damage = Math.Min(3, damage);
            f.Marks.Add(f.Damage >= 2 ? "크게 부딪혀 금이 갔다" : "부딪힌 자국");
            Sound(f.Damage >= 2 ? SoundKind.Crash : SoundKind.Fall, f.Pos, 0.35f, by);
            S.Log("FurnitureHit", by, room: f.Room, pos: f.Pos, data: f.Type + ":" + f.Damage, secret: by == null);
        }

        /// <summary>Someone was hit by an object the player threw or shoved. A light knock stings; a heavy one hurts.</summary>
        public void PlayerThrownHit(Actor npc, Item it, float impulse, BodyRegion region)
        {
            if (npc == null || !npc.Alive || npc.IsPlayer) return;
            var def = it?.Def; float mass = def?.Mass ?? 0.5f;
            int sev = impulse > 25 && mass > 1.0f ? 2 : impulse > 8 ? 1 : 0;
            if (sev > 0) Strike(Cast.Player, npc, region, DamageType.Blunt, sev, it?.Id, "thrown");
            else Sound(SoundKind.Fall, npc.Pos, 0.2f, Cast.Player);
            Relations.Change(S, npc.Id, Cast.Player, like: -0.04f * (sev + 1), grudge: 0.05f * (sev + 1), fear: sev > 1 ? 0.08f : 0.01f, memory: sev > 0 ? "민혁이 던진 " + (def?.Kor ?? "물건") + "에 맞았다" : "민혁이 던진 물건이 몸에 부딪혔다");
            if (npc.Status == ActorStatus.Active) Speak(npc, sev > 0 ? "hurt_react" : "startle", Cast.Player, loud: sev > 1);
            S.Log("ThrownHit", Cast.Player, npc.Id, it?.Id, npc.Room, npc.Pos, sev.ToString());
        }
    }
}
