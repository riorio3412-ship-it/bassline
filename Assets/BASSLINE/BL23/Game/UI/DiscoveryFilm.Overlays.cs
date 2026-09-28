using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Screen overlays that must not sit over the discovery film's close-ups: the time flow's stop banner ("비명이 들렸다"),
    /// people's speech bubbles, the system notes (and, for the probe's stills, the HUD). Those canvases switch themselves on
    /// every frame, so their Canvas.enabled cannot be borrowed; each is faded out through a CanvasGroup on its root instead
    /// (one added for the purpose is removed again, an existing one gets its alpha back exactly). Yusti's broadcasts wait on
    /// their own (<see cref="AnnouncementUI"/>) so none of his words are lost.
    /// </summary>
    public static class FilmOverlays
    {
        static readonly string[] Names = { "TimeFlow", "SpeechBubbleCanvas", "HudSys" };
        sealed class Rec { public CanvasGroup G; public bool Added; public float Alpha; }
        static readonly Dictionary<Canvas, Rec> _hidden = new Dictionary<Canvas, Rec>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _hidden.Clear(); }

        public static bool Hidden => _hidden.Count > 0;

        /// <summary>Fade the overlays out at once (hud: the HUD and the broadcast box too — the probe's stills).</summary>
        public static void Hide(bool hud = false)
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c == null || !c.isRootCanvas || _hidden.ContainsKey(c)) continue;
                bool want = System.Array.IndexOf(Names, c.name) >= 0 || (hud && (c.name == "HUD" || c.name == "Announce"));
                if (!want) continue;
                var g = c.GetComponent<CanvasGroup>(); bool added = g == null;
                if (added) g = c.gameObject.AddComponent<CanvasGroup>();
                _hidden[c] = new Rec { G = g, Added = added, Alpha = g.alpha };
                g.alpha = 0f;
            }
        }

        /// <summary>Give every overlay back exactly as it was (safe to call any number of times).</summary>
        public static void Show()
        {
            foreach (var kv in _hidden)
            {
                var r = kv.Value; if (kv.Key == null || r.G == null) continue;
                if (r.Added) { r.G.alpha = 1f; Object.Destroy(r.G); } else r.G.alpha = r.Alpha;
            }
            _hidden.Clear();
        }
    }
}
