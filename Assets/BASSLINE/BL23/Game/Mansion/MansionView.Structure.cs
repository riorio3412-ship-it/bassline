using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    public sealed partial class MansionView
    {
        // ------------------------------------------------------------------ doors
        void BuildDoors()
        {
            foreach (var d in Layout.Doors)
            {
                var g = _grids[d.Pos.f];
                Vector3 n = d.AlongX ? Vector3.forward : Vector3.right;
                int plus = d.AlongX ? g.At(d.Pos.x, d.Pos.z + 0.25f) : g.At(d.Pos.x + 0.25f, d.Pos.z);
                Vector3 intoA = plus == d.RoomA ? n : -n;
                var ra = Layout.Room(d.RoomA); var rb = Layout.Room(d.RoomB);
                var rv = Rooms[d.RoomA];
                var dv = DoorView.Build(this, d, _doorH[d.Id], rv.Root, rv.Pal, ra, rb, intoA);
                if (dv != null && d.Sealed) dv.SetSealed(true);
                Doors[d.Id] = dv;
                foreach (var r in dv.GetComponentsInChildren<Renderer>(true)) rv.Renderers.Add(r);
                // keep decor off door approaches
                float hw = d.Width / 2f + 0.4f;
                var keep = d.AlongX ? new RectF(d.Pos.x - hw, d.Pos.z - 1.3f, d.Pos.x + hw, d.Pos.z + 1.3f) : new RectF(d.Pos.x - 1.3f, d.Pos.z - hw, d.Pos.x + 1.3f, d.Pos.z + hw);
                rv.Blocked.Add(keep);
                if (rb != null) Rooms[rb.Id].Blocked.Add(keep);
            }
        }

        // ------------------------------------------------------------------ grand hall: void edges, fascia, railings, flesh columns
        void BuildGrandHall()
        {
            if (HallRoom == null || VoidRoom == null) return;
            var hall = Rooms[HallRoom.Id]; var land = LandingRoom != null ? Rooms[LandingRoom.Id] : hall;
            var pal = hall.Pal;
            var grand = Layout.Stairs.FirstOrDefault(s => s.Grand);
            var gf = Layout.Furniture.FirstOrDefault(f => f.Type == "GrandStair");
            float stairX0 = gf != null ? gf.Pos.x - 2.5f : 0, stairX1 = gf != null ? gf.Pos.x + 2.5f : 0;
            bool stairNorth = gf != null && Math.Abs(gf.Yaw) < 1f;   // yaw 0 -> rises toward +z (top at void z1)
            float stairEdgeZ = stairNorth ? VoidRect.z1 : VoidRect.z0;
            var vr = VoidRect;
            var mbH = new MeshBuilder();   // hall (1F) side
            var mbL = new MeshBuilder();   // landing (2F) side
            var mbHd = new MeshBuilder();  // hall details without shadow casting (teeth, banners)
            // edges of the void rectangle, each with outward normal (toward the ring)
            var edges = new List<(Vector3 a, Vector3 b, Vector3 outN, bool alongX, float line)>
            {
                (new Vector3(vr.x0, 0, vr.z0), new Vector3(vr.x1, 0, vr.z0), Vector3.back, true, vr.z0),
                (new Vector3(vr.x0, 0, vr.z1), new Vector3(vr.x1, 0, vr.z1), Vector3.forward, true, vr.z1),
                (new Vector3(vr.x0, 0, vr.z0), new Vector3(vr.x0, 0, vr.z1), Vector3.left, false, vr.x0),
                (new Vector3(vr.x1, 0, vr.z0), new Vector3(vr.x1, 0, vr.z1), Vector3.right, false, vr.x1),
            };
            foreach (var e in edges)
            {
                Vector3 inN = -e.outN;   // toward the void centre
                // --- 1F slab edge fascia (4.35..4.85) with a tooth molding hanging into the void
                var segs = new List<(float a, float b)>();
                float ea = e.alongX ? e.a.x : e.a.z, eb = e.alongX ? e.b.x : e.b.z;
                bool stairEdge = gf != null && e.alongX && Math.Abs(e.line - stairEdgeZ) < 0.05f;
                if (stairEdge) { segs.Add((ea, stairX0 - 0.05f)); segs.Add((stairX1 + 0.05f, eb)); }
                else segs.Add((ea, eb));
                foreach (var (sa, sb) in segs)
                {
                    if (sb - sa < 0.05f) continue;
                    Vector3 A = e.alongX ? new Vector3(sa, 0, e.line) : new Vector3(e.line, 0, sa);
                    Vector3 B = e.alongX ? new Vector3(sb, 0, e.line) : new Vector3(e.line, 0, sb);
                    // fascia box
                    mbH.Set(S.WoodDark, Color.Lerp(Color.white, pal.Wood * 2f, 0.3f));
                    Vector3 mn = Vector3.Min(A, B) + new Vector3(0, 4.3f, 0) + Vector3.Min(Vector3.zero, inN * 0.12f) + Vector3.Min(Vector3.zero, e.outN * 0.05f);
                    Vector3 mx = Vector3.Max(A, B) + new Vector3(0, 4.82f, 0) + Vector3.Max(Vector3.zero, inN * 0.12f) + Vector3.Max(Vector3.zero, e.outN * 0.05f);
                    mbH.BoxMM(mn, mx);
                    mbH.Set(S.Gold, pal.Trim);
                    mbH.BoxMM(mn + new Vector3(0, 0.38f, 0) + Vector3.Min(Vector3.zero, inN * 0.03f), new Vector3(mx.x, mn.y + 0.44f, mx.z) + Vector3.Max(Vector3.zero, inN * 0.03f));
                    // teeth (no shadows: thin details under the chandelier made rows of blocky shadow on the floor)
                    mbHd.Set(S.Bone, Color.white);
                    float L = sb - sa;
                    var rnd = hall.Rng;
                    for (float t = 0.08f; t < L - 0.05f; t += 0.13f)
                    {
                        float len = 0.1f + (float)rnd.NextDouble() * 0.04f + (rnd.NextDouble() < 0.05 ? 0.22f : 0f);
                        Vector3 p = Vector3.Lerp(A, B, t / L) + inN * 0.1f; p.y = 4.3f - len * 0.5f;
                        mbHd.Box(p, new Vector3(0.06f, len, 0.06f));
                    }
                    // hanging velvet banners every ~3 m
                    int nb = Math.Max(1, (int)(L / 4.6f));
                    for (int i = 0; i < nb; i++)
                    {
                        float t = (i + 0.5f) / nb;
                        Vector3 p = Vector3.Lerp(A, B, t) + inN * 0.16f;
                        Banner(mbHd, p + Vector3.up * 4.25f, e.alongX ? Vector3.right : Vector3.forward, inN, 0.62f, 1.3f, i % 2 == 0 ? pal.Fabric : pal.Carpet, pal);
                    }
                    // --- 2F: balustrade on the landing side
                    Balustrade(mbL, A + Vector3.up * 4.8f + e.outN * 0.12f, B + Vector3.up * 4.8f + e.outN * 0.12f, pal, true);
                    // --- 2F: upper fascia between landing ceiling (8.6) and void ceiling (9.0)
                    mbL.Set(S.Ceiling, Color.Lerp(pal.Wall, Color.white, 0.2f));
                    mbL.QuadAuto(A + Vector3.up * 8.55f, A + Vector3.up * 9.02f, B + Vector3.up * 9.02f, B + Vector3.up * 8.55f, inN);
                    mbL.Set(S.Gold, pal.Trim);
                    mbL.BoxMM(Vector3.Min(A, B) + Vector3.up * 8.5f + Vector3.Min(Vector3.zero, inN * 0.08f), Vector3.Max(A, B) + Vector3.up * 8.62f + Vector3.Max(Vector3.zero, inN * 0.08f));
                }
            }
            // stone piers at the void corners (1F floor to the slab). Where the kernel
            // lets NPCs walk, an iron candle corona hangs from the slab instead (lowest point 2.45 m): nothing solid in a walkable cell.
            foreach (var c in new[] { new Vector3(vr.x0, 0, vr.z0), new Vector3(vr.x1, 0, vr.z0), new Vector3(vr.x0, 0, vr.z1), new Vector3(vr.x1, 0, vr.z1) })
            {
                if (DecorFootprintFree(hall, c, 0.42f))
                {
                    FleshColumn(mbH, c, 0.3f, 4.35f, hall.Rng, pal);
                    AddDecorCollider(hall, c + Vector3.up * 2.175f, new Vector3(0.84f, 4.35f, 0.84f), "HallColumn");
                }
                else HangingFlesh(mbH, c, 0.3f, 4.35f, 2.45f, hall.Rng, pal);
                // marble cap at 2F: newel post with a moon finial
                mbL.Set(S.Marble, Color.Lerp(Color.white, pal.FloorA, 0.3f));
                mbL.Box(c + new Vector3(0, 5.35f, 0), new Vector3(0.32f, 1.1f, 0.32f));
                mbL.Set(S.Gold, pal.Trim);
                mbL.Sphere(c + new Vector3(0, 6.02f, 0), 0.13f, 12, 8);
            }
            Emit(hall, mbH, "HallVoidEdge");
            Emit(hall, mbHd, "HallVoidDetail", null, ShadowCastingMode.Off);
            Emit(land, mbL, "LandingBalustrade");
            // railing colliders (2F)
            var col = new GameObject("VoidRailColliders"); col.transform.SetParent(land.Root, false);
            foreach (var e in edges)
            {
                bool stairEdge = gf != null && e.alongX && Math.Abs(e.line - stairEdgeZ) < 0.05f;
                float ea = e.alongX ? e.a.x : e.a.z, eb = e.alongX ? e.b.x : e.b.z;
                var parts = stairEdge ? new[] { (ea, stairX0), (stairX1, eb) } : new[] { (ea, eb) };
                foreach (var (sa, sb) in parts)
                {
                    if (sb - sa < 0.05f) continue;
                    var bc = col.AddComponent<BoxCollider>();
                    float mid = (sa + sb) * 0.5f;
                    bc.center = e.alongX ? new Vector3(mid, 5.35f, e.line + e.outN.z * 0.12f) : new Vector3(e.line + e.outN.x * 0.12f, 5.35f, mid);
                    bc.size = e.alongX ? new Vector3(sb - sa, 1.1f, 0.14f) : new Vector3(0.14f, 1.1f, sb - sa);
                }
            }
        }

        internal void Banner(MeshBuilder mb, Vector3 top, Vector3 along, Vector3 facing, float w, float h, Color c, MansionPalette pal)
        {
            // velvet banner with a pointed (swallowtail) bottom and an embroidered eye
            mb.Set(S.Velvet, c);
            Vector3 r = along * w * 0.5f; Vector3 d = Vector3.down * h;
            Vector3 f = facing * 0.004f;
            Vector3 t0 = top - r, t1 = top + r, b0 = top - r + d, b1 = top + r + d, tip = top + d * 1.18f;
            mb.QuadAuto(t0 + f, b0 + f, b1 + f, t1 + f, facing);
            mb.QuadAuto(t0 - f, b0 - f, b1 - f, t1 - f, -facing);
            mb.TriAuto(b0 + f, tip + f, b1 + f, facing); mb.TriAuto(b0 - f, tip - f, b1 - f, -facing);
            mb.Set(S.Gold, pal.Trim);
            mb.Rod(top - r * 1.15f, top + r * 1.15f, 0.018f, 6, true);
            mb.Push(Matrix4x4.TRS(top + d * 0.45f + facing * 0.012f, Quaternion.LookRotation(facing), Vector3.one));
            // eye embroidery (gold almond + dark iris)
            var pts = new List<Vector3>();
            for (int i = 0; i <= 16; i++) { float t = i / 16f * Mathf.PI * 2; pts.Add(new Vector3(Mathf.Cos(t) * w * 0.3f, Mathf.Sin(t) * w * 0.13f * Mathf.Abs(Mathf.Cos(t) * 0.3f + 0.7f), 0)); }
            mb.Tube(pts, 0.008f, 4);
            mb.Set(S.Velvet, pal.Neon);
            mb.Disc(Vector3.zero + new Vector3(0, 0, 0.002f), w * 0.07f, 12, false);
            mb.Pop();
        }

        internal void Balustrade(MeshBuilder mb, Vector3 a, Vector3 b, MansionPalette pal, bool marble, float height = 1.05f, float spacing = 0.19f)
        {
            float L = Vector3.Distance(a, b); if (L < 0.05f) return;
            Vector3 dir = (b - a) / L;
            int slotPost = marble ? S.Marble : S.WoodDark;
            Color tint = marble ? Color.Lerp(Color.white, pal.FloorA, 0.3f) : Color.Lerp(Color.white, pal.Wood * 2f, 0.4f);
            mb.Set(slotPost, tint);
            // bottom rail & handrail
            Vector3 side = Vector3.Cross(Vector3.up, dir) * 0.07f;
            mb.BoxMM(Vector3.Min(a, b) - new Vector3(Math.Abs(side.x), 0, Math.Abs(side.z)) + Vector3.up * 0.0f, Vector3.Max(a, b) + new Vector3(Math.Abs(side.x), 0, Math.Abs(side.z)) + Vector3.up * 0.12f);
            mb.Set(S.WoodDark, Color.Lerp(Color.white, pal.Wood * 2f, 0.3f));
            Vector3 w2 = new Vector3(Math.Abs(side.x) * 1.3f, 0, Math.Abs(side.z) * 1.3f);
            mb.BoxMM(Vector3.Min(a, b) - w2 + Vector3.up * (height - 0.07f), Vector3.Max(a, b) + w2 + Vector3.up * height);
            mb.Set(S.Gold, pal.Trim);
            mb.BoxMM(Vector3.Min(a, b) - w2 * 0.6f + Vector3.up * height, Vector3.Max(a, b) + w2 * 0.6f + Vector3.up * (height + 0.02f));
            // balusters (lathe)
            mb.Set(slotPost, tint);
            int n = Math.Max(1, (int)(L / spacing));
            var prof = new[] { new Vector2(0.034f, 0.12f), new Vector2(0.034f, 0.17f), new Vector2(0.02f, 0.21f), new Vector2(0.038f, 0.36f), new Vector2(0.042f, 0.44f), new Vector2(0.017f, 0.66f), new Vector2(0.016f, 0.8f), new Vector2(0.03f, 0.84f), new Vector2(0.028f, height - 0.07f) };
            for (int i = 0; i < n; i++)
            {
                var p = a + dir * ((i + 0.5f) * L / n);
                mb.Push(p, 0); mb.Lathe(prof, 7); mb.Pop();
            }
        }

        /// <summary>A wrought-iron pendant hanging from the slab where a column cannot stand (walkable cell below): an iron
        /// corona of candles on a rod, its lowest point at tipY (never lower than 2.45 m).</summary>
        void HangingFlesh(MeshBuilder mb, Vector3 basePos, float r, float top, float tipY, System.Random rnd, MansionPalette pal)
        {
            var iron = new Color(0.13f, 0.12f, 0.11f);
            float ringY = tipY + 0.25f;
            mb.Set(S.Iron, iron);
            mb.Rod(basePos + new Vector3(0, top, 0), basePos + new Vector3(0, ringY + 0.35f, 0), 0.012f, 6, false);
            // three chains down to the ring
            for (int k = 0; k < 3; k++)
            {
                float a = k / 3f * Mathf.PI * 2;
                mb.Rod(basePos + new Vector3(0, ringY + 0.35f, 0), basePos + new Vector3(Mathf.Cos(a) * 0.32f, ringY, Mathf.Sin(a) * 0.32f), 0.005f, 4, false);
            }
            mb.Torus(basePos + new Vector3(0, ringY, 0), 0.34f, 0.018f, 24, 5);
            mb.Torus(basePos + new Vector3(0, ringY - 0.06f, 0), 0.3f, 0.01f, 24, 4);
            // a pointed drop finial under the centre
            mb.Push(basePos + new Vector3(0, tipY, 0), 0);
            mb.Lathe(new[] { new Vector2(0.001f, 0f), new Vector2(0.03f, 0.08f), new Vector2(0.05f, 0.16f), new Vector2(0.02f, 0.22f), new Vector2(0.012f, ringY - tipY + 0.35f) }, 8);
            mb.Pop();
            // six candle cups with flames
            for (int k = 0; k < 6; k++)
            {
                float a = (k + 0.5f) / 6f * Mathf.PI * 2;
                var cup = basePos + new Vector3(Mathf.Cos(a) * 0.34f, ringY + 0.02f, Mathf.Sin(a) * 0.34f);
                mb.Set(S.Iron, iron); mb.Cyl(cup, 0.03f, 0.03f, 8);
                mb.Set(S.Wax, Color.white); mb.Cyl(cup + Vector3.up * 0.03f, 0.016f, 0.12f, 6);
                FlameQuad(mb, cup + Vector3.up * 0.155f, 0.06f, -2);
            }
        }

        /// <summary>A clustered stone pier on a plinth: four shafts around a core, moulded base, a flared capital with a gilt
        /// astragal. (It replaced the "flesh column", which read as a broken model.)</summary>
        void FleshColumn(MeshBuilder mb, Vector3 basePos, float r, float h, System.Random rnd, MansionPalette pal)
        {
            var stone = Color.Lerp(new Color(0.78f, 0.76f, 0.72f), pal.FloorA, 0.25f);
            // plinth
            mb.Set(S.Marble, Color.Lerp(Color.white, pal.FloorA, 0.3f));
            mb.Box(basePos + new Vector3(0, 0.15f, 0), new Vector3(r * 2.6f, 0.3f, r * 2.6f));
            mb.Set(S.Marble, stone);
            // moulded base
            mb.Push(basePos + new Vector3(0, 0.3f, 0), 0);
            mb.Lathe(new[] { new Vector2(r * 1.2f, 0f), new Vector2(r * 1.2f, 0.06f), new Vector2(r * 1.05f, 0.1f), new Vector2(r * 1.12f, 0.16f), new Vector2(r * 0.95f, 0.22f), new Vector2(r * 0.95f, 0.26f) }, 20, false, true);
            mb.Pop();
            // clustered shaft
            float y0 = 0.56f, y1 = h - 0.55f;
            mb.Cyl(basePos + new Vector3(0, y0, 0), r * 0.72f, y1 - y0, 16, false);
            for (int k = 0; k < 4; k++)
            {
                float a = k / 4f * Mathf.PI * 2 + Mathf.PI * 0.25f;
                mb.Cyl(basePos + new Vector3(Mathf.Cos(a) * r * 0.55f, y0, Mathf.Sin(a) * r * 0.55f), r * 0.36f, y1 - y0, 12, false);
            }
            // a shaft ring halfway up
            mb.Push(basePos + new Vector3(0, (y0 + y1) * 0.5f, 0), 0); mb.Lathe(new[] { new Vector2(r * 1.0f, -0.04f), new Vector2(r * 1.08f, 0f), new Vector2(r * 1.0f, 0.04f) }, 20); mb.Pop();
            // capital: bell, astragal, abacus
            mb.Push(basePos + new Vector3(0, y1, 0), 0);
            mb.Lathe(new[] { new Vector2(r * 0.95f, 0f), new Vector2(r * 1.0f, 0.1f), new Vector2(r * 1.25f, 0.3f), new Vector2(r * 1.4f, 0.38f) }, 20, false, false);
            mb.Pop();
            mb.Box(basePos + new Vector3(0, y1 + 0.45f, 0), new Vector3(r * 2.9f, 0.14f, r * 2.9f));
            mb.Set(S.Gold, new Color(0.72f, 0.58f, 0.36f));
            mb.Push(basePos + new Vector3(0, y1 + 0.02f, 0), 0); mb.Torus(Vector3.zero, r * 0.98f, 0.015f, 24, 4); mb.Pop();
        }

        // ------------------------------------------------------------------ stairs
        void BuildStairs()
        {
            foreach (var st in Layout.Stairs)
            {
                try
                {
                    if (st.Grand) BuildGrandStair(st);
                    else if ((st.Name == "심판장 승강기" || st.Name == "재판장 승강기")) BuildElevatorPath(st);
                    else BuildServiceStair(st);
                }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        void BuildElevatorPath(Stair st)
        {
            var a = ToWorld(st.A); var b = ToWorld(st.B);
            _stairPaths[st.Id] = new[] { a, a + Vector3.up * 0.01f, b + Vector3.up * 0.01f, b };
        }

        void BuildGrandStair(Stair st)
        {
            var gf = Layout.Furniture.FirstOrDefault(f => f.Type == "GrandStair");
            if (gf == null) return;
            var rv = Rooms[gf.Room]; var pal = rv.Pal;
            float yaw = gf.Yaw;
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 origin = ToWorld(gf.Pos);
            float W = 5f, D = 8.5f, H = 4.8f;
            // local z where the flight meets the landing (void edge)
            float edgeWorldZ = Math.Abs(yaw) < 1f ? VoidRect.z1 : VoidRect.z0;
            float zEdge = (edgeWorldZ - origin.z) * (Math.Abs(yaw) < 1f ? 1f : -1f);
            zEdge = Mathf.Clamp(zEdge, -D / 2 + 3f, D / 2);
            float z0 = -D / 2;
            float run = zEdge - z0;
            int steps = Mathf.RoundToInt(H / 0.172f);
            float rise = H / steps, tread = run / steps;
            float slope = H / run;
            float Soffit(float z) => Mathf.Max(0f, (z - z0) * slope - 0.5f);   // underside of the flight
            var marble = Color.Lerp(Color.white, pal.FloorA, 0.2f);
            var mb = new MeshBuilder();
            mb.Push(origin, yaw);
            float runner = 2.6f;
            float hw = W / 2 - 0.02f;
            for (int i = 0; i < steps; i++)
            {
                float y0 = i * rise, y1 = (i + 1) * rise;
                float za = z0 + i * tread, zb = za + tread;
                float flare = i < 3 ? (3 - i) * 0.1f : 0f;
                float w = hw + flare;
                // riser + tread (white marble), velvet runner with brass rods
                mb.Set(S.Marble, marble);
                mb.QuadAuto(new Vector3(-w, y0, za), new Vector3(-w, y1, za), new Vector3(w, y1, za), new Vector3(w, y0, za), Vector3.back);
                mb.Box(new Vector3(0, y1 - 0.025f, (za + zb) * 0.5f - 0.02f), new Vector3(w * 2, 0.05f, tread + 0.04f), MeshBuilder.Faces.PY | MeshBuilder.Faces.NZ | MeshBuilder.Faces.PX | MeshBuilder.Faces.NX);
                // cut-string side faces: the step profile down to the sloped soffit (open underneath)
                float sa = Soffit(za), sb = Soffit(zb);
                foreach (float sx in new[] { -1f, 1f })
                {
                    float x = sx * w;
                    mb.QuadAuto(new Vector3(x, sa, za), new Vector3(x, y1, za), new Vector3(x, y1, zb), new Vector3(x, sb, zb), new Vector3(sx, 0, 0));
                    // scroll bracket under each nosing (gilt curl)
                    mb.Set(S.Gold, pal.Trim);
                    mb.Push(new Vector3(x + sx * 0.012f, y1 - 0.12f, za + 0.06f), Quaternion.Euler(0, 0, sx * 90), Vector3.one);
                    mb.Torus(Vector3.zero, 0.05f, 0.012f, 10, 4, 0, 270);
                    mb.Pop();
                    mb.Set(S.Marble, marble);
                }
                // underside strip (dark plaster soffit with a gilt band every few steps)
                if (sb > 0.01f || sa > 0.01f)
                {
                    mb.Set(S.Plaster, Color.Lerp(pal.Wall, Color.black, 0.2f));
                    mb.QuadAuto(new Vector3(-w, sa, za), new Vector3(w, sa, za), new Vector3(w, sb, zb), new Vector3(-w, sb, zb), new Vector3(0, -1, slope * 0.3f));
                    if (i % 4 == 2)
                    {
                        mb.Set(S.Gold, pal.Trim);
                        mb.Push(new Vector3(0, (sa + sb) * 0.5f - 0.02f, (za + zb) * 0.5f), Quaternion.Euler(-Mathf.Atan(slope) * Mathf.Rad2Deg, 0, 0), Vector3.one);
                        mb.Box(Vector3.zero, new Vector3(W * 0.7f, 0.03f, 0.05f));
                        mb.Pop();
                    }
                }
                mb.Set(S.Velvet, pal.Carpet);
                mb.Box(new Vector3(0, y1 + 0.004f, (za + zb) * 0.5f - 0.01f), new Vector3(runner, 0.012f, tread + 0.01f), MeshBuilder.Faces.PY | MeshBuilder.Faces.NZ);
                mb.QuadAuto(new Vector3(-runner / 2, y0 + 0.01f, za - 0.03f), new Vector3(-runner / 2, y1 + 0.01f, za - 0.03f), new Vector3(runner / 2, y1 + 0.01f, za - 0.03f), new Vector3(runner / 2, y0 + 0.01f, za - 0.03f), Vector3.back);
                mb.Set(S.Brass, Color.white);
                mb.Rod(new Vector3(-runner / 2 - 0.05f, y0 + 0.03f, za - 0.05f), new Vector3(runner / 2 + 0.05f, y0 + 0.03f, za - 0.05f), 0.012f, 6, true);
            }
            // balustrades on both sides, newels with candelabra finials
            var flames = new List<Vector3>();
            var columns = new List<Vector3>();
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * (hw - 0.09f);
                SlopedBalustrade(mb, new Vector3(x, rise, z0 + tread * 0.5f), new Vector3(x, H, zEdge), pal);
                mb.Set(S.Marble, marble);
                var np = new Vector3(sx * (hw + 0.13f), 0, z0 + 0.12f);
                mb.Box(np + new Vector3(0, 0.65f, 0), new Vector3(0.42f, 1.3f, 0.42f));
                mb.Set(S.Gold, pal.Trim);
                mb.Box(np + new Vector3(0, 1.33f, 0), new Vector3(0.5f, 0.06f, 0.5f));
                mb.Push(np + new Vector3(0, 1.36f, 0), 0);
                mb.Lathe(new[] { new Vector2(0.12f, 0), new Vector2(0.05f, 0.15f), new Vector2(0.03f, 0.6f), new Vector2(0.08f, 0.66f), new Vector2(0.001f, 0.7f) }, 10);
                mb.Pop();
                for (int k = 0; k < 5; k++)
                {
                    float ang = k / 5f * Mathf.PI * 2; var arm = np + new Vector3(Mathf.Cos(ang) * 0.22f, 2.0f, Mathf.Sin(ang) * 0.22f);
                    mb.Set(S.Gold, pal.Trim); mb.Rod(np + new Vector3(0, 1.95f, 0), arm, 0.012f, 5, false);
                    mb.Set(S.Wax, Color.white); mb.Cyl(arm, 0.02f, 0.16f, 6);
                    mb.Cyl(arm + new Vector3(0.015f, -0.04f, 0), 0.008f, 0.05f, 4);
                    flames.Add(arm + Vector3.up * 0.17f);
                }
                columns.Add(new Vector3(sx * (hw - 0.55f), 0, z0 + run * 0.72f));
            }
            mb.Pop();
            // supporting stone piers under the high end (inside the footprint)
            foreach (var cp in columns)
            {
                float ch = Soffit(cp.z) + 0.1f;
                FleshColumn(mb, origin + q * cp, 0.22f, ch, rv.Rng, pal);
                AddDecorCollider(rv, origin + q * cp + Vector3.up * ch * 0.5f, new Vector3(0.6f, ch, 0.6f), "StairColumn");
            }
            // newel posts with their candelabra finials
            foreach (float sx in new[] { -1f, 1f })
                AddDecorCollider(rv, origin + q * new Vector3(sx * (hw + 0.13f), 1.1f, z0 + 0.12f), new Vector3(0.5f, 2.2f, 0.5f), "Newel");
            // a spill of candles on the floor under the stair (visible through the open sides)
            var under = new Vector3(0, 0, z0 + run * 0.62f);
            var uFlames = new List<Vector3>();
            CandleCluster(mb, origin + q * under, rv.Rng, uFlames);
            CandleCluster(mb, origin + q * (under + new Vector3(0.8f, 0, 0.6f)), rv.Rng, uFlames);
            foreach (var f in uFlames) FlameQuad(mb, f, 0.07f, -2);
            AddLight(rv, origin + q * (under + Vector3.up * 0.5f), rv.Pal.Warm, 3f, 5f, LightType.Point, false, 0.5f, fire: true);
            // newel flames (world space)
            foreach (var f in flames) FlameQuad(mb, origin + q * f, 0.08f, -2);
            foreach (float sx in new[] { -1f, 1f })
            {
                var lpos = origin + q * new Vector3(sx * (hw + 0.13f), 2.4f, z0 + 0.12f);
                AddLight(rv, lpos, rv.Pal.Warm, 3.2f, 6f, LightType.Point, false, 0.5f, fire: true);
            }
            Emit(rv, mb, "GrandStair");
            // ramp collider along the flight + thin side guards
            var col = new GameObject("GrandStairCollider"); col.transform.SetParent(rv.Root, false);
            col.transform.position = origin; col.transform.rotation = q;
            float len = Mathf.Sqrt(run * run + H * H); float ang2 = Mathf.Atan2(H, run) * Mathf.Rad2Deg;
            var ramp = new GameObject("Ramp"); ramp.transform.SetParent(col.transform, false);
            ramp.transform.localPosition = new Vector3(0, H * 0.5f - 0.12f * Mathf.Cos(ang2 * Mathf.Deg2Rad), (z0 + zEdge) * 0.5f + 0.12f * Mathf.Sin(ang2 * Mathf.Deg2Rad));
            ramp.transform.localRotation = Quaternion.Euler(-ang2, 0, 0);
            var rbc = ramp.AddComponent<BoxCollider>(); rbc.size = new Vector3(W, 0.24f, len);
            foreach (float sx in new[] { -1f, 1f })
            {
                var side = col.AddComponent<BoxCollider>();
                side.center = new Vector3(sx * (W / 2 - 0.05f), H * 0.5f + 0.5f, (z0 + zEdge) * 0.5f); side.size = new Vector3(0.1f, H + 1.0f, run);
            }
            // path for actors: A -> foot of flight -> top of flight -> B
            var a = ToWorld(st.A); var b = ToWorld(st.B);
            var foot = origin + q * new Vector3(0, 0, z0 - 0.3f);
            var head = origin + q * new Vector3(0, H, zEdge + 0.3f);
            _stairPaths[st.Id] = new[] { a, foot, head, b };
            rv.Blocked.Add(new RectF(Math.Min(foot.x, head.x) - 2.8f, Math.Min(foot.z, head.z) - 0.5f, Math.Max(foot.x, head.x) + 2.8f, Math.Max(foot.z, head.z) + 0.5f));
        }

        void SlopedBalustrade(MeshBuilder mb, Vector3 a, Vector3 b, MansionPalette pal)
        {
            float L = Vector3.Distance(a, b);
            mb.Set(S.WoodDark, Color.Lerp(Color.white, pal.Wood * 2f, 0.3f));
            mb.Bar(a + Vector3.up * 0.95f, b + Vector3.up * 0.95f, 0.12f, 0.08f);
            mb.Set(S.Gold, pal.Trim);
            mb.Bar(a + Vector3.up * 1.0f, b + Vector3.up * 1.0f, 0.06f, 0.02f);
            mb.Set(S.Marble, Color.Lerp(Color.white, pal.FloorA, 0.2f));
            int n = Math.Max(2, (int)(L / 0.26f));
            var prof = new[] { new Vector2(0.032f, 0f), new Vector2(0.032f, 0.06f), new Vector2(0.018f, 0.1f), new Vector2(0.036f, 0.26f), new Vector2(0.04f, 0.34f), new Vector2(0.016f, 0.58f), new Vector2(0.015f, 0.8f), new Vector2(0.028f, 0.92f) };
            for (int i = 1; i < n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n);
                mb.Push(p, 0); mb.Lathe(prof, 7); mb.Pop();
            }
        }

        void BuildServiceStair(Stair st)
        {
            var a = ToWorld(st.A); var b = ToWorld(st.B);
            var lo = a.y < b.y ? a : b; var hi = a.y < b.y ? b : a;
            var rv = Rooms[st.RoomA]; var pal = rv.Pal;
            float W = StairWidth(st);
            Vector3 flat = new Vector3(hi.x - lo.x, 0, hi.z - lo.z);
            float run = flat.magnitude; if (run < 0.5f) return;
            Vector3 dir = flat / run; Vector3 side = Vector3.Cross(Vector3.up, dir);
            float H = hi.y - lo.y;
            int steps = Mathf.Max(4, Mathf.RoundToInt(H / 0.2f));
            float rise = H / steps, tread = run / steps;
            var mb = new MeshBuilder();
            var q = Quaternion.LookRotation(dir, Vector3.up);
            mb.Push(Matrix4x4.TRS(lo, q, Vector3.one));
            // steps (stone treads with dark risers) on a thin sloped slab: floats like an Escher flight
            for (int i = 0; i < steps; i++)
            {
                float y0 = i * rise, y1 = (i + 1) * rise, za = i * tread, zb = za + tread;
                mb.Set(S.StoneFloor, Color.Lerp(Color.white, pal.FloorA, 0.2f));
                mb.Box(new Vector3(0, y1 - 0.025f, (za + zb) * 0.5f - 0.015f), new Vector3(W, 0.05f, tread + 0.03f), MeshBuilder.Faces.PY | MeshBuilder.Faces.NZ | MeshBuilder.Faces.PX | MeshBuilder.Faces.NX);
                mb.Set(S.WoodDark, Color.Lerp(Color.white, pal.Wood * 2f, 0.3f));
                mb.QuadAuto(new Vector3(-W / 2, y0, za), new Vector3(-W / 2, y1 - 0.05f, za), new Vector3(W / 2, y1 - 0.05f, za), new Vector3(W / 2, y0, za), Vector3.back);
                // side triangles (stringer faces): dressed stone, so the flight reads as masonry and not a black wedge
                mb.Set(S.StoneWall, new Color(0.72f, 0.7f, 0.66f));
                foreach (float sx in new[] { -1f, 1f })
                    mb.QuadAuto(new Vector3(sx * W / 2, y0 - 0.28f, za), new Vector3(sx * W / 2, y1, za), new Vector3(sx * W / 2, y1, zb), new Vector3(sx * W / 2, y1 - 0.28f, zb), new Vector3(sx, 0, 0));
            }
            // sloped underside
            mb.Set(S.Plaster, Color.Lerp(pal.Wall, Color.white, 0.3f));
            float slab = 0.28f;
            mb.QuadAuto(new Vector3(-W / 2, -slab, 0), new Vector3(W / 2, -slab, 0), new Vector3(W / 2, H - slab, run), new Vector3(-W / 2, H - slab, run), new Vector3(0, -run, H));
            // iron railings both sides
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * (W / 2 - 0.04f);
                mb.Set(S.Iron, new Color(0.18f, 0.17f, 0.18f));
                int n = Math.Max(2, (int)(run / 0.3f));
                for (int i = 0; i <= n; i++) { float z = run * i / n; float y = H * i / n; mb.Box(new Vector3(x, y + 0.5f, z), new Vector3(0.022f, 0.95f, 0.022f)); }
                mb.Set(S.WoodDark, Color.Lerp(Color.white, pal.Wood * 2f, 0.4f));
                mb.Bar(new Vector3(x, 0.98f, 0), new Vector3(x, H + 0.98f, run), 0.07f, 0.05f);
            }
            mb.Pop();
            Emit(rv, mb, "Stair_" + st.Id);
            // railings around the shaft opening on the upper floor
            int upId = Layout.RoomAt(st.A.f > st.B.f ? st.A : st.B);
            var upper = upId >= 0 && Rooms[upId] != null ? Rooms[upId] : rv;
            var mbR = new MeshBuilder();
            foreach (var h in upper.FloorHoles)
            {
                if (IsPoolHole(h)) continue;
                float y = upper.FloorY;
                bool longX = h.W >= h.D;
                // long sides
                if (longX)
                {
                    Balustrade(mbR, new Vector3(h.x0, y, h.z0 - 0.06f), new Vector3(h.x1, y, h.z0 - 0.06f), pal, false, 1.0f, 0.24f);
                    Balustrade(mbR, new Vector3(h.x0, y, h.z1 + 0.06f), new Vector3(h.x1, y, h.z1 + 0.06f), pal, false, 1.0f, 0.24f);
                    // closed end = the end away from the arriving flight (arrival is at 'hi')
                    float endX = Math.Abs(hi.x - h.x0) < Math.Abs(hi.x - h.x1) ? h.x1 : h.x0;
                    Balustrade(mbR, new Vector3(endX, y, h.z0), new Vector3(endX, y, h.z1), pal, false, 1.0f, 0.24f);
                }
                else
                {
                    Balustrade(mbR, new Vector3(h.x0 - 0.06f, y, h.z0), new Vector3(h.x0 - 0.06f, y, h.z1), pal, false, 1.0f, 0.24f);
                    Balustrade(mbR, new Vector3(h.x1 + 0.06f, y, h.z0), new Vector3(h.x1 + 0.06f, y, h.z1), pal, false, 1.0f, 0.24f);
                    float endZ = Math.Abs(hi.z - h.z0) < Math.Abs(hi.z - h.z1) ? h.z1 : h.z0;
                    Balustrade(mbR, new Vector3(h.x0, y, endZ), new Vector3(h.x1, y, endZ), pal, false, 1.0f, 0.24f);
                }
            }
            Emit(upper, mbR, "ShaftRail_" + st.Id);
            // ramp collider
            var go = new GameObject("StairRamp_" + st.Id); go.transform.SetParent(rv.Root, false);
            float len = Mathf.Sqrt(run * run + H * H); float ang = Mathf.Atan2(H, run);
            go.transform.position = (lo + hi) * 0.5f + Vector3.up * (-0.1f * Mathf.Cos(ang)) + dir * (0.1f * Mathf.Sin(ang));
            go.transform.rotation = q * Quaternion.Euler(-ang * Mathf.Rad2Deg, 0, 0);
            var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(W, 0.2f, len);
            _stairPaths[st.Id] = new[] { a, b };
        }
    }
}
