using System;
using System.Linq;
namespace BASSLINE.Core
{
    // Authored content describes a game action; it does not assign a culprit or create evidence.
    [Serializable] public sealed class IncidentExecutionDefinition
    {
        public string Id="",Revision="",Source="",GrammarId="",ActionId="ContactOutcome",ReviewStatus="Authored";
        public string PublicRule="",IntentLine="",ContactObservation="",ContinuousObservation="",TraceObservation="",TraceValue="";
        public string[] MotiveTags=Array.Empty<string>(),TabooTags=Array.Empty<string>(),AssetIds=Array.Empty<string>(),VariantIds=Array.Empty<string>();
        public int ContactTicks=60,IntentTicks=180,DelayTicks=600,RescueTicks=120,RiskNoticeTicks=120;
        public IncidentExecutionDefinition Copy(){var c=(IncidentExecutionDefinition)MemberwiseClone();c.MotiveTags=(string[])MotiveTags.Clone();c.TabooTags=(string[])TabooTags.Clone();c.AssetIds=(string[])AssetIds.Clone();c.VariantIds=(string[])VariantIds.Clone();return c;}
        public static void Validate(IncidentExecutionDefinition d)
        {
            if(d==null||string.IsNullOrWhiteSpace(d.Id)||string.IsNullOrWhiteSpace(d.Revision)||string.IsNullOrWhiteSpace(d.Source)||!Enumerable.Range(1,12).Select(i=>"IG"+i.ToString("00")).Contains(d.GrammarId)||d.ActionId!="ContactOutcome"||!new[]{"Authored","Reviewed"}.Contains(d.ReviewStatus))throw new ArgumentException("Unregistered incident definition");
            if(new[]{d.PublicRule,d.IntentLine,d.ContactObservation,d.ContinuousObservation}.Any(string.IsNullOrWhiteSpace)||d.MotiveTags==null||d.MotiveTags.Length==0||d.TabooTags==null||d.AssetIds==null||d.AssetIds.Length==0||d.VariantIds==null||d.VariantIds.Length<3||d.MotiveTags.Concat(d.TabooTags).Concat(d.AssetIds).Concat(d.VariantIds).Any(string.IsNullOrWhiteSpace))throw new ArgumentException("Incomplete incident content");
            if(d.ContactTicks<1||d.IntentTicks<1||d.RiskNoticeTicks<1||d.DelayTicks<2||d.RescueTicks<1||d.RescueTicks>=d.DelayTicks||d.DelayTicks>36000)throw new ArgumentException("Unbounded contact outcome or missing intervention window");
        }
    }
    [Serializable] public sealed class IncidentExecutionBinding
    {
        public IncidentExecutionDefinition Definition;
        public string PurposeId="";
        public ResidentPurposeBinding Purpose;
        public string RuleRecordId="",MotiveTag="",MapVersion="",ToolNode="",ContactNode="";
        public string[] AppliedTaboos=Array.Empty<string>();
        public long BoundTick;
        public IncidentExecutionBinding Copy()=>new IncidentExecutionBinding{Definition=Definition?.Copy(),PurposeId=PurposeId??"",Purpose=string.IsNullOrEmpty(PurposeId)?null:Purpose?.Copy(),RuleRecordId=RuleRecordId,MotiveTag=MotiveTag,MapVersion=MapVersion,ToolNode=ToolNode,ContactNode=ContactNode,AppliedTaboos=(string[])AppliedTaboos.Clone(),BoundTick=BoundTick};
        public static void Validate(IncidentExecutionBinding binding,IncidentPlanState plan,IActorKnowledgeQuery own,long tick)
        {
            if(binding==null||plan==null||own==null)throw new ArgumentException("Missing execution binding");
            IncidentExecutionDefinition.Validate(binding.Definition);IncidentPlanIntegrity.Validate(plan,own,tick);
            var d=binding.Definition;
            if(!string.IsNullOrEmpty(binding.PurposeId)){
                ResidentPurposeBinding.Validate(binding.Purpose,plan,own,tick);
                if(binding.PurposeId!=plan.GoalId||binding.Purpose.Definition.MotiveTag!=binding.MotiveTag||binding.Purpose.RuleRecordId!=binding.RuleRecordId||binding.Purpose.MapVersion!=binding.MapVersion)throw new ArgumentException("Execution has a different personal purpose");
            }
            if(plan.Origin!="Deliberated"||plan.Action!="Execute"||plan.OwnerId!=own.OwnerId||!d.MotiveTags.Contains(binding.MotiveTag)||binding.AppliedTaboos==null||d.TabooTags.Intersect(binding.AppliedTaboos).Any()||binding.BoundTick<plan.DecisionTick||binding.BoundTick>tick||string.IsNullOrEmpty(binding.MapVersion)||string.IsNullOrEmpty(binding.ToolNode)||string.IsNullOrEmpty(binding.ContactNode))throw new ArgumentException("Plan is not eligible for this action");
            var rule=own.Find(binding.RuleRecordId);
            if(rule==null||!rule.Direct||rule.Kind!="Document"||rule.Predicate!="IncidentActionRule"||rule.SubjectId!=d.Id||rule.Value!=d.Revision||rule.ProvenanceKey!="ACTION_RULE_"+d.Id+"_"+d.Revision||rule.Text!=d.PublicRule||rule.ReceivedTick>binding.BoundTick)throw new ArgumentException("Actor has not read the action's current public rule");
        }
    }
}
