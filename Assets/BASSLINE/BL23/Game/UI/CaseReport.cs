using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using TMPro;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// 심판 전 정리: shown once when the trial is summoned — the four case questions with what is known, the important clues
    /// by title, and the two court keys to remember. E (or Esc / Space / click) closes it.
    /// </summary>
    public sealed class CaseReport : MonoBehaviour
    {
        public static bool Open; public static int ClosedFrame = -1;
        static CaseReport _i;
        Session _s; Canvas _c; float _openedAt; string _shownFor;

        public static void Show(Session s)
        {
            if (s == null || s.S == null) return;
            var inc = CaseProgress.Current(s.S); if (inc == null) return;
            if (_i == null) { var c = UIKit.Root("CaseReport", 55); _i = c.gameObject.AddComponent<CaseReport>(); _i._c = c; c.enabled = false; }
            if (_i._shownFor == inc.Id && Open) return;
            _i._s = s; _i._shownFor = inc.Id; _i.Build(inc);
            _i._c.enabled = true; _i._openedAt = Time.unscaledTime; Open = true;
            s.Pause("report"); UISfx.Page();
        }

        public static void Close()
        {
            if (!Open) return; Open = false; ClosedFrame = Time.frameCount;
            if (_i != null) { _i._c.enabled = false; UIKit.Clear(_i._c.transform); _i._s?.Resume("report"); }
        }

        void OnDestroy() { if (_i == this) { _i = null; if (Open) { Open = false; _s?.Resume("report"); } } }

        void Build(Incident inc)
        {
            var t = _c.transform; UIKit.Clear(t); var S = _s.S; var sim = _s.Sim;
            UIKit.Img(t, "Dim", Pal.A(Pal.Ink, 0.7f), Vector2.zero, Vector2.one).raycastTarget = true;
            const float w = 1180, h = 820;
            var p = UIKit.Slant(t, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-w / 2, -h / 2), new Vector2(w / 2, h / 2), Pal.A(Pal.Panel2, 0.99f), Pal.A(Pal.Ink, 0.99f), Pal.A(Pal.Gold, 0.9f), 0);
            p.Glow = 0.25f; UIKit.FitToCanvas(p.rectTransform, w, h);
            UIKit.Text(p.transform, "Kicker", "심판 전 정리", 20, Pal.A(Pal.Gold, 0.9f), TextAlignmentOptions.Top, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(40, 22), new Vector2(-40, -24)).characterSpacing = 6;
            UIKit.Text(p.transform, "Title", $"{Cast.NameOf(inc.Victim)} — {S.RoomName(inc.FoundRoom)}", 40, Pal.Text, TextAlignmentOptions.Top, Fonts.Title, Vector2.zero, Vector2.one, new Vector2(40, 22), new Vector2(-40, -54));
            UIKit.Img(p.transform, "Rule", Pal.A(Pal.Gold, 0.5f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(70, -118), new Vector2(-70, -117));
            // the four questions
            List<CaseBoard.Question> qs = null; try { qs = CaseBoard.Questions(sim); } catch (System.Exception e) { Debug.LogWarning("[BL23] Questions: " + e.Message); }
            var sb = new System.Text.StringBuilder();
            sb.Append("<color=#D6AD62>사건 개요</color>");
            int firm = 0;
            if (qs != null && qs.Count > 0)
                foreach (var q in qs.OrderBy(x => x.Index))
                {
                    if (q.Firm) firm++;
                    string ans = string.IsNullOrEmpty(q.Answer) ? "<color=#8A7E6C>아직 모른다</color>" : q.Firm ? q.Answer : $"<color=#9A8E7C>{q.Answer} · 더 확인 필요</color>";
                    sb.Append("\n").Append(q.Firm ? "<color=#D6AD62>●</color>" : "<color=#9A8E7C>○</color>").Append("  ").Append(q.Ask).Append("  <color=#9A8E7C>·</color>  ").Append(ans);
                }
            else sb.Append("\n<color=#9A8E7C>정리할 수 있는 것이 아직 없다.</color>");
            UIKit.Text(p.transform, "Questions", sb.ToString(), 25, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(80, 300), new Vector2(-70, -140)).lineSpacing = 10;
            // the important clues, by title
            List<string> keys = new List<string>();
            try { keys = CaseBoard.Cards(sim).Where(v => v.Key && v.InCase).Select(v => v.Title).Where(x => !string.IsNullOrEmpty(x)).ToList(); } catch (System.Exception e) { Debug.LogWarning("[BL23] Cards: " + e.Message); }
            string keyText = keys.Count == 0 ? "<color=#9A8E7C>아직 없다 — 수첩(Tab)의 단서를 살펴보자.</color>"
                : string.Join("   ", keys.Take(12).Select(k => "<color=#D6AD62>◆</color> " + k.Replace(" ", " "))) + (keys.Count > 12 ? $"   <color=#9A8E7C>외 {keys.Count - 12}건</color>" : "");   // no-break spaces: a line never ends on a lone ◆ or splits a title
            UIKit.Text(p.transform, "KeysHead", $"<color=#D6AD62>중요 단서</color> <color=#9A8E7C>({keys.Count})</color>", 25, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Serif, new Vector2(0, 0), new Vector2(1, 0), new Vector2(80, 250), new Vector2(-70, 290));
            UIKit.Text(p.transform, "Keys", keyText, 22, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Body, new Vector2(0, 0), new Vector2(1, 0), new Vector2(80, 110), new Vector2(-70, 248)).lineSpacing = 8;
            UIKit.Img(p.transform, "Rule2", Pal.A(Pal.Gold, 0.35f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(70, 92), new Vector2(-70, 93));
            UIKit.Text(p.transform, "Court", "심판에서:  <color=#D6AD62>F</color> 발언 되짚기  ·  <color=#D6AD62>Tab</color> 수첩", 23, Pal.Text, TextAlignmentOptions.MidlineLeft, Fonts.Serif, new Vector2(0, 0), new Vector2(1, 0), new Vector2(80, 30), new Vector2(-70, 86));
            UIKit.Text(p.transform, "Close", "E 닫기", 19, Pal.A(Pal.TextDim, 0.9f), TextAlignmentOptions.MidlineRight, Fonts.Body, new Vector2(0, 0), new Vector2(1, 0), new Vector2(80, 30), new Vector2(-50, 86));
            Debug.Log($"[BL23] 심판 전 정리: 개요 {firm}/4, 중요 단서 {keys.Count}");
        }

        void Update()
        {
            if (!Open || _s == null) return;
            if ((_s.Trial?.Active ?? false) || _s.S == null || _s.S.Phase == Phase.Trial || _s.S.Phase == Phase.Verdict) { Close(); return; }
            float age = Time.unscaledTime - _openedAt;
            if (AutoProbe.Active && age > 4f) { Close(); return; }
            if (age < 0.4f) return;
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)) { UISfx.Confirm(); Close(); }
        }
    }
}
