using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>Caches the decimated GLB mesh (the slow part of a GLB bake) under Library/BL23GlbCache.</summary>
    public static class GlbCache
    {
        const int Version = 3;
        static string PathFor(string model, int tris)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/BL23GlbCache"));
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, model.Replace(':', '_') + "_" + tris + "_v" + Version + ".bin");
        }

        public static bool TryLoad(string model, int tris, out List<Vector3> V, out List<Vector3> N, out List<Vector2> UV, out List<int> T, out int[] triMat)
        {
            V = null; N = null; UV = null; T = null; triMat = null;
            string p = PathFor(model, tris);
            if (!File.Exists(p)) return false;
            try
            {
                using (var r = new BinaryReader(File.OpenRead(p)))
                {
                    int nv = r.ReadInt32(), nt = r.ReadInt32();
                    V = new List<Vector3>(nv); N = new List<Vector3>(nv); UV = new List<Vector2>(nv); T = new List<int>(nt * 3); triMat = new int[nt];
                    for (int i = 0; i < nv; i++) V.Add(new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));
                    for (int i = 0; i < nv; i++) N.Add(new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));
                    for (int i = 0; i < nv; i++) UV.Add(new Vector2(r.ReadSingle(), r.ReadSingle()));
                    for (int i = 0; i < nt * 3; i++) T.Add(r.ReadInt32());
                    for (int i = 0; i < nt; i++) triMat[i] = r.ReadInt32();
                }
                return true;
            }
            catch { return false; }
        }

        public static void Save(string model, int tris, List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<int> T, int[] triMat)
        {
            using (var w = new BinaryWriter(File.Create(PathFor(model, tris))))
            {
                w.Write(V.Count); w.Write(triMat.Length);
                foreach (var v in V) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
                foreach (var v in N) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
                foreach (var v in UV) { w.Write(v.x); w.Write(v.y); }
                foreach (int t in T) w.Write(t);
                foreach (int m in triMat) w.Write(m);
            }
        }
    }
}
