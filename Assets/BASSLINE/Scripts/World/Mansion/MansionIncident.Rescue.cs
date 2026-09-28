using System;
using System.Linq;
using BASSLINE.Core;

namespace BASSLINE.World.Mansion
{
    public sealed partial class MansionIncident
    {
        // Registered fictional X31 intervention. Other incident templates need their own rule.
        // No treatment, instant recovery, remote aid, or irreversible outcome reversal is implied.
        public const int X31RescueTicks=120;
        public bool RiskResolved=>state.Stage=="RiskResolved";
        public string RescuerId=>state.Rescuer??"";
        public int RescueProgress=>state.RescueProgress;
        public int RescueDuration=>RequiredRescueTicks;
        public bool RescueAvailable=>state.Settings!=null&&(state.Settings.PlayerInitiated||state.Settings.ExplicitTestSession||state.Settings.Execution?.Definition.ActionId=="ContactOutcome")&&state.Stage=="CauseCommitted"&&state.Reservation;
        string RescueActivity=>"Rescue:"+CaseId;

        public string BeginRescue(MansionWorld world,string helper,IMansionIncidentPhysics physics)
        {
            if(world.Paused||!RescueAvailable||world.Tick>=state.DueTick||helper==TargetId||!world.CanAct(helper))return "Unavailable";
            if(RescuerId!="")return RescuerId==helper?"InProgress":"Occupied";
            var actor=world.Resident(helper);
            if(actor.HeldObject!=""||actor.Activity.StartsWith("Rescue:",StringComparison.Ordinal)||!physics.CanReach(helper,TargetId,2)||!physics.CanSee(helper,TargetId))return "Unavailable";
            world.StopResidentActivity(helper);
            actor.Phase="Performing";actor.Activity=RescueActivity;actor.ActivityTicks=int.MaxValue;
            state.Rescuer=helper;state.RescueStartedTick=world.Tick;state.RescueLastTick=world.Tick;state.RescueProgress=0;
            state.RescuerPosition=actor.Position;state.RescuePosition=world.Resident(TargetId).Position;
            world.Emit("RescueStarted",helper,TargetId,CaseId);return "Started";
        }

        public void CancelRescue(MansionWorld world,string helper,string reason)
        {
            if(world.Paused||RescuerId==""||RescuerId!=helper)return;
            world.Emit("RescueInterrupted",helper,TargetId,CaseId+":"+reason);
            ReleaseRescuer(world);
        }

        void ReleaseRescuer(MansionWorld world)
        {
            string helper=RescuerId;
            if(helper!=""&&world.Resident(helper).Activity==RescueActivity)world.StopResidentActivity(helper);
            state.Rescuer="";state.RescueProgress=0;state.RescueStartedTick=-1;state.RescueLastTick=-1;
        }

        bool AdvanceRescue(MansionWorld world,IMansionIncidentPhysics physics)
        {
            if(RescuerId=="")return false;
            string helper=RescuerId;var actor=world.Resident(helper);
            // The deadline is exclusive: a late completion cannot undo the result of this tick.
            if(world.Tick>=state.DueTick||!world.CanAct(helper)||actor.Activity!=RescueActivity||actor.Phase!="Performing"||actor.HeldObject!=""||
                world.Tick!=state.RescueLastTick+1||actor.Position.Distance(state.RescuerPosition)>.15||world.Resident(TargetId).Position.Distance(state.RescuePosition)>.15||
                !physics.CanReach(helper,TargetId,2)||!physics.CanSee(helper,TargetId)){
                CancelRescue(world,helper,"ContactOrActionLost");return false;
            }
            state.RescueLastTick=world.Tick;
            if(++state.RescueProgress<RequiredRescueTicks)return false;
            long sequence=world.ResolveCaseRisk(CaseId,helper);
            state.RiskResolvedTick=world.Tick;state.RiskResolvedBy=helper;state.RiskResolutionEvent="M_EVENT_"+sequence;
            state.Stage="RiskResolved";state.Reservation=false;state.Reason="RegisteredRescueCompleted";
            if(state.Plan!=null)state.Plan.State="RiskResolved";
            foreach(var observer in world.Residents.Where(r=>world.CanAct(r.Id)&&r.Activity!="Sleep"&&r.Activity!="Sleeping"&&physics.CanSee(r.Id,TargetId))){
                bool identity=physics.Identifies(observer.Id,TargetId);
                Observe(world,observer.Id,"PhysicalCondition",identity?TargetId:"UNKNOWN_ACTOR","Stabilized","도움을 받은 인물이 다시 움직일 수 있게 된 모습을 보았다.",world.Tick,world.Tick+1,identity,state.RescuePosition,new[]{"관측 당시의 상태 변화"},new[]{"이전 원인·실행자·완전한 회복을 입증하지 않음"});
            }
            ReleaseRescuer(world);return true;
        }

