using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BASSLINE.AuthoringData;
using BASSLINE.Save;
using UnityEditor;
using UnityEngine;

namespace BASSLINE.Authoring
{
    public static partial class MansionBuilder
    {
        sealed class WallSpan { public bool AlongZ; public float Plane, Min, Max, Bottom, Top; public Material Material; }
        static readonly List<WallSpan> wallSpans = new List<WallSpan>();

        static void CreateShell(MansionRoom r)
        {
            if (r == rooms.Values.First()) wallSpans.Clear();
            var parent = r.transform.Find("Architecture"); var p = r.FloorCenter; float w = r.Bounds.size.x, d = r.Bounds.size.z;
            if (r.RoomId == "R_GRAND") { GrandStair(r); return; }
            if (r.GeometryType == "Subzone") return;
            if (r.GeometryType == "PerimeterRing") { Balcony(r); return; }
            if (r.RoomId == "R_LIBRARY") { Library(r); return; }
            if (r.GeometryType == "StairVolume") ServiceStair(r);
            else if (r.RoomId == "R_POOL") Pool(r);
            else if (r.RoomId == "R_HALL") HallFloor(r);
            else Box(parent, "WalkFloor", p - Vector3.up * .15f, new Vector3(w, .3f, d), r.RoomId == "R_TRIAL" ? black : tile);
            if (r.CeilingHeight > 0)
                Box(parent, "Ceiling", p + Vector3.up * (r.CeilingHeight + .1f), new Vector3(w, .2f, d), r.RoomId == "R_TRIAL" ? black : stone);
            float height = r.GeometryType == "Outdoor" ? 1.1f : r.CeilingHeight;
            var wallMaterial = r.RoomId == "R_TRIAL" ? black : r.WingId == "ANX" ? aged : stone;
            wallSpans.Add(new WallSpan { AlongZ = true, Plane = p.x - w / 2, Min = p.z - d / 2, Max = p.z + d / 2, Bottom = p.y, Top = p.y + height, Material = wallMaterial });
            wallSpans.Add(new WallSpan { AlongZ = true, Plane = p.x + w / 2, Min = p.z - d / 2, Max = p.z + d / 2, Bottom = p.y, Top = p.y + height, Material = wallMaterial });
            wallSpans.Add(new WallSpan { AlongZ = false, Plane = p.z - d / 2, Min = p.x - w / 2, Max = p.x + w / 2, Bottom = p.y, Top = p.y + height, Material = wallMaterial });
            wallSpans.Add(new WallSpan { AlongZ = false, Plane = p.z + d / 2, Min = p.x - w / 2, Max = p.x + w / 2, Bottom = p.y, Top = p.y + height, Material = wallMaterial });
        }

