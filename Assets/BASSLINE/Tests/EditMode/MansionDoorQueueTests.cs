using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using BASSLINE.Core;
using BASSLINE.World.Mansion;
public sealed class MansionDoorQueueTests
{
    static readonly MansionNode[] Nodes={new MansionNode{Id="N_A",Room="R_A",Position=default},new MansionNode{Id="N_B",Room="R_B",Position=new Point3(10,0,0)}};
    static readonly MansionEdge[] Edges={new MansionEdge{From="N_A",To="N_B",Door="D_1"},new MansionEdge{From="N_B",To="N_A",Door="D_1"}};
    class FlatPhysics:IMansionPhysics
    {
        public MansionWorld World;public bool AreaClear=true;public double PresentedOpen;
        public Point3 Move(string actor,Point3 motion)=>World.Resident(actor).Position.Plus(motion);
        public bool DoorClear(string id)=>AreaClear;
        public void DoorPose(string id,double open){PresentedOpen=open;}
    }
    sealed class PassagePhysics:FlatPhysics,IMansionPassagePhysics
    {
        public readonly HashSet<string> ClearedActors=new HashSet<string>();
        public bool ActorClearedDoor(string id,string actor)=>ClearedActors.Contains(actor);
    }
    static MansionWorld World()=>new MansionWorld(new MansionState{Residents=Enumerable.Range(1,18).Select(i=>new ResidentState{Id="CH_"+i.ToString("00"),Node="N_A",Position=new Point3(i,0,0)}).ToArray(),Doors=new[]{new MansionDoorState{Id="D_1"}}},Nodes,Edges);
    static void Step(MansionWorld world,IMansionPhysics physics,int count=1){for(int i=0;i<count;i++)world.Step(default,false,physics);}
    [Test] public void WaitingActorInsideClearanceDoesNotBlockCompletedHolderRelease()
    {
        var world=World();var physics=new PassagePhysics{World=world,AreaClear=false};world.UseDoor("D_1","CH_02");world.UseDoor("D_1","CH_03");Step(world,physics);
        physics.ClearedActors.Add("CH_02");Step(world,physics,90);
        Assert.That(world.Door("D_1").Holder,Is.EqualTo("CH_03"));Assert.That(world.Door("D_1").Open,Is.EqualTo(1));Assert.That(world.Events.Single(e=>e.Type=="DoorReleased").Actor,Is.EqualTo("CH_02"));
    }
    [Test] public void GrantKeepsFull90TicksAndFifoNeverJumps()
    {
        var world=World();var physics=new PassagePhysics{World=world};foreach(string id in new[]{"CH_02","CH_03","CH_04"})world.UseDoor("D_1",id);Step(world,physics);physics.ClearedActors.UnionWith(new[]{"CH_02","CH_03","CH_04"});
        Assert.That(world.Door("D_1").GrantedTick,Is.EqualTo(1));Step(world,physics,89);Assert.That(world.Tick,Is.EqualTo(90));Assert.That(world.Door("D_1").Holder,Is.EqualTo("CH_02"));Step(world,physics);Assert.That(world.Tick,Is.EqualTo(91));Assert.That(world.Door("D_1").Holder,Is.EqualTo("CH_03"));
        world.UseDoor("D_1","CH_04");world.UseDoor("D_1","CH_02");Assert.That(world.Door("D_1").Queue,Is.EqualTo(new[]{"CH_04","CH_02"}));Step(world,physics,90);Assert.That(world.Door("D_1").Holder,Is.EqualTo("CH_04"));Assert.That(world.Events.Where(e=>e.Type=="DoorGranted").Select(e=>e.Actor),Is.EqualTo(new[]{"CH_02","CH_03","CH_04"}));
    }
    [Test] public void AreaClearanceStillPreventsClosingAfterHolderHasLeft()
    {
        var world=World();var physics=new PassagePhysics{World=world,AreaClear=false};world.UseDoor("D_1","CH_02");Step(world,physics,30);physics.ClearedActors.Add("CH_02");Step(world,physics,61);Assert.That(world.Door("D_1").Holder,Is.Empty);Assert.That(world.Door("D_1").Open,Is.EqualTo(1));Step(world,physics,30);Assert.That(world.Door("D_1").Open,Is.EqualTo(1));
        physics.AreaClear=true;Step(world,physics,31);Assert.That(world.Door("D_1").Open,Is.EqualTo(0));
    }
    [Test] public void HolderInsideDoorCannotReleaseEvenWhenGlobalAdapterReportsClear()
    {
        var world=World();var physics=new PassagePhysics{World=world,AreaClear=true};world.UseDoor("D_1","CH_02");world.UseDoor("D_1","CH_03");Step(world,physics,240);Assert.That(world.Door("D_1").Holder,Is.EqualTo("CH_02"));Assert.That(world.Events.Any(e=>e.Type=="DoorReleased"),Is.False);
    }
    [Test] public void ExistingFlatPhysicsKeepsSafeFallbackBehavior()
    {
        var world=World();var physics=new FlatPhysics{World=world,AreaClear=false};world.UseDoor("D_1","CH_02");world.UseDoor("D_1","CH_03");Step(world,physics,120);Assert.That(world.Door("D_1").Holder,Is.EqualTo("CH_02"));physics.AreaClear=true;Step(world,physics);Assert.That(world.Door("D_1").Holder,Is.EqualTo("CH_03"));
    }
    [Test] public void OnlyResidentsAndPresenterMayQueueAndUnavailableResidentsArePruned()
    {
        var world=World();var physics=new PassagePhysics{World=world,AreaClear=false};Assert.That(world.UseDoor("D_1","UNKNOWN"),Is.EqualTo("Unavailable"));Assert.That(world.UseDoor("D_1","CH_19",true),Is.EqualTo("Unavailable"));Assert.That(world.UseDoor("D_1",null),Is.EqualTo("Unavailable"));
        world.UseDoor("D_1","CH_02");world.UseDoor("D_1","CH_03");world.UseDoor("D_1","PRES_YUSTI");Step(world,physics,30);world.Resident("CH_02").Alive=false;world.Resident("CH_03").Present=false;Step(world,physics);
        Assert.That(world.Door("D_1").Holder,Is.EqualTo("PRES_YUSTI"));Assert.That(world.Door("D_1").Queue,Is.Empty);Assert.That(world.Door("D_1").Open,Is.EqualTo(1));Assert.That(world.UseDoor("D_1","CH_02"),Is.EqualTo("Unavailable"));Assert.That(world.Events.Count(e=>e.Type=="DoorQueueCancelled"),Is.EqualTo(1));Step(world,physics);Assert.That(world.Events.Count(e=>e.Type=="DoorQueueCancelled"),Is.EqualTo(1));
    }
    [Test] public void RestoreRetainsRemainingGrantAgeAndRejectsInvalidGrantOrQueue()
    {
        var world=World();var physics=new PassagePhysics{World=world};world.UseDoor("D_1","CH_02");world.UseDoor("D_1","CH_03");Step(world,physics,40);var saved=world.Capture();var restored=new MansionWorld(saved,Nodes,Edges);physics.World=restored;physics.ClearedActors.Add("CH_02");Step(restored,physics,50);Assert.That(restored.Door("D_1").Holder,Is.EqualTo("CH_02"));Step(restored,physics);Assert.That(restored.Door("D_1").Holder,Is.EqualTo("CH_03"));
        var bad=saved.Copy();bad.Doors[0].GrantedTick=0;Assert.Throws<ArgumentException>(()=>new MansionWorld(bad,Nodes,Edges));bad=saved.Copy();bad.Doors[0].GrantedTick=bad.Tick+1;Assert.Throws<ArgumentException>(()=>new MansionWorld(bad,Nodes,Edges));bad=saved.Copy();bad.Doors[0].Queue=new[]{"FAKE_ACTOR"};Assert.Throws<ArgumentException>(()=>new MansionWorld(bad,Nodes,Edges));bad=saved.Copy();bad.Residents.Single(a=>a.Id=="CH_03").Alive=false;Assert.Throws<ArgumentException>(()=>new MansionWorld(bad,Nodes,Edges));bad=saved.Copy();bad.Doors[0].Queue=new[]{"CH_02"};Assert.Throws<ArgumentException>(()=>new MansionWorld(bad,Nodes,Edges));
        var presenter=saved.Copy();presenter.Doors[0].Queue=new[]{"PRES_YUSTI"};Assert.DoesNotThrow(()=>new MansionWorld(presenter,Nodes,Edges));
    }

