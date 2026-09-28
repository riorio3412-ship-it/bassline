using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace BL23.Game.Cinema
{
    /// <summary>
    /// A cinematic "window" of live panes. Each page is a leaded-glass window of 1–5 angled panes; every pane shows a live
    /// camera (RenderTexture) with its own shot and in-pane move. Panes assemble one by one (light sweeping across the
    /// glass as the came is traced on), the view glides from pane to pane over the page, the narration runs beneath, and
    /// finished panes keep their last frame (the camera is released) so a page can hold moments from different times.
    /// At most four cameras are live; they are pooled. Gothic by construction: lead, gilt, deep jewel glass, candlelight.
    /// </summary>
    public sealed class PaneMontage : MonoBehaviour
    {
        public sealed class Pane
        {
            public int Index; public RectTransform Rt; public PaneGraphic Img; public LeadFrame Lead; public WipeGlow Glow;
            public List<Vector2> Shape; public Rect Bounds; public RenderTexture RT; public LiveCam Cam; public bool Overlay;
            public Vector2 WipeDir; public float Revealed; public float Drift; public string Name;
        }
        public sealed class LiveCam
        {
            public Camera Cam; public CineShot Shot; public float T0, Dur; public Pane Pane; public bool Primary;
        }

        Canvas _c; RectTransform _root, _page, _panes, _capRoot; RawImage _bgGlow; GlassBorder _border; Image _pageBack;
        TextMeshProUGUI _cap, _kicker, _hint; CanvasGroup _capCg, _rootCg; Camera _screenCam;
        readonly List<Pane> _list = new List<Pane>(); readonly List<LiveCam> _pool = new List<LiveCam>();
        Light _key, _rim, _fill; float _lightK;
        Vector2 _pagePos, _pagePosV, _pagePosT; float _pageScale = 1, _pageScaleV, _pageScaleT = 1, _pageRot, _pageRotV, _pageRotT;
        string _capFull = ""; float _capT0; int _depthSeq;
        public bool Open { get; private set; }
        public Rect PageRect { get; private set; }
        public const int MaxLive = 4;

        public static PaneMontage Create(Transform host, int order)
        {
            var go = new GameObject("PaneMontage"); if (host != null) go.transform.SetParent(host, false); DontDestroyOnLoad(go);
            var m = go.AddComponent<PaneMontage>(); m.Build(order); return m;
        }

        void Build(int order)
        {
            _c = UIKit.Root("CineMontage", order); _root = UIKit.Rect(_c.transform, "Root", Vector2.zero, Vector2.one); _rootCg = _root.gameObject.AddComponent<CanvasGroup>(); _rootCg.blocksRaycasts = false;
            UIKit.Img(_root, "Black", new Color(0.012f, 0.009f, 0.008f, 1f), Vector2.zero, Vector2.one);
            var g = UIKit.Rect(_root, "Glow", new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f)); g.sizeDelta = new Vector2(2600, 1500);
            _bgGlow = g.gameObject.AddComponent<RawImage>(); _bgGlow.texture = GTex.Glow; _bgGlow.color = new Color(0.55f, 0.32f, 0.14f, 0.16f); _bgGlow.raycastTarget = false;
            _page = UIKit.Rect(_root, "Page", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var brt = UIKit.Rect(_page, "Border", Vector2.zero, Vector2.one, new Vector2(-30, -30), new Vector2(30, 30)); _border = brt.gameObject.AddComponent<GlassBorder>(); _border.raycastTarget = false; _border.Band = 26f;
            _pageBack = UIKit.Img(_page, "Lead", new Color(0.028f, 0.024f, 0.022f, 1f), Vector2.zero, Vector2.one);
            _panes = UIKit.Rect(_page, "Panes", Vector2.zero, Vector2.one);
            // the narration: under the window, a gilt rule, the voice in the serif
            _capRoot = UIKit.Rect(_root, "Caption", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 186)); _capCg = _capRoot.gameObject.AddComponent<CanvasGroup>();
            var bed = UIKit.Rect(_capRoot, "Bed", Vector2.zero, Vector2.one, new Vector2(-200, -60), new Vector2(200, 40)); var bi = bed.gameObject.AddComponent<RawImage>(); bi.texture = GTex.Glow; bi.color = new Color(0, 0, 0, 0.85f); bi.raycastTarget = false;
            UIKit.Img(_capRoot, "Rule", GPal.A(GPal.Brass, 0.7f), new Vector2(0.3f, 1), new Vector2(0.7f, 1), new Vector2(0, -38), new Vector2(0, -37));
            var loz = TrialFx.Centered(_capRoot, "Loz", new Vector2(9, 9)); loz.anchorMin = loz.anchorMax = new Vector2(0.5f, 1); loz.anchoredPosition = new Vector2(0, -37.5f); loz.localRotation = Quaternion.Euler(0, 0, 45); var li = loz.gameObject.AddComponent<Image>(); li.color = GPal.Gilt; li.raycastTarget = false;
            _cap = Goth.Text(_capRoot, "Text", "", 31, GPal.Bone, TextAlignmentOptions.Top, false, new Vector2(0.1f, 0), new Vector2(0.9f, 1), new Vector2(0, 24), new Vector2(0, -56)); _cap.lineSpacing = 8;
            _kicker = Goth.Text(_capRoot, "Kicker", "", 18, GPal.A(GPal.Brass, 0.95f), TextAlignmentOptions.TopLeft, true, new Vector2(0, 1), new Vector2(0.3f, 1), new Vector2(60, -52), new Vector2(0, -26)); _kicker.characterSpacing = 6;
            _hint = Goth.Text(_capRoot, "Hint", "E  다음      Esc  건너뛰기", 16, GPal.A(GPal.Smoke, 0.75f), TextAlignmentOptions.TopRight, false, new Vector2(0.7f, 1), new Vector2(1, 1), new Vector2(0, -52), new Vector2(-60, -26)); _hint.characterSpacing = 2;
            _c.enabled = false;
            var sc = new GameObject("CineScreenCam", typeof(Camera)); sc.transform.SetParent(transform, false); _screenCam = sc.GetComponent<Camera>(); _screenCam.cullingMask = 0; _screenCam.clearFlags = CameraClearFlags.SolidColor; _screenCam.backgroundColor = Color.black; _screenCam.depth = -5; _screenCam.enabled = false;
            var sd = sc.AddComponent<UniversalAdditionalCameraData>(); sd.renderPostProcessing = false; sd.renderShadows = false;
        }

        public void Destroy() { CloseAll(); foreach (var lc in _pool) if (lc.Cam != null) { CineDof.Forget(lc.Cam); UnityEngine.Object.Destroy(lc.Cam.gameObject); } _pool.Clear(); if (_c) UnityEngine.Object.Destroy(_c.gameObject); if (_key) UnityEngine.Object.Destroy(_key.gameObject); if (_rim) UnityEngine.Object.Destroy(_rim.gameObject); UnityEngine.Object.Destroy(gameObject); }

        // ---------------------------------------------------------------- open / close
        public void Show()
        {
            Open = true; _c.enabled = true; _screenCam.enabled = true; _rootCg.alpha = 1; Canvas.ForceUpdateCanvases();
            var cr = ((RectTransform)_c.transform).rect;
            float mx = 84, top = 64, bot = 196; float w = cr.width - mx * 2, h = cr.height - top - bot;
            PageRect = new Rect(-w * 0.5f, -h * 0.5f, w, h);
            _page.sizeDelta = new Vector2(w, h); _page.anchoredPosition = Vector2.zero; _pageBase = new Vector2(0, (bot - top) * 0.5f);
            _pagePos = _pagePosT = Vector2.zero; _pageScale = _pageScaleT = 1; _pageRot = _pageRotT = 0; ApplyPage();
            _cap.text = ""; _kicker.text = ""; _capFull = "";
        }
        Vector2 _pageBase;
        public void Hide() { CloseAll(); Open = false; _c.enabled = false; _screenCam.enabled = false; if (_key) _key.enabled = false; if (_rim) _rim.enabled = false; if (_fill) _fill.enabled = false; }

        void CloseAll()
        {
            foreach (var p in _list) { if (p.Cam != null) ReleaseCam(p.Cam); if (p.RT != null) { p.RT.Release(); UnityEngine.Object.Destroy(p.RT); } if (p.Rt != null) UnityEngine.Object.Destroy(p.Rt.gameObject); }
            _list.Clear();
        }

        // ---------------------------------------------------------------- layouts
        /// <summary>Lay out a page: 'layout' names a partition (angled cuts, lancets, a roundel); panes start hidden.</summary>
        public List<Pane> NewPage(string layout, int seed = 0)
        {
            CloseAll();
            _page.gameObject.SetActive(true); _border.Seed = 3 + seed; _border.Refresh();
            var polys = Layout(layout, PageRect.width, PageRect.height, seed);
            var dirs = new[] { new Vector2(1f, 0.3f), new Vector2(-0.4f, -1f), new Vector2(-1f, 0.25f), new Vector2(0.3f, 1f), new Vector2(1f, -0.45f) };
            for (int i = 0; i < polys.Count; i++)
            {
                var (shape, overlay) = polys[i];
                var rt = UIKit.Rect(_panes, "Pane" + i, Vector2.zero, Vector2.one);
                var img = rt.gameObject.AddComponent<PaneGraphic>(); img.raycastTarget = false; img.Shape = shape; img.Wipe = 0f; img.WipeDir = dirs[(i + seed) % dirs.Length]; img.color = Color.white;
                var grt = UIKit.Rect(rt, "Glow", Vector2.zero, Vector2.one); var glow = grt.gameObject.AddComponent<WipeGlow>(); glow.Pane = img; glow.raycastTarget = false; glow.color = new Color(1f, 0.82f, 0.52f, 0.9f);
                var lrt = UIKit.Rect(rt, "Lead", Vector2.zero, Vector2.one); var lead = lrt.gameObject.AddComponent<LeadFrame>(); lead.raycastTarget = false; lead.Shape = shape; lead.Draw = 0f; lead.Width = overlay ? 15f : 11f;
                var p = new Pane { Index = i, Rt = rt, Img = img, Lead = lead, Glow = glow, Shape = shape, Bounds = Poly.Bounds(shape), Overlay = overlay, WipeDir = img.WipeDir };
                img.Tex = null; img.color = new Color(0.05f, 0.04f, 0.035f, 1f);   // unlit glass until its shot arrives
                _list.Add(p);
            }
            _pagePosT = Vector2.zero; _pageScaleT = 1f; _pageRotT = 0f;
            return new List<Pane>(_list);
        }

        static List<Vector2> N(float W, float H, params float[] xy)
        {
            var l = new List<Vector2>(); for (int i = 0; i + 1 < xy.Length; i += 2) l.Add(new Vector2((xy[i] - 0.5f) * W, (xy[i + 1] - 0.5f) * H)); return Poly.CCW(l);
        }

        static List<(List<Vector2>, bool)> Layout(string name, float W, float H, int seed)
        {
            const float gap = 8f; bool flip = (seed & 1) == 1;
            var res = new List<(List<Vector2>, bool)>();
            void Add(List<Vector2> p, bool overlay = false)
            {
                if (flip) { for (int i = 0; i < p.Count; i++) p[i] = new Vector2(-p[i].x, p[i].y); p.Reverse(); }
                res.Add((overlay ? p : Poly.Inset(p, gap), overlay));
            }
            switch (name)
            {
                case "single": Add(N(W, H, 0, 0, 1, 0, 1, 1, 0, 1)); break;
                case "duo":
                    Add(N(W, H, 0, 0, 0.47f, 0, 0.55f, 1, 0, 1)); Add(N(W, H, 0.47f, 0, 1, 0, 1, 1, 0.55f, 1)); break;
                case "trio":
                    Add(N(W, H, 0, 0, 0.58f, 0, 0.62f, 1, 0, 1));
                    Add(N(W, H, 0.5984f, 0.46f, 1, 0.52f, 1, 1, 0.62f, 1));
                    Add(N(W, H, 0.58f, 0, 1, 0, 1, 0.52f, 0.5984f, 0.46f)); break;
                case "quad":
                    Add(N(W, H, 0, 0.3f, 0.5392f, 0.24f, 0.6f, 1, 0, 1));
                    Add(N(W, H, 0, 0, 0.52f, 0, 0.5392f, 0.24f, 0, 0.3f));
                    Add(N(W, H, 0.5648f, 0.56f, 1, 0.62f, 1, 1, 0.6f, 1));
                    Add(N(W, H, 0.52f, 0, 1, 0, 1, 0.62f, 0.5648f, 0.56f)); break;
                case "strip":
                    Add(N(W, H, 0, 0.52f, 1, 0.46f, 1, 1, 0, 1));
                    Add(N(W, H, 0, 0, 0.5f, 0, 0.45f, 0.493f, 0, 0.52f));
                    Add(N(W, H, 0.5f, 0, 1, 0, 1, 0.46f, 0.45f, 0.493f)); break;
                case "lancets":
                    {
                        float mw = 0.34f, sw = (1f - mw - 0.04f) * 0.5f;
                        Add(Poly.Lancet(RectN(W, H, 0, 0, sw, 0.9f), 0.36f));
                        Add(Poly.Lancet(RectN(W, H, sw + 0.02f, 0, sw + 0.02f + mw, 1f), 0.34f));
                        Add(Poly.Lancet(RectN(W, H, 1 - sw, 0, 1, 0.9f), 0.36f));
                        break;
                    }
                case "rose":
                    {
                        Add(N(W, H, 0, 0, 0.47f, 0, 0.55f, 1, 0, 1)); Add(N(W, H, 0.47f, 0, 1, 0, 1, 1, 0.55f, 1));
                        var c = new Vector2((0.51f - 0.5f) * W, (0.36f - 0.5f) * H); float r = H * 0.23f;
                        res.Add((Poly.Circle(flip ? new Vector2(-c.x, c.y) : c, r, 48), true));
                        break;
                    }
                case "wide3":
                    {
                        // a tall lancet with two stacked panes beside it
                        Add(Poly.Lancet(RectN(W, H, 0, 0, 0.36f, 1f), 0.3f));
                        Add(N(W, H, 0.37f, 0.5f, 1, 0.56f, 1, 1, 0.37f, 1));
                        Add(N(W, H, 0.37f, 0, 1, 0, 1, 0.56f, 0.37f, 0.5f));
                        break;
                    }
                default: Add(N(W, H, 0, 0, 1, 0, 1, 1, 0, 1)); break;
            }
            return res;
        }
        static Rect RectN(float W, float H, float x0, float y0, float x1, float y1) => Rect.MinMaxRect((x0 - 0.5f) * W, (y0 - 0.5f) * H, (x1 - 0.5f) * W, (y1 - 0.5f) * H);

        // ---------------------------------------------------------------- panes
        /// <summary>Give a pane its live shot: a pooled camera renders into the pane's own texture; the glass lights up and the came is traced.</summary>
        public LiveCam Go(Pane p, CineShot shot, float dur, bool primary = true)
        {
            if (p == null) return null;
            // pixel size of the pane as it will be seen (zoomed a little), capped
            float sf = _c.scaleFactor * 1.18f; int w = Mathf.Clamp(Mathf.RoundToInt(p.Bounds.width * sf), 160, 1600), h = Mathf.Clamp(Mathf.RoundToInt(p.Bounds.height * sf), 120, 1000);
            float want = p.Bounds.width / Mathf.Max(1f, p.Bounds.height); if (w / (float)h > want + 0.01f) w = Mathf.RoundToInt(h * want); else h = Mathf.RoundToInt(w / want);
            if (p.RT == null || p.RT.width != w || p.RT.height != h)
            {
                if (p.RT != null) { p.RT.Release(); UnityEngine.Object.Destroy(p.RT); }
                p.RT = new RenderTexture(Mathf.Max(64, w), Mathf.Max(64, h), 24, RenderTextureFormat.ARGB32) { name = "Pane" + p.Index, antiAliasing = 1 }; p.RT.Create();
            }
            if (p.Cam != null) ReleaseCam(p.Cam);
            var lc = TakeCam(); if (lc == null) return null;
            lc.Shot = shot; lc.T0 = Time.unscaledTime; lc.Dur = Mathf.Max(0.5f, dur); lc.Pane = p; lc.Primary = primary; p.Cam = lc;
            lc.Cam.targetTexture = p.RT; lc.Cam.depth = 30 + (++_depthSeq % 500);
            shot.Apply(lc.Cam, 0f, 0f);
            // the house draws only the rooms/lights around whichever camera is looking: settle it here before the first frame
            var mv = Session.I?.World?.Mansion; if (mv != null && primary) { mv.ViewCamera = lc.Cam; mv.Cull(lc.Cam.transform.position); }
            lc.Cam.enabled = true;
            p.Img.Tex = p.RT; p.Img.color = Color.white; p.Img.Dim = 0f; p.Img.Refresh();
            if (primary) foreach (var o in _list) if (o != p && o.Cam != null) o.Cam.Primary = false;
            return lc;
        }

        /// <summary>Stop the pane's camera: the glass keeps the last frame (and drifts very slightly, so it never looks dead).</summary>
        public void Freeze(Pane p) { if (p?.Cam != null) { ReleaseCam(p.Cam); p.Cam = null; } }
        public void FreezeAll() { foreach (var p in _list) Freeze(p); }

        /// <summary>
        /// The safety net: a moment after a pane goes live, read its image back (asynchronously, once); if it came out
        /// (nearly) black — a lens inside a coat, a wall, an unlit corner — call onDark so the caller can swap in a wider shot.
        /// </summary>
        public void WatchDark(Pane p, float after, Action onDark) { if (p != null && SystemInfo.supportsAsyncGPUReadback) StartCoroutine(DarkCo(p, after, onDark)); }
        IEnumerator DarkCo(Pane p, float after, Action onDark)
        {
            float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < after) yield return null;
            var rt = p.RT; var cam = p.Cam; if (rt == null || cam == null) yield break;
            var req = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32);
            while (!req.done) yield return null;
            if (req.hasError || p.RT != rt || p.Cam != cam) yield break;
            float mean, spread, lit;
            try
            {
                // brightness, and how much the picture varies: a lens pressed into a coat or a wall gives a flat smear
                var data = req.GetData<Color32>(); double sum = 0, sum2 = 0; int n = 0, bright = 0; int step = Mathf.Max(1, data.Length / 6000);
                for (int i = 0; i < data.Length; i += step) { var c = data[i]; float y = (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f; sum += y; sum2 += y * y; n++; if (y > 0.3f) bright++; }
                mean = n > 0 ? (float)(sum / n) : 1f; spread = n > 0 ? Mathf.Sqrt(Mathf.Max(0f, (float)(sum2 / n) - mean * mean)) : 1f; lit = n > 0 ? bright / (float)n : 1f;
            }
            catch (Exception) { yield break; }
            bool bad = mean < 0.025f || (spread < 0.04f && lit < 0.01f);
            if (AutoProbe.Active) Debug.Log($"[CINE] pane {p.Index} brightness {mean:0.000} spread {spread:0.000} lit {lit:0.000}" + (bad ? " — dark/featureless, replacing the shot" : ""));
            if (bad) { try { onDark?.Invoke(); } catch (Exception e) { Debug.LogException(e); } }
        }

        LiveCam TakeCam()
        {
            var free = _pool.FirstOrDefault(c => c.Pane == null);
            if (free == null)
            {
                if (_pool.Count >= MaxLive) { var old = _pool.OrderBy(c => c.T0).First(); if (old.Pane != null) { old.Pane.Cam = null; } old.Pane = null; free = old; }
                else
                {
                    var go = new GameObject("PaneCam" + _pool.Count, typeof(Camera)); go.transform.SetParent(transform, false);
                    var cam = go.GetComponent<Camera>(); cam.enabled = false; go.AddComponent<UniversalAdditionalCameraData>();
                    BL23.Game.Mansion.MansionAtmosphere.SetupCamera(cam); cam.cullingMask = ~(1 << 5); cam.nearClipPlane = 0.03f; cam.farClipPlane = 140f;
                    CineDof.Register(cam);
                    free = new LiveCam { Cam = cam }; _pool.Add(free);
                }
            }
            return free;
        }
        void ReleaseCam(LiveCam lc) { if (lc == null) return; if (lc.Cam != null) { lc.Cam.enabled = false; lc.Cam.targetTexture = null; CineDof.Off(lc.Cam); } if (lc.Pane != null && lc.Pane.Cam == lc) lc.Pane.Cam = null; lc.Pane = null; lc.Primary = false; }

        /// <summary>Light the pane: the wipe sweeps, the came is drawn around it. Yields until done.</summary>
        public IEnumerator Assemble(Pane p, float secs = 0.7f, bool sound = true)
        {
            if (p == null || p.Rt == null || p.Img == null) yield break; if (sound) Sfx.Play(p.Overlay ? "candle_flare" : "quill", null, p.Overlay ? 0.45f : 0.3f);
            float t0 = Time.unscaledTime; var basePos = Vector2.zero; var off = -p.WipeDir.normalized * 26f;
            while (Time.unscaledTime - t0 < secs)
            {
                if (p.Rt == null || p.Img == null || p.Glow == null || p.Lead == null) yield break;   // the page closed under us
                float k = Mathf.Clamp01((Time.unscaledTime - t0) / secs); float e = 1f - Mathf.Pow(1f - k, 3f);
                p.Img.Wipe = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.15f)); p.Img.Refresh(); p.Glow.Refresh();
                p.Lead.Draw = Mathf.Clamp01(k * 1.25f); p.Lead.Refresh();
                p.Rt.anchoredPosition = basePos + off * (1f - e);
                yield return null;
            }
            if (p.Rt == null || p.Img == null || p.Glow == null || p.Lead == null) yield break;
            p.Img.Wipe = 1f; p.Img.Refresh(); p.Glow.Refresh(); p.Lead.Draw = 1f; p.Lead.Refresh(); p.Rt.anchoredPosition = basePos; p.Revealed = 1f;
        }

        /// <summary>Panes fold shut one after another (like shutters), then the page is gone.</summary>
        public IEnumerator Fold(float secs = 0.55f)
        {
            Sfx.Play("parchment", null, 0.45f);
            float t0 = Time.unscaledTime; int n = _list.Count;
            while (Time.unscaledTime - t0 < secs + n * 0.06f)
            {
                for (int i = 0; i < n; i++)
                {
                    if (i >= _list.Count) break; var p = _list[i]; if (p.Img == null || p.Glow == null || p.Lead == null) continue; float k = Mathf.Clamp01((Time.unscaledTime - t0 - i * 0.06f) / secs); float e = k * k;
                    p.Img.WipeDir = -p.WipeDir; p.Img.Wipe = 1f - e; p.Img.Refresh(); p.Glow.Refresh(); p.Lead.Draw = 1f - e; p.Lead.Refresh();
                }
                yield return null;
            }
            CloseAll();
        }
        /// <summary>Candle-flare cut: a warm bloom swells over the window, the page is gone under it.</summary>
        public IEnumerator Flare(float secs = 0.45f)
        {
            Sfx.Play("candle_flare", null, 0.6f);
            var rt = UIKit.Rect(_root, "Flare", new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f)); rt.sizeDelta = new Vector2(600, 400);
            var im = rt.gameObject.AddComponent<RawImage>(); im.texture = GTex.Glow; im.raycastTarget = false;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < secs)
            {
                if (rt == null) yield break;
                float k = Mathf.Clamp01((Time.unscaledTime - t0) / secs);
                rt.sizeDelta = Vector2.Lerp(new Vector2(600, 400), new Vector2(4200, 2600), k * k); im.color = new Color(1f, 0.78f, 0.48f, Mathf.Sin(k * Mathf.PI) * 0.85f);
                if (k > 0.5f && _list.Count > 0) CloseAll();
                yield return null;
            }
            CloseAll(); if (rt != null) UnityEngine.Object.Destroy(rt.gameObject);
        }
        /// <summary>The whole window slides away to one side as if the view were moving along a wall of windows.</summary>
        public IEnumerator Glide(float dir = -1f, float secs = 0.6f)
        {
            Sfx.Play("parchment", null, 0.35f);
            float t0 = Time.unscaledTime; var from = _pagePos;
            while (Time.unscaledTime - t0 < secs) { float k = Mathf.Clamp01((Time.unscaledTime - t0) / secs); float e = k * k * (3 - 2 * k); _pagePos = _pagePosT = from + new Vector2(dir * PageRect.width * 1.25f * e, 0); _pageRotT = _pageRot = dir * 2f * e; ApplyPage(); yield return null; }
            CloseAll(); _pagePos = _pagePosT = new Vector2(-dir * PageRect.width * 1.2f, 0); _pageRot = _pageRotT = -dir * 2f; ApplyPage(); _pagePosT = Vector2.zero; _pageRotT = 0f;
        }

        // ---------------------------------------------------------------- the view over the page
        /// <summary>Glide the view towards a pane (zoomed in on it, the rest of the window still around it).</summary>
        public void Focus(Pane p, float tilt = 0f)
        {
            if (p == null) { _pagePosT = Vector2.zero; _pageScaleT = 1f; _pageRotT = 0f; foreach (var o in _list) o.Img.Dim = 0f; RefreshAll(); return; }
            var c = p.Bounds.center; var s = p.Bounds.size;
            float z = Mathf.Clamp(Mathf.Min(PageRect.width * 0.8f / Mathf.Max(1, s.x), PageRect.height * 0.84f / Mathf.Max(1, s.y)), 1f, 1.32f);
            _pageScaleT = z; _pagePosT = -c * z * 0.86f; _pageRotT = tilt;
            foreach (var o in _list) o.Img.Dim = o == p || o.Revealed < 0.5f ? 0f : 0.42f;
            RefreshAll();
        }
        void RefreshAll() { foreach (var o in _list) o.Img.Refresh(); }
        void ApplyPage() { _page.anchoredPosition = _pageBase + _pagePos; _page.localScale = Vector3.one * _pageScale; _page.localRotation = Quaternion.Euler(0, 0, _pageRot); }

        // ---------------------------------------------------------------- narration
        public void Caption(string text, string kicker = null)
        {
            _capFull = text ?? ""; _capT0 = Time.unscaledTime; _cap.text = _capFull; _cap.maxVisibleCharacters = 0; if (kicker != null) _kicker.text = kicker;
        }
        public bool CaptionDone => _cap.maxVisibleCharacters >= _capFull.Length;
        public void CaptionFinish() { _capT0 = -999f; }
        public void Hint(string s) { _hint.text = s ?? ""; }

        /// <summary>A sound word, sparingly: engraved gilt letters pressed into the corner of a pane.</summary>
        public IEnumerator SoundWord(Pane p, string word, Vector2 at, float angle = -7f)
        {
            if (p == null || p.Rt == null || string.IsNullOrEmpty(word)) yield break;
            var pos = new Vector2(Mathf.Lerp(p.Bounds.xMin, p.Bounds.xMax, at.x), Mathf.Lerp(p.Bounds.yMin, p.Bounds.yMax, at.y));
            var rt = UIKit.Rect(p.Rt, "Word", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)); rt.sizeDelta = new Vector2(420, 160); rt.anchoredPosition = pos; rt.localRotation = Quaternion.Euler(0, 0, angle);
            var sh = UIKit.Rect(rt, "Shade", Vector2.zero, Vector2.one, new Vector2(-30, -20), new Vector2(30, 20)); var si = sh.gameObject.AddComponent<RawImage>(); si.texture = GTex.Glow; si.color = new Color(0, 0, 0, 0.55f); si.raycastTarget = false;
            var t = Goth.Engraved(rt, "T", word, 78, GPal.Gilt); t.characterSpacing = 10; t.outlineWidth = 0.24f; t.fontStyle = FontStyles.Bold;
            var cg = rt.gameObject.AddComponent<CanvasGroup>();
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 2.2f && rt != null)
            {
                float k = Time.unscaledTime - t0;
                float s = k < 0.14f ? Mathf.Lerp(1.5f, 0.96f, k / 0.14f) : k < 0.26f ? Mathf.Lerp(0.96f, 1f, (k - 0.14f) / 0.12f) : 1f + (k - 0.26f) * 0.02f;
                rt.localScale = Vector3.one * s; cg.alpha = k < 0.1f ? k / 0.1f : k > 1.7f ? Mathf.Clamp01(1f - (k - 1.7f) / 0.5f) : 1f;
                yield return null;
            }
            if (rt != null) UnityEngine.Object.Destroy(rt.gameObject);
        }

        /// <summary>A full-window title card (no panes): engraved words over candlelight.</summary>
        public IEnumerator Title(string pre, string title, string sub, float hold, Func<bool> skip)
        {
            _page.gameObject.SetActive(false); _capCg.alpha = 0f;
            var rt = UIKit.Rect(_root, "Title", Vector2.zero, Vector2.one); var cg = rt.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0;
            var gl = Goth.Glow(rt, "Glow", GPal.A(GPal.Candle, 0.2f), new Vector2(1500, 700), new Vector2(0, 30));
            // an empty lancet window drawn in lead and gilt around the words: the windows to come
            var win = UIKit.Rect(rt, "Window", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)); win.sizeDelta = new Vector2(980, 700); win.anchoredPosition = new Vector2(0, 30);
            var wl = win.gameObject.AddComponent<LeadFrame>(); wl.raycastTarget = false; wl.Shape = Poly.Lancet(new Rect(-470, -330, 940, 660), 0.5f, 20); wl.Width = 7f; wl.Bosses = false; wl.Lead = new Color(0.05f, 0.035f, 0.025f, 0.9f); wl.color = new Color(1, 1, 1, 0.75f);
            var wl2 = UIKit.Rect(win, "Inner", Vector2.zero, Vector2.one).gameObject.AddComponent<LeadFrame>(); wl2.raycastTarget = false; wl2.Shape = Poly.Inset(Poly.Lancet(new Rect(-470, -330, 940, 660), 0.5f, 20), 16f); wl2.Width = 3f; wl2.Bosses = false; wl2.color = new Color(1, 1, 1, 0.45f);
            var p = Goth.Text(rt, "Pre", pre ?? "", 26, GPal.A(GPal.Brass, 0.9f), TextAlignmentOptions.Center, false, new Vector2(0, 0.66f), new Vector2(1, 0.72f)); p.characterSpacing = 14;
            var t = Goth.Engraved(rt, "T", title, 104); t.rectTransform.anchorMin = new Vector2(0, 0.47f); t.rectTransform.anchorMax = new Vector2(1, 0.66f); t.characterSpacing = 16;
            var rule = UIKit.Img(rt, "Rule", GPal.A(GPal.Brass, 0.8f), new Vector2(0.36f, 0.455f), new Vector2(0.64f, 0.455f), new Vector2(0, -1), new Vector2(0, 1));
            var s = Goth.Text(rt, "Sub", sub ?? "", 28, GPal.Bone, TextAlignmentOptions.Center, false, new Vector2(0, 0.36f), new Vector2(1, 0.44f)); s.characterSpacing = 4;
            Sfx.Play("bell_toll", null, 0.8f);
            float t0 = Time.unscaledTime;
            wl.Draw = 0; wl2.Draw = 0;
            while (Time.unscaledTime - t0 < 1.6f)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - t0) / 1.3f), kd = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - t0) / 1.6f));
                cg.alpha = k; rule.rectTransform.localScale = new Vector3(k, 1, 1); gl.color = GPal.A(GPal.Candle, 0.22f * k);
                wl.Draw = kd; wl.Refresh(); wl2.Draw = kd; wl2.Refresh(); yield return null;
            }
            float h0 = Time.unscaledTime; while (Time.unscaledTime - h0 < hold) { if (Time.unscaledTime - h0 > 0.5f && skip != null && skip()) break; yield return null; }
            AutoProbe.Shot("cine_recap_title");
            float f0 = Time.unscaledTime; while (Time.unscaledTime - f0 < 0.8f) { cg.alpha = 1f - Mathf.Clamp01((Time.unscaledTime - f0) / 0.8f); yield return null; }
            UnityEngine.Object.Destroy(rt.gameObject); _page.gameObject.SetActive(true);
        }
        public void CaptionVisible(bool on) { _capTarget = on ? 1f : 0f; }
        float _capTarget = 1f;

        // ---------------------------------------------------------------- per frame
        void LateUpdate()
        {
            if (!Open) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            // the view over the window
            _pagePos = Vector2.SmoothDamp(_pagePos, _pagePosT, ref _pagePosV, 0.55f, float.MaxValue, dt);
            _pageScale = Mathf.SmoothDamp(_pageScale, _pageScaleT, ref _pageScaleV, 0.6f, float.MaxValue, dt);
            _pageRot = Mathf.SmoothDamp(_pageRot, _pageRotT, ref _pageRotV, 0.7f, float.MaxValue, dt);
            ApplyPage();
            // live panes follow their shots; frozen ones drift a hair
            LiveCam primary = null;
            foreach (var p in _list)
            {
                if (p.Cam != null && p.Cam.Shot != null)
                {
                    float u = (Time.unscaledTime - p.Cam.T0) / p.Cam.Dur; p.Cam.Shot.Apply(p.Cam.Cam, u, dt);
                    if (p.Cam.Primary) primary = p.Cam;
                }
                else if (p.RT != null && p.Revealed > 0.5f)
                {
                    p.Drift += dt; float k = Mathf.Min(0.035f, p.Drift * 0.004f); var uv = new Rect(k * 0.5f, k * 0.35f, 1f - k, 1f - k);
                    if (p.Img.Uv != uv) { p.Img.Uv = uv; p.Img.Refresh(); }
                }
            }
            if (primary != null) { var mv = Session.I?.World?.Mansion; if (mv != null) mv.ViewCamera = primary.Cam; }
            Lights(primary, dt);
            // narration types itself out
            if (_capFull.Length > 0) { int n = Mathf.Clamp((int)((Time.unscaledTime - _capT0) * 34f), 0, _capFull.Length); if (_capT0 < -100) n = _capFull.Length; _cap.maxVisibleCharacters = n; }
            _capCg.alpha = Mathf.MoveTowards(_capCg.alpha, _page.gameObject.activeSelf ? _capTarget : 0f, dt * 3f);
            _bgGlow.color = new Color(0.55f, 0.32f, 0.14f, 0.13f + 0.03f * Mathf.PerlinNoise(Time.unscaledTime * 0.7f, 3.1f));
        }

        /// <summary>Candle key + cool rim on whatever the live pane is looking at (cinema lighting; only while a pane is live).</summary>
        void Lights(LiveCam lc, float dt)
        {
            if (_key == null)
            {
                _key = new GameObject("CineKey").AddComponent<Light>(); _key.type = LightType.Spot; _key.spotAngle = 44f; _key.innerSpotAngle = 14f; _key.range = 7f; _key.color = new Color(1f, 0.84f, 0.66f); _key.shadows = LightShadows.None; _key.intensity = 0;
                _rim = new GameObject("CineRim").AddComponent<Light>(); _rim.type = LightType.Point; _rim.range = 2.4f; _rim.color = new Color(0.66f, 0.74f, 1f); _rim.shadows = LightShadows.None; _rim.intensity = 0;
                _fill = new GameObject("CineFill").AddComponent<Light>(); _fill.type = LightType.Point; _fill.range = 3f; _fill.color = new Color(0.92f, 0.9f, 0.96f); _fill.shadows = LightShadows.None; _fill.intensity = 0; _fill.transform.SetParent(transform, true);
                _key.transform.SetParent(transform, true); _rim.transform.SetParent(transform, true);
            }
            // faces get the key and the rim; a wide view of a room keeps the room's own light (no hot spot on the floor)
            bool close = lc != null && lc.Shot?.Spec != null && lc.Shot.Spec.Kind != ShotKind.Establish && lc.Shot.Spec.Kind != ShotKind.Top;
            _lightK = Mathf.MoveTowards(_lightK, close ? 1f : 0f, dt * 2f);
            if (lc != null)
            {
                var (k, r, s) = CineSolver.LightsFor(lc.Shot, lc.Shot.CamPos);
                _key.transform.position = Vector3.Lerp(_key.transform.position, k, 1f - Mathf.Exp(-6f * dt)); _key.transform.rotation = Quaternion.LookRotation((s - _key.transform.position).normalized);
                _rim.transform.position = Vector3.Lerp(_rim.transform.position, r, 1f - Mathf.Exp(-6f * dt));
                var tc = lc.Shot.CamPos - s; _fill.transform.position = Vector3.Lerp(_fill.transform.position, s + tc.normalized * Mathf.Min(1.2f, tc.magnitude * 0.6f) + Vector3.up * 0.1f, 1f - Mathf.Exp(-6f * dt));
            }
            _key.intensity = 2.6f * _lightK; _rim.intensity = 1.1f * _lightK; _fill.intensity = 0.9f * _lightK; _key.enabled = _rim.enabled = _fill.enabled = _lightK > 0.01f;
        }
    }
}
