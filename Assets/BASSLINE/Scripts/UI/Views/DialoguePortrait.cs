using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BASSLINE.UI
{
    // Authored 2D standing when available; otherwise a temporary capture of the encountered actor.
    // No actor behaviour, collider, identity discovery, or world-state accessor is cloned.
    public sealed class DialoguePortrait:MonoBehaviour
    {
        Texture2D texture;bool ownsTexture;UnityEngine.UI.RawImage image;Transform captured;bool capturedAuthored;
        Coroutine captureRoutine;GameObject preview;readonly List<Mesh> temporaryMeshes=new List<Mesh>();
        string diagnostics="";
        public void Show(Transform actor,UnityEngine.UI.Image slot,bool useAuthored=true)
        {
            if(!actor||!slot)return;
            if(image&&image.transform.parent!=slot.transform){image.transform.SetParent(slot.transform,false);slot.enabled=false;}
            if(captured==actor&&capturedAuthored==useAuthored&&(texture||captureRoutine!=null))return;
            if(captureRoutine!=null){StopCoroutine(captureRoutine);captureRoutine=null;}ClearPreview();captured=actor;capturedAuthored=useAuthored;
            if(!image)
            {
                // Image and RawImage both derive from Graphic and cannot share a GameObject.
                var surface=new GameObject("Standing render",typeof(RectTransform),typeof(UnityEngine.UI.RawImage));
                surface.transform.SetParent(slot.transform,false);image=surface.GetComponent<UnityEngine.UI.RawImage>();image.raycastTarget=false;
                var rect=image.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                var aspect=surface.AddComponent<UnityEngine.UI.AspectRatioFitter>();aspect.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=512f/768;
            }
            var identity=actor.GetComponent<BASSLINE.AuthoringData.FixtureTarget>();
            var standing=identity&&useAuthored?Resources.Load<Texture2D>("BASSLINE/Standing/"+(identity.StableId=="CH_02"?"CH_02_v019":identity.StableId)):null;
            ReleaseTexture();
            if(standing){
                texture=standing;ownsTexture=false;image.texture=texture;image.enabled=true;slot.enabled=false;
                // Presentation crop only: keep the authored texture untouched and frame the upper body.
                image.uvRect=new Rect(0,.32f,1,.68f);
                image.GetComponent<UnityEngine.UI.AspectRatioFitter>().aspectRatio=texture.width/(texture.height*.68f);
                diagnostics="Authored2D="+identity.StableId+" size="+texture.width+"x"+texture.height;return;
            }
            image.uvRect=new Rect(0,0,1,1);image.GetComponent<UnityEngine.UI.AspectRatioFitter>().aspectRatio=512f/768;
            captureRoutine=StartCoroutine(Capture(actor,slot,!useAuthored));
        }
        IEnumerator Capture(Transform actor,UnityEngine.UI.Image slot,bool canonical)
        {
            preview=new GameObject("Standing model preview");
            Vector3 offset=new Vector3(10000,10000,10000);Bounds bounds=new Bounds();bool any=false;
            Transform source=actor;
            if(canonical){
                // Profiles show the known model in a neutral pose, never the remote actor's
                // current death pose, active state, carried objects, or live animation.
                var visual=actor.Find("CharacterProxy");
                if(!visual){image.enabled=false;ClearPreview();captureRoutine=null;yield break;}
                source=Instantiate(visual.gameObject,preview.transform).transform;source.gameObject.SetActive(true);
                source.SetPositionAndRotation(offset,Quaternion.identity);
                foreach(string name in new[]{"LegLeft","LegRight"}){var leg=source.Find(name);if(leg)leg.localRotation=Quaternion.identity;}
                foreach(var arm in source.GetComponentsInChildren<BASSLINE.AuthoringData.ActorArmRig>(true)){
                    float height=(arm.UpperLength+arm.ForearmLength)/.295f;
                    arm.Pose(source.TransformPoint(new Vector3(arm.Side*.255f,height*.47f,.07f)),source.rotation);
                    arm.Curl(0);
                }
            }
            var renderers=source.GetComponentsInChildren<Renderer>();
            Camera previewCamera=null;RenderTexture target=null;int renderedFrames=0;
            System.Action<ScriptableRenderContext,Camera> rendered=(context,cam)=>{if(cam==previewCamera)renderedFrames++;};
            RenderPipelineManager.endCameraRendering+=rendered;
            diagnostics="Actor="+actor.name+" sourceRenderers="+renderers.Length;
            try
            {
                foreach(var renderer in renderers)
                {
                    if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                    Mesh mesh=null;
                    if(renderer is SkinnedMeshRenderer skinned){mesh=new Mesh();skinned.BakeMesh(mesh);temporaryMeshes.Add(mesh);}
                    else if(renderer is MeshRenderer){var filter=renderer.GetComponent<MeshFilter>();if(filter)mesh=filter.sharedMesh;}
                    if(!mesh)continue;
                    if(!any){bounds=renderer.bounds;any=true;}else bounds.Encapsulate(renderer.bounds);
                    var go=new GameObject("Standing surface",typeof(MeshFilter),typeof(MeshRenderer));go.layer=30;go.transform.SetParent(preview.transform,false);
                    go.transform.SetPositionAndRotation(renderer.transform.position+offset,renderer.transform.rotation);go.transform.localScale=renderer.transform.lossyScale;
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
                }
                if(!any){image.enabled=false;yield break;}
                target=new RenderTexture(512,768,24,RenderTextureFormat.ARGB32){name="Dialogue standing capture"};target.Create();
                var cameraObject=new GameObject("Standing camera",typeof(Camera));cameraObject.transform.SetParent(preview.transform,false);var camera=cameraObject.GetComponent<Camera>();camera.depth=-100;
                previewCamera=camera;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<30;camera.orthographic=true;camera.useOcclusionCulling=false;
                camera.orthographicSize=Mathf.Max(bounds.extents.y*1.1f,bounds.extents.x*1.7f);camera.nearClipPlane=.05f;camera.farClipPlane=20;
                Vector3 center=bounds.center+offset;camera.transform.position=center+(canonical?Vector3.forward:actor.forward)*5;camera.transform.LookAt(center);camera.targetTexture=target;
                diagnostics+=" bounds="+bounds+" camera="+camera.transform.position+" rotation="+camera.transform.eulerAngles+" ortho="+camera.orthographicSize+" active="+camera.isActiveAndEnabled;
                foreach(var surface in preview.GetComponentsInChildren<MeshRenderer>())diagnostics+=" surface="+surface.bounds+" viewport="+camera.WorldToViewportPoint(surface.bounds.center)+" shader="+surface.sharedMaterial.shader.name+" supported="+surface.sharedMaterial.shader.isSupported;
                var lightObject=new GameObject("Standing light",typeof(Light));lightObject.transform.SetParent(preview.transform,false);var light=lightObject.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(25,215,0);
                image.enabled=false;slot.enabled=false;
                // Newly created surfaces enter the renderer on the next frame. Keep the isolated
                // camera alive through actual SRP frames before disposing its visible-model copy.
                yield return null;
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                // Keep a complete frame independently of the temporary SRP camera/target lifetime.
                // A retained RenderTexture was cleared after camera teardown on the Windows player.
                var snapshot=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false){name="Dialogue standing proxy",wrapMode=TextureWrapMode.Clamp};
                var previous=RenderTexture.active;
                try{RenderTexture.active=target;snapshot.ReadPixels(new Rect(0,0,target.width,target.height),0,0);snapshot.Apply();}
                catch{Destroy(snapshot);throw;}
                finally{RenderTexture.active=previous;}
                ReleaseTexture();texture=snapshot;ownsTexture=true;image.texture=texture;image.enabled=true;
                if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-bassline-mansion-smoke")>=0)
                {int filled=0;foreach(var pixel in texture.GetPixels32())if(pixel.a>0||pixel.r>0||pixel.g>0||pixel.b>0)filled++;diagnostics+=" capturedPixels="+filled;}
            }
            finally{RenderPipelineManager.endCameraRendering-=rendered;diagnostics+=" frames="+renderedFrames+" snapshotCreated="+(texture!=null);if(previewCamera)previewCamera.targetTexture=null;ClearPreview();if(target){target.Release();Destroy(target);}captureRoutine=null;}
        }
        void ClearPreview(){if(preview){preview.SetActive(false);Destroy(preview);preview=null;}foreach(var mesh in temporaryMeshes)if(mesh)Destroy(mesh);temporaryMeshes.Clear();}
        void ReleaseTexture(){if(image)image.texture=null;if(texture&&ownsTexture)Destroy(texture);texture=null;ownsTexture=false;}
        void OnDestroy(){if(captureRoutine!=null)StopCoroutine(captureRoutine);ClearPreview();ReleaseTexture();}
    }
}
