using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.Trial
{
    [Serializable] public sealed class WitnessExaminationState
    {
        public string Id="",Requester="",Witness="",StatementId="",SpanId="",Question="",QuestionSpeechId="",AnswerSpeechId="",AnswerState="",Phase="QuestionQueued";
        public long RequestedTick,CompletedTick=-1;
        public WitnessExaminationState Copy()=>(WitnessExaminationState)MemberwiseClone();
    }
    public sealed partial class TrialDirector
    {
        public string BeginExamination(string requester,string witness,string question)
        {
            if(state.Phase!="Debate"||state.Focus?.Owner!=requester||!WitnessQuestions.Kinds.Contains(question)||!state.Participants.Contains(witness)||witness==requester)return "Unavailable";
            var original=state.Transcript.FirstOrDefault(h=>h.Speech.Claim?.Id==state.Focus.ClaimId&&h.ReceivedBy.Contains(requester));
            if(original==null||original.Speech.Speaker!=witness)return "WrongWitness";
            if(state.Examinations.Any(e=>e.Phase=="QuestionQueued"||e.Phase=="AnswerQueued"||e.Phase=="AwaitingAnswer"))return "QuestionInProgress";
            string id="EXAM_"+(state.Examinations.Length+1);var session=new WitnessExaminationState{Id=id,Requester=requester,Witness=witness,StatementId=state.Focus.ClaimId,SpanId=state.Focus.SpanId,Question=question,QuestionSpeechId=id+"_QUESTION",RequestedTick=state.CourtTick};
            var speech=new SpeechDraft{Id=session.QuestionSpeechId,Speaker=requester,Topic="진술 확인",Text=WitnessQuestions.Label(question),ExaminationId=id};
            if(QueueSpeech(speech)!="Queued")return "Unavailable";
            PrioritizeSpeech(speech.Id);state.Examinations=state.Examinations.Concat(new[]{session}).ToArray();CancelFocus(requester);return id;
        }
        void PrioritizeSpeech(string id)
        {
            var speech=state.Pending.Single(s=>s.Id==id);var other=state.Pending.Where(s=>s.Id!=id).ToList();other.Insert(state.VoiceCursor==0?0:Math.Min(1,other.Count),speech);state.Pending=other.ToArray();
        }
        public string QueueArgumentResponse(SpeechDraft speech,string claimId,string spanId)
        {
            if(speech==null||speech.Claim!=null||!state.ActiveClaimIds.Contains(claimId)||!state.Transcript.Any(h=>h.Speech.Claim?.Id==claimId&&h.Speech.Claim.Spans.Any(s=>s.Id==spanId)&&h.ReceivedBy.Contains(speech.Speaker)))return "Unavailable";
            var response=speech.Copy();response.CounterClaimId=claimId;response.CounterSpanId=spanId;
            string result=QueueSpeech(response);if(result=="Queued")PrioritizeSpeech(response.Id);return result;
        }
        void UpdateExaminationReceipts(PublicSpeech received)
        {
            var session=state.Examinations.FirstOrDefault(e=>e.Id==received.Speech.ExaminationId);if(session==null)return;
            if(received.Speech.Id==session.QuestionSpeechId){session.Phase=received.ReceivedBy.Contains(session.Witness)?"AwaitingAnswer":"Unheard";if(session.Phase=="Unheard")session.CompletedTick=state.CourtTick;}
            else if(received.Speech.Id==session.AnswerSpeechId){session.Phase="Answered";session.CompletedTick=state.CourtTick;}
        }
        public string QueueExaminationAnswer(string examinationId,SpeechDraft speech,string answerState)
        {
            var session=state.Examinations.FirstOrDefault(e=>e.Id==examinationId);
            if(session==null||session.Phase!="AwaitingAnswer"||speech?.Speaker!=session.Witness||!new[]{"Known","Partial","Unknown","Correction","Refused"}.Contains(answerState))return "Unavailable";
            var answer=speech.Copy();answer.ExaminationId=session.Id;answer.ResponseToClaimId=session.StatementId;
            string result=QueueSpeech(answer);if(result!="Queued")return result;
            PrioritizeSpeech(answer.Id);session.AnswerSpeechId=answer.Id;session.AnswerState=answerState;session.Phase="AnswerQueued";return result;
        }
        public WitnessExaminationState[] ReadExaminations(string viewer)
        {
            if(!state.Participants.Contains(viewer))return Array.Empty<WitnessExaminationState>();
            return state.Examinations.Where(e=>e.Requester==viewer||state.Transcript.Any(h=>h.Speech.Id==e.QuestionSpeechId&&h.ReceivedBy.Contains(viewer))).Select(e=>{
                var visible=e.Copy();bool heard=state.Transcript.Any(h=>h.Speech.Id==e.AnswerSpeechId&&h.ReceivedBy.Contains(viewer));
                if(!heard){visible.AnswerSpeechId="";visible.AnswerState="";if(visible.Phase=="AnswerQueued"||visible.Phase=="Answered")visible.Phase="AwaitingAnswer";}return visible;
            }).ToArray();
        }
        static void ValidateExaminations(TrialSnapshot snapshot)
        {
            if(snapshot.Examinations==null)snapshot.Examinations=Array.Empty<WitnessExaminationState>();
            var sessions=snapshot.Examinations;
            if(sessions.Any(e=>e==null)||sessions.Select(e=>e.Id).Distinct().Count()!=sessions.Length||sessions.Count(e=>new[]{"QuestionQueued","AwaitingAnswer","AnswerQueued"}.Contains(e.Phase))>1)throw new ArgumentException("Invalid examination history");
            var all=snapshot.Pending.Concat(snapshot.Transcript.Select(h=>h.Speech)).ToArray();
            foreach(var e in sessions){
                var statement=snapshot.Transcript.FirstOrDefault(h=>h.Speech.Claim?.Id==e.StatementId);var question=all.FirstOrDefault(s=>s.Id==e.QuestionSpeechId);var answer=all.FirstOrDefault(s=>s.Id==e.AnswerSpeechId);
                if(!snapshot.Participants.Contains(e.Requester)||!snapshot.Participants.Contains(e.Witness)||e.Requester==e.Witness||statement==null||statement.Speech.Speaker!=e.Witness||!statement.ReceivedBy.Contains(e.Requester)||!statement.Speech.Claim.Spans.Any(s=>s.Id==e.SpanId)||!WitnessQuestions.Kinds.Contains(e.Question)||question==null||question.Speaker!=e.Requester||question.ExaminationId!=e.Id||question.Text!=WitnessQuestions.Label(e.Question)||e.RequestedTick<0||e.RequestedTick>snapshot.CourtTick||e.CompletedTick>snapshot.CourtTick||!new[]{"QuestionQueued","AwaitingAnswer","AnswerQueued","Answered","Unheard"}.Contains(e.Phase))throw new ArgumentException("Invalid examination question");
                if(e.AnswerSpeechId!=""&&(answer==null||answer.Speaker!=e.Witness||answer.ExaminationId!=e.Id||answer.ResponseToClaimId!=e.StatementId)||e.Phase=="QuestionQueued"&&!snapshot.Pending.Any(s=>s.Id==e.QuestionSpeechId)||new[]{"AwaitingAnswer","AnswerQueued","Answered"}.Contains(e.Phase)&&!snapshot.Transcript.Any(h=>h.Speech.Id==e.QuestionSpeechId&&h.ReceivedBy.Contains(e.Witness))||e.Phase=="AnswerQueued"&&!snapshot.Pending.Any(s=>s.Id==e.AnswerSpeechId)||e.Phase=="Answered"&&!snapshot.Transcript.Any(h=>h.Speech.Id==e.AnswerSpeechId))throw new ArgumentException("Invalid examination answer reception");
            }
        }
    }
}
