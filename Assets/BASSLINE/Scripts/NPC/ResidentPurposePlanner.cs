using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.NPC
{
    public sealed class ResidentPurposePlanner
    {
        public ResidentIntentState Consider(IActorKnowledgeQuery own,ResidentPurposeBinding binding,SocialExperience[] experiences,string id,long tick,uint random,bool registered,IncidentPlanState previous)
        {
            ResidentPurposeDefinition.Validate(binding.Definition);var definition=binding.Definition;
            binding.BasisIds=ResidentPurposeBinding.MatchReasons(definition,own,binding.TargetId,tick);
            if(binding.BasisIds.Length==0||ResidentPurposeBinding.Resolved(binding,own,tick,binding.BasisIds.Select(own.Find).Max(r=>r.ReceivedTick)))return null;
            var known=own.Records().Where(r=>r.LoopId==own.LoopId&&r.Kind!="ArchiveMeta"&&r.ReceivedTick<=tick).ToArray();
            KnownRecord Location(string subject)=>known.Where(r=>r.SubjectId==subject&&r.Direct&&r.IdentityConfirmed&&r.Predicate=="AtPlace"&&r.Value==r.PlaceId&&r.ToTick<=tick+1&&tick-r.ToTick<=definition.FreshnessTicks).OrderByDescending(r=>r.ToTick).FirstOrDefault();
            var targetLocation=Location(binding.TargetId);
            var helper=experiences.Where(e=>e.Owner==own.OwnerId&&e.Other!=binding.TargetId&&e.Other!=own.OwnerId&&e.Tick<=tick).GroupBy(e=>e.Other).Where(g=>g.Sum(e=>e.Trust)>0&&Location(g.Key)!=null).OrderByDescending(g=>g.Sum(e=>e.Trust)).ThenBy(g=>g.Key,StringComparer.Ordinal).Select(g=>g.Key).FirstOrDefault();
            var proof=binding.BasisIds.Select(own.Find).FirstOrDefault(r=>r.Direct);
            var alternatives=definition.Responses.Select(response=>{
                string recipient=response.Action=="Withdraw"?"":response.Action=="AskForHelp"?helper??"":binding.TargetId;
                bool eligible=response.Action=="Withdraw"?true:response.Action=="AskForHelp"?helper!=null:targetLocation!=null;
                if(response.Action=="Disclose")eligible&=proof!=null;
                if(response.Action=="Execute")eligible&=registered&&!ResidentPurposeBinding.Taboo(binding,own,tick)&&definition.AllowsHarm;
                var extra=new[]{Location(recipient)?.Id,response.Action=="Execute"?Location(binding.ObjectId)?.Id:null,response.Action=="Execute"?binding.RuleRecordId:null,response.Action=="Disclose"?proof?.Id:null};
                return new IncidentAlternative{Action=response.Action,TargetId=recipient,Eligible=eligible,GoalBenefit=response.GoalBenefit,RelationshipBenefit=response.RelationshipBenefit,RiskCost=response.RiskCost,TravelCost=response.TravelCost,Reason=response.Reason,BasisIds=binding.BasisIds.Concat(extra.Where(x=>!string.IsNullOrEmpty(x))).Distinct().ToArray()};
            }).ToArray();
            var reasons=binding.BasisIds.Concat(string.IsNullOrEmpty(binding.RuleRecordId)?Array.Empty<string>():new[]{binding.RuleRecordId}).Distinct().ToArray();
            var plan=new IncidentPlanner().Consider(own,new IncidentDecisionInput{Id=id,GoalId=definition.Id,Tick=tick,Revision=(previous?.Revision??0)+1,RandomState=random,KnownReasonIds=reasons,ProposedTarget=binding.TargetId,ObjectId=binding.ObjectId,GoalAllowsHarm=definition.AllowsHarm,TabooApplies=ResidentPurposeBinding.Taboo(binding,own,tick),HasRegisteredAction=registered,FreshnessTicks=definition.FreshnessTicks,Alternatives=alternatives},previous);
            if(plan==null)return null;
            binding.ProofRecordId=plan.Action=="Disclose"?proof?.Id??"":"";
            return new ResidentIntentState{Plan=plan,PurposeId=definition.Id,Purpose=binding.Copy(),SourceExperienceId="PURPOSE_"+id,ContextId=definition.Id,ItemId=binding.ObjectId,RecipientId=plan.TargetId,LocationRecordId=Location(plan.TargetId)?.Id??"",ProofRecordId=binding.ProofRecordId,NextPlanTick=tick,DeadlineTick=tick+definition.OpportunityTicks};
        }
        public string Statement(ResidentIntentState task,IActorKnowledgeQuery own,Func<string,string> name)
        {
            var binding=task.Purpose;var option=binding.Definition.Responses.Single(r=>r.Action==task.Plan.Action);
            var original=own.Find(binding.BasisIds[0]);if(original==null)return "";
            return option.OpeningLine+"\n"+(task.Plan.Action=="AskForHelp"?name(binding.TargetId)+" 씨와 관련해서 ":"")+"제가 받은 내용은 “"+original.Text+"”예요."+(task.Plan.Action=="Disclose"?Disclosure(task,own):" 확인한 범위와 아직 모르는 부분을 나눠서 이야기하고 싶어요.");
        }
        static string Disclosure(ResidentIntentState task,IActorKnowledgeQuery own)
        {
            var proof=own.Find(task.ProofRecordId);return proof==null?"":"\n직접 확인한 자료도 보여 드릴게요. “"+proof.Text+"”";
        }
        public string Reply(ResidentIntentState task,IActorKnowledgeQuery own)
        {
            // Receiving a request is not consent or a claim to know the speaker's private grounds.
            return "무엇 때문에 이야기하려는지는 들었어요. 제가 직접 본 일과 전해 들은 말을 구분해서 생각해 볼게요. 지금 이 말만으로 누가 옳은지는 단정하지 않을게요.";
        }
    }
}
