using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.NPC
{
    // First production motive binding: protect one's account after personally hearing an
    // unfounded accusation. This authored goal does not authorize harm or infer violent intent.
    public sealed class ResidentConflictPlanner
    {
        public ResidentIntentState Consider(IActorKnowledgeQuery own,SocialExperience experience,SocialExperience[] experiences,string itemId,string planId,long tick,uint random)
        {
            if(own==null||experience?.Owner!=own.OwnerId||experience.Kind!="UnfoundedAccusation"||experience.Tick>tick)return null;
            var records=own.Records().Where(r=>r.LoopId==own.LoopId&&r.Kind!="ArchiveMeta"&&r.ReceivedTick<=tick).ToArray();
            var accusation=records.FirstOrDefault(r=>r.Predicate=="SaidStatement"&&r.Source==experience.Other&&r.Value=="LoanDiscussion:Accuse:"+experience.Reason&&r.ReceivedTick==experience.Tick);
            if(accusation==null)return null;
            if(experiences.Any(e=>e.Owner==own.OwnerId&&e.Other==experience.Other&&e.Reason==experience.Reason&&e.Kind=="ClaimCorrected"&&e.Tick>=experience.Tick))return null;
            KnownRecord Location(string actor)=>records.Where(r=>r.Direct&&r.IdentityConfirmed&&r.SubjectId==actor&&r.Predicate=="AtPlace"&&r.Value==r.PlaceId&&tick-r.ToTick<=10*60*60).OrderByDescending(r=>r.ToTick).FirstOrDefault();
            var otherPosition=Location(experience.Other);
            var proof=records.Where(r=>r.Direct&&r.SubjectId==itemId&&r.Predicate=="SurfaceObjectTransfer").OrderByDescending(r=>r.ReceivedTick).FirstOrDefault();
            var helper=experiences.Where(e=>e.Owner==own.OwnerId&&e.Other!=experience.Other&&e.Other!=own.OwnerId&&e.Tick<=tick).GroupBy(e=>e.Other).Where(g=>g.Sum(e=>e.Trust)>0&&Location(g.Key)!=null).OrderByDescending(g=>g.Sum(e=>e.Trust)).ThenBy(g=>g.Key,StringComparer.Ordinal).Select(g=>g.Key).FirstOrDefault();
            int trust=experiences.Where(e=>e.Owner==own.OwnerId&&e.Other==experience.Other&&e.Tick<=tick).Sum(e=>e.Trust);
            var reasonIds=new[]{accusation.Id};
            IncidentAlternative Option(string action,string target,bool eligible,int benefit,int relationship,int risk,string why,params string[] basis)=>new IncidentAlternative{Action=action,TargetId=target,Eligible=eligible,GoalBenefit=benefit,RelationshipBenefit=relationship,RiskCost=risk,TravelCost=0,Reason=why,BasisIds=basis.Where(id=>!string.IsNullOrEmpty(id)).Distinct().ToArray()};
            var alternatives=new[]{
                Option("Withdraw","",true,1,0,0,"지금은 거리를 두고 하던 일로 돌아간다.",accusation.Id),
                Option("Negotiate",experience.Other,otherPosition!=null,3,1,trust< -2?3:1,"단정한 말의 근거를 직접 묻는다.",accusation.Id,otherPosition?.Id),
                Option("Disclose",experience.Other,otherPosition!=null&&proof!=null,4,0,0,"내가 실제로 확인한 자료를 설명한다.",accusation.Id,proof?.Id,otherPosition?.Id),
                Option("AskForHelp",helper??"",helper!=null,3,1,1,"도움을 받은 경험이 있는 사람에게 확인을 부탁한다.",accusation.Id,helper==null?null:Location(helper)?.Id),
                Option("Execute",experience.Other,false,0,0,4,"이 갈등의 목적은 설명과 정정이다. 위해 행동은 목적에 맞지 않는다.",accusation.Id)
            };
            var plan=new IncidentPlanner().Consider(own,new IncidentDecisionInput{Id=planId,GoalId="GOAL_PROTECT_OWN_ACCOUNT",Tick=tick,RandomState=random,KnownReasonIds=reasonIds,ProposedTarget=experience.Other,ObjectId=itemId,GoalAllowsHarm=false,HasRegisteredAction=false,Alternatives=alternatives});
            if(plan==null)return null;
            var position=plan.TargetId==""?null:Location(plan.TargetId);
            return new ResidentIntentState{Plan=plan,SourceExperienceId=experience.Id,ContextId=experience.Reason,ItemId=itemId,RecipientId=plan.TargetId,LocationRecordId=position?.Id??"",ProofRecordId=plan.Action=="Disclose"?proof?.Id??"":"",NextPlanTick=tick,DeadlineTick=tick+10*60*60};
        }
        public string Statement(ResidentIntentState task,IActorKnowledgeQuery own,Func<string,string> name)
        {
            var original=own.Find(task.Plan.KnownReasons[0]);if(original==null)return "";
            if(task.Plan.Action=="AskForHelp")return name(original.Source)+" 씨에게 이런 말을 들었어요. “"+original.Text+"” 제가 직접 확인한 것과 모르는 부분을 구분하고 싶어요. 이 물건에 관해 직접 본 게 있나요?";
            if(task.Plan.Action=="Disclose"){
                var proof=own.Find(task.ProofRecordId);if(proof==null)return "";
                return "아까 한 말 때문에 다시 이야기하고 싶어요. 제가 확인한 건 이 기록이에요. “"+proof.Text+"” 이 자료가 확인하는 범위부터 같이 봐 주세요. 제가 하지 않은 일까지 단정하지는 말아 주세요.";
            }
            return "아까 “"+original.Text+"”라고 했죠. 무엇을 직접 보고 한 말인지 듣고 싶어요. 확인한 사실과 추측을 나눠서 이야기해 줄래요?";
        }
    }
}
