namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Cape / mantle / coat re-drape of the T-pose scans to the relaxed rest pose (owner: charpolish implementer 2,
    /// proportions + GLB model fixes). Step-0 STUB. Contract read by the physics implementer's GlbCloth.Build during the
    /// same bake: <see cref="Panels"/> gives, per vertex of the current bake's mesh, which garment panel it belongs to.
    /// </summary>
    public static class GlbDrape
    {
        /// <summary>Panel ids: -1 none, 0 cape left, 1 cape right, 2 cape back, 3 capelet/mantle ring, 10..17 coat-skirt sectors
        /// (10 = front-left, clockwise seen from above), 20 scarf end left, 21 scarf end right.</summary>
        public const int None = -1, CapeL = 0, CapeR = 1, CapeBack = 2, Mantle = 3, CoatSector0 = 10, ScarfL = 20, ScarfR = 21;

        /// <summary>Per-vertex panel ids of the actor currently being baked (null = not computed / no garment panels).</summary>
        public static int[] Panels(string id) => null;
    }
}
