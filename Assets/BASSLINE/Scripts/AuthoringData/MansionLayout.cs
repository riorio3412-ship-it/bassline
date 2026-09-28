using System;
using System.Collections.Generic;
using UnityEngine;

namespace BASSLINE.AuthoringData
{
    /// <summary>Physical M01 authoring data. This is geometry, never character knowledge.</summary>
    public sealed class MansionLayout : MonoBehaviour
    {
        public string MapVersion = "M01";
        public string BuildVersion;
        public string SourceHash;
        public MansionRoom[] Rooms = Array.Empty<MansionRoom>();
        public MansionConnection[] Connections = Array.Empty<MansionConnection>();
        public MansionAnchor[] Anchors = Array.Empty<MansionAnchor>();
        public MansionNavNode[] NavigationNodes = Array.Empty<MansionNavNode>();
        public MansionNavEdge[] NavigationEdges = Array.Empty<MansionNavEdge>();
        public string[] GeometryNotes = Array.Empty<string>();

        public MansionRoom Room(string id) => Array.Find(Rooms, r => r != null && r.RoomId == id);
        public MansionConnection Connection(string id) => Array.Find(Connections, c => c != null && (c.ConnectionId == id || c.DoorId == id));
        public MansionAnchor Anchor(string id) => Array.Find(Anchors, a => a != null && a.AnchorId == id);

        public MansionRoom RoomAt(Vector3 feet)
        {
            MansionRoom best = null; float smallest = float.MaxValue;
            foreach (var room in Rooms)
            {
                if (room == null || !room.ContainsWalkPoint(feet)) continue;
                float area = room.Bounds.size.x * room.Bounds.size.z;
                if (area < smallest) { smallest = area; best = room; }
            }
            return best;
        }

        // Object observations use the room's air volume, not the narrow feet-on-floor band.
        // Bounds.y is an authoring/navigation envelope (often 12m), so use the actual ceiling.
        public MansionRoom RoomContainingPoint(Vector3 point)
        {
            MansionRoom best=null;float bestFloor=float.NegativeInfinity,area=float.MaxValue;
            foreach(var room in Rooms){
                if(!room)continue;float height=point.y-room.FloorCenter.y;
                float minimum=room.RoomId=="R_LIBRARY"?-2.3f:room.RoomId=="R_POOL"?-2.5f:-.1f;
                float ceiling=room.CeilingHeight>0?room.CeilingHeight:8f;
                if(room.GeometryType=="StairVolume")ceiling=Mathf.Max(ceiling,18.5f);
                if(height<minimum||height>=ceiling)continue;
                if(point.x<room.Bounds.min.x||point.x>room.Bounds.max.x||point.z<room.Bounds.min.z||point.z>room.Bounds.max.z)continue;
                var flat=point-room.FloorCenter;flat.y=0;
                if(room.RoomId=="R_LIBRARY"&&flat.sqrMagnitude>100.01f)continue;
                if(room.GeometryType=="PerimeterRing"&&Mathf.Abs(flat.x)<10.5f&&Mathf.Abs(flat.z)<8.5f)continue;
                float size=room.Bounds.size.x*room.Bounds.size.z;
                if(room.FloorCenter.y>bestFloor||Mathf.Approximately(room.FloorCenter.y,bestFloor)&&size<area){best=room;bestFloor=room.FloorCenter.y;area=size;}
            }return best;
        }

        public int NearestNode(Vector3 point, string roomId = null, float maxDistance = float.PositiveInfinity)
        {
            int index = -1; float best = maxDistance * maxDistance;
            for (int i = 0; i < NavigationNodes.Length; i++)
            {
                var n = NavigationNodes[i];
                if (roomId != null && n.RoomId != roomId) continue;
                float distance = (n.Position - point).sqrMagnitude;
                if (distance < best) { best = distance; index = i; }
            }
            return index;
        }

        /// <summary>Returns ordered node indices. Callers supply their OWN known access policy.</summary>
        public int[] FindPath(int from, int to, Predicate<string> mayUseConnection = null)
        {
            int count = NavigationNodes.Length;
            if (from < 0 || to < 0 || from >= count || to >= count) return Array.Empty<int>();
            var adjacent = new List<MansionNavEdge>[count];
            foreach (var edge in NavigationEdges)
            {
                if (edge.From < 0 || edge.From >= count || edge.To < 0 || edge.To >= count) continue;
                if (adjacent[edge.From] == null) adjacent[edge.From] = new List<MansionNavEdge>();
                adjacent[edge.From].Add(edge);
            }
            var cost = new float[count]; var prior = new int[count];
            for (int i = 0; i < count; i++) { cost[i] = float.PositiveInfinity; prior[i] = -1; }
            var heap = new List<HeapEntry>(); cost[from] = 0; Push(heap, new HeapEntry(from, 0));
            while (heap.Count > 0)
            {
                var entry = Pop(heap); int node = entry.Node;
                if (entry.Cost > cost[node]) continue;
                if (node == to) break;
                if (adjacent[node] == null) continue;
                foreach (var edge in adjacent[node])
                {
                    if (!string.IsNullOrEmpty(edge.ConnectionId) && mayUseConnection != null && !mayUseConnection(edge.ConnectionId)) continue;
                    float next = entry.Cost + edge.Distance;
                    if (next >= cost[edge.To]) continue;
                    cost[edge.To] = next; prior[edge.To] = node; Push(heap, new HeapEntry(edge.To, next));
                }
            }
            if (float.IsPositiveInfinity(cost[to])) return Array.Empty<int>();
            var path = new List<int>(); for (int i = to; i != -1; i = prior[i]) path.Add(i);
            path.Reverse(); return path.ToArray();
        }

        struct HeapEntry { public int Node; public float Cost; public HeapEntry(int n, float c) { Node = n; Cost = c; } }
        static void Push(List<HeapEntry> heap, HeapEntry e)
        {
            heap.Add(e); int i = heap.Count - 1;
            while (i > 0) { int p = (i - 1) / 2; if (heap[p].Cost <= e.Cost) break; heap[i] = heap[p]; i = p; }
            heap[i] = e;
        }
        static HeapEntry Pop(List<HeapEntry> heap)
        {
            var first = heap[0]; var last = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
            if (heap.Count == 0) return first;
            int i = 0;
            while (i * 2 + 1 < heap.Count)
            {
                int c = i * 2 + 1; if (c + 1 < heap.Count && heap[c + 1].Cost < heap[c].Cost) c++;
                if (last.Cost <= heap[c].Cost) break; heap[i] = heap[c]; i = c;
            }
            heap[i] = last; return first;
        }
    }

    [Serializable] public struct MansionNavNode { public string Id; public string RoomId; public Vector3 Position; }
    [Serializable] public struct MansionNavEdge { public int From; public int To; public string ConnectionId; public float Distance; }
}
