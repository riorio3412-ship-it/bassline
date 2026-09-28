using UnityEngine;

namespace BASSLINE.AuthoringData
{
    public sealed class MansionRoom : MonoBehaviour
    {
        public string RoomId, DisplayName, WingId, Floor, GeometryType, ParentRoomId;
        public string LightingProfile, NoiseProfile, MaterialSet, NormalActivities;
        public string SourceStatus = "PRODUCTION PROPOSAL";
        public Bounds Bounds;
        public Vector3 FloorCenter;
        public float CeilingHeight;
        public Vector3 WalkPoint;
        public int WalkNode = -1;
        public Light[] Lights;
        public bool ContainsWalkPoint(Vector3 feet)
        {
            if (feet.x < Bounds.min.x || feet.x > Bounds.max.x || feet.z < Bounds.min.z || feet.z > Bounds.max.z) return false;
            float height = feet.y - FloorCenter.y;
            if (GeometryType == "StairVolume") return height >= -.3f && height <= 18.5f;
            if (RoomId == "R_GRAND") return height >= -.3f && height <= 6.3f;
            if (RoomId == "R_LIBRARY")
            {
                var d = feet - FloorCenter; d.y = 0;
                return d.sqrMagnitude <= 100.01f && height >= -2.3f && height < .65f;
            }
            if (height < -.3f || height > .65f) return false;
            if (GeometryType == "PerimeterRing") return Mathf.Abs(feet.x - FloorCenter.x) >= 10.5f || Mathf.Abs(feet.z - FloorCenter.z) >= 8.5f;
            if (RoomId == "R_POOL") return Mathf.Abs(feet.x - FloorCenter.x) >= 6 || Mathf.Abs(feet.z - FloorCenter.z) >= 8;
            return true;
        }
    }
}
