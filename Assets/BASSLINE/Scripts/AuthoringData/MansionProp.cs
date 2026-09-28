using UnityEngine;

namespace BASSLINE.AuthoringData
{
    public sealed class MansionProp : MonoBehaviour
    {
        public string ObjectId, RoomId, Function;
        public bool Movable;
        public bool Protected;
        public string AssetStatus = "FunctionalProxy";
        public Vector3 UsePoint;
    }
}
