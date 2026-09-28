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
    /// Exploration HUD: time and place, the case lines (남은 수사 · 개요 n/4 · 다음 — …), the compass line, what you look at,
    /// toasts (one per find), overheard speech, the clue card, look captions and the first-day key strip.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public static Hud I;
        Session _s; GameState S => _s.S; Canvas _c;
        TextMeshProUGUI _clock, _place, _target, _hint, _timer, _center; SlantPanel _clockPanel; SlantPanel _timerPanel; Image _cross, _ring, _aim, _hover;
        RectTransform _toasts, _subs; readonly List<(GameObject go, float until)> _live = new List<(GameObject, float)>();
        RectTransform _card; float _cardUntil; SlantPanel _cardPanel; TextMeshProUGUI _cardKicker, _cardTitle, _cardBody, _cardMeta, _cardTab;
        RectTransform _banner; float _bannerUntil; TextMeshProUGUI _bannerText;
        SlantPanel _lookPanel; TextMeshProUGUI _look; float _lookUntil;
        SlantPanel _keysPanel; TextMeshProUGUI _keys; CanvasGroup _keysCg;
        SlantPanel _savePanel; TextMeshProUGUI _saveText; float _saveUntil;
        RectTransform _marksRoot; readonly List<TextMeshProUGUI> _marks = new List<TextMeshProUGUI>();
        string _approachFrom; float _approachUntil;
        // as wide as the clock plate above it: the case lines wrap under it instead of running beneath Yusti's broadcast
        // (which starts at x≈500 on a 1920 screen)
        const float InfoW = 436f;

        public void Init(Session s)
        {
            I = this; _s = s; _c = UIKit.Root("HUD", 10); var t = _c.transform;
            _clockPanel = UIKit.Slant(t, "ClockPanel", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -122), new Vector2(460, -24), Pal.A(Pal.Ink, 0.97f), Pal.A(Pal.Panel, 0.9f), Pal.A(Pal.Gold, 0.85f), 0);   // near-opaque: linear colour turns 0.7 into a see-through smear over bright walls
            _clockPanel.EdgeLeftOnly = true;
            _clock = UIKit.Text(_clockPanel.transform, "Clock", "", 32, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Title, Vector2.zero, Vector2.one, new Vector2(26, 8), new Vector2(-10, -8));
            _clock.textWrappingMode = TextWrappingModes.NoWrap; _clock.enableAutoSizing = true; _clock.fontSizeMin = 20; _clock.fontSizeMax = 32; _clock.overflowMode = TextOverflowModes.Ellipsis;
            _clock.rectTransform.offsetMin = new Vector2(26, 44);
            _place = UIKit.Text(_clockPanel.transform, "Place", "", 21, Pal.A(Pal.Gold, 0.95f), TextAlignmentOptions.BottomLeft, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(28, 10), new Vector2(-10, -10));
            // one line: a long room name ("1층 남쪽 복도 서쪽 끝") shrinks instead of wrapping up over the clock line
            _place.textWrappingMode = TextWrappingModes.NoWrap; _place.enableAutoSizing = true; _place.fontSizeMin = 14; _place.fontSizeMax = 21; _place.overflowMode = TextOverflowModes.Ellipsis;
            // the case / rules / appointment / compass lines: one ink plate that grows with its lines (reads over bright walls and candles)
            _timerPanel = UIKit.Slant(t, "InfoPanel", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -184), new Vector2(24 + InfoW, -128), Pal.A(Pal.Ink, 0.93f), Pal.A(Pal.Panel, 0.8f), Pal.A(Pal.Gold, 0.7f), 0); _timerPanel.EdgeLeftOnly = true; _timerPanel.gameObject.SetActive(false);
            _timer = UIKit.Text(_timerPanel.transform, "Info", "", 23, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(20, 8), new Vector2(-14, -8));
            _timer.lineSpacing = 4;
            _cross = UIKit.Img(t, "Cross", Pal.A(Pal.Text, 0.75f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-3, -3), new Vector2(3, 3));
            _hover = UIKit.Img(t, "Hover", Pal.A(Pal.Gold, 0.65f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-13, -13), new Vector2(13, 13)); _hover.sprite = RingSprite(); _hover.enabled = false;
            _ring = UIKit.Img(t, "Ring", Pal.A(Pal.Gold, 0.9f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-28, -28), new Vector2(28, 28));
            _ring.type = Image.Type.Filled; _ring.fillMethod = Image.FillMethod.Radial360; _ring.fillAmount = 0; _ring.sprite = RingSprite();
            _aim = UIKit.Img(t, "Aim", Pal.A(Pal.Blood, 0.8f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-18, -18), new Vector2(18, 18)); _aim.sprite = RingSprite(); _aim.enabled = false;
            _target = UIKit.Text(t, "Target", "", 26, Pal.Text, TextAlignmentOptions.Top, Fonts.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-500, -80), new Vector2(500, -30));
            _hint = UIKit.Text(t, "Hint", "", 20, Pal.A(Pal.Text, 0.85f), TextAlignmentOptions.Top, Fonts.Body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-600, -118), new Vector2(600, -80));
            _center = UIKit.Text(t, "Center", "", 30, Pal.Gold, TextAlignmentOptions.Center, Fonts.Serif, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-700, 140), new Vector2(700, 260));
            // a look that found nothing: a short caption under the crosshair
            _lookPanel = UIKit.Slant(t, "LookPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -176), new Vector2(300, -128), Pal.A(Pal.Ink, 0.9f), Pal.A(Pal.Panel, 0.85f), Pal.A(Pal.Gold, 0.55f), 0);
            _lookPanel.EdgeLeftOnly = true;
            _look = UIKit.Text(_lookPanel.transform, "T", "", 21, Pal.A(Pal.Text, 0.95f), TextAlignmentOptions.Center, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(18, 2), new Vector2(-14, -2));
            _look.textWrappingMode = TextWrappingModes.NoWrap; _look.overflowMode = TextOverflowModes.Ellipsis;
            _lookPanel.gameObject.SetActive(false);
            _toasts = UIKit.Rect(t, "Toasts", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-560, -780), new Vector2(-24, -236));   // below the broadcast panel
            _subs = UIKit.Rect(t, "Overheard", new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 44), new Vector2(1100, 364));
            _marksRoot = UIKit.Full(t, "TraceMarks"); _marksRoot.SetAsFirstSibling();
            BuildCard(t); BuildBanner(t); BuildKeys(t); BuildSaveMark(t);
        }

        public void Destroy() { if (_c != null) Object.Destroy(_c.gameObject); if (_sysC != null) Object.Destroy(_sysC.gameObject); if (I == this) I = null; }

        static Sprite _ringSprite;
        static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            int n = 128; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float dx = x - n / 2f + 0.5f, dy = y - n / 2f + 0.5f; float r = Mathf.Sqrt(dx * dx + dy * dy); float a = Mathf.Clamp01(1 - Mathf.Abs(r - n * 0.42f) / 4f); px[x + y * n] = new Color32(255, 255, 255, (byte)(a * 255)); }
            tex.SetPixels32(px); tex.Apply(); _ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f)); return _ringSprite;
        }

        void BuildCard(Transform t)
        {
            // left of centre, below the case lines: never under the toast column on the right
            _cardPanel = UIKit.Slant(t, "ClueCard", new Vector2(0, 0.42f), new Vector2(0, 0.42f), new Vector2(30, -150), new Vector2(640, 150), Pal.A(Pal.Panel2, 0.98f), Pal.A(Pal.Ink, 0.98f), Pal.A(Pal.Gold, 0.8f), 0);
            _cardPanel.Glow = 0.2f; _card = _cardPanel.rectTransform;
            _cardKicker = UIKit.Text(_cardPanel.transform, "Kicker", "단서", 18, Pal.A(Pal.Gold, 0.9f), TextAlignmentOptions.TopLeft, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(36, 14), new Vector2(-24, -14));
            _cardKicker.characterSpacing = 4;
            _cardTitle = UIKit.Text(_cardPanel.transform, "Title", "", 30, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Title, Vector2.zero, Vector2.one, new Vector2(36, 14), new Vector2(-24, -42));
            _cardTitle.textWrappingMode = TextWrappingModes.NoWrap; _cardTitle.overflowMode = TextOverflowModes.Ellipsis;
            _cardBody = UIKit.Text(_cardPanel.transform, "Body", "", 21, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(36, 50), new Vector2(-28, -92));
            _cardBody.lineSpacing = 6; _cardBody.overflowMode = TextOverflowModes.Ellipsis;
            _cardMeta = UIKit.Text(_cardPanel.transform, "Meta", "", 17, Pal.TextDim, TextAlignmentOptions.BottomLeft, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(36, 16), new Vector2(-150, -10));
            _cardTab = UIKit.Text(_cardPanel.transform, "Tab", "Tab 수첩", 17, Pal.A(Pal.Gold, 0.85f), TextAlignmentOptions.BottomRight, Fonts.Bold, Vector2.zero, Vector2.one, new Vector2(30, 16), new Vector2(-24, -10));
            _card.gameObject.SetActive(false);
        }

        void BuildBanner(Transform t)
        {
            var p = UIKit.Slant(t, "Banner", new Vector2(0, 0.62f), new Vector2(1, 0.62f), new Vector2(-40, -70), new Vector2(40, 70), Pal.A(Pal.Blood, 0.92f), Pal.A(Pal.Ink, 0.95f), Pal.Magenta, 60);
            p.Glow = 0.6f; _banner = p.rectTransform;
            _bannerText = UIKit.Text(p.transform, "T", "", 64, Color.white, TextAlignmentOptions.Center, Fonts.Title);
            _banner.gameObject.SetActive(false);
        }

        void BuildKeys(Transform t)
        {
            _keysPanel = UIKit.Slant(t, "KeyStrip", new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 8), new Vector2(760, 40), Pal.A(Pal.Ink, 0.85f), Pal.A(Pal.Panel, 0.75f), Pal.A(Pal.Gold, 0.6f), 0);
            _keysPanel.EdgeLeftOnly = true; _keysCg = _keysPanel.gameObject.AddComponent<CanvasGroup>(); _keysCg.alpha = 0;
            const string k = "<color=#D6AD62>";
            _keys = UIKit.Text(_keysPanel.transform, "T", $"{k}Tab</color> 수첩  ·  {k}E</color> 말 걸기·사용  ·  {k}R</color> 살펴보기  ·  {k}H</color> 지난 대화", 18, Pal.A(Pal.Text, 0.9f), TextAlignmentOptions.MidlineLeft, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-8, 0));
            _keys.textWrappingMode = TextWrappingModes.NoWrap;
        }

        void BuildSaveMark(Transform t)
        {
            _savePanel = UIKit.Slant(t, "SaveMark", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-230, 24), new Vector2(-24, 64), Pal.A(Pal.Ink, 0.88f), Pal.A(Pal.Panel, 0.8f), Pal.A(Pal.Gold, 0.8f), 0);
            _savePanel.EdgeLeftOnly = true; _savePanel.gameObject.AddComponent<CanvasGroup>();
            _saveText = UIKit.Text(_savePanel.transform, "T", "자동 저장", 19, Pal.A(Pal.Gold, 0.95f), TextAlignmentOptions.MidlineLeft, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-8, 0));
            _saveText.fontStyle = FontStyles.Italic;
            _savePanel.gameObject.SetActive(false);
        }

        void Update()
        {
            if (S == null || S.Player == null) return;
            PlaceToasts();
            // overheard lines wait behind the notebook and the pre-trial summary (they peeked out under both panels)
            if (_subs != null) { bool subsOn = !_s.Note.Open && !CaseReport.Open; if (_subs.gameObject.activeSelf != subsOn) _subs.gameObject.SetActive(subsOn); }
            bool explore = (S.Phase == Phase.Daily || S.Phase == Phase.Investigation || S.Phase == Phase.Assembly) && !_s.Dialogue.Active && !_s.Note.Open && !(_s.Trial?.Active ?? false) && !(_s.Reveal?.Active ?? false)
                && !CaseReport.Open && !(_s.Menu?.Open ?? false);   // the pre-trial summary and the menus cover the aim label and the key strip
            bool cardUp = _s.Cine != null && _s.Cine.CardShowing;   // a title card owns the centre of the screen
            _c.enabled = !(_s.Trial?.Active ?? false) && !(_s.Reveal?.Active ?? false) && !(_s.Cine?.Busy ?? false) && S.Phase != Phase.Prologue;
            var room = S.Layout.Room(S.Player.Room);
            _clock.text = $"{S.Day}일째 · {ClockFmt.Period(S.Clock)} <size=55%><color=#D6AD62>{ClockFmt.BellShort(S.Clock)}</color></size>";
            _place.text = $"{room?.Name ?? "—"}   <color=#9A8E7C>루프 {S.Loop} · 챕터 {S.Chapter} · 생존 {S.Survivors}</color>";
            AutoTrack();
            UpdateInfo();
            var ia = _s.Player?.Interact;
            bool clueUp = _card.gameObject.activeSelf;
            // the aim label and its key hint step aside while a clue card is up (the card is what you just looked at)
            _target.text = explore && !cardUp && !clueUp && ia?.TargetLabel != null ? ia.TargetLabel : "";
            _hint.text = explore && !cardUp && !clueUp && ia?.Hint != null ? ia.Hint : "";
            _cross.enabled = explore;
            bool hov = explore && Settings.Assist >= 2 && ia != null && ia.CanExamine; if (_hover.enabled != hov) _hover.enabled = hov;
            // someone coming to talk: the centre prompt steps aside while a clue card or a title card (수사 …) is up
            if (Time.time < _approachUntil && _approachFrom != null && explore && !clueUp && !cardUp) _center.text = LineBank.FixParticles($"{Cast.NameOf(_approachFrom)}이(가) 말을 걸어온다 — 다가가서 E"); else if (_center.text.Contains("말을 걸어")) _center.text = "";
            if (cardUp && Time.time < _approachUntil) _approachUntil += Time.deltaTime;   // it waits for the card rather than expiring under it
            // modals (심판 전 정리, the menus) own the screen: the case plate and the toasts wait underneath instead of peeking out
            // half-covered; a title card holds the toasts back until it fades (their timers wait too)
            bool modal = CaseReport.Open || (_s.Menu?.Open ?? false);
            HoldOverlays(modal, cardUp);
            FlushEvidence();
            RequestCards();
            Nudge();
            TimeNudge();   // --- time-on-demand
            TraceMarks(explore && !cardUp);
            KeyStrip(explore);
            for (int i = _live.Count - 1; i >= 0; i--) { if (Time.time > _live[i].until) { Object.Destroy(_live[i].go); _live.RemoveAt(i); } else { var cg = _live[i].go.GetComponent<CanvasGroup>(); if (cg) cg.alpha = Mathf.Clamp01((_live[i].until - Time.time) * 2f); } }
            if (_card.gameObject.activeSelf && Time.time > _cardUntil) _card.gameObject.SetActive(false);
            if (_lookPanel.gameObject.activeSelf && (Time.time > _lookUntil || !explore)) _lookPanel.gameObject.SetActive(false);
            if (_banner.gameObject.activeSelf && Time.time > _bannerUntil) _banner.gameObject.SetActive(false);
            if (_savePanel.gameObject.activeSelf) { float left = _saveUntil - Time.unscaledTime; if (left <= 0) _savePanel.gameObject.SetActive(false); else _savePanel.GetComponent<CanvasGroup>().alpha = Mathf.Clamp01(left * 2.5f); }
        }

        CanvasGroup _infoCg, _toastCg;
        /// <summary>Hide the case plate under a modal, and the toast column under a modal or a title card. Toasts keep their time
        /// while hidden (they show once the screen is free, not expire unseen).</summary>
        void HoldOverlays(bool modal, bool titleCard)
        {
            if (_infoCg == null) { _infoCg = _timerPanel.gameObject.GetComponent<CanvasGroup>(); if (_infoCg == null) _infoCg = _timerPanel.gameObject.AddComponent<CanvasGroup>(); }
            if (_toastCg == null) { _toastCg = _toasts.gameObject.GetComponent<CanvasGroup>(); if (_toastCg == null) _toastCg = _toasts.gameObject.AddComponent<CanvasGroup>(); }
            float ia = modal ? 0f : 1f; if (_infoCg.alpha != ia) _infoCg.alpha = ia;
            bool hold = modal || titleCard;
            float ta = hold ? 0f : 1f; if (_toastCg.alpha != ta) _toastCg.alpha = ta;
            if (hold) for (int i = 0; i < _live.Count; i++) if (_live[i].go != null && _live[i].go.transform.parent == _toasts) _live[i] = (_live[i].go, _live[i].until + Time.deltaTime);
        }

        // ------------------------------------------------------------------ the case / rules / compass lines (top left)
        float _caseAt; string _caseLine2; int _firm = -1; string _infoText;
        void UpdateInfo()
        {
            var lines = new List<string>();
            // --- time-on-demand: first line in still daily life — 시간 멈춤 · T 시간 보내기 (함께 다니는 중 / 긴급 / 곧 시간이 멈춘다)
            { var chip = _s.ClockChip(); if (chip != null) lines.Add($"<size=86%><color=#E9D3A8>{chip}</color></size>"); }
            if (S.Phase == Phase.Investigation)
            {
                if (Time.unscaledTime >= _caseAt)
                {
                    _caseAt = Time.unscaledTime + 1f;
                    try { _firm = CaseBoard.FirmCount(_s.Sim); } catch (System.Exception e) { _firm = 0; Debug.LogWarning("[BL23] FirmCount: " + e.Message); }
                    _caseLine2 = null;
                    if (Settings.Assist >= 1) { try { _caseLine2 = CaseBoard.NextSteps(_s.Sim, 1).FirstOrDefault(); } catch (System.Exception e) { Debug.LogWarning("[BL23] NextSteps: " + e.Message); } }
                }
                int m = Mathf.Max(0, Mathf.CeilToInt((float)(S.Ch.InvestigationEnd - S.Clock)));
                lines.Add($"<color=#E9D3A8>수사 {m}분 남음</color>  <color=#9A8E7C>·</color>  사건 개요 <color=#D6AD62>{Mathf.Max(0, _firm)}/4</color>");
                if (!string.IsNullOrEmpty(_caseLine2)) lines.Add($"<size=86%><color=#C9B48A>다음 — {_caseLine2}</color></size>");
            }
            else if (S.Phase == Phase.Assembly) lines.Add("<color=#E9D3A8>심판이 열린다 — 중앙 홀 승강기로 가자</color>");
            else if (S.Ch.Rules.Count > 0 && S.Phase == Phase.Daily) lines.Add("<size=86%><color=#B8AC98>지금 규칙: " + string.Join(" · ", S.Ch.Rules.Where(r => r.Active).Select(r => r.Name)) + "</color></size>");
            // invitations Minhyuk knows about (the version he was told — it may have changed since)
            if (S.Phase == Phase.Daily)
                foreach (var g in S.Gatherings)
                {
                    if (g.Done || g.Cancelled || !g.KnownRev.TryGetValue(Cast.Player, out var kr) || kr < 0) continue;
                    var R = g.Revs[kr]; double left = R.Start - S.Clock;
                    if (left < 60 && S.Clock < R.End) { lines.Add($"<size=86%><color=#D6AD62>초대: {Cast.GivenOf(g.Host)}의 {g.Label} — {S.RoomName(R.Room)} {ClockFmt.Anchor(R.Start)}{(left > 0 ? " (곧 시작)" : " (벌써 시작)")}</color></size>"); break; }
                }
            var comp = CompassLine(); if (comp != null) lines.Add(comp);
            string text = string.Join("\n", lines);
            if (text != _infoText)
            {
                _infoText = text; _timer.text = text;
                bool on = text.Length > 0; if (_timerPanel.gameObject.activeSelf != on) _timerPanel.gameObject.SetActive(on);
                if (on) { float h = _timer.GetPreferredValues(text, InfoW - 34, 0).y; var rt = _timerPanel.rectTransform; rt.offsetMin = new Vector2(24, -128 - Mathf.Max(40, h + 18)); rt.offsetMax = new Vector2(24 + InfoW, -128); }
            }
        }

        // ------------------------------------------------------------------ wayfinding: appointments and tracked rooms
        readonly HashSet<string> _reqShown = new HashSet<string>();
        void AutoTrack()
        {
            // the summons: the way to the grand hall, unless the player is following something else
            if (S.Phase == Phase.Assembly)
            {
                var hall = S.Layout.Rooms.FirstOrDefault(r => r.Type == RoomType.GrandHall && r.Floor == 0);
                if (hall != null && (Guide.TargetRoom < 0 || Guide.Auto) && Guide.AutoKey != "assembly") Guide.TrackAuto(hall.Id, hall.Name, "assembly");
                return;
            }
            if (Guide.Auto && S.Phase != Phase.Daily) { Guide.Clear(); return; }   // no automatic tracking during an investigation
            if (S.Phase != Phase.Daily || S.Requests == null) return;
            if (Guide.Auto && Guide.AutoKey != null)
            {
                var cur = S.Requests.FirstOrDefault(r => r.Id == Guide.AutoKey);
                if (cur == null || cur.State != "accepted") Guide.Clear();
            }
            if (Guide.TargetRoom >= 0 && !Guide.Auto) return;   // the player chose a target
            var next = S.Requests.Where(r => r.Kind == "invite" && r.State == "accepted" && r.Room >= 0 && r.At + 25 > S.Clock).OrderBy(r => r.At).FirstOrDefault();
            if (next == null) { if (Guide.Auto) Guide.Clear(); return; }
            if (Guide.AutoKey != next.Id) Guide.TrackAuto(next.Room, "약속", next.Id);
        }

        float _compAt; string _compDir; int _compRoom = -2;
        string CompassLine()
        {
            if (Guide.TargetRoom < 0 || S.Player == null) return null;
            var room = S.Layout.Room(Guide.TargetRoom); if (room == null) { Guide.Clear(); return null; }
            var me = S.Player;
            if (me.Room == room.Id)
            {
                if (!Guide.Auto) { Toast(room.Name + "에 도착했다", Pal.Gold, 2f); Guide.Clear(); }
                return null;   // an appointment stays tracked until it is kept (leaving the room shows the way back)
            }
            if (Time.unscaledTime >= _compAt || _compRoom != room.Id) { _compAt = Time.unscaledTime + 0.5f; _compRoom = room.Id; _compDir = Direction(room); }
            if (Guide.Auto && Guide.AutoKey != null)
            {
                var r = S.Requests?.FirstOrDefault(x => x.Id == Guide.AutoKey);
                if (r != null)
                {
                    int left = Mathf.CeilToInt((float)(r.At - S.Clock));
                    string when = left > 0 ? $"{left}분 뒤" : "지금 바로";
                    return $"<size=86%><color=#D6AD62>◆ 약속</color> · {room.Name} · {Cast.GivenOf(r.From)} · {when} · {_compDir}</size>";
                }
            }
            return $"<size=86%><color=#D6AD62>◆ {Guide.TargetLabel ?? room.Name}</color> · {FloorName(room.Floor)} · {_compDir}</size>";
        }

        public static string FloorName(int f) => f == 0 ? "1층" : f == 1 ? "2층" : f == -1 ? "지하" : f < -1 ? $"지하 {-f}층" : $"{f + 1}층";

        /// <summary>Which way to go, from where the player looks: along the walking route (doors and stairs), not through walls.</summary>
        PathResult _path; int _pathTo = -2, _pathFrom = -2; float _pathAt = -99f;
        string Direction(Room room)
        {
            var me = S.Player; var cam = _s.Player?.Cam; if (cam == null) return "";
            var goal = new P3(room.Floor, room.Rect.CX, room.Rect.CZ);
            // the route is found again when the target or the player's room changes (and every few seconds), not every frame
            if (_path == null || _pathTo != room.Id || _pathFrom != me.Room || Time.unscaledTime - _pathAt > 4f)
            {
                _pathTo = room.Id; _pathFrom = me.Room; _pathAt = Time.unscaledTime;
                try { _path = Pathfinder.Find(S.Layout, me.Pos, goal, null, 30000); } catch (System.Exception) { _path = null; }
            }
            P3 wp = goal; bool viaStairs = false;
            if (_path != null && _path.Ok && _path.Points.Count > 1)
            {
                var pts = _path.Points; int best = -1; float bd = float.MaxValue;
                for (int i = 0; i < pts.Count; i++) if (pts[i].f == me.Pos.f) { float d = pts[i].DistXZ(me.Pos); if (d < bd) { bd = d; best = i; } }
                if (best >= 0)
                {
                    // already past the nearest route point? aim at the one after it
                    if (best + 1 < pts.Count && pts[best + 1].f == me.Pos.f && me.Pos.DistXZ(pts[best + 1]) < pts[best].DistXZ(pts[best + 1])) best++;
                    wp = pts[pts.Count - 1];
                    for (int i = best; i < pts.Count; i++)
                    {
                        if (pts[i].f != me.Pos.f) { wp = pts[Mathf.Max(best, i - 1)]; viaStairs = true; break; }
                        if (pts[i].DistXZ(me.Pos) > 1.6f || i == pts.Count - 1) { wp = pts[i]; break; }
                    }
                }
            }
            var to = _s.World.ToWorld(wp) - cam.transform.position; to.y = 0;
            var fwd = cam.transform.forward; fwd.y = 0;
            string dir = "앞쪽";
            if (to.sqrMagnitude > 0.04f && fwd.sqrMagnitude > 0.001f)
            {
                float a = Vector3.SignedAngle(fwd, to, Vector3.up);
                dir = Mathf.Abs(a) < 40 ? "앞쪽" : Mathf.Abs(a) > 140 ? "뒤쪽" : a > 0 ? "오른쪽" : "왼쪽";
            }
            if (room.Floor != me.Pos.f) return (room.Floor > me.Pos.f ? "위층" : "아래층") + (viaStairs || wp.f == me.Pos.f ? $" — 계단은 {dir}에" : "");
            return dir;
        }

        // ------------------------------------------------------------------ toasts
        public void Progress(float t) { _ring.fillAmount = Mathf.Clamp01(t); }
        public void Aim(bool on) { if (_aim.enabled != on) _aim.enabled = on; }

        /// <summary>In a conversation the numbered options own the right side of the screen: the toast stack moves to the left
        /// edge (under the case plate) until it ends, so "…의 기억에 남았다" never lands on top of an option.</summary>
        void PlaceToasts()
        {
            bool left = _s != null && _s.Dialogue != null && _s.Dialogue.Active;
            if (_toasts == null || left == _toastsLeft) return;
            _toastsLeft = left;
            _toasts.anchorMin = _toasts.anchorMax = left ? new Vector2(0, 1) : new Vector2(1, 1);
            _toasts.offsetMin = left ? new Vector2(24, -780) : new Vector2(-560, -780);
            _toasts.offsetMax = left ? new Vector2(560, -236) : new Vector2(-24, -236);
        }
        bool _toastsLeft;

        public void Toast(string text, Color c, float secs = 3f)
        {
            if (_toasts == null || string.IsNullOrEmpty(text)) return;
            PlaceToasts();
            // the same news twice is one toast (its time renewed); the stack never grows past five
            foreach (Transform ch in _toasts)
            {
                var tt = ch.Find("T")?.GetComponent<TMP_Text>(); if (tt == null || tt.text != text) continue;
                for (int i = 0; i < _live.Count; i++) if (_live[i].go == ch.gameObject) _live[i] = (ch.gameObject, Mathf.Max(_live[i].until, Time.time + secs));
                return;
            }
            while (_toasts.childCount >= 5) { var old = _toasts.GetChild(0).gameObject; _live.RemoveAll(x => x.go == old); old.transform.SetParent(null, false); Object.Destroy(old); }
            int n = _toasts.childCount;
            var p = UIKit.Slant(_toasts, "Toast", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -60 - n * 64), new Vector2(0, -8 - n * 64), Pal.A(Pal.Ink, 0.9f), Pal.A(Pal.Panel, 0.82f), c, 14);
            p.EdgeLeftOnly = true; p.gameObject.AddComponent<CanvasGroup>();
            var tx = UIKit.Text(p.transform, "T", text, 21, Pal.Text, TextAlignmentOptions.MidlineLeft, Fonts.Bold, Vector2.zero, Vector2.one, new Vector2(24, 4), new Vector2(-12, -4));
            tx.overflowMode = TextOverflowModes.Ellipsis;
            _live.Add((p.gameObject, Time.time + secs));
            Relayout();
        }

        /// <summary>Probe: how many toasts on screen contain this text.</summary>
        public int ProbeToasts(string contains)
        {
            int n = 0; if (_toasts == null) return 0;
            foreach (Transform ch in _toasts) { var tt = ch.Find("T")?.GetComponent<TMP_Text>(); if (tt != null && tt.text.Contains(contains)) n++; }
            return n;
        }
        /// <summary>Probe: the case / compass lines as shown.</summary>
        public string ProbeInfo => _infoText;

        void Relayout()
        {
            int i = 0; foreach (Transform c in _toasts) { var rt = (RectTransform)c; rt.offsetMin = new Vector2(0, -60 - i * 64); rt.offsetMax = new Vector2(0, -8 - i * 64); i++; }
        }

        public void Overheard(string actor, string text, float dist)
        {
            if (_subs == null || string.IsNullOrEmpty(text)) return;
            // overheard lines float above the speaker's head (SpeechBubbles); this corner list is only the fallback
            if (SpeechBubbles.Show(actor, LineBank.Pages(text).FirstOrDefault() ?? text, dist)) return;
            float alpha = Mathf.Clamp01(1.3f - dist / 14f);
            // a line already on screen is not stacked again; at most four overheard lines at once (the oldest goes first)
            string line = LineBank.Pages(text).FirstOrDefault();
            foreach (Transform ch in _subs) { var ot = ch.Find("T")?.GetComponent<TMP_Text>(); if (ot != null && ot.text.EndsWith(line)) return; }
            while (_subs.childCount >= 4) { var old = _subs.GetChild(0).gameObject; _live.RemoveAll(x => x.go == old); old.transform.SetParent(null, false); Object.Destroy(old); }
            var p = UIKit.Rect(_subs, "Sub", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 44));
            var cg = p.gameObject.AddComponent<CanvasGroup>(); cg.alpha = alpha;
            var col = ColorUtility.ToHtmlStringRGB(Pal.Cyan);
            UIKit.Text(p, "T", $"<color=#{col}>{Cast.GivenOf(actor)}</color>  {line}", 22, Pal.A(Pal.Text, 0.95f), TextAlignmentOptions.BottomLeft, Fonts.Body);
            _live.Add((p.gameObject, Time.time + 4.5f));
            int i = 0; foreach (Transform c in _subs.Cast<Transform>().Reverse()) { var rt = (RectTransform)c; rt.anchoredPosition = new Vector2(0, i * 42); i++; }
        }

        // appointment cards: a concrete "when, where, with whom" the moment a request is accepted, and one reminder 10 minutes before
        void RequestCards()
        {
            if (S?.Requests == null) return;
            foreach (var r in S.Requests)
            {
                if (r.State != "accepted") continue;
                if (_reqShown.Add("card:" + r.Id))
                {
                    string t = r.Kind == "invite" ? $"약속 — {ClockFmt.Mark(r.At, true)} · {S.RoomName(r.Room)} · {Cast.GivenOf(r.From)}"
                             : r.Kind == "find" ? $"부탁 — {Cast.GivenOf(r.From)}의 잃어버린 {S.I(r.Item)?.Kor ?? "물건"} 찾아 주기 · 수첩 일정"
                             : $"부탁 — {Cast.GivenOf(r.From)}의 쪽지를 {Cast.GivenOf(r.To)}에게 전하기";
                    Toast(t, Pal.Gold, 5f);
                }
                if (r.Kind == "invite" && r.At - S.Clock <= 10 && r.At - S.Clock > 0 && _reqShown.Add("soon:" + r.Id))
                {
                    Toast($"약속까지 10분 — {S.RoomName(r.Room)} · {Cast.GivenOf(r.From)}", Pal.Gold, 5f);
                    UISfx.Play("chime", 0.35f);
                }
            }
        }

        // ------------------------------------------------------------------ clue notifications: one per find
        struct EvNote { public string Text, Key, Id; }
        readonly List<EvNote> _evPending = new List<EvNote>(); float _evFlushAt = -1f, _filedSfxAt = -9f;
        readonly Dictionary<string, float> _cardShownAt = new Dictionary<string, float>();

        /// <summary>Legacy entry (no id): announced as a new clue.</summary>
        public void EvidenceToast(string title) => EvidenceToast(title, "new", null);

        /// <summary>An evidence event from the kernel ("new" / "update"). Several within 1.2 s become one toast; a card the
        /// player is already looking at (Hud.Card in the last 3 s) is not announced again.</summary>
        public void EvidenceToast(string text, string key, string id)
        {
            // filed while talking: the conversation box says it quietly, and that is the one notification for it
            if (_s.Dialogue != null && _s.Dialogue.Active)
            {
                var ev = FindEvidence(id); if (ev != null && ev.Hidden) return;
                string title = text; bool isKey = false;
                if (ev != null) { try { var v = CaseBoard.Describe(_s.Sim, ev); if (!string.IsNullOrEmpty(v.Title)) title = v.Title; isKey = v.Key; } catch (System.Exception) { } }
                _s.Dialogue.Filed(title, isKey); if (Time.unscaledTime - _filedSfxAt > 1.2f) { _filedSfxAt = Time.unscaledTime; Sfx.Play("evidence", null, 0.45f); }
                return;
            }
            _evPending.Add(new EvNote { Text = text, Key = string.IsNullOrEmpty(key) ? "new" : key, Id = id });
            if (_evFlushAt < 0) _evFlushAt = Time.unscaledTime + 1.2f;
        }

        Evidence FindEvidence(string id) => string.IsNullOrEmpty(id) ? null : S.K(Cast.Player).Evidence.FirstOrDefault(e => e.Id == id);

        void FlushEvidence()
        {
            if (_evFlushAt < 0 || Time.unscaledTime < _evFlushAt) return;
            _evFlushAt = -1f; if (_evPending.Count == 0) return;
            float now = Time.unscaledTime;
            var order = new List<string>(); var byId = new Dictionary<string, (string title, string key, bool isKey)>();
            int anon = 0;
            foreach (var p in _evPending)
            {
                if (p.Id != null && _cardShownAt.TryGetValue(p.Id, out var at) && now - at < 3f) continue;   // the card popup already told the player
                var ev = FindEvidence(p.Id);
                if (ev != null && ev.Hidden) continue;   // folded into another card in the meantime
                string title = p.Text; bool isKey = false;
                if (ev != null) { try { var v = CaseBoard.Describe(_s.Sim, ev); if (!string.IsNullOrEmpty(v.Title)) title = v.Title; isKey = v.Key; } catch (System.Exception) { title = ev.Title ?? title; } }
                if (string.IsNullOrEmpty(title)) continue;
                int cut = title.IndexOf(" — "); if (cut > 0) title = title.Substring(0, cut);
                string id = p.Id ?? ("#" + anon++);
                if (byId.TryGetValue(id, out var prev)) { byId[id] = (title, prev.key == "new" || p.Key == "new" ? "new" : "update", prev.isKey || isKey); continue; }
                byId[id] = (title, p.Key, isKey); order.Add(id);
            }
            _evPending.Clear();
            foreach (var k in _cardShownAt.Where(x => now - x.Value > 10f).Select(x => x.Key).ToList()) _cardShownAt.Remove(k);
            if (order.Count == 0) return;
            if (order.Count == 1)
            {
                var e = byId[order[0]];
                // the glossary's four toasts: 단서 추가 · 중요 단서 · 단서 갱신 · 단서 n건
                if (e.key == "update") Toast("단서 갱신 — " + e.title, Pal.Magenta, 3f);
                else if (e.isKey) Toast("중요 단서 — " + e.title + " · Tab", Pal.Gold, 4f);
                else Toast("단서 추가 — " + e.title + " · Tab", Pal.Magenta, 3.5f);
            }
            else
            {
                var first = order.Select(i => byId[i]).OrderByDescending(x => x.isKey).First();
                bool anyKey = order.Any(i => byId[i].isKey);
                Toast($"단서 {order.Count}건 — {first.title} 외 · Tab", anyKey ? Pal.Gold : Pal.Magenta, 4f);
            }
            Sfx.Play("evidence", null, 0.6f);
        }

        /// <summary>What the player just examined: a short clue card (title, one line, two bullets). A look that found nothing
        /// notable is a caption under the crosshair instead.</summary>
        public void Card(Evidence ev)
        {
            if (ev == null) return;
            if (ev.Loose) { Look(ev); return; }
            CaseBoard.View v = null;
            try { v = CaseBoard.Describe(_s.Sim, ev); } catch (System.Exception e) { Debug.LogWarning("[BL23] Describe: " + e.Message); }
            string title = v?.Title ?? ev.Title ?? "단서", line = v?.Line ?? ev.Line ?? FirstLine(ev.Desc);
            bool key = v?.Key ?? ev.Important;
            _cardKicker.text = key ? "◆ 중요 단서" : "단서"; _cardKicker.color = key ? Pal.Gold : Pal.A(Pal.TextDim, 0.95f);
            _cardPanel.Edge = key ? Pal.A(Pal.Gold, 0.95f) : Pal.A(Pal.Gold, 0.55f); _cardPanel.Refresh();
            _cardTitle.text = title;
            var body = new System.Text.StringBuilder(line ?? "");
            if (v != null) foreach (var b in v.Bullets.Where(b => !string.IsNullOrEmpty(b) && b != line).Take(2)) body.Append("\n<color=#B8AC98>·</color> <size=92%>").Append(b).Append("</size>");
            _cardBody.text = body.ToString();
            var meta = new List<string>();
            if (!string.IsNullOrEmpty(v?.KindLabel)) meta.Add(v.KindLabel);
            if (v != null && v.Hearsay) meta.Add("전해 들은 말");
            if (!string.IsNullOrEmpty(v?.When)) meta.Add(v.When);
            _cardMeta.text = string.Join(" · ", meta);
            FitCard();
            _card.gameObject.SetActive(true); _cardUntil = Time.time + 4f;
            if (!string.IsNullOrEmpty(ev.Id)) _cardShownAt[ev.Id] = Time.unscaledTime;
            if (_lookPanel.gameObject.activeSelf) _lookPanel.gameObject.SetActive(false);
            Sfx.Play("evidence", null, 0.6f);
        }

        /// <summary>The card hugs its words (top edge fixed): title, body, then the meta line — never a frame two-thirds empty.</summary>
        void FitCard()
        {
            const float Top = 150f, W = 610f;
            float bh = _cardBody.GetPreferredValues(_cardBody.text ?? "", W - 64f, 0).y;
            float h = Mathf.Clamp(92f + bh + 56f, 180f, 380f);
            _card.offsetMin = new Vector2(30, Top - h); _card.offsetMax = new Vector2(30 + W, Top);
            _cardPanel.Refresh();
        }

        /// <summary>A card the player is looking at elsewhere (the notebook opened on it): its find is not toasted again.</summary>
        public void MarkShown(string id) { if (!string.IsNullOrEmpty(id)) _cardShownAt[id] = Time.unscaledTime; }

        static string FirstLine(string s) { if (string.IsNullOrEmpty(s)) return ""; int i = s.IndexOf('\n'); return i < 0 ? s : s.Substring(0, i); }

        /// <summary>A look that turned up nothing notable: "{Title} — {Line}" under the crosshair for 2.5 s. Nothing is filed.</summary>
        public void Look(Evidence ev)
        {
            if (ev == null) return;
            string line = !string.IsNullOrEmpty(ev.Line) ? ev.Line : FirstLine(ev.Desc);
            string text = string.IsNullOrEmpty(line) ? ev.Title : (string.IsNullOrEmpty(ev.Title) ? line : ev.Title + " — " + line);
            Caption(text, 2.5f);
        }

        /// <summary>A one-line caption under the crosshair (looks, small results).</summary>
        public void Caption(string text, float secs)
        {
            if (string.IsNullOrEmpty(text)) return;
            _look.text = text;
            float w = Mathf.Min(1100f, _look.GetPreferredValues(text).x + 48f);
            var rt = _lookPanel.rectTransform; rt.offsetMin = new Vector2(-w / 2, -176); rt.offsetMax = new Vector2(w / 2, -128);
            _lookPanel.gameObject.SetActive(true); _lookUntil = Time.time + secs;
        }

        /// <summary>A page of mansion lore (books, films, curiosities): shown on the clue card frame, but it is not a clue.</summary>
        public void Lore(string title, string text)
        {
            _cardKicker.text = "읽은 것"; _cardKicker.color = Pal.A(Pal.TextDim, 0.95f);
            _cardPanel.Edge = Pal.A(Pal.Gold, 0.45f); _cardPanel.Refresh();
            _cardTitle.text = title; _cardBody.text = "<i>" + text + "</i>";
            _cardMeta.text = "저택에서 읽은 글 — 단서는 아니다"; _cardTab.text = "";
            FitCard();
            _card.gameObject.SetActive(true); _cardUntil = Time.time + 9f;
            CancelInvoke(nameof(RestoreTab)); Invoke(nameof(RestoreTab), 9.2f);
        }
        void RestoreTab() { if (_cardTab != null) _cardTab.text = "Tab 수첩"; }

        // a one-line system note that shows over everything (court, cinematics): "지금은 저장할 수 없다"
        Canvas _sysC; SlantPanel _sysPanel; TextMeshProUGUI _sysText; float _sysUntil;
        public void SysNote(string text, float secs = 1.8f)
        {
            if (_sysC == null)
            {
                _sysC = UIKit.Root("HudSys", 63);
                _sysPanel = UIKit.Slant(_sysC.transform, "P", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-300, 60), new Vector2(300, 104), Pal.A(Pal.Ink, 0.92f), Pal.A(Pal.Panel, 0.85f), Pal.A(Pal.Gold, 0.7f), 0);
                _sysPanel.EdgeLeftOnly = true;
                _sysText = UIKit.Text(_sysPanel.transform, "T", "", 21, Pal.Text, TextAlignmentOptions.Center, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-12, 0));
            }
            _sysText.text = text; float w = Mathf.Min(1000f, _sysText.GetPreferredValues(text).x + 60f);
            _sysPanel.rectTransform.offsetMin = new Vector2(-w / 2, 60); _sysPanel.rectTransform.offsetMax = new Vector2(w / 2, 104);
            _sysC.enabled = true; _sysUntil = Time.unscaledTime + secs;
        }
        void LateUpdate() { if (_sysC != null && _sysC.enabled && Time.unscaledTime > _sysUntil) _sysC.enabled = false; }

        /// <summary>Small "자동 저장" plate, bottom right, for 1.5 s.</summary>
        public void SaveMark(string text = "자동 저장")
        {
            _saveText.text = text; _savePanel.gameObject.SetActive(true); _saveUntil = Time.unscaledTime + 1.5f; _savePanel.GetComponent<CanvasGroup>().alpha = 1;
        }

        public void Discovery(string victim, bool dead)
        {
            _bannerText.text = dead ? "시신을 발견했다" : "누군가 쓰러져 있다";
            _banner.gameObject.SetActive(true); _bannerUntil = Time.time + 3.2f;
            Toast(dead ? "R을 길게 눌러 살펴보자 — 곧 종이 울린다" : "Q 응급처치 · E 둘러메고 구급실로", Pal.Blood, 6f);
        }

        public void Approach(string actor) { _approachFrom = actor; _approachUntil = Time.time + 6; }
        public void Center(string text, float secs) { _center.text = text; CancelInvoke(nameof(ClearCenter)); Invoke(nameof(ClearCenter), secs); }
        void ClearCenter() { _center.text = ""; }

        // ------------------------------------------------------------------ first-day key strip
        void KeyStrip(bool explore)
        {
            bool want = explore && Settings.FirstDayKeys && S.Day <= 1 && S.Loop <= 1 && S.Chapter <= 1;
            float a = Mathf.MoveTowards(_keysCg.alpha, want ? 1f : 0f, Time.unscaledDeltaTime * (want ? 3f : 0.8f));
            if (!Mathf.Approximately(a, _keysCg.alpha)) _keysCg.alpha = a;
            bool on = a > 0.001f; if (_keysPanel.gameObject.activeSelf != on) _keysPanel.gameObject.SetActive(on || want);
        }

        // ------------------------------------------------------------------ 도움 친절: nudge and trace marks
        float _nudgeProgressAt = -1f, _nudgeLastAt = -999f, _nudgeCheckAt; int _nudgeFirm = -1, _nudgeKeys = -1;
        void Nudge()
        {
            if (S.Phase != Phase.Investigation || Settings.Assist < 2) { _nudgeProgressAt = -1f; return; }
            float now = Time.unscaledTime;
            if (_nudgeProgressAt < 0) { _nudgeProgressAt = now; _nudgeFirm = -1; _nudgeKeys = -1; }
            if (now < _nudgeCheckAt) return;
            _nudgeCheckAt = now + 5f;
            if (_s.Paused || _s.Dialogue.Active || _s.Note.Open) { _nudgeProgressAt = Mathf.Max(_nudgeProgressAt, now - 240f); return; }   // reading the notebook is progress enough
            int firm = Mathf.Max(0, _firm), keys = 0;
            try { keys = CaseBoard.Cards(_s.Sim).Count(v => v.Key); } catch (System.Exception) { }
            if (firm != _nudgeFirm || keys != _nudgeKeys) { _nudgeFirm = firm; _nudgeKeys = keys; _nudgeProgressAt = now; return; }
            if (now - _nudgeProgressAt < 300f || now - _nudgeLastAt < 600f) return;
            string step = null; try { step = CaseBoard.NextSteps(_s.Sim, 1).FirstOrDefault(); } catch (System.Exception) { }
            if (string.IsNullOrEmpty(step)) return;
            _nudgeLastAt = now; _nudgeProgressAt = now;
            Toast("귀띔 — " + step + " · Tab", Pal.Gold, 7f);
        }

        // --- time-on-demand (begin): eight real minutes of still daily life with no time spent → a one-line reminder
        // (at most three a day)
        float _stillSince = -1f; int _timeNudgeDay = -1, _timeNudges;
        void TimeNudge()
        {
            if (!_s.OnDemandDaily || !_s.WorldStill || (_s.TimeDir?.Active ?? false)) { _stillSince = -1f; return; }
            float now = Time.unscaledTime;
            if (_stillSince < 0) { _stillSince = now; return; }
            if (now - _stillSince < 480f) return;
            _stillSince = now;
            if (_timeNudgeDay != S.Day) { _timeNudgeDay = S.Day; _timeNudges = 0; }
            if (_timeNudges >= 3 || _s.Dialogue.Active || _s.Note.Open || (_s.Menu?.Open ?? false)) return;
            _timeNudges++;
            SysNote("T를 누르면 다음 일까지 시간을 보낼 수 있다", 5f);
        }
        // --- time-on-demand (end)

        float _marksAt; readonly List<Trace> _markTraces = new List<Trace>();
        void TraceMarks(bool show)
        {
            bool on = show && Settings.Assist >= 2 && S.Phase == Phase.Investigation;
            if (on && Time.unscaledTime >= _marksAt)
            {
                _marksAt = Time.unscaledTime + 0.5f; _markTraces.Clear();
                var inc = CaseProgress.Current(S); var k = S.K(Cast.Player);
                if (inc != null)
                    foreach (var t in S.Traces)
                    {
                        if (t.Room != inc.FoundRoom || t.Cleaned || t.Visibility > 1 || t.Type == "PowerResidue" || t.Pos.f != S.Player.Pos.f) continue;
                        if (k.Examined.Contains("trace:" + t.Id) || t.Pos.DistXZ(S.Player.Pos) > 5f) continue;
                        _markTraces.Add(t); if (_markTraces.Count >= 8) break;
                    }
            }
            var cam = _s.Player?.Cam; int used = 0;
            if (on && cam != null && cam.isActiveAndEnabled)
            {
                var root = (RectTransform)_c.transform;
                foreach (var t in _markTraces)
                {
                    var wp = _s.World.ToWorld(t.Pos) + Vector3.up * 0.08f; var sp = cam.WorldToScreenPoint(wp);
                    if (sp.z < 0.2f || sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height) continue;
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out var lp)) continue;
                    if (used >= _marks.Count) { var m = UIKit.Text(_marksRoot, "Mark", "◆", 22, Pal.A(Pal.Gold, 0.85f), TextAlignmentOptions.Center, Fonts.Body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-14, -14), new Vector2(14, 14)); _marks.Add(m); }
                    var mk = _marks[used++]; mk.gameObject.SetActive(true);
                    mk.rectTransform.anchoredPosition = lp + new Vector2(0, 18f + Mathf.Sin(Time.unscaledTime * 3f) * 3f);
                }
            }
            for (int i = used; i < _marks.Count; i++) if (_marks[i].gameObject.activeSelf) _marks[i].gameObject.SetActive(false);
        }
    }
}
