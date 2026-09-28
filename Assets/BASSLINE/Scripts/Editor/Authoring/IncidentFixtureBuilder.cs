using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.Bootstrap;
using BASSLINE.World.Fixture;
using BASSLINE.Save;
namespace BASSLINE.Authoring
{
 public static class IncidentFixtureBuilder
 {
  public const string ScenePath="Assets/BASSLINE/Scenes/Tests/FixtureK_Incident.unity";
  const string Version="IncidentFixtureBuilder_006";
  [MenuItem("BASSLINE/Production/7 Build FixtureK incident contact subset")]
  public static void Build()
  {
   var index=ProductionImporter.LoadIndex();string hash=AtomicSaveStore.Hash(Version+ProductionImporter.CatalogHash());
   if(!ProductionImporter.CanWrite(index,"SCN_FixtureK_Incident",ScenePath,hash))return;
   FixtureBuilder.Build();EditorSceneManager.OpenScene(FixtureBuilder.ScenePath);
   var runtime=UnityEngine.Object.FindFirstObjectByType<FixtureRuntime>();
   const string dataPath="Assets/BASSLINE/Data/Incidents/K_INCIDENT_SETUP_01.asset";
   if(ProductionImporter.CanWrite(index,"SO_K_INCIDENT_01",dataPath,hash)){
    var data=AssetDatabase.LoadAssetAtPath<FixtureIncidentDefinition>(dataPath);
    if(!data){data=ScriptableObject.CreateInstance<FixtureIncidentDefinition>();AssetDatabase.CreateAsset(data,dataPath);}
    data.Settings=new IncidentSettings();EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();ProductionImporter.Track(index,"SO_K_INCIDENT_01",dataPath,hash,"TestOnly_ContactSubset_Proxy");
   }
   runtime.IncidentDefinition=AssetDatabase.LoadAssetAtPath<FixtureIncidentDefinition>(dataPath);
   var initial=new LifeWorld(runtime.IncidentDefinition.Settings);
   foreach(var body in runtime.Bodies){body.transform.position=FixtureRuntime.V(initial.Pose(body.ActorId));
    var fallen=new GameObject("CollapsedCollision_Proxy");fallen.transform.SetParent(body.transform,false);var collider=fallen.AddComponent<BoxCollider>();collider.center=new Vector3(.55f,.25f,0);collider.size=new Vector3(body.Height*.9f,.5f,.5f);fallen.SetActive(false);
   }
   runtime.Bodies.Single(x=>x.ActorId=="CH_03").Head.rotation=Quaternion.LookRotation(new Vector3(-2.35f,0,1.15f));
   runtime.Bodies.Single(x=>x.ActorId=="CH_18").Head.rotation=Quaternion.Euler(0,0,0);
   var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/BASSLINE/Environment/Materials/Proxy/K_MAT_PROP.mat");
   var root=runtime.transform.parent; // Null is deliberate: fixture world objects are roots with stable IDs.
   GameObject Box(string id,Vector3 p,Vector3 scale){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=id;go.transform.SetParent(root);go.transform.position=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;ProductionImporter.Identity(go,id,"TestOnly_Proxy");return go;}
   Box("K_WORKBENCH",new Vector3(26.85f,.45f,-1.95f),new Vector3(1.45f,.9f,.55f));
   var item=initial.Object("K_O31");var tool=Box(item.Id,FixtureRuntime.V(item.Position),new Vector3(.18f,.1f,.18f));var binding=tool.AddComponent<FixtureObjectBody>();binding.ObjectId=item.Id;binding.Collider=tool.GetComponent<Collider>();var target=tool.AddComponent<FixtureTarget>();target.StableId=item.Id;target.PublicName=item.Name;runtime.ObjectBodies=runtime.ObjectBodies.Concat(new[]{binding}).ToArray();
   var readerObject=Box("K_V1",new Vector3(29.5f,1.05f,-1.6f),new Vector3(.3f,.25f,.18f));var reader=readerObject.AddComponent<FixtureCaseReader>();reader.StableId="K_V1";reader.Kind="Video";target=readerObject.AddComponent<FixtureTarget>();target.StableId="K_V1";target.PublicName="V1 · 작업대 기록 열람";
   var sensor=new GameObject("K_V1_PreinstalledSensor");sensor.transform.position=new Vector3(27.1f,2.9f,-2.6f);sensor.transform.rotation=Quaternion.LookRotation(new Vector3(27.1f,.9f,-1.1f)-sensor.transform.position);reader.Sensor=sensor.transform;ProductionImporter.Identity(sensor,"K_V1_SENSOR","TestOnly_PreinstalledSensor");
   var trace=Box("K_P31",new Vector3(27.5f,.3f,-1.1f),new Vector3(.12f,.025f,.12f));var traceReader=trace.AddComponent<FixtureCaseReader>();traceReader.StableId="K_P31";traceReader.Kind="ContactTrace";target=trace.AddComponent<FixtureTarget>();target.StableId="K_P31";target.PublicName="P31 · 접촉 흔적";trace.GetComponent<Collider>().isTrigger=true;trace.SetActive(false);runtime.CaseReaders=new[]{reader,traceReader};
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
   ProductionImporter.Track(ProductionImporter.LoadIndex(),"SCN_FixtureK_Incident",ScenePath,hash,"TestOnly_ContactSubset_Proxy");
   EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(x=>x.path!=ScenePath).Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();AssetDatabase.SaveAssets();
   Debug.Log("FixtureK contact subset generated. Source clock 14:59; physically conditioned X31; not full FixtureK/P7 acceptance.");
  }
 }
}





