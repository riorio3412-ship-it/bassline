using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Whose room is this: the resident's threshold in the corridor (a doormat in their colour and the thing they leave
    /// outside their door) and the room itself — the wall pieces, floor pieces, what lies on the bed and one quiet hint
    /// of the secret they keep (CharacterBible). Everything is chosen by owner id (OwnerStyles) with the owner's own
    /// random stream, so a resident's room carries the same things on every seed and loop; only where they stand
    /// follows the room's shape. Kernel items (keys, envelopes, personal items) are never moved: decor keeps clear.
    /// </summary>
    public sealed partial class MansionView
    {
        // ================================================================== thresholds (corridor side)
        void BuildOwnerThresholds()
        {
            foreach (var d in Layout.Doors)
            {
                try { OwnerThreshold(d); } catch (Exception e) { Debug.LogWarning("[Mansion] owner threshold door " + d.Id + ": " + e.Message); }
            }
        }

        static bool IsOwned(Room r) => r != null && r.Type == RoomType.Bedroom && !string.IsNullOrEmpty(r.Owner) && OwnerStyles.Get(r.Owner) != null;

        void OwnerThreshold(Door d)
        {
            var ra = Layout.Room(d.RoomA); var rb = Layout.Room(d.RoomB);
            Room owned = IsOwned(ra) ? ra : IsOwned(rb) ? rb : null; if (owned == null) return;
            Room corr = owned == ra ? rb : ra; if (corr == null || Rooms[corr.Id] == null) return;
            var own = OwnerStyles.Get(owned.Owner); var cv = Rooms[corr.Id];
            var g = _grids[d.Pos.f];
            Vector3 n = d.AlongX ? Vector3.forward : Vector3.right;
            int plus = d.AlongX ? g.At(d.Pos.x, d.Pos.z + 0.25f) : g.At(d.Pos.x + 0.25f, d.Pos.z);
            Vector3 toC = plus == corr.Id ? n : -n;
            Vector3 along = d.AlongX ? Vector3.right : Vector3.forward;
            float fy = Layout.FloorY(d.Pos.f);
            var door = new Vector3(d.Pos.x, fy, d.Pos.z);
            var rnd = new System.Random(OwnerStyles.Seed(own.Id) + 17);
            var mb = new MeshBuilder();
            // the doormat: coir in the resident's colour with a border in their signature colour
            {
                float mw = Mathf.Min(d.Width * 0.8f, 1.1f), md = 0.55f;
                var c = door + toC * (0.14f + md / 2);
                var q = Quaternion.LookRotation(toC, Vector3.up);
                mb.Push(Matrix4x4.TRS(c, q, Vector3.one));
                mb.Set(S.Carpet, Color.Lerp(own.Mat, new Color(0.35f, 0.28f, 0.2f), 0.25f)); mb.BevelBox(new Vector3(0, 0.007f, 0), new Vector3(mw, 0.014f, md), 0.004f);
                mb.Set(S.Felt, own.Sig);
                mb.Box(new Vector3(0, 0.0145f, md / 2 - 0.04f), new Vector3(mw - 0.06f, 0.002f, 0.03f)); mb.Box(new Vector3(0, 0.0145f, -md / 2 + 0.04f), new Vector3(mw - 0.06f, 0.002f, 0.03f));
                mb.Box(new Vector3(mw / 2 - 0.04f, 0.0145f, 0), new Vector3(0.03f, 0.002f, md - 0.06f)); mb.Box(new Vector3(-mw / 2 + 0.04f, 0.0145f, 0), new Vector3(0.03f, 0.002f, md - 0.06f));
                mb.Pop();
            }
            // what they leave outside: beside the frame, tight to the wall, clear of the door approach and every lane
            foreach (float s in new[] { 1f, -1f })
            {
                var p = door + along * s * (d.Width / 2 + 0.62f) + toC * 0.27f;
                if (!FloorFree(cv, p.x, p.z, 0.2f)) continue;
                if (!DecorRectFree(d.Pos.f, new RectF(p.x - 0.19f, p.z - 0.19f, p.x + 0.19f, p.z + 0.19f))) continue;
                float yaw = Mathf.Atan2(toC.x, toC.z) * Mathf.Rad2Deg;
                bool tall = OutsideObject(mb, p, yaw, own, rnd, owned.Circuit);
                cv.Blocked.Add(new RectF(p.x - 0.3f, p.z - 0.3f, p.x + 0.3f, p.z + 0.3f));
                if (tall) AddDecorCollider(cv, p + Vector3.up * 0.55f, new Vector3(0.36f, 1.1f, 0.36f), "Threshold");
                break;
            }
            var mr = Emit(cv, mb, "Threshold_" + own.Id, null, ShadowCastingMode.Off);
            if (mr != null) mr.gameObject.layer = ClutterLayer;
        }

        /// <summary>The object by the door, standing at p (floor), facing yaw (into the corridor). True when tall.</summary>
        bool OutsideObject(MeshBuilder mb, Vector3 p, float yaw, OwnerLook own, System.Random rnd, int circuit)
        {
            mb.Push(p, yaw);
            bool tall = false;
            Color leather = new Color(0.22f, 0.15f, 0.1f);
            void Shoe(Vector3 at, float y, Color c, float len = 0.27f, float h = 0.1f, bool boot = false)
            {
                mb.Set(S.Leather, c); mb.Push(at, y);
                mb.BevelBox(new Vector3(0, 0.025f, 0), new Vector3(0.1f, 0.05f, len), 0.02f);
                mb.BevelBox(new Vector3(0, h * 0.5f + 0.02f, -len * 0.18f), new Vector3(0.095f, h, len * 0.55f), 0.03f);
                if (boot) mb.BevelBox(new Vector3(0, 0.2f, -len * 0.25f), new Vector3(0.09f, 0.22f, 0.12f), 0.025f);
                mb.Set(S.Rubber, new Color(0.08f, 0.08f, 0.08f)); mb.Box(new Vector3(0, 0.006f, 0), new Vector3(0.105f, 0.012f, len + 0.01f));
                mb.Pop();
            }
            switch (own.Outside)
            {
                case "boots":   // walking boots, laces loose, mud on the soles
                    Shoe(new Vector3(-0.07f, 0, 0), 4f, leather, 0.29f, 0.1f, true); Shoe(new Vector3(0.07f, 0, 0.03f), -8f, leather, 0.29f, 0.1f, true);
                    mb.Set(S.Clay, new Color(0.3f, 0.22f, 0.15f)); mb.Box(new Vector3(0, 0.002f, 0.02f), new Vector3(0.3f, 0.003f, 0.36f));
                    break;
                case "wrappers":   // a small brass dish of sweets on a stool, a few wrappers dropped round it
                    mb.Set(S.WoodDark, new Color(0.35f, 0.25f, 0.2f)); mb.Cyl(new Vector3(0, 0.44f, 0), 0.15f, 0.03f, 14); foreach (float a in new[] { 0f, 120f, 240f }) mb.Rod(Quaternion.Euler(0, a, 0) * new Vector3(0.1f, 0, 0), Quaternion.Euler(0, a, 0) * new Vector3(0.07f, 0.44f, 0), 0.015f, 5);
                    mb.Set(S.Brass, Color.white); mb.Push(new Vector3(0, 0.47f, 0), 0); mb.Lathe(new[] { new Vector2(0.04f, 0), new Vector2(0.11f, 0.04f), new Vector2(0.12f, 0.05f) }, 14); mb.Pop();
                    for (int k = 0; k < 9; k++) { mb.Set(S.GlossPaint, Color.HSVToRGB((k * 0.17f) % 1f, 0.6f, 0.75f)); mb.Sphere(new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.12f, 0.5f + (float)rnd.NextDouble() * 0.03f, ((float)rnd.NextDouble() - 0.5f) * 0.12f), 0.018f, 6, 4); }
                    for (int k = 0; k < 4; k++) { mb.Set(S.Paper, Color.HSVToRGB((k * 0.23f) % 1f, 0.5f, 0.8f)); mb.Push(new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.4f, 0.003f, ((float)rnd.NextDouble() - 0.5f) * 0.3f), (float)rnd.NextDouble() * 360f); mb.Box(Vector3.zero, new Vector3(0.05f, 0.004f, 0.03f)); mb.Pop(); }
                    break;
                case "slippers":   // house slippers set side by side, squared to the wall
                    foreach (float s in new[] { -1f, 1f }) { mb.Set(S.Felt, own.Door); mb.Push(new Vector3(s * 0.06f, 0, 0), 0); mb.BevelBox(new Vector3(0, 0.02f, 0), new Vector3(0.1f, 0.04f, 0.26f), 0.018f); mb.BevelBox(new Vector3(0, 0.045f, 0.06f), new Vector3(0.095f, 0.03f, 0.1f), 0.014f); mb.Pop(); }
                    break;
                case "levelframe":   // a frame leaning at the wall, a spirit level laid exactly on its top edge
                    mb.Set(S.Gold, new Color(0.55f, 0.45f, 0.3f)); mb.Push(new Vector3(0, 0, -0.1f), Quaternion.Euler(-8f, 0, 0), Vector3.one); FurnitureFactory.PictureFrame(mb, new Vector3(0, 0.42f, 0.02f), 0.5f, 0.66f, 3, MansionPalette.Get(own.Palette), true); mb.Pop();
                    mb.Set(S.PaintedMetal, new Color(0.9f, 0.7f, 0.15f)); mb.Box(new Vector3(0, 0.02f, 0.12f), new Vector3(0.4f, 0.035f, 0.03f));
                    mb.Set(S.Glass, Color.white); mb.Box(new Vector3(0, 0.038f, 0.12f), new Vector3(0.06f, 0.012f, 0.02f));
                    tall = false; break;
                case "shoes_towel":   // polished shoes, and a rolled pool towel with goggles on it
                    Shoe(new Vector3(-0.08f, 0, 0), 0, new Color(0.08f, 0.07f, 0.07f), 0.28f, 0.07f); Shoe(new Vector3(0.05f, 0, 0), 0, new Color(0.08f, 0.07f, 0.07f), 0.28f, 0.07f);
                    mb.Set(S.Cloth, own.Sig); mb.Rod(new Vector3(0.2f, 0.07f, -0.1f), new Vector3(0.2f, 0.07f, 0.12f), 0.07f, 12);
                    mb.Set(S.Rubber, new Color(0.1f, 0.1f, 0.12f)); mb.Push(Matrix4x4.TRS(new Vector3(0.2f, 0.145f, 0.01f), Quaternion.identity, Vector3.one)); mb.Torus(Vector3.zero, 0.05f, 0.005f, 12, 3); mb.Pop();
                    break;
                case "parcels":   // parcels stacked for dispatch, string-tied, labelled
                    for (int k = 0; k < 4; k++)
                    {
                        float w = 0.3f - k * 0.04f, h = 0.16f + (k % 2) * 0.04f; float y0 = 0; for (int j = 0; j < k; j++) y0 += 0.16f + (j % 2) * 0.04f;
                        mb.Push(new Vector3((k % 2) * 0.02f, y0, 0), k * 7f);
                        mb.Set(S.Paper, new Color(0.62f, 0.48f, 0.32f)); mb.Box(new Vector3(0, h / 2, 0), new Vector3(w, h, w * 0.8f));
                        mb.Set(S.Linen, new Color(0.85f, 0.8f, 0.7f)); mb.Box(new Vector3(0, h / 2, 0), new Vector3(w + 0.004f, h + 0.004f, 0.01f)); mb.Box(new Vector3(0, h / 2, 0), new Vector3(0.01f, h + 0.004f, w * 0.8f + 0.004f));
                        mb.Set(S.Paper, new Color(0.95f, 0.93f, 0.88f)); mb.Box(new Vector3(w * 0.2f, h + 0.002f, w * 0.2f), new Vector3(0.08f, 0.002f, 0.05f));
                        mb.Pop();
                    }
                    break;
                case "sneakers_beer":   // sneakers kicked off, empty bottles lined against the wall
                    Shoe(new Vector3(-0.1f, 0, 0.02f), 25f, new Color(0.9f, 0.88f, 0.84f), 0.29f, 0.09f); Shoe(new Vector3(0.02f, 0, 0.1f), -40f, new Color(0.9f, 0.88f, 0.84f), 0.29f, 0.09f);
                    for (int k = 0; k < 5; k++) { mb.Set(S.Glass, new Color(0.35f, 0.22f, 0.08f)); mb.Push(new Vector3(0.18f - k * 0.075f, 0, -0.14f), 0); mb.Lathe(new[] { new Vector2(0.032f, 0), new Vector2(0.032f, 0.15f), new Vector2(0.012f, 0.2f), new Vector2(0.012f, 0.24f) }, 10, true, true); mb.Pop(); }
                    break;
                case "guitarcase":   // a hard bass case leaning on the wall
                    mb.Set(S.Leather, new Color(0.08f, 0.08f, 0.09f));
                    mb.Push(Matrix4x4.TRS(new Vector3(0, 0.62f, -0.07f), Quaternion.Euler(-10f, 0, 0), Vector3.one));
                    mb.BevelBox(new Vector3(0, -0.2f, 0), new Vector3(0.38f, 0.8f, 0.12f), 0.05f); mb.BevelBox(new Vector3(0, 0.4f, 0), new Vector3(0.16f, 0.5f, 0.1f), 0.04f);
                    mb.Set(S.Chrome, Color.white); mb.Box(new Vector3(0.19f, -0.2f, 0), new Vector3(0.01f, 0.06f, 0.04f)); mb.Box(new Vector3(0.19f, 0.1f, 0), new Vector3(0.01f, 0.05f, 0.04f));
                    mb.Set(S.Paper, own.Sig); mb.Box(new Vector3(-0.05f, -0.3f, 0.061f), new Vector3(0.1f, 0.08f, 0.002f));
                    mb.Pop();
                    tall = true; break;
                case "seat37":   // a single folding theatre seat, its brass number plate "37"
                    mb.Set(S.Iron, new Color(0.15f, 0.14f, 0.13f)); foreach (float s in new[] { -1f, 1f }) mb.Box(new Vector3(s * 0.25f, 0.4f, -0.05f), new Vector3(0.05f, 0.8f, 0.3f));
                    mb.Set(S.Velvet, new Color(0.45f, 0.08f, 0.1f)); mb.BevelBox(new Vector3(0, 0.45f, 0.02f), new Vector3(0.44f, 0.08f, 0.4f), 0.03f); mb.BevelBox(new Vector3(0, 0.8f, -0.17f), new Vector3(0.44f, 0.6f, 0.08f), 0.03f);
                    mb.Set(S.Brass, Color.white); mb.Box(new Vector3(0, 1.05f, -0.12f), new Vector3(0.1f, 0.05f, 0.005f));
                    mb.Set(S.Obsidian, Color.white); mb.Box(new Vector3(-0.015f, 1.05f, -0.116f), new Vector3(0.02f, 0.03f, 0.002f)); mb.Box(new Vector3(0.015f, 1.05f, -0.116f), new Vector3(0.02f, 0.03f, 0.002f));
                    tall = true; break;
                case "basil":   // a terracotta pot of basil and a small watering can
                    mb.Set(S.Clay, new Color(0.7f, 0.4f, 0.28f)); mb.Push(Vector3.zero, 0); mb.Lathe(new[] { new Vector2(0.1f, 0), new Vector2(0.13f, 0.22f), new Vector2(0.14f, 0.24f) }, 14, true); mb.Pop();
                    mb.Set(S.Soil, new Color(0.25f, 0.18f, 0.12f)); mb.Disc(new Vector3(0, 0.22f, 0), 0.125f, 14, true);
                    FurnitureFactory.PlantShape(mb, new Vector3(0, 0.22f, 0), 0.32f, rnd, MansionPalette.Get(own.Palette), 1);
                    mb.Set(S.Copper, new Color(0.7f, 0.45f, 0.3f)); mb.Cyl(new Vector3(0.22f, 0, 0.05f), 0.06f, 0.14f, 12); mb.Rod(new Vector3(0.22f, 0.1f, 0.1f), new Vector3(0.22f, 0.2f, 0.2f), 0.01f, 5);
                    break;
                case "toolbox":   // a red steel toolbox, a coil of wire on top
                    mb.Set(S.PaintedMetal, new Color(0.55f, 0.12f, 0.1f)); mb.BevelBox(new Vector3(0, 0.12f, 0), new Vector3(0.46f, 0.24f, 0.22f), 0.01f);
                    mb.Set(S.Steel, Color.white); mb.Rod(new Vector3(-0.12f, 0.28f, 0), new Vector3(0.12f, 0.28f, 0), 0.012f, 6); mb.Box(new Vector3(-0.13f, 0.26f, 0), new Vector3(0.02f, 0.04f, 0.02f)); mb.Box(new Vector3(0.13f, 0.26f, 0), new Vector3(0.02f, 0.04f, 0.02f));
                    mb.Set(S.Copper, new Color(0.8f, 0.5f, 0.3f)); mb.Push(new Vector3(0.14f, 0.25f, 0.02f), 0); mb.Torus(Vector3.zero, 0.05f, 0.008f, 12, 4); mb.Torus(Vector3.up * 0.012f, 0.045f, 0.008f, 12, 4); mb.Pop();
                    break;
                case "starpot":   // a pot plant with star picks stuck in the soil, a pink ribbon round the pot
                    mb.Set(S.Porcelain, new Color(0.95f, 0.9f, 0.92f)); mb.Push(Vector3.zero, 0); mb.Lathe(new[] { new Vector2(0.1f, 0), new Vector2(0.12f, 0.24f), new Vector2(0.13f, 0.26f) }, 14, true); mb.Pop();
                    mb.Set(S.Velvet, own.Sig); mb.Cyl(new Vector3(0, 0.12f, 0), 0.113f, 0.03f, 14, false);
                    FurnitureFactory.PlantShape(mb, new Vector3(0, 0.24f, 0), 0.45f, rnd, MansionPalette.Get(own.Palette), 0);
                    mb.Set(S.Gold, new Color(0.95f, 0.8f, 0.5f)); for (int k = 0; k < 3; k++) { var sp = new Vector3((k - 1) * 0.07f, 0.45f + (k % 2) * 0.1f, 0.02f); mb.Rod(new Vector3(sp.x, 0.24f, 0), sp, 0.003f, 3); mb.Push(Matrix4x4.TRS(sp, Quaternion.identity, Vector3.one)); DoorView.Relief(mb, DoorView.Star(5, 0.035f, 0.015f), 0.008f); mb.Pop(); }
                    break;
                case "sneakers":   // black-and-red trainers, laces tucked in
                    Shoe(new Vector3(-0.07f, 0, 0), 0, new Color(0.1f, 0.1f, 0.1f), 0.29f, 0.1f); Shoe(new Vector3(0.07f, 0, 0), 0, new Color(0.1f, 0.1f, 0.1f), 0.29f, 0.1f);
                    mb.Set(S.GlossPaint, own.Sig); foreach (float x in new[] { -0.07f, 0.07f }) mb.Box(new Vector3(x, 0.06f, -0.06f), new Vector3(0.1f, 0.02f, 0.1f));
                    break;
                case "lilyvase":   // a tall black vase of white lilies on the floor
                    mb.Set(S.Obsidian, Color.white); mb.Push(Vector3.zero, 0); mb.Lathe(new[] { new Vector2(0.08f, 0), new Vector2(0.11f, 0.2f), new Vector2(0.07f, 0.55f), new Vector2(0.09f, 0.62f) }, 16, true); mb.Pop();
                    for (int k = 0; k < 5; k++)
                    {
                        float a = k * 72f * Mathf.Deg2Rad; var tip = new Vector3(Mathf.Cos(a) * 0.12f, 0.95f + (k % 2) * 0.12f, Mathf.Sin(a) * 0.12f);
                        mb.Set(S.Leaf, new Color(0.2f, 0.32f, 0.2f)); mb.Rod(new Vector3(0, 0.55f, 0), tip, 0.005f, 4);
                        mb.Set(S.Porcelain, new Color(0.96f, 0.95f, 0.92f)); mb.Push(tip, k * 40f); mb.Lathe(new[] { new Vector2(0.005f, 0), new Vector2(0.03f, 0.05f), new Vector2(0.05f, 0.09f) }, 6); mb.Pop();
                    }
                    tall = true; break;
                case "newspapers":   // a string-tied stack of newspapers waiting to go down
                    for (int k = 0; k < 9; k++) { mb.Set(S.Paper, Color.Lerp(new Color(0.85f, 0.83f, 0.76f), new Color(0.7f, 0.67f, 0.58f), (k % 3) / 3f)); mb.Push(new Vector3(0, k * 0.022f, 0), (k % 3 - 1) * 4f); mb.Box(new Vector3(0, 0.011f, 0), new Vector3(0.3f, 0.02f, 0.4f)); mb.Pop(); }
                    mb.Set(S.Linen, new Color(0.7f, 0.6f, 0.45f)); mb.Box(new Vector3(0, 0.1f, 0), new Vector3(0.012f, 0.205f, 0.405f)); mb.Box(new Vector3(0, 0.1f, 0), new Vector3(0.305f, 0.205f, 0.012f));
                    break;
                case "shopbag":   // two paper shopping bags from her own shop, tissue paper showing
                    for (int k = 0; k < 2; k++)
                    {
                        mb.Push(new Vector3(-0.08f + k * 0.18f, 0, k * 0.04f), k * 12f);
                        mb.Set(S.Paper, k == 0 ? Color.Lerp(own.Door, Color.white, 0.2f) : new Color(0.95f, 0.93f, 0.9f)); mb.Box(new Vector3(0, 0.17f, 0), new Vector3(0.22f, 0.34f, 0.1f));
                        mb.Set(S.Linen, new Color(0.1f, 0.1f, 0.1f)); mb.Push(Matrix4x4.TRS(new Vector3(0, 0.36f, 0), Quaternion.identity, Vector3.one)); mb.Torus(Vector3.zero, 0.05f, 0.003f, 10, 3, 0, 180); mb.Pop();
                        mb.Set(S.Paper, new Color(0.98f, 0.9f, 0.95f)); mb.Ellipsoid(new Vector3(0, 0.35f, 0), new Vector3(0.08f, 0.04f, 0.035f), 8, 4);
                        mb.Pop();
                    }
                    break;
                case "inviteStool":   // a little stool with a basket of hand-made invitations ("take one")
                    mb.Set(S.WoodPainted, Color.Lerp(own.Sig, Color.white, 0.3f)); mb.Cyl(new Vector3(0, 0.4f, 0), 0.16f, 0.03f, 16); foreach (float a in new[] { 45f, 135f, 225f, 315f }) mb.Rod(Quaternion.Euler(0, a, 0) * new Vector3(0.14f, 0, 0), Quaternion.Euler(0, a, 0) * new Vector3(0.1f, 0.4f, 0), 0.013f, 5);
                    mb.Set(S.WoodLight, new Color(0.75f, 0.6f, 0.4f)); mb.Push(new Vector3(0, 0.43f, 0), 0); mb.Lathe(new[] { new Vector2(0.09f, 0), new Vector2(0.12f, 0.08f) }, 12, true); mb.Pop();
                    for (int k = 0; k < 6; k++) { mb.Set(S.Paper, k % 2 == 0 ? new Color(0.96f, 0.94f, 0.9f) : Color.Lerp(own.Sig, Color.white, 0.4f)); mb.Push(new Vector3(-0.06f + k * 0.024f, 0.47f, 0), 0); mb.Push(Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, -15f + k * 6f), Vector3.one)); mb.Box(new Vector3(0, 0.04f, 0), new Vector3(0.004f, 0.08f, 0.11f)); mb.Pop(); mb.Pop(); }
                    break;
                case "workboots":   // heavy work boots set side by side, a folded pair of gloves on one
                    Shoe(new Vector3(-0.08f, 0, 0), 0, new Color(0.3f, 0.22f, 0.12f), 0.31f, 0.12f, true); Shoe(new Vector3(0.08f, 0, 0), 0, new Color(0.3f, 0.22f, 0.12f), 0.31f, 0.12f, true);
                    mb.Set(S.Cloth, new Color(0.55f, 0.55f, 0.4f)); mb.Box(new Vector3(0.08f, 0.33f, -0.08f), new Vector3(0.11f, 0.02f, 0.16f));
                    break;
            }
            mb.Pop();
            return tall;
        }

        // ================================================================== the room
        sealed class OwnCtx
        {
            public RoomView Rv; public OwnerLook Own; public MansionPalette Pal; public System.Random Rnd; public MeshBuilder Mb; public float Fy;
            public Furniture Bed, Desk; public int Placed; public List<Vector3> Flames = new List<Vector3>();
        }

        Furniture FindIn(RoomView rv, params string[] types)
        {
            foreach (int fid in rv.Room.Furniture) { var f = Layout.Furniture[fid]; foreach (var t in types) if (f.Type == t) return f; }
            return null;
        }

        bool ItemClear(RoomView rv, Vector3 p, float rad)
        {
            foreach (var sp in Layout.ItemSpawns) if (sp.Room == rv.Room.Id && (sp.Pos.x - p.x) * (sp.Pos.x - p.x) + (sp.Pos.z - p.z) * (sp.Pos.z - p.z) < (rad + 0.3f) * (rad + 0.3f)) return false;
            return true;
        }

        /// <summary>A free stretch of wall 2·halfW wide from 'bottom' to 'top' metres above the floor; reserved once taken.</summary>
        bool TakeWall(OwnCtx o, float halfW, float bottom, float top, out Vector3 p, out Vector3 n)
        {
            var rv = o.Rv; p = default; n = default;
            if (rv.CeilY - 0.3f < o.Fy + top) return false;
            int cnt = rv.WallSlots.Count; if (cnt == 0) return false;
            int start = (OwnerStyles.Seed(o.Own.Id) + o.Placed * 7) % cnt;
            for (int k = 0; k < cnt; k++)
            {
                int i = (start + k) % cnt;
                var sp = rv.WallSlots[i]; var sn = rv.WallSlotN[i];
                if (!WallFree(rv, sp, sn, halfW, o.Fy + bottom)) continue;
                p = new Vector3(sp.x, o.Fy, sp.z); n = sn;
                rv.WallReserved.Add(new Vector4(sp.x, sp.z, halfW + 0.05f, 0));
                o.Placed++;
                return true;
            }
            return false;
        }

        /// <summary>Floor spot (a corner first, else against a wall) clear of furniture, spots, doors and kernel items.</summary>
        bool TakeFloor(OwnCtx o, float rad, out Vector3 p, out float yaw, bool cornerOnly = false)
        {
            var rv = o.Rv; p = default; yaw = 0;
            foreach (var c in FreeCorners(rv, rad))
            {
                if (!ItemClear(rv, c, rad)) continue;
                p = c; var toC = new Vector3(rv.Room.Rect.CX - c.x, 0, rv.Room.Rect.CZ - c.z);
                yaw = Mathf.Atan2(toC.x, toC.z) * Mathf.Rad2Deg;
                rv.Blocked.Add(new RectF(c.x - rad - 0.05f, c.z - rad - 0.05f, c.x + rad + 0.05f, c.z + rad + 0.05f)); o.Placed++;
                return true;
            }
            if (cornerOnly) return false;
            int cnt = rv.WallSlots.Count;
            for (int k = 0; k < cnt; k++)
            {
                int i = (OwnerStyles.Seed(o.Own.Id) + k * 3 + o.Placed) % cnt;
                var sp = rv.WallSlots[i]; var sn = rv.WallSlotN[i];
                var c = new Vector3(sp.x, o.Fy, sp.z) + sn * (rad + 0.08f);
                if (!FloorFree(rv, c.x, c.z, rad) || !ItemClear(rv, c, rad)) continue;
                p = c; yaw = Mathf.Atan2(sn.x, sn.z) * Mathf.Rad2Deg;
                rv.Blocked.Add(new RectF(c.x - rad - 0.05f, c.z - rad - 0.05f, c.x + rad + 0.05f, c.z + rad + 0.05f)); o.Placed++;
                return true;
            }
            return false;
        }

        static Matrix4x4 WallFrame(Vector3 p, Vector3 n, float y) => Matrix4x4.TRS(new Vector3(p.x, y, p.z), Quaternion.LookRotation(n, Vector3.up), Vector3.one);

        /// <summary>Local frame on a furniture top: x across, z toward the front, origin at the top centre.</summary>
        Matrix4x4 TopFrame(Furniture f, float fy, float top) => Matrix4x4.TRS(new Vector3(f.Pos.x, fy + top, f.Pos.z), Quaternion.Euler(0, f.Yaw, 0), Vector3.one);

        void OwnerInterior(RoomView rv, MeshBuilder mb)
        {
            var own = OwnerStyles.Get(rv.Room.Owner); if (own == null) return;
            var o = new OwnCtx { Rv = rv, Own = own, Pal = rv.Pal, Rnd = new System.Random(OwnerStyles.Seed(own.Id)), Mb = mb, Fy = rv.FloorY, Bed = FindIn(rv, "Bed"), Desk = FindIn(rv, "Desk") };
            try { OwnerSet(o); } catch (Exception e) { Debug.LogWarning("[Mansion] owner room " + own.Id + ": " + e.Message + "\n" + e.StackTrace); }
            foreach (var f in o.Flames) FlameQuad(mb, f, 0.06f, -2);
            if (o.Flames.Count > 0) AddLight(rv, o.Flames[0] + Vector3.up * 0.2f, own.Light, 1.0f, 2.8f, LightType.Point, false, 0.5f, fire: true);
        }
    }
}
