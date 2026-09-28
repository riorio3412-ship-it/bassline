using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Room background cards (SocialEventsDesign §5, the owner's 48 room paintings): the first time 민혁 steps into a kind of
    /// room this session, its painting fills the screen for a moment with the room's name over it, like a visual novel's
    /// background card. The paintings live in Resources/Rooms/ as {RoomType}.png, with variants {RoomType}_2 and _3 (a room
    /// picks one by its id). A kind of room without a painting shows nothing, so the cards arrive one file at a time.
    /// Presentation only: nothing here touches the kernel. Drawn with IMGUI, over everything, without taking input.
    /// </summary>
    public sealed class RoomCardUI : MonoBehaviour
    {
        Session _s; int _lastRoom = -1;
        readonly HashSet<RoomType> _seen = new HashSet<RoomType>();
        readonly Dictionary<RoomType, Texture2D[]> _cards = new Dictionary<RoomType, Texture2D[]>();
        Texture2D _tex; string _name; float _t0 = -1; GUIStyle _style;
        const float FadeIn = 0.25f, Hold = 2.2f, FadeOut = 0.6f;
        /// <summary>Off: no cards (a settings toggle can set this).</summary>
        public static bool Enabled = true;

        public void Init(Session s) { _s = s; }

        Texture2D[] Cards(RoomType t)
        {
            if (_cards.TryGetValue(t, out var c)) return c;
            var list = new List<Texture2D>();
            foreach (var key in new[] { t.ToString(), t + "_2", t + "_3" })
            {
                var tex = Resources.Load<Texture2D>("Rooms/" + key);
                if (tex != null) list.Add(tex);
            }
            c = list.ToArray(); _cards[t] = c; return c;
        }

        void Update()
        {
            if (!Enabled || _s == null || _s.Sim == null) return;
            var S = _s.S; var p = S?.Player; if (p == null || !p.Alive) return;
            if (S.Phase != Phase.Daily && S.Phase != Phase.Investigation) { _lastRoom = p.Room; return; }
            if (_s.Cine != null && _s.Cine.Busy) return;   // after the cinematic, the room still counts as new
            if (p.Room == _lastRoom) return;
            _lastRoom = p.Room;
            var r = S.Layout.Room(p.Room); if (r == null || _seen.Contains(r.Type)) return;
            var cards = Cards(r.Type); if (cards.Length == 0) return;
            _seen.Add(r.Type); _tex = cards[r.Id % cards.Length]; _name = r.Name; _t0 = Time.unscaledTime;
        }

        void OnGUI()
        {
            if (_tex == null || _t0 < 0) return;
            float e = Time.unscaledTime - _t0;
            float a = e < FadeIn ? e / FadeIn : e < FadeIn + Hold ? 1f : 1f - (e - FadeIn - Hold) / FadeOut;
            if (a <= 0f) { _tex = null; _t0 = -1; return; }
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a * 0.92f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _tex, ScaleMode.ScaleAndCrop);
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerLeft, fontStyle = FontStyle.Bold };
            _style.fontSize = Mathf.Max(24, Screen.height / 16);
            var box = new Rect(Screen.width * 0.05f, Screen.height * 0.70f, Screen.width * 0.9f, Screen.height * 0.22f);
            GUI.color = new Color(0f, 0f, 0f, a * 0.8f); GUI.Label(new Rect(box.x + 3, box.y + 3, box.width, box.height), _name, _style);   // a shadow under the name
            GUI.color = new Color(1f, 1f, 1f, a); GUI.Label(box, _name, _style);
            GUI.color = old;
        }
    }
}
