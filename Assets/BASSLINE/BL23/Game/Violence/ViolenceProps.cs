using System.Collections.Generic;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Procedural meshes for the violence track's items: a silver revolver, a walnut dueling pistol, a double-barrelled
    /// shotgun, a hunting crossbow and its bolts, their ammunition, insulating tape, a torn clump of hair, a spent shell and
    /// a burnt paper patch. Same structure as MurderProps: grip at the origin, the business end along +Z, a "Grip" child,
    /// a box collider, a Rigidbody and a PropMaterial. Returns null for every other type.
    /// </summary>
    public static class ViolenceProps
    {
        static readonly Dictionary<string, (Mesh mesh, int[] slots)> _cache = new Dictionary<string, (Mesh, int[])>();
        static readonly HashSet<string> Types = new HashSet<string> { "Revolver", "DuelingPistol", "HuntingShotgun", "Crossbow", "Bolt", "Cartridges", "ShotShells", "PowderFlask", "BoltQuiver", "Tape", "HairStrands", "SpentShell", "Wadding" };
        public static bool Handles(string type) => type != null && Types.Contains(type);

        public static GameObject Create(ItemDef def, string itemId)
        {
            if (def == null || !Types.Contains(def.Type)) return null;
            var e = MeshOf(def);
            var go = new GameObject(string.IsNullOrEmpty(itemId) ? def.Type : itemId);
            var vis = new GameObject("vis"); vis.transform.SetParent(go.transform, false);
            vis.AddComponent<MeshFilter>().sharedMesh = e.mesh;
            vis.AddComponent<MeshRenderer>().sharedMaterials = MansionMats.Materials(e.slots);
            var grip = new GameObject("Grip").transform; grip.SetParent(go.transform, false);
            var b = e.mesh.bounds; var bc = go.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = Vector3.Max(b.size, new Vector3(0.01f, 0.01f, 0.01f));
            var rb = go.AddComponent<Rigidbody>(); rb.mass = Mathf.Max(0.01f, def.Mass); rb.linearDamping = 0.1f; rb.angularDamping = 0.2f;
            rb.collisionDetectionMode = def.Mass < 0.3f ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.Discrete;
            var pm = go.AddComponent<PropMaterial>(); pm.Mat = def.Mat; pm.ItemId = itemId; pm.Fragile = false; pm.Toughness = def.Heavy ? 2f : 1f;
            return go;
        }

        /// <summary>A bare visual (no physics) for the first-person hands or a probe camera.</summary>
        public static GameObject Visual(ItemDef def)
        {
            if (def == null || !Types.Contains(def.Type)) return null;
            var e = MeshOf(def); var go = new GameObject(def.Type + "_vm");
            go.AddComponent<MeshFilter>().sharedMesh = e.mesh; go.AddComponent<MeshRenderer>().sharedMaterials = MansionMats.Materials(e.slots);
            return go;
        }

        static (Mesh mesh, int[] slots) MeshOf(ItemDef def)
        {
            if (_cache.TryGetValue(def.Type, out var e) && e.mesh != null) return e;
            MansionMats.Init();
            var mb = new MeshBuilder(); Build(def.Type, mb);
            if (mb.Empty) { mb.Set(S.WoodLight, Color.white); mb.Box(new Vector3(0, 0.05f, 0), new Vector3(0.1f, 0.1f, 0.2f)); }
            e = (mb.ToMesh("Item_" + def.Type, out var slots), slots); _cache[def.Type] = e; return e;
        }

        static readonly Quaternion AlongZ = Quaternion.Euler(90, 0, 0);
        static void ZCyl(MeshBuilder mb, Vector3 at, float r, float len, int seg = 10, float rTop = -1f) { mb.Push(at, AlongZ, Vector3.one); mb.Cyl(Vector3.zero, r, len, seg, true, rTop); mb.Pop(); }

        static void Build(string type, MeshBuilder mb)
        {
            var walnut = new Color(0.36f, 0.2f, 0.11f); var silver = new Color(0.86f, 0.87f, 0.9f); var blued = new Color(0.16f, 0.17f, 0.2f); var brass = new Color(0.85f, 0.66f, 0.3f);
            switch (type)
            {
                case "Revolver":
                    mb.Set(S.WoodDark, walnut); mb.Push(new Vector3(0, 0.045f, -0.03f), Quaternion.Euler(-18, 0, 0), Vector3.one); mb.BevelBox(Vector3.zero, new Vector3(0.028f, 0.095f, 0.04f), 0.01f); mb.Pop();
                    mb.Set(S.Chrome, silver);
                    mb.BevelBox(new Vector3(0, 0.1f, 0.03f), new Vector3(0.026f, 0.038f, 0.1f), 0.006f);           // frame
                    ZCyl(mb, new Vector3(0, 0.103f, 0.035f), 0.021f, 0.045f, 12);                                    // cylinder
                    ZCyl(mb, new Vector3(0, 0.113f, 0.075f), 0.0085f, 0.14f, 8);                                     // barrel
                    mb.Box(new Vector3(0, 0.126f, 0.21f), new Vector3(0.003f, 0.006f, 0.006f));                     // front sight
                    mb.Set(S.Steel, blued); mb.Box(new Vector3(0, 0.128f, -0.012f), new Vector3(0.008f, 0.02f, 0.014f));   // hammer
                    mb.Torus(new Vector3(0, 0.072f, 0.02f), 0.014f, 0.0025f, 10, 4);                                 // trigger guard
                    mb.Set(S.Gold, brass); mb.Box(new Vector3(0, 0.09f, 0.003f), new Vector3(0.027f, 0.004f, 0.02f));   // engraved band
                    break;
                case "DuelingPistol":
                    mb.Set(S.WoodDark, walnut);
                    mb.Push(new Vector3(0, 0.045f, -0.04f), Quaternion.Euler(-28, 0, 0), Vector3.one); mb.BevelBox(Vector3.zero, new Vector3(0.03f, 0.11f, 0.045f), 0.012f); mb.Pop();
                    mb.BevelBox(new Vector3(0, 0.095f, 0.09f), new Vector3(0.026f, 0.03f, 0.22f), 0.008f);            // fore-stock
                    mb.Set(S.Steel, blued); ZCyl(mb, new Vector3(0, 0.112f, 0.0f), 0.011f, 0.31f, 8);                // octagonal-ish barrel
                    mb.Set(S.Gold, brass); mb.Sphere(new Vector3(0, 0.02f, -0.075f), 0.018f, 10, 6);                // pommel cap
                    mb.Box(new Vector3(0.016f, 0.11f, 0.0f), new Vector3(0.006f, 0.03f, 0.05f));                    // lock plate
                    mb.Set(S.Steel, blued); mb.Box(new Vector3(0.018f, 0.135f, -0.012f), new Vector3(0.006f, 0.022f, 0.01f));   // cock
                    mb.Torus(new Vector3(0, 0.07f, 0.01f), 0.015f, 0.0025f, 10, 4);
                    break;
                case "HuntingShotgun":
                    mb.Set(S.WoodDark, walnut);
                    mb.Push(new Vector3(0, 0.05f, -0.22f), Quaternion.Euler(-8, 0, 0), Vector3.one); mb.BevelBox(Vector3.zero, new Vector3(0.042f, 0.1f, 0.34f), 0.014f); mb.Pop();   // butt stock
                    mb.BevelBox(new Vector3(0, 0.085f, 0.22f), new Vector3(0.04f, 0.03f, 0.2f), 0.01f);              // fore-end
                    mb.Set(S.Steel, blued);
                    mb.BevelBox(new Vector3(0, 0.095f, 0.02f), new Vector3(0.044f, 0.045f, 0.1f), 0.008f);         // action
                    ZCyl(mb, new Vector3(-0.0115f, 0.108f, 0.06f), 0.011f, 0.72f, 8); ZCyl(mb, new Vector3(0.0115f, 0.108f, 0.06f), 0.011f, 0.72f, 8);
                    mb.Box(new Vector3(0, 0.121f, 0.42f), new Vector3(0.008f, 0.004f, 0.7f));                       // rib
                    mb.Torus(new Vector3(0, 0.065f, 0.0f), 0.018f, 0.003f, 10, 4);
                    mb.Set(S.Gold, brass); mb.Sphere(new Vector3(0, 0.126f, 0.775f), 0.003f, 6, 4);
                    break;
                case "Crossbow":
                    mb.Set(S.WoodDark, walnut);
                    mb.BevelBox(new Vector3(0, 0.06f, 0.1f), new Vector3(0.045f, 0.05f, 0.62f), 0.012f);             // tiller
                    mb.Push(new Vector3(0, 0.035f, -0.24f), Quaternion.Euler(-10, 0, 0), Vector3.one); mb.BevelBox(Vector3.zero, new Vector3(0.04f, 0.08f, 0.16f), 0.012f); mb.Pop();
                    mb.Set(S.Steel, blued);
                    // the prod: two arms swept back from the nose, and the string drawn back to the nut
                    mb.Rod(new Vector3(0, 0.08f, 0.4f), new Vector3(-0.3f, 0.08f, 0.33f), 0.012f, 6, true);
                    mb.Rod(new Vector3(0, 0.08f, 0.4f), new Vector3(0.3f, 0.08f, 0.33f), 0.012f, 6, true);
                    mb.Set(S.Linen, new Color(0.85f, 0.8f, 0.68f));
                    mb.Rod(new Vector3(-0.3f, 0.08f, 0.33f), new Vector3(0, 0.086f, 0.05f), 0.0025f, 4, false);
                    mb.Rod(new Vector3(0.3f, 0.08f, 0.33f), new Vector3(0, 0.086f, 0.05f), 0.0025f, 4, false);
                    mb.Set(S.Iron, new Color(0.25f, 0.25f, 0.27f));
                    mb.Torus(new Vector3(0, 0.08f, 0.45f), 0.045f, 0.006f, 12, 4);                                   // stirrup
                    mb.Box(new Vector3(0.03f, 0.06f, -0.14f), new Vector3(0.012f, 0.012f, 0.06f));                  // cranequin stub
                    mb.Set(S.Gold, brass); mb.Box(new Vector3(0, 0.088f, 0.05f), new Vector3(0.02f, 0.012f, 0.02f));   // nut
                    break;
                case "Bolt":
                    mb.Set(S.WoodLight, new Color(0.7f, 0.55f, 0.35f)); ZCyl(mb, new Vector3(0, 0.006f, -0.2f), 0.0055f, 0.34f, 6);
                    mb.Set(S.Steel, new Color(0.55f, 0.56f, 0.6f)); ZCyl(mb, new Vector3(0, 0.006f, 0.14f), 0.008f, 0.05f, 6, 0.0005f);
                    mb.Set(S.Leather, new Color(0.45f, 0.08f, 0.08f));
                    for (int i = 0; i < 3; i++) { mb.Push(new Vector3(0, 0.006f, -0.17f), Quaternion.Euler(0, 0, i * 120f), Vector3.one); mb.Box(new Vector3(0, 0.012f, 0), new Vector3(0.002f, 0.016f, 0.055f)); mb.Pop(); }
                    break;
                case "Cartridges":
                    mb.Set(S.Paper, new Color(0.72f, 0.64f, 0.46f)); mb.BevelBox(new Vector3(0, 0.022f, 0), new Vector3(0.07f, 0.044f, 0.05f), 0.004f);
                    mb.Set(S.Gold, brass); for (int i = 0; i < 3; i++) ZCyl(mb, new Vector3(-0.02f + i * 0.02f, 0.05f, -0.004f), 0.005f, 0.008f, 6);
                    break;
                case "ShotShells":
                    mb.Set(S.Paper, new Color(0.55f, 0.12f, 0.1f)); mb.BevelBox(new Vector3(0, 0.03f, 0), new Vector3(0.1f, 0.06f, 0.07f), 0.005f);
                    mb.Set(S.Paper, new Color(0.92f, 0.86f, 0.7f)); mb.Box(new Vector3(0, 0.03f, 0.0355f), new Vector3(0.07f, 0.03f, 0.002f));
                    break;
                case "PowderFlask":
                    mb.Set(S.Bone, new Color(0.82f, 0.74f, 0.58f)); mb.Push(new Vector3(0, 0.03f, 0), AlongZ, Vector3.one); mb.Lathe(new[] { new Vector2(0.028f, -0.05f), new Vector2(0.032f, 0f), new Vector2(0.02f, 0.05f), new Vector2(0.008f, 0.075f) }, 10, true, true); mb.Pop();
                    mb.Set(S.Gold, brass); ZCyl(mb, new Vector3(0, 0.03f, 0.075f), 0.008f, 0.015f, 8);
                    mb.Set(S.Leather, new Color(0.35f, 0.24f, 0.14f)); mb.Ellipsoid(new Vector3(0.05f, 0.02f, -0.01f), new Vector3(0.025f, 0.02f, 0.03f), 8, 5);
                    break;
                case "BoltQuiver":
                    mb.Set(S.Leather, new Color(0.33f, 0.2f, 0.12f)); mb.Cyl(new Vector3(0, 0, 0), 0.05f, 0.34f, 12, true, 0.045f);
                    mb.Set(S.Leather, new Color(0.45f, 0.08f, 0.08f)); for (int i = 0; i < 5; i++) { float a = i * 1.26f; mb.Box(new Vector3(Mathf.Cos(a) * 0.022f, 0.38f, Mathf.Sin(a) * 0.022f), new Vector3(0.003f, 0.06f, 0.02f)); }
                    mb.Set(S.WoodLight, new Color(0.7f, 0.55f, 0.35f)); for (int i = 0; i < 5; i++) { float a = i * 1.26f; mb.Rod(new Vector3(Mathf.Cos(a) * 0.022f, 0.3f, Mathf.Sin(a) * 0.022f), new Vector3(Mathf.Cos(a) * 0.022f, 0.43f, Mathf.Sin(a) * 0.022f), 0.004f, 5, true); }
                    break;
                case "Tape":
                    mb.Set(S.Rubber, new Color(0.1f, 0.1f, 0.11f)); mb.Torus(new Vector3(0, 0.012f, 0), 0.03f, 0.012f, 14, 6);
                    break;
                case "HairStrands":
                    mb.Set(S.Cloth, new Color(0.12f, 0.08f, 0.06f));
                    for (int i = 0; i < 5; i++)
                    {
                        var path = new List<Vector3>(); for (int k = 0; k <= 6; k++) { float t = k / 6f; path.Add(new Vector3(Mathf.Sin(t * 3f + i) * 0.02f, 0.002f, -0.06f + t * 0.14f + i * 0.004f)); }
                        mb.Tube(path, 0.0012f, 3, false);
                    }
                    break;
                case "SpentShell":
                    mb.Set(S.Paper, new Color(0.55f, 0.12f, 0.1f)); mb.Push(new Vector3(0, 0.011f, 0), AlongZ, Vector3.one); mb.Cyl(Vector3.zero, 0.0105f, 0.055f, 8); mb.Pop();
                    mb.Set(S.Gold, brass); mb.Push(new Vector3(0, 0.011f, -0.002f), AlongZ, Vector3.one); mb.Cyl(Vector3.zero, 0.0112f, 0.014f, 8); mb.Pop();
                    break;
                case "Wadding":
                    mb.Set(S.Paper, new Color(0.35f, 0.3f, 0.26f)); mb.Ellipsoid(new Vector3(0, 0.008f, 0), new Vector3(0.014f, 0.008f, 0.012f), 7, 4);
                    break;
            }
        }
    }
}
