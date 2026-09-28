using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BL23.Game
{
    /// <summary>
    /// The discovery film's only title: 시 신 발 견 in bone white, a 1 px wax-red hairline drawn left to right with a small
    /// iron lozenge at its centre, and under it who, where and when. A slow fade with a small rise; no stamp, no splash.
    /// Lives on the cinematic canvas and keeps its own clock, so it can stay a moment after control has returned.
    /// </summary>
    public sealed class DiscoveryTitle : MonoBehaviour
    {
        static readonly Color Bone = new Color(0.91f, 0.88f, 0.80f, 1f);
        static readonly Color Hair = new Color(0.45f, 0.03f, 0.04f, 1f);
        static readonly Color Iron = new Color(0.33f, 0.32f, 0.31f, 1f);
        static readonly Color SubCol = new Color(0.72f, 0.66f, 0.58f, 1f);
        const float Fade = 0.35f, Rise = 6f, LineTime = 0.7f, LineHalfW = 260f;

        RectTransform _root; CanvasGroup _cg; TextMeshProUGUI _title, _sub; Image _line, _diamond; RectTransform _lineRt;
        float _showAt = -1f, _hideAt = -1f, _hideStart = -1f;
        Vector2 _rootPos;

        public bool Visible => _cg != null && _cg.alpha > 0.01f;

        public static DiscoveryTitle Ensure(Canvas canvas)
        {
            if (canvas == null) return null;
            var have = canvas.GetComponentInChildren<DiscoveryTitle>(true); if (have != null) return have;
            var root = UIKit.Rect(canvas.transform, "DiscoveryTitle", Vector2.zero, Vector2.one);
            var t = root.gameObject.AddComponent<DiscoveryTitle>(); t.Build(root);
            return t;
        }

        void Build(RectTransform root)
        {
            _root = root; _rootPos = root.anchoredPosition;
            _cg = root.gameObject.AddComponent<CanvasGroup>(); _cg.alpha = 0f; _cg.blocksRaycasts = false; _cg.interactable = false;
            _title = UIKit.Text(root, "Title", "시 신 발 견", 92, Bone, TextAlignmentOptions.Center, Fonts.Title, new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.44f));
            _title.characterSpacing = 22f; _title.textWrappingMode = TextWrappingModes.NoWrap;
            _lineRt = UIKit.Rect(root, "Hairline", new Vector2(0.5f, 0.292f), new Vector2(0.5f, 0.292f), new Vector2(-LineHalfW, 0f), new Vector2(-LineHalfW, 1f));
            _line = _lineRt.gameObject.AddComponent<Image>(); _line.color = Hair; _line.raycastTarget = false;
            var drt = UIKit.Rect(root, "Lozenge", new Vector2(0.5f, 0.292f), new Vector2(0.5f, 0.292f), new Vector2(-5f, -4.5f), new Vector2(5f, 5.5f));
            drt.localRotation = Quaternion.Euler(0, 0, 45f);
            _diamond = drt.gameObject.AddComponent<Image>(); _diamond.color = Iron; _diamond.raycastTarget = false;
            _sub = UIKit.Text(root, "Sub", "", 30, SubCol, TextAlignmentOptions.Center, Fonts.Serif, new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.28f));
            _sub.characterSpacing = 3f; _sub.textWrappingMode = TextWrappingModes.NoWrap;
            root.gameObject.SetActive(false);
        }

        /// <summary>Show from `at` (unscaled time) with this subline; stays until HideAt/HideNow.</summary>
        public void Show(string subline, float at)
        {
            if (_root == null) return;
            _sub.text = subline ?? ""; _showAt = at; _hideAt = -1f; _hideStart = -1f;
            _cg.alpha = 0f; _root.gameObject.SetActive(true); _root.SetAsLastSibling();
        }
        public void HideAt(float at) { _hideAt = at; }
        public void HideNow() { _showAt = -1f; _hideAt = -1f; if (_cg != null) _cg.alpha = 0f; if (_root != null) _root.gameObject.SetActive(false); }

        void Update()
        {
            if (_showAt < 0f) return;
            float now = Time.unscaledTime;
            float a = Mathf.Clamp01((now - _showAt) / Fade);
            if (_hideAt >= 0f && now >= _hideAt)
            {
                if (_hideStart < 0f) _hideStart = now;
                a = Mathf.Min(a, 1f - Mathf.Clamp01((now - _hideStart) / Fade));
                if (now - _hideStart >= Fade) { HideNow(); return; }
            }
            float e = a * a * (3f - 2f * a);
            _cg.alpha = e;
            _root.anchoredPosition = _rootPos + new Vector2(0f, -Rise * (1f - e));
            // the hairline is drawn from the left, the lozenge settles in once it passes the centre
            float lu = Mathf.Clamp01((now - _showAt - 0.1f) / LineTime); lu = 1f - (1f - lu) * (1f - lu);
            _lineRt.offsetMax = new Vector2(-LineHalfW + 2f * LineHalfW * lu, 1f);
            _diamond.color = new Color(Iron.r, Iron.g, Iron.b, Mathf.Clamp01((lu - 0.45f) / 0.2f));
        }
    }

    /// <summary>2.39:1 bars for the film (slides in and out; hidden otherwise).</summary>
    public sealed class FilmLetterbox : MonoBehaviour
    {
        RectTransform _top, _bot; float _k, _from, _to, _t0 = -1f, _dur = 0.25f;

        public static FilmLetterbox Ensure(Canvas canvas)
        {
            if (canvas == null) return null;
            var have = canvas.GetComponentInChildren<FilmLetterbox>(true); if (have != null) return have;
            var root = UIKit.Rect(canvas.transform, "FilmLetterbox", Vector2.zero, Vector2.one);
            root.SetAsFirstSibling();
            var lb = root.gameObject.AddComponent<FilmLetterbox>();
            lb._top = UIKit.Img(root, "Top", Color.black, new Vector2(0, 1), new Vector2(1, 1)).rectTransform;
            lb._bot = UIKit.Img(root, "Bottom", Color.black, new Vector2(0, 0), new Vector2(1, 0)).rectTransform;
            lb.Apply(0f);
            return lb;
        }

        public void Slide(bool show, float secs) { _from = _k; _to = show ? 1f : 0f; _t0 = Time.unscaledTime; _dur = Mathf.Max(0.01f, secs); }
        public void HideNow() { _t0 = -1f; Apply(0f); }

        void Apply(float k)
        {
            _k = k;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            float bar = Mathf.Max(0f, (1f - aspect / 2.39f) * 0.5f) * k;
            _top.anchorMin = new Vector2(0, 1f - bar); _top.anchorMax = new Vector2(1, 1); _top.offsetMin = _top.offsetMax = Vector2.zero;
            _bot.anchorMin = new Vector2(0, 0); _bot.anchorMax = new Vector2(1, bar); _bot.offsetMin = _bot.offsetMax = Vector2.zero;
            _top.gameObject.SetActive(bar > 0.0005f); _bot.gameObject.SetActive(bar > 0.0005f);
        }

        void Update()
        {
            if (_t0 < 0f) return;
            float u = Mathf.Clamp01((Time.unscaledTime - _t0) / _dur); u = u * u * (3f - 2f * u);
            Apply(Mathf.Lerp(_from, _to, u));
            if (u >= 1f) _t0 = -1f;
        }
    }
}
