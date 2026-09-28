using UnityEngine;

namespace BASSLINE.AuthoringData
{
    public sealed class MansionAnchor : MonoBehaviour
    {
        public string AnchorId, RoomId, InteractionType, FurnitureId;
        public int ParticipantSlots = 1;
        public float ApproachClearance = 1.2f;
        public Vector3 ApproachPoint;
        public int ApproachNode = -1;
        public Transform LookTarget, LeftHand, RightHand, LeftFoot, RightFoot;
        public bool SocketsResolved;
        public string DefinitionRevision = "M01_FUNCTIONAL_01";
    }
}
