using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BASSLINE.AuthoringData;
using UnityEngine;

namespace BASSLINE.Authoring
{
    public static partial class MansionBuilder
    {
        const float NavSpacing = .9f;
        const float BodyRadius = .30f;
        const float TallestResidentHeight = 1.87f;
        static readonly Collider[] overlaps = new Collider[128];
        static readonly RaycastHit[] castHits = new RaycastHit[128];
        static List<MansionNavNode> navNodes;
        static List<MansionNavEdge> navEdges;
        static Dictionary<string, List<int>> roomNodes;
        static HashSet<string> edgeKeys;
        static HashSet<int> connectedNodes;
        static List<string> navIssues;
        static string physicalFailure;
        static string blockingDoorLeaf;
        static List<PortalRoomLink> portalRoomLinks;
        static List<RejectedPortalRoomLink> rejectedPortalRoomLinks;
        static List<PortalRefinement> portalRefinements;
        static bool Owned(Collider c) => c != null && c.transform.IsChildOf(layout.transform) && !c.isTrigger;

        static void BuildNavigation()
        {
            navNodes = new List<MansionNavNode>(); navEdges = new List<MansionNavEdge>(); roomNodes = new Dictionary<string, List<int>>(); edgeKeys = new HashSet<string>(); connectedNodes = new HashSet<int>(); navIssues = new List<string>();
            portalRoomLinks = new List<PortalRoomLink>(); rejectedPortalRoomLinks = new List<RejectedPortalRoomLink>();
            portalRefinements = new List<PortalRefinement>();
            foreach (var r in rooms.Values) roomNodes.Add(r.RoomId, new List<int>());
            var initialDoors = connections.Select(c => new NavigationDoorState {
                Connection = c, OpenAmount = c.OpenAmount,
                FirstEnabled = c.LeafCollider != null && c.LeafCollider.enabled,
                SecondEnabled = c.SecondLeafCollider != null && c.SecondLeafCollider.enabled
            }).ToArray();
            try
            {
                // An open leaf remains a real obstacle beside the aperture. Removing it while
                // baking allowed room-side shortcuts through the leaf, notably D_014's dining exit.
                // Door authority still gates traversal at runtime; this graph models passable geometry.
                foreach (var c in connections) { c.SetLeafCollision(true); c.ApplyOpenAmount(1); }
                Physics.SyncTransforms();
                foreach (var room in rooms.Values) BuildRoomGrid(room);
                foreach (var path in stairPaths)
                {
                    int previous = -1; var routeIndices = new List<int>();
                    for (int i = 0; i < path.points.Length; i++)
                    {
                        string nodeRoom = path.room == "R_GRAND" && i == path.points.Length - 1 ? "R_BALCONY" : path.room;
                        int index = AddNode("NAV_" + path.room + "_STAIR_" + stairPaths.IndexOf(path).ToString("D2") + "_" + i.ToString("D3"), nodeRoom, path.points[i]);
                        routeIndices.Add(index);
                        if (!BodyClear(path.points[i])) navIssues.Add("Stair headroom/body obstruction: " + navNodes[index].Id + " at " + path.points[i] + " [" + physicalFailure + "]");
                        if (previous >= 0)
                        {
                            if (!SegmentClear(navNodes[previous].Position, path.points[i], true)) navIssues.Add("Stair segment obstructed: " + navNodes[previous].Id + " -> " + navNodes[index].Id + " [" + physicalFailure + "]");
                            else AddEdge(previous, index, path.room == "R_GRAND" ? "CN_010" : "");
                        }
                        previous = index;
                    }
                    // Connect every clear tread/landing to close same-floor samples. No air shortcuts.
                    foreach (int index in routeIndices) LinkToRoom(index, navNodes[index].RoomId, 1.65f, 8, .24f);
                    if (path.room == "R_GRAND")
                    {
                        var c = connections.Single(x => x.ConnectionId == "CN_010"); c.EntryNodeA = routeIndices[0]; c.EntryNodeB = routeIndices[routeIndices.Count - 1];
                    }
                }
                foreach (var connection in connections)
                {
                    if (connection.Kind == "Stair") continue;
                    if (connection.ConnectionId == "CN_009") connection.Route = new[] { new Vector3(-.95f, 0, 2.2f), new Vector3(-.03f, 0, 2.2f) };

                    var route = connection.Route; int previous = -1;
                    for (int i = 0; i < route.Length; i++)
                    {
                        string nodeRoom = i == route.Length - 1 ? connection.RoomB : connection.RoomA;
                        int index = AddNode("NAV_" + connection.ConnectionId + "_" + i, nodeRoom, route[i]);
                        if (i == 0) connection.EntryNodeA = index;
                        if (i == route.Length - 1) connection.EntryNodeB = index;
                        if (!BodyClear(route[i])) navIssues.Add("Portal approach obstruction: " + connection.ConnectionId + " point " + i + " " + route[i] + " [" + physicalFailure + "]");
                        if (previous >= 0)
                        {
                            if (!SegmentClear(route[i - 1], route[i], true)) navIssues.Add("Portal passage blocked: " + connection.ConnectionId + " [" + physicalFailure + "]");
                            else AddEdge(previous, index, connection.ConnectionId);
                        }
                        if (i == 0 || i == route.Length - 1)
                        {
                            int linked = LinkToRoom(index, nodeRoom, 2.8f, 12, .28f, connection.ConnectionId);
                            if (linked == 0) navIssues.Add("Portal does not reach a room walk surface: " + connection.ConnectionId + " " + nodeRoom);
                        }
                        previous = index;
                    }
                }
                // Subzones share the parent's open physical floor. A room-grid boundary must
                // not turn a patch beside the stairs into an artificial disconnected island.
                foreach (var child in rooms.Values.Where(r => !string.IsNullOrEmpty(r.ParentRoomId)))
                    foreach (int n in roomNodes[child.RoomId].ToArray()) LinkToRoom(n, child.ParentRoomId, 1.35f, 4, .035f);
                RefineDisconnectedPortalRooms();
                // A coarse room grid is insufficient for a hand to reach a real workbench.
                // Add grounded physical approach nodes, keeping the normal collision/link checks.
                foreach(var anchor in anchors.Where(a=>a.InteractionType=="Work"&&!string.IsNullOrEmpty(a.FurnitureId))){
                    if(!BodyClear(anchor.ApproachPoint))throw new InvalidOperationException("Workbench approach blocked: "+anchor.AnchorId+" ["+physicalFailure+"]");
                    int index=AddNode("NAV_"+anchor.AnchorId,anchor.RoomId,anchor.ApproachPoint);
                    if(LinkToRoom(index,anchor.RoomId,1.65f,8,.035f)==0)throw new InvalidOperationException("Workbench approach has no physical route: "+anchor.AnchorId+" at "+anchor.ApproachPoint+" ["+physicalFailure+"]");
                }
                layout.NavigationNodes = navNodes.ToArray(); layout.NavigationEdges = navEdges.ToArray();
                foreach (var room in rooms.Values)
                {
                    int node = ClosestInRoom(room.RoomId, room.WalkPoint);
                    if (node < 0) navIssues.Add("No walk point: " + room.RoomId);
                    else { room.WalkNode = node; room.WalkPoint = navNodes[node].Position; }
                }
                foreach (var anchor in anchors)
                {
                    int nearest = ClosestInRoom(anchor.RoomId, anchor.ApproachPoint);
                    if (nearest < 0) { navIssues.Add("No anchor approach: " + anchor.AnchorId); continue; }
                    anchor.ApproachNode = nearest; anchor.ApproachPoint = navNodes[nearest].Position;
                    if (anchor.InteractionType != "Seat" && anchor.InteractionType != "Rest" && anchor.InteractionType != "SwimLane")
                        anchor.transform.position = anchor.ApproachPoint;
                }
                // The existing portal traversal test covers aperture routes only. Test the
                // separately authored room links too while real door leaves are fully open.
                // As with the existing probe, interactive additive scenes are never modified for testing.
                if (Application.isBatchMode) VerifyOpenDoorRoomLinks();
            }
            finally
            {
                foreach (var state in initialDoors)
                {
                    state.Connection.ApplyOpenAmount(state.OpenAmount);
                    if (state.Connection.LeafCollider != null) state.Connection.LeafCollider.enabled = state.FirstEnabled;
                    if (state.Connection.SecondLeafCollider != null) state.Connection.SecondLeafCollider.enabled = state.SecondEnabled;
                }
                Physics.SyncTransforms();
            }
        }

