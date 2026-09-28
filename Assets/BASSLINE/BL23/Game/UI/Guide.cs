namespace BL23.Game
{
    /// <summary>
    /// Wayfinding target shared by the notebook ([추적하기]), the appointment auto-track and the HUD compass line.
    /// Presentation only: it never touches the kernel.
    /// </summary>
    public static class Guide
    {
        public static int TargetRoom = -1;
        public static string TargetLabel;
        /// <summary>True when the current target was set automatically (an accepted appointment), not chosen by the player.</summary>
        public static bool Auto;
        /// <summary>Request id of an automatically tracked appointment (so it can be dropped when the appointment ends).</summary>
        public static string AutoKey;

        /// <summary>The player chose a target (notebook, map): it wins over automatic tracking until it is reached or cleared.</summary>
        public static void Track(int room, string label) { TargetRoom = room; TargetLabel = label; Auto = false; AutoKey = null; }

        /// <summary>Automatic tracking (an accepted appointment in Daily). Never replaces a target the player chose.</summary>
        public static void TrackAuto(int room, string label, string key)
        {
            if (TargetRoom >= 0 && !Auto) return;
            TargetRoom = room; TargetLabel = label; Auto = true; AutoKey = key;
        }

        public static void Clear() { TargetRoom = -1; TargetLabel = null; Auto = false; AutoKey = null; }
    }
}
