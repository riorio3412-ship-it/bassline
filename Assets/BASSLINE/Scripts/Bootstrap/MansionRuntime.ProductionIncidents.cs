using System;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IMansionIncidentRiskDisplay,IMansionWitnessDisplay
    {
        MansionIncidentDeviceSignal[] incidentSignals=Array.Empty<MansionIncidentDeviceSignal>();
        ObservedActionDisplay IMansionWitnessDisplay.ReadIncidentDisplay(string observer,string device)
        {
            if(!World.CanAct(observer)||!targets.TryGetValue(device,out var target))return null;
            var signal=target.GetComponent<MansionIncidentDeviceSignal>();
            if(!signal||!signal.isActiveAndEnabled||!signal.WarningLabel||!signal.WarningLabel.gameObject.activeInHierarchy||string.IsNullOrEmpty(signal.ActivationId))return null;
            var label=signal.WarningLabel;var renderer=label.GetComponent<UnityEngine.Renderer>();
            if(!renderer||!renderer.enabled||!label.font||label.color.a<.9f||!Visible(observer,label.transform.position,2.5f,true))return null;
            return new ObservedActionDisplay{ActivationId=signal.ActivationId,TargetId=signal.TargetId,DefinitionId=signal.DefinitionId,Revision=signal.Revision};
        }
        bool IMansionIncidentRiskDisplay.ShowVisibleWarning(MansionCaseSettings settings)
        {
            if(!targets.TryGetValue(settings.ObjectId,out var target))return false;
            var signal=target.GetComponent<MansionIncidentDeviceSignal>();
            if(!signal||!signal.isActiveAndEnabled||!signal.WarningLabel||!signal.WarningLabel.font)return false;
            var incident=incidents.Find(settings.Id)?.Capture();if(incident==null)return false;
            ShowIncidentSignal(signal,incident);
            return signal.WarningLabel.GetComponent<UnityEngine.Renderer>() is UnityEngine.Renderer renderer&&renderer.enabled&&Visible(settings.ActorId,signal.WarningLabel.transform.position,3,false);
        }
        void ClearInactiveIncidentSignals()
        {
            var cases=incidents.Capture();
            foreach(var signal in incidentSignals.Where(s=>s)){
                string id=signal.GetComponent<FixtureTarget>().StableId;
                var active=cases.FirstOrDefault(c=>c.Settings?.ObjectId==id&&(c.Stage=="Contact"&&c.RiskNoticeTick>=0||c.Stage=="CauseCommitted"));
                if(active==null)signal.Clear();
                else ShowIncidentSignal(signal,active);
            }
        }
        void ShowIncidentSignal(MansionIncidentDeviceSignal signal,BASSLINE.World.Mansion.MansionIncidentSnapshot state)
        {
            if(state.Settings.ExplicitTestSession||state.Settings.PlayerInitiated){signal.Clear();return;}
            var d=state.Settings.Execution.Definition;
            signal.Show((state.Stage=="CauseCommitted"?"작동 중":"위험 안내")+"\n"+d.PublicRule+"\n작동 번호: "+state.ActivationId+"\n대상: "+NameOf(state.Settings.TargetId)+"\n안내 개정: "+d.Revision+(state.Stage=="CauseCommitted"?"":"\n접촉을 멈추면 작동 전에 취소됩니다."),state.ActivationId,state.Settings.TargetId,d.Id,d.Revision);
        }
        MansionIncidentRulePlate IncidentRulePlate(string id)=>targets.TryGetValue(id,out var target)?target.GetComponent<MansionIncidentRulePlate>():null;
        string ReadIncidentRule(string actor,string plateId)
        {
            var plate=IncidentRulePlate(plateId);
            if(!CanReadIncidentRule(actor,plateId))return "";
            IncidentExecutionDefinition.Validate(plate.Definition);var d=plate.Definition;
            string key="ACTION_RULE_"+d.Id+"_"+d.Revision;
            var prior=Knowledge.For(actor).Records().FirstOrDefault(r=>r.Direct&&r.Kind=="Document"&&r.ProvenanceKey==key&&r.Text==d.PublicRule);
            if(prior!=null)return prior.Id;
            return Knowledge.Observe(actor,new KnownRecord{Kind="Document",ProvenanceKey=key,SubjectId=d.Id,Predicate="IncidentActionRule",Value=d.Revision,PlaceId=PlaceOf(plate.transform.position),Position=P(plate.transform.position),Text=d.PublicRule,Source=plateId,FromTick=World.Tick,ToTick=World.Tick+1,Supports=new[]{"직접 읽은 장치의 공개된 작동 조건과 취소 범위"},DoesNotEstablish=new[]{"누가 실제로 실행했는지", "어떤 대상에게 결과가 발생했는지", "다른 사람도 설명을 읽었는지"}},World.Tick);
        }
        // Authority adapter for the goal planner. No player/debug UI can choose a culprit through it.
        internal string BindIncidentPlan(IncidentPlanState plan,string plateId,string objectId,string toolNode,string contactNode,string motiveTag,string[] taboos,long opportunityEndTick,ResidentPurposeBinding purpose=null)
        {
            if(plan==null||plan.Origin!="Deliberated"||plan.Action!="Execute"||court.Phase!="NotStarted")return "Unavailable";
            var plate=IncidentRulePlate(plateId);if(!plate||!plate.isActiveAndEnabled)return "MissingAuthoredAction";
            var definition=plate.Definition;IncidentExecutionDefinition.Validate(definition);
            var own=Knowledge.For(plan.OwnerId);string key="ACTION_RULE_"+definition.Id+"_"+definition.Revision;
            var rule=own.Records().FirstOrDefault(r=>r.Direct&&r.Kind=="Document"&&r.ProvenanceKey==key&&r.Text==definition.PublicRule);
            if(rule==null)return "RuleNotRead";
            var chapter=World.CaseBook.Capture();
            var binding=new IncidentExecutionBinding{Definition=definition.Copy(),RuleRecordId=rule.Id,MotiveTag=motiveTag,AppliedTaboos=(taboos??Array.Empty<string>()).ToArray(),MapVersion=Layout.MapVersion,ToolNode=toolNode,ContactNode=contactNode,BoundTick=World.Tick};
            if(purpose!=null){binding.PurposeId=purpose.Definition.Id;binding.Purpose=purpose.Copy();}
            var settings=new MansionCaseSettings{Id="CASE_"+plan.Id,Enabled=true,ExplicitTestSession=false,Template=definition.Id,Source=definition.Source,ActorId=plan.OwnerId,TargetId=plan.TargetId,ObjectId=objectId,ToolNode=toolNode,ContactNode=contactNode,ChapterStartingResidents=chapter.StartingResidents,ChapterStartTick=chapter.ChapterStartTick,ApproachTick=World.Tick,EarliestCauseTick=World.Tick+definition.IntentTicks+definition.ContactTicks,OpportunityEndTick=opportunityEndTick};
            return incidents.ConfigurePlanned(World,settings,plan,binding,own);
        }
    }
}
