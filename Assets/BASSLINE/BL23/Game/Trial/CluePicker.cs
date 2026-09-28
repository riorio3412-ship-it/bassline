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
    /// <summary>
    /// The one court picker: statements on the left, the player's clue cards on the right (six to a page, every card reachable),
    /// a preview of the selected card below the list and the moves this moment allows. Every court prompt uses it —
    /// 발언 되짚기 (Review), a decision moment (Moment), 반론 우선권 (Rebut) and 최종 변론 (Defense).
    /// Keys: ←/→ switch pane · ↑/↓ or number keys act on the focused pane (↓ past row 6 turns the page) · Enter moves to the
    /// cards, then presents · Esc steps back (cards → statements → close). Double-click a card to present it; wheel turns pages.
    /// It never judges a card: the order is the kernel's CaseBoard ranking (or the notebook order at 도움 끔).
    /// </summary>
    public sealed class CluePicker : MonoBehaviour
    {
        public enum Mode { Review, Moment, Rebut, Defense }
        public const int PerPage = 6;

        Simulation _sim; GameState S => _sim.S; Mode _mode; List<TrialClaim> _claims = new List<TrialClaim>();
        Action<string, string, string> _commit; Action _cancel;
        RectTransform _root, _leftList, _leftContent, _cardList, _preview, _buttons, _pageBar; ScrollRect _leftScroll;
        TextMeshProUGUI _pageLabel, _guide, _hintLine;
        readonly List<PickRow> _claimRows = new List<PickRow>(); readonly List<PickRow> _cardRows = new List<PickRow>();
        List<string> _order = new List<string>(); readonly HashSet<string> _dots = new HashSet<string>();
        readonly Dictionary<string, CaseBoard.View> _views = new Dictionary<string, CaseBoard.View>();
        int _claimIdx = -1, _page, _builtFrame; string _cardId, _guideCard; bool _cardsFocus, _closed; float _rowH = 60f;
        GBtn _bPrimary, _bAgree, _bSource, _bBack;

        public bool Closed => _closed;
        public string SelectedClaim => _claimIdx >= 0 && _claimIdx < _claims.Count ? _claims[_claimIdx].Id : null;
        public string SelectedCard => _cardId;
        public int CardCount => _order.Count;

        // ================================================================== which statements each mode shows
        /// <summary>Statements already heard in court (their line has played), newest first with unresolved ones on top.</summary>
        public static List<TrialClaim> ReviewClaims(TrialState T)
        {
            if (T == null) return new List<TrialClaim>();
            var all = T.Claims.Where(c => Heard(T, c) && (c.Topic != "premise" || T.GameLog.Contains("shown:" + c.Id)))
                .OrderBy(c => c.Status == "open" ? 0 : 1).ThenByDescending(c => c.Beat).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
            // the same speaker saying the very same thing about the very same fact (a repeated line) is one row
            var seen = new HashSet<string>(); var res = new List<TrialClaim>();
            foreach (var c in all)
            {
                string key = c.Speaker + "|" + c.Text + "|" + (c.Prop != null ? CaseBoard.FactKey(c.Prop) : "") + "|" + c.Accused;
                if (seen.Add(key)) res.Add(c);
            }
            return res;
        }
        /// <summary>반론 우선권: the newest two open accusations.</summary>
        public static List<TrialClaim> RebutClaims(TrialState T)
        {
            if (T == null) return new List<TrialClaim>();
            return T.Claims.Where(c => c.Accused != null && c.Status == "open" && c.Text != null).OrderByDescending(c => c.Beat).Take(2).ToList();
        }
        /// <summary>최종 변론: the statements accusing the player, and the premises holding them up.</summary>
        public static List<TrialClaim> DefenseClaims(TrialState T)
        {
            var res = new List<TrialClaim>(); if (T == null) return res;
            foreach (var c in T.Claims.Where(c => c.Accused == Cast.Player && c.Status != "retracted" && c.Text != null).OrderByDescending(c => c.Beat))
            {
                if (!res.Contains(c)) res.Add(c);
                foreach (var pid in c.Premises) { var pc = T.Claims.FirstOrDefault(x => x.Id == pid); if (pc != null && pc.Text != null && !res.Contains(pc)) res.Add(pc); }
            }
            return res;
        }
        static bool Heard(TrialState T, TrialClaim c) => c != null && c.Text != null && c.Status != "retracted" && c.Beat < Math.Max(T.Cursor, 1);
        /// <summary>Has any statement been heard yet (the court's F key is off until then)?</summary>
        public static bool AnyHeard(TrialState T) => T != null && T.Claims.Any(c => Heard(T, c));

        /// <summary>도움 (bl23.assist): 0 끔 · 1 보통 · 2 친절.</summary>
        public static int Assist => Settings.Assist;

        // ================================================================== build
        public static CluePicker Build(RectTransform parent, Simulation sim, IList<TrialClaim> claims, Mode mode, Action<string, string, string> commit, Action cancel)
        {
            var root = UIKit.Rect(parent, "CluePicker", Vector2.zero, Vector2.one);
            var p = root.gameObject.AddComponent<CluePicker>();
            p._root = root; p._sim = sim; p._mode = mode; p._commit = commit; p._cancel = cancel; p._builtFrame = Time.frameCount;
            if (claims != null) foreach (var c in claims) if (c != null && !p._claims.Contains(c)) p._claims.Add(c);
            p.Layout();
            // start on the first statement that can still be challenged (or simply the first)
            int first = p._claims.FindIndex(c => c.Status != "refuted"); p._claimIdx = p._claims.Count == 0 ? -1 : Math.Max(0, first);
            p.Reorder(); p.PaintClaims(); p.BuildPage(); p.ShowPreview(); p.PaintButtons();
            if (p._claims.Count == 0 && p._order.Count > 0) p.SetFocus(true);
            return p;
        }

        void Layout()
        {
            // bottom strip: the first-trial guide on the left, key hints on the right
            var strip = UIKit.Rect(_root, "Strip", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 34));
            _guide = Goth.Text(strip, "Guide", "", 20, GPal.Gilt, TextAlignmentOptions.MidlineLeft, true, Vector2.zero, new Vector2(0.64f, 1), new Vector2(6, 0), Vector2.zero);
            _guide.textWrappingMode = TextWrappingModes.NoWrap; _guide.overflowMode = TextOverflowModes.Ellipsis;
            string enter = _mode == Mode.Defense ? "Enter 제시한다" : "Enter 내민다";
            _hintLine = Goth.Text(strip, "Keys", $"←/→ 칸 옮기기 · ↑/↓ 고르기 · {enter} · Esc 뒤로 · Tab 수첩", 17, GPal.A(GPal.Bone, 0.7f), TextAlignmentOptions.MidlineRight, false, new Vector2(0.5f, 0), Vector2.one, Vector2.zero, new Vector2(-6, 0));
            _hintLine.textWrappingMode = TextWrappingModes.NoWrap;

            // left pane: the statements (parchment, a transcript)
            var lp = UIKit.Rect(_root, "Statements", new Vector2(0, 0), new Vector2(0.43f, 1), new Vector2(0, 46), new Vector2(-10, 0));
            var parch = Goth.Parchment(lp, "P", Vector2.zero, Vector2.one);
            string cap = _mode == Mode.Defense ? "나를 지목한 이유" : _mode == Mode.Moment ? "지금 나온 말" : _mode == Mode.Rebut ? "나를 향한 말" : "나온 발언";
            var ct = Goth.Text(lp, "Cap", cap, 21, GPal.InkRed, TextAlignmentOptions.TopLeft, true, new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -50), new Vector2(-26, -16)); ct.characterSpacing = 4;
            UIKit.Img(lp, "CapRule", GPal.A(GPal.InkRed, 0.45f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -56), new Vector2(-26, -55));
            _leftList = UIKit.Rect(lp, "List", Vector2.zero, Vector2.one, new Vector2(16, 16), new Vector2(-16, -64));
            _leftScroll = _leftList.gameObject.AddComponent<ScrollRect>(); _leftScroll.horizontal = false; _leftScroll.scrollSensitivity = 40; _leftScroll.movementType = ScrollRect.MovementType.Clamped;
            _leftList.gameObject.AddComponent<RectMask2D>();
            var lbed = _leftList.gameObject.AddComponent<Image>(); lbed.color = new Color(0, 0, 0, 0); lbed.raycastTarget = true;
            _leftContent = UIKit.Rect(_leftList, "Content", new Vector2(0, 1), new Vector2(1, 1)); _leftContent.pivot = new Vector2(0.5f, 1);
            _leftScroll.content = _leftContent; _leftScroll.viewport = _leftList;
            BuildClaims();

            // right pane: the cards (lacquer)
            var rp = UIKit.Rect(_root, "Cards", new Vector2(0.43f, 0), new Vector2(1, 1), new Vector2(10, 46), Vector2.zero);
            Goth.Panel(rp, "F", Vector2.zero, Vector2.one);
            _pageBar = UIKit.Rect(rp, "PageBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -58), new Vector2(-24, -12));
            _pageLabel = Goth.Text(_pageBar, "Label", "", 21, GPal.Brass, TextAlignmentOptions.MidlineLeft, true, Vector2.zero, new Vector2(0.5f, 1)); _pageLabel.characterSpacing = 3;
            UIKit.Img(rp, "Rule", GPal.A(GPal.Brass, 0.45f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -62), new Vector2(-24, -61));
            // six rows a page; rows shrink a little on short screens so the preview always keeps room
            Canvas.ForceUpdateCanvases(); float hr = rp.rect.height > 100 ? rp.rect.height : 800f;
            _rowH = Mathf.Clamp((hr - 70 - 98 - 12 - 170) / PerPage, 42f, 60f); float listH = _rowH * PerPage;
            _cardList = UIKit.Rect(rp, "List", new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -72 - listH), new Vector2(-20, -70));
            var cbed = _cardList.gameObject.AddComponent<Image>(); cbed.color = new Color(0, 0, 0, 0); cbed.raycastTarget = true;
            _cardList.gameObject.AddComponent<WheelSink>().OnWheel = d => { if (d < 0) Flip(1); else if (d > 0) Flip(-1); };
            _buttons = UIKit.Rect(rp, "Buttons", new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 22), new Vector2(-24, 84));
            _preview = UIKit.Rect(rp, "Preview", new Vector2(0, 0), new Vector2(1, 1), new Vector2(28, 98), new Vector2(-28, -84 - listH));
            UIKit.Img(rp, "PrevRule", GPal.A(GPal.Brass, 0.3f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -78 - listH), new Vector2(-24, -77 - listH));
            BuildButtons();
        }

        // ================================================================== statements
        void BuildClaims()
        {
            UIKit.Clear(_leftContent); _claimRows.Clear();
            const float H = 108, G = 8;
            if (_claims.Count == 0)
            {
                Goth.Text(_leftContent, "None", _mode == Mode.Defense ? "나를 지목한 말은 아직 없다." : "아직 나온 발언이 없다.", 21, GPal.Ink, TextAlignmentOptions.TopLeft, false, new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -60), new Vector2(-10, -8));
                _leftContent.sizeDelta = new Vector2(0, 80); return;
            }
            for (int i = 0; i < _claims.Count; i++)
            {
                var c = _claims[i]; int idx = i;
                var row = UIKit.Rect(_leftContent, "S" + i, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -(i + 1) * (H + G) + G), new Vector2(0, -i * (H + G)));
                var f = row.gameObject.AddComponent<GFrame>(); f.raycastTarget = true; f.Gap = 0; f.Corners = false; f.Width = 1.2f;
                var pr = row.gameObject.AddComponent<PickRow>(); pr.Frame = f; pr.OnParchment = true;
                pr.OnClick = () => { SelectClaim(idx); _cardsFocus = false; PaintFocus(); };
                pr.OnDouble = () => { SelectClaim(idx); SetFocus(true); };
                var mh = UIKit.Rect(row, "MH", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(96, 0)); Goth.Medallion(mh, "M", c.Speaker, new Vector2(74, 74), Vector2.zero);
                string lead = c.Topic == "premise" ? "근거 · " : c.Accused != null && c.Topic != "defense" ? Cast.GivenOf(c.Accused) + " 범인설 · " : "";
                var nm = Goth.Text(row, "N", $"<color={GPal.Hex(GPal.InkRed)}>{Num(i)}</color>  {lead}{Cast.NameOf(c.Speaker)}", 18, GPal.Ink, TextAlignmentOptions.TopLeft, true, Vector2.zero, Vector2.one, new Vector2(100, 8), new Vector2(-12, -8));
                nm.textWrappingMode = TextWrappingModes.NoWrap; nm.overflowMode = TextOverflowModes.Ellipsis;
                var st = Goth.StatusWord(c.Status);
                if (st != null) { var chip = Goth.KindChip(row, st, Goth.ChipTone.Smoke, 0, 14, 22); chip.anchorMin = chip.anchorMax = new Vector2(1, 1); chip.pivot = new Vector2(1, 1); chip.anchoredPosition = new Vector2(-10, -8); }
                string q = LineBank.FixParticles(Plain(LineBank.Pages(c.Text).FirstOrDefault() ?? c.Text ?? ""));
                // two statements that read the same (one line, two sightings at different times): each row leads with its own
                // fact, so the two can be told apart and the card goes against the right one
                string fact = null;
                if (c.Prop != null && _claims.Any(o => o != c && o.Speaker == c.Speaker && o.Text == c.Text))
                    try { fact = CaseBoard.Sentence(_sim, c.Prop); } catch (Exception) { }
                string body = string.IsNullOrEmpty(fact) ? "“" + q + "”" : $"<color={GPal.Hex(GPal.InkRed)}>→</color> {LineBank.FixParticles(Plain(fact))}\n“{q}”";
                var qt = Goth.Text(row, "Q", body, 20, GPal.Ink, TextAlignmentOptions.TopLeft, false, Vector2.zero, Vector2.one, new Vector2(100, 8), new Vector2(-14, -36));
                qt.maxVisibleLines = 2; qt.overflowMode = TextOverflowModes.Ellipsis; qt.lineSpacing = -4;
                if (c.Status == "refuted") { nm.alpha = 0.55f; qt.alpha = 0.55f; }
                _claimRows.Add(pr);
            }
            _leftContent.sizeDelta = new Vector2(0, _claims.Count * (H + G) + 8);
        }
        static string Num(int i) => (i + 1).ToString();

        void PaintClaims()
        {
            for (int i = 0; i < _claimRows.Count; i++) { var r = _claimRows[i]; r.Selected = i == _claimIdx; r.Focused = i == _claimIdx && !_cardsFocus; r.Paint(); }
        }

        void SelectClaim(int i)
        {
            if (_claims.Count == 0) return; i = Mathf.Clamp(i, 0, _claims.Count - 1);
            if (i == _claimIdx) return;
            _claimIdx = i; PaintClaims(); ScrollTo(i);
            // the ranking follows the statement in question (at 도움 보통/친절)
            var keep = _cardId; Reorder(); _page = 0; _cardId = null;
            if (keep != null && _order.Contains(keep) && _cardsFocus) { _cardId = keep; _page = _order.IndexOf(keep) / PerPage; }
            BuildPage(); ShowPreview(); PaintButtons();
        }

        void ScrollTo(int i)
        {
            if (_leftScroll == null || _claims.Count == 0) return; Canvas.ForceUpdateCanvases();
            float H = 116, view = _leftList.rect.height, total = _leftContent.rect.height; if (total <= view + 1) return;
            float y = i * H, y1 = y + H, cur = _leftContent.anchoredPosition.y;
            if (y < cur) cur = y; else if (y1 > cur + view) cur = y1 - view;
            _leftContent.anchoredPosition = new Vector2(_leftContent.anchoredPosition.x, Mathf.Clamp(cur, 0, total - view));
        }

        // ================================================================== cards
        CaseBoard.View View(string id)
        {
            if (id == null) return null;
            if (_views.TryGetValue(id, out var v)) return v;
            var ev = TrialGames.BulletEvidence(S, id); if (ev == null) return null;
            try { v = CaseBoard.Describe(_sim, ev); } catch (Exception e) { Debug.LogException(e); v = null; }
            if (v == null) v = new CaseBoard.View { Ev = ev, Id = ev.Id, Title = ev.Title, Line = (ev.Desc ?? "").Split('\n')[0], KindLabel = "기록" };
            _views[id] = v; return v;
        }

        /// <summary>Card order: 도움 끔 → the notebook's order; 보통/친절 → CaseBoard.Rank for the statement in question. Every
        /// Arsenal card is in the list either way (anything the ranking misses is appended).</summary>
        void Reorder()
        {
            var arsenal = TrialGames.Arsenal(_sim).Select(b => b.Id).ToList();
            var order = new List<string>(); _dots.Clear();
            int assist = Assist;
            if (assist >= 1)
            {
                var focus = _mode == Mode.Defense || SelectedClaim == null ? (IList<TrialClaim>)_claims : new List<TrialClaim> { _claims[_claimIdx] };
                List<(string id, double score)> scored = null;
                try { scored = CaseBoard.RankScored(_sim, focus); } catch (Exception e) { Debug.LogException(e); }
                if (scored != null)
                {
                    var set = new HashSet<string>(arsenal);
                    foreach (var (id, _) in scored) if (set.Contains(id) && !order.Contains(id)) order.Add(id);
                    if (assist >= 2) foreach (var (id, sc) in scored.Where(x => set.Contains(x.id)).Take(3)) if (sc > 0) _dots.Add(id);
                }
            }
            foreach (var id in arsenal) if (!order.Contains(id)) order.Add(id);
            _order = order;
        }

        int Pages => Math.Max(1, (_order.Count + PerPage - 1) / PerPage);

        void BuildPage()
        {
            UIKit.Clear(_cardList); _cardRows.Clear(); UIKit.Clear(_pageBar.Find("Nav") ?? UIKit.Rect(_pageBar, "Nav", new Vector2(0.5f, 0), Vector2.one));
            _page = Mathf.Clamp(_page, 0, Pages - 1);
            _pageLabel.text = _order.Count == 0 ? "단서" : $"단서 {_page + 1}/{Pages}";
            var nav = (RectTransform)_pageBar.Find("Nav");
            if (Pages > 1)
            {
                var prev = Goth.Button(nav, "이전", () => Flip(-1), new Vector2(0.28f, 0), new Vector2(0.62f, 1), new Vector2(0, 4), new Vector2(-6, -4), 18); prev.Interactable = _page > 0; prev.Paint();
                var next = Goth.Button(nav, "다음 ▶", () => Flip(1), new Vector2(0.64f, 0), new Vector2(1, 1), new Vector2(0, 4), new Vector2(0, -4), 18); next.Interactable = _page < Pages - 1; next.Paint();
            }
            if (_order.Count == 0)
            {
                Goth.Text(_cardList, "None", "내밀 단서가 없다.\n수사 때 살펴본 것과 들은 말이 여기에 모인다.", 21, GPal.A(GPal.Bone, 0.8f), TextAlignmentOptions.TopLeft, false, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, -10));
                return;
            }
            float G = 6, H = _rowH - G;
            for (int k = 0; k < PerPage; k++)
            {
                int i = _page * PerPage + k; if (i >= _order.Count) break;
                var id = _order[i]; var v = View(id); if (v == null) continue;
                var row = UIKit.Rect(_cardList, "C" + k, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -(k + 1) * (H + G) + G), new Vector2(0, -k * (H + G)));
                var f = row.gameObject.AddComponent<GFrame>(); f.raycastTarget = true; f.Gap = 0; f.Corners = false; f.Width = 1.2f;
                var pr = row.gameObject.AddComponent<PickRow>(); pr.Frame = f; pr.Id = id; pr.Guide = id == _guideCard;
                pr.OnClick = () => { SelectCard(id); _cardsFocus = true; PaintFocus(); };
                pr.OnDouble = () => { SelectCard(id); _cardsFocus = true; Commit(PrimaryAct); };
                var num = Goth.Text(row, "K", (k + 1).ToString(), 15, GPal.A(GPal.Brass, 0.7f), TextAlignmentOptions.Center, true, new Vector2(0, 0), new Vector2(0, 1), new Vector2(4, 0), new Vector2(24, 0));
                var chip = Goth.KindChip(row, string.IsNullOrEmpty(v.KindLabel) ? "기록" : v.KindLabel, v.Key ? Goth.ChipTone.Gold : Goth.ChipTone.Plain, 30, 15, 26);
                float x = 30 + chip.sizeDelta.x + 12;
                // a witness sheet has no time of its own: its row says what the witness saw instead ("해린의 증언 · 도윤이 청동 흉상을…")
                bool sheet = v.Cat == CaseBoard.Cat.Witness && string.IsNullOrEmpty(v.When) && !string.IsNullOrEmpty(v.Line);
                string head = (v.Key ? $"<color={GPal.Hex(GPal.Gilt)}>◆</color> " : "") + Plain(v.Title ?? "") + (sheet ? $"  <size=80%><color={GPal.Hex(GPal.A(GPal.Smoke, 0.95f))}>· {Plain(v.Line)}</color></size>" : "");
                var tt = Goth.Text(row, "T", head, 21, GPal.Bone, TextAlignmentOptions.MidlineLeft, v.Key, Vector2.zero, Vector2.one, new Vector2(x, 0), new Vector2(sheet ? (_dots.Contains(id) ? -30 : -12) : -180, 0));
                tt.textWrappingMode = TextWrappingModes.NoWrap; tt.overflowMode = TextOverflowModes.Ellipsis;
                var wt = Goth.Text(row, "W", v.When ?? "", 16, GPal.A(GPal.Smoke, 0.95f), TextAlignmentOptions.MidlineRight, false, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-176, 0), new Vector2(_dots.Contains(id) ? -30 : -12, 0));
                wt.textWrappingMode = TextWrappingModes.NoWrap; wt.overflowMode = TextOverflowModes.Ellipsis;
                if (_dots.Contains(id)) Goth.Text(row, "Dot", "●", 13, GPal.Gilt, TextAlignmentOptions.Center, true, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-26, 0), new Vector2(-8, 0));
                _cardRows.Add(pr);
            }
            PaintCards();
        }

        void PaintCards()
        {
            foreach (var r in _cardRows) { r.Selected = r.Id == _cardId; r.Focused = r.Selected && _cardsFocus; r.Guide = r.Id == _guideCard; r.Paint(); }
        }

        void SelectCard(string id)
        {
            if (id == null || !_order.Contains(id)) return;
            int pg = _order.IndexOf(id) / PerPage;
            bool rebuild = pg != _page; _cardId = id; _page = pg;
            if (rebuild) BuildPage(); else PaintCards();
            ShowPreview(); PaintButtons();
        }

        void Flip(int d)
        {
            int np = Mathf.Clamp(_page + d, 0, Pages - 1); if (np == _page) return;
            _page = np; TrialFx.Sound("parchment", 0.35f);
            // keep a selection on the new page when the cards pane has focus (keyboard flow)
            if (_cardsFocus) { int i = Mathf.Min(_order.Count - 1, _page * PerPage + (d < 0 ? PerPage - 1 : 0)); _cardId = i >= 0 ? _order[i] : null; }
            BuildPage(); ShowPreview(); PaintButtons();
        }

        void ShowPreview()
        {
            UIKit.Clear(_preview);
            var v = View(_cardId);
            if (v == null)
            {
                string msg = _order.Count == 0 ? "" : _mode == Mode.Defense ? "나를 지목한 말을 뒤집을 단서를 고른다." : SelectedClaim == null ? "왼쪽에서 발언을 고른다." : "그 말과 맞지 않는 단서를 고른다.";
                Goth.Text(_preview, "Msg", msg, 20, GPal.A(GPal.Bone, 0.65f), TextAlignmentOptions.TopLeft, false, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(0, -6));
                return;
            }
            var title = Goth.Text(_preview, "T", Plain(v.Title ?? ""), 26, v.Key ? GPal.Gilt : GPal.Bone, TextAlignmentOptions.TopLeft, true, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -38), new Vector2(0, 0));
            title.textWrappingMode = TextWrappingModes.NoWrap; title.overflowMode = TextOverflowModes.Ellipsis;
            var chips = UIKit.Rect(_preview, "Chips", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -72), new Vector2(0, -44));
            float x = 0;
            x += Goth.KindChip(chips, string.IsNullOrEmpty(v.KindLabel) ? "기록" : v.KindLabel, Goth.ChipTone.Plain, x, 15, 26).sizeDelta.x + 8;
            if (v.Key) x += Goth.KindChip(chips, "중요", Goth.ChipTone.Gold, x, 15, 26).sizeDelta.x + 8;
            if (v.Hearsay) x += Goth.KindChip(chips, "전해 들음", Goth.ChipTone.Smoke, x, 15, 26).sizeDelta.x + 8;
            if (!string.IsNullOrEmpty(v.When)) Goth.Text(chips, "When", v.When, 16, GPal.Smoke, TextAlignmentOptions.MidlineLeft, false, Vector2.zero, Vector2.one, new Vector2(x + 6, 0), Vector2.zero);
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(v.Line)) sb.Append(Plain(v.Line));
            foreach (var b in (v.Bullets ?? new List<string>()).Where(b => !string.IsNullOrEmpty(b)).Take(2)) sb.Append($"\n<size=88%><color={GPal.Hex(GPal.Brass)}>·</color> {Plain(b)}</size>");
            if (!string.IsNullOrEmpty(v.Caution)) sb.Append($"\n<size=85%><color={GPal.Hex(GPal.Brass)}>주의 — {v.Caution}</color></size>");
            var body = Goth.Text(_preview, "B", sb.ToString(), 21, GPal.Bone, TextAlignmentOptions.TopLeft, false, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(0, -80));
            body.enableAutoSizing = true; body.fontSizeMin = 15; body.fontSizeMax = 21; body.lineSpacing = 4; body.overflowMode = TextOverflowModes.Ellipsis;
        }

        // ================================================================== moves
        string PrimaryAct => _mode == Mode.Defense ? "present" : "contra";

        void BuildButtons()
        {
            UIKit.Clear(_buttons);
            var labels = new List<(string label, Action a, bool primary)>();
            switch (_mode)
            {
                case Mode.Review:
                    labels.Add(("반박한다", () => Commit("contra"), true)); labels.Add(("뒷받침한다", () => Commit("agree"), false)); labels.Add(("출처 묻기", () => Commit("source"), false)); break;
                case Mode.Moment:
                    labels.Add(("반박한다", () => Commit("contra"), true)); labels.Add(("출처 묻기", () => Commit("source"), false)); labels.Add(("지켜본다  <size=72%><color=#8C8070>Esc</color></size>", Cancel, false)); break;
                case Mode.Rebut:
                    labels.Add(("반박한다", () => Commit("contra"), true)); labels.Add(("뒤로  <size=72%><color=#8C8070>Esc</color></size>", Cancel, false)); break;
                case Mode.Defense:
                    labels.Add(("제시한다", () => Commit("present"), true)); labels.Add(("변론하지 않는다", Cancel, false)); break;
            }
            int n = labels.Count; float gap = 12;
            for (int i = 0; i < n; i++)
            {
                var (label, a, primary) = labels[i];
                var b = Goth.Button(_buttons, label, a, new Vector2(i / (float)n, 0), new Vector2((i + 1) / (float)n, 1), new Vector2(i == 0 ? 0 : gap / 2, 0), new Vector2(i == n - 1 ? 0 : -gap / 2, 0), 22, primary);
                if (primary) _bPrimary = b;
                else if (label.StartsWith("뒷받침")) _bAgree = b;
                else if (label.StartsWith("출처")) _bSource = b;
                else _bBack = b;
            }
        }

        void PaintButtons()
        {
            var c = SelectedClaim != null ? _claims[_claimIdx] : null;
            bool open = c != null && c.Status != "refuted";
            if (_bPrimary != null) { _bPrimary.Interactable = _cardId != null && (_mode == Mode.Defense || open); _bPrimary.Paint(); }
            if (_bAgree != null) { _bAgree.Interactable = _cardId != null && c != null && c.Status != "refuted"; _bAgree.Paint(); }
            if (_bSource != null) { bool ok = c != null && open && c.Speaker != Cast.Player; _bSource.Interactable = ok; _bSource.gameObject.SetActive(c == null || c.Speaker != Cast.Player); _bSource.Paint(); }
        }

        void Commit(string act)
        {
            if (_closed) return;
            var c = SelectedClaim != null ? _claims[_claimIdx] : null;
            bool ok = act == "source" ? c != null && c.Speaker != Cast.Player && c.Status != "refuted"
                    : act == "present" ? _cardId != null
                    : _cardId != null && c != null && c.Status != "refuted";
            if (!ok) { TrialFx.Sound("ui_cancel", 0.4f); if (act != "source" && _cardId == null) { SetFocus(true); } return; }
            _closed = true; TrialFx.Sound(act == "source" ? "quill" : "wax_stamp", 0.6f);
            _commit?.Invoke(c?.Id, act == "source" ? null : _cardId, act);
        }

        void Cancel() { if (_closed) return; _closed = true; TrialFx.Sound("parchment", 0.45f); _cancel?.Invoke(); }

        // ================================================================== focus & keys
        void SetFocus(bool cards)
        {
            if (cards && _order.Count == 0) { TrialFx.Sound("ui_cancel", 0.35f); return; }
            _cardsFocus = cards;
            if (cards && (_cardId == null || _order.IndexOf(_cardId) / PerPage != _page)) { int i = Math.Min(_order.Count - 1, _page * PerPage); if (i >= 0) SelectCard(_order[i]); }
            PaintFocus();
        }
        void PaintFocus() { PaintClaims(); PaintCards(); }

        void Step(int d)
        {
            if (!_cardsFocus) { if (_claims.Count > 0) SelectClaim(_claimIdx < 0 ? 0 : Mathf.Clamp(_claimIdx + d, 0, _claims.Count - 1)); return; }
            if (_order.Count == 0) return;
            int i = _cardId == null ? _page * PerPage : _order.IndexOf(_cardId) + d;
            i = Mathf.Clamp(i, 0, _order.Count - 1);
            SelectCard(_order[i]); PaintFocus();
        }

        void Number(int k)
        {
            if (!_cardsFocus) { if (k - 1 < _claims.Count) SelectClaim(k - 1); return; }
            if (k > PerPage) return; int i = _page * PerPage + k - 1; if (i < _order.Count) { SelectCard(_order[i]); PaintFocus(); }
        }

        static bool Blocked()
        {
            var s = Session.I; if (s == null) return false;
            return (s.Note != null && (s.Note.Open || s.Note.ClosedFrame == Time.frameCount)) || (s.Menu != null && s.Menu.Open) || BacklogUI.Open || BacklogUI.ClosedFrame == Time.frameCount;
        }

        void Update()
        {
            if (_closed || Time.frameCount == _builtFrame || Blocked()) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) SetFocus(false);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) SetFocus(true);
            if (Input.GetKeyDown(KeyCode.UpArrow)) Step(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) Step(1);
            if (Input.GetKeyDown(KeyCode.PageDown)) Flip(1); else if (Input.GetKeyDown(KeyCode.PageUp)) Flip(-1);
            for (int k = 1; k <= 9; k++) if (Input.GetKeyDown(KeyCode.Alpha0 + k) || Input.GetKeyDown(KeyCode.Keypad0 + k)) { Number(k); break; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (!_cardsFocus) SetFocus(true);
                else Commit(PrimaryAct);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_cardsFocus && _claims.Count > 0) SetFocus(false);
                else Cancel();
            }
        }

        // ================================================================== guide / probe
        /// <summary>Preselect a statement and a card, outline the card in gold and say one line about it (the first-trial guide).</summary>
        public void Guide(string claimId, string cardId, string line)
        {
            int i = _claims.FindIndex(c => c.Id == claimId); if (i >= 0) SelectClaim(i);
            _guideCard = cardId;
            if (cardId != null && _order.Contains(cardId)) { _cardsFocus = true; SelectCard(cardId); }
            PaintFocus();
            if (_guide != null) _guide.text = line ?? "";
        }
        /// <summary>A plain guide line with nothing preselected.</summary>
        public void Say(string line) { if (_guide != null) _guide.text = line ?? ""; }

        /// <summary>Probe only: select a statement and a card (by id) as a player would.</summary>
        public void Select(string claimId, string cardId)
        {
            int i = claimId == null ? -1 : _claims.FindIndex(c => c.Id == claimId); if (i >= 0) SelectClaim(i);
            if (cardId != null) { _cardsFocus = true; SelectCard(cardId); }
            PaintFocus();
        }
        /// <summary>Probe only: commit the current selection with the primary move.</summary>
        public void ProbeCommit() => Commit(PrimaryAct);

        // ================================================================== text
        /// <summary>Court text as the player reads it (idempotent): the kernel's CaseBoard.Plain plus the UI's own wording
        /// ("자료" → "단서"; no rule codes, no "조건부" jargon).</summary>
        public static string Plain(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            try { s = CaseBoard.Plain(s) ?? s; } catch (Exception) { }
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s*\[LR\d+\]", "");
            s = s.Replace(" (전해 들은 내용이라 조건부)", " (전해 들은 말이라 확정할 수 없다)").Replace("자료", "단서").Replace("조건부", "확인 필요").Replace("범위 제한", "흔들림");
            return s;
        }
    }

    /// <summary>A picker row: single click selects, a second click (double-click) acts. Painted as a lacquer strip on the card
    /// pane, a faint wash on parchment; gold hairline when selected, a thick gold outline for the first-trial guide.</summary>
    public sealed class PickRow : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public GFrame Frame; public string Id; public Action OnClick, OnDouble; public bool Selected, Focused, Hover, Guide, OnParchment;
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (e.clickCount >= 2) OnDouble?.Invoke(); else { TrialFx.Sound("quill", 0.35f); OnClick?.Invoke(); }
        }
        public void OnPointerEnter(PointerEventData e) { Hover = true; Paint(); }
        public void OnPointerExit(PointerEventData e) { Hover = false; Paint(); }
        public void Paint()
        {
            if (Frame == null) return;
            if (OnParchment)
            {
                Frame.Fill = Selected ? GPal.A(GPal.Wax, 0.2f) : Hover ? GPal.A(GPal.Ink, 0.08f) : GPal.A(GPal.Ink, 0.02f);
                Frame.FillBottom = Frame.Fill;
                Frame.Border = Selected ? GPal.A(GPal.InkRed, Focused ? 0.95f : 0.7f) : GPal.A(GPal.Ink, 0.18f); Frame.Width = Focused ? 2.4f : 1.2f;
            }
            else
            {
                Frame.Fill = Selected ? GPal.A(GPal.Oxblood, 1f) : Hover ? GPal.A(new Color(0.16f, 0.06f, 0.05f), 1f) : GPal.A(new Color(0.075f, 0.052f, 0.036f), 1f);
                Frame.FillBottom = Selected ? GPal.A(new Color(0.16f, 0.025f, 0.035f), 1f) : GPal.A(GPal.Lacquer, 1f);
                Frame.Border = Guide ? GPal.Gilt : Selected ? GPal.A(GPal.Gilt, Focused ? 1f : 0.75f) : GPal.A(GPal.Brass, 0.4f);
                Frame.Width = Guide ? 3f : Focused ? 2.2f : 1.2f;
            }
            Frame.Refresh();
        }
    }

    /// <summary>Turns the mouse wheel over a list into page flips.</summary>
    public sealed class WheelSink : MonoBehaviour, IScrollHandler
    {
        public Action<float> OnWheel; float _last;
        public void OnScroll(PointerEventData e) { if (Time.unscaledTime - _last < 0.12f) return; _last = Time.unscaledTime; OnWheel?.Invoke(e.scrollDelta.y); }
    }
}
