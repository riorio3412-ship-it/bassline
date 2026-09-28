using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// World kernel. Fixed 10Hz ticks. Order per tick: clock schedule → bodies → facilities → NPC brains/movement →
    /// perception (staggered) → conversations → replay record. The player moves in Unity and reports its pose each frame.
    /// </summary>
    public sealed partial class Simulation
    {
        public GameState S;
        public const float WalkSpeed = 1.35f, RunSpeed = 3.1f;
        public const float DailyRate = 0.5f, InvestigationRate = 1f / 12f, NightFastRate = 0.5f;
        public bool Headless;              // dotnet tests: no presentation
        public Func<string, bool> PlayerBusy; // UI hook: player in menu/dialogue

        public static Simulation NewCampaign(ulong seed, int floorPreset = 4, string build = "BL23")
        {
            var sim = new Simulation { S = new GameState() };
            sim.S.CampaignId = "C" + seed.ToString("X");
            sim.S.BuildVersion = build;
            sim.S.Rng.CampaignSeed = seed;
            sim.S.FloorPreset = floorPreset;
            sim.S.Profile.FloorPreset = floorPreset;
            sim.StartLoop(1);
            return sim;
        }

        public static Simulation FromState(GameState s)
        {
            var sim = new Simulation { S = s };
            if (s.Out == null) s.Out = new List<GameEvent>();
            foreach (var k in s.Know.Values) { k.Open = new Dictionary<string, Sighting>(); }
            s.Layout.InvalidateNav();
            // --- time-on-demand (begin): the minute schedule does not replay the minute the save was made in (no second bell),
            // and conversations (not saved) do not leave people standing frozen facing each other
            sim._lastMinute = (int)Math.Floor(s.Clock);
            foreach (var a in s.Actors.Values) if (a.TalkingTo != null && !a.IsPlayer) a.TalkingTo = null;
            // --- time-on-demand (end)
            return sim;
        }

        // ------------------------------------------------------------------ loop / chapter lifecycle
        public void StartLoop(int loop)
        {
            var S = this.S;
            S.Loop = loop; S.Chapter = 1; S.Tick = 0; S.Clock = 7 * 60 + 40; // day 1, 07:40
            S.FloorLocked = S.FloorPreset;
            S.Layout = LayoutGenerator.Generate(S.Rng.CampaignSeed, loop, S.Rng);
            S.Actors.Clear(); S.Items.Clear(); S.Traces.Clear(); S.Know.Clear(); S.Rels.Clear(); S.Plans.Clear(); S.Incidents.Clear(); S.Goals.Clear();
            S.Announcements.Clear(); S.Replays.Clear(); S.Trial = null; S.Ledger.Clear(); S.CircuitMask = -1; S.Darkness = 0; S.Noise = 0; S.ReplayBuf.Clear();
            foreach (var key in S.Flags.Keys.Where(k => !k.StartsWith("persist:")).ToList()) S.Flags.Remove(key);
            S.PressRam = 0; S.PressPowered = true; S.PressVictim = null; S.PressFireAt = -1;
            SpawnActors();
            SpawnItems();
            Methods.SpawnLoop(this);   // weapons and tools that belong to particular rooms (BL23 murder content)
            Relations.InitLoop(S);
            Goals.InitLoop(S);
            Abilities.AssignLoop(S);
            Grammars.InitLoop(this);
            S.Ch = new ChapterState();
            S.Log("LoopStart", null, data: $"loop={loop} layout={S.Layout.Hash} skel={S.Layout.Skeleton}");
            S.Dev($"loop {loop} start layout {S.Layout.Hash} attempts {S.Layout.Attempts}");
            S.Phase = loop == 1 ? Phase.Prologue : Phase.Daily;
            S.PhaseStart = S.Clock;
            BeginChapter(true);
            S.Emit(GameEventType.LoopReset, data: loop.ToString());
        }

        void SpawnActors()
        {
            var L = S.Layout;
            var hall = L.Rooms.First(r => r.Type == RoomType.GrandHall && r.Floor == 0);
            var gather = L.Spots.Where(s => s.Room == hall.Id).ToList();
            var rng = S.R(Stream.Life);
            int i = 0;
            foreach (var c in Cast.All)
            {
                var a = new Actor { Id = c.Id };
                // everyone wakes up in their own room on loop start except the prologue gathering in the hall
                var bed = L.BedroomOf(c.Id);
                P3 p;
                if (c.IsButler) { var br = L.First(RoomType.ButlerRoom); p = br != null ? new P3(br.Floor, br.Rect.CX, br.Rect.CZ) : new P3(0, hall.Rect.CX, hall.Rect.z0 + 2); }
                else if (S.Loop == 1)
                {
                    double ang = i / 18.0 * Math.PI * 2; float r = Math.Min(hall.Rect.W, hall.Rect.D) * 0.3f;
                    p = new P3(0, hall.Rect.CX + (float)Math.Sin(ang) * r, hall.Rect.CZ + (float)Math.Cos(ang) * r + 3f);
                    i++;
                }
                else p = bed != null ? SpotIn(bed.Id, "stand") : new P3(0, hall.Rect.CX, hall.Rect.CZ);
                a.Pos = Snap(p); a.Room = L.RoomAt(a.Pos); a.Yaw = rng.Range(0, 360);
                a.Needs.Hunger = rng.Range(0.2f, 0.45f); a.Needs.Energy = rng.Range(0.75f, 0.95f); a.Needs.Social = rng.Range(0.3f, 0.7f); a.Needs.Fun = rng.Range(0.3f, 0.6f);
                a.NextThink = S.Clock + rng.Range(0.5f, 3f); a.NextPerceive = S.Clock; a.NextSocial = S.Clock + rng.Range(10, 40);
                a.Needs.Stress = rng.Range(0f, 0.35f); a.Needs.Anger = rng.Range(0f, 0.15f); S.Flags["desp:" + c.Id] = rng.Range(0.6f, 1.6f); S.Flags["inh:" + c.Id] = rng.Range(-0.24f, 0.24f);
                a.NextThink = S.Clock + rng.Range(0.5f, 3f); a.NextPerceive = S.Clock; a.NextSocial = S.Clock + rng.Range(10, 40);
                S.Actors[c.Id] = a; S.K(c.Id);
            }
            if (S.Loop == 1) { var b = S.Butler; b.Pos = Snap(new P3(0, hall.Rect.CX, hall.Rect.z1 - 2.5f)); b.Room = hall.Id; b.Yaw = 180; }
        }

        P3 SpotIn(int room, string tag)
        {
            var sp = S.Layout.Spots.FirstOrDefault(s => s.Room == room && s.Tag == tag) ?? S.Layout.Spots.FirstOrDefault(s => s.Room == room);
            var r = S.Layout.Room(room);
            return sp != null ? sp.Approach : new P3(r.Floor, r.Rect.CX, r.Rect.CZ);
        }

        public P3 Snap(P3 p)
        {
            var g = S.Layout.Nav(p.f); int k = Pathfinder.Snap(g, p); return k >= 0 ? g.Center(k) : p;
        }

        void SpawnItems()
        {
            int n = 0;
            foreach (var sp in S.Layout.ItemSpawns)
            {
                var def = ItemCatalog.Get(sp.Type); if (def == null) continue;
                var it = new Item { Id = "it" + (++n), Type = sp.Type, Name = sp.Name, Pos = sp.Pos, Room = sp.Room, Yaw = sp.Yaw, Owner = sp.Owner, HomeRoom = sp.Room, HomePos = sp.Pos };
                if (sp.Type == "RoomKey" && sp.Owner != null) { it.KeyFor = "key_" + sp.Owner; it.Holder = sp.Owner; it.Room = -1; S.A(sp.Owner)?.Pocket.Add(it.Id); }
                S.Items[it.Id] = it;
            }
            // butler master key
            var mk = new Item { Id = "it_master", Type = "MasterKey", Name = "유스티의 열쇠 꾸러미", KeyFor = "key_master;key_butler", Holder = Cast.Butler, Room = -1 };
            S.Items[mk.Id] = mk; S.Butler.Pocket.Add(mk.Id);
            // a call bell in every non-passage room is modeled as a fixed facility (no item)
        }

        /// <summary>ChapterStart: commits chapter number, StartN, cap and rule plan as one event. Never on day change or extra victim.</summary>
        public void BeginChapter(bool first)
        {
            if (!first) S.Chapter++;
            Hunger.Release(this);
            S.ChapterStartId = $"L{S.Loop}C{S.Chapter}";
            int n = S.Survivors;
            S.Ch = new ChapterState { StartN = n, ChapterStartClock = S.Clock };
            S.Ch.VictimCap = n >= 7 ? 2 : (n >= 4 ? 1 : 0);
            Rules.SelectForChapter(this);
            if (S.RuleActive("CH21") && n >= 8) S.Ch.VictimCap = 3;
            S.Log("ChapterStart", null, data: $"chapter={S.Chapter} startN={n} cap={S.Ch.VictimCap} rules={string.Join(",", S.Ch.Rules.Select(r => r.Rule))}");
            S.Dev($"chapter {S.Chapter} start N={n} cap={S.Ch.VictimCap} rules={string.Join(",", S.Ch.Rules.Select(r => r.Rule))}");
            S.Emit(GameEventType.ChapterStart, data: S.Chapter.ToString(), value: n);
            if (!first) SetPhase(Phase.Daily);
        }

        public void SetPhase(Phase p)
        {
            if (S.Phase == p) return;
            var old = S.Phase; S.Phase = p; S.PhaseStart = S.Clock;
            S.ClockRate = p == Phase.Investigation ? InvestigationRate : DailyRate;
            PendingStop = null;   // --- time-on-demand: a phase change is its own stop
            S.Log("Phase", null, data: $"{old}->{p}");
            S.Emit(GameEventType.PhaseChanged, data: p.ToString(), text: old.ToString());
        }

        // ------------------------------------------------------------------ main tick
        public void Step()
        {
            var S = this.S;
            if (S.Phase == Phase.Trial || S.Phase == Phase.Verdict || S.Phase == Phase.Execution || S.Phase == Phase.Reveal || S.Phase == Phase.Settlement || S.Phase == Phase.LoopEpilogue || S.Phase == Phase.Boot)
            {
                S.Tick++; return; // world frozen underground; trial has its own clock
            }
            S.Tick++;
            S.Clock += S.ClockRate * SimTime.Dt;
            if (_gatherSoon.Count > 0) Guard("gather-soon", FlushGatherSoon);   // --- time-on-demand: people called by the player's zero-time look (TimeFlow.cs)
            // each stage is isolated: a fault in one system (or one person) is logged and must not stop the world
            Guard("schedule", Schedule); Guard("bodies", Bodies); Guard("facilities", Facilities); Guard("grammars", () => Grammars.Tick(this));
            foreach (var a in S.Actors.Values)
            {
                if (a.IsPlayer || !a.Alive) continue;
                if (a.Status == ActorStatus.Unconscious) continue;
                try
                {
                    if (S.Clock >= a.NextThink && (a.Act == null || a.Act.Interruptible || a.Act.Cur == null)) Think(a);
                    Execute(a);
                }
                catch (Exception e) { Fault("actor " + a.Id + " " + (a.Act?.Id ?? "-") + "/" + (a.Act?.Cur?.Kind ?? "-"), e); a.Act = null; a.Speed = 0; a.NextThink = S.Clock + 1; }
            }
            Guard("crowd", Crowd);
            Guard("follow", FollowUpdate); Guard("perception", Perception); Guard("convos", Conversations);
            Guard("crime", () => Crime.Update(this)); Guard("cases", () => Cases.Update(this));
            if (S.Tick % 5 == 0) Guard("replay", () => Replay.Record(this));
        }

        public int Faults;
        void Guard(string what, Action f) { try { f(); } catch (Exception e) { Fault(what, e); } }
        void Fault(string what, Exception e)
        {
            Faults++;
            if (Faults < 200) S.Dev($"EXC {what}: {e.GetType().Name} {e.Message} @ {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
            OnFault?.Invoke(what, e);
        }
        public Action<string, Exception> OnFault;   // Unity layer forwards these to its log

        public void RunTicks(int n) { for (int i = 0; i < n; i++) Step(); }

        /// <summary>Accelerated waiting. Stops at audible alarms, announcements, or when stop() says so. Returns clock reached.</summary>
        public double Wait(double minutes, Func<bool> stop = null)
        {
            double target = S.Clock + minutes; int guard = 0; int startAnn = S.Announcements.Count;
            double start0 = S.Clock;   // --- time-on-demand: ClockSkip reports the minutes that really passed
            while (S.Clock < target && guard++ < 400000)
            {
                Step();
                if (S.Phase != Phase.Daily && S.Phase != Phase.Investigation) break;
                if (S.Announcements.Count != startAnn) break;
                if (stop != null && stop()) break;
                if (PlayerAlarm) { PlayerAlarm = false; break; }
            }
            S.Emit(GameEventType.ClockSkip, value: (float)(S.Clock - start0));
            return S.Clock;
        }
        // --- time-on-demand: PlayerAlarm is now a property over PendingStop (Systems/TimeFlow.cs)

        // ------------------------------------------------------------------ player interface (Unity writes its pose here)
        public void SetPlayerPose(P3 p, float yaw, bool running, bool crouch)
        {
            var a = S.Player; if (a == null || !a.Alive) return;
            if (a.CarriedBy != null) return;
            // --- time-on-demand (begin): a seat is kept only while the player holds a seat spot and is still at it (a teleport or
            // the elevator stands them up); a sleep without a spot is the legacy wait-sleep and is kept as before
            bool seated = a.Spot >= 0 && a.Spot < S.Layout.Spots.Count && S.Layout.Spots[a.Spot].Occupant == a.Id;
            if (a.Spot >= 0 && (!seated || S.Layout.Spots[a.Spot].Pos.f != p.f || S.Layout.Spots[a.Spot].Pos.DistXZ(p) > 2.0f)) { ReleaseSpot(a); seated = false; }
            a.Pos = p; a.Yaw = yaw; a.Running = running;
            a.Pose = seated && (a.Pose == Pose.Sit || a.Pose == Pose.Sleep) ? a.Pose : (a.Pose == Pose.Sleep && a.Spot < 0 && !crouch ? Pose.Sleep : crouch ? Pose.Crouch : Pose.Stand);
            // --- time-on-demand (end)
            int r = S.Layout.RoomAt(p); if (r >= 0 && r != a.Room) { int old = a.Room; a.Room = r; S.Log("Enter", a.Id, room: r); OnEnterRoom(a, old, r); }
            if (a.Carrying != null) { var c = S.A(a.Carrying); if (c != null) { c.Pos = p; c.Room = a.Room; } }
        }
    }
}
