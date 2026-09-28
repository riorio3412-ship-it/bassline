using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Audio;
using BL23.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BL23.Game
{
    /// <summary>
    /// What passing time looks like (time-on-demand): the brass clock dial of a time-lapse (bottom centre; large and
    /// centred while asleep; small at the top right during time spent with someone), the veil and fade of long skips,
    /// the interruption banner ("비명 소리가 들렸다"), the result card of a pastime or of time spent together, the lines
    /// said during it, and a small pop for tiny lapses ("이야기하는 사이 6분이 흘렀다"). Its own canvas: the HUD hides
    /// while a cinematic holds the screen, the dial must not.
    /// </summary>
    public sealed class TimeUI : MonoBehaviour
    {
        public enum DialMode { Bottom, Center, TopRight }
        Session _s; GameState S => _s.S; Canvas _c;
        Image _veil, _fade, _vignette; RawImage _grain; float _veilWant, _veilA, _fxWant, _fxA;
        float _fadeFrom, _fadeTo, _fadeT0, _fadeSecs = -1f;
        RectTransform _dial; CanvasGroup _dialCg; Image _face, _rim, _arc, _hourHand, _minHand; TextMeshProUGUI _dialTime, _dialSub; float _dialWant, _dialA; DialMode _mode;
        RectTransform _banner; CanvasGroup _bannerCg; SlantPanel _bannerPanel; TextMeshProUGUI _bTitle, _bSub, _bClock, _bHint; float _bannerUntil = -1f;
        RectTransform _card; CanvasGroup _cardCg; TextMeshProUGUI _cTitle, _cBody; float _cardUntil = -1f;
        RectTransform _line; CanvasGroup _lineCg; TextMeshProUGUI _lWho, _lText; float _lineUntil = -1f;
        RectTransform _pop; CanvasGroup _popCg; TextMeshProUGUI _popText; float _popUntil = -1f;
        /// <summary>The last interruption banner (Session suppresses the matching toast / approach prompt for a second).</summary>
        public float LastBannerAt = -99f; public StopKind LastBannerKind; public string LastBannerActor;
        public bool BannerShowing => _banner != null && _banner.gameObject.activeSelf;
        public string BannerText => _bTitle != null ? _bTitle.text : null;

        public void Init(Session s)
        {
            _s = s; _c = UIKit.Root("TimeFlow", 55); var t = _c.transform;
            // veil (long skips: the world goes on in the dark), vignette + grain (a time-lapse is being shown), black fade (sleep)
            _veil = UIKit.Img(t, "Veil", new Color(0.07f, 0.055f, 0.045f, 0f), Vector2.zero, Vector2.one);
            _vignette = UIKit.Img(t, "Vignette", new Color(0.02f, 0.014f, 0.01f, 0f), Vector2.zero, Vector2.one); _vignette.sprite = VignetteSprite();
            var grainGo = UIKit.Rect(t, "Grain", Vector2.zero, Vector2.one); _grain = grainGo.gameObject.AddComponent<RawImage>(); _grain.texture = GrainTexture(); _grain.color = new Color(1, 1, 1, 0); _grain.raycastTarget = false;
            _fade = UIKit.Img(t, "Fade", new Color(0, 0, 0, 0), Vector2.zero, Vector2.one);
            BuildDial(t); BuildBanner(t); BuildCard(t); BuildLine(t); BuildPop(t);
            _c.enabled = false;
        }

        public void Destroy() { if (_c != null) UnityEngine.Object.Destroy(_c.gameObject); }

        // ------------------------------------------------------------------ building
        void BuildDial(Transform t)
        {
            _dial = UIKit.Rect(t, "Dial", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-80, 150), new Vector2(80, 310));
            _dialCg = _dial.gameObject.AddComponent<CanvasGroup>(); _dialCg.alpha = 0;
            var glow = UIKit.Img(_dial, "Glow", Pal.A(Pal.Ink, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-98, -98), new Vector2(98, 98)); glow.sprite = DiscSprite(true);
            _face = UIKit.Img(_dial, "Face", new Color(0.09f, 0.065f, 0.05f, 0.96f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-72, -72), new Vector2(72, 72)); _face.sprite = DiscSprite(false);
            _rim = UIKit.Img(_dial, "Rim", Pal.A(Pal.Gold, 0.95f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-76, -76), new Vector2(76, 76)); _rim.sprite = RingSprite(3.2f);
            var inner = UIKit.Img(_dial, "Inner", Pal.A(Pal.Gold, 0.35f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-62, -62), new Vector2(62, 62)); inner.sprite = RingSprite(1.2f);
            _arc = UIKit.Img(_dial, "Arc", Pal.A(new Color(0.93f, 0.78f, 0.47f), 0.9f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-84, -84), new Vector2(84, 84));
            _arc.sprite = RingSprite(2.2f); _arc.type = Image.Type.Filled; _arc.fillMethod = Image.FillMethod.Radial360; _arc.fillOrigin = (int)Image.Origin360.Top; _arc.fillClockwise = true; _arc.fillAmount = 0;
            // twelve hour marks (brass), the quarters longer
            for (int i = 0; i < 12; i++)
            {
                var m = UIKit.Img(_dial, "Mark" + i, Pal.A(Pal.Gold, i % 3 == 0 ? 0.95f : 0.6f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                m.rectTransform.sizeDelta = new Vector2(i % 3 == 0 ? 4 : 2, i % 3 == 0 ? 12 : 7); m.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                float a = i * 30f * Mathf.Deg2Rad; m.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 60f; m.rectTransform.localRotation = Quaternion.Euler(0, 0, -i * 30f);
            }
            _hourHand = Hand("Hour", 5f, 38f, Pal.A(Pal.Text, 0.95f));
            _minHand = Hand("Minute", 3f, 56f, Pal.A(Pal.Gold, 1f));
            var hub = UIKit.Img(_dial, "Hub", Pal.Gold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-6, -6), new Vector2(6, 6)); hub.sprite = DiscSprite(false);
            _dialTime = UIKit.Text(_dial, "Time", "", 30, Pal.Text, TextAlignmentOptions.Top, Fonts.Serif, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-260, -46), new Vector2(260, -6));
            _dialTime.textWrappingMode = TextWrappingModes.NoWrap;
            _dialSub = UIKit.Text(_dial, "Sub", "", 20, Pal.A(Pal.TextDim, 0.95f), TextAlignmentOptions.Top, Fonts.Serif, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-360, -76), new Vector2(360, -46));
            _dialSub.textWrappingMode = TextWrappingModes.NoWrap;
        }

        Image Hand(string name, float w, float len, Color c)
        {
            var h = UIKit.Img(_dial, name, c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            h.rectTransform.pivot = new Vector2(0.5f, 0.08f); h.rectTransform.sizeDelta = new Vector2(w, len); h.rectTransform.anchoredPosition = Vector2.zero;
            return h;
        }

        void BuildBanner(Transform t)
        {
            // top centre, under the butler's broadcast plate (-44..-214) and clear of the toast column on the right
            _banner = UIKit.Rect(t, "Banner", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-380, -330), new Vector2(380, -226));
            _bannerCg = _banner.gameObject.AddComponent<CanvasGroup>();
            _bannerPanel = UIKit.Slant(_banner, "P", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Pal.A(new Color(0.24f, 0.05f, 0.07f), 0.97f), Pal.A(Pal.Ink, 0.97f), Pal.A(Pal.Gold, 0.95f), 0);
            _bannerPanel.Glow = 0.3f;
            _bTitle = UIKit.Text(_banner, "Title", "", 34, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Title, Vector2.zero, Vector2.one, new Vector2(34, 10), new Vector2(-150, -12));
            _bTitle.textWrappingMode = TextWrappingModes.NoWrap; _bTitle.enableAutoSizing = true; _bTitle.fontSizeMin = 22; _bTitle.fontSizeMax = 34; _bTitle.overflowMode = TextOverflowModes.Ellipsis;
            _bSub = UIKit.Text(_banner, "Sub", "", 20, Pal.A(Pal.TextDim, 0.95f), TextAlignmentOptions.BottomLeft, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(36, 12), new Vector2(-150, -10));
            _bSub.textWrappingMode = TextWrappingModes.NoWrap; _bSub.overflowMode = TextOverflowModes.Ellipsis;
            _bClock = UIKit.Text(_banner, "Clock", "", 22, Pal.A(Pal.Gold, 0.95f), TextAlignmentOptions.TopRight, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(20, 14), new Vector2(-26, -14));
            _bHint = UIKit.Text(_banner, "Hint", "", 18, Pal.A(Pal.Gold, 0.9f), TextAlignmentOptions.BottomRight, Fonts.Bold, Vector2.zero, Vector2.one, new Vector2(20, 12), new Vector2(-26, -10));
            _banner.gameObject.SetActive(false);
        }

        void BuildCard(Transform t)
        {
            _card = UIKit.Rect(t, "Result", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-430, 128), new Vector2(430, 300));
            _cardCg = _card.gameObject.AddComponent<CanvasGroup>();
            var p = UIKit.Slant(_card, "P", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Pal.A(Pal.Panel2, 0.98f), Pal.A(Pal.Ink, 0.98f), Pal.A(Pal.Gold, 0.85f), 0); p.Glow = 0.2f;
            _cTitle = UIKit.Text(_card, "Title", "", 28, Pal.Gold, TextAlignmentOptions.TopLeft, Fonts.Title, Vector2.zero, Vector2.one, new Vector2(34, 12), new Vector2(-30, -14));
            _cTitle.textWrappingMode = TextWrappingModes.NoWrap; _cTitle.overflowMode = TextOverflowModes.Ellipsis;
            _cBody = UIKit.Text(_card, "Body", "", 21, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(36, 14), new Vector2(-30, -54));
            _cBody.lineSpacing = 6; _cBody.overflowMode = TextOverflowModes.Ellipsis;
            _card.gameObject.SetActive(false);
        }

        void BuildLine(Transform t)
        {
            _line = UIKit.Rect(t, "Line", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-640, 40), new Vector2(640, 150));
            _lineCg = _line.gameObject.AddComponent<CanvasGroup>();
            UIKit.Slant(_line, "P", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Pal.A(Pal.Panel2, 0.95f), Pal.A(Pal.Ink, 0.95f), Pal.A(Pal.Gold, 0.7f), 0).EdgeLeftOnly = true;
            _lWho = UIKit.Text(_line, "Who", "", 20, Pal.Gold, TextAlignmentOptions.TopLeft, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(34, 10), new Vector2(-30, -10));
            _lText = UIKit.Text(_line, "T", "", 26, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(36, 10), new Vector2(-30, -40));
            _line.gameObject.SetActive(false);
        }

        void BuildPop(Transform t)
        {
            _pop = UIKit.Rect(t, "Pop", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-340, 110), new Vector2(340, 150));
            _popCg = _pop.gameObject.AddComponent<CanvasGroup>();
            var p = UIKit.Slant(_pop, "P", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Pal.A(Pal.Ink, 0.9f), Pal.A(Pal.Panel, 0.85f), Pal.A(Pal.Gold, 0.7f), 0); p.EdgeLeftOnly = true;
            _popText = UIKit.Text(_pop, "T", "", 21, Pal.Text, TextAlignmentOptions.Center, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-12, 0));
            _popText.textWrappingMode = TextWrappingModes.NoWrap;
            _pop.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ the dial
        public void ShowDial(DialMode mode)
        {
            _mode = mode; _dialWant = 1f;
            switch (mode)
            {
                case DialMode.Center: Place(_dial, new Vector2(0.5f, 0.5f), new Vector2(0, 60), 1.45f); break;
                case DialMode.TopRight: Place(_dial, new Vector2(1, 1), new Vector2(-150, -150), 0.62f); break;
                default: Place(_dial, new Vector2(0.5f, 0), new Vector2(0, 270), 1f); break;   // above the system note (y 60–104)
            }
            _dialSub.text = "";
        }
        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, float scale)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.sizeDelta = new Vector2(160, 160); rt.anchoredPosition = pos; rt.localScale = Vector3.one * scale;
        }
        public void HideDial() { _dialWant = 0f; }
        /// <summary>now: gone this frame (a menu opened over it), with its little note.</summary>
        public void HideDial(bool now)
        {
            _dialWant = 0f;
            if (!now) return;
            _dialA = 0f; _dialCg.alpha = 0f;
            if (_pop.gameObject.activeSelf) _popUntil = Mathf.Min(_popUntil, Time.unscaledTime);
        }
        public bool DialShowing => _dialA > 0.01f;

        /// <summary>Hands follow the game clock; the thin arc is how much of the skip has passed.</summary>
        public void SetDial(double clock, float progress, string sub = null)
        {
            double m = clock % 1440.0; if (m < 0) m += 1440;
            float minute = (float)(m % 60.0), hour = (float)((m / 60.0) % 12.0);
            _minHand.rectTransform.localRotation = Quaternion.Euler(0, 0, -minute / 60f * 360f);
            _hourHand.rectTransform.localRotation = Quaternion.Euler(0, 0, -hour / 12f * 360f);
            _arc.fillAmount = Mathf.Clamp01(progress);
            _dialTime.text = _mode == DialMode.Center ? $"{ClockFmt.Day(clock)}일째 · {Clock12(clock)}" : Clock12(clock);
            if (sub != null) _dialSub.text = sub;
        }

        // ------------------------------------------------------------------ veil / effects / fade
        /// <summary>0 = clear .. 0.55 = the long-skip veil (the house goes on in the dark).</summary>
        public void Veil(float a) { _veilWant = a; }
        /// <summary>Vignette and a faint grain while the world is being fast-forwarded.</summary>
        public void Effects(bool on) { _fxWant = on ? 1f : 0f; }
        public void FadeTo(float a, float secs)
        {
            _fadeFrom = _fade.color.a; _fadeTo = a; _fadeT0 = Time.unscaledTime; _fadeSecs = Mathf.Max(0.01f, secs);
            if (a > 0.001f) _c.enabled = true;
        }
        public float FadeAlpha => _fade != null ? _fade.color.a : 0f;
        public void FadeNow(float a) { _fadeSecs = -1f; _fade.color = new Color(0, 0, 0, a); if (a > 0.001f) _c.enabled = true; }

        // ------------------------------------------------------------------ banner / card / line / pop
        /// <summary>Why a wait stopped: title large, what/where dim, the clock; "T 계속 기다리기" when it can go on.</summary>
        public void Banner(TimeStop st, string hint = null)
        {
            if (st == null || string.IsNullOrEmpty(st.Title)) return;
            _bTitle.text = LineBank.FixParticles(st.Title);
            _bSub.text = string.IsNullOrEmpty(st.Sub) ? "" : LineBank.FixParticles(st.Sub);
            _bClock.text = Clock12(S.Clock);
            _bHint.text = hint ?? "";
            bool critical = st.Class == StopClass.Critical;
            _bannerPanel.Top = critical ? Pal.A(new Color(0.34f, 0.04f, 0.06f), 0.97f) : Pal.A(new Color(0.2f, 0.07f, 0.07f), 0.97f);
            _bannerPanel.Edge = critical ? Pal.A(new Color(0.95f, 0.72f, 0.4f), 1f) : Pal.A(Pal.Gold, 0.85f); _bannerPanel.Refresh();
            // the title decides the height: a long sub line never covers it
            _bSub.rectTransform.offsetMax = new Vector2(string.IsNullOrEmpty(_bHint.text) ? -26 : -210, -10);
            _banner.gameObject.SetActive(true); _bannerCg.alpha = 1f; _bannerUntil = Time.unscaledTime + 3.5f; _c.enabled = true;
            LastBannerAt = Time.unscaledTime; LastBannerKind = st.Kind; LastBannerActor = st.Actor;
            if (st.Kind == StopKind.Knock) Sfx.Play("door_knock", null, 0.5f);
            else if (st.Class == StopClass.Social) Sfx.Play("chime", null, 0.4f);
        }

        /// <summary>What came of a pastime or of time spent together: title, effect lines, how it ended.</summary>
        public void ResultCard(string title, IList<string> lines, string footer, float secs = 4.5f)
        {
            var body = new System.Text.StringBuilder();
            if (lines != null) foreach (var l in lines.Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(4)) { if (body.Length > 0) body.Append('\n'); body.Append("<color=#B8AC98>·</color> ").Append(LineBank.FixParticles(l)); }
            if (!string.IsNullOrEmpty(footer)) { if (body.Length > 0) body.Append('\n'); body.Append("<size=88%><color=#C9A86A>").Append(LineBank.FixParticles(footer)).Append("</color></size>"); }
            if (string.IsNullOrEmpty(title) && body.Length == 0) return;
            _cTitle.text = title ?? ""; _cBody.text = body.ToString();
            float h = 70f + Mathf.Max(28f, _cBody.GetPreferredValues(_cBody.text, 790f, 0).y);
            _card.offsetMin = new Vector2(-430, 128); _card.offsetMax = new Vector2(430, 128 + Mathf.Min(260f, h));
            _card.gameObject.SetActive(true); _cardCg.alpha = 1; _cardUntil = Time.unscaledTime + secs; _c.enabled = true;
        }

        /// <summary>A line said while spending time together (speaker in gold, the line below).</summary>
        public void SceneLine(string who, string text, float secs = 3.4f)
        {
            if (string.IsNullOrEmpty(text)) return;
            _lWho.text = who == Cast.Player ? "김민혁" : Cast.NameOf(who); _lText.text = LineBank.FixParticles(LineBank.Pages(text).FirstOrDefault() ?? text);
            _line.gameObject.SetActive(true); _lineCg.alpha = 1; _lineUntil = Time.unscaledTime + secs; _c.enabled = true;
        }
        public void HideLine() { _lineUntil = Mathf.Min(_lineUntil, Time.unscaledTime); }

        /// <summary>A small note by the dial ("이야기하는 사이 6분이 흘렀다", "종이 울린다").</summary>
        public void Pop(string text, float secs = 2.2f)
        {
            if (string.IsNullOrEmpty(text)) return;
            _popText.text = LineBank.FixParticles(text); float w = Mathf.Min(900f, _popText.GetPreferredValues(_popText.text).x + 60f);
            _pop.offsetMin = new Vector2(-w / 2, 110); _pop.offsetMax = new Vector2(w / 2, 150);
            _pop.gameObject.SetActive(true); _popCg.alpha = 1; _popUntil = Time.unscaledTime + secs; _c.enabled = true;
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            if (_c == null) return;
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            _veilA = Mathf.MoveTowards(_veilA, _veilWant, dt * 1.6f);
            _veil.color = new Color(0.07f, 0.055f, 0.045f, _veilA);
            _fxA = Mathf.MoveTowards(_fxA, _fxWant, dt * 3f);
            _vignette.color = new Color(0.02f, 0.014f, 0.01f, 0.55f * _fxA);
            // film grain: fine (about 1.5 screen pixels a grain), faint (≤ 4 %), a new pattern every frame — never coarse blocks
            _grain.color = new Color(1, 1, 1, 0.035f * _fxA);
            if (_fxA > 0.001f)
            {
                float gw = Mathf.Max(1f, Screen.width / (GrainSize * 1.5f)), gh = Mathf.Max(1f, Screen.height / (GrainSize * 1.5f));
                _grain.uvRect = new Rect(UnityEngine.Random.value, UnityEngine.Random.value, gw, gh);
            }
            if (_fadeSecs > 0)
            {
                float k = Mathf.Clamp01((now - _fadeT0) / _fadeSecs);
                _fade.color = new Color(0, 0, 0, Mathf.Lerp(_fadeFrom, _fadeTo, k));
                if (k >= 1f) _fadeSecs = -1f;
            }
            _dialA = Mathf.MoveTowards(_dialA, _dialWant, dt * (_dialWant > _dialA ? 5f : 2.5f));
            _dialCg.alpha = _dialA;
            Fade(_banner, _bannerCg, _bannerUntil, now, 0.35f);
            Fade(_card, _cardCg, _cardUntil, now, 0.4f);
            Fade(_line, _lineCg, _lineUntil, now, 0.3f);
            Fade(_pop, _popCg, _popUntil, now, 0.35f);
            // the court and the reveal own the screen: banners and cards wait underneath (never over a verdict)
            bool courtUp = (_s.Trial?.Active ?? false) || (_s.Reveal?.Active ?? false);
            bool any = _veilA > 0.001f || _fxA > 0.001f || _fade.color.a > 0.001f || _dialA > 0.001f || _banner.gameObject.activeSelf || _card.gameObject.activeSelf || _line.gameObject.activeSelf || _pop.gameObject.activeSelf;
            bool on = any && !courtUp;
            if (_c.enabled != on) _c.enabled = on;
        }

        static void Fade(RectTransform rt, CanvasGroup cg, float until, float now, float tail)
        {
            if (!rt.gameObject.activeSelf) return;
            float left = until - now;
            if (left <= 0) { rt.gameObject.SetActive(false); return; }
            cg.alpha = Mathf.Clamp01(left / tail);
        }

        // ------------------------------------------------------------------ plain times
        /// <summary>"오후 3:40", "밤 10:00", "새벽 3:10", "오전 7:00".</summary>
        public static string Clock12(double t)
        {
            int m = (int)Math.Floor(t % 1440.0); if (m < 0) m += 1440; int h = m / 60, mm = m % 60; int h12 = h % 12 == 0 ? 12 : h % 12;
            string part = h < 5 ? "새벽" : h < 12 ? "오전" : h < 21 ? "오후" : "밤";
            return $"{part} {h12}:{mm:00}";
        }
        /// <summary>"2시간 5분", "40분", "1시간".</summary>
        public static string Span(double minutes)
        {
            int m = Math.Max(0, (int)Math.Round(minutes)); int h = m / 60, r = m % 60;
            if (h == 0) return $"{Math.Max(1, r)}분";
            return r == 0 ? $"{h}시간" : $"{h}시간 {r}분";
        }

        // ------------------------------------------------------------------ procedural sprites
        static Sprite _disc, _discSoft, _vig; static readonly Dictionary<float, Sprite> _rings = new Dictionary<float, Sprite>(); static Texture2D _grainTex;
        static Sprite DiscSprite(bool soft)
        {
            if (soft ? _discSoft != null : _disc != null) return soft ? _discSoft : _disc;
            int n = 128; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = x - n / 2f + 0.5f, dy = y - n / 2f + 0.5f; float r = Mathf.Sqrt(dx * dx + dy * dy) / (n * 0.5f);
                float a = soft ? Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r) * 1.6f : Mathf.Clamp01((1f - r) * n * 0.5f / 1.5f);
                px[x + y * n] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
            tex.SetPixels32(px); tex.Apply(); var sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            if (soft) _discSoft = sp; else _disc = sp; return sp;
        }
        static Sprite RingSprite(float width)
        {
            if (_rings.TryGetValue(width, out var s)) return s;
            int n = 160; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = x - n / 2f + 0.5f, dy = y - n / 2f + 0.5f; float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1 - Mathf.Abs(r - n * 0.46f) / width); px[x + y * n] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply(); s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f)); _rings[width] = s; return s;
        }
        static Sprite VignetteSprite()
        {
            if (_vig != null) return _vig;
            int w = 128, h = 72; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f; float r = Mathf.Sqrt(dx * dx * 0.9f + dy * dy * 1.1f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.55f) / 0.75f));
                px[x + y * w] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply(); _vig = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f)); return _vig;
        }
        const int GrainSize = 256;
        static Texture2D GrainTexture()
        {
            if (_grainTex != null) return _grainTex;
            // every texel a grain (soft, around mid-grey: a sum of three dice), all opaque — the image's own alpha keeps it faint
            int n = GrainSize; _grainTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var px = new Color32[n * n]; var rng = new System.Random(2323);
            for (int i = 0; i < px.Length; i++) { int v = (rng.Next(0, 256) + rng.Next(0, 256) + rng.Next(0, 256)) / 3; px[i] = new Color32((byte)v, (byte)v, (byte)v, 255); }
            _grainTex.SetPixels32(px); _grainTex.Apply(); return _grainTex;
        }
    }
}
