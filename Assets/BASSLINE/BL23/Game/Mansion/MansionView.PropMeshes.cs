using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>Procedural meshes for small dressing props (cached per variant by MansionView.ProcProp). Origin at the
    /// bottom centre, front toward +Z, metres.</summary>
    public sealed partial class MansionView
    {
        static readonly Color[] BookColors =
        {
            new Color(0.42f, 0.07f, 0.09f), new Color(0.09f, 0.22f, 0.14f), new Color(0.11f, 0.12f, 0.28f), new Color(0.33f, 0.21f, 0.1f),
            new Color(0.08f, 0.07f, 0.07f), new Color(0.5f, 0.36f, 0.14f), new Color(0.3f, 0.1f, 0.25f), new Color(0.55f, 0.5f, 0.42f)
        };

        static MeshBuilder CandlestickMesh(int v)
        {
            var mb = new MeshBuilder();
            switch (v % 4) { case 0: mb.Set(S.Brass, Color.white); break; case 1: mb.Set(S.Iron, new Color(0.25f, 0.24f, 0.24f)); break; case 2: mb.Set(S.Steel, new Color(0.85f, 0.85f, 0.88f)); break; default: mb.Set(S.Bone, new Color(0.85f, 0.8f, 0.68f)); break; }
            mb.Lathe(new[] { new Vector2(0.07f, 0), new Vector2(0.075f, 0.012f), new Vector2(0.05f, 0.022f), new Vector2(0.018f, 0.04f), new Vector2(0.014f, 0.1f), new Vector2(0.024f, 0.12f), new Vector2(0.013f, 0.14f), new Vector2(0.012f, 0.2f), new Vector2(0.034f, 0.215f), new Vector2(0.03f, 0.225f) }, 12, true, true);
            // candle with drips
            float h = v % 2 == 0 ? 0.13f : 0.08f;
            mb.Set(S.Wax, Color.white);
            mb.Cyl(new Vector3(0, 0.222f, 0), 0.014f, h, 10);
            mb.Cyl(new Vector3(0.012f, 0.222f + h * 0.4f, 0), 0.004f, h * 0.55f, 5);
            mb.Cyl(new Vector3(-0.006f, 0.222f + h * 0.2f, 0.011f), 0.0035f, h * 0.7f, 5);
            mb.Set(S.Obsidian, Color.white); mb.Cyl(new Vector3(0, 0.222f + h, 0), 0.0015f, 0.008f, 4);
            return mb;
        }

        static MeshBuilder BottleMesh(int v)
        {
            var mb = new MeshBuilder();
            Color glass = v == 0 ? new Color(0.08f, 0.2f, 0.1f) : v == 1 ? new Color(0.25f, 0.06f, 0.08f) : new Color(0.28f, 0.2f, 0.08f);
            mb.Set(S.GlossPaint, glass);
            float r = 0.036f;
            mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(r * 0.9f, 0.002f), new Vector2(r, 0.012f), new Vector2(r, 0.2f), new Vector2(r * 0.8f, 0.235f), new Vector2(0.013f, 0.265f), new Vector2(0.012f, 0.31f), new Vector2(0.015f, 0.315f), new Vector2(0.013f, 0.325f) }, 12, false, true);
            // paper label + foil
            mb.Set(S.Paper, v == 2 ? new Color(0.85f, 0.8f, 0.62f) : new Color(0.9f, 0.86f, 0.76f));
            mb.Lathe(new[] { new Vector2(r + 0.0015f, 0.07f), new Vector2(r + 0.0015f, 0.16f) }, 12, false, false, -70f, 70f);
            mb.Set(S.Gold, new Color(0.8f, 0.6f, 0.3f));
            mb.Lathe(new[] { new Vector2(0.0145f, 0.29f), new Vector2(0.0145f, 0.326f), new Vector2(0.001f, 0.327f) }, 10);
            return mb;
        }

        static MeshBuilder BottleCrateMesh()
        {
            var mb = new MeshBuilder();
            mb.Set(S.WoodWorn, new Color(0.8f, 0.7f, 0.6f));
            float W = 0.5f, D = 0.34f, H = 0.26f;
            mb.Box(new Vector3(0, 0.01f, 0), new Vector3(W, 0.02f, D));
            foreach (float z in new[] { -1f, 1f }) mb.Box(new Vector3(0, H / 2, z * (D / 2 - 0.01f)), new Vector3(W, H, 0.02f));
            foreach (float x in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W / 2 - 0.01f), H / 2, 0), new Vector3(0.02f, H, D));
            for (int i = 0; i < 6; i++)
            {
                var b = BottleMesh(i % 3); var m = b.ToMesh("tmp", out var sl);
                var slotMap = sl;
                mb.Append(m, Matrix4x4.TRS(new Vector3(-0.16f + (i % 3) * 0.16f, 0.02f, -0.08f + (i / 3) * 0.16f), Quaternion.identity, Vector3.one), slotMap);
                Object.DestroyImmediate(m);
            }
            return mb;
        }

        static MeshBuilder CupMesh(bool pair)
        {
            var mb = new MeshBuilder();
            int n = pair ? 2 : 1;
            for (int k = 0; k < n; k++)
            {
                var o = pair ? new Vector3(k == 0 ? -0.08f : 0.09f, 0, k == 0 ? 0.02f : -0.03f) : Vector3.zero;
                mb.Set(S.Porcelain, Color.white);
                mb.Push(o, 0);
                mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.058f, 0.003f), new Vector2(0.064f, 0.01f) }, 14);
                mb.Lathe(new[] { new Vector2(0.001f, 0.008f), new Vector2(0.022f, 0.008f), new Vector2(0.036f, 0.035f), new Vector2(0.04f, 0.058f), new Vector2(0.036f, 0.058f), new Vector2(0.03f, 0.02f) }, 12);
                mb.Torus(new Vector3(0.042f, 0.035f, 0), 0.012f, 0.003f, 8, 4);
                mb.Set(S.Gold, new Color(0.85f, 0.65f, 0.3f));
                mb.Lathe(new[] { new Vector2(0.0385f, 0.054f), new Vector2(0.0405f, 0.058f) }, 12);
                mb.Set(S.Glow, new Color(0.35f, 0.1f, 0.04f), MansionMats.GlowData(0.03f, 0, 0, -1));
                mb.Disc(new Vector3(0, 0.048f, 0), 0.034f, 10, true);
                mb.Pop();
            }
            return mb;
        }

        static MeshBuilder FruitBowlMesh()
        {
            var mb = new MeshBuilder();
            mb.Set(S.Porcelain, new Color(0.92f, 0.9f, 0.86f));
            mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.06f, 0), new Vector2(0.07f, 0.02f), new Vector2(0.03f, 0.03f), new Vector2(0.12f, 0.05f), new Vector2(0.16f, 0.1f), new Vector2(0.15f, 0.1f), new Vector2(0.1f, 0.06f), new Vector2(0.001f, 0.055f) }, 18);
            var rnd = new System.Random(5);
            Color[] fruit = { new Color(0.55f, 0.06f, 0.08f), new Color(0.6f, 0.45f, 0.1f), new Color(0.25f, 0.05f, 0.2f), new Color(0.45f, 0.08f, 0.05f) };
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2; float rr = i == 6 ? 0 : 0.075f;
                mb.Set(S.GlossPaint, fruit[i % fruit.Length] * (0.8f + (float)rnd.NextDouble() * 0.3f));
                mb.Sphere(new Vector3(Mathf.Cos(a) * rr, 0.1f + (i == 6 ? 0.05f : 0), Mathf.Sin(a) * rr), 0.042f, 10, 7);
            }
            mb.Set(S.Leaf, new Color(0.2f, 0.3f, 0.12f));
            for (int i = 0; i < 4; i++) { float a = i * 1.7f; var p = new Vector3(Mathf.Cos(a) * 0.06f, 0.15f, Mathf.Sin(a) * 0.06f); mb.Quad(p, p + new Vector3(0.05f, 0.02f, 0.01f), p + new Vector3(0.08f, 0.0f, 0.03f), p + new Vector3(0.02f, -0.01f, 0.03f)); }
            return mb;
        }

        static MeshBuilder PanMesh()
        {
            var mb = new MeshBuilder();
            mb.Set(S.Iron, new Color(0.2f, 0.2f, 0.2f));
            mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.11f, 0), new Vector2(0.125f, 0.045f), new Vector2(0.118f, 0.045f), new Vector2(0.105f, 0.006f), new Vector2(0.001f, 0.006f) }, 18);
            mb.Set(S.WoodDark, Color.white);
            mb.Box(new Vector3(0.22f, 0.035f, 0), new Vector3(0.2f, 0.022f, 0.03f));
            return mb;
        }

        static MeshBuilder JarsMesh(int v)
        {
            var mb = new MeshBuilder();
            Color[] fill = { new Color(0.55f, 0.25f, 0.05f), new Color(0.3f, 0.35f, 0.12f), new Color(0.5f, 0.1f, 0.1f), new Color(0.8f, 0.75f, 0.6f) };
            int n = v == 0 ? 3 : 2;
            for (int i = 0; i < n; i++)
            {
                var o = new Vector3((i - (n - 1) * 0.5f) * 0.1f, 0, (i % 2) * 0.03f);
                float h = 0.1f + ((i + v) % 3) * 0.035f;
                mb.Push(o, 0);
                mb.Set(S.GlossPaint, fill[(i + v) % fill.Length]);
                mb.Cyl(Vector3.zero, 0.038f, h * 0.8f, 12);
                mb.Set(S.Glass, Color.white);
                mb.Lathe(new[] { new Vector2(0.042f, 0), new Vector2(0.042f, h), new Vector2(0.03f, h + 0.01f) }, 12);
                mb.Set(S.Linen, new Color(0.85f, 0.8f, 0.7f));
                mb.Lathe(new[] { new Vector2(0.034f, h + 0.005f), new Vector2(0.045f, h + 0.015f), new Vector2(0.001f, h + 0.03f) }, 10);
                mb.Pop();
            }
            return mb;
        }

        static MeshBuilder BoardMesh()
        {
            var mb = new MeshBuilder();
            mb.Set(S.WoodLight, new Color(0.9f, 0.8f, 0.65f));
            mb.BevelBox(new Vector3(0, 0.012f, 0), new Vector3(0.4f, 0.024f, 0.26f), 0.006f);
            mb.Set(S.Clay, new Color(0.75f, 0.55f, 0.35f));
            mb.Ellipsoid(new Vector3(-0.06f, 0.05f, 0), new Vector3(0.1f, 0.03f, 0.06f), 10, 6);
            mb.Set(S.Steel, Color.white);
            mb.Box(new Vector3(0.1f, 0.028f, 0.05f), new Vector3(0.16f, 0.004f, 0.025f));
            mb.Set(S.WoodDark, Color.white);
            mb.Box(new Vector3(0.21f, 0.03f, 0.05f), new Vector3(0.07f, 0.014f, 0.018f));
            return mb;
        }

        static MeshBuilder CansMesh()
        {
            var mb = new MeshBuilder();
            Color[] c = { new Color(0.5f, 0.12f, 0.08f), new Color(0.2f, 0.28f, 0.2f), new Color(0.55f, 0.5f, 0.4f) };
            for (int i = 0; i < 3; i++)
            {
                var o = new Vector3((i - 1) * 0.1f, 0, (i % 2) * 0.04f);
                mb.Set(S.PaintedMetal, c[i]); mb.Cyl(o, 0.045f, 0.12f + (i % 2) * 0.05f, 12);
                mb.Set(S.Steel, Color.white); mb.Cyl(o + Vector3.up * (0.12f + (i % 2) * 0.05f), 0.043f, 0.006f, 12);
            }
            return mb;
        }

        static MeshBuilder FrameStackMesh(MansionPalette pal)
        {
            var mb = new MeshBuilder();
            for (int i = 0; i < 4; i++)
            {
                float w = 0.6f + i * 0.12f, h = 0.8f + (i % 2) * 0.2f;
                mb.Push(new Vector3(0, 0, -0.06f * i), Quaternion.Euler(-12, 0, 0), Vector3.one);
                FurnitureFactory.PictureFrame(mb, new Vector3(0, h * 0.5f, 0), w, h, i + 2, pal, i % 2 == 0);
                mb.Pop();
            }
            return mb;
        }

        /// <summary>A stack of books (n) or a single book; v picks colours and jitter.</summary>
        static MeshBuilder BookStackMesh(int v, int n)
        {
            var mb = new MeshBuilder();
            var rnd = new System.Random(v * 31 + n);
            float y = 0;
            for (int i = 0; i < n; i++)
            {
                float w = 0.16f + (float)rnd.NextDouble() * 0.1f, d = 0.12f + (float)rnd.NextDouble() * 0.08f, t = 0.025f + (float)rnd.NextDouble() * 0.035f;
                float ang = ((float)rnd.NextDouble() - 0.5f) * 30f;
                var off = new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.03f, y, ((float)rnd.NextDouble() - 0.5f) * 0.03f);
                mb.Push(off, ang);
                var col = BookColors[rnd.Next(BookColors.Length)] * (0.75f + (float)rnd.NextDouble() * 0.4f);
                mb.Set(S.Books, col);
                mb.BevelBox(new Vector3(0, t * 0.5f, 0), new Vector3(w, t, d), 0.004f);
                // page block (cream) on three sides
                mb.Set(S.Paper, new Color(0.88f, 0.84f, 0.74f));
                mb.Box(new Vector3(0.004f, t * 0.5f, 0), new Vector3(w - 0.012f, t * 0.78f, d + 0.002f), MeshBuilder.Faces.PZ | MeshBuilder.Faces.NZ | MeshBuilder.Faces.PX);
                if (rnd.NextDouble() < 0.6) { mb.Set(S.Gold, new Color(0.85f, 0.65f, 0.3f)); mb.Box(new Vector3(-w * 0.5f - 0.001f, t * 0.5f, 0), new Vector3(0.002f, t * 0.3f, d * 0.8f)); }
                mb.Pop();
                y += t;
            }
            return mb;
        }

        static MeshBuilder OpenBookMesh()
        {
            var mb = new MeshBuilder();
            mb.Set(S.Books, new Color(0.35f, 0.08f, 0.08f));
            foreach (float s in new[] { -1f, 1f })
            {
                mb.Push(new Vector3(s * 0.1f, 0.004f, 0), Quaternion.Euler(0, 0, s * -4f), Vector3.one);
                mb.Box(Vector3.zero, new Vector3(0.2f, 0.006f, 0.27f));
                mb.Set(S.Paper, new Color(0.9f, 0.86f, 0.76f));
                mb.Box(new Vector3(0, 0.012f, 0), new Vector3(0.19f, 0.014f, 0.255f));
                // printed lines
                mb.Set(S.Obsidian, new Color(0.3f, 0.28f, 0.26f));
                for (int k = 0; k < 9; k++) mb.Box(new Vector3(0, 0.0195f, -0.1f + k * 0.024f), new Vector3(0.15f, 0.001f, 0.006f), MeshBuilder.Faces.PY);
                mb.Set(S.Books, new Color(0.35f, 0.08f, 0.08f));
                mb.Pop();
            }
            return mb;
        }

        static MeshBuilder PapersMesh(int v)
        {
            var mb = new MeshBuilder();
            var rnd = new System.Random(v * 17 + 3);
            int n = 3 + v;
            for (int i = 0; i < n; i++)
            {
                mb.Push(new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.12f, 0.001f + i * 0.0015f, ((float)rnd.NextDouble() - 0.5f) * 0.1f), ((float)rnd.NextDouble() - 0.5f) * 50f);
                mb.Set(S.Paper, Color.Lerp(new Color(0.92f, 0.9f, 0.84f), new Color(0.8f, 0.72f, 0.55f), (float)rnd.NextDouble() * 0.6f));
                mb.Box(Vector3.zero, new Vector3(0.21f, 0.001f, 0.28f), MeshBuilder.Faces.PY);
                mb.Set(S.Obsidian, new Color(0.25f, 0.22f, 0.22f));
                for (int k = 0; k < 7; k++) mb.Box(new Vector3(-0.01f, 0.0008f, -0.1f + k * 0.03f), new Vector3(0.15f, 0.0004f, 0.004f), MeshBuilder.Faces.PY);
                mb.Pop();
            }
            // quill + inkwell on one variant
            if (v == 1)
            {
                mb.Set(S.Obsidian, Color.white); mb.Lathe(new[] { new Vector2(0.03f, 0), new Vector2(0.032f, 0.03f), new Vector2(0.015f, 0.045f), new Vector2(0.012f, 0.055f) }, 10, false, true);
                mb.Set(S.Bone, new Color(0.9f, 0.88f, 0.84f)); mb.Push(new Vector3(0, 0.05f, 0), Quaternion.Euler(20, 30, 10), Vector3.one); mb.Tube(new List<Vector3> { Vector3.zero, new Vector3(0, 0.12f, 0.03f), new Vector3(0, 0.22f, 0.07f) }, 0.004f, 4);
                for (int k = 1; k < 6; k++) { var p = new Vector3(0, 0.1f + k * 0.022f, 0.03f + k * 0.008f); mb.Quad(p, p + new Vector3(0.025f, 0.02f, 0), p + new Vector3(0.02f, 0.03f, 0.005f), p + new Vector3(0, 0.022f, 0.004f)); }
                mb.Pop();
            }
            return mb;
        }

        static MeshBuilder SkullMesh()
        {
            var mb = new MeshBuilder();
            mb.Set(S.Bone, new Color(0.86f, 0.8f, 0.66f));
            mb.Ellipsoid(new Vector3(0, 0.085f, 0), new Vector3(0.068f, 0.07f, 0.085f), 14, 10);
            mb.Ellipsoid(new Vector3(0, 0.045f, 0.045f), new Vector3(0.05f, 0.04f, 0.045f), 12, 8);
            mb.Box(new Vector3(0, 0.018f, 0.055f), new Vector3(0.06f, 0.03f, 0.04f));
            mb.Set(S.Obsidian, Color.white);
            foreach (float x in new[] { -0.024f, 0.024f }) mb.Sphere(new Vector3(x, 0.07f, 0.075f), 0.017f, 8, 6);
            mb.Ellipsoid(new Vector3(0, 0.045f, 0.088f), new Vector3(0.009f, 0.013f, 0.006f), 6, 4);
            mb.Set(S.Bone, new Color(0.9f, 0.86f, 0.74f));
            for (int i = 0; i < 6; i++) mb.Box(new Vector3(-0.022f + i * 0.009f, 0.018f, 0.077f), new Vector3(0.006f, 0.012f, 0.004f));
            return mb;
        }

        static MeshBuilder GlobeMesh(float r)
        {
            var mb = new MeshBuilder();
            mb.Set(S.WoodDark, Color.white);
            float stand = r * 1.4f;
            mb.Lathe(new[] { new Vector2(r * 0.7f, 0), new Vector2(r * 0.72f, r * 0.08f), new Vector2(r * 0.12f, r * 0.2f), new Vector2(r * 0.08f, stand * 0.6f), new Vector2(r * 0.14f, stand) }, 12, false, true);
            mb.Set(S.Brass, Color.white);
            mb.Push(new Vector3(0, stand + r, 0), Quaternion.Euler(0, 0, 23), Vector3.one);
            mb.Torus(Vector3.zero, r * 1.08f, r * 0.03f, 24, 4);
            mb.Pop();
            mb.Set(S.Paper, new Color(0.72f, 0.62f, 0.42f));
            mb.Sphere(new Vector3(0, stand + r, 0), r, 18, 12);
            mb.Set(S.Leaf, new Color(0.3f, 0.35f, 0.2f));
            var rnd = new System.Random(3);
            for (int i = 0; i < 9; i++)
            {
                var d = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f).normalized;
                var c = new Vector3(0, stand + r, 0) + d * r * 1.003f;
                mb.Push(c, Quaternion.LookRotation(d), Vector3.one * r * (0.25f + (float)rnd.NextDouble() * 0.3f));
                mb.Disc(Vector3.zero, 1f, 7, true);
                mb.Pop();
            }
            return mb;
        }

        static MeshBuilder LogBasketMesh()
        {
            var mb = new MeshBuilder();
            mb.Set(S.Iron, new Color(0.18f, 0.17f, 0.16f));
            float W = 0.5f, D = 0.36f, H = 0.3f;
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Rod(new Vector3(x * W / 2, 0, z * D / 2), new Vector3(x * W / 2, H, z * D / 2), 0.012f, 5, true);
            foreach (float y in new[] { 0.06f, H })
            {
                mb.Rod(new Vector3(-W / 2, y, -D / 2), new Vector3(W / 2, y, -D / 2), 0.01f, 5, false); mb.Rod(new Vector3(-W / 2, y, D / 2), new Vector3(W / 2, y, D / 2), 0.01f, 5, false);
                mb.Rod(new Vector3(-W / 2, y, -D / 2), new Vector3(-W / 2, y, D / 2), 0.01f, 5, false); mb.Rod(new Vector3(W / 2, y, -D / 2), new Vector3(W / 2, y, D / 2), 0.01f, 5, false);
            }
            mb.Set(S.WoodWorn, new Color(0.55f, 0.42f, 0.32f));
            var rnd = new System.Random(11);
            for (int i = 0; i < 9; i++)
            {
                float y = 0.1f + (i / 3) * 0.085f, z = -D / 2 + 0.07f + (i % 3) * 0.11f + ((float)rnd.NextDouble() - 0.5f) * 0.03f;
                mb.Rod(new Vector3(-W / 2 - 0.03f, y, z), new Vector3(W / 2 + 0.03f, y + ((float)rnd.NextDouble() - 0.5f) * 0.03f, z), 0.042f + (float)rnd.NextDouble() * 0.012f, 7, true);
            }
            return mb;
        }

        static MeshBuilder HatboxMesh(MansionPalette pal)
        {
            var mb = new MeshBuilder();
            mb.Set(S.Paper, Color.Lerp(pal.Fabric, Color.white, 0.35f));
            mb.Cyl(Vector3.zero, 0.2f, 0.2f, 20);
            mb.Set(S.Paper, Color.Lerp(pal.Fabric, Color.black, 0.2f));
            mb.Cyl(new Vector3(0, 0.19f, 0), 0.205f, 0.04f, 20);
            mb.Set(S.Paper, Color.Lerp(pal.Fabric, Color.white, 0.5f));
            mb.Cyl(new Vector3(0.03f, 0.23f, 0.02f), 0.16f, 0.14f, 18);
            mb.Set(S.Velvet, pal.Carpet);
            mb.Cyl(new Vector3(0.03f, 0.36f, 0.02f), 0.165f, 0.03f, 18);
            return mb;
        }

        static MeshBuilder SackMesh()
        {
            var mb = new MeshBuilder();
            mb.Set(S.Linen, new Color(0.62f, 0.52f, 0.38f));
            mb.Push(Vector3.zero, 0);
            mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.2f, 0.02f), new Vector2(0.24f, 0.15f), new Vector2(0.2f, 0.36f), new Vector2(0.08f, 0.46f), new Vector2(0.05f, 0.5f), new Vector2(0.08f, 0.56f), new Vector2(0.001f, 0.58f) }, 12);
            mb.Pop();
            mb.Set(S.Linen, new Color(0.3f, 0.25f, 0.2f));
            mb.Torus(new Vector3(0, 0.49f, 0), 0.052f, 0.01f, 12, 4);
            return mb;
        }

        static MeshBuilder WateringCanMesh()
        {
            var mb = new MeshBuilder();
            mb.Set(S.GreenRust, Color.white);
            mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.11f, 0), new Vector2(0.12f, 0.02f), new Vector2(0.12f, 0.22f), new Vector2(0.08f, 0.26f), new Vector2(0.001f, 0.26f) }, 14);
            mb.Rod(new Vector3(0.1f, 0.06f, 0), new Vector3(0.32f, 0.26f, 0), 0.016f, 6, false);
            mb.Push(new Vector3(0.34f, 0.28f, 0), Quaternion.Euler(0, 0, -50), Vector3.one); mb.Lathe(new[] { new Vector2(0.015f, -0.02f), new Vector2(0.04f, 0.03f) }, 10); mb.Pop();
            mb.Tube(new List<Vector3> { new Vector3(-0.1f, 0.2f, 0), new Vector3(-0.14f, 0.3f, 0), new Vector3(0.02f, 0.33f, 0), new Vector3(0.06f, 0.26f, 0) }, 0.01f, 5);
            return mb;
        }

        static MeshBuilder FloorCandlesMesh(int v)
        {
            var mb = new MeshBuilder();
            var rnd = new System.Random(v * 13 + 1);
            mb.Set(S.Wax, Color.white);
            mb.Lathe(new[] { new Vector2(0.001f, 0.012f), new Vector2(0.16f, 0.008f), new Vector2(0.22f, 0f) }, 14);
            int n = 5 + v * 2;
            var flames = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2, rr = (float)rnd.NextDouble() * 0.14f;
                var p = new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr);
                float h = 0.1f + (float)rnd.NextDouble() * 0.4f, r = 0.02f + (float)rnd.NextDouble() * 0.02f;
                FurnitureFactory.Candle(mb, p, h, r, -2, p + Vector3.up * h, flames);
            }
            foreach (var f in flames) FlameQuad(mb, f, 0.065f, -2);
            return mb;
        }

        /// <summary>A bouquet to sit in a vase mouth: dark roses / lilies / thistles on wiry stems.</summary>
        static MeshBuilder Bouquet(int v, MansionPalette pal)
        {
            var mb = new MeshBuilder();
            var rnd = new System.Random(v * 97 + pal.Id.Length);
            Color bloom = v == 0 ? new Color(0.42f, 0.03f, 0.08f) : v == 1 ? new Color(0.9f, 0.86f, 0.8f) : Color.Lerp(pal.Accent2, new Color(0.35f, 0.1f, 0.4f), 0.5f);
            int n = 7 + v;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2 + (float)rnd.NextDouble() * 0.4f;
                float lean = 0.2f + (float)rnd.NextDouble() * 0.35f; float len = 0.22f + (float)rnd.NextDouble() * 0.16f;
                var dir = new Vector3(Mathf.Cos(a) * lean, 1f, Mathf.Sin(a) * lean).normalized;
                var tip = dir * len;
                mb.Set(S.Leaf, new Color(0.14f, 0.24f, 0.1f));
                mb.Tube(new List<Vector3> { Vector3.zero, dir * len * 0.5f + new Vector3(0, 0.01f, 0), tip }, 0.0035f, 4);
                // a leaf half-way
                var lp = dir * len * 0.45f; var side = Vector3.Cross(dir, Vector3.up).normalized;
                mb.Quad(lp, lp + side * 0.04f + Vector3.up * 0.02f, lp + side * 0.06f + Vector3.up * 0.05f, lp + Vector3.up * 0.03f);
                mb.Set(S.Velvet, bloom * (0.8f + (float)rnd.NextDouble() * 0.35f));
                if (v == 1)
                {
                    // lily: six pointed petals
                    mb.Push(tip, Quaternion.FromToRotation(Vector3.up, dir), Vector3.one);
                    for (int k = 0; k < 6; k++) { float b = k / 6f * Mathf.PI * 2; var e = new Vector3(Mathf.Cos(b) * 0.045f, 0.03f, Mathf.Sin(b) * 0.045f); mb.Tri(Vector3.zero, e + new Vector3(Mathf.Sin(b) * 0.012f, 0, -Mathf.Cos(b) * 0.012f), e); mb.Tri(Vector3.zero, e, e + new Vector3(Mathf.Sin(b) * 0.012f, 0, -Mathf.Cos(b) * 0.012f)); }
                    mb.Pop();
                }
                else mb.Sphere(tip, v == 0 ? 0.03f : 0.022f, 8, 6, v == 0 ? 0.85f : 1.2f);
            }
            return mb;
        }
    }
}