        static void BuildSharedWalls()
        {
            // Decompose the union of collinear wall rectangles, then subtract every aperture.
            // This also cuts the y=6 balcony door from the y=0..11 Hall exterior shell.
            foreach (var group in wallSpans.GroupBy(s => (s.AlongZ ? "X" : "Z") + s.Plane.ToString("F3", CultureInfo.InvariantCulture)))
            {
                var spans = group.ToArray(); bool along = spans[0].AlongZ; float plane = spans[0].Plane;
                var cuts = connections.Where(c => c.Kind != "ZoneBoundary" && c.Kind != "Stair" &&
                    Mathf.Abs((along ? c.transform.position.x : c.transform.position.z) - plane) < .02f &&
                    (along ? Mathf.Abs(c.NormalAToB.x) : Mathf.Abs(c.NormalAToB.z)) > .5f).ToArray();
                var horizontal = spans.SelectMany(s => new[] { s.Min, s.Max }).Concat(cuts.SelectMany(c => {
                    float coordinate = along ? c.transform.position.z : c.transform.position.x; return new[] { coordinate - c.Width / 2, coordinate + c.Width / 2 };
                })).Distinct().OrderBy(v => v).ToArray();
                var vertical = spans.SelectMany(s => new[] { s.Bottom, s.Top }).Concat(cuts.SelectMany(c => new[] { c.transform.position.y, c.transform.position.y + c.ClearHeight })).Distinct().OrderBy(v => v).ToArray();
                for (int yi = 0; yi < vertical.Length - 1; yi++)
                {
                    float bottom = vertical[yi], top = vertical[yi + 1], midY = (bottom + top) / 2;
                    int xi = 0;
                    while (xi < horizontal.Length - 1)
                    {
                        float lo = horizontal[xi], hi = horizontal[xi + 1], mid = (lo + hi) / 2;
                        var covering = spans.FirstOrDefault(s => mid > s.Min - .001f && mid < s.Max + .001f && midY > s.Bottom && midY < s.Top);
                        bool aperture = cuts.Any(c => Mathf.Abs(mid - (along ? c.transform.position.z : c.transform.position.x)) < c.Width / 2 + .001f && midY > c.transform.position.y - .001f && midY < c.transform.position.y + c.ClearHeight);
                        xi++;
                        if (covering == null || aperture) continue;
                        // Merge contiguous cells with the same surface to keep renderer/collider counts bounded.
                        while (xi < horizontal.Length - 1)
                        {
                            float next = (horizontal[xi] + horizontal[xi + 1]) / 2;
                            var coverNext = spans.FirstOrDefault(s => next > s.Min - .001f && next < s.Max + .001f && midY > s.Bottom && midY < s.Top);
                            bool cutNext = cuts.Any(c => Mathf.Abs(next - (along ? c.transform.position.z : c.transform.position.x)) < c.Width / 2 + .001f && midY > c.transform.position.y - .001f && midY < c.transform.position.y + c.ClearHeight);
                            if (coverNext == null || coverNext.Material != covering.Material || cutNext) break;
                            hi = horizontal[xi + 1]; xi++;
                        }
                        Vector3 p = along ? new Vector3(plane, midY, (lo + hi) / 2) : new Vector3((lo + hi) / 2, midY, plane);
                        Vector3 size = along ? new Vector3(.2f, top - bottom, hi - lo) : new Vector3(hi - lo, top - bottom, .2f);
                        Box(architecture, "SharedWall_" + shapeNumber++, p, size, covering.Material);
                    }
                }
            }
        }

        static void HallFloor(MansionRoom r)
        {
            var parent = r.transform.Find("Architecture");
            Box(parent, "FloorWest", new Vector3(-7.75f, -.15f, 0), new Vector3(10.5f, .3f, 22), tile);
            Box(parent, "FloorSpine", new Vector3(0, -.15f, 0), new Vector3(4, .3f, 22), tile);
            Box(parent, "FloorEast", new Vector3(7.75f, -.15f, 0), new Vector3(10.5f, .3f, 22), tile);
            foreach (float x in new[] { -2.25f, 2.25f })
            {
                Box(parent, "ChannelBed", new Vector3(x, -.13f, 0), new Vector3(.5f, .1f, 22), stone);
                Box(parent, "ShallowWater", new Vector3(x, -.012f, 0), new Vector3(.5f, .012f, 22), water, false);
                foreach (float z in new[] { -8.5f, 0f, 8.5f }) Box(parent, "DryCrossing", new Vector3(x, -.05f, z), new Vector3(.5f, .1f, 2.8f), tile);
            }
            Box(parent, "CentralCarpet", new Vector3(0, .008f, 0), new Vector3(3, .015f, 22), red, false);
            foreach (float x in new[] { -10f, 10f }) foreach (float z in new[] { -5.8f, 4f })
            {
                Box(parent, "PillarBase", new Vector3(x, .14f, z), new Vector3(.72f, .28f, .72f), stone);
                Box(parent, "Pillar", new Vector3(x, 5.5f, z), new Vector3(.43f, 11, .43f), stone);
                Box(parent, "PillarCapital", new Vector3(x, 10.65f, z), new Vector3(.78f, .25f, .78f), metal, false);
            }
            for (int i = -5; i <= 5; i++) Box(parent, "FloorInlay", new Vector3(0, .001f, i * 2), new Vector3(25.6f, .003f, .012f), metal, false);
        }

