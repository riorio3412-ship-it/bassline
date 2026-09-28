using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Audio;
using BL23.Game.Characters;
using BL23.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BL23.Game
{
    /// <summary>
    /// Base of the court's rounds. A round is a coroutine that presents the kernel's TrialGame over the 3D courtroom and resolves it
    /// only through TrialGames (Seal / Press / LedgerPick / BoardTie / FinalBoard / QuestionPick / Reconstruct* / Timeout).
    /// Shared furniture: a title card, a one-time illustrated instruction card, a frameless header, the burning taper (the round's
    /// clock), key prompts, evidence cards. Under AutoProbe a round demonstrates itself with a virtual cursor and captures screenshots.
    /// </summary>
    public abstract class TrialMinigame
    {
        protected readonly TrialDirectorUI H; protected readonly TrialGame G;
        protected Session Ses => H.Ses; protected Simulation Sim => Ses.Sim; protected GameState S => Ses.S; protected TrialState T => S.Trial;
        protected RectTransform Root; protected bool Done;
        protected bool Probe => AutoProbe.Active;
        protected float W => Root.rect.width; protected float Ht => Root.rect.height;

        protected TrialMinigame(TrialDirectorUI h) { H = h; G = h.Ses.S.Trial.Game; Root = UIKit.Rect(h.GameRoot, GetType().Name, Vector2.zero, Vector2.one); }
        public abstract IEnumerator Run();
        public virtual void Cleanup() { if (Root) UnityEngine.Object.Destroy(Root.gameObject); }

        // ------------------------------------------------------------ input (a virtual cursor drives the probe demo)
        protected Vector2 Virt; protected bool UseVirt, VirtDown, VirtPressed, VirtReleased;
        protected Vector2 MouseLocal()
        {
            if (UseVirt) return Virt;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, Input.mousePosition, null, out var p); return p;
        }
        protected bool MouseDown => UseVirt ? VirtPressed : Input.GetMouseButtonDown(0);
        protected bool MouseHeld => UseVirt ? VirtDown : Input.GetMouseButton(0);
        protected bool MouseUp => UseVirt ? VirtReleased : Input.GetMouseButtonUp(0);
        protected void EndVirtFrame() { VirtPressed = false; VirtReleased = false; }
        protected Vector2 ToLocal(RectTransform rt, Vector2 rootLocal) => rt.InverseTransformPoint(Root.TransformPoint(rootLocal));
        protected Vector2 ToRoot(RectTransform rt, Vector2 local) => Root.InverseTransformPoint(rt.TransformPoint(local));
        protected bool Inside(RectTransform rt, Vector2 rootLocal) => rt != null && rt.gameObject.activeInHierarchy && rt.rect.Contains(ToLocal(rt, rootLocal));
        protected Vector2 CenterOf(RectTransform rt) => ToRoot(rt, rt.rect.center);
        protected static float Dt => Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        /// <summary>Virtual drag for the probe: press at a, move to b over 'secs', release.</summary>
        protected IEnumerator VirtDrag(Vector2 a, Func<Vector2> b, float secs)
        {
            UseVirt = true; Virt = a; VirtDown = true; VirtPressed = true; yield return null; yield return null;
            float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < secs) { Virt = Vector2.Lerp(a, b(), GFx.EaseOut(GFx.K(t0, secs))); yield return null; }
            Virt = b(); yield return null; VirtDown = false; VirtReleased = true; yield return null;
        }
        protected IEnumerator VirtClick(Vector2 p) { UseVirt = true; Virt = p; yield return null; VirtDown = true; VirtPressed = true; yield return null; VirtDown = false; VirtReleased = true; yield return null; }

        /// <summary>Audible hover: a small brass tick whenever the thing under the cursor changes (and not on every frame).</summary>
        readonly Dictionary<string, object> _hovKey = new Dictionary<string, object>();
        protected void HoverSfx(object key, string slot = "main", string id = "plate_hover", float vol = 0.35f) { _hovKey.TryGetValue(slot, out var old); if (key != null && !ReferenceEquals(key, old)) TrialFx.Sound(id, vol); _hovKey[slot] = key; }

        // ------------------------------------------------------------ shared pieces
        /// <summary>The round's title: gilt serif on black lacquer; the bell tolls once.</summary>
        protected IEnumerator Intro(string title, string sub, string flavour)
        {
            var rt = UIKit.Rect(H.FxRoot, "Intro", Vector2.zero, Vector2.one);
            var dim = UIKit.Img(rt, "Dim", GPal.A(Color.black, 0f), Vector2.zero, Vector2.one);
            var plate = UIKit.Rect(rt, "Plate", new Vector2(0.24f, 0.37f), new Vector2(0.76f, 0.65f));
            var glow = Goth.Glow(plate, "Glow", GPal.A(GPal.Candle, 0f), new Vector2(1400, 560), Vector2.zero);
            Goth.Panel(plate, "F", Vector2.zero, Vector2.one);
            var t = Goth.Engraved(plate, "T", title, 84); t.rectTransform.anchorMin = new Vector2(0, 0.44f); t.rectTransform.anchorMax = new Vector2(1, 0.94f);
            UIKit.Img(plate, "Rule", GPal.A(GPal.Brass, 0.7f), new Vector2(0.36f, 0.43f), new Vector2(0.64f, 0.43f), new Vector2(0, -1), new Vector2(0, 1));
            var s = Goth.Text(plate, "S", sub ?? "", 28, GPal.Bone, TextAlignmentOptions.Center, false, new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.41f)); s.characterSpacing = 3;
            var f = Goth.Text(plate, "Fl", flavour ?? "", 21, GPal.A(GPal.Brass, 0.95f), TextAlignmentOptions.Center, false, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.2f));
            TrialFx.Sound("bell_toll", 0.65f);
            float t0 = Time.unscaledTime; var cgs = plate.gameObject.AddComponent<CanvasGroup>();
            while (Time.unscaledTime - t0 < 0.6f) { float k = GFx.EaseOut(GFx.K(t0, 0.6f)); cgs.alpha = k; dim.color = GPal.A(Color.black, 0.45f * k); glow.color = GPal.A(GPal.Candle, 0.22f * k); yield return null; }
            float h0 = Time.unscaledTime; while (Time.unscaledTime - h0 < (TrialDirectorUI.ProbeFast ? 0.8f : 1.5f)) { if (Time.unscaledTime - h0 > 0.4f && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E))) break; yield return null; }
            if (Probe) AutoProbe.Shot("trial_" + G.Kind + "_intro");
            float f0 = Time.unscaledTime; while (Time.unscaledTime - f0 < 0.45f) { float k = GFx.EaseInOut(GFx.K(f0, 0.45f)); cgs.alpha = 1 - k; dim.color = GPal.A(Color.black, 0.45f * (1 - k)); yield return null; }
            UnityEngine.Object.Destroy(rt.gameObject);
        }

        /// <summary>The first time a kind of round is played: one illustrated card with three short steps. Click or E to begin.</summary>
        protected IEnumerator Teach(string title, string[] steps, Action<RectTransform> art, string id = null)
        {
            string key = "bl23.teach." + (id ?? G.Kind);
            if (!Probe && PlayerPrefs.GetInt(key, 0) == 1) yield break;
            if (!Probe) { PlayerPrefs.SetInt(key, 1); PlayerPrefs.Save(); }
            var rt = UIKit.Rect(H.FxRoot, "Teach", Vector2.zero, Vector2.one);
            var dim = UIKit.Img(rt, "Dim", GPal.A(Color.black, 0f), Vector2.zero, Vector2.one);
            var card = UIKit.Rect(rt, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-600, -290), new Vector2(600, 290)); var cg = card.gameObject.AddComponent<CanvasGroup>();
            Goth.Panel(card, "F", Vector2.zero, Vector2.one);
            var pic = UIKit.Rect(card, "Art", new Vector2(0, 0), new Vector2(0.46f, 1), new Vector2(36, 96), new Vector2(0, -36));
            Goth.Parchment(pic, "Paper", Vector2.zero, Vector2.one);
            try { art?.Invoke(pic); } catch (Exception e) { Debug.LogException(e); }
            var right = UIKit.Rect(card, "Text", new Vector2(0.46f, 0), new Vector2(1, 1), new Vector2(44, 96), new Vector2(-40, -36));
            var tt = Goth.Engraved(right, "T", title, 40); tt.alignment = TextAlignmentOptions.TopLeft; tt.rectTransform.anchorMin = new Vector2(0, 1); tt.rectTransform.anchorMax = new Vector2(1, 1); tt.rectTransform.offsetMin = new Vector2(0, -56); tt.rectTransform.offsetMax = Vector2.zero; tt.characterSpacing = 4;
            UIKit.Img(right, "Rule", GPal.A(GPal.Brass, 0.7f), new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(0, -68));
            float y = -100;
            for (int i = 0; i < steps.Length; i++)
            {
                var n = Goth.Text(right, "N" + i, (i + 1).ToString(), 30, GPal.Gilt, TextAlignmentOptions.TopLeft, true, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y - 60), new Vector2(34, y));
                var s = Goth.Text(right, "S" + i, steps[i], 23, GPal.Bone, TextAlignmentOptions.TopLeft, false, new Vector2(0, 1), new Vector2(1, 1), new Vector2(44, y - 110), new Vector2(0, y + 2)); s.lineSpacing = 6;
                float h = Mathf.Max(56, s.GetPreferredValues(steps[i], right.rect.width > 10 ? right.rect.width - 44 : 520, 0).y + 22); s.rectTransform.offsetMin = new Vector2(44, y - h); y -= h + 8;
            }
            var keys = Goth.KeyBar(card, "클릭 / E|시작하기"); keys.anchoredPosition = new Vector2(-40, 26);
            TrialFx.Sound("parchment", 0.7f);
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.45f) { float k = GFx.EaseOut(GFx.K(t0, 0.45f)); cg.alpha = k; dim.color = GPal.A(Color.black, 0.55f * k); yield return null; }
            cg.alpha = 1; float h0 = Time.unscaledTime;
            if (Probe) { yield return new WaitForSecondsRealtime(1.1f); AutoProbe.Shot("trial_" + (id ?? G.Kind) + "_teach"); }
            else while (!(Time.unscaledTime - h0 > 0.6f && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))) yield return null;
            TrialFx.Sound("ui_confirm", 0.5f);
            float f0 = Time.unscaledTime; while (Time.unscaledTime - f0 < 0.35f) { float k = GFx.EaseInOut(GFx.K(f0, 0.35f)); cg.alpha = 1 - k; dim.color = GPal.A(Color.black, 0.55f * (1 - k)); yield return null; }
            UnityEngine.Object.Destroy(rt.gameObject);
        }

        /// <summary>Top-left: the round's name and what is at stake, engraved straight onto the scene over a soft shadow (no box).</summary>
        protected RectTransform Header(string title, string sub)
        {
            var rt = UIKit.Rect(Root, "Header", new Vector2(0, 1), new Vector2(0, 1)); rt.pivot = new Vector2(0, 1); rt.sizeDelta = new Vector2(640, 96); rt.anchoredPosition = new Vector2(36, -24);
            var bed = UIKit.Rect(rt, "Bed", Vector2.zero, Vector2.one, new Vector2(-120, -60), new Vector2(60, 50)); var bi = bed.gameObject.AddComponent<RawImage>(); bi.texture = GTex.Glow; bi.color = new Color(0, 0, 0, 0.6f); bi.raycastTarget = false;
            var t = Goth.Engraved(rt, "T", title, 38); t.alignment = TextAlignmentOptions.TopLeft; t.rectTransform.offsetMin = new Vector2(0, 40); t.rectTransform.offsetMax = new Vector2(0, 0);
            UIKit.Img(rt, "Rule", GPal.A(GPal.Brass, 0.75f), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 36), new Vector2(220, 37));
            var s = Goth.Text(rt, "S", sub ?? "", 21, GPal.Bone, TextAlignmentOptions.TopLeft, false, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(0, -66)); s.textWrappingMode = TextWrappingModes.NoWrap; s.overflowMode = TextOverflowModes.Ellipsis;
            return rt;
        }

        /// <summary>Key prompts at the bottom right. Spec: "key|what · key|what".</summary>
        protected RectTransform HintLine(string spec) => Goth.KeyBar(Root, spec);

        // ---- the taper: the round's clock (the court's hourglass relights it once per hourglass)
        Goth.CandleRow _candles; float _limit, _left; TextMeshProUGUI _glass; RectTransform _candleRt; int _lastTick = -1;
        protected void Candles(int n, float limit, Vector2 anchor, Vector2 offset)
        {
            _candleRt = UIKit.Rect(Root, "Clock", new Vector2(0.5f, 1), new Vector2(0.5f, 1)); _candleRt.sizeDelta = new Vector2(560, 64); _candleRt.anchoredPosition = new Vector2(0, -58);
            var bed = UIKit.Rect(_candleRt, "Bed", Vector2.zero, Vector2.one, new Vector2(-60, -30), new Vector2(60, 30)); var bi = bed.gameObject.AddComponent<RawImage>(); bi.texture = GTex.Glow; bi.color = new Color(0, 0, 0, 0.5f); bi.raycastTarget = false;
            var cap = Goth.Text(_candleRt, "Cap", "촛  불", 16, GPal.A(GPal.Brass, 0.9f), TextAlignmentOptions.Center, true, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -8), new Vector2(0, 14)); cap.characterSpacing = 6;
            _candles = new Goth.CandleRow(_candleRt, n, new Vector2(0, -8), 440);
            _candles.OnSegmentOut = left => { if (left > 0) TrialFx.Sound("candle_snuff", 0.55f); };
            _glass = Goth.Text(_candleRt, "Glass", "", 18, GPal.Gilt, TextAlignmentOptions.MidlineLeft, true, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-34, -22), new Vector2(150, 6));
            _limit = _left = limit; _lastTick = -1;
        }
        protected float TimeLeft => _left;
        protected float TimeFrac => _limit > 0 ? Mathf.Clamp01(_left / _limit) : 0;
        protected void SpendTime(float secs) { _left = Mathf.Max(0.01f, _left - secs); }
        /// <summary>Advance the taper; returns false once it is out (after any hourglass is spent). The last five seconds tick.</summary>
        protected bool TickCandles(float dt)
        {
            if (_candles == null) return true;
            _left -= dt;
            if (_left <= 0 && TrialGames.UseHourglass(Sim))
            {
                _left = _limit * 0.35f; _candles.Flare(); TrialFx.Sound("hourglass", 0.8f); TrialFx.Sound("candle_flare", 0.6f);
                H.StartCoroutine(GFx.Note(Root, null, "법정의 모래시계가 뒤집힌다 — 촛불이 다시 타오른다.", new Vector2(0.5f, 0.84f), GPal.Ink, 1.6f));
            }
            int sec = Mathf.CeilToInt(_left); if (_left > 0 && _left < 5.5f && sec != _lastTick) { _lastTick = sec; TrialFx.Sound("clock_tick", 0.6f); }
            _candles.Set(TimeFrac); _candles.Relax(dt);
            if (_glass != null) _glass.text = T.Hourglass > 0 ? "모래시계 " + T.Hourglass : "";
            return _left > 0;
        }
        protected void FlareCandles() { _candles?.Flare(); TrialFx.Sound("candle_flare", 0.4f); }

        /// <summary>Evidence cards along the left edge. Cards are dragged out (as a seal or a thread end) and dropped on the target.</summary>
        protected sealed class CardRail
        {
            readonly TrialMinigame _g; readonly RectTransform _rt, _tip; TextMeshProUGUI _tipText, _pageText; readonly List<TrialGames.Bullet> _items; int _page, _per;
            readonly Dictionary<string, RectTransform> _cards = new Dictionary<string, RectTransform>();
            public TrialGames.Bullet Hover, Dragging; public Vector2 DragPos; bool _down; Vector2 _downAt; TrialGames.Bullet _downCard;
            public Action<TrialGames.Bullet, Vector2> OnDrop; public Action<TrialGames.Bullet> OnDragStart; public Func<string, Color> Tint;
            public IReadOnlyList<TrialGames.Bullet> Items => _items;
            public CardRail(TrialMinigame g, List<TrialGames.Bullet> items, string label)
            {
                _g = g; _items = items;
                _rt = UIKit.Rect(g.Root, "Rail", new Vector2(0, 0), new Vector2(0, 1), new Vector2(30, 150), new Vector2(370, -150));
                Goth.Panel(_rt, "Bg", Vector2.zero, Vector2.one);
                Goth.Text(_rt, "L", label, 21, GPal.Gilt, TextAlignmentOptions.TopLeft, true, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -44), new Vector2(-12, -12)).characterSpacing = 3;
                UIKit.Img(_rt, "Rule", GPal.A(GPal.Brass, 0.5f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -50), new Vector2(-20, -49));
                _pageText = Goth.Text(_rt, "Pg", "", 17, GPal.Brass, TextAlignmentOptions.BottomRight, false, new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 10), new Vector2(-18, 36));
                _tip = UIKit.Rect(g.Root, "Tip", new Vector2(0, 0), new Vector2(0, 0)); _tip.pivot = new Vector2(0, 0); _tip.sizeDelta = new Vector2(600, 104); _tip.anchoredPosition = new Vector2(30, 34);
                var sh = UIKit.Rect(_tip, "Shade", Vector2.zero, Vector2.one, new Vector2(-14, -20), new Vector2(14, 8)); var si = sh.gameObject.AddComponent<RawImage>(); si.texture = GTex.Glow; si.color = new Color(0, 0, 0, 0.5f); si.raycastTarget = false;
                Goth.Parchment(_tip, "P", Vector2.zero, Vector2.one);
                _tipText = Goth.Text(_tip, "T", "", 18, GPal.Ink, TextAlignmentOptions.TopLeft, false, Vector2.zero, Vector2.one, new Vector2(16, 10), new Vector2(-14, -10)); _tipText.overflowMode = TextOverflowModes.Ellipsis;
                _tip.gameObject.SetActive(false);
                Build();
            }
            public void Rebuild() => Build();
            void Build()
            {
                foreach (var c in _cards.Values) if (c != null) UnityEngine.Object.Destroy(c.gameObject); _cards.Clear();
                float h = _rt.rect.height - 96; _per = Mathf.Max(3, Mathf.FloorToInt(h / 78f)); int pages = Mathf.Max(1, Mathf.CeilToInt(_items.Count / (float)_per)); _page = Mathf.Clamp(_page, 0, pages - 1);
                _pageText.text = pages > 1 ? $"휠로 넘기기   {_page + 1} / {pages}" : "";
                int i = 0;
                foreach (var b in _items.Skip(_page * _per).Take(_per))
                {
                    var card = UIKit.Rect(_rt, "Card_" + b.Id, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -60 - (i + 1) * 78 + 8), new Vector2(-14, -60 - i * 78));
                    var paper = Goth.Parchment(card, "P", Vector2.zero, Vector2.one); paper.color = Tint != null ? Tint(b.Id) : Color.white;
                    var pin = TrialFx.Centered(card, "Pin", new Vector2(32, 32)); pin.anchorMin = pin.anchorMax = new Vector2(0, 0.5f); pin.anchoredPosition = new Vector2(26, 0);
                    var sg = pin.gameObject.AddComponent<SealGraphic>(); sg.Wax = GPal.Wax; sg.Seed = b.Id.GetHashCode(); sg.raycastTarget = false;
                    var tag = Goth.Text(card, "G", b.Group, 14, GPal.A(GPal.InkRed, 0.9f), TextAlignmentOptions.TopLeft, true, Vector2.zero, Vector2.one, new Vector2(52, 5), new Vector2(-8, -5)); tag.characterSpacing = 2;
                    var tt = Goth.Text(card, "T", ShortTitle(b.Title), 18, GPal.Ink, TextAlignmentOptions.BottomLeft, true, Vector2.zero, Vector2.one, new Vector2(52, 7), new Vector2(-8, -20));
                    tt.overflowMode = TextOverflowModes.Ellipsis; tt.textWrappingMode = TextWrappingModes.Normal; tt.maxVisibleLines = 2;
                    _cards[b.Id] = card; i++;
                }
            }
            public static string ShortTitle(string s) { s = s ?? ""; return s.Length > 34 ? s.Substring(0, 33) + "…" : s; }
            public RectTransform CardOf(string id) => id != null && _cards.TryGetValue(id, out var c) ? c : null;
            public Vector2 PinOf(string id) { var c = CardOf(id); return c != null ? _g.ToRoot(c, new Vector2(c.rect.xMin + 26, c.rect.center.y)) : new Vector2(-_g.W * 0.4f, 0); }
            public void ShowPage(string id) { int idx = _items.FindIndex(b => b.Id == id); if (idx < 0) return; int p = idx / Mathf.Max(1, _per); if (p != _page) { _page = p; Build(); } }
            /// <summary>Poll the mouse. Returns true while something is being dragged.</summary>
            public bool Poll(Vector2 m, bool down, bool held, bool up, float wheel)
            {
                Hover = null;
                foreach (var kv in _cards) if (_g.Inside(kv.Value, m)) { Hover = _items.FirstOrDefault(x => x.Id == kv.Key); break; }
                if (Dragging == null) _g.HoverSfx(Hover, "rail");
                foreach (var kv in _cards) { var target = Hover != null && kv.Key == Hover.Id && Dragging == null ? new Vector3(1.03f, 1.03f, 1) : Vector3.one; kv.Value.localScale = Vector3.Lerp(kv.Value.localScale, target, Time.unscaledDeltaTime * 12); }
                if (Mathf.Abs(wheel) > 0.1f && _g.Inside(_rt, m)) { int pages = Mathf.Max(1, Mathf.CeilToInt(_items.Count / (float)_per)); _page = (_page + (wheel < 0 ? 1 : -1) + pages) % pages; Build(); TrialFx.Sound("parchment", 0.4f); }
                var show = Dragging ?? Hover;
                _tip.gameObject.SetActive(show != null); if (show != null) _tipText.text = $"<b>{show.Title}</b>\n<size=85%>{LineBank.FixParticles(show.Desc)}</size>";
                if (down && Hover != null) { _down = true; _downAt = m; _downCard = Hover; }
                if (_down && held && Dragging == null && _downCard != null && (m - _downAt).magnitude > 10) { Dragging = _downCard; OnDragStart?.Invoke(Dragging); TrialFx.Sound("parchment", 0.55f); }
                if (Dragging != null) DragPos = m;
                if (up)
                {
                    var d = Dragging; Dragging = null; _down = false; _downCard = null;
                    if (d != null) OnDrop?.Invoke(d, m);
                }
                return Dragging != null;
            }
        }

        /// <summary>Speaker reaction on the 3D actor (expression + gesture + babble); the camera moves only when the speaker changes.</summary>
        protected void Speak(string id, string text, Expr e = Expr.Neutral, Gesture g = Gesture.TalkEmphatic, int shot = 0, float dur = 3f)
        {
            if (id == null || id == Cast.Butler) return; H.CamCut(id, shot, dur);
            var v = Ses.World.ViewOf(id); if (v?.Rig == null) return;
            v.Rig.SetExpression(e); v.Rig.Anim?.PlayGesture(g, 1.4f); if (text != null) { VoiceBabble.Speak(id, text); v.Talk(Mathf.Clamp(text.Length * 0.06f, 0.5f, 3f)); }
        }

        protected IEnumerator Wait(float secs) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < secs) yield return null; }
        protected IEnumerator Later(string shot, float secs) { yield return new WaitForSecondsRealtime(secs); AutoProbe.Shot(shot); }

        /// <summary>After a lapse: the low organ, the eyes narrow, the speaker's comeback, then (maybe) someone steering the room.</summary>
        protected IEnumerator Lapse(TrialGames.ShotResult r, string fallbackWho)
        {
            TrialFx.Sound("organ_sting", 0.7f); H.ShakeCam(0.2f); H.EyesStare(0.7f);
            if (!string.IsNullOrEmpty(r.Retort)) { Speak(r.RetortBy ?? fallbackWho, r.Retort, Expr.Angry, Gesture.Point, 3, 2.4f); yield return GFx.Note(Root, r.RetortBy ?? fallbackWho, r.Retort, new Vector2(0.62f, 0.86f), GPal.Ink, 1.7f); }
            if (!string.IsNullOrEmpty(r.Steer)) { Speak(r.SteerBy, r.Steer, Expr.Smirk, Gesture.Point, 2, 2.4f); yield return GFx.Note(Root, r.SteerBy, r.Steer, new Vector2(0.62f, 0.86f), GPal.InkRed, 1.7f); }
        }

        // ------------------------------------------------------------ small illustration kit for the instruction cards
        protected static TextMeshProUGUI ArtText(RectTransform p, string text, float size, Color c, Vector2 aMin, Vector2 aMax, bool bold = false, TextAlignmentOptions al = TextAlignmentOptions.MidlineLeft)
        { var t = Goth.Text(p, "AT", text, size, c, al, bold, aMin, aMax); t.textWrappingMode = TextWrappingModes.NoWrap; return t; }
        protected static void ArtSeal(RectTransform p, Vector2 anchor, Color wax, string letter, float size)
        {
            var rt = TrialFx.Centered(p, "Seal", new Vector2(size, size)); rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = Vector2.zero;
            var s = rt.gameObject.AddComponent<SealGraphic>(); s.Wax = wax; s.Seed = letter.GetHashCode(); s.raycastTarget = false;
            Goth.Text(rt, "L", letter, size * 0.42f, Color.Lerp(wax, Color.black, 0.5f), TextAlignmentOptions.Center, true);
        }
        protected static void ArtThread(RectTransform p, Vector2 a, Vector2 b, Color c, float sag = 20f)
        {
            var rt = UIKit.Rect(p, "Thread", Vector2.zero, Vector2.one); var th = rt.gameObject.AddComponent<ThreadGraphic>(); th.raycastTarget = false; th.color = c; th.Width = 4; th.Sag = sag;
            var r = p.rect; th.Set(new Vector2(r.xMin + r.width * a.x, r.yMin + r.height * a.y), new Vector2(r.xMin + r.width * b.x, r.yMin + r.height * b.y));
        }
    }
}
