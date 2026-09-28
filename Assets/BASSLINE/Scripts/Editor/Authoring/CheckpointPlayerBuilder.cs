using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace BASSLINE.Authoring
{
 public static class CheckpointPlayerBuilder
 {
  [MenuItem("BASSLINE/Production/6 Build Windows FixtureK checkpoint")]
  public static void QueueBuild(){EditorApplication.delayCall+=Build;}
  public static void Build()
  {
   const string reportPath="Verification/checkpoint04-player-build.json";
   try{
    if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64))throw new NotSupportedException("Windows player support missing");
    MainHallBuilder.Build();FixtureBuilder.Build();IncidentFixtureBuilder.Build();
    var engineRecords=ProductionImporter.LoadIndex().Records.Where(r=>r.Status=="EngineGenerated_URP").ToArray();foreach(var record in engineRecords)ProductionImporter.CanWrite(ProductionImporter.LoadIndex(),record.Id,record.Path,record.SourceHash);
    PlayerSettings.companyName="BASSLINE";PlayerSettings.productName="BASSLINE FixtureK - TestOnly Proxy";PlayerSettings.bundleVersion="0.4.0";
    PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
    var output=Path.GetFullPath("Builds/Windows");Directory.CreateDirectory(output);
    var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{FixtureBuilder.ScenePath,"Assets/BASSLINE/Scenes/Mansion_Main.unity",IncidentFixtureBuilder.ScenePath},locationPathName=Path.Combine(output,"BASSLINE_FixtureK.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
    // Unity updates serialized pipeline build caches. Only seal assets whose
    // pre-build bytes matched ownership and whose metadata remained unchanged.
    AssetDatabase.SaveAssets();foreach(var record in engineRecords){if(ProductionImporter.FileHash(record.Path+".meta")!=record.MetaHash)throw new IOException("Engine asset GUID changed during build");ProductionImporter.Track(ProductionImporter.LoadIndex(),record.Id,record.Path,record.SourceHash,record.Status);}
    File.WriteAllText(reportPath,JsonUtility.ToJson(new BuildReceipt{Status=report.summary.result.ToString(),Output=output,Editor=Application.unityVersion,Errors=report.summary.totalErrors,Warnings=report.summary.totalWarnings,Seconds=report.summary.totalTime.TotalSeconds},true));
    if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Player build failed: "+report.summary.result);
   }catch(Exception e){File.WriteAllText(reportPath,JsonUtility.ToJson(new BuildReceipt{Status="Failed",Errors=1,Detail=e.ToString()},true));Debug.LogException(e);}
  }
  [Serializable] sealed class BuildReceipt{public string Status,Output,Editor,Detail;public int Errors,Warnings;public double Seconds;}
 }
}


