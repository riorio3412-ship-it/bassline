using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Non-blocking decor: paintings (scratched-face portraits), curtains, paint drips, sconce-lit portraits, clocks,
    /// dolls, per-room identity props and the mystery-room gimmicks. Everything stays on walls,
    /// above head height or in corners, away from doors, windows and furniture footprints.
    /// </summary>
    public sealed partial class MansionView
    {
        int _decorCount;

        void BuildAllDecor()
        {
            foreach (var rv in Rooms)
            {
                if (rv == null || rv.Room.Void || rv.Room.Type == RoomType.Courtroom) continue;
                try { DecorRoom(rv); } catch (Exception e) { Debug.LogWarning($"[Mansion] decor {rv.Room.Name}: {e.Message}\n{e.StackTrace}"); }
            }
            Stats.Decor = _decorCount;
        }

        // ------------------------------------------------------------------ wall slot tests
        bool WallFree(RoomView rv, Vector3 p, Vector3 n, float halfW, float bottomY)
        {
            var r = rv.Room;
            bool alongX = Mathf.Abs(n.z) > 0.5f;   // wall runs along x
            float line = alongX ? p.z - n.z * 0.1f : p.x - n.x * 0.1f;
            float along = alongX ? p.x : p.z;
            foreach (int did in r.Doors)
            {
                var d = Layout.Doors[did];
                if (d.AlongX != alongX) continue;
                float dl = alongX ? d.Pos.z : d.Pos.x;
                if (Mathf.Abs(dl - line) > 0.3f) continue;
                float da = alongX ? d.Pos.x : d.Pos.z;
                if (Mathf.Abs(da - along) < d.Width / 2 + halfW + 0.25f) return false;
            }
            foreach (var w in Windows)
            {
                if (w.Room != r.Id || Vector3.Dot(w.Normal, n) < 0.9f) continue;
                float wa = alongX ? w.Center.x : w.Center.z;
                float wl = alongX ? w.Center.z : w.Center.x;
                if (Mathf.Abs(wl - line) > 0.3f) continue;
                if (Mathf.Abs(wa - along) < w.W / 2 + halfW + 0.35f) return false;
            }
            // pilasters, pictures and sconces already on this wall
            foreach (var q in rv.WallReserved)
            {
                float ql = alongX ? q.y : q.x, qa = alongX ? q.x : q.y;
                if (Mathf.Abs(ql - line) > 0.35f) continue;
                if (Mathf.Abs(qa - along) < q.z + halfW + 0.1f) return false;
            }
            // furniture standing in front and reaching above the decor bottom
            var front = alongX ? new RectF(along - halfW, line + (n.z > 0 ? 0 : -0.8f), along + halfW, line + (n.z > 0 ? 0.8f : 0))
                               : new RectF(line + (n.x > 0 ? 0 : -0.8f), along - halfW, line + (n.x > 0 ? 0.8f : 0), along + halfW);
            foreach (int fid in r.Furniture)
            {
                var f = Layout.Furniture[fid];
                NavGrid.GetFootprint(f, 0.05f, out var fr);
                if (!fr.Overlaps(front)) continue;
                if (rv.FloorY + Mathf.Max(0.05f, f.H) > bottomY - 0.05f) return false;
            }
            return true;
        }

        /// <summary>Only the reserved spans (pilasters, pictures, sconces): for pieces centred over furniture.</summary>
        static bool ReservedFree(RoomView rv, Vector3 p, Vector3 n, float halfW)
        {
            bool alongX = Mathf.Abs(n.z) > 0.5f;
            float line = alongX ? p.z - n.z * 0.1f : p.x - n.x * 0.1f, along = alongX ? p.x : p.z;
            foreach (var q in rv.WallReserved)
            {
                float ql = alongX ? q.y : q.x, qa = alongX ? q.x : q.y;
                if (Mathf.Abs(ql - line) > 0.35f) continue;
                if (Mathf.Abs(qa - along) < q.z + halfW + 0.1f) return false;
            }
            return true;
        }

        bool FloorFree(RoomView rv, float x, float z, float rad)
        {
            var q = new RectF(x - rad, z - rad, x + rad, z + rad);
            if (!rv.Room.Rect.Inset(0.15f).Contains(x, z)) return false;
            foreach (var b in rv.Blocked) if (b.Overlaps(q)) return false;
            foreach (var h in rv.FloorHoles) if (h.Overlaps(q)) return false;
            foreach (var st in Layout.Stairs)
            {
                foreach (var e in new[] { st.A, st.B })
                    if (e.f == rv.Room.Floor && Mathf.Abs(e.x - x) < 1.4f && Mathf.Abs(e.z - z) < 1.4f) return false;
            }
            foreach (int sid in rv.Room.Spots)
            {
                var s = Layout.Spots[sid];
                if (Mathf.Abs(s.Approach.x - x) < rad + 0.35f && Mathf.Abs(s.Approach.z - z) < rad + 0.35f) return false;
                if (Mathf.Abs(s.Pos.x - x) < rad + 0.35f && Mathf.Abs(s.Pos.z - z) < rad + 0.35f) return false;
            }
            return true;
        }

        /// <summary>Corners of the room (inset) usable for small floor decor.</summary>
        IEnumerable<Vector3> FreeCorners(RoomView rv, float rad)
        {
            var R = rv.Room.Rect; float i = 0.18f + rad;
            foreach (var c in new[] { new Vector2(R.x0 + i, R.z0 + i), new Vector2(R.x1 - i, R.z0 + i), new Vector2(R.x0 + i, R.z1 - i), new Vector2(R.x1 - i, R.z1 - i) })
            {
                // cell must belong to the room (landing ring etc.)
                if (_grids[rv.Room.Floor].At(c.x, c.y) != rv.Room.Id) continue;
                if (FloorFree(rv, c.x, c.y, rad)) yield return new Vector3(c.x, rv.FloorY, c.y);
            }
        }

        // ------------------------------------------------------------------ main per-room decor
        void DecorRoom(RoomView rv)
        {
            var r = rv.Room; var pal = rv.Pal; var rnd = rv.Rng; var st = rv.Style;
            var mb = new MeshBuilder();          // opaque decor
            var tr = new MeshBuilder();          // transparent decals (drips)
            bool formal = st.Lower == S.Wainscot || r.Type == RoomType.Chapel;
            bool utility = IsUtility(r.Type);
            // ---- wall art: few and meaningful. One focal piece above the hearth / sideboard / bed / sofa, one or two more in
            // big rooms, a rhythm of pieces down corridors. Scanned oil paintings, portraits whose faces were scratched out,
            // empty frames showing only wallpaper, and cracked mirrors.
            int placed = 0;
            bool artRoom = !(r.Type == RoomType.Bedroom && OwnerStyles.Get(r.Owner) != null) && !(utility && r.Type != RoomType.Workshop) && r.Type != RoomType.Greenhouse && r.Type != RoomType.Courtyard && r.Type != RoomType.WhiteDoors && r.Type != RoomType.Pool && r.Type != RoomType.Kitchen && r.Type != RoomType.ClockMuseum;
            int maxArt = r.Type == RoomType.Corridor || r.Type == RoomType.Landing ? Mathf.Max(1, (int)(Mathf.Max(r.Rect.W, r.Rect.D) / 7f)) : r.Type == RoomType.Gallery ? 99 : Mathf.Clamp((int)(r.Rect.Area / 40f) + 1, 1, 3);
            var artDone = new List<Vector3>();
            if (artRoom)
            {
                // focal anchors first: the widest wall piece people sit or stand in front of
                var anchors = new List<(Vector3 p, Vector3 n, float w, float bottom)>();
                foreach (int fid in r.Furniture)
                {
                    var f = Layout.Furniture[fid];
                    if (f.Type != "Fireplace" && f.Type != "Sideboard" && f.Type != "Console" && f.Type != "Bed" && f.Type != "Sofa" && f.Type != "DayBed" && f.Type != "Piano_Upright" && f.Type != "Altar" && f.Type != "Desk") continue;
                    float yr = f.Yaw * Mathf.Deg2Rad; var fwd = new Vector3(Mathf.Sin(yr), 0, Mathf.Cos(yr));
                    var back = new Vector3(f.Pos.x, rv.FloorY, f.Pos.z) - fwd * (f.D * 0.5f + 0.03f);
                    // only pieces standing against a wall
                    if (_grids[r.Floor].At(back.x - fwd.x * 0.3f, back.z - fwd.z * 0.3f) == r.Id) continue;
                    float top = f.Type == "Fireplace" ? f.H + 0.25f : f.Type == "Bed" ? 1.45f : Mathf.Max(f.H, 0.9f) + 0.3f;
                    anchors.Add((back + fwd * 0.1f, fwd, f.Type == "Sofa" || f.Type == "Fireplace" || f.Type == "Bed" ? 1.0f : 0.75f, rv.FloorY + top));
                }
                anchors.Sort((a, b) => b.w.CompareTo(a.w));
                foreach (var an in anchors)
                {
                    if (placed >= maxArt) break;
                    bool near = false; foreach (var q0 in artDone) if (Vector3.Distance(q0, an.p) < 2.2f) near = true;
                    if (near) continue;
                    float w = an.w * (0.85f + (float)rnd.NextDouble() * 0.3f), h = w * 0.8f;
                    float cy = Mathf.Min(an.bottom + h * 0.5f + 0.1f, rv.CeilY - 0.75f - h * 0.5f);
                    if (cy - h * 0.5f < an.bottom - 0.05f) continue;
                    if (!ReservedFree(rv, an.p, an.n, w * 0.5f + 0.05f)) continue;   // never across a pilaster
                    WallArt(rv, mb, new Vector3(an.p.x, cy, an.p.z) + an.n * 0.005f - an.n * 0.1f, an.n, w, h, formal, 0);
                    rv.WallReserved.Add(new Vector4(an.p.x, an.p.z, w * 0.5f + 0.05f, 0));
                    artDone.Add(an.p); placed++; _decorCount++;
                }
                for (int i = 0; i < rv.WallSlots.Count && placed < maxArt; i++)
                {
                    var p = rv.WallSlots[i]; var n = rv.WallSlotN[i];
                    if (rnd.NextDouble() < 0.55) continue;
                    bool near = false; foreach (var q0 in artDone) if (Vector3.Distance(q0, p) < (r.Type == RoomType.Corridor ? 5.5f : 3.5f)) near = true;
                    if (near) continue;
                    float w = 0.6f + (float)rnd.NextDouble() * 0.4f; float h = w * (1.15f + (float)rnd.NextDouble() * 0.2f);
                    float cy = rv.FloorY + Mathf.Min(1.9f, rv.CeilY - rv.FloorY - 0.7f - h * 0.5f);
                    if (!WallFree(rv, p, n, w * 0.5f + 0.1f, cy - h * 0.5f)) continue;
                    WallArt(rv, mb, new Vector3(p.x, cy, p.z) + n * 0.005f, n, w, h, formal, 1 + rnd.Next(4));
                    rv.WallReserved.Add(new Vector4(p.x, p.z, w * 0.5f + 0.05f, 0));
                    artDone.Add(p); placed++; _decorCount++;
                }
            }
            // ---- curtains on windows (formal rooms, corridors)
            foreach (var w in Windows)
            {
                if (w.Room != r.Id) continue;
                if (utility || r.Type == RoomType.Kitchen || r.Type == RoomType.Infirmary) continue;
                Curtains(mb, w, rv);
                _decorCount++;
            }
            // ---- magenta paint drips running down from the cornice (grotesque accent)
            double dripChance = RoomInfo.IsMystery(r.Type) ? 0.7 : r.Type == RoomType.GrandHall || r.Type == RoomType.Landing ? 0.45 : r.Type == RoomType.Corridor ? 0.15 : 0.1;
            if (!utility && rnd.NextDouble() < dripChance && rv.WallSlots.Count > 0)
            {
                int n = 1 + rnd.Next(r.Type == RoomType.GrandHall ? 4 : 2);
                for (int k = 0; k < n; k++)
                {
                    int si = rnd.Next(rv.WallSlots.Count);
                    var p = rv.WallSlots[si]; var nn = rv.WallSlotN[si];
                    float along = ((float)rnd.NextDouble() - 0.5f) * 1.0f;
                    Vector3 side = Vector3.Cross(Vector3.up, nn);
                    float top = rv.CeilY - 0.27f, len = 0.9f + (float)rnd.NextDouble() * 1.6f, wdt = 0.5f + (float)rnd.NextDouble() * 0.6f;
                    var c0 = new Vector3(p.x, top, p.z) + side * along + nn * 0.006f;
                    var rect = ProcTex.DecalRect(ProcTex.Decal.PaintDrip);
                    // the walls weep: dark oxblood seeping from the cornice (never bright paint)
                    Color dc = r.Type == RoomType.WhiteDoors ? new Color(0.36f, 0.05f, 0.06f) : new Color(0.2f, 0.03f, 0.035f);
                    tr.Set(S.Decal, new Color(dc.r, dc.g, dc.b, 0.8f), new Vector4(0.85f, 0.25f, 1.5f, 0));
                    tr.QuadUV(c0 - side * wdt * 0.5f - Vector3.up * len, c0 - side * wdt * 0.5f, c0 + side * wdt * 0.5f, c0 + side * wdt * 0.5f - Vector3.up * len,
                        new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin));
                    _decorCount++;
                }
            }
            // ---- corner candle clusters (hall, chapel, landings, mystery, corridors sometimes)
            if (r.Type == RoomType.GrandHall || r.Type == RoomType.Chapel || r.Type == RoomType.Landing || (RoomInfo.IsMystery(r.Type) && r.Type != RoomType.WhiteDoors && r.Type != RoomType.MirrorWater && r.Type != RoomType.RainCorridor) || (r.Type == RoomType.Corridor && rnd.NextDouble() < 0.25) || r.Type == RoomType.WineCellar)
            {
                var flames = new List<Vector3>();
                int nc = 0;
                foreach (var c in FreeCorners(rv, 0.22f))
                {
                    if (nc++ > 2) break;
                    CandleCluster(mb, c, rnd, flames);
                }
                foreach (var f in flames) FlameQuad(mb, f, 0.07f, -2);
                if (flames.Count > 0) AddLight(rv, flames[0] + Vector3.up * 0.2f, pal.Warm, 1.6f, 3f, LightType.Point, false, 0.6f, fire: true);
            }
            // (no fish swimming through the air — user directive 2026-09-27: fish stay inside aquariums only)
            // ---- per-type identity
            RoomIdentity(rv, mb, tr);
            try { HangingDecor(rv, mb); } catch (Exception e) { Debug.LogWarning("[Mansion] hanging decor " + rv.Room.Name + ": " + e.Message); }
            Emit(rv, mb, "Decor");
            Emit(rv, tr, "DecorDecals", null, ShadowCastingMode.Off);
        }

        /// <summary>One piece of wall art centred at 'center' on the wall facing 'n'. focal = 0 favours a real painting.</summary>
        void WallArt(RoomView rv, MeshBuilder mb, Vector3 center, Vector3 n, float w, float h, bool formal, int hint)
        {
            var rnd = rv.Rng; var pal = rv.Pal;
            var q = Quaternion.LookRotation(n, Vector3.up);
            double roll = rnd.NextDouble();
            int kind = hint == 0 ? (roll < 0.55 ? 1 : roll < 0.85 ? 0 : 3) : (roll < 0.4 ? 0 : roll < 0.7 ? 1 : roll < 0.8 ? 2 : 3);
            if (kind == 1)
            {
                // CC0 scanned oil painting in a gilt frame
                var pm = Models.Get(rnd.NextDouble() < 0.65 ? "fancy_picture_frame_01" : "fancy_picture_frame_02");
                if (pm != null)
                {
                    var pg = Models.Place(pm, rv.Root, center, Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg, new Vector3(w * 1.25f, h * 1.15f, 0), null, true, Models.Anchor.Center, 0f);
                    pg.transform.position = center + n * 0.012f;
                    // a slight list: nobody straightens the pictures here
                    if (rnd.NextDouble() < 0.3) pg.transform.rotation = pg.transform.rotation * Quaternion.Euler(0, 0, (float)(rnd.NextDouble() - 0.5) * 7f);
                    foreach (var rr in pg.GetComponentsInChildren<Renderer>()) { rv.Renderers.Add(rr); rr.shadowCastingMode = ShadowCastingMode.Off; }
                    rv.StaticDecor.Add(pg);
                }
                else kind = 0;
            }
            mb.Push(Matrix4x4.TRS(center, q, Vector3.one));
            if (kind == 0) FurnitureFactory.PictureFrame(mb, Vector3.zero, w * 0.85f, h * 1.1f, rnd.Next(8), pal, true);
            else if (kind == 2)
            {
                // an empty frame: only the wallpaper inside, and a paler rectangle where a canvas used to hang
                mb.Set(S.Gold, pal.Trim);
                float fw = w * 0.85f, fh = h * 1.1f, t = 0.07f;
                mb.Box(new Vector3(0, fh / 2, 0.02f), new Vector3(fw, t, 0.04f)); mb.Box(new Vector3(0, -fh / 2, 0.02f), new Vector3(fw, t, 0.04f));
                mb.Box(new Vector3(-fw / 2, 0, 0.02f), new Vector3(t, fh, 0.04f)); mb.Box(new Vector3(fw / 2, 0, 0.02f), new Vector3(t, fh, 0.04f));
                mb.Set(S.Plaster, Color.Lerp(rv.Style.WallA, Color.white, 0.25f));
                mb.Box(new Vector3(0, 0, 0.002f), new Vector3(fw - t, fh - t, 0.002f), MeshBuilder.Faces.PZ);
            }
            else if (kind == 3)
            {
                OvalMirror(mb, Vector3.zero, w * 0.75f, h * 0.9f, pal);
                // a crack spidering from one point of the glass
                mb.Set(S.Obsidian, new Color(0.2f, 0.2f, 0.22f));
                var o = new Vector3(((float)rnd.NextDouble() - 0.5f) * w * 0.3f, ((float)rnd.NextDouble() - 0.2f) * h * 0.3f, 0.03f);
                for (int k = 0; k < 7; k++)
                {
                    float a = k / 7f * Mathf.PI * 2 + (float)rnd.NextDouble() * 0.6f; float len = (0.12f + (float)rnd.NextDouble() * 0.22f) * w;
                    var e = o + new Vector3(Mathf.Cos(a) * len, Mathf.Sin(a) * len, 0);
                    mb.Bar(o, e, 0.004f, 0.002f);
                }
            }
            mb.Pop();
            // picture light for paintings in formal rooms
            if (formal && kind <= 1 && rnd.NextDouble() < 0.6)
            {
                mb.Push(Matrix4x4.TRS(center + Vector3.up * (h * 0.58f + 0.08f), q, Vector3.one));
                mb.Set(S.Brass, Color.white); mb.Box(new Vector3(0, 0, 0.1f), new Vector3(w * 0.5f, 0.04f, 0.05f)); mb.Rod(new Vector3(0, 0, 0.0f), new Vector3(0, 0.02f, 0.1f), 0.01f, 5, false);
                mb.Set(S.Glow, Color.Lerp(pal.Warm, new Color(1f, 0.86f, 0.66f), 0.5f), MansionMats.GlowData(0.5f, 0.05f, 0, rv.Room.Circuit)); mb.Box(new Vector3(0, -0.027f, 0.12f), new Vector3(w * 0.4f, 0.004f, 0.016f));   // only the underside of the brass hood glows, faintly
                mb.Pop();
            }
        }

        // ------------------------------------------------------------------ small builders
        internal static void WallClock(MeshBuilder mb, Vector3 c, float r, MansionPalette pal, int seed, float speed)
        {
            mb.Set(S.Gold, pal.Trim);
            mb.Push(c + new Vector3(0, 0, 0.02f), Quaternion.Euler(90, 0, 0), Vector3.one);
            mb.Lathe(new[] { new Vector2(r * 1.2f, -0.02f), new Vector2(r * 1.25f, 0f), new Vector2(r * 1.1f, 0.02f) }, 24, false, true);
            mb.Pop();
            // sunburst rays
            for (int k = 0; k < 12; k++) { float a = k / 12f * Mathf.PI * 2; mb.Bar(c + new Vector3(Mathf.Cos(a) * r * 1.2f, Mathf.Sin(a) * r * 1.2f, 0.01f), c + new Vector3(Mathf.Cos(a) * r * (1.6f + (k % 2) * 0.3f), Mathf.Sin(a) * r * (1.6f + (k % 2) * 0.3f), 0.01f), 0.012f, 0.01f); }
            mb.Set(S.Clock, Color.white, new Vector4(4, (seed * 5 % 12) + (seed % 7) * 0.13f, speed, 9));
            mb.FaceUV(c + new Vector3(r, -r, 0.043f), new Vector3(-2 * r, 0, 0), new Vector3(0, 2 * r, 0), new Rect(1, 0, -1, 1));
        }

        static void OvalMirror(MeshBuilder mb, Vector3 c, float w, float h, MansionPalette pal)
        {
            mb.Set(S.Gold, Color.Lerp(pal.Trim, new Color(0.42f, 0.33f, 0.2f), 0.55f));   // tarnished gilt, not a glowing hoop
            mb.Push(c, Quaternion.Euler(90, 0, 0), new Vector3(w * 0.5f, 1, h * 0.5f));
            mb.Torus(new Vector3(0, 0.02f, 0), 1f, 0.07f, 28, 6);
            mb.Set(S.Mirror, Color.white);
            mb.Disc(new Vector3(0, 0.025f, 0), 0.97f, 28, true);
            mb.Pop();
        }

        void Curtains(MeshBuilder mb, WindowInfo w, RoomView rv)
        {
            var pal = rv.Pal; var n = w.Normal;
            var ownC = rv.Room.Type == RoomType.Bedroom ? OwnerStyles.Get(rv.Room.Owner) : null;   // a resident's trim wears their signature colour
            Vector3 side = Vector3.Cross(Vector3.up, n);
            float top = rv.CeilY - 0.3f, bottom = rv.FloorY + 0.02f;
            Color c = pal.Fabric;
            // rod
            mb.Set(S.Brass, Color.white);
            var rodA = w.Center + side * (w.W * 0.5f + 0.55f) + n * 0.2f; var rodB = w.Center - side * (w.W * 0.5f + 0.55f) + n * 0.2f;
            rodA.y = top; rodB.y = top;
            mb.Rod(rodA, rodB, 0.018f, 6, true);
            mb.Sphere(rodA, 0.05f, 8, 6); mb.Sphere(rodB, 0.05f, 8, 6);
            // two drapes tied back at 1.1 m, folds as tubes
            foreach (float s in new[] { 1f, -1f })
            {
                mb.Set(S.Velvet, c);
                var edge = w.Center + side * s * (w.W * 0.5f + 0.1f) + n * 0.17f;
                for (int k = 0; k < 5; k++)
                {
                    float off = k * 0.09f;
                    var pts = new List<Vector3>();
                    for (int j = 0; j <= 8; j++)
                    {
                        float t = j / 8f; float y = Mathf.Lerp(top - 0.02f, bottom, t);
                        float pinch = Mathf.Exp(-Mathf.Pow((y - (rv.FloorY + 1.15f)) / 0.35f, 2));
                        float spread = Mathf.Lerp(0.45f, 0.08f, pinch) * (1f - t * 0.2f);
                        pts.Add(edge + side * s * (off * spread / 0.45f + (0.05f)) + n * (Mathf.Sin(k * 1.7f + t * 3f) * 0.03f) + Vector3.up * (y - edge.y));
                    }
                    mb.Tube(pts, 0.055f, 6);
                }
                mb.Set(ownC != null ? S.Velvet : S.Gold, ownC != null ? ownC.Sig : pal.Trim);
                mb.Torus(edge + side * s * 0.12f + Vector3.up * (rv.FloorY + 1.15f - edge.y), 0.1f, 0.012f, 12, 4);
            }
            // valance with teeth-like scallops
            mb.Set(S.Velvet, ownC != null ? Color.Lerp(ownC.Sig, Color.black, 0.35f) : Color.Lerp(c, Color.black, 0.2f));
            var vc = w.Center + n * 0.2f; vc.y = top - 0.12f;
            mb.Box(vc, new Vector3(Mathf.Abs(side.x) * (w.W + 1.3f) + Mathf.Abs(n.x) * 0.06f, 0.24f, Mathf.Abs(side.z) * (w.W + 1.3f) + Mathf.Abs(n.z) * 0.06f));
            int sc = (int)((w.W + 1.2f) / 0.2f);
            for (int k = 0; k < sc; k++)
            {
                var p = vc - side * ((w.W + 1.2f) * 0.5f) + side * ((k + 0.5f) * (w.W + 1.2f) / sc) + Vector3.down * 0.16f + n * 0.03f;
                mb.Push(p, Quaternion.LookRotation(n, Vector3.up), Vector3.one);
                mb.Lathe(new[] { new Vector2(0.06f, 0.05f), new Vector2(0.001f, -0.09f) }, 6);
                mb.Pop();
            }
        }

        static void CandleCluster(MeshBuilder mb, Vector3 c, System.Random rnd, List<Vector3> flames)
        {
            // a spill of melted wax with candles of different heights
            mb.Set(S.Wax, Color.white);
            mb.Push(c + Vector3.up * 0.002f, 0); mb.Lathe(new[] { new Vector2(0.001f, 0.02f), new Vector2(0.18f, 0.01f), new Vector2(0.24f, 0f) }, 14); mb.Pop();
            int n = 4 + rnd.Next(4);
            for (int i = 0; i < n; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2, rr = (float)rnd.NextDouble() * 0.15f;
                var p = c + new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr);
                float h = 0.08f + (float)rnd.NextDouble() * 0.35f, r = 0.02f + (float)rnd.NextDouble() * 0.025f;
                FurnitureFactory.Candle(mb, p, h, r, -2, p + Vector3.up * h, flames);
            }
        }

        static void Garland(MeshBuilder mb, Vector3 a, Vector3 b, float sag, float r, int slot, Color c)
        {
            mb.Set(slot, c);
            var pts = new List<Vector3>();
            for (int i = 0; i <= 10; i++) { float t = i / 10f; pts.Add(Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1 - t)); }
            mb.Tube(pts, r, 5);
        }

        // ------------------------------------------------------------------ room identity & gimmicks
        void RoomIdentity(RoomView rv, MeshBuilder mb, MeshBuilder tr)
        {
            var r = rv.Room; var pal = rv.Pal; var R = r.Rect; var rnd = rv.Rng;
            float fy = rv.FloorY, ceil = rv.CeilY;
            switch (r.Type)
            {
                case RoomType.Kitchen:
                    {
                        // hanging copper pot rack over the centre
                        var c = new Vector3(R.CX, ceil - 1.2f, R.CZ);
                        mb.Set(S.Iron, new Color(0.15f, 0.15f, 0.15f));
                        mb.Box(c, new Vector3(1.6f, 0.04f, 0.5f));
                        foreach (var o in new[] { new Vector3(-0.7f, 0, -0.2f), new Vector3(0.7f, 0, -0.2f), new Vector3(-0.7f, 0, 0.2f), new Vector3(0.7f, 0, 0.2f) }) mb.Rod(c + o, new Vector3(c.x + o.x, ceil, c.z + o.z), 0.008f, 4, false);
                        for (int i = 0; i < 6; i++)
                        {
                            var p = c + new Vector3(-0.65f + i * 0.26f, -0.05f, (i % 2) * 0.2f - 0.1f);
                            mb.Set(S.Iron, Color.white); mb.Rod(p, p + Vector3.down * 0.12f, 0.004f, 3, false);
                            mb.Set(S.Copper, Color.white); mb.Push(p + Vector3.down * 0.35f, 0);
                            float rr = 0.08f + (i % 3) * 0.03f; mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(rr, 0.01f), new Vector2(rr, 0.12f + rr * 0.3f) }, 12); mb.Pop();
                        }
                        // strings of garlic / herbs
                        for (int i = 0; i < 4; i++) { var p = c + new Vector3(-0.6f + i * 0.4f, -0.05f, 0.25f); mb.Set(S.Leaf, new Color(0.4f, 0.5f, 0.25f)); mb.Rod(p, p + Vector3.down * 0.4f, 0.015f, 4, false); }
                        break;
                    }
                case RoomType.Laundry:
                    {
                        // clothes lines across the room with sheets (above head)
                        bool ax = R.W >= R.D;
                        for (int k = 0; k < 3; k++)
                        {
                            float t = (k + 1) / 4f;
                            var a = ax ? new Vector3(R.x0 + 0.2f, ceil - 0.5f, R.z0 + t * R.D) : new Vector3(R.x0 + t * R.W, ceil - 0.5f, R.z0 + 0.2f);
                            var b = ax ? new Vector3(R.x1 - 0.2f, ceil - 0.5f, a.z) : new Vector3(a.x, ceil - 0.5f, R.z1 - 0.2f);
                            Garland(mb, a, b, 0.1f, 0.006f, S.Linen, Color.white);
                            int ns = 3;
                            for (int s = 0; s < ns; s++)
                            {
                                float u = (s + 0.5f) / ns; var p = Vector3.Lerp(a, b, u) + Vector3.down * 0.09f;
                                Vector3 dir = (b - a).normalized; Vector3 nn = Vector3.Cross(dir, Vector3.up);
                                mb.Set(S.Linen, s == 1 && k == 1 ? Color.Lerp(Color.white, new Color(0.7f, 0.2f, 0.25f), 0.35f) : Color.Lerp(Color.white, pal.Accent2, 0.1f * s));
                                mb.QuadAuto(p - dir * 0.4f, p + dir * 0.4f, p + dir * 0.4f + Vector3.down * 0.72f + nn * 0.02f, p - dir * 0.4f + Vector3.down * 0.76f, nn);
                                mb.QuadAuto(p - dir * 0.4f, p + dir * 0.4f, p + dir * 0.4f + Vector3.down * 0.72f + nn * 0.02f, p - dir * 0.4f + Vector3.down * 0.76f, -nn);
                            }
                        }
                        break;
                    }
                case RoomType.Greenhouse:
                    {
                        // vines hanging from the glass roof
                        for (int i = 0; i < 14; i++)
                        {
                            var p = new Vector3(R.x0 + (float)rnd.NextDouble() * R.W, ceil + 0.1f, R.z0 + (float)rnd.NextDouble() * R.D);
                            float len = 0.8f + (float)rnd.NextDouble() * 1.4f;
                            var pts = new List<Vector3>(); for (int j = 0; j <= 6; j++) pts.Add(p + Vector3.down * (len * j / 6f) + new Vector3(Mathf.Sin(j * 1.3f + i) * 0.08f, 0, Mathf.Cos(j * 1.1f + i) * 0.08f));
                            mb.Set(S.Leaf, new Color(0.2f, 0.4f, 0.18f)); mb.Tube(pts, 0.008f, 4);
                            for (int j = 1; j <= 6; j++) { var q = pts[j]; mb.Set(S.Leaf, Color.Lerp(new Color(0.15f, 0.4f, 0.2f), pal.Accent2, 0.2f)); mb.Quad(q, q + new Vector3(0.08f, -0.04f, 0.02f), q + new Vector3(0.1f, -0.1f, 0.03f), q + new Vector3(0.02f, -0.09f, 0)); }
                        }
                        // mist lanterns on the planters
                        break;
                    }
                case RoomType.Courtyard:
                    {
                        // ivy on the walls + lanterns on posts in corners
                        for (int i = 0; i < rv.WallSlots.Count; i += 2)
                        {
                            var p = rv.WallSlots[i]; var n = rv.WallSlotN[i];
                            Vector3 side = Vector3.Cross(Vector3.up, n);
                            mb.Set(S.Leaf, new Color(0.12f, 0.3f, 0.14f));
                            for (int k = 0; k < 18; k++)
                            {
                                var q = new Vector3(p.x, fy + (float)rnd.NextDouble() * (ceil - fy - 0.3f), p.z) + side * (((float)rnd.NextDouble() - 0.5f) * 1.6f) + n * 0.02f;
                                mb.Push(Matrix4x4.TRS(q, Quaternion.LookRotation(n) * Quaternion.Euler(0, 0, (float)rnd.NextDouble() * 360f), Vector3.one * (0.08f + (float)rnd.NextDouble() * 0.08f)));
                                mb.Quad(new Vector3(0, 0, 0), new Vector3(-1, 1, 0.1f), new Vector3(0, 1.6f, 0.15f), new Vector3(1, 1, 0.1f));
                                mb.Pop();
                            }
                        }
                        var flames = new List<Vector3>();
                        foreach (var c in FreeCorners(rv, 0.2f))
                        {
                            mb.Set(S.Iron, new Color(0.12f, 0.12f, 0.12f)); mb.Cyl(c, 0.06f, 1.6f, 8);
                            mb.Push(c + Vector3.up * 1.6f, 45f); for (int k = 0; k < 4; k++) { float a = k * Mathf.PI / 2; mb.Box(new Vector3(Mathf.Cos(a) * 0.12f, 0.2f, Mathf.Sin(a) * 0.12f), new Vector3(0.02f, 0.38f, 0.02f)); } mb.Box(new Vector3(0, 0.41f, 0), new Vector3(0.3f, 0.04f, 0.3f)); mb.Pop();
                            flames.Add(c + Vector3.up * 1.72f);
                            AddDecorCollider(rv, c + Vector3.up * 1.0f, new Vector3(0.32f, 2.0f, 0.32f), "LanternPost");
                        }
                        foreach (var f in flames) { FlameQuad(mb, f, 0.1f, -2); AddLight(rv, f + Vector3.up * 0.1f, pal.Warm, 1.5f, 3.5f, LightType.Point, false, 0.5f, fire: true); }
                        // the moon, huge, in the sky above
                        break;
                    }
                case RoomType.GameRoom:
                    {
                        // a carved, gilded all-seeing eye over the room (no neon: a relief catching the lamplight)
                        if (rv.WallSlots.Count > 0)
                        {
                            int si = rv.WallSlots.Count / 2; var p = rv.WallSlots[si]; var n = rv.WallSlotN[si];
                            if (WallFree(rv, p, n, 0.6f, fy + 2.2f))
                            {
                                var c = new Vector3(p.x, fy + 2.6f, p.z) + n * 0.05f;
                                mb.Set(S.Gold, new Color(0.8f, 0.64f, 0.4f));
                                var pts = new List<Vector3>(); var q = Quaternion.LookRotation(n);
                                for (int k = 0; k <= 24; k++) { float t = k / 24f * Mathf.PI * 2; pts.Add(c + q * new Vector3(Mathf.Cos(t) * 0.45f, Mathf.Sin(t) * 0.18f * Mathf.Abs(Mathf.Cos(t) * 0.4f + 0.6f), 0)); }
                                mb.Tube(pts, 0.015f, 5);
                                mb.Set(S.Obsidian, Color.white);
                                mb.Push(Matrix4x4.TRS(c, q * Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Cyl(Vector3.zero, 0.085f, 0.02f, 16); mb.Pop();   // the pupil, black glass
                                mb.Set(S.Gold, new Color(0.8f, 0.64f, 0.4f));
                                var ip = new List<Vector3>(); for (int k = 0; k <= 16; k++) { float t = k / 16f * Mathf.PI * 2; ip.Add(c + q * new Vector3(Mathf.Cos(t) * 0.1f, Mathf.Sin(t) * 0.1f, 0)); }
                                mb.Tube(ip, 0.015f, 5);
                            }
                        }
                        break;
                    }
                case RoomType.Theater:
                    {
                        // tragedy/comedy masks above the proscenium side walls
                        break;
                    }
                case RoomType.DollRoom:
                    {
                        // a large doll sitting in a corner whose head follows you
                        foreach (var c in FreeCorners(rv, 0.3f))
                        {
                            FurnitureFactory.Doll(mb, c, 0.95f, rnd, pal, false);
                            break;
                        }
                        // paper garlands of small dolls
                        Garland(mb, new Vector3(R.x0 + 0.3f, ceil - 0.5f, R.z0 + 0.3f), new Vector3(R.x1 - 0.3f, ceil - 0.5f, R.z1 - 0.3f), 0.4f, 0.006f, S.Linen, pal.Neon);
                        break;
                    }
                case RoomType.TrophyRoom:
                    {
                        // antlers mounted on the walls
                        for (int i = 0; i < rv.WallSlots.Count; i += 2)
                        {
                            var p = rv.WallSlots[i]; var n = rv.WallSlotN[i];
                            if (!WallFree(rv, p, n, 0.4f, fy + 2.1f)) continue;
                            var c = new Vector3(p.x, fy + 2.5f, p.z) + n * 0.05f;
                            mb.Set(S.WoodDark, Color.white); mb.Push(Matrix4x4.TRS(c, Quaternion.LookRotation(n), Vector3.one)); mb.Lathe(new[] { new Vector2(0.15f, 0), new Vector2(0.001f, 0.04f) }, 12); mb.Pop();
                            mb.Set(S.Bone, new Color(0.85f, 0.8f, 0.7f));
                            foreach (float s in new[] { -1f, 1f })
                            {
                                Vector3 side = Vector3.Cross(Vector3.up, n) * s;
                                var a0 = c + n * 0.06f; var a1 = a0 + side * 0.2f + Vector3.up * 0.15f; var a2 = a1 + side * 0.1f + Vector3.up * 0.25f;
                                mb.Tube(new List<Vector3> { a0, a1, a2 }, 0.018f, 5);
                                mb.Tube(new List<Vector3> { a1, a1 + Vector3.up * 0.15f - side * 0.05f }, 0.012f, 4);
                            }
                        }
                        break;
                    }
                case RoomType.Chapel:
                    {
                        if (!r.Furniture.Any(fid => Layout.Furniture[fid].Type == "Pew"))
                        {
                            // narrow chapel (no pews fit): a velvet aisle to the altar lined with floor candles
                            bool wideC = R.W >= R.D;
                            var aisle = wideC ? new RectF(R.x0 + 1.6f, R.CZ - 0.45f, R.x1 - 0.3f, R.CZ + 0.45f) : new RectF(R.CX - 0.45f, R.z0 + 1.6f, R.CX + 0.45f, R.z1 - 0.3f);
                            mb.Set(S.Velvet, pal.Carpet);
                            mb.Box(new Vector3(aisle.CX, fy + 0.006f, aisle.CZ), new Vector3(aisle.W, 0.012f, aisle.D), MeshBuilder.Faces.PY | MeshBuilder.Faces.Sides);
                            var cfl = new List<Vector3>();
                            for (int k = 0; k < 10; k++)
                            {
                                float t = (k + 0.5f) / 10f;
                                foreach (float s in new[] { -1f, 1f })
                                {
                                    var cp = wideC ? new Vector3(Mathf.Lerp(R.x0 + 1.8f, R.x1 - 0.5f, t), fy, R.CZ + s * 0.75f) : new Vector3(R.CX + s * 0.75f, fy, Mathf.Lerp(R.z0 + 1.8f, R.z1 - 0.5f, t));
                                    if (!FloorFree(rv, cp.x, cp.z, 0.08f)) continue;
                                    float ch = 0.25f + (k % 3) * 0.12f;
                                    FurnitureFactory.Candle(mb, cp, ch, 0.03f, -2, cp + Vector3.up * ch, cfl);
                                }
                            }
                            foreach (var fp in cfl) FlameQuad(mb, fp, 0.07f, -2);
                            if (cfl.Count > 0) AddLight(rv, new Vector3(aisle.CX, fy + 0.6f, aisle.CZ), pal.Warm, 2.2f, 5f, LightType.Point, false, 0.5f, fire: true);
                        }
                        // hanging censers + memorial candles on the side walls
                        for (int i = 0; i < 3; i++)
                        {
                            var p = new Vector3(R.CX + (i - 1) * R.W * 0.25f, ceil - 1.2f, R.CZ);
                            mb.Set(S.Brass, Color.white); mb.Rod(p, new Vector3(p.x, ceil, p.z), 0.006f, 4, false);
                            mb.Sphere(p, 0.08f, 10, 6);
                            mb.Set(S.Glow, new Color(1f, 0.3f, 0.1f), MansionMats.GlowData(1.2f, 0.6f, 0, -2)); mb.Sphere(p + Vector3.down * 0.02f, 0.05f, 6, 4);
                        }
                        break;
                    }
                case RoomType.Bedroom: OwnerInterior(rv, mb); break;   // MansionView.Owners
                case RoomType.GuestRoom:
                    {
                        // dust sheets over the furniture: ghostly shapes
                        foreach (int fid in r.Furniture)
                        {
                            var f = Layout.Furniture[fid]; if (f.Type != "Wardrobe" && f.Type != "Bed") continue;
                            NavGrid.GetFootprint(f, 0.03f, out var fr);
                            float h = Mathf.Max(0.8f, f.Type == "Bed" ? 1.0f : 2.15f);
                            mb.Set(S.Linen, new Color(0.9f, 0.9f, 0.88f));
                            mb.BevelBox(new Vector3(fr.CX, fy + h / 2, fr.CZ), new Vector3(fr.W, h, fr.D), 0.12f);
                        }
                        break;
                    }
                case RoomType.RainCorridor: RainGimmick(rv, mb, tr); break;
                case RoomType.EmptyAuditorium: AuditoriumGimmick(rv, mb); break;
                case RoomType.WaitingRoom: WaitingGimmick(rv, mb); break;
                case RoomType.WhiteDoors: WhiteDoorsGimmick(rv, mb); break;
                case RoomType.MirrorWater: MirrorGimmick(rv, mb); break;
                case RoomType.ClockMuseum: ClockGimmick(rv, mb); break;
                case RoomType.Elevator: ElevatorCar(rv, mb); break;
                case RoomType.Pool:
                    {
                        // lifebuoys on the walls, wet footprints and puddles
                        for (int i = 0; i < rv.WallSlots.Count; i += 3)
                        {
                            var p = rv.WallSlots[i]; var n = rv.WallSlotN[i];
                            if (!WallFree(rv, p, n, 0.4f, fy + 1.4f)) continue;
                            mb.Set(S.GlossPaint, i % 2 == 0 ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.9f, 0.1f, 0.15f));
                            mb.Push(Matrix4x4.TRS(new Vector3(p.x, fy + 1.9f, p.z) + n * 0.08f, Quaternion.LookRotation(Vector3.up, n), Vector3.one)); mb.Torus(Vector3.zero, 0.25f, 0.07f, 16, 8); mb.Pop();
                        }
                        for (int k = 0; k < 6; k++)
                        {
                            var p = new Vector3(R.x0 + 0.8f + (float)rnd.NextDouble() * (R.W - 1.6f), fy + 0.004f, R.z0 + 0.5f + (float)rnd.NextDouble() * 1.4f);
                            var rect = ProcTex.DecalRect(ProcTex.Decal.Water);
                            tr.Set(S.Decal, new Color(0.3f, 0.35f, 0.4f, 0.55f), new Vector4(0.95f, 0, 0.6f, 0));
                            float s = 0.4f + (float)rnd.NextDouble() * 0.6f;
                            tr.QuadUV(p + new Vector3(-s, 0, -s), p + new Vector3(-s, 0, s), p + new Vector3(s, 0, s), p + new Vector3(s, 0, -s), new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin));
                        }
                        break;
                    }
            }
        }

        static void Corkboard(MeshBuilder mb, Vector3 wp, Quaternion q, MansionPalette pal, System.Random rnd, bool pamphlets, bool strings = false)
        {
            float y = wp.y - 1.7f;
            mb.Push(Matrix4x4.TRS(new Vector3(wp.x, y + 1.65f, wp.z), q, Vector3.one));
            mb.Set(S.WoodLight, new Color(0.7f, 0.55f, 0.35f)); mb.Box(new Vector3(0, 0, 0.015f), new Vector3(1.0f, 0.7f, 0.03f));
            var pins = new List<Vector3>();
            for (int i = 0; i < 9; i++)
            {
                var p = new Vector3(-0.38f + (i % 3) * 0.38f + ((float)rnd.NextDouble() - 0.5f) * 0.08f, 0.22f - (i / 3) * 0.22f, 0.032f);
                mb.Set(S.Paper, pamphlets ? Color.HSVToRGB((float)rnd.NextDouble(), 0.5f, 0.9f) : new Color(0.95f, 0.93f, 0.85f));
                mb.Box(p, new Vector3(0.16f, 0.12f, 0.002f));
                mb.Set(S.GlossPaint, new Color(0.9f, 0.1f, 0.1f)); mb.Sphere(p + new Vector3(0, 0.05f, 0.005f), 0.008f, 5, 3);
                pins.Add(p + new Vector3(0, 0.05f, 0.008f));
            }
            if (strings)
            {
                mb.Set(S.GlossPaint, new Color(0.85f, 0.02f, 0.05f));
                for (int i = 0; i < 7; i++) mb.Rod(pins[rnd.Next(pins.Count)], pins[rnd.Next(pins.Count)], 0.002f, 3, false);
            }
            mb.Pop();
        }

        static void BassGuitar(MeshBuilder mb, Vector3 c, Quaternion q, MansionPalette pal)
        {
            mb.Push(Matrix4x4.TRS(c, q * Quaternion.Euler(0, 0, 12), Vector3.one));
            mb.Set(S.GlossPaint, pal.Neon * 0.8f);
            mb.Push(new Vector3(0, -0.35f, 0.04f), Quaternion.Euler(90, 0, 0), new Vector3(1, 1, 1.25f)); mb.Cyl(new Vector3(0, -0.02f, 0), 0.18f, 0.045f, 18); mb.Pop();
            mb.Push(new Vector3(0, -0.12f, 0.04f), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Cyl(new Vector3(0, -0.02f, 0), 0.13f, 0.045f, 16); mb.Pop();
            mb.Set(S.WoodDark, Color.white); mb.Box(new Vector3(0, 0.35f, 0.04f), new Vector3(0.05f, 0.8f, 0.025f));
            mb.Box(new Vector3(0, 0.8f, 0.04f), new Vector3(0.09f, 0.16f, 0.02f));
            mb.Set(S.Chrome, Color.white); for (int i = 0; i < 4; i++) mb.Rod(new Vector3(-0.012f + i * 0.008f, -0.5f, 0.07f), new Vector3(-0.012f + i * 0.008f, 0.75f, 0.058f), 0.0012f, 3, false);
            mb.Set(S.Obsidian, Color.white); mb.Box(new Vector3(0, -0.3f, 0.068f), new Vector3(0.12f, 0.03f, 0.01f));
            mb.Pop();
        }

        // ------------------------------------------------------------------ mystery gimmicks
        void RainGimmick(RoomView rv, MeshBuilder mb, MeshBuilder tr)
        {
            var r = rv.Room; var R = r.Rect; int g = r.MysteryGimmick;
            // particle rain inside the room volume
            var go = new GameObject("Rain"); go.transform.SetParent(rv.Root, false);
            go.transform.position = new Vector3(R.CX, g == 1 ? rv.FloorY + 0.05f : rv.CeilY - 0.05f, R.CZ);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.startLifetime = 1.1f; main.startSpeed = 0f; main.startSize = 0.03f; main.maxParticles = 3000;
            main.startColor = g == 2 ? new Color(1f, 0.35f, 0.4f, 0.55f) : new Color(0.7f, 0.85f, 1f, 0.5f);
            main.gravityModifier = g == 1 ? -0.55f : 0.55f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = Mathf.Clamp(R.Area * 22f, 400, 2500);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(R.W - 0.3f, 0.05f, R.D - 0.3f);
            var col = ps.collision; col.enabled = false;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch; rend.velocityScale = 0.08f; rend.lengthScale = 2f;
            rend.sharedMaterial = MansionMats.Particle("Rain", MansionMats.Proc("RainStreak"), true);
            ps.Simulate(1.2f, true, true); ps.Play();
            rv.Renderers.Add(rend);
            // puddles + ripples on the floor, dripping ceiling stains
            var rnd = rv.Rng;
            for (int k = 0; k < 14; k++)
            {
                var p = new Vector3(R.x0 + 0.4f + (float)rnd.NextDouble() * (R.W - 0.8f), rv.FloorY + 0.004f, R.z0 + 0.4f + (float)rnd.NextDouble() * (R.D - 0.8f));
                var rect = ProcTex.DecalRect(ProcTex.Decal.Water);
                float s = 0.5f + (float)rnd.NextDouble() * 0.9f;
                tr.Set(S.Decal, g == 2 ? new Color(0.35f, 0.05f, 0.08f, 0.7f) : new Color(0.15f, 0.2f, 0.28f, 0.7f), new Vector4(0.98f, 0, 0.4f, 0));
                tr.QuadUV(p + new Vector3(-s, 0, -s), p + new Vector3(-s, 0, s), p + new Vector3(s, 0, s), p + new Vector3(s, 0, -s), new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin));
            }
            // storm light: a cold flickering key
            AddLight(rv, new Vector3(R.CX, rv.CeilY - 0.3f, R.CZ), new Color(0.6f, 0.75f, 1f), 3.5f, Mathf.Max(R.W, R.D) * 0.7f, LightType.Point, false, 0.9f, moon: true);
            // umbrella stand with a single black umbrella, open, upside down (gimmick 1)
            foreach (var c in FreeCorners(rv, 0.25f))
            {
                mb.Set(S.Obsidian, Color.white);
                mb.Push(c + Vector3.up * (g == 1 ? 0.05f : 0.9f), g == 1 ? Quaternion.Euler(180, 0, 0) : Quaternion.identity, Vector3.one);
                mb.Lathe(new[] { new Vector2(0.001f, 0.25f), new Vector2(0.45f, 0f), new Vector2(0.47f, -0.03f) }, 8);
                mb.Rod(Vector3.up * 0.25f, Vector3.down * 0.8f, 0.01f, 4, false);
                mb.Pop();
                break;
            }
        }

        void AuditoriumGimmick(RoomView rv, MeshBuilder mb)
        {
            var r = rv.Room; var R = r.Rect; int g = r.MysteryGimmick; var pal = rv.Pal;
            bool wide = R.W >= R.D;
            // the screen faces the seats: the seats face the "speaker" wall (side 2 if wide, else 0)
            Vector3 wallPt = wide ? new Vector3(R.x0 + 0.12f, 0, R.CZ) : new Vector3(R.CX, 0, R.z0 + 0.12f);
            Vector3 n = wide ? Vector3.right : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, n);
            float sw = Mathf.Min(wide ? R.D : R.W, 6f) - 1.2f, sh = Mathf.Min(rv.CeilY - rv.FloorY - 1.6f, sw * 0.56f);
            var c = wallPt + Vector3.up * (rv.FloorY + 1.2f + sh * 0.5f) + n * 0.05f;
            mb.Set(S.Obsidian, Color.white);
            mb.Box(c - n * 0.02f, new Vector3(Mathf.Abs(side.x) * (sw + 0.3f) + Mathf.Abs(n.x) * 0.05f, sh + 0.3f, Mathf.Abs(side.z) * (sw + 0.3f) + Mathf.Abs(n.z) * 0.05f));
            mb.Set(S.Screen, Color.white, new Vector4(g, r.Id, 1, r.Circuit + 10));
            var o = c - side * (sw * 0.5f) - Vector3.up * (sh * 0.5f) + n * 0.01f;
            mb.QuadUV(o, o + Vector3.up * sh, o + Vector3.up * sh + side * sw, o + side * sw, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            // ON AIR sign above
            mb.Set(S.Screen, Color.white, new Vector4(5, 0, 1, r.Circuit + 10));
            var oa = c + Vector3.up * (sh * 0.5f + 0.35f) - side * 0.45f + n * 0.03f;
            mb.QuadUV(oa, oa + Vector3.up * 0.25f, oa + Vector3.up * 0.25f + side * 0.9f, oa + side * 0.9f, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            // screen glow washing the empty seats
            AddLight(rv, c + n * 1.2f, g == 2 ? new Color(0.9f, 0.9f, 1f) : new Color(0.55f, 0.7f, 1f), 6f, Mathf.Max(R.W, R.D) * 0.9f, LightType.Spot, true, 0.5f, false, false, false, Quaternion.LookRotation(n + Vector3.down * 0.25f), 100f);
            // a single microphone stand in the aisle
            var mp = new Vector3(R.CX, rv.FloorY, R.CZ);
            if (FloorFree(rv, mp.x, mp.z, 0.2f)) { mb.Set(S.Chrome, Color.white); mb.Rod(mp, mp + Vector3.up * 1.5f, 0.012f, 6, false); mb.Disc(mp + Vector3.up * 0.01f, 0.18f, 12, true); mb.Set(S.Obsidian, Color.white); mb.Sphere(mp + Vector3.up * 1.55f, 0.04f, 8, 6, 1.3f); }
        }

        void WaitingGimmick(RoomView rv, MeshBuilder mb)
        {
            var r = rv.Room; var R = r.Rect; var pal = rv.Pal; int g = r.MysteryGimmick;
            // departure board on the best free wall
            int best = -1; float bestLen = 0;
            for (int i = 0; i < rv.WallSlots.Count; i++) { var rg = rv.WallSlotRange[i]; float len = rg.y - rg.x; if (len > bestLen && WallFree(rv, rv.WallSlots[i], rv.WallSlotN[i], 1.0f, rv.FloorY + 2.2f)) { bestLen = len; best = i; } }
            if (best >= 0)
            {
                var p = rv.WallSlots[best]; var n = rv.WallSlotN[best]; var side = Vector3.Cross(Vector3.up, n);
                float bw = Mathf.Min(2.4f, bestLen - 0.6f), bh = 1.1f;
                var c = new Vector3(p.x, rv.FloorY + 2.75f, p.z) + n * 0.12f;
                mb.Set(S.Obsidian, Color.white); mb.Box(c - n * 0.06f, new Vector3(Mathf.Abs(side.x) * (bw + 0.2f) + Mathf.Abs(n.x) * 0.12f, bh + 0.2f, Mathf.Abs(side.z) * (bw + 0.2f) + Mathf.Abs(n.z) * 0.12f));
                mb.Set(S.Screen, Color.white, new Vector4(3, r.Id + g * 13, 1, r.Circuit + 10));
                var o = c + side * (bw * 0.5f) - Vector3.up * (bh * 0.5f) + n * 0.005f;
                mb.QuadUV(o, o + Vector3.up * bh, o + Vector3.up * bh - side * bw, o - side * bw, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
                AddLight(rv, c + n * 0.8f, new Color(1f, 0.7f, 0.3f), 1.8f, 4f, LightType.Point, false, 0.2f);
            }
            // a row of clocks, all wrong (gimmick 1: all run backwards)
            int placed = 0;
            for (int i = 0; i < rv.WallSlots.Count && placed < 7; i++)
            {
                if (i == best) continue;
                var p = rv.WallSlots[i]; var n = rv.WallSlotN[i];
                if (!WallFree(rv, p, n, 0.25f, rv.FloorY + 2.2f)) continue;
                mb.Push(Matrix4x4.TRS(new Vector3(p.x, rv.FloorY + 2.5f, p.z) + n * 0.01f, Quaternion.LookRotation(n), Vector3.one));
                WallClock(mb, Vector3.zero, 0.16f, pal, i * 3 + r.Id, g == 1 ? -1 : (i % 3 == 0 ? 0 : 1 + i));
                mb.Pop();
                placed++;
            }
            // yellow platform line on the floor + suitcases
            var rnd = rv.Rng;
            bool wide = R.W >= R.D;
            mb.Set(S.GlossPaint, new Color(0.95f, 0.8f, 0.1f));
            if (wide) mb.Box(new Vector3(R.CX, rv.FloorY + 0.003f, R.z0 + 0.7f), new Vector3(R.W - 0.6f, 0.006f, 0.12f), MeshBuilder.Faces.PY);
            else mb.Box(new Vector3(R.x0 + 0.7f, rv.FloorY + 0.003f, R.CZ), new Vector3(0.12f, 0.006f, R.D - 0.6f), MeshBuilder.Faces.PY);
            var suit = Models.Get("vintage_suitcase");
            foreach (var cc in FreeCorners(rv, 0.35f)) { if (suit != null) { var s = Models.Place(suit, rv.Root, cc, rnd.Next(4) * 90f, new Vector3(0.7f, 0, 0.3f), null); foreach (var rr in s.GetComponentsInChildren<Renderer>()) rv.Renderers.Add(rr); } break; }
        }

        void WhiteDoorsGimmick(RoomView rv, MeshBuilder mb)
        {
            var r = rv.Room; int g = r.MysteryGimmick; var rnd = rv.Rng;
            var white = new Color(1.12f, 1.12f, 1.12f);
            // doors set into the walls at impossible heights
            for (int i = 0; i < rv.WallSlots.Count; i++)
            {
                if (rnd.NextDouble() < 0.4) continue;
                var p = rv.WallSlots[i]; var n = rv.WallSlotN[i];
                float lift = 0.4f + (float)rnd.NextDouble() * (rv.CeilY - rv.FloorY - 2.2f);
                float scale = g == 2 ? 0.4f + (float)rnd.NextDouble() * 0.9f : 0.75f;
                float dw = 0.9f * scale, dh = 2.1f * scale;
                if (lift + dh > rv.CeilY - rv.FloorY - 0.3f) lift = Mathf.Max(0.2f, rv.CeilY - rv.FloorY - 0.35f - dh);
                if (!WallFree(rv, p, n, dw * 0.5f + 0.1f, rv.FloorY + lift)) continue;
                mb.Push(Matrix4x4.TRS(new Vector3(p.x, rv.FloorY + lift, p.z), Quaternion.LookRotation(n), Vector3.one));
                mb.Set(S.PlasterWhite, white);
                mb.Box(new Vector3(-dw / 2 - 0.05f, dh / 2, 0.03f), new Vector3(0.1f, dh + 0.05f, 0.06f));
                mb.Box(new Vector3(dw / 2 + 0.05f, dh / 2, 0.03f), new Vector3(0.1f, dh + 0.05f, 0.06f));
                mb.Box(new Vector3(0, dh + 0.05f, 0.03f), new Vector3(dw + 0.2f, 0.1f, 0.06f));
                mb.Box(new Vector3(0, dh / 2, 0.015f), new Vector3(dw, dh, 0.03f));
                mb.Set(S.Chrome, Color.white); mb.Sphere(new Vector3(dw / 2 - 0.1f, dh * 0.47f, 0.05f), 0.03f, 8, 5);
                // a keyhole that glows
                mb.Set(S.Glow, new Color(1f, 0.1f, 0.3f), MansionMats.GlowData(1.5f, 0.3f, 0, -1)); mb.Box(new Vector3(dw / 2 - 0.1f, dh * 0.42f, 0.035f), new Vector3(0.012f, 0.03f, 0.004f));
                mb.Pop();
            }
            // gimmick 1: doors on the ceiling
            if (g == 1)
            {
                var R = r.Rect;
                for (int k = 0; k < 4; k++)
                {
                    var c = new Vector3(R.x0 + (k + 0.5f) * R.W / 4, rv.CeilY - 0.02f, R.CZ + (k % 2 - 0.5f) * R.D * 0.3f);
                    mb.Set(S.PlasterWhite, white);
                    mb.Box(c, new Vector3(0.9f, 0.04f, 2.1f));
                    mb.Set(S.Chrome, Color.white); mb.Sphere(c + new Vector3(0.35f, -0.04f, 0), 0.03f, 8, 5);
                }
            }
        }

        void MirrorGimmick(RoomView rv, MeshBuilder mb)
        {
            var r = rv.Room; var R = r.Rect; int g = r.MysteryGimmick; var pal = rv.Pal;
            // gimmick 1: an upside-down dining set bolted to the ceiling, mirrored in the water below
            if (g == 1)
            {
                var c = new Vector3(R.CX, rv.CeilY, R.CZ);
                mb.Push(Matrix4x4.TRS(c, Quaternion.Euler(180, 0, 0), Vector3.one));
                mb.Set(S.WoodCherry, Color.white);
                mb.Box(new Vector3(0, 0.76f, 0), new Vector3(2.2f, 0.05f, 1.0f));
                foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Box(new Vector3(x * 1.0f, 0.37f, z * 0.4f), new Vector3(0.06f, 0.74f, 0.06f));
                mb.Set(S.Linen, Color.white); mb.Box(new Vector3(0, 0.79f, 0), new Vector3(2.3f, 0.01f, 1.1f));
                for (int i = 0; i < 4; i++)
                {
                    float x = -0.75f + i * 0.5f;
                    foreach (float z in new[] { -0.8f, 0.8f })
                    {
                        mb.Set(S.WoodDark, Color.white); mb.Box(new Vector3(x, 0.45f, z), new Vector3(0.42f, 0.05f, 0.42f));
                        mb.Box(new Vector3(x, 0.8f, z + Mathf.Sign(z) * 0.19f), new Vector3(0.42f, 0.7f, 0.04f));
                        foreach (float lx in new[] { -0.18f, 0.18f }) foreach (float lz in new[] { -0.18f, 0.18f }) mb.Box(new Vector3(x + lx, 0.22f, z + lz), new Vector3(0.03f, 0.44f, 0.03f));
                    }
                }
                var fl = new List<Vector3>();
                FurnitureFactory.Candle(mb, new Vector3(0, 0.79f, 0), 0.2f, 0.02f, -2, new Vector3(0, 0.99f, 0), fl);
                mb.Pop();
            }
            // gimmick 2: the water glows the complementary colour (reflection "lies")
            if (g == 2) AddLight(rv, new Vector3(R.CX, rv.FloorY + 0.3f, R.CZ), pal.Neon, 3f, Mathf.Max(R.W, R.D) * 0.8f, neon: true);
            // floating paper lanterns above the water
            var rnd = rv.Rng;
            for (int i = 0; i < 6; i++)
            {
                var p = new Vector3(R.x0 + 1f + (float)rnd.NextDouble() * (R.W - 2f), rv.FloorY + 2.45f + (float)rnd.NextDouble() * 0.6f, R.z0 + 1f + (float)rnd.NextDouble() * (R.D - 2f));
                var lc = Color.Lerp(pal.Warm, pal.Neon, i % 2 * 0.35f);
                mb.Set(S.Glow, lc, MansionMats.GlowData(1.1f, 0.25f, 0, -2));
                mb.Push(p, 0); mb.Lathe(new[] { new Vector2(0.001f, -0.01f), new Vector2(0.07f, 0.02f), new Vector2(0.11f, 0.1f), new Vector2(0.1f, 0.18f), new Vector2(0.05f, 0.24f), new Vector2(0.001f, 0.25f) }, 16); mb.Pop();
                mb.Set(S.Halo, lc, MansionMats.GlowData(0.35f, 0.25f, 0, -2));
                HaloQuad(mb, p + Vector3.up * 0.12f, 0.8f);
            }
            AddLight(rv, new Vector3(R.CX, rv.FloorY + 2.5f, R.CZ), pal.Warm, 3.5f, Mathf.Max(R.W, R.D) * 0.8f, LightType.Point, false, 0.2f);
        }

        void ClockGimmick(RoomView rv, MeshBuilder mb)
        {
            var r = rv.Room; var R = r.Rect; int g = r.MysteryGimmick; var pal = rv.Pal; var rnd = rv.Rng;
            // dozens of wall clocks at different heights and times
            int idx = 0;
            for (int i = 0; i < rv.WallSlots.Count; i++)
            {
                var p = rv.WallSlots[i]; var n = rv.WallSlotN[i];
                for (int k = 0; k < 4; k++)
                {
                    float y = rv.FloorY + 1.2f + k * 0.55f + (float)rnd.NextDouble() * 0.2f;
                    if (y > rv.CeilY - 0.5f) break;
                    var side = Vector3.Cross(Vector3.up, n) * (((float)rnd.NextDouble() - 0.5f) * 0.8f);
                    if (!WallFree(rv, p + side, n, 0.25f, y - 0.2f)) continue;
                    float rr = 0.1f + (float)rnd.NextDouble() * 0.12f;
                    float speed = g == 1 ? (idx == 5 ? 1 : 0) : (idx % 4 == 0 ? -1 : idx % 4 == 1 ? 1 : idx % 4 == 2 ? 12 : 0.5f);
                    mb.Push(Matrix4x4.TRS(new Vector3(p.x, y, p.z) + side + n * 0.01f, Quaternion.LookRotation(n) * Quaternion.Euler(0, 0, g == 2 ? ((float)rnd.NextDouble() - 0.5f) * 40f : 0), g == 2 ? new Vector3(1f, 1.3f, 1f) : Vector3.one));
                    WallClock(mb, Vector3.zero, rr, pal, idx * 11 + r.Id, speed);
                    mb.Pop();
                    idx++;
                }
            }
            // a huge clock face on the floor, under glass (you walk over time)
            var c = new Vector3(R.CX, rv.FloorY + 0.006f, R.CZ);
            if (FloorFree(rv, c.x, c.z, 0.8f) || true)
            {
                mb.Set(S.Clock, Color.white, new Vector4(4, r.Id % 12, g == 1 ? 0 : 60, 9));
                float rr = Mathf.Min(R.W, R.D) * 0.3f;
                mb.QuadUV(c + new Vector3(rr, 0, -rr), c + new Vector3(-rr, 0, -rr), c + new Vector3(-rr, 0, rr), c + new Vector3(rr, 0, rr), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            }
        }

        void ElevatorCar(RoomView rv, MeshBuilder mb)
        {
            var R = rv.Room.Rect; var pal = rv.Pal;
            var c = new Vector3(R.CX, rv.FloorY, R.CZ);
            float s = Mathf.Min(R.W, R.D) - 1.0f;
            // brass cage car, open on the side facing the room door (you walk straight in)
            int open = -1;   // 0 -z, 1 +z, 2 -x, 3 +x
            if (rv.Room.Doors.Count > 0)
            {
                var d = Layout.Doors[rv.Room.Doors[0]];
                float dx = d.Pos.x - c.x, dz = d.Pos.z - c.z;
                open = Mathf.Abs(dx) > Mathf.Abs(dz) ? (dx < 0 ? 2 : 3) : (dz < 0 ? 0 : 1);
            }
            mb.Set(S.Brass, Color.white);
            var sides = new[] { (new Vector3(-s / 2, 0, -s / 2), new Vector3(s / 2, 0, -s / 2)), (new Vector3(-s / 2, 0, s / 2), new Vector3(s / 2, 0, s / 2)), (new Vector3(-s / 2, 0, -s / 2), new Vector3(-s / 2, 0, s / 2)), (new Vector3(s / 2, 0, -s / 2), new Vector3(s / 2, 0, s / 2)) };
            for (int sd = 0; sd < 4; sd++)
            {
                var (a, b) = sides[sd];
                for (int i = 0; i <= 8; i++)
                {
                    float t = i / 8f;
                    if (sd == open && t > 0.05f && t < 0.95f) continue;
                    var e = Vector3.Lerp(a, b, t);
                    mb.Rod(c + e, c + e + Vector3.up * 2.8f, 0.012f, 5, false);
                }
                if (sd != open) mb.Bar(c + a + Vector3.up * 1.1f, c + b + Vector3.up * 1.1f, 0.04f, 0.04f);   // hand rail
            }
            mb.Box(c + Vector3.up * 2.85f, new Vector3(s + 0.1f, 0.1f, s + 0.1f));
            mb.Set(S.Velvet, pal.Carpet); mb.Box(c + Vector3.up * 0.01f, new Vector3(s, 0.02f, s));
            // dial with an arrow pointing down, far below
            mb.Set(S.Clock, Color.white, new Vector4(4, 6.5f, 0, 9));
            mb.Push(Matrix4x4.TRS(c + new Vector3(0, 3.2f, 0), Quaternion.identity, Vector3.one));
            mb.FaceUV(new Vector3(0.3f, -0.3f, -s / 2 - 0.06f), new Vector3(-0.6f, 0, 0), new Vector3(0, 0.6f, 0), new Rect(1, 0, -1, 1));
            mb.Pop();
            // call panel glowing red: going down
            mb.Set(S.Glow, new Color(1f, 0.1f, 0.2f), MansionMats.GlowData(2.5f, 0.2f, 0, -1));
            mb.Box(c + new Vector3(s / 2 - 0.05f, 1.3f, 0), new Vector3(0.02f, 0.08f, 0.08f));
            AddLight(rv, c + Vector3.up * 2.5f, new Color(1f, 0.75f, 0.45f), 3.5f, 4f, LightType.Point, false, 0.1f);
        }
    }
}
