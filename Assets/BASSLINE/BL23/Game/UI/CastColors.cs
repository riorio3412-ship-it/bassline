using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// One signature colour per resident: the bedroom tile on the notebook map and anything else that should say
    /// "this is X's" at a glance (door plaques, room accents). Muted gothic hues spread around the wheel so neighbours
    /// never look alike; P10's is green because that is his outfit.
    /// </summary>
    public static class CastColors
    {
        static readonly Color[] Hues =
        {
            H(0.00f), H(0.56f), H(0.08f), H(0.62f), H(0.13f), H(0.72f), H(0.47f), H(0.93f), H(0.18f),
            H(0.33f), // P10: green
            H(0.80f), H(0.03f), H(0.52f), H(0.88f), H(0.24f), H(0.67f), H(0.40f), H(0.97f),
        };

        static Color H(float hue) => Color.HSVToRGB(hue, 0.5f, 0.82f);

        /// <summary>P01..P18 → their colour; the butler is brass; anyone else a neutral parchment.</summary>
        public static Color Of(string id)
        {
            if (string.IsNullOrEmpty(id)) return new Color(0.72f, 0.68f, 0.6f);
            if (id == "NPC00") return new Color(0.78f, 0.62f, 0.32f);
            if (id.Length == 3 && id[0] == 'P' && int.TryParse(id.Substring(1), out int n) && n >= 1 && n <= Hues.Length) return Hues[n - 1];
            return new Color(0.72f, 0.68f, 0.6f);
        }
    }
}
