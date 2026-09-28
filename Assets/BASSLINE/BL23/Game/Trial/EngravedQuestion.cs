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
    /// 새겨진 물음 — when a refutation has established something, the court itself poses the question: it surfaces engraved above the
    /// floor, and the answers lie as dark nameplates. Name it (TrialGames.QuestionPick). A wrong plate tarnishes and costs the jury's
    /// trust; two close the question. Nobody moderates: the room only watches.
    /// </summary>
    public sealed class EngravedQuestion : TrialMinigame
    {
        sealed class PlateUI { public string Word; public RectTransform Rt; public GFrame Frame; public TextMeshProUGUI Text; public RawImage Glow; public bool Dead; }
        readonly List<PlateUI> _plates = new List<PlateUI>(); IEnumerator _end; bool _busy;

        public EngravedQuestion(TrialDirectorUI h) : base(h) { }

        public override IEnumerator Run()
        {
            MusicDirector.I?.SetState(MusicState.TrialPressure);
            H.CamWide(44);
            yield return Teach("새겨진 물음", new[]
            {
                "반박이 무언가를 드러내면, 법정이 스스로 물음을 새긴다.",
                "알맞은 명판 하나를 클릭해 답한다.",
                "틀린 명판은 녹슬고 배심원의 신뢰가 깎인다. 두 번 틀리면 물음은 닫힌다.",
            }, ArtQuestion);
            Header("새겨진 물음", G.Subtitle);
            Candles(3, G.TimeLimit, new Vector2(0.5f, 1), Vector2.zero);
            // the question, carved into a lacquer panel
            var q = UIKit.Rect(Root, "Q", new Vector2(0.16f, 0.62f), new Vector2(0.84f, 0.8f));
            Goth.Panel(q, "F", Vector2.zero, Vector2.one);
            var lab = Goth.Text(q, "Who", "법정의 물음  ·  " + (G.Subtitle ?? ""), 19, GPal.Brass, TextAlignmentOptions.TopLeft, true, Vector2.zero, Vector2.one, new Vector2(40, 16), new Vector2(-30, -16)); lab.characterSpacing = 4;
            var qt = Goth.Text(q, "T", LineBank.FixParticles(G.Question ?? ""), 40, GPal.Bone, TextAlignmentOptions.MidlineLeft, true, Vector2.zero, Vector2.one, new Vector2(40, 8), new Vector2(-30, -40)); qt.enableAutoSizing = true; qt.fontSizeMin = 24; qt.fontSizeMax = 40;
            // the answers: dark nameplates
            var tray = UIKit.Rect(Root, "Tray", new Vector2(0.16f, 0.14f), new Vector2(0.84f, 0.56f));
            int n = G.Pool.Count; int cols = n <= 4 ? 2 : 3; int rows = Mathf.CeilToInt(n / (float)cols);
            for (int i = 0; i < n; i++)
            {
                int cx = i % cols, cy = i / cols;
                var rt = UIKit.Rect(tray, "Plate" + i, new Vector2(cx / (float)cols, 1 - (cy + 1) / (float)rows), new Vector2((cx + 1) / (float)cols, 1 - cy / (float)rows), new Vector2(16, 14), new Vector2(-16, -14));
                var glow = Goth.Glow(rt, "Glow", GPal.A(GPal.Candle, 0f), new Vector2(560, 260), Vector2.zero);
                var f = Goth.Panel(rt, "F", Vector2.zero, Vector2.one);
                var t = Goth.Text(rt, "T", G.Pool[i], 32, GPal.Gilt, TextAlignmentOptions.Center, true, Vector2.zero, Vector2.one, new Vector2(20, 8), new Vector2(-20, -8)); t.enableAutoSizing = true; t.fontSizeMin = 18; t.fontSizeMax = 32; t.characterSpacing = 3;
                _plates.Add(new PlateUI { Word = G.Pool[i], Rt = rt, Frame = f, Text = t, Glow = glow });
            }
            HintLine("클릭|명판을 골라 답하기 · 두 번 틀리면 물음이 닫힌다");
            TrialFx.Sound("bell_toll", 0.5f); H.EyesStare(0.4f);
            if (Probe) H.StartCoroutine(ProbeDemo());
            while (_end == null) { Tick(); EndVirtFrame(); yield return null; }
            yield return _end;
            Done = true;
        }

        void Tick()
        {
            if (!TickCandles(Dt) && _end == null) { TrialGames.Timeout(Sim); _end = GFx.Plate(H.FxRoot, "대답하지 못했다", "새겨진 글자가 천천히 흐려진다", GPal.Smoke, 1.1f); return; }
            var m = MouseLocal(); PlateUI hovered = null;
            foreach (var p in _plates)
            {
                bool hov = !p.Dead && !_busy && Inside(p.Rt, m); if (hov) hovered = p;
                p.Rt.localScale = Vector3.Lerp(p.Rt.localScale, Vector3.one * (hov ? 1.035f : 1f), Dt * 12);
                if (!p.Dead) { p.Frame.Border = hov ? GPal.Gilt : GPal.A(GPal.Brass, 0.85f); p.Frame.FillBottom = hov ? GPal.A(new Color(0.16f, 0.05f, 0.05f), 1f) : GPal.A(new Color(0.03f, 0.02f, 0.016f), 1f); p.Frame.Refresh(); p.Glow.color = GPal.A(GPal.Candle, Mathf.MoveTowards(p.Glow.color.a, hov ? 0.18f : 0f, Dt * 1.5f)); }
                if (hov && MouseDown) H.StartCoroutine(Pick(p));
            }
            HoverSfx(hovered);
        }

        IEnumerator Pick(PlateUI p)
        {
            if (_busy) yield break; _busy = true; TrialFx.Sound("wax_stamp", 0.6f);
            int r = TrialGames.QuestionPick(Sim, p.Word);
            if (r == 2)
            {
                p.Frame.Fill = GPal.Gilt; p.Frame.FillBottom = GPal.A(new Color(0.72f, 0.56f, 0.28f), 1f); p.Frame.Border = Color.white; p.Frame.Refresh(); p.Text.color = GPal.Lacquer; FlareCandles();
                p.Glow.color = GPal.A(GPal.Candle, 0.5f);
                foreach (var o in _plates.Where(x => x != p)) { var cg = o.Rt.gameObject.AddComponent<CanvasGroup>(); H.StartCoroutine(FadeTo(cg, 0.35f, 0.5f)); }
                TrialFx.Sound("choir_swell", 0.8f); H.EyesStare(0.9f);
                Speak(Cast.Player, null, Expr.Neutral, Gesture.Point, 0, 3f);
                if (Probe) H.StartCoroutine(Later("trial_question_right", 0.5f));
                yield return Wait(0.8f);
                _end = GFx.Plate(H.FxRoot, p.Word, "모두가 그 사실을 받아들였다", GPal.Oxblood, 1.6f, "bell_toll"); yield break;
            }
            p.Dead = true; p.Frame.Fill = GPal.A(new Color(0.16f, 0.18f, 0.14f), 1f); p.Frame.FillBottom = GPal.A(new Color(0.08f, 0.09f, 0.07f), 1f); p.Frame.Border = GPal.A(GPal.Smoke, 0.5f); p.Frame.Refresh(); p.Text.color = GPal.A(GPal.Bone, 0.4f);
            var strike = UIKit.Img(p.Rt, "Strike", GPal.A(GPal.InkRed, 0.85f), new Vector2(0.15f, 0.5f), new Vector2(0.85f, 0.5f), new Vector2(0, -2), new Vector2(0, 2)); strike.rectTransform.localRotation = Quaternion.Euler(0, 0, -4);
            H.ShakeCam(0.2f); H.StartCoroutine(H.ShakeRect(p.Rt, 6, 0.25f));
            if (Probe) H.StartCoroutine(Later("trial_question_wrong", 0.3f));
            if (r == -2) { TrialFx.Sound("organ_sting", 0.7f); _end = GFx.Plate(H.FxRoot, "물음이 닫혔다", "두 번 빗나갔다 — 새겨진 글자가 흐려진다", GPal.Smoke, 1.2f, "candle_snuff"); yield break; }
            yield return Lapse(new TrialGames.ShotResult(), null);
            yield return GFx.Note(Root, null, "명판 하나가 녹슬었다. 한 번 더 틀리면 물음은 닫힌다.", new Vector2(0.5f, 0.6f), GPal.Ink, 1.2f);
            _busy = false;
        }

        static IEnumerator FadeTo(CanvasGroup cg, float a, float secs) { float a0 = cg.alpha, t0 = Time.unscaledTime; while (cg != null && Time.unscaledTime - t0 < secs) { cg.alpha = Mathf.Lerp(a0, a, GFx.K(t0, secs)); yield return null; } }

        static void ArtQuestion(RectTransform p)
        {
            ArtText(p, "그 시각, 태겸이 실제로 있던 곳은?", 22, GPal.Ink, new Vector2(0.08f, 0.72f), new Vector2(0.95f, 0.9f), true);
            string[] w = { "서재", "음악실", "세탁실", "식당" };
            for (int i = 0; i < 4; i++)
            {
                int cx = i % 2, cy = i / 2; var rt = UIKit.Rect(p, "P" + i, new Vector2(0.08f + cx * 0.44f, 0.36f - cy * 0.26f), new Vector2(0.48f + cx * 0.44f, 0.58f - cy * 0.26f));
                var f = Goth.Frame(rt, "F", Vector2.zero, Vector2.one); f.Fill = i == 1 ? GPal.Gilt : GPal.A(new Color(0.06f, 0.045f, 0.03f), 1f); f.FillBottom = i == 1 ? GPal.A(new Color(0.72f, 0.56f, 0.28f), 1f) : GPal.A(GPal.Lacquer, 1f); f.Border = GPal.Brass; f.Gap = 0;
                ArtText(rt, w[i], 20, i == 1 ? GPal.Lacquer : GPal.Gilt, Vector2.zero, Vector2.one, true, TextAlignmentOptions.Center);
            }
        }

        IEnumerator ProbeDemo()
        {
            UseVirt = true; yield return Wait(1.2f); AutoProbe.Shot("trial_question");
            var wrong = _plates.FirstOrDefault(p => p.Word != G.Word); if (wrong != null) { yield return VirtClick(CenterOf(wrong.Rt)); yield return Wait(3.2f); }
            var right = _plates.FirstOrDefault(p => p.Word == G.Word); if (right != null && _end == null) yield return VirtClick(CenterOf(right.Rt));
            yield return Wait(2f); if (_end == null) SpendTime(9999);
        }
    }
}
