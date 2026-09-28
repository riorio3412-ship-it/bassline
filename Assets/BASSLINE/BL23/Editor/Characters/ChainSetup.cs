using BL23.Game.Characters;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Bake-time tuning of every <see cref="SpringChain"/> (owner: charpolish implementer 4, physics). Step 0 moved the
    /// old inline tuning of CharacterBaker.AddChain here unchanged, so the physics implementer can retune chains (and
    /// add new fields) without touching CharacterBaker.cs (owned by implementer 5).
    /// </summary>
    public static class ChainSetup
    {
        /// <summary>Procedural chains: prefix is the chain name without the "_0" suffix (Hair*, Tail*, Coat*, Skirt*...).</summary>
        public static void ConfigureProc(SpringChain sc, string prefix)
        {
            bool hair = prefix.StartsWith("Hair");
            sc.Stiffness = hair ? 0.1f : 0.16f;
            sc.Damping = hair ? 0.16f : 0.22f;
            sc.Gravity = hair ? 3f : 4f;
            sc.MaxAngle = prefix.StartsWith("Tail") ? 45f : 65f;
            sc.Radius = hair ? 0.02f : 0.03f;
        }
    }
}
