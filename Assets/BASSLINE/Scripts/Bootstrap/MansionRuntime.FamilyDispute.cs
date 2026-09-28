using System;
using System.IO;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.NPC;
using BASSLINE.Knowledge;
using BASSLINE.Save;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerFamilyDisputePort
    {
        FamilyDisputeState familyDispute=new FamilyDisputeState{Version=0};
        bool FamilySpeaking=>familyDispute.Speech.Phase=="Speaking";
        string FamilyActor=>familyDispute.Step=="IhyunAction"||familyDispute.Step=="Settlement"?"CH_05":"CH_03";
        bool FamilyControls(string actor)=>FamilySpeaking&&(familyDispute.Speech.SpeakerId==actor||familyDispute.Speech.RecipientId==actor)||familyDispute.Phase=="Seeking"&&actor==FamilyActor;
        void InitializeFamilyDispute()
        {
            if(familyDispute.Version!=0)return;
            familyDispute=new FamilyDisputeState{Loop=World.Loop,InstalledTick=World.Tick};
            foreach(string actor in new[]{"CH_03","CH_05"})Knowledge.Observe(actor,new KnownRecord{Kind="PersonalMemory",Source=actor,SubjectId=actor,Predicate="PersonalHistory",Value="FamilyBusinessLoss",ProvenanceKey="FAMILY_MEMORY_L"+World.Loop+"_"+actor,
                Text=actor=="CH_03"?"우리 가족의 사업이 이현 씨가 한 일 때문에 무너졌다. 나는 책임에 대한 답을 원한다.":"내가 한 일로 서윤 씨 가족의 사업이 무너졌다. 이 일에 대한 책임을 요구받을 수 있다.",FromTick=World.Tick,ToTick=World.Tick+1,Supports=new[]{"당사자가 기억하는 자신의 과거"},DoesNotEstablish=new[]{"다른 사람이 직접 확인한 증거가 아님"}},World.Tick);
        }
        bool FamilyAvailable(string actor)=>actor=="CH_01"?conversationPlayback.Phase=="Idle"&&World.CanAct(actor):
            World.CanAct(actor)&&!ResidentToolBusy(actor)&&!RescueControls(actor)&&!incidents.Controls(actor)&&!ResponseControls(actor)&&!ReturnTaskControls(actor)&&!IntentControls(actor)&&!PlayerTalkingTo(actor)&&!ResidentIsSpeaking(actor)&&!HasReceivedCourtSummons(actor)&&Social.Due(actor,World.Tick)==null&&!new[]{"Sleep","Sleeping"}.Contains(World.Resident(actor).Activity);
        void AdvanceFamilyDispute()
        {
            InitializeFamilyDispute();var f=familyDispute;
            if(FamilySpeaking){
                var s=f.Speech;
                if(!World.CanAct(s.SpeakerId)||!World.CanAct(s.RecipientId)||World.Resident(s.SpeakerId).Activity!=IntentTalk||s.RecipientId!="CH_01"&&World.Resident(s.RecipientId).Activity!=IntentListen||!ReceivesSpeech(s.RecipientId,s.SpeakerId)||!CanReach(s.SpeakerId,s.RecipientId,3)||HasReceivedCourtSummons(s.SpeakerId)){FinishFamilySpeech(false);return;}
                SpeechProgress.Advance(s,World.Tick,ReceivesSpeech);if(s.ElapsedTicks>=s.DurationTicks)FinishFamilySpeech(true);return;
            }
            if(new[]{"Completed","Deferred","Resolved"}.Contains(f.Phase)||World.Tick<f.NextPlanTick||proceedings.Phase=="Gathering")return;
            if(!World.CanAct("CH_03")||!World.CanAct("CH_05")){EndFamilyDispute("Deferred","ParticipantUnavailable");return;}
            if(f.Deadline>0&&World.Tick>f.Deadline){EndFamilyDispute("Deferred","ConversationWindowEnded");return;}
            string speaker=FamilyActor,listener=speaker=="CH_05"?"CH_03":"CH_05";
            if(f.Step=="Action"||f.Step=="IhyunAction"){
                var plan=f.Plans.LastOrDefault(p=>p.OwnerId==speaker);if(plan==null){EndFamilyDispute("Deferred","MissingOwnPlan");return;}
                if(plan.Action=="Withdraw"){EndFamilyDispute("Completed","ChoseDistance");return;}
                listener=plan.TargetId;
            }
            if(!FamilyAvailable(speaker)||!FamilyAvailable(listener))return;
            bool sees=CanSee(speaker,listener)&&Identifies(speaker,listener);
            if(sees&&CanReach(speaker,listener,2.8)&&ReceivesSpeech(listener,speaker)){
                if(f.Deadline==0)f.Deadline=World.Tick+12000;
                string step=f.Step=="Action"||f.Step=="IhyunAction"?f.Plans.Last(p=>p.OwnerId==speaker).Action:f.Step;
                BeginFamilySpeech(speaker,listener,step);return;
            }
            // Before first contact, life schedules continue. No globally-known target chasing.
            var location=Knowledge.For(speaker).Records().Where(r=>r.SubjectId==listener&&r.Direct&&r.IdentityConfirmed&&r.Predicate=="AtPlace"&&r.Value==r.PlaceId&&World.Tick-r.ToTick<=3600).OrderByDescending(r=>r.ToTick).FirstOrDefault();
            f.NextPlanTick=World.Tick+180;
            if(location==null||f.Step=="Settlement")return;
            if(f.Deadline==0)f.Deadline=World.Tick+12000;
            if(f.LocationId==location.Id&&World.Resident(speaker).Phase=="Travelling")return;
            if(f.LocationId==location.Id&&World.Resident(speaker).Node==f.Destination){ReleaseFamilyTravel();f.Phase="Waiting";return;}
            foreach(var node in nodes.Where(n=>n.Room==location.PlaceId&&n.Position.Distance(location.Position)<2.8).OrderBy(n=>Math.Abs(n.Position.Distance(location.Position)-1.3)).ThenBy(n=>n.Id,StringComparer.Ordinal)){
                if(World.Plan(speaker,node.Id,"FamilyConversation",180)=="Accepted"){f.LocationId=location.Id;f.Destination=node.Id;f.Phase="Seeking";return;}
            }
        }
        void BeginFamilySpeech(string speaker,string listener,string step)
        {
            ReleaseFamilyTravel();var f=familyDispute;
            var a=World.Resident(speaker);var b=World.Resident(listener);
            string text=FamilyDisputePlanner.Text(step,speaker);if(text=="")return;
            f.Sequence++;f.Attempts++;f.Phase="Speaking";
            var s=new ConversationPlaybackState{Id="FAMILY_L"+World.Loop+"_S"+f.Sequence,Sequence=f.Sequence,StartedTick=World.Tick,SpeakerId=speaker,RecipientId=listener,PlannedText=text,Value=FamilyDisputePlanner.Prefix+step,Phase="Speaking",DurationTicks=Math.Max(360,text.Length*5),ResumePhase=a.Phase,ResumeActivity=a.Activity,ResumeActivityTicks=a.ActivityTicks,ListenerResumePhase=b.Phase,ListenerResumeActivity=b.Activity,ListenerResumeTicks=b.ActivityTicks,Listeners=World.Residents.Where(r=>r.Alive&&r.Present&&r.Id!=speaker).Select(r=>new ConversationListener{ActorId=r.Id}).ToArray()};
            a.Phase="Performing";a.Activity=IntentTalk;a.ActivityTicks=int.MaxValue;
            if(listener!="CH_01"){b.Phase="Performing";b.Activity=IntentListen;b.ActivityTicks=int.MaxValue;}
            // Both are already in physical sight/reach; facing is a real body rotation.
            var d=bodies[listener].transform.position-bodies[speaker].transform.position;d.y=0;
            if(d.sqrMagnitude>.01f){a.Yaw=Quaternion.LookRotation(d).eulerAngles.y;if(listener!="CH_01")b.Yaw=Quaternion.LookRotation(-d).eulerAngles.y;}
            f.Speech=s;World.Emit("FamilySpeechStarted",speaker,listener,s.Id);
        }
        void FinishFamilySpeech(bool complete)
        {
            var f=familyDispute;var s=f.Speech;if(s.Phase!="Speaking")return;
            string root=CommitSpeech(s,complete);s.Committed=true;s.Phase=complete?"Reading":"Interrupted";RestoreIntentSpeechActivity(s);
            bool heard=complete&&root!=""&&s.Listeners.Any(l=>l.ActorId==s.RecipientId&&l.HeardCharacters==s.PlannedText.Length);
            World.Emit(heard?"FamilySpeechReceived":"FamilySpeechInterrupted",s.SpeakerId,s.RecipientId,s.Id);
            if(!heard){f.Phase=f.Attempts<2?"Waiting":"Deferred";f.NextPlanTick=World.Tick+600;f.Outcome="ConversationInterrupted";return;}
            f.Attempts=0;f.Phase="Waiting";f.NextPlanTick=World.Tick+60;
            if(f.Step=="Opening"){
                var p=ChooseFamilyPlan("CH_05");
                if(p==null||p.Action=="Withdraw"){EndFamilyDispute("Completed","ChoseDistance");return;}
                f.Step=p.Action=="Negotiate"?"Settlement":"IhyunAction";
            }else if(f.Step=="Settlement"){
                if(ChooseFamilyPlan("CH_03")==null){EndFamilyDispute("Deferred","MissingReceivedReply");return;}
                f.Step="Action";
            }else{
                Social.Experience(s.RecipientId,s.SpeakerId,s.Value.EndsWith(":AskForHelp",StringComparison.Ordinal)?"MediationRequested":"ExplanationHeard",s.Id,World.Tick);
                EndFamilyDispute("Completed","StatementDelivered");
            }
        }
        IncidentPlanState ChooseFamilyPlan(string actor)
        {
            var random=IntentRandom(actor);var previous=familyDispute.Plans.LastOrDefault(p=>p.OwnerId==actor);
            var plan=FamilyDisputePlanner.Consider(Knowledge.For(actor),Social.Experiences(actor),World.Tick,random.State,previous);
            if(plan!=null){familyDispute.Plans=familyDispute.Plans.Concat(new[]{plan}).ToArray();random.State=plan.RandomAfter;World.Emit("FamilyChoiceSelected",actor,plan.TargetId,plan.Id+"|"+plan.Action);}
            return plan;
        }
        void ReleaseFamilyTravel(){foreach(string actor in new[]{"CH_03","CH_05"})if(World.Resident(actor).Activity=="FamilyConversation")World.StopResidentActivity(actor);}
        void EndFamilyDispute(string phase,string reason){if(FamilySpeaking)FinishFamilySpeech(false);ReleaseFamilyTravel();familyDispute.Phase=phase;familyDispute.Outcome=reason;World.Emit("FamilyConversationEnded","CH_03","CH_05",reason);}
        void InterruptFamilyDispute(string actor){if(FamilySpeaking&&(familyDispute.Speech.SpeakerId==actor||familyDispute.Speech.RecipientId==actor))FinishFamilySpeech(false);if(actor=="CH_03")ReleaseFamilyTravel();}
        public bool CanDiscussFamilyDispute(string actor)=>new[]{"CH_03","CH_05"}.Contains(actor)&&FamilyDisputePlanner.KnowsTopic(Knowledge.For("CH_01"),World.Tick);
        public string DiscussFamilyDispute(string actor,string choice)
        {
            if(!CanDiscussFamilyDispute(actor)||!CanDiscussAppointment(actor))return "Unavailable";
            if(choice=="Ask"){
                bool helped=FamilyDisputePlanner.Received(Knowledge.For(actor),"Mediate","CH_01",World.Tick)!=null;
                string text=helped?"함께 들어 주겠다고 한 말은 기억하고 있어요. 그 자리에서 확인할 수 있는 것부터 이야기해요.":actor=="CH_03"?"우리 가족 일에 대한 답을 듣고 싶어요. 제가 겪은 일이니까요. 그렇다고 다른 사람까지 제 말만 믿으라고 할 수는 없죠.":"서윤 씨가 책임에 대한 답을 요구하고 있어요. 제가 할 수 있는 일부터 직접 이야기하려고 합니다.";
                return BeginConversation(actor,text,"FamilyDispute:Position",420);
            }
            string line=choice=="Mediate"?"내가 함께 들어 줄게. 지금 결론부터 내리지 말고, 서로 책임질 수 있는 일을 차분히 이야기하자.":choice=="Disclose"?"혼자 덮어 두기보다는, 네가 직접 겪은 일을 믿을 수 있는 사람에게 말해 보는 건 어때?":choice=="Distance"?"지금은 서로 거리를 두고 생각하는 게 좋겠어.":"";
            return line==""?"Unavailable":BeginConversation("CH_01",line,FamilyDisputePlanner.Prefix+choice,420,recipient:actor);
        }
        bool FinishFamilyIntervention(ConversationPlaybackState speech,string root)
        {
            if(speech.SpeakerId!="CH_01"||!speech.Value.StartsWith(FamilyDisputePlanner.Prefix,StringComparison.Ordinal))return false;
            string actor=speech.RecipientId;
            if(root==""||!speech.Listeners.Any(l=>l.ActorId==actor&&l.HeardCharacters==speech.PlannedText.Length))return false;
            Social.Experience(actor,"CH_01","FamilyInterventionHeard",speech.Id,World.Tick,0,speech.Value.EndsWith(":Mediate",StringComparison.Ordinal)?1:0);
            var plan=ChooseFamilyPlan(actor);
            // Only this recipient's action changes. An unheard offer never resolves the other participant.
            if(plan!=null){InterruptFamilyDispute(actor);familyDispute.Step=actor=="CH_03"?"Action":"IhyunAction";familyDispute.Phase="Waiting";familyDispute.Attempts=0;familyDispute.Deadline=World.Tick+12000;familyDispute.NextPlanTick=World.Tick+60;}
            ReleaseConversationalActivity();
            string answer=speech.Value.EndsWith(":Mediate",StringComparison.Ordinal)?"함께 들어 준다면 도움이 되겠어요. 당신이 옳고 그름을 대신 정해 달라는 뜻은 아니에요.":speech.Value.EndsWith(":Distance",StringComparison.Ordinal)?"그 말은 들었어요. 당장 답을 내리려고 몰아붙이지는 않을게요.":"그렇게 생각하는군요. 누군가에게 말하더라도 제가 직접 겪은 일과 모르는 일을 나누어 말할게요.";
            return BeginConversation(actor,answer,"FamilyDispute:InterventionReply",420)=="Dialogue";
        }
        void ValidateFamilyDispute(MansionSessionSnapshot session,KnowledgeLedger knowledge)
        {
            var f=session.FamilyDispute;
            if(f!=null&&f.Version==0&&f.Phase=="Waiting"&&f.Plans!=null&&f.Plans.Length==0&&f.Speech?.Phase=="Idle")return;
            if(f==null||f.Version!=1||f.Loop!=session.World.Loop||f.InstalledTick<0||f.InstalledTick>session.World.Tick||f.Plans==null||f.Speech==null||f.Sequence<0||f.Attempts<0||f.Attempts>2||!new[]{"Waiting","Seeking","Speaking","Completed","Deferred","Resolved"}.Contains(f.Phase)||!new[]{"Opening","Settlement","Action","IhyunAction"}.Contains(f.Step))throw new InvalidDataException("가족 갈등 대화 저장이 올바르지 않습니다.");
            ValidateConversation(f.Speech,session.World.Tick);
            if((f.Phase=="Speaking")!=(f.Speech.Phase=="Speaking"))throw new InvalidDataException("갈등 대화의 발화 상태가 다릅니다.");
            foreach(var plan in f.Plans){
                if(plan==null||!new[]{"CH_03","CH_05"}.Contains(plan.OwnerId)||plan.Action=="Execute")throw new InvalidDataException("검토되지 않은 가족 갈등 실행입니다.");
                IncidentPlanIntegrity.Validate(plan,knowledge.For(plan.OwnerId),session.World.Tick);
                if(FamilyDisputePlanner.Received(knowledge.For(plan.OwnerId),plan.OwnerId=="CH_03"?"Settlement":"Opening",plan.OwnerId=="CH_03"?"CH_05":"CH_03",plan.DecisionTick)==null)throw new InvalidDataException("듣지 않은 답변에서 갈등 행동을 선택했습니다.");
            }
            if(f.Speech.Phase!="Idle"&&(!new[]{"CH_03","CH_05"}.Contains(f.Speech.SpeakerId)||!f.Speech.Value.StartsWith(FamilyDisputePlanner.Prefix,StringComparison.Ordinal)||f.Speech.Sequence>f.Sequence))throw new InvalidDataException("갈등 발화의 소유자가 다릅니다.");
        }
    }
}
