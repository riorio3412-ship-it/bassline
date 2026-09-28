using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.World.Mansion
{
    public sealed partial class MansionIncident
    {
        // A registered device displays its warning during actual contact. The selected actor
        // must keep that contact after a cancellation window; a remembered intention is not a commit.
        bool MaintainRiskNotice(MansionWorld world,IMansionIncidentPhysics physics)
        {
            if(state.Settings.ExplicitTestSession)return true;
            if(!((physics as IMansionIncidentRiskDisplay)?.ShowVisibleWarning(state.Settings.Copy())??false)){
                state.ContactProgress=0;AbandonRiskNotice(world);Release(world);state.Reason="WarningDisplayUnavailable";return false;
            }
            if(state.RiskNoticeTick<0){
                state.RiskNoticeTick=world.Tick;state.RiskContinuedTick=-1;
                world.Emit("IncidentRiskNotice",ActorId,state.Settings.ObjectId,CaseId);
                state.ActivationId="ACT_L"+world.Loop+"_E"+world.Events.Last().Sequence;return false;
            }
            CaptureRiskWitnesses(world,physics);
            if(world.Tick-state.RiskNoticeTick<state.Settings.Execution.Definition.RiskNoticeTicks)return false;
            if(state.RiskContinuedTick<0){state.RiskContinuedTick=world.Tick;world.Emit("IncidentRiskContinued",ActorId,state.Settings.ObjectId,CaseId);}
            CaptureWitnessContinuation(world);
            return true;
        }
        void AbandonRiskNotice(MansionWorld world)
        {
            if(state.Settings==null||state.Settings.ExplicitTestSession||state.CauseTick>=0||state.RiskNoticeTick<0)return;
            world.Emit("IncidentContactAbandoned",ActorId,state.Settings.ObjectId,CaseId);
            state.RiskNoticeTick=-1;state.RiskContinuedTick=-1;state.ActivationId="";
            state.RiskWitnesses=Array.Empty<MansionRiskWitness>();
        }
        static void ValidateRiskNotice(MansionIncidentSnapshot s,MansionWorld world)
        {
            if(s.Settings.ExplicitTestSession||s.Settings.PlayerInitiated)return;
            if(s.RiskNoticeTick>=0&&!world.Events.Any(e=>e.Type=="IncidentRiskNotice"&&e.Detail==s.Settings.Id&&e.Tick==s.RiskNoticeTick&&s.ActivationId=="ACT_L"+world.Loop+"_E"+e.Sequence))throw new ArgumentException("Risk notice activation differs");
            if(s.RiskNoticeTick< -1||s.RiskContinuedTick< -1||s.RiskNoticeTick>world.Tick||s.RiskContinuedTick>world.Tick)throw new ArgumentException("Invalid risk notice clock");
            if(s.RiskNoticeTick>=0&&!world.Events.Any(e=>e.Type=="IncidentRiskNotice"&&e.Actor==s.Settings.ActorId&&e.Target==s.Settings.ObjectId&&e.Detail==s.Settings.Id&&e.Tick==s.RiskNoticeTick))throw new ArgumentException("Missing physical warning");
            if(s.RiskContinuedTick>=0&&(s.RiskNoticeTick<0||s.RiskContinuedTick-s.RiskNoticeTick<s.Settings.Execution.Definition.RiskNoticeTicks||!world.Events.Any(e=>e.Type=="IncidentRiskContinued"&&e.Actor==s.Settings.ActorId&&e.Target==s.Settings.ObjectId&&e.Detail==s.Settings.Id&&e.Tick==s.RiskContinuedTick)))throw new ArgumentException("Missing continued contact after warning");
            if(s.CauseTick>=0&&(s.RiskContinuedTick<0||s.CauseTick<s.RiskContinuedTick+s.Settings.ContactTicks-1)||s.ContactProgress>0&&s.RiskContinuedTick<0)throw new ArgumentException("Cause preceded risk acknowledgement");
        }
    }
}
