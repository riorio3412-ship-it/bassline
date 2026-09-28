using System;
using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Creates a physical item for every ItemCatalog type: visual (procedural or CC0 scan), collider, Rigidbody and
    /// PropMaterial. Root origin = bottom centre (resting pose). A child "Grip" marks the hand hold: align it with the
    /// actor's HandAnchor (+Z = tool direction). Flashlights get a child spot light "Beam" (off by default).
    /// </summary>
    public static class PropFactory
    {
        static readonly Dictionary<string, (Mesh mesh, int[] slots, Vector3 grip, Vector3 gripEuler)> _cache = new Dictionary<string, (Mesh, int[], Vector3, Vector3)>();

        public static GameObject CreateItem(ItemDef def, string itemId)
        {
            if (def == null) return null;
            MansionMats.Init();
            var go = new GameObject(string.IsNullOrEmpty(itemId) ? def.Type : itemId);
            string model = ModelFor(def.Type);
            Vector3 grip = Vector3.zero, gripEuler = Vector3.zero;
            var m = model != null ? Models.Get(model) : null;
            if (m != null)
            {
                float fit = def.Size;
                var g = Models.Place(m, go.transform, Vector3.zero, 0, new Vector3(fit, fit, fit), null, false, Models.Anchor.Bottom, 0f, -1f, ModelYaw(def.Type));
                g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.identity;
                grip = new Vector3(0, fit * 0.3f, 0);
            }
            else
            {
                if (!_cache.TryGetValue(def.Type, out var e))
                {
                    var mb = new MeshBuilder();
                    Build(def, mb, out grip, out gripEuler);
                    if (mb.Empty) { mb.Set(S.WoodLight, Color.white); mb.Box(new Vector3(0, def.Size * 0.25f, 0), new Vector3(def.Size * 0.5f, def.Size * 0.5f, def.Size)); }
                    var mesh = mb.ToMesh("Item_" + def.Type, out var slots);
                    e = (mesh, slots, grip, gripEuler);
                    _cache[def.Type] = e;
                }
                grip = e.grip; gripEuler = e.gripEuler;
                var vis = new GameObject("vis"); vis.transform.SetParent(go.transform, false);
                vis.AddComponent<MeshFilter>().sharedMesh = e.mesh;
                var mr = vis.AddComponent<MeshRenderer>(); mr.sharedMaterials = MansionMats.Materials(e.slots);
            }
            var gripT = new GameObject("Grip").transform; gripT.SetParent(go.transform, false);
            gripT.localPosition = grip; gripT.localRotation = Quaternion.Euler(gripEuler);
            // collider from the visual bounds
            var b = LocalBounds(go);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = b.center; bc.size = Vector3.Max(b.size, new Vector3(0.01f, 0.01f, 0.01f));
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.01f, def.Mass);
            rb.linearDamping = 0.1f; rb.angularDamping = 0.2f;
            rb.collisionDetectionMode = def.Mass < 0.3f ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.Discrete;
            var pm = go.AddComponent<PropMaterial>();
            pm.Mat = def.Mat; pm.ItemId = itemId; pm.Fragile = def.Mat == Mat.Glass || def.Mat == Mat.Ceramic;
            pm.Toughness = def.Heavy ? 2f : 1f;
            if (def.Type == "Recorder")
            {
                // recording / playback lamp (off by default): PropFactory.SetRecording(go, true)
                var lamp = new GameObject("RecLamp"); lamp.transform.SetParent(go.transform, false);
                var lb = new MeshBuilder(); lb.Set(S.Glow, new Color(1f, 0.08f, 0.1f), MansionMats.GlowData(4f, 0.35f, 0, -1));
                var bb = LocalBounds(go); lb.Sphere(new Vector3(bb.center.x, bb.max.y + 0.006f, bb.center.z), 0.008f, 8, 5);
                lb.Set(S.Halo, new Color(1f, 0.1f, 0.12f), MansionMats.GlowData(0.6f, 0.35f, 0, -1)); MansionView.HaloQuad(lb, new Vector3(bb.center.x, bb.max.y + 0.006f, bb.center.z), 0.12f);
                lamp.AddComponent<MeshFilter>().sharedMesh = lb.ToMesh("RecLamp", out var ls);
                var lmr = lamp.AddComponent<MeshRenderer>(); lmr.sharedMaterials = MansionMats.Materials(ls); lmr.shadowCastingMode = ShadowCastingMode.Off;
                var plGo = new GameObject("RecLight"); plGo.transform.SetParent(lamp.transform, false); plGo.transform.localPosition = new Vector3(bb.center.x, bb.max.y + 0.05f, bb.center.z);
                var pl = plGo.AddComponent<Light>(); pl.type = LightType.Point; pl.range = 0.8f; pl.intensity = 0.6f; pl.color = new Color(1f, 0.15f, 0.12f);
                lamp.SetActive(false);
            }
            if (def.Light || def.Type == "Flashlight")
            {
                var beam = new GameObject("Beam"); beam.transform.SetParent(gripT, false); beam.transform.localPosition = new Vector3(0, 0, def.Size * 0.6f);
                var l = beam.AddComponent<Light>(); l.type = LightType.Spot; l.spotAngle = 42f; l.innerSpotAngle = 18f; l.range = 16f; l.intensity = 18f; l.color = new Color(1f, 0.95f, 0.85f);
                l.shadows = LightShadows.Soft; l.shadowNearPlane = 0.1f; l.enabled = false;
            }
            return go;
        }

        /// <summary>The hand hold transform of an item created by this factory.</summary>
        public static Transform GripOf(GameObject item) => item != null ? item.transform.Find("Grip") : null;
        /// <summary>Recorder state: red lamp (and a faint glow) while recording or playing back.</summary>
        public static void SetRecording(GameObject item, bool on)
        {
            var t = item != null ? item.transform.Find("RecLamp") : null;
            if (t != null) t.gameObject.SetActive(on);
        }
        /// <summary>Toggle a flashlight's beam.</summary>
        public static void SetLight(GameObject item, bool on)
        {
            var g = GripOf(item); var b = g != null ? g.Find("Beam") : null; var l = b != null ? b.GetComponent<Light>() : null;
            if (l != null) l.enabled = on;
        }

        static Bounds LocalBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(Vector3.up * 0.05f, Vector3.one * 0.1f);
            var inv = go.transform.worldToLocalMatrix;
            Bounds b = default; bool first = true;
            foreach (var r in rs)
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds; var m = inv * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        static string ModelFor(string t)
        {
            switch (t)
            {
                case "Wrench": return "adjustable_wrench";
                case "Hammer": return "cross_pein_hammer";
                case "Flashlight": return "vintage_flashlight";
                case "FirstAidKit": return "medical_box";
                case "Bucket": return "wooden_bucket_01";
                case "Bleach": return "bleach_bottle";
                case "Recorder": return "cassette_player";
                case "Camera": return "Camera_01";
                case "Thermos": return "modified_thermos";
                case "Bread": return "croissant";
                case "Vase": return "antique_ceramic_vase_01";
                case "WindupToy": return null;
            }
            return null;
        }
        static float ModelYaw(string t) => 0f;

        // ------------------------------------------------------------------ procedural items
        static void Blade(MeshBuilder mb, float len, float w, float handle, bool cleaver)
        {
            // lying flat along +Z; grip at z = handle/2
            mb.Set(S.WoodDark, new Color(0.35f, 0.22f, 0.15f));
            mb.BevelBox(new Vector3(0, 0.012f, handle / 2), new Vector3(0.024f, 0.024f, handle), 0.006f);
            mb.Set(S.Brass, Color.white); mb.Box(new Vector3(0, 0.012f, handle + 0.005f), new Vector3(0.03f, 0.03f, 0.01f));
            mb.Set(S.Steel, Color.white);
            float bl = len - handle;
            if (cleaver) mb.Box(new Vector3(0.0f, 0.004f, handle + bl / 2 + 0.01f), new Vector3(w, 0.006f, bl));
            else
            {
                var poly = new List<Vector2> { new Vector2(-w / 2, handle + 0.01f), new Vector2(-w / 2, handle + bl * 0.7f), new Vector2(0, handle + bl), new Vector2(w / 2, handle + bl * 0.55f), new Vector2(w / 2, handle + 0.01f) };
                poly.Reverse();
                mb.Push(new Vector3(0, 0.001f, 0), 0); mb.Prism(poly, 0, 0.004f, true, true); mb.Pop();
            }
        }

        static void Bottle(MeshBuilder mb, float h, float r, Color glass, Color label, bool cork)
        {
            mb.Set(S.Glass, glass);
            mb.Lathe(new[] { new Vector2(r * 0.9f, 0), new Vector2(r, 0.02f), new Vector2(r, h * 0.62f), new Vector2(r * 0.35f, h * 0.8f), new Vector2(r * 0.32f, h), new Vector2(r * 0.001f, h) }, 12);
            mb.Set(S.Paper, label); mb.Cyl(new Vector3(0, h * 0.2f, 0), r * 1.01f, h * 0.25f, 12, false);
            if (cork) { mb.Set(S.WoodLight, Color.white); mb.Cyl(new Vector3(0, h * 0.97f, 0), r * 0.3f, h * 0.08f, 8); }
        }

        static void Build(ItemDef d, MeshBuilder mb, out Vector3 grip, out Vector3 gripEuler)
        {
            float s = d.Size;
            grip = new Vector3(0, s * 0.3f, 0); gripEuler = Vector3.zero;
            switch (d.Type)
            {
                case "KitchenKnife": Blade(mb, s, 0.035f, 0.11f, false); grip = new Vector3(0, 0.012f, 0.055f); break;
                case "Cleaver": Blade(mb, s, 0.09f, 0.12f, true); grip = new Vector3(0, 0.012f, 0.06f); break;
                case "PaletteKnife": Blade(mb, s, 0.02f, 0.1f, false); grip = new Vector3(0, 0.012f, 0.05f); break;
                case "Chisel":
                    mb.Set(S.WoodLight, Color.white); mb.Push(Vector3.up * 0.015f, Quaternion.Euler(90, 0, 0), Vector3.one); mb.Cyl(Vector3.zero, 0.015f, 0.11f, 8); mb.Pop();
                    mb.Set(S.Steel, Color.white); mb.Box(new Vector3(0, 0.012f, 0.11f + (s - 0.11f) / 2), new Vector3(0.018f, 0.008f, s - 0.11f));
                    grip = new Vector3(0, 0.015f, 0.055f); break;
                case "Scissors":
                    mb.Set(S.Steel, Color.white);
                    foreach (float a in new[] { -8f, 8f }) { mb.Push(Vector3.up * 0.004f, a); mb.Box(new Vector3(0, 0, s * 0.62f), new Vector3(0.012f, 0.004f, s * 0.6f)); mb.Pop(); }
                    mb.Set(S.GlossPaint, new Color(0.1f, 0.1f, 0.1f));
                    foreach (float x in new[] { -0.025f, 0.025f }) mb.Torus(new Vector3(x, 0.005f, 0.05f), 0.022f, 0.006f, 10, 4);
                    grip = new Vector3(0, 0.006f, 0.05f); break;
                case "RollingPin":
                    mb.Set(S.WoodLight, Color.white);
                    mb.Push(new Vector3(0, 0.03f, 0), Quaternion.Euler(90, 0, 0), Vector3.one);
                    mb.Lathe(new[] { new Vector2(0.012f, 0), new Vector2(0.015f, 0.08f), new Vector2(0.03f, 0.09f), new Vector2(0.03f, s - 0.09f), new Vector2(0.015f, s - 0.08f), new Vector2(0.012f, s) }, 10, true, true);
                    mb.Pop(); grip = new Vector3(0, 0.03f, 0.04f); break;
                case "FryingPan":
                    mb.Set(S.Iron, new Color(0.15f, 0.15f, 0.15f));
                    mb.Push(new Vector3(0, 0, 0.14f), 0); mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.11f, 0), new Vector2(0.13f, 0.04f), new Vector2(0.125f, 0.045f), new Vector2(0.105f, 0.006f), new Vector2(0.001f, 0.006f) }, 18); mb.Pop();
                    mb.Set(S.WoodDark, Color.white); mb.Bar(new Vector3(0, 0.03f, -0.05f), new Vector3(0, 0.04f, -0.25f), 0.03f, 0.02f);
                    grip = new Vector3(0, 0.035f, -0.18f); gripEuler = new Vector3(0, 180, 0); break;
                case "Candlestick":
                    mb.Set(S.Steel, new Color(0.85f, 0.85f, 0.88f));
                    mb.Lathe(new[] { new Vector2(0.07f, 0), new Vector2(0.075f, 0.015f), new Vector2(0.02f, 0.04f), new Vector2(0.018f, s * 0.75f), new Vector2(0.03f, s * 0.8f), new Vector2(0.02f, s * 0.85f) }, 12, true);
                    mb.Set(S.Wax, Color.white); mb.Cyl(new Vector3(0, s * 0.85f, 0), 0.012f, s * 0.15f, 8);
                    grip = new Vector3(0, s * 0.4f, 0); gripEuler = new Vector3(-90, 0, 0); break;
                case "FirePoker":
                    mb.Set(S.Iron, new Color(0.12f, 0.12f, 0.12f));
                    mb.Rod(new Vector3(0, 0.01f, 0.05f), new Vector3(0, 0.01f, s), 0.008f, 6, true);
                    mb.Rod(new Vector3(0, 0.01f, s - 0.1f), new Vector3(0.06f, 0.01f, s - 0.05f), 0.007f, 5, true);
                    mb.Set(S.Brass, Color.white); mb.Torus(new Vector3(0, 0.01f, 0.03f), 0.03f, 0.008f, 10, 4);
                    grip = new Vector3(0, 0.01f, 0.08f); break;
                case "Trophy":
                    mb.Set(S.WoodDark, Color.white); mb.Box(new Vector3(0, 0.03f, 0), new Vector3(0.12f, 0.06f, 0.12f));
                    mb.Set(S.Gold, new Color(1f, 0.8f, 0.4f));
                    mb.Push(new Vector3(0, 0.06f, 0), 0); mb.Lathe(new[] { new Vector2(0.04f, 0), new Vector2(0.015f, 0.05f), new Vector2(0.012f, 0.14f), new Vector2(0.07f, 0.2f), new Vector2(0.08f, s - 0.06f), new Vector2(0.075f, s - 0.06f), new Vector2(0.001f, 0.2f) }, 14); mb.Pop();
                    foreach (float x in new[] { -1f, 1f }) mb.Torus(new Vector3(x * 0.09f, 0.22f, 0), 0.03f, 0.006f, 8, 4, x > 0 ? -90 : 90, x > 0 ? 90 : 270);
                    grip = new Vector3(0, 0.12f, 0); gripEuler = new Vector3(-90, 0, 0); break;
                case "Bottle": Bottle(mb, s, 0.04f, new Color(0.15f, 0.3f, 0.15f), new Color(0.9f, 0.85f, 0.7f), true); grip = new Vector3(0, s * 0.8f, 0); gripEuler = new Vector3(90, 0, 0); break;
                case "Soda": Bottle(mb, s, 0.035f, new Color(0.7f, 0.95f, 1f), new Color(0.9f, 0.2f, 0.3f), false); break;
                case "Beer": Bottle(mb, s, 0.035f, new Color(0.45f, 0.25f, 0.05f), new Color(0.95f, 0.85f, 0.3f), false); break;
                case "Vase":
                    mb.Set(S.Porcelain, new Color(0.9f, 0.9f, 0.95f));
                    mb.Lathe(new[] { new Vector2(0.06f, 0), new Vector2(0.12f, s * 0.35f), new Vector2(0.06f, s * 0.75f), new Vector2(0.05f, s * 0.9f), new Vector2(0.07f, s) }, 16, true);
                    break;
                case "Rope":
                    mb.Set(S.Linen, new Color(0.7f, 0.6f, 0.4f));
                    for (int i = 0; i < 5; i++) mb.Torus(new Vector3(0, 0.012f + i * 0.02f, 0), 0.12f - i * 0.005f, 0.012f, 18, 5);
                    grip = new Vector3(0.12f, 0.05f, 0); break;
                case "Scarf":
                    mb.Set(S.Velvet, new Color(0.6f, 0.1f, 0.3f));
                    mb.Box(new Vector3(0, 0.01f, 0), new Vector3(0.18f, 0.02f, s));
                    grip = new Vector3(0, 0.01f, -s * 0.4f); break;
                case "PipeSection":
                    mb.Set(S.RustyMetal, Color.white);
                    mb.Push(new Vector3(0, 0.03f, 0), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Lathe(new[] { new Vector2(0.03f, 0), new Vector2(0.03f, s) }, 10, false, false); mb.Lathe(new[] { new Vector2(0.024f, s), new Vector2(0.024f, 0) }, 10); mb.Pop();
                    grip = new Vector3(0, 0.03f, 0.1f); break;
                case "Book":
                case "Notebook":
                    mb.Set(S.Books, d.Type == "Book" ? new Color(0.45f, 0.08f, 0.1f) : new Color(0.15f, 0.15f, 0.2f));
                    float bw = d.Type == "Book" ? 0.2f : 0.1f, bd = s, bh = d.Type == "Book" ? 0.06f : 0.02f;
                    mb.Box(new Vector3(0, bh / 2, 0), new Vector3(bw, bh, bd));
                    mb.Set(S.Paper, new Color(0.95f, 0.92f, 0.85f)); mb.Box(new Vector3(0.004f, bh / 2, 0), new Vector3(bw - 0.006f, bh - 0.01f, bd - 0.01f));
                    mb.Set(S.Gold, Color.white); mb.Box(new Vector3(-bw / 2 - 0.001f, bh / 2, 0), new Vector3(0.002f, bh * 0.3f, bd * 0.8f));
                    break;
                case "Cup":
                case "Tea":
                    mb.Set(S.Porcelain, Color.white);
                    mb.Lathe(new[] { new Vector2(0.03f, 0), new Vector2(0.045f, 0.055f), new Vector2(0.047f, 0.065f) }, 12, true);
                    mb.Torus(new Vector3(0.05f, 0.035f, 0), 0.015f, 0.004f, 8, 4, -90, 90);
                    if (d.Type == "Tea") { mb.Set(S.Glow, new Color(0.45f, 0.2f, 0.05f), MansionMats.GlowData(0.05f, 0, 0, -1)); mb.Disc(new Vector3(0, 0.05f, 0), 0.042f, 10, true); }
                    break;
                case "Plate":
                    mb.Set(S.Porcelain, Color.white);
                    mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.08f, 0.003f), new Vector2(0.12f, 0.018f), new Vector2(0.125f, 0.02f) }, 18);
                    mb.Set(S.Gold, Color.white); mb.Torus(new Vector3(0, 0.017f, 0), 0.11f, 0.003f, 18, 3);
                    break;
                case "WineGlass":
                    mb.Set(S.Glass, Color.white);
                    mb.Lathe(new[] { new Vector2(0.035f, 0), new Vector2(0.005f, 0.01f), new Vector2(0.004f, 0.09f), new Vector2(0.035f, 0.12f), new Vector2(0.04f, s) }, 12);
                    break;
                case "Towel":
                    mb.Set(S.Linen, new Color(0.92f, 0.92f, 0.9f)); mb.BevelBox(new Vector3(0, 0.03f, 0), new Vector3(0.25f, 0.06f, 0.3f), 0.02f); break;
                case "Sheet":
                    mb.Set(S.Linen, Color.white); mb.BevelBox(new Vector3(0, 0.05f, 0), new Vector3(0.4f, 0.1f, 0.5f), 0.03f); break;
                case "Mop":
                    mb.Set(S.WoodLight, Color.white); mb.Rod(new Vector3(0, 0.02f, 0.2f), new Vector3(0, 0.02f, s), 0.013f, 6, true);
                    mb.Set(S.Linen, new Color(0.75f, 0.72f, 0.65f));
                    for (int i = 0; i < 12; i++) { float a = i / 12f * Mathf.PI * 2; mb.Rod(new Vector3(0, 0.02f, 0.2f), new Vector3(Mathf.Cos(a) * 0.08f, 0.02f + Mathf.Sin(a) * 0.02f, 0.0f), 0.012f, 4, false); }
                    grip = new Vector3(0, 0.02f, s * 0.7f); gripEuler = new Vector3(0, 180, 0); break;
                case "Bucket":
                    mb.Set(S.Plastic, new Color(0.3f, 0.5f, 0.7f)); mb.Lathe(new[] { new Vector2(0.11f, 0), new Vector2(0.14f, s), new Vector2(0.13f, s), new Vector2(0.1f, 0.01f) }, 16, true); break;
                case "Bleach":
                    mb.Set(S.Plastic, new Color(0.95f, 0.95f, 0.9f)); mb.BevelBox(new Vector3(0, s * 0.4f, 0), new Vector3(0.1f, s * 0.8f, 0.06f), 0.02f); mb.Set(S.Plastic, new Color(0.1f, 0.4f, 0.8f)); mb.Cyl(new Vector3(0, s * 0.8f, 0), 0.02f, s * 0.2f, 8); break;
                case "MasterKey":
                case "RoomKey":
                    mb.Set(S.Brass, Color.white);
                    int keys = d.Type == "MasterKey" ? 5 : 1;
                    mb.Torus(new Vector3(0, 0.004f, 0), 0.025f, 0.003f, 12, 4);
                    for (int i = 0; i < keys; i++) { mb.Push(new Vector3(0, 0.004f + i * 0.002f, 0), i * 25f - 50f); mb.Box(new Vector3(0, 0, 0.055f), new Vector3(0.006f, 0.003f, 0.06f)); mb.Torus(new Vector3(0, 0, 0.025f), 0.012f, 0.003f, 10, 3); mb.Box(new Vector3(0.006f, 0, 0.08f), new Vector3(0.008f, 0.003f, 0.012f)); mb.Pop(); }
                    grip = new Vector3(0, 0.005f, 0.02f); break;
                case "TheaterMask":
                    mb.Set(S.Porcelain, new Color(0.95f, 0.92f, 0.88f));
                    mb.Push(new Vector3(0, 0.1f, 0), Quaternion.Euler(-90, 0, 0), new Vector3(1, 0.4f, 1.3f)); mb.Sphere(Vector3.zero, 0.09f, 12, 8); mb.Pop();
                    mb.Set(S.Obsidian, Color.white); mb.Ellipsoid(new Vector3(-0.03f, 0.14f, 0.03f), new Vector3(0.02f, 0.01f, 0.012f), 6, 4); mb.Ellipsoid(new Vector3(0.03f, 0.14f, 0.03f), new Vector3(0.02f, 0.01f, 0.012f), 6, 4);
                    mb.Set(S.GlossPaint, new Color(0.8f, 0.05f, 0.2f)); mb.Ellipsoid(new Vector3(0, 0.07f, 0.035f), new Vector3(0.03f, 0.008f, 0.01f), 8, 4);
                    break;
                case "Cloak":
                case "Raincoat":
                case "SpareApron":
                    mb.Set(d.Type == "Raincoat" ? S.Plastic : S.Velvet, d.Type == "Cloak" ? new Color(0.25f, 0.02f, 0.08f) : d.Type == "Raincoat" ? new Color(0.9f, 0.8f, 0.1f) : new Color(0.9f, 0.9f, 0.88f));
                    mb.BevelBox(new Vector3(0, 0.04f, 0), new Vector3(0.45f, 0.08f, 0.35f), 0.03f); break;
                case "Envelope":
                case "Document":
                case "Invitation":
                    mb.Set(S.Paper, d.Type == "Invitation" ? new Color(0.1f, 0.05f, 0.08f) : new Color(0.93f, 0.9f, 0.82f));
                    mb.Box(new Vector3(0, 0.002f, 0), new Vector3(d.Type == "Document" ? 0.21f : 0.16f, 0.003f, d.Type == "Document" ? 0.29f : 0.11f));
                    if (d.Type != "Document") { mb.Set(S.Wax, new Color(0.6f, 0.02f, 0.08f)); mb.Cyl(new Vector3(0, 0.003f, 0), 0.015f, 0.004f, 10); }
                    if (d.Type == "Invitation") { mb.Set(S.Gold, Color.white); mb.Box(new Vector3(0, 0.0036f, 0), new Vector3(0.14f, 0.0005f, 0.004f)); }
                    grip = new Vector3(0, 0.003f, 0); break;
                case "Candy": mb.Set(S.GlossPaint, new Color(0.9f, 0.2f, 0.5f)); mb.Sphere(new Vector3(0, 0.012f, 0), 0.012f, 8, 5); mb.Set(S.Plastic, new Color(1f, 1f, 1f)); mb.Box(new Vector3(0.02f, 0.012f, 0), new Vector3(0.015f, 0.012f, 0.004f)); mb.Box(new Vector3(-0.02f, 0.012f, 0), new Vector3(0.015f, 0.012f, 0.004f)); break;
                case "Chocolate": mb.Set(S.GlossPaint, new Color(0.25f, 0.12f, 0.06f)); mb.Box(new Vector3(0, 0.006f, 0), new Vector3(0.05f, 0.012f, 0.1f)); mb.Set(S.Paper, new Color(0.6f, 0.1f, 0.12f)); mb.Box(new Vector3(0, 0.007f, 0.03f), new Vector3(0.052f, 0.014f, 0.05f)); break;
                case "Bread": mb.Set(S.Clay, new Color(0.85f, 0.6f, 0.35f)); mb.Ellipsoid(new Vector3(0, 0.04f, 0), new Vector3(0.07f, 0.045f, 0.1f), 10, 6); break;
                case "Snack": mb.Set(S.Plastic, new Color(0.9f, 0.15f, 0.05f)); mb.BevelBox(new Vector3(0, 0.03f, 0), new Vector3(0.14f, 0.06f, 0.2f), 0.02f); break;
                case "Sticker": mb.Set(S.GlossPaint, new Color(1f, 0.4f, 0.8f)); mb.Cyl(Vector3.zero, 0.025f, 0.001f, 12); break;
                case "WindupToy":
                    mb.Set(S.Steel, new Color(0.7f, 0.2f, 0.2f)); mb.BevelBox(new Vector3(0, 0.03f, 0), new Vector3(0.04f, 0.05f, 0.03f), 0.008f);
                    mb.Set(S.Steel, Color.white); mb.Box(new Vector3(0, 0.07f, 0), new Vector3(0.035f, 0.03f, 0.03f));
                    mb.Set(S.Brass, Color.white); mb.Rod(new Vector3(0, 0.03f, -0.015f), new Vector3(0, 0.03f, -0.035f), 0.003f, 4, false); mb.Box(new Vector3(0, 0.03f, -0.038f), new Vector3(0.025f, 0.008f, 0.003f));
                    mb.Set(S.Glow, new Color(1f, 0.2f, 0.1f), MansionMats.GlowData(1.5f, 0.2f, 0, -1)); mb.Sphere(new Vector3(-0.009f, 0.075f, 0.016f), 0.004f, 4, 3); mb.Sphere(new Vector3(0.009f, 0.075f, 0.016f), 0.004f, 4, 3);
                    break;
                case "PaperModel":
                    mb.Set(S.Paper, Color.white);
                    // folded crane
                    mb.Tri(new Vector3(0, 0.02f, -0.08f), new Vector3(0, 0.06f, 0), new Vector3(0, 0.02f, 0.08f));
                    mb.Tri(new Vector3(0, 0.02f, 0.08f), new Vector3(0, 0.06f, 0), new Vector3(0, 0.02f, -0.08f));
                    mb.Tri(new Vector3(0, 0.03f, 0), new Vector3(-0.08f, 0.05f, 0.01f), new Vector3(0, 0.035f, 0.03f));
                    mb.Tri(new Vector3(0, 0.035f, 0.03f), new Vector3(-0.08f, 0.05f, 0.01f), new Vector3(0, 0.03f, 0));
                    mb.Tri(new Vector3(0, 0.03f, 0), new Vector3(0.08f, 0.05f, 0.01f), new Vector3(0, 0.035f, 0.03f));
                    mb.Tri(new Vector3(0, 0.035f, 0.03f), new Vector3(0.08f, 0.05f, 0.01f), new Vector3(0, 0.03f, 0));
                    break;
                case "Button": mb.Set(S.Brass, Color.white); mb.Cyl(Vector3.zero, 0.01f, 0.004f, 10); break;
                case "HandWarmer": mb.Set(S.Plastic, new Color(0.95f, 0.5f, 0.1f)); mb.BevelBox(new Vector3(0, 0.008f, 0), new Vector3(0.07f, 0.016f, 0.1f), 0.006f); break;
                case "Flower":
                    mb.Set(S.Leaf, new Color(0.2f, 0.45f, 0.2f)); mb.Rod(new Vector3(0, 0.005f, 0), new Vector3(0, 0.005f, s), 0.003f, 4, false);
                    mb.Set(S.Porcelain, new Color(0.95f, 0.3f, 0.5f));
                    for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI * 2; mb.Ellipsoid(new Vector3(Mathf.Cos(a) * 0.02f, 0.012f, s + Mathf.Sin(a) * 0.02f), new Vector3(0.018f, 0.006f, 0.018f), 6, 3); }
                    grip = new Vector3(0, 0.005f, s * 0.3f); break;
                case "Fragment": mb.Set(S.Glass, Color.white); mb.Prism(new List<Vector2> { new Vector2(0, 0.05f), new Vector2(0.04f, -0.03f), new Vector2(-0.03f, -0.04f) }, 0, 0.004f, true, true); break;
                case "Fuse":
                    mb.Set(S.Porcelain, new Color(0.9f, 0.88f, 0.82f)); mb.Push(new Vector3(0, 0.012f, 0), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Cyl(new Vector3(0, -0.025f, 0), 0.01f, 0.05f, 10); mb.Pop();
                    mb.Set(S.Brass, Color.white); mb.Push(new Vector3(0, 0.012f, 0), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Cyl(new Vector3(0, -0.032f, 0), 0.011f, 0.008f, 10); mb.Cyl(new Vector3(0, 0.024f, 0), 0.011f, 0.008f, 10); mb.Pop();
                    break;
                case "Tripwire": mb.Set(S.Plastic, new Color(0.85f, 0.95f, 1f)); mb.Torus(new Vector3(0, 0.01f, 0), 0.03f, 0.008f, 12, 4); mb.Set(S.Glass, Color.white); mb.Torus(new Vector3(0, 0.02f, 0), 0.028f, 0.004f, 12, 3); break;
                case "Sedative": mb.Set(S.Glass, new Color(0.5f, 0.3f, 0.1f)); mb.Lathe(new[] { new Vector2(0.015f, 0), new Vector2(0.016f, 0.045f), new Vector2(0.008f, 0.05f), new Vector2(0.008f, 0.058f) }, 10, true); mb.Set(S.Paper, Color.white); mb.Cyl(new Vector3(0, 0.012f, 0), 0.0165f, 0.02f, 10, false); break;
                case "Recorder": mb.Set(S.Plastic, new Color(0.2f, 0.2f, 0.22f)); mb.BevelBox(new Vector3(0, 0.02f, 0), new Vector3(0.07f, 0.04f, 0.12f), 0.008f); break;
                case "Camera": mb.Set(S.Plastic, new Color(0.9f, 0.9f, 0.88f)); mb.BevelBox(new Vector3(0, 0.05f, 0), new Vector3(0.12f, 0.1f, 0.09f), 0.01f); mb.Set(S.Obsidian, Color.white); mb.Push(new Vector3(0, 0.05f, 0.045f), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Cyl(Vector3.zero, 0.03f, 0.03f, 12); mb.Pop(); break;
                case "Thermos": mb.Set(S.Steel, Color.white); mb.Lathe(new[] { new Vector2(0.035f, 0), new Vector2(0.036f, s * 0.8f), new Vector2(0.03f, s * 0.85f), new Vector2(0.03f, s), new Vector2(0.001f, s) }, 12); break;
                case "Flashlight":
                    mb.Set(S.Steel, new Color(0.2f, 0.2f, 0.2f)); mb.Push(new Vector3(0, 0.025f, 0), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Lathe(new[] { new Vector2(0.018f, 0), new Vector2(0.018f, s * 0.7f), new Vector2(0.03f, s * 0.85f), new Vector2(0.03f, s) }, 12, true, true); mb.Pop();
                    mb.Set(S.Glass, Color.white); mb.Disc(new Vector3(0, 0.025f, s + 0.001f), 0.026f, 12, true);
                    grip = new Vector3(0, 0.025f, s * 0.3f); break;
                case "FirstAidKit": mb.Set(S.Plastic, new Color(0.9f, 0.9f, 0.88f)); mb.BevelBox(new Vector3(0, 0.05f, 0), new Vector3(s, 0.1f, s * 0.65f), 0.01f); mb.Set(S.GlossPaint, new Color(0.85f, 0.05f, 0.08f)); mb.Box(new Vector3(0, 0.101f, 0), new Vector3(0.1f, 0.002f, 0.03f)); mb.Box(new Vector3(0, 0.101f, 0), new Vector3(0.03f, 0.002f, 0.1f)); break;
                case "Wrench": mb.Set(S.Steel, Color.white); mb.Box(new Vector3(0, 0.008f, s * 0.45f), new Vector3(0.025f, 0.012f, s * 0.9f)); mb.Torus(new Vector3(0, 0.008f, s * 0.95f), 0.025f, 0.008f, 10, 4, -120, 120); grip = new Vector3(0, 0.008f, s * 0.25f); break;
                case "Hammer": mb.Set(S.WoodLight, Color.white); mb.Box(new Vector3(0, 0.015f, s * 0.45f), new Vector3(0.025f, 0.02f, s * 0.9f)); mb.Set(S.Iron, Color.white); mb.Box(new Vector3(0, 0.02f, s * 0.95f), new Vector3(0.12f, 0.03f, 0.035f)); grip = new Vector3(0, 0.015f, s * 0.3f); break;
                default:
                    mb.Set(S.WoodLight, Color.white); mb.BevelBox(new Vector3(0, s * 0.2f, 0), new Vector3(s * 0.5f, s * 0.4f, s), 0.01f); break;
            }
        }
    }
}
