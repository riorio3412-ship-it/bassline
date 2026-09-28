using System;
using System.IO;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.NPC;
using BASSLINE.Trial;
using BASSLINE.Save;
using BASSLINE.Knowledge;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        void AdvanceNpcCounters()
        {
            var snapshot=court.Capture();if(snapshot.Phase!="Debate"||Reconstruction.Phase!="Editing")return;
            // A queued counter is a timed utterance first. Only its real audience receives the
            // resulting review; never copy the NPC's reasoning or undisclosed B into everyone's B.
            foreach(var pending in proceedings.NpcCounters.Where(p=>p.Phase=="Queued"||p.Phase=="AwaitingReview")){
                var spoken=snapshot.Transcript.FirstOrDefault(h=>h.Speech.Id==pending.SpeechId);if(spoken==null)continue;
                pending.Phase="AwaitingReview";if(court.Focused)return;
                var own=Knowledge.For(pending.ActorId);
                if(court.EnterFocus(own,pending.ClaimId,pending.SpanId)!="Focused"){pending.Phase="Closed";continue;}
                pending.RequestId="NPC_REVIEW_"+pending.Id;
                var result=court.Submit(own,pending.RequestId,pending.Action,pending.RuleId,pending.RecordIds);
                pending.RequestId=result.RequestId;
                foreach(string receiver in spoken.ReceivedBy)court.DeliverSubmission(result.RequestId,receiver);
                if(court.Focused)court.CancelFocus(pending.ActorId);
                pending.Phase="Applied";
            }
            if(court.Focused||snapshot.CourtTick%180!=0||proceedings.NpcCounters.Any(p=>p.Phase=="Queued"||p.Phase=="AwaitingReview")||snapshot.Examinations.Any(e=>new[]{"QuestionQueued","AwaitingAnswer","AnswerQueued"}.Contains(e.Phase)))return;
            foreach(string actor in snapshot.Participants.Where(id=>id!="CH_01").OrderBy(id=>id,StringComparer.Ordinal)){
                var own=Knowledge.For(actor);
                foreach(var claim in court.ReadHeardClaims(actor).Where(c=>c.SpeakerId!=actor&&snapshot.ActiveClaimIds.Contains(c.Id)&&c.Review!="CorrectedBySpeaker")){
                    var response=new TrialReasoning().RespondToClaim(own,claim);if(response==null)continue;
                    string basis=actor+"|"+claim.Id+"|"+claim.Span.Id+"|"+response.Action+"|"+response.RuleId+"|"+string.Join(",",response.RecordIds.OrderBy(id=>id,StringComparer.Ordinal));
                    if(proceedings.NpcCounters.Any(p=>p.BasisKey==basis))continue;
                    string id="COUNTER_"+(proceedings.NpcCounters.Length+1);string speechId="SPEECH_"+id;
                    var evidence=response.RecordIds.Select(own.Find).Where(r=>r!=null).ToArray();
                    string text=NameOf(claim.SpeakerId)+" 씨가 한 말 중에 확인할 부분이 있어요. "+response.Explanation+" "+string.Join(" ",evidence.Select(r=>r.Text+" 확인한 범위는 "+WorldTimeLabel.Format(r.FromTick,World.ClockVersion)+"부터 "+WorldTimeLabel.Format(r.ToTick,World.ClockVersion)+"까지예요."));
                    var speech=new SpeechDraft{Id=speechId,Speaker=actor,Topic="진술 대조",Text=text};
                    if(court.QueueArgumentResponse(speech,claim.Id,claim.Span.Id)!="Queued")continue;
                    proceedings.NpcCounters=proceedings.NpcCounters.Concat(new[]{new NpcCounterProgress{Id=id,ActorId=actor,ClaimId=claim.Id,SpanId=claim.Span.Id,SpeechId=speechId,Action=response.Action,RuleId=response.RuleId,RecordIds=(string[])response.RecordIds.Clone(),BasisKey=basis,QueuedTick=snapshot.CourtTick}}).ToArray();
                    proceedings.SpokenRecords=proceedings.SpokenRecords.Concat(response.RecordIds.Select(record=>speechId+"|"+record)).ToArray();return;
                }
            }
        }
        static void ValidateNpcCounters(MansionSessionSnapshot session,KnowledgeLedger knowledge)
        {
            var counters=session.Proceedings.NpcCounters;var court=session.Proceedings.Court;
            if(counters==null||counters.Any(p=>p==null)||counters.Select(p=>p.Id).Distinct().Count()!=counters.Length||counters.Select(p=>p.BasisKey).Distinct().Count()!=counters.Length)throw new InvalidDataException("잘못된 반론 진행 기록입니다.");
            foreach(var p in counters){
                var source=court.Transcript.FirstOrDefault(h=>h.Speech.Claim?.Id==p.ClaimId&&h.ReceivedBy.Contains(p.ActorId));var own=knowledge.For(p.ActorId);
                var speech=court.Pending.Concat(court.DeferredSpeeches).Concat(court.Transcript.Select(h=>h.Speech)).FirstOrDefault(s=>s.Id==p.SpeechId);
                if(!court.Participants.Contains(p.ActorId)||p.ActorId=="CH_01"||source==null||!source.Speech.Claim.Spans.Any(s=>s.Id==p.SpanId)||speech==null||speech.Speaker!=p.ActorId||p.RecordIds==null||p.RecordIds.Length==0||p.RecordIds.Any(id=>own.Find(id)==null)||p.QueuedTick<0||p.QueuedTick>court.CourtTick||!new[]{"Queued","AwaitingReview","Applied","Closed"}.Contains(p.Phase)||!new[]{"Rebut","Support","LimitScope"}.Contains(p.Action)||!new[]{"LR01","LR03"}.Contains(p.RuleId))throw new InvalidDataException("반론의 발언·자료·시간이 맞지 않습니다.");
                if(p.Phase=="Applied"&&!court.Submissions.Any(s=>s.Id==p.RequestId)||p.Phase!="Queued"&&!court.Transcript.Any(h=>h.Speech.Id==p.SpeechId))throw new InvalidDataException("말하기 전에 적용된 반론입니다.");
            }
        }
    }
}
