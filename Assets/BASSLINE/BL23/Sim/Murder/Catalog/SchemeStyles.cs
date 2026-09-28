using System;
using System.Collections.Generic;

namespace BL23.Sim
{
    /// <summary>How a resident schemes (CharacterBible §4 "Murder and 심판", MurderFoundation §B2 skills). Weights, never casting:
    /// anyone can kill in any way their knowledge allows; the style only tilts which opportunity they reach for.</summary>
    public sealed class SchemeStyle
    {
        public string Id;
        /// <summary>Meticulous | Theatrical | Practical | Impulsive | Technical.</summary>
        public string Style;
        /// <summary>Extra audacity (−0.3 … +0.3) on top of what the personality numbers give.</summary>
        public float Bold;
        public string[] Skills = new string[0];
        /// <summary>Event kinds this person would plausibly host (Initiative.HostKinds ids).</summary>
        public string[] Hosts = new string[0];
        /// <summary>Approaches they lean to / refuse (CaseTruthApi §3.3 words; "method:Push" etc.).</summary>
        public string[] Likes = new string[0], Never = new string[0];
        public bool Skill(string s) { foreach (var x in Skills) if (x == s) return true; return false; }
        public bool Hosting(string k) { foreach (var x in Hosts) if (x == k) return true; return false; }
        public bool Leans(string ap) { foreach (var x in Likes) if (x == ap) return true; return false; }
        public bool Refuses(string ap) { foreach (var x in Never) if (x == ap) return true; return false; }
    }

    public static class SchemeStyles
    {
        static Dictionary<string, SchemeStyle> _d;
        static SchemeStyle S(string id, string style, float bold, string skills, string hosts, string likes, string never = "")
            => new SchemeStyle { Id = id, Style = style, Bold = bold, Skills = Split(skills), Hosts = Split(hosts), Likes = Split(likes), Never = Split(never) };
        static string[] Split(string s) => string.IsNullOrEmpty(s) ? new string[0] : s.Split(',');

        static void Ensure()
        {
            if (_d != null) return;
            _d = new Dictionary<string, SchemeStyle>(StringComparer.Ordinal);
            void A(SchemeStyle x) => _d[x.Id] = x;
            // 진우: tests people, reads them; frames and arranged witnesses; card night is his stage
            A(S("P02", "Theatrical", 0.15f, "Social,Perception,Records", "cards,reading", "errand,rendezvous,serve"));
            // 서윤: rosters and schedules; a roster-checked inspection, a pair on duty, a clock she trusts
            A(S("P03", "Meticulous", -0.1f, "Schedules,Records", "inspection,tea,reading", "errand,rendezvous,method:Bedtime"));
            // 도윤: restores things to their place; tea, a too-tidy scene, staged natural deaths
            A(S("P04", "Meticulous", 0.05f, "Craft,Chem", "tea,reading", "serve,method:Smother,method:Bedtime,visit", "dark-strike"));
            // 이현: a born host; manipulates helpers, prefers others' hands and a crowd for cover
            A(S("P05", "Theatrical", 0.2f, "Social,Helpers,Swim", "wine,tea,photo", "errand,dark-strike,method:Drown"));
            // 태겸: keys, stock and logistics; the house's lock-up as an accomplice
            A(S("P06", "Practical", 0f, "Keys,Records,Logistics", "inspection,cards", "rendezvous,errand,ambush"));
            // 시온: throws the party, acts first and thinks after; darkness and noise
            A(S("P07", "Impulsive", 0.3f, "Sound,Social", "music,cards", "dark-strike,ambush,slip-out", "serve"));
            // 라온: ears; noise cover, a recorded voice, the music room
            A(S("P08", "Technical", 0f, "Sound,Recorder", "music,film", "slip-out,ambush,dark-strike"));
            // 재하: the stage; a voice that can be anyone's, a show in the dark
            A(S("P09", "Theatrical", 0.25f, "Theatre,Voice,Outfits", "show,rehearsal,reading", "dark-strike,rendezvous,errand"));
            // 준서: the kitchen and the cold store; feeds everyone — the plate is the weapon
            A(S("P10", "Practical", -0.05f, "Cooking,Strength,Cold", "cooking,tea", "serve,errand"));
            // 해린: circuits, clocks, machines; the house's own darkness, a clock set a quarter hour wrong
            A(S("P11", "Technical", 0.1f, "Tech,Electric,Clocks", "film,inspection", "dark-strike,method:Shock,errand"));
            // 수아: charm and the stage; others do her small favours without asking why
            A(S("P12", "Theatrical", 0.15f, "Theatre,Charm", "show,music,photo", "errand,dark-strike,serve"));
            // 세나: timing and routes; the pool; speedrun a corridor twice before doing it once
            A(S("P13", "Impulsive", 0.2f, "Timing,Swim", "cards,film", "ambush,slip-out,method:Drown,method:Push"));
            // 은결: the dead, the cold, the chemicals; the first to kneel by a body
            A(S("P14", "Meticulous", 0.1f, "Bodies,Cold,Chem", "memorial,tea", "serve,method:Smother,errand,visit"));
            // 가온: records, notes, handwriting; a forged line in someone else's name
            A(S("P15", "Meticulous", 0f, "Records,Writing", "reading,inspection", "rendezvous,errand,serve"));
            // 채령: fabrics, thread, a borrowed coat; the frame is in the clothes
            A(S("P16", "Meticulous", 0.05f, "Outfits,Thread", "photo,tea", "errand,rendezvous,dark-strike"));
            // 예담: the camera and the screen; a film night in the dark, a helper who holds the lights
            A(S("P17", "Theatrical", 0.2f, "Photo,Theatre,Social", "film,photo,show", "dark-strike,errand,slip-out"));
            // 민서: strength and tools; the cellar check nobody else wants to lead
            A(S("P18", "Practical", -0.15f, "Strength,Tools", "inspection", "errand,ambush"));
        }

        public static SchemeStyle Of(CastDef c)
        {
            Ensure();
            if (c == null) return new SchemeStyle { Id = "?", Style = "Practical" };
            if (_d.TryGetValue(c.Id, out var s)) return s;
            // procedurally generated cast: derive from the numbers
            string st = c.Infer + c.Composure > 165 ? "Meticulous" : c.Deceit > 80 ? "Theatrical" : c.P.Aggression > 0.6f ? "Impulsive" : "Practical";
            return new SchemeStyle { Id = c.Id, Style = st, Bold = 0, Hosts = new[] { "tea", "cards" }, Likes = new[] { "errand", "ambush" } };
        }
    }
}
