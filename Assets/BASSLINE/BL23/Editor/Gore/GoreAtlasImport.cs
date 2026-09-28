using UnityEditor;
using UnityEngine;

namespace BL23.EditorTools.Gore
{
    /// <summary>Resources/Gore textures (the baked blood atlas: A = shape, R = height) import linear, mipmapped, trilinear,
    /// aniso 8, clamped, BC7 (CompressedHQ).</summary>
    public sealed class GoreAtlasImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/BL23/Resources/Gore/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.sRGBTexture = false; ti.mipmapEnabled = true; ti.alphaIsTransparency = false; ti.isReadable = false;
            ti.filterMode = FilterMode.Trilinear; ti.anisoLevel = 8; ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.maxTextureSize = 2048;
            var st = ti.GetPlatformTextureSettings("Standalone");
            st.overridden = true; st.format = TextureImporterFormat.BC7; st.maxTextureSize = 2048; ti.SetPlatformTextureSettings(st);
        }
    }
}
