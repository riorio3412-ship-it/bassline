using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.NPC
{
    // MAIN pp.41,43: responsibility for Seoyun's family's loss; no invented past evidence.
    // This authored conversation has no reviewed lethal grammar. Ordinary disagreement cannot
    // silently become permission to kill or substitute a fixed culprit for deliberation.
    public static class FamilyDisputePlanner
    {
        public const string Prefix="FamilyDispute:";
        public static KnownRecord Received(IActorKnowledgeQuery own,string value,string source,long tick)
            =>own.Records().Where(r=>r.Predicate=="SaidStatement"&&r.Value==Prefix+value&&r.Source==source&&r.ReceivedTick<=tick&&r.Kind!="ArchiveMeta"&&r.LoopId==own.LoopId).OrderByDescending(r=>r.ReceivedTick).FirstOrDefault();
        public static bool KnowsTopic(IActorKnowledgeQuery own,long tick)=>own.Records().Any(r=>r.Predicate=="SaidStatement"&&r.Value.StartsWith(Prefix,StringComparison.Ordinal)&&new[]{"CH_03","CH_05"}.Contains(r.Source)&&r.ReceivedTick<=tick&&r.LoopId==own.LoopId&&r.Kind!="ArchiveMeta");
        public static IncidentPlanState Consider(IActorKnowledgeQuery own,SocialExperience[] experience,long tick,uint random,IncidentPlanState previous=null)
        {
            bool seoyun=own.OwnerId=="CH_03";if(!seoyun&&own.OwnerId!="CH_05")return null;
            string other=seoyun?"CH_05":"CH_03";
            var basis=Received(own,seoyun?"Settlement":"Opening",other,tick);if(basis==null)return null;
            var mediation=Received(own,"Mediate","CH_01",tick);var pressure=Received(own,"Disclose","CH_01",tick);var distance=Received(own,"Distance","CH_01",tick);
            var recent=new[]{mediation,pressure,distance}.Where(r=>r!=null).OrderByDescending(r=>r.ReceivedTick).FirstOrDefault();
            bool calm=recent==mediation&&mediation!=null,back=recent==distance&&distance!=null,push=recent==pressure&&pressure!=null;
            string helper=experience.Where(e=>e.Owner==own.OwnerId&&e.Other!=other&&e.Other!=own.OwnerId&&e.Trust>0&&e.Tick<=tick)
                .Where(e=>own.Records().Any(r=>r.SubjectId==e.Other&&r.Direct&&r.IdentityConfirmed&&r.Predicate=="AtPlace"&&tick-r.ToTick<=3600))
                .OrderByDescending(e=>e.Trust).ThenBy(e=>e.Other,StringComparer.Ordinal).Select(e=>e.Other).FirstOrDefault()??"";
            var reasons=new[]{basis, recent}.Where(r=>r!=null).Select(r=>r.Id).ToArray();
            IncidentAlternative Option(string action,string target,int benefit,int relationship,int risk,bool eligible,string why)=>new IncidentAlternative{Action=action,TargetId=target,GoalBenefit=benefit,RelationshipBenefit=relationship,RiskCost=risk,Eligible=eligible,Reason=why,BasisIds=reasons};
            return new IncidentPlanner().Consider(own,new IncidentDecisionInput{Id="FAMILY_L"+own.LoopId+"_"+own.OwnerId+"_"+(previous==null?1:previous.Revision+1),GoalId=seoyun?"GOAL_FAMILY_ACCOUNTABILITY":"GOAL_LIMIT_PUBLIC_DISCLOSURE",Tick=tick,RandomState=random,Revision=previous==null?1:previous.Revision+1,ProposedTarget=other,KnownReasonIds=reasons,GoalAllowsHarm=false,TabooApplies=true,HasRegisteredAction=false,Alternatives=new[]{
                Option("Withdraw","",back?4:1,back?2:0,0,true,"지금은 거리를 두고 생각한다."),
                Option("AskForHelp",calm?"CH_01":helper,3,calm?3:helper!=""?2:0,0,calm||helper!="","실제로 도움을 제안하거나 신뢰를 쌓은 사람에게 함께 들어 달라고 한다."),
                Option("Disclose",helper,4,push&&seoyun?3:0,push?0:2,seoyun&&helper!="","내가 겪은 일을 아는 범위에서 다른 사람에게 말한다."),
                Option("Negotiate",other,4,1,0,true,seoyun?"당사자에게 책임과 해결 방법을 분명히 답해 달라고 한다.":"당사자와 해결 방법을 먼저 이야기한다."),
                Option("Execute",other,0,0,4,false,"이 갈등에는 폭력을 택할 의사와 검토된 실행·증거 경로가 없다.")}},previous);
        }
        public static string Text(string step,string actor)
        {
            if(step=="Opening")return "이현 씨, 우리 집 일이 당신이 한 일 때문에 무너졌다는 건 저도 알고 있어요. 여기서도 아무 일 없었던 것처럼 지내기는 어려워요. 어떻게 책임질 생각인지 듣고 싶어요.";
            if(step=="Settlement")return "서윤 씨가 왜 저를 찾았는지는 알아요. 다만 여기서 사람들을 모아 이야기하기 전에, 우리 둘이 해결 방법부터 정하면 어떨까요. 지금 당장 약속할 수 있는 범위부터 이야기하죠.";
            if(step=="Negotiate")return actor=="CH_03"?"둘이 이야기하자는 말은 들었어요. 그렇지만 덮어 두자는 약속은 못 해요. 무엇을 책임질 수 있는지 분명히 말해 주세요.":"덮어 달라는 약속을 요구하진 않겠습니다. 지금 제가 할 수 있는 일과 할 수 없는 일을 구분해서 이야기하죠.";
            if(step=="AskForHelp")return actor=="CH_03"?"이현 씨와 우리 가족 일로 이야기하고 있어요. 혼자서 결론을 내리기가 어렵네요. 누가 맞다고 단정하지 말고, 제가 하는 말을 같이 들어 주실 수 있어요?":"서윤 씨와 해결할 일이 있어요. 서로 한 말을 나중에 다르게 기억하지 않도록, 이야기하는 자리에 같이 있어 주실 수 있을까요?";
            if(step=="Disclose")return "제가 겪은 일을 말해 둘게요. 우리 집은 이현 씨가 한 일 때문에 무너졌고, 저는 책임에 대한 답을 요구했어요. 다만 여기서 아직 일어나지 않은 일까지 단정하려는 건 아니에요.";
            if(step=="Acknowledge")return "말은 들었어요. 제가 직접 겪은 일은 아니니까 누구 말이 맞다고 단정하진 않을게요. 함께 듣고 확인할 수 있는 것부터 보죠.";
            return "";
        }
    }
}
