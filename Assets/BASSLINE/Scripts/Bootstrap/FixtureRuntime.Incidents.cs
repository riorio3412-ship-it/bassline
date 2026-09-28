using System;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.World.Fixture;
namespace BASSLINE.Bootstrap
{
 public sealed partial class FixtureRuntime:IIncidentPhysics
 {
  public FixtureIncidentDefinition IncidentDefinition;
  public FixtureCaseReader[] CaseReaders=Array.Empty<FixtureCaseReader>();
  void IncidentPlanning()
  {
   foreach(var id in FixtureDefinition.Actors.Where(x=>x!="CH_01"))TryKnownAppointment(id);
   World.RequestIncidentApproach(Social.Due("CH_04",World.Tick)!=null);
   var c=World.ReadIncidentForSystem();
   // Authored workbench facing, not a look-at lookup of an unseen target.
   if(new[]{"Approaching","Ready","Contact","CauseCommitted"}.Contains(c.Stage)&&World.Personal("CH_04").Available&&World.Pose("CH_04").Distance(FixtureDefinition.Anchors.Single(x=>x.Id==c.Settings.Approach).Position)<.4){
    var body=actors["CH_04"];body.transform.rotation=Quaternion.Euler(0,c.Stage=="Approaching"?160:90,0);body.Head.localRotation=Quaternion.Euler(c.Stage=="Approaching"?45:0,0,0);
   }
  }
  public bool HasContact(string actor,string target,string item,out Point3 point)
  {
   point=default;
   if(!actors.ContainsKey(actor)||!actors.ContainsKey(target)||World.Actor(actor).Incapacitated||World.Actor(target).Incapacitated||!World.Personal(actor).Available)return false;
   var tool=World.Object(item);if(tool.Location!="Hand"||tool.OwnerId!=actor)return false;
   var hand=actors[actor].RightHand.position;var targetBody=actors[target];
   bool overlap=Physics.OverlapSphere(hand,.5f,~0,QueryTriggerInteraction.Ignore).Any(x=>x.GetComponentInParent<FixtureActorBody>()==targetBody);
   point=P(targetBody.transform.position+Vector3.up*(targetBody.Height*.57f));
   return overlap&&World.Pose(actor).Distance(World.Pose(target))<=1.2&&ClearSight(actor,point);
  }
  public bool SeesSubject(string observer,string subject)
  {
   if(!actors.ContainsKey(subject))return false;
   if(observer==subject)return true;
   var p=World.Pose(subject).Plus(new Point3(0,World.Actor(subject).Incapacitated?.25:actors[subject].Height*.88,0));
   return SensorSees(observer,p);
  }
  bool SensorSees(string observer,Point3 p)
  {
   if(actors.ContainsKey(observer))return !World.Actor(observer).Incapacitated&&CanSee(observer,p);
   var reader=CaseReaders.FirstOrDefault(x=>x.StableId==observer&&x.Kind=="Video");if(!reader||!reader.Sensor)return false;
   var origin=reader.Sensor.position;var delta=V(p)-origin;
   if(delta.magnitude>reader.Range||Vector3.Angle(reader.Sensor.forward,delta)>reader.FieldOfView*.5f)return false;
   return !Physics.RaycastAll(origin,delta.normalized,Mathf.Max(0,delta.magnitude-.1f),~0,QueryTriggerInteraction.Ignore).Any(hit=>!hit.collider.bounds.Contains(V(p))&&!hit.collider.transform.IsChildOf(reader.transform));
  }
  public string[] ContactObservers(string actor,string target,string item)
  {
   var hand=P(actors[actor].RightHand.position);
   return FixtureDefinition.Actors.Concat(new[]{"K_V1"}).Where(x=>SeesSubject(x,actor)&&SeesSubject(x,target)&&(x==actor||SensorSees(x,hand))).ToArray();
  }
  void ProcessIncidentKnowledge()
  {
   var c=World.ReadIncidentForSystem();
   foreach(var o in c.Observations.Where(x=>actors.ContainsKey(x.Observer))){
    if(Knowledge.For(o.Observer).Records().Any(r=>r.ProvenanceKey==o.SourceKey&&r.Predicate==o.Predicate&&r.FromTick==o.From))continue;
    RecordIncident(o.Observer,o,"Visual");
   }
  }
  string RecordIncident(string owner,IncidentObservation o,string kind)
  {
   var old=Knowledge.For(owner).Records().FirstOrDefault(r=>r.ProvenanceKey==o.SourceKey&&r.Predicate==o.Predicate&&r.FromTick==o.From);
   if(old!=null)return old.Id;
   return Knowledge.Observe(owner,new KnownRecord{ProvenanceKey=o.SourceKey,Kind=kind,SubjectId=o.Subject,Predicate=o.Predicate,Value=o.Value,PlaceId=PlaceAt(o.Position),Position=o.Position,Text=kind=="Video"?"V1 녹화에서 확인: "+o.Text:o.Text,Source=o.Observer,IdentityConfirmed=o.Identity,FromTick=o.From,ToTick=o.To,Supports=o.Supports,DoesNotEstablish=o.Limits},World.Tick);
  }
  bool CanInspectCase(string id)
  {
   var r=CaseReaders.FirstOrDefault(x=>x.StableId==id);return r&&r.gameObject.activeInHierarchy&&World.Pose("CH_01").Distance(P(r.transform.position))<=1.8&&CanSee("CH_01",P(r.transform.position),2.5);
  }
  bool CanInspectBody(string id)=>actors.ContainsKey(id)&&World.Actor(id).Incapacitated&&World.Pose("CH_01").Distance(World.Pose(id))<=1.8&&CanSee("CH_01",World.Pose(id).Plus(new Point3(0,.3,0)),2.5);
  string ExamineBody(string id)
  {
   if(!CanInspectBody(id))return "Unavailable";
   return Observe("CH_01","Inspection","AtPlace",id,"Collapsed",World.Pose(id),ActorName(id)+"이(가) 쓰러져 있는 상태를 가까이에서 확인했다.",new[]{"확인한 현재 시점의 인물·위치·쓰러진 상태"},new[]{"발생 시각·가해자·도구·고의·의학적 사망 확인은 입증하지 않음"});
  }
  string ExamineCase(string id)
  {
   if(!CanInspectCase(id))return "Unavailable";var c=World.ReadIncidentForSystem();var r=CaseReaders.First(x=>x.StableId==id);
   if(r.Kind=="Video"){
    var captured=c.Observations.Where(x=>x.Observer==id).ToArray();
    if(captured.Length==0)return Observe("CH_01","Inspection","RecordingScope",id,"NoContactRecord",P(r.transform.position),"V1을 확인했다. 이 기록 구간에는 접촉 장면이 없다.",new[]{"지금 열람한 V1 기록의 범위"},new[]{"다른 시간·사각지대에 사건이 없었다는 뜻은 아님"});
    string result="Unavailable";foreach(var o in captured)result=RecordIncident("CH_01",o,"Video");return result;
   }
   if(r.Kind=="ContactTrace"&&c.TracePresent){
    var old=Knowledge.For("CH_01").Records().FirstOrDefault(x=>x.ProvenanceKey=="K_TRACE_P31_01");if(old!=null)return old.Id;
    return Knowledge.Observe("CH_01",new KnownRecord{ProvenanceKey="K_TRACE_P31_01",Kind="Inspection",Predicate="ContactTrace",SubjectId="K_P31",Value="X31_Contact",PlaceId="K_W",Position=P(r.transform.position),Source="K_P31",Text="P31: 가상 접촉 흔적을 확인했다. 흔적 자체에는 행위자의 이름이 없다.",FromTick=World.Tick,ToTick=World.Tick+1,IdentityConfirmed=false,Supports=new[]{"현재 이 표면에 접촉 흔적이 남아 있음"},DoesNotEstablish=new[]{"행위자의 신원·고의·정확한 생성 시각·단독 사인은 입증하지 않음"}},World.Tick);
   }
   return "Unavailable";
  }
  void PresentIncident()
  {
   if(!World.IsIncidentFixture)return;var c=World.ReadIncidentForSystem();
   foreach(var r in CaseReaders.Where(x=>x.Kind=="ContactTrace")){r.gameObject.SetActive(c.TracePresent);if(c.TracePresent)r.transform.position=V(World.Pose(c.Settings.Target))+(World.Actor(c.Settings.Target).Incapacitated?new Vector3(.55f,.52f,0):Vector3.up*.99f);}
   foreach(var b in Bodies){bool down=World.Actor(b.ActorId).Incapacitated;var corpse=b.transform.Find("CollapsedCollision_Proxy");if(corpse)corpse.gameObject.SetActive(down);b.Capsule.enabled=!down;
    var visual=b.transform.Find("NormalSilhouette_Proxy");if(visual&&b.ActorId!="CH_01"){visual.localRotation=down?Quaternion.Euler(0,0,90):Quaternion.identity;visual.localPosition=down?new Vector3(.55f,.25f,0):new Vector3(0,b.Height*.46f,0);}
   }
  }
 }
}




