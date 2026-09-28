using System;
using System.Linq;
using System.Collections.Generic;
using BASSLINE.Core;
namespace BASSLINE.World.Fixture
{
 public sealed partial class LifeWorld
 {
  readonly Dictionary<string,LifeActor> actors=new Dictionary<string,LifeActor>(StringComparer.Ordinal);
  readonly Dictionary<string,LifeDoor> doors=new Dictionary<string,LifeDoor>(StringComparer.Ordinal);
  readonly Dictionary<string,LifeObject> objects=new Dictionary<string,LifeObject>(StringComparer.Ordinal);
  readonly List<SeatReservation> seats=new List<SeatReservation>();readonly List<TransferRecord> transfers=new List<TransferRecord>();
  readonly List<LifeEvent> events=new List<LifeEvent>();readonly Dictionary<string,LifeReceipt> receipts=new Dictionary<string,LifeReceipt>(StringComparer.Ordinal);
  PauseCoordinator pauses=new PauseCoordinator();long sequence,commandSequence;
  public long Tick {get;private set;} public bool Paused=>pauses.IsPaused(ClockScope.World);public bool Autonomous=true;public double PendingTime,PlayerYaw,PlayerPitch;
  public LifeWorld(IncidentSettings settings=null)
  {
   int i=0;foreach(string id in FixtureDefinition.Actors){actors.Add(id,new LifeActor{Id=id,Position=FixtureDefinition.P(-1.8+(i%4)*1.2,.3+(i/4)*1.2)});i++;}
   foreach(string id in new[]{"K_DOOR_N","K_DOOR_S"})doors.Add(id,new LifeDoor{Id=id});
   objects.Add("K_BOOK_01",new LifeObject{Id="K_BOOK_01",Name="공용 책",Position=new Point3(0,.9,-1.8)});
   objects.Add("K_CUP_01",new LifeObject{Id="K_CUP_01",Name="공용 컵",Position=new Point3(.65,.9,-1.8)});ConfigureIncident(settings);
  }
  public void UpdateFacing(string id,float bodyYaw,float headYaw,float headPitch){if(!new Point3(bodyYaw,headYaw,headPitch).Finite())throw new ArgumentException("Nonfinite actor facing");var a=actors[id];a.BodyYaw=bodyYaw;a.HeadYaw=headYaw;a.HeadPitch=headPitch;} public Point3 Pose(string id)=>actors[id].Position;
  public LifeActor Actor(string id)=>Copy(actors[id]);
  public PersonalLifeView Personal(string id)=>new PersonalLifeView{ActorId=id,CompletedActivities=actors[id].CompletedActivities,Available=!actors[id].Incapacitated&&actors[id].Phase=="Idle"&&!transfers.Any(t=>t.FromId==id||t.ToId==id)};
  public LifeDoor Door(string id)=>Copy(doors[id]);
  public LifeObject Object(string id)=>Copy(objects[id]);
  public void Pause(string owner,bool acquire){if(acquire)pauses.Acquire(new StableId(owner),ClockScope.Both);else pauses.Release(new StableId(owner));}
  public bool HasPause(string owner)=>pauses.Capture().Any(x=>x.Owner==owner);
  public string HeldName(string actorId)=>objects.Values.SingleOrDefault(x=>x.OwnerId==actorId&&x.Location=="Hand")?.Name??"없음";
  public string NextCommand()=>"K_CMD_"+(++commandSequence);
  void Emit(string type,string actor,string target,string detail=""){events.Add(new LifeEvent{Id="K_EV_"+(++sequence),Tick=Tick,Type=type,ActorId=actor,TargetId=target,Detail=detail});}
  public static Point3 DoorPosition(string id)=>FixtureDefinition.P(28,id=="K_DOOR_N"?3:-3);
  string Command(string id,string payload,Func<string> body)
  {
   _=new StableId(id);if(receipts.TryGetValue(id,out var old))return old.Payload==payload?old.Outcome:"CommandIdConflict";
   string outcome=Paused?"WorldPaused":body();receipts.Add(id,new LifeReceipt{CommandId=id,Payload=payload,Outcome=outcome});return outcome;
  }
  public string Schedule(string actorId,string activityId,string anchorId,string[] avoidDoors=null)
  {
   if(Paused)return "WorldPaused";
   if(!actors.TryGetValue(actorId,out var actor)||!FixtureDefinition.Activities.Any(x=>x.Id==activityId)||!FixtureDefinition.Anchors.Any(x=>x.Id==anchorId))return "Unavailable";
   if(actor.Incapacitated||avoidDoors!=null&&avoidDoors.Any(x=>!doors.ContainsKey(x)))return "Unavailable";if(actor.Phase!="Idle")return "Busy";actor.RouteAvoidDoors=avoidDoors==null?Array.Empty<string>():(string[])avoidDoors.Clone();actor.ActivityId=activityId;actor.AnchorId=anchorId;actor.Phase="WaitingSeat";Emit("ActivityRequested",actorId,anchorId,activityId);return "Accepted";
  }
  public Point3 PlayerVelocity;
  public readonly LocomotionSettings PlayerMotion=new LocomotionSettings();
  public void MovePlayer(Point3 input,IFixturePhysics physics,bool running=false)
  {
   if(Paused||actors["CH_01"].Incapacitated||actors["CH_01"].Phase!="Idle"||transfers.Any(t=>t.FromId=="CH_01"||t.ToId=="CH_01"))return;var direction=new Point3(input.X,0,input.Z);double length=direction.Distance(default);if(length>1)direction=direction.Scale(1/length);
   PlayerVelocity=Locomotion.Velocity(PlayerVelocity,direction,running,1d/60,PlayerMotion);
   actors["CH_01"].Position=physics.Move("CH_01",PlayerVelocity.Scale(1d/60));
  }
  public string Pickup(string commandId,string actorId,string objectId,IFixturePhysics physics)=>Command(commandId,"pickup|"+actorId+"|"+objectId,()=>{
   if(!actors.TryGetValue(actorId,out var actor)||!objects.TryGetValue(objectId,out var item)||actor.Incapacitated||item.Location!="Surface"||objects.Values.Any(x=>x.Location=="Hand"&&x.OwnerId==actorId)||actor.Position.Distance(item.Position)>1.8||!physics.ClearSight(actorId,item.Position))return "Unavailable";
   item.Location="Hand";item.OwnerId=actorId;item.AnchorId="";Emit("ObjectPickedUp",actorId,objectId);return "Committed";});
  public string Drop(string commandId,string actorId,IFixturePhysics physics)=>Command(commandId,"drop|"+actorId,()=>{
   if(!actors.TryGetValue(actorId,out var actor)||actor.Incapacitated)return "Unavailable";var item=objects.Values.SingleOrDefault(x=>x.OwnerId==actorId&&x.Location=="Hand");
   if(item==null||transfers.Any(x=>x.ObjectId==item.Id))return "Unavailable";
   var spot=new Point3(actor.Position.X,.15,actor.Position.Z);item.Location="Surface";item.OwnerId="";item.AnchorId="K_DROP_SURFACE";item.Position=spot;Emit("ObjectPlaced",actorId,item.Id);return "Committed";});
  public string BeginTransfer(string commandId,string from,string to,IFixturePhysics physics)=>Command(commandId,"transfer|"+from+"|"+to,()=>{
   if(!actors.ContainsKey(from)||!actors.ContainsKey(to)||from==to||actors[from].Incapacitated||actors[to].Incapacitated)return "Unavailable";
   var item=objects.Values.SingleOrDefault(x=>x.Location=="Hand"&&x.OwnerId==from);
   if(item==null||transfers.Any(x=>x.FromId==from||x.ToId==from||x.FromId==to||x.ToId==to)||objects.Values.Any(x=>x.Location=="Hand"&&x.OwnerId==to)||Pose(from).Distance(Pose(to))>1.6||!physics.ClearSight(from,Pose(to).Plus(new Point3(0,1,0))))return "Unavailable";
   transfers.Add(new TransferRecord{Id=commandId,ObjectId=item.Id,FromId=from,ToId=to});Emit("TransferStarted",from,item.Id,to);return "Pending";});
  public string SetDoorLock(string commandId,string actorId,string doorId,bool locked,IFixturePhysics physics)=>Command(commandId,"lock|"+actorId+"|"+doorId+"|"+locked,()=>{
   if(!actors.ContainsKey(actorId)||actors[actorId].Incapacitated||!doors.TryGetValue(doorId,out var door)||Pose(actorId).Distance(DoorPosition(doorId))>1.8||!physics.ClearSight(actorId,DoorPosition(doorId).Plus(new Point3(0,1,0)))||door.Holder!=""||door.Queue.Length>0||!physics.DoorClear(doorId))return "Unavailable";
   door.Locked=locked;if(locked)door.OpenFraction=0;physics.SetDoor(doorId,door.OpenFraction);Emit(locked?"DoorLocked":"DoorUnlocked",actorId,doorId);return "Committed";});
  public string OpenDoor(string commandId,string actorId,string doorId,IFixturePhysics physics)=>Command(commandId,"open|"+actorId+"|"+doorId,()=>{
   if(!actors.ContainsKey(actorId)||actors[actorId].Incapacitated||!doors.TryGetValue(doorId,out var door)||Pose(actorId).Distance(DoorPosition(doorId))>1.8||!physics.ClearSight(actorId,DoorPosition(doorId).Plus(new Point3(0,1,0)))||door.Locked)return "Unavailable";
   Enqueue(door,actorId);return "Accepted";});
  void Enqueue(LifeDoor door,string id){if(door.Holder==id||door.Queue.Any(x=>x.ActorId==id))return;door.Queue=door.Queue.Concat(new[]{new DoorWaiter{ActorId=id,Tick=Tick}}).ToArray();Emit("DoorQueued",id,door.Id);}
  public void Step(IFixturePhysics physics)
  {
   if(Paused)return;Tick++;
   foreach(var door in doors.Values){
    if(door.Holder!=""){
     var holder=actors[door.Holder];double distance=holder.Position.Distance(DoorPosition(door.Id));bool crossed=(holder.Position.Z-DoorPosition(door.Id).Z)*door.EntrySide<0;
     if(crossed&&distance>1.7||distance>4||holder.Phase=="Idle"&&holder.Id!="CH_01"&&physics.DoorClear(door.Id)){if(crossed && distance>1.7)holder.WaitingDoor="";Emit("DoorReleased",door.Holder,door.Id);door.Holder="";door.EntrySide=0;door.Aligned=false;door.IdleSince=Tick;}
    }
    if(!door.Locked&&door.Holder==""&&door.Queue.Length>0&&physics.DoorClear(door.Id)){door.Holder=door.Queue[0].ActorId;door.Aligned=false;door.EntrySide=Math.Sign(Pose(door.Holder).Z-DoorPosition(door.Id).Z);door.Queue=door.Queue.Skip(1).ToArray();Emit("DoorGranted",door.Holder,door.Id);}
    if(!door.Locked&&(door.Holder!=""||door.Queue.Length>0))door.OpenFraction=Math.Min(1,door.OpenFraction+1d/15);
    else if(door.Queue.Length==0&&Tick-door.IdleSince>120&&physics.DoorClear(door.Id))door.OpenFraction=Math.Max(0,door.OpenFraction-1d/15);
    physics.SetDoor(door.Id,door.OpenFraction);
   }
   foreach(var transfer in transfers.ToArray()){
    if(Pose(transfer.FromId).Distance(Pose(transfer.ToId))>1.6||!physics.ClearSight(transfer.FromId,Pose(transfer.ToId).Plus(new Point3(0,1,0)))){transfers.Remove(transfer);receipts[transfer.Id].Outcome="Cancelled";Emit("TransferCancelled",transfer.FromId,transfer.ObjectId);continue;}
    if(--transfer.RemainingTicks<=0){var item=objects[transfer.ObjectId];item.OwnerId=transfer.ToId;transfers.Remove(transfer);receipts[transfer.Id].Outcome="Committed";Emit("ObjectTransferred",transfer.FromId,transfer.ObjectId,transfer.ToId);}
   }
   foreach(var actor in actors.Values.OrderBy(x=>x.Id,StringComparer.Ordinal)){
    if(actor.Incapacitated)continue;if(transfers.Any(x=>x.FromId==actor.Id||x.ToId==actor.Id))continue;
    if(actor.Phase=="WaitingSeat"){
     if(seats.Any(x=>x.AnchorId==actor.AnchorId))continue;seats.Add(new SeatReservation{AnchorId=actor.AnchorId,ActorId=actor.Id});
     var anchor=FixtureDefinition.Anchors.Single(x=>x.Id==actor.AnchorId);var route=FixtureDefinition.Route(actor.Node,anchor.Node,actor.KnownBlockedDoors.Concat(actor.RouteAvoidDoors).Distinct().ToArray());if(route==null){Abort(actor);continue;}
     actor.PathEdges=route.Select(x=>x.Id).ToArray();actor.EdgeIndex=0;actor.Leg=Array.Empty<Point3>();actor.Phase="Travelling";actor.StuckTicks=0;Emit("SeatReserved",actor.Id,anchor.Id);
    }
    if(actor.Phase=="Travelling")Travel(actor,physics);
    else if(actor.Phase=="Performing"){
     if(--actor.ActivityRemaining<=0){Emit("ActivityCompleted",actor.Id,actor.AnchorId,actor.ActivityId);actor.CompletedActivities++;ReleaseSeat(actor);actor.Phase="Idle";actor.ActivityId="";actor.AnchorId="";}
    }
   }
  }
  void Travel(LifeActor actor,IFixturePhysics physics)
  {
   if(actor.EdgeIndex>=actor.PathEdges.Length){
    var anchor=FixtureDefinition.Anchors.Single(x=>x.Id==actor.AnchorId);if(Move(actor,anchor.Position,physics)){
     var reservation=seats.Single(x=>x.ActorId==actor.Id);reservation.Occupied=true;actor.Phase="Performing";actor.ActivityRemaining=FixtureDefinition.Activities.Single(x=>x.Id==actor.ActivityId).Ticks;Emit("ActivityStarted",actor.Id,anchor.Id,actor.ActivityId);
    }return;
   }
   var edge=FixtureDefinition.Edges.Single(x=>x.Id==actor.PathEdges[actor.EdgeIndex]);
   if(actor.Leg.Length==0){actor.Leg=(edge.A==actor.Node?edge.Points:edge.Points.Reverse()).ToArray();actor.PointIndex=actor.Position.Distance(actor.Leg[0])>.35?0:1;}
   if(edge.DoorId!=null&&(actor.WaitingDoor==edge.DoorId||actor.Position.Distance(DoorPosition(edge.DoorId))<1.6)){
    var door=doors[edge.DoorId];if(door.Locked){actor.KnownBlockedDoors=actor.KnownBlockedDoors.Concat(new[]{door.Id}).Distinct().ToArray();Emit("DoorObservedLocked",actor.Id,door.Id);actor.WaitingDoor="";ReleaseSeat(actor);actor.Phase="WaitingSeat";actor.Leg=Array.Empty<Point3>();return;}
    actor.WaitingDoor=door.Id;Enqueue(door,actor.Id);if(door.Holder!=actor.Id){YieldAtDoor(actor,door,physics);return;}if(door.OpenFraction<.999)return;
    var portal=DoorPosition(door.Id);double sideDistance=(actor.Position.Z-portal.Z)*door.EntrySide;
    if(!door.Aligned&&sideDistance>0){var approach=portal.Plus(new Point3(0,0,door.EntrySide*1.15));if(!Move(actor,approach,physics,.12))return;door.Aligned=true;}
   }
   if(Move(actor,actor.Leg[actor.PointIndex],physics,.35)){
    actor.PointIndex++;if(actor.PointIndex==actor.Leg.Length){actor.Node=edge.A==actor.Node?edge.B:edge.A;actor.EdgeIndex++;actor.Leg=Array.Empty<Point3>();actor.WaitingDoor="";}
   }
  }
  void YieldAtDoor(LifeActor actor,LifeDoor door,IFixturePhysics physics)
  {
   bool inside=actor.Node=="K_W";int index=Array.FindIndex(door.Queue,x=>x.ActorId==actor.Id);int ahead=door.Queue.Take(Math.Max(index,0)).Count(x=>(actors[x.ActorId].Node=="K_W")==inside);
   double outsideSign=door.Id=="K_DOOR_N"?1:-1;var centre=DoorPosition(door.Id);
   var waiting=new Point3(centre.X+(inside?-1.2:.63),0,centre.Z+outsideSign*(inside?-1.4-ahead*.7:1.4+ahead*.7));
   Move(actor,waiting,physics,.1);
  }
  bool Move(LifeActor actor,Point3 target,IFixturePhysics physics,double arrivalRadius=.08)
  {
   var delta=target.Minus(actor.Position);delta.Y=0;double distance=delta.Distance(default);if(distance<arrivalRadius)return true;
   var old=actor.Position;actor.Position=physics.Move(actor.Id,delta.Scale(Math.Min(1,FixtureDefinition.WalkSpeed/60/distance)));
   var remaining=target.Minus(actor.Position);remaining.Y=0;if(remaining.Distance(default)>=distance-.0005)actor.StuckTicks++;else actor.StuckTicks=0;
   if(actor.StuckTicks>600){Emit("PathBlocked",actor.Id,actor.AnchorId);Abort(actor);}return false;
  }
  void ReleaseSeat(LifeActor actor){seats.RemoveAll(x=>x.ActorId==actor.Id);}
  void Abort(LifeActor actor){ReleaseSeat(actor);foreach(var door in doors.Values)door.Queue=door.Queue.Where(x=>x.ActorId!=actor.Id).ToArray();actor.Phase="Idle";actor.WaitingDoor="";actor.Leg=Array.Empty<Point3>();}
  public LifeSnapshot Capture()=>new LifeSnapshot{Incident=incident.Copy(),Tick=Tick,Sequence=sequence,CommandSequence=commandSequence,PendingTime=PendingTime,PlayerVelocity=PlayerVelocity,PlayerYaw=PlayerYaw,PlayerPitch=PlayerPitch,Autonomous=Autonomous,Actors=actors.Values.OrderBy(x=>x.Id,StringComparer.Ordinal).Select(Copy).ToArray(),Doors=doors.Values.OrderBy(x=>x.Id,StringComparer.Ordinal).Select(Copy).ToArray(),Objects=objects.Values.OrderBy(x=>x.Id,StringComparer.Ordinal).Select(Copy).ToArray(),Seats=seats.Select(x=>new SeatReservation{ActorId=x.ActorId,AnchorId=x.AnchorId,Occupied=x.Occupied}).ToArray(),Transfers=transfers.Select(x=>new TransferRecord{Id=x.Id,FromId=x.FromId,ToId=x.ToId,ObjectId=x.ObjectId,RemainingTicks=x.RemainingTicks}).ToArray(),Events=events.Select(x=>new LifeEvent{Id=x.Id,Tick=x.Tick,Type=x.Type,ActorId=x.ActorId,TargetId=x.TargetId,Detail=x.Detail}).ToArray(),Receipts=receipts.Values.OrderBy(x=>x.CommandId,StringComparer.Ordinal).Select(x=>new LifeReceipt{CommandId=x.CommandId,Payload=x.Payload,Outcome=x.Outcome}).ToArray(),Pauses=pauses.Capture()};
  static LifeActor Copy(LifeActor x)=>new LifeActor{Id=x.Id,BodyYaw=x.BodyYaw,HeadYaw=x.HeadYaw,HeadPitch=x.HeadPitch,Incapacitated=x.Incapacitated,RouteAvoidDoors=(string[])(x.RouteAvoidDoors??Array.Empty<string>()).Clone(),Node=x.Node,Phase=x.Phase,ActivityId=x.ActivityId,AnchorId=x.AnchorId,WaitingDoor=x.WaitingDoor,Position=x.Position,PathEdges=(string[])x.PathEdges.Clone(),EdgeIndex=x.EdgeIndex,PointIndex=x.PointIndex,CompletedActivities=x.CompletedActivities,ActivityRemaining=x.ActivityRemaining,StuckTicks=x.StuckTicks,KnownBlockedDoors=(string[])x.KnownBlockedDoors.Clone(),Leg=(Point3[])x.Leg.Clone()};
  static LifeDoor Copy(LifeDoor x)=>new LifeDoor{Id=x.Id,Locked=x.Locked,OpenFraction=x.OpenFraction,Aligned=x.Aligned,Holder=x.Holder,EntrySide=x.EntrySide,IdleSince=x.IdleSince,Queue=x.Queue.Select(y=>new DoorWaiter{ActorId=y.ActorId,Tick=y.Tick}).ToArray()};
  static LifeObject Copy(LifeObject x)=>new LifeObject{Id=x.Id,Name=x.Name,Location=x.Location,OwnerId=x.OwnerId,AnchorId=x.AnchorId,Position=x.Position};
  public static LifeWorld Restore(LifeSnapshot s)
  {
   Validate(s);var w=new LifeWorld();w.actors.Clear();w.doors.Clear();w.objects.Clear();foreach(var a in s.Actors)w.actors.Add(a.Id,Copy(a));foreach(var d in s.Doors)w.doors.Add(d.Id,Copy(d));foreach(var o in s.Objects)w.objects.Add(o.Id,Copy(o));
   w.seats.AddRange(s.Seats.Select(x=>new SeatReservation{AnchorId=x.AnchorId,ActorId=x.ActorId,Occupied=x.Occupied}));w.transfers.AddRange(s.Transfers.Select(x=>new TransferRecord{Id=x.Id,ObjectId=x.ObjectId,FromId=x.FromId,ToId=x.ToId,RemainingTicks=x.RemainingTicks}));
   w.events.AddRange(s.Events.Select(x=>new LifeEvent{Id=x.Id,Tick=x.Tick,Type=x.Type,ActorId=x.ActorId,TargetId=x.TargetId,Detail=x.Detail}));foreach(var r in s.Receipts)w.receipts.Add(r.CommandId,new LifeReceipt{CommandId=r.CommandId,Payload=r.Payload,Outcome=r.Outcome});
   w.incident=s.Incident?.Copy()??new IncidentSnapshot();w.Tick=s.Tick;w.sequence=s.Sequence;w.commandSequence=s.CommandSequence;w.PendingTime=s.PendingTime;w.PlayerVelocity=s.PlayerVelocity;w.PlayerYaw=s.PlayerYaw;w.PlayerPitch=s.PlayerPitch;w.Autonomous=s.Autonomous;w.pauses=PauseCoordinator.Restore(s.Pauses);ValidateIncident(s);return w;
  }
  public static void Validate(LifeSnapshot s)
  {
   if(s==null||s.Magic!="BASSLINE_FIXTURE_SAVE"||s.Schema!=1||s.Map!=FixtureDefinition.MapVersion||s.Rules!=FixtureDefinition.RuleVersion||!s.PlayerVelocity.Finite()||s.PlayerVelocity.Distance(default)>5||s.Tick<0||s.CommandSequence<0||double.IsNaN(s.PendingTime)||double.IsInfinity(s.PendingTime)||s.PendingTime<0||!new Point3(s.PlayerYaw,s.PlayerPitch,0).Finite()||Math.Abs(s.PlayerPitch)>75)throw new ArgumentException("Unsupported fixture snapshot");
   if(s.Actors==null||!s.Actors.Select(x=>x.Id).OrderBy(x=>x).SequenceEqual(FixtureDefinition.Actors.OrderBy(x=>x))||s.Doors==null||!s.Doors.Select(x=>x.Id).OrderBy(x=>x).SequenceEqual(new[]{"K_DOOR_N","K_DOOR_S"}))throw new ArgumentException("Fixture registry mismatch");
   var actorIds=new HashSet<string>(s.Actors.Select(x=>x.Id));var cells=FixtureDefinition.WalkCells();
   foreach(var a in s.Actors){
    if(!new Point3(a.BodyYaw,a.HeadYaw,a.HeadPitch).Finite()||!a.Position.Finite()||!cells.Contains((int)Math.Floor(a.Position.X)+","+(int)Math.Floor(a.Position.Z))||!FixtureDefinition.Nodes.ContainsKey(a.Node)||!new[]{"Idle","WaitingSeat","Travelling","Performing"}.Contains(a.Phase)||a.CompletedActivities<0||a.ActivityRemaining<0||a.PathEdges.Any(x=>!FixtureDefinition.Edges.Any(e=>e.Id==x))||a.EdgeIndex<0||a.EdgeIndex>a.PathEdges.Length||a.PointIndex<0||a.Leg.Any(p=>!p.Finite())||a.Leg.Length>0&&a.PointIndex>=a.Leg.Length||a.KnownBlockedDoors.Any(x=>!s.Doors.Any(d=>d.Id==x))||(a.RouteAvoidDoors??Array.Empty<string>()).Any(x=>!s.Doors.Any(d=>d.Id==x)))throw new ArgumentException("Invalid actor/path");
    if(a.Phase!="Idle"&&(!FixtureDefinition.Activities.Any(x=>x.Id==a.ActivityId)||!FixtureDefinition.Anchors.Any(x=>x.Id==a.AnchorId)))throw new ArgumentException("Activity FK invalid");
   }
   foreach(var d in s.Doors)if(double.IsNaN(d.OpenFraction)||d.OpenFraction<0||d.OpenFraction>1||d.Holder!=""&&!actorIds.Contains(d.Holder)||d.Queue.Select(x=>x.ActorId).Distinct().Count()!=d.Queue.Length||d.Queue.Any(x=>!actorIds.Contains(x.ActorId)||x.Tick>s.Tick||x.ActorId==d.Holder))throw new ArgumentException("Invalid door queue");
   if(s.Seats.Select(x=>x.AnchorId).Distinct().Count()!=s.Seats.Length||s.Seats.Select(x=>x.ActorId).Distinct().Count()!=s.Seats.Length||s.Seats.Any(x=>!actorIds.Contains(x.ActorId)||!FixtureDefinition.Anchors.Any(a=>a.Id==x.AnchorId)||s.Actors.Single(a=>a.Id==x.ActorId).AnchorId!=x.AnchorId))throw new ArgumentException("Seat ownership invalid");
   if(s.Objects==null||!s.Objects.Select(x=>x.Id).OrderBy(x=>x).SequenceEqual(s.Incident?.Enabled!=true?new[]{"K_BOOK_01","K_CUP_01"}:new[]{"K_BOOK_01","K_CUP_01","K_O31"})||s.Objects.Any(x=>!x.Position.Finite()||!new[]{"Surface","Hand"}.Contains(x.Location)||(x.Location=="Hand"?!actorIds.Contains(x.OwnerId)||x.AnchorId!="":x.OwnerId!=""))||s.Objects.Where(x=>x.Location=="Hand").GroupBy(x=>x.OwnerId).Any(x=>x.Count()>1))throw new ArgumentException("Object conservation invalid");
   if(s.Transfers.Select(x=>x.ObjectId).Distinct().Count()!=s.Transfers.Length||s.Transfers.Any(x=>!actorIds.Contains(x.FromId)||!actorIds.Contains(x.ToId)||x.FromId==x.ToId||x.RemainingTicks<=0||x.RemainingTicks>60||!s.Objects.Any(o=>o.Id==x.ObjectId&&o.Location=="Hand"&&o.OwnerId==x.FromId)||!s.Receipts.Any(r=>r.CommandId==x.Id&&r.Outcome=="Pending")))throw new ArgumentException("Invalid transfer");
   if(s.Events==null||s.Sequence!=s.Events.LongLength)throw new ArgumentException("Invalid event cursor");long i=0,lastTick=-1;foreach(var e in s.Events){if(e.Id!="K_EV_"+(++i)||e.Tick<lastTick||e.Tick>s.Tick||!actorIds.Contains(e.ActorId))throw new ArgumentException("Invalid event journal");lastTick=e.Tick;}
   if(s.Receipts.Select(x=>x.CommandId).Distinct().Count()!=s.Receipts.Length)throw new ArgumentException("Duplicate receipt");foreach(var r in s.Receipts)_=new StableId(r.CommandId);PauseCoordinator.Restore(s.Pauses);ValidateIncident(s);
  }
 }
}





