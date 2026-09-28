using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BL23.Game
{
    /// <summary>BASSLINE palette: night violet base, neon magenta/cyan accents, tarnished gold.</summary>
    public static class Pal
    {
        // Gothic palette: lamp-black lacquer, oxblood velvet, antique gold leaf, verdigris (stained-glass teal), parchment.
        // (Field names are historical: "Magenta" is the primary accent, "Cyan" the secondary.)
        public static readonly Color Ink = new Color(0.03f, 0.022f, 0.02f, 1f);
        public static readonly Color Panel = new Color(0.065f, 0.045f, 0.038f, 0.93f);
        public static readonly Color Panel2 = new Color(0.115f, 0.078f, 0.062f, 0.95f);
        public static readonly Color Magenta = new Color(0.66f, 0.13f, 0.17f, 1f);     // oxblood
        public static readonly Color MagentaDim = new Color(0.33f, 0.06f, 0.08f, 1f);
        public static readonly Color Cyan = new Color(0.52f, 0.74f, 0.66f, 1f);        // verdigris
        public static readonly Color CyanDim = new Color(0.17f, 0.29f, 0.26f, 1f);
        public static readonly Color Gold = new Color(0.84f, 0.68f, 0.38f, 1f);        // antique gold leaf
        public static readonly Color Blood = new Color(0.62f, 0.05f, 0.08f, 1f);
        public static readonly Color Text = new Color(0.95f, 0.91f, 0.83f, 1f);        // parchment
        public static readonly Color TextDim = new Color(0.68f, 0.62f, 0.53f, 1f);
        public static readonly Color Good = new Color(0.45f, 0.95f, 0.6f, 1f);
        public static Color A(Color c, float a) { c.a = a; return c; }
        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    }

    public static class Fonts
    {
        static TMP_FontAsset _body, _bold, _title, _serif, _impact;
        public static TMP_FontAsset Body => _body ?? (_body = Make("Fonts/NanumGothic-Regular"));
        public static TMP_FontAsset Bold => _bold ?? (_bold = Make("Fonts/NanumGothic-ExtraBold") ?? Body);
        // headings and numerals use the Gowun Batang serif (book-like, quiet); the old poster face stays available as Heavy
        public static TMP_FontAsset Title => _title ?? (_title = Make("Fonts/GowunBatang-Bold") ?? Bold);
        public static TMP_FontAsset Serif => _serif ?? (_serif = Make("Fonts/GowunBatang-Bold") ?? Body);
        public static TMP_FontAsset SerifLight => _serifL ?? (_serifL = Make("Fonts/GowunBatang-Regular") ?? Serif);
        public static TMP_FontAsset Impact => _impact ?? (_impact = Make("Fonts/GowunBatang-Bold") ?? Title);
        public static TMP_FontAsset Heavy => _heavy ?? (_heavy = Make("Fonts/BlackHanSans-Regular") ?? Bold);
        static TMP_FontAsset _heavy, _serifL;
        /// <summary>Latin display face for the logo and engraved headings (Cinzel Decorative, OFL). Hangul falls back to the serif.</summary>
        public static TMP_FontAsset Display { get { if (_display == null) { _display = Make("Fonts/CinzelDecorative-Bold") ?? Serif; if (_display != Serif && Serif != null && !_display.fallbackFontAssetTable.Contains(Serif)) _display.fallbackFontAssetTable.Add(Serif); } return _display; } }
        static TMP_FontAsset _display;

        static TMP_FontAsset Make(string res)
        {
            // prefer the editor-baked dynamic font asset (ships its SDF material); fall back to building one at runtime
            var baked = Resources.Load<TMP_FontAsset>("FontAssets/" + res.Substring(res.LastIndexOf('/') + 1) + " SDF");
            if (baked != null) { if (_body != null && baked != _body) baked.fallbackFontAssetTable = new List<TMP_FontAsset> { _body }; return baked; }
            var f = Resources.Load<Font>(res);
            if (f == null) { Debug.LogWarning("[BL23] font missing " + res); return TMP_Settings.defaultFontAsset; }
            var fa = TMP_FontAsset.CreateFontAsset(f, 72, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            fa.name = f.name + " (dyn)";
            // fallback chain so rare glyphs never render as boxes
            if (_body != null && fa != _body) fa.fallbackFontAssetTable = new List<TMP_FontAsset> { _body };
            return fa;
        }
    }

    /// <summary>Parallelogram panel with gradient fill and neon edge — procedural, resolution independent.</summary>
    public class SlantPanel : MaskableGraphic
    {
        public float Slant = 18f;           // horizontal offset of the top edge (px)
        public Color Top = Pal.Panel, Bottom = Pal.Panel;
        public Color Edge = Pal.A(Pal.Magenta, 0.9f); public float EdgeWidth = 2f; public bool EdgeLeftOnly;
        public float Glow = 0f;             // outer glow alpha
        /// <summary>Gothic frame mode (default): upright panels with a gilded double rule and small lozenge corner ornaments.</summary>
        public static bool Gothic = true;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (Gothic) { Framed(vh); return; }
            vh.Clear(); var r = rectTransform.rect;
            Vector2 bl = new Vector2(r.xMin, r.yMin), br = new Vector2(r.xMax - Slant, r.yMin), tr = new Vector2(r.xMax, r.yMax), tl = new Vector2(r.xMin + Slant, r.yMax);
            if (Slant < 0) { bl.x = r.xMin - Slant; br.x = r.xMax; tr.x = r.xMax + Slant; tl.x = r.xMin; }
            Quad(vh, bl, br, tr, tl, Bottom * color, Bottom * color, Top * color, Top * color);
            if (EdgeWidth > 0)
            {
                var e = Edge * color;
                if (!EdgeLeftOnly)
                {
                    Line(vh, bl, br, EdgeWidth, e); Line(vh, tl, tr, EdgeWidth, e); Line(vh, br, tr, EdgeWidth, e);
                }
                Line(vh, bl, tl, EdgeWidth * (EdgeLeftOnly ? 2.5f : 1f), e);
            }
            if (Glow > 0)
            {
                var g0 = Pal.A(Edge, Glow) * color; var g1 = Pal.A(Edge, 0) * color; float w = 14f;
                Quad(vh, tl, tr, tr + new Vector2(0, w), tl + new Vector2(0, w), g0, g0, g1, g1);
                Quad(vh, bl + new Vector2(0, -w), br + new Vector2(0, -w), br, bl, g1, g1, g0, g0);
            }
        }
        void Framed(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            Vector2 bl = new Vector2(r.xMin, r.yMin), br = new Vector2(r.xMax, r.yMin), tr = new Vector2(r.xMax, r.yMax), tl = new Vector2(r.xMin, r.yMax);
            Quad(vh, bl, br, tr, tl, Bottom * color, Bottom * color, Top * color, Top * color);
            // soft inner shade toward the edges (vellum/lacquer depth)
            var sh = new Color(0, 0, 0, 0.35f) * color; var cl = new Color(0, 0, 0, 0);
            float sw = Mathf.Min(18f, Mathf.Min(r.width, r.height) * 0.2f);
            Quad(vh, bl, br, br + new Vector2(0, sw), bl + new Vector2(0, sw), sh, sh, cl, cl);
            Quad(vh, tl - new Vector2(0, sw), tr - new Vector2(0, sw), tr, tl, cl, cl, sh, sh);
            if (EdgeWidth <= 0) return;
            var gold = Edge * color; var faint = Pal.A(Edge, Edge.a * 0.45f) * color;
            if (EdgeLeftOnly)
            {
                Line(vh, bl + new Vector2(1.5f, 0), tl + new Vector2(1.5f, 0), EdgeWidth * 1.6f, gold);
                Line(vh, tl, tl + new Vector2(Mathf.Min(r.width, 60f), 0), 1f, faint); Line(vh, bl, bl + new Vector2(Mathf.Min(r.width, 60f), 0), 1f, faint);
                return;
            }
            // outer rule + inner hairline
            float w = Mathf.Max(1f, EdgeWidth * 0.75f), inset = Mathf.Min(5f, Mathf.Min(r.width, r.height) * 0.08f);
            Line(vh, bl, br, w, gold); Line(vh, tl, tr, w, gold); Line(vh, bl, tl, w, gold); Line(vh, br, tr, w, gold);
            if (r.width > 40 && r.height > 24)
            {
                Vector2 ibl = bl + new Vector2(inset, inset), ibr = br + new Vector2(-inset, inset), itr = tr + new Vector2(-inset, -inset), itl = tl + new Vector2(inset, -inset);
                Line(vh, ibl, ibr, 1f, faint); Line(vh, itl, itr, 1f, faint); Line(vh, ibl, itl, 1f, faint); Line(vh, ibr, itr, 1f, faint);
                // lozenge ornaments on the corners
                float s = Mathf.Min(7f, inset + 2f);
                foreach (var c in new[] { bl, br, tr, tl })
                    Quad(vh, c + new Vector2(0, -s), c + new Vector2(s, 0), c + new Vector2(0, s), c + new Vector2(-s, 0), gold, gold, gold, gold);
            }
            if (Glow > 0)
            {
                var g0 = Pal.A(Edge, Glow * 0.6f) * color; var g1 = Pal.A(Edge, 0) * color; float gw = 10f;
                Quad(vh, tl, tr, tr + new Vector2(0, gw), tl + new Vector2(0, gw), g0, g0, g1, g1);
                Quad(vh, bl + new Vector2(0, -gw), br + new Vector2(0, -gw), br, bl, g1, g1, g0, g0);
            }
        }

        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, ca, Vector2.zero); vh.AddVert(b, cb, Vector2.zero); vh.AddVert(c, cc, Vector2.zero); vh.AddVert(d, cd, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float w, Color c)
        {
            var n = (b - a).normalized; var p = new Vector2(-n.y, n.x) * (w * 0.5f);
            Quad(vh, a - p, b - p, b + p, a + p, c, c, c, c);
        }
        public void Refresh() => SetVerticesDirty();
    }

    /// <summary>Tiny builder for code-made UI. Everything here is plain uGUI + TMP so it works in batch builds.</summary>
    public static class UIKit
    {
        public static Canvas Root(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920, 1080); s.matchWidthOrHeight = 0.6f;
            s.scaleFactor = 1f;
            _scalers.RemoveAll(x => x == null); _scalers.Add(s); ApplyScale(s);
            UnityEngine.Object.DontDestroyOnLoad(go);
            return c;
        }

        /// <summary>Shrink a centred panel of the given design size so it always fits its canvas.</summary>
        public static void FitToCanvas(RectTransform panel, float w, float h)
        {
            var c = panel.GetComponentInParent<Canvas>(); if (c == null) return; Canvas.ForceUpdateCanvases();
            var cr = ((RectTransform)c.rootCanvas.transform).rect; if (cr.width < 1 || cr.height < 1) return;
            float k = Mathf.Min(1f, (cr.height - 30) / h, (cr.width - 30) / w); panel.localScale = Vector3.one * k;
        }

        static readonly List<CanvasScaler> _scalers = new List<CanvasScaler>();
        /// <summary>UI size setting (accessibility): a smaller reference resolution makes every panel and font larger.</summary>
        public static void ApplyScale(CanvasScaler only = null)
        {
            float k = Mathf.Clamp(Settings.UIScale, 0.8f, 1.6f);
            foreach (var s in only != null ? new List<CanvasScaler> { only } : _scalers) if (s != null) s.referenceResolution = new Vector2(1920, 1080) / k;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.Object.DontDestroyOnLoad(es);
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 offMin = default, Vector2 offMax = default)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax;
            return rt;
        }

        public static RectTransform Full(Transform parent, string name) => Rect(parent, name, Vector2.zero, Vector2.one);

        public static Image Img(Transform parent, string name, Color c, Vector2 aMin, Vector2 aMax, Vector2 offMin = default, Vector2 offMax = default)
        {
            var rt = Rect(parent, name, aMin, aMax, offMin, offMax); var im = rt.gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = false; return im;
        }

        public static SlantPanel Slant(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax, Color top, Color bottom, Color edge, float slant = 18f)
        {
            var rt = Rect(parent, name, aMin, aMax, offMin, offMax); var p = rt.gameObject.AddComponent<SlantPanel>();
            p.Top = top; p.Bottom = bottom; p.Edge = edge; p.Slant = slant; p.raycastTarget = false; return p;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color c, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, TMP_FontAsset font = null, Vector2? aMin = null, Vector2? aMax = null, Vector2 offMin = default, Vector2 offMax = default)
        {
            var rt = Rect(parent, name, aMin ?? Vector2.zero, aMax ?? Vector2.one, offMin, offMax);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font ?? Fonts.Body; t.fontSize = size; t.color = c; t.alignment = align; t.text = text; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow; t.richText = true;
            return t;
        }

        public sealed class Btn : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
        {
            public SlantPanel Bg; public TextMeshProUGUI Label; public Action OnClick; public bool Interactable = true; public bool Hover;
            public Color Idle = Pal.A(Pal.Panel2, 0.9f), HoverTop = Pal.Magenta, HoverBottom = Pal.MagentaDim;
            public void OnPointerEnter(PointerEventData e) { Hover = true; Paint(); if (Interactable) UISfx.Hover(); }
            public void OnPointerExit(PointerEventData e) { Hover = false; Paint(); }
            public void OnSelect(BaseEventData e) { Hover = true; Paint(); }
            public void OnDeselect(BaseEventData e) { Hover = false; Paint(); }
            public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Click(); }
            public void OnSubmit(BaseEventData e) => Click();
            public void Click() { if (!Interactable) { UISfx.Deny(); return; } UISfx.Confirm(); OnClick?.Invoke(); }
            public void Paint()
            {
                if (Bg == null) return;
                if (!Interactable) { Bg.Top = Bg.Bottom = Pal.A(Pal.Panel, 0.6f); Bg.Edge = Pal.A(Pal.TextDim, 0.2f); if (Label) Label.color = Pal.A(Pal.TextDim, 0.6f); }
                else if (Hover) { Bg.Top = HoverTop; Bg.Bottom = HoverBottom; Bg.Edge = Pal.Cyan; if (Label) Label.color = Color.white; }
                else { Bg.Top = Idle; Bg.Bottom = Pal.A(Pal.Ink, 0.9f); Bg.Edge = Pal.A(Pal.Magenta, 0.55f); if (Label) Label.color = Pal.Text; }
                Bg.Refresh();
            }
        }

        public static Btn Button(Transform parent, string label, Action onClick, Vector2 aMin, Vector2 aMax, Vector2 offMin = default, Vector2 offMax = default, float size = 30, float slant = 16f, TMP_FontAsset font = null)
        {
            var p = Slant(parent, "Btn_" + label, aMin, aMax, offMin, offMax, Pal.Panel2, Pal.Ink, Pal.A(Pal.Gold, 0.75f), slant);
            p.raycastTarget = true;
            var b = p.gameObject.AddComponent<Btn>(); b.Bg = p; b.OnClick = onClick;
            b.Label = Text(p.transform, "Label", label, size, Pal.Text, TextAlignmentOptions.MidlineLeft, font ?? Fonts.Bold, Vector2.zero, Vector2.one, new Vector2(28, 0), new Vector2(-16, 0));
            b.Paint();
            return b;
        }

        public static void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject); }

        public static void SetActive(Component c, bool on) { if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on); }
    }

    /// <summary>UI sounds routed to the audio module when present (safe no-op otherwise).</summary>
    public static class UISfx
    {
        public static void Hover() => Play("ui_hover", 0.35f);
        public static void Confirm() => Play("ui_confirm", 0.6f);
        public static void Cancel() => Play("ui_cancel", 0.6f);
        public static void Deny() => Play("ui_cancel", 0.4f);
        public static void Page() => Play("ui_page", 0.5f);
        public static void Play(string id, float vol) { try { Audio.Sfx.Play(id, null, vol); } catch (Exception) { } }
    }
}
