using System;
using System.IO;
using System.Linq;
using BL23.Game.Mansion;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BL23.EditorTools.Mansion
{
    /// <summary>
    /// One-shot project setup for the mansion: Forward+ renderer with SSAO, depth/opaque textures, shadow budget,
    /// shader reference asset (build inclusion) and baked procedural textures.
    /// Batch: Unity -batchmode -executeMethod BL23.EditorTools.Mansion.MansionSetup.RunAll -quit
    /// </summary>
    public static class MansionSetup
    {
        public const string RendererPath = "Assets/BASSLINE/Environment/Rendering/RD_BASSLINE_Forward.asset";
        public const string PipelinePath = "Assets/BASSLINE/Environment/Rendering/RP_BASSLINE_URP.asset";
        public const string Root = "Assets/BASSLINE/BL23";
        public const string ProcDir = Root + "/Resources/Mansion/Proc";

        [MenuItem("BL23/Mansion/Setup (renderer, shaders, baked textures)")]
        public static void RunAll()
        {
            SetupRenderer();
            SetupPipeline();
            CreateShaderRefs();
            BakeProcTextures();
            AssetDatabase.SaveAssets();
            Debug.Log("[MansionSetup] done");
        }

        public static void SetupRenderer()
        {
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rd == null) { Debug.LogError("[MansionSetup] renderer data missing at " + RendererPath); return; }
            var so = new SerializedObject(rd);
            so.FindProperty("m_RenderingMode").intValue = (int)RenderingMode.ForwardPlus;
            var dp = so.FindProperty("m_DepthPrimingMode"); if (dp != null) dp.intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            // SSAO renderer feature (grounds furniture, deepens corners)
            if (!rd.rendererFeatures.Any(f => f is ScreenSpaceAmbientOcclusion))
            {
                var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "SSAO";
                AssetDatabase.AddObjectToAsset(ssao, rd);
                rd.rendererFeatures.Add(ssao);
                var fso = new SerializedObject(rd);
                var map = fso.FindProperty("m_RendererFeatureMap");
                if (map != null)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string _, out long localId);
                    map.arraySize = rd.rendererFeatures.Count;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    fso.ApplyModifiedPropertiesWithoutUndo();
                }
                var sso = new SerializedObject(ssao);
                var settings = sso.FindProperty("m_Settings");
                if (settings != null)
                {
                    void SetF(string n, float v) { var p = settings.FindPropertyRelative(n); if (p != null) p.floatValue = v; }
                    void SetI(string n, int v) { var p = settings.FindPropertyRelative(n); if (p != null) p.intValue = v; }
                    void SetB(string n, bool v) { var p = settings.FindPropertyRelative(n); if (p != null) p.boolValue = v; }
                    SetF("Intensity", 1.3f); SetF("Radius", 0.28f); SetF("DirectLightingStrength", 0.35f); SetF("Falloff", 60f);
                    SetI("Source", 1); SetI("NormalSamples", 1); SetB("Downsample", false); SetB("AfterOpaque", false);
                    SetI("AOMethod", 0); SetI("Samples", 1);
                    sso.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            rd.SetDirty();
            EditorUtility.SetDirty(rd);
            Debug.Log("[MansionSetup] renderer -> Forward+ with SSAO");
        }

        public static void SetupPipeline()
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (rp == null) { Debug.LogError("[MansionSetup] pipeline asset missing"); return; }
            var so = new SerializedObject(rp);
            void I(string n, int v) { var p = so.FindProperty(n); if (p != null) p.intValue = v; else Debug.LogWarning("missing " + n); }
            void F(string n, float v) { var p = so.FindProperty(n); if (p != null) p.floatValue = v; else Debug.LogWarning("missing " + n); }
            void B(string n, bool v) { var p = so.FindProperty(n); if (p != null) p.boolValue = v; else Debug.LogWarning("missing " + n); }
            B("m_RequireDepthTexture", true);
            B("m_RequireOpaqueTexture", true);
            B("m_SupportsHDR", true);
            I("m_MSAA", 4);
            F("m_ShadowDistance", 45f);
            I("m_MainLightShadowmapResolution", 2048);
            B("m_AdditionalLightShadowsSupported", true);
            I("m_AdditionalLightsShadowmapResolution", 4096);
            I("m_AdditionalLightsShadowResolutionTierLow", 512);
            I("m_AdditionalLightsShadowResolutionTierMedium", 1024);
            I("m_AdditionalLightsShadowResolutionTierHigh", 2048);
            B("m_SupportsLightCookies", true);
            I("m_AdditionalLightsCookieResolution", 2048);
            B("m_SoftShadowsSupported", true);
            I("m_SoftShadowQuality", 2);
            B("m_ReflectionProbeBoxProjection", true);
            I("m_ColorGradingMode", 1);          // HDR grading
            I("m_ColorGradingLutSize", 32);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);
            Debug.Log("[MansionSetup] pipeline: depth+opaque textures, HDR grading, shadow atlas 4096");
        }

        public static void CreateShaderRefs()
        {
            string path = Root + "/Resources/Mansion/MansionShaders.asset";
            var refs = AssetDatabase.LoadAssetAtPath<MansionShaderRefs>(path);
            bool create = refs == null;
            if (create) refs = ScriptableObject.CreateInstance<MansionShaderRefs>();
            refs.Lit = Shader.Find("BL23/MansionLit");
            refs.Glow = Shader.Find("BL23/MansionGlow");
            refs.Glass = Shader.Find("BL23/MansionGlass");
            refs.Sky = Shader.Find("BL23/MansionSky");
            refs.Water = Shader.Find("BL23/MansionWater");
            refs.Decal = Shader.Find("BL23/MansionDecal");
            refs.Screen = Shader.Find("BL23/MansionScreen");
            refs.Particle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (create) { Directory.CreateDirectory(Path.GetDirectoryName(path)); AssetDatabase.CreateAsset(refs, path); }
            EditorUtility.SetDirty(refs);
            Debug.Log($"[MansionSetup] shader refs: lit={refs.Lit != null} glow={refs.Glow != null} glass={refs.Glass != null} sky={refs.Sky != null} water={refs.Water != null} decal={refs.Decal != null} screen={refs.Screen != null}");
        }

        public static void BakeProcTextures()
        {
            Directory.CreateDirectory(ProcDir);
            void Save(Texture2D t, string name)
            {
                File.WriteAllBytes(Path.Combine(ProcDir, name + ".png"), t.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(t);
            }
            Save(ProcTex.Wallpaper(ProcTex.Paper.Damask, 512), "PaperDamask");
            Save(ProcTex.Wallpaper(ProcTex.Paper.Damask2, 512), "PaperDamask2");
            Save(ProcTex.Wallpaper(ProcTex.Paper.Stripe, 512), "PaperStripe");
            Save(ProcTex.Wallpaper(ProcTex.Paper.Moonflower, 512), "PaperMoon");
            Save(ProcTex.Wallpaper(ProcTex.Paper.FleshVein, 512), "PaperFlesh");
            Save(ProcTex.DecalAtlas(1024), "DecalAtlas");
            Save(ProcTex.Portraits(1024), "Portraits");
            Save(ProcTex.WindowCookie(256), "WindowCookie");
            Save(ProcTex.StainedCookie(256, new Color(1, 0.2f, 0.7f), new Color(0.2f, 0.9f, 1f), new Color(0.5f, 0.2f, 1f), new Color(1f, 0.75f, 0.2f)), "StainedCookie");
            Save(ProcTex.SoftDot(64), "SoftDot");
            Save(ProcTex.RainStreak(16, 128), "RainStreak");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[MansionSetup] baked procedural textures");
        }
    }

    /// <summary>Import settings for mansion textures (normal maps, linear masks, per-class size budget, cookies).</summary>
    public sealed class MansionTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!MansionTextureBudget.Applies(assetPath)) return;
            MansionTextureBudget.Configure((TextureImporter)assetImporter, assetPath);
        }
    }

    /// <summary>
    /// Texture budget for the mansion (build size): material diffuse 1k BC7, normals/masks 512 (BC5/BC1), scanned-model maps
    /// 1k/512/256 by how large the object is seen. Batch: -executeMethod BL23.EditorTools.Mansion.MansionTextureBudget.Apply
    /// </summary>
    public static class MansionTextureBudget
    {
        // scanned models seen large (hero furniture): diffuse 1k
        static readonly string[] Hero = { "GothicBed_01", "GothicCabinet_01", "GothicCommode_01", "ArmChair_01", "Sofa_01", "vintage_grandfather_clock_01", "gothic_statue", "gothic_coffee_table", "round_wooden_table_01", "Chandelier_01", "Chandelier_02", "Chandelier_03",
            "GreenChair_01", "sofa_02", "sofa_03", "WoodenChair_01", "vintage_cabinet_01", "ClassicConsole_01", "vintage_day_bed", "Rockingchair_01", "treasure_chest", "Ottoman_01" };
        // scanned models only ever seen small (table dressing, tiny props): 256
        static readonly string[] Small = { "chess_set", "rubber_duck_toy", "mantel_clock_01", "croissant", "bleach_bottle", "cassette_player", "Camera_01", "modified_thermos", "lantern_chandelier_01", "ornate_mirror_01",
            "brass_goblets", "brass_vase_03", "brass_vase_04", "wooden_candlestick", "food_apple_01", "jug_01", "standing_picture_frame_01", "vintage_microscope", "brass_pot_01", "pot_enamel_01", "dartboard" };

        public static bool Applies(string path) => path.Contains("/BL23/Resources/Mansion/") || path.Contains("/BL23/Art/Mansion/");

        enum Kind { Diffuse, Normal, Mask }

        static Kind KindOf(string n)
        {
            if (n.Contains("_nor")) return Kind.Normal;
            if (n.EndsWith("_arm") || n.Contains("_arm_") || n.Contains("_rough") || n.Contains("_metal") || n.Contains("_ao_") || n.EndsWith("_ao")) return Kind.Mask;
            return Kind.Diffuse;
        }

        static string ModelId(string path)
        {
            const string key = "/Art/Mansion/Models/";
            int i = path.IndexOf(key, StringComparison.Ordinal); if (i < 0) return null;
            var rest = path.Substring(i + key.Length); int s = rest.IndexOf('/');
            return s > 0 ? rest.Substring(0, s) : null;
        }

        public static void Configure(TextureImporter ti, string path)
        {
            string n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.isReadable = false;
            if (path.Contains("/Resources/Mansion/Proc/"))
            {
                bool linear = n.StartsWith("paper") || n == "decalatlas";
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = !linear;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = n == "softdot" || n == "rainstreak";
                if (n.Contains("cookie")) { ti.wrapMode = TextureWrapMode.Clamp; ti.maxTextureSize = 256; ti.textureCompression = TextureImporterCompression.Uncompressed; return; }
                ti.wrapMode = n == "decalatlas" || n == "portraits" || n == "softdot" || n == "rainstreak" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                ti.maxTextureSize = n.StartsWith("paper") ? 512 : n == "softdot" || n == "rainstreak" ? 128 : 1024;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;   // BC7
                return;
            }
            var kind = KindOf(n);
            string model = ModelId(path);
            int size;
            if (model == null)
            {
                // tiling material textures (Resources/Mansion/Tex): diffuse stays 1k, detail maps 512
                size = kind == Kind.Diffuse ? 1024 : 512;
            }
            else if (Array.IndexOf(Hero, model) >= 0) size = kind == Kind.Diffuse ? 1024 : 512;
            else if (Array.IndexOf(Small, model) >= 0) size = 256;
            else size = kind == Kind.Mask ? 256 : 512;
            ti.maxTextureSize = size;
            switch (kind)
            {
                case Kind.Normal:
                    ti.textureType = TextureImporterType.NormalMap; ti.sRGBTexture = false;
                    ti.textureCompression = TextureImporterCompression.CompressedHQ;   // BC5
                    break;
                case Kind.Mask:
                    ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false;
                    ti.textureCompression = TextureImporterCompression.Compressed;     // BC1
                    break;
                default:
                    ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true;
                    // opaque colour maps: BC1 (half the size of BC7, indistinguishable under candlelight); anything with an
                    // alpha channel in use (opacity, leaves) and the hero pieces keep BC7
                    bool alpha = n.Contains("opacity") || n.Contains("alpha") || n.Contains("leaf") || n.Contains("leaves") || ti.DoesSourceTextureHaveAlpha();
                    ti.textureCompression = alpha || (model != null && Array.IndexOf(Hero, model) >= 0) ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;
                    break;
            }
        }

        [MenuItem("BL23/Mansion/Apply texture budget")]
        public static void Apply()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/BASSLINE/BL23/Resources/Mansion", "Assets/BASSLINE/BL23/Art/Mansion" });
            int changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var g in guids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (ti == null) continue;
                    int before = ti.maxTextureSize; var bc = ti.textureCompression; var bt = ti.textureType;
                    Configure(ti, p);
                    if (before != ti.maxTextureSize || bc != ti.textureCompression || bt != ti.textureType) { EditorUtility.SetDirty(ti); ti.SaveAndReimport(); changed++; }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            long total = 0;
            foreach (var g in guids)
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g));
                if (t != null) total += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);
            }
            Debug.Log($"[MansionTextureBudget] {guids.Length} textures, reconfigured {changed}, runtime memory estimate {total / (1024f * 1024f):0.0} MB");
        }
    }
}
