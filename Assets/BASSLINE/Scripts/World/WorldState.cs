using System;
using System.Collections.Generic;
using System.Linq;
using BASSLINE.Core;

namespace BASSLINE.World
{
    [Serializable] public sealed class ActorRecord
    {
        public string Id, RoomId;
        public double X,Y,Z;
        public ActorRecord Copy() => (ActorRecord)MemberwiseClone();
    }
    [Serializable] public sealed class DoorRecord
    {
        public string Id, RoomA, RoomB, OpenState="Closed", LockState="Unlocked", AccessPolicy="Public", OwnerId="";
        public double X,Y,Z,Width;
        public DoorRecord Copy() => (DoorRecord)MemberwiseClone();
    }
    [Serializable] public sealed class AuthorityEvent
    {
        public string Id,CommandId,ActorId,TargetId,Type;
        public long Tick,BeforeRevision;
        public AuthorityEvent Copy() => (AuthorityEvent)MemberwiseClone();
    }
    [Serializable] public sealed class ProcessedCommand
    {
        public CommandEnvelope Command;
        public CommandResult Result;
    }
    [Serializable] public sealed class WorldSnapshot
    {
        public string SessionId,MapVersion,RuleSetVersion;
        public long Revision,SequenceCursor;
        public string[] RoomIds;
        public ActorRecord[] Actors;
        public DoorRecord[] Doors;
        public AuthorityEvent[] Events;
        public ProcessedCommand[] Processed;
    }
    // A-only assembly. NPC planners, UI and logic assemblies cannot reference this assembly.
    public sealed class WorldState : ICommandPort
    {
        readonly WorldClock clock; readonly PauseCoordinator pause;
        readonly string sessionId,mapVersion,ruleSetVersion;
        readonly HashSet<string> rooms;
        readonly Dictionary<string,ActorRecord> actors;
        readonly Dictionary<string,DoorRecord> doors;
        readonly List<AuthorityEvent> events=new List<AuthorityEvent>();
        readonly Dictionary<string,ProcessedCommand> processed=new Dictionary<string,ProcessedCommand>(StringComparer.Ordinal);
        long revision,sequence;
        public WorldState(WorldClock clock,PauseCoordinator pause,string sessionId,string mapVersion,string ruleSetVersion,
            IEnumerable<string> roomIds,IEnumerable<ActorRecord> actors,IEnumerable<DoorRecord> doors)
        {
            this.clock=clock;this.pause=pause;this.sessionId=new StableId(sessionId).ToString();this.mapVersion=mapVersion;this.ruleSetVersion=ruleSetVersion;
            var allRooms=roomIds.ToArray();rooms=new HashSet<string>(allRooms,StringComparer.Ordinal);
            if(rooms.Count!=allRooms.Length)throw new ArgumentException("Duplicate rooms");
            foreach(var id in rooms)_=new StableId(id);
            this.actors=actors.ToDictionary(x=>new StableId(x.Id).ToString(),x=>x.Copy(),StringComparer.Ordinal);
            this.doors=doors.ToDictionary(x=>new StableId(x.Id).ToString(),x=>x.Copy(),StringComparer.Ordinal);
            foreach(var actor in this.actors.Values) if(!rooms.Contains(actor.RoomId)||!Finite(actor.X,actor.Y,actor.Z))throw new ArgumentException("Actor pose/FK invalid");
            foreach(var door in this.doors.Values)
                if(!rooms.Contains(door.RoomA)||!rooms.Contains(door.RoomB)||door.RoomA==door.RoomB||door.Width<=0||!Finite(door.X,door.Y,door.Z,door.Width)||
                    !new[]{"Closed","Open","Opening","Closing","Blocked"}.Contains(door.OpenState)||!new[]{"Unlocked","KeyLocked","AbilityLocked","FacilityControlled"}.Contains(door.LockState)||
                    !new[]{"Public","OwnerPermission","Facility"}.Contains(door.AccessPolicy)||(door.AccessPolicy=="OwnerPermission"&&!this.actors.ContainsKey(door.OwnerId)))throw new ArgumentException("Door definition/state invalid");
        }
        static bool Finite(params double[] values)=>values.All(x=>!double.IsNaN(x)&&!double.IsInfinity(x));
        static CommandEnvelope Copy(CommandEnvelope c)=>new CommandEnvelope {CommandId=c.CommandId,ActorId=c.ActorId,TargetId=c.TargetId,Action=c.Action,ExpectedRevision=c.ExpectedRevision,RequestTick=c.RequestTick};
        static bool Same(CommandEnvelope a,CommandEnvelope b)=>a.CommandId==b.CommandId&&a.ActorId==b.ActorId&&a.TargetId==b.TargetId&&a.Action==b.Action&&a.ExpectedRevision==b.ExpectedRevision&&a.RequestTick==b.RequestTick;
        public CommandResult Submit(CommandEnvelope command)
        {
            if(command==null)return CommandResult.Reject("InvalidCommand",revision);
            try {_=new StableId(command.CommandId);_=new StableId(command.ActorId);_=new StableId(command.TargetId);}catch(ArgumentException){return CommandResult.Reject("InvalidCommand",revision);}
            if(processed.TryGetValue(command.CommandId,out var previous))return Same(previous.Command,command)?previous.Result.Copy():CommandResult.Reject("CommandIdConflict",revision);
            CommandResult result;
            if(command.ExpectedRevision!=revision)result=CommandResult.Reject("StaleRevision",revision);
            else if(command.RequestTick!=clock.Tick)result=CommandResult.Reject("StaleTick",revision);
            else if(pause.IsPaused(ClockScope.World))result=CommandResult.Reject("WorldPaused",revision);
            else if(!actors.TryGetValue(command.ActorId,out var actor)||!doors.TryGetValue(command.TargetId,out var door))result=CommandResult.Reject("Unavailable",revision);
            else if(command.Action!="RequestUnlock")result=CommandResult.Reject("NotImplemented",revision);
            else if(actor.RoomId!=door.RoomA&&actor.RoomId!=door.RoomB)result=CommandResult.Reject("Unavailable",revision);
            else if(Math.Pow(actor.X-door.X,2)+Math.Pow(actor.Y-door.Y,2)+Math.Pow(actor.Z-door.Z,2)>2.25)result=CommandResult.Reject("Unavailable",revision);
            else if(door.AccessPolicy!="OwnerPermission"||door.OwnerId!=actor.Id||door.LockState!="KeyLocked")result=CommandResult.Reject("Unavailable",revision);
            else
            {
                // P0 state-only unlock. This never opens a leaf, moves a character or grants passage.
                var evt=new AuthorityEvent {Id="EV_"+sessionId+"_"+(sequence+1).ToString(System.Globalization.CultureInfo.InvariantCulture),CommandId=command.CommandId,ActorId=actor.Id,TargetId=door.Id,Type="DoorUnlocked",Tick=clock.Tick,BeforeRevision=revision};
                door.LockState="Unlocked";sequence++;revision++;events.Add(evt);
                result=new CommandResult {Status=CommandStatus.Committed,ReasonCode="Committed",PublicReason="Action completed",NewRevision=revision,EventIds=new[]{evt.Id}};
            }
            processed.Add(command.CommandId,new ProcessedCommand {Command=Copy(command),Result=result.Copy()});
            return result.Copy();
        }
        public WorldSnapshot Capture() => new WorldSnapshot {
            SessionId=sessionId,MapVersion=mapVersion,RuleSetVersion=ruleSetVersion,Revision=revision,SequenceCursor=sequence,
            RoomIds=rooms.OrderBy(x=>x,StringComparer.Ordinal).ToArray(),
            Actors=actors.Values.OrderBy(x=>x.Id,StringComparer.Ordinal).Select(x=>x.Copy()).ToArray(),
            Doors=doors.Values.OrderBy(x=>x.Id,StringComparer.Ordinal).Select(x=>x.Copy()).ToArray(),
            Events=events.Select(x=>x.Copy()).ToArray(),
            Processed=processed.Values.OrderBy(x=>x.Command.CommandId,StringComparer.Ordinal).Select(x=>new ProcessedCommand {Command=Copy(x.Command),Result=x.Result.Copy()}).ToArray()
        };
        public static WorldState Restore(WorldSnapshot s,WorldClock clock,PauseCoordinator pause,string map,string rules)
        {
            if(s.MapVersion!=map||s.RuleSetVersion!=rules)throw new InvalidOperationException("Unsupported map/rules migration");
            if(s.Revision<0||s.SequenceCursor!=s.Events.LongLength||s.Revision!=s.SequenceCursor)throw new ArgumentException("Invalid event cursor");
            var result=new WorldState(clock,pause,s.SessionId,map,rules,s.RoomIds,s.Actors,s.Doors);
            long seq=0,lastTick=-1;var unlockedDoors=new HashSet<string>();
            foreach(var evt in s.Events)
            {
                seq++;
                if(evt.Id!="EV_"+s.SessionId+"_"+seq||evt.BeforeRevision!=seq-1||evt.Tick<0||evt.Tick>clock.Tick||!result.actors.ContainsKey(evt.ActorId)||!result.doors.ContainsKey(evt.TargetId)||evt.Type!="DoorUnlocked")throw new ArgumentException("Invalid event tail");
                if(evt.Tick<lastTick||!unlockedDoors.Add(evt.TargetId)||result.doors[evt.TargetId].LockState!="Unlocked"||result.doors[evt.TargetId].OwnerId!=evt.ActorId)throw new ArgumentException("Event/state mismatch");lastTick=evt.Tick;
                result.events.Add(evt.Copy());
            }
            result.revision=s.Revision;result.sequence=s.SequenceCursor;
            foreach(var p in s.Processed)
            {
                _=new StableId(p.Command.CommandId);
                _=new StableId(p.Command.ActorId);_=new StableId(p.Command.TargetId);
                if(p.Result.NewRevision<0||p.Result.NewRevision>s.Revision||p.Result.EventIds.Any(id=>!s.Events.Any(e=>e.Id==id&&e.CommandId==p.Command.CommandId)))throw new ArgumentException("Invalid command receipt");
                if(p.Result.Status==CommandStatus.Committed)
                {
                    if(p.Result.EventIds.Length!=1)throw new ArgumentException("Committed receipt must name one event");
                    var evt=s.Events.Single(e=>e.Id==p.Result.EventIds[0]);
                    if(p.Command.Action!="RequestUnlock"||p.Command.ActorId!=evt.ActorId||p.Command.TargetId!=evt.TargetId||p.Command.RequestTick!=evt.Tick||p.Command.ExpectedRevision!=evt.BeforeRevision||p.Result.NewRevision!=evt.BeforeRevision+1)throw new ArgumentException("Receipt payload mismatch");
                }
                else if(p.Result.Status!=CommandStatus.Rejected||p.Result.EventIds.Length!=0)throw new ArgumentException("Invalid rejection receipt");
                result.processed.Add(p.Command.CommandId,new ProcessedCommand {Command=Copy(p.Command),Result=p.Result.Copy()});
            }
            foreach(var evt in s.Events) if(!result.processed.TryGetValue(evt.CommandId,out var p)||p.Result.Status!=CommandStatus.Committed||!p.Result.EventIds.Contains(evt.Id))throw new ArgumentException("Missing event receipt");
            return result;
        }
    }
}
