using UnityEngine;

namespace BASSLINE.AuthoringData
{
    public sealed class MansionConnection : MonoBehaviour
    {
        public string ConnectionId, DoorId, RoomA, RoomB, Kind, InitialLock, InitialOpen, PassageToken;
        public bool EmergencyOpen;
        public float Width, ClearHeight;
        public Vector3 NormalAToB;
        public Vector3[] Route;
        public int EntryNodeA = -1, EntryNodeB = -1;
        public Transform LeafPivot, SecondLeafPivot;
        public Collider LeafCollider, SecondLeafCollider;
        public Quaternion ClosedRotation, SecondClosedRotation;
        public float OpenAngle = 100;
        [Range(0, 1)] public float OpenAmount;

        // Presentation only: runtime authority must approve an open command before calling this.
        public void ApplyOpenAmount(float amount)
        {
            OpenAmount = Mathf.Clamp01(amount);
            if (LeafPivot != null) LeafPivot.localRotation = ClosedRotation * Quaternion.Euler(0, -OpenAngle * OpenAmount, 0);
            if (SecondLeafPivot != null) SecondLeafPivot.localRotation = SecondClosedRotation * Quaternion.Euler(0, OpenAngle * OpenAmount, 0);
        }
        public void SetLeafCollision(bool enabled)
        {
            if (LeafCollider != null) LeafCollider.enabled = enabled;
            if (SecondLeafCollider != null) SecondLeafCollider.enabled = enabled;
        }
    }
}
