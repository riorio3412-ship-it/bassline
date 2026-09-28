using System;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.Knowledge;
using BASSLINE.NPC;
using BASSLINE.Investigation;
using BASSLINE.World.Fixture;
using BASSLINE.Save;
namespace BASSLINE.Bootstrap
{
    public sealed partial class FixtureRuntime
    {
        public KnowledgeLedger Knowledge {get;private set;}
        public SocialLedger Social {get;private set;}
        public InvestigationNotebook Notebook {get;private set;}
        long processedWorldEvent;
        PlayerUiSnapshot uiState=new PlayerUiSnapshot();
        InspectionState inspection=new InspectionState(); public int InspectionDurationTicks=120;
        public InspectionState ReadInspection()=>inspection.Copy();
        public void CancelInspection(){if(inspection.State=="Running")inspection.State="Cancelled";}
        public PlayerUiSnapshot ReadUiState()=>uiState.Copy();
        public void StoreUiState(PlayerUiSnapshot state){uiState=state.Copy();}
        void InitializeKnowledge(){Knowledge=new KnowledgeLedger(FixtureDefinition.Actors);Social=new SocialLedger(FixtureDefinition.Actors,new[]{"K_H","K_W","K_L","K_G"});Notebook=new InvestigationNotebook();processedWorldEvent=World.Capture().Sequence;uiState=new PlayerUiSnapshot();inspection=new InspectionState();}
        public FixtureSessionSnapshot CaptureSession()=>new FixtureSessionSnapshot{World=World.Capture(),ClockRemainderHex=BitConverter.DoubleToInt64Bits(World.PendingTime).ToString("X16",System.Globalization.CultureInfo.InvariantCulture),Knowledge=Knowledge.Capture(),Social=Social.Capture(),Investigation=Notebook.Capture(),ProcessedWorldEvent=processedWorldEvent,Ui=uiState.Copy(),Inspection=inspection.Copy()};
        public void RestoreSession(FixtureSessionSnapshot s)
        {
            SessionSaveStore.Validate(s);
            s.World.PendingTime=SessionSaveStore.DecodeClockRemainder(s);
            var world=LifeWorld.Restore(s.World);var knowledge=KnowledgeLedger.Restore(s.Knowledge,FixtureDefinition.Actors,s.World.Tick);
            var social=SocialLedger.Restore(s.Social,FixtureDefinition.Actors,new[]{"K_H","K_W","K_L","K_G"},s.World.Tick);var notebook=InvestigationNotebook.Restore(s.Investigation,knowledge.For);
            World=world;Knowledge=knowledge;Social=social;Notebook=notebook;processedWorldEvent=s.ProcessedWorldEvent;uiState=s.Ui.Copy();inspection=s.Inspection.Copy();
        }
        public static string PlaceAt(Point3 p)
        {
            foreach(var id in new[]{"K_H","K_W","K_L","K_G"}){var c=FixtureDefinition.Nodes[id];if(Math.Abs(p.X-c.X)<3&&Math.Abs(p.Z-c.Z)<3)return id;}return "K_CORRIDOR";
        }
        public static string PlaceName(string id)=>id=="K_H"?"홀":id=="K_W"?"작업실":id=="K_L"?"도서실":id=="K_G"?"온실":"복도";
        public string ActorName(string id)=>actors.TryGetValue(id,out var body)?body.GetComponent<BASSLINE.AuthoringData.FixtureTarget>().PublicName:"신원 미확인";
        public bool CanSee(string observer,Point3 target,double range=12)
        {
            if(World.Actor(observer).Incapacitated)return false;var body=actors[observer];var eye=body.transform.position+Vector3.up*(body.Height*.88f);var delta=V(target)-eye;
            if(delta.magnitude>range||delta.magnitude<.01)return false;
            var forward=observer=="CH_01"?body.GetComponentInChildren<Camera>().transform.forward:body.Head.forward;
            return Vector3.Dot(forward,delta.normalized)>=.5f&&ClearSight(observer,target);
        }
        void ProcessKnowledge()
        {
            if(World.Tick%30==0){
                foreach(string observer in FixtureDefinition.Actors)foreach(string subject in FixtureDefinition.Actors.Where(x=>x!=observer)){
                    var position=World.Pose(subject);if(!CanSee(observer,position.Plus(new Point3(0,actors[subject].Height*.88,0))))continue;
                    var place=PlaceAt(position);var old=Knowledge.For(observer).Records().LastOrDefault(x=>x.Predicate=="AtPlace"&&x.SubjectId==subject&&x.Direct);
                    if(old!=null&&old.PlaceId==place&&World.Tick-old.FromTick<600)continue;
                    Observe(observer,"Visual","AtPlace",subject,place,position,ActorName(subject)+"을(를) "+PlaceName(place)+"에서 봄",new[]{"관측한 순간의 위치와 신원"},new[]{"관측 전후의 위치·목적·행동을 확정하지 않음"});
                }
                foreach(string observer in FixtureDefinition.Actors)foreach(var item in World.Capture().Objects.Where(x=>x.Location=="Surface")){
                    if(!CanSee(observer,item.Position,8))continue;
                    var old=Knowledge.For(observer).Records().LastOrDefault(x=>x.SubjectId==item.Id&&x.Predicate=="AtPlace"&&x.Direct);
                    if(old!=null&&World.Tick-old.FromTick<300&&old.Position.Distance(item.Position)<.1)continue;
                    Observe(observer,"Visual","AtPlace",item.Id,PlaceAt(item.Position),item.Position,item.Name+"의 위치를 봄",new[]{"이 시점에 직접 본 물건 위치"},new[]{"소유자·이전 사용·고의는 입증하지 않음"});
                }
                foreach(string actor in FixtureDefinition.Actors)foreach(var appointment in Social.For(actor,World.Tick).Where(x=>x.State=="Agreed")){
                    string other=appointment.Organizer==actor?appointment.Invitee:appointment.Organizer;
                    if(PlaceAt(World.Pose(actor))==appointment.PlaceId&&PlaceAt(World.Pose(other))==appointment.PlaceId&&World.Pose(actor).Distance(World.Pose(other))<3&&CanSee(actor,World.Pose(other).Plus(new Point3(0,1.5,0)),3))Social.Meet(appointment.Id,actor,other,World.Tick);
                }
            }
            var events=World.Capture().Events;
            foreach(var e in events.Skip((int)processedWorldEvent)){
                if(e.Type=="ObjectTransferred"){
                    Social.Experience(e.ActorId,e.Detail,"ObjectGiven",e.Id,e.Tick,1,1);Social.Experience(e.Detail,e.ActorId,"ObjectReceived",e.Id,e.Tick,1,1);
                }
                if(e.Type=="ObjectPickedUp"||e.Type=="ObjectTransferred")foreach(var observer in FixtureDefinition.Actors){
                    string holder=e.Type=="ObjectTransferred"?e.Detail:e.ActorId;var position=World.Pose(holder);
                    if(observer==holder||CanSee(observer,position.Plus(new Point3(0,actors[holder].Height*.88,0)),8)&&CanSee(observer,P(actors[holder].RightHand.position),8))Observe(observer,"Visual","HeldObject",holder,e.TargetId,position,ActorName(holder)+"의 물건 소지를 확인함",new[]{"관측 시점의 해당 물건 소지"},new[]{"사용·고의·이후 소지는 입증하지 않음"});
                }
            }
            processedWorldEvent=events.LongLength;
        }
        string Observe(string owner,string kind,string predicate,string subject,string value,Point3 position,string text,string[] supports,string[] limits)
            =>Knowledge.Observe(owner,new KnownRecord{Kind=kind,Predicate=predicate,SubjectId=subject,Value=value,Position=position,PlaceId=PlaceAt(position),Text=text,Source=owner,IdentityConfirmed=true,FromTick=World.Tick,ToTick=World.Tick+1,Supports=supports,DoesNotEstablish=limits},World.Tick);
        bool TryKnownAppointment(string actor)
        {
            var appointment=Social.Due(actor,World.Tick);if(appointment==null)return false;
            if(World.Personal(actor).Available){var seat=FixtureDefinition.Anchors.First(x=>x.Room==appointment.PlaceId&&x.Id.StartsWith("K_SEAT_",StringComparison.Ordinal)&&x.Id.EndsWith(actor==appointment.Organizer?"01":"02",StringComparison.Ordinal));World.Schedule(actor,"ACT_WAIT",seat.Id);}return true;
        }
        public NotebookView ReadNotebook()=>new NotebookView{Revision=Knowledge.For("CH_01").Revision,Records=Knowledge.For("CH_01").Records(),Locations=Knowledge.LastConfirmed("CH_01"),Appointments=Social.For("CH_01",World.Tick),RelationshipExperiences=Social.Experiences("CH_01").Select(e=>ActorName(e.Other)+" · "+(e.Kind=="PromiseKept"?"약속 장소에서 만남":e.Kind=="ObjectReceived"?"물건을 받음":e.Kind=="ObjectGiven"?"물건을 건넴":"대화함")+" · "+(e.Tick/60)+"초").ToArray(),Hypotheses=Notebook.For("CH_01").Select(x=>new HypothesisView{Id=x.Id,Text=x.Text,Status=x.Status,References=x.EvidenceRefs}).ToArray()};
        public string Hypothesize(string recordId){var query=Knowledge.For("CH_01");var r=query.Find(recordId);if(r==null)return "Unavailable";return Notebook.CreateHypothesis(query,"검증할 가설: "+r.Text+"\n이 자료만으로 관측 밖 범위까지 말할 수 있는가?",new[]{recordId});}
        public string SetHypothesisStatus(string id,string status)=>Notebook.SetStatus("CH_01",id,status);
        bool Near(string actor)=>actors.ContainsKey(actor)&&!World.Actor(actor).Incapacitated&&actor!="CH_01"&&World.Pose("CH_01").Distance(World.Pose(actor))<2.5&&ClearSight("CH_01",World.Pose(actor).Plus(new Point3(0,1.3,0)));
        public string Talk(string actorId)
        {
            if(World.Paused||!Near(actorId))return "Unavailable";
            var testimony=Knowledge.For(actorId).Records().LastOrDefault(x=>x.Direct&&x.Predicate=="CausedOutcome");if(actorId=="CH_03"&&testimony!=null){Knowledge.Deliver(actorId,"CH_01",testimony.Id,World.Tick);return "Dialogue";}string text="잠깐 이야기할 수 있어. 필요한 일이 있으면 말해 줘.";
            var id=Observe(actorId,"Speech","SaidStatement",actorId,"LivingGreeting",World.Pose(actorId),text,new[]{"해당 인물이 이 생활 대화를 발화함"},new[]{"숨은 의도·호감·계획을 입증하지 않음"});
            Knowledge.Deliver(actorId,"CH_01",id,World.Tick);Social.Experience("CH_01",actorId,"Talk",id,World.Tick);return "Dialogue";
        }
        public string Share(string actorId,string recordId)
        {
            if(World.Paused||!Near(actorId))return "Unavailable";
            return Knowledge.Deliver("CH_01",actorId,recordId,World.Tick);
        }
        public string Invite(string actorId,string placeId,long delayTicks)
        {
            if(World.Paused||!Near(actorId)||delayTicks<60)return "Unavailable";
            string id=Social.Propose("CH_01",actorId,placeId,World.Tick+delayTicks,120*60,World.Tick);if(!id.StartsWith("K_APPT_",StringComparison.Ordinal))return id;
            Social.Receive(id,1,actorId);Social.Accept(id,1,actorId,true);Social.ReceiveAcceptance(id,1,"CH_01");
            var receipt=Observe(actorId,"Speech","SaidStatement",actorId,id,World.Pose(actorId),"그때 "+PlaceName(placeId)+"에서 만나자.",new[]{"이 약속 revision에 동의했다는 발화"},new[]{"실제 참석·알리바이를 입증하지 않음"});Knowledge.Deliver(actorId,"CH_01",receipt,World.Tick);return "Accepted";
        }
        public string RespondToInvitation(string id,bool accept){var a=Social.For("CH_01",World.Tick).FirstOrDefault(x=>x.Id==id);return a==null?"Unavailable":Social.Accept(id,a.Revision,"CH_01",accept);}
        public InteractionView DescribeTarget(string targetId)
        {
            var reader=CaseReaders.FirstOrDefault(x=>x.StableId==targetId);if(reader)return new InteractionView{TargetId=targetId,Label=reader.Kind=="Video"?"V1 작업대 기록":"P31 접촉 흔적",PrimaryAction="살펴보기",Available=CanInspectCase(targetId)};if(actors.ContainsKey(targetId)&&World.Actor(targetId).Incapacitated)return new InteractionView{TargetId=targetId,Label=ActorName(targetId),PrimaryAction="상태 살펴보기",Available=CanInspectBody(targetId)};if(actors.ContainsKey(targetId))return new InteractionView{TargetId=targetId,Label=ActorName(targetId),PrimaryAction=World.HeldName("CH_01")=="없음"?"대화":"물건 전달",Available=Near(targetId)};
            if(doors.ContainsKey(targetId))return new InteractionView{TargetId=targetId,Label=targetId=="K_DOOR_N"?"북문":"남문",PrimaryAction="문 열기",Available=World.Pose("CH_01").Distance(LifeWorld.DoorPosition(targetId))<2};
            var item=ObjectBodies.FirstOrDefault(x=>x.ObjectId==targetId);if(item)return new InteractionView{TargetId=targetId,Label=World.Object(targetId).Name,PrimaryAction="줍기",Available=World.Pose("CH_01").Distance(P(item.transform.position))<1.8};
            return new InteractionView{TargetId=targetId,Label="활동 자리",PrimaryAction="쉬기",Available=FixtureDefinition.Anchors.Any(x=>x.Id==targetId)};
        }
        bool CanInspect(string id)
        {
            if(actors.ContainsKey(id)&&World.Actor(id).Incapacitated)return CanInspectBody(id);if(CaseReaders.Any(x=>x.StableId==id))return CanInspectCase(id);if(doors.ContainsKey(id)){var position=LifeWorld.DoorPosition(id);return World.Pose("CH_01").Distance(position)<=1.8&&CanSee("CH_01",position.Plus(new Point3(0,1,0)),2.5);}
            var item=ObjectBodies.FirstOrDefault(x=>x.ObjectId==id);return item&&World.Pose("CH_01").Distance(P(item.transform.position))<=1.8&&CanSee("CH_01",P(item.transform.position),2.5);
        }
        public string Examine(string id)
        {
            if(World.Paused||inspection.State=="Running"||!CanInspect(id))return "Unavailable";
            inspection=new InspectionState{TargetId=id,State="Running",DurationTicks=Math.Max(1,InspectionDurationTicks)};return "Pending";
        }
        void AdvanceInspection()
        {
            if(inspection.State!="Running")return;
            if(!CanInspect(inspection.TargetId)){inspection.State="Cancelled";return;}
            if(++inspection.ElapsedTicks<inspection.DurationTicks)return;
            string record=CompleteExamination(inspection.TargetId);
            inspection.State=Knowledge.For("CH_01").Find(record)!=null?"Completed":"Cancelled";inspection.RecordId=inspection.State=="Completed"?record:"";
        }
        string CompleteExamination(string targetId)
        {
            if(World.Paused)return "WorldPaused";if(actors.ContainsKey(targetId)&&World.Actor(targetId).Incapacitated)return ExamineBody(targetId);if(CaseReaders.Any(x=>x.StableId==targetId))return ExamineCase(targetId);
            if(doors.ContainsKey(targetId)){
                var position=LifeWorld.DoorPosition(targetId);if(World.Pose("CH_01").Distance(position)>1.8||!ClearSight("CH_01",position.Plus(new Point3(0,1,0))))return "Unavailable";
                var door=World.Door(targetId);string state=door.Locked?"Locked":door.OpenFraction>.99?"Open":"Closed";
                return Observe("CH_01","Inspection","DoorState",targetId,state,position,DescribeTarget(targetId).Label+"의 현재 상태: "+(state=="Locked"?"잠김":state=="Open"?"열림":"닫힘"),new[]{"지금 직접 확인한 문 상태"},new[]{"과거 문 상태·누가 통과했는지 입증하지 않음"});
            }
            var binding=ObjectBodies.FirstOrDefault(x=>x.ObjectId==targetId);if(!binding||World.Pose("CH_01").Distance(P(binding.transform.position))>1.8||!ClearSight("CH_01",P(binding.transform.position)))return "Unavailable";
            var item=World.Object(targetId);return Observe("CH_01","Inspection","AtPlace",targetId,PlaceAt(P(binding.transform.position)),P(binding.transform.position),item.Name+"의 겉모습과 현재 놓인 위치를 확인함",new[]{"현재 보이는 물건과 위치"},new[]{"이전 소지자·사용·숨은 내부는 입증하지 않음"});
        }
        public string Assess(string predicate,string subject,string value,long from,long to,string[] evidenceIds)
        {
            var query=Knowledge.For("CH_01");var claim=new ClaimRecord{Id="K_DRAFT_CLAIM",OwnerId=query.OwnerId,LoopId=query.LoopId,Spans=new[]{new ClaimSpan{Id="K_DRAFT_SPAN",Predicate=predicate,SubjectId=subject,Value=value,FromTick=from,ToTick=to}}};
            var result=new LogicResolver().Resolve(query,"K_DRAFT_ASSESS","LR01",claim,"K_DRAFT_SPAN",evidenceIds);return result.ResultType+" · "+result.Explanation;
        }
    }
}




