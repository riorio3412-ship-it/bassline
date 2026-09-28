using System.IO;
using System;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using BASSLINE.AuthoringData;
namespace BASSLINE.Authoring
{
    public static class CharacterSculptReview
    {
        [MenuItem("BASSLINE/Art/Render Taegyeom modelling review")]
        public static void Render()
        { RenderActor(6,1.81f,"Verification/ArtV14","CharacterPresentationBuilder.Sculpt.cs"); }
        [MenuItem("BASSLINE/Art/Render Minseo modelling review")]
        public static void RenderMinseo()
        { RenderActor(18,1.82f,"Verification/ArtV17","CharacterPresentationBuilder.Minseo.cs"); }
        public static void RenderJinwoo()
        { RenderActor(2,1.63f,"Verification/ArtV19","CharacterPresentationBuilder.Jinwoo.cs"); }
        public static void RenderJinwooPolish()
        { RenderActor(2,1.63f,"Verification/ArtV20","CharacterPresentationBuilder.Deformation.cs"); }
        static void RenderActor(int number,float height,string folder,string sourceFile)
        {
            Directory.CreateDirectory(folder);string id="CH"+number.ToString("00");
            bool previousAsync=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            var oldScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode?NewSceneMode.Single:NewSceneMode.Additive);UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            try{
                var actor=new GameObject(id+" actual authored mesh").AddComponent<FixtureActorBody>();actor.ActorId="CH_"+number.ToString("00");actor.Height=height;
                CharacterPresentationBuilder.Build(actor,number);
                using(var hash=SHA256.Create())File.WriteAllText(folder+"/render-source.txt",DateTime.UtcNow.ToString("O")+"\nSculpt SHA256: "+BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes("Assets/BASSLINE/Scripts/Editor/Authoring/"+sourceFile)))+"\n"+string.Join("\n",actor.GetComponentsInChildren<MeshFilter>().Select(f=>f.name+": "+f.sharedMesh.vertexCount+" vertices, "+f.sharedMesh.bounds)));
                if(number==2)using(var hash=SHA256.Create())foreach(string path in new[]{"Assets/BASSLINE/Scripts/Editor/Authoring/CharacterPresentationBuilder.Jinwoo.cs","Assets/BASSLINE/Scripts/Editor/Authoring/CharacterPresentationBuilder.Deformation.cs","Assets/BASSLINE/Scripts/AuthoringData/ActorArmRig.cs"})File.AppendAllText(folder+"/render-source.txt","\n"+path+" SHA256: "+BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))));
                foreach(Transform t in actor.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
                var camera=new GameObject("Review camera").AddComponent<Camera>();camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.20f,.22f,.26f);camera.fieldOfView=32;camera.nearClipPlane=.02f;camera.farClipPlane=20;camera.allowHDR=false;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                var light=new GameObject("Studio key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2.3f;light.transform.rotation=Quaternion.Euler(28,-35,0);light.cullingMask=1<<30;light.shadows=LightShadows.None;
                var fill=new GameObject("Studio fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.8f;fill.transform.rotation=Quaternion.Euler(20,155,0);fill.cullingMask=1<<30;fill.shadows=LightShadows.None;
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.47f,.48f,.51f);
                Capture("front",new Vector3(0,1.10f,3.5f),new Vector3(0,.95f,0),1000,1400);
                Capture("three_quarter",new Vector3(2.1f,1.25f,3),new Vector3(0,.95f,0),1000,1400);
                Capture("profile",new Vector3(3.5f,1.1f,0),new Vector3(0,.95f,0),1000,1400);
                Capture("back",new Vector3(0,1.1f,-3.5f),new Vector3(0,.95f,0),1000,1400);
                Capture("face",new Vector3(.20f,height*.900f,.95f),new Vector3(0,height*.89f,0),1000,1000);
                if(number==18)Capture("watch",new Vector3(-.45f,.98f,.60f),new Vector3(-.26f,.88f,.015f),1000,1000);
                if(number==2){
                    foreach(var arm in actor.GetComponentsInChildren<ActorArmRig>()){
                        arm.Pose(actor.transform.TransformPoint(new Vector3(arm.Side*.22f,height*.72f,.29f)),actor.transform.rotation);arm.Curl(.65f);
                    }
                    Capture("reaching",new Vector3(2.1f,1.25f,3),new Vector3(0,.95f,0),1000,1400);
                    Capture("sleeve",new Vector3(1.0f,1.10f,.80f),new Vector3(.20f,1.02f,.12f),1000,1000);
                    File.AppendAllText(folder+"/render-source.txt","\nSkinned clothing:\n"+string.Join("\n",actor.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.sharedMesh.name+": vertices="+r.sharedMesh.vertexCount+", bones="+r.bones.Length)));
                }
                AssetDatabase.SaveAssets();
                File.WriteAllText(folder+"/scope.txt","Editor render of actual generated "+actor.ActorId+" meshes under explicit studio lighting. Skinned clothing is baked from the current bone pose through Unity BakeMesh for each static capture. Not a 2D concept, gameplay validation or final art approval.");
                void Capture(string name,Vector3 position,Vector3 aim,int width,int height){
                    // Batch authoring has no animation frame between these captures.
                    // Bake each current skin pose through Unity's skinning API so the
                    // review cannot show a cached arm with a newly posed hand.
                    var baked=new System.Collections.Generic.List<GameObject>();
                    var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach(var skin in skins){var mesh=new Mesh();skin.BakeMesh(mesh);var go=new GameObject("Review baked current sleeve",typeof(MeshFilter),typeof(MeshRenderer));go.layer=30;go.transform.SetParent(skin.transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;baked.Add(go);}
                    camera.transform.position=position;camera.transform.LookAt(aim);
                    var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);target.Create();
                    try{RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                        RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                        var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
                        try{texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(folder+"/"+id+"_"+name+".png",texture.EncodeToPNG());}finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);}
                    }finally{target.Release();UnityEngine.Object.DestroyImmediate(target);foreach(var go in baked){UnityEngine.Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.DestroyImmediate(go);}foreach(var skin in skins)skin.enabled=true;}
                }
            }finally{ShaderUtil.allowAsyncCompilation=previousAsync;if(!Application.isBatchMode){EditorSceneManager.CloseScene(scene,true);if(oldScene.IsValid())UnityEngine.SceneManagement.SceneManager.SetActiveScene(oldScene);}}
        }
    }
}
