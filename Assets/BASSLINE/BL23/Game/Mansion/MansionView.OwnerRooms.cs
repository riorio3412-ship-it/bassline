using System;
using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// The signature set of each resident's room (5–10 pieces: wall, floor, bed, surfaces, ceiling) and the one quiet
    /// hint of their secret — never a case clue by itself, just something that fits the person once you know them.
    /// </summary>
    public sealed partial class MansionView
    {
        /// <summary>Surface points holding owner decor, per room: the dressing pass keeps its props off them.</summary>
        readonly Dictionary<int, List<Vector2>> _ownerAvoid = new Dictionary<int, List<Vector2>>();

        void Avoid(OwnCtx o, Vector3 p)
        {
            if (!_ownerAvoid.TryGetValue(o.Rv.Room.Id, out var l)) _ownerAvoid[o.Rv.Room.Id] = l = new List<Vector2>();
            l.Add(new Vector2(p.x, p.z));
        }

        // ------------------------------------------------------------------ placement helpers
        void OnWall(OwnCtx o, float halfW, float yc, float halfH, Action<MeshBuilder> build)
        {
            if (!TakeWall(o, halfW, yc - halfH, yc + halfH, out var p, out var n)) return;
            o.Mb.Push(WallFrame(p, n, o.Fy + yc)); build(o.Mb); o.Mb.Pop();
        }

        /// <summary>The wall above the bed's head (the room's focal piece), else any free wall.</summary>
        void AboveBed(OwnCtx o, float halfW, float h, Action<MeshBuilder> build)
        {
            var f = o.Bed; var rv = o.Rv;
            if (f != null && o.Own.Bed != 1)
            {
                float yr = f.Yaw * Mathf.Deg2Rad; var fwd = new Vector3(Mathf.Sin(yr), 0, Mathf.Cos(yr));
                var back = new Vector3(f.Pos.x, o.Fy, f.Pos.z) - fwd * (f.D * 0.5f + 0.03f);
                bool wall = _grids[rv.Room.Floor].At(back.x - fwd.x * 0.3f, back.z - fwd.z * 0.3f) != rv.Room.Id;
                float head = o.Own.Bed == 2 ? 1.22f : o.Own.Bed == 3 ? 1.0f : o.Own.Bed == 4 ? 1.3f : 1.45f;
                float yc = Mathf.Min(head + 0.18f + h / 2, rv.CeilY - rv.FloorY - 0.45f - h / 2);
                if (wall && yc - h / 2 > head + 0.05f && ReservedFree(rv, back, fwd, halfW + 0.05f))
                {
                    rv.WallReserved.Add(new Vector4(back.x, back.z, halfW + 0.05f, 0)); o.Placed++;
                    o.Mb.Push(WallFrame(back + fwd * 0.02f, fwd, o.Fy + yc));   // the bed stands 2 cm off the wall: back sits just inside it build(o.Mb); o.Mb.Pop();
                    return;
                }
            }
            OnWall(o, halfW, 1.75f, h / 2, build);
        }

        void OnFloor(OwnCtx o, float rad, Action<MeshBuilder> build, float tallH = 0f, bool cornerOnly = false)
        {
            if (!TakeFloor(o, rad, out var p, out float yaw, false)) return;   // corners first, then any free wall (cornerOnly kept for callers)
            o.Mb.Push(p, yaw); build(o.Mb); o.Mb.Pop();
            if (tallH > 1.2f) AddDecorCollider(o.Rv, p + Vector3.up * tallH * 0.5f, new Vector3(rad * 1.6f, tallH, rad * 1.6f), "OwnerDecor");
        }

        /// <summary>On a furniture top at local (lx, lz) (x across, z toward the front), clear of kernel items.</summary>
        bool OnTopOf(OwnCtx o, Furniture f, float lx, float lz, Action<MeshBuilder> build)
        {
            if (f == null) return false;
            float top = f.H;
            if (FurnitureGo.TryGetValue(f.Id, out var go) && go != null) top = SurfaceTop(f, go, o.Rv);
            var p = OnTop(f, o.Fy, top, lx, lz);
            if (!ItemClear(o.Rv, p, 0.05f)) return false;
            o.Mb.Push(Matrix4x4.TRS(p, Quaternion.Euler(0, f.Yaw, 0), Vector3.one)); build(o.Mb); o.Mb.Pop();
            Avoid(o, p); o.Placed++;
            return true;
        }

        /// <summary>On the bed (local: x across, z from the head -D/2 to the foot +D/2) at the mattress top.</summary>
        void OnBed(OwnCtx o, float lx, float lz, Action<MeshBuilder> build)
        {
            var f = o.Bed; if (f == null) return;
            float top = o.Own.Bed == 0 ? 0.6f : FurnitureFactory.OwnerBedTop(o.Own.Bed);
            var m = TopFrame(f, o.Fy, top + 0.06f);
            var wp = m.MultiplyPoint3x4(new Vector3(lx, 0, lz));
            if (!ItemClear(o.Rv, wp, 0.05f)) return;
            o.Mb.Push(m); o.Mb.Push(new Vector3(lx, 0, lz), 0); build(o.Mb); o.Mb.Pop(); o.Mb.Pop();
            o.Placed++;
        }

        Furniture Nightstand(OwnCtx o) => FindIn(o.Rv, "Nightstand");

        // ------------------------------------------------------------------ generic pieces (built in a wall frame: +Z out of the wall)
        static void Frame(MeshBuilder mb, float w, float h, Color c, float t = 0.035f, int slot = S.WoodDark)
        {
            mb.Set(slot, c);
            mb.Box(new Vector3(0, h / 2, 0.015f), new Vector3(w, t, 0.03f)); mb.Box(new Vector3(0, -h / 2, 0.015f), new Vector3(w, t, 0.03f));
            mb.Box(new Vector3(-w / 2, 0, 0.015f), new Vector3(t, h, 0.03f)); mb.Box(new Vector3(w / 2, 0, 0.015f), new Vector3(t, h, 0.03f));
        }

        static void Sheet(MeshBuilder mb, float x, float y, float w, float h, Color c, float z = 0.004f, float roll = 0f)
        {
            mb.Set(S.Paper, c); mb.Push(Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0, 0, roll), Vector3.one)); mb.Box(Vector3.zero, new Vector3(w, h, 0.003f)); mb.Pop();
        }

        static void FaceDisc(MeshBuilder mb, Vector3 c, float r, int seg = 18)
        {
            mb.Push(Matrix4x4.TRS(c, Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Cyl(Vector3.zero, r, 0.004f, seg); mb.Pop();
        }

        /// <summary>A print or poster: ground, a motif, optional frame. motif 0 type bars, 1 disc, 2 star, 3 figure, 4 blot, 5 photo grid, 6 map.</summary>
        static void Poster(MeshBuilder mb, float w, float h, Color bg, Color ink, int motif, int frame, Color frameC, System.Random rnd)
        {
            Sheet(mb, 0, 0, w, h, bg, 0.006f);
            float z = 0.009f;
            switch (motif)
            {
                case 0:
                    mb.Set(S.Paper, ink); mb.Box(new Vector3(0, h * 0.3f, z), new Vector3(w * 0.8f, h * 0.14f, 0.002f));
                    for (int k = 0; k < 4; k++) mb.Box(new Vector3(-w * 0.1f * (k % 2), -h * 0.05f - k * h * 0.09f, z), new Vector3(w * (0.6f - k * 0.08f), h * 0.03f, 0.002f));
                    break;
                case 1:
                    mb.Set(S.Paper, ink); FaceDisc(mb, new Vector3(0, h * 0.08f, z), Mathf.Min(w, h) * 0.3f);
                    mb.Set(S.Paper, bg); FaceDisc(mb, new Vector3(0, h * 0.08f, z + 0.003f), Mathf.Min(w, h) * 0.06f);
                    mb.Set(S.Paper, ink); mb.Box(new Vector3(0, -h * 0.36f, z), new Vector3(w * 0.7f, h * 0.06f, 0.002f));
                    break;
                case 2:
                    mb.Set(S.Paper, ink); mb.Push(new Vector3(0, h * 0.06f, z), 0); DoorView.Relief(mb, DoorView.Star(5, Mathf.Min(w, h) * 0.3f, Mathf.Min(w, h) * 0.13f), 0.003f); mb.Pop();
                    mb.Box(new Vector3(0, -h * 0.38f, z), new Vector3(w * 0.6f, h * 0.05f, 0.002f));
                    break;
                case 3:
                    mb.Set(S.Paper, ink);
                    mb.Push(Matrix4x4.TRS(new Vector3(0, h * 0.16f, z), Quaternion.identity, new Vector3(1, 1, 0.05f))); mb.Sphere(Vector3.zero, w * 0.13f, 12, 8, 1.2f); mb.Pop();
                    mb.Push(new Vector3(0, 0, z), 0); DoorView.Relief(mb, new List<Vector2> { new Vector2(-w * 0.3f, -h * 0.42f), new Vector2(w * 0.3f, -h * 0.42f), new Vector2(w * 0.16f, h * 0.02f), new Vector2(-w * 0.16f, h * 0.02f) }, 0.002f); mb.Pop();
                    break;
                case 4:
                    mb.Set(S.Paper, ink);
                    for (int k = 0; k < 7; k++)
                    {
                        float x = 0.02f + (float)rnd.NextDouble() * w * 0.28f, y = ((float)rnd.NextDouble() - 0.5f) * h * 0.6f; var r = new Vector3(0.02f + (float)rnd.NextDouble() * w * 0.12f, 0.02f + (float)rnd.NextDouble() * h * 0.1f, 0.002f);
                        foreach (float s in new[] { -1f, 1f }) mb.Ellipsoid(new Vector3(s * x, y, z), r, 10, 4);
                    }
                    break;
                case 5:
                    for (int r = 0; r < 3; r++) for (int k = 0; k < 4; k++)
                        {
                            float x = -w * 0.36f + k * w * 0.24f, y = h * 0.3f - r * h * 0.3f;
                            Sheet(mb, x, y, w * 0.2f, h * 0.24f, new Color(0.95f, 0.94f, 0.9f), z, ((k + r) % 3 - 1) * 4f);
                            Sheet(mb, x, y + h * 0.02f, w * 0.16f, h * 0.16f, Color.Lerp(ink, Color.HSVToRGB((float)rnd.NextDouble(), 0.3f, 0.5f), 0.5f), z + 0.002f, ((k + r) % 3 - 1) * 4f);
                        }
                    break;
                case 6:
                    {
                        mb.Set(S.Paper, ink);
                        var pts = new List<Vector3>(); float x = -w * 0.4f, y = -h * 0.3f;
                        for (int k = 0; k < 14; k++) { pts.Add(new Vector3(x, y, z)); x += w * 0.06f; y += ((float)rnd.NextDouble() - 0.45f) * h * 0.2f; y = Mathf.Clamp(y, -h * 0.4f, h * 0.4f); }
                        mb.Tube(pts, 0.004f, 4);
                        for (int k = 0; k < 6; k++) { var q = pts[k * 2]; mb.Box(q + new Vector3(0, 0, 0.002f), new Vector3(0.05f, 0.04f, 0.002f)); }
                        mb.Set(S.GlossPaint, new Color(0.7f, 0.1f, 0.1f)); FaceDisc(mb, pts[pts.Count - 3] + new Vector3(0, 0, 0.003f), 0.02f, 10);
                        break;
                    }
            }
            if (frame == 1) Frame(mb, w + 0.04f, h + 0.04f, frameC, 0.025f, S.WoodDark);
            else if (frame == 2) Frame(mb, w + 0.07f, h + 0.07f, frameC, 0.05f, S.Gold);
            else { mb.Set(S.Paper, new Color(0.85f, 0.82f, 0.6f)); foreach (float sx in new[] { -1f, 1f }) mb.Box(new Vector3(sx * (w / 2 - 0.02f), h / 2 - 0.01f, 0.01f), new Vector3(0.06f, 0.025f, 0.002f)); }
        }

        /// <summary>A wall shelf (plank + two brackets) at the frame origin, 'w' long; things stand on it from y = 0.02.</summary>
        static void Shelf(MeshBuilder mb, float w, Color wood)
        {
            mb.Set(S.WoodDark, wood); mb.Box(new Vector3(0, 0, 0.11f), new Vector3(w, 0.03f, 0.22f));
            mb.Set(S.Iron, new Color(0.15f, 0.14f, 0.13f)); foreach (float s in new[] { -1f, 1f }) mb.Box(new Vector3(s * (w / 2 - 0.08f), -0.08f, 0.07f), new Vector3(0.02f, 0.14f, 0.12f));
        }

        static void BookRow(MeshBuilder mb, float x0, float x1, float y, float z, System.Random rnd, Color[] cols, float lean = 0f)
        {
            float x = x0;
            while (x < x1 - 0.03f)
            {
                float t = 0.025f + (float)rnd.NextDouble() * 0.03f, h = 0.16f + (float)rnd.NextDouble() * 0.08f, d = 0.13f + (float)rnd.NextDouble() * 0.05f;
                mb.Set(S.Books, cols[rnd.Next(cols.Length)] * (0.8f + (float)rnd.NextDouble() * 0.35f));
                mb.Push(Matrix4x4.TRS(new Vector3(x + t / 2, y + h / 2, z), Quaternion.Euler(0, 0, lean), Vector3.one)); mb.BevelBox(Vector3.zero, new Vector3(t, h, d), 0.003f); mb.Pop();
                x += t + 0.002f;
            }
        }

        static void BoxStack(MeshBuilder mb, int n, float w, float d, float h, Color c, bool labels, System.Random rnd, float jitter = 4f)
        {
            float y = 0;
            for (int k = 0; k < n; k++)
            {
                mb.Push(new Vector3(0, y, 0), ((float)rnd.NextDouble() - 0.5f) * jitter * 2f);
                mb.Set(S.Paper, c * (0.9f + (float)rnd.NextDouble() * 0.2f)); mb.Box(new Vector3(0, h / 2, 0), new Vector3(w, h, d));
                mb.Set(S.Paper, c * 0.7f); mb.Box(new Vector3(0, h - 0.004f, 0), new Vector3(w + 0.004f, 0.01f, 0.05f));
                if (labels) { mb.Set(S.Paper, new Color(0.95f, 0.94f, 0.9f)); mb.Box(new Vector3(0, h * 0.55f, d / 2 + 0.002f), new Vector3(w * 0.4f, h * 0.3f, 0.002f)); }
                mb.Pop();
                y += h;
            }
        }

        static void Garment(MeshBuilder mb, Color c, float w = 0.5f, float d = 0.42f, bool crumpled = false, System.Random rnd = null)
        {
            mb.Set(S.Cloth, c);
            if (!crumpled) { mb.BevelBox(new Vector3(0, 0.015f, 0), new Vector3(w, 0.03f, d), 0.012f); foreach (float s in new[] { -1f, 1f }) mb.BevelBox(new Vector3(s * (w / 2 - 0.06f), 0.032f, 0.02f), new Vector3(0.1f, 0.012f, d * 0.8f), 0.005f); return; }
            for (int k = 0; k < 5; k++) mb.Ellipsoid(new Vector3(((float)rnd.NextDouble() - 0.5f) * w * 0.6f, 0.03f + (float)rnd.NextDouble() * 0.03f, ((float)rnd.NextDouble() - 0.5f) * d * 0.6f), new Vector3(w * 0.25f, 0.04f, d * 0.25f), 9, 5);
        }

        static void Plush(MeshBuilder mb, Color c, float s)
        {
            mb.Set(S.Velvet, c);
            mb.Sphere(new Vector3(0, s * 0.35f, 0), s * 0.35f, 12, 8, 0.9f); mb.Sphere(new Vector3(0, s * 0.85f, 0.02f), s * 0.24f, 10, 7);
            foreach (float x in new[] { -1f, 1f }) { mb.Sphere(new Vector3(x * s * 0.16f, s * 1.08f, 0), s * 0.08f, 6, 4); mb.Sphere(new Vector3(x * s * 0.3f, s * 0.12f, s * 0.2f), s * 0.1f, 6, 4); }
            mb.Set(S.Obsidian, Color.white); foreach (float x in new[] { -1f, 1f }) mb.Sphere(new Vector3(x * s * 0.08f, s * 0.88f, s * 0.24f), s * 0.03f, 5, 3);
        }

        // ------------------------------------------------------------------ the sets
        void OwnerSet(OwnCtx o)
        {
            var mb = o.Mb; var pal = o.Pal; var rnd = o.Rnd; var own = o.Own; var rv = o.Rv; float fy = o.Fy; int circ = rv.Room.Circuit;
            Color wood = Color.Lerp(Color.white, pal.Wood * 2.2f, 0.45f), ivory = new Color(0.93f, 0.9f, 0.82f), ink = new Color(0.1f, 0.09f, 0.09f);
            Color[] booksWarm = { new Color(0.45f, 0.12f, 0.1f), new Color(0.2f, 0.28f, 0.2f), new Color(0.18f, 0.2f, 0.35f), new Color(0.5f, 0.38f, 0.2f), new Color(0.3f, 0.2f, 0.25f) };
            var ns = Nightstand(o);
            switch (own.Id)
            {
                case "P01":   // 김민혁: walks, bread, co-op games — and the loop he alone remembers
                    AboveBed(o, 0.45f, 0.6f, m => Poster(m, 0.8f, 0.56f, new Color(0.9f, 0.85f, 0.72f), new Color(0.35f, 0.25f, 0.18f), 6, 1, wood, rnd));
                    OnWall(o, 0.45f, 1.55f, 0.3f, m => { Shelf(m, 0.8f, wood); float y = 0.015f; for (int k = 0; k < 4; k++) { m.Set(S.Paper, Color.HSVToRGB((0.02f + k * 0.21f) % 1f, 0.5f, 0.55f)); m.Push(new Vector3(-0.1f, y, 0.12f), k * 5f - 8f); m.Box(new Vector3(0, 0.03f, 0), new Vector3(0.34f - k * 0.03f, 0.06f, 0.18f)); m.Pop(); y += 0.06f; } BookRow(m, 0.12f, 0.38f, 0.015f, 0.11f, rnd, booksWarm); });
                    OnFloor(o, 0.24f, m => { m.Set(S.WoodDark, wood); m.Rod(Vector3.zero, Vector3.up * 1.75f, 0.022f, 8); foreach (float a in new[] { 0f, 120f, 240f }) m.Rod(Vector3.up * 0.04f, Quaternion.Euler(0, a, 0) * new Vector3(0.22f, 0, 0), 0.018f, 5); foreach (float a in new[] { 30f, 150f, 270f }) m.Rod(Vector3.up * 1.62f, Vector3.up * 1.7f + Quaternion.Euler(0, a, 0) * new Vector3(0.14f, 0, 0), 0.012f, 5); m.Set(S.Cloth, new Color(0.62f, 0.5f, 0.36f)); m.Push(Matrix4x4.TRS(new Vector3(0.1f, 1.25f, 0), Quaternion.identity, Vector3.one)); m.Lathe(new[] { new Vector2(0.03f, 0.45f), new Vector2(0.14f, 0.2f), new Vector2(0.17f, -0.25f) }, 10); m.Pop(); m.Set(S.Cloth, own.Sig); m.Rod(new Vector3(-0.1f, 1.62f, 0.02f), new Vector3(-0.14f, 1.0f, 0.06f), 0.03f, 6); }, 1.8f);
                    OnFloor(o, 0.22f, m => { m.Set(S.WoodLight, new Color(0.7f, 0.55f, 0.35f)); m.Lathe(new[] { new Vector2(0.16f, 0), new Vector2(0.2f, 0.18f), new Vector2(0.21f, 0.2f) }, 14, true); m.Set(S.Clay, new Color(0.82f, 0.6f, 0.34f)); for (int k = 0; k < 4; k++) { m.Push(Matrix4x4.TRS(new Vector3(-0.08f + k * 0.05f, 0.24f, 0), Quaternion.Euler(0, k * 30f, 55f + k * 8f), Vector3.one)); m.Ellipsoid(Vector3.zero, new Vector3(0.035f, 0.2f, 0.035f), 8, 5); m.Pop(); } });
                    OnBed(o, 0.2f, 0.62f, m => Garment(m, new Color(0.7f, 0.58f, 0.44f), 0.46f, 0.36f));
                    // the secret: tally marks scratched into the paper beside the bed head, groups of five, far too many
                    OnWall(o, 0.28f, 1.4f, 0.2f, m => { m.Set(S.Obsidian, new Color(0.12f, 0.1f, 0.09f)); for (int g = 0; g < 7; g++) { float gx = -0.22f + (g % 4) * 0.13f, gy = 0.1f - (g / 4) * 0.16f; for (int k = 0; k < 4; k++) m.Box(new Vector3(gx + k * 0.022f, gy, 0.004f), new Vector3(0.004f, 0.1f, 0.002f)); m.Push(Matrix4x4.TRS(new Vector3(gx + 0.033f, gy, 0.005f), Quaternion.Euler(0, 0, -55f), Vector3.one)); m.Box(Vector3.zero, new Vector3(0.004f, 0.13f, 0.002f)); m.Pop(); } });
                    break;

                case "P02":   // 김진우: sweets, psychology, watching people — and the reply at 11:40
                    OnFloor(o, 0.24f, m => { m.Set(S.Glass, Color.white); m.Lathe(new[] { new Vector2(0.14f, 0), new Vector2(0.18f, 0.12f), new Vector2(0.18f, 0.46f), new Vector2(0.09f, 0.52f), new Vector2(0.1f, 0.58f) }, 16); m.Set(S.Brass, Color.white); m.Cyl(new Vector3(0, 0.58f, 0), 0.11f, 0.03f, 14); m.Sphere(new Vector3(0, 0.63f, 0), 0.03f, 8, 5); for (int k = 0; k < 40; k++) { m.Set(S.GlossPaint, Color.HSVToRGB((k * 0.137f) % 1f, 0.6f, 0.75f)); float a = k * 2.4f, r = 0.04f + (k % 5) * 0.025f; m.Sphere(new Vector3(Mathf.Cos(a) * r, 0.04f + (k / 5) * 0.045f, Mathf.Sin(a) * r), 0.024f, 6, 4); } }, 0f, true);
                    AboveBed(o, 0.62f, 0.42f, m => { for (int k = 0; k < 3; k++) { m.Push(new Vector3((k - 1) * 0.42f, 0, 0), 0); Poster(m, 0.32f, 0.4f, new Color(0.9f, 0.87f, 0.8f), new Color(0.06f, 0.05f, 0.06f), 4, 1, new Color(0.1f, 0.08f, 0.08f), new System.Random(7 + k)); m.Pop(); } });
                    OnWall(o, 0.45f, 1.6f, 0.3f, m => { Shelf(m, 0.8f, wood); BookRow(m, -0.38f, 0.3f, 0.015f, 0.11f, rnd, new[] { new Color(0.2f, 0.18f, 0.3f), new Color(0.35f, 0.1f, 0.12f), new Color(0.15f, 0.15f, 0.15f) }); m.Set(S.Porcelain, ivory); m.Push(new Vector3(0.34f, 0.015f, 0.11f), 0); m.Lathe(new[] { new Vector2(0.03f, 0), new Vector2(0.035f, 0.05f), new Vector2(0.05f, 0.1f), new Vector2(0.04f, 0.16f), new Vector2(0.001f, 0.18f) }, 10); m.Pop(); });
                    OnFloor(o, 0.2f, m => { for (int k = 0; k < 7; k++) { m.Set(S.Paper, Color.Lerp(new Color(0.84f, 0.82f, 0.75f), new Color(0.7f, 0.68f, 0.6f), (k % 3) / 3f)); m.Push(new Vector3(0, k * 0.02f, 0), (k % 3 - 1) * 6f); m.Box(new Vector3(0, 0.01f, 0), new Vector3(0.28f, 0.018f, 0.38f)); m.Pop(); } });
                    OnBed(o, -0.2f, 0.5f, m => { m.Set(S.Books, new Color(0.25f, 0.2f, 0.32f)); m.Push(Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 20f, 0), Vector3.one)); foreach (float s in new[] { -1f, 1f }) { m.Push(Matrix4x4.TRS(new Vector3(s * 0.07f, 0.015f, 0), Quaternion.Euler(0, 0, -s * 12f), Vector3.one)); m.Box(Vector3.zero, new Vector3(0.14f, 0.012f, 0.22f)); m.Pop(); } m.Pop(); });
                    // the secret: a small clock on the nightstand, stopped at 11:40, a phone laid face down beside it
                    OnTopOf(o, ns, -0.1f, 0.05f, m => { m.Set(S.Brass, new Color(0.75f, 0.62f, 0.4f)); m.Push(Matrix4x4.TRS(new Vector3(0, 0.06f, 0), Quaternion.identity, Vector3.one)); m.Push(Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90, 0, 0), Vector3.one)); m.Cyl(new Vector3(0, -0.02f, 0), 0.05f, 0.04f, 16); m.Pop(); m.Set(S.Porcelain, ivory); m.Push(Matrix4x4.TRS(new Vector3(0, 0, 0.021f), Quaternion.Euler(90, 0, 0), Vector3.one)); m.Cyl(Vector3.zero, 0.043f, 0.001f, 16); m.Pop(); m.Set(S.Obsidian, ink); m.Bar(new Vector3(0, 0, 0.024f), new Vector3(-0.028f, 0.016f, 0.024f), 0.005f, 0.002f); m.Bar(new Vector3(0, 0, 0.024f), new Vector3(0.024f, 0.0f, 0.024f), 0.003f, 0.002f); m.Pop(); m.Set(S.Brass, new Color(0.75f, 0.62f, 0.4f)); foreach (float s in new[] { -1f, 1f }) m.Rod(new Vector3(s * 0.03f, 0, 0), new Vector3(s * 0.035f, 0.02f, 0), 0.004f, 4); });
                    OnTopOf(o, ns, 0.1f, 0.07f, m => { m.Set(S.Plastic, new Color(0.05f, 0.05f, 0.06f)); m.BevelBox(new Vector3(0, 0.005f, 0), new Vector3(0.075f, 0.009f, 0.15f), 0.006f); });
                    break;

                case "P03":   // 한서윤: the rota, pens, a hamster — and a keepsake from her father's print shop
                    AboveBed(o, 0.52f, 0.7f, m => { Corkboard(m, new Vector3(0, 0.05f, 0), Quaternion.identity, pal, rnd, false); m.Set(S.GlossPaint, new Color(0.72f, 0.1f, 0.12f)); for (int k = 0; k < 9; k++) m.Box(new Vector3(-0.34f + (k % 3) * 0.38f, 0.21f - (k / 3) * 0.22f, 0.036f), new Vector3(0.14f, 0.012f, 0.002f)); });
                    OnFloor(o, 0.24f, m => { m.Set(S.WoodLight, new Color(0.6f, 0.45f, 0.3f)); m.Box(new Vector3(0, 0.02f, 0), new Vector3(0.44f, 0.04f, 0.3f)); m.Set(S.Chrome, Color.white); for (int i = 0; i < 14; i++) { float a = i / 14f * Mathf.PI * 2; m.Rod(new Vector3(Mathf.Cos(a) * 0.2f, 0.04f, Mathf.Sin(a) * 0.13f), new Vector3(Mathf.Cos(a) * 0.2f, 0.34f, Mathf.Sin(a) * 0.13f), 0.003f, 3, false); } m.Push(Matrix4x4.TRS(new Vector3(0, 0.34f, 0), Quaternion.identity, new Vector3(1, 0.3f, 0.65f))); m.Sphere(Vector3.zero, 0.2f, 12, 4); m.Pop(); m.Push(Matrix4x4.TRS(new Vector3(0.1f, 0.12f, 0), Quaternion.Euler(0, 0, 90), Vector3.one)); m.Torus(Vector3.zero, 0.07f, 0.006f, 14, 4); m.Pop(); m.Set(S.Velvet, new Color(0.85f, 0.62f, 0.38f)); m.Ellipsoid(new Vector3(-0.08f, 0.07f, 0.02f), new Vector3(0.045f, 0.035f, 0.06f), 8, 5); m.Set(S.Linen, new Color(0.9f, 0.85f, 0.7f)); m.Box(new Vector3(0, 0.045f, 0), new Vector3(0.4f, 0.01f, 0.26f)); }, 0f, true);
                    OnWall(o, 0.3f, 1.55f, 0.3f, m => { m.Set(S.Brass, Color.white); m.Rod(new Vector3(-0.12f, 0.2f, 0), new Vector3(-0.12f, 0.2f, 0.04f), 0.005f, 5); m.Set(S.Cloth, new Color(0.72f, 0.1f, 0.12f)); m.Push(Matrix4x4.TRS(new Vector3(-0.12f, 0.08f, 0.03f), Quaternion.identity, new Vector3(1, 1, 0.35f))); m.Cyl(new Vector3(0, -0.06f, 0), 0.06f, 0.1f, 14, false); m.Pop(); m.Set(S.Paper, ivory); m.Box(new Vector3(-0.12f, 0.02f, 0.052f), new Vector3(0.05f, 0.03f, 0.002f)); m.Push(new Vector3(0.14f, 0.02f, 0), 0); Poster(m, 0.24f, 0.32f, ivory, new Color(0.15f, 0.2f, 0.35f), 0, 2, pal.Trim, rnd); m.Pop(); });
                    OnFloor(o, 0.2f, m => BoxStack(m, 3, 0.36f, 0.28f, 0.22f, new Color(0.25f, 0.3f, 0.42f), true, rnd, 0f));
                    OnBed(o, 0f, 0.7f, m => { Garment(m, new Color(0.12f, 0.15f, 0.28f), 0.44f, 0.34f); m.Set(S.Cloth, new Color(0.72f, 0.1f, 0.12f)); m.Box(new Vector3(0.12f, 0.036f, 0), new Vector3(0.06f, 0.006f, 0.32f)); });
                    // the secret: a letterpress type case on the wall — lead letters in their little compartments
                    OnWall(o, 0.3f, 1.35f, 0.18f, m => { m.Set(S.WoodDark, new Color(0.5f, 0.36f, 0.24f)); m.Box(new Vector3(0, 0, 0.02f), new Vector3(0.54f, 0.32f, 0.04f)); for (int k = 0; k <= 6; k++) m.Box(new Vector3(-0.27f + k * 0.09f, 0, 0.045f), new Vector3(0.008f, 0.32f, 0.012f)); for (int r = 0; r <= 3; r++) m.Box(new Vector3(0, -0.16f + r * 0.107f, 0.045f), new Vector3(0.54f, 0.008f, 0.012f)); m.Set(S.Steel, new Color(0.45f, 0.45f, 0.48f)); for (int k = 0; k < 14; k++) m.Box(new Vector3(-0.23f + (k % 6) * 0.09f + ((float)rnd.NextDouble() - 0.5f) * 0.02f, -0.12f + (k / 6) * 0.107f, 0.046f), new Vector3(0.012f, 0.012f, 0.02f)); });
                    break;

                case "P04":   // 차도윤: old frames hung dead level, ceramics evenly spaced — and a canvas turned to the wall
                    AboveBed(o, 0.62f, 0.52f, m => { for (int k = 0; k < 3; k++) FurnitureFactory.PictureFrame(m, new Vector3((k - 1) * 0.42f, 0, 0), 0.32f, 0.42f, 4 + k, pal, true); });
                    OnWall(o, 0.45f, 1.5f, 0.28f, m => { Shelf(m, 0.8f, wood); for (int k = 0; k < 3; k++) { m.Set(k == 1 ? S.Porcelain : S.Ceramic, k == 1 ? ivory : new Color(0.55f, 0.6f, 0.62f)); m.Push(new Vector3((k - 1) * 0.26f, 0.015f, 0.11f), 0); m.Lathe(new[] { new Vector2(0.03f, 0), new Vector2(0.06f, 0.06f), new Vector2(0.03f, 0.16f), new Vector2(0.035f, 0.2f) }, 14, true); m.Pop(); } });
                    OnFloor(o, 0.22f, m => { for (int k = 0; k < 3; k++) { m.Set(S.Paper, new Color(0.62f, 0.58f, 0.5f)); m.Box(new Vector3(0, 0.12f + k * 0.24f, 0), new Vector3(0.4f, 0.24f, 0.3f)); m.Set(S.Paper, new Color(0.5f, 0.46f, 0.4f)); m.Box(new Vector3(0, 0.235f + k * 0.24f, 0), new Vector3(0.405f, 0.012f, 0.305f)); m.Set(S.Paper, ivory); m.Box(new Vector3(0, 0.12f + k * 0.24f, 0.151f), new Vector3(0.12f, 0.06f, 0.002f)); } }, 0f);
                    // the secret: a stretched canvas leaning in the corner with its face to the wall — only the bare back shows
                    OnFloor(o, 0.3f, m => { m.Push(Matrix4x4.TRS(new Vector3(0, 0, -0.12f), Quaternion.Euler(-10f, 0, 0), Vector3.one)); m.Set(S.Linen, new Color(0.8f, 0.76f, 0.66f)); m.Box(new Vector3(0, 0.55f, -0.012f), new Vector3(0.62f, 1.0f, 0.004f)); m.Set(S.WoodLight, new Color(0.72f, 0.6f, 0.42f)); m.Box(new Vector3(0, 1.03f, 0.008f), new Vector3(0.62f, 0.04f, 0.03f)); m.Box(new Vector3(0, 0.07f, 0.008f), new Vector3(0.62f, 0.04f, 0.03f)); m.Box(new Vector3(-0.29f, 0.55f, 0.008f), new Vector3(0.04f, 1.0f, 0.03f)); m.Box(new Vector3(0.29f, 0.55f, 0.008f), new Vector3(0.04f, 1.0f, 0.03f)); m.Box(new Vector3(0, 0.55f, 0.01f), new Vector3(0.03f, 0.96f, 0.02f)); m.Pop(); }, 1.1f, true);
                    break;

                case "P05":   // 백이현: the campaign, gold, the pool — and a bin of shredded paper
                    AboveBed(o, 0.4f, 0.8f, m => { Poster(m, 0.6f, 0.8f, new Color(0.12f, 0.2f, 0.22f), new Color(0.9f, 0.86f, 0.78f), 3, 2, pal.Trim, rnd); m.Set(S.Paper, own.Sig); m.Box(new Vector3(0, -0.28f, 0.01f), new Vector3(0.6f, 0.08f, 0.002f)); m.Set(S.Gold, pal.Trim); m.Box(new Vector3(0, 0.32f, 0.011f), new Vector3(0.44f, 0.05f, 0.002f)); });
                    OnWall(o, 0.4f, 1.6f, 0.26f, m => { for (int k = 0; k < 6; k++) { m.Push(new Vector3(-0.26f + (k % 3) * 0.26f, 0.12f - (k / 3) * 0.24f, 0), 0); Poster(m, 0.18f, 0.14f, new Color(0.5f, 0.48f, 0.45f), new Color(0.25f, 0.24f, 0.22f), 3, 2, pal.Trim, rnd); m.Pop(); } });
                    OnFloor(o, 0.22f, m => { m.Set(S.Leather, new Color(0.1f, 0.2f, 0.22f)); m.BevelBox(new Vector3(0, 0.13f, 0), new Vector3(0.5f, 0.26f, 0.26f), 0.08f); m.Set(S.Cloth, ivory); m.Rod(new Vector3(-0.15f, 0.3f, 0), new Vector3(0.15f, 0.3f, 0), 0.06f, 12); m.Set(S.Rubber, new Color(0.1f, 0.1f, 0.12f)); m.Push(new Vector3(0.1f, 0.37f, 0), 0); m.Torus(Vector3.zero, 0.045f, 0.005f, 12, 3); m.Pop(); });
                    OnWall(o, 0.2f, 1.45f, 0.16f, m => { m.Set(S.WoodDark, new Color(0.2f, 0.12f, 0.08f)); m.BevelBox(new Vector3(0, 0, 0.012f), new Vector3(0.3f, 0.3f, 0.024f), 0.006f); m.Set(S.Gold, pal.Trim); m.Box(new Vector3(0, 0.02f, 0.026f), new Vector3(0.22f, 0.16f, 0.004f)); m.Set(S.Obsidian, ink); for (int k = 0; k < 3; k++) m.Box(new Vector3(0, 0.07f - k * 0.04f, 0.029f), new Vector3(0.16f - k * 0.03f, 0.012f, 0.001f)); });
                    OnBed(o, 0f, 0.55f, m => { Garment(m, new Color(0.62f, 0.63f, 0.64f), 0.5f, 0.44f); m.Set(S.Velvet, own.Sig); m.Box(new Vector3(0, 0.036f, 0.02f), new Vector3(0.05f, 0.006f, 0.36f)); });
                    // the secret: a gilt waste basket overflowing with shredded paper strips
                    OnFloor(o, 0.18f, m => { m.Set(S.Gold, pal.Trim); m.Lathe(new[] { new Vector2(0.11f, 0), new Vector2(0.15f, 0.32f), new Vector2(0.155f, 0.33f) }, 16, true); m.Set(S.Paper, ivory); for (int k = 0; k < 40; k++) { float a = k * 2.39f, r = (float)rnd.NextDouble() * 0.13f; m.Push(Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * r, 0.33f + (float)rnd.NextDouble() * 0.06f, Mathf.Sin(a) * r), Quaternion.Euler((float)rnd.NextDouble() * 70f, (float)rnd.NextDouble() * 180f, 0), Vector3.one)); m.Box(Vector3.zero, new Vector3(0.006f, 0.001f, 0.14f)); m.Pop(); } for (int k = 0; k < 6; k++) { m.Push(Matrix4x4.TRS(new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.5f, 0.001f, ((float)rnd.NextDouble() - 0.5f) * 0.4f), Quaternion.Euler(0, (float)rnd.NextDouble() * 180f, 0), Vector3.one)); m.Box(Vector3.zero, new Vector3(0.006f, 0.001f, 0.14f)); m.Pop(); } });
                    break;

                case "P06":   // 권태겸: stock, wrapping paper, paydays — and a second ledger under the mattress
                    AboveBed(o, 0.3f, 0.62f, m => { Sheet(m, 0, 0, 0.44f, 0.62f, ivory, 0.006f); m.Set(S.Paper, new Color(0.25f, 0.35f, 0.25f)); m.Box(new Vector3(0, 0.25f, 0.009f), new Vector3(0.44f, 0.1f, 0.002f)); m.Set(S.Paper, new Color(0.3f, 0.3f, 0.3f)); for (int r = 0; r < 5; r++) for (int k = 0; k < 7; k++) m.Box(new Vector3(-0.18f + k * 0.06f, 0.14f - r * 0.07f, 0.009f), new Vector3(0.045f, 0.05f, 0.001f)); m.Set(S.GlossPaint, new Color(0.75f, 0.08f, 0.08f)); foreach (var (k, r) in new[] { (4, 1), (4, 3), (6, 4) }) { m.Push(Matrix4x4.TRS(new Vector3(-0.18f + k * 0.06f, 0.14f - r * 0.07f, 0.011f), Quaternion.Euler(90, 0, 0), Vector3.one)); m.Torus(Vector3.zero, 0.028f, 0.003f, 14, 3); m.Pop(); } m.Set(S.Brass, Color.white); m.Rod(new Vector3(0, 0.33f, 0), new Vector3(0, 0.33f, 0.02f), 0.004f, 4); });
                    OnWall(o, 0.42f, 1.4f, 0.2f, m => { m.Set(S.Iron, new Color(0.15f, 0.14f, 0.13f)); m.Rod(new Vector3(-0.4f, 0, 0.08f), new Vector3(0.4f, 0, 0.08f), 0.008f, 6); foreach (float s in new[] { -1f, 1f }) m.Box(new Vector3(s * 0.4f, 0, 0.04f), new Vector3(0.02f, 0.03f, 0.08f)); for (int k = 0; k < 6; k++) { m.Set(S.Paper, Color.HSVToRGB((0.05f + k * 0.13f) % 1f, 0.45f, 0.55f)); m.Rod(new Vector3(-0.33f + k * 0.13f, 0.1f, 0.08f), new Vector3(-0.33f + k * 0.13f, -0.35f, 0.08f), 0.035f, 10); } });
                    OnFloor(o, 0.24f, m => BoxStack(m, 3, 0.42f, 0.32f, 0.26f, new Color(0.62f, 0.48f, 0.32f), true, rnd, 3f));
                    OnFloor(o, 0.22f, m => { for (int k = 0; k < 5; k++) { m.Set(S.Paper, Color.HSVToRGB((k * 0.19f) % 1f, 0.45f, 0.5f)); var p = new Vector3(((k % 2) - 0.5f) * 0.25f, 0.1f + (k / 2) * 0.2f, 0); m.Box(p, new Vector3(0.24f, 0.2f, 0.22f)); m.Set(S.Gold, pal.Trim); m.Box(p, new Vector3(0.245f, 0.205f, 0.03f)); } });
                    OnBed(o, 0.25f, 0.6f, m => { m.Set(S.WoodLight, new Color(0.7f, 0.56f, 0.4f)); m.Box(new Vector3(0, 0.004f, 0), new Vector3(0.23f, 0.008f, 0.32f)); m.Set(S.Paper, ivory); m.Box(new Vector3(0, 0.01f, -0.01f), new Vector3(0.2f, 0.003f, 0.27f)); m.Set(S.Steel, Color.white); m.Box(new Vector3(0, 0.016f, -0.15f), new Vector3(0.08f, 0.012f, 0.03f)); });
                    // the secret: a second ledger, its green spine just showing between mattress and frame
                    if (o.Bed != null) { var f = o.Bed; float top = own.Bed == 0 ? 0.6f : FurnitureFactory.OwnerBedTop(own.Bed); mb.Push(TopFrame(f, fy, top - 0.1f)); mb.Set(S.Leather, new Color(0.12f, 0.24f, 0.16f)); mb.Push(Matrix4x4.TRS(new Vector3(f.W / 2 - 0.06f, 0.02f, -f.D / 2 + 0.3f), Quaternion.Euler(0, 8f, 0), Vector3.one)); mb.BevelBox(Vector3.zero, new Vector3(0.24f, 0.035f, 0.3f), 0.004f); mb.Set(S.Gold, pal.Trim); mb.Box(new Vector3(0.121f, 0, 0), new Vector3(0.002f, 0.02f, 0.2f)); mb.Pop(); mb.Pop(); }
                    break;

                case "P07":   // 유시온: the mic, the crowd he hosts, books he hides — and a photo laid face down
                    OnFloor(o, 0.26f, m => { m.Set(S.Chrome, Color.white); m.Rod(Vector3.zero, Vector3.up * 1.5f, 0.012f, 6, false); foreach (float a in new[] { 0f, 120f, 240f }) m.Rod(Vector3.up * 0.02f, Quaternion.Euler(0, a, 0) * new Vector3(0.24f, 0, 0), 0.01f, 4); m.Rod(Vector3.up * 1.5f, new Vector3(0, 1.52f, 0.18f), 0.01f, 5); m.Set(S.Obsidian, Color.white); m.Sphere(new Vector3(0, 1.54f, 0.22f), 0.035f, 8, 6, 1.3f); m.Set(S.Plastic, new Color(0.12f, 0.35f, 0.14f)); m.Box(new Vector3(0.28f, 0.15f, 0.05f), new Vector3(0.35f, 0.3f, 0.25f)); m.Set(S.Glass, new Color(0.3f, 0.2f, 0.08f)); for (int k = 0; k < 6; k++) m.Cyl(new Vector3(0.18f + (k % 3) * 0.1f, 0.3f, -0.02f + (k / 3) * 0.1f), 0.028f, 0.12f, 8); }, 1.55f, true);
                    AboveBed(o, 0.7f, 0.66f, m => { m.Push(new Vector3(-0.35f, 0, 0), 0); Poster(m, 0.5f, 0.66f, new Color(0.08f, 0.07f, 0.07f), new Color(0.85f, 0.66f, 0.25f), 0, 0, Color.black, rnd); m.Pop(); m.Push(new Vector3(0.3f, -0.05f, 0), Quaternion.Euler(0, 0, 3f), Vector3.one); Poster(m, 0.46f, 0.6f, new Color(0.55f, 0.18f, 0.1f), new Color(0.08f, 0.07f, 0.07f), 2, 0, Color.black, rnd); m.Pop(); });
                    OnWall(o, 0.45f, 1.6f, 0.3f, m => { Shelf(m, 0.8f, wood); BookRow(m, -0.38f, 0.38f, 0.015f, 0.11f, rnd, booksWarm, 0f); m.Set(S.Books, new Color(0.3f, 0.25f, 0.2f)); m.Push(new Vector3(0, 0.25f, 0.11f), 0); m.Box(Vector3.zero, new Vector3(0.5f, 0.05f, 0.14f)); m.Pop(); });
                    OnFloor(o, 0.22f, m => { for (int k = 0; k < 4; k++) { m.Set(S.Paper, k % 2 == 0 ? new Color(0.9f, 0.4f, 0.12f) : new Color(0.1f, 0.1f, 0.1f)); m.Push(new Vector3((k % 2) * 0.02f, k * 0.13f, 0), k * 5f); m.Box(new Vector3(0, 0.065f, 0), new Vector3(0.34f, 0.13f, 0.22f)); m.Pop(); } });
                    OnFloor(o, 0.3f, m => { for (int k = 0; k < 8; k++) { m.Set(S.Glass, k % 3 == 0 ? new Color(0.2f, 0.35f, 0.18f) : new Color(0.35f, 0.22f, 0.08f)); bool down = k % 3 == 1; float a = k * 1.9f, r = 0.08f + (k % 4) * 0.06f; m.Push(Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * r, down ? 0.03f : 0, Mathf.Sin(a) * r), down ? Quaternion.Euler(90, k * 40f, 0) : Quaternion.identity, Vector3.one)); m.Lathe(new[] { new Vector2(0.03f, 0), new Vector2(0.03f, 0.14f), new Vector2(0.011f, 0.19f), new Vector2(0.011f, 0.22f) }, 8, true, true); m.Pop(); } });
                    OnBed(o, -0.1f, 0.35f, m => { m.Set(S.Velvet, new Color(0.55f, 0.42f, 0.3f)); for (int k = 0; k < 6; k++) m.Ellipsoid(new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.5f, 0.05f, ((float)rnd.NextDouble() - 0.5f) * 0.5f), new Vector3(0.18f, 0.07f, 0.16f), 9, 5); m.Set(S.Gold, new Color(1f, 0.82f, 0.4f)); for (int k = 0; k < 14; k++) { m.Push(Matrix4x4.TRS(new Vector3(0.1f + Mathf.Cos(k * 0.45f) * 0.12f, 0.1f, 0.1f + Mathf.Sin(k * 0.45f) * 0.1f), Quaternion.Euler(k % 2 == 0 ? 0 : 90, k * 25f, 0), Vector3.one)); m.Torus(Vector3.zero, 0.011f, 0.004f, 8, 3); m.Pop(); } });
                    // the secret: a photo frame laid face down on the nightstand
                    OnTopOf(o, ns, -0.05f, 0.02f, m => { m.Set(S.WoodDark, new Color(0.2f, 0.14f, 0.1f)); m.Push(Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 12f, 0), Vector3.one)); m.BevelBox(new Vector3(0, 0.008f, 0), new Vector3(0.2f, 0.016f, 0.16f), 0.004f); m.Set(S.Paper, new Color(0.55f, 0.45f, 0.32f)); m.Box(new Vector3(0, 0.0165f, 0), new Vector3(0.17f, 0.002f, 0.13f)); m.Pop(); });
                    break;

                case "P08":   // 서라온: the bass, the amp, cables, flyers — and a cassette with a name crossed out
                    OnWall(o, 0.3f, 1.9f, 0.6f, m => BassGuitar(m, new Vector3(0, 0, 0.06f), Quaternion.identity, pal));
                    AboveBed(o, 0.6f, 0.6f, m => { m.Set(S.Felt, new Color(0.18f, 0.19f, 0.21f)); for (int r = 0; r < 3; r++) for (int k = 0; k < 4; k++) { var c = new Vector3(-0.45f + k * 0.3f, 0.2f - r * 0.2f, 0.02f); m.Box(c, new Vector3(0.28f, 0.18f, 0.04f)); for (int q = 0; q < 3; q++) m.Box(c + new Vector3((q - 1) * 0.09f, 0, 0.025f), new Vector3(0.04f, 0.16f, 0.02f)); } });
                    OnFloor(o, 0.26f, m => { m.Set(S.Leather, new Color(0.08f, 0.08f, 0.08f)); m.BevelBox(new Vector3(0, 0.26f, 0), new Vector3(0.5f, 0.52f, 0.3f), 0.02f); m.Set(S.Cloth, new Color(0.2f, 0.18f, 0.16f)); m.Box(new Vector3(0, 0.24f, 0.151f), new Vector3(0.42f, 0.38f, 0.002f)); m.Set(S.Chrome, Color.white); for (int k = 0; k < 5; k++) m.Cyl(new Vector3(-0.16f + k * 0.08f, 0.47f, 0.13f), 0.012f, 0.02f, 8); m.Set(S.Rubber, new Color(0.06f, 0.06f, 0.06f)); var cab = new List<Vector3> { new Vector3(0.2f, 0.47f, 0.15f), new Vector3(0.3f, 0.2f, 0.3f), new Vector3(0.1f, 0.01f, 0.45f), new Vector3(-0.2f, 0.01f, 0.4f) }; m.Tube(cab, 0.006f, 4); m.Set(S.PaintedMetal, own.Sig); m.Box(new Vector3(-0.28f, 0.03f, 0.42f), new Vector3(0.14f, 0.06f, 0.12f)); }, 0f, true);
                    OnWall(o, 0.4f, 1.5f, 0.3f, m => { for (int k = 0; k < 5; k++) { m.Push(new Vector3(-0.28f + (k % 3) * 0.28f + (k / 3) * 0.14f, 0.12f - (k / 3) * 0.3f, 0), Quaternion.Euler(0, 0, (k % 3 - 1) * 5f), Vector3.one); Poster(m, 0.2f, 0.28f, Color.HSVToRGB(0.55f + k * 0.03f, 0.2f, 0.55f + k * 0.06f), new Color(0.1f, 0.1f, 0.12f), k % 3, 0, Color.black, rnd); m.Pop(); } });
                    OnBed(o, 0.15f, 0.4f, m => { Garment(m, new Color(0.25f, 0.42f, 0.44f), 0.5f, 0.44f, true, rnd); m.Set(S.Cloth, new Color(0.3f, 0.35f, 0.4f)); m.Sphere(new Vector3(-0.25f, 0.05f, -0.1f), 0.08f, 10, 6, 0.7f); m.Set(S.Paper, new Color(0.95f, 0.95f, 0.95f)); for (int k = 0; k < 3; k++) m.Box(new Vector3(0.28f + k * 0.03f, 0.01f, -0.15f + k * 0.08f), new Vector3(0.08f, 0.012f, 0.11f)); });
                    // the secret: a demo cassette in its case on the nightstand, the label's first name struck through
                    OnTopOf(o, ns, 0.02f, 0.04f, m => { m.Push(Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, -14f, 0), Vector3.one)); m.Set(S.Glass, Color.white); m.Box(new Vector3(0, 0.009f, 0), new Vector3(0.11f, 0.017f, 0.07f)); m.Set(S.Plastic, new Color(0.1f, 0.1f, 0.1f)); m.Box(new Vector3(0, 0.008f, 0), new Vector3(0.1f, 0.012f, 0.062f)); m.Set(S.Paper, ivory); m.Box(new Vector3(0, 0.0152f, 0.012f), new Vector3(0.085f, 0.001f, 0.025f)); m.Set(S.Paper, ink); m.Box(new Vector3(-0.02f, 0.016f, 0.016f), new Vector3(0.03f, 0.001f, 0.004f)); m.Box(new Vector3(0.02f, 0.016f, 0.008f), new Vector3(0.03f, 0.001f, 0.004f)); m.Set(S.GlossPaint, new Color(0.7f, 0.08f, 0.08f)); m.Box(new Vector3(-0.02f, 0.0165f, 0.016f), new Vector3(0.036f, 0.001f, 0.0015f)); m.Pop(); });
                    break;

                case "P09":   // 문재하: the stage, the playbills, the Berber doll — and a table laid for someone who hasn't come
                    OnFloor(o, 0.2f, m => FurnitureFactory.Doll(m, Vector3.zero, 0.55f, new System.Random(9), pal, false), 0f, true);
                    AboveBed(o, 0.4f, 0.8f, m => { Poster(m, 0.56f, 0.8f, new Color(0.08f, 0.2f, 0.16f), new Color(0.9f, 0.82f, 0.6f), 3, 2, pal.Trim, rnd); m.Set(S.Gold, pal.Trim); m.Box(new Vector3(0, 0.3f, 0.011f), new Vector3(0.4f, 0.05f, 0.002f)); });
                    OnWall(o, 0.52f, 1.65f, 0.36f, m => Corkboard(m, new Vector3(0, 0.05f, 0), Quaternion.identity, pal, rnd, true));
                    OnFloor(o, 0.3f, m => { m.Set(S.Leather, new Color(0.35f, 0.2f, 0.12f)); m.BevelBox(new Vector3(0, 0.2f, 0), new Vector3(0.62f, 0.4f, 0.4f), 0.02f); m.Set(S.Brass, Color.white); foreach (float x in new[] { -0.25f, 0.25f }) m.Box(new Vector3(x, 0.2f, 0.2f), new Vector3(0.03f, 0.38f, 0.01f)); m.Push(Matrix4x4.TRS(new Vector3(0, 0.4f, -0.2f), Quaternion.Euler(-100f, 0, 0), Vector3.one)); m.Set(S.Leather, new Color(0.35f, 0.2f, 0.12f)); m.Box(new Vector3(0, 0.2f, 0), new Vector3(0.62f, 0.4f, 0.03f)); m.Pop(); m.Set(S.Velvet, new Color(0.45f, 0.08f, 0.12f)); m.Ellipsoid(new Vector3(0.05f, 0.42f, 0), new Vector3(0.26f, 0.06f, 0.16f), 10, 5); m.Set(S.Velvet, new Color(0.85f, 0.75f, 0.35f)); m.Ellipsoid(new Vector3(-0.12f, 0.44f, 0.05f), new Vector3(0.12f, 0.05f, 0.1f), 8, 4); }, 0f);
                    OnBed(o, 0.1f, 0.45f, m => { m.Set(S.Velvet, new Color(0.1f, 0.42f, 0.3f)); var sc = new List<Vector3>(); for (int k = 0; k <= 10; k++) sc.Add(new Vector3(-0.35f + k * 0.07f, 0.01f + Mathf.Sin(k * 0.9f) * 0.012f, Mathf.Sin(k * 0.6f) * 0.1f)); m.Tube(sc, 0.025f, 6); });
                    // the secret: a tea table laid for two — his cup used, the other untouched, a folded note under its saucer
                    {
                        var t = FindIn(rv, "SideTable", "Console", "Nightstand");
                        OnTopOf(o, t, 0f, 0.02f, m => { foreach (float s in new[] { -1f, 1f }) { m.Set(S.Porcelain, new Color(0.94f, 0.93f, 0.9f)); m.Push(new Vector3(s * 0.09f, 0, 0), 0); m.Cyl(Vector3.zero, 0.055f, 0.006f, 14); m.Lathe(new[] { new Vector2(0.025f, 0.006f), new Vector2(0.04f, 0.06f), new Vector2(0.042f, 0.065f) }, 12, true); if (s < 0) { m.Set(S.Glass, new Color(0.35f, 0.2f, 0.1f)); m.Disc(new Vector3(0, 0.045f, 0), 0.036f, 12, true); } else { m.Set(S.Paper, ivory); m.Box(new Vector3(0.03f, 0.0065f, 0.05f), new Vector3(0.06f, 0.002f, 0.04f)); } m.Pop(); } });
                    }
                    break;

                case "P10":   // 강준서: herbs, knives, recipes — and a salt jar nearly empty, the spoons set out to measure
                    OnWall(o, 0.48f, 1.5f, 0.3f, m => { m.Set(S.WoodLight, Color.white); m.Box(new Vector3(0, 0, 0.12f), new Vector3(0.9f, 0.03f, 0.24f)); for (int i = 0; i < 4; i++) { var p = new Vector3(-0.33f + i * 0.22f, 0.015f, 0.12f); m.Set(S.Clay, Color.white); m.Push(p, 0); m.Lathe(new[] { new Vector2(0.05f, 0), new Vector2(0.07f, 0.1f) }, 10); m.Pop(); FurnitureFactory.PlantShape(m, p + Vector3.up * 0.09f, 0.25f, rnd, pal, 1); } });
                    OnWall(o, 0.3f, 1.55f, 0.2f, m => { m.Set(S.WoodDark, new Color(0.25f, 0.18f, 0.12f)); m.Box(new Vector3(0, 0.1f, 0.012f), new Vector3(0.5f, 0.05f, 0.024f)); m.Set(S.Steel, new Color(0.85f, 0.86f, 0.88f)); for (int k = 0; k < 3; k++) { float x = -0.16f + k * 0.12f; m.Box(new Vector3(x, -0.02f, 0.026f), new Vector3(0.04f - k * 0.005f, 0.2f, 0.003f)); m.Set(S.WoodDark, new Color(0.2f, 0.14f, 0.1f)); m.Box(new Vector3(x, 0.14f, 0.03f), new Vector3(0.025f, 0.1f, 0.02f)); m.Set(S.Steel, new Color(0.85f, 0.86f, 0.88f)); } m.Rod(new Vector3(0.2f, 0.14f, 0.03f), new Vector3(0.2f, 0.04f, 0.03f), 0.008f, 6); for (int k = 0; k < 4; k++) { m.Push(Matrix4x4.TRS(new Vector3(0.2f, -0.03f, 0.03f), Quaternion.Euler(0, 45f * k, 0), new Vector3(0.45f, 1f, 0.45f))); m.Torus(Vector3.zero, 0.07f, 0.002f, 14, 3, 180, 360); m.Pop(); } });
                    AboveBed(o, 0.5f, 0.4f, m => { for (int k = 0; k < 4; k++) { m.Push(new Vector3(-0.36f + k * 0.24f, (k % 2) * 0.04f, 0), 0); Poster(m, 0.18f, 0.26f, ivory, new Color(0.3f, 0.25f, 0.2f), 0, 1, wood, rnd); m.Pop(); } });
                    OnFloor(o, 0.24f, m => { m.Set(S.Cloth, new Color(0.8f, 0.76f, 0.66f)); m.Ellipsoid(new Vector3(-0.08f, 0.2f, 0), new Vector3(0.18f, 0.22f, 0.14f), 12, 8); m.Set(S.Linen, new Color(0.6f, 0.5f, 0.3f)); m.Cyl(new Vector3(-0.08f, 0.38f, 0), 0.05f, 0.05f, 8); m.Set(S.WoodLight, new Color(0.6f, 0.48f, 0.32f)); m.Box(new Vector3(0.16f, 0.1f, 0.04f), new Vector3(0.28f, 0.2f, 0.2f)); m.Set(S.Leaf, new Color(0.35f, 0.5f, 0.2f)); for (int k = 0; k < 5; k++) m.Sphere(new Vector3(0.08f + (k % 3) * 0.07f, 0.22f, (k / 3) * 0.08f), 0.045f, 8, 5); }, 0f, true);
                    OnBed(o, 0f, 0.7f, m => { Garment(m, new Color(0.93f, 0.92f, 0.88f), 0.36f, 0.3f); m.Set(S.Linen, new Color(0.9f, 0.88f, 0.82f)); m.Rod(new Vector3(-0.18f, 0.03f, -0.12f), new Vector3(-0.3f, 0.02f, -0.3f), 0.006f, 4); });
                    // the secret: a big salt jar scraped nearly empty, measuring spoons laid out in a row beside it
                    {
                        var t = FindIn(rv, "TeaCart", "Console", "Nightstand");
                        OnTopOf(o, t, -0.05f, 0f, m => { m.Set(S.Glass, Color.white); m.Lathe(new[] { new Vector2(0.06f, 0), new Vector2(0.065f, 0.16f), new Vector2(0.04f, 0.18f) }, 14, true); m.Set(S.WoodLight, Color.white); m.Cyl(new Vector3(0, 0.18f, 0), 0.045f, 0.02f, 12); m.Set(S.Porcelain, new Color(0.96f, 0.96f, 0.96f)); m.Cyl(new Vector3(0, 0.003f, 0), 0.058f, 0.012f, 14); m.Set(S.Paper, ivory); m.Box(new Vector3(0, 0.09f, 0.064f), new Vector3(0.06f, 0.04f, 0.002f)); m.Set(S.Steel, Color.white); for (int k = 0; k < 4; k++) { m.Rod(new Vector3(0.1f + k * 0.03f, 0.004f, -0.05f), new Vector3(0.1f + k * 0.03f, 0.004f, 0.04f), 0.003f, 4); m.Sphere(new Vector3(0.1f + k * 0.03f, 0.008f, -0.06f), 0.009f + k * 0.002f, 8, 4, 0.5f); } });
                    }
                    break;

                case "P11":   // 윤해린: gears, blueprints, soda, tools — and one cracked part sealed in a jar
                    OnWall(o, 0.42f, 1.9f, 0.25f, m => { m.Set(S.Brass, Color.white); for (int g = 0; g < 3; g++) { var c = new Vector3((g - 1) * 0.28f, (g % 2) * 0.15f, 0.02f); float rr = 0.12f + g * 0.03f; m.Push(c, Quaternion.Euler(90, 0, 0), Vector3.one); m.Torus(Vector3.zero, rr, 0.02f, 16, 4); for (int t = 0; t < 12; t++) { float a = t / 12f * Mathf.PI * 2; m.Box(new Vector3(Mathf.Cos(a) * (rr + 0.025f), 0, Mathf.Sin(a) * (rr + 0.025f)), new Vector3(0.025f, 0.02f, 0.025f)); } m.Pop(); } });
                    AboveBed(o, 0.62f, 0.5f, m => { for (int k = 0; k < 3; k++) { m.Push(new Vector3((k - 1) * 0.42f, (k % 2) * 0.03f, 0), Quaternion.Euler(0, 0, (k - 1) * 2f), Vector3.one); Sheet(m, 0, 0, 0.38f, 0.48f, new Color(0.14f, 0.24f, 0.42f), 0.006f); m.Set(S.Paper, new Color(0.75f, 0.82f, 0.9f)); for (int q = 0; q < 5; q++) m.Box(new Vector3(0, -0.16f + q * 0.08f, 0.009f), new Vector3(0.3f - (q % 2) * 0.1f, 0.003f, 0.001f)); FaceDisc(m, new Vector3(0.06f, 0.05f, 0.009f), 0.07f); m.Set(S.Paper, new Color(0.14f, 0.24f, 0.42f)); FaceDisc(m, new Vector3(0.06f, 0.05f, 0.011f), 0.066f); m.Pop(); } });
                    OnFloor(o, 0.22f, m => { m.Set(S.Plastic, new Color(0.7f, 0.25f, 0.1f)); m.Box(new Vector3(0, 0.12f, 0), new Vector3(0.4f, 0.24f, 0.28f)); m.Set(S.Glass, new Color(0.55f, 0.75f, 0.6f)); for (int k = 0; k < 12; k++) m.Cyl(new Vector3(-0.15f + (k % 4) * 0.1f, 0.24f, -0.09f + (k / 4) * 0.09f), 0.03f, 0.12f, 8); }, 0f);
                    OnFloor(o, 0.26f, m => { m.Set(S.PaintedMetal, new Color(0.25f, 0.3f, 0.35f)); m.BevelBox(new Vector3(0, 0.14f, 0), new Vector3(0.5f, 0.28f, 0.26f), 0.01f); m.Push(Matrix4x4.TRS(new Vector3(0, 0.28f, -0.13f), Quaternion.Euler(-70f, 0, 0), Vector3.one)); m.Box(new Vector3(0, 0, 0.13f), new Vector3(0.5f, 0.02f, 0.26f)); m.Pop(); m.Set(S.Steel, Color.white); for (int k = 0; k < 6; k++) m.Rod(new Vector3(-0.18f + k * 0.07f, 0.28f, -0.05f), new Vector3(-0.16f + k * 0.07f, 0.36f, 0.02f), 0.008f, 5); m.Set(S.Brass, Color.white); for (int k = 0; k < 5; k++) { m.Push(new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.6f, 0.005f, 0.2f + (float)rnd.NextDouble() * 0.1f), 0); m.Torus(Vector3.zero, 0.02f, 0.005f, 10, 3); m.Pop(); } }, 0f);
                    OnBed(o, -0.1f, 0.4f, m => { Garment(m, new Color(0.85f, 0.42f, 0.12f), 0.52f, 0.46f, true, rnd); m.Set(S.Leather, new Color(0.25f, 0.18f, 0.1f)); m.Box(new Vector3(0.25f, 0.06f, 0.1f), new Vector3(0.16f, 0.015f, 0.05f)); m.Set(S.Glass, Color.white); foreach (float s in new[] { -1f, 1f }) m.Cyl(new Vector3(0.25f + s * 0.045f, 0.06f, 0.1f), 0.032f, 0.03f, 12); });
                    // the secret: on its own shelf, one cracked bearing sealed in a glass jar, tagged like evidence she keeps from herself
                    OnWall(o, 0.18f, 1.45f, 0.2f, m => { Shelf(m, 0.32f, wood); m.Set(S.Glass, Color.white); m.Push(new Vector3(0, 0.015f, 0.11f), 0); m.Lathe(new[] { new Vector2(0.06f, 0), new Vector2(0.065f, 0.15f), new Vector2(0.05f, 0.16f) }, 14, true); m.Pop(); m.Set(S.Iron, new Color(0.3f, 0.3f, 0.32f)); m.Cyl(new Vector3(0, 0.175f, 0.11f), 0.052f, 0.02f, 14); m.Set(S.Steel, new Color(0.55f, 0.55f, 0.58f)); m.Push(Matrix4x4.TRS(new Vector3(0, 0.07f, 0.11f), Quaternion.Euler(80, 0, 0), Vector3.one)); m.Torus(Vector3.zero, 0.035f, 0.01f, 14, 5, 0, 330); m.Pop(); m.Set(S.Paper, ivory); m.Push(Matrix4x4.TRS(new Vector3(0.05f, 0.12f, 0.17f), Quaternion.Euler(0, 0, -20f), Vector3.one)); m.Box(Vector3.zero, new Vector3(0.05f, 0.03f, 0.002f)); m.Pop(); });
                    break;

                case "P12":   // 오수아: her own poster, goods, stickers, lightsticks — and a phone with its battery pulled
                    OnFloor(o, 0.2f, m => { for (int i = 0; i < 3; i++) { m.Set(S.Plastic, new Color(0.9f, 0.88f, 0.9f)); m.Rod(new Vector3((i - 1) * 0.08f, 0.02f, 0), new Vector3((i - 1) * 0.12f, 0.28f, 0.05f), 0.014f, 6); m.Set(S.Glow, Color.Lerp(own.Sig, new Color(1f, 0.7f, 0.85f), 0.5f), MansionMats.GlowData(0.6f, 0.1f, 0, circ)); m.Sphere(new Vector3((i - 1) * 0.125f, 0.33f, 0.055f), 0.04f, 10, 6); } }, 0f, true);
                    AboveBed(o, 0.4f, 0.8f, m => { Poster(m, 0.56f, 0.8f, new Color(0.12f, 0.08f, 0.1f), new Color(0.95f, 0.72f, 0.82f), 3, 1, new Color(0.08f, 0.06f, 0.07f), rnd); m.Set(S.Gold, new Color(0.95f, 0.8f, 0.55f)); for (int k = 0; k < 5; k++) { m.Push(new Vector3(-0.22f + k * 0.11f, 0.3f + (k % 2) * 0.04f, 0.011f), 0); DoorView.Relief(m, DoorView.Star(5, 0.03f, 0.013f), 0.002f); m.Pop(); } });
                    OnWall(o, 0.45f, 1.6f, 0.26f, m => { Shelf(m, 0.8f, wood); for (int k = 0; k < 5; k++) { m.Set(S.Glass, Color.white); m.Box(new Vector3(-0.3f + k * 0.15f, 0.1f, 0.1f), new Vector3(0.09f, 0.16f, 0.006f)); m.Set(S.Paper, Color.HSVToRGB(0.9f + k * 0.02f, 0.4f, 0.9f)); m.Box(new Vector3(-0.3f + k * 0.15f, 0.1f, 0.098f), new Vector3(0.06f, 0.12f, 0.002f)); } m.Push(new Vector3(0.36f, 0.015f, 0.1f), 0); Plush(m, new Color(0.95f, 0.8f, 0.85f), 0.14f); m.Pop(); });
                    OnWall(o, 0.4f, 1.3f, 0.25f, m => { for (int i = 0; i < 16; i++) { m.Set(S.GlossPaint, Color.HSVToRGB((0.85f + (i % 5) * 0.04f) % 1f, 0.5f, 0.85f)); m.Push(Matrix4x4.TRS(new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.7f, ((float)rnd.NextDouble() - 0.5f) * 0.4f, 0.003f), Quaternion.Euler(90, 0, 0), Vector3.one)); if (i % 3 == 0) DoorView.Relief(m, DoorView.Star(5, 0.04f, 0.018f), 0.002f); else m.Cyl(Vector3.zero, 0.035f, 0.002f, 10); m.Pop(); } });
                    OnBed(o, -0.1f, -0.1f, m => { m.Push(new Vector3(-0.25f, 0, 0), 20f); Plush(m, new Color(0.95f, 0.85f, 0.88f), 0.22f); m.Pop(); m.Push(new Vector3(0.05f, 0, 0.1f), -10f); Plush(m, new Color(0.85f, 0.85f, 0.9f), 0.18f); m.Pop(); m.Set(S.Velvet, new Color(0.06f, 0.05f, 0.05f)); foreach (float s in new[] { -1f, 1f }) m.Ellipsoid(new Vector3(0.3f + s * 0.07f, 0.04f, -0.2f), new Vector3(0.07f, 0.03f, 0.05f), 8, 5); });
                    OnFloor(o, 0.18f, m => { for (int k = 0; k < 6; k++) { m.Set(S.Plastic, k % 2 == 0 ? new Color(0.8f, 0.1f, 0.08f) : new Color(0.1f, 0.08f, 0.08f)); m.Push(new Vector3(((k % 3) - 1) * 0.1f, (k / 3) * 0.05f, 0), k * 11f); m.BevelBox(new Vector3(0, 0.025f, 0), new Vector3(0.14f, 0.05f, 0.2f), 0.02f); m.Pop(); } });
                    // the secret: an old phone on the nightstand, back off, its battery lying beside it
                    OnTopOf(o, ns, -0.02f, 0.04f, m => { m.Set(S.Plastic, new Color(0.9f, 0.86f, 0.88f)); m.BevelBox(new Vector3(0, 0.005f, 0), new Vector3(0.07f, 0.01f, 0.14f), 0.006f); m.Set(S.Plastic, new Color(0.15f, 0.15f, 0.16f)); m.Box(new Vector3(0, 0.0105f, 0.01f), new Vector3(0.055f, 0.002f, 0.08f)); m.Set(S.Plastic, new Color(0.9f, 0.86f, 0.88f)); m.Push(Matrix4x4.TRS(new Vector3(0.09f, 0.003f, 0.02f), Quaternion.Euler(0, 25f, 0), Vector3.one)); m.Box(Vector3.zero, new Vector3(0.068f, 0.004f, 0.135f)); m.Pop(); m.Set(S.Plastic, new Color(0.1f, 0.12f, 0.2f)); m.Box(new Vector3(-0.08f, 0.004f, -0.02f), new Vector3(0.045f, 0.007f, 0.065f)); });
                    break;

                case "P13":   // 정세나: jersey, trophies, weights, cans (the desk carries her left-hand setup)
                    AboveBed(o, 0.36f, 0.72f, m => { Frame(m, 0.62f, 0.74f, new Color(0.08f, 0.08f, 0.08f), 0.03f, S.WoodDark); m.Set(S.Linen, new Color(0.08f, 0.08f, 0.09f)); m.Box(new Vector3(0, 0, 0.004f), new Vector3(0.58f, 0.7f, 0.003f)); m.Set(S.Cloth, Color.Lerp(own.Sig, new Color(0.15f, 0.15f, 0.2f), 0.35f)); m.Push(new Vector3(0, 0, 0.007f), 0); DoorView.Relief(m, new List<Vector2> { new Vector2(-0.14f, -0.27f), new Vector2(0.14f, -0.27f), new Vector2(0.14f, 0.12f), new Vector2(0.24f, 0.06f), new Vector2(0.27f, 0.18f), new Vector2(0.1f, 0.28f), new Vector2(-0.1f, 0.28f), new Vector2(-0.27f, 0.18f), new Vector2(-0.24f, 0.06f), new Vector2(-0.14f, 0.12f) }, 0.006f); m.Pop(); m.Set(S.Paper, new Color(0.92f, 0.92f, 0.94f)); m.Box(new Vector3(-0.035f, -0.03f, 0.015f), new Vector3(0.04f, 0.14f, 0.002f)); m.Box(new Vector3(0.035f, -0.03f, 0.015f), new Vector3(0.04f, 0.14f, 0.002f)); });
                    OnWall(o, 0.4f, 1.9f, 0.14f, m => { m.Set(S.Cloth, Color.Lerp(own.Sig, new Color(0.1f, 0.1f, 0.12f), 0.3f)); DoorView.Relief(m, new List<Vector2> { new Vector2(-0.38f, 0.12f), new Vector2(0.38f, 0.12f), new Vector2(0.38f, -0.02f), new Vector2(0f, -0.14f), new Vector2(-0.38f, -0.02f) }, 0.004f); m.Set(S.Paper, new Color(0.92f, 0.92f, 0.94f)); for (int k = 0; k < 4; k++) m.Box(new Vector3(-0.18f + k * 0.12f, 0.03f, 0.006f), new Vector3(0.08f, 0.06f, 0.001f)); });
                    OnWall(o, 0.45f, 1.5f, 0.3f, m => { Shelf(m, 0.8f, new Color(0.25f, 0.25f, 0.27f)); for (int k = 0; k < 4; k++) { m.Set(S.Gold, k % 2 == 0 ? new Color(0.85f, 0.7f, 0.4f) : new Color(0.75f, 0.76f, 0.78f)); m.Push(new Vector3(-0.3f + k * 0.2f, 0.015f, 0.11f), 0); m.Cyl(Vector3.zero, 0.04f, 0.03f, 10); m.Lathe(new[] { new Vector2(0.01f, 0.03f), new Vector2(0.01f, 0.08f), new Vector2(0.045f, 0.12f), new Vector2(0.05f, 0.18f) }, 12); m.Pop(); } });
                    OnFloor(o, 0.3f, m => { m.Set(S.Rubber, Color.Lerp(own.Sig, new Color(0.1f, 0.1f, 0.1f), 0.5f)); m.Box(new Vector3(0, 0.004f, 0), new Vector3(0.55f, 0.008f, 0.5f)); m.Set(S.Iron, new Color(0.12f, 0.12f, 0.12f)); foreach (float x in new[] { -0.12f, 0.12f }) { m.Rod(new Vector3(x, 0.05f, -0.12f), new Vector3(x, 0.05f, 0.12f), 0.015f, 6); foreach (float z in new[] { -0.1f, 0.1f }) { m.Push(Matrix4x4.TRS(new Vector3(x, 0.05f, z), Quaternion.Euler(90, 0, 0), Vector3.one)); m.Cyl(new Vector3(0, -0.02f, 0), 0.05f, 0.04f, 10); m.Pop(); } } }, 0f);
                    OnFloor(o, 0.18f, m => { m.Set(S.PaintedMetal, new Color(0.15f, 0.15f, 0.16f)); m.Lathe(new[] { new Vector2(0.12f, 0), new Vector2(0.13f, 0.3f) }, 12, true); m.Set(S.PaintedMetal, new Color(0.2f, 0.25f, 0.3f)); for (int k = 0; k < 7; k++) m.Cyl(new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.14f, 0.26f + (k % 3) * 0.03f, ((float)rnd.NextDouble() - 0.5f) * 0.14f), 0.025f, 0.1f, 8); });
                    OnBed(o, 0.1f, 0.5f, m => { Garment(m, new Color(0.1f, 0.1f, 0.11f), 0.5f, 0.44f, true, rnd); m.Set(S.Plastic, new Color(0.1f, 0.1f, 0.11f)); m.Push(Matrix4x4.TRS(new Vector3(-0.3f, 0.05f, -0.2f), Quaternion.Euler(90, 0, 0), Vector3.one)); m.Torus(Vector3.zero, 0.08f, 0.012f, 14, 5, 0, 200); m.Pop(); });
                    break;

                case "P14":   // 차은결: paper cranes, lilies, tea — and white cloths folded beside a bottle of bleach
                    for (int i = 0; i < 12; i++)
                    {
                        var R = rv.Room.Rect;
                        var p = new Vector3(R.x0 + 0.6f + (float)rnd.NextDouble() * (R.W - 1.2f), rv.CeilY - 0.5f - (float)rnd.NextDouble() * 0.7f, R.z0 + 0.6f + (float)rnd.NextDouble() * (R.D - 1.2f));
                        mb.Set(S.Linen, Color.white); mb.Rod(p, new Vector3(p.x, rv.CeilY, p.z), 0.002f, 3);
                        mb.Set(S.Paper, i % 4 == 0 ? new Color(0.08f, 0.08f, 0.08f) : new Color(0.95f, 0.94f, 0.9f));
                        mb.Push(p, (float)rnd.NextDouble() * 360f);
                        mb.Tri(new Vector3(0, 0, -0.08f), new Vector3(0, 0.04f, 0), new Vector3(0, 0, 0.08f)); mb.Tri(new Vector3(0, 0, 0.08f), new Vector3(0, 0.04f, 0), new Vector3(0, 0, -0.08f));
                        mb.Tri(new Vector3(0, 0.01f, 0), new Vector3(-0.09f, 0.03f, 0.01f), new Vector3(0, 0.015f, 0.03f)); mb.Tri(new Vector3(0, 0.015f, 0.03f), new Vector3(-0.09f, 0.03f, 0.01f), new Vector3(0, 0.01f, 0));
                        mb.Tri(new Vector3(0, 0.01f, 0), new Vector3(0.09f, 0.03f, 0.01f), new Vector3(0, 0.015f, 0.03f)); mb.Tri(new Vector3(0, 0.015f, 0.03f), new Vector3(0.09f, 0.03f, 0.01f), new Vector3(0, 0.01f, 0));
                        mb.Pop();
                    }
                    OnFloor(o, 0.2f, m => { m.Set(S.Obsidian, Color.white); m.Lathe(new[] { new Vector2(0.08f, 0), new Vector2(0.11f, 0.2f), new Vector2(0.07f, 0.55f), new Vector2(0.09f, 0.62f) }, 16, true); for (int k = 0; k < 6; k++) { float a = k * 60f * Mathf.Deg2Rad; var tip = new Vector3(Mathf.Cos(a) * 0.14f, 1.0f + (k % 2) * 0.14f, Mathf.Sin(a) * 0.14f); m.Set(S.Leaf, new Color(0.2f, 0.32f, 0.2f)); m.Rod(new Vector3(0, 0.55f, 0), tip, 0.005f, 4); m.Set(S.Porcelain, new Color(0.96f, 0.95f, 0.92f)); m.Push(tip, k * 40f); m.Lathe(new[] { new Vector2(0.005f, 0), new Vector2(0.03f, 0.05f), new Vector2(0.05f, 0.09f) }, 6); m.Pop(); } }, 1.2f, true);
                    AboveBed(o, 0.3f, 0.56f, m => { Frame(m, 0.44f, 0.56f, new Color(0.05f, 0.05f, 0.05f), 0.04f, S.WoodDark); Sheet(m, 0, 0, 0.36f, 0.48f, new Color(0.92f, 0.9f, 0.85f), 0.005f); m.Set(S.Leaf, new Color(0.25f, 0.3f, 0.22f)); m.Rod(new Vector3(0, -0.2f, 0.009f), new Vector3(0.02f, 0.12f, 0.009f), 0.004f, 4); m.Set(S.Paper, new Color(0.97f, 0.96f, 0.93f)); for (int k = 0; k < 6; k++) { m.Push(Matrix4x4.TRS(new Vector3(0.02f, 0.14f, 0.01f), Quaternion.Euler(0, 0, k * 60f), new Vector3(1, 1, 0.2f))); m.Ellipsoid(new Vector3(0, 0.04f, 0), new Vector3(0.018f, 0.045f, 0.01f), 8, 4); m.Pop(); } });
                    OnWall(o, 0.35f, 1.35f, 0.2f, m => { Shelf(m, 0.6f, new Color(0.15f, 0.13f, 0.12f)); for (int k = 0; k < 5; k++) { m.Set(S.Wax, new Color(0.95f, 0.94f, 0.9f)); float h = 0.08f + (k % 3) * 0.05f; m.Cyl(new Vector3(-0.22f + k * 0.11f, 0.015f, 0.11f), 0.02f, h, 10); o.Flames.Add(m.M.MultiplyPoint3x4(new Vector3(-0.22f + k * 0.11f, 0.015f + h + 0.005f, 0.11f))); } });
                    OnBed(o, 0.05f, 0.1f, m => { m.Set(S.Cloth, new Color(0.08f, 0.08f, 0.08f)); foreach (float s in new[] { 0f, 0.08f }) { m.Push(new Vector3(-0.1f + s, 0.002f, 0), 12f + s * 80f); m.Box(Vector3.zero, new Vector3(0.07f, 0.004f, 0.18f)); for (int k = 0; k < 4; k++) m.Box(new Vector3(-0.025f + k * 0.017f, 0, -0.12f), new Vector3(0.012f, 0.004f, 0.06f)); m.Pop(); } m.Set(S.Linen, new Color(0.06f, 0.06f, 0.06f)); m.Box(new Vector3(0.2f, 0.01f, 0.2f), new Vector3(0.3f, 0.018f, 0.24f)); });
                    // the secret: white cloths folded to a knife edge and a bleach bottle, tucked away in the corner
                    OnFloor(o, 0.18f, m => { for (int k = 0; k < 5; k++) { m.Set(S.Linen, new Color(0.96f, 0.96f, 0.95f)); m.BevelBox(new Vector3(-0.05f, 0.02f + k * 0.035f, 0), new Vector3(0.24f, 0.034f, 0.2f), 0.01f); } m.Set(S.Plastic, new Color(0.92f, 0.93f, 0.95f)); m.Push(new Vector3(0.14f, 0, 0.02f), 0); m.Lathe(new[] { new Vector2(0.05f, 0), new Vector2(0.055f, 0.2f), new Vector2(0.02f, 0.26f), new Vector2(0.02f, 0.29f) }, 12, true, true); m.Pop(); m.Set(S.Plastic, new Color(0.2f, 0.35f, 0.6f)); m.Cyl(new Vector3(0.14f, 0.29f, 0.02f), 0.024f, 0.025f, 10); m.Set(S.Paper, new Color(0.2f, 0.35f, 0.6f)); m.Box(new Vector3(0.14f, 0.12f, 0.074f), new Vector3(0.06f, 0.08f, 0.002f)); }, 0f, true);
                    break;

                case "P15":   // 남가온: the string board, maps, coffee cups — and a diary with no title
                    AboveBed(o, 0.52f, 0.7f, m => Corkboard(m, new Vector3(0, 0.05f, 0), Quaternion.identity, pal, rnd, true, true));
                    OnWall(o, 0.5f, 1.55f, 0.36f, m => { Poster(m, 0.9f, 0.62f, new Color(0.8f, 0.72f, 0.58f), new Color(0.2f, 0.15f, 0.12f), 6, 0, Color.black, rnd); m.Set(S.Paper, new Color(0.2f, 0.15f, 0.12f)); for (int k = 0; k < 5; k++) m.Box(new Vector3(-0.3f + k * 0.15f, 0.2f - (k % 2) * 0.35f, 0.01f), new Vector3(0.1f, 0.07f, 0.001f)); });
                    OnFloor(o, 0.22f, m => { for (int s = 0; s < 2; s++) for (int k = 0; k < 8; k++) { m.Set(S.Paper, Color.Lerp(new Color(0.85f, 0.83f, 0.76f), new Color(0.7f, 0.67f, 0.58f), (k % 3) / 3f)); m.Push(new Vector3(-0.1f + s * 0.22f, k * 0.022f, s * 0.04f), (k % 3 - 1) * 5f + s * 20f); m.Box(new Vector3(0, 0.011f, 0), new Vector3(0.28f, 0.02f, 0.36f)); m.Pop(); } });
                    OnFloor(o, 0.16f, m => { m.Set(S.PaintedMetal, new Color(0.18f, 0.18f, 0.18f)); m.Lathe(new[] { new Vector2(0.11f, 0), new Vector2(0.13f, 0.3f) }, 12, true); for (int k = 0; k < 9; k++) { m.Set(S.Paper, new Color(0.9f, 0.88f, 0.84f)); m.Push(Matrix4x4.TRS(new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.14f, 0.24f + (k % 3) * 0.04f, ((float)rnd.NextDouble() - 0.5f) * 0.14f), Quaternion.Euler(((float)rnd.NextDouble() - 0.5f) * 60f, 0, ((float)rnd.NextDouble() - 0.5f) * 60f), Vector3.one)); m.Lathe(new[] { new Vector2(0.03f, 0), new Vector2(0.042f, 0.12f) }, 10, true); m.Pop(); } });
                    OnBed(o, 0f, 0.3f, m => { m.Set(S.Velvet, new Color(0.08f, 0.08f, 0.08f)); m.Push(Matrix4x4.TRS(new Vector3(-0.2f, 0.02f, 0), Quaternion.identity, new Vector3(1, 0.25f, 1))); m.Sphere(Vector3.zero, 0.13f, 12, 6); m.Pop(); m.Set(S.Leather, new Color(0.55f, 0.1f, 0.1f)); m.Box(new Vector3(0.15f, 0.01f, 0.05f), new Vector3(0.1f, 0.018f, 0.15f)); m.Set(S.Cloth, own.Sig); var ly = new List<Vector3>(); for (int k = 0; k <= 8; k++) ly.Add(new Vector3(0.05f + Mathf.Sin(k * 0.8f) * 0.1f, 0.004f, -0.1f + k * 0.04f)); m.Tube(ly, 0.006f, 4); });
                    // the secret: a diary with no title, its strap buckled, on the nightstand
                    OnTopOf(o, ns, 0f, 0.03f, m => { m.Push(Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 8f, 0), Vector3.one)); m.Set(S.Leather, new Color(0.28f, 0.1f, 0.08f)); m.BevelBox(new Vector3(0, 0.017f, 0), new Vector3(0.15f, 0.034f, 0.21f), 0.006f); m.Set(S.Paper, ivory); m.Box(new Vector3(0.004f, 0.017f, 0), new Vector3(0.14f, 0.026f, 0.2f), MeshBuilder.Faces.PZ | MeshBuilder.Faces.NZ | MeshBuilder.Faces.PX); m.Set(S.Leather, new Color(0.15f, 0.08f, 0.06f)); m.Box(new Vector3(0.02f, 0.018f, 0), new Vector3(0.02f, 0.037f, 0.215f)); m.Set(S.Brass, Color.white); m.Box(new Vector3(0.02f, 0.036f, 0.06f), new Vector3(0.024f, 0.004f, 0.02f)); m.Pop(); });
                    break;

                case "P16":   // 신채령: the duck mascot, sketches, buttons, the shop window — and bolts of fabric never unwrapped
                    OnFloor(o, 0.24f, m => { m.Set(S.Velvet, new Color(0.95f, 0.8f, 0.3f)); m.Sphere(new Vector3(0, 0.22f, 0), 0.22f, 12, 8); m.Sphere(new Vector3(0, 0.52f, 0.02f), 0.14f, 10, 7); m.Set(S.GlossPaint, new Color(0.95f, 0.5f, 0.15f)); m.Push(Matrix4x4.TRS(new Vector3(0, 0.5f, 0.16f), Quaternion.identity, new Vector3(1, 0.4f, 1))); m.Sphere(Vector3.zero, 0.06f, 8, 5); m.Pop(); m.Set(S.Obsidian, Color.white); m.Sphere(new Vector3(-0.05f, 0.56f, 0.13f), 0.02f, 6, 4); m.Sphere(new Vector3(0.05f, 0.56f, 0.13f), 0.02f, 6, 4); m.Set(S.Velvet, own.Sig); m.Torus(new Vector3(0, 0.4f, 0), 0.1f, 0.02f, 12, 4); }, 0f, true);
                    OnWall(o, 0.48f, 1.6f, 0.3f, m => { for (int k = 0; k < 6; k++) { float x = -0.3f + (k % 3) * 0.3f, y = 0.14f - (k / 3) * 0.3f; Sheet(m, x, y, 0.22f, 0.28f, ivory, 0.004f); m.Set(S.Paper, new Color(0.12f, 0.1f, 0.1f)); m.Push(new Vector3(x, y, 0.007f), 0); DoorView.Relief(m, new List<Vector2> { new Vector2(-0.02f, 0.1f), new Vector2(0.02f, 0.1f), new Vector2(0.03f, 0.02f), new Vector2(0.07f, -0.11f), new Vector2(-0.07f, -0.11f), new Vector2(-0.03f, 0.02f) }, 0.001f); m.Pop(); m.Set(S.Paper, Color.HSVToRGB((0.82f + k * 0.03f) % 1f, 0.5f, 0.6f)); m.Box(new Vector3(x + 0.07f, y - 0.1f, 0.008f), new Vector3(0.04f, 0.04f, 0.001f)); } });
                    AboveBed(o, 0.4f, 0.54f, m => { Frame(m, 0.72f, 0.54f, new Color(0.78f, 0.78f, 0.82f), 0.035f, S.Gold); Sheet(m, 0, 0, 0.66f, 0.48f, new Color(0.18f, 0.14f, 0.16f), 0.005f); m.Set(S.Glow, new Color(1f, 0.85f, 0.7f), MansionMats.GlowData(0.35f, 0, 0, circ)); m.Box(new Vector3(0, -0.02f, 0.008f), new Vector3(0.42f, 0.28f, 0.002f)); m.Set(S.Paper, own.Sig); m.Box(new Vector3(0, 0.16f, 0.009f), new Vector3(0.5f, 0.07f, 0.002f)); m.Set(S.Paper, new Color(0.1f, 0.08f, 0.08f)); foreach (float x in new[] { -0.1f, 0.1f }) m.Box(new Vector3(x, -0.06f, 0.0095f), new Vector3(0.04f, 0.2f, 0.001f)); });
                    OnWall(o, 0.3f, 1.4f, 0.2f, m => { Shelf(m, 0.52f, wood); for (int k = 0; k < 3; k++) { m.Set(S.Glass, Color.white); m.Push(new Vector3(-0.16f + k * 0.16f, 0.015f, 0.11f), 0); m.Lathe(new[] { new Vector2(0.05f, 0), new Vector2(0.055f, 0.13f), new Vector2(0.04f, 0.15f) }, 12, true); m.Pop(); for (int b = 0; b < 8; b++) { m.Set(S.Porcelain, Color.HSVToRGB((0.8f + k * 0.08f + b * 0.02f) % 1f, 0.4f, 0.75f)); m.Push(Matrix4x4.TRS(new Vector3(-0.16f + k * 0.16f + ((b % 3) - 1) * 0.025f, 0.03f + (b / 3) * 0.02f, 0.11f + ((b % 2) - 0.5f) * 0.03f), Quaternion.Euler(b * 37f, 0, b * 21f), Vector3.one)); m.Cyl(Vector3.zero, 0.013f, 0.005f, 8); m.Pop(); } m.Set(S.Steel, Color.white); m.Cyl(new Vector3(-0.16f + k * 0.16f, 0.165f, 0.11f), 0.042f, 0.015f, 12); } });
                    OnBed(o, 0f, 0.5f, m => { Garment(m, own.Sig * 0.8f, 0.56f, 0.46f); for (int k = 0; k < 5; k++) { m.Set(S.Velvet, Color.HSVToRGB((0.8f + k * 0.04f) % 1f, 0.5f, 0.6f)); m.Push(new Vector3(0.3f, 0.034f + k * 0.004f, -0.25f + k * 0.02f), k * 12f); m.Box(Vector3.zero, new Vector3(0.1f, 0.004f, 0.12f)); m.Pop(); } });
                    // the secret: bolts of fabric stacked up in a corner, still wrapped in the supplier's plastic (three times too many)
                    OnFloor(o, 0.3f, m => { for (int k = 0; k < 7; k++) { bool up = k >= 4; var c = Color.HSVToRGB((0.78f + k * 0.05f) % 1f, 0.45f, 0.5f); m.Set(S.Cloth, c); if (up) { m.Push(Matrix4x4.TRS(new Vector3(-0.15f + (k - 4) * 0.15f, 0.55f, -0.12f), Quaternion.Euler(-8f, 0, 0), Vector3.one)); m.Box(Vector3.zero, new Vector3(0.12f, 1.1f, 0.12f)); m.Set(S.Glass, Color.white); m.Box(Vector3.zero, new Vector3(0.126f, 1.08f, 0.126f)); m.Pop(); } else { m.Push(new Vector3(0, 0.07f + (k % 2) * 0.13f, (k / 2) * 0.14f - 0.05f), 0); m.Rod(new Vector3(-0.28f, 0, 0), new Vector3(0.28f, 0, 0), 0.065f, 10); m.Set(S.Glass, Color.white); m.Rod(new Vector3(-0.285f, 0, 0), new Vector3(0.285f, 0, 0), 0.068f, 10); m.Pop(); } } }, 1.15f, true);
                    break;

                case "P17":   // 송예담: the ring light, the paper mansion, polaroids, bunting — and an envelope with its address scratched out
                    { Vector3? ringAt = null; OnFloor(o, 0.3f, m => { m.Set(S.Obsidian, Color.white); for (int k = 0; k < 3; k++) { float a = k / 3f * Mathf.PI * 2; m.Rod(Vector3.up * 0.9f, new Vector3(Mathf.Cos(a) * 0.3f, 0, Mathf.Sin(a) * 0.3f), 0.01f, 4); } m.Rod(Vector3.up * 0.9f, Vector3.up * 1.5f, 0.012f, 5); m.Set(S.Glow, new Color(1f, 0.95f, 0.9f), MansionMats.GlowData(1.4f, 0, 0, circ)); m.Push(Vector3.up * 1.65f + Vector3.forward * 0.02f, Quaternion.Euler(90, 0, 0), Vector3.one); m.Torus(Vector3.zero, 0.2f, 0.02f, 24, 5); m.Pop(); ringAt = m.M.MultiplyPoint3x4(Vector3.up * 1.65f + Vector3.forward * 0.35f); }, 1.85f, true); if (ringAt.HasValue) AddLight(rv, ringAt.Value, new Color(1f, 0.95f, 0.9f), 1.6f, 3.2f); }
                    {
                        // the paper mansion model on her console: rooms added day by day, some lit from inside
                        var t = FindIn(rv, "Console", "SideTable", "Chest");
                        OnTopOf(o, t, 0f, -0.02f, m => { var pr = new System.Random(1717); for (int k = 0; k < 9; k++) { float w = 0.12f + (float)pr.NextDouble() * 0.08f, d = 0.1f + (float)pr.NextDouble() * 0.06f, h = 0.08f + (k % 3) * 0.05f; var c = new Vector3(-0.3f + (k % 4) * 0.17f, (k / 4) * 0.0f, -0.08f + (k / 4) * 0.12f); m.Set(S.Paper, Color.Lerp(new Color(0.96f, 0.95f, 0.92f), own.Sig, (k % 3) * 0.12f)); m.Box(c + new Vector3(0, h / 2, 0), new Vector3(w, h, d)); m.Set(S.Paper, new Color(0.85f, 0.83f, 0.8f)); m.Tri(c + new Vector3(-w / 2, h, d / 2 + 0.001f), c + new Vector3(0, h + 0.05f, d / 2 + 0.001f), c + new Vector3(w / 2, h, d / 2 + 0.001f)); m.Tri(c + new Vector3(w / 2, h, -d / 2 - 0.001f), c + new Vector3(0, h + 0.05f, -d / 2 - 0.001f), c + new Vector3(-w / 2, h, -d / 2 - 0.001f)); m.Quad(c + new Vector3(-w / 2, h, -d / 2), c + new Vector3(0, h + 0.05f, -d / 2), c + new Vector3(0, h + 0.05f, d / 2), c + new Vector3(-w / 2, h, d / 2)); m.Quad(c + new Vector3(0, h + 0.05f, -d / 2), c + new Vector3(w / 2, h, -d / 2), c + new Vector3(w / 2, h, d / 2), c + new Vector3(0, h + 0.05f, d / 2)); if (k % 2 == 0) { m.Set(S.Glow, new Color(1f, 0.82f, 0.5f), MansionMats.GlowData(0.6f, 0.05f, 0, circ)); m.Box(c + new Vector3(0, h * 0.5f, d / 2 + 0.002f), new Vector3(0.025f, 0.03f, 0.002f)); } } });
                    }
                    for (int k = 0; k < 2; k++)
                    {
                        var R = rv.Room.Rect; float y = rv.CeilY - 0.35f - k * 0.1f;
                        var a = k == 0 ? new Vector3(R.x0 + 0.3f, y, R.z0 + 0.3f) : new Vector3(R.x1 - 0.3f, y, R.z0 + 0.3f);
                        var b = k == 0 ? new Vector3(R.x1 - 0.3f, y, R.z1 - 0.3f) : new Vector3(R.x0 + 0.3f, y, R.z1 - 0.3f);
                        var str = new List<Vector3>(); for (int s = 0; s <= 16; s++) { float t = s / 16f; str.Add(Vector3.Lerp(a, b, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.35f); }
                        mb.Set(S.Linen, new Color(0.85f, 0.82f, 0.74f)); mb.Tube(str, 0.003f, 4);
                        var dir = (b - a).normalized;
                        for (int s = 1; s < 16; s++) { var p = str[s]; mb.Set(S.Paper, s % 3 == 0 ? own.Sig : s % 3 == 1 ? new Color(0.93f, 0.9f, 0.86f) : new Color(0.8f, 0.7f, 0.75f)); mb.Tri(p - dir * 0.05f, p + dir * 0.05f, p + Vector3.down * 0.12f); mb.Tri(p + dir * 0.05f, p - dir * 0.05f, p + Vector3.down * 0.12f); }
                    }
                    AboveBed(o, 0.5f, 0.52f, m => Poster(m, 0.9f, 0.56f, new Color(0.28f, 0.34f, 0.32f), new Color(0.25f, 0.25f, 0.28f), 5, 0, Color.black, rnd));
                    OnBed(o, -0.2f, 0f, m => { m.Push(new Vector3(0, 0, 0), 15f); Plush(m, new Color(0.75f, 0.9f, 0.85f), 0.2f); m.Pop(); m.Set(S.Velvet, Color.Lerp(own.Sig, Color.white, 0.4f)); m.BevelBox(new Vector3(0.25f, 0.05f, -0.1f), new Vector3(0.3f, 0.1f, 0.22f), 0.04f); });
                    OnFloor(o, 0.22f, m => { m.Set(S.Paper, new Color(0.62f, 0.48f, 0.32f)); m.Box(new Vector3(0, 0.12f, 0), new Vector3(0.4f, 0.24f, 0.3f)); for (int k = 0; k < 5; k++) { m.Set(S.Paper, Color.HSVToRGB((0.4f + k * 0.12f) % 1f, 0.3f, 0.9f)); m.Rod(new Vector3(-0.14f + k * 0.07f, 0.2f, -0.05f), new Vector3(-0.12f + k * 0.07f, 0.38f, 0.02f), 0.02f, 8); } });
                    // the secret: over the desk, one envelope pinned up on its own, the address scratched out hard
                    OnWall(o, 0.14f, 1.35f, 0.1f, m => { m.Set(S.Paper, new Color(0.95f, 0.93f, 0.88f)); m.Box(new Vector3(0, 0, 0.004f), new Vector3(0.22f, 0.13f, 0.003f)); m.Set(S.Paper, new Color(0.85f, 0.82f, 0.76f)); m.Tri(new Vector3(-0.11f, 0.065f, 0.0065f), new Vector3(0, 0.0f, 0.0065f), new Vector3(0.11f, 0.065f, 0.0065f)); m.Set(S.Paper, new Color(0.1f, 0.1f, 0.12f)); for (int k = 0; k < 9; k++) { m.Push(Matrix4x4.TRS(new Vector3(0.01f + ((float)rnd.NextDouble() - 0.5f) * 0.02f, -0.03f + ((float)rnd.NextDouble() - 0.5f) * 0.02f, 0.007f), Quaternion.Euler(0, 0, ((float)rnd.NextDouble() - 0.5f) * 40f), Vector3.one)); m.Box(Vector3.zero, new Vector3(0.12f, 0.004f, 0.001f)); m.Pop(); } m.Set(S.GlossPaint, own.Sig); FaceDisc(m, new Vector3(0, 0.055f, 0.008f), 0.008f, 8); });
                    break;

                case "P18":   // 임민서: the hand truck, shifts, his siblings — and a school textbook under the pillow
                    OnFloor(o, 0.26f, m => { m.Set(S.Iron, new Color(0.3f, 0.2f, 0.15f)); m.Rod(new Vector3(-0.2f, 0.1f, 0), new Vector3(-0.2f, 1.2f, 0.05f), 0.015f, 5, false); m.Rod(new Vector3(0.2f, 0.1f, 0), new Vector3(0.2f, 1.2f, 0.05f), 0.015f, 5, false); m.Rod(new Vector3(-0.2f, 1.1f, 0.045f), new Vector3(0.2f, 1.1f, 0.045f), 0.012f, 5); m.Box(new Vector3(0, 0.02f, 0.12f), new Vector3(0.42f, 0.02f, 0.2f)); m.Set(S.Rubber, new Color(0.08f, 0.08f, 0.08f)); foreach (float s in new[] { -1f, 1f }) { m.Push(Matrix4x4.TRS(new Vector3(s * 0.24f, 0.1f, -0.02f), Quaternion.Euler(0, 0, 90), Vector3.one)); m.Cyl(new Vector3(0, -0.03f, 0), 0.1f, 0.06f, 12); m.Pop(); } m.Set(S.Paper, new Color(0.7f, 0.55f, 0.35f)); m.Box(new Vector3(0, 0.23f, 0.1f), new Vector3(0.36f, 0.4f, 0.3f)); m.Box(new Vector3(0, 0.6f, 0.1f), new Vector3(0.3f, 0.34f, 0.28f)); }, 1.25f, true);
                    OnWall(o, 0.24f, 1.6f, 0.3f, m => { Sheet(m, 0, 0, 0.36f, 0.5f, ivory, 0.005f); m.Set(S.Paper, new Color(0.35f, 0.4f, 0.3f)); m.Box(new Vector3(0, 0.2f, 0.008f), new Vector3(0.36f, 0.08f, 0.002f)); m.Set(S.Paper, new Color(0.25f, 0.25f, 0.25f)); for (int r = 0; r < 5; r++) for (int k = 0; k < 7; k++) { m.Box(new Vector3(-0.15f + k * 0.05f, 0.1f - r * 0.07f, 0.008f), new Vector3(0.04f, 0.05f, 0.001f)); } m.Set(S.GlossPaint, new Color(0.2f, 0.25f, 0.45f)); for (int k = 0; k < 18; k++) { int r = k / 5, c = (k * 3) % 7; m.Box(new Vector3(-0.15f + c * 0.05f, 0.1f - r * 0.07f, 0.0095f), new Vector3(0.03f, 0.006f, 0.001f)); } m.Set(S.Brass, Color.white); m.Rod(new Vector3(0, 0.26f, 0), new Vector3(0, 0.26f, 0.02f), 0.004f, 4); });
                    OnWall(o, 0.14f, 1.45f, 0.12f, m => { Frame(m, 0.24f, 0.18f, new Color(0.5f, 0.44f, 0.34f), 0.02f, S.WoodLight); Sheet(m, 0, 0, 0.2f, 0.14f, new Color(0.62f, 0.6f, 0.55f), 0.005f); m.Set(S.Paper, new Color(0.25f, 0.22f, 0.2f)); for (int k = 0; k < 4; k++) { float h = 0.05f + (3 - k) * 0.012f; m.Box(new Vector3(-0.066f + k * 0.044f, -0.065f + h / 2, 0.008f), new Vector3(0.03f, h, 0.002f)); m.Push(Matrix4x4.TRS(new Vector3(-0.066f + k * 0.044f, -0.065f + h + 0.012f, 0.008f), Quaternion.identity, new Vector3(1, 1, 0.1f))); m.Sphere(Vector3.zero, 0.012f, 8, 5); m.Pop(); } });
                    // the secret: a worn school textbook pushed under the pillow, one corner showing
                    if (o.Bed != null)
                    {
                        var f = o.Bed; float top = own.Bed == 0 ? 0.6f : FurnitureFactory.OwnerBedTop(own.Bed);
                        mb.Push(TopFrame(f, fy, top)); mb.Push(Matrix4x4.TRS(new Vector3(f.W * 0.28f, 0.016f, -f.D / 2 + 0.5f), Quaternion.Euler(0, 24f, 0), Vector3.one));
                        mb.Set(S.Books, new Color(0.3f, 0.42f, 0.55f)); mb.BevelBox(Vector3.zero, new Vector3(0.19f, 0.022f, 0.26f), 0.004f);
                        mb.Set(S.Paper, ivory); mb.Box(new Vector3(0.004f, 0, 0), new Vector3(0.18f, 0.016f, 0.255f), MeshBuilder.Faces.PZ | MeshBuilder.Faces.NZ | MeshBuilder.Faces.PX);
                        mb.Set(S.Paper, new Color(0.9f, 0.88f, 0.8f)); mb.Box(new Vector3(0, 0.0115f, 0.05f), new Vector3(0.1f, 0.001f, 0.05f));
                        mb.Pop(); mb.Pop();
                    }
                    break;
            }
        }
    }
}
