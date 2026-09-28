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
        const int ToolReturnReach=54,ToolReturnLower=84,ToolReturnRelease=96,ToolReturnEnd=138;
        ResidentToolReturn[] residentToolReturns=Array.Empty<ResidentToolReturn>();
        ResidentToolReturn ReturnForTool(string tool)=>residentToolReturns.FirstOrDefault(p=>p.Running&&p.ReleasedTick<0&&p.ToolId==tool);
        ResidentToolReturn ReturnedToolPose(string tool)
        {
            var item=World.Object(tool);if(item.Location!="World")return null;
            var returned=residentToolReturns.Where(p=>p.ToolId==tool&&p.ReleasedTick>=0&&p.SupportId==item.AnchorId&&p.TargetPosition.Distance(item.Position)<.002).OrderByDescending(p=>p.ReleasedTick).FirstOrDefault();
            if(returned==null)return null;
            var last=World.Events.LastOrDefault(e=>e.Target==tool&&e.Type.StartsWith("Object",StringComparison.Ordinal));
            return last!=null&&last.Sequence==returned.ReleaseEventSequence?returned:null;
        }
        static string ToolReturnPhase(int tick)=>tick<ToolReturnReach?"Reaching":tick<ToolReturnLower?"Lowering":tick<ToolReturnRelease?"Releasing":tick<ToolReturnEnd?"Withdrawing":"Completed";
        // The original position is a remembered destination, not a promise that a
        // table still exists there. All feet must rest on the same current support.
        bool FindToolRest(string actor,string tool,Vector3 requested,Quaternion rotation,out Vector3 resting,out string supportId)
        {
            resting=requested;supportId="";var source=ObjectBodies.FirstOrDefault(o=>o.ObjectId==tool);
            if(!source||source.transform.lossyScale!=Vector3.one||!(source.Collider is BoxCollider box)||box.transform.lossyScale!=Vector3.one)return false;
            var relative=Quaternion.Inverse(source.transform.rotation)*box.transform.rotation;
            if(Vector3.Dot(rotation*relative*Vector3.up,Vector3.up)<.999f)return false;
            Collider support=null;float minimum=float.MaxValue,maximum=float.MinValue;
            foreach(var offset in new[]{new Vector2(-.9f,-.9f),new Vector2(-.9f,.9f),new Vector2(.9f,-.9f),new Vector2(.9f,.9f),Vector2.zero}){
                var local=source.transform.InverseTransformPoint(box.transform.TransformPoint(box.center+Vector3.Scale(box.size*.5f,new Vector3(offset.x,-1,offset.y))));
                var foot=requested+rotation*local;
                int count=Physics.RaycastNonAlloc(foot+Vector3.up*.03f,Vector3.down,rayHits,.06f,~0,QueryTriggerInteraction.Ignore);if(count==rayHits.Length)return false;
                var hit=Enumerable.Range(0,count).Select(i=>rayHits[i]).Where(h=>!h.transform.IsChildOf(source.transform)&&!h.transform.IsChildOf(bodies[actor].transform)).OrderBy(h=>h.distance).FirstOrDefault();
                if(!hit.collider||hit.collider.GetComponentInParent<FixtureActorBody>()||hit.collider.GetComponentInParent<FixtureObjectBody>()||Vector3.Dot(hit.normal,Vector3.up)<.999f||support&&support!=hit.collider)return false;
                support=hit.collider;float gap=foot.y-hit.point.y;minimum=Mathf.Min(minimum,gap);maximum=Mathf.Max(maximum,gap);
            }
            if(minimum<-.002f||maximum>.025f||maximum-minimum>.002f)return false;
            var prop=support.GetComponentInParent<MansionProp>();var target=support.GetComponentInParent<FixtureTarget>();
            supportId=prop?prop.ObjectId:target?target.StableId:"";if(string.IsNullOrEmpty(supportId))return false;
            resting=requested+Vector3.up*(.001f-minimum);return true;
        }
        static void ToolReturnPose(ResidentToolReturn p,int tick,out Vector3 hand,out Quaternion rotation)
        {
            var initial=Quaternion.LookRotation(V(p.StartForward),V(p.StartUp))*Quaternion.Euler(0,-90,0);
            var target=Quaternion.LookRotation(V(p.TargetForward),V(p.TargetUp))*Quaternion.Euler(0,-90,0);
            var rest=Quaternion.LookRotation(V(p.RestForward),V(p.RestUp));
            var over=V(p.TargetPosition)+Vector3.up*.12f;
            if(tick<=ToolReturnReach){float t=Mathf.SmoothStep(0,1,tick/(float)ToolReturnReach);hand=Vector3.Lerp(V(p.StartPosition),over,t);rotation=Quaternion.Slerp(initial,target,t);}
            else if(tick<=ToolReturnLower){float t=Mathf.SmoothStep(0,1,(tick-ToolReturnReach)/(float)(ToolReturnLower-ToolReturnReach));hand=Vector3.Lerp(over,V(p.TargetPosition),t);rotation=target;}
            else if(tick<=ToolReturnRelease){hand=V(p.TargetPosition);rotation=target;}
            else{float t=Mathf.SmoothStep(0,1,(tick-ToolReturnRelease)/(float)(ToolReturnEnd-ToolReturnRelease));hand=Vector3.Lerp(V(p.TargetPosition),V(p.RestHand),t);rotation=Quaternion.Slerp(target,rest,t);}
        }
        bool PoseToolReturn(ResidentToolReturn p,int tick)
        {
            var arm=Arm(p.ActorId);if(!arm)return false;ToolReturnPose(p,tick,out var hand,out var rotation);
            arm.Curl(1-Mathf.Clamp01((tick-ToolReturnLower)/(float)(ToolReturnRelease-ToolReturnLower)));return arm.Pose(hand,rotation);
        }
        void BeginResidentToolReturns()
        {
            if(World.Paused||World.Tick%30!=0)return;
            Physics.SyncTransforms();
            foreach(var pickup in residentToolPickups.Where(p=>p.Phase=="Completed")){
                var actor=World.Resident(pickup.ActorId);
                if(!ResidentCanUseTool(actor.Id)||ResidentToolBusy(actor.Id)||actor.Phase!="Performing"||actor.Activity!=pickup.Activity||actor.Node!=pickup.Node||World.Tick+actor.ActivityTicks!=pickup.ActivityEndTick||actor.ActivityTicks<=ToolReturnEnd||actor.HeldObject!=pickup.ToolId)continue;
                var work=residentToolWork.FirstOrDefault(w=>w.ActorId==actor.Id&&w.ActivityEndTick==pickup.ActivityEndTick&&w.Motion.ToolId==pickup.ToolId&&w.Motion.StartedTick>=pickup.LastTick&&!w.Motion.Running);
                // If no usable surface action can start, allow returning the tool
                // after giving the ordinary work starter its next two attempts.
                if(work==null&&World.Tick-pickup.LastTick<60||work!=null&&!new[]{"Completed","Cancelled"}.Contains(work.Motion.Phase)||residentToolReturns.Any(p=>p.ActorId==actor.Id&&p.PickupStartedTick==pickup.StartedTick))continue;
                var source=ObjectBodies.First(o=>o.ObjectId==pickup.ToolId);var item=World.Object(pickup.ToolId);if(item.Location!="Hand"||item.Owner!=actor.Id)continue;
                var originalRotation=Quaternion.LookRotation(V(pickup.ToolForward),V(pickup.ToolUp));
                if(!Visible(actor.Id,V(pickup.ToolStart),2.5f,true)||!FindToolRest(actor.Id,pickup.ToolId,V(pickup.ToolStart),originalRotation,out var target,out var supportId))continue;
                var body=bodies[actor.Id];var p=new ResidentToolReturn{ActorId=actor.Id,ToolId=pickup.ToolId,Activity=actor.Activity,Node=actor.Node,SupportId=supportId,PickupStartedTick=pickup.StartedTick,StartedTick=World.Tick,LastTick=World.Tick,ActivityEndTick=pickup.ActivityEndTick,BodyYaw=actor.Yaw,ActorPosition=actor.Position,StartPosition=P(source.transform.position),StartForward=P(source.transform.forward),StartUp=P(source.transform.up),TargetPosition=P(target),TargetForward=pickup.ToolForward,TargetUp=pickup.ToolUp,RestHand=P(RestGrip(actor.Id,false)),RestForward=P(body.transform.forward),RestUp=P(body.transform.up),HandPosition=P(source.transform.position),ItemPosition=P(source.transform.position)};
                bool reachable=PoseToolReturn(p,ToolReturnLower)&&PressArmSpaceClear(new ToolPressState{ActorId=actor.Id,ToolId=p.ToolId});PoseRestArms(actor.Id);if(!reachable)continue;
                residentToolReturns=residentToolReturns.Where(old=>old.ActorId!=actor.Id).Concat(new[]{p}).ToArray();World.Emit("ResidentToolReturnStarted",actor.Id,p.ToolId,supportId);
            }
        }
        void AdvanceResidentToolReturns()
        {
            if(World.Paused)return;
            foreach(var p in residentToolReturns.Where(p=>p.Running)){
                var actor=World.Resident(p.ActorId);var item=World.Object(p.ToolId);var source=ObjectBodies.First(o=>o.ObjectId==p.ToolId);
                bool released=p.ReleasedTick>=0;
                if(!ResidentCanUseTool(p.ActorId)||actor.Phase!="Performing"||actor.Activity!=p.Activity||actor.Node!=p.Node||World.Tick+actor.ActivityTicks!=p.ActivityEndTick||World.Tick>=p.ActivityEndTick||World.Tick!=p.LastTick+1||actor.Position.Distance(p.ActorPosition)>.025||Mathf.Abs(Mathf.DeltaAngle((float)p.BodyYaw,(float)actor.Yaw))>25||!released&&(actor.HeldObject!=p.ToolId||item.Location!="Hand"||item.Owner!=p.ActorId||!Visible(p.ActorId,V(p.TargetPosition),2.5f,true))){CancelResidentToolReturn(p,"ReturnInterrupted");continue;}
                int next=p.ElapsedTicks+1;ToolReturnPose(p,p.ElapsedTicks,out var previous,out var from);ToolReturnPose(p,next,out var hand,out var to);
                if(!PickupHandPathClear(new ResidentToolPickup{ActorId=p.ActorId,ToolId=p.ToolId},previous,hand)){CancelResidentToolReturn(p,"HandPathBlocked");continue;}
                if(!released){
                    var a=from*Quaternion.Euler(0,90,0);var b=to*Quaternion.Euler(0,90,0);
                    var segment=new ToolPressState{ActorId=p.ActorId,ToolId=p.ToolId,StartPosition=P(previous),ContactPosition=P(hand),StartForward=P(a*Vector3.forward),StartUp=P(a*Vector3.up),ContactForward=P(b*Vector3.forward),ContactUp=P(b*Vector3.up)};
                    if(!(source.Collider is BoxCollider box)||!ToolPressPathClear(segment,0,PressReachTicks,box)){CancelResidentToolReturn(p,"ToolPathBlocked");continue;}
                }
                if(!PoseToolReturn(p,next)||!PressArmSpaceClear(new ToolPressState{ActorId=p.ActorId,ToolId=p.ToolId})){CancelResidentToolReturn(p,"ArmPathBlocked");continue;}
                if(next==ToolReturnRelease){
                    Physics.SyncTransforms();var rotation=Quaternion.LookRotation(V(p.TargetForward),V(p.TargetUp));
                    if(!FindToolRest(p.ActorId,p.ToolId,V(p.TargetPosition),rotation,out var rest,out var support)||support!=p.SupportId||Vector3.Distance(rest,V(p.TargetPosition))>.003f||Vector3.Distance(bodies[p.ActorId].RightHand.position,V(p.TargetPosition))>.015f||World.PlaceAt(p.ActorId,p.ToolId,p.SupportId,p.TargetPosition,false)!="Committed"){CancelResidentToolReturn(p,"RestingPlaceUnavailable");continue;}
                    p.ReleasedTick=World.Tick;p.ReleaseEventSequence=World.EventSequence;p.ItemPosition=p.TargetPosition;released=true;
                    source.transform.SetPositionAndRotation(V(p.TargetPosition),rotation);source.Collider.enabled=true;Physics.SyncTransforms();ObserveResidentToolReturn(p);
                }
                p.ElapsedTicks=next;p.LastTick=World.Tick;p.HandPosition=P(hand);p.Phase=ToolReturnPhase(next);
                if(!released){p.ItemPosition=P(hand);item.Position=p.ItemPosition;}
                if(next==ToolReturnEnd)World.Emit("ResidentToolReturnEnded",p.ActorId,p.ToolId,"Placed");
            }
        }
        void ObserveResidentToolReturn(ResidentToolReturn p)
        {
            foreach(var observer in World.Residents.Where(r=>World.CanAct(r.Id))){
                bool self=observer.Id==p.ActorId;
                if(!self&&(!SeesPassagePerson(observer.Id,bodies[p.ActorId],out bool identity)||!identity||!CanSee(observer.Id,p.ToolId)||!Visible(observer.Id,V(p.TargetPosition),6,true)))continue;
                string root="TOOL_RETURN_L"+World.Loop+"_"+p.StartedTick+"_"+p.ToolId+"_"+observer.Id;
                Knowledge.Observe(observer.Id,new KnownRecord{Kind=self?"Touch":"Visual",Source=observer.Id,SubjectId=p.ActorId,IdentityConfirmed=true,Predicate="PlacedObject",Value=p.ToolId,ProvenanceKey=root,FromTick=World.Tick,ToTick=World.Tick+1,PlaceId=PlaceOf(V(p.TargetPosition)),Position=p.TargetPosition,Text=self?KoreanText.AsObject(NameOf(p.ToolId))+" 받침 위에 내려놓았다.":KoreanText.AsSubject(NameOf(p.ActorId))+" "+KoreanText.AsObject(NameOf(p.ToolId))+" 받침 위에 내려놓는 것을 보았다.",Supports=new[]{"물건을 내려놓은 순간의 인물·물건·장소"},DoesNotEstablish=new[]{"물건의 사용 목적", "그 뒤의 소유자", "사건의 원인"}},World.Tick);
                if(CanSee(observer.Id,p.ToolId))Knowledge.Observe(observer.Id,new KnownRecord{Kind="Visual",Source=observer.Id,SubjectId=p.ToolId,IdentityConfirmed=true,Predicate="AtPlace",Value=PlaceOf(V(p.TargetPosition)),PlaceId=PlaceOf(V(p.TargetPosition)),Position=p.TargetPosition,ProvenanceKey=root,FromTick=World.Tick,ToTick=World.Tick+1,Text=NameOf(p.ToolId)+"의 놓인 위치를 확인했다.",Supports=new[]{"내려놓은 직후 직접 본 물건의 위치"},DoesNotEstablish=new[]{"이후에도 같은 자리에 있었는지"}},World.Tick);
            }
        }
        void CancelResidentToolReturn(ResidentToolReturn p,string reason)
        {
            if(!p.Running)return;p.Phase="Cancelled";p.Reason=reason;PoseRestArms(p.ActorId);
            // Never recall an item already released onto its support.
            World.Emit("ResidentToolReturnEnded",p.ActorId,p.ToolId,reason);
        }
        void ValidateResidentToolReturns(MansionSessionSnapshot s)
        {
            if(s.ResidentToolReturns==null||s.ResidentToolReturns.Any(p=>p==null)||s.ResidentToolReturns.Select(p=>p.ActorId).Distinct().Count()!=s.ResidentToolReturns.Length)throw new System.IO.InvalidDataException("도구를 내려놓던 상태가 올바르지 않습니다.");
            foreach(var p in s.ResidentToolReturns){
                var pickup=s.ResidentToolPickups.FirstOrDefault(x=>x.ActorId==p.ActorId&&x.ToolId==p.ToolId&&x.StartedTick==p.PickupStartedTick&&x.Phase=="Completed");
                if(pickup==null||p.Activity!=pickup.Activity||p.Node!=pickup.Node||p.ActivityEndTick!=pickup.ActivityEndTick||p.StartedTick<pickup.LastTick||p.LastTick<p.StartedTick||p.LastTick>s.World.Tick||p.LastTick-p.StartedTick!=p.ElapsedTicks||p.ElapsedTicks<0||p.ElapsedTicks>ToolReturnEnd||p.ReleasedTick< -1||p.ReleasedTick>=0&&(p.ReleasedTick!=p.StartedTick+ToolReturnRelease||p.ReleasedTick>p.LastTick)||p.ReleasedTick<0&&p.ElapsedTicks>=ToolReturnRelease||string.IsNullOrEmpty(p.SupportId)||p.TargetPosition.Distance(pickup.ToolStart)>.026||p.TargetForward.Distance(pickup.ToolForward)>.0001||p.TargetUp.Distance(pickup.ToolUp)>.0001||double.IsNaN(p.BodyYaw)||double.IsInfinity(p.BodyYaw)||new[]{p.ActorPosition,p.StartPosition,p.StartForward,p.StartUp,p.TargetPosition,p.TargetForward,p.TargetUp,p.RestHand,p.RestForward,p.RestUp,p.HandPosition,p.ItemPosition}.Any(v=>!v.Finite()))throw new System.IO.InvalidDataException("집었던 자리와 내려놓기 동작이 맞지 않습니다.");
                if(!new[]{"Reaching","Lowering","Releasing","Withdrawing","Completed","Cancelled"}.Contains(p.Phase)||p.Phase!="Cancelled"&&p.Phase!=ToolReturnPhase(p.ElapsedTicks))throw new System.IO.InvalidDataException("내려놓기 단계가 맞지 않습니다.");
                var release=s.World.Events.FirstOrDefault(e=>e.Sequence==p.ReleaseEventSequence);
                if(p.ReleasedTick<0?p.ReleaseEventSequence!=0:release==null||release.Type!="ObjectPlacedAt"||release.Actor!=p.ActorId||release.Target!=p.ToolId||release.Detail!=p.SupportId||release.Tick!=p.ReleasedTick)throw new System.IO.InvalidDataException("실제로 물건을 놓은 결과와 동작이 다릅니다.");
                foreach(var pair in new[]{new[]{p.StartForward,p.StartUp},new[]{p.TargetForward,p.TargetUp},new[]{p.RestForward,p.RestUp}}){var f=V(pair[0]);var u=V(pair[1]);if(Mathf.Abs(f.sqrMagnitude-1)>.01f||Mathf.Abs(u.sqrMagnitude-1)>.01f||Mathf.Abs(Vector3.Dot(f,u))>.01f)throw new System.IO.InvalidDataException("내려놓기 방향이 올바르지 않습니다.");}
                ToolReturnPose(p,p.ElapsedTicks,out var hand,out _);if(Vector3.Distance(hand,V(p.HandPosition))>.002f||p.ItemPosition.Distance(p.ReleasedTick<0?p.HandPosition:p.TargetPosition)>.002)throw new System.IO.InvalidDataException("내려놓기 손과 물건 위치가 다릅니다.");
                if(!p.Running)continue;
                var actor=s.World.Residents.Single(a=>a.Id==p.ActorId);var item=s.World.Objects.Single(o=>o.Id==p.ToolId);
                if(!actor.Alive||!actor.Present||actor.Phase!="Performing"||actor.Activity!=p.Activity||actor.Node!=p.Node||s.World.Tick+actor.ActivityTicks!=p.ActivityEndTick||actor.Position.Distance(p.ActorPosition)>.025||s.World.PauseOwners.Contains("M_COURT")||s.ResidentToolPickups.Any(x=>x.ActorId==p.ActorId&&x.Running)||s.ResidentToolWork.Any(w=>w.ActorId==p.ActorId&&w.Motion.Running)||p.ReleasedTick<0&&(actor.HeldObject!=p.ToolId||item.Location!="Hand"||item.Owner!=p.ActorId||item.Position.Distance(p.ItemPosition)>.02))throw new System.IO.InvalidDataException("손의 소유 상태와 내려놓기가 다릅니다.");
            }
        }
    }
}
