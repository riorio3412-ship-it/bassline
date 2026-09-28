using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.Knowledge;
using BASSLINE.Save;
using BASSLINE.World.Mansion;

namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        const string InspectBodyActivity="CheckCollapsedResident",SeekPresenterActivity="FindPresenter",ReportActivity="ReportCollapsedResident";
        List<ResidentResponseState> residentResponses=new List<ResidentResponseState>();
        static bool ResponseActive(ResidentResponseState s)=>s.Phase=="Inspecting"||s.Phase=="SeekingYusti"||s.Phase=="Reporting";
        bool ResponseControls(string actor)=>residentResponses.Any(s=>s.ActorId==actor&&ResponseActive(s));
        bool PlayerTalkingTo(string actor)=>conversationPlayback.Phase!="Idle"&&(conversationPlayback.SpeakerId==actor||conversationPlayback.RecipientId==actor);
        bool PresenterHearsReport(string actor)=>Presenter&&CanReach(actor,"PRES_YUSTI",3)&&!RoomSilent(actor)&&!RoomSilent("PRES_YUSTI")&&SpeechPathOpen("PRES_YUSTI",actor);

        void ObservePresenterAndOwnPlace(string actor)
        {
            if(new[]{"Sleep","Sleeping"}.Contains(World.Resident(actor).Activity))return;
            var body=bodies[actor];string room=PlaceOf(body.transform.position);var own=Knowledge.For(actor);
            if(actor!="CH_01"&&!own.Records().Any(r=>r.Direct&&r.Predicate=="PlaceVisited"&&r.SubjectId==actor&&r.PlaceId==room))
                Observe(actor,actor,"PlaceVisited",room,"직접 이동한 장소",body.transform.position,new[]{"직접 방문한 장소"},new[]{"다른 인물의 현재 위치는 알 수 없음"});
            if(!Presenter||!Identifies(actor,"PRES_YUSTI"))return;
            var prior=Knowledge.LastDirect(actor,"PRES_YUSTI","AtPlace");
            if(prior==null||World.Tick-prior.FromTick>=60)
                Observe(actor,"PRES_YUSTI","AtPlace",PlaceOf(Presenter.transform.position),"유스티를 직접 보았다.",Presenter.transform.position,new[]{"관측 당시 유스티의 위치"},new[]{"이후 위치나 이동 목적지는 알 수 없음"});
        }
        void AdvanceResidentResponses()
        {
            // A nearby visible collapsed body starts a physical check. Case authority only resolves
            // the body being perceived; no hidden incident, cause, or reporter status drives a choice.
            if(World.Tick%30==0)foreach(var actor in World.Residents.Where(a=>a.Id!="CH_01"&&a.Alive&&a.Present)){
                if(!World.CanAct(actor.Id)||RescueControls(actor.Id)||ResponseControls(actor.Id)||incidents.Controls(actor.Id)||ReturnTaskControls(actor.Id)||PlayerTalkingTo(actor.Id)||actor.Activity=="Sleep"||actor.Activity=="Sleeping"||HasReceivedCourtSummons(actor.Id))continue;
                var seen=incidents.All().FirstOrDefault(c=>new[]{"ResultCommitted","Discovered","Reported","Confirmed"}.Contains(c.Stage)&&!World.Resident(c.TargetId).Alive&&World.Resident(c.TargetId).Present&&CanReach(actor.Id,c.TargetId,2.5)&&CanSee(actor.Id,c.TargetId)
                    &&!residentResponses.Any(s=>s.ActorId==actor.Id&&s.CaseId==c.CaseId&&s.Phase!="Cancelled")
                    &&!c.ReceiptsFor(actor.Id).Any(r=>r.Record.Kind=="OfficialReport"));
                if(seen==null)continue;
                InterruptResidentConversation(actor.Id);
                var task=residentResponses.FirstOrDefault(s=>s.ActorId==actor.Id&&s.CaseId==seen.CaseId);
                if(task==null){task=new ResidentResponseState{ActorId=actor.Id,CaseId=seen.CaseId,TargetId=seen.TargetId};residentResponses.Add(task);}
                task.Phase="Inspecting";task.StartedTick=World.Tick;task.InspectionTicks=0;task.ScanSteps=0;
                actor.Phase="Performing";actor.Activity=InspectBodyActivity;actor.ActivityTicks=int.MaxValue;
                World.Emit("ResidentBodyCheckStarted",actor.Id,seen.TargetId,seen.CaseId);
            }
            foreach(var task in residentResponses.Where(ResponseActive).ToArray()){
                var actor=World.Resident(task.ActorId);var incident=incidents.Find(task.CaseId);
                if(!World.CanAct(actor.Id)){StopResponse(task,"Cancelled");continue;}
                if(PlayerTalkingTo(actor.Id)||incidents.Controls(actor.Id)||ReturnTaskControls(actor.Id)){
                    if(task.Phase=="Reporting")FinishBodyReport(task,false);
                    if(task.Phase=="Inspecting"){task.InspectionTicks=0;StopResponse(task,"Cancelled");}continue;
                }
                if(incident.ReceiptsFor(actor.Id).Any(r=>r.Record.Kind=="OfficialReport")){StopResponse(task,task.ObservationId==""?"Cancelled":"Reported");continue;}
                if(task.Phase=="Inspecting"){
                    if(!CanReach(actor.Id,task.TargetId,2.5)||!CanSee(actor.Id,task.TargetId)){StopResponse(task,"Cancelled");continue;}
                    if(++task.InspectionTicks<120)continue;
                    if(incident.Discover(World,actor.Id,this)!="Discovered"){StopResponse(task,"Cancelled");continue;}
                    ImportCaseReceipts();task.ObservationId=incident.ReceiptsFor(actor.Id).Last(r=>r.Record.Value=="Collapsed").Id;
                    task.Phase="SeekingYusti";task.NextPlanTick=World.Tick;ReleaseResponseActivity(actor);
                }
                if(task.Phase=="Reporting"){
                    if(actor.Activity!=ReportActivity||!PresenterHearsReport(actor.Id)){FinishBodyReport(task,false);continue;}
                    SpeechProgress.Advance(task.Speech,World.Tick,ReceivesSpeech);
                    if(task.Speech.ElapsedTicks>=task.Speech.DurationTicks)FinishBodyReport(task,true);
                    continue;
                }
                if(PresenterHearsReport(actor.Id)&&CanSee(actor.Id,"PRES_YUSTI")){BeginBodyReport(task);continue;}
                if(World.Tick<task.NextPlanTick)continue;
                SeekKnownPresenter(task);
            }
        }
        void SeekKnownPresenter(ResidentResponseState task)
        {
            var actor=World.Resident(task.ActorId);var own=Knowledge.For(actor.Id);
            var seen=own.Records().Where(r=>r.SubjectId=="PRES_YUSTI"&&r.Predicate=="AtPlace"&&r.Direct&&r.IdentityConfirmed).OrderByDescending(r=>r.FromTick).FirstOrDefault();
            Vector3 point;
            if(seen!=null&&seen.Id!=task.PresenterRecordId){task.PresenterRecordId=seen.Id;point=V(seen.Position);}
            else{
                if(actor.Phase=="Travelling"){task.NextPlanTick=World.Tick+60;return;}
                if(task.ScanSteps<4){actor.Yaw=(actor.Yaw+90)%360;task.ScanSteps++;task.NextPlanTick=World.Tick+60;return;}
                task.ScanSteps=0;
                // Search only public places this person has visited, using remembered positions.
                var places=own.Records().Where(r=>r.Direct&&r.Predicate=="PlaceVisited"&&r.SubjectId==actor.Id&&MansionAnnouncementPolicy.IsPublicSearchRoom(r.PlaceId)).OrderBy(r=>r.PlaceId,StringComparer.Ordinal).ToArray();
                if(places.Length==0){task.NextPlanTick=World.Tick+60;return;}
                int prior=Array.FindIndex(places,r=>r.PlaceId==task.SearchRoom);var place=places[(prior+1)%places.Length];task.SearchRoom=place.PlaceId;point=V(place.Position);
            }
            // Stand beside the remembered location, not on another body's occupied node.
            foreach(var node in nodes.Where(n=>Vector3.Distance(V(n.Position),point)<2.3f).OrderBy(n=>Math.Abs(Vector3.Distance(V(n.Position),point)-1.2f)).ThenBy(n=>n.Id,StringComparer.Ordinal))
                if(World.Plan(actor.Id,node.Id,SeekPresenterActivity,180)=="Accepted")break;
            if(actor.Phase!="Travelling")actor.Yaw=(actor.Yaw+90)%360;
            task.NextPlanTick=World.Tick+180;
        }
        void BeginBodyReport(ResidentResponseState task)
        {
            var actor=World.Resident(task.ActorId);var receipt=incidents.Find(task.CaseId).ReceiptsFor(actor.Id).Single(r=>r.Id==task.ObservationId).Record;
            string who=receipt.IdentityConfirmed?NameOf(receipt.SubjectId)+" 씨가":"누군가";
            task.Speech=new ConversationPlaybackState{Id="REPORT_L"+World.Loop+"_"+World.Tick+"_"+actor.Id,Sequence=World.Tick,StartedTick=World.Tick,Phase="Speaking",SpeakerId=actor.Id,RecipientId="PRES_YUSTI",PlannedText="유스티, "+PlaceLabel(PlaceOf(V(receipt.Position)))+"에서 "+who+" 쓰러져 있는 걸 봤어요. 같이 확인해 주세요.",Value="BodyReport:"+task.TargetId,DurationTicks=360,Listeners=World.Residents.Where(a=>a.Alive&&a.Present&&a.Id!=actor.Id).Select(a=>new ConversationListener{ActorId=a.Id}).ToArray()};
            actor.Phase="Performing";actor.Activity=ReportActivity;actor.ActivityTicks=int.MaxValue;task.Phase="Reporting";
            World.Emit("ResidentBodyReportStarted",actor.Id,"PRES_YUSTI",task.CaseId);
        }
        void FinishBodyReport(ResidentResponseState task,bool completed)
        {
            var speech=task.Speech;if(speech.Phase!="Speaking")return;
            CommitSpeech(speech,completed);speech.Committed=true;speech.Phase=completed?"Reading":"Interrupted";
            task.Phase="SeekingYusti";task.NextPlanTick=World.Tick+120;ReleaseResponseActivity(World.Resident(task.ActorId));
            if(completed&&ReceiveBodyReport(task.ActorId,incidents.Find(task.CaseId))){task.Phase="Reported";MakeRoomAfterReport(task);StartNextReportedCase();}
            World.Emit(completed?"ResidentBodyReportCompleted":"ResidentBodyReportInterrupted",task.ActorId,"PRES_YUSTI",task.CaseId);
        }
        bool ReceiveBodyReport(string actor,MansionIncident incident)
        {
            if(incident.CanConvene){DeliverOfficialNotice(actor,false);return true;}
            if(incident.Report(World,actor,true)!="Reported")return false;
            if(!proceedings.PendingReportCases.Contains(incident.CaseId))proceedings.PendingReportCases=proceedings.PendingReportCases.Concat(new[]{incident.CaseId}).ToArray();
            return true;
        }
        void MakeRoomAfterReport(ResidentResponseState task)
        {
            // Give the examiner room to pass using nearby visible floor and the location just
            // reported. The reporter still walks through the normal collision/door system.
            var actor=World.Resident(task.ActorId);var receipt=incidents.Find(task.CaseId).ReceiptsFor(actor.Id).Single(r=>r.Id==task.ObservationId).Record;
            Vector3 origin=Presenter.transform.position,forward=V(receipt.Position)-origin;forward.y=0;forward.Normalize();
            Vector3 side=Vector3.Cross(Vector3.up,forward),position=V(actor.Position);
            foreach(var node in nodes.Where(n=>n.Room==PlaceOf(position)&&Vector3.Distance(V(n.Position),position)<2.8f&&Math.Abs(Vector3.Dot(V(n.Position)-origin,side))>1.3f&&Visible(actor.Id,V(n.Position)+Vector3.up,4,false)).OrderBy(n=>Vector3.Distance(V(n.Position),position)).ThenBy(n=>n.Id,StringComparer.Ordinal))
                if(World.Plan(actor.Id,node.Id,"MakeRoomAfterReport",180)=="Accepted")break;
        }
        void StopResponse(ResidentResponseState task,string phase)
        {
            if(task.Phase=="Reporting")FinishBodyReport(task,false);
            task.Phase=phase;ReleaseResponseActivity(World.Resident(task.ActorId));
        }
        static void ReleaseResponseActivity(ResidentState actor)
        {
            if(!new[]{InspectBodyActivity,SeekPresenterActivity,ReportActivity}.Contains(actor.Activity))return;
            actor.Phase="Idle";actor.Activity="Rest";actor.ActivityTicks=0;actor.Destination="";actor.Path=Array.Empty<string>();actor.PathCursor=0;
        }
        void ValidateResidentResponses(MansionSessionSnapshot snapshot,MansionIncidentCollection cases,KnowledgeLedger knowledge)
        {
            var tasks=snapshot.ResidentResponses;if(tasks==null||tasks.Length>36||tasks.Any(s=>s==null)||tasks.Select(s=>s.ActorId+"|"+s.CaseId).Distinct().Count()!=tasks.Length||tasks.Where(ResponseActive).GroupBy(s=>s.ActorId).Any(g=>g.Count()>1))throw new InvalidDataException("잘못된 현장 신고 작업입니다.");
            foreach(var task in tasks){
                var incident=cases.Find(task.CaseId);var actor=snapshot.World.Residents.FirstOrDefault(r=>r.Id==task.ActorId);
                if(actor==null||actor.Id=="CH_01"||incident==null||incident.TargetId!=task.TargetId||task.StartedTick<0||task.StartedTick>snapshot.World.Tick||task.NextPlanTick<0||task.NextPlanTick>snapshot.World.Tick+180||task.InspectionTicks<0||task.InspectionTicks>120||task.ScanSteps<0||task.ScanSteps>4||!new[]{"Inspecting","SeekingYusti","Reporting","Reported","Cancelled"}.Contains(task.Phase)||ResponseActive(task)&&(!actor.Alive||!actor.Present))throw new InvalidDataException("현장 신고의 진행 상태가 맞지 않습니다.");
                if(new[]{"SeekingYusti","Reporting","Reported"}.Contains(task.Phase)&&!incident.ReceiptsFor(task.ActorId).Any(r=>r.Id==task.ObservationId&&r.Record.Value=="Collapsed"))throw new InvalidDataException("확인하지 않은 현장을 신고할 수 없습니다.");
                var own=knowledge.For(task.ActorId);
                if(task.PresenterRecordId==null||task.SearchRoom==null||task.PresenterRecordId!=""&&!own.Records().Any(r=>r.Id==task.PresenterRecordId&&r.SubjectId=="PRES_YUSTI"&&r.Predicate=="AtPlace"&&r.Direct&&r.IdentityConfirmed)||task.SearchRoom!=""&&!own.Records().Any(r=>r.Direct&&r.Predicate=="PlaceVisited"&&r.SubjectId==task.ActorId&&r.PlaceId==task.SearchRoom))throw new InvalidDataException("찾아갈 장소에 대한 개인 관측이 없습니다.");
                ValidateConversation(task.Speech,snapshot.World.Tick);
                if((task.Phase=="Reporting")!=(task.Speech.Phase=="Speaking")||task.Speech.Phase!="Idle"&&(task.Speech.SpeakerId!=task.ActorId||task.Speech.RecipientId!="PRES_YUSTI"||task.Speech.Value!="BodyReport:"+task.TargetId)||task.Phase=="Reporting"&&actor.Activity!=ReportActivity||task.Phase=="Inspecting"&&actor.Activity!=InspectBodyActivity)throw new InvalidDataException("신고 발화와 행동 상태가 맞지 않습니다.");
            }
        }
    }
}