        static void Balcony(MansionRoom r)
        {
            var parent = r.transform.Find("Architecture");
            Box(parent, "NorthDeck", new Vector3(0, 5.9f, 9.75f), new Vector3(26, .2f, 2.5f), stone);
            Box(parent, "SouthDeck", new Vector3(0, 5.9f, -9.75f), new Vector3(26, .2f, 2.5f), stone);
            Box(parent, "WestDeck", new Vector3(-11.75f, 5.9f, 0), new Vector3(2.5f, .2f, 17), stone);
            Box(parent, "EastDeck", new Vector3(11.75f, 5.9f, 0), new Vector3(2.5f, .2f, 17), stone);
            Rail(parent, new Vector3(-10.5f, 6, -8.5f), new Vector3(10.5f, 6, -8.5f));
            Rail(parent, new Vector3(-10.5f, 6, -8.5f), new Vector3(-10.5f, 6, 8.5f));
            // Leave only the real stair bridge and exit openings in the ring rail.
            Rail(parent, new Vector3(-10.5f, 6, 8.5f), new Vector3(-.6f, 6, 8.5f));
            Rail(parent, new Vector3(10.5f, 6, -8.5f), new Vector3(10.5f, 6, 4.7f));
            Rail(parent, new Vector3(10.5f, 6, 7.3f), new Vector3(10.5f, 6, 8.5f));
            r.WalkPoint = new Vector3(11.75f, 6, 0);
        }

        static void GrandStair(MansionRoom r)
        {
            var parent = r.transform.Find("Architecture"); var route = new List<Vector3>();
            Vector3 a = new Vector3(.42f, 0, 2.2f), b = new Vector3(5.74f, 3, 2.2f);
            route.Add(a - Vector3.right * .45f); route.AddRange(Flight(parent, "GrandA", a, b, 18, 2.4f));
            Box(parent, "MiddleLanding", new Vector3(6.35f, 2.9f, 4), new Vector3(1.3f, .2f, 6), stone);
            route.Add(new Vector3(6.35f, 3, 2.2f)); route.Add(new Vector3(6.35f, 3, 5.8f));
            var c = new Vector3(5.74f, 3, 5.8f); route.Add(c + Vector3.right * .45f);
            route.AddRange(Flight(parent, "GrandB", c, new Vector3(.42f, 6, 5.8f), 18, 2.4f));
            Box(parent, "TopTurn", new Vector3(.05f, 5.9f, 6.8f), new Vector3(1.2f, .2f, 4.4f), stone);
            Box(parent, "TopBridge", new Vector3(4.95f, 5.9f, 8.3f), new Vector3(11, .2f, 2.4f), stone);
            Box(parent, "ExitTurn", new Vector3(9.9f, 5.9f, 6.6f), new Vector3(1.2f, .2f, 3.4f), stone);
            Box(parent, "ExitJoin", new Vector3(10.8f, 5.9f, 6), new Vector3(1.8f, .2f, 2.4f), stone);
            route.Add(new Vector3(.05f, 6, 5.8f)); route.Add(new Vector3(.05f, 6, 8.3f)); route.Add(new Vector3(9.9f, 6, 8.3f)); route.Add(new Vector3(9.9f, 6, 6)); route.Add(new Vector3(11.75f, 6, 6));
            Rail(parent, new Vector3(-.5f, 6, 4.6f), new Vector3(-.5f, 6, 7.05f));
            Rail(parent, new Vector3(.65f, 6, 7.05f), new Vector3(9.3f, 6, 7.05f));
            Rail(parent, new Vector3(9.3f, 6, 4.8f), new Vector3(9.3f, 6, 7.05f));
            stairPaths.Add((r.RoomId, route.ToArray()));
            var connection = connections.Single(x => x.Kind == "Stair"); connection.Route = route.ToArray();
            r.WalkPoint = a - Vector3.right * .45f;
        }

