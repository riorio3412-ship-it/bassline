using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Blood and body colours for every Game/Gore visual (linear values, used directly as vertex colour / albedo).
    /// Blood is deep crimson to near-black, glossy, drying brown at the edges; never pink or magenta: every computed blood
    /// colour satisfies g ≤ 0.35·r and b ≤ 0.25·r (<see cref="Check"/>). Watery blood is the same hue at lower alpha.
    /// </summary>
    public static class GorePalette
    {
        public static readonly Color Wet = new Color(0.30f, 0.012f, 0.02f, 1f);        // fresh, wet crimson
        public static readonly Color Thick = new Color(0.14f, 0.005f, 0.01f, 1f);      // pooled / clotting, near-black
        public static readonly Color DryRim = new Color(0.20f, 0.06f, 0.035f, 1f);     // drying brown edge
        public static readonly Color Watery = new Color(0.22f, 0.02f, 0.025f, 0.35f);  // washed-down drain blood
        public static readonly Color Bone = new Color(0.80f, 0.76f, 0.66f, 1f);
        public static readonly Color Muscle = new Color(0.36f, 0.03f, 0.04f, 1f);
        public static readonly Color SkinRim = new Color(0.74f, 0.62f, 0.52f, 1f);
        public static readonly Color Linen = new Color(0.78f, 0.74f, 0.68f, 1f);
        public static readonly Color Char = new Color(0.06f, 0.05f, 0.045f, 1f);       // burnt bone
        public static readonly Color Ash = new Color(0.22f, 0.21f, 0.2f, 0.85f);

        static bool _warned;

        /// <summary>Clamp a blood colour into the crimson family (g ≤ 0.35 r, b ≤ 0.25 r). Development builds and the editor
        /// log the first violation so a pink regression is caught in probes.</summary>
        public static Color Check(Color c)
        {
            float gMax = 0.35f * c.r, bMax = 0.25f * c.r;
            if (c.g <= gMax + 1e-5f && c.b <= bMax + 1e-5f) return c;
            if (!_warned && (Application.isEditor || Debug.isDebugBuild)) { _warned = true; Debug.LogWarning($"[Gore] blood colour out of the crimson family: {c} (clamped)"); }
            c.g = Mathf.Min(c.g, gMax); c.b = Mathf.Min(c.b, bMax);
            return c;
        }

        /// <summary>Blood colour for a given age in clock minutes and position within the stain (0 centre .. 1 rim):
        /// wet under 20 min, the rim browns from 20 to 120 min, the thick centre darkens toward near-black.</summary>
        public static Color Aged(float ageMin, float rim01, float alpha = 1f)
        {
            float dry = Mathf.Clamp01((ageMin - 20f) / 100f);
            var centre = Color.Lerp(Color.Lerp(Wet, Thick, 0.55f), Thick, dry * 0.6f);
            var rim = Color.Lerp(Wet, DryRim, dry);
            var c = Color.Lerp(centre, rim, Mathf.SmoothStep(0f, 1f, rim01));
            c.a = alpha;
            return Check(c);
        }

        /// <summary>Gloss for a given age and rim position: wet centres ~0.9, rims ~0.35, dry stains lose their sheen.</summary>
        public static float Gloss(float ageMin, float rim01)
        {
            float dry = Mathf.Clamp01((ageMin - 20f) / 100f);
            return Mathf.Lerp(Mathf.Lerp(0.9f, 0.62f, dry), Mathf.Lerp(0.35f, 0.18f, dry), Mathf.SmoothStep(0f, 1f, rim01));
        }

        /// <summary>A thin stain (drop, smear, print) at an age: these dry much faster than a pool.</summary>
        public static Color Thin(float ageMin, float alpha = 1f)
        {
            float dry = Mathf.Clamp01((ageMin - 5f) / 55f);
            var c = Color.Lerp(Wet, Color.Lerp(DryRim, Thick, 0.35f), dry); c.a = alpha;
            return Check(c);
        }
    }
}
