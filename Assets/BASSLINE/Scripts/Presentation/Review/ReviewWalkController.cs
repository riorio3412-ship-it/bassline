using UnityEngine;
using BASSLINE.Core;
using BASSLINE.UI;
namespace BASSLINE.Presentation
{
    // ArtReview instrument only. This does not mutate any production character or room state.
    [RequireComponent(typeof(UnityEngine.CharacterController))]
    public sealed class ReviewWalkController:MonoBehaviour
    {
        public MonoBehaviour ClockSource;
        public Transform View;
        public float Speed=3;
        public bool ReadInput=true;
        UnityEngine.CharacterController capsule;
        float verticalVelocity,pitch;
        bool paused;PlayerControls controls;
        void Awake(){capsule=GetComponent<UnityEngine.CharacterController>();controls=PlayerControls.Load();}
        void Start(){SetReviewPause(false);}
        void OnDestroy(){if(paused&&ClockSource&&ClockSource is IReadPausePort p)p.ReleaseReadPause("HALL_REVIEW_PAUSE");Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        void OnApplicationFocus(bool focused){if(!Application.isBatchMode&&!focused)SetReviewPause(true);}
        public void SetReviewPause(bool value){if(value!=paused&&ClockSource is IReadPausePort p){if(value)p.AcquireReadPause("HALL_REVIEW_PAUSE");else p.ReleaseReadPause("HALL_REVIEW_PAUSE");}paused=value;Cursor.lockState=paused?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=paused;}
        public void ApplyMouseDelta(float x,float y){if(paused||ClockSource is IClockView c&&c.WorldPaused||!View)return;transform.Rotate(0,x*controls.Sensitivity,0);pitch=Mathf.Clamp(pitch+y*controls.Sensitivity*(controls.InvertY?1:-1),-80,80);View.localRotation=Quaternion.Euler(pitch,0,0);}
        void Update()
        {
            if(!ReadInput)return;
            if(Input.GetKeyDown(KeyCode.Escape)){SetReviewPause(!paused);return;}
            if(paused)return;
            if(ClockSource is IClockView clock&&clock.WorldPaused)return;
            float x=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);
            float z=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);
            if(Cursor.lockState==CursorLockMode.Locked)ApplyMouseDelta(Input.GetAxisRaw("Mouse X"),Input.GetAxisRaw("Mouse Y"));else if(Input.GetMouseButtonDown(0))SetReviewPause(false);
            MoveForReview(transform.TransformDirection(new Vector3(x,0,z)),Time.deltaTime);
        }
        public void MoveForReview(Vector3 horizontal,float seconds)
        {
            if(seconds<=0||seconds>.1f||float.IsNaN(seconds))return;
            if(capsule.isGrounded&&verticalVelocity<0)verticalVelocity=-2;
            verticalVelocity-=9.81f*seconds;
            capsule.Move((Vector3.ClampMagnitude(new Vector3(horizontal.x,0,horizontal.z),1)*Speed+Vector3.up*verticalVelocity)*seconds);
        }
    }
}
