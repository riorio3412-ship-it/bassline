using System;
using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;
using Pose = BL23.Sim.Pose;

namespace BL23.Game
{
    /// <summary>
    /// Time-on-demand, interaction side: the one door from the player's hands and the still-world life to the kernel's
    /// container/seat/time API (Sim/Systems/Containers.cs, TimeFlow.cs, Together.cs) and to the TimeDirector
    /// (Game/Core/TimeDirector.cs). Everything the interaction code needs from those two goes through here. Presentation
    /// only: the kernel calls are the validated player commands; nothing here draws kernel RNG.
    /// </summary>
    public static class TimeLink
    {
        static Session Ses => Session.I;
        static Simulation Sim => Ses != null ? Ses.Sim : null;
        static GameState S => Sim != null ? Sim.S : null;

        public static bool IsContainer(Furniture f) => Simulation.IsContainer(f);

        // ------------------------------------------------------------------ kernel: containers and seats
        /// <summary>Hidden items kept in this piece of furniture (drawers, shelves behind doors, under a lid), by id.</summary>
        public static List<Item> ContainedItems(Furniture f) => f == null || Sim == null ? new List<Item>() : Sim.ContainedItems(f);

        /// <summary>The player opened (part of) a container: the ids shown in the opened part are revealed (the kernel only
        /// reveals those it really holds), prying in someone else's room is noticed. Returns the revealed items, the kernel's
        /// text and who saw it.</summary>
        public static (List<Item> revealed, string text, string seenBy) Open(Furniture f, IList<string> itemIds, bool firstOpen)
        {
            if (f == null || Sim == null) return (new List<Item>(), null, null);
            var r = Sim.PlayerOpen(f, itemIds ?? new List<string>(), firstOpen);
            return (r?.Revealed ?? new List<Item>(), r?.Text, r?.SeenBy);
        }

        /// <summary>Sit on (or lie on) a piece of furniture: the nearest free seat spot to 'aim'. Null when there is none.</summary>
        public static Spot Sit(Furniture f, P3 aim, bool lie = false) => f == null || Sim == null ? null : Sim.PlayerSit(f, aim, lie);

        /// <summary>Get up from the seat (the spot is freed, the kernel pose stands at the seat's approach point).</summary>
        public static void Stand() { if (Sim != null) Sim.PlayerStand(); }

        public static bool KernelSeated => Sim != null && Sim.PlayerSeated;

        /// <summary>Probe/test: put an item into a container (hidden, within reach).</summary>
        public static bool ProbeHide(Item it, Furniture f) => Sim != null && it != null && f != null && Sim.ProbeHide(it, f);

        /// <summary>The nearest container the kernel would snap evidence into (null when none within reach).</summary>
        public static Furniture NearestContainer(P3 pos, float maxDist = 6f) => Sim?.NearestContainer(pos, maxDist);

        // ------------------------------------------------------------------ kernel: time
        /// <summary>The clock only moves when time is spent (OnDemand) — false for probes and the continuous flow.</summary>
        public static bool OnDemand => Sim != null && Sim.OnDemand;

        /// <summary>An action that spends clock time (reading for half an hour); simple ones take none.</summary>
        public static bool IsPastime(PlayerAction a) => a != null && a.Minutes > 0;

        /// <summary>A still NPC in the way steps aside (zero time, no RNG). False when there is nowhere to go.</summary>
        public static bool YieldTo(Actor npc, P3 playerPos) => Sim != null && npc != null && Sim.YieldTo(npc, playerPos);

        /// <summary>The agreed "spend time with" plan waiting for the dialogue to close (null when none).</summary>
        public static TogetherPlan PendingTogether { get => Sim?.PendingTogether; set { if (Sim != null) Sim.PendingTogether = value; } }

        // ------------------------------------------------------------------ the TimeDirector
        static TimeDirector _hooked;
        /// <summary>The session's director (a new one after every load; its Ended is relayed as SkipEnded).</summary>
        public static TimeDirector Dir
        {
            get
            {
                var d = Ses != null ? Ses.TimeDir : null;
                if (d != null && !ReferenceEquals(d, _hooked)) { _hooked = d; d.Ended += (p, r) => RaiseSkipEnded(); }
                return d;
            }
        }

        /// <summary>No kernel tick for 0.4 real seconds (a stopped clock, a conversation, a pause): people show life in place.
        /// The session's own measure (any tick, whoever ran it).</summary>
        public static bool WorldStill => Ses != null && Ses.Sim != null && Ses.WorldStill;
        /// <summary>Time is being passed (the world fast-forwards): no barks, no idle gestures.</summary>
        public static bool Lapsing { get { var d = Dir; return d != null && d.Lapsing; } }
        /// <summary>A skip holds the player in place: only the mouse looks around.</summary>
        public static bool LocksMovement { get { var d = Dir; return d != null && d.LocksMovement; } }
        /// <summary>A skip (wait, sleep, pastime, time together) is running.</summary>
        public static bool TimeActive { get { var d = Dir; return d != null && d.Active; } }

        /// <summary>Raised once when a skip ends (views forget the activity changes that happened meanwhile).</summary>
        public static event Action SkipEnded;
        /// <summary>Called every frame by the still-world life (hooks a new session's director).</summary>
        public static void Poll() { var _ = Dir; }
        static void RaiseSkipEnded() { try { SkipEnded?.Invoke(); } catch (Exception e) { Debug.LogException(e); } }

        // ------------------------------------------------------------------ helpers
        /// <summary>Does the kernel have the player seated or lying on a spot?</summary>
        public static bool PlayerOnSpot(out Spot sp)
        {
            sp = null; var me = S?.Player; if (me == null || me.Spot < 0 || me.Spot >= S.Layout.Spots.Count) return false;
            sp = S.Layout.Spots[me.Spot]; return me.Pose == Pose.Sit || me.Pose == Pose.Sleep;
        }
    }
}
