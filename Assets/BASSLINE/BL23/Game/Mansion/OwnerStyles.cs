using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Who lives here: the fixed identity of each resident's private room (Documentation/BL23/CharacterBible.md, Cast.cs).
    /// Everything is keyed by the owner id only, never by seed or loop, so the same person always gets the same room:
    /// the door (signature colour, emblem, lit name plaque, what stands or hangs outside), the palette and surfaces,
    /// the bed, the light mood, the surface vocabulary and how tidy they are. The props themselves are placed by
    /// MansionView.Owners / Dress against the room's real furniture.
    /// </summary>
    internal sealed class OwnerLook
    {
        public string Id;
        public string Palette;          // registered in MansionPalette ("Own_P01"...)
        public Color Sig;               // signature colour = CastColors.Of(id), the notebook map's tile colour
        public Color Door;              // door inset panels: the signature hue, deepened for dark wood
        public string Emblem;           // relief on the corridor face of the door
        public string Outside;          // object standing beside the door frame in the corridor
        public string Hang;             // item hung on the corridor face of the door (or null)
        public Color Mat;               // doormat colour
        public int Floor, Paper;        // S slots (Paper: wallpaper or plaster/brick/stone)
        public int Bed;                 // 0 gothic scan, 1 canopy, 2 iron cot, 3 low platform, 4 sleigh
        public float Clutter;           // 0 spotless .. 1 chaos
        public Color Light;             // lamp / light mood colour
        public string LampKind;         // desk light: "lamp" oil lamp, "banker" green banker's lamp, "monitor", "work", "candles", "vanity", "bulb"
        public string[] Surface;        // Dress kinds for desk / nightstand / console tops
    }

    internal static class OwnerStyles
    {
        static Dictionary<string, OwnerLook> _all;

        static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        static void Add(string id, string emblem, string outside, string hang, string mat, int floor, int paper, int bed, float clutter, string light, string lamp, params string[] surface)
        {
            // the signature colour is the notebook map's (CastColors): the door panels, plaque band, doormat border and the
            // bedspread / curtain trim all wear it, so the map tile and the corridor agree at a glance
            var sig = CastColors.Of(id);
            Color.RGBToHSV(sig, out float h, out float s, out float v);
            _all[id] = new OwnerLook
            {
                Id = id, Palette = "Own_" + id, Sig = sig, Door = Color.HSVToRGB(h, Mathf.Min(0.62f, s * 1.15f), 0.46f), Emblem = emblem, Outside = outside, Hang = hang,
                Mat = Color.Lerp(H(mat), Color.HSVToRGB(h, 0.4f, 0.3f), 0.5f), Floor = floor, Paper = paper, Bed = bed, Clutter = clutter, Light = H(light), LampKind = lamp, Surface = surface
            };
        }

        static void Ensure()
        {
            if (_all != null) return;
            _all = new Dictionary<string, OwnerLook>();
            //   id     emblem      outside        hang        mat        floor          paper            bed clutter light      lamp       surface vocabulary
            Add("P01", "wheat",     "boots",       null,       "#C8B48C", S.Herringbone, S.PaperDamask,   4, 0.35f, "#FFC38A", "lamp",    "bread", "books", "cups", "board_games", "papers");
            Add("P02", "candyjar",  "wrappers",    null,       "#4A3A6A", S.Parquet,     S.PaperEyes,     0, 0.15f, "#E8E0FF", "banker",  "candy_bowl", "books", "chess_clock", "papers", "books");
            Add("P03", "clipboard", "slippers",    "roster",   "#2A3A60", S.Herringbone, S.PaperStripe,   0, 0.02f, "#FFF2DC", "banker",  "pen_tray", "binders", "cup", "papers");
            Add("P04", "frame",     "levelframe",  null,       "#6A655C", S.Parquet,     S.Plaster,       0, 0.0f,  "#F2F0EA", "work",    "cup", "tools_cloth", "bell_jar", "frame");
            Add("P05", "rosette",   "shoes_towel", null,       "#1A4A48", S.MarbleFloor, S.PaperStripe,   4, 0.2f,  "#FFE8C8", "vanity",  "speech", "decanter", "goblets", "papers");
            Add("P06", "watch",     "parcels",     null,       "#3A4A30", S.Herringbone, S.PaperDamask2,  0, 0.25f, "#E8D8A8", "banker",  "ledgers", "chocolate", "scales", "parcel_small");
            Add("P07", "mic",       "sneakers_beer","chain",   "#3A2A1A", S.WoodWorn,    S.Brick,         3, 0.85f, "#FFB070", "bulb",    "cans", "books", "bottle", "cups", "papers");
            Add("P08", "clef",      "guitarcase",  null,       "#3A4A5A", S.Carpet,      S.Plaster,       3, 0.7f,  "#9FC4D8", "bulb",    "hotpacks", "cables", "cans", "cup", "papers");
            Add("P09", "masks",     "seat37",      null,       "#1E4A36", S.Carpet,      S.PaperDamask2,  1, 0.6f,  "#FFD2A0", "vanity",  "playbills", "flowers", "decanter", "books");
            Add("P10", "knifewhisk","basil",       null,       "#6A5A3A", S.Terrazzo,    S.PaperMoon,     4, 0.3f,  "#FFD08A", "lamp",    "spoons", "herb_pot", "jug", "bowl", "cookbook");
            Add("P11", "gear",      "toolbox",     null,       "#5A4A3A", S.WornTile,    S.Brick,         2, 0.8f,  "#E6EEF4", "work",    "windup", "soda", "jar", "cans", "parts");
            Add("P12", "star",      "starpot",     "starcharm","#D8A0B8", S.Carpet,      S.PaperStripe,   1, 0.55f, "#FFC8DC", "vanity",  "stickers", "flowers_low", "cup", "goods");
            Add("P13", "controller","sneakers",    "headset",  "#2A1A1A", S.Carpet,      S.Plaster,       2, 0.5f,  "#A8C8FF", "monitor", "cans", "trophy_small", "cables", "cup");
            Add("P14", "lily",      "lilyvase",    "ribbon",   "#1A1A1A", S.MarbleFloor, S.PaperDamask,   1, 0.0f,  "#F4EEE0", "candles", "paper_lilies", "tea", "candle_mess", "gloves");
            Add("P15", "camera",    "newspapers",  null,       "#6A5A2A", S.Parquet,     S.PaperStripe,   0, 0.75f, "#FFD89A", "lamp",    "papers", "paper_cups", "typewriter", "books");
            Add("P16", "button",    "shopbag",     "shopsign", "#5A1E50", S.Herringbone, S.PaperDamask,   1, 0.3f,  "#FFD8E8", "vanity",  "button_jars", "tape_measure", "fabric_small", "vase_small");
            Add("P17", "paperhouse","inviteStool", "bunting",  "#A8D8CC", S.Carpet,      S.PaperMoon,     3, 0.65f, "#E8FFF4", "ring",    "paper_models", "invites", "scissors_glue", "papers");
            Add("P18", "thermos",   "workboots",   null,       "#4A483A", S.WoodWorn,    S.Plaster,       2, 0.0f,  "#FFE4B8", "bulb",    "thermos", "puzzle_book");
        }

        public static OwnerLook Get(string owner) { Ensure(); return owner != null && _all.TryGetValue(owner, out var o) ? o : null; }

        /// <summary>Stable per-owner seed (no string.GetHashCode: the same number on every machine and run).</summary>
        public static int Seed(string owner)
        {
            int h = 17; if (owner != null) foreach (char ch in owner) h = h * 31 + ch;
            return h & 0x7fffffff;
        }
    }
}