        static void ServiceStair(MansionRoom r)
        {
            var parent = r.transform.Find("Architecture");
            // Three perimeter flights per 6m floor interval: 36 risers, 1.6m width and actual flat corner landings.
            for (int level = -6; level <= 12; level += 6)
            {
                Box(parent, "WestLanding_" + level, new Vector3(-8.05f, level - .1f, 16), new Vector3(4.9f, .2f, 2.4f), stone);
                // The east flight passes below this bridge. Its shorter north/south extent
                // leaves real headroom for capsule step-up at the last risers of flight B.
                Box(parent, "EastLandingBridge_" + level, new Vector3(-4.05f, level - .1f, 16), new Vector3(3.1f, .2f, 1.6f), stone);
                Box(parent, "WestApproach_" + level, new Vector3(-9, level - .1f, 13.75f), new Vector3(1.8f, .2f, 4.5f), stone);
                Box(parent, "WestReturn_" + level, new Vector3(-9.65f, level - .1f, 18), new Vector3(1.3f, .2f, 2.4f), stone);
                Sign(parent, "Floor_" + level, new Vector3(-10.37f, level + 1.6f, 16), Quaternion.Euler(0, -90, 0), level == -6 ? "B1" : level == 0 ? "1F" : level == 6 ? "2F" : "3F", .75f, .45f);
                if (level == 12) continue;
                var route = new List<Vector3> { new Vector3(-9.45f, level, 16), new Vector3(-9.45f, level, 12.5f) };
                route.AddRange(Flight(parent, "ServiceA_" + level, new Vector3(-9, level, 12.5f), new Vector3(-5, level + 2, 12.5f), 12, 1.6f));
                Box(parent, "TurnA_" + level, new Vector3(-4.1f, level + 1.9f, 12.5f), new Vector3(1.8f, .2f, 1.8f), stone);
                route.Add(new Vector3(-4, level + 2, 12.5f)); route.Add(new Vector3(-4, level + 2, 12.95f));
                route.AddRange(Flight(parent, "ServiceB_" + level, new Vector3(-4, level + 2, 13.4f), new Vector3(-4, level + 4, 17.8f), 12, 1.6f));
                Box(parent, "TurnB_" + level, new Vector3(-4.1f, level + 3.9f, 18.5f), new Vector3(1.8f, .2f, 1.6f), stone);
                route.Add(new Vector3(-4, level + 4, 18.5f)); route.Add(new Vector3(-4.55f, level + 4, 18.5f));
                route.AddRange(Flight(parent, "ServiceC_" + level, new Vector3(-5, level + 4, 18.5f), new Vector3(-9, level + 6, 18.5f), 12, 1.6f));
                route.Add(new Vector3(-9.5f, level + 6, 18.5f)); route.Add(new Vector3(-9.5f, level + 6, 16)); route.Add(new Vector3(-9, level + 6, 16)); stairPaths.Add((r.RoomId, route.ToArray()));
            }
            r.WalkPoint = new Vector3(-8.8f, 0, 16);
        }

        static List<Vector3> Flight(Transform parent, string name, Vector3 start, Vector3 end, int risers, float width)
        {
            var points = new List<Vector3>(); Vector3 horizontal = end - start; horizontal.y = 0;
            float run = horizontal.magnitude, rise = end.y - start.y; var forward = horizontal.normalized;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            for (int i = 0; i < risers; i++)
            {
                float top = start.y + rise * (i + 1) / risers;
                Vector3 center = start + forward * run * (i + .5f) / risers; center.y = (start.y + top) / 2 - .01f;
                var stair = Box(parent, name + "_Tread_" + i.ToString("D2"), center, new Vector3(width, top - start.y + .02f, run / risers + .008f), stone); stair.transform.rotation = rotation;
                // Treads are physical geometry, not stop targets. Capsule stepping raises feet
                // before the centre reaches a short tread; navigation targets flat landings.
            }
            points.Add(end);
            Vector3 side = Vector3.Cross(Vector3.up, forward) * (width / 2 - .05f);
            // Leave turning clearance at landings; continuous side rails across a right-angle
            // junction would intersect the next flight's centreline.
            float inset = Mathf.Min(width * .55f, run * .22f);
            Rail(parent, start + side + forward * inset, end + side - forward * inset); Rail(parent, start - side + forward * inset, end - side - forward * inset);
            return points;
        }

