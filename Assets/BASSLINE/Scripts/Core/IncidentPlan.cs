using System;
using System.Linq;
namespace BASSLINE.Core
{
    [Serializable] public sealed class IncidentAlternative
    {
        public string Action="",TargetId="",Reason="";
        public bool Eligible;public int GoalBenefit,RelationshipBenefit,RiskCost,TravelCost;
        public string[] BasisIds=Array.Empty<string>();
        public int Score=>GoalBenefit+RelationshipBenefit-RiskCost-TravelCost;
        public IncidentAlternative Copy(){var c=(IncidentAlternative)MemberwiseClone();c.BasisIds=(string[])BasisIds.Clone();return c;}
    }
    [Serializable] public sealed class IncidentPlanCheck
    {
        public string Stage="",Outcome="",Reason="",Node="";public long Tick,KnowledgeRevision;
        public string[] BasisIds=Array.Empty<string>();
        public IncidentPlanCheck Copy(){var c=(IncidentPlanCheck)MemberwiseClone();c.BasisIds=(string[])BasisIds.Clone();return c;}
    }
    [Serializable] public sealed class IncidentPlanState
    {
        public string Id="",OwnerId="",LoopId="",Origin="",State="Unconsidered",Action="",TargetId="",Reason="",GoalId="";
        public int Revision=1;public long DecisionTick,KnowledgeRevision;public uint RandomBefore=1,RandomAfter=1;
        public string[] KnownReasons=Array.Empty<string>(),TargetCandidates=Array.Empty<string>(),RequiredResources=Array.Empty<string>(),EntryConditions=Array.Empty<string>(),AbortConditions=Array.Empty<string>(),InterventionPoints=Array.Empty<string>();
        public IncidentAlternative[] Alternatives=Array.Empty<IncidentAlternative>();
        public IncidentPlanCheck[] Checks=Array.Empty<IncidentPlanCheck>();
        public IncidentPlanState Copy(){var c=(IncidentPlanState)MemberwiseClone();c.KnownReasons=(string[])KnownReasons.Clone();c.TargetCandidates=(string[])TargetCandidates.Clone();c.RequiredResources=(string[])RequiredResources.Clone();c.EntryConditions=(string[])EntryConditions.Clone();c.AbortConditions=(string[])AbortConditions.Clone();c.InterventionPoints=(string[])InterventionPoints.Clone();c.Alternatives=Alternatives.Select(a=>a.Copy()).ToArray();c.Checks=Checks.Select(x=>x.Copy()).ToArray();return c;}
    }
    // Authored goal interpretation is separate from observation. An accusation or a failed
    // appointment is never silently turned into a lethal motive by this contract.
    public sealed class IncidentDecisionInput
    {
        public string Id="",GoalId="",ProposedTarget="",ObjectId="";
        public long Tick,FreshnessTicks=600;public int Revision=1;
        public uint RandomState=1;
        public bool GoalAllowsHarm,TabooApplies,HasRegisteredAction;
        public string[] KnownReasonIds=Array.Empty<string>();
        public IncidentAlternative[] Alternatives=Array.Empty<IncidentAlternative>();
    }
    public static class IncidentPlanIntegrity
    {
        public static readonly string[] Actions={"Withdraw","AskForHelp","Disclose","Negotiate","Execute"};
        public static void Validate(IncidentPlanState s,IActorKnowledgeQuery own,long tick)
        {
            if(s==null||s.OwnerId!=own.OwnerId||s.LoopId!=own.LoopId||s.Revision<1||s.DecisionTick<0||s.DecisionTick>tick||s.KnowledgeRevision>own.Revision||s.KnownReasons==null||s.Alternatives==null||s.Checks==null||s.RequiredResources==null||s.TargetCandidates==null||s.EntryConditions==null||s.AbortConditions==null||s.InterventionPoints==null)throw new ArgumentException("Invalid owner-bound incident plan");
            bool Known(string id)=>own.Find(id)!=null&&own.Find(id).LoopId==own.LoopId&&own.Find(id).Kind!="ArchiveMeta";
            if(s.KnownReasons.Any(id=>!Known(id)||own.Find(id).ReceivedTick>s.DecisionTick)||s.Checks.Any(c=>c==null||c.Tick<s.DecisionTick||c.Tick>tick||c.KnowledgeRevision>own.Revision||c.BasisIds==null||c.BasisIds.Any(id=>!Known(id)||own.Find(id).ReceivedTick>c.Tick))||s.Checks.Zip(s.Checks.Skip(1),(a,b)=>b.Tick<a.Tick).Any(x=>x))throw new ArgumentException("Plan references unavailable knowledge");
            if(!Actions.Contains(s.Action)||s.Alternatives.Any(a=>a==null||string.IsNullOrEmpty(a.Action)||s.Origin=="Deliberated"&&!Actions.Contains(a.Action)||a.BasisIds==null||a.BasisIds.Any(id=>!Known(id)||own.Find(id).ReceivedTick>s.DecisionTick))||!s.Alternatives.Any(a=>a.Action==s.Action&&a.TargetId==s.TargetId&&a.Eligible))throw new ArgumentException("Plan did not consider its selected action");
            if(s.Origin=="Deliberated"&&(string.IsNullOrEmpty(s.GoalId)||s.KnownReasons.Length==0||!Actions.All(action=>s.Alternatives.Any(a=>a.Action==action))||s.Action=="Execute"&&(!s.TargetCandidates.Contains(s.TargetId)||s.RequiredResources.Length==0)))throw new ArgumentException("Missing reasons, alternatives or resources");
            if(s.Origin!="Deliberated"&&s.Origin!="ExplicitTestReplay")throw new ArgumentException("Unknown plan origin");
        }
    }
}
