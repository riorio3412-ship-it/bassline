using System;
using System.Linq;
using NUnit.Framework;
using BASSLINE.Core;
using BASSLINE.World.Mansion;
using BASSLINE.Knowledge;

public sealed class MansionRecordingTests
{
    static readonly RecordingCoverage[] Cameras={new RecordingCoverage{Id="CAMERA_WORK",PlaceId="ROOM_WORK"}};
    static readonly MansionNode[] Nodes={new MansionNode{Id="NODE_WORK",Room="ROOM_WORK",Position=default}};
    sealed class Physics:IMansionPhysics,IMansionRecordingPhysics
    {
        public bool Visible=true,Identity=true;
        public string Hidden="";
        public Point3 Move(string actor,Point3 delta)=>default;
        public bool DoorClear(string id)=>true;
        public void DoorPose(string id,double open){}
        public bool Records(string source,string subject)=>Visible&&subject!=Hidden&&(subject=="CH_02"||subject=="CH_03"||subject=="PROP");
        public bool IdentifiesOnRecording(string source,string actor)=>Identity&&Records(source,actor);
    }
    static MansionWorld World()=>new MansionWorld(new MansionState{Residents=Enumerable.Range(1,18).Select(n=>new ResidentState{Id="CH_"+n.ToString("00"),Node="NODE_WORK",Position=default}).ToArray(),Objects=new[]{new MansionObjectState{Id="PROP",Name="물건"}}},Nodes,Array.Empty<MansionEdge>());
    // Synthetic observed event timings test ONLY the camera adapter. No production murder is scheduled here.
    static MansionIncidentSnapshot[] Case(long tick)=>new[]{new MansionIncidentSnapshot{Settings=new MansionCaseSettings{Id="CASE_CAMERA_TEST",ActorId="CH_02",TargetId="CH_03",ObjectId="PROP"},CauseTick=tick>=2?2:-1,DueTick=5,ResultTick=tick>=5?5:-1}};
    static void Tick(MansionWorld w,MansionRecording r,Physics p){w.Step(default,false,p);if(w.Tick==5)w.Resident("CH_03").Alive=false;r.Step(w,Case(w.Tick),p);}
    [Test]public void ContinuousFootageBecomesEvidenceOnlyAfterPhysicalReading()
    {
        var w=World();var p=new Physics();var r=new MansionRecording(Cameras,1,0);var knowledge=new KnowledgeLedger(w.Residents.Select(a=>a.Id),"LOOP_01");
        for(int i=0;i<5;i++)Tick(w,r,p);
        Assert.That(knowledge.For("CH_01").Records(),Is.Empty);
        var clips=r.Read("CAMERA_WORK");Assert.That(clips.Count(c=>c.Predicate=="CausedOutcome"),Is.EqualTo(1));
        Assert.That(clips.Where(c=>c.Predicate=="CausedOutcome").Single().FromTick,Is.EqualTo(2));
        foreach(var clip in clips)knowledge.Observe("CH_01",clip,w.Tick);
        Assert.That(knowledge.For("CH_04").Records(),Is.Empty);
        Assert.That(knowledge.For("CH_01").Records().Select(c=>c.ProvenanceKey).Distinct().Count(),Is.EqualTo(1),"Several frames remain one physical source.");
        clips[0].Text="modified copy";Assert.That(r.Read("CAMERA_WORK")[0].Text,Is.Not.EqualTo("modified copy"));
    }
    [Test]public void OneOccludedTickOrMissingIdentityCannotBeRepairedByLaterSight()
    {
        foreach(bool loseIdentity in new[]{false,true}){
            var w=World();var p=new Physics();var r=new MansionRecording(Cameras,1,0);Tick(w,r,p);Tick(w,r,p);
            if(loseIdentity)p.Identity=false;else p.Hidden="CH_03";Tick(w,r,p);p.Identity=true;p.Hidden="";Tick(w,r,p);Tick(w,r,p);
            Assert.That(r.Read("CAMERA_WORK").Any(c=>c.Predicate=="UsedObject"),Is.True);
            Assert.That(r.Read("CAMERA_WORK").Any(c=>c.Predicate=="CausedOutcome"),Is.False);
            Assert.That(r.Read("CAMERA_WORK").Any(c=>c.Value=="Collapsed"),Is.True);
        }
    }
    [Test]public void InstallingAfterCauseNeverBackfillsAndUnidentifiedPeopleRemainUnknown()
    {
        var w=World();var p=new Physics{Identity=false};for(int i=0;i<3;i++)w.Step(default,false,p);
        var r=new MansionRecording(Cameras,1,w.Tick);r.Step(w,Case(w.Tick),p);Tick(w,r,p);Tick(w,r,p);
        Assert.That(r.Read("CAMERA_WORK").All(c=>c.SubjectId=="UNKNOWN_ACTOR"&&!c.IdentityConfirmed),Is.True);
        Assert.That(r.Read("CAMERA_WORK").Any(c=>c.Predicate=="UsedObject"||c.Predicate=="CausedOutcome"),Is.False);
        Assert.That(r.Read("CAMERA_WORK").All(c=>c.FromTick>=3),Is.True);
    }
    [Test]public void SaveDuringContinuousRecordingResumesOnceAndPausesDoNotInventFrames()
    {
        var w=World();var p=new Physics();var r=new MansionRecording(Cameras,1,0);Tick(w,r,p);Tick(w,r,p);
        w.Pause("NOTE",true);for(int n=0;n<20;n++)Tick(w,r,p);Assert.That(w.Tick,Is.EqualTo(2));
        Assert.That(r.Capture().Chains.Length,Is.EqualTo(1));
        r=MansionRecording.Restore(r.Capture(),Cameras,w,Case(w.Tick));w.Pause("NOTE",false);
        Tick(w,r,p);Tick(w,r,p);Tick(w,r,p);long sequence=r.Capture().Sequence;r.Step(w,Case(w.Tick),p);
        Assert.That(r.Capture().Sequence,Is.EqualTo(sequence));Assert.That(r.Read("CAMERA_WORK").Count(c=>c.Predicate=="CausedOutcome"),Is.EqualTo(1));
        Assert.DoesNotThrow(()=>MansionRecording.Restore(r.Capture(),Cameras,w,Case(w.Tick)));
    }
    [Test]public void UnrecordedSimulationGapBreaksContinuityAndFutureOrWrongSourceClipsAreRejected()
    {
        var w=World();var p=new Physics();var r=new MansionRecording(Cameras,1,0);Tick(w,r,p);Tick(w,r,p);
        w.Step(default,false,p);Tick(w,r,p);Tick(w,r,p);Assert.That(r.Read("CAMERA_WORK").Any(c=>c.Predicate=="CausedOutcome"),Is.False);
        var invalid=r.Capture();invalid.Clips[0].ToTick=w.Tick+20;Assert.Throws<ArgumentException>(()=>MansionRecording.Restore(invalid,Cameras,w,Case(w.Tick)));
        invalid=r.Capture();invalid.Clips[0].Source="CAMERA_REMOTE";Assert.Throws<ArgumentException>(()=>MansionRecording.Restore(invalid,Cameras,w,Case(w.Tick)));
        invalid=r.Capture();invalid.Loop=2;Assert.Throws<ArgumentException>(()=>MansionRecording.Restore(invalid,Cameras,w,Case(w.Tick)));
    }
    [Test]public void RecordedPossessionUsesTheReasoningContractWithoutBecomingProofOfUse()
    {
        var w=World();var p=new Physics();var r=new MansionRecording(Cameras,1,0);w.Pickup("PROP","CH_02");Tick(w,r,p);
        var k=new KnowledgeLedger(w.Residents.Select(a=>a.Id),"LOOP_01");var held=r.Read("CAMERA_WORK").Single(c=>c.Predicate=="HeldObject");string id=k.Observe("CH_01",held,w.Tick);
        var claim=new BASSLINE.Investigation.ClaimRecord{Id="CLAIM_USE",OwnerId="CH_01",LoopId="LOOP_01",Spans=new[]{new BASSLINE.Investigation.ClaimSpan{Id="SPAN_USE",SubjectId="CH_02",Predicate="UsedObject",Value="PROP",PlaceId="ROOM_WORK",FromTick=1,ToTick=2}}};
        var answer=new BASSLINE.Investigation.LogicResolver().Resolve(k.For("CH_01"),"QUERY_USE","LR04",claim,"SPAN_USE",new[]{id});Assert.That(answer.ReasonCode,Is.EqualTo("MissingUse"));
        Assert.That(MansionRecording.SameClip(k.For("CH_01").Find(id),held),Is.True);
    }
}
