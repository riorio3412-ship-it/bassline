using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.World.Fixture
{
 [Serializable] public sealed class IncidentWitness
 {
  public string Observer;public bool Continuous=true;public IncidentWitness Copy()=>(IncidentWitness)MemberwiseClone();
 }
 [Serializable] public sealed class IncidentObservation
 {
  public string Id,Observer,SourceKey,Predicate,Subject,Value,Text;public long From,To;
  public Point3 Position;public bool Identity;public string[] Supports,Limits;
  public IncidentObservation Copy()=>new IncidentObservation{Id=Id,Observer=Observer,SourceKey=SourceKey,Predicate=Predicate,Subject=Subject,Value=Value,Text=Text,From=From,To=To,Position=Position,Identity=Identity,Supports=(string[])Supports.Clone(),Limits=(string[])Limits.Clone()};
 }
 [Serializable] public sealed class IncidentSnapshot
 {
  public bool Enabled;public IncidentSettings Settings;public string Stage="Inactive",Reason="",CauseEvent="",ResultEvent="";public long CauseTick=-1,DueTick=-1,ResultTick=-1;public int ContactProgress,ReservedFatalities;
  public Point3 CausePosition,ResultPosition,ContactPoint;public bool TracePresent;
  public IncidentWitness[] Witnesses=Array.Empty<IncidentWitness>();public IncidentObservation[] Observations=Array.Empty<IncidentObservation>();
  public IncidentSnapshot Copy()=>new IncidentSnapshot{Enabled=Enabled,Settings=Enabled?Settings?.Copy():null,Stage=Stage,Reason=Reason,CauseEvent=CauseEvent,ResultEvent=ResultEvent,CauseTick=CauseTick,DueTick=DueTick,ResultTick=ResultTick,ContactProgress=ContactProgress,ReservedFatalities=ReservedFatalities,CausePosition=CausePosition,ResultPosition=ResultPosition,ContactPoint=ContactPoint,TracePresent=TracePresent,Witnesses=Witnesses.Select(x=>x.Copy()).ToArray(),Observations=Observations.Select(x=>x.Copy()).ToArray()};
 }
 public sealed partial class LifeWorld
 {
  IncidentSnapshot incident=new IncidentSnapshot();
  public bool IsIncidentFixture=>incident.Enabled;
  public IncidentSnapshot ReadIncidentForSystem()=>incident.Copy();
  void ConfigureIncident(IncidentSettings settings)
  {
   if(settings==null)return;ValidateSettings(settings);incident=new IncidentSnapshot{Enabled=true,Settings=settings.Copy(),Stage="Planned"};Tick=settings.StartTick;
   void Place(string id,string node,Point3 p){actors[id].Node=node;actors[id].Position=p;}
   Place("CH_01","K_L",new Point3(-28,0,-1));Place("CH_02","K_W",new Point3(27.5,0,-1.1));Place("CH_03","K_W",new Point3(29,0,-2.25));Place("CH_04","K_H",new Point3(-1,0,.5));Place("CH_11","K_L",new Point3(-26.5,0,1));Place("CH_18","K_S",new Point3(28.6,0,-4));
   actors["CH_03"].HeadYaw=296.076f;objects.Add("K_O31",new LifeObject{Id="K_O31",Name="O31 · 가상 접촉 도구",Position=new Point3(26.85,.95,-1.7),AnchorId="K_WORKBENCH"});
  }
  static void ValidateSettings(IncidentSettings s)
  {
   if(s==null||s.Id!="K_INCIDENT_SETUP_01"||s.Version!="FixtureK_Contact_001"||s.Template!="X31"||s.Actor!="CH_04"||s.Target!="CH_02"||s.Object!="K_O31"||s.Approach!="K_SEAT_W_01"||s.StartTick<0||s.ApproachTick<s.StartTick||s.EarliestCauseTick<s.ApproachTick||s.EarliestDepartureTick<s.EarliestCauseTick+s.DelayTicks||s.OpportunityEndTick<s.EarliestCauseTick||s.ContactTicks<1||s.DelayTicks<1||s.FatalityCap!=1)throw new ArgumentException("Unregistered TestOnly incident settings");
  }
  public void RequestIncidentApproach(bool knownAppointmentDue)
  {
   if(!IsIncidentFixture||Paused||knownAppointmentDue)return;var s=incident.Settings;
   if(incident.Stage=="Planned"&&Tick>=s.ApproachTick&&Personal(s.Actor).Available){
    if(Schedule(s.Actor,"ACT_PASSAGE",s.Approach)=="Accepted"){incident.Stage="Approaching";Emit("IntentSelected",s.Actor,s.Id,"TestOnly conflict; withdraw/help alternatives rejected; X31 chosen");}
   }
  }
  public void AdvanceIncident(IIncidentPhysics contact,IFixturePhysics physics,IActorKnowledgeQuery ownKnowledge)
  {
   if(!IsIncidentFixture||Paused)return;var s=incident.Settings;
   if(incident.Stage=="ResultCommitted"){
    if(Tick>=s.EarliestDepartureTick&&Personal(s.Actor).Available&&Schedule(s.Actor,"ACT_PASSAGE","K_SEAT_H_01",new[]{"K_DOOR_N"})=="Accepted")incident.Stage="Departing";
    return;
   }
   if(incident.Stage=="Departing"){if(actors[s.Actor].Node=="K_H"&&Personal(s.Actor).Available)incident.Stage="Ended";return;}
   if(incident.Stage=="CauseCommitted"){
    foreach(var witness in incident.Witnesses)if(!contact.SeesSubject(witness.Observer,s.Actor)||!contact.SeesSubject(witness.Observer,s.Target))witness.Continuous=false;
    if(Tick<incident.DueTick)return;
    var victim=actors[s.Target];if(victim.Incapacitated)throw new InvalidOperationException("Reserved target already incapacitated");
    foreach(var transfer in transfers.Where(x=>x.FromId==s.Target||x.ToId==s.Target).ToArray()){transfers.Remove(transfer);receipts[transfer.Id].Outcome="Cancelled";Emit("TransferCancelled",s.Target,transfer.ObjectId);}
    if(objects.Values.Any(x=>x.OwnerId==s.Target&&x.Location=="Hand"))Drop(NextCommand(),s.Target,physics);
    ReleaseSeat(victim);victim.Phase="Idle";victim.ActivityId="";victim.AnchorId="";victim.PathEdges=Array.Empty<string>();victim.Leg=Array.Empty<Point3>();victim.EdgeIndex=0;victim.PointIndex=1;victim.Incapacitated=true;
    foreach(var door in doors.Values)door.Queue=door.Queue.Where(x=>x.ActorId!=s.Target).ToArray();
    incident.ResultTick=Tick;incident.ResultPosition=victim.Position;incident.ReservedFatalities=0;incident.Stage="ResultCommitted";Emit("IncidentResultCommitted",s.Actor,s.Target,incident.CauseEvent);incident.ResultEvent=events.Last().Id;
    foreach(var observer in FixtureDefinition.Actors.Concat(new[]{"K_V1"}).Where(x=>x!=s.Target&&contact.SeesSubject(x,s.Target))){
     bool continuous=incident.Witnesses.Any(x=>x.Observer==observer&&x.Continuous);
     AddIncidentObservation(observer,continuous?"CausedOutcome":"AtPlace",continuous?s.Actor:s.Target,continuous?"X31_Contact_Then_Collapse":"Collapsed",continuous?"도윤의 O31 접촉부터 진우가 쓰러지는 순간까지 계속 보았다.":"진우가 작업실에서 쓰러지는 모습을 보았다.",continuous?incident.CauseTick:Tick,Tick+1,continuous?new[]{"해당 시각의 신원·접촉·연속된 쓰러짐 관측"}:new[]{"관측 순간의 쓰러진 인물과 위치"},new[]{"관측 밖 동선·숨은 의도·의학적 사망 확인은 별도"});
    }
    return;
   }
   if(incident.Stage=="Cancelled"||incident.Stage=="Ended")return;
   if(Tick>s.OpportunityEndTick){incident.Stage="Cancelled";incident.Reason="OpportunityExpired";incident.ContactProgress=0;Emit("IncidentCancelled",s.Actor,s.Id,incident.Reason);return;}
   if(incident.Stage=="Planned"||actors[s.Actor].Incapacitated||actors[s.Target].Incapacitated)return;
   if(ownKnowledge.OwnerId!=s.Actor)throw new InvalidOperationException("Incident planner received another actor's knowledge");
   // Choosing an action uses the actor's observations; physical execution checks A again.
   bool Known(string subject,string predicate)=>ownKnowledge.Records().Any(x=>x.SubjectId==subject&&x.Predicate==predicate&&x.Direct&&x.IdentityConfirmed&&Tick-x.ToTick<=600);
   if(incident.Stage=="Approaching"){
    if(!Personal(s.Actor).Available||Pose(s.Actor).Distance(FixtureDefinition.Anchors.Single(x=>x.Id==s.Approach).Position)>.4)return;
    if(!Known(s.Object,"AtPlace")){incident.Reason="ObjectNotObserved";return;}
    if(Pickup(NextCommand(),s.Actor,s.Object,physics)!="Committed"){incident.Reason="ToolUnavailable";return;}
    incident.Stage="Ready";incident.Reason="";
   }
   if(incident.Stage!="Ready"&&incident.Stage!="Contact")return;
   if(Tick<s.EarliestCauseTick-s.ContactTicks+1)return;
   if(!Known(s.Target,"AtPlace")||!contact.HasContact(s.Actor,s.Target,s.Object,out var point)||objects[s.Object].OwnerId!=s.Actor){incident.ContactProgress=0;incident.Stage="Ready";incident.Reason="ContactOpportunityLost";return;}
   incident.Stage="Contact";incident.Reason="";if(++incident.ContactProgress<s.ContactTicks)return;
   if(actors.Values.Count(x=>x.Incapacitated)+incident.ReservedFatalities>=s.FatalityCap){incident.Stage="Cancelled";incident.Reason="FatalityCap";return;}
   incident.ReservedFatalities=1;incident.Stage="CauseCommitted";incident.CauseTick=Tick;incident.DueTick=Tick+s.DelayTicks;incident.CausePosition=Pose(s.Target);incident.ContactPoint=point;incident.TracePresent=true;
   Emit("IncidentCauseCommitted",s.Actor,s.Object,s.Target);incident.CauseEvent=events.Last().Id;
   incident.Witnesses=contact.ContactObservers(s.Actor,s.Target,s.Object).Distinct().Select(x=>new IncidentWitness{Observer=x}).ToArray();
   foreach(var witness in incident.Witnesses)AddIncidentObservation(witness.Observer,"UsedObject",s.Actor,s.Object,"도윤이 진우에게 O31을 접촉하는 모습을 보았다.",Tick,Tick+1,new[]{"이 순간 실제 보인 신원·도구·접촉"},new[]{"아직 결과·고의·관측 밖 행동은 입증하지 않음"});
  }
  void AddIncidentObservation(string observer,string predicate,string subject,string value,string text,long from,long to,string[] supports,string[] limits)
  {
   string id="K_CASE_OBS_"+(incident.Observations.Length+1);incident.Observations=incident.Observations.Concat(new[]{new IncidentObservation{Id=id,Observer=observer,SourceKey=observer=="K_V1"?"K_MEDIA_V1_STREAM_01":"K_WITNESS_"+observer+"_01",Predicate=predicate,Subject=subject,Value=value,Text=text,From=from,To=to,Position=Pose(incident.Settings.Target),Identity=true,Supports=supports,Limits=limits}}).ToArray();
  }
  static void ValidateIncident(LifeSnapshot snapshot)
  {
   var c=snapshot.Incident;if(c==null||!c.Enabled){if(snapshot.Actors.Any(x=>x.Incapacitated)||snapshot.Events.Any(x=>x.Type.StartsWith("Incident",StringComparison.Ordinal)))throw new ArgumentException("Incident state missing");return;}
   ValidateSettings(c.Settings);if(!new[]{"Planned","Approaching","Ready","Contact","CauseCommitted","ResultCommitted","Departing","Ended","Cancelled"}.Contains(c.Stage)||c.ContactProgress<0||c.ContactProgress>c.Settings.ContactTicks||c.ReservedFatalities<0||c.ReservedFatalities>1||!c.CausePosition.Finite()||!c.ResultPosition.Finite()||!c.ContactPoint.Finite())throw new ArgumentException("Invalid incident stage");
   bool caused=c.CauseTick>=0,finished=c.ResultTick>=0;if(caused!=c.TracePresent||caused&&!snapshot.Events.Any(e=>e.Id==c.CauseEvent&&e.Type=="IncidentCauseCommitted"&&e.Tick==c.CauseTick)||finished&&!snapshot.Events.Any(e=>e.Id==c.ResultEvent&&e.Type=="IncidentResultCommitted"&&e.Tick==c.ResultTick)||caused&&c.DueTick!=c.CauseTick+c.Settings.DelayTicks||finished&&c.ResultTick<c.DueTick||snapshot.Actors.Single(x=>x.Id==c.Settings.Target).Incapacitated!=finished||snapshot.Actors.Count(x=>x.Incapacitated)>1)throw new ArgumentException("Invalid cause/result chain");
   if(c.Stage=="CauseCommitted"&&(!caused||finished||c.ReservedFatalities!=1)||finished&&c.ReservedFatalities!=0||caused!=new[]{"CauseCommitted","ResultCommitted","Departing","Ended"}.Contains(c.Stage)||finished!=new[]{"ResultCommitted","Departing","Ended"}.Contains(c.Stage)||!caused&&(c.ReservedFatalities!=0||c.CauseEvent!=""||c.ResultEvent!=""||c.DueTick!=-1)||caused&&(c.CauseTick>snapshot.Tick||c.CauseTick<c.Settings.EarliestCauseTick)||finished&&c.ResultTick>snapshot.Tick)throw new ArgumentException("Invalid fatality reservation/stage");
   if(c.Witnesses==null||c.Observations==null||c.Witnesses.Select(x=>x.Observer).Distinct().Count()!=c.Witnesses.Length||c.Witnesses.Any(x=>!FixtureDefinition.Actors.Concat(new[]{"K_V1"}).Contains(x.Observer))||!caused&&(c.Witnesses.Length>0||c.Observations.Length>0))throw new ArgumentException("Invalid witness set");
   long n=0;foreach(var o in c.Observations){if(o.Id!="K_CASE_OBS_"+(++n)||!FixtureDefinition.Actors.Concat(new[]{"K_V1"}).Contains(o.Observer)||o.From<0||o.To<=o.From||o.To>snapshot.Tick+1||!o.Position.Finite()||o.Supports==null||o.Limits==null)throw new ArgumentException("Invalid recorded observation");}
  }
 }
}





