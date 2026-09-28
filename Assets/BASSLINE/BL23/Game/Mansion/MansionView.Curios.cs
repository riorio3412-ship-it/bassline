using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>Curios: the uncanny details on the mansion's surfaces (bell jars, specimen jars, wilted flowers, a stuffed
    /// crow, decanters, candle stubs, scattered cards, glazed urns). Origin at the bottom centre, +Z front.</summary>
    public sealed partial class MansionView
    {
        /// <summary>A glass dome on a turned base holding something that should not be kept: a stuffed bird, a single
        /// staring eye, a black rose, a pinned moth, a small withered hand.</summary>
        static MeshBuilder BellJarMesh(int v)
        {
            var mb = new MeshBuilder();
            mb.Set(S.WoodDark, new Color(0.6f, 0.45f, 0.35f));
            mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.12f, 0), new Vector2(0.125f, 0.025f), new Vector2(0.11f, 0.04f), new Vector2(0.001f, 0.04f) }, 20);
            var c = new Vector3(0, 0.04f, 0);
            switch (v % 5)
            {
                case 0: // stuffed crow on a twig
                    mb.Set(S.WoodWorn, new Color(0.4f, 0.3f, 0.22f)); mb.Rod(c + new Vector3(-0.06f, 0.02f, 0), c + new Vector3(0.05f, 0.09f, 0.01f), 0.006f, 5, true);
                    mb.Set(S.Velvet, new Color(0.05f, 0.05f, 0.06f));
                    mb.Ellipsoid(c + new Vector3(0, 0.13f, 0), new Vector3(0.035f, 0.045f, 0.06f), 10, 7);
                    mb.Sphere(c + new Vector3(0, 0.18f, 0.045f), 0.026f, 8, 6);
                    mb.Ellipsoid(c + new Vector3(0, 0.12f, -0.07f), new Vector3(0.02f, 0.012f, 0.045f), 6, 4);
                    mb.Set(S.Obsidian, Color.white); mb.Sphere(c + new Vector3(0.018f, 0.187f, 0.06f), 0.005f, 5, 3); mb.Sphere(c + new Vector3(-0.018f, 0.187f, 0.06f), 0.005f, 5, 3);
                    mb.Set(S.Bone, new Color(0.3f, 0.28f, 0.25f)); mb.Push(c + new Vector3(0, 0.178f, 0.07f), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Lathe(new[] { new Vector2(0.008f, 0), new Vector2(0.001f, 0.035f) }, 6); mb.Pop();
                    break;
                case 1: // an eye on a stalk, looking out
                    mb.Set(S.Flesh, new Color(0.8f, 0.45f, 0.45f)); mb.Tube(new List<Vector3> { c, c + new Vector3(0.01f, 0.07f, 0), c + new Vector3(-0.01f, 0.12f, 0.01f) }, 0.012f, 6);
                    mb.Set(S.Porcelain, new Color(0.95f, 0.92f, 0.88f)); mb.Sphere(c + new Vector3(-0.01f, 0.16f, 0.01f), 0.042f, 14, 10);
                    mb.Set(S.GlossPaint, new Color(0.35f, 0.5f, 0.3f)); mb.Push(c + new Vector3(-0.01f, 0.16f, 0.048f), Quaternion.Euler(-90, 0, 0), Vector3.one); mb.Disc(Vector3.zero, 0.018f, 12, true); mb.Pop();
                    mb.Set(S.Obsidian, Color.white); mb.Push(c + new Vector3(-0.01f, 0.16f, 0.0525f), Quaternion.Euler(-90, 0, 0), Vector3.one); mb.Disc(Vector3.zero, 0.008f, 10, true); mb.Pop();
                    break;
                case 2: // a black rose, one petal fallen
                    mb.Set(S.Leaf, new Color(0.12f, 0.18f, 0.08f)); mb.Rod(c, c + new Vector3(0, 0.15f, 0), 0.004f, 5, false);
                    mb.Set(S.Velvet, new Color(0.08f, 0.02f, 0.04f));
                    for (int k = 0; k < 7; k++) { float a = k * 2.1f; mb.Ellipsoid(c + new Vector3(Mathf.Cos(a) * 0.012f, 0.16f + k * 0.003f, Mathf.Sin(a) * 0.012f), new Vector3(0.02f, 0.014f, 0.02f), 7, 5); }
                    mb.Ellipsoid(c + new Vector3(0.05f, 0.004f, 0.02f), new Vector3(0.016f, 0.003f, 0.012f), 6, 3);
                    break;
                case 3: // a moth the size of a hand, pinned to a card
                    mb.Set(S.Paper, new Color(0.85f, 0.8f, 0.68f)); mb.Push(c + new Vector3(0, 0.11f, 0), Quaternion.Euler(-75, 0, 0), Vector3.one); mb.Box(Vector3.zero, new Vector3(0.16f, 0.12f, 0.005f));
                    mb.Set(S.Velvet, new Color(0.35f, 0.28f, 0.2f));
                    foreach (float s in new[] { -1f, 1f }) { mb.Ellipsoid(new Vector3(s * 0.035f, 0.012f, 0.005f), new Vector3(0.035f, 0.028f, 0.003f), 8, 4); mb.Ellipsoid(new Vector3(s * 0.028f, -0.025f, 0.005f), new Vector3(0.022f, 0.02f, 0.003f), 8, 4); }
                    mb.Set(S.Obsidian, Color.white); mb.Ellipsoid(new Vector3(0, 0, 0.007f), new Vector3(0.007f, 0.03f, 0.006f), 6, 4);
                    mb.Set(S.GlossPaint, new Color(0.8f, 0.7f, 0.3f)); foreach (float s in new[] { -1f, 1f }) mb.Sphere(new Vector3(s * 0.038f, 0.015f, 0.009f), 0.007f, 6, 4);
                    mb.Pop();
                    break;
                default: // a small grey hand, palm up
                    mb.Set(S.Flesh, new Color(0.62f, 0.55f, 0.5f));
                    mb.Ellipsoid(c + new Vector3(0, 0.03f, 0), new Vector3(0.035f, 0.015f, 0.04f), 10, 6);
                    for (int k = 0; k < 4; k++) mb.Tube(new List<Vector3> { c + new Vector3(-0.024f + k * 0.016f, 0.035f, 0.035f), c + new Vector3(-0.026f + k * 0.017f, 0.05f, 0.06f), c + new Vector3(-0.028f + k * 0.018f, 0.075f, 0.066f) }, 0.006f, 5, true);
                    mb.Tube(new List<Vector3> { c + new Vector3(0.035f, 0.03f, 0.0f), c + new Vector3(0.055f, 0.045f, 0.02f) }, 0.007f, 5, true);
                    mb.Set(S.Flesh, new Color(0.5f, 0.3f, 0.3f)); mb.Cyl(c + new Vector3(0, 0.015f, -0.04f), 0.022f, 0.02f, 8);
                    break;
            }
            // the dome
            mb.Set(S.Glass, Color.white);
            mb.Lathe(new[] { new Vector2(0.105f, 0.04f), new Vector2(0.105f, 0.2f), new Vector2(0.09f, 0.26f), new Vector2(0.05f, 0.29f), new Vector2(0.001f, 0.3f) }, 18);
            mb.Set(S.Brass, Color.white); mb.Sphere(new Vector3(0, 0.305f, 0), 0.012f, 8, 5);
            return mb;
        }

        /// <summary>Anatomical specimen jars: glass, a sunken pale thing, a yellowed liquid line, corks and labels.</summary>
        static MeshBuilder SpecimenJarsMesh(int v)
        {
            var mb = new MeshBuilder();
            var rnd = new System.Random(v * 41 + 9);
            int n = 2 + v % 2;
            for (int i = 0; i < n; i++)
            {
                float r = 0.045f + (float)rnd.NextDouble() * 0.03f, h = 0.16f + (float)rnd.NextDouble() * 0.12f;
                var o = new Vector3((i - (n - 1) * 0.5f) * 0.13f, 0, (i % 2) * 0.05f - 0.02f);
                mb.Push(o, (float)rnd.NextDouble() * 360f);
                mb.Set(S.Flesh, Color.Lerp(new Color(0.85f, 0.75f, 0.6f), new Color(0.7f, 0.4f, 0.4f), (float)rnd.NextDouble()));
                int kind = (i + v) % 3;
                if (kind == 0)
                {
                    var pts = new List<Vector3>();
                    for (int k = 0; k <= 10; k++) { float t = k / 10f; pts.Add(new Vector3(Mathf.Cos(t * 9f) * r * 0.45f * (1 - t * 0.5f), 0.03f + t * h * 0.6f, Mathf.Sin(t * 9f) * r * 0.45f * (1 - t * 0.5f))); }
                    mb.Tube(pts, r * 0.2f, 6, true);
                }
                else if (kind == 1)
                {
                    mb.Ellipsoid(new Vector3(0, h * 0.4f, 0), new Vector3(r * 0.55f, h * 0.28f, r * 0.45f), 10, 7);
                    mb.Set(S.Flesh, new Color(0.5f, 0.15f, 0.2f));
                    mb.Tube(new List<Vector3> { new Vector3(0, h * 0.62f, 0), new Vector3(r * 0.2f, h * 0.72f, 0), new Vector3(r * 0.1f, h * 0.8f, r * 0.2f) }, r * 0.12f, 5);
                }
                else
                {
                    mb.Sphere(new Vector3(0, h * 0.35f, 0), r * 0.5f, 10, 7);
                    mb.Set(S.GlossPaint, new Color(0.15f, 0.25f, 0.2f));
                    mb.Push(new Vector3(0, h * 0.35f, r * 0.48f), Quaternion.Euler(-90, 0, 0), Vector3.one); mb.Disc(Vector3.zero, r * 0.2f, 10, true); mb.Pop();
                }
                mb.Set(S.GlossPaint, new Color(0.62f, 0.58f, 0.3f)); mb.Disc(new Vector3(0, h * 0.85f, 0), r * 0.96f, 14, true);
                mb.Set(S.Glass, Color.white); mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(r, 0.002f), new Vector2(r, h), new Vector2(r * 0.8f, h + 0.012f) }, 14);
                mb.Set(S.WoodWorn, new Color(0.7f, 0.55f, 0.4f)); mb.Cyl(new Vector3(0, h + 0.005f, 0), r * 0.82f, 0.03f, 12);
                mb.Set(S.Paper, new Color(0.88f, 0.82f, 0.66f)); mb.Lathe(new[] { new Vector2(r + 0.001f, h * 0.35f), new Vector2(r + 0.001f, h * 0.55f) }, 14, false, false, -50f, 50f);
                mb.Pop();
            }
            return mb;
        }

        /// <summary>Flowers left too long: bent brown stems, heads hanging.</summary>
        static MeshBuilder WiltedBouquet(int v)
        {
            var mb = new MeshBuilder();
            var rnd = new System.Random(v * 53 + 7);
            int n = 6 + v;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2 + (float)rnd.NextDouble() * 0.5f;
                float lean = 0.35f + (float)rnd.NextDouble() * 0.4f, len = 0.2f + (float)rnd.NextDouble() * 0.12f;
                var dir = new Vector3(Mathf.Cos(a) * lean, 1f, Mathf.Sin(a) * lean).normalized;
                var mid = dir * len * 0.7f; var tip = mid + new Vector3(Mathf.Cos(a) * 0.08f, -0.09f - (float)rnd.NextDouble() * 0.07f, Mathf.Sin(a) * 0.08f);
                mb.Set(S.Leaf, new Color(0.3f, 0.26f, 0.12f));
                mb.Tube(new List<Vector3> { Vector3.zero, mid, tip }, 0.0035f, 4);
                mb.Set(S.Velvet, Color.Lerp(new Color(0.3f, 0.12f, 0.1f), new Color(0.35f, 0.3f, 0.22f), (float)rnd.NextDouble()));
                mb.Sphere(tip, 0.022f, 7, 5, 0.7f);
            }
            return mb;
        }

        /// <summary>Glazed urn in a palette colour, one of four profiles.</summary>
        static MeshBuilder UrnMesh(int v, Color glaze, Color trim)
        {
            var mb = new MeshBuilder();
            mb.Set(S.Porcelain, glaze);
            Vector2[] prof;
            switch (v % 4)
            {
                case 0: prof = new[] { new Vector2(0.05f, 0), new Vector2(0.06f, 0.02f), new Vector2(0.03f, 0.04f), new Vector2(0.1f, 0.12f), new Vector2(0.11f, 0.2f), new Vector2(0.06f, 0.3f), new Vector2(0.05f, 0.34f), new Vector2(0.07f, 0.36f) }; break;
                case 1: prof = new[] { new Vector2(0.06f, 0), new Vector2(0.12f, 0.08f), new Vector2(0.13f, 0.14f), new Vector2(0.07f, 0.22f), new Vector2(0.05f, 0.26f), new Vector2(0.06f, 0.28f) }; break;
                case 2: prof = new[] { new Vector2(0.04f, 0), new Vector2(0.05f, 0.1f), new Vector2(0.08f, 0.3f), new Vector2(0.075f, 0.42f), new Vector2(0.03f, 0.47f), new Vector2(0.035f, 0.5f) }; break;
                default: prof = new[] { new Vector2(0.07f, 0), new Vector2(0.09f, 0.03f), new Vector2(0.08f, 0.06f), new Vector2(0.14f, 0.12f), new Vector2(0.12f, 0.18f), new Vector2(0.13f, 0.2f) }; break;
            }
            mb.Lathe(prof, 16, true);
            mb.Set(S.Gold, trim);
            float top = prof[prof.Length - 1].y, rr = prof[prof.Length - 1].x;
            mb.Torus(new Vector3(0, top, 0), rr, 0.006f, 16, 4);
            if (v % 4 == 0) foreach (float s in new[] { -1f, 1f }) mb.Tube(new List<Vector3> { new Vector3(s * 0.055f, 0.3f, 0), new Vector3(s * 0.12f, 0.29f, 0), new Vector3(s * 0.1f, 0.2f, 0) }, 0.008f, 5);
            return mb;
        }

        static MeshBuilder CrowMesh()
        {
            var mb = new MeshBuilder();
            mb.Set(S.WoodDark, Color.white); mb.Cyl(Vector3.zero, 0.06f, 0.025f, 12);
            mb.Set(S.Velvet, new Color(0.04f, 0.04f, 0.05f));
            mb.Ellipsoid(new Vector3(0, 0.11f, 0), new Vector3(0.05f, 0.06f, 0.09f), 12, 8);
            mb.Sphere(new Vector3(0, 0.18f, 0.07f), 0.035f, 10, 7);
            mb.Ellipsoid(new Vector3(0, 0.09f, -0.11f), new Vector3(0.028f, 0.012f, 0.07f), 8, 4);
            foreach (float s in new[] { -1f, 1f }) mb.Ellipsoid(new Vector3(s * 0.045f, 0.12f, -0.02f), new Vector3(0.012f, 0.04f, 0.08f), 8, 4);
            mb.Set(S.Obsidian, Color.white); mb.Push(new Vector3(0, 0.175f, 0.1f), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Lathe(new[] { new Vector2(0.012f, 0), new Vector2(0.001f, 0.05f) }, 6); mb.Pop();
            mb.Set(S.GlossPaint, new Color(0.7f, 0.6f, 0.2f)); foreach (float s in new[] { -1f, 1f }) mb.Sphere(new Vector3(s * 0.025f, 0.19f, 0.09f), 0.006f, 5, 3);
            mb.Set(S.Iron, new Color(0.2f, 0.2f, 0.2f)); foreach (float s in new[] { -1f, 1f }) mb.Rod(new Vector3(s * 0.015f, 0.025f, 0), new Vector3(s * 0.015f, 0.06f, 0), 0.004f, 4, false);
            return mb;
        }

        static MeshBuilder DecanterMesh(int v)
        {
            var mb = new MeshBuilder();
            Color wine = v == 0 ? new Color(0.25f, 0.03f, 0.05f) : new Color(0.45f, 0.25f, 0.06f);
            mb.Set(S.GlossPaint, wine);
            mb.Lathe(new[] { new Vector2(0.001f, 0.006f), new Vector2(0.07f, 0.01f), new Vector2(0.078f, 0.06f), new Vector2(0.05f, 0.1f), new Vector2(0.001f, 0.105f) }, 14);
            mb.Set(S.Glass, Color.white);
            mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.075f, 0.004f), new Vector2(0.085f, 0.06f), new Vector2(0.06f, 0.14f), new Vector2(0.022f, 0.19f), new Vector2(0.02f, 0.25f), new Vector2(0.03f, 0.26f) }, 14);
            mb.Lathe(new[] { new Vector2(0.025f, 0.26f), new Vector2(0.035f, 0.29f), new Vector2(0.001f, 0.31f) }, 10);
            return mb;
        }

        /// <summary>A mess of stubby candles on a saucer, wax run down in long tongues; flames baked in.</summary>
        static MeshBuilder CandleMessMesh(int v)
        {
            var mb = new MeshBuilder();
            var rnd = new System.Random(v * 23 + 5);
            mb.Set(S.Brass, new Color(0.7f, 0.6f, 0.45f));
            mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.11f, 0.002f), new Vector2(0.13f, 0.014f) }, 16);
            mb.Set(S.Wax, new Color(0.92f, 0.86f, 0.75f));
            mb.Lathe(new[] { new Vector2(0.001f, 0.012f), new Vector2(0.09f, 0.012f), new Vector2(0.115f, 0.004f) }, 14);
            var flames = new List<Vector3>();
            int n = 3 + v % 3;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2 + (float)rnd.NextDouble(), rr = 0.02f + (float)rnd.NextDouble() * 0.05f;
                var p = new Vector3(Mathf.Cos(a) * rr, 0.012f, Mathf.Sin(a) * rr);
                float h = 0.05f + (float)rnd.NextDouble() * 0.16f, r = 0.014f + (float)rnd.NextDouble() * 0.012f;
                FurnitureFactory.Candle(mb, p, h, r, -2, p + Vector3.up * (h + 0.006f), flames);
                mb.Set(S.Wax, new Color(0.9f, 0.84f, 0.72f));
                mb.Tube(new List<Vector3> { p + new Vector3(r, h * 0.7f, 0), p + new Vector3(r * 1.3f, h * 0.3f, 0), p + new Vector3(r * 2.2f, 0.004f, 0) }, 0.0045f, 4, true);
            }
            foreach (var f in flames) FlameQuad(mb, f, 0.05f, -2);
            return mb;
        }

        static MeshBuilder CardsMesh()
        {
            var mb = new MeshBuilder();
            var rnd = new System.Random(77);
            for (int i = 0; i < 9; i++)
            {
                mb.Push(new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.3f, 0.001f + i * 0.0008f, ((float)rnd.NextDouble() - 0.5f) * 0.2f), (float)rnd.NextDouble() * 360f);
                mb.Set(S.Paper, new Color(0.93f, 0.9f, 0.84f)); mb.Box(Vector3.zero, new Vector3(0.063f, 0.0006f, 0.088f), MeshBuilder.Faces.PY);
                mb.Set(S.GlossPaint, i % 2 == 0 ? new Color(0.6f, 0.05f, 0.07f) : new Color(0.06f, 0.05f, 0.06f));
                mb.Box(new Vector3(0, 0.0004f, 0), new Vector3(0.02f, 0.0003f, 0.026f), MeshBuilder.Faces.PY);
                mb.Pop();
            }
            mb.Set(S.Paper, new Color(0.35f, 0.08f, 0.1f)); mb.Box(new Vector3(0.12f, 0.009f, 0.06f), new Vector3(0.063f, 0.018f, 0.088f));
            return mb;
        }

        static MeshBuilder MetronomeMesh()
        {
            var mb = new MeshBuilder();
            mb.Set(S.WoodCherry, Color.white);
            var a = new Vector3(-0.055f, 0, -0.045f); var b = new Vector3(0.055f, 0, -0.045f); var c = new Vector3(0.055f, 0, 0.045f); var d = new Vector3(-0.055f, 0, 0.045f); var t = new Vector3(0, 0.22f, 0);
            mb.TriAuto(a, b, t, Vector3.back); mb.TriAuto(b, c, t, Vector3.right); mb.TriAuto(c, d, t, Vector3.forward); mb.TriAuto(d, a, t, Vector3.left);
            mb.Set(S.Steel, Color.white); mb.Bar(new Vector3(0, 0.03f, 0.046f), new Vector3(0.03f, 0.2f, 0.046f), 0.004f);
            mb.Set(S.Brass, Color.white); mb.Box(new Vector3(0.02f, 0.14f, 0.048f), new Vector3(0.018f, 0.02f, 0.008f));
            return mb;
        }

        /// <summary>Folded towels, one to three in a stack, in faded bath colours.</summary>
        static MeshBuilder TowelsMesh(int v)
        {
            var mb = new MeshBuilder();
            Color[] cols = { new Color(0.85f, 0.83f, 0.78f), new Color(0.35f, 0.45f, 0.5f), new Color(0.6f, 0.25f, 0.25f), new Color(0.82f, 0.8f, 0.72f) };
            int n = 1 + v % 3; float y = 0;
            for (int i = 0; i < n; i++) { mb.Set(S.Linen, cols[(v + i) % cols.Length]); mb.BevelBox(new Vector3(0.01f * i, y + 0.03f, -0.01f * i), new Vector3(0.4f, 0.06f, 0.26f), 0.025f); y += 0.058f; }
            return mb;
        }

        static MeshBuilder DollPropMesh(int v, MansionPalette pal)
        {
            var mb = new MeshBuilder();
            FurnitureFactory.Doll(mb, Vector3.zero, 0.26f + (v % 3) * 0.04f, new System.Random(v * 19 + 3), pal, v % 4 == 3);
            return mb;
        }
    }
}
