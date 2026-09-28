namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Per-model proportion corrections of the scanned (GLB) actors, applied at bake time (owner: charpolish implementer
    /// 2, proportions + GLB model fixes). Step-0 STUB: every value is neutral until implementer 2 fills the table in.
    /// Other code may READ these (the face implementer scales P10's face calibration by <see cref="HeadScale"/>).
    /// </summary>
    public static class GlbProportions
    {
        /// <summary>Uniform head scale applied to the scan in GlbRigger.LoadScan before landmarks (1 = unchanged).</summary>
        public static float HeadScale(string id) => 1f;
    }
}
