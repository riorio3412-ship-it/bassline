using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using BASSLINE.Core;
using BASSLINE.World.Fixture;
using BASSLINE.AuthoringData;
using BASSLINE.Bootstrap;
using BASSLINE.UI;
using BASSLINE.Save;
namespace BASSLINE.Authoring
{
 public static class FixtureBuilder
 {
  public const string ScenePath="Assets/BASSLINE/Scenes/Tests/FixtureK_Life.unity";
  const string Version="FixtureBuilder_009";static Material floor,wall,accent,doorMat,prop;static Transform geometry;
  [MenuItem("BASSLINE/Production/5 Build FixtureK life scene")]
  public static void Build()
  {
   if(!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset))throw new InvalidOperationException("URP must be configured first");
   var index=ProductionImporter.LoadIndex();string hash=AtomicSaveStore.Hash(Version+FixtureDefinition.MapVersion+ProductionImporter.CatalogHash());if(!ProductionImporter.CanWrite(index,"SCN_FixtureK_Life",ScenePath,hash))return;
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
   geometry=new GameObject("WorldRoot_FixtureK_TestOnly").transform;ProductionImporter.Identity(geometry.gameObject,"K_WORLD_ROOT","FixtureK_TestOnly");
   floor=Mat("K_MAT_FLOOR",new Color(.56f,.62f,.64f));wall=Mat("K_MAT_WALL",new Color(.75f,.79f,.8f));accent=Mat("K_MAT_ACCENT",new Color(.29f,.38f,.42f));doorMat=Mat("K_MAT_DOOR",new Color(.37f,.27f,.28f));prop=Mat("K_MAT_PROP",new Color(.67f,.48f,.31f));
   var cells=FixtureDefinition.WalkCells();foreach(string key in cells){var xy=key.Split(',');int x=int.Parse(xy[0]),z=int.Parse(xy[1]);Box("K_FLOOR_"+x+"_"+z,new Vector3(x+.5f,-.1f,z+.5f),new Vector3(1,.2f,1),floor,true);
    foreach(var side in new[]{new Vector2Int(1,0),new Vector2Int(-1,0),new Vector2Int(0,1),new Vector2Int(0,-1)})if(!cells.Contains((x+side.x)+","+(z+side.y)))Box("K_WALL_"+x+"_"+z+"_"+side.x+"_"+side.y,new Vector3(x+.5f+side.x*.5f,1.8f,z+.5f+side.y*.5f),new Vector3(side.x==0?1:.12f,3.6f,side.x==0?.12f:1),wall,true);
   }
   var runtime=new GameObject("Bootstrap_FixtureK_TestOnly").AddComponent<FixtureRuntime>();runtime.gameObject.AddComponent<TestOnlySceneNavigation>();ProductionImporter.Identity(runtime.gameObject,"SESSION_FIXTURE_K","TestOnly");
   var doors=new List<FixtureDoorBody>();foreach(string id in new[]{"K_DOOR_N","K_DOOR_S"}){
    Vector3 p=FixtureRuntime.V(LifeWorld.DoorPosition(id));var root=new GameObject(id);root.transform.SetParent(geometry,false);root.transform.position=p;ProductionImporter.Identity(root,id,"FixtureDoor");var binding=root.AddComponent<FixtureDoorBody>();binding.DoorId=id;
    var leaf=Box(id+"_Leaf",p+Vector3.up*1.19f,new Vector3(1.18f,2.38f,.14f),doorMat,true);leaf.transform.SetParent(root.transform,true);binding.Leaf=leaf.transform;binding.ClosedLocalPosition=leaf.transform.localPosition;var target=root.AddComponent<FixtureTarget>();target.StableId=id;target.PublicName=id=="K_DOOR_N"?"북문 N":"남문 S";
    foreach(float x in new[]{-.8f,.8f})Box(id+"_Jamb_"+(x<0?"L":"R"),p+new Vector3(x,1.2f,0),new Vector3(.4f,2.4f,.2f),accent,true);
    Box(id+"_Lintel",p+new Vector3(0,3f,0),new Vector3(2,1.2f,.2f),accent,true);doors.Add(binding);
   }runtime.DoorBodies=doors.ToArray();
   foreach(var a in FixtureDefinition.Anchors){var marker=Box(a.Id,FixtureRuntime.V(a.Position)+Vector3.up*.015f,new Vector3(.65f,.03f,.65f),accent,false);var collider=marker.AddComponent<BoxCollider>();collider.size=new Vector3(1,2,1);collider.isTrigger=true;var target=marker.AddComponent<FixtureTarget>();target.StableId=a.Id;target.PublicName="활동 자리";ProductionImporter.Identity(marker,a.Id,a.Id.StartsWith("K_PASS_",StringComparison.Ordinal)?"PassageAnchor_TestOnly":"SeatAnchor_Proxy");
    // Seat pose is a standing Proxy marker until approved rig/seat IK. It reserves real floor space.
   }
   var bodies=new List<FixtureActorBody>();var characters=PackageData.Load(ProductionImporter.SourceRoot).Characters;
   var initial=new LifeWorld();foreach(var id in FixtureDefinition.Actors){var row=characters.Single(x=>x["CharacterID"]==id);float h=(float)row.Number("HeightCm")/100;
    var root=new GameObject(id+"_Proxy");root.transform.position=FixtureRuntime.V(initial.Pose(id));var capsule=root.AddComponent<UnityEngine.CharacterController>();capsule.height=h;capsule.center=new Vector3(0,h/2,0);capsule.radius=.28f;capsule.skinWidth=.02f;capsule.stepOffset=.2f;capsule.minMoveDistance=0;
    var body=root.AddComponent<FixtureActorBody>();body.ActorId=id;body.Capsule=capsule;body.Height=h;var target=root.AddComponent<FixtureTarget>();target.StableId=id;target.PublicName=row["Name"];ProductionImporter.Identity(root,id,"CharacterProxy_TestOnly");
    var torso=GameObject.CreatePrimitive(PrimitiveType.Capsule);torso.name="NormalSilhouette_Proxy";torso.transform.SetParent(root.transform,false);torso.transform.localPosition=new Vector3(0,h*.46f,0);torso.transform.localScale=new Vector3(.46f,h*.45f,.46f);UnityEngine.Object.DestroyImmediate(torso.GetComponent<Collider>());torso.GetComponent<Renderer>().sharedMaterial=accent;if(id=="CH_01")torso.SetActive(false);
    body.Head=Child(root.transform,"HeadProxy",new Vector3(0,h*.88f,0));body.RightHand=Child(root.transform,"RightHandProxy",new Vector3(.29f,h*.57f,.2f));body.LeftHand=Child(root.transform,"LeftHandProxy",new Vector3(-.29f,h*.57f,.2f));bodies.Add(body);
   }runtime.Bodies=bodies.ToArray();
   var items=new List<FixtureObjectBody>();foreach(var item in initial.Capture().Objects){var go=Box(item.Id,FixtureRuntime.V(item.Position),item.Id.Contains("BOOK")?new Vector3(.28f,.08f,.34f):new Vector3(.12f,.2f,.12f),prop,true);var binding=go.AddComponent<FixtureObjectBody>();binding.ObjectId=item.Id;binding.Collider=go.GetComponent<Collider>();var target=go.AddComponent<FixtureTarget>();target.StableId=item.Id;target.PublicName=item.Name;items.Add(binding);}runtime.ObjectBodies=items.ToArray();
   Box("K_TABLE_H",new Vector3(.3f,.4f,-1.8f),new Vector3(1.5f,.8f,.65f),accent,true);
   var camera=new GameObject("MainCamera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.SetParent(bodies.Single(x=>x.ActorId=="CH_01").transform,false);camera.transform.localPosition=new Vector3(0,1.6f,0);camera.fieldOfView=68;camera.Additional();camera.gameObject.AddComponent<AudioListener>();
   var light=new GameObject("FixtureDaylight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.65f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(55,-30,0);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.42f,.45f,.48f);
   ProductionUiBuilder.Attach(runtime,camera);Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));EditorSceneManager.SaveScene(scene,ScenePath);ProductionImporter.Track(ProductionImporter.LoadIndex(),"SCN_FixtureK_Life",ScenePath,hash,"TestOnly_FunctionalProxy");
   var scenes=EditorBuildSettings.scenes.Where(x=>x.path!=ScenePath).ToList();scenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=scenes.ToArray();
   File.WriteAllLines("Verification/fixture-route-lengths.csv",new[]{"RouteID,LengthMeters,Source"}.Concat(FixtureDefinition.Edges.Select(e=>$"{e.Id},{e.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)},SRC11_P1636_or_ExplicitLocalLeg")));
   AssetDatabase.SaveAssets();Debug.Log("FixtureK life scene generated. No incident/story assignments; all models and seats Proxy.");
  }
  static void Additional(this Camera camera){camera.gameObject.AddComponent<UniversalAdditionalCameraData>();}
  static Transform Child(Transform parent,string name,Vector3 local){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=local;return go.transform;}
  static Material Mat(string id,Color color){string path="Assets/BASSLINE/Environment/Materials/Proxy/"+id+".mat";var index=ProductionImporter.LoadIndex();string hash=AtomicSaveStore.Hash(Version+id);if(!ProductionImporter.CanWrite(index,id,path,hash))return AssetDatabase.LoadAssetAtPath<Material>(path);var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.2f);EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();ProductionImporter.Track(index,id,path,hash,"MaterialProxy_URP");return material;}
  static GameObject Box(string id,Vector3 position,Vector3 scale,Material mat,bool collision){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=id;go.transform.SetParent(geometry,false);go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
  static void CreateHud(FixtureRuntime runtime,Camera camera)
  {
   var root=new GameObject("UIRoot",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
   var hud=root.AddComponent<FixtureHud>();hud.Source=runtime;hud.ViewCamera=camera;hud.PlayerFacing=camera.transform.parent;hud.FontFamily="Malgun Gothic";
   hud.Clock=Text("Clock",root.transform,new Vector2(1,1),new Vector2(-30,-22),new Vector2(180,45),26,TMP_Settings.defaultFontAsset);hud.Clock.alignment=TextAlignmentOptions.TopRight;hud.Clock.text="14:50:00";
   var panel=new GameObject("NOTE",typeof(RectTransform),typeof(UnityEngine.UI.Image));panel.transform.SetParent(root.transform,false);var rect=panel.GetComponent<RectTransform>();rect.anchorMin=new Vector2(.2f,.1f);rect.anchorMax=new Vector2(.8f,.9f);rect.offsetMin=rect.offsetMax=Vector2.zero;panel.GetComponent<UnityEngine.UI.Image>().color=new Color(.06f,.085f,.1f,.97f);hud.NotePanel=panel;
   hud.NoteText=Text("NoteText",panel.transform,new Vector2(0,1),new Vector2(40,-35),new Vector2(1000,780),28,TMP_Settings.defaultFontAsset);hud.NoteText.text="NOTE";panel.SetActive(false);
  }
  static TMP_Text Text(string name,Transform parent,Vector2 anchor,Vector2 offset,Vector2 size,int point,TMP_FontAsset font){var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=anchor;rect.anchoredPosition=offset;rect.sizeDelta=size;var text=go.GetComponent<TextMeshProUGUI>();text.font=font;text.fontSize=point;text.enableAutoSizing=false;text.color=new Color(.92f,.94f,.93f);return text;}
 }
}

