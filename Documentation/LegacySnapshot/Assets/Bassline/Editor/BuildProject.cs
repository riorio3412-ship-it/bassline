using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bassline.Editor
{
    public static class BuildProject
    {
        [MenuItem("BASSLINE/Create entry scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Bassline/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene,"Assets/Bassline/Scenes/Mansion.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Bassline/Scenes/Mansion.unity",true)};
            PlayerSettings.companyName="BasslinePrototype";PlayerSettings.productName="BASSLINE";PlayerSettings.bundleVersion="0.1.0";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.runInBackground=true;PlayerSettings.resizableWindow=true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.colorSpace=ColorSpace.Gamma;
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input=settings.FindProperty("activeInputHandler");if(input!=null){input.intValue=0;settings.ApplyModifiedPropertiesWithoutUndo();}
            AssetDatabase.SaveAssets();
            Debug.Log("BASSLINE entry scene created.");
        }
        [MenuItem("BASSLINE/Build Windows")]
        public static void BuildWindows()
        {
            CreateScene();Directory.CreateDirectory("Build");
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{"Assets/Bassline/Scenes/Mansion.unity"},locationPathName="Build/BASSLINE.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development });
            File.WriteAllText("Build/build-result.txt",result.summary.result+"\n"+result.summary.totalErrors+" errors\n"+result.summary.totalSize+" bytes\n");
            if(result.summary.result!=BuildResult.Succeeded)throw new Exception("BASSLINE build failed: "+result.summary.result);
            Debug.Log("BASSLINE_BUILD_OK");
        }
    }
}
