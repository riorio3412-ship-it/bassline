using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Containers you can open: wardrobes, china cabinets, vitrines, sideboards, nightstands, desks, file cabinets,
    /// medicine cabinets and chests. Each is a hollow carcass (static, batched with the room) plus separate moving parts —
    /// "Door_L" / "Door_R" hinged on the local -X / +X edge, "Lid" hinged along the back top edge, "Drawer_0".."Drawer_n"
    /// sliding out along local +Z — and "Slot_i" anchors where an item can be shown (on the shelves behind doors, inside
    /// drawers). The parts and slots are registered in an <see cref="OpenableParts"/> on the furniture root.
    /// </summary>
    internal static partial class FurnitureFactory
    {
        static readonly Color BrassAged = new Color(0.72f, 0.56f, 0.36f);

        sealed class Rig
        {
            public Ctx C; public OpenableParts Op; public int SlotN;
            public Rig(Ctx c) { C = c; Op = c.Go.GetComponent<OpenableParts>() ?? c.Go.AddComponent<OpenableParts>(); }

            public Transform Pivot(string name, Vector3 local)
            {
                var t = new GameObject(name).transform; t.SetParent(C.T, false); t.localPosition = local; return t;
            }
            public void Emit(Transform t, MeshBuilder m)
            {
                if (m.Empty) return;
                var mesh = m.ToMesh(C.F.Type + "_" + t.name, out var slots);
                t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = t.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterials = MansionMats.Materials(slots);
            }
            public Transform Slot(Transform parent, Vector3 local)
            {
                var s = new GameObject("Slot_" + SlotN++).transform; s.SetParent(parent, false); s.localPosition = local; return s;
            }
            /// <summary>Where the interior bounce light sits (local) and how far it reaches.</summary>
            public void Interior(Vector3 local, float range) { Op.InteriorCenter = local; Op.InteriorRange = range; }
            public OpenableParts.Part Add(string name, OpenableParts.PartKind kind, Transform pivot, Vector3 axis, float amount)
            {
                var p = new OpenableParts.Part { Name = name, Kind = kind, Pivot = pivot, Axis = axis, Amount = amount };
                Op.Parts.Add(p); return p;
            }
        }

        static bool DispatchOpenable(Ctx c, MeshBuilder mb)
        {
            switch (c.F.Type)
            {
                case "Wardrobe": OpenWardrobe(c, mb); return true;
                case "Cabinet": OpenChinaCabinet(c, mb); return true;
                case "DisplayCase": OpenVitrine(c, mb); return true;
                case "Sideboard": OpenSideboard(c, mb); return true;
                case "Nightstand": OpenNightstand(c, mb); Lamp(c, mb, new Vector3(0, c.H, 0), 0.22f); return true;
                case "FileCabinet": OpenFileCabinet(c, mb); return true;
                case "MedCabinet": OpenMedCabinet(c, mb); return true;
                case "Chest": OpenChest(c, mb); return true;
                case "CardCatalog": OpenCardCatalog(c, mb); return true;
                case "ColdLocker": OpenColdLocker(c, mb); return true;
                case "Fridge": OpenFridge(c, mb); return true;
                case "ButlerDesk": if (c.Rv.Room.Type == RoomType.Courtroom) return false; OpenButlerDesk(c, mb); return true;
            }
            return false;
        }

        /// <summary>Kitchen base cabinet under a counter top: a row of drawers over pairs of doors.</summary>
        internal static void OpenCounterBase(Ctx c, MeshBuilder mb, Color paint)
        {
            var rig = new Rig(c); float W = c.W, D = c.D, H = c.H;
            float t = 0.02f, th = 0.025f, y0 = 0.1f, top = H - 0.05f, dr = top - 0.17f;
            mb.Set(S.WoodPainted, paint); Carcass(mb, W - 0.02f, D - 0.04f, y0, top, th, t);
            mb.Set(S.WoodDark, Darker(c.WoodC, 0.3f)); mb.Box(new Vector3(0, y0 * 0.5f, 0), new Vector3(W - 0.1f, y0, D - 0.14f));
            mb.Set(S.WoodPainted, paint); mb.Box(new Vector3(0, dr, D * 0.5f - 0.02f - t * 0.5f), new Vector3(W - 0.06f, 0.02f, t));
            int n = Mathf.Max(1, Mathf.RoundToInt(W / 0.6f));
            DrawerRow(rig, n, -W * 0.5f + th, W * 0.5f - th, dr + 0.01f, top - th, D * 0.5f - 0.02f - t, D - 0.12f, Lighter(paint, 0.1f), false, false, false, S.WoodPainted);
            mb.Set(S.WoodLight, Lighter(paint, 0.15f)); float sh = y0 + (dr - y0) * 0.5f; mb.Box(new Vector3(0, sh, -0.03f), new Vector3(W - 0.08f, 0.016f, D - 0.14f));
            int pairs = Mathf.Max(1, Mathf.RoundToInt(W / 1.2f)); float pw = (W - 2 * th) / pairs;
            for (int k = 0; k < pairs; k++)
            {
                float x0 = -W * 0.5f + th + k * pw, x1 = x0 + pw, xc = (x0 + x1) * 0.5f;
                var slots = new List<(Vector3, int)> { (new Vector3(xc - pw * 0.25f, y0 + th + 0.004f, -0.03f), -1), (new Vector3(xc + pw * 0.25f, sh + 0.01f, -0.03f), 1) };
                DoorPair(rig, x0 + 0.004f, x1 - 0.004f, y0 + 0.006f, dr - 0.006f, D * 0.5f - 0.02f - t, t, Lighter(paint, 0.1f), 0, 0, slots, k == 0 ? "" : "_" + k, S.WoodPainted);
            }
            rig.Interior(new Vector3(0, (y0 + dr) * 0.5f, 0.2f), 1.3f);
        }

        /// <summary>Vanity pedestals: two stacks of two drawers under the dressing top.</summary>
        internal static void OpenVanityPedestals(Ctx c, MeshBuilder mb, Color paint)
        {
            var rig = new Rig(c); float W = c.W, D = c.D;
            foreach (float s in new[] { -1f, 1f })
            {
                float px = s * (W * 0.5f - 0.2f), pd = D - 0.05f;
                mb.Set(S.WoodPainted, paint); mb.Push(new Vector3(px, 0, 0), 0); Carcass(mb, 0.36f, pd, 0.02f, 0.72f, 0.018f, 0.02f); mb.Pop();
                DrawerRow(rig, 1, px - 0.162f, px + 0.162f, 0.38f, 0.7f, pd * 0.5f - 0.02f, pd - 0.08f, Lighter(paint, 0.05f), false, false, false, S.WoodPainted);
                DrawerRow(rig, 1, px - 0.162f, px + 0.162f, 0.04f, 0.37f, pd * 0.5f - 0.02f, pd - 0.08f, Lighter(paint, 0.05f), false, false, false, S.WoodPainted);
            }
            rig.Interior(new Vector3(0, 0.45f, D * 0.5f + 0.25f), 1.0f);
        }

        static void OpenCardCatalog(Ctx c, MeshBuilder mb)
        {
            var rig = new Rig(c); float W = c.W, D = c.D, H = c.H; var wood = c.WoodC * 0.9f;
            float th = 0.025f, y0 = 0.12f, top = H - 0.03f, t = 0.018f;
            mb.Set(S.WoodDark, wood); Carcass(mb, W, D, y0, top, th, t);
            mb.BevelBox(new Vector3(0, top + 0.015f, 0.005f), new Vector3(W + 0.03f, 0.03f, D + 0.02f), 0.008f);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) TurnedLeg(mb, new Vector3(x * (W * 0.5f - 0.04f), 0, z * (D * 0.5f - 0.04f)), y0, 0.022f);
            int rows = 4, cols = 4; float rh = (top - y0 - 2 * th) / rows;
            for (int j = rows - 1; j >= 0; j--) DrawerRow(rig, cols, -W * 0.5f + th, W * 0.5f - th, y0 + th + j * rh, y0 + th + (j + 1) * rh, D * 0.5f - t, D - t - 0.04f, Lighter(wood, 0.1f), false, true, false, S.WoodLight);
            rig.Interior(new Vector3(0, (y0 + top) * 0.5f, D * 0.5f + 0.3f), 1.0f);
        }

        /// <summary>Mortuary cold store: six steel compartments, each a tray that slides out on runners.</summary>
        static void OpenColdLocker(Ctx c, MeshBuilder mb)
        {
            var rig = new Rig(c); float W = c.W, D = c.D, H = c.H; var steel = new Color(0.75f, 0.8f, 0.84f);
            float th = 0.03f, y0 = 0.15f, top = H - 0.15f, t = 0.03f;
            mb.Set(S.Steel, steel); Carcass(mb, W, D, 0.02f, H, th, t);
            mb.Box(new Vector3(0, y0 * 0.5f, 0), new Vector3(W - 0.02f, y0, D - 0.02f));
            int cols = 2, rows = 3; float rh = (top - y0) / rows;
            for (int j = 1; j < rows; j++) mb.Box(new Vector3(0, y0 + j * rh, 0), new Vector3(W - 2 * th, 0.02f, D - t));
            mb.Box(new Vector3(0, (y0 + top) * 0.5f, 0), new Vector3(0.02f, top - y0, D - t));
            // frost along the seams, a cold pilot lamp
            mb.Set(S.PlasterWhite, new Color(0.9f, 0.95f, 1f)); for (int j = 0; j <= rows; j++) mb.Box(new Vector3(0, y0 + j * rh, D * 0.5f + 0.002f), new Vector3(W - 0.04f, 0.012f, 0.008f));
            mb.Set(S.Glow, new Color(0.4f, 0.8f, 1f), MansionMats.GlowData(1.2f, 0.2f, 0, c.Circuit)); mb.Box(new Vector3(W / 2 - 0.1f, H - 0.08f, D / 2 + 0.01f), new Vector3(0.05f, 0.03f, 0.01f));
            int ajar = c.Var % 2 == 0 ? 1 : -1;   // one compartment left open, a sheet over what lies on the tray
            for (int j = rows - 1; j >= 0; j--)
                for (int i = 0; i < cols; i++)
                {
                    float x0 = -W * 0.5f + th + i * (W - 2 * th) / cols, x1 = x0 + (W - 2 * th) / cols, ya = y0 + j * rh + 0.012f, yb = ya + rh - 0.024f;
                    string name = "Drawer_" + rig.Op.Parts.FindAll(p => p.Kind == OpenableParts.PartKind.Drawer).Count;
                    var pv = rig.Pivot(name, new Vector3((x0 + x1) * 0.5f, ya, D * 0.5f - t));
                    var m = new MeshBuilder(); float w = x1 - x0 - 0.01f, h = yb - ya, depth = D - t - 0.05f;
                    m.Set(S.Steel, Lighter(steel, 0.08f)); m.BevelBox(new Vector3(0, h * 0.5f, t * 0.5f), new Vector3(w, h, t), 0.008f);
                    m.Set(S.Chrome, Color.white); m.Box(new Vector3(0, h * 0.62f, t + 0.02f), new Vector3(0.22f, 0.025f, 0.03f));
                    m.Set(S.Paper, new Color(0.9f, 0.88f, 0.8f)); m.Box(new Vector3(-w * 0.3f, h * 0.35f, t + 0.002f), new Vector3(0.1f, 0.06f, 0.002f));
                    m.Set(S.Steel, Darker(steel, 0.1f)); m.Box(new Vector3(0, 0.03f, -depth * 0.5f), new Vector3(w - 0.06f, 0.02f, depth));   // the tray
                    bool body = j == 0 && i == 1 && ajar > 0;
                    if (body) { m.Set(S.Linen, new Color(0.85f, 0.85f, 0.82f)); m.BevelBox(new Vector3(0, 0.1f, -depth * 0.5f), new Vector3(w - 0.2f, 0.13f, depth - 0.1f), 0.05f); m.Sphere(new Vector3(0, 0.11f, -depth + 0.18f), 0.1f, 10, 7, 0.8f); }
                    rig.Emit(pv, m);
                    var p = rig.Add(name, OpenableParts.PartKind.Drawer, pv, Vector3.forward, depth * 0.85f);
                    p.Slots.Add(rig.Slot(pv, new Vector3(0, 0.045f, -depth * 0.5f)));
                    if (body) rig.Op.Snap(rig.Op.Count - 1, true);
                }
            rig.Interior(new Vector3(0, H * 0.5f, D * 0.5f + 0.5f), 1.6f);
        }

        static void OpenFridge(Ctx c, MeshBuilder mb)
        {
            var rig = new Rig(c); float W = c.W, D = c.D, H = c.H;
            // a Victorian oak ice-box: zinc-lined, brass latch and hinges (no white enamel block in a gothic kitchen)
            var oak = c.WoodC * 0.9f; float t = 0.06f;
            mb.Set(S.WoodCherry, oak); Carcass(mb, W, D, 0.06f, H - 0.02f, 0.04f, t);
            mb.BevelBox(new Vector3(0, H - 0.01f, 0.005f), new Vector3(W + 0.04f, 0.04f, D + 0.02f), 0.01f);
            mb.Set(S.Chrome, Color.white); foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Cyl(new Vector3(x * (W / 2 - 0.08f), 0, z * (D / 2 - 0.08f)), 0.03f, 0.06f, 8);
            // inside: white enamel, wire shelves
            mb.Set(S.Steel, new Color(0.6f, 0.62f, 0.62f)); mb.Box(new Vector3(0, H * 0.5f, -D * 0.5f + 0.045f), new Vector3(W - 0.09f, H - 0.14f, 0.005f));   // zinc lining
            var slots = new List<Vector3>();
            for (int s = 1; s <= 3; s++)
            {
                float y = 0.06f + s * (H - 0.1f) / 4.2f;
                mb.Set(S.Chrome, Color.white);
                for (int k = 0; k < 7; k++) mb.Rod(new Vector3(-W * 0.5f + 0.05f, y, -D * 0.5f + 0.06f + k * (D - t - 0.1f) / 6f), new Vector3(W * 0.5f - 0.05f, y, -D * 0.5f + 0.06f + k * (D - t - 0.1f) / 6f), 0.004f, 4, false);
                slots.Add(new Vector3(0, y + 0.005f, -0.03f));
            }
            // the door: one thick rounded slab, hinged on the left, a chrome lever on the right, a little eye badge
            var L = rig.Pivot("Door_L", new Vector3(-W * 0.5f, 0.07f, D * 0.5f - t * 0.5f));
            var m = new MeshBuilder(); float dh = H - 0.12f;
            DoorLeaf(m, W, dh, t, 1f, oak, 0, false);
            m.Set(S.Brass, BrassAged);
            m.Box(new Vector3(W - 0.08f, dh * 0.58f, t * 0.5f + 0.02f), new Vector3(0.05f, 0.16f, 0.03f));                 // latch
            foreach (float y in new[] { dh * 0.15f, dh * 0.85f }) m.Box(new Vector3(0.05f, y, t * 0.5f + 0.006f), new Vector3(0.1f, 0.06f, 0.012f));   // strap hinges
            m.Set(S.Steel, new Color(0.6f, 0.62f, 0.62f)); m.Box(new Vector3(W * 0.5f, dh * 0.5f, -t * 0.5f - 0.004f), new Vector3(W - 0.08f, dh - 0.08f, 0.008f));
            rig.Emit(L, m);
            var p = rig.Add("Door_L", OpenableParts.PartKind.Door, L, Vector3.up, -110f);
            foreach (var s in slots) p.Slots.Add(rig.Slot(c.T, s));
            rig.Interior(new Vector3(0, H * 0.55f, 0.1f), 1.3f);
            rig.Op.InteriorIntensity = 0.7f;
        }

        /// <summary>The butler's reception desk: solid and emblazoned at the front, worked from behind (two drawers, a shelf).</summary>
        static void OpenButlerDesk(Ctx c, MeshBuilder mb)
        {
            var rig = new Rig(c); float W = c.W, D = c.D, H = c.H;
            mb.Set(S.WoodCherry, c.WoodC);
            mb.Push(Vector3.zero, Quaternion.Euler(0, 180, 0), Vector3.one); Carcass(mb, W, D, 0.04f, H, 0.04f, 0.02f); mb.Pop();
            mb.Set(S.WoodCherry, Darker(c.WoodC, 0.1f)); mb.Box(new Vector3(0, 0.02f, 0), new Vector3(W, 0.04f, D));
            mb.Set(S.Gold, c.Pal.Trim);
            mb.Box(new Vector3(0, H + 0.01f, 0), new Vector3(W + 0.05f, 0.03f, D + 0.05f));
            // fish emblem in gold on the front (Yusti)
            mb.Ellipsoid(new Vector3(0, H * 0.55f, D / 2 + 0.01f), new Vector3(0.18f, 0.09f, 0.02f), 12, 6);
            mb.Push(new Vector3(0.2f, H * 0.55f, D / 2 + 0.01f), 0); mb.Box(Vector3.zero, new Vector3(0.1f, 0.12f, 0.02f)); mb.Pop();
            mb.Set(S.Glass, Color.white); mb.Cyl(new Vector3(-W * 0.3f, H + 0.02f, 0), 0.08f, 0.22f, 12);
            mb.Set(S.WoodCherry, c.WoodC); float sh = H * 0.42f; mb.Box(new Vector3(0, sh, 0), new Vector3(W - 0.08f, 0.02f, D - 0.06f));
            DrawerRow(rig, 2, -W * 0.5f + 0.04f, W * 0.5f - 0.04f, H - 0.26f, H - 0.04f, -D * 0.5f + 0.02f, D - 0.12f, c.WoodC, false, false, true);
            rig.Interior(new Vector3(0, H * 0.6f, -D * 0.5f - 0.3f), 1.2f);
        }

        // ------------------------------------------------------------------ parts
        /// <summary>A door leaf in its hinge frame: it runs from the hinge along dir·X (dir +1 for a door hinged on its
        /// left, -1 for one hinged on its right), front face toward +Z. Panelled, glazed or mirrored.</summary>
        static void DoorLeaf(MeshBuilder m, float w, float h, float t, float dir, Color wood, int face, bool knob = true, int mat = -1)
        {
            float cx = dir * w * 0.5f, s = Mathf.Min(0.065f, w * 0.18f); int wm = mat >= 0 ? mat : S.WoodCherry;
            m.Set(wm, wood);
            m.Box(new Vector3(dir * s * 0.5f, h * 0.5f, 0), new Vector3(s, h, t));
            m.Box(new Vector3(dir * (w - s * 0.5f), h * 0.5f, 0), new Vector3(s, h, t));
            m.Box(new Vector3(cx, s * 0.5f, 0), new Vector3(w - 2 * s, s, t));
            m.Box(new Vector3(cx, h - s * 0.5f, 0), new Vector3(w - 2 * s, s, t));
            float pw = w - 2 * s, ph = h - 2 * s;
            if (face == 1)
            {
                // glazed, with a thin glazing bar grid
                m.Set(S.Glass, Color.white); m.Box(new Vector3(cx, h * 0.5f, 0), new Vector3(pw + 0.01f, ph + 0.01f, 0.006f), MeshBuilder.Faces.PZ | MeshBuilder.Faces.NZ);
                m.Set(S.WoodCherry, wood);
                int bars = ph > 0.9f ? 2 : 1;
                for (int i = 1; i <= bars; i++) m.Box(new Vector3(cx, s + ph * i / (bars + 1f), t * 0.1f), new Vector3(pw, 0.016f, t * 0.5f));
                m.Box(new Vector3(cx, h * 0.5f, t * 0.1f), new Vector3(0.016f, ph, t * 0.5f));
            }
            else if (face == 2)
            {
                // an old mirror, spotted and dark
                m.Set(S.Mirror, new Color(0.62f, 0.62f, 0.6f)); m.Box(new Vector3(cx, h * 0.5f, t * 0.1f), new Vector3(pw + 0.01f, ph + 0.01f, 0.008f));
            }
            else
            {
                // raised field panel with a pointed (gothic) top
                m.Set(wm, Darker(wood, 0.1f)); m.Box(new Vector3(cx, h * 0.5f, -t * 0.12f), new Vector3(pw + 0.01f, ph + 0.01f, t * 0.55f));
                m.Set(wm, wood);
                float fh = ph - 0.07f, fw = pw - 0.07f;
                m.BevelBox(new Vector3(cx, s + 0.035f + fh * 0.44f, t * 0.12f), new Vector3(fw, fh * 0.88f, t * 0.45f), 0.012f);
                var a = new Vector3(cx - fw * 0.5f, s + 0.035f + fh * 0.88f, t * 0.35f); var b = new Vector3(cx + fw * 0.5f, a.y, a.z); var tip = new Vector3(cx, s + 0.035f + fh, a.z);
                m.TriAuto(a, b, tip, Vector3.forward);
            }
            if (!knob) return;
            m.Set(S.Brass, BrassAged);
            float kx = dir * (w - s * 0.5f);
            m.Box(new Vector3(kx, h * 0.5f, t * 0.5f + 0.002f), new Vector3(0.026f, 0.07f, 0.004f));      // escutcheon
            m.Set(S.Obsidian, Color.white); m.Box(new Vector3(kx, h * 0.5f - 0.012f, t * 0.5f + 0.0045f), new Vector3(0.006f, 0.018f, 0.001f));   // keyhole
            m.Set(S.Brass, BrassAged); m.Sphere(new Vector3(kx, h * 0.5f + 0.05f, t * 0.5f + 0.016f), 0.013f, 8, 6);
        }

        /// <summary>A drawer in its closed frame: front panel on z 0..t, box running back to -depth, pull on the front.</summary>
        static void DrawerBox(MeshBuilder m, float w, float h, float t, float depth, Color wood, bool metal = false, bool label = false, int mat = -1)
        {
            m.Set(mat >= 0 ? mat : metal ? S.PaintedMetal : S.WoodCherry, wood);
            m.BevelBox(new Vector3(0, h * 0.5f, t * 0.5f), new Vector3(w, h, t), 0.006f);
            // box (hollow): two sides, back, bottom
            m.Set(S.WoodLight, Lighter(wood, 0.25f));
            float bh = h * 0.78f, iw = w - 0.03f;
            m.Box(new Vector3(-iw * 0.5f + 0.006f, bh * 0.5f + 0.01f, -depth * 0.5f), new Vector3(0.012f, bh, depth));
            m.Box(new Vector3(iw * 0.5f - 0.006f, bh * 0.5f + 0.01f, -depth * 0.5f), new Vector3(0.012f, bh, depth));
            m.Box(new Vector3(0, bh * 0.5f + 0.01f, -depth + 0.006f), new Vector3(iw, bh, 0.012f));
            m.Box(new Vector3(0, 0.012f, -depth * 0.5f), new Vector3(iw, 0.008f, depth));
            m.Set(S.Brass, BrassAged);
            if (label)
            {
                m.Box(new Vector3(0, h * 0.62f, t + 0.003f), new Vector3(Mathf.Min(0.1f, w * 0.4f), 0.035f, 0.004f));
                m.Set(S.Paper, new Color(0.88f, 0.84f, 0.72f)); m.Box(new Vector3(0, h * 0.62f, t + 0.0055f), new Vector3(Mathf.Min(0.085f, w * 0.34f), 0.024f, 0.001f));
                m.Set(S.Brass, BrassAged); m.Box(new Vector3(0, h * 0.34f, t + 0.012f), new Vector3(Mathf.Min(0.09f, w * 0.35f), 0.012f, 0.02f));
            }
            else if (w > 0.45f) { foreach (float x in new[] { -w * 0.25f, w * 0.25f }) { m.Box(new Vector3(x, h * 0.5f, t + 0.003f), new Vector3(0.05f, 0.03f, 0.004f)); m.Rod(new Vector3(x - 0.03f, h * 0.5f - 0.012f, t + 0.012f), new Vector3(x + 0.03f, h * 0.5f - 0.012f, t + 0.012f), 0.005f, 5, true); } }
            else m.Sphere(new Vector3(0, h * 0.5f, t + 0.012f), 0.013f, 8, 6);
        }

        /// <summary>Hang a pair of doors over an opening x0..x1, y0..y1, front plane z; slots are shared out by side.</summary>
        static void DoorPair(Rig rig, float x0, float x1, float y0, float y1, float z, float t, Color wood, int faceL, int faceR, List<(Vector3 p, int side)> slots, string suffix = "", int mat = -1)
        {
            float w = (x1 - x0) * 0.5f - 0.003f, h = y1 - y0;
            var L = rig.Pivot("Door_L" + suffix, new Vector3(x0, y0, z + t * 0.5f)); var ml = new MeshBuilder(); DoorLeaf(ml, w, h, t, 1f, wood, faceL, true, mat); rig.Emit(L, ml);
            var R = rig.Pivot("Door_R" + suffix, new Vector3(x1, y0, z + t * 0.5f)); var mr = new MeshBuilder(); DoorLeaf(mr, w, h, t, -1f, wood, faceR, true, mat); rig.Emit(R, mr);
            var pl = rig.Add("Door_L" + suffix, OpenableParts.PartKind.Door, L, Vector3.up, -105f);
            var pr = rig.Add("Door_R" + suffix, OpenableParts.PartKind.Door, R, Vector3.up, 105f);
            foreach (var s in slots)
            {
                var st = rig.Slot(rig.C.T, s.p);
                if (s.side <= 0) pl.Slots.Add(st); if (s.side >= 0) pr.Slots.Add(st);
            }
        }

        /// <summary>A single door over an opening, hinged on the left (-X) edge.</summary>
        static void DoorSingle(Rig rig, float x0, float x1, float y0, float y1, float z, float t, Color wood, int face, List<Vector3> slots)
        {
            var L = rig.Pivot("Door_L", new Vector3(x0, y0, z + t * 0.5f)); var ml = new MeshBuilder(); DoorLeaf(ml, x1 - x0 - 0.004f, y1 - y0, t, 1f, wood, face); rig.Emit(L, ml);
            var p = rig.Add("Door_L", OpenableParts.PartKind.Door, L, Vector3.up, -105f);
            foreach (var s in slots) p.Slots.Add(rig.Slot(rig.C.T, s));
        }

        /// <summary>A row of drawers across x0..x1 at y0..y1, fronts on the plane z, sliding out 'pull' metres.</summary>
        static void DrawerRow(Rig rig, int n, float x0, float x1, float y0, float y1, float z, float depth, Color wood, bool metal = false, bool label = false, bool back = false, int mat = -1, float pull = -1f)
        {
            float w = (x1 - x0) / n;
            for (int i = 0; i < n; i++)
            {
                string name = "Drawer_" + rig.Op.Parts.FindAll(p => p.Kind == OpenableParts.PartKind.Drawer).Count;
                // a back-facing drawer (a desk worked from behind) is the same drawer turned round
                var t = rig.Pivot(name, new Vector3(x0 + w * (i + 0.5f), y0 + 0.004f, z)); if (back) t.localRotation = Quaternion.Euler(0, 180, 0);
                var m = new MeshBuilder(); DrawerBox(m, w - 0.008f, y1 - y0 - 0.008f, 0.022f, depth, wood, metal, label, mat); rig.Emit(t, m);
                var p = rig.Add(name, OpenableParts.PartKind.Drawer, t, back ? Vector3.back : Vector3.forward, pull > 0 ? pull : Mathf.Min(depth * 0.72f, 0.42f));
                p.Slots.Add(rig.Slot(t, new Vector3(0, 0.017f, -depth * 0.5f)));
            }
        }

        /// <summary>Hollow case: sides, back, bottom and top around an interior, front open (the parts close it).</summary>
        static void Carcass(MeshBuilder mb, float W, float D, float y0, float y1, float th, float frontInset)
        {
            float zf = D * 0.5f - frontInset, zb = -D * 0.5f;
            float d = zf - zb;
            mb.Box(new Vector3(-W * 0.5f + th * 0.5f, (y0 + y1) * 0.5f, (zf + zb) * 0.5f), new Vector3(th, y1 - y0, d));
            mb.Box(new Vector3(W * 0.5f - th * 0.5f, (y0 + y1) * 0.5f, (zf + zb) * 0.5f), new Vector3(th, y1 - y0, d));
            mb.Box(new Vector3(0, (y0 + y1) * 0.5f, zb + th * 0.25f), new Vector3(W - 2 * th, y1 - y0, th * 0.5f));
            mb.Box(new Vector3(0, y0 + th * 0.5f, (zf + zb) * 0.5f), new Vector3(W - 2 * th, th, d));
            mb.Box(new Vector3(0, y1 - th * 0.5f, (zf + zb) * 0.5f), new Vector3(W - 2 * th, th, d));
        }

        static void BunFeet(MeshBuilder mb, float W, float D, float r) { foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Sphere(new Vector3(x * (W * 0.5f - r * 1.2f), r * 0.8f, z * (D * 0.5f - r * 1.2f)), r, 10, 7, 0.8f); }

        static void Cornice(MeshBuilder mb, float W, float D, float y, float h, Color wood)
        {
            mb.Set(S.WoodCherry, wood);
            mb.BevelBox(new Vector3(0, y + h * 0.3f, 0.01f), new Vector3(W + 0.04f, h * 0.6f, D + 0.03f), 0.01f);
            mb.BevelBox(new Vector3(0, y + h * 0.8f, 0.015f), new Vector3(W + 0.08f, h * 0.4f, D + 0.05f), 0.012f);
            mb.Set(S.Gold, new Color(0.7f, 0.56f, 0.36f));
            mb.Box(new Vector3(0, y + h * 0.6f, D * 0.5f + 0.027f), new Vector3(W + 0.05f, 0.01f, 0.006f));
        }

        // ------------------------------------------------------------------ the pieces
        static void OpenWardrobe(Ctx c, MeshBuilder mb)
        {
            var rig = new Rig(c); float W = c.W, D = c.D, H = c.H; var wood = c.WoodC * 0.85f;
            float y0 = 0.14f, y1 = H - 0.17f, t = 0.028f, th = 0.03f;
            mb.Set(S.WoodCherry, wood);
            Carcass(mb, W, D, y0, y1, th, t);
            mb.Set(S.WoodCherry, Darker(wood, 0.15f)); mb.BevelBox(new Vector3(0, y0 * 0.5f + 0.02f, 0), new Vector3(W + 0.02f, y0 - 0.02f, D + 0.01f), 0.01f);
            BunFeet(mb, W, D, 0.035f);
            Cornice(mb, W, D, y1, H - y1, wood);
            // a pointed crest over the doors
            mb.Set(S.WoodCherry, wood);
            mb.Prism(new List<Vector2> { new Vector2(-0.22f, D * 0.5f + 0.02f), new Vector2(0.22f, D * 0.5f + 0.02f), new Vector2(0.22f, D * 0.5f), new Vector2(-0.22f, D * 0.5f) }, H, H + 0.02f);
            mb.TriAuto(new Vector3(-0.22f, H, D * 0.5f + 0.02f), new Vector3(0.22f, H, D * 0.5f + 0.02f), new Vector3(0, H + 0.2f, D * 0.5f + 0.02f), Vector3.forward);
            // inside: hat shelf, hanging rail, a dark coat left behind
            float zi = -t * 0.5f;
            mb.Set(S.WoodLight, Lighter(wood, 0.2f)); mb.Box(new Vector3(0, y1 - 0.3f, zi), new Vector3(W - 2 * th, 0.02f, D - t - 0.04f));
            mb.Set(S.Brass, BrassAged); mb.Rod(new Vector3(-W * 0.5f + th, y1 - 0.4f, zi), new Vector3(W * 0.5f - th, y1 - 0.4f, zi), 0.012f, 8, false);
            // three empty wooden hangers
            mb.Set(S.WoodLight, Lighter(wood, 0.3f));
            foreach (float hx in new[] { -0.3f, -0.18f, 0.25f }) { mb.Rod(new Vector3(hx, y1 - 0.4f, zi), new Vector3(hx, y1 - 0.45f, zi), 0.004f, 4, false); mb.Rod(new Vector3(hx - 0.2f, y1 - 0.52f, zi), new Vector3(hx, y1 - 0.45f, zi), 0.008f, 5, true); mb.Rod(new Vector3(hx + 0.2f, y1 - 0.52f, zi), new Vector3(hx, y1 - 0.45f, zi), 0.008f, 5, true); }
            var slots = new List<(Vector3, int)> { (new Vector3(-W * 0.22f, y0 + th + 0.005f, zi), -1), (new Vector3(W * 0.22f, y0 + th + 0.005f, zi), 1), (new Vector3(W * 0.22f, y1 - 0.29f, zi), 1) };
            DoorPair(rig, -W * 0.5f + th * 0.5f, W * 0.5f - th * 0.5f, y0 + 0.004f, y1 - 0.004f, D * 0.5f - t, t, wood, 0, c.Var % 2 == 0 ? 2 : 0, slots);
            rig.Interior(new Vector3(0, (y0 + y1) * 0.5f, 0.12f), 1.5f);
        }

        static void OpenChinaCabinet(Ctx c, MeshBuilder mb)
        {
            var rig = new Rig(c); float W = c.W, D = c.D, H = c.H; var wood = c.WoodC * 0.85f;
            float t = 0.025f, th = 0.03f;
            // lower chest of four drawers
            float lb = 0.12f, lt = 0.88f;
            mb.Set(S.WoodCherry, wood);
            Carcass(mb, W, D, lb, lt, th, t);
            mb.BevelBox(new Vector3(0, lt + 0.02f, 0.01f), new Vector3(W + 0.04f, 0.04f, D + 0.03f), 0.01f);
            BunFeet(mb, W, D, 0.04f);
            mb.Set(S.WoodCherry, wood); mb.Box(new Vector3(0, (lb + lt) * 0.5f, D * 0.5f - t * 0.5f), new Vector3(0.03f, lt - lb, t));   // centre muntin
            float mid = (lb + lt) * 0.5f;
            DrawerRow(rig, 1, -W * 0.5f + th, -0.015f, lb + th, mid, D * 0.5f - t, D - t - 0.05f, wood);
            DrawerRow(rig, 1, -W * 0.5f + th, -0.015f, mid, lt - th, D * 0.5f - t, D - t - 0.05f, wood);
            DrawerRow(rig, 1, 0.015f, W * 0.5f - th, lb + th, mid, D * 0.5f - t, D - t - 0.05f, wood);
            DrawerRow(rig, 1, 0.015f, W * 0.5f - th, mid, lt - th, D * 0.5f - t, D - t - 0.05f, wood);
            // upper glazed case, set back
            float ud = D * 0.72f, ub = lt + 0.04f, ut = H - 0.16f, uz = -(D - ud) * 0.5f;
            mb.Push(new Vector3(0, 0, uz), 0);
            mb.Set(S.WoodCherry, wood); Carcass(mb, W - 0.06f, ud, ub, ut, th, t);
            Cornice(mb, W - 0.06f, ud, ut, H - ut, wood);
            // glass sides
            mb.Set(S.Glass, Color.white);
            foreach (float x in new[] { -1f, 1f }) mb.Box(new Vector3(x * ((W - 0.06f) * 0.5f + 0.004f), (ub + ut) * 0.5f, 0), new Vector3(0.004f, ut - ub - 0.08f, ud - 0.1f));
            // shelves with a velvet lining
            float sh1 = ub + (ut - ub) * 0.36f, sh2 = ub + (ut - ub) * 0.68f;
            mb.Set(S.Velvet, Darker(c.Fabric, 0.3f));
            foreach (float y in new[] { ub + th + 0.002f, sh1, sh2 }) mb.Box(new Vector3(0, y, -t * 0.5f), new Vector3(W - 0.06f - 2 * th, 0.012f, ud - t - 0.02f));
            mb.Pop();
            var slots = new List<(Vector3, int)>();
            foreach (float y in new[] { ub + th + 0.01f, sh1 + 0.008f, sh2 + 0.008f }) { slots.Add((new Vector3(-W * 0.22f, y, uz - t * 0.5f), -1)); slots.Add((new Vector3(W * 0.22f, y, uz - t * 0.5f), 1)); }
            DoorPair(rig, -(W - 0.06f) * 0.5f + th * 0.5f, (W - 0.06f) * 0.5f - th * 0.5f, ub + 0.004f, ut - 0.004f, uz + ud * 0.5f - t, t, wood, 1, 1, slots);
            rig.Interior(new Vector3(0, (ub + ut) * 0.5f, uz + 0.1f), 1.6f);
            rig.Op.GlassFront = true;
        }

        static void OpenVitrine(Ctx c, MeshBuilder mb)
        {
            var rig = new Rig(c); float W = c.W, D = c.D, H = c.H; var wood = c.WoodC * 0.75f;
            float t = 0.022f, th = 0.035f, base0 = 0.6f;
            // base with two drawers
            mb.Set(S.WoodCherry, wood); Carcass(mb, W, D, 0.08f, base0, th, t);
            BunFeet(mb, W, D, 0.04f);
            DrawerRow(rig, 2, -W * 0.5f + th, W * 0.5f - th, 0.1f + th, base0 - th, D * 0.5f - t, D - t - 0.05f, wood);
            // glass box: gilt corner posts, glass sides and back, a cornice
            mb.Set(S.WoodCherry, wood); mb.Box(new Vector3(0, H - 0.04f, 0), new Vector3(W, 0.08f, D));
            mb.Set(S.Gold, new Color(0.7f, 0.56f, 0.36f));
            foreach (float x in new[] { -W / 2 + 0.02f, W / 2 - 0.02f }) foreach (float z in new[] { -D / 2 + 0.02f, D / 2 - 0.02f }) mb.Box(new Vector3(x, (H + base0) / 2, z), new Vector3(0.03f, H - base0, 0.03f));
            mb.Set(S.Glass, Color.white);
            foreach (float x in new[] { -1f, 1f }) mb.Box(new Vector3(x * (W * 0.5f - 0.02f), (H + base0) * 0.5f - 0.04f, 0), new Vector3(0.005f, H - base0 - 0.08f, D - 0.06f));
            mb.Box(new Vector3(0, (H + base0) * 0.5f - 0.04f, -D * 0.5f + 0.02f), new Vector3(W - 0.06f, H - base0 - 0.08f, 0.005f));
            // velvet shelves and the curiosities on them
            float s1 = base0 + 0.01f, s2 = base0 + (H - base0) * 0.5f;
            mb.Set(S.Velvet, c.Fabric); mb.Box(new Vector3(0, s1, 0), new Vector3(W - 0.08f, 0.01f, D - 0.08f)); mb.Box(new Vector3(0, s2, 0), new Vector3(W - 0.08f, 0.02f, D - 0.08f));
            var rnd = new System.Random(c.F.Id * 31);
            for (int i = 0; i < 4; i++)
            {
                float x = -W / 2 + 0.22f + i * (W - 0.44f) / 3, y = i % 2 == 0 ? s1 + 0.005f : s2 + 0.01f, z = -D * 0.18f;
                switch ((i + rnd.Next(3)) % 4)
                {
                    case 0: mb.Set(S.Glass, new Color(0.8f, 0.9f, 0.8f)); mb.Cyl(new Vector3(x, y, z), 0.06f, 0.16f, 10); mb.Set(S.Bone, Color.white); mb.Sphere(new Vector3(x, y + 0.05f, z), 0.03f, 6, 4); break;
                    case 1: mb.Set(S.Porcelain, new Color(0.96f, 0.92f, 0.9f)); mb.Ellipsoid(new Vector3(x, y + 0.04f, z), new Vector3(0.04f, 0.04f, 0.1f), 8, 6); break;
                    case 2: mb.Set(S.Porcelain, Color.white); mb.Sphere(new Vector3(x, y + 0.05f, z), 0.05f, 10, 8); mb.Set(S.GlossPaint, c.Pal.Accent2); mb.Disc(new Vector3(x, y + 0.05f, z + 0.05f), 0.02f, 8, true); break;
                    default: mb.Set(S.Gold, new Color(0.7f, 0.56f, 0.36f)); mb.Cyl(new Vector3(x, y, z), 0.04f, 0.2f, 8, true, 0.02f); break;
                }
            }
            var slots = new List<(Vector3, int)> { (new Vector3(-W * 0.2f, s1 + 0.006f, D * 0.12f), -1), (new Vector3(W * 0.2f, s1 + 0.006f, D * 0.12f), 1), (new Vector3(-W * 0.2f, s2 + 0.011f, D * 0.12f), -1), (new Vector3(W * 0.2f, s2 + 0.011f, D * 0.12f), 1) };
            DoorPair(rig, -W * 0.5f + 0.035f, W * 0.5f - 0.035f, base0 + 0.02f, H - 0.09f, D * 0.5f - t, t, new Color(0.62f, 0.5f, 0.32f), 1, 1, slots);
            rig.Interior(new Vector3(0, (base0 + H) * 0.5f, 0.1f), 1.3f);
            rig.Op.GlassFront = true;
        }

        static void OpenSideboard(Ctx c, MeshBuilder mb)
        {
            var rig = new Rig(c); float W = c.W, D = c.D, H = c.H; var wood = c.WoodC * 0.85f;
            float t = 0.024f, th = 0.03f, y0 = 0.16f, top = H - 0.045f;
            mb.Set(S.WoodCherry, wood); Carcass(mb, W, D - 0.02f, y0, top, th, t);
            mb.BevelBox(new Vector3(0, top + 0.022f, 0.005f), new Vector3(W + 0.06f, 0.045f, D + 0.02f), 0.014f);
            // plinth rail with short turned legs
            mb.Set(S.WoodCherry, Darker(wood, 0.1f)); mb.Box(new Vector3(0, y0 - 0.03f, 0), new Vector3(W - 0.04f, 0.06f, D - 0.04f));
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) TurnedLeg(mb, new Vector3(x * (W * 0.5f - 0.06f), 0, z * (D * 0.5f - 0.07f)), y0, 0.03f);
            float dr = top - 0.2f;
            mb.Set(S.WoodCherry, wood); mb.Box(new Vector3(0, dr, D * 0.5f - t * 0.5f - 0.01f), new Vector3(W - 2 * th, 0.02f, t));
            DrawerRow(rig, 3, -W * 0.5f + th, W * 0.5f - th, dr + 0.01f, top - th, D * 0.5f - t - 0.01f, D - t - 0.08f, wood);
            // inside the cupboard: one shelf
            mb.Set(S.WoodLight, Lighter(wood, 0.2f)); float sh = y0 + (dr - y0) * 0.5f; mb.Box(new Vector3(0, sh, -t * 0.5f), new Vector3(W - 2 * th, 0.018f, D - t - 0.06f));
            var slots = new List<(Vector3, int)> { (new Vector3(-W * 0.25f, y0 + th + 0.004f, -0.02f), -1), (new Vector3(W * 0.25f, y0 + th + 0.004f, -0.02f), 1), (new Vector3(-W * 0.25f, sh + 0.01f, -0.02f), -1), (new Vector3(W * 0.25f, sh + 0.01f, -0.02f), 1) };
            DoorPair(rig, -W * 0.5f + th * 0.5f, W * 0.5f - th * 0.5f, y0 + 0.004f, dr - 0.004f, D * 0.5f - t - 0.01f, t, wood, 0, 0, slots);
            rig.Interior(new Vector3(0, (y0 + top) * 0.5f, 0.15f), 1.4f);
        }

        static void OpenNightstand(Ctx c, MeshBuilder mb)
        {
            var rig = new Rig(c); float W = c.W, D = c.D, H = c.H; var wood = c.WoodC * 0.85f;
            float t = 0.02f, th = 0.022f, y0 = 0.1f, top = H - 0.03f;
            mb.Set(S.WoodCherry, wood); Carcass(mb, W, D, y0, top, th, t);
            mb.BevelBox(new Vector3(0, top + 0.015f, 0.005f), new Vector3(W + 0.03f, 0.03f, D + 0.02f), 0.008f);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) TurnedLeg(mb, new Vector3(x * (W * 0.5f - 0.03f), 0, z * (D * 0.5f - 0.03f)), y0, 0.018f);
            float dr = top - 0.12f;
            mb.Set(S.WoodCherry, wood); mb.Box(new Vector3(0, dr, D * 0.5f - t * 0.5f), new Vector3(W - 2 * th, 0.016f, t));
            DrawerRow(rig, 1, -W * 0.5f + th, W * 0.5f - th, dr + 0.008f, top - th, D * 0.5f - t, D - t - 0.04f, wood);
            DoorSingle(rig, -W * 0.5f + th * 0.5f, W * 0.5f - th * 0.5f, y0 + 0.004f, dr - 0.008f, D * 0.5f - t, t, wood, 0, new List<Vector3> { new Vector3(0, y0 + th + 0.004f, -0.01f) });
            rig.Interior(new Vector3(0, (y0 + top) * 0.5f, 0.12f), 0.7f);
        }

        static void OpenFileCabinet(Ctx c, MeshBuilder mb)
        {
            var rig = new Rig(c); float W = c.W, D = c.D, H = c.H;
            // an oak card-file cabinet: two banks of four labelled drawers
            var wood = c.WoodC * 0.8f; float t = 0.022f, th = 0.025f, y0 = 0.08f, top = H - 0.03f;
            mb.Set(S.WoodCherry, wood); Carcass(mb, W, D, y0, top, th, t);
            mb.BevelBox(new Vector3(0, top + 0.015f, 0.005f), new Vector3(W + 0.03f, 0.03f, D + 0.02f), 0.008f);
            mb.Set(S.WoodCherry, Darker(wood, 0.15f)); mb.Box(new Vector3(0, y0 * 0.5f, 0), new Vector3(W - 0.02f, y0, D - 0.02f));
            mb.Set(S.WoodCherry, wood); mb.Box(new Vector3(0, (y0 + top) * 0.5f, D * 0.5f - t * 0.5f), new Vector3(0.024f, top - y0, t));
            rig.Interior(new Vector3(0, (y0 + top) * 0.5f, D * 0.5f + 0.25f), 1.1f);
            int rows = 4; float rh = (top - y0 - 2 * th) / rows;
            for (int j = rows - 1; j >= 0; j--)
            {
                float a = y0 + th + j * rh;
                DrawerRow(rig, 1, -W * 0.5f + th, -0.012f, a, a + rh, D * 0.5f - t, D - t - 0.05f, wood, false, true);
                DrawerRow(rig, 1, 0.012f, W * 0.5f - th, a, a + rh, D * 0.5f - t, D - t - 0.05f, wood, false, true);
            }
        }

        static void OpenMedCabinet(Ctx c, MeshBuilder mb)
        {
            var rig = new Rig(c); float W = c.W, D = c.D, H = c.H; var enamel = new Color(0.86f, 0.87f, 0.84f);
            float t = 0.022f, th = 0.025f, split = 0.72f;
            mb.Set(S.PaintedMetal, enamel);
            Carcass(mb, W, D, 0.06f, H - 0.03f, th, t);
            mb.Box(new Vector3(0, split, D * 0.5f - t * 0.5f), new Vector3(W - 2 * th, 0.03f, t));
            mb.Set(S.PaintedMetal, new Color(0.2f, 0.2f, 0.2f)); mb.Box(new Vector3(0, 0.03f, 0), new Vector3(W - 0.04f, 0.06f, D - 0.04f));
            // glass shelves of bottles
            var rnd = c.Rng; var slots = new List<(Vector3, int)>();
            for (int s = 0; s < 3; s++)
            {
                float y = split + 0.03f + s * (H - split - 0.1f) / 3f;
                mb.Set(S.Glass, Color.white); mb.Box(new Vector3(0, y, -t * 0.5f), new Vector3(W - 2 * th - 0.01f, 0.01f, D - t - 0.03f));
                for (int k = 0; k < 5; k++)
                {
                    if (k == 2) { slots.Add((new Vector3(-0.05f, y + 0.006f, 0.02f), -1)); slots.Add((new Vector3(0.05f, y + 0.006f, 0.02f), 1)); continue; }   // room left for an item
                    var p = new Vector3(-W / 2 + 0.12f + k * (W - 0.24f) / 4, y + 0.006f, -0.06f);
                    Color bc = k % 3 == 0 ? new Color(0.42f, 0.25f, 0.12f) : k % 3 == 1 ? new Color(0.12f, 0.24f, 0.34f) : new Color(0.9f, 0.9f, 0.86f);
                    mb.Set(k % 3 == 2 ? S.Porcelain : S.Glass, bc);
                    float h = 0.08f + (float)rnd.NextDouble() * 0.1f;
                    mb.Push(p, 0); mb.Lathe(new[] { new Vector2(0.03f, 0), new Vector2(0.032f, h * 0.8f), new Vector2(0.012f, h), new Vector2(0.012f, h + 0.02f) }, 8); mb.Pop();
                }
            }
            // a faded red cross on the top rail
            mb.Set(S.GlossPaint, new Color(0.45f, 0.08f, 0.07f));
            mb.Box(new Vector3(0, H - 0.1f, D * 0.5f + 0.001f), new Vector3(0.12f, 0.035f, 0.004f)); mb.Box(new Vector3(0, H - 0.1f, D * 0.5f + 0.001f), new Vector3(0.035f, 0.12f, 0.004f));
            DoorPair(rig, -W * 0.5f + th * 0.5f, W * 0.5f - th * 0.5f, split + 0.02f, H - 0.16f, D * 0.5f - t, t, enamel, 1, 1, slots);
            DrawerRow(rig, 2, -W * 0.5f + th, W * 0.5f - th, 0.08f, split - 0.02f, D * 0.5f - t, D - t - 0.04f, enamel, true);
            rig.Interior(new Vector3(0, (split + H) * 0.5f, 0.1f), 1.3f);
            rig.Op.GlassFront = true;
        }

        static void OpenChest(Ctx c, MeshBuilder mb)
        {
            var rig = new Rig(c); float W = c.W, D = c.D, H = c.H; var wood = Darker(c.WoodC, 0.15f);
            float body = H * 0.7f, th = 0.025f;
            // hollow body on low skids
            mb.Set(S.WoodDark, wood);
            mb.Box(new Vector3(0, 0.02f, 0), new Vector3(W - 0.06f, 0.04f, D - 0.04f));
            Carcass(mb, W, D, 0.04f, body, th, 0f);
            mb.Box(new Vector3(0, (0.04f + body) * 0.5f, D * 0.5f - th * 0.5f), new Vector3(W - 2 * th, body - 0.04f, th));   // front board
            mb.Set(S.Velvet, new Color(0.25f, 0.06f, 0.07f)); mb.Box(new Vector3(0, 0.04f + th + 0.002f, 0), new Vector3(W - 2 * th - 0.01f, 0.004f, D - 2 * th - 0.01f));   // lining
            // iron bands and a lock plate on the body
            mb.Set(S.Iron, new Color(0.18f, 0.17f, 0.16f));
            foreach (float x in new[] { -W * 0.32f, W * 0.32f }) { mb.Box(new Vector3(x, body * 0.5f + 0.02f, D * 0.5f + 0.003f), new Vector3(0.04f, body - 0.02f, 0.006f)); mb.Box(new Vector3(x, body * 0.5f + 0.02f, -D * 0.5f - 0.003f), new Vector3(0.04f, body - 0.02f, 0.006f)); }
            mb.Set(S.Brass, BrassAged); mb.Box(new Vector3(0, body - 0.07f, D * 0.5f + 0.005f), new Vector3(0.08f, 0.1f, 0.008f));
            // the lid: a shallow barrel vault, hinged along the back top edge
            var lid = rig.Pivot("Lid", new Vector3(0, body, -D * 0.5f));
            var lm = new MeshBuilder(); float lift = H - body;
            lm.Set(S.WoodDark, wood);
            for (int i = 0; i < 10; i++)
            {
                float a0 = Mathf.Lerp(-1f, 1f, i / 10f), a1 = Mathf.Lerp(-1f, 1f, (i + 1) / 10f);
                var p0 = new Vector3(0, Mathf.Sqrt(Mathf.Max(0, 1 - a0 * a0)) * lift, D * 0.5f + a0 * D * 0.5f); var p1 = new Vector3(0, Mathf.Sqrt(Mathf.Max(0, 1 - a1 * a1)) * lift, D * 0.5f + a1 * D * 0.5f);
                lm.QuadAuto(p0 + Vector3.right * W * 0.5f, p1 + Vector3.right * W * 0.5f, p1 - Vector3.right * W * 0.5f, p0 - Vector3.right * W * 0.5f, (p0 + p1) * 0.5f - new Vector3(0, 0, D * 0.5f) + Vector3.up * 0.2f);
                lm.QuadAuto(p0 + Vector3.right * W * 0.5f, p1 + Vector3.right * W * 0.5f, p1 - Vector3.right * W * 0.5f, p0 - Vector3.right * W * 0.5f, -((p0 + p1) * 0.5f - new Vector3(0, 0, D * 0.5f) + Vector3.up * 0.2f));
            }
            foreach (float x in new[] { -1f, 1f })
            {
                // end caps (fans of the arc)
                for (int i = 0; i < 10; i++)
                {
                    float a0 = Mathf.Lerp(-1f, 1f, i / 10f), a1 = Mathf.Lerp(-1f, 1f, (i + 1) / 10f);
                    var q0 = new Vector3(x * W * 0.5f, 0, D * 0.5f); var q1 = new Vector3(x * W * 0.5f, Mathf.Sqrt(Mathf.Max(0, 1 - a0 * a0)) * lift, D * 0.5f + a0 * D * 0.5f); var q2 = new Vector3(x * W * 0.5f, Mathf.Sqrt(Mathf.Max(0, 1 - a1 * a1)) * lift, D * 0.5f + a1 * D * 0.5f);
                    lm.TriAuto(q0, q1, q2, new Vector3(x, 0, 0)); lm.TriAuto(q0, q2, q1, new Vector3(-x, 0, 0));
                }
            }
            lm.Set(S.Iron, new Color(0.18f, 0.17f, 0.16f));
            foreach (float x in new[] { -W * 0.32f, W * 0.32f })
                for (int i = 0; i < 10; i++)
                {
                    float a0 = Mathf.Lerp(-1f, 1f, i / 10f), a1 = Mathf.Lerp(-1f, 1f, (i + 1) / 10f);
                    var p0 = new Vector3(x, Mathf.Sqrt(Mathf.Max(0, 1 - a0 * a0)) * lift + 0.004f, D * 0.5f + a0 * D * 0.5f); var p1 = new Vector3(x, Mathf.Sqrt(Mathf.Max(0, 1 - a1 * a1)) * lift + 0.004f, D * 0.5f + a1 * D * 0.5f);
                    lm.Bar(p0, p1, 0.04f, 0.006f);
                }
            lm.Set(S.Brass, BrassAged); lm.Box(new Vector3(0, 0.02f, D + 0.006f), new Vector3(0.05f, 0.08f, 0.008f));   // hasp
            rig.Emit(lid, lm);
            var p = rig.Add("Lid", OpenableParts.PartKind.Lid, lid, Vector3.right, -100f);
            p.Slots.Add(rig.Slot(c.T, new Vector3(-W * 0.18f, 0.04f + th + 0.006f, 0)));
            p.Slots.Add(rig.Slot(c.T, new Vector3(W * 0.18f, 0.04f + th + 0.006f, 0)));
            rig.Interior(new Vector3(0, body + 0.15f, 0.1f), 0.9f);
        }
    }
}
