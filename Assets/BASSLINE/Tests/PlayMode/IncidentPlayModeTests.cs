using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BASSLINE.Core;
using BASSLINE.Bootstrap;
using BASSLINE.UI;
using BASSLINE.World.Fixture;
using BASSLINE.Save;
public sealed class IncidentPlayModeTests
{
 FixtureRuntime runtime;FixtureHud hud;
 [UnitySetUp] public IEnumerator Load(){yield return SceneManager.LoadSceneAsync("FixtureK_Incident");runtime=UnityEngine.Object.FindFirstObjectByType<FixtureRuntime>();runtime.AutomaticTick=false;hud=UnityEngine.Object.FindFirstObjectByType<FixtureHud>();yield return null;hud.enabled=false;runtime.LoadTestSnapshot(new LifeWorld(runtime.IncidentDefinition.Settings).Capture());TestContext.Progress.WriteLine("checkpoint04 TestOnly Contact subset; Editor="+Application.unityVersion+"; URP17.6.0; UTF1.8.0; seed0; Map="+FixtureDefinition.MapVersion+"; Rule=FixtureK_Contact_001; revision="+File.ReadAllText("Verification/build-revision.txt"));}
 void Until(long tick){while(runtime.World.Tick<tick)runtime.AdvanceOne();}
 string Diagnostic(){var c=runtime.World.ReadIncidentForSystem();return JsonUtility.ToJson(c)+" actor="+JsonUtility.ToJson(runtime.World.Actor("CH_04"))+" B="+JsonUtility.ToJson(runtime.Knowledge.Capture());}
 [UnityTest] public IEnumerator AT_CASE_02_ActualContactDelayedResult_NoRemoteKnowledge_ResumeExactly()
 {
  Until(43500);var c=runtime.World.ReadIncidentForSystem();Assert.That(c.Stage,Is.EqualTo("CauseCommitted"),Diagnostic());Assert.That(c.CauseTick,Is.EqualTo(43500));Assert.That(runtime.World.Actor("CH_02").Incapacitated,Is.False);
  Assert.That(runtime.World.Capture().Events.Any(x=>x.Type=="DoorGranted"&&x.ActorId=="CH_04"&&x.TargetId=="K_DOOR_N"),Is.True);Assert.That(runtime.World.Object("K_O31").OwnerId,Is.EqualTo("CH_04"));
  Until(43650);string path=Path.Combine(Application.temporaryCachePath,"bassline_incident_pending.dat");runtime.SaveTo(path);Until(43800);string expected=Canonical(runtime.CaptureSession());
  runtime.LoadFrom(path);Until(43800);Assert.That(Canonical(runtime.CaptureSession()),Is.EqualTo(expected));c=runtime.World.ReadIncidentForSystem();Assert.That(c.ResultTick,Is.EqualTo(43800));Assert.That(c.ResultTick-c.CauseTick,Is.EqualTo(300));Assert.That(runtime.World.Actor("CH_02").Incapacitated,Is.True);
  Assert.That(runtime.World.Schedule("CH_02","ACT_READ","K_SEAT_W_02"),Is.EqualTo("Unavailable"));Assert.That(runtime.ReadNotebook().Records.Any(x=>x.Predicate=="UsedObject"||x.Predicate=="CausedOutcome"||x.Value=="Collapsed"),Is.False,"Library player must not receive A");
  Assert.That(runtime.Knowledge.For("CH_03").Records().Any(x=>x.Predicate=="CausedOutcome"),Is.True,"continuous human witness");Assert.That(c.Observations.Any(x=>x.Observer=="K_V1"&&x.Predicate=="CausedOutcome"),Is.True,"preinstalled video");Assert.That(runtime.Knowledge.For("CH_18").Records().Any(x=>x.Predicate=="CausedOutcome"),Is.False,"door alone does not identify action");
  Until(49200);Assert.That(runtime.World.Actor("CH_04").Node,Is.EqualTo("K_H"),Diagnostic());Assert.That(runtime.World.Capture().Events.Any(x=>x.Type=="DoorGranted"&&x.ActorId=="CH_04"&&x.TargetId=="K_DOOR_S"),Is.True);Assert.That(runtime.World.Actor("CH_04").KnownBlockedDoors,Does.Not.Contain("K_DOOR_N"));
  foreach(var suffix in new[]{"",".previous",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);yield return null;
 }
 [UnityTest] public IEnumerator AT_CASE_01_PlayerTakesTool_CannotForceOutcomeOrRevealPrevention()
 {
  var s=runtime.World.Capture();var player=s.Actors.Single(x=>x.Id=="CH_01");player.Position=new Point3(25.8,0,-1.15);player.Node="K_W";runtime.LoadTestSnapshot(s);Assert.That(runtime.Interact("K_O31"),Is.EqualTo("Committed"));Until(57601);
  var c=runtime.World.ReadIncidentForSystem();Assert.That(c.Stage,Is.EqualTo("Cancelled"));Assert.That(c.CauseEvent,Is.Empty);Assert.That(c.TracePresent,Is.False);Assert.That(runtime.World.Actor("CH_02").Incapacitated,Is.False);Assert.That(runtime.ReadNotebook().Records.Any(x=>x.Predicate=="CausedOutcome"||x.Value=="MurderPrevented"),Is.False);Assert.That(runtime.World.Object("K_O31").OwnerId,Is.EqualTo("CH_01"));yield return null;
 }
 [UnityTest] public IEnumerator AT_INFO_01_OccludedHumanWitnessDoesNotReceiveVideoKnowledge()
 {
  var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="TestOnlyOccluder";wall.transform.position=new Vector3(27.95f,1.5f,-1.8f);wall.transform.localScale=new Vector3(.1f,3,1.8f);Physics.SyncTransforms();Until(43800);
  Assert.That(runtime.World.ReadIncidentForSystem().Stage,Is.EqualTo("ResultCommitted"),Diagnostic());Assert.That(runtime.Knowledge.For("CH_03").Records().Any(x=>x.Predicate=="UsedObject"||x.Predicate=="CausedOutcome"),Is.False);Assert.That(runtime.World.ReadIncidentForSystem().Observations.Any(x=>x.Observer=="K_V1"&&x.Predicate=="CausedOutcome"),Is.True);UnityEngine.Object.Destroy(wall);yield return null;
 }
 [UnityTest] public IEnumerator AT_CASE_03_ActualReader_EInspection_NestedPause_CopyDedup_TraceScope()
 {
  Until(43800);var snapshot=runtime.CaptureSession();var p=snapshot.World.Actors.Single(x=>x.Id=="CH_01");p.Position=new Point3(29.4,0,-.5);p.Node="K_W";runtime.RestoreSession(snapshot);
  // Test setup placement only; actual examination still uses PhysX range/FOV and its tick cost.
  string path=Path.Combine(Application.temporaryCachePath,"bassline_incident_reader.dat");runtime.SaveTo(path);runtime.LoadFrom(path);LookAt("K_V1");hud.Primary("K_V1");Assert.That(hud.CurrentScreen,Is.EqualTo(14));Until(runtime.World.Tick+60);hud.ToggleNote();long paused=runtime.World.Tick;for(int i=0;i<120;i++)runtime.AdvanceOne();Assert.That(runtime.World.Tick,Is.EqualTo(paused));runtime.SaveTo(path);runtime.LoadFrom(path);hud.SyncPauseView();hud.ToggleNote();Until(runtime.World.Tick+60);
  Assert.That(runtime.ReadInspection().State,Is.EqualTo("Completed"));var records=runtime.ReadNotebook().Records.Where(x=>x.ProvenanceKey=="K_MEDIA_V1_STREAM_01").ToArray();Assert.That(records.Length,Is.EqualTo(2));Assert.That(records.Last().Predicate,Is.EqualTo("CausedOutcome"));hud.Back();LookAt("K_V1");hud.Primary("K_V1");Until(runtime.World.Tick+120);Assert.That(runtime.ReadNotebook().Records.Count(x=>x.ProvenanceKey=="K_MEDIA_V1_STREAM_01"),Is.EqualTo(2));hud.Back();
  snapshot=runtime.CaptureSession();snapshot.World.Actors.Single(x=>x.Id=="CH_01").Position=new Point3(28.3,0,-1.95);runtime.RestoreSession(snapshot);runtime.SaveTo(path);runtime.LoadFrom(path);LookAt("K_P31");hud.Primary("K_P31");Assert.That(runtime.ReadInspection().State,Is.EqualTo("Running"));Until(runtime.World.Tick+120);var trace=runtime.ReadNotebook().Records.Single(x=>x.ProvenanceKey=="K_TRACE_P31_01");Assert.That(trace.SubjectId,Is.EqualTo("K_P31"));Assert.That(trace.IdentityConfirmed,Is.False);Assert.That(trace.DoesNotEstablish.Any(x=>x.Contains("신원")),Is.True);
  hud.Back();var angle=Quaternion.LookRotation(FixtureRuntime.V(runtime.World.Pose("CH_02"))+Vector3.up*.3f-hud.ViewCamera.transform.position).eulerAngles;runtime.SetLook(angle.y,Mathf.DeltaAngle(0,angle.x));hud.Primary("CH_02");Assert.That(runtime.ReadInspection().State,Is.EqualTo("Running"));Until(runtime.World.Tick+120);Assert.That(runtime.ReadNotebook().Records.Any(x=>x.Predicate=="AtPlace"&&x.SubjectId=="CH_02"&&x.Value=="Collapsed"),Is.True);hud.Back();Assert.That(runtime.Talk("CH_03"),Is.EqualTo("Dialogue"));Assert.That(runtime.ReadNotebook().Records.Any(x=>x.Predicate=="CausedOutcome"&&x.Source=="CH_03"&&!x.Direct),Is.True);
  foreach(var suffix in new[]{"",".previous",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);yield return null;
 }
 [UnityTest] public IEnumerator AT_CASE_01_ContactWindowPausesRestoresAndPhysicalBarrierCancelsProgress()
 {
  Until(43485);Assert.That(runtime.World.ReadIncidentForSystem().Stage,Is.EqualTo("Contact"));int progress=runtime.World.ReadIncidentForSystem().ContactProgress;Assert.That(progress,Is.EqualTo(15));runtime.Pause("K_NOTE",true);for(int n=0;n<60;n++)runtime.AdvanceOne();Assert.That(runtime.World.ReadIncidentForSystem().ContactProgress,Is.EqualTo(progress));
  string path=Path.Combine(Application.temporaryCachePath,"bassline_contact_window.dat");runtime.SaveTo(path);runtime.Pause("K_NOTE",false);Until(43500);Assert.That(runtime.World.ReadIncidentForSystem().Stage,Is.EqualTo("CauseCommitted"));runtime.LoadFrom(path);Assert.That(runtime.World.Paused,Is.True);Assert.That(runtime.World.ReadIncidentForSystem().ContactProgress,Is.EqualTo(progress));
  var barrier=GameObject.CreatePrimitive(PrimitiveType.Cube);barrier.transform.position=new Vector3(27.13f,.8f,-1.1f);barrier.transform.localScale=new Vector3(.08f,1.6f,.9f);Physics.SyncTransforms();runtime.Pause("K_NOTE",false);Until(43510);Assert.That(runtime.World.ReadIncidentForSystem().CauseEvent,Is.Empty);Assert.That(runtime.World.ReadIncidentForSystem().ContactProgress,Is.EqualTo(0));Assert.That(runtime.World.ReadIncidentForSystem().TracePresent,Is.False);UnityEngine.Object.Destroy(barrier);
  foreach(var suffix in new[]{"",".previous",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);yield return null;
 }
 void LookAt(string id){var reader=runtime.CaseReaders.Single(x=>x.StableId==id);var angle=Quaternion.LookRotation(reader.transform.position-hud.ViewCamera.transform.position).eulerAngles;runtime.SetLook(angle.y,Mathf.DeltaAngle(0,angle.x));Physics.SyncTransforms();}
 static string Canonical(FixtureSessionSnapshot s){
  // JsonUtility's double parser may differ by one ULP for promoted PhysX floats.
  // Compare positions at 1 micrometre; IDs, ticks, receipts and clock bits remain exact.
  Point3 P(Point3 p)=>new Point3(Math.Round(p.X,6),Math.Round(p.Y,6),Math.Round(p.Z,6));
  foreach(var a in s.World.Actors){a.Position=P(a.Position);a.Leg=a.Leg.Select(P).ToArray();}foreach(var o in s.World.Objects)o.Position=P(o.Position);
  foreach(var r in s.Knowledge.Memories)r.Record.Position=P(r.Record.Position);var c=s.World.Incident;c.CausePosition=P(c.CausePosition);c.ResultPosition=P(c.ResultPosition);c.ContactPoint=P(c.ContactPoint);foreach(var o in c.Observations)o.Position=P(o.Position);
  return JsonUtility.ToJson(s);
 }
}



