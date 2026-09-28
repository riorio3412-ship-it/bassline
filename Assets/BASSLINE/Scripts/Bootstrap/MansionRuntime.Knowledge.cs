using System;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.Investigation;
using BASSLINE.NPC;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerConversationPort
    {
        PlayerUiSnapshot ui=new PlayerUiSnapshot();InspectionState inspection=new InspectionState();
        string NameOf(string id)=>targets.TryGetValue(id,out var t)?t.PublicName:id;
        string PlaceOf(Vector3 position)=>Layout.RoomAt(position)?.RoomId??"R_CORRIDOR";
        string ObjectPlace(Vector3 position)=>Layout.RoomContainingPoint(position)?.RoomId??"R_CORRIDOR";
        string Observe(string owner,string subject,string predicate,string value,string text,Vector3 position,string[] supports,string[] limits,string place=null)
            =>Knowledge.Observe(owner,new KnownRecord{Kind="Visual",SubjectId=subject,Predicate=predicate,Value=value,PlaceId=place??PlaceOf(position),Position=P(position),Text=text,Source=owner,IdentityConfirmed=true,FromTick=World.Tick,ToTick=World.Tick+1,Supports=supports,DoesNotEstablish=limits},World.Tick);
        void ProcessKnowledge()
        {
            if(World.Tick%30!=0)return;
            ProcessWrittenAppointmentReceipts();
            foreach(var observer in Bodies){if(!World.CanAct(observer.ActorId))continue;
                ObserveMap(observer);ObservePresenterAndOwnPlace(observer.ActorId);
                foreach(var item in ObjectBodies){if(!item.gameObject.activeInHierarchy||!Visible(observer.ActorId,item.transform.position))continue;
                    var priorItem=Knowledge.LastDirect(observer.ActorId,item.ObjectId,"AtPlace");
                    if(priorItem==null||World.Tick-priorItem.FromTick>=300){string place=ObjectPlace(item.transform.position);Observe(observer.ActorId,item.ObjectId,"AtPlace",place,NameOf(item.ObjectId)+"의 현재 위치를 보았다.",item.transform.position,new[]{"관측 순간 물체 위치"},new[]{"이전 소지자나 용도를 확정하지 않음"},place);}
                }
                foreach(var appt in Social.For(observer.ActorId,World.Tick).Where(a=>a.State=="Agreed")){
                    string other=appt.Organizer==observer.ActorId?appt.Invitee:appt.Organizer;
                    if(PlaceOf(observer.transform.position)==appt.PlaceId&&PlaceOf(bodies[other].transform.position)==appt.PlaceId&&Visible(observer.ActorId,bodies[other].Head.position,3,false))Social.Meet(appt.Id,observer.ActorId,other,World.Tick);
                }
            }
        }
        public NotebookView ReadNotebook()=>new NotebookView{Revision=Knowledge.For("CH_01").Revision,Records=ReadPersonalRecords(),Locations=Knowledge.LastConfirmed("CH_01"),Appointments=Social.For("CH_01",World.Tick),RelationshipExperiences=Social.Experiences("CH_01").Select(e=>NameOf(e.Other)+" · "+(e.Kind=="PromiseKept"?"약속 장소에서 만났다.":e.Kind=="ItemReturned"?"빌린 물건을 돌려주었다.":e.Kind=="EverydayHelp"?"부탁받은 물건을 직접 전달했다.":e.Kind=="ClaimCorrected"?"단정했던 말을 바로잡고 사과했다.":"대화를 나누었다.")).ToArray(),Hypotheses=Notebook.For("CH_01").Select(h=>new HypothesisView{Id=h.Id,Text=h.Text,Status=h.Status,References=h.EvidenceRefs}).ToArray()};
        public InteractionView DescribeTarget(string id)
        {
            if(!targets.ContainsKey(id))return new InteractionView{TargetId=id,Label="",Available=false};
            string action=id=="PRES_YUSTI"?"말 걸기":bodies.ContainsKey(id)?(World.PhysicalBand(id)=="Critical"?"돕기":World.Resident(id).Alive?"말 걸기":"상태 확인"):doors.ContainsKey(id)?"문 확인 / 열기":ObjectBodies.Any(o=>o.ObjectId==id)?"물건 집기":"살펴보기";
            if(WashStation(id))action="손에 든 물건 씻기";
            if(PigmentSurface(id)&&HeldPigmentTool())action="표면에 도구 대기";
            if(id==AppointmentDesk.CardId)action="약속 카드 읽기 / 쓰기";
            if(returnStation&&SurfaceAction(id)!="")action=SurfaceAction(id);
            return new InteractionView{TargetId=id,Label=NameOf(id),Available=Reach(id),PrimaryAction=action};
        }
        public string Talk(string actorId)
        {
            if(World.Paused||actorId=="CH_01"||!bodies.ContainsKey(actorId)||!Reach(actorId))return "Unavailable";
            var actor=World.Resident(actorId);if(!World.CanAct(actorId)||!World.CanAct("CH_01")||RescueControls(actorId))return "Unavailable";
            CancelWeaponRinse();CancelToolPress();StopWaiting("이야기를 나누려고 멈췄어요.");
            string incidentText=IncidentConversation.InVoice(actorId,IncidentConversation.Greeting(Knowledge.For(actorId),IncidentConversationSince));
            string text=incidentText!=""?incidentText:new ResidentConversation().Greeting(actorId,actor.Activity,Knowledge.For(actorId));
            return BeginConversation(actorId,text,incidentText!=""?"IncidentGreeting":"LivingGreeting",Math.Max(240,text.Length*5));
        }
        public string Share(string actor,string record){if(World.Paused||!Reach(actor)||!bodies.ContainsKey(actor))return "Unavailable";return Knowledge.Deliver("CH_01",actor,record,World.Tick);}
        public string AskRecentObservation(string actor)
        {
            if(World.Paused||!bodies.ContainsKey(actor)||!World.Resident(actor).Alive||!World.Resident(actor).Present||!Reach(actor))return "Unavailable";
            foreach(var observed in presenceObservations.Sight.Where(p=>p.Observer==actor))PublishPresence(observed);
            var known=Knowledge.For(actor).Records().Where(r=>r.Direct&&(r.Kind=="Visual"||r.Kind=="Touch")&&r.PlaceId!=null&&!r.PlaceId.StartsWith("R_BED_",StringComparison.Ordinal)&&new[]{"AtPlace","HeldObject","PlacedObject","UsedObject","TracePresent","SurfacePattern","ContactPattern","SurfaceResidue","RinsedObject","DoorState","DoorMotion","DoorAttempt","PassedDoor","CausedOutcome"}.Contains(r.Predicate)).OrderByDescending(r=>r.ReceivedTick).FirstOrDefault();
            if(known==null)return "지금 전할 수 있는 직접 관측이 없습니다.";
            return BeginConversation(actor,"제가 직접 확인한 건 여기까지입니다. "+known.Text,"RecentObservation",720,"",known.Id);
        }
        public string RespondToInvitation(string id,bool accept)
        {var a=Social.For("CH_01",World.Tick).FirstOrDefault(x=>x.Id==id);return a==null?"Unavailable":Social.Accept(id,a.Revision,"CH_01",accept);}
        public string Hypothesize(string id){var q=Knowledge.For("CH_01");var r=q.Find(id);return r==null?"Unavailable":Notebook.CreateHypothesis(q,"검증할 가설: "+r.Text+"\n관측 범위 밖에서도 성립하는가?",new[]{id});}
        public string SetHypothesisStatus(string id,string status)=>Notebook.SetStatus("CH_01",id,status);
        public string Assess(string predicate,string subject,string value,long from,long to,string[] refs)
        {
            var query=Knowledge.For("CH_01");var claim=new ClaimRecord{Id="M_CLAIM",OwnerId="CH_01",LoopId=query.LoopId,Text="현재 자료 검토",Spans=new[]{new ClaimSpan{Id="M_SPAN",Predicate=predicate,SubjectId=subject,Value=value,FromTick=from,ToTick=to}}};
            var result=new LogicResolver().Resolve(query,"M_REVIEW","LR01",claim,"M_SPAN",refs);return result.Explanation;
        }
        public PlayerUiSnapshot ReadUiState()=>ui.Copy();
        public void StoreUiState(PlayerUiSnapshot state){ui=state.Copy();}
    }
}
