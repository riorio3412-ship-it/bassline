using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using BASSLINE.Core;
using BASSLINE.World;
using BASSLINE.Save;
using BASSLINE.Bootstrap;
using BASSLINE.Authoring;
using BASSLINE.AuthoringData;
using BASSLINE.Presentation;

public sealed class BootstrapEditModeTests
{
 [Serializable] sealed class AssemblyInfo{public string name;public string[] references;public bool noEngineReferences;}
 sealed class Surface:IVisualBreakSurface
 {
  public string CurrentSpeakerId {get;set;}="CH_02";
  public bool Approved; public string Shown;
  public bool HasApprovedOverlay(BreakCue cue)=>Approved;
  public void ShowOverlay(BreakCue cue){Shown=cue.Id;}
  public void ClearOverlay(){Shown=null;}
 }
 [SetUp] public void Metadata(){TestContext.Progress.WriteLine("Editor="+Application.unityVersion+"; package=com.unity.test-framework@1.8.0; seed=0; Map=M01/TestOnly_P0; Rule=Bootstrap_001_REV10; revision="+File.ReadAllText("Verification/build-revision.txt")+"; session=TestOnly; Schema=1");}
 [Test] public void AT_SPACE_01_ImportedManifestReferences()
 {
  var data=PackageData.Load(ProductionImporter.SourceRoot);
  Assert.That(PackageValidator.Validate(data,Directory.GetCurrentDirectory()).Where(x=>x.Severity=="ERROR"),Is.Empty);
  Assert.That(data.Rooms.Count,Is.EqualTo(80));Assert.That(data.Connections.Count,Is.EqualTo(79));
  foreach(var room in data.Rooms){
   var definition=data.Assets.Single(x=>x["AssetID"]=="SO_"+room["RoomID"]);
   Assert.That(AssetDatabase.LoadAssetAtPath<ImportedDefinition>(definition["AssetPath"]).StableId,Is.EqualTo(room["RoomID"]));
   var prefab=data.Assets.Single(x=>x["AssetID"]=="ENV_"+room["RoomID"]);
   Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefab["AssetPath"]).GetComponent<AuthoringIdentity>().StableId,Is.EqualTo(room["RoomID"]));
  }
 }
 [Test] public void IMPORT_RepeatedRunPreservesPathsHashesAndGuids()
 {
  var before=ProductionImporter.LoadIndex().Records.Select(x=>x.Id+"|"+x.Path+"|"+ProductionImporter.FileHash(x.Path)+"|"+AssetDatabase.AssetPathToGUID(x.Path)).ToArray();
  MainHallBuilder.Build();
  var after=ProductionImporter.LoadIndex().Records.Select(x=>x.Id+"|"+x.Path+"|"+ProductionImporter.FileHash(x.Path)+"|"+AssetDatabase.AssetPathToGUID(x.Path)).ToArray();
  Assert.That(after,Is.EqualTo(before));
 }
 [Test] public void IMPORT_ManualConflictFailsClosed()
 {
  var path=Path.Combine(Path.GetTempPath(),"bassline_"+Guid.NewGuid().ToString("N")+".txt");
  try{
   File.WriteAllText(path,"manual work");Assert.Throws<InvalidOperationException>(()=>ProductionImporter.CanWrite(new GeneratedIndex(),"TEST_ID",path,"source"));Assert.That(File.ReadAllText(path),Is.EqualTo("manual work"));
   File.WriteAllText(path+".meta","guid: original");var index=new GeneratedIndex();index.Records.Add(new GeneratedRecord{Id="TEST_ID",Path=path,SourceHash="source",OutputHash=ProductionImporter.FileHash(path),MetaHash=ProductionImporter.FileHash(path+".meta")});
   File.WriteAllText(path+".meta","guid: manual-change");Assert.Throws<InvalidOperationException>(()=>ProductionImporter.CanWrite(index,"TEST_ID",path,"source"));Assert.That(File.ReadAllText(path+".meta"),Is.EqualTo("guid: manual-change"));
  }
  finally{foreach(var suffix in new[]{"",".meta"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
 }
 [Test] public void AT_TIME_02_DomainNestedPauseOwners()
 {
  var p=new PauseCoordinator();var c=new WorldClock(p,60);
  p.Acquire(new StableId("NOTE_01"),ClockScope.Both);p.Acquire(new StableId("SETTINGS_01"),ClockScope.Both);
  Assert.That(c.Step(),Is.False);p.Release(new StableId("NOTE_01"));Assert.That(c.Step(),Is.False);
  p.Release(new StableId("SETTINGS_01"));Assert.That(c.Step(),Is.True);Assert.That(c.Tick,Is.EqualTo(1));
 }
 [Test] public void AT_SAVE_01_02_SubsetUnityCodecAtomicRestore()
 {
  var p=new PauseCoordinator();var c=new WorldClock(p,60);
  var w=new WorldState(c,p,"TEST_01","TestOnly_P0","Bootstrap_001_REV10",new[]{"R_A","R_B"},new[]{new ActorRecord{Id="CH_01",RoomId="R_A"}},new[]{new DoorRecord{Id="D_01",RoomA="R_A",RoomB="R_B",X=0,Y=0,Z=1,Width=1.2,AccessPolicy="OwnerPermission",OwnerId="CH_01",LockState="KeyLocked"}});
  var cmd=new CommandEnvelope {CommandId="CMD_01",ActorId="CH_01",TargetId="D_01",Action="RequestUnlock",ExpectedRevision=0,RequestTick=0};
  Assert.That(w.Submit(cmd).Status,Is.EqualTo(CommandStatus.Committed));p.Acquire(new StableId("NOTE_01"),ClockScope.Both);
  var s=new SessionSnapshot {CatalogHash="test",TickRate=60,WorldTick=0,PendingWorldSeconds=.005,Pause=p.Capture(),World=w.Capture()};
  var codec=new UnitySnapshotCodec();var store=new AtomicSaveStore(codec);var path=Path.Combine(Path.GetTempPath(),"bassline_"+Guid.NewGuid().ToString("N")+".dat");
  try{
   store.Save(path,s);Assert.Throws<IOException>(()=>store.Save(path,s,k=>{if(k==SaveCheckpoint.BeforeReplace)throw new IOException("Injected");}));
   var loaded=store.Load(path,"test","TestOnly_P0","Bootstrap_001_REV10");Assert.That(codec.Encode(loaded),Is.EqualTo(codec.Encode(s)));
   var restoredPause=PauseCoordinator.Restore(loaded.Pause);var restoredClock=new WorldClock(restoredPause,loaded.TickRate,loaded.WorldTick);
   var restored=WorldState.Restore(loaded.World,restoredClock,restoredPause,"TestOnly_P0","Bootstrap_001_REV10");
   Assert.That(restored.Submit(cmd).Status,Is.EqualTo(CommandStatus.Committed));Assert.That(restored.Capture().Events.Length,Is.EqualTo(1));Assert.That(restoredClock.Step(),Is.False);
   File.AppendAllText(path,"corrupt");Assert.Throws<InvalidDataException>(()=>store.Load(path,"test","TestOnly_P0","Bootstrap_001_REV10"));
  }finally{foreach(var suffix in new[]{"",".tmp",".previous"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
 }
 [Test] public void BREAK_OnlyThreePublicSpeakersAndApprovedAssets()
 {
  var s=new Surface();using(var p=new VisualBreakPlayer(s)){
   Assert.That(p.PlayVisualBreak("Jinwoo_Mocking_01"),Is.EqualTo(BreakResult.MissingApprovedArt));s.Approved=true;
   s.CurrentSpeakerId="CH_03";Assert.That(p.PlayVisualBreak("Jinwoo_Mocking_01"),Is.EqualTo(BreakResult.WrongSpeaker));
   s.CurrentSpeakerId="CH_02";Assert.That(p.PlayVisualBreak("Jinwoo_Mocking_01"),Is.EqualTo(BreakResult.Started));
   Assert.That(p.PlayVisualBreak("Jinwoo_Mocking_01"),Is.EqualTo(BreakResult.Busy));p.Paused=true;p.Advance(10);Assert.That(s.Shown,Is.Not.Null);
   p.Paused=false;p.Advance(.85);Assert.That(s.Shown,Is.Null);
   s.CurrentSpeakerId="PRES_YUSTI";Assert.That(p.PlayVisualBreak("Yusti_Panic_01"),Is.EqualTo(BreakResult.Started));p.Advance(.35);Assert.That(s.Shown,Is.Null);
   s.CurrentSpeakerId="CH_04";Assert.That(p.PlayVisualBreak("Doyoon_Silence_01"),Is.EqualTo(BreakResult.Started));s.CurrentSpeakerId="CH_01";p.Advance(0);Assert.That(s.Shown,Is.Null);
   Assert.That(p.PlayVisualBreak("Minhyuk_Break_01"),Is.EqualTo(BreakResult.UnknownCue));
  }
 }
 [Test] public void BREAK_ReducedEffectsAndDisableClearTransientState()
 {
  var s=new Surface{Approved=true};var p=new VisualBreakPlayer(s);p.PlayVisualBreak("Jinwoo_Mocking_01");p.ReducedEffects=true;
  Assert.That(s.Shown,Is.Null);Assert.That(p.PlayVisualBreak("Jinwoo_Mocking_01"),Is.EqualTo(BreakResult.ReducedEffects));p.Dispose();
  Assert.That(p.PlayVisualBreak("Jinwoo_Mocking_01"),Is.EqualTo(BreakResult.Disabled));
 }
 [Test] public void AT_INFO_01_SubsetAssemblyAuthorityBoundary()
 {
  var definitions=Directory.GetFiles("Assets/BASSLINE/Scripts","*.asmdef",SearchOption.AllDirectories).Select(p=>JsonUtility.FromJson<AssemblyInfo>(File.ReadAllText(p))).ToDictionary(x=>x.name);
  foreach(var root in definitions.Values){
   var visited=new System.Collections.Generic.HashSet<string>();var pending=new System.Collections.Generic.Stack<string>();pending.Push(root.name);
   while(pending.Count>0){var current=pending.Pop();if(!visited.Add(current))continue;foreach(var reference in definitions[current].references??Array.Empty<string>()){if(!reference.StartsWith("BASSLINE.",StringComparison.Ordinal))continue;Assert.That(definitions.ContainsKey(reference),Is.True,"Unresolved assembly "+reference);pending.Push(reference);}}
   if(root.name.StartsWith("BASSLINE.Presentation",StringComparison.Ordinal)||new[]{"BASSLINE.UI","BASSLINE.NPC","BASSLINE.Knowledge","BASSLINE.Investigation","BASSLINE.Trial"}.Contains(root.name)){Assert.That(visited,Does.Not.Contain("BASSLINE.World"));Assert.That(visited,Does.Not.Contain("BASSLINE.Save"));Assert.That(visited,Does.Not.Contain("BASSLINE.Bootstrap"));}
   if(root.noEngineReferences)Assert.That(visited.Any(x=>x.StartsWith("BASSLINE.Presentation",StringComparison.Ordinal)&&!root.name.StartsWith("BASSLINE.Presentation",StringComparison.Ordinal)),Is.False,"Domain depends on Presentation");
  }
 }
 [Test] public void AT_SAVE_04_SubsetRejectsMismatchedReceipts()
 {
  var pause=new PauseCoordinator();var clock=new WorldClock(pause,60);
  var world=new WorldState(clock,pause,"TEST_01","TestOnly_P0","Bootstrap_001_REV10",new[]{"R_A","R_B"},new[]{new ActorRecord{Id="CH_01",RoomId="R_A"}},new[]{new DoorRecord{Id="D_01",RoomA="R_A",RoomB="R_B",Z=1,Width=1.2,AccessPolicy="OwnerPermission",OwnerId="CH_01",LockState="KeyLocked"}});
  world.Submit(new CommandEnvelope {CommandId="CMD_01",ActorId="CH_01",TargetId="D_01",Action="RequestUnlock"});
  var snapshot=world.Capture();snapshot.Processed[0].Command.ActorId="CH_02";
  Assert.Throws<ArgumentException>(()=>WorldState.Restore(snapshot,clock,pause,"TestOnly_P0","Bootstrap_001_REV10"));
  snapshot=world.Capture();snapshot.Doors[0].LockState="KeyLocked";
  Assert.Throws<ArgumentException>(()=>WorldState.Restore(snapshot,clock,pause,"TestOnly_P0","Bootstrap_001_REV10"));
 }
}
