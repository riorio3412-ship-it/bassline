using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>Keeps the mansion shaders referenced from Resources so player builds include them (Shader.Find alone would strip).</summary>
    public sealed class MansionShaderRefs : ScriptableObject
    {
        public Shader Lit, Glow, Glass, Sky, Water, Decal, Screen, Particle;
    }
}
