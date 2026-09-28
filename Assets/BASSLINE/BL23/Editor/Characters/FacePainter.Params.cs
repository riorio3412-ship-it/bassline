namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// charpolish step 0 contract (frozen; change only through the integrator): per-actor face-shape parameters of the
    /// procedural cast. The procedural implementer (5) SETS them per actor in ProcBuilder.SetupFace (from LookSpec and a
    /// per-id table); the face implementer (1) makes FacePainter HONOUR them when it paints the atlas. Defaults = the
    /// current look, so nothing changes until both sides use them. Existing knobs stay where they are (Sharp, Female,
    /// LashesHeavy, EyeLidTilt, BrowThick, CuteBlush in FacePainter.cs).
    /// </summary>
    public sealed partial class FacePainter
    {
        public float EyeScaleX = 1f;        // eye opening width multiplier
        public float EyeScaleY = 1f;        // eye opening height multiplier (men ~0.8-0.9: narrower eyes)
        public float IrisScale = 1f;        // iris size relative to the opening (men ~0.85-0.92)
        public float BrowAngleDeg = 0f;     // + = outer end up (neutral brow)
        public float BrowArch = 0.5f;       // 0 straight .. 1 high arch
        public float LashWeight = 1f;       // upper lash line thickness multiplier
        public float MouthWidth = 1f;       // mouth width multiplier
        public bool MaleFace;               // male variant art (sharper lids, fewer lashes, heavier brows, no cute blush)
    }
}