        static void BuildRoomGrid(MansionRoom room)
        {
            float[] floors = room.RoomId == "R_STAIR" ? new[] { -6f, 0f, 6f, 12f } : room.RoomId == "R_GRAND" ? new[] { 0f, 3f, 6f } : room.RoomId == "R_LIBRARY" ? new[] { 0f, -2f } : new[] { room.FloorCenter.y };
            int nx = Mathf.FloorToInt((room.Bounds.size.x - 1.0f) / NavSpacing) + 1, nz = Mathf.FloorToInt((room.Bounds.size.z - 1.0f) / NavSpacing) + 1;
            // Keep an exact centreline sample for narrow symmetric passages between chairs/planters.
            if (nx % 2 == 0) nx--; if (nz % 2 == 0) nz--;
            // Centre the grid in the room so narrow corridors retain symmetric clearance.
            float x0 = room.FloorCenter.x - (nx - 1) * NavSpacing / 2, z0 = room.FloorCenter.z - (nz - 1) * NavSpacing / 2;
            for (int fi = 0; fi < floors.Length; fi++)
            {
                var grid = new int[nx, nz];
                for (int ix = 0; ix < nx; ix++) for (int iz = 0; iz < nz; iz++) grid[ix, iz] = -1;
                for (int ix = 0; ix < nx; ix++) for (int iz = 0; iz < nz; iz++)
                {
                    var expected = new Vector3(x0 + ix * NavSpacing, floors[fi], z0 + iz * NavSpacing);
                    if (!room.ContainsWalkPoint(expected)) continue;
                    // The six sealed guest alcoves are physical scenery, never new walkable guest rooms.
                    if (room.RoomId == "R_CLOSED" && Mathf.Abs(expected.z - room.FloorCenter.z) > 1.5f) continue;
                    if (!FloorAt(expected, .035f, out var supported)) continue;
                    if (!BodyClear(supported, false)) continue;
                    grid[ix, iz] = AddNode("NAV_" + room.RoomId + "_F" + fi + "_" + ix.ToString("D3") + "_" + iz.ToString("D3"), room.RoomId, supported);
                }
                for (int ix = 0; ix < nx; ix++) for (int iz = 0; iz < nz; iz++)
                {
                    int a = grid[ix, iz]; if (a < 0) continue;
                    foreach (var delta in new[] { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, -1) })
                    {
                        int x = ix + delta.x, z = iz + delta.y;
                        if (x >= nx || z < 0 || z >= nz) continue; int b = grid[x, z]; if (b < 0) continue;
                        if (SegmentClear(navNodes[a].Position, navNodes[b].Position, true)) AddEdge(a, b, "");
                    }
                }
            }
        }