        static void Rail(Transform parent, Vector3 a, Vector3 b)
        {
            Vector3 direction = b - a; float length = direction.magnitude;
            var top = Box(parent, "RailTop_" + shapeNumber++, (a + b) / 2 + Vector3.up * 1.03f, new Vector3(.065f, .065f, length), metal);
            top.transform.rotation = Quaternion.LookRotation(direction);
            int count = Mathf.CeilToInt(length / 1.25f);
            for (int i = 0; i <= count; i++) Box(parent, "RailPost_" + shapeNumber++, Vector3.Lerp(a, b, (float)i / count) + Vector3.up * .51f, new Vector3(.05f, 1.02f, .05f), metal);
            var barrier = new GameObject("RailPhysicalInfill_" + shapeNumber++); barrier.transform.SetParent(parent, false); barrier.transform.position = (a + b) / 2 + Vector3.up * .52f; barrier.transform.rotation = Quaternion.LookRotation(direction);
            barrier.AddComponent<BoxCollider>().size = new Vector3(.055f, 1.03f, length);
            // Visible horizontal infill accompanies the collider; rails are never invisible navigation fences.
            foreach (float h in new[] { .25f, .50f, .75f }) { var bar = Box(parent, "RailInfill", (a + b) / 2 + Vector3.up * h, new Vector3(.025f, .025f, length), metal, false); bar.transform.rotation = Quaternion.LookRotation(direction); }
        }

        static void Pool(MansionRoom r)
        {
            var p = r.FloorCenter; var parent = r.transform.Find("Architecture");
            foreach (int side in new[] { -1, 1 })
            {
                Box(parent, "DeckSide", p + new Vector3(side * 8, -.15f, 0), new Vector3(4, .3f, 24), tile);
                Box(parent, "DeckEnd", p + new Vector3(0, -.15f, side * 10), new Vector3(12, .3f, 4), tile);
                Box(parent, "BasinWallX", p + new Vector3(side * 6.1f, -.6f, 0), new Vector3(.2f, 1.2f, 16.2f), stone);
                Box(parent, "BasinWallZ", p + new Vector3(0, -.6f, side * 8.1f), new Vector3(12.2f, 1.2f, .2f), stone);
            }
            Box(parent, "BasinFloor", p + new Vector3(0, -1.3f, 0), new Vector3(12, .2f, 16), tile);
            Box(parent, "WaterSurface_NoWalkCollider", p + new Vector3(0, -.04f, 0), new Vector3(12, .02f, 16), water, false);
            for (int i = -2; i <= 2; i++) Box(parent, "LaneMark", p + new Vector3(i * 2.2f, -1.195f, 0), new Vector3(.08f, .01f, 14), black, false);
            foreach (float x in new[] { -.45f, .45f }) Cylinder(parent, "PoolLadderRail", p + new Vector3(x, -.05f, 7.75f), .045f, 2, metal);
            for (int i = 0; i < 5; i++) Box(parent, "PoolLadderRung", p + new Vector3(0, -.9f + i * .3f, 7.75f), new Vector3(.9f, .055f, .12f), metal);
            Sign(parent, "PoolDepth", p + new Vector3(0, .035f, 9.3f), Quaternion.Euler(90, 0, 0), "수심 1.2 m", 2.3f, .4f);
            r.WalkPoint = p + new Vector3(8, 0, 0);
        }

