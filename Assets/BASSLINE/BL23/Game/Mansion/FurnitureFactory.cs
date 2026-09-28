using System;
using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Builds a visual + physical object for every FurnitureCatalog type, matching the footprint (W x D x H, +Z front,
    /// origin at floor centre). Scanned CC0 models where they fit the gothic look, procedural builds elsewhere.
    /// </summary>
    internal static partial class FurnitureFactory
    {
        internal sealed class Ctx
        {
            public MansionView View; public MansionView.RoomView Rv; public Furniture F; public FurnitureDef Def;
            public MansionPalette Pal; public System.Random Rng; public GameObject Go; public Transform T;
            public int Circuit => Rv.Room.Circuit;
            public float W => F.W; public float D => F.D; public float H => F.H;
            public int Var => F.Variant;
            public Color Tint;      // furniture tint (Furniture.Tint or palette fabric)
            public Color WoodC => Color.Lerp(Color.white, Pal.Wood * 2.3f, 0.45f);
            public Color Fabric => Tint;
            public Vector3 W2(Vector3 local) => T.TransformPoint(local);
        }

        static readonly Dictionary<string, (Mesh mesh, int[] slots)> _meshCache = new Dictionary<string, (Mesh, int[])>();
        public static void ClearCache() => _meshCache.Clear();

        /// <summary>Creates the furniture object (positioned, rotated, with collider/physics/material).</summary>
        public static GameObject Build(MansionView view, MansionView.RoomView rv, Furniture f)
        {
            var def = FurnitureCatalog.Get(f.Type);
            var go = new GameObject($"F{f.Id}_{f.Type}");
            go.transform.SetParent(rv.Root, false);
            go.transform.position = view.ToWorld(f.Pos);
            go.transform.rotation = Quaternion.Euler(0, f.Yaw, 0);
            var c = new Ctx { View = view, Rv = rv, F = f, Def = def, Pal = rv.Pal, Rng = new System.Random(f.Id * 7919 + (int)(view.Layout.Seed % 10007)), Go = go, T = go.transform };
            c.Tint = MansionPalette.Parse(f.Tint, rv.Pal.Fabric);
            { float lum = c.Tint.r * 0.3f + c.Tint.g * 0.55f + c.Tint.b * 0.15f; if (lum < 0.2f && lum > 0.001f) c.Tint = Color.Lerp(c.Tint * (0.2f / lum), c.Tint, 0.35f); }   // upholstery never reads as a black hole
            var mb = new MeshBuilder();
            bool handled = true;
            try { handled = Dispatch(c, mb); }
            catch (Exception e) { Debug.LogWarning($"[Furniture] {f.Type} #{f.Id}: {e.Message}\n{e.StackTrace}"); }
            if (f.Type != "GrandStair" && (!handled || (mb.Empty && go.transform.childCount == 0))) Generic(c, mb);
            if (!mb.Empty)
            {
                var mesh = mb.ToMesh(f.Type, out var slots);
                var vis = new GameObject("vis"); vis.transform.SetParent(go.transform, false);
                vis.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = vis.AddComponent<MeshRenderer>(); mr.sharedMaterials = MansionMats.Materials(slots);
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) rv.Renderers.Add(r);
            AddPhysics(c);
            var fv = go.AddComponent<FurnitureView>();
            fv.Init(view, f, def);
            Physicality.PhysicalProp.EnsureFurniture(go, f, def);
            return go;
        }

        static void AddPhysics(Ctx c)
        {
            var def = c.Def; var f = c.F;
            if (def == null) return;
            float h = f.H;
            if (f.Type == "GrandStair") return;
            if (f.Type == "PoolWater" || f.Type == "ShallowWater" || f.Type == "Rug") { AttachMaterial(c, def); return; }
            // only hand-sized pieces are loose rigidbodies (a chair, a side table, a nightstand): anything bigger stands firm, so
            // nobody shoves sofas, plants and easels around just by walking into them
            bool dynamicBody = def.Movable && def.Mass <= 10f && f.W * f.D <= 0.36f && (def.Blocks || f.Type == "Chair") && f.Type != "Rug" && f.Type != "DoorLogger";
            // chandelier furniture hangs above head height (>= 2.3 m clearance): no collider needed
            if (f.Type != "Chandelier" && h > 0.03f && c.Go.GetComponent<Collider>() == null && c.Go.GetComponentInChildren<Collider>() == null)
            {
                var bc = c.Go.AddComponent<BoxCollider>();
                bc.center = new Vector3(0, h / 2, 0);
                bc.size = new Vector3(Mathf.Max(0.05f, f.W), Mathf.Max(0.05f, h), Mathf.Max(0.05f, f.D));
                // tall static pieces the kernel marks non-blocking stand on walkable cells: shrink the collider until it
                // leaves every walkable cell and door opening free, or drop it
                if (!def.Blocks && !dynamicBody && h >= 1.2f && f.Id >= 0) FitToNav(c, bc);
            }
            // a bed's tall headboard and posts: a second, full-height collider at the head so no camera slips into it
            if (f.Type == "Bed" || f.Type == "InfirmaryBed")
            {
                var b0 = c.Go.GetComponent<BoxCollider>();
                var hb = c.Go.AddComponent<BoxCollider>(); hb.center = new Vector3(0, 0.95f, -f.D / 2 + 0.1f); hb.size = new Vector3(f.W, 1.9f, 0.2f);
                // a resident's low bed (platform, cot): the body collider stops at the mattress, where things lie on it
                var own = f.Type == "Bed" ? OwnerOf(c) : null;
                if (own != null && own.Bed != 0 && b0 != null) { float bt = OwnerBedTop(own.Bed) + 0.04f; b0.center = new Vector3(0, bt / 2, 0); b0.size = new Vector3(b0.size.x, bt, b0.size.z); }
            }
            if (dynamicBody)
            {
                var rb = c.Go.AddComponent<Rigidbody>();
                rb.mass = def.Mass; rb.linearDamping = 0.2f; rb.angularDamping = 0.4f;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                c.Go.AddComponent<FurnitureSync>().FurnitureId = c.F.Id;
                // a body warped into it (a teleport, someone sitting down) eases it aside instead of firing it across the room
                rb.maxDepenetrationVelocity = 0.8f;
                rb.Sleep();
                // and it stays frozen until the player is within reach (nobody else can shove it about unseen)
                MansionView.Freeze(rb); c.Rv.PhysProps.Add(rb);
            }
            AttachMaterial(c, def);
        }

        /// <summary>Static collider for a decor part of this furniture given in local space (world AABB, nav-aware).</summary>
        internal static void DecorBox(Ctx c, Vector3 localMin, Vector3 localMax, string label)
        {
            var b = new Bounds(c.T.TransformPoint(localMin), Vector3.zero);
            for (int i = 1; i < 8; i++) b.Encapsulate(c.T.TransformPoint(new Vector3((i & 1) == 0 ? localMin.x : localMax.x, (i & 2) == 0 ? localMin.y : localMax.y, (i & 4) == 0 ? localMin.z : localMax.z)));
            c.View.AddDecorCollider(c.Rv, b, label);
        }

        internal static int NavShrunk, NavDropped;
        static void FitToNav(Ctx c, BoxCollider bc)
        {
            bool rot = Mathf.Abs(Mathf.Round(c.F.Yaw / 90f)) % 2 == 1;
            float w = rot ? bc.size.z : bc.size.x, d = rot ? bc.size.x : bc.size.z;
            var p = c.T.position;
            var r = new RectF(p.x - w / 2, p.z - d / 2, p.x + w / 2, p.z + d / 2);
            if (c.View.FitDecorRect(c.Rv.Room.Floor, r, 0.3f, out var fit))
            {
                float k = fit.W / Mathf.Max(1e-3f, r.W);
                if (k < 0.999f) { bc.size = new Vector3(bc.size.x * k, bc.size.y, bc.size.z * k); NavShrunk++; }
            }
            else { if (Application.isPlaying) UnityEngine.Object.Destroy(bc); else UnityEngine.Object.DestroyImmediate(bc); NavDropped++; }
        }

        static void AttachMaterial(Ctx c, FurnitureDef def)
        {
            var pm = c.Go.AddComponent<PropMaterial>();
            pm.Mat = def.Mat; pm.Fragile = def.Fragile; pm.FurnitureId = c.F.Id;
        }

        // ------------------------------------------------------------------ dispatch
        static bool Dispatch(Ctx c, MeshBuilder mb)
        {
            if (DispatchOpenable(c, mb)) return true;   // containers with doors, drawers and lids that open
            if (DispatchSalon(c, mb)) return true;
            switch (c.F.Type)
            {
                case "LongTable": LongTable(c, mb); return true;
                case "Chair": Chair(c, mb); return true;
                case "Armchair": return ModelOr(c, "ArmChair_01", c.Fabric, 0.9f, () => Armchair(c, mb));
                case "Sofa": return ModelOr(c, "Sofa_01", c.Fabric, 0.9f, () => Sofa(c, mb));
                case "CoffeeTable": return ModelOr(c, "gothic_coffee_table", null, -1, () => SimpleTable(c, mb, 0.05f));
                case "RoundTable": RoundTable(c, mb); return true;
                case "Sideboard": return ModelOr(c, "GothicCommode_01", null, -1, () => Cabinet(c, mb, 2));
                case "Nightstand": ModelOr(c, "ClassicNightstand_01", null, -1, () => Cabinet(c, mb, 1)); Lamp(c, mb, new Vector3(0, c.H, 0), 0.22f); return true;
                case "Wardrobe": return ModelOr(c, "GothicCabinet_01", null, -1, () => Cabinet(c, mb, 2));
                case "Bed": Bed(c, mb); return true;
                case "Desk": Desk(c, mb); return true;
                case "ReadingTable": ReadingTable(c, mb); return true;
                case "Counter": Counter(c, mb, false); return true;
                case "Island": Counter(c, mb, true); return true;
                case "Stove": Stove(c, mb); return true;
                case "Sink": Sink(c, mb); return true;
                case "Fridge": Fridge(c, mb); return true;
                case "KnifeRack": KnifeRack(c, mb); return true;
                case "Bookshelf": Bookshelf(c, mb); return true;
                case "Fireplace": Fireplace(c, mb); return true;
                case "Piano": GrandPiano(c, mb); return true;
                case "Piano_Upright": UprightPiano(c, mb); return true;
                case "Organ": Organ(c, mb); return true;
                case "MusicStand": MusicStand(c, mb); return true;
                case "Drums": Drums(c, mb); return true;
                case "Stage": Stage(c, mb); return true;
                case "Seats": SeatRow(c, mb, 5, 1.0f); return true;
                case "AudSeat": SeatRow(c, mb, 1, 0.6f); return true;
                case "CostumeRack": CostumeRack(c, mb); return true;
                case "Mirror": StandingMirror(c, mb); return true;
                case "Planter": Planter(c, mb); return true;
                case "Bench": Bench(c, mb); return true;
                case "WaitBench": WaitBench(c, mb); return true;
                case "InfirmaryBed": InfirmaryBed(c, mb); return true;
                case "MedCabinet": MedCabinet(c, mb); return true;
                case "Washer": Washer(c, mb); return true;
                case "DryRack": DryRack(c, mb); return true;
                case "Workbench": Workbench(c, mb); return true;
                case "ToolWall": ToolWall(c, mb); return true;
                case "Crates": Crates(c, mb); return true;
                case "Shelves": Shelves(c, mb); return true;
                case "GameTable": GameTable(c, mb); return true;
                case "Arcade": Arcade(c, mb); return true;
                case "PoolTable": PoolTable(c, mb); return true;
                case "PoolWater": PoolWater(c, mb); return true;
                case "Lounger": Lounger(c, mb); return true;
                case "PumpUnit": PumpUnit(c, mb); return true;
                case "Terminal": Terminal(c, mb); return true;
                case "Switchboard": Switchboard(c, mb); return true;
                case "Generator": Generator(c, mb); return true;
                case "Press": Press(c, mb); return true;
                case "PressConsole": PressConsole(c, mb); return true;
                case "Pipes": Pipes(c, mb); return true;
                case "Easel": Easel(c, mb); return true;
                case "Pedestal": Pedestal(c, mb); return true;
                case "FrameWall": FrameWall(c, mb); return true;
                case "DressForm": DressForm(c, mb); return true;
                case "VanityDesk": VanityDesk(c, mb); return true;
                case "Pew": Pew(c, mb); return true;
                case "Altar": Altar(c, mb); return true;
                case "Candelabra": Candelabra(c, mb); return true;
                case "Statue": Statue(c, mb); return true;
                case "Chandelier": ChandelierFurniture(c, mb); return true;
                case "Clock": GrandfatherClock(c, mb); return true;
                case "ClockCase": ClockCase(c, mb); return true;
                case "FileCabinet": FileCabinet(c, mb); return true;
                case "Recorder": RecorderStand(c, mb); return true;
                case "Plant": Plant(c, mb); return true;
                case "Rug": Rug(c, mb); return true;
                case "DoorFrameFree": FreeDoor(c, mb); return true;
                case "Speaker": Speaker(c, mb); return true;
                case "TicketBooth": TicketBooth(c, mb); return true;
                case "ShallowWater": ShallowWater(c, mb); return true;
                case "RainFrame": RainFrame(c, mb); return true;
                case "ButlerDesk": ButlerDesk(c, mb); return true;
                case "TrialStand": TrialStand(c, mb); return true;
                case "GrandStair": return true;   // built by the stair builder
                case "Aquarium": Aquarium(c, mb); return true;
                case "Cart": Cart(c, mb, 0); return true;
                case "Trolley": Cart(c, mb, 1); return true;
                case "TeaCart": Cart(c, mb, 2); return true;
                case "Fountain": Fountain(c, mb); return true;
                case "Dollhouse": Dollhouse(c, mb); return true;
                case "DollShelf": DollShelf(c, mb); return true;
                case "Barrel": return ModelOr(c, "wine_barrel_01", null, -1, () => BarrelProc(c, mb));
                case "WineRack": WineRack(c, mb); return true;
                case "Boiler": Boiler(c, mb); return true;
                case "StuffedBeast": StuffedBeast(c, mb); return true;
                case "DoorLogger": DoorLogger(c, mb); return true;
            }
            return DispatchLeisure(c, mb);
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>Place a scanned model fitted to the footprint; fallback builder if the model is missing.</summary>
        internal static bool ModelOr(Ctx c, string id, Color? tint, float recolor, Action fallback, float yawOffset = 0f, Vector3? fit = null, Vector3? offset = null)
        {
            var m = Models.Get(id);
            if (m == null) { fallback(); return true; }
            var size = fit ?? new Vector3(c.W, c.H > 0 ? c.H * 1.08f : 0, c.D);
            var go = Models.Place(m, c.T, Vector3.zero, 0, size, tint, false, Models.Anchor.Bottom, 0.18f, recolor, yawOffset);
            go.transform.localPosition = offset ?? Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return true;
        }

        internal static Color Lighter(Color c, float k) => Color.Lerp(c, Color.white, k);
        internal static Color Darker(Color c, float k) => Color.Lerp(c, Color.black, k);

        static void TurnedLeg(MeshBuilder mb, Vector3 foot, float h, float r)
        {
            mb.Push(foot, 0);
            mb.Lathe(new[] { new Vector2(r * 0.7f, 0), new Vector2(r * 0.9f, h * 0.06f), new Vector2(r * 0.55f, h * 0.18f), new Vector2(r, h * 0.42f), new Vector2(r * 0.6f, h * 0.62f), new Vector2(r * 0.75f, h * 0.8f), new Vector2(r * 1.1f, h * 0.92f), new Vector2(r * 1.1f, h) }, 8, false, true);
            mb.Pop();
        }

        /// <summary>Claw-ish foot: a bulb with three toes (grotesque furniture feet).</summary>
        static void ClawFoot(MeshBuilder mb, Vector3 p, float r)
        {
            mb.Sphere(p + Vector3.up * r * 0.9f, r, 8, 6, 0.8f);
            for (int k = 0; k < 3; k++)
            {
                float a = k / 3f * Mathf.PI * 2 + 0.5f;
                mb.Ellipsoid(p + new Vector3(Mathf.Cos(a) * r * 0.9f, r * 0.3f, Mathf.Sin(a) * r * 0.9f), new Vector3(r * 0.45f, r * 0.3f, r * 0.45f), 6, 4);
            }
        }

        internal static void Candle(MeshBuilder mb, Vector3 p, float h, float r, int group, Vector3 worldWick, List<Vector3> flames)
        {
            mb.Set(S.Wax, Color.white);
            mb.Cyl(p, r, h, 8);
            // wax drips down the side
            mb.Cyl(p + new Vector3(r * 0.9f, h * 0.45f, 0), r * 0.25f, h * 0.5f, 5);
            mb.Cyl(p + new Vector3(-r * 0.4f, h * 0.2f, r * 0.8f), r * 0.2f, h * 0.7f, 5);
            flames?.Add(worldWick);
        }

        static void Books(MeshBuilder mb, Vector3 start, float length, float maxH, float depth, System.Random rnd, MansionPalette pal, bool leaning = true)
        {
            Color[] leather = { new Color(0.45f, 0.08f, 0.1f), new Color(0.1f, 0.25f, 0.15f), new Color(0.12f, 0.12f, 0.3f), new Color(0.35f, 0.22f, 0.1f), new Color(0.08f, 0.07f, 0.07f), new Color(0.55f, 0.4f, 0.15f), pal.Fabric, pal.Ink, pal.Neon * 0.6f };
            float x = 0;
            while (x < length - 0.03f)
            {
                float w = 0.025f + (float)rnd.NextDouble() * 0.045f;
                if (x + w > length) break;
                if (rnd.NextDouble() < 0.07) { x += w * 1.5f; continue; }          // gaps
                float h = maxH * (0.7f + (float)rnd.NextDouble() * 0.3f);
                float d = depth * (0.75f + (float)rnd.NextDouble() * 0.25f);
                var col = leather[rnd.Next(leather.Length)] * (0.7f + (float)rnd.NextDouble() * 0.5f);
                mb.Set(S.Books, col);
                var c = start + new Vector3(x + w / 2, h / 2, d / 2 - depth / 2);
                mb.Box(c, new Vector3(w, h, d), MeshBuilder.Faces.All);
                // gilt bands on the spine
                if (rnd.NextDouble() < 0.7)
                {
                    mb.Set(S.Gold, pal.Trim);
                    mb.Box(c + new Vector3(0, h * 0.32f, d / 2 + 0.001f), new Vector3(w * 0.9f, 0.012f, 0.004f));
                    mb.Box(c + new Vector3(0, -h * 0.32f, d / 2 + 0.001f), new Vector3(w * 0.9f, 0.012f, 0.004f));
                }
                x += w + 0.002f;
            }
        }

        internal static void Lamp(Ctx c, MeshBuilder mb, Vector3 localBase, float size, bool green = false)
        {
            // table lamp: brass stem + fabric shade glowing from inside; real light
            mb.Set(S.Brass, Color.white);
            mb.Push(localBase, 0);
            mb.Lathe(new[] { new Vector2(size * 0.35f, 0), new Vector2(size * 0.4f, size * 0.05f), new Vector2(size * 0.08f, size * 0.12f), new Vector2(size * 0.05f, size * 0.9f), new Vector2(size * 0.08f, size) }, 10, false, true);
            Color shade = green ? new Color(0.1f, 0.5f, 0.25f) : Color.Lerp(c.Pal.Fabric, c.Pal.Warm, 0.5f);
            mb.Set(S.Glow, shade, MansionMats.GlowData(green ? 0.8f : 0.7f, 0.05f, 0, c.Circuit));
            mb.Lathe(new[] { new Vector2(size * 0.55f, size * 0.95f), new Vector2(size * 0.3f, size * 1.5f) }, 12);
            mb.Pop();
            var wp = c.W2(localBase + Vector3.up * size * 1.2f);
            c.View.AddLight(c.Rv, wp, c.Pal.Warm, 1.6f, 3.2f, LightType.Point, false, 0.05f);
        }

        static void Flames(Ctx c, MeshBuilder mb, List<Vector3> localWicks, float h, bool light = true, float intensity = 1.5f, float range = 3.5f)
        {
            // flames are emitted into the local mesh (billboarded in the shader); one warm fire light for the cluster
            if (localWicks.Count == 0) return;
            Vector3 sum = Vector3.zero;
            foreach (var w in localWicks) { MansionView.FlameQuad(mb, w, h, -2); sum += w; }
            if (light) c.View.AddLight(c.Rv, c.W2(sum / localWicks.Count + Vector3.up * 0.15f), c.Pal.Warm, intensity, range, LightType.Point, false, 0.55f, fire: true);
        }

        // ------------------------------------------------------------------ generic fallback
        static void Generic(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.WoodDark, c.WoodC);
            float h = Mathf.Max(0.05f, c.H);
            mb.BevelBox(new Vector3(0, h / 2, 0), new Vector3(c.W, h, c.D), 0.02f);
        }

        // ------------------------------------------------------------------ tables & seating
        static void LongTable(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodCherry, c.WoodC);
            mb.BevelBox(new Vector3(0, H - 0.03f, 0), new Vector3(W, 0.06f, D), 0.015f);
            mb.Box(new Vector3(0, H - 0.12f, 0), new Vector3(W - 0.3f, 0.12f, D - 0.2f));
            int legs = W > 3 ? 3 : 2;
            for (int i = 0; i < legs; i++)
            {
                float x = Mathf.Lerp(-W / 2 + 0.25f, W / 2 - 0.25f, legs == 1 ? 0.5f : i / (float)(legs - 1));
                foreach (float z in new[] { -D / 2 + 0.2f, D / 2 - 0.2f }) { TurnedLeg(mb, new Vector3(x, 0, z), H - 0.18f, 0.06f); ClawFoot(mb, new Vector3(x, 0, z), 0.05f); }
            }
            // white tablecloth draping over the sides + velvet runner
            mb.Set(S.Linen, new Color(0.95f, 0.93f, 0.88f));
            mb.Box(new Vector3(0, H + 0.004f, 0), new Vector3(W + 0.1f, 0.008f, D + 0.1f), MeshBuilder.Faces.PY);
            foreach (float z in new[] { -1f, 1f }) mb.QuadAuto(new Vector3(-W / 2 - 0.05f, H + 0.008f, z * (D / 2 + 0.05f)), new Vector3(W / 2 + 0.05f, H + 0.008f, z * (D / 2 + 0.05f)), new Vector3(W / 2 + 0.05f, H - 0.3f, z * (D / 2 + 0.07f)), new Vector3(-W / 2 - 0.05f, H - 0.3f, z * (D / 2 + 0.07f)), new Vector3(0, 0, z));
            foreach (float x in new[] { -1f, 1f }) mb.QuadAuto(new Vector3(x * (W / 2 + 0.05f), H + 0.008f, -D / 2 - 0.05f), new Vector3(x * (W / 2 + 0.05f), H + 0.008f, D / 2 + 0.05f), new Vector3(x * (W / 2 + 0.07f), H - 0.3f, D / 2 + 0.05f), new Vector3(x * (W / 2 + 0.07f), H - 0.3f, -D / 2 - 0.05f), new Vector3(x, 0, 0));
            mb.Set(S.Velvet, c.Pal.Carpet);
            mb.Box(new Vector3(0, H + 0.012f, 0), new Vector3(W - 0.2f, 0.006f, 0.42f), MeshBuilder.Faces.PY);
            // table dressing: candelabra pair, plates, glasses, a centrepiece of dark flowers
            var flames = new List<Vector3>();
            foreach (float x in new[] { -W * 0.28f, W * 0.28f }) TableCandelabra(c, mb, new Vector3(x, H + 0.015f, 0), flames);
            Flames(c, mb, flames, 0.07f, true, 2.2f, 4.5f);
            int seats = Mathf.Max(2, Mathf.RoundToInt(W / 1.1f));
            for (int i = 0; i < seats; i++)
            {
                float x = -W / 2 + (i + 0.5f) * W / seats;
                foreach (float z in new[] { -D / 2 + 0.28f, D / 2 - 0.28f })
                {
                    mb.Set(S.Porcelain, Color.white);
                    mb.Push(new Vector3(x, H + 0.016f, z), 0); mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.1f, 0.004f), new Vector2(0.13f, 0.018f) }, 14); mb.Pop();
                    mb.Set(S.Gold, c.Pal.Trim);
                    mb.Push(new Vector3(x, H + 0.021f, z), 0); mb.Lathe(new[] { new Vector2(0.115f, 0.0f), new Vector2(0.128f, 0.012f) }, 14); mb.Pop();
                    mb.Set(S.Glass, Color.white);
                    mb.Push(new Vector3(x + 0.18f, H + 0.016f, z * 0.8f), 0); mb.Lathe(new[] { new Vector2(0.03f, 0), new Vector2(0.005f, 0.01f), new Vector2(0.004f, 0.08f), new Vector2(0.03f, 0.1f), new Vector2(0.035f, 0.16f) }, 8); mb.Pop();
                }
            }
            mb.Set(S.Porcelain, c.Pal.Trim);
            mb.Push(new Vector3(0, H + 0.015f, 0), 0); mb.Lathe(new[] { new Vector2(0.08f, 0), new Vector2(0.05f, 0.08f), new Vector2(0.15f, 0.18f), new Vector2(0.17f, 0.2f) }, 14); mb.Pop();
            var rnd = c.Rng;
            for (int k = 0; k < 9; k++)
            {
                mb.Set(S.Velvet, Color.Lerp(c.Pal.Neon, Color.black, 0.45f + (float)rnd.NextDouble() * 0.3f));
                mb.Sphere(new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.22f, H + 0.24f + (float)rnd.NextDouble() * 0.12f, ((float)rnd.NextDouble() - 0.5f) * 0.22f), 0.05f + (float)rnd.NextDouble() * 0.03f, 8, 5);
            }
        }

        internal static void TableCandelabra(Ctx c, MeshBuilder mb, Vector3 p, List<Vector3> flames)
        {
            mb.Set(S.Brass, c.Pal.Trim);
            mb.Push(p, 0);
            mb.Lathe(new[] { new Vector2(0.09f, 0), new Vector2(0.1f, 0.015f), new Vector2(0.03f, 0.04f), new Vector2(0.02f, 0.3f), new Vector2(0.04f, 0.32f), new Vector2(0.015f, 0.36f) }, 10, false, true);
            mb.Pop();
            for (int k = 0; k < 5; k++)
            {
                Vector3 arm = k == 0 ? new Vector3(0, 0.44f, 0) : new Vector3(Mathf.Cos(k * Mathf.PI / 2) * 0.15f, 0.38f, Mathf.Sin(k * Mathf.PI / 2) * 0.15f);
                mb.Set(S.Brass, c.Pal.Trim);
                if (k > 0) mb.Tube(new List<Vector3> { p + new Vector3(0, 0.3f, 0), p + arm * 0.5f + new Vector3(0, 0.3f, 0), p + arm }, 0.008f, 5);
                mb.Push(p + arm, 0); mb.Lathe(new[] { new Vector2(0.001f, -0.01f), new Vector2(0.03f, 0f), new Vector2(0.025f, 0.012f) }, 8); mb.Pop();
                float ch = 0.09f + (k * 13 % 5) * 0.012f;
                Candle(mb, p + arm + Vector3.up * 0.01f, ch, 0.012f, -2, p + arm + Vector3.up * (0.01f + ch), flames);
            }
        }

        static void Chair(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D;
            float seat = 0.47f;
            mb.Set(S.WoodDark, c.WoodC);
            foreach (float x in new[] { -W / 2 + 0.05f, W / 2 - 0.05f })
            {
                foreach (float z in new[] { -D / 2 + 0.05f, D / 2 - 0.05f })
                {
                    if (z < 0) continue;
                    TurnedLeg(mb, new Vector3(x, 0, z), seat - 0.04f, 0.024f);
                }
                // back posts rise tall, like horns
                mb.Box(new Vector3(x, 0.7f, -D / 2 + 0.05f), new Vector3(0.045f, 1.4f, 0.045f));
                mb.Set(S.Gold, c.Pal.Trim);
                mb.Push(new Vector3(x, 1.4f, -D / 2 + 0.05f), 0); mb.Lathe(new[] { new Vector2(0.03f, 0), new Vector2(0.035f, 0.03f), new Vector2(0.001f, 0.11f) }, 8); mb.Pop();
                mb.Set(S.WoodDark, c.WoodC);
            }
            mb.Box(new Vector3(0, seat - 0.05f, 0), new Vector3(W - 0.04f, 0.06f, D - 0.04f));
            // upholstered seat + tall back panel (velvet, palette tint)
            mb.Set(S.Velvet, c.Fabric);
            mb.BevelBox(new Vector3(0, seat + 0.02f, 0.02f), new Vector3(W - 0.08f, 0.08f, D - 0.1f), 0.03f);
            mb.BevelBox(new Vector3(0, 0.95f, -D / 2 + 0.07f), new Vector3(W - 0.14f, 0.72f, 0.05f), 0.02f);
            // carved crest with an eye
            mb.Set(S.WoodDark, c.WoodC);
            mb.Box(new Vector3(0, 1.33f, -D / 2 + 0.05f), new Vector3(W - 0.06f, 0.08f, 0.05f));
            mb.Set(S.Gold, c.Pal.Trim);
            mb.Ellipsoid(new Vector3(0, 1.33f, -D / 2 + 0.08f), new Vector3(0.06f, 0.03f, 0.015f), 10, 5);
        }

        static void Armchair(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.Velvet, c.Fabric);
            mb.BevelBox(new Vector3(0, 0.3f, 0.05f), new Vector3(c.W - 0.2f, 0.25f, c.D - 0.2f), 0.06f);
            mb.BevelBox(new Vector3(0, 0.7f, -c.D / 2 + 0.12f), new Vector3(c.W - 0.1f, 0.75f, 0.2f), 0.08f);
            foreach (float x in new[] { -1f, 1f }) mb.BevelBox(new Vector3(x * (c.W / 2 - 0.08f), 0.45f, 0), new Vector3(0.16f, 0.35f, c.D - 0.1f), 0.06f);
            mb.Set(S.WoodDark, c.WoodC);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) ClawFoot(mb, new Vector3(x * (c.W / 2 - 0.1f), 0, z * (c.D / 2 - 0.1f)), 0.05f);
        }

        static void Sofa(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.Velvet, c.Fabric);
            mb.BevelBox(new Vector3(0, 0.28f, 0.05f), new Vector3(c.W - 0.3f, 0.22f, c.D - 0.2f), 0.06f);
            mb.BevelBox(new Vector3(0, 0.62f, -c.D / 2 + 0.12f), new Vector3(c.W - 0.1f, 0.6f, 0.22f), 0.08f);
            foreach (float x in new[] { -1f, 1f }) mb.BevelBox(new Vector3(x * (c.W / 2 - 0.1f), 0.42f, 0), new Vector3(0.2f, 0.32f, c.D - 0.1f), 0.08f);
            for (int i = 0; i < 3; i++) mb.BevelBox(new Vector3((i - 1) * (c.W - 0.4f) / 3, 0.43f, 0.05f), new Vector3((c.W - 0.45f) / 3, 0.1f, c.D - 0.3f), 0.04f);
            mb.Set(S.WoodDark, c.WoodC);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) ClawFoot(mb, new Vector3(x * (c.W / 2 - 0.12f), 0, z * (c.D / 2 - 0.12f)), 0.05f);
        }

        static void SimpleTable(Ctx c, MeshBuilder mb, float top)
        {
            mb.Set(S.WoodCherry, c.WoodC);
            mb.BevelBox(new Vector3(0, c.H - top / 2, 0), new Vector3(c.W, top, c.D), 0.01f);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) TurnedLeg(mb, new Vector3(x * (c.W / 2 - 0.08f), 0, z * (c.D / 2 - 0.08f)), c.H - top, 0.03f);
        }

        static void RoundTable(Ctx c, MeshBuilder mb)
        {
            var m = Models.Get("round_wooden_table_01");
            if (m != null) Models.Place(m, c.T, c.T.position, c.F.Yaw, new Vector3(c.W, c.H * 1.05f, c.D), null).transform.SetParent(c.T, true);
            else
            {
                mb.Set(S.WoodCherry, c.WoodC);
                mb.Push(new Vector3(0, c.H - 0.04f, 0), 0); mb.Cyl(Vector3.zero, c.W / 2, 0.04f, 24); mb.Pop();
                mb.Push(Vector3.zero, 0); mb.Lathe(new[] { new Vector2(0.3f, 0), new Vector2(0.32f, 0.04f), new Vector2(0.08f, 0.12f), new Vector2(0.06f, 0.4f), new Vector2(0.1f, 0.55f), new Vector2(0.06f, c.H - 0.04f) }, 12); mb.Pop();
            }
            // lace cloth + tea set
            mb.Set(S.Linen, new Color(0.96f, 0.94f, 0.9f));
            mb.Push(new Vector3(0, c.H + 0.003f, 0), 0); mb.Lathe(new[] { new Vector2(c.W * 0.32f, 0), new Vector2(0.001f, 0.002f) }, 20); mb.Pop();
            TeaSet(c, mb, new Vector3(0, c.H + 0.005f, 0));
        }

        internal static void TeaSet(Ctx c, MeshBuilder mb, Vector3 p)
        {
            mb.Set(S.Porcelain, Lighter(c.Pal.Accent2, 0.7f));
            mb.Push(p, 0);
            mb.Lathe(new[] { new Vector2(0.04f, 0), new Vector2(0.075f, 0.03f), new Vector2(0.08f, 0.08f), new Vector2(0.05f, 0.13f), new Vector2(0.025f, 0.15f), new Vector2(0.001f, 0.165f) }, 12, true);
            mb.Rod(new Vector3(0.07f, 0.06f, 0), new Vector3(0.14f, 0.12f, 0), 0.008f, 5, true);
            mb.Pop();
            for (int k = 0; k < 3; k++)
            {
                float a = k / 3f * Mathf.PI * 2 + 0.4f;
                var cp = p + new Vector3(Mathf.Cos(a) * 0.22f, 0, Mathf.Sin(a) * 0.22f);
                mb.Set(S.Porcelain, Color.white);
                mb.Push(cp, 0); mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.06f, 0.004f), new Vector2(0.065f, 0.01f) }, 12); mb.Pop();
                mb.Push(cp + Vector3.up * 0.008f, 0); mb.Lathe(new[] { new Vector2(0.02f, 0), new Vector2(0.04f, 0.04f), new Vector2(0.042f, 0.05f) }, 10, true); mb.Pop();
                mb.Set(S.Glow, new Color(0.5f, 0.12f, 0.05f), MansionMats.GlowData(0.05f, 0, 0, -1));
                mb.Disc(cp + Vector3.up * 0.052f, 0.036f, 10, true);
            }
        }

        static void Bed(Ctx c, MeshBuilder mb)
        {
            // a resident's own bed style and bedding (FurnitureFactory.Owners); the gothic scan stays the default
            var own = OwnerOf(c);
            if (own != null && own.Bed != 0) { OwnerBed(c, mb, own); return; }
            var m = Models.Get("GothicBed_01");
            Color sheet = Lighter(c.Fabric, 0.15f);
            if (m != null)
            {
                var go = Models.Place(m, c.T, Vector3.zero, 0, new Vector3(c.W * 1.02f, 0, c.D * 1.02f), c.Fabric, false, Models.Anchor.Bottom, 0.1f, 0.85f);
                go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            }
            else
            {
                mb.Set(S.WoodDark, c.WoodC);
                mb.Box(new Vector3(0, 0.2f, 0), new Vector3(c.W, 0.2f, c.D));
                mb.Box(new Vector3(0, 0.65f, -c.D / 2 + 0.05f), new Vector3(c.W, 1.3f, 0.1f));
                foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Box(new Vector3(x * (c.W / 2 - 0.05f), 0.9f, z * (c.D / 2 - 0.05f)), new Vector3(0.08f, 1.8f, 0.08f));
                mb.Set(S.Velvet, c.Fabric);
                mb.BevelBox(new Vector3(0, 0.45f, 0.05f), new Vector3(c.W - 0.1f, 0.25f, c.D - 0.2f), 0.06f);
            }
            if (own != null) { OwnerBedding(c, mb, own, m != null ? 0.6f : 0.58f); return; }
            // pillows at the head and a velvet throw draped over the foot (it hangs down both sides: never a floating slab)
            {
                float top = m != null ? 0.6f : 0.58f;
                var throwCol = Color.Lerp(c.Fabric, new Color(0.25f, 0.06f, 0.07f), 0.45f);
                mb.Set(S.Linen, new Color(0.86f, 0.83f, 0.76f));
                foreach (float x in new[] { -c.W * 0.22f, c.W * 0.22f }) mb.BevelBox(new Vector3(x, top + 0.07f, -c.D / 2 + 0.36f), new Vector3(c.W * 0.4f, 0.13f, 0.34f), 0.06f);
                mb.Set(S.Velvet, throwCol);
                mb.BevelBox(new Vector3(0, top + 0.012f, c.D / 2 - 0.42f), new Vector3(c.W * 0.96f, 0.024f, 0.46f), 0.01f);
                foreach (float s in new[] { -1f, 1f }) mb.Box(new Vector3(s * (c.W * 0.48f + 0.012f), top - 0.13f, c.D / 2 - 0.42f), new Vector3(0.02f, 0.28f, 0.46f));
                mb.Set(S.Gold, new Color(0.62f, 0.5f, 0.32f));
                foreach (float s in new[] { -1f, 1f }) mb.Box(new Vector3(s * (c.W * 0.48f + 0.02f), top - 0.27f, c.D / 2 - 0.42f), new Vector3(0.012f, 0.03f, 0.46f));   // tassel fringe
            }
        }

        static void Desk(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.WoodCherry, c.WoodC);
            mb.BevelBox(new Vector3(0, c.H - 0.025f, 0), new Vector3(c.W, 0.05f, c.D), 0.01f);
            // the drawer pedestal: a hollow case with three drawers that open
            {
                var rig = new Rig(c); float pd = c.D - 0.05f, px = c.W / 2 - 0.22f;
                mb.Push(new Vector3(px, 0, 0), 0); Carcass(mb, 0.4f, pd, 0, c.H - 0.05f, 0.02f, 0.022f); mb.Pop();
                mb.Set(S.WoodCherry, c.WoodC);
                float rh = (c.H - 0.05f - 0.06f) / 3f;
                for (int k = 2; k >= 0; k--) DrawerRow(rig, 1, px - 0.18f, px + 0.18f, 0.03f + k * rh, 0.03f + (k + 1) * rh, pd * 0.5f - 0.022f, pd - 0.08f, c.WoodC);
                rig.Interior(new Vector3(px, c.H * 0.5f, pd * 0.5f + 0.25f), 0.9f);
            }
            foreach (float x in new[] { -1f }) foreach (float z in new[] { -1f, 1f }) TurnedLeg(mb, new Vector3(x * (c.W / 2 - 0.05f), 0, z * (c.D / 2 - 0.05f)), c.H - 0.05f, 0.025f);
            // a resident's desk is laid out for their own work (FurnitureFactory.Owners)
            { var own = OwnerOf(c); if (own != null) { OwnerDeskTop(c, mb, own); return; } }
            // leather writing pad, inkwell, papers, a candle
            mb.Set(S.LeatherRed, Color.white);
            mb.Box(new Vector3(-0.1f, c.H + 0.003f, 0.05f), new Vector3(0.55f, 0.006f, 0.35f));
            mb.Set(S.Paper, Color.white);
            mb.Box(new Vector3(-0.05f, c.H + 0.008f, 0.08f), new Vector3(0.21f, 0.004f, 0.28f));
            mb.Set(S.Obsidian, Color.white); mb.Cyl(new Vector3(0.28f, c.H, 0.1f), 0.03f, 0.05f, 8);
            var flames = new List<Vector3>();
            mb.Set(S.Brass, Color.white); mb.Push(new Vector3(-c.W / 2 + 0.15f, c.H, -0.1f), 0); mb.Lathe(new[] { new Vector2(0.05f, 0), new Vector2(0.05f, 0.01f), new Vector2(0.012f, 0.02f), new Vector2(0.012f, 0.06f), new Vector2(0.025f, 0.07f) }, 8); mb.Pop();
            Candle(mb, new Vector3(-c.W / 2 + 0.15f, c.H + 0.07f, -0.1f), 0.12f, 0.014f, -2, new Vector3(-c.W / 2 + 0.15f, c.H + 0.19f, -0.1f), flames);
            Flames(c, mb, flames, 0.06f, true, 0.9f, 2.4f);
        }

        static void ReadingTable(Ctx c, MeshBuilder mb)
        {
            SimpleTable(c, mb, 0.06f);
            mb.Set(S.Leather, new Color(0.25f, 0.4f, 0.25f));
            mb.Box(new Vector3(0, c.H + 0.003f, 0), new Vector3(c.W - 0.2f, 0.006f, c.D - 0.2f));
            Lamp(c, mb, new Vector3(-c.W * 0.3f, c.H, -c.D * 0.25f), 0.25f, true);
            Lamp(c, mb, new Vector3(c.W * 0.3f, c.H, -c.D * 0.25f), 0.25f, true);
            Books(mb, new Vector3(-0.2f, c.H + 0.006f, 0.1f), 0.35f, 0.25f, 0.18f, c.Rng, c.Pal);
        }

        static void Cabinet(Ctx c, MeshBuilder mb, int doors)
        {
            mb.Set(S.WoodDark, c.WoodC);
            mb.BevelBox(new Vector3(0, c.H / 2 + 0.05f, 0), new Vector3(c.W, c.H - 0.1f, c.D), 0.02f);
            mb.Set(S.WoodDark, Darker(c.WoodC, 0.2f));
            for (int i = 0; i < doors; i++)
            {
                float w = c.W / doors;
                mb.Box(new Vector3(-c.W / 2 + w * (i + 0.5f), c.H / 2, c.D / 2 + 0.01f), new Vector3(w - 0.08f, c.H - 0.3f, 0.02f));
            }
            mb.Set(S.Brass, Color.white);
            for (int i = 0; i < doors; i++) mb.Sphere(new Vector3(-c.W / 2 + c.W / doors * (i + 0.5f) + (i % 2 == 0 ? 0.1f : -0.1f), c.H / 2, c.D / 2 + 0.03f), 0.02f, 6, 4);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) ClawFoot(mb, new Vector3(x * (c.W / 2 - 0.06f), 0, z * (c.D / 2 - 0.06f)), 0.04f);
        }
    }
}