    [Test] public void OwnerCanExitLockedBedroomWithoutRememberingOwnDoorAsImpassable()
    {
        var world=World();var physics=new FlatPhysics{World=world};var door=world.Door("D_1");door.OwnerId="CH_02";door.RelockAfterUse=true;door.Locked=true;
        world.Resident("CH_02").Position=default;world.Resident("CH_02").KnownLocked=new[]{"D_1"};
        Assert.That(world.Plan("CH_02","N_B","Rest",60),Is.EqualTo("Accepted"));Step(world,physics,2);
        Assert.That(door.Locked,Is.False);Assert.That(world.Resident("CH_02").KnownLocked,Is.Empty);Assert.That(world.Events.Count(e=>e.Type=="LockChanged"&&e.Actor=="CH_02"&&e.Detail=="Unlocked:OwnerPermission"),Is.EqualTo(1));
        Assert.That(world.Events.Any(e=>e.Type=="LockedDoorObserved"&&e.Actor=="CH_02"),Is.False);
    }
    [Test] public void PermissionAndLockAreSeparate_OnlyApproachDiscoversDenial()
    {
        var world=World();var physics=new FlatPhysics{World=world};var door=world.Door("D_1");door.OwnerId="CH_02";door.Locked=true;
        Assert.That(world.FindPath("N_A","N_B",Array.Empty<string>()),Is.EqualTo(new[]{"N_A","N_B"}));Assert.That(world.UseDoor("D_1","CH_03",true),Is.EqualTo("소유자의 허락이 필요합니다."));Assert.That(door.Locked,Is.True);
        world.Resident("CH_03").Position=default;world.Plan("CH_03","N_B","Read",60);Assert.That(world.Resident("CH_03").KnownLocked,Is.Empty);Step(world,physics,2);Assert.That(world.Resident("CH_03").KnownLocked,Is.EquivalentTo(new[]{"D_1"}));Assert.That(world.Events.Any(e=>e.Type=="DoorPermissionDenied"&&e.Actor=="CH_03"),Is.True);
        door.Locked=false;Assert.That(world.UseDoor("D_1","CH_03"),Is.EqualTo("소유자의 허락이 필요합니다."));door.Open=1;Assert.That(world.UseDoor("D_1","CH_03"),Is.EqualTo("문이 열립니다."));
    }
    [Test] public void OwnerDoorRelocksOnlyAfterPassageAndSafeFullClosure()
    {
        var world=World();var physics=new PassagePhysics{World=world};var door=world.Door("D_1");door.OwnerId="CH_02";door.Locked=true;door.RelockAfterUse=true;
        world.UseDoor("D_1","CH_02");Step(world,physics,30);physics.ClearedActors.Add("CH_02");physics.AreaClear=false;Step(world,physics,90);Assert.That(door.Holder,Is.Empty);Assert.That(door.Open,Is.EqualTo(1));Assert.That(door.Locked,Is.False);
        physics.AreaClear=true;Step(world,physics,29);Assert.That(door.Locked,Is.False);Step(world,physics,2);Assert.That(door.Open,Is.Zero);Assert.That(door.Locked,Is.True);Assert.That(world.Events.Count(e=>e.Detail=="Locked:AfterPassage"),Is.EqualTo(1));Step(world,physics,60);Assert.That(world.Events.Count(e=>e.Detail=="Locked:AfterPassage"),Is.EqualTo(1));
        var restored=new MansionWorld(world.Capture(),Nodes,Edges);Assert.That(restored.Door("D_1").OwnerId,Is.EqualTo("CH_02"));Assert.That(restored.Door("D_1").RelockAfterUse,Is.True);Assert.That(restored.UseDoor("D_1","CH_02"),Is.EqualTo("문이 열립니다."));
    }
    [Test] public void PresenterOverrideRequiresDoorEmergencyCapabilityAndExplicitCall()
    {
        var world=World();var door=world.Door("D_1");door.OwnerId="CH_02";door.Locked=true;
        Assert.That(world.UseDoor("D_1","PRES_YUSTI",emergency:true),Is.EqualTo("소유자의 허락이 필요합니다."));door.EmergencyOpen=true;
        Assert.That(world.UseDoor("D_1","PRES_YUSTI"),Is.EqualTo("소유자의 허락이 필요합니다."));Assert.That(world.UseDoor("D_1","CH_03",emergency:true),Is.EqualTo("소유자의 허락이 필요합니다."));
        Assert.That(world.UseDoor("D_1","PRES_YUSTI",emergency:true),Is.EqualTo("문이 열립니다."));Assert.That(door.Locked,Is.False);Assert.That(world.Events.Single(e=>e.Type=="LockChanged").Detail,Is.EqualTo("Unlocked:ReportedEmergencyInspection"));
        door.Queue=Array.Empty<string>();door.Locked=true;door.OwnerId="";door.EmergencyOpen=false;Assert.That(world.UseDoor("D_1","PRES_YUSTI",emergency:true),Is.EqualTo("잠겨 있습니다."));door.EmergencyOpen=true;Assert.That(world.UseDoor("D_1","PRES_YUSTI",emergency:true),Is.EqualTo("문이 열립니다."));
    }
    [Test] public void InvalidOwnerCannotBeRestored()
    {
        var world=World();var snapshot=world.Capture();snapshot.Doors[0].OwnerId="PRES_YUSTI";Assert.Throws<ArgumentException>(()=>new MansionWorld(snapshot,Nodes,Edges));snapshot.Doors[0].OwnerId="CH_19";Assert.Throws<ArgumentException>(()=>new MansionWorld(snapshot,Nodes,Edges));
    }

    [Test] public void ManualUnlockDoesNotAutolockBeforeAnyPassageAndPendingRelockSurvivesRestore()
    {
        var world=World();var physics=new PassagePhysics{World=world};var door=world.Door("D_1");door.OwnerId="CH_02";door.RelockAfterUse=true;door.Locked=true;
        world.UseDoor("D_1","CH_02",toggleLock:true);Step(world,physics,120);Assert.That(door.Locked,Is.False);Assert.That(door.RelockPending,Is.False);
        world.UseDoor("D_1","CH_02");Step(world,physics,30);var saved=world.Capture();Assert.That(saved.Doors[0].RelockPending,Is.True);var restored=new MansionWorld(saved,Nodes,Edges);physics.World=restored;physics.ClearedActors.Add("CH_02");Step(restored,physics,121);Assert.That(restored.Door("D_1").Locked,Is.True);Assert.That(restored.Door("D_1").RelockPending,Is.False);
    }

}
