using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>0.5m navigation lattice for one floor. Edge flags come from the shared wall model so NPC paths,
    /// perception rays and the rendered walls agree.</summary>
    public sealed class NavGrid
    {
        public const float C = 0.5f;
        public readonly int Floor, NX, NZ; public readonly float X0, Z0;
        public readonly int[] Room;        // room id per cell, -1 outside/void
        public readonly bool[] Block;      // furniture / stair footprint
        public readonly byte[] NearWall;   // soft cost
        // edge to the east (+x) and north (+z) of each cell: -2 wall, -1 open, >=0 door id
        public readonly int[] EdgeE, EdgeN;
        readonly Layout L;

        public NavGrid(Layout layout, int floor)
        {
            L = layout; Floor = floor; var fi = layout.Floor(floor);
            X0 = fi.Bounds.x0; Z0 = fi.Bounds.z0;
            NX = (int)Math.Round(fi.Bounds.W / C); NZ = (int)Math.Round(fi.Bounds.D / C);
            Room = new int[NX * NZ]; Block = new bool[NX * NZ]; NearWall = new byte[NX * NZ];
            EdgeE = new int[NX * NZ]; EdgeN = new int[NX * NZ];
            for (int k = 0; k < Room.Length; k++) { Room[k] = -1; EdgeE[k] = -1; EdgeN[k] = -1; }
            foreach (var r in layout.Rooms.Where(r => r.Floor == floor))
            {
                int val = r.Void ? -1 : r.Id; // void (open to below) overwrites the landing ring interior
                int i0 = I(r.Rect.x0), i1 = I(r.Rect.x1), j0 = J(r.Rect.z0), j1 = J(r.Rect.z1);
                for (int i = Math.Max(0, i0); i < Math.Min(NX, i1); i++) for (int j = Math.Max(0, j0); j < Math.Min(NZ, j1); j++) Room[i + j * NX] = val;
            }
            // edges: default open within same room; walls between different rooms unless open/door
            for (int i = 0; i < NX; i++) for (int j = 0; j < NZ; j++)
                {
                    int k = i + j * NX;
                    if (i + 1 < NX) EdgeE[k] = Room[k] == Room[k + 1] ? -1 : -2; else EdgeE[k] = -2;
                    if (j + 1 < NZ) EdgeN[k] = Room[k] == Room[k + NX] ? -1 : -2; else EdgeN[k] = -2;
                }
            foreach (var w in layout.Walls().Where(w => w.Floor == floor))
            {
                if (!(w.Open || w.DoorId >= 0)) continue;
                int val = w.DoorId >= 0 ? w.DoorId : -1;
                if (w.x0 == w.x1)
                {
                    int i = I(w.x0) - 1; if (i < 0 || i >= NX - 1) continue;
                    for (int j = J(w.z0); j < J(w.z1); j++) if (j >= 0 && j < NZ) EdgeE[i + j * NX] = val;
                }
                else
                {
                    int j = J(w.z0) - 1; if (j < 0 || j >= NZ - 1) continue;
                    for (int i = I(w.x0); i < I(w.x1); i++) if (i >= 0 && i < NX) EdgeN[i + j * NX] = val;
                }
            }
            // furniture footprint
            foreach (var f in layout.Furniture.Where(f => f.Pos.f == floor && f.Blocks))
            {
                GetFootprint(f, 0.28f, out var rect);
                for (int i = I(rect.x0); i <= I(rect.x1); i++) for (int j = J(rect.z0); j <= J(rect.z1); j++)
                        if (i >= 0 && j >= 0 && i < NX && j < NZ && Room[i + j * NX] == f.Room && CellRectOverlap(i, j, rect)) Block[i + j * NX] = true;
            }
            for (int i = 0; i < NX; i++) for (int j = 0; j < NZ; j++)
                {
                    int k = i + j * NX; if (Room[k] < 0) continue;
                    bool nw = (i > 0 && EdgeE[k - 1] == -2) || EdgeE[k] == -2 || (j > 0 && EdgeN[k - NX] == -2) || EdgeN[k] == -2;
                    NearWall[k] = (byte)(nw ? 1 : 0);
                }
            // spots must stay reachable
            foreach (var s in layout.Spots.Where(s => s.Approach.f == floor)) { int k = CellOf(s.Approach.x, s.Approach.z); if (k >= 0 && Room[k] == s.Room) Block[k] = false; }
            foreach (var s in layout.Stairs) { if (s.A.f == floor) Unblock(s.A); if (s.B.f == floor) Unblock(s.B); }
            foreach (var d in layout.Doors.Where(d => d.Pos.f == floor))
            {
                // keep 1m in front of each door clear on both sides
                for (float t = -0.9f; t <= 0.9f; t += 0.5f)
                {
                    for (float s = -d.Width * 0.4f; s <= d.Width * 0.4f; s += 0.5f)
                    {
                        float x = d.AlongX ? d.Pos.x + s : d.Pos.x + t, z = d.AlongX ? d.Pos.z + t : d.Pos.z + s;
                        int k = CellOf(x, z); if (k >= 0) Block[k] = false;
                    }
                }
            }
        }
        void Unblock(P3 p) { int k = CellOf(p.x, p.z); if (k >= 0) Block[k] = false; }

        public static void GetFootprint(Furniture f, float inflate, out RectF r)
        {
            bool rot = Math.Abs(Math.Round(f.Yaw / 90f)) % 2 == 1;
            float w = rot ? f.D : f.W, d = rot ? f.W : f.D;
            r = new RectF(f.Pos.x - w / 2 - inflate, f.Pos.z - d / 2 - inflate, f.Pos.x + w / 2 + inflate, f.Pos.z + d / 2 + inflate);
        }
        bool CellRectOverlap(int i, int j, RectF r)
        {
            float cx0 = X0 + i * C, cz0 = Z0 + j * C; return cx0 < r.x1 && cx0 + C > r.x0 && cz0 < r.z1 && cz0 + C > r.z0;
        }

        public int I(float x) => (int)Math.Floor((x - X0) / C + 1e-4);
        public int J(float z) => (int)Math.Floor((z - Z0) / C + 1e-4);
        public int CellOf(float x, float z) { int i = I(x), j = J(z); if (i < 0 || j < 0 || i >= NX || j >= NZ) return -1; return i + j * NX; }
        public P3 Center(int k) => new P3(Floor, X0 + (k % NX + 0.5f) * C, Z0 + (k / NX + 0.5f) * C);
        public int RoomAtWorld(float x, float z) { int k = CellOf(x, z); return k >= 0 ? Room[k] : -1; }
        public bool Walkable(int k) => k >= 0 && Room[k] >= 0 && !Block[k];

        // ---- connectivity: walkable pockets sealed off by furniture (behind a bar, between a bed and the wall) are not
        // places anyone can stand in or start a walk from; only regions reachable from a door or a stair count
        int[] _comp; HashSet<int> _main;
        public bool InMain(int k)
        {
            if (k < 0) return false;
            if (_comp == null) BuildComponents();
            return _comp[k] >= 0 && _main.Contains(_comp[k]);
        }
        void BuildComponents()
        {
            _comp = new int[Room.Length]; for (int k = 0; k < _comp.Length; k++) _comp[k] = -1;
            _main = new HashSet<int>(); int id = 0; var q = new Queue<int>();
            for (int s = 0; s < Room.Length; s++)
            {
                if (!Walkable(s) || _comp[s] >= 0) continue;
                _comp[s] = id; q.Enqueue(s);
                while (q.Count > 0)
                {
                    int c = q.Dequeue(); int ci = c % NX, cj = c / NX;
                    foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int ni = ci + di, nj = cj + dj; if (ni < 0 || nj < 0 || ni >= NX || nj >= NZ) continue;
                        int n = ni + nj * NX; if (_comp[n] >= 0 || !Walkable(n)) continue;
                        int e = Edge(c, n); if (e == -2) continue;
                        if (e >= 0) _main.Add(id);
                        _comp[n] = id; q.Enqueue(n);
                    }
                }
                id++;
            }
            foreach (var st in L.Stairs)
                foreach (var p in new[] { st.A, st.B })
                {
                    if (p.f != Floor) continue;
                    int k = CellOf(p.x, p.z);
                    // the stair endpoint may sit on a blocked footprint: take the nearest labelled cell
                    for (int r = 0; r < 6 && k >= 0; r++)
                    {
                        int bi = k % NX, bj = k / NX; bool found = false;
                        for (int di = -r; di <= r && !found; di++) for (int dj = -r; dj <= r && !found; dj++)
                            {
                                int i = bi + di, j = bj + dj; if (i < 0 || j < 0 || i >= NX || j >= NZ) continue;
                                int kk = i + j * NX; if (_comp[kk] >= 0) { _main.Add(_comp[kk]); found = true; }
                            }
                        if (found) break;
                    }
                }
            // a floor with no doors at all (e.g. the court): its biggest region is the main one
            if (_main.Count == 0 && id > 0)
            {
                var size = new int[id]; foreach (var c in _comp) if (c >= 0) size[c]++;
                int best = 0; for (int i = 1; i < id; i++) if (size[i] > size[best]) best = i; _main.Add(best);
            }
        }

        /// <summary>Edge between two orthogonally adjacent cells: -2 wall, -1 open, else door id.</summary>
        public int Edge(int a, int b)
        {
            if (b == a + 1) return EdgeE[a]; if (b == a - 1) return EdgeE[b];
            if (b == a + NX) return EdgeN[a]; if (b == a - NX) return EdgeN[b];
            return -2;
        }

        /// <summary>Grid ray for sight: returns false if a wall, closed door or tall blocker intersects. Door ids crossed are reported.</summary>
        public bool Ray(float x0, float z0, float x1, float z1, Func<int, bool> doorTransparent, bool blockersOpaque, out int doorsCrossed)
        {
            doorsCrossed = 0;
            int a = CellOf(x0, z0), b = CellOf(x1, z1); if (a < 0 || b < 0) return false;
            float dx = x1 - x0, dz = z1 - z0; float len = (float)Math.Sqrt(dx * dx + dz * dz); if (len < 1e-3) return true;
            int steps = (int)Math.Ceiling(len / (C * 0.5f));
            int prev = a;
            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps; int k = CellOf(x0 + dx * t, z0 + dz * t); if (k < 0) return false;
                if (k == prev) continue;
                int pi = prev % NX, pj = prev / NX, ki = k % NX, kj = k / NX;
                if (Math.Abs(pi - ki) + Math.Abs(pj - kj) == 2)
                {
                    int mid = ki + pj * NX; // go through one orthogonal neighbor
                    if (!EdgeOk(prev, mid, doorTransparent, ref doorsCrossed)) { mid = pi + kj * NX; if (!EdgeOk(prev, mid, doorTransparent, ref doorsCrossed)) return false; if (!EdgeOk(mid, k, doorTransparent, ref doorsCrossed)) return false; }
                    else if (!EdgeOk(mid, k, doorTransparent, ref doorsCrossed)) return false;
                }
                else if (!EdgeOk(prev, k, doorTransparent, ref doorsCrossed)) return false;
                if (Room[k] < 0) return false;
                prev = k;
            }
            return true;
        }
        bool EdgeOk(int a, int b, Func<int, bool> doorTransparent, ref int doors)
        {
            int e = Edge(a, b); if (e == -2) return false; if (e == -1) return true;
            doors++; return doorTransparent == null || doorTransparent(e);
        }
    }

    public sealed class PathResult
    {
        public List<P3> Points = new List<P3>();
        public List<int> Doors = new List<int>();     // doors in traversal order
        public List<int> DoorAtPoint = new List<int>(); // door id crossed when reaching point i (-1 none)
        public List<int> StairAtPoint = new List<int>();
        public float Length; public bool Ok;
    }

    public static class Pathfinder
    {
        sealed class Node { public int F; public int K; }
        /// <summary>A* across floors. doorCost returns &lt;0 for impassable (by the traveller's knowledge), else extra cost.</summary>
        public static PathResult Find(Layout L, P3 from, P3 to, Func<int, float> doorCost, int maxExpand = 60000)
        {
            var res = new PathResult();
            var floors = L.Floors.Select(f => f.F).ToList();
            var grids = floors.ToDictionary(f => f, f => L.Nav(f));
            int start = Snap(grids[from.f], from), goal = Snap(grids[to.f], to);
            if (start < 0 || goal < 0) return res;
            long Key(int f, int k) => ((long)(f + 8) << 32) | (uint)k;
            var g = new Dictionary<long, float>(); var came = new Dictionary<long, long>(); var viaDoor = new Dictionary<long, int>(); var viaStair = new Dictionary<long, int>();
            var open = new SortedSet<(float, long)>();
            long sKey = Key(from.f, start), gKey = Key(to.f, goal);
            g[sKey] = 0; open.Add((H(grids, from.f, start, to), sKey));
            int expand = 0;
            var stairsByCell = new Dictionary<long, List<(Stair st, int toF, int toK)>>();
            foreach (var st in L.Stairs)
            {
                if (!grids.ContainsKey(st.A.f) || !grids.ContainsKey(st.B.f)) continue;
                int ka = Snap(grids[st.A.f], st.A), kb = Snap(grids[st.B.f], st.B); if (ka < 0 || kb < 0) continue;
                long a = Key(st.A.f, ka), b = Key(st.B.f, kb);
                if (!stairsByCell.TryGetValue(a, out var la)) stairsByCell[a] = la = new List<(Stair, int, int)>(); la.Add((st, st.B.f, kb));
                if (!stairsByCell.TryGetValue(b, out var lb)) stairsByCell[b] = lb = new List<(Stair, int, int)>(); lb.Add((st, st.A.f, ka));
            }
            bool found = false;
            while (open.Count > 0 && expand++ < maxExpand)
            {
                var cur = open.Min; open.Remove(cur); long ck = cur.Item2;
                if (ck == gKey) { found = true; break; }
                int cf = (int)((ck >> 32) - 8), cc = (int)(ck & 0xffffffff); var grid = grids[cf];
                float cg = g[ck];
                int ci = cc % grid.NX, cj = cc / grid.NX;
                for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int ni = ci + dx, nj = cj + dz; if (ni < 0 || nj < 0 || ni >= grid.NX || nj >= grid.NZ) continue;
                        int nk = ni + nj * grid.NX; if (!grid.Walkable(nk)) continue;
                        float step = (dx != 0 && dz != 0) ? 1.4142f : 1f; int door = -1;
                        if (dx != 0 && dz != 0)
                        {
                            int m1 = ni + cj * grid.NX, m2 = ci + nj * grid.NX;
                            if (!grid.Walkable(m1) || !grid.Walkable(m2)) continue;
                            if (grid.Edge(cc, m1) != -1 || grid.Edge(m1, nk) != -1 || grid.Edge(cc, m2) != -1 || grid.Edge(m2, nk) != -1) continue;
                        }
                        else
                        {
                            int e = grid.Edge(cc, nk); if (e == -2) continue;
                            if (e >= 0) { float dc = doorCost != null ? doorCost(e) : 0; if (dc < 0) continue; step += dc; door = e; }
                        }
                        step *= NavGrid.C; if (grid.NearWall[nk] == 1) step *= 1.6f;
                        long nkKey = Key(cf, nk); float ng = cg + step;
                        if (!g.TryGetValue(nkKey, out var old) || ng < old - 1e-4f)
                        {
                            if (g.ContainsKey(nkKey)) open.Remove((old + H(grids, cf, nk, to), nkKey));
                            g[nkKey] = ng; came[nkKey] = ck; if (door >= 0) viaDoor[nkKey] = door; else viaDoor.Remove(nkKey); viaStair.Remove(nkKey);
                            open.Add((ng + H(grids, cf, nk, to), nkKey));
                        }
                    }
                if (stairsByCell.TryGetValue(ck, out var sl))
                    foreach (var (st, tf, tk) in sl)
                    {
                        long nkKey = Key(tf, tk); float ng = cg + st.Seconds * 1.2f;
                        if (!g.TryGetValue(nkKey, out var old) || ng < old - 1e-4f)
                        {
                            if (g.ContainsKey(nkKey)) open.Remove((old + H(grids, tf, tk, to), nkKey));
                            g[nkKey] = ng; came[nkKey] = ck; viaStair[nkKey] = st.Id; viaDoor.Remove(nkKey);
                            open.Add((ng + H(grids, tf, tk, to), nkKey));
                        }
                    }
            }
            if (!found) return res;
            var chain = new List<long>(); long at = gKey; chain.Add(at);
            while (at != sKey) { at = came[at]; chain.Add(at); }
            chain.Reverse();
            // string pull per floor segment, keeping door & stair crossings as anchors
            var pts = new List<(P3 p, int door, int stair)>();
            foreach (var k in chain)
            {
                int f = (int)((k >> 32) - 8), c = (int)(k & 0xffffffff);
                pts.Add((grids[f].Center(c), viaDoor.TryGetValue(k, out var d) ? d : -1, viaStair.TryGetValue(k, out var s) ? s : -1));
            }
            pts[0] = (from, -1, -1); pts[pts.Count - 1] = (to, pts[pts.Count - 1].door, pts[pts.Count - 1].stair);
            var outPts = new List<(P3 p, int door, int stair)> { pts[0] };
            int anchor = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                bool force = pts[i].door >= 0 || pts[i].stair >= 0 || (i + 1 < pts.Count && (pts[i + 1].door >= 0 || pts[i + 1].stair >= 0)) || i == pts.Count - 1;
                if (force || !Clear(grids[pts[anchor].p.f], pts[anchor].p, pts[Math.Min(i + 1, pts.Count - 1)].p))
                {
                    outPts.Add(pts[i]); anchor = i;
                }
            }
            foreach (var p in outPts) { res.Points.Add(p.p); res.DoorAtPoint.Add(p.door); res.StairAtPoint.Add(p.stair); if (p.door >= 0) res.Doors.Add(p.door); }
            for (int i = 1; i < res.Points.Count; i++) res.Length += res.Points[i - 1].f == res.Points[i].f ? res.Points[i - 1].DistXZ(res.Points[i]) : 6f;
            res.Ok = true; return res;
        }

        static float H(Dictionary<int, NavGrid> grids, int f, int k, P3 to)
        {
            var p = grids[f].Center(k); return p.DistXZ(to) + Math.Abs(f - to.f) * 8f;
        }

        public static int Snap(NavGrid g, P3 p)
        {
            int k = g.CellOf(p.x, p.z); if (g.Walkable(k) && g.InMain(k)) return k;
            if (k >= 0 && g.Room[k] >= 0)
            {
                // BFS through furniture (not walls) so we never snap across a wall into another room
                var q = new Queue<int>(); var seen = new HashSet<int> { k }; q.Enqueue(k);
                while (q.Count > 0 && seen.Count < 900)
                {
                    int c = q.Dequeue(); if (g.Walkable(c) && g.InMain(c)) return c;
                    int ci = c % g.NX, cj = c / g.NX;
                    foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int ni = ci + di, nj = cj + dj; if (ni < 0 || nj < 0 || ni >= g.NX || nj >= g.NZ) continue;
                        int n = ni + nj * g.NX; if (seen.Contains(n) || g.Room[n] < 0 || g.Edge(c, n) == -2) continue;
                        seen.Add(n); q.Enqueue(n);
                    }
                }
            }
            int bi = g.I(p.x), bj = g.J(p.z);
            for (int r = 1; r < 8; r++)
                for (int di = -r; di <= r; di++) for (int dj = -r; dj <= r; dj++)
                    {
                        if (Math.Abs(di) != r && Math.Abs(dj) != r) continue;
                        int i = bi + di, j = bj + dj; if (i < 0 || j < 0 || i >= g.NX || j >= g.NZ) continue;
                        int kk = i + j * g.NX; if (g.Walkable(kk) && g.InMain(kk)) return kk;
                    }
            return -1;
        }

        static bool Clear(NavGrid g, P3 a, P3 b)
        {
            if (a.f != b.f) return false;
            float dx = b.x - a.x, dz = b.z - a.z; float len = (float)Math.Sqrt(dx * dx + dz * dz);
            int steps = Math.Max(1, (int)(len / 0.2f)); int prev = g.CellOf(a.x, a.z);
            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps;
                int k = g.CellOf(a.x + dx * t, a.z + dz * t);
                if (!g.Walkable(k)) return false;
                if (g.NearWall[k] == 1 && s > 1 && s < steps - 1) return false;
                if (k != prev)
                {
                    int pi = prev % g.NX, pj = prev / g.NX, ki = k % g.NX, kj = k / g.NX;
                    if (Math.Abs(pi - ki) + Math.Abs(pj - kj) == 1) { if (g.Edge(prev, k) != -1) return false; }
                    else
                    {
                        int m1 = ki + pj * g.NX, m2 = pi + kj * g.NX;
                        if (!g.Walkable(m1) || !g.Walkable(m2) || g.Edge(prev, m1) != -1 || g.Edge(m1, k) != -1 || g.Edge(prev, m2) != -1 || g.Edge(m2, k) != -1) return false;
                    }
                    prev = k;
                }
            }
            return true;
        }
    }
}
