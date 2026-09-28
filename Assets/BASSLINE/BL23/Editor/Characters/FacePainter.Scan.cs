using System.Collections.Generic;
using BL23.Game.Characters;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Eye cells for the procedural cast built from a scanned eye template (GlbEyeTemplate): the recoloured scan eye
    /// is the open eye, the lid / blink / expression cells are painted over it exactly like on the scanned heads, with
    /// the character's own skin colour. Mouths and brows stay procedural (painted in the matching thin-line style).
    /// </summary>
    public sealed partial class FacePainter
    {
        public GlbEyeTemplate.Tpl ScanEyes;     // set -> eye cells come from the scan template
        public Color[] ScanEyePx;               // recoloured template (GlbEyeTemplate.Res^2)

        GlbFaceMask.GlbArt SyntheticArt()
        {
            var t = ScanEyes;
            var art = new GlbFaceMask.GlbArt
            {
                L = new GlbFace.Layout { C = t.C, EyeHalfM = t.HalfM, F = new GlbFace.Frame { Scale = 0.21f } },
                EhMM = t.HalfM * 1000f,
                Skin = Skin,
                Lash = Color.Lerp(new Color(0.08f, 0.06f, 0.07f), Hair * 0.5f, 0.3f),
            };
            // lids: the character's skin, a touch warmer / darker toward the lash line
            for (int i = 0; i < GlbFaceMask.GlbArt.EyeRes; i++)
            {
                art.LidTop[i] = Color.Lerp(Skin, Skin * new Color(0.93f, 0.86f, 0.88f, 1f), 0.5f); art.LidTop[i].a = 1f;
                art.LidBot[i] = Skin;
            }
            return art;
        }

        void BlitTemplate(Cell c)
        {
            int R = GlbEyeTemplate.Res;
            for (int y = 0; y < c.H; y++)
                for (int x = 0; x < c.W; x++)
                {
                    float u = (x + 0.5f) / c.W * R - 0.5f, v = (y + 0.5f) / c.H * R - 0.5f;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, R - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(v), 0, R - 1);
                    int x1 = Mathf.Min(x0 + 1, R - 1), y1 = Mathf.Min(y0 + 1, R - 1);
                    float fx = Mathf.Clamp01(u - x0), fy = Mathf.Clamp01(v - y0);
                    // premultiplied bilinear so the colour bleed never shows
                    Color Pm(Color q) => new Color(q.r * q.a, q.g * q.a, q.b * q.a, q.a);
                    Color s = Color.Lerp(Color.Lerp(Pm(ScanEyePx[y0 * R + x0]), Pm(ScanEyePx[y0 * R + x1]), fx), Color.Lerp(Pm(ScanEyePx[y1 * R + x0]), Pm(ScanEyePx[y1 * R + x1]), fx), fy);
                    if (s.a <= 0.001f) continue;
                    var col = new Color(s.r / s.a, s.g / s.a, s.b / s.a, 1f);
                    c.C.Blend(c.X0 + x, c.Y0 + y, col, s.a);
                }
        }

        void PaintScanEye(Cell c, int idx)
        {
            bool closedType = idx == FaceCells.EyeClosed || idx == FaceCells.EyeHappy || idx == FaceCells.EyeLaugh || idx == FaceCells.EyePain;
            if (!closedType)
            {
                BlitTemplate(c);
                if (Female || LashesHeavy)
                {
                    // a little extra flair on the outer lashes
                    for (int k = 0; k < 2; k++)
                    {
                        float x0 = OX + PL(0.8f + k * 2.2f), y0 = TopP(x0) - Lw * 0.3f;
                        var fl = Curve(new Vector2(x0, y0), new Vector2(x0 - PL(2.4f - k * 0.6f), y0 + PL(1.6f + k * 0.5f)), new Vector2(x0 - PL(3.6f - k * 0.9f), y0 + PL(0.9f + k * 1.2f)), 10);
                        Fill(c, p => Stroke(p, fl, u => Lw * 0.28f * (1f - u) + PL(0.08f)), _g.Lash, 1.3f);
                    }
                }
            }
            PaintGlbEye(c, idx);
        }
    }
}
