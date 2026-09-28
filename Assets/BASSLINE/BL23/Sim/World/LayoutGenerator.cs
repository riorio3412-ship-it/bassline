using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Generates the whole mansion for a loop: shared vertical corridor skeleton (1F/2F), per-floor zone subdivision,
    /// functional + mystery room assignment, doors/suites, stairs, elevator, circuits and furniture.
    /// Same seed + loop => same layout. The result is stored whole in the save.
    /// </summary>
    public static class LayoutGenerator
    {
        public const int W = 68, D = 54;

        sealed class Area { public int Id; public RectI R; public bool Passage; public RoomType Type = RoomType.Parlor; public bool Assigned; public string Tag; public bool Void; public bool Dead; }
        struct RectI
        {
            public int x0, z0, x1, z1; // [x0,x1) cells
            public RectI(int a, int b, int c, int d) { x0 = Math.Min(a, c); z0 = Math.Min(b, d); x1 = Math.Max(a, c); z1 = Math.Max(b, d); }
            public int W => x1 - x0; public int D => z1 - z0; public int Area => W * D;
            public RectF F(float ox = 0, float oz = 0) => new RectF(x0 + ox, z0 + oz, x1 + ox, z1 + oz);
            public bool Valid => W > 0 && D > 0;
            public RectI Inflate(int d) => new RectI(x0 - d, z0 - d, x1 + d, z1 + d);
            public RectI Clip(RectI o) => new RectI(Math.Max(x0, o.x0), Math.Max(z0, o.z0), Math.Min(x1, o.x1), Math.Min(z1, o.z1));
        }

        sealed class FloorGrid
        {
            public int F, X0, Z0, NX, NZ; public int[,] G; public List<Area> Areas = new List<Area>();
            public FloorGrid(int f, int x0, int z0, int nx, int nz) { F = f; X0 = x0; Z0 = z0; NX = nx; NZ = nz; G = new int[nx, nz]; for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) G[i, j] = -1; }
            public bool In(int x, int z) => x >= X0 && z >= Z0 && x < X0 + NX && z < Z0 + NZ;
            public int At(int x, int z) => In(x, z) ? G[x - X0, z - Z0] : -2;
            public Area Add(RectI r, bool passage, RoomType t, bool overwrite = false)
            {
                var a = new Area { Id = Areas.Count, R = r, Passage = passage, Type = t, Assigned = !passage ? false : true };
                Areas.Add(a);
                for (int x = r.x0; x < r.x1; x++) for (int z = r.z0; z < r.z1; z++) if (In(x, z) && (overwrite || G[x - X0, z - Z0] == -1)) G[x - X0, z - Z0] = a.Id;
                return a;
            }
            public bool Free(RectI r) { for (int x = r.x0; x < r.x1; x++) for (int z = r.z0; z < r.z1; z++) if (!In(x, z) || G[x - X0, z - Z0] != -1) return false; return true; }
            // length of contiguous boundary between area rect side and passages. side: 0=S(z0),1=N(z1),2=W(x0),3=E(x1)
            public int Touch(RectI r, int side, Func<int, bool> pred, out int bestStart, out int bestLen)
            {
                bestStart = 0; bestLen = 0; int run = 0, runStart = 0, total = 0;
                int n = side < 2 ? r.W : r.D;
                for (int t = 0; t < n; t++)
                {
                    int x, z;
                    if (side == 0) { x = r.x0 + t; z = r.z0 - 1; } else if (side == 1) { x = r.x0 + t; z = r.z1; }
                    else if (side == 2) { x = r.x0 - 1; z = r.z0 + t; } else { x = r.x1; z = r.z0 + t; }
                    int id = At(x, z); bool ok = id >= 0 && pred(id);
                    if (ok) { if (run == 0) runStart = t; run++; total++; if (run > bestLen) { bestLen = run; bestStart = runStart; } } else run = 0;
                }
                return total;
            }
        }

        public static Layout Generate(ulong campaignSeed, int loop, RngSet rngs, bool validate = true)
        {
            var rng = rngs.Get(Stream.Layout, loop);
            Layout best = null; var log = new List<string>();
            for (int attempt = 1; attempt <= 40; attempt++)
            {
                var L = TryGenerate(rng, loop, out string fail);
                if (L != null)
                {
                    string err = validate ? Validate(L) : null;
                    if (err == null) { L.Attempts = attempt; L.Seed = campaignSeed; L.GenLog.AddRange(log); L.GenLog.Add($"attempt {attempt} ok skeleton={L.Skeleton}"); L.Hash = L.ComputeHash(); return L; }
                    fail = err;
                }
                log.Add($"attempt {attempt} rejected: {fail}");
                best = L ?? best;
            }
            throw new InvalidOperationException("Layout generation failed after retries: " + string.Join("; ", log.Skip(Math.Max(0, log.Count - 5))));
        }

        static Layout TryGenerate(Rng rng, int loop, out string fail)
        {
            fail = null;
            var L = new Layout { LoopId = loop };
            L.Floors.Add(new FloorInfo { F = 0, Bounds = new RectF(0, 0, W, D), BaseY = 0f, Height = 4.8f });
            L.Floors.Add(new FloorInfo { F = 1, Bounds = new RectF(0, 0, W, D), BaseY = 4.8f, Height = 4.2f });
            L.Floors.Add(new FloorInfo { F = -1, Bounds = new RectF(10, 8, 58, 46), BaseY = -4.6f, Height = 4.2f });
            L.Floors.Add(new FloorInfo { F = -2, Bounds = new RectF(18, 12, 42, 36), BaseY = -16f, Height = 11f });

            // ---------- shared skeleton ----------
            // Double-loaded galleries: every gallery (4 m, furnished, windowed where it meets the facade) has rooms
            // 10-17 m deep on its sides, so the house reads as a few grand, individual rooms rather than a warren.
            string[] skels = { "Ring", "Cross", "Comb", "Spine" };
            string skel = skels[rng.R(skels.Length)];
            L.Skeleton = skel;
            var bands = new List<RectI>();
            int cw = 5, sa = cw / 2, sb = cw - cw / 2;
            int hw = rng.Pick(new[] { 22, 24, 26 }), hd = rng.Pick(new[] { 16, 18 });
            int hcx = W / 2 + rng.R(-2, 3), hcz = D / 2 + rng.R(-1, 2);
            RectI hall;
            if (skel == "Ring")
            {
                // perimeter gallery along the facade, rooms ring the hall inside it, 2-3 spokes cut through
                hall = new RectI(hcx - hw / 2, hcz - hd / 2, hcx + hw / 2, hcz + hd / 2);
                bands.Add(new RectI(1, 1, W - 1, 1 + cw)); bands.Add(new RectI(1, D - 1 - cw, W - 1, D - 1));
                bands.Add(new RectI(1, 1 + cw, 1 + cw, D - 1 - cw)); bands.Add(new RectI(W - 1 - cw, 1 + cw, W - 1, D - 1 - cw));
                var sides = new List<int> { 0, 1, 2, 3 }; rng.Shuffle(sides); int ns = rng.R(2, 4);
                if (!(sides.Take(ns).Contains(0) || sides.Take(ns).Contains(1))) sides.Insert(0, 0);
                foreach (var s in sides.Take(Math.Max(ns, 2)))
                {
                    int off = rng.R(-3, 4);
                    if (s == 0) bands.Add(new RectI(hcx - sa + off, 1 + cw, hcx + sb + off, hall.z0));
                    if (s == 1) bands.Add(new RectI(hcx - sa + off, hall.z1, hcx + sb + off, D - 1 - cw));
                    if (s == 2) bands.Add(new RectI(1 + cw, hcz - sa + off, hall.x0, hcz + sb + off));
                    if (s == 3) bands.Add(new RectI(hall.x1, hcz - sa + off, W - 1 - cw, hcz + sb + off));
                }
            }
            else if (skel == "Comb")
            {
                // two long galleries (south/north) through the whole house; the hall spans the band between them
                int sd = rng.R(11, 15), nd = rng.R(11, 15);
                int zs = 1 + sd, zn = D - 1 - nd - cw;
                hall = new RectI(hcx - hw / 2, zs + cw, hcx + hw / 2, zn);
                bands.Add(new RectI(1, zs, W - 1, zs + cw)); bands.Add(new RectI(1, zn, W - 1, zn + cw));
                // one end connector keeps a loop; the other end is a room
                if (rng.Chance(0.5)) bands.Add(new RectI(1, zs + cw, 1 + cw, zn)); else bands.Add(new RectI(W - 1 - cw, zs + cw, W - 1, zn));
            }
            else if (skel == "Cross")
            {
                // four galleries radiate from the hall to the facade; quadrants hold the big rooms
                hall = new RectI(hcx - hw / 2, hcz - hd / 2, hcx + hw / 2, hcz + hd / 2);
                bands.Add(new RectI(hcx - sa, 1, hcx + sb, hall.z0)); bands.Add(new RectI(hcx - sa, hall.z1, hcx + sb, D - 1));
                bands.Add(new RectI(1, hcz - sa, hall.x0, hcz + sb)); bands.Add(new RectI(hall.x1, hcz - sa, W - 1, hcz + sb));
            }
            else // Spine: one east-west gallery through the hall, two short north-south ribs
            {
                hall = new RectI(hcx - hw / 2, hcz - hd / 2, hcx + hw / 2, hcz + hd / 2);
                bands.Add(new RectI(1, hcz - sa, hall.x0, hcz + sb)); bands.Add(new RectI(hall.x1, hcz - sa, W - 1, hcz + sb));
                int xw = rng.R(12, Math.Max(13, hall.x0 - cw - 11)), xe = rng.R(Math.Min(hall.x1 + 11, W - cw - 13), W - cw - 12);
                if (rng.Chance(0.5)) bands.Add(new RectI(xw, 1, xw + cw, hcz - sa)); else bands.Add(new RectI(xw, hcz + sb, xw + cw, D - 1));
                if (rng.Chance(0.5)) bands.Add(new RectI(xe, 1, xe + cw, hcz - sa)); else bands.Add(new RectI(xe, hcz + sb, xe + cw, D - 1));
            }
            bands = bands.Where(b => b.Valid).Select(b => b.Clip(new RectI(1, 1, W - 1, D - 1))).Where(b => b.Valid).ToList();

            // stairwell reserved on all floors: adjacent to a band, outside hall, inside B1 bounds
            RectI stairwell = default; bool swOk = false; bool swAlongX = true;
            var bandList = bands.ToList(); rng.Shuffle(bandList);
            // a generous 8 x 6 m well first (two side-by-side flights 2.5 m wide: up on one side, down on the other), the
            // compact 6 x 4 m well only where nothing larger fits
            foreach (var (sl, sd) in new[] { (8, 6), (6, 4) })
            foreach (var b in bandList)
            {
                if (swOk) break;
                for (int tries = 0; tries < 12 && !swOk; tries++)
                {
                    bool horizontal = b.W >= b.D; // band runs along x
                    RectI cand;
                    if (horizontal)
                    {
                        int x = rng.R(b.x0, Math.Max(b.x0 + 1, b.x1 - sl)); bool north = rng.Chance(0.5);
                        cand = north ? new RectI(x, b.z1, x + sl, b.z1 + sd) : new RectI(x, b.z0 - sd, x + sl, b.z0);
                        swAlongX = true;
                    }
                    else
                    {
                        int z = rng.R(b.z0, Math.Max(b.z0 + 1, b.z1 - sl)); bool east = rng.Chance(0.5);
                        cand = east ? new RectI(b.x1, z, b.x1 + sd, z + sl) : new RectI(b.x0 - sd, z, b.x0, z + sl);
                        swAlongX = false;
                    }
                    if (cand.x0 < 13 || cand.z0 < 11 || cand.x1 > 55 || cand.z1 > 43) continue;
                    if (Overlap(cand, hall.Inflate(1))) continue;
                    if (bands.Any(o => Overlap(cand, o))) continue;
                    stairwell = cand; swOk = true;
                }
            }
            if (!swOk) { fail = "no stairwell spot"; return null; }

            // ---------- floors ----------
            var f0 = new FloorGrid(0, 0, 0, W, D); var f1 = new FloorGrid(1, 0, 0, W, D);
            var hall0 = f0.Add(hall, true, RoomType.GrandHall);
            foreach (var b in bands) { f0.Add(b, true, RoomType.Corridor); f1.Add(b, true, RoomType.Corridor); }
            f0.Add(stairwell, true, RoomType.Stairwell); f1.Add(stairwell, true, RoomType.Stairwell);
            // 2F: landing ring + void over the hall
            var ring = hall; var voidR = new RectI(hall.x0 + 3, hall.z0 + 3, hall.x1 - 3, hall.z1 - 3);
            var landing = f1.Add(ring, true, RoomType.Landing);
            var voidA = f1.Add(voidR, false, RoomType.GrandHall, true); voidA.Void = true; voidA.Assigned = true;

            // elevator + butler room carved next to the hall on 1F before subdivision
            var elevator = CarveNextTo(f0, hall, 4, 4, rng, bands, stairwell);
            if (elevator == null) { fail = "no elevator spot"; return null; }
            elevator.Type = RoomType.Elevator; elevator.Assigned = true;

            // B1 grid
            var b1b = L.Floor(-1).Bounds; var fb = new FloorGrid(-1, (int)b1b.x0, (int)b1b.z0, (int)b1b.W, (int)b1b.D);
            fb.Add(stairwell, true, RoomType.Stairwell);
            // B1 corridors: one along the stairwell's open side, one crossing
            {
                RectI c1, c2;
                if (swAlongX)
                {
                    bool bandNorth = f0.At(stairwell.x0 + 1, stairwell.z1) >= 0 && f0.Areas[f0.At(stairwell.x0 + 1, stairwell.z1)].Passage;
                    int z = bandNorth ? stairwell.z1 : stairwell.z0 - 5;
                    c1 = new RectI((int)b1b.x0 + 2, z, (int)b1b.x1 - 2, z + 5);
                    int x = Math.Max((int)b1b.x0 + 8, Math.Min((int)b1b.x1 - 11, stairwell.x0 + rng.R(-10, 10)));
                    c2 = new RectI(x, (int)b1b.z0 + 2, x + 5, (int)b1b.z1 - 2);
                }
                else
                {
                    bool bandEast = f0.At(stairwell.x1, stairwell.z0 + 1) >= 0 && f0.Areas[f0.At(stairwell.x1, stairwell.z0 + 1)].Passage;
                    int x = bandEast ? stairwell.x1 : stairwell.x0 - 5;
                    c1 = new RectI(x, (int)b1b.z0 + 2, x + 5, (int)b1b.z1 - 2);
                    int z = Math.Max((int)b1b.z0 + 8, Math.Min((int)b1b.z1 - 11, stairwell.z0 + rng.R(-10, 10)));
                    c2 = new RectI((int)b1b.x0 + 2, z, (int)b1b.x1 - 2, z + 5);
                }
                fb.Add(c1, true, RoomType.Corridor); fb.Add(c2, true, RoomType.Corridor);
            }

            // subdivide
            // large, individual rooms: few deep leaves on 1F, generous bedrooms on 2F, then slivers are merged away
            Subdivide(f0, rng, 17, 8, 17);
            Subdivide(f1, rng, 9, 6, 9);
            Subdivide(fb, rng, 13, 6, 14);
            MergeSlivers(f0, 56, 6, 300); MergeSlivers(fb, 36, 5, 220);
            // bedrooms need many rooms on 2F; split big leaves until enough
            EnsureLeaves(f1, rng, 25, 30);
            MergeSlivers(f1, 22, 4, 120);

            // ---------- assign ----------
            var mystery = new List<RoomType> { RoomType.RainCorridor, RoomType.EmptyAuditorium, RoomType.WaitingRoom, RoomType.WhiteDoors, RoomType.MirrorWater, RoomType.ClockMuseum };
            rng.Shuffle(mystery); int mcount = rng.R(2, 5); var chosenM = mystery.Take(mcount).ToList();
            L.MysteryTypes = chosenM.Select(m => m.ToString()).ToList();
            var m0 = new List<RoomType>(); var m1 = new List<RoomType>(); var mb = new List<RoomType>();
            for (int i = 0; i < chosenM.Count; i++) { if (i % 3 == 0) m0.Add(chosenM[i]); else if (i % 3 == 1) m1.Add(chosenM[i]); else mb.Add(chosenM[i]); }

            var req0 = new List<(RoomType t, int area, RoomType partner)>
            {
                (RoomType.Pool, 230, RoomType.Corridor), (RoomType.Theater, 190, RoomType.Corridor), (RoomType.Dining, 170, RoomType.Corridor),
                (RoomType.Library, 170, RoomType.Corridor), (RoomType.Greenhouse, 160, RoomType.Corridor), (RoomType.Lounge, 150, RoomType.Corridor),
                (RoomType.MusicRoom, 110, RoomType.Corridor), (RoomType.GameRoom, 110, RoomType.Corridor), (RoomType.Workshop, 90, RoomType.Corridor),
                (RoomType.Kitchen, 80, RoomType.Dining), (RoomType.WaterRoom, 36, RoomType.Pool), (RoomType.Infirmary, 60, RoomType.Corridor),
                (RoomType.ButlerRoom, 36, RoomType.GrandHall)
            };
            foreach (var m in m0) req0.Add((m, 110, RoomType.Corridor));
            var req1 = new List<(RoomType t, int area, RoomType partner)> { (RoomType.Gallery, 120, RoomType.Corridor), (RoomType.Chapel, 100, RoomType.Corridor), (RoomType.Archive, 80, RoomType.Corridor), (RoomType.Wardrobe, 60, RoomType.Corridor), (RoomType.Darkroom, 28, RoomType.Corridor) };   // + 사진 암실 (BL23 사건 보조 공간)
            foreach (var m in m1) req1.Add((m, 90, RoomType.Corridor));
            var reqb = new List<(RoomType t, int area, RoomType partner)> { (RoomType.MachineRoom, 130, RoomType.Corridor), (RoomType.PowerRoom, 48, RoomType.MachineRoom), (RoomType.Laundry, 64, RoomType.Corridor), (RoomType.Storage, 80, RoomType.Corridor), (RoomType.Incinerator, 30, RoomType.Corridor), (RoomType.ColdStorage, 34, RoomType.Corridor) };   // + 소각실·저온 보관실 (BL23 사건 보조 공간)
            foreach (var m in mb) reqb.Add((m, 80, RoomType.Corridor));

            if (!Assign(f0, req0, rng, out fail)) return null;
            // bedrooms first on 2F (by size window), then the rest
            if (!AssignBedrooms(f1, rng, out fail)) return null;
            if (!Assign(f1, req1, rng, out fail)) return null;
            if (!Assign(fb, reqb, rng, out fail)) return null;
            AssignLeftovers(f0, rng, new[] { (RoomType.Study, 1), (RoomType.TeaRoom, 1), (RoomType.Courtyard, 1), (RoomType.DollRoom, 1), (RoomType.TrophyRoom, 1), (RoomType.Parlor, 1) }, RoomType.Parlor);
            AssignLeftovers(f1, rng, new[] { (RoomType.Study, 1), (RoomType.DollRoom, 1), (RoomType.TeaRoom, 1), (RoomType.TrophyRoom, 1), (RoomType.GuestRoom, 2), (RoomType.Parlor, 1) }, RoomType.GuestRoom);
            AssignLeftovers(fb, rng, new[] { (RoomType.WineCellar, 1), (RoomType.BoilerRoom, 1), (RoomType.Storage, 1) }, RoomType.Closet);

            // ---------- emit rooms ----------
            var map = new Dictionary<(int f, int a), int>();
            void Emit(FloorGrid fg)
            {
                foreach (var a in fg.Areas)
                {
                    if (a.Dead) continue;
                    var r = new Room { Id = L.Rooms.Count, Type = a.Type, Floor = fg.F, Rect = a.R.F(), Void = a.Void };
                    r.CeilingH = fg.F == 0 ? (a.Type == RoomType.GrandHall ? 9.0f : 4.4f) : fg.F == 1 ? 3.8f : 3.8f;
                    if (fg.F == 1 && a.Type == RoomType.GrandHall) r.CeilingH = 4.2f;
                    r.Name = RoomInfo.Kor(r.Type);
                    L.Rooms.Add(r); map[(fg.F, a.Id)] = r.Id;
                }
            }
            Emit(f0); Emit(f1); Emit(fb);
            // courtroom
            var court = new Room { Id = L.Rooms.Count, Type = RoomType.Courtroom, Floor = -2, Rect = new RectF(20, 14, 40, 34), CeilingH = 10.5f, Name = RoomInfo.Kor(RoomType.Courtroom) };
            L.Rooms.Add(court);
            // corridor naming & bedroom owners
            AssignOwners(L, rng);
            NameRooms(L);

            // ---------- doors ----------
            MakeDoors(L, f0, map, rng); MakeDoors(L, f1, map, rng); MakeDoors(L, fb, map, rng);

            // ---------- stairs ----------
            var hallRoom = L.Rooms[map[(0, hall0.Id)]]; var landRoom = L.Rooms[map[(1, landing.Id)]];
            // grand stair against the hall side facing away from the elevator; rises toward the landing
            {
                var er = elevator.R; bool elevNorth = er.z0 >= hall.z1 - 1;
                float sx = hall.x0 + hall.W / 2f;
                float zBottom = elevNorth ? hall.z0 + 11.5f : hall.z1 - 11.5f, zTop = elevNorth ? hall.z0 + 1.5f : hall.z1 - 1.5f;
                var st = new Stair { Id = L.Stairs.Count, Name = "대계단", A = new P3(0, sx, zBottom), B = new P3(1, sx, zTop), RoomA = hallRoom.Id, RoomB = landRoom.Id, Seconds = 7f, Grand = true };
                L.Stairs.Add(st);
                L.Furniture.Add(new Furniture { Id = L.Furniture.Count, Room = hallRoom.Id, Type = "GrandStair", Pos = new P3(0, sx, (zBottom + zTop) / 2f - (elevNorth ? 0.8f : -0.8f)), Yaw = elevNorth ? 180 : 0, W = 5f, D = 8.5f, H = 4.8f, Material = Mat.Stone });
                hallRoom.Furniture.Add(L.Furniture.Count - 1);
            }
            {
                var sw0 = L.Rooms.First(r => r.Floor == 0 && r.Type == RoomType.Stairwell); var sw1 = L.Rooms.First(r => r.Floor == 1 && r.Type == RoomType.Stairwell); var swb = L.Rooms.First(r => r.Floor == -1 && r.Type == RoomType.Stairwell);
                var R = sw0.Rect; bool longX = R.W > R.D;
                // a wide well holds the two flights side by side (up in one lane, down in the other); a narrow one stacks them
                float across = longX ? R.D : R.W, lane = across >= 5.5f ? across * 0.25f : 0f;
                P3 p1(int f, float o) => longX ? new P3(f, R.x0 + 1.2f, R.CZ + o) : new P3(f, R.CX + o, R.z0 + 1.2f);
                P3 p2(int f, float o) => longX ? new P3(f, R.x1 - 1.2f, R.CZ + o) : new P3(f, R.CX + o, R.z1 - 1.2f);
                L.Stairs.Add(new Stair { Id = L.Stairs.Count, Name = "서비스 계단(상)", A = p1(0, lane), B = p2(1, lane), RoomA = sw0.Id, RoomB = sw1.Id, Seconds = 8f });
                L.Stairs.Add(new Stair { Id = L.Stairs.Count, Name = "서비스 계단(하)", A = p2(0, -lane), B = p1(-1, -lane), RoomA = sw0.Id, RoomB = swb.Id, Seconds = 8f });
            }
            {
                var el = L.Rooms.First(r => r.Type == RoomType.Elevator);
                L.Stairs.Add(new Stair { Id = L.Stairs.Count, Name = "심판장 승강기", A = new P3(0, el.Rect.CX, el.Rect.CZ), B = new P3(-2, 30, 17.5f), RoomA = el.Id, RoomB = court.Id, Seconds = 20f });
            }

            // ---------- circuits ----------
            MakeCircuits(L, hall);
            // ---------- palettes / variants / furniture ----------
            var props = rng; // layout stream keeps dressing deterministic with the structure
            foreach (var r in L.Rooms) { r.Variant = rng.R(4); r.MysteryGimmick = rng.R(3); r.Palette = Palettes.For(r.Type, rng); r.BaseLight = Palettes.BaseLight(r.Type); r.Exterior = r.Floor >= 0 && (r.Rect.x0 <= 0.01f || r.Rect.z0 <= 0.01f || r.Rect.x1 >= W - 0.01f || r.Rect.z1 >= D - 0.01f); }
            Decorator.Furnish(L, props);
            ItemPlacement.Settle(L, props);   // items onto tables, counters, workbenches, pedestals — floor things against walls
            return L;
        }

        static bool Overlap(RectI a, RectI b) => a.x0 < b.x1 && b.x0 < a.x1 && a.z0 < b.z1 && b.z0 < a.z1;

        static Area CarveNextTo(FloorGrid g, RectI hall, int w, int d, Rng rng, List<RectI> bands, RectI stairwell)
        {
            var cands = new List<RectI>();
            for (int x = hall.x0 + 1; x <= hall.x1 - w - 1; x++) { cands.Add(new RectI(x, hall.z1, x + w, hall.z1 + d)); cands.Add(new RectI(x, hall.z0 - d, x + w, hall.z0)); }
            for (int z = hall.z0 + 1; z <= hall.z1 - w - 1; z++) { cands.Add(new RectI(hall.x1, z, hall.x1 + d, z + w)); cands.Add(new RectI(hall.x0 - d, z, hall.x0, z + w)); }
            rng.Shuffle(cands);
            foreach (var c in cands) if (g.Free(c)) { var a = g.Add(c, false, RoomType.Elevator); return a; }
            return null;
        }

        // Recursive zone subdivision: every leaf keeps a passage edge (slice perpendicular to a touching side),
        // and zones too deep for rooms get an internal corridor carved from the passage.
        static void Subdivide(FloorGrid g, Rng rng, int maxDepth, int minW, int maxW)
        {
            // decompose empty space into big rectangles: repeatedly take the largest empty rectangle
            var used = new bool[g.NX, g.NZ];
            var zones = new List<RectI>();
            var h = new int[g.NX];
            for (int guard = 0; guard < 200; guard++)
            {
                int bestA = 0; RectI best = default;
                Array.Clear(h, 0, h.Length);
                for (int j = 0; j < g.NZ; j++)
                {
                    for (int i = 0; i < g.NX; i++) h[i] = (g.G[i, j] == -1 && !used[i, j]) ? h[i] + 1 : 0;
                    // largest rectangle in histogram
                    var st = new Stack<int>();
                    for (int i = 0; i <= g.NX; i++)
                    {
                        int cur = i == g.NX ? 0 : h[i];
                        while (st.Count > 0 && h[st.Peek()] >= cur)
                        {
                            int top = st.Pop(); int height = h[top]; int left = st.Count == 0 ? 0 : st.Peek() + 1; int width = i - left;
                            int w2 = width, d2 = height;
                            if (Math.Min(w2, d2) >= 3)
                            {
                                // favour room-friendly proportions over long strips
                                double score = w2 * d2 * (Math.Max(w2, d2) > 3.5 * Math.Min(w2, d2) ? 0.6 : 1.0);
                                if (score > bestA) { bestA = (int)score; best = new RectI(g.X0 + left, g.Z0 + j - height + 1, g.X0 + left + width, g.Z0 + j + 1); }
                            }
                        }
                        st.Push(i);
                    }
                }
                if (bestA <= 0) break;
                for (int x = best.x0; x < best.x1; x++) for (int z = best.z0; z < best.z1; z++) used[x - g.X0, z - g.Z0] = true;
                zones.Add(best);
            }
            foreach (var z in zones) Divide(g, z, rng, maxDepth, minW, maxW, 0);
        }

        static bool IsPassageId(FloorGrid g, int id) => id >= 0 && g.Areas[id].Passage && !g.Areas[id].Void;

        static void Divide(FloorGrid g, RectI r, Rng rng, int maxDepth, int minW, int maxW, int depth)
        {
            if (r.W < 3 || r.D < 3) return; // slivers stay solid wall mass
            var touches = new List<(int side, int len)>();
            for (int s = 0; s < 4; s++) { g.Touch(r, s, id => IsPassageId(g, id), out _, out int bl); if (bl >= 2) touches.Add((s, bl)); }
            if (touches.Count == 0)
            {
                // isolated interior piece: becomes a suite room (door to neighbour) — kept small
                g.Add(r, false, RoomType.Parlor); return;
            }
            var pick = rng.Weighted(touches, t => t.len);
            int side = pick.side;
            int breadth = side < 2 ? r.W : r.D, deep = side < 2 ? r.D : r.W;
            if (deep > maxDepth && breadth >= 2 * minW + 3 && depth < 6)
            {
                // carve a corridor perpendicular to the touching side
                int pos = rng.R(minW, breadth - minW - 3 + 1);
                RectI corr, a, b;
                if (side < 2) { corr = new RectI(r.x0 + pos, r.z0, r.x0 + pos + 3, r.z1); a = new RectI(r.x0, r.z0, r.x0 + pos, r.z1); b = new RectI(r.x0 + pos + 3, r.z0, r.x1, r.z1); }
                else { corr = new RectI(r.x0, r.z0 + pos, r.x1, r.z0 + pos + 3); a = new RectI(r.x0, r.z0, r.x1, r.z0 + pos); b = new RectI(r.x0, r.z0 + pos + 3, r.x1, r.z1); }
                // stop the carved corridor early sometimes (dead-end alcove feels more mansion-like)
                g.Add(corr, true, RoomType.Corridor);
                Divide(g, a, rng, maxDepth, minW, maxW, depth + 1); Divide(g, b, rng, maxDepth, minW, maxW, depth + 1);
                return;
            }
            if (deep > maxDepth + 4 && breadth < 2 * minW + 3)
            {
                // narrow and very deep: split parallel; far piece becomes a back room (suite)
                int cut = rng.R(Math.Max(4, deep / 2 - 2), Math.Min(deep - 3, deep / 2 + 3));
                RectI front, back;
                if (side == 0) { front = new RectI(r.x0, r.z0, r.x1, r.z0 + cut); back = new RectI(r.x0, r.z0 + cut, r.x1, r.z1); }
                else if (side == 1) { front = new RectI(r.x0, r.z1 - cut, r.x1, r.z1); back = new RectI(r.x0, r.z0, r.x1, r.z1 - cut); }
                else if (side == 2) { front = new RectI(r.x0, r.z0, r.x0 + cut, r.z1); back = new RectI(r.x0 + cut, r.z0, r.x1, r.z1); }
                else { front = new RectI(r.x1 - cut, r.z0, r.x1, r.z1); back = new RectI(r.x0, r.z0, r.x1 - cut, r.z1); }
                g.Add(front, false, RoomType.Parlor); Divide(g, back, rng, maxDepth, minW, maxW, depth + 1); return;
            }
            int target = rng.R(minW, maxW + 1);
            if (breadth <= target || breadth < 2 * minW) { g.Add(r, false, RoomType.Parlor); return; }
            int cutAt = rng.R(minW, breadth - minW + 1);
            if (breadth - cutAt < minW) cutAt = breadth - minW;
            RectI p, q;
            if (side < 2) { p = new RectI(r.x0, r.z0, r.x0 + cutAt, r.z1); q = new RectI(r.x0 + cutAt, r.z0, r.x1, r.z1); }
            else { p = new RectI(r.x0, r.z0, r.x1, r.z0 + cutAt); q = new RectI(r.x0, r.z0 + cutAt, r.x1, r.z1); }
            Divide(g, p, rng, maxDepth, minW, maxW, depth + 1); Divide(g, q, rng, maxDepth, minW, maxW, depth + 1);
        }

        /// <summary>Absorb undersized or skinny leaves into a neighbouring leaf when the union stays a clean rectangle,
        /// so the floor reads as a few generous rooms instead of a warren of closets.</summary>
        static void MergeSlivers(FloorGrid g, int minArea, int minDim, int maxArea)
        {
            bool Union(RectI a, RectI b, out RectI u)
            {
                u = default;
                if (a.x0 == b.x0 && a.x1 == b.x1 && (a.z1 == b.z0 || b.z1 == a.z0)) { u = new RectI(a.x0, Math.Min(a.z0, b.z0), a.x1, Math.Max(a.z1, b.z1)); return true; }
                if (a.z0 == b.z0 && a.z1 == b.z1 && (a.x1 == b.x0 || b.x1 == a.x0)) { u = new RectI(Math.Min(a.x0, b.x0), a.z0, Math.Max(a.x1, b.x1), a.z1); return true; }
                return false;
            }
            for (int guard = 0; guard < 400; guard++)
            {
                bool merged = false;
                foreach (var a in g.Areas.Where(x => !x.Passage && !x.Void && !x.Assigned && (x.R.Area < minArea || Math.Min(x.R.W, x.R.D) < minDim)).OrderBy(x => x.R.Area).ToList())
                {
                    Area best = null; RectI bu = default;
                    foreach (var b in g.Areas)
                    {
                        if (b == a || b.Passage || b.Void || b.Assigned) continue;
                        if (!Union(a.R, b.R, out var u) || u.Area > maxArea) continue;
                        if (Math.Max(u.W, u.D) > 3.2 * Math.Min(u.W, u.D)) continue;
                        if (best == null || b.R.Area < best.R.Area) { best = b; bu = u; }
                    }
                    if (best == null) continue;
                    for (int x = a.R.x0; x < a.R.x1; x++) for (int z = a.R.z0; z < a.R.z1; z++) if (g.In(x, z) && g.G[x - g.X0, z - g.Z0] == a.Id) g.G[x - g.X0, z - g.Z0] = best.Id;
                    best.R = bu; a.Void = a.Dead = a.Assigned = true;
                    merged = true; break;
                }
                if (!merged) return;
            }
        }

        static void EnsureLeaves(FloorGrid g, Rng rng, int want, int minArea)
        {
            for (int guard = 0; guard < 40; guard++)
            {
                var leaves = g.Areas.Where(a => !a.Passage && !a.Void && a.R.Area >= minArea).ToList();
                if (leaves.Count >= want) return;
                var big = leaves.OrderByDescending(a => a.R.Area).FirstOrDefault(a => a.R.Area >= 2 * minArea + 4 && Math.Max(a.R.W, a.R.D) >= 9);
                if (big == null) return;
                // split along longer axis
                var r = big.R; RectI p, q;
                if (r.W >= r.D) { int c = r.W / 2; p = new RectI(r.x0, r.z0, r.x0 + c, r.z1); q = new RectI(r.x0 + c, r.z0, r.x1, r.z1); }
                else { int c = r.D / 2; p = new RectI(r.x0, r.z0, r.x1, r.z0 + c); q = new RectI(r.x0, r.z0 + c, r.x1, r.z1); }
                big.R = p; var qa = g.Add(q, false, RoomType.Parlor, true);
            }
        }

        static bool Assign(FloorGrid g, List<(RoomType t, int area, RoomType partner)> req, Rng rng, out string fail)
        {
            fail = null;
            foreach (var (t, area, partner) in req.OrderByDescending(r => r.area))
            {
                var free = g.Areas.Where(a => !a.Passage && !a.Assigned && !a.Void && a.R.W >= 3 && a.R.D >= 3).ToList();
                if (free.Count == 0) { fail = $"no room for {t} on F{g.F}"; return false; }
                Area best = null; double bestScore = double.MaxValue;
                foreach (var a in free)
                {
                    double s = Math.Abs(a.R.Area - area) / (double)area;
                    double aspect = Math.Max(a.R.W, a.R.D) / (double)Math.Min(a.R.W, a.R.D); if (aspect > 2.6) s += (aspect - 2.6) * 0.5;
                    if (partner != RoomType.Corridor)
                    {
                        bool adj = g.Areas.Any(o => o.Assigned && o.Type == partner && Adjacent(a.R, o.R, 2));
                        if (!adj && partner == RoomType.GrandHall) adj = g.Areas.Any(o => o.Passage && o.Type == RoomType.GrandHall && Adjacent(a.R, o.R, 2));
                        s += adj ? -0.6 : 0.8;
                    }
                    if (t == RoomType.Pool && a.R.Area < 110) s += 2;
                    if (RoomInfo.IsMystery(t) && a.R.Area < 36) s += 2;
                    if (a.R.Area < area * 0.6) s += 1.5;                         // undersized: a function room must not get a scrap
                    if (a.R.Area > area * 1.7 && CanSplit(g, a, area, out _, out _)) s -= 0.3; // oversized but splittable: fine
                    s += rng.F() * 0.15;
                    if (s < bestScore) { bestScore = s; best = a; }
                }
                if (best == null) { fail = $"no candidate for {t}"; return false; }
                // an oversized leaf is cut to the requested size; the remainder stays a free leaf for other rooms
                if (best.R.Area > area * 1.7 && CanSplit(g, best, area, out var keep, out var rest))
                {
                    best.R = keep; g.Add(rest, false, RoomType.Parlor, true);
                }
                best.Type = t; best.Assigned = true;
            }
            return true;
        }

        /// <summary>Cut a leaf perpendicular to its passage-facing side so both parts keep a passage edge.</summary>
        static bool CanSplit(FloorGrid g, Area a, int area, out RectI keep, out RectI rest)
        {
            keep = rest = default; var r = a.R;
            int bestSide = -1, bestLen = 0;
            for (int s = 0; s < 4; s++) { g.Touch(r, s, id => IsPassageId(g, id), out _, out int bl); if (bl > bestLen) { bestLen = bl; bestSide = s; } }
            if (bestSide < 0) return false;
            int breadth = bestSide < 2 ? r.W : r.D, deep = bestSide < 2 ? r.D : r.W;
            int cut = Math.Max(5, (int)Math.Round(area / (double)Math.Max(1, deep)));
            if (cut < 5 || breadth - cut < 5) return false;
            RectI p, q;
            if (bestSide < 2) { p = new RectI(r.x0, r.z0, r.x0 + cut, r.z1); q = new RectI(r.x0 + cut, r.z0, r.x1, r.z1); }
            else { p = new RectI(r.x0, r.z0, r.x1, r.z0 + cut); q = new RectI(r.x0, r.z0 + cut, r.x1, r.z1); }
            g.Touch(p, bestSide, id => IsPassageId(g, id), out _, out int lp); g.Touch(q, bestSide, id => IsPassageId(g, id), out _, out int lq);
            if (lp >= 2 && lq >= 2) { keep = p; rest = q; return true; }
            // try the other end
            if (bestSide < 2) { p = new RectI(r.x1 - cut, r.z0, r.x1, r.z1); q = new RectI(r.x0, r.z0, r.x1 - cut, r.z1); }
            else { p = new RectI(r.x0, r.z1 - cut, r.x1, r.z1); q = new RectI(r.x0, r.z0, r.x1, r.z1 - cut); }
            g.Touch(p, bestSide, id => IsPassageId(g, id), out _, out lp); g.Touch(q, bestSide, id => IsPassageId(g, id), out _, out lq);
            if (lp >= 2 && lq >= 2) { keep = p; rest = q; return true; }
            return false;
        }

        static void AssignLeftovers(FloorGrid g, Rng rng, (RoomType t, int max)[] pool, RoomType fallback)
        {
            var left = g.Areas.Where(a => !a.Passage && !a.Assigned && !a.Void).ToList(); rng.Shuffle(left);
            var counts = new Dictionary<RoomType, int>();
            foreach (var a in left.OrderByDescending(a => a.R.Area))
            {
                if (a.R.Area < 16) { a.Type = RoomType.Closet; a.Assigned = true; continue; }
                var opts = pool.Where(p => (counts.TryGetValue(p.t, out var c) ? c : 0) < p.max && (p.t != RoomType.Courtyard || a.R.Area >= 40)).ToList();
                RoomType t = opts.Count > 0 ? rng.Pick(opts).t : fallback;
                counts[t] = (counts.TryGetValue(t, out var cc) ? cc : 0) + 1;
                a.Type = t; a.Assigned = true;
            }
        }

        static bool Adjacent(RectI a, RectI b, int minShared)
        {
            if (a.x1 == b.x0 || b.x1 == a.x0) { int s = Math.Min(a.z1, b.z1) - Math.Max(a.z0, b.z0); return s >= minShared; }
            if (a.z1 == b.z0 || b.z1 == a.z0) { int s = Math.Min(a.x1, b.x1) - Math.Max(a.x0, b.x0); return s >= minShared; }
            return false;
        }

        static bool AssignBedrooms(FloorGrid g, Rng rng, out string fail)
        {
            fail = null;
            var cands = g.Areas.Where(a => !a.Passage && !a.Assigned && !a.Void && a.R.Area >= 20 && a.R.W >= 4 && a.R.D >= 4 && a.R.Area <= 60).OrderBy(a => Math.Abs(a.R.Area - 32)).ToList();
            if (cands.Count < 18)
            {
                var more = g.Areas.Where(a => !a.Passage && !a.Assigned && !a.Void && a.R.Area > 60 && a.R.W >= 4 && a.R.D >= 4).OrderBy(a => a.R.Area).ToList();
                cands.AddRange(more);
            }
            if (cands.Count < 18) { fail = $"only {cands.Count} bedroom candidates"; return false; }
            foreach (var a in cands.Take(18)) { a.Type = RoomType.Bedroom; a.Assigned = true; }
            return true;
        }

        static void AssignOwners(Layout L, Rng rng)
        {
            var beds = L.Rooms.Where(r => r.Type == RoomType.Bedroom).ToList();
            var hall = L.Rooms.First(r => r.Type == RoomType.GrandHall && r.Floor == 0);
            // wings: sort west->east so CH02 two-wing split is spatially meaningful
            beds = beds.OrderBy(b => b.Rect.CX).ThenBy(b => b.Rect.CZ).ToList();
            var people = Cast.Participants.Select(c => c.Id).ToList(); rng.Shuffle(people);
            for (int i = 0; i < beds.Count && i < people.Count; i++) { beds[i].Owner = people[i]; beds[i].Name = Cast.NameOf(people[i]) + "의 방"; }
            var butler = L.Rooms.FirstOrDefault(r => r.Type == RoomType.ButlerRoom); if (butler != null) butler.Owner = Cast.Butler;
        }

        static void NameRooms(Layout L)
        {
            var hall = L.Rooms.First(r => r.Type == RoomType.GrandHall && r.Floor == 0);
            int n = 0;
            foreach (var r in L.Rooms)
            {
                if (r.Type == RoomType.Corridor)
                {
                    string dir = r.Rect.CZ > hall.Rect.z1 ? "북" : r.Rect.CZ < hall.Rect.z0 ? "남" : r.Rect.CX < hall.Rect.CX ? "서" : "동";
                    string fl = r.Floor == 0 ? "1층" : r.Floor == 1 ? "2층" : "지하";
                    r.Name = $"{fl} {dir}쪽 복도"; n++;
                }
                if (r.Type == RoomType.Parlor) r.Name = r.Floor == 1 ? "빈 객실" : "작은 응접실";
                if (r.Type == RoomType.GrandHall && r.Floor == 1) r.Name = "대현관 홀 (위)";
            }
            // disambiguate duplicate names the way people would say it: the floor first ("2층 계단실"), then where it lies
            // (a corridor: "2층 북쪽 복도 서쪽 끝"; a room: "북쪽 창고"); a number only when four or more still share a name
            // ("빈 객실 3번"), never a debug-like "복도 D". No brackets: particles after a name follow its last syllable.
            foreach (var grp in L.Rooms.GroupBy(r => r.Name).Where(g => g.Count() > 1).ToList())
                if (grp.Select(r => r.Floor).Distinct().Count() > 1 && !grp.Key.Contains("층") && !grp.Key.StartsWith("지하"))
                    foreach (var r in grp) r.Name = FloorWord(r.Floor) + " " + r.Name;
            foreach (var grp in L.Rooms.GroupBy(r => r.Name).Where(g => g.Count() > 1).ToList())
            {
                var list = grp.ToList();
                bool alongX = list.Max(r => r.Rect.CX) - list.Min(r => r.Rect.CX) >= list.Max(r => r.Rect.CZ) - list.Min(r => r.Rect.CZ);
                list = alongX ? list.OrderBy(r => r.Rect.CX).ThenBy(r => r.Rect.CZ).ToList() : list.OrderBy(r => r.Rect.CZ).ThenBy(r => r.Rect.CX).ToList();
                string[] pos = list.Count == 2 ? (alongX ? new[] { "서쪽", "동쪽" } : new[] { "남쪽", "북쪽" })
                             : list.Count == 3 ? (alongX ? new[] { "서쪽", "가운데", "동쪽" } : new[] { "남쪽", "가운데", "북쪽" }) : null;
                bool passage = RoomInfo.IsPassage(list[0].Type);
                for (int k = 0; k < list.Count; k++)
                    list[k].Name = pos == null ? $"{list[k].Name} {k + 1}번"
                                 : passage ? $"{list[k].Name} {pos[k]}{(pos[k] == "가운데" ? "" : " 끝")}"
                                 : pos[k] == "가운데" ? $"가운데 {list[k].Name}" : $"{pos[k]} {list[k].Name}";
                // "북쪽 복도 북쪽 끝" → "북쪽 복도 끝"; the end toward the hall → "북쪽 복도 안쪽"
                foreach (var (d, o) in new[] { ("북", "남"), ("남", "북"), ("동", "서"), ("서", "동") })
                    foreach (var r in list) r.Name = r.Name.Replace($"{d}쪽 복도 {d}쪽 끝", $"{d}쪽 복도 끝").Replace($"{d}쪽 복도 {o}쪽 끝", $"{d}쪽 복도 안쪽");
            }
        }

        static string FloorWord(int f) => f == 0 ? "1층" : f > 0 ? $"{f + 1}층" : f == -1 ? "지하" : $"지하 {-f}층";

        static void MakeDoors(Layout L, FloorGrid g, Dictionary<(int f, int a), int> map, Rng rng)
        {
            var wide = new HashSet<RoomType> { RoomType.Dining, RoomType.Theater, RoomType.Pool, RoomType.Greenhouse, RoomType.Library, RoomType.Chapel, RoomType.Gallery, RoomType.Lounge, RoomType.MachineRoom };
            foreach (var a in g.Areas)
            {
                if (a.Passage || a.Void) continue;
                var room = L.Rooms[map[(g.F, a.Id)]];
                // candidate sides touching passages
                var sides = new List<(int side, int start, int len, int passId)>();
                for (int s = 0; s < 4; s++)
                {
                    int total = g.Touch(a.R, s, id => IsPassageId(g, id), out int bs, out int bl);
                    if (bl >= 2)
                    {
                        int x, z; SideCell(a.R, s, bs + bl / 2, out x, out z);
                        sides.Add((s, bs, bl, g.At(x, z)));
                    }
                }
                sides = sides.OrderByDescending(s => s.len).ToList();
                int doorsWanted = 1;
                if (a.R.Area >= 90 && sides.Count > 1) doorsWanted = 2;
                if (room.Type == RoomType.Bedroom || room.Type == RoomType.Closet || room.Type == RoomType.WaterRoom || room.Type == RoomType.PowerRoom || room.Type == RoomType.Elevator) doorsWanted = 1;
                if (room.Type == RoomType.Elevator) sides = sides.Where(s => g.Areas[s.passId].Type == RoomType.GrandHall).Concat(sides).ToList();
                int made = 0; var usedPass = new HashSet<int>();
                foreach (var s in sides)
                {
                    if (made >= doorsWanted) break;
                    if (usedPass.Contains(s.passId) && made > 0) continue;
                    float width = wide.Contains(room.Type) ? 3.0f : room.Type == RoomType.Bedroom || room.Type == RoomType.Closet || room.Type == RoomType.PowerRoom || room.Type == RoomType.WaterRoom || room.Type == RoomType.ButlerRoom ? 1.5f : 2.5f;
                    if (room.Type == RoomType.Elevator) width = 2.0f;
                    while (width > 1.5f && s.len < width + 1.0f) width -= 0.5f;
                    if (s.len < 2) continue;
                    float margin = 0.25f + width / 2f; float lo = s.start + margin, hi = s.start + s.len - margin;
                    float t = hi > lo ? lo + rng.F() * (hi - lo) : s.start + s.len / 2f;
                    t = DoorSnap(t, width);
                    var d = MakeDoor(L, g.F, a.R, s.side, t, width, room.Id, map[(g.F, s.passId)]);
                    Configure(L, d, room);
                    usedPass.Add(s.passId); made++;
                }
                if (made == 0)
                {
                    // interior piece: connect to largest adjacent non-bedroom room
                    var nb = g.Areas.Where(o => o != a && !o.Passage && !o.Void && Adjacent(a.R, o.R, 2)).OrderBy(o => o.Type == RoomType.Bedroom ? 1 : 0).ThenByDescending(o => o.R.Area).FirstOrDefault();
                    if (nb != null) SuiteDoor(L, g, a, nb, map, rng, 1.3f);
                }
            }
            // suites and interior connections
            var pairs = new[] { (RoomType.Kitchen, RoomType.Dining), (RoomType.WaterRoom, RoomType.Pool), (RoomType.PowerRoom, RoomType.MachineRoom), (RoomType.Wardrobe, RoomType.Theater), (RoomType.Infirmary, RoomType.Corridor) };
            foreach (var (x, y) in pairs)
            {
                var ax = g.Areas.FirstOrDefault(o => !o.Passage && o.Type == x); var ay = g.Areas.FirstOrDefault(o => !o.Passage && o.Type == y);
                if (ax != null && ay != null && Adjacent(ax.R, ay.R, 2)) SuiteDoor(L, g, ax, ay, map, rng, x == RoomType.Kitchen ? 1.6f : 1.3f);
            }
            var nonBed = g.Areas.Where(o => !o.Passage && !o.Void && o.Type != RoomType.Bedroom && o.Type != RoomType.Elevator && o.Type != RoomType.PowerRoom).ToList();
            for (int i = 0; i < nonBed.Count; i++) for (int j = i + 1; j < nonBed.Count; j++)
                    if (Adjacent(nonBed[i].R, nonBed[j].R, 3) && rng.Chance(nonBed[i].R.Area + nonBed[j].R.Area > 150 ? 0.5 : 0.3)) SuiteDoor(L, g, nonBed[i], nonBed[j], map, rng, 1.2f);
        }

        /// <summary>Door centers sit on the 0.5m lattice so the opening covers whole nav cells: 1.5m = 3 cells, 2.0m = 4 cells.</summary>
        static float DoorSnap(float t, float width)
        {
            // odd cell counts (1.5 m = 3, 2.5 m = 5) centre on a quarter; even ones (2.0, 3.0) on the half-metre lattice
            int cells = (int)Math.Round(width / 0.5f);
            if (cells % 2 == 1) return (float)Math.Floor(t * 2f) / 2f + 0.25f;
            return (float)Math.Round(t * 2f) / 2f;
        }

        static void SideCell(RectI r, int side, int t, out int x, out int z)
        {
            if (side == 0) { x = r.x0 + t; z = r.z0 - 1; } else if (side == 1) { x = r.x0 + t; z = r.z1; } else if (side == 2) { x = r.x0 - 1; z = r.z0 + t; } else { x = r.x1; z = r.z0 + t; }
        }

        static Door MakeDoor(Layout L, int f, RectI r, int side, float t, float width, int roomA, int roomB)
        {
            var d = new Door { Id = L.Doors.Count, RoomA = roomA, RoomB = roomB, Width = width };
            if (side == 0) { d.Pos = new P3(f, r.x0 + t, r.z0); d.AlongX = true; }
            else if (side == 1) { d.Pos = new P3(f, r.x0 + t, r.z1); d.AlongX = true; }
            else if (side == 2) { d.Pos = new P3(f, r.x0, r.z0 + t); d.AlongX = false; }
            else { d.Pos = new P3(f, r.x1, r.z0 + t); d.AlongX = false; }
            L.Doors.Add(d); L.Rooms[roomA].Doors.Add(d.Id); L.Rooms[roomB].Doors.Add(d.Id);
            return d;
        }

        static void SuiteDoor(Layout L, FloorGrid g, Area a, Area b, Dictionary<(int f, int a), int> map, Rng rng, float width)
        {
            int ra = map[(g.F, a.Id)], rb = map[(g.F, b.Id)];
            if (L.Doors.Any(d => (d.RoomA == ra && d.RoomB == rb) || (d.RoomA == rb && d.RoomB == ra))) return;
            RectI A = a.R, B = b.R; int side; int s0, s1;
            if (A.x1 == B.x0) { side = 3; s0 = Math.Max(A.z0, B.z0); s1 = Math.Min(A.z1, B.z1); }
            else if (B.x1 == A.x0) { side = 2; s0 = Math.Max(A.z0, B.z0); s1 = Math.Min(A.z1, B.z1); }
            else if (A.z1 == B.z0) { side = 1; s0 = Math.Max(A.x0, B.x0); s1 = Math.Min(A.x1, B.x1); }
            else { side = 0; s0 = Math.Max(A.x0, B.x0); s1 = Math.Min(A.x1, B.x1); }
            int len = s1 - s0; if (len < 2) return;
            width = len >= 5 ? 2.5f : len >= 3 ? 2.0f : 1.5f;
            float mid = s0 + len / 2f + (len > 4 ? rng.Range(-(len / 2f - 1.5f), len / 2f - 1.5f) : 0);
            mid = DoorSnap(mid, width);
            mid = Math.Max(s0 + width / 2f, Math.Min(s1 - width / 2f, mid));
            float t = side < 2 ? mid - A.x0 : mid - A.z0;
            var d = MakeDoor(L, g.F, A, side, t, width, ra, rb);
            Configure(L, d, L.Rooms[ra]); Configure(L, d, L.Rooms[rb]);
        }

        static void Configure(Layout L, Door d, Room room)
        {
            if (room.Type == RoomType.Bedroom) { d.Lockable = true; d.KeyId = "key_" + room.Owner; }
            if (RoomInfo.NightLocked(room.Type)) { d.Lockable = true; d.NightPolicy = true; if (d.KeyId == null) d.KeyId = "key_master"; }
            if (room.Type == RoomType.Elevator) { d.Lockable = true; d.Locked = true; d.KeyId = "key_butler"; }
            if (room.Type == RoomType.PowerRoom || room.Type == RoomType.MachineRoom || room.Type == RoomType.Archive || room.Type == RoomType.Infirmary || room.Type == RoomType.Storage || room.Type == RoomType.WaterRoom || room.Type == RoomType.ButlerRoom)
            { d.Lockable = true; if (d.KeyId == null) d.KeyId = "key_master"; }
            if (room.Type == RoomType.ButlerRoom) { d.Locked = true; d.KeyId = "key_butler"; }
        }

        static void MakeCircuits(Layout L, RectI hall)
        {
            var c = new List<Circuit>
            {
                new Circuit { Id = 0, Name = "중앙 홀·복도", Emergency = true },
                new Circuit { Id = 1, Name = "1층 서쪽" }, new Circuit { Id = 2, Name = "1층 동쪽" },
                new Circuit { Id = 3, Name = "2층 서쪽" }, new Circuit { Id = 4, Name = "2층 동쪽" },
                new Circuit { Id = 5, Name = "지하 설비" }, new Circuit { Id = 6, Name = "수영장·수질" }, new Circuit { Id = 7, Name = "기계 동력" }
            };
            float cx = hall.x0 + hall.W / 2f;
            foreach (var r in L.Rooms)
            {
                int id;
                if (r.Type == RoomType.Courtroom) continue;
                if (RoomInfo.IsPassage(r.Type)) id = 0;
                else if (r.Type == RoomType.Pool || r.Type == RoomType.WaterRoom) id = 6;
                else if (r.Floor == -1) id = 5;
                else if (r.Floor == 0) id = r.Rect.CX < cx ? 1 : 2;
                else id = r.Rect.CX < cx ? 3 : 4;
                r.Circuit = id; c[id].Rooms.Add(r.Id);
            }
            var mr = L.Rooms.FirstOrDefault(r => r.Type == RoomType.MachineRoom); if (mr != null) c[7].Rooms.Add(mr.Id);
            L.Circuits = c;
        }

        /// <summary>Access checks the design requires before a loop can start.</summary>
        public static string Validate(Layout L)
        {
            foreach (var t in new[] { RoomType.Dining, RoomType.Kitchen, RoomType.Infirmary, RoomType.PowerRoom, RoomType.MachineRoom, RoomType.Pool, RoomType.Elevator, RoomType.Library })
                if (!L.Rooms.Any(r => r.Type == t)) return "missing " + t;
            if (L.Rooms.Count(r => r.Type == RoomType.Bedroom && r.Owner != null) < 18) return "bedrooms<18";
            var hall = L.Rooms.First(r => r.Type == RoomType.GrandHall && r.Floor == 0);
            var from = L.Stairs.First(s => s.Grand).A;
            Func<int, float> anyDoor = d => 0f;
            var targets = new List<Room>();
            targets.AddRange(L.Rooms.Where(r => r.Type == RoomType.Bedroom));
            targets.AddRange(L.Rooms.Where(r => r.Type == RoomType.Dining || r.Type == RoomType.Infirmary || r.Type == RoomType.Kitchen || r.Type == RoomType.PowerRoom || r.Type == RoomType.MachineRoom || r.Type == RoomType.Pool || RoomInfo.IsMystery(r.Type)));
            foreach (var t in targets)
            {
                var sp = L.Spots.FirstOrDefault(s => s.Room == t.Id);
                P3 to = sp != null ? sp.Approach : new P3(t.Floor, t.Rect.CX, t.Rect.CZ);
                var p = Pathfinder.Find(L, from, to, anyDoor);
                if (!p.Ok) return "unreachable " + t.Name;
            }
            foreach (var d in L.Doors) if (d.Width < 1.1f) return "door too narrow " + d.Id;
            return null;
        }
    }

    public static class Palettes
    {
        // Dream palettes: saturated jewel tones with a grotesque accent. Resolved to colors in the Unity layer.
        public static readonly string[] All = { "Amethyst", "BloodOpera", "TealAbyss", "RosePorcelain", "GildedRot", "MoonMint", "Absinthe", "Nocturne", "CoralFlesh", "BoneIvory", "CobaltCandle", "PeachMold" };
        public static string For(RoomType t, Rng rng)
        {
            switch (t)
            {
                case RoomType.GrandHall: case RoomType.Landing: return rng.Pick(new[] { "Amethyst", "Nocturne", "BloodOpera" });
                case RoomType.Dining: return rng.Pick(new[] { "BloodOpera", "GildedRot", "CoralFlesh" });
                case RoomType.Kitchen: return rng.Pick(new[] { "BoneIvory", "PeachMold", "MoonMint" });
                case RoomType.Library: case RoomType.Archive: return rng.Pick(new[] { "GildedRot", "Nocturne", "Absinthe" });
                case RoomType.Pool: case RoomType.WaterRoom: case RoomType.MirrorWater: return rng.Pick(new[] { "TealAbyss", "MoonMint", "CobaltCandle" });
                case RoomType.Greenhouse: return rng.Pick(new[] { "Absinthe", "MoonMint", "PeachMold" });
                case RoomType.Theater: case RoomType.EmptyAuditorium: return rng.Pick(new[] { "BloodOpera", "Amethyst", "CoralFlesh" });
                case RoomType.Chapel: return rng.Pick(new[] { "BoneIvory", "CobaltCandle", "Amethyst" });
                case RoomType.PowerRoom: case RoomType.MachineRoom: case RoomType.Laundry: case RoomType.Storage: return rng.Pick(new[] { "Absinthe", "CobaltCandle", "GildedRot" });
                case RoomType.WhiteDoors: return "BoneIvory";
                case RoomType.RainCorridor: return rng.Pick(new[] { "Nocturne", "TealAbyss" });
                case RoomType.ClockMuseum: return rng.Pick(new[] { "GildedRot", "Amethyst" });
                case RoomType.WaitingRoom: return rng.Pick(new[] { "PeachMold", "MoonMint", "BoneIvory" });
                case RoomType.Courtroom: return "BloodOpera";
                case RoomType.DollRoom: return rng.Pick(new[] { "RosePorcelain", "CoralFlesh", "PeachMold" });
                case RoomType.TrophyRoom: return rng.Pick(new[] { "GildedRot", "BloodOpera" });
                case RoomType.Courtyard: return rng.Pick(new[] { "Nocturne", "MoonMint", "Absinthe" });
                case RoomType.TeaRoom: return rng.Pick(new[] { "RosePorcelain", "MoonMint", "PeachMold" });
                case RoomType.Study: return rng.Pick(new[] { "GildedRot", "Nocturne", "CobaltCandle" });
                case RoomType.WineCellar: case RoomType.BoilerRoom: case RoomType.Incinerator: return rng.Pick(new[] { "GildedRot", "BloodOpera", "Absinthe" });
                case RoomType.GuestRoom: case RoomType.Bedroom: return All[rng.R(All.Length)];
            }
            return All[rng.R(All.Length)];
        }
        public static float BaseLight(RoomType t)
        {
            switch (t)
            {
                case RoomType.Corridor: return 0.55f; case RoomType.Stairwell: return 0.45f; case RoomType.Storage: case RoomType.Closet: return 0.45f;
                case RoomType.PowerRoom: case RoomType.MachineRoom: return 0.55f; case RoomType.Chapel: return 0.6f; case RoomType.RainCorridor: return 0.4f;
                case RoomType.EmptyAuditorium: return 0.35f; case RoomType.Theater: return 0.6f;
                case RoomType.Darkroom: return 0.22f; case RoomType.ColdStorage: return 0.5f; case RoomType.Incinerator: return 0.5f;
            }
            return 0.85f;
        }
    }
}