        static void RefineDisconnectedPortalRooms()
        {
            // A valid portal link can still lead into an isolated pocket: the .9m room
            // grid can miss the narrow clear turn between an open leaf and furniture.
            // Refine only disconnected room interiors, not every room in the mansion.
            var adjacency = new List<int>[navNodes.Count];
            foreach (var edge in navEdges)
            {
                if (adjacency[edge.From] == null) adjacency[edge.From] = new List<int>();
                adjacency[edge.From].Add(edge.To);
            }
            var reachable = new HashSet<int>(); var pending = new Queue<int>();
            int start = ClosestInRoom("R_HALL", rooms["R_HALL"].WalkPoint);
            if (start >= 0) { reachable.Add(start); pending.Enqueue(start); }
            while (pending.Count > 0)
            {
                int current = pending.Dequeue(); if (adjacency[current] == null) continue;
                foreach (int next in adjacency[current]) if (reachable.Add(next)) pending.Enqueue(next);
            }
            foreach (var room in rooms.Values.OrderBy(r => r.RoomId, StringComparer.Ordinal))
            {
                int main = ClosestInRoom(room.RoomId, room.WalkPoint);
                if (main >= 0 && reachable.Contains(main)) continue;
                foreach (var connection in connections.Where(c => c.Kind == "Door" && (c.RoomA == room.RoomId || c.RoomB == room.RoomId)))
                {
                    bool sideA = connection.RoomA == room.RoomId;
                    int endpoint = sideA ? connection.EntryNodeA : connection.EntryNodeB;
                    if (endpoint >= 0) RefinePortalRoom(room, connection, endpoint, sideA ? "A" : "B");
                }
            }
        }

