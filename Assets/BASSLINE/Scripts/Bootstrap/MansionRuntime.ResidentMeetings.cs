using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.NPC;
using BASSLINE.Knowledge;
using BASSLINE.Save;
using BASSLINE.World.Mansion;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerResidentMeetingsPort
    {
        List<ConversationPlaybackState> residentConversations=new List<ConversationPlaybackState>();
        bool ResidentIsSpeaking(string actor)=>residentConversations.Any(s=>s.Phase=="Speaking"&&(s.SpeakerId==actor||s.RecipientId==actor));
        bool FreeToSocialize(ResidentState actor)=>actor.Id!="CH_01"&&!FamilyControls(actor.Id)&&World.CanAct(actor.Id)&&!RescueControls(actor.Id)&&!incidents.Controls(actor.Id)&&!ReturnTaskControls(actor.Id)&&!ResponseControls(actor.Id)&&!IntentControls(actor.Id)&&!ResidentIsSpeaking(actor.Id)
            &&!(conversationPlayback.Phase!="Idle"&&(conversationPlayback.SpeakerId==actor.Id||conversationPlayback.RecipientId==actor.Id))
            &&(actor.Phase=="Idle"||actor.Phase=="Performing"&&new[]{"Rest","Read","Tea","Wait","Talk","Game"}.Contains(actor.Activity));
        string ResidentOfferText(string actor,AppointmentSpeech a)
        {
            bool formal=new[]{"CH_04","CH_06","CH_14","CH_18"}.Contains(actor);
            bool polite=formal||new[]{"CH_03","CH_05","CH_10","CH_12","CH_15"}.Contains(actor);
            string activity=a.PlaceId=="R_LIBRARY"?"책 좀 같이 볼":a.PlaceId=="R_DINING"?"잠깐 같이 앉아 있을":a.PlaceId=="R_GREEN"||a.PlaceId=="R_GARDEN"?"같이 한 바퀴 걸을":a.PlaceId=="R_ARCADE"?"같이 한 판 할":"잠깐 이야기할";
            return AppointmentWhen(a.StartTick)+"에 "+PlaceLabel(a.PlaceId)+"에서 "+activity+(formal?"까요?":polite?"래요?":"래?");
        }
        void PlanResidentMeetings()
        {
            if(World.Paused||proceedings.Phase=="Gathering")return;
            var venues=InvitationPlaceIds();var planner=new ResidentMeetingPlanner();
            foreach(var actor in World.Residents.OrderBy(a=>a.Id,StringComparer.Ordinal)){
                if(actor.NextSocialTick>World.Tick||actor.Phase!="Idle"||!FreeToSocialize(actor))continue;
                // A reply interrupted by the player's conversation can be resumed at a later real encounter.
                var pending=Social.For(actor.Id,World.Tick).Where(a=>a.Invitee==actor.Id&&a.State=="Proposed"&&a.StartTick>World.Tick+720).OrderBy(a=>a.StartTick).FirstOrDefault();
                if(pending!=null){
                    var original=Social.Capture().Appointments.Single(a=>a.Id==pending.Id&&a.Revision==pending.Revision);
                    if(original.Organizer!="CH_01"&&FreeToSocialize(World.Resident(original.Organizer))&&CanSee(actor.Id,original.Organizer)&&ReceivesSpeech(actor.Id,original.Organizer)&&ReceivesSpeech(original.Organizer,actor.Id)&&Knowledge.For(actor.Id).Records().Any(r=>r.RootId==original.ProposalRecordId))BeginResidentReply(original);
                    continue;
                }
                string place=PlaceOf(bodies[actor.Id].transform.position);if(!venues.Contains(place))continue;
                var nearby=World.Residents.Where(other=>other.Id!=actor.Id&&other.NextSocialTick<=World.Tick&&FreeToSocialize(other)&&PlaceOf(bodies[other.Id].transform.position)==place&&CanSee(actor.Id,other.Id)&&ReceivesSpeech(actor.Id,other.Id)&&ReceivesSpeech(other.Id,actor.Id)).Select(a=>a.Id).ToArray();
                var intent=planner.Choose(actor.Id,place,actor.CompletedActivities,World.Tick,Knowledge.For(actor.Id),Social.For(actor.Id,World.Tick),Social.Experiences(actor.Id),nearby);
                if(intent==null)continue;
                var payload=new AppointmentSpeech{Stage="Offer",PlaceId=intent.PlaceId,StartTick=intent.StartTick,Duration=intent.Duration};
                BeginResidentSpeech(intent.Organizer,intent.Invitee,ResidentOfferText(intent.Organizer,payload),OfferValue(payload),payload);
            }
        }
        void BeginResidentSpeech(string speaker,string listener,string text,string value,AppointmentSpeech payload)
        {
            if(!FreeToSocialize(World.Resident(speaker))||!FreeToSocialize(World.Resident(listener))||!ReceivesSpeech(listener,speaker)||!ReceivesSpeech(speaker,listener))return;
            var from=World.Resident(speaker);var to=World.Resident(listener);
            var s=new ConversationPlaybackState{Id="RES_L"+World.Loop+"_"+World.Tick+"_"+speaker,Sequence=World.Tick,StartedTick=World.Tick,Phase="Speaking",SpeakerId=speaker,RecipientId=listener,PlannedText=text,Value=value,DurationTicks=360,Appointment=payload,
                ResumePhase=from.Phase,ResumeActivity=from.Activity,ResumeActivityTicks=from.ActivityTicks,ListenerResumePhase=to.Phase,ListenerResumeActivity=to.Activity,ListenerResumeTicks=to.ActivityTicks,
                Listeners=World.Residents.Where(a=>a.Id!=speaker&&a.Alive&&a.Present).Select(a=>new ConversationListener{ActorId=a.Id}).ToArray()};
            from.Phase=to.Phase="Performing";from.Activity="Talk";to.Activity="Listen";from.ActivityTicks=to.ActivityTicks=int.MaxValue;
            from.NextSocialTick=to.NextSocialTick=World.Tick+10*60*60;
            residentConversations.Add(s);World.Emit("ResidentSpeechStarted",speaker,listener,s.Id);
        }
        void ReleaseResidentSpeech(ConversationPlaybackState s)
        {
            var a=World.Resident(s.SpeakerId);var b=World.Resident(s.RecipientId);
            if(a.Alive&&a.Present&&a.Activity=="Talk"){a.Phase=s.ResumePhase;a.Activity=s.ResumeActivity;a.ActivityTicks=s.ResumeActivityTicks;}
            if(b.Alive&&b.Present&&b.Activity=="Listen"){b.Phase=s.ListenerResumePhase;b.Activity=s.ListenerResumeActivity;b.ActivityTicks=s.ListenerResumeTicks;}
        }
        void InterruptResidentConversation(string actor)
        {
            InterruptFamilyDispute(actor);InterruptResidentIntentConversation(actor);
            foreach(var task in residentResponses.Where(t=>t.ActorId==actor&&ResponseActive(t)).ToArray()){
                if(task.Phase=="Reporting")FinishBodyReport(task,false);
                if(task.Phase=="Inspecting")StopResponse(task,"Cancelled");
                else ReleaseResponseActivity(World.Resident(actor));
            }
            foreach(var s in residentConversations.Where(s=>s.Phase=="Speaking"&&(s.SpeakerId==actor||s.RecipientId==actor)).ToArray())FinishResidentSpeech(s,false);
        }
        void AdvanceResidentConversations()
        {
            foreach(var s in residentConversations.Where(s=>s.Phase=="Speaking").ToArray()){
                var a=World.Resident(s.SpeakerId);var b=World.Resident(s.RecipientId);
                if(!a.Alive||!a.Present||!b.Alive||!b.Present||a.Activity!="Talk"||b.Activity!="Listen"||!ReceivesSpeech(b.Id,a.Id)||!ReceivesSpeech(a.Id,b.Id)){FinishResidentSpeech(s,false);continue;}
                SpeechProgress.Advance(s,World.Tick,ReceivesSpeech);
                if(s.ElapsedTicks>=s.DurationTicks)FinishResidentSpeech(s,true);
            }
            residentConversations.RemoveAll(s=>s.Phase!="Speaking"&&World.Tick-s.StartedTick-s.ElapsedTicks>480);
        }
        void FinishResidentSpeech(ConversationPlaybackState s,bool completed)
        {
            if(s.Phase!="Speaking")return;
            string root=CommitSpeech(s,completed);s.Committed=true;s.Phase=completed?"Reading":"Interrupted";ReleaseResidentSpeech(s);
            World.Emit(completed?"ResidentSpeechCompleted":"ResidentSpeechInterrupted",s.SpeakerId,s.RecipientId,s.Id);
            if(!completed){World.Resident(s.SpeakerId).NextSocialTick=World.Tick+600;World.Resident(s.RecipientId).NextSocialTick=World.Tick+600;return;}
            if(root==""||!s.Listeners.Any(l=>l.ActorId==s.RecipientId&&l.HeardCharacters==s.PlannedText.Length))return;
            var p=s.Appointment;
            if(p.Stage=="Offer"){
                string id=Social.Propose(s.SpeakerId,s.RecipientId,p.PlaceId,p.StartTick,p.Duration,World.Tick);if(id=="Unavailable")return;
                Social.RecordSpokenProposal(id,1,root,World.Tick);World.Emit("InvitationReceived",s.SpeakerId,s.RecipientId,id);
                BeginResidentReply(Social.Capture().Appointments.Single(a=>a.Id==id));
            }else{
                Social.RecordSpokenReply(p.AppointmentId,p.Revision,root,World.Tick,p.Outcome=="Accepted");World.Emit("InvitationAnswered",s.SpeakerId,s.RecipientId,p.AppointmentId+":"+p.Outcome);
            }
        }
        string ResidentReplyText(string actor,AppointmentSpeech p)=>ReplyText(actor,p).Replace(", 민혁아","").Replace("민혁아, ","").Replace("민혁 씨, ","");
        void BeginResidentReply(AppointmentRevision original)
        {
            bool conflict=Social.For(original.Invitee,World.Tick).Any(a=>a.Id!=original.Id&&a.State=="Agreed"&&a.StartTick<original.StartTick+original.Duration&&original.StartTick<a.StartTick+a.Duration);
            var decision=new ResidentConversation().EvaluateInvitation(original.Invitee,ResidentMeetingPlanner.Activity(original.PlaceId),original.PlaceId,Social.Experiences(original.Invitee),conflict,inviterId:original.Organizer);
            var p=new AppointmentSpeech{Stage="Reply",AppointmentId=original.Id,Revision=original.Revision,PlaceId=original.PlaceId,StartTick=original.StartTick,Duration=original.Duration,Outcome=original.StartTick<=World.Tick+360?"Declined":decision.Outcome};
            BeginResidentSpeech(original.Invitee,original.Organizer,ResidentReplyText(original.Invitee,p),ReplyValue(p),p);
        }
        void PlanMeetingVisit(ResidentState actor,AppointmentView appointment)
        {
            var room=Layout.Room(appointment.PlaceId);if(!room||room.WalkNode<0)return;
            var center=Layout.NavigationNodes[room.WalkNode].Position;
            // Two residents need distinct physical destinations; reserving one shared node prevents the second arrival.
            foreach(var node in Layout.NavigationNodes.Where(n=>n.RoomId==room.RoomId&&Vector3.Distance(n.Position,center)<=1.3f).OrderBy(n=>Vector3.Distance(n.Position,center)).ThenBy(n=>n.Id,StringComparer.Ordinal))
                if(World.Plan(actor.Id,node.Id,"Wait",1800)=="Accepted")return;
        }
        public AmbientSpeechView ReadAmbientSpeech()
        {
            var received=residentConversations.Concat(new[]{familyDispute.Speech}).Concat(residentResponses.Select(t=>t.Speech)).Concat(residentIntents.Intents.SelectMany(t=>new[]{t.Speech,t.Reply})).Select(s=>new{s,heard=s.Listeners.FirstOrDefault(l=>l.ActorId=="CH_01")}).Where(x=>x.heard!=null&&x.heard.HeardCharacters>0&&World.Tick-x.heard.LastHeardTick<=480).OrderByDescending(x=>x.heard.LastHeardTick).ThenBy(x=>x.s.Id,StringComparer.Ordinal).FirstOrDefault();
            return received==null?new AmbientSpeechView():new AmbientSpeechView{SpeakerId=received.s.SpeakerId,HeardText=received.heard.HeardText};
        }
        public string AskResidentPlans(string actor)
        {
            if(!CanDiscussAppointment(actor))return "Unavailable";
            var a=Social.For(actor,World.Tick).Where(a=>a.StartTick+a.Duration>World.Tick&&a.State!="Declined"&&a.State!="Met").OrderBy(a=>a.StartTick).FirstOrDefault();
            bool formal=new[]{"CH_04","CH_06","CH_14","CH_18"}.Contains(actor);bool polite=formal||new[]{"CH_03","CH_05","CH_10","CH_12","CH_15"}.Contains(actor);
            if(a==null)return BeginConversation(actor,formal?"아직 정해 둔 약속은 없습니다. 하던 일부터 마치려고 합니다.":polite?"아직 정해 둔 약속은 없어요. 하던 일부터 끝내려고요.":"아직 정해 둔 약속은 없어. 하던 일부터 끝내려고.","ResidentPlansNone",360);
            string other=a.Organizer==actor?a.Invitee:a.Organizer;
            var original=Social.Capture().Appointments.Single(x=>x.Id==a.Id&&x.Revision==a.Revision);string source=original.WrittenRecordId!=""?original.WrittenRecordId:original.ProposalRecordId;
            var own=Knowledge.For(actor).Records().FirstOrDefault(r=>r.RootId==source);
            string text=NameOf(other)+"하고 "+AppointmentWhen(a.StartTick)+"에 "+PlaceLabel(a.PlaceId)+"에서 만나기로 "+(formal?"이야기했습니다.":polite?"얘기했어요.":"얘기했어.")+(a.State=="Proposed"?(formal?" 아직 서로 괜찮다고 확인한 것은 아닙니다.":polite?" 아직 서로 괜찮다고 확인한 건 아니에요.":" 아직 서로 괜찮다고 확인한 건 아니야."):"");
            return BeginConversation(actor,text,"ResidentPlans:"+a.Id+":"+a.Revision,540,sharedRecord:own?.Id??"");
        }
        void ValidateResidentConversations(MansionSessionSnapshot snapshot,KnowledgeLedger knowledge)
        {
            var items=snapshot.ResidentConversations;var world=snapshot.World;
            if(items==null||items.Length>40||items.Any(s=>s==null)||items.Select(s=>s.Id).Distinct().Count()!=items.Length||world.Residents.Any(a=>a.NextSocialTick<0||a.NextSocialTick>world.Tick+24L*60*60*60))throw new InvalidDataException("인물 간 대화 상태가 잘못되었습니다.");
            var participants=items.Where(s=>s.Phase=="Speaking").SelectMany(s=>new[]{s.SpeakerId,s.RecipientId}).ToArray();
            if(participants.Distinct().Count()!=participants.Length)throw new InvalidDataException("한 인물이 동시에 두 대화에 참여하고 있습니다.");
            foreach(var s in items){
                ValidateConversation(s,world.Tick,true);var p=s.Appointment;
                if(p==null||!new[]{"Offer","Reply"}.Contains(p.Stage)||s.DurationTicks!=360||p.Duration!=6000||!InvitationPlaceIds().Contains(p.PlaceId)||p.StartTick<=s.StartedTick||s.SharedRecordId!=""||s.EvidenceIds.Length!=0||s.FastForward||!world.Events.Any(e=>e.Type=="ResidentSpeechStarted"&&e.Actor==s.SpeakerId&&e.Target==s.RecipientId&&e.Detail==s.Id&&e.Tick==s.StartedTick))throw new InvalidDataException("실제로 시작한 인물 간 약속 발화가 아닙니다.");
                if(p.Stage=="Offer"){
                    if(p.Revision!=0||p.AppointmentId!=""||p.Outcome!=""||s.Value!=OfferValue(p)||s.PlannedText!=ResidentOfferText(s.SpeakerId,p))throw new InvalidDataException("인물이 제안한 약속과 발화가 다릅니다.");
                }else{
                    var a=snapshot.Social.Appointments.SingleOrDefault(a=>a.Id==p.AppointmentId&&a.Revision==p.Revision);
                    if(a==null||a.Invitee!=s.SpeakerId||a.Organizer!=s.RecipientId||a.PlaceId!=p.PlaceId||a.StartTick!=p.StartTick||!a.ReceivedBy.Contains(s.SpeakerId)||s.Value!=ReplyValue(p)||s.PlannedText!=ResidentReplyText(s.SpeakerId,p)||!new[]{"Accepted","CounterOffer","Declined"}.Contains(p.Outcome)||!knowledge.For(s.SpeakerId).Records().Any(r=>r.RootId==a.ProposalRecordId&&r.ReceivedTick<=s.StartedTick))throw new InvalidDataException("받지 않은 약속에 대한 답변입니다.");
                }
                if(s.Phase=="Speaking"&&(world.Residents.Single(a=>a.Id==s.SpeakerId).Activity!="Talk"||world.Residents.Single(a=>a.Id==s.RecipientId).Activity!="Listen"))throw new InvalidDataException("진행 중인 인물 간 대화와 활동이 다릅니다.");
            }
        }
    }
}
