using System.IO;
using UnityEditor;
using UnityEngine;

namespace BL23.EditorTools.Audio
{
    /// <summary>
    /// Import defaults for BL23 audio:
    ///  - Resources/Music/*: Vorbis q0.55, Streaming, no preload, load in background (≈2.7 h of music stays cheap in RAM).
    ///    Sample-synchronous stems (MusicLibrary.NeedsInMemory, e.g. murder + murder_kick_ver) use CompressedInMemory so
    ///    PlayScheduled starts them on the same sample.
    ///  - any other audio under Assets/BASSLINE/BL23: short SFX defaults (Decompress On Load / ADPCM, mono for 3D),
    ///    longer files CompressedInMemory Vorbis.
    /// Only runs when a file is (re)imported; manual overrides made later in the inspector are kept until reimport.
    /// </summary>
    public sealed class BL23AudioImportRules : AssetPostprocessor
    {
        const string Root = "Assets/BASSLINE/BL23/";
        const string MusicDir = "Assets/BASSLINE/BL23/Resources/Music/";
        const string AmbienceDir = "Assets/BASSLINE/BL23/Resources/Ambience/";
        const string SfxDir = "Assets/BASSLINE/BL23/Resources/Sfx/";

        /// <summary>Ids flagged as loops in Resources/Sfx/_manifest.txt (written by BL23Lab/AudioLab/Forge).</summary>
        static System.Collections.Generic.HashSet<string> SfxLoopIds()
        {
            var set = new System.Collections.Generic.HashSet<string>();
            try
            {
                foreach (var raw in File.ReadAllLines(SfxDir + "_manifest.txt"))
                {
                    var p = raw.Trim().Split('|');
                    if (p.Length >= 7 && !raw.StartsWith("#") && p[6].Trim() == "1") set.Add(p[0]);
                }
            }
            catch { }
            return set;
        }

        void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Root)) return;
            var imp = (AudioImporter)assetImporter;
            var s = imp.defaultSampleSettings;

            // ---- BEGIN physical pack (audio track, 2026-09-27) ----
            // Forge `phys` writes the physical / violence pack (Resources/Sfx, manifest _manifest_phys.txt) as dual-mono Vorbis
            // with its own metas (userData bl23phys-*, normalize: 0). Unity downmixes to mono without normalising (the pack is
            // loudness-balanced offline); load type comes from the meta: short non-vocal one-shots Decompress On Load, voices,
            // longer clips and loops Vorbis in memory.
            if (path.StartsWith(SfxDir) && (imp.userData ?? "").StartsWith("bl23phys"))
            {
                imp.forceToMono = true;
                imp.loadInBackground = true;
                if (s.loadType == AudioClipLoadType.Streaming) s.loadType = AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.45f;
                s.preloadAudioData = true;
                s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                imp.defaultSampleSettings = s;
                return;
            }
            // ---- END physical pack ----

            if (path.StartsWith(MusicDir))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                bool synced = BL23.Game.Audio.MusicLibrary.NeedsInMemory(name);
                imp.forceToMono = false;
                imp.loadInBackground = true;
                s.loadType = synced ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.55f;
                s.preloadAudioData = false;
                s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            }
            else if (path.StartsWith(AmbienceDir))
            {
                // room beds: stereo, looped, several at once -> ADPCM in memory (decoding costs almost nothing, noise-like beds hide its grain)
                imp.forceToMono = false;
                imp.loadInBackground = true;
                s.loadType = AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = AudioCompressionFormat.ADPCM;
                s.quality = 0.5f;
                s.preloadAudioData = false;
                s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            }
            else if (path.StartsWith(SfxDir))
            {
                // recorded effects (Forge writes .wav for clips up to 1.5 s, dual-mono .ogg for longer ones and loops):
                //  - tiny clips (< 40 KB: UI ticks, clicks) stay PCM, decompressed on load (crisp, no decode cost)
                //  - other short clips ADPCM in memory (3.5:1, near-zero decode cost)
                //  - long clips and loops Vorbis q0.5 in memory (the identical channels of a dual-mono file cost almost nothing)
                // forceToMono stays off: it would also peak-normalise and undo the loudness balance of the manifest
                long bytes = 0;
                try { bytes = new FileInfo(path).Length; } catch { }
                string stem = Path.GetFileNameWithoutExtension(path); int u = stem.LastIndexOf('_');
                string id = u > 0 ? stem.Substring(0, u) : stem;
                bool loop = SfxLoopIds().Contains(id);
                bool longClip = loop || path.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase) || bytes > 300 * 1024;
                bool tiny = !longClip && bytes > 0 && bytes < 40 * 1024;
                imp.forceToMono = false;
                imp.loadInBackground = true;   // data loads off the main thread (preloaded at startup by Sfx)
                s.loadType = tiny ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = longClip ? AudioCompressionFormat.Vorbis : tiny ? AudioCompressionFormat.PCM : AudioCompressionFormat.ADPCM;
                s.quality = 0.5f;
                s.preloadAudioData = !longClip;
                s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            }
            else
            {
                long bytes = 0;
                try { bytes = new FileInfo(path).Length; } catch { }
                bool small = bytes > 0 && bytes < 400 * 1024;
                imp.forceToMono = true;
                imp.loadInBackground = !small;
                s.loadType = small ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = small ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                s.preloadAudioData = small;
                s.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            }
            imp.defaultSampleSettings = s;
        }
    }

    /// <summary>Menu helper to force the rules onto already-imported music.</summary>
    public static class BL23AudioMenu
    {
        [MenuItem("BASSLINE/BL23/Audio/Reimport Music With BL23 Rules")]
        static void ReimportMusic()
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/BASSLINE/BL23/Resources/Music" });
            AssetDatabase.StartAssetEditing();
            try { foreach (var g in guids) AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate); }
            finally { AssetDatabase.StopAssetEditing(); }
            Debug.Log($"[BL23 Audio] reimported {guids.Length} music clips");
        }
    }
}
