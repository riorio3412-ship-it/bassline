using System;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.Save;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        const string SurfaceWorkPrefix="MarkSurface:";
        ResidentToolWork[] residentToolWork=Array.Empty<ResidentToolWork>();
        bool ResidentToolBusy(string actor)=>residentToolWork.Any(w=>w.ActorId==actor&&w.Motion.Running)||residentToolPickups.Any(p=>p.ActorId==actor&&p.Running)||residentToolReturns.Any(p=>p.ActorId==actor&&p.Running)||residentReadings.Any(p=>p.ActorId==actor&&p.Running);
        ToolPressState PressForTool(string tool)=>toolPress.Running&&toolPress.ToolId==tool?toolPress:residentToolWork.Select(w=>w.Motion).FirstOrDefault(p=>p.Running&&p.ToolId==tool);
        bool ResidentCanUseTool(string actor)
        {
            if(!IntentActorAvailable(actor)||IntentControls(actor))return false;
            var h=itemExchange.Handoff;
            return !(h.Running&&(h.Giver==actor||h.Receiver==actor))&&!(SurfaceRunning&&ReturnState.Motion.Actor==actor);
        }
        string PrepareSurfacePress(string actor,string id,out ToolPressState candidate)
        {
            candidate=null;var source=HeldPigmentTool(actor);var surface=PigmentSurface(id);var rig=Arm(actor);
            if(!source||!surface||!rig)return "손에 사용할 도구가 없거나 표면이 준비되지 않았어요.";
            if(!(source.GetComponent<FixtureObjectBody>().Collider is BoxCollider box)||source.transform.lossyScale!=Vector3.one||source.Face.transform.lossyScale!=Vector3.one||surface.transform.lossyScale!=Vector3.one)return "이 도구와 표면의 사용 동작이 준비되지 않았어요.";
            if(!CanReach(actor,id,2.5)||!CanSee(actor,id))return "표면이 보이는 가까운 곳으로 다가가 주세요.";
            Physics.SyncTransforms();var body=bodies[actor];var eye=actor=="CH_01"?cameraView.transform.position:body.transform.position+Vector3.up*body.Height*.88f;
            var towards=surface.ContactSurface.bounds.center-eye;
            if(towards.sqrMagnitude<.0001f||!surface.ContactSurface.Raycast(new Ray(eye,towards.normalized),out var hit,2.5f)||!Visible(actor,hit.point,2.5f,true))return "닿을 표면을 확인할 수 없어요.";
            var face=source.Face.transform;var normal=hit.normal;
            var up=Vector3.ProjectOnPlane(Vector3.up,normal).normalized;if(up.sqrMagnitude<.5f)up=Vector3.ProjectOnPlane(body.transform.forward,normal).normalized;
            if(up.sqrMagnitude<.5f)return "도구를 댈 방향을 잡을 수 없어요.";
            var localFaceRotation=Quaternion.Inverse(source.transform.rotation)*face.rotation;
            var rotation=Quaternion.LookRotation(-normal,up)*Quaternion.Inverse(localFaceRotation);
            var contact=hit.point+normal*.0015f-rotation*source.transform.InverseTransformPoint(face.position);
            var p=new ToolPressState{ActorId=actor,Phase="Reaching",ToolId=TraceSourceId(source),SurfaceId=id,Pattern=source.Face.Pattern.Key,StartedTick=World.Tick,LastTick=World.Tick,BodyYaw=actor=="CH_01"?World.Yaw:World.Resident(actor).Yaw,ActorPosition=P(body.transform.position),StartPosition=P(source.transform.position),Position=P(source.transform.position),ContactPosition=P(contact),StartForward=P(source.transform.forward),StartUp=P(source.transform.up),ContactForward=P(rotation*Vector3.forward),ContactUp=P(rotation*Vector3.up),SurfacePoint=P(hit.point),SurfaceNormal=P(normal)};
            bool reachable=rig.Pose(contact,rotation*Quaternion.Euler(0,-90,0));bool armClear=reachable&&PressArmSpaceClear(p);PoseRestArms(actor);
            if(!reachable)return "손이 닿지 않아요. 표면 쪽으로 조금 더 다가가 주세요.";
            if(!armClear||!ToolPressPathClear(p,0,PressReachTicks,box))return "도구와 팔을 뻗을 공간이 부족해요. 표면 앞을 비워 주세요.";
            candidate=p;return "Ready";
        }
        void BeginResidentToolWork()
        {
            if(World.Paused||World.Tick%30!=0)return;
            // This is an explicitly authored everyday activity, not an inferred crime.
            // Its identifier names a desired surface, not knowledge about its current state.
            foreach(var actor in World.Residents.Where(a=>a.Id!="CH_01"&&a.Phase=="Performing"&&(a.Activity??"").StartsWith(SurfaceWorkPrefix,StringComparison.Ordinal))){
                if(!ResidentCanUseTool(actor.Id)||ResidentToolBusy(actor.Id)||actor.ActivityTicks<PressEndTicks+1)continue;
                long end=World.Tick+actor.ActivityTicks;
                if(residentToolWork.Any(w=>w.ActorId==actor.Id&&w.Activity==actor.Activity&&w.Node==actor.Node&&w.ActivityEndTick==end))continue;
                string surface=actor.Activity.Substring(SurfaceWorkPrefix.Length);
                if(actor.HeldObject==""){TryBeginResidentToolPickup(actor.Id);continue;}
                if(PrepareSurfacePress(actor.Id,surface,out var press)!="Ready")continue;
                residentToolWork=residentToolWork.Where(w=>w.ActorId!=actor.Id).Concat(new[]{new ResidentToolWork{ActorId=actor.Id,Activity=actor.Activity,Node=actor.Node,ActivityEndTick=end,Motion=press}}).ToArray();
                World.Emit("ResidentToolWorkStarted",actor.Id,surface,press.ToolId);
            }
        }
        void AdvanceResidentToolWork()
        {
            if(World.Paused)return;
            foreach(var work in residentToolWork.Where(w=>w.Motion.Running)){
                var p=work.Motion;var actor=World.Resident(work.ActorId);var source=HeldPigmentTool(actor.Id);var surface=PigmentSurface(p.SurfaceId);
                if(!ResidentCanUseTool(actor.Id)||actor.Phase!="Performing"||actor.Activity!=work.Activity||actor.Node!=work.Node||World.Tick>=work.ActivityEndTick||World.Tick+actor.ActivityTicks!=work.ActivityEndTick||World.Tick!=p.LastTick+1||!source||TraceSourceId(source)!=p.ToolId||source.Face.Pattern.Key!=p.Pattern||!surface||actor.Position.Distance(p.ActorPosition)>.025||Mathf.Abs(Mathf.DeltaAngle((float)p.BodyYaw,(float)actor.Yaw))>50||!CanSee(actor.Id,p.SurfaceId)){
                    CancelResidentToolWork(work,"ActivityInterrupted");continue;
                }
                if(Vector3.Distance(surface.ContactSurface.ClosestPoint(V(p.SurfacePoint)+V(p.SurfaceNormal)*.02f),V(p.SurfacePoint))>.004f){CancelResidentToolWork(work,"SurfaceMoved");continue;}
                int next=Math.Min(PressEndTicks,p.ElapsedTicks+1);
                if(!(source.GetComponent<FixtureObjectBody>().Collider is BoxCollider box)||!ToolPressPathClear(p,p.ElapsedTicks,next,box)){CancelResidentToolWork(work,"ToolPathBlocked");continue;}
                int prior=p.ElapsedTicks;p.ElapsedTicks=next;
                if(!PoseToolPress(p)||!PressArmSpaceClear(p)){p.ElapsedTicks=prior;CancelResidentToolWork(work,"ArmPathBlocked");continue;}
                PressPose(p,next,out var position,out _);p.Position=P(position);p.LastTick=World.Tick;
                p.Phase=next<PressReachTicks?"Reaching":next<PressReachTicks+PressHoldTicks?"Pressing":"Returning";
                if(next==PressEndTicks){p.Phase="Completed";World.Emit("ResidentToolWorkEnded",actor.Id,p.SurfaceId,p.MarkId==""?"NoSupportedContact":"ContactCompleted");}
            }
        }
        void CancelResidentToolWork(ResidentToolWork work,string reason)
        {
            if(!work.Motion.Running)return;
            work.Motion.Phase="Cancelled";work.Motion.Reason=reason;PoseRestArms(work.ActorId);
            World.Emit("ResidentToolWorkEnded",work.ActorId,work.Motion.SurfaceId,reason);
        }
        void CancelResidentToolWork(string actor,string reason)
        {foreach(var work in residentToolWork.Where(w=>w.ActorId==actor))CancelResidentToolWork(work,reason);foreach(var pickup in residentToolPickups.Where(p=>p.ActorId==actor))CancelResidentToolPickup(pickup,reason);foreach(var returned in residentToolReturns.Where(p=>p.ActorId==actor))CancelResidentToolReturn(returned,reason);foreach(var reading in residentReadings.Where(p=>p.ActorId==actor))CancelResidentReading(reading,reason);}
        void ValidateResidentToolWork(MansionSessionSnapshot s)
        {
            if(s.ResidentToolWork==null||s.ResidentToolWork.Any(w=>w==null||w.Motion==null)||s.ResidentToolWork.Select(w=>w.ActorId).Distinct().Count()!=s.ResidentToolWork.Length)throw new System.IO.InvalidDataException("주민의 도구 작업이 중복되거나 비어 있습니다.");
            foreach(var work in s.ResidentToolWork){
                var p=work.Motion;
                if(work.ActorId=="CH_01"||!bodies.ContainsKey(work.ActorId)||p.ActorId!=work.ActorId||work.Activity!=SurfaceWorkPrefix+p.SurfaceId||!nodes.Any(n=>n.Id==work.Node)||work.ActivityEndTick<=p.StartedTick||p.Phase=="Idle")throw new System.IO.InvalidDataException("주민의 생활 행동과 도구 동작이 다릅니다.");
                ValidateToolPress(s,p,work.ActorId);
                var actor=s.World.Residents.Single(a=>a.Id==work.ActorId);
                if(p.Running&&(actor.Phase!="Performing"||actor.Activity!=work.Activity||actor.Node!=work.Node||s.World.Tick>=work.ActivityEndTick||s.World.Tick+actor.ActivityTicks!=work.ActivityEndTick))throw new System.IO.InvalidDataException("중단된 생활 행동의 도구 동작을 계속할 수 없습니다.");
            }
        }
    }
}
