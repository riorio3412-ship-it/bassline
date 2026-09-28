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
    /// 촛불 심문 — the testimonies of the topic are read into a parchment ledger while the candles burn. Drag an evidence card's
    /// wax seal onto the exact phrase it breaks (crimson wax) or vouches for (black wax); the kernel judges it (TrialGames.Seal →
    /// Logic). A witness can be pressed once (TrialGames.Press). A wrong seal cracks, costs the jury's trust, and gives the room's
    /// loudest voice an opening.
    /// </summary>
    public sealed class CandleInquiry : TrialMinigame
    {
        sealed class Entry { public int Index; public GameLine L; public RectTransform Rt; public TextMeshProUGUI Text, Note; public RectTransform PressBtn; public readonly List<Rect> Phrase = new List<Rect>(); public bool Lit; }

        readonly List<Entry> _entries = new List<Entry>(); RectTransform _ledger, _body, _dragSeal, _waxRed, _waxBlack; CardRail _rail;
        bool _black; IEnumerator _end; float _camNext; Entry _hoverEntry; TextMeshProUGUI _waxLabel;

        public CandleInquiry(TrialDirectorUI h) : base(h) { }

        public override IEnumerator Run()
        {
            MusicDirector.I?.SetState(MusicState.TrialPressure);
            H.CamWide(44);
            yield return Intro("촛불 심문", "논점 — " + G.Subtitle, "앞뒤가 안 맞는 한 줄을 찾아, 그 말을 깨뜨릴 증거를 찍어라");
            yield return Teach("촛불 심문", new[]
            {
                "증언이 기록에 한 줄씩 적힌다. 밑줄 친 부분이 수상한 대목이다.",
                "왼쪽 증거 카드를 끌어다 그 부분 위에 놓는다. 붉은 밀랍은 반박, 검은 밀랍은 뒷받침.",
                "이름 옆 '추궁'을 눌러 캐물으면 새로운 말이 나온다. 촛불이 다 타기 전에.",
            }, ArtInquiry);
            Header("촛불 심문", "논점 — " + G.Subtitle);
            Candles(5, G.TimeLimit, new Vector2(0.5f, 1), new Vector2(40, -130));
            BuildLedger();
            var byId = TrialGames.Arsenal(Sim).GroupBy(b => b.Id).ToDictionary(g => g.Key, g => g.First());
            var order = G.Bullets.Where(byId.ContainsKey).Concat(byId.Keys.Where(k => !G.Bullets.Contains(k))).Select(k => byId[k]).ToList();
            _rail = new CardRail(this, order, "증거 — 끌어다 문장에 찍는다");
            _rail.OnDragStart = b => { _dragSeal = MakeSeal(); };
            _rail.OnDrop = (b, m) => Drop(b, m);
            BuildWax();
            HintLine("드래그|증거를 문장에 찍기 · 1 / 2|밀랍 바꾸기 · 클릭|이름 옆 ‘추궁’으로 캐묻기 · Esc|심문 끝내기");
            yield return ReadIn();
            if (Probe) H.StartCoroutine(ProbeDemo());
            while (_end == null) { Tick(); EndVirtFrame(); yield return null; }
            if (_dragSeal != null) Object.Destroy(_dragSeal.gameObject);
            yield return _end;
            Done = true;
        }

        // ------------------------------------------------------------ ledger
        void BuildLedger()
        {
            _ledger = UIKit.Rect(Root, "Ledger", new Vector2(0, 0), new Vector2(1, 1), new Vector2(400, 184), new Vector2(-44, -150));
            _ledger.localRotation = Quaternion.Euler(0, 0, -0.6f);
            UIKit.Img(_ledger, "Shade", GPal.A(Color.black, 0.5f), Vector2.zero, Vector2.one, new Vector2(10, -14), new Vector2(10, -14));
            Goth.Parchment(_ledger, "Paper", Vector2.zero, Vector2.one);
            var fr = Goth.Frame(_ledger, "Rule", Vector2.zero, Vector2.one, new Vector2(14, 14), new Vector2(-14, -14), false, false); fr.Border = GPal.A(GPal.InkRed, 0.55f); fr.Width = 1.5f; fr.Corners = false;
            Goth.Text(_ledger, "Title", "심문 기록 — " + G.Subtitle, 28, GPal.A(GPal.Ink, 0.9f), TextAlignmentOptions.Top, true, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -58), new Vector2(-40, -18)).characterSpacing = 4;
            UIKit.Img(_ledger, "TitleRule", GPal.A(GPal.Ink, 0.5f), new Vector2(0.08f, 1), new Vector2(0.92f, 1), new Vector2(0, -64), new Vector2(0, -62));
            _body = UIKit.Rect(_ledger, "Body", Vector2.zero, Vector2.one, new Vector2(46, 26), new Vector2(-40, -74));
            float y = 0; float width = _body.rect.width - 120;
            for (int i = 0; i < G.Lines.Count; i++)
            {
                var L = G.Lines[i]; var e = new Entry { Index = i, L = L };
                string text = Markup(L);
                var probe = Goth.Text(_body, "Probe", text, 29, GPal.Ink, TextAlignmentOptions.TopLeft, false); float th = probe.GetPreferredValues(text, width, 0).y; Object.Destroy(probe.gameObject);
                float h = Mathf.Max(64, th + 36);
                e.Rt = UIKit.Rect(_body, "E" + i, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -(y + h)), new Vector2(0, -y)); y += h + 10;
                var who = Goth.Text(e.Rt, "Who", Cast.NameOf(L.Speaker) ?? "", 19, GPal.InkRed, TextAlignmentOptions.TopLeft, true, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-120, 0)); who.characterSpacing = 3;
                e.Text = Goth.Text(e.Rt, "T", text, 29, GPal.Ink, TextAlignmentOptions.TopLeft, false, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-120, -28));
                e.Text.lineSpacing = 4;
                if (L.ClaimId != null && L.Speaker != null && L.Speaker != Cast.Player)
                {
                    e.PressBtn = UIKit.Rect(e.Rt, "Press", new Vector2(1, 1), new Vector2(1, 1)); e.PressBtn.pivot = new Vector2(1, 1); e.PressBtn.sizeDelta = new Vector2(100, 34); e.PressBtn.anchoredPosition = new Vector2(0, -2);
                    var pf = Goth.Frame(e.PressBtn, "F", Vector2.zero, Vector2.one); pf.Fill = GPal.A(new Color(0.08f, 0.055f, 0.035f), 1f); pf.FillBottom = GPal.A(GPal.Lacquer, 1f); pf.Border = GPal.A(GPal.Brass, 0.9f); pf.Corners = false; pf.Gap = 0;
                    Goth.Text(e.PressBtn, "T", L.Pressed ? "추궁함" : "추궁", 18, GPal.Gilt, TextAlignmentOptions.Center, true);
                }
                e.Rt.gameObject.SetActive(false);
                _entries.Add(e);
            }
        }

        static string Markup(GameLine L)
        {
            var t = L.Text ?? "…";
            if (L.ClaimId == null || L.WeakAt < 0 || L.WeakAt + L.WeakLen > t.Length) return t;
            return t.Substring(0, L.WeakAt) + "<u>" + t.Substring(L.WeakAt, L.WeakLen) + "</u>" + t.Substring(L.WeakAt + L.WeakLen);
        }

        void MeasurePhrase(Entry e)
        {
            e.Phrase.Clear(); if (e.L.ClaimId == null || e.L.WeakAt < 0) return;
            e.Text.ForceMeshUpdate(true, true); var ti = e.Text.textInfo; var lines = new Dictionary<int, Rect>();
            for (int c = e.L.WeakAt; c < e.L.WeakAt + e.L.WeakLen && c < ti.characterCount; c++)
            {
                var ci = ti.characterInfo[c]; if (!ci.isVisible) continue; var r = Rect.MinMaxRect(ci.bottomLeft.x, ci.descender, ci.topRight.x, ci.ascender);
                lines[ci.lineNumber] = lines.TryGetValue(ci.lineNumber, out var o) ? Rect.MinMaxRect(Mathf.Min(o.xMin, r.xMin), Mathf.Min(o.yMin, r.yMin), Mathf.Max(o.xMax, r.xMax), Mathf.Max(o.yMax, r.yMax)) : r;
            }
            foreach (var r in lines.Values) e.Phrase.Add(Rect.MinMaxRect(r.xMin - 10, r.yMin - 10, r.xMax + 10, r.yMax + 10));
        }

        bool OnPhrase(Entry e, Vector2 m) { if (e.Phrase.Count == 0 || !e.Rt.gameObject.activeSelf) return false; var lp = ToLocal(e.Text.rectTransform, m); return e.Phrase.Any(r => r.Contains(lp)); }
        Vector2 PhraseCenter(Entry e) => e.Phrase.Count > 0 ? ToRoot(e.Text.rectTransform, e.Phrase[0].center) : CenterOf(e.Rt);

        void Tint(Entry e, Color c)
        {
            if (e.L.WeakAt < 0) return; var ti = e.Text.textInfo; if (ti == null) return; bool any = false;
            for (int k = e.L.WeakAt; k < e.L.WeakAt + e.L.WeakLen && k < ti.characterCount; k++)
            {
                var ci = ti.characterInfo[k]; if (!ci.isVisible) continue; var cols = ti.meshInfo[ci.materialReferenceIndex].colors32; int vi = ci.vertexIndex; if (cols == null || vi + 3 >= cols.Length) continue;
                cols[vi] = cols[vi + 1] = cols[vi + 2] = cols[vi + 3] = c; any = true;
            }
            if (any) e.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        /// <summary>The testimonies are read into the record, one voice at a time.</summary>
        IEnumerator ReadIn()
        {
            foreach (var e in _entries)
            {
                e.Rt.gameObject.SetActive(true); MeasurePhrase(e);
                Speak(e.L.Speaker, e.L.Text, e.L.Emo == Emotion.Neutral ? Expr.Neutral : ActorView.MapEmo(e.L.Emo), Gesture.TalkEmphatic, 0, 2.6f);
                TrialFx.Sound("quill", 0.5f);
                var cg = e.Rt.gameObject.AddComponent<CanvasGroup>(); float t0 = Time.unscaledTime; float dur = TrialDirectorUI.ProbeFast ? 0.35f : Mathf.Clamp(0.5f + (e.L.Text?.Length ?? 10) * 0.025f, 0.8f, 1.8f);
                e.Text.maxVisibleCharacters = 0; int n = e.L.Text?.Length ?? 0;
                while (Time.unscaledTime - t0 < dur) { float k = GFx.K(t0, dur); cg.alpha = Mathf.Clamp01(k * 3); e.Text.maxVisibleCharacters = (int)(n * Mathf.Clamp01(k * 1.4f)) + 12; if (Time.unscaledTime - t0 > 0.2f && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))) break; yield return null; }
                cg.alpha = 1; e.Text.maxVisibleCharacters = 99999; MeasurePhrase(e);
            }
            _camNext = Time.unscaledTime + 3f;
        }

        // ------------------------------------------------------------ wax
        void BuildWax()
        {
            var box = UIKit.Rect(Root, "Wax", new Vector2(1, 0), new Vector2(1, 0)); box.pivot = new Vector2(1, 0); box.sizeDelta = new Vector2(430, 130); box.anchoredPosition = new Vector2(-40, 104);   // above the key-hint strip
            _waxRed = TrialFx.Centered(box, "Red", new Vector2(92, 92), new Vector2(-120, 12)); var r = _waxRed.gameObject.AddComponent<SealGraphic>(); r.Wax = GPal.Wax; r.Seed = 11; r.raycastTarget = false;
            Goth.Text(_waxRed, "L", "반", 38, Color.Lerp(GPal.Wax, Color.black, 0.5f), TextAlignmentOptions.Center, true);
            _waxBlack = TrialFx.Centered(box, "Black", new Vector2(92, 92), new Vector2(0, 12)); var b = _waxBlack.gameObject.AddComponent<SealGraphic>(); b.Wax = GPal.BlackWax; b.Seed = 12; b.raycastTarget = false;
            Goth.Text(_waxBlack, "L", "보", 38, GPal.Brass, TextAlignmentOptions.Center, true);
            _waxLabel = Goth.Text(box, "Label", "", 20, GPal.Bone, TextAlignmentOptions.Center, true, new Vector2(0, 0), new Vector2(1, 0), new Vector2(-20, -30), new Vector2(0, 4));
            PaintWax();
        }
        void PaintWax()
        {
            _waxRed.localScale = Vector3.one * (_black ? 0.78f : 1.08f); _waxBlack.localScale = Vector3.one * (_black ? 1.08f : 0.78f);
            _waxRed.GetComponent<SealGraphic>().color = new Color(1, 1, 1, _black ? 0.55f : 1f); _waxBlack.GetComponent<SealGraphic>().color = new Color(1, 1, 1, _black ? 1f : 0.55f);
            _waxLabel.text = _black ? "검은 밀랍 — 이 말을 <b>뒷받침</b>한다" : "붉은 밀랍 — 이 말을 <b>반박</b>한다";
        }
        RectTransform MakeSeal() { var s = GFx.Seal(Root, MouseLocal(), _black ? GPal.BlackWax : GPal.Wax, _black ? "보" : "반", 76); s.color = new Color(1, 1, 1, 0.85f); return s.rectTransform; }

        // ------------------------------------------------------------ frame
        void Tick()
        {
            float dt = Dt; var m = MouseLocal();
            if (!TickCandles(dt) && _end == null) { _end = Burnout(); return; }
            if (!UseVirt)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) { _black = false; PaintWax(); TrialFx.Sound("trial_lock", 0.5f); }
                if (Input.GetKeyDown(KeyCode.Alpha2)) { _black = true; PaintWax(); TrialFx.Sound("trial_lock", 0.5f); }
                if (Input.GetKeyDown(KeyCode.Tab)) { _black = !_black; PaintWax(); TrialFx.Sound("trial_lock", 0.5f); }
                if (Input.GetKeyDown(KeyCode.Escape)) { _end = Burnout(true); return; }
                if (MouseDown && Inside(_waxRed, m)) { _black = false; PaintWax(); TrialFx.Sound("trial_lock", 0.5f); }
                if (MouseDown && Inside(_waxBlack, m)) { _black = true; PaintWax(); TrialFx.Sound("trial_lock", 0.5f); }
            }
            bool dragging = _rail.Poll(m, MouseDown, MouseHeld, MouseUp, UseVirt ? 0 : Input.mouseScrollDelta.y);
            if (_dragSeal != null) { if (_rail.Dragging == null) { Object.Destroy(_dragSeal.gameObject); _dragSeal = null; } else { _dragSeal.anchoredPosition = m + new Vector2(0, 10); _dragSeal.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 6) * 6); } }
            // hover: the phrase under a carried seal glows; the camera looks at whoever's line the cursor rests on
            Entry over = null, phrase = null; RectTransform press = null;
            foreach (var e in _entries)
            {
                bool onP = dragging && e.L.Result != "hit" && e.L.Result != "backed" && OnPhrase(e, m); if (onP) phrase = e;
                Tint(e, onP ? (Color32)(_black ? GPal.Sapphire : GPal.InkRed) : e.L.Result == "hit" ? (Color32)GPal.InkRed : (Color32)GPal.Ink);
                if (Inside(e.Rt, m)) over = e;
                bool onPress = e.PressBtn != null && !e.L.Pressed && !dragging && Inside(e.PressBtn, m);
                if (e.PressBtn != null) e.PressBtn.localScale = Vector3.Lerp(e.PressBtn.localScale, Vector3.one * (onPress ? 1.06f : 1f), Dt * 12);
                if (onPress) { press = e.PressBtn; if (MouseDown) H.StartCoroutine(PressCo(e)); }
            }
            HoverSfx(phrase, "phrase", "plate_hover", 0.45f); HoverSfx(press, "press");
            HoverSfx(!dragging && Inside(_waxRed, m) ? _waxRed : !dragging && Inside(_waxBlack, m) ? _waxBlack : null, "wax");
            // the camera turns to a witness only when the eye settles on their line (and never faster than every few seconds)
            if (over != null && over != _hoverEntry && over.L.Speaker != null && Time.unscaledTime > _camNext) { _hoverEntry = over; _camNext = Time.unscaledTime + 4f; Speak(over.L.Speaker, null, Expr.Neutral, Gesture.Listen, 0, 5f); }
        }

        static void ArtInquiry(RectTransform p)
        {
            ArtText(p, "김진우", 16, GPal.InkRed, new Vector2(0.08f, 0.78f), new Vector2(0.9f, 0.88f), true);
            ArtText(p, "그때 나는 <u>서재</u>에 있었어.", 26, GPal.Ink, new Vector2(0.08f, 0.64f), new Vector2(0.95f, 0.78f));
            UIKit.Img(p, "Strike", GPal.A(GPal.InkRed, 0.85f), new Vector2(0.39f, 0.705f), new Vector2(0.55f, 0.705f), new Vector2(0, -2), new Vector2(0, 2));
            var card = UIKit.Rect(p, "Card", new Vector2(0.08f, 0.12f), new Vector2(0.55f, 0.34f)); Goth.Parchment(card, "P", Vector2.zero, Vector2.one);
            UIKit.Img(card, "Edge", GPal.A(GPal.Ink, 0.3f), Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 2));
            ArtText(card, "출입 기록 — 음악실", 16, GPal.Ink, new Vector2(0.3f, 0.1f), new Vector2(0.98f, 0.9f), true);
            ArtSeal(card, new Vector2(0.15f, 0.5f), GPal.Wax, "반", 34);
            ArtThread(p, new Vector2(0.4f, 0.36f), new Vector2(0.47f, 0.6f), GPal.A(GPal.InkRed, 0.6f), -30f);
            ArtSeal(p, new Vector2(0.47f, 0.7f), GPal.Wax, "반", 58);
            ArtSeal(p, new Vector2(0.8f, 0.22f), GPal.BlackWax, "보", 50);
            ArtText(p, "검은 밀랍 = 뒷받침", 15, GPal.Ink, new Vector2(0.62f, 0.02f), new Vector2(0.99f, 0.11f), true);
        }

        IEnumerator PressCo(Entry e)
        {
            e.L.Pressed = true; SpendTime(G.TimeLimit * 0.1f); TrialFx.Sound("wax_stamp", 0.5f); TrialFx.Sound("quill", 0.6f);
            if (e.PressBtn != null) e.PressBtn.GetComponentInChildren<TextMeshProUGUI>().text = "추궁함";
            var said = TrialGames.Press(Sim, e.Index);
            Speak(Cast.Player, null, Expr.Neutral, Gesture.Point, 1, 2f);
            foreach (var (who, text) in said)
            {
                if (who != null && who != Cast.Player) Speak(who, text, Expr.Surprised, Gesture.Think, 0, 2.4f);
                if (who != Cast.Player) yield return GFx.Note(Root, who, text, new Vector2(0.64f, 0.84f), GPal.Ink, 1.6f);
            }
            if (e.L.Result == "shaken") { var t = Goth.Text(e.Rt, "Shaken", "— 흔들림", 20, GPal.InkRed, TextAlignmentOptions.BottomRight, true, Vector2.zero, Vector2.one, new Vector2(0, -4), new Vector2(-126, 0)); t.fontStyle = FontStyles.Italic; }
        }

        void Drop(TrialGames.Bullet b, Vector2 m)
        {
            var e = _entries.FirstOrDefault(x => x.L.Result != "hit" && x.L.Result != "backed" && OnPhrase(x, m));
            if (_dragSeal != null) { Object.Destroy(_dragSeal.gameObject); _dragSeal = null; }
            if (e == null) { TrialFx.Sound("ui_cancel", 0.5f); if (_entries.Any(x => Inside(x.Rt, m))) H.StartCoroutine(GFx.Note(Root, null, "밀랍은 밑줄 친 부분에 찍어야 한다.", new Vector2(0.64f, 0.12f), GPal.Ink, 1.2f)); return; }
            H.StartCoroutine(Resolve(e, b, _black));
        }

        bool _busy;
        IEnumerator Resolve(Entry e, TrialGames.Bullet b, bool black)
        {
            if (_busy) yield break; _busy = true;
            var at = PhraseCenter(e);
            var seal = GFx.Seal(Root, at, black ? GPal.BlackWax : GPal.Wax, black ? "보" : "반", 88);
            yield return GFx.Slam(seal);
            var r = TrialGames.Seal(Sim, e.Index, b.Id, black);
            if (r.Valid)
            {
                FlareCandles();
                if (r.Hourglass) { TrialFx.Sound("hourglass", 0.8f); H.StartCoroutine(GFx.Note(Root, null, "연달아 맞혔다 — 법정의 모래시계가 저절로 뒤집힌다.", new Vector2(0.5f, 0.9f), GPal.Ink, 1.6f)); }
                H.EyesStare(1f);
                if (!black) { Strike(e); Speak(e.L.Speaker, null, Expr.Fear, Gesture.Flinch, 4, 3f); }
                else Speak(e.L.Speaker, null, Expr.Smile, Gesture.Nod, 0, 3f);
                if (r.Ended) { if (Probe) H.StartCoroutine(Later("trial_inquiry_seal", 0.6f)); _end = Win(r, e); }
                else yield return GFx.Note(Root, Cast.Player, "뒷받침 — " + r.Text, new Vector2(0.64f, 0.84f), GPal.Ink, 1.6f);
                _busy = false; yield break;
            }
            if (Probe) H.StartCoroutine(Later("trial_inquiry_crack", 0.45f));
            H.StartCoroutine(GFx.CrackAndFall(seal));
            if (r.Ended) { _end = Lost(); _busy = false; yield break; }
            yield return Lapse(r, e.L.Speaker);
            _busy = false;
        }

        void Strike(Entry e)
        {
            foreach (var p in e.Phrase)
            {
                var line = UIKit.Img(e.Text.rectTransform, "Strike", GPal.A(GPal.InkRed, 0.9f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                line.rectTransform.pivot = new Vector2(0, 0.5f); line.rectTransform.sizeDelta = new Vector2(p.width - 16, 5); line.rectTransform.anchoredPosition = new Vector2(p.xMin + 8 - e.Text.rectTransform.rect.center.x, p.center.y - e.Text.rectTransform.rect.center.y);
                line.rectTransform.localRotation = Quaternion.Euler(0, 0, Random.Range(-3f, 3f));
                H.StartCoroutine(Grow(line.rectTransform));
            }
        }
        static IEnumerator Grow(RectTransform rt) { float t0 = Time.unscaledTime; while (rt != null && Time.unscaledTime - t0 < 0.25f) { rt.localScale = new Vector3(GFx.EaseOut(GFx.K(t0, 0.25f)), 1, 1); yield return null; } if (rt != null) rt.localScale = Vector3.one; }

        IEnumerator Win(TrialGames.ShotResult r, Entry e)
        {
            TrialFx.Sound("choir_swell", 0.8f);
            yield return Wait(0.4f);
            yield return GFx.Plate(H.FxRoot, r.Seal ?? "반증", r.Text, r.R == LogicResult.Support ? GPal.Sapphire : GPal.Oxblood, 1.6f, "bell_toll");
        }

        IEnumerator Lost() { yield return GFx.Plate(H.FxRoot, "신뢰를 잃었다", "배심원들은 이제 당신이 찍는 밀랍을 믿지 않는다", GPal.Oxblood, 1.4f, "organ_sting"); }

        IEnumerator Burnout(bool quit = false)
        {
            TrialGames.Timeout(Sim);

            bool quiet = G.Why == "quiet";
            yield return GFx.Plate(H.FxRoot, quit ? "심문 종료" : "촛불이 꺼졌다", quiet ? "심문을 마쳤다 — 무너뜨릴 만한 말은 나오지 않았다" : "누구의 증언도 무너지지 않았다", GPal.Smoke, 1.1f);
        }

        // ------------------------------------------------------------ probe demo
        IEnumerator ProbeDemo()
        {
            UseVirt = true; Virt = new Vector2(0, -Ht * 0.3f); yield return Wait(0.8f);
            AutoProbe.Shot("trial_inquiry_ledger");
            var pressable = _entries.FirstOrDefault(e => e.PressBtn != null);
            if (pressable != null) { yield return VirtClick(CenterOf(pressable.PressBtn)); yield return Wait(0.9f); AutoProbe.Shot("trial_inquiry_press"); yield return Wait(1.4f); }
            // one wrong seal, then the right one
            var target = _entries.FirstOrDefault(e => e.L.ClaimId != null && _rail.Items.Any(b => TrialGames.ProbeWorks(S, e.L.ClaimId, false, b.Id)));
            var wrongOn = target ?? _entries.FirstOrDefault(e => e.L.ClaimId != null);
            if (wrongOn != null)
            {
                var wrong = _rail.Items.FirstOrDefault(b => !TrialGames.ProbeWorks(S, wrongOn.L.ClaimId, false, b.Id) && !TrialGames.ProbeWorks(S, wrongOn.L.ClaimId, true, b.Id));
                if (wrong != null) { _rail.ShowPage(wrong.Id); yield return null; yield return VirtDrag(_rail.PinOf(wrong.Id), () => PhraseCenter(wrongOn), 0.6f); AutoProbe.Shot("trial_inquiry_drag"); yield return Wait(3.8f); }
            }
            if (_end == null && target != null)
            {
                var ok = _rail.Items.First(b => TrialGames.ProbeWorks(S, target.L.ClaimId, false, b.Id));
                _rail.ShowPage(ok.Id); yield return null; yield return VirtDrag(_rail.PinOf(ok.Id), () => PhraseCenter(target), 0.7f);
            }
            else if (_end == null)
            {
                var def = _entries.FirstOrDefault(e => e.L.Kind == "defense" && _rail.Items.Any(b => TrialGames.ProbeWorks(S, e.L.ClaimId, true, b.Id)));
                if (def != null) { _black = true; PaintWax(); var ok = _rail.Items.First(b => TrialGames.ProbeWorks(S, def.L.ClaimId, true, b.Id)); _rail.ShowPage(ok.Id); yield return null; yield return VirtDrag(_rail.PinOf(ok.Id), () => PhraseCenter(def), 0.7f); }
            }
            yield return Wait(2.5f);
            if (_end == null) SpendTime(9999);
        }
    }
}
