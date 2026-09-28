using BL23.Game.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace BL23.Game
{
    /// <summary>Filled ring / disc (annulus) — clock faces, medallion rims and masks, pins, stones.</summary>
    public sealed class RingGraphic : MaskableGraphic
    {
        public float Thickness = 6f; public int Segments = 64; public float Fill = 1f; public int Dashes; public float StartAngle = 90f;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; float R = Mathf.Min(r.width, r.height) * 0.5f; float ri = Mathf.Max(0, R - Thickness); var c = r.center;
            int n = Mathf.Max(8, Segments); int drawn = Mathf.CeilToInt(n * Mathf.Clamp01(Fill));
            for (int i = 0; i < drawn; i++)
            {
                if (Dashes > 0 && (i * Dashes * 2 / n) % 2 == 1) continue;
                float a0 = (StartAngle - 360f * i / n) * Mathf.Deg2Rad, a1 = (StartAngle - 360f * (i + 1) / n) * Mathf.Deg2Rad;
                Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)), d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                int k = vh.currentVertCount;
                vh.AddVert(c + d0 * ri, color, Vector2.zero); vh.AddVert(c + d0 * R, color, Vector2.zero); vh.AddVert(c + d1 * R, color, Vector2.zero); vh.AddVert(c + d1 * ri, color, Vector2.zero);
                vh.AddTriangle(k, k + 1, k + 2); vh.AddTriangle(k, k + 2, k + 3);
            }
        }
        public void Refresh() => SetVerticesDirty();
    }

    /// <summary>Small layout / sound helpers shared by the trial presentation.</summary>
    public static class TrialFx
    {
        public static RectTransform Centered(Transform parent, string name, Vector2 size, Vector2 pos = default)
        {
            var rt = UIKit.Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)); rt.sizeDelta = size; rt.anchoredPosition = pos; return rt;
        }

        public static void Sound(string id, float vol = 1f) { try { Sfx.Play(id, null, vol); } catch (System.Exception) { } }
    }
}
