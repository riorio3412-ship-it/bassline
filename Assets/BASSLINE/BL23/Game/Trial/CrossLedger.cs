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
    /// 증언 대조 — two people's testimonies laid side by side on two pages. Mark one line on each page that cannot both be true;
    /// a red ink line joins them and the kernel judges the pair (TrialGames.LedgerPick → Logic, both directions).
    /// Three wrong pairings close the ledger.
    /// </summary>
    public sealed class CrossLedger : TrialMinigame
    {
        sealed class Row { public int Side, Index; public GameLine L; public RectTransform Rt; public Image Mark; }
        readonly List<Row> _rows = new List<Row>(); Row _left, _right; IEnumerator _end; ThreadGraphic _link; bool _busy;

        public CrossLedger(TrialDirectorUI h) : base(h) { }

        public override IEnumerator Run()
        {
            MusicDirector.I?.SetState(MusicState.TrialPressure);
            H.CamWide(42);
            yield return Intro("증언 대조", G.Subtitle, "두 사람의 말 가운데 둘 다 맞을 수는 없는 한 쌍을 짚어라");
            yield return Teach("증언 대조", new[]
            {
                "두 사람의 증언이 나란히 펼쳐진다.",
                "왼쪽에서 한 줄, 오른쪽에서 한 줄 — 둘 다 맞을 수는 없는 짝을 고른다. 붉은 잉크가 두 줄을 잇는다.",
                "짝을 잘못 고르면 배심원의 신뢰가 깎인다. 세 번 틀리면 대조는 끝난다.",
            }, ArtLedger);
            Header("증언 대조", G.Subtitle);
            Candles(4, G.TimeLimit, new Vector2(0.5f, 1), new Vector2(40, -130));
            Page(0, G.Opponent, G.Lines, new Vector2(0.03f, 0.1f), new Vector2(0.49f, 0.76f));
            Page(1, G.Second, G.Lines2, new Vector2(0.51f, 0.1f), new Vector2(0.97f, 0.76f));
            var lk = UIKit.Rect(Root, "Link", Vector2.zero, Vector2.one); _link = lk.gameObject.AddComponent<ThreadGraphic>(); _link.color = GPal.A(GPal.InkRed, 0f); _link.raycastTarget = false; _link.Width = 5; _link.Sag = 25;
            HintLine("클릭|양쪽에서 한 줄씩 고르기 · 다시 클릭|선택 해제 · Esc|대조 포기");
            Speak(G.Opponent, null, Expr.Neutral, Gesture.CrossArms, 0, 3f);
            if (Probe) H.StartCoroutine(ProbeDemo());
            while (_end == null) { Tick(); EndVirtFrame(); yield return null; }
            yield return _end;
            Done = true;
        }

        void Page(int side, string who, List<GameLine> lines, Vector2 aMin, Vector2 aMax)
        {
            var rt = UIKit.Rect(Root, "Page" + side, aMin, aMax); rt.localRotation = Quaternion.Euler(0, 0, side == 0 ? 0.8f : -0.8f);
            UIKit.Img(rt, "Shade", GPal.A(Color.black, 0.5f), Vector2.zero, Vector2.one, new Vector2(10, -14), new Vector2(10, -14));
            Goth.Parchment(rt, "Paper", Vector2.zero, Vector2.one);
            var med = Goth.Medallion(rt, "M", who, new Vector2(104, 104), Vector2.zero); med.anchorMin = med.anchorMax = new Vector2(0, 1); med.anchoredPosition = new Vector2(80, -70);
            Goth.Text(rt, "Name", Cast.NameOf(who) + "의 증언", 30, GPal.Ink, TextAlignmentOptions.MidlineLeft, true, new Vector2(0, 1), new Vector2(1, 1), new Vector2(150, -110), new Vector2(-20, -30)).characterSpacing = 3;
            var body = UIKit.Rect(rt, "Body", Vector2.zero, Vector2.one, new Vector2(34, 24), new Vector2(-30, -134));
            float y = 0, width = body.rect.width - 20;
            for (int i = 0; i < lines.Count; i++)
            {
                var L = lines[i];
                var probe = Goth.Text(body, "P", L.Text, 26, GPal.Ink, TextAlignmentOptions.TopLeft, false); float h = Mathf.Max(56, probe.GetPreferredValues(L.Text, width, 0).y + 22); Object.Destroy(probe.gameObject);
                var row = UIKit.Rect(body, "R" + i, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -(y + h)), new Vector2(0, -y)); y += h + 8;
                var mark = UIKit.Img(row, "Mark", GPal.A(GPal.InkRed, 0f), Vector2.zero, Vector2.one, new Vector2(-10, -4), new Vector2(6, 4));
                Goth.Text(row, "T", "· " + L.Text, 26, GPal.Ink, TextAlignmentOptions.TopLeft, false, Vector2.zero, Vector2.one, new Vector2(6, 0), new Vector2(-6, -6));
                _rows.Add(new Row { Side = side, Index = i, L = L, Rt = row, Mark = mark });
            }
        }

        void Tick()
        {
            if (!TickCandles(Dt) && _end == null) { _end = Burnout(); return; }
            if (!UseVirt && Input.GetKeyDown(KeyCode.Escape)) { _end = Burnout(true); return; }
            var m = MouseLocal();
            foreach (var r in _rows)
            {
                bool sel = r == _left || r == _right; bool hov = !_busy && Inside(r.Rt, m) && r.L.Result != "hit";
                r.Mark.color = r.L.Result == "hit" ? GPal.A(GPal.InkRed, 0.25f) : sel ? GPal.A(GPal.InkRed, 0.18f) : hov ? GPal.A(GPal.Ink, 0.08f) : GPal.A(GPal.InkRed, 0f);
                if (hov && MouseDown)
                {
                    TrialFx.Sound("quill", 0.6f);
                    if (r.Side == 0) _left = _left == r ? null : r; else _right = _right == r ? null : r;
                    Speak(r.L.Speaker, null, Expr.Neutral, Gesture.Listen, 0, 2.5f);
                    if (_left != null && _right != null) H.StartCoroutine(Judge(_left, _right));
                }
            }
            HoverSfx(_rows.FirstOrDefault(r => !_busy && r.L.Result != "hit" && Inside(r.Rt, m)));
            if (_left != null && _right != null) { _link.Set(RowAnchor(_left, true), RowAnchor(_right, false)); _link.color = GPal.A(GPal.InkRed, 0.9f); }
            else _link.color = GPal.A(GPal.InkRed, 0f);
        }

        Vector2 RowAnchor(Row r, bool rightEdge) { var rc = r.Rt.rect; return ToRoot(r.Rt, new Vector2(rightEdge ? rc.xMax : rc.xMin, rc.center.y)); }

        IEnumerator Judge(Row a, Row b)
        {
            if (_busy) yield break; _busy = true;
            TrialFx.Sound("wax_stamp", 0.5f); yield return Wait(0.35f);
            var r = TrialGames.LedgerPick(Sim, a.Index, b.Index);
            if (r.Valid)
            {
                if (r.Hourglass) { TrialFx.Sound("hourglass", 0.8f); H.StartCoroutine(GFx.Note(Root, null, "연달아 맞혔다 — 법정의 모래시계가 저절로 뒤집힌다.", new Vector2(0.5f, 0.92f), GPal.Ink, 1.5f)); }
                H.EyesStare(1f);
                foreach (var row in new[] { a, b }) { var s = GFx.Seal(Root, CenterOf(row.Rt) + new Vector2(row.Rt.rect.width * 0.35f, 0), GPal.Wax, "모", 70); H.StartCoroutine(GFx.Slam(s)); }
                Speak(G.Opponent, null, Expr.Fear, Gesture.Flinch, 4, 2f);
                if (Probe) H.StartCoroutine(Later("trial_ledger_hit", 0.5f));
                _end = Win(r); yield break;
            }
            H.StartCoroutine(Fade());
            if (Probe) H.StartCoroutine(Later("trial_ledger_miss", 0.4f));
            if (r.Ended) { _end = Lost(); yield break; }
            yield return Lapse(r, a.L.Speaker);
            _left = _right = null; _busy = false;
        }

        IEnumerator Fade() { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < 0.5f) { _link.Snap = GFx.K(t0, 0.5f) * 0.6f; _link.color = GPal.A(GPal.InkRed, 0.9f * (1 - GFx.K(t0, 0.5f))); _link.SetVerticesDirty(); yield return null; } _link.Snap = 0; }

        IEnumerator Win(TrialGames.ShotResult r) { TrialFx.Sound("choir_swell", 0.8f); yield return Wait(0.5f); yield return GFx.Plate(H.FxRoot, "모순", r.Text, GPal.Oxblood, 1.8f, "bell_toll"); }
        IEnumerator Lost() { yield return GFx.Plate(H.FxRoot, "대조 실패", "어긋난 곳을 짚어 내지 못했다", GPal.Smoke, 1.2f, "organ_sting"); }
        IEnumerator Burnout(bool quit = false) { TrialGames.Timeout(Sim); yield return GFx.Plate(H.FxRoot, quit ? "대조 포기" : "촛불이 꺼졌다", "두 증언은 나란히 남았다", GPal.Smoke, 1.1f); }

        static void ArtLedger(RectTransform p)
        {
            ArtText(p, "세나", 16, GPal.InkRed, new Vector2(0.06f, 0.82f), new Vector2(0.45f, 0.9f), true);
            ArtText(p, "태겸", 16, GPal.InkRed, new Vector2(0.54f, 0.82f), new Vector2(0.95f, 0.9f), true);
            UIKit.Img(p, "Mid", GPal.A(GPal.Ink, 0.3f), new Vector2(0.5f, 0.1f), new Vector2(0.5f, 0.86f), new Vector2(-1, 0), new Vector2(1, 0));
            string[] l = { "· 저녁엔 식당에 있었어.", "· 태겸이랑 같이 있었어.", "· 종소리를 들었어." }, r = { "· 나는 혼자 서재에 있었어.", "· 복도는 조용했어.", "· 종은 못 들었어." };
            for (int i = 0; i < 3; i++)
            {
                ArtText(p, l[i], 16, GPal.Ink, new Vector2(0.06f, 0.66f - i * 0.18f), new Vector2(0.49f, 0.78f - i * 0.18f));
                ArtText(p, r[i], 16, GPal.Ink, new Vector2(0.54f, 0.66f - i * 0.18f), new Vector2(0.99f, 0.78f - i * 0.18f));
            }
            UIKit.Img(p, "SelA", GPal.A(GPal.InkRed, 0.18f), new Vector2(0.04f, 0.48f), new Vector2(0.49f, 0.6f));
            UIKit.Img(p, "SelB", GPal.A(GPal.InkRed, 0.18f), new Vector2(0.52f, 0.66f), new Vector2(0.99f, 0.78f));
            ArtThread(p, new Vector2(0.49f, 0.54f), new Vector2(0.52f, 0.72f), GPal.A(GPal.InkRed, 0.9f), 6f);
            ArtSeal(p, new Vector2(0.5f, 0.2f), GPal.Wax, "모", 60);
        }

        IEnumerator ProbeDemo()
        {
            UseVirt = true; yield return Wait(1f);
            AutoProbe.Shot("trial_ledger");
            int li = -1, ri = -1;
            for (int i = 0; i < G.Lines.Count && li < 0; i++) for (int j = 0; j < G.Lines2.Count; j++) if (TrialGames.ProbeLedger(S, i, j)) { li = i; ri = j; break; }
            // one wrong pair first (if there is one)
            int wl = -1, wr = -1; for (int i = 0; i < G.Lines.Count && wl < 0; i++) for (int j = 0; j < G.Lines2.Count; j++) if (!TrialGames.ProbeLedger(S, i, j)) { wl = i; wr = j; break; }
            if (wl >= 0) { yield return VirtClick(CenterOf(_rows.First(r => r.Side == 0 && r.Index == wl).Rt)); yield return Wait(0.3f); yield return VirtClick(CenterOf(_rows.First(r => r.Side == 1 && r.Index == wr).Rt)); yield return Wait(4.2f); }
            if (_end == null && li >= 0) { yield return VirtClick(CenterOf(_rows.First(r => r.Side == 0 && r.Index == li).Rt)); yield return Wait(0.4f); yield return VirtClick(CenterOf(_rows.First(r => r.Side == 1 && r.Index == ri).Rt)); }
            yield return Wait(2.5f);
            if (_end == null) SpendTime(9999);
        }
    }
}
