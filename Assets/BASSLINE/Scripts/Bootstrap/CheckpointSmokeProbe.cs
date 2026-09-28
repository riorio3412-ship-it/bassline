using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
namespace BASSLINE.Bootstrap
{
 public sealed class CheckpointSmokeProbe:MonoBehaviour
 {
  string reportPath;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install()
  {
   var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--bassline-smoke-report");if(index<0||index+1>=args.Length)return;
   var probe=new GameObject("TestOnly_StandaloneSmoke").AddComponent<CheckpointSmokeProbe>();probe.reportPath=args[index+1];DontDestroyOnLoad(probe.gameObject);
  }
  IEnumerator Start()
  {
   yield return null;yield return null;
   var report=new Receipt{Editor=Application.unityVersion,Status="FAIL",Device=SystemInfo.graphicsDeviceName};
   var runtime=UnityEngine.Object.FindAnyObjectByType<FixtureRuntime>();string save=Path.Combine(Application.temporaryCachePath,"bassline_player_smoke.dat");
   try{
    if(!runtime)throw new InvalidOperationException("Fixture runtime missing");runtime.AutomaticTick=false;
    var hud=UnityEngine.Object.FindObjectsByType<MonoBehaviour>().Single(x=>x.GetType().FullName=="BASSLINE.UI.FixtureHud");
    var setup=runtime.World.Capture();setup.Autonomous=false;setup.Actors.Single(x=>x.Id=="CH_01").Position=new BASSLINE.Core.Point3(-.5,0,-.8);setup.Actors.Single(x=>x.Id=="CH_02").Position=new BASSLINE.Core.Point3(.7,0,-.8);runtime.LoadTestSnapshot(setup);
    double yaw=runtime.ReadPlayer().Yaw;hud.GetType().GetMethod("ApplyMouseDelta").Invoke(hud,new object[]{3f,0f});report.MouseLook=runtime.ReadPlayer().Yaw!=yaw;if(!report.MouseLook)throw new InvalidOperationException("Free mouse look failed");
    hud.GetType().GetMethod("Primary").Invoke(hud,new object[]{"K_BOOK_01"});report.Pickup=runtime.World.Object("K_BOOK_01").OwnerId=="CH_01";if(!report.Pickup)throw new InvalidOperationException("Primary pickup failed");runtime.AdvanceOne();runtime.Interact("DROP");runtime.AdvanceOne();
    if(runtime.Talk("CH_02")!="Dialogue"||runtime.Invite("CH_02","K_H",600)!="Accepted")throw new InvalidOperationException("Living dialogue or appointment failed");
    var camera=Camera.main;var delta=runtime.ObjectBodies.Single(x=>x.ObjectId=="K_BOOK_01").transform.position-camera.transform.position;var angles=Quaternion.LookRotation(delta).eulerAngles;runtime.SetLook(angles.y,Mathf.DeltaAngle(0,angles.x));
    if(runtime.Examine("K_BOOK_01")!="Pending")throw new InvalidOperationException("Physical examination unavailable");for(int i=0;i<120;i++)runtime.AdvanceOne();report.Inspection=runtime.ReadInspection().State=="Completed";if(!report.Inspection)throw new InvalidOperationException("Timed inspection failed");runtime.Hypothesize(runtime.ReadInspection().RecordId);
    hud.SendMessage("ToggleNote");hud.GetType().GetMethod("OpenNotePage").Invoke(hud,new object[]{12});report.UiScreens=((Array)hud.GetType().GetField("ScreenDefinitions").GetValue(hud)).Length;
    var sessionBefore=runtime.CaptureSession();runtime.SaveTo(save);var saved=runtime.World.Capture();string expected=JsonUtility.ToJson(saved);hud.SendMessage("ToggleNote");runtime.AdvanceOne();runtime.LoadFrom(save);hud.SendMessage("SyncPauseView");
    var loaded=runtime.World.Capture();
    // JsonUtility in the standalone Mono player can round the last double digit.
    // Permit <= 1 micrometre for physical positions only; every discrete field,
    // event, path, reservation, pause and tick must still match exactly.
    foreach(var actor in loaded.Actors){var original=saved.Actors.Single(a=>a.Id==actor.Id);double error=actor.Position.Distance(original.Position);report.MaxPoseError=Math.Max(report.MaxPoseError,error);if(error>0.000001)throw new InvalidOperationException("Restored physical pose outside tolerance");actor.Position=original.Position;}
    string actual=JsonUtility.ToJson(loaded);if(actual!=expected){File.WriteAllText(reportPath+".expected.json",expected);File.WriteAllText(reportPath+".actual.json",actual);throw new InvalidOperationException("Standalone discrete state restore mismatch");}
    var sessionAfter=runtime.CaptureSession();sessionAfter.World=sessionBefore.World;
    for(int i=0;i<sessionAfter.Knowledge.Memories.Length;i++){var original=sessionBefore.Knowledge.Memories[i].Record;var record=sessionAfter.Knowledge.Memories[i].Record;if(record.Position.Distance(original.Position)>0.000001)throw new InvalidOperationException("Memory position outside tolerance");record.Position=original.Position;}
    report.SessionRestored=JsonUtility.ToJson(sessionBefore)==JsonUtility.ToJson(sessionAfter);report.UiResume=(int)hud.GetType().GetProperty("CurrentScreen").GetValue(hud)==12;report.KnowledgeReceipts=sessionAfter.Knowledge.Memories.Length;
    if(!report.SessionRestored||!report.UiResume||report.UiScreens!=41)throw new InvalidOperationException("Session sections or UI return route mismatch");
    report.Tick=runtime.World.Tick;runtime.Pause("K_SMOKE",true);runtime.AdvanceOne();if(runtime.World.Tick!=report.Tick)throw new InvalidOperationException("Pause advanced world");
    report.Pipeline=GraphicsSettings.defaultRenderPipeline?GraphicsSettings.defaultRenderPipeline.GetType().Name:"Built-in";if(report.Pipeline!="UniversalRenderPipelineAsset")throw new InvalidOperationException("URP inactive in player");
    hud.GetType().GetMethod("OpenNotePage").Invoke(hud,new object[]{7});
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath));ScreenCapture.CaptureScreenshot(Path.ChangeExtension(reportPath,"png"));report.Status="PASS";
   }catch(Exception e){report.Detail=e.ToString();}
   for(int i=0;i<6;i++)yield return null;
   report.NoteCapture=File.Exists(Path.ChangeExtension(reportPath,"png"));
   if(report.Status=="PASS"){
    yield return SceneManager.LoadSceneAsync("Mansion_Main");yield return null;report.MainHallLoaded=Camera.main!=null;if(!report.MainHallLoaded){report.Status="FAIL";report.Detail="Main Hall camera missing";}
   }
   File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));foreach(var suffix in new[]{"",".tmp",".previous"})if(File.Exists(save+suffix))File.Delete(save+suffix);
   Application.Quit(report.Status=="PASS"?0:1);
  }
  [Serializable] sealed class Receipt{public string Status,Editor,Pipeline,Device,Detail;public long Tick;public double MaxPoseError;public int UiScreens,KnowledgeReceipts;public bool NoteCapture,MainHallLoaded,MouseLook,Pickup,Inspection,SessionRestored,UiResume;}
 }
}

