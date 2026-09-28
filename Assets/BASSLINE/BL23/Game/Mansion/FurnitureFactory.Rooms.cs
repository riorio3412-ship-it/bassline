using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Mansion
{
    internal static partial class FurnitureFactory
    {
        // ------------------------------------------------------------------ kitchen
        static void Counter(Ctx c, MeshBuilder mb, bool island)
        {
            float W = c.W, D = c.D, H = c.H;
            // a wall counter's base opens (drawers over doors); the island stays a solid block with dummy fronts both sides
            if (!island) OpenCounterBase(c, mb, Color.Lerp(c.Pal.Wall, Color.white, 0.45f));
            else
            {
            mb.Set(S.WoodPainted, Color.Lerp(c.Pal.Wall, Color.white, 0.35f));
            mb.Box(new Vector3(0, (H - 0.05f) / 2 + 0.05f, 0), new Vector3(W - 0.04f, H - 0.1f, D - 0.06f));
            mb.Set(S.WoodDark, Darker(c.WoodC, 0.3f));
            mb.Box(new Vector3(0, 0.05f, 0.02f), new Vector3(W - 0.1f, 0.1f, D - 0.12f));
            // cabinet doors + brass pulls
            int n = Mathf.Max(1, Mathf.RoundToInt(W / 0.6f));
            for (int s = 0; s < 2; s++)
            {
                float z = s == 0 ? D / 2 - 0.02f : -D / 2 + 0.02f;
                for (int i = 0; i < n; i++)
                {
                    float x = -W / 2 + (i + 0.5f) * W / n;
                    mb.Set(S.WoodPainted, Color.Lerp(c.Pal.Wall, Color.white, 0.5f));
                    mb.Box(new Vector3(x, 0.47f, z), new Vector3(W / n - 0.06f, 0.62f, 0.02f));
                    mb.Set(S.Brass, Color.white);
                    mb.Box(new Vector3(x, 0.72f, z + (s == 0 ? 0.015f : -0.015f)), new Vector3(0.12f, 0.015f, 0.015f));
                }
            }
            }
            mb.Set(S.Marble, Color.Lerp(Color.white, c.Pal.FloorA, 0.3f));
            mb.BevelBox(new Vector3(0, H - 0.025f, 0), new Vector3(W, 0.05f, D), 0.01f);
            // clutter: cutting board, jars, a bowl of dark fruit
            var rnd = c.Rng;
            mb.Set(S.WoodLight, Color.white);
            mb.Box(new Vector3(-W * 0.25f, H + 0.012f, 0.02f), new Vector3(0.42f, 0.025f, 0.28f));
            for (int i = 0; i < 3; i++)
            {
                mb.Set(S.Glass, Color.white);
                var p = new Vector3(W * 0.22f + i * 0.13f, H, -D * 0.25f);
                mb.Push(p, 0); mb.Lathe(new[] { new Vector2(0.045f, 0), new Vector2(0.05f, 0.02f), new Vector2(0.05f, 0.16f), new Vector2(0.03f, 0.18f) }, 10); mb.Pop();
                mb.Set(S.Cloth, i == 0 ? new Color(0.7f, 0.1f, 0.1f) : i == 1 ? new Color(0.9f, 0.8f, 0.3f) : new Color(0.3f, 0.5f, 0.1f));
                mb.Cyl(p + Vector3.up * 0.01f, 0.04f, 0.1f, 8);
                mb.Set(S.Copper, Color.white);
                mb.Cyl(p + Vector3.up * 0.18f, 0.035f, 0.02f, 8);
            }
            if (island)
            {
                mb.Set(S.Porcelain, Color.white);
                mb.Push(new Vector3(W * 0.1f, H, 0), 0); mb.Lathe(new[] { new Vector2(0.06f, 0), new Vector2(0.16f, 0.07f), new Vector2(0.17f, 0.08f) }, 14); mb.Pop();
                for (int i = 0; i < 6; i++) { mb.Set(S.Velvet, new Color(0.35f, 0.02f, 0.12f)); mb.Sphere(new Vector3(W * 0.1f + ((float)rnd.NextDouble() - 0.5f) * 0.16f, H + 0.08f, ((float)rnd.NextDouble() - 0.5f) * 0.16f), 0.04f, 8, 6); }
            }
        }

        static void Stove(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.Iron, new Color(0.14f, 0.14f, 0.15f));
            mb.BevelBox(new Vector3(0, H / 2, 0), new Vector3(W, H, D), 0.03f);
            mb.Set(S.Chrome, Color.white);
            mb.Box(new Vector3(0, H - 0.02f, 0), new Vector3(W + 0.02f, 0.03f, D + 0.02f));
            mb.Rod(new Vector3(-W / 2 + 0.05f, H - 0.12f, D / 2 + 0.05f), new Vector3(W / 2 - 0.05f, H - 0.12f, D / 2 + 0.05f), 0.015f, 6, true);
            // oven doors with glowing fire windows
            foreach (float x in new[] { -W / 4, W / 4 })
            {
                mb.Set(S.Iron, new Color(0.2f, 0.2f, 0.21f));
                mb.Box(new Vector3(x, 0.4f, D / 2 + 0.01f), new Vector3(W / 2 - 0.08f, 0.5f, 0.02f));
                mb.Set(S.Glow, new Color(1f, 0.35f, 0.06f), MansionMats.GlowData(2.4f, 0.6f, 0, -2));
                mb.Box(new Vector3(x, 0.45f, D / 2 + 0.022f), new Vector3(0.18f, 0.08f, 0.005f));
            }
            // burners and a steaming pot
            mb.Set(S.Iron, new Color(0.05f, 0.05f, 0.05f));
            foreach (float x in new[] { -0.3f, 0.3f }) foreach (float z in new[] { -0.15f, 0.15f }) mb.Torus(new Vector3(x, H + 0.012f, z), 0.1f, 0.012f, 12, 4);
            mb.Set(S.Copper, Color.white);
            mb.Push(new Vector3(-0.3f, H + 0.02f, -0.15f), 0); mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.14f, 0.005f), new Vector2(0.15f, 0.2f), new Vector2(0.155f, 0.21f) }, 14); mb.Pop();
            mb.Set(S.Glow, new Color(0.9f, 0.25f, 0.05f), MansionMats.GlowData(0.8f, 0.4f, 0, -2));
            mb.Disc(new Vector3(-0.3f, H + 0.18f, -0.15f), 0.14f, 14, true);
            c.View.AddLight(c.Rv, c.W2(new Vector3(0, 0.45f, D / 2 + 0.3f)), new Color(1f, 0.45f, 0.15f), 1.3f, 2.5f, LightType.Point, false, 0.6f, fire: true);
        }

        static void Sink(Ctx c, MeshBuilder mb)
        {
            Counter(c, mb, false);
            float H = c.H;
            mb.Set(S.Porcelain, Color.white);
            mb.Box(new Vector3(0, H - 0.1f, 0.02f), new Vector3(0.7f, 0.2f, 0.45f), MeshBuilder.Faces.All);
            mb.Set(S.Obsidian, Color.white);
            mb.Box(new Vector3(0, H + 0.001f, 0.02f), new Vector3(0.6f, 0.002f, 0.36f), MeshBuilder.Faces.PY);
            mb.Set(S.Brass, Color.white);
            mb.Tube(new List<Vector3> { new Vector3(0, H, -0.25f), new Vector3(0, H + 0.35f, -0.25f), new Vector3(0, H + 0.4f, -0.15f), new Vector3(0, H + 0.3f, -0.05f) }, 0.015f, 6);
            foreach (float x in new[] { -0.12f, 0.12f }) mb.Cyl(new Vector3(x, H, -0.25f), 0.025f, 0.06f, 8);
        }

        static void Fridge(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.GlossPaint, Color.Lerp(new Color(0.92f, 0.88f, 0.75f), c.Pal.Accent2, 0.15f));
            mb.BevelBox(new Vector3(0, H / 2 + 0.06f, 0), new Vector3(W, H - 0.12f, D), 0.1f);
            mb.Set(S.Chrome, Color.white);
            mb.Box(new Vector3(W / 2 - 0.1f, 1.2f, D / 2 + 0.03f), new Vector3(0.04f, 0.35f, 0.04f));
            mb.Box(new Vector3(0, 1.45f, D / 2 + 0.005f), new Vector3(W - 0.04f, 0.01f, 0.01f));
            mb.Set(S.Chrome, Color.white);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Cyl(new Vector3(x * (W / 2 - 0.08f), 0, z * (D / 2 - 0.08f)), 0.03f, 0.06f, 8);
            // brand plate: a little eye
            mb.Set(S.Gold, c.Pal.Trim);
            mb.Ellipsoid(new Vector3(0, H - 0.2f, D / 2 + 0.005f), new Vector3(0.07f, 0.03f, 0.01f), 10, 4);
        }

        static void KnifeRack(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.WoodLight, Color.white);
            mb.Push(new Vector3(0, 0, 0), Quaternion.Euler(-12, 0, 0), Vector3.one);
            mb.BevelBox(new Vector3(0, 0.14f, 0), new Vector3(c.W * 0.6f, 0.28f, c.D * 0.8f), 0.02f);
            mb.Set(S.Obsidian, Color.white);
            for (int i = 0; i < 5; i++) mb.Box(new Vector3(-0.1f + i * 0.05f, 0.33f, 0), new Vector3(0.02f, 0.1f, 0.03f));
            mb.Pop();
        }

        // ------------------------------------------------------------------ library / study
        static void Bookshelf(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodDark, c.WoodC);
            mb.Box(new Vector3(-W / 2 + 0.03f, H / 2, 0), new Vector3(0.06f, H, D));
            mb.Box(new Vector3(W / 2 - 0.03f, H / 2, 0), new Vector3(0.06f, H, D));
            mb.Box(new Vector3(0, H / 2, -D / 2 + 0.01f), new Vector3(W, H, 0.02f));
            // crown with teeth
            mb.Box(new Vector3(0, H - 0.05f, 0.02f), new Vector3(W + 0.08f, 0.1f, D + 0.06f));
            mb.Set(S.Gold, c.Pal.Trim);
            mb.Box(new Vector3(0, H - 0.11f, D / 2 + 0.03f), new Vector3(W, 0.02f, 0.02f));
            mb.Set(S.Bone, Color.white);
            for (float x = -W / 2 + 0.06f; x < W / 2 - 0.04f; x += 0.09f) mb.Box(new Vector3(x, H - 0.15f, D / 2 + 0.02f), new Vector3(0.04f, 0.06f, 0.03f));
            int shelves = 6;
            float sh = (H - 0.25f) / shelves;
            for (int i = 0; i < shelves; i++)
            {
                float y = 0.1f + i * sh;
                mb.Set(S.WoodDark, c.WoodC);
                mb.Box(new Vector3(0, y, 0), new Vector3(W - 0.1f, 0.03f, D - 0.04f));
                if (i == shelves - 1 && c.Var % 2 == 0)
                {
                    // top shelf: a skull, a jar with an eye, a stuffed bird
                    mb.Set(S.Bone, Color.white); mb.Sphere(new Vector3(-0.4f, y + 0.1f, 0), 0.09f, 10, 7, 0.95f);
                    mb.Set(S.Glass, Color.white); mb.Cyl(new Vector3(0.3f, y + 0.015f, 0), 0.07f, 0.2f, 10);
                    mb.Set(S.Porcelain, Color.white); mb.Sphere(new Vector3(0.3f, y + 0.1f, 0), 0.035f, 8, 6);
                    mb.Set(S.Obsidian, Color.white); mb.Sphere(new Vector3(0.3f, y + 0.1f, 0.03f), 0.015f, 6, 4);
                    continue;
                }
                Books(mb, new Vector3(-W / 2 + 0.07f, y + 0.015f, 0.02f), W - 0.14f, sh - 0.08f, D - 0.1f, c.Rng, c.Pal);
            }
            // library ladder rail
            if (c.Var == 1)
            {
                mb.Set(S.Brass, Color.white);
                mb.Rod(new Vector3(-W / 2, H - 0.3f, D / 2 + 0.08f), new Vector3(W / 2, H - 0.3f, D / 2 + 0.08f), 0.015f, 6, true);
            }
        }

        static void Fireplace(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.Marble, Color.Lerp(Color.white, c.Pal.FloorA, 0.35f));
            // hearth, jambs, mantel
            mb.Box(new Vector3(0, 0.04f, 0.1f), new Vector3(W, 0.08f, D + 0.2f));
            mb.Box(new Vector3(-W / 2 + 0.25f, H * 0.45f, 0), new Vector3(0.5f, H * 0.9f, D));
            mb.Box(new Vector3(W / 2 - 0.25f, H * 0.45f, 0), new Vector3(0.5f, H * 0.9f, D));
            mb.Box(new Vector3(0, H * 0.78f, 0), new Vector3(W - 0.9f, H * 0.24f, D));
            mb.BevelBox(new Vector3(0, H - 0.06f, 0.05f), new Vector3(W + 0.2f, 0.12f, D + 0.15f), 0.03f);
            // carved faces on the jambs (grotesque masks)
            foreach (float x in new[] { -1f, 1f })
            {
                mb.Set(S.Marble, Color.Lerp(Color.white, c.Pal.FloorA, 0.2f));
                mb.Ellipsoid(new Vector3(x * (W / 2 - 0.25f), H * 0.62f, D / 2 + 0.03f), new Vector3(0.14f, 0.18f, 0.06f), 10, 8);
                mb.Set(S.Obsidian, Color.white);
                mb.Sphere(new Vector3(x * (W / 2 - 0.25f) - 0.05f, H * 0.65f, D / 2 + 0.085f), 0.025f, 6, 4);
                mb.Sphere(new Vector3(x * (W / 2 - 0.25f) + 0.05f, H * 0.65f, D / 2 + 0.085f), 0.025f, 6, 4);
                mb.Ellipsoid(new Vector3(x * (W / 2 - 0.25f), H * 0.56f, D / 2 + 0.08f), new Vector3(0.05f, 0.03f, 0.01f), 8, 4);
            }
            // firebox (dark) + logs + fire
            mb.Set(S.Brick, new Color(0.25f, 0.15f, 0.12f));
            mb.QuadAuto(new Vector3(-W / 2 + 0.5f, 0.08f, -D / 2 + 0.05f), new Vector3(-W / 2 + 0.5f, H * 0.66f, -D / 2 + 0.05f), new Vector3(W / 2 - 0.5f, H * 0.66f, -D / 2 + 0.05f), new Vector3(W / 2 - 0.5f, 0.08f, -D / 2 + 0.05f), Vector3.forward);
            mb.Set(S.WoodWorn, new Color(0.3f, 0.2f, 0.15f));
            mb.Rod(new Vector3(-0.35f, 0.14f, 0f), new Vector3(0.35f, 0.16f, -0.05f), 0.06f, 8, true);
            mb.Rod(new Vector3(-0.3f, 0.22f, -0.1f), new Vector3(0.3f, 0.2f, 0.05f), 0.05f, 8, true);
            mb.Set(S.Glow, new Color(1f, 0.3f, 0.05f), MansionMats.GlowData(2.5f, 0.8f, 0, -2));
            mb.Box(new Vector3(0, 0.1f, -0.02f), new Vector3(0.7f, 0.04f, 0.3f));
            var flames = new List<Vector3>();
            for (int i = 0; i < 7; i++) flames.Add(new Vector3(-0.3f + i * 0.1f, 0.2f, -0.03f + (i % 2) * 0.05f));
            foreach (var w in flames) MansionView.FlameQuad(mb, w, 0.25f + (w.x * 7 % 3) * 0.05f, -2);
            c.View.AddLight(c.Rv, c.W2(new Vector3(0, 0.5f, D / 2 + 0.4f)), new Color(1f, 0.5f, 0.2f), 4.5f, 6.5f, LightType.Point, true, 0.7f, fire: true);
            // mantel dressing: clock + candlesticks
            var mc = Models.Get("mantel_clock_01");
            if (mc != null) { var g = Models.Place(mc, c.T, Vector3.zero, 0, new Vector3(0.45f, 0, 0.2f), null); g.transform.localPosition = new Vector3(0, H, 0.05f); g.transform.localRotation = Quaternion.identity; }
            var fl2 = new List<Vector3>();
            foreach (float x in new[] { -W * 0.35f, W * 0.35f })
            {
                mb.Set(S.Brass, c.Pal.Trim);
                mb.Push(new Vector3(x, H, 0.05f), 0); mb.Lathe(new[] { new Vector2(0.06f, 0), new Vector2(0.015f, 0.03f), new Vector2(0.012f, 0.22f), new Vector2(0.03f, 0.24f) }, 8); mb.Pop();
                Candle(mb, new Vector3(x, H + 0.24f, 0.05f), 0.16f, 0.016f, -2, new Vector3(x, H + 0.4f, 0.05f), fl2);
            }
            foreach (var w in fl2) MansionView.FlameQuad(mb, w, 0.06f, -2);
        }

        // ------------------------------------------------------------------ music
        static void GrandPiano(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            // curved body: extruded polygon (straight left, curved right tail)
            var poly = new List<Vector2>();
            float hw = W / 2, hd = D / 2;
            poly.Add(new Vector2(-hw, hd)); poly.Add(new Vector2(hw, hd));
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float x = Mathf.Lerp(hw, -hw * 0.1f, t) + Mathf.Sin(t * Mathf.PI) * 0.15f;
                float z = Mathf.Lerp(hd * 0.3f, -hd, t);
                poly.Add(new Vector2(x, z));
            }
            poly.Add(new Vector2(-hw, -hd * 0.7f));
            poly.Reverse();
            mb.Set(S.Obsidian, Color.white);
            mb.Prism(poly, 0.62f, 0.95f, true, true);
            // propped lid
            mb.Push(new Vector3(-hw, 0.96f, 0), Quaternion.Euler(0, 0, 38), Vector3.one);
            var lid = new List<Vector2>(); foreach (var p in poly) lid.Add(new Vector2(p.x + hw, p.y));
            mb.Prism(lid, 0f, 0.02f, true, true);
            mb.Pop();
            mb.Rod(new Vector3(hw * 0.3f, 0.95f, 0.1f), new Vector3(hw * 0.4f, 1.55f, 0.1f), 0.01f, 5, false);
            // keyboard
            mb.Set(S.Porcelain, Color.white);
            mb.Box(new Vector3(0, 0.74f, hd + 0.08f), new Vector3(W * 0.9f, 0.03f, 0.16f));
            mb.Set(S.Obsidian, Color.white);
            for (int i = 0; i < 36; i++) { if (i % 7 == 2 || i % 7 == 6) continue; mb.Box(new Vector3(-W * 0.44f + i * W * 0.88f / 36, 0.765f, hd + 0.05f), new Vector3(0.012f, 0.015f, 0.09f)); }
            // legs
            foreach (var p in new[] { new Vector3(-hw + 0.12f, 0, hd - 0.1f), new Vector3(hw - 0.12f, 0, hd - 0.1f), new Vector3(0, 0, -hd + 0.3f) }) TurnedLeg(mb, p, 0.62f, 0.05f);
            // candelabrum on the piano
            var fl = new List<Vector3>();
            TableCandelabra(c, mb, new Vector3(-hw * 0.4f, 0.95f, -hd * 0.3f), fl);
            Flames(c, mb, fl, 0.06f, true, 1.2f, 3f);
        }

        static void UprightPiano(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodCherry, Darker(c.WoodC, 0.3f));
            mb.BevelBox(new Vector3(0, H / 2, -D / 2 + 0.22f), new Vector3(W, H, 0.4f), 0.02f);
            mb.Box(new Vector3(0, 0.72f, 0.05f), new Vector3(W, 0.08f, D - 0.1f));
            mb.Set(S.Porcelain, Color.white); mb.Box(new Vector3(0, 0.775f, 0.12f), new Vector3(W * 0.9f, 0.02f, 0.14f));
            mb.Set(S.Obsidian, Color.white);
            for (int i = 0; i < 30; i++) { if (i % 7 == 2 || i % 7 == 6) continue; mb.Box(new Vector3(-W * 0.43f + i * W * 0.86f / 30, 0.795f, 0.1f), new Vector3(0.012f, 0.012f, 0.08f)); }
            foreach (float x in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.05f), 0.36f, 0.15f), new Vector3(0.06f, 0.72f, 0.06f));
            mb.Set(S.Paper, Color.white); mb.Box(new Vector3(0, 1.0f, -D / 2 + 0.43f), new Vector3(0.4f, 0.28f, 0.01f));
            var fl = new List<Vector3>();
            foreach (float x in new[] { -W * 0.38f, W * 0.38f })
            {
                mb.Set(S.Brass, Color.white); mb.Rod(new Vector3(x, 1.05f, -D / 2 + 0.43f), new Vector3(x, 1.05f, -D / 2 + 0.55f), 0.01f, 5, false);
                Candle(mb, new Vector3(x, 1.05f, -D / 2 + 0.55f), 0.12f, 0.013f, -2, new Vector3(x, 1.17f, -D / 2 + 0.55f), fl);
            }
            Flames(c, mb, fl, 0.05f, true, 0.8f, 2.5f);
        }

        static void Organ(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodDark, c.WoodC);
            mb.Box(new Vector3(0, 0.55f, 0.1f), new Vector3(W * 0.6f, 1.1f, D - 0.2f));
            mb.Set(S.Porcelain, Color.white); mb.Box(new Vector3(0, 0.82f, D / 2 - 0.1f), new Vector3(W * 0.5f, 0.02f, 0.12f));
            mb.Box(new Vector3(0, 0.9f, D / 2 - 0.2f), new Vector3(W * 0.5f, 0.02f, 0.12f));
            // pipes: brass rising like teeth, tallest in the middle
            int n = 23;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1) * 2 - 1;
                float h = 1.4f + (1 - t * t) * (H - 1.5f) + (i % 3) * 0.1f;
                float x = t * (W / 2 - 0.08f);
                mb.Set(S.Brass, Color.Lerp(Color.white, c.Pal.Trim, 0.5f));
                mb.Cyl(new Vector3(x, 1.1f, -D / 2 + 0.2f), 0.05f, h - 1.1f, 8);
                mb.Push(new Vector3(x, h, -D / 2 + 0.2f), 0); mb.Lathe(new[] { new Vector2(0.05f, 0), new Vector2(0.001f, 0.08f) }, 8); mb.Pop();
                mb.Set(S.Obsidian, Color.white);
                mb.Box(new Vector3(x, 1.35f, -D / 2 + 0.25f), new Vector3(0.05f, 0.03f, 0.01f));
            }
        }

        static void MusicStand(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.Iron, new Color(0.2f, 0.2f, 0.2f));
            for (int k = 0; k < 3; k++) { float a = k / 3f * Mathf.PI * 2; mb.Rod(new Vector3(0, 0.25f, 0), new Vector3(Mathf.Cos(a) * 0.25f, 0.01f, Mathf.Sin(a) * 0.25f), 0.008f, 4, false); }
            mb.Rod(new Vector3(0, 0.25f, 0), new Vector3(0, 1.05f, 0), 0.01f, 6, false);
            mb.Push(new Vector3(0, 1.1f, 0), Quaternion.Euler(-20, 0, 0), Vector3.one);
            mb.Box(new Vector3(0, 0, 0), new Vector3(0.48f, 0.32f, 0.01f));
            mb.Set(S.Paper, Color.white); mb.Box(new Vector3(0, 0.02f, 0.01f), new Vector3(0.42f, 0.28f, 0.004f));
            mb.Pop();
        }

        static void Drums(Ctx c, MeshBuilder mb)
        {
            void Drum(Vector3 p, float r, float h, float tilt)
            {
                mb.Push(p, Quaternion.Euler(tilt, 0, 0), Vector3.one);
                mb.Set(S.GlossPaint, c.Pal.Neon * 0.8f); mb.Cyl(Vector3.zero, r, h, 16, false);
                mb.Set(S.Paper, new Color(0.95f, 0.93f, 0.88f)); mb.Disc(new Vector3(0, h, 0), r, 16, true);
                mb.Set(S.Chrome, Color.white); mb.Torus(new Vector3(0, h, 0), r, 0.01f, 16, 4); mb.Torus(Vector3.zero, r, 0.01f, 16, 4);
                mb.Pop();
            }
            // kick drum lying sideways
            mb.Push(new Vector3(0, 0.3f, 0.1f), Quaternion.Euler(90, 0, 0), Vector3.one);
            mb.Set(S.GlossPaint, c.Pal.Neon * 0.8f); mb.Cyl(new Vector3(0, -0.2f, 0), 0.3f, 0.4f, 18, false);
            mb.Set(S.Paper, new Color(0.9f, 0.88f, 0.8f)); mb.Disc(new Vector3(0, 0.2f, 0), 0.3f, 18, true);
            mb.Pop();
            Drum(new Vector3(-0.55f, 0.55f, 0.2f), 0.18f, 0.16f, -10);
            Drum(new Vector3(0.2f, 0.72f, -0.15f), 0.13f, 0.12f, -15);
            Drum(new Vector3(0.55f, 0.45f, 0.1f), 0.2f, 0.3f, 0);
            mb.Set(S.Chrome, Color.white);
            foreach (var p in new[] { new Vector3(-0.75f, 0, -0.3f), new Vector3(0.75f, 0, -0.4f) })
            {
                mb.Rod(p, p + Vector3.up * 1.05f, 0.012f, 6, false);
                mb.Set(S.Brass, c.Pal.Trim); mb.Push(p + Vector3.up * 1.05f, 0); mb.Lathe(new[] { new Vector2(0.001f, 0.02f), new Vector2(0.22f, 0f) }, 16); mb.Pop(); mb.Set(S.Chrome, Color.white);
            }
        }

        static void Speaker(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.WoodDark, new Color(0.3f, 0.25f, 0.25f));
            mb.BevelBox(new Vector3(0, c.H / 2, 0), new Vector3(c.W, c.H, c.D), 0.02f);
            mb.Set(S.Cloth, new Color(0.08f, 0.08f, 0.08f));
            mb.Box(new Vector3(0, c.H / 2, c.D / 2 + 0.005f), new Vector3(c.W - 0.08f, c.H - 0.1f, 0.01f));
            mb.Set(S.Rubber, Color.white);
            foreach (float y in new[] { c.H * 0.3f, c.H * 0.68f }) { mb.Push(new Vector3(0, y, c.D / 2 + 0.01f), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Lathe(new[] { new Vector2(0.001f, -0.06f), new Vector2(0.1f * (y > 1 ? 0.7f : 1.3f), 0f) }, 16); mb.Pop(); }
            mb.Set(S.Glow, c.Pal.Neon, MansionMats.GlowData(3f, 0.3f, 0, c.Circuit)); mb.Sphere(new Vector3(c.W / 2 - 0.08f, c.H - 0.08f, c.D / 2 + 0.01f), 0.015f, 6, 4);
        }

        // ------------------------------------------------------------------ theatre / chapel
        static void Stage(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            // the kernel treats the stage as walkable: an apron step (half height) along the front lets the player climb it
            float step = 0.42f, front = D / 2 - step;
            mb.Set(S.WoodWorn, Darker(c.WoodC, 0.1f));
            mb.Box(new Vector3(0, H / 2, -step / 2), new Vector3(W, H, D - step));
            mb.Box(new Vector3(0, H / 4, front + step / 2), new Vector3(W, H / 2, step));
            mb.Set(S.Velvet, Darker(c.Pal.Fabric, 0.3f));
            mb.Box(new Vector3(0, H * 0.75f, front + 0.01f), new Vector3(W, H / 2 - 0.02f, 0.02f));
            mb.Box(new Vector3(0, H / 4, D / 2 + 0.01f), new Vector3(W, H / 2 - 0.02f, 0.02f));
            mb.Set(S.Gold, c.Pal.Trim);
            mb.Box(new Vector3(0, H / 2 + 0.01f, D / 2 - 0.02f), new Vector3(W, 0.02f, 0.04f));
            var body = c.Go.AddComponent<BoxCollider>(); body.center = new Vector3(0, H / 2, -step / 2); body.size = new Vector3(W, H, D - step);
            var apron = c.Go.AddComponent<BoxCollider>(); apron.center = new Vector3(0, H / 4, front + step / 2); apron.size = new Vector3(W, H / 2, step);
            // footlights along the upper edge
            for (int i = 0; i < 9; i++)
            {
                float x = -W / 2 + 0.4f + i * (W - 0.8f) / 8;
                mb.Set(S.Brass, Color.white); mb.Box(new Vector3(x, H + 0.04f, front - 0.1f), new Vector3(0.18f, 0.08f, 0.1f));
                mb.Set(S.Glow, c.Pal.Warm, MansionMats.GlowData(3f, 0.1f, 0, c.Circuit)); mb.Box(new Vector3(x, H + 0.05f, front - 0.045f), new Vector3(0.14f, 0.05f, 0.01f));
            }
            // curtains: heavy velvet folds on both sides + a valance, hanging from the ceiling
            float ceil = c.Rv.CeilY - c.Rv.FloorY;
            foreach (float sx in new[] { -1f, 1f }) DecorBox(c, new Vector3(sx > 0 ? W / 2 - 1.45f : -W / 2, H, -D / 2 + 0.1f), new Vector3(sx > 0 ? W / 2 : -W / 2 + 1.45f, ceil - 0.2f, -D / 2 + 0.45f), "StageCurtain");
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 7; k++)
                {
                    float x = side * (W / 2 - 0.12f - k * 0.18f);
                    mb.Set(S.Velvet, Color.Lerp(c.Pal.Fabric, c.Pal.Neon, 0.2f) * (0.8f + (k % 2) * 0.25f));
                    var pts = new List<Vector3>();
                    for (int j = 0; j <= 6; j++) { float t = j / 6f; float sway = Mathf.Sin(t * Mathf.PI) * side * 0.1f * (k * 0.3f); pts.Add(new Vector3(x - sway, H + (ceil - H - 0.2f) * (1 - t), -D / 2 + 0.25f + (k % 2) * 0.06f)); }
                    mb.Tube(pts, 0.1f, 6);
                }
            mb.Set(S.Velvet, c.Pal.Fabric);
            for (int k = 0; k < 16; k++)
            {
                float x = -W / 2 + (k + 0.5f) * W / 16;
                mb.Push(new Vector3(x, ceil - 0.5f, -D / 2 + 0.2f), 0);
                mb.Lathe(new[] { new Vector2(0.001f, -0.25f), new Vector2(0.15f, -0.1f), new Vector2(0.2f, 0.3f) }, 8);
                mb.Pop();
            }
            mb.Set(S.Gold, c.Pal.Trim);
            mb.Box(new Vector3(0, ceil - 0.12f, -D / 2 + 0.2f), new Vector3(W, 0.2f, 0.12f));
            // back cloth: painted moon
            mb.Set(S.Velvet, Darker(c.Pal.Wall, 0.4f));
            mb.Box(new Vector3(0, (ceil + H) / 2, -D / 2 + 0.05f), new Vector3(W - 0.3f, ceil - H, 0.02f));
            mb.Set(S.Glow, new Color(1f, 0.95f, 0.8f), MansionMats.GlowData(0.9f, 0f, 0, c.Circuit));
            mb.Push(new Vector3(W * 0.2f, H + (ceil - H) * 0.6f, -D / 2 + 0.07f), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Disc(Vector3.zero, 0.55f, 24, true); mb.Pop();
            // stage spot from above
            c.View.AddLight(c.Rv, c.W2(new Vector3(0, ceil - 0.3f, D / 2 + 1.5f)), Color.Lerp(c.Pal.Warm, Color.white, 0.4f), 22f, 9f, LightType.Spot, true, 0.03f, false, false, false, Quaternion.LookRotation(c.T.TransformDirection(new Vector3(0, -1.2f, -1f).normalized)), 42f);
        }

        static void SeatRow(Ctx c, MeshBuilder mb, int n, float legH)
        {
            float W = c.W, D = c.D;
            float sw = W / n;
            for (int i = 0; i < n; i++)
            {
                float x = -W / 2 + (i + 0.5f) * sw;
                bool broken = c.Rv.Room.Type == RoomType.EmptyAuditorium && ((i * 31 + c.F.Id * 7) % 11 == 0);
                mb.Set(S.Velvet, broken ? Darker(c.Fabric, 0.5f) : c.Fabric);
                // seat tipped up (empty theatre) or down
                mb.Push(new Vector3(x, 0.45f, 0.05f), Quaternion.Euler(broken ? 0 : -70, 0, 0), Vector3.one);
                mb.BevelBox(new Vector3(0, 0.0f, 0.18f), new Vector3(sw - 0.1f, 0.08f, 0.4f), 0.03f);
                mb.Pop();
                mb.BevelBox(new Vector3(x, 0.72f, -D / 2 + 0.1f), new Vector3(sw - 0.1f, 0.55f, 0.1f), 0.04f);
                mb.Set(S.Iron, new Color(0.18f, 0.16f, 0.14f));
                mb.Box(new Vector3(x - sw / 2 + 0.03f, 0.35f, 0), new Vector3(0.05f, 0.7f, D - 0.2f));
                mb.Set(S.Gold, c.Pal.Trim);
                mb.Box(new Vector3(x - sw / 2 + 0.03f, 0.64f, 0.05f), new Vector3(0.06f, 0.04f, D * 0.6f));
                // seat number plaque
                mb.Box(new Vector3(x, 0.98f, -D / 2 + 0.16f), new Vector3(0.07f, 0.03f, 0.005f));
            }
            mb.Set(S.Iron, new Color(0.18f, 0.16f, 0.14f));
            mb.Box(new Vector3(W / 2 - 0.03f, 0.35f, 0), new Vector3(0.05f, 0.7f, D - 0.2f));
        }

        static void Pew(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D;
            mb.Set(S.WoodDark, c.WoodC);
            mb.Box(new Vector3(0, 0.45f, 0.05f), new Vector3(W - 0.1f, 0.06f, D - 0.25f));
            mb.Push(new Vector3(0, 0.75f, -D / 2 + 0.1f), Quaternion.Euler(-8, 0, 0), Vector3.one); mb.Box(Vector3.zero, new Vector3(W - 0.1f, 0.6f, 0.05f)); mb.Pop();
            foreach (float x in new[] { -1f, 1f })
            {
                // gothic end panels with pointed tops
                var poly = new List<Vector2> { new Vector2(-D / 2, 0), new Vector2(-D / 2, 1.05f), new Vector2(-D / 2 + 0.12f, 1.2f), new Vector2(0, 0.95f), new Vector2(D / 2, 0.7f), new Vector2(D / 2, 0) };
                mb.Push(new Vector3(x * (W / 2 - 0.03f), 0, 0), Quaternion.Euler(0, 90, 0), Vector3.one);
                var pts = new List<Vector2>(); foreach (var p in poly) pts.Add(new Vector2(p.x, p.y));
                // extrude along local x (after rotation it's along world z) -> build as prism in XZ then rotate: simpler boxes
                mb.Pop();
                mb.Box(new Vector3(x * (W / 2 - 0.03f), 0.52f, 0), new Vector3(0.06f, 1.04f, D - 0.05f));
                mb.Set(S.Gold, c.Pal.Trim);
                mb.Push(new Vector3(x * (W / 2 - 0.03f), 1.04f, -D / 2 + 0.08f), 0); mb.Lathe(new[] { new Vector2(0.04f, 0), new Vector2(0.001f, 0.16f) }, 6); mb.Pop();
                mb.Set(S.WoodDark, c.WoodC);
            }
            // kneeler + hymn books
            mb.Set(S.Velvet, c.Pal.Carpet);
            mb.Box(new Vector3(0, 0.1f, D / 2 - 0.1f), new Vector3(W - 0.2f, 0.08f, 0.14f));
            mb.Set(S.Books, new Color(0.2f, 0.05f, 0.05f));
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(-W / 3 + i * W / 3, 0.52f, 0.1f), new Vector3(0.14f, 0.04f, 0.2f));
        }

        static void Altar(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.Marble, Color.Lerp(Color.white, c.Pal.FloorA, 0.3f));
            mb.Box(new Vector3(0, 0.1f, 0.1f), new Vector3(W + 0.4f, 0.2f, D + 0.3f));
            mb.BevelBox(new Vector3(0, (H + 0.2f) / 2, 0), new Vector3(W, H - 0.2f, D), 0.03f);
            mb.Set(S.Linen, new Color(0.96f, 0.95f, 0.92f));
            mb.Box(new Vector3(0, H + 0.005f, 0), new Vector3(W + 0.06f, 0.01f, D + 0.04f));
            mb.Set(S.Velvet, c.Pal.Fabric);
            mb.Box(new Vector3(0, H - 0.3f, D / 2 + 0.03f), new Vector3(0.7f, 0.6f, 0.01f));
            mb.Set(S.Gold, c.Pal.Trim);
            mb.Ellipsoid(new Vector3(0, H - 0.25f, D / 2 + 0.04f), new Vector3(0.14f, 0.07f, 0.01f), 12, 4);
            // memorial photographs (portraits) in black frames, lilies, candles of different heights
            for (int i = 0; i < 5; i++)
            {
                float x = -W * 0.4f + i * W * 0.2f;
                var p = new Vector3(x, H + 0.01f, -D * 0.25f);
                mb.Set(S.Obsidian, Color.white);
                mb.Push(p, Quaternion.Euler(-12, 0, 0), Vector3.one);
                mb.Box(new Vector3(0, 0.17f, 0), new Vector3(0.22f, 0.3f, 0.02f));
                var r = ProcTex.PortraitRect(i + c.Var);
                mb.Set(S.Painting, Color.white);
                mb.FaceUV(new Vector3(0.09f, 0.04f, 0.012f), new Vector3(-0.18f, 0, 0), new Vector3(0, 0.26f, 0), new Rect(r.xMax, r.yMin, -r.width, r.height));
                mb.Pop();
                // black ribbon
                mb.Set(S.Velvet, new Color(0.03f, 0.03f, 0.03f));
                mb.Box(new Vector3(x + 0.08f, H + 0.32f, -D * 0.25f + 0.02f), new Vector3(0.04f, 0.04f, 0.01f));
            }
            var fl = new List<Vector3>();
            for (int i = 0; i < 9; i++)
            {
                float x = -W * 0.45f + i * W * 0.9f / 8; float h = 0.1f + (i * 7 % 5) * 0.06f;
                Candle(mb, new Vector3(x, H + 0.01f, D * 0.3f), h, 0.025f, -2, new Vector3(x, H + 0.01f + h, D * 0.3f), fl);
            }
            Flames(c, mb, fl, 0.07f, true, 2.5f, 5f);
            Lilies(mb, new Vector3(-W / 2 + 0.2f, H, 0), c.Rng);
            Lilies(mb, new Vector3(W / 2 - 0.2f, H, 0), c.Rng);
        }

        static void Lilies(MeshBuilder mb, Vector3 p, System.Random rnd)
        {
            mb.Set(S.Porcelain, new Color(0.9f, 0.88f, 0.85f));
            mb.Push(p, 0); mb.Lathe(new[] { new Vector2(0.06f, 0), new Vector2(0.08f, 0.1f), new Vector2(0.05f, 0.22f), new Vector2(0.07f, 0.26f) }, 10); mb.Pop();
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2 + (float)rnd.NextDouble();
                var tip = p + new Vector3(Mathf.Cos(a) * 0.15f, 0.55f + (float)rnd.NextDouble() * 0.2f, Mathf.Sin(a) * 0.15f);
                mb.Set(S.Leaf, new Color(0.25f, 0.45f, 0.2f)); mb.Rod(p + Vector3.up * 0.22f, tip, 0.006f, 4, false);
                mb.Set(S.Porcelain, Color.white);
                mb.Push(tip, Quaternion.FromToRotation(Vector3.up, (tip - p).normalized), Vector3.one);
                mb.Lathe(new[] { new Vector2(0.004f, 0), new Vector2(0.03f, 0.05f), new Vector2(0.06f, 0.08f) }, 6);
                mb.Pop();
            }
        }

        static void Candelabra(Ctx c, MeshBuilder mb)
        {
            float H = c.H;
            mb.Set(S.Iron, new Color(0.15f, 0.13f, 0.12f));
            for (int k = 0; k < 3; k++) { float a = k / 3f * Mathf.PI * 2; ClawFoot(mb, new Vector3(Mathf.Cos(a) * 0.16f, 0, Mathf.Sin(a) * 0.16f), 0.04f); mb.Rod(new Vector3(Mathf.Cos(a) * 0.16f, 0.06f, Mathf.Sin(a) * 0.16f), new Vector3(0, 0.3f, 0), 0.015f, 5, false); }
            mb.Push(Vector3.zero, 0);
            mb.Lathe(new[] { new Vector2(0.03f, 0.28f), new Vector2(0.018f, 0.5f), new Vector2(0.03f, 0.55f), new Vector2(0.016f, 0.6f), new Vector2(0.018f, H - 0.35f), new Vector2(0.04f, H - 0.3f) }, 8);
            mb.Pop();
            var fl = new List<Vector3>();
            for (int k = 0; k < 7; k++)
            {
                float a = k / 6f * Mathf.PI; float r = k == 3 ? 0 : 0.22f + (k % 2) * 0.05f;
                var arm = new Vector3(Mathf.Cos(a) * r, H - 0.25f + (k == 3 ? 0.12f : Mathf.Sin(a) * 0.05f), 0);
                mb.Set(S.Iron, new Color(0.15f, 0.13f, 0.12f));
                mb.Tube(new List<Vector3> { new Vector3(0, H - 0.32f, 0), new Vector3(arm.x * 0.6f, H - 0.33f, 0), arm }, 0.01f, 5);
                mb.Push(arm, 0); mb.Lathe(new[] { new Vector2(0.001f, -0.01f), new Vector2(0.035f, 0f), new Vector2(0.03f, 0.015f) }, 8); mb.Pop();
                float ch = 0.12f + (k * 7 % 4) * 0.05f;
                Candle(mb, arm + Vector3.up * 0.01f, ch, 0.016f, -2, arm + Vector3.up * (0.01f + ch), fl);
                // thick overflowing wax pooling on the drip pans
                mb.Set(S.Wax, Color.white); mb.Sphere(arm + Vector3.up * 0.015f, 0.03f, 6, 4, 0.4f);
            }
            Flames(c, mb, fl, 0.075f, true, 2.2f, 4.5f);
            // melted wax puddle on the floor
            mb.Set(S.Wax, Color.white);
            mb.Push(new Vector3(0.05f, 0.003f, 0.02f), 0); mb.Lathe(new[] { new Vector2(0.001f, 0.006f), new Vector2(0.12f, 0f) }, 12); mb.Pop();
        }
    }
}
