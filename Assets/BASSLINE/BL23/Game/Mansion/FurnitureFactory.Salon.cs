using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Salon furniture: the dressing pieces that turn furniture-on-the-walls into lived-in rooms (side tables, consoles,
    /// ottomans, day beds, rocking chairs, chests, china cabinets, standard lamps) plus CC0 model variants for the
    /// seating so a lounge, a library and a bedroom do not share one sofa. Every piece keeps a procedural fallback.
    /// </summary>
    internal static partial class FurnitureFactory
    {
        /// <summary>Types (or room-specific variants) built here; false lets the classic dispatch handle it.</summary>
        static bool DispatchSalon(Ctx c, MeshBuilder mb)
        {
            var rt = c.Rv.Room.Type;
            switch (c.F.Type)
            {
                case "SideTable":
                    ModelOr(c, "side_table_tall_01", null, -1, () => SideTableProc(c, mb), 0, new Vector3(c.W * 0.95f, c.H, c.D * 0.95f));
                    return true;
                case "Console":
                    ModelOr(c, "ClassicConsole_01", null, -1, () => SimpleTable(c, mb, 0.05f), 0, new Vector3(c.W, c.H * 1.0f, c.D));
                    return true;
                case "Ottoman":
                    ModelOr(c, "Ottoman_01", null, -1, () => { mb.Set(S.Leather, c.WoodC); mb.BevelBox(new Vector3(0, c.H * 0.55f, 0), new Vector3(c.W, c.H * 0.8f, c.D), 0.06f); });
                    return true;
                case "DayBed":
                    ModelOr(c, "vintage_day_bed", c.Fabric, 0.3f, () => Sofa(c, mb), 0, new Vector3(c.W, c.H * 1.08f, c.D));
                    return true;
                case "RockingChair":
                    ModelOr(c, "Rockingchair_01", null, -1, () => Chair(c, mb), 0, new Vector3(c.W, c.H * 1.05f, c.D));
                    return true;
                case "Chest":
                    // the scanned sea chest is heavy (100k tris): only where it is the point of the room
                    if (rt == RoomType.Storage || rt == RoomType.Wardrobe || rt == RoomType.TrophyRoom || rt == RoomType.WineCellar || rt == RoomType.DollRoom)
                        ModelOr(c, "treasure_chest", null, -1, () => ChestProc(c, mb), 0, new Vector3(c.W, c.H * 1.05f, c.D));
                    else ChestProc(c, mb);
                    return true;
                case "Cabinet":
                    ModelOr(c, "vintage_cabinet_01", null, -1, () => Cabinet(c, mb, 4), 0, new Vector3(c.W, c.H * 1.02f, c.D));
                    return true;
                case "FloorLamp":
                    FloorLamp(c, mb);
                    return true;
                case "Incinerator": IncineratorProc(c, mb); return true;
                case "ColdLocker": ColdLockerProc(c, mb); return true;
                case "DevelopTable": DevelopTableProc(c, mb); return true;
                case "Armchair":
                    // two chair families so a room's seats are not clones: velvet Louis chairs and the plain wing chair
                    if ((c.F.Id + c.Var) % 2 == 1 || rt == RoomType.Bedroom || rt == RoomType.TeaRoom || rt == RoomType.Parlor)
                        return ModelOr(c, "GreenChair_01", c.Fabric, 0.4f, () => Armchair(c, mb), 0, new Vector3(c.W * 0.92f, c.H * 1.05f, c.D * 0.95f));
                    return false;
                case "Sofa":
                    if (rt == RoomType.Library || rt == RoomType.Study || rt == RoomType.GameRoom || rt == RoomType.Workshop || rt == RoomType.Archive || rt == RoomType.ButlerRoom)
                        return ModelOr(c, "sofa_02", null, -1, () => Sofa(c, mb), 0, new Vector3(c.W, c.H * 1.05f, c.D));
                    if (rt == RoomType.Lounge || rt == RoomType.Parlor || rt == RoomType.Gallery || rt == RoomType.MusicRoom || rt == RoomType.TeaRoom || rt == RoomType.DollRoom || (c.F.Id % 2 == 0))
                        return ModelOr(c, "sofa_03", c.Fabric, 0.3f, () => Sofa(c, mb), 0, new Vector3(c.W, c.H * 1.2f, c.D));
                    return false;
                case "Chair":
                    if (IsHeadChair(c))
                    {
                        // the host's seat at the end of a long table: a carved gothic throne, taller than everyone else's
                        ModelOr(c, "WoodenChair_01", null, -1, () => Chair(c, mb), 0, new Vector3(0.72f, 1.62f, 0.7f));
                        return true;
                    }
                    return false;
                case "Plant":
                    PottedPlant(c, mb);
                    return true;
                case "Dartboard":
                    {
                        var m = Models.Get("dartboard");
                        if (m == null) return false;
                        var g = Models.Place(m, c.T, Vector3.zero, 0, new Vector3(0.46f, 0.46f, 0), null, true, Models.Anchor.Center, 0f);
                        g.transform.localPosition = new Vector3(0, 1.73f, 0.03f); g.transform.localRotation = Quaternion.identity;
                        mb.Set(S.WoodDark, c.WoodC); mb.Box(new Vector3(0, 1.73f, 0.01f), new Vector3(0.62f, 0.62f, 0.02f));
                        return true;
                    }
            }
            return false;
        }

        /// <summary>The incinerator: a riveted iron furnace, its hatch glowing, a flue to the ceiling, ash spilled at the mouth.</summary>
        static void IncineratorProc(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.Iron, new Color(0.28f, 0.25f, 0.23f));
            mb.BevelBox(new Vector3(0, H * 0.42f, 0), new Vector3(W, H * 0.84f, D), 0.04f);
            mb.Set(S.RustyMetal, Color.white);
            mb.Box(new Vector3(0, H * 0.86f, -D * 0.1f), new Vector3(W * 0.7f, H * 0.06f, D * 0.6f));
            mb.Cyl(new Vector3(0, H * 0.88f, -D * 0.1f), 0.2f, (c.Rv.CeilY - c.Rv.FloorY) - H * 0.88f, 12, false);
            // rivets in rows
            mb.Set(S.Iron, new Color(0.2f, 0.18f, 0.17f));
            for (int row = 0; row < 4; row++) for (int i = 0; i < 8; i++) mb.Sphere(new Vector3(-W / 2 + 0.1f + i * (W - 0.2f) / 7f, 0.15f + row * H * 0.22f, D / 2 + 0.005f), 0.015f, 5, 3);
            // the hatch: glowing slit and a latch
            mb.Set(S.Iron, new Color(0.18f, 0.16f, 0.15f));
            mb.Box(new Vector3(0, H * 0.4f, D / 2 + 0.03f), new Vector3(W * 0.55f, H * 0.36f, 0.05f));
            mb.Set(S.Glow, new Color(1f, 0.35f, 0.08f), MansionMats.GlowData(3.2f, 0.7f, 0, -2));
            mb.Box(new Vector3(0, H * 0.3f, D / 2 + 0.057f), new Vector3(W * 0.42f, 0.05f, 0.004f));
            for (int i = 0; i < 5; i++) mb.Box(new Vector3(-W * 0.16f + i * W * 0.08f, H * 0.47f, D / 2 + 0.057f), new Vector3(0.025f, H * 0.14f, 0.004f));
            mb.Set(S.Brass, new Color(0.5f, 0.4f, 0.3f)); mb.Rod(new Vector3(W * 0.2f, H * 0.52f, D / 2 + 0.06f), new Vector3(W * 0.2f, H * 0.3f, D / 2 + 0.1f), 0.015f, 6, true);
            // ash and cinders spilled on the floor at the mouth
            mb.Set(S.Soil, new Color(0.35f, 0.33f, 0.32f));
            mb.Push(new Vector3(0, 0, D / 2 + 0.35f), 0); mb.Lathe(new[] { new Vector2(0.001f, 0.03f), new Vector2(0.3f, 0.015f), new Vector2(0.45f, 0f) }, 12); mb.Pop();
            c.View.AddLight(c.Rv, c.W2(new Vector3(0, H * 0.4f, D / 2 + 0.5f)), new Color(1f, 0.42f, 0.12f), 3.2f, 5.5f, LightType.Point, false, 0.65f, fire: true);
        }

        /// <summary>Cold store racking: steel drawers the size of a person, frost at the seams, one drawer ajar.</summary>
        static void ColdLockerProc(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.Steel, new Color(0.75f, 0.8f, 0.84f));
            mb.BevelBox(new Vector3(0, H / 2, 0), new Vector3(W, H, D), 0.02f);
            int cols = 2, rows = 3;
            for (int i = 0; i < cols; i++) for (int j = 0; j < rows; j++)
                {
                    float x = -W / 2 + (i + 0.5f) * W / cols, y = 0.15f + (j + 0.5f) * (H - 0.3f) / rows;
                    bool ajar = i == 1 && j == 0 && c.Var % 2 == 0;
                    float out_ = ajar ? 0.35f : 0f;
                    mb.Set(S.Steel, new Color(0.82f, 0.86f, 0.9f));
                    mb.Box(new Vector3(x, y, D / 2 + 0.012f + out_ * 0.5f), new Vector3(W / cols - 0.06f, (H - 0.3f) / rows - 0.06f, 0.024f + out_));
                    mb.Set(S.Chrome, Color.white); mb.Box(new Vector3(x, y + 0.08f, D / 2 + 0.05f + out_), new Vector3(0.22f, 0.025f, 0.03f));
                    mb.Set(S.Paper, new Color(0.9f, 0.88f, 0.8f)); mb.Box(new Vector3(x - 0.18f, y - 0.06f, D / 2 + 0.026f + out_), new Vector3(0.1f, 0.06f, 0.002f));
                    // frost along the seams
                    mb.Set(S.PlasterWhite, new Color(0.9f, 0.95f, 1f));
                    mb.Box(new Vector3(x, y - (H - 0.3f) / rows * 0.5f + 0.03f, D / 2 + 0.014f), new Vector3(W / cols - 0.04f, 0.012f, 0.01f));
                    if (ajar) { mb.Set(S.Linen, new Color(0.85f, 0.85f, 0.82f)); mb.BevelBox(new Vector3(x, y + 0.04f, D / 2 + 0.2f), new Vector3(W / cols - 0.2f, 0.12f, 0.4f), 0.05f); }
                }
            mb.Set(S.Glow, new Color(0.4f, 0.8f, 1f), MansionMats.GlowData(1.6f, 0.2f, 0, c.Circuit));
            mb.Box(new Vector3(W / 2 - 0.1f, H - 0.08f, D / 2 + 0.01f), new Vector3(0.05f, 0.03f, 0.01f));
        }

        /// <summary>The darkroom bench: enamel trays of developer, stop and fix, bottles, a red safelight lamp and a
        /// line of drying prints clipped above — the faces on them not quite right.</summary>
        static void DevelopTableProc(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.PaintedMetal, new Color(0.3f, 0.3f, 0.3f));
            mb.Box(new Vector3(0, H - 0.03f, 0), new Vector3(W, 0.06f, D));
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.05f), (H - 0.06f) / 2, z * (D / 2 - 0.05f)), new Vector3(0.05f, H - 0.06f, 0.05f));
            Color[] fluid = { new Color(0.55f, 0.5f, 0.3f), new Color(0.4f, 0.45f, 0.35f), new Color(0.6f, 0.55f, 0.45f) };
            for (int i = 0; i < 3; i++)
            {
                float x = -W * 0.3f + i * W * 0.3f;
                mb.Set(S.Porcelain, new Color(0.92f, 0.92f, 0.9f)); mb.Box(new Vector3(x, H + 0.03f, 0.05f), new Vector3(W * 0.26f, 0.06f, D * 0.6f), MeshBuilder.Faces.Sides | MeshBuilder.Faces.NY);
                mb.Set(S.GlossPaint, fluid[i]); mb.Box(new Vector3(x, H + 0.045f, 0.05f), new Vector3(W * 0.24f, 0.002f, D * 0.56f), MeshBuilder.Faces.PY);
                if (i == 1) { mb.Set(S.Paper, new Color(0.85f, 0.83f, 0.8f)); mb.Box(new Vector3(x, H + 0.047f, 0.05f), new Vector3(0.2f, 0.001f, 0.15f), MeshBuilder.Faces.PY); mb.Set(S.Obsidian, new Color(0.25f, 0.2f, 0.2f)); mb.Box(new Vector3(x, H + 0.048f, 0.05f), new Vector3(0.1f, 0.001f, 0.08f), MeshBuilder.Faces.PY); }
            }
            // drying line with clipped prints
            float ly = Mathf.Min(c.Rv.CeilY - c.Rv.FloorY - 0.5f, 2.2f);
            mb.Set(S.Linen, new Color(0.6f, 0.58f, 0.5f)); mb.Rod(new Vector3(-W / 2, ly, -D * 0.2f), new Vector3(W / 2, ly - 0.05f, -D * 0.2f), 0.004f, 4, false);
            var rnd = c.Rng;
            for (int i = 0; i < 6; i++)
            {
                float x = -W / 2 + 0.2f + i * (W - 0.4f) / 5f;
                mb.Set(S.Paper, new Color(0.88f, 0.86f, 0.82f));
                var p = new Vector3(x, ly - 0.03f - 0.14f, -D * 0.2f);
                mb.Box(p, new Vector3(0.18f, 0.24f, 0.002f));
                mb.Set(S.Obsidian, new Color(0.2f + (float)rnd.NextDouble() * 0.2f, 0.15f, 0.15f));
                mb.Ellipsoid(p + new Vector3(0, 0.03f, 0.002f), new Vector3(0.04f, 0.05f, 0.001f), 8, 4);
                mb.Box(p + new Vector3(0, -0.07f, 0.002f), new Vector3(0.12f, 0.05f, 0.001f));
                mb.Set(S.WoodLight, Color.white); mb.Box(new Vector3(x, ly - 0.02f, -D * 0.2f), new Vector3(0.02f, 0.05f, 0.012f));
            }
        }

        static bool IsHeadChair(Ctx c)
        {
            var room = c.Rv.Room; var L = c.View.Layout;
            if (room.Type != RoomType.Dining) return false;
            float fx = Mathf.Sin(c.F.Yaw * Mathf.Deg2Rad), fz = Mathf.Cos(c.F.Yaw * Mathf.Deg2Rad);
            foreach (int fid in room.Furniture)
            {
                var t = L.Furniture[fid]; if (t.Type != "LongTable") continue;
                float ax = Mathf.Cos(t.Yaw * Mathf.Deg2Rad), az = -Mathf.Sin(t.Yaw * Mathf.Deg2Rad);   // table long axis (local x)
                float dx = c.F.Pos.x - t.Pos.x, dz = c.F.Pos.z - t.Pos.z;
                float along = dx * ax + dz * az, across = -dx * az + dz * ax;
                if (Mathf.Abs(across) < 0.3f && Mathf.Abs(along) > t.W * 0.5f && Mathf.Abs(along) < t.W * 0.5f + 0.8f && Mathf.Abs(fx * ax + fz * az) > 0.9f) return true;
            }
            return false;
        }

        static void SideTableProc(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.WoodCherry, c.WoodC);
            mb.Push(Vector3.zero, 0);
            mb.Lathe(new[] { new Vector2(0.001f, c.H - 0.03f), new Vector2(c.W * 0.46f, c.H - 0.03f), new Vector2(c.W * 0.48f, c.H), new Vector2(0.001f, c.H) }, 18);
            mb.Lathe(new[] { new Vector2(0.14f, 0), new Vector2(0.15f, 0.03f), new Vector2(0.04f, 0.08f), new Vector2(0.03f, 0.3f), new Vector2(0.05f, 0.45f), new Vector2(0.03f, c.H - 0.03f) }, 10);
            mb.Pop();
        }

        /// <summary>Iron-banded dark oak chest with a shallow barrel lid and brass corners.</summary>
        static void ChestProc(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            float body = H * 0.72f;
            mb.Set(S.WoodDark, Darker(c.WoodC, 0.1f));
            mb.BevelBox(new Vector3(0, body / 2 + 0.03f, 0), new Vector3(W, body - 0.06f, D), 0.012f);
            // barrel lid
            var prof = new List<Vector3>();
            mb.Push(new Vector3(0, body, 0), Quaternion.Euler(0, 90, 0), Vector3.one);
            float r = D * 0.55f, lift = H - body;
            for (int i = 0; i < 10; i++)
            {
                float a0 = Mathf.Lerp(-1f, 1f, i / 10f), a1 = Mathf.Lerp(-1f, 1f, (i + 1) / 10f);
                Vector3 p0 = new Vector3(a0 * D / 2, Mathf.Sqrt(Mathf.Max(0, 1 - a0 * a0)) * lift, 0), p1 = new Vector3(a1 * D / 2, Mathf.Sqrt(Mathf.Max(0, 1 - a1 * a1)) * lift, 0);
                mb.QuadAuto(p0 + Vector3.forward * W / 2, p1 + Vector3.forward * W / 2, p1 - Vector3.forward * W / 2, p0 - Vector3.forward * W / 2, (p0 + p1) * 0.5f + Vector3.up * 0.01f);
            }
            mb.Pop();
            foreach (float x in new[] { -1f, 1f })
            {
                var pts = new List<Vector2> { new Vector2(0, 0) };
                // lid end caps
                mb.Push(new Vector3(x * W / 2, body, 0), 0);
                for (int i = 0; i < 10; i++)
                {
                    float a0 = Mathf.Lerp(-1f, 1f, i / 10f), a1 = Mathf.Lerp(-1f, 1f, (i + 1) / 10f);
                    mb.TriAuto(Vector3.zero, new Vector3(0, Mathf.Sqrt(Mathf.Max(0, 1 - a0 * a0)) * lift, a0 * D / 2), new Vector3(0, Mathf.Sqrt(Mathf.Max(0, 1 - a1 * a1)) * lift, a1 * D / 2), new Vector3(x, 0, 0));
                }
                mb.Pop();
            }
            // iron bands + brass corners and lock plate
            mb.Set(S.Iron, new Color(0.25f, 0.23f, 0.22f));
            foreach (float x in new[] { -W * 0.3f, W * 0.3f })
            {
                mb.Box(new Vector3(x, body / 2 + 0.03f, D / 2 + 0.004f), new Vector3(0.05f, body - 0.04f, 0.008f));
                mb.Box(new Vector3(x, body / 2 + 0.03f, -D / 2 - 0.004f), new Vector3(0.05f, body - 0.04f, 0.008f));
            }
            mb.Set(S.Brass, Color.white);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.03f), 0.06f, z * (D / 2 - 0.03f)), new Vector3(0.07f, 0.1f, 0.07f));
            mb.Box(new Vector3(0, body - 0.05f, D / 2 + 0.008f), new Vector3(0.1f, 0.12f, 0.012f));
        }

        /// <summary>Standard lamp: turned brass pole, tasselled fabric shade glowing from within, and a real light.</summary>
        static void FloorLamp(Ctx c, MeshBuilder mb)
        {
            float H = c.H;
            mb.Set(S.Brass, Color.white);
            mb.Push(Vector3.zero, 0);
            mb.Lathe(new[] { new Vector2(0.16f, 0), new Vector2(0.17f, 0.02f), new Vector2(0.06f, 0.06f), new Vector2(0.018f, 0.12f), new Vector2(0.016f, H * 0.5f), new Vector2(0.03f, H * 0.52f), new Vector2(0.015f, H * 0.56f), new Vector2(0.014f, H - 0.3f), new Vector2(0.03f, H - 0.28f) }, 12, false, true);
            mb.Pop();
            Color shade = Color.Lerp(c.Pal.Fabric, new Color(1f, 0.8f, 0.55f), 0.45f);
            mb.Set(S.Glow, shade, MansionMats.GlowData(0.45f, 0.04f, 0, c.Circuit));   // a glowing shade, not a flare
            mb.Push(new Vector3(0, H - 0.34f, 0), 0);
            mb.Lathe(new[] { new Vector2(0.24f, 0), new Vector2(0.13f, 0.34f) }, 16);
            mb.Lathe(new[] { new Vector2(0.13f, 0.34f), new Vector2(0.24f, 0) }, 16);
            mb.Pop();
            mb.Set(S.Gold, c.Pal.Trim);
            for (int i = 0; i < 16; i++) { float a = i / 16f * Mathf.PI * 2; mb.Box(new Vector3(Mathf.Cos(a) * 0.24f, H - 0.37f, Mathf.Sin(a) * 0.24f), new Vector3(0.01f, 0.06f, 0.01f)); }
            c.View.AddLight(c.Rv, c.W2(new Vector3(0, H - 0.25f, 0)), new Color(1f, 0.72f, 0.45f), 2.2f, 4.2f, LightType.Point, false, 0.04f);
        }

        /// <summary>A clay pot with a leafy plant: the scanned pot plants where the room deserves one, procedural elsewhere.</summary>
        static void PottedPlant(Ctx c, MeshBuilder mb)
        {
            var rt = c.Rv.Room.Type;
            bool hero = rt == RoomType.GrandHall || rt == RoomType.Lounge || rt == RoomType.Gallery || rt == RoomType.Landing || rt == RoomType.Parlor || rt == RoomType.Dining || rt == RoomType.Greenhouse || rt == RoomType.TeaRoom;
            // the scanned plant is heavy (70k tris): one per room, the rest are potted procedurally
            if (hero) foreach (int fid in c.Rv.Room.Furniture) { var o = c.View.Layout.Furniture[fid]; if (o.Type == "Plant" && o.Id < c.F.Id) { hero = false; break; } }
            var big = hero ? Models.Get("potted_plant_02") : null;
            if (big != null)
            {
                var g = Models.Place(big, c.T, Vector3.zero, (c.F.Id * 47) % 360, new Vector3(c.W * 1.1f, c.H * 0.95f, c.D * 1.1f), null, false, Models.Anchor.Bottom, 0.1f);
                g.transform.localPosition = Vector3.zero;
                return;
            }
            var pot = Models.Get("planter_pot_clay");
            if (pot != null)
            {
                var g = Models.Place(pot, c.T, Vector3.zero, 0, new Vector3(0.52f, 0.46f, 0.52f), null, false, Models.Anchor.Bottom, 0f);
                g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.identity;
                mb.Set(S.Soil, Color.white); mb.Disc(new Vector3(0, 0.4f, 0), 0.21f, 12, true);
                PlantShape(mb, new Vector3(0, 0.4f, 0), c.H - 0.42f, c.Rng, c.Pal, c.Var % 3);
                return;
            }
            Plant(c, mb);
        }
    }
}
