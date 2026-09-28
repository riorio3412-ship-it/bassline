using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>Material slots shared by the whole mansion (one material per slot, tints via vertex colors).</summary>
    public static class S
    {
        public const int PaperDamask = 0, PaperStripe = 1, PaperEyes = 2, PaperMoon = 3, PaperFlesh = 4, PaperDamask2 = 5;
        public const int Wainscot = 10, WoodDark = 11, WoodLight = 12, WoodPainted = 13, WoodCherry = 14, WoodWorn = 15;
        public const int Gold = 20, Brass = 21, Iron = 22, Steel = 23, Chrome = 24, Copper = 25, RustyMetal = 26, GreenRust = 27, PaintedMetal = 28, MetalPlate = 29;
        public const int FloorChecker = 30, FloorCheckerTile = 31, Parquet = 32, Herringbone = 33, Carpet = 34, CarpetJacquard = 35, StoneFloor = 36, Concrete = 37,
                         Mosaic = 38, Terrazzo = 39, FloorTile = 40, PoolTile = 41, Soil = 42, BrickFloor = 43, WornTile = 44, MarbleFloor = 45, WetStone = 46;
        public const int Plaster = 50, PlasterWhite = 51, Brick = 52, WallTile = 53, StoneWall = 54, Ceiling = 55, ConcreteWall = 56;
        public const int Velvet = 60, Jacquard = 61, Linen = 62, Leather = 63, LeatherRed = 64, Quatrefoil = 65, Felt = 66, Cloth = 67;
        public const int Marble = 70, MarbleDark = 71, Porcelain = 72, Ceramic = 73, Flesh = 74, Bone = 75, Paper = 76, Plastic = 77, Rubber = 78, Leaf = 79,
                         Wax = 80, Books = 81, Painting = 82, Mirror = 83, Clay = 84, Obsidian = 85, GlossPaint = 86;
        public const int Glow = 100, GlowAdd = 101, Flame = 102, Ray = 103, Halo = 104;
        public const int Window = 110, Stained = 111, Sky = 112, Water = 113, Caustic = 114, Screen = 115, Decal = 116, Glass = 117, ShallowWater = 118, Aquarium = 119, Clock = 120;
        public const int RugA = 121, RugB = 122, RugC = 123, RugD = 124;   // Persian rugs (uv 0..1 over the rug)
    }

    /// <summary>Loads shaders/textures (Resources/Mansion) and creates the shared materials.</summary>
    public static class MansionMats
    {
        static readonly Dictionary<int, Material> _slot = new Dictionary<int, Material>();
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Material> _named = new Dictionary<string, Material>();
        static Shader _lit, _glow, _glass, _sky, _water, _decal, _screen, _particle;
        public static bool Ready => _lit != null;

        public static Shader Lit { get { Init(); return _lit; } }
        public static Shader GlowShader { get { Init(); return _glow; } }

        public static void Init()
        {
            if (_lit != null) return;
            var refs = Resources.Load<MansionShaderRefs>("Mansion/MansionShaders");
            _lit = refs != null && refs.Lit != null ? refs.Lit : Shader.Find("BL23/MansionLit");
            _glow = refs != null && refs.Glow != null ? refs.Glow : Shader.Find("BL23/MansionGlow");
            _glass = refs != null && refs.Glass != null ? refs.Glass : Shader.Find("BL23/MansionGlass");
            _sky = refs != null && refs.Sky != null ? refs.Sky : Shader.Find("BL23/MansionSky");
            _water = refs != null && refs.Water != null ? refs.Water : Shader.Find("BL23/MansionWater");
            _decal = refs != null && refs.Decal != null ? refs.Decal : Shader.Find("BL23/MansionDecal");
            _screen = refs != null && refs.Screen != null ? refs.Screen : Shader.Find("BL23/MansionScreen");
            _particle = refs != null && refs.Particle != null ? refs.Particle : Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (_lit == null) { Debug.LogError("[Mansion] BL23/MansionLit shader missing"); _lit = Shader.Find("Universal Render Pipeline/Lit"); }
        }

        // ------------------------------------------------------------------ textures
        public static Texture2D Tex(string id, string map)
        {
            string key = id + "_" + map;
            if (_tex.TryGetValue(key, out var t)) return t;
            t = Resources.Load<Texture2D>("Mansion/Tex/" + key);
            _tex[key] = t;
            return t;
        }

        public static Texture2D Proc(string name)
        {
            if (_tex.TryGetValue("proc_" + name, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>("Mansion/Proc/" + name);
            if (t == null)
            {
                switch (name)
                {
                    case "PaperDamask": t = ProcTex.Wallpaper(ProcTex.Paper.Damask, 256); break;
                    case "PaperDamask2": t = ProcTex.Wallpaper(ProcTex.Paper.Damask2, 256); break;
                    case "PaperStripe": t = ProcTex.Wallpaper(ProcTex.Paper.Stripe, 256); break;
                    case "PaperMoon": t = ProcTex.Wallpaper(ProcTex.Paper.Moonflower, 256); break;
                    case "PaperFlesh": t = ProcTex.Wallpaper(ProcTex.Paper.FleshVein, 256); break;
                    case "DecalAtlas": t = ProcTex.DecalAtlas(512); break;
                    case "Portraits": t = ProcTex.Portraits(512); break;
                    case "WindowCookie": t = ProcTex.WindowCookie(128); break;
                    case "SoftDot": t = ProcTex.SoftDot(64); break;
                    case "RainStreak": t = ProcTex.RainStreak(16, 128); break;
                    case "PersianRug0": t = ProcTex.PersianRug(512, 768, 0); break;
                    case "PersianRug1": t = ProcTex.PersianRug(512, 768, 1); break;
                    case "PersianRug2": t = ProcTex.PersianRug(512, 768, 2); break;
                    case "PersianRug3": t = ProcTex.PersianRug(512, 768, 3); break;
                    case "StainedCookie": t = ProcTex.StainedCookie(128, new Color(1, 0.2f, 0.7f), new Color(0.2f, 0.9f, 1f), new Color(0.5f, 0.2f, 1f), new Color(1f, 0.75f, 0.2f)); break;
                }
            }
            if (t != null && (name == "WindowCookie" || name == "StainedCookie")) t.wrapMode = TextureWrapMode.Clamp;
            _tex["proc_" + name] = t;
            return t;
        }

        // ------------------------------------------------------------------ material factory helpers
        public static Material NewLit(string name, string texId, float metersPerTile, float rough, float metal = 0f, bool normal = true, bool mask = true, float normalScale = 1f)
        {
            Init();
            var m = new Material(_lit) { name = name, enableInstancing = false };
            var d = texId != null ? Tex(texId, "diff") : null;
            if (d != null) m.SetTexture("_BaseMap", d);
            float k = metersPerTile > 0 ? 1f / metersPerTile : 1f;
            m.SetVector("_Tiling", new Vector4(k, k, 0, 0));
            var n = normal && texId != null ? Tex(texId, "nor") : null;
            if (n != null) { m.SetTexture("_NormalMap", n); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_NormalScale", normalScale); }
            var a = mask && texId != null ? Tex(texId, "arm") : null;
            if (a != null)
            {
                m.SetTexture("_MaskMap", a); m.EnableKeyword("_MASKMAP");
                m.SetFloat("_Roughness", rough / 0.7f); m.SetFloat("_RoughnessBias", 0); m.SetFloat("_Metallic", 0); m.SetFloat("_MetallicBias", metal);
            }
            else { m.SetFloat("_Roughness", rough); m.SetFloat("_Metallic", metal); }
            return m;
        }

        static Material Glow(string name, string mode, bool additive, int queueOffset = 0)
        {
            Init();
            var m = new Material(_glow) { name = name };
            foreach (var kw in new[] { "_MODE_SOLID", "_MODE_ADD", "_MODE_FLAME", "_MODE_RAY", "_MODE_HALO" }) m.DisableKeyword(kw);
            m.EnableKeyword("_MODE_" + mode);
            m.SetFloat("_Mode", mode == "SOLID" ? 0 : mode == "ADD" ? 1 : mode == "FLAME" ? 2 : mode == "RAY" ? 3 : 4);
            if (additive)
            {
                m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.One); m.SetFloat("_ZWrite", 0);
                m.renderQueue = (int)RenderQueue.Transparent + queueOffset;
                m.SetOverrideTag("RenderType", "Transparent");
            }
            else { m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.Zero); m.SetFloat("_ZWrite", 1); m.renderQueue = (int)RenderQueue.Geometry; }
            return m;
        }

        public static Material Get(int slot)
        {
            if (_slot.TryGetValue(slot, out var m) && m != null) return m;
            Init();
            m = Create(slot);
            m.name = "Mansion_" + slot;
            _slot[slot] = m;
            return m;
        }

        static void Wallpaper(Material m, string pattern, float eyeDensity, float patTiling = 1f)
        {
            m.EnableKeyword("_WALLPAPER");
            m.SetTexture("_PatternMap", Proc(pattern));
            m.SetFloat("_PatternTiling", patTiling);
            m.SetFloat("_EyeDensity", eyeDensity);
            m.SetFloat("_EyeScale", 3.0f);
            m.SetFloat("_Grime", 0.35f);
        }

        static Material Create(int slot)
        {
            Material m;
            switch (slot)
            {
                // ----- wallpapers: base plaster, pattern mask over it (tile ~0.62 m)
                case S.PaperDamask: m = NewLit("PaperDamask", "painted_plaster_wall", 1.6f, 0.75f, 0, true, false, 0.4f); Wallpaper(m, "PaperDamask", 0.05f, 3.2f); return m;
                case S.PaperDamask2: m = NewLit("PaperDamask2", "painted_plaster_wall", 1.6f, 0.75f, 0, true, false, 0.4f); Wallpaper(m, "PaperDamask2", 0.045f, 2.8f); return m;
                case S.PaperEyes: m = NewLit("PaperEyes", "painted_plaster_wall", 1.6f, 0.7f, 0, true, false, 0.4f); Wallpaper(m, "PaperDamask", 0.22f, 3.2f); return m;
                case S.PaperStripe: m = NewLit("PaperStripe", "painted_plaster_wall", 1.6f, 0.7f, 0, true, false, 0.4f); Wallpaper(m, "PaperStripe", 0.02f, 2.0f); return m;
                case S.PaperMoon: m = NewLit("PaperMoon", "painted_plaster_wall", 1.6f, 0.75f, 0, true, false, 0.4f); Wallpaper(m, "PaperMoon", 0.02f, 2.8f); return m;
                case S.PaperFlesh: m = NewLit("PaperFlesh", "painted_plaster_wall", 1.6f, 0.45f, 0, true, false, 0.6f); Wallpaper(m, "PaperFlesh", 0.08f, 1.0f); return m;
                // ----- woods
                // the scanned wood maps are very dark (sRGB means 30-52): lifted so furniture reads as dark wood under
                // candlelight instead of a black silhouette (target linear albedo ~0.05-0.1)
                case S.Wainscot: m = NewLit("Wainscot", "dark_wood", 1.2f, 0.42f); m.SetColor("_BaseColor", new Color(1.9f, 1.85f, 1.9f)); return m;
                case S.WoodDark: m = NewLit("WoodDark", "dark_wood", 1.0f, 0.45f); m.SetColor("_BaseColor", new Color(2.1f, 2.05f, 2.1f)); return m;
                case S.WoodLight: m = NewLit("WoodLight", "wood_table_001", 1.0f, 0.5f); m.SetColor("_BaseColor", new Color(3.6f, 3.4f, 3.2f)); return m;
                case S.WoodCherry: m = NewLit("WoodCherry", "lacquered_cherry_wood", 1.0f, 0.38f); m.SetColor("_BaseColor", new Color(3.0f, 2.85f, 2.9f)); return m;
                case S.WoodWorn: return NewLit("WoodWorn", "wood_floor_worn", 1.5f, 0.7f);
                case S.WoodPainted: m = NewLit("WoodPainted", "wood_table_001", 1.0f, 0.4f, 0, true, false, 0.5f); m.SetTexture("_BaseMap", Texture2D.whiteTexture); return m;
                // ----- metals
                case S.Gold: m = NewLit("Gold", null, 1, 0.46f, 1f); m.SetColor("_BaseColor", new Color(0.8f, 0.62f, 0.34f)); return m;   // aged gilding: soft sheen, no glints
                case S.Brass: m = NewLit("Brass", "rusty_metal_02", 1.0f, 0.5f, 1f, true, false, 0.25f); m.SetTexture("_BaseMap", Texture2D.whiteTexture); m.SetColor("_BaseColor", new Color(0.72f, 0.55f, 0.32f)); return m;
                case S.Iron: m = NewLit("Iron", "metal_plate", 1.0f, 0.55f, 0.9f); m.SetColor("_BaseColor", new Color(0.35f, 0.34f, 0.36f)); return m;
                case S.Steel: m = NewLit("Steel", "metal_plate", 1.0f, 0.48f, 1f); m.SetColor("_BaseColor", new Color(0.75f, 0.76f, 0.78f)); return m;
                case S.Chrome: m = NewLit("Chrome", null, 1, 0.24f, 1f); m.SetColor("_BaseColor", new Color(0.72f, 0.72f, 0.74f)); return m;   // tarnished nickel
                case S.Copper: m = NewLit("Copper", "rusty_metal_02", 1.0f, 0.62f, 1f, true, false, 0.35f); m.SetTexture("_BaseMap", Texture2D.whiteTexture); m.SetColor("_BaseColor", new Color(0.62f, 0.36f, 0.25f)); return m;
                case S.RustyMetal: return NewLit("RustyMetal", "rusty_metal_02", 1.2f, 0.7f, 0.4f);
                case S.GreenRust: return NewLit("GreenRust", "green_metal_rust", 1.5f, 0.65f, 0.3f);
                case S.PaintedMetal: m = NewLit("PaintedMetal", "painted_metal_shutter", 1.5f, 0.45f, 0.2f); return m;
                case S.MetalPlate: return NewLit("MetalPlate", "metal_plate", 1.2f, 0.5f, 0.8f);
                // ----- floors
                case S.FloorChecker: m = NewLit("FloorChecker", "marble_01", 1.6f, 0.2f, 0, true, true, 0.5f); m.EnableKeyword("_CHECKER"); m.SetFloat("_CheckerSize", 0.8f); m.EnableKeyword("_PLANAR"); m.SetFloat("_PlanarStrength", 1f); return m;
                case S.FloorCheckerTile: m = NewLit("FloorCheckerTile", "floor_tiles_06", 1.2f, 0.3f, 0, true, true, 0.4f); m.EnableKeyword("_CHECKER"); m.SetFloat("_CheckerSize", 0.3f); m.EnableKeyword("_PLANAR"); m.SetFloat("_PlanarStrength", 0.6f); return m;
                case S.MarbleFloor: m = NewLit("MarbleFloor", "marble_01", 2.0f, 0.22f); m.EnableKeyword("_PLANAR"); m.SetFloat("_PlanarStrength", 1f); return m;
                case S.Parquet: m = NewLit("Parquet", "diagonal_parquet", 2.0f, 0.38f); m.EnableKeyword("_PLANAR"); m.SetFloat("_PlanarStrength", 0.45f); return m;
                case S.Herringbone: m = NewLit("Herringbone", "herringbone_parquet", 1.6f, 0.36f); m.EnableKeyword("_PLANAR"); m.SetFloat("_PlanarStrength", 0.45f); return m;
                case S.Carpet: m = NewLit("Carpet", "dirty_carpet", 1.5f, 0.95f); m.SetFloat("_Recolor", 0.85f); return m;
                case S.CarpetJacquard: m = NewLit("CarpetJacquard", "quatrefoil_jacquard_fabric", 0.8f, 0.9f); m.SetFloat("_Recolor", 0.8f); return m;
                case S.StoneFloor: m = NewLit("StoneFloor", "monastery_stone_floor", 2.5f, 0.6f); m.EnableKeyword("_PLANAR"); m.SetFloat("_PlanarStrength", 0.3f); return m;
                case S.WetStone: m = NewLit("WetStone", "monastery_stone_floor", 2.5f, 0.6f); m.SetFloat("_Wet", 0.85f); m.EnableKeyword("_PLANAR"); m.SetFloat("_PlanarStrength", 1f); return m;
                case S.Concrete: return NewLit("Concrete", "concrete_floor_worn_001", 3f, 0.8f);
                case S.Mosaic: m = NewLit("Mosaic", "old_mosaic_floor", 2f, 0.55f); return m;
                case S.Terrazzo: m = NewLit("Terrazzo", "terrazzo_tiles", 1.5f, 0.25f); m.EnableKeyword("_PLANAR"); m.SetFloat("_PlanarStrength", 0.6f); return m;
                case S.FloorTile: m = NewLit("FloorTile", "floor_tiles_06", 1.6f, 0.3f); m.EnableKeyword("_PLANAR"); m.SetFloat("_PlanarStrength", 0.5f); return m;
                case S.PoolTile: m = NewLit("PoolTile", "square_tiles_02", 1.0f, 0.2f); return m;
                case S.Soil: return NewLit("Soil", "forest_ground_04", 2f, 0.9f);
                case S.BrickFloor: return NewLit("BrickFloor", "castle_brick_07", 2f, 0.75f);
                case S.WornTile: return NewLit("WornTile", "worn_tile_floor", 2f, 0.5f);
                // ----- walls / ceilings
                case S.Plaster: return NewLit("Plaster", "plastered_wall_02", 3f, 0.85f, 0, true, false, 0.6f);
                case S.Ceiling: return NewLit("Ceiling", "painted_plaster_wall", 2.5f, 0.9f, 0, true, false, 0.5f);
                case S.PlasterWhite: m = NewLit("PlasterWhite", "white_plaster_02", 2.5f, 0.8f, 0, true, false, 0.3f); return m;
                case S.Brick: return NewLit("Brick", "castle_brick_07", 2.4f, 0.8f);
                case S.WallTile: m = NewLit("WallTile", "long_white_tiles", 1.2f, 0.3f); return m;
                case S.StoneWall: return NewLit("StoneWall", "stone_wall", 3f, 0.8f);
                case S.ConcreteWall: return NewLit("ConcreteWall", "concrete_floor_worn_001", 3f, 0.85f);
                // ----- fabrics
                case S.Velvet: m = NewLit("Velvet", "velour_velvet", 0.6f, 0.9f); m.SetFloat("_Recolor", 0.92f); return m;
                case S.Jacquard: m = NewLit("Jacquard", "floral_jacquard", 0.6f, 0.85f); m.SetFloat("_Recolor", 0.7f); return m;
                case S.Quatrefoil: m = NewLit("Quatrefoil", "quatrefoil_jacquard_fabric", 0.5f, 0.85f); m.SetFloat("_Recolor", 0.75f); return m;
                case S.Linen: m = NewLit("Linen", "rough_linen", 0.8f, 0.9f); m.SetFloat("_Recolor", 0.6f); return m;
                case S.Cloth: m = NewLit("Cloth", "rough_linen", 0.8f, 0.9f); m.SetFloat("_Recolor", 0.95f); return m;
                case S.Leather: m = NewLit("Leather", "brown_leather", 0.8f, 0.5f); m.SetFloat("_Recolor", 0.5f); m.SetColor("_BaseColor", new Color(2.4f, 2.3f, 2.2f)); return m;
                case S.LeatherRed: m = NewLit("LeatherRed", "leather_red_02", 0.8f, 0.45f); return m;
                case S.Felt: m = NewLit("Felt", "velour_velvet", 0.5f, 0.95f, 0, true, true, 0.3f); m.SetFloat("_Recolor", 1f); return m;
                // ----- misc solids
                case S.Marble: m = NewLit("Marble", "marble_01", 1.2f, 0.28f); return m;
                case S.MarbleDark: m = NewLit("MarbleDark", "marble_01", 1.2f, 0.24f); m.EnableKeyword("_CHECKER"); m.SetFloat("_CheckerSize", 1000f); m.SetFloat("_CheckerPhase", 1); return m;
                case S.Porcelain: m = NewLit("Porcelain", null, 1, 0.3f); m.SetColor("_BaseColor", new Color(0.95f, 0.93f, 0.9f)); return m;
                case S.Ceramic: m = NewLit("Ceramic", null, 1, 0.36f); return m;
                case S.GlossPaint: m = NewLit("GlossPaint", null, 1, 0.34f); return m;
                case S.Flesh: m = NewLit("Flesh", "painted_plaster_wall", 0.8f, 0.3f, 0, true, false, 1.2f); Wallpaper(m, "PaperFlesh", 0, 0.8f); m.SetFloat("_Grime", 0.1f); return m;
                case S.Bone: m = NewLit("Bone", null, 1, 0.42f); m.SetColor("_BaseColor", new Color(0.93f, 0.89f, 0.78f)); return m;
                case S.Paper: m = NewLit("Paper", null, 1, 0.85f); m.SetColor("_BaseColor", new Color(0.92f, 0.9f, 0.84f)); return m;
                case S.Plastic: m = NewLit("Plastic", null, 1, 0.35f); return m;
                case S.Rubber: m = NewLit("Rubber", null, 1, 0.8f); m.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.12f)); return m;
                case S.Leaf: m = NewLit("Leaf", null, 1, 0.5f); m.SetColor("_BaseColor", new Color(0.3f, 0.55f, 0.25f)); m.SetFloat("_Cull", 0); return m;
                case S.Wax: m = NewLit("Wax", null, 1, 0.35f); m.SetColor("_BaseColor", new Color(0.95f, 0.92f, 0.82f)); m.SetColor("_EmissionColor", new Color(0.12f, 0.06f, 0.02f)); m.SetFloat("_EmissionCircuit", -2); return m;
                case S.Books: m = NewLit("Books", "brown_leather", 0.5f, 0.55f, 0, true, false); m.SetFloat("_Recolor", 0.85f); return m;
                case S.Painting: m = NewLit("Painting", null, 1, 0.55f); m.SetTexture("_BaseMap", Proc("Portraits")); m.SetVector("_Tiling", new Vector4(1, 1, 0, 0)); return m;
                case S.Mirror: m = NewLit("Mirror", null, 1, 0.1f, 1f); m.SetColor("_BaseColor", new Color(0.44f, 0.45f, 0.47f)); m.EnableKeyword("_PLANAR"); return m;   // old silvering: dim, a little foxed
                case S.Clay: m = NewLit("Clay", null, 1, 0.8f); m.SetColor("_BaseColor", new Color(0.62f, 0.33f, 0.2f)); return m;
                case S.Obsidian: m = NewLit("Obsidian", "marble_01", 1.2f, 0.16f); m.EnableKeyword("_CHECKER"); m.SetFloat("_CheckerSize", 1000f); m.SetFloat("_CheckerPhase", 1); m.EnableKeyword("_PLANAR"); m.SetFloat("_PlanarStrength", 1f); return m;
                // ----- emissive / special
                case S.Glow: return Glow("Glow", "SOLID", false);
                case S.GlowAdd: m = Glow("GlowAdd", "ADD", true, 10); m.SetTexture("_MainTex", Proc("SoftDot")); return m;
                case S.Halo: m = Glow("Halo", "HALO", true, 20); m.SetFloat("_Soft", 1.2f); m.SetFloat("_DepthFade", 0.3f); return m;
                case S.Flame: m = Glow("Flame", "FLAME", true, 30); m.SetFloat("_Group", -2); m.SetFloat("_Cull", 0); return m;
                case S.Ray: m = Glow("Ray", "RAY", true, 5); m.SetFloat("_Soft", 2.2f); m.SetFloat("_DepthFade", 1.2f); m.SetFloat("_Cull", 0); return m;
                case S.Window:
                    m = new Material(_glass) { name = "Window" }; m.EnableKeyword("_KIND_WINDOW"); m.DisableKeyword("_KIND_STAINED"); m.SetFloat("_Intensity", 1.7f); return m;
                case S.Stained:
                    m = new Material(_glass) { name = "Stained" }; m.EnableKeyword("_KIND_STAINED"); m.DisableKeyword("_KIND_WINDOW"); m.SetFloat("_Kind", 1); m.SetFloat("_Intensity", 1.55f); m.SetColor("_ColorC", new Color(0.62f, 0.4f, 0.14f)); m.SetColor("_ColorD", new Color(0.14f, 0.3f, 0.32f)); return m;
                case S.Sky: m = new Material(_sky) { name = "Sky" }; return m;
                case S.Water:
                    m = new Material(_water) { name = "Water" }; return m;
                case S.ShallowWater:
                    m = new Material(_water) { name = "ShallowWater" };
                    m.SetFloat("_Mirror", 0.75f); m.SetFloat("_Absorb", 0.6f); m.SetFloat("_Waves", 0.12f); m.SetFloat("_WaveScale", 2.2f); m.SetFloat("_Speed", 0.15f);
                    m.SetColor("_Shallow", new Color(0.8f, 0.95f, 1f)); m.SetColor("_Deep", new Color(0.1f, 0.2f, 0.3f)); return m;
                case S.Glass:
                    m = new Material(_water) { name = "Glass" };
                    m.SetFloat("_Waves", 0.0f); m.SetFloat("_Absorb", 0.08f); m.SetFloat("_Refract", 0.01f); m.SetFloat("_Reflect", 0.26f); m.SetFloat("_Smooth", 0.78f); m.SetFloat("_EnvRough", 0.2f);
                    m.SetColor("_Shallow", new Color(0.9f, 0.97f, 1f)); m.SetColor("_Deep", new Color(0.5f, 0.6f, 0.65f)); return m;
                case S.Aquarium:
                    m = new Material(_water) { name = "Aquarium" };
                    m.SetFloat("_Waves", 0.05f); m.SetFloat("_Absorb", 0.35f); m.SetFloat("_Refract", 0.03f); m.SetFloat("_Reflect", 0.45f); m.SetFloat("_Smooth", 0.84f);
                    m.SetColor("_Shallow", new Color(0.35f, 0.95f, 0.9f)); m.SetColor("_Deep", new Color(0.02f, 0.2f, 0.25f)); return m;
                case S.Caustic:
                    m = new Material(_water) { name = "Caustic" }; m.EnableKeyword("_CAUSTICS"); m.SetFloat("_UseCaustics", 1); m.renderQueue = (int)RenderQueue.Transparent - 40; return m;
                case S.Screen: m = new Material(_screen) { name = "Screen" }; return m;
                case S.Clock: m = new Material(_screen) { name = "Clock" }; return m;
                case S.Decal: m = new Material(_decal) { name = "Decal" }; m.SetTexture("_MainTex", Proc("DecalAtlas")); return m;
                case S.RugA: case S.RugB: case S.RugC: case S.RugD:
                    m = NewLit("Rug" + (slot - S.RugA), null, 1, 0.95f); m.SetTexture("_BaseMap", Proc("PersianRug" + (slot - S.RugA))); m.SetVector("_Tiling", new Vector4(1, 1, 0, 0)); m.SetFloat("_Occlusion", 0); return m;
            }
            return NewLit("Default" + slot, null, 1, 0.6f);
        }

        /// <summary>Named one-off materials (e.g. particles).</summary>
        public static Material Particle(string name, Texture tex, bool additive)
        {
            if (_named.TryGetValue(name, out var m) && m != null) return m;
            Init();
            m = new Material(_particle) { name = name };
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", additive ? 2 : 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)(additive ? BlendMode.SrcAlpha : BlendMode.SrcAlpha));
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            _named[name] = m;
            return m;
        }

        /// <summary>Material for a scanned model part, cached by model+part+tint.</summary>
        public static Material ModelPart(MansionModel model, int part, Color? tint, float recolor = -1f)
        {
            var p = model.Parts[part];
            string key = model.name + "#" + part + "#" + (tint.HasValue ? ColorUtility.ToHtmlStringRGB(tint.Value) : "-") + "#" + recolor.ToString("0.00");
            if (_named.TryGetValue(key, out var m) && m != null) return m;
            Init();
            m = new Material(_lit) { name = key };
            if (p.BaseMap != null) m.SetTexture("_BaseMap", p.BaseMap);
            m.SetVector("_Tiling", new Vector4(1, 1, 0, 0));
            if (p.NormalMap != null) { m.SetTexture("_NormalMap", p.NormalMap); m.EnableKeyword("_NORMALMAP"); }
            if (p.MaskMap != null)
            {
                m.SetTexture("_MaskMap", p.MaskMap); m.EnableKeyword("_MASKMAP");
                m.SetFloat("_Roughness", p.Roughness); m.SetFloat("_Metallic", p.Metallic); m.SetFloat("_RoughnessBias", 0); m.SetFloat("_MetallicBias", 0);
            }
            else { m.SetFloat("_Roughness", p.Roughness); m.SetFloat("_Metallic", p.Metallic); }
            Color bc = p.BaseColor;
            // scans whose albedo is near black (lacquer, black leather) vanish into a silhouette under candlelight: lift them
            float lift = DarkLift(model.name, p.Name);
            if (lift != 1f) bc = new Color(bc.r * lift, bc.g * lift, bc.b * lift, bc.a);
            float rc = recolor >= 0 ? recolor : (p.Recolor ? 0.8f : 0f);
            if (tint.HasValue)
            {
                if (rc > 0) { m.SetFloat("_Recolor", rc); bc = tint.Value; }
                else bc = new Color(bc.r * Mathf.Lerp(1, tint.Value.r * 1.6f, 0.35f), bc.g * Mathf.Lerp(1, tint.Value.g * 1.6f, 0.35f), bc.b * Mathf.Lerp(1, tint.Value.b * 1.6f, 0.35f), bc.a);
            }
            m.SetColor("_BaseColor", bc);
            if (p.AlphaClip) { m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cutoff", 0.5f); }
            if (p.DoubleSided) m.SetFloat("_Cull", 0);
            _named[key] = m;
            return m;
        }

        /// <summary>Linear albedo multiplier for scanned models whose base map averages below ~30/255 (measured offline).</summary>
        static float DarkLift(string model, string part)
        {
            if (part != null && (part.Contains("glass") || part.Contains("flame"))) return 1f;
            switch (model)
            {
                case "ClassicConsole_01": return 4.5f;
                case "sofa_02": return 3.5f;
                case "ClassicNightstand_01": return 3f;
                case "gothic_coffee_table": return 2.6f;
                case "vintage_cabinet_01": case "side_table_tall_01": return 2.2f;
                case "GothicCommode_01": case "GothicCabinet_01": case "round_wooden_table_01": return 1.7f;
            }
            return 1f;
        }

        public static Material[] Materials(int[] slots)
        {
            var arr = new Material[slots.Length];
            for (int i = 0; i < slots.Length; i++) arr[i] = Get(slots[i]);
            return arr;
        }

        /// <summary>Glow vertex custom data: intensity, flicker, extra, power group (-1 always, -2 fire, 0..7 circuit).</summary>
        public static Vector4 GlowData(float intensity, float flicker, float extra, int group) => new Vector4(intensity, flicker, extra, group + 10);
    }
}
