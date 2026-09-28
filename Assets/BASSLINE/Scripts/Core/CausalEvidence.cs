using System;
using System.Linq;
namespace BASSLINE.Core
{
    public sealed class ObservedActionDisplay
    {
        public string ActivationId="",TargetId="",DefinitionId="",Revision="";
    }
    public static class CausalEvidence
    {
        public static string OutcomeValue(string activation,string target)=>"ACT:"+activation+":TARGET:"+target;
        // Convenience selection only: the caller supplies records already in its own notebook.
        // Matching labels do not establish a conclusion; Resolve still checks every premise.
        public static string[] RelatedRecords(KnownRecord[] records,string selectedId)
        {
            var selected=records.FirstOrDefault(r=>r.Id==selectedId);
            if(selected==null||new[]{selected.ActivationId,selected.DeviceId,selected.OutcomeTarget,selected.ActionDefinition,selected.ActionRevision}.Any(string.IsNullOrEmpty))return Array.Empty<string>();
            return records.Where(r=>r.ActivationId==selected.ActivationId&&r.DeviceId==selected.DeviceId&&r.OutcomeTarget==selected.OutcomeTarget&&r.ActionDefinition==selected.ActionDefinition&&r.ActionRevision==selected.ActionRevision||r.Predicate=="IncidentActionRule"&&r.SubjectId==selected.ActionDefinition&&r.Value==selected.ActionRevision&&r.ProvenanceKey=="ACTION_RULE_"+selected.ActionDefinition+"_"+selected.ActionRevision||r.Predicate=="DeviceLogCoverage"&&r.SubjectId==selected.DeviceId&&records.Any(log=>log.Kind=="DeviceLog"&&log.ActivationId==selected.ActivationId&&log.DeviceId==selected.DeviceId&&log.Source==r.Source)).Select(r=>r.Id).Distinct().ToArray();
        }
        // These are explicit, read observations. No time-only join, case lookup, text parsing,
        // private intent, or probability bonus can supply a missing premise.
        public static bool TryLink(KnownRecord[] records,string actor,string value,long from,long to,out string missing)
        {
            bool linked=TryLinkActionJournal(records,actor,value,from,to,out bool conditional,out missing);
            if(linked&&conditional){missing="전해 들은 행동은 직접 확인한 사실로 바뀌지 않습니다.";return false;}
            return linked;
        }
        // A person supplies the externally observed action; a local device log supplies
        // the resulting state. The journal never supplies identity, intent or remote vision.
        public static bool TryLinkActionJournal(KnownRecord[] records,string actor,string value,long from,long to,out bool conditional,out string missing)
        {
            conditional=false;missing="행동을 본 자료와 같은 장치의 작동·결과 기록이 필요합니다.";
            if(records==null||from<0||to<=from+1||records.Any(RetiredSurveillance.IsRecord))return false;
            bool Seen(KnownRecord r)=>r!=null&&(r.Direct&&r.Kind=="Visual"||!r.Direct&&r.Kind=="Statement"&&(r.Parents?.Length??0)>0);
            foreach(var cause in records.Where(r=>Seen(r)&&r.CausalStage=="Cause"&&r.Predicate=="UsedObject"&&r.SubjectId==actor&&r.IdentityConfirmed&&r.Value==r.DeviceId&&r.FromTick==from&&r.ToTick==from+1&&OutcomeValue(r.ActivationId,r.OutcomeTarget)==value&&!new[]{r.ActivationId,r.DeviceId,r.OutcomeTarget,r.ActionDefinition,r.ActionRevision,r.ProvenanceKey}.Any(string.IsNullOrEmpty))){
                bool Binding(KnownRecord r)=>r!=null&&r.LoopId==cause.LoopId&&r.ActivationId==cause.ActivationId&&r.DeviceId==cause.DeviceId&&r.OutcomeTarget==cause.OutcomeTarget&&r.ActionDefinition==cause.ActionDefinition&&r.ActionRevision==cause.ActionRevision;
                bool SameWitness(KnownRecord r)=>Seen(r)&&Binding(r)&&r.SubjectId==actor&&r.IdentityConfirmed&&r.ProvenanceKey==cause.ProvenanceKey;
                var continued=records.FirstOrDefault(r=>SameWitness(r)&&r.CausalStage=="ContactContinued"&&r.Predicate=="UsedObject"&&r.Value==cause.DeviceId&&r.FromTick<=from);
                var warning=continued==null?null:records.FirstOrDefault(r=>SameWitness(r)&&r.CausalStage=="WarningShown"&&r.Predicate=="DeviceWarning"&&r.Value==cause.DeviceId&&r.FromTick<continued.FromTick);
                if(warning==null){missing="같은 목격자가 위험 안내와 그 뒤에도 계속된 행동을 확인한 자료가 필요합니다.";continue;}
                if(!records.Any(r=>r.LoopId==cause.LoopId&&r.Direct&&r.Kind=="Document"&&r.Predicate=="IncidentActionRule"&&r.SubjectId==cause.ActionDefinition&&r.Value==cause.ActionRevision&&r.ProvenanceKey=="ACTION_RULE_"+cause.ActionDefinition+"_"+cause.ActionRevision)){
                    missing="현장의 공개 작동 규칙을 직접 읽어야 합니다.";continue;
                }
                bool Journal(KnownRecord r)=>Binding(r)&&r.Direct&&r.Kind=="DeviceLog"&&r.SubjectId==cause.DeviceId&&r.Predicate=="DeviceStateTransition"&&r.Value==r.ActivationId+":"+r.CausalStage&&r.ToTick==r.FromTick+1&&!string.IsNullOrEmpty(r.Source)&&!string.IsNullOrEmpty(r.ProvenanceKey)&&r.ProvenanceKey!=cause.ProvenanceKey;
                foreach(var result in records.Where(r=>Journal(r)&&r.CausalStage=="Result"&&r.FromTick==to-1)){
                    bool SameJournal(KnownRecord r)=>Journal(r)&&r.Source==result.Source&&r.ProvenanceKey==result.ProvenanceKey;
                    if(!records.Any(r=>r.LoopId==cause.LoopId&&r.Direct&&r.Kind=="Document"&&r.Predicate=="DeviceLogCoverage"&&r.SubjectId==cause.DeviceId&&r.Source==result.Source)){
                        missing="이 기록판이 어떤 장치의 상태를 기록하는지 확인해야 합니다.";continue;
                    }
                    bool stage(string kind,long tick)=>records.Any(r=>SameJournal(r)&&r.CausalStage==kind&&r.FromTick==tick);
                    bool begun=records.Any(r=>SameJournal(r)&&r.CausalStage=="WarningShown"&&r.FromTick<=warning.FromTick&&r.FromTick<continued.FromTick);
                    if(!begun||!stage("ContactContinued",continued.FromTick)||!stage("Cause",from)){
                        missing="같은 기록판의 위험 안내·동작 유지·작동 기록을 함께 확인해야 합니다.";continue;
                    }
                    if(records.Any(r=>SameJournal(r)&&(r.CausalStage=="Cancelled"||r.CausalStage=="RiskResolved")&&r.FromTick>=warning.FromTick&&r.FromTick<=result.FromTick)){
                        missing="중단 또는 위험 해소 기록과 결과 기록이 충돌합니다. 먼저 기록을 대조해야 합니다.";continue;
                    }
                    bool hearsay=new[]{warning,continued,cause}.Any(r=>!r.Direct);
                    if(!hearsay){conditional=false;missing="";return true;}
                    conditional=true;
                }
            }
            if(conditional){missing="";return true;}
            return false;
        }
        public static bool SameBinding(KnownRecord a,KnownRecord b)=>(a.ActivationId??"")==(b.ActivationId??"")&&(a.DeviceId??"")==(b.DeviceId??"")&&(a.OutcomeTarget??"")==(b.OutcomeTarget??"")&&(a.ActionDefinition??"")==(b.ActionDefinition??"")&&(a.ActionRevision??"")==(b.ActionRevision??"")&&(a.CausalStage??"")==(b.CausalStage??"");
        public static bool TryLinkWitness(KnownRecord[] records,string actor,string value,long from,long to,out bool conditional,out string missing)
        {
            conditional=false;missing="위험 안내부터 실행과 결과까지 이어지는 같은 목격자의 관측이 필요합니다.";
            bool Observation(KnownRecord r)=>!RetiredSurveillance.IsRecord(r)&&(r.Direct&&r.Kind=="Visual"||!r.Direct&&r.Kind=="Statement"&&(r.Parents?.Length??0)>0);
            foreach(var cause in records.Where(r=>Observation(r)&&r.CausalStage=="Cause"&&r.Predicate=="UsedObject"&&r.SubjectId==actor&&r.IdentityConfirmed&&r.Value==r.DeviceId&&r.FromTick==from&&OutcomeValue(r.ActivationId,r.OutcomeTarget)==value&&!new[]{r.ActivationId,r.DeviceId,r.OutcomeTarget,r.ActionDefinition,r.ActionRevision,r.ProvenanceKey}.Any(string.IsNullOrEmpty))){
                bool Same(KnownRecord r)=>Observation(r)&&r.SubjectId==actor&&r.IdentityConfirmed&&r.ProvenanceKey==cause.ProvenanceKey&&r.ActivationId==cause.ActivationId&&r.DeviceId==cause.DeviceId&&r.OutcomeTarget==cause.OutcomeTarget&&r.ActionDefinition==cause.ActionDefinition&&r.ActionRevision==cause.ActionRevision;
                var continuation=records.FirstOrDefault(r=>Same(r)&&r.CausalStage=="ContactContinued"&&r.Predicate=="UsedObject"&&r.Value==cause.DeviceId&&r.FromTick<=from);
                if(continuation==null){missing="그 목격자가 경고 뒤에도 유지된 동작을 보았는지 확인해야 합니다.";continue;}
                var warning=records.FirstOrDefault(r=>Same(r)&&r.CausalStage=="WarningShown"&&r.Predicate=="DeviceWarning"&&r.Value==cause.DeviceId&&r.FromTick<continuation.FromTick);
                if(warning==null){missing="같은 목격자가 작동 전에 위험 안내와 대상을 읽은 관측이 필요합니다.";continue;}
                var outcome=records.FirstOrDefault(r=>Same(r)&&r.CausalStage=="Result"&&r.Predicate=="CausedOutcome"&&r.Value==value&&r.FromTick==from&&r.ToTick==to);
                if(outcome==null){missing="실행부터 같은 대상의 결과까지 끊김 없이 본 목격이 필요합니다.";continue;}
                if(!records.Any(r=>r.Direct&&r.Kind=="Document"&&r.Predicate=="IncidentActionRule"&&r.SubjectId==cause.ActionDefinition&&r.Value==cause.ActionRevision&&r.ProvenanceKey=="ACTION_RULE_"+cause.ActionDefinition+"_"+cause.ActionRevision)){
                    missing="목격 내용과 연결할 장치의 공개 작동 규칙을 직접 확인해야 합니다.";continue;
                }
                bool hearsay=new[]{warning,continuation,cause,outcome}.Any(r=>!r.Direct);
                if(!hearsay){conditional=false;missing="";return true;}
                conditional=true;
            }
            if(conditional){missing="";return true;}
            return false;
        }
    }
}
