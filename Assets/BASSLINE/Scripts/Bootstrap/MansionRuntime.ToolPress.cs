using System;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        const int PressReachTicks=42,PressHoldTicks=18,PressReturnTicks=36;
        const int PressEndTicks=PressReachTicks+PressHoldTicks+PressReturnTicks;
        ToolPressState toolPress=new ToolPressState();
        MansionTraceSource HeldPigmentTool(string actor="CH_01")=>traceSources.FirstOrDefault(s=>s&&s.isActiveAndEnabled&&s.TransfersPigment&&s.Face&&TraceSourceId(s)==World.Resident(actor).HeldObject&&World.Object(TraceSourceId(s)).Location=="Hand"&&World.Object(TraceSourceId(s)).Owner==actor);
        MansionTraceSurface PigmentSurface(string id)=>traceSurfaces.FirstOrDefault(s=>s&&s.isActiveAndEnabled&&TraceSurfaceId(s)==id&&s.ContactSurface&&s.ContactSurface.enabled&&s.TraceMaterial);
        string BeginToolPress(string id)
        {
            if(toolPress.Running)return "표면에 도구를 대고 있어요.";
            var source=HeldPigmentTool();var surface=PigmentSurface(id);var rig=Arm("CH_01");
            if(World.Paused||!World.CanAct("CH_01")||!source||!surface||!rig||itemExchange.Handoff.Running||PlayerSurfaceRunning||WritingCard||conversationPlayback.Phase=="Speaking"||RescueControls("CH_01"))return "지금은 표면에 도구를 댈 수 없어요.";
            string prepared=PrepareSurfacePress("CH_01",id,out var candidate);
            if(prepared!="Ready")return prepared;
            CancelInspection();StopWaiting("도구를 사용하려고 멈췄어요.");move=default;running=false;toolPress=candidate;
            return "표면에 도구를 대고 있어요. 움직이면 멈춥니다.";
        }
        static void PressPose(ToolPressState p,int tick,out Vector3 position,out Quaternion rotation)
        {
            float t=tick<=PressReachTicks?Mathf.SmoothStep(0,1,tick/(float)PressReachTicks):tick<=PressReachTicks+PressHoldTicks?1:1-Mathf.SmoothStep(0,1,(tick-PressReachTicks-PressHoldTicks)/(float)PressReturnTicks);
            position=Vector3.Lerp(V(p.StartPosition),V(p.ContactPosition),t);
            rotation=Quaternion.Slerp(Quaternion.LookRotation(V(p.StartForward),V(p.StartUp)),Quaternion.LookRotation(V(p.ContactForward),V(p.ContactUp)),t);
        }
        bool PoseToolPress()=>PoseToolPress(toolPress);
        bool PoseToolPress(ToolPressState press)
        {
            if(!press.Running)return true;
            PressPose(press,press.ElapsedTicks,out var position,out var rotation);var rig=Arm(press.ActorId);
            if(!rig)return false;rig.Curl(1);return rig.Pose(position,rotation*Quaternion.Euler(0,-90,0));
        }
        bool ToolPressPathClear(ToolPressState press,int fromTick,int toTick,BoxCollider box)
        {
            var source=targets[press.ToolId].transform;var player=bodies[press.ActorId].transform;
            bool Blocks(Collider c)=>!c.transform.IsChildOf(source)&&!c.transform.IsChildOf(player);
            PressPose(press,fromTick,out var previous,out var priorRotation);PressPose(press,toTick,out var next,out var nextRotation);
            int samples=Math.Max(1,Math.Max(Mathf.CeilToInt(Vector3.Distance(previous,next)/.008f),Mathf.CeilToInt(Quaternion.Angle(priorRotation,nextRotation)/2f)));
            var relativeRotation=Quaternion.Inverse(source.rotation)*box.transform.rotation;
            var localCentre=source.InverseTransformPoint(box.transform.TransformPoint(box.center));
            var half=Vector3.Scale(box.size*.5f,box.transform.lossyScale);
            for(int sample=0;sample<=samples;sample++){
                float t=sample/(float)samples;var point=Vector3.Lerp(previous,next,t);var rotation=Quaternion.Slerp(priorRotation,nextRotation,t)*relativeRotation;
                var centre=point+Quaternion.Slerp(priorRotation,nextRotation,t)*localCentre;
                int count=Physics.OverlapBoxNonAlloc(centre,half,overlap,rotation,~0,QueryTriggerInteraction.Ignore);
                if(count==overlap.Length)return false;for(int i=0;i<count;i++)if(Blocks(overlap[i]))return false;
                if(sample==0)continue;
                float oldT=(sample-1)/(float)samples;var oldRotation=Quaternion.Slerp(priorRotation,nextRotation,oldT);
                var oldCentre=Vector3.Lerp(previous,next,oldT)+oldRotation*localCentre;var delta=centre-oldCentre;
                if(delta.sqrMagnitude<1e-10f)continue;
                float rotationPadding=half.magnitude*Quaternion.Angle(oldRotation,Quaternion.Slerp(priorRotation,nextRotation,t))*Mathf.Deg2Rad*.5f;
                count=Physics.BoxCastNonAlloc(oldCentre,half+Vector3.one*rotationPadding,delta.normalized,rayHits,Quaternion.Slerp(oldRotation,Quaternion.Slerp(priorRotation,nextRotation,t),.5f)*relativeRotation,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
                if(count==rayHits.Length)return false;for(int i=0;i<count;i++)if(Blocks(rayHits[i].collider))return false;
            }
            return true;
        }
        bool PressArmSpaceClear()=>PressArmSpaceClear(toolPress);
        bool PressArmSpaceClear(ToolPressState press)
        {
            var arm=Arm(press.ActorId);if(!arm)return false;
            bool Segment(Vector3 a,Vector3 b,float radius){
                int count=Physics.OverlapCapsuleNonAlloc(a,b,radius,overlap,~0,QueryTriggerInteraction.Ignore);if(count==overlap.Length)return false;
                for(int i=0;i<count;i++)if(!overlap[i].transform.IsChildOf(bodies[press.ActorId].transform)&&!overlap[i].transform.IsChildOf(targets[press.ToolId].transform))return false;return true;
            }
            return Segment(arm.Upper.position,arm.Forearm.position,.05f)&&Segment(arm.Forearm.position,arm.Wrist.position,.035f);
        }
        void AdvanceToolPress()
        {
            if(!toolPress.Running||World.Paused)return;
            var p=toolPress;var source=HeldPigmentTool();var surface=PigmentSurface(p.SurfaceId);
            if(World.Tick!=p.LastTick+1||!World.CanAct("CH_01")||!source||TraceSourceId(source)!=p.ToolId||source.Face.Pattern.Key!=p.Pattern||!surface||World.Resident("CH_01").Position.Distance(p.ActorPosition)>.025||Mathf.Abs(Mathf.DeltaAngle((float)p.BodyYaw,(float)World.Yaw))>50||!CanSee("CH_01",p.SurfaceId)||itemExchange.Handoff.Running||PlayerSurfaceRunning||WritingCard||conversationPlayback.Phase=="Speaking") {CancelToolPress("도구를 사용하던 동작을 멈췄어요.");return;}
            if(Vector3.Distance(surface.ContactSurface.ClosestPoint(V(p.SurfacePoint)+V(p.SurfaceNormal)*.02f),V(p.SurfacePoint))>.004f){CancelToolPress("표면의 위치가 달라져 멈췄어요.");return;}
            int next=Math.Min(PressEndTicks,p.ElapsedTicks+1);
            if(!(source.GetComponent<FixtureObjectBody>().Collider is BoxCollider box)||!ToolPressPathClear(p,p.ElapsedTicks,next,box)){CancelToolPress("사이에 물체가 있어 도구를 멈췄어요.");return;}
            int prior=p.ElapsedTicks;p.ElapsedTicks=next;
            if(!PoseToolPress()||!PressArmSpaceClear()){p.ElapsedTicks=prior;CancelToolPress("팔을 뻗을 공간이 부족해 멈췄어요.");return;}
            PressPose(p,next,out var position,out _);p.Position=P(position);p.LastTick=World.Tick;
            p.Phase=next<PressReachTicks?"Reaching":next<PressReachTicks+PressHoldTicks?"Pressing":"Returning";
            if(next==PressEndTicks){p.Phase="Completed";message=p.MarkId==""?"도구를 댔지만 확인할 자국은 남지 않았어요.":"표면에 자국이 남았어요. 관찰하기로 살펴볼 수 있어요.";}
        }
        void CancelToolPress(string reason="도구를 사용하던 동작을 멈췄어요.")
        {
            if(!toolPress.Running)return;toolPress.Phase="Cancelled";toolPress.Reason=reason;message=reason;PoseRestArms("CH_01");
            // Marks already deposited are physical history, never undone by cancellation.
        }
        void ObserveToolPressContact(ToolPressState press,MansionTraceSource source,MansionTraceSurface surface,Vector3 point)
        {
            // Only called after the ordinary physical adapter actually finds a supported contact.
            // No knowledge comes from knowing who owns a private plan or who will be a culprit.
            string action="TOOL_CONTACT_L"+World.Loop+"_"+press.StartedTick+"_"+press.ToolId;
            foreach(var resident in World.Residents.Where(r=>World.CanAct(r.Id))){
                string observer=resident.Id;
                if(observer!=press.ActorId&&(!SeesPassagePerson(observer,bodies[press.ActorId],out bool identified)||!identified)||!CanSee(observer,press.ToolId)||!Visible(observer,point,7,true))continue;
                string root=action+"_WITNESS_"+observer;
                if(Knowledge.For(observer).Records().Any(r=>r.Direct&&r.ProvenanceKey==root))continue;
                Knowledge.Observe(observer,new KnownRecord{Kind="Visual",Source=observer,SubjectId=press.ActorId,Predicate="UsedObject",Value=press.ToolId,ProvenanceKey=root,IdentityConfirmed=true,Text=KoreanText.AsSubject(NameOf(press.ActorId))+" "+KoreanText.AsObject(NameOf(press.ToolId))+" "+NameOf(press.SurfaceId)+"에 대는 모습을 보았다.",PlaceId=PlaceOf(point),Position=P(point),FromTick=World.Tick,ToTick=World.Tick+1,Supports=new[]{"실제로 본 사람·도구·접촉 장면"},DoesNotEstablish=new[]{"이전에 같은 도구를 쓴 사람", "표면의 다른 자국을 남긴 사람", "사용 목적·사건 원인·다른 대상에게 생긴 결과"}},World.Tick);
            }
        }
        void ValidateToolPress(BASSLINE.Save.MansionSessionSnapshot s)=>ValidateToolPress(s,s.ToolPress,"CH_01");
        void ValidateToolPress(BASSLINE.Save.MansionSessionSnapshot s,ToolPressState p,string actor)
        {
            if(p==null||p.ActorId!=actor||!new[]{"Idle","Reaching","Pressing","Returning","Completed","Cancelled"}.Contains(p.Phase))throw new System.IO.InvalidDataException("도구 사용 상태가 올바르지 않습니다.");
            if(p.Phase=="Idle")return;
            var source=traceSources.FirstOrDefault(x=>TraceSourceId(x)==p.ToolId);
            if(!source||!source.Face||!traceSurfaces.Any(x=>TraceSurfaceId(x)==p.SurfaceId)||p.Pattern!=source.Face.Pattern.Key||p.StartedTick<0||p.LastTick<p.StartedTick||p.LastTick>s.World.Tick||p.ElapsedTicks<0||p.ElapsedTicks>PressEndTicks||p.LastTick-p.StartedTick!=p.ElapsedTicks||double.IsNaN(p.BodyYaw)||double.IsInfinity(p.BodyYaw)||new[]{p.ActorPosition,p.StartPosition,p.ContactPosition,p.Position,p.StartForward,p.StartUp,p.ContactForward,p.ContactUp,p.SurfacePoint,p.SurfaceNormal}.Any(v=>!v.Finite()))throw new System.IO.InvalidDataException("도구 동작의 대상이나 시간이 일치하지 않습니다.");
            bool Unit(Point3 v)=>Math.Abs(v.X*v.X+v.Y*v.Y+v.Z*v.Z-1)<.01;
            double Dot(Point3 a,Point3 b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
            if(!Unit(p.StartForward)||!Unit(p.StartUp)||!Unit(p.ContactForward)||!Unit(p.ContactUp)||!Unit(p.SurfaceNormal)||Math.Abs(Dot(p.StartForward,p.StartUp))>.01||Math.Abs(Dot(p.ContactForward,p.ContactUp))>.01)throw new System.IO.InvalidDataException("도구 방향이 올바르지 않습니다.");
            PressPose(p,p.ElapsedTicks,out var point,out _);if(Vector3.Distance(point,V(p.Position))>.002f)throw new System.IO.InvalidDataException("도구 동작 위치가 일치하지 않습니다.");
            if(p.MarkId!=""&&!s.SurfaceTraces.Marks.Any(m=>m.Id==p.MarkId&&m.SurfaceId==p.SurfaceId&&m.Pattern.Mask==SurfacePattern.Mirror(source.Face.Pattern.Mask)&&m.Pattern.Colour==source.Face.Pattern.Colour&&m.Pattern.SizeMm==source.Face.Pattern.SizeMm&&m.DepositedTick<=p.LastTick))throw new System.IO.InvalidDataException("도구 동작의 자국이 일치하지 않습니다.");
            if(p.Phase=="Completed"&&p.ElapsedTicks!=PressEndTicks)throw new System.IO.InvalidDataException("끝나지 않은 도구 동작입니다.");
            var rotation=Quaternion.LookRotation(V(p.ContactForward),V(p.ContactUp));
            var facePoint=source.transform.InverseTransformPoint(source.Face.transform.position);
            if(Vector3.Distance(V(p.ContactPosition)+rotation*facePoint,V(p.SurfacePoint)+V(p.SurfaceNormal)*.0015f)>.002f||p.ContactPosition.Distance(p.ActorPosition)>2.5)throw new System.IO.InvalidDataException("접촉면과 도구 위치가 맞지 않습니다.");
            if(p.Running){var item=s.World.Objects.Single(o=>o.Id==p.ToolId);var player=s.World.Residents.Single(a=>a.Id==actor);
                if(!player.Alive||!player.Present||player.HeldObject!=p.ToolId||item.Owner!=actor||item.Location!="Hand"||player.Position.Distance(p.ActorPosition)>.025||item.Position.Distance(p.Position)>.02||s.World.PauseOwners.Contains("M_COURT")||s.Conversation.Phase=="Speaking"&&(actor=="CH_01"||s.Conversation.SpeakerId==actor||s.Conversation.RecipientId==actor)||s.ItemExchange.Handoff.Running&&(s.ItemExchange.Handoff.Giver==actor||s.ItemExchange.Handoff.Receiver==actor)||p.ElapsedTicks>=PressEndTicks||p.Phase!=(p.ElapsedTicks<PressReachTicks?"Reaching":p.ElapsedTicks<PressReachTicks+PressHoldTicks?"Pressing":"Returning"))throw new System.IO.InvalidDataException("도구를 사용 중인 손과 동작 상태가 맞지 않습니다.");
            }
        }
    }
}
