using UnityEngine;
using UnityEngine.Rendering;
namespace BASSLINE.AuthoringData
{
    // Deity-only observation. No FixtureTarget, knowledge writer, media, or NPC API.
    [DisallowMultipleComponent]
    public sealed class MansionDivineEye:MonoBehaviour
    {
        [Tooltip("Enable only for a deliberately authored dark corner.")]
        public bool AuthoredDarkCorner;
        public Renderer[] ClosedVisuals=System.Array.Empty<Renderer>();
        public Renderer[] OpenVisuals=System.Array.Empty<Renderer>();
        [Min(.01f)] public float ApertureRadius=.12f;
        [Min(0)] public float UnseenDelay=.2f;
        Camera playerView;
        Transform playerRoot;
        float unseenSeconds;
        bool observing;
        readonly RaycastHit[] sightHits=new RaycastHit[64];
        static readonly Vector2[] apertureSamples={Vector2.zero,Vector2.left,Vector2.right,Vector2.up,Vector2.down,new Vector2(-.7f,-.7f),new Vector2(-.7f,.7f),new Vector2(.7f,-.7f),new Vector2(.7f,.7f)};

        public void BindPlayerView(Camera view,Transform player)
        {
            playerView=view;playerRoot=player;unseenSeconds=0;SetObserving(false);
        }
        void OnEnable()
        {
            unseenSeconds=0;SetObserving(false);
            Camera.onPreCull+=BeforeCamera;
            RenderPipelineManager.beginCameraRendering+=BeforeSrpCamera;
        }
        void OnDisable()
        {
            Camera.onPreCull-=BeforeCamera;
            RenderPipelineManager.beginCameraRendering-=BeforeSrpCamera;
            unseenSeconds=0;SetObserving(false);
        }
        void LateUpdate()
        {
            if(!CanObserve()||PlayerCanSeeAperture()) {unseenSeconds=0;SetObserving(false);return;}
            unseenSeconds+=Time.deltaTime;
            SetObserving(unseenSeconds>=Mathf.Max(0,UnseenDelay));
        }
        bool CanObserve()=>AuthoredDarkCorner&&playerView&&playerView.isActiveAndEnabled&&playerRoot;
        void BeforeSrpCamera(ScriptableRenderContext context,Camera view)=>BeforeCamera(view);
        void BeforeCamera(Camera view)
        {
            // Recheck after player look has updated, before rendering: no one-frame open eye.
            // Scene/debug/other cameras cannot activate or close an eye on the player's behalf.
            if(view!=playerView)return;
            if(!CanObserve()||PlayerCanSeeAperture()){unseenSeconds=0;SetObserving(false);}
        }
        bool PlayerCanSeeAperture()
        {
            Vector3 origin=playerView.transform.position;
            foreach(var offset in apertureSamples){
                Vector3 point=transform.position+(transform.right*offset.x+transform.up*offset.y)*Mathf.Max(.01f,ApertureRadius);
                Vector3 viewport=playerView.WorldToViewportPoint(point);
                // Slightly conservative border keeps partial glances from showing activation.
                if(viewport.z<=0||viewport.z>playerView.farClipPlane||viewport.x<-.025f||viewport.x>1.025f||viewport.y<-.025f||viewport.y>1.025f)continue;
                Vector3 delta=point-origin;
                if(delta.sqrMagnitude<.0001f)return true;
                int count=Physics.RaycastNonAlloc(origin,delta.normalized,sightHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
                if(count==sightHits.Length)return true; // Uncertain visibility defaults to closed.
                bool blocked=false;
                for(int i=0;i<count;i++){
                    Transform hit=sightHits[i].transform;
                    if(hit.IsChildOf(transform)||hit.IsChildOf(playerRoot))continue;
                    blocked=true;break;
                }
                if(!blocked)return true;
            }
            return false;
        }
        void SetObserving(bool active)
        {
            observing=active;
            foreach(var visual in OpenVisuals)if(visual)visual.enabled=observing;
            foreach(var visual in ClosedVisuals)if(visual)visual.enabled=!observing;
        }
    }
}
