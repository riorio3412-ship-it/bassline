using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>Geometry cleanup of generated scans: tiny floating fragments are removed before rigging.</summary>
    public static class GlbCleanup
    {
        /// <summary>
        /// Removes floating fragments: pieces are grouped by mesh connectivity AND by proximity (anything within
        /// 'minGap' of other geometry belongs to it - buttons, studs, strand tips stay), then the small, isolated
        /// groups are dropped. Returns the number of removed groups.
        /// </summary>
        public static int RemoveFloaters(List<Vector3> V, List<int> T, ref int[] triMat, float maxSize, float minGap, System.Text.StringBuilder log, string id)
        {
            int n = V.Count;
            var used = new bool[n];
            foreach (int i in T) used[i] = true;
            var uf = new int[n]; for (int i = 0; i < n; i++) uf[i] = i;
            int Find(int x) { while (uf[x] != x) { uf[x] = uf[uf[x]]; x = uf[x]; } return x; }
            void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) uf[a] = b; }
            for (int t = 0; t < T.Count; t += 3) { Union(T[t], T[t + 1]); Union(T[t], T[t + 2]); }
            // proximity union through a hash grid of cell = gap
            float cell = minGap;
            var grid = new Dictionary<long, List<int>>(MeshDecimator.EdgeKeyComparer.Instance);
            long Key(int x, int y, int z) => ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (long)(z & 0x1FFFFF);
            for (int i = 0; i < n; i++)
            {
                if (!used[i]) continue;
                long k = Key(Mathf.FloorToInt(V[i].x / cell), Mathf.FloorToInt(V[i].y / cell), Mathf.FloorToInt(V[i].z / cell));
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>(4);
                l.Add(i);
            }
            float g2 = minGap * minGap;
            foreach (var kv in grid)
            {
                var l = kv.Value;
                int cx = (int)((kv.Key >> 42) & 0x1FFFFF), cy = (int)((kv.Key >> 21) & 0x1FFFFF), cz = (int)(kv.Key & 0x1FFFFF);
                foreach (int i in l) Union(i, l[0]);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dy == 0 && dz == 0) continue;
                            if (!grid.TryGetValue(Key(cx + dx, cy + dy, cz + dz), out var o)) continue;
                            if (Find(o[0]) == Find(l[0])) continue;
                            bool near = false;
                            foreach (int a in l) { foreach (int b in o) if ((V[a] - V[b]).sqrMagnitude < g2) { near = true; break; } if (near) break; }
                            if (near) Union(l[0], o[0]);
                        }
            }
            // groups
            var cnt = new Dictionary<int, int>(); var mn = new Dictionary<int, Vector3>(); var mx = new Dictionary<int, Vector3>();
            for (int i = 0; i < n; i++)
            {
                if (!used[i]) continue;
                int r = Find(i);
                cnt.TryGetValue(r, out int c); cnt[r] = c + 1;
                if (!mn.TryGetValue(r, out var a)) { mn[r] = V[i]; mx[r] = V[i]; }
                else { mn[r] = Vector3.Min(a, V[i]); mx[r] = Vector3.Max(mx[r], V[i]); }
            }
            int biggest = cnt.OrderByDescending(kv => kv.Value).First().Key;
            var drop = new HashSet<int>();
            foreach (var kv in cnt)
                if (kv.Key != biggest && kv.Value < 600 && (mx[kv.Key] - mn[kv.Key]).magnitude < maxSize) drop.Add(kv.Key);
            if (drop.Count == 0) { log?.AppendLine($"[{id}] cleanup: no floating fragments ({cnt.Count} groups)"); return 0; }
            var nt = new List<int>(T.Count); var nm = new List<int>(T.Count / 3);
            int removedTris = 0;
            for (int t = 0; t < T.Count / 3; t++)
            {
                if (drop.Contains(Find(T[t * 3]))) { removedTris++; continue; }
                nt.Add(T[t * 3]); nt.Add(T[t * 3 + 1]); nt.Add(T[t * 3 + 2]); nm.Add(triMat[t]);
            }
            T.Clear(); T.AddRange(nt); triMat = nm.ToArray();
            log?.AppendLine($"[{id}] cleanup: removed {drop.Count} floating fragments ({removedTris} triangles) of {cnt.Count} groups");
            return drop.Count;
        }
    }
}
