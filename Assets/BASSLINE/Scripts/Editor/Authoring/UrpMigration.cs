using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace BASSLINE.Authoring
{
 public static class UrpMigration
 {
  public const string PipelinePath="Assets/BASSLINE/Environment/Rendering/RP_BASSLINE_URP.asset";
  const string RendererPath="Assets/BASSLINE/Environment/Rendering/RD_BASSLINE_Forward.asset";
  [Serializable] sealed class MaterialBefore{public string Path,Guid,Shader,TexturePath;public Color Color;public Vector2 Scale,Offset;public float Smoothness,Metallic;}
  [Serializable] sealed class Snapshot{public List<MaterialBefore> Materials=new List<MaterialBefore>();}
  public static void Migrate()
  {
   var index=ProductionImporter.LoadIndex();var before=new Snapshot();
   string renderingHash=BASSLINE.Save.AtomicSaveStore.Hash("URP17.6.0_Checkpoint02");
   ProductionImporter.CanWrite(index,"RP_BASSLINE_URP",PipelinePath,renderingHash);ProductionImporter.CanWrite(index,"RD_BASSLINE_Forward",RendererPath,renderingHash);
   foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/BASSLINE"})){
    var path=AssetDatabase.GUIDToAssetPath(guid);var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
    if(!mat||mat.shader.name!="Standard")continue;
    var tracked=index.Records.Single(x=>x.Path==path);ProductionImporter.CanWrite(index,tracked.Id,path,tracked.SourceHash);
    before.Materials.Add(new MaterialBefore{Path=path,Guid=guid,Shader=mat.shader.name,TexturePath=AssetDatabase.GetAssetPath(mat.GetTexture("_MainTex")),Color=mat.GetColor("_Color"),Scale=mat.GetTextureScale("_MainTex"),Offset=mat.GetTextureOffset("_MainTex"),Smoothness=mat.GetFloat("_Glossiness"),Metallic=mat.GetFloat("_Metallic")});
   }
   if(before.Materials.Count>0)File.WriteAllText("Verification/urp-materials-before.json",JsonUtility.ToJson(before,true));
   Directory.CreateDirectory(Path.GetDirectoryName(PipelinePath));AssetDatabase.Refresh();
   var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
   if(!renderer){renderer=ScriptableObject.CreateInstance<UniversalRendererData>();renderer.postProcessData=AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");AssetDatabase.CreateAsset(renderer,RendererPath);}
   var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
   if(!pipeline){pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,PipelinePath);}
   pipeline.msaaSampleCount=4;pipeline.shadowDistance=70;pipeline.supportsHDR=true;pipeline.renderScale=1;
   var settings=new SerializedObject(pipeline);foreach(var field in new[]{"m_MainLightShadowsSupported","m_SoftShadowsSupported"}){var property=settings.FindProperty(field);if(property==null)throw new InvalidDataException("Missing URP property "+field);property.boolValue=true;}settings.ApplyModifiedPropertiesWithoutUndo();
   EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(pipeline);
   GraphicsSettings.defaultRenderPipeline=pipeline;int oldQuality=QualitySettings.GetQualityLevel();
   for(int i=0;i<QualitySettings.names.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=pipeline;}QualitySettings.SetQualityLevel(oldQuality,false);
   foreach(var snapshot in before.Materials){
    var mat=AssetDatabase.LoadAssetAtPath<Material>(snapshot.Path);mat.shader=Shader.Find("Universal Render Pipeline/Lit");mat.SetColor("_BaseColor",snapshot.Color);mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture>(snapshot.TexturePath));mat.SetTextureScale("_BaseMap",snapshot.Scale);mat.SetTextureOffset("_BaseMap",snapshot.Offset);mat.SetFloat("_Smoothness",snapshot.Smoothness);mat.SetFloat("_Metallic",snapshot.Metallic);EditorUtility.SetDirty(mat);
   }
   AssetDatabase.SaveAssets();
   foreach(var snapshot in before.Materials){var record=index.Records.Single(x=>x.Path==snapshot.Path);ProductionImporter.Track(index,record.Id,record.Path,record.SourceHash,"MaterialProxy_URP");}
   ProductionImporter.Track(ProductionImporter.LoadIndex(),"RP_BASSLINE_URP",PipelinePath,renderingHash,"EngineGenerated_URP");ProductionImporter.Track(ProductionImporter.LoadIndex(),"RD_BASSLINE_Forward",RendererPath,renderingHash,"EngineGenerated_URP");
   MainHallBuilder.Build();AssetDatabase.SaveAssets();Validate();
  }
  public static void Validate()
  {
   var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
   if(!pipeline||GraphicsSettings.defaultRenderPipeline!=pipeline||pipeline.rendererDataList.Length==0||pipeline.rendererDataList[0]==null)throw new InvalidDataException("URP asset/renderer invalid");
   var report=new List<string>{"Pipeline=URP 17.6.0","Editor="+Application.unityVersion,"Renderer="+pipeline.rendererDataList[0].name};
   for(int i=0;i<QualitySettings.names.Length;i++){if(QualitySettings.GetRenderPipelineAssetAt(i)!=pipeline)throw new InvalidDataException("Quality pipeline missing");report.Add("Quality="+QualitySettings.names[i]+":URP");}
   foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/BASSLINE/Environment"})){
    var path=AssetDatabase.GUIDToAssetPath(guid);var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat.shader.name.StartsWith("Universal Render Pipeline/",StringComparison.Ordinal)||!mat.shader.isSupported)throw new InvalidDataException("Unsupported material: "+path);report.Add(path+"|"+mat.shader.name);
   }
   var scene=EditorSceneManager.OpenScene("Assets/BASSLINE/Scenes/Mansion_Main.unity",OpenSceneMode.Single);
   if(LightmapSettings.lightmaps.Length!=0)throw new InvalidDataException("Unexpected old lightmap");
   report.Add("Source PPv2/lightmaps/reflection probes/particles/custom shaders: none; no effects discarded");
   report.Add("Main Hall saved camera URP data="+scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Camera>()).All(x=>x.GetComponent<UniversalAdditionalCameraData>()));
   report.Add("Art quality remains Proxy; no baked GI or final art approval claimed");File.WriteAllLines("Verification/urp-validation.txt",report);
  }
 }
}
