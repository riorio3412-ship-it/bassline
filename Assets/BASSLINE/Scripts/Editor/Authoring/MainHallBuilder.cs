using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BASSLINE.Bootstrap;
using BASSLINE.Save;
using BASSLINE.Presentation;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BASSLINE.Authoring
{
    public static class MainHallBuilder
    {
        const string ScenePath="Assets/BASSLINE/Scenes/Mansion_Main.unity";
        const string BuilderVersion="MainHall_FunctionalProxy_005_URP";
        static Transform authority,art;
        static Material stone,tile,metal,red,water;
        [MenuItem("BASSLINE/Production/3 Build Main Hall review scene")]
        public static void Build()
        {
            ProductionImporter.Import();var index=ProductionImporter.LoadIndex();
            var hash=AtomicSaveStore.Hash(BuilderVersion+ProductionImporter.CatalogHash());
            if(!ProductionImporter.CanWrite(index,"SCN_Mansion_Main",ScenePath,hash))return;
            if(!Application.isBatchMode&&SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the current scene before building");
            // Additive construction preserves any already open scenes and unsaved user work.
            var previous=SceneManager.GetActiveScene();
            if(!Application.isBatchMode&&string.IsNullOrEmpty(previous.path))throw new InvalidOperationException("Save the current untitled scene before building");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode?NewSceneMode.Single:NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var boot=new GameObject("Bootstrap");boot.AddComponent<TestOnlySceneNavigation>();var session=boot.AddComponent<BootstrapSession>();session.CatalogHash=ProductionImporter.CatalogHash();
                ProductionImporter.Identity(boot,"SESSION_ARTREVIEW_01","ArtReview");
                authority=new GameObject("WorldRoot").transform;art=new GameObject("ArtRoot").transform;new GameObject("UIRoot");
                stone=Material("MAT_PROXY_STONE",new Color32(220,221,215,255),0.25f,0);
                tile=Material("MAT_PROXY_TILE",new Color32(184,193,196,255),0.45f,0);
                metal=Material("MAT_PROXY_METAL",new Color32(113,130,139,255),0.6f,1);
                red=Material("MAT_PROXY_CARPET",new Color32(100,43,53,255),0.08f,0);
                water=Material("MAT_PROXY_WATER",new Color32(130,158,165,255),0.84f,0);
                // Full slab at y=0; the shallow water channels are bounded recesses at y=-0.08.
                Box("HALL_FLOOR_W",new Vector3(-7.75f,-0.15f,0),new Vector3(10.5f,.3f,22),tile);
                Box("HALL_FLOOR_SPINE",new Vector3(0,-.15f,0),new Vector3(4,.3f,22),tile);
                Box("HALL_FLOOR_E",new Vector3(7.75f,-.15f,0),new Vector3(10.5f,.3f,22),tile);
                foreach(float x in new[]{-2.25f,2.25f})
                {
                    Box("CHANNEL_BED_"+(x<0?"W":"E"),new Vector3(x,-.13f,0),new Vector3(.5f,.1f,22),stone);
                    Visual("WATER_"+(x<0?"W":"E"),new Vector3(x,-.012f,0),new Vector3(.5f,.012f,22),water);
                    // Dry crossings at actual east/west circulation axis and entrance; no water-surface collider.
                    Box("CHANNEL_BRIDGE_"+(x<0?"W":"E"),new Vector3(x,-.05f,0),new Vector3(.5f,.1f,4),tile);
                }
                Visual("CARPET",new Vector3(0,.012f,0),new Vector3(3,.02f,22),red);
                // Exterior wall shells: openings are removed geometrically, never filled with hidden collision.
                Wall("S",false,-11.1f,26,2.4f,2.4f);
                Wall("N",false,11.1f,26,4,2.4f);
                Wall("W",true,-13.1f,22,4,2.4f);
                Wall("E",true,13.1f,22,4,2.4f);
                Box("CEILING",new Vector3(0,11.1f,0),new Vector3(26,.2f,22),stone);
                // Short real stub corridors show physical hub openings. These are not M01 completion.
                Box("HUB_E",new Vector3(16,-.15f,0),new Vector3(6,.3f,4),tile);
                Box("HUB_W",new Vector3(-16,-.15f,0),new Vector3(6,.3f,4),tile);
                Box("HUB_N",new Vector3(0,-.15f,14),new Vector3(4,.3f,6),tile);
                Box("HUB_S",new Vector3(0,-.15f,-14),new Vector3(2.4f,.3f,6),tile);
                // Balcony: 2.5m ring, exactly 21x17m central void.
                Box("BALCONY_N",new Vector3(0,5.9f,9.75f),new Vector3(26,.2f,2.5f),stone);
                Box("BALCONY_S",new Vector3(0,5.9f,-9.75f),new Vector3(26,.2f,2.5f),stone);
                Box("BALCONY_W",new Vector3(-11.75f,5.9f,0),new Vector3(2.5f,.2f,17),stone);
                Box("BALCONY_E",new Vector3(11.75f,5.9f,0),new Vector3(2.5f,.2f,17),stone);
                Rail("BAL_N",new Vector3(0,6.55f,8.5f),new Vector3(21,1.1f,.1f));
                Rail("BAL_S",new Vector3(0,6.55f,-8.5f),new Vector3(21,1.1f,.1f));
                Rail("BAL_W",new Vector3(-10.5f,6.55f,0),new Vector3(.1f,1.1f,17));
                Rail("BAL_E_S",new Vector3(10.5f,6.55f,-1.85f),new Vector3(.1f,1.1f,13.3f));
                Rail("BAL_E_N",new Vector3(10.5f,6.55f,7.85f),new Vector3(.1f,1.1f,1.3f));
                // Reversible two-flight geometry proposal inside R_GRAND. 36 real risers, 2.4m width.
                for(int i=0;i<18;i++)
                {
                    float h=(i+1)/6f;
                    Box("STAIR_A_"+i.ToString("D2"),new Vector3(.84f+i*.28f,h/2,2.2f),new Vector3(.28f,h,2.4f),stone);
                    float top=3+(i+1)/6f;
                    Box("STAIR_B_"+i.ToString("D2"),new Vector3(5.60f-i*.28f,(top+3)/2,5.8f),new Vector3(.28f,top-3,2.4f),stone);
                }
                Box("STAIR_MID",new Vector3(6.49f,2.9f,4),new Vector3(1.5f,.2f,6),stone);
                Box("STAIR_TOP",new Vector3(.1f,5.9f,5.8f),new Vector3(1.2f,.2f,2.4f),stone);
                // Upper bridge runs beside flight B rather than over its headroom.
                Box("STAIR_TOP_BRIDGE",new Vector3(5,5.9f,8.3f),new Vector3(11,.2f,2.4f),stone);
                Box("STAIR_TOP_TURN",new Vector3(.1f,5.9f,7.2f),new Vector3(1.2f,.2f,2.8f),stone);
                Box("STAIR_EXIT",new Vector3(9.9f,5.9f,6.4f),new Vector3(1.2f,.2f,3.8f),stone);
                Box("NOTICE",new Vector3(-8,1.65f,7.8f),new Vector3(4,2.4f,.2f),metal);
                // Neutral scale marker only: not a character model or approved silhouette.
                Box("SCALE_175CM",new Vector3(-4,.875f,-5),new Vector3(.4f,1.75f,.3f),metal);
                for(int i=0;i<5;i++)Box("MATERIAL_SAMPLE_"+i,new Vector3(-11,1,-5+i*1.3f),new Vector3(.12f,1.4f,1),new[]{stone,tile,metal,red,water}[i]);
                var camera=new GameObject("MainCamera");camera.tag="MainCamera";camera.AddComponent<Camera>().fieldOfView=65;camera.AddComponent<AudioListener>();
                camera.AddComponent<UniversalAdditionalCameraData>();
                var walker=new GameObject("ArtReview_WalkInstrument");walker.transform.position=new Vector3(0,.04f,-9);
                var capsule=walker.AddComponent<UnityEngine.CharacterController>();capsule.height=1.75f;capsule.radius=.3f;capsule.center=new Vector3(0,.875f,0);capsule.stepOffset=.3f;capsule.skinWidth=.03f;
                camera.transform.SetParent(walker.transform,false);camera.transform.localPosition=new Vector3(0,1.62f,0);
                var controller=walker.AddComponent<ReviewWalkController>();controller.View=camera.transform;controller.ClockSource=session;
                var light=new GameObject("ReviewKeyLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.7f;light.color=new Color(.89f,.94f,1);light.transform.rotation=Quaternion.Euler(55,-25,0);light.shadows=LightShadows.Soft;
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.38f,.40f,.42f);
                // Real interior luminaires: sunlight cannot illuminate through the ceiling.
                for(int i=0;i<3;i++){
                    var interior=new GameObject("LIGHT_HALL_CEILING_0"+(i+1));interior.transform.position=new Vector3(0,8,-7+i*7);
                    ProductionImporter.Identity(interior,"LIGHT_HALL_CEILING_0"+(i+1),"LightingProposal");var lamp=interior.AddComponent<Light>();lamp.type=LightType.Point;lamp.range=22;lamp.intensity=5;lamp.color=new Color(.87f,.93f,1);lamp.shadows=LightShadows.Soft;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Scene save failed");
                ProductionImporter.Track(ProductionImporter.LoadIndex(),"SCN_Mansion_Main",ScenePath,hash,"FunctionalProxy_ArtAndNavNotApproved");
                var buildScenes=EditorBuildSettings.scenes.Where(x=>x.path!=ScenePath).Select(x=>new EditorBuildSettingsScene(x.path,x.enabled&&File.Exists(x.path))).ToList();
                buildScenes.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=buildScenes.ToArray();
                Debug.Log("BASSLINE MainHall proxy generated by Editor API. Navigation, rail continuity, art review NOT RUN.");
            }
            finally{if(!Application.isBatchMode){EditorSceneManager.CloseScene(scene,true);if(previous.IsValid())SceneManager.SetActiveScene(previous);}}
        }
        public static void CaptureReview()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var camera=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Camera>()).Single();
            camera.transform.position=new Vector3(-7,3.8f,-9);camera.transform.LookAt(new Vector3(1,4,3));camera.fieldOfView=75;
            var texture=new RenderTexture(1440,900,24);var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=texture});RenderTexture.active=texture;pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();File.WriteAllBytes("Verification/MainHall_URP.png",pixels.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(texture);}
        }
        static Material Material(string id,Color color,float smooth,float metallic)
        {
            string path="Assets/BASSLINE/Environment/Materials/Proxy/"+id+".mat";
            var index=ProductionImporter.LoadIndex();var hash=AtomicSaveStore.Hash(BuilderVersion+id);
            if(!ProductionImporter.CanWrite(index,id,path,hash))return AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP Lit shader unavailable");
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);bool create=material==null;if(create)material=new Material(shader);
            material.shader=shader;material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",smooth);material.SetFloat("_Metallic",metallic);
            Directory.CreateDirectory(Path.GetDirectoryName(path));if(create)AssetDatabase.CreateAsset(material,path);else EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();ProductionImporter.Track(index,id,path,hash,"MaterialProxy");return material;
        }
        static void Wall(string id,bool alongZ,float offset,float length,float opening,float height)
        {
            float side=(length-opening)/2;
            foreach(int sign in new[]{-1,1})Box("WALL_"+id+"_"+(sign<0?"A":"B"),alongZ?new Vector3(offset,5.5f,sign*(opening/2+side/2)):new Vector3(sign*(opening/2+side/2),5.5f,offset),alongZ?new Vector3(.2f,11,side):new Vector3(side,11,.2f),stone);
            Box("WALL_"+id+"_LINTEL",alongZ?new Vector3(offset,(11+height)/2,0):new Vector3(0,(11+height)/2,offset),alongZ?new Vector3(.2f,11-height,opening):new Vector3(opening,11-height,.2f),stone);
        }
        static void Rail(string id,Vector3 pos,Vector3 size)=>Box(id,pos,size,metal);
        static void Box(string id,Vector3 pos,Vector3 size,Material material)
        {
            var proxy=new GameObject(id+"_Collision");proxy.transform.SetParent(authority,false);proxy.transform.position=pos;
            proxy.AddComponent<BoxCollider>().size=size;ProductionImporter.Identity(proxy,"GEO_"+id,"FixedCollisionAndOcclusion");Visual(id,pos,size,material);
        }
        static void Visual(string id,Vector3 pos,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=id+"_Proxy";go.transform.SetParent(art,false);go.transform.position=pos;go.transform.localScale=size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
}
