using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using BASSLINE.Core;
using BASSLINE.World.Fixture;
using BASSLINE.Save;
using BASSLINE.Authoring;

public sealed class FixtureDomainTests
{
 sealed class NoObstacles:IFixturePhysics
 {
  public Dictionary<string,Point3> Positions;public NoObstacles(LifeWorld w){Positions=w.Capture().Actors.ToDictionary(a=>a.Id,a=>a.Position);}
  public Point3 Move(string id,Point3 delta)=>Positions[id]=Positions[id].Plus(delta);
  public bool ClearSight(string id,Point3 target)=>true;public bool DoorClear(string id)=>true;public void SetDoor(string id,double fraction){}
 }
 [SetUp] public void Metadata(){TestContext.Progress.WriteLine("TestOnly; Editor="+Application.unityVersion+"; URP=17.6.0; UTF=1.8.0; seed=0; Map="+FixtureDefinition.MapVersion+"; Rules="+FixtureDefinition.RuleVersion+"; revision="+File.ReadAllText("Verification/build-revision.txt")+"; pure domain tests do not establish physical traversal");}
 [Test] public void AT_NAV_02_SourceDistancesAndKnownDoorReroute()
 {
  Assert.That(FixtureDefinition.Edges.Single(x=>x.Id=="K_ROUTE_N").Length,Is.EqualTo(42));
  Assert.That(FixtureDefinition.Edges.Single(x=>x.Id=="K_ROUTE_S").Length,Is.EqualTo(56));
  Assert.That(FixtureDefinition.Route("K_H","K_W",Array.Empty<string>()).Select(x=>x.Id),Does.Contain("K_N_ENTRY"));
  Assert.That(FixtureDefinition.Route("K_H","K_W",new[]{"K_DOOR_N"}).Select(x=>x.Id),Does.Contain("K_S_ENTRY"));
  Assert.That(FixtureDefinition.Route("K_H","K_W",new[]{"K_DOOR_N","K_DOOR_S"}),Is.Null);
 }
 [Test] public void AT_SAVE_02_TransferMidpointRestoresExactEventsAndSingleOwnership()
 {
  var s=new LifeWorld().Capture();s.Autonomous=false;s.Actors.Single(a=>a.Id=="CH_01").Position=new Point3(-.5,0,-.8);s.Actors.Single(a=>a.Id=="CH_02").Position=new Point3(.7,0,-.8);
  var w=LifeWorld.Restore(s);var p=new NoObstacles(w);
  Assert.That(w.Pickup("CMD_PICK","CH_01","K_BOOK_01",p),Is.EqualTo("Committed"));
  Assert.That(w.BeginTransfer("CMD_GIVE","CH_01","CH_02",p),Is.EqualTo("Pending"));
  for(int i=0;i<30;i++)w.Step(p);string encoded=JsonUtility.ToJson(w.Capture());var resumed=LifeWorld.Restore(JsonUtility.FromJson<LifeSnapshot>(encoded));var p2=new NoObstacles(resumed);
  for(int i=0;i<30;i++){w.Step(p);resumed.Step(p2);}Assert.That(JsonUtility.ToJson(resumed.Capture()),Is.EqualTo(JsonUtility.ToJson(w.Capture())));
  Assert.That(resumed.Object("K_BOOK_01").OwnerId,Is.EqualTo("CH_02"));Assert.That(resumed.BeginTransfer("CMD_GIVE","CH_01","CH_02",p2),Is.EqualTo("Committed"));
  Assert.That(resumed.Capture().Events.Count(e=>e.Type=="ObjectTransferred"),Is.EqualTo(1));
 }
 [Test] public void AT_SAVE_04_InvalidReferencesAndDuplicateOwnershipRejected()
 {
  var w=new LifeWorld();var s=w.Capture();s.Objects[0].Location="Hand";s.Objects[0].OwnerId="CH_99";s.Objects[0].AnchorId="";Assert.Throws<ArgumentException>(()=>LifeWorld.Restore(s));
  s=w.Capture();foreach(var o in s.Objects){o.Location="Hand";o.OwnerId="CH_01";o.AnchorId="";}Assert.Throws<ArgumentException>(()=>LifeWorld.Restore(s));
  s=w.Capture();s.Actors[0].Position.X=double.NaN;Assert.Throws<ArgumentException>(()=>LifeWorld.Restore(s));
  s=w.Capture();s.Actors[0].Position.X=9;s.Actors[0].Position.Z=5;Assert.Throws<ArgumentException>(()=>LifeWorld.Restore(s));
  s=w.Capture();s.Actors[0].Id="CH_99";Assert.Throws<ArgumentException>(()=>LifeWorld.Restore(s));
 }
 [Test] public void AT_SAVE_01_FixtureAtomicInterruptedWriteKeepsOriginal()
 {
  string path=Path.Combine(Path.GetTempPath(),"bassline_fixture_"+Guid.NewGuid().ToString("N")+".dat");var store=new FixtureSaveStore(s=>JsonUtility.ToJson(s),s=>JsonUtility.FromJson<LifeSnapshot>(s));var w=new LifeWorld();
  try{store.Save(path,w.Capture());w.Step(new NoObstacles(w));Assert.Throws<IOException>(()=>store.Save(path,w.Capture(),c=>{if(c==SaveCheckpoint.BeforeReplace)throw new IOException("injected");}));Assert.That(store.Load(path).Tick,Is.EqualTo(0));File.AppendAllText(path,"corrupt");Assert.Throws<InvalidDataException>(()=>store.Load(path));}
  finally{foreach(var suffix in new[]{"",".tmp",".previous"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
 }
 [Test] public void AT_TIME_02_ActivityReservationAndNestedPauseSave()
 {
  var w=new LifeWorld();var p=new NoObstacles(w);w.Schedule("CH_02","ACT_READ","K_SEAT_H_01");w.Schedule("CH_03","ACT_READ","K_SEAT_H_01");w.Step(p);
  Assert.That(w.Capture().Seats.Length,Is.EqualTo(1));Assert.That(w.Actor("CH_03").Phase,Is.EqualTo("WaitingSeat"));w.Pause("K_NOTE",true);w.Pause("K_SETTINGS",true);
  var before=JsonUtility.ToJson(w.Capture());for(int i=0;i<100;i++)w.Step(p);Assert.That(JsonUtility.ToJson(w.Capture()),Is.EqualTo(before));
  var restored=LifeWorld.Restore(w.Capture());restored.Pause("K_NOTE",false);Assert.That(restored.Paused,Is.True);restored.Pause("K_SETTINGS",false);Assert.That(restored.Paused,Is.False);
 }
 [Test] public void URP_AllQualityLevelsAndBothScenesUseUniversal()
 {
  var pipeline=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(UrpMigration.PipelinePath);Assert.That(pipeline,Is.Not.Null);
  Assert.That(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline,Is.SameAs(pipeline));for(int i=0;i<QualitySettings.names.Length;i++)Assert.That(QualitySettings.GetRenderPipelineAssetAt(i),Is.SameAs(pipeline));
  foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/BASSLINE/Environment"})){var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));Assert.That(m.shader.name,Is.EqualTo("Universal Render Pipeline/Lit"));}
 }
}
