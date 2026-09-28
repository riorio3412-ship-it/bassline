using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.World.Mansion
{
    public sealed partial class MansionIncident
    {
        public string ConfigurePlanned(MansionWorld world,MansionCaseSettings settings,IncidentPlanState plan,IncidentExecutionBinding binding,IActorKnowledgeQuery own)
        {
            if(state.Stage!="Inactive")return "AlreadyConfigured";
            if(settings==null||!settings.Enabled||settings.ExplicitTestSession)return "Unavailable";
            IncidentExecutionBinding.Validate(binding,plan,own,world.Tick);
            var definition=binding.Definition;
            if(definition.ReviewStatus!="Reviewed")return "ContentReviewPending";
            if(binding.MapVersion!=world.Capture().MapVersion||settings.ActorId!=own.OwnerId||settings.TargetId!=plan.TargetId||!plan.RequiredResources.Contains(settings.ObjectId)||settings.Template!=definition.Id||settings.ToolNode!=binding.ToolNode||settings.ContactNode!=binding.ContactNode)return "BindingMismatch";
            // Cast, route and resource were chosen before registration from owner-bound knowledge.
            // Physical authority rejects unavailable resources; it never substitutes another victim.
            var prepared=settings.Copy();prepared.Execution=binding.Copy();prepared.IntentSpeech=definition.IntentLine;prepared.DecisionReason=plan.Reason;
            prepared.ContactTicks=definition.ContactTicks;prepared.MinimumIntentTicks=definition.IntentTicks;prepared.DelayTicks=definition.DelayTicks;
            prepared.RejectedAlternatives=plan.Alternatives.Where(a=>a.Action!="Execute").Select(a=>a.Action).Distinct().ToArray();
            ValidateSettings(prepared);
            if(!world.CanAct(prepared.ActorId)||!world.CanAct(prepared.TargetId)||!world.Objects.Any(o=>o.Id==prepared.ObjectId)||world.FindPath(world.Resident(prepared.ActorId).Node,prepared.ToolNode,world.Resident(prepared.ActorId).KnownLocked).Length==0||world.FindPath(prepared.ToolNode,prepared.ContactNode,world.Resident(prepared.ActorId).KnownLocked).Length==0)return "PhysicalReferencesUnavailable";
            string registered=world.RegisterCase(prepared);if(registered!="Registered")return registered;
            state=new MansionIncidentSnapshot{Settings=prepared,Plan=plan.Copy(),Stage="Planned",FatalityCap=prepared.ChapterStartingResidents<=6?1:2};
            world.Emit("IncidentPlanBound",prepared.ActorId,prepared.Id,plan.Id+"|"+definition.Id+"|"+definition.Revision);return "Configured";
        }
        bool RecheckProductionBinding(MansionWorld world,IActorKnowledgeQuery own)
        {
            if(state.Settings.ExplicitTestSession)return true;
            var purpose=state.Settings.Execution.Purpose;
            if(!string.IsNullOrEmpty(state.Settings.Execution.PurposeId)&&(ResidentPurposeBinding.Resolved(purpose,own,world.Tick,state.Plan.DecisionTick)||ResidentPurposeBinding.Taboo(purpose,own,world.Tick))){Cancel(world,"PurposeResolvedOrTabooObserved");return false;}
            try{IncidentExecutionBinding.Validate(state.Settings.Execution,state.Plan,own,world.Tick);}
            catch(ArgumentException){Cancel(world,"PlanBasisUnavailable");return false;}
            if(state.Settings.Execution.MapVersion!=world.Capture().MapVersion){Cancel(world,"LayoutChanged");return false;}
            return true;
        }
        string ContactObservation=>state.Settings.PlayerInitiated?"손에 든 무기로 상대를 치는 모습을 보았다.":state.Settings.ExplicitTestSession?"가상 소품이 인물에게 접촉하는 모습을 보았다.":state.Settings.Execution.Definition.ContactObservation;
        string ContinuousObservation=>state.Settings.PlayerInitiated?"같은 인물이 무기로 상대를 친 뒤 쓰러질 때까지 끊김 없이 보았다.":state.Settings.ExplicitTestSession?"같은 인물의 접촉부터 대상이 쓰러질 때까지 끊김 없이 보았다.":state.Settings.Execution.Definition.ContinuousObservation;
        string TraceObservation=>"가상 소품에서 P31 표식을 관찰했다."; // Explicit fixture inspection only.
        string TraceValue=>"P31"; // Production uses rendered physical mark observations.
        int RequiredRescueTicks=>state.Settings.PlayerInitiated?PlayerRescueTicks:state.Settings.ExplicitTestSession?X31RescueTicks:state.Settings.Execution.Definition.RescueTicks;
    }
}
