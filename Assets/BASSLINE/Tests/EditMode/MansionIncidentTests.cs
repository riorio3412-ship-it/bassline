using System;
using System.Linq;
using NUnit.Framework;
using BASSLINE.Core;
using BASSLINE.World.Mansion;
using BASSLINE.Knowledge;
public sealed class MansionIncidentTests
{
    static readonly MansionNode[] Nodes={new MansionNode{Id="NODE_TOOL",Room="ROOM_W",Position=new Point3(0,0,0)},new MansionNode{Id="NODE_CONTACT",Room="ROOM_W",Position=new Point3(1,0,0)}};
    static readonly MansionEdge[] Edges={new MansionEdge{From="NODE_TOOL",To="NODE_CONTACT"},new MansionEdge{From="NODE_CONTACT",To="NODE_TOOL"}};
    sealed class Physics:IMansionPhysics,IMansionIncidentPhysics
    {
        public MansionWorld World;public bool Contact=true,Reach=true;public string[] Viewers={"CH_04","CH_03"};public bool Identity=true;
        public Point3 Move(string actor,Point3 delta)=>World.Resident(actor).Position.Plus(delta);
        public bool DoorClear(string id)=>true;public void DoorPose(string id,double open){}
        public bool CanReach(string actor,string subject,double distance)=>Reach;
        public bool CanSee(string actor,string subject)=>Viewers.Contains(actor);
        public bool Identifies(string actor,string subject)=>Identity;
        public bool HasContact(string actor,string target,string item,out Point3 point){point=World.Resident(target).Position;return Contact;}
        public bool ReceivesSpeech(string observer,string speaker)=>Viewers.Contains(observer);
    }
    static (MansionWorld world,MansionIncident incident,KnowledgeLedger knowledge,Physics physics) Fixture()
    {
        var residents=Enumerable.Range(1,18).Select(i=>new ResidentState{Id="CH_"+i.ToString("00"),Node=i==2?"NODE_CONTACT":"NODE_TOOL",Position=new Point3(i==2?1:0,0,0)}).ToArray();
        var world=new MansionWorld(new MansionState{Residents=residents,Objects=new[]{new MansionObjectState{Id="ITEM_X31",Name="가상 소품",Position=default}}},Nodes,Edges);
        var k=new KnowledgeLedger(residents.Select(r=>r.Id));foreach(var subject in new[]{"CH_02","ITEM_X31"})k.Observe("CH_04",new KnownRecord{Kind="Visual",SubjectId=subject,Predicate="AtPlace",Value="ROOM_W",FromTick=0,ToTick=1,IdentityConfirmed=true,Position=default},0);
        var incident=new MansionIncident();Assert.That(incident.Configure(world,Settings()),Is.EqualTo("Configured"));return(world,incident,k,new Physics{World=world});
    }
    static MansionCaseSettings Settings()=>new MansionCaseSettings{Enabled=true,ExplicitTestSession=true,Id="CASE_TEST_01",ActorId="CH_04",TargetId="CH_02",ObjectId="ITEM_X31",ToolNode="NODE_TOOL",ContactNode="NODE_CONTACT",DecisionReason="TestOnly 갈등과 대안 거부",IntentSpeech="이 세션의 의도 발언",RejectedAlternatives=new[]{"Withdraw","AskForHelp"},ApproachTick=1,EarliestCauseTick=10,OpportunityEndTick=500,ContactTicks=3,DelayTicks=10,MinimumIntentTicks=3};
    static void Tick(MansionWorld w,MansionIncident i,KnowledgeLedger k,Physics p){w.Step(default,false,p);i.Step(w,k.For("CH_04"),p);}
    [Test] public void DisabledByDefaultAndNoKnowledgeCannotPlan()
    {
        var f=Fixture();var other=new MansionIncident();Assert.That(other.Configure(f.world,new MansionCaseSettings()),Is.EqualTo("Disabled"));var empty=new KnowledgeLedger(f.world.Residents.Select(x=>x.Id));for(int n=0;n<50;n++)Tick(f.world,f.incident,empty,f.physics);Assert.That(f.incident.Stage,Is.EqualTo("Planned"));Assert.That(f.world.Events.Any(e=>e.Type=="IncidentCauseCommitted"),Is.False);Assert.That(f.incident.Read("CH_01").State,Is.EqualTo("Unknown"));
    }
    [Test] public void StolenToolAndBrokenContactPreventCauseWithoutReplacementVictim()
    {
        var f=Fixture();f.world.Pickup("ITEM_X31","CH_01");for(int n=0;n<100;n++)Tick(f.world,f.incident,f.knowledge,f.physics);Assert.That(f.incident.Capture().CauseTick,Is.EqualTo(-1));Assert.That(f.world.Residents.All(r=>r.Alive),Is.True);
        var g=Fixture();g.physics.Contact=false;for(int n=0;n<510;n++)Tick(g.world,g.incident,g.knowledge,g.physics);Assert.That(g.incident.Stage,Is.EqualTo("Cancelled"));Assert.That(g.world.Residents.All(r=>r.Alive),Is.True);Assert.That(g.incident.Capture().Settings.TargetId,Is.EqualTo("CH_02"));
    }
    [Test] public void ContactDelayedOutcomeSaveRestoreAndPrivateReceipts()
    {
        var f=Fixture();for(int n=0;n<300&&f.incident.Stage!="CauseCommitted";n++)Tick(f.world,f.incident,f.knowledge,f.physics);var cause=f.incident.Capture();Assert.That(cause.CauseTick,Is.GreaterThan(0));Assert.That(f.world.Resident("CH_02").Alive,Is.True);Assert.That(cause.TracePresent,Is.True);Assert.That(f.incident.CanConvene,Is.False);
        var restoredWorld=new MansionWorld(f.world.Capture(),Nodes,Edges);var restored=MansionIncident.Restore(cause,restoredWorld);f.physics.World=restoredWorld;while(restoredWorld.Tick<cause.DueTick)Tick(restoredWorld,restored,f.knowledge,f.physics);
        Assert.That(restoredWorld.Resident("CH_02").Alive,Is.False);Assert.That(restoredWorld.Events.Count(e=>e.Type=="IncidentCauseCommitted"),Is.EqualTo(1));Assert.That(restoredWorld.Events.Count(e=>e.Type=="IncidentResultCommitted"),Is.EqualTo(1));Assert.That(restored.ReceiptsFor("CH_03").Any(r=>r.Record.Predicate=="CausedOutcome"),Is.True);Assert.That(restored.ReceiptsFor("CH_01"),Is.Empty);Assert.That(restored.Read("CH_01").State,Is.EqualTo("Unknown"));
        var again=MansionIncident.Restore(restored.Capture(),restoredWorld);for(int n=0;n<20;n++)Tick(restoredWorld,again,f.knowledge,f.physics);Assert.That(restoredWorld.Events.Count(e=>e.Type=="IncidentResultCommitted"),Is.EqualTo(1));
    }
    [Test] public void DiscoveryReportConfirmationAndActualAnnouncementAreSeparate()
    {
        var f=Fixture();for(int n=0;n<300&&f.incident.Stage!="ResultCommitted";n++)Tick(f.world,f.incident,f.knowledge,f.physics);Assert.That(f.incident.Confirm(f.world,true),Is.EqualTo("Unavailable"));Assert.That(f.incident.Discover(f.world,"CH_01",f.physics),Is.EqualTo("Unavailable"));
        Assert.That(f.incident.Discover(f.world,"CH_03",f.physics),Is.EqualTo("Discovered"));Assert.That(f.incident.Report(f.world,"CH_03",false),Is.EqualTo("Unavailable"));Assert.That(f.incident.Report(f.world,"CH_03",true),Is.EqualTo("Reported"));Assert.That(f.incident.Confirm(f.world,false),Is.EqualTo("Unavailable"));Assert.That(f.incident.Confirm(f.world,true),Is.EqualTo("Confirmed"));Assert.That(f.incident.CanConvene,Is.True);
        Assert.That(f.incident.Read("CH_01").State,Is.EqualTo("Unknown"));Assert.That(f.incident.ReceiveAnnouncement(f.world,"CH_01",false),Is.EqualTo("Unavailable"));f.incident.ReceiveAnnouncement(f.world,"CH_01",true);var view=f.incident.Read("CH_01");Assert.That(view.Confirmed,Is.True);Assert.That(view.VictimId,Is.EqualTo("CH_02"));Assert.That(f.incident.ReceiptsFor("CH_01").Single().Record.Predicate,Is.EqualTo("ConfirmedDeath"));Assert.That(f.incident.ReceiptsFor("CH_01").Single().Record.SubjectId,Does.Not.Contain("CH_04"));
    }
    [Test] public void InterruptedViewDoesNotBecomeContinuousCausalityProof()
    {
        var f=Fixture();for(int n=0;n<300&&f.incident.Stage!="CauseCommitted";n++)Tick(f.world,f.incident,f.knowledge,f.physics);f.physics.Viewers=new[]{"CH_04"};Tick(f.world,f.incident,f.knowledge,f.physics);f.physics.Viewers=new[]{"CH_04","CH_03"};for(int n=0;n<15;n++)Tick(f.world,f.incident,f.knowledge,f.physics);Assert.That(f.incident.ReceiptsFor("CH_03").Any(r=>r.Record.Predicate=="CausedOutcome"),Is.False);Assert.That(f.incident.ReceiptsFor("CH_03").Any(r=>r.Record.Value=="Collapsed"),Is.True);
    }
    [Test] public void ReportedDestinationComesFromActualReporterObservationRatherThanAnotherDiscoverer()
    {
        var f=Fixture();for(int n=0;n<300&&f.incident.Stage!="ResultCommitted";n++)Tick(f.world,f.incident,f.knowledge,f.physics);
        Assert.That(f.incident.Discover(f.world,"CH_03",f.physics),Is.EqualTo("Discovered"));var original=f.incident.Capture().DiscoveryPosition;
        var moved=new Point3(6,0,3);f.world.Resident("CH_02").Position=moved;f.physics.Viewers=new[]{"CH_01"};Tick(f.world,f.incident,f.knowledge,f.physics);
        Assert.That(f.incident.Discover(f.world,"CH_01",f.physics),Is.EqualTo("Discovered"));Assert.That(f.incident.Report(f.world,"CH_01",true),Is.EqualTo("Reported"));
        Assert.That(f.incident.ReportedPosition.Distance(moved),Is.LessThan(.001));Assert.That(f.incident.Capture().DiscoveryPosition.Distance(original),Is.LessThan(.001));
        var restored=MansionIncident.Restore(f.incident.Capture(),new MansionWorld(f.world.Capture(),Nodes,Edges));Assert.That(restored.ReportedPosition.Distance(moved),Is.LessThan(.001));
    }
    [Test] public void IndependentPhysicalIncidentsShareBudgetAndSurviveSaveBeforeOutcomes()
    {
        var f=Fixture();var initial=f.world.Capture();initial.Objects=initial.Objects.Concat(new[]{new MansionObjectState{Id="ITEM_SECOND",Name="두 번째 가상 소품",Position=default}}).ToArray();
        var world=new MansionWorld(initial,Nodes,Edges);var firstState=f.incident.Capture();firstState.Settings.DelayTicks=600;
        var first=MansionIncident.Restore(firstState,world);var second=new MansionIncident();var settings=Settings();
        settings.Id="CASE_TEST_02";settings.ActorId="CH_05";settings.TargetId="CH_03";settings.ObjectId="ITEM_SECOND";settings.DelayTicks=800;
        Assert.That(second.Configure(world,settings),Is.EqualTo("Configured"));f.physics.World=world;f.physics.Viewers=new[]{"CH_03","CH_04","CH_05"};
        foreach(var subject in new[]{"CH_03","ITEM_SECOND"})f.knowledge.Observe("CH_05",new KnownRecord{Kind="Visual",SubjectId=subject,Predicate="AtPlace",Value="ROOM_W",FromTick=0,ToTick=1,IdentityConfirmed=true,Position=default},0);
        for(int n=0;n<300&&second.Stage!="CauseCommitted";n++){world.Step(default,false,f.physics);first.Step(world,f.knowledge.For("CH_04"),f.physics);second.Step(world,f.knowledge.For("CH_05"),f.physics);world.SealCaseOutcomes();}
        Assert.That(first.Stage,Is.EqualTo("CauseCommitted"));Assert.That(second.Stage,Is.EqualTo("CauseCommitted"));Assert.That(world.CaseBook.Used,Is.EqualTo(2));
        world=new MansionWorld(world.Capture(),Nodes,Edges);first=MansionIncident.Restore(first.Capture(),world);second=MansionIncident.Restore(second.Capture(),world);f.physics.World=world;
        for(int n=0;n<1000&&second.Stage!="ResultCommitted";n++){world.Step(default,false,f.physics);second.Step(world,f.knowledge.For("CH_05"),f.physics);first.Step(world,f.knowledge.For("CH_04"),f.physics);world.SealCaseOutcomes();}
        Assert.That(world.Residents.Count(r=>!r.Alive),Is.EqualTo(2));Assert.That(world.CaseBook.AdjudicatedActorId,Is.EqualTo("CH_04"));Assert.That(world.CaseBook.Capacity,Is.EqualTo(2));
        Assert.That(world.Events.Count(e=>e.Type=="IncidentCauseCommitted"),Is.EqualTo(2));Assert.That(world.Events.Count(e=>e.Type=="IncidentResultCommitted"),Is.EqualTo(2));
        var receipts=first.Capture().Receipts.Concat(second.Capture().Receipts).ToArray();Assert.That(receipts.Select(r=>r.Id).Distinct().Count(),Is.EqualTo(receipts.Length));
        Assert.That(first.Read("CH_01").State,Is.EqualTo("Unknown"));Assert.That(second.Read("CH_01").State,Is.EqualTo("Unknown"));
        Assert.DoesNotThrow(()=>new MansionWorld(world.Capture(),Nodes,Edges));
    }
    [Test] public void LegacySaveImportsActualCauseHistoryAndContinuesExactlyOnce()
    {
        var f=Fixture();for(int n=0;n<300&&f.incident.Stage!="CauseCommitted";n++)Tick(f.world,f.incident,f.knowledge,f.physics);
        var old=f.world.Capture();old.Cases=null;var world=new MansionWorld(old,Nodes,Edges);var incident=MansionIncident.Restore(f.incident.Capture(),world);f.physics.World=world;
        Assert.That(world.CaseBook.Find("CASE_TEST_01").CauseTick,Is.EqualTo(incident.Capture().CauseTick));
        for(int n=0;n<30;n++)Tick(world,incident,f.knowledge,f.physics);
        Assert.That(world.Events.Count(e=>e.Type=="IncidentResultCommitted"),Is.EqualTo(1));Assert.That(world.CaseBook.AdjudicatedActorId,Is.EqualTo("CH_04"));
        Assert.DoesNotThrow(()=>MansionIncident.Restore(incident.Capture(),new MansionWorld(world.Capture(),Nodes,Edges)));
    }
    [Test] public void LegacyImportRejectsParticipantChangedFromActualCauseHistory()
    {
        var f=Fixture();for(int n=0;n<300&&f.incident.Stage!="CauseCommitted";n++)Tick(f.world,f.incident,f.knowledge,f.physics);
        var old=f.world.Capture();old.Cases=null;var world=new MansionWorld(old,Nodes,Edges);var altered=f.incident.Capture();altered.Settings.ActorId="CH_05";
        Assert.Throws<ArgumentException>(()=>MansionIncident.Restore(altered,world));
        Assert.That(world.CaseBook.Find("CASE_TEST_01"),Is.Null,"A rejected legacy import must not publish its reconstructed entry.");
    }
    [Test] public void RejectedLateResultDoesNotKillResidentOrAppendHistory()
    {
        var f=Fixture();Assert.That(f.world.ReserveCaseOutcome("CASE_TEST_01"),Is.True);f.world.CommitCaseCause("CASE_TEST_01",1);
        f.world.Step(default,false,f.physics);f.world.SealCaseOutcomes();long sequence=f.world.Capture().Sequence;
        Assert.Throws<InvalidOperationException>(()=>f.world.CommitCaseResult("CASE_TEST_01"));
        Assert.That(f.world.Resident("CH_02").Alive,Is.True);Assert.That(f.world.Capture().Sequence,Is.EqualTo(sequence));
        Assert.That(f.world.CaseBook.Find("CASE_TEST_01").ResultTick,Is.EqualTo(-1));
    }
    [Test] public void SaveRejectsDeadReservedVictimWithoutCommittedResult()
    {
        var f=Fixture();Assert.That(f.world.ReserveCaseOutcome("CASE_TEST_01"),Is.True);f.world.CommitCaseCause("CASE_TEST_01",1);
        var broken=f.world.Capture();broken.Residents.Single(r=>r.Id=="CH_02").Alive=false;
        Assert.Throws<ArgumentException>(()=>new MansionWorld(broken,Nodes,Edges));
    }
    [TestCase(true)] [TestCase(false)] public void UnitySessionCodecRestoresPendingOutcomeWithAndWithoutBookMarker(bool hasBook)
    {
        var f=Fixture();for(int n=0;n<300&&f.incident.Stage!="CauseCommitted";n++)Tick(f.world,f.incident,f.knowledge,f.physics);
        var session=new BASSLINE.Save.MansionSessionSnapshot{World=f.world.Capture(),Incident=f.incident.Capture(),Proceedings=new BASSLINE.Save.MansionProceedings(),OptionalObjects=1|(hasBook?16:0)};
        if(!hasBook)session.World.Cases=null;
        var decode=typeof(BASSLINE.Bootstrap.MansionRuntime).GetMethod("DecodeSession",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var loaded=(BASSLINE.Save.MansionSessionSnapshot)decode.Invoke(null,new object[]{UnityEngine.JsonUtility.ToJson(session)});
        Assert.That(loaded.World.Cases!=null,Is.EqualTo(hasBook));
        var world=new MansionWorld(loaded.World,Nodes,Edges);var incident=MansionIncident.Restore(loaded.Incident,world);f.physics.World=world;
        Assert.That(world.CaseBook.Find("CASE_TEST_01").CauseTick,Is.EqualTo(session.Incident.CauseTick));
        for(int n=0;n<30;n++)Tick(world,incident,f.knowledge,f.physics);
        Assert.That(world.Resident("CH_02").Alive,Is.False);Assert.That(world.Events.Count(e=>e.Type=="IncidentResultCommitted"),Is.EqualTo(1));
        Assert.That(world.CaseBook.AdjudicatedActorId,Is.EqualTo("CH_04"));
    }
}
