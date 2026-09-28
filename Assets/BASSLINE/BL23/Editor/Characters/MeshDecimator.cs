using System;
using System.Collections.Generic;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Quadric edge-collapse decimation (Garland-Heckbert, half-edge collapse so vertex attributes are kept exactly).
    /// Material borders and open borders are protected by boundary quadrics; 'locked' vertices are never removed.
    /// </summary>
    public static class MeshDecimator
    {
        struct Q
        {
            public double a, b, c, d, e, f, g, h, i, j; // symmetric 4x4 (upper): a b c d / e f g / h i / j
            public static Q Plane(double nx, double ny, double nz, double w, double weight)
            {
                return new Q
                {
                    a = nx * nx * weight, b = nx * ny * weight, c = nx * nz * weight, d = nx * w * weight,
                    e = ny * ny * weight, f = ny * nz * weight, g = ny * w * weight,
                    h = nz * nz * weight, i = nz * w * weight, j = w * w * weight
                };
            }
            public void Add(in Q o) { a += o.a; b += o.b; c += o.c; d += o.d; e += o.e; f += o.f; g += o.g; h += o.h; i += o.i; j += o.j; }
            public double Eval(Vector3 v)
            {
                double x = v.x, y = v.y, z = v.z;
                return a * x * x + 2 * b * x * y + 2 * c * x * z + 2 * d * x + e * y * y + 2 * f * y * z + 2 * g * y + h * z * z + 2 * i * z + j;
            }
        }

        struct HeapItem { public double Cost; public int U, V, SU, SV; }

        /// <summary>Hash for packed (a shl 32 or b) edge keys. long.GetHashCode is a ^ b, which collapses the keys of
        /// sequentially indexed meshes (a, a + 1 ...) into a few buckets and makes the edge maps quadratic.</summary>
        public sealed class EdgeKeyComparer : IEqualityComparer<long>
        {
            public static readonly EdgeKeyComparer Instance = new EdgeKeyComparer();
            public bool Equals(long x, long y) => x == y;
            public int GetHashCode(long k) { ulong h = (ulong)k * 0x9E3779B97F4A7C15UL; return (int)(h >> 32) ^ (int)h; }
        }

        sealed class Heap
        {
            HeapItem[] a = new HeapItem[1024]; int n;
            public int Count => n;
            public void Push(HeapItem x)
            {
                if (n == a.Length) Array.Resize(ref a, n * 2);
                int k = n++; a[k] = x;
                while (k > 0) { int p = (k - 1) >> 1; if (a[p].Cost <= a[k].Cost) break; var t = a[p]; a[p] = a[k]; a[k] = t; k = p; }
            }
            public HeapItem Pop()
            {
                var top = a[0]; a[0] = a[--n];
                int k = 0;
                while (true)
                {
                    int l = 2 * k + 1, r = l + 1, m = k;
                    if (l < n && a[l].Cost < a[m].Cost) m = l;
                    if (r < n && a[r].Cost < a[m].Cost) m = r;
                    if (m == k) break;
                    var t = a[m]; a[m] = a[k]; a[k] = t; k = m;
                }
                return top;
            }
        }

        /// <summary>
        /// Decimates in place. tris: index list (3 per triangle); triMat: material per triangle.
        /// Returns the new vertex remap (old index -> new index or -1) and rewrites tris/triMat.
        /// </summary>
        public static int[] Decimate(List<Vector3> pos, ref List<int> tris, ref int[] triMat, int targetTris, double maxError, bool[] locked = null, float borderWeight = 40f)
        {
            int nv = pos.Count, nt = tris.Count / 3;
            if (nt <= targetTris) return Identity(nv);
            var T = tris.ToArray();
            var alive = new bool[nt];
            var vtris = new List<int>[nv];
            for (int i = 0; i < nv; i++) vtris[i] = new List<int>(8);
            var Qs = new Q[nv];
            for (int t = 0; t < nt; t++)
            {
                alive[t] = true;
                int a = T[t * 3], b = T[t * 3 + 1], c = T[t * 3 + 2];
                vtris[a].Add(t); vtris[b].Add(t); vtris[c].Add(t);
                Vector3 n = Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]);
                double area = n.magnitude;
                if (area < 1e-14) continue;
                Vector3 nn = n / (float)area;
                double w = -(nn.x * pos[a].x + nn.y * pos[a].y + nn.z * pos[a].z);
                var q = Q.Plane(nn.x, nn.y, nn.z, w, 1.0);
                Qs[a].Add(q); Qs[b].Add(q); Qs[c].Add(q);
            }
            // border / material-seam quadrics
            var edgeTris = new Dictionary<long, (int t0, int t1, int cnt)>(nt * 2, EdgeKeyComparer.Instance);
            for (int t = 0; t < nt; t++)
                for (int k = 0; k < 3; k++)
                {
                    int a = T[t * 3 + k], b = T[t * 3 + (k + 1) % 3];
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (edgeTris.TryGetValue(key, out var v)) edgeTris[key] = (v.t0, t, v.cnt + 1);
                    else edgeTris[key] = (t, -1, 1);
                }
            var border = new bool[nv];
            foreach (var kv in edgeTris)
            {
                var v = kv.Value;
                bool isBorder = v.cnt == 1 || (v.cnt == 2 && triMat[v.t0] != triMat[v.t1]);
                if (!isBorder) continue;
                int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffff);
                border[a] = border[b] = true;
                int t = v.t0;
                Vector3 fn = Vector3.Cross(pos[T[t * 3 + 1]] - pos[T[t * 3]], pos[T[t * 3 + 2]] - pos[T[t * 3]]).normalized;
                Vector3 e = pos[b] - pos[a];
                Vector3 pn = Vector3.Cross(e, fn);
                float len = pn.magnitude;
                if (len < 1e-10f) continue;
                pn /= len;
                double w = -(pn.x * pos[a].x + pn.y * pos[a].y + pn.z * pos[a].z);
                var q = Q.Plane(pn.x, pn.y, pn.z, w, borderWeight);
                Qs[a].Add(q); Qs[b].Add(q);
            }
            var stamp = new int[nv];
            var removed = new bool[nv];
            var heap = new Heap();
            var nbrs = new HashSet<int>();
            var nbrV = new HashSet<int>();

            void PushEdge(int u, int v)
            {
                double best = double.MaxValue; int bu = -1, bv = -1;
                bool lu = locked != null && locked[u], lv = locked != null && locked[v];
                var qs = Qs[u]; qs.Add(Qs[v]);
                if (!lu) { double c = qs.Eval(pos[v]); if (c < best) { best = c; bu = u; bv = v; } }
                if (!lv) { double c = qs.Eval(pos[u]); if (c < best) { best = c; bu = v; bv = u; } }
                if (bu < 0) return;
                heap.Push(new HeapItem { Cost = best, U = bu, V = bv, SU = stamp[bu], SV = stamp[bv] });
            }

            foreach (var kv in edgeTris)
            {
                int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffff);
                PushEdge(a, b);
            }

            int liveTris = nt;
            while (heap.Count > 0 && liveTris > targetTris)
            {
                var it = heap.Pop();
                if (it.Cost > maxError) break;
                int u = it.U, v = it.V;
                if (removed[u] || removed[v] || stamp[u] != it.SU || stamp[v] != it.SV) continue;
                // link condition: |N(u) ∩ N(v)| <= 2
                nbrs.Clear();
                foreach (int t in vtris[u]) { if (!alive[t]) continue; for (int k = 0; k < 3; k++) { int x = T[t * 3 + k]; if (x != u) nbrs.Add(x); } }
                if (!nbrs.Contains(v)) continue;
                if (vtris[v].Count + vtris[u].Count > 64) continue;   // no huge fans (slivers / slow collapses)
                nbrV.Clear();
                foreach (int t in vtris[v]) { if (!alive[t]) continue; for (int k = 0; k < 3; k++) { int x = T[t * 3 + k]; if (x != v) nbrV.Add(x); } }
                int shared = 0;
                foreach (int x in nbrV) if (x != u && nbrs.Contains(x)) shared++;
                if (shared > 2) continue;
                // flip check
                bool bad = false;
                foreach (int t in vtris[u])
                {
                    if (!alive[t]) continue;
                    int a = T[t * 3], b = T[t * 3 + 1], c = T[t * 3 + 2];
                    if (a == v || b == v || c == v) continue;
                    Vector3 pa = pos[a], pb = pos[b], pc = pos[c];
                    Vector3 n0 = Vector3.Cross(pb - pa, pc - pa);
                    if (a == u) pa = pos[v]; else if (b == u) pb = pos[v]; else pc = pos[v];
                    Vector3 n1 = Vector3.Cross(pb - pa, pc - pa);
                    float l0 = n0.magnitude, l1 = n1.magnitude;
                    if (l1 < 1e-12f || Vector3.Dot(n0, n1) < 0.3f * l0 * l1) { bad = true; break; }
                    // avoid slivers
                    float perim = (pb - pa).sqrMagnitude + (pc - pb).sqrMagnitude + (pa - pc).sqrMagnitude;
                    if (l1 / (perim + 1e-12f) < 0.02f) { bad = true; break; }
                }
                if (bad) continue;
                // collapse u -> v
                foreach (int t in vtris[u])
                {
                    if (!alive[t]) continue;
                    int a = T[t * 3], b = T[t * 3 + 1], c = T[t * 3 + 2];
                    if (a == v || b == v || c == v) { alive[t] = false; liveTris--; continue; }
                    if (a == u) T[t * 3] = v; else if (b == u) T[t * 3 + 1] = v; else T[t * 3 + 2] = v;
                    vtris[v].Add(t);
                }
                removed[u] = true;
                Qs[v].Add(Qs[u]);
                stamp[v]++;
                vtris[v].RemoveAll(t => !alive[t]);
                nbrs.Clear();
                foreach (int t in vtris[v]) for (int k = 0; k < 3; k++) { int x = T[t * 3 + k]; if (x != v) nbrs.Add(x); }
                foreach (int x in nbrs) PushEdge(v, x);
            }
            // compact
            var remap = new int[nv];
            for (int i = 0; i < nv; i++) remap[i] = -1;
            var newTris = new List<int>(liveTris * 3);
            var newMat = new List<int>(liveTris);
            int cnt = 0;
            for (int t = 0; t < nt; t++)
            {
                if (!alive[t]) continue;
                int a = T[t * 3], b = T[t * 3 + 1], c = T[t * 3 + 2];
                if (a == b || b == c || a == c) continue;
                for (int k = 0; k < 3; k++)
                {
                    int x = T[t * 3 + k];
                    if (remap[x] < 0) remap[x] = cnt++;
                    newTris.Add(remap[x]);
                }
                newMat.Add(triMat[t]);
            }
            tris = newTris;
            triMat = newMat.ToArray();
            return remap;
        }

        static int[] Identity(int n) { var r = new int[n]; for (int i = 0; i < n; i++) r[i] = i; return r; }

        /// <summary>Applies a remap (old->new, -1 = dropped) to a per-vertex list.</summary>
        public static List<T> Remap<T>(List<T> src, int[] remap)
        {
            int n = 0; foreach (int r in remap) if (r >= 0) n = Math.Max(n, r + 1);
            var dst = new T[n];
            for (int i = 0; i < remap.Length; i++) if (remap[i] >= 0) dst[remap[i]] = src[i];
            return new List<T>(dst);
        }

        public static T[] Remap<T>(T[] src, int[] remap)
        {
            int n = 0; foreach (int r in remap) if (r >= 0) n = Math.Max(n, r + 1);
            var dst = new T[n];
            for (int i = 0; i < remap.Length; i++) if (remap[i] >= 0) dst[remap[i]] = src[i];
            return dst;
        }
    }
}
