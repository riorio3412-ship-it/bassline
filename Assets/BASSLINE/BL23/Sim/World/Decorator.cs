using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Room dressing rules. Every functional room gets a distinct furniture identity and activity spots; mystery rooms
    /// vary their interior per loop. Furniture here is the physical/nav truth; the Unity layer adds non-blocking decor.
    /// </summary>
    public static class Decorator
    {
        sealed class Dresser
        {
            public Layout L; public Room Room; public RectF R; public Rng Rng; public List<RectF> Keep = new List<RectF>(); public List<RectF> Used = new List<RectF>();
            /// <summary>Parallel to Used: flat pieces (rugs) lie under furniture and never block other placements.</summary>
            public List<bool> UsedFlat = new List<bool>();
            public static bool IsFlat(string type) => type == "Rug";
            public void PopUsed() { Used.RemoveAt(Used.Count - 1); UsedFlat.RemoveAt(UsedFlat.Count - 1); }
            /// <summary>Walking lanes: while on, a standing piece is refused when it would leave two of the room's doors without
            /// a 1.2 m wide lane between them (people should never have to shove through furniture to cross a room).</summary>
            public bool LaneGuard;
            /// <summary>Floor kept clear in front of working furniture (stove, shelves, desk...): where someone stands to use it.</summary>
            public List<RectF> Front = new List<RectF>(); public List<int> FrontOwner = new List<int>();
            public const float LaneHalf = 0.6f, WallMargin = 0.45f, LaneCell = 0.25f;
            /// <summary>Pieces the lane check ignores: flat, hanging, wall-mounted, or over water.</summary>
            static bool LaneIgnores(string type) => type == "Rug" || type == "Chandelier" || type == "Dartboard" || type == "DoorLogger" || type == "DivingBoard" || type == "ShallowWater";

            /// <summary>A piece the room cannot do without (its table, its altar, its piano): placed lane-aware if it can be,
            /// else placed anyway.</summary>
            public Furniture Must(Func<Furniture> place)
            {
                var f = place(); if (f != null || !LaneGuard) return f;
                LaneGuard = false; try { return place(); } finally { LaneGuard = true; }
            }

            /// <summary>Share of the room's interior (1.4 m in from the walls) with something standing on it.</summary>
            public float InteriorFill()
            {
                var I = new RectF(R.x0 + 1.3f, R.z0 + 1.3f, R.x1 - 1.3f, R.z1 - 1.3f);
                if (I.W <= 0.2f || I.D <= 0.2f) return 1f;
                float cov = 0;
                foreach (var fid in Room.Furniture)
                {
                    var f = L.Furniture[fid]; if (f.H < 0.3f || IsFlat(f.Type) || LaneIgnores(f.Type)) continue;
                    NavGrid.GetFootprint(f, 0f, out var a);
                    float ox = Math.Min(a.x1, I.x1) - Math.Max(a.x0, I.x0), oz = Math.Min(a.z1, I.z1) - Math.Max(a.z0, I.z0);
                    if (ox > 0 && oz > 0) cov += ox * oz;
                }
                return cov / (I.W * I.D);
            }

            /// <summary>Undo every placement made after furniture count 'count' (spots, clear zones and footprints too).</summary>
            public void RollbackTo(int count)
            {
                while (L.Furniture.Count > count)
                {
                    var f = L.Furniture[L.Furniture.Count - 1];
                    Room.Furniture.Remove(f.Id);
                    L.Spots.RemoveAll(s => s.Furniture == f.Id); Room.Spots.RemoveAll(sid => sid >= L.Spots.Count);
                    for (int i = FrontOwner.Count - 1; i >= 0; i--) if (FrontOwner[i] == f.Id) { FrontOwner.RemoveAt(i); Front.RemoveAt(i); }
                    L.Furniture.RemoveAt(L.Furniture.Count - 1); PopUsed();
                }
            }

            /// <summary>True when every pair of doors of this room stays joined by a lane 1.2 m wide with 'extra' also standing.</summary>
            public bool LanesOk() => LanesOk(new RectF(-9999, -9999, -9998, -9998));
            public bool LanesOk(RectF extra)
            {
                if (Room.Doors.Count < 2) return true;
                var RR = Room.Rect; const float C = LaneCell;
                int nx = (int)Math.Ceiling(RR.W / C), nz = (int)Math.Ceiling(RR.D / C); if (nx <= 0 || nz <= 0) return true;
                var block = new bool[nx, nz];
                void Mark(RectF r)
                {
                    int i0 = Math.Max(0, (int)Math.Floor((r.x0 - LaneHalf - RR.x0) / C)), i1 = Math.Min(nx - 1, (int)Math.Floor((r.x1 + LaneHalf - RR.x0) / C));
                    int j0 = Math.Max(0, (int)Math.Floor((r.z0 - LaneHalf - RR.z0) / C)), j1 = Math.Min(nz - 1, (int)Math.Floor((r.z1 + LaneHalf - RR.z0) / C));
                    for (int i = i0; i <= i1; i++) for (int j = j0; j <= j1; j++)
                        {
                            float cx = RR.x0 + (i + 0.5f) * C, cz = RR.z0 + (j + 0.5f) * C;
                            if (cx > r.x0 - LaneHalf && cx < r.x1 + LaneHalf && cz > r.z0 - LaneHalf && cz < r.z1 + LaneHalf) block[i, j] = true;
                        }
                }
                for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
                    {
                        float cx = RR.x0 + (i + 0.5f) * C, cz = RR.z0 + (j + 0.5f) * C;
                        if (cx < RR.x0 + WallMargin || cx > RR.x1 - WallMargin || cz < RR.z0 + WallMargin || cz > RR.z1 - WallMargin) block[i, j] = true;
                    }
                foreach (var fid in Room.Furniture) { var f = L.Furniture[fid]; if (LaneIgnores(f.Type) || f.H < 0.05f) continue; NavGrid.GetFootprint(f, 0f, out var fr); Mark(fr); }
                Mark(extra);
                var comp = new int[nx, nz]; int first = 0;
                var q = new Queue<(int, int)>();
                foreach (var did in Room.Doors)
                {
                    var d = L.Doors[did];
                    float dx = d.AlongX ? 0 : (d.Pos.x < RR.CX ? 0.8f : -0.8f), dz = d.AlongX ? (d.Pos.z < RR.CZ ? 0.8f : -0.8f) : 0;
                    int ci = Math.Max(0, Math.Min(nx - 1, (int)((d.Pos.x + dx - RR.x0) / C))), cj = Math.Max(0, Math.Min(nz - 1, (int)((d.Pos.z + dz - RR.z0) / C)));
                    if (block[ci, cj]) return false;
                    if (first == 0)
                    {
                        first = 1; comp[ci, cj] = 1; q.Enqueue((ci, cj));
                        while (q.Count > 0)
                        {
                            var (a, b) = q.Dequeue();
                            if (a > 0 && !block[a - 1, b] && comp[a - 1, b] == 0) { comp[a - 1, b] = 1; q.Enqueue((a - 1, b)); }
                            if (a < nx - 1 && !block[a + 1, b] && comp[a + 1, b] == 0) { comp[a + 1, b] = 1; q.Enqueue((a + 1, b)); }
                            if (b > 0 && !block[a, b - 1] && comp[a, b - 1] == 0) { comp[a, b - 1] = 1; q.Enqueue((a, b - 1)); }
                            if (b < nz - 1 && !block[a, b + 1] && comp[a, b + 1] == 0) { comp[a, b + 1] = 1; q.Enqueue((a, b + 1)); }
                        }
                    }
                    else if (comp[ci, cj] != 1) return false;
                }
                return true;
            }

            /// <summary>Free stretches along wall 'side' (0=S 1=N 2=W 3=E) for a piece 'depth' deep: wall coordinates minus doors,
            /// stairs and whatever already stands against that wall. Returned as (start, end) along the wall from R's corner.</summary>
            public List<(float a, float b)> FreeRuns(int side, float depth)
            {
                float len = side < 2 ? R.W : R.D;
                var cuts = new List<(float a, float b)>();
                // the band of floor in front of this wall
                RectF band = side == 0 ? new RectF(R.x0, R.z0, R.x1, R.z0 + depth + 0.3f) : side == 1 ? new RectF(R.x0, R.z1 - depth - 0.3f, R.x1, R.z1)
                    : side == 2 ? new RectF(R.x0, R.z0, R.x0 + depth + 0.3f, R.z1) : new RectF(R.x1 - depth - 0.3f, R.z0, R.x1, R.z1);
                void Cut(RectF r, float m) { if (!r.Overlaps(band)) return; if (side < 2) cuts.Add((r.x0 - R.x0 - m, r.x1 - R.x0 + m)); else cuts.Add((r.z0 - R.z0 - m, r.z1 - R.z0 + m)); }
                foreach (var k in Keep) Cut(k, 0.05f);
                for (int i = 0; i < Used.Count; i++) if (!UsedFlat[i]) Cut(Used[i], 0.12f);
                foreach (var k in Front) Cut(k, 0.02f);
                cuts.Sort((x, y) => x.a.CompareTo(y.a));
                var runs = new List<(float a, float b)>(); float cur = 0;
                foreach (var c in cuts) { if (c.a > cur + 0.4f) runs.Add((cur, Math.Min(len, c.a))); cur = Math.Max(cur, c.b); }
                if (len - cur > 0.4f) runs.Add((cur, len));
                return runs;
            }

            /// <summary>Back against wall 'side' with its centre 'along' metres from the wall's start.</summary>
            public Furniture At(string type, int side, float along, float clearance = 0.12f, string tint = null)
            {
                var def = FurnitureCatalog.Get(type); if (def == null) return null;
                float yaw = side == 0 ? 0 : side == 1 ? 180 : side == 2 ? 90 : 270;
                float x, z;
                if (side < 2) { x = R.x0 + along; z = side == 0 ? R.z0 + def.D / 2 + 0.02f : R.z1 - def.D / 2 - 0.02f; }
                else { z = R.z0 + along; x = side == 2 ? R.x0 + def.D / 2 + 0.02f : R.x1 - def.D / 2 - 0.02f; }
                return Place(type, x, z, yaw, false, tint, clearance);
            }

            /// <summary>A fitted run of pieces standing flush side by side against one wall (a kitchen line, a workshop wall),
            /// centred on 't' (0..1) inside the longest free stretch. Optional trailing pieces are dropped until it fits.</summary>
            public List<Furniture> Run(int side, string[] types, float t = 0.5f, float gap = 0.03f, int required = 1)
            {
                var res = new List<Furniture>();
                var runs = FreeRuns(side, 0.8f); if (runs.Count == 0) return res;
                var best = runs.OrderByDescending(r => r.b - r.a).First();
                for (int n = types.Length; n >= required && res.Count == 0; n--)
                {
                    float total = 0; for (int i = 0; i < n; i++) total += FurnitureCatalog.Get(types[i]).W + (i > 0 ? gap : 0);
                    float len = best.b - best.a; if (total > len - 0.05f) continue;
                    float start = best.a + (len - total) * MathX.Clamp01(t);
                    int before = L.Furniture.Count; float cur = start;
                    for (int i = 0; i < n; i++)
                    {
                        var w = FurnitureCatalog.Get(types[i]).W;
                        var f = At(types[i], side, cur + w / 2, 0.01f);
                        if (f == null) { RollbackTo(before); res.Clear(); break; }
                        res.Add(f); cur += w + gap;
                    }
                }
                return res;
            }
            /// <summary>Floor area taken by standing furniture (rugs excluded).</summary>
            public float SolidArea() { float a = 0; for (int i = 0; i < Used.Count; i++) if (!UsedFlat[i]) a += Math.Max(0, Used[i].W) * Math.Max(0, Used[i].D); return a; }
            public Dresser(Layout l, Room r, Rng rng)
            {
                L = l; Room = r; Rng = rng; R = r.Rect.Inset(0.12f);
                foreach (var did in r.Doors)
                {
                    var d = l.Doors[did]; float hw = d.Width / 2f + 0.35f, dep = 1.5f;
                    Keep.Add(d.AlongX ? new RectF(d.Pos.x - hw, d.Pos.z - dep, d.Pos.x + hw, d.Pos.z + dep) : new RectF(d.Pos.x - dep, d.Pos.z - hw, d.Pos.x + dep, d.Pos.z + hw));
                }
                foreach (var s in l.Stairs)
                {
                    if (s.A.f == r.Floor && r.Rect.Contains(s.A.x, s.A.z)) Keep.Add(new RectF(s.A.x - 1.4f, s.A.z - 1.4f, s.A.x + 1.4f, s.A.z + 1.4f));
                    if (s.B.f == r.Floor && r.Rect.Contains(s.B.x, s.B.z)) Keep.Add(new RectF(s.B.x - 1.4f, s.B.z - 1.4f, s.B.x + 1.4f, s.B.z + 1.4f));
                }
                foreach (var fid in r.Furniture) { var f = l.Furniture[fid]; NavGrid.GetFootprint(f, 0.05f, out var fr); Used.Add(fr); UsedFlat.Add(IsFlat(f.Type)); }
            }

            public Furniture Place(string type, float x, float z, float yaw, bool force = false, string tint = null, float clearance = 0.35f)
            {
                var def = FurnitureCatalog.Get(type); if (def == null) return null;
                var f = new Furniture { Id = L.Furniture.Count, Room = Room.Id, Type = type, Pos = new P3(Room.Floor, x, z), Yaw = yaw, W = def.W, D = def.D, H = def.H, Blocks = def.Blocks, Material = def.Mat, Variant = Rng.R(4), Tint = tint };
                NavGrid.GetFootprint(f, 0f, out var fp);
                bool flat = IsFlat(type);
                if (!force)
                {
                    if (fp.x0 < R.x0 || fp.z0 < R.z0 || fp.x1 > R.x1 || fp.z1 > R.z1) return null;
                    if (!flat)
                    {
                        foreach (var k in Keep) if (fp.Overlaps(k)) return null;
                        var fpc = def.Blocks ? new RectF(fp.x0 - clearance, fp.z0 - clearance, fp.x1 + clearance, fp.z1 + clearance) : fp;
                        for (int i = 0; i < Used.Count; i++) if (!UsedFlat[i] && fpc.Overlaps(Used[i])) return null;
                        if (def.Blocks) foreach (var k in Front) if (fp.Overlaps(k)) return null;
                        if (LaneGuard && !LaneIgnores(type) && !LanesOk(fp)) return null;
                    }
                }
                f.Origin = f.Pos;
                L.Furniture.Add(f); Room.Furniture.Add(f.Id); Used.Add(fp); UsedFlat.Add(flat);
                AddSpots(f, def);
                return f;
            }

            /// <summary>A rug of any size (clamped inside the room), lying under whatever stands on it.</summary>
            public Furniture Rug(float x, float z, float w, float d, float yaw = 0)
            {
                var def = FurnitureCatalog.Get("Rug"); if (def == null) return null;
                bool rot = Math.Abs(Math.Round(yaw / 90f)) % 2 == 1;
                float ww = rot ? d : w, dd = rot ? w : d;
                float mx = R.W - 0.5f, mz = R.D - 0.5f;
                if (ww > mx) ww = mx; if (dd > mz) dd = mz;
                if (ww < 1.2f || dd < 1.0f) return null;
                x = MathX.Clamp(x, R.x0 + 0.25f + ww / 2, R.x1 - 0.25f - ww / 2); z = MathX.Clamp(z, R.z0 + 0.25f + dd / 2, R.z1 - 0.25f - dd / 2);
                var f = new Furniture { Id = L.Furniture.Count, Room = Room.Id, Type = "Rug", Pos = new P3(Room.Floor, x, z), Yaw = yaw, W = rot ? dd : ww, D = rot ? ww : dd, H = def.H, Blocks = false, Material = def.Mat, Variant = Rng.R(4) };
                f.Origin = f.Pos;
                NavGrid.GetFootprint(f, 0f, out var fp);
                L.Furniture.Add(f); Room.Furniture.Add(f.Id); Used.Add(fp); UsedFlat.Add(true);
                return f;
            }

            void AddSpots(Furniture f, FurnitureDef def)
            {
                if (def.Spots.Length == 0) return;
                double yr = f.Yaw * Math.PI / 180.0; float fx = (float)Math.Sin(yr), fz = (float)Math.Cos(yr); // forward
                float rx = fz, rz = -fx; // right
                int n = def.Spots.Length;
                for (int i = 0; i < n; i++)
                {
                    string tag = def.Spots[i];
                    bool onSeat = tag == "sit" || tag == "sleep" || tag == "rest" || (tag == "pray" && f.Type == "Pew");
                    float lateral = n == 1 ? 0 : (i - (n - 1) / 2f) * (def.W / n);
                    float fwd = onSeat ? (tag == "sleep" || tag == "rest" ? 0f : 0.05f) : def.D / 2f + 0.45f;
                    var p = new P3(f.Pos.f, f.Pos.x + rx * lateral + fx * fwd, f.Pos.z + rz * lateral + fz * fwd);
                    float yaw = onSeat ? f.Yaw : f.Yaw + 180f;
                    P3 ap = p;
                    if (onSeat)
                    {
                        if (tag == "sleep" || tag == "rest") ap = new P3(f.Pos.f, p.x + rx * (def.W / 2f + 0.5f), p.z + rz * (def.W / 2f + 0.5f));
                        else ap = new P3(f.Pos.f, p.x + fx * (def.D / 2f + 0.5f), p.z + fz * (def.D / 2f + 0.5f));
                    }
                    var s = new Spot { Id = L.Spots.Count, Room = Room.Id, Furniture = f.Id, Pos = p, Yaw = yaw, Tag = tag, Approach = ap, OnFurniture = onSeat };
                    L.Spots.Add(s); Room.Spots.Add(s.Id);
                    // where someone stands to use it stays clear of later pieces
                    if (!onSeat && def.Blocks) { Front.Add(new RectF(p.x - 0.4f, p.z - 0.4f, p.x + 0.4f, p.z + 0.4f)); FrontOwner.Add(f.Id); }
                }
            }

            public void Spot(string tag, float x, float z, float yaw)
            {
                if (!R.Contains(x, z)) return;
                foreach (var u in Used) if (u.Contains(x, z)) return;
                foreach (var k in Keep) if (k.Contains(x, z)) return;
                var s = new Spot { Id = L.Spots.Count, Room = Room.Id, Pos = new P3(Room.Floor, x, z), Yaw = yaw, Tag = tag, Approach = new P3(Room.Floor, x, z) };
                L.Spots.Add(s); Room.Spots.Add(s.Id);
            }

            public void Item(string type, float x, float z, string owner = null, string name = null) => L.ItemSpawns.Add(new ItemSpawn { Type = type, Room = Room.Id, Pos = new P3(Room.Floor, x, z), Owner = owner, Name = name, Yaw = Rng.Range(0, 360) });
            public void ItemOn(Furniture f, string type, string owner = null) { if (f == null) { Item(type, R.CX + Rng.Range(-1, 1), R.CZ + Rng.Range(-1, 1), owner); return; } L.ItemSpawns.Add(new ItemSpawn { Type = type, Room = Room.Id, Pos = new P3(Room.Floor, f.Pos.x + Rng.Range(-f.W * 0.3f, f.W * 0.3f), f.Pos.z + Rng.Range(-0.15f, 0.15f)), Owner = owner, Furniture = f.Id, Yaw = Rng.Range(0, 360) }); }

            /// <summary>Place furniture with its back against a wall. side 0=S 1=N 2=W 3=E. t in 0..1 along the wall.</summary>
            public Furniture Wall(string type, int side, float t, float gap = 0.02f, string tint = null)
            {
                var def = FurnitureCatalog.Get(type); if (def == null) return null;
                float yaw = side == 0 ? 0 : side == 1 ? 180 : side == 2 ? 90 : 270;
                float x, z;
                if (side < 2) { x = R.x0 + def.W / 2 + t * (R.W - def.W); z = side == 0 ? R.z0 + def.D / 2 + gap : R.z1 - def.D / 2 - gap; }
                else { z = R.z0 + def.W / 2 + t * (R.D - def.W); x = side == 2 ? R.x0 + def.D / 2 + gap : R.x1 - def.D / 2 - gap; }
                return Place(type, x, z, yaw, false, tint, 0.2f);
            }

            /// <summary>Line the wall with as many copies as fit.</summary>
            public List<Furniture> Line(string type, int side, float spacing = 0.15f, int max = 99)
            {
                // an even row with one constant gap, centred in each free stretch of the wall (doors and other pieces split it);
                // a capped row goes whole into the longest stretch
                var res = new List<Furniture>(); var def = FurnitureCatalog.Get(type); if (def == null) return res; float step = def.W + spacing;
                var runs = FreeRuns(side, def.D);
                if (max < 99) runs = runs.OrderByDescending(r => r.b - r.a).ToList();
                foreach (var (a, b) in runs)
                {
                    float len = b - a;
                    int n = Math.Min(max - res.Count, (int)((len + spacing) / step));
                    if (n <= 0) continue;
                    float used = n * def.W + (n - 1) * spacing, start = a + (len - used) / 2 + def.W / 2;
                    for (int i = 0; i < n; i++) { var f = At(type, side, start + i * step, Math.Min(0.2f, spacing * 0.8f)); if (f != null) res.Add(f); }
                    if (res.Count >= max) break;
                }
                return res;
            }

            public Furniture Center(string type, float yaw = 0, float dx = 0, float dz = 0) => Place(type, R.CX + dx, R.CZ + dz, yaw);

            /// <summary>Facility furniture the game logic depends on (switchboard, press console, boiler, stove...): every wall and
            /// position is tried, then a grid search, and as a last resort it is forced in at the centre. Never silently missing.</summary>
            public Furniture Ensure(string type, params int[] preferSides)
            {
                if (Room.Furniture.Any(id => L.Furniture[id].Type == type)) return L.Furniture[Room.Furniture.First(id => L.Furniture[id].Type == type)];
                var sides = preferSides.Concat(new[] { 0, 1, 2, 3 }).Distinct();
                foreach (var s in sides) foreach (var tt in new[] { 0.5f, 0.3f, 0.7f, 0.12f, 0.88f }) { var f = Wall(type, s, tt); if (f != null) return f; }
                var c = Center(type); if (c != null) return c;
                for (float z = R.z0 + 0.6f; z < R.z1 - 0.6f; z += 0.5f) for (float x = R.x0 + 0.6f; x < R.x1 - 0.6f; x += 0.5f) foreach (var yaw in new[] { 0f, 90f }) { var f = Place(type, x, z, yaw, false, null, 0.1f); if (f != null) return f; }
                return Place(type, R.CX, R.CZ, 0, true);
            }
            public int LongSide => R.W >= R.D ? 0 : 2;
            public float Wd => R.W; public float Dd => R.D;
        }

        public static void Furnish(Layout L, Rng rng)
        {
            foreach (var room in L.Rooms.ToList())
            {
                var d = new Dresser(L, room, rng);
                d.LaneGuard = true;   // nothing may cut the walking lanes between doors (identity pieces use Must)
                try { Dress(d, room, rng); } catch (Exception e) { L.GenLog.Add("dress error " + room.Name + ": " + e.Message); }
                d.LaneGuard = true;   // from here on nothing may cut the walking lanes between doors
                try { Zones(d, room, rng); } catch (Exception e) { L.GenLog.Add("zone error " + room.Name + ": " + e.Message); }
                try { Island(d, room, rng); } catch (Exception e) { L.GenLog.Add("island error " + room.Name + ": " + e.Message); }
                try { Perimeter(d, room, rng); } catch (Exception e) { L.GenLog.Add("perimeter error " + room.Name + ": " + e.Message); }
                if ((room.Type == RoomType.Lounge || room.Type == RoomType.Dining || room.Type == RoomType.Library)
                    && !room.Furniture.Any(id => L.Furniture[id].Type == "Clock"))
                    foreach (var side in new[] { 1, 3, 0, 2 }) { if (d.Wall("Clock", side, 0.92f) != null) break; }
                FixReach(L, room);
            }
            // compact spot ids after drops
            var keepIds = new HashSet<int>(L.Rooms.SelectMany(r => r.Spots));
            var map = new Dictionary<int, int>(); var list = new List<Spot>();
            foreach (var s in L.Spots) if (keepIds.Contains(s.Id)) { map[s.Id] = list.Count; s.Id = list.Count; list.Add(s); }
            L.Spots = list;
            foreach (var r in L.Rooms) r.Spots = r.Spots.Where(map.ContainsKey).Select(x => map[x]).ToList();
        }

        /// <summary>Guarantee every spot has a walkable approach connected to the room's doors (mirrors NavGrid inflation).</summary>
        static void FixReach(Layout L, Room room)
        {
            if (room.Spots.Count == 0) return;
            const float C = NavGrid.C; var R = room.Rect;
            int nx = (int)Math.Round(R.W / C), nz = (int)Math.Round(R.D / C); if (nx <= 0 || nz <= 0) return;
            var block = new bool[nx, nz];
            foreach (var fid in room.Furniture)
            {
                var f = L.Furniture[fid]; if (!f.Blocks) continue;
                NavGrid.GetFootprint(f, 0.28f, out var fr);
                for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
                    {
                        float cx0 = R.x0 + i * C, cz0 = R.z0 + j * C;
                        if (cx0 < fr.x1 && cx0 + C > fr.x0 && cz0 < fr.z1 && cz0 + C > fr.z0) block[i, j] = true;
                    }
            }
            var reach = new bool[nx, nz]; var q = new Queue<(int, int)>();
            void Seed(float x, float z) { int i = (int)Math.Floor((x - R.x0) / C), j = (int)Math.Floor((z - R.z0) / C); if (i >= 0 && j >= 0 && i < nx && j < nz) { block[i, j] = false; if (!reach[i, j]) { reach[i, j] = true; q.Enqueue((i, j)); } } }
            foreach (var did in room.Doors)
            {
                var dr = L.Doors[did];
                for (float s = -dr.Width * 0.4f; s <= dr.Width * 0.4f; s += 0.25f)
                    for (float t = -0.9f; t <= 0.9f; t += 0.5f)
                        Seed(dr.AlongX ? dr.Pos.x + s : dr.Pos.x + t, dr.AlongX ? dr.Pos.z + t : dr.Pos.z + s);
            }
            if (room.Doors.Count == 0 || RoomInfo.IsPassage(room.Type)) { for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) if (!block[i, j] && q.Count == 0) { reach[i, j] = true; q.Enqueue((i, j)); } }
            while (q.Count > 0)
            {
                var (ci, cj) = q.Dequeue();
                foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int ni = ci + di, nj = cj + dj; if (ni < 0 || nj < 0 || ni >= nx || nj >= nz || block[ni, nj] || reach[ni, nj]) continue;
                    reach[ni, nj] = true; q.Enqueue((ni, nj));
                }
            }
            bool Ok(P3 p) { int i = (int)Math.Floor((p.x - R.x0) / C), j = (int)Math.Floor((p.z - R.z0) / C); return i >= 0 && j >= 0 && i < nx && j < nz && reach[i, j]; }
            var keep = new List<int>();
            foreach (var sid in room.Spots)
            {
                var s = L.Spots[sid];
                if (Ok(s.Approach)) { keep.Add(sid); continue; }
                // try alternatives around the spot / its furniture
                var cands = new List<P3>();
                float ext = 0.9f;
                if (s.Furniture >= 0) { var f = L.Furniture[s.Furniture]; ext = Math.Max(f.W, f.D) / 2f + 0.45f; }
                for (int a = 0; a < 16; a++)
                {
                    double ang = a / 16.0 * Math.PI * 2; for (float r = 0.5f; r <= ext + 0.6f; r += 0.35f)
                        cands.Add(new P3(s.Pos.f, s.Pos.x + (float)Math.Sin(ang) * r, s.Pos.z + (float)Math.Cos(ang) * r));
                }
                var best = cands.Where(Ok).OrderBy(c => c.DistXZ(s.Pos)).Select(c => (P3?)c).FirstOrDefault();
                if (best.HasValue) { s.Approach = best.Value; if (!s.OnFurniture) s.Pos = best.Value; keep.Add(sid); }
                else L.GenLog.Add($"spot dropped {room.Name}:{s.Tag}");
            }
            room.Spots = keep;
        }

        static bool TableWithChairs(Dresser d, string table, float x, float z, float yaw, int perSide, string tint = null, bool rug = false, bool heads = false, bool lanes = false)
        {
            int start = d.L.Furniture.Count;
            var def = FurnitureCatalog.Get(table);
            bool rot = Math.Abs(Math.Round(yaw / 90f)) % 2 == 1;
            float len = def.W, dep = def.D;
            // the rug goes down first: table and chairs stand on it
            if (rug) { if (!rot) d.Rug(x, z, len + 1.9f, dep + 2.7f); else d.Rug(x, z, dep + 2.7f, len + 1.9f); }
            // the table keeps a chair's depth of floor around it, so the chairs never end up in a wall or another piece
            var t = d.Place(table, x, z, yaw, false, tint, 0.8f) ?? d.Place(table, x, z, yaw, false, tint);
            if (t == null && !lanes) t = d.Must(() => d.Place(table, x, z, yaw, false, tint));
            if (t == null) { d.RollbackTo(start); return false; }
            // chairs go down in facing pairs, evenly spaced and pushed in (a seat that cannot stand free is forced so the
            // table keeps its full count of places)
            bool squeezed = false;
            void Pair(float ax, float az, float ay, float bx, float bz, float by)
            {
                int before = d.L.Furniture.Count;
                var a = d.Place("Chair", ax, az, ay, false, null, 0.02f); var b = d.Place("Chair", bx, bz, by, false, null, 0.02f);
                if (a == null || b == null)
                {
                    d.RollbackTo(before);
                    if (lanes) { squeezed = true; return; }   // a trial placement that cannot seat everyone moves on
                    d.Place("Chair", ax, az, ay, true); d.Place("Chair", bx, bz, by, true);
                }
            }
            for (int i = 0; i < perSide; i++)
            {
                float off = (i + 0.5f) / perSide * len - len / 2;
                if (!rot) Pair(x + off, z - dep / 2 - 0.36f, 0, x + off, z + dep / 2 + 0.36f, 180);
                else Pair(x - dep / 2 - 0.36f, z + off, 90, x + dep / 2 + 0.36f, z + off, 270);
            }
            // host and hostess at the two ends of a long table (both or neither)
            if (heads)
            {
                int before = d.L.Furniture.Count; Furniture h1, h2;
                if (!rot) { h1 = d.Place("Chair", x - len / 2 - 0.38f, z, 90, false, null, 0.02f); h2 = d.Place("Chair", x + len / 2 + 0.38f, z, 270, false, null, 0.02f); }
                else { h1 = d.Place("Chair", x, z - len / 2 - 0.38f, 0, false, null, 0.02f); h2 = d.Place("Chair", x, z + len / 2 + 0.38f, 180, false, null, 0.02f); }
                if (h1 == null || h2 == null) d.RollbackTo(before);
            }
            // the whole setting must leave the lanes between the doors (else the caller moves it)
            if (lanes && (squeezed || !d.LanesOk())) { d.RollbackTo(start); return false; }
            return true;
        }

        /// <summary>Nightstands either side of the headboard, a rug across the foot half of the bed, sometimes a chest at its foot.</summary>
        static void Bedside(Dresser d, Furniture bed, Rng rng, bool lived)
        {
            double yr = bed.Yaw * Math.PI / 180; float fx = (float)Math.Sin(yr), fz = (float)Math.Cos(yr); float rx = fz, rz = -fx;
            var def = FurnitureCatalog.Get("Bed"); float hw = def.W / 2, hd = def.D / 2;
            var ns = FurnitureCatalog.Get("Nightstand");
            float lat = hw + ns.W / 2 + 0.05f, back = -(hd - ns.D / 2 - 0.02f);
            foreach (float s in new[] { -1f, 1f }) d.Place("Nightstand", bed.Pos.x + rx * lat * s + fx * back, bed.Pos.z + rz * lat * s + fz * back, bed.Yaw, false, null, 0.02f);
            float ry = (float)(Math.Round(bed.Yaw / 90f) * 90);
            d.Rug(bed.Pos.x + fx * 0.5f, bed.Pos.z + fz * 0.5f, def.W + 1.5f, 2.3f, ry);
            if (rng.Chance(lived ? 0.45 : 0.6))
            {
                var ch = FurnitureCatalog.Get("Chest"); float f = hd + ch.D / 2 + 0.06f;
                d.Place("Chest", bed.Pos.x + fx * f, bed.Pos.z + fz * f, bed.Yaw, false, null, 0.02f);
            }
        }

        /// <summary>A conversation group around a rug: two sofas facing across a low table, armchairs closing the ends and
        /// side tables at the sofa arms. 'alongX' lays the sofas along the x axis (facing each other across z).</summary>
        static bool Conversation(Dresser d, float x, float z, bool alongX, bool armchairs = true, bool sideTables = true, float gap = 1.5f, bool lanes = true)
        {
            int start = d.L.Furniture.Count;
            float rw = alongX ? 5.0f : 4.2f, rd = alongX ? 4.2f : 5.0f;
            if (!armchairs) { if (alongX) rw = 3.4f; else rd = 3.4f; }
            var rug = d.Rug(x, z, rw, rd);
            Furniture s1, s2;
            if (alongX) { s1 = d.Place("Sofa", x, z - gap, 0); s2 = d.Place("Sofa", x, z + gap, 180); }
            else { s1 = d.Place("Sofa", x - gap, z, 90); s2 = d.Place("Sofa", x + gap, z, 270); }
            if (s1 == null && s2 == null)
            {
                // no room for the pair: roll the rug back
                d.RollbackTo(start);
                return false;
            }
            d.Place("CoffeeTable", x, z, alongX ? 0 : 90, false, null, 0.25f);
            if (armchairs)
            {
                // a matched pair closing the ends, or none
                int before = d.L.Furniture.Count; Furniture a1, a2;
                if (alongX) { a1 = d.Place("Armchair", x - 2.2f, z, 90); a2 = d.Place("Armchair", x + 2.2f, z, 270); }
                else { a1 = d.Place("Armchair", x, z - 2.2f, 0); a2 = d.Place("Armchair", x, z + 2.2f, 180); }
                if (a1 == null || a2 == null) d.RollbackTo(before);
            }
            if (sideTables)
            {
                // a matched pair at the arms of one sofa (a lamp on each)
                var s = s1 ?? s2; float sz = s == s1 ? -gap : gap; int before = d.L.Furniture.Count; Furniture t1, t2;
                if (alongX) { t1 = d.Place("SideTable", x - 1.42f, z + sz, 0, false, null, 0.02f); t2 = d.Place("SideTable", x + 1.42f, z + sz, 0, false, null, 0.02f); }
                else { t1 = d.Place("SideTable", x + sz, z - 1.42f, 0, false, null, 0.02f); t2 = d.Place("SideTable", x + sz, z + 1.42f, 0, false, null, 0.02f); }
                if (t1 == null || t2 == null) d.RollbackTo(before);
            }
            // the group must leave a clear lane between the doors (or it moves elsewhere)
            if (lanes && !d.LanesOk()) { d.RollbackTo(start); return false; }
            return true;
        }

        static void Dress(Dresser d, Room room, Rng rng)
        {
            var R = d.R; float cx = R.CX, cz = R.CZ;
            bool wide = R.W >= R.D;
            switch (room.Type)
            {
                case RoomType.GrandHall:
                    if (room.Floor != 0) { d.Spot("view", R.x0 + 1.5f, R.CZ, 90); d.Spot("view", R.x1 - 1.5f, R.CZ, 270); break; }
                    d.Place("Rug", cx, cz, 0);
                    d.Place("Statue", R.x0 + 1.4f, R.z0 + 1.4f, 45); d.Place("Statue", R.x1 - 1.4f, R.z0 + 1.4f, -45);
                    d.Wall("Aquarium", wide ? 2 : 0, 0.5f); d.Wall("Clock", wide ? 3 : 1, 0.3f);
                    d.Wall("Bench", 2, 0.2f); d.Wall("Bench", 3, 0.8f); d.Wall("Bench", 2, 0.8f); d.Wall("Bench", 3, 0.2f);
                    foreach (var t in new[] { 0.22f, 0.78f }) { d.Wall("Console", wide ? 0 : 2, t); d.Wall("Console", wide ? 1 : 3, t); }
                    d.Place("Plant", R.x0 + 0.8f, R.z1 - 0.8f, 0); d.Place("Plant", R.x1 - 0.8f, R.z1 - 0.8f, 0);
                    for (int i = 0; i < 6; i++) d.Spot("gather", cx + (i - 2.5f) * 1.4f, cz + (i % 2 == 0 ? -1.2f : 1.2f), 0);
                    d.Item("Candlestick", R.x0 + 1.6f, R.z1 - 1.6f);
                    break;
                case RoomType.Landing:
                    d.Line("Bench", 1, 3.5f, 2);
                    break;
                case RoomType.Corridor:
                    {
                        float len = Math.Max(R.W, R.D);
                        int n = (int)(len / 9);
                        for (int i = 0; i < n; i++)
                        {
                            float t = (i + 0.5f) / Math.Max(1, n);
                            string what = rng.Pick(new[] { "Plant", "Bench", "Candelabra", "Statue", "Pedestal" });
                            if (wide) d.Wall(what, rng.Chance(0.5) ? 0 : 1, t); else d.Wall(what, rng.Chance(0.5) ? 2 : 3, t);
                        }
                        if (rng.Chance(0.25)) d.Item(rng.Pick(new[] { "Vase", "Candlestick", "Book" }), cx, cz);
                        break;
                    }
                case RoomType.Dining:
                    {
                        int tables = R.Area > 120 ? 2 : 1;
                        // each table tries its place and a few nudges across the room until the doors keep a lane between them;
                        // the last resort is the old place, lanes or not (a dining room always has its table)
                        void Setting(float x0, float z0, float y0)
                        {
                            float s = y0 == 0 ? 1 : 0;
                            foreach (var o in new[] { 0f, -0.45f, 0.45f, -0.9f, 0.9f })
                                if (TableWithChairs(d, "LongTable", x0 + (1 - s) * o, z0 + s * o, y0, 5, null, true, true, true)) return;
                            TableWithChairs(d, "LongTable", x0, z0, y0, 5, null, true, true);
                        }
                        if (tables == 1) Setting(cx, cz, wide ? 0 : 90);
                        else if (wide) { Setting(cx, cz - R.D * 0.22f, 0); Setting(cx, cz + R.D * 0.22f, 0); }
                        else { Setting(cx - R.W * 0.22f, cz, 90); Setting(cx + R.W * 0.22f, cz, 90); }
                        var sb = d.Wall("Sideboard", wide ? 1 : 3, 0.5f); d.ItemOn(sb, "WineGlass"); d.ItemOn(sb, "Bottle"); d.ItemOn(sb, "Plate");
                        d.Wall("Cabinet", wide ? 0 : 2, 0.5f);
                        d.Wall("Trolley", wide ? 0 : 2, 0.15f);
                        d.Wall("Clock", wide ? 0 : 2, 0.85f);
                        d.Item("Candlestick", cx + 0.6f, cz); d.Item("Cup", cx - 0.8f, cz);
                        break;
                    }
                case RoomType.Kitchen:
                    {
                        int s1 = wide ? 1 : 3, s2 = wide ? 0 : 2;
                        // one fitted working line (counter, range, counter, sink) and a fridge + counter run opposite
                        var line = d.Run(s1, new[] { "Counter", "Stove", "Counter", "Sink" }, 0.5f, 0.03f, 2);
                        var c1 = line.FirstOrDefault(f => f.Type == "Counter");
                        if (!line.Any(f => f.Type == "Stove") && d.Wall("Stove", s1, 0.6f) == null) d.Ensure("Stove", s1);
                        var sink = line.FirstOrDefault(f => f.Type == "Sink") ?? d.Wall("Sink", s1, 0.95f) ?? d.Ensure("Sink", s1);
                        var line2 = d.Run(s2, new[] { "Fridge", "Counter" }, 0.5f, 0.03f, 1);
                        var c2 = line2.FirstOrDefault(f => f.Type == "Counter");
                        var isl = d.Center("Island", wide ? 0 : 90);
                        var rack = d.Wall("KnifeRack", s2, 0.05f);
                        d.ItemOn(c1, "KitchenKnife"); d.ItemOn(c1, "KitchenKnife"); d.ItemOn(c2, "Cleaver"); d.ItemOn(isl ?? c2, "RollingPin"); d.ItemOn(c2, "FryingPan");
                        d.ItemOn(isl ?? c1, "Bread"); d.ItemOn(c1, "Tea"); d.ItemOn(sink, "Towel"); d.ItemOn(c2, "Plate");
                        break;
                    }
                case RoomType.Lounge:
                case RoomType.Parlor:
                    {
                        bool big = room.Type == RoomType.Lounge;
                        // the hearth wall first, then the conversation group facing it
                        if (big) { d.Wall("Fireplace", wide ? 1 : 3, 0.5f); d.Wall("Bookshelf", wide ? 0 : 2, 0.2f); d.Wall("Piano_Upright", wide ? 0 : 2, 0.85f); d.Item("FirePoker", cx + 2.6f, R.z1 - 0.6f); }
                        // centred if the lanes allow, else slid along the long axis; as a last resort without the lane rule
                        bool grp = false;
                        foreach (var (ox, oz) in new[] { (0f, 0f), (wide ? -R.W * 0.14f : 0f, wide ? 0f : -R.D * 0.14f), (wide ? R.W * 0.14f : 0f, wide ? 0f : R.D * 0.14f) })
                        { if (grp) break; grp = Conversation(d, cx + ox, cz + oz, wide, R.W * R.D > 34f) || Conversation(d, cx + ox, cz + oz, !wide, false, true, 1.3f); }
                        if (!grp && !Conversation(d, cx, cz, wide, R.W * R.D > 34f, true, 1.5f, false)) Conversation(d, cx, cz, !wide, false, true, 1.3f, false);
                        d.Place("Plant", R.x0 + 0.6f, R.z0 + 0.6f, 0); d.Place("Mirror", R.x1 - 0.3f, R.CZ, 270);
                        d.Item(rng.Pick(new[] { "Book", "Cup", "Vase", "PaperModel" }), cx + 0.2f, cz);
                        if (!big && rng.Chance(0.5)) d.Place("RoundTable", R.x0 + 1.4f, R.z1 - 1.4f, 0);
                        break;
                    }
                case RoomType.Library:
                    {
                        d.Line("Bookshelf", wide ? 1 : 3, 0.1f); d.Line("Bookshelf", wide ? 3 : 1, 0.2f, 2);
                        if (R.Area > 90) { d.Place("Bookshelf", cx - 2.5f, cz + 1.8f, 0); d.Place("Bookshelf", cx + 2.5f, cz + 1.8f, 0); }
                        d.Rug(cx, cz - 1.5f, 6.6f, 3.2f);
                        var t1 = d.Place("ReadingTable", cx - 2f, cz - 1.5f, 0); var t2 = d.Place("ReadingTable", cx + 2f, cz - 1.5f, 0);
                        foreach (var t in new[] { t1, t2 }) if (t != null) { d.Place("Chair", t.Pos.x - 0.45f, t.Pos.z - 0.85f, 0, false, null, 0.02f); d.Place("Chair", t.Pos.x + 0.45f, t.Pos.z - 0.85f, 0, false, null, 0.02f); }
                        d.Wall("Fireplace", wide ? 0 : 2, 0.5f); d.Place("Armchair", R.x0 + 1.2f, R.z0 + 1.2f, 45);
                        d.ItemOn(t1, "Book"); d.ItemOn(t2, "Book"); d.ItemOn(t1, "Notebook");
                        break;
                    }
                case RoomType.Archive:
                    {
                        d.Line("FileCabinet", wide ? 1 : 3, 0.1f); d.Line("Shelves", wide ? 0 : 2, 0.4f, 3);
                        var t = d.Center("ReadingTable", 0); d.ItemOn(t, "Document"); d.ItemOn(t, "Document"); d.ItemOn(t, "Envelope");
                        d.Item("Camera", cx + 1, cz + 0.5f); d.Item("Recorder", cx - 1, cz + 0.5f);
                        break;
                    }
                case RoomType.MusicRoom:
                    {
                        d.Rug(cx - R.W * 0.2f, cz, wide ? 3.4f : 3.0f, wide ? 3.0f : 3.4f);
                        d.Must(() => d.Place("Piano", cx - R.W * 0.2f, cz, wide ? 90 : 0)); d.Place("Drums", cx + R.W * 0.22f, cz + R.D * 0.15f, 180);
                        d.Place("MusicStand", cx + 0.5f, cz - 1.2f, 180); d.Place("MusicStand", cx + 1.6f, cz - 1.2f, 180);
                        d.Wall("Sofa", wide ? 0 : 2, 0.5f); d.Wall("Speaker", wide ? 1 : 3, 0.1f); d.Wall("Speaker", wide ? 1 : 3, 0.9f);
                        d.Item("Recorder", cx + 1f, cz - 1.6f); d.Item("HandWarmer", cx - 1, cz - 1.6f);
                        break;
                    }
                case RoomType.Theater:
                    {
                        int stageSide = wide ? 2 : 0;
                        d.Must(() => d.Wall("Stage", stageSide, 0.5f));
                        int rows = (int)((wide ? R.W : R.D) / 2.2f) - 3;
                        for (int i = 0; i < Math.Max(2, rows); i++)
                        {
                            float off = 4.2f + i * 1.6f;
                            if (i < 2) d.Must(() => wide ? d.Place("Seats", R.x0 + off, cz, 270) : d.Place("Seats", cx, R.z0 + off, 180)); else if (wide) d.Place("Seats", R.x0 + off, cz, 270); else d.Place("Seats", cx, R.z0 + off, 180);
                        }
                        d.Wall("CostumeRack", wide ? 1 : 3, 0.05f); d.Wall("Mirror", wide ? 0 : 2, 0.95f);
                        d.Item("TheaterMask", R.x0 + 1.5f, R.z1 - 1.0f); d.Item("Cloak", R.x0 + 2.2f, R.z1 - 1.0f);
                        d.Item("Candlestick", cx, cz);
                        break;
                    }
                case RoomType.Greenhouse:
                    {
                        int rows = Math.Max(2, (int)((wide ? R.D : R.W) / 3.2f));
                        for (int i = 0; i < rows; i++)
                        {
                            float t = (i + 0.5f) / rows;
                            if (wide) { d.Place("Planter", R.x0 + R.W * 0.3f, R.z0 + t * R.D, 0); d.Place("Planter", R.x0 + R.W * 0.7f, R.z0 + t * R.D, 180); }
                            else { d.Place("Planter", R.x0 + t * R.W, R.z0 + R.D * 0.3f, 90); d.Place("Planter", R.x0 + t * R.W, R.z0 + R.D * 0.7f, 270); }
                        }
                        d.Wall("Bench", wide ? 1 : 3, 0.5f); d.Place("Plant", R.x0 + 0.7f, R.z0 + 0.7f, 0); d.Place("Plant", R.x1 - 0.7f, R.z1 - 0.7f, 0);
                        d.Item("Flower", cx, cz); d.Item("Bucket", R.x1 - 1, R.z0 + 1); d.Item("Scissors", cx + 1, cz + 0.4f); d.Item("PoisonVial", R.x0 + 1.2f, R.z1 - 1.0f);
                        break;
                    }
                case RoomType.Infirmary:
                    {
                        d.Line("InfirmaryBed", wide ? 1 : 3, 0.6f, 3); var cab = d.Wall("MedCabinet", wide ? 0 : 2, 0.2f); d.Wall("Desk", wide ? 0 : 2, 0.8f);
                        d.ItemOn(cab, "FirstAidKit"); d.ItemOn(cab, "FirstAidKit"); d.ItemOn(cab, "Sedative"); d.ItemOn(cab, "Towel");
                        break;
                    }
                case RoomType.Laundry:
                    {
                        d.Line("Washer", wide ? 1 : 3, 0.1f, 4); d.Place("DryRack", cx, cz, wide ? 0 : 90); d.Wall("Shelves", wide ? 0 : 2, 0.2f);
                        d.Item("Sheet", cx + 1, cz); d.Item("Bleach", R.x0 + 1, R.z0 + 1); d.Item("Towel", cx - 1, cz); d.Item("SpareApron", cx, cz + 1);
                        break;
                    }
                case RoomType.Workshop:
                    {
                        var wb = d.Wall("Workbench", wide ? 1 : 3, 0.3f); var wb2 = d.Wall("Workbench", wide ? 0 : 2, 0.6f); d.Wall("ToolWall", wide ? 1 : 3, 0.85f);
                        d.Place("Crates", R.x1 - 1, R.z1 - 1, 0); d.Place("Easel", cx, cz, 0);
                        d.ItemOn(wb, "Hammer"); d.ItemOn(wb, "Wrench"); d.ItemOn(wb2, "Chisel"); d.ItemOn(wb2, "Rope"); d.ItemOn(wb, "WindupToy"); d.ItemOn(wb2, "Tripwire");
                        break;
                    }
                case RoomType.Storage:
                case RoomType.Closet:
                    {
                        d.Line("Shelves", wide ? 1 : 3, 0.3f, 3); d.Place("Crates", R.x0 + 1, R.z0 + 1, 0); if (R.Area > 30) d.Place("Crates", R.x1 - 1, R.z0 + 1, 0);
                        if (room.Type == RoomType.Storage) { d.Item("Rope", cx, cz); d.Item("Bucket", cx + 1, cz); d.Item("Mop", cx - 1, cz); d.Item("PipeSection", R.x0 + 1.5f, R.z1 - 1); d.Item("Flashlight", cx, cz + 1); d.Place("Cart", cx, cz - 1.2f, 0); d.Item("Sheet", cx + 0.5f, cz + 0.5f); }
                        else if (rng.Chance(0.5)) d.Item(rng.Pick(new[] { "Towel", "Sheet", "Bucket", "Candlestick" }), cx, cz);
                        break;
                    }
                case RoomType.GameRoom:
                    {
                        d.Must(() => d.Center("PoolTable", wide ? 0 : 90)); d.Wall("Arcade", wide ? 1 : 3, 0.15f); d.Wall("Arcade", wide ? 1 : 3, 0.35f);
                        d.Place("GameTable", R.x0 + 1.6f, R.z0 + 1.4f, 0); d.Wall("Sofa", wide ? 0 : 2, 0.7f);
                        d.Item("Trophy", R.x1 - 0.8f, R.z1 - 0.8f); d.Item("Snack", R.x0 + 1.6f, R.z0 + 1.4f); d.Item("Soda", R.x0 + 1.8f, R.z0 + 1.2f);
                        break;
                    }
                case RoomType.Pool:
                    {
                        float pw = Math.Max(4, R.W - 5f), pd = Math.Max(3, R.D - 5f);
                        Furniture f = null;
                        for (float sc = 1f; sc >= 0.45f && f == null; sc -= 0.1f)
                        {
                            // the basin shrinks until it fits the room it was given (a pool room always has water)
                            var def = FurnitureCatalog.Get("PoolWater"); float ow = def.W, od = def.D; def.W = Math.Max(2.5f, pw * sc); def.D = Math.Max(2f, pd * sc);
                            f = d.Place("PoolWater", cx, cz, 0); def.W = ow; def.D = od;
                            if (f != null) { f.W = Math.Max(2.5f, pw * sc); f.D = Math.Max(2f, pd * sc); pw = f.W; pd = f.D; }
                        }
                        if (f == null) { f = d.Place("PoolWater", cx, cz, 0, true); if (f != null) { f.W = Math.Max(2.5f, R.W - 3f); f.D = Math.Max(2f, R.D - 3f); pw = f.W; pd = f.D; } }
                        for (int i = 0; i < 3; i++) d.Wall("Lounger", wide ? 0 : 2, 0.2f + i * 0.3f);
                        d.Spot("swim", cx, cz, 0); d.Spot("swim", cx + pw * 0.3f, cz, 90);
                        d.Item("Towel", R.x0 + 1, R.z1 - 0.8f);
                        break;
                    }
                case RoomType.WaterRoom:
                    d.Wall("PumpUnit", wide ? 1 : 3, 0.5f); d.Wall("Terminal", wide ? 0 : 2, 0.5f); d.Wall("Pipes", wide ? 1 : 3, 0.05f); d.Item("Bleach", cx, cz);
                    break;
                case RoomType.PowerRoom:
                    d.Ensure("Switchboard", wide ? 1 : 3); d.Ensure("Generator", wide ? 0 : 2); d.Item("Fuse", cx, cz); d.Item("Flashlight", cx + 0.5f, cz + 0.5f);
                    break;
                case RoomType.MachineRoom:
                    {
                        var press = d.Place("Press", R.x0 + R.W * 0.62f, R.z0 + R.D * 0.55f, 0, false, null, 0.9f) ?? d.Must(() => d.Center("Press", 0));
                        var con = d.Wall("PressConsole", wide ? 0 : 2, 0.12f) ?? d.Wall("PressConsole", wide ? 1 : 3, 0.1f) ?? d.Ensure("PressConsole");
                        d.Wall("Pipes", wide ? 1 : 3, 0.2f); d.Place("Crates", R.x0 + 1, R.z1 - 1, 0);
                        d.Item("Wrench", R.x0 + 1.2f, R.z0 + 1.2f); d.Item("PipeSection", R.x1 - 1.2f, R.z0 + 1.2f);
                        if (press != null) d.Spot("pressbed", press.Pos.x, press.Pos.z + 0.1f, 0);
                        break;
                    }
                case RoomType.Gallery:
                    {
                        d.Line("FrameWall", wide ? 1 : 3, 0.8f); int n = Math.Max(2, (int)(Math.Max(R.W, R.D) / 3.4f));
                        for (int i = 0; i < n; i++) { float t = (i + 0.5f) / n; var p = wide ? d.Place("Pedestal", R.x0 + t * R.W, cz, 0) : d.Place("Pedestal", cx, R.z0 + t * R.D, 0); d.ItemOn(p, rng.Pick(new[] { "Vase", "Trophy", "Vase" })); }
                        var wb = d.Wall("Workbench", wide ? 0 : 2, 0.2f); d.Wall("Easel", wide ? 0 : 2, 0.7f); d.ItemOn(wb, "PaletteKnife"); d.ItemOn(wb, "Chisel");
                        break;
                    }
                case RoomType.Wardrobe:
                    {
                        d.Line("CostumeRack", wide ? 1 : 3, 0.4f, 3); d.Wall("VanityDesk", wide ? 0 : 2, 0.3f); d.Wall("Mirror", wide ? 0 : 2, 0.8f);
                        d.Place("DressForm", cx, cz, 180); d.Place("DressForm", cx + 1.2f, cz, 180);
                        d.Item("Cloak", cx - 1, cz + 1); d.Item("TheaterMask", cx + 1, cz + 1); d.Item("Thread", cx + 0.4f, cz - 1.2f); d.Item("Raincoat", cx, cz + 1.2f); d.Item("Scarf", cx - 0.5f, cz - 0.8f); d.Item("Scissors", cx + 0.6f, cz - 0.8f);
                        break;
                    }
                case RoomType.Chapel:
                    {
                        int altarSide = wide ? 2 : 0; d.Must(() => d.Wall("Altar", altarSide, 0.5f));
                        int rows = Math.Max(2, (int)((wide ? R.W : R.D) / 2.0f) - 2);
                        int pews = 0;
                        for (int i = 0; i < rows; i++) { float off = 3.2f + i * 1.5f; if (wide) { if (d.Place("Pew", R.x0 + off, cz - 1.8f, 270) != null) pews++; if (d.Place("Pew", R.x0 + off, cz + 1.8f, 270) != null) pews++; } else { if (d.Place("Pew", cx - 1.8f, R.z0 + off, 180) != null) pews++; if (d.Place("Pew", cx + 1.8f, R.z0 + off, 180) != null) pews++; } }
                        // narrow chapels: a single central file of pews instead of two
                        if (pews < 2) for (int i = 0; i < rows; i++) { float off = 2.8f + i * 1.4f; if (wide) d.Place("Pew", R.x0 + off, cz, 270, false, null, 0.15f); else d.Place("Pew", cx, R.z0 + off, 180, false, null, 0.15f); }
                        // a chapel always keeps at least one pew, lanes or not
                        if (!d.Room.Furniture.Any(id => d.L.Furniture[id].Type == "Pew")) d.Must(() => wide ? d.Place("Pew", R.x0 + 2.8f, cz, 270, false, null, 0.15f) : d.Place("Pew", cx, R.z0 + 2.8f, 180, false, null, 0.15f));
                        d.Place("Candelabra", R.x0 + 0.8f, R.z0 + 0.8f, 0); d.Place("Candelabra", R.x1 - 0.8f, R.z0 + 0.8f, 0);
                        d.Item("Candlestick", cx, R.z0 + 1.5f); d.Item("Flower", cx + 0.5f, R.z0 + 1.5f);
                        break;
                    }
                case RoomType.Bedroom:
                    {
                        var c = Cast.Get(room.Owner);
                        int bedSide = rng.R(4);
                        // the bed centred on its wall (headboard flush), nightstands both sides
                        var bed = d.Wall("Bed", bedSide, 0.5f) ?? d.Wall("Bed", (bedSide + 1) % 4, 0.5f) ?? d.Wall("Bed", bedSide, 0.3f) ?? d.Wall("Bed", (bedSide + 1) % 4, 0.3f) ?? d.Wall("Bed", (bedSide + 2) % 4, 0.5f) ?? d.Must(() => d.Wall("Bed", bedSide, 0.5f) ?? d.Wall("Bed", (bedSide + 1) % 4, 0.5f));
                        if (bed != null) Bedside(d, bed, rng, true);
                        var desk = d.Wall("Desk", (bedSide + 2) % 4, 0.5f) ?? d.Wall("Desk", (bedSide + 2) % 4, 0.75f) ?? d.Wall("Desk", (bedSide + 1) % 4, 0.7f);
                        if (desk != null) { double yr = desk.Yaw * Math.PI / 180; d.Place("Chair", desk.Pos.x + (float)Math.Sin(yr) * 0.58f, desk.Pos.z + (float)Math.Cos(yr) * 0.58f, desk.Yaw + 180, false, null, 0.02f); }   // tucked in, unless something stands there
                        d.Wall("Wardrobe", (bedSide + 3) % 4, 0.8f);
                        d.Item("RoomKey", R.CX, R.CZ, room.Owner, (c?.Given ?? "") + "의 방 열쇠");
                        foreach (var it in PersonalItems(room.Owner)) d.ItemOn(desk ?? bed, it, room.Owner);
                        OwnerFurniture(d, room, bedSide);   // owner dressing (env-art): after the kernel items, never moving them
                        break;
                    }
                case RoomType.ButlerRoom:
                    d.Wall("ButlerDesk", 0, 0.5f); d.Wall("Aquarium", 1, 0.5f); d.Wall("Shelves", 2, 0.5f);
                    break;
                case RoomType.Courtroom:
                    {
                        // 18 stands in a ring + butler's high seat
                        float rad = 6.2f;
                        for (int i = 0; i < 18; i++)
                        {
                            double a = i / 18.0 * Math.PI * 2; float x = cx + (float)Math.Sin(a) * rad, z = cz + (float)Math.Cos(a) * rad;
                            float yaw = (float)(a * 180 / Math.PI) + 180f;
                            var st = d.Place("TrialStand", x, z, yaw, true); if (st != null) st.Variant = i;
                        }
                        d.Place("ButlerDesk", cx, R.z1 - 1.2f, 180, true);
                        break;
                    }
                case RoomType.RainCorridor:
                    {
                        int n = Math.Max(3, (int)(Math.Max(R.W, R.D) / 2.5f));
                        for (int i = 0; i < n; i++) { float t = (i + 0.5f) / n; if (wide) { d.Place("RainFrame", R.x0 + t * R.W, R.z0 + 1.0f, 0); d.Place("RainFrame", R.x0 + t * R.W, R.z1 - 1.0f, 0); } else { d.Place("RainFrame", R.x0 + 1.0f, R.z0 + t * R.D, 0); d.Place("RainFrame", R.x1 - 1.0f, R.z0 + t * R.D, 0); } }
                        d.Center("Bench", wide ? 0 : 90); d.Item("Raincoat", cx + 0.8f, cz); if (room.MysteryGimmick == 1) d.Item("Recorder", cx - 0.8f, cz);
                        break;
                    }
                case RoomType.EmptyAuditorium:
                    {
                        int rows = Math.Max(2, (int)((wide ? R.W : R.D) / 1.4f) - 2), per = Math.Max(3, (int)((wide ? R.D : R.W) / 0.9f) - 2);
                        for (int i = 0; i < rows; i++) for (int j = 0; j < per; j++)
                            {
                                if (rng.Chance(0.12)) continue; // missing seats
                                float a = 2.2f + i * 1.3f, b = (j + 0.5f) / per;
                                if (wide) d.Place("AudSeat", R.x0 + a, R.z0 + 0.6f + b * (R.D - 1.2f), 270, false, null, 0.2f); else d.Place("AudSeat", R.x0 + 0.6f + b * (R.W - 1.2f), R.z0 + a, 180, false, null, 0.2f);
                            }
                        d.Wall("Speaker", wide ? 2 : 0, 0.5f); d.Item("Recorder", cx, cz);
                        break;
                    }
                case RoomType.WaitingRoom:
                    {
                        d.Wall("TicketBooth", wide ? 1 : 3, 0.5f); d.Wall("Clock", wide ? 1 : 3, 0.1f);
                        int n = Math.Max(2, (int)((wide ? R.W : R.D) / 3.5f));
                        for (int i = 0; i < n; i++) { float t = (i + 0.5f) / n; if (wide) d.Place("WaitBench", R.x0 + t * R.W, cz, 0); else d.Place("WaitBench", cx, R.z0 + t * R.D, 90); }
                        d.Item("Invitation", cx, cz + 0.5f); d.Item("Document", cx + 1, cz - 0.5f);
                        break;
                    }
                case RoomType.WhiteDoors:
                    {
                        int n = 4 + room.MysteryGimmick;
                        for (int i = 0; i < n; i++) d.Place("DoorFrameFree", R.x0 + 1.2f + rng.F() * (R.W - 2.4f), R.z0 + 1.2f + rng.F() * (R.D - 2.4f), rng.Pick(new[] { 0f, 90f, 45f, 180f }));
                        d.Item("Mirror".Length > 0 ? "Candlestick" : "Vase", cx, cz);
                        break;
                    }
                case RoomType.MirrorWater:
                    {
                        var w = d.Place("ShallowWater", cx, cz, 0, true); if (w != null) { w.W = R.W - 1.5f; w.D = R.D - 1.5f; w.Blocks = false; }
                        d.Wall("Mirror", 1, 0.3f); d.Wall("Mirror", 1, 0.7f); d.Wall("Mirror", 0, 0.5f);
                        d.Item("Flower", cx, cz); d.Spot("stand", cx, cz, 0);
                        break;
                    }
                case RoomType.ClockMuseum:
                    {
                        d.Line("ClockCase", wide ? 1 : 3, 0.8f); d.Wall("Clock", wide ? 0 : 2, 0.2f); d.Wall("Clock", wide ? 0 : 2, 0.5f); d.Wall("Clock", wide ? 0 : 2, 0.8f);
                        d.Center("Pedestal", 0); d.Item("WindupToy", cx, cz);
                        break;
                    }
                case RoomType.Study:
                    {
                        d.Rug(cx, cz, wide ? 3.6f : 2.8f, wide ? 2.8f : 3.6f);
                        // the writing desk in the middle, its chair tucked in on the drawer (working) side
                        var desk = d.Must(() => d.Center("Desk", wide ? 0 : 90, 0, 0));
                        if (desk != null) { double yr = desk.Yaw * Math.PI / 180; d.Place("Chair", desk.Pos.x + (float)Math.Sin(yr) * 0.58f, desk.Pos.z + (float)Math.Cos(yr) * 0.58f, desk.Yaw + 180, true); }
                        d.Line("Bookshelf", wide ? 1 : 3, 0.2f, 2); d.Wall("Armchair", wide ? 0 : 2, 0.2f);
                        d.ItemOn(desk, "Document"); d.ItemOn(desk, "Book"); if (rng.Chance(0.5)) d.ItemOn(desk, "Envelope");
                        break;
                    }
                case RoomType.TeaRoom:
                    {
                        // two tea tables with their chairs on one rug; the pair slides across the room until the doors keep a lane
                        bool TeaTables(float oz, bool lanes)
                        {
                            int start = d.L.Furniture.Count;
                            d.Rug(cx, cz + oz, 6.2f, 3.4f);
                            foreach (float tx in new[] { cx - 1.65f, cx + 1.65f })
                            {
                                if ((d.Place("RoundTable", tx, cz + oz, 0, false, null, 0.75f) ?? d.Place("RoundTable", tx, cz + oz, 0)) == null) continue;
                                d.Place("Chair", tx, cz + oz - 0.95f, 0, true); d.Place("Chair", tx, cz + oz + 0.95f, 180, true);
                                d.Place("Chair", tx - 0.95f, cz + oz, 90, false, null, 0.02f); d.Place("Chair", tx + 0.95f, cz + oz, 270, false, null, 0.02f);
                            }
                            if (!d.L.Furniture.Skip(start).Any(f => f.Type == "RoundTable") || (lanes && !d.LanesOk())) { d.RollbackTo(start); return false; }
                            return true;
                        }
                        if (!TeaTables(0, true) && !TeaTables(-0.7f, true) && !TeaTables(0.7f, true)) TeaTables(0, false);
                        var sb = d.Wall("Sideboard", wide ? 1 : 3, 0.5f); d.Wall("TeaCart", wide ? 0 : 2, 0.2f); d.Wall("Cabinet", wide ? 0 : 2, 0.62f);
                        d.ItemOn(sb, "Cup"); d.ItemOn(sb, "Tea"); d.ItemOn(sb, "Cup"); d.Item("Chocolate", cx + 1.4f, cz);
                        break;
                    }
                case RoomType.Courtyard:
                    {
                        d.Must(() => d.Center("Fountain", 0)); d.Wall("Bench", 0, 0.5f); d.Wall("Bench", 1, 0.5f);
                        d.Place("Planter", R.x0 + 1.5f, R.z0 + 1.2f, 0); d.Place("Planter", R.x1 - 1.5f, R.z1 - 1.2f, 180); d.Place("Plant", R.x0 + 0.8f, R.z1 - 0.8f, 0);
                        d.Item("Flower", cx + 1.6f, cz); d.Spot("view", cx, cz - 2.2f, 0);
                        break;
                    }
                case RoomType.DollRoom:
                    {
                        d.Line("DollShelf", wide ? 1 : 3, 0.2f, 3); if (d.Center("Dollhouse", wide ? 0 : 90) == null && d.Center("Dollhouse", wide ? 0 : 90, wide ? R.W * 0.18f : 0, wide ? 0 : R.D * 0.18f) == null) d.Must(() => d.Center("Dollhouse", wide ? 0 : 90)); d.Wall("Armchair", wide ? 0 : 2, 0.3f); d.Wall("Mirror", wide ? 0 : 2, 0.8f);
                        d.Item("PaperModel", cx + 1, cz - 1); d.Item("Scissors", cx - 1, cz - 1);
                        break;
                    }
                case RoomType.TrophyRoom:
                    {
                        d.Wall("StuffedBeast", wide ? 1 : 3, 0.3f); d.Wall("StuffedBeast", wide ? 1 : 3, 0.8f);
                        int n = Math.Max(2, (int)(Math.Max(R.W, R.D) / 3f)); for (int i = 0; i < n; i++) { float t = (i + 0.5f) / n; var p = wide ? d.Place("Pedestal", R.x0 + t * R.W, cz - 0.6f, 0) : d.Place("Pedestal", cx - 0.6f, R.z0 + t * R.D, 0); d.ItemOn(p, "Trophy"); }
                        d.Wall("FrameWall", wide ? 0 : 2, 0.5f);
                        break;
                    }
                case RoomType.WineCellar:
                    {
                        d.Line("WineRack", wide ? 1 : 3, 0.3f, 3); d.Place("Barrel", R.x0 + 1, R.z0 + 1, 0); d.Place("Barrel", R.x0 + 2, R.z0 + 1, 0); d.Place("Barrel", R.x1 - 1, R.z0 + 1, 0);
                        d.Item("Bottle", cx, cz); d.Item("Bottle", cx + 0.5f, cz + 0.3f); d.Item("Beer", cx - 0.5f, cz);
                        break;
                    }
                case RoomType.BoilerRoom:
                    d.Ensure("Boiler", wide ? 1 : 3); d.Wall("Pipes", wide ? 0 : 2, 0.3f); d.Place("Crates", R.x0 + 1, R.z1 - 1, 0); d.Item("PipeSection", cx, cz); d.Item("Wrench", cx + 0.6f, cz);
                    break;
                // rooms that help a crime (BL23 murder content, Sim/Systems/Methods*.cs): the furnace, the cold racks and the darkroom trays are logic anchors
                case RoomType.Incinerator:
                    d.Ensure("Incinerator", wide ? 1 : 3); d.Wall("Crates", wide ? 0 : 2, 0.2f); d.Wall("Shelves", wide ? 0 : 2, 0.8f);
                    d.Item("Bucket", cx + 0.8f, cz);
                    break;
                case RoomType.ColdStorage:
                    d.Ensure("ColdLocker", wide ? 1 : 3); d.Wall("ColdLocker", wide ? 0 : 2, 0.5f); d.Place("Crates", R.x0 + 1, R.z0 + 1, 0);
                    break;
                case RoomType.Darkroom:
                    {
                        var dt = d.Ensure("DevelopTable", wide ? 1 : 3); d.Wall("Shelves", wide ? 0 : 2, 0.5f);
                        d.ItemOn(dt, "DevChemical"); d.Item("Camera", cx, cz);
                        break;
                    }
                case RoomType.GuestRoom:
                    {
                        var gb = d.Wall("Bed", rng.R(4), 0.4f); if (gb != null) Bedside(d, gb, rng, false); d.Wall("Wardrobe", rng.R(4), 0.8f);
                        if (rng.Chance(0.4)) d.Item(rng.Pick(new[] { "Sheet", "Towel", "Book", "Candlestick" }), cx, cz);
                        break;
                    }
                case RoomType.Elevator:
                case RoomType.Stairwell:
                    break;
            }
            // generic standing spots so NPCs have somewhere to be in any room
            if (!RoomInfo.IsPassage(room.Type) && room.Type != RoomType.Elevator && room.Type != RoomType.Courtroom)
            {
                for (int k = 0; k < 2; k++) d.Spot("stand", R.x0 + 1f + rng.F() * Math.Max(0.1f, R.W - 2f), R.z0 + 1f + rng.F() * Math.Max(0.1f, R.D - 2f), rng.Range(0, 360));
            }
            else if (room.Type == RoomType.Corridor || room.Type == RoomType.Landing)
            {
                d.Spot("walk", R.CX, R.CZ, 0);
            }
        }

        // ------------------------------------------------------------------ activity zones for large rooms
        /// <summary>Big rooms get several distinct corners to spend time in (a bar, a card table, a gramophone nook...),
        /// each a real furniture group with activity spots, until the room is comfortably full.</summary>
        static void Zones(Dresser d, Room room, Rng rng)
        {
            var R = d.R; float area = R.W * R.D;
            if (RoomInfo.IsPassage(room.Type) || room.Type == RoomType.Courtroom || room.Type == RoomType.Elevator) { if (room.Type == RoomType.Corridor || room.Type == RoomType.Landing) Gallery(d, room, rng); return; }
            bool intimate = room.Type == RoomType.Bedroom || room.Type == RoomType.GuestRoom || room.Type == RoomType.Study || room.Type == RoomType.Parlor || room.Type == RoomType.TeaRoom;
            if (area < (intimate ? 20 : 55)) return;
            var list = (room.Type == RoomType.Bedroom ? OwnerZones(room.Owner) : null) ?? ZoneList(room.Type);
            if (list.Count == 0) return;
            // a room is "comfortably full" when about a third of its floor is furnished
            float Used() => d.SolidArea();
            int budget = area > 180 ? 11 : area > 120 ? 8 : area > 80 ? 5 : area > 45 ? 3 : 1;
            foreach (var z in list)
            {
                if (budget <= 0 || Used() > area * 0.4f) break;
                if (TryZone(d, z, rng)) budget--;
            }
        }

        // ------------------------------------------------------------------ wall lining
        sealed class WallSet { public string[] Any, Low; public float Cover; }

        static WallSet PerimeterSet(RoomType t)
        {
            WallSet S(float cover, string[] any, string[] low) => new WallSet { Any = any, Low = low, Cover = cover };
            switch (t)
            {
                case RoomType.Library: return S(0.6f, new[] { "Bookshelf", "Bookshelf", "Bookshelf", "Cabinet", "Console", "Plant", "DisplayCase" }, new[] { "Console", "Chest", "Bench", "Plant", "CardCatalog" });
                case RoomType.Study: case RoomType.Archive: return S(0.5f, new[] { "Bookshelf", "Bookshelf", "Cabinet", "Console", "FileCabinet", "Plant" }, new[] { "Console", "Chest", "Plant", "Armchair" });
                case RoomType.Lounge: case RoomType.Parlor: return S(0.42f, new[] { "Console", "Sideboard", "Cabinet", "Plant", "Armchair", "DisplayCase", "Statue", "Bookshelf" }, new[] { "Console", "Sideboard", "Bench", "Plant", "Chest", "Armchair" });
                case RoomType.MusicRoom: return S(0.4f, new[] { "Console", "Cabinet", "Plant", "Chair", "Chair", "Harp", "Statue" }, new[] { "Console", "Chair", "Chair", "Bench", "Plant" });
                case RoomType.GameRoom: return S(0.4f, new[] { "Console", "Cabinet", "Plant", "Chair", "Arcade", "DisplayCase" }, new[] { "Console", "Chair", "Bench", "Plant" });
                case RoomType.TeaRoom: return S(0.45f, new[] { "Cabinet", "Console", "Sideboard", "Plant", "Chair", "DisplayCase" }, new[] { "Console", "Sideboard", "Plant", "Chair", "Bench" });
                case RoomType.Dining: return S(0.45f, new[] { "Sideboard", "Console", "Cabinet", "Plant", "Chair", "Chair", "Statue" }, new[] { "Sideboard", "Console", "Plant", "Bench", "Chair" });
                case RoomType.Gallery: case RoomType.TrophyRoom: return S(0.45f, new[] { "Pedestal", "Statue", "DisplayCase", "Bench", "Console", "Plant", "Pedestal" }, new[] { "Bench", "Pedestal", "Console", "Pedestal" });
                case RoomType.ClockMuseum: return S(0.5f, new[] { "Clock", "Clock", "ClockCase", "Pedestal", "Bench" }, new[] { "Pedestal", "Bench", "Console" });
                case RoomType.Bedroom: case RoomType.GuestRoom: return S(0.34f, new[] { "Console", "Chest", "Bookshelf", "Plant", "Armchair", "Mirror", "Chair" }, new[] { "Console", "Chest", "Bench", "Armchair", "Plant" });
                case RoomType.Wardrobe: return S(0.5f, new[] { "CostumeRack", "Mirror", "Chest", "DressForm", "Console", "Cabinet" }, new[] { "Chest", "Bench", "Console", "DressForm" });
                case RoomType.DollRoom: return S(0.5f, new[] { "DollShelf", "DisplayCase", "Chest", "Armchair", "Console", "Plant" }, new[] { "Chest", "Bench", "Console", "Armchair" });
                case RoomType.Kitchen: return S(0.5f, new[] { "Shelves", "Counter", "Crates", "Barrel", "Sideboard" }, new[] { "Counter", "Crates", "Barrel", "Sideboard" });
                case RoomType.Infirmary: return S(0.45f, new[] { "MedCabinet", "Chair", "Plant", "Console", "Shelves" }, new[] { "Chair", "Bench", "Console" });
                case RoomType.Workshop: return S(0.5f, new[] { "Shelves", "Workbench", "Crates", "ToolWall", "Chest", "Barrel" }, new[] { "Crates", "Workbench", "Chest", "Barrel" });
                case RoomType.Storage: case RoomType.Closet: case RoomType.Laundry: case RoomType.BoilerRoom: return S(0.5f, new[] { "Shelves", "Shelves", "Crates", "Barrel", "Chest" }, new[] { "Crates", "Barrel", "Chest" });
                case RoomType.WineCellar: return S(0.55f, new[] { "WineRack", "WineRack", "Barrel", "Crates" }, new[] { "Barrel", "Crates" });
                case RoomType.ButlerRoom: return S(0.5f, new[] { "Shelves", "Cabinet", "Console", "Chest", "FileCabinet" }, new[] { "Console", "Chest" });
                case RoomType.Chapel: return S(0.35f, new[] { "Candelabra", "Statue", "Bench", "Plant", "Candelabra" }, new[] { "Candelabra", "Bench", "Plant" });
                case RoomType.Theater: return S(0.3f, new[] { "Chair", "Statue", "Plant", "CostumeRack", "Console" }, new[] { "Chair", "Bench", "Plant" });
                case RoomType.Pool: return S(0.3f, new[] { "Lounger", "Plant", "Bench", "Plant" }, new[] { "Lounger", "Plant", "Bench" });
                case RoomType.Greenhouse: return S(0.5f, new[] { "Planter", "Plant", "Bench", "Plant" }, new[] { "Planter", "Plant", "Bench" });
                case RoomType.Courtyard: return S(0.3f, new[] { "Bench", "Planter", "Plant", "Statue" }, new[] { "Bench", "Planter", "Plant" });
                case RoomType.GrandHall: return S(0.35f, new[] { "Console", "Statue", "Plant", "Bench", "DisplayCase" }, new[] { "Console", "Bench", "Plant" });
                case RoomType.Incinerator: return S(0.45f, new[] { "Crates", "Shelves", "Barrel", "Crates" }, new[] { "Crates", "Barrel" });
                case RoomType.ColdStorage: return S(0.5f, new[] { "Shelves", "Crates", "ColdLocker" }, new[] { "Crates", "Shelves" });
                case RoomType.Darkroom: return S(0.45f, new[] { "Shelves", "Console", "Chest", "FileCabinet" }, new[] { "Console", "Chest" });
            }
            return null;
        }

        static bool RowType(string t) => t == "Bookshelf" || t == "Shelves" || t == "WineRack" || t == "FileCabinet" || t == "DollShelf" || t == "ClockCase" || t == "CostumeRack" || t == "Crates" || t == "Barrel" || t == "Planter" || t == "MedCabinet" || t == "ColdLocker" || t == "Pedestal" || t == "Clock" || t == "Lounger" || t == "Washer";
        static bool CaseType(string t) => t == "Bookshelf" || t == "Shelves" || t == "WineRack" || t == "FileCabinet" || t == "DollShelf" || t == "ClockCase" || t == "MedCabinet" || t == "ColdLocker";
        static bool FlankType(string t) => t == "Plant" || t == "Candelabra" || t == "Chair" || t == "Statue" || t == "Pedestal" || t == "Armchair" || t == "Clock" || t == "Barrel" || t == "DressForm";

        /// <summary>Line the walls after the groups and zones. Every free stretch of wall gets one deliberate arrangement: a
        /// row of cases with a constant gap, or a centred piece with a matched pair either side (plant-console-plant,
        /// chair-sideboard-chair) — never a random scatter. Low pieces under exterior windows; lanes and doors stay clear.</summary>
        static void Perimeter(Dresser d, Room room, Rng rng)
        {
            if (room.Floor < -1) return;
            var set = (room.Type == RoomType.Bedroom ? OwnerPerimeter(room.Owner) : null) ?? PerimeterSet(room.Type); if (set == null) return;
            var R = d.R;
            foreach (int side in new[] { 0, 1, 2, 3 }.OrderBy(_ => rng.F()))
            {
                bool ext = room.Floor >= 0 && (side == 0 ? R.z0 <= 0.3f : side == 1 ? R.z1 >= LayoutGenerator.D - 0.3f : side == 2 ? R.x0 <= 0.3f : R.x1 >= LayoutGenerator.W - 0.3f);
                float len = side < 2 ? R.W : R.D;
                float covered = 0;
                // what already stands against this wall counts toward the cover
                foreach (int fid in room.Furniture)
                {
                    var f = d.L.Furniture[fid]; if (!f.Blocks || Dresser.IsFlat(f.Type)) continue;
                    NavGrid.GetFootprint(f, 0, out var fp);
                    bool touches = side == 0 ? fp.z0 < R.z0 + 0.35f : side == 1 ? fp.z1 > R.z1 - 0.35f : side == 2 ? fp.x0 < R.x0 + 0.35f : fp.x1 > R.x1 - 0.35f;
                    if (touches) covered += side < 2 ? fp.W : fp.D;
                }
                float target = len * set.Cover;
                var pool = ext ? set.Low : set.Any;
                string Fit(float room0, Func<string, bool> ok)
                {
                    for (int k = 0; k < 8; k++) { var t = rng.Pick(pool); var df = FurnitureCatalog.Get(t); if (df != null && df.W <= room0 && (ok == null || ok(t))) return t; }
                    return null;
                }
                foreach (var (a, b) in d.FreeRuns(side, 0.65f).OrderByDescending(r => r.b - r.a).ToList())
                {
                    if (covered >= target) break;
                    float L = b - a; if (L < 0.9f) continue;
                    string main = Fit(L - 0.1f, null); if (main == null) continue;
                    var md = FurnitureCatalog.Get(main); float mid = (a + b) / 2;
                    if (RowType(main))
                    {
                        // a row: constant gap, centred in the stretch, no longer than the cover needs
                        float gap = CaseType(main) ? 0.06f : 0.4f;
                        int fit = (int)((L - 0.1f + gap) / (md.W + gap));
                        int need = Math.Max(1, (int)Math.Ceiling((target - covered) / md.W));
                        int n = Math.Max(1, Math.Min(fit, need));
                        float used = n * md.W + (n - 1) * gap, start = mid - used / 2 + md.W / 2;
                        for (int i = 0; i < n; i++) if (d.At(main, side, start + i * (md.W + gap), Math.Min(0.12f, gap * 0.8f)) != null) covered += md.W;
                        continue;
                    }
                    if (d.At(main, side, mid) == null) continue;
                    covered += md.W;
                    // a matched pair either side: close flankers, or the same piece centred in each leftover stretch
                    float rest = (L - md.W) / 2 - 0.15f;
                    if (covered >= target || rest < 0.6f) continue;
                    string fl = Fit(rest - 0.1f, t => FlankType(t) && t != main) ?? Fit(rest - 0.1f, t => t != main);
                    if (fl == null) continue;
                    var fd = FurnitureCatalog.Get(fl);
                    float off = FlankType(fl) ? md.W / 2 + 0.25f + fd.W / 2 : md.W / 2 + 0.15f + rest / 2;
                    int before = d.L.Furniture.Count;
                    var p1 = d.At(fl, side, mid - off); var p2 = p1 != null ? d.At(fl, side, mid + off) : null;
                    if (p1 == null || p2 == null) d.RollbackTo(before); else covered += 2 * fd.W;
                }
            }
        }

        /// <summary>Back against wall 'side' with its centre at 'along' metres from the wall's start.</summary>
        static Furniture WallAt(Dresser d, string type, int side, float along)
        {
            var def = FurnitureCatalog.Get(type); if (def == null) return null;
            var R = d.R;
            float yaw = side == 0 ? 0 : side == 1 ? 180 : side == 2 ? 90 : 270;
            float x, z;
            if (side < 2) { x = R.x0 + along; z = side == 0 ? R.z0 + def.D / 2 + 0.02f : R.z1 - def.D / 2 - 0.02f; }
            else { z = R.z0 + along; x = side == 2 ? R.x0 + def.D / 2 + 0.02f : R.x1 - def.D / 2 - 0.02f; }
            return d.Place(type, x, z, yaw, false, null, 0.12f);
        }

        static List<string> ZoneList(RoomType t)
        {
            switch (t)
            {
                case RoomType.Lounge: return new List<string> { "bar", "nook", "cards", "ottomans", "gramophone", "daybed", "table4", "chess", "seating", "console", "plant_group", "reading" };
                case RoomType.Parlor: return new List<string> { "nook", "cards", "gramophone", "fireplace", "console", "chess", "plants", "daybed" };
                case RoomType.Library: return new List<string> { "stacks", "nook", "table4", "globe", "catalog", "chess", "reading", "stacks", "telescope", "nook", "plant_group", "reading" };
                case RoomType.Study: return new List<string> { "nook", "cabinet", "globe", "telescope", "gramophone", "chess", "reading" };
                case RoomType.Dining: return new List<string> { "fireplace", "console", "display", "bar", "gramophone", "plants", "console" };
                case RoomType.Pool: return new List<string> { "poolside", "towels", "poolside", "diving", "plants", "bar", "loungers", "plant_group", "cage" };
                case RoomType.Theater: return new List<string> { "projector", "lectern", "piano", "costume" };
                case RoomType.Greenhouse: return new List<string> { "fountain", "plant_group", "cage", "bench", "plant_group", "telescope", "plants" };
                case RoomType.MusicRoom: return new List<string> { "listening", "harp", "stands", "gramophone", "seating", "nook", "plant_group", "daybed", "jukebox" };
                case RoomType.GameRoom: return new List<string> { "darts", "table4", "chess", "cards", "jukebox", "ottomans", "punch", "seating", "darts", "nook" };
                case RoomType.Workshop: return new List<string> { "sewing", "bench2", "easel", "crates" };
                case RoomType.Chapel: return new List<string> { "organ", "lectern", "candles" };
                case RoomType.Gallery: return new List<string> { "statue", "ottomans", "display", "bench", "console", "easel", "statue", "plant_group", "console" };
                case RoomType.Archive: return new List<string> { "catalog", "carrel", "carrel", "cabinet" };
                case RoomType.Wardrobe: return new List<string> { "sewing", "mirror", "costume", "chest" };
                case RoomType.TeaRoom: return new List<string> { "cards", "gramophone", "cage", "console", "display", "nook" };
                case RoomType.Courtyard: return new List<string> { "telescope", "bench", "plants", "cage" };
                case RoomType.TrophyRoom: return new List<string> { "display", "statue", "bench", "console", "chest" };
                case RoomType.DollRoom: return new List<string> { "cage", "display", "seating", "chest" };
                case RoomType.Kitchen: return new List<string> { "pantry", "cards" };
                case RoomType.Infirmary: return new List<string> { "display", "carrel" };
                case RoomType.Bedroom: case RoomType.GuestRoom: return new List<string> { "nook", "reading", "gramophone", "console" };
                case RoomType.MachineRoom: case RoomType.Storage: case RoomType.BoilerRoom: case RoomType.Laundry: return new List<string> { "crates", "crates", "chest" };
            }
            return new List<string>();
        }

        /// <summary>Try a zone at candidate anchors across the room (walls first, then open floor); true if placed.</summary>
        static bool TryZone(Dresser d, string z, Rng rng)
        {
            var R = d.R;
            var anchors = new List<(float x, float z, float yaw, int side)>();
            // wall anchors: facing into the room
            foreach (var side in new[] { 0, 1, 2, 3 }.OrderBy(_ => rng.F()))
                foreach (var t in new[] { 0.2f, 0.5f, 0.8f, 0.35f, 0.65f })
                {
                    float x = side < 2 ? R.x0 + t * R.W : side == 2 ? R.x0 : R.x1, zz = side < 2 ? (side == 0 ? R.z0 : R.z1) : R.z0 + t * R.D;
                    anchors.Add((x, zz, side == 0 ? 0 : side == 1 ? 180 : side == 2 ? 90 : 270, side));
                }
            // floor anchors: a lattice centred on the room (mirror-symmetric about both axes), each group turned along the
            // room axis toward the centre — no random headings
            var xs = new List<float> { R.CX }; var zs = new List<float> { R.CZ };
            for (float k = 2.4f; R.CX - k >= R.x0 + 1.8f; k += 2.4f) { xs.Add(R.CX - k); xs.Add(R.CX + k); }
            for (float k = 2.4f; R.CZ - k >= R.z0 + 1.8f; k += 2.4f) { zs.Add(R.CZ - k); zs.Add(R.CZ + k); }
            var floor = new List<(float x, float z, float yaw, int side)>();
            foreach (var fz in zs) foreach (var fx in xs)
                {
                    float dx = R.CX - fx, dz = R.CZ - fz, yaw;
                    if (Math.Abs(dx) < 0.1f && Math.Abs(dz) < 0.1f) yaw = R.W >= R.D ? 0 : 90;
                    else if (Math.Abs(dx) > Math.Abs(dz)) yaw = dx > 0 ? 90 : 270; else yaw = dz > 0 ? 0 : 180;
                    floor.Add((fx, fz, yaw, -1));
                }
            anchors.AddRange(floor.OrderBy(a => rng.F()));
            foreach (var a in anchors)
            {
                int before = d.L.Furniture.Count;
                if (BuildZone(d, z, a.x, a.z, a.yaw, a.side, rng)) return true;
                d.RollbackTo(before);   // partial group: undo it whole
            }
            return false;
        }

        /// <summary>Anchor point (x,z) is on a wall when side ≥ 0 (yaw faces into the room) or on open floor.</summary>
        static bool BuildZone(Dresser d, string z, float x, float zz, float yaw, int side, Rng rng)
        {
            double yr = yaw * Math.PI / 180; float fx = (float)Math.Sin(yr), fz = (float)Math.Cos(yr), rx = fz, rz = -fx;
            P3 At(float fwd, float lat) => new P3(d.Room.Floor, x + fx * fwd + rx * lat, zz + fz * fwd + rz * lat);
            Furniture P(string type, float fwd, float lat, float y, bool force = false) { var p = At(fwd, lat); return d.Place(type, p.x, p.z, yaw + y, force); }
            Furniture PC(string type, float fwd, float lat, float y, float clearance) { var p = At(fwd, lat); return d.Place(type, p.x, p.z, yaw + y, false, null, clearance); }
            bool wall = side >= 0;
            switch (z)
            {
                case "bar":
                    {
                        if (!wall) return false;
                        var b = P("BarCounter", 0.4f, 0, 0); if (b == null) return false;
                        for (int i = -1; i <= 1; i++) P("BarStool", 1.25f, i * 0.9f, 180);
                        d.ItemOn(b, "Bottle"); d.ItemOn(b, "WineGlass"); d.ItemOn(b, rng.Pick(new[] { "Beer", "Soda", "Cup" }));
                        return true;
                    }
                case "cards":
                    {
                        if (wall) return false;
                        var t = d.Place("RoundTable", x, zz, 0, false, null, 0.8f); if (t == null) return false;
                        foreach (var (cx0, cz0, cy0) in new[] { (0f, -0.95f, 0f), (0f, 0.95f, 180f), (-0.95f, 0f, 90f), (0.95f, 0f, 270f) }) if (d.Place("Chair", x + cx0, zz + cz0, cy0, false, null, 0.02f) == null) return false;
                        d.ItemOn(t, rng.Pick(new[] { "Cup", "Snack", "Chocolate" }));
                        return true;
                    }
                case "gramophone":
                    {
                        if (!wall) return false;
                        var g = P("Gramophone", 0.35f, 0, 0); if (g == null) return false;
                        P("Armchair", 1.6f, 0.9f, 200); return true;
                    }
                case "jukebox": return wall && P("Jukebox", 0.35f, 0, 0) != null;
                case "chess":
                    {
                        var t = wall ? P("ChessTable", 1.4f, 0, 0) : d.Place("ChessTable", x, zz, 0); if (t == null) return false;
                        return true;
                    }
                case "seating":
                    {
                        if (wall) return false;
                        // U-shaped group on a centred rug: sofa facing a low table, a matched pair of armchairs facing each other at its ends
                        bool rot = Math.Abs(Math.Round(yaw / 90f)) % 2 == 1; var rc = At(-0.35f, 0);
                        d.Rug(rc.x, rc.z, rot ? 3.3f : 4.4f, rot ? 4.4f : 3.3f);
                        if (P("Sofa", -1.15f, 0, 0) == null) return false;
                        if (P("CoffeeTable", 0, 0, 0) == null) return false;
                        if (P("Armchair", 0, 1.45f, -90) == null || P("Armchair", 0, -1.45f, 90) == null) return false;
                        return true;
                    }
                case "reading":
                    {
                        if (!wall) return false;
                        // a bookcase with a reading chair beside it, back to the wall, a side table between
                        var sh = P("Bookshelf", 0.25f, 0, 0); if (sh == null) return false;
                        if (PC("SideTable", 0.3f, 1.3f, 0, 0.02f) == null) return false;
                        PC("Armchair", 0.5f, 2.1f, 0, 0.02f);
                        return true;
                    }
                case "stacks":
                    {
                        if (wall) return false;
                        var a = d.Place("Bookshelf", x, zz, yaw); if (a == null) return false;
                        d.Place("Bookshelf", x + fx * 1.9f, zz + fz * 1.9f, yaw + 180);
                        return true;
                    }
                case "globe": return (wall ? P("Globe", 0.8f, 0, 0) : d.Place("Globe", x, zz, 0)) != null;
                case "telescope": return wall && P("Telescope", 0.8f, 0, 0) != null;
                case "catalog": return wall && P("CardCatalog", 0.3f, 0, 0) != null;
                case "fireplace":
                    {
                        if (!wall || d.Room.Furniture.Any(id => d.L.Furniture[id].Type == "Fireplace")) return false;
                        var f = P("Fireplace", 0.35f, 0, 0); if (f == null) return false;
                        P("Armchair", 1.9f, -1.0f, 200); P("Armchair", 1.9f, 1.0f, 160);
                        return true;
                    }
                case "display": return wall && P("DisplayCase", 0.3f, 0, 0) != null;
                case "plants": { var a = wall ? P("Plant", 0.4f, 0, 0) : d.Place("Plant", x, zz, 0); if (a == null) return false; if (wall) P("Plant", 0.4f, 1.2f, 0); return true; }
                case "diving":
                    {
                        var water = d.Room.Furniture.Select(id => d.L.Furniture[id]).FirstOrDefault(f => f.Type == "PoolWater"); if (water == null) return false;
                        // at the pool's short end, pointing over the water
                        bool alongX = water.W >= water.D; float hx = alongX ? water.W / 2 : 0, hz = alongX ? 0 : water.D / 2;
                        var db = d.Place("DivingBoard", water.Pos.x - hx - (alongX ? 0.9f : 0), water.Pos.z - hz - (alongX ? 0 : 0.9f), alongX ? 90 : 0, true);
                        return db != null;
                    }
                case "loungers": { if (!wall) return false; var a = P("Lounger", 1.1f, -0.6f, 0); if (a == null) return false; P("Lounger", 1.1f, 0.6f, 0); return true; }
                case "cage": return (wall ? P("BirdCage", 0.5f, 0, 0) : d.Place("BirdCage", x, zz, 0)) != null;
                case "projector":
                    {
                        if (d.Room.Type != RoomType.Theater) return false;
                        var stage = d.Room.Furniture.Select(id => d.L.Furniture[id]).FirstOrDefault(f => f.Type == "Stage"); if (stage == null || wall) return false;
                        // behind the last row, aimed at the stage
                        float yawTo = (float)(Math.Atan2(stage.Pos.x - x, stage.Pos.z - zz) * 180 / Math.PI);
                        if (Math.Sqrt((stage.Pos.x - x) * (stage.Pos.x - x) + (stage.Pos.z - zz) * (stage.Pos.z - zz)) < 6) return false;
                        return d.Place("FilmProjector", x, zz, yawTo) != null;
                    }
                case "lectern": return (wall ? P("Lectern", 1.2f, 0, 180) : d.Place("Lectern", x, zz, yaw)) != null;
                case "piano": return wall && P("Piano_Upright", 0.3f, 0, 0) != null;
                case "costume": return wall && P("CostumeRack", 0.3f, 0, 0) != null;
                case "fountain": return !wall && d.Place("Fountain", x, zz, 0) != null;
                case "bench": return (wall ? P("Bench", 0.25f, 0, 0) : d.Place("Bench", x, zz, yaw)) != null;
                case "harp": return (wall ? P("Harp", 0.8f, 0, 0) : d.Place("Harp", x, zz, yaw)) != null;
                case "stands": { if (wall) return false; var a = d.Place("MusicStand", x, zz, yaw); if (a == null) return false; d.Place("MusicStand", x + rx * 0.9f, zz + rz * 0.9f, yaw); d.Place("Chair", x - fx * 0.7f, zz - fz * 0.7f, yaw, false, null, 0.02f); return true; }
                case "darts": return wall && P("Dartboard", 0.07f, 0, 0) != null;
                case "punch": return (wall ? P("PunchingBag", 0.9f, 0, 0) : d.Place("PunchingBag", x, zz, 0)) != null;
                case "sewing": { if (!wall) return false; var s = P("SewingTable", 0.3f, 0, 0); if (s == null) return false; P("Chair", 0.95f, 0, 180, true); d.ItemOn(s, "Thread"); d.ItemOn(s, rng.Pick(new[] { "Scissors", "Scarf", "Button" })); return true; }
                case "bench2": { if (!wall) return false; var w = P("Workbench", 0.45f, 0, 0); if (w == null) return false; d.ItemOn(w, rng.Pick(new[] { "Rope", "Hammer", "WindupToy" })); return true; }
                case "easel": return (wall ? P("Easel", 0.9f, 0, 0) : d.Place("Easel", x, zz, yaw)) != null;
                case "crates": return wall && P("Crates", 0.62f, 0, 0) != null;   // against a wall, square to it
                case "organ": return wall && !d.Room.Furniture.Any(id => d.L.Furniture[id].Type == "Organ") && P("Organ", 0.5f, 0, 0) != null;
                case "candles": { var a = wall ? P("Candelabra", 0.4f, 0, 0) : d.Place("Candelabra", x, zz, 0); if (a == null) return false; if (wall) P("Candelabra", 0.4f, 1.4f, 0); return true; }
                case "statue": return !wall && d.Place("Statue", x, zz, yaw) != null;   // turned to the middle of the room
                case "carrel": { if (!wall) return false; var dk = P("Desk", 0.3f, 0, 0); if (dk == null) return false; P("Chair", 0.85f, 0, 180, true); d.ItemOn(dk, rng.Pick(new[] { "Document", "Book", "Notebook" })); return true; }
                case "mirror": return wall && P("Mirror", 0.1f, 0, 0) != null;
                case "pantry": { if (!wall) return false; var s = P("Shelves", 0.3f, 0, 0); if (s == null) return false; d.ItemOn(s, rng.Pick(new[] { "Bread", "Snack", "Tea" })); return true; }
                case "nook":
                    {
                        // a reading corner: an easy chair angled into the room, a side table and a standard lamp
                        if (!wall) return false;
                        var seat = P(rng.Chance(0.35) ? "RockingChair" : "Armchair", 0.72f, 0, 0); if (seat == null) return false;
                        PC("SideTable", 0.45f, 0.95f, 0, 0.02f);
                        PC("FloorLamp", 0.34f, -0.85f, 0, 0.02f);
                        return true;
                    }
                case "daybed":
                    {
                        if (!wall) return false;
                        var b = P("DayBed", 0.47f, 0, 0); if (b == null) return false;
                        PC("SideTable", 0.3f, 1.25f, 0, 0.02f);
                        return true;
                    }
                case "console": return wall && P("Console", 0.26f, 0, 0) != null;
                case "cabinet": return wall && !d.Room.Furniture.Any(id => d.L.Furniture[id].Type == "Cabinet") && P("Cabinet", 0.34f, 0, 0) != null;
                case "chest": return wall && P("Chest", 0.31f, 0, 0) != null;
                case "listening":
                    {
                        // three chairs in a shallow arc on open floor, turned toward the room's performer / hearth side
                        if (wall) return false;
                        int ok = 0;
                        for (int i = -1; i <= 1; i++) { var p = At(0.25f * i * i, i * 0.8f); if (d.Place("Chair", p.x, p.z, yaw - i * 14f, false, null, 0.05f) != null) ok++; }
                        return ok >= 2;
                    }
                case "poolside":
                    {
                        if (wall) return false;
                        var a = d.Place("Lounger", x - 0.55f, zz, yaw); if (a == null) return false;
                        d.Place("Lounger", x + 0.55f, zz, yaw); d.Place("SideTable", x + 1.25f, zz - 0.4f, 0, false, null, 0.05f);
                        return true;
                    }
                case "towels": return (wall ? P("Cart", 0.5f, 0, 0) : d.Place("Cart", x, zz, yaw)) != null;
                case "ottomans":
                    {
                        if (wall) return false;
                        var a = d.Place("Ottoman", x - 0.55f, zz, 0); if (a == null) return false;
                        d.Place("Ottoman", x + 0.55f, zz, 0); d.Place("SideTable", x, zz + 0.6f, 0, false, null, 0.05f);
                        return true;
                    }
                case "table4":
                    {
                        if (wall) return false;
                        var t = d.Place("RoundTable", x, zz, 0, false, null, 0.8f); if (t == null) return false;
                        foreach (var (cx0, cz0, cy0) in new[] { (0f, -0.95f, 0f), (0f, 0.95f, 180f), (-0.95f, 0f, 90f), (0.95f, 0f, 270f) }) if (d.Place("Chair", x + cx0, zz + cz0, cy0, false, null, 0.02f) == null) return false;
                        return true;
                    }
                case "plant_group":
                    {
                        var a = wall ? P("Plant", 0.45f, 0, 0) : d.Place("Plant", x, zz, 0); if (a == null) return false;
                        if (wall) { PC("Plant", 0.4f, 0.75f, 0, 0.02f); PC("Plant", 0.4f, -0.75f, 0, 0.02f); } else { d.Place("Plant", x + 0.75f, zz + 0.2f, 0, false, null, 0.02f); }
                        return true;
                    }
            }
            return false;
        }

        // ------------------------------------------------------------------ central islands
        static string[] IslandKinds(RoomType t)
        {
            switch (t)
            {
                case RoomType.Study: return new[] { "table", "chat", "globe_chess", "seating" };
                case RoomType.Library: return new[] { "table", "vitrines", "seating" };
                case RoomType.Archive: return new[] { "shelfrow", "table" };
                case RoomType.MusicRoom: return new[] { "seating", "chat", "cards" };
                case RoomType.GameRoom: return new[] { "cards", "table4", "seating" };
                case RoomType.Lounge: case RoomType.Parlor: case RoomType.TeaRoom: return new[] { "chat", "cards" };
                case RoomType.Gallery: case RoomType.TrophyRoom: return new[] { "gallery_bench", "vitrines" };
                case RoomType.DollRoom: return new[] { "cards", "chat" };
                case RoomType.Wardrobe: return new[] { "ottomans", "chat" };
                case RoomType.Bedroom: case RoomType.GuestRoom: return new[] { "chat" };
                case RoomType.Kitchen: case RoomType.Infirmary: case RoomType.Laundry: return new[] { "table" };
                case RoomType.Workshop: case RoomType.Darkroom: return new[] { "workbench" };
                case RoomType.ColdStorage: return new[] { "workbench", "crates" };
                case RoomType.Storage: case RoomType.Closet: return new[] { "shelfrow", "crates" };
                case RoomType.WineCellar: return new[] { "barrel", "crates" };
                case RoomType.BoilerRoom: case RoomType.PowerRoom: case RoomType.WaterRoom: case RoomType.MachineRoom: case RoomType.Incinerator: return new[] { "crates" };
                case RoomType.ButlerRoom: return new[] { "table4" };
            }
            return null;
        }

        /// <summary>A room whose middle stayed empty after its groups and zones gets one deliberate island there (two in
        /// big rooms): a library table with its chairs, back-to-back vitrines, a rug with a seating group, a tea party of
        /// chairs, a workbench, a double-sided shelf row... always leaving the 1.2 m lanes between the doors.</summary>
        static void Island(Dresser d, Room room, Rng rng)
        {
            var R = d.R;
            if (room.Floor < -1 || RoomInfo.IsPassage(room.Type) || RoomInfo.IsMystery(room.Type)) return;
            var kinds = IslandKinds(room.Type); if (kinds == null) return;
            if (R.W - 2.6f < 2.2f || R.D - 2.6f < 2.2f) return;
            if (d.InteriorFill() >= 0.12f) return;
            bool alongX = R.W >= R.D; float yaw = alongX ? 0 : 90;
            // candidate centres: the middle first, then the halves and quarters of the room (mirror pairs), each turned
            // along the room first and across it second
            var centres = new List<(float x, float z)>();
            foreach (float ox in new[] { 0f, -0.24f, 0.24f }) foreach (float oz in new[] { 0f, -0.24f, 0.24f }) centres.Add((R.CX + ox * R.W, R.CZ + oz * R.D));
            centres = centres.OrderBy(c => Math.Abs(c.x - R.CX) / R.W + Math.Abs(c.z - R.CZ) / R.D).ToList();
            int max = R.W * R.D > 150 ? 3 : R.W * R.D > 90 ? 2 : 1, placed = 0;
            foreach (var c in centres)
            {
                if (placed >= max || d.InteriorFill() >= 0.16f) break;
                bool done = false;
                foreach (var k in kinds) foreach (float yw in new[] { yaw, yaw + 90 })
                {
                    if (done) break;
                    int before = d.L.Furniture.Count;
                    if (BuildIsland(d, k, c.x, c.z, yw, rng)) { placed++; done = true; break; }
                    if (DebugIslands) d.L.GenLog.Add($"islandfail {room.Type} {k}/{yw} fill {d.InteriorFill():0.00} at {c.x - R.CX:0.0},{c.z - R.CZ:0.0} room {R.W:0.0}x{R.D:0.0} last {IslandFail}");
                    d.RollbackTo(before);
                }
            }
        }

        public static bool DebugIslands; static string IslandFail;
        static bool BuildIsland(Dresser d, string kind, float x, float z, float yaw, Rng rng)
        {
            IslandFail = "";
            double yr = yaw * Math.PI / 180; float fx = (float)Math.Sin(yr), fz = (float)Math.Cos(yr), rx = fz, rz = -fx;
            P3 At(float fwd, float lat) => new P3(d.Room.Floor, x + fx * fwd + rx * lat, z + fz * fwd + rz * lat);
            Furniture P(string type, float fwd, float lat, float y, float clearance = 0.35f) { var p = At(fwd, lat); var r = d.Place(type, p.x, p.z, yaw + y, false, null, clearance); if (r == null && DebugIslands) IslandFail += type + " "; return r; }
            bool rot = Math.Abs(Math.Round(yaw / 90f)) % 2 == 1;
            void Rug(float w, float dd) { d.Rug(x, z, rot ? dd : w, rot ? w : dd); }
            switch (kind)
            {
                case "table":
                    {
                        // a long reading / work table on a rug, two chairs a side, pushed in (the lamp and books come with the dressing)
                        Rug(3.4f, 3.0f);
                        if (P("ReadingTable", 0, 0, 0, 0.8f) == null) return false;
                        foreach (float lat in new[] { -0.45f, 0.45f })
                        {
                            int b = d.L.Furniture.Count;
                            var c1 = P("Chair", -0.81f, lat, 0, 0.02f); var c2 = P("Chair", 0.81f, lat, 180, 0.02f);
                            if (c1 == null || c2 == null) d.RollbackTo(b);
                        }
                        return true;
                    }
                case "vitrines":
                    {
                        // two glass cases back to back, a pedestal at each end
                        if (P("DisplayCase", -0.31f, 0, 180, 0.05f) == null || P("DisplayCase", 0.31f, 0, 0, 0.05f) == null) return false;
                        int b = d.L.Furniture.Count;
                        var p1 = P("Pedestal", 0, -1.35f, 0, 0.05f); var p2 = P("Pedestal", 0, 1.35f, 0, 0.05f);
                        if (p1 == null || p2 == null) d.RollbackTo(b); else { d.ItemOn(p1, rng.Pick(new[] { "Vase", "Trophy" })); d.ItemOn(p2, rng.Pick(new[] { "Vase", "Candlestick" })); }
                        return true;
                    }
                case "seating": return BuildZone(d, "seating", x, z, yaw, -1, rng);
                case "cards": return BuildZone(d, "cards", x, z, yaw, -1, rng);
                case "table4": return BuildZone(d, "table4", x, z, yaw, -1, rng);
                case "ottomans": return BuildZone(d, "ottomans", x, z, yaw, -1, rng);
                case "chat":
                    {
                        // a tête-à-tête: two easy chairs facing across a small table on a rug
                        Rug(3.0f, 2.2f);
                        if (P("SideTable", 0, 0, 0, 0.05f) == null) return false;
                        if (P("Armchair", 0, -0.9f, 90, 0.02f) == null || P("Armchair", 0, 0.9f, -90, 0.02f) == null) return false;
                        return true;
                    }
                case "globe_chess":
                    {
                        Rug(2.8f, 2.0f);
                        if (P("ChessTable", 0, -0.55f, 0, 0.05f) == null || P("Globe", 0, 0.65f, 0, 0.05f) == null) return false;
                        int b = d.L.Furniture.Count; var a = P("Chair", -0.7f, -0.55f, 0, 0.02f); var c = P("Chair", 0.7f, -0.55f, 180, 0.02f); if (a == null || c == null) d.RollbackTo(b);
                        return true;
                    }
                case "gallery_bench":
                    {
                        // a double bench in the middle of the room, back to back, to sit and look at the walls
                        if (P("Bench", -0.27f, 0, 180, 0.02f) == null || P("Bench", 0.27f, 0, 0, 0.02f) == null) return false;
                        return true;
                    }
                case "shelfrow":
                    {
                        // a free-standing double-sided shelf run (backs together)
                        if (P("Shelves", -0.31f, 0, 180, 0.02f) == null || P("Shelves", 0.31f, 0, 0, 0.02f) == null) return false;
                        return true;
                    }
                case "workbench":
                    {
                        if (P("Workbench", 0, 0, 0, 0.6f) == null) return false;
                        P("Chair", 0.85f, -0.5f, 180, 0.02f);
                        return true;
                    }
                case "barrel":
                    {
                        // a tasting barrel with two stools
                        if (P("Barrel", 0, 0, 0, 0.4f) == null) return false;
                        P("BarStool", 0, -0.72f, 90, 0.02f); P("BarStool", 0, 0.72f, -90, 0.02f);
                        return true;
                    }
                case "crates":
                    {
                        if (P("Crates", 0, -0.4f, 0, 0.1f) == null) return false;
                        P("Barrel", 0, 0.75f, 0, 0.05f);
                        return true;
                    }
            }
            return false;
        }

        /// <summary>4 m galleries are promenades, not service corridors: benches, statues, plants and paintings at intervals along both walls.</summary>
        static void Gallery(Dresser d, Room room, Rng rng)
        {
            var R = d.R; bool alongX = R.W >= R.D; float len = alongX ? R.W : R.D;
            if (Math.Min(R.W, R.D) < 3.5f) return;
            int n = (int)(len / 4.6f);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / Math.Max(1, n);
                int side = alongX ? (i % 2 == 0 ? 0 : 1) : (i % 2 == 0 ? 2 : 3);
                string what = rng.Pick(new[] { "Console", "Bench", "Plant", "Console", "Pedestal", "Candelabra", "Statue", "Bench", "Console", "DisplayCase", "BirdCage" });
                // matched pairs facing each other across the promenade (both or neither)
                int before = d.L.Furniture.Count;
                var f = d.Wall(what, side, t, 0.02f); var f2 = f != null ? d.Wall(what, alongX ? 1 - side : 5 - side, t, 0.02f) : null;
                if (f == null || f2 == null) { d.RollbackTo(before); continue; }
                if (what == "Pedestal") d.ItemOn(f, rng.Pick(new[] { "Vase", "Trophy", "Candlestick" }));
            }
        }

        // ==================================================================== owner dressing (env-art) — BEGIN
        // Each resident's private room carries their own furniture, chosen by owner id only (never by rng), so the same
        // person lives with the same things on every seed and loop. Placed lane-aware against the walls after the base set
        // (bed, nightstands, desk, wardrobe) and the kernel's personal items, so it never moves either. Only neutral pieces:
        // nothing interactive (speakers, dollhouses, arcades) that would add gameplay to a private room.

        /// <summary>The resident's own pieces, most characteristic first (dropped from the end when the room is full).</summary>
        static string[] OwnerPieces(string owner)
        {
            switch (owner)
            {
                case "P01": return new[] { "Armchair", "SideTable" };                    // the player: a reading chair by the lamp
                case "P02": return new[] { "ChessTable", "Armchair", "Bookshelf" };      // the observer's chair, psychology shelves
                case "P03": return new[] { "FileCabinet", "Bookshelf" };                 // the council president's files
                case "P04": return new[] { "Easel", "Console" };                         // the restorer's easel and a bare console
                case "P05": return new[] { "Lectern", "Mirror" };                        // speech practice in front of a mirror
                case "P06": return new[] { "FileCabinet", "Shelves" };                   // stock and ledgers
                case "P07": return new[] { "Sofa", "Gramophone" };                       // the host: a sofa for the crowd, records
                case "P08": return new[] { "MusicStand", "Armchair" };                   // the bassist's corner
                case "P09": return new[] { "DayBed", "VanityDesk" };                     // the actor lounging, a dressing-room mirror
                case "P10": return new[] { "TeaCart", "Plant" };                         // the cook's cart and something green
                case "P11": return new[] { "Workbench", "ToolWall" };                    // the engineer's bench
                case "P12": return new[] { "VanityDesk", "DisplayCase" };                // the idol's mirror and her goods
                case "P13": return new[] { "Bench", "Shelves" };                         // the gamer: a workout bench, trophies
                case "P14": return new[] { "Candelabra", "TeaCart" };                    // the funeral director: candles, tea
                case "P15": return new[] { "FileCabinet", "Globe", "Bookshelf" };        // the journalist's files and map
                case "P16": return new[] { "DressForm", "SewingTable", "CostumeRack" };  // the stylist's atelier
                case "P17": return new[] { "Console", "Ottoman" };                       // the paper-model table, a seat for guests
                case "P18": return new[] { "Chest" };                                    // the labourer: almost nothing
            }
            return new string[0];
        }

        static void OwnerFurniture(Dresser d, Room room, int bedSide)
        {
            foreach (var type in OwnerPieces(room.Owner))
            {
                var def = FurnitureCatalog.Get(type); if (def == null) continue;
                // walls in a fixed order from the bed's opposite wall; the widest free stretch that takes it, centred
                bool done = false;
                for (int k = 0; k < 4 && !done; k++)
                {
                    int side = (bedSide + 2 + k) % 4;
                    foreach (var (a, b) in d.FreeRuns(side, def.D).OrderByDescending(r => r.b - r.a))
                    {
                        if (b - a < def.W + 0.1f) continue;
                        if (d.At(type, side, (a + b) / 2) != null) { done = true; break; }
                    }
                }
                if (!done && (type == "Sofa" || type == "Workbench")) OwnerFallback(d, type == "Sofa" ? "Ottoman" : "Console", bedSide);
            }
        }

        static void OwnerFallback(Dresser d, string type, int bedSide)
        {
            var def = FurnitureCatalog.Get(type); if (def == null) return;
            for (int k = 0; k < 4; k++)
                foreach (var (a, b) in d.FreeRuns((bedSide + 2 + k) % 4, def.D).OrderByDescending(r => r.b - r.a))
                    if (b - a >= def.W + 0.1f && d.At(type, (bedSide + 2 + k) % 4, (a + b) / 2) != null) return;
        }

        /// <summary>How much of the walls the resident lines, and with what: tidy people keep bare walls, messy ones fill them.</summary>
        static WallSet OwnerPerimeter(string owner)
        {
            WallSet S(float cover, string[] any, string[] low) => new WallSet { Any = any, Low = low, Cover = cover };
            switch (owner)
            {
                case "P01": return S(0.32f, new[] { "Bookshelf", "Console", "Plant", "Chest" }, new[] { "Console", "Chest", "Plant" });
                case "P02": return S(0.34f, new[] { "Bookshelf", "Bookshelf", "Console", "Plant" }, new[] { "Console", "Chest" });
                case "P03": return S(0.26f, new[] { "Bookshelf", "Console", "FileCabinet" }, new[] { "Console" });
                case "P04": return S(0.16f, new[] { "Console" }, new[] { "Console" });
                case "P05": return S(0.3f, new[] { "Console", "DisplayCase", "Plant", "Armchair" }, new[] { "Console", "Plant" });
                case "P06": return S(0.38f, new[] { "Shelves", "Chest", "Console", "FileCabinet" }, new[] { "Chest", "Console" });
                case "P07": return S(0.42f, new[] { "Console", "Chest", "Armchair", "Plant" }, new[] { "Chest", "Console", "Armchair" });
                case "P08": return S(0.36f, new[] { "Chest", "Console", "Armchair" }, new[] { "Chest", "Console" });
                case "P09": return S(0.34f, new[] { "CostumeRack", "Console", "Plant", "Mirror" }, new[] { "Console", "Plant" });
                case "P10": return S(0.34f, new[] { "Plant", "Console", "Plant", "Cabinet" }, new[] { "Plant", "Console" });
                case "P11": return S(0.4f, new[] { "Shelves", "Chest", "Console" }, new[] { "Chest", "Console" });
                case "P12": return S(0.34f, new[] { "Console", "Mirror", "Plant", "Chest" }, new[] { "Console", "Chest" });
                case "P13": return S(0.3f, new[] { "Console", "Chest", "Shelves" }, new[] { "Console", "Chest" });
                case "P14": return S(0.18f, new[] { "Console", "Candelabra" }, new[] { "Console" });
                case "P15": return S(0.42f, new[] { "Bookshelf", "FileCabinet", "Console", "Chest" }, new[] { "Console", "Chest" });
                case "P16": return S(0.36f, new[] { "CostumeRack", "Mirror", "Console", "Chest" }, new[] { "Console", "Chest" });
                case "P17": return S(0.34f, new[] { "Console", "Chest", "Plant", "Bookshelf" }, new[] { "Console", "Chest" });
                case "P18": return S(0.12f, new[] { "Chest" }, new[] { "Chest" });
            }
            return null;
        }

        /// <summary>Activity corners for the larger private rooms (none for the spartan and the spotless).</summary>
        static List<string> OwnerZones(string owner)
        {
            switch (owner)
            {
                case "P02": return new List<string> { "reading" };
                case "P04": case "P14": case "P18": case "P03": return new List<string>();
                case "P07": return new List<string> { "nook" };
                case "P09": return new List<string> { "gramophone" };
                case "P15": return new List<string> { "reading" };
            }
            return null;
        }
        // ==================================================================== owner dressing (env-art) — END

        public static string[] PersonalItems(string owner)
        {
            switch (owner)
            {
                case "P01": return new[] { "Bread" };
                case "P02": return new[] { "Candy", "Book" };
                case "P03": return new[] { "Notebook", "Snack" };
                case "P04": return new[] { "Tea", "PaletteKnife" };
                case "P05": return new[] { "Document" };
                case "P06": return new[] { "Chocolate", "Document" };
                case "P07": return new[] { "Beer", "Book" };
                case "P08": return new[] { "HandWarmer" };
                case "P09": return new[] { "PaperModel" };
                case "P10": return new[] { "Bread" };
                case "P11": return new[] { "WindupToy", "Soda" };
                case "P12": return new[] { "Sticker", "Snack" };
                case "P13": return new[] { "Soda" };
                case "P14": return new[] { "PaperModel", "Tea" };
                case "P15": return new[] { "Notebook", "Camera" };
                case "P16": return new[] { "Button", "Scissors" };
                case "P17": return new[] { "PaperModel", "Invitation" };
                case "P18": return new[] { "Thermos" };
            }
            return new string[0];
        }
    }
}
