using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.NPC
{
    // Only received records enter these decisions. No world, case book or culprit lookup.
    public static class IncidentConversation
    {
        public static bool Strike(KnownRecord r)=>r.Predicate=="UsedObject"&&(r.CausalStage=="PhysicalStrike"||(r.ProvenanceKey??"").Contains("_CASE_PLAYER_"));
        public static bool Relevant(KnownRecord r)=>Strike(r)||r.Predicate=="CausedOutcome"||r.Predicate=="ConfirmedDeath"||r.Predicate=="PhysicalCondition"||r.Value=="Collapsed";
        public static KnownRecord Select(IActorKnowledgeQuery own,long since)=>own.Records().Where(r=>r.ReceivedTick>=since&&Relevant(r)).OrderByDescending(r=>r.Direct&&r.IdentityConfirmed&&r.Predicate=="CausedOutcome"?6:r.Direct&&Strike(r)?5:r.Predicate=="ConfirmedDeath"?4:r.Predicate=="PhysicalCondition"&&r.Value=="Stabilized"?3:r.Direct?2:1).ThenByDescending(r=>r.ReceivedTick).FirstOrDefault();
        public static bool SawPlayerStrike(IActorKnowledgeQuery own,long since)=>own.Records().Any(r=>r.ReceivedTick>=since&&r.Direct&&r.IdentityConfirmed&&r.SubjectId=="CH_01"&&Strike(r));
        public static string Greeting(IActorKnowledgeQuery own,long since)
        {
            if(SawPlayerStrike(own,since))return "거기서 멈춰 줘. 네가 사람을 공격하는 걸 봤어. 아무 일 없었던 것처럼 이야기할 수는 없어.";
            var known=Select(own,since);if(known==null)return "";
            if(known.Predicate=="PhysicalCondition"&&known.Value=="Stabilized")return "아까 쓰러졌던 사람은 도움을 받았어. 그래도 무슨 일이 있었는지 알아야 해.";
            if(known.Predicate=="PhysicalCondition")return "움직이지 못하는 사람이 있어. 지금은 그 사람을 돕는 게 먼저야.";
            return "아까 일 때문에 신경이 쓰여. 내가 아는 것부터 이야기할게.";
        }
        public static string Explain(KnownRecord r,Func<string,string> name,bool directQuestion)
        {
            if(r==null)return "아직 그 일을 직접 보거나 전해 들은 게 없어. 모르는 부분을 짐작해서 말하고 싶지는 않아.";
            string actor=r.IdentityConfirmed?name(r.SubjectId):"누군가";
            string target=!string.IsNullOrEmpty(r.OutcomeTarget)&&r.OutcomeTarget!="UNKNOWN_ACTOR"?name(r.OutcomeTarget):"다른 사람";
            if(!r.Direct&&r.Predicate!="ConfirmedDeath")return "직접 본 건 아니야. "+name(r.Source)+"에게 들었어. 그 사람이 말한 내용은 이거야.\n"+r.Text;
            string lead=directQuestion?"응. 내가 그 자리에 있었어. ":"";
            if(Strike(r))return lead+actor+Particle(actor,"이","가")+" "+name(r.Value)+"으로 "+target+Particle(target,"을","를")+" 치는 걸 봤어. 그 뒤의 일까지 전부 봤다는 뜻은 아니야.";
            if(r.Predicate=="CausedOutcome")return lead+actor+"의 행동부터 상대가 쓰러질 때까지 계속 봤어. 중간에 다른 곳을 보지는 않았어.";
            if(r.Predicate=="ConfirmedDeath")return "사망했다는 건 유스티에게 확인받은 내용이야. 누가 무슨 이유로 그랬는지는 이 안내만으로 알 수 없어.";
            if(r.Predicate=="PhysicalCondition")return lead+(r.Value=="Stabilized"?actor+Particle(actor,"이","가")+" 도움을 받고 다시 움직이는 걸 봤어.":actor+Particle(actor,"이","가")+" 움직이지 못하는 모습을 봤어. 누가 그랬는지는 보지 못했어.");
            return lead+"쓰러진 사람을 봤어. 그 전에 무슨 일이 있었는지는 내가 본 범위 밖이야.";
        }
        public static string InVoice(string actor,string text)
        {
            if(!new[]{"CH_03","CH_04","CH_05","CH_06","CH_10","CH_12","CH_14","CH_15","CH_18"}.Contains(actor))return text;
            return text.Replace("내가 ","제가 ").Replace("네가 ","민혁 씨가 ").Replace("네 말","민혁 씨 말")
                .Replace("응. ","네. ").Replace("멈춰 줘.","멈춰 주세요.").Replace("보여 줘.","보여 주세요.")
                .Replace("봤어.","봤어요.").Replace("없어.","없어요.").Replace("있어.","있어요.")
                .Replace("받았어.","받았어요.").Replace("있었어.","있었어요.").Replace("들었어.","들었어요.")
                .Replace("아니야.","아니에요.").Replace("이거야.","이거예요.").Replace("내용이야.","내용이에요.")
                .Replace("뜻은 아니야.","뜻은 아니에요.").Replace("않아.","않아요.").Replace("할게.","할게요.")
                .Replace("해야 해.","해야 해요.").Replace("먼저야.","먼저예요.").Replace("알아야 해.","알아야 해요.")
                .Replace("않았어.","않았어요.").Replace("보자.","봐요.").Replace("밖이야.","밖이에요.");
        }
        static string Particle(string text,string closed,string open)
        {
            if(string.IsNullOrEmpty(text))return closed;char last=text[text.Length-1];
            return last>='가'&&last<='힣'&&(last-'가')%28==0?open:closed;
        }
        public static string Reply(IActorKnowledgeQuery own,long since,bool admit)
        {
            bool saw=SawPlayerStrike(own,since);
            if(admit)return saw?"네가 인정한 말도 기억할게. 하지만 이유와 그 뒤에 있었던 일은 따로 확인해야 해.":"네가 했다는 말은 들었어. 내가 직접 본 건 아니니까, 그 말만으로 모든 일을 단정하지는 않을게.";
            return saw?"나는 네가 공격하는 걸 직접 봤어. 아니라고 말해도 내가 본 일이 없어지지는 않아. 다르게 설명할 부분이 있다면 근거를 보여 줘.":"아니라는 말은 들었어. 하지만 내가 못 봤다는 이유만으로 네 말이 맞다고 할 수도 없어. 확인할 수 있는 것부터 보자.";
        }
    }
}
