using BL23.Sim;

namespace BL23.EditorTools.Characters
{
    /// <summary>Auto-rigs the provided GLB scans onto the common skeleton (implemented in GlbRigger.Impl.cs).</summary>
    public static partial class GlbRigger
    {
        public static bool Bake(CastDef def, System.Text.StringBuilder log)
        {
            return BakeImpl(def, log);
        }

        static partial void Dummy();
    }
}
