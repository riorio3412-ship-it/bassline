using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BL23.Game
{
    /// <summary>민혁의 수첩 — five fixed tabs: 1 단서 (사건 개요 + the clue cards), 2 동선 (who was where, one row per person),
    /// 3 인물, 4 지도, 5 일정·소지품 (appointments, chapter rules, goals, things carried, past cases).
    /// Keys: 1–5 tabs · Q/E cycle · ↑/↓ move the list selection · Enter ★ pin · Space 자세히 · Tab/Esc close.
    /// Reads only what the player knows (CaseBoard is the kernel's player-side view); never shows hidden totals or truths.</summary>
    public sealed class NoteUI : MonoBehaviour
    {
        Session _s; GameState S => _s.S; Simulation Sim => _s.Sim; Canvas _c; public bool Open;
        RectTransform _tabs, _body, _detail;
        static readonly string[] TabIds = { "evidence", "timeline", "people", "map", "schedule" };
        static readonly string[] TabNames = { "단서", "동선", "인물", "지도", "일정·소지품" };
        string _tab = "schedule", _lastTab = "schedule";
        string _sel;                                   // the selected row of the current tab (card / person / item / room key)
        bool _more, _otherOpen, _archiveOpen, _tlUnknownOpen; int _filterQ = -1; string _section; int _mapFloor = int.MinValue, _mapRoom = -1; string _tlPerson;
        // keyboard: the current list's rows in order, and where each sits in its scroll view
        readonly List<string> _navIds = new List<string>(); readonly Dictionary<string, Action> _navPick = new Dictionary<string, Action>();
        readonly Dictionary<string, (float y, float h)> _rowPos = new Dictionary<string, (float, float)>();
        ScrollRect _listScroll; RectTransform _listContent; string _builtTab;

        public int OpenedFrame = -1, ClosedFrame = -1;

        public void Init(Session s)
        {
            _s = s; _c = UIKit.Root("Note", 40); var t = _c.transform;
            UIKit.Img(t, "Dim", Pal.A(Pal.Ink, 0.92f), Vector2.zero, Vector2.one);
            // a leather-bound book: near-opaque (linear colour makes 0.9x read as see-through), a thin gold rule for the edge
            var frame = UIKit.Slant(t, "Frame", new Vector2(0, 0), new Vector2(1, 1), new Vector2(60, 50), new Vector2(-60, -50), Pal.A(Pal.Panel2, 0.995f), Pal.A(Pal.Ink, 0.995f), Pal.A(Pal.Gold, 0.75f), 0);
            UIKit.Text(frame.transform, "Title", "민혁의 수첩", 44, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Title, Vector2.zero, Vector2.one, new Vector2(40, 20), new Vector2(-40, -22));
            UIKit.Text(frame.transform, "Sub", "수첩을 보는 동안에는 시간이 멈춘다", 18, Pal.TextDim, TextAlignmentOptions.TopLeft, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(330, 34), new Vector2(-40, -30));
            _tabs = UIKit.Rect(frame.transform, "Tabs", new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -140), new Vector2(-30, -80));
            _body = UIKit.Rect(frame.transform, "Body", new Vector2(0, 0), new Vector2(0.52f, 1), new Vector2(40, 40), new Vector2(0, -160));
            _detail = UIKit.Rect(frame.transform, "Detail", new Vector2(0.53f, 0), new Vector2(1, 1), new Vector2(10, 40), new Vector2(-40, -160));
            UIKit.Text(frame.transform, "Close", "1–5 탭 · Q/E 넘기기 · ↑/↓ 고르기 · Tab/Esc 닫기", 18, Pal.TextDim, TextAlignmentOptions.TopRight, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(0, 22), new Vector2(-44, -24));
            _c.enabled = false;
        }

        public void Destroy() { if (_c) UnityEngine.Object.Destroy(_c.gameObject); }

        /// <summary>Tab/N: during a case the notebook opens on 단서; otherwise on the tab used last (일정·소지품 at first).</summary>
        public void Toggle() { if (Open) Close(); else Show(CaseProgress.Current(S) != null ? "evidence" : _lastTab); }
        public void OpenTab(string tab) { Show(tab); }
        public void Close() { Open = false; _c.enabled = false; _s.Resume("note"); UISfx.Cancel(); ClosedFrame = Time.frameCount; }

        /// <summary>Old tab ids (and the sections of 일정·소지품) map onto the five fixed tabs.</summary>
        static string MapTab(string id, out string section)
        {
            section = null;
            switch (id)
            {
                case "evidence": return "evidence";
                case "timeline": return "timeline";
                case "people": case "theory": return "people";
                case "map": return "map";
                case "goals": section = "goals"; return "schedule";
                case "inventory": section = "inventory"; return "schedule";
                case "rules": section = "rules"; return "schedule";
                case "archive": section = "archive"; return "schedule";
                case "schedule": return "schedule";
            }
            return "schedule";
        }

        void Show(string tab)
        {
            if (!Open) OpenedFrame = Time.frameCount;
            tab = MapTab(tab, out var section);
            if (section != null) { _section = section; if (section == "archive") _archiveOpen = true; }
            if (tab != _tab || !Open) { if (tab != _tab) { _sel = null; _more = false; _filterQ = -1; } if (tab == "map") { _mapFloor = S.Player != null ? S.Player.Pos.f : 0; _mapRoom = -1; } }
            _tab = tab; _lastTab = tab; Open = true; _c.enabled = true;
            // over the court the notebook must sit above the court's own layers (decision moments live on the FX canvas)
            _c.sortingOrder = _s.Trial != null && _s.Trial.Active ? 62 : 40;
            _s.Pause("note"); UISfx.Page(); Rebuild();
        }

        void Update()
        {
            if (!Open || Time.frameCount == OpenedFrame) return;
            if (BacklogUI.Open || Time.frameCount == BacklogUI.ClosedFrame) return;   // 지난 대화 over the notebook owns the keys (its Esc closes only itself)
            if (Input.GetKeyDown(KeyCode.Escape) || (Input.GetKeyDown(KeyCode.Tab) && Time.frameCount > 2)) { Close(); return; }
            if (Input.GetKeyDown(KeyCode.Q)) { Cycle(-1); return; }
            if (Input.GetKeyDown(KeyCode.E)) { Cycle(1); return; }
            for (int i = 0; i < TabIds.Length; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) { if (TabIds[i] != _tab) Show(TabIds[i]); return; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) Nav(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) Nav(1);
            if (_tab == "evidence")
            {
                if (_sel == OtherId && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))) { ToggleOther(); return; }
                var sel = SelectedCard();
                if (sel != null && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) { sel.Ev.Pinned = !sel.Ev.Pinned; UISfx.Confirm(); Rebuild(); }
                if (sel != null && Input.GetKeyDown(KeyCode.Space)) { _more = !_more; UISfx.Page(); Rebuild(); }
            }
        }
        void Cycle(int d) { int i = Array.IndexOf(TabIds, _tab); i = (i + d + TabIds.Length) % TabIds.Length; Show(TabIds[i]); }

        void Nav(int d)
        {
            if (_navIds.Count == 0) return;
            int i = _sel == null ? -1 : _navIds.IndexOf(_sel);
            i = i < 0 ? (d > 0 ? 0 : _navIds.Count - 1) : Mathf.Clamp(i + d, 0, _navIds.Count - 1);
            if (_navPick.TryGetValue(_navIds[i], out var pick)) { UISfx.Hover(); pick(); }
        }

        void Rebuild()
        {
            float keepY = _listContent != null && _builtTab == _tab ? _listContent.anchoredPosition.y : -1f;
            bool jump = _section != null;   // a section was asked for (J / I / rules / archive): the tab scrolls there instead
            UIKit.Clear(_tabs); UIKit.Clear(_body); UIKit.Clear(_detail);
            _navIds.Clear(); _navPick.Clear(); _rowPos.Clear(); _listScroll = null; _listContent = null;
            float w = 1f / TabIds.Length;
            for (int i = 0; i < TabIds.Length; i++)
            {
                var id = TabIds[i]; bool empty = TabEmpty(id);
                string label = $"<size=70%><color=#9A8E7C>{i + 1}</color></size>  " + (empty && id != _tab ? $"<color=#7C7266>{TabNames[i]}</color>" : TabNames[i]);
                var b = UIKit.Button(_tabs, label, () => { if (_tab != id) Show(id); }, new Vector2(i * w, 0), new Vector2((i + 1) * w, 1), new Vector2(3, 0), new Vector2(-3, 0), 24, 12, Fonts.Bold);
                if (id == _tab) { b.Idle = Pal.MagentaDim; b.Paint(); }
            }
            switch (_tab)
            {
                case "evidence": Evidence(); break;
                case "timeline": Timeline(); break;
                case "people": People(); break;
                case "map": Map(); break;
                default: Schedule(); break;
            }
            if (_listContent != null && _listScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                if (!jump) { if (keepY >= 0) SetScroll(keepY); EnsureVisible(_sel); }
                if (_scrollTo != null && _rowPos.TryGetValue(_scrollTo, out var st)) SetScroll(st.y - 4);
            }
            _scrollTo = null;
            _builtTab = _tab;
        }

        bool TabEmpty(string id)
        {
            var k = S.K(Cast.Player);
            if (id == "evidence") return !k.Evidence.Any(e => e.Loop == S.Loop && e.Chapter == S.Chapter && !e.Hidden);
            if (id == "timeline") return k.Sightings.Count == 0 && k.Statements.Count == 0;
            return false;
        }

        // ---------------------------------------------------------------- helpers
        ScrollRect Scroll(RectTransform parent, out RectTransform content)
        {
            var view = UIKit.Rect(parent, "Scroll", Vector2.zero, Vector2.one);
            var sr = view.gameObject.AddComponent<ScrollRect>(); sr.horizontal = false; sr.scrollSensitivity = 40; sr.movementType = ScrollRect.MovementType.Clamped;
            view.gameObject.AddComponent<RectMask2D>();
            var bed = view.gameObject.AddComponent<Image>(); bed.color = new Color(0, 0, 0, 0); bed.raycastTarget = true;
            content = UIKit.Rect(view, "Content", new Vector2(0, 1), new Vector2(1, 1)); content.pivot = new Vector2(0.5f, 1);
            sr.content = content; sr.viewport = view;
            // a thin gold thumb at the right edge whenever the list runs past the page (so a list that scrolls says so)
            var track = UIKit.Img(view, "Track", Pal.A(Pal.Gold, 0.12f), new Vector2(1, 0), new Vector2(1, 1), new Vector2(-5, 2), new Vector2(-2, -2)); track.raycastTarget = false;
            var thumb = UIKit.Img(view, "Thumb", Pal.A(Pal.Gold, 0.7f), new Vector2(1, 0), new Vector2(1, 1), new Vector2(-5, 0), new Vector2(-2, 0)); thumb.raycastTarget = false;
            var cue = view.gameObject.AddComponent<ScrollCue>(); cue.Sr = sr; cue.Track = track; cue.Thumb = thumb;
            return sr;
        }
        /// <summary>The tab's main list (the one ↑/↓ moves through).</summary>
        RectTransform List(RectTransform parent) { _listScroll = Scroll(parent, out var content); _listContent = content; return content; }
        void Height(RectTransform content, float h) { content.sizeDelta = new Vector2(0, h); }
        void SetScroll(float y)
        {
            if (_listScroll == null) return; float view = _listScroll.viewport.rect.height, total = _listContent.rect.height;
            _listContent.anchoredPosition = new Vector2(_listContent.anchoredPosition.x, Mathf.Clamp(y, 0, Mathf.Max(0, total - view)));
        }
        void EnsureVisible(string id)
        {
            if (id == null || _listScroll == null || !_rowPos.TryGetValue(id, out var p)) return;
            float view = _listScroll.viewport.rect.height, cur = _listContent.anchoredPosition.y;
            if (p.y < cur) cur = p.y - 6; else if (p.y + p.h > cur + view) cur = p.y + p.h - view + 6;
            SetScroll(cur);
        }

        /// <summary>A list row. With an id it joins the keyboard list; a chip (a clue's kind) sits at its left.</summary>
        UIKit.Btn RowAt(RectTransform content, ref float y, string id, string label, Action a, float h = 54, float size = 21, string chip = null, Goth.ChipTone tone = Goth.ChipTone.Plain, bool sel = false)
        {
            var b = UIKit.Button(content, label, a, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -y - h), new Vector2(-12, -y), size, 10, Fonts.Body);
            b.Label.textWrappingMode = TextWrappingModes.NoWrap; b.Label.overflowMode = TextOverflowModes.Ellipsis;
            if (chip != null) { var c = Goth.KindChip(b.transform, chip, tone, 24, 15, 26); b.Label.rectTransform.offsetMin = new Vector2(24 + c.sizeDelta.x + 12, 0); }
            // hover is only a faint lift (the red fill belongs to the selected row), so the row under the mouse never reads as chosen
            b.HoverTop = sel ? Pal.MagentaDim : Pal.A(new Color(0.16f, 0.11f, 0.085f), 0.95f); b.HoverBottom = Pal.A(Pal.Ink, 0.9f);
            if (sel)
            {
                b.Idle = Pal.MagentaDim;
                var rule = UIKit.Img(b.transform, "SelRule", Pal.A(Pal.Gold, 0.95f), new Vector2(0, 0), new Vector2(0, 1), new Vector2(2, 4), new Vector2(6, -4)); rule.raycastTarget = false;
            }
            b.Paint();
            if (id != null) { _rowPos[id] = (y, h); if (!_navPick.ContainsKey(id)) { _navIds.Add(id); _navPick[id] = a; } }
            y += h + 6;
            return b;
        }
        void Header(RectTransform content, ref float y, string text, float h = 38)
        {
            UIKit.Text(content, "H", text, 20, Pal.Gold, TextAlignmentOptions.BottomLeft, Fonts.Bold, new Vector2(0, 1), new Vector2(1, 1), new Vector2(6, -y - h + 4), new Vector2(-12, -y));
            UIKit.Img(content, "HR", Pal.A(Pal.Gold, 0.35f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(6, -y - h), new Vector2(-12, -y - h + 1));
            y += h + 6;
        }
        void Note(RectTransform content, ref float y, string text, float size = 19, float h = 34)
        {
            var t = UIKit.Text(content, "N", text, size, Pal.TextDim, TextAlignmentOptions.TopLeft, Fonts.Body, new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -y - 2000), new Vector2(-16, -y));
            t.ForceMeshUpdate(); float th = Mathf.Max(h, t.preferredHeight + 8); ((RectTransform)t.transform).offsetMin = new Vector2(10, -y - th); y += th;
        }

        TextMeshProUGUI DetailText(string text, float size = 22) => DetailIn(_detail, text, size);
        TextMeshProUGUI DetailIn(RectTransform area, string text, float size)
        {
            Scroll(area, out var content);
            var t = UIKit.Text(content, "T", text, size, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Body, new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -4000), new Vector2(-20, -4));
            t.ForceMeshUpdate(); float h = t.preferredHeight + 40; ((RectTransform)t.transform).offsetMin = new Vector2(10, -h); Height(content, h);
            return t;
        }

        static string Dim(string s) => $"<color=#9A8E7C>{s}</color>";
        static string Gold(string s) => $"<color=#D6AD62>{s}</color>";
        static string Ago(double minutes)
        {
            if (minutes < 1.5) return "방금"; if (minutes < 60) return $"{(int)Math.Round(minutes)}분 전";
            if (minutes < 24 * 60) return $"{(int)Math.Round(minutes / 60)}시간 전"; return $"{(int)Math.Round(minutes / 1440)}일 전";
        }
        static string Short(string room) => string.IsNullOrEmpty(room) ? "?" : room.Length > 6 ? room.Substring(0, 6) : room;
        /// <summary>A sound in two or three syllables for a board cell.</summary>
        static string SoundShort(SoundKind k)
        {
            switch (k)
            {
                case SoundKind.Scream: return "비명"; case SoundKind.Strike: return "충격음"; case SoundKind.Struggle: return "몸싸움"; case SoundKind.Fall: return "쓰러짐";
                case SoundKind.GlassBreak: return "유리 깨짐"; case SoundKind.Crash: return "부서짐"; case SoundKind.Splash: return "물소리"; case SoundKind.Press: return "기계음";
                case SoundKind.Machine: return "기계음"; case SoundKind.Door: return "문 소리"; case SoundKind.DoorSlam: return "문 쾅"; case SoundKind.Knock: return "노크";
                case SoundKind.Running: return "뛰는 발"; case SoundKind.Shout: return "고함"; case SoundKind.Switch: return "딸깍"; case SoundKind.Bell: return "호출벨";
                case SoundKind.Music: return "음악"; case SoundKind.Laugh: return "웃음"; case SoundKind.Cry: return "울음"; case SoundKind.Rain: return "빗소리";
                case SoundKind.Static: return "지직"; case SoundKind.Clock: return "종소리";
                case SoundKind.Scrape: return "끄는 소리";
            }
            return "소리";
        }
        static int Assist => Settings.Assist;
        T Safe<T>(Func<T> f, T fallback) { try { return f(); } catch (Exception e) { Debug.LogException(e); return fallback; } }

        // ================================================================ 1 단서
        List<CaseBoard.View> _cards = new List<CaseBoard.View>();
        CaseBoard.View SelectedCard() => _sel == null ? null : _cards.FirstOrDefault(v => v.Id == _sel);

        void Evidence()
        {
            _cards = Safe(() => CaseBoard.Cards(Sim), new List<CaseBoard.View>());
            bool inCase = CaseProgress.Current(S) != null;
            var qs = inCase ? Safe(() => CaseBoard.Questions(Sim), new List<CaseBoard.Question>()) : new List<CaseBoard.Question>();
            if (_filterQ >= qs.Count) _filterQ = -1;
            float top = 0;
            // 사건 개요: four questions, ○ / ●
            if (inCase && qs.Count > 0)
            {
                int firm = qs.Count(q => q.Firm);
                // compact (4 × 38 px): with eight case cards the 기타 group still shows under the list
                const float QH = 38, QTop = 36;
                var box = UIKit.Rect(_body, "Overview", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -(QTop + 4 * QH + 8)), new Vector2(-12, 0));
                UIKit.Img(box, "Bed", Pal.A(Pal.Ink, 0.55f), Vector2.zero, Vector2.one);
                UIKit.Img(box, "Rule", Pal.A(Pal.Gold, 0.55f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -1), Vector2.zero);
                UIKit.Text(box, "H", $"사건 개요  {Dim($"{firm}/4")}", 21, Pal.Gold, TextAlignmentOptions.MidlineLeft, Fonts.Title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -QTop), new Vector2(-10, -2));
                for (int i = 0; i < qs.Count && i < 4; i++)
                {
                    var q = qs[i]; int qi = i;
                    string mark = q.Firm ? Gold("●") : Dim("○");
                    string ans = string.IsNullOrEmpty(q.Answer) ? Dim("아직 모른다") : q.Firm ? q.Answer : Dim(q.Answer + " · 더 확인 필요");
                    var b = UIKit.Button(box, $"{mark}  {q.Ask}  <size=90%>· {ans}</size>", () => { _filterQ = _filterQ == qi ? -1 : qi; _sel = null; Rebuild(); },
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(6, -QTop - (i + 1) * QH), new Vector2(-6, -QTop - i * QH - 3), 19, 8, Fonts.Body);
                    b.Label.textWrappingMode = TextWrappingModes.NoWrap; b.Label.overflowMode = TextOverflowModes.Ellipsis;
                    if (_filterQ == qi) { b.Idle = Pal.MagentaDim; b.Paint(); }
                }
                top = QTop + 4 * QH + 18;
            }
            else if (!inCase)
            {
                UIKit.Text(_body, "NoCase", "사건이 없는 날이다. 수상한 걸 보면 여기에 적힌다.", 20, Pal.TextDim, TextAlignmentOptions.MidlineLeft, Fonts.Body, new Vector2(0, 1), new Vector2(1, 1), new Vector2(6, -44), new Vector2(-12, 0));
                top = 52;
            }
            var area = UIKit.Rect(_body, "Area", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -top));
            var content = List(area); float y = 0;
            List<CaseBoard.View> main, other;
            if (_filterQ >= 0)
            {
                var ids = new HashSet<string>(qs[_filterQ].CardIds ?? new List<string>());
                main = _cards.Where(v => ids.Contains(v.Id)).ToList(); other = new List<CaseBoard.View>();
                var fb = UIKit.Button(content, $"{Gold("●")} ‘{qs[_filterQ].Ask}’ 관련 단서만 보는 중  {Dim("· 모두 보기")}", () => { _filterQ = -1; Rebuild(); }, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -44), new Vector2(-12, 0), 18, 8, Fonts.Body);
                fb.Label.textWrappingMode = TextWrappingModes.NoWrap; y = 50;
            }
            else if (inCase) { main = _cards.Where(v => v.InCase).ToList(); other = _cards.Where(v => !v.InCase).ToList(); }
            else { main = _cards.ToList(); other = new List<CaseBoard.View>(); }
            // ★ pinned cards float to the top of their group (the kernel's order otherwise)
            main = main.OrderByDescending(v => v.Ev != null && v.Ev.Pinned).ToList(); other = other.OrderByDescending(v => v.Ev != null && v.Ev.Pinned).ToList();
            if (inCase && _filterQ < 0) Header(content, ref y, $"사건 단서 ({main.Count})");
            foreach (var v in main) CardRow(content, ref y, v);
            if (main.Count == 0 && _filterQ >= 0) Note(content, ref y, "아직 이 물음과 관련된 단서가 없다.");
            if (inCase && _filterQ < 0 && other.Count > 0)
            {
                y += 6;
                // a keyboard row (↑/↓ reaches it, Enter or a click opens it); opening it scrolls it to the top of the list
                var ob = RowAt(content, ref y, OtherId, _otherOpen ? $"기타 ({other.Count})  {Dim("· 접기")}" : $"기타 ({other.Count}) ▶  {Dim("사건과 덜 관련된 것")}", ToggleOther, 44, 19, null, Goth.ChipTone.Plain, _sel == OtherId);
                ob.Label.color = Pal.TextDim; ob.Label.font = Fonts.Bold;
                _navPick[OtherId] = () => { _sel = OtherId; Rebuild(); };
                if (_otherOpen) foreach (var v in other) CardRow(content, ref y, v);
            }
            Height(content, y + 20);

            var sel = SelectedCard();
            if (sel != null) { ShowCard(sel); return; }
            if (_filterQ >= 0) { var q = qs[_filterQ]; DetailText($"<size=30>{q.Ask}</size>\n{(q.Firm ? Gold("● 확인됨") : Dim("○ 아직 확실하지 않다"))}\n\n{(string.IsNullOrEmpty(q.Answer) ? Dim("아직 모른다") : q.Answer)}\n\n{(q.Firm || string.IsNullOrEmpty(q.Hint) ? "" : Gold("다음 — ") + q.Hint)}", 22); return; }
            if (_cards.Count == 0) { DetailText(inCase ? "아직 적힌 단서가 없다.\n\n시신이나 현장의 흔적을 보며 R을 길게 누르면 살펴볼 수 있다.\n사람들에게 사건 이야기를 물으면 그 말이 적힌다.\n\n쓸 만한 것만 수첩에 남는다." : "아직 적힌 단서가 없다.\n\n수상한 것을 보며 R을 길게 누르면 살펴볼 수 있다.\n쓸 만한 것만 수첩에 남는다.", 22); return; }
            var steps = inCase && Assist >= 1 ? Safe(() => CaseBoard.NextSteps(Sim, 3), new List<string>()) : new List<string>();
            if (steps.Count > 0) DetailText("<size=30>다음에 할 일</size>\n\n" + string.Join("\n\n", steps.Select((s, i) => $"{Gold((i + 1) + ".")} {s}")) + "\n\n" + Dim("왼쪽에서 단서를 고르면 자세히 보인다. ↑/↓로도 고를 수 있다."), 22);
            else DetailText(inCase ? "왼쪽에서 단서를 골라 보자.\n\n" + Dim("◆ 표시는 중요 단서다. 사건 개요의 물음을 누르면 그 물음과 관련된 단서만 보인다.") : "왼쪽에서 단서를 골라 보자.", 22);
        }

        const string OtherId = "#other";
        string _scrollTo;   // a row to bring to the top of the list after the next rebuild (the 기타 header when it opens)
        void ToggleOther() { _otherOpen = !_otherOpen; _sel = OtherId; if (_otherOpen) _scrollTo = OtherId; UISfx.Page(); Rebuild(); }

        void CardRow(RectTransform content, ref float y, CaseBoard.View v)
        {
            var id = v.Id; string pin = v.Ev != null && v.Ev.Pinned ? Gold("★") + " " : "";
            string title = (v.Key ? Gold("◆") + " " : "") + pin + CluePicker.Plain(v.Title ?? "");
            string when = string.IsNullOrEmpty(v.When) ? "" : $"  <size=16>{Dim(v.When)}</size>";
            // a witness row says what the witness saw ("해린의 증언 · 도윤이 청동 흉상을 들고 있었다"), so testimony can be told apart at a glance
            if (v.Cat == CaseBoard.Cat.Witness && string.IsNullOrEmpty(v.When) && !string.IsNullOrEmpty(v.Line)) when = $"  <size=17>{Dim("· " + CluePicker.Plain(v.Line))}</size>";
            RowAt(content, ref y, id, title + when, () => { _sel = id; _more = false; Rebuild(); }, 46, 20, string.IsNullOrEmpty(v.KindLabel) ? "기록" : v.KindLabel, v.Key ? Goth.ChipTone.Gold : Goth.ChipTone.Plain, id == _sel);
        }

        void ShowCard(CaseBoard.View v)
        {
            UIKit.Clear(_detail);
            var e = v.Ev;
            var title = UIKit.Text(_detail, "Title", CluePicker.Plain(v.Title ?? ""), 34, v.Key ? Pal.Gold : Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -52), new Vector2(-10, -2));
            title.textWrappingMode = TextWrappingModes.NoWrap; title.overflowMode = TextOverflowModes.Ellipsis;
            var chips = UIKit.Rect(_detail, "Chips", new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -90), new Vector2(-10, -60));
            float x = 0;
            x += Goth.KindChip(chips, string.IsNullOrEmpty(v.KindLabel) ? "기록" : v.KindLabel, Goth.ChipTone.Plain, x, 16, 28).sizeDelta.x + 8;
            if (v.Key) x += Goth.KindChip(chips, "중요", Goth.ChipTone.Gold, x, 16, 28).sizeDelta.x + 8;
            if (v.Hearsay) x += Goth.KindChip(chips, "전해 들음", Goth.ChipTone.Smoke, x, 16, 28).sizeDelta.x + 8;
            if (!string.IsNullOrEmpty(v.When)) UIKit.Text(chips, "When", v.When, 18, Pal.TextDim, TextAlignmentOptions.MidlineLeft, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(x + 6, 0), Vector2.zero);

            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(v.Line)) sb.Append($"<size=24>{CluePicker.Plain(v.Line)}</size>\n");
            var bullets = (v.Bullets ?? new List<string>()).Where(b => !string.IsNullOrEmpty(b)).Take(4).ToList();
            if (bullets.Count > 0) sb.Append("\n" + string.Join("\n", bullets.Select(b => $"{Gold("·")} {CluePicker.Plain(b)}")) + "\n");
            if (!string.IsNullOrEmpty(v.Caution)) sb.Append($"\n{Gold("주의 —")} {v.Caution}\n");
            if (_more && e != null)
            {
                if (e.Quotes != null && e.Quotes.Count > 0) sb.Append($"\n{Gold("들은 말")}\n" + string.Join("\n", e.Quotes.Select(q => $"“{q}”")) + "\n");
                sb.Append("\n");
                if (e.Room >= 0) sb.Append($"{Dim("장소")}  {S.RoomName(e.Room)}\n");
                if (!string.IsNullOrEmpty(e.Source)) sb.Append($"{Dim("알게 된 경위")}  {e.Source}\n");
                if (e.SharedWith != null && e.SharedWith.Count > 0) sb.Append($"{Dim("알려 준 사람")}  {string.Join(", ", e.SharedWith.Select(Cast.GivenOf))}\n");
            }
            var area = UIKit.Rect(_detail, "Body", Vector2.zero, Vector2.one, new Vector2(0, 66), new Vector2(0, -98));
            DetailIn(area, sb.ToString(), 21);
            bool pinned = e != null && e.Pinned;
            UIKit.Button(_detail, pinned ? "★ 고정 풀기  <size=70%>Enter</size>" : "★ 고정  <size=70%>Enter</size>", () => { if (e != null) e.Pinned = !e.Pinned; Rebuild(); }, new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(-6, 54), 20);
            UIKit.Button(_detail, _more ? "간단히  <size=70%>Space</size>" : "자세히 ▶  <size=70%>Space</size>", () => { _more = !_more; Rebuild(); }, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(6, 0), new Vector2(0, 54), 20);
        }

        // ================================================================ 2 동선: one row per person, a few time columns around the case
        float _bandFrom = -1f, _bandTo;
        static double Floor30(double t) => Math.Floor(t / 30) * 30;
        static double Ceil30(double t) => Math.Ceiling(t / 30) * 30;
        static string PartOf(double t) { string a = ClockFmt.Mark(t, true), b = ClockFmt.Mark(t, false); return a.Length > b.Length ? a.Substring(0, a.Length - b.Length).Trim() : ""; }

        void Timeline()
        {
            var k = S.K(Cast.Player);
            var inc = CaseProgress.Current(S);
            double now = S.Clock, t0, t1; bool examined = false; double w0 = 0, w1 = 0, found = -1;
            if (inc != null)
            {
                var kw = Safe(() => CaseBoard.KnownWindow(Sim), (0.0, 0.0, false));
                examined = kw.Item3; w0 = kw.Item1; w1 = kw.Item2;
                found = inc.DiscoverClock > 0 ? inc.DiscoverClock : inc.ConfirmClock > 0 ? inc.ConfirmClock : now; if (found > now) found = now;
                t0 = Floor30((kw.Item1 > 0 ? kw.Item1 : found - 150) - 60); t1 = Ceil30(found);
                if (w1 > found) w1 = found;
            }
            else { t1 = Ceil30(now); t0 = Floor30(now - 180); }
            double step = t1 - t0 <= 180 ? 30 : 60;
            int cols = (int)Math.Ceiling((t1 - t0) / step - 0.001);
            if (cols > 6) { cols = 6; } if (cols < 4) cols = 4;
            t0 = t1 - cols * step;
            // facts: (person, a, b, room, 0 = seen / 1 = told, note, source)
            var facts = new List<(string who, double a, double b, int room, int style, string note, string src)>();
            foreach (var s in k.Sightings.Where(s => s.T1 >= t0 && s.T0 <= t1 && s.IdConf >= 0.5f && s.Target != null))
                facts.Add((s.Target, s.T0, s.T1, s.Room, 0, s.Dead ? "쓰러져 있었다" : s.Bloody ? "옷에 붉은 얼룩이 있었다" : s.Held != null ? LineBank.FixParticles((ItemCatalog.Get(s.Held)?.Kor ?? "무언가") + "을(를) 들고 있었다") : s.Running ? "뛰어갔다" : s.Carrying ? "무언가를 옮겼다" : s.Disguise != null ? "얼굴을 가렸다" : null, "직접 봄"));
            foreach (var st in k.Statements.Where(x => x.Prop != null && x.Prop.A != null && x.Prop.T1 >= t0 && x.Prop.T0 <= t1 && x.Prop.Room >= 0 && (x.Prop.Kind == PropKind.AtPlace || x.Prop.Kind == PropKind.WithPerson || x.Prop.Kind == PropKind.AliveAt || x.Prop.Kind == PropKind.Held)))
            {
                string src = Cast.GivenOf(st.Speaker) + "의 말";
                facts.Add((st.Prop.A, st.Prop.T0, st.Prop.T1, st.Prop.Room, 1, null, src));
                if (st.Prop.Kind == PropKind.WithPerson && st.Prop.B != null) facts.Add((st.Prop.B, st.Prop.T0, st.Prop.T1, st.Prop.Room, 1, null, src));
            }
            // the board is about the time before the body was found: after that everyone gathers at the scene, which says nothing
            // about where they were when it happened. Facts are cut at the discovery; the body itself sits on the 발견 tick.
            if (found > 0)
            {
                var cut = new List<(string who, double a, double b, int room, int style, string note, string src)>();
                foreach (var f in facts)
                {
                    if (f.note == "쓰러져 있었다") continue;
                    if (f.a >= found) continue;
                    cut.Add((f.who, f.a, Math.Min(f.b, found), f.room, f.style, f.note, f.src));
                }
                // the victim on the 발견 tick: where the body was found (seen with your own eyes, or known from the broadcast)
                bool sawBody = k.Sightings.Any(s => s.Target == inc.Victim && s.Dead);
                if (inc.FoundRoom >= 0) cut.Add((inc.Victim, found, found, inc.FoundRoom, sawBody ? 0 : 1, "쓰러져 있었다", sawBody ? "직접 봄" : "시신 발견"));
                facts = cut;
            }
            var conflicts = inc != null ? Safe(() => CaseBoard.Conflicts(Sim), new List<CaseBoard.Conflict>()) : new List<CaseBoard.Conflict>();
            var known = facts.Select(f => f.who).Distinct().Where(id => S.A(id) != null && id != Cast.Butler && id != Cast.Player).ToList();
            var people = known.OrderBy(id => id == inc?.Victim ? 0 : 1).ThenBy(id => Cast.NameOf(id), StringComparer.Ordinal).ToList();
            if (inc != null && !people.Contains(inc.Victim) && S.A(inc.Victim) != null) people.Insert(0, inc.Victim);
            var unknown = S.Actors.Values.Where(a => !a.IsPlayer && !a.IsButler && !people.Contains(a.Id) && (a.Alive || a.Id == inc?.Victim)).Select(a => a.Id).OrderBy(id => Cast.NameOf(id), StringComparer.Ordinal).ToList();
            if (_tlPerson == null || !people.Contains(_tlPerson)) _tlPerson = people.FirstOrDefault();

            const float NameW = 0.2f; float colW = (1 - NameW) / cols;
            // header: the columns, the part of the day said on the first column and wherever it changes
            var head = UIKit.Rect(_body, "Head", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -56), new Vector2(-12, 0));
            UIKit.Text(head, "W", inc != null ? "사건 전후" : "최근", 17, Pal.A(Pal.Gold, 0.9f), TextAlignmentOptions.BottomLeft, Fonts.Bold, new Vector2(0, 0), new Vector2(NameW, 1), new Vector2(4, 4), Vector2.zero);
            string lastPart = null;
            for (int c = 0; c < cols; c++)
            {
                double a = t0 + step * c; string part = PartOf(a); bool withPart = c == 0 || part != lastPart; lastPart = part;
                var ht = UIKit.Text(head, "C" + c, ClockFmt.Mark(a, withPart), 16, withPart ? Pal.Text : Pal.TextDim, TextAlignmentOptions.BottomLeft, Fonts.Body, new Vector2(NameW + colW * c, 0), new Vector2(NameW + colW * (c + 1), 1), new Vector2(4, 4), Vector2.zero);
                ht.textWrappingMode = TextWrappingModes.NoWrap;
                UIKit.Img(head, "Tick" + c, Pal.A(Pal.Gold, 0.35f), new Vector2(NameW + colW * c, 0), new Vector2(NameW + colW * c, 0), new Vector2(0, 0), new Vector2(1, 14));
            }
            var area = UIKit.Rect(_body, "Area", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -60));
            // the estimated time of death, only once the body has been examined
            _bandFrom = -1f;
            if (examined && w1 > t0 && w0 < t1)
            {
                float fa = NameW + (1 - NameW) * (float)((Math.Max(w0, t0) - t0) / (t1 - t0)), fb = NameW + (1 - NameW) * (float)((Math.Min(w1, t1) - t0) / (t1 - t0));
                _bandFrom = fa; _bandTo = fb;
                UIKit.Text(head, "Band", "숨진 무렵", 14, Pal.A(new Color(0.9f, 0.45f, 0.45f), 1f), TextAlignmentOptions.TopLeft, Fonts.Bold, new Vector2(fa, 1), new Vector2(Mathf.Max(fb, fa + 0.15f), 1), new Vector2(4, -18), new Vector2(0, 0));
            }
            // the discovery: a thin gold line down the board (nothing after it is drawn)
            float foundX = -1f;
            if (found > t0 && found <= t1)
            {
                foundX = NameW + (1 - NameW) * (float)((found - t0) / (t1 - t0));
                // one row below the 숨진 무렵 label (never on top of it), right of the line — or left of it at the board's edge
                bool right = foundX < 0.94f;
                var ft = UIKit.Text(head, "Found", "발견", 13, Pal.Gold, right ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopRight, Fonts.Bold, new Vector2(foundX, 1), new Vector2(foundX, 1), right ? new Vector2(3, -35) : new Vector2(-60, -35), right ? new Vector2(60, -19) : new Vector2(-3, -19));
                ft.textWrappingMode = TextWrappingModes.NoWrap;
            }
            var content = List(area); float y = 0; const float RH = 54;
            foreach (var id in people)
            {
                var pid = id; bool sel = id == _tlPerson; bool victim = id == inc?.Victim;
                var r = UIKit.Rect(content, "R" + id, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -y - RH), new Vector2(-12, -y));
                UIKit.Img(r, "Bed", sel ? Pal.A(Pal.MagentaDim, 0.9f) : Pal.A(Pal.Ink, _navIds.Count % 2 == 0 ? 0.55f : 0.35f), Vector2.zero, Vector2.one).raycastTarget = true;
                r.gameObject.AddComponent<NoteClick>().OnClick = () => { _tlPerson = pid; _sel = pid; Rebuild(); };
                string nm = victim ? $"<color=#C04A55>{Cast.GivenOf(id)}</color> <size=72%>{Dim("피해자")}</size>" : Cast.GivenOf(id);
                var nt = UIKit.Text(r, "N", nm, 18, Pal.Text, TextAlignmentOptions.MidlineLeft, Fonts.Bold, new Vector2(0, 0), new Vector2(NameW, 1), new Vector2(10, 2), new Vector2(-2, -2));
                nt.textWrappingMode = TextWrappingModes.NoWrap; nt.overflowMode = TextOverflowModes.Ellipsis;
                var mine = conflicts.Where(cf => cf.Who == id).ToList();
                for (int c = 0; c < cols; c++)
                {
                    double a = t0 + step * c, b = a + step;
                    var here = facts.Where(f => f.who == id && f.b >= a && f.a <= b).ToList();
                    if (here.Count == 0) continue;
                    var best = here.OrderBy(f => f.style).ThenByDescending(f => Math.Min(f.b, b) - Math.Max(f.a, a)).First();
                    bool seen = best.style == 0; bool dead = here.Any(f => f.note == "쓰러져 있었다");
                    bool bang = mine.Any(cf => cf.T1 >= a && cf.T0 <= b);
                    string name = Short(S.RoomName(best.room));
                    string cell = dead ? $"<color=#C04A55>{name} ●</color>" : seen ? $"<color=#CFE8DC>{name}</color> {Gold("●")}" : $"<color=#C9A0A6>{name}</color> {Dim("○")}";
                    if (bang) cell += " <color=#E0606A><b>!</b></color>";
                    var ct = UIKit.Text(r, "C" + c, cell, 16, Pal.Text, TextAlignmentOptions.Center, Fonts.Body, new Vector2(NameW + colW * c, 0), new Vector2(NameW + colW * (c + 1), 1), new Vector2(2, 0), new Vector2(-2, 0));
                    ct.enableAutoSizing = true; ct.fontSizeMin = 11; ct.fontSizeMax = 16;
                }
                _rowPos[id] = (y, RH); _navIds.Add(id); _navPick[id] = () => { _tlPerson = pid; _sel = pid; Rebuild(); };
                y += RH + 4;
            }
            // sounds heard (a row only when there is something)
            var sounds = k.Heard.Where(h => h.Clock >= t0 && h.Clock <= t1 && (found <= 0 || h.Clock <= found) && h.Loud > 0.2f && h.Kind != SoundKind.Footsteps && h.Kind != SoundKind.Talk && h.Kind != SoundKind.Announcement).ToList();
            if (sounds.Count > 0)
            {
                var r = UIKit.Rect(content, "Sounds", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -y - RH), new Vector2(-12, -y));
                UIKit.Img(r, "Bed", Pal.A(Pal.Ink, 0.45f), Vector2.zero, Vector2.one);
                UIKit.Text(r, "N", "소리", 18, Pal.A(Pal.Gold, 0.9f), TextAlignmentOptions.MidlineLeft, Fonts.Bold, new Vector2(0, 0), new Vector2(NameW, 1), new Vector2(10, 0), Vector2.zero);
                for (int c = 0; c < cols; c++)
                {
                    double a = t0 + step * c, b = a + step; var hs = sounds.Where(h => h.Clock >= a && h.Clock < b).ToList();
                    if (hs.Count > 0)
                    {
                        // one short line a cell: "충격음 · 대현관" (the loudest sound in that half hour)
                        var h0 = hs.OrderByDescending(h => h.Loud).ThenBy(h => h.Clock).First();
                        string rn = S.RoomName(h0.GuessRoom); rn = rn != null && rn.Length > 4 ? rn.Substring(0, 4) : rn;
                        var st = UIKit.Text(r, "C" + c, $"<color=#E8C170>{SoundShort(h0.Kind)}</color> <color=#9A8E7C>· {rn}</color>", 15, Pal.Text, TextAlignmentOptions.Center, Fonts.Body, new Vector2(NameW + colW * c, 0), new Vector2(NameW + colW * (c + 1), 1), new Vector2(2, 0), new Vector2(-2, 0));
                        st.textWrappingMode = TextWrappingModes.NoWrap; st.enableAutoSizing = true; st.fontSizeMin = 13; st.fontSizeMax = 15; st.overflowMode = TextOverflowModes.Ellipsis;
                    }
                }
                y += RH + 4;
            }
            if (unknown.Count > 0)
            {
                var ub = UIKit.Button(content, _tlUnknownOpen ? $"아직 모르는 사람 {unknown.Count}명  {Dim("· 접기")}" : $"아직 모르는 사람 {unknown.Count}명 ▶", () => { _tlUnknownOpen = !_tlUnknownOpen; Rebuild(); }, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -y - 44), new Vector2(-12, -y), 18, 8, Fonts.Body);
                ub.Label.color = Pal.TextDim; y += 50;
                if (_tlUnknownOpen) Note(content, ref y, string.Join(" · ", unknown.Select(Cast.GivenOf)) + "\n" + Dim("이 사람들이 그때 어디 있었는지는 아직 전혀 모른다."), 18);
            }
            Height(content, y + 20);
            // a faint band over the columns of the estimated time of death (drawn over the rows, never catching clicks)
            if (_bandFrom >= 0) { var band = UIKit.Img(area, "DeathBand", Pal.A(Pal.Blood, 0.14f), new Vector2(_bandFrom, 0), new Vector2(_bandTo, 1), new Vector2(-12 * _bandFrom, 0), new Vector2(-12 * _bandTo, 0)); band.raycastTarget = false; }
            if (foundX >= 0)
            {
                var fl = UIKit.Img(area, "FoundLine", Pal.A(Pal.Gold, 0.6f), new Vector2(foundX, 0), new Vector2(foundX, 1), new Vector2(-12 * foundX - 1, 0), new Vector2(-12 * foundX + 1, 0)); fl.raycastTarget = false;
            }
            if (people.Count == 0) { DetailText("누가 어디에 있었는지 아직 적을 게 없다.\n\n사람들을 직접 보거나, 수사 때 사건 이야기를 물으면 한 줄씩 채워진다.", 22); return; }

            // the selected person's accounts, in order, and any real conflict
            var list = facts.Where(f => f.who == _tlPerson).OrderBy(f => f.a).ToList();
            var sb = new System.Text.StringBuilder();
            sb.Append($"<size=32><b>{Cast.NameOf(_tlPerson)}</b></size>\n{Dim(ClockFmt.AnchorRange(t0, found > 0 ? found : t1, now) + (found > 0 ? " · 시신 발견 전까지" : ""))}\n\n");
            foreach (var f in list)
                sb.Append($"{(f.style == 0 ? Gold("●") + " 직접 봄" : Dim("○") + " " + f.src)}  {Dim(ClockFmt.AnchorRange(f.a, f.b, now))} · {S.RoomName(f.room)}{(f.note != null ? " — " + f.note : "")}\n");
            foreach (var cf in conflicts.Where(cf => cf.Who == _tlPerson))
                sb.Append($"\n<color=#E0606A><b>!</b> 두 사람의 말이 어긋난다</color>\n· {cf.A}\n· {cf.B}\n");
            if (list.Count == 0) sb.Append(Dim("이 무렵 어디 있었는지 아는 게 없다.") + "\n");
            sb.Append($"\n<size=18>{Dim($"{Gold("●")} 내가 직접 본 것 · ○ 들은 말 · <color=#E0606A>!</color> 두 사람의 말이 어긋남")}</size>");
            DetailText(sb.ToString(), 21);
            _sel = _tlPerson;
        }

        // ================================================================ 3 인물
        void People()
        {
            var content = List(_body); float y = 0;
            bool inCase = CaseProgress.Current(S) != null;
            var k = S.K(Cast.Player);
            // the person in the detail pane is always the marked row
            if (_sel == null || Cast.Get(_sel) == null || Cast.Get(_sel).IsPlayer) _sel = Cast.All.FirstOrDefault(c => !c.IsPlayer)?.Id;
            foreach (var c in Cast.All.Where(c => !c.IsPlayer))
            {
                var a = S.A(c.Id); var cc = c;
                string st = a == null ? "" : a.Status == ActorStatus.Dead && k.KnownDead.Contains(c.Id) ? "  <size=85%><color=#E3A4A4>· 사망</color></size>" : a.Status == ActorStatus.Executed ? "  <size=85%><color=#E3A4A4>· 처형</color></size>" : a.Status == ActorStatus.Escaped ? "  <size=85%><color=#D6AD62>· 탈출</color></size>" : "";
                string last = k.LastSeen.TryGetValue(c.Id, out var ls) ? $"  <size=16>{Dim($"{S.RoomName(ls.room)}에서 봄 · {Ago(S.Clock - ls.t)}")}</size>" : "";
                var b = RowAt(content, ref y, c.Id, $"{c.Name}{st}{last}", () => { _sel = cc.Id; Rebuild(); }, 52, 21, null, Goth.ChipTone.Plain, c.Id == _sel);
                if (inCase && a != null && a.Alive && !c.IsButler)
                {
                    bool asked = Safe(() => CaseBoard.Asked(Sim, c.Id), false);
                    var chip = Goth.KindChip(b.transform, asked ? "물어봄" : "아직", asked ? Goth.ChipTone.Plain : Goth.ChipTone.Gold, 0, 14, 24);
                    chip.anchorMin = chip.anchorMax = new Vector2(1, 0.5f); chip.pivot = new Vector2(1, 0.5f); chip.anchoredPosition = new Vector2(-14, 0);
                    b.Label.rectTransform.offsetMax = new Vector2(-chip.sizeDelta.x - 24, 0);
                }
            }
            Height(content, y + 20);
            ShowPerson(Cast.Get(_sel ?? Cast.All.FirstOrDefault(c => !c.IsPlayer)?.Id ?? "P02"));
        }

        static readonly System.Text.RegularExpressions.Regex MemStamp = new System.Text.RegularExpressions.Regex(@"^(\d+)일차 (\d{1,2}):(\d{2}) (.*)$");
        /// <summary>"1일차 07:43 부탁을 들어주겠다고 했다" → "어제 아침 7시 40분쯤 — 부탁을 들어주겠다고 했다" (plain times, like the rest of the notebook).</summary>
        string PlainMemory(string m)
        {
            var x = MemStamp.Match(m ?? ""); if (!x.Success) return m;
            double t = (int.Parse(x.Groups[1].Value) - 1) * 1440.0 + int.Parse(x.Groups[2].Value) * 60 + int.Parse(x.Groups[3].Value);
            return $"{Dim(ClockFmt.Anchor(t, S.Clock))}  {x.Groups[4].Value}";
        }

        void ShowPerson(CastDef c)
        {
            UIKit.Clear(_detail); if (c == null) return;
            var k = S.K(Cast.Player); var sb = new System.Text.StringBuilder();
            sb.Append($"<b><size=38>{c.Name}</size></b>  {Dim($"{c.HeightCm}cm · {c.Job}")}\n");
            if (!c.IsPlayer && !c.IsButler)
            {
                var r = S.R(c.Id, Cast.Player); var rp = S.R(Cast.Player, c.Id);
                if (CaseProgress.Current(S) != null && S.A(c.Id)?.Alive == true) sb.Append(Safe(() => CaseBoard.Asked(Sim, c.Id), false) ? Dim("사건 이야기를 물어봤다") + "\n" : Gold("아직 사건 이야기를 묻지 않았다") + "\n");
                if (k.LastSeen.TryGetValue(c.Id, out var ls)) sb.Append($"<color=#9CC4B2>마지막으로 본 곳</color> {S.RoomName(ls.room)} · {ClockFmt.Anchor(ls.t, S.Clock)}\n");
                // where you have usually seen them at each part of the day (your own sightings only)
                {
                    string Block(double t) { int m = (int)(t % 1440); return m < 12 * 60 ? "오전" : m < 18 * 60 ? "오후" : "저녁"; }
                    var habits = k.Sightings.Where(s => s.Target == c.Id && s.Direct && !s.Dead && s.Room >= 0)
                        .GroupBy(s => Block(s.T0)).Select(g => (block: g.Key, room: g.GroupBy(s => s.Room).OrderByDescending(x => x.Count()).ThenBy(x => x.Key).First(), n: g.Count()))
                        .Where(x => x.room.Count() >= 2).OrderBy(x => x.block == "오전" ? 0 : x.block == "오후" ? 1 : 2).ToList();
                    if (habits.Count > 0) sb.Append("<color=#9CC4B2>자주 보이는 곳</color> " + string.Join(" · ", habits.Select(h => $"{h.block} {S.RoomName(h.room.Key)}")) + "\n");
                }
                {
                    int stage = _s.Sim.BondStage(c.Id);
                    sb.Append($"{Gold("유대")} <color=#E8C170>{new string('●', Math.Min(4, stage))}</color><color=#5A4E44>{new string('○', Math.Max(0, 4 - stage))}</color>\n");
                    foreach (var note in k.Facts.Where(f => f.StartsWith("bondnote:" + c.Id + ":")).Select(f => f.Substring(("bondnote:" + c.Id + ":").Length)).Distinct())
                        sb.Append($"{Dim("· " + note)}\n");
                }
                var likes = k.Facts.Where(f => f.StartsWith("likes:" + c.Id + ":")).Select(f => f.Split(':')[2]).Distinct().ToList();
                if (likes.Count > 0) sb.Append($"{Gold("좋아하는 것")} {string.Join(", ", likes)}\n");
                if (k.Facts.Contains("contract:" + c.Id)) sb.Append($"<color=#C04A55>알게 된 계약</color> {c.Contract}\n");
                if (k.Facts.Contains("secret:" + c.Id)) sb.Append($"<color=#C04A55>알게 된 비밀</color> {c.Secret}\n");
                else if (k.Facts.Contains("hint:" + c.Id)) sb.Append("<color=#C04A55>뭔가 숨기는 게 있어 보인다</color>\n");
                if (rp.Tags.Contains("lover")) sb.Append("<color=#C04A55>서로 마음을 확인했다</color>\n");
                if (rp.Casual) sb.Append($"{Dim("서로 말을 놓는 사이")}\n");
                var sawPower = k.Facts.Where(f => f.StartsWith("saw-power:" + c.Id + ":")).Select(f => f.Split(':')[2]).Distinct().ToList();
                if (sawPower.Count > 0) sb.Append($"{Gold("눈치챈 권능의 징후")} {string.Join(", ", sawPower.Select(x => Abilities.Get(x)?.Kor).Where(x => x != null))}\n");
                var mem = rp.Memory.Concat(r.Memory).Distinct().TakeLast(10).ToList();
                if (mem.Count > 0) sb.Append($"\n{Gold("함께한 일")}\n" + string.Join("\n", mem.Select(m => "· " + PlainMemory(m))) + "\n");
                var said = k.Statements.Where(s => s.Speaker == c.Id).TakeLast(6).ToList();
                if (said.Count > 0) sb.Append($"\n{Gold("이 사람이 한 말")}\n" + string.Join("\n", said.Select(s => $"· {Dim(ClockFmt.Anchor(s.Clock, S.Clock))} “{LineBank.Pages(s.Text).FirstOrDefault()}”")) + "\n");
            }
            else if (c.IsButler) sb.Append("\n어항 머리를 한 집사. 규칙을 알리고, 저택 시설을 돌보고, 죽음을 확인하고, 심판을 진행한다. 자기 눈으로 확인한 지금의 상태만 틀림없다고 말한다.");
            DetailText(sb.ToString());
        }

        // ================================================================ 4 지도
        static string FloorName(int f) => f > 0 ? $"{f + 1}층" : f == 0 ? "1층" : f == -1 ? "지하" : $"지하 {-f}층";

        void Map()
        {
            var k = S.K(Cast.Player); var inc = CaseProgress.Current(S);
            var floors = S.Layout.Floors.Select(x => x.F).Where(f => f == _mapFloor || S.Layout.Rooms.Any(r => r.Floor == f && k.Facts.Contains("visited:" + r.Id))).OrderByDescending(f => f).ToList();
            if (!floors.Contains(_mapFloor)) _mapFloor = floors.FirstOrDefault();
            var bar = UIKit.Rect(_body, "Floors", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50), new Vector2(-12, 0));
            for (int i = 0; i < floors.Count; i++)
            {
                int f = floors[i]; var fb = UIKit.Button(bar, FloorName(f) + (S.Player != null && S.Player.Pos.f == f ? $"  <size=75%>{Dim("여기")}</size>" : ""), () => { _mapFloor = f; _mapRoom = -1; Rebuild(); }, new Vector2(i / (float)floors.Count, 0), new Vector2((i + 1) / (float)floors.Count, 1), new Vector2(2, 0), new Vector2(-2, 0), 20);
                if (f == _mapFloor) { fb.Idle = Pal.MagentaDim; fb.Paint(); }
            }
            // one-line legend: the colours mean one thing each (red is only ever the scene)
            var legend = UIKit.Text(_body, "Legend", $"<color=#C0404C>■</color> 현장   <color=#8F7FB8>■</color> 수수께끼 방   <color=#7FB0A0>■</color> 방   <color=#C8A0D0>▌</color> 개인실(주인 색)   <color=#8A8070>■</color> 복도·홀   {Gold("나")} 지금 위치", 16, Pal.TextDim, TextAlignmentOptions.MidlineLeft, Fonts.Body, new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, -88), new Vector2(-12, -56));
            legend.textWrappingMode = TextWrappingModes.NoWrap;
            var area = UIKit.Rect(_body, "Map", Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-12, -92));
            var fi = S.Layout.Floor(_mapFloor); if (fi == null) return;
            UIKit.Img(area, "Bg", Pal.A(Pal.Ink, 0.7f), Vector2.zero, Vector2.one);
            float W = Mathf.Max(1f, fi.Bounds.W), D = Mathf.Max(1f, fi.Bounds.D);
            Vector2 N(float x, float z) => new Vector2((x - fi.Bounds.x0) / W, (z - fi.Bounds.z0) / D);
            // case cards per room (only cards that belong to the case), pins, and who was last seen where
            var cards = inc != null ? Safe(() => CaseBoard.Cards(Sim), new List<CaseBoard.View>()).Where(v => v.InCase && v.Ev != null && v.Ev.Room >= 0).ToList() : new List<CaseBoard.View>();
            var perRoom = cards.GroupBy(v => v.Ev.Room).ToDictionary(g => g.Key, g => g.Count());
            int scene = inc != null && inc.FoundRoom >= 0 ? inc.FoundRoom : -1;
            var appt = (S.Requests ?? new List<Request>()).Where(r => r.Kind == "invite" && r.State == "accepted" && r.Room >= 0).OrderBy(r => r.At).FirstOrDefault();
            int mine = S.Layout.BedroomOf(Cast.Player)?.Id ?? -1;
            var seenBy = k.LastSeen.Where(kv => S.Clock - kv.Value.t <= 240 && kv.Key != Cast.Player && S.A(kv.Key) != null && S.A(kv.Key).Alive).GroupBy(kv => kv.Value.room).ToDictionary(g => g.Key, g => g.Select(x => x.Key).OrderBy(x => Cast.NameOf(x), StringComparer.Ordinal).ToList());
            foreach (var r in S.Layout.Rooms.Where(r => r.Floor == _mapFloor && !r.Void).OrderBy(r => r.Id))
            {
                bool known = k.Facts.Contains("visited:" + r.Id) || r.Id == scene || r.Id == mine || (appt != null && appt.Room == r.Id);
                if (!known) continue; // unknown rooms are not drawn (no hidden outlines)
                bool passage = RoomInfo.IsPassage(r.Type); bool sel = r.Id == _mapRoom;
                // red is the scene only; rooms of the house's mysteries are violet; the selected room gets a gold outline
                var col = r.Id == scene ? Pal.A(Pal.Blood, 0.6f) : passage ? Pal.A(Pal.TextDim, 0.22f) : RoomInfo.IsMystery(r.Type) ? Pal.A(new Color(0.46f, 0.38f, 0.66f), 0.4f) : Pal.A(Pal.Cyan, 0.26f);
                bool bedroom = r.Type == RoomType.Bedroom && !string.IsNullOrEmpty(r.Owner) && r.Id != scene;
                if (bedroom) col = Pal.A(CastColors.Of(r.Owner), 0.32f);   // each resident's room in their own colour (owner: 개인실 구별이 편하게)
                if (sel) col = Color.Lerp(col, Pal.A(Pal.Text, col.a + 0.1f), 0.18f);
                var img = UIKit.Img(area, "R" + r.Id, col, N(r.Rect.x0, r.Rect.z0), N(r.Rect.x1, r.Rect.z1), new Vector2(1, 1), new Vector2(-1, -1)); img.raycastTarget = true;
                if (bedroom) UIKit.Img(img.rectTransform, "Owner", Pal.A(CastColors.Of(r.Owner), 0.95f), new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(4, 0)).raycastTarget = false;
                int rid = r.Id; img.gameObject.AddComponent<NoteClick>().OnClick = () => { _mapRoom = rid; _sel = "room:" + rid; Rebuild(); };
                var rt = img.rectTransform;
                if (sel)
                    foreach (var (a0, a1, o0, o1) in new[] { (new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 2)), (new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2), new Vector2(0, 0)), (new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(2, 0)), (new Vector2(1, 0), new Vector2(1, 1), new Vector2(-2, 0), new Vector2(0, 0)) })
                        UIKit.Img(rt, "Sel", Pal.A(Pal.Gold, 0.95f), a0, a1, o0, o1).raycastTarget = false;
                // gold word pins: 현장 · 약속 · 내 방 — drawn for every room, the grand hall and corridors included
                var pins = new List<string>(); if (r.Id == scene) pins.Add("현장"); if (appt != null && appt.Room == r.Id) pins.Add("약속"); if (r.Id == mine) pins.Add("내 방");
                bool marked = pins.Count > 0 || perRoom.ContainsKey(r.Id) || seenBy.ContainsKey(r.Id) || sel;
                // the name: always on rooms, on a passage only when it is big (the grand hall) or something is marked there
                if (!passage || Math.Min(r.Rect.W, r.Rect.D) >= 6 || marked)
                {
                    var nt = UIKit.Text(rt, "N", r.Name, 17, Pal.Text, TextAlignmentOptions.Center, Fonts.Bold, Vector2.zero, Vector2.one, new Vector2(3, 18), new Vector2(-3, -3));
                    nt.enableAutoSizing = true; nt.fontSizeMin = 15; nt.fontSizeMax = 17; nt.overflowMode = TextOverflowModes.Overflow; nt.outlineWidth = 0.18f; nt.outlineColor = new Color32(12, 8, 6, 255);
                }
                float px = 3; foreach (var p in pins) { var ch = Goth.KindChip(rt, p, Goth.ChipTone.Gold, px, 13, 21); ch.anchorMin = ch.anchorMax = new Vector2(0, 0); ch.pivot = new Vector2(0, 0); ch.anchoredPosition = new Vector2(px, 3); px += ch.sizeDelta.x + 3; }
                if (perRoom.TryGetValue(r.Id, out var n)) UIKit.Text(rt, "Cards", $"단서 {n}", 14, Pal.Gold, TextAlignmentOptions.BottomRight, Fonts.Bold, Vector2.zero, Vector2.one, new Vector2(2, 3), new Vector2(-4, -2)).textWrappingMode = TextWrappingModes.NoWrap;
                if (seenBy.TryGetValue(r.Id, out var who))
                {
                    string label = who.Count == 1 ? Cast.GivenOf(who[0]) : $"{who.Count}명";
                    var ch = Goth.KindChip(rt, label, Goth.ChipTone.Plain, 0, 13, 20); ch.anchorMin = ch.anchorMax = new Vector2(1, 1); ch.pivot = new Vector2(1, 1); ch.anchoredPosition = new Vector2(-3, -3);
                }
            }
            var me = S.Player; if (me != null && me.Pos.f == _mapFloor) { var p = N(me.Pos.x, me.Pos.z); var mc = Goth.KindChip(area, "나", Goth.ChipTone.Gold, 0, 13, 20); mc.anchorMin = mc.anchorMax = p; mc.pivot = new Vector2(0.5f, 0.5f); mc.anchoredPosition = Vector2.zero; }

            if (_mapRoom >= 0 && S.Layout.Room(_mapRoom) != null)
            {
                var room = S.Layout.Room(_mapRoom); var sb = new System.Text.StringBuilder($"<size=32>{room.Name}</size>\n{Dim(FloorName(room.Floor))}\n\n");
                if (room.Id == scene) sb.Append(Gold("현장") + " — 시신이 발견된 곳\n");
                if (appt != null && appt.Room == room.Id) sb.Append(Gold("약속") + $" — {Cast.GivenOf(appt.From)}, {ClockFmt.Anchor(appt.At, S.Clock)}\n");
                if (room.Id == mine) sb.Append(Gold("내 방") + "\n");
                var here = k.LastSeen.Where(kv => kv.Value.room == room.Id && kv.Key != Cast.Player).OrderByDescending(kv => kv.Value.t).ToList();
                // a short list (the scene after a discovery can hold the whole house): names on one line when there are many
                string seenText = here.Count == 0 ? Dim("없음") + "\n"
                    : here.Count <= 4 ? string.Join("\n", here.Select(kv => $"· {Cast.GivenOf(kv.Key)} {Dim(ClockFmt.Anchor(kv.Value.t, S.Clock))}")) + "\n"
                    : string.Join(" · ", here.Take(8).Select(kv => Cast.GivenOf(kv.Key))) + (here.Count > 8 ? Dim($" 외 {here.Count - 8}명") : "") + "\n" + Dim($"가장 최근: {ClockFmt.Anchor(here[0].Value.t, S.Clock)}") + "\n";
                sb.Append("\n" + Gold("마지막으로 여기서 본 사람") + "\n" + seenText);
                var rc = cards.Where(v => v.Ev.Room == room.Id).ToList();
                if (rc.Count > 0) sb.Append($"\n{Gold($"이 방에서 나온 사건 단서 {rc.Count}개")}\n" + string.Join("\n", rc.Take(5).Select(v => "· " + CluePicker.Plain(v.Title))) + (rc.Count > 5 ? "\n" + Dim($"· 외 {rc.Count - 5}개 — 1 단서 탭") : "") + "\n");
                var area2 = UIKit.Rect(_detail, "Info", Vector2.zero, Vector2.one, new Vector2(0, 66), Vector2.zero);
                DetailIn(area2, sb.ToString(), 21);
                bool tracking = Guide.TargetRoom == room.Id;
                UIKit.Button(_detail, tracking ? "길 안내 끄기" : "여기로 길 안내", () => { if (Guide.TargetRoom == room.Id) Guide.Clear(); else Guide.Track(room.Id, room.Name); Rebuild(); }, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 54), 22);
                return;
            }
            DetailText($"<size=30>{FloorName(_mapFloor)} 지도</size>\n\n직접 들어가 본 방만 그려진다. 사람 표시는 그 사람을 마지막으로 <b>직접 본</b> 곳이다(4시간 안).\n방을 누르면 거기서 누구를 봤는지 나오고, 그 방까지 길 안내를 받을 수 있다.\n\n{Gold("현장")} 시신이 발견된 곳 · {Gold("약속")} 약속 장소 · {Gold("내 방")}\n{Dim("단서 n")} 그 방에서 나온 사건 단서 수\n\n들어가 본 방: {k.Facts.Count(f => f.StartsWith("visited:"))}곳", 21);
        }

        // ================================================================ 5 일정·소지품
        void Schedule()
        {
            var content = List(_body); float y = 0; var me = S.Player;
            float yRules = -1, yGoals = 0, yInv = 0, yArch = 0;
            var reqs = S.Requests ?? new List<Request>();
            // 1. 약속: the next appointment, with a countdown and [추적하기]
            Header(content, ref y, "약속");
            var appt = reqs.Where(r => r.Kind == "invite" && r.State == "accepted").OrderBy(r => r.At).FirstOrDefault();
            if (appt != null)
            {
                double left = appt.At - S.Clock; string cd = left > 1 ? (left < 60 ? $"{(int)Math.Ceiling(left)}분 뒤" : $"{(int)(left / 60)}시간 {(int)(left % 60)}분 뒤") : "지금 가야 한다";
                var ar = appt; var b = RowAt(content, ref y, "appt:" + appt.Id, $"{Cast.GivenOf(appt.From)} — {S.RoomName(appt.Room)}, {ClockFmt.Anchor(appt.At, S.Clock)}  <size=16>{Gold(cd)}</size>", () => { _sel = "appt:" + ar.Id; Rebuild(); }, 54, 20, null, Goth.ChipTone.Plain, _sel == "appt:" + appt.Id);
                bool tracking = Guide.TargetRoom == appt.Room;
                UIKit.Button(b.transform, tracking ? "안내 중" : "길 안내", () => { if (Guide.TargetRoom == ar.Room) Guide.Clear(); else Guide.Track(ar.Room, S.RoomName(ar.Room)); Rebuild(); }, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-150, 6), new Vector2(-10, -6), 17, 8, Fonts.Body);
                b.Label.rectTransform.offsetMax = new Vector2(-160, 0);
            }
            else Note(content, ref y, "잡아 둔 약속이 없다.");
            // 2. 이번 챕터 규칙 (only when there are any)
            if (S.Ch.Rules.Count > 0)
            {
                yRules = y; Header(content, ref y, "이번 챕터 규칙");
                foreach (var r in S.Ch.Rules) { var rr = r; RowAt(content, ref y, "rule:" + r.Rule, $"{r.Name}{(r.Active ? "" : "  " + Dim("(끝남)"))}", () => { _sel = "rule:" + rr.Rule; Rebuild(); }, 48, 20, null, Goth.ChipTone.Plain, _sel == "rule:" + r.Rule); }
            }
            // 3. 목표 — promises, favours, goals, invitations, things to give back
            yGoals = y; Header(content, ref y, "목표");
            foreach (var r in reqs.Where(r => r.State == "accepted" || r.State == "offered" || ((r.State == "met" || r.State == "done" || r.State == "missed") && S.Clock - r.Made < 24 * 60)).OrderBy(r => r.State == "accepted" ? 0 : 1).ThenBy(r => r.At))
            {
                string mark = r.State == "met" || r.State == "done" ? "● " : r.State == "missed" ? Dim("놓침") + " " : r.State == "offered" ? "… " : "▶ ";
                string what = r.Kind == "invite" ? $"{Gold("약속")} {Cast.GivenOf(r.From)} — {S.RoomName(r.Room)}, {ClockFmt.Anchor(r.At, S.Clock)}"
                            : r.Kind == "find" ? $"{Gold("부탁")} {Cast.GivenOf(r.From)}의 {S.I(r.Item)?.Kor ?? "물건"} 찾아 주기"
                            : $"{Gold("부탁")} {Cast.GivenOf(r.To)}에게 {Cast.GivenOf(r.From)}의 쪽지 전하기";
                RowAt(content, ref y, null, mark + what + (r.State == "offered" ? $"  <size=80%>{Dim("(아직 대답하지 않음)")}</size>" : ""), () => { }, 50, 19);
            }
            foreach (var g in S.Goals.Values.Where(g => g.Owner == Cast.Player))
            {
                var gg = g;
                RowAt(content, ref y, null, (g.Done ? "● " : g.PlayerAccepted ? "▶ " : "○ ") + g.Label + (g.PlayerAccepted ? $"  ({g.Stage}/{g.Stages})" : $"  {Dim("— 눌러서 맡기")}"), () => { _s.Sim.PlayerAcceptGoal(gg.Id); Rebuild(); }, 50, 19);
            }
            foreach (var g in S.Gatherings.Where(g => !g.Cancelled && g.KnownRev.TryGetValue(Cast.Player, out var kr) && kr >= 0))
            {
                var R = g.Revs[g.KnownRev[Cast.Player]];
                string when = g.Done ? "(끝남)" : S.Clock < R.Start ? $"{ClockFmt.Anchor(R.Start, S.Clock)}부터" : S.Clock <= R.End ? "(진행 중)" : "(시간 지남)";
                RowAt(content, ref y, null, $"{Gold("초대")} {Cast.GivenOf(g.Host)}의 {g.Label} — {S.RoomName(R.Room)} {when}", () => { }, 50, 19);
            }
            if (me != null) foreach (var it in _s.Sim.Carried(me).Where(x => x.Owner != null && x.Owner != Cast.Player && x.KeyFor == null && x.Type != "Invitation"))
                RowAt(content, ref y, null, $"<color=#9CC4B2>남의 물건</color> {it.Kor} — {Cast.GivenOf(it.Owner)}에게 돌려줄 수 있다", () => { }, 50, 19);
            foreach (var g in S.Goals.Values.Where(g => g.Owner != Cast.Player && (g.Helpers.Contains(Cast.Player) || S.K(Cast.Player).Sightings.Count(s => s.Target == g.Owner) > 10)).Take(10))
                RowAt(content, ref y, null, $"{Dim(Cast.GivenOf(g.Owner) + "의 일:")} {g.Label} {(g.Done ? "(끝냄)" : g.Abandoned ? "(그만둠)" : "")}", () => { }, 50, 19);
            if (y - yGoals < 50) Note(content, ref y, "지금은 따로 할 일이 없다. 조용한 날도 나쁘지 않다 — T로 시간을 보낼 수 있다.");
            // 4. 소지품
            yInv = y; Header(content, ref y, "소지품");
            var carried = me != null ? _s.Sim.Carried(me).ToList() : new List<Item>();
            // --- concealment: in a hand (everyone sees it) or where on the body it is hidden; then what 민혁 hid (or saw hidden) in the house
            foreach (var it in carried.OrderBy(i => me.HandR == i.Id || me.HandL == i.Id ? 0 : Concealment.SlotOf(S, i) == BodySlot.Coat ? 1 : Concealment.SlotOf(S, i) == BodySlot.Bag ? 2 : 3).ThenBy(i => i.Id, StringComparer.Ordinal))
            {
                var x = it; bool hand = me.HandR == it.Id || me.HandL == it.Id;
                string where = me.HandR == it.Id ? "오른손 — 보인다" : me.HandL == it.Id ? "왼손 — 보인다" : Concealment.SlotWord(me.Id, Concealment.SlotOf(S, it)) + " — 숨김";
                RowAt(content, ref y, "item:" + it.Id, $"{it.Kor}  <size=16>{(hand ? Gold(where) : Dim(where))}{Dim(it.Bloody ? " · 붉은 얼룩" : "")}</size>", () => { _sel = "item:" + x.Id; Rebuild(); }, 50, 20, null, Goth.ChipTone.Plain, _sel == "item:" + it.Id);
            }
            if (carried.Count == 0) Note(content, ref y, "가진 것이 없다.");
            {
                var stashes = Concealment.Remembered(S);
                if (stashes.Count > 0)
                {
                    Header(content, ref y, "숨겨 둔 물건");
                    foreach (var (sit, sf, by) in stashes)
                    {
                        var xi = sit; string who = by != null && by != Cast.Player ? " · " + LineBank.FixParticles($"{Cast.GivenOf(by)}이(가) 숨기는 걸 봤다") : "";
                        RowAt(content, ref y, "stash:" + sit.Id, $"{sit.Kor}  <size=16>{Dim(Concealment.PlaceName(S, sf) + who)}</size>", () => { _sel = "stash:" + xi.Id; Rebuild(); }, 50, 20, null, Goth.ChipTone.Plain, _sel == "stash:" + sit.Id);
                    }
                }
            }
            // --- concealment (end)
            // 5. 지난 사건 (collapsed) and the house rules
            yArch = y; y += 6;
            var ab = UIKit.Button(content, _archiveOpen ? $"지난 사건 ({S.Settlements.Count})  {Dim("· 접기")}" : $"지난 사건 ({S.Settlements.Count}) ▶", () => { _archiveOpen = !_archiveOpen; _sel = _archiveOpen ? "archive" : null; Rebuild(); }, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -y - 46), new Vector2(-12, -y), 20, 8, Fonts.Bold);
            ab.Label.color = Pal.Gold; y += 52;
            if (_archiveOpen)
            {
                foreach (var st in S.Settlements)
                    Note(content, ref y, $"{Gold($"루프 {st.Loop} · 챕터 {st.Chapter}")}  지목 {Cast.NameOf(st.Accused)} → {(st.Exception ? st.Note : st.Correct ? "맞혔다" : "빗나갔다")}", 18, 30);
                if (S.Settlements.Count == 0) Note(content, ref y, "아직 끝난 사건이 없다.", 18);
            }
            RowAt(content, ref y, "house", $"저택의 규칙과 권능 ▶", () => { _sel = "house"; Rebuild(); }, 48, 19, null, Goth.ChipTone.Plain, _sel == "house");
            Height(content, y + 20);
            // jump to the section asked for (J → 목표, I → 소지품, …)
            if (_section != null)
            {
                float jy = _section == "goals" ? yGoals : _section == "inventory" ? yInv : _section == "rules" ? (yRules >= 0 ? yRules : yArch) : _section == "archive" ? yArch : 0;
                if (_section == "rules" && yRules < 0) _sel = "house";
                Canvas.ForceUpdateCanvases(); SetScroll(jy); _section = null;
            }
            ScheduleDetail(carried);
        }

        void ScheduleDetail(List<Item> carried)
        {
            var me = S.Player;
            if (_sel != null && _sel.StartsWith("item:")) { var it = carried.FirstOrDefault(x => "item:" + x.Id == _sel); if (it != null) { ShowItem(it); return; } }
            if (_sel != null && _sel.StartsWith("stash:")) { var st = Concealment.Remembered(S).FirstOrDefault(x => "stash:" + x.it.Id == _sel); if (st.it != null) { ShowStash(st.it, st.f, st.by); return; } }   // --- concealment
            if (_sel != null && _sel.StartsWith("rule:"))
            {
                var r = S.Ch.Rules.FirstOrDefault(x => "rule:" + x.Rule == _sel);
                if (r != null) { DetailText($"<size=32>{r.Name}</size>{(r.Active ? "" : "  " + Dim("(끝남)"))}\n\n{r.Desc}\n\n{Dim($"이번 챕터 희생자는 최대 {S.Ch.VictimCap}명 · 시작 인원 {S.Ch.StartN}명")}", 22); return; }
            }
            if (_sel == "house") { HouseRules(); return; }
            if (_sel == "archive") { Archive(); return; }
            // today at a glance
            var sb = new System.Text.StringBuilder($"<size=30>오늘</size>\n{Dim(ClockFmt.Stamp(S.Clock))}\n\n");
            var appt = (S.Requests ?? new List<Request>()).Where(r => r.Kind == "invite" && r.State == "accepted").OrderBy(r => r.At).FirstOrDefault();
            if (appt != null) sb.Append($"{Gold("다음 약속")} {Cast.GivenOf(appt.From)} — {S.RoomName(appt.Room)}, {ClockFmt.Anchor(appt.At, S.Clock)}\n");
            if (Guide.TargetRoom >= 0) sb.Append($"{Gold("길 안내 중")} {Guide.TargetLabel ?? S.RoomName(Guide.TargetRoom)}\n");
            sb.Append($"\n{Dim("E 줍기 · G 내려놓기 (길게: 던지기) · T 시간 보내기")}\n{Dim("M 지도 · J 목표 · I 소지품")}");
            DetailText(sb.ToString(), 22);
        }

        void HouseRules()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<size=30>저택의 규칙</size>\n");
            sb.Append("· 시신은 세 명 이상이 직접 봐야 발견 안내가 울리고 수사가 시작된다. 먼저 본 사람은 다른 사람을 불러와야 한다.\n· 심판에서 들키지 않은 범인은 소원을 이루고 저택을 떠난다.\n· 투표로 범인을 맞히면 범인이 처형된다. 틀리면 범인은 빠져나가고, 남은 사람 중 한 명이 공개 추첨으로 처형된다.\n· 사건이 일어나면 남은 인원이 적더라도 수사와 심판, 뒤처리를 먼저 마친다.\n· 남은 인원이 기준 이하로 줄면 세계는 처음으로 돌아간다.\n");
            sb.Append($"\n{Dim($"{S.FloorLocked}명 이하가 되면 루프가 끝난다")}\n");
            sb.Append($"\n<size=30>권능</size>\n{Dim("누가 어떤 권능을 가졌는지는 공개되지 않는다.")}\n\n");
            foreach (var ab in Abilities.Catalog) sb.Append($"<color=#C04A55>{ab.Kor}</color> {Dim("(" + ab.God + ")")}\n{ab.PublicRule}\n{Dim($"한계: {ab.Limit} · 징후: {ab.Sign}")}\n\n");
            sb.Append($"<color=#9CC4B2>민혁</color> — {Abilities.MinhyukSupport}");
            DetailText(sb.ToString(), 20);
        }

        void ShowItem(Item it)
        {
            UIKit.Clear(_detail);
            var area = UIKit.Rect(_detail, "Info", Vector2.zero, Vector2.one, new Vector2(0, 66), Vector2.zero);
            // --- concealment: where it is (seen in a hand, or hidden on the body) and what can be done with it
            var me = S.Player; bool hand = me.HandR == it.Id || me.HandL == it.Id; var slot = Concealment.SlotOf(S, it);
            string whereLine = hand ? Gold("손에 들고 있다 — 누구에게나 보인다") : Dim($"{Concealment.SlotWord(me.Id, slot)}에 숨겨 두었다");
            var (clabel, ccan, _) = hand ? _s.Sim.ConcealPrompt(it) : (null, false, BodySlot.None);
            string last = Concealer.I?.LastText != null && Concealer.I.LastItem == it.Id ? "\n\n" + Concealer.I.LastText : "";
            DetailIn(area, $"<size=32>{it.Kor}</size>\n{whereLine}\n\n{(it.Def?.IsWeapon == true ? "<color=#C04A55>흉기로 쓸 수 있다</color>\n" : "")}{(it.Bloody ? "붉은 얼룩이 묻어 있다\n" : "")}{(it.Washed ? "씻은 흔적이 있다\n" : "")}{(it.Owner != null ? "원래 주인: " + Cast.NameOf(it.Owner) + "\n" : "")}{(hand && !ccan && clabel != null ? Dim(clabel) + "\n" : "")}\n{Dim("Q 품에 숨기기·꺼내기 (길게: 고르기) · 숨길 곳을 보며 E 넣어 두기 · G 내려놓기 (길게: 던지기)")}{last}", 22);
            float third = 1f / 3f;
            if (hand) { var hb = UIKit.Button(_detail, ccan ? clabel : "숨길 수 없다", () => { Concealer.I?.Conceal(it); Rebuild(); }, new Vector2(0, 0), new Vector2(third, 0), new Vector2(0, 0), new Vector2(-6, 54), 22); hb.Interactable = ccan; hb.Paint(); }
            else UIKit.Button(_detail, "꺼내기", () => { Concealer.I?.Draw(it); Rebuild(); }, new Vector2(0, 0), new Vector2(third, 0), new Vector2(0, 0), new Vector2(-6, 54), 22);
            UIKit.Button(_detail, "내려놓기", () => { _s.Sim.PlayerDrop(it, S.Player.Pos); _sel = null; Rebuild(); }, new Vector2(third, 0), new Vector2(2 * third, 0), new Vector2(6, 0), new Vector2(-6, 54), 22);
            if (it.Surface.Any(s => s.StartsWith("secret:"))) UIKit.Button(_detail, "읽기", () => { _s.Sim.PlayerPickUp(it); Rebuild(); }, new Vector2(2 * third, 0), new Vector2(1, 0), new Vector2(6, 0), new Vector2(0, 54), 22);
        }

        /// <summary>A thing 민혁 hid (or saw someone hide) somewhere in the house: where, whose, and take it back when standing there.</summary>
        void ShowStash(Item it, Furniture f, string by)
        {
            UIKit.Clear(_detail);
            var area = UIKit.Rect(_detail, "Info", Vector2.zero, Vector2.one, new Vector2(0, 66), Vector2.zero);
            var me = S.Player; bool near = me != null && f.Pos.f == me.Pos.f && me.Pos.DistXZ(f.Pos) <= Concealment.Reach(f);
            string whose = by == null || by == Cast.Player ? "내가 숨겨 두었다" : LineBank.FixParticles($"{Cast.GivenOf(by)}이(가) 숨기는 걸 봤다");
            string last = Concealer.I?.LastText != null && Concealer.I.LastItem == it.Id ? "\n\n" + Concealer.I.LastText : "";
            DetailIn(area, $"<size=32>{it.Kor}</size>\n{Gold(Concealment.PlaceName(S, f))}\n{Dim(whose)}\n\n{(it.Def?.IsWeapon == true ? "<color=#C04A55>흉기로 쓸 수 있다</color>\n" : "")}{(it.Bloody ? "붉은 얼룩이 묻어 있다\n" : "")}\n{Dim(near ? "바로 앞이다 — 꺼낼 수 있다." : "그 자리에 가야 꺼낼 수 있다. 누가 먼저 찾아냈을 수도 있다.")}{last}", 22);
            var rb = UIKit.Button(_detail, "꺼내기", () => { Concealer.I?.Retrieve(it); Rebuild(); }, new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(-6, 54), 22); rb.Interactable = near; rb.Paint();
            bool tracking = Guide.TargetRoom == f.Room;
            UIKit.Button(_detail, tracking ? "안내 중" : "길 안내", () => { if (Guide.TargetRoom == f.Room) Guide.Clear(); else Guide.Track(f.Room, Concealment.PlaceName(S, f)); Rebuild(); }, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(6, 0), new Vector2(0, 54), 22);
        }
        // --- concealment (end)

        void Archive()
        {
            var sb = new System.Text.StringBuilder($"<size=30>지난 사건</size>\n{Dim("판결 뒤에 드러난 전말이다. 지금 사람들은 기억하지 못하고, 이번 사건의 증거로 낼 수도 없다.")}\n\n");
            foreach (var st in S.Settlements) sb.Append($"{Gold($"루프 {st.Loop} · 챕터 {st.Chapter}")} 지목 {Cast.NameOf(st.Accused)} · 진범 {Cast.NameOf(st.Culprit) ?? "없음"} → {(st.Exception ? st.Note : st.Correct ? "맞혔다" : "빗나갔다")}{(st.Executed != null ? " · 처형 " + Cast.NameOf(st.Executed) : "")}{(st.Escaped != null ? " · 탈출 " + Cast.NameOf(st.Escaped) : "")}\n");
            if (S.Archive.Count > 0) sb.Append("\n" + string.Join("\n", S.Archive.Select(a => "· " + a)) + "\n");
            sb.Append($"\n{Dim($"레벨 {S.Profile.Level} · 경험 {S.Profile.Exp} · 루프 {S.Profile.Runs}회 완료")}");
            DetailText(sb.ToString(), 20);
        }

        // ---------------------------------------------------------------- probe helpers (automation only)
        /// <summary>Probe only: 단서 with one card selected (null: nothing selected).</summary>
        public void ProbeSelect(string cardId) { _sel = cardId; _more = false; _filterQ = -1; Show("evidence"); }
        /// <summary>Probe only: open or fold the 기타 group.</summary>
        public void ProbeOther(bool open) { _otherOpen = open; _sel = open ? OtherId : null; if (open) _scrollTo = OtherId; Show("evidence"); }
        /// <summary>Probe only: 지도 on a room's floor with that room selected.</summary>
        public void ProbeMapRoom(int room) { var r = S.Layout.Room(room); Show("map"); if (r != null) { _mapFloor = r.Floor; _mapRoom = room; Rebuild(); } }
        /// <summary>Probe only (concealment): 일정·소지품 scrolled to 소지품 with a row selected ("item:id" / "stash:id").</summary>
        public void ProbeInventory(string sel) { Show("schedule"); _sel = sel; _section = "inventory"; Rebuild(); }

        // ---------------------------------------------------------------- body examination view
        /// <summary>After examining a body: 단서 with that body's card selected (the kernel folds a body's cards into one).</summary>
        public void ShowBody(Actor body, Evidence ev)
        {
            _sel = ev?.Id; _more = false; _filterQ = -1;
            Show("evidence");
            if (SelectedCard() == null && body != null)
            {
                var v = _cards.FirstOrDefault(c => c.Ev != null && c.Ev.Kind == EvKind.Body && c.Ev.Subject == body.Id) ?? _cards.FirstOrDefault(c => c.Ev != null && c.Ev.Kind == EvKind.Body);
                if (v != null) { _sel = v.Id; Rebuild(); }
            }
        }
    }

    /// <summary>The notebook lists' scroll indicator: shown only while the content is taller than the view.</summary>
    public sealed class ScrollCue : MonoBehaviour
    {
        public ScrollRect Sr; public Image Track, Thumb;
        void LateUpdate()
        {
            if (Sr == null || Sr.content == null || Track == null || Thumb == null) return;
            float view = Sr.viewport.rect.height, total = Sr.content.rect.height;
            bool on = total > view + 2 && view > 1;
            if (Track.enabled != on) { Track.enabled = on; Thumb.enabled = on; }
            if (!on) return;
            float frac = Mathf.Clamp(view / total, 0.08f, 1f), pos = Mathf.Clamp01(Sr.content.anchoredPosition.y / Mathf.Max(1f, total - view));
            float top = 1f - pos * (1f - frac), bot = top - frac;
            Thumb.rectTransform.anchorMin = new Vector2(1, bot); Thumb.rectTransform.anchorMax = new Vector2(1, top);
        }
    }

    /// <summary>A plain click target inside the notebook (map rooms, board rows).</summary>
    public sealed class NoteClick : MonoBehaviour, IPointerClickHandler
    {
        public Action OnClick;
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) { UISfx.Hover(); OnClick?.Invoke(); } }
    }
}
