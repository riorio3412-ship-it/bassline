using System.Collections.Generic;
using BL23.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BL23.Game
{
    /// <summary>
    /// 지난 대화: a ring of the last 120 lines the player heard (dialogue, overheard speech, announcements).
    /// Presentation-only memory; it is not saved and never feeds the kernel.
    /// </summary>
    public static class Backlog
    {
        public sealed class Entry { public string Speaker, Text, Room; public double Clock; }
        public const int Cap = 120;
        static readonly List<Entry> _lines = new List<Entry>();
        public static IReadOnlyList<Entry> Lines => _lines;
        public static int Version { get; private set; }

        /// <summary>Adds a line; the same speaker saying the same thing twice in a row is kept once.</summary>
        public static void Add(string speaker, string text, double clock, string room)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            text = string.Join(" ", LineBank.Pages(text)).Trim(); if (text.Length == 0) return;
            if (_lines.Count > 0) { var last = _lines[_lines.Count - 1]; if (last.Speaker == speaker && last.Text == text) return; }
            _lines.Add(new Entry { Speaker = speaker, Text = text, Clock = clock, Room = room });
            if (_lines.Count > Cap) _lines.RemoveRange(0, _lines.Count - Cap);
            Version++;
        }

        public static void Clear() { _lines.Clear(); Version++; }
    }

    /// <summary>The 지난 대화 overlay (H anywhere, or the mouse wheel up in a conversation). Esc or H closes it; newest at the bottom.</summary>
    public sealed class BacklogUI : MonoBehaviour
    {
        public static bool Open; public static int ClosedFrame = -1;
        static BacklogUI _i;
        Session _s; Canvas _c; ScrollRect _scroll; int _openedFrame;

        public static void Toggle(Session s) { if (Open) Close(); else Show(s); }

        public static void Show(Session s)
        {
            if (s == null || Open) return;
            if (_i == null)
            {
                var c = UIKit.Root("Backlog", 64);   // above the notebook and the court, below the menu
                _i = c.gameObject.AddComponent<BacklogUI>(); _i._c = c;
            }
            _i._s = s; _i.Build(); _i._c.enabled = true; _i._openedFrame = Time.frameCount; Open = true;
            s.Pause("backlog"); UISfx.Page();
        }

        public static void Close()
        {
            if (!Open) return;
            Open = false; ClosedFrame = Time.frameCount;
            if (_i != null) { _i._c.enabled = false; UIKit.Clear(_i._c.transform); _i._s?.Resume("backlog"); }
            UISfx.Cancel();
        }

        void OnDestroy() { if (_i == this) { _i = null; if (Open) { Open = false; _s?.Resume("backlog"); } } }

        void Build()
        {
            var t = _c.transform; UIKit.Clear(t);
            UIKit.Img(t, "Dim", Pal.A(Pal.Ink, 0.78f), Vector2.zero, Vector2.one).raycastTarget = true;
            const float w = 1240, h = 900;
            var p = UIKit.Slant(t, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-w / 2, -h / 2), new Vector2(w / 2, h / 2), Pal.A(Pal.Panel2, 0.99f), Pal.A(Pal.Ink, 0.99f), Pal.A(Pal.Gold, 0.85f), 0);
            p.Glow = 0.2f; UIKit.FitToCanvas(p.rectTransform, w, h);
            UIKit.Text(p.transform, "Title", "지난 대화", 40, Pal.Text, TextAlignmentOptions.Top, Fonts.Title, Vector2.zero, Vector2.one, new Vector2(30, 20), new Vector2(-30, -24));
            UIKit.Text(p.transform, "Keys", "휠·↑↓ 넘기기 · H / Esc 닫기", 18, Pal.A(Pal.TextDim, 0.85f), TextAlignmentOptions.TopRight, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(30, 20), new Vector2(-36, -34));
            UIKit.Img(p.transform, "Rule", Pal.A(Pal.Gold, 0.45f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -92), new Vector2(-60, -91));
            // scroll view
            var view = UIKit.Rect(p.transform, "View", Vector2.zero, Vector2.one, new Vector2(60, 40), new Vector2(-50, -104));
            var vimg = view.gameObject.AddComponent<Image>(); vimg.color = new Color(0, 0, 0, 0.001f); vimg.raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            var content = UIKit.Rect(view, "Content", new Vector2(0, 1), new Vector2(1, 1));
            content.pivot = new Vector2(0.5f, 1);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>(); vl.spacing = 14; vl.childControlHeight = true; vl.childControlWidth = true; vl.childForceExpandHeight = false; vl.childForceExpandWidth = true; vl.padding = new RectOffset(4, 16, 6, 10);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll = view.gameObject.AddComponent<ScrollRect>(); _scroll.content = content; _scroll.viewport = view; _scroll.horizontal = false; _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped; _scroll.scrollSensitivity = 42; _scroll.inertia = false;
            var lines = Backlog.Lines; var S = _s.S; double now = S.Clock; int lastDay = -1;
            if (lines.Count == 0) Entry(content, "<color=#9A8E7C>아직 들은 말이 없다.</color>", 22);
            foreach (var e in lines)
            {
                int day = ClockFmt.Day(e.Clock);
                if (lastDay >= 0 && day != lastDay) Entry(content, $"<color=#9A8E7C>— {day}일째 —</color>", 18, TextAlignmentOptions.Center);
                lastDay = day;
                string who = e.Speaker == Cast.Butler ? "유스티" : e.Speaker == Cast.Player ? "김민혁" : Cast.NameOf(e.Speaker);
                string col = e.Speaker == Cast.Player ? "#B8AC98" : e.Speaker == Cast.Butler ? "#C9A15A" : "#D6AD62";
                string meta = ClockFmt.Anchor(e.Clock, now) + (string.IsNullOrEmpty(e.Room) ? "" : " · " + e.Room);
                Entry(content, $"<color={col}>{who}</color>  <size=72%><color=#8A7E6C>{meta}</color></size>\n{e.Text}", 23);
            }
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content); _scroll.verticalNormalizedPosition = 0f;   // newest at the bottom
        }

        static void Entry(RectTransform parent, string text, float size, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var go = new GameObject("Line", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>(); t.font = Fonts.Serif; t.fontSize = size; t.color = Pal.Text; t.alignment = align; t.text = text; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal; t.richText = true; t.lineSpacing = 2;
        }

        void Update()
        {
            if (!Open || Time.frameCount == _openedFrame) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.H)) { Close(); return; }
            if (_scroll == null) return;
            float step = 0;
            if (Input.GetKeyDown(KeyCode.UpArrow)) step = 0.08f; if (Input.GetKeyDown(KeyCode.DownArrow)) step = -0.08f;
            if (Input.GetKeyDown(KeyCode.PageUp)) step = 0.4f; if (Input.GetKeyDown(KeyCode.PageDown)) step = -0.4f;
            if (step != 0) _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition + step);
        }
    }
}
