using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.World.Mansion
{
    [Serializable] public sealed class MansionRiskWitness
    {
        public string Observer="",WarningReceiptId="",ContinuationReceiptId="";
        public long LastTick=-1;
        public MansionRiskWitness Copy()=>(MansionRiskWitness)MemberwiseClone();
    }
    public sealed partial class MansionIncident
    {
        void CaptureRiskWitnesses(MansionWorld world,IMansionIncidentPhysics physics)
        {
            var reader=physics as IMansionWitnessDisplay;
            if(reader==null){state.RiskWitnesses=Array.Empty<MansionRiskWitness>();return;}
            var s=state.Settings;var next=new System.Collections.Generic.List<MansionRiskWitness>();
            foreach(var resident in world.Residents.Where(r=>r.Alive&&r.Present)){
                string observer=resident.Id;
                if(!physics.CanSee(observer,s.ActorId)||!physics.CanSee(observer,s.TargetId)||!physics.CanSee(observer,s.ObjectId)||!physics.Identifies(observer,s.ActorId)||!physics.Identifies(observer,s.TargetId))continue;
                var display=reader.ReadIncidentDisplay(observer,s.ObjectId);
                if(display==null||display.ActivationId!=state.ActivationId||display.TargetId!=s.TargetId||display.DefinitionId!=s.Execution.Definition.Id||display.Revision!=s.Execution.Definition.Revision)continue;
                var prior=state.RiskWitnesses.FirstOrDefault(w=>w.Observer==observer&&w.LastTick==world.Tick-1);
                if(prior==null){
                    // A late arrival cannot retroactively have read the warning window.
                    if(world.Tick>=state.RiskNoticeTick+s.Execution.Definition.RiskNoticeTicks)continue;
                    var observed=new KnownRecord{ActivationId=display.ActivationId,DeviceId=s.ObjectId,OutcomeTarget=display.TargetId,ActionDefinition=display.DefinitionId,ActionRevision=display.Revision};
                    string receipt=ObserveRiskStage(world,observer,observed,"WarningShown","DeviceWarning","접촉 중인 인물과 장치의 위험 안내·작동 번호·대상을 직접 확인했다.",world.Tick,world.Tick+1);
                    prior=new MansionRiskWitness{Observer=observer,WarningReceiptId=receipt};
                }
                prior.LastTick=world.Tick;next.Add(prior);
            }
            state.RiskWitnesses=next.ToArray();
        }
        void CaptureWitnessContinuation(MansionWorld world)
        {
            if(state.RiskContinuedTick!=world.Tick)return;
            foreach(var witness in state.RiskWitnesses){
                var warning=state.Receipts.Single(r=>r.Id==witness.WarningReceiptId).Record;
                if(warning.FromTick>=world.Tick)continue;
                witness.ContinuationReceiptId=ObserveRiskStage(world,witness.Observer,warning,"ContactContinued","UsedObject","위험 안내가 표시된 뒤에도 같은 인물이 장치 접촉을 유지하는 모습을 계속 보았다.",world.Tick,world.Tick+1);
            }
        }
        void CaptureWitnessCauses(MansionWorld world)
        {
            if(state.Settings.ExplicitTestSession||state.Settings.PlayerInitiated)return;
            foreach(var witness in state.Witnesses.Where(w=>w.ActorIdentified&&w.TargetIdentified)){
                var risk=state.RiskWitnesses.FirstOrDefault(w=>w.Observer==witness.Observer&&w.LastTick==world.Tick&&!string.IsNullOrEmpty(w.ContinuationReceiptId));
                if(risk==null)continue;
                var seen=state.Receipts.Single(r=>r.Id==risk.ContinuationReceiptId).Record;
                witness.CauseReceiptId=ObserveRiskStage(world,witness.Observer,seen,"Cause","UsedObject","위험 안내 이후 유지된 접촉과 장치 작동을 같은 작동 번호로 확인했다.",world.Tick,world.Tick+1);
            }
        }
        void CaptureWitnessOutcomes(MansionWorld world,IMansionIncidentPhysics physics)
        {
            foreach(var witness in state.Witnesses.Where(w=>w.Continuous&&!string.IsNullOrEmpty(w.CauseReceiptId))){
                if(!Eligible(world,witness.Observer)||!physics.Identifies(witness.Observer,state.Settings.TargetId))continue;
                var cause=state.Receipts.Single(r=>r.Id==witness.CauseReceiptId).Record;
                ObserveRiskStage(world,witness.Observer,cause,"Result","CausedOutcome","경고 뒤의 장치 작동부터 같은 대상이 쓰러질 때까지 끊김 없이 지켜보았다.",cause.FromTick,world.Tick+1);
            }
        }
        string ObserveRiskStage(MansionWorld world,string observer,KnownRecord seen,string stage,string predicate,string text,long from,long to)
        {
            Observe(world,observer,predicate,state.Settings.ActorId,predicate=="CausedOutcome"?CausalEvidence.OutcomeValue(seen.ActivationId,seen.OutcomeTarget):seen.DeviceId,text+"\n작동 번호: "+seen.ActivationId+" · 안내 개정: "+seen.ActionRevision,from,to,true,world.Resident(state.Settings.TargetId).Position,new[]{"직접 관측한 신원·장치 표시와 해당 시간의 동작"},new[]{"사적인 동기·관측 밖 행동·공식 사망 확인은 별도"});
            var receipt=state.Receipts.Last();var record=receipt.Record;
            record.ActivationId=seen.ActivationId;record.DeviceId=seen.DeviceId;record.OutcomeTarget=seen.OutcomeTarget;record.ActionDefinition=seen.ActionDefinition;record.ActionRevision=seen.ActionRevision;record.CausalStage=stage;
            // One observer is one source group, even when several stages were observed.
            record.ProvenanceKey="WITNESS_L"+world.Loop+"_"+CaseId+"_"+observer;
            return receipt.Id;
        }
        static void ValidateRiskWitnesses(MansionIncidentSnapshot s,MansionWorld world)
        {
            if(s.RiskWitnesses.Select(w=>w.Observer).Distinct().Count()!=s.RiskWitnesses.Length)throw new ArgumentException("Duplicate risk observer");
            MansionCaseReceipt Receipt(string id,string observer,string stage)=>s.Receipts.SingleOrDefault(r=>r.Id==id&&r.Observer==observer&&r.Record.CausalStage==stage);
            foreach(var witness in s.RiskWitnesses){
                var warning=Receipt(witness.WarningReceiptId,witness.Observer,"WarningShown");
                if(warning==null||witness.LastTick<warning.Record.FromTick||witness.LastTick>world.Tick||warning.Record.ActivationId!=s.ActivationId)throw new ArgumentException("Missing observed risk display");
                if(!string.IsNullOrEmpty(witness.ContinuationReceiptId)){
                    var continued=Receipt(witness.ContinuationReceiptId,witness.Observer,"ContactContinued");
                    if(continued==null||continued.Record.ActivationId!=warning.Record.ActivationId||continued.Record.FromTick<=warning.Record.FromTick||continued.Record.FromTick!=s.RiskContinuedTick)throw new ArgumentException("Missing observed risk continuation");
                }
            }
            foreach(var witness in s.Witnesses.Where(w=>!string.IsNullOrEmpty(w.CauseReceiptId))){
                var cause=Receipt(witness.CauseReceiptId,witness.Observer,"Cause");
                if(cause==null||cause.Record.FromTick!=s.CauseTick||cause.Record.ActivationId!=s.ActivationId||!witness.ActorIdentified||!witness.TargetIdentified)throw new ArgumentException("Missing observed cause");
            }
            foreach(var receipt in s.Receipts.Where(r=>!string.IsNullOrEmpty(r.Record.ActivationId))){
                var r=receipt.Record;
                if(s.Settings.ExplicitTestSession||r.Kind!="Visual"||!r.IdentityConfirmed||r.SubjectId!=s.Settings.ActorId||r.DeviceId!=s.Settings.ObjectId||r.OutcomeTarget!=s.Settings.TargetId||r.ActionDefinition!=s.Settings.Execution.Definition.Id||r.ActionRevision!=s.Settings.Execution.Definition.Revision||r.ProvenanceKey!="WITNESS_L"+world.Loop+"_"+s.Settings.Id+"_"+receipt.Observer)throw new ArgumentException("Invalid observed display binding");
                if(!new[]{"WarningShown","ContactContinued","Cause","Result"}.Contains(r.CausalStage))throw new ArgumentException("Invalid observed risk stage");
                bool Earlier(string stage)=>s.Receipts.Any(p=>p.Observer==receipt.Observer&&p.Record.ActivationId==r.ActivationId&&p.Record.CausalStage==stage&&p.Record.FromTick<=r.FromTick);
                if(r.CausalStage=="WarningShown"&&(r.Predicate!="DeviceWarning"||r.Value!=r.DeviceId)||r.CausalStage=="ContactContinued"&&(r.Predicate!="UsedObject"||r.Value!=r.DeviceId||!Earlier("WarningShown"))||r.CausalStage=="Cause"&&(r.Predicate!="UsedObject"||r.Value!=r.DeviceId||r.FromTick!=s.CauseTick||!Earlier("ContactContinued")))throw new ArgumentException("Incomplete observed risk chain");
                if(r.CausalStage=="Result"&&(s.ResultTick<0||r.FromTick!=s.CauseTick||r.ToTick!=s.ResultTick+1||r.Predicate!="CausedOutcome"||r.Value!=CausalEvidence.OutcomeValue(r.ActivationId,r.OutcomeTarget)))throw new ArgumentException("Invalid continuous observed result");
                if(r.CausalStage=="Result"&&(!Earlier("Cause")||!s.Witnesses.Any(w=>w.Observer==receipt.Observer&&w.Continuous&&!string.IsNullOrEmpty(w.CauseReceiptId))))throw new ArgumentException("Interrupted observed result chain");
            }
        }
    }
}
