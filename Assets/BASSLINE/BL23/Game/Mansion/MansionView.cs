using System;
using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Builds and drives the 3D mansion for one loop's <see cref="Layout"/>. Everything is generated in code:
    /// architecture (walls/floors/ceilings/windows/railings/stairs), doors, furniture, decor, lights per circuit,
    /// atmosphere volume and the courtroom. See Documentation/BL23/Contracts.md section 3.
    /// </summary>
    public sealed partial class MansionView : MonoBehaviour
    {
        // ------------------------------------------------------------------ public data
        public Layout Layout { get; private set; }
        /// <summary>Camera used for light culling / reflections / mood. Defaults to Camera.main.</summary>
        public Camera ViewCamera;
        /// <summary>Build statistics (filled by Build).</summary>
        public BuildStats Stats = new BuildStats();
        public CourtroomView Courtroom { get; private set; }
        public MansionAtmosphere Atmosphere { get; private set; }

        [Serializable]
        public sealed class BuildStats
        {
            public string DecorColliders = "";
            public float BuildMs; public int Rooms, Lights, ShadowLights, Renderers, Meshes, Triangles, Colliders, Furniture, Decor;
            public Dictionary<string, float> Phases = new Dictionary<string, float>();
            public override string ToString()
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"build {BuildMs:0} ms | rooms {Rooms} furniture {Furniture} decor {Decor} lights {Lights} (shadow {ShadowLights}) renderers {Renderers} meshes {Meshes} tris {Triangles} colliders {Colliders}");
                foreach (var kv in Phases) sb.Append($"\n  {kv.Key}: {kv.Value:0} ms");
                if (!string.IsNullOrEmpty(DecorColliders)) sb.Append("\n  decor colliders: ").Append(DecorColliders);
                return sb.ToString();
            }
        }

        // ------------------------------------------------------------------ internal per-room data
        internal sealed class RoomView
        {
            public Room Room; public Transform Root; public MansionPalette Pal; public RoomStyle Style;
            public Transform Anchor; public Bounds Bounds;
            public float FloorY, CeilY, WallTop;
            public readonly List<Renderer> Renderers = new List<Renderer>();
            public readonly List<LightRec> Lights = new List<LightRec>();
            public MeshBuilder Shell;            // walls/floor/ceiling
            public MeshBuilder Detail;           // small trims (teeth, dentils): rendered without shadow casting
            public System.Random Rng;
            public bool Visible = true;
            public readonly List<RectF> FloorHoles = new List<RectF>();
            public readonly List<RectF> CeilHoles = new List<RectF>();
            public readonly List<RectF> Blocked = new List<RectF>();   // furniture + door keep zones for decor
            /// <summary>Wall spans already taken by pilasters, pictures and sconces: (x, z, half-width, 0).</summary>
            public readonly List<Vector4> WallReserved = new List<Vector4>();
            public readonly List<Vector3> WallSlots = new List<Vector3>();      // points on solid wall faces at 1.7 m
            public readonly List<Vector3> WallSlotN = new List<Vector3>();
            public readonly List<Vector2> WallSlotRange = new List<Vector2>();
            public readonly List<Rigidbody> PhysProps = new List<Rigidbody>();      // small dressing props: kinematic until the player is near
            public readonly List<GameObject> StaticDecor = new List<GameObject>();  // static model decor merged per room after dressing
        }

        internal sealed class LightRec
        {
            public Light Light; public float Base; public float Range; public int Circuit; public bool Emergency; public bool Moon; public bool Fire;
            public float Flicker; public float Seed; public bool Shadows; public int Room; public bool Neon;
            public bool HasNight; public Color NightColor; public float NightBase; public bool SkyFill;   // daylight: the moon look it returns to at night
        }

        internal RoomView[] Rooms;
        internal readonly Dictionary<int, DoorView> Doors = new Dictionary<int, DoorView>();
        internal readonly Dictionary<int, GameObject> FurnitureGo = new Dictionary<int, GameObject>();
        internal readonly List<LightRec> AllLights = new List<LightRec>();
        internal readonly Dictionary<int, bool> CircuitOn = new Dictionary<int, bool>();
        internal float Darkness, Noise;
        internal PressView Press;
        internal SwitchboardView Switchboard;
        Transform _root;

        // ------------------------------------------------------------------ build
        /// <summary>Time the last mansion was built: impacts in the settling seconds after that are not breakage.</summary>
        public static float LastBuildTime = -100f;

        public static MansionView Build(Layout L, Transform parent)
        {
            LastBuildTime = Time.time;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var go = new GameObject("Mansion_Loop" + L.LoopId);
            if (parent != null) go.transform.SetParent(parent, false);
            var v = go.AddComponent<MansionView>();
            v.Layout = L;
            v._root = go.transform;
            try { v.Construct(sw); }
            catch (Exception e) { Debug.LogException(e); }
            v.Stats.BuildMs = (float)sw.Elapsed.TotalMilliseconds;
            return v;
        }

        void Phase(System.Diagnostics.Stopwatch sw, ref double last, string name)
        {
            double now = sw.Elapsed.TotalMilliseconds; Stats.Phases[name] = (float)(now - last); last = now;
        }

        void Construct(System.Diagnostics.Stopwatch sw)
        {
            double t = 0;
            MansionMats.Init();
            FurnitureFactory.NavShrunk = 0; FurnitureFactory.NavDropped = 0;
            foreach (var c in Layout.Circuits) CircuitOn[c.Id] = c.On;
            BuildGrids();
            CreateRooms();
            PlanDoorsAndHoles();
            Phase(sw, ref t, "plan");
            BuildShells();
            Phase(sw, ref t, "shells");
            BuildGrandHall();
            BuildStairs();
            Phase(sw, ref t, "hall+stairs");
            BuildDoors();
            Phase(sw, ref t, "doors");
            BuildAllFurniture();
            BuildOwnerThresholds();   // MansionView.Owners: doormats and what residents leave outside their doors
            Phase(sw, ref t, "furniture");
            BuildAllDecor();
            Phase(sw, ref t, "decor");
            BuildAllDress();
            Phase(sw, ref t, "dress");
            BuildCourtroom();
            Phase(sw, ref t, "courtroom");
            BuildLighting();
            Phase(sw, ref t, "lights");
            FinalizeShells();
            BatchStatics();
            Atmosphere = MansionAtmosphere.Create(this);
            Phase(sw, ref t, "finalize");
            CollectStats();
            ApplyPower();
        }

        void CollectStats()
        {
            Stats.Rooms = Layout.Rooms.Count;
            Stats.Lights = AllLights.Count;
            int sh = 0; foreach (var l in AllLights) if (l.Shadows) sh++;
            Stats.ShadowLights = sh;
            var rends = GetComponentsInChildren<Renderer>(true);
            Stats.Renderers = rends.Length;
            var meshes = new HashSet<Mesh>(); long tris = 0;
            foreach (var r in rends)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    meshes.Add(mf.sharedMesh);
                    var m = mf.sharedMesh;
                    for (int s = 0; s < m.subMeshCount; s++) tris += m.GetIndexCount(s) / 3;
                }
            }
            Stats.Meshes = meshes.Count; Stats.Triangles = (int)tris;
            Stats.Colliders = GetComponentsInChildren<Collider>(true).Length;
            Stats.Furniture = FurnitureGo.Count;
            int total = 0; var parts = new List<string>();
            foreach (var kv in DecorColliderCounts) { total += kv.Value; parts.Add(kv.Key + "=" + kv.Value); }
            Stats.DecorColliders = $"{total} added [{string.Join(", ", parts)}], {DecorCollidersSkipped} skipped (walkable/door); non-blocking tall furniture colliders shrunk {FurnitureFactory.NavShrunk}, removed {FurnitureFactory.NavDropped}";
        }

        // ------------------------------------------------------------------ public API (contract)
        public Vector3 ToWorld(P3 p) => new Vector3(p.x, Layout != null ? Layout.FloorY(p.f) : (p.f == -2 ? -16f : p.f == -1 ? -4.6f : p.f * 4.8f), p.z);

        public void SetDoor(int doorId, bool open, bool locked)
        {
            if (Doors.TryGetValue(doorId, out var d) && d != null) d.Set(open, locked);
        }

        public void SetCircuit(int circuitId, bool on)
        {
            CircuitOn[circuitId] = on;
            if (Switchboard != null) Switchboard.SetLever(circuitId, on);
            ApplyPower();
        }

        public void SetDarkness(float t)
        {
            Darkness = Mathf.Clamp01(t);
            ApplyPower();
            if (Atmosphere != null) Atmosphere.SetDarkness(Darkness);
        }

        public void SetNoise(float t)
        {
            Noise = Mathf.Clamp01(t);
            Shader.SetGlobalVector("_BL_Globals", new Vector4(Darkness, Noise, 0, 1));
            if (Atmosphere != null) Atmosphere.SetNoise(Noise);
        }

        internal readonly HashSet<int> Batched = new HashSet<int>();
        /// <summary>Fixed furniture whose visuals were merged into the room's per-material furniture meshes.</summary>
        internal readonly HashSet<int> Merged = new HashSet<int>();
        public void SetFurnitureState(int furnitureId, int damage, P3 pos, float yaw)
        {
            if (!FurnitureGo.TryGetValue(furnitureId, out var go) || go == null) return;
            // a piece merged into its room's furniture mesh keeps its place; damage only adds marks (no ghost copy)
            if (Merged.Contains(furnitureId) && furnitureId >= 0 && furnitureId < Layout.Furniture.Count)
            {
                var gp = go.transform.position; var np = ToWorld(pos);
                if ((gp - np).sqrMagnitude < 0.0025f && Mathf.Abs(Mathf.DeltaAngle(go.transform.eulerAngles.y, yaw)) < 1f)
                {
                    var fvm = go.GetComponent<FurnitureView>(); if (fvm != null) fvm.SetDamage(damage);
                    return;
                }
                Merged.Remove(furnitureId); Batched.Add(furnitureId);   // really moved: rebuild below
            }
            // a statically batched piece cannot move or tilt: swap in a fresh, unbatched copy the first time it changes
            if (Batched.Contains(furnitureId) && furnitureId >= 0 && furnitureId < Layout.Furniture.Count)
            {
                var f = Layout.Furniture[furnitureId]; var rv = f.Room >= 0 && f.Room < Rooms.Length ? Rooms[f.Room] : null;
                if (rv != null)
                {
                    try { var fresh = FurnitureFactory.Build(this, rv, f); go.SetActive(false); FurnitureGo[furnitureId] = fresh; go = fresh; Batched.Remove(furnitureId); }
                    catch (Exception e) { Debug.LogWarning("[Mansion] rebuild " + f.Type + ": " + e.Message); }
                }
            }
            var fv = go.GetComponent<FurnitureView>();
            go.transform.position = ToWorld(pos);
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (fv != null) fv.SetDamage(damage);
        }

        /// <summary>A fireplace someone has piled high: a much bigger, redder fire (a staged-warmth clue).</summary>
        public void StokeFire(int furnitureId)
        {
            var f = furnitureId >= 0 && furnitureId < Layout.Furniture.Count ? Layout.Furniture[furnitureId] : null; if (f == null || Rooms == null || f.Room < 0 || f.Room >= Rooms.Length || Rooms[f.Room] == null) return;
            var rv = Rooms[f.Room]; double yr = f.Yaw * Mathf.Deg2Rad;
            var p = ToWorld(f.Pos) + new Vector3((float)Math.Sin(yr), 0, (float)Math.Cos(yr)) * 0.35f + Vector3.up * 0.45f;
            AddLight(rv, p, new Color(1f, 0.45f, 0.15f), 7f, 9f, LightType.Point, false, 0.7f, fire: true);
            var mb = new MeshBuilder();
            for (int i = 0; i < 7; i++) FlameQuad(mb, p + new Vector3((i - 3) * 0.12f, -0.2f + (i % 2) * 0.05f, 0), 0.35f + (i % 3) * 0.08f, -2);
            Emit(rv, mb, "StokedFire", rv.Root, UnityEngine.Rendering.ShadowCastingMode.Off);
        }

        public void SetPress(float ram01, bool powered)
        {
            if (Press != null) Press.Set(ram01, powered);
        }

        public Transform RoomAnchor(int roomId)
        {
            if (Rooms == null || roomId < 0 || roomId >= Rooms.Length || Rooms[roomId] == null) return null;
            return Rooms[roomId].Anchor;
        }

        public Bounds RoomBounds(int roomId)
        {
            if (Rooms == null || roomId < 0 || roomId >= Rooms.Length || Rooms[roomId] == null) return new Bounds();
            return Rooms[roomId].Bounds;
        }

        public GameObject FurnitureObject(int furnitureId) => FurnitureGo.TryGetValue(furnitureId, out var g) ? g : null;

        /// <summary>Extra: walkable polyline for a stair (world space), from A to B. Actors can follow it for stair traversal.</summary>
        public Vector3[] StairPath(int stairId) => _stairPaths.TryGetValue(stairId, out var p) ? p : null;

        /// <summary>Extra: room id containing a world point (uses the layout lattice), -1 if none.</summary>
        public int RoomAtWorld(Vector3 p)
        {
            int f = FloorOfY(p.y);
            if (!_grids.TryGetValue(f, out var g)) return -1;
            return g.At(p.x, p.z);
        }

        /// <summary>Extra: which floor a world height belongs to.</summary>
        public int FloorOfY(float y)
        {
            if (y < -5.2f) return -2;
            if (y < -0.3f) return -1;
            if (y < 4.7f) return 0;
            return 1;
        }

        // ------------------------------------------------------------------ power / lights
        internal void ApplyPower()
        {
            // circuit multipliers for emissive shaders (electric groups 0..7)
            var p0 = new Vector4(); var p1 = new Vector4();
            for (int c = 0; c < 8; c++)
            {
                bool on = !CircuitOn.TryGetValue(c, out var o) || o;
                float v = on ? 1f : (c == 0 ? 0.14f : 0.0f);
                v *= 1f - Darkness;
                if (c < 4) p0[c] = v; else p1[c - 4] = v;
            }
            Shader.SetGlobalVector("_BL_CircuitPower0", p0);
            Shader.SetGlobalVector("_BL_CircuitPower1", p1);
            Shader.SetGlobalVector("_BL_Globals", new Vector4(Darkness, Noise, 0, 1));
            UpdateAmbient();
            foreach (var l in AllLights) ApplyLight(l);
        }

        internal float LightFactor(LightRec l)
        {
            if (l.Moon) return Mathf.Lerp(1f, 0.25f, Darkness);
            if (l.Fire) return 1f - Darkness;
            bool on = !CircuitOn.TryGetValue(l.Circuit, out var o) || o;
            float f;
            if (l.Emergency) f = on ? 0f : 1f;          // emergency lamps glow only when power is out
            else if (on) f = 1f;
            else f = l.Circuit == 0 ? 0.12f : 0f;      // circuit 0 (hall/corridors) keeps a dim emergency level
            if (l.Emergency) return f * (1f - Darkness * 0.6f);
            return f * (1f - Darkness);
        }

        void ApplyLight(LightRec l)
        {
            if (l.Light == null) return;
            float f = LightFactor(l);
            l.Light.intensity = l.Base * f;
            l.Light.enabled = f > 0.001f && _lightVisible.Contains(l);
        }

        // ------------------------------------------------------------------ runtime update: culling, flicker
        readonly HashSet<LightRec> _lightVisible = new HashSet<LightRec>();
        float _cullTimer;
        int _camRoom = -2;
        public int CameraRoom => _camRoom;
        /// <summary>CourtroomView detects the court cull pin by reflection (its CameraCeiling rises only when the pin exists).</summary>
        public const int CourtPinVersion = 1;

        Camera _clutterCam;
        float _perfT, _perfAcc; int _perfN; float _perfMax;
        static int _perfLog = -1;

        /// <summary>Small clutter lives on its own layer and is not drawn beyond ~13 m (it would be a few pixels anyway).</summary>
        void EnsureClutterCulling(Camera cam)
        {
            if (cam == null || cam == _clutterCam) return;
            var d = cam.layerCullDistances; if (d == null || d.Length != 32) d = new float[32];
            d[ClutterLayer] = ClutterCullDistance;
            cam.layerCullDistances = d; cam.layerCullSpherical = true;
            _clutterCam = cam;
        }

        /// <summary>Probe builds only: once a second, what the frame is spending (physics bodies awake, lights on, renderers).</summary>
        void PerfLog()
        {
            if (_perfLog < 0) { _perfLog = Array.IndexOf(Environment.GetCommandLineArgs(), "-bl23probe") >= 0 ? 1 : 0; }
            if (_perfLog == 0) return;
            float dt = Time.unscaledDeltaTime; _perfAcc += dt; _perfN++; _perfMax = Mathf.Max(_perfMax, dt);
            _perfT += dt; if (_perfT < 2f) return;
            int awake = 0, bodies = 0;
            foreach (var rb in FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) { if (rb.isKinematic) continue; bodies++; if (!rb.IsSleeping()) awake++; }
            int lightsOn = 0; foreach (var l in AllLights) if (l.Light != null && l.Light.enabled) lightsOn++;
            int rends = 0; if (Rooms != null) foreach (var rv in Rooms) if (rv != null && rv.Visible) rends += rv.Renderers.Count;
            FrameTimingManager.CaptureFrameTimings(); var ft = new FrameTiming[1]; uint got = FrameTimingManager.GetLatestTimings(1, ft);
            Debug.Log($"[MansionPerf] room {_camRoom} avg {1000f * _perfAcc / Math.Max(1, _perfN):0.0} ms max {1000f * _perfMax:0.0} | bodies {bodies} awake {awake} | lights on {lightsOn}/{AllLights.Count} | room renderers {rends}" + (got > 0 ? $" | cpu {ft[0].cpuFrameTime:0.0} gpu {ft[0].gpuFrameTime:0.0}" : ""));
            _perfT = 0; _perfAcc = 0; _perfN = 0; _perfMax = 0;
        }

        void Update()
        {
            var cam = ViewCamera != null ? ViewCamera : Camera.main;
            EnsureClutterCulling(cam);
            PerfLog();
            UpdateDaylight();
            UpdateSceneKeys();
            if (cam != null) UpdateProps(cam.transform.position);
            _cullTimer -= Time.unscaledDeltaTime;
            if (cam != null && _cullTimer <= 0f)
            {
                _cullTimer = 0.2f;
                Cull(cam.transform.position);
            }
            Flicker();
        }

        /// <summary>Distance/floor culling of lights and room renderers. Call manually for offline renders.</summary>
        public void Cull(Vector3 camPos)
        {
            if (Rooms == null) return;
            int f = FloorOfY(camPos.y);
            int room = RoomAtWorld(camPos);
            // court pin: a trial / court-vista lens anywhere inside the court's great well stays on floor -2 in the court room
            bool courtPin = Courtroom != null && Courtroom.Pinned && Courtroom.Holds(camPos);
            if (courtPin) { f = -2; room = Courtroom.RoomId; }
            // a lens that slips into a wall, a doorway gap or a chair back is still in the room it just left: keep that room
            // (otherwise every light and room on the floor is culled and the frame goes black)
            if (!courtPin && room < 0 && _camRoom >= 0 && _camRoom < Layout.Rooms.Count)
            {
                var lr = Layout.Rooms[_camRoom]; var LR = lr.Rect; var fi = Layout.Floor(lr.Floor);
                float ex = Math.Max(0, Math.Max(LR.x0 - camPos.x, camPos.x - LR.x1)), ez = Math.Max(0, Math.Max(LR.z0 - camPos.z, camPos.z - LR.z1));
                if (ex * ex + ez * ez < 2.5f * 2.5f && fi != null && Math.Abs(camPos.y - fi.BaseY) < 5f) { room = _camRoom; f = lr.Floor; }
            }
            _camRoom = room;
            bool inHall = room >= 0 && (Layout.Rooms[room].Type == RoomType.GrandHall || Layout.Rooms[room].Type == RoomType.Landing || Layout.Rooms[room].Type == RoomType.Stairwell);
            _lightVisible.Clear();
            // rank lights by distance, keep the nearest N (Forward+ can take many, shadows are the real cost)
            _tmpLights.Clear();
            foreach (var l in AllLights)
            {
                if (l.Light == null) continue;
                var rv = Rooms[l.Room];
                int lf = rv.Room.Floor;
                bool floorOk = lf == f || (inHall && Math.Abs(lf - f) == 1) || (lf == 0 && f == 1 && Layout.Rooms[l.Room].Type == RoomType.GrandHall) || (lf == 1 && f == 0 && inHall);
                if (!floorOk) continue;
                float d = Vector3.Distance(camPos, l.Light.transform.position) - l.Range;
                if (d > (courtPin ? 80f : 18f)) continue;
                _tmpLights.Add((d, l));
            }
            _tmpLights.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            int shadowBudget = 4;
            for (int i = 0; i < _tmpLights.Count && i < 56; i++)
            {
                var l = _tmpLights[i].Item2;
                _lightVisible.Add(l);
                if (l.Shadows)
                {
                    bool sh = shadowBudget > 0 && _tmpLights[i].Item1 < (courtPin ? 40f : 10f);
                    if (sh) shadowBudget--;
                    l.Light.shadows = sh ? LightShadows.Soft : LightShadows.None;
                }
            }
            foreach (var l in AllLights) ApplyLight(l);
            // portal depth from the lens's room (doors and stairs as portals, the hall void open to its ring): walls hide
            // whatever lies more than three rooms away, so the hall no longer draws nearly the whole house
            bool portals = room >= 0 && !courtPin && f != -2;
            if (portals) PortalDepth(room, inHall);
            // renderers: hide rooms on other floors (except around the hall void / stairwells)
            for (int i = 0; i < Rooms.Length; i++)
            {
                var rv = Rooms[i]; if (rv == null) continue;
                int rf = rv.Room.Floor;
                bool vis = rf == f || Math.Abs(rf - f) == 1 && (inHall || IsVertical(rv.Room)) ;
                if (vis && portals && _depth[i] > 3 && !(inHall && (IsVertical(rv.Room) || rv.Room.Void))) vis = false;
                // only the lens's room and its door neighbours cast shadows; the rest are too far for the shadow lights
                if (vis) RoomShadows(rv, !portals || _depth[i] <= 1 || (inHall && IsVertical(rv.Room) && rv.Room.Floor == f));
                // distance cull on the same floor: far rooms are hidden by walls and fog anyway (passages and the hall reach further)
                if (vis && rf == f && i != room)
                {
                    var R = rv.Room.Rect; float dx = Math.Max(0, Math.Max(R.x0 - camPos.x, camPos.x - R.x1)), dz = Math.Max(0, Math.Max(R.z0 - camPos.z, camPos.z - R.z1));
                    float lim = RoomInfo.IsPassage(rv.Room.Type) ? 48f : 30f;
                    if (dx * dx + dz * dz > lim * lim) vis = false;
                }
                if (rf == -2) vis = f == -2;
                if (f == -2) vis = rf == -2;
                if (vis != rv.Visible)
                {
                    rv.Visible = vis;
                    foreach (var r in rv.Renderers) if (r != null) r.enabled = vis;
                }
            }
            if (Atmosphere != null && room >= 0) Atmosphere.SetRoom(Layout.Rooms[room]);
        }
        readonly List<(float, LightRec)> _tmpLights = new List<(float, LightRec)>();

        bool IsVertical(Room r) => r.Type == RoomType.GrandHall || r.Type == RoomType.Landing || r.Type == RoomType.Stairwell;

        // ------------------------------------------------------------------ portal depth (culling)
        List<int>[] _adj; int[] _depth; int _depthKey = int.MinValue;
        readonly Dictionary<Renderer, ShadowCastingMode> _shadowMode = new Dictionary<Renderer, ShadowCastingMode>();
        bool[] _roomShadow;

        void PortalDepth(int room, bool inHall)
        {
            int key = room * 2 + (inHall ? 1 : 0);
            if (_adj == null)
            {
                _adj = new List<int>[Rooms.Length]; for (int i = 0; i < _adj.Length; i++) _adj[i] = new List<int>();
                void Link(int a, int b) { if (a < 0 || b < 0 || a >= _adj.Length || b >= _adj.Length || a == b) return; if (!_adj[a].Contains(b)) _adj[a].Add(b); if (!_adj[b].Contains(a)) _adj[b].Add(a); }
                foreach (var d in Layout.Doors) Link(d.RoomA, d.RoomB);
                foreach (var s in Layout.Stairs) Link(s.RoomA, s.RoomB);
                _depth = new int[Rooms.Length];
            }
            if (key == _depthKey) return;
            _depthKey = key;
            for (int i = 0; i < _depth.Length; i++) _depth[i] = 99;
            var q = new Queue<int>();
            _depth[room] = 0; q.Enqueue(room);
            if (inHall)
                for (int i = 0; i < Rooms.Length; i++)
                {
                    var r = Rooms[i]?.Room; if (r == null || i == room) continue;
                    if (r.Type == RoomType.GrandHall || r.Type == RoomType.Landing || r.Void) { _depth[i] = 1; q.Enqueue(i); }
                }
            while (q.Count > 0)
            {
                int a = q.Dequeue(); if (_depth[a] >= 3) continue;
                foreach (int b in _adj[a]) if (_depth[b] > _depth[a] + 1) { _depth[b] = _depth[a] + 1; q.Enqueue(b); }
            }
        }

        void RoomShadows(RoomView rv, bool on)
        {
            if (_roomShadow == null) { _roomShadow = new bool[Rooms.Length]; for (int i = 0; i < _roomShadow.Length; i++) _roomShadow[i] = true; }
            int id = rv.Room.Id; if (id < 0 || id >= _roomShadow.Length || _roomShadow[id] == on) return;
            _roomShadow[id] = on;
            foreach (var r in rv.Renderers)
            {
                if (r == null || r.gameObject.name == "Shell") continue;   // walls keep casting: no light leaks through them
                if (!on)
                {
                    if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
                    if (!_shadowMode.ContainsKey(r)) _shadowMode[r] = r.shadowCastingMode;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
                else if (_shadowMode.TryGetValue(r, out var m)) r.shadowCastingMode = m;
            }
        }

        void Flicker()
        {
            float t = Time.time;
            foreach (var l in _lightVisible)
            {
                if (l.Flicker <= 0 || l.Light == null || !l.Light.enabled) continue;
                float f = LightFactor(l);
                float n = Mathf.PerlinNoise(t * (l.Fire ? 7f : 13f), l.Seed);
                float k = l.Fire ? 0.82f + 0.3f * n : (n > 0.93f && l.Flicker > 0.5f ? 0.15f : 0.9f + 0.1f * n);
                if (Noise > 0 && !l.Moon && !l.Fire && Mathf.PerlinNoise(t * 20f, l.Seed + 3) > 1f - Noise * 0.4f) k *= 0.2f;
                l.Light.intensity = l.Base * f * Mathf.Lerp(1f, k, l.Flicker);
            }
        }

        void OnDestroy()
        {
            if (Atmosphere != null) Destroy(Atmosphere.gameObject);
        }
    }
}
