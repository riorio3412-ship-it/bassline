using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>Resolved colors for one dream palette id (see BL23.Sim.Palettes.All).</summary>
    public sealed class MansionPalette
    {
        public string Id;
        public Color Wall;        // wallpaper ground
        public Color Ink;         // wallpaper motif
        public Color Wood;        // wainscot / furniture wood tint
        public Color Trim;        // gilded trim, frames
        public Color Neon;        // emissive accent (HDR-ish, keep saturated)
        public Color Fabric;      // upholstery, curtains
        public Color Carpet;      // rugs, runners
        public Color FloorA;      // light checker square / tile
        public Color FloorB;      // dark checker square
        public Color Warm;        // candle / lamp light color
        public Color Ambient;     // room fill (dim)
        public Color Fog;
        public Color Accent2;     // second saturated accent (flowers, glass)

        static Dictionary<string, MansionPalette> _all;

        static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        static void Add(string id, string wall, string ink, string wood, string trim, string neon, string fabric, string carpet,
                        string floorA, string floorB, string warm, string amb, string fog, string acc2)
        {
            _all[id] = new MansionPalette
            {
                Id = id, Wall = H(wall), Ink = H(ink), Wood = H(wood), Trim = H(trim), Neon = H(neon), Fabric = H(fabric), Carpet = H(carpet),
                FloorA = H(floorA), FloorB = H(floorB), Warm = H(warm), Ambient = H(amb), Fog = H(fog), Accent2 = H(acc2)
            };
        }

        static void Ensure()
        {
            if (_all != null) return;
            _all = new Dictionary<string, MansionPalette>();
            //   id              wall       ink        wood       trim       neon       fabric     carpet     floorA     floorB     warm       ambient    fog        accent2
            // Refined jewel tones: saturated but never fluorescent. "Neon" is the room's accent jewel (stained glass,
            // lacquer, piping), not an electric colour; walls are deep Victorian papers rather than candy pastels.
            Add("Amethyst",     "#3A1C50", "#7A4AA6", "#3A2030", "#D4A850", "#A8408E", "#5A2072", "#5E1A50", "#E6DDF0", "#2A1038", "#FFB070", "#2E2238", "#2A1440", "#4A9EB4");
            Add("BloodOpera",   "#4A0E18", "#8E1C2E", "#361410", "#D8AC4C", "#B42438", "#7C121F", "#6E101C", "#EDE0D6", "#260608", "#FFA050", "#3A1E22", "#3A0A12", "#E4B866");
            Add("TealAbyss",    "#0E3A40", "#26807C", "#1E2A2C", "#C8B070", "#2E9E9A", "#0F5058", "#0C4048", "#D8F0EE", "#041C22", "#FFC890", "#1C3438", "#08303A", "#B85A82");
            Add("RosePorcelain","#9C6070", "#6E2A40", "#5A3034", "#E8D2B8", "#C45E86", "#94485E", "#86384E", "#F4E8E4", "#6E2A40", "#FFD2B4", "#3E2E34", "#5A3040", "#6A9EBC");
            Add("GildedRot",    "#3E3214", "#8E7428", "#3A2412", "#E0B840", "#D09A30", "#6A4412", "#5A3A10", "#EFE2C0", "#221606", "#FFB050", "#342C1C", "#2E2410", "#8EA83C");
            Add("MoonMint",     "#3E6E62", "#1E5446", "#2C3A34", "#D0C49A", "#56B094", "#2E7464", "#285E52", "#EAF4EE", "#16403A", "#FFE6C8", "#26383A", "#1E4A44", "#C06E9E");
            Add("Absinthe",     "#34480E", "#7E9C22", "#2A2A10", "#D0AC48", "#94B830", "#445E10", "#38500C", "#EEF4D0", "#1A2406", "#FFD890", "#2A3218", "#22300C", "#A848A0");
            Add("Nocturne",     "#161C40", "#34448E", "#1A1A26", "#B8B08E", "#5A54C4", "#1E2660", "#1A2050", "#DADFF4", "#080A1C", "#FFB878", "#1C2036", "#0E1230", "#B44880");
            Add("CoralFlesh",   "#8E3E38", "#5E1C26", "#4A2420", "#E8D0B4", "#CC5A48", "#8A3030", "#76262C", "#F2DCD2", "#5A1418", "#FFB890", "#3A2626", "#4A1E20", "#3AA89C");
            Add("BoneIvory",    "#CFC2A6", "#8E7E60", "#5A4A38", "#E8DCC0", "#D8E4EE", "#B4A686", "#9C8A64", "#F6F0E4", "#6E6450", "#FFF0D6", "#3E3A30", "#4A4436", "#B84458");
            Add("CobaltCandle", "#16306A", "#2E5AB4", "#1C1A24", "#DCAE4C", "#3A70C8", "#1A3A86", "#16306E", "#E4ECFA", "#060E2C", "#FFA848", "#1C2440", "#0C1840", "#E0AE40");
            Add("PeachMold",    "#A87050", "#5E6A34", "#5A3A28", "#E0C088", "#D8905A", "#A06448", "#86503A", "#F2E2D0", "#4A3A20", "#FFD4A0", "#3E3226", "#4A3624", "#88B858");
            // ---- private rooms: one palette per resident (OwnerStyles), never shared, never drawn by seed
            Add("Own_P01", "#8A6A48", "#C8A878", "#4A3020", "#C8A060", "#C87A3A", "#8A4A2A", "#7A4A2E", "#E8D8C0", "#3A2418", "#FFC38A", "#3A3028", "#3A2A1E", "#6A8A4A");
            Add("Own_P02", "#2A2448", "#6A5AA0", "#2A1E24", "#B8A060", "#C83A5A", "#3A2A5A", "#4A2A4A", "#DCD8EC", "#141028", "#FFDCC0", "#221E30", "#1A1628", "#E0C040");
            Add("Own_P03", "#1E2A44", "#3A4E7A", "#2A2020", "#C8B080", "#B3263A", "#22305A", "#2A3458", "#E0E4EC", "#101828", "#FFF0D8", "#1E2432", "#141C2C", "#B3263A");
            Add("Own_P04", "#8A857A", "#6A655A", "#3A3026", "#B8A070", "#8A7A5A", "#9A9284", "#6A6458", "#E6E2D8", "#3A3630", "#F2EEE6", "#34322E", "#2A2824", "#5A6A72");
            Add("Own_P05", "#5A6468", "#1F6F6A", "#2A2A2A", "#D0B060", "#1F8F8A", "#2A5A58", "#244A48", "#E6EAEA", "#1A2A2A", "#FFE6C4", "#263032", "#1A2628", "#D0B060");
            Add("Own_P06", "#2A3A2C", "#4A6A48", "#3A2A1A", "#B8893A", "#8AA050", "#3A4A2A", "#4A3A22", "#E0DCC8", "#1A2014", "#FFD8A0", "#262C22", "#1A2216", "#B8893A");
            Add("Own_P07", "#3A2A22", "#8A4A2A", "#2A1E18", "#E3B23C", "#E07A2A", "#6A3A1E", "#3A2A1E", "#D8C8B8", "#1A120E", "#FFB070", "#2E2420", "#241A14", "#E3B23C");
            Add("Own_P08", "#3A4A5A", "#5A7A8A", "#2A2A30", "#A8A8A8", "#4AA0A8", "#3A5A6A", "#2E3E4E", "#D8E0E8", "#141C24", "#C8DCE8", "#20282E", "#182028", "#C9C9C9");
            Add("Own_P09", "#1E4A38", "#3A8A62", "#3A2418", "#C88A4A", "#1E8A63", "#7A2A2A", "#5A1E1E", "#EAD8C8", "#14261E", "#FFD2A0", "#22302A", "#18241E", "#C88A4A");
            Add("Own_P10", "#8A9A6A", "#5A6A3A", "#5A3A22", "#C8A060", "#C86A3A", "#E0D8C0", "#8A5A3A", "#E8E0D0", "#4A3A2A", "#FFD08A", "#34362A", "#2A2C1E", "#C86A3A");
            Add("Own_P11", "#6A5A4A", "#8A6A4A", "#3A2A1E", "#9AA8B0", "#C4702A", "#7A5A3A", "#4A3A30", "#C8C0B4", "#2A2622", "#E6EEF4", "#2C2A28", "#242220", "#7FA8B8");
            Add("Own_P12", "#6A3A4A", "#B8607E", "#2A1A20", "#D8B070", "#E07AA0", "#E0B0C0", "#5A2A3A", "#F0E0E6", "#1A0E14", "#FFC8DC", "#2E2228", "#261A20", "#111111");
            Add("Own_P13", "#1C1A1E", "#3A1A1E", "#1A1A1A", "#8A8A8A", "#C0282E", "#2A2A2E", "#2A1A1C", "#C8C8CC", "#0E0E10", "#A8C8FF", "#1C1C22", "#141418", "#E6E6EA");
            Add("Own_P14", "#1A1818", "#3A3634", "#141212", "#D8D0B8", "#EDE8DA", "#0E0E0E", "#1E1A1A", "#F0ECE4", "#0A0A0A", "#F4EEE0", "#222020", "#181616", "#EDE8DA");
            Add("Own_P15", "#8A6A2A", "#5A3A1A", "#3A2A1A", "#C8A040", "#B83A2A", "#8A3A2A", "#5A4020", "#EAE0C8", "#2A1E10", "#FFD89A", "#322A1C", "#2A2014", "#B83A2A");
            Add("Own_P16", "#4A1E42", "#9A3A86", "#2A1A24", "#C9CCD2", "#C83A9A", "#6A2A60", "#4A1A40", "#F0E4EC", "#1E0A1A", "#FFD8E8", "#2A2028", "#22161E", "#C9CCD2");
            Add("Own_P17", "#8ABFB0", "#5A9A8A", "#6A5A4A", "#E8D8B0", "#F29CB8", "#D8E8E0", "#A8C8BC", "#F4F0EA", "#5A7A70", "#F0FFF4", "#303A36", "#28322E", "#F29CB8");
            Add("Own_P18", "#7A786A", "#6A6858", "#4A4234", "#9A9070", "#5A6A4A", "#6A6A58", "#4A4A3E", "#D8D4C8", "#2E2C26", "#FFE4B8", "#2C2C26", "#24241E", "#B08A3A");
            // a resident's accents are their signature colour (CastColors, the notebook map's tile): map and room agree
            for (int i = 1; i <= 18; i++)
            {
                string pid = "P" + i.ToString("00");
                var sig = CastColors.Of(pid); Color.RGBToHSV(sig, out float h, out float s, out float v);
                var p = _all["Own_" + pid]; p.Accent2 = sig; p.Neon = Color.HSVToRGB(h, Mathf.Min(0.75f, s * 1.3f), 0.72f);
            }
        }

        public static MansionPalette Get(string id)
        {
            Ensure();
            if (id != null && _all.TryGetValue(id, out var p)) return p;
            return _all["Amethyst"];
        }

        public static IEnumerable<MansionPalette> All { get { Ensure(); return _all.Values; } }

        /// <summary>Parse "#RRGGBB" tint strings from furniture, falling back to a palette color.</summary>
        public static Color Parse(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            var p = _all != null && _all.ContainsKey(hex) ? _all[hex] : null;
            return p != null ? p.Fabric : fallback;
        }

        public static Color Mul(Color a, float k) => new Color(a.r * k, a.g * k, a.b * k, a.a);
        public static Color Lerp(Color a, Color b, float t) => Color.Lerp(a, b, t);
        public static Color Sat(Color c, float s)
        {
            Color.RGBToHSV(c, out float h, out float sa, out float v);
            return Color.HSVToRGB(h, Mathf.Clamp01(sa * s), v);
        }
        public static Color Val(Color c, float v)
        {
            Color.RGBToHSV(c, out float h, out float sa, out float vv);
            return Color.HSVToRGB(h, sa, Mathf.Clamp01(vv * v));
        }
        public static Color HueShift(Color c, float dh)
        {
            Color.RGBToHSV(c, out float h, out float sa, out float v);
            h = Mathf.Repeat(h + dh, 1f);
            return Color.HSVToRGB(h, sa, v);
        }
    }
}
