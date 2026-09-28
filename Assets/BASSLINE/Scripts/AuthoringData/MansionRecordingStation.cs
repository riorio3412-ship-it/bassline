using UnityEngine;
namespace BASSLINE.AuthoringData
{
    public sealed class MansionRecordingStation:MonoBehaviour
    {
        public const string WorkshopCamera="M_WORK_CAMERA",WorkshopReader="M_WORK_RECORDINGS";
        public string SourceId=WorkshopCamera,ReaderId=WorkshopReader,RoomId="R_WORK";
        public string ReaderNode="";
        public Transform Lens;
        public float Range=7,FieldOfView=85,IdentityRange=5;
        // Legacy serialized shape only; never instantiate a surveillance device.
        void Awake()=>gameObject.SetActive(false);
    }
}