        void ObserveCriticalCondition(MansionWorld world,IMansionIncidentPhysics physics)
        {
            foreach(var observer in world.Residents.Where(r=>world.CanAct(r.Id)&&r.Activity!="Sleep"&&r.Activity!="Sleeping"&&r.Id!=TargetId&&physics.CanSee(r.Id,TargetId))){
                bool identity=physics.Identifies(observer.Id,TargetId);
                if(state.Receipts.Any(r=>r.Observer==observer.Id&&r.Record.Predicate=="PhysicalCondition"&&r.Record.Value=="Critical"&&r.Record.IdentityConfirmed==identity))continue;
                Observe(world,observer.Id,"PhysicalCondition",identity?TargetId:"UNKNOWN_ACTOR","Critical","인물이 움직이지 못하고 도움을 필요로 하는 모습을 보았다.",world.Tick,world.Tick+1,identity,world.Resident(TargetId).Position,new[]{"직접 본 인물의 외관 상태와 위치"},new[]{"원인·실행자·남은 시간·사망 여부는 확인하지 않음"});
            }
        }

        static void ValidateRescue(MansionIncidentSnapshot s,MansionWorld world)
        {
            int duration=s.Settings.PlayerInitiated?PlayerRescueTicks:s.Settings.ExplicitTestSession?X31RescueTicks:s.Settings.Execution.Definition.RescueTicks;
            bool resolved=s.Stage=="RiskResolved";string helper=s.Rescuer??"";
            if(resolved){
                if(s.CauseTick<0||s.ResultTick>=0||s.Reservation||s.RiskResolvedTick<=s.CauseTick||s.RiskResolvedTick>=s.DueTick||s.RiskResolvedTick>world.Tick||
                    !world.Residents.Any(r=>r.Id==s.RiskResolvedBy&&r.Id!=s.Settings.TargetId)||
                    !world.Events.Any(e=>"M_EVENT_"+e.Sequence==s.RiskResolutionEvent&&e.Type=="IncidentRiskResolved"&&e.Tick==s.RiskResolvedTick&&e.Actor==s.RiskResolvedBy&&e.Target==s.Settings.TargetId&&e.Detail==s.Settings.Id)||
                    !world.Events.Any(e=>e.Type=="RescueStarted"&&e.Actor==s.RiskResolvedBy&&e.Target==s.Settings.TargetId&&e.Detail==s.Settings.Id&&e.Tick==s.RiskResolvedTick-duration))throw new ArgumentException("Invalid resolved risk chain");
            }else if(!string.IsNullOrEmpty(s.RiskResolutionEvent)||!string.IsNullOrEmpty(s.RiskResolvedBy))throw new ArgumentException("Unexpected rescue result");
            if(helper==""){if(s.RescueProgress!=0)throw new ArgumentException("Rescue progress without a helper");return;}
            if(s.Stage!="CauseCommitted"||!s.Reservation||!world.CanAct(helper)||helper==s.Settings.TargetId||world.Resident(helper).Activity!="Rescue:"+s.Settings.Id||world.Resident(helper).Phase!="Performing"||world.Resident(helper).HeldObject!=""||
                s.RescueStartedTick<s.CauseTick||s.RescueLastTick!=world.Tick||s.RescueProgress<0||s.RescueProgress>=duration||s.RescueProgress!=s.RescueLastTick-s.RescueStartedTick||world.Tick>=s.DueTick||
                !s.RescuerPosition.Finite()||!s.RescuePosition.Finite()||world.Resident(helper).Position.Distance(s.RescuerPosition)>.15||world.Resident(s.Settings.TargetId).Position.Distance(s.RescuePosition)>.15||
                !world.Events.Any(e=>e.Type=="RescueStarted"&&e.Actor==helper&&e.Target==s.Settings.TargetId&&e.Detail==s.Settings.Id&&e.Tick==s.RescueStartedTick))throw new ArgumentException("Invalid active rescue");
        }
    }
}
