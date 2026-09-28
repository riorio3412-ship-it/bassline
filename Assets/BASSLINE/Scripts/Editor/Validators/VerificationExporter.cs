using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using BASSLINE.AuthoringData;
using BASSLINE.Save;
namespace BASSLINE.Authoring
{
 public static class VerificationExporter
 {
  public static void BuildAndExport(){MainHallBuilder.Build();FixtureBuilder.Build();IncidentFixtureBuilder.Build();Export();}
  [MenuItem("BASSLINE/Production/4 Export verification manifest")]
  public static void Export()
  {
   var index=ProductionImporter.LoadIndex();var rows=new List<string>{"StableID,Kind,AssetPath,AssetGUID,LocalFileID,Status"};
   foreach(var record in index.Records){
    if(!File.Exists(record.Path)||ProductionImporter.FileHash(record.Path)!=record.OutputHash)throw new InvalidDataException("Generated asset changed: "+record.Path);
    rows.Add(Line(record.Id,"GeneratedAsset",record.Path,AssetDatabase.AssetPathToGUID(record.Path),"",record.Status));
    if(record.Path.EndsWith(".prefab",StringComparison.Ordinal)){
     var root=AssetDatabase.LoadAssetAtPath<GameObject>(record.Path);
     foreach(var identity in root.GetComponentsInChildren<AuthoringIdentity>(true)){
      AssetDatabase.TryGetGUIDAndLocalFileIdentifier(identity,out string guid,out long localId);
      rows.Add(Line(identity.StableId,identity.Kind,record.Path,guid,localId.ToString(),identity.AssetStatus));
     }
    }
    if(record.Path.EndsWith(".unity",StringComparison.Ordinal)){
     if(!Application.isBatchMode)throw new InvalidOperationException("Scene binding export is batch-only; preserves interactive scenes");
     var scene=EditorSceneManager.OpenScene(record.Path,OpenSceneMode.Single);
     foreach(var identity in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<AuthoringIdentity>(true)))
      rows.Add(Line(identity.StableId,identity.Kind,record.Path,AssetDatabase.AssetPathToGUID(record.Path),GlobalObjectId.GetGlobalObjectIdSlow(identity).targetObjectId.ToString(),identity.AssetStatus));
     foreach(var binding in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<FixtureTarget>(true)).Where(x=>!x.GetComponents<AuthoringIdentity>().Any(a=>a.StableId==x.StableId)))
      rows.Add(Line(binding.StableId,"FixtureInteractionTarget",record.Path,AssetDatabase.AssetPathToGUID(record.Path),GlobalObjectId.GetGlobalObjectIdSlow(binding).targetObjectId.ToString(),"TestOnly_Proxy"));
    }
   }
   File.WriteAllLines("Verification/GeneratedBindingManifest.csv",rows);
   var scripts=Directory.GetFiles("Assets/BASSLINE","*.cs",SearchOption.AllDirectories).Concat(Directory.GetFiles("Assets/BASSLINE","*.asmdef",SearchOption.AllDirectories)).OrderBy(x=>x,StringComparer.Ordinal);
   var revision=AtomicSaveStore.Hash(string.Join("\n",scripts.Select(x=>x.Replace('\\','/')+":"+AtomicSaveStore.Hash(File.ReadAllText(x)))));
   File.WriteAllText("Verification/build-revision.txt",revision+"\n");
   File.WriteAllText("Verification/engine-environment.txt","Editor="+Application.unityVersion+"\nOS="+SystemInfo.operatingSystem+"\nCPU="+SystemInfo.processorType+"\nRAM_MB="+SystemInfo.systemMemorySize+"\nGPU="+SystemInfo.graphicsDeviceName+"\nMap=M01\nRule=Bootstrap_001_REV10\nSaveSchema=BASSLINE_BOOTSTRAP_SAVE/1\nSeed=0 (no RNG implemented)\nRevision="+revision+"\nCatalog="+ProductionImporter.CatalogHash()+"\n");
   File.AppendAllText("Verification/engine-environment.txt","Pipeline=URP 17.6.0\nuGUI=2.6.0\nTestFramework=1.8.0\nFixtureMap="+BASSLINE.World.Fixture.FixtureDefinition.MapVersion+"\nFixtureRules="+BASSLINE.World.Fixture.FixtureDefinition.RuleVersion+"\nFixtureSave=BASSLINE_FIXTURE_SAVE/1\nSession=TestOnly\n");
   Debug.Log("BASSLINE verification binding manifest exported, revision "+revision);
  }
  static string Line(params string[] values)=>string.Join(",",values.Select(v=>"\""+(v??"").Replace("\"","\"\"")+"\""));
 }
}

