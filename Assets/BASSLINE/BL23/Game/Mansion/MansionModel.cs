using System;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// A CC0 scanned model converted from glTF by the editor tool (BL23.EditorTools.Mansion.MansionModelConverter).
    /// Stored under Resources/Mansion/Models so the runtime builder can load it without glTFast.
    /// </summary>
    public sealed class MansionModel : ScriptableObject
    {
        [Serializable]
        public sealed class Part
        {
            public string Name;
            public Texture2D BaseMap, NormalMap, MaskMap;   // mask = glTF ARM (AO, rough, metal)
            public Color BaseColor = Color.white;
            public float Metallic = 1f, Roughness = 1f;
            public bool AlphaClip;
            public bool DoubleSided;
            public bool Recolor;                            // fabric-like part that accepts palette tints
        }

        public string SourceId;
        public string Author;
        public Mesh Mesh;
        public Part[] Parts = new Part[0];
        public Bounds Bounds;
        public int Triangles;
    }
}
