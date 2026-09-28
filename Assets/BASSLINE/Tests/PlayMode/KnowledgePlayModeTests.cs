using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BASSLINE.Bootstrap;
using BASSLINE.Core;
using BASSLINE.UI;
using BASSLINE.World.Fixture;
public sealed class KnowledgePlayModeTests
{
 FixtureRuntime runtime;FixtureHud hud;
 [UnitySetUp] public IEnumerator Load(){yield return SceneManager.LoadSceneAsync("FixtureK_Life");runtime=UnityEngine.Object.FindFirstObjectByType<FixtureRuntime>();runtime.AutomaticTick=false;hud=UnityEngine.Object.FindFirstObjectByType<FixtureHud>();yield return null;hud.enabled=false;runtime.LoadTestSnapshot(new LifeWorld().Capture());runtime.World.Autonomous=false;TestContext.Progress.WriteLine("checkpoint03 TestOnly; Editor="+Application.unityVersion+"; URP17.6.0; UTF1.8.0; seed0; "+FixtureDefinition.MapVersion+"; "+FixtureDefinition.RuleVersion+"; revision="+File.ReadAllText("Verification/build-revision.txt"));}
 [UnityTest] public IEnumerator AT_INPUT_01_MouseWithoutButton_EPickup_ModalBlocksLook()
 {
  var defaults=new PlayerControls();Assert.That(defaults.Key("Interact"),Is.EqualTo(KeyCode.E));Assert.That(defaults.Key("Focus"),Is.EqualTo(KeyCode.F));Assert.That(defaults.Rebind("Interact",KeyCode.W),Is.Not.EqualTo("Applied"));
  Assert.That(hud.WantsLockedCursor,Is.True);double before=runtime.ReadPlayer().Yaw;hud.ApplyMouseDelta(3,2);Assert.That(runtime.ReadPlayer().Yaw,Is.EqualTo(before+3*hud.MouseSensitivity).Within(.001));Assert.That(runtime.ReadPlayer().Pitch,Is.EqualTo(-2*hud.MouseSensitivity).Within(.001));
  hud.ToggleNote();before=runtime.ReadPlayer().Yaw;hud.ApplyMouseDelta(9,9);Assert.That(runtime.ReadPlayer().Yaw,Is.EqualTo(before));Assert.That(hud.WantsLockedCursor,Is.False);hud.ToggleNote();
  var s=runtime.World.Capture();s.Actors.Single(a=>a.Id=="CH_01").Position=new Point3(-.5,0,-.8);runtime.LoadTestSnapshot(s);hud.Primary("K_BOOK_01");Assert.That(runtime.World.Object("K_BOOK_01").OwnerId,Is.EqualTo("CH_01"));yield return null;
 }
 [UnityTest] public IEnumerator AT_INFO_01_P4_ClosedDoorCannotCreateFaceMemory()
 {
  var s=runtime.World.Capture();s.Actors.Single(a=>a.Id=="CH_01").Position=new Point3(28,0,4.4);s.Actors.Single(a=>a.Id=="CH_02").Position=new Point3(28,0,1.6);s.PlayerYaw=180;runtime.LoadTestSnapshot(s);
  for(int i=0;i<30;i++)runtime.AdvanceOne();Assert.That(runtime.ReadNotebook().Records.Any(r=>r.SubjectId=="CH_02"),Is.False);
  runtime.Interact("K_DOOR_N");for(int i=0;i<60;i++)runtime.AdvanceOne();var observation=runtime.ReadNotebook().Locations.Single(x=>x.ActorId=="CH_02");Assert.That(observation.PlaceId,Is.EqualTo("K_W"));
  runtime.SetLook(0,0);for(int i=0;i<90;i++)runtime.AdvanceOne();Assert.That(runtime.ReadNotebook().Locations.Single(x=>x.ActorId=="CH_02").Tick,Is.EqualTo(observation.Tick));yield return null;
 }
 [UnityTest] public IEnumerator AT_SAVE_02_P5_InspectionPauseAndSessionRestore()
 {
  var s=runtime.World.Capture();s.Actors.Single(a=>a.Id=="CH_01").Position=new Point3(-.5,0,-.8);s.Actors.Single(a=>a.Id=="CH_02").Position=new Point3(.7,0,-.8);runtime.LoadTestSnapshot(s);
  Assert.That(runtime.Talk("CH_02"),Is.EqualTo("Dialogue"));Assert.That(runtime.Invite("CH_02","K_H",300),Is.EqualTo("Accepted"));var towards=runtime.ObjectBodies.Single(x=>x.ObjectId=="K_BOOK_01").transform.position-hud.ViewCamera.transform.position;var angles=Quaternion.LookRotation(towards).eulerAngles;runtime.SetLook(angles.y,Mathf.DeltaAngle(0,angles.x));Assert.That(runtime.Examine("K_BOOK_01"),Is.EqualTo("Pending"));hud.Open(14);
  for(int i=0;i<60;i++)runtime.AdvanceOne();Assert.That(runtime.ReadInspection().ElapsedTicks,Is.EqualTo(60));hud.ToggleNote();for(int i=0;i<90;i++)runtime.AdvanceOne();Assert.That(runtime.ReadInspection().ElapsedTicks,Is.EqualTo(60));hud.OpenNotePage(12);
  string path=Path.Combine(Application.temporaryCachePath,"bassline_knowledge_save.dat");
  try{runtime.SaveTo(path);string original=JsonUtility.ToJson(runtime.CaptureSession());hud.ToggleNote();for(int i=0;i<10;i++)runtime.AdvanceOne();runtime.LoadFrom(path);Assert.That(JsonUtility.ToJson(runtime.CaptureSession()),Is.EqualTo(original));hud.SyncPauseView();Assert.That(hud.CurrentScreen,Is.EqualTo(12));hud.ToggleNote();Assert.That(hud.CurrentScreen,Is.EqualTo(14));for(int i=0;i<60;i++)runtime.AdvanceOne();Assert.That(runtime.ReadInspection().State,Is.EqualTo("Completed"));var id=runtime.ReadInspection().RecordId;Assert.That(runtime.Knowledge.For("CH_01").Find(id).DoesNotEstablish,Is.Not.Empty);Assert.That(runtime.Knowledge.For("CH_03").Find(id),Is.Null);
   hud.enabled=true;yield return null;hud.enabled=false;Assert.That(hud.CurrentScreen,Is.EqualTo(15));Assert.That(runtime.ReadUiState().SelectedRecordId,Is.EqualTo(id));
   string h=runtime.Hypothesize(id);runtime.SetHypothesisStatus(h,"Held");hud.Back();hud.Open(21);hud.Open(38);runtime.SaveTo(path);string nested=JsonUtility.ToJson(runtime.CaptureSession());hud.ToggleSettings();hud.ToggleNote();runtime.LoadFrom(path);hud.SyncPauseView();Assert.That(hud.CurrentScreen,Is.EqualTo(38));Assert.That(JsonUtility.ToJson(runtime.CaptureSession()),Is.EqualTo(nested));hud.Back();Assert.That(hud.CurrentScreen,Is.EqualTo(21));Assert.That(runtime.World.Paused,Is.True);hud.Back();Assert.That(runtime.World.Paused,Is.False);
  }finally{foreach(var suffix in new[]{"",".tmp",".previous"})if(File.Exists(path+suffix))File.Delete(path+suffix);}yield return null;
 }
 [UnityTest] public IEnumerator AT_UI_01_LongKoreanAtTwoHundredPercentCanScroll()
 {
  hud.Open(41);var view=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(x=>x.ScreenId=="UI_41");view.Fonts(hud.KoreanFont,2);view.Body.text=string.Join("\n\n",Enumerable.Repeat("관측한 위치와 실제 위치를 구분합니다. 직접 확인하지 않은 정보는 표시하지 않습니다. 김민혁의 기록에는 출처와 시간 범위가 함께 보존됩니다.",45));yield return null;Canvas.ForceUpdateCanvases();view.Body.ForceMeshUpdate();Assert.That(view.Body.textInfo.characterCount,Is.GreaterThan(1000));Assert.That(view.BodyScroll.content.rect.height,Is.GreaterThan(view.BodyScroll.viewport.rect.height));Assert.That(hud.ScreenDefinitions.Length,Is.EqualTo(41));Assert.That(hud.ScreenDefinitions.Select(x=>x.ScreenId).Distinct().Count(),Is.EqualTo(41));Assert.That(hud.Open(23),Is.False,"No trial before physical gathering and case authority");
 }
}


