using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BASSLINE.Bootstrap
{
 // Activated only by an explicit automation argument; never alters ordinary play.
 public sealed class IncidentSmokeProbe:MonoBehaviour
 {
  string reportPath;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--bassline-incident-smoke-report");if(i<0||i+1>=args.Length)return;var probe=new GameObject("TestOnly_IncidentSmoke").AddComponent<IncidentSmokeProbe>();probe.reportPath=args[i+1];DontDestroyOnLoad(probe);}
  IEnumerator Start()
  {
   yield return SceneManager.LoadSceneAsync("FixtureK_Incident");yield return null;
   var r=new Receipt{Status="FAIL",Editor=Application.unityVersion,Map=BASSLINE.World.Fixture.FixtureDefinition.MapVersion,Rule="FixtureK_Contact_001",Seed=0};
   string path=Path.Combine(Application.temporaryCachePath,"bassline_incident_player_smoke.dat");
   try{
    var world=UnityEngine.Object.FindAnyObjectByType<FixtureRuntime>();world.AutomaticTick=false;
    while(world.World.Tick<43650)world.AdvanceOne();var pending=world.World.ReadIncidentForSystem();if(pending.Stage!="CauseCommitted")throw new InvalidOperationException("Actual contact did not commit");world.SaveTo(path);
    while(world.World.Tick<43800)world.AdvanceOne();string expected=JsonUtility.ToJson(world.World.ReadIncidentForSystem());world.LoadFrom(path);while(world.World.Tick<43800)world.AdvanceOne();var c=world.World.ReadIncidentForSystem();r.Restored=JsonUtility.ToJson(c)==expected;
    r.CauseTick=c.CauseTick;r.ResultTick=c.ResultTick;r.CauseEvents=world.World.Capture().Events.Count(x=>x.Type=="IncidentCauseCommitted");r.ResultEvents=world.World.Capture().Events.Count(x=>x.Type=="IncidentResultCommitted");r.VictimInactive=world.World.Actor("CH_02").Incapacitated;
    r.HumanWitness=world.Knowledge.For("CH_03").Records().Any(x=>x.Predicate=="CausedOutcome");r.VideoRecorded=c.Observations.Any(x=>x.Observer=="K_V1"&&x.Predicate=="CausedOutcome");r.NoRemoteLeak=!world.ReadNotebook().Records.Any(x=>x.Predicate=="UsedObject"||x.Predicate=="CausedOutcome"||x.Value=="Collapsed");
    if(!r.Restored||!r.VictimInactive||!r.HumanWitness||!r.VideoRecorded||!r.NoRemoteLeak||r.CauseEvents!=1||r.ResultEvents!=1||r.ResultTick-r.CauseTick!=300)throw new InvalidOperationException("Incident execution/knowledge/save gate failed");r.Status="PASS";
   }catch(Exception e){r.Detail=e.ToString();}
   Directory.CreateDirectory(Path.GetDirectoryName(reportPath));File.WriteAllText(reportPath,JsonUtility.ToJson(r,true));foreach(var suffix in new[]{"",".tmp",".previous"})if(File.Exists(path+suffix))File.Delete(path+suffix);Application.Quit(r.Status=="PASS"?0:1);
  }
  [Serializable] sealed class Receipt{public string Status,Editor,Map,Rule,Detail;public long CauseTick,ResultTick;public int CauseEvents,ResultEvents,Seed;public bool Restored,VictimInactive,HumanWitness,VideoRecorded,NoRemoteLeak;}
 }
}
