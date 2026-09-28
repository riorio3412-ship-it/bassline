using System.Collections.Generic;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Face-feature cleanup of the scanned base maps (owner: charpolish implementer 1, face): removes painted/scanned
    /// features that an animated overlay replaces (the scanned eyes, mouth line and grin, gold tooth, smears), so no feature
    /// is ever drawn twice. Runs on the full-resolution source texture inside GlbFix.Texture, after the per-model
    /// fixes. Step-0 STUB: Wants() is false for everyone, so bakes are unchanged until the face implementer fills it in.
    /// </summary>
    public static class GlbFaceInpaint
    {
        /// <summary>True when this scanned actor needs a face cleanup pass (forces the texel map to be built).</summary>
        public static bool Wants(string id) => false;

        /// <summary>Edits ctx.TP (texel colours, with texel positions / normals in bind space) in place.</summary>
        public static void Apply(GlbFix.Ctx ctx) { }

        /// <summary>charpolish step 0b hook (called in GlbRigger.BakeImpl right after GlbFace.BuildImage, before
        /// SmoothFaceNormals and the blend shapes): flatten the modelled eye sockets and lip crease toward the fitted skin
        /// surface (so the toon ramp and the outline stop drawing a second eye / mouth). Stub: no change.</summary>
        public static void FlattenFeatures(string id, List<Vector3> V, List<Vector3> N, GlbRigger.Landmarks lm, GlbFace.Frame f, GlbFaceMask.Img img, System.Text.StringBuilder log) { }

        /// <summary>charpolish step 0b hook (called right after GlbFix.ThinOutlinesNearEyes): raise the outline reduction
        /// (faceUv[i].w, 1 = no outline) over the mouth region and on hair vertices (masks[i].x = hair) that hang in front of
        /// the face. Stub: no change.</summary>
        public static void OutlineMask(string id, List<Vector3> V, List<Vector2> masks, List<Vector4> faceUv, GlbRigger.Landmarks lm, int count) { }
    }
}
