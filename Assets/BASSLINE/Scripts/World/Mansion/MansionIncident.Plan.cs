using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.World.Mansion
{
    public interface IMansionIncidentInterventions
    {
        // The adapter reports actual interruptions, not a future branch or the player's UI state.
        string InterruptionFor(string actor,string target);
    }
    public interface IMansionIncidentAdmission
    {
        IncidentAdmissionInput ReadCauseAdmission(MansionCaseSettings settings);
    }
    public sealed partial class MansionIncident
    {
        void EnsureExecutionPlan(MansionWorld world,IActorKnowledgeQuery own)
        {
            if(state.Plan!=null&&!string.IsNullOrEmpty(state.Plan.OwnerId))return;
            var s=state.Settings;
            if(!s.ExplicitTestSession)throw new InvalidOperationException("Production execution requires its original deliberated plan");
            // Existing X31 sessions are input replays, not autonomous motive decisions. Keep
            // that distinction explicit; never claim their fixed cast was chosen by the NPC AI.
            state.Plan=new IncidentPlanState{Id=s.Id+"_PLAN",OwnerId=s.ActorId,LoopId=own.LoopId,Origin="ExplicitTestReplay",State="Selected",Action="Execute",TargetId=s.TargetId,Reason=s.DecisionReason,DecisionTick=world.Tick,KnowledgeRevision=own.Revision,RequiredResources=new[]{s.ObjectId},TargetCandidates=new[]{s.TargetId},Alternatives=s.RejectedAlternatives.Distinct().Select(a=>new IncidentAlternative{Action=a,Eligible=false,Reason="명시적 입력 재생에서 선택하지 않은 대안"}).Concat(new[]{new IncidentAlternative{Action="Execute",TargetId=s.TargetId,Eligible=true,Reason=s.DecisionReason}}).ToArray(),EntryConditions=new[]{"ObservedResource","ObservedTarget","PhysicalContact","OutcomeBudget"},AbortConditions=new[]{"ParticipantUnavailable","ResourceUnavailable","Intervention","OpportunityExpired"},InterventionPoints=new[]{"BeforeTravel","AfterPassage","AfterAcquisition","BeforeCause","BeforeResult"}};
        }
        void RecordPlanCheck(MansionWorld world,IActorKnowledgeQuery own,string stage,string outcome,string reason)
        {
            var p=state.Plan;if(p==null||p.OwnerId=="")return;
            var actor=world.Resident(p.OwnerId);var last=p.Checks.LastOrDefault();
            if(last!=null&&last.Stage==stage&&last.Outcome==outcome&&last.Reason==reason&&last.Node==actor.Node)return;
            var ids=own.Records().Where(r=>r.LoopId==own.LoopId&&r.Kind!="ArchiveMeta"&&(r.SubjectId==p.TargetId||p.RequiredResources.Contains(r.SubjectId))&&r.ReceivedTick<=world.Tick).GroupBy(r=>r.SubjectId+"|"+r.Predicate).Select(g=>g.OrderByDescending(r=>r.ReceivedTick).First().Id).ToArray();
            p.Checks=p.Checks.Concat(new[]{new IncidentPlanCheck{Stage=stage,Outcome=outcome,Reason=reason,Node=actor.Node,Tick=world.Tick,KnowledgeRevision=own.Revision,BasisIds=ids}}).ToArray();
            p.State=outcome=="Abort"?"Withdrawn":stage;
            world.Emit("IncidentPlanRechecked",p.OwnerId,state.Settings.Id,stage+"|"+outcome+"|"+reason);
        }
        bool RecheckExecution(MansionWorld world,IActorKnowledgeQuery own,IMansionIncidentPhysics physics)
        {
            if(!RecheckProductionBinding(world,own))return false;
            EnsureExecutionPlan(world,own);
            var s=state.Settings;var actor=world.Resident(s.ActorId);
            string interrupted=(physics as IMansionIncidentInterventions)?.InterruptionFor(s.ActorId,s.TargetId)??"";
            if(interrupted!=""){
                RecordPlanCheck(world,own,state.Stage,"Abort",interrupted);Cancel(world,interrupted);return false;
            }
            if((state.Stage=="ApproachingTool"||state.Stage=="ApproachingTarget")&&actor.Phase=="Idle"&&actor.Node!=(state.Stage=="ApproachingTool"?s.ToolNode:s.ContactNode)){
                RecordPlanCheck(world,own,"AfterPassage","Abort","RouteInterrupted");Cancel(world,"RouteInterrupted");return false;
            }
            if(new[]{"ApproachingTarget","Intent","Contact"}.Contains(state.Stage)){
                var tool=world.Object(s.ObjectId);
                if(actor.HeldObject!=s.ObjectId||tool.Location!="Hand"||tool.Owner!=s.ActorId){RecordPlanCheck(world,own,"AfterAcquisition","Abort","ResourceUnavailable");Cancel(world,"ResourceUnavailable");return false;}
            }
            if(state.Stage=="ApproachingTool"||state.Stage=="ApproachingTarget")RecordPlanCheck(world,own,"AfterPassage","Continue","");
            else if(state.Plan.Checks.Length==0)RecordPlanCheck(world,own,"BeforeTravel","Continue","");return true;
        }
        bool AdmitCause(MansionWorld world,IActorKnowledgeQuery own,IMansionIncidentPhysics physics)
        {
            // Explicit fixtures exercise their registered replay. This is not a certificate
            // that X31 has production assets, fair paths or an autonomous selected motive.
            if(state.Settings.ExplicitTestSession)return true;
            var input=(physics as IMansionIncidentAdmission)?.ReadCauseAdmission(state.Settings.Copy());
            if(input==null){RecordPlanCheck(world,own,"BeforeCause","Abort","MissingAdmission");Cancel(world,"MissingAdmission");return false;}
            if(input.CaseId!=state.Settings.Id||input.TemplateId!=state.Settings.Template||input.DefinitionRevision!=state.Settings.Execution.Definition.Revision||input.Tick!=world.Tick){RecordPlanCheck(world,own,"BeforeCause","Abort","StaleAdmission");Cancel(world,"StaleAdmission");return false;}
            state.Admission=IncidentAdmission.Evaluate(input);
            if(state.Admission.Status=="Admitted")return true;
            RecordPlanCheck(world,own,"BeforeCause","Abort",state.Admission.Reason);Cancel(world,state.Admission.Reason);return false;
        }
        void ReleasePlanMovement(MansionWorld world)
        {
            var actor=world.Resident(state.Settings.ActorId);
            // Release only the route/activity this incident requested. A player's conversation
            // or an independent reaction may already have taken control of this person.
            if(actor.Destination!=state.Settings.ToolNode&&actor.Destination!=state.Settings.ContactNode)return;
            actor.Path=Array.Empty<string>();actor.PathCursor=0;actor.Destination="";actor.QueueDoor="";actor.Recovering=false;actor.RecoveryTicks=0;
            if(actor.Activity=="Examine"||actor.Activity=="Wait"){actor.Phase="Idle";actor.Activity="Rest";actor.ActivityTicks=0;}
            foreach(var door in world.Doors){door.Queue=door.Queue.Where(id=>id!=actor.Id).ToArray();if(door.Holder==actor.Id)door.Holder="";}
        }
    }
}
