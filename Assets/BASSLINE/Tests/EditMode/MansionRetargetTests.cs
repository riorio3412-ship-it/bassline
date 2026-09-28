using System;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.World.Mansion;
using NUnit.Framework;
public sealed class MansionRetargetTests
{
    static readonly MansionNode[] Nodes={
        new MansionNode{Id="A",Room="ROOM",Position=default},
        new MansionNode{Id="B",Room="ROOM",Position=new Point3(2,0,0)},
        new MansionNode{Id="C",Room="ROOM",Position=new Point3(4,0,0)},
        new MansionNode{Id="OLD",Room="ROOM",Position=new Point3(6,0,0)},
        new MansionNode{Id="NEW",Room="ROOM",Position=new Point3(4,0,2)}};
    static MansionEdge[] Edges(bool door)=>new[]{
        new MansionEdge{From="A",To="B"},new MansionEdge{From="B",To="C",Door=door?"D_1":""},
        new MansionEdge{From="C",To="OLD"},new MansionEdge{From="C",To="NEW"}};
    sealed class Movement:IMansionPhysics
    {
        public MansionWorld World;
        public Point3 Move(string actor,Point3 motion)=>World.Resident(actor).Position.Plus(motion);
        public bool DoorClear(string id)=>true;
        public void DoorPose(string id,double open){}
    }
    static MansionWorld Create(bool door=false)
    {
        var state=new MansionState{Residents=Enumerable.Range(1,18).Select(n=>new ResidentState{Id="CH_"+n.ToString("00"),Node="A"}).ToArray(),
            Doors=door?new[]{new MansionDoorState{Id="D_1",Locked=true}}:Array.Empty<MansionDoorState>()};
        var actor=state.Residents[1];actor.Node="A";actor.Position=new Point3(3,0,0);actor.Phase="Travelling";
        actor.Destination="OLD";actor.Path=new[]{"A","B","C","OLD"};actor.PathCursor=2;
        return new MansionWorld(state,Nodes,Edges(door));
    }
    [Test] public void SummonsDuringSmoothedTravelContinuesForwardAndSurvivesRestore()
    {
        var world=Create();var actor=world.Resident("CH_02");var position=actor.Position;
        Assert.That(world.Plan(actor.Id,"NEW","WaitForCourt",900),Is.EqualTo("Accepted"));
        Assert.That(actor.Position.Distance(position),Is.Zero,"Retargeting is not physical movement");
        world=new MansionWorld(world.Capture(),Nodes,Edges(false));actor=world.Resident("CH_02");
        var physics=new Movement{World=world};world.Step(default,false,physics);
        Assert.That(actor.Position.X,Is.GreaterThan(position.X),"Do not return to the obsolete exact node");
        for(int i=0;i<300;i++)world.Step(default,false,physics);
        Assert.That(actor.Phase,Is.EqualTo("Performing"));
        Assert.That(actor.Position.Distance(Nodes[4].Position),Is.LessThan(.14));
    }
    [Test] public void RetargetingPreservesIncomingDoorPermissionChecks()
    {
        var world=Create(true);var actor=world.Resident("CH_02");var position=actor.Position;
        Assert.That(world.Plan(actor.Id,"NEW","WaitForCourt",900),Is.EqualTo("Accepted"));
        world.Step(default,false,new Movement{World=world});
        Assert.That(actor.Position.Distance(position),Is.Zero);
        Assert.That(actor.KnownLocked,Does.Contain("D_1"));
        Assert.That(world.Door("D_1").Locked,Is.True);
    }
}
