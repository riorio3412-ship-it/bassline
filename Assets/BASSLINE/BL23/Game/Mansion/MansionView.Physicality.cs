using UnityEngine;

namespace BL23.Game.Mansion
{
    public sealed partial class MansionView
    {
        /// <summary>Keep moved furniture in its actual room's visibility and sleeping lists without rebuilding its body.</summary>
        public void RehomePhysicalFurniture(int furnitureId, int roomId)
        {
            if (Rooms == null || !FurnitureGo.TryGetValue(furnitureId, out var go) || go == null) return;
            var destination = roomId >= 0 && roomId < Rooms.Length ? Rooms[roomId] : null;
            var nextParent = destination != null ? destination.Root : _root;
            if (go.transform.parent == nextParent) return;
            RoomView origin = null;
            foreach (var room in Rooms) if (room != null && go.transform.parent == room.Root) { origin = room; break; }
            var body = go.GetComponent<Rigidbody>();
            bool trackedBody = body != null && origin != null && origin.PhysProps.Remove(body);
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                bool wasRoomHidden = origin != null && !origin.Visible;
                origin?.Renderers.Remove(renderer);
                if (destination != null && !destination.Renderers.Contains(renderer)) destination.Renderers.Add(renderer);
                if (renderer.enabled || wasRoomHidden) renderer.enabled = destination == null || destination.Visible;
            }
            if (origin != null)
            {
                for (int i = origin.Lights.Count - 1; i >= 0; i--)
                {
                    var light = origin.Lights[i];
                    if (light.Light == null || !light.Light.transform.IsChildOf(go.transform)) continue;
                    origin.Lights.RemoveAt(i); light.Room = roomId;
                    destination?.Lights.Add(light);
                }
            }
            if (body != null && trackedBody && destination != null && !destination.PhysProps.Contains(body)) destination.PhysProps.Add(body);
            go.transform.SetParent(nextParent, true);
        }
    }
}
