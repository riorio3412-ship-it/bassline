using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.World.Fixture;
using BASSLINE.Knowledge;
using BASSLINE.Investigation;
public sealed class IncidentDomainTests
{
 [SetUp] public void Metadata()=>TestContext.Progress.WriteLine("checkpoint04; Editor="+Application.unityVersion+"; URP17.6.0; UTF1.8.0; seed0; Map=FixtureK_Life_004; Rule=FixtureK_Contact_001; revision="+File.ReadAllText("Verification/build-revision.txt"));
 [Test] public void AT_INFO_03_ReacquiredRecordingAndForwardedCopyShareOneSource()
 {
  var k=new KnowledgeLedger(FixtureDefinition.Actors);
  KnownRecord Record(string source)=>new KnownRecord{ProvenanceKey=source,Kind="Video",SubjectId="CH_04",Predicate="UsedObject",Value="K_O31",FromTick=100,ToTick=101,IdentityConfirmed=true};
  string a=k.Observe("CH_01",Record("K_V1_STREAM"),101),b=k.Observe("CH_03",Record("K_V1_STREAM"),101);b=k.Deliver("CH_03","CH_01",b,102);
  var claim=new ClaimRecord{Id="K_CLAIM",OwnerId="CH_01",LoopId=k.LoopId,Spans=new[]{new ClaimSpan{Id="K_SPAN",SubjectId="CH_04",Predicate="UsedObject",Value="K_O31",FromTick=100,ToTick=101}}};
  var resolver=new LogicResolver();Assert.That(resolver.Resolve(k.For("CH_01"),"K_REQ","LR09",claim,"K_SPAN",new[]{a,b}).ReasonCode,Is.EqualTo("SameRoot"));
  string witness=k.Observe("CH_03",Record("K_WITNESS_CH_03"),101);witness=k.Deliver("CH_03","CH_01",witness,102);Assert.That(resolver.Resolve(k.For("CH_01"),"K_REQ2","LR09",claim,"K_SPAN",new[]{a,witness}).ReasonCode,Is.EqualTo("IndependentRoots"));
  var saved=k.Capture();saved.Memories.Last().Record.ProvenanceKey="K_FORGED_ROOT";Assert.Throws<ArgumentException>(()=>KnowledgeLedger.Restore(saved,FixtureDefinition.Actors,102));
 }
 [Test] public void AT_SAVE_04_RejectsFabricatedResultAndWitnessWithoutCause()
 {
  var w=new LifeWorld(new IncidentSettings());var s=w.Capture();s.Incident.Stage="ResultCommitted";Assert.Throws<ArgumentException>(()=>LifeWorld.Restore(s));
  s=w.Capture();s.Actors.Single(x=>x.Id=="CH_02").Incapacitated=true;Assert.Throws<ArgumentException>(()=>LifeWorld.Restore(s));
  s=w.Capture();s.Incident.Witnesses=new[]{new IncidentWitness{Observer="CH_99"}};Assert.Throws<ArgumentException>(()=>LifeWorld.Restore(s));
  s=w.Capture();s.Objects.Single(x=>x.Id=="K_O31").Id="K_UNKNOWN";Assert.Throws<ArgumentException>(()=>LifeWorld.Restore(s));
 }
 [Test] public void AT_SAVE_02_IncidentSnapshotsDeepCopyAndOldLifeSnapshotLoads()
 {
  var w=new LifeWorld(new IncidentSettings());var s=w.Capture();s.Incident.Settings.Target="CH_01";Assert.That(w.ReadIncidentForSystem().Settings.Target,Is.EqualTo("CH_02"));
  s=new LifeWorld().Capture();s.Incident=null;foreach(var a in s.Actors)a.RouteAvoidDoors=null;Assert.That(LifeWorld.Restore(s).IsIncidentFixture,Is.False);Assert.That(LifeWorld.Restore(s).Capture().Actors.All(x=>x.RouteAvoidDoors!=null),Is.True);
 }
 [Test] public void AT_CASE_01_AppointmentDelaysIntentWithoutSecretRouteKnowledge()
 {
  var settings=new IncidentSettings{StartTick=39600};var w=new LifeWorld(settings);w.RequestIncidentApproach(true);Assert.That(w.ReadIncidentForSystem().Stage,Is.EqualTo("Planned"));w.RequestIncidentApproach(false);Assert.That(w.ReadIncidentForSystem().Stage,Is.EqualTo("Approaching"));Assert.That(w.Actor("CH_04").KnownBlockedDoors,Is.Empty);
 }
}
