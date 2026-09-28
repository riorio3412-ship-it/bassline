using System;
using System.IO;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.NPC;
using BASSLINE.Knowledge;
using BASSLINE.Save;
using BASSLINE.World.Mansion;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        ResidentIntentSnapshot residentIntents=new ResidentIntentSnapshot();
        const string IntentTravel="SeekConversation",IntentTalk="ExplainMisunderstanding",IntentListen="HearExplanation";
        static bool ActiveIntent(ResidentIntentState task)=>new[]{"Selected","Seeking","Speaking","Replying","Executing"}.Contains(task.Phase);
        bool IntentIsSpeaking(string actor)=>residentIntents.Intents.Any(t=>(t.Phase=="Speaking"||t.Phase=="Replying")&&(t.Plan.OwnerId==actor||t.RecipientId==actor));
        bool IntentControls(string actor)=>residentIntents.Intents.Any(t=>ActiveIntent(t)&&((t.Phase=="Speaking"||t.Phase=="Replying")?(t.Plan.OwnerId==actor||t.RecipientId==actor&&actor!="CH_01"):t.Plan.OwnerId==actor&&IntentActorAvailable(actor)));
        bool IntentActorAvailable(string actor)=>actor!="CH_01"&&!FamilyControls(actor)&&World.CanAct(actor)&&!RescueControls(actor)&&!incidents.Controls(actor)&&!ResponseControls(actor)&&!ReturnTaskControls(actor)&&!PlayerTalkingTo(actor)&&!ResidentIsSpeaking(actor)&&!HasReceivedCourtSummons(actor)&&Social.Due(actor,World.Tick)==null&&!new[]{"Sleep","Sleeping"}.Contains(World.Resident(actor).Activity);
        void SelectResidentIntents()
        {
            if(World.Tick%1800!=0||proceedings.Phase=="Gathering")return;
            SelectResidentPurposes();
            foreach(var actor in World.Residents.Where(a=>a.Id!="CH_01"&&a.Alive&&a.Present).OrderBy(a=>a.Id,StringComparer.Ordinal)){
                if(residentIntents.Intents.Any(t=>ActiveIntent(t)&&t.Plan.OwnerId==actor.Id)||!IntentActorAvailable(actor.Id))continue;
                var experiences=Social.Experiences(actor.Id);
                foreach(var experience in experiences.Where(e=>e.Kind=="UnfoundedAccusation"&&!residentIntents.Intents.Any(t=>t.SourceExperienceId==e.Id)).OrderBy(e=>e.Tick)){
                    var random=IntentRandom(actor.Id);
                    string id="INTENT_L"+World.Loop+"_"+(residentIntents.Sequence+1);
                    var task=new ResidentConflictPlanner().Consider(Knowledge.For(actor.Id),experience,experiences,LoanPen,id,World.Tick,random.State);
                    if(task==null)continue;
                    residentIntents.Sequence++;random.State=task.Plan.RandomAfter;residentIntents.Intents=residentIntents.Intents.Concat(new[]{task}).ToArray();
                    World.Emit("ResidentIntentSelected",actor.Id,task.Plan.TargetId,task.Plan.Id+"|"+task.Plan.Action);
                    if(task.Plan.Action=="Withdraw")EndResidentIntent(task,"Withdrawn","ChoseDistance");
                    break;
                }
            }
        }
        void AdvanceResidentIntents()
        {
            SelectResidentIntents();
            foreach(var task in residentIntents.Intents.Where(ActiveIntent).ToArray()){
                string actor=task.Plan.OwnerId;
                if(AdvancePurposeExecution(task))continue;
                bool corrected=!string.IsNullOrEmpty(task.PurposeId)?ResidentPurposeBinding.Resolved(task.Purpose,Knowledge.For(actor),World.Tick,task.Plan.DecisionTick):Social.Experiences(actor).Any(e=>e.Kind=="ClaimCorrected"&&e.Reason==task.ContextId&&e.Tick>=task.Plan.DecisionTick);
                if(corrected){EndResidentIntent(task,"Resolved","CorrectionReceived");continue;}
                if(!World.Resident(actor).Alive||!World.Resident(actor).Present||HasReceivedCourtSummons(actor)){EndResidentIntent(task,"Cancelled","Unavailable");continue;}
                if(World.Tick>=task.DeadlineTick){EndResidentIntent(task,"Withdrawn","ConversationWindowEnded");continue;}
                if(task.Phase=="Speaking"||task.Phase=="Replying"){
                    var speech=task.Phase=="Speaking"?task.Speech:task.Reply;
                    bool connected=ReceivesSpeech(speech.RecipientId,speech.SpeakerId)&&CanReach(speech.SpeakerId,speech.RecipientId,3)&&World.Resident(speech.SpeakerId).Activity==IntentTalk;
                    if(!connected||PlayerTalkingTo(actor)||PlayerTalkingTo(task.RecipientId)||ResponseControls(actor)){FinishIntentUtterance(task,false);continue;}
                    SpeechProgress.Advance(speech,World.Tick,ReceivesSpeech);
                    if(speech.ElapsedTicks>=speech.DurationTicks)FinishIntentUtterance(task,true);
                    continue;
                }
                if(!IntentActorAvailable(actor)){ReleaseIntentActivity(actor);continue;}
                if(task.RecipientId==""||task.Plan.Action=="Withdraw"){EndResidentIntent(task,"Withdrawn","ChoseDistance");continue;}
                bool sees=CanSee(actor,task.RecipientId)&&Identifies(actor,task.RecipientId);
                if(sees&&(!World.Resident(task.RecipientId).Alive||!World.Resident(task.RecipientId).Present)){EndResidentIntent(task,"Withdrawn","ObservedRecipientUnavailable");continue;}
                bool listenerFree=task.RecipientId=="CH_01"?conversationPlayback.Phase=="Idle":IntentActorAvailable(task.RecipientId)&&!IntentControls(task.RecipientId);
                if(sees&&listenerFree&&CanReach(actor,task.RecipientId,3)&&ReceivesSpeech(task.RecipientId,actor)){
                    BeginIntentUtterance(task,false);continue;
                }
                if(World.Tick<task.NextPlanTick)continue;
                var own=Knowledge.For(actor);var location=own.Records().Where(r=>r.SubjectId==task.RecipientId&&r.Direct&&r.IdentityConfirmed&&r.Predicate=="AtPlace"&&r.Value==r.PlaceId).OrderByDescending(r=>r.ToTick).FirstOrDefault();
                if(location==null){EndResidentIntent(task,"Withdrawn","NoKnownLocation");continue;}
                var resident=World.Resident(actor);
                if(location.Id==task.LocationRecordId&&resident.Phase=="Travelling"){task.NextPlanTick=World.Tick+180;continue;}
                if(location.Id==task.LocationRecordId&&task.DestinationNode!=""&&resident.Node==task.DestinationNode&&!sees){EndResidentIntent(task,"Withdrawn","NotAtLastSeenPlace");continue;}
                task.LocationRecordId=location.Id;
                string chosen="";
                foreach(var node in nodes.Where(n=>n.Room==location.PlaceId&&n.Position.Distance(location.Position)<2.8).OrderBy(n=>Math.Abs(n.Position.Distance(location.Position)-1.4)).ThenBy(n=>n.Id,StringComparer.Ordinal))
                    if(World.Plan(actor,node.Id,IntentTravel,180)=="Accepted"){chosen=node.Id;break;}
                if(chosen==""){EndResidentIntent(task,"Withdrawn","KnownRouteUnavailable");continue;}
                task.DestinationNode=chosen;task.Phase="Seeking";task.NextPlanTick=World.Tick+180;
                task.Plan.State="ApproachingConversation";
                task.Plan.Checks=task.Plan.Checks.Concat(new[]{new IncidentPlanCheck{Stage="BeforeTravel",Outcome="Continue",Reason="LastPersonallySeenPosition",Node=chosen,Tick=World.Tick,KnowledgeRevision=own.Revision,BasisIds=new[]{location.Id}}}).ToArray();
            }
        }
        void BeginIntentUtterance(ResidentIntentState task,bool reply)
        {
            string speaker=reply?task.RecipientId:task.Plan.OwnerId,listener=reply?task.Plan.OwnerId:task.RecipientId;
            var own=Knowledge.For(speaker);string shared="",text;
            if(!string.IsNullOrEmpty(task.PurposeId)){
                var planner=new ResidentPurposePlanner();text=reply?planner.Reply(task,own):planner.Statement(task,own,NameOf);
                if(!reply&&task.Plan.Action=="Disclose")shared=task.ProofRecordId;
            }else if(reply){
                var known=own.Records().Where(r=>r.Direct&&r.SubjectId==task.ItemId&&r.Predicate=="SurfaceObjectTransfer").OrderByDescending(r=>r.ReceivedTick).FirstOrDefault();
                text=known==null?"그 말은 들었어요. 다만 제가 직접 확인한 자료가 없어서, 누가 맞는지 단정할 수는 없어요.":"제가 직접 확인한 범위는 여기까지예요. “"+known.Text+"” 관측하지 않은 일까지 확인했다고 말하지는 않을게요.";shared=known?.Id??"";
            }else{text=new ResidentConflictPlanner().Statement(task,own,NameOf);shared=task.Plan.Action=="Disclose"?task.ProofRecordId:"";}
            if(text==""){EndResidentIntent(task,"Withdrawn","MissingSource");return;}
            if(!reply)task.Attempts++;
            ReleaseIntentActivity(task.Plan.OwnerId);
            var speech=new ConversationPlaybackState{Id=task.Plan.Id+(reply?"_REPLY_":"_SPEECH_")+task.Attempts,Sequence=residentIntents.Sequence,StartedTick=World.Tick,Phase="Speaking",SpeakerId=speaker,RecipientId=listener,PlannedText=text,Value="IntentConversation:"+task.Plan.Id+":"+(reply?"Reply":task.Plan.Action),SharedRecordId=shared,DurationTicks=Math.Max(360,text.Length*5),Listeners=World.Residents.Where(r=>r.Alive&&r.Present&&r.Id!=speaker).Select(r=>new ConversationListener{ActorId=r.Id}).ToArray()};
            var from=World.Resident(speaker);speech.ResumePhase=from.Phase;speech.ResumeActivity=from.Activity;speech.ResumeActivityTicks=from.ActivityTicks;
            from.Phase="Performing";from.Activity=IntentTalk;from.ActivityTicks=int.MaxValue;
            if(listener!="CH_01"){
                var to=World.Resident(listener);speech.ListenerResumePhase=to.Phase;speech.ListenerResumeActivity=to.Activity;speech.ListenerResumeTicks=to.ActivityTicks;
                to.Phase="Performing";to.Activity=IntentListen;to.ActivityTicks=int.MaxValue;
            }
            if(reply){task.Reply=speech;task.Phase="Replying";}else{task.Speech=speech;task.Phase="Speaking";}
            World.Emit("ResidentIntentSpeechStarted",speaker,listener,speech.Id);
        }
        void FinishIntentUtterance(ResidentIntentState task,bool completed)
        {
            bool reply=task.Phase=="Replying";var speech=reply?task.Reply:task.Speech;if(speech.Phase!="Speaking")return;
            string root=CommitSpeech(speech,completed);speech.Committed=true;speech.Phase=completed?"Reading":"Interrupted";
            RestoreIntentSpeechActivity(speech);
            bool received=completed&&root!=""&&speech.Listeners.Any(l=>l.ActorId==speech.RecipientId&&l.HeardCharacters==speech.PlannedText.Length);
            World.Emit(received?"ResidentIntentSpeechReceived":"ResidentIntentSpeechInterrupted",speech.SpeakerId,speech.RecipientId,speech.Id);
            if(!received){task.Phase="Seeking";task.NextPlanTick=World.Tick+600;if(reply||task.Attempts>=2)EndResidentIntent(task,"Withdrawn","ConversationInterrupted");return;}
            // Hearing an explanation is an experience, not automatic agreement or exoneration.
            Social.Experience(speech.RecipientId,speech.SpeakerId,reply?"HelpReplyReceived":task.Plan.Action=="AskForHelp"?"MediationRequested":"ExplanationHeard",speech.Id,World.Tick);
            if(!reply&&task.RecipientId!="CH_01"){BeginIntentUtterance(task,true);return;}
            EndResidentIntent(task,"Completed",reply?"AnswerReceived":"ExplanationDelivered");
        }
        void RestoreIntentSpeechActivity(ConversationPlaybackState speech)
        {
            var from=World.Resident(speech.SpeakerId);if(from.Alive&&from.Present&&from.Activity==IntentTalk){from.Phase=speech.ResumePhase;from.Activity=speech.ResumeActivity;from.ActivityTicks=speech.ResumeActivityTicks;}
            if(speech.RecipientId=="CH_01")return;
            var to=World.Resident(speech.RecipientId);if(to.Alive&&to.Present&&to.Activity==IntentListen){to.Phase=speech.ListenerResumePhase;to.Activity=speech.ListenerResumeActivity;to.ActivityTicks=speech.ListenerResumeTicks;}
        }
        void ReleaseIntentActivity(string actor)
        {
            var resident=World.Resident(actor);if(resident.Activity!=IntentTravel)return;
            resident.Phase="Idle";resident.Activity="Rest";resident.ActivityTicks=0;resident.Path=Array.Empty<string>();resident.PathCursor=0;resident.Destination="";resident.QueueDoor="";resident.Recovering=false;resident.RecoveryTicks=0;
            foreach(var door in World.Doors){door.Queue=door.Queue.Where(id=>id!=actor).ToArray();if(door.Holder==actor)door.Holder="";}
        }
        void EndResidentIntent(ResidentIntentState task,string phase,string reason)
        {
            if(task.Phase=="Speaking"||task.Phase=="Replying"){
                var speech=task.Phase=="Speaking"?task.Speech:task.Reply;
                if(speech.Phase=="Speaking"){CommitSpeech(speech,false);speech.Committed=true;speech.Phase="Interrupted";RestoreIntentSpeechActivity(speech);}
            }
            if(PlayerTalkingTo(task.Plan.OwnerId))ForgetCancelledPlanResume(task.Plan.OwnerId);
            ReleaseIntentActivity(task.Plan.OwnerId);task.Phase=phase;task.Outcome=reason;task.CompletedTick=World.Tick;task.Plan.State=phase;
            World.Emit("ResidentIntentEnded",task.Plan.OwnerId,task.RecipientId,task.Plan.Id+"|"+reason);
        }
        void InterruptResidentIntentConversation(string actor)
        {
            foreach(var task in residentIntents.Intents.Where(t=>(t.Phase=="Speaking"||t.Phase=="Replying")&&(t.Plan.OwnerId==actor||t.RecipientId==actor)).ToArray())FinishIntentUtterance(task,false);
        }
        void ValidateResidentIntents(MansionSessionSnapshot session,KnowledgeLedger knowledge)
        {
            var state=session.ResidentIntents;
            if(state!=null)ValidatePurposeReadings(state,session.World.Tick);
            if(state==null||state.Sequence<0||state.Intents==null||state.Random==null||state.Intents.Any(t=>t==null||t.Plan==null)||state.Random.Any(r=>r==null||!bodies.ContainsKey(r.OwnerId)||r.OwnerId=="CH_01"||r.State==0)||state.Intents.Select(t=>t.Plan.Id).Distinct().Count()!=state.Intents.Length||state.Intents.Select(t=>t.SourceExperienceId).Distinct().Count()!=state.Intents.Length||state.Random.Select(r=>r.OwnerId).Distinct().Count()!=state.Random.Length||state.Sequence<state.Intents.Length||state.Intents.Where(ActiveIntent).GroupBy(t=>t.Plan.OwnerId).Any(g=>g.Count()>1))throw new InvalidDataException("개인 행동 계획의 저장 상태가 잘못되었습니다.");
            foreach(var task in state.Intents){
                if(!bodies.ContainsKey(task.Plan.OwnerId)||task.Plan.OwnerId=="CH_01")throw new InvalidDataException("개인 행동 계획의 소유자가 잘못되었습니다.");
                var own=knowledge.For(task.Plan.OwnerId);IncidentPlanIntegrity.Validate(task.Plan,own,session.World.Tick);
                bool purpose=!string.IsNullOrEmpty(task.PurposeId);
                if(purpose)ValidateResidentPurpose(task,session,knowledge);
                var source=session.Social.Experiences.FirstOrDefault(e=>e.Id==task.SourceExperienceId&&e.Owner==task.Plan.OwnerId&&e.Kind=="UnfoundedAccusation"&&e.Reason==task.ContextId);
                if(!purpose&&(source==null||task.Plan.Origin!="Deliberated"||task.Plan.GoalId!="GOAL_PROTECT_OWN_ACCOUNT"||task.Plan.Action=="Execute"||task.ItemId!=LoanPen||task.RecipientId!=task.Plan.TargetId||task.RecipientId!=""&&!bodies.ContainsKey(task.RecipientId)||!task.Plan.KnownReasons.Any(id=>own.Find(id).Predicate=="SaidStatement"&&own.Find(id).Source==source.Other&&own.Find(id).Value=="LoanDiscussion:Accuse:"+task.ContextId&&own.Find(id).ReceivedTick==source.Tick)))throw new InvalidDataException("실제로 인지한 갈등과 행동 계획이 맞지 않습니다.");
                if(task.LocationRecordId==null||task.ProofRecordId==null||task.DestinationNode==null||task.LocationRecordId!=""&&!own.Records().Any(r=>r.Id==task.LocationRecordId&&r.Direct&&r.IdentityConfirmed&&r.SubjectId==task.RecipientId&&r.Predicate=="AtPlace"&&r.Value==r.PlaceId)||task.ProofRecordId!=""&&!own.Records().Any(r=>r.Id==task.ProofRecordId&&r.Direct&&(purpose||r.SubjectId==task.ItemId&&r.Predicate=="SurfaceObjectTransfer"))||task.DestinationNode!=""&&!nodes.Any(n=>n.Id==task.DestinationNode))throw new InvalidDataException("행동 계획의 위치·자료를 직접 확인하지 않았습니다.");
                if(!new[]{"Selected","Seeking","Speaking","Replying","Executing","Withdrawn","Resolved","Completed","Cancelled"}.Contains(task.Phase)||task.DeadlineTick!=task.Plan.DecisionTick+(purpose?task.Purpose.Definition.OpportunityTicks:10*60*60)||task.NextPlanTick<0||task.NextPlanTick>session.World.Tick+600||task.Attempts<0||task.Attempts>2||ActiveIntent(task)&&task.CompletedTick!=-1||!ActiveIntent(task)&&(task.CompletedTick<task.Plan.DecisionTick||task.CompletedTick>session.World.Tick))throw new InvalidDataException("개인 행동의 진행 단계와 시간이 맞지 않습니다.");
                ValidateConversation(task.Speech,session.World.Tick);ValidateConversation(task.Reply,session.World.Tick);
                if((task.Phase=="Speaking")!=(task.Speech.Phase=="Speaking")||(task.Phase=="Replying")!=(task.Reply.Phase=="Speaking"))throw new InvalidDataException("개인 행동과 실제 발화 단계가 다릅니다.");
                foreach(var speech in new[]{task.Speech,task.Reply}.Where(s=>s.Phase!="Idle")){
                    bool reply=ReferenceEquals(speech,task.Reply);string speaker=reply?task.RecipientId:task.Plan.OwnerId,listener=reply?task.Plan.OwnerId:task.RecipientId;
                    if(speech.SpeakerId!=speaker||speech.RecipientId!=listener||speech.Value!="IntentConversation:"+task.Plan.Id+":"+(reply?"Reply":task.Plan.Action)||!speech.Id.StartsWith(task.Plan.Id+(reply?"_REPLY_":"_SPEECH_"),StringComparison.Ordinal)||speech.SharedRecordId!=""&&knowledge.For(speaker).Find(speech.SharedRecordId)==null)throw new InvalidDataException("개인 설명 발화의 참여자·출처가 다릅니다.");
                    if(speech.Phase=="Speaking"&&session.World.Residents.Single(a=>a.Id==speaker).Activity!=IntentTalk)throw new InvalidDataException("발화 중인 인물이 다른 행동을 하고 있습니다.");
                }
                if(task.Reply.Phase!="Idle"&&(task.RecipientId=="CH_01"||!task.Speech.Listeners.Any(l=>l.ActorId==task.RecipientId&&l.HeardCharacters==task.Speech.PlannedText.Length)))throw new InvalidDataException("받지 않은 설명에 답하는 상태입니다.");
                if(task.Phase=="Resolved"&&!purpose&&!session.Social.Experiences.Any(e=>e.Owner==task.Plan.OwnerId&&e.Kind=="ClaimCorrected"&&e.Reason==task.ContextId&&e.Tick>=task.Plan.DecisionTick))throw new InvalidDataException("정정을 받지 않은 계획을 해결 처리할 수 없습니다.");
            }
        }
    }
}
