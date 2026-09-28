using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.NPC
{
    // Per-person deliberation. This class cannot read World, a culprit field, another person's
    // knowledge, elapsed peaceful days or the chapter's demand for a murder.
    public sealed class IncidentPlanner
    {
        public IncidentPlanState Consider(IActorKnowledgeQuery own,IncidentDecisionInput input,IncidentPlanState previous=null)
        {
            if(own==null||input==null||string.IsNullOrEmpty(input.Id)||string.IsNullOrEmpty(input.GoalId)||input.Revision<1||input.Tick<0||input.KnownReasonIds==null||input.Alternatives==null||input.Alternatives.Any(a=>a==null||a.BasisIds==null)||input.FreshnessTicks<1)return null;
            var reasons=input.KnownReasonIds.Distinct().Select(own.Find).ToArray();
            if(reasons.Length==0||reasons.Any(r=>r==null||r.LoopId!=own.LoopId||r.Kind=="ArchiveMeta"||r.ReceivedTick>input.Tick))return null;
            // Repeated observation of the same root or a new wall-clock tick is not a new motive.
            string Root(KnownRecord r)=>string.IsNullOrEmpty(r.ProvenanceKey)?r.RootId??r.Id:r.ProvenanceKey;
            if(previous!=null){
                if(previous.OwnerId!=own.OwnerId||previous.LoopId!=own.LoopId||input.Revision<=previous.Revision)return null;
                var old=previous.KnownReasons.Select(own.Find).Where(r=>r!=null).Select(Root).ToArray();
                if(reasons.All(r=>old.Contains(Root(r))))return null;
            }
            bool Fresh(string id)=>own.Records().Any(r=>r.SubjectId==id&&r.Predicate=="AtPlace"&&r.Direct&&r.IdentityConfirmed&&r.ToTick<=input.Tick+1&&input.Tick-r.ToTick<=input.FreshnessTicks);
            var alternatives=input.Alternatives.Select(a=>a.Copy()).ToArray();
            if(!IncidentPlanIntegrity.Actions.All(a=>alternatives.Any(c=>c.Action==a))||alternatives.Any(a=>!IncidentPlanIntegrity.Actions.Contains(a.Action)))return null;
            foreach(var a in alternatives){
                if(a.GoalBenefit<0||a.GoalBenefit>4||a.RelationshipBenefit< -3||a.RelationshipBenefit>3||a.RiskCost<0||a.RiskCost>4||a.TravelCost<0||a.TravelCost>3)return null;
                if(a.BasisIds.Any(id=>own.Find(id)==null||own.Find(id).LoopId!=own.LoopId||own.Find(id).Kind=="ArchiveMeta"||own.Find(id).ReceivedTick>input.Tick))return null;
                if(a.Action=="Execute"&&(!input.GoalAllowsHarm||input.TabooApplies||!input.HasRegisteredAction||a.TargetId!=input.ProposedTarget||a.TargetId==own.OwnerId||!Fresh(input.ProposedTarget)||!Fresh(input.ObjectId))){a.Eligible=false;a.Reason="현재 목적·금기·관측·실행 조건으로 이 행동을 선택할 수 없다.";}
                if(a.Action=="Withdraw"){a.Eligible=true;a.TargetId="";}
            }
            var eligible=alternatives.Where(a=>a.Eligible).ToArray();if(eligible.Length==0)return null;
            int best=eligible.Max(a=>a.Score);var ties=eligible.Where(a=>a.Score==best).OrderBy(a=>a.Action,StringComparer.Ordinal).ThenBy(a=>a.TargetId,StringComparer.Ordinal).ToArray();
            uint before=input.RandomState==0?1:input.RandomState,after=before;int choice=0;
            if(ties.Length>1){after^=after<<13;after^=after>>17;after^=after<<5;choice=(int)(after%(uint)ties.Length);}
            var selected=ties[choice];
            return new IncidentPlanState{Id=input.Id,GoalId=input.GoalId,OwnerId=own.OwnerId,LoopId=own.LoopId,Origin="Deliberated",State="Selected",Action=selected.Action,TargetId=selected.TargetId,Reason=selected.Reason,Revision=input.Revision,DecisionTick=input.Tick,KnowledgeRevision=own.Revision,RandomBefore=before,RandomAfter=after,KnownReasons=reasons.Select(r=>r.Id).ToArray(),TargetCandidates=alternatives.Where(a=>a.TargetId!="").Select(a=>a.TargetId).Distinct().ToArray(),RequiredResources=selected.Action=="Execute"?new[]{input.ObjectId}:Array.Empty<string>(),Alternatives=alternatives,EntryConditions=new[]{"OwnKnownReasons","ObservedResource","ObservedTarget","RegisteredAction","PhysicalOpportunity","IndependentEvidencePaths","OutcomeBudget"},AbortConditions=new[]{"TargetAbsent","ResourceUnavailable","RouteBlocked","Intervention","OpportunityExpired","AdmissionLost"},InterventionPoints=new[]{"BeforeTravel","AfterPassage","AfterAcquisition","BeforeCause","BeforeResult"}};
        }
    }
}