        static void RefinePortalRoom(MansionRoom room, MansionConnection connection, int endpoint, string side)
        {
            const float spacing = .3f; const int halfCount = 9; const float joinRange = 1.35f;
            int nodesBefore = navNodes.Count, edgesBefore = navEdges.Count;
            Vector3 origin = navNodes[endpoint].Position;
            // Do not let nearest fine nodes crowd out links back to the original room grid.
            // Exclude other portal nodes so refinement cannot add an untagged door crossing.
            var existing = roomNodes[room.RoomId].Where(n => !navNodes[n].Id.StartsWith("NAV_CN_", StringComparison.Ordinal)).ToArray();
            int count = halfCount * 2 + 1; var grid = new int[count, count];
            for (int x = 0; x < count; x++) for (int z = 0; z < count; z++) grid[x, z] = -1;
            for (int x = 0; x < count; x++) for (int z = 0; z < count; z++)
            {
                var expected = origin + new Vector3((x - halfCount) * spacing, 0, (z - halfCount) * spacing);
                if (!room.ContainsWalkPoint(expected)) continue;
                if (room.RoomId == "R_CLOSED" && Mathf.Abs(expected.z - room.FloorCenter.z) > 1.5f) continue;
                if (!FloorAt(expected, .035f, out var supported) || !BodyClear(supported, false)) continue;
                grid[x, z] = AddNode("NAV_" + room.RoomId + "_DOOR_FINE_" + connection.ConnectionId + "_" + side + "_" + x.ToString("D2") + "_" + z.ToString("D2"), room.RoomId, supported);
            }
            for (int x = 0; x < count; x++) for (int z = 0; z < count; z++)
            {
                int node = grid[x, z]; if (node < 0) continue;
                foreach (var offset in new[] { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, -1) })
                {
                    int nx = x + offset.x, nz = z + offset.y;
                    if (nx >= count || nz < 0 || nz >= count || grid[nx, nz] < 0) continue;
                    int next = grid[nx, nz];
                    if (SegmentClear(navNodes[node].Position, navNodes[next].Position, true)) AddEdge(node, next, "");
                }
                Vector3 p = navNodes[node].Position; int joined = 0;
                foreach (int original in existing.Where(n => Mathf.Abs(navNodes[n].Position.y - p.y) <= .035f &&
                    (navNodes[n].Position - p).sqrMagnitude <= joinRange * joinRange).OrderBy(n => (navNodes[n].Position - p).sqrMagnitude))
                {
                    if (!SegmentClear(p, navNodes[original].Position, true)) continue;
                    AddEdge(node, original, ""); if (++joined >= 6) break;
                }
            }
            int linked = LinkToRoom(endpoint, room.RoomId, 1.1f, 12, .035f, connection.ConnectionId);
            if (linked > 0) navIssues.Remove("Portal does not reach a room walk surface: " + connection.ConnectionId + " " + room.RoomId);
            portalRefinements.Add(new PortalRefinement { ConnectionId = connection.ConnectionId, RoomId = room.RoomId,
                Endpoint = navNodes[endpoint].Id, Origin = origin, Spacing = spacing, HalfExtent = halfCount * spacing,
                AddedNodes = navNodes.Count - nodesBefore, AddedDirectedEdges = navEdges.Count - edgesBefore, PortalLinks = linked });
        }

        [Serializable] sealed class PortalRefinement
        {
            public string ConnectionId, RoomId, Endpoint; public Vector3 Origin;
            public float Spacing, HalfExtent; public int AddedNodes, AddedDirectedEdges, PortalLinks;
        }
        static int AddNode(string id, string room, Vector3 point)
        {
            int index = navNodes.Count; navNodes.Add(new MansionNavNode { Id = id, RoomId = room, Position = point }); roomNodes[room].Add(index); return index;
        }
        static void AddEdge(int a, int b, string connection)
        {
            if (a == b) return; string key = Mathf.Min(a, b) + ":" + Mathf.Max(a, b) + ":" + connection;
            if (!edgeKeys.Add(key)) return; float distance = Mathf.Max(.001f, Vector3.Distance(navNodes[a].Position, navNodes[b].Position));
            connectedNodes.Add(a); connectedNodes.Add(b);
            navEdges.Add(new MansionNavEdge { From = a, To = b, ConnectionId = connection, Distance = distance });
            navEdges.Add(new MansionNavEdge { From = b, To = a, ConnectionId = connection, Distance = distance });
        }
        static int LinkToRoom(int index, string room, float range, int maximum, float verticalTolerance, string portalConnection = null)
        {
            Vector3 p = navNodes[index].Position; int count = 0;
            foreach (int other in roomNodes[room].Where(i => i != index && Mathf.Abs(navNodes[i].Position.y - p.y) <= verticalTolerance && (navNodes[i].Position - p).sqrMagnitude <= range * range).OrderBy(i => (navNodes[i].Position - p).sqrMagnitude).ToArray())
            {
                if (!SegmentClear(p, navNodes[other].Position, true))
                {
                    if (portalConnection != null && !string.IsNullOrEmpty(blockingDoorLeaf))
                        rejectedPortalRoomLinks.Add(new RejectedPortalRoomLink { ConnectionId = portalConnection,
                            From = navNodes[index].Id, To = navNodes[other].Id, BlockingDoorId = blockingDoorLeaf,
                            FromPosition = p, ToPosition = navNodes[other].Position, Collider = physicalFailure });
                    continue;
                }
                if (portalConnection != null) portalRoomLinks.Add(new PortalRoomLink { ConnectionId = portalConnection, From = index, To = other });
                AddEdge(index, other, ""); if (++count >= maximum) break;
            }
            return count;
        }
        static int ClosestInRoom(string room, Vector3 target)
        {
            int best = -1; float distance = float.MaxValue;
            foreach (int n in roomNodes[room])
            {
                float d = (navNodes[n].Position - target).sqrMagnitude;
                if (d >= distance || !BodyClear(navNodes[n].Position)) continue;
                // Do not select a disconnected isolated sample just because it is closest.
                if (!connectedNodes.Contains(n)) continue;
                distance = d; best = n;
            }
            return best;
        }
        static bool FloorAt(Vector3 expected, float tolerance, out Vector3 ground)
        {
            ground = expected;
            int count = Physics.RaycastNonAlloc(expected + Vector3.up * .55f, Vector3.down, castHits, 1.2f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity; RaycastHit best = default;
            for (int i = 0; i < count; i++) if (Owned(castHits[i].collider) && castHits[i].distance < nearest) { nearest = castHits[i].distance; best = castHits[i]; }
            if (float.IsPositiveInfinity(nearest) || best.normal.y < .65f || Mathf.Abs(best.point.y - expected.y) > tolerance) { physicalFailure = "Unsupported step at " + expected + (best.collider == null ? " no floor" : " hits " + best.collider.name + " y=" + best.point.y); return false; }
            ground.y = best.point.y; return true;
        }
        static bool BodyClear(Vector3 feet, bool allowStep = true)
        {
            // Below 0.32m is the validated CharacterController step envelope, not invisible clearance.
            int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * (allowStep ? .63f : .32f), feet + Vector3.up * (TallestResidentHeight - BodyRadius), BodyRadius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (Owned(overlaps[i])) { RecordObstruction(overlaps[i]); return false; }
            return count < overlaps.Length;
        }
        static bool SegmentClear(Vector3 from, Vector3 to, bool requireGround)
        {
            blockingDoorLeaf = null;
            Vector3 delta = to - from; float distance = delta.magnitude;
            bool stepping = Mathf.Abs(delta.y) > .075f;
            if (!BodyClear(from, stepping) || !BodyClear(to, stepping)) return false;
            if (distance > .005f)
            {
                int count = Physics.CapsuleCastNonAlloc(from + Vector3.up * (stepping ? .63f : .32f), from + Vector3.up * (TallestResidentHeight - BodyRadius), BodyRadius, delta / distance, castHits, distance, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++) if (Owned(castHits[i].collider)) { RecordObstruction(castHits[i].collider); return false; }
                if (count >= castHits.Length) return false;
            }
            if (requireGround)
            {
                int samples = Mathf.Max(1, Mathf.CeilToInt(distance / .35f));
                for (int i = 0; i <= samples; i++) if (!FloorAt(Vector3.Lerp(from, to, (float)i / samples), .28f, out _)) return false;
            }
            return true;
        }

        static void RecordObstruction(Collider collider)
        {
            var connection = collider.GetComponentInParent<MansionConnection>();
            if (connection != null && (collider == connection.LeafCollider || collider == connection.SecondLeafCollider))
                blockingDoorLeaf = connection.DoorId;
            physicalFailure = (connection == null ? "" : connection.ConnectionId + "/") + collider.transform.parent.name + "/" + collider.name;
        }

        sealed class NavigationDoorState
        {
            public MansionConnection Connection; public float OpenAmount; public bool FirstEnabled, SecondEnabled;
        }
        sealed class PortalRoomLink { public string ConnectionId; public int From, To; }
        [Serializable] sealed class RejectedPortalRoomLink
        {
            public string ConnectionId, From, To, BlockingDoorId, Collider; public Vector3 FromPosition, ToPosition;
        }
        [Serializable] sealed class PortalRoomLinkFailure
        {
            public string ConnectionId, From, To, Direction, Collider, ControllerFlags; public float Speed, ControllerHeight;
            public Vector3 Expected, Actual;
        }
        [Serializable] sealed class PortalRoomLinkReceipt
        {
            public string Builder, Editor, Scope, Result;
            public int OpenDoors, PortalEnds, LinkedPortalEnds, UndirectedRoomLinks, AdditionalLibraryApertureSegments, Probes, Passed, Moves;
            public float Height = TallestResidentHeight, Radius = .28f, StepOffset = .32f, ArrivalTolerance = .14f;
            public float[] LibraryControllerHeights;
            public RejectedPortalRoomLink[] RejectedOpenLeafLinks;
            public PortalRoomLinkFailure[] Failures;
        }
        static void VerifyOpenDoorRoomLinks()
        {
            var report = new PortalRoomLinkReceipt {
                Builder = BuilderVersion, Editor = Application.unityVersion,
                Scope = "TestOnly isolated Editor CharacterController checks of every accepted portal endpoint-to-room graph link, with all actual door leaves enabled and fully open. Runs before runtime actors/portable objects are placed. Not dynamic door authority, crowds, or progression. CN_021 aperture and room links additionally cover every source character height.",
                OpenDoors = connections.Count(c => c.Kind == "Door"),
                PortalEnds = connections.Count(c => c.Kind != "Stair") * 2,
                LinkedPortalEnds = portalRoomLinks.Select(l => l.From).Distinct().Count(),
                UndirectedRoomLinks = portalRoomLinks.Count,
                RejectedOpenLeafLinks = rejectedPortalRoomLinks.ToArray()
            };
            var failures = new List<PortalRoomLinkFailure>();
            var libraryHeights = CsvTable.Load(Path.Combine(ProductionImporter.SourceRoot, "07_ASSET_MANIFEST/CHARACTER_MANIFEST.csv"))
                .Select(row => (float)row.Number("HeightCm") / 100).Distinct().OrderBy(h => h).ToArray();
            report.LibraryControllerHeights = libraryHeights;
            var probes = new List<PortalRoomLink>(portalRoomLinks);
            var libraryPortal = connections.Single(c => c.ConnectionId == "CN_021");
            for (int i = 1; i < libraryPortal.Route.Length; i++)
            {
                probes.Add(new PortalRoomLink { ConnectionId = "CN_021", From = navNodes.FindIndex(n => n.Id == "NAV_CN_021_" + (i - 1)),
                    To = navNodes.FindIndex(n => n.Id == "NAV_CN_021_" + i) });
                report.AdditionalLibraryApertureSegments++;
            }
            var probe = new GameObject("TEST_ONLY_M01_OPEN_DOOR_ROOM_LINK_CAPSULE");
            var controller = probe.AddComponent<UnityEngine.CharacterController>();
            controller.height = TallestResidentHeight; controller.radius = .28f; controller.center = Vector3.up * TallestResidentHeight * .5f;
            controller.stepOffset = .32f; controller.skinWidth = .025f; controller.minMoveDistance = 0;
            try
            {
                foreach (var link in probes)
                    foreach (float height in link.ConnectionId == "CN_021" ? libraryHeights : new[] { TallestResidentHeight })
                    foreach (float speed in new[] { 1.4f, 4.8f })
                        foreach (bool reverse in new[] { false, true })
                        {
                            int from = reverse ? link.To : link.From, to = reverse ? link.From : link.To;
                            var start = navNodes[from].Position; var target = navNodes[to].Position;
                            report.Probes++;
                            controller.enabled = false; controller.height = height; controller.center = Vector3.up * height * .5f;
                            probe.transform.position = start + Vector3.up * .035f; controller.enabled = true;
                            Physics.SyncTransforms();
                            for (int settle = 0; settle < 3; settle++) controller.Move(Vector3.down * (2f / 60));
                            int maximum = Mathf.CeilToInt(Vector3.Distance(probe.transform.position, target) / speed * 120) + 180;
                            int count = 0; CollisionFlags flags = CollisionFlags.None;
                            while (Vector3.Distance(probe.transform.position, target) >= .14f && count++ < maximum)
                            {
                                flags = controller.Move(Vector3.ClampMagnitude(target - probe.transform.position, speed / 60) + Vector3.down * (2f / 60));
                                report.Moves++;
                            }
                            if (Vector3.Distance(probe.transform.position, target) < .14f) { report.Passed++; continue; }
                            physicalFailure = "No body overlap at rest; blocked during motion or step-up";
                            BodyClear(probe.transform.position, false);
                            failures.Add(new PortalRoomLinkFailure { ConnectionId = link.ConnectionId, From = navNodes[from].Id, To = navNodes[to].Id,
                                Direction = reverse ? "Reverse" : "Forward", Speed = speed, ControllerHeight = height, Expected = target,
                                Actual = probe.transform.position, Collider = physicalFailure, ControllerFlags = flags.ToString() });
                        }
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); Physics.SyncTransforms(); }
            if (report.LinkedPortalEnds != report.PortalEnds)
                navIssues.Add("Open-door graph has room links for " + report.LinkedPortalEnds + "/" + report.PortalEnds + " portal endpoints.");
            foreach (var failure in failures)
                navIssues.Add("Open-door room-link CharacterController blocked: " + failure.ConnectionId + " " + failure.From + " -> " + failure.To + " " + failure.Speed + "m/s [" + failure.Collider + "]");
            report.Failures = failures.ToArray();
            report.Result = failures.Count == 0 && report.LinkedPortalEnds == report.PortalEnds ? "PASS_EDITOR_OPEN_DOOR_ROOM_LINKS" : "FAIL";
            Directory.CreateDirectory("Verification"); File.WriteAllText("Verification/mansion-open-door-links.json", JsonUtility.ToJson(report, true));
        }

        [Serializable] sealed class NavigationDebug
        {
            public MansionNavNode[] Nodes; public MansionNavEdge[] Edges; public int[] Reachable;
            public NavigationBlock[] DiningBlocks;
        }
        [Serializable] sealed class NavigationBlock {public string Name;public Bounds Bounds;}
        [Serializable] sealed class GeometryReceipt
        {
            public string Builder, Editor, MapVersion, Result;
            public int Rooms, Bedrooms, Connections, Doors, Seats, TrialSeats, Nodes, DirectedEdges, ReachableRooms, Anchors, ReachableAnchors, PortalRoomLinks, RejectedOpenLeafRoomLinks;
            public string NavigationDoorPose;
            public PortalRefinement[] PortalRefinements;
            public string[] Errors, Notes;
        }
        static void ValidateGeometry()
        {
            var errors = new List<string>(navIssues); var report = new GeometryReceipt {
                Builder = BuilderVersion, Editor = Application.unityVersion, MapVersion = "M01", Rooms = rooms.Count,
                Bedrooms = rooms.Keys.Count(x => x.StartsWith("R_BED_", StringComparison.Ordinal) && x != "R_BED_COR"), Connections = connections.Count,
                Doors = connections.Count(x => x.Kind == "Door"), Seats = anchors.Count(x => x.InteractionType == "Seat"), TrialSeats = anchors.Count(x => x.InteractionType == "Seat" && x.RoomId == "R_TRIAL"),
                Nodes = navNodes.Count, DirectedEdges = navEdges.Count, Anchors = anchors.Count, Notes = layout.GeometryNotes,
                NavigationDoorPose = "Actual fully-open leaves with collision enabled; initial poses/enabled flags restored after navigation generation.",
                PortalRoomLinks = portalRoomLinks.Count, RejectedOpenLeafRoomLinks = rejectedPortalRoomLinks.Count, PortalRefinements = portalRefinements.ToArray()
            };
            if (report.Rooms != 80) errors.Add("Expected 80 room records, actual " + report.Rooms);
            if (report.Bedrooms != 18) errors.Add("Expected 18 bedrooms, actual " + report.Bedrooms);
            if (report.Connections != 79) errors.Add("Expected 79 connections, actual " + report.Connections);
            if (report.Seats != 201 || report.TrialSeats != 18) errors.Add("Seat count mismatch " + report.Seats + "/" + report.TrialSeats);
            foreach (var duplicate in navNodes.GroupBy(x => x.Id).Where(g => g.Count() > 1)) errors.Add("Duplicate node " + duplicate.Key);
            foreach (var duplicate in anchors.GroupBy(x => x.AnchorId).Where(g => g.Count() > 1)) errors.Add("Duplicate anchor " + duplicate.Key);
            var adjacency = new List<int>[navNodes.Count];
            foreach (var edge in navEdges) { if (adjacency[edge.From] == null) adjacency[edge.From] = new List<int>(); adjacency[edge.From].Add(edge.To); }
            var visited = new HashSet<int>(); var queue = new Queue<int>(); int start = rooms["R_HALL"].WalkNode;
            if (start >= 0) { queue.Enqueue(start); visited.Add(start); }
            while (queue.Count > 0) { int n = queue.Dequeue(); if (adjacency[n] == null) continue; foreach (int next in adjacency[n]) if (visited.Add(next)) queue.Enqueue(next); }
            foreach (var room in rooms.Values) { if (room.WalkNode >= 0 && visited.Contains(room.WalkNode)) report.ReachableRooms++; else errors.Add("Room not reachable by physical graph: " + room.RoomId + " at " + room.WalkPoint); }
            foreach (var anchor in anchors) { if (anchor.ApproachNode >= 0 && visited.Contains(anchor.ApproachNode)) report.ReachableAnchors++; else errors.Add("Anchor not reachable by physical graph: " + anchor.AnchorId + " at " + anchor.ApproachPoint); }
            foreach (var c in connections)
            {
                if (c.Kind == "Door" && (c.LeafPivot == null || c.LeafCollider == null)) errors.Add("Door lacks actual leaf " + c.DoorId);
                if (c.EntryNodeA < 0 || c.EntryNodeB < 0 || !visited.Contains(c.EntryNodeA) || !visited.Contains(c.EntryNodeB)) errors.Add("Connection unreachable " + c.ConnectionId);
                if (c.Kind == "ZoneBoundary" || c.Kind == "Stair") continue;
                var a = rooms[c.RoomA]; var b = rooms[c.RoomB];
                foreach (var room in new[] { a, b })
                {
                    var p = c.transform.position; bool boundary = Mathf.Abs(p.x - room.Bounds.min.x) < .02f || Mathf.Abs(p.x - room.Bounds.max.x) < .02f || Mathf.Abs(p.z - room.Bounds.min.z) < .02f || Mathf.Abs(p.z - room.Bounds.max.z) < .02f;
                    if (!boundary) errors.Add("Portal not on room boundary " + c.ConnectionId + " " + room.RoomId);
                }
            }
            report.Errors = errors.Distinct().ToArray(); report.Result = errors.Count == 0 ? "PASS_STATIC_PHYSICS_GRAPH_NOT_PLAYTEST" : "FAIL";
            Directory.CreateDirectory("Verification"); File.WriteAllText("Verification/mansion-geometry.json", JsonUtility.ToJson(report, true));
            if (errors.Count > 0) {
                File.WriteAllText("Verification/mansion-navigation-debug.json",JsonUtility.ToJson(new NavigationDebug {Nodes=navNodes.ToArray(),Edges=navEdges.ToArray(),Reachable=visited.ToArray(),DiningBlocks=rooms["R_DINING"].GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger&&c.bounds.max.y>.05f&&c.bounds.min.y<1.87f).Select(c=>new NavigationBlock{Name=c.transform.parent.name+"/"+c.name,Bounds=c.bounds}).ToArray()}));
                throw new InvalidOperationException("M01 physical authoring validation failed (" + errors.Count + "): " + string.Join(" | ", errors.Take(12)) + ". See Verification/mansion-geometry.json.");
            }
        }
    }
}
