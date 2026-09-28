using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>Diagnostics for the auto-rigged GLB actors (rest skeleton, retarget offsets, arm membership splats).</summary>
    public static class GlbDebug
    {
        public static void DumpRig()
        {
            var sb = new StringBuilder();
            foreach (var id in new[] { "P01", "P02", "P04", "P06" })
            {
                var def = Cast.Get(id);
                var holder = new GameObject("H");
                var rig = ActorFactory.Create(def, holder.transform);
                rig.Anim.AutoFidget = false;
                sb.AppendLine($"== {id} H {rig.Height} armOut {rig.Anim.ArmIdleOut}");
                for (int i = 0; i < ActorSkeleton.Count; i++)
                    sb.AppendLine($"  {(HBone)i,-10} rest {rig.Anim.RestPos((HBone)i).ToString("F3")}");
                sb.AppendLine($"  tipL {rig.HandTipRestL.ToString("F3")} tipR {rig.HandTipRestR.ToString("F3")}");
                Object.DestroyImmediate(holder);
            }
            Debug.Log("[GlbDebug]\n" + sb);
            File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/glbdebug.txt"), sb.ToString());
        }

        /// <summary>Zoomed splat of a region: 3 views along the given frame axes (e0 right, e1 up, e2 toward the viewer).</summary>
        public static void SplatRegion(string name, List<Vector3> V, System.Func<int, Color> col, Vector3 c, float half, Vector3 e0, Vector3 e1, Vector3 e2)
        {
            int R = 400;
            var img = new Color[R * 3 * R]; var zb = new float[R * 3 * R];
            for (int i = 0; i < img.Length; i++) { img[i] = new Color(0.08f, 0.08f, 0.1f); zb[i] = float.MinValue; }
            Vector3[,] views = { { e0, e1, e2 }, { e2, e1, -e0 }, { e0, e2, -e1 } };
            for (int i = 0; i < V.Count; i++)
            {
                Vector3 d = V[i] - c;
                if (Mathf.Abs(d.x) > half * 1.8f || Mathf.Abs(d.y) > half * 1.8f || Mathf.Abs(d.z) > half * 1.8f) continue;
                var cc = col(i);
                for (int v = 0; v < 3; v++)
                {
                    float u = Vector3.Dot(d, views[v, 0]) / half, w = Vector3.Dot(d, views[v, 1]) / half, z = Vector3.Dot(d, views[v, 2]);
                    int x = Mathf.RoundToInt((u * 0.5f + 0.5f) * R), y = Mathf.RoundToInt((w * 0.5f + 0.5f) * R);
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= R || yy >= R) continue;
                        int k = yy * R * 3 + v * R + xx;
                        if (z <= zb[k]) continue;
                        zb[k] = z; img[k] = cc;
                    }
                }
            }
            var t = new Texture2D(R * 3, R, TextureFormat.RGB24, false);
            t.SetPixels(img); t.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots")), name + ".png"), t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        /// <summary>Point splat (front | side | back) of a vertex set with per-vertex colours; orthographic, z-buffered.</summary>
        public static void Splat(string name, List<Vector3> V, System.Func<int, Color> col, float H, List<Vector3> marks = null)
        {
            int W = 420, Hh = 900;
            float scale = Hh / (H + 0.1f);
            var img = new Color[W * 3 * Hh];
            var zb = new float[W * 3 * Hh];
            for (int i = 0; i < img.Length; i++) { img[i] = new Color(0.08f, 0.08f, 0.1f); zb[i] = float.MinValue; }
            void Put(int panel, float u, float v, float depth, Color c, int r)
            {
                int cx = Mathf.RoundToInt(W * 0.5f + u * scale) + panel * W, cy = Mathf.RoundToInt(v * scale + 20);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (x < panel * W || x >= (panel + 1) * W || y < 0 || y >= Hh) continue;
                        int k = y * W * 3 + x;
                        if (depth <= zb[k]) continue;
                        zb[k] = depth; img[k] = c;
                    }
            }
            for (int i = 0; i < V.Count; i++)
            {
                var p = V[i]; var c = col(i);
                Put(0, -p.x, p.y, p.z, c, 1);        // front (character faces the viewer; its left is image right)
                Put(1, p.z, p.y, p.x, c, 1);          // side (from the character's right... +x toward viewer)
                Put(2, p.x, p.y, -p.z, c, 1);         // back
            }
            if (marks != null)
                foreach (var m in marks)
                {
                    Put(0, -m.x, m.y, 99f, Color.yellow, 3); Put(1, m.z, m.y, 99f, Color.yellow, 3); Put(2, m.x, m.y, 99f, Color.yellow, 3);
                }
            var t = new Texture2D(W * 3, Hh, TextureFormat.RGB24, false);
            t.SetPixels(img); t.Apply();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots"));
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }
    }
}
