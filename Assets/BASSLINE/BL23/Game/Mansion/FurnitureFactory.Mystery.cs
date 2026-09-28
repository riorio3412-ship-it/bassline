using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    internal static partial class FurnitureFactory
    {
        static void Statue(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.Marble, Color.Lerp(Color.white, c.Pal.FloorA, 0.3f));
            mb.Box(new Vector3(0, 0.45f, 0), new Vector3(W * 0.85f, 0.9f, D * 0.85f));
            mb.Box(new Vector3(0, 0.93f, 0), new Vector3(W * 0.95f, 0.06f, D * 0.95f));
            mb.Box(new Vector3(0, 0.04f, 0), new Vector3(W, 0.08f, D));
            var model = Models.Get(c.Var % 2 == 0 ? "gothic_statue" : "marble_bust_01");
            if (model != null)
            {
                var size = c.Var % 2 == 0 ? new Vector3(W * 0.9f, H - 0.96f, D * 0.9f) : new Vector3(W * 0.7f, 0.9f, D * 0.7f);
                var g = Models.Place(model, c.T, Vector3.zero, 0, size, null);
                g.transform.localPosition = new Vector3(0, 0.96f, 0); g.transform.localRotation = Quaternion.identity;
                // a velvet blindfold over the eyes of the bust. (The statues had a gilt halo; the scans give no reliable head
                // height, so it floated above the figure: dropped.)
                if (c.Var % 2 == 1) { mb.Set(S.Velvet, Color.Lerp(c.Pal.Neon, new Color(0.3f, 0.05f, 0.06f), 0.6f)); mb.Box(new Vector3(0, 0.96f + 0.9f * 0.72f, 0.07f), new Vector3(0.22f, 0.05f, 0.24f)); }
                return;
            }
            // procedural robed figure without a face
            mb.Set(S.Marble, Color.white);
            mb.Push(new Vector3(0, 0.96f, 0), 0);
            mb.Lathe(new[] { new Vector2(0.3f, 0), new Vector2(0.28f, 0.5f), new Vector2(0.2f, 1.0f), new Vector2(0.18f, 1.1f), new Vector2(0.08f, 1.15f), new Vector2(0.1f, 1.25f), new Vector2(0.001f, 1.4f) }, 12);
            mb.Pop();
        }

        static void ChandelierFurniture(Ctx c, MeshBuilder mb)
        {
            // stand-alone chandelier furniture (hung at its footprint height from the ceiling)
            float ceil = c.Rv.CeilY - c.Rv.FloorY;
            var p = new Vector3(0, Mathf.Min(ceil - c.H * 0.5f - 0.05f, Mathf.Max(ceil - 1.2f, 2.3f + c.H * 0.5f)), 0);   // keep >= 2.3 m clearance
            var m = Models.Get("Chandelier_03");
            if (m != null) { var g = Models.Place(m, c.T, Vector3.zero, 0, new Vector3(c.W, c.H, c.D), null, true); g.transform.localPosition = p; g.transform.localRotation = Quaternion.identity; }
            mb.Set(S.Brass, Color.white); mb.Rod(p + Vector3.up * 0.5f, new Vector3(0, ceil, 0), 0.02f, 6, false);
        }

        static void GrandfatherClock(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            var m = Models.Get("vintage_grandfather_clock_01");
            float faceY = H * 0.8f, faceR = W * 0.28f, faceZ = D / 2 + 0.012f;
            if (m != null)
            {
                var g = Models.Place(m, c.T, Vector3.zero, 0, new Vector3(W, H, D), null);
                g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.identity;
            }
            else
            {
                mb.Set(S.WoodCherry, Darker(c.WoodC, 0.2f));
                mb.Box(new Vector3(0, 0.25f, 0), new Vector3(W, 0.5f, D));
                mb.Box(new Vector3(0, 1.1f, 0), new Vector3(W * 0.75f, 1.2f, D * 0.8f));
                mb.Box(new Vector3(0, faceY, 0), new Vector3(W, 0.6f, D));
                mb.Push(new Vector3(0, H - 0.2f, 0), 0); mb.Lathe(new[] { new Vector2(W * 0.5f, 0), new Vector2(W * 0.3f, 0.15f), new Vector2(0.001f, 0.22f) }, 4); mb.Pop();
            }
            // our own face drawn on top: time is wrong, sometimes backwards
            float speed = c.Rv.Room.Type == RoomType.ClockMuseum ? (c.Var == 0 ? -1 : c.Var == 1 ? 7 : 1) : 1;
            mb.Set(S.Clock, Color.white, new Vector4(4, (c.F.Id * 5 % 12) + 0.37f * c.Var, speed, 9));
            mb.FaceUV(new Vector3(faceR, faceY - faceR, faceZ), new Vector3(-2 * faceR, 0, 0), new Vector3(0, 2 * faceR, 0), new Rect(1, 0, -1, 1));
            // pendulum behind glass
            var pen = new MeshBuilder();
            pen.Set(S.Brass, Color.white); pen.Rod(Vector3.zero, new Vector3(0, -0.75f, 0), 0.006f, 4, false);
            pen.Push(new Vector3(0, -0.8f, 0), Quaternion.Euler(90, 0, 0), Vector3.one); pen.Cyl(new Vector3(0, -0.01f, 0), 0.09f, 0.02f, 16); pen.Pop();
            var pt = Child(c, "Pendulum", new Vector3(0, faceY - faceR - 0.05f, D / 2 - 0.04f), pen);
            var sw = pt.gameObject.AddComponent<Swinger>(); sw.Amplitude = 9f; sw.Period = 2f * (1f / Mathf.Max(0.3f, Mathf.Abs(speed) * 0.4f + 0.6f)); sw.Phase = c.F.Id;
        }

        static void ClockCase(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodDark, c.WoodC);
            mb.Box(new Vector3(0, 0.4f, 0), new Vector3(W, 0.8f, D));
            mb.Box(new Vector3(0, H - 0.04f, 0), new Vector3(W, 0.08f, D));
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.02f), (H + 0.8f) / 2, z * (D / 2 - 0.02f)), new Vector3(0.04f, H - 0.8f, 0.04f));
            mb.Set(S.Velvet, c.Pal.Fabric); mb.Box(new Vector3(0, 0.81f, 0), new Vector3(W - 0.06f, 0.02f, D - 0.06f));
            mb.Box(new Vector3(0, 1.3f, -D / 2 + 0.03f), new Vector3(W - 0.06f, 1.0f, 0.02f));
            // many small clocks, each showing a different time (some backwards, some stopped)
            int n = Mathf.Max(3, (int)(W / 0.32f));
            for (int i = 0; i < n; i++)
            {
                float x = -W / 2 + (i + 0.5f) * W / n;
                for (int row = 0; row < 2; row++)
                {
                    float y = row == 0 ? 0.82f : 1.35f;
                    float r = 0.08f + ((i + row) % 3) * 0.02f;
                    mb.Set(S.Brass, c.Pal.Trim);
                    if (row == 0) { mb.Box(new Vector3(x, y + r + 0.02f, 0), new Vector3(r * 2.2f, r * 2.4f, 0.1f)); }
                    else { mb.Push(new Vector3(x, y, -D / 2 + 0.06f), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Cyl(Vector3.zero, r * 1.15f, 0.04f, 14); mb.Pop(); }
                    float speed = ((i * 7 + row * 3 + c.F.Id) % 5) switch { 0 => -1, 1 => 0, 2 => 3, 3 => 1, _ => 60 };
                    mb.Set(S.Clock, Color.white, new Vector4(4, (i * 5 + row * 7 + c.F.Id) % 12 + 0.5f * row, speed, 9));
                    Vector3 fc = row == 0 ? new Vector3(x, y + r + 0.03f, 0.051f) : new Vector3(x, y, -D / 2 + 0.105f);
                    mb.FaceUV(fc + new Vector3(r, -r, 0), new Vector3(-2 * r, 0, 0), new Vector3(0, 2 * r, 0), new Rect(1, 0, -1, 1));
                }
            }
            // glass
            mb.Set(S.Glass, Color.white);
            mb.Box(new Vector3(0, (H + 0.8f) / 2, D / 2 - 0.005f), new Vector3(W - 0.04f, H - 0.88f, 0.008f), MeshBuilder.Faces.PZ | MeshBuilder.Faces.NZ);
        }

        static void FileCabinet(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.PaintedMetal, Color.Lerp(new Color(0.35f, 0.4f, 0.35f), c.Pal.Wall, 0.2f));
            mb.BevelBox(new Vector3(0, H / 2, 0), new Vector3(W, H, D), 0.01f);
            int rows = 4, cols = 2;
            for (int i = 0; i < cols; i++) for (int j = 0; j < rows; j++)
                {
                    float x = -W / 2 + (i + 0.5f) * W / cols, y = 0.08f + (j + 0.5f) * (H - 0.1f) / rows;
                    bool open = (i + j + c.F.Id) % 7 == 0;
                    mb.Set(S.PaintedMetal, Color.Lerp(new Color(0.4f, 0.45f, 0.4f), c.Pal.Wall, 0.2f));
                    mb.Box(new Vector3(x, y, D / 2 + (open ? 0.2f : 0.005f)), new Vector3(W / cols - 0.04f, (H - 0.1f) / rows - 0.04f, 0.02f));
                    mb.Set(S.Brass, Color.white); mb.Box(new Vector3(x, y + 0.05f, D / 2 + (open ? 0.22f : 0.02f)), new Vector3(0.1f, 0.03f, 0.02f));
                    mb.Set(S.Paper, Color.white); mb.Box(new Vector3(x, y - 0.03f, D / 2 + (open ? 0.216f : 0.016f)), new Vector3(0.08f, 0.04f, 0.002f));
                    if (open) { mb.Set(S.Paper, new Color(0.9f, 0.85f, 0.7f)); for (int k = 0; k < 6; k++) mb.Box(new Vector3(x, y + 0.02f, D / 2 + 0.05f - k * 0.03f), new Vector3(W / cols - 0.1f, 0.18f, 0.004f)); }
                }
        }

        static void RecorderStand(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.Iron, Color.white);
            mb.Rod(new Vector3(0, 0, 0), new Vector3(0, 0.8f, 0), 0.02f, 6, false);
            for (int k = 0; k < 3; k++) { float a = k / 3f * Mathf.PI * 2; mb.Rod(Vector3.zero + Vector3.up * 0.05f, new Vector3(Mathf.Cos(a) * 0.22f, 0.0f, Mathf.Sin(a) * 0.22f), 0.012f, 4, true); }
            mb.Set(S.PaintedMetal, new Color(0.25f, 0.22f, 0.2f));
            mb.Box(new Vector3(0, 0.88f, 0), new Vector3(0.45f, 0.16f, 0.3f));
            mb.Set(S.Obsidian, Color.white);
            foreach (float x in new[] { -0.11f, 0.11f }) { mb.Cyl(new Vector3(x, 0.96f, 0), 0.09f, 0.01f, 16); mb.Set(S.Brass, Color.white); mb.Cyl(new Vector3(x, 0.965f, 0), 0.015f, 0.02f, 6); mb.Set(S.Obsidian, Color.white); }
            mb.Set(S.Glow, new Color(1f, 0.1f, 0.1f), MansionMats.GlowData(3f, 0.4f, 0, c.Circuit)); mb.Sphere(new Vector3(0.18f, 0.9f, 0.15f), 0.012f, 6, 4);
            mb.Set(S.Chrome, Color.white); mb.Rod(new Vector3(0, 0.96f, 0.1f), new Vector3(0, 1.25f, 0.15f), 0.006f, 4, false); mb.Sphere(new Vector3(0, 1.28f, 0.16f), 0.035f, 8, 6, 1.3f);
        }

        static void FreeDoor(Ctx c, MeshBuilder mb)
        {
            // free-standing door in its own frame, pure white; each at a different sill height (WhiteDoors gimmick)
            float W = c.W, H = c.H;
            int g = c.Rv.Room.MysteryGimmick;
            float lift = (c.F.Id * 37 % 5) * 0.35f * (g == 2 ? 1.6f : 1f);
            float scale = g == 2 ? 0.55f + (c.F.Id % 4) * 0.35f : 1f;
            bool upside = g == 1 && c.F.Id % 2 == 0;
            var white = new Color(1.1f, 1.1f, 1.1f);
            mb.Push(new Vector3(0, upside ? (c.Rv.CeilY - c.Rv.FloorY) : 0, 0), upside ? Quaternion.Euler(180, 0, 0) : Quaternion.identity, new Vector3(scale, scale, 1));
            mb.Set(S.PlasterWhite, white);
            // steps up to high doors
            if (lift > 0.01f && !upside) for (int s = 0; s < Mathf.CeilToInt(lift / 0.18f); s++) mb.Box(new Vector3(0, s * 0.18f + 0.09f, 0.25f + (Mathf.CeilToInt(lift / 0.18f) - s) * 0.22f), new Vector3(W * 0.9f, 0.18f, 0.24f));
            if (lift > 0.01f) mb.Box(new Vector3(0, lift / 2, 0), new Vector3(W + 0.1f, lift, 0.3f));
            mb.Box(new Vector3(-W / 2 + 0.06f, lift + H / 2, 0), new Vector3(0.12f, H, 0.3f));
            mb.Box(new Vector3(W / 2 - 0.06f, lift + H / 2, 0), new Vector3(0.12f, H, 0.3f));
            mb.Box(new Vector3(0, lift + H - 0.06f, 0), new Vector3(W, 0.12f, 0.3f));
            // the leaf, slightly ajar, showing darkness behind (or light for one of them)
            bool dark = (c.F.Id % 3) == 0;
            mb.Push(new Vector3(-W / 2 + 0.12f, lift, 0.05f), Quaternion.Euler(0, dark ? -28 : -6, 0), Vector3.one);
            mb.Box(new Vector3((W - 0.24f) / 2, (H - 0.12f) / 2, 0), new Vector3(W - 0.24f, H - 0.12f, 0.05f));
            mb.Set(S.Chrome, Color.white); mb.Sphere(new Vector3(W - 0.35f, 1.0f, 0.05f), 0.03f, 8, 5);
            mb.Pop();
            if (dark)
            {
                mb.Set(S.Obsidian, new Color(0.02f, 0.02f, 0.02f));
                mb.Box(new Vector3(0, lift + (H - 0.12f) / 2, -0.12f), new Vector3(W - 0.26f, H - 0.14f, 0.02f));
            }
            else
            {
                mb.Set(S.Glow, new Color(1f, 1f, 1f), MansionMats.GlowData(3f, 0, 0, -1));
                mb.Box(new Vector3(0, lift + (H - 0.12f) / 2, -0.12f), new Vector3(W - 0.26f, H - 0.14f, 0.02f));
            }
            mb.Pop();
        }

        static void TicketBooth(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodCherry, c.WoodC);
            mb.Box(new Vector3(0, 0.55f, 0), new Vector3(W, 1.1f, D));
            mb.Set(S.Marble, Color.Lerp(Color.white, c.Pal.FloorA, 0.3f)); mb.BevelBox(new Vector3(0, 1.12f, 0.05f), new Vector3(W + 0.1f, 0.05f, D + 0.1f), 0.01f);
            mb.Set(S.WoodCherry, c.WoodC);
            foreach (float x in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.05f), 1.8f, 0), new Vector3(0.1f, 1.3f, D));
            mb.Box(new Vector3(0, 2.5f, 0), new Vector3(W, 0.15f, D));
            mb.Set(S.Glass, Color.white); mb.Box(new Vector3(0, 1.8f, D / 2 - 0.1f), new Vector3(W - 0.2f, 1.2f, 0.01f));
            mb.Set(S.Brass, Color.white); for (int i = 0; i < 8; i++) mb.Rod(new Vector3(-W / 2 + 0.15f + i * (W - 0.3f) / 7, 1.15f, D / 2 - 0.09f), new Vector3(-W / 2 + 0.15f + i * (W - 0.3f) / 7, 2.42f, D / 2 - 0.09f), 0.008f, 4, false);
            // little lamp + a bell + a sign with a blank face
            Lamp(c, mb, new Vector3(-W * 0.3f, 1.14f, -0.1f), 0.2f, true);
            mb.Set(S.Brass, Color.white); mb.Push(new Vector3(W * 0.3f, 1.145f, 0.3f), 0); mb.Lathe(new[] { new Vector2(0.05f, 0), new Vector2(0.045f, 0.03f), new Vector2(0.02f, 0.06f), new Vector2(0.004f, 0.08f) }, 12); mb.Pop();
            mb.Set(S.Screen, Color.white, new Vector4(3, c.F.Id, 1, c.Circuit + 10));
            mb.FaceUV(new Vector3(W / 2 - 0.1f, 2.6f, D / 2 + 0.01f), new Vector3(-(W - 0.2f), 0, 0), new Vector3(0, 0.45f, 0), new Rect(1, 0, -1, 1));
        }

        static void ShallowWater(Ctx c, MeshBuilder mb)
        {
            // a sheet of still water over the whole floor; lily pads; reflection handled by the planar camera
            float W = c.W, D = c.D;
            var water = new MeshBuilder(); water.Set(S.ShallowWater, Color.white);
            int nx = Mathf.Max(2, (int)W), nz = Mathf.Max(2, (int)D);
            for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
                {
                    float x0 = -W / 2 + i * W / nx, x1 = x0 + W / nx, z0 = -D / 2 + j * D / nz, z1 = z0 + D / nz;
                    water.Quad(new Vector3(x0, 0.12f, z0), new Vector3(x0, 0.12f, z1), new Vector3(x1, 0.12f, z1), new Vector3(x1, 0.12f, z0));
                }
            var wt = Child(c, "Water", Vector3.zero, water, ShadowCastingMode.Off);
            var bc = wt.gameObject.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.center = new Vector3(0, 0.06f, 0); bc.size = new Vector3(W, 0.12f, D);
            var rnd = c.Rng;
            for (int i = 0; i < 9; i++)
            {
                var p = new Vector3(((float)rnd.NextDouble() - 0.5f) * (W - 1f), 0.125f, ((float)rnd.NextDouble() - 0.5f) * (D - 1f));
                mb.Set(S.Leaf, new Color(0.15f, 0.35f, 0.2f));
                mb.Push(p, (float)rnd.NextDouble() * 360f); mb.Disc(Vector3.zero, 0.2f + (float)rnd.NextDouble() * 0.15f, 12, true); mb.Pop();
                if (i % 3 == 0) { mb.Set(S.Porcelain, Color.Lerp(Color.white, c.Pal.Neon, 0.3f)); mb.Push(p + Vector3.up * 0.01f, 0); mb.Lathe(new[] { new Vector2(0.01f, 0), new Vector2(0.07f, 0.03f), new Vector2(0.03f, 0.08f) }, 8); mb.Pop(); }
            }
            // stepping stones
            mb.Set(S.StoneFloor, Color.white);
            for (int i = 0; i < 6; i++) { var p = new Vector3(-W / 2 + 0.8f + i * (W - 1.6f) / 5, 0.09f, Mathf.Sin(i * 1.3f) * D * 0.15f); mb.Push(p, i * 40f); mb.Cyl(Vector3.zero, 0.3f, 0.06f, 10); mb.Pop(); }
        }

        static void RainFrame(Ctx c, MeshBuilder mb)
        {
            // glass column with water running down inside (the rain corridor's "rain pillars")
            float H = c.H;
            mb.Set(S.Brass, c.Pal.Trim);
            mb.Cyl(Vector3.zero, 0.2f, 0.1f, 14); mb.Cyl(new Vector3(0, H - 0.1f, 0), 0.2f, 0.1f, 14);
            var inner = new MeshBuilder(); inner.Set(S.Aquarium, Color.white);
            inner.Push(new Vector3(0, 0.1f, 0), 0); inner.Lathe(new[] { new Vector2(0.13f, 0), new Vector2(0.13f, H - 0.2f) }, 14, false, false); inner.Pop();
            Child(c, "Glass", Vector3.zero, inner, ShadowCastingMode.Off);
            mb.Set(S.Glow, new Color(0.5f, 0.75f, 1f), MansionMats.GlowData(0.8f, 0.3f, 0, -1));
            for (int k = 0; k < 5; k++) { float a = k / 5f * Mathf.PI * 2; mb.Rod(new Vector3(Mathf.Cos(a) * 0.06f, 0.1f, Mathf.Sin(a) * 0.06f), new Vector3(Mathf.Cos(a) * 0.06f, H - 0.1f, Mathf.Sin(a) * 0.06f), 0.004f, 3, false); }
        }

        static void ButlerDesk(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            bool court = c.Rv.Room.Type == RoomType.Courtroom;
            mb.Set(court ? S.WoodDark : S.WoodCherry, court ? new Color(0.2f, 0.13f, 0.09f) : c.WoodC);
            mb.BevelBox(new Vector3(0, H / 2, 0), new Vector3(W, H, D), 0.03f);
            mb.Set(S.Gold, c.Pal.Trim);
            mb.Box(new Vector3(0, H + 0.01f, 0), new Vector3(W + 0.05f, 0.03f, D + 0.05f));
            // fish emblem in gold on the front (Yusti)
            mb.Ellipsoid(new Vector3(0, H * 0.55f, D / 2 + 0.01f), new Vector3(0.18f, 0.09f, 0.02f), 12, 6);
            mb.Push(new Vector3(0.2f, H * 0.55f, D / 2 + 0.01f), 0); mb.Box(Vector3.zero, new Vector3(0.1f, 0.12f, 0.02f)); mb.Pop();
            mb.Set(S.Glass, Color.white); mb.Cyl(new Vector3(-W * 0.3f, H + 0.02f, 0), 0.08f, 0.22f, 12);
            mb.Set(S.Brass, Color.white); mb.Push(new Vector3(W * 0.3f, H + 0.02f, 0), 0); mb.Lathe(new[] { new Vector2(0.06f, 0), new Vector2(0.05f, 0.03f), new Vector2(0.02f, 0.06f), new Vector2(0.004f, 0.09f) }, 12); mb.Pop();
            if (court) { mb.Set(S.Gold, c.Pal.Trim); mb.Box(new Vector3(0, 0.05f, D / 2 + 0.01f), new Vector3(W, 0.04f, 0.02f)); }
        }

        static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII" };

        static void TrialStand(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            int seat = c.F.Variant;
            // carved oak pulpit: octagonal body, moulded top rail, a brass plate with the seat's numeral, one candle
            var poly = new List<Vector2>();
            for (int i = 0; i < 8; i++) { float a = (i + 0.5f) / 8f * Mathf.PI * 2; poly.Add(new Vector2(Mathf.Cos(a) * W * 0.52f, Mathf.Sin(a) * D * 0.62f)); }
            poly.Reverse();
            var oak = new Color(0.3f, 0.19f, 0.12f); var oakDark = new Color(0.17f, 0.11f, 0.08f);
            mb.Set(S.WoodDark, oakDark); mb.Prism(poly, 0f, 0.12f, true, false);
            mb.Set(S.WoodDark, oak); mb.Prism(poly, 0.12f, H - 0.07f, true, false);
            mb.Set(S.WoodDark, oakDark); mb.Prism(poly, H - 0.07f, H, true, false);
            // recessed tracery panels on each face
            mb.Set(S.WoodDark, oakDark * 0.9f);
            for (int i = 0; i < 8; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % 8]; var m = (a + b) * 0.5f; var n = new Vector3(m.x, 0, m.y).normalized;
                var t = new Vector3(b.x - a.x, 0, b.y - a.y);
                var o = new Vector3(m.x, 0, m.y) + n * 0.004f;
                mb.QuadAuto(o - t * 0.32f + Vector3.up * 0.25f, o - t * 0.32f + Vector3.up * (H - 0.22f), o + t * 0.32f + Vector3.up * (H - 0.22f), o + t * 0.32f + Vector3.up * 0.25f, n);
            }
            mb.Set(S.Gold, c.Pal.Trim);
            for (int i = 0; i < 8; i++) { var a = poly[i]; var b = poly[(i + 1) % 8]; mb.Bar(new Vector3(a.x * 1.01f, H + 0.005f, a.y * 1.01f), new Vector3(b.x * 1.01f, H + 0.005f, b.y * 1.01f), 0.018f, 0.018f); }
            mb.Set(S.Brass, Color.white); mb.Box(new Vector3(0, H * 0.62f, D * 0.62f + 0.012f), new Vector3(0.34f, 0.12f, 0.01f));
            var t0 = Child(c, "Numeral", new Vector3(0, H * 0.62f, D * 0.62f + 0.02f), null);
            if (t0 != null)
            {
                var tm = t0.gameObject.AddComponent<TMPro.TextMeshPro>();
                tm.font = BL23.Game.Fonts.Display; tm.text = Roman[Mathf.Clamp(seat, 0, 17)]; tm.fontSize = 1.1f; tm.alignment = TMPro.TextAlignmentOptions.Center; tm.color = new Color(0.12f, 0.08f, 0.05f);
                tm.rectTransform.sizeDelta = new Vector2(0.34f, 0.12f); tm.transform.localRotation = Quaternion.Euler(0, 180, 0);
            }
            // a single candle on the rail (lit only when the court is in session: flames are grouped with the room)
            var wicks = new List<Vector3>();
            Candle(mb, new Vector3(W * 0.36f, H, -D * 0.1f), 0.14f, 0.016f, -2, new Vector3(W * 0.36f, H + 0.15f, -D * 0.1f), wicks);
            foreach (var w in wicks) MansionView.FlameQuad(mb, w, 0.06f, -2);
        }

        static void Aquarium(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodCherry, Darker(c.WoodC, 0.2f));
            mb.Box(new Vector3(0, 0.4f, 0), new Vector3(W, 0.8f, D));
            mb.Box(new Vector3(0, H - 0.06f, 0), new Vector3(W, 0.12f, D));
            mb.Set(S.Gold, c.Pal.Trim);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.02f), (H + 0.8f) / 2, z * (D / 2 - 0.02f)), new Vector3(0.04f, H - 0.92f, 0.04f));
            // sand, stones, weeds inside; glowing cyan light from the lid
            mb.Set(S.Soil, new Color(0.9f, 0.8f, 0.6f)); mb.Box(new Vector3(0, 0.84f, 0), new Vector3(W - 0.06f, 0.08f, D - 0.06f), MeshBuilder.Faces.PY);
            var rnd = c.Rng;
            for (int i = 0; i < 9; i++)
            {
                var p = new Vector3(((float)rnd.NextDouble() - 0.5f) * (W - 0.3f), 0.88f, ((float)rnd.NextDouble() - 0.5f) * (D - 0.2f));
                mb.Set(S.Leaf, Color.Lerp(new Color(0.1f, 0.5f, 0.3f), c.Pal.Neon, 0.2f));
                mb.Tube(new List<Vector3> { p, p + new Vector3(0.05f, 0.4f, 0), p + new Vector3(-0.03f, 0.75f + (float)rnd.NextDouble() * 0.3f, 0.02f) }, 0.015f, 4);
            }
            mb.Set(S.Bone, Color.white); mb.Sphere(new Vector3(W * 0.3f, 0.93f, 0), 0.09f, 8, 6, 0.9f);
            mb.Set(S.Glow, new Color(0.3f, 0.95f, 1f), MansionMats.GlowData(2.5f, 0.05f, 0, c.Circuit)); mb.Box(new Vector3(0, H - 0.125f, 0), new Vector3(W - 0.1f, 0.01f, D - 0.1f));
            var water = new MeshBuilder(); water.Set(S.Aquarium, Color.white);
            water.Box(new Vector3(0, (H + 0.8f) / 2 - 0.03f, 0), new Vector3(W - 0.05f, H - 0.95f, D - 0.05f));
            Child(c, "Water", Vector3.zero, water, ShadowCastingMode.Off);
            c.View.AddLight(c.Rv, c.W2(new Vector3(0, H * 0.6f, D / 2 + 0.6f)), new Color(0.3f, 0.9f, 1f), 3f, 4.5f, LightType.Point, false, 0.08f);
            // goldfish swimming inside
            var school = new GameObject("Fish"); school.transform.SetParent(c.T, false); school.transform.localPosition = new Vector3(0, (H + 0.8f) / 2, 0);
            var fs = school.AddComponent<FishSchool>();
            fs.Build(new Vector3(W / 2 - 0.25f, (H - 1.0f) / 2 - 0.1f, D / 2 - 0.15f), 7, 0.1f, c.F.Id, c.Pal);
        }

        static void Cart(Ctx c, MeshBuilder mb, int kind)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(kind == 0 ? S.PaintedMetal : S.Brass, kind == 0 ? new Color(0.35f, 0.4f, 0.38f) : Color.white);
            foreach (float y in new[] { 0.25f, H - 0.03f }) mb.Box(new Vector3(0, y, 0), new Vector3(W, 0.03f, D));
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Rod(new Vector3(x * (W / 2 - 0.02f), 0.08f, z * (D / 2 - 0.02f)), new Vector3(x * (W / 2 - 0.02f), H, z * (D / 2 - 0.02f)), 0.012f, 5, false);
            mb.Rod(new Vector3(-W / 2, H + 0.15f, -D / 2 + 0.02f), new Vector3(-W / 2, H + 0.15f, D / 2 - 0.02f), 0.012f, 5, true);
            mb.Set(S.Rubber, Color.white);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) { mb.Push(new Vector3(x * (W / 2 - 0.05f), 0.05f, z * (D / 2 - 0.05f)), Quaternion.Euler(0, 0, 90), Vector3.one); mb.Cyl(new Vector3(0, -0.015f, 0), 0.05f, 0.03f, 10); mb.Pop(); }
            if (kind == 2 || kind == 1) TeaSet(c, mb, new Vector3(0, H, 0));
            else { mb.Set(S.Linen, Color.white); mb.BevelBox(new Vector3(0, 0.33f, 0), new Vector3(W * 0.8f, 0.12f, D * 0.7f), 0.03f); mb.Set(S.Plastic, c.Pal.Accent2); mb.Cyl(new Vector3(0.2f, H, 0), 0.12f, 0.25f, 12); }
        }

        static void Fountain(Ctx c, MeshBuilder mb)
        {
            float W = c.W, H = c.H;
            mb.Set(S.StoneWall, Color.Lerp(Color.white, c.Pal.FloorA, 0.3f));
            mb.Push(Vector3.zero, 0);
            mb.Lathe(new[] { new Vector2(W / 2, 0), new Vector2(W / 2, 0.5f), new Vector2(W / 2 - 0.15f, 0.5f), new Vector2(W / 2 - 0.15f, 0.12f), new Vector2(0.001f, 0.12f) }, 28, true);
            mb.Lathe(new[] { new Vector2(0.2f, 0.12f), new Vector2(0.14f, 0.7f), new Vector2(0.55f, 0.85f), new Vector2(0.5f, 0.95f), new Vector2(0.1f, 0.95f), new Vector2(0.08f, 1.1f) }, 20);
            mb.Pop();
            // dry: dead leaves and a black stain ring; a child's shoe
            mb.Set(S.Soil, new Color(0.35f, 0.2f, 0.12f));
            mb.Disc(new Vector3(0, 0.13f, 0), W / 2 - 0.16f, 24, true);
            var rnd = c.Rng;
            mb.Set(S.Leaf, new Color(0.4f, 0.25f, 0.1f));
            for (int i = 0; i < 30; i++) { var p = new Vector3(((float)rnd.NextDouble() - 0.5f) * (W - 0.5f), 0.14f, ((float)rnd.NextDouble() - 0.5f) * (W - 0.5f)); if (p.magnitude > W / 2 - 0.2f) continue; mb.Push(p, (float)rnd.NextDouble() * 360); mb.Quad(new Vector3(-0.05f, 0, -0.03f), new Vector3(-0.05f, 0.01f, 0.03f), new Vector3(0.05f, 0.005f, 0.03f), new Vector3(0.05f, 0, -0.03f)); mb.Pop(); }
            // a fish statue spitting nothing, eyes of glass
            mb.Set(S.Marble, Color.white);
            mb.Ellipsoid(new Vector3(0, 1.3f, 0), new Vector3(0.12f, 0.25f, 0.18f), 10, 8);
            mb.Push(new Vector3(0, 1.55f, -0.05f), Quaternion.Euler(-30, 0, 0), Vector3.one); mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.12f, 0.15f), new Vector2(0.02f, 0.2f) }, 8); mb.Pop();
            mb.Set(S.Glass, Color.white); mb.Sphere(new Vector3(0.1f, 1.25f, 0.1f), 0.03f, 6, 4); mb.Sphere(new Vector3(-0.1f, 1.25f, 0.1f), 0.03f, 6, 4);
        }

        static void Dollhouse(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodPainted, Lighter(c.Pal.Wall, 0.3f));
            mb.Box(new Vector3(0, 0.3f, -0.1f), new Vector3(W, 0.6f, D - 0.2f));
            // open-front house: floors and rooms
            for (int fl = 0; fl < 2; fl++)
            {
                float y = 0.6f + fl * 0.5f;
                mb.Set(S.WoodPainted, Lighter(c.Pal.Wall, 0.3f));
                mb.Box(new Vector3(0, y, -0.1f), new Vector3(W, 0.03f, D - 0.2f));
                mb.Box(new Vector3(0, y + 0.25f, -D / 2 + 0.12f), new Vector3(W, 0.5f, 0.03f));
                for (int r = 0; r < 3; r++)
                {
                    float x = -W / 2 + (r + 0.5f) * W / 3;
                    mb.Set(S.WoodPainted, Color.HSVToRGB((r * 0.27f + fl * 0.4f + c.Var * 0.1f) % 1f, 0.4f, 0.7f));
                    mb.Box(new Vector3(x, y + 0.25f, -D / 2 + 0.14f), new Vector3(W / 3 - 0.04f, 0.46f, 0.005f));
                    mb.Set(S.WoodPainted, Lighter(c.Pal.Wall, 0.3f));
                    if (r > 0) mb.Box(new Vector3(-W / 2 + r * W / 3, y + 0.25f, -0.1f), new Vector3(0.02f, 0.5f, D - 0.25f));
                    // tiny furniture and a tiny figure (always facing the viewer)
                    mb.Set(S.WoodDark, c.WoodC); mb.Box(new Vector3(x, y + 0.06f, -0.1f), new Vector3(0.18f, 0.1f, 0.1f));
                    if ((r + fl) % 2 == 0) { mb.Set(S.Porcelain, new Color(0.95f, 0.9f, 0.85f)); mb.Cyl(new Vector3(x + 0.12f, y + 0.02f, 0.05f), 0.02f, 0.1f, 6); mb.Sphere(new Vector3(x + 0.12f, y + 0.14f, 0.05f), 0.025f, 6, 4); }
                    mb.Set(S.Glow, c.Pal.Warm, MansionMats.GlowData(1.2f, 0.3f, 0, -2)); mb.Sphere(new Vector3(x, y + 0.45f, -0.1f), 0.015f, 5, 3);
                }
            }
            // gable roof
            mb.Set(S.WoodPainted, Darker(c.Pal.Ink, 0.2f));
            mb.Push(new Vector3(0, 1.6f, -0.1f), 0);
            mb.QuadAuto(new Vector3(-W / 2 - 0.05f, 0, -D / 2 + 0.05f), new Vector3(0, H - 1.6f, -D / 2 + 0.05f), new Vector3(0, H - 1.6f, D / 2 - 0.25f), new Vector3(-W / 2 - 0.05f, 0, D / 2 - 0.25f), new Vector3(-1, 1, 0));
            mb.QuadAuto(new Vector3(W / 2 + 0.05f, 0, -D / 2 + 0.05f), new Vector3(0, H - 1.6f, -D / 2 + 0.05f), new Vector3(0, H - 1.6f, D / 2 - 0.25f), new Vector3(W / 2 + 0.05f, 0, D / 2 - 0.25f), new Vector3(1, 1, 0));
            mb.Pop();
            c.View.AddLight(c.Rv, c.W2(new Vector3(0, 1.0f, 0.4f)), c.Pal.Warm, 1.2f, 2.5f, LightType.Point, false, 0.3f, fire: true);
        }

        static void DollShelf(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodPainted, Lighter(c.Pal.Wall, 0.5f));
            mb.Box(new Vector3(0, H / 2, -D / 2 + 0.01f), new Vector3(W, H, 0.02f));
            foreach (float x in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.02f), H / 2, 0), new Vector3(0.04f, H, D));
            var rnd = c.Rng;
            for (int s = 0; s < 4; s++)
            {
                float y = 0.15f + s * (H - 0.25f) / 4;
                mb.Set(S.WoodPainted, Lighter(c.Pal.Wall, 0.5f)); mb.Box(new Vector3(0, y, 0), new Vector3(W, 0.025f, D));
                int n = 5;
                for (int i = 0; i < n; i++) Doll(mb, new Vector3(-W / 2 + 0.2f + i * (W - 0.4f) / (n - 1), y + 0.012f, 0.02f), 0.3f, rnd, c.Pal, (i * 7 + s * 3 + c.F.Id) % 5 == 0);
            }
        }

        internal static void Doll(MeshBuilder mb, Vector3 p, float h, System.Random rnd, MansionPalette pal, bool wrong)
        {
            Color dress = Color.HSVToRGB((float)rnd.NextDouble(), 0.5f, 0.6f);
            if (rnd.NextDouble() < 0.3) dress = pal.Fabric;
            mb.Set(S.Velvet, dress);
            mb.Push(p, 0); mb.Lathe(new[] { new Vector2(h * 0.28f, 0), new Vector2(h * 0.22f, h * 0.3f), new Vector2(h * 0.12f, h * 0.5f), new Vector2(h * 0.1f, h * 0.62f), new Vector2(0.001f, h * 0.64f) }, 10); mb.Pop();
            mb.Set(S.Porcelain, new Color(0.96f, 0.9f, 0.86f));
            // head: sometimes turned backwards / tilted (wrong)
            var head = p + Vector3.up * h * 0.78f;
            mb.Sphere(head, h * 0.17f, 10, 7);
            mb.Set(S.Obsidian, Color.white);
            float fz = wrong ? -1 : 1;
            mb.Sphere(head + new Vector3(-h * 0.06f, h * 0.02f, fz * h * 0.15f), h * 0.03f, 6, 4);
            mb.Sphere(head + new Vector3(h * 0.06f, h * 0.02f, fz * h * 0.15f), h * 0.03f, 6, 4);
            mb.Set(S.Velvet, Color.HSVToRGB((float)rnd.NextDouble(), 0.4f, 0.3f));
            mb.Sphere(head + new Vector3(0, h * 0.06f, -fz * h * 0.03f), h * 0.18f, 8, 5, 0.8f);
            mb.Set(S.GlossPaint, new Color(0.8f, 0.1f, 0.15f)); mb.Sphere(head + new Vector3(0, -h * 0.07f, fz * h * 0.15f), h * 0.02f, 5, 3);
        }

        static void BarrelProc(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.WoodWorn, Color.white);
            mb.Push(Vector3.zero, 0); mb.Lathe(new[] { new Vector2(c.W * 0.4f, 0), new Vector2(c.W * 0.5f, c.H * 0.5f), new Vector2(c.W * 0.4f, c.H) }, 16, true, true); mb.Pop();
            mb.Set(S.Iron, Color.white); mb.Torus(new Vector3(0, c.H * 0.2f, 0), c.W * 0.45f, 0.012f, 16, 4); mb.Torus(new Vector3(0, c.H * 0.8f, 0), c.W * 0.45f, 0.012f, 16, 4);
        }

        static void WineRack(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodWorn, Darker(c.WoodC, 0.1f));
            foreach (float x in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.03f), H / 2, 0), new Vector3(0.06f, H, D));
            int rows = 8, cols = Mathf.Max(3, (int)(W / 0.14f));
            var rnd = c.Rng;
            for (int j = 0; j < rows; j++)
            {
                float y = 0.15f + j * (H - 0.2f) / rows;
                mb.Set(S.WoodWorn, Darker(c.WoodC, 0.1f)); mb.Box(new Vector3(0, y - 0.07f, 0), new Vector3(W - 0.1f, 0.02f, D));
                for (int i = 0; i < cols; i++)
                {
                    if (rnd.NextDouble() < 0.15) continue;
                    float x = -W / 2 + 0.1f + i * (W - 0.2f) / (cols - 1);
                    Color g = rnd.NextDouble() < 0.5 ? new Color(0.1f, 0.2f, 0.1f) : new Color(0.3f, 0.05f, 0.08f);
                    mb.Set(S.Glass, g);
                    mb.Push(new Vector3(x, y, D / 2 - 0.02f), Quaternion.Euler(-90, 0, 0), Vector3.one);
                    mb.Lathe(new[] { new Vector2(0.035f, 0), new Vector2(0.036f, 0.2f), new Vector2(0.013f, 0.25f), new Vector2(0.013f, 0.3f) }, 8);
                    mb.Pop();
                    mb.Set(S.Wax, new Color(0.5f, 0.05f, 0.08f)); mb.Cyl(new Vector3(x, y - 0.013f, D / 2 + 0.002f), 0.015f, 0.026f, 6);
                }
            }
        }

        static void Boiler(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.GreenRust, Color.white);
            mb.Push(new Vector3(0, 0.2f, 0), 0); mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(W * 0.42f, 0.05f), new Vector2(W * 0.45f, H * 0.7f), new Vector2(W * 0.35f, H * 0.85f), new Vector2(0.2f, H * 0.9f), new Vector2(0.2f, H - 0.2f) }, 20, true); mb.Pop();
            mb.Set(S.Iron, Color.white);
            for (int i = 0; i < 5; i++) mb.Torus(new Vector3(0, 0.3f + i * H * 0.14f, 0), W * 0.455f, 0.015f, 24, 4);
            mb.Box(new Vector3(0, 0.1f, 0), new Vector3(W, 0.2f, D));
            // firebox door glowing
            mb.Set(S.Iron, new Color(0.1f, 0.1f, 0.1f)); mb.Box(new Vector3(0, 0.55f, W * 0.43f), new Vector3(0.5f, 0.4f, 0.06f));
            mb.Set(S.Glow, new Color(1f, 0.35f, 0.05f), MansionMats.GlowData(3f, 0.7f, 0, -2)); for (int i = 0; i < 4; i++) mb.Box(new Vector3(-0.15f + i * 0.1f, 0.55f, W * 0.46f), new Vector3(0.04f, 0.28f, 0.01f));
            c.View.AddLight(c.Rv, c.W2(new Vector3(0, 0.6f, W * 0.8f)), new Color(1f, 0.4f, 0.1f), 3f, 4.5f, LightType.Point, false, 0.7f, fire: true);
            // gauges
            mb.Set(S.Clock, Color.white, new Vector4(4, c.F.Id % 12, 40, 9));
            mb.FaceUV(new Vector3(0.1f, H * 0.62f, W * 0.455f), new Vector3(-0.2f, 0, 0), new Vector3(0, 0.2f, 0), new Rect(1, 0, -1, 1));
            mb.Set(S.Copper, Color.white);
            mb.Tube(new List<Vector3> { new Vector3(0, H - 0.2f, 0), new Vector3(0, H + 0.3f, 0), new Vector3(0, H + 0.4f, -D / 2 - 0.3f) }, 0.1f, 10);
        }

        /// <summary>Door register: a small blackened-walnut plaque hung flush on the wall beside the door frame, facing into the
        /// room — an aged-brass bezel over a mechanical number drum (dim amber backlight), a paper-tape slot and a tiny pilot
        /// lamp, with a thin iron pipe running up the wall. The kernel only knows a point near the door; the plaque finds the
        /// wall itself.</summary>
        static void DoorLogger(Ctx c, MeshBuilder mb)
        {
            const float W = 0.2f, H = 0.28f, D = 0.06f, y0 = 1.28f;
            // the door this register watches: the nearest door of the room
            var L = c.View.Layout; var room = c.Rv.Room; Door door = null; float best = 9f;
            foreach (int did in room.Doors) { var dd = L.Doors[did]; float dist = Mathf.Abs(dd.Pos.x - c.F.Pos.x) + Mathf.Abs(dd.Pos.z - c.F.Pos.z); if (dist < best) { best = dist; door = dd; } }
            Vector3 local = Vector3.zero; float yaw = 0f;
            if (door != null)
            {
                // flush on the room side of the door's wall, clear of the architrave, facing into the room
                float into = door.AlongX ? Mathf.Sign(room.Rect.CZ - door.Pos.z) : Mathf.Sign(room.Rect.CX - door.Pos.x);
                float side = door.AlongX ? Mathf.Sign(c.F.Pos.x - door.Pos.x) : Mathf.Sign(c.F.Pos.z - door.Pos.z); if (side == 0) side = 1;
                float lat = Mathf.Max(0.9f, door.Width * 0.5f + 0.42f);
                Vector3 world = door.AlongX ? new Vector3(door.Pos.x + side * lat, 0, door.Pos.z + into * (0.1f + D * 0.5f + 0.005f))
                                            : new Vector3(door.Pos.x + into * (0.1f + D * 0.5f + 0.005f), 0, door.Pos.z + side * lat);
                var wl = c.T.InverseTransformPoint(new Vector3(world.x, c.T.position.y, world.z)); local = new Vector3(wl.x, 0, wl.z);
                // plaque front (+z local) must face into the room
                Vector3 n = door.AlongX ? new Vector3(0, 0, into) : new Vector3(into, 0, 0);
                yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg - c.T.eulerAngles.y;
            }
            var q = Quaternion.Euler(0, yaw, 0);
            mb.Push(Matrix4x4.TRS(local, q, Vector3.one));
            // body: blackened walnut, bevelled, with an aged-brass edge
            mb.Set(S.WoodDark, new Color(0.3f, 0.24f, 0.2f));
            mb.BevelBox(new Vector3(0, y0 + H / 2, 0), new Vector3(W, H, D), 0.01f);
            mb.Set(S.Brass, new Color(0.62f, 0.5f, 0.34f));
            mb.Box(new Vector3(0, y0 + H - 0.012f, D / 2 + 0.002f), new Vector3(W - 0.02f, 0.006f, 0.004f));
            mb.Box(new Vector3(0, y0 + 0.012f, D / 2 + 0.002f), new Vector3(W - 0.02f, 0.006f, 0.004f));
            // brass bezel ring around the register window
            float wy = y0 + H * 0.6f;
            mb.Push(new Vector3(0, wy, D / 2 + 0.004f), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Torus(Vector3.zero, 0.052f, 0.007f, 18, 5); mb.Pop();
            // window: dark glass, a faint amber backlight, and a row of number-drum wheels
            mb.Set(S.Obsidian, Color.white); mb.Box(new Vector3(0, wy, D / 2 + 0.001f), new Vector3(0.09f, 0.06f, 0.003f));
            mb.Set(S.Glow, new Color(1f, 0.62f, 0.3f), MansionMats.GlowData(0.35f, 0.05f, 0, c.Circuit));
            mb.Box(new Vector3(0, wy, D / 2 + 0.0022f), new Vector3(0.078f, 0.026f, 0.001f));
            mb.Set(S.Paper, new Color(0.86f, 0.8f, 0.66f));
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(-0.027f + i * 0.018f, wy, D / 2 + 0.0032f), new Vector3(0.014f, 0.02f, 0.002f));
            mb.Set(S.Obsidian, Color.white);
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(-0.027f + i * 0.018f, wy + (i % 2 == 0 ? 0.002f : -0.002f), D / 2 + 0.0044f), new Vector3(0.005f, 0.009f, 0.0008f));   // the digits
            // paper-tape slot with a curl of tape
            mb.Set(S.Iron, new Color(0.12f, 0.12f, 0.12f)); mb.Box(new Vector3(0, y0 + H * 0.24f, D / 2 + 0.002f), new Vector3(0.1f, 0.01f, 0.004f));
            mb.Set(S.Paper, new Color(0.9f, 0.86f, 0.74f)); mb.Box(new Vector3(0.01f, y0 + H * 0.24f - 0.018f, D / 2 + 0.006f), new Vector3(0.05f, 0.03f, 0.002f));
            // pilot lamp: a dim ruby jewel
            mb.Set(S.Glow, new Color(0.9f, 0.18f, 0.08f), MansionMats.GlowData(0.7f, 0.2f, 0, c.Circuit));
            mb.Sphere(new Vector3(W * 0.34f, y0 + H * 0.86f, D / 2 + 0.004f), 0.006f, 6, 4);
            // a thin iron pipe hugging the wall up to the picture rail
            mb.Set(S.Iron, new Color(0.14f, 0.13f, 0.12f));
            mb.Rod(new Vector3(-W * 0.3f, y0 + H, -D * 0.2f), new Vector3(-W * 0.3f, y0 + H + 0.9f, -D * 0.2f), 0.007f, 6, false);
            mb.Pop();
            var col = c.Go.AddComponent<BoxCollider>();
            col.center = local + q * new Vector3(0, y0 + H / 2, 0);
            var sz = q * new Vector3(W, H, D); col.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
        }

        static void StuffedBeast(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodDark, c.WoodC); mb.Box(new Vector3(0, 0.2f, 0), new Vector3(W, 0.4f, D));
            mb.Set(S.Gold, c.Pal.Trim); mb.Box(new Vector3(0, 0.41f, 0), new Vector3(W * 0.95f, 0.02f, D * 0.95f));
            // a crouching beast body (fur) with a scanned head
            mb.Set(S.Velvet, new Color(0.3f, 0.22f, 0.16f));
            mb.Ellipsoid(new Vector3(-0.1f, 0.85f, 0), new Vector3(W * 0.38f, 0.32f, D * 0.36f), 12, 8);
            foreach (float x in new[] { -0.35f, 0.25f }) foreach (float z in new[] { -0.2f, 0.2f }) mb.Rod(new Vector3(x, 0.42f, z), new Vector3(x + 0.05f, 0.75f, z * 0.8f), 0.06f, 6, true);
            string[] heads = { "bull_head", "horse_head", "lion_head" };
            var m = Models.Get(heads[(c.F.Id + c.Var) % 3]);
            if (m != null) { var g = Models.Place(m, c.T, Vector3.zero, 0, new Vector3(0.45f, 0.55f, 0.5f), null); g.transform.localPosition = new Vector3(0.45f, 0.95f, 0); g.transform.localRotation = Quaternion.Euler(0, 90, 0); }
            else { mb.Ellipsoid(new Vector3(0.45f, 1.1f, 0), new Vector3(0.2f, 0.2f, 0.17f), 10, 7); }
            // glass eyes glint
            mb.Set(S.Glow, new Color(1f, 0.8f, 0.3f), MansionMats.GlowData(0.6f, 0.2f, 0, -1));
            mb.Sphere(new Vector3(0.62f, 1.18f, 0.08f), 0.015f, 5, 3); mb.Sphere(new Vector3(0.62f, 1.18f, -0.08f), 0.015f, 5, 3);
        }
    }
}
