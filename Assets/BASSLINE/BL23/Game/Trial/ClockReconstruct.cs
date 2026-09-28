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
    /// 시계와 평면도 — the climax. Turn the hands of the great clock to the hour of the attack, place the culprit's pewter figure
    /// in the room on the real floor plan of the mansion, and set how it was done (weapon, the staged trick, what became of the
    /// weapon). "대조" checks the reconstruction against what is PUBLIC (TrialGames.ReconstructCheck — never the hidden truth);
    /// "제시" presents it (TrialGames.ReconstructSubmit) and the night is replayed step by step on the board.
    /// </summary>
    public sealed class ClockReconstruct : TrialMinigame
    {
        TrialSystem.RQ _who, _when, _where, _weapon, _moved, _trick, _conceal;
        readonly Dictionary<string, int> _ans = new Dictionary<string, int>();
        RectTransform _clock, _hourHand, _minHand, _map, _mapRooms, _token, _victimTok, _whoStrip; TextMeshProUGUI _timeText, _msg; int _floor;
        readonly Dictionary<int, RectTransform> _roomRts = new Dictionary<int, RectTransform>();
        readonly Dictionary<string, (RectTransform rt, TextMeshProUGUI val)> _sel = new Dictionary<string, (RectTransform, TextMeshProUGUI)>();
        readonly Dictionary<string, RectTransform> _marks = new Dictionary<string, RectTransform>();
        TextMeshProUGUI _whoLabel; RectTransform _checkBtn, _submitBtn; bool _submit, _dragClock; Dictionary<string, string> _check; int _foundRoom = -1; string _victim; float _prevAngle;

        public ClockReconstruct(TrialDirectorUI h) : base(h) { }

        TrialSystem.RQ Q(string id) => T.RQ.FirstOrDefault(q => q.Id == id);

        public override IEnumerator Run()
        {
            MusicDirector.I?.SetState(MusicState.TrialClimax);
            _who = Q("who"); _when = Q("when"); _where = Q("where"); _weapon = Q("weapon"); _moved = Q("moved"); _trick = Q("trick"); _conceal = Q("conceal");
            var inc = TrialSystem.TargetIncident(S); _foundRoom = inc?.FoundRoom ?? -1; _victim = inc?.Victim;
            H.CamWide(40);
            yield return Intro("시계와 평면도", "그날 있었던 일을 처음부터 되짚어라", "시계를 돌리고, 평면도에 사람을 놓고, 범인이 꾸민 속임수를 짚어라");
            yield return Teach("시계와 평면도", new[]
            {
                "시계 판을 끌거나 휠을 굴려 범행 시각을 맞춘다.",
                "평면도에서 범행 장소를, 아래 초상에서 범인을 고른다. 왼쪽에서 흉기와 속임수를 고른다.",
                "'대조'를 누르면 밝혀진 사실과 어긋나는 칸을 알려 준다. 준비되면 '재구성 내놓기'.",
            }, ArtClock);
            Goth.Panel(Root, "Column", new Vector2(0, 0), new Vector2(0, 1), new Vector2(26, 60), new Vector2(404, -140));
            Goth.Panel(Root, "WhoBed", new Vector2(0, 0), new Vector2(1, 0), new Vector2(424, 164), new Vector2(-34, 250));
            Header("시계와 평면도", "사건 재구성");
            if (_when != null) _ans["when"] = _when.Options.Count / 2;
            BuildClock(); BuildMap(); BuildWho(); BuildSelectors(); BuildButtons();
            _msg = Goth.Text(Root, "Msg", "시각·장소·범인·방법을 정한 뒤 '대조'로 밝혀진 사실과 견주어 보고, '재구성 내놓기'로 법정에 내놓는다.", 20, GPal.Bone, TextAlignmentOptions.TopLeft, false, new Vector2(0.36f, 1), new Vector2(1, 1), new Vector2(0, -150), new Vector2(-40, -104));
            HintLine("드래그 / 휠|시계 돌리기 · 클릭|장소와 범인 고르기 · 〈 〉|방법 고르기 · Enter|내놓기");
            RefreshAll();
            if (Probe) H.StartCoroutine(ProbeDemo());
            while (!_submit) { Tick(); EndVirtFrame(); yield return null; }
            // moved follows from the placement: attacked somewhere else than where the body was found
            if (_moved != null && _ans.TryGetValue("where", out var wi) && wi >= 0 && _where != null) _ans["moved"] = _where.Options[wi] == _foundRoom.ToString() ? 0 : 1;
            var story = TrialGames.ReconstructSubmit(Sim, new Dictionary<string, int>(_ans));
            yield return Replay(story);
            Done = true;
        }

        // ------------------------------------------------------------ the great clock
        void BuildClock()
        {
            _clock = UIKit.Rect(Root, "Clock", new Vector2(0, 1), new Vector2(0, 1)); _clock.pivot = new Vector2(0.5f, 1); _clock.sizeDelta = new Vector2(320, 320); _clock.anchoredPosition = new Vector2(215, -168);
            Goth.Glow(_clock, "Glow", GPal.A(GPal.Candle, 0.18f), new Vector2(560, 560), Vector2.zero);
            var face = TrialFx.Centered(_clock, "Face", new Vector2(320, 320)); var fg = face.gameObject.AddComponent<RingGraphic>(); fg.Thickness = 160; fg.color = GPal.A(GPal.Bone, 0.95f); fg.raycastTarget = false;
            var ring = TrialFx.Centered(_clock, "Ring", new Vector2(340, 340)); var rg = ring.gameObject.AddComponent<RingGraphic>(); rg.Thickness = 14; rg.color = GPal.Brass; rg.raycastTarget = false;
            var ring2 = TrialFx.Centered(_clock, "Ring2", new Vector2(296, 296)); var rg2 = ring2.gameObject.AddComponent<RingGraphic>(); rg2.Thickness = 2; rg2.color = GPal.A(GPal.Ink, 0.6f); rg2.raycastTarget = false;
            string[] rom = { "XII", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI" };
            for (int i = 0; i < 12; i++)
            {
                float a = (90 - i * 30) * Mathf.Deg2Rad; var t = Goth.Text(_clock, "N" + i, rom[i], 24, GPal.Ink, TextAlignmentOptions.Center, true, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                t.rectTransform.sizeDelta = new Vector2(60, 30); t.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 122; t.textWrappingMode = TextWrappingModes.NoWrap;
            }
            _hourHand = Hand("Hour", 84, 9, GPal.Ink); _minHand = Hand("Min", 126, 5, GPal.Ink);
            var cap = TrialFx.Centered(_clock, "Cap", new Vector2(22, 22)); var cg = cap.gameObject.AddComponent<RingGraphic>(); cg.Thickness = 11; cg.color = GPal.Brass; cg.raycastTarget = false;
            _timeText = Goth.Text(_clock, "Time", "", 20, GPal.Gilt, TextAlignmentOptions.Top, true, new Vector2(-0.12f, 0), new Vector2(1.12f, 0), new Vector2(0, -70), new Vector2(0, -10));
            _timeText.textWrappingMode = TextWrappingModes.Normal;
            Mark("when", _clock, new Vector2(150, -150));
        }
        RectTransform Hand(string name, float len, float w, Color c)
        {
            var h = UIKit.Rect(_clock, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)); h.pivot = new Vector2(0.5f, 0.1f); h.sizeDelta = new Vector2(w, len);
            var im = h.gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = false; return h;
        }
        double WhenValue() => _when != null && _ans.TryGetValue("when", out var i) && i >= 0 && i < _when.Options.Count ? double.Parse(_when.Options[i], System.Globalization.CultureInfo.InvariantCulture) : 0;
        void PaintClock(double t)
        {
            float m = (float)(t % 60), hh = (float)(t % 720) / 60f;
            _minHand.localRotation = Quaternion.Euler(0, 0, -m * 6); _hourHand.localRotation = Quaternion.Euler(0, 0, -hh * 30);
            _timeText.text = t > 0 ? ClockFmt.Vague(t) : "";
        }

        // ------------------------------------------------------------ the floor plan
        void BuildMap()
        {
            _map = UIKit.Rect(Root, "Map", new Vector2(0, 0), new Vector2(1, 1), new Vector2(424, 266), new Vector2(-34, -160));
            UIKit.Img(_map, "Shade", GPal.A(Color.black, 0.5f), Vector2.zero, Vector2.one, new Vector2(10, -12), new Vector2(10, -12));
            Goth.Parchment(_map, "Paper", Vector2.zero, Vector2.one);
            var fr = Goth.Frame(_map, "Rule", Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10), false, false); fr.Border = GPal.A(GPal.Ink, 0.5f); fr.Width = 1.5f;
            _floor = _foundRoom >= 0 ? S.Layout.Room(_foundRoom)?.Floor ?? 0 : 0;
            var floors = S.Layout.Rooms.Where(r => r.Type != RoomType.Courtroom && r.Type != RoomType.Elevator).Select(r => r.Floor).Distinct().OrderBy(f => f).ToList();
            for (int i = 0; i < floors.Count; i++)
            {
                int f = floors[i]; var tab = UIKit.Rect(_map, "Tab" + f, new Vector2(0, 1), new Vector2(0, 1)); tab.pivot = new Vector2(0, 1); tab.sizeDelta = new Vector2(92, 36); tab.anchoredPosition = new Vector2(20 + i * 98, -16);
                var tf = Goth.Frame(tab, "F", Vector2.zero, Vector2.one); tf.Fill = GPal.A(GPal.OakDark, 0.95f); tf.FillBottom = GPal.A(GPal.Lacquer, 0.95f); tf.Border = GPal.Brass; tf.Corners = false; tf.Gap = 3;
                Goth.Text(tab, "T", f < 0 ? $"지하 {-f}층" : $"{f + 1}층", 18, GPal.Bone, TextAlignmentOptions.Center, true);
            }
            _mapRooms = UIKit.Rect(_map, "Rooms", Vector2.zero, Vector2.one, new Vector2(24, 24), new Vector2(-24, -62));
            DrawFloor();
            Mark("where", _map, new Vector2(-30, -20), new Vector2(1, 1));
        }

        void DrawFloor()
        {
            UIKit.Clear(_mapRooms); _roomRts.Clear(); _token = null; _victimTok = null;
            var rooms = S.Layout.Rooms.Where(r => r.Floor == _floor && r.Type != RoomType.Courtroom && r.Type != RoomType.Elevator).ToList(); if (rooms.Count == 0) return;
            float minX = rooms.Min(r => r.Rect.x0), maxX = rooms.Max(r => r.Rect.x1), minZ = rooms.Min(r => r.Rect.z0), maxZ = rooms.Max(r => r.Rect.z1);
            var pr = _mapRooms.rect; float sc = Mathf.Min(pr.width / Mathf.Max(1, maxX - minX), pr.height / Mathf.Max(1, maxZ - minZ));
            float ox = (pr.width - (maxX - minX) * sc) * 0.5f, oy = (pr.height - (maxZ - minZ) * sc) * 0.5f;
            foreach (var r in rooms.OrderBy(r => RoomInfo.IsPassage(r.Type) ? 0 : 1))
            {
                bool pass = RoomInfo.IsPassage(r.Type);
                var rt = UIKit.Rect(_mapRooms, "R" + r.Id, Vector2.zero, Vector2.zero); rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(ox + (r.Rect.x0 - minX) * sc, oy + (r.Rect.z0 - minZ) * sc); rt.sizeDelta = new Vector2(r.Rect.W * sc, r.Rect.D * sc);
                UIKit.Img(rt, "Fill", pass ? GPal.A(GPal.ParchDark, 0.35f) : GPal.A(GPal.Parchment, 0.4f), Vector2.zero, Vector2.one);
                var f = Goth.Frame(rt, "Ink", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, false); f.Border = GPal.A(GPal.Ink, pass ? 0.35f : 0.8f); f.Width = pass ? 1f : 2f; f.Gap = 0; f.Corners = false;
                if (!pass && rt.sizeDelta.x > 40 && rt.sizeDelta.y > 22) { var t = Goth.Text(rt, "N", S.RoomName(r.Id), 15, GPal.A(GPal.Ink, 0.85f), TextAlignmentOptions.Center, false, Vector2.zero, Vector2.one, new Vector2(3, 2), new Vector2(-3, -2)); t.enableAutoSizing = true; t.fontSizeMin = 9; t.fontSizeMax = 15; }
                if (r.Id == _foundRoom) { var m = UIKit.Img(rt, "Found", GPal.A(GPal.InkRed, 0.18f), Vector2.zero, Vector2.one); var ft = Goth.Text(rt, "F", "발견", 14, GPal.InkRed, TextAlignmentOptions.TopRight, true, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-4, -2)); }
                _roomRts[r.Id] = rt;
            }
            PlaceTokens();
        }

        void PlaceTokens()
        {
            if (_token != null) Object.Destroy(_token.gameObject); if (_victimTok != null) Object.Destroy(_victimTok.gameObject); _token = _victimTok = null;
            if (_victim != null && _roomRts.TryGetValue(_foundRoom, out var fr)) { _victimTok = Token(_victim, fr, true); }
            int wi = _ans.TryGetValue("where", out var w) ? w : -1;
            if (wi >= 0 && _where != null && int.TryParse(_where.Options[wi], out var room) && _roomRts.TryGetValue(room, out var rr))
            {
                string culprit = _ans.TryGetValue("who", out var ci) && ci >= 0 && _who != null ? _who.Options[ci] : null;
                _token = Token(culprit, rr, false);
            }
        }

        RectTransform Token(string id, RectTransform room, bool victim)
        {
            var m = Goth.Medallion(_mapRooms, victim ? "Victim" : "Culprit", id ?? "", new Vector2(56, 56), Vector2.zero);
            m.anchorMin = m.anchorMax = Vector2.zero; m.anchoredPosition = room.anchoredPosition + room.sizeDelta * 0.5f + (victim ? new Vector2(14, -8) : new Vector2(-14, 8));
            m.GetComponent<RingGraphic>().color = victim ? GPal.Smoke : GPal.Brass;
            if (id == null) Goth.Text(m, "Q", "?", 30, GPal.Brass, TextAlignmentOptions.Center, true);
            return m;
        }

        // ------------------------------------------------------------ culprit strip & selectors
        void BuildWho()
        {
            _whoStrip = UIKit.Rect(Root, "Who", new Vector2(0, 0), new Vector2(1, 0), new Vector2(424, 164), new Vector2(-34, 250));
            _whoLabel = Goth.Text(_whoStrip, "L", "범인", 20, GPal.Gilt, TextAlignmentOptions.TopLeft, true, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -34), new Vector2(0, -8)); _whoLabel.characterSpacing = 3;
            if (_who == null) return; int n = _who.Options.Count; float w = Mathf.Min(78, (_whoStrip.rect.width - 80) / Mathf.Max(1, n));
            for (int i = 0; i < n; i++)
            {
                var id = _who.Options[i]; var m = Goth.Medallion(_whoStrip, "W" + i, id, new Vector2(w - 8, w - 8), Vector2.zero); m.anchorMin = m.anchorMax = new Vector2(0, 0.5f); m.anchoredPosition = new Vector2(20 + i * w + w * 0.5f, -12);
                if (T.Dead.Contains(id)) { var dd = Goth.Text(m, "D", "†", 22, GPal.InkRed, TextAlignmentOptions.TopRight, true); }
            }
            Mark("who", _whoStrip, new Vector2(40, 30), new Vector2(0, 1));
        }

        void BuildSelectors()
        {
            var box = UIKit.Rect(Root, "Sel", new Vector2(0, 0), new Vector2(0, 0)); box.pivot = new Vector2(0, 0); box.sizeDelta = new Vector2(346, 300); box.anchoredPosition = new Vector2(42, 76);
            int k = 0;
            foreach (var (id, label) in new[] { ("weapon", "사인·흉기"), ("trick", "속임수"), ("conceal", "흉기의 행방") })
            {
                var q = Q(id); if (q == null) continue;
                var rt = UIKit.Rect(box, "S" + id, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -(k + 1) * 96 + 8), new Vector2(0, -k * 96)); k++;
                var f = Goth.Frame(rt, "F", Vector2.zero, Vector2.one); f.Fill = GPal.A(new Color(0.09f, 0.06f, 0.04f), 1f); f.FillBottom = GPal.A(GPal.Lacquer, 1f); f.Border = GPal.A(GPal.Brass, 0.7f); f.Gap = 0; f.Corners = false;
                Goth.Text(rt, "L", label, 17, GPal.Brass, TextAlignmentOptions.TopLeft, true, Vector2.zero, Vector2.one, new Vector2(16, 6), new Vector2(-10, -6)).characterSpacing = 3;
                var v = Goth.Text(rt, "V", "—", 24, GPal.Bone, TextAlignmentOptions.Center, true, Vector2.zero, Vector2.one, new Vector2(44, 6), new Vector2(-44, -30)); v.enableAutoSizing = true; v.fontSizeMin = 14; v.fontSizeMax = 24;
                Goth.Text(rt, "Prev", "〈", 28, GPal.Gilt, TextAlignmentOptions.MidlineLeft, true, Vector2.zero, Vector2.one, new Vector2(10, -8), Vector2.zero);
                Goth.Text(rt, "Next", "〉", 28, GPal.Gilt, TextAlignmentOptions.MidlineRight, true, Vector2.zero, Vector2.one, new Vector2(0, -8), new Vector2(-10, 0));
                _sel[id] = (rt, v); Mark(id, rt, new Vector2(-10, -10), new Vector2(1, 1));
            }
        }

        void BuildButtons()
        {
            _checkBtn = Btn("대조 — 밝혀진 사실과 견주기", new Vector2(-354, 86), new Color(0.09f, 0.06f, 0.04f));
            _submitBtn = Btn("재구성 내놓기", new Vector2(-34, 86), GPal.Oxblood);
        }
        RectTransform Btn(string label, Vector2 pos, Color fill)
        {
            var rt = UIKit.Rect(Root, "B" + label, new Vector2(1, 0), new Vector2(1, 0)); rt.pivot = new Vector2(1, 0); rt.sizeDelta = new Vector2(306, 60); rt.anchoredPosition = pos;
            var f = Goth.Panel(rt, "F", Vector2.zero, Vector2.one); f.Fill = GPal.A(fill, 1f); f.FillBottom = GPal.A(GPal.Lacquer, 1f); f.Border = GPal.Gilt;
            Goth.Text(rt, "T", label, 22, GPal.Gilt, TextAlignmentOptions.Center, true).characterSpacing = 2; return rt;
        }

        void Mark(string id, RectTransform parent, Vector2 pos, Vector2? anchor = null)
        {
            var rt = UIKit.Rect(parent, "Mark_" + id, anchor ?? new Vector2(1, 0), anchor ?? new Vector2(1, 0)); rt.sizeDelta = new Vector2(52, 52); rt.anchoredPosition = pos;
            var s = rt.gameObject.AddComponent<SealGraphic>(); s.Wax = GPal.Wax; s.Seed = id.GetHashCode(); s.raycastTarget = false;
            Goth.Text(rt, "L", "", 24, GPal.Bone, TextAlignmentOptions.Center, true);
            rt.gameObject.SetActive(false); _marks[id] = rt;
        }

        void RefreshAll()
        {
            PaintClock(WhenValue());
            foreach (var kv in _sel) { var q = Q(kv.Key); int i = _ans.TryGetValue(kv.Key, out var a) ? a : -1; kv.Value.val.text = i >= 0 ? TrialGames.OptionLabel(S, q, i) : "— 고르지 않음 —"; }
            PlaceTokens();
            int wsel = _ans.TryGetValue("who", out var wv) ? wv : -1;
            if (_whoLabel != null) _whoLabel.text = wsel >= 0 && _who != null ? "범인 — " + Cast.NameOf(_who.Options[wsel]) : "범인 — 초상을 고른다";
            if (_who != null) for (int i = 0; i < _who.Options.Count; i++) { var w = _whoStrip.Find("W" + i) as RectTransform; if (w == null) continue; w.localScale = Vector3.one * (i == wsel ? 1.18f : 1f); var rg = w.GetComponent<RingGraphic>(); if (rg != null) { rg.color = i == wsel ? GPal.Gilt : GPal.BrassDim; rg.Refresh(); } }
            foreach (var kv in _marks)
            {
                string st = _check != null && _check.TryGetValue(kv.Key, out var s0) ? s0 : null;
                kv.Value.gameObject.SetActive(st == "ok" || st == "conflict");
                if (st == null) continue;
                var sg = kv.Value.GetComponent<SealGraphic>(); sg.Wax = st == "ok" ? GPal.BrassDim : GPal.Wax; sg.Refresh();
                kv.Value.GetComponentInChildren<TextMeshProUGUI>().text = st == "ok" ? "확" : "모";
            }
            if (_check != null && _check.TryGetValue("where", out var sw)) { if (_check.ContainsKey("moved") && _check["moved"] == "conflict") { _marks["where"].gameObject.SetActive(true); var sg = _marks["where"].GetComponent<SealGraphic>(); sg.Wax = GPal.Wax; sg.Refresh(); _marks["where"].GetComponentInChildren<TextMeshProUGUI>().text = "모"; } }
        }

        // ------------------------------------------------------------ input
        void Tick()
        {
            var m = MouseLocal();
            // hover: rooms, portraits, selectors and buttons answer with a small brass tick; buttons lean in
            object hov = null;
            foreach (var kv in _roomRts) if (Inside(kv.Value, m) && _where != null && _where.Options.Contains(kv.Key.ToString())) { hov = kv.Value; break; }
            if (hov == null && _who != null) for (int i = 0; i < _who.Options.Count; i++) { var w = _whoStrip.Find("W" + i) as RectTransform; if (w != null && Inside(w, m)) hov = w; }
            if (hov == null) foreach (var kv in _sel) if (Inside(kv.Value.rt, m)) hov = kv.Value.rt;
            foreach (var b in new[] { _checkBtn, _submitBtn }) { bool on = b != null && b.gameObject.activeSelf && Inside(b, m); if (on) hov = b; if (b != null) b.localScale = Vector3.Lerp(b.localScale, Vector3.one * (on ? 1.04f : 1f), Dt * 12); }
            HoverSfx(hov);
            // clock: drag the face (or scroll over it) to move the hands
            if (_when != null && _when.Options.Count > 0)
            {
                var lp = ToLocal(_clock, m); bool onFace = lp.magnitude < 170;
                float wheel = UseVirt ? 0 : Input.mouseScrollDelta.y;
                if (onFace && Mathf.Abs(wheel) > 0.1f) Step("when", wheel > 0 ? 1 : -1);
                if (onFace && MouseDown) { _dragClock = true; _prevAngle = Mathf.Atan2(lp.y, lp.x); }
                if (_dragClock && MouseHeld)
                {
                    float a = Mathf.Atan2(lp.y, lp.x); float d = Mathf.DeltaAngle(_prevAngle * Mathf.Rad2Deg, a * Mathf.Rad2Deg);
                    if (Mathf.Abs(d) >= 60f) { Step("when", d < 0 ? 1 : -1); _prevAngle = a; }   // 60° of the minute hand = ten minutes
                }
                if (!MouseHeld) _dragClock = false;
            }
            if (MouseDown)
            {
                foreach (var kv in _roomRts) if (Inside(kv.Value, m) && _where != null) { int i = _where.Options.IndexOf(kv.Key.ToString()); if (i >= 0) { _ans["where"] = i; TrialFx.Sound("quill", 0.6f); Dirty(); } break; }
                foreach (Transform tab in _map) if (tab.name.StartsWith("Tab") && Inside((RectTransform)tab, m)) { _floor = int.Parse(tab.name.Substring(3)); TrialFx.Sound("parchment", 0.6f); DrawFloor(); }
                if (_who != null) for (int i = 0; i < _who.Options.Count; i++) { var w = _whoStrip.Find("W" + i) as RectTransform; if (w != null && Inside(w, m)) { _ans["who"] = i; TrialFx.Sound("wax_stamp", 0.45f); H.CamCut(_who.Options[i], 0, 3f); Dirty(); } }
                foreach (var kv in _sel) if (Inside(kv.Value.rt, m)) { var lp = ToLocal(kv.Value.rt, m); Step(kv.Key, lp.x < kv.Value.rt.rect.center.x ? -1 : 1); }
                if (Inside(_checkBtn, m)) Check();
                if (Inside(_submitBtn, m)) { _submit = true; TrialFx.Sound("bell_toll", 0.7f); }
            }
            if (!UseVirt && Input.GetKeyDown(KeyCode.Return)) { _submit = true; TrialFx.Sound("bell_toll", 0.7f); }
        }

        void Step(string id, int d)
        {
            var q = Q(id); if (q == null || q.Options.Count == 0) return;
            int i = _ans.TryGetValue(id, out var a) ? a : (d > 0 ? -1 : 0); i = ((i + d) % q.Options.Count + q.Options.Count) % q.Options.Count; _ans[id] = i;
            TrialFx.Sound(id == "when" ? "clock_tick" : "plate_hover", 0.6f); Dirty();
        }

        void Dirty() { if (_check != null) { _check = null; } RefreshAll(); }

        void Check()
        {
            var a = new Dictionary<string, int>(_ans);
            if (_moved != null && a.TryGetValue("where", out var wi) && wi >= 0 && _where != null) a["moved"] = _where.Options[wi] == _foundRoom.ToString() ? 0 : 1;
            _check = TrialGames.ReconstructCheck(Sim, a);
            int bad = _check.Values.Count(v => v == "conflict"), ok = _check.Values.Count(v => v == "ok");
            _msg.text = bad > 0 ? $"<color=#E8826E>밝혀진 사실과 어긋나는 곳 {bad}군데</color> — 고쳐서 다시 대조하거나, 그대로 내놓는다." : ok > 0 ? $"어긋나는 곳이 없다 — {ok}군데는 밝혀진 사실이 뒷받침한다." : "밝혀진 사실만으로는 가릴 수 없다 — 나머지는 당신의 추리에 달렸다.";
            TrialFx.Sound(bad > 0 ? "organ_sting" : ok > 0 ? "choir_swell" : "quill", bad > 0 ? 0.6f : 0.5f); if (bad > 0) { H.ShakeCam(0.2f); H.EyesStare(0.6f); }
            RefreshAll();
            if (Probe) H.StartCoroutine(Later("trial_reconstruct_check", 0.4f));
        }

        // ------------------------------------------------------------ the night, replayed on the board
        IEnumerator Replay(List<string> story)
        {
            foreach (Transform t in Root) if (t.name.StartsWith("B") || t.name == "Hint" || t.name == "Keys") t.gameObject.SetActive(false);
            var cap = UIKit.Rect(Root, "Caption", new Vector2(0.24f, 0), new Vector2(0.98f, 0), new Vector2(0, 20), new Vector2(0, 150));
            Goth.Parchment(cap, "P", Vector2.zero, Vector2.one);
            var ct = Goth.Text(cap, "T", "", 30, GPal.Ink, TextAlignmentOptions.MidlineLeft, true, Vector2.zero, Vector2.one, new Vector2(34, 10), new Vector2(-30, -10)); ct.enableAutoSizing = true; ct.fontSizeMin = 18; ct.fontSizeMax = 30;
            string culprit = _ans.TryGetValue("who", out var ci) && ci >= 0 && _who != null ? _who.Options[ci] : null;
            var trick = _ans.TryGetValue("trick", out var ti) && ti >= 0 && _trick != null ? _trick.Options[ti] : null;
            for (int i = 0; i < story.Count; i++)
            {
                string line = story[i];
                switch (i)
                {
                    case 0: H.StartCoroutine(SweepClock()); if (_token != null) H.StartCoroutine(Pulse(_token)); break;
                    case 1: if (_sel.TryGetValue("weapon", out var w)) H.StartCoroutine(Pulse(w.rt)); if (_victimTok != null) H.StartCoroutine(Pulse(_victimTok)); break;
                    case 2: if (_ans.TryGetValue("where", out var wi) && _where != null && _where.Options[wi] != _foundRoom.ToString() && _token != null && _victimTok != null) H.StartCoroutine(Drag(_token.anchoredPosition, _victimTok)); break;
                    case 3: H.StartCoroutine(TrickFx(trick)); if (_sel.TryGetValue("trick", out var tr)) H.StartCoroutine(Pulse(tr.rt)); break;
                    case 4: if (_sel.TryGetValue("conceal", out var cc)) H.StartCoroutine(Pulse(cc.rt)); break;
                    case 5: if (culprit != null) { Speak(culprit, null, Expr.Fear, Gesture.Flinch, 4, 4f); if (_token != null) H.StartCoroutine(Pulse(_token, 1.8f)); } break;
                }
                if (i == 0) H.CamOrbit(40);
                VoiceBabble.Speak(Cast.Player, line); TrialFx.Sound("quill", 0.5f);
                float t0 = Time.unscaledTime, dur = TrialDirectorUI.ProbeFast ? 1.1f : Mathf.Clamp(line.Length * 0.07f, 2.4f, 4.2f);
                while (Time.unscaledTime - t0 < dur) { ct.text = line.Substring(0, Mathf.Min(line.Length, (int)((Time.unscaledTime - t0) * 38))); if (Time.unscaledTime - t0 > 0.6f && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))) break; yield return null; }
                ct.text = line;
                if (Probe && (i == 0 || i == 3 || i == 5)) AutoProbe.Shot("trial_reconstruct_replay" + i);
            }
            H.EyesStare(1f);
            yield return GFx.Plate(H.FxRoot, "사건의 전모", culprit != null ? "범인 — " + Cast.NameOf(culprit) : "범인은 아직 어둠 속에", GPal.Oxblood, 1.8f, "bell_toll");
        }

        IEnumerator SweepClock() { double to = WhenValue(); double from = to - 180; float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < 1.2f) { PaintClock(from + (to - from) * GFx.EaseOut(GFx.K(t0, 1.2f))); yield return null; } PaintClock(to); TrialFx.Sound("bell_toll", 0.6f); }
        static IEnumerator Pulse(RectTransform rt, float amp = 1.25f) { float t0 = Time.unscaledTime; while (rt != null && Time.unscaledTime - t0 < 0.6f) { float k = GFx.K(t0, 0.6f); rt.localScale = Vector3.one * (1 + Mathf.Sin(k * Mathf.PI) * (amp - 1)); yield return null; } if (rt != null) rt.localScale = Vector3.one; }
        IEnumerator Drag(Vector2 from, RectTransform victim)
        {
            if (victim == null) yield break; var to = victim.anchoredPosition; victim.anchoredPosition = from; var path = UIKit.Rect(_mapRooms, "Path", Vector2.zero, Vector2.one).gameObject.AddComponent<ThreadGraphic>(); path.raycastTarget = false; path.color = GPal.A(GPal.InkRed, 0.8f); path.Width = 3; path.Sag = 0;
            var o = -_mapRooms.rect.size * 0.5f; float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 1.2f && victim != null) { float k = GFx.EaseOut(GFx.K(t0, 1.2f)); victim.anchoredPosition = Vector2.Lerp(from, to, k); path.Set(from + o, victim.anchoredPosition + o); yield return null; }
        }
        IEnumerator TrickFx(string trick)
        {
            if (trick == null || trick == TrialSystem.TrickOptions[0] || _victimTok == null) yield break;
            var at = _victimTok.anchoredPosition; string glyph = trick == TrialSystem.TrickOptions[1] ? "실" : trick == TrialSystem.TrickOptions[2] ? "열" : trick == TrialSystem.TrickOptions[3] ? "글" : "칼";
            Color c = trick == TrialSystem.TrickOptions[2] ? GPal.Candle : GPal.InkRed;
            var g = Goth.Glow(_mapRooms, "TrickGlow", GPal.A(c, 0.6f), new Vector2(220, 220), Vector2.zero); g.rectTransform.anchorMin = g.rectTransform.anchorMax = Vector2.zero; g.rectTransform.anchoredPosition = at;
            var s = GFx.Seal(_mapRooms, Vector2.zero, GPal.Wax, glyph, 64); s.rectTransform.anchorMin = s.rectTransform.anchorMax = Vector2.zero; s.rectTransform.anchoredPosition = at + new Vector2(46, 30);
            yield return GFx.Slam(s);
            if (trick == TrialSystem.TrickOptions[1] && _roomRts.TryGetValue(_foundRoom, out var room))
            {   // the thread from the latch out under the door
                var th = UIKit.Rect(_mapRooms, "Thread", Vector2.zero, Vector2.one).gameObject.AddComponent<ThreadGraphic>(); th.raycastTarget = false; th.color = GPal.Wax; th.Width = 2.5f; th.Sag = 6;
                var o = -_mapRooms.rect.size * 0.5f; var a = room.anchoredPosition + room.sizeDelta * 0.5f; var b = room.anchoredPosition + new Vector2(room.sizeDelta.x * 0.5f, -26);
                float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < 0.8f) { th.Set(a + o, Vector2.Lerp(a, b, GFx.K(t0, 0.8f)) + o); yield return null; }
            }
            float h0 = Time.unscaledTime; while (Time.unscaledTime - h0 < 1.2f && g != null) { g.color = GPal.A(c, 0.35f + Mathf.Sin((Time.unscaledTime - h0) * 8) * 0.2f); yield return null; }
        }

        static void ArtClock(RectTransform p)
        {
            var face = TrialFx.Centered(p, "Face", new Vector2(150, 150)); face.anchorMin = face.anchorMax = new Vector2(0.28f, 0.66f); face.anchoredPosition = Vector2.zero;
            var fg = face.gameObject.AddComponent<RingGraphic>(); fg.Thickness = 75; fg.color = GPal.Bone; fg.raycastTarget = false;
            var ring = TrialFx.Centered(face, "Ring", new Vector2(160, 160)); var rg = ring.gameObject.AddComponent<RingGraphic>(); rg.Thickness = 7; rg.color = GPal.Brass; rg.raycastTarget = false;
            var hh = UIKit.Img(face, "H", GPal.Ink, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)); hh.rectTransform.pivot = new Vector2(0.5f, 0.1f); hh.rectTransform.sizeDelta = new Vector2(6, 42); hh.rectTransform.localRotation = Quaternion.Euler(0, 0, -120);
            var mm = UIKit.Img(face, "M", GPal.Ink, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)); mm.rectTransform.pivot = new Vector2(0.5f, 0.1f); mm.rectTransform.sizeDelta = new Vector2(4, 62); mm.rectTransform.localRotation = Quaternion.Euler(0, 0, 30);
            var plan = UIKit.Rect(p, "Plan", new Vector2(0.56f, 0.44f), new Vector2(0.94f, 0.9f));
            for (int i = 0; i < 4; i++) { var r = UIKit.Rect(plan, "R" + i, new Vector2((i % 2) * 0.5f, (i / 2) * 0.5f), new Vector2((i % 2) * 0.5f + 0.5f, (i / 2) * 0.5f + 0.5f), new Vector2(3, 3), new Vector2(-3, -3)); UIKit.Img(r, "F", GPal.A(i == 1 ? GPal.InkRed : GPal.ParchDark, i == 1 ? 0.3f : 0.35f), Vector2.zero, Vector2.one); var f = Goth.Frame(r, "I", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, false); f.Border = GPal.A(GPal.Ink, 0.8f); f.Gap = 0; f.Corners = false; }
            ArtSeal(plan, new Vector2(0.75f, 0.25f), GPal.Wax, "범", 40);
            for (int i = 0; i < 5; i++) { var m = UIKit.Rect(p, "W" + i, new Vector2(0.12f + i * 0.17f, 0.2f), new Vector2(0.12f + i * 0.17f, 0.2f)); m.sizeDelta = new Vector2(46, 46); var rg2 = m.gameObject.AddComponent<RingGraphic>(); rg2.Thickness = i == 2 ? 23 : 4; rg2.color = i == 2 ? GPal.Gilt : GPal.BrassDim; rg2.raycastTarget = false; }
            ArtText(p, "범인", 15, GPal.InkRed, new Vector2(0.08f, 0.3f), new Vector2(0.4f, 0.36f), true);
        }

        // ------------------------------------------------------------ probe demo
        IEnumerator ProbeDemo()
        {
            UseVirt = true; yield return Wait(1f);
            AutoProbe.Shot("trial_reconstruct");
            // the test player knows the truth, but slips on the weapon first to show the public check
            foreach (var q in T.RQ) if (q.Answer >= 0 && q.Id != "moved") _ans[q.Id] = q.Answer;
            if (_weapon != null && _weapon.Answer >= 0) _ans["weapon"] = (_weapon.Answer + 1) % _weapon.Options.Count;
            if (_where != null && _ans.TryGetValue("where", out var wi) && int.TryParse(_where.Options[wi], out var room)) { var rr = S.Layout.Room(room); if (rr != null && rr.Floor != _floor) { _floor = rr.Floor; DrawFloor(); } }
            RefreshAll(); yield return Wait(0.6f); AutoProbe.Shot("trial_reconstruct_set");
            Check(); yield return Wait(1.6f);
            if (_weapon != null && _weapon.Answer >= 0) _ans["weapon"] = _weapon.Answer; RefreshAll(); Check(); yield return Wait(1.2f);
            _submit = true;
        }
    }
}
