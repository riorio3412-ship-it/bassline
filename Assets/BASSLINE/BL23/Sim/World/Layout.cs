using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BL23.Sim
{
    [Serializable]
    public sealed class FloorInfo { public int F; public RectF Bounds; public float BaseY; public float Height; }

    [Serializable]
    public sealed class ItemSpawn { public string Type; public int Room; public P3 Pos; public float Yaw; public string Owner; public int Furniture = -1; public string Name; }

    [Serializable]
    public sealed class WallSeg
    {
        public int Floor; public float x0, z0, x1, z1;   // axis-aligned
        public int RoomA, RoomB;                           // RoomB -1 = exterior
        public int DoorId = -1;                            // if this segment is a door opening
        public bool Open;                                  // passage opening without door leaf
        public bool Exterior;
    }

    /// <summary>The generated mansion for one loop. Serialized fully in the save (not just the seed).</summary>
    [Serializable]
    public sealed class Layout
    {
        public int LoopId; public ulong Seed; public string Skeleton; public int Attempts;
        public List<FloorInfo> Floors = new List<FloorInfo>();
        public List<Room> Rooms = new List<Room>();
        public List<Door> Doors = new List<Door>();
        public List<Stair> Stairs = new List<Stair>();
        public List<Furniture> Furniture = new List<Furniture>();
        public List<Spot> Spots = new List<Spot>();
        public List<Circuit> Circuits = new List<Circuit>();
        public List<ItemSpawn> ItemSpawns = new List<ItemSpawn>();
        public List<string> MysteryTypes = new List<string>();
        public List<string> GenLog = new List<string>();
        public string Hash;

        [NonSerialized] Dictionary<int, NavGrid> _nav;
        [NonSerialized] List<WallSeg> _walls;

        public Room Room(int id) => id >= 0 && id < Rooms.Count ? Rooms[id] : null;
        public Door Door(int id) => id >= 0 && id < Doors.Count ? Doors[id] : null;
        public FloorInfo Floor(int f) => Floors.FirstOrDefault(x => x.F == f);
        public IEnumerable<Room> OfType(RoomType t) => Rooms.Where(r => r.Type == t);
        public Room First(RoomType t) => Rooms.FirstOrDefault(r => r.Type == t);
        public Room BedroomOf(string actor) => Rooms.FirstOrDefault(r => r.Type == RoomType.Bedroom && r.Owner == actor);

        public NavGrid Nav(int floor)
        {
            if (_nav == null) _nav = new Dictionary<int, NavGrid>();
            if (!_nav.TryGetValue(floor, out var g)) { g = new NavGrid(this, floor); _nav[floor] = g; }
            return g;
        }
        public void InvalidateNav() { _nav = null; }
        public void InvalidateNav(int floor) { _nav?.Remove(floor); }

        public int RoomAt(P3 p)
        {
            var g = Nav(p.f); int r = g.RoomAtWorld(p.x, p.z); if (r >= 0) return r;
            foreach (var room in Rooms) if (room.Floor == p.f && room.Rect.Contains(p.x, p.z)) return room.Id;
            return -1;
        }

        public float FloorY(int f) => Floor(f)?.BaseY ?? 0f;

        public IEnumerable<int> Neighbors(int roomId)
        {
            var r = Room(roomId); if (r == null) yield break;
            foreach (var d in r.Doors) { var door = Doors[d]; yield return door.RoomA == roomId ? door.RoomB : door.RoomA; }
            foreach (var o in OpenNeighbors(roomId)) yield return o;
            foreach (var s in Stairs) { if (s.RoomA == roomId) yield return s.RoomB; else if (s.RoomB == roomId) yield return s.RoomA; }
        }

        [NonSerialized] Dictionary<int, List<int>> _open;
        public List<int> OpenNeighbors(int roomId)
        {
            if (_open == null)
            {
                _open = new Dictionary<int, List<int>>();
                foreach (var w in Walls()) if (w.Open && w.RoomB >= 0)
                    {
                        if (!_open.TryGetValue(w.RoomA, out var la)) _open[w.RoomA] = la = new List<int>();
                        if (!_open.TryGetValue(w.RoomB, out var lb)) _open[w.RoomB] = lb = new List<int>();
                        if (!la.Contains(w.RoomB)) la.Add(w.RoomB); if (!lb.Contains(w.RoomA)) lb.Add(w.RoomA);
                    }
            }
            return _open.TryGetValue(roomId, out var l) ? l : new List<int>();
        }

        /// <summary>Axis-aligned wall segments with door openings. Shared by nav, perception and the Unity builder.</summary>
        public List<WallSeg> Walls()
        {
            if (_walls != null) return _walls;
            _walls = WallBuilder.Build(this);
            return _walls;
        }

        public string ComputeHash()
        {
            var sb = new StringBuilder();
            sb.Append(Seed).Append('|').Append(Skeleton);
            foreach (var r in Rooms) sb.Append(r.Id).Append(r.Type).Append(r.Floor).Append(r.Rect.ToString()).Append(r.Owner).Append(r.Variant);
            foreach (var d in Doors) sb.Append(d.RoomA).Append(d.RoomB).Append(d.Pos.x.ToString("0.0")).Append(d.Pos.z.ToString("0.0"));
            foreach (var f in Furniture) sb.Append(f.Type).Append(f.Pos.x.ToString("0.0")).Append(f.Pos.z.ToString("0.0"));
            return Rng.Hash(sb.ToString()).ToString("X16");
        }
    }

    static class WallBuilder
    {
        // Walls are computed on a 0.5m lattice from room occupancy; openings from doors and passage adjacency.
        public static List<WallSeg> Build(Layout L)
        {
            var result = new List<WallSeg>();
            foreach (var fi in L.Floors)
            {
                const float c = 0.5f;
                var b = fi.Bounds; int nx = (int)Math.Round(b.W / c), nz = (int)Math.Round(b.D / c);
                var cell = new int[nx, nz];
                for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) cell[i, j] = -1;
                foreach (var r in L.Rooms.Where(r => r.Floor == fi.F))
                {
                    int i0 = (int)Math.Round((r.Rect.x0 - b.x0) / c), i1 = (int)Math.Round((r.Rect.x1 - b.x0) / c);
                    int j0 = (int)Math.Round((r.Rect.z0 - b.z0) / c), j1 = (int)Math.Round((r.Rect.z1 - b.z0) / c);
                    for (int i = Math.Max(0, i0); i < Math.Min(nx, i1); i++) for (int j = Math.Max(0, j0); j < Math.Min(nz, j1); j++) cell[i, j] = r.Id;
                }
                Func<int, bool> passage = id => id >= 0 && RoomInfo.IsPassage(L.Rooms[id].Type) && !L.Rooms[id].Void;
                // vertical boundaries (walls running along z at x = b.x0 + i*c)
                for (int i = 0; i <= nx; i++)
                {
                    WallSeg cur = null;
                    for (int j = 0; j < nz; j++)
                    {
                        int a = i > 0 ? cell[i - 1, j] : -1, bb = i < nx ? cell[i, j] : -1;
                        WallSeg want = null;
                        if (a != bb && (a >= 0 || bb >= 0))
                        {
                            int ra = a >= 0 ? a : bb, rb = a >= 0 ? bb : -1;
                            bool open = passage(a) && passage(bb);
                            bool voidEdge = (a >= 0 && L.Rooms[a].Void) || (bb >= 0 && L.Rooms[bb].Void);
                            if (voidEdge) open = false;
                            float x = b.x0 + i * c, z = b.z0 + j * c;
                            int door = DoorAt(L, fi.F, x, z + c * 0.5f, false);
                            want = new WallSeg { Floor = fi.F, x0 = x, x1 = x, z0 = z, z1 = z + c, RoomA = ra, RoomB = rb, Open = open, Exterior = rb < 0, DoorId = door };
                        }
                        if (want != null && cur != null && Same(cur, want) && Math.Abs(cur.z1 - want.z0) < 0.01f) cur.z1 = want.z1;
                        else { if (cur != null) result.Add(cur); cur = want; }
                    }
                    if (cur != null) result.Add(cur);
                }
                // horizontal boundaries (walls running along x at z = b.z0 + j*c)
                for (int j = 0; j <= nz; j++)
                {
                    WallSeg cur = null;
                    for (int i = 0; i < nx; i++)
                    {
                        int a = j > 0 ? cell[i, j - 1] : -1, bb = j < nz ? cell[i, j] : -1;
                        WallSeg want = null;
                        if (a != bb && (a >= 0 || bb >= 0))
                        {
                            int ra = a >= 0 ? a : bb, rb = a >= 0 ? bb : -1;
                            bool open = passage(a) && passage(bb);
                            bool voidEdge = (a >= 0 && L.Rooms[a].Void) || (bb >= 0 && L.Rooms[bb].Void);
                            if (voidEdge) open = false;
                            float x = b.x0 + i * c, z = b.z0 + j * c;
                            int door = DoorAt(L, fi.F, x + c * 0.5f, z, true);
                            want = new WallSeg { Floor = fi.F, x0 = x, x1 = x + c, z0 = z, z1 = z, RoomA = ra, RoomB = rb, Open = open, Exterior = rb < 0, DoorId = door };
                        }
                        if (want != null && cur != null && Same(cur, want) && Math.Abs(cur.x1 - want.x0) < 0.01f) cur.x1 = want.x1;
                        else { if (cur != null) result.Add(cur); cur = want; }
                    }
                    if (cur != null) result.Add(cur);
                }
            }
            return result;
        }
        static bool Same(WallSeg a, WallSeg b) => a.RoomA == b.RoomA && a.RoomB == b.RoomB && a.Open == b.Open && a.DoorId == b.DoorId;
        static int DoorAt(Layout L, int f, float x, float z, bool alongX)
        {
            foreach (var d in L.Doors)
            {
                if (d.Pos.f != f || d.AlongX != alongX) continue;
                float half = d.Width * 0.5f - 0.24f;
                if (alongX) { if (Math.Abs(d.Pos.z - z) < 0.05f && Math.Abs(d.Pos.x - x) <= half) return d.Id; }
                else { if (Math.Abs(d.Pos.x - x) < 0.05f && Math.Abs(d.Pos.z - z) <= half) return d.Id; }
            }
            return -1;
        }
    }
}
