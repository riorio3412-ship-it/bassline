using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Yusti, the butler. He takes no part in the case procedure: the house itself rings its bells, locks its night rooms
    /// and tolls for the dead (<see cref="House"/>). He keeps to his own room — tea, ledgers, the fishbowl — and can be
    /// visited and talked to there. His one public act is the verdict line in the court.
    /// </summary>
    public static class Butler
    {
        public static void Think(Simulation sim, Actor b)
        {
            if (b.Act != null) return;
            var S = sim.S; var L = S.Layout; var rng = S.R(Stream.Life);
            var home = L.First(RoomType.ButlerRoom) ?? L.Rooms.First(r => r.Type == RoomType.GrandHall && r.Floor == 0);
            var idle = new Activity { Id = "butler:idle", Label = "대기", Priority = 0 };
            idle.Steps.Add(Simulation.GoTo(sim.RandomPointIn(home, rng)));
            idle.Steps.Add(Simulation.WaitStep(rng.Range(25, 70)));
            sim.Assign(b, idle);
        }

        /// <summary>No butler-only steps remain (the house does the rounds).</summary>
        public static bool Exec(Simulation sim, Actor b, ActionStep st) => false;
    }

    /// <summary>
    /// The house as an actor without a body: morning and night bells, night rooms that lock themselves once empty,
    /// and the toll for a death once someone has found the body (or when nobody has for too long).
    /// </summary>
    public static class House
    {
        const string Why = "저택";

        /// <summary>Once per clock minute.</summary>
        public static void Minute(Simulation sim, int mod)
        {
            var S = sim.S; var L = S.Layout;
            if (S.Phase == Phase.Daily)
            {
                if (mod == 22 * 60)
                {
                    // the night bell: whoever lingers in a night room drifts out
                    foreach (var r in L.Rooms.Where(r => RoomInfo.NightLocked(r.Type)))
                        foreach (var x in S.LivingNpcs.Where(x => x.Room == r.Id && !x.IsButler)) { S.Flags["leave:" + x.Id] = r.Id; if (x.Act == null || x.Act.Interruptible) sim.Interrupt(x, 0.2); }
                }
                bool night = mod >= 22 * 60 || mod < 7 * 60;
                if (night && mod % 2 == 0)
                {
                    // a night room locks itself as soon as it is empty
                    foreach (var r in L.Rooms.Where(r => RoomInfo.NightLocked(r.Type)))
                    {
                        if (S.Actors.Values.Any(x => x.Alive && x.Room == r.Id)) continue;
                        foreach (var did in r.Doors) { var d = L.Doors[did]; if (d.NightPolicy && !d.Locked && !d.Sealed) sim.SetDoor(null, d, false, true, "야간 잠금"); }
                    }
                }
                if (mod == 7 * 60) OpenNightRooms(sim, "아침 개방");
            }
            // nobody found them: the house tolls on its own after half a day
            if ((S.Phase == Phase.Daily || S.Phase == Phase.Investigation) && mod % 30 == 0)
            {
                foreach (var inc in S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter && !i.Confirmed).ToList())
                {
                    var v = S.A(inc.Victim); if (v == null || v.Status != ActorStatus.Dead) continue;
                    if (S.Clock - v.Body.DeathClock < 14 * 60) continue;
                    // three-witness rule: first the house draws people to the body; only a body nobody can reach for a full day
                    // (hidden in a container, a sealed place) is announced without witnesses so the chapter cannot stall
                    if (S.Clock - v.Body.DeathClock < 24 * 60) { Cases.Gather(sim, inc, v.Room); continue; }
                    S.Log("HouseToll", null, v.Id, room: v.Room, data: "unfound");
                    Cases.HouseConfirm(sim, v.Room, force: true);
                }
            }
        }

        public static void OpenNightRooms(Simulation sim, string why)
        {
            var S = sim.S; var L = S.Layout;
            foreach (var r in L.Rooms.Where(r => RoomInfo.NightLocked(r.Type)))
                foreach (var did in r.Doors) { var d = L.Doors[did]; if (d.NightPolicy && d.Locked && !d.Sealed) sim.SetDoor(null, d, null, false, why); }
        }

        /// <summary>Someone found a death: the house tolls shortly after (a scream carries; the bell answers).</summary>
        public static void Call(Simulation sim, int room, double delayMin = 1.5)
        {
            var S = sim.S;
            if (S.Flags.TryGetValue("house:call", out var c) && c >= 0) return;
            S.Flags["house:call"] = room; S.Flags["house:callAt"] = S.Clock + delayMin;
        }

        /// <summary>Per tick (cheap): a pending toll comes due.</summary>
        public static void Tick(Simulation sim)
        {
            var S = sim.S;
            if (!S.Flags.TryGetValue("house:call", out var c) || c < 0) return;
            if (S.Flags.TryGetValue("house:callAt", out var at) && S.Clock < at) return;
            S.Flags["house:call"] = -1;
            Cases.HouseConfirm(sim, (int)c);
        }
    }
}
