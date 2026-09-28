using NUnit.Framework;
using UnityEngine;
using BASSLINE.AuthoringData;
namespace BASSLINE.Tests
{
    public sealed class RoomObservationGeometryTests
    {
        GameObject root;MansionLayout layout;
        [SetUp]public void Setup(){root=new GameObject("Room volume test");layout=root.AddComponent<MansionLayout>();}
        [TearDown]public void Cleanup(){Object.DestroyImmediate(root);}
        MansionRoom Room(string id,float floor,float ceiling=3)
        {
            var node=new GameObject(id);node.transform.SetParent(root.transform);var r=node.AddComponent<MansionRoom>();r.RoomId=id;r.FloorCenter=new Vector3(0,floor,0);r.CeilingHeight=ceiling;r.Bounds=new Bounds(new Vector3(0,floor+6,0),new Vector3(24,12,24));return r;
        }
        [Test]public void HandHeightUsesRoomVolumeButFloorAndCeilingDoNotLeakAcrossStoreys()
        {
            var ground=Room("GROUND",0);var upper=Room("UPPER",3.4f);layout.Rooms=new[]{ground,upper};
            Assert.That(layout.RoomAt(new Vector3(0,1.15f,0)),Is.Null,"Walking feet keep their original strict floor band.");
            Assert.That(layout.RoomContainingPoint(new Vector3(0,1.15f,0)),Is.SameAs(ground));
            Assert.That(layout.RoomContainingPoint(new Vector3(0,4.55f,0)),Is.SameAs(upper));
            Assert.That(layout.RoomContainingPoint(new Vector3(0,3.15f,0)),Is.Null,"Solid slab between rooms is not their air volume.");
            Assert.That(layout.RoomContainingPoint(new Vector3(30,1,0)),Is.Null);
        }
        [Test]public void NestedRoomUsesSmallerFootprintOnTheSameFloor()
        {
            var large=Room("LARGE",0);var small=Room("SMALL",0);small.Bounds=new Bounds(Vector3.up*6,new Vector3(4,12,4));layout.Rooms=new[]{large,small};
            Assert.That(layout.RoomContainingPoint(new Vector3(0,1,0)),Is.SameAs(small));Assert.That(layout.RoomContainingPoint(new Vector3(4,1,0)),Is.SameAs(large));
        }
        [Test]public void LibraryPitAndGalleryRingRespectTheirActualHorizontalShape()
        {
            var library=Room("R_LIBRARY",0);layout.Rooms=new[]{library};
            Assert.That(layout.RoomContainingPoint(new Vector3(0,-1,0)),Is.SameAs(library));Assert.That(layout.RoomContainingPoint(new Vector3(9,1,9)),Is.Null);
            var ring=Room("R_RING",4);ring.GeometryType="PerimeterRing";layout.Rooms=new[]{ring};
            Assert.That(layout.RoomContainingPoint(new Vector3(0,5,0)),Is.Null);Assert.That(layout.RoomContainingPoint(new Vector3(11,5,0)),Is.SameAs(ring));
        }
    }
}
