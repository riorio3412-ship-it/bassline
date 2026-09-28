using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.World.Fixture;
using BASSLINE.AuthoringData;
using BASSLINE.Save;
using BASSLINE.NPC;
namespace BASSLINE.Bootstrap
{
 public sealed partial class FixtureRuntime:MonoBehaviour,IPlayerLifePort,IFixturePhysics,IPlayerNotebookPort,IPlayerMotionPort
 {
  public FixtureActorBody[] Bodies;public FixtureDoorBody[] DoorBodies;public FixtureObjectBody[] ObjectBodies;
  public bool AutomaticTick=true;
  public LifeWorld World {get;private set;}
  Dictionary<string,FixtureActorBody> actors;Dictionary<string,FixtureDoorBody> doors;
  readonly LifePlanner planner=new LifePlanner();Point3 move;bool running;string message="";
  SimulationMode previousSimulationMode;
  void OnEnable(){previousSimulationMode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;}
  void OnDisable(){Physics.simulationMode=previousSimulationMode;}
  public static Vector3 V(Point3 p)=>new Vector3((float)p.X,(float)p.Y,(float)p.Z);
  public static Point3 P(Vector3 p)=>new Point3(p.x,p.y,p.z);
  void Awake(){actors=Bodies.ToDictionary(x=>x.ActorId);doors=DoorBodies.ToDictionary(x=>x.DoorId);World=new LifeWorld(IncidentDefinition?IncidentDefinition.Settings:null);InitializeKnowledge();ApplyPoses();}
  void Update(){if(!AutomaticTick||World==null||World.Paused)return;World.PendingTime+=Time.unscaledDeltaTime;int count=0;while(World.PendingTime>=1d/60&&count++<12){World.PendingTime-=1d/60;AdvanceOne();}Present();}
  public void AdvanceOne()
  {
   if(World.Paused)return;
   if(World.IsIncidentFixture)IncidentPlanning();else if(World.Autonomous)foreach(var id in FixtureDefinition.Actors.Where(x=>x!="CH_01")){if(TryKnownAppointment(id))continue;var next=planner.Choose(World.Personal(id));if(next!=null)World.Schedule(id,next.ActivityId,next.AnchorId);}
   // One authoritative tick must also advance PhysX, even when several ticks share a rendered frame.
   // Otherwise CharacterController query shapes can lag behind their visible transforms.
   Physics.SyncTransforms();World.Step(this);World.MovePlayer(move,this,running);Present();Physics.SyncTransforms();Physics.Simulate(1f/60);CaptureFacing();ProcessKnowledge();if(World.IsIncidentFixture){World.AdvanceIncident(this,this,Knowledge.For("CH_04"));ProcessIncidentKnowledge();PresentIncident();}AdvanceInspection();
  }
  public void SetRun(bool value){running=value&&!World.Paused;}
  public void SetMove(double x,double z){move=new Point3(x,0,z);}
  public void SetLook(double yaw,double pitch){if(World.Paused||!new Point3(yaw,pitch,0).Finite())return;World.PlayerYaw=yaw%360;World.PlayerPitch=Math.Max(-75,Math.Min(75,pitch));ApplyLook();}
  void ApplyLook(){var player=actors["CH_01"];player.transform.rotation=Quaternion.Euler(0,(float)World.PlayerYaw,0);var camera=player.GetComponentInChildren<Camera>();if(camera)camera.transform.localRotation=Quaternion.Euler((float)World.PlayerPitch,0,0);}
  public PlayerLifeView ReadPlayer(){var a=World.Actor("CH_01");return new PlayerLifeView{Tick=World.Tick,Yaw=World.PlayerYaw,Pitch=World.PlayerPitch,Paused=World.Paused,NotePause=World.HasPause("K_NOTE"),SettingsPause=World.HasPause("K_SETTINGS"),HeldItem=World.HeldName("CH_01"),Activity=a.Phase,Message=message};}
  public void Pause(string owner,bool acquire){if(World.HasPause(owner)!=acquire)World.Pause(owner,acquire);move=default;running=false;World.PlayerVelocity=default;}
  public string Interact(string id)
  {
   string command=World.NextCommand();
   if(id=="DROP")message=World.Drop(command,"CH_01",this);
   else if(id.StartsWith("LOCK:",StringComparison.Ordinal)){string door=id.Substring(5);if(!doors.ContainsKey(door))return "Unavailable";message=World.SetDoorLock(command,"CH_01",door,!World.Door(door).Locked,this);}
   else if(doors.ContainsKey(id))message=World.OpenDoor(command,"CH_01",id,this);
   else if(CaseReaders.Any(x=>x.StableId==id))message=CanInspectCase(id)?"Inspect":"Unavailable";else if(ObjectBodies.Any(x=>x.ObjectId==id))message=World.Pickup(command,"CH_01",id,this);
   else if(actors.ContainsKey(id)&&World.Actor(id).Incapacitated)message=CanInspectBody(id)?"Inspect":"Unavailable";else if(actors.ContainsKey(id)&&id!="CH_01")message=World.HeldName("CH_01")!="없음"?World.BeginTransfer(command,"CH_01",id,this):Talk(id);
   else if(FixtureDefinition.Anchors.Any(x=>x.Id==id))message=World.Pose("CH_01").Distance(FixtureDefinition.Anchors.Single(x=>x.Id==id).Position)<1.8?World.Schedule("CH_01","ACT_REST",id):"Unavailable";
   else message="Unavailable";return message;
  }
  SessionSaveStore Store()=>new SessionSaveStore(x=>JsonUtility.ToJson(x),x=>JsonUtility.FromJson<FixtureSessionSnapshot>(x));
  public string SavePath=>Path.Combine(Application.persistentDataPath,"TestOnly",World.IsIncidentFixture?"FixtureK_Incident":"FixtureK","session-slot-v1.dat");
  public string SaveSlot(){try{SaveTo(SavePath);return message="저장 완료";}catch(Exception e){return message="저장 실패: "+e.Message;}}
  public string LoadSlot(){try{LoadFrom(SavePath);return message="불러오기 완료";}catch(Exception e){return message="불러오기 실패: "+e.Message;}}
  public void SaveTo(string path)=>Store().Save(path,CaptureSession());
  public void LoadFrom(string path){var loaded=Store().Load(path);if((loaded.World.Incident?.Enabled==true)!=World.IsIncidentFixture)throw new InvalidDataException("Save belongs to another TestOnly fixture");ValidatePlacement(loaded.World);RestoreSession(loaded);move=default;ApplyPoses();}
  public void LoadTestSnapshot(LifeSnapshot snapshot){LifeWorld.Validate(snapshot);ValidatePlacement(snapshot);World=LifeWorld.Restore(snapshot);InitializeKnowledge();move=default;ApplyPoses();}
  void ValidatePlacement(LifeSnapshot snapshot)
  {
   for(int i=0;i<snapshot.Actors.Length;i++)for(int j=i+1;j<snapshot.Actors.Length;j++)if(snapshot.Actors[i].Position.Distance(snapshot.Actors[j].Position)<.56)throw new InvalidDataException("Overlapping loaded actors");
   foreach(var a in snapshot.Actors){Vector3 p=V(a.Position);float h=actors[a.Id].Height;
    foreach(var d in snapshot.Doors){var centre=V(LifeWorld.DoorPosition(d.Id))+Vector3.right*(float)(d.OpenFraction*1.4);if(Mathf.Abs(p.x-centre.x)<.86f&&Mathf.Abs(p.z-centre.z)<.34f)throw new InvalidDataException("Loaded pose intersects door leaf: "+a.Id);}
    foreach(var collider in Physics.OverlapCapsule(p+Vector3.up*.32f,p+Vector3.up*(h-.32f),.27f))if(!collider.isTrigger&&!collider.GetComponentInParent<FixtureActorBody>()&&!collider.GetComponentInParent<FixtureDoorBody>()&&!collider.GetComponentInParent<FixtureObjectBody>()&&!(collider.GetComponentInParent<FixtureCaseReader>()?.Kind=="ContactTrace"))throw new InvalidDataException("Loaded pose intersects static geometry: "+a.Id);
   }
  }
  void ApplyPoses(){foreach(var a in Bodies)a.Capsule.enabled=false;foreach(var a in Bodies){var state=World.Actor(a.ActorId);a.transform.position=V(state.Position);a.transform.rotation=Quaternion.Euler(0,state.BodyYaw,0);a.Head.rotation=Quaternion.Euler(state.HeadPitch,state.HeadYaw,0);}foreach(var d in DoorBodies)SetDoor(d.DoorId,World.Door(d.DoorId).OpenFraction);foreach(var a in Bodies)a.Capsule.enabled=true;ApplyLook();Physics.SyncTransforms();Present();}
  void CaptureFacing(){foreach(var b in Bodies.Where(x=>x.ActorId!="CH_01")){var s=World.Actor(b.ActorId);float body=Quaternion.Angle(b.transform.rotation,Quaternion.Euler(0,s.BodyYaw,0))<.001f?s.BodyYaw:b.transform.eulerAngles.y;bool same=Quaternion.Angle(b.Head.rotation,Quaternion.Euler(s.HeadPitch,s.HeadYaw,0))<.001f;World.UpdateFacing(b.ActorId,body,same?s.HeadYaw:b.Head.eulerAngles.y,same?s.HeadPitch:b.Head.eulerAngles.x);}}
  public Point3 Move(string actorId,Point3 delta)
  {
   var body=actors[actorId];var old=body.transform.position;var motion=V(delta);
   foreach(var id in doors.Keys){var door=World.Door(id);var next=old+motion;var position=V(LifeWorld.DoorPosition(id));if(Mathf.Abs(next.x-position.x)<.85f&&Mathf.Abs(next.z-position.z)<.52f&&(door.Holder!=actorId||door.OpenFraction<.999))return P(old);}
   if(motion.sqrMagnitude>.000001f){
    var direction=motion.normalized;
    bool personAhead=Physics.SphereCastAll(old+Vector3.up*.45f,.27f,direction,.4f).Any(hit=>{var other=hit.collider.GetComponentInParent<FixtureActorBody>();return other&&other!=body&&Vector3.Dot(other.transform.position-old,direction)>.05f;});
    bool portalTraffic=World.Actor(actorId).WaitingDoor!=""||doors.Keys.Any(id=>World.Door(id).Holder==actorId);
    if(personAhead&&actorId!="CH_01"&&!portalTraffic){
     var right=Vector3.Cross(Vector3.up,direction);
     foreach(var candidate in new[]{(direction-right).normalized,(direction+right).normalized,-right,right}){
      bool blocked=Physics.SphereCastAll(old+Vector3.up*.45f,.27f,candidate,.35f,~0,QueryTriggerInteraction.Ignore).Any(hit=>{var other=hit.collider.GetComponentInParent<FixtureActorBody>();if(other==body)return false;if(other&&Vector3.Dot(other.transform.position-old,candidate)<0)return false;return true;});
      if(!blocked){motion=candidate*motion.magnitude;break;}
     }
    }
    if(body.Head)body.Head.rotation=Quaternion.LookRotation(direction);
   }
   body.Capsule.Move(motion+Vector3.down*(2f/60));return P(body.transform.position);
  }
  public bool ClearSight(string actorId,Point3 target)
  {
   Vector3 origin=actors[actorId].transform.position+Vector3.up*(actors[actorId].Height*.88f),delta=V(target)-origin;
   return !Physics.RaycastAll(origin,delta.normalized,Mathf.Max(0,delta.magnitude-.1f),~0,QueryTriggerInteraction.Ignore).Any(hit=>{
    var actor=hit.collider.GetComponentInParent<FixtureActorBody>();if(actor&&actor.ActorId==actorId)return false;
    return !hit.collider.bounds.Contains(V(target));
   });
  }
  public bool DoorClear(string doorId){var point=V(LifeWorld.DoorPosition(doorId));return !Physics.OverlapBox(point+Vector3.up, new Vector3(.82f,1,.25f)).Any(x=>x.GetComponentInParent<FixtureActorBody>());}
  public void SetDoor(string doorId,double fraction){var door=doors[doorId];door.Leaf.localPosition=door.ClosedLocalPosition+Vector3.right*(float)(fraction*1.4);Physics.SyncTransforms();}
  void Present()
  {
   PresentIncident();foreach(var binding in ObjectBodies){var item=World.Object(binding.ObjectId);binding.Collider.enabled=item.Location=="Surface";
    if(item.Location=="Hand"){var hand=actors[item.OwnerId].RightHand;binding.transform.position=hand.position;binding.transform.rotation=hand.rotation;}else{binding.transform.position=V(item.Position);binding.transform.rotation=Quaternion.identity;}}
  }
 }
}








