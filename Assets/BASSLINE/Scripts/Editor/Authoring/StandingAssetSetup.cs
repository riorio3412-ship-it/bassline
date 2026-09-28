using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace BASSLINE.Authoring
{
    public sealed class StandingAssetSetup:IPreprocessBuildWithReport
    {
        const string Folder="Assets/BASSLINE/Characters/Standing/Resources/BASSLINE/Standing";
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report)=>Import();
        public static void Import()
        {
            if(!Directory.Exists(Folder))return;
            var receipt=new System.Collections.Generic.List<string>();
            foreach(string path in Directory.GetFiles(Folder,"*.png").OrderBy(x=>x,StringComparer.Ordinal)){
                string asset=path.Replace('\\','/');AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);
                var importer=AssetImporter.GetAtPath(asset) as TextureImporter;
                if(importer==null)throw new InvalidOperationException("Standing texture importer unavailable: "+asset);
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
                importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
                importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;
                // The standalone visual probe checks source alpha; do not discard it during import.
                importer.isReadable=true;importer.SaveAndReimport();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(asset);
                if(!texture||texture.width<256||texture.height<256)throw new InvalidOperationException("Standing texture is invalid: "+asset);
                receipt.Add(asset+" | "+AssetDatabase.AssetPathToGUID(asset)+" | "+texture.width+"x"+texture.height);
            }
            Directory.CreateDirectory("Verification");File.WriteAllLines("Verification/standing-import.txt",receipt);AssetDatabase.SaveAssets();
        }
    }
}
