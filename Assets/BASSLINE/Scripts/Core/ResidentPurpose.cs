using System;
using System.Linq;
namespace BASSLINE.Core
{
    [Serializable] public sealed class PurposePremise
    {
        public string Predicate="",Value="",SubjectRole="Target",SourceRole="Any",Kind="";
        public bool DirectRequired=true,IdentityRequired;
        public PurposePremise Copy()=>(PurposePremise)MemberwiseClone();
        public bool Matches(KnownRecord record,string owner,string target,long tick)=>record!=null&&record.Kind!="ArchiveMeta"&&record.ReceivedTick<=tick&&record.Predicate==Predicate&&record.Value==Value&&(Kind==""||record.Kind==Kind)&&(!DirectRequired||record.Direct)&&(!IdentityRequired||record.IdentityConfirmed)&&(SubjectRole=="Any"||record.SubjectId==(SubjectRole=="Owner"?owner:target))&&(SourceRole=="Any"||record.Source==target);
    }
    [Serializable] public sealed class PurposeResponse
    {
        public string Action="",Reason="",OpeningLine="";
        public int GoalBenefit,RelationshipBenefit,RiskCost,TravelCost;
        public PurposeResponse Copy()=>(PurposeResponse)MemberwiseClone();
    }
    [Serializable] public sealed class ResidentPurposeDefinition
    {
        public string Id="",Revision="",Source="",MotiveTag="",Title="";
        public bool AllowsHarm;
        public int OpportunityTicks=36000,FreshnessTicks=600;
        public PurposePremise[] Required=Array.Empty<PurposePremise>(),ResolvedBy=Array.Empty<PurposePremise>(),Taboos=Array.Empty<PurposePremise>();
        public PurposeResponse[] Responses=Array.Empty<PurposeResponse>();
        public ResidentPurposeDefinition Copy(){var c=(ResidentPurposeDefinition)MemberwiseClone();c.Required=Required.Select(x=>x.Copy()).ToArray();c.ResolvedBy=ResolvedBy.Select(x=>x.Copy()).ToArray();c.Taboos=Taboos.Select(x=>x.Copy()).ToArray();c.Responses=Responses.Select(x=>x.Copy()).ToArray();return c;}
        public static void Validate(ResidentPurposeDefinition d)
        {
            if(d==null||new[]{d.Id,d.Revision,d.Source,d.MotiveTag,d.Title}.Any(string.IsNullOrWhiteSpace)||d.Required==null||d.Required.Length==0||d.ResolvedBy==null||d.Taboos==null||d.Responses==null||d.OpportunityTicks<1||d.OpportunityTicks>4*24*60*60*60||d.FreshnessTicks<1)throw new ArgumentException("Incomplete authored purpose");
            foreach(var premise in d.Required.Concat(d.ResolvedBy).Concat(d.Taboos))if(premise==null||string.IsNullOrEmpty(premise.Predicate)||string.IsNullOrEmpty(premise.Value)||!new[]{"Owner","Target","Any"}.Contains(premise.SubjectRole)||!new[]{"Target","Any"}.Contains(premise.SourceRole))throw new ArgumentException("Invalid purpose premise");
            // A goal must concern a known person; a global fact alone cannot nominate a target.
            if(!d.Required.Any(p=>p.SubjectRole=="Target"||p.SourceRole=="Target"))throw new ArgumentException("Missing personally known target binding");
            if(d.Responses.Length!=IncidentPlanIntegrity.Actions.Length||d.Responses.Select(r=>r?.Action).Distinct().Count()!=d.Responses.Length||!IncidentPlanIntegrity.Actions.All(a=>d.Responses.Any(r=>r?.Action==a)))throw new ArgumentException("Missing purpose alternatives");
            foreach(var response in d.Responses)if(string.IsNullOrWhiteSpace(response.Reason)||response.GoalBenefit<0||response.GoalBenefit>4||response.RelationshipBenefit< -3||response.RelationshipBenefit>3||response.RiskCost<0||response.RiskCost>4||response.TravelCost<0||response.TravelCost>3||new[]{"Negotiate","Disclose","AskForHelp"}.Contains(response.Action)&&string.IsNullOrWhiteSpace(response.OpeningLine))throw new ArgumentException("Invalid authored purpose response");
        }
    }
    [Serializable] public sealed class ResidentPurposeBinding
    {
        public ResidentPurposeDefinition Definition;
        public string TargetId="",PlateId="",ObjectId="",ToolNode="",ContactNode="",MapVersion="",RuleRecordId="",ProofRecordId="";
        public string[] BasisIds=Array.Empty<string>(),AppliedTaboos=Array.Empty<string>();
        public ResidentPurposeBinding Copy(){var c=(ResidentPurposeBinding)MemberwiseClone();c.Definition=Definition.Copy();c.BasisIds=(string[])BasisIds.Clone();c.AppliedTaboos=(string[])AppliedTaboos.Clone();return c;}
        public static string[] MatchReasons(ResidentPurposeDefinition definition,IActorKnowledgeQuery own,string target,long tick)
        {
            var records=own.Records().Where(r=>r.LoopId==own.LoopId).ToArray();
            var found=definition.Required.Select(p=>records.Where(r=>p.Matches(r,own.OwnerId,target,tick)).OrderByDescending(r=>r.ReceivedTick).ThenBy(r=>r.Id,StringComparer.Ordinal).FirstOrDefault()).ToArray();
            return found.Any(r=>r==null)?Array.Empty<string>():found.Select(r=>r.Id).Distinct().ToArray();
        }
        public static bool Resolved(ResidentPurposeBinding binding,IActorKnowledgeQuery own,long tick,long since)=>binding.Definition.ResolvedBy.Any(p=>own.Records().Any(r=>r.LoopId==own.LoopId&&r.ReceivedTick>=since&&p.Matches(r,own.OwnerId,binding.TargetId,tick)));
        public static bool Taboo(ResidentPurposeBinding binding,IActorKnowledgeQuery own,long tick)=>binding.AppliedTaboos.Length>0||binding.Definition.Taboos.Any(p=>own.Records().Any(r=>r.LoopId==own.LoopId&&p.Matches(r,own.OwnerId,binding.TargetId,tick)));
        public static void Validate(ResidentPurposeBinding binding,IncidentPlanState plan,IActorKnowledgeQuery own,long tick)
        {
            if(binding==null||binding.BasisIds==null||binding.AppliedTaboos==null)throw new ArgumentException("Missing personal purpose binding");
            ResidentPurposeDefinition.Validate(binding.Definition);IncidentPlanIntegrity.Validate(plan,own,tick);
            if(plan.Origin!="Deliberated"||plan.GoalId!=binding.Definition.Id||binding.TargetId==own.OwnerId||string.IsNullOrEmpty(binding.TargetId)||plan.DecisionTick>tick||binding.BasisIds.Length==0||binding.BasisIds.Any(id=>!plan.KnownReasons.Contains(id)))throw new ArgumentException("Purpose is not bound to its original reasons");
            var original=binding.BasisIds.Select(own.Find).ToArray();
            if(original.Any(r=>r==null||r.LoopId!=own.LoopId)||binding.Definition.Required.Any(p=>!original.Any(r=>p.Matches(r,own.OwnerId,binding.TargetId,plan.DecisionTick)))||Resolved(binding,own,plan.DecisionTick,original.Max(r=>r.ReceivedTick)))throw new ArgumentException("Purpose lacked a current known conflict");
            if(plan.Action=="Execute"&&(!binding.Definition.AllowsHarm||Taboo(binding,own,plan.DecisionTick)||plan.TargetId!=binding.TargetId||!plan.RequiredResources.Contains(binding.ObjectId)||string.IsNullOrEmpty(binding.PlateId)||string.IsNullOrEmpty(binding.MapVersion)))throw new ArgumentException("Purpose did not permit its execution");
        }
    }
}
