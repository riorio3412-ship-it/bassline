using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>Visual style of a room shell, derived from type + palette + variant.</summary>
    internal sealed class RoomStyle
    {
        public int Paper = S.PaperDamask, Lower = S.Wainscot, Floor = S.Herringbone, Ceil = S.Ceiling, Trim = S.Gold, Base = S.WoodDark;
        public float LowerH = 1.0f;
        public bool Panels = true, Dentils = true, ChairRail = true, Crown = true;
        public bool Teeth;                // grotesque rooms: the dentil course is a row of teeth (with the odd fang)
        public int CeilKind;              // 0 plain, 1 coffered, 2 sky, 3 glass roof, 4 industrial, 5 dome (hall void), 6 none
        public bool Windows = true, Stained;
        public Color WallA = Color.white, WallB = Color.gray, LowerTint = Color.white, FloorA = Color.white, FloorB = Color.black, CeilTint = Color.white, TrimTint = Color.white;
        public int Runner = -1;           // corridor runner carpet slot
        public float DoorScale = 1f;      // door height multiplier (too-tall doors)
    }

    public sealed partial class MansionView
    {
        // ------------------------------------------------------------------ lattice ownership (mirrors Sim WallBuilder)
        internal sealed class Grid
        {
            public int F; public float X0, Z0; public int NX, NZ; public int[] Cell;
            public int At(float x, float z)
            {
                int i = (int)Math.Floor((x - X0) / 0.5f + 1e-4), j = (int)Math.Floor((z - Z0) / 0.5f + 1e-4);
                if (i < 0 || j < 0 || i >= NX || j >= NZ) return -1;
                return Cell[i + j * NX];
            }
        }
        internal readonly Dictionary<int, Grid> _grids = new Dictionary<int, Grid>();
        readonly Dictionary<int, Vector3[]> _stairPaths = new Dictionary<int, Vector3[]>();
        readonly Dictionary<int, float> _doorH = new Dictionary<int, float>();
        readonly Dictionary<int, List<RectF>> _floorHoles = new Dictionary<int, List<RectF>>();
        internal readonly List<RectF> _poolHoles = new List<RectF>();
        internal readonly List<Furniture> SynthPools = new List<Furniture>();
        internal bool IsPoolHole(RectF h) { foreach (var p in _poolHoles) if (p.Overlaps(h)) return true; return false; }
        internal readonly List<WindowInfo> Windows = new List<WindowInfo>();
        readonly List<(Vector3 c, Vector3 size)> _wallColliders = new List<(Vector3, Vector3)>();
        internal Room HallRoom, LandingRoom, VoidRoom;
        internal RectF VoidRect;

        internal sealed class WindowInfo
        {
            public int Room; public Vector3 Center; public Vector3 Normal; public float W, H, Sill; public bool Stained; public bool Lit;
        }

        void BuildGrids()
        {
            foreach (var fi in Layout.Floors)
            {
                var g = new Grid { F = fi.F, X0 = fi.Bounds.x0, Z0 = fi.Bounds.z0, NX = (int)Math.Round(fi.Bounds.W / 0.5f), NZ = (int)Math.Round(fi.Bounds.D / 0.5f) };
                g.Cell = new int[g.NX * g.NZ];
                for (int k = 0; k < g.Cell.Length; k++) g.Cell[k] = -1;
                foreach (var r in Layout.Rooms)
                {
                    if (r.Floor != fi.F) continue;
                    int i0 = (int)Math.Round((r.Rect.x0 - g.X0) / 0.5f), i1 = (int)Math.Round((r.Rect.x1 - g.X0) / 0.5f);
                    int j0 = (int)Math.Round((r.Rect.z0 - g.Z0) / 0.5f), j1 = (int)Math.Round((r.Rect.z1 - g.Z0) / 0.5f);
                    for (int i = Math.Max(0, i0); i < Math.Min(g.NX, i1); i++)
                        for (int j = Math.Max(0, j0); j < Math.Min(g.NZ, j1); j++) g.Cell[i + j * g.NX] = r.Id;
                }
                _grids[fi.F] = g;
            }
        }

        internal float NextFloorBase(int f)
        {
            var above = Layout.Floor(f + 1);
            if (above != null) return above.BaseY;
            var fi = Layout.Floor(f);
            return fi != null ? fi.BaseY + fi.Height : 0;
        }

        void CreateRooms()
        {
            Rooms = new RoomView[Layout.Rooms.Count];
            HallRoom = Layout.Rooms.FirstOrDefault(r => r.Type == RoomType.GrandHall && r.Floor == 0);
            LandingRoom = Layout.Rooms.FirstOrDefault(r => r.Type == RoomType.Landing);
            VoidRoom = Layout.Rooms.FirstOrDefault(r => r.Void);
            if (VoidRoom != null) VoidRect = VoidRoom.Rect;
            var roomsRoot = new GameObject("Rooms").transform; roomsRoot.SetParent(_root, false);
            foreach (var r in Layout.Rooms)
            {
                string palId = (r.Type == RoomType.Landing || r.Void) && HallRoom != null ? HallRoom.Palette : r.Palette;
                var own = r.Type == RoomType.Bedroom ? OwnerStyles.Get(r.Owner) : null;
                if (own != null) palId = own.Palette;   // a resident's room always wears their own colours
                var rv = new RoomView { Room = r, Pal = MansionPalette.Get(palId), Rng = new System.Random((int)(Layout.Seed % 100000) * 31 + r.Id * 7919 + Layout.LoopId) };
                var go = new GameObject($"R{r.Id:00}_{r.Type}_{r.Name}");
                go.transform.SetParent(roomsRoot, false);
                rv.Root = go.transform;
                rv.FloorY = Layout.FloorY(r.Floor);
                rv.CeilY = rv.FloorY + r.CeilingH;
                rv.WallTop = NextFloorBase(r.Floor);
                if (r.Type == RoomType.GrandHall && r.Floor == 0) rv.CeilY = rv.FloorY + 4.4f;   // under the landing ring
                if (r.Type == RoomType.Courtroom) rv.WallTop = rv.CeilY;
                var anchor = new GameObject("Anchor").transform; anchor.SetParent(go.transform, false);
                anchor.position = new Vector3(r.Rect.CX, rv.FloorY + 1.6f, r.Rect.CZ);
                rv.Anchor = anchor;
                float top = r.Void ? rv.FloorY + r.CeilingH : rv.CeilY;
                float bottom = r.Void ? rv.FloorY - 4.8f : rv.FloorY;
                rv.Bounds = new Bounds(new Vector3(r.Rect.CX, (top + bottom) * 0.5f, r.Rect.CZ), new Vector3(r.Rect.W, top - bottom, r.Rect.D));
                rv.Style = MakeStyle(r, rv.Pal, rv.Rng);
                rv.Shell = new MeshBuilder();
                rv.Detail = new MeshBuilder();
                Rooms[r.Id] = rv;
            }
        }

        // ------------------------------------------------------------------ styles
        RoomStyle MakeStyle(Room r, MansionPalette p, System.Random rng)
        {
            var s = new RoomStyle();
            int[] papers = { S.PaperDamask, S.PaperStripe, S.PaperDamask2, S.PaperMoon };
            s.Paper = papers[(r.Variant + r.Id) % papers.Length];
            s.WallA = p.Wall; s.WallB = p.Ink; s.LowerTint = Color.Lerp(Color.white, p.Wood * 2.2f, 0.45f); s.TrimTint = p.Trim;
            s.FloorA = Color.Lerp(Color.white, p.FloorA, 0.5f); s.FloorB = p.FloorB; s.CeilTint = Color.Lerp(p.Wall, Color.white, 0.55f);
            s.Floor = S.Herringbone; s.Ceil = S.Ceiling;
            bool eyes = rng.NextDouble() < 0.18;
            switch (r.Type)
            {
                case RoomType.GrandHall:
                case RoomType.Landing:
                    s.Floor = r.Type == RoomType.GrandHall ? S.FloorChecker : S.Herringbone; s.FloorA = Color.Lerp(Color.white, p.FloorA, 0.25f); s.FloorB = PaletteDark(p);
                    s.Paper = S.PaperDamask; s.CeilKind = r.Void ? 5 : 1; s.Stained = true; s.LowerH = 1.3f;
                    if (r.Type == RoomType.Landing) s.Runner = S.Velvet;
                    break;
                case RoomType.Corridor:
                    s.Floor = r.Floor < 0 ? S.StoneFloor : S.Parquet; s.Runner = r.Floor < 0 ? -1 : S.Velvet; s.Paper = eyes ? S.PaperEyes : s.Paper; s.Dentils = r.Floor >= 0;
                    if (r.Floor < 0) { s.Paper = S.Brick; s.Lower = S.ConcreteWall; s.Panels = false; s.CeilKind = 4; s.WallA = Color.Lerp(Color.white, p.Wall, 0.25f); s.Trim = S.Iron; s.ChairRail = false; }
                    break;
                case RoomType.Stairwell:
                    s.Floor = S.StoneFloor; s.Paper = S.StoneWall; s.WallA = Color.Lerp(Color.white, p.Wall, 0.35f); s.Panels = false; s.Dentils = false; s.Lower = S.StoneWall; s.LowerTint = s.WallA * 0.8f; s.ChairRail = false;
                    break;
                case RoomType.Dining:
                    s.Floor = S.Herringbone; s.CeilKind = 1; s.Paper = S.PaperDamask; s.LowerH = 1.2f; break;
                case RoomType.Kitchen:
                    s.Floor = S.FloorCheckerTile; s.FloorA = Color.Lerp(Color.white, p.FloorA, 0.3f); s.FloorB = PaletteDark(p);
                    s.Lower = S.WallTile; s.LowerH = 1.7f; s.Panels = false; s.Paper = S.Plaster; s.WallA = Color.Lerp(Color.white, p.Wall, 0.6f); s.LowerTint = Color.Lerp(Color.white, p.Accent2, 0.25f); s.Dentils = false; s.ChairRail = false; s.Trim = S.Brass; break;
                case RoomType.Lounge:
                case RoomType.Parlor:
                    s.Floor = S.Herringbone; s.CeilKind = r.Type == RoomType.Lounge ? 1 : 0; s.Paper = eyes ? S.PaperEyes : s.Paper; break;
                case RoomType.Library:
                case RoomType.Archive:
                case RoomType.Study:
                    s.Floor = S.Herringbone; s.CeilKind = 1; s.Paper = S.PaperStripe; s.LowerH = 1.1f; break;
                case RoomType.MusicRoom:
                    s.Floor = S.Parquet; s.Paper = S.PaperMoon; s.CeilKind = 1; break;
                case RoomType.Theater:
                case RoomType.EmptyAuditorium:
                    s.Floor = S.Carpet; s.FloorA = Muted(p.Carpet); s.Paper = S.Velvet; s.WallA = Color.Lerp(p.Fabric, Color.black, 0.2f); s.Lower = S.WoodDark; s.LowerH = 1.1f; s.CeilKind = r.Type == RoomType.Theater ? 1 : 0; s.Dentils = r.Type == RoomType.Theater;
                    s.CeilTint = Color.Lerp(p.Wall, Color.black, 0.5f); s.Windows = false; break;
                case RoomType.Greenhouse:
                    s.Floor = S.Mosaic; s.Paper = S.Window; s.Lower = S.Brick; s.LowerH = 0.9f; s.Panels = false; s.LowerTint = Color.Lerp(Color.white, p.Wall, 0.2f); s.CeilKind = 3; s.Dentils = false; s.Trim = S.Iron; s.ChairRail = true; s.Windows = false; break;
                case RoomType.Courtyard:
                    s.Floor = S.Mosaic; s.Paper = S.StoneWall; s.Lower = S.StoneWall; s.Panels = false; s.LowerTint = Color.Lerp(Color.white, p.Wall, 0.25f); s.WallA = s.LowerTint; s.CeilKind = 2; s.Dentils = false; s.ChairRail = false; s.Windows = true; s.Crown = false; break;
                case RoomType.Infirmary:
                    s.Floor = S.FloorTile; s.Lower = S.WallTile; s.LowerH = 1.6f; s.Panels = false; s.Paper = S.Plaster; s.WallA = Color.Lerp(Color.white, p.Wall, 0.25f); s.LowerTint = Color.Lerp(Color.white, p.Accent2, 0.15f); s.Dentils = false; s.ChairRail = false; s.Trim = S.Steel; break;
                case RoomType.Laundry:
                    s.Floor = S.WornTile; s.Lower = S.WallTile; s.LowerH = 1.5f; s.Panels = false; s.Paper = S.Plaster; s.WallA = Color.Lerp(Color.white, p.Wall, 0.35f); s.Dentils = false; s.ChairRail = false; s.CeilKind = 4; s.Trim = S.Iron; break;
                case RoomType.Workshop:
                    s.Floor = S.WoodWorn; s.Paper = S.Plaster; s.WallA = Color.Lerp(Color.white, p.Wall, 0.5f); s.Dentils = false; s.CeilKind = 4; break;
                case RoomType.Storage:
                case RoomType.Closet:
                    s.Floor = r.Floor < 0 ? S.Concrete : S.WoodWorn; s.Paper = r.Floor < 0 ? S.Brick : S.Plaster; s.Lower = r.Floor < 0 ? S.ConcreteWall : S.Wainscot; s.Panels = false; s.Dentils = false; s.ChairRail = false; s.CeilKind = r.Floor < 0 ? 4 : 0; s.WallA = Color.Lerp(Color.white, p.Wall, 0.3f); break;
                case RoomType.GameRoom:
                    s.Floor = S.CarpetJacquard; s.FloorA = Muted(p.Carpet); s.Paper = S.PaperStripe; s.CeilKind = 1; break;
                case RoomType.Pool:
                    s.Floor = S.PoolTile; s.FloorA = Color.Lerp(Color.white, p.Accent2, 0.15f); s.Lower = S.WallTile; s.LowerH = 2.2f; s.Panels = false; s.Paper = S.PaperMoon; s.LowerTint = Color.Lerp(Color.white, p.Wall, 0.3f); s.Dentils = false; s.ChairRail = true; s.CeilKind = 3; break;
                case RoomType.WaterRoom:
                case RoomType.PowerRoom:
                case RoomType.MachineRoom:
                case RoomType.BoilerRoom:
                    s.Floor = r.Type == RoomType.BoilerRoom ? S.Concrete : S.MetalPlate; s.Paper = S.ConcreteWall; s.Lower = r.Type == RoomType.WaterRoom ? S.WallTile : S.PaintedMetal; s.LowerH = 1.2f; s.Panels = false; s.Dentils = false; s.ChairRail = false;
                    s.CeilKind = 4; s.WallA = Color.Lerp(Color.white, p.Wall, 0.3f); s.LowerTint = Color.Lerp(p.Wall, Color.white, 0.3f); s.Trim = S.Iron; s.Windows = false; break;
                case RoomType.Gallery:
                    s.Floor = S.MarbleFloor; s.FloorA = Color.Lerp(Color.white, p.FloorA, 0.3f); s.Paper = S.PaperDamask2; s.CeilKind = 1; s.LowerH = 1.0f; break;
                case RoomType.Wardrobe:
                    s.Floor = S.Carpet; s.FloorA = Muted(p.Carpet); s.Paper = S.PaperStripe; break;
                case RoomType.Chapel:
                    s.Floor = S.StoneFloor; s.Paper = S.StoneWall; s.WallA = Color.Lerp(Color.white, p.Wall, 0.3f); s.CeilKind = 7; s.Crown = false; s.Stained = true; s.Dentils = false; break;
                case RoomType.Bedroom:
                case RoomType.GuestRoom:
                    s.Floor = (r.Variant % 2 == 0) ? S.Carpet : S.Herringbone; s.FloorA = Color.Lerp(Muted(p.Carpet), Color.white, 0.15f);
                    if (r.Type == RoomType.GuestRoom) s.Paper = S.PaperDamask2;
                    {
                        // a resident's own floor and walls (OwnerStyles): brick for the rapper and the engineer, plain plaster for the
                        // restorer and the labourer, watching-eye paper for the psychologist...
                        var own = r.Type == RoomType.Bedroom ? OwnerStyles.Get(r.Owner) : null;
                        if (own != null)
                        {
                            s.Floor = own.Floor; s.Paper = own.Paper;
                            if (own.Floor == S.Carpet) s.FloorA = Color.Lerp(Muted(p.Carpet), Color.white, 0.1f);
                            else if (own.Floor == S.MarbleFloor || own.Floor == S.Terrazzo || own.Floor == S.WornTile) s.FloorA = Color.Lerp(Color.white, p.FloorA, 0.3f);
                            else s.FloorA = Color.Lerp(Color.white, p.Wood * 2f, 0.25f);
                            if (own.Paper == S.Plaster || own.Paper == S.Brick) s.WallA = Color.Lerp(Color.white, p.Wall, 0.55f);
                            if (own.Paper == S.Brick) { s.Panels = false; s.LowerH = 0.9f; }
                        }
                    }
                    break;
                case RoomType.ButlerRoom:
                    s.Floor = S.FloorCheckerTile; s.FloorB = PaletteDark(p); s.Paper = S.PaperMoon; break;
                case RoomType.Elevator:
                    s.Floor = S.FloorChecker; s.FloorB = PaletteDark(p); s.Paper = S.PaperStripe; s.Lower = S.WoodCherry; s.Trim = S.Brass; s.Windows = false; break;
                case RoomType.DollRoom:
                    s.Floor = S.FloorCheckerTile; s.FloorA = Color.Lerp(Color.white, p.FloorA, 0.4f); s.FloorB = p.Ink; s.Paper = r.Variant % 2 == 0 ? S.PaperFlesh : S.PaperStripe; s.WallA = p.Wall; break;
                case RoomType.TrophyRoom:
                    s.Floor = S.Herringbone; s.Paper = S.PaperDamask; s.CeilKind = 1; break;
                case RoomType.WineCellar:
                    s.Floor = S.BrickFloor; s.Paper = S.Brick; s.Lower = S.Brick; s.Panels = false; s.Dentils = false; s.ChairRail = false; s.CeilKind = 4; s.WallA = Color.Lerp(Color.white, p.Wall, 0.25f); s.LowerTint = s.WallA * 0.85f; s.Trim = S.Iron; break;
                case RoomType.TeaRoom:
                    s.Floor = S.FloorCheckerTile; s.FloorA = Color.Lerp(Color.white, p.FloorA, 0.3f); s.FloorB = p.Ink; s.Paper = S.PaperMoon; break;
                // ----- mystery rooms
                case RoomType.RainCorridor:
                    s.Floor = S.WetStone; s.Paper = S.PaperStripe; s.WallA = Color.Lerp(p.Wall, Color.black, 0.2f); s.Lower = S.WallTile; s.LowerTint = Color.Lerp(p.Wall, Color.white, 0.2f); s.Panels = false; s.LowerH = 1.4f; s.CeilKind = 0; s.CeilTint = Color.Lerp(p.Wall, Color.black, 0.5f); s.Dentils = false; break;
                case RoomType.WaitingRoom:
                    s.Floor = S.Terrazzo; s.FloorA = Color.Lerp(Color.white, p.FloorA, 0.3f); s.Paper = S.Plaster; s.WallA = Color.Lerp(Color.white, p.Wall, 0.55f); s.Lower = S.WallTile; s.LowerH = 1.3f; s.Panels = false; s.Dentils = false; s.ChairRail = true; s.Trim = S.Steel; break;
                case RoomType.WhiteDoors:
                    s.Floor = S.MarbleFloor; s.FloorA = new Color(1.1f, 1.1f, 1.1f); s.Paper = S.PlasterWhite; s.WallA = new Color(1.15f, 1.15f, 1.15f); s.Lower = S.PlasterWhite; s.LowerTint = s.WallA; s.Panels = false; s.Dentils = false; s.ChairRail = false; s.Crown = true; s.Trim = S.PlasterWhite; s.TrimTint = s.WallA; s.CeilTint = s.WallA; s.Base = S.PlasterWhite; s.Windows = false; break;
                case RoomType.MirrorWater:
                    s.Floor = S.PoolTile; s.FloorA = Color.Lerp(Color.white, p.Accent2, 0.1f); s.Paper = S.PaperMoon; s.CeilKind = 2; s.Lower = S.WallTile; s.LowerH = 0.8f; s.Panels = false; s.Dentils = false; s.Windows = false; break;
                case RoomType.ClockMuseum:
                    s.Floor = S.FloorChecker; s.FloorB = PaletteDark(p); s.Paper = S.PaperStripe; s.CeilKind = 1; break;
                case RoomType.Courtroom:
                    s.Floor = S.Obsidian; s.Windows = false; break;
                // ----- rooms that help a crime: legible at a glance, uneasy up close
                case RoomType.Incinerator:
                    s.Floor = S.Concrete; s.Paper = S.Brick; s.Lower = S.ConcreteWall; s.LowerH = 1.0f; s.Panels = false; s.Dentils = false; s.ChairRail = false; s.CeilKind = 4;
                    s.WallA = new Color(0.45f, 0.36f, 0.32f); s.LowerTint = new Color(0.32f, 0.3f, 0.28f); s.CeilTint = new Color(0.18f, 0.16f, 0.15f); s.Trim = S.Iron; s.Windows = false; break;
                case RoomType.ColdStorage:
                    s.Floor = S.FloorTile; s.FloorA = new Color(0.85f, 0.9f, 0.95f); s.Lower = S.WallTile; s.LowerH = 2.4f; s.Panels = false; s.Paper = S.Plaster; s.WallA = new Color(0.78f, 0.86f, 0.92f);
                    s.LowerTint = new Color(0.82f, 0.9f, 0.95f); s.Dentils = false; s.ChairRail = false; s.CeilKind = 4; s.CeilTint = new Color(0.7f, 0.75f, 0.8f); s.Trim = S.Steel; s.Windows = false; break;
                case RoomType.Darkroom:
                    s.Floor = S.WoodWorn; s.FloorA = new Color(0.35f, 0.3f, 0.28f); s.Paper = S.Plaster; s.WallA = new Color(0.12f, 0.1f, 0.1f); s.Lower = S.WoodDark; s.LowerTint = new Color(0.3f, 0.26f, 0.24f);
                    s.Panels = true; s.Dentils = false; s.CeilKind = 0; s.CeilTint = new Color(0.1f, 0.09f, 0.09f); s.Windows = false; break;
            }
            s.Teeth = r.Type == RoomType.DollRoom || r.Type == RoomType.TrophyRoom || (RoomInfo.IsMystery(r.Type) && r.Type != RoomType.WhiteDoors);
            if (r.Floor < 0) s.Windows = false;
            if (RoomInfo.IsMystery(r.Type)) s.DoorScale = 1.38f;
            return s;
        }

        /// <summary>Wall-to-wall carpet a shade quieter than the palette jewel, so a room is not one flat colour wash.</summary>
        static Color Muted(Color c) { float l = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f; return Color.Lerp(c, new Color(l, l, l), 0.35f) * 0.92f; }

        // dark checker squares: the palette's dark tone, muted toward a warm near-black stone (a purple palette must give an
        // aubergine-black floor, never a magenta one)
        static Color PaletteDark(MansionPalette p) { var c = Color.Lerp(p.FloorB, p.Ink, 0.25f); float l = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f; return Color.Lerp(c, new Color(l * 1.05f, l, l * 0.92f), 0.55f) * 1.1f; }

        // ------------------------------------------------------------------ doors & holes planning
        void PlanDoorsAndHoles()
        {
            foreach (var d in Layout.Doors)
            {
                var ra = Layout.Room(d.RoomA); var rb = Layout.Room(d.RoomB);
                float minCeil = Math.Min(ra?.CeilingH ?? 4f, rb?.CeilingH ?? 4f);
                float scale = Math.Max(Rooms[d.RoomA]?.Style.DoorScale ?? 1f, Rooms[d.RoomB]?.Style.DoorScale ?? 1f);
                float h = (d.Width >= 2.8f ? 3.3f : d.Width >= 1.9f ? 2.95f : 2.45f) * scale;
                if (ra != null && ra.Type == RoomType.Elevator) h = 3.0f;
                h = Math.Min(h, minCeil - 0.3f);
                _doorH[d.Id] = h;
            }
            foreach (var fi in Layout.Floors) _floorHoles[fi.F] = new List<RectF>();
            // service stair shafts: upper floor needs an opening where the flight passes head height
            foreach (var st in Layout.Stairs)
            {
                if (st.Grand || (st.Name == "심판장 승강기" || st.Name == "재판장 승강기") || Math.Abs(st.A.f - st.B.f) != 1) continue;
                var lo = st.A.f < st.B.f ? st.A : st.B; var hi = st.A.f < st.B.f ? st.B : st.A;
                float yLo = Layout.FloorY(lo.f), yHi = Layout.FloorY(hi.f);
                float rise = yHi - yLo;
                float ceilLo = yLo + (Layout.Room(Layout.RoomAt(lo))?.CeilingH ?? 3.8f);
                // parameter t (0 at lo, 1 at hi) where the flight is 2.45 m below the lower room's ceiling: from there up the
                // slab is cut away, so nobody (1.72 m capsule, eyes at 1.62 m) ever brushes it on the way down
                float tHole = Mathf.Clamp01((ceilLo - 2.45f - yLo) / rise);
                Vector2 a = new Vector2(lo.x, lo.z), b = new Vector2(hi.x, hi.z);
                Vector2 p0 = Vector2.Lerp(a, b, tHole - 0.04f), p1 = b + (b - a).normalized * 0.05f;
                float w = StairWidth(st) * 0.5f + 0.05f;
                RectF hole = Math.Abs(a.x - b.x) > Math.Abs(a.y - b.y)
                    ? new RectF(Math.Min(p0.x, p1.x), a.y - w, Math.Max(p0.x, p1.x), a.y + w)
                    : new RectF(a.x - w, Math.Min(p0.y, p1.y), a.x + w, Math.Max(p0.y, p1.y));
                _floorHoles[hi.f].Add(hole);
            }
            // pool basins (+ a synthetic basin when the layout could not place PoolWater in a Pool room)
            foreach (var f in Layout.Furniture)
            {
                if (f.Type != "PoolWater") continue;
                NavGrid.GetFootprint(f, 0, out var fr);
                _floorHoles[f.Pos.f].Add(fr);
                _poolHoles.Add(fr);
            }
            SynthPools.Clear();
            foreach (var r in Layout.Rooms)
            {
                if (r.Type != RoomType.Pool || r.Furniture.Any(fid => Layout.Furniture[fid].Type == "PoolWater")) continue;
                var ir = r.Rect.Inset(0.12f); float pw = Math.Max(4f, ir.W - 5f), pd = Math.Max(3f, ir.D - 5f);
                var sf = new Furniture { Id = -1000 - r.Id, Room = r.Id, Type = "PoolWater", Pos = new P3(r.Floor, ir.CX, ir.CZ), Yaw = 0, W = pw, D = pd, H = -1.6f, Material = Mat.Liquid, Blocks = false };
                SynthPools.Add(sf);
                var hr = new RectF(ir.CX - pw / 2, ir.CZ - pd / 2, ir.CX + pw / 2, ir.CZ + pd / 2);
                _floorHoles[r.Floor].Add(hr); _poolHoles.Add(hr);
            }
            foreach (var rv in Rooms)
            {
                if (rv == null) continue;
                var r = rv.Room;
                foreach (var h in _floorHoles[r.Floor]) if (h.Overlaps(r.Rect)) rv.FloorHoles.Add(Clip(h, r.Rect));
                if (_floorHoles.TryGetValue(r.Floor + 1, out var up))
                    foreach (var h in up) if (h.Overlaps(r.Rect)) rv.CeilHoles.Add(Clip(h, r.Rect));
            }
        }

        internal float StairWidth(Stair st)
        {
            if (st.Grand) return 5f;
            var rm = Layout.Room(st.RoomA);
            if (rm == null) return 1.6f;
            float across = rm.Rect.W > rm.Rect.D ? rm.Rect.D : rm.Rect.W;
            // a wide well holds two side-by-side flights (see LayoutGenerator): each gets half the well, generously
            if (across >= 5.5f) return Mathf.Min(2.6f, across * 0.5f - 0.4f);
            return Mathf.Min(1.7f, across - 0.5f);
        }

        static RectF Clip(RectF a, RectF b) => new RectF(Math.Max(a.x0, b.x0), Math.Max(a.z0, b.z0), Math.Min(a.x1, b.x1), Math.Min(a.z1, b.z1));

        /// <summary>Subtract holes from a rectangle -> list of rectangles (grid decomposition).</summary>
        internal static List<RectF> Subtract(RectF r, IList<RectF> holes)
        {
            var res = new List<RectF>();
            if (holes == null || holes.Count == 0) { res.Add(r); return res; }
            var xs = new List<float> { r.x0, r.x1 }; var zs = new List<float> { r.z0, r.z1 };
            foreach (var h in holes) { if (!h.Overlaps(r)) continue; xs.Add(Mathf.Clamp(h.x0, r.x0, r.x1)); xs.Add(Mathf.Clamp(h.x1, r.x0, r.x1)); zs.Add(Mathf.Clamp(h.z0, r.z0, r.z1)); zs.Add(Mathf.Clamp(h.z1, r.z0, r.z1)); }
            xs = xs.Distinct().OrderBy(x => x).ToList(); zs = zs.Distinct().OrderBy(z => z).ToList();
            // merge cells row by row into strips
            for (int j = 0; j < zs.Count - 1; j++)
            {
                float z0 = zs[j], z1 = zs[j + 1]; if (z1 - z0 < 1e-3f) continue;
                float runStart = float.NaN;
                for (int i = 0; i < xs.Count - 1; i++)
                {
                    float x0 = xs[i], x1 = xs[i + 1]; if (x1 - x0 < 1e-3f) continue;
                    float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
                    bool inHole = false; foreach (var h in holes) if (cx > h.x0 && cx < h.x1 && cz > h.z0 && cz < h.z1) { inHole = true; break; }
                    if (!inHole) { if (float.IsNaN(runStart)) runStart = x0; }
                    else if (!float.IsNaN(runStart)) { res.Add(new RectF(runStart, z0, x0, z1)); runStart = float.NaN; }
                }
                if (!float.IsNaN(runStart)) res.Add(new RectF(runStart, z0, xs[xs.Count - 1], z1));
            }
            return res;
        }

        // ------------------------------------------------------------------ shells
        enum Kind { Solid, Door, Open, VoidEdge }
        sealed class Interval { public float a, b; public Kind K; public int Door = -1; public int Other = -1; public bool Exterior; public WallSeg Seg; }
        sealed class Strip
        {
            public int Room; public bool AlongX; public float Line; public int Sign; // normal = +/- axis perpendicular
            public List<Interval> Iv = new List<Interval>();
            public float A => Iv[0].a; public float B => Iv[Iv.Count - 1].b;
        }
        internal readonly List<(WallSeg seg, int room, int sign)> _voidEdges = new List<(WallSeg, int, int)>();

        void BuildShells()
        {
            // 1) gather intervals per room side
            var map = new Dictionary<(int room, bool alongX, int line, int sign), List<Interval>>();
            foreach (var w in Layout.Walls())
            {
                bool alongX = Math.Abs(w.z0 - w.z1) < 1e-4f;
                float line = alongX ? w.z0 : w.x0;
                var g = _grids[w.Floor];
                float mid = alongX ? (w.x0 + w.x1) * 0.5f : (w.z0 + w.z1) * 0.5f;
                int lowSide = alongX ? g.At(mid, line - 0.25f) : g.At(line - 0.25f, mid);
                bool voidEdge = (w.RoomA >= 0 && Layout.Rooms[w.RoomA].Void) || (w.RoomB >= 0 && Layout.Rooms[w.RoomB].Void);
                foreach (int side in new[] { w.RoomA, w.RoomB })
                {
                    if (side < 0) continue;
                    int sign = lowSide == side ? -1 : 1;          // normal points into the room
                    int other = side == w.RoomA ? w.RoomB : w.RoomA;
                    if (voidEdge) { if (!Layout.Rooms[side].Void) _voidEdges.Add((w, side, sign)); continue; }
                    var iv = new Interval
                    {
                        a = alongX ? Math.Min(w.x0, w.x1) : Math.Min(w.z0, w.z1), b = alongX ? Math.Max(w.x0, w.x1) : Math.Max(w.z0, w.z1),
                        K = w.DoorId >= 0 ? Kind.Door : w.Open ? Kind.Open : Kind.Solid, Door = w.DoorId, Other = other, Exterior = other < 0, Seg = w
                    };
                    var key = (side, alongX, (int)Math.Round(line * 2), sign);
                    if (!map.TryGetValue(key, out var list)) map[key] = list = new List<Interval>();
                    list.Add(iv);
                }
            }
            // 2) merge into strips and build
            foreach (var kv in map)
            {
                var list = kv.Value.OrderBy(i => i.a).ToList();
                Strip cur = null;
                foreach (var iv in list)
                {
                    if (cur != null && Math.Abs(cur.B - iv.a) < 1e-3f) { cur.Iv.Add(iv); continue; }
                    if (cur != null) BuildStrip(cur);
                    cur = new Strip { Room = kv.Key.room, AlongX = kv.Key.alongX, Line = kv.Key.line / 2f, Sign = kv.Key.sign };
                    cur.Iv.Add(iv);
                }
                if (cur != null) BuildStrip(cur);
            }
            // 3) wall colliders from segments (once per segment)
            var colRoot = new GameObject("WallColliders"); colRoot.transform.SetParent(_root, false);
            colRoot.layer = 0;
            foreach (var w in Layout.Walls())
            {
                bool voidEdge = (w.RoomA >= 0 && Layout.Rooms[w.RoomA].Void) || (w.RoomB >= 0 && Layout.Rooms[w.RoomB].Void);
                if (voidEdge) continue;
                bool alongX = Math.Abs(w.z0 - w.z1) < 1e-4f;
                float y0 = Layout.FloorY(w.Floor);
                float top = NextFloorBase(w.Floor);
                if (w.Floor == -2) top = y0 + 10.5f;
                float lo = y0;
                if (w.DoorId >= 0) lo = y0 + _doorH[w.DoorId];
                else if (w.Open)
                {
                    float ha = Layout.Rooms[w.RoomA].CeilingH, hb = w.RoomB >= 0 ? Layout.Rooms[w.RoomB].CeilingH : ha;
                    lo = y0 + Math.Min(ha, hb);
                    if (Layout.Rooms[w.RoomA].Type == RoomType.GrandHall || (w.RoomB >= 0 && Layout.Rooms[w.RoomB].Type == RoomType.GrandHall)) lo = y0 + 4.4f;
                }
                if (top - lo < 0.05f) continue;
                var c = new Vector3((w.x0 + w.x1) * 0.5f, (lo + top) * 0.5f, (w.z0 + w.z1) * 0.5f);
                var size = alongX ? new Vector3(Math.Abs(w.x1 - w.x0) + 0.2f, top - lo, 0.2f) : new Vector3(0.2f, top - lo, Math.Abs(w.z1 - w.z0) + 0.2f);
                var bc = colRoot.AddComponent<BoxCollider>(); bc.center = c; bc.size = size;
            }
            // 4) floors & ceilings
            foreach (var rv in Rooms)
            {
                if (rv == null) continue;
                BuildFloorAndCeiling(rv);
            }
        }

        // Face helpers ---------------------------------------------------------------------------
        Vector3 P(Strip s, float along, float y, float off)
        {
            // off measured into the room from the wall line
            float n = s.Sign * off;
            return s.AlongX ? new Vector3(along, y, s.Line + n) : new Vector3(s.Line + n, y, along);
        }
        Vector3 N(Strip s) => s.AlongX ? new Vector3(0, 0, s.Sign) : new Vector3(s.Sign, 0, 0);
        Vector3 Along(Strip s) => s.AlongX ? Vector3.right : Vector3.forward;

        void WallRect(MeshBuilder mb, Strip s, float a, float b, float y0, float y1, float off)
        {
            if (b - a < 1e-4f || y1 - y0 < 1e-4f) return;
            mb.QuadAuto(P(s, a, y0, off), P(s, a, y1, off), P(s, b, y1, off), P(s, b, y0, off), N(s));
        }

        /// <summary>Box protruding from the wall face (along a..b, y0..y1, depth d from the face at 0.1).</summary>
        void WallBox(MeshBuilder mb, Strip s, float a, float b, float y0, float y1, float d, float baseOff = 0.1f)
        {
            Vector3 p0 = P(s, a, y0, baseOff), p1 = P(s, b, y1, baseOff + d);
            mb.BoxMM(Vector3.Min(p0, p1), Vector3.Max(p0, p1), MeshBuilder.Faces.All);
        }

        void BuildStrip(Strip s)
        {
            var rv = Rooms[s.Room]; var r = rv.Room; var st = rv.Style; var mb = rv.Shell;
            if (r.Void) return;
            float fy = rv.FloorY, cy = rv.CeilY, top = rv.WallTop;
            if (r.Type == RoomType.Courtroom) return; // circular room built by CourtroomView
            var g = _grids[r.Floor];
            // corner extension (convex corners)
            float A = s.A, B = s.B;
            float probeOff = 0.25f * s.Sign;
            int before = s.AlongX ? g.At(A - 0.25f, s.Line + probeOff) : g.At(s.Line + probeOff, A - 0.25f);
            int after = s.AlongX ? g.At(B + 0.25f, s.Line + probeOff) : g.At(s.Line + probeOff, B + 0.25f);
            float ea = before == s.Room ? -0.1f : 0f, eb = after == s.Room ? 0.1f : 0f;

            var rnd = rv.Rng;
            float lowH = st.LowerH;
            float ceilRel = cy - fy;
            Color wallA = st.WallA, ink = st.WallB;
            var pal = rv.Pal;

            for (int k = 0; k < s.Iv.Count; k++)
            {
                var iv = s.Iv[k];
                float a = iv.a + (k == 0 ? ea : 0), b = iv.b + (k == s.Iv.Count - 1 ? eb : 0);
                switch (iv.K)
                {
                    case Kind.Solid:
                        {
                            // windows on exterior walls
                            var wins = new List<(float c, float w, float sill, float h)>();
                            if (iv.Exterior && st.Windows && r.Floor >= 0 && r.Type != RoomType.Corridor || (iv.Exterior && r.Type == RoomType.Corridor && r.Floor >= 0))
                                PlanWindows(rv, iv.a, iv.b, wins, s);
                            float cursor = a;
                            foreach (var w in wins)
                            {
                                float wa = w.c - w.w * 0.5f, wb = w.c + w.w * 0.5f;
                                SolidWall(rv, s, cursor, wa, 0, top - fy, true, true);
                                SolidWall(rv, s, wa, wb, 0, w.sill, true, true);           // below sill
                                SolidWall(rv, s, wa, wb, w.sill + w.h, top - fy, false, true, w.sill + w.h - w.w * 0.5f, w.c, w.w * 0.5f); // above (arch infill)
                                BuildWindow(rv, s, w.c, w.w, w.sill, w.h);
                                cursor = wb;
                            }
                            SolidWall(rv, s, cursor, b, 0, top - fy, true, true);
                            Pilasters(rv, s, a, b, k > 0 && s.Iv[k - 1].K != Kind.Solid, k < s.Iv.Count - 1 && s.Iv[k + 1].K != Kind.Solid, wins);
                            break;
                        }
                    case Kind.Door:
                        {
                            float dh = _doorH.TryGetValue(iv.Door, out var hh) ? hh : 2.4f;
                            SolidWall(rv, s, a, b, dh, top - fy, false, true);
                            break;
                        }
                    case Kind.Open:
                        {
                            var other = iv.Other >= 0 ? Rooms[iv.Other] : null;
                            float otherCeil = other != null ? other.CeilY - other.FloorY : ceilRel;
                            float openTop = Math.Min(ceilRel, otherCeil);
                            if (r.Type == RoomType.GrandHall || (other != null && other.Room.Type == RoomType.GrandHall)) openTop = Math.Min(openTop, 4.4f);
                            // arch: slightly lower header with a rib, for rhythm
                            float archTop = openTop - 0.35f;
                            SolidWall(rv, s, a, b, archTop, top - fy, false, true);
                            // soffit + jambs (built once, from the lower id side)
                            if (other == null || s.Room < iv.Other)
                            {
                                mb.Set(st.Trim == S.Iron ? S.Iron : S.WoodDark, Color.white);
                                Vector3 n = N(s);
                                // soffit spanning the wall thickness
                                mb.QuadAuto(P(s, a, archTop, 0.1f), P(s, b, archTop, 0.1f), P(s, b, archTop, -0.1f), P(s, a, archTop, -0.1f), Vector3.down);
                                // jambs where the neighbouring interval is a wall
                                bool prevWall = k > 0 && s.Iv[k - 1].K == Kind.Solid, nextWall = k < s.Iv.Count - 1 && s.Iv[k + 1].K == Kind.Solid;
                                if (prevWall) mb.QuadAuto(P(s, a, 0, 0.1f), P(s, a, archTop, 0.1f), P(s, a, archTop, -0.1f), P(s, a, 0, -0.1f), Along(s));
                                if (nextWall) mb.QuadAuto(P(s, b, 0, 0.1f), P(s, b, archTop, 0.1f), P(s, b, archTop, -0.1f), P(s, b, 0, -0.1f), -Along(s));
                                // decorative rib under the soffit (teeth for grotesque rooms)
                                var dmb = rv.Detail; dmb.Set(S.Bone, Color.white);
                                if (st.Teeth && b - a > 1.2f)
                                    for (float t = a + 0.12f; t < b - 0.08f; t += 0.16f)
                                    {
                                        float len = 0.06f + (float)rnd.NextDouble() * 0.05f + (rnd.NextDouble() < 0.06 ? 0.12f : 0);
                                        Vector3 c0 = P(s, t, archTop - len, 0.0f);
                                        dmb.Box(c0 + Vector3.up * len * 0.5f, s.AlongX ? new Vector3(0.05f, len, 0.12f) : new Vector3(0.12f, len, 0.05f));
                                    }
                            }
                            break;
                        }
                }
            }

            // crown molding + dentils along the whole strip at the ceiling line
            if (st.Crown && r.Type != RoomType.Greenhouse)
            {
                bool openWholeTop = false;
                if (!openWholeTop) Crown(rv, s, A + ea, B + eb, ceilRel);
            }
            // store wall slots for decor (points on solid intervals, at eye height)
            foreach (var iv in s.Iv)
            {
                if (iv.K != Kind.Solid) continue;
                for (float t = iv.a + 0.9f; t <= iv.b - 0.9f; t += 1.3f)
                {
                    var p = P(s, t, fy + 1.7f, 0.1f);
                    rv.WallSlots.Add(p);
                    rv.WallSlotN.Add(N(s));
                    rv.WallSlotRange.Add(new Vector2(iv.a, iv.b));
                }
            }
        }

        /// <summary>Engaged pilasters (plinth, fluted shaft, gilt-necked capital) marking the bays of a formal wall: at the
        /// ends of every solid run (clear of door architraves and window reveals) and every ~3.3 m between.</summary>
        void Pilasters(RoomView rv, Strip s, float a, float b, bool doorBefore, bool doorAfter, List<(float c, float w, float sill, float h)> wins)
        {
            var st = rv.Style; var r = rv.Room;
            if (!st.Panels || st.Lower != S.Wainscot || r.Floor < 0 || st.Teeth) return;
            float fy = rv.FloorY, ceilRel = rv.CeilY - fy;
            float k = ceilRel > 5f ? 1.35f : ceilRel > 4.1f ? 1.15f : 1f;
            float topY = ceilRel - 0.31f * k;
            var runs = new List<(float a, float b, float ma, float mb)>();
            float cur = a, curM = doorBefore ? 0.45f : 0.25f;
            foreach (var w in wins) { runs.Add((cur, w.c - w.w * 0.5f, curM, 0.32f)); cur = w.c + w.w * 0.5f; curM = 0.32f; }
            runs.Add((cur, b, curM, doorAfter ? 0.45f : 0.25f));
            var mb = rv.Shell;
            foreach (var run in runs)
            {
                float len = run.b - run.a;
                var pos = new List<float>();
                if (len < 0.75f) continue;
                if (len < 1.5f) pos.Add((run.a + run.b) * 0.5f);
                else
                {
                    float p0 = run.a + run.ma, p1 = run.b - run.mb;
                    int n = Math.Max(1, (int)Math.Round((p1 - p0) / 3.3f));
                    for (int i = 0; i <= n; i++) pos.Add(Mathf.Lerp(p0, p1, i / (float)n));
                }
                foreach (float t in pos)
                {
                    Color wood = Color.Lerp(st.LowerTint, Color.white, 0.08f);
                    { var pw = P(s, t, fy, 0); rv.WallReserved.Add(new Vector4(pw.x, pw.z, 0.2f, 0)); }   // pictures and sconces keep off
                    mb.Set(st.Lower, wood);
                    WallBox(mb, s, t - 0.19f, t + 0.19f, fy, fy + 0.34f, 0.09f);                       // plinth
                    WallBox(mb, s, t - 0.14f, t + 0.14f, fy + 0.34f, fy + topY - 0.26f, 0.055f);        // shaft
                    mb.Set(st.Lower, wood * 0.72f);
                    foreach (float fl in new[] { -0.07f, 0f, 0.07f }) WallBox(mb, s, t + fl - 0.012f, t + fl + 0.012f, fy + 0.5f, fy + topY - 0.42f, 0.006f, 0.155f);   // flutes
                    mb.Set(S.Gold, st.TrimTint);
                    WallBox(mb, s, t - 0.16f, t + 0.16f, fy + topY - 0.26f, fy + topY - 0.22f, 0.07f);  // gilt necking
                    mb.Set(st.Lower, wood);
                    WallBox(mb, s, t - 0.2f, t + 0.2f, fy + topY - 0.22f, fy + topY, 0.1f);             // capital
                }
            }
        }

        /// <summary>Wall face piece from y0..y1 (relative to floor), with wainscot, baseboard and optional arch infill.</summary>
        void SolidWall(RoomView rv, Strip s, float a, float b, float y0, float y1, bool withBase, bool withUpper, float archSpring = -1, float archC = 0, float archR = 0)
        {
            if (b - a < 1e-3f || y1 - y0 < 1e-3f) return;
            var st = rv.Style; var mb = rv.Shell; float fy = rv.FloorY;
            float ceilRel = rv.CeilY - fy;
            float lowH = st.LowerH;
            // lower zone (wainscot / tiles)
            if (y0 < lowH)
            {
                float ly1 = Math.Min(y1, lowH);
                mb.Set(st.Lower, st.LowerTint, new Vector4(st.WallB.r, st.WallB.g, st.WallB.b, 0));
                WallRect(mb, s, a, b, fy + y0, fy + ly1, 0.1f);
                if (st.Panels && y0 < 0.3f && ly1 >= lowH - 0.01f && b - a > 0.5f)
                {
                    int n = Math.Max(1, (int)Math.Round((b - a) / 0.85f));
                    float pw = (b - a) / n;
                    for (int i = 0; i < n; i++)
                    {
                        float pa = a + i * pw + 0.09f, pb = a + (i + 1) * pw - 0.09f;
                        if (pb - pa < 0.15f) continue;
                        mb.Set(st.Lower, Color.Lerp(st.LowerTint, Color.white, 0.12f));
                        WallBox(mb, s, pa, pb, fy + 0.3f, fy + lowH - 0.14f, 0.018f);
                        mb.Set(st.Lower, st.LowerTint * 0.85f);
                        WallBox(mb, s, pa + 0.06f, pb - 0.06f, fy + 0.36f, fy + lowH - 0.2f, 0.03f);
                    }
                }
                if (st.ChairRail && ly1 >= lowH - 0.01f)
                {
                    mb.Set(st.Trim, st.TrimTint);
                    WallBox(mb, s, a, b, fy + lowH - 0.05f, fy + lowH + 0.03f, 0.04f);
                    WallBox(mb, s, a, b, fy + lowH + 0.03f, fy + lowH + 0.05f, 0.022f);
                }
            }
            // upper zone (wallpaper / plaster / special)
            if (withUpper && y1 > lowH)
            {
                float uy0 = Math.Max(y0, lowH), uy1 = Math.Min(y1, ceilRel);
                if (uy1 > uy0)
                {
                    int slot = st.Paper;
                    if (slot == S.Window)
                    {
                        // greenhouse glass walls: iron frame grid + glass panes glowing with sky
                        mb.Set(S.Window, Color.white, Vector4.zero);
                        float pane = 1.2f; int n = Math.Max(1, (int)Math.Round((b - a) / pane)); float pw = (b - a) / n;
                        for (int i = 0; i < n; i++)
                        {
                            float pa = a + i * pw, pb = pa + pw;
                            var q0 = P(s, pa, fy + uy0, 0.06f); var q1 = P(s, pa, fy + uy1, 0.06f); var q2 = P(s, pb, fy + uy1, 0.06f); var q3 = P(s, pb, fy + uy0, 0.06f);
                            if (Vector3.Dot(Vector3.Cross(q1 - q0, q2 - q0), N(s)) >= 0) mb.QuadUV(q0, q1, q2, q3, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
                            else mb.QuadUV(q3, q2, q1, q0, new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0));
                            mb.Set(S.Iron, new Color(0.3f, 0.32f, 0.3f));
                            WallBox(mb, s, pa - 0.03f, pa + 0.03f, fy + uy0, fy + uy1, 0.06f, 0.04f);
                            mb.Set(S.Window, Color.white, Vector4.zero);
                        }
                        mb.Set(S.Iron, new Color(0.3f, 0.32f, 0.3f));
                        WallBox(mb, s, a, b, fy + uy0 + (uy1 - uy0) * 0.62f, fy + uy0 + (uy1 - uy0) * 0.62f + 0.06f, 0.06f, 0.04f);
                    }
                    else
                    {
                        mb.Set(slot, st.WallA, new Vector4(st.WallB.r, st.WallB.g, st.WallB.b, 0));
                        if (archSpring > 0 && archR > 0.05f)
                        {
                            // arched opening top: fill between the arch curve and the rectangle top (uy0)
                            float rectTop = uy0; // window top (arch apex)
                            float spring = archSpring;
                            // area between spring..rectTop above arch
                            int segs = 10;
                            for (int i = 0; i < segs; i++)
                            {
                                float t0 = Mathf.PI * i / segs, t1 = Mathf.PI * (i + 1) / segs;
                                float x0 = archC - Mathf.Cos(t0) * archR, x1 = archC - Mathf.Cos(t1) * archR;
                                float h0 = spring + Mathf.Sin(t0) * archR, h1 = spring + Mathf.Sin(t1) * archR;
                                mb.QuadAuto(P(s, x0, fy + h0, 0.1f), P(s, x0, fy + rectTop, 0.1f), P(s, x1, fy + rectTop, 0.1f), P(s, x1, fy + h1, 0.1f), N(s));
                            }
                        }
                        WallRect(mb, s, a, b, fy + uy0, fy + uy1, 0.1f);
                    }
                }
                // hidden part above the ceiling (visible only through stair shafts)
                if (y1 > ceilRel + 0.01f)
                {
                    mb.Set(S.Plaster, st.CeilTint * 0.6f);
                    WallRect(mb, s, a, b, fy + Math.Max(ceilRel, y0), fy + y1, 0.1f);
                }
            }
            // baseboard
            if (withBase && y0 < 0.01f)
            {
                mb.Set(st.Base, st.Base == S.PlasterWhite ? st.WallA : Color.Lerp(Color.white, rv.Pal.Wood * 2f, 0.4f));
                WallBox(mb, s, a, b, fy, fy + 0.17f, 0.024f);
                mb.Set(st.Trim, st.TrimTint);
                WallBox(mb, s, a, b, fy + 0.17f, fy + 0.19f, 0.014f);
            }
        }

        /// <summary>Sweep a moulding profile (list of (offset from the wall line, height)) along a..b.</summary>
        void SweepProfile(MeshBuilder mb, Strip s, float a, float b, IList<Vector2> prof)
        {
            Vector3 n = N(s);
            for (int i = 0; i < prof.Count - 1; i++)
            {
                var p0 = prof[i]; var p1 = prof[i + 1];
                // outward normal of this facet in the (off, y) plane, off grows into the room
                Vector2 d = p1 - p0; Vector2 nn = new Vector2(d.y, -d.x).normalized;   // rotate: facing away from the wall/ceiling mass
                Vector3 want = n * nn.x + Vector3.up * nn.y;
                mb.QuadAuto(P(s, a, p0.y, p0.x), P(s, a, p1.y, p1.x), P(s, b, p1.y, p1.x), P(s, b, p0.y, p0.x), want);
            }
        }

        void Crown(RoomView rv, Strip s, float a, float b, float ceilRel)
        {
            var st = rv.Style; var mb = rv.Shell; float y = rv.FloorY + ceilRel;
            if (!st.Teeth)
            {
                // classical plaster cornice: cove + bead + fascia, gilded bead, a regular dentil course and a picture rail
                float k = ceilRel > 5f ? 1.35f : ceilRel > 4.1f ? 1.15f : 1f;
                bool dark = st.Trim == S.Iron || st.Ceil != S.Ceiling;
                int corniceSlot = dark ? S.WoodDark : S.Ceiling;
                Color corniceTint = dark ? Color.Lerp(Color.white, rv.Pal.Wood * 2f, 0.3f) : Color.Lerp(st.CeilTint, Color.white, 0.35f);
                mb.Set(corniceSlot, corniceTint);
                var prof = new List<Vector2>
                {
                    new Vector2(0.1f, y - 0.30f * k), new Vector2(0.125f, y - 0.30f * k), new Vector2(0.125f, y - 0.25f * k),       // fascia
                    new Vector2(0.15f, y - 0.24f * k), new Vector2(0.16f, y - 0.21f * k),                                              // bed mould
                    new Vector2(0.17f, y - 0.2f * k), new Vector2(0.2f, y - 0.14f * k), new Vector2(0.26f, y - 0.07f * k), new Vector2(0.33f * k + 0.02f, y - 0.025f * k), new Vector2(0.37f * k, y) // cove to ceiling
                };
                SweepProfile(mb, s, a, b, prof);
                // gilded bead under the cove
                mb.Set(S.Gold, st.TrimTint);
                WallBox(mb, s, a, b, y - 0.215f * k, y - 0.195f * k, 0.075f * k, 0.1f);
                // picture rail
                mb.Set(st.Trim == S.Iron ? S.Iron : S.WoodDark, Color.Lerp(Color.white, rv.Pal.Wood * 2f, 0.3f));
                WallBox(mb, s, a, b, y - 0.62f * k, y - 0.58f * k, 0.022f);
                mb.Set(S.Gold, st.TrimTint);
                WallBox(mb, s, a, b, y - 0.585f * k, y - 0.575f * k, 0.028f);
                if (!st.Dentils) return;
                var dmb = rv.Detail;
                dmb.Set(corniceSlot, Color.Lerp(corniceTint, Color.black, 0.08f));
                float step = 0.12f * k, tw = 0.055f * k;
                for (float t = a + step * 0.5f; t < b - step * 0.3f; t += step)
                {
                    var p0 = P(s, t - tw * 0.5f, y - 0.29f * k, 0.125f);
                    var p1 = P(s, t + tw * 0.5f, y - 0.245f * k, 0.125f + 0.05f * k);
                    dmb.BoxMM(Vector3.Min(p0, p1), Vector3.Max(p0, p1), MeshBuilder.Faces.All);
                }
                return;
            }
            mb.Set(st.Trim == S.Gold ? S.Gold : st.Trim, st.TrimTint);
            // stepped cornice
            WallBox(mb, s, a, b, y - 0.1f, y, 0.09f);
            WallBox(mb, s, a, b, y - 0.16f, y - 0.1f, 0.05f);
            mb.Set(S.WoodDark, Color.Lerp(Color.white, rv.Pal.Wood * 2f, 0.3f));
            WallBox(mb, s, a, b, y - 0.26f, y - 0.16f, 0.03f);
            if (!st.Dentils) return;
            // dentils: a row of teeth, some too long (detail mesh: no shadow casting, avoids blocky shadow rows)
            mb = rv.Detail;
            mb.Set(S.Bone, Color.white);
            var rnd = rv.Rng;
            for (float t = a + 0.06f; t < b - 0.04f; t += 0.11f)
            {
                float len = 0.07f + (float)rnd.NextDouble() * 0.015f;
                if (rnd.NextDouble() < 0.035) len += 0.1f + (float)rnd.NextDouble() * 0.08f; // a fang
                float tw = 0.05f;
                var p0 = P(s, t - tw * 0.5f, y - 0.16f - len, 0.13f);
                var p1 = P(s, t + tw * 0.5f, y - 0.16f, 0.13f + 0.045f);
                var mn = Vector3.Min(p0, p1); var mx = Vector3.Max(p0, p1);
                // taper: box + pointed tip for fangs
                mb.BoxMM(mn + Vector3.up * 0.02f, mx, MeshBuilder.Faces.All);
                var tip = (mn + mx) * 0.5f; tip.y = mn.y - 0.015f;
                var c0 = new Vector3(mn.x, mn.y + 0.02f, mn.z); var c1 = new Vector3(mx.x, mn.y + 0.02f, mn.z); var c2 = new Vector3(mx.x, mn.y + 0.02f, mx.z); var c3 = new Vector3(mn.x, mn.y + 0.02f, mx.z);
                mb.TriAuto(c0, c1, tip, (c0 + c1) * 0.5f - (mn + mx) * 0.5f + Vector3.down * 0.01f);
                mb.TriAuto(c1, c2, tip, (c1 + c2) * 0.5f - (mn + mx) * 0.5f + Vector3.down * 0.01f);
                mb.TriAuto(c2, c3, tip, (c2 + c3) * 0.5f - (mn + mx) * 0.5f + Vector3.down * 0.01f);
                mb.TriAuto(c3, c0, tip, (c3 + c0) * 0.5f - (mn + mx) * 0.5f + Vector3.down * 0.01f);
            }
        }

        void PlanWindows(RoomView rv, float ia, float ib, List<(float c, float w, float sill, float h)> outList, Strip strip = null)
        {
            var r = rv.Room;
            float len = ib - ia;
            float w = rv.Style.Stained ? 1.5f : 1.25f;
            if (r.Type == RoomType.Corridor) w = 1.1f;
            float margin = 0.55f;
            if (len < w + 2 * margin) return;
            float pitch = w + (r.Type == RoomType.Corridor ? 2.6f : 1.7f);
            int n = Math.Max(1, (int)((len - 2 * margin + (pitch - w)) / pitch));
            float used = n * w + (n - 1) * (pitch - w);
            float start = ia + (len - used) * 0.5f + w * 0.5f;
            float ceilRel = rv.CeilY - rv.FloorY;
            float sill = r.Floor == 0 ? 1.05f : 0.95f;
            float h = Math.Min(ceilRel - 0.55f - sill, r.Floor == 0 ? 2.75f : 2.25f);
            if (h < 1.2f) return;
            // a window never opens behind a bookcase or a wardrobe: tall pieces standing against this wall drop the window behind them
            var tall = new List<(float a, float b)>();
            if (strip != null)
                foreach (int fid in r.Furniture)
                {
                    var f = Layout.Furniture[fid]; if (f.H < sill - 0.12f || f.Type == "Rug" || f.Type == "Chandelier") continue;
                    NavGrid.GetFootprint(f, 0f, out var fp);
                    bool near = strip.AlongX ? Math.Min(Math.Abs(fp.z0 - strip.Line), Math.Abs(fp.z1 - strip.Line)) < 0.7f : Math.Min(Math.Abs(fp.x0 - strip.Line), Math.Abs(fp.x1 - strip.Line)) < 0.7f;
                    if (near) tall.Add(strip.AlongX ? (fp.x0, fp.x1) : (fp.z0, fp.z1));
                }
            for (int i = 0; i < n; i++)
            {
                float c = start + i * pitch; bool hidden = false;
                foreach (var t in tall) if (t.b > c - w * 0.5f - 0.05f && t.a < c + w * 0.5f + 0.05f) { hidden = true; break; }
                if (!hidden) outList.Add((c, w, sill, h));
            }
        }

        void BuildWindow(RoomView rv, Strip s, float c, float w, float sill, float h)
        {
            var mb = rv.Shell; float fy = rv.FloorY; var st = rv.Style;
            float r = w * 0.5f, spring = sill + h - r;
            float a = c - r, b = c + r;
            Vector3 n = N(s);
            // reveal (through the wall thickness)
            mb.Set(S.Plaster, Color.Lerp(st.WallA, Color.white, 0.5f));
            mb.QuadAuto(P(s, a, fy + sill, 0.1f), P(s, a, fy + spring, 0.1f), P(s, a, fy + spring, -0.1f), P(s, a, fy + sill, -0.1f), Along(s));
            mb.QuadAuto(P(s, b, fy + sill, 0.1f), P(s, b, fy + spring, 0.1f), P(s, b, fy + spring, -0.1f), P(s, b, fy + sill, -0.1f), -Along(s));
            int segs = 10;
            for (int i = 0; i < segs; i++)
            {
                float t0 = Mathf.PI * i / segs, t1 = Mathf.PI * (i + 1) / segs;
                float x0 = c - Mathf.Cos(t0) * r, x1 = c - Mathf.Cos(t1) * r;
                float h0 = spring + Mathf.Sin(t0) * r, h1 = spring + Mathf.Sin(t1) * r;
                Vector3 inward = (new Vector3(0, spring, 0) - new Vector3(0, (h0 + h1) * 0.5f, 0));
                var mid = (P(s, x0, fy + h0, 0) + P(s, x1, fy + h1, 0)) * 0.5f; var ctr = P(s, c, fy + spring, 0);
                mb.QuadAuto(P(s, x0, fy + h0, 0.1f), P(s, x1, fy + h1, 0.1f), P(s, x1, fy + h1, -0.1f), P(s, x0, fy + h0, -0.1f), ctr - mid);
            }
            // stone sill protruding into the room
            mb.Set(S.Marble, Color.Lerp(Color.white, rv.Pal.FloorA, 0.3f));
            var s0 = P(s, a - 0.06f, fy + sill - 0.06f, -0.1f); var s1 = P(s, b + 0.06f, fy + sill, 0.2f);
            mb.BoxMM(Vector3.Min(s0, s1), Vector3.Max(s0, s1));
            // glass (arched), UV 0..1 over the window rect
            bool stained = st.Stained || (rv.Room.Type == RoomType.Corridor && rv.Rng.NextDouble() < 0.2);
            var pal = rv.Pal;
            float motif = stained ? (float)(rv.Rng.Next(3)) : 0;
            // church glass colours, not the palette neon: ruby and cobalt pulled from the room palette, amber and green from the shader
            Color gA = Color.Lerp(pal.Neon, new Color(0.52f, 0.07f, 0.09f), 0.7f), gB = Color.Lerp(pal.Accent2, new Color(0.1f, 0.17f, 0.42f), 0.7f);
            mb.Set(stained ? S.Stained : S.Window, stained ? gA : Color.Lerp(Color.white, pal.Accent2, 0.15f), new Vector4(gB.r, gB.g, gB.b, motif));
            float gOff = -0.06f;
            Vector2 UV(float x, float y) => new Vector2((x - a) / w, (y - sill) / h);
            {
                var p00 = P(s, a, fy + sill, gOff); var p01 = P(s, a, fy + spring, gOff); var p11 = P(s, b, fy + spring, gOff); var p10 = P(s, b, fy + sill, gOff);
                if (Vector3.Dot(Vector3.Cross(p01 - p00, p11 - p00), n) >= 0) mb.QuadUV(p00, p01, p11, p10, UV(a, sill), UV(a, spring), UV(b, spring), UV(b, sill));
                else mb.QuadUV(p10, p11, p01, p00, UV(b, sill), UV(b, spring), UV(a, spring), UV(a, sill));
                var cc = P(s, c, fy + spring, gOff);
                for (int i = 0; i < segs; i++)
                {
                    float t0 = Mathf.PI * i / segs, t1 = Mathf.PI * (i + 1) / segs;
                    float x0 = c - Mathf.Cos(t0) * r, x1 = c - Mathf.Cos(t1) * r;
                    float h0 = spring + Mathf.Sin(t0) * r, h1 = spring + Mathf.Sin(t1) * r;
                    var q0 = P(s, x0, fy + h0, gOff); var q1 = P(s, x1, fy + h1, gOff);
                    if (Vector3.Dot(Vector3.Cross(q0 - cc, q1 - cc), n) >= 0) mb.QuadUV(cc, q0, q1, cc, UV(c, spring), UV(x0, h0), UV(x1, h1), UV(c, spring));
                    else mb.QuadUV(cc, q1, q0, cc, UV(c, spring), UV(x1, h1), UV(x0, h0), UV(c, spring));
                }
            }
            // iron tracery: centre mullion + transom + frame
            mb.Set(S.Iron, new Color(0.18f, 0.16f, 0.17f));
            var m0 = P(s, c - 0.03f, fy + sill, -0.08f); var m1 = P(s, c + 0.03f, fy + spring + r - 0.02f, -0.02f);
            mb.BoxMM(Vector3.Min(m0, m1), Vector3.Max(m0, m1));
            var t0p = P(s, a, fy + spring - 0.03f, -0.08f); var t1p = P(s, b, fy + spring + 0.03f, -0.02f);
            mb.BoxMM(Vector3.Min(t0p, t1p), Vector3.Max(t0p, t1p));
            // record for lighting (moonlight)
            var center = P(s, c, fy + sill + h * 0.5f, 0f);
            Windows.Add(new WindowInfo { Room = s.Room, Center = center, Normal = n, W = w, H = h, Sill = sill, Stained = stained });
        }

        // ------------------------------------------------------------------ floors & ceilings
        void BuildFloorAndCeiling(RoomView rv)
        {
            var r = rv.Room; var st = rv.Style; var mb = rv.Shell;
            if (r.Type == RoomType.Courtroom) return;
            var pal = rv.Pal;
            // ---- floor
            if (!r.Void)
            {
                var holes = new List<RectF>(rv.FloorHoles);
                if (r.Type == RoomType.Landing && VoidRoom != null) holes.Add(VoidRect);
                var rects = Subtract(r.Rect, holes);
                mb.Set(st.Floor, st.FloorA, new Vector4(st.FloorB.r, st.FloorB.g, st.FloorB.b, 0));
                foreach (var q in rects)
                    mb.Quad(new Vector3(q.x0, rv.FloorY, q.z0), new Vector3(q.x0, rv.FloorY, q.z1), new Vector3(q.x1, rv.FloorY, q.z1), new Vector3(q.x1, rv.FloorY, q.z0));
                // floor collider
                var col = rv.Root.gameObject;
                foreach (var q in rects)
                {
                    var bc = col.AddComponent<BoxCollider>();
                    bc.center = new Vector3(q.CX, rv.FloorY - 0.15f, q.CZ); bc.size = new Vector3(q.W, 0.3f, q.D);
                }
                // hole rims (slab edges) down to the ceiling below
                foreach (var h in rv.FloorHoles)
                {
                    if (IsPoolHole(h)) continue;
                    float below = rv.FloorY - 0.45f;
                    mb.Set(S.StoneFloor, Color.Lerp(Color.white, pal.Wall, 0.2f));
                    HoleRim(mb, h, rv.FloorY, below);
                }
            }
            // ---- ceiling
            if (r.Type == RoomType.Landing || (r.Type == RoomType.GrandHall && r.Floor == 0))
            {
                // ring ceiling around the void (1F: landing underside, 2F: landing ceiling)
                var holes = new List<RectF>(rv.CeilHoles) { VoidRect };
                var rects = Subtract(r.Rect, holes);
                Ceiling(rv, rects, st.CeilKind == 5 ? 1 : st.CeilKind);
            }
            else if (r.Void)
            {
                BuildVoidDome(rv);
            }
            else
            {
                var rects = Subtract(r.Rect, rv.CeilHoles);
                Ceiling(rv, rects, st.CeilKind);
                foreach (var h in rv.CeilHoles)
                {
                    mb.Set(S.Plaster, st.CeilTint * 0.8f);
                    HoleRim(mb, h, NextFloorBase(r.Floor), rv.CeilY, true);
                }
            }
            // runner carpet for corridors / landing
            if (st.Runner >= 0 && !r.Void)
            {
                mb.Set(st.Runner, pal.Carpet);
                bool alongX = r.Rect.W >= r.Rect.D;
                if (r.Type == RoomType.Landing) return;
                float w = Math.Min(1.3f, Math.Min(r.Rect.W, r.Rect.D) - 1.2f);
                if (w > 0.5f)
                {
                    var rr = alongX ? new RectF(r.Rect.x0 + 0.3f, r.Rect.CZ - w / 2, r.Rect.x1 - 0.3f, r.Rect.CZ + w / 2) : new RectF(r.Rect.CX - w / 2, r.Rect.z0 + 0.3f, r.Rect.CX + w / 2, r.Rect.z1 - 0.3f);
                    foreach (var q in Subtract(rr, rv.FloorHoles))
                    {
                        float y = rv.FloorY + 0.012f;
                        mb.Box(new Vector3(q.CX, y - 0.006f, q.CZ), new Vector3(q.W, 0.012f, q.D), MeshBuilder.Faces.NoBottom);
                        // gold edging
                        mb.Set(S.Gold, pal.Trim);
                        if (alongX) { mb.Box(new Vector3(q.CX, y, q.z0 + 0.04f), new Vector3(q.W, 0.014f, 0.035f), MeshBuilder.Faces.NoBottom); mb.Box(new Vector3(q.CX, y, q.z1 - 0.04f), new Vector3(q.W, 0.014f, 0.035f), MeshBuilder.Faces.NoBottom); }
                        else { mb.Box(new Vector3(q.x0 + 0.04f, y, q.CZ), new Vector3(0.035f, 0.014f, q.D), MeshBuilder.Faces.NoBottom); mb.Box(new Vector3(q.x1 - 0.04f, y, q.CZ), new Vector3(0.035f, 0.014f, q.D), MeshBuilder.Faces.NoBottom); }
                        mb.Set(st.Runner, pal.Carpet);
                    }
                }
            }
        }

        void HoleRim(MeshBuilder mb, RectF h, float yTop, float yBot, bool facingIn = false)
        {
            // four inward-facing (into the hole) faces
            Vector3 a = new Vector3(h.x0, 0, h.z0), b = new Vector3(h.x1, 0, h.z0), c = new Vector3(h.x1, 0, h.z1), d = new Vector3(h.x0, 0, h.z1);
            Vector3 ctr = new Vector3(h.CX, 0, h.CZ);
            void F(Vector3 p, Vector3 q)
            {
                var want = ctr - (p + q) * 0.5f; want.y = 0; if (facingIn) { }
                mb.QuadAuto(new Vector3(p.x, yBot, p.z), new Vector3(p.x, yTop, p.z), new Vector3(q.x, yTop, q.z), new Vector3(q.x, yBot, q.z), want);
            }
            F(a, b); F(b, c); F(c, d); F(d, a);
        }

        void Ceiling(RoomView rv, List<RectF> rects, int kind)
        {
            var mb = rv.Shell; var st = rv.Style; var pal = rv.Pal; float y = rv.CeilY; var r = rv.Room;
            switch (kind)
            {
                case 2: // sky (courtyard / painted heavens)
                    mb.Set(S.Sky, Color.white);
                    foreach (var q in rects) mb.Quad(new Vector3(q.x0, y, q.z0), new Vector3(q.x1, y, q.z0), new Vector3(q.x1, y, q.z1), new Vector3(q.x0, y, q.z1));
                    if (r.Type != RoomType.Courtyard)
                    {
                        // gilded frame around the painted sky
                        mb.Set(S.Gold, pal.Trim);
                        foreach (var q in rects) CeilingFrame(mb, q, y, 0.35f);
                    }
                    return;
                case 3: // greenhouse glass roof: sky behind an iron grid, shallow vault
                    {
                        mb.Set(S.Sky, new Color(1f, 1f, 1f, 0.5f));   // alpha 0.5: the sky seen through old glass (grime, fallen leaves)
                        foreach (var q in rects) mb.Quad(new Vector3(q.x0, y + 0.3f, q.z0), new Vector3(q.x1, y + 0.3f, q.z0), new Vector3(q.x1, y + 0.3f, q.z1), new Vector3(q.x0, y + 0.3f, q.z1));
                        mb.Set(S.Iron, new Color(0.22f, 0.26f, 0.22f));
                        foreach (var q in rects)
                        {
                            for (float x = q.x0; x <= q.x1 + 0.01f; x += Math.Max(0.8f, q.W / Mathf.Round(q.W / 1.2f))) mb.Box(new Vector3(x, y + 0.2f, q.CZ), new Vector3(0.06f, 0.08f, q.D));
                            for (float z = q.z0; z <= q.z1 + 0.01f; z += Math.Max(0.8f, q.D / Mathf.Round(q.D / 1.2f))) mb.Box(new Vector3(q.CX, y + 0.16f, z), new Vector3(q.W, 0.06f, 0.05f));
                            // vault ribs
                            bool alongX = q.W >= q.D;
                            for (float t = 0; t <= 1.001f; t += 0.25f)
                            {
                                var pts = new List<Vector3>();
                                for (int i = 0; i <= 12; i++)
                                {
                                    float u = i / 12f; float lift = Mathf.Sin(u * Mathf.PI) * 0.25f;
                                    pts.Add(alongX ? new Vector3(Mathf.Lerp(q.x0, q.x1, t), y + lift - 0.05f, Mathf.Lerp(q.z0, q.z1, u)) : new Vector3(Mathf.Lerp(q.x0, q.x1, u), y + lift - 0.05f, Mathf.Lerp(q.z0, q.z1, t)));
                                }
                                mb.Tube(pts, 0.035f, 6);
                            }
                        }
                        return;
                    }
                case 4: // industrial: concrete + pipes
                    {
                        mb.Set(S.ConcreteWall, st.CeilTint);
                        foreach (var q in rects) mb.Quad(new Vector3(q.x0, y, q.z0), new Vector3(q.x1, y, q.z0), new Vector3(q.x1, y, q.z1), new Vector3(q.x0, y, q.z1));
                        var R = r.Rect; bool alongX = R.W >= R.D; var rnd = rv.Rng;
                        int np = 2 + rnd.Next(3);
                        for (int i = 0; i < np; i++)
                        {
                            float off = (i + 1) / (float)(np + 1);
                            float rad = 0.05f + (float)rnd.NextDouble() * 0.08f;
                            mb.Set(i % 2 == 0 ? S.RustyMetal : S.Copper, Color.white);
                            var a = alongX ? new Vector3(R.x0 + 0.1f, y - 0.25f - i * 0.05f, Mathf.Lerp(R.z0, R.z1, off)) : new Vector3(Mathf.Lerp(R.x0, R.x1, off), y - 0.25f - i * 0.05f, R.z0 + 0.1f);
                            var b = alongX ? new Vector3(R.x1 - 0.1f, a.y, a.z) : new Vector3(a.x, a.y, R.z1 - 0.1f);
                            mb.Rod(a, b, rad, 10, false);
                            // clamps
                            mb.Set(S.Iron, Color.white);
                            float L = Vector3.Distance(a, b);
                            for (float t = 0.8f; t < L; t += 1.6f) { var p = Vector3.Lerp(a, b, t / L); mb.Box(new Vector3(p.x, (p.y + y) * 0.5f, p.z), new Vector3(0.04f, y - p.y, 0.04f)); }
                        }
                        return;
                    }
                case 6: return;
                case 7: // chapel: pointed ribbed vault, the web painted midnight blue with gilt stars
                    if (rv.CeilHoles.Count == 0 && ChapelVault(rv)) return;
                    break;
            }
            // plain / coffered plaster
            mb.Set(st.Ceil, st.CeilTint);
            foreach (var q in rects) mb.Quad(new Vector3(q.x0, y, q.z0), new Vector3(q.x1, y, q.z0), new Vector3(q.x1, y, q.z1), new Vector3(q.x0, y, q.z1));
            if (kind == 1)
            {
                foreach (var q in rects)
                {
                    if (q.W < 1.5f || q.D < 1.5f) continue;
                    int nx = Math.Max(1, (int)Math.Round(q.W / 1.7f)), nz = Math.Max(1, (int)Math.Round(q.D / 1.7f));
                    float bw = 0.16f, bd = 0.2f;
                    mb.Set(S.WoodDark, Color.Lerp(Color.white, pal.Wood * 2.4f, 0.5f));
                    for (int i = 1; i < nx; i++) { float x = q.x0 + q.W * i / nx; mb.Box(new Vector3(x, y - bd * 0.5f, q.CZ), new Vector3(bw, bd, q.D), MeshBuilder.Faces.Sides | MeshBuilder.Faces.NY); }
                    for (int j = 1; j < nz; j++) { float z = q.z0 + q.D * j / nz; mb.Box(new Vector3(q.CX, y - bd * 0.5f + 0.001f, z), new Vector3(q.W, bd - 0.002f, bw), MeshBuilder.Faces.Sides | MeshBuilder.Faces.NY); }
                    // gilded rosettes at intersections + painted coffers (palette ink)
                    for (int i = 1; i < nx; i++) for (int j = 1; j < nz; j++)
                        {
                            float x = q.x0 + q.W * i / nx, z = q.z0 + q.D * j / nz;
                            mb.Set(S.Gold, pal.Trim);
                            mb.Push(new Vector3(x, y - bd, z), 0);
                            mb.Lathe(new[] { new Vector2(0.001f, -0.05f), new Vector2(0.07f, -0.035f), new Vector2(0.1f, 0.0f) }, 10);
                            mb.Pop();
                        }
                    mb.Set(st.Ceil, Color.Lerp(st.CeilTint, pal.Ink, 0.35f));
                    for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
                        {
                            float x0 = q.x0 + q.W * i / nx + bw * 0.5f + 0.12f, x1 = q.x0 + q.W * (i + 1) / nx - bw * 0.5f - 0.12f;
                            float z0 = q.z0 + q.D * j / nz + bw * 0.5f + 0.12f, z1 = q.z0 + q.D * (j + 1) / nz - bw * 0.5f - 0.12f;
                            if (x1 - x0 < 0.2f || z1 - z0 < 0.2f) continue;
                            mb.Quad(new Vector3(x0, y - 0.004f, z0), new Vector3(x1, y - 0.004f, z0), new Vector3(x1, y - 0.004f, z1), new Vector3(x0, y - 0.004f, z1));
                        }
                }
            }
        }

        /// <summary>Pointed (depressed gothic) barrel vault along the room's long axis with transverse ribs, a ridge rib,
        /// a springing cornice and a star-spangled web. Returns false if the room is too small.</summary>
        bool ChapelVault(RoomView rv)
        {
            var r = rv.Room; var R = r.Rect; var mb = rv.Shell; var pal = rv.Pal;
            bool alongX = R.W >= R.D;
            float span = alongX ? R.D : R.W, len = alongX ? R.W : R.D;
            if (span < 3f) return false;
            float spring = rv.CeilY - 0.5f;                 // above the window heads
            float apex = Mathf.Min(rv.WallTop - 0.04f, rv.CeilY + 0.35f);
            float rise = apex - spring;
            const int segs = 18;
            // cross-section: two circular arcs (equilateral pointed arch), squashed vertically to the available rise
            Vector2 Sect(float u)
            {
                float x = (u - 0.5f) * span;              // -span/2 .. span/2
                float half = span * 0.5f;
                float cx = x < 0 ? half : -half;          // each arc centred at the opposite springing point
                float h = Mathf.Sqrt(Mathf.Max(0, span * span - (x - cx) * (x - cx)));
                return new Vector2(x, h / (0.8660254f * span) * rise);
            }
            Vector3 W(float u, float t, float inset = 0f)
            {
                var s = Sect(u); float across = (alongX ? R.CZ : R.CX) + s.x * (1f - inset); float y = spring + s.y - inset * 0.02f;
                float along = alongX ? Mathf.Lerp(R.x0, R.x1, t) : Mathf.Lerp(R.z0, R.z1, t);
                return alongX ? new Vector3(along, y, across) : new Vector3(across, y, along);
            }
            Color web = Color.Lerp(new Color(0.08f, 0.1f, 0.26f), pal.Wall, 0.2f);
            mb.Set(S.Ceiling, web);
            int nl = Mathf.Max(2, Mathf.RoundToInt(len / 1.2f));
            for (int j = 0; j < nl; j++)
                for (int i = 0; i < segs; i++)
                {
                    float t0 = j / (float)nl, t1 = (j + 1) / (float)nl, u0 = i / (float)segs, u1 = (i + 1) / (float)segs;
                    var mid = (W(u0, t0) + W(u1, t1)) * 0.5f; var down = new Vector3(alongX ? mid.x : R.CX, spring - 2f, alongX ? R.CZ : mid.z) - mid;
                    mb.QuadAuto(W(u0, t0), W(u1, t0), W(u1, t1), W(u0, t1), down);
                }
            // springing cornice along both long walls
            mb.Set(S.StoneWall, Color.Lerp(Color.white, pal.Wall, 0.25f));
            foreach (float side in new[] { -1f, 1f })
            {
                float across = (alongX ? R.CZ : R.CX) + side * (span * 0.5f - 0.12f);
                var c = alongX ? new Vector3(R.CX, spring - 0.08f, across) : new Vector3(across, spring - 0.08f, R.CZ);
                mb.Box(c, alongX ? new Vector3(len, 0.16f, 0.24f) : new Vector3(0.24f, 0.16f, len));
                mb.Set(S.Gold, pal.Trim);
                mb.Box(c + Vector3.down * 0.1f, alongX ? new Vector3(len, 0.03f, 0.2f) : new Vector3(0.2f, 0.03f, len));
                mb.Set(S.StoneWall, Color.Lerp(Color.white, pal.Wall, 0.25f));
            }
            // transverse ribs + wall shafts (engaged colonnettes) down to the floor
            int nr = Mathf.Max(2, Mathf.RoundToInt(len / 2.4f));
            for (int k = 0; k <= nr; k++)
            {
                float t = Mathf.Clamp(k / (float)nr, 0.01f, 0.99f);
                var pts = new List<Vector3>();
                for (int i = 0; i <= segs; i++) pts.Add(W(i / (float)segs, t, 0.01f) + Vector3.down * 0.05f);
                mb.Set(S.StoneWall, Color.Lerp(Color.white, pal.Wall, 0.15f));
                mb.Tube(pts, 0.09f, 6);
                mb.Set(S.Gold, pal.Trim);
                mb.Tube(pts, 0.035f, 5);
                foreach (float side in new[] { 0f, 1f })
                {
                    var top = W(side, t); var foot = new Vector3(top.x, rv.FloorY, top.z);
                    if (OpeningNear(r, foot, 0.35f)) continue;
                    var inward = new Vector3(alongX ? 0 : (side < 0.5f ? 1 : -1), 0, alongX ? (side < 0.5f ? 1 : -1) : 0);
                    mb.Set(S.StoneWall, Color.Lerp(Color.white, pal.Wall, 0.2f));
                    mb.Cyl(foot + inward * 0.13f, 0.09f, spring - rv.FloorY, 8, false);
                    mb.Push(top + inward * 0.13f + Vector3.down * 0.12f, 0); mb.Lathe(new[] { new Vector2(0.09f, -0.1f), new Vector2(0.16f, 0.02f), new Vector2(0.12f, 0.06f) }, 10); mb.Pop();
                }
            }
            // ridge rib with bosses
            {
                var ridge = new List<Vector3>();
                for (int j = 0; j <= 8; j++) ridge.Add(W(0.5f, j / 8f, 0.01f) + Vector3.down * 0.06f);
                mb.Set(S.StoneWall, Color.Lerp(Color.white, pal.Wall, 0.15f)); mb.Tube(ridge, 0.07f, 6);
                mb.Set(S.Gold, pal.Trim);
                for (int k = 0; k <= nr; k++) { var p = W(0.5f, Mathf.Clamp(k / (float)nr, 0.01f, 0.99f)) + Vector3.down * 0.12f; mb.Sphere(p, 0.13f, 10, 6, 0.7f); }
            }
            // gilt stars scattered on the web (detail, no shadows)
            var dmb = rv.Detail; var rnd = new System.Random(r.Id * 131 + 7);
            dmb.Set(S.Gold, Color.Lerp(pal.Trim, Color.white, 0.2f));
            int ns = Mathf.RoundToInt(len * span * 1.6f);
            for (int i = 0; i < ns; i++)
            {
                float u = 0.12f + (float)rnd.NextDouble() * 0.76f, t = (float)rnd.NextDouble();
                var p = W(u, t, 0.004f); var n = (new Vector3(alongX ? p.x : R.CX, spring, alongX ? R.CZ : p.z) - p).normalized;
                var q = Quaternion.LookRotation(n, alongX ? Vector3.right : Vector3.forward);
                float sz = 0.035f + (float)rnd.NextDouble() * 0.03f;
                dmb.Push(Matrix4x4.TRS(p + n * 0.01f, q, Vector3.one * sz));
                for (int a = 0; a < 5; a++)
                {
                    float a0 = a / 5f * Mathf.PI * 2, a1 = (a + 0.5f) / 5f * Mathf.PI * 2, a2 = (a + 1) / 5f * Mathf.PI * 2;
                    var o = Vector3.zero; var tip = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * 1f;
                    var l = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * 0.4f; var rr = new Vector3(Mathf.Cos(a2), Mathf.Sin(a2), 0) * 0.4f;
                    dmb.TriAuto(o, l, tip, Vector3.forward); dmb.TriAuto(o, tip, rr, Vector3.forward);
                }
                dmb.Pop();
            }
            return true;
        }

        /// <summary>True when a door or window of the room opens within 'margin' of this wall-foot point.</summary>
        internal bool OpeningNear(Room r, Vector3 p, float margin)
        {
            foreach (int did in r.Doors)
            {
                var d = Layout.Doors[did];
                if (d.AlongX) { if (Mathf.Abs(d.Pos.z - p.z) < 0.6f && Mathf.Abs(d.Pos.x - p.x) < d.Width * 0.5f + margin) return true; }
                else { if (Mathf.Abs(d.Pos.x - p.x) < 0.6f && Mathf.Abs(d.Pos.z - p.z) < d.Width * 0.5f + margin) return true; }
            }
            foreach (var w in Windows)
            {
                if (w.Room != r.Id) continue;
                bool ax = Mathf.Abs(w.Normal.z) > 0.5f;
                if (ax) { if (Mathf.Abs(w.Center.z - p.z) < 0.6f && Mathf.Abs(w.Center.x - p.x) < w.W * 0.5f + margin) return true; }
                else { if (Mathf.Abs(w.Center.x - p.x) < 0.6f && Mathf.Abs(w.Center.z - p.z) < w.W * 0.5f + margin) return true; }
            }
            return false;
        }

        void CeilingFrame(MeshBuilder mb, RectF q, float y, float w)
        {
            mb.Box(new Vector3(q.CX, y - 0.03f, q.z0 + w * 0.5f), new Vector3(q.W, 0.06f, w), MeshBuilder.Faces.Sides | MeshBuilder.Faces.NY);
            mb.Box(new Vector3(q.CX, y - 0.03f, q.z1 - w * 0.5f), new Vector3(q.W, 0.06f, w), MeshBuilder.Faces.Sides | MeshBuilder.Faces.NY);
            mb.Box(new Vector3(q.x0 + w * 0.5f, y - 0.03f, q.CZ), new Vector3(w, 0.06f, q.D - 2 * w), MeshBuilder.Faces.Sides | MeshBuilder.Faces.NY);
            mb.Box(new Vector3(q.x1 - w * 0.5f, y - 0.03f, q.CZ), new Vector3(w, 0.06f, q.D - 2 * w), MeshBuilder.Faces.Sides | MeshBuilder.Faces.NY);
        }

        /// <summary>The ceiling over the hall void: coffered dome ring with a moon oculus of stained glass.</summary>
        void BuildVoidDome(RoomView rv)
        {
            var mb = rv.Shell; var pal = rv.Pal; var r = rv.Room; float y = rv.FloorY + r.CeilingH;
            var q = r.Rect;
            float cx = q.CX, cz = q.CZ;
            float rad = Math.Min(q.W, q.D) * 0.5f - 0.2f;
            float ocR = Math.Min(2.6f, rad * 0.45f);
            // flat plaster ceiling outside the dome circle
            mb.Set(S.Ceiling, Color.Lerp(pal.Wall, Color.white, 0.25f));
            int seg = 48;
            var corners = new[] { new Vector3(q.x0, y, q.z0), new Vector3(q.x1, y, q.z0), new Vector3(q.x1, y, q.z1), new Vector3(q.x0, y, q.z1) };
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2, a1 = (i + 1) / (float)seg * Mathf.PI * 2;
                var p0 = new Vector3(cx + Mathf.Cos(a0) * rad, y, cz + Mathf.Sin(a0) * rad);
                var p1 = new Vector3(cx + Mathf.Cos(a1) * rad, y, cz + Mathf.Sin(a1) * rad);
                // project to rectangle boundary
                Vector3 E(float a) { float dx = Mathf.Cos(a), dz = Mathf.Sin(a); float tx = dx > 0 ? (q.x1 - cx) / dx : dx < 0 ? (q.x0 - cx) / dx : 1e9f; float tz = dz > 0 ? (q.z1 - cz) / dz : dz < 0 ? (q.z0 - cz) / dz : 1e9f; float t = Mathf.Min(tx, tz); return new Vector3(cx + dx * t, y, cz + dz * t); }
                var e0 = E(a0); var e1 = E(a1);
                mb.QuadAuto(p0, e0, e1, p1, Vector3.down);
            }
            // dome (hemisphere-ish, rising 1.4 m into the roof) with coffers
            mb.Set(S.Ceiling, Color.Lerp(pal.Wall, pal.Ink, 0.3f));
            var prof = new List<Vector2>();
            for (int i = 0; i <= 10; i++) { float t = i / 10f; float rr = Mathf.Lerp(rad, ocR, t); float h = Mathf.Sin(t * Mathf.PI * 0.5f) * 1.4f; prof.Add(new Vector2(rr, h)); }
            mb.Push(new Vector3(cx, y, cz), Quaternion.identity, new Vector3(1, 1, 1));
            // inner surface: reverse so it faces down/in
            var rev = new List<Vector2>(prof); rev.Reverse();
            mb.Lathe(rev, 48);
            // gilded ribs
            mb.Set(S.Gold, pal.Trim);
            for (int k = 0; k < 16; k++)
            {
                float ang = k / 16f * 360f;
                mb.Push(Vector3.zero, ang);
                var pts = new List<Vector3>(); foreach (var pp in prof) pts.Add(new Vector3(pp.x - 0.02f, pp.y - 0.05f, 0));
                mb.Tube(pts, 0.05f, 6);
                mb.Pop();
            }
            // concentric rings
            mb.Torus(new Vector3(0, -0.02f, 0), rad, 0.07f, 64, 6);
            mb.Torus(new Vector3(0, 1.35f, 0), ocR + 0.05f, 0.09f, 48, 6);
            // teeth ring around the oculus (subtle grotesque)
            mb.Set(S.Bone, Color.white);
            for (int k = 0; k < 40; k++)
            {
                float a = k / 40f * Mathf.PI * 2; float len = 0.12f + ((k * 37) % 7 == 0 ? 0.18f : 0);
                var p = new Vector3(Mathf.Cos(a) * (ocR + 0.05f), 1.33f - len * 0.5f, Mathf.Sin(a) * (ocR + 0.05f));
                mb.Box(p, new Vector3(0.07f, len, 0.07f));
            }
            // stained glass moon oculus (UV 0..1 over its square)
            mb.Set(S.Stained, pal.Neon, new Vector4(pal.Accent2.r, pal.Accent2.g, pal.Accent2.b, 0));
            var ctr = new Vector3(0, 1.4f, 0);
            for (int i = 0; i < 40; i++)
            {
                float a0 = i / 40f * Mathf.PI * 2, a1 = (i + 1) / 40f * Mathf.PI * 2;
                var p0 = new Vector3(Mathf.Cos(a0) * ocR, 1.4f, Mathf.Sin(a0) * ocR); var p1 = new Vector3(Mathf.Cos(a1) * ocR, 1.4f, Mathf.Sin(a1) * ocR);
                Vector2 U(Vector3 p) => new Vector2(p.x / (2 * ocR) + 0.5f, p.z / (2 * ocR) + 0.5f);
                mb.QuadUV(ctr, p0, p1, ctr, U(ctr), U(p0), U(p1), U(ctr));
            }
            mb.Pop();
            _oculus = new Vector3(cx, y + 1.4f, cz); _oculusR = ocR;
        }
        internal Vector3 _oculus; internal float _oculusR;

        // ------------------------------------------------------------------ finalize: shell meshes -> renderers
        void FinalizeShells()
        {
            foreach (var rv in Rooms)
            {
                if (rv == null || rv.Shell == null || rv.Shell.Empty) continue;
                var mesh = rv.Shell.ToMesh("Shell_" + rv.Room.Id, out var slots);
                var go = new GameObject("Shell"); go.transform.SetParent(rv.Root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = MansionMats.Materials(slots);
                mr.shadowCastingMode = ShadowCastingMode.TwoSided;
                rv.Renderers.Add(mr);
                rv.Shell = null;
                if (rv.Detail != null && !rv.Detail.Empty) Emit(rv, rv.Detail, "Detail", null, ShadowCastingMode.Off);
                rv.Detail = null;
            }
        }

        /// <summary>Helper for other builders: create a renderer from a MeshBuilder under a room.</summary>
        internal MeshRenderer Emit(RoomView rv, MeshBuilder mb, string name, Transform parent = null, ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            if (mb.Empty) return null;
            var mesh = mb.ToMesh(name, out var slots);
            var go = new GameObject(name); go.transform.SetParent(parent != null ? parent : rv.Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = MansionMats.Materials(slots);
            mr.shadowCastingMode = shadows;
            rv.Renderers.Add(mr);
            return mr;
        }
    }
}
