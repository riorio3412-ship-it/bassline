using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BASSLINE.Core;
using BASSLINE.World.Fixture;
using BASSLINE.Bootstrap;
using BASSLINE.UI;
public sealed class FixturePlayModeTests
{
 FixtureRuntime runtime;FixtureHud hud;
 [UnitySetUp] public IEnumerator Load()
 {
  yield return SceneManager.LoadSceneAsync("FixtureK_Life");runtime=UnityEngine.Object.FindFirstObjectByType<FixtureRuntime>();runtime.AutomaticTick=false;
  hud=UnityEngine.Object.FindFirstObjectByType<FixtureHud>();yield return null;hud.enabled=false;runtime.LoadTestSnapshot(new LifeWorld().Capture());runtime.World.Autonomous=false;
  TestContext.Progress.WriteLine("TestOnly; Editor="+Application.unityVersion+"; URP=17.6.0; uGUI=2.6.0; UTF=1.8.0; seed=0; Map="+FixtureDefinition.MapVersion+"; Rules="+FixtureDefinition.RuleVersion+"; revision="+File.ReadAllText("Verification/build-revision.txt")+"; device="+SystemInfo.graphicsDeviceName+"; tick rate=60; speed=1.4m/s");
 }
 [UnityTest] public IEnumerator AT_NAV_02_LockedNorthDoorPhysicallyReroutesSouth()
 {
  var s=runtime.World.Capture();s.Doors.Single(d=>d.Id=="K_DOOR_N").Locked=true;runtime.LoadTestSnapshot(s);
  Assert.That(runtime.World.Schedule("CH_02","ACT_WORK","K_SEAT_W_01"),Is.EqualTo("Accepted"));Point3 before=runtime.World.Pose("CH_02");double distance=0;
  for(int tick=0;tick<16000&&runtime.World.Actor("CH_02").CompletedActivities==0;tick++){
   runtime.AdvanceOne();var after=runtime.World.Pose("CH_02");double step=before.Distance(after);Assert.That(step,Is.LessThan(.06),"teleport/speed discontinuity");distance+=step;before=after;if(tick%240==0)yield return null;
  }
  var result=runtime.World.Capture();File.WriteAllText("Verification/fixture-locked-route.json",JsonUtility.ToJson(result,true));
  Assert.That(runtime.World.Actor("CH_02").CompletedActivities,Is.EqualTo(1),"Final phase="+runtime.World.Actor("CH_02").Phase+" pose="+JsonUtility.ToJson(runtime.World.Pose("CH_02")));
  Assert.That(result.Events.Any(e=>e.ActorId=="CH_02"&&e.Type=="DoorObservedLocked"&&e.TargetId=="K_DOOR_N"),Is.True);
  Assert.That(result.Events.Any(e=>e.ActorId=="CH_02"&&e.Type=="DoorGranted"&&e.TargetId=="K_DOOR_S"),Is.True);
  Assert.That(result.Events.Any(e=>e.ActorId=="CH_02"&&e.Type=="DoorGranted"&&e.TargetId=="K_DOOR_N"),Is.False);
  Assert.That(distance,Is.GreaterThan(140),"Should physically backtrack and take the south route");TestContext.Progress.WriteLine("Travel distance="+distance+"m; arrival and activity complete tick="+result.Tick);
 }
 [UnityTest] public IEnumerator AT_NAV_01_P1_FiveAutonomousNpcsCompletePhysicalActivities()
 {
  runtime.World.Autonomous=true;double maxStep=0;int collisions=0;
  for(int tick=0;tick<14400;tick++){
   var before=FixtureDefinition.Actors.Select(runtime.World.Pose).ToArray();runtime.AdvanceOne();var after=FixtureDefinition.Actors.Select(runtime.World.Pose).ToArray();
   for(int i=0;i<after.Length;i++){maxStep=Math.Max(maxStep,before[i].Distance(after[i]));for(int j=i+1;j<after.Length;j++)if(after[i].Distance(after[j])<.5)collisions++;}
   if(tick%240==0)yield return null;
   if(FixtureDefinition.Actors.Where(id=>id!="CH_01").All(id=>runtime.World.Actor(id).CompletedActivities>0))break;
  }
  var snapshot=runtime.World.Capture();File.WriteAllText("Verification/fixture-autonomous.json",JsonUtility.ToJson(snapshot,true));
  foreach(var id in FixtureDefinition.Actors.Where(id=>id!="CH_01"))Assert.That(runtime.World.Actor(id).CompletedActivities,Is.GreaterThan(0),id+" phase="+runtime.World.Actor(id).Phase+" pose="+JsonUtility.ToJson(runtime.World.Pose(id)));
  Assert.That(maxStep,Is.LessThan(.06));Assert.That(collisions,Is.Zero,"Capsule penetration > skin tolerance");
  Assert.That(snapshot.Seats.Select(s=>s.AnchorId).Distinct().Count(),Is.EqualTo(snapshot.Seats.Length));TestContext.Progress.WriteLine("Five NPCs completed first activities by tick="+snapshot.Tick+"; max displacement="+maxStep+"; penetration samples="+collisions+". This is not the full six-at-one-door 20s acceptance scenario.");
 }
 [UnityTest] public IEnumerator AT_SAVE_02_P2_TransferMidpointPhysicalRestore()
 {
  var s=runtime.World.Capture();s.Actors.Single(a=>a.Id=="CH_01").Position=new Point3(-.5,0,-.8);s.Actors.Single(a=>a.Id=="CH_02").Position=new Point3(.7,0,-.8);runtime.LoadTestSnapshot(s);
  Assert.That(runtime.World.Pickup("CMD_PICK","CH_01","K_BOOK_01",runtime),Is.EqualTo("Committed"));
  Assert.That(runtime.World.BeginTransfer("CMD_GIVE","CH_01","CH_02",runtime),Is.EqualTo("Pending"));for(int i=0;i<30;i++)runtime.AdvanceOne();
  string path=Path.Combine(Application.temporaryCachePath,"bassline_fixture_transfer.dat");
  try{runtime.SetLook(45,-20);runtime.SaveTo(path);string before=JsonUtility.ToJson(runtime.World.Capture());runtime.AdvanceOne();runtime.SetLook(0,0);runtime.LoadFrom(path);Assert.That(JsonUtility.ToJson(runtime.World.Capture()),Is.EqualTo(before));Assert.That(runtime.ReadPlayer().Yaw,Is.EqualTo(45));Assert.That(runtime.ReadPlayer().Pitch,Is.EqualTo(-20));for(int i=0;i<30;i++)runtime.AdvanceOne();
   Assert.That(runtime.World.Object("K_BOOK_01").OwnerId,Is.EqualTo("CH_02"));Assert.That(runtime.World.Capture().Events.Count(e=>e.Type=="ObjectTransferred"),Is.EqualTo(1));
   var item=runtime.ObjectBodies.Single(b=>b.ObjectId=="K_BOOK_01");Assert.That(item.Collider.enabled,Is.False);Assert.That(Vector3.Distance(item.transform.position,runtime.Bodies.Single(b=>b.ActorId=="CH_02").RightHand.position),Is.LessThan(.001f));
  }finally{foreach(string suffix in new[]{"",".tmp",".previous"})if(File.Exists(path+suffix))File.Delete(path+suffix);}yield return null;
 }
 [UnityTest] public IEnumerator AT_TIME_02_UI_01_NestedNoteAndKoreanGlyphs()
 {
  hud.ToggleNote();hud.ToggleSettings();long frozen=runtime.World.Tick;for(int i=0;i<120;i++)runtime.AdvanceOne();Assert.That(runtime.World.Tick,Is.EqualTo(frozen));
  var snapshot=runtime.World.Capture();hud.ToggleNote();Assert.That(runtime.World.Paused,Is.True);hud.ToggleSettings();Assert.That(runtime.World.Paused,Is.False);runtime.LoadTestSnapshot(snapshot);hud.SyncPauseView();Assert.That(hud.NoteOpen,Is.True);
  string text="김민혁 김진우 차도윤 유스티 세계 시간은 읽는 동안 멈춥니다. 저장 불러오기 관계 생활 물건 전달 NOTE F5 14:50";
  Assert.That(hud.KoreanFont.HasCharacters(text,out uint[] missing,false,true),Is.True,"Missing glyphs: "+string.Join(",",missing??Array.Empty<uint>()));
  hud.NoteText.text=string.Join("\n",Enumerable.Repeat(text,10));hud.NoteText.ForceMeshUpdate(true);Assert.That(hud.NoteText.textInfo.characterCount,Is.GreaterThan(500));
  hud.ToggleNote();Assert.That(runtime.World.Paused,Is.True);hud.ToggleSettings();runtime.AdvanceOne();Assert.That(runtime.World.Tick,Is.GreaterThan(frozen));yield return null;
 }
 [UnityTest] public IEnumerator AT_SAVE_04_LoadInsideWallRejectedWithoutChangingWorld()
 {
  string before=JsonUtility.ToJson(runtime.World.Capture());var bad=runtime.World.Capture();bad.Actors[0].Position=new Point3(2.98,0,0);
  Assert.Throws<InvalidDataException>(()=>runtime.LoadTestSnapshot(bad));Assert.That(JsonUtility.ToJson(runtime.World.Capture()),Is.EqualTo(before));yield return null;
 }
 [UnityTest] public IEnumerator AT_NAV_01_SixOpposingDoorRequestsWithinTwentySeconds()
 {
  var baseline=runtime.World.Capture();
  foreach(int batch in new[]{30,120,240}){
  runtime.LoadTestSnapshot(baseline);
  var setup=runtime.World.Capture();for(int i=0;i<setup.Actors.Length;i++){var a=setup.Actors[i];a.Node=i<3?"K_N":"K_W";a.Position=new Point3(28,0,i<3?4.2+(i*.75):1.8-((i-3)*.75));}runtime.LoadTestSnapshot(setup);
  for(int i=0;i<setup.Actors.Length;i++)Assert.That(runtime.World.Schedule(setup.Actors[i].Id,"ACT_PASSAGE",(i<3?"K_PASS_W_":"K_PASS_N_")+"0"+(i%3+1)),Is.EqualTo("Accepted"));
  int overlapSamples=0;for(int tick=0;tick<1200;tick++){
   runtime.AdvanceOne();var poses=setup.Actors.Select(a=>runtime.World.Pose(a.Id)).ToArray();for(int i=0;i<poses.Length;i++)for(int j=i+1;j<poses.Length;j++)if(poses[i].Distance(poses[j])<.5)overlapSamples++;
   var d=runtime.World.Door("K_DOOR_N");if(!runtime.DoorClear(d.Id)&&d.OpenFraction>.01)Assert.That(d.OpenFraction,Is.EqualTo(1).Within(.00001),"Door closes during crossing");
   if(tick%batch==0)yield return null;
  }
  var state=runtime.World.Capture();File.WriteAllText("Verification/fixture-six-door.json",JsonUtility.ToJson(state,true));
  File.WriteAllText("Verification/fixture-six-door-batch-"+batch+".json",JsonUtility.ToJson(state,true));
  File.WriteAllLines("Verification/fixture-door-query.txt",Physics.OverlapBox(FixtureRuntime.V(LifeWorld.DoorPosition("K_DOOR_N"))+Vector3.up,new Vector3(.82f,1,.25f)).Select(c=>c.name+" | "+c.transform.position+" | "+c.bounds+" | actor="+c.GetComponentInParent<BASSLINE.AuthoringData.FixtureActorBody>()));
  var queued=state.Events.Where(e=>e.Type=="DoorQueued"&&e.TargetId=="K_DOOR_N").Select(e=>e.ActorId).ToArray();var granted=state.Events.Where(e=>e.Type=="DoorGranted"&&e.TargetId=="K_DOOR_N").Select(e=>e.ActorId).ToArray();
  Assert.That(queued.Length,Is.EqualTo(6));Assert.That(granted,Is.EqualTo(queued),"FIFO");
  var releases=state.Events.Where(e=>e.Type=="DoorReleased"&&e.TargetId=="K_DOOR_N").ToArray();Assert.That(releases.Length,Is.EqualTo(6));Assert.That(releases.Last().Tick,Is.LessThanOrEqualTo(1200));
  foreach(var a in state.Actors){var initial=setup.Actors.Single(x=>x.Id==a.Id);Assert.That((a.Position.Z-3)*(initial.Position.Z-3),Is.LessThan(0),a.Id+" has not crossed");}
  Assert.That(state.Doors.Single(d=>d.Id=="K_DOOR_N").Queue,Is.Empty);Assert.That(overlapSamples,Is.Zero);
  TestContext.Progress.WriteLine("Six actual crossings; FIFO and no penetration/closing; last release tick="+releases.Last().Tick+" ("+(releases.Last().Tick/60d)+"s). Destination activity completion is a separate criterion.");
  // The 20s requirement is passage, not walking all the way to the parking area.
  // Egress markers are away from the portal so completed actors do not park in its queue.
  for(int i=0;i<600;i++)runtime.AdvanceOne();foreach(var a in runtime.World.Capture().Actors)Assert.That(a.CompletedActivities,Is.EqualTo(1),a.Id+" destination not completed after crossing");
  }
 }
 [UnityTest] public IEnumerator AT_INFO_01_DoorColliderBlocksSightAndIllegalLoadedPose()
 {
  var s=runtime.World.Capture();s.Actors.Single(a=>a.Id=="CH_01").Position=new Point3(28,0,4.4);s.Actors.Single(a=>a.Id=="CH_02").Position=new Point3(28,0,1.6);runtime.LoadTestSnapshot(s);
  var target=runtime.World.Pose("CH_02").Plus(new Point3(0,1,0));Assert.That(runtime.ClearSight("CH_01",target),Is.False);
  var bad=runtime.World.Capture();bad.Actors.Single(a=>a.Id=="CH_01").Position=new Point3(28,0,3);Assert.Throws<InvalidDataException>(()=>runtime.LoadTestSnapshot(bad));
  Assert.That(runtime.World.OpenDoor("CMD_OPEN","CH_01","K_DOOR_N",runtime),Is.EqualTo("Accepted"));for(int i=0;i<16;i++)runtime.AdvanceOne();Assert.That(runtime.ClearSight("CH_01",target),Is.True);yield return null;
 }
 [UnityTest] public IEnumerator AT_NAV_03_VisibleAndHiddenActorsUseSamePhysicsAtDifferentFrameBatchSizes()
 {
  var initial=runtime.World.Capture();string expected=null;long expectedTick=0;
  for(int pass=0;pass<2;pass++){
   runtime.LoadTestSnapshot(initial);foreach(var body in runtime.Bodies)foreach(var renderer in body.GetComponentsInChildren<Renderer>())renderer.enabled=pass==0;
   runtime.World.Schedule("CH_02","ACT_WORK","K_SEAT_W_01");int tick=0;
   for(;tick<10000&&runtime.World.Actor("CH_02").CompletedActivities==0;tick++){runtime.AdvanceOne();if(tick%(pass==0?30:240)==0)yield return null;}
   Assert.That(runtime.World.Actor("CH_02").CompletedActivities,Is.EqualTo(1));string events=string.Join("|",runtime.World.Capture().Events.Select(e=>e.Tick+":"+e.Type+":"+e.ActorId+":"+e.TargetId));
   if(pass==0){expected=events;expectedTick=runtime.World.Tick;}else{Assert.That(events,Is.EqualTo(expected));Assert.That(runtime.World.Tick,Is.EqualTo(expectedTick));}
  }
 }
 [UnityTest] public IEnumerator AT_SAVE_02_MovingSnapshotRestoresDiscreteStateAndMicrometrePose()
 {
  runtime.World.Schedule("CH_02","ACT_WORK","K_SEAT_W_01");for(int i=0;i<300;i++)runtime.AdvanceOne();var expected=runtime.World.Capture();string path=Path.Combine(Application.temporaryCachePath,"bassline_moving_save.dat");
  try{runtime.SaveTo(path);for(int i=0;i<60;i++)runtime.AdvanceOne();runtime.LoadFrom(path);var actual=runtime.World.Capture();
   foreach(var actor in actual.Actors){var original=expected.Actors.Single(x=>x.Id==actor.Id);Assert.That(actor.Position.Distance(original.Position),Is.LessThanOrEqualTo(.000001));actor.Position=original.Position;}
   Assert.That(JsonUtility.ToJson(actual),Is.EqualTo(JsonUtility.ToJson(expected)));runtime.AdvanceOne();Assert.That(runtime.World.Tick,Is.EqualTo(expected.Tick+1));
  }finally{foreach(var suffix in new[]{"",".tmp",".previous"})if(File.Exists(path+suffix))File.Delete(path+suffix);}yield return null;
 }
}

