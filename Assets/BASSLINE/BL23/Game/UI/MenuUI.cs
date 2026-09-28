using System;
using System.Linq;
using BL23.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BL23.Game
{
    /// <summary>Pause / settings / save-load / wait / facility panels (switchboard, press console), and confirmations.</summary>
    public sealed class MenuUI : MonoBehaviour
    {
        Session _s; GameState S => _s.S; Canvas _c; RectTransform _panel; public bool Open;
        /// <summary>The frame the menu closed on (Esc must not reopen the pause menu in the same frame).</summary>
        public static int ClosedFrame = -1;
        int _openedFrame = -1; Action _back; GameObject _confirm; Action _confirmYes, _confirmNo; bool _confirmOnly;

        public void Init(Session s) { _s = s; _c = UIKit.Root("Menu", 66); _c.enabled = false; }   // above the court's effects layer (60)
        public void Destroy() { if (_c) UnityEngine.Object.Destroy(_c.gameObject); }

        void Begin(string title, float w = 760, float h = 760, Action back = null)
        {
            UIKit.Clear(_c.transform); _confirm = null; _confirmOnly = false; _back = back; ClearKeys();   // --- time-on-demand: row keys belong to the wait menu
            _c.enabled = true; if (!Open) _openedFrame = Time.frameCount; Open = true; _s.Pause("menu");
            UIKit.Img(_c.transform, "Dim", Pal.A(Pal.Ink, 0.8f), Vector2.zero, Vector2.one).raycastTarget = true;
            var p = UIKit.Slant(_c.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-w / 2, -h / 2), new Vector2(w / 2, h / 2), Pal.A(Pal.Panel2, 0.98f), Pal.A(Pal.Ink, 0.98f), Pal.A(Pal.Gold, 0.8f), 30);
            p.Glow = 0.2f; _panel = p.rectTransform;
            // never larger than the screen (small windows or large UI sizes): shrink the whole panel to fit
            Canvas.ForceUpdateCanvases(); var cr = ((RectTransform)_c.transform).rect;
            if (cr.width > 1 && cr.height > 1) { float k = Mathf.Min(1f, (cr.height - 30) / h, (cr.width - 30) / w); _panel.localScale = Vector3.one * k; }
            UIKit.Text(_panel, "Title", title, 42, Pal.Text, TextAlignmentOptions.Top, Fonts.Title, Vector2.zero, Vector2.one, new Vector2(30, 20), new Vector2(-30, -26));
            UIKit.Text(_panel, "Esc", back != null ? "Esc 뒤로" : "Esc 닫기", 17, Pal.A(Pal.TextDim, 0.8f), TextAlignmentOptions.TopRight, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(30, 20), new Vector2(-34, -30));
        }

        public void Close()
        {
            Open = false; _c.enabled = false; UIKit.Clear(_c.transform); _confirm = null; _confirmOnly = false; _back = null; ClearKeys();   // --- time-on-demand
            _s.Resume("menu"); ClosedFrame = Time.frameCount;
        }

        void Update()
        {
            if (!Open || Time.frameCount <= 1 || Time.frameCount == _openedFrame) return;
            if (_confirm != null)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { UISfx.Confirm(); var yes = _confirmYes; CloseConfirm(); yes?.Invoke(); return; }
                if (Input.GetKeyDown(KeyCode.Escape)) { UISfx.Cancel(); if (_confirmOnly) { Close(); return; } var no = _confirmNo; CloseConfirm(); no?.Invoke(); return; }
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) { UISfx.Cancel(); if (_back != null) _back(); else Close(); return; }
            WaitKeys();   // --- time-on-demand: 1–9, ←/→, T / Enter in the wait menu
        }

        UIKit.Btn Btn(int i, string label, Action a, float y0 = 120) => UIKit.Button(_panel, label, a, new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -y0 - (i + 1) * 70), new Vector2(-60, -y0 - i * 70 - 8), 28);

        // ------------------------------------------------------------------ confirmations
        /// <summary>A yes/no plate over whatever the menu shows. Enter = yes, Esc = no.</summary>
        public void Confirm(string title, string body, string yes, Action onYes, string no = "취소", Action onNo = null)
        {
            if (!Open) { ConfirmOnly(title, body, yes, onYes); return; }
            CloseConfirm();
            var root = UIKit.Full(_c.transform, "Confirm"); _confirm = root.gameObject; _confirmYes = onYes; _confirmNo = onNo;
            UIKit.Img(root, "Dim", Pal.A(Pal.Ink, 0.72f), Vector2.zero, Vector2.one).raycastTarget = true;
            const float w = 820, h = 380;
            var p = UIKit.Slant(root, "P", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-w / 2, -h / 2), new Vector2(w / 2, h / 2), Pal.A(Pal.Panel2, 0.99f), Pal.A(Pal.Ink, 0.99f), Pal.A(Pal.Gold, 0.9f), 0);
            p.Glow = 0.25f; UIKit.FitToCanvas(p.rectTransform, w, h);
            UIKit.Text(p.transform, "T", title, 34, Pal.Text, TextAlignmentOptions.Top, Fonts.Title, Vector2.zero, Vector2.one, new Vector2(40, 24), new Vector2(-40, -26));
            UIKit.Text(p.transform, "B", body, 24, Pal.Text, TextAlignmentOptions.Top, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(50, 120), new Vector2(-50, -84));
            UIKit.Button(p.transform, yes + "  <size=70%><color=#9A8E7C>Enter</color></size>", () => { var y = _confirmYes; CloseConfirm(); y?.Invoke(); }, new Vector2(0.08f, 0), new Vector2(0.48f, 0), new Vector2(0, 34), new Vector2(0, 100), 26);
            UIKit.Button(p.transform, no + "  <size=70%><color=#9A8E7C>Esc</color></size>", () => { if (_confirmOnly) { Close(); return; } var n = _confirmNo; CloseConfirm(); n?.Invoke(); }, new Vector2(0.52f, 0), new Vector2(0.92f, 0), new Vector2(0, 34), new Vector2(0, 100), 26);
        }

        void CloseConfirm() { if (_confirm != null) Destroy(_confirm); _confirm = null; }

        /// <summary>Only a confirmation (e.g. F9 quick load from free exploration): "no" closes the menu.</summary>
        public void ConfirmOnly(string title, string body, string yes, Action onYes)
        {
            UIKit.Clear(_c.transform); _confirm = null; _back = null;
            _c.enabled = true; if (!Open) _openedFrame = Time.frameCount; Open = true; _s.Pause("menu");
            Confirm(title, body, yes, () => { Close(); onYes?.Invoke(); }, "취소", Close);
            _confirmOnly = true;
        }

        /// <summary>"루프 1 · 챕터 2 · 9월 27일 15:42" — which run and when it was written.</summary>
        public static string SlotLine(SaveStore.SlotInfo info) =>
            info == null || !info.Exists ? "" : info.Corrupt ? "손상된 파일" : $"루프 {info.Loop} · 챕터 {info.Chapter} · {info.Saved.Month}월 {info.Saved.Day}일 {info.Saved:HH:mm}";

        // ------------------------------------------------------------------ pause
        bool CanSaveHere(out string why)
        {
            why = null; var ph = S.Phase;
            if ((_s.Trial?.Active ?? false) || ph == Phase.Trial || ph == Phase.Verdict) { why = "심판 중에는 저장할 수 없다"; return false; }
            if ((_s.Reveal?.Active ?? false) || (_s.Cine?.Busy ?? false) || ph == Phase.Prologue || ph == Phase.LoopEpilogue) { why = "지금은 저장할 수 없다"; return false; }
            if (_s.Dialogue.Active) { why = "대화 중에는 저장할 수 없다"; return false; }
            if (_s.TimeDir != null && _s.TimeDir.Active) { why = "시간이 흐르는 동안에는 저장할 수 없다"; return false; }   // --- time-on-demand
            if (S.Player == null || !S.Player.Alive) { why = "지금은 저장할 수 없다"; return false; }
            return true;
        }

        public void OpenPause()
        {
            Begin("일시정지", 760, 800);
            int i = 0;
            Btn(i++, "계속하기", Close);
            bool canSave = CanSaveHere(out var why);
            var sb = Btn(i++, canSave ? "저장하기" : $"저장하기  <size=70%><color=#9A8E7C>{why}</color></size>", () => OpenSaveLoad(true, OpenPause));
            if (!canSave) { sb.Interactable = false; sb.Paint(); }
            Btn(i++, "불러오기", () => OpenSaveLoad(false, OpenPause));
            Btn(i++, "설정", () => OpenSettings(OpenPause));
            Btn(i++, "조작법", () => OpenHelp(OpenPause));
            Btn(i++, "타이틀로", () => Confirm("타이틀로", "타이틀 화면으로 돌아갈까?\n<size=85%><color=#9A8E7C>저장하지 않은 부분은 사라진다.</color></size>", "돌아가기", () => { Close(); GameBoot.I.ToTitle(); }));
            Btn(i++, "게임 종료", () => Confirm("게임 종료", "게임을 끝낼까?\n<size=85%><color=#9A8E7C>저장하지 않은 부분은 사라진다.</color></size>", "끝내기", Application.Quit));
            var newest = Session.NewestSave();
            if (newest != null)
            {
                var info = SaveStore.Info(newest);
                UIKit.Text(_panel, "Last", $"<color=#9A8E7C>마지막 저장</color>  {info.Label}\n<size=85%><color=#9A8E7C>{SlotLine(info)} · F5 빠른 저장 · F9 빠른 불러오기</color></size>", 19, Pal.Text, TextAlignmentOptions.Bottom, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(40, 26), new Vector2(-40, 0));
            }
        }

        // ------------------------------------------------------------------ help
        public void OpenHelp() => OpenHelp(null);
        void OpenHelp(Action back)
        {
            Begin("조작법", 1240, 900, back);
            const string g = "<color=#D6AD62>", e = "</color>";
            // --- time-on-demand (begin): simple things are instant; how the clock moves depends on the time flow
            bool od = _s.TimeFlow == TimeFlowMode.OnDemand;
            string timeNote = od
                ? "<color=#9A8E7C>일상에서는 시간이 멈춰 있다. T로 시간을 보내거나, 누군가와 함께 시간을 보내면 흐른다. 대화를 나누면 그만큼 조금 흐르고, 누군가와 함께 다니는 동안과 급한 일이 벌어졌을 때는 저절로 흐른다.\n" +
                  "문·서랍 열기, 앉기, 둘러보기에는 시간이 들지 않는다. 수사 중에는 시간이 계속 흐른다.</color>"
                : "<color=#9A8E7C>대화 중에는 한 마디에 1분이 채 안 되는 시간이 흐른다. 선택지를 고르거나 수첩·메뉴를 펼쳐 둔 동안에는 저택의 시간이 멈춘다.\n" +
                  "걸어 다니는 동안에는 시간이 흐른다.</color>";
            // --- time-on-demand (end)
            UIKit.Text(_panel, "T",
                $"{g}이동{e}   WASD 이동 · Shift 달리기 · C 몸 낮추기 · 마우스 시점 · F 손전등\n" +
                $"{g}행동{e}   E 말 걸기·줍기·문·열기·앉기·사용 · X 시간을 들이는 일(책 읽기·연주 등)·전부 열기 · Z 누르고 있으면 들기(클릭: 던지기) · G 손에 든 것 내려놓기(길게: 던지기)\n" +
                $"          R(길게) 살펴보기 — 쓸 만한 건 수첩에 적힌다 · L 문 잠그기/풀기 · K 노크 · B 종 울리기 · Q 응급처치\n" +
                $"{g}수첩{e}   Tab/N 수첩 (1–5 탭 · Q/E 탭 넘기기 · Enter ★ 고정 · Space 자세히) · M 지도 · J 목표 · I 소지품\n" +
                $"{g}시간{e}   T 시간 보내기 (수사 중: 수사를 마치고 심판으로) · H 지난 대화 · F5 빠른 저장 · F9 빠른 불러오기 · Esc 메뉴\n" +
                $"{g}대화{e}   1–9 선택 (0은 열 번째) · ↑/↓ + Enter · V 자동 진행 · Ctrl(누르고 있기) 빨리 넘기기 · 휠 위로 지난 대화\n" +
                $"{g}심판{e}   F 발언 되짚기 · ←/→ 발언·단서 칸 바꾸기 · Enter 내밀기 · Esc 한 단계 뒤로 · A 지목(범인 논의 때)\n" +
                $"          V 자동 진행 · Ctrl 빨리 넘기기 · Esc 일시정지\n" +
                $"{g}공격{e}   무기를 들고 우클릭으로 겨눈 뒤 좌클릭 — 다른 사람이 저지른 일과 똑같이 흔적이 남고 조사된다\n\n" +
                timeNote,
                23, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(60, 60), new Vector2(-60, -110));
        }

        // ------------------------------------------------------------------ settings
        public void OpenSettings() => OpenSettings(null);
        void OpenSettings(Action back)
        {
            // two columns (sliders | switches) so the panel fits a 1280×720 canvas even at 150% UI size
            Begin("설정", 1240, 860, back); int i = 0;
            void Again() => OpenSettings(back);
            Slider(i++, "전체 음량", Settings.Master, v => { Settings.Master = v; Settings.Apply(); });
            Slider(i++, "음악", Settings.Music, v => { Settings.Music = v; Settings.Apply(); });
            Slider(i++, "효과음", Settings.SfxVol, v => { Settings.SfxVol = v; Settings.Apply(); });
            Slider(i++, "대사 효과음", Settings.Voice, v => { Settings.Voice = v; Settings.Apply(); });
            Slider(i++, "마우스 감도", Settings.Sensitivity / 5f, v => Settings.Sensitivity = Mathf.Max(0.2f, v * 5f));
            Slider(i++, "시야각", (Settings.Fov - 55) / 40f, v => Settings.Fov = 55 + v * 40);
            Slider(i++, "텍스트 속도", Settings.TextSpeed / 2f, v => Settings.TextSpeed = Mathf.Max(0.3f, v * 2));
            i = 0;
            string[] assist = { "끔", "보통 — 다음 할 일 한 줄", "친절 — 흔적 표시·조준 고리" };
            Toggle(i++, "도움: " + assist[Settings.Assist], () => { Settings.Assist = (Settings.Assist + 1) % 3; Again(); });
            string[] tt = { "끔", "60초", "30초" };
            Toggle(i++, "심판 제한 시간: " + tt[Settings.TrialTime], () => { Settings.TrialTime = (Settings.TrialTime + 1) % 3; Again(); });
            Toggle(i++, "대화 자동 진행 (V): " + (Settings.AutoAdvance ? "켬" : "끔"), () => { Settings.AutoAdvance = !Settings.AutoAdvance; Again(); });
            Toggle(i++, "심판 자동 진행 (V): " + (Settings.TrialAuto ? "켬" : "끔"), () => { Settings.TrialAuto = !Settings.TrialAuto; Again(); });
            Toggle(i++, "첫날 조작 안내: " + (Settings.FirstDayKeys ? "켬" : "끔"), () => { Settings.FirstDayKeys = !Settings.FirstDayKeys; Again(); });
            Toggle(i++, "잔혹 표현: " + (Settings.Gore == 2 ? "강함" : Settings.Gore == 1 ? "기본" : "완화") + " (얻는 정보는 같다)", () => { Settings.Gore = (Settings.Gore + 2) % 3; Again(); });
            Toggle(i++, "걸을 때 화면 흔들림: " + (Settings.HeadBob ? "켬" : "끔"), () => { Settings.HeadBob = !Settings.HeadBob; Again(); });
            Toggle(i++, "앉기: " + (Settings.CrouchToggle ? "한 번 누르기" : "누르고 있기"), () => { Settings.CrouchToggle = !Settings.CrouchToggle; Again(); });
            Toggle(i++, "Y축 반전: " + (Settings.InvertY ? "켬" : "끔"), () => { Settings.InvertY = !Settings.InvertY; Again(); });
            Toggle(i++, "루프 기준 인원(새 캠페인): " + Settings.FloorPreset + "명", () => { Settings.FloorPreset = Settings.FloorPreset == 4 ? 5 : 4; Again(); });
            Toggle(i++, $"UI 크기: {Mathf.RoundToInt(Settings.UIScale * 100)}%", () => { Settings.UIScale = Settings.UIScale < 1.1f ? 1.25f : Settings.UIScale < 1.4f ? 1.5f : 1f; Settings.Apply(); Again(); });
        }

        void Slider(int i, string label, float v, Action<float> set)
        {
            var row = UIKit.Rect(_panel, label, new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(50, -110 - (i + 1) * 62), new Vector2(-20, -110 - i * 62));
            UIKit.Text(row, "L", label, 24, Pal.Text, TextAlignmentOptions.MidlineLeft, Fonts.Body, new Vector2(0, 0), new Vector2(0.45f, 1));
            var bar = UIKit.Img(row, "Bar", Pal.A(Pal.TextDim, 0.3f), new Vector2(0.48f, 0.4f), new Vector2(1, 0.6f));
            var fill = UIKit.Img(bar.transform, "Fill", Pal.Magenta, Vector2.zero, new Vector2(Mathf.Clamp01(v), 1));
            var knob = bar.gameObject.AddComponent<SliderDrag>(); knob.Fill = fill.rectTransform; knob.OnChange = set; bar.raycastTarget = true;
        }

        void Toggle(int i, string label, Action a) => UIKit.Button(_panel, label, a, new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(20, -110 - (i + 1) * 62), new Vector2(-50, -110 - i * 62 - 8), 20);

        // ------------------------------------------------------------------ save / load
        public void OpenSaveLoad(bool save) => OpenSaveLoad(save, null);
        void OpenSaveLoad(bool save, Action back)
        {
            if (save)
            {
                if (!CanSaveHere(out var why)) { Hud.I?.Toast(why, Pal.TextDim, 2f); return; }
                Begin("저장", 960, 800, back);
                for (int slot = 1; slot <= 6; slot++)
                {
                    int sl = slot; string path = Settings.SlotPath(slot); var info = SaveStore.Info(path);
                    SlotRow(_panel, new Vector2(0, 1), new Vector2(1, 1), slot - 1, $"슬롯 {slot}", info, () =>
                    {
                        void Do() { _s.Save(sl, _s.SaveLabel()); OpenSaveLoad(true, back); }
                        if (info.Exists) Confirm("덮어쓰기", $"슬롯 {sl}에 덮어쓸까?\n<size=80%><color=#9A8E7C>{info.Label}\n{SlotLine(info)}</color></size>", "덮어쓰기", Do);
                        else Do();
                    });
                }
                UIKit.Text(_panel, "Note", "<color=#9A8E7C>자동 저장은 챕터 시작·아침·수사 시작·심판 직전에 세 칸을 번갈아 쓴다 · F5 빠른 저장</color>", 19, Pal.TextDim, TextAlignmentOptions.Bottom, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(30, 24), new Vector2(-30, 0));
                return;
            }
            Begin("불러오기", 1500, 820, back);
            UIKit.Text(_panel, "HL", "자동 · 빠른 저장", 22, Pal.A(Pal.Gold, 0.9f), TextAlignmentOptions.TopLeft, Fonts.Serif, new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(60, -112), new Vector2(0, -84));
            UIKit.Text(_panel, "HR", "직접 저장한 슬롯", 22, Pal.A(Pal.Gold, 0.9f), TextAlignmentOptions.TopLeft, Fonts.Serif, new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(20, -112), new Vector2(0, -84));
            int li = 0, ri = 0;
            foreach (var (name, path) in Session.SaveFiles())
            {
                var info = SaveStore.Info(path); bool manual = name.StartsWith("슬롯"); string p = path, nm = name;
                var b = manual ? SlotRow(_panel, new Vector2(0.5f, 1), new Vector2(1, 1), ri++, name, info, null, 12) : SlotRow(_panel, new Vector2(0, 1), new Vector2(0.5f, 1), li++, name, info, null, 12);
                b.OnClick = () => Confirm("불러오기", $"「{nm}」에서 불러올까?\n<size=80%><color=#9A8E7C>{info.Label}\n{SlotLine(info)}\n저장하지 않은 부분은 사라진다.</color></size>", "불러오기", () =>
                {
                    var st = Session.LoadPath(p, out var note);
                    if (st == null) { Hud.I?.Toast(note ?? "불러올 수 없다", Pal.Blood); return; }
                    Close(); GameBoot.I.StartFromState(st, note);
                });
                if (!info.Exists || info.Corrupt) { b.Interactable = false; b.Paint(); }
            }
        }

        /// <summary>One save row: "자동 2 — 3일째 오후 1:02 · 도서실 · 수사 중" over "루프 1 · 챕터 2 · 9월 27일 15:42".</summary>
        UIKit.Btn SlotRow(RectTransform parent, Vector2 aMin, Vector2 aMax, int i, string name, SaveStore.SlotInfo info, Action a, float top = 0)
        {
            string label = $"<color=#D6AD62>{name}</color>  " + (info.Exists ? (info.Corrupt ? "<color=#C04A55>손상됨</color>" : (info.Label ?? "")) + $"\n<size=72%><color=#9A8E7C>{SlotLine(info)}</color></size>" : "<color=#9A8E7C>비어 있음</color>");
            float y0 = 110 + top + 20;
            var b = UIKit.Button(parent, label, a, aMin, aMax, new Vector2(aMin.x > 0.1f ? 20 : 60, -y0 - (i + 1) * 96), new Vector2(aMax.x < 0.9f ? -20 : -60, -y0 - i * 96 - 10), 22);
            b.Label.textWrappingMode = TextWrappingModes.NoWrap; b.Label.overflowMode = TextOverflowModes.Ellipsis;
            return b;
        }

        // ------------------------------------------------------------------ waiting
        // --- time-on-demand (begin): 시간 보내기 — the smart row (계속 기다리기 / 잠자리에 든다 / 다음 일까지), 15분·30분·1시간·3시간,
        // "~까지" rows, things to do here, the quiet toggle. Keys: 1–9 pick a row, ←/→ a chip, T or Enter the highlighted row.
        readonly System.Collections.Generic.Dictionary<int, Action> _numKeys = new System.Collections.Generic.Dictionary<int, Action>();
        Action _enterAct; UIKit.Btn[] _chipBtns; Action[] _chipActs; int _chip = -1; bool _smartIsDue;
        void ClearKeys() { _numKeys.Clear(); _enterAct = null; _chipBtns = null; _chipActs = null; _chip = -1; }

        /// <summary>Row keys of the wait menu (called from Update; false when the menu has none).</summary>
        bool WaitKeys()
        {
            if (_numKeys.Count == 0 && _enterAct == null) return false;
            for (int k = 1; k <= 9; k++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + k)) || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad0 + k)))
                { if (_numKeys.TryGetValue(k, out var a)) { UISfx.Confirm(); a(); } return true; }
            if (_chipBtns != null && (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))
            {
                int n = _chipBtns.Length; bool right = Input.GetKeyDown(KeyCode.RightArrow);
                _chip = _chip < 0 ? (right ? 0 : n - 1) : (_chip + (right ? 1 : n - 1)) % n;
                for (int i = 0; i < n; i++) { _chipBtns[i].Hover = i == _chip; _chipBtns[i].Paint(); }
                UISfx.Hover(); return true;
            }
            if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                var a = _chip >= 0 && _chipActs != null && _chip < _chipActs.Length ? _chipActs[_chip] : _enterAct;
                if (a != null) { UISfx.Confirm(); a(); }
                return true;
            }
            return false;
        }

        UIKit.Btn Row(float y, float h, string label, Action a, float size = 25, float x0 = 60, float x1 = -60) =>
            UIKit.Button(_panel, label, a, new Vector2(0, 1), new Vector2(1, 1), new Vector2(x0, -(y + h)), new Vector2(x1, -y), size);
        static string Num(int n) => $"<color=#D6AD62>{n}</color>  ";
        static string Dim(string s) => $"<size=72%><color=#9A8E7C>{s}</color></size>";

        public void OpenWait() => OpenWait(false);
        void OpenWait(bool expanded)
        {
            if (S.Phase == Phase.Investigation) { OpenWaitInvestigation(); return; }
            if (S.Phase != Phase.Daily) { Hud.I?.Toast("지금은 시간을 보낼 수 없다", Pal.TextDim, 2f); return; }
            var td = _s.TimeDir; if (td != null && td.Active) return;
            bool od = _s.TimeFlow == TimeFlowMode.OnDemand; var me = S.Player; int m = S.Minute;
            bool night = m >= 20 * 60 || m < 6 * 60 + 30;
            // what the menu offers
            var smart = SmartRow(night);
            var until = UntilRows(smart.at);
            var here = Pastimes();
            bool sleepRow = night && !smart.sleep;
            int rows = until.Count + (here.Count == 0 ? 0 : expanded ? here.Count : 1) + (sleepRow ? 1 : 0);
            float h = 150 + 80 + 70 + rows * 60 + 60 + 90;
            Begin("시간 보내기", 1100, h);
            ClearKeys();
            UIKit.Text(_panel, "Sub", $"{S.Day}일째 · {TimeUI.Clock12(S.Clock)}" + (od ? " · <color=#D6AD62>지금은 시간이 멈춰 있다</color>" : ""), 22, Pal.A(Pal.TextDim, 0.95f), TextAlignmentOptions.Top, Fonts.Serif, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -118), new Vector2(-40, -84));
            float y = 140; int key = 1;
            // 1: the smart row (highlighted: T / Enter runs it)
            var sb = Row(y, 70, Num(key) + smart.label, smart.act, 27); sb.Idle = Pal.A(new Color(0.3f, 0.09f, 0.1f), 0.95f); sb.Paint();
            _numKeys[key++] = smart.act; _enterAct = smart.act; y += 80;
            // 2–5: chips
            int[] mins = { 15, 30, 60, 180 }; string[] names = { "15분", "30분", "1시간", "3시간" };
            _chipBtns = new UIKit.Btn[4]; _chipActs = new Action[4];
            float cw = (1100 - 120 - 3 * 14) / 4f;
            for (int i = 0; i < 4; i++)
            {
                int mm = mins[i]; Action a = () => DoWait(mm);
                float x0 = 60 + i * (cw + 14);
                _chipBtns[i] = UIKit.Button(_panel, Num(key) + names[i], a, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x0, -(y + 58)), new Vector2(x0 + cw, -y), 24);
                _chipActs[i] = a; _numKeys[key++] = a;
            }
            y += 70;
            // "~까지"
            foreach (var u in until) { Row(y, 52, (key <= 9 ? Num(key) : "") + u.label, u.act, 23); if (key <= 9) _numKeys[key] = u.act; key++; y += 60; }
            // things to do here (pastimes: 책 읽기 30분…)
            if (here.Count > 0)
            {
                if (!expanded) { Action a = () => OpenWait(true); Row(y, 52, (key <= 9 ? Num(key) : "") + $"여기서 할 일 ({here.Count}) " + Dim("— 펼치기"), a, 23); if (key <= 9) _numKeys[key] = a; key++; y += 60; }   // (plain words: the font has no ▸)
                else foreach (var (f, act) in here)
                    {
                        var ff = f; var aa = act; Action a = () => { Close(); _s.TimeDir?.StartPastime(ff, aa); };
                        string fk = FurnitureCatalog.Get(f.Type)?.Kor ?? f.Type; string eff = Effect(act.Id);
                        Row(y, 52, (key <= 9 ? Num(key) : "") + $"{act.Label} " + Dim($"— {fk} · {Mathf.RoundToInt((float)act.Minutes)}분{(eff != null ? " · " + eff : "")}"), a, 23);
                        if (key <= 9) _numKeys[key] = a; key++; y += 60;
                    }
            }
            if (sleepRow)
            {
                bool inRoom = me != null && S.Layout.BedroomOf(Cast.Player)?.Id == me.Room;
                Action a = () => { Close(); _s.TimeDir?.StartSleep(true); };
                Row(y, 52, (key <= 9 ? Num(key) : "") + (inRoom ? "잠자리에 든다" : "방으로 돌아가 잔다") + " " + Dim("— 아침 종(오전 7:00)까지"), a, 23);
                if (key <= 9) _numKeys[key] = a; key++; y += 60;
            }
            // the quiet toggle
            bool quiet = TimeDirector.QuietPref;
            UIKit.Button(_panel, $"작은 일로는 멈추지 않기: <color=#D6AD62>{(quiet ? "켬" : "끔")}</color> " + Dim("— 비명이나 약속 같은 일에만 멈춘다"), () => { TimeDirector.QuietPref = !TimeDirector.QuietPref; OpenWait(expanded); },
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -(y + 8 + 46)), new Vector2(-60, -(y + 8)), 20);
            UIKit.Text(_panel, "Note", "<color=#9A8E7C>비명, 노크, 말을 걸어오는 사람, 약속, 식사 종이 있으면 저절로 멈춘다. Esc나 이동 키로 언제든 멈출 수 있다.\n1–9 고르기 · ←/→ 시간 고르기 · T나 Enter 맨 위 줄</color>", 19, Pal.TextDim, TextAlignmentOptions.Bottom, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(30, 20), new Vector2(-30, 0));
        }

        /// <summary>The top row: continue an interrupted wait here, go to bed at night, or wait for the next thing that happens.</summary>
        (string label, Action act, double at, bool sleep) SmartRow(bool night)
        {
            var td = _s.TimeDir; var me = S.Player;
            var li = td?.LastInterrupted;
            if (li != null && li.Target > S.Clock + 1 && me != null && td.LastInterruptedAt.f == me.Pos.f && td.LastInterruptedAt.DistXZ(me.Pos) <= 3f)
            {
                var lp = li; double at = li.Target;
                return ($"계속 기다리기 — {TimeUI.Clock12(at)}까지 " + Dim($"({TimeUI.Span(at - S.Clock)} 남음)"), () =>
                {
                    // someone came to talk and was not answered: they go back to what they were doing
                    var st = td.LastResult?.Stop; if (st != null && st.Kind == StopKind.Approach && st.Actor != null) { var who = S.A(st.Actor); if (who != null) { try { _s.Sim.DismissApproach(who); } catch (Exception e) { Debug.LogException(e); } } }
                    SkipPlan p = null; try { p = _s.Sim.ResumePlan(lp); } catch (Exception e) { Debug.LogException(e); } RunPlan(p, null);
                }, at, false);
            }
            // an appointment that is due now, somewhere else: named first — waiting here would only miss it (the next thing moves down)
            _smartIsDue = false;
            UpcomingEvent due = null; try { due = _s.Sim.DueAppointment(); } catch (Exception e) { Debug.LogException(e); }
            if (due != null)
            {
                _smartIsDue = true;
                string who = due.Actor != null ? Cast.GivenOf(due.Actor) : "누군가", room = S.RoomName(due.Room);
                var rq = AppointmentOf(due); string when = rq != null && rq.At > S.Clock + 0.5 ? TimeUI.Clock12(rq.At) : "지금";
                string label = LineBank.FixParticles($"약속 시간이다 — {who}이(가) {room}에서 기다린다 ({when}) ") + Dim(LineBank.FixParticles($"{room}(으)로 가면 만난다"));
                return (label, () => { Close(); Hud.I?.Toast(LineBank.FixParticles($"{room}(으)로 가자 — {who}이(가) 기다린다"), Pal.Gold, 3f); }, S.Clock, false);
            }
            if (night)
            {
                bool inRoom = me != null && S.Layout.BedroomOf(Cast.Player)?.Id == me.Room;
                string when = S.Minute >= 20 * 60 ? "내일 아침 종(오전 7:00)까지" : "아침 종(오전 7:00)까지";
                return ((inRoom ? "잠자리에 든다" : "방으로 돌아가 잔다") + " — " + when, () => { Close(); _s.TimeDir?.StartSleep(true); }, -1, true);
            }
            UpcomingEvent ev = null; try { ev = _s.Sim.NextEvent(S.Clock); } catch (Exception e) { Debug.LogException(e); }
            if (ev == null || ev.At <= S.Clock + 0.5) ev = FallbackTargets().FirstOrDefault();
            if (ev != null)
            {
                var e2 = ev; double at = WaitTarget(ev), shown = ShownAt(ev);
                return ($"다음 일까지 — {EvText(ev)} · {TimeUI.Clock12(shown)} " + Dim($"({TimeUI.Span(shown - S.Clock)} 뒤)"), () => RunUntil(at, IsAppointment(e2) ? "약속" : IsGathering(e2) ? "모임" : e2.Text), ev.At, false);
            }
            return ("1시간 기다리기", () => DoWait(60), S.Clock + 60, false);
        }

        static bool IsAppointment(UpcomingEvent e) => e != null && e.Kind != null && (e.Kind.Contains("appoint") || e.Kind.Contains("invite") || e.Kind.Contains("request"));
        static bool IsGathering(UpcomingEvent e) => e != null && e.Kind != null && e.Kind.Contains("gather");

        /// <summary>The accepted invitation an appointment row stands for.</summary>
        Request AppointmentOf(UpcomingEvent e) => S.Requests?.Where(r => r.Kind == "invite" && r.State == "accepted" && (e.Actor == null || r.From == e.Actor) && (e.Room < 0 || r.Room == e.Room)).OrderBy(r => r.At).FirstOrDefault();
        /// <summary>The time shown for an event (an appointment: the appointment itself, not the moment to set out).</summary>
        double ShownAt(UpcomingEvent e) { if (IsAppointment(e)) { var r = AppointmentOf(e); if (r != null) return r.At; } return e.At; }
        /// <summary>"약속 — 지아 · 서재" reads "약속 · 지아 · 서재" inside a row that already has a dash.</summary>
        static string EvText(UpcomingEvent e) => (e?.Text ?? "").Replace(" — ", " · ");

        /// <summary>Where a wait for this event ends: an appointment in its room waits until they come (the kernel stops when
        /// they arrive, at most 25 minutes past), elsewhere a few minutes early to walk there (the kernel's time); a gathering 5
        /// minutes early.</summary>
        double WaitTarget(UpcomingEvent e)
        {
            if (IsAppointment(e))
            {
                var r = AppointmentOf(e); bool here = S.Player != null && e.Room >= 0 && S.Player.Room == e.Room;
                if (here && r != null) return Math.Max(S.Clock + 1, r.At + 25);
                return Math.Max(S.Clock + 1, e.At);
            }
            if (IsGathering(e)) return Math.Max(S.Clock + 1, e.At - 5);
            return e.At;
        }

        /// <summary>Up to three "~까지" rows (the kernel's list; the house's own timetable when it has none).</summary>
        System.Collections.Generic.List<(string label, Action act)> UntilRows(double smartAt)
        {
            var l = new System.Collections.Generic.List<(string, Action)>();
            System.Collections.Generic.List<UpcomingEvent> evs = null;
            try { evs = _s.Sim.UntilTargets(); } catch (Exception e) { Debug.LogException(e); }
            if (evs == null || evs.Count == 0) evs = FallbackTargets();
            if (_smartIsDue && S.Minute >= 6 * 60 + 30 && S.Minute < 20 * 60)
            {
                // the due appointment took the top row: the next thing is still one press away
                UpcomingEvent nx = null; try { nx = _s.Sim.NextEvent(S.Clock); } catch (Exception e) { Debug.LogException(e); }
                if (nx != null && nx.At > S.Clock + 0.5 && !IsAppointment(nx))
                {
                    var ev0 = nx; double at0 = WaitTarget(nx);
                    l.Add((LineBank.FixParticles($"다음 일까지 — {EvText(nx)} · {TimeUI.Clock12(nx.At)} ") + Dim($"({TimeUI.Span(nx.At - S.Clock)} 뒤)"), () => RunUntil(at0, ev0.Text)));
                    smartAt = nx.At;
                }
            }
            foreach (var e in evs.Where(x => x != null && x.At > S.Clock + 0.5).OrderBy(x => x.At))
            {
                if (Math.Abs(e.At - smartAt) < 0.5 && !IsAppointment(e)) continue;   // already the top row
                if (l.Count >= 3) break;
                var ev = e; double at = WaitTarget(e), shown = ShownAt(e); string label;
                if (IsAppointment(e))
                {
                    bool there = S.Player != null && S.Player.Room == e.Room;
                    string who = e.Actor != null ? Cast.GivenOf(e.Actor) : "";
                    label = $"약속까지 — {who}{(who.Length > 0 ? " · " : "")}{S.RoomName(e.Room)} · {TimeUI.Clock12(shown)} " + Dim($"({TimeUI.Span(shown - S.Clock)} 뒤) · " + (there ? "여기서 올 때까지 기다린다" : "조금 일찍 멈춘다 · 그 방에 가 있으면 올 때까지 기다린다"));
                }
                else if (IsGathering(e)) label = $"모임까지 — {EvText(e).Replace("모임 · ", "")} · {TimeUI.Clock12(e.At)}";
                else label = $"{e.Text}까지 — {TimeUI.Clock12(e.At)} " + Dim($"({TimeUI.Span(e.At - S.Clock)} 뒤)");
                l.Add((LineBank.FixParticles(label), () => RunUntil(at, IsAppointment(ev) ? "약속" : IsGathering(ev) ? "모임" : ev.Text)));
            }
            return l;
        }

        /// <summary>The house's own timetable when the kernel offers none: meals, an accepted appointment, a known gathering,
        /// evening and night.</summary>
        System.Collections.Generic.List<UpcomingEvent> FallbackTargets()
        {
            var l = new System.Collections.Generic.List<UpcomingEvent>(); double now = S.Clock, day0 = Math.Floor(now / 1440.0) * 1440.0;
            void At(double minOfDay, string text, string kind) { double t = day0 + minOfDay; if (t <= now + 0.5) t += 1440; l.Add(new UpcomingEvent { At = t, Text = text, Kind = kind }); }
            At(8 * 60, "아침 식사 종", "meal"); At(12 * 60 + 30, "점심 시간", "meal"); At(18 * 60 + 30, "저녁 식사 종", "meal");
            At(18 * 60, "저녁", "time"); At(22 * 60, "밤 10시 종", "time");
            var rq = S.Requests?.Where(r => r.Kind == "invite" && r.State == "accepted" && r.At + 25 > now).OrderBy(r => r.At).FirstOrDefault();
            if (rq != null) l.Add(new UpcomingEvent { At = S.Player != null && S.Player.Room == rq.Room ? rq.At : rq.At - 5, Text = "약속", Kind = "appointment", Actor = rq.From, Room = rq.Room });
            if (S.Gatherings != null)
                foreach (var g in S.Gatherings)
                {
                    if (g.Done || g.Cancelled || !g.KnownRev.TryGetValue(Cast.Player, out var kr) || kr < 0) continue;
                    var R = g.Revs[kr]; if (R.Start <= now) continue;
                    l.Add(new UpcomingEvent { At = R.Start, Text = LineBank.FixParticles($"{Cast.GivenOf(g.Host)}의 {g.Label}"), Kind = "gathering", Actor = g.Host, Room = R.Room });
                }
            return l.Where(e => e.At - now <= 16 * 60).OrderBy(e => e.At).ToList();
        }

        /// <summary>Pastimes in this room (things that take time: 책 읽기 30분, 연주하기 25분…), one per kind, nearest first.</summary>
        System.Collections.Generic.List<(Furniture f, PlayerAction act)> Pastimes()
        {
            var l = new System.Collections.Generic.List<(Furniture, PlayerAction)>(); var me = S.Player; var room = S.Layout.Room(me?.Room ?? -1);
            if (room == null) return l;
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var f in room.Furniture.Select(id => S.Layout.Furniture[id]).OrderBy(f => f.Pos.DistXZ(me.Pos)))
            {
                System.Collections.Generic.List<PlayerAction> acts = null; try { acts = _s.Sim.FurnitureActions(f); } catch (Exception) { }
                if (acts == null) continue;
                foreach (var a in acts)
                {
                    if (a.Minutes <= 0 || a.Id == "sit" || a.Id == "search") continue;
                    if (!seen.Add(a.Id)) continue;
                    l.Add((f, a)); if (l.Count >= 6) return l;
                }
            }
            return l;
        }

        static string Effect(string id)
        {
            switch (id)
            {
                case "read": case "listen_long": return "마음이 편해진다";
                case "play_music": case "game": return "기분이 좋아진다";
                case "chess": return "머리를 쓴다";
                case "cook": return "먹을 것을 만든다";
                case "garden": return "꽃이 필지도 모른다";
                case "pray": return "마음을 추스른다";
                case "perform": return "누가 보러 올지도 모른다";
                case "swim": case "exercise": return "몸이 풀린다";
                case "craft": return "뭔가 만들어진다";
                case "drink": return "한 잔 챙긴다";
                case "film": return "옛 필름을 본다";
                case "nap": return "기운이 조금 돈다";
                case "wash": return "옷이 깨끗해진다";
                case "eat": return "배가 부르다";
            }
            return null;
        }

        void RunPlan(SkipPlan p, string why)
        {
            Close();
            if (p == null) { Hud.I?.Toast(why ?? "지금은 기다릴 수 없다", Pal.TextDim, 2f); return; }
            p.Quiet = p.Quiet || TimeDirector.QuietPref;
            if (!(_s.TimeDir?.Start(p) ?? false)) Hud.I?.Toast("지금은 시간을 보낼 수 없다", Pal.TextDim, 2f);
        }
        void RunUntil(double at, string label)
        {
            SkipPlan p = null; string why = null;
            try { p = _s.Sim.PlanUntil(at, label, out why); } catch (Exception e) { Debug.LogException(e); }
            RunPlan(p, why);
        }

        void OpenWaitInvestigation()
        {
            Begin("시간 보내기 — 수사 중", 900, 660); int i = 0; ClearKeys();
            int leftMin = Mathf.Max(0, Mathf.CeilToInt((float)(S.Ch.InvestigationEnd - S.Clock)));
            UIKit.Text(_panel, "Sub", $"남은 수사 시간 {leftMin}분 · {TimeUI.Clock12(S.Clock)}", 22, Pal.A(Pal.TextDim, 0.95f), TextAlignmentOptions.Top, Fonts.Serif, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -118), new Vector2(-40, -84));
            Action wait10 = () => DoWait(10);
            Btn(i++, Num(1) + "10분 기다리기", wait10, 140); _numKeys[1] = wait10; _enterAct = wait10;
            bool can = _s.Sim.CanEndInvestigation(out var why, out var left);
            int firm = 0; try { firm = CaseBoard.FirmCount(_s.Sim); } catch (Exception) { }
            string label = $"수사를 마치고 심판으로 <size=72%><color=#9A8E7C>(약 {Mathf.CeilToInt((float)left)}분 남음)</color></size>";
            Action end = () =>
                Confirm("수사 마치기", $"사건 개요 {firm}/4 — 정말 수사를 마칠까?\n<size=85%><color=#9A8E7C>남은 수사 시간은 버려지고, 모두 심판장으로 모인다.</color></size>", "마치기", () =>
                {
                    bool ok = _s.Sim.PlayerEndInvestigation(); Close();
                    Hud.I?.Toast(ok ? "수사를 마쳤다 — 곧 심판이 열린다" : "지금은 마칠 수 없다", ok ? Pal.Gold : Pal.TextDim, 3f);
                });
            var b = Btn(i++, Num(2) + (can ? label : $"수사를 마치고 심판으로 <size=72%><color=#9A8E7C>({why})</color></size>"), end, 140);
            if (!can) { b.Interactable = false; b.Paint(); } else _numKeys[2] = end;
            Btn(i++, "취소", Close, 140);
            UIKit.Text(_panel, "Note", "<color=#9A8E7C>수사 중에는 시간이 계속 흐른다. 수사 시간이 끝나면 저절로 심판이 열린다. 기다리는 도중에도 Esc나 이동 키로 멈출 수 있다.</color>", 20, Pal.TextDim, TextAlignmentOptions.Bottom, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(30, 24), new Vector2(-30, 0));
        }

        void DoWait(double minutes)
        {
            Close();
            _s.Cine.Wait(minutes);
        }

        /// <summary>Probe: the label of wait-menu row k (1 = the top row), or null.</summary>
        public string ProbeRowLabel(int k)
        {
            if (!Open || !_numKeys.TryGetValue(k, out var a)) return null;
            foreach (var b in _panel.GetComponentsInChildren<UIKit.Btn>(true)) if (b.OnClick == a && b.Label != null) return b.Label.text;
            return "";
        }
        /// <summary>Probe: press row k of the wait menu (as the number key would).</summary>
        public bool ProbeRow(int k) { if (!Open || !_numKeys.TryGetValue(k, out var a)) return false; a(); return true; }
        /// <summary>Probe: answer yes on an open confirmation (as Enter would).</summary>
        public bool ProbeConfirm() { if (_confirm == null) return false; var y = _confirmYes; CloseConfirm(); y?.Invoke(); return true; }
        // --- time-on-demand (end)

        // ------------------------------------------------------------------ facilities
        public void OpenSwitchboard(Furniture f)
        {
            Begin("배전반", 900, 820); int i = 0;
            foreach (var c in S.Layout.Circuits)
            {
                var cc = c; bool on = S.CircuitOn(c.Id);
                Btn(i++, $"{(on ? "<color=#9CC4B2>● 켜짐</color>" : "<color=#C04A55>○ 꺼짐</color>")}  {c.Name}{(c.Emergency ? " (비상등은 켜진 채)" : "")}", () => { var r = _s.Sim.PlayerOperate(f, cc.Id); Hud.I?.Toast(r, Pal.Gold); OpenSwitchboard(f); });
            }
            UIKit.Text(_panel, "Note", "<color=#9A8E7C>레버를 움직이면 흔적이 남고, 그 구역에 있던 사람들은 불이 꺼진 것을 알게 된다.</color>", 19, Pal.TextDim, TextAlignmentOptions.Bottom, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(30, 24), new Vector2(-30, 0));
        }

        public void OpenPressConsole(Furniture f)
        {
            Begin("프레스 조작부", 800, 520); int i = 0;
            Btn(i++, "상태 보기", () => { var ev = _s.Sim.PlayerExamine(f); Close(); if (ev != null) Hud.I?.Card(ev); });
            if (S.PressFireAt >= 0) Btn(i++, "<color=#C04A55>예약된 작동 취소</color>", () => { Hud.I?.Toast(_s.Sim.PlayerOperate(f, 2), Pal.Gold); Close(); });
            Btn(i++, "닫기", Close);
        }
    }

    public sealed class SliderDrag : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IDragHandler
    {
        public RectTransform Fill; public Action<float> OnChange;
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) => Set(e);
        public void OnDrag(UnityEngine.EventSystems.PointerEventData e) => Set(e);
        void Set(UnityEngine.EventSystems.PointerEventData e)
        {
            var rt = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, null, out var lp))
            { float v = Mathf.Clamp01((lp.x - rt.rect.xMin) / rt.rect.width); Fill.anchorMax = new Vector2(v, 1); OnChange?.Invoke(v); }
        }
    }
}
