using System;
namespace BASSLINE.Core
{
 [Serializable] public struct Point3
 {
  public double X,Y,Z;
  public Point3(double x,double y,double z){X=x;Y=y;Z=z;}
  public double Distance(Point3 b)=>Math.Sqrt((X-b.X)*(X-b.X)+(Y-b.Y)*(Y-b.Y)+(Z-b.Z)*(Z-b.Z));
  public Point3 Plus(Point3 b)=>new Point3(X+b.X,Y+b.Y,Z+b.Z);
  public Point3 Scale(double value)=>new Point3(X*value,Y*value,Z*value);
  public Point3 Minus(Point3 b)=>new Point3(X-b.X,Y-b.Y,Z-b.Z);
  public bool Finite()=>!double.IsNaN(X)&&!double.IsInfinity(X)&&!double.IsNaN(Y)&&!double.IsInfinity(Y)&&!double.IsNaN(Z)&&!double.IsInfinity(Z);
 }
 public interface IFixturePhysics
 {
  Point3 Move(string actorId,Point3 delta);
  bool ClearSight(string actorId,Point3 target);
  bool DoorClear(string doorId);
  void SetDoor(string doorId,double fraction);
 }
 [Serializable] public sealed class PersonalLifeView { public string ActorId;public int CompletedActivities;public bool Available; }
 public sealed class LifeRequest {public string ActivityId,AnchorId;}
 public sealed class PlayerLifeView {public long Tick;public int ClockVersion;public double Yaw,Pitch;public bool Paused,NotePause,SettingsPause;public string HeldItem,Activity,Message;}
 public interface IPlayerLifePort
 {
  PlayerLifeView ReadPlayer();
  void SetMove(double x,double z);
  void SetLook(double yaw,double pitch);
  string Interact(string stableTargetId);
  void Pause(string owner,bool acquire);
  string SaveSlot();
  string LoadSlot();
 }
}
