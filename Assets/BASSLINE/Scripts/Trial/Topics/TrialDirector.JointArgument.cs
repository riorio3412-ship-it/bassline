using System;
using System.Linq;
using System.Globalization;
using BASSLINE.Core;
using BASSLINE.Investigation;
namespace BASSLINE.Trial
{
    [Serializable] public sealed class JointArgumentPart
    {
        public string Id="",Actor="",SharedRecordId="",RootId="",SourceRecordId="",State="Draft",Decision="";
        public string RequestId="",AnswerId="",SpeechId="",WithdrawalId="",Reason="";
        public long CompletedTick=-1;
        public JointArgumentPart Copy()=>(JointArgumentPart)MemberwiseClone();
    }
    [Serializable] public sealed class JointArgumentState
    {
        public string Id="",Owner="",Phase="Draft",Disclosure="FullRecord",ClosingId="";
        public int Sequence;
        public long CreatedTick;
        public JointArgumentPart[] Parts=Array.Empty<JointArgumentPart>();
        public JointArgumentState Copy()=>new JointArgumentState{Id=Id,Owner=Owner,Phase=Phase,Disclosure=Disclosure,ClosingId=ClosingId,Sequence=Sequence,CreatedTick=CreatedTick,Parts=Parts.Select(p=>p.Copy()).ToArray()};
    }
    public sealed partial class TrialDirector
    {
        public bool JointArgumentBusy=>state.JointArguments.Any(j=>new[]{"Requesting","Ready","Speaking"}.Contains(j.Phase));
        JointArgumentState LatestJoint(string owner)=>state.JointArguments.LastOrDefault(j=>j.Owner==owner);
        public string ToggleJointProof(IActorKnowledgeQuery own,string recordId)
        {
            if(own==null||own.LoopId!=state.LoopId||state.Phase!="Debate"||Focused||!state.Participants.Contains(own.OwnerId))return "Unavailable";
            var record=own.Find(recordId);
            // Candidates must be actual received copies. Matching a private NPC record alone is not sharing.
            if(record==null||record.Direct||record.Parents.Length==0||record.Source==own.OwnerId||!state.Participants.Contains(record.Source))return "NotShared";
            var j=LatestJoint(own.OwnerId);
            if(j==null||j.Phase=="Completed"||j.Phase=="Cancelled"){
                if(JointArgumentBusy)return "Wait";
                j=new JointArgumentState{Id="JOINT_"+(state.JointArguments.Length+1),Owner=own.OwnerId,CreatedTick=state.CourtTick};
                state.JointArguments=state.JointArguments.Concat(new[]{j}).ToArray();
            }
            if(j.Phase!="Draft")return "Wait";
            if(j.Parts.Any(p=>p.SharedRecordId==recordId)){j.Parts=j.Parts.Where(p=>p.SharedRecordId!=recordId).ToArray();return "Removed";}
            if(j.Parts.Length>=3)return "Limit";
            if(j.Parts.Any(p=>p.RootId==record.RootId))return "SameRoot";
            j.Parts=j.Parts.Concat(new[]{new JointArgumentPart{Id=j.Id+"_PART_"+(++j.Sequence),Actor=record.Source,SharedRecordId=record.Id,RootId=record.RootId}}).ToArray();return "Added";
        }
        public string MoveJointProofFirst(string owner,string recordId)
        {
            var j=LatestJoint(owner);if(j==null||j.Phase!="Draft")return "Unavailable";var part=j.Parts.FirstOrDefault(p=>p.SharedRecordId==recordId);if(part==null)return "Unavailable";
            j.Parts=new[]{part}.Concat(j.Parts.Where(p=>p!=part)).ToArray();return "Changed";
        }
        public string ToggleJointDisclosure(string owner)
        {
            var j=LatestJoint(owner);if(j==null||j.Phase!="Draft")return "Unavailable";
            j.Disclosure=j.Disclosure=="FullRecord"?"ScopeOnly":"FullRecord";return "Changed";
        }
        public string RequestJointConsent(string owner)
        {
            var j=LatestJoint(owner);if(state.Phase!="Debate"||Focused||j==null||j.Phase!="Draft"||j.Parts.Length==0||JointArgumentBusy||state.Examinations.Any(e=>new[]{"QuestionQueued","AwaitingAnswer","AnswerQueued"}.Contains(e.Phase)))return "Unavailable";
            foreach(var p in j.Parts)p.State="NotAsked";j.Phase="Requesting";return "Requested";
        }
        public string QueueJointRequest(string owner,string partId,string text)
        {
            var j=LatestJoint(owner);var p=j?.Parts.FirstOrDefault(x=>x.Id==partId);
            if(j?.Phase!="Requesting"||p?.State!="NotAsked"||Focused)return "Unavailable";
            string id=p.Id+"_REQUEST";if(QueueSpeech(new SpeechDraft{Id=id,Speaker=owner,Topic="함께 설명할 자료",Text=text})!="Queued")return "Unavailable";
            p.RequestId=id;p.State="RequestQueued";PrioritizeSpeech(id);return "Queued";
        }
        public string AnswerJointRequest(IActorKnowledgeQuery own,string jointId,string partId,string decision,string reason,string text)
        {
            var j=state.JointArguments.FirstOrDefault(x=>x.Id==jointId);var p=j?.Parts.FirstOrDefault(x=>x.Id==partId);
            if(j?.Phase!="Requesting"||p?.State!="AwaitingAnswer"||own==null||own.OwnerId!=p.Actor||own.LoopId!=state.LoopId||!new[]{"Agreed","Refused","Deferred"}.Contains(decision)||!state.Transcript.Any(h=>h.Speech.Id==p.RequestId&&h.ReceivedBy.Contains(own.OwnerId)))return "Unavailable";
            var source=own.Records().FirstOrDefault(r=>r.RootId==p.RootId);
            if(decision=="Agreed"&&source==null)return "MissingRecord";
            string id=p.Id+"_ANSWER";if(QueueSpeech(new SpeechDraft{Id=id,Speaker=p.Actor,Topic="공개 범위에 대한 답",Text=text})!="Queued")return "Unavailable";
            p.SourceRecordId=decision=="Agreed"?source.Id:"";p.Decision=decision;p.Reason=reason;p.AnswerId=id;p.State="AnswerQueued";PrioritizeSpeech(id);return "Queued";
        }
        public string StartJointArgument(string owner)
        {
            var j=LatestJoint(owner);if(state.Phase!="Debate"||Focused||j?.Phase!="Ready"||!j.Parts.Any(p=>p.State=="Agreed"))return "Unavailable";
            j.Phase="Speaking";return "Started";
        }
        public string QueueJointContribution(IActorKnowledgeQuery own,string jointId,string partId,string text)
        {
            var j=state.JointArguments.FirstOrDefault(x=>x.Id==jointId);var p=j?.Parts.FirstOrDefault(x=>x.Id==partId);
            if(j?.Phase!="Speaking"||p?.State!="Agreed"||own==null||own.OwnerId!=p.Actor||own.LoopId!=state.LoopId||Focused||j.Parts.Any(x=>x.State=="Speaking"||x.State=="Withdrawing")||j.Parts.FirstOrDefault(x=>x.State=="Agreed")!=p)return "Unavailable";
            var record=own.Find(p.SourceRecordId);if(record==null||record.RootId!=p.RootId)return "MissingRecord";
            string id=p.Id+"_EXPLANATION";ClaimRecord claim=null;
            if(j.Disclosure=="FullRecord"&&new[]{"AtPlace","PassedDoor","HeldObject","UsedObject","DoorState","HeardSound","SaidStatement","ReceivedInformation","CausedOutcome"}.Contains(record.Predicate))
                claim=new ClaimRecord{Id="CLAIM_"+id,OwnerId=p.Actor,LoopId=own.LoopId,Text=text,Spans=new[]{new ClaimSpan{Id=id+"_SPAN",SubjectId=record.SubjectId,Predicate=record.Predicate,Value=record.Value,PlaceId=record.PlaceId,FromTick=record.FromTick,ToTick=record.ToTick,Quantifier="Particular"}}};
            if(QueueSpeech(new SpeechDraft{Id=id,Speaker=p.Actor,Topic="공유한 자료 설명",Text=text,Claim=claim})!="Queued")return "Unavailable";
            p.SpeechId=id;p.State="Speaking";PrioritizeSpeech(id);return "Queued";
        }
        public string QueueJointClosing(string owner,string text)
        {
            var j=LatestJoint(owner);if(j?.Phase!="Speaking"||Focused||j.ClosingId!=""||j.Parts.Any(p=>p.State=="Speaking"||p.State=="Withdrawing"||p.State=="Agreed"))return "Unavailable";
            string id=j.Id+"_CLOSING";if(QueueSpeech(new SpeechDraft{Id=id,Speaker=owner,Topic="함께 확인한 범위",Text=text})!="Queued")return "Unavailable";j.ClosingId=id;PrioritizeSpeech(id);return "Queued";
        }
        void UpdateJointReceipts(PublicSpeech speech)
        {
            foreach(var j in state.JointArguments){
                foreach(var p in j.Parts){
                    if(p.State=="RequestQueued"&&p.RequestId==speech.Speech.Id)p.State=speech.ReceivedBy.Contains(p.Actor)?"AwaitingAnswer":"Unheard";
                    if(p.State=="AnswerQueued"&&p.AnswerId==speech.Speech.Id)p.State=speech.ReceivedBy.Contains(j.Owner)?p.Decision:"Unheard";
                    if(p.State=="Speaking"&&p.SpeechId==speech.Speech.Id){p.State="Spoke";p.CompletedTick=speech.CourtTick;}
                    if(p.State=="Withdrawing"&&p.WithdrawalId==speech.Speech.Id)p.State="Withdrawn";
                }
                if(j.Phase=="Requesting"&&j.Parts.All(p=>new[]{"Agreed","Refused","Deferred","Unheard"}.Contains(p.State)))j.Phase="Ready";
                if(j.Phase=="Speaking"&&j.ClosingId==speech.Speech.Id)j.Phase="Completed";
            }
        }
        // Never promote an interrupted prefix into the original claim or its full evidence receipt.
        void CancelJointSpeeches(string[] ids)
        {
            var current=state.Pending.FirstOrDefault();if(current!=null&&ids.Contains(current.Id)){
                if(state.VoiceCursor>0){var offsets=StringInfo.ParseCombiningCharacters(current.Text);int end=state.VoiceCursor>=offsets.Length?current.Text.Length:offsets[state.VoiceCursor];
                    state.Transcript=state.Transcript.Concat(new[]{new PublicSpeech{Speech=new SpeechDraft{Id=current.Id+"_FRAGMENT",Speaker=current.Speaker,Topic=current.Topic,Text=current.Text.Substring(0,end)},ReceivedBy=(string[])state.ActiveListeners.Clone(),CourtTick=state.CourtTick}}).ToArray();}
                state.VoiceCursor=0;state.ActiveListeners=Array.Empty<string>();
            }
            state.Pending=state.Pending.Where(s=>!ids.Contains(s.Id)).ToArray();state.DeferredSpeeches=state.DeferredSpeeches.Where(s=>!ids.Contains(s.Id)).ToArray();
        }
        public string WithdrawJointParticipation(string actor,string jointId,string partId,string reason)
        {
            var j=state.JointArguments.FirstOrDefault(x=>x.Id==jointId);var p=j?.Parts.FirstOrDefault(x=>x.Id==partId);
            if(state.Phase!="Debate"||Focused||j==null||p==null||p.Actor!=actor||!new[]{"Ready","Speaking"}.Contains(j.Phase)||!new[]{"Agreed","Speaking"}.Contains(p.State))return "Unavailable";
            string id=p.Id+"_WITHDRAWAL";
            if(QueueSpeech(new SpeechDraft{Id=id,Speaker=actor,Topic="함께 설명하기 중단",Text="이 자료에 관한 제 설명은 여기서 멈추겠습니다. 아직 말하지 않은 내용은 제 설명으로 받아들이지 말아 주세요. 이미 전달한 자료와 말한 부분은 그대로 남습니다."})!="Queued")return "Unavailable";
            CancelJointSpeeches(new[]{p.SpeechId});p.WithdrawalId=id;p.Reason=reason;p.State="Withdrawing";PrioritizeSpeech(id);return "Queued";
        }
        public string StopJointArgument(string owner)
        {
            var j=LatestJoint(owner);if(j==null||j.Phase=="Cancelled"||j.Phase=="Completed"||state.Phase!="Debate")return "Unavailable";
            if(Focused){if(state.Focus.Owner!=owner)return "Unavailable";CancelFocus(owner);}
            var ids=j.Parts.SelectMany(p=>new[]{p.RequestId,p.AnswerId,p.SpeechId,p.WithdrawalId}).Concat(new[]{j.ClosingId}).Where(id=>id!="").ToArray();
            CancelJointSpeeches(ids);
            foreach(var p in j.Parts.Where(p=>new[]{"Draft","NotAsked","RequestQueued","AwaitingAnswer","AnswerQueued","Agreed","Speaking","Withdrawing"}.Contains(p.State))){p.State="Stopped";p.Reason="OwnerStopped";}
            j.Phase="Cancelled";return "Stopped";
        }
        static void ValidateJointArguments(TrialSnapshot s)
        {
            if(s.JointArguments==null)s.JointArguments=Array.Empty<JointArgumentState>();
            if(s.JointArguments.Any(j=>j==null)||s.JointArguments.Select(j=>j.Id).Distinct().Count()!=s.JointArguments.Length||s.JointArguments.Count(j=>new[]{"Requesting","Ready","Speaking"}.Contains(j.Phase))>1)throw new ArgumentException("Invalid joint arguments");
            foreach(var j in s.JointArguments){
                Id(j.Id);if(!s.Participants.Contains(j.Owner)||j.CreatedTick<0||j.CreatedTick>s.CourtTick||!new[]{"Draft","Requesting","Ready","Speaking","Completed","Cancelled"}.Contains(j.Phase)||!new[]{"FullRecord","ScopeOnly"}.Contains(j.Disclosure)||j.Parts==null||j.Parts.Length>3||j.Parts.Any(p=>p==null)||j.Parts.Select(p=>p.Id).Distinct().Count()!=j.Parts.Length||j.Parts.Select(p=>p.RootId).Distinct().Count()!=j.Parts.Length)throw new ArgumentException("Invalid joint plan");
                var all=s.Pending.Concat(s.DeferredSpeeches).Concat(s.Transcript.Select(h=>h.Speech)).ToArray();
                foreach(var p in j.Parts){
                    Id(p.Id);Id(p.RootId);Id(p.SharedRecordId);if(!s.Participants.Contains(p.Actor)||p.Actor==j.Owner||!new[]{"Draft","NotAsked","RequestQueued","AwaitingAnswer","AnswerQueued","Agreed","Refused","Deferred","Unheard","Speaking","Spoke","Skipped","Stopped","Withdrawing","Withdrawn"}.Contains(p.State))throw new ArgumentException("Invalid joint participant");
                    bool Heard(string id,string actor)=>s.Transcript.Any(h=>h.Speech.Id==id&&h.ReceivedBy.Contains(actor));
                    if(new[]{"AwaitingAnswer","AnswerQueued","Agreed","Refused","Deferred","Speaking","Spoke","Skipped"}.Contains(p.State)&&!Heard(p.RequestId,p.Actor))throw new ArgumentException("Joint request was not received");
                    if(new[]{"Agreed","Refused","Deferred","Speaking","Spoke","Skipped"}.Contains(p.State)&&!Heard(p.AnswerId,j.Owner))throw new ArgumentException("Joint consent was not received");
                    if(new[]{"Agreed","Speaking","Spoke","Skipped"}.Contains(p.State)&&(p.Decision!="Agreed"||string.IsNullOrEmpty(p.SourceRecordId)))throw new ArgumentException("Joint contribution lacks consent");
                    if(p.State=="Spoke"&&!s.Transcript.Any(h=>h.Speech.Id==p.SpeechId&&h.Speech.Speaker==p.Actor&&h.CourtTick==p.CompletedTick))throw new ArgumentException("Joint contribution was not spoken");
                    if(p.State=="Withdrawing"&&!s.Pending.Any(h=>h.Id==p.WithdrawalId&&h.Speaker==p.Actor)||p.State=="Withdrawn"&&!s.Transcript.Any(h=>h.Speech.Id==p.WithdrawalId&&h.Speech.Speaker==p.Actor))throw new ArgumentException("Missing joint withdrawal speech");
                    if(p.State=="RequestQueued"&&!s.Pending.Any(x=>x.Id==p.RequestId&&x.Speaker==j.Owner)||p.State=="AnswerQueued"&&!s.Pending.Any(x=>x.Id==p.AnswerId&&x.Speaker==p.Actor)||p.State=="Speaking"&&!s.Pending.Any(x=>x.Id==p.SpeechId&&x.Speaker==p.Actor))throw new ArgumentException("Missing queued joint speech");
                    if(all.Any(x=>x.Id==p.RequestId&&(x.Speaker!=j.Owner||x.Claim!=null)||x.Id==p.AnswerId&&(x.Speaker!=p.Actor||x.Claim!=null)||x.Id==p.SpeechId&&(x.Speaker!=p.Actor||j.Disclosure=="ScopeOnly"&&x.Claim!=null)))throw new ArgumentException("Joint disclosure mismatch");
                }
                if(j.Phase=="Completed"&&!s.Transcript.Any(h=>h.Speech.Id==j.ClosingId&&h.Speech.Speaker==j.Owner))throw new ArgumentException("Missing joint closing");
            }
        }
    }
}
