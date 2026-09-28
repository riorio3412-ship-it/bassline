using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BASSLINE.AuthoringData;
using BASSLINE.Core;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        readonly Dictionary<string,ActorArmRig> armRigs=new Dictionary<string,ActorArmRig>();
        ActorArmRig Arm(string actor,int side=1)
        {
            string key=actor+side;if(!armRigs.TryGetValue(key,out var rig)){
                rig=bodies[actor].GetComponentsInChildren<ActorArmRig>(true).FirstOrDefault(r=>r.Side==side);armRigs[key]=rig;
            }return rig;
        }
        Vector3 RestGrip(string actor,bool held,int side=1)
        {
            var b=bodies[actor];bool firstPersonHeld=held&&actor=="CH_01";
            return b.transform.TransformPoint(new Vector3(side*.255f,b.Height*(held?(firstPersonHeld?.80f:.61f):.47f),held?(firstPersonHeld?.42f:.28f):.07f));
        }
        void PoseRestArms(string actor)
        {
            var b=bodies[actor];for(int side=-1;side<=1;side+=2){
                bool held=side>0&&World.Resident(actor).HeldObject!="";var rig=Arm(actor,side);if(rig){rig.Pose(RestGrip(actor,held,side),b.transform.rotation);rig.Curl(held?1:0);}
            }
        }
        void PresentArms()
        {
            foreach(var b in Bodies)if(World.Resident(b.ActorId).Alive&&World.Resident(b.ActorId).Present)PoseRestArms(b.ActorId);
            var h=itemExchange.Handoff;
            if(h.Running&&h.MotionVersion==1&&h.Phase!="Approaching")PoseHandoffArms(h,h.ElapsedTicks,out _);
            PoseSurfaceTransfer();PoseToolPress();foreach(var work in residentToolWork)PoseToolPress(work.Motion);
            foreach(var pickup in residentToolPickups.Where(p=>p.Running))PoseToolPickup(pickup,pickup.ElapsedTicks);
            foreach(var returned in residentToolReturns.Where(p=>p.Running))PoseToolReturn(returned,returned.ElapsedTicks);
            foreach(var reading in residentReadings.Where(p=>p.Running))PoseResidentReading(reading,reading.ElapsedTicks);
        }
        static void HandoffTargets(ItemHandoff h,int tick,out Vector3 giver,out Vector3 receiver,out Vector3 item)
        {
            var contact=V(h.ContactPosition);var offset=V(h.ContactAxis)*.04f;
            if(tick<=90){float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(tick/60f));
                giver=Vector3.Lerp(V(h.StartPosition),contact-offset,t);receiver=Vector3.Lerp(V(h.ReceiverStartPosition),contact+offset,t);item=giver+offset*t;
            }else{float t=Mathf.SmoothStep(0,1,(tick-90)/30f);
                giver=Vector3.Lerp(contact-offset,V(h.GiverEndPosition),t);receiver=Vector3.Lerp(contact+offset,V(h.ReceiverEndPosition),t);item=receiver-offset*(1-t);
            }
        }
        bool PoseHandoffArms(ItemHandoff h,int tick,out Vector3 item)
        {
            HandoffTargets(h,tick,out var giver,out var receiver,out item);
            var a=Arm(h.Giver);var b=Arm(h.Receiver);
            if(a)a.Curl(tick<=90?1:1-Mathf.SmoothStep(0,1,(tick-90)/30f));
            if(b)b.Curl(Mathf.SmoothStep(0,1,Mathf.Clamp01((tick-30)/45f)));
            return a&&b&&a.Pose(giver,bodies[h.Giver].transform.rotation)&&b.Pose(receiver,bodies[h.Receiver].transform.rotation);
        }
        bool ArmSpaceClear(ItemHandoff h)
        {
            foreach(string id in new[]{h.Giver,h.Receiver}){
                var arm=Arm(id);if(!arm)return false;
                if(!SegmentClear(arm.Upper.position,arm.Forearm.position,.052f)||!SegmentClear(arm.Forearm.position,arm.Grip.position,.047f))return false;
            }return true;
            bool SegmentClear(Vector3 start,Vector3 end,float radius){
                int count=Physics.OverlapCapsuleNonAlloc(start,end,radius,overlap,~0,QueryTriggerInteraction.Ignore);
                if(count==overlap.Length)return false;
                for(int i=0;i<count;i++){
                    var t=overlap[i].transform;
                    if(t.IsChildOf(bodies[h.Giver].transform)||t.IsChildOf(bodies[h.Receiver].transform)||t.IsChildOf(targets[h.ItemId].transform))continue;
                    return false;
                }return true;
            }
        }
        void AdvanceHandoffApproach(ItemHandoff h)
        {
            h.ApproachTicks++;
            var npc=bodies[h.ResidentId];var player=bodies["CH_01"];var delta=player.transform.position-npc.transform.position;delta.y=0;
            var actor=World.Resident(h.ResidentId);float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            actor.Yaw=Mathf.MoveTowardsAngle((float)actor.Yaw,yaw,3f);npc.transform.rotation=Quaternion.Euler(0,(float)actor.Yaw,0);
            if(delta.magnitude>.82f){
                float distance=Mathf.Min(.018f,delta.magnitude-.82f);
                if(!SteeringStepClear(npc,delta.normalized,distance)){CancelItemExchange("사이에 장애물이 있어 다가가지 못했어요. 조금 자리를 바꿔 주세요.");return;}
                actor.Position=MoveActor(h.ResidentId,P(delta.normalized*distance),false);Physics.SyncTransforms();
            }
            PoseRestArms(h.Giver);PoseRestArms(h.Receiver);
            var next=bodies[h.Giver].RightHand.position;
            if(!HandoffPathClear(h,next)){CancelItemExchange("손에 든 물건이 장애물에 닿아 멈췄어요.");return;}
            h.Position=P(next);World.Object(h.ItemId).Position=h.Position;
            if(h.ApproachTicks>=180){CancelItemExchange("손이 닿는 곳까지 다가가지 못했어요. 조금 자리를 바꿔 주세요.");return;}
            if(Vector3.Distance(npc.transform.position,player.transform.position)>.85f||Mathf.Abs(Mathf.DeltaAngle((float)actor.Yaw,yaw))>3)return;
            var giver=Arm(h.Giver);var receiver=Arm(h.Receiver);
            if(!giver||!receiver){CancelItemExchange("물건을 건네는 동작을 준비하지 못했어요.");return;}
            h.StartPosition=h.Position;h.ReceiverStartPosition=P(bodies[h.Receiver].RightHand.position);
            h.GiverEndPosition=P(RestGrip(h.Giver,false));h.ReceiverEndPosition=P(RestGrip(h.Receiver,true));
            h.ContactPosition=P((giver.Shoulder+receiver.Shoulder)*.5f-Vector3.up*.12f);h.ContactAxis=P(player.transform.right);
            h.Phase="Reaching";
        }
    }
}
