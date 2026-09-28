using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Audio;
using BL23.Game.Characters;
using BL23.Game.Mansion;
using BL23.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace BL23.Game
{
    /// <summary>
    /// The trial (심판) presentation, in the house's own gothic idiom (candles, wax, brass, parchment, a bell, a gallery of eyes).
    /// Beats come from the kernel's non-linear debate; when the kernel opens a round (TrialGames: 촛불 심문, 증언 대조, 붉은 실,
    /// 새겨진 물음, 시계와 평면도) the matching TrialMinigame plays it over the 3D courtroom and resolves it through the kernel.
    /// The butler sits silent on the judge's seat throughout and speaks only the verdict.
    /// The jury's scales (top right) show the player's credibility and where the room is leaning, live.
    /// Truth is never colour-coded.
    /// </summary>
    public sealed partial class TrialDirectorUI : MonoBehaviour
    {
        Session _s; GameState S => _s.S; Simulation Sim => _s.Sim; TrialState T => S.Trial;
        public Session Ses => _s;
        Canvas _c, _gc, _hc, _fc; Camera _cam; public bool Active; public string BreakFor, BreakKind;
        public RectTransform GameRoot, HudRoot, FxRoot; RectTransform _chromeRoot;
        /// <summary>Small canvases (1280×720 at 150% UI scale) get a virtual layer at least 900 units tall, uniformly scaled, so layouts keep their proportions.</summary>
        static void Fit(RectTransform rt, float minH = 900f)
        {
            if (rt == null) return; var parent = rt.parent as RectTransform; if (parent == null) return; var ps = parent.rect.size; if (ps.y < 1) return;
            float k = Mathf.Min(1f, ps.y / minH); var want = ps / k;
            if (Mathf.Abs(rt.localScale.x - k) < 0.0005f && (rt.sizeDelta - want).sqrMagnitude < 0.25f && rt.anchorMin == new Vector2(0.5f, 0.5f)) return;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = want; rt.localScale = Vector3.one * k; rt.anchoredPosition = Vector2.zero;
        }
        void FitAll() { Fit(_chromeRoot); Fit(GameRoot); Fit(HudRoot); Fit(FxRoot); }
        RectTransform _modePlate, _linePanel, _namePlate, _focus, _modal, _keys; TextMeshProUGUI _speaker, _line, _mode, _topic, _hint, _claimTag; SfxHandle _ambience; int _chapters;
        ScalesGraphic _scales; RectTransform _hud; readonly List<(RectTransform rt, TextMeshProUGUI name, Image bar, string id)> _lean = new List<(RectTransform, TextMeshProUGUI, Image, string)>(); float _leanNext; string _leader;
        TrialBeat _beat; float _typed; bool _waiting; float _autoAt; string[] _pages; int _page;
        Vector3 _camPos, _camLook, _camWantPos, _camWantLook; float _camT; bool _inFocus; string _focusClaim; string _focusAction;
        bool _verdictShown; int _busy; bool _inGame;
        Vector3 _center;
        // clue picker / convenience: the hint plate, the live key bar, the picker in use, menu hand-off, auto / fast-forward
        RectTransform _hintBox; GFrame _hintBed; float _hintUntil = -1f; string _keySpec; CluePicker _picker; int _menuFrame = -9; float _waitStart; bool _forceAuto;
        /// <summary>Automated probe: dialogue pages advance quickly so a whole trial fits in a screenshot run.</summary>
        public static bool ProbeFast;

        public void Init(Session s)
        {
            _s = s; _c = UIKit.Root("Trial", 35); _chromeRoot = UIKit.Rect(_c.transform, "Root", Vector2.zero, Vector2.one); var t = _chromeRoot;
            // the chapter (top left): engraved straight onto the scene over a soft shadow, a brass rule, the topic beneath
            _modePlate = UIKit.Rect(t, "Mode", new Vector2(0, 1), new Vector2(0, 1)); _modePlate.pivot = new Vector2(0, 1); _modePlate.sizeDelta = new Vector2(620, 96); _modePlate.anchoredPosition = new Vector2(36, -24);
            var mbed = UIKit.Rect(_modePlate, "Bed", Vector2.zero, Vector2.one, new Vector2(-120, -70), new Vector2(60, 50)); var mbi = mbed.gameObject.AddComponent<RawImage>(); mbi.texture = GTex.Glow; mbi.color = new Color(0, 0, 0, 0.6f); mbi.raycastTarget = false;
            _mode = Goth.Engraved(_modePlate, "T", "심판", 36); _mode.alignment = TextAlignmentOptions.TopLeft; _mode.rectTransform.offsetMin = new Vector2(0, 40); _mode.rectTransform.offsetMax = Vector2.zero;
            UIKit.Img(_modePlate, "Rule", GPal.A(GPal.Brass, 0.75f), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 36), new Vector2(220, 37));
            _topic = Goth.Text(_modePlate, "Topic", "", 21, GPal.Bone, TextAlignmentOptions.TopLeft, false, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -66)); _topic.textWrappingMode = TextWrappingModes.NoWrap; _topic.overflowMode = TextOverflowModes.Ellipsis;
            // the room's passing remarks (a hint, "지켜보기로 했다"…) sit on their own small ink plate, never straight on the glass
            _hintBox = UIKit.Rect(t, "HintBox", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -170), new Vector2(800, -122));
            _hintBed = Goth.Frame(_hintBox, "Bed", new Vector2(0, 0), new Vector2(0, 1)); _hintBed.Fill = GPal.A(new Color(0.055f, 0.04f, 0.03f), 1f); _hintBed.FillBottom = GPal.A(GPal.Lacquer, 1f); _hintBed.Border = GPal.A(GPal.Brass, 0.6f); _hintBed.Gap = 0; _hintBed.Width = 1.2f; _hintBed.CornerSize = 3.5f;
            _hintBed.rectTransform.pivot = new Vector2(0, 0.5f);
            _hint = Goth.Text(_hintBox, "Hint", "", 19, GPal.A(GPal.Brass, 0.95f), TextAlignmentOptions.MidlineLeft, false, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-8, 0)); _hint.textWrappingMode = TextWrappingModes.NoWrap; _hint.overflowMode = TextOverflowModes.Ellipsis;
            // the dialogue box: lacquer panel with a gilt hairline; the name sits above it on its own dark plate with a gold rule
            _linePanel = UIKit.Rect(t, "Line", new Vector2(0, 0), new Vector2(1, 0), new Vector2(150, 36), new Vector2(-150, 236));
            Goth.Panel(_linePanel, "F", Vector2.zero, Vector2.one);
            _namePlate = Goth.Plate(_linePanel, "Name", "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -4), new Vector2(334, 46), 26);
            _speaker = _namePlate.GetComponentInChildren<TextMeshProUGUI>();
            _claimTag = Goth.Text(_linePanel, "Claim", "기록됨", 17, GPal.A(GPal.Brass, 0.95f), TextAlignmentOptions.MidlineLeft, true, new Vector2(0, 1), new Vector2(0, 1), new Vector2(350, -2), new Vector2(620, 40)); _claimTag.characterSpacing = 3;
            _line = Goth.Text(_linePanel, "Text", "", 32, GPal.Bone, TextAlignmentOptions.TopLeft, false, Vector2.zero, Vector2.one, new Vector2(76, 30), new Vector2(-76, -40)); _line.lineSpacing = 8;
            RefreshKeys(true);
            _focus = UIKit.Rect(t, "Focus", Vector2.zero, Vector2.one); _focus.gameObject.SetActive(false);
            _modal = UIKit.Rect(t, "Modal", Vector2.zero, Vector2.one); _modal.gameObject.SetActive(false);
            _c.enabled = false;
            // round layer, HUD (the jury's scales) above it, FX layer (plates / interjections / chapter cards) on top of everything
            _gc = UIKit.Root("TrialGame", 36); GameRoot = UIKit.Rect(_gc.transform, "Root", Vector2.zero, Vector2.one); _gc.enabled = false;
            _hc = UIKit.Root("TrialHud", 37); HudRoot = UIKit.Rect(_hc.transform, "Root", Vector2.zero, Vector2.one); _hc.enabled = false;
            _fc = UIKit.Root("TrialFx", 60); FxRoot = UIKit.Rect(_fc.transform, "Root", Vector2.zero, Vector2.one); _fc.enabled = false;
            BuildScales();
            var camGo = new GameObject("TrialCamera", typeof(Camera)); DontDestroyOnLoad(camGo); _cam = camGo.GetComponent<Camera>(); _cam.enabled = false; _cam.fieldOfView = 34; _cam.farClipPlane = 160;
            camGo.AddComponent<UniversalAdditionalCameraData>();
            BL23.Game.Mansion.MansionAtmosphere.SetupCamera(_cam);   // same HDR / SMAA High / depth+colour setup as the mansion camera
            BL23.Game.Cinema.CineDof.Register(_cam);                // depth of field on close shots (camera code: Cinema/TrialDirectorUI.Camera.cs)
        }
        public void Destroy() { foreach (var c in new[] { _c, _gc, _hc, _fc }) if (c) UnityEngine.Object.Destroy(c.gameObject); if (_cam) UnityEngine.Object.Destroy(_cam.gameObject); TrialPortraits.Clear(); }

        // ---------------------------------------------------------------- the jury's scales (HUD)
        void BuildScales()
        {
            _hud = UIKit.Rect(HudRoot, "Scales", new Vector2(1, 1), new Vector2(1, 1)); _hud.pivot = new Vector2(1, 1); _hud.sizeDelta = new Vector2(470, 176); _hud.anchoredPosition = new Vector2(-30, -24); Cg(_hud).alpha = 0;
            Goth.Panel(_hud, "F", Vector2.zero, Vector2.one);
            var sc = UIKit.Rect(_hud, "Balance", new Vector2(0, 0), new Vector2(0, 1), new Vector2(14, 28), new Vector2(200, -10)); _scales = sc.gameObject.AddComponent<ScalesGraphic>(); _scales.raycastTarget = false;
            Goth.Text(_hud, "Cap", "배심원의 신뢰", 16, GPal.Brass, TextAlignmentOptions.Bottom, true, new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 6), new Vector2(200, 30)).characterSpacing = 3;
            Goth.Text(_hud, "L", "신뢰", 13, GPal.Bone, TextAlignmentOptions.Center, false, new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 30), new Vector2(76, 50));
            Goth.Text(_hud, "R", "의혹", 13, GPal.Bone, TextAlignmentOptions.Center, false, new Vector2(0, 0), new Vector2(0, 0), new Vector2(138, 30), new Vector2(200, 50));
            Goth.Text(_hud, "LeanCap", "지금 의심받는 사람", 16, GPal.Brass, TextAlignmentOptions.TopLeft, true, new Vector2(0, 1), new Vector2(1, 1), new Vector2(214, -34), new Vector2(-12, -10)).characterSpacing = 3;
            for (int i = 0; i < 3; i++)
            {
                var row = UIKit.Rect(_hud, "Lean" + i, new Vector2(0, 1), new Vector2(1, 1), new Vector2(214, -34 - (i + 1) * 44), new Vector2(-14, -34 - i * 44 - 4));
                var m = Goth.Medallion(row, "M", "", new Vector2(36, 36), Vector2.zero); m.anchorMin = m.anchorMax = new Vector2(0, 0.5f); m.anchoredPosition = new Vector2(20, 0);
                var n = Goth.Text(row, "N", "", 18, GPal.Bone, TextAlignmentOptions.MidlineLeft, true, Vector2.zero, Vector2.one, new Vector2(46, 12), new Vector2(0, 0)); n.textWrappingMode = TextWrappingModes.NoWrap;
                var bar = UIKit.Img(row, "Bar", GPal.A(GPal.Wax, 0.85f), new Vector2(0, 0), new Vector2(0, 0), new Vector2(46, 2), new Vector2(200, 8));
                _lean.Add((row, n, bar, null));
            }
        }

        void UpdateScales()
        {
            if (T == null || _scales == null) return;
            _scales.Value = T.Influence;
            if (Time.unscaledTime < _leanNext) return; _leanNext = Time.unscaledTime + 0.5f;
            var npcs = T.Participants.Where(x => x != Cast.Player && S.A(x)?.Alive == true).ToList(); if (npcs.Count == 0) return;
            var avg = T.Participants.Where(x => !T.Dead.Contains(x)).Select(x => (id: x, v: npcs.Where(n => n != x).Select(n => S.K(n).Suspicion.TryGetValue(x, out var v) ? v : 0f).DefaultIfEmpty(0).Average())).OrderByDescending(x => x.v).Take(3).ToList();
            float max = Mathf.Max(0.3f, avg.Count > 0 ? avg[0].v : 1);
            for (int i = 0; i < _lean.Count; i++)
            {
                var (rt, name, bar, id) = _lean[i];
                if (i >= avg.Count || avg[i].v <= 0.01f) { rt.gameObject.SetActive(false); continue; }
                rt.gameObject.SetActive(true);
                if (id != avg[i].id)
                {
                    var old = rt.Find("M"); if (old != null) UnityEngine.Object.Destroy(old.gameObject);
                    var m = Goth.Medallion(rt, "M", avg[i].id, new Vector2(36, 36), Vector2.zero); m.anchorMin = m.anchorMax = new Vector2(0, 0.5f); m.anchoredPosition = new Vector2(20, 0);
                    _lean[i] = (rt, name, bar, avg[i].id); name.text = (avg[i].id == Cast.Player ? "당신" : Cast.GivenOf(avg[i].id));
                    if (i == 0 && _leader != null && _leader != avg[i].id) TrialFx.Sound("parchment", 0.35f);   // the room has turned towards someone else
                    if (i == 0) _leader = avg[i].id;
                }
                bar.rectTransform.offsetMax = new Vector2(46 + 190 * Mathf.Clamp01(avg[i].v / max), 8);
            }
        }

        public IEnumerator ShakeRect(RectTransform rt, float amt, float secs)
        {
            if (rt == null) yield break; var p0 = rt.anchoredPosition; float t0 = Time.unscaledTime;
            while (rt != null && Time.unscaledTime - t0 < secs) { float k = 1 - (Time.unscaledTime - t0) / secs; rt.anchoredPosition = p0 + UnityEngine.Random.insideUnitCircle * amt * k; yield return null; }
            if (rt != null) rt.anchoredPosition = p0;
        }

        // ---------------------------------------------------------------- lifecycle
        public void Begin() { StartCoroutine(BeginCo()); }
        IEnumerator BeginCo()
        {
            Active = true; _verdictShown = false; _busy = 1; _inGame = false; _leader = null; _s.Player.SetControl(false); Canvas.ForceUpdateCanvases(); FitAll();   // _busy held until the opening card is gone (beats wait)
            _c.enabled = true; _gc.enabled = true; _hc.enabled = true; _fc.enabled = true; _line.text = ""; _speaker.text = "";
            Sfx.Play("elevator_rumble", null, 0.9f); MusicDirector.I?.SetState(MusicState.TrialOpening);
            var court = S.Layout.Rooms.First(r => r.Type == RoomType.Courtroom);
            _center = _s.World.ToWorld(new P3(-2, court.Rect.CX, court.Rect.CZ)) + Vector3.up * 1.5f;
            var cv = _s.World?.Court; _floorC = cv != null ? cv.Center : _center - Vector3.up * 1.5f; _courtR = cv != null && cv.Radius > 2f ? cv.Radius : 9f;
            _center = new Vector3(_floorC.x, _floorC.y + 1.5f, _floorC.z);
            foreach (var v in _s.World.Actors.Values) v.Snap();
            var heads = T.Participants.Select(id => _s.World.ViewOf(id)).Where(v => v != null).Select(v => { var d = v.HeadPos - _floorC; d.y = 0; return d.magnitude; }).Where(r => r > 0.5f).ToList();
            _standR = heads.Count > 0 ? Mathf.Clamp(heads.Average(), 2f, _courtR - 0.5f) : _courtR * 0.6f;
            _cam.enabled = true; _chapters = 0; _subject = null;
            Establish(0f, true);
            _mode.text = "심판"; _topic.text = ""; _hint.text = ""; ChromeVisible(false);
            _ambience?.Stop(0.1f); _ambience = Sfx.Loop("court_ambience", null, 0.55f, 2.5f);
            yield return null; yield return null;
            // the court was culled a moment ago (the camera was upstairs): show its room and the people in it before any face is taken
            _s.World?.Mansion?.Cull(_cam.transform.position);
            foreach (var v in _s.World.Actors.Values) v.Tick(0f);
            yield return null; yield return null;
            // faces for medallions, plates and the urns — rendered from the actors at their podiums (the butler is not part of the debate)
            if (!_s.Headless) yield return TrialPortraits.Capture(_s, T.Participants, _center);
            if (!_s.Headless) yield return OpeningCard();
            while (!_s.Headless && Time.unscaledTime < _craneUntil + 1.2f) yield return null;   // (cinematics) the opening tilt lands on the judge (and holds) before the first beat
            ChromeVisible(true);
            MusicDirector.I?.SetState(MusicState.TrialDebate);
            _busy = 0; _beat = null; NextBeat();
        }

        IEnumerator Tolls(int n, float gap) { for (int i = 0; i < n; i++) { TrialFx.Sound("bell_toll", i == n - 1 ? 0.95f : 0.75f); yield return new WaitForSecondsRealtime(ProbeFast ? 0.4f : gap); } }

        IEnumerator OpeningCard()
        {
            var rt = UIKit.Rect(FxRoot, "Opening", Vector2.zero, Vector2.one);
            var dim = UIKit.Img(rt, "Dim", GPal.A(Color.black, 0.86f), Vector2.zero, Vector2.one);
            var glow = Goth.Glow(rt, "Glow", GPal.A(GPal.Candle, 0f), new Vector2(1500, 820), new Vector2(0, 20));
            var pre = Goth.Text(rt, "Pre", "꿈꾸는 저택의", 30, GPal.A(GPal.Brass, 0.9f), TextAlignmentOptions.Center, false, new Vector2(0, 0.7f), new Vector2(1, 0.78f)); pre.characterSpacing = 12;
            var title = Goth.Engraved(rt, "T", "심    판", 130); title.rectTransform.anchorMin = new Vector2(0, 0.5f); title.rectTransform.anchorMax = new Vector2(1, 0.72f); title.characterSpacing = 18;
            var rule = UIKit.Img(rt, "Rule", GPal.A(GPal.Brass, 0.8f), new Vector2(0.34f, 0.49f), new Vector2(0.66f, 0.49f), new Vector2(0, -1), new Vector2(0, 1));
            var inc = TrialSystem.TargetIncident(S);
            string sub = inc != null ? $"피해자  {Cast.NameOf(inc.Victim)}   ·   발견된 곳  {S.RoomName(inc.FoundRoom)}" : "이 죽음의 진상을 가린다";
            var st = Goth.Text(rt, "S", sub, 30, GPal.Bone, TextAlignmentOptions.Center, false, new Vector2(0, 0.36f), new Vector2(1, 0.47f)); st.characterSpacing = 3;
            var yu = Goth.Text(rt, "Y", "종소리가 세 번 울린다", 20, GPal.A(GPal.Brass, 0.8f), TextAlignmentOptions.Center, false, new Vector2(0, 0.28f), new Vector2(1, 0.34f)); yu.characterSpacing = 6;
            var cg = rt.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0;
            StartCoroutine(Tolls(3, 1.3f));
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 1.4f) { float k = GFx.EaseOut(GFx.K(t0, 1.4f)); cg.alpha = k; glow.color = GPal.A(GPal.Candle, 0.26f * k); st.alpha = Mathf.Clamp01(k * 1.5f - 0.5f); yu.alpha = st.alpha; rule.rectTransform.localScale = new Vector3(k, 1, 1); yield return null; }
            float h0 = Time.unscaledTime;
            while (Time.unscaledTime - h0 < (ProbeFast ? 1.2f : 2.4f)) { if (Time.unscaledTime - h0 > 0.5f && (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))) break; yield return null; }
            AutoProbe.Shot("trial_opening_card");
            // the card lifts off the court: the circle, the stands and the watching gallery
            Establish(0.35f, false);
            float f0 = Time.unscaledTime; while (Time.unscaledTime - f0 < 1.2f) { cg.alpha = 1 - GFx.EaseInOut(GFx.K(f0, 1.2f)); yield return null; }
            UnityEngine.Object.Destroy(rt.gameObject);
            EyesStare(0.5f);
            yield return new WaitForSecondsRealtime(ProbeFast ? 0.6f : 1.6f);
            AutoProbe.Shot("trial_establish");
        }

        void End()
        {
            Active = false; _c.enabled = false; _gc.enabled = false; _hc.enabled = false; _fc.enabled = false; _cam.enabled = false; BreakFor = null;
            _focus.gameObject.SetActive(false); _modal.gameObject.SetActive(false);
            _ambience?.Stop(1.5f); _ambience = null;
            if (_key != null) { _key.enabled = false; _rim.enabled = false; _keyK = 0; }
            CamEnd();
        }

        // ---------------------------------------------------------------- beats
        void NextBeat()
        {
            if (T == null) return;
            // whatever the kernel already said plays first; a pending round/prompt starts once the room has heard it
            if (T.Cursor < T.Beats.Count) { var q = TrialSystem.Next(Sim); if (q != null) { Show(q); return; } }
            if (T.PendingPrompt != null) { Prompt(T.PendingPrompt); return; }
            var b = TrialSystem.Next(Sim);
            if (b == null) { if (T.PendingPrompt != null) Prompt(T.PendingPrompt); return; }
            Show(b);
        }

        static string[] ShortPages(string text)
        {
            // short, punchy pages: split long lines at sentence ends
            var res = new List<string>();
            foreach (var pg in LineBank.Pages(text))
            {
                if (pg.Length <= 46) { res.Add(pg); continue; }
                var parts = System.Text.RegularExpressions.Regex.Split(pg, @"(?<=[\.!?…])\s+");
                string cur = "";
                foreach (var p in parts) { if (cur.Length > 0 && cur.Length + p.Length > 46) { res.Add(cur); cur = p; } else cur = cur.Length > 0 ? cur + " " + p : p; }
                if (cur.Length > 0) res.Add(cur);
            }
            return res.ToArray();
        }

        void Show(TrialBeat b)
        {
            _beat = b; _typed = 0; _waiting = false; _boxTarget = b.Kind == "mode" || b.Kind == "topic" || b.Kind == "twist" || b.Kind == "game" ? 0 : 1;
            _forceAuto = b.Kind == "mode" || b.Kind == "topic";   // title cards always move on by themselves
            switch (b.Kind)
            {
                case "game": _beat = null; NextBeat(); return;   // the round itself presents its title
                case "mode":
                    {
                        string name = ModeName(b.Mode), sub = b.Text != null && b.Text != name && b.Text != "첫 진술" ? CluePicker.Plain(b.Text) : null;
                        _mode.text = name; if (sub != null) _topic.text = sub;
                        if (b.Mode == TrialMode.TM07_Reconstruct || b.Mode == TrialMode.TM08_FinalDefense) MusicDirector.I?.SetState(MusicState.TrialClimax);
                        else if (b.Mode == TrialMode.TM02_Crossfire || b.Mode == TrialMode.TM04_Chain || b.Mode == TrialMode.TM05_Theory) MusicDirector.I?.SetState(MusicState.TrialPressure);
                        else if (b.Mode == TrialMode.Vote) MusicDirector.I?.SetState(MusicState.Vote);
                        ShotWide(); _autoAt = Time.unscaledTime + (_s.Headless ? 0.01f : ProbeFast ? 0.15f : 0.7f); _waiting = true; _pages = new[] { "" }; _page = 0; _line.text = ""; _speaker.text = ""; _namePlate.gameObject.SetActive(false); _claimTag.gameObject.SetActive(false);
                        // a new chapter of the trial: the bell and a title card, carried by the court itself
                        if (!_s.Headless && _chapterSeen.Add(b.Mode.ToString())) { _chapters++; _busy++; StartCoroutine(Busy(GFx.Chapter(FxRoot, Roman(_chapters), name, sub ?? (b.Text == "첫 진술" ? "첫 진술" : null)))); }
                        else TrialFx.Sound("parchment", 0.4f);
                        return;
                    }
                case "topic":
                    _topic.text = CluePicker.Plain(b.Text ?? "").Replace("논점: ", "논점 — "); _autoAt = Time.unscaledTime + (_s.Headless ? 0.01f : ProbeFast ? 0.3f : 1.6f); _waiting = true; _pages = new[] { "" }; _page = 0; ShotWide();
                    _line.text = ""; _speaker.text = ""; _namePlate.gameObject.SetActive(false); _claimTag.gameObject.SetActive(false);
                    if (!_s.Headless) StartCoroutine(TopicCard(_topic.text));
                    return;
                case "twist":
                    if (!_s.Headless) { _busy++; StartCoroutine(Busy(GFx.Twist(FxRoot, b.Speaker, CluePicker.Plain(b.Text)))); ShotSpeaker(b.Speaker, true); EyesStare(1f); }
                    _beat = null; return;
                case "break":
                    Sfx.Play("break", null, 0.6f); MusicDirector.I?.Stinger("break"); ShakeCam(0.2f); EyesStare(1f);
                    if (!_s.Headless) { var seal = GFx.Seal(FxRoot, new Vector2(FxRoot.rect.width * 0.36f, -FxRoot.rect.height * 0.28f), GPal.Wax, "반", 96); StartCoroutine(SealLife(seal)); }
                    break;
                case "result":
                    if (b.Data == "Contradict" || b.Data == "LimitScope") { TrialFx.Sound("bell_toll", 0.45f); EyesStare(0.8f); }
                    else if (b.Data == "Conditional" || b.Data == "Named") TrialFx.Sound("quill", 0.5f);
                    break;
                case "vote": TrialFx.Sound("bell_toll", 0.7f); EyesStare(0.9f); if (!_s.Headless) { _busy++; StartCoroutine(Busy(UrnReveal(b))); } break;
                case "system": if (b.Data == "Hourglass") { TrialFx.Sound("hourglass", 0.7f); TrialFx.Sound("candle_flare", 0.5f); } else if (b.Key == "vote_call" || b.Key == "accuse_call") TrialFx.Sound("bell_toll", 0.6f); break;
            }
            _namePlate.gameObject.SetActive(b.Speaker != null);
            _speaker.text = b.Speaker != null ? Cast.NameOf(b.Speaker) : "";
            _claimTag.gameObject.SetActive(b.Speaker != null && b.ClaimId != null && b.Kind == "line");
            _pages = ShortPages(LineBank.FixParticles(CluePicker.Plain(b.Text ?? ""))); if (_pages.Length == 0) _pages = new[] { "…" }; _page = 0;
            StartPage();
            bool accusation = b.Key == "accuse" || b.Key == "accuse_player" || b.Key == "p_accuse";
            bool drama = b.Kind == "break" || b.Key == "object" || accusation || b.Key == "panic" || b.Key == "reveal";
            var v = _s.World.ViewOf(b.Speaker);
            if (b.Speaker != null)
            {
                bool changed = b.Speaker != _subject; ShotSpeaker(b.Speaker, drama);
                if (changed && v != null) EyesWatch(v.HeadPos);
                if (drama) EyesStare(accusation ? 1f : 0.7f);
            }
            Perform(v, b.Text, b.Emotion, accusation || b.Gesture == Anim.Point);
            // the one being accused flinches; a strong line makes its listener react
            if (accusation && b.ClaimId != null) { var c = T.Claims.FirstOrDefault(x => x.Id == b.ClaimId); var tgt = c?.Accused ?? c?.Prop?.A; if (tgt != null && tgt != b.Speaker) SpeechGestures.React(_s.World.ViewOf(tgt), b.Emotion, true); }
            // BREAK staging for the designated characters when they are publicly cornered (not a truth flag)
            BreakFor = (b.Key == "panic" || b.Key == "counter") && (b.Speaker == "P02" || b.Speaker == "P04") ? b.Speaker : null;
            BreakKind = BreakFor != null ? b.Key : null;   // "panic" → cornered stare, "counter" → hollow grin (ActorView; no hologram glitch)
            // interjections: a slim banner with the speaker's medallion for the moments that turn the room
            if (!_s.Headless && b.Kind == "line" && b.Speaker != null)
            {
                string phrase = b.Key == "object" ? "잠깐, 그 말…!" : b.Key == "counter" ? "그렇다면 이건?" : accusation ? "범인은 바로—" : b.Key == "p_object" ? "그 말은 틀렸어." : b.Key == "panic" ? "아, 아니…!" : b.Key == "recant" ? "…다시 생각해 보니…" : b.Key == "steer" ? "역시 수상한 건—" : null;
                if (phrase != null) { _busy++; StartCoroutine(Busy(GFx.Interject(FxRoot, b.Speaker, phrase, b.Key == "panic" ? GPal.Wax : b.Key == "recant" ? GPal.Smoke : GPal.Brass, ProbeFast ? 0.3f : 0.7f))); }
            }
        }
        readonly HashSet<string> _chapterSeen = new HashSet<string>();
        static string Roman(int n) { string[] r = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" }; return r[Mathf.Clamp(n - 1, 0, r.Length - 1)]; }

        IEnumerator Busy(IEnumerator e) { yield return e; _busy = Mathf.Max(0, _busy - 1); }
        IEnumerator SealLife(SealGraphic s) { yield return GFx.Slam(s); yield return new WaitForSecondsRealtime(1.4f); float t0 = Time.unscaledTime; while (s != null && Time.unscaledTime - t0 < 0.5f) { s.color = new Color(1, 1, 1, 1 - GFx.K(t0, 0.5f)); yield return null; } if (s != null) UnityEngine.Object.Destroy(s.gameObject); }

        /// <summary>The topic: engraved words rise over a soft shadow and settle into the chapter corner.</summary>
        IEnumerator TopicCard(string text)
        {
            var rt = UIKit.Rect(FxRoot, "Topic", new Vector2(0.15f, 0.56f), new Vector2(0.85f, 0.7f));
            var cg = rt.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0; cg.blocksRaycasts = false;
            var bed = UIKit.Rect(rt, "Bed", Vector2.zero, Vector2.one, new Vector2(60, -40), new Vector2(-60, 40)); var bi = bed.gameObject.AddComponent<RawImage>(); bi.texture = GTex.Glow; bi.color = new Color(0, 0, 0, 0.72f); bi.raycastTarget = false;
            var t = Goth.Engraved(rt, "T", text, 50, GPal.Gilt); t.characterSpacing = 6; t.enableAutoSizing = true; t.fontSizeMin = 28; t.fontSizeMax = 50;
            UIKit.Img(rt, "Rule", GPal.A(GPal.Brass, 0.7f), new Vector2(0.4f, 0.08f), new Vector2(0.6f, 0.08f), new Vector2(0, -1), new Vector2(0, 1));
            TrialFx.Sound("parchment", 0.7f);
            float t0 = Time.unscaledTime; var p1 = rt.anchoredPosition;
            while (Time.unscaledTime - t0 < 0.5f) { float k = GFx.EaseOut(GFx.K(t0, 0.5f)); cg.alpha = k; rt.anchoredPosition = p1 + new Vector2(0, -16 * (1 - k)); yield return null; }
            cg.alpha = 1; rt.anchoredPosition = p1;
            yield return new WaitForSecondsRealtime(ProbeFast ? 0.4f : 1.2f);
            float f0 = Time.unscaledTime; while (Time.unscaledTime - f0 < 0.5f) { cg.alpha = 1 - GFx.EaseInOut(GFx.K(f0, 0.5f)); yield return null; }
            UnityEngine.Object.Destroy(rt.gameObject);
        }

        void StartPage()
        {
            _typed = 0; _waiting = false; _line.text = _pages[_page]; _line.maxVisibleCharacters = 0; _lineFade = 0.25f;
            if (_beat?.Speaker != null) VoiceBabble.Speak(_beat.Speaker, _pages[_page]);
            _s.World.ViewOf(_beat?.Speaker)?.Talk(Mathf.Clamp(_pages[_page].Length * 0.06f, 0.5f, 4f));
            _line.color = _beat?.Speaker == null ? GPal.A(GPal.Brass, 1f) : GPal.Bone;   // the room's own narration reads in brass, voices in bone
            _line.fontStyle = _beat?.Speaker == null ? FontStyles.Italic : FontStyles.Normal;
        }

        void Update()
        {
            if (!Active) return;
            UpdateCam(Time.unscaledDeltaTime);
            FitAll(); UpdateScales(); UpdateChrome(Time.unscaledDeltaTime);
            // the pause menu or 지난 대화 over the court: everything waits (the line's timer too) until it closes; the Esc/H that closes it is not ours
            if ((_s.Menu != null && _s.Menu.Open) || BacklogUI.Open || Time.frameCount == BacklogUI.ClosedFrame) { _menuFrame = Time.frameCount; _autoAt += Time.unscaledDeltaTime; _hintUntil += _hintUntil > 0 ? Time.unscaledDeltaTime : 0; return; }
            if (_hintUntil > 0 && Time.unscaledTime > _hintUntil) { _hintUntil = -1f; _hint.text = ""; }
            RefreshKeys();
            if (_inGame || _busy > 0) return;
            if (_verdictShown || (_s.Note != null && (_s.Note.Open || _s.Note.ClosedFrame == Time.frameCount))) return;   // notebook open over the court: the debate waits
            if (_modal.gameObject.activeSelf) { if (_picker != null && (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.N))) _s.Note?.OpenTab("evidence"); return; }
            if (Input.GetKeyDown(KeyCode.F) && !_inFocus && T != null && T.Stage != "Vote" && T.Stage != "Done")
            {
                if (CluePicker.AnyHeard(T)) { OpenFocus(); return; }
                Hint("아직 되짚을 발언이 없다", 3f);
            }
            // in 발언 되짚기 the picker owns the arrows, Enter and Esc; Tab opens the notebook, F closes
            if (_inFocus) { if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.N)) { _s.Note.OpenTab("evidence"); return; } if (Input.GetKeyDown(KeyCode.F) || (_picker == null && Input.GetKeyDown(KeyCode.Escape))) CloseFocus(); return; }
            if (Input.GetKeyDown(KeyCode.Escape) && Time.frameCount - _menuFrame > 1 && _s.Menu != null) { _s.Menu.OpenPause(); return; }
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.N)) { _s.Note?.OpenTab("evidence"); return; }
            if (Input.GetKeyDown(KeyCode.V)) { TrialAuto = !TrialAuto; Hint(TrialAuto ? "자동 진행 켬" : "자동 진행 끔 — E로 넘긴다", 2.5f); RefreshKeys(true); }
            if (Input.GetKeyDown(KeyCode.A) && T != null && T.Participants.Contains(Cast.Player))
            {
                if ((T.Stage == "Culprit" || T.Stage == "Suspicious") && T.PlayerAccused == null) { OpenAccuse(); return; }
                if (T.PlayerAccused == null) Hint("지목은 범인을 가리는 논의 때 할 수 있다", 3f);
            }
            if (_beat == null) { NextBeat(); return; }
            bool ff = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);   // hold Ctrl: lines race by
            float speed = 48f * Settings.TextSpeed * (ProbeFast ? 6f : 1f) * (ff ? 8f : 1f);
            _typed += Time.unscaledDeltaTime * speed; int n = Mathf.Min(_pages[_page].Length, (int)_typed); _line.maxVisibleCharacters = n;
            if (!_waiting && n >= _pages[_page].Length) { _waiting = true; _waitStart = Time.unscaledTime; _autoAt = Time.unscaledTime + (_s.Headless ? 0.01f : ProbeFast ? 0.12f : Mathf.Clamp(_pages[_page].Length * 0.045f, 1.2f, 3.2f)); }
            bool adv = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
            if (adv && !_waiting) { _typed = 9999; return; }
            bool auto = _forceAuto || TrialAuto || _s.Headless || ProbeFast || AutoProbe.Active;
            if (_waiting && (adv || (auto && Time.unscaledTime >= _autoAt) || (ff && Time.unscaledTime - _waitStart > 0.12f)))
            {
                if (_page + 1 < _pages.Length) { _page++; StartPage(); return; }
                _beat = null;
                if (T == null || T.Finished) { if (S.Phase == Phase.Verdict) ShowVerdict(); return; }
                NextBeat();
            }
        }

        // ---------------------------------------------------------------- 발언 되짚기 (review any statement already made)
        void OpenFocus()
        {
            _inFocus = true; _focusClaim = null; _focusAction = null; TrialFx.Sound("parchment", 0.7f); ChromeVisible(false);
            BuildFocus();
        }
        void CloseFocus(bool resume = true)
        {
            _inFocus = false; _picker = null; UIKit.Clear(_focus); _focus.gameObject.SetActive(false); ChromeVisible(true); TrialFx.Sound("parchment", 0.5f);
            // opened from a decision moment and closed without acting: the debate picks up where it stopped
            if (_resumeAfterFocus) { _resumeAfterFocus = false; if (resume) { _beat = null; NextBeat(); } }
        }
        float _chromeTarget = 1, _lineFade = 1, _boxTarget = 1;
        CanvasGroup Cg(RectTransform rt) => rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>();
        /// <summary>The chrome (chapter corner, dialogue box, the jury's scales) fades rather than pops.</summary>
        public void ChromeVisible(bool on) { _chromeTarget = on ? 1 : 0; foreach (var rt in new[] { _modePlate, _hintBox, _linePanel, _hud }) { var cg = Cg(rt); if (on) rt.gameObject.SetActive(true); cg.blocksRaycasts = on; } }
        void UpdateChrome(float dt)
        {
            foreach (var rt in new[] { _modePlate, _hintBox, _linePanel, _hud })
            {
                if (!rt.gameObject.activeSelf) continue; var cg = Cg(rt);
                float tgt = rt == _linePanel ? _chromeTarget * _boxTarget : rt == _hud ? (_inGame ? 0 : _chromeTarget) : _chromeTarget;
                cg.alpha = Mathf.MoveTowards(cg.alpha, tgt, dt * 2.5f);
                if (cg.alpha <= 0.001f && _chromeTarget <= 0) rt.gameObject.SetActive(false);
            }
            _lineFade = Mathf.Min(1, _lineFade + dt * 5f); _line.alpha = _lineFade;
            // the hint plate hugs its words and disappears with them
            bool hint = !string.IsNullOrEmpty(_hint.text);
            if (_hintBed.enabled != hint) _hintBed.enabled = hint;
            if (hint) { float w = Mathf.Min(_hintBox.rect.width, _hint.GetPreferredValues(_hint.text, 4000, 40).x + 32); var sd = _hintBed.rectTransform.sizeDelta; if (Mathf.Abs(sd.x - w) > 0.5f) _hintBed.rectTransform.sizeDelta = new Vector2(w, sd.y); }
        }

        /// <summary>A passing line on the hint plate (top left); it clears itself after a while.</summary>
        void Hint(string msg, float secs = 6f) { _hint.text = msg ?? ""; _hintUntil = string.IsNullOrEmpty(msg) ? -1f : Time.unscaledTime + secs; }

        /// <summary>자동 진행 in court (bl23.trialauto, on by default: the debate moves at its own pace; V toggles).</summary>
        static bool TrialAuto { get => Settings.TrialAuto; set { Settings.TrialAuto = value; PlayerPrefs.Save(); } }

        /// <summary>The key bar shows only what works right now.</summary>
        float _keysNext;
        void RefreshKeys(bool force = false)
        {
            if (!force && Time.unscaledTime < _keysNext) return; _keysNext = Time.unscaledTime + 0.25f;
            var T = _s?.S?.Trial;
            var parts = new List<string> { "E|넘기기", "Ctrl|빨리", TrialAuto ? "V|자동 켬" : "V|자동 끔" };
            if (T != null && T.Stage != "Vote" && T.Stage != "Done" && CluePicker.AnyHeard(T)) parts.Add("F|발언 되짚기");
            if (T != null && (T.Stage == "Culprit" || T.Stage == "Suspicious") && T.PlayerAccused == null && T.Participants.Contains(Cast.Player)) parts.Add("A|지목");
            parts.Add("Tab|수첩"); parts.Add("Esc|메뉴");
            var spec = string.Join(" · ", parts);
            if (!force && spec == _keySpec) return;
            _keySpec = spec;
            if (_keys != null) UnityEngine.Object.Destroy(_keys.gameObject);
            _keys = Goth.KeyBar(_linePanel, spec, 17); _keys.anchorMin = _keys.anchorMax = new Vector2(1, 1); _keys.anchoredPosition = new Vector2(-30, 6);
        }

        void BuildFocus()
        {
            UIKit.Clear(_focus); _focus.gameObject.SetActive(true);
            UIKit.Img(_focus, "Dim", GPal.A(Color.black, 0.8f), Vector2.zero, Vector2.one);
            var head = UIKit.Rect(_focus, "Head", new Vector2(0, 1), new Vector2(1, 1), new Vector2(64, -122), new Vector2(-64, -26));
            var tt = Goth.Engraved(head, "T", "발언 되짚기", 46); tt.alignment = TextAlignmentOptions.TopLeft; tt.characterSpacing = 8;
            Goth.Text(head, "S", "지금까지 나온 말 가운데 틀린 곳을 단서로 뒤집는다", 21, GPal.Bone, TextAlignmentOptions.BottomLeft, false, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(0, -60));
            var keys = Goth.KeyBar(_focus, "F|닫기"); keys.anchoredPosition = new Vector2(-64, 26);   // Tab 수첩 is on the picker's own key line
            var page = UIKit.Rect(_focus, "Page", new Vector2(0, 0), new Vector2(1, 1), new Vector2(64, 76), new Vector2(-64, -134));
            _picker = CluePicker.Build(page, Sim, CluePicker.ReviewClaims(T), CluePicker.Mode.Review, OnPick, () => CloseFocus());
            if (_focusClaim != null) _picker.Select(_focusClaim, null);
        }

        /// <summary>The player's move from any picker: ask the source, or put a card against a statement; a short ink plate says how it
        /// landed before the court answers.</summary>
        void OnPick(string claimId, string cardId, string act)
        {
            if (_inFocus) CloseFocus(false);
            if (act == "source") { if (claimId != null) TrialSystem.PlayerAskSource(Sim, claimId); _beat = null; NextBeat(); return; }
            var claim = ClaimOf(claimId);
            if (claim == null || cardId == null) { _beat = null; NextBeat(); return; }
            var before = Snapshot();
            var r = act == "agree" ? TrialSystem.PlayerSupport(Sim, claim.Id, cardId) : TrialSystem.PlayerContradict(Sim, claim.Id, cardId);
            StartCoroutine(AfterMove(cardId, claim, act, r, before));
        }

        Dictionary<string, string> Snapshot() => T == null ? new Dictionary<string, string>() : T.Claims.ToDictionary(c => c.Id, c => c.Status);

        /// <summary>After a presented card: a 2–2.5 s ink plate ("{단서} → {화자}의 말을 뒤집었다" / "맞지 않았다 — …"), then the debate.</summary>
        IEnumerator AfterMove(string cardId, TrialClaim claim, string act, TrialSystem.Result r, Dictionary<string, string> before)
        {
            _busy++;
            // the first rebuttal that lands is the lesson learned: the first-trial guide retires
            if (r != null && r.Valid && !AutoProbe.Active && PlayerPrefs.GetInt("bl23.tut.court", 0) == 0) { PlayerPrefs.SetInt("bl23.tut.court", 1); PlayerPrefs.Save(); }
            if (!_s.Headless)
            {
                var ev = TrialGames.BulletEvidence(S, cardId); string title = null;
                try { title = ev != null ? CaseBoard.Describe(Sim, ev)?.Title : null; } catch (Exception e) { Debug.LogException(e); }
                title = CluePicker.Plain(title ?? ev?.Title ?? "단서");
                // which statement actually moved (a defense can land on a premise rather than the one picked)
                var moved = T?.Claims.FirstOrDefault(c => before.TryGetValue(c.Id, out var s0) ? s0 != c.Status : false) ?? claim;
                bool valid = r != null && r.Valid;
                string who = moved != null ? Cast.GivenOf(moved.Speaker) : null;
                string head = valid ? (act == "agree" ? $"{title} → {who}의 말을 뒷받침했다" : who != null ? $"{title} → {who}의 말을 뒤집었다" : $"{title} — 받아들여졌다") : "통하지 않았다";
                string why = CluePicker.Plain(r?.Text ?? "");
                if (!valid && (string.IsNullOrEmpty(why) || why.Contains("모두가"))) why = act == "present" ? "이 단서로는 나를 지목한 말을 뒤집을 수 없다" : "이 단서로는 그 말을 뒤집을 수 없다";
                yield return ResultPlate(head, why, valid, valid ? 2.5f : 2f);
            }
            _busy = Mathf.Max(0, _busy - 1);
            ChromeVisible(true); _beat = null; NextBeat();
        }

        static bool _plateShot;
        IEnumerator ResultPlate(string head, string why, bool valid, float hold)
        {
            var rt = UIKit.Rect(FxRoot, "ResultPlate", new Vector2(0.2f, 0.56f), new Vector2(0.8f, 0.74f));
            var cg = rt.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0; cg.blocksRaycasts = false;
            var fr = Goth.Panel(rt, "F", Vector2.zero, Vector2.one); fr.Border = valid ? GPal.A(GPal.Gilt, 0.95f) : GPal.A(GPal.Smoke, 0.7f);
            UIKit.Img(rt, "Rule", valid ? GPal.A(GPal.Gilt, 0.8f) : GPal.A(GPal.Wax, 0.8f), new Vector2(0.08f, 0.5f), new Vector2(0.92f, 0.5f), new Vector2(0, -1), new Vector2(0, 0));
            var h = Goth.Text(rt, "H", head, 34, valid ? GPal.Gilt : GPal.Bone, TextAlignmentOptions.Center, true, new Vector2(0, 0.5f), Vector2.one, new Vector2(30, 4), new Vector2(-30, -10));
            h.enableAutoSizing = true; h.fontSizeMin = 22; h.fontSizeMax = 34; h.textWrappingMode = TextWrappingModes.NoWrap; h.overflowMode = TextOverflowModes.Ellipsis;
            var w = Goth.Text(rt, "W", why, 22, GPal.A(GPal.Bone, 0.9f), TextAlignmentOptions.Center, false, Vector2.zero, new Vector2(1, 0.5f), new Vector2(36, 10), new Vector2(-36, -8));
            w.enableAutoSizing = true; w.fontSizeMin = 15; w.fontSizeMax = 22;
            TrialFx.Sound(valid ? "wax_stamp" : "candle_snuff", 0.7f);
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.25f) { cg.alpha = GFx.EaseOut(GFx.K(t0, 0.25f)); yield return null; }
            cg.alpha = 1;
            if (AutoProbe.Active && !_plateShot) { _plateShot = true; AutoProbe.Shot("trial_result_plate"); }
            float hh = ProbeFast ? Mathf.Min(hold, 1f) : hold; float h0 = Time.unscaledTime;
            while (Time.unscaledTime - h0 < hh) { if (Time.unscaledTime - h0 > 0.5f && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))) break; yield return null; }
            float f0 = Time.unscaledTime; while (Time.unscaledTime - f0 < 0.25f) { cg.alpha = 1 - GFx.K(f0, 0.25f); yield return null; }
            UnityEngine.Object.Destroy(rt.gameObject);
        }

        // ---------------------------------------------------------------- accusation / prompts
        void OpenAccuse()
        {
            _busy++;
            StartCoroutine(PortraitPick("지목", "누구를 지목할까? (Esc: 취소)", T.Participants.Where(x => x != Cast.Player).ToList(), null, id =>
            {
                _busy = Mathf.Max(0, _busy - 1);
                if (id != null) { TrialSystem.PlayerAccuse(Sim, id); _beat = null; NextBeat(); }
            }, true));
        }

        public void ProbeFocus() { if (!_inFocus && T != null && !_inGame && _busy == 0 && CluePicker.AnyHeard(T)) OpenFocus(); }   // like F: never an empty page
        public void ProbeCloseFocus() { if (_inFocus) CloseFocus(); }

        void Prompt(string p)
        {
            if (p.StartsWith("game:") || p == "accuse")
            {
                if (_s.Headless || !T.Participants.Contains(Cast.Player)) { TrialGames.AutoResolve(Sim, true); _beat = null; NextBeat(); return; }
                if (p == "accuse") { _busy++; StartCoroutine(AccusePrompt()); return; }
                TrialMinigame g = null;
                switch (p)
                {
                    case "game:inquiry": g = new CandleInquiry(this); break;
                    case "game:ledger": g = new CrossLedger(this); break;
                    case "game:board": g = new ThreadBoard(this); break;
                    case "game:question": g = new EngravedQuestion(this); break;
                    case "game:reconstruct": g = new ClockReconstruct(this); break;
                }
                if (g == null) { TrialGames.AutoResolve(Sim, false); _beat = null; NextBeat(); return; }
                StartCoroutine(RunGame(g));
                return;
            }
            if (AutoProbe.Active && (p == "vote" || p == "reconstruct" || p == "defense"))
            {
                if (p == "vote") { _busy++; StartCoroutine(VotePrompt()); return; }
                if (p == "defense") TrialSystem.PlayerDefense(Sim, null);
                T.PendingPrompt = null; _beat = null; NextBeat(); return;
            }
            if (p == "vote") { if (_s.Headless) { TrialSystem.PlayerVote(Sim, AutoPick()); T.PendingPrompt = null; _beat = null; NextBeat(); return; } _busy++; StartCoroutine(VotePrompt()); return; }
            if (p == "reconstruct") { T.PendingPrompt = null; _beat = null; NextBeat(); return; }
            if (p == "rebut")
            {
                if (AutoProbe.Active || _s.Headless) { T.PendingPrompt = null; _beat = null; NextBeat(); return; }
                var rc = CluePicker.RebutClaims(T);
                if (rc.Count == 0) { T.PendingPrompt = null; _beat = null; NextBeat(); return; }
                OpenPromptPicker("반론 우선권", "나를 향한 말 가운데 틀린 곳을 먼저 단서로 뒤집는다", rc, CluePicker.Mode.Rebut,
                    (c, e, a) => { T.PendingPrompt = null; OnPick(c, e, "contra"); },
                    () => { T.PendingPrompt = null; _beat = null; NextBeat(); });
                return;
            }
            if (p == "defense")
            {
                OpenPromptPicker("최종 변론", "나를 지목한 이유를 뒤집을 단서를 내민다", CluePicker.DefenseClaims(T), CluePicker.Mode.Defense,
                    (c, e, a) => { T.PendingPrompt = null; var before = Snapshot(); var r = TrialSystem.PlayerPresent(Sim, e, "@player"); StartCoroutine(AfterMove(e, ClaimOf(c), "present", r, before)); },
                    () => { TrialSystem.PlayerDefense(Sim, null); _beat = null; NextBeat(); });
                return;
            }
            if (p.StartsWith("witness:") || p.StartsWith("chain:") || p == "theory")
            {
                T.PendingPrompt = null;
                // the debate stops and turns to you: a real decision, not a hint line that scrolls past
                if ((!_s.Headless || ProbeMomentOnce) && T.Participants.Contains(Cast.Player) && PlayerAlive()) { ProbeMomentOnce = false; StartCoroutine(Moment(p)); return; }
                string msg = p == "theory" ? "두 가설이 맞선다 — F로 발언을 되짚을 수 있다" : p.StartsWith("chain:") ? "꼬리를 무는 주장 — F로 그 밑에 깔린 말 하나를 짚을 수 있다" : "증언 심문 — F로 발언을 되짚어 출처를 물을 수 있다";
                Hint(msg); _beat = null; NextBeat(); return;
            }
            T.PendingPrompt = null; NextBeat();
        }

        /// <summary>반론 우선권 / 최종 변론: the clue picker over the court (the debate waits; Tab still opens the notebook).</summary>
        void OpenPromptPicker(string title, string sub, List<TrialClaim> claims, CluePicker.Mode mode, Action<string, string, string> commit, Action cancel)
        {
            UIKit.Clear(_modal); _modal.gameObject.SetActive(true); ChromeVisible(false);
            UIKit.Img(_modal, "Dim", GPal.A(Color.black, 0.8f), Vector2.zero, Vector2.one);
            var head = UIKit.Rect(_modal, "Head", new Vector2(0, 1), new Vector2(1, 1), new Vector2(64, -122), new Vector2(-64, -26));
            var tt = Goth.Engraved(head, "T", title, 46); tt.alignment = TextAlignmentOptions.TopLeft; tt.characterSpacing = 8;
            Goth.Text(head, "S", sub, 21, GPal.Bone, TextAlignmentOptions.BottomLeft, false, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -60));
            var page = UIKit.Rect(_modal, "Page", Vector2.zero, Vector2.one, new Vector2(64, 76), new Vector2(-64, -134));   // Tab 수첩: on the picker's key line
            TrialFx.Sound("parchment", 0.6f);
            _picker = CluePicker.Build(page, Sim, claims, mode, (c, e, a) => { ClosePromptPicker(); commit(c, e, a); }, () => { ClosePromptPicker(); cancel(); });
        }
        void ClosePromptPicker() { _picker = null; UIKit.Clear(_modal); _modal.gameObject.SetActive(false); ChromeVisible(true); }

        /// <summary>Probe only: show the next decision moment even while the trial runs headless (one screenshot of it).</summary>
        public static bool ProbeMomentOnce;
        bool PlayerAlive() => S.A(Cast.Player)?.Alive == true;
        TrialClaim ClaimOf(string id) => id == null ? null : T?.Claims.FirstOrDefault(c => c.Id == id);

        // ---------------------------------------------------------------- decision moments
        // A witness under questioning, a chain of reasoning aimed at someone, two theories colliding: the court falls silent,
        // the eyes in the gallery turn, and the statements at stake sit beside your clue cards — one step: pick, present, or watch.
        bool _resumeAfterFocus;
        IEnumerator Moment(string p)
        {
            _busy++;
            var ids = new List<string>(); string title, sub = "이 말들 가운데 틀린 곳이 있다면, 뒤집을 단서를 내민다";
            if (p.StartsWith("witness:"))
            {
                var c0 = ClaimOf(p.Substring(8)); if (c0 != null) ids.Add(c0.Id);
                title = c0 != null ? Cast.GivenOf(c0.Speaker) + "의 증언" : "증언 심문"; sub = "직접 본 말인지, 전해 들은 말인지";
            }
            else if (p.StartsWith("chain:"))
            {
                var c0 = ClaimOf(p.Substring(6)); if (c0 != null) { ids.AddRange(c0.Premises); ids.Add(c0.Id); }
                title = c0 != null ? Cast.GivenOf(c0.Speaker) + "의 논리" : "이어지는 주장";
            }
            else
            {
                ids.AddRange(T.Claims.Where(c => c.Accused != null && c.Status != "refuted" && c.Status != "retracted").GroupBy(c => c.Accused).Select(g => g.OrderByDescending(x => x.Supporters.Count).ThenBy(x => x.Beat).First().Id).Take(3));
                title = "가설 대립";
            }
            var all = ids.Select(ClaimOf).Where(c => c != null && !string.IsNullOrEmpty(c.Text)).Distinct().ToList();
            // the same speaker saying the same thing about the same fact twice (two sightings a few minutes apart) is one row on
            // the page; a card presented against it goes to whichever of the two it actually breaks
            string FactOf(TrialClaim c) { try { return CaseBoard.Sentence(Sim, c.Prop) ?? ""; } catch (Exception) { return ""; } }
            var twins = new Dictionary<string, List<TrialClaim>>(); var claims = new List<TrialClaim>();
            foreach (var c in all)
            {
                var keep = claims.FirstOrDefault(o => o.Speaker == c.Speaker && o.Text == c.Text && FactOf(o) == FactOf(c));
                if (keep != null) { twins[keep.Id].Add(c); continue; }
                claims.Add(c); twins[c.Id] = new List<TrialClaim>();
            }
            claims = claims.Take(4).ToList();
            string BreakTarget(string claimId, string card)
            {
                if (claimId == null || card == null || !twins.TryGetValue(claimId, out var tw) || tw.Count == 0 || TrialGames.ProbeWorks(S, claimId, false, card)) return claimId;
                return tw.FirstOrDefault(t => TrialGames.ProbeWorks(S, t.Id, false, card))?.Id ?? claimId;
            }
            if (claims.Count == 0) { _busy = Mathf.Max(0, _busy - 1); _beat = null; NextBeat(); yield break; }
            ShotSpeaker(claims[claims.Count - 1].Speaker, true); EyesStare(0.85f); TrialFx.Sound("organ_sting", 0.45f);
            ChromeVisible(false);

            string act = null, cid = null, cardId = null;
            var rt = UIKit.Rect(FxRoot, "Moment", Vector2.zero, Vector2.one); var cg = rt.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0;
            UIKit.Img(rt, "Dim", GPal.A(Color.black, 0.66f), Vector2.zero, Vector2.one);
            var card = Goth.Panel(rt, "Card", new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f)); var cr = (RectTransform)card.transform;
            var tt = Goth.Engraved(cr, "T", title, 44, GPal.Gilt); tt.rectTransform.anchorMin = new Vector2(0, 1); tt.rectTransform.anchorMax = new Vector2(1, 1); tt.rectTransform.offsetMin = new Vector2(40, -84); tt.rectTransform.offsetMax = new Vector2(-40, -22); tt.alignment = TextAlignmentOptions.Center; tt.characterSpacing = 8;
            Goth.Text(cr, "S", sub, 21, GPal.Bone, TextAlignmentOptions.Center, false, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -118), new Vector2(-40, -86));
            UIKit.Img(cr, "Rule", GPal.A(GPal.Brass, 0.6f), new Vector2(0.3f, 1), new Vector2(0.7f, 1), new Vector2(0, -126), new Vector2(0, -125));
            // the countdown (설정 → 재판 제한 시간; off by default and never in the first trial of a save)
            int tset = Settings.TrialTime; float limit = tset == 1 ? 60f : tset == 2 ? 30f : 0f;
            bool firstTrial = S.Settlements.Count == 0; if (firstTrial) limit = 0f;
            Image bar = null; TextMeshProUGUI secs = null;
            if (limit > 0)
            {
                UIKit.Img(cr, "TimerTrack", GPal.A(GPal.Lacquer, 1f), new Vector2(0.3f, 1), new Vector2(0.7f, 1), new Vector2(0, -138), new Vector2(0, -133));
                bar = UIKit.Img(cr, "Timer", GPal.A(GPal.Brass, 0.9f), new Vector2(0.3f, 1), new Vector2(0.7f, 1), new Vector2(0, -138), new Vector2(0, -133));
                secs = Goth.Text(cr, "Secs", "", 16, GPal.A(GPal.Bone, 0.75f), TextAlignmentOptions.MidlineLeft, false, new Vector2(0.7f, 1), new Vector2(0.8f, 1), new Vector2(10, -146), new Vector2(0, -126));
            }
            var area = UIKit.Rect(cr, "Pick", Vector2.zero, Vector2.one, new Vector2(30, 22), new Vector2(-30, -152));
            var picker = CluePicker.Build(area, Sim, claims, CluePicker.Mode.Moment, (c, e, a) => { cid = c; cardId = e; act = a; }, () => { act = "pass"; });
            // the first trial walks you through one rebuttal: the guide stays armed until a moment where some card really breaks a
            // statement (that card is outlined in gold), or until your first rebuttal lands — a moment with nothing to break only
            // says how it works and does not use the lesson up
            bool guided = false; string gc = null, gcard = null;
            if (firstTrial && PlayerPrefs.GetInt("bl23.tut.court", 0) == 0)
            {
                guided = true;
                var cards = TrialGames.Arsenal(Sim).Select(b => b.Id).ToList();
                foreach (var c in claims.Where(c => c.Status != "refuted")) { var w = cards.FirstOrDefault(x => TrialGames.ProbeWorks(S, BreakTarget(c.Id, x), false, x)); if (w != null) { gc = c.Id; gcard = w; break; } }
                if (gcard != null)
                {
                    string ct = null; var gev = TrialGames.BulletEvidence(S, gcard);
                    try { ct = gev != null ? CaseBoard.Describe(Sim, gev)?.Title : null; } catch (Exception e) { Debug.LogException(e); }
                    var gt = CluePicker.Plain(ct ?? gev?.Title ?? "단서");
                    picker.Guide(gc, gcard, $"이 말은 「{gt}」{LineBank.Josa(gt, "와")} 맞지 않는다 — Enter로 내민다");
                    if (!AutoProbe.Active) { PlayerPrefs.SetInt("bl23.tut.court", 1); PlayerPrefs.Save(); }
                }
                else picker.Say("틀린 말이 보이면 단서를 골라 Enter. 확실하지 않으면 Esc로 지켜본다");
            }
            // probe only: find a card that breaks one of these statements, to present it once (the result plate photographs itself)
            if (AutoProbe.Active && gcard == null && !_plateShot && !_s.Headless)
            {
                var cards = TrialGames.Arsenal(Sim).Select(b => b.Id).ToList();
                foreach (var c in claims.Where(c => c.Status != "refuted")) { var w = cards.FirstOrDefault(x => TrialGames.ProbeWorks(S, BreakTarget(c.Id, x), false, x)); if (w != null) { gc = c.Id; gcard = w; break; } }
            }

            float t0 = Time.unscaledTime, used = 0f;
            while (act == null)
            {
                cg.alpha = Mathf.Clamp01((Time.unscaledTime - t0) / 0.35f);
                bool held = (_s.Note != null && _s.Note.Open) || (_s.Menu != null && _s.Menu.Open) || BacklogUI.Open;
                if (!held && _s.Note != null && _s.Note.ClosedFrame != Time.frameCount && (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.N))) _s.Note.OpenTab("evidence");
                if (limit > 0 && !held)
                {
                    used += Time.unscaledDeltaTime; float left = 1f - Mathf.Clamp01(used / limit);
                    bar.rectTransform.anchorMax = new Vector2(Mathf.Lerp(0.3f, 0.7f, left), 1); bar.color = left < 0.27f ? GPal.A(GPal.Wax, 0.95f) : GPal.A(GPal.Brass, 0.9f);
                    secs.text = Mathf.CeilToInt(limit - used) + "초";
                    if (left <= 0f) act = "timeout";
                }
                if (AutoProbe.Active && Time.unscaledTime - t0 > 1.1f)
                {
                    AutoProbe.Shot("trial_moment_pick"); if (guided) AutoProbe.Shot(gcard != null ? "trial_first_guide" : "trial_first_guide_idle");
                    yield return new WaitForSecondsRealtime(0.4f);
                    if (act == null)
                    {
                        // present the working card once (non-headless only: the result plate is what is being photographed)
                        if (gcard != null && !_plateShot && !_s.Headless) { AutoProbe.Note($"trial: moment — presenting a working card against {gc}"); cid = gc; cardId = gcard; act = "contra"; }
                        else act = "pass";
                    }
                }
                yield return null;
            }
            float f0 = Time.unscaledTime; while (Time.unscaledTime - f0 < 0.25f) { cg.alpha = 1f - (Time.unscaledTime - f0) / 0.25f; yield return null; }
            UnityEngine.Object.Destroy(rt.gameObject);
            EyesStare(0.3f);
            _busy = Mathf.Max(0, _busy - 1);
            switch (act)
            {
                case "source": ChromeVisible(true); OnPick(cid, null, "source"); break;
                case "contra": OnPick(BreakTarget(cid, cardId), cardId, "contra"); break;
                default:
                    ChromeVisible(true); Hint(act == "timeout" ? "망설이는 사이 논의는 다음으로 넘어갔다" : "지켜보기로 했다", 4f); _beat = null; NextBeat(); break;
            }
        }

        // ---------------------------------------------------------------- probe helpers (automation only)
        string _probeCard;
        /// <summary>Probe only: open 발언 되짚기 and select the first statement some card can break (the card is remembered).</summary>
        public void ProbePicker()
        {
            if (T == null || _inGame || _busy > 0) return;
            if (!_inFocus) OpenFocus(); if (_picker == null) return;
            var cards = TrialGames.Arsenal(Sim).Select(b => b.Id).ToList(); _probeCard = null;
            foreach (var c in CluePicker.ReviewClaims(T).Where(c => c.Status != "refuted"))
            {
                var w = cards.FirstOrDefault(x => TrialGames.ProbeWorks(S, c.Id, false, x));
                if (w != null) { _picker.Select(c.Id, null); _probeCard = w; return; }
            }
            var first = CluePicker.ReviewClaims(T).FirstOrDefault(); if (first != null) _picker.Select(first.Id, null);
        }
        /// <summary>Probe only: does some card break a statement heard so far (without opening anything)?</summary>
        public bool ProbeBreakable()
        {
            if (T == null) return false;
            var cards = TrialGames.Arsenal(Sim).Select(b => b.Id).ToList();
            return CluePicker.ReviewClaims(T).Where(c => c.Status != "refuted").Any(c => cards.Any(x => TrialGames.ProbeWorks(S, c.Id, false, x)));
        }
        /// <summary>Probe only: select the remembered card (or the top card) so its preview shows.</summary>
        public void ProbePickerCard() { if (_picker == null) return; var id = _probeCard ?? TrialGames.Arsenal(Sim).Select(b => b.Id).FirstOrDefault(); if (id != null) _picker.Select(null, id); }
        /// <summary>Probe only: present the remembered card if it breaks the statement (the result plate photographs itself).</summary>
        public bool ProbePickerCommit() { if (_picker == null || _probeCard == null || _picker.SelectedCard != _probeCard) return false; _picker.ProbeCommit(); return true; }
        public bool PickerOpen => _picker != null && !_picker.Closed;
        /// <summary>Probe only: the court is between lines (no round, prompt, picker or card on screen).</summary>
        public bool ProbeReady => Active && !_inGame && _busy == 0 && !_inFocus && !_modal.gameObject.activeSelf && !_verdictShown && T != null && T.PendingPrompt == null;

        string AutoPick() => TrialSystem.VoteCandidates(S).Where(x => x != Cast.Player).OrderByDescending(x => S.Know.TryGetValue(Cast.Player, out var k) && k.Suspicion.TryGetValue(x, out var v) ? v : 0).FirstOrDefault() ?? TrialSystem.VoteCandidates(S).First();

        IEnumerator RunGame(TrialMinigame g)
        {
            _inGame = true; _beat = null; ChromeVisible(false); _line.text = "";
            var game = T.Game;
            yield return Safe(g.Run());
            try { g.Cleanup(); } catch (Exception e) { Debug.LogException(e); }
            // safety: a round must never leave the kernel waiting
            if (T != null && T.Game == game && game != null && game.Status == "open") { Debug.LogWarning("[BL23 trial] round ended without resolution: " + game.Kind); TrialGames.AutoResolve(Sim, false); }
            if (T != null && T.PendingPrompt != null && T.PendingPrompt.StartsWith("game:") && (T.Game == null || T.Game.Status != "open")) T.PendingPrompt = null;
            _inGame = false; ChromeVisible(true);
            MusicDirector.I?.SetState(T != null && T.Stage == "Final" ? MusicState.TrialClimax : MusicState.TrialDebate);
            _beat = null; NextBeat();
        }

        /// <summary>Runs a nested coroutine tree; an exception ends it (logged) instead of freezing the trial.</summary>
        static IEnumerator Safe(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(root);
            while (stack.Count > 0)
            {
                var top = stack.Peek(); bool moved; object cur;
                try { moved = top.MoveNext(); cur = moved ? top.Current : null; }
                catch (Exception e) { Debug.LogException(e); yield break; }
                if (!moved) { stack.Pop(); continue; }
                if (cur is IEnumerator nested) { stack.Push(nested); continue; }
                yield return cur;
            }
        }

        IEnumerator AccusePrompt()
        {
            MusicDirector.I?.SetState(MusicState.TrialClimax);
            var cands = T.Participants.Where(x => x != Cast.Player).ToList();
            string pick = null; bool done = false;
            yield return PortraitPick("지목", "이 사건의 범인은 누구인가", cands, null, id => { pick = id; done = true; }, false, AutoProbe.Active ? AutoPick() : null);
            while (!done) yield return null;
            T.PendingPrompt = null;
            if (pick != null) { yield return GFx.Interject(FxRoot, Cast.Player, "범인은 너야, " + Cast.GivenOf(pick) + ".", GPal.Wax, 0.7f); TrialSystem.PlayerAccuse(Sim, pick); }
            _busy = Mathf.Max(0, _busy - 1);
            _beat = null; NextBeat();
        }

        IEnumerator VotePrompt()
        {
            MusicDirector.I?.SetState(MusicState.Vote);
            string pick = null; bool done = false;
            var alive = TrialSystem.VoteCandidates(S); var dead = TrialSystem.DeadCandidates(S);
            yield return PortraitPick("투표", "범인이라고 생각하는 사람의 초상 아래 검은 돌을 하나 넣는다 — 이름은 남지 않고, 기권은 없다", alive, dead, id => { pick = id; done = true; }, false, AutoProbe.Active ? AutoPick() : null);
            while (!done) yield return null;
            TrialSystem.PlayerVote(Sim, pick ?? AutoPick()); T.PendingPrompt = null;
            _busy = Mathf.Max(0, _busy - 1);
            _beat = null; NextBeat();
        }

        /// <summary>A gallery of arched portrait frames (accusation / vote). Dead candidates, if given, are a second gallery.</summary>
        IEnumerator PortraitPick(string title, string sub, List<string> ids, List<string> dead, Action<string> done, bool cancel, string auto = null)
        {
            ChromeVisible(false); var rt = UIKit.Rect(GameRoot, "Pick", Vector2.zero, Vector2.one); var rcg = rt.gameObject.AddComponent<CanvasGroup>(); rcg.alpha = 0;
            UIKit.Img(rt, "Dim", GPal.A(Color.black, 0.8f), Vector2.zero, Vector2.one);
            Goth.Glow(rt, "Glow", GPal.A(GPal.Candle, 0.1f), new Vector2(1800, 1000), Vector2.zero);
            var head = Goth.Engraved(rt, "T", title, 60); head.rectTransform.anchorMin = new Vector2(0, 0.86f); head.rectTransform.anchorMax = new Vector2(1, 0.97f); head.characterSpacing = 20;
            UIKit.Img(rt, "Rule", GPal.A(GPal.Brass, 0.7f), new Vector2(0.44f, 0.855f), new Vector2(0.56f, 0.855f), new Vector2(0, -1), new Vector2(0, 1));
            Goth.Text(rt, "S", sub, 23, GPal.Bone, TextAlignmentOptions.Top, false, new Vector2(0, 0.79f), new Vector2(1, 0.845f));
            var grid = UIKit.Rect(rt, "Grid", new Vector2(0, 0), new Vector2(1, 1), new Vector2(90, 130), new Vector2(-90, -240));
            var bar = UIKit.Rect(rt, "Bar", new Vector2(0, 0), new Vector2(1, 0), new Vector2(90, 30), new Vector2(-90, 96));
            string chosen = null; bool showDead = false; bool finished = false; string result = null; bool dirty = true;
            TrialFx.Sound("parchment", 0.6f); EyesStare(0.8f);
            void Build()
            {
                UIKit.Clear(grid); UIKit.Clear(bar);
                var list = showDead && dead != null ? dead : ids; int n = list.Count; int cols = n <= 6 ? Mathf.Max(1, n) : n <= 10 ? 5 : n <= 14 ? 7 : 8; int rows = Mathf.Max(1, Mathf.CeilToInt(n / (float)cols));
                for (int i = 0; i < n; i++)
                {
                    var id = list[i]; int cx = i % cols, cy = i / cols; bool sel = id == chosen;
                    var cell = UIKit.Rect(grid, "C" + id, new Vector2(cx / (float)cols, 1 - (cy + 1) / (float)rows), new Vector2((cx + 1) / (float)cols, 1 - cy / (float)rows), new Vector2(12, 12), new Vector2(-12, -12));
                    var f = Goth.Frame(cell, "F", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true); f.raycastTarget = true; f.Gap = 5; f.Width = sel ? 2.5f : 1.5f;
                    var face = UIKit.Rect(cell, "Face", new Vector2(0, 0.2f), new Vector2(1, 0.88f), new Vector2(18, 0), new Vector2(-18, 0));
                    var raw = face.gameObject.AddComponent<RawImage>(); raw.raycastTarget = false; var tex = TrialPortraits.Get(id); raw.texture = tex;
                    raw.color = tex != null ? (showDead ? new Color(0.45f, 0.42f, 0.4f) : new Color(1, 0.95f, 0.88f)) : GPal.Lacquer; raw.uvRect = new Rect(0.05f, 0.1f, 0.9f, 0.8f);
                    if (tex == null) Goth.Text(face, "Q", Goth.Initial(id), 60, GPal.BrassDim, TextAlignmentOptions.Center, true);
                    var nm = Goth.Text(cell, "N", Cast.NameOf(id) + (id == Cast.Player ? "  (당신)" : "") + (showDead ? "  · 사망" : ""), 20, sel ? GPal.Gilt : GPal.Bone, TextAlignmentOptions.Center, true, new Vector2(0, 0.03f), new Vector2(1, 0.19f)); nm.enableAutoSizing = true; nm.fontSizeMin = 13; nm.fontSizeMax = 20;
                    var btn = cell.gameObject.AddComponent<GBtn>(); btn.Frame = f; btn.Label = nm; btn.Selected = sel; var idd = id;
                    btn.OnClick = () => { if (chosen == idd) { result = idd; finished = true; } else { chosen = idd; dirty = true; } };
                    btn.Paint(); if (sel) { f.Border = GPal.Gilt; f.Refresh(); }
                }
                var confirm = Goth.Button(bar, chosen != null ? $"결정 — {Cast.NameOf(chosen)}" : "한 사람을 골라 주세요", () => { if (chosen != null) { result = chosen; finished = true; } }, new Vector2(0.62f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero, 26, true);
                confirm.Interactable = chosen != null; confirm.Paint();
                if (dead != null && dead.Count > 0) Goth.Button(bar, showDead ? "살아 있는 사람" : $"확인된 사망자 ({dead.Count})", () => { showDead = !showDead; chosen = null; dirty = true; }, new Vector2(0, 0), new Vector2(0.3f, 1), Vector2.zero, Vector2.zero, 22);
                if (cancel) Goth.Button(bar, "취소", () => { result = null; finished = true; }, new Vector2(0.33f, 0), new Vector2(0.52f, 1), Vector2.zero, Vector2.zero, 22);
            }
            float t0 = Time.unscaledTime;
            while (!finished)
            {
                if (dirty) { dirty = false; Build(); }
                rcg.alpha = Mathf.MoveTowards(rcg.alpha, 1, Time.unscaledDeltaTime * 3f);
                if (cancel && Input.GetKeyDown(KeyCode.Escape)) { result = null; finished = true; }
                if (chosen != null && Input.GetKeyDown(KeyCode.Return)) { result = chosen; finished = true; }
                if (auto != null && Time.unscaledTime - t0 > 1.0f && chosen == null) { chosen = auto; dirty = true; TrialFx.Sound("quill", 0.5f); }
                if (auto != null && Time.unscaledTime - t0 > 2.0f && chosen != null) { AutoProbe.Shot("trial_pick_" + (title == "투표" ? "vote" : "accuse")); yield return new WaitForSecondsRealtime(0.2f); result = chosen; finished = true; }
                yield return null;
            }
            TrialFx.Sound(result != null ? "wax_stamp" : "parchment", 0.7f);
            float f0 = Time.unscaledTime; while (Time.unscaledTime - f0 < 0.3f) { rcg.alpha = 1 - GFx.K(f0, 0.3f); yield return null; }
            UnityEngine.Object.Destroy(rt.gameObject);
            ChromeVisible(true); done(result);
        }

        // ---------------------------------------------------------------- the vote: black stones into urns under the portraits
        IEnumerator UrnReveal(TrialBeat b)
        {
            var votes = (b.Data ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('>')).Where(x => x.Length == 2).Select(x => (voter: x[0], who: x[1])).ToList();
            var tally = votes.GroupBy(v => v.who).Select(g => (who: g.Key, n: g.Count())).OrderByDescending(x => x.n).ThenBy(x => x.who, StringComparer.Ordinal).ToList();
            if (tally.Count == 0) yield break;
            bool tie = tally.Count > 1 && tally[0].n == tally[1].n;
            ChromeVisible(false); if (_keys != null) _keys.gameObject.SetActive(false); var rt = UIKit.Rect(GameRoot, "Urns", Vector2.zero, Vector2.one);
            UIKit.Img(rt, "Dim", GPal.A(Color.black, 0.8f), Vector2.zero, Vector2.one);
            Goth.Glow(rt, "Glow", GPal.A(GPal.Candle, 0.14f), new Vector2(1800, 900), new Vector2(0, -60));
            var head = Goth.Engraved(rt, "T", "투 표", 60); head.rectTransform.anchorMin = new Vector2(0, 0.86f); head.rectTransform.anchorMax = new Vector2(1, 0.98f); head.characterSpacing = 24;
            var cap = Goth.Text(rt, "C", "검은 돌이 하나씩 항아리에 떨어진다 — 누가 넣었는지는 아무도 모른다", 22, GPal.Bone, TextAlignmentOptions.Top, false, new Vector2(0, 0.8f), new Vector2(1, 0.86f));
            var cand = tally.Take(6).Select(x => x.who).ToList(); int n = cand.Count; float cw = Mathf.Min(260, (rt.rect.width - 120) / n);
            var urns = new Dictionary<string, (RectTransform urn, TextMeshProUGUI count, int stones)>();
            for (int i = 0; i < n; i++)
            {
                var id = cand[i]; float x = (i - (n - 1) * 0.5f) * cw;
                var frame = TrialFx.Centered(rt, "P" + id, new Vector2(cw - 40, (cw - 40) * 1.25f), new Vector2(x, rt.rect.height * 0.14f));
                var f = Goth.Frame(frame, "F", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true); f.Fill = GPal.A(GPal.Lacquer, 0.95f); f.FillBottom = GPal.A(GPal.OakDark, 0.95f); f.Border = GPal.Brass;
                var face = UIKit.Rect(frame, "Face", new Vector2(0, 0.14f), new Vector2(1, 0.86f), new Vector2(12, 0), new Vector2(-12, 0)); var raw = face.gameObject.AddComponent<RawImage>(); raw.raycastTarget = false; raw.texture = TrialPortraits.Get(id); raw.color = raw.texture != null ? new Color(1, 0.94f, 0.86f) : GPal.Lacquer; raw.uvRect = new Rect(0.05f, 0.1f, 0.9f, 0.8f);
                Goth.Text(frame, "N", Cast.NameOf(id), 18, GPal.Bone, TextAlignmentOptions.Center, true, new Vector2(0, 0), new Vector2(1, 0.14f));
                var urn = TrialFx.Centered(rt, "U" + id, new Vector2(cw * 0.5f, cw * 0.55f), new Vector2(x, -rt.rect.height * 0.1f));   // (above the key hints and the dialogue box)
                var ug = urn.gameObject.AddComponent<UrnGraphic>(); ug.raycastTarget = false;
                var cnt = Goth.Text(urn, "Cnt", "", 40, GPal.Gilt, TextAlignmentOptions.Center, true, new Vector2(0, 0.25f), new Vector2(1, 0.75f)); cnt.outlineWidth = 0.2f; cnt.outlineColor = new Color32(20, 10, 4, 255);
                urns[id] = (urn, cnt, 0);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            // stones drop one by one; the anonymous ballot shows no voter
            var order = votes.Where(v => urns.ContainsKey(v.who)).ToList(); var rng = new System.Random(order.Count * 31 + n);
            order = order.OrderBy(_ => rng.Next()).ToList();
            float gap = ProbeFast ? 0.08f : Mathf.Clamp(2.8f / Mathf.Max(1, order.Count), 0.12f, 0.35f);
            foreach (var v in order)
            {
                var (urn, cnt, stones) = urns[v.who];
                var stone = TrialFx.Centered(rt, "Stone", new Vector2(26, 26), urn.anchoredPosition + new Vector2(UnityEngine.Random.Range(-10f, 10f), urn.sizeDelta.y + 60)); var sg = stone.gameObject.AddComponent<RingGraphic>(); sg.Thickness = 13; sg.color = new Color(0.06f, 0.06f, 0.07f); sg.raycastTarget = false;
                StartCoroutine(DropStone(stone, urn.anchoredPosition + new Vector2(0, urn.sizeDelta.y * 0.25f)));
                stones++; urns[v.who] = (urn, cnt, stones);
                yield return new WaitForSecondsRealtime(0.18f);
                cnt.text = stones.ToString(); TrialFx.Sound("trial_coin", 0.6f);
                yield return new WaitForSecondsRealtime(gap);
            }
            var top = tally[0].who;
            if (!tie)
            {
                var (urn, _, _) = urns[top]; Goth.Glow(rt, "Win", GPal.A(GPal.Candle, 0.45f), new Vector2(cw * 1.6f, 700), new Vector2(urn.anchoredPosition.x, 0)).transform.SetAsFirstSibling();
                cap.text = $"가장 많은 돌을 받은 사람 — {Cast.NameOf(top)}"; TrialFx.Sound("verdict", 1f);
            }
            else { cap.text = "돌의 수가 같다 — 다시 던지거나, 모두 앞에서 추첨한다"; TrialFx.Sound("bell_toll", 0.6f); }
            yield return new WaitForSecondsRealtime(0.4f);
            AutoProbe.Shot("trial_vote_urns");
            float h0 = Time.unscaledTime; while (Time.unscaledTime - h0 < (ProbeFast ? 1.2f : 2.8f)) { if (Time.unscaledTime - h0 > 0.8f && (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))) break; yield return null; }
            UnityEngine.Object.Destroy(rt.gameObject); if (_keys != null) _keys.gameObject.SetActive(true);
            yield return WellBeat("vote", 3.6f);   // (cinematics) the urns close: the well wakes; a look straight up, no UI
            ChromeVisible(true);
        }
        static IEnumerator DropStone(RectTransform s, Vector2 to) { var from = s.anchoredPosition; float t0 = Time.unscaledTime; while (s != null && Time.unscaledTime - t0 < 0.25f) { float k = GFx.K(t0, 0.25f); s.anchoredPosition = Vector2.Lerp(from, to, k * k); yield return null; } if (s != null) UnityEngine.Object.Destroy(s.gameObject); }

        // ---------------------------------------------------------------- verdict → execution → reveal
        public void ShowVerdict() { if (!_verdictShown) StartCoroutine(VerdictCo()); }
        IEnumerator VerdictCo()
        {
            _verdictShown = true; _c.enabled = true; _cam.enabled = true; _hc.enabled = false; Active = true; ChromeVisible(true);
            while (_busy > 0 && !_s.Headless) yield return null;
            var st = S.Settlements.LastOrDefault();
            // the court falls silent; the one who has sat still on the judge's seat the whole time speaks, once
            _mode.text = "판결"; _topic.text = ""; _hint.text = ""; _line.text = ""; _speaker.text = ""; _namePlate.gameObject.SetActive(false); _claimTag.gameObject.SetActive(false); _boxTarget = 0;
            var judge = JudgeHead(); ShotJudge(judge); EyesWatch(judge);
            yield return new WaitForSecondsRealtime(_s.Headless ? 0.05f : ProbeFast ? 0.8f : 2f);
            if (st != null)
            {
                bool correct = st.Correct;
                MusicDirector.I?.SetState(st.Exception ? MusicState.Aftermath : correct ? MusicState.VerdictCorrect : MusicState.VerdictWrong);
                string nm = Cast.NameOf(st.Accused) ?? "그 사람";
                string text = correct ? nm + LineBank.Josa(nm, "은") + " 범인입니다." : nm + LineBank.Josa(nm, "은") + " 범인이 아닙니다.";
                _boxTarget = 1; _speaker.text = Cast.NameOf(Cast.Butler) ?? "유스티"; _namePlate.gameObject.SetActive(true);
                _line.color = GPal.Bone; _line.fontStyle = FontStyles.Normal; _line.text = text; _line.maxVisibleCharacters = 9999; _lineFade = 0;
                VoiceBabble.Speak(Cast.Butler, text); _s.World.ViewOf(Cast.Butler)?.Talk(1.6f);
                TrialFx.Sound("bell_toll", 1f); EyesStare(1f); CourtBeat("verdict");
                yield return Hold(2.6f);
                yield return WellBeat("verdict", 3.6f, false);   // (cinematics) up the well: the eyes wake band by band, the pupil closes
                ShotJudge(JudgeHead()); yield return Hold(1.2f);
                if (!_s.Headless) yield return GFx.Plate(FxRoot, st.Exception ? "심판 종료" : correct ? "진범" : "오판", st.Exception ? CluePicker.Plain(st.Note) : correct ? "지목한 사람이 범인이었다" : "지목한 사람은 범인이 아니었다", correct ? GPal.Sapphire : GPal.Oxblood, 2f, correct ? "choir_swell" : "organ_sting");
                _speaker.text = ""; _namePlate.gameObject.SetActive(false); _line.color = GPal.Brass; _line.fontStyle = FontStyles.Italic;
                _line.text = LineBank.FixParticles(CluePicker.Plain(S.LastVerdictSummary ?? "")); _line.maxVisibleCharacters = 9999; _lineFade = 0; yield return Hold(3f);
                _line.fontStyle = FontStyles.Normal; _line.color = GPal.Bone;
                if (st.Escaped != null) yield return Escape(st.Escaped);
                if (st.Executed != null) yield return Execute(st.Executed, correct);
            }
            End();
            _s.Reveal.Play(() => _s.FinishChapterPresentation());
        }

        IEnumerator Hold(float secs) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < (_s.Headless ? 0.02f : ProbeFast ? Mathf.Min(secs, 1.2f) : secs)) { if (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)) break; yield return null; } }

        IEnumerator Escape(string id)
        {
            MusicDirector.I?.SetState(MusicState.Escape);
            var v = _s.World.ViewOf(id); _mode.text = "탈출"; _speaker.text = Cast.NameOf(id); _namePlate.gameObject.SetActive(true); _boxTarget = 1; _lineFade = 0;
            _line.text = string.Join(" ", LineBank.Pages(Sim.Render(id, null, "escape_line") ?? "…")); _line.maxVisibleCharacters = 9999; VoiceBabble.Speak(id, _line.text);
            ShotSpeaker(id, false); v?.Rig?.SetExpression(Expr.Smirk);
            yield return Hold(4f);
            v?.Rig?.SetVisible(false);
        }

        IEnumerator Execute(string id, bool correct)
        {
            MusicDirector.I?.SetState(MusicState.Execution);
            var v = _s.World.ViewOf(id); _mode.text = "처형"; _speaker.text = Cast.NameOf(id); _namePlate.gameObject.SetActive(true); _boxTarget = 1; _lineFade = 0; EyesWatch(v != null ? v.HeadPos : _center); EyesStare(1f);
            var lastWords = LineBank.Pages(Sim.Render(id, null, correct ? "confess" : "execution_last") ?? "…");
            _line.text = string.Join(" ", lastWords); _line.maxVisibleCharacters = 9999; VoiceBabble.Speak(id, lastWords.FirstOrDefault());
            ShotSpeaker(id, true); v?.Rig?.SetExpression(correct ? Expr.Crying : Expr.Fear);
            yield return Hold(5f);
            // dreamlike execution: the court floods with aquarium light, fish circle, the figure sinks into the water
            var fx = ExecutionFx.Spawn(v != null ? v.transform.position : _center, id); CourtBeat("execution");
            _line.text = ""; _speaker.text = "";
            for (float t = 0; t < (_s.Headless ? 0.1f : ProbeFast ? 2f : 5f); t += Time.unscaledDeltaTime)
            {
                if (v != null) v.transform.position += Vector3.down * Time.unscaledDeltaTime * 0.35f;
                _camWantPos = (v != null ? v.transform.position : _center) + new Vector3(Mathf.Sin(t * 0.4f) * 4, 2.5f + t * 0.3f, Mathf.Cos(t * 0.4f) * 4); _camWantLook = v != null ? v.transform.position + Vector3.up : _center; _dT0 = -1;
                yield return null;
            }
            v?.Rig?.SetVisible(false); fx?.Stop();
        }

        // ---------------------------------------------------------------- bodies follow words
        /// <summary>Gesture + face for a spoken line (shared mapper: words, punctuation, emotion, intent).</summary>
        public static void Perform(ActorView v, string text, Emotion emo, bool accusation)
        {
            if (v?.Rig == null || string.IsNullOrEmpty(text)) return;
            SpeechGestures.Perform(v, text, emo, accusation);
        }

        static string ModeName(TrialMode m)
        {
            switch (m)
            {
                case TrialMode.TM01_Debate: return "자유 논의"; case TrialMode.TM02_Crossfire: return "교차 논쟁"; case TrialMode.TM03_Witness: return "집중 심문";
                case TrialMode.TM04_Chain: return "이어지는 주장"; case TrialMode.TM05_Theory: return "가설 대립"; case TrialMode.TM06_Joint: return "합동 논증";
                case TrialMode.TM07_Reconstruct: return "재구성"; case TrialMode.TM08_FinalDefense: return "최종 변론"; case TrialMode.Vote: return "투표";
            }
            return "심판";
        }
    }

    /// <summary>Stylized execution: aquarium flood light and a sparse drift of soft ash motes. Not a gore sequence.</summary>
    public sealed class ExecutionFx : MonoBehaviour
    {
        ParticleSystem _ps; Light _light;
        public static ExecutionFx Spawn(Vector3 pos, string id)
        {
            var go = new GameObject("ExecutionFx"); go.transform.position = pos; var fx = go.AddComponent<ExecutionFx>();
            fx._light = new GameObject("L").AddComponent<Light>(); fx._light.transform.SetParent(go.transform, false); fx._light.transform.localPosition = Vector3.up * 3; fx._light.type = LightType.Point; fx._light.range = 14; fx._light.intensity = 12; fx._light.color = new Color(0.2f, 0.9f, 1f);
            // (cinematics, court request) no flat cyan squares: a sparse drift of soft round motes, ash / cold white, low alpha
            fx._ps = go.AddComponent<ParticleSystem>(); var main = fx._ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6.5f); main.startSpeed = 0.12f; main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.8f, 0.84f, 0.2f), new Color(0.92f, 0.94f, 0.98f, 0.3f)); main.maxParticles = 120; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = fx._ps.emission; em.rateOverTime = 18; var sh = fx._ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.6f; sh.rotation = new Vector3(-90, 0, 0);
            var vel = fx._ps.velocityOverLifetime; vel.enabled = true; vel.y = 0.28f; vel.orbitalY = 0.15f;
            var col = fx._ps.colorOverLifetime; col.enabled = true; var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f) }); col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = BL23.Game.Mansion.MansionMats.Particle("ExecAsh", BL23.Game.Mansion.MansionMats.Proc("SoftDot"), false);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            Audio.Sfx.Play("water_splash", pos, 1f);
            return fx;
        }
        public void Stop() { if (_ps) _ps.Stop(); Destroy(gameObject, 3f); }
        void Update() { if (_light) _light.intensity = 10 + Mathf.Sin(Time.time * 3) * 3; }
    }
}
