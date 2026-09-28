using System;
using BASSLINE.Core;
namespace BASSLINE.World.Fixture
{
 [Serializable] public sealed class LifeActor
 {
  public string Id,Node="K_H",Phase="Idle",ActivityId="",AnchorId="",WaitingDoor="";
  public Point3 Position;public string[] PathEdges=Array.Empty<string>();public int EdgeIndex,PointIndex=1,CompletedActivities,ActivityRemaining,StuckTicks;
  public float BodyYaw,HeadYaw,HeadPitch;public bool Incapacitated;public string[] RouteAvoidDoors=Array.Empty<string>();public string[] KnownBlockedDoors=Array.Empty<string>();public Point3[] Leg=Array.Empty<Point3>();
 }
 [Serializable] public sealed class DoorWaiter{public string ActorId;public long Tick;}
 [Serializable] public sealed class LifeDoor{public string Id;public bool Locked,Aligned;public double OpenFraction;public long IdleSince;public int EntrySide;public string Holder="";public DoorWaiter[] Queue=Array.Empty<DoorWaiter>();}
 [Serializable] public sealed class SeatReservation{public string AnchorId,ActorId;public bool Occupied;}
 [Serializable] public sealed class LifeObject{public string Id,Name,Location="Surface",OwnerId="",AnchorId="K_TABLE_H";public Point3 Position;}
 [Serializable] public sealed class TransferRecord{public string Id,ObjectId,FromId,ToId;public int RemainingTicks=60;}
 [Serializable] public sealed class LifeEvent{public string Id,Type,ActorId,TargetId,Detail;public long Tick;}
 [Serializable] public sealed class LifeReceipt{public string CommandId,Payload,Outcome;}
 [Serializable] public sealed class LifeSnapshot
 {
  public string Magic="BASSLINE_FIXTURE_SAVE",Map=FixtureDefinition.MapVersion,Rules=FixtureDefinition.RuleVersion;
  public IncidentSnapshot Incident=new IncidentSnapshot();public int Schema=1;public long Tick,Sequence,CommandSequence;
  public double PendingTime,PlayerYaw,PlayerPitch;public bool Autonomous=true;public Point3 PlayerVelocity;
  public LifeActor[] Actors;public LifeDoor[] Doors;public SeatReservation[] Seats;
  public LifeObject[] Objects;public TransferRecord[] Transfers;public LifeEvent[] Events;public LifeReceipt[] Receipts;public PauseRecord[] Pauses;
 }
}


