using System;
using System.Linq;
using System.Collections.Generic;
using BASSLINE.Core;

namespace BASSLINE.World.Mansion
{
    [Serializable] public sealed class ResidentState
    {
        public string Id,Node,Destination="",Activity="Rest",Phase="Idle",HeldObject="";
        public Point3 Position,QueuePosition;public string QueueDoor="";public double Yaw;public bool Alive=true,Present=true;
        public bool Recovering;public Point3 RecoveryPoint,RecoveryTarget;public int RecoveryTicks;
        public string[] Path=Array.Empty<string>(),KnownLocked=Array.Empty<string>();
        public int PathCursor,ActivityTicks,CompletedActivities,StuckTicks,ScheduleCursor;
        public long NextSocialTick;
        public ResidentState Copy(){var c=(ResidentState)MemberwiseClone();c.Path=(string[])Path.Clone();c.KnownLocked=(string[])KnownLocked.Clone();return c;}
    }
    [Serializable] public sealed class MansionDoorState
    {
        public string Id,Holder="",OwnerId="";public bool Locked,RelockAfterUse,RelockPending,EmergencyOpen;public double Open;
        public string[] Queue=Array.Empty<string>();public long GrantedTick;
        public MansionDoorState Copy(){var c=(MansionDoorState)MemberwiseClone();c.Queue=(string[])Queue.Clone();return c;}
    }
    [Serializable] public sealed class MansionObjectState
    {
        public string Id,Name,Location="World",Owner="",AnchorId="";public Point3 Position;
        public int PhysicsVersion;
        public Point3 Rotation,LinearVelocity,AngularVelocity;
        public bool PhysicsSleeping;
        public long ReleaseEventSequence;
        public string[] ActiveContacts=Array.Empty<string>();
        public MansionObjectContact[] PhysicalContacts=Array.Empty<MansionObjectContact>();
        public MansionObjectState Copy(){var c=(MansionObjectState)MemberwiseClone();c.ActiveContacts=(string[])(ActiveContacts??Array.Empty<string>()).Clone();c.PhysicalContacts=(PhysicalContacts??Array.Empty<MansionObjectContact>()).Select(x=>x.Copy()).ToArray();return c;}
    }
    [Serializable] public sealed class MansionObjectContact
    {
        public string OtherId,ChainId;public long EventSequence,CauseEventSequence,BeginTick,EndTick=-1;
        public Point3 Position,Normal,RelativeVelocity;
        public MansionObjectContact Copy()=>(MansionObjectContact)MemberwiseClone();
    }
    [Serializable] public sealed class MansionEvent
    {
        public long Sequence,Tick;public string Type,Actor,Target,Detail;
        public MansionEvent Copy()=>(MansionEvent)MemberwiseClone();
    }
    [Serializable] public sealed class MansionState
    {
        public string Magic="BASSLINE_MANSION",MapVersion,RuleVersion="M01_LIFE_004",CatalogHash;
        public long Tick,Sequence;public int ClockVersion;public int Loop=1,Chapter=1;public double Yaw,Pitch,PendingTime;
        public Point3 PlayerVelocity;
        public ResidentState[] Residents=Array.Empty<ResidentState>();public MansionDoorState[] Doors=Array.Empty<MansionDoorState>();
        public MansionObjectState[] Objects=Array.Empty<MansionObjectState>();public MansionEvent[] Events=Array.Empty<MansionEvent>();
        public string[] PauseOwners=Array.Empty<string>();
        public MansionCaseBookSnapshot Cases;
        public MansionState Copy(){var c=(MansionState)MemberwiseClone();c.Residents=Residents.Select(x=>x.Copy()).ToArray();c.Doors=Doors.Select(x=>x.Copy()).ToArray();c.Objects=Objects.Select(x=>x.Copy()).ToArray();c.Events=Events.Select(x=>x.Copy()).ToArray();c.PauseOwners=(string[])PauseOwners.Clone();c.Cases=Cases?.Copy();return c;}
    }
    public sealed class MansionNode {public string Id,Room;public Point3 Position;}
    public sealed class MansionEdge {public string From,To,Door;}
    public interface IMansionPhysics
    {
        Point3 Move(string actor,Point3 motion);
        bool DoorClear(string id);
        void DoorPose(string id,double open);
    }
    public interface IMansionWaypointPhysics
    {
        bool CanBypassIntermediate(string actor,Point3 nextPoint);
    }
    /// <summary>Physical waiting beside an occupied passage, including actors approaching its endpoint.</summary>
    public interface IMansionTrafficPhysics
    {
        Point3 WaitForPassage(string actor,string door,Point3 approach,Point3 exit);
    }
    /// <summary>Optional holder-specific passage clearance. Waiting actors must not prevent a completed holder's release.
    /// Implementations return false while this holder still occupies the doorway; DoorClear remains the closing-safety check.</summary>
    public interface IMansionPassagePhysics
    {
        bool ActorClearedDoor(string id,string actor);
    }
    /// <summary>Authority for one physical mansion. Geometry and personal knowledge stay in separate adapters.</summary>
    public sealed partial class MansionWorld
    {
        readonly Dictionary<string,MansionNode> nodes;
        readonly Dictionary<string,MansionEdge[]> outgoing;
        readonly Dictionary<string,ResidentState> residents;
        readonly Dictionary<string,MansionDoorState> doors;
        readonly Dictionary<string,MansionObjectState> objects;
        readonly List<MansionEvent> history;
        readonly HashSet<string> pauses;
        readonly LocomotionSettings locomotion=new LocomotionSettings();
        static readonly HashSet<string> PassageActorIds=new HashSet<string>(Enumerable.Range(1,18).Select(i=>"CH_"+i.ToString("00")).Concat(new[]{"PRES_YUSTI"}),StringComparer.Ordinal);
        const long MinimumPassageTicks=90;
        bool PassageActorAvailable(string id)=>PassageActorIds.Contains(id)&&(id=="PRES_YUSTI"||residents.TryGetValue(id,out var actor)&&actor.Alive&&actor.Present);
        MansionState state;
        public long Tick=>state.Tick;
        public int ClockVersion=>state.ClockVersion;
        public bool Paused=>pauses.Count>0;
        public double Yaw {get=>state.Yaw;set=>state.Yaw=value;}
        public double Pitch {get=>state.Pitch;set=>state.Pitch=Math.Max(-75,Math.Min(75,value));}
        public double PendingTime {get=>state.PendingTime;set=>state.PendingTime=value;}
        public long EventSequence=>state.Sequence;
        public int Loop=>state.Loop;
        public int Chapter=>state.Chapter;
        public IEnumerable<ResidentState> Residents=>residents.Values;
        public IEnumerable<MansionDoorState> Doors=>doors.Values;
        public IEnumerable<MansionObjectState> Objects=>objects.Values;
        public IReadOnlyList<MansionEvent> Events=>history;
        public MansionWorld(MansionState initial,IEnumerable<MansionNode> mapNodes,IEnumerable<MansionEdge> edges)
        {
            nodes=mapNodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
            outgoing=edges.GroupBy(e=>e.From).ToDictionary(g=>g.Key,g=>g.OrderBy(e=>e.To,StringComparer.Ordinal).ToArray());
            Validate(initial,nodes);
            state=initial.Copy();residents=state.Residents.ToDictionary(r=>r.Id);doors=state.Doors.ToDictionary(d=>d.Id);objects=state.Objects.ToDictionary(o=>o.Id);
            history=new List<MansionEvent>(state.Events);pauses=new HashSet<string>(state.PauseOwners);
            InitializeCaseBook(initial);
        }
        public ResidentState Resident(string id)=>residents[id];
        public Point3 NavigationPoint(string id)=>nodes[id].Position;
        public MansionDoorState Door(string id)=>doors[id];
        public MansionObjectState Object(string id)=>objects[id];
        public bool HasPause(string id)=>pauses.Contains(id);
        public void Pause(string id,bool acquire){if(acquire)pauses.Add(id);else pauses.Remove(id);state.PlayerVelocity=default;}
        public void Emit(string type,string actor,string target,string detail="")=>history.Add(new MansionEvent{Sequence=++state.Sequence,Tick=Tick,Type=type,Actor=actor,Target=target,Detail=detail});
        public string Plan(string actorId,string destination,string activity,int duration)
        {
            if(Paused||!residents.TryGetValue(actorId,out var actor)||!CanAct(actorId)||actor.Activity.StartsWith("Rescue:",StringComparison.Ordinal)||!nodes.ContainsKey(destination))return "Unavailable";
            if(actor.Phase=="Travelling"&&actor.Destination==destination)return "Accepted";
            if(residents.Values.Any(other=>other.Id!=actorId&&other.Present&&other.Alive&&other.Phase!="Idle"&&other.Destination==destination))return "활동 자리를 사용 중입니다.";
            // Smoothed routes may bypass many intermediate nodes without physically reaching them.
            // On a new request, finish the current segment instead of walking back to the last exact node.
            bool inSegment=actor.Phase=="Travelling"&&actor.PathCursor<actor.Path.Length;
            int cursor=inSegment?actor.PathCursor:0;
            string start=inSegment?actor.Path[cursor]:actor.Node;
            var path=FindPath(start,destination,actor.KnownLocked.Where(id=>!doors.TryGetValue(id,out var d)||d.OwnerId!=actorId).ToArray());
            if(path.Length==0)return "경로를 확인할 수 없습니다.";
            // Keep the incoming edge as well: a retarget must not bypass its door grant/lock checks.
            actor.Path=inSegment?actor.Path.Take(cursor).Concat(path).ToArray():path;actor.PathCursor=cursor;actor.Destination=destination;actor.Activity=activity;actor.ActivityTicks=Math.Max(60,duration);actor.Phase="Travelling";actor.StuckTicks=0;actor.QueueDoor="";actor.Recovering=false;actor.RecoveryTicks=0;
            Emit("ActivityRequested",actorId,destination,activity);return "Accepted";
        }
        public string[] FindPath(string from,string to,string[] knownLocked)
        {
            if(!nodes.ContainsKey(from)||!nodes.ContainsKey(to))return Array.Empty<string>();
            var previous=new Dictionary<string,string>();var cost=new Dictionary<string,double>{{from,0}};var open=new SortedSet<PathCandidate>(new PathComparer());open.Add(new PathCandidate{Id=from,Cost=0});
            while(open.Count>0){var c=open.Min;open.Remove(c);if(c.Cost>cost[c.Id])continue;if(c.Id==to)break;if(!outgoing.TryGetValue(c.Id,out var list))continue;
                foreach(var edge in list){if(!string.IsNullOrEmpty(edge.Door)&&knownLocked.Contains(edge.Door))continue;double next=c.Cost+nodes[c.Id].Position.Distance(nodes[edge.To].Position);if(cost.TryGetValue(edge.To,out double old)&&old<=next)continue;cost[edge.To]=next;previous[edge.To]=c.Id;open.Add(new PathCandidate{Id=edge.To,Cost=next});}}
            if(!cost.ContainsKey(to))return Array.Empty<string>();var path=new List<string>();string cursor=to;path.Add(cursor);while(cursor!=from){cursor=previous[cursor];path.Add(cursor);}path.Reverse();return path.ToArray();
        }
        struct PathCandidate {public string Id;public double Cost;}
        sealed class PathComparer:IComparer<PathCandidate>{public int Compare(PathCandidate a,PathCandidate b){int n=a.Cost.CompareTo(b.Cost);return n!=0?n:StringComparer.Ordinal.Compare(a.Id,b.Id);}}
        public string UseDoor(string id,string actor,bool toggleLock=false,bool emergency=false)
        {
            if(Paused||!PassageActorAvailable(actor)||!doors.TryGetValue(id,out var door))return "Unavailable";
            bool owner=door.OwnerId==actor;
            bool emergencyGranted=actor=="PRES_YUSTI"&&emergency&&door.EmergencyOpen;
            bool authorized=string.IsNullOrEmpty(door.OwnerId)||owner||emergencyGranted;
            if(toggleLock){
                if(!authorized)return "소유자의 허락이 필요합니다.";
                if(door.Holder!=""||door.Queue.Length>0||door.Open>.001)return "문을 닫고 통행이 끝난 뒤 잠글 수 있습니다.";
                door.Locked=!door.Locked;door.RelockPending=false;Emit("LockChanged",actor,id,door.Locked?"Locked":"Unlocked");
                if(!door.Locked&&residents.TryGetValue(actor,out var user))user.KnownLocked=user.KnownLocked.Where(x=>x!=id).ToArray();
                return door.Locked?"문을 잠갔습니다.":"잠금을 풀었습니다.";
            }
            // A permission is authority to operate a closed private door, not an invisible wall across an open doorway.
            if(!authorized&&door.Open<.999)return "소유자의 허락이 필요합니다.";
            if(door.Locked){
                if(!owner&&!emergencyGranted){Emit("DoorResistanceObserved",actor,id,"WouldNotOpen");return "잠겨 있습니다.";}
                door.Locked=false;Emit("LockChanged",actor,id,emergencyGranted?"Unlocked:ReportedEmergencyInspection":"Unlocked:OwnerPermission");
                if(residents.TryGetValue(actor,out var user))user.KnownLocked=user.KnownLocked.Where(x=>x!=id).ToArray();
            }
            if(!door.Queue.Contains(actor)&&door.Holder!=actor)door.Queue=door.Queue.Concat(new[]{actor}).ToArray();
            if(door.RelockAfterUse)door.RelockPending=true;
            return "문이 열립니다.";
        }
        public void Step(Point3 movement,bool running,IMansionPhysics physics)
        {
            if(Paused)return;CaseBook.Seal(state.Tick);state.Tick++;
            foreach(var door in doors.Values){
                var removed=door.Queue.Where(id=>!PassageActorAvailable(id)).ToArray();
                if(removed.Length>0){door.Queue=door.Queue.Where(PassageActorAvailable).ToArray();foreach(string id in removed)Emit("DoorQueueCancelled",id,door.Id,"ParticipantUnavailable");}
                if(door.Holder!=""){
                    bool unavailable=!PassageActorAvailable(door.Holder);
                    bool cleared=!unavailable&&Tick-door.GrantedTick>=MinimumPassageTicks&&(physics is IMansionPassagePhysics passage?passage.ActorClearedDoor(door.Id,door.Holder):physics.DoorClear(door.Id));
                    if(unavailable||cleared){string previous=door.Holder;door.Holder="";door.GrantedTick=0;Emit("DoorReleased",previous,door.Id,unavailable?"ParticipantUnavailable":"PassageCleared");}
                }
                if(door.Holder==""&&door.Queue.Length>0&&!door.Locked){door.Holder=door.Queue[0];door.Queue=door.Queue.Skip(1).ToArray();door.GrantedTick=Tick;Emit("DoorGranted",door.Holder,door.Id);}
                double wanted=door.Holder!=""?1:0;if(wanted<door.Open&&!physics.DoorClear(door.Id))wanted=1;
                door.Open=MoveTowards(door.Open,wanted,1d/30);physics.DoorPose(door.Id,door.Open);
                if(door.RelockAfterUse&&door.RelockPending&&!door.Locked&&door.Holder==""&&door.Queue.Length==0&&door.Open==0&&physics.DoorClear(door.Id)){
                    door.Locked=true;door.RelockPending=false;Emit("LockChanged",door.OwnerId,door.Id,"Locked:AfterPassage");
                }
            }
            foreach(var actor in residents.Values){if(!actor.Alive||!actor.Present)continue;
                if(!CanAct(actor.Id)){if(actor.Id=="CH_01")state.PlayerVelocity=default;continue;}
                if(actor.Id=="CH_01"){
                    if(actor.Activity.StartsWith("Rescue:",StringComparison.Ordinal)){state.PlayerVelocity=default;continue;}
                    state.PlayerVelocity=Locomotion.Velocity(state.PlayerVelocity,movement,running,1d/60,locomotion);
                    actor.Position=physics.Move(actor.Id,state.PlayerVelocity.Scale(1d/60));continue;
                }
                if(actor.Phase=="Performing"){if(--actor.ActivityTicks<=0){actor.Phase="Idle";actor.CompletedActivities++;actor.ScheduleCursor++;Emit("ActivityCompleted",actor.Id,actor.Destination,actor.Activity);}continue;}
                if(actor.Phase!="Travelling")continue;
                if(actor.PathCursor>=actor.Path.Length){
                    if(residents.Values.Any(other=>other.Id!=actor.Id&&other.Alive&&other.Present&&other.Phase=="Performing"&&other.Destination==actor.Destination))continue;
                    actor.Phase="Performing";Emit("ActivityStarted",actor.Id,actor.Destination,actor.Activity);continue;
                }
                string targetId=actor.Path[actor.PathCursor];var target=nodes[targetId].Position;
                if(physics is IMansionTrafficPhysics traffic&&UpcomingPassage(actor,out var passageDoor,out var approach,out var exit)&&(actor.Position.Distance(approach)<2.6||actor.QueueDoor==passageDoor.Id)){
                    if(passageDoor.Holder!=actor.Id){
                        // Request close to the physical approach, before everyone piles onto the same exact point.
                        // Private permission and lock observations still go through the real use attempt.
                        if(actor.Position.Distance(approach)<1.1&&!passageDoor.Queue.Contains(actor.Id)){
                            string request=UseDoor(passageDoor.Id,actor.Id);
                            if(request=="잠겨 있습니다."||request=="소유자의 허락이 필요합니다."){
                                actor.KnownLocked=actor.KnownLocked.Concat(new[]{passageDoor.Id}).Distinct().ToArray();actor.Phase="Idle";
                                Emit(request=="잠겨 있습니다."?"LockedDoorObserved":"DoorPermissionDenied",actor.Id,passageDoor.Id);continue;
                            }
                        }
                        if(passageDoor.Holder!=""||passageDoor.Queue.Length>0){
                            var beforeWait=actor.Position;actor.Position=traffic.WaitForPassage(actor.Id,passageDoor.Id,approach,exit);
                            var waited=actor.Position.Minus(beforeWait);if(waited.Distance(default)>.001)actor.Yaw=Math.Atan2(waited.X,waited.Z)*180/Math.PI;
                            actor.StuckTicks++;continue;
                        }
                    }
                }
                actor.QueueDoor="";
                if(actor.PathCursor>0&&outgoing.TryGetValue(actor.Path[actor.PathCursor-1],out var links)){
                    var edge=links.FirstOrDefault(e=>e.To==targetId);
                    if(edge!=null&&!string.IsNullOrEmpty(edge.Door)&&doors.TryGetValue(edge.Door,out var door)){
                        if(door.Holder!=actor.Id||door.Open<.999){
                            string result=UseDoor(door.Id,actor.Id);
                            if(result=="잠겨 있습니다."||result=="소유자의 허락이 필요합니다."){
                                // This edge begins at the physically reached portal approach. Planning never queried its hidden lock.
                                actor.KnownLocked=actor.KnownLocked.Concat(new[]{door.Id}).Distinct().ToArray();actor.Phase="Idle";
                                Emit(result=="잠겨 있습니다."?"LockedDoorObserved":"DoorPermissionDenied",actor.Id,door.Id);
                            }
                            continue;
                        }
                    }
                }
                var delta=target.Minus(actor.Position);double distance=delta.Distance(default);
                if(distance<.8&&actor.PathCursor+1<actor.Path.Length&&physics is IMansionWaypointPhysics steering){
                    string nextId=actor.Path[actor.PathCursor+1];
                    var nextEdge=outgoing.TryGetValue(targetId,out var choices)?choices.FirstOrDefault(e=>e.To==nextId):null;
                    var previousEdge=actor.PathCursor>0&&outgoing.TryGetValue(actor.Path[actor.PathCursor-1],out var entries)?entries.FirstOrDefault(e=>e.To==targetId):null;
                    if(nextEdge!=null&&string.IsNullOrEmpty(nextEdge.Door)&&(previousEdge==null||string.IsNullOrEmpty(previousEdge.Door))&&Math.Abs(target.Y-nodes[nextId].Position.Y)<.05&&steering.CanBypassIntermediate(actor.Id,nodes[nextId].Position)){
                        // Skip a routing point only. Position and last physically reached node never jump; final activity arrival remains exact.
                        actor.PathCursor++;continue;
                    }
                }
                if(distance<.14){actor.Node=targetId;actor.PathCursor++;actor.StuckTicks=0;continue;}
                var before=actor.Position;actor.Position=physics.Move(actor.Id,delta.Scale(Math.Min(1,1.4/60/distance)));
                var actual=actor.Position.Minus(before);if(actual.Distance(default)<.001)actor.StuckTicks++;else{actor.StuckTicks=0;actor.Yaw=Math.Atan2(actual.X,actual.Z)*180/Math.PI;}
            }
        }
        bool UpcomingPassage(ResidentState actor,out MansionDoorState door,out Point3 approach,out Point3 exit)
            =>UpcomingPassage(actor.Path,actor.PathCursor,actor.Position,out door,out approach,out exit);
        public bool UpcomingPassage(string[] path,int cursor,Point3 position,out MansionDoorState door,out Point3 approach,out Point3 exit)
        {
            door=null;approach=exit=default;if(path==null||cursor<0||cursor>=path.Length)return false;double distance=cursor==0?position.Distance(nodes[path[0]].Position):0;
            for(int i=Math.Max(1,cursor);i<path.Length;i++){
                var from=nodes[path[i-1]].Position;var to=nodes[path[i]].Position;
                if(distance>5)return false;
                var edge=outgoing.TryGetValue(path[i-1],out var links)?links.FirstOrDefault(e=>e.To==path[i]):null;
                if(edge==null||string.IsNullOrEmpty(edge.Door)){distance+=i==cursor?position.Distance(to):from.Distance(to);continue;}
                if(!doors.TryGetValue(edge.Door,out door))return false;
                approach=from;exit=to;
                // A passage has several consecutive edges; wait beside its first endpoint, not its centre.
                for(int p=i-1;p>0;p--){var earlier=outgoing.TryGetValue(path[p-1],out var before)?before.FirstOrDefault(e=>e.To==path[p]):null;if(earlier?.Door!=door.Id)break;approach=nodes[path[p-1]].Position;}
                for(int p=i+1;p<path.Length;p++){var later=outgoing.TryGetValue(path[p-1],out var after)?after.FirstOrDefault(e=>e.To==path[p]):null;if(later?.Door!=door.Id)break;exit=nodes[path[p]].Position;}
                return true;
            }
            return false;
        }
        static double MoveTowards(double a,double b,double max)=>Math.Abs(b-a)<=max?b:a+Math.Sign(b-a)*max;
        public string Pickup(string id,string actor)
        {
            if(Paused||!CanAct(actor)||!objects.TryGetValue(id,out var item)||item.Location!="World"||residents[actor].HeldObject!="")return "Unavailable";
            item.Location="Hand";item.Owner=actor;item.AnchorId="";item.LinearVelocity=default;item.AngularVelocity=default;item.ActiveContacts=Array.Empty<string>();foreach(var contact in item.PhysicalContacts??Array.Empty<MansionObjectContact>())if(contact.EndTick<0)contact.EndTick=Tick;residents[actor].HeldObject=id;Emit("ObjectPickedUp",actor,id);return "Committed";
        }
        public string Drop(string actor)
        {
            if(Paused||residents[actor].HeldObject=="")return "Unavailable";var item=objects[residents[actor].HeldObject];item.Location="World";item.Owner="";if(item.PhysicsVersion==1){item.LinearVelocity=actor=="CH_01"?state.PlayerVelocity:default;item.AngularVelocity=default;item.PhysicsSleeping=false;item.AnchorId="";}else item.Position=residents[actor].Position.Plus(new Point3(0,.15,0));residents[actor].HeldObject="";Emit(item.PhysicsVersion==1?"ObjectReleased":"ObjectPlaced",actor,item.Id);if(item.PhysicsVersion==1)item.ReleaseEventSequence=state.Sequence;return "Committed";
        }
        // Caller supplies the completed physical action; this is its atomic custody commit.
        public string PlaceAt(string actor,string itemId,string anchor,Point3 position,bool container)
        {
            if(Paused||!CanAct(actor)||string.IsNullOrWhiteSpace(anchor)||!position.Finite()||!residents.TryGetValue(actor,out var a)||a.HeldObject!=itemId||!objects.TryGetValue(itemId,out var item)||item.Location!="Hand"||item.Owner!=actor)return "Unavailable";
            a.HeldObject="";item.Owner="";item.Location=container?"Container":"World";item.AnchorId=anchor;item.Position=position;Emit(container?"ObjectStored":"ObjectPlacedAt",actor,itemId,anchor);return "Committed";
        }
        public string Retrieve(string actor,string itemId,string container)
        {
            if(Paused||!CanAct(actor)||!residents.TryGetValue(actor,out var a)||a.HeldObject!=""||!objects.TryGetValue(itemId,out var item)||item.Location!="Container"||item.AnchorId!=container)return "Unavailable";
            a.HeldObject=itemId;item.Location="Hand";item.Owner=actor;item.AnchorId="";Emit("ObjectRetrieved",actor,itemId,container);return "Committed";
        }
        public string HandOver(string itemId,string giver,string receiver)
        {
            if(Paused||giver==receiver||!CanAct(giver)||!CanAct(receiver)||!objects.TryGetValue(itemId,out var item)||!residents.TryGetValue(giver,out var from)||!residents.TryGetValue(receiver,out var to)||from.HeldObject!=itemId||item.Location!="Hand"||item.Owner!=giver||to.HeldObject!="")return "Unavailable";
            from.HeldObject="";to.HeldObject=itemId;item.Owner=receiver;item.Position=to.Position;
            Emit("ObjectHandedOver",giver,itemId,receiver);return "Committed";
        }
        public MansionState Capture()
        {
            state.Events=history.ToArray();state.PauseOwners=pauses.OrderBy(p=>p,StringComparer.Ordinal).ToArray();state.Cases=CaseBook.Capture();return state.Copy();
        }
        public static void Validate(MansionState s,IDictionary<string,MansionNode> nodes)
        {
            if(s==null||s.Magic!="BASSLINE_MANSION"||s.RuleVersion!="M01_LIFE_004"||!WorldTimeLabel.IsSupported(s.ClockVersion)||s.Tick<0||s.Sequence!=s.Events.LongLength||!s.PlayerVelocity.Finite()||s.PlayerVelocity.Distance(default)>5||double.IsNaN(s.PendingTime)||double.IsInfinity(s.PendingTime)||s.PendingTime<0||!new Point3(s.Yaw,s.Pitch,0).Finite()||Math.Abs(s.Pitch)>75)throw new ArgumentException("Invalid or unsupported mansion state");
            if(s.Residents.Length!=18||s.Residents.Select(a=>a.Id).Distinct().Count()!=18||!s.Residents.Any(a=>a.Id=="CH_01")||s.Doors.Select(d=>d.Id).Distinct().Count()!=s.Doors.Length||s.Objects.Select(o=>o.Id).Distinct().Count()!=s.Objects.Length)throw new ArgumentException("Registry mismatch");
            foreach(var actor in s.Residents)if(!actor.Position.Finite()||!actor.QueuePosition.Finite()||!actor.RecoveryPoint.Finite()||!actor.RecoveryTarget.Finite()||actor.RecoveryTicks<0||actor.RecoveryTicks>90||actor.QueueDoor==null||actor.QueueDoor!=""&&!s.Doors.Any(d=>d.Id==actor.QueueDoor)||!nodes.ContainsKey(actor.Node)||actor.Path.Any(p=>!nodes.ContainsKey(p))||actor.PathCursor<0||actor.PathCursor>actor.Path.Length||actor.KnownLocked.Any(d=>!s.Doors.Any(x=>x.Id==d)))throw new ArgumentException("Invalid actor path");
            bool PassageActorValid(string id)=>PassageActorIds.Contains(id)&&(id=="PRES_YUSTI"||s.Residents.Any(a=>a.Id==id&&a.Alive&&a.Present));
            foreach(var d in s.Doors)if(d.OwnerId==null||d.OwnerId!=""&&(!PassageActorIds.Contains(d.OwnerId)||d.OwnerId=="PRES_YUSTI"||!s.Residents.Any(a=>a.Id==d.OwnerId))||double.IsNaN(d.Open)||d.Open<0||d.Open>1||d.Queue==null||d.Holder==null||d.Queue.Distinct().Count()!=d.Queue.Length||d.Queue.Any(id=>!PassageActorValid(id))||d.Holder!=""&&(!PassageActorValid(d.Holder)||d.Queue.Contains(d.Holder)||d.GrantedTick<=0)||d.GrantedTick<0||d.GrantedTick>s.Tick)throw new ArgumentException("Invalid door queue or grant time");
            foreach(var item in s.Objects)if(!item.Position.Finite()||!new[]{"World","Hand","Container"}.Contains(item.Location)||item.Location=="Hand"&&!s.Residents.Any(a=>a.Id==item.Owner&&a.HeldObject==item.Id))throw new ArgumentException("Invalid object custody");
            foreach(var item in s.Objects.Where(o=>o.PhysicsVersion!=0)){
                if(item.PhysicsVersion!=1||!item.Rotation.Finite()||!item.LinearVelocity.Finite()||!item.AngularVelocity.Finite()||item.ReleaseEventSequence<0||item.ReleaseEventSequence>s.Sequence||item.ActiveContacts==null||item.PhysicalContacts==null||item.ActiveContacts.Distinct().Count()!=item.ActiveContacts.Length)throw new ArgumentException("Invalid object physics");
                foreach(var c in item.PhysicalContacts)if(c==null||string.IsNullOrEmpty(c.OtherId)||string.IsNullOrEmpty(c.ChainId)||!c.Position.Finite()||!c.Normal.Finite()||!c.RelativeVelocity.Finite()||c.BeginTick<0||c.BeginTick>s.Tick||c.EndTick< -1||c.EndTick>=0&&(c.EndTick<c.BeginTick||c.EndTick>s.Tick)||c.EventSequence<1||c.EventSequence>s.Sequence||c.CauseEventSequence<0||c.CauseEventSequence>c.EventSequence)throw new ArgumentException("Invalid physical contact history");
            }
            foreach(var actor in s.Residents)if(actor.HeldObject!=""&&!s.Objects.Any(o=>o.Id==actor.HeldObject&&o.Location=="Hand"&&o.Owner==actor.Id))throw new ArgumentException("Invalid held object");
            for(int i=0;i<s.Events.Length;i++)if(s.Events[i].Sequence!=i+1||s.Events[i].Tick>s.Tick)throw new ArgumentException("Invalid event history");
            ValidateCaseBook(s);
        }
    }
}
