using System.Collections;
using BL23.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BL23.Game
{
    /// <summary>Courtroom effects in the house style: wax seals, engraved plates, margin notes, interjections, chapter cards.
    /// Everything fades in and out (no pops, no full-screen tints, no grain).</summary>
    public static class GFx
    {
        public static float K(float t0, float dur) => Mathf.Clamp01((Time.unscaledTime - t0) / Mathf.Max(0.001f, dur));
        public static float EaseOut(float k) => 1 - (1 - k) * (1 - k) * (1 - k);
        public static float EaseInOut(float k) => k * k * (3 - 2 * k);
        static float Fast(float secs) => TrialDirectorUI.ProbeFast ? Mathf.Min(secs, 0.45f) : secs;

        static CanvasGroup Group(RectTransform rt) { var g = rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>(); g.blocksRaycasts = false; g.interactable = false; return g; }
        static IEnumerator FadeIn(RectTransform rt, CanvasGroup g, float secs, Vector2 drift)
        {
            var p1 = rt.anchoredPosition; var p0 = p1 + drift; float t0 = Time.unscaledTime;
            while (rt != null && Time.unscaledTime - t0 < secs) { float k = EaseOut(K(t0, secs)); g.alpha = k; rt.anchoredPosition = Vector2.LerpUnclamped(p0, p1, k); yield return null; }
            if (rt != null) { g.alpha = 1; rt.anchoredPosition = p1; }
        }
        static IEnumerator FadeOut(RectTransform rt, CanvasGroup g, float secs, Vector2 drift)
        {
            if (rt == null) yield break; var p0 = rt.anchoredPosition; float t0 = Time.unscaledTime;
            while (rt != null && Time.unscaledTime - t0 < secs) { float k = EaseInOut(K(t0, secs)); g.alpha = 1 - k; rt.anchoredPosition = p0 + drift * k; yield return null; }
            if (rt != null) Object.Destroy(rt.gameObject);
        }
        static bool Skip() => Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space);

        /// <summary>An interjection: a slim lacquer banner eases in under the top edge with the speaker's medallion and a few words.</summary>
        public static IEnumerator Interject(RectTransform root, string speaker, string phrase, Color accent, float hold = 0.8f)
        {
            var rt = UIKit.Rect(root, "Interject", new Vector2(0.24f, 1), new Vector2(0.72f, 1), new Vector2(0, -196), new Vector2(0, -76));
            var g = Group(rt);
            var f = Goth.Panel(rt, "F", Vector2.zero, Vector2.one); f.Border = GPal.A(accent, 0.9f);
            UIKit.Img(rt, "Accent", GPal.A(accent, 0.95f), new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 8), new Vector2(4, -8));
            var med = Goth.Medallion(rt, "Face", speaker, new Vector2(92, 92), Vector2.zero); med.anchorMin = med.anchorMax = new Vector2(0, 0.5f); med.anchoredPosition = new Vector2(66, 0);
            var name = Goth.Text(rt, "Name", Cast.NameOf(speaker) ?? "", 19, GPal.A(GPal.Brass, 0.95f), TextAlignmentOptions.TopLeft, true, Vector2.zero, Vector2.one, new Vector2(128, 12), new Vector2(-24, -14)); name.characterSpacing = 4;
            var t = Goth.Text(rt, "Phrase", phrase, 44, GPal.Bone, TextAlignmentOptions.MidlineLeft, true, Vector2.zero, Vector2.one, new Vector2(128, 0), new Vector2(-24, -22));
            t.textWrappingMode = TextWrappingModes.NoWrap; t.enableAutoSizing = true; t.fontSizeMin = 24; t.fontSizeMax = 44;
            TrialFx.Sound("wax_stamp", 0.45f);
            yield return FadeIn(rt, g, 0.35f, new Vector2(-36, 0));
            yield return new WaitForSecondsRealtime(Fast(hold));
            yield return FadeOut(rt, g, 0.4f, new Vector2(24, 0));
        }

        /// <summary>A wax seal pressed onto a spot: returns the seal so the caller can crack it or leave it.</summary>
        public static SealGraphic Seal(RectTransform parent, Vector2 pos, Color wax, string letter, float size = 84)
        {
            var rt = TrialFx.Centered(parent, "Seal", new Vector2(size, size), pos);
            var s = rt.gameObject.AddComponent<SealGraphic>(); s.Wax = wax; s.Seed = Random.Range(1, 9999); s.raycastTarget = false; Group(rt);
            var t = Goth.Text(rt, "L", letter, size * 0.42f, Color.Lerp(wax, Color.black, 0.5f), TextAlignmentOptions.Center, true); t.outlineWidth = 0.08f; t.outlineColor = new Color32(255, 220, 200, 80);
            return s;
        }

        public static IEnumerator Slam(SealGraphic s, float secs = 0.22f)
        {
            if (s == null) yield break; var rt = s.rectTransform; float t0 = Time.unscaledTime; rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-14f, 14f));
            TrialFx.Sound("wax_stamp", 0.85f);
            var cg = rt.GetComponent<CanvasGroup>();
            while (rt != null && Time.unscaledTime - t0 < secs) { float k = K(t0, secs); rt.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, EaseOut(k)); if (cg != null) cg.alpha = Mathf.Clamp01(k * 2.5f); yield return null; }
            if (cg != null) cg.alpha = 1;
            if (rt != null) rt.localScale = Vector3.one;
        }

        public static IEnumerator CrackAndFall(SealGraphic s)
        {
            if (s == null) yield break; float t0 = Time.unscaledTime; TrialFx.Sound("wood_crack", 0.6f);
            while (s != null && Time.unscaledTime - t0 < 0.35f) { s.Crack = K(t0, 0.35f); s.Refresh(); yield return null; }
            var rt = s != null ? s.rectTransform : null; float f0 = Time.unscaledTime; var p0 = rt != null ? rt.anchoredPosition : Vector2.zero; var cg = rt != null ? rt.GetComponent<CanvasGroup>() : null;
            while (rt != null && Time.unscaledTime - f0 < 0.6f) { float k = K(f0, 0.6f); rt.anchoredPosition = p0 + new Vector2(14 * k, -110 * k * k); rt.localRotation = Quaternion.Euler(0, 0, 30 * k); if (cg != null) cg.alpha = 1 - k; yield return null; }
            if (rt != null) Object.Destroy(rt.gameObject);
        }

        /// <summary>The engraved result of a move ("반증", "모순", "끊어졌다"…) on a lacquer plate. The sound tells success from failure
        /// before the eye does: choir for a truth, organ for a mistake, a snuffed candle for a round that simply ends.</summary>
        public static IEnumerator Plate(RectTransform root, string word, string sub, Color accent, float hold = 1.3f, string sound = null)
        {
            var rt = UIKit.Rect(root, "Plate", new Vector2(0.24f, 0.38f), new Vector2(0.76f, 0.62f));
            var dim = UIKit.Img(root, "PlateDim", GPal.A(Color.black, 0f), Vector2.zero, Vector2.one); dim.raycastTarget = false; dim.transform.SetSiblingIndex(rt.GetSiblingIndex());
            var g = Group(rt);
            var glow = Goth.Glow(rt, "Glow", GPal.A(accent, 0.3f), new Vector2(1300, 460), Vector2.zero);
            var fr = Goth.Panel(rt, "Frame", Vector2.zero, Vector2.one); fr.FillBottom = GPal.A(Color.Lerp(new Color(0.03f, 0.02f, 0.016f), accent, 0.22f), 1f); fr.Border = GPal.A(GPal.Gilt, 0.9f);
            UIKit.Img(rt, "RuleA", GPal.A(accent, 0.9f), new Vector2(0.3f, 0.36f), new Vector2(0.7f, 0.36f), new Vector2(0, -1), new Vector2(0, 1));
            var w = Goth.Engraved(rt, "Word", word, 88, GPal.Gilt); w.rectTransform.anchorMin = new Vector2(0, 0.4f); w.rectTransform.anchorMax = new Vector2(1, 0.96f); w.enableAutoSizing = true; w.fontSizeMin = 40; w.fontSizeMax = 88;
            if (sub != null) { var s = Goth.Text(rt, "Sub", sub, 26, GPal.Bone, TextAlignmentOptions.Center, false, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.33f)); s.enableAutoSizing = true; s.fontSizeMin = 17; s.fontSizeMax = 26; }
            TrialFx.Sound(sound ?? (accent == GPal.Smoke ? "candle_snuff" : "wax_stamp"), 0.85f);
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.4f) { float k = EaseOut(K(t0, 0.4f)); g.alpha = k; rt.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, k); dim.color = GPal.A(Color.black, 0.3f * k); yield return null; }
            rt.localScale = Vector3.one; g.alpha = 1;
            float h0 = Time.unscaledTime; float hold2 = TrialDirectorUI.ProbeFast ? Mathf.Min(hold, 0.9f) : hold;
            while (Time.unscaledTime - h0 < hold2) { glow.color = GPal.A(accent, 0.24f + Mathf.Sin((Time.unscaledTime - h0) * 2.4f) * 0.05f); if (Time.unscaledTime - h0 > 0.5f && Skip()) break; yield return null; }
            float f0 = Time.unscaledTime;
            while (Time.unscaledTime - f0 < 0.35f) { float k = EaseInOut(K(f0, 0.35f)); g.alpha = 1 - k; dim.color = GPal.A(Color.black, 0.3f * (1 - k)); yield return null; }
            Object.Destroy(rt.gameObject); Object.Destroy(dim.gameObject);
        }

        /// <summary>A handwritten margin note (a retort, a steering remark, the room's own voice) pinned for a moment.</summary>
        public static IEnumerator Note(RectTransform root, string who, string text, Vector2 anchor, Color ink, float secs = 2f)
        {
            if (string.IsNullOrEmpty(text)) yield break;
            var rt = UIKit.Rect(root, "Note", anchor, anchor); rt.sizeDelta = new Vector2(640, 116); rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-1.2f, 1.2f));
            var g = Group(rt);
            var sh = UIKit.Rect(rt, "Shade", Vector2.zero, Vector2.one, new Vector2(-16, -24), new Vector2(16, 8)); var si = sh.gameObject.AddComponent<RawImage>(); si.texture = GTex.Glow; si.color = new Color(0, 0, 0, 0.5f); si.raycastTarget = false;
            Goth.Parchment(rt, "P", Vector2.zero, Vector2.one);
            if (who != null) { var m = Goth.Medallion(rt, "M", who, new Vector2(76, 76), Vector2.zero); m.anchorMin = m.anchorMax = new Vector2(0, 0.5f); m.anchoredPosition = new Vector2(52, 0); }
            var t = Goth.Text(rt, "T", (who != null ? $"<size=72%><color={GPal.Hex(GPal.InkRed)}>{Cast.NameOf(who)}</color></size>\n" : "") + LineBank.FixParticles(string.Join(" ", LineBank.Pages(text))), 24, ink, TextAlignmentOptions.MidlineLeft, false, Vector2.zero, Vector2.one, new Vector2(who != null ? 102 : 24, 8), new Vector2(-20, -8));
            t.enableAutoSizing = true; t.fontSizeMin = 15; t.fontSizeMax = 24;
            TrialFx.Sound("quill", 0.5f);
            yield return FadeIn(rt, g, 0.25f, new Vector2(0, -14));
            float h0 = Time.unscaledTime; while (rt != null && Time.unscaledTime - h0 < Fast(secs)) yield return null;
            yield return FadeOut(rt, g, 0.3f, new Vector2(0, 10));
        }

        /// <summary>A turn of events: the room darkens a little and a card rises — "반전".</summary>
        public static IEnumerator Twist(RectTransform root, string who, string text)
        {
            var dim = UIKit.Img(root, "TwistDim", GPal.A(Color.black, 0f), Vector2.zero, Vector2.one); dim.raycastTarget = false;
            var rt = UIKit.Rect(root, "Twist", new Vector2(0.32f, 0.3f), new Vector2(0.68f, 0.72f)); var g = Group(rt);
            var fr = Goth.Frame(rt, "F", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true); fr.Fill = GPal.A(GPal.Velvet, 1f); fr.FillBottom = GPal.A(GPal.Lacquer, 1f); fr.Border = GPal.Gilt; fr.Width = 1.5f;
            Goth.Medallion(rt, "M", who, new Vector2(140, 140), new Vector2(0, 26));
            var w = Goth.Engraved(rt, "W", "반전", 64, GPal.Gilt); w.rectTransform.anchorMin = new Vector2(0, 0.72f); w.rectTransform.anchorMax = new Vector2(1, 0.94f);
            Goth.Text(rt, "T", LineBank.FixParticles(text ?? ""), 26, GPal.Bone, TextAlignmentOptions.Center, true, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.26f));
            TrialFx.Sound("bell_toll", 0.6f); TrialFx.Sound("discovery", 0.35f);
            float t0 = Time.unscaledTime; var p1 = rt.anchoredPosition;
            while (Time.unscaledTime - t0 < 0.6f) { float k = EaseOut(K(t0, 0.6f)); g.alpha = k; rt.anchoredPosition = p1 + new Vector2(0, -30 * (1 - k)); dim.color = GPal.A(Color.black, 0.45f * k); yield return null; }
            g.alpha = 1; rt.anchoredPosition = p1;
            yield return new WaitForSecondsRealtime(TrialDirectorUI.ProbeFast ? 0.9f : 1.9f);
            float f0 = Time.unscaledTime; while (Time.unscaledTime - f0 < 0.45f) { float k = EaseInOut(K(f0, 0.45f)); g.alpha = 1 - k; dim.color = GPal.A(Color.black, 0.45f * (1 - k)); yield return null; }
            Object.Destroy(rt.gameObject); Object.Destroy(dim.gameObject);
        }

        /// <summary>A chapter of the trial begins: the bell tolls, a Roman numeral and the chapter's name rise out of the dark and settle.</summary>
        public static IEnumerator Chapter(RectTransform root, string numeral, string title, string sub)
        {
            var rt = UIKit.Rect(root, "Chapter", new Vector2(0, 0.38f), new Vector2(1, 0.66f)); var g = Group(rt);
            var bed = UIKit.Rect(rt, "Bed", new Vector2(0.15f, 0), new Vector2(0.85f, 1), new Vector2(0, -40), new Vector2(0, 40)); var bi = bed.gameObject.AddComponent<RawImage>(); bi.texture = GTex.Glow; bi.color = new Color(0, 0, 0, 0.7f); bi.raycastTarget = false;
            var num = Goth.Text(rt, "N", numeral ?? "", 26, GPal.A(GPal.Brass, 0.9f), TextAlignmentOptions.Center, false, new Vector2(0, 0.72f), new Vector2(1, 0.95f)); num.characterSpacing = 14;
            var t = Goth.Engraved(rt, "T", title ?? "", 66, GPal.Gilt); t.rectTransform.anchorMin = new Vector2(0, 0.3f); t.rectTransform.anchorMax = new Vector2(1, 0.74f); t.characterSpacing = 12;
            var rule = UIKit.Img(rt, "Rule", GPal.A(GPal.Brass, 0.8f), new Vector2(0.4f, 0.28f), new Vector2(0.6f, 0.28f), new Vector2(0, -1), new Vector2(0, 1));
            if (!string.IsNullOrEmpty(sub)) { var s = Goth.Text(rt, "S", sub, 24, GPal.Bone, TextAlignmentOptions.Center, false, new Vector2(0.1f, 0), new Vector2(0.9f, 0.24f)); s.characterSpacing = 3; }
            TrialFx.Sound("bell_toll", 0.8f);
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.8f) { float k = EaseOut(K(t0, 0.8f)); g.alpha = k; rule.rectTransform.localScale = new Vector3(k, 1, 1); yield return null; }
            g.alpha = 1; rule.rectTransform.localScale = Vector3.one;
            float h0 = Time.unscaledTime; while (Time.unscaledTime - h0 < (TrialDirectorUI.ProbeFast ? 0.5f : 1.5f)) { if (Time.unscaledTime - h0 > 0.4f && Skip()) break; yield return null; }
            float f0 = Time.unscaledTime; while (Time.unscaledTime - f0 < 0.6f) { g.alpha = 1 - EaseInOut(K(f0, 0.6f)); yield return null; }
            Object.Destroy(rt.gameObject);
        }
    }
}
