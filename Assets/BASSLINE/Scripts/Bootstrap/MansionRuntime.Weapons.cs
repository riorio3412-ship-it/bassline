using System;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.Save;
using BASSLINE.World.Mansion;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerWeaponPort
    {
        const int WeaponContentBit=268435456,WeaponEnd=54;
        WeaponMotionState weaponMotion=new WeaponMotionState();
        MansionWeapon HeldWeapon(){string id=World.Resident("CH_01").HeldObject;return id!=""&&targets.TryGetValue(id,out var target)?target.GetComponent<MansionWeapon>():null;}
        public string HeldWeaponId()=>HeldWeapon()?World.Resident("CH_01").HeldObject:"";
        public string HeldWeaponHint()=>HeldWeapon()?(weaponRinse.Running?"씻는 중 "+(weaponRinse.Elapsed*100/180)+"% · 움직이면 멈춤":weaponMotion.Running?"동작 중":"마우스 왼쪽 · 공격 (맞은 상대는 쓰러질 수 있음)") : "";
        public string SwingWeapon()
        {
            var weapon=HeldWeapon();
            if(!weapon||World.Paused||!World.CanAct("CH_01")||weaponMotion.Running||weaponRinse.Running||itemExchange.Handoff.Running||PlayerSurfaceRunning||WritingCard||toolPress.Running||conversationPlayback.Phase=="Speaking"||inspection.State=="Running")return "지금은 휘두를 수 없어요.";
            if(World.CaseBook.Used>=World.CaseBook.Capacity)return "이번 장에서는 더 공격할 수 없어요.";
            StopWaiting("손에 든 무기를 사용하려고 멈췄어요.");CancelPlayerRescue("WeaponUse");move=default;running=false;
            weaponMotion=new WeaponMotionState{Version=1,Phase="Swinging",WeaponId=World.Resident("CH_01").HeldObject,StartedTick=World.Tick,LastTick=World.Tick,Yaw=World.Yaw,Origin=P(bodies["CH_01"].transform.position),Position=P(RestGrip("CH_01",true))};
            World.Emit("WeaponSwingStarted","CH_01",weaponMotion.WeaponId,"");return message="";
        }
        void WeaponPose(WeaponMotionState motion,int elapsed,out Vector3 grip,out Quaternion rotation)
        {
            float t=Mathf.Clamp01(elapsed/18f);var rest=new Vector3(.255f,bodies["CH_01"].Height*.80f,.42f);
            Vector3 local;Quaternion turn;
            if(elapsed<=18){local=Vector3.Lerp(rest,rest+new Vector3(.08f,.10f,-.12f),Mathf.SmoothStep(0,1,t));turn=Quaternion.Slerp(Quaternion.Euler(-35,0,0),Quaternion.Euler(-70,30,0),t);}
            else if(elapsed<=36){t=Mathf.SmoothStep(0,1,(elapsed-18)/18f);local=Vector3.Lerp(rest+new Vector3(.08f,.10f,-.12f),rest+new Vector3(-.18f,-.04f,.10f),t);turn=Quaternion.Slerp(Quaternion.Euler(-70,30,0),Quaternion.Euler(25,-45,0),t);}
            else{t=Mathf.SmoothStep(0,1,(elapsed-36)/18f);local=Vector3.Lerp(rest+new Vector3(-.18f,-.04f,.10f),rest,t);turn=Quaternion.Slerp(Quaternion.Euler(25,-45,0),Quaternion.Euler(-35,0,0),t);}
            var yaw=Quaternion.Euler(0,(float)motion.Yaw,0);grip=V(motion.Origin)+yaw*local;rotation=yaw*turn;
            // Follow the actual arm reach envelope; do not stop an ordinary swing at an unreachable animation key.
            var arm=Arm("CH_01");if(arm){var wrist=grip-yaw*arm.GripOffset;var delta=wrist-arm.Shoulder;
                float length=Mathf.Clamp(delta.magnitude,Mathf.Abs(arm.UpperLength-arm.ForearmLength)+.01f,arm.UpperLength+arm.ForearmLength-.01f);
                grip=arm.Shoulder+delta.normalized*length+yaw*arm.GripOffset;}

        }
        bool WeaponSweep(BoxCollider box,Vector3 from,Quaternion oldRotation,Vector3 to,Quaternion rotation,out Collider hit)
        {
            hit=null;Vector3 half=box.size*.5f;
            bool Blocks(Collider c)=>!c.transform.IsChildOf(bodies["CH_01"].transform)&&!c.transform.IsChildOf(box.transform);
            int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(Vector3.Distance(from,to)/.01f,Quaternion.Angle(oldRotation,rotation)/3f)));
            for(int i=1;i<=steps;i++){
                float t=(float)i/steps;float previous=(i-1f)/steps;Quaternion q=Quaternion.Slerp(oldRotation,rotation,t),p=Quaternion.Slerp(oldRotation,rotation,previous);
                Vector3 centre=Vector3.Lerp(from,to,t)+q*box.center,start=Vector3.Lerp(from,to,previous)+p*box.center,delta=centre-start;
                if(delta.sqrMagnitude>1e-10f){
                    float padding=half.magnitude*Quaternion.Angle(p,q)*Mathf.Deg2Rad;
                    int count=Physics.BoxCastNonAlloc(start,half+Vector3.one*padding,delta.normalized,rayHits,p,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
                    if(count==rayHits.Length)return false;
                    var first=rayHits.Take(count).Where(h=>Blocks(h.collider)).OrderBy(h=>h.distance).FirstOrDefault();if(first.collider){hit=first.collider;return false;}
                }
                int overlaps=Physics.OverlapBoxNonAlloc(centre,half,overlap,q,~0,QueryTriggerInteraction.Ignore);if(overlaps==overlap.Length)return false;
                hit=overlap.Take(overlaps).FirstOrDefault(Blocks);if(hit)return false;
            }return true;
        }
        void AdvanceWeapon()
        {
            var m=weaponMotion;if(!m.Running)return;
            var weapon=HeldWeapon();
            if(!weapon||weapon.GetComponent<FixtureTarget>().StableId!=m.WeaponId||!World.CanAct("CH_01")||World.Resident("CH_01").Position.Distance(m.Origin)>.04){CancelWeapon();return;}
            int next=Math.Min(WeaponEnd,m.Elapsed+1);m.LastTick=World.Tick;
            if(m.StoppedAt>=0){m.Elapsed=next;if(next>=WeaponEnd)m.Phase="Completed";return;}
            WeaponPose(m,m.Elapsed,out var from,out var oldRotation);WeaponPose(m,next,out var to,out var rotation);
            var box=weapon.GetComponent<FixtureObjectBody>().Collider as BoxCollider;Collider hit=null;
            if(!box||!WeaponSweep(box,from,oldRotation,to,rotation,out hit)){
                m.StoppedAt=m.Elapsed;m.Phase="Recovering";m.ContactTarget=hit?hit.GetComponentInParent<FixtureTarget>()?.StableId??hit.name:"";
                World.Emit("WeaponBlocked","CH_01",m.WeaponId,m.ContactTarget);message="닿는 곳에서 동작이 멈췄어요.";
                if(hit&&m.Elapsed>=18&&m.Elapsed<=36)CommitWeaponHit(m,hit,hit.ClosestPoint(from));
            }else{
                var arm=Arm("CH_01");if(!arm||!arm.Pose(to,bodies["CH_01"].transform.rotation)){m.StoppedAt=m.Elapsed;m.Phase="Recovering";}
                else{m.Position=P(to);if(next==WeaponEnd)m.Phase="Completed";}
            }
            m.Elapsed=next;
        }
        void CommitWeaponHit(WeaponMotionState m,Collider hit,Vector3 point)
        {
            var victim=bodies.FirstOrDefault(pair=>pair.Key!="CH_01"&&hit.transform.IsChildOf(pair.Value.transform));
            if(string.IsNullOrEmpty(victim.Key)||!World.CanAct(victim.Key))return;
            World.Emit("PlayerWeaponHit","CH_01",victim.Key,m.WeaponId);
            string result=incidents.CommitPlayerStrike(World,victim.Key,m.WeaponId,P(point),this);
            message=result=="CauseCommitted"?"상대가 쓰러졌어요. 무기를 내려놓고 가까이에서 도울 수 있어요.":"공격이 이어지지 않았어요.";
            if(result=="CauseCommitted")DepositWeaponResidue(m.WeaponId);
            ImportCaseReceipts();
        }
        void CancelWeapon(){if(weaponMotion.Running){weaponMotion.Phase="Cancelled";message="동작을 멈췄어요.";}}
        void PresentWeapon(FixtureObjectBody body,MansionObjectState item)
        {
            if(!body.GetComponent<MansionWeapon>())return;
            PresentWeaponResidue(body);
            if(item.Location!="Hand"){if(item.PhysicsVersion==1)body.transform.rotation=Quaternion.Euler(V(item.Rotation));return;}
            if(item.Owner=="CH_01"&&weaponRinse.Running&&weaponRinse.WeaponId==item.Id){RinsePose(weaponRinse.Elapsed,out var rinseGrip,out var rinseRotation);Arm("CH_01")?.Pose(rinseGrip,bodies["CH_01"].transform.rotation);body.transform.position=rinseGrip;body.transform.rotation=rinseRotation;item.Position=P(rinseGrip);}
            else if(item.Owner=="CH_01"&&weaponMotion.Running&&weaponMotion.WeaponId==item.Id){
                WeaponPose(weaponMotion,weaponMotion.StoppedAt>=0?weaponMotion.StoppedAt:weaponMotion.Elapsed,out var grip,out var rotation);
                Arm("CH_01")?.Pose(grip,bodies["CH_01"].transform.rotation);body.transform.position=grip;body.transform.rotation=rotation;item.Position=P(grip);
            }else body.transform.rotation=bodies[item.Owner].transform.rotation*Quaternion.Euler(-35,0,0);
            if(item.PhysicsVersion==1)item.Rotation=P(body.transform.eulerAngles);
        }
        MansionSessionSnapshot UpgradeWeaponContent(MansionSessionSnapshot s)
        {
            if(s?.World==null||(s.OptionalObjects&WeaponContentBit)!=0)return s;
            foreach(var body in ObjectBodies.Where(o=>o.GetComponent<MansionWeapon>()))if(!s.World.Objects.Any(o=>o.Id==body.ObjectId)){
                var weapon=body.GetComponent<MansionWeapon>();s.World.Objects=s.World.Objects.Concat(new[]{new MansionObjectState{Id=body.ObjectId,Name=NameOf(body.ObjectId),Location="World",Position=P(weapon.InitialPosition)}}).ToArray();
                AppendEvent(s.World,"ContentObjectAdded","",body.ObjectId,"Version0.26; physical weapon added at load");
            }
            s.WeaponMotion=new WeaponMotionState();s.OptionalObjects|=WeaponContentBit;return s;
        }
        void ValidateWeaponMotion(MansionSessionSnapshot s)
        {
            var m=s.WeaponMotion;
            if(m==null||m.Version<0||m.Version>1||!new[]{"Idle","Swinging","Recovering","Completed","Cancelled"}.Contains(m.Phase))throw new System.IO.InvalidDataException("무기 동작 상태가 올바르지 않습니다.");
            if(m.Phase=="Idle")return;
            if(!ObjectBodies.Any(o=>o.ObjectId==m.WeaponId&&o.GetComponent<MansionWeapon>())||m.StartedTick<0||m.LastTick<m.StartedTick||m.LastTick>s.World.Tick||m.Elapsed<0||m.Elapsed>WeaponEnd||m.StoppedAt< -1||m.StoppedAt>m.Elapsed||!m.Origin.Finite()||!m.Position.Finite()||double.IsNaN(m.Yaw)||double.IsInfinity(m.Yaw))throw new System.IO.InvalidDataException("무기 동작의 대상과 시간이 맞지 않습니다.");
            if(m.Running){var player=s.World.Residents.Single(r=>r.Id=="CH_01");var item=s.World.Objects.Single(o=>o.Id==m.WeaponId);if(!player.Alive||!player.Present||player.HeldObject!=m.WeaponId||item.Owner!="CH_01"||item.Location!="Hand"||player.Position.Distance(m.Origin)>.04||m.Elapsed>=WeaponEnd)throw new System.IO.InvalidDataException("무기를 사용 중인 손과 동작이 맞지 않습니다.");}
        }
    }
}
