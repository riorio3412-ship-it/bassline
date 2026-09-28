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
        IEnumerator ReviewWeaponResidue()
        {
            driveWorld=false;
            var weapon=runtime.ObjectBodies.Single(o=>o.ObjectId=="M_KITCHEN_KNIFE");
            bool picked=false;
            foreach(var n in runtime.Layout.NavigationNodes.Select((node,index)=>new{node,index}).Where(n=>Vector3.Distance(n.node.Position,weapon.transform.position)<2.3f)){
                if(!PlayerCapsuleClear(n.node.Position))continue;SetTestOnlyPlayerPosition(n.node.Position,n.index);
                var a=Quaternion.LookRotation(weapon.transform.position-Camera.main.transform.position).eulerAngles;runtime.SetLook(a.y,Mathf.DeltaAngle(0,a.x));
                if(!runtime.DescribeTarget(weapon.ObjectId).Available)continue;runtime.Interact(weapon.ObjectId);if(runtime.HeldWeaponId()!=weapon.ObjectId)continue;picked=true;break;
            }
            Require(picked,"Could not collect actual knife");
            var space=runtime.Layout.NavigationNodes.Select((node,index)=>new{node,index}).FirstOrDefault(n=>PlayerCapsuleClear(n.node.Position)&&runtime.Bodies.Where(b=>b.ActorId!="CH_01").All(b=>Vector3.Distance(b.transform.position,n.node.Position)>7)&&Physics.OverlapSphere(n.node.Position+Vector3.up*1.35f,1.25f,~0,QueryTriggerInteraction.Ignore).All(c=>c.transform.IsChildOf(player.transform)||c.transform.IsChildOf(weapon.transform)));
            Require(space!=null,"No clear residue review space");SetTestOnlyPlayerPosition(space.node.Position,space.index);runtime.SetLook(0,0);runtime.AdvanceOne();
            Require(((WeaponResidues)Field(runtime,"weaponResidues")).For(weapon.ObjectId).Length==0,"Clean weapon has fabricated residue");
            var victim=runtime.Bodies.Single(b=>b.ActorId=="CH_02");var resident=runtime.World.Resident(victim.ActorId);var position=space.node.Position+Vector3.forward*.86f;
            placements.Add(new PlacementReceipt{Purpose="TestOnly close target for real weapon impact and residue; not natural encounter",ActorId=victim.ActorId,From=victim.transform.position,To=position,NodeId=space.node.Id,Tick=runtime.World.Tick,PhysicalReachAccepted=true});
            runtime.World.StopResidentActivity(victim.ActorId);victim.Capsule.enabled=false;victim.transform.position=position;victim.Capsule.enabled=true;resident.Position=MansionRuntime.P(position);resident.Node=space.node.Id;resident.Phase="Performing";resident.Activity="Wait";resident.ActivityTicks=3000;
            Physics.SyncTransforms();runtime.AdvanceOne();Require(runtime.SwingWeapon()=="","Attack unavailable");for(int i=0;i<60;i++){runtime.AdvanceOne();yield return null;}
            Require(((WeaponResidues)Field(runtime,"weaponResidues")).For(weapon.ObjectId).Length==1,"Actual hit did not leave one physical stain");
            runtime.SetLook(0,25);runtime.AdvanceOne();Require(runtime.Examine(weapon.ObjectId)=="Pending","Stained held weapon cannot be examined");for(int i=0;i<125;i++){runtime.AdvanceOne();yield return null;}
            Require(runtime.ReadInspection().State=="Completed","Stain inspection was interrupted");
            var before=runtime.Knowledge.For("CH_01").Find(runtime.ReadInspection().RecordId);
            Require(before?.Predicate=="SurfaceResidue"&&before.Value=="RedStain"&&!before.IdentityConfirmed&&before.OutcomeTarget==""&&before.ActivationId=="","Inspection leaked hidden cause or failed to observe red stain");
            yield return Capture("weapon_residue_before_rinse",1,1920,1080);
            var station=UnityEngine.Object.FindFirstObjectByType<MansionWashStation>();Require(station&&station.WaterPoint,"Physical kitchen wash station missing");
            bool started=false;
            foreach(float x in new[]{.48f,.55f,.62f,.40f}){
                foreach(float z in new[]{-.26f,-.18f,0f,.18f,.26f}){
                    var pos=station.transform.position+new Vector3(x,0,z);pos.y=runtime.Layout.Room("R_KITCHEN").FloorCenter.y;
                    if(!PlayerCapsuleClear(pos))continue;SetTestOnlyPlayerPosition(pos,runtime.Layout.NearestNode(pos));runtime.SetLook(270,20);runtime.AdvanceOne();
                    runtime.Interact(station.GetComponent<FixtureTarget>().StableId);
                    if(!((WeaponRinseState)Field(runtime,"weaponRinse")).Running)continue;
                    for(int i=0;i<45&&((WeaponRinseState)Field(runtime,"weaponRinse")).Running;i++){runtime.AdvanceOne();yield return null;}
                    if(((WeaponRinseState)Field(runtime,"weaponRinse")).Running){started=true;break;}
                }
                if(started)break;
            }
            Require(started,"Could not physically reach the tap without a collision");
            runtime.SetMove(.01,0);runtime.SetMove(0,0);
            Require(!((WeaponRinseState)Field(runtime,"weaponRinse")).Running&&((WeaponResidues)Field(runtime,"weaponResidues")).For(weapon.ObjectId).All(m=>m.RinsedTick<0),"Interrupted rinse changed the mark");
            runtime.AdvanceOne();runtime.Interact(station.GetComponent<FixtureTarget>().StableId);Require(((WeaponRinseState)Field(runtime,"weaponRinse")).Running,"Rinse did not restart");
            for(int i=0;i<80;i++){runtime.AdvanceOne();yield return null;}
            Require(((WeaponRinseState)Field(runtime,"weaponRinse")).Running&&station.Water.activeSelf,"Rinse did not reach the running water");
            Require(runtime.HeldWeaponHint().Contains("씻는 중")&&!runtime.HeldWeaponHint().Contains("공격"),"Rinse HUD still advertises an unavailable attack");
            string save=runtime.SaveSlot();Require(save=="저장 완료","Mid-rinse save failed: "+save);
            yield return Capture("weapon_rinse_mid_action",1,1920,1080);
            for(int i=0;i<110;i++){runtime.AdvanceOne();yield return null;}
            Require(((WeaponRinseState)Field(runtime,"weaponRinse")).Phase=="Completed","Rinse did not finish");
            string load=runtime.LoadSlot();Require(load=="불러오기 완료","Mid-rinse restore failed: "+load);
            Require(((WeaponRinseState)Field(runtime,"weaponRinse")).Elapsed==80&&station.Water.activeSelf,"Rinse cursor/water not restored");
            for(int i=0;i<110;i++){runtime.AdvanceOne();yield return null;}
            Require(runtime.World.Events.Count(e=>e.Type=="WeaponRinsed")==1,"Rinse duplicated after restore");
            Require(((WeaponResidues)Field(runtime,"weaponResidues")).For(weapon.ObjectId).All(m=>m.RinsedTick>=0),"Rinse failed to change the physical stain");
            runtime.SetLook(270,25);runtime.AdvanceOne();Require(runtime.Examine(weapon.ObjectId)=="Pending","Rinsed weapon not inspectable");for(int i=0;i<125;i++){runtime.AdvanceOne();yield return null;}
            var after=runtime.Knowledge.For("CH_01").Find(runtime.ReadInspection().RecordId);
            Require(after?.Predicate=="SurfaceResidue"&&after.Value=="FaintRedStain"&&after.ProvenanceKey==before.ProvenanceKey,"Rinse reread is not the same physical source");
            Require(runtime.Knowledge.For("CH_01").Find(before.Id)?.Value=="RedStain","Rinsing erased an already received observation");
            Require(runtime.SaveSlot()=="저장 완료"&&runtime.LoadSlot()=="불러오기 완료","Completed rinse did not survive save/restore");
            yield return Capture("weapon_residue_after_rinse",1,1920,1080);
            receipt.Scope+=" Residue slice: actual knife collision deposits one stain; timed inspection records appearance only; real tap and swept hand motion; interruption before rinse preserves original; mid-rinse save/load completes once; old observation retained, faint stain has same physical provenance. Test-only placement, no natural NPC case/full trial/final art claim.";
        }
    }
}
