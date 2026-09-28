using System;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.NPC;
using BASSLINE.Trial;
using BASSLINE.Investigation;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        void AdvanceWitnessExaminations()
        {
            var snapshot=court.Capture();
            foreach(var examination in snapshot.Examinations.Where(e=>e.Phase=="AwaitingAnswer")){
                var original=snapshot.Transcript.Single(h=>h.Speech.Claim?.Id==examination.StatementId);var claim=original.Speech.Claim;
                var span=claim.Spans.Single(s=>s.Id==examination.SpanId);
                var statement=new NpcHeardClaim{Id=claim.Id,SpeakerId=claim.OwnerId,ReceiverId=examination.Witness,LoopId=claim.LoopId,Span=new NpcTrialSpan{Id=span.Id,SubjectId=span.SubjectId,Predicate=span.Predicate,Value=span.Value,PlaceId=span.PlaceId,FromTick=span.FromTick,ToTick=span.ToTick,Quantifier=span.Quantifier}};
                string[] refs=proceedings.SpokenRecords.Where(p=>p.StartsWith(original.Speech.Id+"|",StringComparison.Ordinal)).Select(p=>p.Substring(original.Speech.Id.Length+1)).ToArray();
                string id=examination.Id+"_ANSWER";
                var answer=new WitnessAnswerPlanner().Answer(Knowledge.For(examination.Witness),statement,examination.Question,id,refs,NameOf,PlaceLabel,World.ClockVersion);
                var draft=answer.Speech;
                var publicClaim=draft.Spans.Length==0?null:new ClaimRecord{Id="CLAIM_"+id,OwnerId=draft.SpeakerId,LoopId=Knowledge.LoopId,Text=draft.Text,Spans=draft.Spans.Select(s=>new ClaimSpan{Id=s.Id,SubjectId=s.SubjectId,Predicate=s.Predicate,Value=s.Value,PlaceId=s.PlaceId,FromTick=s.FromTick,ToTick=s.ToTick,Quantifier=s.Quantifier}).ToArray()};
                if(court.QueueExaminationAnswer(examination.Id,new SpeechDraft{Id=id,Speaker=draft.SpeakerId,Topic=draft.Topic,Text=draft.Text,Claim=publicClaim},answer.State)=="Queued")
                    proceedings.SpokenRecords=proceedings.SpokenRecords.Concat(draft.RecordIds.Select(record=>id+"|"+record)).ToArray();
            }
        }
    }
}
