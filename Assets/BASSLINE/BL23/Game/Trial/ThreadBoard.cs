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
    /// 붉은 실 — the deduction board on the lectern. (a) An accusation and the statements holding it up are pinned to velvet;
    /// tie a red thread from an evidence card to the statement it cuts (TrialGames.BoardTie). A true cut drops the card and the
    /// accusation loses its footing; a false one snaps the thread — two snaps and the accusation stands.
    /// (b) The final argument: the accused is pinned at the centre with three anchors — opportunity, means, deceit; tie one card to
    /// each and knot the argument (TrialGames.FinalBoard). Each thread glows or snaps as the court weighs it.
    /// </summary>
    public sealed class ThreadBoard : TrialMinigame
    {
        sealed class Pin { public int Index; public GameLine L; public GameSlot Slot; public RectTransform Rt; public bool Dropped; public string Tied; public ThreadGraphic Thread; }
        readonly List<Pin> _pins = new List<Pin>(); CardRail _rail; ThreadGraphic _live; RectTransform _board; IEnumerator _end; bool _busy; bool Final => G.Why == "final";

        public ThreadBoard(TrialDirectorUI h) : base(h) { }

        public override IEnumerator Run()
        {
            MusicDirector.I?.SetState(Final ? MusicState.TrialClimax : MusicState.TrialPressure);
            Speak(G.Opponent, null, Final ? Expr.Fear : Expr.Angry, Final ? Gesture.CrossArms : Gesture.Point, 2, 3f);
            yield return Intro(Final ? "붉은 실 — 최후 논증" : "붉은 실", G.Subtitle, Final ? "기회, 수단, 거짓 — 세 매듭에 증거를 이어 논증을 완성하라" : "지목을 받치는 말 하나를 골라, 그 말을 끊어 낼 증거를 실로 이어라");
            yield return Teach(Final ? "붉은 실 — 최후 논증" : "붉은 실", Final ? new[]
            {
                "지목된 사람을 가운데 두고 세 매듭이 놓인다 — 기회, 수단, 거짓.",
                "왼쪽 증거 카드를 끌어 각 매듭에 실로 잇는다. 다시 이으면 바뀐다.",
                "준비되면 '논증 매듭짓기'(Space). 법정이 실 한 가닥 한 가닥을 따져 본다.",
            } : new[]
            {
                "누군가의 지목과 그 지목을 받치는 말들이 핀에 꽂혀 있다.",
                "그중 한 말을 끊어 낼 증거 카드를 끌어다 실로 잇는다.",
                "틀린 실은 끊어진다. 두 번 끊어지면 지목은 그대로 선다.",
            }, ArtBoard, Final ? "board_final" : "board");
            BuildBoard();
            Header(Final ? "붉은 실 — 최후 논증" : "붉은 실", G.Subtitle);
            Candles(Final ? 5 : 4, G.TimeLimit, new Vector2(0.5f, 1), new Vector2(60, -118));
            var byId = TrialGames.Arsenal(Sim).GroupBy(b => b.Id).ToDictionary(g => g.Key, g => g.First());
            var order = G.Bullets.Where(byId.ContainsKey).Concat(byId.Keys.Where(k => !G.Bullets.Contains(k))).Select(k => byId[k]).ToList();
            _rail = new CardRail(this, order, "증거 — 끌어다 실로 잇는다");
            _rail.Tint = id => _pins.Any(p => p.Tied == id) ? new Color(1f, 0.85f, 0.8f) : Color.white;
            _rail.OnDragStart = b => { _live.color = GPal.A(GPal.Wax, 0.95f); };
            _rail.OnDrop = Drop;
            var lk = UIKit.Rect(Root, "Live", Vector2.zero, Vector2.one); _live = lk.gameObject.AddComponent<ThreadGraphic>(); _live.raycastTarget = false; _live.color = GPal.A(GPal.Wax, 0f); _live.Width = 4;
            HintLine(Final ? "드래그|증거를 매듭에 잇기 · Space|논증 매듭짓기 · Esc|포기" : "드래그|증거를 핀 꽂힌 말에 잇기 · Esc|포기");
            if (Probe) H.StartCoroutine(ProbeDemo());
            while (_end == null) { Tick(); EndVirtFrame(); yield return null; }
            _live.color = GPal.A(GPal.Wax, 0f);
            yield return _end;
            Done = true;
        }

        // ------------------------------------------------------------ board
        void BuildBoard()
        {
            _board = UIKit.Rect(Root, "Board", new Vector2(0, 0), new Vector2(1, 1), new Vector2(392, 96), new Vector2(-40, -150));
            var velvet = Goth.Frame(_board, "Velvet", Vector2.zero, Vector2.one); velvet.Fill = GPal.A(GPal.Velvet, 0.96f); velvet.FillBottom = GPal.A(new Color(0.1f, 0.01f, 0.02f), 0.96f); velvet.Border = GPal.A(GPal.Brass, 0.85f); velvet.Width = 1.5f; velvet.Gap = 6;
            Goth.Glow(_board, "Light", GPal.A(GPal.Candle, 0.12f), new Vector2(1400, 900), new Vector2(0, 80));
            if (Final)
            {
                var acc = Goth.Medallion(_board, "Accused", G.Opponent, new Vector2(190, 190), new Vector2(0, 20));
                var nm = Goth.Text(acc, "N", Cast.NameOf(G.Opponent), 26, GPal.Gilt, TextAlignmentOptions.Top, true, new Vector2(-1, 0), new Vector2(2, 0), new Vector2(0, -44), new Vector2(0, -6));
                var pos = new[] { new Vector2(-0.34f, 0.26f), new Vector2(0.34f, 0.26f), new Vector2(0f, -0.34f) };
                for (int i = 0; i < G.Slots.Count; i++)
                {
                    var s = G.Slots[i]; var rt = TrialFx.Centered(_board, "Anchor" + i, new Vector2(330, 128), new Vector2(pos[i % 3].x * _board.rect.width, pos[i % 3].y * _board.rect.height));
                    Goth.Parchment(rt, "P", Vector2.zero, Vector2.one);
                    Goth.Text(rt, "L", s.Label, 34, GPal.InkRed, TextAlignmentOptions.TopLeft, true, Vector2.zero, Vector2.one, new Vector2(60, 8), new Vector2(-10, -8)).characterSpacing = 6;
                    var hint = Goth.Text(rt, "H", s.Hint, 17, GPal.Ink, TextAlignmentOptions.BottomLeft, false, Vector2.zero, Vector2.one, new Vector2(16, 30), new Vector2(-10, -50)); hint.enableAutoSizing = true; hint.fontSizeMin = 12; hint.fontSizeMax = 17;
                    var tied = Goth.Text(rt, "Tied", "— 비어 있음 —", 16, GPal.A(GPal.Ink, 0.6f), TextAlignmentOptions.BottomLeft, true, Vector2.zero, Vector2.one, new Vector2(16, 6), new Vector2(-10, -96)); tied.overflowMode = TextOverflowModes.Ellipsis; tied.textWrappingMode = TextWrappingModes.NoWrap;
                    PinHead(rt, new Vector2(30, -26));
                    var th = UIKit.Rect(_board, "Thread" + i, Vector2.zero, Vector2.one).gameObject.AddComponent<ThreadGraphic>(); th.raycastTarget = false; th.color = GPal.A(GPal.Wax, 0f); th.Width = 4;
                    var a2 = UIKit.Rect(_board, "Tie" + i, Vector2.zero, Vector2.one).gameObject.AddComponent<ThreadGraphic>(); a2.raycastTarget = false; a2.color = GPal.A(GPal.Brass, 0.6f); a2.Width = 2; a2.Sag = 10;
                    a2.Set(ToBoard(CenterOf(rt)), ToBoard(CenterOf(acc)));
                    _pins.Add(new Pin { Index = i, Slot = s, Rt = rt, Thread = th });
                }
                var knot = UIKit.Rect(_board, "Knot", new Vector2(1, 0), new Vector2(1, 0)); knot.pivot = new Vector2(1, 0); knot.sizeDelta = new Vector2(300, 64); knot.anchoredPosition = new Vector2(-26, 26);
                var kf = Goth.Panel(knot, "F", Vector2.zero, Vector2.one); kf.Fill = GPal.A(GPal.Oxblood, 1f); kf.FillBottom = GPal.A(new Color(0.12f, 0.02f, 0.03f), 1f); kf.Border = GPal.Gilt;
                Goth.Text(knot, "T", "논증 매듭짓기", 26, GPal.Gilt, TextAlignmentOptions.Center, true).characterSpacing = 3;
                _knot = knot;
                return;
            }
            // the accusation card at the top, its supports below
            var top = TrialFx.Centered(_board, "Accusation", new Vector2(Mathf.Min(_board.rect.width * 0.7f, 900), 150), new Vector2(0, _board.rect.height * 0.5f - 105));
            Goth.Parchment(top, "P", Vector2.zero, Vector2.one);
            var am = Goth.Medallion(top, "M", G.Final.Speaker, new Vector2(96, 96), Vector2.zero); am.anchorMin = am.anchorMax = new Vector2(0, 0.5f); am.anchoredPosition = new Vector2(66, 0);
            Goth.Text(top, "Who", Cast.NameOf(G.Final.Speaker) + "의 지목", 18, GPal.InkRed, TextAlignmentOptions.TopLeft, true, Vector2.zero, Vector2.one, new Vector2(130, 10), new Vector2(-16, -10)).characterSpacing = 3;
            var at = Goth.Text(top, "T", Underline(G.Final), 26, GPal.Ink, TextAlignmentOptions.MidlineLeft, false, Vector2.zero, Vector2.one, new Vector2(130, 8), new Vector2(-16, -30)); at.enableAutoSizing = true; at.fontSizeMin = 16; at.fontSizeMax = 26;
            PinHead(top, new Vector2(top.rect.width * 0.5f, -14));
            _pins.Add(new Pin { Index = -1, L = G.Final, Rt = top });
            int n = G.Lines.Count; float w = Mathf.Min(360, (_board.rect.width - 60) / Mathf.Max(1, n) - 24);
            for (int i = 0; i < n; i++)
            {
                var L = G.Lines[i]; float x = (i - (n - 1) * 0.5f) * (w + 24);
                var rt = TrialFx.Centered(_board, "Premise" + i, new Vector2(w, 190), new Vector2(x, -_board.rect.height * 0.12f)); rt.localRotation = Quaternion.Euler(0, 0, (i % 2 == 0 ? 1.5f : -1.5f));
                Goth.Parchment(rt, "P", Vector2.zero, Vector2.one);
                Goth.Text(rt, "Who", Cast.NameOf(L.Speaker) + "의 말", 17, GPal.InkRed, TextAlignmentOptions.TopLeft, true, Vector2.zero, Vector2.one, new Vector2(16, 10), new Vector2(-10, -10)).characterSpacing = 3;
                var t = Goth.Text(rt, "T", Underline(L), 21, GPal.Ink, TextAlignmentOptions.TopLeft, false, Vector2.zero, Vector2.one, new Vector2(16, 12), new Vector2(-12, -40)); t.enableAutoSizing = true; t.fontSizeMin = 13; t.fontSizeMax = 21;
                PinHead(rt, new Vector2(w * 0.5f, -14));
                var th = UIKit.Rect(_board, "Up" + i, Vector2.zero, Vector2.one).gameObject.AddComponent<ThreadGraphic>(); th.raycastTarget = false; th.color = GPal.A(GPal.Brass, 0.55f); th.Width = 2; th.Sag = 18;
                th.Set(ToBoard(CenterOf(rt) + new Vector2(0, 80)), ToBoard(CenterOf(top) - new Vector2(0, 60)));
                _pins.Add(new Pin { Index = i, L = L, Rt = rt, Thread = th });
            }
        }
        RectTransform _knot;

        Vector2 ToBoard(Vector2 rootP) => _board.InverseTransformPoint(Root.TransformPoint(rootP));
        static string Underline(GameLine L) { var t = L.Text ?? "…"; if (L.WeakAt < 0 || L.WeakAt + L.WeakLen > t.Length) return t; return t.Substring(0, L.WeakAt) + "<u>" + t.Substring(L.WeakAt, L.WeakLen) + "</u>" + t.Substring(L.WeakAt + L.WeakLen); }
        static void PinHead(RectTransform parent, Vector2 topOffset)
        {
            var p = UIKit.Rect(parent, "Pin", new Vector2(0, 1), new Vector2(0, 1)); p.sizeDelta = new Vector2(22, 22); p.anchoredPosition = new Vector2(topOffset.x, topOffset.y);
            var g = p.gameObject.AddComponent<RingGraphic>(); g.Thickness = 11; g.color = GPal.Brass; g.raycastTarget = false;
        }

        // ------------------------------------------------------------ frame
        void Tick()
        {
            if (!TickCandles(Dt) && _end == null) { _end = Final ? Knot() : Yield("실이 다 타 버렸다"); return; }
            if (!UseVirt) { if (Input.GetKeyDown(KeyCode.Escape)) { _end = Final ? Knot() : Yield("포기"); return; } if (Final && Input.GetKeyDown(KeyCode.Space)) { _end = Knot(); return; } }
            var m = MouseLocal();
            bool onKnot = Final && _knot != null && _rail.Dragging == null && Inside(_knot, m); HoverSfx(onKnot ? _knot : null, "knot");
            if (_knot != null) _knot.localScale = Vector3.Lerp(_knot.localScale, Vector3.one * (onKnot ? 1.04f : 1f), Dt * 12);
            if (onKnot && MouseDown) { TrialFx.Sound("wax_stamp", 0.7f); _end = Knot(); return; }
            bool dragging = _rail.Poll(m, MouseDown, MouseHeld, MouseUp, UseVirt ? 0 : Input.mouseScrollDelta.y);
            if (dragging) { _live.Set(_rail.PinOf(_rail.Dragging.Id), m); _live.Sag = 40; }
            else _live.color = GPal.A(GPal.Wax, 0f);
            Pin hp = null; foreach (var p in _pins) if (!p.Dropped) { bool h = dragging && Inside(p.Rt, m); if (h) hp = p; p.Rt.localScale = Vector3.Lerp(p.Rt.localScale, Vector3.one * (h ? 1.05f : 1f), Dt * 12); }
            HoverSfx(hp, "pin", "plate_hover", 0.45f);
            // keep tied threads anchored to their card (the rail may page)
            if (Final) foreach (var p in _pins) if (p.Tied != null) { var card = _rail.CardOf(p.Tied); if (card != null) p.Thread.Set(ToBoard(_rail.PinOf(p.Tied)), ToBoard(CenterOf(p.Rt) + new Vector2(-p.Rt.rect.width * 0.5f + 30, p.Rt.rect.height * 0.5f - 26))); else p.Thread.color = GPal.A(GPal.Wax, 0.25f); }
        }

        void Drop(TrialGames.Bullet b, Vector2 m)
        {
            var p = _pins.FirstOrDefault(x => !x.Dropped && Inside(x.Rt, m)); if (p == null) return;
            if (Final)
            {
                foreach (var o in _pins.Where(x => x.Tied == b.Id)) { o.Tied = null; o.Thread.color = GPal.A(GPal.Wax, 0f); o.Rt.Find("Tied").GetComponent<TextMeshProUGUI>().text = "— 비어 있음 —"; }
                p.Tied = b.Id; p.Thread.color = GPal.A(GPal.Wax, 0.95f); p.Rt.Find("Tied").GetComponent<TextMeshProUGUI>().text = "— " + CardRail.ShortTitle(b.Title);
                TrialFx.Sound("trial_lock", 0.6f); TrialFx.Sound("quill", 0.4f); _rail.Rebuild();
                return;
            }
            H.StartCoroutine(Tie(p, b, m));
        }

        IEnumerator Tie(Pin p, TrialGames.Bullet b, Vector2 m)
        {
            if (_busy) yield break; _busy = true;
            var th = UIKit.Rect(Root, "Tied", Vector2.zero, Vector2.one).gameObject.AddComponent<ThreadGraphic>(); th.raycastTarget = false; th.color = GPal.A(GPal.Wax, 0.95f); th.Width = 4.5f;
            var from = _rail.PinOf(b.Id); var to = CenterOf(p.Rt) + new Vector2(0, p.Rt.rect.height * 0.5f - 14);
            float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < 0.3f) { th.Sag = Mathf.Lerp(50, 4, GFx.K(t0, 0.3f)); th.Set(from, to); yield return null; }
            TrialFx.Sound("trial_lock", 0.6f);
            var r = TrialGames.BoardTie(Sim, p.Index, b.Id);
            if (r.Valid)
            {
                FlareCandles(); H.EyesStare(1f); if (r.Hourglass) { TrialFx.Sound("hourglass", 0.8f); H.StartCoroutine(GFx.Note(Root, null, "연달아 맞혔다 — 법정의 모래시계가 저절로 뒤집힌다.", new Vector2(0.5f, 0.92f), GPal.Ink, 1.5f)); }
                Speak(p.L.Speaker, null, Expr.Fear, Gesture.Flinch, 4, 2.5f);
                H.StartCoroutine(Fall(p.Rt)); p.Dropped = true; if (p.Thread != null) p.Thread.color = GPal.A(GPal.Brass, 0f);
                if (Probe) H.StartCoroutine(Later("trial_board_cut", 0.45f));
                yield return Wait(0.5f); Object.Destroy(th.gameObject);
                _end = Win(r); yield break;
            }
            // the thread snaps
            TrialFx.Sound("wood_crack", 0.8f);
            float s0 = Time.unscaledTime; while (Time.unscaledTime - s0 < 0.4f) { th.Snap = GFx.K(s0, 0.4f); th.Sag = 60 * GFx.K(s0, 0.4f); th.color = GPal.A(GPal.Wax, 0.95f * (1 - GFx.K(s0, 0.4f))); th.Set(from, to); yield return null; }
            Object.Destroy(th.gameObject);
            if (Probe) H.StartCoroutine(Later("trial_board_snap", 0.1f));
            if (r.Ended) { _end = Yield("실이 두 번 끊어졌다"); yield break; }
            yield return Lapse(r, p.L.Speaker);
            _busy = false;
        }

        IEnumerator Fall(RectTransform rt)
        {
            float t0 = Time.unscaledTime; var p0 = rt.anchoredPosition; float spin = Random.Range(-40f, 40f); TrialFx.Sound("parchment", 0.8f);
            while (rt != null && Time.unscaledTime - t0 < 0.8f) { float k = GFx.K(t0, 0.8f); rt.anchoredPosition = p0 + new Vector2(spin * 2 * k, -700 * k * k); rt.localRotation = Quaternion.Euler(0, 0, spin * k); yield return null; }
        }

        IEnumerator Win(TrialGames.ShotResult r) { TrialFx.Sound("choir_swell", 0.8f); yield return GFx.Plate(H.FxRoot, "끊어졌다", r.Text, GPal.Oxblood, 1.7f, "bell_toll"); }
        IEnumerator Yield(string why) { TrialGames.BoardYield(Sim); TrialFx.Sound("organ_sting", 0.5f); yield return GFx.Plate(H.FxRoot, why, Cast.NameOf(G.Opponent) + "의 주장이 힘을 얻는다", GPal.Smoke, 1.3f); }

        IEnumerator Knot()
        {
            var chosen = _pins.Select(p => p.Tied).ToList();
            var res = TrialGames.FinalBoard(Sim, chosen);
            for (int i = 0; i < _pins.Count; i++)
            {
                var p = _pins[i]; var s = p.Slot; bool ok = s.Result == "ok";
                if (ok) { p.Thread.color = GPal.Gilt; p.Thread.Width = 6; p.Thread.SetVerticesDirty(); TrialFx.Sound("wax_stamp", 0.7f); }
                else { if (p.Tied != null) H.StartCoroutine(SnapThread(p.Thread)); TrialFx.Sound("wood_crack", 0.6f); }
                var stamp = Goth.Engraved(p.Rt, "Stamp", ok ? "이어짐" : s.Result == "empty" ? "빈 매듭" : "끊어짐", 40, ok ? GPal.Gilt : GPal.InkRed); stamp.rectTransform.localRotation = Quaternion.Euler(0, 0, -10);
                if (s.Why != null) { var w = Goth.Text(p.Rt, "Why", s.Why, 15, GPal.Ink, TextAlignmentOptions.Bottom, true, Vector2.zero, Vector2.one, new Vector2(10, -34), new Vector2(-10, -130)); w.enableAutoSizing = true; w.fontSizeMin = 11; w.fontSizeMax = 15; }
                H.StartCoroutine(H.ShakeRect(p.Rt, 8, 0.2f));
                yield return Wait(0.55f);
            }
            if (Probe) AutoProbe.Shot("trial_board_final");
            bool won = G.Status == "won";
            Speak(G.Opponent, null, won ? Expr.Break : Expr.Smirk, won ? Gesture.Cower : Gesture.CrossArms, won ? 4 : 2, 3f);
            if (won && (G.Opponent == "P02" || G.Opponent == "P04")) H.BreakFor = G.Opponent;
            yield return Wait(0.6f);
            if (won) { TrialFx.Sound("choir_swell", 0.85f); H.EyesStare(1f); }
            yield return GFx.Plate(H.FxRoot, won ? "매듭지어졌다" : "매듭이 풀렸다", won ? "세 가닥의 실이 한 사람에게 모인다" : "논증은 결정타가 되지 못했다", won ? GPal.Oxblood : GPal.Smoke, 1.7f, won ? "bell_toll" : "organ_sting");
            H.BreakFor = null;
        }
        static IEnumerator SnapThread(ThreadGraphic th) { float t0 = Time.unscaledTime; var c = th.color; while (th != null && Time.unscaledTime - t0 < 0.4f) { th.Snap = GFx.K(t0, 0.4f); th.color = GPal.A(c, 1 - GFx.K(t0, 0.4f)); th.SetVerticesDirty(); yield return null; } }

        static void ArtBoard(RectTransform p)
        {
            var velvet = Goth.Frame(p, "V", Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10)); velvet.Fill = GPal.A(GPal.Velvet, 1f); velvet.FillBottom = GPal.A(new Color(0.1f, 0.01f, 0.02f), 1f); velvet.Border = GPal.A(GPal.Brass, 0.8f); velvet.Gap = 0;
            var top = UIKit.Rect(p, "Top", new Vector2(0.2f, 0.7f), new Vector2(0.8f, 0.88f)); Goth.Parchment(top, "P", Vector2.zero, Vector2.one);
            ArtText(top, "범인은 태겸이야.", 18, GPal.Ink, new Vector2(0.08f, 0), new Vector2(0.95f, 1), true);
            for (int i = 0; i < 2; i++)
            {
                var c = UIKit.Rect(p, "C" + i, new Vector2(0.1f + i * 0.46f, 0.36f), new Vector2(0.44f + i * 0.46f, 0.56f)); Goth.Parchment(c, "P", Vector2.zero, Vector2.one);
                ArtText(c, i == 0 ? "서재에 있었어" : "칼을 들었어", 15, GPal.Ink, new Vector2(0.08f, 0), new Vector2(0.98f, 1));
                ArtThread(p, new Vector2(0.27f + i * 0.46f, 0.56f), new Vector2(0.5f, 0.7f), GPal.A(GPal.Brass, 0.6f), 8f);
            }
            var card = UIKit.Rect(p, "Card", new Vector2(0.08f, 0.08f), new Vector2(0.46f, 0.24f)); Goth.Parchment(card, "P", Vector2.zero, Vector2.one);
            ArtSeal(card, new Vector2(0.16f, 0.5f), GPal.Wax, "증", 28); ArtText(card, "출입 기록", 15, GPal.Ink, new Vector2(0.32f, 0), new Vector2(0.98f, 1), true);
            ArtThread(p, new Vector2(0.14f, 0.24f), new Vector2(0.27f, 0.36f), GPal.A(GPal.Wax, 0.95f), 16f);
            ArtText(p, "끊는다", 18, GPal.Gilt, new Vector2(0.55f, 0.1f), new Vector2(0.95f, 0.22f), true);
        }

        // ------------------------------------------------------------ probe demo
        IEnumerator ProbeDemo()
        {
            UseVirt = true; yield return Wait(1f);
            AutoProbe.Shot("trial_board");
            if (Final)
            {
                var used = new HashSet<string>();
                foreach (var p in _pins)
                {
                    var b = _rail.Items.FirstOrDefault(x => !used.Contains(x.Id) && TrialGames.ProbeSlot(Sim, p.Slot.Id, x.Id)) ?? _rail.Items.FirstOrDefault(x => !used.Contains(x.Id));
                    if (b == null) continue; used.Add(b.Id); _rail.ShowPage(b.Id); yield return null;
                    var pp = p; yield return VirtDrag(_rail.PinOf(b.Id), () => CenterOf(pp.Rt), 0.5f); yield return Wait(0.2f);
                }
                AutoProbe.Shot("trial_board_tied"); yield return Wait(0.4f);
                if (_end == null) _end = Knot();
                yield break;
            }
            var targets = _pins.Where(p => p.L?.ClaimId != null).ToList();
            var wrongP = targets.FirstOrDefault(); var wrong = wrongP != null ? _rail.Items.FirstOrDefault(b => !TrialGames.ProbeWorks(S, wrongP.L.ClaimId, false, b.Id)) : null;
            if (wrong != null && G.Misses == 0) { _rail.ShowPage(wrong.Id); yield return null; yield return VirtDrag(_rail.PinOf(wrong.Id), () => CenterOf(wrongP.Rt), 0.5f); yield return Wait(4f); }
            foreach (var p in targets)
            {
                if (_end != null) break;
                var ok = _rail.Items.FirstOrDefault(b => TrialGames.ProbeWorks(S, p.L.ClaimId, false, b.Id)); if (ok == null) continue;
                _rail.ShowPage(ok.Id); yield return null; var pp = p; yield return VirtDrag(_rail.PinOf(ok.Id), () => CenterOf(pp.Rt), 0.6f); yield return Wait(1.5f);
            }
            yield return Wait(1.5f);
            if (_end == null) SpendTime(9999);
        }
    }
}
