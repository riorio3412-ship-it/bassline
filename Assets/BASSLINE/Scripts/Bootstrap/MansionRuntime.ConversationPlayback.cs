using System;
using System.IO;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerConversationPlaybackPort
    {
        ConversationPlaybackState conversationPlayback=new ConversationPlaybackState();
        public void FinishListeningQuickly(){if(!World.Paused&&conversationPlayback.Phase=="Speaking")conversationPlayback.FastForward=true;}
        void AdvanceConversationFastBatch()
        {
            // Fast presentation still executes every authoritative 60 Hz world step.
            // Bound work per rendered frame so menus and interruptions stay responsive.
            for(int i=0;i<30&&!World.Paused&&conversationPlayback.Phase=="Speaking";i++)AdvanceOne();
        }
        public ConversationPlaybackView ReadConversationPlayback()
        {
            var s=conversationPlayback;
            return new ConversationPlaybackView{SpeakerId=s.SpeakerId,Phase=s.Phase,HeardText=s.SpeakerId=="CH_01"?s.PlannedText.Substring(0,s.EmittedCharacters):s.Listeners.FirstOrDefault(l=>l.ActorId=="CH_01")?.HeardText??"",EndReason=s.EndReason};
        }
        string BeginConversation(string speaker,string text,string value,int duration,string relationshipKey="",string sharedRecord="",string recipient="",string[] evidenceIds=null)
        {
            string listener=speaker=="CH_01"?recipient:"CH_01";
            if(speaker!="PRES_YUSTI"&&(!World.CanAct(speaker)||RescueControls(speaker))||listener!=""&&(!World.CanAct(listener)||RescueControls(listener)))return "Unavailable";
            if(World.Paused||toolPress.Running||WritingCard||itemExchange.Handoff.Running||PlayerSurfaceRunning||SurfaceRunning&&(ReturnState.Motion.Actor==speaker||ReturnState.Motion.Actor==recipient)||conversationPlayback.Phase=="Speaking"||string.IsNullOrWhiteSpace(text)||!ReceivesSpeech(listener,speaker))return "Unavailable";
            CancelInspection();CancelResidentToolWork(speaker,"ConversationStarted");if(recipient!="")CancelResidentToolWork(recipient,"ConversationStarted");
            InterruptResidentConversation(speaker);if(recipient!="")InterruptResidentConversation(recipient);
            long sequence=conversationPlayback.Sequence+1;
            ReleaseConversationalActivity();
            conversationPlayback=new ConversationPlaybackState{Sequence=sequence,Id="CONV_L"+World.Loop+"_"+sequence,StartedTick=World.Tick,Phase="Speaking",SpeakerId=speaker,PlannedText=text,Value=value,DurationTicks=Math.Max(60,duration),RelationshipKey=relationshipKey,SharedRecordId=sharedRecord,
                RecipientId=recipient,EvidenceIds=evidenceIds??Array.Empty<string>(),Listeners=World.Residents.Where(r=>r.Alive&&r.Present&&r.Id!=speaker).Select(r=>new ConversationListener{ActorId=r.Id}).ToArray()};
            if(bodies.ContainsKey(speaker)){
                var actor=World.Resident(speaker);var s=conversationPlayback;
                s.ResumePhase=actor.Phase;s.ResumeActivity=actor.Activity;s.ResumeActivityTicks=actor.ActivityTicks;
                actor.Phase="Performing";actor.Activity="Talk";actor.ActivityTicks=int.MaxValue;
            }
            if(speaker=="CH_01"){
                var actor=World.Resident(recipient);var s=conversationPlayback;
                s.ListenerResumePhase=actor.Phase;s.ListenerResumeActivity=actor.Activity;s.ListenerResumeTicks=actor.ActivityTicks;
                actor.Phase="Performing";actor.Activity="Listen";actor.ActivityTicks=int.MaxValue;
            }
            StopWaiting("이야기를 듣고 있습니다.");move=default;running=false;return "Dialogue";
        }
        void AdvanceConversation()
        {
            var s=conversationPlayback;if(s.Phase!="Speaking")return;
            var speaker=World.Residents.FirstOrDefault(a=>a.Id==s.SpeakerId);
            if(speaker!=null&&(!speaker.Alive||!speaker.Present||speaker.Activity!="Talk")||s.SpeakerId=="CH_01"&&World.Resident(s.RecipientId).Activity!="Listen"||!ReceivesSpeech(s.SpeakerId=="CH_01"?s.RecipientId:"CH_01",s.SpeakerId)){
                FinishConversation(false,"말소리가 끊겼습니다. 들은 부분만 기록했습니다.");return;
            }
            SpeechProgress.Advance(s,World.Tick,ReceivesSpeech);
            if(s.ElapsedTicks>=s.DurationTicks)FinishConversation(true,"");
        }
        void FinishConversation(bool completed,string reason)
        {
            var s=conversationPlayback;if(s.Phase!="Speaking")return;
            string root=CommitSpeech(s,completed);
            s.Committed=true;s.Phase=completed?"Reading":"Interrupted";s.EndReason=reason;
            if(completed&&FinishFamilyIntervention(s,root))return;
            if(completed&&FinishIncidentPosition(s,root))return;
            if(completed&&FinishAppointmentStatement(s,root))return;
            if(completed&&FinishEverydayStatement(s,root))return;
            if(completed&&s.SpeakerId=="CH_01"&&FinishLoanStatement(s,root))return;
            // The central token is acquired on the exact final world tick, before another tick can run.
            if(ui.Pages.Contains(3))World.Pause("K_DIALOGUE_READ",true);
            if(!completed)ReleaseConversationalActivity();
        }
        string CommitSpeech(ConversationPlaybackState s,bool completed)
        {
            // Commit only the portion actually uttered; all receipts share one causal source key.
            string uttered=s.PlannedText.Substring(0,s.EmittedCharacters);string root="",owner="";
            if(uttered.Length>0){
                owner=bodies.ContainsKey(s.SpeakerId)?s.SpeakerId:s.Listeners.FirstOrDefault(l=>l.HeardCharacters==s.EmittedCharacters)?.ActorId??"";
                if(owner!="")root=ConversationReceipt(s,owner,uttered,completed);
                foreach(var listener in s.Listeners.Where(l=>l.HeardCharacters>0&&l.ActorId!=owner)){
                    if(root!=""&&listener.HeardCharacters==s.EmittedCharacters&&s.SpeakerId!="PRES_YUSTI")Knowledge.Deliver(owner,listener.ActorId,root,World.Tick);
                    else ConversationReceipt(s,listener.ActorId,listener.HeardText,false);
                }
            }
            if(completed&&root!=""){
                if(bodies.ContainsKey(s.SpeakerId))SealFirstStatement(s.SpeakerId,root);
                if(s.SharedRecordId!="")foreach(var listener in s.Listeners.Where(l=>l.HeardCharacters==s.PlannedText.Length))Knowledge.Deliver(s.SpeakerId,listener.ActorId,s.SharedRecordId,World.Tick);
                if(s.RelationshipKey!=""&&s.Listeners.Any(l=>l.ActorId=="CH_01"&&l.HeardCharacters==s.PlannedText.Length)){
                    Social.Experience("CH_01",s.SpeakerId,"EverydayTalk",s.RelationshipKey,World.Tick,0,1);
                    Social.Experience(s.SpeakerId,"CH_01","EverydayTalk",s.RelationshipKey,World.Tick,0,1);
                }
            }
            return root;
        }
        string ConversationReceipt(ConversationPlaybackState s,string owner,string text,bool complete)
        {
            var body=PhysicalBody(s.SpeakerId);var listener=s.Listeners.FirstOrDefault(l=>l.ActorId==owner);
            long from=listener!=null&&listener.FirstHeardTick>=0?listener.FirstHeardTick:s.StartedTick;
            long through=listener!=null&&listener.LastHeardTick>=0?listener.LastHeardTick+1:World.Tick;
            return Knowledge.Observe(owner,new KnownRecord{Kind="Speech",Source=s.SpeakerId,ConversationWith=s.RecipientId!=""?s.RecipientId:"CH_01",SubjectId=s.SpeakerId,Predicate=complete?"SaidStatement":"HeardFragment",Value=s.Value,ProvenanceKey=s.Id,Text=text,PlaceId=body?PlaceOf(body.transform.position):"",Position=body?P(body.transform.position):default,FromTick=from,ToTick=Math.Max(from+1,through),Supports=new[]{complete?"이 인물이 실제로 한 말":"실제로 들은 발언의 일부"},DoesNotEstablish=new[]{"발언 내용의 진실·아직 듣지 못한 말·숨은 의도를 보장하지 않음"}},World.Tick);
        }
        void ReleaseConversationalActivity()
        {
            var s=conversationPlayback;
            if(!bodies.ContainsKey(s.SpeakerId))return;
            var actor=World.Resident(s.SpeakerId);
            if(actor.Alive&&actor.Present&&actor.Activity=="Talk"){
                actor.Phase=s.ResumePhase;actor.Activity=s.ResumeActivity;actor.ActivityTicks=s.ResumeActivityTicks;
            }
            if(s.SpeakerId=="CH_01"&&bodies.ContainsKey(s.RecipientId)){
                var listener=World.Resident(s.RecipientId);if(listener.Alive&&listener.Present&&listener.Activity=="Listen"){
                    listener.Phase=s.ListenerResumePhase;listener.Activity=s.ListenerResumeActivity;listener.ActivityTicks=s.ListenerResumeTicks;
                }
            }
        }
        public void EndConversation()
        {
            if(conversationPlayback.Phase=="Speaking")FinishConversation(false,"대화를 중단했습니다. 들은 부분만 남습니다.");
            ReleaseConversationalActivity();long sequence=conversationPlayback.Sequence;conversationPlayback=new ConversationPlaybackState{Sequence=sequence};World.Pause("K_DIALOGUE_READ",false);
        }
        void ValidateConversation(ConversationPlaybackState s,long tick,bool resident=false)
        {
            if(s==null||s.Sequence<0||s.Listeners==null||!new[]{"Idle","Speaking","Reading","Interrupted"}.Contains(s.Phase))throw new InvalidDataException("잘못된 대화 재생 상태입니다.");
            if(s.Phase=="Idle")return;
            if(resident&&(s.SpeakerId=="CH_01"||s.RecipientId=="CH_01"||s.SpeakerId==s.RecipientId||!bodies.ContainsKey(s.RecipientId)))throw new InvalidDataException("인물 간 대화의 참가자가 잘못되었습니다.");
            if(!targets.ContainsKey(s.SpeakerId)||s.SpeakerId=="CH_01"&&(!bodies.ContainsKey(s.RecipientId)||s.RecipientId=="CH_01"||!(s.Value.StartsWith("FamilyDispute:",StringComparison.Ordinal)||s.Value.StartsWith("LoanDiscussion:",StringComparison.Ordinal)||s.Value=="IncidentPosition:Admit"||s.Value=="IncidentPosition:Deny"||s.Value.StartsWith("AppointmentOffer:",StringComparison.Ordinal)||s.Value.StartsWith("AppointmentChange:",StringComparison.Ordinal)||s.Value.StartsWith("EverydayReply:",StringComparison.Ordinal)))||s.StartedTick<0||s.StartedTick>tick||s.PlannedText==null||s.PlannedText.Length>12000||s.DurationTicks<60||s.DurationTicks>72000||s.ElapsedTicks<0||s.ElapsedTicks>s.DurationTicks||s.EmittedCharacters!=(int)((long)s.PlannedText.Length*s.ElapsedTicks/s.DurationTicks)||s.StartedTick+s.ElapsedTicks>tick||s.Committed==(s.Phase=="Speaking"))throw new InvalidDataException("발화 커서가 유효하지 않습니다.");
            if(s.Listeners.Select(l=>l.ActorId).Distinct().Count()!=s.Listeners.Length||s.Listeners.Any(l=>!bodies.ContainsKey(l.ActorId)||l.ActorId==s.SpeakerId||l.HeardText==null||l.HeardCharacters<0||l.HeardCharacters>s.EmittedCharacters||l.HeardText.Length>24000||l.HeardCharacters>0&&(l.FirstHeardTick<s.StartedTick||l.LastHeardTick<l.FirstHeardTick||l.LastHeardTick>tick)))throw new InvalidDataException("발언 수신 범위가 유효하지 않습니다.");
        }
    }
}
