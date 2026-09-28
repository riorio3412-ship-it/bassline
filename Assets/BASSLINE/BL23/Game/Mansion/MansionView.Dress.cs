using System;
using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Surface and floor clutter: the small things that make a room look lived in — books, candlesticks, oil lamps,
    /// vases of flowers, bottles, goblets, cups, papers, frames, statuettes, pillows, baskets, crates. Small props are
    /// individual Rigidbody objects (with PropMaterial: glass shatters, paper crumples, metal dents) that the player and
    /// the NPC bodies knock about and PhysicsGrab can pick up; heavy or awkward pieces are static. Purely visual: the
    /// kernel never hears about them.
    /// </summary>
    public sealed partial class MansionView
    {
        int _propCount, _propDynamic, _propLights;
        /// <summary>Layer of small dressing props (unnamed layer 27): the view camera culls it beyond ClutterCullDistance.</summary>
        internal const int ClutterLayer = 27;
        internal const float ClutterCullDistance = 13f;

        sealed class DressRoomState
        {
            public RoomView Rv; public System.Random Rnd; public int Budget; public int Lights; public List<Vector2> Avoid = new List<Vector2>();
            public int Placed;
            public int Physical;                     // reachable physical props (capped per room)
            public readonly List<GameObject> Statics = new List<GameObject>();
        }

        void BuildAllDress()
        {
            foreach (var rv in Rooms)
            {
                if (rv == null || rv.Room.Void || rv.Room.Type == RoomType.Courtroom || rv.Room.Type == RoomType.Elevator) continue;
                try { DressRoom(rv); } catch (Exception e) { Debug.LogWarning($"[Mansion] dress {rv.Room.Name}: {e.Message}\n{e.StackTrace}"); }
            }
            Physics.SyncTransforms();
            Debug.Log($"[Mansion] dressing: {_propCount} props ({_propDynamic} physical), {_propLights} prop lights");
        }

        static int PropBudget(Room r)
        {
            float a = r.Rect.W * r.Rect.D;
            switch (r.Type)
            {
                case RoomType.Corridor: case RoomType.Landing: case RoomType.Stairwell: return Mathf.Clamp((int)(a / 6f), 4, 22);
                case RoomType.PowerRoom: case RoomType.MachineRoom: case RoomType.BoilerRoom: case RoomType.WaterRoom: return 10;
                case RoomType.Pool: case RoomType.Greenhouse: case RoomType.Courtyard: return 12;
            }
            if (RoomInfo.IsMystery(r.Type)) return 8;
            return Mathf.Clamp((int)(a / 3.2f), 10, 40);
        }

        void DressRoom(RoomView rv)
        {
            var r = rv.Room;
            var st = new DressRoomState { Rv = rv, Rnd = new System.Random(r.Id * 7349 + (int)(Layout.Seed % 9973) + Layout.LoopId * 31), Budget = PropBudget(r) };
            // keep clear of kernel items resting in this room (they are dropped onto whatever surface is below them)
            foreach (var sp in Layout.ItemSpawns) if (sp.Room == r.Id) st.Avoid.Add(new Vector2(sp.Pos.x, sp.Pos.z));
            if (_ownerAvoid.TryGetValue(r.Id, out var oav)) st.Avoid.AddRange(oav);   // a resident's own pieces on their surfaces
            // furniture surfaces, in a stable order
            foreach (int fid in r.Furniture)
            {
                if (st.Placed >= st.Budget) break;
                var f = Layout.Furniture[fid];
                if (!FurnitureGo.TryGetValue(fid, out var go) || go == null) continue;
                try { DressFurniture(st, f, go); } catch (Exception e) { Debug.LogWarning($"[Mansion] dress {f.Type}#{f.Id}: {e.Message}"); }
            }
            DressFloor(st);
            // the props that never move are merged into one static batch per room (draw calls)
            st.Statics.AddRange(rv.StaticDecor); rv.StaticDecor.Clear();
            try { CombineStatic(rv, st.Statics, "Clutter"); } catch (Exception e) { Debug.LogWarning("[Mansion] clutter merge " + rv.Room.Name + ": " + e.Message); }
        }

        // ------------------------------------------------------------------ surfaces
        /// <summary>Top of the furniture as seen: the scanned model's bounds when there is one, else the catalog height.
        /// The furniture box collider is trimmed to the same height so props rest where they appear to rest.</summary>
        float SurfaceTop(Furniture f, GameObject go, RoomView rv)
        {
            float top = -1f;
            foreach (Transform ch in go.transform)
            {
                if (ch.name == "vis" || ch.name == "Lid" || ch.name.StartsWith("Door_") || ch.name.StartsWith("Drawer_") || ch.name.StartsWith("Slot_")) continue;   // moving parts are not the top
                foreach (var rr in ch.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, rr.bounds.max.y - rv.FloorY);
            }
            if (top < 0.2f) top = f.H;
            top = Mathf.Clamp(top, 0.25f, Mathf.Max(0.3f, f.H * 1.35f));
            var bc = go.GetComponent<BoxCollider>();
            if (bc != null && Mathf.Abs(bc.size.y - top) > 0.02f && Mathf.Abs(bc.size.y - f.H) < 0.05f)
            {
                bc.size = new Vector3(bc.size.x, top, bc.size.z); bc.center = new Vector3(bc.center.x, top * 0.5f, bc.center.z);
            }
            return top;
        }

        /// <summary>World point on a furniture top: local x across the width (0 = centre), z toward the front.</summary>
        static Vector3 OnTop(Furniture f, float floorY, float top, float lx, float lz)
        {
            float yr = f.Yaw * Mathf.Deg2Rad; float fx = Mathf.Sin(yr), fz = Mathf.Cos(yr);
            return new Vector3(f.Pos.x + fz * lx + fx * lz, floorY + top, f.Pos.z - fx * lx + fz * lz);
        }

        bool Clear(DressRoomState st, Vector3 p, float rad)
        {
            foreach (var a in st.Avoid) if ((a.x - p.x) * (a.x - p.x) + (a.y - p.z) * (a.y - p.z) < (rad + 0.26f) * (rad + 0.26f)) return false;   // kernel items are dropped onto whatever lies below them
            return true;
        }

        void DressFurniture(DressRoomState st, Furniture f, GameObject go)
        {
            var rv = st.Rv; var rnd = st.Rnd; var rt = rv.Room.Type;
            float fy = rv.FloorY;
            float W = f.W, D = f.D;
            bool formal = rt != RoomType.Kitchen && rt != RoomType.Workshop && !IsUtility(rt);
            if (rt == RoomType.Bedroom && OwnerDressFurniture(st, f, go)) return;   // a resident's own surfaces (MansionView.OwnerProps)
            switch (f.Type)
            {
                case "Sideboard":
                case "Console":
                    {
                        float top = SurfaceTop(f, go, rv);
                        float hw = W * 0.5f - 0.14f, bz = -D * 0.12f;
                        // symmetric: centrepiece flanked by a pair of candlesticks, one extra piece off-centre
                        string centre = Pick(rnd, Centres(rt));
                        Put(st, f, top, centre, 0, bz);
                        bool lit = rnd.NextDouble() < 0.55;
                        Put(st, f, top, "candlestick", -hw, bz, 0, lit); Put(st, f, top, "candlestick", hw, bz, 0, false);
                        if (W > 1.2f) Put(st, f, top, Pick(rnd, Extras(rt)), (rnd.NextDouble() < 0.5 ? -1 : 1) * hw * 0.5f, D * 0.15f);
                        if (W > 1.7f) Put(st, f, top, Pick(rnd, Extras(rt)), (rnd.NextDouble() < 0.5 ? -1 : 1) * hw * 0.62f, -D * 0.05f);
                        break;
                    }
                case "Nightstand":
                    {
                        float top = SurfaceTop(f, go, rv);
                        Put(st, f, top, Pick(rnd, "books", "book", "cup", "frame", "candlestick", "candle_mess", "doll_small", "wilted", "decanter"), W * 0.18f, D * 0.12f, 0, rnd.NextDouble() < 0.4);
                        if (rnd.NextDouble() < 0.5) Put(st, f, top, Pick(rnd, "vase_small", "cup", "papers"), -W * 0.05f, D * 0.2f);
                        break;
                    }
                case "SideTable":
                    {
                        float top = SurfaceTop(f, go, rv);
                        // the pool of light beside the seat
                        Put(st, f, top, rnd.NextDouble() < 0.7 ? "lamp" : "candlestick", 0, 0, 0, true);
                        break;
                    }
                case "CoffeeTable":
                    {
                        float top = SurfaceTop(f, go, rv);
                        Put(st, f, top, Pick(rnd, Extras(rt)), -W * 0.25f, 0);
                        Put(st, f, top, Pick(rnd, "cups", "cup", "papers", "bowl"), W * 0.2f, D * 0.05f);
                        if (rnd.NextDouble() < 0.5) Put(st, f, top, Pick(rnd, "candlestick", "candle_mess", "bell_jar", "urn", "vase_small"), W * 0.02f, -D * 0.15f, 0, rnd.NextDouble() < 0.3);
                        break;
                    }
                case "Desk":
                    {
                        float top = f.H;
                        Put(st, f, top, "books", W * 0.36f, -D * 0.2f);
                        Put(st, f, top, Pick(rnd, "frame", "lamp", "statuette", "globe_small"), -W * 0.05f, -D * 0.3f, 0, true);
                        Put(st, f, top, "papers", W * 0.1f, D * 0.15f);
                        if (rt == RoomType.Study || rt == RoomType.Infirmary) Put(st, f, top, rt == RoomType.Infirmary ? "microscope" : Pick(rnd, "skull", "bottle", "cup"), -W * 0.38f, D * 0.05f);
                        break;
                    }
                case "ReadingTable":
                    {
                        float top = f.H;
                        Put(st, f, top, "books", -W * 0.1f, D * 0.1f); Put(st, f, top, "books", W * 0.35f, -D * 0.05f);
                        Put(st, f, top, "open_book", W * 0.05f, D * 0.2f);
                        Put(st, f, top, "lamp", -W * 0.36f, -D * 0.1f, 0, true);   // the reading lamp: a pool of light on the island table
                        break;
                    }
                case "LongTable":
                    {
                        float top = f.H + 0.012f;
                        int n = W > 4f ? 3 : 2;
                        for (int i = 0; i < n; i++)
                        {
                            float x = (i - (n - 1) * 0.5f) * W / n + (float)(rnd.NextDouble() - 0.5) * 0.2f;
                            Put(st, f, top, Pick(rnd, "bottle", "goblets", "bottle", "bowl", "jug"), x, (rnd.NextDouble() < 0.5 ? -1 : 1) * 0.12f);
                        }
                        break;
                    }
                case "RoundTable":
                    {
                        if (rt == RoomType.TeaRoom || rt == RoomType.Dining) break;   // tea service already laid
                        float top = SurfaceTop(f, go, rv);
                        Put(st, f, top, Pick(rnd, "cups", "books", "flowers_low"), W * 0.22f, 0);
                        break;
                    }
                case "Counter":
                case "Island":
                    {
                        float top = f.H;
                        for (int i = 0; i < (f.Type == "Island" ? 3 : 2); i++)
                            Put(st, f, top, Pick(rnd, "pot", "jug", "bowl", "bottle", "jar", "jar", "board", "pan"), (i - 0.5f) * W * 0.4f + (float)(rnd.NextDouble() - 0.5) * 0.2f, (float)(rnd.NextDouble() - 0.5) * D * 0.3f);
                        break;
                    }
                case "BarCounter":
                    {
                        float top = f.H;
                        Put(st, f, top, "bottle_row", -W * 0.2f, -D * 0.22f, 0, false, false);
                        Put(st, f, top, "goblets", W * 0.25f, D * 0.1f);
                        Put(st, f, top, "bottle", W * 0.05f, D * 0.12f);
                        break;
                    }
                case "Piano_Upright":
                    {
                        float top = f.H;
                        Put(st, f, top, "candlestick", -W * 0.36f, -D * 0.1f, 0, rnd.NextDouble() < 0.5);
                        Put(st, f, top, Pick(rnd, "frame", "vase", "statuette"), W * 0.3f, -D * 0.1f);
                        Put(st, f, top, "papers", 0, -D * 0.05f, 0, false, false);
                        break;
                    }
                case "Chest":
                    {
                        if (go.GetComponent<OpenableParts>() == null && rnd.NextDouble() < 0.45) Put(st, f, SurfaceTop(f, go, rv), Pick(rnd, "books", "suitcase_small", "candlestick", "basket"), 0, 0);   // nothing on a lid that opens
                        break;
                    }
                case "Workbench":
                    {
                        float top = f.H;
                        Put(st, f, top, Pick(rnd, "jar", "bottle", "pot", "cans"), W * 0.35f, -D * 0.2f);
                        Put(st, f, top, Pick(rnd, "papers", "board", "cans"), -W * 0.1f, D * 0.1f, 0, false, false);
                        break;
                    }
                case "Bookshelf":
                    {
                        // an ornament on top of every other tall case
                        if (f.H > 2f && rnd.NextDouble() < 0.4) Put(st, f, f.H, Pick(rnd, "vase", "bust", "globe_small"), (float)(rnd.NextDouble() - 0.5) * W * 0.5f, 0, 0, false, false);
                        break;
                    }
                case "Fireplace":
                    {
                        // logs in a basket beside the hearth; fire irons are the kernel's poker
                        var side = OnTop(f, fy, 0, (rnd.NextDouble() < 0.5 ? -1 : 1) * (W * 0.5f + 0.32f), 0.05f);
                        if (FloorFree(rv, side.x, side.z, 0.22f)) Prop(st, "log_basket", side, f.Yaw, false, false);
                        break;
                    }
                case "Sofa":
                case "DayBed":
                    {
                        if (!formal) break;
                        // cushions tucked into one corner of the seat (static: the seat collider is a box)
                        float s = rnd.NextDouble() < 0.5 ? -1 : 1;
                        var p = OnTop(f, fy, 0.42f, s * (W * 0.5f - 0.38f), -D * 0.08f);
                        Prop(st, "pillows", p, f.Yaw + s * 8f, false, false);
                        break;
                    }
                case "Cabinet":
                    {
                        if (rnd.NextDouble() < 0.6) Put(st, f, SurfaceTop(f, go, rv), Pick(rnd, "vase", "vase_small", "bowl"), (float)(rnd.NextDouble() - 0.5) * W * 0.5f, 0, 0, false, false);
                        break;
                    }
                case "Lounger":
                    {
                        if (rnd.NextDouble() < 0.6) Put(st, f, 0.36f, "towels", 0, D * 0.3f, 90f);
                        break;
                    }
                case "Cart":
                case "Trolley":
                    {
                        if (rt == RoomType.Pool || rt == RoomType.Laundry || rt == RoomType.Infirmary) Put(st, f, f.H, "towels", 0, 0);
                        else if (rt == RoomType.Dining || rt == RoomType.Kitchen || rt == RoomType.TeaRoom) Put(st, f, f.H, Pick(rnd, "cups", "bottle", "decanter", "bowl"), 0, 0);
                        break;
                    }
                case "Shelves":
                case "WineRack":
                    break;
            }
        }

        static string Pick(System.Random rnd, params string[] a) => a[rnd.Next(a.Length)];

        /// <summary>Centrepieces for a console / sideboard, by room: each room type has its own vocabulary.</summary>
        static string[] Centres(RoomType t)
        {
            switch (t)
            {
                case RoomType.Dining: return new[] { "flowers", "bowl", "decanter", "urn", "flowers" };
                case RoomType.Library: case RoomType.Archive: return new[] { "bell_jar", "globe_small", "bust", "lamp", "urn" };
                case RoomType.Study: return new[] { "bell_jar", "skull", "globe_small", "specimens", "lamp" };
                case RoomType.Lounge: case RoomType.Parlor: return new[] { "flowers", "bell_jar", "urn", "statuette", "lamp", "wilted" };
                case RoomType.Bedroom: case RoomType.GuestRoom: return new[] { "lamp", "wilted", "doll_small", "urn", "flowers" };
                case RoomType.TeaRoom: return new[] { "flowers", "urn", "bowl", "flowers_low" };
                case RoomType.MusicRoom: return new[] { "metronome", "flowers", "urn", "candle_mess" };
                case RoomType.GameRoom: return new[] { "decanter", "skull", "bell_jar", "lamp" };
                case RoomType.Gallery: case RoomType.TrophyRoom: return new[] { "bell_jar", "bust", "specimens", "crow", "statuette" };
                case RoomType.DollRoom: return new[] { "doll_small", "bell_jar", "wilted", "doll_small" };
                case RoomType.Infirmary: return new[] { "specimens", "microscope", "jar" };
                case RoomType.Chapel: return new[] { "candle_mess", "wilted", "skull" };
                case RoomType.Wardrobe: return new[] { "doll_small", "urn", "frame" };
                case RoomType.Darkroom: return new[] { "papers", "bottle", "jar", "frame" };
                case RoomType.ColdStorage: return new[] { "specimens", "jar", "cans" };
                case RoomType.Incinerator: return new[] { "cans", "bucket", "papers" };
                case RoomType.GrandHall: case RoomType.Corridor: case RoomType.Landing: return new[] { "flowers", "urn", "bell_jar", "statuette", "wilted", "crow", "lamp" };
            }
            if (RoomInfo.IsMystery(t)) return new[] { "bell_jar", "specimens", "wilted", "doll_small", "candle_mess" };
            return new[] { "flowers", "urn", "vase", "lamp" };
        }

        /// <summary>Secondary surface pieces, by room.</summary>
        static string[] Extras(RoomType t)
        {
            switch (t)
            {
                case RoomType.Dining: return new[] { "bottle", "goblets", "decanter", "jug", "candle_mess", "bowl" };
                case RoomType.Kitchen: return new[] { "pot", "jar", "board", "pan", "bowl", "bottle", "cans", "jug" };
                case RoomType.Library: case RoomType.Archive: return new[] { "books", "books", "open_book", "papers", "candle_mess", "skull" };
                case RoomType.Study: return new[] { "books", "papers", "decanter", "crow", "skull", "open_book" };
                case RoomType.Lounge: case RoomType.Parlor: return new[] { "books", "decanter", "goblets", "cups", "crow", "vase_small", "cards" };
                case RoomType.Bedroom: case RoomType.GuestRoom: return new[] { "books", "cup", "frame", "candle_mess", "papers" };
                case RoomType.TeaRoom: return new[] { "cups", "cups", "bowl", "vase_small" };
                case RoomType.MusicRoom: return new[] { "papers", "candle_mess", "vase_small", "cups" };
                case RoomType.GameRoom: return new[] { "cards", "bottle", "goblets", "cups", "decanter" };
                case RoomType.Gallery: case RoomType.TrophyRoom: return new[] { "specimens", "skull", "vase_small", "papers" };
                case RoomType.DollRoom: return new[] { "doll_small", "cup", "wilted" };
                case RoomType.Infirmary: return new[] { "bottle", "jar", "papers", "bowl" };
                case RoomType.Workshop: return new[] { "jar", "cans", "papers", "bottle", "board" };
                case RoomType.Wardrobe: return new[] { "books", "cup", "frame" };
                case RoomType.Darkroom: return new[] { "papers", "bottle", "jar", "papers" };
                case RoomType.ColdStorage: return new[] { "jar", "specimens", "cans" };
                case RoomType.Incinerator: return new[] { "cans", "papers" };
            }
            if (RoomInfo.IsMystery(t)) return new[] { "wilted", "candle_mess", "specimens", "papers" };
            return new[] { "books", "vase_small", "goblets", "papers", "cups" };
        }

        /// <summary>Put a prop on a furniture top at local (lx, lz).</summary>
        GameObject Put(DressRoomState st, Furniture f, float top, string kind, float lx, float lz, float dyaw = 0f, bool lit = false, bool physical = true)
        {
            if (st.Placed >= st.Budget) return null;
            var p = OnTop(f, st.Rv.FloorY, top, lx, lz);
            if (!Clear(st, p, 0.1f)) return null;
            // nothing may stand where another piece rises through it (a standard lamp beside a console, a cabinet edge)
            foreach (int ofid in st.Rv.Room.Furniture)
            {
                var o = Layout.Furniture[ofid];
                if (o == f || o.H < top - 0.05f || o.Type == "Rug" || o.Type == "Chandelier") continue;
                NavGrid.GetFootprint(o, 0.08f, out var orc);
                if (orc.Contains(p.x, p.z)) return null;
            }
            return Prop(st, kind, p, f.Yaw + dyaw + (float)(st.Rnd.NextDouble() - 0.5) * 30f, physical, lit);
        }

        // ------------------------------------------------------------------ floor clutter
        void DressFloor(DressRoomState st)
        {
            var rv = st.Rv; var r = rv.Room; var rnd = st.Rnd; var R = r.Rect;
            string[] kinds = null; int n = 0;
            switch (r.Type)
            {
                case RoomType.Library: case RoomType.Study: case RoomType.Archive: kinds = new[] { "book_pile", "book_pile", "globe_floor", "basket" }; n = 3; break;
                case RoomType.Storage: case RoomType.Closet: kinds = new[] { "crate", "crate", "bucket", "suitcase", "crate_small", "sack" }; n = 5; break;
                case RoomType.WineCellar: kinds = new[] { "barrel_small", "crate", "crate_small", "bottle_crate" }; n = 5; break;
                case RoomType.BoilerRoom: case RoomType.MachineRoom: case RoomType.PowerRoom: case RoomType.WaterRoom: kinds = new[] { "crate", "bucket", "cans", "toolchest" }; n = 3; break;
                case RoomType.Workshop: kinds = new[] { "toolchest", "crate", "bucket", "crate_small" }; n = 4; break;
                case RoomType.Wardrobe: case RoomType.GuestRoom: kinds = new[] { "suitcase", "hatbox", "basket" }; n = 2; break;
                case RoomType.Bedroom: kinds = new[] { "suitcase", "book_pile", "basket" }; n = 1; break;
                case RoomType.Kitchen: kinds = new[] { "crate_small", "basket", "bucket", "sack" }; n = 3; break;
                case RoomType.Laundry: kinds = new[] { "basket", "basket", "bucket" }; n = 3; break;
                case RoomType.Lounge: case RoomType.Parlor: case RoomType.MusicRoom: kinds = new[] { "basket", "book_pile" }; n = 1; break;
                case RoomType.Greenhouse: kinds = new[] { "pot_plant", "pot_plant", "bucket", "watering" }; n = 4; break;
                case RoomType.Gallery: kinds = new[] { "crate", "frames_stack" }; n = 2; break;
                case RoomType.TrophyRoom: kinds = new[] { "crate_small", "frames_stack" }; n = 1; break;
                case RoomType.Chapel: kinds = new[] { "candles_floor", "candles_floor" }; n = 2; break;
            }
            if (r.Type == RoomType.Bedroom) OwnerFloorKinds(r, ref kinds, ref n);   // tidy residents keep bare floors, messy ones don't
            if (kinds == null || n <= 0) return;
            // floor-standing things live in corners first, then tight against a wall — square to it, never loose mid-floor,
            // and they stay put (static): nothing on the floor to trip over or kick across a lane
            var cands = new List<(Vector3 p, float yaw)>();
            var corners = new List<(Vector3 p, float yaw)>();
            { var R0 = r.Rect; float i0 = 0.18f + 0.3f; foreach (var (cx, cz, cy) in new[] { (R0.x0 + i0, R0.z0 + i0, 45f), (R0.x1 - i0, R0.z0 + i0, -45f), (R0.x0 + i0, R0.z1 - i0, 135f), (R0.x1 - i0, R0.z1 - i0, 225f) }) if (_grids[r.Floor].At(cx, cz) == r.Id) corners.Add((new Vector3(cx, rv.FloorY, cz), cy)); }
            for (int i = corners.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var t = corners[i]; corners[i] = corners[j]; corners[j] = t; }
            var walls = new List<(Vector3 p, float yaw)>();
            for (int i = 0; i < rv.WallSlots.Count; i++)
            {
                var p = rv.WallSlots[i]; var nn = rv.WallSlotN[i];
                walls.Add((new Vector3(p.x, rv.FloorY, p.z) + nn * 0.3f, Mathf.Atan2(nn.x, nn.z) * Mathf.Rad2Deg));
            }
            for (int i = walls.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var t = walls[i]; walls[i] = walls[j]; walls[j] = t; }
            cands.AddRange(corners); cands.AddRange(walls);
            int placed = 0;
            foreach (var (c, wy) in cands)
            {
                if (placed >= n || st.Placed >= st.Budget + 4) break;
                if (!FloorFree(rv, c.x, c.z, 0.26f) || !Clear(st, c, 0.2f)) continue;
                if (!DecorRectFree(r.Floor, new RectF(c.x - 0.22f, c.z - 0.22f, c.x + 0.22f, c.z + 0.22f))) continue;
                string k = kinds[rnd.Next(kinds.Length)];
                if (Prop(st, k, c, wy + (float)(rnd.NextDouble() - 0.5) * 8f, false, false) != null) { placed++; rv.Blocked.Add(new RectF(c.x - 0.3f, c.z - 0.3f, c.x + 0.3f, c.z + 0.3f)); }
            }
        }

        // ------------------------------------------------------------------ the prop kit
        /// <summary>Spawn one prop standing at 'bottom' (world). physical = own Rigidbody + PropMaterial (small things);
        /// lit = candle/lamp flame with a warm light (capped per room).</summary>
        GameObject Prop(DressRoomState st, string kind, Vector3 bottom, float yaw, bool physical, bool lit)
        {
            var rv = st.Rv; var rnd = st.Rnd; var pal = rv.Pal;
            GameObject go = null; float mass = 1f; Mat mat = Mat.Wood; bool fragile = false; float flameY = -1f; float flameH = 0.06f; bool lampLight = false;
            switch (kind)
            {
                case "vase":
                    {
                        string id = Pick(rnd, "brass_vase_01", "brass_vase_02", "ceramic_vase_01", "ceramic_vase_04", "antique_ceramic_vase_01", "ceramic_vase_02");
                        go = ModelProp(rv, id, bottom, yaw, new Vector3(0.24f, 0.42f, 0.24f));
                        mass = 2.5f; mat = id.StartsWith("brass") ? Mat.Metal : Mat.Ceramic; fragile = mat == Mat.Ceramic; break;
                    }
                case "vase_small":
                    {
                        string id = Pick(rnd, "brass_vase_03", "brass_vase_04", "ceramic_vase_03");
                        go = ModelProp(rv, id, bottom, yaw, new Vector3(0.12f, 0.24f, 0.12f));
                        mass = 0.8f; mat = id.StartsWith("brass") ? Mat.Metal : Mat.Ceramic; fragile = mat == Mat.Ceramic; break;
                    }
                case "flowers":
                case "flowers_low":
                    {
                        bool low = kind == "flowers_low";
                        string id = Pick(rnd, "ceramic_vase_01", "ceramic_vase_02", "ceramic_vase_04", "brass_vase_02");
                        go = ModelProp(rv, id, bottom, yaw, low ? new Vector3(0.14f, 0.2f, 0.14f) : new Vector3(0.2f, 0.32f, 0.2f));
                        if (go != null)
                        {
                            float h = PropHeight(go, bottom.y);
                            { int bv = rnd.Next(3); AttachMesh(rv, go, "bouquet" + bv + pal.Id, () => Bouquet(bv, pal), new Vector3(0, h - 0.02f, 0), low ? 0.7f : 1f); }
                        }
                        mass = 2f; mat = Mat.Ceramic; fragile = true; break;
                    }
                case "statuette":
                    go = ModelProp(rv, Pick(rnd, "horse_statue_01", "marble_bust_01", "gothic_statue"), bottom, yaw, new Vector3(0.2f, 0.32f, 0.2f));
                    mass = 3f; mat = Mat.Ceramic; fragile = true; break;
                case "bust":
                    go = ModelProp(rv, Pick(rnd, "marble_bust_01", "lion_head", "horse_head", "bull_head"), bottom, yaw, new Vector3(0.3f, 0.42f, 0.28f));
                    mass = 9f; mat = Mat.Stone; physical = false; break;
                case "clock":
                    go = ModelProp(rv, "mantel_clock_01", bottom, yaw, new Vector3(0.42f, 0.3f, 0.18f));
                    mass = 3f; mat = Mat.Wood; break;
                case "lamp":
                    {
                        go = ModelProp(rv, "vintage_oil_lamp", bottom, yaw, new Vector3(0.2f, 0.52f, 0.2f));
                        if (go != null) { float h = PropHeight(go, bottom.y); flameY = h * 0.64f; flameH = 0.05f; lampLight = true; }
                        mass = 2.2f; mat = Mat.Glass; fragile = true; lit = true; break;
                    }
                case "candlestick":
                    {
                        int cv = rnd.Next(4); go = ProcProp(rv, "candlestick" + cv, bottom, yaw, () => CandlestickMesh(cv), out float h);
                        flameY = h + 0.005f; flameH = 0.055f;
                        mass = 1.2f; mat = Mat.Metal; break;
                    }
                case "goblets":
                    go = ModelProp(rv, "brass_goblets", bottom, yaw, new Vector3(0.3f, 0.18f, 0.2f));
                    mass = 0.9f; mat = Mat.Metal; break;
                case "bottle":
                    { int v = rnd.Next(3); go = ProcProp(rv, "bottle" + v, bottom, yaw, () => BottleMesh(v), out _); }
                    mass = 1.1f; mat = Mat.Glass; fragile = true; break;
                case "bottle_row":
                    go = ModelProp(rv, "wine_bottles_01", bottom, yaw, new Vector3(0.52f, 0.34f, 0.1f));
                    mass = 4f; mat = Mat.Glass; physical = false; break;
                case "bottle_crate":
                    go = ProcProp(rv, "bottlecrate", bottom, yaw, BottleCrateMesh, out _);
                    mass = 14f; mat = Mat.Wood; break;
                case "cup":
                    go = ProcProp(rv, "cup", bottom, yaw, () => CupMesh(false), out _);
                    mass = 0.3f; mat = Mat.Ceramic; fragile = true; break;
                case "cups":
                    go = ProcProp(rv, "cups", bottom, yaw, () => CupMesh(true), out _);
                    mass = 0.8f; mat = Mat.Ceramic; fragile = true; break;
                case "bowl":
                    go = ProcProp(rv, "fruitbowl", bottom, yaw, FruitBowlMesh, out _);
                    mass = 1.6f; mat = Mat.Ceramic; fragile = true; break;
                case "jug":
                    go = ModelProp(rv, "jug_01", bottom, yaw, new Vector3(0.22f, 0.18f, 0.16f));
                    mass = 1.2f; mat = Mat.Ceramic; fragile = true; break;
                case "pot":
                    go = ModelProp(rv, Pick(rnd, "pot_enamel_01", "brass_pot_01"), bottom, yaw, new Vector3(0.28f, 0.2f, 0.24f));
                    mass = 2f; mat = Mat.Metal; break;
                case "pan":
                    go = ProcProp(rv, "pan", bottom, yaw, PanMesh, out _);
                    mass = 1.4f; mat = Mat.Metal; break;
                case "jar":
                    { int v = rnd.Next(2); go = ProcProp(rv, "jars" + v, bottom, yaw, () => JarsMesh(v), out _); }
                    mass = 1f; mat = Mat.Glass; fragile = true; break;
                case "board":
                    go = ProcProp(rv, "board", bottom, yaw, BoardMesh, out _);
                    mass = 0.8f; mat = Mat.Wood; break;
                case "cans":
                    go = ProcProp(rv, "cans", bottom, yaw, CansMesh, out _);
                    mass = 1.5f; mat = Mat.Metal; break;
                case "frame":
                    go = ModelProp(rv, "standing_picture_frame_01", bottom, yaw, new Vector3(0.2f, 0.24f, 0.1f), null, -90f);
                    mass = 0.6f; mat = Mat.Glass; break;
                case "frames_stack":
                    go = ProcProp(rv, "framestack" + pal.Id, bottom, yaw, () => FrameStackMesh(pal), out _);
                    mass = 8f; mat = Mat.Wood; physical = false; break;
                case "books":
                    { int v = rnd.Next(6); go = ProcProp(rv, "books" + v, bottom, yaw, () => BookStackMesh(v, 2 + v % 3), out _); }
                    mass = 2.4f; mat = Mat.Paper; break;
                case "book":
                    { int v = rnd.Next(4); go = ProcProp(rv, "book" + v, bottom, yaw, () => BookStackMesh(v + 10, 1), out _); }
                    mass = 0.9f; mat = Mat.Paper; break;
                case "open_book":
                    go = ProcProp(rv, "openbook", bottom, yaw, OpenBookMesh, out _);
                    mass = 0.9f; mat = Mat.Paper; physical = false; break;
                case "book_pile":
                    { int v = rnd.Next(3); go = ProcProp(rv, "bookpile" + v, bottom, yaw, () => BookStackMesh(v + 20, 6 + v * 2), out _); }
                    mass = 7f; mat = Mat.Paper; break;
                case "papers":
                    { int v = rnd.Next(3); go = ProcProp(rv, "papers" + v, bottom, yaw, () => PapersMesh(v), out _); }
                    physical = false; break;
                case "skull":
                    go = ProcProp(rv, "skull", bottom, yaw, SkullMesh, out _);
                    mass = 1.2f; mat = Mat.Ceramic; break;
                case "microscope":
                    go = ModelProp(rv, "vintage_microscope", bottom, yaw, new Vector3(0.14f, 0.34f, 0.2f));
                    mass = 3f; mat = Mat.Metal; break;
                case "globe_small":
                    go = ProcProp(rv, "globe", bottom, yaw, () => GlobeMesh(0.12f), out _);
                    mass = 1.5f; mat = Mat.Wood; break;
                case "globe_floor":
                    go = ProcProp(rv, "globefloor", bottom, yaw, () => GlobeMesh(0.3f), out _);
                    mass = 8f; mat = Mat.Wood; physical = false; break;
                case "pillows":
                    go = ModelProp(rv, "throw_pillows_01", bottom, yaw, new Vector3(0.62f, 0.34f, 0.3f), pal.Fabric, 0f, 0.55f);
                    physical = false; mat = Mat.Cloth; break;
                case "basket":
                    go = ModelProp(rv, "wicker_basket_01", bottom, yaw, new Vector3(0.42f, 0.16f, 0.34f));
                    mass = 1.2f; mat = Mat.Wood; break;
                case "log_basket":
                    go = ProcProp(rv, "logbasket", bottom, yaw, LogBasketMesh, out _);
                    mass = 12f; mat = Mat.Wood; physical = false; break;
                case "suitcase":
                case "suitcase_small":
                    go = ModelProp(rv, "vintage_suitcase", bottom, yaw, kind == "suitcase" ? new Vector3(0.62f, 0.45f, 0.24f) : new Vector3(0.42f, 0.3f, 0.16f));
                    mass = kind == "suitcase" ? 6f : 3f; mat = Mat.Leather; break;
                case "hatbox":
                    go = ProcProp(rv, "hatbox" + pal.Id, bottom, yaw, () => HatboxMesh(pal), out _);
                    mass = 1f; mat = Mat.Paper; break;
                case "crate":
                    go = ModelProp(rv, Pick(rnd, "wooden_crate_01", "wooden_crate_02"), bottom, yaw, new Vector3(0.6f, 0.5f, 0.5f));
                    mass = 18f; mat = Mat.Wood; physical = false; break;
                case "crate_small":
                    go = ModelProp(rv, "wooden_crate_01", bottom, yaw, new Vector3(0.4f, 0.32f, 0.34f));
                    mass = 7f; mat = Mat.Wood; break;
                case "bucket":
                    go = ModelProp(rv, "wooden_bucket_01", bottom, yaw, new Vector3(0.3f, 0.3f, 0.3f));
                    mass = 2f; mat = Mat.Wood; break;
                case "toolchest":
                    go = ModelProp(rv, "metal_tool_chest", bottom, yaw, new Vector3(0.62f, 0.6f, 0.38f));
                    mass = 40f; mat = Mat.Metal; physical = false; break;
                case "barrel_small":
                    go = ModelProp(rv, "wine_barrel_01", bottom, yaw, new Vector3(0.5f, 0.62f, 0.5f));
                    mass = 30f; mat = Mat.Wood; physical = false; break;
                case "sack":
                    go = ProcProp(rv, "sack", bottom, yaw, SackMesh, out _);
                    mass = 9f; mat = Mat.Cloth; break;
                case "pot_plant":
                    go = ModelProp(rv, Pick(rnd, "potted_plant_04", "planter_pot_clay"), bottom, yaw, new Vector3(0.3f, 0.34f, 0.3f));
                    mass = 3f; mat = Mat.Ceramic; fragile = true; break;
                case "watering":
                    go = ProcProp(rv, "wateringcan", bottom, yaw, WateringCanMesh, out _);
                    mass = 1f; mat = Mat.Metal; break;
                case "bell_jar":
                    { int v = rnd.Next(5); go = ProcProp(rv, "belljar" + v, bottom, yaw, () => BellJarMesh(v), out _); mass = 2f; mat = Mat.Glass; fragile = true; break; }
                case "specimens":
                    { int v = rnd.Next(4); go = ProcProp(rv, "specimens" + v, bottom, yaw, () => SpecimenJarsMesh(v), out _); mass = 3f; mat = Mat.Glass; fragile = true; physical = false; break; }
                case "wilted":
                    {
                        string id = Pick(rnd, "ceramic_vase_03", "brass_vase_02", "ceramic_vase_02", "antique_ceramic_vase_01");
                        go = ModelProp(rv, id, bottom, yaw, new Vector3(0.16f, 0.26f, 0.16f));
                        if (go != null) { float h = PropHeight(go, bottom.y); int wv = rnd.Next(3); AttachMesh(rv, go, "wilted" + wv, () => WiltedBouquet(wv), new Vector3(0, h - 0.02f, 0), 1f); }
                        mass = 1.5f; mat = Mat.Ceramic; fragile = true; break;
                    }
                case "urn":
                    {
                        int v = rnd.Next(4); int c = rnd.Next(3);
                        Color glaze = c == 0 ? Color.Lerp(pal.Ink, Color.white, 0.15f) : c == 1 ? Color.Lerp(pal.Accent2, pal.Wall, 0.4f) : new Color(0.9f, 0.88f, 0.82f);
                        go = ProcProp(rv, "urn" + v + "_" + c + pal.Id, bottom, yaw, () => UrnMesh(v, glaze, pal.Trim), out _);
                        mass = 2.5f; mat = Mat.Ceramic; fragile = true; break;
                    }
                case "crow":
                    go = ProcProp(rv, "crow", bottom, yaw, CrowMesh, out _); mass = 1f; mat = Mat.Cloth; break;
                case "decanter":
                    { int v = rnd.Next(2); go = ProcProp(rv, "decanter" + v, bottom, yaw, () => DecanterMesh(v), out _); mass = 1.4f; mat = Mat.Glass; fragile = true; break; }
                case "candle_mess":
                    {
                        int v = rnd.Next(3); go = ProcProp(rv, "candlemess" + v, bottom, yaw, () => CandleMessMesh(v), out _);
                        mass = 1f; mat = Mat.Metal; physical = false;
                        if (lit && st.Lights < 2 && _propLights < 48) { AddLight(rv, bottom + Vector3.up * 0.3f, pal.Warm, 0.9f, 2.4f, LightType.Point, false, 0.6f, fire: true); st.Lights++; _propLights++; }
                        break;
                    }
                case "cards":
                    go = ProcProp(rv, "cards", bottom, yaw, CardsMesh, out _); physical = false; break;
                case "metronome":
                    go = ProcProp(rv, "metronome", bottom, yaw, MetronomeMesh, out _); mass = 0.8f; mat = Mat.Wood; break;
                case "towels":
                    { int v = rnd.Next(4); go = ProcProp(rv, "towels" + v, bottom, yaw, () => TowelsMesh(v), out _); mass = 0.8f; mat = Mat.Cloth; break; }
                case "doll_small":
                    { int v = rnd.Next(6); go = ProcProp(rv, "doll" + v + pal.Id, bottom, yaw, () => DollPropMesh(v, pal), out _); mass = 0.6f; mat = Mat.Ceramic; fragile = true; break; }
                case "candles_floor":
                    {
                        int fv = rnd.Next(2); go = ProcProp(rv, "floorcandles" + fv, bottom, yaw, () => FloorCandlesMesh(fv), out float h);
                        flameY = -2f; physical = false; break;
                    }
                default:   // a resident's own things (MansionView.OwnerProps)
                    go = OwnerKindProp(st, kind, bottom, yaw, lit, ref mass, ref mat, ref fragile, ref physical);
                    break;
            }
            if (go == null) return null;
            // no two alike: every placement gets its own size
            if (kind != "pillows" && kind != "papers" && kind != "cards" && kind != "bottle_row") go.transform.localScale = Vector3.one * (0.88f + (float)rnd.NextDouble() * 0.24f);
            st.Placed++; _propCount++;
            foreach (var rr in go.GetComponentsInChildren<Renderer>())
            {
                rv.Renderers.Add(rr);
                rr.shadowCastingMode = ShadowCastingMode.Off;       // clutter never casts: the shadow budget belongs to furniture and people
                rr.gameObject.layer = ClutterLayer;                 // culled beyond ~13 m (Camera.layerCullDistances)
            }
            // flame + light
            if (flameY > 0f)
            {
                AttachFlame(rv, go, new Vector3(0, flameY, 0), flameH);
                if (lit && st.Lights < 2 && _propLights < 48)
                {
                    var lp = go.transform.TransformPoint(new Vector3(0, flameY + 0.08f, 0));
                    var rec = AddLight(rv, lp, lampLight ? new Color(1f, 0.7f, 0.4f) : pal.Warm, lampLight ? 1.7f : 1.1f, lampLight ? 3.8f : 2.8f, LightType.Point, false, lampLight ? 0.12f : 0.5f, fire: true);
                    if (rec != null && rec.Light != null) rec.Light.transform.SetParent(go.transform, true);
                    st.Lights++; _propLights++;
                }
            }
            else if (flameY < -1.5f) AttachFloorCandleFlames(rv, go, st, lit);
            go.layer = ClutterLayer;
            if (physical && (mass > 3.2f || st.Physical >= MaxPhysicalPerRoom)) physical = false;   // heavy pieces and the surplus stay put (merged)
            if (physical) { var rb = MakePhysical(go, mass, mat, fragile); if (rb != null) { Freeze(rb); rv.PhysProps.Add(rb); st.Physical++; _propDynamic++; } }
            else st.Statics.Add(go);
            return go;
        }

        static float PropHeight(GameObject go, float baseY)
        {
            float top = baseY;
            foreach (var rr in go.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, rr.bounds.max.y);
            return top - baseY;
        }

        GameObject ModelProp(RoomView rv, string id, Vector3 bottom, float yaw, Vector3 fit, Color? tint = null, float modelYaw = 0f, float recolor = -1f)
        {
            var m = Models.Get(id); if (m == null) return null;
            var go = Models.Place(m, rv.Root, bottom, yaw, fit, tint, false, Models.Anchor.Bottom, 0f, recolor, modelYaw);
            go.name = "Prop_" + id;
            return go;
        }

        readonly Dictionary<string, (Mesh mesh, int[] slots, float h)> _propMeshes = new Dictionary<string, (Mesh, int[], float)>();

        GameObject ProcProp(RoomView rv, string key, Vector3 bottom, float yaw, Func<MeshBuilder> build, out float height)
        {
            if (!_propMeshes.TryGetValue(key, out var e))
            {
                var mb = build();
                var mesh = mb.ToMesh("Prop_" + key, out var slots);
                e = (mesh, slots, mesh.bounds.max.y);
                _propMeshes[key] = e;
            }
            height = e.h;
            var go = new GameObject("Prop_" + key);
            go.transform.SetParent(rv.Root, false);
            go.transform.SetPositionAndRotation(bottom, Quaternion.Euler(0, yaw, 0));
            var vis = new GameObject("vis"); vis.transform.SetParent(go.transform, false);
            vis.AddComponent<MeshFilter>().sharedMesh = e.mesh;
            vis.AddComponent<MeshRenderer>().sharedMaterials = MansionMats.Materials(e.slots);
            return go;
        }

        void AttachMesh(RoomView rv, GameObject parent, string key, Func<MeshBuilder> build, Vector3 local, float scale)
        {
            if (!_propMeshes.TryGetValue(key, out var e))
            {
                var mb = build();
                var mesh = mb.ToMesh("Prop_" + key, out var slots);
                e = (mesh, slots, mesh.bounds.max.y);
                _propMeshes[key] = e;
            }
            var go = new GameObject(key); go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = local; go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = e.mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = MansionMats.Materials(e.slots);
        }

        Mesh _flameMesh;
        void AttachFlame(RoomView rv, GameObject parent, Vector3 local, float h)
        {
            if (_flameMesh == null) { var mb = new MeshBuilder(); FlameQuad(mb, Vector3.zero, 0.06f, -2); _flameMesh = mb.ToMesh("PropFlame", out _); _flameMesh.bounds = new Bounds(Vector3.up * 0.05f, Vector3.one * 0.2f); }
            var go = new GameObject("flame"); go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = local; go.transform.localScale = Vector3.one * (h / 0.06f);
            go.AddComponent<MeshFilter>().sharedMesh = _flameMesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = MansionMats.Get(S.Flame); mr.shadowCastingMode = ShadowCastingMode.Off;
            rv.Renderers.Add(mr);
        }

        void AttachFloorCandleFlames(RoomView rv, GameObject go, DressRoomState st, bool lit)
        {
            // the floor candle cluster mesh carries its own flame quads (baked into the prop mesh); one light
            if (st.Lights < 2 && _propLights < 48) { AddLight(rv, go.transform.position + Vector3.up * 0.45f, rv.Pal.Warm, 1.2f, 3f, LightType.Point, false, 0.55f, fire: true); st.Lights++; _propLights++; }
        }

        const int MaxPhysicalPerRoom = 16;

        static Rigidbody MakePhysical(GameObject go, float mass, Mat mat, bool fragile)
        {
            // collider from the visual bounds in the prop's own frame
            var inv = go.transform.worldToLocalMatrix; bool first = true; Bounds b = default;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.gameObject.name == "flame") continue;
                var mbnd = mf.sharedMesh.bounds; var m = inv * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = m.MultiplyPoint3x4(mbnd.center + Vector3.Scale(mbnd.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
            }
            if (first) return null;
            var bc = go.AddComponent<BoxCollider>();
            // lift the collider 3 mm so it starts resting, not interpenetrating, on the surface below
            bc.center = b.center + Vector3.up * 0.003f; bc.size = Vector3.Max(b.size * 0.96f, new Vector3(0.02f, 0.02f, 0.02f));
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.05f, mass); rb.linearDamping = 0.08f; rb.angularDamping = 0.25f;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = mass < 0.5f ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.Discrete;
            rb.maxDepenetrationVelocity = 0.6f;   // overlapped by a body or a moved piece: slide aside, never shoot off
            rb.Sleep();
            var pm = go.AddComponent<PropMaterial>(); pm.Mat = mat; pm.Fragile = fragile; pm.Toughness = fragile ? 1f : 1.3f;
            return rb;
        }
    }
}
