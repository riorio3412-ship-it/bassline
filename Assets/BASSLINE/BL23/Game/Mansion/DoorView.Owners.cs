using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// The corridor face of a resident's door (OwnerStyles): the inset panels in their signature colour, a relief emblem
    /// that reads their personality at a glance (a candy jar, a controller, a camera, a knife and whisk...), a lit name
    /// plaque readable from across the corridor, and sometimes something hung on it (a rota, a gold chain, a headset).
    /// Built in a "face frame": origin on the leaf's centre line at floor level, +Z out of the door toward the corridor.
    /// </summary>
    public sealed partial class DoorView
    {
        internal const float PlaqueY = 1.5f, PlaqueW = 0.44f, PlaqueH = 0.14f, EmblemY = 1.86f;

        /// <summary>Plaque on the face: brass frame, a softly lit ivory card (the name is TMP text in front of it).</summary>
        internal static void OwnerPlaque(MeshBuilder mb, OwnerLook own, float z0, int circuit)
        {
            mb.Set(S.Brass, new Color(0.74f, 0.6f, 0.4f));
            mb.BevelBox(new Vector3(0, PlaqueY, z0 + 0.007f), new Vector3(PlaqueW, PlaqueH, 0.014f), 0.004f);
            // two brass screws and a thin band in the owner's colour under the card
            mb.Set(S.Brass, new Color(0.9f, 0.76f, 0.5f));
            foreach (float s in new[] { -1f, 1f }) mb.Sphere(new Vector3(s * (PlaqueW / 2 - 0.018f), PlaqueY, z0 + 0.015f), 0.006f, 6, 4);
            mb.Set(S.Glow, new Color(1f, 0.93f, 0.8f), MansionMats.GlowData(0.42f, 0f, 0, circuit));
            mb.Box(new Vector3(0, PlaqueY + 0.008f, z0 + 0.0145f), new Vector3(PlaqueW - 0.06f, PlaqueH - 0.05f, 0.002f));
            mb.Set(S.GlossPaint, own.Sig);
            mb.Box(new Vector3(0, PlaqueY - PlaqueH / 2 + 0.017f, z0 + 0.0145f), new Vector3(PlaqueW - 0.06f, 0.01f, 0.002f));
        }

        /// <summary>A flat relief of a closed outline (x right, y up), 'depth' proud of z=0 toward +Z.</summary>
        internal static void Relief(MeshBuilder mb, IList<Vector2> poly, float depth)
        {
            Vector2 c = Vector2.zero; foreach (var p in poly) c += p; c /= poly.Count;
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % n];
                mb.TriAuto(new Vector3(c.x, c.y, depth), new Vector3(a.x, a.y, depth), new Vector3(b.x, b.y, depth), Vector3.forward);
                var mid = (a + b) * 0.5f - c;
                mb.QuadAuto(new Vector3(a.x, a.y, 0), new Vector3(a.x, a.y, depth), new Vector3(b.x, b.y, depth), new Vector3(b.x, b.y, 0), new Vector3(mid.x, mid.y, 0));
            }
        }

        internal static List<Vector2> Star(int points, float r0, float r1, float rot = 90f)
        {
            var l = new List<Vector2>();
            for (int i = 0; i < points * 2; i++) { float a = (rot + i * 180f / points) * Mathf.Deg2Rad; float r = i % 2 == 0 ? r0 : r1; l.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r)); }
            return l;
        }

        /// <summary>Squashed solid of revolution lying on the face (axis along y), 'flat' of its depth.</summary>
        static void FlatLathe(MeshBuilder mb, Vector3 at, IList<Vector2> prof, float flat, int seg = 14)
        {
            mb.Push(Matrix4x4.TRS(at, Quaternion.identity, new Vector3(1, 1, flat))); mb.Lathe(prof, seg, true, true); mb.Pop();
        }

        /// <summary>The emblem medallion, centred at the origin facing +Z (about 0.26 m across).</summary>
        internal static void OwnerEmblem(MeshBuilder mb, OwnerLook own, MansionPalette pal, int circuit)
        {
            Color brass = new Color(0.8f, 0.64f, 0.38f), ivory = new Color(0.9f, 0.86f, 0.76f), dark = new Color(0.07f, 0.06f, 0.06f);
            Color acc = own.Sig;
            // the ground: a round cartouche in dark lacquer with a brass rim (every emblem sits on the same ground, so the
            // door reads as "someone's room" from far off, and the motif tells whose)
            mb.Set(S.Obsidian, Color.Lerp(dark, own.Door, 0.25f));
            mb.Push(Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Cyl(Vector3.zero, 0.145f, 0.012f, 24); mb.Pop();
            mb.Set(S.Brass, brass);
            mb.Push(Matrix4x4.TRS(new Vector3(0, 0, 0.012f), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Torus(Vector3.zero, 0.145f, 0.009f, 28, 5); mb.Pop();
            mb.Push(new Vector3(0, 0, 0.012f), 0);
            switch (own.Emblem)
            {
                case "wheat":
                    mb.Set(S.Gold, brass);
                    foreach (float a in new[] { -18f, 0f, 18f })
                    {
                        mb.Push(Matrix4x4.TRS(new Vector3(0, -0.1f, 0), Quaternion.Euler(0, 0, a), Vector3.one));
                        mb.Rod(new Vector3(0, 0, 0.004f), new Vector3(0, 0.15f, 0.004f), 0.004f, 5);
                        for (int k = 0; k < 5; k++) foreach (float s in new[] { -1f, 1f }) mb.Ellipsoid(new Vector3(s * 0.012f, 0.13f + k * 0.022f, 0.006f), new Vector3(0.009f, 0.016f, 0.006f), 6, 4);
                        mb.Ellipsoid(new Vector3(0, 0.25f, 0.006f), new Vector3(0.008f, 0.016f, 0.006f), 6, 4);
                        mb.Pop();
                    }
                    break;
                case "candyjar":
                    mb.Set(S.Glass, Color.white); FlatLathe(mb, new Vector3(0, -0.1f, 0.01f), new[] { new Vector2(0.06f, 0), new Vector2(0.08f, 0.04f), new Vector2(0.08f, 0.13f), new Vector2(0.045f, 0.16f), new Vector2(0.05f, 0.18f) }, 0.25f);
                    mb.Set(S.Brass, brass); mb.Box(new Vector3(0, 0.09f, 0.015f), new Vector3(0.1f, 0.016f, 0.01f)); mb.Sphere(new Vector3(0, 0.11f, 0.015f), 0.014f, 8, 5);
                    for (int k = 0; k < 9; k++) { mb.Set(S.GlossPaint, Color.HSVToRGB((k * 0.13f) % 1f, 0.65f, 0.8f)); mb.Sphere(new Vector3(-0.05f + (k % 3) * 0.05f, -0.07f + (k / 3) * 0.045f, 0.02f), 0.016f, 7, 5, 0.7f); }
                    break;
                case "clipboard":
                    mb.Set(S.WoodLight, new Color(0.75f, 0.6f, 0.42f)); mb.Box(new Vector3(0, -0.01f, 0.004f), new Vector3(0.15f, 0.2f, 0.008f));
                    mb.Set(S.Paper, ivory); mb.Box(new Vector3(0, -0.02f, 0.009f), new Vector3(0.12f, 0.16f, 0.002f));
                    mb.Set(S.Paper, new Color(0.2f, 0.22f, 0.3f)); for (int k = 0; k < 5; k++) mb.Box(new Vector3(0.01f, -0.07f + k * 0.025f, 0.011f), new Vector3(0.08f, 0.004f, 0.001f));
                    mb.Set(S.GlossPaint, new Color(0.75f, 0.1f, 0.12f)); for (int k = 0; k < 5; k++) mb.Box(new Vector3(-0.045f, -0.07f + k * 0.025f, 0.011f), new Vector3(0.012f, 0.012f, 0.002f));
                    mb.Set(S.Steel, Color.white); mb.BevelBox(new Vector3(0, 0.085f, 0.013f), new Vector3(0.07f, 0.03f, 0.014f), 0.004f);
                    break;
                case "frame":
                    mb.Set(S.Gold, brass);
                    foreach (var (p, s) in new[] { (new Vector3(0, 0.07f, 0.008f), new Vector3(0.19f, 0.03f, 0.016f)), (new Vector3(0, -0.07f, 0.008f), new Vector3(0.19f, 0.03f, 0.016f)), (new Vector3(-0.08f, 0, 0.008f), new Vector3(0.03f, 0.17f, 0.016f)), (new Vector3(0.08f, 0, 0.008f), new Vector3(0.03f, 0.17f, 0.016f)) }) mb.BevelBox(p, s, 0.005f);
                    foreach (float x in new[] { -0.08f, 0.08f }) foreach (float y in new[] { -0.07f, 0.07f }) mb.Sphere(new Vector3(x, y, 0.018f), 0.012f, 8, 5);
                    mb.Set(S.Plaster, new Color(0.55f, 0.52f, 0.46f)); mb.Box(new Vector3(0, 0, 0.003f), new Vector3(0.13f, 0.11f, 0.004f));
                    break;
                case "rosette":
                    mb.Set(S.Velvet, acc);
                    foreach (float s in new[] { -1f, 1f }) { var tail = new List<Vector2> { new Vector2(s * 0.01f, 0), new Vector2(s * 0.06f, -0.02f), new Vector2(s * 0.07f, -0.13f), new Vector2(s * 0.045f, -0.11f), new Vector2(s * 0.03f, -0.14f) }; Relief(mb, tail, 0.006f); }
                    mb.Push(new Vector3(0, 0.02f, 0.006f), 0); Relief(mb, Star(16, 0.085f, 0.07f), 0.008f); mb.Pop();
                    mb.Set(S.Gold, brass); mb.Push(Matrix4x4.TRS(new Vector3(0, 0.02f, 0.014f), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Cyl(Vector3.zero, 0.05f, 0.01f, 18); mb.Pop();
                    break;
                case "watch":
                    mb.Set(S.Gold, brass);
                    mb.Push(Matrix4x4.TRS(new Vector3(0, -0.015f, 0.004f), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Cyl(Vector3.zero, 0.085f, 0.012f, 24); mb.Pop();
                    mb.Set(S.Porcelain, ivory); mb.Push(Matrix4x4.TRS(new Vector3(0, -0.015f, 0.016f), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Cyl(Vector3.zero, 0.07f, 0.002f, 24); mb.Pop();
                    mb.Set(S.Obsidian, dark); for (int k = 0; k < 12; k++) { float a = k * 30f * Mathf.Deg2Rad; mb.Box(new Vector3(Mathf.Sin(a) * 0.058f, -0.015f + Mathf.Cos(a) * 0.058f, 0.019f), new Vector3(0.004f, 0.004f, 0.001f)); }
                    mb.Bar(new Vector3(0, -0.015f, 0.02f), new Vector3(0.028f, 0.02f, 0.02f), 0.004f, 0.002f); mb.Bar(new Vector3(0, -0.015f, 0.02f), new Vector3(-0.012f, 0.035f, 0.02f), 0.003f, 0.002f);
                    mb.Set(S.Gold, brass); mb.Cyl(new Vector3(0, 0.07f, 0.01f), 0.012f, 0.02f, 8); mb.Push(Matrix4x4.TRS(new Vector3(0, 0.1f, 0.01f), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Torus(Vector3.zero, 0.018f, 0.004f, 12, 4); mb.Pop();
                    break;
                case "mic":
                    mb.Set(S.Chrome, Color.white);
                    mb.Push(Matrix4x4.TRS(new Vector3(0, 0.05f, 0.02f), Quaternion.identity, new Vector3(1, 1.25f, 0.5f))); mb.Sphere(Vector3.zero, 0.055f, 14, 10); mb.Pop();
                    mb.Set(S.Obsidian, dark); for (int k = -2; k <= 2; k++) mb.Box(new Vector3(0, 0.05f + k * 0.018f, 0.047f), new Vector3(0.1f - Mathf.Abs(k) * 0.018f, 0.003f, 0.002f));
                    mb.Set(S.Gold, brass); mb.Push(Matrix4x4.TRS(new Vector3(0, -0.02f, 0.02f), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Torus(Vector3.zero, 0.058f, 0.006f, 16, 4, 180, 360); mb.Pop();
                    mb.Set(S.Obsidian, dark); mb.Rod(new Vector3(0, -0.015f, 0.012f), new Vector3(0, -0.11f, 0.012f), 0.011f, 8);
                    break;
                case "clef":
                    {
                        mb.Set(S.Porcelain, ivory);
                        var path = new List<Vector3>();
                        for (int k = 0; k <= 16; k++) { float t = k / 16f; float a = Mathf.Lerp(200f, -60f, t) * Mathf.Deg2Rad; float r = Mathf.Lerp(0.045f, 0.075f, t); path.Add(new Vector3(0.035f - Mathf.Cos(a) * r, 0.02f + Mathf.Sin(a) * r * (1f + t * 0.4f), 0.01f)); }   // face +X reads as the viewer's left: the clef is built mirrored
                        for (int k = 1; k <= 5; k++) { var l = path[path.Count - 1]; path.Add(l + new Vector3(0.012f * k, -0.022f * k, 0)); }
                        mb.Tube(path, 0.011f, 7, true);
                        mb.Sphere(new Vector3(0.07f, 0.0f, 0.012f), 0.022f, 10, 6, 0.6f);
                        mb.Sphere(new Vector3(-0.07f, 0.045f, 0.01f), 0.013f, 8, 5, 0.6f); mb.Sphere(new Vector3(-0.07f, -0.005f, 0.01f), 0.013f, 8, 5, 0.6f);
                        break;
                    }
                case "masks":
                    foreach (float s in new[] { -1f, 1f })
                    {
                        mb.Set(S.Porcelain, s < 0 ? ivory : Color.Lerp(own.Door, ivory, 0.35f));
                        var c = new Vector3(s * 0.045f, s * 0.018f, 0.014f);
                        mb.Push(Matrix4x4.TRS(c, Quaternion.Euler(0, 0, s * 14f), new Vector3(1, 1, 0.45f))); mb.Sphere(Vector3.zero, 0.062f, 12, 8, 1.25f); mb.Pop();
                        mb.Set(S.Obsidian, dark);
                        foreach (float e in new[] { -1f, 1f }) { mb.Push(Matrix4x4.TRS(c + new Vector3(e * 0.022f, 0.02f, 0.03f), Quaternion.Euler(0, 0, e * s * 20f), new Vector3(1, 0.6f, 0.2f))); mb.Sphere(Vector3.zero, 0.012f, 8, 5); mb.Pop(); }
                        mb.Push(Matrix4x4.TRS(c + new Vector3(0, -0.03f, 0.03f), Quaternion.Euler(0, 0, s < 0 ? 180 : 0), new Vector3(1, 1, 0.2f))); mb.Torus(Vector3.zero, 0.02f, 0.004f, 10, 3, 0, 180); mb.Pop();
                    }
                    break;
                case "knifewhisk":
                    mb.Push(Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 40f), Vector3.one));
                    mb.Set(S.Steel, new Color(0.85f, 0.86f, 0.88f)); Relief(mb, new List<Vector2> { new Vector2(-0.012f, -0.01f), new Vector2(0.018f, -0.01f), new Vector2(0.01f, 0.11f), new Vector2(-0.006f, 0.15f), new Vector2(-0.012f, 0.1f) }, 0.006f);
                    mb.Set(S.WoodDark, new Color(0.3f, 0.2f, 0.14f)); mb.BevelBox(new Vector3(0, -0.06f, 0.006f), new Vector3(0.028f, 0.1f, 0.012f), 0.005f);
                    mb.Pop();
                    mb.Push(Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, -40f), Vector3.one));
                    mb.Set(S.Steel, new Color(0.85f, 0.86f, 0.88f)); mb.Rod(new Vector3(0, -0.11f, 0.008f), new Vector3(0, -0.01f, 0.008f), 0.008f, 6);
                    for (int k = 0; k < 4; k++) { mb.Push(Matrix4x4.TRS(new Vector3(0, 0.05f, 0.012f), Quaternion.Euler(0, 45f * k, 0), new Vector3(1, 1, 0.35f))); mb.Torus(Vector3.zero, 0.035f, 0.002f, 14, 3); mb.Pop(); }
                    mb.Pop();
                    break;
                case "gear":
                    mb.Set(S.Copper, new Color(0.85f, 0.55f, 0.32f));
                    Relief(mb, Star(12, 0.1f, 0.082f, 0f), 0.012f);
                    mb.Set(S.Obsidian, Color.Lerp(dark, own.Door, 0.3f)); mb.Push(Matrix4x4.TRS(new Vector3(0, 0, 0.012f), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Cyl(Vector3.zero, 0.035f, 0.002f, 14); mb.Pop();
                    mb.Set(S.Brass, brass); for (int k = 0; k < 4; k++) { float a = k * 90f * Mathf.Deg2Rad + 0.4f; mb.Bar(new Vector3(Mathf.Cos(a) * 0.035f, Mathf.Sin(a) * 0.035f, 0.013f), new Vector3(Mathf.Cos(a) * 0.075f, Mathf.Sin(a) * 0.075f, 0.013f), 0.012f, 0.004f); }
                    break;
                case "star":
                    mb.Set(S.Gold, new Color(0.95f, 0.8f, 0.55f)); Relief(mb, Star(5, 0.105f, 0.045f), 0.012f);
                    mb.Set(S.GlossPaint, Color.Lerp(own.Door, Color.white, 0.2f)); mb.Push(new Vector3(0, 0, 0.012f), 0); Relief(mb, Star(5, 0.06f, 0.026f), 0.004f); mb.Pop();
                    break;
                case "controller":
                    {
                        Color body = new Color(0.12f, 0.12f, 0.13f);
                        mb.Set(S.Plastic, body);
                        mb.Push(Matrix4x4.TRS(new Vector3(0, 0.01f, 0.014f), Quaternion.identity, new Vector3(1, 1, 0.35f))); mb.Ellipsoid(Vector3.zero, new Vector3(0.1f, 0.05f, 0.05f), 16, 8); mb.Pop();
                        foreach (float s in new[] { -1f, 1f }) { mb.Push(Matrix4x4.TRS(new Vector3(s * 0.07f, -0.035f, 0.012f), Quaternion.Euler(0, 0, s * -25f), new Vector3(1, 1, 0.35f))); mb.Ellipsoid(Vector3.zero, new Vector3(0.035f, 0.055f, 0.04f), 10, 6); mb.Pop(); }
                        mb.Set(S.Plastic, new Color(0.3f, 0.3f, 0.32f)); mb.Box(new Vector3(0.055f, 0.015f, 0.032f), new Vector3(0.036f, 0.011f, 0.004f)); mb.Box(new Vector3(0.055f, 0.015f, 0.032f), new Vector3(0.011f, 0.036f, 0.004f));
                        mb.Set(S.Glow, Color.Lerp(own.Door, new Color(1f, 0.2f, 0.2f), 0.3f), MansionMats.GlowData(0.6f, 0.02f, 0, circuit));
                        foreach (var (x, y) in new[] { (-0.055f, 0.032f), (-0.075f, 0.015f), (-0.035f, 0.015f), (-0.055f, -0.002f) }) mb.Sphere(new Vector3(x, y, 0.032f), 0.008f, 7, 4, 0.5f);
                        mb.Set(S.Plastic, new Color(0.22f, 0.22f, 0.24f)); foreach (float s in new[] { -1f, 1f }) mb.Cyl(new Vector3(s * 0.025f, -0.02f, 0.03f), 0.012f, 0.005f, 10);
                        break;
                    }
                case "lily":
                    mb.Set(S.Leaf, new Color(0.2f, 0.3f, 0.2f)); mb.Rod(new Vector3(0.01f, -0.13f, 0.006f), new Vector3(0, -0.01f, 0.006f), 0.005f, 5);
                    mb.Push(Matrix4x4.TRS(new Vector3(0.03f, -0.08f, 0.006f), Quaternion.Euler(0, 0, -40f), new Vector3(1, 1, 0.3f))); mb.Ellipsoid(Vector3.zero, new Vector3(0.012f, 0.045f, 0.01f), 8, 4); mb.Pop();
                    mb.Set(S.Porcelain, new Color(0.95f, 0.94f, 0.9f));
                    for (int k = 0; k < 6; k++) { float a = k * 60f + 90f; mb.Push(Matrix4x4.TRS(new Vector3(0, 0.03f, 0.014f), Quaternion.Euler(0, 0, a - 90f), Vector3.one)); mb.Push(Matrix4x4.TRS(new Vector3(0, 0.045f, 0), Quaternion.identity, new Vector3(1, 1, 0.3f))); mb.Ellipsoid(Vector3.zero, new Vector3(0.018f, 0.05f, 0.02f), 8, 5); mb.Pop(); mb.Pop(); }
                    mb.Set(S.Gold, brass); for (int k = 0; k < 5; k++) { float a = (k * 72f + 10f) * Mathf.Deg2Rad; mb.Rod(new Vector3(0, 0.03f, 0.02f), new Vector3(Mathf.Cos(a) * 0.03f, 0.03f + Mathf.Sin(a) * 0.03f, 0.024f), 0.0018f, 3); }
                    break;
                case "camera":
                    mb.Set(S.Leather, new Color(0.08f, 0.08f, 0.08f)); mb.BevelBox(new Vector3(0, -0.01f, 0.012f), new Vector3(0.19f, 0.11f, 0.024f), 0.008f);
                    mb.Set(S.Chrome, Color.white); mb.Box(new Vector3(0, 0.05f, 0.012f), new Vector3(0.19f, 0.012f, 0.024f)); mb.BevelBox(new Vector3(-0.055f, 0.066f, 0.012f), new Vector3(0.04f, 0.022f, 0.02f), 0.004f);
                    mb.Push(Matrix4x4.TRS(new Vector3(0.01f, -0.012f, 0.024f), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Cyl(Vector3.zero, 0.042f, 0.014f, 20); mb.Set(S.Obsidian, dark); mb.Cyl(Vector3.up * 0.014f, 0.03f, 0.004f, 18); mb.Pop();
                    mb.Set(S.Glass, Color.white); mb.Sphere(new Vector3(0.01f, -0.012f, 0.042f), 0.02f, 10, 6, 0.4f);
                    mb.Set(S.Porcelain, ivory); mb.Box(new Vector3(0.065f, 0.035f, 0.025f), new Vector3(0.03f, 0.014f, 0.004f));
                    break;
                case "button":
                    mb.Set(S.Porcelain, new Color(0.88f, 0.84f, 0.86f));
                    mb.Push(Matrix4x4.TRS(new Vector3(0, 0, 0.004f), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Cyl(Vector3.zero, 0.09f, 0.012f, 24); mb.Pop();
                    mb.Push(Matrix4x4.TRS(new Vector3(0, 0, 0.016f), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Torus(Vector3.zero, 0.078f, 0.008f, 24, 4); mb.Pop();
                    mb.Set(S.Obsidian, dark); foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f }) { mb.Push(Matrix4x4.TRS(new Vector3(x * 0.022f, y * 0.022f, 0.016f), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Cyl(Vector3.zero, 0.009f, 0.001f, 8); mb.Pop(); }
                    mb.Set(S.Velvet, acc); mb.Bar(new Vector3(-0.03f, -0.03f, 0.019f), new Vector3(0.03f, 0.03f, 0.019f), 0.006f, 0.003f); mb.Bar(new Vector3(-0.03f, 0.03f, 0.019f), new Vector3(0.03f, -0.03f, 0.019f), 0.006f, 0.003f);
                    break;
                case "paperhouse":
                    mb.Set(S.Paper, new Color(0.95f, 0.94f, 0.9f));
                    Relief(mb, new List<Vector2> { new Vector2(-0.08f, -0.09f), new Vector2(0.08f, -0.09f), new Vector2(0.08f, 0.02f), new Vector2(0, 0.1f), new Vector2(-0.08f, 0.02f) }, 0.01f);
                    mb.Set(S.Paper, acc); Relief(mb, new List<Vector2> { new Vector2(-0.1f, 0.01f), new Vector2(0, 0.11f), new Vector2(0.1f, 0.01f), new Vector2(0.09f, 0.0f), new Vector2(0, 0.09f), new Vector2(-0.09f, 0.0f) }, 0.016f);
                    mb.Set(S.WoodDark, new Color(0.3f, 0.22f, 0.18f)); mb.Box(new Vector3(0, -0.055f, 0.012f), new Vector3(0.035f, 0.07f, 0.004f));
                    mb.Set(S.Glow, new Color(1f, 0.85f, 0.55f), MansionMats.GlowData(0.5f, 0.05f, 0, circuit)); foreach (float x in new[] { -0.045f, 0.045f }) mb.Box(new Vector3(x, -0.03f, 0.012f), new Vector3(0.025f, 0.025f, 0.003f));
                    break;
                case "thermos":
                    mb.Set(S.PaintedMetal, Color.Lerp(own.Door, new Color(0.2f, 0.25f, 0.18f), 0.4f)); FlatLathe(mb, new Vector3(0, -0.11f, 0.012f), new[] { new Vector2(0.04f, 0), new Vector2(0.042f, 0.005f), new Vector2(0.042f, 0.15f), new Vector2(0.03f, 0.17f) }, 0.35f);
                    mb.Set(S.Steel, Color.white); FlatLathe(mb, new Vector3(0, 0.06f, 0.012f), new[] { new Vector2(0.034f, 0), new Vector2(0.036f, 0.05f), new Vector2(0.03f, 0.055f) }, 0.35f);
                    mb.Set(S.Obsidian, dark); mb.Push(Matrix4x4.TRS(new Vector3(0.052f, -0.03f, 0.012f), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Torus(Vector3.zero, 0.03f, 0.007f, 12, 4, -90, 90); mb.Pop();
                    break;
            }
            mb.Pop();
        }

        /// <summary>Something the resident hung on their own door (face frame; knob at kx, 1.02 m).</summary>
        internal static void OwnerHang(MeshBuilder mb, OwnerLook own, float kx, float kz, float face)
        {
            switch (own.Hang)
            {
                case "roster":
                    {
                        // a duty rota on a clipboard, hung on a brass nail under the plaque
                        mb.Set(S.Brass, Color.white); mb.Rod(new Vector3(0, 1.38f, face), new Vector3(0, 1.38f, face + 0.02f), 0.003f, 4);
                        mb.Set(S.WoodLight, new Color(0.7f, 0.56f, 0.4f)); mb.Box(new Vector3(0, 1.21f, face + 0.006f), new Vector3(0.22f, 0.3f, 0.006f));
                        mb.Set(S.Paper, new Color(0.95f, 0.93f, 0.86f)); mb.Box(new Vector3(0, 1.2f, face + 0.0105f), new Vector3(0.19f, 0.25f, 0.002f));
                        mb.Set(S.Paper, new Color(0.18f, 0.2f, 0.3f));
                        for (int r = 0; r < 7; r++) mb.Box(new Vector3(0, 1.1f + r * 0.03f, face + 0.012f), new Vector3(0.17f, 0.0015f, 0.001f));
                        for (int k = 0; k < 4; k++) mb.Box(new Vector3(-0.08f + k * 0.053f, 1.19f, face + 0.012f), new Vector3(0.0015f, 0.2f, 0.001f));
                        mb.Set(S.GlossPaint, new Color(0.72f, 0.1f, 0.12f)); for (int k = 0; k < 6; k++) mb.Box(new Vector3(-0.055f + (k % 3) * 0.053f, 1.115f + (k / 3) * 0.06f + (k % 2) * 0.03f, face + 0.013f), new Vector3(0.03f, 0.012f, 0.001f));
                        mb.Set(S.Steel, Color.white); mb.BevelBox(new Vector3(0, 1.345f, face + 0.013f), new Vector3(0.09f, 0.03f, 0.014f), 0.004f);
                        break;
                    }
                case "chain":
                    {
                        // a heavy gold chain looped over the handle
                        mb.Set(S.Gold, new Color(1f, 0.82f, 0.4f));
                        for (int k = 0; k < 16; k++)
                        {
                            float t = k / 15f; float x = kx + Mathf.Lerp(-0.07f, 0.07f, t); float y = 1.0f - Mathf.Sin(t * Mathf.PI) * 0.2f;
                            mb.Push(Matrix4x4.TRS(new Vector3(x, y, kz - 0.01f), Quaternion.Euler(k % 2 == 0 ? 0 : 90, 0, 90f), Vector3.one)); mb.Torus(Vector3.zero, 0.012f, 0.004f, 8, 4); mb.Pop();
                        }
                        break;
                    }
                case "starcharm":
                    {
                        mb.Set(S.Velvet, new Color(0.08f, 0.06f, 0.07f)); mb.Rod(new Vector3(kx, 1.0f, kz), new Vector3(kx - 0.01f, 0.84f, kz - 0.02f), 0.004f, 4, false);
                        mb.Push(Matrix4x4.TRS(new Vector3(kx - 0.01f, 0.8f, kz - 0.02f), Quaternion.Euler(0, 10f, 0), Vector3.one)); mb.Set(S.Gold, new Color(0.95f, 0.8f, 0.55f)); Relief(mb, Star(5, 0.05f, 0.022f), 0.01f); mb.Pop();
                        mb.Set(S.Velvet, new Color(0.08f, 0.06f, 0.07f)); foreach (float s in new[] { -1f, 1f }) { mb.Push(Matrix4x4.TRS(new Vector3(kx + s * 0.03f, 1.0f, kz), Quaternion.Euler(0, 0, s * 30f), new Vector3(1, 1, 0.5f))); mb.Ellipsoid(Vector3.zero, new Vector3(0.03f, 0.018f, 0.02f), 8, 5); mb.Pop(); }
                        break;
                    }
                case "headset":
                    {
                        // headphones hung on the handle by their band
                        mb.Set(S.Plastic, new Color(0.1f, 0.1f, 0.11f));
                        mb.Push(Matrix4x4.TRS(new Vector3(kx, 0.95f, kz), Quaternion.identity, Vector3.one)); mb.Torus(Vector3.zero, 0.085f, 0.012f, 16, 5, 0, 180); mb.Pop();
                        foreach (float s in new[] { -1f, 1f }) { mb.Push(Matrix4x4.TRS(new Vector3(kx + s * 0.085f, 0.92f, kz - 0.01f), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 0.55f))); mb.Sphere(Vector3.zero, 0.05f, 12, 8, 1.1f); mb.Pop(); }
                        mb.Set(S.Glow, new Color(0.8f, 0.12f, 0.14f), MansionMats.GlowData(0.5f, 0, 0, -1));
                        foreach (float s in new[] { -1f, 1f }) { mb.Push(Matrix4x4.TRS(new Vector3(kx + s * 0.085f, 0.92f, kz + 0.018f), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Torus(Vector3.zero, 0.03f, 0.003f, 12, 3); mb.Pop(); }
                        break;
                    }
                case "ribbon":
                    {
                        // a black mourning bow tied round the handle, its two tails hanging
                        mb.Set(S.Velvet, new Color(0.05f, 0.05f, 0.05f));
                        foreach (float s in new[] { -1f, 1f }) { mb.Push(Matrix4x4.TRS(new Vector3(kx + s * 0.04f, 1.03f, kz), Quaternion.Euler(0, 0, s * 15f), new Vector3(1, 1, 0.4f))); mb.Ellipsoid(Vector3.zero, new Vector3(0.042f, 0.026f, 0.03f), 10, 6); mb.Pop(); }
                        mb.Sphere(new Vector3(kx, 1.02f, kz), 0.018f, 8, 5);
                        foreach (float s in new[] { -1f, 1f }) mb.QuadAuto(new Vector3(kx + s * 0.008f, 1.01f, kz), new Vector3(kx + s * 0.035f, 0.82f, kz - 0.01f), new Vector3(kx + s * 0.06f, 0.84f, kz - 0.01f), new Vector3(kx + s * 0.02f, 1.01f, kz), Vector3.forward);
                        foreach (float s in new[] { -1f, 1f }) mb.QuadAuto(new Vector3(kx + s * 0.008f, 1.01f, kz - 0.001f), new Vector3(kx + s * 0.035f, 0.82f, kz - 0.011f), new Vector3(kx + s * 0.06f, 0.84f, kz - 0.011f), new Vector3(kx + s * 0.02f, 1.01f, kz - 0.001f), Vector3.back);
                        break;
                    }
                case "shopsign":
                    {
                        // a little shop sign on a cord (the duck mascot on it)
                        mb.Set(S.Brass, Color.white); mb.Rod(new Vector3(0, 1.36f, face), new Vector3(0, 1.36f, face + 0.02f), 0.003f, 4);
                        mb.Set(S.Linen, new Color(0.8f, 0.75f, 0.6f)); mb.Rod(new Vector3(0, 1.36f, face + 0.015f), new Vector3(-0.08f, 1.26f, face + 0.012f), 0.002f, 3, false); mb.Rod(new Vector3(0, 1.36f, face + 0.015f), new Vector3(0.08f, 1.26f, face + 0.012f), 0.002f, 3, false);
                        mb.Set(S.GlossPaint, Color.Lerp(own.Door, Color.black, 0.2f)); mb.BevelBox(new Vector3(0, 1.2f, face + 0.012f), new Vector3(0.2f, 0.12f, 0.012f), 0.006f);
                        mb.Set(S.Gold, new Color(0.85f, 0.85f, 0.88f)); mb.Box(new Vector3(0, 1.2f, face + 0.0185f), new Vector3(0.18f, 0.1f, 0.001f));
                        mb.Set(S.GlossPaint, Color.Lerp(own.Door, Color.black, 0.2f)); mb.Box(new Vector3(0, 1.2f, face + 0.019f), new Vector3(0.17f, 0.09f, 0.001f));
                        mb.Set(S.GlossPaint, new Color(0.95f, 0.8f, 0.25f)); mb.Push(Matrix4x4.TRS(new Vector3(0, 1.19f, face + 0.02f), Quaternion.identity, new Vector3(1, 1, 0.3f))); mb.Ellipsoid(new Vector3(0.005f, 0, 0), new Vector3(0.03f, 0.022f, 0.02f), 10, 6); mb.Sphere(new Vector3(-0.022f, 0.022f, 0), 0.016f, 8, 5); mb.Pop();
                        mb.Set(S.GlossPaint, new Color(0.95f, 0.5f, 0.15f)); mb.Box(new Vector3(-0.042f, 1.21f, face + 0.022f), new Vector3(0.014f, 0.006f, 0.004f));
                        break;
                    }
            }
        }
    }
}
