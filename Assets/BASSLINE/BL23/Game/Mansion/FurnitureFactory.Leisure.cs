using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>Leisure / activity furniture: the pieces that give each big room several things to do.
    /// Procedural builds in the mansion's dream-gothic style (gilded trim, neon accents, slightly wrong proportions).</summary>
    internal static partial class FurnitureFactory
    {
        static bool DispatchLeisure(Ctx c, MeshBuilder mb)
        {
            switch (c.F.Type)
            {
                case "BarCounter": BarCounter(c, mb); return true;
                case "BarStool": BarStool(c, mb); return true;
                case "Gramophone": Gramophone(c, mb); return true;
                case "Jukebox": Jukebox(c, mb); return true;
                case "Dartboard": Dartboard(c, mb); return true;
                case "ChessTable": ChessTable(c, mb); return true;
                case "Globe": GlobeStand(c, mb); return true;
                case "DivingBoard": DivingBoard(c, mb); return true;
                case "FilmProjector": FilmProjector(c, mb); return true;
                case "Telescope": Telescope(c, mb); return true;
                case "Harp": Harp(c, mb); return true;
                case "Lectern": Lectern(c, mb); return true;
                case "SewingTable": SewingTable(c, mb); return true;
                case "BirdCage": BirdCage(c, mb); return true;
                case "PunchingBag": PunchingBag(c, mb); return true;
                case "DisplayCase": DisplayCase(c, mb); return true;
                case "CardCatalog": CardCatalog(c, mb); return true;
            }
            return false;
        }

        static void BarCounter(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            // curved-front body, dark lacquer with a gilded rail and a neon strip at the kick
            mb.Set(S.WoodCherry, Color.Lerp(c.WoodC, new Color(0.35f, 0.12f, 0.14f), 0.5f));
            mb.BevelBox(new Vector3(0, H * 0.48f, 0), new Vector3(W, H * 0.96f, D * 0.8f), 0.03f);
            mb.Set(S.Marble, new Color(0.92f, 0.9f, 0.95f));
            mb.BevelBox(new Vector3(0, H - 0.025f, 0.03f), new Vector3(W + 0.08f, 0.05f, D), 0.012f);
            mb.Set(S.Gold, Color.white);
            mb.Rod(new Vector3(-W / 2, 0.22f, D / 2 + 0.06f), new Vector3(W / 2, 0.22f, D / 2 + 0.06f), 0.02f, 8, true);
            for (int i = 0; i < 5; i++) { float x = -W / 2 + 0.2f + i * (W - 0.4f) / 4; mb.Box(new Vector3(x, H * 0.5f, D * 0.4f + 0.005f), new Vector3(0.03f, H * 0.8f, 0.01f)); }
            mb.Set(S.Brass, Color.white);   // a brass kick rail, not a glowing strip
            mb.Box(new Vector3(0, 0.04f, D * 0.4f + 0.01f), new Vector3(W - 0.1f, 0.03f, 0.01f));
            // back shelf of bottles
            mb.Set(S.Glass, Color.white);
            for (int i = 0; i < 9; i++)
            {
                float x = -W / 2 + 0.25f + i * (W - 0.5f) / 8;
                var col = Color.HSVToRGB((i * 0.13f + c.Var * 0.2f) % 1f, 0.6f, 0.7f);
                mb.Set(S.Glass, col); mb.Cyl(new Vector3(x, H, -D * 0.25f), 0.035f, 0.22f + (i % 3) * 0.04f, 8, true, 0.02f);
            }
            c.View.AddLight(c.Rv, c.W2(new Vector3(0, H + 0.6f, 0.2f)), c.Pal.Warm, 1.2f, 3.2f, LightType.Point, false, 0.05f);
        }

        static void BarStool(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.Chrome, Color.white);
            mb.Cyl(new Vector3(0, 0, 0), 0.2f, 0.02f, 14); mb.Rod(new Vector3(0, 0.02f, 0), new Vector3(0, 0.7f, 0), 0.025f, 8, false);
            mb.Torus(new Vector3(0, 0.3f, 0), 0.16f, 0.01f, 14, 4);
            mb.Set(S.LeatherRed, Color.Lerp(c.Fabric, new Color(0.6f, 0.05f, 0.1f), 0.5f));
            mb.Cyl(new Vector3(0, 0.7f, 0), 0.19f, 0.08f, 16, true, 0.18f);
        }

        static void Gramophone(Ctx c, MeshBuilder mb)
        {
            // cabinet
            mb.Set(S.WoodDark, c.WoodC); mb.BevelBox(new Vector3(0, 0.4f, 0), new Vector3(0.55f, 0.8f, 0.5f), 0.02f);
            mb.Set(S.Gold, Color.white); mb.Box(new Vector3(0, 0.81f, 0), new Vector3(0.56f, 0.02f, 0.51f));
            // platter + record
            mb.Set(S.Obsidian, Color.white); mb.Cyl(new Vector3(0, 0.82f, 0), 0.17f, 0.012f, 24);
            mb.Set(S.GlossPaint, c.Pal.Accent2); mb.Cyl(new Vector3(0, 0.832f, 0), 0.05f, 0.003f, 16);
            // tone arm and a flower horn (brass, slightly too big, petals)
            mb.Set(S.Brass, Color.white);
            mb.Rod(new Vector3(0.2f, 0.84f, -0.15f), new Vector3(0.05f, 0.86f, 0.05f), 0.008f, 5, false);
            mb.Tube(new List<Vector3> { new Vector3(0.2f, 0.84f, -0.18f), new Vector3(0.2f, 0.95f, -0.2f), new Vector3(0.1f, 1.02f, -0.12f) }, 0.025f, 8);
            mb.Push(new Vector3(0.05f, 1.02f, -0.05f), Quaternion.Euler(-35, 20, 0), Vector3.one);
            mb.Lathe(new[] { new Vector2(0.03f, 0), new Vector2(0.06f, 0.08f), new Vector2(0.14f, 0.18f), new Vector2(0.26f, 0.26f), new Vector2(0.3f, 0.28f) }, 16);
            mb.Pop();
        }

        static void Jukebox(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodCherry, c.WoodC); mb.BevelBox(new Vector3(0, H * 0.35f, 0), new Vector3(W, H * 0.7f, D), 0.03f);
            // arched top as a half torus/lathe
            mb.Push(new Vector3(0, H * 0.7f, 0), Quaternion.Euler(90, 0, 0), new Vector3(1, 1, 1));
            mb.Set(S.Glow, Color.Lerp(c.Pal.Neon, new Color(1f, 0.62f, 0.3f), 0.65f), MansionMats.GlowData(1.4f, 0.1f, 0, c.Circuit));
            mb.Torus(Vector3.zero, W * 0.42f, 0.04f, 20, 6, 0, 180);
            mb.Pop();
            mb.Set(S.Chrome, Color.white); mb.Box(new Vector3(0, H * 0.72f, 0), new Vector3(W * 0.8f, 0.03f, D * 0.9f));
            mb.Set(S.Glass, new Color(0.9f, 0.8f, 1f)); mb.Box(new Vector3(0, H * 0.5f, D / 2 + 0.005f), new Vector3(W * 0.7f, H * 0.25f, 0.01f));
            mb.Set(S.Glow, Color.Lerp(c.Pal.Accent2, new Color(1f, 0.7f, 0.4f), 0.5f), MansionMats.GlowData(1.1f, 0.3f, 0, c.Circuit));
            for (int i = 0; i < 6; i++) mb.Box(new Vector3(-W * 0.3f + i * W * 0.12f, H * 0.2f, D / 2 + 0.01f), new Vector3(0.05f, H * 0.28f, 0.01f));
            c.View.AddLight(c.Rv, c.W2(new Vector3(0, H * 0.8f, D / 2 + 0.3f)), c.Pal.Neon, 1.1f, 3f, LightType.Point, false, 0.15f, neon: true);
        }

        static void Dartboard(Ctx c, MeshBuilder mb)
        {
            // mounted on the wall (back at -D/2), bull at 1.73 m
            mb.Set(S.WoodDark, c.WoodC); mb.Box(new Vector3(0, 1.73f, -c.D / 2 + 0.02f), new Vector3(0.7f, 0.7f, 0.03f));
            mb.Push(new Vector3(0, 1.73f, -c.D / 2 + 0.04f), Quaternion.Euler(-90, 0, 0), Vector3.one);
            Color[] ring = { new Color(0.08f, 0.08f, 0.08f), new Color(0.9f, 0.85f, 0.7f), new Color(0.75f, 0.05f, 0.1f), new Color(0.05f, 0.45f, 0.2f) };
            float[] rr = { 0.23f, 0.2f, 0.13f, 0.1f, 0.03f, 0.012f };
            for (int i = 0; i < rr.Length; i++) { mb.Set(S.Felt, ring[i % ring.Length]); mb.Cyl(new Vector3(0, i * 0.002f, 0), rr[i], 0.02f, 24); }
            mb.Pop();
            mb.Set(S.Steel, Color.white);
            for (int i = 0; i < 3; i++) mb.Rod(new Vector3(-0.05f + i * 0.06f, 1.7f + i * 0.04f, -c.D / 2 + 0.06f), new Vector3(-0.05f + i * 0.06f, 1.7f + i * 0.04f, -c.D / 2 + 0.16f), 0.004f, 4, false);
        }

        static void ChessTable(Ctx c, MeshBuilder mb)
        {
            SimpleTable(c, mb, 0.04f);
            float y = c.H + 0.005f, s = 0.05f;
            for (int i = 0; i < 8; i++) for (int j = 0; j < 8; j++)
                {
                    mb.Set(S.Marble, (i + j) % 2 == 0 ? new Color(0.92f, 0.9f, 0.88f) : new Color(0.12f, 0.08f, 0.1f));
                    mb.Box(new Vector3((i - 3.5f) * s, y, (j - 3.5f) * s), new Vector3(s, 0.006f, s));
                }
            // a few pieces mid-game
            var rnd = new System.Random(c.F.Id);
            for (int k = 0; k < 10; k++)
            {
                int i = rnd.Next(8), j = rnd.Next(8); bool white = k % 2 == 0;
                mb.Set(S.Porcelain, white ? new Color(0.95f, 0.93f, 0.9f) : new Color(0.1f, 0.07f, 0.12f));
                mb.Push(new Vector3((i - 3.5f) * s, y + 0.003f, (j - 3.5f) * s));
                mb.Lathe(new[] { new Vector2(0.016f, 0), new Vector2(0.014f, 0.01f), new Vector2(0.007f, 0.03f), new Vector2(0.011f, 0.045f), new Vector2(0, 0.055f) }, 8);
                mb.Pop();
            }
        }

        static void GlobeStand(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.WoodDark, c.WoodC);
            for (int k = 0; k < 3; k++) { float a = k / 3f * Mathf.PI * 2; mb.Rod(new Vector3(0, 0.55f, 0), new Vector3(Mathf.Cos(a) * 0.3f, 0, Mathf.Sin(a) * 0.3f), 0.02f, 6, false); }
            mb.Set(S.Brass, Color.white); mb.Torus(new Vector3(0, 0.55f, 0), 0.32f, 0.015f, 20, 5);
            mb.Push(new Vector3(0, 0.85f, 0), Quaternion.Euler(0, 0, 23.5f), Vector3.one);
            mb.Torus(Vector3.zero, 0.3f, 0.01f, 24, 4); mb.Pop();
            // an ocean-less, bruise-coloured globe: the continents are the mansion's floor plans
            mb.Set(S.Paper, Color.Lerp(new Color(0.8f, 0.72f, 0.55f), c.Pal.Accent2, 0.25f));
            mb.Sphere(new Vector3(0, 0.85f, 0), 0.27f, 20, 14);
        }

        static void DivingBoard(Ctx c, MeshBuilder mb)
        {
            float D = c.D;
            mb.Set(S.PaintedMetal, new Color(0.85f, 0.85f, 0.9f));
            mb.Box(new Vector3(0, 0.45f, -D / 2 + 0.3f), new Vector3(0.6f, 0.9f, 0.5f));
            mb.Set(S.GlossPaint, Color.Lerp(c.Pal.Accent2, Color.white, 0.3f));
            mb.Push(new Vector3(0, 0.95f, 0.1f), Quaternion.Euler(-2, 0, 0), Vector3.one);
            mb.BevelBox(Vector3.zero, new Vector3(0.5f, 0.06f, D + 0.4f), 0.02f); mb.Pop();
            mb.Set(S.Chrome, Color.white);
            foreach (float x in new[] { -0.3f, 0.3f }) { mb.Rod(new Vector3(x, 0, -D / 2), new Vector3(x, 1.4f, -D / 2 + 0.2f), 0.02f, 6, false); mb.Rod(new Vector3(x, 1.4f, -D / 2 + 0.2f), new Vector3(x, 1.2f, -D / 2 + 0.7f), 0.02f, 6, false); }
        }

        static void FilmProjector(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.Iron, new Color(0.2f, 0.2f, 0.22f));
            for (int k = 0; k < 3; k++) { float a = k / 3f * Mathf.PI * 2 + 0.3f; mb.Rod(new Vector3(0, 0.9f, 0), new Vector3(Mathf.Cos(a) * 0.35f, 0, Mathf.Sin(a) * 0.35f), 0.02f, 6, false); }
            mb.Set(S.PaintedMetal, new Color(0.15f, 0.14f, 0.18f)); mb.BevelBox(new Vector3(0, 1.05f, 0), new Vector3(0.3f, 0.3f, 0.5f), 0.03f);
            mb.Set(S.Chrome, Color.white); mb.Push(new Vector3(0, 1.05f, 0.25f), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Cyl(Vector3.zero, 0.06f, 0.15f, 12); mb.Pop();
            // two reels
            mb.Set(S.Steel, Color.white);
            foreach (float z in new[] { -0.2f, 0.15f }) { mb.Push(new Vector3(0, 1.32f, z), Quaternion.Euler(0, 0, 90), Vector3.one); mb.Cyl(new Vector3(0, -0.02f, 0), 0.16f, 0.04f, 20); mb.Pop(); }
            mb.Set(S.GlossPaint, new Color(0.35f, 0.34f, 0.3f)); mb.Disc(new Vector3(0, 1.05f, 0.401f), 0.05f, 12, true);
        }

        static void Telescope(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.WoodDark, c.WoodC);
            for (int k = 0; k < 3; k++) { float a = k / 3f * Mathf.PI * 2; mb.Rod(new Vector3(0, 1.0f, 0), new Vector3(Mathf.Cos(a) * 0.35f, 0, Mathf.Sin(a) * 0.35f), 0.02f, 6, false); }
            mb.Set(S.Brass, Color.white);
            mb.Push(new Vector3(0, 1.1f, 0), Quaternion.Euler(-60, 0, 0), Vector3.one);
            mb.Cyl(new Vector3(0, -0.35f, 0), 0.05f, 0.9f, 14, true, 0.07f);
            mb.Torus(new Vector3(0, 0.2f, 0), 0.065f, 0.01f, 14, 4); mb.Torus(new Vector3(0, -0.3f, 0), 0.055f, 0.01f, 14, 4);
            mb.Set(S.Glass, new Color(0.6f, 0.8f, 1f)); mb.Disc(new Vector3(0, 0.551f, 0), 0.065f, 14, true);
            mb.Pop();
        }

        static void Harp(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.Gold, Color.white);
            mb.Box(new Vector3(0, 0.05f, 0), new Vector3(0.5f, 0.1f, 0.4f));
            // column + curved neck + soundboard
            mb.Rod(new Vector3(-0.22f, 0.1f, 0), new Vector3(-0.22f, 1.75f, 0), 0.035f, 8, true);
            mb.Tube(new List<Vector3> { new Vector3(-0.22f, 1.75f, 0), new Vector3(0, 1.62f, 0), new Vector3(0.18f, 1.45f, 0), new Vector3(0.3f, 1.3f, 0) }, 0.035f, 8);
            mb.Set(S.WoodLight, c.WoodC);
            mb.Push(new Vector3(0.1f, 0.1f, 0), Quaternion.Euler(0, 0, 11), Vector3.one); mb.Box(new Vector3(0.0f, 0.62f, 0), new Vector3(0.12f, 1.25f, 0.18f)); mb.Pop();
            mb.Set(S.Steel, new Color(0.9f, 0.85f, 0.7f));
            for (int i = 0; i < 12; i++) { float t = i / 11f; var top = Vector3.Lerp(new Vector3(-0.18f, 1.7f, 0), new Vector3(0.28f, 1.33f, 0), t); var bot = new Vector3(Mathf.Lerp(0.02f, 0.3f, t), Mathf.Lerp(0.3f, 1.2f, t) * 0.6f + 0.15f, 0); mb.Rod(bot, top, 0.002f, 3, false); }
        }

        static void Lectern(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.WoodDark, c.WoodC);
            mb.Box(new Vector3(0, 0.04f, 0), new Vector3(0.6f, 0.08f, 0.5f));
            mb.BevelBox(new Vector3(0, 0.55f, 0), new Vector3(0.35f, 1.0f, 0.3f), 0.02f);
            mb.Push(new Vector3(0, 1.12f, 0), Quaternion.Euler(-25, 0, 0), Vector3.one); mb.BevelBox(Vector3.zero, new Vector3(0.65f, 0.05f, 0.45f), 0.01f); mb.Set(S.Paper, new Color(0.96f, 0.94f, 0.88f)); mb.Box(new Vector3(0, 0.03f, 0), new Vector3(0.45f, 0.02f, 0.32f)); mb.Pop();
            mb.Set(S.Gold, Color.white); mb.Box(new Vector3(0, 0.55f, 0.151f), new Vector3(0.14f, 0.14f, 0.005f));
        }

        static void SewingTable(Ctx c, MeshBuilder mb)
        {
            SimpleTable(c, mb, 0.04f);
            // black enamel machine with gold scroll-work
            mb.Set(S.GlossPaint, new Color(0.04f, 0.04f, 0.05f));
            mb.Box(new Vector3(-0.1f, c.H + 0.05f, 0), new Vector3(0.45f, 0.1f, 0.2f));
            mb.Box(new Vector3(-0.28f, c.H + 0.2f, 0), new Vector3(0.1f, 0.3f, 0.18f));
            mb.Box(new Vector3(-0.05f, c.H + 0.33f, 0), new Vector3(0.45f, 0.1f, 0.16f));
            mb.Box(new Vector3(0.14f, c.H + 0.23f, 0), new Vector3(0.08f, 0.16f, 0.12f));
            mb.Set(S.Gold, Color.white); mb.Torus(new Vector3(-0.3f, c.H + 0.28f, 0.1f), 0.06f, 0.006f, 12, 4);
            mb.Set(S.Cloth, c.Pal.Accent2); mb.Box(new Vector3(0.2f, c.H + 0.01f, 0.05f), new Vector3(0.3f, 0.01f, 0.25f));
        }

        static void BirdCage(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.Brass, Color.white);
            mb.Rod(new Vector3(0, 0, 0), new Vector3(0, 0.9f, 0), 0.025f, 8, false);
            mb.Cyl(new Vector3(0, 0, 0), 0.25f, 0.03f, 16);
            mb.Disc(new Vector3(0, 0.9f, 0), 0.28f, 18, true); mb.Disc(new Vector3(0, 0.9f, 0), 0.28f, 18, false);
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2; var b = new Vector3(Mathf.Cos(a) * 0.28f, 0.9f, Mathf.Sin(a) * 0.28f);
                mb.Tube(new List<Vector3> { b, b + new Vector3(0, 0.55f, 0), new Vector3(Mathf.Cos(a) * 0.12f, 1.72f, Mathf.Sin(a) * 0.12f), new Vector3(0, 1.8f, 0) }, 0.004f, 4);
            }
            mb.Torus(new Vector3(0, 1.85f, 0), 0.05f, 0.008f, 10, 4);
            // the "bird": a clockwork thing with one glowing eye
            mb.Set(S.Copper, Color.white); mb.Ellipsoid(new Vector3(0, 1.22f, 0), new Vector3(0.07f, 0.06f, 0.11f), 10, 8);
            mb.Sphere(new Vector3(0, 1.3f, 0.07f), 0.045f, 10, 8);
            mb.Set(S.Glow, c.Pal.Neon, MansionMats.GlowData(4f, 0.4f, 0, c.Circuit)); mb.Sphere(new Vector3(0.025f, 1.31f, 0.11f), 0.012f, 6, 4);
            mb.Set(S.Iron, Color.white); mb.Rod(new Vector3(-0.2f, 1.15f, 0), new Vector3(0.2f, 1.15f, 0), 0.006f, 4, false);
        }

        static void PunchingBag(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.Iron, new Color(0.15f, 0.15f, 0.15f));
            mb.Box(new Vector3(0, 0.03f, 0), new Vector3(0.6f, 0.06f, 0.6f)); mb.Rod(new Vector3(-0.28f, 0, -0.28f), new Vector3(-0.28f, 1.9f, -0.28f), 0.025f, 6, false);
            mb.Rod(new Vector3(-0.28f, 1.88f, -0.28f), new Vector3(0, 1.88f, 0), 0.02f, 6, false);
            mb.Rod(new Vector3(0, 1.88f, 0), new Vector3(0, 1.62f, 0), 0.006f, 4, false);
            mb.Set(S.LeatherRed, Color.Lerp(new Color(0.55f, 0.05f, 0.08f), c.Pal.Accent2, 0.2f));
            mb.Cyl(new Vector3(0, 0.55f, 0), 0.17f, 1.05f, 16, true);
            mb.Set(S.Leather, new Color(0.1f, 0.08f, 0.08f)); mb.Torus(new Vector3(0, 1.2f, 0), 0.172f, 0.01f, 16, 4); mb.Torus(new Vector3(0, 0.75f, 0), 0.172f, 0.01f, 16, 4);
        }

        static void DisplayCase(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodDark, c.WoodC); mb.BevelBox(new Vector3(0, 0.3f, 0), new Vector3(W, 0.6f, D), 0.02f); mb.Box(new Vector3(0, H - 0.04f, 0), new Vector3(W, 0.08f, D));
            mb.Set(S.Gold, Color.white);
            foreach (float x in new[] { -W / 2 + 0.02f, W / 2 - 0.02f }) foreach (float z in new[] { -D / 2 + 0.02f, D / 2 - 0.02f }) mb.Box(new Vector3(x, (H + 0.6f) / 2, z), new Vector3(0.03f, H - 0.6f, 0.03f));
            mb.Set(S.Glass, Color.white); mb.Box(new Vector3(0, (H + 0.6f) / 2, 0), new Vector3(W - 0.05f, H - 0.68f, D - 0.05f));
            mb.Set(S.Velvet, c.Fabric); mb.Box(new Vector3(0, 0.61f, 0), new Vector3(W - 0.08f, 0.01f, D - 0.08f)); mb.Box(new Vector3(0, 1.2f, 0), new Vector3(W - 0.08f, 0.02f, D - 0.08f));
            // curiosities: teeth in a jar, a porcelain hand, a doll's eye
            var rnd = new System.Random(c.F.Id * 31);
            for (int i = 0; i < 5; i++)
            {
                float x = -W / 2 + 0.2f + i * (W - 0.4f) / 4, y = i % 2 == 0 ? 0.62f : 1.22f;
                switch ((i + rnd.Next(3)) % 4)
                {
                    case 0: mb.Set(S.Glass, new Color(0.8f, 1f, 0.8f)); mb.Cyl(new Vector3(x, y, 0), 0.06f, 0.16f, 10); mb.Set(S.Bone, Color.white); mb.Sphere(new Vector3(x, y + 0.05f, 0), 0.03f, 6, 4); break;
                    case 1: mb.Set(S.Porcelain, new Color(0.96f, 0.92f, 0.9f)); mb.Ellipsoid(new Vector3(x, y + 0.04f, 0), new Vector3(0.04f, 0.04f, 0.1f), 8, 6); break;
                    case 2: mb.Set(S.Porcelain, Color.white); mb.Sphere(new Vector3(x, y + 0.05f, 0), 0.05f, 10, 8); mb.Set(S.GlossPaint, c.Pal.Accent2); mb.Disc(new Vector3(x, y + 0.05f, 0.05f), 0.02f, 8, true); break;
                    default: mb.Set(S.Gold, Color.white); mb.Cyl(new Vector3(x, y, 0), 0.04f, 0.2f, 8, true, 0.02f); break;
                }
            }
        }

        static void CardCatalog(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodDark, c.WoodC); mb.BevelBox(new Vector3(0, H / 2, 0), new Vector3(W, H, D), 0.02f);
            int cols = 6, rows = 5;
            for (int i = 0; i < cols; i++) for (int j = 0; j < rows; j++)
                {
                    float x = -W / 2 + (i + 0.5f) * W / cols, y = 0.15f + (j + 0.5f) * (H - 0.2f) / rows;
                    mb.Set(S.WoodLight, c.WoodC * 1.1f); mb.Box(new Vector3(x, y, D / 2 + 0.005f), new Vector3(W / cols - 0.02f, (H - 0.2f) / rows - 0.02f, 0.01f));
                    mb.Set(S.Brass, Color.white); mb.Box(new Vector3(x, y, D / 2 + 0.013f), new Vector3(0.05f, 0.012f, 0.006f));
                    mb.Set(S.Paper, new Color(0.95f, 0.93f, 0.85f)); mb.Box(new Vector3(x, y + 0.03f, D / 2 + 0.012f), new Vector3(0.06f, 0.025f, 0.002f));
                }
        }
    }
}
