using System.Collections.Generic;
using System.Linq;
using BL23.Game.Characters;
using UnityEditor;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>Builds 유스티's aquarium head (glass box, brass corners, water, goldfish, bubbles, gravel, weed) under the Head bone.</summary>
    public static class AquariumBuilder
    {
        public static AquariumHead Build(ActorRig rig, ProcBuilder pb, string dir)
        {
            var head = rig.Bone(HBone.Head);
            Vector3 size = pb.AquariumSize;
            var root = new GameObject("Aquarium");
            root.transform.SetParent(head, false);
            root.transform.position = rig.transform.TransformPoint(pb.AquariumCenter);
            root.transform.rotation = rig.transform.rotation;
            var aq = root.AddComponent<AquariumHead>();

            // --- glass
            var glassMat = LoadOrCreate(dir + "/NPC00_Glass.mat", "BL23/AquariumGlass");
            glassMat.SetColor("_Tint", new Color(0.72f, 0.95f, 1f, 0.07f));
            var glassMesh = CharacterBaker.SaveMesh(BoxMesh(size * 0.5f, true), dir + "/NPC00_GlassBox.asset");
            var glass = MakeGO("Glass", root.transform, glassMesh, glassMat);
            glass.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // --- water (75% full)
            float wt = 0.006f;
            Vector3 wHalf = new Vector3(size.x * 0.5f - wt, size.y * 0.5f * 0.78f, size.z * 0.5f - wt);
            Vector3 wCenter = new Vector3(0, -size.y * 0.5f + wt + wHalf.y, 0);
            var waterMat = LoadOrCreate(dir + "/NPC00_Water.mat", "BL23/AquariumWater");
            waterMat.SetFloat("_Height", wHalf.y * 2f);
            var waterMesh = CharacterBaker.SaveMesh(BoxMesh(wHalf, false, 10), dir + "/NPC00_WaterBox.asset");
            var water = MakeGO("Water", root.transform, waterMesh, waterMat);
            water.transform.localPosition = wCenter;
            water.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // --- brass corners + gravel + weed + fish via SDF meshing
            var M = new CharMesher();
            M.Bones.Add(new BoneDef { Name = "Root", Parent = -1 });
            int brass = M.Mat(new MatSpec { Name = "Brass", Color = new Color(0.72f, 0.55f, 0.24f), Gloss = 1.1f, GlossThreshold = 0.9f, Rim = 0.5f, Shade = new Color(0.55f, 0.46f, 0.42f), Shade2 = new Color(0.36f, 0.29f, 0.3f), OutlineWidth = 1.1f });
            var corners = new Part { Name = "Corners", Res = 0.0014f, Skin = SkinRule.RigidTo(0), AOScale = 0.3f };
            Vector3 h = size * 0.5f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 c = new Vector3(sx * h.x, sy * h.y, sz * h.z);
                        float L = 0.03f;
                        var bits = new List<Sdf>
                        {
                            Sdf.Box(c - new Vector3(sx * L * 0.5f, 0, 0), new Vector3(L * 0.5f + 0.004f, 0.006f, 0.006f), 0.002f),
                            Sdf.Box(c - new Vector3(0, sy * L * 0.5f, 0), new Vector3(0.006f, L * 0.5f + 0.004f, 0.006f), 0.002f),
                            Sdf.Box(c - new Vector3(0, 0, sz * L * 0.5f), new Vector3(0.006f, 0.006f, L * 0.5f + 0.004f), 0.002f),
                            Sdf.Sphere(c + new Vector3(sx, sy, sz) * 0.003f, 0.009f),
                        };
                        // curled filigree leaf on the front/back faces
                        Vector3 leafC = c - new Vector3(sx * 0.02f, sy * 0.02f, 0) + new Vector3(0, 0, sz * 0.004f);
                        bits.Add(Sdf.Torus(leafC, Quaternion.Euler(90f, 0, 0), 0.008f, 0.0022f));
                        bits.Add(Sdf.Sphere(c - new Vector3(sx * 0.035f, 0, 0), 0.004f));
                        bits.Add(Sdf.Sphere(c - new Vector3(0, sy * 0.035f, 0), 0.004f));
                        corners.Add(Sdf.Union(0.003f, bits), brass, 1);
                    }
            M.Parts.Add(corners);
            // thin brass frame along the top rim
            var rim = new Part { Name = "Rim", Res = 0.0015f, Skin = SkinRule.RigidTo(0) };
            rim.Add(Sdf.Sub(Sdf.Box(new Vector3(0, h.y - 0.002f, 0), new Vector3(h.x + 0.002f, 0.004f, h.z + 0.002f), 0.001f), Sdf.Box(new Vector3(0, h.y - 0.002f, 0), new Vector3(h.x - 0.006f, 0.02f, h.z - 0.006f), 0f)), brass, 1);
            M.Parts.Add(rim);
            // gravel
            var gravel = new Part { Name = "Gravel", Res = 0.0022f, Skin = SkinRule.RigidTo(0), AOScale = 0.4f };
            var rnd = new System.Random(7);
            var stones = new List<Sdf>();
            float gy = -h.y + wt + 0.008f;
            for (int i = 0; i < 70; i++)
            {
                float x = ((float)rnd.NextDouble() * 2 - 1) * (h.x - 0.016f);
                float z = ((float)rnd.NextDouble() * 2 - 1) * (h.z - 0.016f);
                float r = 0.006f + (float)rnd.NextDouble() * 0.007f;
                stones.Add(Sdf.Ellipsoid(new Vector3(x, gy + (float)rnd.NextDouble() * 0.004f, z), new Vector3(r, r * 0.6f, r * 0.85f)));
            }
            int gmat = M.Mat(new MatSpec { Name = "Gravel", Color = new Color(0.55f, 0.5f, 0.45f), Rim = 0.2f, OutlineWidth = 0.8f });
            gravel.Add(Sdf.Union(0.002f, stones), gmat, 1, p => Color.Lerp(new Color(0.75f, 0.68f, 0.6f), new Color(0.35f, 0.42f, 0.4f), Noise.Value(p * 180f)).linear);
            M.Parts.Add(gravel);
            // water weed
            var weed = new Part { Name = "Weed", Res = 0.0016f, Skin = SkinRule.RigidTo(0) };
            int wmat = M.Mat(new MatSpec { Name = "Weed", Color = new Color(0.25f, 0.55f, 0.35f), Rim = 0.4f, OutlineWidth = 0.8f });
            for (int i = 0; i < 5; i++)
            {
                float x = -h.x * 0.6f + i * 0.012f, z = -h.z * 0.45f + (i % 2) * 0.012f;
                var pts = new List<Vector3>();
                for (int k = 0; k <= 5; k++) pts.Add(new Vector3(x + Mathf.Sin(k * 0.9f + i) * 0.006f, gy + k * wHalf.y * 0.28f, z));
                weed.Add(new SLock(pts, pts.Select((p, k) => Mathf.Lerp(0.004f, 0.001f, k / 5f)).ToList(), new Vector3(x, gy, z + 0.2f), 0.4f), wmat, 1);
            }
            M.Parts.Add(weed);
            var decoMesh = M.Build("NPC00_AquariumDeco");
            var decoMats = new Material[M.Mats.Count];
            for (int i = 0; i < M.Mats.Count; i++) decoMats[i] = CharacterBaker.MakeMat(M.Mats[i], dir + "/NPC00_Aq" + i + "_" + M.Mats[i].Name + ".mat", null, null, pb);
            decoMesh.boneWeights = null;
            decoMesh = CharacterBaker.SaveMesh(decoMesh, dir + "/NPC00_AquariumDeco.asset");
            var deco = MakeGO("Deco", root.transform, decoMesh, decoMats[0]);
            deco.GetComponent<MeshRenderer>().sharedMaterials = decoMats;

            // --- goldfish (body + separate tail for wagging)
            var F = new CharMesher();
            F.Bones.Add(new BoneDef { Name = "Root", Parent = -1 });
            int orange = F.Mat(new MatSpec { Name = "FishOrange", Color = new Color(1f, 0.45f, 0.12f), Rim = 0.6f, Gloss = 0.5f, GlossThreshold = 0.92f, Shade = new Color(0.85f, 0.6f, 0.6f), OutlineWidth = 0.9f });
            int white = F.Mat(new MatSpec { Name = "FishWhite", Color = new Color(1f, 0.97f, 0.92f), Rim = 0.6f, Shade = new Color(0.85f, 0.8f, 0.85f), OutlineWidth = 0.9f });
            int black = F.Mat(new MatSpec { Name = "FishEye", Color = new Color(0.03f, 0.03f, 0.04f), Gloss = 1f, GlossThreshold = 0.85f, OutlineWidth = 0f });
            float fl = 0.052f;
            var body = new Part { Name = "FishBody", Res = 0.0007f, Skin = SkinRule.RigidTo(0), AOScale = 0.2f };
            var bodyS = Sdf.Union(0.004f, Sdf.Ellipsoid(Vector3.zero, new Vector3(fl * 0.27f, fl * 0.34f, fl * 0.5f)), Sdf.Ellipsoid(new Vector3(0, 0, fl * 0.35f), new Vector3(fl * 0.22f, fl * 0.26f, fl * 0.24f)));
            body.Add(bodyS, orange, 1);
            // white belly patch
            body.Add(Sdf.Inter(Sdf.Offset(bodyS, 0.0004f), Sdf.Plane(new Vector3(0, -fl * 0.05f, 0), Vector3.up)), white, 2);
            // dorsal + pectoral fins
            body.Add(Sdf.Ellipsoid(new Vector3(0, fl * 0.34f, -fl * 0.05f), new Vector3(fl * 0.02f, fl * 0.16f, fl * 0.2f), Quaternion.Euler(-20f, 0, 0)), orange, 1);
            for (int s = -1; s <= 1; s += 2)
                body.Add(Sdf.Ellipsoid(new Vector3(s * fl * 0.22f, -fl * 0.15f, fl * 0.12f), new Vector3(fl * 0.12f, fl * 0.02f, fl * 0.1f), Quaternion.Euler(0, s * 30f, s * 30f)), white, 2);
            for (int s = -1; s <= 1; s += 2)
                body.Add(Sdf.Sphere(new Vector3(s * fl * 0.19f, fl * 0.07f, fl * 0.42f), fl * 0.055f), black, 3);
            F.Parts.Add(body);
            var fishMesh = F.Build("NPC00_Fish");
            var T = new CharMesher { Mats = F.Mats };
            T.Bones.Add(new BoneDef { Name = "Root", Parent = -1 });
            var tail = new Part { Name = "FishTail", Res = 0.0007f, Skin = SkinRule.RigidTo(0) };
            for (int s = -1; s <= 1; s += 2)
                tail.Add(Sdf.Ellipsoid(new Vector3(0, s * fl * 0.14f, -fl * 0.2f), new Vector3(fl * 0.015f, fl * 0.2f, fl * 0.26f), Quaternion.Euler(s * -35f, 0, 0)), s < 0 ? white : orange, 1);
            T.Parts.Add(tail);
            var tailMesh = T.Build("NPC00_FishTail");
            var fishMats = new Material[F.Mats.Count];
            for (int i = 0; i < F.Mats.Count; i++) fishMats[i] = CharacterBaker.MakeMat(F.Mats[i], dir + "/NPC00_Fish" + i + ".mat", null, null, pb);
            fishMesh.boneWeights = null; tailMesh.boneWeights = null;
            fishMesh = CharacterBaker.SaveMesh(fishMesh, dir + "/NPC00_Fish.asset");
            tailMesh = CharacterBaker.SaveMesh(tailMesh, dir + "/NPC00_FishTail.asset");
            var fish = MakeGO("Goldfish", root.transform, fishMesh, fishMats[0]);
            fish.GetComponent<MeshRenderer>().sharedMaterials = fishMats;
            fish.transform.localPosition = wCenter;
            var tailGo = MakeGO("Tail", fish.transform, tailMesh, fishMats[0]);
            tailGo.GetComponent<MeshRenderer>().sharedMaterials = fishMats;
            tailGo.transform.localPosition = new Vector3(0, 0, -fl * 0.42f);

            // --- bubbles
            var bubbleMat = LoadOrCreate(dir + "/NPC00_Bubble.mat", "BL23/AquariumGlass");
            bubbleMat.SetColor("_Tint", new Color(0.9f, 1f, 1f, 0.25f));
            bubbleMat.SetFloat("_Fresnel", 1.6f);
            bubbleMat.SetFloat("_Streak", 0f);
            var sphere = CharacterBaker.SaveMesh(SphereMesh(12, 8), dir + "/NPC00_Bubble.asset");
            var bubbles = new List<Transform>();
            for (int i = 0; i < 10; i++)
            {
                var b = MakeGO("Bubble" + i, root.transform, sphere, bubbleMat);
                b.transform.localScale = Vector3.one * 0.006f;
                b.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bubbles.Add(b.transform);
            }

            aq.Fish = fish.transform;
            aq.FishTail = tailGo.transform;
            aq.Bubbles = bubbles.ToArray();
            aq.Water = water.GetComponent<MeshRenderer>();
            aq.Glass = glass.GetComponent<MeshRenderer>();
            aq.WaterCenter = wCenter;
            aq.WaterHalf = wHalf;
            // aquarium renders relative to the AquariumHead transform space -> reparent logic uses local positions under 'root'
            aq.enabled = true;
            EditorUtility.SetDirty(glassMat); EditorUtility.SetDirty(waterMat); EditorUtility.SetDirty(bubbleMat);
            return aq;
        }

        static Material LoadOrCreate(string path, string shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
            else m.shader = Shader.Find(shader);
            return m;
        }

        static GameObject MakeGO(string n, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>Box with per-face 0..1 uvs; subdivided top face (for waves).</summary>
        static Mesh BoxMesh(Vector3 h, bool uvPerFace, int topDiv = 1)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            void Face(Vector3 c, Vector3 u, Vector3 w, Vector3 nrm, int div)
            {
                int b = v.Count;
                for (int y = 0; y <= div; y++)
                    for (int x = 0; x <= div; x++)
                    {
                        float fx = x / (float)div, fy = y / (float)div;
                        v.Add(c + u * (fx * 2 - 1) + w * (fy * 2 - 1)); n.Add(nrm); uv.Add(new Vector2(fx, fy));
                    }
                for (int y = 0; y < div; y++)
                    for (int x = 0; x < div; x++)
                    {
                        int i = b + y * (div + 1) + x;
                        tri.Add(i); tri.Add(i + div + 1); tri.Add(i + 1);
                        tri.Add(i + 1); tri.Add(i + div + 1); tri.Add(i + div + 2);
                    }
            }
            Face(new Vector3(0, 0, h.z), new Vector3(-h.x, 0, 0), new Vector3(0, h.y, 0), Vector3.forward, 1);
            Face(new Vector3(0, 0, -h.z), new Vector3(h.x, 0, 0), new Vector3(0, h.y, 0), Vector3.back, 1);
            Face(new Vector3(h.x, 0, 0), new Vector3(0, 0, h.z), new Vector3(0, h.y, 0), Vector3.right, 1);
            Face(new Vector3(-h.x, 0, 0), new Vector3(0, 0, -h.z), new Vector3(0, h.y, 0), Vector3.left, 1);
            Face(new Vector3(0, h.y, 0), new Vector3(h.x, 0, 0), new Vector3(0, 0, h.z), Vector3.up, topDiv);
            Face(new Vector3(0, -h.y, 0), new Vector3(-h.x, 0, 0), new Vector3(0, 0, h.z), Vector3.down, 1);
            var m = new Mesh { name = "Box" };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            // fix winding against normals
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 fn = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(fn, n[t[i]]) < 0) { int s = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = s; }
            }
            m.triangles = t;
            m.RecalculateBounds();
            return m;
        }

        static Mesh SphereMesh(int seg, int rings)
        {
            var v = new List<Vector3>(); var tri = new List<int>(); var n = new List<Vector3>();
            for (int r = 0; r <= rings; r++)
                for (int s = 0; s <= seg; s++)
                {
                    float a = r / (float)rings * Mathf.PI, b = s / (float)seg * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(b));
                    v.Add(p * 0.5f); n.Add(p);
                }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < seg; s++)
                {
                    int i = r * (seg + 1) + s;
                    tri.Add(i); tri.Add(i + 1); tri.Add(i + seg + 1);
                    tri.Add(i + 1); tri.Add(i + seg + 2); tri.Add(i + seg + 1);
                }
            var m = new Mesh { name = "Sphere" };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(tri, 0);
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 fn = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(fn, n[t[i]]) < 0) { int s = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = s; }
            }
            m.triangles = t;
            var uv = new List<Vector2>(); for (int i = 0; i < v.Count; i++) uv.Add(new Vector2(0.5f, 0.5f));
            m.SetUVs(0, uv);
            m.RecalculateBounds();
            return m;
        }
    }
}
