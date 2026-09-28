using System;
using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Volume without floor: big rooms get things hanging overhead that fill the air and leave the floor free. Brass
    /// birdcages holding bone birds hang in the libraries, studies, doll and trophy rooms; long heraldic banners hang down
    /// the walls of the halls, chapels and galleries. Everything hangs at least 2.3 m above the floor, clear of tall
    /// furniture and of the chandeliers on the room's centre line.
    /// </summary>
    public sealed partial class MansionView
    {
        void HangingDecor(RoomView rv, MeshBuilder mb)
        {
            var r = rv.Room; var R = r.Rect; float ceilRel = rv.CeilY - rv.FloorY;
            if (r.Floor < 0 || r.Void || R.Area < 40f || ceilRel < 3.4f) return;
            bool cages = r.Type == RoomType.Library || r.Type == RoomType.Study || r.Type == RoomType.DollRoom || r.Type == RoomType.TrophyRoom || r.Type == RoomType.Lounge
                         || r.Type == RoomType.Parlor || r.Type == RoomType.MusicRoom || r.Type == RoomType.Greenhouse || r.Type == RoomType.TeaRoom || r.Type == RoomType.Archive;
            bool banners = (r.Type == RoomType.GrandHall || r.Type == RoomType.Chapel || r.Type == RoomType.Gallery || r.Type == RoomType.Theater || r.Type == RoomType.Dining || r.Type == RoomType.TrophyRoom) && ceilRel >= 4.0f;
            if (!cages && !banners) return;
            var rnd = new System.Random(r.Id * 977 + 13);
            bool alongX = R.W >= R.D; float len = Mathf.Max(R.W, R.D), wid = Mathf.Min(R.W, R.D);
            int n = Math.Max(1, (int)Math.Round(len / 7.5f));          // the chandelier rhythm along the centre line
            if (cages)
            {
                // a pair of cages flanking the first chandelier, mirror-symmetric across the long axis
                float t = 0.5f / n + (n == 1 ? 0.22f : 0.5f / n);
                foreach (float side in new[] { -1f, 1f })
                {
                    float along = (alongX ? R.x0 : R.z0) + t * len, across = (alongX ? R.CZ : R.CX) + side * wid * 0.24f;
                    var p = alongX ? new Vector3(along, 0, across) : new Vector3(across, 0, along);
                    if (TallUnder(r, p.x, p.z, 0.35f, 2.2f)) continue;
                    float bottom = rv.FloorY + Mathf.Min(ceilRel - 0.9f, 2.45f + (float)rnd.NextDouble() * 0.2f);
                    HangingCage(mb, new Vector3(p.x, bottom, p.z), rv.CeilY);
                    _decorCount++;
                }
            }
            if (banners)
            {
                // banners hang from the ceiling a metre off both long walls, facing into the room, never before a window
                int nb = Math.Max(1, (int)(len / 4.2f));
                Color cloth = Color.Lerp(rv.Pal.Accent2, new Color(0.25f, 0.05f, 0.06f), 0.55f) * 0.9f;
                for (int i = 0; i < nb; i++)
                {
                    float along = (alongX ? R.x0 : R.z0) + (i + 0.5f) / nb * len;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float across = side < 0 ? (alongX ? R.z0 : R.x0) + 0.95f : (alongX ? R.z1 : R.x1) - 0.95f;
                        var p = alongX ? new Vector3(along, 0, across) : new Vector3(across, 0, along);
                        var inward = alongX ? new Vector3(0, 0, -side) : new Vector3(-side, 0, 0);
                        bool nearWin = false;
                        foreach (var w in Windows)
                            if (w.Room == r.Id && Vector3.Distance(new Vector3(w.Center.x, 0, w.Center.z), p - inward * 0.95f) < w.W * 0.5f + 0.6f) nearWin = true;
                        if (nearWin || TallUnder(r, p.x, p.z, 0.45f, 2.6f)) continue;
                        float top = rv.CeilY - 0.35f, length = Mathf.Min(2.3f, top - (rv.FloorY + 2.9f));
                        if (length < 1.0f) continue;
                        Banner(mb, new Vector3(p.x, top, p.z), inward, 0.72f, length, cloth, rv.CeilY);
                        _decorCount++;
                    }
                }
            }
        }

        /// <summary>True when furniture taller than 'h' stands within 'rad' of (x, z).</summary>
        bool TallUnder(Room r, float x, float z, float rad, float h)
        {
            foreach (int fid in r.Furniture)
            {
                var f = Layout.Furniture[fid]; if (f.H < h || f.Type == "Chandelier") continue;
                NavGrid.GetFootprint(f, rad, out var fp);
                if (fp.Contains(x, z)) return true;
            }
            return false;
        }

        /// <summary>A brass birdcage on a chain: domed top, fourteen bars, a perch and a small bird made of bones.</summary>
        static void HangingCage(MeshBuilder mb, Vector3 bottom, float ceilY)
        {
            float h = 0.55f, rr = 0.2f;
            var brass = new Color(0.66f, 0.52f, 0.33f);
            mb.Set(S.Iron, new Color(0.18f, 0.17f, 0.16f));
            mb.Rod(bottom + Vector3.up * (h + 0.12f), new Vector3(bottom.x, ceilY, bottom.z), 0.006f, 4, false);
            mb.Set(S.Brass, brass);
            mb.Push(bottom, 0);
            mb.Cyl(Vector3.zero, rr + 0.012f, 0.035f, 16);
            mb.Torus(new Vector3(0, h * 0.5f, 0), rr, 0.006f, 20, 4);
            for (int i = 0; i < 14; i++) { float a = i / 14f * Mathf.PI * 2; mb.Rod(new Vector3(Mathf.Cos(a) * rr, 0.03f, Mathf.Sin(a) * rr), new Vector3(Mathf.Cos(a) * rr, h * 0.72f, Mathf.Sin(a) * rr), 0.004f, 4, false); }
            mb.Lathe(new[] { new Vector2(rr, h * 0.72f), new Vector2(rr * 0.85f, h * 0.86f), new Vector2(rr * 0.45f, h * 0.97f), new Vector2(0.02f, h), new Vector2(0.001f, h + 0.02f) }, 16);
            mb.Torus(new Vector3(0, h + 0.07f, 0), 0.04f, 0.007f, 12, 4);
            mb.Rod(new Vector3(-rr * 0.8f, h * 0.35f, 0), new Vector3(rr * 0.8f, h * 0.35f, 0), 0.006f, 5, false);   // perch
            // the bird: a bone skeleton, head bowed
            mb.Set(S.Bone, new Color(0.9f, 0.86f, 0.76f));
            float yb = h * 0.35f + 0.035f;
            mb.Ellipsoid(new Vector3(0, yb, 0), new Vector3(0.035f, 0.03f, 0.055f), 8, 6);
            mb.Sphere(new Vector3(0, yb + 0.03f, 0.055f), 0.022f, 8, 6);
            mb.Rod(new Vector3(0, yb + 0.02f, 0.075f), new Vector3(0, yb + 0.005f, 0.105f), 0.004f, 4, false);
            for (int k = 0; k < 4; k++) mb.Rod(new Vector3(-0.03f, yb - 0.01f + k * 0.006f, -0.03f + k * 0.018f), new Vector3(0.03f, yb - 0.01f + k * 0.006f, -0.03f + k * 0.018f), 0.002f, 3, false);   // ribs
            mb.Rod(new Vector3(0, yb, -0.05f), new Vector3(0, yb - 0.03f, -0.1f), 0.003f, 3, false);   // tail
            mb.Pop();
        }

        /// <summary>A long heraldic banner: an iron rod on two chains, heavy cloth with a gilt border and a swallowtail, an
        /// eye embroidered in gold. 'n' is the direction it faces.</summary>
        static void Banner(MeshBuilder mb, Vector3 top, Vector3 n, float w, float len, Color cloth, float ceilY)
        {
            var right = Vector3.Cross(Vector3.up, n).normalized;
            mb.Set(S.Iron, new Color(0.15f, 0.14f, 0.13f));
            mb.Rod(top - right * (w * 0.5f + 0.06f), top + right * (w * 0.5f + 0.06f), 0.012f, 6, true);
            foreach (float s in new[] { -1f, 1f })
            {
                mb.Rod(top + right * s * (w * 0.45f), new Vector3(top.x, ceilY, top.z) + right * s * 0.05f, 0.004f, 3, false);
                mb.Sphere(top + right * s * (w * 0.5f + 0.07f), 0.022f, 6, 4);
            }
            // cloth: gently rippled strips, both faces
            mb.Set(S.Velvet, cloth);
            int seg = 6; float tail = 0.28f;
            for (int k = 0; k < seg; k++)
            {
                float y0 = -len * k / seg, y1 = -len * (k + 1) / seg;
                float z0 = Mathf.Sin(k * 1.3f) * 0.02f, z1 = Mathf.Sin((k + 1) * 1.3f) * 0.02f;
                var a = top + Vector3.up * y0 - right * w * 0.5f + n * (0.01f + z0); var b = top + Vector3.up * y0 + right * w * 0.5f + n * (0.01f + z0);
                var c = top + Vector3.up * y1 + right * w * 0.5f + n * (0.01f + z1); var d = top + Vector3.up * y1 - right * w * 0.5f + n * (0.01f + z1);
                mb.QuadAuto(a, b, c, d, n); mb.QuadAuto(a, b, c, d, -n);
            }
            // swallowtail
            var bl = top + Vector3.up * -len - right * w * 0.5f + n * 0.01f; var br = top + Vector3.up * -len + right * w * 0.5f + n * 0.01f; var bc = top + Vector3.up * -len + n * 0.01f;
            var tl = bl + Vector3.down * tail; var tr = br + Vector3.down * tail; var notch = bc + Vector3.down * tail * 0.15f;
            mb.QuadAuto(bl, bc, notch, tl, n); mb.QuadAuto(bl, bc, notch, tl, -n);
            mb.QuadAuto(bc, br, tr, notch, n); mb.QuadAuto(bc, br, tr, notch, -n);
            // gilt border and an embroidered eye
            mb.Set(S.Gold, new Color(0.72f, 0.58f, 0.36f));
            foreach (float s in new[] { -1f, 1f })
                mb.Box(top + Vector3.up * (-len * 0.5f) + right * s * (w * 0.5f - 0.03f) + n * 0.03f, new Vector3(Mathf.Abs(right.x) * 0.015f + Mathf.Abs(n.x) * 0.006f, len, Mathf.Abs(right.z) * 0.015f + Mathf.Abs(n.z) * 0.006f));
            var eye = top + Vector3.up * (-len * 0.38f) + n * 0.035f;
            mb.Push(Matrix4x4.TRS(eye, Quaternion.LookRotation(n, Vector3.up), Vector3.one));
            var pts = new List<Vector3>();
            for (int k = 0; k <= 20; k++) { float t = k / 20f * Mathf.PI * 2; pts.Add(new Vector3(Mathf.Cos(t) * w * 0.3f, Mathf.Sin(t) * w * 0.12f * (0.6f + 0.4f * Mathf.Abs(Mathf.Cos(t))), 0)); }
            mb.Tube(pts, 0.008f, 4);
            mb.Push(Vector3.zero, Quaternion.Euler(90, 0, 0), Vector3.one); mb.Torus(Vector3.zero, w * 0.08f, 0.008f, 14, 4); mb.Pop();   // iris ring, in the cloth plane
            mb.Set(S.Obsidian, Color.white); mb.Ellipsoid(new Vector3(0, 0, 0.004f), new Vector3(w * 0.05f, w * 0.05f, 0.004f), 10, 6);
            mb.Pop();
        }
    }
}
