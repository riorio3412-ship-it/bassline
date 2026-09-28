using System;
using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// A resident's own surface things (OwnerStyles.Surface): what sits on their desk, nightstand and console, their
    /// kind of light (a banker's lamp, a clamp work light, a vanity mirror ringed with bulbs), and how much they leave on
    /// the floor. Small pieces are physical props like any other dressing; the kinds are built here.
    /// </summary>
    public sealed partial class MansionView
    {
        static string OwnerAlias(string k)
        {
            switch (k)
            {
                case "speech": return "papers";
                case "ledgers": case "cookbook": case "typewriter": return "books";
                case "puzzle_book": return "book";
            }
            return k;
        }

        static string OwnerLampKind(OwnerLook own)
        {
            switch (own.LampKind)
            {
                case "banker": return "lamp_banker";
                case "work": return "lamp_work";
                case "vanity": return "lamp_vanity";
                case "bulb": return "lamp_bulb";
                case "ring": return "lamp_ring";
                case "candles": return "candle_mess";
                case "monitor": return null;   // the gamer's monitors light her desk (FurnitureFactory.Owners)
            }
            return "lamp";
        }

        bool OwnerDressFurniture(DressRoomState st, Furniture f, GameObject go)
        {
            var rv = st.Rv; var own = OwnerStyles.Get(rv.Room.Owner); if (own == null || own.Surface == null || own.Surface.Length == 0) return false;
            var voc = own.Surface; int seed = OwnerStyles.Seed(own.Id);
            string V(int i) => OwnerAlias(voc[(i + seed) % voc.Length]);
            float W = f.W, D = f.D; bool messy = own.Clutter >= 0.45f;
            string lamp = OwnerLampKind(own);
            switch (f.Type)
            {
                case "Desk":
                    {
                        // the whole top, their light included, is laid out by the furniture builder (FurnitureFactory.Owners)
                        return true;
                    }
                case "Nightstand":
                    {
                        float top = SurfaceTop(f, go, rv);
                        bool deskLamp = lamp != null && FindIn(rv, "Desk") != null;
                        if (!deskLamp && lamp != null && !st.Rv.Room.Furniture.Exists(id => Layout.Furniture[id].Type == "Nightstand" && Layout.Furniture[id].Id < f.Id)) Put(st, f, top, lamp, 0, -D * 0.05f, 0, true);
                        else Put(st, f, top, V(1), W * 0.12f, D * 0.1f);
                        if (messy) Put(st, f, top, V(2), -W * 0.14f, -D * 0.12f);
                        return true;
                    }
                case "Console":
                case "Sideboard":
                case "SideTable":
                case "TeaCart":
                    {
                        float top = SurfaceTop(f, go, rv);
                        int n = W < 0.7f ? 1 : own.Clutter < 0.1f ? 1 : messy ? 3 : 2;
                        for (int i = 0; i < n; i++) Put(st, f, top, V(3 + i), (n == 1 ? 0 : (i - (n - 1) * 0.5f) * W * 0.3f), -D * 0.08f + (i % 2) * D * 0.14f);
                        return true;
                    }
                case "Chest":
                    {
                        if (go.GetComponent<OpenableParts>() == null && messy) Put(st, f, SurfaceTop(f, go, rv), V(5), 0, 0);
                        return true;
                    }
            }
            return false;
        }

        void OwnerFloorKinds(Room r, ref string[] kinds, ref int n)
        {
            var own = OwnerStyles.Get(r.Owner); if (own == null) return;
            n = own.Clutter < 0.1f ? 0 : own.Clutter < 0.35f ? 1 : own.Clutter < 0.65f ? 2 : 3;
            switch (own.Id)
            {
                case "P01": kinds = new[] { "basket", "book_pile" }; break;
                case "P02": kinds = new[] { "book_pile" }; break;
                case "P05": kinds = new[] { "suitcase" }; break;
                case "P06": kinds = new[] { "crate_small", "sack" }; break;
                case "P07": kinds = new[] { "bottle_crate", "crate_small", "book_pile" }; break;
                case "P08": kinds = new[] { "basket", "book_pile", "crate_small" }; break;
                case "P09": kinds = new[] { "hatbox", "suitcase", "book_pile" }; break;
                case "P10": kinds = new[] { "basket", "sack" }; break;
                case "P11": kinds = new[] { "toolchest", "crate_small", "bucket" }; break;
                case "P12": kinds = new[] { "hatbox", "basket" }; break;
                case "P13": kinds = new[] { "basket" }; break;
                case "P15": kinds = new[] { "book_pile", "book_pile", "basket" }; break;
                case "P16": kinds = new[] { "hatbox", "basket", "suitcase_small" }; break;
                case "P17": kinds = new[] { "basket", "hatbox" }; break;
                default: n = 0; break;
            }
        }

        /// <summary>Builds one of the residents' own prop kinds (null for an unknown kind).</summary>
        GameObject OwnerKindProp(DressRoomState st, string kind, Vector3 bottom, float yaw, bool lit, ref float mass, ref Mat mat, ref bool fragile, ref bool physical)
        {
            var rv = st.Rv; var own = OwnerStyles.Get(rv.Room.Owner);
            Color sig = own != null ? own.Sig : new Color(0.6f, 0.5f, 0.4f), light = own != null ? own.Light : rv.Pal.Warm;
            string key = "own_" + kind + (own != null ? "_" + own.Id : "");
            float bulbY = -1f;
            switch (kind)
            {
                case "bread": mass = 0.5f; mat = Mat.Food; break;
                case "board_games": mass = 1.5f; mat = Mat.Paper; break;
                case "candy_bowl": mass = 1f; mat = Mat.Glass; fragile = true; break;
                case "chess_clock": mass = 0.8f; mat = Mat.Wood; break;
                case "pen_tray": mass = 0.4f; mat = Mat.Wood; break;
                case "binders": mass = 2f; mat = Mat.Paper; physical = false; break;
                case "tools_cloth": mass = 0.6f; mat = Mat.Cloth; break;
                case "chocolate": mass = 0.3f; mat = Mat.Food; break;
                case "scales": mass = 2f; mat = Mat.Metal; physical = false; break;
                case "parcel_small": mass = 0.8f; mat = Mat.Paper; break;
                case "hotpacks": mass = 0.2f; mat = Mat.Plastic; break;
                case "cables": mass = 0.5f; mat = Mat.Plastic; physical = false; break;
                case "playbills": mass = 0.2f; mat = Mat.Paper; physical = false; break;
                case "spoons": mass = 1f; mat = Mat.Ceramic; fragile = true; break;
                case "herb_pot": mass = 1.2f; mat = Mat.Ceramic; fragile = true; break;
                case "windup": mass = 0.4f; mat = Mat.Metal; break;
                case "soda": mass = 0.9f; mat = Mat.Glass; fragile = true; break;
                case "parts": mass = 0.8f; mat = Mat.Metal; physical = false; break;
                case "stickers": mass = 0.1f; mat = Mat.Paper; physical = false; break;
                case "goods": mass = 0.4f; mat = Mat.Plastic; break;
                case "trophy_small": mass = 1f; mat = Mat.Metal; break;
                case "paper_lilies": mass = 0.1f; mat = Mat.Paper; physical = false; break;
                case "tea": mass = 1.2f; mat = Mat.Ceramic; fragile = true; break;
                case "gloves": mass = 0.1f; mat = Mat.Cloth; physical = false; break;
                case "paper_cups": mass = 0.2f; mat = Mat.Paper; break;
                case "button_jars": mass = 1.2f; mat = Mat.Glass; fragile = true; break;
                case "tape_measure": mass = 0.2f; mat = Mat.Metal; break;
                case "fabric_small": mass = 0.3f; mat = Mat.Cloth; physical = false; break;
                case "paper_models": mass = 0.1f; mat = Mat.Paper; break;
                case "invites": mass = 0.2f; mat = Mat.Paper; physical = false; break;
                case "scissors_glue": mass = 0.4f; mat = Mat.Metal; break;
                case "thermos": mass = 0.9f; mat = Mat.Metal; break;
                case "lamp_banker": mass = 2.5f; mat = Mat.Glass; fragile = true; bulbY = 0.3f; break;
                case "lamp_work": mass = 1.8f; mat = Mat.Metal; bulbY = 0.42f; break;
                case "lamp_vanity": mass = 3f; mat = Mat.Glass; fragile = true; physical = false; bulbY = 0.3f; break;
                case "lamp_bulb": mass = 0.6f; mat = Mat.Metal; bulbY = 0.36f; break;
                case "lamp_ring": mass = 1.2f; mat = Mat.Plastic; physical = false; bulbY = 0.3f; break;
                default: return null;
            }
            var go = ProcProp(rv, key, bottom, yaw, () => OwnerKindMesh(kind, sig, light, rv.Room.Circuit), out _);
            if (bulbY > 0f && lit && st.Lights < 2 && _propLights < 48)
            {
                var rec = AddLight(rv, go.transform.TransformPoint(new Vector3(0, bulbY, 0.04f)), light, kind == "lamp_ring" || kind == "lamp_vanity" ? 1.3f : 1.6f, 3.4f, LightType.Point, false, 0.03f);
                if (rec != null && rec.Light != null) rec.Light.transform.SetParent(go.transform, true);
                st.Lights++; _propLights++;
            }
            return go;
        }

        static MeshBuilder OwnerKindMesh(string kind, Color sig, Color light, int circuit) { var mb = new MeshBuilder(); OwnerKindBuild(mb, kind, sig, light, circuit); return mb; }

        /// <summary>Writes one owner prop kind into mb at its current frame (also used for the desk lamps on owner desks).</summary>
        internal static void OwnerKindBuild(MeshBuilder mb, string kind, Color sig, Color light, int circuit)
        {
            Color ivory = new Color(0.93f, 0.9f, 0.82f), dark = new Color(0.08f, 0.08f, 0.08f);
            var rnd = new System.Random(kind.Length * 131 + (int)(sig.r * 97));
            switch (kind)
            {
                case "bread":
                    mb.Set(S.WoodLight, new Color(0.75f, 0.6f, 0.42f)); mb.BevelBox(new Vector3(0, 0.01f, 0), new Vector3(0.3f, 0.02f, 0.18f), 0.005f);
                    mb.Set(S.Clay, new Color(0.78f, 0.55f, 0.3f)); mb.Ellipsoid(new Vector3(-0.02f, 0.06f, 0), new Vector3(0.12f, 0.045f, 0.065f), 12, 7);
                    mb.Set(S.Clay, new Color(0.92f, 0.82f, 0.62f)); mb.Push(Matrix4x4.TRS(new Vector3(0.1f, 0.05f, 0), Quaternion.Euler(0, 0, 90), Vector3.one)); mb.Cyl(new Vector3(0, -0.01f, 0), 0.04f, 0.02f, 12); mb.Pop();
                    break;
                case "board_games":
                    mb.Set(S.Paper, new Color(0.55f, 0.15f, 0.12f)); mb.Box(new Vector3(0, 0.03f, 0), new Vector3(0.3f, 0.06f, 0.2f));
                    mb.Set(S.Paper, new Color(0.15f, 0.3f, 0.45f)); mb.Push(new Vector3(0.01f, 0.06f, 0), 8f); mb.Box(new Vector3(0, 0.025f, 0), new Vector3(0.26f, 0.05f, 0.18f)); mb.Pop();
                    mb.Set(S.Paper, ivory); mb.Box(new Vector3(0, 0.061f, 0.101f), new Vector3(0.18f, 0.03f, 0.002f));
                    break;
                case "candy_bowl":
                    mb.Set(S.Glass, Color.white); mb.Lathe(new[] { new Vector2(0.04f, 0), new Vector2(0.05f, 0.02f), new Vector2(0.1f, 0.07f), new Vector2(0.105f, 0.08f) }, 14, true);
                    for (int k = 0; k < 12; k++) { mb.Set(S.GlossPaint, Color.HSVToRGB((k * 0.137f) % 1f, 0.6f, 0.8f)); float a = k * 2.4f, r = 0.02f + (k % 4) * 0.018f; mb.Sphere(new Vector3(Mathf.Cos(a) * r, 0.05f + (k / 6) * 0.015f, Mathf.Sin(a) * r), 0.016f, 6, 4); }
                    break;
                case "chess_clock":
                    mb.Set(S.WoodDark, new Color(0.45f, 0.3f, 0.2f)); mb.Push(Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one)); mb.Prism(new List<Vector2> { new Vector2(-0.12f, -0.04f), new Vector2(0.12f, -0.04f), new Vector2(0.12f, 0.03f), new Vector2(-0.12f, 0.03f) }, 0, 0.09f, true, false); mb.Pop();
                    foreach (float s in new[] { -1f, 1f }) { mb.Set(S.Porcelain, ivory); mb.Push(Matrix4x4.TRS(new Vector3(s * 0.055f, 0.055f, 0.031f), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Cyl(Vector3.zero, 0.035f, 0.004f, 14); mb.Pop(); mb.Set(S.Brass, Color.white); mb.Cyl(new Vector3(s * 0.055f, 0.09f, 0), 0.01f, 0.025f, 8); }
                    break;
                case "pen_tray":
                    mb.Set(S.WoodDark, new Color(0.3f, 0.2f, 0.14f)); mb.Box(new Vector3(0, 0.01f, 0), new Vector3(0.24f, 0.02f, 0.08f));
                    for (int k = 0; k < 5; k++) { mb.Set(S.GlossPaint, k == 0 ? new Color(0.7f, 0.1f, 0.1f) : k == 1 ? sig : new Color(0.1f, 0.12f, 0.2f)); mb.Rod(new Vector3(-0.1f, 0.024f, -0.028f + k * 0.014f), new Vector3(0.1f, 0.024f, -0.028f + k * 0.014f), 0.005f, 6); }
                    break;
                case "binders":
                    for (int k = 0; k < 3; k++) { mb.Set(S.Leather, k == 1 ? sig * 0.7f : new Color(0.12f, 0.15f, 0.25f)); mb.Box(new Vector3(-0.05f + k * 0.05f, 0.13f, 0), new Vector3(0.045f, 0.26f, 0.2f)); mb.Set(S.Paper, ivory); mb.Box(new Vector3(-0.05f + k * 0.05f, 0.16f, 0.101f), new Vector3(0.03f, 0.08f, 0.002f)); }
                    break;
                case "tools_cloth":
                    mb.Set(S.Linen, new Color(0.9f, 0.88f, 0.82f)); mb.Rod(new Vector3(-0.14f, 0.03f, 0), new Vector3(0.14f, 0.03f, 0), 0.03f, 10);
                    for (int k = 0; k < 5; k++) { mb.Set(S.WoodLight, Color.white); mb.Rod(new Vector3(-0.1f + k * 0.05f, 0.05f, 0), new Vector3(-0.1f + k * 0.05f, 0.1f + (k % 2) * 0.03f, -0.02f), 0.005f, 5); }
                    break;
                case "chocolate":
                    for (int k = 0; k < 3; k++) { mb.Set(S.Gold, new Color(0.7f, 0.66f, 0.6f)); mb.Push(new Vector3(0, k * 0.012f, 0), k * 9f); mb.Box(new Vector3(0, 0.006f, 0), new Vector3(0.16f, 0.012f, 0.08f)); mb.Pop(); }
                    mb.Set(S.Clay, new Color(0.2f, 0.12f, 0.08f)); mb.Box(new Vector3(0.02f, 0.04f, 0.01f), new Vector3(0.1f, 0.01f, 0.07f));
                    break;
                case "scales":
                    mb.Set(S.Brass, Color.white); mb.Cyl(Vector3.zero, 0.06f, 0.02f, 12); mb.Rod(new Vector3(0, 0.02f, 0), new Vector3(0, 0.24f, 0), 0.007f, 6); mb.Rod(new Vector3(-0.13f, 0.23f, 0), new Vector3(0.13f, 0.23f, 0), 0.005f, 5);
                    foreach (float s in new[] { -1f, 1f }) { for (int k = 0; k < 3; k++) { float a = k * 120f * Mathf.Deg2Rad; mb.Rod(new Vector3(s * 0.13f, 0.23f, 0), new Vector3(s * 0.13f + Mathf.Cos(a) * 0.05f, 0.12f, Mathf.Sin(a) * 0.05f), 0.0015f, 3); } mb.Push(new Vector3(s * 0.13f, 0.11f, 0), 0); mb.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.055f, 0.012f), new Vector2(0.06f, 0.016f) }, 12); mb.Pop(); }
                    break;
                case "parcel_small":
                    mb.Set(S.Paper, new Color(0.62f, 0.48f, 0.32f)); mb.Box(new Vector3(0, 0.06f, 0), new Vector3(0.18f, 0.12f, 0.14f));
                    mb.Set(S.Linen, new Color(0.8f, 0.72f, 0.6f)); mb.Box(new Vector3(0, 0.06f, 0), new Vector3(0.184f, 0.124f, 0.01f)); mb.Box(new Vector3(0, 0.06f, 0), new Vector3(0.01f, 0.124f, 0.144f));
                    mb.Set(S.Paper, sig); mb.Box(new Vector3(0.04f, 0.121f, 0.03f), new Vector3(0.06f, 0.002f, 0.04f));
                    break;
                case "hotpacks":
                    for (int k = 0; k < 4; k++) { mb.Set(S.Plastic, k % 2 == 0 ? new Color(0.9f, 0.9f, 0.88f) : new Color(0.85f, 0.35f, 0.2f)); mb.Push(new Vector3((k % 2) * 0.03f, k * 0.008f, (k / 2) * 0.02f), k * 23f); mb.BevelBox(new Vector3(0, 0.004f, 0), new Vector3(0.09f, 0.008f, 0.12f), 0.003f); mb.Pop(); }
                    break;
                case "cables":
                    mb.Set(S.Rubber, new Color(0.07f, 0.07f, 0.08f)); for (int k = 0; k < 5; k++) mb.Torus(new Vector3(0, 0.006f + k * 0.009f, 0), 0.08f - k * 0.004f, 0.006f, 14, 4);
                    mb.Set(S.Chrome, Color.white); mb.Cyl(new Vector3(0.1f, 0.006f, 0.02f), 0.008f, 0.05f, 8);
                    break;
                case "playbills":
                    for (int k = 0; k < 5; k++) { mb.Set(S.Paper, Color.Lerp(Color.HSVToRGB((0.05f + k * 0.17f) % 1f, 0.45f, 0.7f), ivory, 0.3f)); mb.Push(new Vector3(0, 0.002f + k * 0.002f, 0), -20f + k * 10f); mb.Box(new Vector3(0, 0, 0.06f), new Vector3(0.1f, 0.002f, 0.15f)); mb.Pop(); }
                    break;
                case "spoons":
                    mb.Set(S.Ceramic, new Color(0.62f, 0.48f, 0.36f)); mb.Lathe(new[] { new Vector2(0.045f, 0), new Vector2(0.05f, 0.12f), new Vector2(0.052f, 0.13f) }, 12, true);
                    for (int k = 0; k < 5; k++) { float a = k * 72f * Mathf.Deg2Rad; mb.Set(S.WoodLight, Color.white); var tip = new Vector3(Mathf.Cos(a) * 0.04f, 0.3f, Mathf.Sin(a) * 0.04f); mb.Rod(new Vector3(Mathf.Cos(a) * 0.015f, 0.05f, Mathf.Sin(a) * 0.015f), tip, 0.006f, 5); if (k % 2 == 0) mb.Ellipsoid(tip + Vector3.up * 0.02f, new Vector3(0.02f, 0.03f, 0.008f), 6, 4); else { mb.Set(S.Steel, Color.white); mb.Push(Matrix4x4.TRS(tip + Vector3.up * 0.03f, Quaternion.identity, new Vector3(0.5f, 1.2f, 0.5f))); mb.Torus(Vector3.zero, 0.02f, 0.002f, 10, 3); mb.Pop(); } }
                    break;
                case "herb_pot":
                    mb.Set(S.Clay, new Color(0.7f, 0.4f, 0.28f)); mb.Lathe(new[] { new Vector2(0.05f, 0), new Vector2(0.065f, 0.1f), new Vector2(0.07f, 0.11f) }, 12, true);
                    mb.Set(S.Leaf, new Color(0.3f, 0.52f, 0.25f)); for (int k = 0; k < 9; k++) { float a = k * 2.4f; mb.Ellipsoid(new Vector3(Mathf.Cos(a) * 0.035f, 0.14f + (k % 3) * 0.03f, Mathf.Sin(a) * 0.035f), new Vector3(0.025f, 0.012f, 0.018f), 6, 3); }
                    break;
                case "windup":
                    mb.Set(S.PaintedMetal, Color.Lerp(sig, new Color(0.6f, 0.2f, 0.15f), 0.4f)); mb.BevelBox(new Vector3(0, 0.05f, 0), new Vector3(0.07f, 0.08f, 0.05f), 0.008f); mb.BevelBox(new Vector3(0, 0.115f, 0), new Vector3(0.05f, 0.05f, 0.045f), 0.008f);
                    mb.Set(S.Glass, new Color(0.9f, 0.8f, 0.4f)); foreach (float s in new[] { -1f, 1f }) mb.Sphere(new Vector3(s * 0.012f, 0.12f, 0.023f), 0.007f, 6, 4);
                    mb.Set(S.Brass, Color.white); mb.Rod(new Vector3(0, 0.05f, -0.025f), new Vector3(0, 0.05f, -0.045f), 0.004f, 4); mb.Push(Matrix4x4.TRS(new Vector3(0, 0.05f, -0.05f), Quaternion.Euler(0, 0, 0), Vector3.one)); mb.Torus(new Vector3(-0.012f, 0, 0), 0.01f, 0.003f, 8, 3); mb.Torus(new Vector3(0.012f, 0, 0), 0.01f, 0.003f, 8, 3); mb.Pop();
                    foreach (float s in new[] { -1f, 1f }) { mb.Set(S.PaintedMetal, dark); mb.Box(new Vector3(s * 0.02f, 0.008f, 0), new Vector3(0.025f, 0.016f, 0.06f)); }
                    break;
                case "soda":
                    for (int k = 0; k < 2; k++) { mb.Set(S.Glass, new Color(0.45f, 0.7f, 0.55f)); mb.Push(new Vector3(k * 0.07f - 0.035f, 0, (k % 2) * 0.03f), 0); mb.Lathe(new[] { new Vector2(0.028f, 0), new Vector2(0.03f, 0.13f), new Vector2(0.012f, 0.19f), new Vector2(0.012f, 0.21f) }, 10, true, true); mb.Pop(); mb.Set(S.Steel, Color.white); mb.Cyl(new Vector3(k * 0.07f - 0.035f, 0.21f, (k % 2) * 0.03f), 0.014f, 0.01f, 8); }
                    break;
                case "parts":
                    mb.Set(S.PaintedMetal, new Color(0.3f, 0.32f, 0.35f)); mb.Box(new Vector3(0, 0.01f, 0), new Vector3(0.22f, 0.02f, 0.14f));
                    mb.Set(S.Brass, Color.white); for (int k = 0; k < 7; k++) { mb.Push(new Vector3(-0.08f + (k % 4) * 0.05f, 0.025f, -0.035f + (k / 4) * 0.06f), k * 30f); mb.Torus(Vector3.zero, 0.012f + (k % 3) * 0.005f, 0.004f, 10, 3); mb.Pop(); }
                    mb.Set(S.Steel, Color.white); for (int k = 0; k < 5; k++) mb.Cyl(new Vector3(-0.06f + k * 0.03f, 0.02f, 0.05f), 0.004f, 0.02f, 6);
                    break;
                case "stickers":
                    for (int k = 0; k < 3; k++) { mb.Set(S.Paper, new Color(0.96f, 0.96f, 0.96f)); mb.Push(new Vector3(k * 0.01f, 0.002f + k * 0.002f, k * 0.01f), k * 8f); mb.Box(Vector3.zero, new Vector3(0.12f, 0.002f, 0.16f)); for (int s = 0; s < 6; s++) { mb.Set(S.GlossPaint, Color.HSVToRGB((0.85f + s * 0.05f) % 1f, 0.5f, 0.9f)); mb.Disc(new Vector3(-0.035f + (s % 3) * 0.035f, 0.0015f, -0.04f + (s / 3) * 0.06f), 0.014f, 8, true); } mb.Pop(); }
                    break;
                case "goods":
                    mb.Set(S.Glass, Color.white); mb.Box(new Vector3(-0.04f, 0.07f, 0), new Vector3(0.07f, 0.13f, 0.005f)); mb.Box(new Vector3(-0.04f, 0.003f, 0), new Vector3(0.07f, 0.006f, 0.04f));
                    mb.Set(S.Paper, Color.Lerp(sig, Color.white, 0.3f)); mb.Box(new Vector3(-0.04f, 0.075f, 0.003f), new Vector3(0.05f, 0.1f, 0.001f));
                    mb.Set(S.Velvet, new Color(0.95f, 0.85f, 0.88f)); mb.Sphere(new Vector3(0.05f, 0.03f, 0), 0.03f, 8, 6); mb.Sphere(new Vector3(0.05f, 0.075f, 0), 0.022f, 8, 5);
                    break;
                case "trophy_small":
                    mb.Set(S.WoodDark, new Color(0.15f, 0.12f, 0.1f)); mb.Box(new Vector3(0, 0.02f, 0), new Vector3(0.07f, 0.04f, 0.07f));
                    mb.Set(S.Gold, new Color(0.85f, 0.7f, 0.4f)); mb.Push(new Vector3(0, 0.04f, 0), 0); mb.Lathe(new[] { new Vector2(0.01f, 0), new Vector2(0.01f, 0.05f), new Vector2(0.04f, 0.08f), new Vector2(0.045f, 0.13f) }, 12); mb.Pop();
                    break;
                case "paper_lilies":
                    mb.Set(S.Paper, new Color(0.97f, 0.97f, 0.95f)); for (int k = 0; k < 3; k++) { mb.Push(new Vector3(-0.05f + k * 0.05f, 0, (k % 2) * 0.03f), k * 40f); mb.Lathe(new[] { new Vector2(0.004f, 0), new Vector2(0.022f, 0.03f), new Vector2(0.034f, 0.05f) }, 5); mb.Pop(); }
                    break;
                case "tea":
                    mb.Set(S.Porcelain, new Color(0.94f, 0.93f, 0.9f)); mb.Lathe(new[] { new Vector2(0.05f, 0), new Vector2(0.07f, 0.05f), new Vector2(0.04f, 0.1f), new Vector2(0.015f, 0.12f) }, 14, true);
                    mb.Rod(new Vector3(0.06f, 0.05f, 0), new Vector3(0.1f, 0.09f, 0), 0.008f, 5);
                    mb.Push(new Vector3(-0.1f, 0, 0.05f), 0); mb.Lathe(new[] { new Vector2(0.025f, 0), new Vector2(0.038f, 0.045f), new Vector2(0.04f, 0.05f) }, 12, true); mb.Pop();
                    mb.Set(S.Obsidian, dark); mb.Cyl(new Vector3(0, 0.12f, 0), 0.02f, 0.012f, 10);
                    break;
                case "gloves":
                    mb.Set(S.Cloth, dark); foreach (float s in new[] { 0f, 0.07f }) { mb.Push(new Vector3(-0.03f + s, 0.002f, 0), 10f + s * 60f); mb.Box(Vector3.zero, new Vector3(0.06f, 0.004f, 0.15f)); for (int k = 0; k < 4; k++) mb.Box(new Vector3(-0.022f + k * 0.015f, 0, -0.1f), new Vector3(0.011f, 0.004f, 0.05f)); mb.Pop(); }
                    break;
                case "paper_cups":
                    for (int k = 0; k < 2; k++) { mb.Set(S.Paper, new Color(0.92f, 0.9f, 0.86f)); mb.Push(new Vector3(k * 0.09f - 0.045f, 0, (k % 2) * 0.04f), 0); mb.Lathe(new[] { new Vector2(0.028f, 0), new Vector2(0.04f, 0.11f) }, 12, true); mb.Set(S.Plastic, new Color(0.15f, 0.12f, 0.1f)); mb.Cyl(new Vector3(0, 0.11f, 0), 0.042f, 0.01f, 12); mb.Set(S.Paper, new Color(0.55f, 0.35f, 0.2f)); mb.Cyl(new Vector3(0, 0.04f, 0), 0.036f, 0.03f, 12, false); mb.Pop(); }
                    break;
                case "button_jars":
                    mb.Set(S.Glass, Color.white); mb.Lathe(new[] { new Vector2(0.05f, 0), new Vector2(0.055f, 0.14f), new Vector2(0.04f, 0.16f) }, 12, true);
                    for (int b = 0; b < 14; b++) { mb.Set(S.Porcelain, Color.HSVToRGB((0.8f + b * 0.03f) % 1f, 0.4f, 0.75f)); mb.Push(Matrix4x4.TRS(new Vector3(((b % 3) - 1) * 0.025f, 0.015f + (b / 3) * 0.022f, ((b % 2) - 0.5f) * 0.03f), Quaternion.Euler(b * 37f, 0, b * 21f), Vector3.one)); mb.Cyl(Vector3.zero, 0.013f, 0.005f, 8); mb.Pop(); }
                    mb.Set(S.Steel, Color.white); mb.Cyl(new Vector3(0, 0.155f, 0), 0.042f, 0.015f, 12);
                    break;
                case "tape_measure":
                    mb.Set(S.GlossPaint, new Color(0.95f, 0.85f, 0.25f)); mb.Cyl(Vector3.zero, 0.035f, 0.02f, 14);
                    var tp = new List<Vector3>(); for (int k = 0; k <= 12; k++) { float t = k / 12f; tp.Add(new Vector3(0.035f + t * 0.22f, 0.002f + Mathf.Sin(t * 6f) * 0.0f, Mathf.Sin(t * 3f) * 0.05f)); } mb.Tube(tp, 0.004f, 3);
                    break;
                case "fabric_small":
                    for (int k = 0; k < 4; k++) { mb.Set(S.Velvet, Color.HSVToRGB((0.8f + k * 0.05f) % 1f, 0.45f, 0.55f + k * 0.08f)); mb.Push(new Vector3(0, k * 0.012f, 0), k * 6f); mb.BevelBox(new Vector3(0, 0.006f, 0), new Vector3(0.2f, 0.012f, 0.16f), 0.004f); mb.Pop(); }
                    break;
                case "paper_models":
                    mb.Set(S.Paper, new Color(0.96f, 0.95f, 0.92f)); mb.Box(new Vector3(0, 0.04f, 0), new Vector3(0.1f, 0.08f, 0.08f));
                    mb.Set(S.Paper, Color.Lerp(sig, Color.white, 0.4f)); mb.Quad(new Vector3(-0.055f, 0.08f, -0.045f), new Vector3(0, 0.12f, -0.045f), new Vector3(0, 0.12f, 0.045f), new Vector3(-0.055f, 0.08f, 0.045f)); mb.Quad(new Vector3(0, 0.12f, -0.045f), new Vector3(0.055f, 0.08f, -0.045f), new Vector3(0.055f, 0.08f, 0.045f), new Vector3(0, 0.12f, 0.045f));
                    mb.Set(S.Paper, new Color(0.3f, 0.25f, 0.2f)); mb.Box(new Vector3(0, 0.03f, 0.041f), new Vector3(0.02f, 0.04f, 0.002f));
                    break;
                case "invites":
                    for (int k = 0; k < 5; k++) { mb.Set(S.Paper, k % 2 == 0 ? new Color(0.96f, 0.94f, 0.9f) : Color.Lerp(sig, Color.white, 0.5f)); mb.Push(new Vector3(0, 0.002f + k * 0.003f, 0), k * 7f - 14f); mb.Box(Vector3.zero, new Vector3(0.16f, 0.003f, 0.11f)); mb.Pop(); }
                    mb.Set(S.GlossPaint, sig * 0.7f); mb.Cyl(new Vector3(0, 0.016f, 0), 0.014f, 0.004f, 10);
                    break;
                case "scissors_glue":
                    mb.Set(S.Steel, Color.white); mb.Push(new Vector3(-0.04f, 0.004f, 0), 20f); mb.Box(new Vector3(0, 0, 0.05f), new Vector3(0.012f, 0.004f, 0.1f)); mb.Pop(); mb.Push(new Vector3(-0.04f, 0.006f, 0), -5f); mb.Box(new Vector3(0, 0, 0.05f), new Vector3(0.012f, 0.004f, 0.1f)); mb.Pop();
                    mb.Set(S.Plastic, sig); foreach (float s in new[] { -1f, 1f }) mb.Torus(new Vector3(-0.04f + s * 0.018f, 0.005f, -0.025f), 0.016f, 0.004f, 10, 3);
                    mb.Set(S.Plastic, new Color(0.95f, 0.95f, 0.92f)); mb.Cyl(new Vector3(0.06f, 0, 0), 0.02f, 0.09f, 10); mb.Set(S.Plastic, new Color(0.9f, 0.5f, 0.15f)); mb.Cyl(new Vector3(0.06f, 0.09f, 0), 0.012f, 0.025f, 8);
                    break;
                case "thermos":
                    mb.Set(S.PaintedMetal, new Color(0.3f, 0.36f, 0.26f)); mb.Lathe(new[] { new Vector2(0.04f, 0), new Vector2(0.042f, 0.005f), new Vector2(0.042f, 0.24f), new Vector2(0.03f, 0.27f) }, 14, true);
                    mb.Set(S.Steel, Color.white); mb.Lathe(new[] { new Vector2(0.034f, 0.27f), new Vector2(0.036f, 0.33f), new Vector2(0.03f, 0.335f) }, 12, false, true);
                    mb.Set(S.Obsidian, dark); mb.Push(Matrix4x4.TRS(new Vector3(0.05f, 0.14f, 0), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Torus(Vector3.zero, 0.035f, 0.007f, 12, 4, -90, 90); mb.Pop();
                    break;
                case "lamp_banker":
                    mb.Set(S.Brass, Color.white); mb.BevelBox(new Vector3(0, 0.012f, 0), new Vector3(0.22f, 0.024f, 0.14f), 0.006f); mb.Rod(new Vector3(0, 0.02f, -0.03f), new Vector3(0, 0.27f, -0.03f), 0.009f, 6);
                    mb.Set(S.Glow, new Color(0.08f, 0.45f, 0.22f), MansionMats.GlowData(0.7f, 0.02f, 0, circuit));
                    mb.Push(Matrix4x4.TRS(new Vector3(0, 0.3f, 0.02f), Quaternion.Euler(0, 0, 90), new Vector3(1, 1, 1))); mb.Lathe(new[] { new Vector2(0.05f, -0.15f), new Vector2(0.07f, -0.12f), new Vector2(0.075f, 0.12f), new Vector2(0.05f, 0.15f) }, 12, false, false, 90f, 270f); mb.Pop();
                    mb.Set(S.Glow, light, MansionMats.GlowData(1.2f, 0.02f, 0, circuit)); mb.Box(new Vector3(0, 0.27f, 0.035f), new Vector3(0.2f, 0.01f, 0.06f));
                    break;
                case "lamp_work":
                    mb.Set(S.PaintedMetal, new Color(0.14f, 0.14f, 0.15f)); mb.Cyl(Vector3.zero, 0.07f, 0.02f, 14); mb.Rod(new Vector3(0, 0.02f, 0), new Vector3(0, 0.25f, 0.08f), 0.008f, 6); mb.Rod(new Vector3(0, 0.25f, 0.08f), new Vector3(0, 0.44f, 0.02f), 0.008f, 6);
                    mb.Push(Matrix4x4.TRS(new Vector3(0, 0.44f, 0.05f), Quaternion.Euler(-35f, 0, 0), Vector3.one)); mb.Lathe(new[] { new Vector2(0.02f, 0.05f), new Vector2(0.07f, -0.04f), new Vector2(0.072f, -0.045f) }, 14); mb.Set(S.Glow, light, MansionMats.GlowData(1.4f, 0, 0, circuit)); mb.Disc(new Vector3(0, -0.035f, 0), 0.06f, 12, false); mb.Pop();
                    break;
                case "lamp_vanity":
                    mb.Set(S.WoodDark, new Color(0.25f, 0.15f, 0.12f)); mb.Box(new Vector3(0, 0.015f, 0), new Vector3(0.3f, 0.03f, 0.1f)); mb.Rod(new Vector3(0, 0.03f, -0.01f), new Vector3(0, 0.1f, -0.01f), 0.01f, 6);
                    mb.Push(Matrix4x4.TRS(new Vector3(0, 0.3f, -0.01f), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Set(S.Gold, new Color(0.8f, 0.66f, 0.42f)); mb.Cyl(new Vector3(0, -0.01f, 0), 0.2f, 0.02f, 24); mb.Set(S.Mirror, Color.white); mb.Cyl(new Vector3(0, 0.0f, 0), 0.17f, 0.012f, 24); mb.Pop();
                    mb.Set(S.Glow, light, MansionMats.GlowData(1.3f, 0.03f, 0, circuit)); for (int k = 0; k < 10; k++) { float a = k / 10f * Mathf.PI * 2; mb.Sphere(new Vector3(Mathf.Cos(a) * 0.185f, 0.3f + Mathf.Sin(a) * 0.185f, 0.01f), 0.014f, 6, 4); }
                    break;
                case "lamp_bulb":
                    mb.Set(S.Iron, new Color(0.15f, 0.14f, 0.13f)); mb.Box(new Vector3(0, 0.02f, 0), new Vector3(0.06f, 0.04f, 0.06f)); mb.Rod(new Vector3(0, 0.04f, 0), new Vector3(0, 0.3f, 0.05f), 0.005f, 5);
                    mb.Push(Matrix4x4.TRS(new Vector3(0, 0.3f, 0.05f), Quaternion.Euler(-60f, 0, 0), Vector3.one)); mb.Lathe(new[] { new Vector2(0.015f, 0.03f), new Vector2(0.075f, -0.03f), new Vector2(0.078f, -0.035f) }, 12); mb.Set(S.Glow, light, MansionMats.GlowData(1.8f, 0.02f, 0, circuit)); mb.Sphere(new Vector3(0, -0.01f, 0), 0.025f, 8, 6); mb.Pop();
                    mb.Set(S.Rubber, new Color(0.06f, 0.06f, 0.06f)); mb.Tube(new List<Vector3> { new Vector3(0, 0.03f, -0.03f), new Vector3(-0.05f, 0.005f, -0.08f), new Vector3(-0.12f, 0.004f, -0.06f) }, 0.004f, 4);
                    break;
                case "lamp_ring":
                    mb.Set(S.Obsidian, dark); foreach (float a in new[] { 0f, 120f, 240f }) mb.Rod(new Vector3(0, 0.08f, 0), Quaternion.Euler(0, a, 0) * new Vector3(0.06f, 0, 0), 0.004f, 4); mb.Rod(new Vector3(0, 0.08f, 0), new Vector3(0, 0.18f, 0), 0.005f, 5);
                    mb.Set(S.Glow, light, MansionMats.GlowData(1.6f, 0, 0, circuit)); mb.Push(Matrix4x4.TRS(new Vector3(0, 0.3f, 0), Quaternion.Euler(90, 0, 0), Vector3.one)); mb.Torus(Vector3.zero, 0.11f, 0.012f, 20, 5); mb.Pop();
                    mb.Set(S.Plastic, dark); mb.Box(new Vector3(0, 0.3f, 0.005f), new Vector3(0.05f, 0.09f, 0.008f));
                    break;
            }
            return;
        }
    }
}
