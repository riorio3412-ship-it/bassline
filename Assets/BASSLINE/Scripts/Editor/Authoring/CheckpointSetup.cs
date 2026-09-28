using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
namespace BASSLINE.Authoring
{
 public static class CheckpointSetup
 {
  static AddAndRemoveRequest request;static double deadline;
  public static void Install()
  {
   var scene=EditorSceneManager.OpenScene("Assets/BASSLINE/Scenes/Mansion_Main.unity",OpenSceneMode.Single);
   var mats=AssetDatabase.FindAssets("t:Material",new[]{"Assets/BASSLINE"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
   File.WriteAllLines("Verification/checkpoint02-preflight.txt",new[]{"Editor="+Application.unityVersion,"Graphics="+(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline?.name??"Built-in"),"Materials="+string.Join(",",mats),"Lightmaps="+LightmapSettings.lightmaps.Length,"ReflectionProbes="+UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Length,"PPv2="+UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Count(x=>x&&x.GetType().FullName.Contains("PostProcess")),"Rollback=../BASSLINE_Implementation_checkpoint01.zip SHA256 74D955F6F1CB20B11A8CC828726C6B724F7C4E41B474ECB9C713710788C6261E"});
   request=Client.AddAndRemove(new[]{"com.unity.render-pipelines.universal","com.unity.pipeline","com.unity.ugui"});deadline=EditorApplication.timeSinceStartup+300;EditorApplication.update+=Poll;
  }
  static void Poll(){if(request==null)return;if(!request.IsCompleted){if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("Package install timeout");EditorApplication.Exit(2);}return;}EditorApplication.update-=Poll;if(request.Status!=StatusCode.Success){Debug.LogError(request.Error.message);EditorApplication.Exit(1);return;}Debug.Log("CHECKPOINT02_PACKAGES="+string.Join(",",request.Result.Select(x=>x.name+"@"+x.version)));AssetDatabase.SaveAssets();EditorApplication.Exit(0);}
 }
}
