using UnityEngine;

namespace BASSLINE.UI
{
    public static class PlayerPresentationSettings
    {
        public static void Apply(PlayerControls controls,Transform uiRoot)
        {
            controls.Validate();AudioListener.volume=controls.Volume;QualitySettings.vSyncCount=controls.VSync;
            if(uiRoot)
            {
                var scaler=uiRoot.GetComponentInParent<UnityEngine.UI.CanvasScaler>();
                if(scaler&&scaler.uiScaleMode==UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize)scaler.referenceResolution=new Vector2(1920,1080)/controls.UiScale;
            }
            foreach(var channel in Object.FindObjectsByType<PlayerAudioChannel>(FindObjectsInactive.Include))channel.Apply(controls);
            // Editor Game View size is controlled by the Editor; apply mode only in the player.
            if(controls.HasDisplayOverride&&!Application.isEditor&&!Application.isBatchMode)
            {
                int width=controls.ResolutionWidth>0?controls.ResolutionWidth:Screen.width;
                int height=controls.ResolutionHeight>0?controls.ResolutionHeight:Screen.height;
                var mode=controls.Fullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;
                if(Screen.width!=width||Screen.height!=height||Screen.fullScreenMode!=mode)Screen.SetResolution(width,height,mode);
            }
        }
        public static void UpdateCamera(Camera camera,PlayerControls controls,bool running,float unscaledDelta)
        {
            if(!camera||controls==null)return;
            float target=controls.Fov+(running&&controls.RunFovEffect&&!controls.ReduceMotion?controls.RunFovAmount:0);
            camera.fieldOfView=controls.ReduceMotion?target:Mathf.MoveTowards(camera.fieldOfView,target,45*Mathf.Clamp(unscaledDelta,0,.1f));
        }
    }
}
