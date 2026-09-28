// Compile-only stub of the mansion module contract (Documentation/BL23/Contracts.md §3). Not shipped.
using UnityEngine;
namespace BL23.Game.Mansion
{
    public class MansionView : MonoBehaviour
    {
        public static MansionView Build(BL23.Sim.Layout L, Transform parent) => null;
        public Vector3 ToWorld(BL23.Sim.P3 p) => Vector3.zero;
        public void SetDoor(int doorId, bool open, bool locked) { }
        public void SetCircuit(int circuitId, bool on) { }
        public void SetDarkness(float t) { }
        public void SetNoise(float t) { }
        public void SetFurnitureState(int furnitureId, int damage, BL23.Sim.P3 pos, float yaw) { }
        public void SetPress(float ram01, bool powered) { }
        public Transform RoomAnchor(int roomId) => null;
        public Bounds RoomBounds(int roomId) => default;
        public GameObject FurnitureObject(int furnitureId) => null;
    }
    public static class PropFactory { public static GameObject CreateItem(BL23.Sim.ItemDef def, string itemId) => null; }
    public static class TraceFactory { public static GameObject Create(string traceType, Vector3 pos, Vector3 normal, float size, Color tint) => null; }
    public class CourtroomView : MonoBehaviour { public Transform StandAnchor(int seat) => null; public Transform ButlerAnchor; public void SetTension(float t) { } }
    public class PropMaterial : MonoBehaviour { public BL23.Sim.Mat Mat; public static event System.Action<PropMaterial, int, Vector3, float> OnDamaged; }
}
