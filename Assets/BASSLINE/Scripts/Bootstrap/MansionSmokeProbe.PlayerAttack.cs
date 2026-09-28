using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using BASSLINE.AuthoringData;
using BASSLINE.Core;
using BASSLINE.World.Mansion;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionSmokeProbe
    {
        IEnumerator ReviewPlayerAttack()
        {
            driveWorld=false;
            var weapon=runtime.ObjectBodies.Single(o=>o.ObjectId=="M_KITCHEN_KNIFE");
            bool picked=false;
            foreach(var n in runtime.Layout.NavigationNodes.Select((node,index)=>new{node,index}).Where(n=>Vector3.Distance(n.node.Position,weapon.transform.position)<2.3f)){
                if(!PlayerCapsuleClear(n.node.Position))continue;
                SetTestOnlyPlayerPosition(n.node.Position,n.index);
                var angle=Quaternion.LookRotation(weapon.transform.position-Camera.main.transform.position).eulerAngles;runtime.SetLook(angle.y,Mathf.DeltaAngle(0,angle.x));
                if(!runtime.DescribeTarget(weapon.ObjectId).Available)continue;
                runtime.Interact(weapon.ObjectId);if(runtime.HeldWeaponId()!=weapon.ObjectId)continue;picked=true;break;
            }
            Require(picked,"Could not pick up the actual knife for player attack review");
            var space=runtime.Layout.NavigationNodes.Select((node,index)=>new{node,index}).FirstOrDefault(n=>PlayerCapsuleClear(n.node.Position)&&runtime.Bodies.Where(b=>b.ActorId!="CH_01").All(b=>Vector3.Distance(b.transform.position,n.node.Position)>7)&&Physics.OverlapSphere(n.node.Position+Vector3.up*1.35f,1.25f,~0,QueryTriggerInteraction.Ignore).All(c=>c.transform.IsChildOf(player.transform)||c.transform.IsChildOf(weapon.transform)));
            Require(space!=null,"No isolated physical attack review space");
            SetTestOnlyPlayerPosition(space.node.Position,space.index);runtime.SetLook(0,0);runtime.AdvanceOne();
            Require(runtime.SwingWeapon()=="","Empty-space attack did not begin");
            for(int i=0;i<24;i++){runtime.AdvanceOne();yield return null;}
            var savedMotion=((WeaponMotionState)Field(runtime,"weaponMotion")).Copy();
            Require(savedMotion.Running,"Mid-swing motion completed early");
            Require(runtime.SaveSlot()=="저장 완료","Mid-swing save failed");
            for(int i=0;i<36;i++)runtime.AdvanceOne();
            Require(runtime.LoadSlot()=="불러오기 완료","Mid-swing restore failed");
            Require(((WeaponMotionState)Field(runtime,"weaponMotion")).Elapsed==savedMotion.Elapsed,"Swing progress was not restored");
            for(int i=0;i<36;i++){runtime.AdvanceOne();yield return null;}
            Require(!runtime.World.Events.Any(e=>e.Type=="IncidentCauseCommitted"),"A missed swing injured somebody");
            var victim=runtime.Bodies.Single(b=>b.ActorId=="CH_02");var state=runtime.World.Resident(victim.ActorId);
            var position=space.node.Position+Vector3.forward*.86f;
            placements.Add(new PlacementReceipt{Purpose="TestOnly isolated victim framing for a real swept knife hit; not a natural encounter",ActorId=victim.ActorId,From=victim.transform.position,To=position,NodeId=runtime.Layout.NavigationNodes[runtime.Layout.NearestNode(position)].Id,Tick=runtime.World.Tick,PhysicalReachAccepted=true});
            runtime.World.StopResidentActivity(victim.ActorId);victim.Capsule.enabled=false;victim.transform.position=position;victim.transform.rotation=Quaternion.Euler(0,180,0);victim.Capsule.enabled=true;
            state.Position=MansionRuntime.P(position);state.Node=runtime.Layout.NavigationNodes[runtime.Layout.NearestNode(position)].Id;state.Phase="Performing";state.Activity="Wait";state.ActivityTicks=2000;
            Physics.SyncTransforms();runtime.AdvanceOne();
            Require(runtime.SwingWeapon()=="","Player attack did not begin");
            for(int i=0;i<60;i++){runtime.AdvanceOne();yield return null;}
            var cases=(MansionIncidentCollection)Field(runtime,"incidents");var incident=cases.All().SingleOrDefault(c=>c.ActorId=="CH_01"&&c.Capture().CauseTick>=0);
            Require(incident!=null,"Actual swept attack did not create a player incident; motion="+JsonUtility.ToJson(Field(runtime,"weaponMotion")));
            Require(state.Alive&&runtime.World.PhysicalBand(victim.ActorId)=="Critical","Hit victim was not critically injured before the result");
            Require(incident.Capture().Settings.PlayerInitiated&&!incident.Capture().Settings.ExplicitTestSession,"Player attack relied on the test incident path");
            string saved=runtime.SaveSlot();Require(saved=="저장 완료","Player-cause save failed: "+saved);
            yield return Capture("player_attack_critical",1,1920,1080);
            runtime.Interact("DROP");string help=runtime.Interact(victim.ActorId);
            Require(help.Contains("돕기 시작"),"Could not help the victim after putting down weapon: "+help);
            for(int i=0;i<125;i++){runtime.AdvanceOne();yield return null;}
            Require(incident.RiskResolved&&state.Alive,"Player could not prevent the delayed result by helping");
            string loaded=runtime.LoadSlot();Require(loaded=="불러오기 완료","Player-cause restore failed: "+loaded);
            cases=(MansionIncidentCollection)Field(runtime,"incidents");incident=cases.All().Single(c=>c.ActorId=="CH_01");
            long due=incident.Capture().DueTick;
            while(runtime.World.Tick<=due+1){runtime.AdvanceOne();yield return null;}
            Require(!runtime.World.Resident(victim.ActorId).Alive,"Unresolved player strike did not produce death");
            Require(runtime.World.CaseBook.AdjudicatedActorId=="CH_01","Player was omitted from actual-culprit adjudication");
            Require(runtime.World.Events.Count(e=>e.Type=="IncidentResultCommitted"&&e.Detail==incident.CaseId)==1,"Result was committed more than once after restore");
            string found=runtime.Interact(victim.ActorId);Require(incident.Capture().DiscoveryTick>=0,"Player could not discover the physical body: "+found);
            Require(runtime.SaveSlot()=="저장 완료"&&runtime.LoadSlot()=="불러오기 완료","Completed player incident did not survive save/load");
            yield return Capture("player_attack_result",1,1920,1080);
            receipt.Scope+=" Player attack: real held-knife swept contact, empty-space miss, mid-swing save/resume, critical injury, successful help, cause save/resume to one death, player culprit registration and physical body discovery. Explicit isolated test-only placement; full natural trial progression and final art not asserted.";
        }
    }
}
