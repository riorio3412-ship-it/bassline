using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    internal static partial class FurnitureFactory
    {
        // ------------------------------------------------------------------ helpers for sub-objects
        static Transform Child(Ctx c, string name, Vector3 local, MeshBuilder mb, ShadowCastingMode sh = ShadowCastingMode.On)
        {
            var go = new GameObject(name); go.transform.SetParent(c.T, false); go.transform.localPosition = local;
            if (mb != null && !mb.Empty)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name, out var slots);
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = MansionMats.Materials(slots); mr.shadowCastingMode = sh;
            }
            return go.transform;
        }

        // ------------------------------------------------------------------ infirmary / laundry / workshop
        static void InfirmaryBed(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D;
            mb.Set(S.PaintedMetal, new Color(0.85f, 0.88f, 0.86f));
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Rod(new Vector3(x * (W / 2 - 0.03f), 0.08f, z * (D / 2 - 0.03f)), new Vector3(x * (W / 2 - 0.03f), z < 0 ? 1.0f : 0.8f, z * (D / 2 - 0.03f)), 0.018f, 6, true);
            mb.Box(new Vector3(0, 0.45f, 0), new Vector3(W, 0.05f, D));
            foreach (float z in new[] { -1f, 1f }) { float top = z < 0 ? 1.0f : 0.8f; mb.Rod(new Vector3(-W / 2, top, z * (D / 2 - 0.03f)), new Vector3(W / 2, top, z * (D / 2 - 0.03f)), 0.018f, 6, true); for (int i = 1; i < 5; i++) mb.Rod(new Vector3(-W / 2 + i * W / 5, 0.5f, z * (D / 2 - 0.03f)), new Vector3(-W / 2 + i * W / 5, top, z * (D / 2 - 0.03f)), 0.008f, 4, false); }
            mb.Set(S.Rubber, Color.white);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Sphere(new Vector3(x * (W / 2 - 0.03f), 0.05f, z * (D / 2 - 0.03f)), 0.045f, 6, 4);
            mb.Set(S.Linen, new Color(0.92f, 0.94f, 0.95f));
            mb.BevelBox(new Vector3(0, 0.55f, 0.05f), new Vector3(W - 0.06f, 0.14f, D - 0.12f), 0.04f);
            mb.BevelBox(new Vector3(0, 0.66f, -D / 2 + 0.22f), new Vector3(W * 0.7f, 0.1f, 0.3f), 0.04f);
            // a stain on the sheet (subtle)
            mb.Set(S.Linen, new Color(0.55f, 0.3f, 0.3f));
            mb.Box(new Vector3(0.1f, 0.622f, 0.3f), new Vector3(0.18f, 0.004f, 0.12f), MeshBuilder.Faces.PY);
            // IV stand
            mb.Set(S.Chrome, Color.white);
            mb.Rod(new Vector3(W / 2 + 0.25f, 0, -D / 2 + 0.3f), new Vector3(W / 2 + 0.25f, 1.8f, -D / 2 + 0.3f), 0.01f, 6, false);
            mb.Set(S.Glass, Color.white); mb.Ellipsoid(new Vector3(W / 2 + 0.25f, 1.65f, -D / 2 + 0.3f), new Vector3(0.06f, 0.1f, 0.03f), 8, 6);
            DecorBox(c, new Vector3(-W / 2 - 0.3f, 0f, -D / 2), new Vector3(-W / 2 - 0.2f, 1.7f, -D / 2 + 1.86f), "PrivacyScreen");
            // privacy screen (folding, white cloth)
            mb.Set(S.PaintedMetal, new Color(0.85f, 0.88f, 0.86f));
            for (int i = 0; i < 3; i++)
            {
                float x = -W / 2 - 0.25f; float z0 = -D / 2 + i * 0.62f, z1 = z0 + 0.6f;
                mb.Set(S.Linen, new Color(0.9f, 0.93f, 0.95f));
                mb.QuadAuto(new Vector3(x, 0.15f, z0), new Vector3(x, 1.7f, z0), new Vector3(x, 1.7f, z1), new Vector3(x, 0.15f, z1), Vector3.right);
                mb.QuadAuto(new Vector3(x, 0.15f, z0), new Vector3(x, 1.7f, z0), new Vector3(x, 1.7f, z1), new Vector3(x, 0.15f, z1), Vector3.left);
            }
        }

        static void MedCabinet(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.PaintedMetal, new Color(0.9f, 0.92f, 0.9f));
            mb.Box(new Vector3(0, H / 2, -D / 2 + 0.01f), new Vector3(W, H, 0.02f));
            foreach (float x in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.015f), H / 2, 0), new Vector3(0.03f, H, D));
            mb.Box(new Vector3(0, H - 0.015f, 0), new Vector3(W, 0.03f, D)); mb.Box(new Vector3(0, 0.08f, 0), new Vector3(W, 0.16f, D));
            var rnd = c.Rng;
            for (int s = 0; s < 4; s++)
            {
                float y = 0.18f + s * (H - 0.3f) / 4;
                mb.Set(S.Glass, Color.white); mb.Box(new Vector3(0, y, 0), new Vector3(W - 0.06f, 0.012f, D - 0.04f));
                for (int k = 0; k < 7; k++)
                {
                    var p = new Vector3(-W / 2 + 0.1f + k * (W - 0.2f) / 6, y + 0.006f, ((float)rnd.NextDouble() - 0.5f) * 0.15f);
                    Color bc = k % 3 == 0 ? new Color(0.45f, 0.25f, 0.1f) : k % 3 == 1 ? new Color(0.1f, 0.25f, 0.4f) : new Color(0.9f, 0.9f, 0.88f);
                    mb.Set(k % 3 == 2 ? S.Porcelain : S.Glass, bc);
                    float h = 0.08f + (float)rnd.NextDouble() * 0.1f;
                    mb.Push(p, 0); mb.Lathe(new[] { new Vector2(0.03f, 0), new Vector2(0.032f, h * 0.8f), new Vector2(0.012f, h), new Vector2(0.012f, h + 0.02f) }, 8); mb.Pop();
                }
            }
            // glass doors + red cross
            mb.Set(S.Glass, Color.white);
            mb.Box(new Vector3(0, H / 2 + 0.08f, D / 2), new Vector3(W - 0.04f, H - 0.2f, 0.01f));
            mb.Set(S.Glow, new Color(1f, 0.08f, 0.1f), MansionMats.GlowData(1.5f, 0.05f, 0, c.Circuit));
            mb.Box(new Vector3(0, H + 0.12f, 0), new Vector3(0.2f, 0.06f, 0.04f)); mb.Box(new Vector3(0, H + 0.12f, 0), new Vector3(0.06f, 0.2f, 0.04f));
        }

        static void Washer(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.GlossPaint, Color.Lerp(new Color(0.9f, 0.9f, 0.87f), c.Pal.Accent2, 0.12f));
            mb.BevelBox(new Vector3(0, H / 2, 0), new Vector3(W, H, D), 0.04f);
            mb.Set(S.Chrome, Color.white);
            mb.Torus(new Vector3(0, 0.42f, D / 2 + 0.01f), 0.2f, 0.03f, 20, 6);
            mb.Push(new Vector3(0, 0.42f, D / 2), Quaternion.Euler(90, 0, 0), Vector3.one);
            mb.Set(S.Glass, Color.white); mb.Disc(Vector3.zero + Vector3.up * 0.02f, 0.19f, 20, true);
            mb.Set(S.Cloth, c.Pal.Fabric); mb.Disc(Vector3.zero - Vector3.up * 0.05f, 0.17f, 16, true);
            mb.Pop();
            mb.Set(S.Chrome, Color.white);
            for (int i = 0; i < 3; i++) mb.Cyl(new Vector3(-0.2f + i * 0.12f, H - 0.08f, D / 2 - 0.02f), 0.025f, 0.03f, 8);
            mb.Set(S.Glow, new Color(0.2f, 1f, 0.4f), MansionMats.GlowData(2f, 0.4f, 0, c.Circuit)); mb.Sphere(new Vector3(0.25f, H - 0.07f, D / 2), 0.012f, 6, 4);
        }

        static void DryRack(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodLight, Color.white);
            foreach (float x in new[] { -1f, 1f }) { mb.Rod(new Vector3(x * W / 2, 0, -D / 2), new Vector3(x * W / 2, H, 0), 0.015f, 5, true); mb.Rod(new Vector3(x * W / 2, 0, D / 2), new Vector3(x * W / 2, H, 0), 0.015f, 5, true); }
            for (int i = 0; i < 3; i++) mb.Rod(new Vector3(-W / 2, H - i * 0.35f, 0), new Vector3(W / 2, H - i * 0.35f, 0), 0.008f, 4, false);
            // hanging sheets (one with a pinkish stain)
            for (int i = 0; i < 2; i++)
            {
                mb.Set(S.Linen, i == 0 ? new Color(0.93f, 0.93f, 0.9f) : Color.Lerp(new Color(0.93f, 0.93f, 0.9f), c.Pal.Fabric, 0.4f));
                float x0 = -W / 2 + 0.05f + i * W / 2, x1 = x0 + W / 2 - 0.1f;
                var pts = new List<Vector3>();
                mb.QuadAuto(new Vector3(x0, H, 0.01f), new Vector3(x0, H - 1.0f, 0.02f + i * 0.02f), new Vector3(x1, H - 1.05f, 0.02f), new Vector3(x1, H, 0.01f), Vector3.forward);
                mb.QuadAuto(new Vector3(x0, H, -0.01f), new Vector3(x0, H - 1.0f, -0.02f), new Vector3(x1, H - 1.05f, -0.02f), new Vector3(x1, H, -0.01f), Vector3.back);
            }
        }

        static void Workbench(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodWorn, Color.white);
            mb.Box(new Vector3(0, H - 0.04f, 0), new Vector3(W, 0.08f, D));
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.08f), (H - 0.08f) / 2, z * (D / 2 - 0.08f)), new Vector3(0.09f, H - 0.08f, 0.09f));
            mb.Box(new Vector3(0, 0.2f, 0), new Vector3(W - 0.2f, 0.04f, D - 0.2f));
            // vise, shavings, jars of screws, a half-built wooden bird
            mb.Set(S.Iron, new Color(0.2f, 0.25f, 0.3f));
            mb.Box(new Vector3(W / 2 - 0.25f, H + 0.08f, D / 2 - 0.08f), new Vector3(0.2f, 0.16f, 0.12f));
            mb.Rod(new Vector3(W / 2 - 0.25f, H + 0.05f, D / 2 + 0.02f), new Vector3(W / 2 - 0.25f, H + 0.05f, D / 2 + 0.2f), 0.012f, 5, true);
            mb.Set(S.WoodLight, Color.white);
            for (int i = 0; i < 12; i++) { var rp = new Vector3(((float)c.Rng.NextDouble() - 0.5f) * W * 0.8f, H + 0.005f, ((float)c.Rng.NextDouble() - 0.5f) * D * 0.6f); mb.Torus(rp, 0.02f, 0.004f, 6, 3); }
            mb.Set(S.Glass, Color.white); mb.Cyl(new Vector3(-W / 2 + 0.2f, H, -D / 4), 0.05f, 0.12f, 8);
            mb.Set(S.WoodLight, Color.white);
            mb.Ellipsoid(new Vector3(-0.2f, H + 0.08f, 0), new Vector3(0.12f, 0.07f, 0.06f), 8, 6);
            mb.Ellipsoid(new Vector3(-0.08f, H + 0.13f, 0), new Vector3(0.05f, 0.05f, 0.05f), 8, 6);
            Lamp(c, mb, new Vector3(-W / 2 + 0.2f, H, D / 4), 0.22f, true);
        }

        static void ToolWall(Ctx c, MeshBuilder mb)
        {
            float W = c.W, H = c.H;
            mb.Set(S.WoodLight, new Color(0.8f, 0.7f, 0.55f));
            mb.Box(new Vector3(0, 0.3f + H / 2, -0.05f), new Vector3(W, H * 0.8f, 0.02f));
            mb.Set(S.Obsidian, Color.white);
            for (float x = -W / 2 + 0.1f; x < W / 2; x += 0.1f) for (float y = 0.5f; y < H; y += 0.1f) mb.Box(new Vector3(x, y, -0.035f), new Vector3(0.01f, 0.01f, 0.002f));
            var rnd = c.Rng;
            for (int i = 0; i < 14; i++)
            {
                float x = -W / 2 + 0.2f + i * (W - 0.4f) / 13; float y = 0.9f + (i % 3) * 0.4f;
                mb.Set(i % 2 == 0 ? S.Steel : S.Iron, Color.white);
                if (i % 4 == 0) { mb.Box(new Vector3(x, y, 0), new Vector3(0.03f, 0.25f, 0.02f)); mb.Set(S.WoodLight, Color.white); mb.Box(new Vector3(x, y - 0.17f, 0), new Vector3(0.035f, 0.1f, 0.03f)); }
                else if (i % 4 == 1) { mb.Box(new Vector3(x, y, 0), new Vector3(0.12f, 0.035f, 0.02f)); mb.Box(new Vector3(x, y - 0.12f, 0), new Vector3(0.025f, 0.22f, 0.02f)); }
                else if (i % 4 == 2) { mb.Torus(new Vector3(x, y, 0), 0.06f, 0.008f, 10, 4); }
                else { mb.Box(new Vector3(x, y, 0), new Vector3(0.02f, 0.3f, 0.01f)); mb.Set(S.GlossPaint, new Color(0.6f, 0.1f, 0.05f)); mb.Box(new Vector3(x, y - 0.18f, 0), new Vector3(0.035f, 0.08f, 0.025f)); }
            }
            // painted outlines of missing tools (one outline is empty: a missing hammer)
            mb.Set(S.Paper, new Color(0.2f, 0.2f, 0.2f));
            mb.Box(new Vector3(0.15f, 1.5f, -0.038f), new Vector3(0.14f, 0.04f, 0.002f)); mb.Box(new Vector3(0.15f, 1.38f, -0.038f), new Vector3(0.03f, 0.24f, 0.002f));
        }

        static void Crates(Ctx c, MeshBuilder mb)
        {
            var m1 = Models.Get("wooden_crate_01"); var m2 = Models.Get("wooden_crate_02");
            if (m1 != null)
            {
                var a = Models.Place(m1, c.T, Vector3.zero, 0, new Vector3(c.W, 0.6f, c.D * 0.6f), null); a.transform.localPosition = new Vector3(0, 0, -c.D * 0.18f); a.transform.localRotation = Quaternion.identity;
                var b = Models.Place(m1, c.T, Vector3.zero, 0, new Vector3(c.W * 0.8f, 0.5f, c.D * 0.5f), null); b.transform.localPosition = new Vector3(0.05f, 0.42f, -c.D * 0.18f); b.transform.localRotation = Quaternion.Euler(0, 8, 0);
                if (m2 != null) { var d = Models.Place(m2, c.T, Vector3.zero, 0, new Vector3(c.W * 0.5f, 0.5f, c.D * 0.45f), null); d.transform.localPosition = new Vector3(-0.2f, 0, c.D * 0.28f); d.transform.localRotation = Quaternion.Euler(0, 90, 0); }
                return;
            }
            mb.Set(S.WoodWorn, Color.white);
            mb.Box(new Vector3(0, 0.3f, 0), new Vector3(c.W, 0.6f, c.D));
            mb.Box(new Vector3(0.05f, 0.85f, 0.05f), new Vector3(c.W * 0.7f, 0.5f, c.D * 0.7f));
        }

        static void Shelves(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            bool metal = c.Rv.Room.Floor < 0 || c.Rv.Room.Type == RoomType.ButlerRoom;
            mb.Set(metal ? S.PaintedMetal : S.WoodDark, metal ? new Color(0.4f, 0.45f, 0.42f) : c.WoodC);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.02f), H / 2, z * (D / 2 - 0.02f)), new Vector3(0.04f, H, 0.04f));
            var rnd = c.Rng;
            for (int s = 0; s < 5; s++)
            {
                float y = 0.1f + s * (H - 0.2f) / 4;
                mb.Set(metal ? S.PaintedMetal : S.WoodDark, metal ? new Color(0.4f, 0.45f, 0.42f) : c.WoodC);
                mb.Box(new Vector3(0, y, 0), new Vector3(W, 0.025f, D));
                float x = -W / 2 + 0.05f;
                while (x < W / 2 - 0.2f)
                {
                    int kind = rnd.Next(5); float w = 0.2f + (float)rnd.NextDouble() * 0.3f;
                    if (x + w > W / 2 - 0.05f) break;
                    float h = 0.12f + (float)rnd.NextDouble() * 0.25f;
                    if (kind <= 1) { mb.Set(S.Paper, new Color(0.7f, 0.58f, 0.4f)); mb.Box(new Vector3(x + w / 2, y + 0.013f + h / 2, 0), new Vector3(w, h, D * 0.8f)); }
                    else if (kind == 2) { for (int j = 0; j < 3; j++) { mb.Set(S.Glass, Color.white); mb.Push(new Vector3(x + 0.05f + j * 0.08f, y + 0.013f, 0), 0); mb.Lathe(new[] { new Vector2(0.035f, 0), new Vector2(0.035f, 0.14f), new Vector2(0.02f, 0.16f) }, 8); mb.Pop(); mb.Set(S.Cloth, c.Pal.Accent2 * 0.6f); mb.Cyl(new Vector3(x + 0.05f + j * 0.08f, y + 0.02f, 0), 0.03f, 0.09f, 6); } }
                    else if (kind == 3) { mb.Set(S.Linen, Lighter(c.Pal.Fabric, 0.5f)); for (int j = 0; j < 4; j++) mb.BevelBox(new Vector3(x + w / 2, y + 0.03f + j * 0.05f, 0), new Vector3(w, 0.045f, D * 0.7f), 0.015f); }
                    else { mb.Set(S.Iron, Color.white); mb.Cyl(new Vector3(x + w / 2, y + 0.013f, 0), Mathf.Min(w, D) * 0.4f, h, 10); }
                    x += w + 0.04f;
                }
            }
        }

        // ------------------------------------------------------------------ games / pool
        static void GameTable(Ctx c, MeshBuilder mb)
        {
            SimpleTable(c, mb, 0.05f);
            mb.Set(S.Felt, Color.Lerp(new Color(0.05f, 0.3f, 0.15f), c.Pal.Neon, 0.1f));
            mb.Box(new Vector3(0, c.H + 0.004f, 0), new Vector3(c.W - 0.12f, 0.008f, c.D - 0.12f));
            var chess = Models.Get("chess_set");
            if (chess != null) { var g = Models.Place(chess, c.T, Vector3.zero, 0, new Vector3(0.5f, 0, 0.5f), null); g.transform.localPosition = new Vector3(-0.3f, c.H + 0.008f, 0); g.transform.localRotation = Quaternion.Euler(0, 17, 0); }
            // cards fanned out
            mb.Set(S.Paper, Color.white);
            for (int i = 0; i < 5; i++) { mb.Push(new Vector3(0.45f, c.H + 0.01f + i * 0.001f, 0), i * 12f - 24f); mb.Box(new Vector3(0, 0, 0.05f), new Vector3(0.06f, 0.002f, 0.09f)); mb.Pop(); }
        }

        static void Arcade(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            // a mutoscope / fortune-machine cabinet: lacquered dark wood with brass, a peep-show glass up top
            mb.Set(S.WoodCherry, c.WoodC * 0.62f);
            var side = new List<Vector2> { new Vector2(-D / 2, 0), new Vector2(D / 2, 0), new Vector2(D / 2, 0.9f), new Vector2(D / 2 - 0.25f, 1.05f), new Vector2(D / 2 - 0.1f, 1.55f), new Vector2(D / 2 - 0.05f, H), new Vector2(-D / 2, H) };
            mb.Box(new Vector3(0, H / 2, -0.05f), new Vector3(W, H, D - 0.1f));
            // control deck
            mb.Set(S.Obsidian, Color.white); mb.Push(new Vector3(0, 0.95f, D / 2 - 0.1f), Quaternion.Euler(-15, 0, 0), Vector3.one); mb.Box(Vector3.zero, new Vector3(W - 0.05f, 0.05f, 0.3f)); mb.Pop();
            for (int i = 0; i < 4; i++) { mb.Set(S.Brass, new Color(0.62f, 0.5f, 0.34f)); mb.Cyl(new Vector3(-0.15f + i * 0.1f, 0.98f, D / 2 - 0.08f), 0.022f, 0.02f, 8); }
            mb.Set(S.Obsidian, Color.white); mb.Rod(new Vector3(-0.28f, 0.97f, D / 2 - 0.1f), new Vector3(-0.28f, 1.07f, D / 2 - 0.08f), 0.008f, 5, false); mb.Sphere(new Vector3(-0.28f, 1.08f, D / 2 - 0.08f), 0.022f, 8, 5);
            // screen (static / eye / bars) + marquee neon
            mb.Set(S.Screen, Color.white, new Vector4(c.Var % 3 == 0 ? 1 : c.Var % 3 == 1 ? 2 : 0, c.F.Id, 1, c.Circuit + 10));
            mb.Push(new Vector3(0, 1.3f, D / 2 - 0.17f), Quaternion.Euler(-12, 0, 0), Vector3.one);
            mb.FaceUV(new Vector3(W * 0.4f, -0.2f, 0), new Vector3(-W * 0.8f, 0, 0), new Vector3(0, 0.42f, 0), new Rect(1, 0, -1, 1));
            mb.Pop();
            mb.Set(S.Glow, Color.Lerp(c.Pal.Neon, c.Pal.Warm, 0.55f), MansionMats.GlowData(1.3f, 0.25f, 0, c.Circuit));
            mb.Box(new Vector3(0, H - 0.12f, D / 2 - 0.12f), new Vector3(W - 0.06f, 0.16f, 0.02f));
            c.View.AddLight(c.Rv, c.W2(new Vector3(0, 1.4f, D / 2 + 0.4f)), Color.Lerp(c.Pal.Neon, c.Pal.Warm, 0.6f), 0.9f, 3f, LightType.Point, false, 0.2f, neon: true);
        }

        static void PoolTable(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodCherry, Darker(c.WoodC, 0.1f));
            mb.BevelBox(new Vector3(0, H - 0.15f, 0), new Vector3(W, 0.2f, D), 0.03f);
            foreach (float x in new[] { -1f, 0f, 1f }) foreach (float z in new[] { -1f, 1f }) { TurnedLeg(mb, new Vector3(x * (W / 2 - 0.15f), 0, z * (D / 2 - 0.15f)), H - 0.25f, 0.07f); }
            mb.Set(S.Felt, Color.Lerp(new Color(0.05f, 0.32f, 0.18f), c.Pal.Neon, 0.25f));
            mb.Box(new Vector3(0, H - 0.04f, 0), new Vector3(W - 0.22f, 0.02f, D - 0.22f), MeshBuilder.Faces.PY);
            foreach (float z in new[] { -1f, 1f }) mb.Box(new Vector3(0, H - 0.02f, z * (D / 2 - 0.13f)), new Vector3(W - 0.26f, 0.05f, 0.04f));
            foreach (float x in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.13f), H - 0.02f, 0), new Vector3(0.04f, 0.05f, D - 0.26f));
            mb.Set(S.Obsidian, Color.white);
            foreach (float x in new[] { -1f, 0f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Disc(new Vector3(x * (W / 2 - 0.14f), H - 0.028f, z * (D / 2 - 0.14f)), 0.055f, 10, true);
            // balls: one of them is an eye
            var rnd = c.Rng;
            for (int i = 0; i < 9; i++)
            {
                var p = new Vector3(((float)rnd.NextDouble() - 0.5f) * (W - 0.5f), H - 0.0f, ((float)rnd.NextDouble() - 0.5f) * (D - 0.5f));
                mb.Set(S.GlossPaint, i == 0 ? Color.white : Color.HSVToRGB(i / 9f, 0.8f, 0.8f));
                mb.Sphere(p, 0.03f, 10, 6);
                if (i == 0) { mb.Set(S.Obsidian, Color.white); mb.Sphere(p + new Vector3(0, 0.012f, 0.022f), 0.012f, 6, 4); }
            }
            // low green-shaded lamp over the table
            mb.Set(S.Brass, Color.white);
            float ceil = c.Rv.CeilY - c.Rv.FloorY;
            mb.Rod(new Vector3(0, ceil, 0), new Vector3(0, H + 0.9f, 0), 0.01f, 5, false);
            mb.Box(new Vector3(0, H + 0.9f, 0), new Vector3(W * 0.6f, 0.03f, 0.05f));
            foreach (float x in new[] { -W * 0.25f, 0, W * 0.25f })
            {
                mb.Set(S.Glow, new Color(0.1f, 0.6f, 0.3f), MansionMats.GlowData(1.2f, 0.05f, 0, c.Circuit));
                mb.Push(new Vector3(x, H + 0.72f, 0), 0); mb.Lathe(new[] { new Vector2(0.18f, 0), new Vector2(0.06f, 0.16f) }, 12); mb.Pop();
            }
            c.View.AddLight(c.Rv, c.W2(new Vector3(0, H + 0.6f, 0)), new Color(1f, 0.92f, 0.75f), 4f, 3.5f, LightType.Point, true, 0.02f);
        }

        static void PoolWater(Ctx c, MeshBuilder mb)
        {
            // basin walls / floor from 0 to -depth, tiles; water surface a little below the floor
            float W = c.W, D = c.D, depth = Mathf.Abs(c.H) > 0.1f ? Mathf.Abs(c.H) : 1.6f;
            mb.Set(S.PoolTile, Color.Lerp(Color.white, c.Pal.Accent2, 0.2f));
            mb.Quad(new Vector3(-W / 2, -depth, -D / 2), new Vector3(-W / 2, -depth, D / 2), new Vector3(W / 2, -depth, D / 2), new Vector3(W / 2, -depth, -D / 2));
            mb.QuadAuto(new Vector3(-W / 2, -depth, -D / 2), new Vector3(-W / 2, 0, -D / 2), new Vector3(W / 2, 0, -D / 2), new Vector3(W / 2, -depth, -D / 2), Vector3.forward);
            mb.QuadAuto(new Vector3(-W / 2, -depth, D / 2), new Vector3(-W / 2, 0, D / 2), new Vector3(W / 2, 0, D / 2), new Vector3(W / 2, -depth, D / 2), Vector3.back);
            mb.QuadAuto(new Vector3(-W / 2, -depth, -D / 2), new Vector3(-W / 2, 0, -D / 2), new Vector3(-W / 2, 0, D / 2), new Vector3(-W / 2, -depth, D / 2), Vector3.right);
            mb.QuadAuto(new Vector3(W / 2, -depth, -D / 2), new Vector3(W / 2, 0, -D / 2), new Vector3(W / 2, 0, D / 2), new Vector3(W / 2, -depth, D / 2), Vector3.left);
            // lane lines (dark tile) on the floor
            mb.Set(S.Obsidian, Color.white);
            int lanes = Mathf.Max(2, (int)(D / 1.6f));
            for (int i = 1; i < lanes; i++) mb.Box(new Vector3(0, -depth + 0.005f, -D / 2 + i * D / lanes), new Vector3(W - 1.2f, 0.01f, 0.18f), MeshBuilder.Faces.PY);
            // marble coping around the edge
            mb.Set(S.Marble, Color.Lerp(Color.white, c.Pal.FloorA, 0.3f));
            mb.Box(new Vector3(0, 0.02f, -D / 2 - 0.15f), new Vector3(W + 0.6f, 0.04f, 0.3f)); mb.Box(new Vector3(0, 0.02f, D / 2 + 0.15f), new Vector3(W + 0.6f, 0.04f, 0.3f));
            mb.Box(new Vector3(-W / 2 - 0.15f, 0.02f, 0), new Vector3(0.3f, 0.04f, D)); mb.Box(new Vector3(W / 2 + 0.15f, 0.02f, 0), new Vector3(0.3f, 0.04f, D));
            // ladder
            mb.Set(S.Chrome, Color.white);
            foreach (float x in new[] { W / 2 - 1.3f, W / 2 - 0.8f })
                mb.Tube(new List<Vector3> { new Vector3(x, -1.0f, D / 2 - 0.12f), new Vector3(x, 0.5f, D / 2 - 0.12f), new Vector3(x, 0.8f, D / 2 + 0.1f), new Vector3(x, 0.02f, D / 2 + 0.3f) }, 0.02f, 6);
            for (int i = 0; i < 4; i++) mb.Rod(new Vector3(W / 2 - 1.3f, -0.9f + i * 0.3f, D / 2 - 0.12f), new Vector3(W / 2 - 0.8f, -0.9f + i * 0.3f, D / 2 - 0.12f), 0.015f, 5, false);
            // underwater lights (cyan glow + light) + caustics on the basin floor
            mb.Set(S.Glow, c.Pal.Accent2, MansionMats.GlowData(3f, 0.05f, 0, c.Circuit));
            for (int i = 0; i < 3; i++)
            {
                float x = -W / 2 + (i + 0.5f) * W / 3;
                foreach (float z in new[] { -1f, 1f }) mb.Box(new Vector3(x, -0.6f, z * (D / 2 - 0.01f)), new Vector3(0.3f, 0.2f, 0.02f));
                c.View.AddLight(c.Rv, c.W2(new Vector3(x, -0.8f, 0)), Color.Lerp(new Color(0.2f, 0.9f, 1f), c.Pal.Accent2, 0.3f), 5f, 5.5f, LightType.Point, false, 0.03f);
            }
            var caus = new MeshBuilder(); caus.Set(S.Caustic, new Color(1, 1, 1, 0.6f));
            caus.Quad(new Vector3(-W / 2, -depth + 0.02f, -D / 2), new Vector3(-W / 2, -depth + 0.02f, D / 2), new Vector3(W / 2, -depth + 0.02f, D / 2), new Vector3(W / 2, -depth + 0.02f, -D / 2));
            Child(c, "Caustics", Vector3.zero, caus, ShadowCastingMode.Off);
            // the water surface itself (transparent, reflective)
            var water = new MeshBuilder(); water.Set(S.Water, Color.white);
            int nx = Mathf.Max(2, (int)W), nz = Mathf.Max(2, (int)D);
            for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
                {
                    float x0 = -W / 2 + i * W / nx, x1 = x0 + W / nx, z0 = -D / 2 + j * D / nz, z1 = z0 + D / nz;
                    water.Quad(new Vector3(x0, -0.12f, z0), new Vector3(x0, -0.12f, z1), new Vector3(x1, -0.12f, z1), new Vector3(x1, -0.12f, z0));
                }
            var wt = Child(c, "Water", Vector3.zero, water, ShadowCastingMode.Off);
            var trig = wt.gameObject.AddComponent<BoxCollider>(); trig.isTrigger = true; trig.center = new Vector3(0, -depth / 2, 0); trig.size = new Vector3(W, depth, D);
            // basin colliders
            var col = new GameObject("BasinColliders"); col.transform.SetParent(c.T, false);
            var f0 = col.AddComponent<BoxCollider>(); f0.center = new Vector3(0, -depth - 0.1f, 0); f0.size = new Vector3(W, 0.2f, D);
            foreach (float z in new[] { -1f, 1f }) { var b = col.AddComponent<BoxCollider>(); b.center = new Vector3(0, -depth / 2, z * (D / 2 + 0.1f)); b.size = new Vector3(W + 0.4f, depth, 0.2f); }
            foreach (float x in new[] { -1f, 1f }) { var b = col.AddComponent<BoxCollider>(); b.center = new Vector3(x * (W / 2 + 0.1f), -depth / 2, 0); b.size = new Vector3(0.2f, depth, D); }
            // floating things: a rubber duck, a white cloth drifting
            var duck = Models.Get("rubber_duck_toy");
            if (duck != null) { var g = Models.Place(duck, c.T, Vector3.zero, 0, new Vector3(0.25f, 0.25f, 0.25f), null); g.transform.localPosition = new Vector3(W * 0.2f, -0.2f, -D * 0.15f); g.transform.localRotation = Quaternion.Euler(0, 140, 0); }
            mb.Set(S.Linen, new Color(0.9f, 0.9f, 0.92f));
            mb.Quad(new Vector3(-0.8f, -0.115f, 0.3f), new Vector3(-0.9f, -0.115f, 1.0f), new Vector3(-0.1f, -0.115f, 1.1f), new Vector3(0.0f, -0.115f, 0.2f));
        }

        static void Lounger(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D;
            mb.Set(S.WoodLight, Color.white);
            mb.Box(new Vector3(0, 0.3f, 0.2f), new Vector3(W, 0.05f, D - 0.4f));
            mb.Push(new Vector3(0, 0.3f, -D / 2 + 0.4f), Quaternion.Euler(-35, 0, 0), Vector3.one); mb.Box(new Vector3(0, 0, -0.3f), new Vector3(W, 0.05f, 0.65f)); mb.Pop();
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.04f), 0.15f, z * (D / 2 - 0.15f)), new Vector3(0.04f, 0.3f, 0.04f));
            mb.Set(S.Linen, Color.Lerp(Color.white, c.Pal.Accent2, 0.3f));
            mb.BevelBox(new Vector3(0, 0.35f, 0.2f), new Vector3(W - 0.06f, 0.05f, D - 0.45f), 0.02f);
            // folded towel with a stain
            mb.Set(S.Linen, Color.white); mb.BevelBox(new Vector3(0, 0.4f, 0.5f), new Vector3(0.3f, 0.06f, 0.25f), 0.02f);
        }

        // ------------------------------------------------------------------ machines
        static void PumpUnit(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.PaintedMetal, new Color(0.2f, 0.45f, 0.5f));
            mb.Box(new Vector3(0, 0.1f, 0), new Vector3(W, 0.2f, D));
            mb.Push(new Vector3(-W * 0.2f, 0.55f, 0), Quaternion.Euler(0, 0, 90), Vector3.one); mb.Cyl(new Vector3(0, -0.4f, 0), 0.32f, 0.8f, 16); mb.Pop();
            mb.Set(S.GreenRust, Color.white);
            mb.Sphere(new Vector3(W * 0.3f, 0.6f, 0), 0.35f, 14, 10);
            mb.Set(S.Copper, Color.white);
            mb.Tube(new List<Vector3> { new Vector3(W * 0.3f, 0.95f, 0), new Vector3(W * 0.3f, H + 0.3f, 0), new Vector3(W * 0.3f, H + 0.4f, -D / 2 - 0.1f) }, 0.07f, 10);
            // pressure gauges
            for (int i = 0; i < 2; i++)
            {
                mb.Set(S.Brass, Color.white); mb.Push(new Vector3(-0.3f + i * 0.3f, 1.05f, D / 2 - 0.05f), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Cyl(Vector3.zero, 0.08f, 0.04f, 14); mb.Pop();
                mb.Set(S.Clock, Color.white, new Vector4(4, i * 3 + 1, 30 * (i + 1), 9));
                mb.FaceUV(new Vector3(-0.3f + i * 0.3f + 0.07f, 0.98f, D / 2 - 0.009f), new Vector3(-0.14f, 0, 0), new Vector3(0, 0.14f, 0), new Rect(1, 0, -1, 1));
            }
        }

        static void Terminal(Ctx c, MeshBuilder mb)
        {
            // the house's register desk: an oak lectern cabinet with a brass instrument panel — two dial gauges, a paper-tape
            // recorder and a row of brass toggles with dim amber pilot jewels (no screens, no LEDs)
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodDark, c.WoodC * 0.8f);
            mb.BevelBox(new Vector3(0, 0.45f, 0), new Vector3(W, 0.9f, D), 0.02f);
            mb.Set(S.Brass, new Color(0.62f, 0.5f, 0.34f));
            mb.Box(new Vector3(0, 0.9f, 0), new Vector3(W + 0.02f, 0.02f, D + 0.02f));
            // sloped brass-framed panel
            mb.Push(new Vector3(0, 0.98f, 0.02f), Quaternion.Euler(-24, 0, 0), Vector3.one);
            mb.Set(S.WoodDark, c.WoodC * 0.65f); mb.Box(Vector3.zero, new Vector3(W - 0.04f, 0.05f, D * 0.8f));
            mb.Set(S.Brass, new Color(0.6f, 0.48f, 0.33f)); mb.Box(new Vector3(0, 0.027f, 0), new Vector3(W - 0.1f, 0.004f, D * 0.7f));
            for (int i = 0; i < 5; i++)
            {
                float x = -W * 0.3f + i * W * 0.15f;
                mb.Set(S.Iron, new Color(0.18f, 0.17f, 0.16f)); mb.Rod(new Vector3(x, 0.03f, D * 0.2f), new Vector3(x, 0.07f, D * 0.24f), 0.005f, 5, false);
                mb.Set(S.Glow, new Color(1f, 0.55f, 0.22f), MansionMats.GlowData(0.5f, 0.3f, 0, c.Circuit)); mb.Sphere(new Vector3(x, 0.032f, D * 0.05f), 0.007f, 6, 4);
            }
            mb.Pop();
            // back board with two gauges and the tape recorder window
            mb.Set(S.WoodDark, c.WoodC * 0.75f);
            mb.Box(new Vector3(0, 1.18f, -D / 2 + 0.06f), new Vector3(W * 0.92f, 0.4f, 0.1f));
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1 : 1) * W * 0.24f;
                mb.Set(S.Brass, new Color(0.62f, 0.5f, 0.34f));
                mb.Push(new Vector3(x, 1.2f, -D / 2 + 0.115f), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Torus(Vector3.zero, 0.075f, 0.01f, 18, 5); mb.Pop();
                mb.Set(S.Clock, Color.white, new Vector4(4, i * 2 + 5, 0, 9));
                mb.FaceUV(new Vector3(x + 0.07f, 1.13f, -D / 2 + 0.112f), new Vector3(-0.14f, 0, 0), new Vector3(0, 0.14f, 0), new Rect(1, 0, -1, 1));
            }
            mb.Set(S.Obsidian, Color.white); mb.Box(new Vector3(0, 1.2f, -D / 2 + 0.112f), new Vector3(0.12f, 0.08f, 0.004f));
            mb.Set(S.Paper, new Color(0.9f, 0.86f, 0.74f)); mb.Box(new Vector3(0, 1.2f, -D / 2 + 0.115f), new Vector3(0.08f, 0.05f, 0.002f));
            mb.Set(S.Paper, new Color(0.88f, 0.84f, 0.72f)); mb.Box(new Vector3(0, 1.1f, -D / 2 + 0.14f), new Vector3(0.07f, 0.12f, 0.002f));   // tape spilling out
        }

        static void Switchboard(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.PaintedMetal, new Color(0.25f, 0.3f, 0.28f));
            mb.Box(new Vector3(0, H / 2, -0.05f), new Vector3(W, H, D - 0.1f));
            mb.Set(S.Obsidian, Color.white);
            mb.Box(new Vector3(0, H * 0.55f, D / 2 - 0.09f), new Vector3(W - 0.2f, H * 0.7f, 0.02f));
            // hazard stripes along the bottom
            for (int i = 0; i < 12; i++) { mb.Set(S.GlossPaint, i % 2 == 0 ? new Color(0.9f, 0.7f, 0.05f) : new Color(0.05f, 0.05f, 0.05f)); mb.Box(new Vector3(-W / 2 + (i + 0.5f) * W / 12, 0.1f, D / 2 - 0.09f), new Vector3(W / 12, 0.14f, 0.01f)); }
            // gauges
            for (int i = 0; i < 3; i++)
            {
                mb.Set(S.Clock, Color.white, new Vector4(4, i * 2 + 3, 0, 9));
                mb.FaceUV(new Vector3(-0.6f + i * 0.6f + 0.1f, H - 0.35f, D / 2 - 0.078f), new Vector3(-0.2f, 0, 0), new Vector3(0, 0.2f, 0), new Rect(1, 0, -1, 1));
            }
            var sb = c.Go.AddComponent<SwitchboardView>();
            int n = 8;
            for (int i = 0; i < n; i++)
            {
                float x = -W / 2 + 0.25f + i * (W - 0.5f) / (n - 1);
                // lever base
                mb.Set(S.Iron, Color.white); mb.Box(new Vector3(x, 1.0f, D / 2 - 0.06f), new Vector3(0.12f, 0.2f, 0.06f));
                // colored plaque per circuit
                mb.Set(S.GlossPaint, Color.HSVToRGB(i / (float)n, 0.7f, 0.8f)); mb.Box(new Vector3(x, 0.8f, D / 2 - 0.075f), new Vector3(0.14f, 0.06f, 0.005f));
                var lev = new MeshBuilder();
                lev.Set(S.Chrome, Color.white); lev.Rod(Vector3.zero, new Vector3(0, 0.25f, 0), 0.012f, 6, false);
                lev.Set(S.GlossPaint, new Color(0.7f, 0.05f, 0.05f)); lev.Sphere(new Vector3(0, 0.27f, 0), 0.035f, 8, 6);
                var lt = Child(c, "Lever" + i, new Vector3(x, 1.0f, D / 2 - 0.03f), lev);
                var on = new MeshBuilder(); on.Set(S.Glow, new Color(0.2f, 1f, 0.35f), MansionMats.GlowData(3f, 0.1f, 0, -1)); on.Sphere(Vector3.zero, 0.022f, 8, 5);
                var off = new MeshBuilder(); off.Set(S.Glow, new Color(1f, 0.1f, 0.08f), MansionMats.GlowData(3f, 0.3f, 0, -1)); off.Sphere(Vector3.zero, 0.022f, 8, 5);
                var onT = Child(c, "On" + i, new Vector3(x - 0.04f, 1.2f, D / 2 - 0.07f), on, ShadowCastingMode.Off);
                var offT = Child(c, "Off" + i, new Vector3(x + 0.04f, 1.2f, D / 2 - 0.07f), off, ShadowCastingMode.Off);
                sb.Add(i, lt, onT.gameObject, offT.gameObject);
            }
            c.View.Switchboard = sb;
            foreach (var kv in c.View.CircuitOn) sb.SetLever(kv.Key, kv.Value);
            c.View.AddLight(c.Rv, c.W2(new Vector3(0, 1.8f, D / 2 + 0.6f)), new Color(0.6f, 1f, 0.7f), 1.2f, 3f, LightType.Point, false, 0.2f);
        }

        static void Generator(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.PaintedMetal, new Color(0.45f, 0.12f, 0.1f));
            mb.Box(new Vector3(0, 0.15f, 0), new Vector3(W, 0.3f, D));
            mb.Push(new Vector3(-0.2f, 0.8f, 0), Quaternion.Euler(0, 0, 90), Vector3.one); mb.Cyl(new Vector3(0, -0.6f, 0), 0.5f, 1.2f, 20); mb.Pop();
            mb.Set(S.Iron, Color.white);
            // flywheel
            mb.Push(new Vector3(W / 2 - 0.2f, 0.85f, 0), Quaternion.Euler(0, 0, 90), Vector3.one);
            mb.Torus(Vector3.zero, 0.55f, 0.06f, 24, 6);
            for (int k = 0; k < 6; k++) { float a = k / 6f * Mathf.PI * 2; mb.Rod(Vector3.zero, new Vector3(Mathf.Cos(a) * 0.52f, 0, Mathf.Sin(a) * 0.52f), 0.03f, 5, false); }
            mb.Pop();
            mb.Set(S.Copper, Color.white);
            for (int i = 0; i < 5; i++) mb.Torus(new Vector3(-0.2f + (i - 2) * 0.22f, 0.8f, 0), 0.51f, 0.015f, 20, 4);
            mb.Set(S.Glow, new Color(1f, 0.6f, 0.1f), MansionMats.GlowData(2f, 0.3f, 0, c.Circuit));
            mb.Box(new Vector3(-0.2f, 1.35f, D / 2 - 0.3f), new Vector3(0.3f, 0.06f, 0.06f));
        }

        static void Press(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            float bedTop = 0.85f;
            mb.Set(S.PaintedMetal, new Color(0.55f, 0.45f, 0.1f));
            foreach (float x in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.22f), H / 2, 0), new Vector3(0.4f, H, D * 0.7f));
            mb.Box(new Vector3(0, H - 0.25f, 0), new Vector3(W, 0.5f, D * 0.8f));
            mb.Box(new Vector3(0, bedTop / 2, 0), new Vector3(W - 0.5f, bedTop, D * 0.8f));
            mb.Set(S.MetalPlate, Color.white);
            mb.Box(new Vector3(0, bedTop + 0.02f, 0), new Vector3(W - 0.6f, 0.04f, D * 0.7f));
            // hazard stripes on the columns
            for (int i = 0; i < 10; i++) foreach (float x in new[] { -1f, 1f }) { mb.Set(S.GlossPaint, i % 2 == 0 ? new Color(0.9f, 0.75f, 0.05f) : new Color(0.04f, 0.04f, 0.04f)); mb.Box(new Vector3(x * (W / 2 - 0.22f), 0.1f + i * 0.12f, D * 0.35f + 0.005f), new Vector3(0.4f, 0.06f, 0.01f)); }
            // hydraulic cylinder
            mb.Set(S.Steel, Color.white);
            mb.Cyl(new Vector3(0, H - 0.5f, 0), 0.3f, 0.7f, 18);
            mb.Set(S.Copper, Color.white);
            mb.Tube(new List<Vector3> { new Vector3(0.3f, H - 0.2f, 0), new Vector3(W / 2, H - 0.1f, 0), new Vector3(W / 2 + 0.2f, H - 0.3f, -D / 2) }, 0.04f, 8);
            // dried stains on the bed (a hint)
            mb.Set(S.Linen, new Color(0.25f, 0.08f, 0.06f)); mb.Box(new Vector3(0.1f, bedTop + 0.041f, 0.05f), new Vector3(0.5f, 0.002f, 0.35f), MeshBuilder.Faces.PY);
            // moving ram (child)
            var ram = new MeshBuilder();
            ram.Set(S.Steel, Color.white);
            ram.Cyl(new Vector3(0, 0.3f, 0), 0.16f, 1.2f, 16);
            ram.Set(S.PaintedMetal, new Color(0.55f, 0.45f, 0.1f));
            ram.Box(new Vector3(0, 0.15f, 0), new Vector3(W - 0.7f, 0.3f, D * 0.65f));
            ram.Set(S.MetalPlate, Color.white);
            ram.Box(new Vector3(0, 0.0f, 0), new Vector3(W - 0.72f, 0.04f, D * 0.62f));
            var rt = Child(c, "Ram", new Vector3(0, H - 1.7f, 0), ram);
            var bc = rt.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.15f, 0); bc.size = new Vector3(W - 0.7f, 0.3f, D * 0.65f);
            // warning beacon
            var bea = new MeshBuilder(); bea.Set(S.Glow, new Color(1f, 0.25f, 0.05f), MansionMats.GlowData(3f, 0.9f, 0, 7)); bea.Sphere(Vector3.zero, 0.08f, 10, 6, 1.2f);
            var bt = Child(c, "Beacon", new Vector3(W / 2 - 0.22f, H + 0.1f, 0), bea, ShadowCastingMode.Off);
            var pv = c.Go.AddComponent<PressView>();
            var beaconLight = c.View.AddLight(c.Rv, c.W2(new Vector3(W / 2 - 0.22f, H + 0.3f, 0)), new Color(1f, 0.25f, 0.05f), 3f, 6f, LightType.Point, false, 0.9f);
            pv.Init(rt, bedTop + 0.04f, H - 1.7f, bt.gameObject, beaconLight.Light);
            c.View.Press = pv;
            // the collider of the press frame (columns + crown), leaving the bed accessible
            var col = new GameObject("PressColliders"); col.transform.SetParent(c.T, false);
            foreach (float x in new[] { -1f, 1f }) { var b = col.AddComponent<BoxCollider>(); b.center = new Vector3(x * (W / 2 - 0.22f), H / 2, 0); b.size = new Vector3(0.4f, H, D * 0.7f); }
            var bb = col.AddComponent<BoxCollider>(); bb.center = new Vector3(0, bedTop / 2, 0); bb.size = new Vector3(W - 0.5f, bedTop, D * 0.8f);
            var tb = col.AddComponent<BoxCollider>(); tb.center = new Vector3(0, H - 0.25f, 0); tb.size = new Vector3(W, 0.5f, D * 0.8f);
        }

        static void PressConsole(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.PaintedMetal, new Color(0.3f, 0.32f, 0.33f));
            mb.Box(new Vector3(0, 0.5f, 0), new Vector3(W, 1.0f, D));
            mb.Push(new Vector3(0, 1.05f, 0.05f), Quaternion.Euler(-25, 0, 0), Vector3.one); mb.Box(Vector3.zero, new Vector3(W, 0.1f, D * 0.8f)); mb.Pop();
            // big red mushroom button, two black ones, a key switch, lamps
            mb.Set(S.GlossPaint, new Color(0.8f, 0.03f, 0.03f)); mb.Push(new Vector3(0.18f, 1.12f, 0.05f), Quaternion.Euler(-25, 0, 0), Vector3.one); mb.Lathe(new[] { new Vector2(0.03f, 0), new Vector2(0.07f, 0.03f), new Vector2(0.05f, 0.06f), new Vector2(0.001f, 0.065f) }, 12); mb.Pop();
            mb.Set(S.Obsidian, Color.white); for (int i = 0; i < 2; i++) { mb.Push(new Vector3(-0.15f + i * 0.12f, 1.1f, 0.08f), Quaternion.Euler(-25, 0, 0), Vector3.one); mb.Cyl(Vector3.zero, 0.03f, 0.03f, 8); mb.Pop(); }
            mb.Set(S.Glow, new Color(1f, 0.6f, 0.1f), MansionMats.GlowData(2.5f, 0.2f, 0, 7)); mb.Sphere(new Vector3(-0.3f, 1.15f, -0.05f), 0.02f, 6, 4);
            mb.Set(S.Glow, new Color(0.2f, 1f, 0.3f), MansionMats.GlowData(2.5f, 0.2f, 0, 7)); mb.Sphere(new Vector3(-0.22f, 1.15f, -0.05f), 0.02f, 6, 4);
            mb.Set(S.Screen, new Color(1f, 0.5f, 0.3f), new Vector4(3, c.F.Id + 7, 1, 7 + 10));
            mb.Push(new Vector3(0, 1.22f, -D / 2 + 0.05f), Quaternion.Euler(-10, 0, 0), Vector3.one); mb.FaceUV(new Vector3(0.25f, 0, 0), new Vector3(-0.5f, 0, 0), new Vector3(0, 0.25f, 0), new Rect(1, 0, -1, 1)); mb.Pop();
        }

        static void Pipes(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            var rnd = c.Rng;
            for (int i = 0; i < 4; i++)
            {
                float y = 0.4f + i * (H - 0.5f) / 3; float r = 0.05f + (i % 2) * 0.04f;
                mb.Set(i % 2 == 0 ? S.Copper : S.RustyMetal, Color.white);
                mb.Rod(new Vector3(-W / 2, y, -D / 2 + 0.15f), new Vector3(W / 2, y, -D / 2 + 0.15f), r, 10, false);
                mb.Set(S.Iron, Color.white);
                for (float x = -W / 2 + 0.3f; x < W / 2; x += 0.9f) mb.Box(new Vector3(x, y, -D / 2 + 0.1f), new Vector3(0.05f, r * 2.4f, 0.12f));
            }
            // vertical risers with valve wheels
            for (int k = 0; k < 2; k++)
            {
                float x = -W / 4 + k * W / 2;
                mb.Set(S.RustyMetal, Color.white); mb.Rod(new Vector3(x, 0, -D / 2 + 0.2f), new Vector3(x, H, -D / 2 + 0.2f), 0.07f, 10, false);
                mb.Set(S.GlossPaint, new Color(0.7f, 0.08f, 0.05f));
                mb.Push(new Vector3(x, 1.2f, -D / 2 + 0.33f), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Torus(Vector3.zero, 0.11f, 0.014f, 16, 5); for (int s = 0; s < 4; s++) { float a = s * Mathf.PI / 2; mb.Rod(Vector3.zero, new Vector3(Mathf.Cos(a) * 0.1f, 0, Mathf.Sin(a) * 0.1f), 0.01f, 4, false); } mb.Pop();
            }
            // a leak: dripping water glint
            mb.Set(S.Glass, Color.white); mb.Sphere(new Vector3(0.2f, 0.36f, -D / 2 + 0.15f), 0.015f, 6, 4, 1.4f);
        }

        // ------------------------------------------------------------------ gallery / wardrobe
        static void Easel(Ctx c, MeshBuilder mb)
        {
            float H = c.H;
            mb.Set(S.WoodLight, Color.white);
            mb.Rod(new Vector3(-0.3f, 0, 0.2f), new Vector3(0, H, 0), 0.02f, 5, true); mb.Rod(new Vector3(0.3f, 0, 0.2f), new Vector3(0, H, 0), 0.02f, 5, true); mb.Rod(new Vector3(0, 0, -0.35f), new Vector3(0, H * 0.95f, 0), 0.02f, 5, true);
            mb.Box(new Vector3(0, 0.75f, 0.14f), new Vector3(0.6f, 0.04f, 0.06f));
            // canvas: an unfinished portrait whose face is blank
            mb.Set(S.Painting, Color.white);
            var r = ProcTex.PortraitRect(c.F.Id % 8);
            mb.Push(new Vector3(0, 0.77f, 0.13f), Quaternion.Euler(-10, 0, 0), Vector3.one);
            mb.FaceUV(new Vector3(0.28f, 0, 0.02f), new Vector3(-0.56f, 0, 0), new Vector3(0, 0.74f, 0), new Rect(r.xMax, r.yMin, -r.width, r.height));
            mb.Set(S.Linen, Color.white); mb.Box(new Vector3(0, 0.37f, 0), new Vector3(0.6f, 0.76f, 0.03f));
            mb.Pop();
            // palette + brushes on the ledge
            mb.Set(S.WoodLight, Color.white); mb.Disc(new Vector3(0.2f, 0.772f, 0.2f), 0.09f, 10, true);
            foreach (var pc in new[] { c.Pal.Neon, c.Pal.Accent2, new Color(0.6f, 0.05f, 0.05f) }) { mb.Set(S.GlossPaint, pc); mb.Sphere(new Vector3(0.2f + ((float)c.Rng.NextDouble() - 0.5f) * 0.1f, 0.78f, 0.2f + ((float)c.Rng.NextDouble() - 0.5f) * 0.08f), 0.015f, 6, 4, 0.4f); }
        }

        static void Pedestal(Ctx c, MeshBuilder mb)
        {
            float H = c.H;
            mb.Set(S.Marble, Color.Lerp(Color.white, c.Pal.FloorA, 0.3f));
            mb.Box(new Vector3(0, 0.06f, 0), new Vector3(c.W, 0.12f, c.D));
            mb.Push(Vector3.zero, 0);
            mb.Lathe(new[] { new Vector2(c.W * 0.35f, 0.12f), new Vector2(c.W * 0.3f, 0.2f), new Vector2(c.W * 0.22f, 0.3f), new Vector2(c.W * 0.2f, H - 0.25f), new Vector2(c.W * 0.28f, H - 0.12f) }, 16);
            mb.Pop();
            mb.Box(new Vector3(0, H - 0.06f, 0), new Vector3(c.W * 0.9f, 0.12f, c.D * 0.9f));
            mb.Set(S.Gold, c.Pal.Trim);
            mb.Box(new Vector3(0, H - 0.14f, 0), new Vector3(c.W * 0.62f, 0.03f, c.D * 0.62f));
            // a small brass plaque
            mb.Box(new Vector3(0, H * 0.55f, c.W * 0.21f), new Vector3(0.12f, 0.05f, 0.005f));
        }

        static void FrameWall(Ctx c, MeshBuilder mb)
        {
            float W = c.W, H = c.H;
            var rnd = c.Rng;
            // salon hang: several gilded frames of different sizes with scratched-face portraits
            var slots = new List<Rect>();
            float x = -W / 2 + 0.1f;
            int idx = c.F.Id;
            while (x < W / 2 - 0.4f)
            {
                float fw = 0.45f + (float)rnd.NextDouble() * 0.4f; if (x + fw > W / 2 - 0.05f) fw = W / 2 - 0.05f - x; if (fw < 0.3f) break;
                float fh = fw * (1.15f + (float)rnd.NextDouble() * 0.25f);
                float y = 0.9f + (float)rnd.NextDouble() * 0.4f;
                PictureFrame(mb, new Vector3(x + fw / 2, y + fh / 2, 0), fw, fh, idx++, c.Pal, rnd.NextDouble() < 0.5);
                if (rnd.NextDouble() < 0.6 && y + fh + 0.1f + fw * 0.8f < H + 0.8f)
                {
                    float fw2 = fw * 0.7f, fh2 = fw2 * 0.8f;
                    PictureFrame(mb, new Vector3(x + fw / 2, y + fh + 0.12f + fh2 / 2, 0), fw2, fh2, idx++, c.Pal, true);
                }
                x += fw + 0.12f;
            }
            // picture lights
            c.View.AddLight(c.Rv, c.W2(new Vector3(0, H + 0.4f, 0.8f)), c.Pal.Warm, 3f, 3.5f, LightType.Spot, false, 0.03f, false, false, false, Quaternion.LookRotation(c.T.TransformDirection(new Vector3(0, -0.7f, -1f).normalized)), 70f);
        }

        internal static void PictureFrame(MeshBuilder mb, Vector3 center, float w, float h, int portrait, MansionPalette pal, bool ornate)
        {
            float fw = ornate ? 0.08f : 0.05f, fd = 0.05f;
            mb.Set(S.Gold, pal.Trim);
            mb.Box(center + new Vector3(0, h / 2 - fw / 2, fd / 2), new Vector3(w, fw, fd));
            mb.Box(center + new Vector3(0, -h / 2 + fw / 2, fd / 2), new Vector3(w, fw, fd));
            mb.Box(center + new Vector3(-w / 2 + fw / 2, 0, fd / 2), new Vector3(fw, h - 2 * fw, fd));
            mb.Box(center + new Vector3(w / 2 - fw / 2, 0, fd / 2), new Vector3(fw, h - 2 * fw, fd));
            if (ornate)
            {
                mb.Set(S.Gold, Lighter(pal.Trim, 0.2f));
                foreach (var cc in new[] { new Vector3(-1, -1), new Vector3(-1, 1), new Vector3(1, -1), new Vector3(1, 1) }) mb.Sphere(center + new Vector3(cc.x * (w / 2 - fw / 2), cc.y * (h / 2 - fw / 2), fd + 0.01f), fw * 0.55f, 8, 5);
                mb.Ellipsoid(center + new Vector3(0, h / 2 + 0.02f, fd), new Vector3(w * 0.18f, 0.05f, 0.02f), 10, 5);
            }
            var r = ProcTex.PortraitRect(portrait);
            mb.Set(S.Painting, Color.white);
            // UV x mirrored because the face looks along +Z
            mb.FaceUV(center + new Vector3(w / 2 - fw, -h / 2 + fw, 0.012f), new Vector3(-(w - 2 * fw), 0, 0), new Vector3(0, h - 2 * fw, 0), new Rect(r.xMax, r.yMin, -r.width, r.height));
        }

        static void DressForm(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.WoodDark, c.WoodC);
            for (int k = 0; k < 3; k++) { float a = k / 3f * Mathf.PI * 2; mb.Rod(new Vector3(0, 0.12f, 0), new Vector3(Mathf.Cos(a) * 0.25f, 0.0f, Mathf.Sin(a) * 0.25f), 0.015f, 5, true); }
            mb.Rod(new Vector3(0, 0.1f, 0), new Vector3(0, 0.85f, 0), 0.02f, 6, false);
            mb.Set(S.Linen, Color.Lerp(new Color(0.85f, 0.78f, 0.68f), c.Pal.Fabric, 0.2f));
            mb.Push(Vector3.zero, 0);
            mb.Lathe(new[] { new Vector2(0.001f, 0.8f), new Vector2(0.17f, 0.85f), new Vector2(0.2f, 0.98f), new Vector2(0.14f, 1.15f), new Vector2(0.18f, 1.3f), new Vector2(0.21f, 1.42f), new Vector2(0.16f, 1.52f), new Vector2(0.06f, 1.56f), new Vector2(0.05f, 1.62f), new Vector2(0.001f, 1.64f) }, 16);
            mb.Pop();
            // pinned fabric drape + measuring tape; a porcelain doll head on top instead of a finial
            mb.Set(S.Velvet, c.Pal.Fabric);
            mb.Push(new Vector3(0, 0, 0.02f), 0); mb.Lathe(new[] { new Vector2(0.22f, 0.8f), new Vector2(0.21f, 1.0f), new Vector2(0.001f, 1.02f) }, 16, false, false, -40, 200); mb.Pop();
            mb.Set(S.Porcelain, new Color(0.95f, 0.9f, 0.86f));
            mb.Sphere(new Vector3(0, 1.72f, 0), 0.09f, 12, 8);
            mb.Set(S.Obsidian, Color.white);
            mb.Sphere(new Vector3(-0.03f, 1.74f, 0.08f), 0.013f, 6, 4); mb.Sphere(new Vector3(0.03f, 1.74f, 0.08f), 0.013f, 6, 4);
        }

        static void VanityDesk(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D;
            mb.Set(S.WoodPainted, Lighter(c.Pal.Wall, 0.4f));
            mb.BevelBox(new Vector3(0, 0.74f, 0), new Vector3(W, 0.05f, D), 0.01f);
            OpenVanityPedestals(c, mb, Lighter(c.Pal.Wall, 0.4f));   // two pedestals of drawers that open
            // mirror with a ring of bulbs
            mb.Set(S.WoodPainted, Lighter(c.Pal.Wall, 0.4f));
            mb.Box(new Vector3(0, 1.2f, -D / 2 + 0.05f), new Vector3(W * 0.8f, 0.85f, 0.04f));
            mb.Set(S.Mirror, Color.white);
            mb.Box(new Vector3(0, 1.2f, -D / 2 + 0.075f), new Vector3(W * 0.72f, 0.75f, 0.01f));
            mb.Set(S.Glow, new Color(1f, 0.9f, 0.75f), MansionMats.GlowData(2.2f, 0.12f, 0, c.Circuit));
            for (int i = 0; i < 7; i++) { mb.Sphere(new Vector3(-W * 0.38f, 0.88f + i * 0.11f, -D / 2 + 0.1f), 0.025f, 6, 4); mb.Sphere(new Vector3(W * 0.38f, 0.88f + i * 0.11f, -D / 2 + 0.1f), 0.025f, 6, 4); }
            c.View.AddLight(c.Rv, c.W2(new Vector3(0, 1.2f, 0.4f)), new Color(1f, 0.88f, 0.75f), 1.8f, 3f);
            // cosmetics
            for (int i = 0; i < 5; i++) { mb.Set(i % 2 == 0 ? S.Glass : S.GlossPaint, i % 2 == 0 ? Color.white : c.Pal.Neon); mb.Cyl(new Vector3(-0.3f + i * 0.14f, 0.765f, 0.05f), 0.025f, 0.06f + i % 3 * 0.03f, 8); }
        }

        static void CostumeRack(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.Chrome, Color.white);
            foreach (float x in new[] { -1f, 1f }) { mb.Rod(new Vector3(x * W / 2, 0.08f, 0), new Vector3(x * W / 2, H, 0), 0.015f, 6, false); mb.Rod(new Vector3(x * W / 2, 0.08f, -D / 2), new Vector3(x * W / 2, 0.08f, D / 2), 0.015f, 6, true); }
            mb.Rod(new Vector3(-W / 2, H, 0), new Vector3(W / 2, H, 0), 0.015f, 6, true);
            var rnd = c.Rng;
            int n = (int)(W / 0.14f);
            for (int i = 0; i < n; i++)
            {
                float x = -W / 2 + 0.1f + i * (W - 0.2f) / Mathf.Max(1, n - 1);
                Color col = Color.HSVToRGB(((float)rnd.NextDouble() * 0.2f + (i % 3) * 0.33f) % 1f, 0.6f, 0.5f);
                if (i % 4 == 0) col = c.Pal.Fabric;
                mb.Set(S.Velvet, col);
                float len = 0.8f + (float)rnd.NextDouble() * 0.6f;
                // hanger + garment (flattened lathe)
                mb.Push(new Vector3(x, H - 0.08f, 0), Quaternion.Euler(0, 90, 0), new Vector3(1, 1, 0.18f));
                mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.2f, -0.05f), new Vector2(0.18f, -0.3f), new Vector2(0.22f + (float)rnd.NextDouble() * 0.1f, -len), new Vector2(0.001f, -len - 0.01f) }, 10);
                mb.Pop();
                mb.Set(S.Chrome, Color.white); mb.Torus(new Vector3(x, H + 0.03f, 0), 0.025f, 0.004f, 8, 3);
            }
        }

        static void StandingMirror(Ctx c, MeshBuilder mb)
        {
            float W = c.W, H = c.H;
            mb.Set(S.WoodDark, c.WoodC);
            mb.Box(new Vector3(0, 0.03f, 0), new Vector3(W, 0.06f, 0.4f));
            foreach (float x in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.04f), H / 2, 0), new Vector3(0.06f, H, 0.06f));
            mb.Push(new Vector3(0, H / 2 + 0.1f, 0), Quaternion.Euler(-6, 0, 0), Vector3.one);
            mb.Set(S.Gold, Color.Lerp(c.Pal.Trim, new Color(0.42f, 0.33f, 0.2f), 0.55f));
            mb.Box(new Vector3(0, 0, -0.02f), new Vector3(W - 0.14f, H - 0.35f, 0.04f));
            mb.Set(S.Mirror, Color.white);
            mb.Box(new Vector3(0, 0, 0.005f), new Vector3(W - 0.24f, H - 0.45f, 0.01f));
            // a single crack across the glass
            mb.Set(S.Obsidian, Color.white);
            mb.Bar(new Vector3(-0.25f, 0.4f, 0.012f), new Vector3(0.2f, -0.1f, 0.012f), 0.004f, 0.002f);
            mb.Bar(new Vector3(0.0f, 0.15f, 0.012f), new Vector3(0.25f, 0.3f, 0.012f), 0.003f, 0.002f);
            mb.Pop();
            var m = Models.Get("ornate_mirror_01");
            if (m != null) { var g = Models.Place(m, c.T, Vector3.zero, 0, new Vector3(W * 0.6f, 0.5f, 0), null); g.transform.localPosition = new Vector3(0, H - 0.1f, 0.03f); g.transform.localRotation = Quaternion.identity; }
        }

        static void Bench(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D;
            bool outdoor = c.Rv.Room.Type == RoomType.Courtyard || c.Rv.Room.Type == RoomType.Greenhouse;
            mb.Set(outdoor ? S.Iron : S.WoodDark, outdoor ? new Color(0.2f, 0.22f, 0.2f) : c.WoodC);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) TurnedLeg(mb, new Vector3(x * (W / 2 - 0.08f), 0, z * (D / 2 - 0.07f)), 0.4f, 0.03f);
            if (outdoor) { mb.Set(S.WoodWorn, Color.white); for (int i = 0; i < 4; i++) mb.Box(new Vector3(0, 0.42f, -D / 2 + 0.07f + i * (D - 0.14f) / 3), new Vector3(W, 0.03f, 0.08f)); }
            else { mb.Set(S.Velvet, c.Fabric); mb.BevelBox(new Vector3(0, 0.44f, 0), new Vector3(W - 0.04f, 0.1f, D - 0.04f), 0.03f); mb.Set(S.Gold, c.Pal.Trim); for (int i = 0; i < 5; i++) mb.Sphere(new Vector3(-W / 2 + 0.2f + i * (W - 0.4f) / 4, 0.49f, 0), 0.012f, 5, 3); }
        }

        static void WaitBench(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D;
            int n = Mathf.Max(2, (int)(W / 0.55f));
            mb.Set(S.Steel, Color.white);
            mb.Box(new Vector3(0, 0.3f, 0), new Vector3(W, 0.05f, 0.08f));
            foreach (float x in new[] { -W / 2 + 0.2f, W / 2 - 0.2f }) { mb.Box(new Vector3(x, 0.15f, 0), new Vector3(0.05f, 0.3f, 0.05f)); mb.Box(new Vector3(x, 0.01f, 0), new Vector3(0.1f, 0.02f, D * 0.8f)); }
            for (int i = 0; i < n; i++)
            {
                float x = -W / 2 + (i + 0.5f) * W / n;
                mb.Set(S.Plastic, i % 3 == 1 ? c.Pal.Neon * 0.8f : Color.Lerp(c.Pal.Wall, Color.white, 0.2f));
                mb.BevelBox(new Vector3(x, 0.43f, 0.03f), new Vector3(W / n - 0.06f, 0.05f, D - 0.12f), 0.02f);
                mb.Push(new Vector3(x, 0.65f, -D / 2 + 0.06f), Quaternion.Euler(-10, 0, 0), Vector3.one); mb.BevelBox(Vector3.zero, new Vector3(W / n - 0.06f, 0.42f, 0.04f), 0.02f); mb.Pop();
            }
            // a forgotten suitcase on one seat
            var sc = Models.Get("vintage_suitcase");
            if (sc != null && c.Var % 2 == 0) { var g = Models.Place(sc, c.T, Vector3.zero, 0, new Vector3(0.6f, 0, 0.35f), null); g.transform.localPosition = new Vector3(W / 2 - 0.4f, 0.46f, 0); g.transform.localRotation = Quaternion.Euler(0, 90, 90); }
        }

        static void Planter(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D, H = c.H;
            mb.Set(S.StoneWall, Color.Lerp(Color.white, c.Pal.Wall, 0.2f));
            mb.Box(new Vector3(0, H / 2, -D / 2 + 0.05f), new Vector3(W, H, 0.1f)); mb.Box(new Vector3(0, H / 2, D / 2 - 0.05f), new Vector3(W, H, 0.1f));
            mb.Box(new Vector3(-W / 2 + 0.05f, H / 2, 0), new Vector3(0.1f, H, D)); mb.Box(new Vector3(W / 2 - 0.05f, H / 2, 0), new Vector3(0.1f, H, D));
            mb.Set(S.Soil, Color.white); mb.Box(new Vector3(0, H - 0.08f, 0), new Vector3(W - 0.2f, 0.02f, D - 0.2f), MeshBuilder.Faces.PY);
            int n = Mathf.Max(3, (int)(W / 0.45f));
            for (int i = 0; i < n; i++)
            {
                var p = new Vector3(-W / 2 + 0.25f + i * (W - 0.5f) / (n - 1), H - 0.07f, ((float)c.Rng.NextDouble() - 0.5f) * (D - 0.4f));
                PlantShape(mb, p, 0.5f + (float)c.Rng.NextDouble() * 0.7f, c.Rng, c.Pal, (i + c.Var) % 3);
            }
        }

        static void Plant(Ctx c, MeshBuilder mb)
        {
            mb.Set(S.Clay, Color.Lerp(new Color(0.62f, 0.33f, 0.2f), c.Pal.Wall, 0.3f));
            mb.Push(Vector3.zero, 0); mb.Lathe(new[] { new Vector2(0.18f, 0), new Vector2(0.24f, 0.36f), new Vector2(0.27f, 0.4f), new Vector2(0.25f, 0.42f) }, 14, true); mb.Pop();
            mb.Set(S.Soil, Color.white); mb.Disc(new Vector3(0, 0.39f, 0), 0.24f, 12, true);
            PlantShape(mb, new Vector3(0, 0.38f, 0), c.H - 0.4f, c.Rng, c.Pal, c.Var % 3);
        }

        /// <summary>kind 0 = palm fronds, 1 = sword leaves (sansevieria), 2 = dark carnivorous bulbs</summary>
        internal static void PlantShape(MeshBuilder mb, Vector3 p, float h, System.Random rnd, MansionPalette pal, int kind)
        {
            Color leaf = Color.Lerp(new Color(0.12f, 0.35f, 0.15f), pal.Accent2, kind == 2 ? 0.1f : 0.15f);
            if (kind == 2) leaf = Color.Lerp(new Color(0.25f, 0.05f, 0.12f), pal.Neon, 0.2f);
            int n = kind == 1 ? 9 : 7;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2 + (float)rnd.NextDouble() * 0.5f;
                float lean = kind == 1 ? 0.15f : 0.5f + (float)rnd.NextDouble() * 0.3f;
                Vector3 dir = new Vector3(Mathf.Cos(a) * lean, 1, Mathf.Sin(a) * lean).normalized;
                float len = h * (0.7f + (float)rnd.NextDouble() * 0.3f);
                mb.Set(S.Leaf, leaf * (0.8f + (float)rnd.NextDouble() * 0.4f));
                var right = Vector3.Cross(dir, Vector3.up).normalized;
                if (kind == 0)
                {
                    // arched frond: stem + leaflets
                    var tip = p + dir * len + Vector3.down * len * 0.35f;
                    var mid = p + dir * len * 0.6f + Vector3.up * len * 0.1f;
                    mb.Tube(new List<Vector3> { p, mid, tip }, 0.008f, 4);
                    for (int k = 1; k < 8; k++)
                    {
                        float t = k / 8f; var q = t < 0.6f ? Vector3.Lerp(p, mid, t / 0.6f) : Vector3.Lerp(mid, tip, (t - 0.6f) / 0.4f);
                        float lw = len * 0.22f * Mathf.Sin(t * Mathf.PI);
                        mb.Quad(q, q + right * lw + Vector3.down * lw * 0.3f, q + right * lw * 0.8f + dir * 0.06f + Vector3.down * lw * 0.3f, q + dir * 0.06f);
                        mb.Quad(q, q + dir * 0.06f, q - right * lw * 0.8f + dir * 0.06f + Vector3.down * lw * 0.3f, q - right * lw + Vector3.down * lw * 0.3f);
                    }
                }
                else if (kind == 1)
                {
                    var tip = p + dir * len;
                    float w = 0.05f;
                    mb.Quad(p - right * w, tip - right * 0.005f, tip + right * 0.005f, p + right * w);
                    mb.Quad(p + right * w, tip + right * 0.005f, tip - right * 0.005f, p - right * w);
                }
                else
                {
                    var tip = p + dir * len * 0.7f;
                    mb.Tube(new List<Vector3> { p, p + dir * len * 0.35f + right * 0.05f, tip }, 0.012f, 5);
                    // pitcher bulb / mouth with teeth
                    mb.Push(tip, Quaternion.FromToRotation(Vector3.up, dir), Vector3.one);
                    mb.Lathe(new[] { new Vector2(0.001f, -0.02f), new Vector2(0.06f, 0.05f), new Vector2(0.045f, 0.14f), new Vector2(0.06f, 0.17f) }, 8);
                    mb.Set(S.Bone, Color.white);
                    for (int t = 0; t < 7; t++) { float ta = t / 7f * Mathf.PI * 2; mb.Box(new Vector3(Mathf.Cos(ta) * 0.055f, 0.19f, Mathf.Sin(ta) * 0.055f), new Vector3(0.012f, 0.04f, 0.012f)); }
                    mb.Pop();
                }
            }
        }

        static void Rug(Ctx c, MeshBuilder mb)
        {
            float W = c.W, D = c.D;
            // a knotted Persian rug: colourway follows the room's palette warmth; the design runs along the long side
            var pal = c.Pal;
            float warm = pal.Wall.r - pal.Wall.b;
            int v = warm > 0.12f ? (c.F.Id % 2 == 0 ? 0 : 3) : warm < -0.05f ? (c.F.Id % 2 == 0 ? 1 : 2) : (c.F.Id % 4);
            mb.Set(S.RugA + v, Color.white);
            float y = 0.012f;
            var a = new Vector3(-W / 2, y, -D / 2); var b = new Vector3(-W / 2, y, D / 2); var cc = new Vector3(W / 2, y, D / 2); var d = new Vector3(W / 2, y, -D / 2);
            if (W >= D) mb.QuadUV(a, b, cc, d, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));   // v along x (the long side)
            else mb.QuadUV(a, b, cc, d, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            // pile edge (sides)
            mb.Set(S.Velvet, Darker(c.Pal.Carpet, 0.5f));
            mb.Box(new Vector3(0, 0.006f, 0), new Vector3(W, 0.012f, D), MeshBuilder.Faces.Sides);
            // fringe on the short ends
            mb.Set(S.Linen, new Color(0.86f, 0.8f, 0.68f));
            if (W >= D) { for (float z = -D / 2 + 0.03f; z < D / 2; z += 0.045f) { mb.Box(new Vector3(-W / 2 - 0.04f, 0.004f, z), new Vector3(0.08f, 0.005f, 0.012f)); mb.Box(new Vector3(W / 2 + 0.04f, 0.004f, z), new Vector3(0.08f, 0.005f, 0.012f)); } }
            else { for (float x = -W / 2 + 0.03f; x < W / 2; x += 0.045f) { mb.Box(new Vector3(x, 0.004f, -D / 2 - 0.04f), new Vector3(0.012f, 0.005f, 0.08f)); mb.Box(new Vector3(x, 0.004f, D / 2 + 0.04f), new Vector3(0.012f, 0.005f, 0.08f)); } }
        }
    }
}
