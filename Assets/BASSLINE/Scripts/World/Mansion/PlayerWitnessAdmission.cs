using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.World.Mansion
{
    // Admission for the directly witnessed variant only. The player's existing notebook
    // does not depend on another NPC agreeing to testify. The local journal preserves
    // the device transitions, never the actor's identity. This is a prospective route,
    // not evidence of a cause/result that has not happened yet.
    public static class PlayerWitnessAdmission
    {
        public static IncidentEvidenceRoute Read(IActorKnowledgeQuery own,MansionIncidentSnapshot incident,
            MansionActionJournalSnapshot journal,string source,string[] path,string[] dependencies,long tick)
        {
            if(own==null||own.OwnerId!="CH_01"||incident?.Settings?.Execution?.Definition==null||journal==null||
                incident.Settings.ExplicitTestSession||incident.Settings.ActorId==own.OwnerId||incident.Settings.TargetId==own.OwnerId||
                incident.CauseTick>=0||incident.RiskContinuedTick<0||journal.LastTick!=tick-1||journal.InstalledTick>incident.RiskNoticeTick||
                path==null||path.Length==0||dependencies==null||string.IsNullOrEmpty(source))return null;
            var d=incident.Settings.Execution.Definition;
            var witness=incident.RiskWitnesses.FirstOrDefault(w=>w.Observer==own.OwnerId&&w.LastTick==tick&&!string.IsNullOrEmpty(w.ContinuationReceiptId));
            if(witness==null)return null;
            string root="WITNESS_L"+journal.Loop+"_"+incident.Settings.Id+"_"+own.OwnerId;
            bool Seen(KnownRecord r)=>r!=null&&r.LoopId==own.LoopId&&r.Direct&&r.Kind=="Visual"&&r.IdentityConfirmed&&r.ProvenanceKey==root&&
                r.SubjectId==incident.Settings.ActorId&&r.ActivationId==incident.ActivationId&&r.DeviceId==incident.Settings.ObjectId&&
                r.OutcomeTarget==incident.Settings.TargetId&&r.ActionDefinition==d.Id&&r.ActionRevision==d.Revision&&r.ReceivedTick<tick;
            var records=own.Records();
            var continued=records.FirstOrDefault(r=>Seen(r)&&r.CausalStage=="ContactContinued"&&r.Predicate=="UsedObject"&&r.Value==r.DeviceId&&r.FromTick==incident.RiskContinuedTick);
            var warning=records.FirstOrDefault(r=>Seen(r)&&r.CausalStage=="WarningShown"&&r.Predicate=="DeviceWarning"&&r.Value==r.DeviceId&&r.FromTick>=incident.RiskNoticeTick&&r.FromTick<incident.RiskContinuedTick);
            var rule=records.FirstOrDefault(r=>r.LoopId==own.LoopId&&r.Direct&&r.Kind=="Document"&&r.Predicate=="IncidentActionRule"&&r.SubjectId==d.Id&&r.Value==d.Revision&&r.Text==d.PublicRule&&r.ProvenanceKey=="ACTION_RULE_"+d.Id+"_"+d.Revision&&r.ReceivedTick<tick);
            if(warning==null||continued==null||rule==null||!journal.Links.Any(l=>l.SourceId==source&&l.CaseId==incident.Settings.Id&&l.ActivationId==incident.ActivationId))return null;
            var entries=journal.Entries.Where(e=>e.SourceId==source&&e.ActivationId==incident.ActivationId).ToArray();
            bool Stage(string kind,long at)=>entries.Any(e=>e.Kind==kind&&e.Tick==at&&e.DeviceId==incident.Settings.ObjectId&&e.TargetId==incident.Settings.TargetId&&e.DefinitionId==d.Id&&e.Revision==d.Revision);
            if(!Stage("WarningShown",incident.RiskNoticeTick)||!Stage("ContactContinued",incident.RiskContinuedTick)||entries.Any(e=>new[]{"Cancelled","RiskResolved","Result","Cause"}.Contains(e.Kind)))return null;
            return new IncidentEvidenceRoute{Id="PLAYER_WITNESS_JOURNAL_"+source,Available=true,Preserved=true,
                RequiresPerson=false,RequiresFavor=false,RequiresRareTool=false,
                RootIds=new[]{root,source+"_JOURNAL_L"+journal.Loop},
                AccessDependencies=dependencies.Concat(new[]{"OWN_NOTEBOOK_CH_01","JOURNAL_"+source}).Distinct().ToArray(),
                AccessNodes=(string[])path.Clone(),ObservationIds=new[]{warning.Id,continued.Id,rule.Id}.Concat(entries.Select(e=>e.Id)).ToArray(),
                Responsibilities=(string[])IncidentAdmission.RequiredResponsibilities.Clone()};
        }
    }
}
