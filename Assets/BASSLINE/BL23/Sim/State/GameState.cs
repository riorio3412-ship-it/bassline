using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>The single authoritative world state (A-ledger). Everything the save needs is in here.</summary>
    [Serializable]
    public sealed class GameState
    {
        public int Schema = 23;
        public string CampaignId; public string BuildVersion;
        public RngSet Rng = new RngSet();
        public int Loop = 1; public int Chapter = 1; public string ChapterStartId;
        public long Tick; public double Clock; public long Seq;
        public Phase Phase = Phase.Boot; public double PhaseStart; public int FloorPreset = 4; public int FloorLocked = 4;
        public float ClockRate = 0.5f;     // clock minutes per sim second
        public Layout Layout;
        public Dictionary<string, Actor> Actors = new Dictionary<string, Actor>();
        public Dictionary<string, Item> Items = new Dictionary<string, Item>();
        public List<Trace> Traces = new List<Trace>();
        public List<LedgerEvent> Ledger = new List<LedgerEvent>();
        public Dictionary<string, Knowledge> Know = new Dictionary<string, Knowledge>();
        public Dictionary<string, Rel> Rels = new Dictionary<string, Rel>();
        public Dictionary<string, MurderPlan> Plans = new Dictionary<string, MurderPlan>();
        public Dictionary<string, Incident> Incidents = new Dictionary<string, Incident>();
        public Dictionary<string, Goal> Goals = new Dictionary<string, Goal>();
        public ChapterState Ch = new ChapterState();
        public List<AbilityAssignment> Abilities = new List<AbilityAssignment>();
        public Dictionary<string, string> PrevAbilityOwner = new Dictionary<string, string>();
        public List<Announcement> Announcements = new List<Announcement>();
        public List<Settlement> Settlements = new List<Settlement>();
        public List<string> Archive = new List<string>();          // closed case summaries (ArchiveMeta, not NPC knowledge)
        public TrialState Trial;
        public List<ReplaySegment> Replays = new List<ReplaySegment>();
        public List<ReplayFrame> ReplayBuf = new List<ReplayFrame>();
        public Profile Profile = new Profile();
        public MurderWorld Mur = new MurderWorld();   // the murder model: culprit schemes, their tells, favours (Sim/Murder/*.cs)
        public Dictionary<string, double> Flags = new Dictionary<string, double>();
        public List<string> DevLog = new List<string>();
        public bool PlayerDeadObserve; public string LastVerdictSummary;
        public int CircuitMask = -1;       // bit per circuit on
        public float Darkness; public float Noise;
        public double PressRam; public bool PressPowered = true; public string PressVictim; public double PressFireAt = -1; public string PressArmedBy;
        // everyday grammars (IG01-12 + BL23 trick extensions) — see Systems/Grammars.cs, Systems/Tricks.cs
        public List<Loan> Loans = new List<Loan>();
        public List<Gathering> Gatherings = new List<Gathering>();
        public List<Faction> Factions = new List<Faction>();   // who holds together (Sim/Life/Factions.cs), recomputed each morning
        public List<Request> Requests = new List<Request>();   // personal invitations and favours to 민혁 (Systems/Requests.cs)
        public List<Trap> Traps = new List<Trap>();
        public List<DeviceRecord> DeviceLog = new List<DeviceRecord>();
        public List<Repair> Repairs = new List<Repair>();
        public Dictionary<int, double> ClockOffset = new Dictionary<int, double>();   // clock furniture id → minutes ahead(+)/behind(-)
        public Dictionary<int, int> DoorLoggers = new Dictionary<int, int>();          // logged door id → logger furniture id
        public List<string> Deliveries = new List<string>();                           // courier records (IG10) "from|via|to|clock|item"
        public int NextId = 1;
        // --- violence track (Sim/Violence/ViolenceState.cs): prolonged assaults, bindings, firearm state, shots, embedded bolts
        public ViolenceState Violence = new ViolenceState();

        [NonSerialized] public List<GameEvent> Out = new List<GameEvent>();

        public string NewId(string prefix)
        {
            // (fix, concealment pass) loop-start items are "it1".."itN" from their own counter: a new "it" id must not land on one of
            // them (it replaced that item in Items — a key in someone's pocket became the new thing)
            string id; do id = prefix + (NextId++).ToString(); while (prefix == "it" && Items.ContainsKey(id));
            return id;
        }
        public Actor A(string id) => id != null && Actors.TryGetValue(id, out var a) ? a : null;
        public Item I(string id) => id != null && Items.TryGetValue(id, out var i) ? i : null;
        public Knowledge K(string id) { if (!Know.TryGetValue(id, out var k)) Know[id] = k = new Knowledge(); if (k.Open == null) k.Open = new Dictionary<string, Sighting>(); return k; }
        public Rel R(string a, string b) { string key = a + ">" + b; if (!Rels.TryGetValue(key, out var r)) Rels[key] = r = new Rel(); return r; }
        public bool HasRel(string a, string b) => Rels.ContainsKey(a + ">" + b);
        public IEnumerable<Actor> Living => Actors.Values.Where(a => a.Alive && !a.IsButler);
        public IEnumerable<Actor> LivingNpcs => Actors.Values.Where(a => a.Alive && !a.IsButler && !a.IsPlayer);
        public int Survivors => Actors.Values.Count(a => !a.IsButler && a.Alive);
        public Actor Player => A(Cast.Player);
        public Actor Butler => A(Cast.Butler);
        public Room RoomOf(Actor a) => Layout.Room(a.Room);
        public string RoomName(int r) => Layout.Room(r)?.Name ?? "알 수 없는 곳";
        public Rng R(Stream s) => Rng.Get(s, Loop);
        public int Minute => (int)Math.Floor(Clock % 1440);
        public int Day => (int)Math.Floor(Clock / 1440) + 1;
        public bool IsNight => Minute >= 22 * 60 || Minute < 7 * 60;
        public bool RuleActive(string rule) => Ch.Rules.Any(r => r.Rule == rule && r.Active);
        public RuleInstance Rule(string rule) => Ch.Rules.FirstOrDefault(r => r.Rule == rule && r.Active);
        public bool CircuitOn(int c) => c < 0 || (CircuitMask & (1 << c)) != 0;

        public LedgerEvent Log(string type, string actor, string target = null, string item = null, int room = -1, P3? pos = null, string data = null, string plan = null, bool secret = false)
        {
            var e = new LedgerEvent { Seq = ++Seq, Tick = Tick, Clock = Clock, Type = type, Actor = actor, Target = target, Item = item, Room = room, Pos = pos ?? default, Data = data, Plan = plan, Chapter = Chapter, Secret = secret };
            Ledger.Add(e); return e;
        }

        public GameEvent Emit(GameEventType t, string actor = null, string target = null, string text = null, int room = -1, P3? pos = null, float value = 0, string data = null, int id = -1, string key = null)
        {
            var e = new GameEvent { Type = t, Actor = actor, Target = target, Text = LineBank.FixParticles(text), Room = room, Pos = pos ?? default, Value = value, Data = data, Id = id, Key = key, Tick = Tick, Clock = Clock };
            if (Out == null) Out = new List<GameEvent>();
            Out.Add(e); if (Out.Count > 4000) Out.RemoveRange(0, 1000);
            return e;
        }
        public void Dev(string s) { DevLog.Add($"[{ClockFmt.DayHM(Clock)} L{Loop}C{Chapter}] {s}"); if (DevLog.Count > 3000) DevLog.RemoveRange(0, 500); }
    }
}
