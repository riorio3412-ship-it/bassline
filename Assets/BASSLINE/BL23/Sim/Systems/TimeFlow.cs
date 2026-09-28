using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    // ======================================================================================================================
    // Time on demand (kernel). In a player build the daily clock stands still; time passes only when the player passes it
    // (waits, "~까지" targets, sleep, pastimes), spends it with someone, or when something urgent is going on. The kernel
    // clock itself is untouched: every passage of time is real Step() ticks, so the per-minute schedule, the bells, the
    // murder planner and everything else run exactly as they always did. What is new is *when* the ticks run and *why they
    // stop*: StepSkip runs a plan's ticks and ends it on the stops below (the same rules headless and presented).
    // Nothing here draws RNG in a stop, a perception, a yield, or a "settled"/"hot" check.
    // ======================================================================================================================

    public enum StopClass { Ambient = 0, Social = 1, Critical = 2 }
    public enum StopKind { Target, Cancelled, Phase, Discovery, Attack, Wounded, Heard, Woken, Announcement, Meal, Approach, Knock, Appointment, Gathering, PartnerLeft, Moved, PlayerDead, Guard }

    /// <summary>Why time stopped (or would have). Title is the banner's line, Sub a dimmer second line.</summary>
    public sealed class TimeStop { public StopKind Kind; public StopClass Class; public string Title, Sub, Actor; public int Room = -1; public double Clock; }

    public enum SkipKind { Wait, Until, Sleep, Pastime, Together, Talk, Toll, Settle }

    /// <summary>One passage of time. Made by a Plan* call, run by BeginSkip / StepSkip / EndSkip (or RunSkip).</summary>
    public sealed class SkipPlan
    {
        public SkipKind Kind; public string Label; public double Start, Target;
        public bool Quiet;                                   // T-menu toggle: only Critical stops and appointments stop it
        public int Furniture = -1; public string Action;     // Pastime
        public string Partner, TogetherId, Activity, RequestId; public int Spot = -1, PartnerSpot = -1;   // Together
        public List<Utterance> Lines = new List<Utterance>();   // Together: shown by the presentation at 1/4, 1/2, 3/4
        public List<string> Passed = new List<string>();        // things that happened meanwhile (plain lines, no "그 사이:" prefix)
        public TimeStop Stop; public bool Done; public int Ticks;

        // ---- kernel-private run state (never saved; a plan does not survive a load)
        internal bool Begun, CriticalOnly, SeatedByKernel, StoodByKernel, Natural;
        internal P3 StartPos; internal int StartRoom = -1; internal Phase StartPhase; internal Simulation.UseCtx Use; internal TogetherPlan Together;
        internal int AnnSeen, StmtAtStart; internal double LastMinuteScan = -1;
        internal TimeStop Candidate; internal double FirstEndAt = -1;   // the stop (or natural end) waiting for deferral / settle
        internal double MealUntil = -1; internal TimeStop MealStop;    // a meal bell: wait for the diners to gather first
        internal double SettleUntil = -1, SettleStart = -1; internal int Deferred; internal double SettledMinutes, DeferredMinutes, MealWaitMinutes;
        internal HashSet<string> MetAtStart;
        internal double MinUntil = -1; internal bool DayStart;          // a day-start settle: at least this long, until people have left the hall
        internal double CancelAt = -1;                                  // the player stopped it while a murder was in a hot moment: runs on until it cools
        /// <summary>The player asked to stop, and the house runs on (critical stops only) until a murder's hot moment has passed.</summary>
        public bool CancelDeferred => CancelAt >= 0 && !Done;
    }

    public sealed class SkipResult
    {
        public double Minutes; public bool Interrupted; public TimeStop Stop; public string Title;
        public List<string> Lines = new List<string>(); public PlayerActivityResult Activity; public string Partner, BondReady; public int Overheard;
    }

    public sealed class UpcomingEvent { public double At; public string Text; public string Kind; public string Actor; public int Room = -1; }

    public sealed partial class Simulation
    {
        /// <summary>Set by the presentation (Session.TimeFlow == OnDemand) after every create/load, and by tests. Not saved.</summary>
        public bool OnDemand;
        /// <summary>The strongest thing that happened to the player since it was last taken. Higher class wins; equal class →
        /// the later one replaces it. Not saved.</summary>
        public TimeStop PendingStop;

        /// <summary>Legacy alarm (waits and the Speed>1.5 loop): true while a Critical stop or a wake-up is pending.</summary>
        public bool PlayerAlarm
        {
            get => PendingStop != null && (PendingStop.Class == StopClass.Critical || PendingStop.Kind == StopKind.Woken);
            set { if (!value) PendingStop = null; else RaiseStop(StopKind.Heard, StopClass.Critical, "무언가가 기다림을 깨웠다"); }
        }

        public void RaiseStop(StopKind k, StopClass c, string title, string sub = null, string actor = null, int room = -1)
        {
            if (PendingStop != null && PendingStop.Class > c) return;
            PendingStop = new TimeStop { Kind = k, Class = c, Title = title != null ? LineBank.FixParticles(title) : null, Sub = sub, Actor = actor, Room = room, Clock = S.Clock };
        }

        public TimeStop TakeStop() { var s = PendingStop; PendingStop = null; return s; }

        /// <summary>Kernel ticks per clock minute in the current phase (20 in daily life, 120 while investigating).</summary>
        public int TicksPerMinute => (int)Math.Round(1.0 / Math.Max(1e-6, S.ClockRate * SimTime.Dt));

        /// <summary>Clock minutes a daily conversation took (OnDemand), spent by the presentation with PlanTalk after it closes. Not saved.</summary>
        public double PendingTalk;
        /// <summary>A "함께 시간을 보낸다" the NPC agreed to, waiting for the dialogue to close. Not saved.</summary>
        public TogetherPlan PendingTogether;

        // non-serialized scan memory: appointment / gathering reminders already given (so a resumed wait does not re-stop at once)
        readonly HashSet<string> _reminded = new HashSet<string>();
        HeardSound _wakeSound; long _wakeSoundTick = -1;

        // ------------------------------------------------------------------ wording
        static string Hm12(double t)
        {
            int m = (int)Math.Floor(t % 1440); if (m < 0) m += 1440; int h = m / 60, mm = m % 60; int h12 = h % 12 == 0 ? 12 : h % 12;
            return $"{(h < 12 ? "오전" : h >= 21 ? "밤" : "오후")} {h12}:{mm:00}";
        }
        static string Span(double minutes)
        {
            int m = (int)Math.Round(Math.Max(0, minutes)); int h = m / 60, mm = m % 60;
            return h > 0 ? (mm > 0 ? $"{h}시간 {mm}분" : $"{h}시간") : $"{mm}분";
        }

        // ------------------------------------------------------------------ stop sources that live here (the rest raise at their site)
        /// <summary>Perception: the sound that is about to wake the sleeping player (for the wake-up's reason).</summary>
        internal void NoteWakeSound(HeardSound h) { _wakeSound = h; _wakeSoundTick = S.Tick; }

        /// <summary>Perception.Wake(player): the reason is inferred — a sound this tick, else the speakers.</summary>
        internal void OnPlayerWoken()
        {
            if (_wakeSound != null && _wakeSoundTick == S.Tick)
            {
                var h = _wakeSound; string where = h.GuessRoom == P?.Room ? "가까이" : S.RoomName(h.GuessRoom) + " 쪽";
                if (h.Kind == SoundKind.Knock) RaiseStop(StopKind.Woken, StopClass.Social, "노크 소리에 깼다", where, room: h.GuessRoom);
                else if (h.Kind == SoundKind.Scream) RaiseStop(StopKind.Woken, StopClass.Social, "비명에 깼다", where, room: h.GuessRoom);
                else RaiseStop(StopKind.Woken, StopClass.Social, Perception_SoundText(h.Kind) + " 소리에 깼다", where, room: h.GuessRoom);
            }
            else RaiseStop(StopKind.Woken, StopClass.Social, "방송에 깼다");
        }
        static string Perception_SoundText(SoundKind k) => SoundText(k);

        /// <summary>Is the source standing at a door of this room (a knock or a rattle there)?</summary>
        internal bool KnockAtRoom(int room, string source)
        {
            var r = S.Layout.Room(room); var src = S.A(source); if (r == null || src == null || src.IsPlayer) return false;
            foreach (var did in r.Doors) { var d = S.Layout.Doors[did]; if (d.Pos.f == src.Pos.f && d.Pos.DistXZ(src.Pos) < 1.8f) return true; }
            return false;
        }

        /// <summary>Something urgent the player knows about: the clock runs in real time for a while (OnDemand only; saved in flags).
        /// why: 1 heard, 2 attack seen, 3 wounded, 4 the player struck someone.</summary>
        internal void EmergencyTrigger(int why, string attacker = null, SoundKind snd = SoundKind.Scream)
        {
            if (!OnDemand) return;
            S.Flags["emerg:t"] = S.Clock; S.Flags["emerg:why"] = why; if (why == 1) S.Flags["emerg:snd"] = (int)snd;
            if (attacker != null && attacker != Cast.Player) S.Flags["emerg:atk:" + attacker] = S.Clock;
        }

        /// <summary>Non-null while the player knows of something urgent: a scream/attack/wound/fight in the last 20 clock minutes,
        /// someone they saw lying hurt still bleeding, or an attacker they saw still in sight (OnDemand only).</summary>
        public string EmergencyWhy()
        {
            if (!OnDemand || S.Player == null) return null;
            if (S.Flags.TryGetValue("emerg:t", out var t) && S.Clock - t < 20)
            {
                int why = S.Flags.TryGetValue("emerg:why", out var w) ? (int)w : 1;
                switch (why)
                {
                    case 2: return "누군가 공격당하고 있다";
                    case 3: return "다쳤다";
                    case 4: return "몸싸움이 벌어졌다";
                    default:
                        {
                            var kind = S.Flags.TryGetValue("emerg:snd", out var sk) ? (SoundKind)(int)sk : SoundKind.Scream;
                            return kind == SoundKind.Scream ? "비명이 들렸다" : SoundText(kind) + " 소리가 들렸다";
                        }
                }
            }
            foreach (var x in S.Actors.Values)
                if (x.Alive && !x.IsPlayer && x.Body.Critical && !x.Body.Stabilized && S.Flags.ContainsKey("seenbody:" + Cast.Player + ":" + x.Id))
                    return "다친 사람이 피를 흘리고 있다";
            // a body the player found that the house has not tolled for yet (three residents must see it): people are called to it and
            // come running — the clock runs while they do (20 clock minutes from the sighting or the latest call, e.g. the B bell)
            foreach (var inc in S.Incidents.Values)
            {
                if (inc.Loop != S.Loop || inc.Confirmed || inc.Discoverers == null || !inc.Discoverers.Contains(Cast.Player)) continue;
                double t0 = S.Flags.TryGetValue("seenbody:" + Cast.Player + ":" + inc.Victim, out var sb) ? sb : -1;
                if (S.Flags.TryGetValue("gather:" + inc.Id, out var g) && g > t0) t0 = g;
                if (t0 >= 0 && S.Clock - t0 < 20) return "사람들이 시신 쪽으로 모이고 있다";
            }
            var k = S.K(Cast.Player);
            foreach (var kv in S.Flags)
            {
                if (!kv.Key.StartsWith("emerg:atk:") || S.Clock - kv.Value > 30) continue;
                string id = kv.Key.Substring(10); var a = S.A(id); if (a == null || !a.Alive) continue;
                if (k.Open != null && k.Open.ContainsKey(id)) return "위험한 사람이 가까이 있다";
                if (k.LastSeen.TryGetValue(id, out var ls) && S.Clock - ls.t < 3) return "위험한 사람이 가까이 있다";
            }
            return null;
        }

        public bool CalmEnough() => Settled() && !AnyPlanHot();

        /// <summary>Nobody near the player is mid-way somewhere: no one on a stair touching the player's floor, no one within 30 m
        /// on that floor with more than 1.5 m of walk left, and no one on their way to talk to the player (within 25 m).</summary>
        public bool Settled()
        {
            var me = S.Player; if (me == null) return true; int pf = me.Pos.f;
            foreach (var a in S.Actors.Values)
            {
                if (a.IsPlayer || !a.Alive || a.Status != ActorStatus.Active || a.CarriedBy != null) continue;
                if (a.StairId >= 0 && a.StairId < S.Layout.Stairs.Count) { var st = S.Layout.Stairs[a.StairId]; if (st.A.f == pf || st.B.f == pf) return false; continue; }
                if (a.Pos.f != pf) continue;
                float d = a.Pos.DistXZ(me.Pos);
                var cur = a.Act?.Cur;
                if (cur != null && cur.Kind == "Talk" && cur.Actor == Cast.Player && d < 25f && a.TalkingTo == null) return false;
                if (d > 30f) continue;
                if (cur != null && cur.Kind == "GoTo" && a.TalkingTo == null)
                {
                    float left = PathLeft(a, cur);
                    if (left > 1.5f) return false;
                }
                else if (a.Speed > 0.05f && a.TalkingTo == null) return false;
            }
            return true;
        }

        float PathLeft(Actor a, ActionStep cur)
        {
            var act = a.Act;
            if (act.Path == null || act.PathIdx >= act.Path.Count) return cur.HasTarget && cur.Target.f == a.Pos.f ? a.Pos.DistXZ(cur.Target) : (cur.HasTarget ? 99f : 0f);
            float left = 0; P3 prev = a.Pos;
            for (int i = act.PathIdx; i < act.Path.Count && left < 50f; i++) { var q = act.Path[i]; left += q.f == prev.f ? prev.DistXZ(q) : 5f; prev = q; }
            return left;
        }

        static readonly HashSet<string> HotSteps = new HashSet<string> { "CarryTo", "MoveBody", "WashWeapon", "HideWeapon", "CleanUp", "TakeOff", "LockRoom" };
        static Func<Simulation, MurderPlan, bool> _crimeIsHot; static bool _crimeIsHotLooked;

        /// <summary>A murder plan is in a moment that must not be frozen and toured: its killer is at the attack in the target's room,
        /// or carrying/cleaning/hiding/locking after it. Read-only, no RNG.</summary>
        public bool AnyPlanHot()
        {
            if (!_crimeIsHotLooked)
            {
                _crimeIsHotLooked = true;
                try
                {
                    var mi = typeof(Crime).GetMethod("IsHot", new[] { typeof(Simulation), typeof(MurderPlan) });
                    if (mi != null && mi.ReturnType == typeof(bool)) _crimeIsHot = (sim, pl) => (bool)mi.Invoke(null, new object[] { sim, pl });
                }
                catch (Exception) { _crimeIsHot = null; }
            }
            foreach (var plan in S.Plans.Values)
            {
                if (plan.Stage == "Done" || plan.Stage == "Aborted") continue;
                if (_crimeIsHot != null) { try { if (_crimeIsHot(this, plan)) return true; continue; } catch (Exception) { _crimeIsHot = null; } }
                if (plan.Step < 0 || plan.Step >= plan.Steps.Count) continue;
                var killer = S.A(plan.Actor); if (killer == null || !killer.Alive || killer.Act == null) continue;
                if (killer.Act.Id != "murder:" + plan.Id + ":" + plan.Step) continue;
                var st = plan.Steps[plan.Step]; string k = st.Kind ?? "";
                if (k == "Attack" || k == "KnockOut" || k == "Drown")
                {
                    var t = S.A(plan.Target); if (t != null && t.Room == killer.Room) return true;
                    continue;
                }
                if (HotSteps.Contains(k)) return true;
                if (k.StartsWith("X_") && plan.Stage == "Concealing" && !(k.Contains("Wait") || k == "Alibi" || Methods.Waiting(k))) return true;
            }
            return false;
        }

        public bool TollPending => S.Flags.TryGetValue("house:call", out var c) && c >= 0;

        /// <summary>Someone is walking with the player (a living, active follower on the same floor within 25 m): the clock runs.</summary>
        public bool FollowerActive
        {
            get
            {
                var me = S.Player; if (me == null || !me.Alive) return false;
                foreach (var a in S.Actors.Values)
                    if (!a.IsPlayer && a.Alive && a.Status == ActorStatus.Active && a.Following == Cast.Player && a.Pos.f == me.Pos.f && a.Pos.DistXZ(me.Pos) < 25f) return true;
                return false;
            }
        }

        // ------------------------------------------------------------------ zero-time helpers for a still world
        /// <summary>The player looks around without time passing (knowledge only: sightings, bodies, attacks). No RNG, no clock.</summary>
        public void PlayerPerceive()
        {
            var me = S.Player;
            if (me == null || !me.Alive || me.Status != ActorStatus.Active || me.Pose == Pose.Sleep || me.StairId >= 0 || me.CarriedBy != null) return;
            if (S.Phase != Phase.Daily && S.Phase != Phase.Investigation && S.Phase != Phase.Assembly && S.Phase != Phase.Prologue) return;
            _stillLook = true;
            try { See(me); } finally { _stillLook = false; }
        }

        // a body the player finds in a still world: the call for people (three-witness rule) runs on the next tick — Gather picks
        // where each comes running by the dice, and a zero-time look never draws them (not saved: the per-tick recheck in Cases
        // calls people anyway if a save/load falls in between)
        bool _stillLook;
        readonly List<KeyValuePair<string, int>> _gatherSoon = new List<KeyValuePair<string, int>>();

        /// <summary>Cases (the player found a body that too few have seen): call people to it — now inside a tick, on the next tick
        /// from a zero-time look (PlayerPerceive).</summary>
        internal void GatherForPlayer(Incident inc, int room)
        {
            if (inc == null) return;
            if (!_stillLook) { Cases.Gather(this, inc, room); return; }
            foreach (var g in _gatherSoon) if (g.Key == inc.Id) return;
            _gatherSoon.Add(new KeyValuePair<string, int>(inc.Id, room));
        }

        /// <summary>Step (first thing after the clock moves): the calls a zero-time look queued.</summary>
        internal void FlushGatherSoon()
        {
            if (_gatherSoon.Count == 0) return;
            var list = _gatherSoon.ToList(); _gatherSoon.Clear();
            foreach (var g in list) if (S.Incidents.TryGetValue(g.Key, out var inc) && !inc.Confirmed) Cases.Gather(this, inc, g.Value);
        }

        /// <summary>Calls for people queued by a zero-time look, not yet run (tests).</summary>
        public int GatherSoonCount => _gatherSoon.Count;

        /// <summary>A still NPC steps aside for the player (0.8–1.2 m: left of the player's approach, then right, then back;
        /// never onto a door, never onto someone). Zero time, no RNG. Presentation only calls it while the world is still.</summary>
        public bool YieldTo(Actor npc, P3 playerPos)
        {
            if (npc == null || npc.IsPlayer || !npc.Alive || npc.Status != ActorStatus.Active || npc.CarriedBy != null || npc.Carrying != null) return false;
            if (npc.Spot >= 0 && npc.Pose != Pose.Stand && npc.Pose != Pose.Crouch) return false;   // seated people are not in the way
            if (npc.StairId >= 0 && npc.StairId < S.Layout.Stairs.Count)
            {
                // back to the end of the stair they stepped on from (their room does not change: no room hooks, no dice)
                var end = npc.StairFrom;
                if (npc.Pos.DistXZ(end) > 3f || S.Layout.RoomAt(end) != npc.Room) return false;
                npc.StairId = -1; npc.StairUntil = 0; npc.Pos = Snap(end); if (npc.Act != null) npc.Act.Path = null;
                S.Log("Yield", npc.Id, Cast.Player, room: npc.Room, pos: npc.Pos, data: "stair");
                return true;
            }
            float dx = npc.Pos.x - playerPos.x, dz = npc.Pos.z - playerPos.z; float dl = (float)Math.Sqrt(dx * dx + dz * dz);
            if (dl < 0.01f) { dx = (float)Math.Sin(npc.Yaw * Math.PI / 180.0); dz = (float)Math.Cos(npc.Yaw * Math.PI / 180.0); dl = 1f; }
            dx /= dl; dz /= dl;
            // left of the approach = rotate the approach direction by +90° (x,z) → (-z, x)
            var dirs = new[] { (-dz, dx), (dz, -dx), (dx, dz) };
            var g = S.Layout.Nav(npc.Pos.f);
            foreach (var (ux, uz) in dirs)
                foreach (var dist in new[] { 1.0f, 0.8f, 1.2f })
                {
                    var q = new P3(npc.Pos.f, npc.Pos.x + ux * dist, npc.Pos.z + uz * dist);
                    int k = g.CellOf(q.x, q.z); if (k < 0 || !g.Walkable(k)) continue;
                    if (S.Layout.RoomAt(q) != npc.Room) continue;   // a step aside, not into another room (no room hooks)
                    if (!g.Ray(npc.Pos.x, npc.Pos.z, q.x, q.z, door => S.Layout.Doors[door].Open, false, out _)) continue;
                    bool nearDoor = false; foreach (var d in S.Layout.Doors) if (d.Pos.f == q.f && d.Pos.DistXZ(q) < 0.9f) { nearDoor = true; break; }
                    if (nearDoor) continue;
                    bool crowded = false;
                    foreach (var o in S.Actors.Values) { if (o == npc || o.Pos.f != q.f || o.Status == ActorStatus.Executed || o.Status == ActorStatus.Escaped) continue; if (o.Pos.DistXZ(q) < 0.6f) { crowded = true; break; } }
                    if (crowded) continue;
                    npc.Pos = q; if (npc.Act != null) npc.Act.Path = null;
                    S.Log("Yield", npc.Id, Cast.Player, room: npc.Room, pos: q);
                    return true;
                }
            return false;
        }

        /// <summary>"T 계속 기다리기" on an approach: they go back to what they were doing (an open offer stays and expires as usual).</summary>
        public void DismissApproach(Actor npc)
        {
            if (npc == null) return;
            S.Flags.Remove("approach:" + npc.Id);
            if (npc.Act != null && npc.Act.Cur != null && npc.Act.Cur.Kind == "Talk" && npc.Act.Cur.Actor == Cast.Player) NextStep(npc);
            npc.NextThink = Math.Min(npc.NextThink, S.Clock);
        }

        /// <summary>Probe: this person comes to talk to the player now.</summary>
        public bool ProbeApproach(Actor npc)
        {
            if (npc == null || npc.IsPlayer || !npc.Alive || npc.Status != ActorStatus.Active || npc.Pose == Pose.Sleep) return false;
            var act = new Activity { Id = "social:" + Cast.Player, Label = "민혁하고 이야기", Priority = 1.2 };
            act.Steps.Add(new ActionStep { Kind = "Talk", Actor = Cast.Player, Duration = 0 });
            npc.TalkingTo = null; Assign(npc, act);
            return true;
        }

        /// <summary>Probe: someone in that room screams (a real scream: the house hears it).</summary>
        public bool ProbeScream(int room)
        {
            var o = S.LivingNpcs.Where(x => x.Room == room && x.Status == ActorStatus.Active).OrderBy(x => x.Id).FirstOrDefault();
            if (o == null) return false;
            Speak(o, "scream_discover", null, loud: true);
            Sound(SoundKind.Scream, o.Pos, 0.85f, o.Id, o.Id);
            return true;
        }

        // ------------------------------------------------------------------ what is coming (the T menu's rows)
        /// <summary>The next thing worth passing time to (the T menu's smart row).</summary>
        public UpcomingEvent NextEvent(double from)
        {
            // the meals, lunch, the night bell, appointments and gatherings (not "evening" or the morning bell: sleep covers that)
            var list = Upcoming(from, from + 26 * 60).Where(e => e.Kind != "evening" && e.Kind != "morning").ToList();
            return list.Count > 0 ? list[0] : null;
        }

        /// <summary>At most three relevant "~까지" targets, by time: appointments and gatherings first in weight, then lunch,
        /// evening, night.</summary>
        public List<UpcomingEvent> UntilTargets()
        {
            double now = S.Clock; var all = Upcoming(now, now + 14 * 60);
            var pick = new List<UpcomingEvent>();
            foreach (var e in all.Where(e => e.Kind == "appointment" || e.Kind == "gathering")) if (pick.Count < 3) pick.Add(e);
            foreach (var e in all.Where(e => e.Kind == "lunch" || e.Kind == "evening" || e.Kind == "night"))
            {
                if (pick.Count >= 3) break;
                if (pick.Any(p => p.Kind == e.Kind)) continue;
                pick.Add(e);
            }
            return pick.OrderBy(e => e.At).ToList();
        }

        /// <summary>An appointment that is due now (its window is open, At−5 … At+25) while the player is somewhere else: the T menu
        /// names it first ("약속 시간이다 — 진우가 라운지에서 기다린다") instead of skipping past it to the next one. Null when none.</summary>
        public UpcomingEvent DueAppointment()
        {
            if (S.Phase != Phase.Daily || P == null) return null;
            foreach (var r in (S.Requests ?? new List<Request>()).Where(r => r.Kind == "invite" && r.State == "accepted").OrderBy(r => r.At).ThenBy(r => r.Id, StringComparer.Ordinal))
            {
                if (S.Clock < r.At - 5 || S.Clock >= r.At + 25 || P.Room == r.Room) continue;
                return new UpcomingEvent { At = S.Clock, Kind = "appointment_due", Actor = r.From, Room = r.Room, Text = $"약속 — {Cast.GivenOf(r.From)} · {S.RoomName(r.Room)}" };
            }
            return null;
        }

        List<UpcomingEvent> Upcoming(double from, double to)
        {
            var list = new List<UpcomingEvent>();
            double day0 = Math.Floor(from / 1440) * 1440;
            void At(double m, string kind, string text) { for (int d = 0; d < 2; d++) { double t = day0 + d * 1440 + m; if (t > from + 0.5 && t <= to) { list.Add(new UpcomingEvent { At = t, Kind = kind, Text = text }); break; } } }
            if (S.Phase == Phase.Daily)
            {
                At(MealStart[0], "meal", "아침 식사 종");
                At(MealStart[1], "lunch", "점심 시간");
                At(MealStart[2], "meal", "저녁 식사 종");
                At(18 * 60, "evening", "저녁");
                At(22 * 60, "night", "밤 10시 종");
                At(7 * 60, "morning", "아침 종");
                foreach (var r in (S.Requests ?? new List<Request>()).Where(r => r.Kind == "invite" && r.State == "accepted"))
                {
                    // in the room already: the appointment itself (a wait there runs on until they come); elsewhere: 5 minutes early
                    bool here = P != null && P.Room == r.Room;
                    double t = here ? r.At : r.At - 5;
                    if (t <= from + 0.5 || t > to) continue;
                    list.Add(new UpcomingEvent { At = t, Kind = "appointment", Actor = r.From, Room = r.Room, Text = $"약속 — {Cast.GivenOf(r.From)} · {S.RoomName(r.Room)}" });
                }
                foreach (var g in S.Gatherings ?? new List<Gathering>())
                {
                    if (g.Cancelled || g.Done || g.Revs.Count == 0 || !KnowsGathering(g)) continue;
                    double t = g.Cur.Start; if (t <= from + 0.5 || t > to) continue;
                    list.Add(new UpcomingEvent { At = t, Kind = "gathering", Actor = g.Host, Room = g.Cur.Room, Text = $"모임 — {g.Label} · {S.RoomName(g.Cur.Room)}" });
                }
            }
            else if (S.Phase == Phase.Investigation && S.Ch.InvestigationEnd >= 0)
                list.Add(new UpcomingEvent { At = S.Ch.InvestigationEnd, Kind = "trial", Text = "심판 소집" });
            list.Sort((a, b) => a.At.CompareTo(b.At));
            return list;
        }

        bool KnowsGathering(Gathering g)
        {
            if (g.KnownRev != null && g.KnownRev.TryGetValue(Cast.Player, out var kr) && kr >= 0) return true;
            return g.Status != null && g.Status.TryGetValue(Cast.Player, out var st) && (st == "invited" || st == "accepted");
        }

        // ------------------------------------------------------------------ plans
        bool CanPassTime(out string why)
        {
            why = null;
            if (S.Player == null || !S.Player.Alive || S.Player.Status != ActorStatus.Active) { why = "지금은 시간을 보낼 수 없다"; return false; }
            if (S.Phase != Phase.Daily && S.Phase != Phase.Investigation) { why = "지금은 시간을 보낼 수 없다"; return false; }
            if (S.Player.CarriedBy != null || S.Player.Carrying != null) { why = "지금은 시간을 보낼 수 없다"; return false; }
            return true;
        }

        SkipPlan NewPlan(SkipKind kind, double target, string label)
            => new SkipPlan { Kind = kind, Start = S.Clock, Target = target, Label = label, CriticalOnly = S.Phase == Phase.Investigation };

        public SkipPlan PlanWait(double minutes, out string why)
        {
            if (!CanPassTime(out why)) return null;
            if (minutes <= 0) { why = "기다릴 시간이 없다"; return null; }
            return NewPlan(SkipKind.Wait, S.Clock + minutes, Span(minutes) + " 기다리기");
        }

        public SkipPlan PlanUntil(double clock, string label, out string why)
        {
            if (!CanPassTime(out why)) return null;
            if (clock <= S.Clock + 0.05) { why = "이미 지난 시각이다"; return null; }
            return NewPlan(SkipKind.Until, clock, label ?? (Hm12(clock) + "까지"));
        }

        /// <summary>Sleep in one's own bed, 20:00–06:30, until the morning bell (07:00, +1 minute).</summary>
        public SkipPlan PlanSleep(out string why)
        {
            if (!CanPassTime(out why)) return null;
            if (S.Phase != Phase.Daily) { why = "지금은 잘 수 없다"; return null; }
            int m = S.Minute;
            if (!(m >= 20 * 60 || m < 6 * 60 + 30)) { why = "밤에만 잘 수 있다"; return null; }
            var bed = S.Layout.BedroomOf(Cast.Player);
            if (bed == null || P.Room != bed.Id) { why = "자기 방 침대에서만 잘 수 있다"; return null; }
            double day0 = Math.Floor(S.Clock / 1440) * 1440;
            double target = (m >= 20 * 60 ? day0 + 1440 : day0) + 7 * 60 + 1;
            var p = NewPlan(SkipKind.Sleep, target, "잠자리에 든다");
            var f = S.Layout.Furniture.FirstOrDefault(x => x.Room == bed.Id && x.Type == "Bed"); if (f != null) p.Furniture = f.Id;
            return p;
        }

        /// <summary>A pastime at a piece of furniture (reading, playing, cooking, a nap...). Validated and prepared now (company may
        /// join); its outcome is applied at EndSkip, scaled by the time really spent.</summary>
        public SkipPlan PlanPastime(Furniture f, string actionId, out string why)
        {
            why = null;
            if (!CanPassTime(out why)) return null;
            var ctx = PrepareUse(f, actionId, true);
            if (ctx.Fail != null) { why = ctx.Fail; return null; }
            if (ctx.Act.Minutes <= 0) { why = "시간이 드는 일이 아니다"; return null; }
            var p = NewPlan(SkipKind.Pastime, S.Clock + ctx.Minutes, ctx.Act.Label);
            p.Furniture = f.Id; p.Action = actionId; p.Use = ctx;
            return p;
        }

        /// <summary>The minutes a daily conversation took (OnDemand): a tiny lapse right after the dialogue closes.</summary>
        public SkipPlan PlanTalk(double minutes)
        {
            var p = NewPlan(SkipKind.Talk, S.Clock + Math.Max(0.05, minutes), "대화");
            p.CriticalOnly = false; return p;
        }

        /// <summary>After a discovery: until the house tolls and the investigation opens (cap 6 clock minutes).</summary>
        public SkipPlan PlanToll() { var p = NewPlan(SkipKind.Toll, S.Clock + 6, "종이 울린다"); return p; }

        /// <summary>At the start of a day: until people have dispersed to what they do or the cap; never across a meal bell. At least
        /// 4 clock minutes, and not before everyone has had a first thought and set off (right after the prologue or a trial the
        /// house stands everyone in one room; "settled" there only means nobody has started walking yet), nor while two idle people
        /// still stand shoulder to shoulder in the room the day began in (the first 12 minutes).</summary>
        public SkipPlan PlanSettle(double capMinutes)
        {
            double target = S.Clock + Math.Max(0.1, capMinutes);
            double day0 = Math.Floor(S.Clock / 1440) * 1440;
            foreach (var mb in new[] { MealStart[0], MealStart[2] })
                for (int d = 0; d < 2; d++) { double t = day0 + d * 1440 + mb; if (t > S.Clock && t - 1 < target) target = Math.Max(S.Clock + 0.05, t - 1); }
            var p = NewPlan(SkipKind.Settle, target, "하루의 시작");
            p.DayStart = true; p.MinUntil = Math.Min(target, S.Clock + 4);
            return p;
        }

        /// <summary>Everyone active has had the first thought of the day (an activity, or their think time has come and gone).</summary>
        bool EveryoneSetOff()
        {
            foreach (var a in S.LivingNpcs)
            {
                if (a.IsButler || a.Status != ActorStatus.Active || a.CarriedBy != null || a.Pose == Pose.Sleep) continue;
                if (a.Act == null && S.Clock < a.NextThink) return false;
            }
            return true;
        }

        /// <summary>Two idle people shoulder to shoulder (within 1.2 m) in this room — a crowd the script left, not people living.</summary>
        bool Clumped(int room)
        {
            if (room < 0) return false;
            var idle = new List<Actor>();
            foreach (var a in S.LivingNpcs)
            {
                if (a.Room != room || a.IsButler || a.Status != ActorStatus.Active || a.Spot >= 0 || a.TalkingTo != null || a.StairId >= 0) continue;
                if (a.Speed > 0.05f) continue;
                if (a.Anim != Anim.Idle && a.Anim != Anim.None) continue;
                idle.Add(a);
            }
            for (int i = 0; i < idle.Count; i++)
                for (int j = i + 1; j < idle.Count; j++)
                    if (idle[i].Pos.f == idle[j].Pos.f && idle[i].Pos.DistXZ(idle[j].Pos) < 1.2f) return true;
            return false;
        }

        /// <summary>"계속 기다리기": the same absolute target, fresh stop state (a pastime or scene resumes as a plain wait).</summary>
        public SkipPlan ResumePlan(SkipPlan interrupted)
        {
            if (interrupted == null || interrupted.Target <= S.Clock + 0.05) return null;
            if (!CanPassTime(out _)) return null;
            var kind = interrupted.Kind == SkipKind.Sleep ? SkipKind.Sleep : interrupted.Kind == SkipKind.Wait ? SkipKind.Wait : SkipKind.Until;
            if (kind == SkipKind.Sleep) { var sp = PlanSleep(out _); if (sp == null) return null; sp.Target = interrupted.Target; sp.Quiet = interrupted.Quiet; return sp; }
            var p = NewPlan(kind, interrupted.Target, interrupted.Kind == SkipKind.Wait || interrupted.Kind == SkipKind.Until ? interrupted.Label : "계속 기다리기");
            p.Quiet = interrupted.Quiet; return p;
        }

        // ------------------------------------------------------------------ running a plan
        public void BeginSkip(SkipPlan p)
        {
            if (p == null || p.Begun) return;
            p.Begun = true; PendingStop = null; var me = S.Player;
            p.Start = S.Clock; p.StartPhase = S.Phase; p.AnnSeen = S.Announcements.Count;
            p.StmtAtStart = S.K(Cast.Player).Statements.Count;
            p.MetAtStart = new HashSet<string>((S.Requests ?? new List<Request>()).Where(r => r.State == "met").Select(r => r.Id));
            p.LastMinuteScan = Math.Floor(S.Clock);
            switch (p.Kind)
            {
                case SkipKind.Sleep:
                    {
                        var f = p.Furniture >= 0 ? S.Layout.Furniture[p.Furniture] : null;
                        if (f != null && !(me.Spot >= 0 && S.Layout.Spots[me.Spot].Furniture == f.Id && me.Pose == Pose.Sleep))
                        {
                            if (PlayerSit(f, me.Pos, true) != null) p.SeatedByKernel = true;
                        }
                        me.Pose = Pose.Sleep; me.Anim = Anim.Sleep;
                        break;
                    }
                case SkipKind.Pastime:
                    BeginUse(p.Use, p);
                    break;
                case SkipKind.Together:
                    BeginTogether(p);
                    break;
                default:
                    if (me.Pose != Pose.Sit && me.Pose != Pose.Sleep) me.Anim = Anim.Idle;
                    break;
            }
            p.StartPos = me.Pos; p.StartRoom = me.Room;
            S.Dev($"skip begin {p.Kind} '{p.Label}' {ClockFmt.HM(p.Start)}→{ClockFmt.HM(p.Target)} quiet={p.Quiet}");
        }

        /// <summary>Runs up to maxTicks kernel ticks of the plan. True when it is done (p.Stop says why).</summary>
        public bool StepSkip(SkipPlan p, int maxTicks)
        {
            if (p == null) return true;
            if (!p.Begun) BeginSkip(p);
            for (int i = 0; i < maxTicks && !p.Done; i++)
            {
                // only a move the kernel makes inside a tick counts as "moved" (the presentation may push the pose between calls)
                var me0 = S.Player; P3 before = me0 != null ? me0.Pos : default; int beforeRoom = me0 != null ? me0.Room : -1;
                Step(); p.Ticks++;
                SkipTick(p, before, beforeRoom);
            }
            return p.Done;
        }

        bool Deferrable(SkipPlan p) => OnDemand && S.Phase == Phase.Daily && (p.Kind == SkipKind.Wait || p.Kind == SkipKind.Until || p.Kind == SkipKind.Pastime || p.Kind == SkipKind.Together || p.Kind == SkipKind.Sleep || p.Kind == SkipKind.Settle);

        void Finish(SkipPlan p, TimeStop stop)
        {
            if (stop.Clock > 0 && S.Clock - stop.Clock >= 2 && stop.Kind != StopKind.Target) stop.Sub = (stop.Sub != null ? stop.Sub + " " : "") + "— " + Hm12(stop.Clock);
            p.Stop = stop; p.Done = true;
        }

        TimeStop Mk(StopKind k, StopClass c, string title, string sub = null, string actor = null, int room = -1)
            => new TimeStop { Kind = k, Class = c, Title = title != null ? LineBank.FixParticles(title) : null, Sub = sub, Actor = actor, Room = room, Clock = S.Clock };

        void SkipTick(SkipPlan p, P3 before, int beforeRoom)
        {
            var me = S.Player;
            // ---- 1. things that end any passage of time at once
            if (me == null || !me.Alive || S.Phase == Phase.PlayerDead) { Finish(p, Mk(StopKind.PlayerDead, StopClass.Critical, "쓰러졌다")); return; }
            if (me.Pos.f != before.f || me.Room != beforeRoom && beforeRoom >= 0 || me.Pos.DistXZ(before) > 0.5f)
            { Finish(p, Mk(StopKind.Moved, StopClass.Critical, "어딘가로 옮겨졌다", S.RoomName(me.Room), room: me.Room)); return; }
            if (S.Phase != p.StartPhase)
            {
                if (p.Kind == SkipKind.Toll && S.Phase == Phase.Investigation) { p.Natural = true; Finish(p, Mk(StopKind.Target, StopClass.Ambient, "종이 울렸다")); return; }
                Finish(p, Mk(StopKind.Phase, StopClass.Critical, PhaseTitle(S.Phase))); return;
            }
            // ---- 2. announcements made this tick (bells, rules, the toll)
            bool morningBell = false; bool ambientAnn = false, otherAnn = false; string ambientTitle = null;
            while (p.AnnSeen < S.Announcements.Count)
            {
                var an = S.Announcements[p.AnnSeen++];
                var cls = AnnouncementClass(an, out string title);
                if (cls == StopClass.Ambient && an.Key != "y_morning") { ambientAnn = true; ambientTitle = title; } else otherAnn = true;
                if (an.Key == "y_morning")
                {
                    morningBell = true;
                    if (PendingStop != null && PendingStop.Kind == StopKind.Woken) { PendingStop.Title = "아침 종이 울렸다"; PendingStop.Sub = null; }
                }
                if (cls == StopClass.Critical) { Finish(p, Mk(StopKind.Announcement, StopClass.Critical, title)); return; }
                if (an.Key == "y_meal_breakfast" || an.Key == "y_meal_dinner")
                {
                    if (MealStops(p) && p.MealUntil < 0 && p.Candidate == null)
                    { p.MealUntil = S.Clock + 10; p.MealStop = Mk(StopKind.Meal, StopClass.Social, an.Key == "y_meal_breakfast" ? "아침 식사 종이 울렸다" : "저녁 식사 종이 울렸다", "모두 식당에 모였다"); }
                    else p.Passed.Add(an.Key == "y_meal_breakfast" ? "아침 식사 종이 울렸다" : "저녁 식사 종이 울렸다");
                    continue;
                }
                if (cls == StopClass.Social) { Candidate(p, Mk(StopKind.Announcement, StopClass.Social, title)); continue; }
                if (!(p.Kind == SkipKind.Sleep && an.Key == "y_morning")) p.Passed.Add(title);
            }
            // ---- 3. what happened to the player (raised at its source)
            if (PendingStop != null)
            {
                var ps = PendingStop;
                if (ps.Class == StopClass.Critical) { PendingStop = null; Finish(p, ps); return; }
                PendingStop = null;
                bool asleepKind = p.Kind == SkipKind.Sleep || p.Kind == SkipKind.Pastime && p.Action == "nap";
                if (p.Kind == SkipKind.Sleep && ps.Kind == StopKind.Woken && morningBell) { p.Natural = true; }
                else if (asleepKind && ps.Kind == StopKind.Woken && ambientAnn && !otherAnn && !morningBell)
                {
                    // an everyday announcement in the night (a repair notice): half awake for a moment, then back to sleep
                    me.Pose = Pose.Sleep; me.Anim = Anim.Sleep;
                    p.Passed.Remove(ambientTitle); p.Passed.Add(ambientTitle + " — 잠깐 깼다가 다시 잠들었다");
                }
                else if (StopsFor(p, ps)) Candidate(p, ps);
                else p.Passed.Add(PassedLine(p, ps));
            }
            // ---- 3b. the player asked to stop while a murder was in a hot moment: on until it has passed (at most 30 clock minutes)
            if (p.CancelAt >= 0)
            {
                if (!AnyPlanHot() || S.Clock - p.CancelAt >= 30)
                {
                    p.Deferred++; p.DeferredMinutes = Math.Max(p.DeferredMinutes, S.Clock - p.CancelAt);
                    p.Stop = Mk(StopKind.Cancelled, StopClass.Ambient, "기다리기를 멈췄다"); p.Done = true;
                }
                else GuardCheck(p);
                return;
            }
            // ---- 4. together: the partner left
            if (p.Kind == SkipKind.Together && p.Candidate == null)
            {
                var partner = S.A(p.Partner);
                if (partner == null || !partner.Alive || partner.Status != ActorStatus.Active || partner.Act == null || partner.Act.Id != "social:together:" + Cast.Player)
                    Candidate(p, Mk(StopKind.PartnerLeft, StopClass.Social, $"{Cast.GivenOf(p.Partner)}이(가) 잠깐 자리를 떴다", actor: p.Partner));
            }
            // ---- 5. once a clock minute: appointments and gatherings the player knows about
            if (Math.Floor(S.Clock) > p.LastMinuteScan) { p.LastMinuteScan = Math.Floor(S.Clock); MinuteScan(p); }
            // ---- 6. a meal bell waits for the diners to sit down first (at most 10 clock minutes)
            if (p.MealUntil >= 0 && p.Candidate == null)
            {
                if (S.Clock >= p.MealUntil || DinersGathered()) { var ms = p.MealStop; p.MealWaitMinutes = S.Clock - ms.Clock; p.MealUntil = -1; p.MealStop = null; Candidate(p, ms); }
            }
            // ---- 7. the natural end
            if (p.Candidate == null && !p.Natural && NaturalEnd(p)) p.Natural = true;
            if (p.Candidate == null && !p.Natural) { GuardCheck(p); return; }
            // ---- 8. deferral (a murder in a hot moment is not frozen and toured) and settling (arrivals get to arrive)
            if (p.FirstEndAt < 0) p.FirstEndAt = S.Clock;
            if (Deferrable(p) && AnyPlanHot() && S.Clock - p.FirstEndAt < 30)
            {
                p.Deferred++; p.DeferredMinutes = S.Clock - p.FirstEndAt;
                p.SettleUntil = -1; p.SettleStart = -1;   // a settle that had begun starts over once the hot moment has passed
                GuardCheck(p); return;
            }
            bool settleKind = p.Candidate == null || p.Candidate.Kind == StopKind.Meal || p.Candidate.Kind == StopKind.Appointment;
            if (Deferrable(p) && settleKind && p.Kind != SkipKind.Settle)
            {
                if (p.SettleUntil < 0) { p.SettleStart = S.Clock; p.SettleUntil = S.Clock + 5; }
                if (!Settled() && S.Clock < p.SettleUntil) { GuardCheck(p); return; }
                p.SettledMinutes = Math.Max(0, S.Clock - p.SettleStart);
            }
            if (p.Candidate != null) Finish(p, p.Candidate);
            else Finish(p, Mk(StopKind.Target, StopClass.Ambient, p.Kind == SkipKind.Sleep ? "아침 종이 울렸다" : Span(S.Clock - p.Start) + "이 흘렀다"));
        }

        void GuardCheck(SkipPlan p)
        {
            if (S.Clock > p.Target + 60 || p.Ticks > (p.Target - p.Start + 60) * 120 + 2000)
            {
                S.Dev($"GUARD skip {p.Kind} '{p.Label}' ran past its target ({ClockFmt.HM(p.Target)}), ticks {p.Ticks}");
                Finish(p, Mk(StopKind.Guard, StopClass.Critical, "시간이 멈췄다"));
            }
        }

        void Candidate(SkipPlan p, TimeStop s)
        {
            if (s == null) return;
            if (p.Candidate == null) { p.Candidate = s; if (p.FirstEndAt < 0) p.FirstEndAt = S.Clock; }
            else p.Passed.Add(PassedLine(p, s));
        }

        string PassedLine(SkipPlan p, TimeStop s)
        {
            if (s.Kind == StopKind.Approach && s.Actor != null && p.Kind == SkipKind.Together) return LineBank.FixParticles($"{Cast.GivenOf(s.Actor)}이(가) 말을 걸려다 돌아갔다");
            return s.Title + (s.Sub != null && s.Kind != StopKind.Approach ? " — " + s.Sub : "");
        }

        bool MealStops(SkipPlan p) => !p.CriticalOnly && !p.Quiet && (p.Kind == SkipKind.Wait || p.Kind == SkipKind.Until || p.Kind == SkipKind.Pastime);

        /// <summary>Does a non-critical stop end this kind of plan (the masks in the spec's §3.3)?</summary>
        bool StopsFor(SkipPlan p, TimeStop s)
        {
            if (p.CriticalOnly) return false;
            switch (p.Kind)
            {
                case SkipKind.Sleep: return s.Kind == StopKind.Woken;
                case SkipKind.Together: return s.Kind == StopKind.PartnerLeft;
                case SkipKind.Wait: case SkipKind.Until: case SkipKind.Pastime:
                    if (p.Quiet) return s.Kind == StopKind.Appointment;
                    return s.Class >= StopClass.Social;
                default: return false;
            }
        }

        bool NaturalEnd(SkipPlan p)
        {
            switch (p.Kind)
            {
                case SkipKind.Toll: return !TollPending && p.Ticks > 0 || S.Clock >= p.Target;
                case SkipKind.Settle:
                    if (S.Clock >= p.Target) return true;
                    if (p.Ticks < 2 || p.MinUntil >= 0 && S.Clock < p.MinUntil) return false;
                    if (p.DayStart && (!EveryoneSetOff() || S.Clock - p.Start < 12 && Clumped(p.StartRoom))) return false;
                    return Settled();
                default: return S.Clock >= p.Target;
            }
        }

        void MinuteScan(SkipPlan p)
        {
            var me = S.Player;
            foreach (var r in S.Requests ?? new List<Request>())
            {
                if (r.Kind != "invite") continue;
                if (r.State == "accepted" && S.Clock >= r.At - 5 && S.Clock < r.At + 25 && me.Room != r.Room && _reminded.Add("appt:" + r.Id))
                {
                    var s = Mk(StopKind.Appointment, StopClass.Social, "약속 시간이 다 되었다", $"{Cast.GivenOf(r.From)} · {S.RoomName(r.Room)}", r.From, r.Room);
                    if (StopsFor(p, s)) Candidate(p, s); else p.Passed.Add(s.Title + " — " + s.Sub);
                }
                if (r.State == "met" && !p.MetAtStart.Contains(r.Id) && _reminded.Add("met:" + r.Id))
                {
                    var s = Mk(StopKind.Appointment, StopClass.Social, $"{Cast.GivenOf(r.From)}이(가) 약속 장소에 왔다", S.RoomName(r.Room), r.From, r.Room);
                    if (StopsFor(p, s)) Candidate(p, s); else p.Passed.Add(s.Title);
                }
            }
            foreach (var g in S.Gatherings ?? new List<Gathering>())
            {
                if (g.Cancelled || g.Done || g.Revs.Count == 0 || !KnowsGathering(g)) continue;
                var cur = g.Cur;
                if (S.Clock >= cur.Start - 5 && S.Clock < cur.End && _reminded.Add($"gath:{g.Id}:{g.Rev}"))
                {
                    var s = Mk(StopKind.Gathering, StopClass.Social, $"{g.Label}이(가) 곧 시작된다", S.RoomName(cur.Room), g.Host, cur.Room);
                    if (StopsFor(p, s)) Candidate(p, s); else p.Passed.Add(s.Title);
                }
            }
        }

        bool DinersGathered()
        {
            var din = S.Layout.First(RoomType.Dining); if (din == null) return true;
            int n = 0, ok = 0;
            foreach (var a in S.LivingNpcs)
            {
                if (a.Act == null || a.Act.Id != "life:eat") continue;
                n++;
                if (a.Room == din.Id && (a.Pose == Pose.Sit || a.Anim == Anim.Eat)) ok++;
            }
            return n == 0 ? S.LivingNpcs.Any(a => a.Room == din.Id) : ok >= n * 0.6;
        }

        StopClass AnnouncementClass(Announcement an, out string title)
        {
            string k = an.Key ?? "";
            if (k.StartsWith("y_body")) { title = "시신 발견 안내가 울렸다"; return StopClass.Critical; }
            if (k == "y_invest_start") { title = "수사가 시작되었다"; return StopClass.Critical; }
            if (k == "y_invest_extend") { title = "수사 시간이 늘어났다"; return StopClass.Critical; }
            if (k == "y_trial_summon") { title = "심판 소집 종이 울렸다"; return StopClass.Critical; }
            if (k.StartsWith("y_rule_")) { title = "새 규칙이 발표되었다"; return StopClass.Critical; }
            if (k == "y_hunger")
            {
                // the swallowed room is the one whose flag was set this very minute
                int eaten = -1; foreach (var kv in S.Flags) if (kv.Key.StartsWith("swallowed:") && Math.Abs(kv.Value - an.Clock) < 0.01 && int.TryParse(kv.Key.Substring(10), out var rid)) eaten = rid;
                var me = S.Player; bool near = false;
                if (eaten >= 0 && me != null)
                {
                    if (me.Room == eaten) near = true;
                    else { var er = S.Layout.Room(eaten); if (er != null) foreach (var did in er.Doors) { var d = S.Layout.Doors[did]; if (d.RoomA == me.Room || d.RoomB == me.Room) near = true; } }
                }
                title = eaten >= 0 ? $"저택이 {S.RoomName(eaten)}을(를) 삼켰다" : "저택 어딘가의 방이 닫혔다";
                return near ? StopClass.Critical : StopClass.Ambient;
            }
            if (k == "y_meal_breakfast") { title = "아침 식사 종이 울렸다"; return StopClass.Social; }
            if (k == "y_meal_dinner") { title = "저녁 식사 종이 울렸다"; return StopClass.Social; }
            if (k == "y_morning") { title = "아침 종이 울렸다"; return StopClass.Ambient; }
            if (k == "y_night") { title = "밤 10시 종이 울렸다"; return StopClass.Ambient; }
            if (k == "y_repair") { title = "수리 안내 방송이 나왔다"; return StopClass.Ambient; }
            if (k == "y_idle") { title = "방송이 흘러나왔다"; return StopClass.Ambient; }
            if (k == "y_trial_night" || k == "y_trial_day") { title = "심판이 끝났다는 안내가 나왔다"; return StopClass.Ambient; }
            title = "안내 방송이 울렸다"; return StopClass.Social;
        }

        static string PhaseTitle(Phase ph)
        {
            switch (ph)
            {
                case Phase.Investigation: return "수사가 시작되었다";
                case Phase.Assembly: return "심판 소집 종이 울렸다";
                case Phase.Trial: return "심판이 시작된다";
                case Phase.Daily: return "새 하루가 시작되었다";
                default: return "상황이 바뀌었다";
            }
        }

        /// <summary>The player stops the skip (Esc, a movement key). While a murder is in a hot moment (OnDemand daily life) the house
        /// does not stop there — that would freeze a killer mid-act for a free tour: it runs on silently (critical stops only, at
        /// most 30 clock minutes) until the moment has passed, then the skip ends as cancelled. p.Done says whether it ended now.</summary>
        public void CancelSkip(SkipPlan p)
        {
            if (p == null || p.Done) return;
            if (p.CancelAt >= 0) return;   // already running on until it cools
            if (p.Begun && Deferrable(p) && AnyPlanHot())
            {
                p.CancelAt = S.Clock; p.CriticalOnly = true;
                S.Dev($"skip cancel deferred {p.Kind} '{p.Label}' at {ClockFmt.HM(S.Clock)} (a plan is hot)");
                return;
            }
            AbortSkip(p);
        }

        /// <summary>Ends the skip at once, as cancelled (errors, or the presentation ending it for good).</summary>
        public void AbortSkip(SkipPlan p)
        {
            if (p == null || p.Done) return;
            p.Stop = Mk(StopKind.Cancelled, StopClass.Ambient, "기다리기를 멈췄다"); p.Done = true;
        }

        public SkipResult EndSkip(SkipPlan p)
        {
            var res = new SkipResult(); if (p == null) return res;
            if (!p.Done) AbortSkip(p);
            var me = S.Player;
            res.Minutes = S.Clock - p.Start; res.Stop = p.Stop;
            res.Interrupted = p.Stop != null && p.Stop.Kind != StopKind.Target;
            res.Title = p.Stop?.Title; res.Lines = p.Passed.Distinct().ToList();
            res.Overheard = Math.Max(0, S.K(Cast.Player).Statements.Count - p.StmtAtStart);
            double planned = Math.Max(0.1, p.Target - p.Start);
            float frac = (float)Math.Min(1.0, res.Minutes / planned);
            bool full = !res.Interrupted || p.Natural;
            switch (p.Kind)
            {
                case SkipKind.Sleep:
                    if (me != null && me.Alive)
                    {
                        if (full) me.Needs.Energy = 1f; else me.Needs.Energy = MathX.Clamp01(me.Needs.Energy + (float)(res.Minutes / 60.0 / 7.0));
                        me.Needs.LastSleep = S.Clock;
                        // out of bed (also when a noise already woke the body: the bed is free again either way)
                        if (me.Spot >= 0 && (p.SeatedByKernel || me.Pose == Pose.Sleep || S.Layout.Spots[me.Spot].Tag == "sleep")) PlayerStand();
                        else if (me.Pose == Pose.Sleep) me.Pose = Pose.Stand;
                        me.Anim = Anim.Idle;
                    }
                    break;
                case SkipKind.Pastime:
                    res.Activity = FinishUse(p.Use, res.Minutes, p);
                    break;
                case SkipKind.Together:
                    EndTogether(p, res, full ? 1f : frac);
                    break;
            }
            if (p.Kind == SkipKind.Sleep && full) res.Title = "아침 종이 울렸다";
            S.Emit(GameEventType.ClockSkip, Cast.Player, value: (float)res.Minutes, data: p.Kind.ToString(), text: res.Title);
            S.Dev($"skip end {p.Kind} {res.Minutes:0.0}min ticks={p.Ticks} stop={p.Stop?.Kind} '{p.Stop?.Title}' deferred={p.Deferred} settle={p.SettledMinutes:0.0}");
            return res;
        }

        /// <summary>Begin + step to the end + End, in one call (headless and tests).</summary>
        public SkipResult RunSkip(SkipPlan p)
        {
            if (p == null) return null;
            BeginSkip(p);
            int guard = 0; while (!StepSkip(p, 2000) && guard++ < 5000) { }
            return EndSkip(p);
        }
    }
}
