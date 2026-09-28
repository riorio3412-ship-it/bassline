using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using BASSLINE.AuthoringData;

namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionSmokeProbe
    {
        IEnumerator ReviewWeapons()
        {
            driveWorld=false;
            var weapons=runtime.ObjectBodies.Where(o=>o.GetComponent<MansionWeapon>()).ToArray();
            Require(weapons.Length==3,"Expected three authored weapon objects");
            foreach(var weapon in weapons){
                bool reached=false;
                var from=player.transform.position;
                foreach(var node in runtime.Layout.NavigationNodes.Select((node,index)=>new{node,index}).Where(n=>Vector3.Distance(n.node.Position,weapon.transform.position)<2.3f).OrderBy(n=>Vector3.Distance(n.node.Position,weapon.transform.position))){
                    if(!PlayerCapsuleClear(node.node.Position))continue;
                    SetTestOnlyPlayerPosition(node.node.Position,node.index);
                    Vector3 angles=Quaternion.LookRotation(weapon.transform.position-Camera.main.transform.position).eulerAngles;
                    runtime.SetLook(angles.y,Mathf.DeltaAngle(0,angles.x));
                    if(!runtime.DescribeTarget(weapon.ObjectId).Available)continue;
                    string result=runtime.Interact(weapon.ObjectId);
                    if(runtime.World.Resident("CH_01").HeldObject!=weapon.ObjectId)continue;
                    placements.Add(new PlacementReceipt{Purpose="TestOnly weapon reach framing; not a completed travel route",ActorId="CH_01",From=from,To=node.node.Position,NodeId=node.node.Id,Tick=runtime.World.Tick,PhysicalReachAccepted=true});
                    reached=true;break;
                }
                Require(reached,"Could not physically pick up "+weapon.ObjectId);
                runtime.AdvanceOne();
                yield return Capture("weapon_held_"+weapon.ObjectId,1,1920,1080);
                // Find a genuinely empty volume in the authored mansion for a physical drop.
                var clear=runtime.Layout.NavigationNodes.Select((node,index)=>new{node,index}).FirstOrDefault(n=>PlayerCapsuleClear(n.node.Position)&&Physics.OverlapSphere(n.node.Position+Vector3.up*1.35f,.82f,~0,QueryTriggerInteraction.Ignore).All(c=>c.transform.IsChildOf(player.transform)||c.transform.IsChildOf(weapon.transform)));
                Require(clear!=null,"No clear physical weapon review space");
                placements.Add(new PlacementReceipt{Purpose="TestOnly drop framing; not natural travel",ActorId="CH_01",From=player.transform.position,To=clear.node.Position,NodeId=clear.node.Id,Tick=runtime.World.Tick,PhysicalReachAccepted=true});
                SetTestOnlyPlayerPosition(clear.node.Position,clear.index);runtime.SetLook(0,25);runtime.AdvanceOne();
                Require(runtime.HeldWeaponId()==weapon.ObjectId,"Held weapon cannot be selected for inspection");
                Require(runtime.Examine(weapon.ObjectId)=="Pending","Held weapon could not be examined from its visible hand position");
                for(int i=0;i<125;i++){runtime.AdvanceOne();yield return null;}
                Require(runtime.ReadInspection().State=="Completed","Held-weapon inspection did not complete");
                Require(runtime.Knowledge.For("CH_01").Records().Any(r=>r.SubjectId==weapon.ObjectId&&r.Predicate=="ObjectFeature"),"Inspection failed to record the visible weapon features");
                var release=weapon.transform.position;int originalContacts=runtime.World.Object(weapon.ObjectId).PhysicalContacts.Length;
                runtime.Interact("DROP");
                Require(Vector3.Distance(MansionRuntime.V(runtime.World.Object(weapon.ObjectId).Position),release)<.01f,"Release teleported the object away from the hand");
                for(int i=0;i<12;i++){runtime.AdvanceOne();yield return null;}
                var falling=runtime.World.Object(weapon.ObjectId);
                Require(falling.Location=="World"&&falling.Position.Y<release.y-.03f&&falling.LinearVelocity.Y<0,"Released object did not fall under gravity");
                string saved=runtime.SaveSlot();Require(saved=="저장 완료","Falling-object save failed: "+saved);
                var savedPose=falling.Copy();
                for(int i=0;i<120;i++){runtime.AdvanceOne();yield return null;}
                var settled=runtime.World.Object(weapon.ObjectId);
                Require(settled.PhysicalContacts.Length>originalContacts,"Drop created no physical contact history");
                Require(settled.Position.Y>clear.node.Position.y-.03f&&settled.Position.Y<clear.node.Position.y+.4f,"Weapon did not settle on the authored floor");
                var firstRest=settled.Copy();
                var down=Quaternion.LookRotation(weapon.transform.position-Camera.main.transform.position).eulerAngles;
                runtime.SetLook(down.y,Mathf.DeltaAngle(0,down.x));
                yield return Capture("weapon_dropped_"+weapon.ObjectId,1,1920,1080);
                string loaded=runtime.LoadSlot();Require(loaded=="불러오기 완료","Falling-object load failed: "+loaded);
                var restored=runtime.World.Object(weapon.ObjectId);
                Require(restored.Position.Distance(savedPose.Position)<.0001&&restored.LinearVelocity.Distance(savedPose.LinearVelocity)<.0001,"Falling position or velocity was not restored");
                Require(restored.PhysicalContacts.Length==savedPose.PhysicalContacts.Length,"Loading invented past contacts");
                // The same floor remains after load; future physics is simulated again, not replayed.
                for(int i=0;i<120;i++){runtime.AdvanceOne();yield return null;}
                var resumed=runtime.World.Object(weapon.ObjectId);
                Require(resumed.Position.Distance(firstRest.Position)<.08,"Same-input resumed drop diverged beyond the physical tolerance");
                Require(resumed.PhysicalContacts.Any(c=>c.CauseEventSequence==resumed.ReleaseEventSequence),"Drop contact lost its release event linkage");
                Require(!runtime.World.Events.Any(e=>e.Type=="WeaponSwingStarted"),"Removed free-attack producer is still active");

            }
            receipt.Scope+=" Picked up and physically dropped all three authored weapons, recorded actual floor contacts, saved while falling and resumed from the same pose/velocity with explicit test-only framing. No free player attack, injury, death or incident-progression claim.";
        }
    }
}
