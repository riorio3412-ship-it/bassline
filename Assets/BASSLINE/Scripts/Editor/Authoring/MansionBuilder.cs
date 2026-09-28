using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BASSLINE.AuthoringData;
using BASSLINE.Save;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BASSLINE.Authoring
{
    /// <summary>Reproducible M01 environment. Art remains explicitly replaceable functional proxy.</summary>
    public static partial class MansionBuilder
    {
        public const string ScenePath = "Assets/BASSLINE/Scenes/Mansion_M01.unity";
        public const string BuilderVersion = "M01_FurnishedPhysical_002";
        static MansionLayout layout;
        static Transform roomsRoot, architecture, connectionsRoot;
        static Dictionary<string, MansionRoom> rooms;
        static Dictionary<string, Dictionary<string, string>> specs;
        static List<MansionConnection> connections;
        static List<MansionAnchor> anchors;
        static List<(string room, Vector3[] points)> stairPaths;
        static Material stone, tile, metal, red, water, wood, cloth, purple, cyan, black, aged, glass, lightFace;
        static TMP_FontAsset font;
        static int shapeNumber;

        [MenuItem("BASSLINE/Production/7 Build complete M01 environment")]
        public static void Build()
        {
            string sourceHash = AtomicSaveStore.Hash(BuilderVersion + AppearanceVersion + ProductionImporter.CatalogHash());
            if (!ProductionImporter.CanWrite(ProductionImporter.LoadIndex(), "SCN_Mansion_M01", ScenePath, sourceHash)) return;
            var previous = SceneManager.GetActiveScene();
            if (!Application.isBatchMode && (previous.isDirty || string.IsNullOrEmpty(previous.path)))
                throw new InvalidOperationException("Save the active scene before creating the M01 scene.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                BuildIntoActiveScene();
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("M01 scene save failed");
                ProductionImporter.Track(ProductionImporter.LoadIndex(), "SCN_Mansion_M01", ScenePath, sourceHash, "FunctionalProxy_PhysicalNavigationVerifiedByBuilder");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }

        /// <summary>Call from the production setup in a NEW scene before adding actors/runtime/UI.</summary>
        public static MansionLayout BuildIntoActiveScene()
        {
            var active = SceneManager.GetActiveScene();
            if (active.GetRootGameObjects().Any(x => x.GetComponent<MansionLayout>() != null))
                throw new InvalidOperationException("This scene already contains a MansionLayout. Rebuild via the tracked scene setup to protect manual edits.");
            rooms = new Dictionary<string, MansionRoom>(StringComparer.Ordinal);
            connections = new List<MansionConnection>(); anchors = new List<MansionAnchor>();
            stairPaths = new List<(string, Vector3[])>(); shapeNumber = 0;
            specs = ReadSpecs(Path.Combine(ProductionImporter.SourceRoot, "02_MANSION/BASSLINE_ROOM_SPECS.md"));
            var root = new GameObject("Mansion_M01_World"); layout = root.AddComponent<MansionLayout>();
            layout.BuildVersion = BuilderVersion; layout.SourceHash = ProductionImporter.CatalogHash();
            roomsRoot = Group(root.transform, "Rooms"); architecture = Group(root.transform, "SharedArchitecture");
            connectionsRoot = Group(root.transform, "Connections");
            LoadMaterials();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BASSLINE/UI/Fonts/FONT_Korean_System.asset");
            foreach (var row in CsvTable.Load(Path.Combine(ProductionImporter.SourceRoot, "07_ASSET_MANIFEST/ROOM_MANIFEST.csv"))) CreateRoom(row);
            foreach (var row in CsvTable.Load(Path.Combine(ProductionImporter.SourceRoot, "02_MANSION/BASSLINE_ROOM_CONNECTIONS.csv"))) CreateConnection(row);
            foreach (var room in rooms.Values) CreateShell(room);
            BuildSharedWalls();
            foreach (var room in rooms.Values) Furnish(room);
            foreach (var movable in root.GetComponentsInChildren<MansionProp>().Where(x => x.Movable))
                foreach (var child in movable.GetComponentsInChildren<Transform>()) child.gameObject.isStatic = false;
            CreateSeats();
            CreateAnchors();
            foreach (var room in rooms.Values) Illuminate(room);
            layout.Rooms = rooms.Values.ToArray(); layout.Connections = connections.ToArray(); layout.Anchors = anchors.ToArray();
            layout.GeometryNotes = new[] {
                "PRODUCTION PROPOSAL: M01 coordinates preserved. Shared 0.2m walls are centered on CSV adjacency planes, consuming 0.1m of nominal room envelopes on either side; declared door clear widths are preserved.",
                "FunctionalProxy materials, built furniture and spatial forms; final art approval remains separate.",
                "PRODUCTION PROPOSAL: D_014 swings into the 5m living corridor. Inward leaves trapped entrants against fixed dining seat backs; source doorway/seat coordinates and clear width are unchanged.",
                "Grand stair tread route is a reversible detailed implementation inside R_GRAND, replacing the CSV schematic polyline without changing room connectivity.",
                "Seat coordinates retained. Activity authoring points are projected onto reachable walk surfaces while preserving stable anchor IDs and revision.",
                "No camera or automatic knowledge source is placed. Entry/observe markers are only query origins.",
                "Navigation grid clearance radius 0.30m / tallest resident height 1.87m; dynamic doors are evaluated independently by authoritative access/motion commands."
            };
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.36f, .38f, .40f);
            RenderSettings.fog = false;
            Physics.SyncTransforms();
            BuildNavigation();
            ValidateGeometry();
            // Batch scene setup is isolated. In an interactive additive scene, unrelated user
            // colliders share the physics world and must not be toggled for an authoring test.
            if (Application.isBatchMode) VerifyCharacterTraversal();
            ApplyAppearance();
            AssetDatabase.SaveAssets();
            Debug.Log($"M01 generated: {layout.Rooms.Length} rooms, {layout.Connections.Length} connections, {layout.Anchors.Count(a => a.InteractionType == "Seat")} seats, {layout.NavigationNodes.Length} physical navigation nodes. Art is FunctionalProxy.");
            return layout;
        }

        static Dictionary<string, Dictionary<string, string>> ReadSpecs(string path)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(); Dictionary<string, string> fields = null;
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith("## R_", StringComparison.Ordinal)) { string id = line.Substring(3).Split(' ')[0]; fields = new Dictionary<string, string>(); result[id] = fields; }
                else if (fields != null && line.StartsWith("| ", StringComparison.Ordinal))
                {
                    var columns = line.Split('|'); if (columns.Length >= 4) fields[columns[1].Trim()] = columns[2].Trim();
                }
            }
            return result;
        }
        static string Spec(MansionRoom room, string field)
        {
            return specs.TryGetValue(room.RoomId, out var f) && f.TryGetValue(field, out var value) ? value : "";
        }
        static float N(CsvRow row, string key) => (float)row.Number(key);
        static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }
        static void CreateRoom(CsvRow row)
        {
            var go = new GameObject(row["RoomID"] + " " + row["DisplayName"]); go.transform.SetParent(roomsRoot, false);
            var r = go.AddComponent<MansionRoom>(); r.RoomId = row["RoomID"]; r.DisplayName = row["DisplayName"];
            r.WingId = row["WingID"]; r.Floor = row["Floor"]; r.GeometryType = row["GeometryType"]; r.ParentRoomId = row["ParentRoomID"];
            r.FloorCenter = new Vector3(N(row, "X"), N(row, "Y"), N(row, "Z")); r.CeilingHeight = N(row, "CeilingHeight");
            r.Bounds = new Bounds(r.FloorCenter + Vector3.up * Mathf.Max(r.CeilingHeight, 12) * .5f, new Vector3(N(row, "Width"), Mathf.Max(r.CeilingHeight, 12), N(row, "Depth")));
            r.WalkPoint = r.FloorCenter;
            r.LightingProfile = Spec(r, "LightingProfile").Split(';')[0]; r.NoiseProfile = Spec(r, "NoiseProfile").Split(';')[0];
            r.MaterialSet = Spec(r, "MaterialSet").Split(';')[0]; r.NormalActivities = Spec(r, "NormalActivities");
            ProductionImporter.Identity(go, r.RoomId, "PhysicalRoom"); rooms.Add(r.RoomId, r);
            foreach (string name in new[] { "Architecture", "Furniture", "Anchors", "Seats", "Lighting", "ObservationZones" }) Group(go.transform, name);
        }
        static void CreateConnection(CsvRow row)
        {
            var go = new GameObject(row["ConnectionID"] + (row["DoorID"] != "" ? " " + row["DoorID"] : "")); go.transform.SetParent(connectionsRoot, false);
            go.transform.position = new Vector3(N(row, "X"), N(row, "Y"), N(row, "Z"));
            var c = go.AddComponent<MansionConnection>(); c.ConnectionId = row["ConnectionID"]; c.DoorId = row["DoorID"];
            c.RoomA = row["RoomA"]; c.RoomB = row["RoomB"]; c.Kind = row["Kind"]; c.Width = N(row, "Width"); c.ClearHeight = N(row, "ClearHeight");
            c.InitialLock = row["InitialLock"]; c.InitialOpen = row["InitialOpen"]; c.EmergencyOpen = row["EmergencyOpen"] == "Yes"; c.PassageToken = row["PassageToken"];
            var a = rooms[c.RoomA]; var b = rooms[c.RoomB]; Vector3 direction = b.FloorCenter - a.FloorCenter; direction.y = 0;
            // Portal normal follows the actual shared boundary, not a diagonal room-centre vector.
            bool planeX = Mathf.Abs(go.transform.position.x - a.Bounds.min.x) < .01f || Mathf.Abs(go.transform.position.x - a.Bounds.max.x) < .01f;
            if (c.Kind == "ZoneBoundary" || c.Kind == "Stair") planeX = Mathf.Abs(direction.x) > Mathf.Abs(direction.z);
            c.NormalAToB = planeX ? new Vector3(Mathf.Sign(direction.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(direction.z));
            if (c.NormalAToB == Vector3.zero) c.NormalAToB = Vector3.forward;
            Vector3 p = go.transform.position; c.Route = new[] { p - c.NormalAToB * 1.25f, p, p + c.NormalAToB * 1.25f };
            ProductionImporter.Identity(go, c.ConnectionId, c.Kind);
            connections.Add(c);
            if (c.Kind == "Door") CreateDoor(c);
            if (c.Kind != "ZoneBoundary" && c.Kind != "Stair")
            {
                QueryOrigin(a, "OZ_" + a.RoomId + "_" + c.ConnectionId + "_ENTRY", p - c.NormalAToB);
                QueryOrigin(b, "OZ_" + b.RoomId + "_" + c.ConnectionId + "_ENTRY", p + c.NormalAToB);
            }
        }

        static void QueryOrigin(MansionRoom room, string id, Vector3 position)
        {
            var go = new GameObject(id); go.transform.SetParent(room.transform.Find("ObservationZones"), false); go.transform.position = position + Vector3.up * 1.6f;
            ProductionImporter.Identity(go, id, "ObservationQueryOrigin_NoAutomaticKnowledge");
        }

        static void CreateDoor(MansionConnection c)
        {
            c.OpenAngle = c.DoorId == "D_014" ? -100 : 100;
            var root = c.transform;
            root.rotation = Quaternion.LookRotation(c.NormalAToB, Vector3.up);
            for (int side = -1; side <= 1; side += 2)
                LocalBox(root, "Jamb_" + side, new Vector3(side * (c.Width / 2 + .075f), c.ClearHeight / 2, 0), new Vector3(.15f, c.ClearHeight, .23f), metal);
            LocalBox(root, "Header", new Vector3(0, c.ClearHeight + .1f, 0), new Vector3(c.Width + .3f, .2f, .23f), metal);
            int leaves = c.Width >= 2 ? 2 : 1;
            for (int i = 0; i < leaves; i++)
            {
                int side = i == 0 ? -1 : 1; float leafWidth = c.Width / leaves;
                var pivot = Group(root, i == 0 ? "LeafPivot" : "SecondLeafPivot"); pivot.localPosition = new Vector3(side * c.Width / 2, 0, 0);
                var leaf = LocalBox(pivot, "Leaf", new Vector3(-side * leafWidth / 2, c.ClearHeight / 2, 0), new Vector3(leafWidth - .02f, c.ClearHeight - .02f, .075f), c.RoomB == "R_TRIAL" ? black : aged);
                LocalBox(pivot, "Inset", new Vector3(-side * leafWidth / 2, 1.45f, -.048f), new Vector3(leafWidth - .20f, 1.40f, .025f), stone, false);
                LocalBox(pivot, "Handle", new Vector3(-side * (leafWidth - .16f), 1.05f, -.10f), new Vector3(.04f, .25f, .05f), metal, false);
                if (i == 0) { c.LeafPivot = pivot; c.LeafCollider = leaf.GetComponent<Collider>(); c.ClosedRotation = pivot.localRotation; }
                else { c.SecondLeafPivot = pivot; c.SecondLeafCollider = leaf.GetComponent<Collider>(); c.SecondClosedRotation = pivot.localRotation; }
            }
            string label = RoomLabel(rooms[c.RoomB]);
            Sign(root, "DoorSignA", new Vector3(0, 2.72f, -.145f), Quaternion.Euler(0, 180, 0), label, c.Width + .2f, .28f);
            Sign(root, "DoorSignB", new Vector3(0, 2.72f, .145f), Quaternion.identity, RoomLabel(rooms[c.RoomA]), c.Width + .2f, .28f);
            c.ApplyOpenAmount(c.InitialOpen == "Open" ? 1 : 0);
            foreach (var child in c.LeafPivot.GetComponentsInChildren<Transform>()) child.gameObject.isStatic = false;
            if (c.SecondLeafPivot != null) foreach (var child in c.SecondLeafPivot.GetComponentsInChildren<Transform>()) child.gameObject.isStatic = false;
        }

        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collision = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.position = position; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true; return go;
        }
        static GameObject LocalBox(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collision = true)
        {
            var go = Box(parent, name, Vector3.zero, size, material, collision); go.transform.localPosition = position; go.transform.localRotation = Quaternion.identity; return go;
        }
        static GameObject Cylinder(Transform parent, string name, Vector3 p, float radius, float height, Material material, bool collision = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; go.transform.SetParent(parent, false); go.transform.position = p; go.transform.localScale = new Vector3(radius * 2, height / 2, radius * 2);
            go.GetComponent<Renderer>().sharedMaterial = material;
            // Unity's primitive cylinder uses a CapsuleCollider, which becomes a giant sphere
            // for a broad thin floor/ceiling. Use the actual cylinder hull for physical support.
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            if (collision) { var hull = go.AddComponent<MeshCollider>(); hull.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh; hull.convex = true; }
            go.isStatic = true; return go;
        }
        static void Sign(Transform parent, string id, Vector3 localPoint, Quaternion facing, string text, float width = 2, float height = .42f)
        {
            var sign = Group(parent, id); sign.localPosition = localPoint; sign.localRotation = facing;
            LocalBox(sign, "SignBacking", Vector3.zero, new Vector3(width, height, .03f), cyan, false);
            var lettering = new GameObject("Lettering"); lettering.transform.SetParent(sign, false); lettering.transform.localPosition = new Vector3(0, 0, -.021f);
            var tmp = lettering.AddComponent<TextMeshPro>(); if (font != null) tmp.font = font;
            tmp.text = text; tmp.fontSize = 2.6f; tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color(.10f, .14f, .17f);
            tmp.rectTransform.sizeDelta = new Vector2(width - .08f, height - .04f); tmp.enableAutoSizing = true; tmp.fontSizeMin = 1.1f; tmp.fontSizeMax = 2.6f;
        }
        static string RoomLabel(MansionRoom room)
        {
            if (room.RoomId.StartsWith("R_BED_", StringComparison.Ordinal)) return room.RoomId.Substring(6) + "  " + room.DisplayName.Replace(" private bedroom", " 개인실");
            var labels = new Dictionary<string, string> {
                {"R_HALL","중앙 홀"},{"R_VEST","현관"},{"R_EXT","외부 입구"},{"R_CE","동측 회랑"},{"R_CW","서측 회랑"},{"R_CN","북측 회랑"},
                {"R_TRIAL","재판실"},{"R_TRIAL_ENTRY","재판실 입구"},{"R_LIV_COR","생활 구역"},{"R_DINING","식당"},{"R_KITCHEN","주방"},{"R_FOOD","식품 창고"},
                {"R_LOUNGE","휴게실"},{"R_BATH_A","공용 욕실 A"},{"R_BATH_B","공용 욕실 B"},{"R_LIB_COR","도서 · 기록 구역"},{"R_LIBRARY","원형 도서관"},
                {"R_READING","독서실"},{"R_ARCHIVE","기록실"},{"R_CUL_COR","문화 구역"},{"R_MUSIC","음악실"},{"R_ARCADE","오락실"},{"R_THEATER","극장"},{"R_EXHIBIT","전시실"},
                {"R_NAT_COR","자연 구역"},{"R_GREEN","온실"},{"R_GARDEN","실내 정원"},{"R_FLOWER","보라 꽃 회랑"},{"R_WRK_COR","작업 · 정비 구역"},{"R_WORK","작업실"},
                {"R_STORAGE","대형 창고"},{"R_LAUNDRY","세탁실"},{"R_MACHINE","기계실"},{"R_GEN","발전기실"},{"R_MAINT","정비 복도"},{"R_MED_COR","의료 구역"},
                {"R_AID","응급 처치실"},{"R_RECOVERY","회복실"},{"R_AQU_COR","수경 구역"},{"R_POOL","실내 수영장"},{"R_FLOOD","얕은 물 회랑"},{"R_WATER","수질 관리실"},
                {"R_ANX_COR","별관 구역"},{"R_ANNEX","별관 로비"},{"R_CLOSED","폐쇄 객실동"},{"R_MEETING","옛 회의실"},{"R_BED_COR","개인실 01 — 18"},{"R_UP_E","개인실 연결 회랑"},
                {"R_BALCONY","중앙 홀 발코니"},{"R_STAIR","서비스 계단"},{"R_COLD","저온 창고"},{"R_ABANDON","폐기 대기 창고"},{"R_OBSERVE","전망실"},{"R_ASTRO","천체 관측실"},{"R_ROOF","옥상 정원"}
            };
            return labels.TryGetValue(room.RoomId, out var value) ? value : room.DisplayName;
        }
        static void LoadMaterials()
        {
            stone = Mat("Stone", "DCDDD7", .28f); tile = Mat("Tile", "B8C1C4", .48f); metal = Mat("Metal", "71828B", .6f, .8f);
            red = Mat("Carpet", "642B35", .08f); water = Mat("Water", "829EA5", .85f); wood = Mat("Wood", "A59B87", .3f);
            cloth = Mat("Cloth", "A7A9AA", .08f); purple = Mat("Plant", "66566F", .18f); cyan = Mat("Sign", "8DAEAE", .35f);
            black = Mat("Trial", "080C11", .04f); aged = Mat("Annex", "CAC9BE", .18f); glass = Mat("GlassOpaque", "B4C8CC", .78f);
            lightFace = Mat("Diffuser", "EBF4F2", .15f, 0, .5f);
        }
        static Material Mat(string name, string hex, float smooth, float metallic = 0, float emission = 0)
        {
            string path = "Assets/BASSLINE/Environment/Materials/M01/M01_" + name + ".mat";
            string id = "MAT_M01_" + name, hash = AtomicSaveStore.Hash(BuilderVersion + name + hex + smooth.ToString(CultureInfo.InvariantCulture));
            var old = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!ProductionImporter.CanWrite(ProductionImporter.LoadIndex(), id, path, hash)) return old;
            var shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) throw new InvalidOperationException("URP Lit shader is required");
            var mat = old != null ? old : new Material(shader); mat.shader = shader;
            ColorUtility.TryParseHtmlString("#" + hex, out var color); mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", smooth); mat.SetFloat("_Metallic", metallic);
            if (emission > 0) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * emission); }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (old == null) AssetDatabase.CreateAsset(mat, path); else EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets(); ProductionImporter.Track(ProductionImporter.LoadIndex(), id, path, hash, "FunctionalProxy"); return mat;
        }
    }
}
