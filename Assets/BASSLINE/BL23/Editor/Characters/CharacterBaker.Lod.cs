using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.EditorTools.Characters
{
    public static partial class CharacterBakerLod
    {
        /// <summary>Builds a decimated copy of a skinned mesh (all channels preserved, material borders kept).</summary>
        public static Mesh MakeLod(Mesh src, float keep, bool lockBorders)
        {
            var V = new List<Vector3>(); src.GetVertices(V);
            var N = new List<Vector3>(); src.GetNormals(N);
            var Tn = new List<Vector4>(); src.GetTangents(Tn);
            var C = new List<Color>(); src.GetColors(C);
            var U0 = new List<Vector2>(); src.GetUVs(0, U0);
            var U1 = new List<Vector4>(); src.GetUVs(1, U1);
            var U2 = new List<Vector3>(); src.GetUVs(2, U2);
            var U3 = new List<Vector2>(); src.GetUVs(3, U3);
            var W = src.boneWeights;
            var T = new List<int>(); var TM = new List<int>();
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tri = src.GetTriangles(s);
                T.AddRange(tri);
                for (int i = 0; i < tri.Length / 3; i++) TM.Add(s);
            }
            bool[] locked = null;
            if (lockBorders)
            {
                var edges = new Dictionary<long, int>(MeshDecimator.EdgeKeyComparer.Instance);
                for (int t = 0; t < T.Count; t += 3)
                    for (int k = 0; k < 3; k++)
                    {
                        int a = T[t + k], b = T[t + (k + 1) % 3];
                        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                        edges.TryGetValue(key, out int c); edges[key] = c + 1;
                    }
                locked = new bool[V.Count];
                foreach (var kv in edges) if (kv.Value == 1) { locked[(int)(kv.Key >> 32)] = true; locked[(int)(kv.Key & 0xffffffff)] = true; }
            }
            var triMat = TM.ToArray();
            int target = Mathf.Max(500, (int)(T.Count / 3 * keep));
            var remap = MeshDecimator.Decimate(V, ref T, ref triMat, target, 40.0 * 0.0025 * 0.0025, locked, 20f);
            var m = new Mesh { name = src.name + "_LOD1" };
            var nV = MeshDecimator.Remap(V, remap);
            m.indexFormat = nV.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.SetVertices(nV);
            if (N.Count == V.Count) m.SetNormals(MeshDecimator.Remap(N, remap));
            if (Tn.Count == V.Count) m.SetTangents(MeshDecimator.Remap(Tn, remap));
            if (C.Count == V.Count) m.SetColors(MeshDecimator.Remap(C, remap));
            if (U0.Count == V.Count) m.SetUVs(0, MeshDecimator.Remap(U0, remap));
            if (U1.Count == V.Count) m.SetUVs(1, MeshDecimator.Remap(U1, remap));
            if (U2.Count == V.Count) m.SetUVs(2, MeshDecimator.Remap(U2, remap));
            if (U3.Count == V.Count) m.SetUVs(3, MeshDecimator.Remap(U3, remap));
            m.subMeshCount = src.subMeshCount;
            var subs = new List<int>[src.subMeshCount];
            for (int s = 0; s < subs.Length; s++) subs[s] = new List<int>();
            for (int t = 0; t < T.Count / 3; t++) { var l = subs[triMat[t]]; l.Add(T[t * 3]); l.Add(T[t * 3 + 1]); l.Add(T[t * 3 + 2]); }
            for (int s = 0; s < subs.Length; s++) m.SetTriangles(subs[s], s, false);
            if (W != null && W.Length == V.Count) m.boneWeights = MeshDecimator.Remap(W, remap);
            m.bindposes = src.bindposes;
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Adds LOD1 + LOD2 skinned renderers and a 3-level LODGroup (scanned actors: dense source meshes).</summary>
        public static SkinnedMeshRenderer[] AddLods(GameObject root, SkinnedMeshRenderer body, float keep1, float keep2, bool lockBorders, string assetPath1, string assetPath2)
        {
            SkinnedMeshRenderer Make(float keep, string path, string name)
            {
                var lodMesh = MakeLod(body.sharedMesh, keep, lockBorders);
                lodMesh.name = body.sharedMesh.name + "_" + name;
                lodMesh = CharacterBaker.SaveMesh(lodMesh, path);
                var go = new GameObject("Body_" + name);
                go.transform.SetParent(body.transform.parent, false);
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = lodMesh;
                smr.bones = body.bones;
                smr.rootBone = body.rootBone;
                smr.sharedMaterials = body.sharedMaterials;
                smr.localBounds = body.localBounds;
                smr.quality = SkinQuality.Bone2;
                return smr;
            }
            var l1 = Make(keep1, assetPath1, "LOD1");
            var l2 = Make(keep2, assetPath2, "LOD2");
            var lg = root.AddComponent<LODGroup>();
            var extras = root.GetComponentsInChildren<Renderer>(true).Where(r => r != body && r != l1 && r != l2 && !(r is SkinnedMeshRenderer)).ToArray();
            var lod0 = new List<Renderer> { body }; lod0.AddRange(extras);
            var lod1 = new List<Renderer> { l1 }; lod1.AddRange(extras);
            var lod2 = new List<Renderer> { l2 };
            lg.SetLODs(new[] { new LOD(0.28f, lod0.ToArray()), new LOD(0.1f, lod1.ToArray()), new LOD(0.006f, lod2.ToArray()) });
            lg.RecalculateBounds();
            return new[] { l1, l2 };
        }

        /// <summary>Adds a LOD1 skinned renderer + LODGroup next to 'body'.</summary>
        public static SkinnedMeshRenderer AddLod(GameObject root, SkinnedMeshRenderer body, float keep, bool lockBorders, string assetPath)
        {
            var lodMesh = MakeLod(body.sharedMesh, keep, lockBorders);
            lodMesh = CharacterBaker.SaveMesh(lodMesh, assetPath);
            var go = new GameObject("Body_LOD1");
            go.transform.SetParent(body.transform.parent, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = lodMesh;
            smr.bones = body.bones;
            smr.rootBone = body.rootBone;
            smr.sharedMaterials = body.sharedMaterials;
            smr.localBounds = body.localBounds;
            var lg = root.AddComponent<LODGroup>();
            var extras = root.GetComponentsInChildren<Renderer>(true).Where(r => r != body && r != smr && !(r is SkinnedMeshRenderer)).ToArray();
            var lod0 = new List<Renderer> { body }; lod0.AddRange(extras);
            var lod1 = new List<Renderer> { smr }; lod1.AddRange(extras);
            lg.SetLODs(new[] { new LOD(0.2f, lod0.ToArray()), new LOD(0.006f, lod1.ToArray()) });
            lg.RecalculateBounds();
            return smr;
        }
    }
}