        static void Library(MansionRoom r)
        {
            var p = r.FloorCenter; var parent = r.transform.Find("Architecture"); const int segments = 256;
            var vertices = new Vector3[(segments + 1) * 2]; var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments; float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
                float major = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)), minor = Mathf.Min(Mathf.Abs(x), Mathf.Abs(z));
                float hole = minor < .0001f ? 8.36f / major : Mathf.Min(8.36f / major, 1.2f / minor); float inner = Mathf.Max(5, hole);
                vertices[i * 2] = new Vector3(x * inner, 0, z * inner); vertices[i * 2 + 1] = new Vector3(x * 10, 0, z * 10);
                if (i == segments) continue; int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 3; triangles[t + 2] = v + 1;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "M01_LibraryRim" }; mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var uv = new Vector2[vertices.Length]; for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(vertices[i].x, vertices[i].z); mesh.uv = uv;
            string meshPath = "Assets/BASSLINE/Environment/M01/M01_LibraryRim.asset"; Directory.CreateDirectory(Path.GetDirectoryName(meshPath));
            string meshHash = AtomicSaveStore.Hash(BuilderVersion + "LibraryRim");
            if (ProductionImporter.CanWrite(ProductionImporter.LoadIndex(), "MESH_M01_LibraryRim", meshPath, meshHash))
            {
                var old = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (old == null) AssetDatabase.CreateAsset(mesh, meshPath); else { EditorUtility.CopySerialized(mesh, old); UnityEngine.Object.DestroyImmediate(mesh); mesh = old; }
                AssetDatabase.SaveAssets(); ProductionImporter.Track(ProductionImporter.LoadIndex(), "MESH_M01_LibraryRim", meshPath, meshHash, "FunctionalGeometry");
            }
            else { UnityEngine.Object.DestroyImmediate(mesh); mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath); }
            var rim = new GameObject("ActualAnnularRimWithFourStairVoids"); rim.transform.SetParent(parent, false); rim.transform.position = p; rim.AddComponent<MeshFilter>().sharedMesh = mesh; rim.AddComponent<MeshRenderer>().sharedMaterial = stone; rim.AddComponent<MeshCollider>().sharedMesh = mesh;
            Cylinder(parent, "LoweredCentralFloor", p - Vector3.up * 2.15f, 5.12f, .3f, tile);
            Cylinder(parent, "LibraryCeiling", p + Vector3.up * 6.1f, 10, .2f, stone);
            Box(parent, "DoorChordJoin", p + new Vector3(9.963f, -.1f, 0), new Vector3(.1f, .2f, 2.4f), tile);
            // The curved wall meets the OUTER jamb edge, leaving the full 2.4m clear opening.
            float doorwayAngle = Mathf.Asin(1.32f / 10);
            var wallAngles = Enumerable.Range(0, 65).Select(i => i * Mathf.PI * 2 / 64).Concat(new[] { doorwayAngle, Mathf.PI * 2 - doorwayAngle }).OrderBy(a => a).ToArray();
            for (int i = 0; i < wallAngles.Length - 1; i++)
            {
                float a = wallAngles[i], b = wallAngles[i + 1];
                var va = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var vb = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                var mid = (va + vb) * 5;
                if (mid.x > 9.7f && Mathf.Abs(mid.z) < 1.3199f) continue;
                var wall = Box(parent, "CircularWall_" + i, p + mid + Vector3.up * 3, new Vector3(.2f, 6, (va - vb).magnitude * 10 + .015f), stone);
                wall.transform.rotation = Quaternion.LookRotation(vb - va);
                float cardinal = Mathf.Min(Mathf.Abs(mid.x), Mathf.Abs(mid.z));
                if (cardinal > 2.7f) Rail(parent, p + va * 5, p + vb * 5);
            }
            Box(parent, "DoorChordLintel", p + new Vector3(10, 4.2f, 0), new Vector3(.2f, 3.6f, 2.8f), stone);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2; var axis = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var low = p + axis * 5 - Vector3.up * 2; var high = p + axis * 8.36f;
                var route = new List<Vector3> { p + axis * 4.5f - Vector3.up * 2, low - axis * .45f };
                route.AddRange(Flight(parent, "LibraryStair_" + i, low, high, 12, 2.4f)); route.Add(p + axis * 9);
                stairPaths.Add((r.RoomId, route.ToArray()));
            }
            r.WalkPoint = p + new Vector3(9, 0, 0);
        }
    }
}
