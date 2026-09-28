using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
public static class RepairPackages
{
 static ListRequest list; static AddAndRemoveRequest change;
 static readonly string[] keep={"com.unity.modules.audio","com.unity.modules.imgui","com.unity.modules.jsonserialize","com.unity.modules.physics","com.unity.modules.screencapture","com.unity.test-framework"};
 public static void Run(){list=Client.List(true,true);EditorApplication.update+=Tick;}
 static void Tick(){
  if(change!=null){if(!change.IsCompleted)return;Debug.Log("REPAIR_RESULT="+change.Status+" "+change.Error?.message);EditorApplication.Exit(change.Status==StatusCode.Success?0:1);return;}
  if(!list.IsCompleted)return;
  if(list.Status!=StatusCode.Success){Debug.LogError(list.Error.message);EditorApplication.Exit(1);return;}
  var remove=list.Result.Where(p=>p.isDirectDependency&&!keep.Contains(p.name)).Select(p=>p.name).ToArray();
  var add=keep.Where(id=>!list.Result.Any(p=>p.name==id&&p.isDirectDependency)).ToArray();
  Debug.Log("REPAIR_ADD="+string.Join(",",add)+" REMOVE="+string.Join(",",remove));change=Client.AddAndRemove(add,remove);
 }
}
