using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// A resident's own bed and desk (OwnerStyles): the bed style and how it is made (a spotless funeral director's taut
    /// sheets, a rapper's kicked-off duvet), and a desk top laid out for their work — the gamer's monitors, the
    /// journalist's typewriter, the restorer's brushes in a row. Chosen by owner id only, never by seed.
    /// </summary>
    internal static partial class FurnitureFactory
    {
        internal static OwnerLook OwnerOf(Ctx c) => c.Rv != null && c.Rv.Room.Type == RoomType.Bedroom ? OwnerStyles.Get(c.Rv.Room.Owner) : null;

        /// <summary>Mattress top (m above the floor) of an owner bed style: where things lie on the bed.</summary>
        internal static float OwnerBedTop(int style) => style == 1 ? 0.62f : style == 2 ? 0.55f : style == 3 ? 0.44f : 0.6f;

        // ------------------------------------------------------------------ beds
        static void OwnerBed(Ctx c, MeshBuilder mb, OwnerLook own)
        {
            float W = c.W, D = c.D; Color wood = c.WoodC; float top = OwnerBedTop(own.Bed);
            Color sheet = new Color(0.86f, 0.83f, 0.76f);
            switch (own.Bed)
            {
                case 1:
                    {
                        // four-poster with a cloth canopy, the drapes gathered and tied at the posts
                        mb.Set(S.WoodDark, wood);
                        mb.BevelBox(new Vector3(0, 0.27f, 0), new Vector3(W, 0.22f, D), 0.02f);
                        float ch = 2.25f;
                        foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) TurnedLeg(mb, new Vector3(x * (W / 2 - 0.04f), 0, z * (D / 2 - 0.04f)), ch, 0.035f);
                        mb.Box(new Vector3(0, ch, -D / 2 + 0.04f), new Vector3(W, 0.07f, 0.06f)); mb.Box(new Vector3(0, ch, D / 2 - 0.04f), new Vector3(W, 0.07f, 0.06f));
                        mb.Box(new Vector3(-W / 2 + 0.04f, ch, 0), new Vector3(0.06f, 0.07f, D)); mb.Box(new Vector3(W / 2 - 0.04f, ch, 0), new Vector3(0.06f, 0.07f, D));
                        mb.Set(S.Velvet, Color.Lerp(c.Fabric, Color.black, 0.3f));
                        mb.BevelBox(new Vector3(0, 0.98f, -D / 2 + 0.07f), new Vector3(W - 0.14f, 0.95f, 0.08f), 0.04f);
                        var cloth = Color.Lerp(c.Fabric, c.Pal.Wall, 0.25f);
                        mb.Set(S.Velvet, cloth);
                        mb.Box(new Vector3(0, ch + 0.04f, 0), new Vector3(W + 0.02f, 0.012f, D + 0.02f));
                        // scalloped valance round the top
                        for (int s = 0; s < 4; s++)
                        {
                            bool alongX = s < 2; float len = alongX ? W : D; int n = Mathf.Max(3, (int)(len / 0.35f));
                            for (int k = 0; k < n; k++)
                            {
                                float t = (k + 0.5f) / n - 0.5f;
                                var p = alongX ? new Vector3(t * len, ch - 0.1f, (s == 0 ? -1 : 1) * (D / 2 + 0.005f)) : new Vector3((s == 2 ? -1 : 1) * (W / 2 + 0.005f), ch - 0.1f, t * len);
                                var sz = alongX ? new Vector3(len / n - 0.01f, 0.2f + (k % 2) * 0.04f, 0.012f) : new Vector3(0.012f, 0.2f + (k % 2) * 0.04f, len / n - 0.01f);
                                mb.Box(p, sz);
                            }
                        }
                        // drapes gathered at the four posts, with a gold tie-back
                        foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                            {
                                var bp = new Vector3(x * (W / 2 - 0.1f), 0.02f, z * (D / 2 - 0.1f));
                                mb.Set(S.Velvet, cloth);
                                mb.Push(bp, 0); mb.Lathe(new[] { new Vector2(0.1f, 0), new Vector2(0.075f, 0.5f), new Vector2(0.045f, 1.05f), new Vector2(0.06f, 1.3f), new Vector2(0.1f, ch - 0.25f) }, 9); mb.Pop();
                                mb.Set(S.Gold, c.Pal.Trim); mb.Push(bp + Vector3.up * 1.05f, 0); mb.Torus(Vector3.zero, 0.05f, 0.012f, 10, 4); mb.Pop();
                            }
                        mb.Set(S.Linen, sheet);
                        mb.BevelBox(new Vector3(0, top - 0.12f, 0.02f), new Vector3(W - 0.1f, 0.24f, D - 0.16f), 0.06f);
                        break;
                    }
                case 2:
                    {
                        // an iron cot: tube posts, barred head and foot, brass knobs, a thin ticking mattress on springs
                        var iron = new Color(0.13f, 0.13f, 0.13f);
                        mb.Set(S.Iron, iron);
                        float hx = W / 2 - 0.03f, hz = D / 2 - 0.03f;
                        foreach (float x in new[] { -hx, hx }) { mb.Rod(new Vector3(x, 0, -hz), new Vector3(x, 1.15f, -hz), 0.02f, 8); mb.Rod(new Vector3(x, 0, hz), new Vector3(x, 0.85f, hz), 0.02f, 8); mb.Rod(new Vector3(x, 0.36f, -hz), new Vector3(x, 0.36f, hz), 0.015f, 6); }
                        foreach (var (zz, hh) in new[] { (-hz, 1.1f), (hz, 0.8f) })
                        {
                            var arch = new List<Vector3>();
                            for (int k = 0; k <= 10; k++) { float t = k / 10f; arch.Add(new Vector3(Mathf.Lerp(-hx, hx, t), hh - 0.08f + Mathf.Sin(t * Mathf.PI) * 0.08f, zz)); }
                            mb.Tube(arch, 0.014f, 6);
                            mb.Rod(new Vector3(-hx, 0.42f, zz), new Vector3(hx, 0.42f, zz), 0.012f, 6);
                            for (float x = -hx + 0.12f; x < hx - 0.06f; x += 0.12f) mb.Rod(new Vector3(x, 0.42f, zz), new Vector3(x, hh - 0.08f + Mathf.Sin(Mathf.InverseLerp(-hx, hx, x) * Mathf.PI) * 0.08f, zz), 0.008f, 5, false);
                        }
                        mb.Set(S.Brass, new Color(0.7f, 0.56f, 0.34f));
                        foreach (float x in new[] { -hx, hx }) { mb.Sphere(new Vector3(x, 1.17f, -hz), 0.032f, 8, 6); mb.Sphere(new Vector3(x, 0.87f, hz), 0.032f, 8, 6); }
                        mb.Set(S.Iron, iron * 0.8f); mb.Box(new Vector3(0, 0.37f, 0), new Vector3(W - 0.08f, 0.02f, D - 0.08f));
                        mb.Set(S.Linen, new Color(0.78f, 0.76f, 0.7f));
                        mb.BevelBox(new Vector3(0, top - 0.08f, 0), new Vector3(W - 0.1f, 0.16f, D - 0.1f), 0.05f);
                        break;
                    }
                case 3:
                    {
                        // a low platform, the mattress straight on it, a slatted headboard leaning on the wall
                        mb.Set(S.WoodWorn, Color.Lerp(wood, new Color(0.55f, 0.45f, 0.35f), 0.4f));
                        mb.BevelBox(new Vector3(0, 0.1f, 0), new Vector3(W, 0.2f, D), 0.015f);
                        for (int k = 0; k < 6; k++) { float x = (k - 2.5f) / 6f * (W - 0.1f); mb.Box(new Vector3(x, 0.55f, -D / 2 + 0.04f), new Vector3((W - 0.1f) / 6f - 0.04f, 0.7f, 0.04f)); }
                        mb.Box(new Vector3(0, 0.92f, -D / 2 + 0.04f), new Vector3(W, 0.06f, 0.06f));
                        mb.Set(S.Linen, sheet);
                        mb.BevelBox(new Vector3(0, top - 0.12f, 0.02f), new Vector3(W - 0.1f, 0.24f, D - 0.14f), 0.07f);
                        break;
                    }
                default:
                    {
                        // a sleigh bed: scrolled head and foot boards bent outward, side rails between them
                        mb.Set(S.WoodCherry, wood);
                        float hx = W / 2;
                        foreach (var (z0, zs, hh) in new[] { (-D / 2 + 0.06f, -1f, 1.2f), (D / 2 - 0.06f, 1f, 0.78f) })
                        {
                            var prof = new List<Vector2>();
                            for (int k = 0; k <= 8; k++) { float t = k / 8f; prof.Add(new Vector2(z0 + zs * (Mathf.Pow(t, 2.2f) * 0.14f), 0.12f + t * (hh - 0.12f))); }
                            for (int k = 0; k < prof.Count - 1; k++)
                            {
                                var a = prof[k]; var b = prof[k + 1];
                                mb.QuadAuto(new Vector3(-hx, a.y, a.x), new Vector3(-hx, b.y, b.x), new Vector3(hx, b.y, b.x), new Vector3(hx, a.y, a.x), new Vector3(0, 0, -zs));
                                mb.QuadAuto(new Vector3(-hx, a.y, a.x + 0.03f * -zs), new Vector3(-hx, b.y, b.x + 0.03f * -zs), new Vector3(hx, b.y, b.x + 0.03f * -zs), new Vector3(hx, a.y, a.x + 0.03f * -zs), new Vector3(0, 0, zs));
                            }
                            var tp = prof[prof.Count - 1];
                            mb.Rod(new Vector3(-hx - 0.02f, tp.y, tp.x + zs * 0.03f), new Vector3(hx + 0.02f, tp.y, tp.x + zs * 0.03f), 0.065f, 12);
                            foreach (float x in new[] { -hx, hx }) mb.Box(new Vector3(x - Mathf.Sign(x) * 0.02f, (hh + 0.12f) * 0.5f, z0), new Vector3(0.05f, hh - 0.12f, 0.08f));
                            foreach (float x in new[] { -hx + 0.05f, hx - 0.05f }) mb.Box(new Vector3(x, 0.06f, z0), new Vector3(0.08f, 0.12f, 0.08f));
                        }
                        foreach (float x in new[] { -hx + 0.03f, hx - 0.03f }) mb.BevelBox(new Vector3(x, 0.3f, 0), new Vector3(0.05f, 0.26f, D - 0.12f), 0.01f);
                        mb.Set(S.Linen, sheet);
                        mb.BevelBox(new Vector3(0, top - 0.13f, 0), new Vector3(W - 0.1f, 0.26f, D - 0.2f), 0.06f);
                        break;
                    }
            }
            OwnerBedding(c, mb, own, top);
        }

        /// <summary>How the bed is made, from spotless to kicked apart (own.Clutter), in the resident's colours.</summary>
        internal static void OwnerBedding(Ctx c, MeshBuilder mb, OwnerLook own, float top)
        {
            float W = c.W, D = c.D, cl = own.Clutter;
            var rnd = new System.Random(OwnerStyles.Seed(own.Id));
            Color sheet = new Color(0.88f, 0.86f, 0.8f);
            Color duvet = Color.Lerp(c.Fabric, c.Pal.Wall, 0.15f);
            Color band = Color.Lerp(own.Sig, c.Fabric, 0.2f);   // the signature colour (notebook map) on the throw
            float y = top;
            float head = -D / 2 + (own.Bed == 4 ? 0.16f : 0.1f);
            // pillows
            mb.Set(S.Linen, own.Id == "P14" ? new Color(0.92f, 0.9f, 0.86f) : Color.Lerp(sheet, duvet, 0.18f));
            if (cl < 0.3f)
            {
                foreach (float x in new[] { -W * 0.22f, W * 0.22f }) mb.BevelBox(new Vector3(x, y + 0.07f, head + 0.24f), new Vector3(W * 0.4f, 0.13f, 0.34f), 0.06f);
            }
            else
            {
                mb.Push(new Vector3(-W * 0.2f, y + 0.07f, head + 0.26f), Quaternion.Euler(0, -8f - cl * 12f, 0), Vector3.one); mb.BevelBox(Vector3.zero, new Vector3(W * 0.4f, 0.13f, 0.34f), 0.06f); mb.Pop();
                mb.Push(new Vector3(W * 0.18f, y + 0.09f + cl * 0.03f, head + 0.3f + cl * 0.1f), Quaternion.Euler(cl * 18f, 14f + cl * 20f, cl * 10f), Vector3.one); mb.BevelBox(Vector3.zero, new Vector3(W * 0.4f, 0.13f, 0.34f), 0.06f); mb.Pop();
            }
            if (cl < 0.3f)
            {
                // made: the duvet squared to the mattress, its hem hanging level both sides, the sheet turned down over it
                // at the head and a throw folded at the foot in the accent colour
                float z0 = head + 0.5f, z1 = D / 2 - 0.02f;
                mb.Set(S.Velvet, duvet);
                mb.Box(new Vector3(0, y + 0.03f, (z0 + z1) / 2), new Vector3(W - 0.04f, 0.05f, z1 - z0));
                foreach (float s in new[] { -1f, 1f }) mb.Box(new Vector3(s * (W / 2 - 0.01f), y - 0.1f, (z0 + z1) / 2), new Vector3(0.02f, 0.26f, z1 - z0));
                mb.Set(S.Linen, sheet); mb.Box(new Vector3(0, y + 0.058f, z0 + 0.12f), new Vector3(W - 0.04f, 0.012f, 0.24f));
                mb.Set(S.Velvet, band); mb.Box(new Vector3(0, y + 0.07f, D / 2 - 0.34f), new Vector3(W - 0.02f, 0.035f, 0.42f));
                foreach (float s in new[] { -1f, 1f }) mb.Box(new Vector3(s * (W / 2 + 0.005f), y - 0.06f, D / 2 - 0.34f), new Vector3(0.02f, 0.22f, 0.42f));
            }
            else if (cl < 0.65f)
            {
                // slept in and pulled up: a soft slab with a couple of folds and one corner turned back
                float z0 = head + 0.46f, z1 = D / 2 - 0.02f;
                mb.Set(S.Velvet, duvet);
                mb.Box(new Vector3(0, y + 0.035f, (z0 + z1) / 2), new Vector3(W - 0.02f, 0.06f, z1 - z0));
                foreach (float s in new[] { -1f, 1f }) mb.Box(new Vector3(s * (W / 2 + 0.005f), y - 0.1f, (z0 + z1) / 2 + s * 0.05f), new Vector3(0.02f, 0.28f, z1 - z0 - 0.1f));
                for (int k = 0; k < 3; k++) mb.Ellipsoid(new Vector3((float)(rnd.NextDouble() - 0.5) * W * 0.5f, y + 0.06f, Mathf.Lerp(z0 + 0.3f, z1 - 0.3f, k / 2f)), new Vector3(0.28f, 0.05f, 0.2f), 10, 5);
                mb.Set(S.Linen, sheet);
                mb.Tri(new Vector3(W / 2 - 0.02f, y + 0.07f, z0), new Vector3(W / 2 - 0.45f, y + 0.07f, z0), new Vector3(W / 2 - 0.02f, y + 0.07f, z0 + 0.45f));
                mb.Set(S.Velvet, band); mb.Box(new Vector3(0, y + 0.075f, D / 2 - 0.26f), new Vector3(W * 0.7f, 0.03f, 0.3f));
            }
            else
            {
                // kicked apart: the duvet heaped toward the foot and sliding off one side, the sheet rucked, clothes on top
                mb.Set(S.Linen, sheet);
                for (int k = 0; k < 3; k++) mb.Ellipsoid(new Vector3((k - 1) * W * 0.25f, y + 0.02f, head + 0.7f + k * 0.12f), new Vector3(0.22f, 0.025f, 0.3f), 8, 4);
                mb.Set(S.Velvet, duvet);
                float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                for (int k = 0; k < 6; k++)
                {
                    var p = new Vector3(side * (0.05f + (float)rnd.NextDouble() * W * 0.35f), y + 0.08f + (float)rnd.NextDouble() * 0.06f, Mathf.Lerp(head + 0.9f, D / 2 - 0.25f, (float)rnd.NextDouble()));
                    mb.Ellipsoid(p, new Vector3(0.3f + (float)rnd.NextDouble() * 0.15f, 0.08f + (float)rnd.NextDouble() * 0.05f, 0.26f), 10, 6);
                }
                mb.Box(new Vector3(side * (W / 2 + 0.01f), y - 0.2f, D * 0.12f), new Vector3(0.025f, 0.42f, D * 0.55f));
                mb.Ellipsoid(new Vector3(side * (W / 2 + 0.02f), y - 0.02f, D * 0.12f), new Vector3(0.06f, 0.1f, D * 0.28f), 8, 5);
                mb.Set(S.Cloth, Color.Lerp(c.Pal.Accent2, new Color(0.2f, 0.2f, 0.2f), 0.5f));
                mb.Ellipsoid(new Vector3(-side * W * 0.2f, y + 0.07f, D / 2 - 0.45f), new Vector3(0.24f, 0.06f, 0.18f), 9, 5);
                mb.Ellipsoid(new Vector3(-side * W * 0.1f, y + 0.1f, D / 2 - 0.36f), new Vector3(0.14f, 0.05f, 0.12f), 8, 4);
            }
        }

        // ------------------------------------------------------------------ desks
        /// <summary>The desk top laid out for the resident's work (low pieces in the middle, tall ones at the back corners so
        /// the kernel's personal items still find a place to lie).</summary>
        static void OwnerDeskTop(Ctx c, MeshBuilder mb, OwnerLook own)
        {
            float W = c.W, D = c.D, H = c.H; var pal = c.Pal;
            var rnd = new System.Random(OwnerStyles.Seed(own.Id) + 5);
            Vector3 T(float x, float z) => new Vector3(x, H, z);
            void Sheet(float x, float z, float yaw, Color col, float w = 0.21f, float d = 0.29f) { mb.Set(S.Paper, col); mb.Push(T(x, z) + Vector3.up * 0.002f, yaw); mb.Box(Vector3.zero, new Vector3(w, 0.003f, d)); mb.Pop(); }
            void BookFlat(float x, float z, float yaw, Color col, float th = 0.03f) { mb.Set(S.Leather, col); mb.Push(T(x, z), yaw); mb.BevelBox(new Vector3(0, th / 2, 0), new Vector3(0.17f, th, 0.24f), 0.004f); mb.Pop(); }
            void Pen(float x, float z, float yaw, Color col) { mb.Set(S.GlossPaint, col); mb.Push(T(x, z) + Vector3.up * 0.006f, yaw); mb.Rod(new Vector3(-0.07f, 0, 0), new Vector3(0.07f, 0, 0), 0.005f, 5); mb.Pop(); }
            Color paper = new Color(0.93f, 0.9f, 0.82f);
            switch (own.Id)
            {
                case "P01":   // a hand-drawn walking map, a board-game box, bread in a paper bag
                    Sheet(-0.05f, 0.08f, 4f, paper, 0.42f, 0.3f);
                    mb.Set(S.Paper, new Color(0.35f, 0.25f, 0.2f)); for (int k = 0; k < 7; k++) mb.Box(T(-0.2f + k * 0.05f, 0.02f + Mathf.Sin(k) * 0.06f) + Vector3.up * 0.005f, new Vector3(0.025f, 0.002f, 0.006f));
                    mb.Set(S.Paper, new Color(0.62f, 0.2f, 0.16f)); mb.Box(T(W / 2 - 0.2f, -0.12f) + Vector3.up * 0.03f, new Vector3(0.3f, 0.06f, 0.22f));
                    mb.Set(S.Paper, new Color(0.72f, 0.58f, 0.4f)); mb.Box(T(-W / 2 + 0.14f, -0.16f) + Vector3.up * 0.1f, new Vector3(0.14f, 0.2f, 0.09f));
                    mb.Set(S.Clay, new Color(0.8f, 0.58f, 0.34f)); mb.Ellipsoid(T(-W / 2 + 0.14f, -0.16f) + Vector3.up * 0.22f, new Vector3(0.05f, 0.03f, 0.04f), 8, 5);
                    break;
                case "P02":   // notes on people: a pad of sketched faces, psychology books squared, a candy dish
                    Sheet(0.02f, 0.08f, -3f, paper);
                    mb.Set(S.Paper, new Color(0.3f, 0.3f, 0.32f)); for (int k = 0; k < 6; k++) { var p = T(-0.05f + (k % 3) * 0.06f, 0.0f + (k / 3) * 0.1f) + Vector3.up * 0.005f; mb.Disc(p, 0.02f, 10, true, 0.016f); }
                    BookFlat(W / 2 - 0.16f, -0.14f, 0, new Color(0.25f, 0.2f, 0.35f), 0.05f); BookFlat(W / 2 - 0.16f, -0.14f, 6f, new Color(0.45f, 0.12f, 0.15f), 0.1f);
                    mb.Set(S.Glass, Color.white); mb.Push(T(-W / 2 + 0.16f, -0.14f), 0); mb.Lathe(new[] { new Vector2(0.03f, 0), new Vector2(0.08f, 0.04f), new Vector2(0.085f, 0.05f) }, 12); mb.Pop();
                    for (int k = 0; k < 6; k++) { mb.Set(S.GlossPaint, Color.HSVToRGB(k / 6f, 0.7f, 0.85f)); mb.Sphere(T(-W / 2 + 0.16f + Mathf.Cos(k) * 0.04f, -0.14f + Mathf.Sin(k) * 0.04f) + Vector3.up * 0.03f, 0.014f, 6, 4); }
                    break;
                case "P03":   // everything squared to the edge: a planner, ruler, pens in a tray, a rota sheet
                    BookFlat(-0.1f, 0.06f, 0, new Color(0.14f, 0.2f, 0.36f), 0.025f);
                    Sheet(0.16f, 0.06f, 0, paper);
                    mb.Set(S.Paper, new Color(0.7f, 0.15f, 0.18f)); for (int k = 0; k < 4; k++) mb.Box(T(0.16f, -0.04f + k * 0.05f) + Vector3.up * 0.004f, new Vector3(0.16f, 0.001f, 0.004f));
                    mb.Set(S.WoodLight, Color.white); mb.Box(T(0, -0.2f) + Vector3.up * 0.004f, new Vector3(0.3f, 0.004f, 0.035f));
                    mb.Set(S.WoodDark, c.WoodC); mb.Box(T(-W / 2 + 0.18f, -0.18f) + Vector3.up * 0.015f, new Vector3(0.22f, 0.03f, 0.08f));
                    for (int k = 0; k < 4; k++) Pen(-W / 2 + 0.18f, -0.2f + k * 0.015f, 0, k == 0 ? new Color(0.7f, 0.1f, 0.1f) : new Color(0.1f, 0.12f, 0.2f));
                    break;
                case "P04":   // restoration: a white cotton cloth with brushes and scalpels laid in a perfect row
                    mb.Set(S.Linen, new Color(0.95f, 0.94f, 0.9f)); mb.Box(T(0, 0.05f) + Vector3.up * 0.002f, new Vector3(0.6f, 0.003f, 0.34f));
                    for (int k = 0; k < 7; k++)
                    {
                        float x = -0.24f + k * 0.08f;
                        mb.Set(k < 4 ? S.WoodLight : S.Steel, Color.white); mb.Rod(T(x, -0.06f) + Vector3.up * 0.008f, T(x, 0.14f) + Vector3.up * 0.008f, 0.005f, 5);
                        if (k < 4) { mb.Set(S.Cloth, new Color(0.2f, 0.15f, 0.1f)); mb.Rod(T(x, 0.14f) + Vector3.up * 0.008f, T(x, 0.18f) + Vector3.up * 0.008f, 0.007f, 5); }
                    }
                    mb.Set(S.Brass, Color.white); mb.Cyl(T(W / 2 - 0.12f, -0.18f), 0.06f, 0.02f, 12); mb.Rod(T(W / 2 - 0.12f, -0.18f) + Vector3.up * 0.02f, T(W / 2 - 0.2f, -0.1f) + Vector3.up * 0.3f, 0.008f, 6);
                    mb.Set(S.Glass, Color.white); mb.Push(T(W / 2 - 0.2f, -0.1f) + Vector3.up * 0.3f, Quaternion.Euler(60, 0, 0), Vector3.one); mb.Torus(Vector3.zero, 0.06f, 0.008f, 14, 4); mb.Disc(Vector3.zero, 0.058f, 14, true); mb.Pop();
                    break;
                case "P05":   // a leather blotter, a gold pen stand, a speech draft scored in red
                    mb.Set(S.Leather, new Color(0.18f, 0.28f, 0.28f)); mb.Box(T(0, 0.04f) + Vector3.up * 0.003f, new Vector3(0.56f, 0.006f, 0.36f));
                    Sheet(0.02f, 0.05f, -2f, paper);
                    mb.Set(S.GlossPaint, new Color(0.75f, 0.08f, 0.1f)); for (int k = 0; k < 5; k++) mb.Box(T(0.02f, -0.05f + k * 0.045f) + Vector3.up * 0.006f, new Vector3(0.12f - (k % 2) * 0.05f, 0.001f, 0.004f));
                    mb.Set(S.Gold, pal.Trim); mb.Box(T(-W / 2 + 0.18f, -0.18f) + Vector3.up * 0.015f, new Vector3(0.18f, 0.03f, 0.08f));
                    mb.Rod(T(-W / 2 + 0.14f, -0.18f) + Vector3.up * 0.03f, T(-W / 2 + 0.12f, -0.16f) + Vector3.up * 0.16f, 0.005f, 5);
                    mb.Rod(T(-W / 2 + 0.22f, -0.18f) + Vector3.up * 0.03f, T(-W / 2 + 0.24f, -0.16f) + Vector3.up * 0.16f, 0.005f, 5);
                    break;
                case "P06":   // an open ledger, rubber stamps, an abacus, a bar of bitter chocolate
                    mb.Set(S.Paper, paper); mb.Push(T(0, 0.05f), 0); mb.Box(new Vector3(-0.11f, 0.012f, 0), new Vector3(0.21f, 0.02f, 0.3f)); mb.Box(new Vector3(0.11f, 0.012f, 0), new Vector3(0.21f, 0.02f, 0.3f)); mb.Pop();
                    mb.Set(S.Paper, new Color(0.3f, 0.35f, 0.3f)); for (int k = 0; k < 8; k++) { mb.Box(T(-0.11f, -0.09f + k * 0.035f) + Vector3.up * 0.023f, new Vector3(0.17f, 0.001f, 0.003f)); mb.Box(T(0.11f, -0.09f + k * 0.035f) + Vector3.up * 0.023f, new Vector3(0.17f, 0.001f, 0.003f)); }
                    mb.Set(S.WoodDark, c.WoodC); mb.Box(T(W / 2 - 0.18f, -0.16f) + Vector3.up * 0.06f, new Vector3(0.26f, 0.12f, 0.02f));
                    mb.Set(S.Porcelain, new Color(0.85f, 0.8f, 0.7f)); for (int r = 0; r < 4; r++) for (int k = 0; k < 7; k++) mb.Sphere(T(W / 2 - 0.29f + k * 0.035f + (r % 2) * 0.02f, -0.16f) + Vector3.up * (0.02f + r * 0.028f), 0.009f, 6, 4);
                    for (int k = 0; k < 3; k++) { mb.Set(S.WoodLight, Color.white); mb.Cyl(T(-W / 2 + 0.12f + k * 0.06f, -0.18f), 0.015f, 0.07f, 8); mb.Set(S.Rubber, new Color(0.1f, 0.1f, 0.1f)); mb.Box(T(-W / 2 + 0.12f + k * 0.06f, -0.18f) + Vector3.up * 0.005f, new Vector3(0.045f, 0.01f, 0.03f)); }
                    mb.Set(S.Paper, new Color(0.18f, 0.1f, 0.06f)); mb.Box(T(0.3f, 0.18f) + Vector3.up * 0.006f, new Vector3(0.16f, 0.012f, 0.07f));
                    break;
                case "P07":   // a lyric notebook scrawled over, big headphones, bottle caps everywhere
                    Sheet(-0.06f, 0.06f, 12f, paper, 0.3f, 0.22f);
                    mb.Set(S.Paper, new Color(0.15f, 0.15f, 0.18f)); for (int k = 0; k < 7; k++) mb.Box(T(-0.06f, -0.02f + k * 0.025f) + Vector3.up * 0.005f, new Vector3(0.22f * (0.5f + (k * 37 % 10) / 20f), 0.001f, 0.004f));
                    mb.Set(S.Plastic, new Color(0.1f, 0.1f, 0.1f)); mb.Push(T(W / 2 - 0.2f, -0.1f) + Vector3.up * 0.04f, Quaternion.Euler(0, 0, 90), Vector3.one); mb.Torus(Vector3.zero, 0.09f, 0.012f, 14, 5, 0, 180); mb.Pop();
                    mb.Set(S.Leather, new Color(0.08f, 0.08f, 0.08f)); foreach (float s in new[] { -1f, 1f }) mb.Ellipsoid(T(W / 2 - 0.2f, -0.1f + s * 0.09f) + Vector3.up * 0.035f, new Vector3(0.045f, 0.035f, 0.02f), 8, 5);
                    mb.Set(S.Gold, pal.Trim); for (int k = 0; k < 9; k++) mb.Cyl(T((float)(rnd.NextDouble() - 0.5) * W * 0.8f, (float)(rnd.NextDouble() - 0.5) * D * 0.6f), 0.013f, 0.006f, 8);
                    break;
                case "P08":   // headphones, a coil of cable, a clip tuner, hand-ruled staff paper
                    Sheet(0.02f, 0.06f, -6f, paper);
                    mb.Set(S.Paper, new Color(0.2f, 0.2f, 0.22f)); for (int g = 0; g < 3; g++) for (int k = 0; k < 5; k++) mb.Box(T(0.02f, -0.06f + g * 0.08f + k * 0.008f) + Vector3.up * 0.005f, new Vector3(0.18f, 0.001f, 0.0015f));
                    mb.Set(S.Rubber, new Color(0.08f, 0.08f, 0.09f)); mb.Push(T(-W / 2 + 0.2f, -0.12f) + Vector3.up * 0.01f, Quaternion.identity, Vector3.one); for (int k = 0; k < 4; k++) mb.Torus(Vector3.up * k * 0.008f, 0.09f - k * 0.006f, 0.006f, 14, 4); mb.Pop();
                    mb.Set(S.Plastic, new Color(0.15f, 0.2f, 0.22f)); mb.Push(T(W / 2 - 0.2f, -0.12f) + Vector3.up * 0.04f, Quaternion.Euler(0, 20, 90), Vector3.one); mb.Torus(Vector3.zero, 0.085f, 0.012f, 14, 5, 0, 180); mb.Pop();
                    mb.Set(S.GlossPaint, new Color(0.9f, 0.45f, 0.1f)); mb.Box(T(0.28f, 0.18f) + Vector3.up * 0.01f, new Vector3(0.05f, 0.02f, 0.04f));
                    break;
                case "P09":   // a script with pages flagged, a highlighter, a pot of cold tea
                    mb.Set(S.Paper, paper); mb.Push(T(0, 0.06f), -5f); mb.BevelBox(new Vector3(0, 0.012f, 0), new Vector3(0.21f, 0.024f, 0.29f), 0.004f); mb.Pop();
                    for (int k = 0; k < 5; k++) { mb.Set(S.Paper, Color.HSVToRGB(0.1f + k * 0.18f, 0.6f, 0.9f)); mb.Box(T(0.11f, -0.05f + k * 0.04f) + Vector3.up * 0.015f, new Vector3(0.03f, 0.002f, 0.012f)); }
                    Pen(-0.18f, 0.12f, 30f, new Color(0.95f, 0.85f, 0.2f));
                    mb.Set(S.Porcelain, new Color(0.9f, 0.9f, 0.86f)); mb.Push(T(-W / 2 + 0.16f, -0.16f), 0); mb.Lathe(new[] { new Vector2(0.05f, 0), new Vector2(0.07f, 0.05f), new Vector2(0.05f, 0.1f), new Vector2(0.02f, 0.12f) }, 12, true); mb.Pop();
                    break;
                case "P10":   // a recipe notebook of exact weights, a kitchen scale, measuring spoons in a row, a board with herbs
                    Sheet(-0.12f, 0.06f, 0, paper);
                    mb.Set(S.Paper, new Color(0.25f, 0.25f, 0.25f)); for (int k = 0; k < 8; k++) mb.Box(T(-0.12f, -0.05f + k * 0.03f) + Vector3.up * 0.005f, new Vector3(0.14f, 0.001f, 0.003f));
                    mb.Set(S.Steel, Color.white); mb.Box(T(W / 2 - 0.18f, -0.14f) + Vector3.up * 0.02f, new Vector3(0.18f, 0.04f, 0.16f)); mb.Cyl(T(W / 2 - 0.18f, -0.14f) + Vector3.up * 0.04f, 0.08f, 0.006f, 16);
                    mb.Set(S.Glow, new Color(0.3f, 0.9f, 0.5f), MansionMats.GlowData(0.5f, 0, 0, c.Circuit)); mb.Box(T(W / 2 - 0.18f, -0.06f) + Vector3.up * 0.03f, new Vector3(0.08f, 0.02f, 0.002f));
                    for (int k = 0; k < 5; k++) { mb.Set(S.Steel, Color.white); mb.Rod(T(0.1f + k * 0.035f, 0.1f) + Vector3.up * 0.005f, T(0.1f + k * 0.035f, 0.2f) + Vector3.up * 0.005f, 0.004f, 4); mb.Sphere(T(0.1f + k * 0.035f, 0.08f) + Vector3.up * 0.008f, 0.01f + k * 0.002f, 8, 4, 0.5f); }
                    mb.Set(S.WoodLight, Color.white); mb.Box(T(0.12f, -0.1f) + Vector3.up * 0.01f, new Vector3(0.3f, 0.02f, 0.18f));
                    mb.Set(S.Leaf, new Color(0.3f, 0.55f, 0.25f)); for (int k = 0; k < 5; k++) mb.Ellipsoid(T(0.05f + k * 0.03f, -0.1f + (k % 2) * 0.03f) + Vector3.up * 0.025f, new Vector3(0.02f, 0.006f, 0.012f), 6, 3);
                    break;
                case "P11":   // a cutting mat of stripped clockwork, screwdrivers in a row, goggles
                    mb.Set(S.Rubber, new Color(0.14f, 0.28f, 0.22f)); mb.Box(T(0, 0.04f) + Vector3.up * 0.002f, new Vector3(0.6f, 0.004f, 0.4f));
                    mb.Set(S.Brass, Color.white); for (int k = 0; k < 9; k++) { var p = T((float)(rnd.NextDouble() - 0.5) * 0.5f, 0.04f + (float)(rnd.NextDouble() - 0.5) * 0.3f) + Vector3.up * 0.006f; mb.Push(p, (float)rnd.NextDouble() * 360); mb.Torus(Vector3.zero, 0.015f + (float)rnd.NextDouble() * 0.02f, 0.004f, 10, 3); mb.Pop(); }
                    mb.Set(S.Steel, Color.white); for (int k = 0; k < 6; k++) mb.Sphere(T(0.2f - k * 0.03f, 0.18f) + Vector3.up * 0.006f, 0.005f, 5, 3);
                    for (int k = 0; k < 5; k++) { mb.Set(S.Plastic, Color.HSVToRGB(0.05f * k, 0.8f, 0.8f)); mb.Rod(T(-W / 2 + 0.1f + k * 0.03f, -0.22f) + Vector3.up * 0.01f, T(-W / 2 + 0.1f + k * 0.03f, -0.14f) + Vector3.up * 0.01f, 0.009f, 6); mb.Set(S.Steel, Color.white); mb.Rod(T(-W / 2 + 0.1f + k * 0.03f, -0.14f) + Vector3.up * 0.01f, T(-W / 2 + 0.1f + k * 0.03f, -0.06f) + Vector3.up * 0.01f, 0.003f, 4); }
                    mb.Set(S.Leather, new Color(0.25f, 0.18f, 0.1f)); mb.Box(T(W / 2 - 0.2f, -0.18f) + Vector3.up * 0.01f, new Vector3(0.16f, 0.015f, 0.05f));
                    mb.Set(S.Glass, Color.white); foreach (float s in new[] { -1f, 1f }) mb.Cyl(T(W / 2 - 0.2f + s * 0.045f, -0.18f) + Vector3.up * 0.018f, 0.032f, 0.03f, 12);
                    break;
                case "P12":   // fan letters in a ribboned bundle, sticker sheets, a compact mirror
                    for (int k = 0; k < 6; k++) { mb.Set(S.Paper, Color.Lerp(new Color(0.98f, 0.85f, 0.9f), paper, k % 2)); mb.Push(T(-0.1f, 0.06f) + Vector3.up * (0.004f + k * 0.006f), (k - 3) * 4f); mb.Box(Vector3.zero, new Vector3(0.16f, 0.005f, 0.11f)); mb.Pop(); }
                    mb.Set(S.Velvet, new Color(0.08f, 0.06f, 0.07f)); mb.Box(T(-0.1f, 0.06f) + Vector3.up * 0.02f, new Vector3(0.012f, 0.03f, 0.13f));
                    for (int k = 0; k < 3; k++) { mb.Set(S.Paper, new Color(0.95f, 0.95f, 0.95f)); mb.Box(T(0.16f + k * 0.02f, 0.02f + k * 0.03f) + Vector3.up * (0.003f + k * 0.002f), new Vector3(0.14f, 0.002f, 0.19f)); }
                    for (int k = 0; k < 8; k++) { mb.Set(S.GlossPaint, Color.HSVToRGB(0.85f + (k % 3) * 0.05f, 0.55f, 0.95f)); mb.Disc(T(0.12f + (k % 4) * 0.03f, -0.02f + (k / 4) * 0.05f) + Vector3.up * 0.01f, 0.012f, 8, true); }
                    mb.Set(S.Gold, pal.Trim); mb.Cyl(T(W / 2 - 0.16f, -0.16f), 0.045f, 0.012f, 14);
                    break;
                case "P13":   // the gamer's setup: two monitors, the keyboard pushed to her right, the pad and mouse on her LEFT (+x: she faces -z)
                    {
                        mb.Set(S.Rubber, new Color(0.05f, 0.05f, 0.06f)); mb.Box(T(0, 0.08f) + Vector3.up * 0.002f, new Vector3(W - 0.1f, 0.003f, 0.36f));
                        mb.Set(S.Plastic, new Color(0.08f, 0.08f, 0.09f));
                        mb.Push(T(-0.14f, 0.06f), 8f); mb.BevelBox(new Vector3(0, 0.012f, 0), new Vector3(0.36f, 0.022f, 0.12f), 0.004f); mb.Pop();
                        mb.Set(S.Plastic, new Color(0.18f, 0.18f, 0.2f)); for (int r = 0; r < 4; r++) for (int k = 0; k < 12; k++) { mb.Push(T(-0.14f, 0.06f), 8f); mb.Box(new Vector3(-0.16f + k * 0.029f, 0.026f, -0.04f + r * 0.027f), new Vector3(0.022f, 0.008f, 0.02f)); mb.Pop(); }
                        // the left-hand pad she laid out in rehab, and the mouse on the left side
                        mb.Set(S.Plastic, new Color(0.08f, 0.08f, 0.09f)); mb.Push(T(0.22f, 0.08f), -12f); mb.BevelBox(new Vector3(0, 0.014f, 0), new Vector3(0.16f, 0.026f, 0.13f), 0.01f); mb.Pop();
                        mb.Set(S.Glow, new Color(0.75f, 0.1f, 0.12f), MansionMats.GlowData(0.35f, 0, 0, c.Circuit)); mb.Push(T(0.22f, 0.08f), -12f); for (int k = 0; k < 9; k++) mb.Box(new Vector3(-0.05f + (k % 3) * 0.035f, 0.029f, -0.035f + (k / 3) * 0.035f), new Vector3(0.026f, 0.006f, 0.026f)); mb.Pop();
                        mb.Set(S.Plastic, new Color(0.1f, 0.1f, 0.11f)); mb.Ellipsoid(T(0.42f, 0.1f) + Vector3.up * 0.015f, new Vector3(0.03f, 0.018f, 0.05f), 8, 5);
                        foreach (var (x, yaw) in new[] { (-0.22f, 14f), (0.22f, -14f) })
                        {
                            mb.Push(T(x, -0.17f), yaw);
                            mb.Set(S.Plastic, new Color(0.06f, 0.06f, 0.07f));
                            mb.Box(new Vector3(0, 0.006f, 0.02f), new Vector3(0.18f, 0.012f, 0.12f)); mb.Box(new Vector3(0, 0.1f, -0.01f), new Vector3(0.04f, 0.2f, 0.03f));
                            mb.Box(new Vector3(0, 0.33f, 0.0f), new Vector3(0.5f, 0.3f, 0.03f));
                            mb.Set(S.Glow, Color.Lerp(own.Light, new Color(0.2f, 0.3f, 0.5f), 0.5f), MansionMats.GlowData(0.55f, 0.05f, 0, c.Circuit));
                            mb.Box(new Vector3(0, 0.335f, 0.016f), new Vector3(0.47f, 0.27f, 0.002f));
                            mb.Pop();
                        }
                        c.View.AddLight(c.Rv, c.W2(T(0, 0.15f) + Vector3.up * 0.35f), Color.Lerp(own.Light, new Color(0.35f, 0.45f, 0.8f), 0.4f), 0.9f, 2.6f, LightType.Point, false, 0.04f);
                        break;
                    }
                case "P14":   // a lacquer tray with one cup, paper lilies folded in a row, lace gloves laid flat
                    mb.Set(S.Obsidian, Color.white); mb.BevelBox(T(-0.14f, 0.02f) + Vector3.up * 0.008f, new Vector3(0.34f, 0.016f, 0.24f), 0.006f);
                    mb.Set(S.Porcelain, new Color(0.95f, 0.95f, 0.93f)); mb.Push(T(-0.2f, 0.02f) + Vector3.up * 0.016f, 0); mb.Lathe(new[] { new Vector2(0.025f, 0), new Vector2(0.04f, 0.05f), new Vector2(0.042f, 0.055f) }, 12, true); mb.Pop();
                    mb.Push(T(-0.08f, 0.02f) + Vector3.up * 0.016f, 0); mb.Lathe(new[] { new Vector2(0.045f, 0), new Vector2(0.06f, 0.05f), new Vector2(0.03f, 0.1f), new Vector2(0.012f, 0.12f) }, 12, true); mb.Pop();
                    mb.Set(S.Paper, new Color(0.97f, 0.97f, 0.95f)); for (int k = 0; k < 5; k++) { var p = T(0.12f + k * 0.07f, 0.12f); mb.Push(p, 0); mb.Lathe(new[] { new Vector2(0.004f, 0), new Vector2(0.02f, 0.03f), new Vector2(0.03f, 0.05f) }, 5); mb.Pop(); }
                    mb.Set(S.Cloth, new Color(0.08f, 0.08f, 0.08f)); foreach (float s in new[] { 0f, 0.07f }) { mb.Push(T(0.22f + s, -0.12f) + Vector3.up * 0.003f, 10f + s * 100f); mb.Box(Vector3.zero, new Vector3(0.06f, 0.004f, 0.16f)); for (int k = 0; k < 4; k++) mb.Box(new Vector3(-0.022f + k * 0.015f, 0, -0.1f), new Vector3(0.01f, 0.004f, 0.05f)); mb.Pop(); }
                    break;
                case "P15":   // the typewriter, newspapers spread under it, a red pen, a paper coffee cup
                    {
                        Sheet(-0.1f, 0.08f, 9f, new Color(0.82f, 0.8f, 0.74f), 0.38f, 0.28f); Sheet(0.16f, 0.1f, -14f, new Color(0.84f, 0.82f, 0.76f), 0.38f, 0.28f);
                        mb.Set(S.Paper, new Color(0.2f, 0.2f, 0.2f)); for (int k = 0; k < 9; k++) mb.Box(T(-0.1f + (k % 3) * 0.1f, 0.0f + (k / 3) * 0.05f) + Vector3.up * 0.005f, new Vector3(0.08f, 0.001f, 0.012f));
                        mb.Push(T(0.02f, -0.12f), -4f);
                        mb.Set(S.PaintedMetal, new Color(0.14f, 0.15f, 0.14f));
                        mb.BevelBox(new Vector3(0, 0.05f, 0), new Vector3(0.34f, 0.1f, 0.26f), 0.02f);
                        mb.Push(new Vector3(0, 0.1f, 0.06f), Quaternion.Euler(-20, 0, 0), Vector3.one); mb.Box(new Vector3(0, 0.01f, 0), new Vector3(0.3f, 0.02f, 0.12f)); mb.Pop();
                        mb.Set(S.Porcelain, new Color(0.85f, 0.83f, 0.78f)); for (int r = 0; r < 3; r++) for (int k = 0; k < 9; k++) mb.Cyl(new Vector3(-0.12f + k * 0.03f + r * 0.01f, 0.1f + r * 0.012f, 0.1f - r * 0.03f), 0.009f, 0.006f, 8);
                        mb.Set(S.Rubber, new Color(0.06f, 0.06f, 0.06f)); mb.Rod(new Vector3(-0.19f, 0.15f, -0.08f), new Vector3(0.19f, 0.15f, -0.08f), 0.025f, 12);
                        mb.Set(S.Paper, new Color(0.95f, 0.94f, 0.9f)); mb.Push(new Vector3(0, 0.2f, -0.1f), Quaternion.Euler(-12, 0, 0), Vector3.one); mb.Box(Vector3.zero, new Vector3(0.21f, 0.14f, 0.002f)); mb.Pop();
                        mb.Pop();
                        Pen(0.3f, 0.2f, 40f, new Color(0.75f, 0.08f, 0.08f));
                        mb.Set(S.Paper, new Color(0.9f, 0.88f, 0.84f)); mb.Push(T(-W / 2 + 0.14f, -0.16f), 0); mb.Lathe(new[] { new Vector2(0.03f, 0), new Vector2(0.042f, 0.12f) }, 12, true); mb.Pop();
                        mb.Set(S.Plastic, new Color(0.15f, 0.12f, 0.1f)); mb.Cyl(T(-W / 2 + 0.14f, -0.16f) + Vector3.up * 0.12f, 0.044f, 0.012f, 12);
                        break;
                    }
                case "P16":   // a swatch book fanned open, a pin cushion, the tape measure, the gift ledger
                    for (int k = 0; k < 6; k++) { mb.Set(S.Velvet, Color.HSVToRGB((0.82f + k * 0.04f) % 1f, 0.55f, 0.55f + k * 0.05f)); mb.Push(T(-0.12f, 0.06f) + Vector3.up * (0.004f + k * 0.003f), -20f + k * 9f); mb.Box(new Vector3(0, 0, 0.06f), new Vector3(0.09f, 0.003f, 0.14f)); mb.Pop(); }
                    mb.Set(S.Velvet, new Color(0.7f, 0.12f, 0.2f)); mb.Sphere(T(0.1f, -0.1f) + Vector3.up * 0.03f, 0.04f, 10, 6, 0.7f);
                    mb.Set(S.Steel, Color.white); for (int k = 0; k < 7; k++) { float a = k * 0.9f; mb.Rod(T(0.1f, -0.1f) + new Vector3(Mathf.Cos(a) * 0.02f, 0.04f, Mathf.Sin(a) * 0.02f), T(0.1f, -0.1f) + new Vector3(Mathf.Cos(a) * 0.035f, 0.075f, Mathf.Sin(a) * 0.035f), 0.0015f, 3); }
                    mb.Set(S.GlossPaint, new Color(0.95f, 0.85f, 0.25f)); mb.Push(T(0.24f, 0.12f), 0); mb.Cyl(Vector3.zero, 0.035f, 0.02f, 12); mb.Pop(); mb.Box(T(0.3f, 0.14f) + Vector3.up * 0.002f, new Vector3(0.12f, 0.002f, 0.016f));
                    BookFlat(W / 2 - 0.16f, -0.16f, 0, new Color(0.32f, 0.1f, 0.28f), 0.035f);
                    break;
                case "P17":   // a cutting mat, cut-out paper walls waiting to be glued, a craft knife, the phone on a mini tripod
                    mb.Set(S.Rubber, new Color(0.35f, 0.62f, 0.55f)); mb.Box(T(-0.05f, 0.05f) + Vector3.up * 0.002f, new Vector3(0.45f, 0.004f, 0.32f));
                    for (int k = 0; k < 6; k++) { mb.Set(S.Paper, Color.Lerp(new Color(0.96f, 0.94f, 0.9f), pal.Accent2, (k % 3) * 0.15f)); mb.Push(T(-0.2f + k * 0.06f, 0.02f + (k % 2) * 0.08f) + Vector3.up * 0.006f, k * 13f); mb.Box(Vector3.zero, new Vector3(0.05f, 0.002f, 0.07f)); mb.Pop(); }
                    mb.Set(S.Paper, new Color(0.96f, 0.95f, 0.92f)); mb.Push(T(0.12f, 0.02f), 20f); mb.Box(new Vector3(0, 0.035f, 0), new Vector3(0.08f, 0.07f, 0.06f)); mb.Tri(new Vector3(-0.04f, 0.07f, -0.03f), new Vector3(0, 0.11f, -0.03f), new Vector3(0.04f, 0.07f, -0.03f)); mb.Tri(new Vector3(0.04f, 0.07f, 0.03f), new Vector3(0, 0.11f, 0.03f), new Vector3(-0.04f, 0.07f, 0.03f)); mb.Pop();
                    mb.Set(S.GlossPaint, new Color(0.95f, 0.8f, 0.2f)); mb.Rod(T(-0.26f, 0.18f) + Vector3.up * 0.006f, T(-0.16f, 0.2f) + Vector3.up * 0.006f, 0.006f, 5);
                    mb.Set(S.Plastic, new Color(0.1f, 0.1f, 0.1f)); foreach (float a in new[] { 0f, 120f, 240f }) mb.Rod(T(W / 2 - 0.18f, -0.16f) + Vector3.up * 0.1f, T(W / 2 - 0.18f, -0.16f) + Quaternion.Euler(0, a, 0) * new Vector3(0.06f, 0, 0), 0.004f, 4);
                    mb.Box(T(W / 2 - 0.18f, -0.16f) + Vector3.up * 0.17f, new Vector3(0.075f, 0.15f, 0.008f));
                    break;
                case "P18":   // one puzzle book squared to the corner, one pencil beside it: nothing else
                    BookFlat(0.1f, 0.1f, 0, new Color(0.55f, 0.52f, 0.4f), 0.012f);
                    mb.Set(S.Paper, new Color(0.2f, 0.2f, 0.2f)); for (int r = 0; r < 4; r++) for (int k = 0; k < 4; k++) mb.Box(T(0.07f + k * 0.022f, 0.05f + r * 0.022f) + Vector3.up * 0.0135f, new Vector3(0.018f, 0.0005f, 0.018f));
                    Pen(0.26f, 0.1f, 90f, new Color(0.85f, 0.7f, 0.2f));
                    break;
            }
            OwnerDeskLight(c, mb, own);
        }

        /// <summary>The resident's own kind of light, in the corner of the desk their things leave free.</summary>
        static void OwnerDeskLight(Ctx c, MeshBuilder mb, OwnerLook own)
        {
            if (own.LampKind == "monitor") return;   // the gamer's monitors are her light
            float W = c.W, D = c.D, H = c.H;
            float lx, lz = -D / 2 + 0.13f;
            switch (own.Id)
            {
                case "P01": case "P02": case "P06": case "P08": case "P11": lx = 0.04f; lz = -D / 2 + 0.1f; break;   // their back corners are taken
                case "P03": case "P05": case "P09": case "P15": lx = W / 2 - 0.15f; break;
                default: lx = -W / 2 + 0.15f; break;
            }
            var at = new Vector3(lx, H, lz);
            switch (own.LampKind)
            {
                case "lamp": Lamp(c, mb, at, 0.22f); return;
                case "candles":
                    {
                        var flames = new List<Vector3>();
                        mb.Set(S.Brass, new Color(0.7f, 0.58f, 0.4f)); mb.Cyl(at, 0.06f, 0.012f, 12);
                        for (int k = 0; k < 3; k++) { var p = at + new Vector3((k - 1) * 0.035f, 0.012f, (k % 2) * 0.02f); float h = 0.1f + k * 0.04f; Candle(mb, p, h, 0.012f, -2, p + Vector3.up * (h + 0.005f), flames); }
                        Flames(c, mb, flames, 0.05f, true, 0.9f, 2.6f);   // wicks are local to the desk
                        return;
                    }
            }
            string kind = "lamp_" + own.LampKind;
            mb.Push(at, 0); MansionView.OwnerKindBuild(mb, kind, own.Sig, own.Light, c.Circuit); mb.Pop();
            float bulbY = kind == "lamp_work" ? 0.42f : kind == "lamp_bulb" ? 0.36f : 0.3f;
            c.View.AddLight(c.Rv, c.W2(at + new Vector3(0, bulbY, 0.05f)), own.Light, kind == "lamp_ring" || kind == "lamp_vanity" ? 1.3f : 1.6f, 3.4f, LightType.Point, false, 0.03f);
        }
    }
}
