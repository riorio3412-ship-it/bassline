using System;
using System.Linq;
using BASSLINE.World.Mansion;

namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        bool RescueControls(string actor)=>incidents.All().Any(c=>c.RescuerId==actor);
        MansionIncident RescueForTarget(string target)=>incidents.All().FirstOrDefault(c=>c.TargetId==target&&c.RescueAvailable);

        void CancelPlayerRescue(string reason)
        {
            if(World.Paused)return;
            foreach(var incident in incidents.All().Where(c=>c.RescuerId=="CH_01")){
                incident.CancelRescue(World,"CH_01",reason);message="도움을 중단했습니다. 다시 곁에서 시작할 수 있습니다.";
            }
        }

        string RescueFeedback()
        {
            if(World.PhysicalBand("CH_01")=="Critical")return "몸을 움직일 수 없습니다. 가까운 사람의 도움이 필요합니다.";
            if(weaponRinse.Running)return "물건을 씻는 중 · "+(weaponRinse.Elapsed*100/180)+"% · 움직이면 멈춥니다.";
            if(toolPress.Running)return (toolPress.Phase=="Reaching"?"손을 뻗는 중":toolPress.Phase=="Pressing"?"표면에 도구를 대는 중":"손을 거두는 중")+" · "+(toolPress.ElapsedTicks*100/PressEndTicks)+"% · 움직이면 멈춥니다.";
            var task=incidents.All().FirstOrDefault(c=>c.RescuerId=="CH_01");
            return task==null?message:"돕는 중 · "+(100*task.RescueProgress/task.RescueDuration)+"% · 움직이면 중단됩니다.";
        }

        string HelpResident(string target)
        {
            var incident=RescueForTarget(target);
            if(incident==null||!CanReach("CH_01",target,2)||!CanSee("CH_01",target))return "더 가까이 다가가서 도와주세요.";
            if(incident.RescuerId=="CH_01")return "돕고 있습니다. 곁에 잠시 머물러 주세요.";
            if(incident.RescuerId!="")return "다른 사람이 돕고 있습니다.";
            if(World.Resident("CH_01").HeldObject!="")return "손에 든 물건을 내려놓고 도와주세요.";
            EndConversation();CancelInspection();CancelItemExchange();CancelAppointmentCard();StopWaiting("도움을 주려고 멈췄어요.");
            move=default;running=false;
            string result=incident.BeginRescue(World,"CH_01",this);
            return result=="Started"?"돕기 시작했습니다. 곁에 잠시 머물러 주세요.":"지금은 도울 수 없습니다.";
        }

        void AdvanceNearbyRescues()
        {
            // Choice follows a newly received direct visual record and current physical reach.
            // No unseen victim lookup, countdown knowledge, or magically arriving helper.
            if(World.Tick%15!=0)return;
            foreach(var actor in World.Residents.Where(r=>r.Id!="CH_01"&&World.CanAct(r.Id)).OrderBy(r=>r.Id,StringComparer.Ordinal)){
                if(RescueControls(actor.Id)||incidents.Controls(actor.Id)||ResponseControls(actor.Id)||ReturnTaskControls(actor.Id)||PlayerTalkingTo(actor.Id)||actor.HeldObject!=""||new[]{"Sleep","Sleeping"}.Contains(actor.Activity))continue;
                var own=Knowledge.For(actor.Id);
                foreach(var seen in own.Records().Where(r=>r.Direct&&r.Predicate=="PhysicalCondition"&&r.Value=="Critical"&&r.IdentityConfirmed).OrderByDescending(r=>r.ReceivedTick)){
                    var incident=RescueForTarget(seen.SubjectId);
                    if(incident==null||incident.RescuerId!=""||seen.SubjectId==actor.Id||!CanReach(actor.Id,seen.SubjectId,2)||!CanSee(actor.Id,seen.SubjectId))continue;
                    InterruptResidentConversation(actor.Id);
                    incident.BeginRescue(World,actor.Id,this);break;
                }
            }
        }

        void UpdateRescueFeedback()
        {
            for(int i=World.Events.Count-1;i>=0&&World.Events[i].Tick==World.Tick;i--){
                var e=World.Events[i];if(e.Actor!="CH_01"||e.Type!="IncidentRiskResolved"&&e.Type!="RescueInterrupted")continue;
                message=e.Type=="IncidentRiskResolved"?"도움을 마쳤습니다. 상대가 다시 움직일 수 있습니다.":"도움을 끝내지 못했습니다.";break;
            }
        }
    }
}
