using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BL23.Sim
{
    /// <summary>
    /// Banter in ordinary NPC conversations — the voice packs' mature keys brought to life (one hook: Social.ConvoLine):
    ///   tease / tease_react         among friends (and on the "tease" topic)
    ///   joke / joke_react           among friends, on small talk
    ///   insult / insult_back        rivals, grudges, anger (the "argue" topic)
    ///   outburst                    someone already angry at a rival
    ///   gloat / gloat_react         the third line of a quarrel: whoever is on top rubs it in
    ///   scared (→ comfort)          someone afraid
    ///   flirt / love_hint / flirt_react   only with an attraction tie (crush, lover, romance)
    ///   dirty_joke / react_dirty    close, casual friends whose voice has it
    ///   react_swear                 answering a line that swore
    /// GUARDRAILS enforced here, in code (the content already follows them):
    ///   · no flirt / love_hint / flirt_react / dirty_joke / react_dirty with 예담 P17, 수아 P12 or 진우 P02 as speaker or listener;
    ///   · no joke, tease, insult or gloat line that mentions 세나's hand (P13 listening or talked about) — another variant, or plain talk.
    /// Swearing itself is allowed (the owner wants it; there is no filter or toggle).
    /// Also the lying keys: LifeLieKey (Testimony) — a resident with lie_&lt;key&gt; lies in their own voice, with their tell.
    /// </summary>
    public sealed partial class Simulation
    {
        /// <summary>Never sexual or romantic toward, from or about these three (the voice authors' and the owner's exclusion).</summary>
        public static bool NoRomance(string id) => id == "P17" || id == "P12" || id == "P02";
        static readonly HashSet<string> RomanticKeys = new HashSet<string> { "flirt", "love_hint", "flirt_react", "dirty_joke", "react_dirty" };
        static readonly HashSet<string> MockKeys = new HashSet<string> { "tease", "tease_react", "joke", "joke_react", "insult", "insult_back", "gloat", "gloat_react", "outburst", "dirty_joke" };
        static readonly Regex _swear = new Regex("(씨발|시발|씨이|좆|존나|개새|새끼|미친|뒈져|꺼져|젠장|빌어먹)", RegexOptions.Compiled);
        static readonly Regex _handMock = new Regex("(의수|오른손|왼손|손목|손가락|쇠손|가짜 손|손이|손을|손은|손도|손만|손 하나)", RegexOptions.Compiled);

        /// <summary>Is this key off-limits for this pair (romance guardrail)?</summary>
        public static bool BanterBlocked(string key, string speaker, string listener, string about = null)
            => key != null && RomanticKeys.Contains(key) && (NoRomance(speaker) || NoRomance(listener) || NoRomance(about));

        readonly Dictionary<string, (long tick, string key, int n, string text)> _banter = new Dictionary<string, (long, string, int, string)>();

        bool VoiceHas(string who, string key, string to) => LineBank.Has(who, key) || (to != null && LineBank.Has(who, key + "@" + to));

        /// <summary>Hook: Social.ConvoLine. True = this line was said here (a banter key); false = the ordinary topic line.</summary>
        internal bool LifeConvoLine(string topic, string third, Actor sp, Actor li, bool isOpener)
        {
            if (topic == null || sp == null || li == null || topic.StartsWith("g_") || sp.IsPlayer || li.IsPlayer) return false;
            try
            {
                string pk = string.CompareOrdinal(sp.Id, li.Id) < 0 ? sp.Id + li.Id : li.Id + sp.Id;
                bool cont = _banter.TryGetValue(pk, out var prev) && S.Tick - prev.tick < 90;
                int n = cont ? prev.n + 1 : 0;
                string last = cont ? prev.key : null, lastText = cont ? prev.text : null;
                var r = S.R(sp.Id, li.Id); var rng = LR;
                bool friends = r.Like > 0.3f || r.Tags.Contains("friend") || r.Tags.Contains("warmth") || r.Tags.Contains("banter");
                bool rivals = r.Grudge > 0.3f || r.Tags.Contains("rival") || r.Tags.Contains("feud") || r.Tags.Contains("enemy") || r.Tags.Contains("grudge");
                bool romance = !NoRomance(sp.Id) && !NoRomance(li.Id) && (r.Romance > 0.3f || r.Tags.Contains("crush") || r.Tags.Contains("lover"));
                string key = null; var slots = new Dictionary<string, string>();
                if (!isOpener && last != null)
                {
                    switch (last)
                    {
                        case "tease": key = "tease_react"; break;
                        case "insult": case "outburst": key = VoiceHas(sp.Id, "insult_back", li.Id) ? "insult_back" : null; break;
                        case "joke": key = VoiceHas(sp.Id, "joke_react", li.Id) ? "joke_react" : null; break;
                        case "gloat": key = VoiceHas(sp.Id, "gloat_react", li.Id) ? "gloat_react" : null; break;
                        case "flirt": case "love_hint": key = romance && VoiceHas(sp.Id, "flirt_react", li.Id) ? "flirt_react" : "small_talk"; break;
                        case "dirty_joke": key = VoiceHas(sp.Id, "react_dirty", li.Id) ? "react_dirty" : null; break;
                        case "scared": if (VoiceHas(sp.Id, "comfort", li.Id)) { key = "comfort"; slots["t"] = "@" + li.Id; } break;
                    }
                    if (lastText != null && _swear.IsMatch(lastText) && VoiceHas(sp.Id, "react_swear", li.Id) && rng.Chance(0.5)) key = "react_swear";
                }
                else if (isOpener)
                {
                    if (sp.Needs.Fear > 0.5f && VoiceHas(sp.Id, "scared", li.Id) && rng.Chance(0.35)) key = "scared";
                    else if (sp.Needs.Anger > 0.6f && rivals && VoiceHas(sp.Id, "outburst", li.Id) && rng.Chance(0.4)) key = "outburst";
                    else switch (topic)
                        {
                            case "tease": if (VoiceHas(sp.Id, "tease", li.Id)) key = "tease"; break;
                            case "argue":
                                if (n >= 2 && VoiceHas(sp.Id, "gloat", li.Id) && rng.Chance(0.5)) key = "gloat";
                                else if ((r.Grudge > 0.4f || sp.Needs.Anger > 0.5f) && VoiceHas(sp.Id, "insult", li.Id) && rng.Chance(0.5)) key = "insult";
                                break;
                            case "flirt": key = romance ? (VoiceHas(sp.Id, "flirt", li.Id) && rng.Chance(0.5) ? "flirt" : "love_hint") : "small_talk"; break;
                            case "small": case "like":
                                if (friends && VoiceHas(sp.Id, "joke", li.Id) && rng.Chance(0.25)) key = "joke";
                                else if (friends && r.Casual && !NoRomance(sp.Id) && !NoRomance(li.Id) && VoiceHas(sp.Id, "dirty_joke", li.Id) && rng.Chance(0.08)) key = "dirty_joke";
                                else if (friends && VoiceHas(sp.Id, "tease", li.Id) && rng.Chance(0.12)) key = "tease";
                                else if (rivals && VoiceHas(sp.Id, "insult", li.Id) && rng.Chance(0.1)) key = "insult";
                                break;
                        }
                }
                if (key == null)
                {
                    // the ordinary line is said by the topic code; remember the turn so the next line can answer it
                    _banter[pk] = (S.Tick, topic == "flirt" && (NoRomance(sp.Id) || NoRomance(li.Id)) ? "small" : "default:" + topic, n, null);
                    if (topic == "flirt" && (NoRomance(sp.Id) || NoRomance(li.Id))) { var safe = Render(sp.Id, li.Id, "small_talk"); if (safe != null) { LifeSayNow(sp, li.Id, safe, "small_talk"); return true; } }
                    return false;
                }
                if (BanterBlocked(key, sp.Id, li.Id)) key = "small_talk";
                string text = RenderGuarded(sp.Id, li.Id, key, slots);
                if (text == null) { _banter[pk] = (S.Tick, "default:" + topic, n, null); return false; }
                LifeSayNow(sp, li.Id, text, key);
                _banter[pk] = (S.Tick, key, n, text);
                LFinc("lbanter:" + key);
                if (_banter.Count > 200) _banter.Clear();
                return true;
            }
            catch (Exception e) { Fault("life:banter", e); return false; }
        }

        /// <summary>Renders a banter line; a mocking line that would touch 세나's hand is re-drawn (another variant) or becomes small talk.</summary>
        string RenderGuarded(string speaker, string listener, string key, Dictionary<string, string> slots)
        {
            bool guardHand = MockKeys.Contains(key) && (listener == "P13" || (slots != null && slots.TryGetValue("t", out var t) && t == "@P13"));
            for (int i = 0; i < 4; i++)
            {
                var text = Render(speaker, listener, key, slots);
                if (text == null) return null;
                if (!guardHand || !_handMock.IsMatch(text)) return text;
            }
            return Render(speaker, listener, "small_talk", slots);
        }

        /// <summary>Hook: Testimony — a lying answer in the resident's own lying voice (lie_&lt;key&gt;, e.g. lie_alibi_where), so the tell shows.</summary>
        public static string LifeLieKey(string speaker, string key) => speaker != null && key != null && LineBank.Has(speaker, "lie_" + key) ? "lie_" + key : key;
    }
}
