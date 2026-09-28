using System.Collections;
using System.Linq;
using BASSLINE.AuthoringData;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace BASSLINE.Tests
{
    public sealed partial class CommonReturnPlayModeTests
    {
        [UnityTest]public IEnumerator IndividualCollectorModelKeepsRealScaleAndArtAddsNoCollisionShapes()
        {
            var body=runtime.Bodies.Single(b=>b.ActorId=="CH_18");var visual=body.transform.Find("CharacterProxy");
            Assert.That(visual,Is.Not.Null);Assert.That(visual.GetComponentsInChildren<Collider>(),Is.Empty);
            Assert.That(visual.GetComponentsInChildren<Rigidbody>(),Is.Empty);
            Assert.That(visual.GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh.name=="M17_Trouser_-1"),Is.True);
            var points=visual.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>body.transform.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
            Assert.That(points.Min(p=>p.y),Is.InRange(-.001f,.01f),"Model soles must stay at the actor origin.");
            Assert.That(points.Max(p=>p.y),Is.EqualTo(body.Height).Within(.01f),"The rendered model must keep the specified 182 cm height.");
            Assert.That(body.Height,Is.EqualTo(1.82f).Within(.001f));yield return null;
        }
        [UnityTest]public IEnumerator CollectorWorkwearAndWatchFollowTheActualCleanupRigWithoutStretching()
        {
            Borrow();PlaceOnTray();Place("CH_01",new Vector3(-4,0,-5.3f));StartCollector();
            var body=runtime.Bodies.Single(b=>b.ActorId=="CH_18");var arms=body.GetComponentsInChildren<ActorArmRig>();
            var left=arms.Single(a=>a.Side<0);var watch=left.Wrist.Find("Digital watch");Assert.That(watch,Is.Not.Null);
            Vector3 original=watch.position;bool moved=false,carried=false;
            for(int i=0;i<2400&&runtime.CaptureSession().ItemExchange.CommonReturn.Entries.Length==0;i++){
                Ticks(1);moved|=Vector3.Distance(watch.position,original)>.05f;carried|=runtime.World.Resident("CH_18").HeldObject==Pen;
                Assert.That(Vector3.Distance(watch.position,left.Wrist.TransformPoint(new Vector3(0,.025f,0))),Is.LessThan(.0001f));
                foreach(var arm in arms){
                    Assert.That(Vector3.Distance(arm.Upper.position,arm.Forearm.position),Is.EqualTo(arm.UpperLength).Within(.0001f));
                    Assert.That(Vector3.Distance(arm.Forearm.position,arm.Wrist.position),Is.EqualTo(arm.ForearmLength).Within(.0001f));
                }
                if(i%120==0)yield return null;
            }
            Assert.That(moved,Is.True);Assert.That(carried,Is.True);Assert.That(runtime.CaptureSession().ItemExchange.CommonReturn.Entries.Length,Is.EqualTo(1));
        }
    }
}
