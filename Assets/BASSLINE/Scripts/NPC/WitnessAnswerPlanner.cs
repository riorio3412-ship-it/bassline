using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.NPC
{
    // Receives a heard claim and only this witness's B. Repetition cannot manufacture a memory,
    // certainty, a culprit, or an independent source. Names/places are presentation adapters.
    public sealed class WitnessAnswerPlanner
    {
        public WitnessAnswerDraft Answer(IActorKnowledgeQuery own,NpcHeardClaim statement,string question,string speechId,string[] originalRefs,Func<string,string> person,Func<string,string> place,int clockVersion)
        {
            if(own==null||statement?.Span==null||statement.SpeakerId!=own.OwnerId||statement.LoopId!=own.LoopId||!WitnessQuestions.Kinds.Contains(question))throw new ArgumentException("Witness needs their own public statement and an explicit question");
            var span=statement.Span;
            var available=own.Records().Where(r=>r.LoopId==own.LoopId&&r.FromTick<span.ToTick&&span.FromTick<r.ToTick&&r.SubjectId==span.SubjectId&&r.Predicate==span.Predicate).ToArray();
            var original=(originalRefs??Array.Empty<string>()).Select(own.Find).Where(r=>r!=null&&available.Any(a=>a.Id==r.Id)).ToArray();
            var record=original.OrderByDescending(r=>r.Direct).ThenBy(r=>r.Id,StringComparer.Ordinal).FirstOrDefault()
                ??available.OrderByDescending(r=>r.Direct).ThenBy(r=>r.Id,StringComparer.Ordinal).FirstOrDefault();
            var reply=new NpcTrialSpeech{Id=speechId,SpeakerId=own.OwnerId,Topic=WitnessQuestions.Label(question),Text="그 부분은 제가 확인하지 못했어요. 추측해서 답하지 않을게요."};
            var result=new WitnessAnswerDraft{Speech=reply};if(record==null)return result;
            bool observed=record.Direct&&(record.Kind=="Visual"||record.Kind=="Video"||record.Kind=="Inspection");
            bool limited=record.FromTick>span.FromTick||record.ToTick<span.ToTick||!record.IdentityConfirmed||span.Quantifier!="Particular";
            string bounds=record.DoesNotEstablish.Length==0?"제가 확인한 범위 밖의 일은 알 수 없어요.":string.Join(" ",record.DoesNotEstablish);
            string origin=observed?(record.Kind=="Video"?"촬영 기록에서 확인했어요. 그 현장에 직접 있었던 건 아니에요.":"제가 그 장면을 직접 확인했어요."):
                record.Direct?"직접 들은 말이에요. 그 말의 내용까지 직접 본 건 아니에요.":person(record.Source)+"에게 전달받았어요. 내용 자체를 직접 확인한 건 아니에요.";
            switch(question){
                case "When":reply.Text="제 기록에 남은 범위는 "+WorldTimeLabel.Format(record.FromTick,clockVersion)+"부터 "+WorldTimeLabel.Format(record.ToTick,clockVersion)+"까지예요. 그 전후까지 계속 보고 있었다는 뜻은 아니에요.";break;
                case "Where":reply.Text=string.IsNullOrEmpty(record.PlaceId)?"그 장소를 특정할 만큼 확인하지는 못했어요.":place(record.PlaceId)+"에서 확인한 내용이에요. "+origin;break;
                case "Direct":reply.Text=origin+" "+bounds;break;
                case "Source":reply.Text=record.Direct?origin:person(record.Source)+"에게 받은 내용이에요. 원래 장면을 본 사람인지는 이 기록만으로는 확정할 수 없어요.";break;
                case "Conditions":reply.Text=(record.IdentityConfirmed?"대상의 신원을 확인한 기록이에요. ":"누구였는지까지는 확인하지 못했어요. ")+origin+" "+bounds;break;
                case "Compare":reply.Text=record.Value!=span.Value?"아까 말한 내용과 제 기록이 달라요. 정정할게요. "+record.Text:limited?"아까 말이 넓게 들렸다면 범위를 바로잡을게요. "+record.Text+" "+bounds:"아까 말한 내용은 이 기록에 근거했어요. "+record.Text+" "+bounds;break;
            }
            result.State=question=="Compare"&&(record.Value!=span.Value||limited)?"Correction":limited?"Partial":"Known";
            // The original source is reused even for repeated questions/corrections.
            reply.RecordIds=new[]{record.Id};reply.Spans=new[]{new NpcTrialSpan{Id=speechId+"_SPAN_01",SubjectId=record.SubjectId,Predicate=record.Predicate,Value=record.Value,PlaceId=record.PlaceId,FromTick=record.FromTick,ToTick=record.ToTick,Quantifier="Particular"}};
            return result;
        }
    }
}
