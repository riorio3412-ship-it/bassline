using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Per-model texture fixes of the scanned actors, painted in 3D on the bind-pose surface (<see cref="GlbTexPaint"/>):
    /// artifact cleanup (skin-coloured patches baked into hair, blotches on clothes) and the art-direction changes
    /// (recolours, a mole...). Used by the bake and by the "scan" QA sheet (raw scan vs fixed scan).
    /// </summary>
    public static partial class GlbFix
    {
        public const int Size = 2048;

        /// <summary>Base colour map of material 0 after the fixes, <see cref="Size"/> square (null: the model has no texture).</summary>
        public static Texture2D Texture(CastDef def, GlbRigger.Scan scan, System.Text.StringBuilder log, bool applyFixes = true)
        {
            var glb = scan.Glb;
            if (glb.Materials.Count == 0 || glb.Materials[0].BaseColorImage < 0) return null;
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(glb.Images[glb.Materials[0].BaseColorImage]);
            var tex = ResizeTo(src, Size);
            Object.DestroyImmediate(src);
            bool hasFix = Fixes.TryGetValue(def.Id, out var fix);
            bool faceFix = GlbFaceInpaint.Wants(def.Id);   // charpolish step 0 hook (face implementer's GlbFaceInpaint.cs)
            if (!applyFixes || (!hasFix && !faceFix)) return tex;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var tp = GlbTexPaint.Build(tex, scan.V, scan.N, scan.UV, scan.T, scan.TM.ToArray(), 0);
            var ctx = new Ctx { Def = def, Scan = scan, TP = tp, LM = scan.LM, Log = log };
            if (hasFix) fix(ctx);
            if (faceFix) GlbFaceInpaint.Apply(ctx);
            tp.PadEdits(4);
            tex.SetPixels(tp.Px); tex.Apply(true);
            log?.AppendLine($"[{def.Id}] texture fixes in {sw.ElapsedMilliseconds} ms");
            return tex;
        }

        public sealed class Ctx
        {
            public CastDef Def; public GlbRigger.Scan Scan; public GlbTexPaint TP; public GlbRigger.Landmarks LM; public System.Text.StringBuilder Log;
            public void Note(string s) => Log?.AppendLine($"[{Def.Id}] fix: {s}");
        }

        static readonly Dictionary<string, System.Action<Ctx>> Fixes = new Dictionary<string, System.Action<Ctx>>();

        static Texture2D ResizeTo(Texture2D src, int size)
        {
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            t.ReadPixels(new Rect(0, 0, size, size), 0, 0); t.Apply(true);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            return t;
        }

        /// <summary>
        /// Per-vertex shading masks for the single-material scans (uv3): x = hair (angel-ring highlight), y = skin
        /// (warm terminator). Hair = head-weighted, non-skin texture colour above the jaw, outside the eyes.
        /// </summary>
        public static List<Vector2> VertexMasks(CastDef def, List<Vector3> V, List<Vector2> UV, List<BoneWeight> W, Texture2D tex, GlbRigger.Landmarks lm, int count)
        {
            var o = new List<Vector2>(V.Count);
            int head = (int)BL23.Game.Characters.HBone.Head;
            var cal = GlbFace.Get(def.Id);
            Vector3 e0 = lm.EyeCenter + new Vector3(cal.EyeDX, cal.EyeDY, 0), e1 = lm.EyeCenter + new Vector3(-cal.EyeDX, cal.EyeDY, 0);
            int nh = 0, ns = 0;
            for (int i = 0; i < V.Count; i++)
            {
                if (i >= count || tex == null) { o.Add(Vector2.zero); continue; }
                var col = tex.GetPixelBilinear(UV[i].x, UV[i].y);
                float hw = 0f; var w = W[i];
                if (w.boneIndex0 == head) hw += w.weight0; if (w.boneIndex1 == head) hw += w.weight1;
                if (w.boneIndex2 == head) hw += w.weight2; if (w.boneIndex3 == head) hw += w.weight3;
                var p = V[i];
                bool skin = SkinLike(col);
                bool eye = ((p - e0).sqrMagnitude < 0.016f * 0.016f || (p - e1).sqrMagnitude < 0.016f * 0.016f) && p.z > lm.EyeCenter.z - 0.01f;
                bool hair = hw > 0.4f && !skin && !eye && p.y > lm.ChinY - 0.01f;
                if (hair) nh++; if (skin) ns++;
                o.Add(new Vector2(hair ? 1f : 0f, skin ? 1f : 0f));
            }
            Debug.Log($"[{def.Id}] vertex masks: hair {nh}, skin {ns} of {V.Count}");
            return o;
        }

        /// <summary>
        /// Hair strands in front of the eyes get a much thinner outline (uv1.w = outline reduction): black ink lines
        /// slicing across the eyes read as broken eyes in close-ups.
        /// </summary>
        public static void ThinOutlinesNearEyes(CastDef def, List<Vector3> V, List<Vector2> masks, List<Vector4> faceUv, GlbRigger.Landmarks lm, int count)
        {
            var cal = GlbFace.Get(def.Id); var e = lm.EyeCenter;
            int n = 0;
            for (int i = 0; i < count && i < V.Count; i++)
            {
                // everything in the eye band (hair, fused strands, painted-over shards): the face interior needs no ink hull
                var p = V[i];
                float dy = Mathf.Abs(p.y - (e.y + cal.EyeDY)), dx = Mathf.Abs(p.x - e.x);
                // the whole fringe in front of the face (brow line up to +60 mm, down to the cheeks): no ink hull
                float yRel = p.y - (e.y + cal.EyeDY);
                if (p.z < lm.HeadCenter.z || yRel < -0.045f || yRel > 0.07f || dx > cal.EyeDX + 0.04f) continue;
                float k = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.055f, 0.07f, yRel))) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.03f, -0.045f, yRel))) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(cal.EyeDX + 0.026f, cal.EyeDX + 0.04f, dx)));
                var f = faceUv[i];
                faceUv[i] = new Vector4(f.x, f.y, f.z, Mathf.Max(f.w, k));
                if (k > 0.5f) n++;
            }
            Debug.Log($"[{def.Id}] thin outlines near the eyes: {n} hair vertices");
        }

        // ------------------------------------------------------------------ shared helpers for the per-model fixes
        public static float Lum(Color c) => GlbTexPaint.Lum(c);
        public static void Hsv(Color c, out float h, out float s, out float v) => Color.RGBToHSV(c, out h, out s, out v);
        /// <summary>Distance of a texel to the head centre, scaled so the head ellipsoid is ~1.</summary>
        public static bool InBox(Vector3 p, Vector3 mn, Vector3 mx) => p.x >= mn.x && p.x <= mx.x && p.y >= mn.y && p.y <= mx.y && p.z >= mn.z && p.z <= mx.z;
    }
}
