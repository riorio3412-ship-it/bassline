using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using BASSLINE.AuthoringData;
using BASSLINE.Core;
using BASSLINE.Save;
using BASSLINE.World.Mansion;

namespace BASSLINE.Bootstrap
{
    // PhysX owns the response; the world owns the resulting position, motion and contact history.
    public sealed partial class MansionRuntime
    {
        readonly Dictionary<string,Rigidbody> looseBodies=new Dictionary<string,Rigidbody>();
        readonly List<LooseContact> looseContacts=new List<LooseContact>();
        sealed class LooseContact { public string Item,Other;public bool Enter;public Vector3 Point,Normal,Velocity; }
        void InitializeLooseObjects()
        {
            foreach(var body in ObjectBodies.Where(o=>o.GetComponent<MansionWeapon>())){
                var rb=body.GetComponent<Rigidbody>()??body.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic=true;rb.useGravity=false;rb.interpolation=RigidbodyInterpolation.None;
                rb.mass=body.ObjectId=="M_WORK_HAMMER"?.7f:body.ObjectId=="M_EXHIBIT_DAGGER"?.35f:.22f;
                rb.linearDamping=.05f;rb.angularDamping=.08f;rb.maxAngularVelocity=30;
                rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                rb.solverIterations=12;rb.solverVelocityIterations=4;
                body.Collider.sharedMaterial=new PhysicsMaterial("RegisteredMetalAndGrip"){dynamicFriction=.45f,staticFriction=.6f,bounciness=.08f,frictionCombine=PhysicsMaterialCombine.Average,bounceCombine=PhysicsMaterialCombine.Minimum};
                var relay=body.GetComponent<MansionObjectContactRelay>()??body.gameObject.AddComponent<MansionObjectContactRelay>();
                string itemId=body.ObjectId;relay.Receive=(collision,enter)=>QueueLooseContact(itemId,collision,enter);
                looseBodies[itemId]=rb;
            }
        }
        void PrepareLooseObjects(bool restore=false)
        {
            if(restore)looseContacts.Clear();
            foreach(var pair in looseBodies){
                var item=World.Object(pair.Key);var rb=pair.Value;
                bool active=item.PhysicsVersion==1&&item.Location=="World"&&string.IsNullOrEmpty(item.AnchorId);
                bool changed=rb.isKinematic==active;
                rb.isKinematic=!active;rb.useGravity=active;
                if(!active){if(item.Location=="Hand")rb.GetComponent<FixtureObjectBody>().Collider.enabled=false;continue;}
                rb.GetComponent<FixtureObjectBody>().Collider.enabled=true;
                // Do not reseed a live solver each tick: restore only a custody/position transition.
                if(restore||changed||Vector3.Distance(rb.position,V(item.Position))>.002f){
                    rb.position=V(item.Position);rb.rotation=Quaternion.Euler(V(item.Rotation));
                    rb.linearVelocity=V(item.LinearVelocity);rb.angularVelocity=V(item.AngularVelocity);
                    if(item.PhysicsSleeping)rb.Sleep();else rb.WakeUp();
                }
            }
        }
        string PhysicalColliderId(Collider collider)
        {
            string owner=collider.GetComponentInParent<FixtureTarget>()?.StableId??collider.GetComponentInParent<MansionProp>()?.ObjectId;
            if(!string.IsNullOrEmpty(owner))return owner+"/"+collider.name;
            var names=new List<string>();for(var t=collider.transform;t!=null;t=t.parent)names.Add(t.name);
            names.Reverse();return string.Join("/",names);
        }
        void QueueLooseContact(string itemId,Collision collision,bool enter)
        {
            if(World==null||World.Object(itemId).Location!="World")return;
            var c=new LooseContact{Item=itemId,Other=PhysicalColliderId(collision.collider),Enter=enter,Velocity=collision.relativeVelocity};
            if(enter&&collision.contactCount>0){var point=collision.GetContact(0);c.Point=point.point;c.Normal=point.normal;}
            looseContacts.Add(c);
        }
        void CommitLooseObjects()
        {
            foreach(var pair in looseBodies){
                if(pair.Value.isKinematic)continue;var item=World.Object(pair.Key);var rb=pair.Value;
                item.Position=P(rb.position);item.Rotation=P(rb.rotation.eulerAngles);
                item.LinearVelocity=P(rb.linearVelocity);item.AngularVelocity=P(rb.angularVelocity);item.PhysicsSleeping=rb.IsSleeping();
            }
            foreach(var c in looseContacts.OrderBy(c=>c.Item,StringComparer.Ordinal).ThenBy(c=>c.Other,StringComparer.Ordinal)){
                var item=World.Object(c.Item);if(item.Location!="World")continue;
                if(c.Enter){
                    if(item.ActiveContacts.Contains(c.Other))continue;
                    item.ActiveContacts=item.ActiveContacts.Concat(new[]{c.Other}).ToArray();
                    World.Emit("ObjectPhysicalContact","",item.Id,c.Other);
                    var contact=new MansionObjectContact{OtherId=c.Other,EventSequence=World.EventSequence,CauseEventSequence=item.ReleaseEventSequence,ChainId=item.ReleaseEventSequence>0?"RELEASE_L"+World.Loop+"_"+item.ReleaseEventSequence:"INITIAL_"+item.Id,BeginTick=World.Tick,Position=P(c.Point),Normal=P(c.Normal),RelativeVelocity=P(c.Velocity)};
                    item.PhysicalContacts=item.PhysicalContacts.Concat(new[]{contact}).ToArray();
                }else{
                    item.ActiveContacts=item.ActiveContacts.Where(id=>id!=c.Other).ToArray();
                    var prior=item.PhysicalContacts.LastOrDefault(x=>x.OtherId==c.Other&&x.EndTick<0);if(prior!=null)prior.EndTick=World.Tick;
                }
            }
            looseContacts.Clear();
        }
        MansionSessionSnapshot UpgradeLooseObjects(MansionSessionSnapshot s)
        {
            if(s?.World==null)return s;
            foreach(var body in ObjectBodies.Where(o=>o.GetComponent<MansionWeapon>())){
                var item=s.World.Objects.Single(o=>o.Id==body.ObjectId);if(item.PhysicsVersion!=0)continue;
                item.PhysicsVersion=1;item.Rotation=P(body.GetComponent<MansionWeapon>().InitialEuler);
                item.LinearVelocity=default;item.AngularVelocity=default;item.PhysicsSleeping=false;
                item.ActiveContacts=Array.Empty<string>();item.PhysicalContacts=Array.Empty<MansionObjectContact>();
            }
            // Legacy swings had no damage. Do not turn a saved harmless gesture into an attack.
            if(s.WeaponMotion!=null&&s.WeaponMotion.Version==0&&s.WeaponMotion.Running)s.WeaponMotion.Phase="Cancelled";
            return s;
        }
    }
}
