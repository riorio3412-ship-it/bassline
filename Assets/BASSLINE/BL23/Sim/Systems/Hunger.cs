using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// The mansion's hunger. When a chapter drags on without a death, the dream house starts to close in: each quiet
    /// morning one non-essential room is swallowed (its doors grown shut), portions at meals shrink, and everyone wakes
    /// more strained. Nobody is made to do anything — pressure only rises through what people feel. A death (or the
    /// next chapter) lets the house exhale: the swallowed rooms open again.
    /// </summary>
    public static class Hunger
    {
        const double Grace = 36 * 60;   // quiet time before the house gets hungry
        static readonly HashSet<RoomType> Essential = new HashSet<RoomType>
        {
            RoomType.Dining, RoomType.Kitchen, RoomType.Infirmary, RoomType.Bedroom, RoomType.GrandHall, RoomType.Corridor, RoomType.Landing, RoomType.Stairwell,
            RoomType.Elevator, RoomType.Courtroom, RoomType.ButlerRoom, RoomType.PowerRoom, RoomType.MachineRoom, RoomType.Laundry
        };

        public static int Level(GameState S) => S.Flags.TryGetValue($"hunger:{S.Loop}:{S.Chapter}", out var v) ? (int)v : 0;

        /// <summary>Morning check (called once at the 7 o'clock bell).</summary>
        public static void Morning(Simulation sim)
        {
            var S = sim.S;
            if (S.Phase != Phase.Daily) return;
            bool death = S.Incidents.Values.Any(i => i.Loop == S.Loop && i.Chapter == S.Chapter);
            if (death || S.Clock - S.Ch.ChapterStartClock < Grace || S.Survivors <= S.FloorLocked) return;
            Tighten(sim, "y_hunger", null);
        }

        /// <summary>One more level of hunger: a room swallowed, everyone more strained, the wall a little thinner. The morning
        /// check calls it; so does the house when its patience runs out (HousePush).</summary>
        public static bool Tighten(Simulation sim, string announceKey, string rule)
        {
            var S = sim.S;
            int lv = Level(S); if (lv >= 4) return false;
            lv++; S.Flags[$"hunger:{S.Loop}:{S.Chapter}"] = lv;
            var rng = S.R(Stream.ChapterRule);
            // a room is swallowed: somewhere people liked to be, never a room life depends on
            var cands = S.Layout.Rooms.Where(r => !Essential.Contains(r.Type) && !RoomInfo.IsPassage(r.Type) && !r.Void && !S.Flags.ContainsKey("swallowed:" + r.Id)).ToList();
            Room eaten = null;
            if (cands.Count > 0)
            {
                eaten = rng.Weighted(cands, r => 1 + S.Living.Count(a => a.Room == r.Id) * 0.5 + (r.Type == RoomType.Pool || r.Type == RoomType.Library || r.Type == RoomType.Lounge ? 0.5 : 0));
                S.Flags["swallowed:" + eaten.Id] = S.Clock;
                // people inside are gently put out first (they wake up in the corridor), then the doors grow shut
                foreach (var a in S.Actors.Values.Where(a => a.Alive && a.Room == eaten.Id))
                {
                    var door = eaten.Doors.Select(id => S.Layout.Doors[id]).FirstOrDefault(); if (door == null) continue;
                    var outside = S.Layout.Room(door.RoomA == eaten.Id ? door.RoomB : door.RoomA);
                    a.Pos = sim.SnapPublic(sim.RandomPointIn(outside, rng)); a.Room = outside.Id; a.Act = null;
                }
                foreach (var did in eaten.Doors) { var d = S.Layout.Doors[did]; d.Open = false; d.Locked = true; d.Sealed = true; S.Emit(GameEventType.Door, null, id: d.Id, text: "sealed", value: 1); }
                // what was left inside stays inside (a sealed room can hide things)
                S.Log("RoomSwallowed", Cast.Butler, room: eaten.Id, data: "hunger" + lv);
            }
            foreach (var a in S.LivingNpcs)
            {
                a.Needs.Stress = MathX.Clamp01(a.Needs.Stress + 0.07f * lv + a.Def.P.Fearfulness * 0.05f);
                a.Needs.Fear = MathX.Clamp01(a.Needs.Fear + 0.05f * lv);
                a.Needs.Anger = MathX.Clamp01(a.Needs.Anger + (a.Def.P.Aggression > 0.5f ? 0.05f * lv : 0));
            }
            Conscience.Erode(S, null, 0.03f, "hunger" + lv);
            sim.Announce(announceKey, new Dictionary<string, string> { { "place", eaten != null ? eaten.Name : "어딘가" }, { "n", lv.ToString() } }, rule);
            S.Dev($"HUNGER level {lv} swallowed {eaten?.Name}");
            return true;
        }

        /// <summary>Meals leave people hungrier while the house is hungry.</summary>
        public static float MealFactor(GameState S) { int lv = Level(S); return 1f - 0.15f * lv; }

        /// <summary>New chapter: the house exhales and every swallowed room opens again.</summary>
        public static void Release(Simulation sim)
        {
            var S = sim.S;
            foreach (var key in S.Flags.Keys.Where(k => k.StartsWith("swallowed:")).ToList())
            {
                S.Flags.Remove(key);
                if (!int.TryParse(key.Substring(10), out var rid)) continue;
                var r = S.Layout.Room(rid); if (r == null) continue;
                foreach (var did in r.Doors) { var d = S.Layout.Doors[did]; if (!d.Sealed) continue; d.Sealed = false; d.Locked = false; S.Emit(GameEventType.Door, null, id: d.Id, text: "unsealed", value: 0); }
                S.Log("RoomReleased", Cast.Butler, room: rid);
            }
        }
    }
}
