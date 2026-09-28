using System.Collections.Generic;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Bake-time secondary-motion rig of the scanned (GLB) actors (owner: charpolish implementer 4, physics): appends
    /// cloth / hair chain bones AFTER the 52 HBones (names "Cloth*"/"HairG*", never an HBone name), re-weights cape,
    /// coat-skirt and hair vertices to them, and adds the SpringChain components on the prefab.
    /// Step-0 STUB: Build returns null and Attach does nothing, so bakes are unchanged until the physics implementer
    /// fills them in. Called from GlbRigger.BakeImpl (implementer 2's file): Build after the vertex masks, before the
    /// bone weights are written to the mesh; Attach after CharacterBaker.FillRig.
    /// </summary>
    public static class GlbCloth
    {
        public sealed class Result
        {
            public readonly List<string> ChainRoots = new List<string>();   // BoneDef names of the chain roots
            public readonly List<string> ChainKinds = new List<string>();   // "cape", "coat", "hair", "scarf"... (ChainSetup tuning key)
        }

        /// <param name="V">vertices (reposed rest pose; the base mesh is [0, baseVerts), accessories after)</param>
        /// <param name="W">final bone weights (modified in place for cloth / hair vertices)</param>
        /// <param name="vmasks">uv3 per-vertex masks (x = hair, y = skin)</param>
        /// <param name="faceUv">uv1 (xy face uv, z front gate): never re-weight gated face-front vertices</param>
        /// <param name="bones">bone list (52 HBones first); chain bones are appended</param>
        public static Result Build(CastDef def, List<Vector3> V, List<Vector3> N, List<BoneWeight> W, List<Vector2> vmasks, List<Vector4> faceUv,
            int baseVerts, List<BoneDef> bones, GlbRigger.Landmarks lm, BodyProportions P, GlbArms.Params arms, System.Text.StringBuilder log)
        {
            return null;
        }

        /// <summary>Adds the SpringChain components for the chains Build created (boneT = transforms in bone-list order).</summary>
        public static void Attach(Result r, ActorRig rig, Transform[] boneT, List<BoneDef> bones) { }
    }
}
