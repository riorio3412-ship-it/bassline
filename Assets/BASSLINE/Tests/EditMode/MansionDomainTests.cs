using System;
using System.Linq;
using NUnit.Framework;
using BASSLINE.Core;
using BASSLINE.World.Mansion;
namespace BASSLINE.Tests
{
    public sealed class MansionDomainTests
    {
        sealed class FlatPhysics:IMansionPhysics
        {
            public MansionWorld World;
            public Point3 Move(string actor,Point3 motion)=>World.Resident(actor).Position.Plus(motion);
            public bool DoorClear(string id)=>true;
            public void DoorPose(string id,double open){}
        }
        static MansionWorld World()
        {
            var residents=Enumerable.Range(1,18).Select(i=>new ResidentState{Id="CH_"+i.ToString("00"),Node="N_A",Position=new Point3(i,0,0)}).ToArray();
            return new MansionWorld(new MansionState{MapVersion="TEST",CatalogHash="TEST",Residents=residents,Doors=new[]{new MansionDoorState{Id="D_1"}}},new[]{new MansionNode{Id="N_A",Room="R_A",Position=default},new MansionNode{Id="N_B",Room="R_B",Position=new Point3(10,0,0)}},new[]{new MansionEdge{From="N_A",To="N_B",Door="D_1"},new MansionEdge{From="N_B",To="N_A",Door="D_1"}});
        }
        [Test] public void SnapshotIsImmutableAndInputStateIsNotAliased()
        {
            var world=World();var saved=world.Capture();saved.Residents[0].Position=new Point3(90,0,0);saved.Doors[0].Locked=true;
            Assert.That(world.Resident("CH_01").Position.X,Is.EqualTo(1));Assert.That(world.Door("D_1").Locked,Is.False);
        }
        [Test] public void ReservationDoesNotMeanArrivalAndCannotDoubleAllocate()
        {
            var world=World();Assert.That(world.Plan("CH_02","N_B","Read",120),Is.EqualTo("Accepted"));
            Assert.That(world.Resident("CH_02").Phase,Is.EqualTo("Travelling"));Assert.That(world.Resident("CH_02").Position.X,Is.EqualTo(2));
            Assert.That(world.Plan("CH_03","N_B","Read",120),Is.Not.EqualTo("Accepted"));
        }
        [Test] public void NestedPauseFreezesClockMovementAndDoorAnimation()
        {
            var world=World();var physics=new FlatPhysics{World=world};world.UseDoor("D_1","CH_01");world.Pause("NOTE",true);world.Pause("MENU",true);world.Pause("NOTE",false);
            for(int i=0;i<180;i++)world.Step(new Point3(1,0,0),true,physics);
            Assert.That(world.Tick,Is.Zero);Assert.That(world.Door("D_1").Open,Is.Zero);Assert.That(world.Resident("CH_01").Position.X,Is.EqualTo(1));world.Pause("MENU",false);world.Step(default,false,physics);Assert.That(world.Tick,Is.EqualTo(1));
        }
        [Test] public void APathDoesNotRevealUnobservedDoorLock()
        {
            var world=World();world.Door("D_1").Locked=true;
            Assert.That(world.FindPath("N_A","N_B",Array.Empty<string>()),Is.EqualTo(new[]{"N_A","N_B"}));
            Assert.That(world.FindPath("N_A","N_B",new[]{"D_1"}),Is.Empty);
        }
    }
}
