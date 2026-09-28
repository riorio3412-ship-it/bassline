using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BL23.Game.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.EditorTools.Characters
{
    /// <summary>Material description; turned into a ToonCharacter material at bake time.</summary>
    public sealed class MatSpec
    {
        public string Name;
        public Color Color = Color.white;
        public int Pattern;
        public Color PatternColor = Color.black;
        public Vector4 PatternParams = new Vector4(0.02f, 0.1f, 0, 0);
        public float Gloss, GlossThreshold = 0.965f, HairRing, Rim = 0.3f;
        public float SkinSSS = -1f;       // skin terminator warmth (-1: by material name)
        public bool VertexMasks;          // hair / skin masks per vertex in uv3 (scanned actors: one material)
        public bool Face;
        public Color? Shade;
        public Color? Shade2;
        public Color? Outline;
        public float OutlineWidth = 1.6f;
        public Texture2D BaseMap, Decal;
        public Vector4 DecalRect;
        public bool UseVertexColor = true;
        public float Emission;
        public Color EmissionColor = Color.black;
        public float Cavity = 0.75f;
        public bool DoubleSided;

        public string Key => $"{Name}|{ColorUtility.ToHtmlStringRGB(Color)}|{Pattern}|{ColorUtility.ToHtmlStringRGB(PatternColor)}|{Gloss}|{HairRing}|{Face}|{Emission}";
    }

    public sealed class Layer
    {
        public Sdf S;
        public int Mat;
        public int Priority;
        public Func<Vector3, Color> Tint;
    }

    public sealed class SkinRule
    {
        public int Rigid = -1;
        public int[] Bones;
        public float Power = 4f;
        public Func<Vector3, List<KeyValuePair<int, float>>> Custom;
        public static SkinRule RigidTo(int b) => new SkinRule { Rigid = b };
        public static SkinRule Blend(float power, params int[] bones) => new SkinRule { Bones = bones, Power = power };
    }

    /// <summary>Explicit triangle mesh piece for a Part (per-vertex colour tint, one material).</summary>
    public sealed class RawGeo
    {
        public List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();
        public List<int> T = new List<int>();
        public List<Color> C = new List<Color>();
        public int Mat;
        public float OutlineReduce;
    }

    public sealed class Part
    {
        public string Name;
        public List<Layer> Layers = new List<Layer>();
        public float Res = 0.004f;
        public SkinRule Skin;
        public bool Head;           // face uv + spherical face normals
        public float OutlineReduce;
        public Bounds? Clip;        // optional mesh region override
        public float AOScale = 1f;
        public float Keep = 0.07f;      // decimation target (fraction of triangles kept)
        public float Tolerance = 0.0011f; // max surface deviation (m) for decimation
        public Part Add(Sdf s, int mat, int prio = 0, Func<Vector3, Color> tint = null) { Layers.Add(new Layer { S = s, Mat = mat, Priority = prio, Tint = tint }); return this; }
        /// <summary>Explicit triangle geometry (hair clumps, cards): meshed as is, no SDF / decimation.</summary>
        public readonly List<RawGeo> Raw = new List<RawGeo>();
        public Part AddRaw(RawGeo g) { Raw.Add(g); return this; }
    }

    public sealed class BoneDef
    {
        public string Name;
        public int Parent;
        public Vector3 Pos;     // rest position (root space)
        public Vector3 Tail;    // segment end for distance skinning
        public float Radius;    // influence scale (optional)
    }

    public sealed class HeadFrame
    {
        public Vector3 Center;  // face projection center
        public float Scale;     // face uv scale (meters per uv unit)
        public Vector3 EllipsoidC, EllipsoidR; // for smooth face normals
        public float FaceZ;     // z beyond which a vertex is "front"
    }

    /// <summary>Meshes the parts of a character, assigns materials / weights / helper channels, and merges everything into one skinned mesh.</summary>
    public sealed class CharMesher
    {
        public List<MatSpec> Mats = new List<MatSpec>();
        public List<Part> Parts = new List<Part>();
        public List<BoneDef> Bones = new List<BoneDef>();
        public HeadFrame HeadF;
        public Sdf AoField;
        public string Log = "";

        public int Mat(MatSpec m)
        {
            string key = m.Key;
            for (int i = 0; i < Mats.Count; i++) if (Mats[i].Key == key) return i;
            Mats.Add(m);
            return Mats.Count - 1;
        }

        public int BoneIndex(string name)
        {
            for (int i = 0; i < Bones.Count; i++) if (Bones[i].Name == name) return i;
            return -1;
        }

        sealed class PartMesh
        {
            public List<Vector3> V, N;
            public List<int> T;
            public int[] TriMat;
            public Color[] Col;
            public BoneWeight[] W;
            public Vector4[] UV1;
        }

        public Mesh Build(string meshName)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var list = new List<Sdf>();
            foreach (var p in Parts) foreach (var l in p.Layers) list.Add(l.S);
            AoField = Sdf.Union(0f, list);

            var pms = new List<PartMesh>();
            foreach (var part in Parts)
            {
                var pm = MeshPart(part);
                if (pm != null) pms.Add(pm);
                Log += $"{part.Name}: {pm?.V.Count ?? 0} v, {(pm?.T.Count ?? 0) / 3} t ({sw.ElapsedMilliseconds} ms)\n";
            }

            // merge
            var V = new List<Vector3>(); var N = new List<Vector3>(); var C = new List<Color>(); var W = new List<BoneWeight>();
            var U1 = new List<Vector4>(); var U2 = new List<Vector3>(); var Tan = new List<Vector4>();
            var sub = new List<int>[Mats.Count];
            for (int i = 0; i < sub.Length; i++) sub[i] = new List<int>();
            foreach (var pm in pms)
            {
                int bse = V.Count;
                V.AddRange(pm.V); N.AddRange(pm.N); C.AddRange(pm.Col); W.AddRange(pm.W); U1.AddRange(pm.UV1);
                for (int i = 0; i < pm.V.Count; i++) { U2.Add(pm.V[i]); Tan.Add(new Vector4(pm.N[i].x, pm.N[i].y, pm.N[i].z, 1f)); }
                for (int t = 0; t < pm.T.Count / 3; t++)
                {
                    var s = sub[pm.TriMat[t]];
                    s.Add(bse + pm.T[t * 3]); s.Add(bse + pm.T[t * 3 + 1]); s.Add(bse + pm.T[t * 3 + 2]);
                }
            }
            var mesh = new Mesh { name = meshName };
            mesh.indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetColors(C); mesh.SetTangents(Tan);
            mesh.SetUVs(1, U1); mesh.SetUVs(2, U2);
            var uv0 = new List<Vector2>(V.Count);
            for (int i = 0; i < V.Count; i++) uv0.Add(Vector2.zero);
            mesh.SetUVs(0, uv0);
            mesh.subMeshCount = Mats.Count;
            for (int i = 0; i < Mats.Count; i++) mesh.SetTriangles(sub[i], i, false);
            mesh.boneWeights = W.ToArray();
            mesh.RecalculateBounds();
            Log += $"TOTAL {V.Count} verts, {CountTris(sub)} tris, {sw.ElapsedMilliseconds} ms\n";
            return mesh;
        }

        static int CountTris(List<int>[] s) { int n = 0; foreach (var l in s) n += l.Count / 3; return n; }

        PartMesh MeshRaw(Part part)
        {
            var pm = new PartMesh { V = new List<Vector3>(), N = new List<Vector3>(), T = new List<int>() };
            var mats = new List<int>(); var cols = new List<Color>(); var outl = new List<float>();
            foreach (var g in part.Raw)
            {
                int b = pm.V.Count;
                pm.V.AddRange(g.V); pm.N.AddRange(g.N);
                foreach (int i in g.T) pm.T.Add(b + i);
                for (int t = 0; t < g.T.Count / 3; t++) mats.Add(g.Mat);
                for (int i = 0; i < g.V.Count; i++) { cols.Add(i < g.C.Count ? g.C[i] : Color.white); outl.Add(g.OutlineReduce); }
            }
            if (pm.V.Count == 0) return null;
            pm.TriMat = mats.ToArray();
            int nv = pm.V.Count;
            pm.Col = new Color[nv];
            var ao = AoField;
            Parallel.For(0, nv, i =>
            {
                Vector3 p = pm.V[i], n = pm.N[i];
                float occ = 0f;
                float[] ds = { 0.006f, 0.014f, 0.028f };
                float[] ws = { 0.5f, 0.3f, 0.2f };
                for (int s = 0; s < 3; s++)
                {
                    float d = ds[s] * part.AOScale;
                    float f = ao != null ? ao.D(p + n * d) : d;
                    occ += ws[s] * Mathf.Clamp01((d - f) / d);
                }
                var c = cols[i]; c.a = Mathf.Clamp01(Mathf.Min(c.a, 1f - occ * 1.25f)); pm.Col[i] = c;
            });
            pm.UV1 = new Vector4[nv];
            for (int i = 0; i < nv; i++) pm.UV1[i] = new Vector4(0, 0, 0, Mathf.Max(part.OutlineReduce, outl[i]));
            pm.W = new BoneWeight[nv];
            var rule = part.Skin ?? SkinRule.RigidTo(0);
            Parallel.For(0, nv, i => pm.W[i] = Weigh(rule, pm.V[i]));
            return pm;
        }

        PartMesh MeshPart(Part part)
        {
            if (part.Layers.Count == 0) return part.Raw.Count > 0 ? MeshRaw(part) : null;
            var sdfs = new List<Sdf>();
            foreach (var l in part.Layers) sdfs.Add(l.S);
            Sdf field = sdfs.Count == 1 ? sdfs[0] : Sdf.Union(0f, sdfs);
            Bounds region = part.Clip ?? field.B;
            var r = SurfaceNets.Mesh(field, region, part.Res);
            if (r.V.Count == 0) return null;
            var pm = new PartMesh { V = r.V, N = r.N, T = r.T };
            int nv = r.V.Count, nt = r.T.Count / 3;

            // fix winding against SDF normals
            for (int t = 0; t < nt; t++)
            {
                int a = r.T[t * 3], b = r.T[t * 3 + 1], c = r.T[t * 3 + 2];
                Vector3 fn = Vector3.Cross(r.V[b] - r.V[a], r.V[c] - r.V[a]);
                Vector3 vn = r.N[a] + r.N[b] + r.N[c];
                if (Vector3.Dot(fn, vn) < 0) { r.T[t * 3 + 1] = c; r.T[t * 3 + 2] = b; }
            }

            // per-triangle material: outermost layer at the centroid
            pm.TriMat = new int[nt];
            var layers = part.Layers;
            float eps = part.Res * 0.6f;
            Parallel.For(0, nt, t =>
            {
                Vector3 c = (r.V[r.T[t * 3]] + r.V[r.T[t * 3 + 1]] + r.V[r.T[t * 3 + 2]]) / 3f;
                pm.TriMat[t] = layers[PickLayer(layers, c, eps)].Mat;
            });

            // decimate (quadric edge collapse), keeping material borders
            if (part.Keep < 0.999f)
            {
                var triList = pm.T;
                var tm = pm.TriMat;
                int target = Mathf.Max(200, (int)(nt * part.Keep));
                double maxErr = 40.0 * part.Tolerance * part.Tolerance;
                var remap = MeshDecimator.Decimate(r.V, ref triList, ref tm, target, maxErr, null, 30f);
                pm.T = triList; pm.TriMat = tm;
                pm.V = MeshDecimator.Remap(r.V, remap);
                pm.N = MeshDecimator.Remap(r.N, remap);
                r.V = pm.V; r.N = pm.N; r.T = pm.T;
                nv = pm.V.Count; nt = pm.T.Count / 3;
            }

            // per-vertex tint + AO
            pm.Col = new Color[nv];
            var ao = AoField;
            Parallel.For(0, nv, i =>
            {
                Vector3 p = r.V[i], n = r.N[i];
                int li = PickLayer(layers, p, eps);
                Color tint = layers[li].Tint != null ? layers[li].Tint(p) : Color.white;
                float occ = 0f;
                float[] ds = { 0.006f, 0.014f, 0.028f };
                float[] ws = { 0.5f, 0.3f, 0.2f };
                for (int s = 0; s < 3; s++)
                {
                    float d = ds[s] * part.AOScale;
                    float f = ao.D(p + n * d);
                    occ += ws[s] * Mathf.Clamp01((d - f) / d);
                }
                tint.a = Mathf.Clamp01(1f - occ * 1.25f);
                pm.Col[i] = tint;
            });

            // face uv / normals
            pm.UV1 = new Vector4[nv];
            if (part.Head && HeadF != null)
            {
                var hf = HeadF;
                for (int i = 0; i < nv; i++)
                {
                    Vector3 p = r.V[i];
                    Vector3 q = p - hf.Center;
                    float u = 0.5f - q.x / hf.Scale;
                    float v = 0.5f + q.y / hf.Scale;
                    float front = Mathf.Clamp01((p.z - hf.FaceZ) / (hf.Scale * 0.25f)) * Mathf.Clamp01(r.N[i].z * 2.2f + 0.3f);
                    pm.UV1[i] = new Vector4(u, v, front, part.OutlineReduce);
                    // smooth ellipsoid normals on the face so the anime face shades cleanly
                    Vector3 e = p - hf.EllipsoidC;
                    Vector3 en = new Vector3(e.x / (hf.EllipsoidR.x * hf.EllipsoidR.x), e.y / (hf.EllipsoidR.y * hf.EllipsoidR.y), e.z / (hf.EllipsoidR.z * hf.EllipsoidR.z)).normalized;
                    float wgt = 0.55f + 0.35f * Mathf.Clamp01(front * 1.3f);
                    r.N[i] = Vector3.Slerp(r.N[i], en, wgt).normalized;
                }
            }
            else
            {
                for (int i = 0; i < nv; i++) pm.UV1[i] = new Vector4(0, 0, 0, part.OutlineReduce);
            }

            // weights
            pm.W = new BoneWeight[nv];
            var rule = part.Skin ?? SkinRule.RigidTo(0);
            Parallel.For(0, nv, i => pm.W[i] = Weigh(rule, r.V[i]));
            SmoothWeights(pm, rule, 2);
            return pm;
        }


        static int PickLayer(List<Layer> layers, Vector3 p, float eps)
        {
            int best = 0; float bd = float.MaxValue;
            var ds = new float[layers.Count];
            for (int i = 0; i < layers.Count; i++)
            {
                ds[i] = layers[i].S.D(p);
                if (ds[i] < bd) { bd = ds[i]; best = i; }
            }
            for (int i = 0; i < layers.Count; i++)
                if (i != best && ds[i] < bd + eps && layers[i].Priority > layers[best].Priority) best = i;
            return best;
        }

        public BoneWeight Weigh(SkinRule rule, Vector3 p)
        {
            if (rule.Rigid >= 0) return new BoneWeight { boneIndex0 = rule.Rigid, weight0 = 1f };
            List<KeyValuePair<int, float>> ws;
            if (rule.Custom != null) ws = rule.Custom(p);
            else
            {
                ws = new List<KeyValuePair<int, float>>();
                foreach (int b in rule.Bones)
                {
                    var bd = Bones[b];
                    float d = SegDist(p, bd.Pos, bd.Tail);
                    if (bd.Radius > 0) d /= bd.Radius;
                    float w = 1f / Mathf.Pow(d + 0.004f, rule.Power);
                    ws.Add(new KeyValuePair<int, float>(b, w));
                }
            }
            return ToBW(ws);
        }

        public static BoneWeight ToBW(List<KeyValuePair<int, float>> ws)
        {
            ws.Sort((a, b) => b.Value.CompareTo(a.Value));
            int n = Mathf.Min(4, ws.Count);
            float sum = 0; for (int i = 0; i < n; i++) sum += ws[i].Value;
            if (sum <= 0) return new BoneWeight { boneIndex0 = ws.Count > 0 ? ws[0].Key : 0, weight0 = 1 };
            var bw = new BoneWeight();
            if (n > 0) { bw.boneIndex0 = ws[0].Key; bw.weight0 = ws[0].Value / sum; }
            if (n > 1) { bw.boneIndex1 = ws[1].Key; bw.weight1 = ws[1].Value / sum; }
            if (n > 2) { bw.boneIndex2 = ws[2].Key; bw.weight2 = ws[2].Value / sum; }
            if (n > 3) { bw.boneIndex3 = ws[3].Key; bw.weight3 = ws[3].Value / sum; }
            return Prune(bw);
        }

        static BoneWeight Prune(BoneWeight bw)
        {
            float t = 0.02f;
            if (bw.weight3 < t) bw.weight3 = 0; if (bw.weight2 < t) bw.weight2 = 0; if (bw.weight1 < t) bw.weight1 = 0;
            float s = bw.weight0 + bw.weight1 + bw.weight2 + bw.weight3;
            bw.weight0 /= s; bw.weight1 /= s; bw.weight2 /= s; bw.weight3 /= s;
            return bw;
        }

        public static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a; float l2 = ab.sqrMagnitude;
            if (l2 < 1e-10f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2);
            return (p - (a + ab * t)).magnitude;
        }

        void SmoothWeights(PartMesh pm, SkinRule rule, int iters)
        {
            if (rule.Rigid >= 0 || iters <= 0) return;
            int nv = pm.V.Count;
            var adj = new List<int>[nv];
            for (int i = 0; i < nv; i++) adj[i] = new List<int>(6);
            for (int t = 0; t < pm.T.Count; t += 3)
            {
                int a = pm.T[t], b = pm.T[t + 1], c = pm.T[t + 2];
                adj[a].Add(b); adj[a].Add(c); adj[b].Add(a); adj[b].Add(c); adj[c].Add(a); adj[c].Add(b);
            }
            int nb = Bones.Count;
            var cur = new Dictionary<int, float>[nv];
            for (int i = 0; i < nv; i++) cur[i] = FromBW(pm.W[i]);
            for (int it = 0; it < iters; it++)
            {
                var next = new Dictionary<int, float>[nv];
                Parallel.For(0, nv, i =>
                {
                    var acc = new Dictionary<int, float>();
                    foreach (var kv in cur[i]) acc[kv.Key] = kv.Value * 2f;
                    float tot = 2f;
                    foreach (int j in adj[i])
                    {
                        foreach (var kv in cur[j]) { acc.TryGetValue(kv.Key, out float v); acc[kv.Key] = v + kv.Value; }
                        tot += 1f;
                    }
                    var res = new Dictionary<int, float>();
                    foreach (var kv in acc) res[kv.Key] = kv.Value / tot;
                    next[i] = res;
                });
                cur = next;
            }
            for (int i = 0; i < nv; i++)
            {
                var l = new List<KeyValuePair<int, float>>(cur[i]);
                pm.W[i] = ToBW(l);
            }
        }

        static Dictionary<int, float> FromBW(BoneWeight w)
        {
            var d = new Dictionary<int, float>();
            if (w.weight0 > 0) d[w.boneIndex0] = w.weight0;
            if (w.weight1 > 0) { d.TryGetValue(w.boneIndex1, out float v); d[w.boneIndex1] = v + w.weight1; }
            if (w.weight2 > 0) { d.TryGetValue(w.boneIndex2, out float v); d[w.boneIndex2] = v + w.weight2; }
            if (w.weight3 > 0) { d.TryGetValue(w.boneIndex3, out float v); d[w.boneIndex3] = v + w.weight3; }
            return d;
        }
    }
}
