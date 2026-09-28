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
        const int PickupReachTicks=60,PickupGripTicks=12,PickupLiftTicks=30,PickupCarryTicks=42;
        const int PickupContactTick=PickupReachTicks+PickupGripTicks,PickupLiftEnd=PickupContactTick+PickupLiftTicks,PickupEnd=PickupLiftEnd+PickupCarryTicks;
        ResidentToolPickup[] residentToolPickups=Array.Empty<ResidentToolPickup>();
        ResidentToolPickup PickupForTool(string id)=>residentToolPickups.FirstOrDefault(p=>p.Running&&p.AcquiredTick>=0&&p.ToolId==id);
        static string PickupPhase(int elapsed)=>elapsed<PickupReachTicks?"Reaching":elapsed<PickupContactTick?"Gripping":elapsed<PickupLiftEnd?"Lifting":elapsed<PickupEnd?"Carrying":"Completed";
        void TryBeginResidentToolPickup(string actorId)
        {
            var actor=World.Resident(actorId);
            if(actor.HeldObject!=""||actor.ActivityTicks<=PickupEnd+PressEndTicks+ToolReturnEnd+60)return;
            long end=World.Tick+actor.ActivityTicks;
            if(residentToolPickups.Any(p=>p.ActorId==actorId&&p.Activity==actor.Activity&&p.Node==actor.Node&&p.ActivityEndTick==end))return;
            // The desired tool belongs to this resident's authored routine. Its location
            // must still have been personally seen; registry membership is not a sighting.
            string tool=Routines.FirstOrDefault(r=>r.ActorId==actorId)?.SurfaceToolId;
            if(string.IsNullOrEmpty(tool))return;
            var known=Knowledge.LastDirect(actorId,tool,"AtPlace");
            if(known==null||!known.IdentityConfirmed||World.Tick-known.ToTick>600||!CanReach(actorId,tool,1.6)||!CanSee(actorId,tool))return;
            var source=traceSources.FirstOrDefault(s=>s&&s.isActiveAndEnabled&&TraceSourceId(s)==tool&&s.TransfersPigment&&s.Face);
            if(!source||source.transform.lossyScale!=Vector3.one||!(source.GetComponent<FixtureObjectBody>().Collider is BoxCollider box)||!box.enabled||!source.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.gameObject.activeInHierarchy))return;
            var item=World.Object(tool);if(item.Location!="World")return;
            var body=bodies[actorId];var rig=Arm(actorId);if(!rig)return;
            var rest=RestGrip(actorId,true);var restRotation=body.transform.rotation;
            var p=new ResidentToolPickup{ActorId=actorId,ToolId=tool,Activity=actor.Activity,Node=actor.Node,LocationRecordId=known.Id,StartedTick=World.Tick,LastTick=World.Tick,ActivityEndTick=end,BodyYaw=actor.Yaw,ActorPosition=actor.Position,
                StartHand=P(body.RightHand.position),StartHandForward=P(body.RightHand.forward),StartHandUp=P(body.RightHand.up),ToolStart=P(source.transform.position),ToolForward=P(source.transform.forward),ToolUp=P(source.transform.up),RestHand=P(rest),RestForward=P(restRotation*Vector3.forward),RestUp=P(restRotation*Vector3.up),HandPosition=P(body.RightHand.position),ItemPosition=P(source.transform.position)};
            bool reachable=PoseToolPickup(p,PickupContactTick)&&PressArmSpaceClear(new ToolPressState{ActorId=actorId,ToolId=tool});PoseRestArms(actorId);
            if(!reachable)return;
            residentToolReturns=residentToolReturns.Where(old=>old.ActorId!=actorId).ToArray();
            residentToolPickups=residentToolPickups.Where(old=>old.ActorId!=actorId).Concat(new[]{p}).ToArray();
            World.Emit("ResidentToolPickupStarted",actorId,tool,known.Id);
        }
        static void PickupPose(ResidentToolPickup p,int tick,out Vector3 hand,out Quaternion rotation)
        {
            var initial=Quaternion.LookRotation(V(p.StartHandForward),V(p.StartHandUp));
            var grasp=Quaternion.LookRotation(V(p.ToolForward),V(p.ToolUp))*Quaternion.Euler(0,-90,0);
            var rest=Quaternion.LookRotation(V(p.RestForward),V(p.RestUp));
            var lift=V(p.ToolStart)+Vector3.up*.12f;
            if(tick<=PickupReachTicks){float t=Mathf.SmoothStep(0,1,tick/(float)PickupReachTicks);hand=Vector3.Lerp(V(p.StartHand),V(p.ToolStart),t);rotation=Quaternion.Slerp(initial,grasp,t);}
            else if(tick<=PickupContactTick){hand=V(p.ToolStart);rotation=grasp;}
            else if(tick<=PickupLiftEnd){float t=Mathf.SmoothStep(0,1,(tick-PickupContactTick)/(float)PickupLiftTicks);hand=Vector3.Lerp(V(p.ToolStart),lift,t);rotation=grasp;}
            else{float t=Mathf.SmoothStep(0,1,(tick-PickupLiftEnd)/(float)PickupCarryTicks);hand=Vector3.Lerp(lift,V(p.RestHand),t);rotation=Quaternion.Slerp(grasp,rest,t);}
        }
        bool PoseToolPickup(ResidentToolPickup p,int tick)
        {
            var rig=Arm(p.ActorId);if(!rig)return false;
            PickupPose(p,tick,out var hand,out var rotation);
            rig.Curl(Mathf.Clamp01((tick-PickupReachTicks)/(float)PickupGripTicks));return rig.Pose(hand,rotation);
        }
        bool PickupHandPathClear(ResidentToolPickup p,Vector3 from,Vector3 to)
        {
            // Sweep the fingertip/grip region; the separate posed arm check covers
            // the larger wrist and forearm volumes above the object.
            int count=Physics.OverlapCapsuleNonAlloc(from,to,.025f,overlap,~0,QueryTriggerInteraction.Ignore);
            if(count==overlap.Length)return false;
            for(int i=0;i<count;i++)if(!overlap[i].transform.IsChildOf(bodies[p.ActorId].transform)&&!overlap[i].transform.IsChildOf(targets[p.ToolId].transform))return false;
            return true;
        }
        void AdvanceResidentToolPickups()
        {
            if(World.Paused)return;
            foreach(var p in residentToolPickups.Where(p=>p.Running)){
                var actor=World.Resident(p.ActorId);var item=World.Object(p.ToolId);var source=ObjectBodies.First(o=>o.ObjectId==p.ToolId);
                bool held=p.AcquiredTick>=0;
                if(!ResidentCanUseTool(p.ActorId)||actor.Phase!="Performing"||actor.Activity!=p.Activity||actor.Node!=p.Node||World.Tick+actor.ActivityTicks!=p.ActivityEndTick||World.Tick>=p.ActivityEndTick||World.Tick!=p.LastTick+1||actor.Position.Distance(p.ActorPosition)>.025||Mathf.Abs(Mathf.DeltaAngle((float)p.BodyYaw,(float)actor.Yaw))>25||!CanSee(p.ActorId,p.ToolId)
                    ||(held?(actor.HeldObject!=p.ToolId||item.Location!="Hand"||item.Owner!=p.ActorId):(actor.HeldObject!=""||item.Location!="World"||item.Position.Distance(p.ToolStart)>.003||Vector3.Distance(source.transform.position,V(p.ToolStart))>.003))){CancelResidentToolPickup(p,"PickupInterrupted");continue;}
                int next=p.ElapsedTicks+1;PickupPose(p,p.ElapsedTicks,out var previous,out var previousRotation);PickupPose(p,next,out var hand,out var rotation);
                if(!PickupHandPathClear(p,previous,hand)){CancelResidentToolPickup(p,"HandPathBlocked");continue;}
                if(held){
                    var from=previousRotation*Quaternion.Euler(0,90,0);var to=rotation*Quaternion.Euler(0,90,0);
                    var segment=new ToolPressState{ActorId=p.ActorId,ToolId=p.ToolId,StartPosition=P(previous),ContactPosition=P(hand),StartForward=P(from*Vector3.forward),StartUp=P(from*Vector3.up),ContactForward=P(to*Vector3.forward),ContactUp=P(to*Vector3.up)};
                    if(!(source.Collider is BoxCollider box)||!ToolPressPathClear(segment,0,PressReachTicks,box)){CancelResidentToolPickup(p,"HeldToolPathBlocked");continue;}
                }
                if(!PoseToolPickup(p,next)||!PressArmSpaceClear(new ToolPressState{ActorId=p.ActorId,ToolId=p.ToolId})){CancelResidentToolPickup(p,"ArmPathBlocked");continue;}
                if(next==PickupContactTick){
                    if(Vector3.Distance(bodies[p.ActorId].RightHand.position,V(p.ToolStart))>.015f||World.Pickup(p.ToolId,p.ActorId)!="Committed"){CancelResidentToolPickup(p,"ToolNoLongerAvailable");continue;}
                    p.AcquiredTick=World.Tick;held=true;ObserveResidentToolPickup(p);
                }
                p.ElapsedTicks=next;p.LastTick=World.Tick;p.HandPosition=P(hand);p.Phase=PickupPhase(next);
                if(held){p.ItemPosition=P(hand);item.Position=p.ItemPosition;}
                if(next==PickupEnd)World.Emit("ResidentToolPickupEnded",p.ActorId,p.ToolId,"Held");
            }
        }
        void ObserveResidentToolPickup(ResidentToolPickup p)
        {
            foreach(var actor in World.Residents.Where(r=>World.CanAct(r.Id))){
                bool self=actor.Id==p.ActorId;
                if(!self&&(!SeesPassagePerson(actor.Id,bodies[p.ActorId],out bool identified)||!identified||!CanSee(actor.Id,p.ToolId)||!Visible(actor.Id,V(p.ToolStart),6,true)))continue;
                Knowledge.Observe(actor.Id,new KnownRecord{Kind=self?"Touch":"Visual",Source=actor.Id,SubjectId=p.ActorId,IdentityConfirmed=true,Predicate="HeldObject",Value=p.ToolId,ProvenanceKey="TOOL_PICKUP_L"+World.Loop+"_"+p.StartedTick+"_"+p.ToolId+"_"+actor.Id,FromTick=World.Tick,ToTick=World.Tick+1,Position=p.ToolStart,PlaceId=PlaceOf(V(p.ToolStart)),
                    Text=self?KoreanText.AsObject(NameOf(p.ToolId))+" 직접 집었다.":KoreanText.AsSubject(NameOf(p.ActorId))+" "+KoreanText.AsObject(NameOf(p.ToolId))+" 집어 드는 것을 보았다.",Supports=new[]{"집어 든 순간의 인물과 물건"},DoesNotEstablish=new[]{"이전 소유자", "물건을 사용한 목적", "이후의 사용이나 사건 원인"}},World.Tick);
            }
        }
        void CancelResidentToolPickup(ResidentToolPickup p,string reason)
        {
            if(!p.Running)return;p.Phase="Cancelled";p.Reason=reason;PoseRestArms(p.ActorId);
            // A completed grasp keeps custody. Interrupting reach never grants custody.
            World.Emit("ResidentToolPickupEnded",p.ActorId,p.ToolId,reason);
        }
        void ValidateResidentToolPickups(MansionSessionSnapshot s,BASSLINE.Knowledge.KnowledgeLedger knowledge)
        {
            if(s.ResidentToolPickups==null||s.ResidentToolPickups.Any(p=>p==null)||s.ResidentToolPickups.Select(p=>p.ActorId).Distinct().Count()!=s.ResidentToolPickups.Length)throw new System.IO.InvalidDataException("주민의 집기 동작 상태가 올바르지 않습니다.");
            foreach(var p in s.ResidentToolPickups){
                if(p.ActorId=="CH_01"||!bodies.ContainsKey(p.ActorId)||!ObjectBodies.Any(o=>o.ObjectId==p.ToolId)||!nodes.Any(n=>n.Id==p.Node)||!(p.Activity??"").StartsWith(SurfaceWorkPrefix,StringComparison.Ordinal)||p.StartedTick<0||p.LastTick<p.StartedTick||p.LastTick>s.World.Tick||p.LastTick-p.StartedTick!=p.ElapsedTicks||p.ActivityEndTick<=p.StartedTick||p.ElapsedTicks<0||p.ElapsedTicks>PickupEnd||p.AcquiredTick< -1||p.AcquiredTick>=0&&(p.AcquiredTick!=p.StartedTick+PickupContactTick||p.AcquiredTick>p.LastTick)||p.AcquiredTick<0&&p.ElapsedTicks>=PickupContactTick||double.IsNaN(p.BodyYaw)||double.IsInfinity(p.BodyYaw)||new[]{p.ActorPosition,p.StartHand,p.StartHandForward,p.StartHandUp,p.ToolStart,p.ToolForward,p.ToolUp,p.RestHand,p.RestForward,p.RestUp,p.HandPosition,p.ItemPosition}.Any(v=>!v.Finite()))throw new System.IO.InvalidDataException("도구를 집던 대상·위치·시간이 일치하지 않습니다.");
                if(!new[]{"Reaching","Gripping","Lifting","Carrying","Completed","Cancelled"}.Contains(p.Phase)||p.Phase!="Cancelled"&&p.Phase!=PickupPhase(p.ElapsedTicks))throw new System.IO.InvalidDataException("도구 집기 단계가 맞지 않습니다.");
                var known=knowledge.For(p.ActorId).Find(p.LocationRecordId);
                if(known==null||!known.Direct||!known.IdentityConfirmed||known.SubjectId!=p.ToolId||known.Predicate!="AtPlace"||known.ReceivedTick>p.StartedTick||p.StartedTick-known.ToTick>600)throw new System.IO.InvalidDataException("직접 확인하지 않은 물건을 집으려는 상태입니다.");
                foreach(var pair in new[]{new[]{p.StartHandForward,p.StartHandUp},new[]{p.ToolForward,p.ToolUp},new[]{p.RestForward,p.RestUp}}){var f=V(pair[0]);var u=V(pair[1]);if(Mathf.Abs(f.sqrMagnitude-1)>.01f||Mathf.Abs(u.sqrMagnitude-1)>.01f||Mathf.Abs(Vector3.Dot(f,u))>.01f)throw new System.IO.InvalidDataException("집기 동작의 방향이 올바르지 않습니다.");}
                PickupPose(p,p.ElapsedTicks,out var hand,out _);
                if(Vector3.Distance(hand,V(p.HandPosition))>.002f||p.ItemPosition.Distance(p.AcquiredTick<0?p.ToolStart:p.HandPosition)>.002)throw new System.IO.InvalidDataException("집기 동작의 손과 도구 위치가 다릅니다.");
                if(!p.Running)continue;
                var actor=s.World.Residents.Single(a=>a.Id==p.ActorId);var item=s.World.Objects.Single(o=>o.Id==p.ToolId);
                if(!actor.Alive||!actor.Present||actor.Phase!="Performing"||actor.Activity!=p.Activity||actor.Node!=p.Node||s.World.Tick+actor.ActivityTicks!=p.ActivityEndTick||actor.Position.Distance(p.ActorPosition)>.025||s.World.PauseOwners.Contains("M_COURT")||s.ResidentToolWork.Any(w=>w.ActorId==p.ActorId&&w.Motion.Running)||(p.AcquiredTick>=0?(actor.HeldObject!=p.ToolId||item.Location!="Hand"||item.Owner!=p.ActorId||item.Position.Distance(p.ItemPosition)>.02):(actor.HeldObject!=""||item.Location!="World"||item.Position.Distance(p.ToolStart)>.003)))throw new System.IO.InvalidDataException("실제 손의 소유 상태와 진행 중 집기가 다릅니다.");
            }
        }
    }
}
