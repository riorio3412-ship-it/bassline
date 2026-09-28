using System;
using System.Collections.Generic;
using BL23.Game.Characters;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Overlay art for the scanned GLB faces: eyelids painted with the scan's own lid / cheek colours that slide over
    /// the painted eyes (blink, half-lid, closed, happy, angry / sad slants...), skin-inpainted mouth covers with small
    /// anime mouth shapes in the scan's lip colour, and cheek FX placed from the measured layout.
    /// Nothing here is drawn outside the scanned eye / mouth / cheek areas, and the shader masks it all by the skin mask.
    /// </summary>
    public sealed partial class FacePainter
    {
        GlbFaceMask.GlbArt _g;

        // eye cell: p = 0.5 + mm / (2 * EhMM)   (x toward the nose, y up)
        float P(float mm) => 0.5f + mm / (2f * _g.EhMM);
        float PL(float mm) => mm / (2f * _g.EhMM);
        float XMM(float p) => (p - 0.5f) * 2f * _g.EhMM;
        float TopP(float px) { GlbFaceMask.EyeTopBot(_g.L.C, XMM(px), out float t, out _); return P(t); }
        float BotP(float px) { GlbFaceMask.EyeTopBot(_g.L.C, XMM(px), out _, out float b); return P(b); }
        float OX => P(_g.L.C.EyeOut.x);
        float IX => P(_g.L.C.EyeIn.x);
        float Lw => PL(_g.L.C.LashW);
        Color LidAt(Vector2 p)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt(p.x * GlbFaceMask.GlbArt.EyeRes), 0, GlbFaceMask.GlbArt.EyeRes - 1);
            float top = TopP(Mathf.Clamp(p.x, OX, IX)), bot = BotP(Mathf.Clamp(p.x, OX, IX));
            float t = Mathf.Clamp01((p.y - bot) / Mathf.Max(1e-4f, top - bot));
            var c = Color.Lerp(_g.LidBot[i], _g.LidTop[i], Mathf.SmoothStep(0f, 1f, t));
            c.a = 1f;
            return c;
        }

        public Texture2D PaintGlbAtlas(GlbFaceMask.GlbArt art)
        {
            _g = art;
            LipOverride = Color.Lerp(art.Lip, new Color(0.25f, 0.1f, 0.1f), 0.25f);
            var cv = new Canvas(1024, 2048);
            for (int i = 0; i < 16; i++)
            {
                var cell = new Cell { C = cv, X0 = (i % 4) * 256, Y0 = 2048 - (i / 4 + 1) * 256, W = 256, H = 256 };
                PaintGlbEye(cell, i);
            }
            for (int i = 0; i < 16; i++)
            {
                var cell = new Cell { C = cv, X0 = (i % 4) * 256, Y0 = 1024 - (i / 4 + 1) * 128, W = 256, H = 128 };
                if (i == FaceCells.MouthNeutral) continue;
                PaintGlbMouthCover(cell, i);
                PaintMouth(cell, i);
            }
            return ToTex(cv, "FaceAtlasGlb");
        }

        // ------------------------------------------------------------------ eyes
        /// <summary>Upper lid sliding down: drop 0 = scanned eye untouched .. 1 = lid edge on the lower lid. slant: + = inner corner lower.</summary>
        /// <summary>Partial lids on narrow scanned eyes cover less (P04: the iris must stay visible when narrowed / angry).</summary>
        float LidK => Mathf.Clamp((_g.L.C.EyeTop.y - _g.L.C.EyeBot.y - 6f) / 16f, 0.4f, 1f);

        void Lid(Cell c, float drop, float slant = 0f, float lowerRaise = 0f, bool lashes = true)
        {
            if (drop < 0.99f) { drop *= LidK; slant *= LidK; lowerRaise *= LidK; }
            float ox = OX, ix = IX, lw = Lw, m = PL(0.55f);
            bool closed = drop >= 0.99f;
            float mb = closed ? PL(1.3f) : 0f, mc = closed ? PL(1.6f) : m;
            Func<float, float> dd = x => Mathf.Clamp01(drop + slant * (Mathf.InverseLerp(ox, ix, x) - 0.5f));
            Func<float, float> edge = x => Mathf.Lerp(TopP(x) - lw, BotP(x), dd(x)) - mb;
            // lid skin: from the lid edge up to just above the scanned upper lash line (closed: past the lower lash line too)
            Sd lid = p =>
            {
                float x = Mathf.Clamp(p.x, ox, ix);
                float e = edge(x);
                float d = Mathf.Max(e - p.y, p.y - (TopP(x) + m));
                d = Mathf.Max(d, Mathf.Max(ox - mc - p.x, p.x - (ix + mc)));
                return d;
            };
            Fill(c, lid, p =>
            {
                var col = LidAt(p);
                // soft fold shadow just above the lid edge
                float e = edge(Mathf.Clamp(p.x, ox, ix));
                float sh = Mathf.Clamp01(1f - (p.y - e) / (lw * 2.5f));
                return Color.Lerp(col, col * new Color(0.86f, 0.8f, 0.84f, 1f), sh * 0.5f * Mathf.Clamp01(drop * 3f));
            }, 1.4f);
            if (lowerRaise > 0f)
            {
                Sd low = p =>
                {
                    float x = Mathf.Clamp(p.x, ox, ix);
                    float b = BotP(x), t = TopP(x);
                    float up = Mathf.Lerp(b, t, lowerRaise * Mathf.Sin(Mathf.InverseLerp(ox, ix, x) * Mathf.PI));
                    return Mathf.Max(Mathf.Max(p.y - up, (b - m) - p.y), Mathf.Max(ox - p.x, p.x - ix));
                };
                Fill(c, low, p => LidAt(p), 1.4f);
                var lowLine = new List<Vector2>();
                for (int k = 0; k <= 24; k++)
                {
                    float x = Mathf.Lerp(ox + (ix - ox) * 0.12f, ix - (ix - ox) * 0.1f, k / 24f);
                    lowLine.Add(new Vector2(x, Mathf.Lerp(BotP(x), TopP(x), lowerRaise * Mathf.Sin(Mathf.InverseLerp(ox, ix, x) * Mathf.PI))));
                }
                Fill(c, p => Stroke(p, lowLine, u => lw * 0.18f * Mathf.Sin(u * Mathf.PI)), new Color(_g.Lash.r, _g.Lash.g, _g.Lash.b, 0.75f));
            }
            if (!lashes || drop <= 0.001f && slant == 0f) return;
            // the scanned lash line, moved to the new lid edge
            var line = new List<Vector2>();
            for (int k = 0; k <= 32; k++)
            {
                float x = Mathf.Lerp(ox, ix, k / 32f);
                line.Add(new Vector2(x, edge(x) + lw * 0.5f));
            }
            Fill(c, p => Stroke(p, line, u => lw * 0.5f * (0.45f + 0.75f * Mathf.Sin(Mathf.Clamp01(u * 1.1f) * Mathf.PI)) * (1.1f - 0.3f * u)), _g.Lash, 1.3f);
            var flick = Curve(line[0], line[0] + new Vector2(-PL(2.2f), PL(0.2f)), line[0] + new Vector2(-PL(3.2f), -PL(1.4f)), 10);
            Fill(c, p => Stroke(p, flick, u => lw * 0.35f * (1f - u) + PL(0.12f)), _g.Lash, 1.3f);
        }

        /// <summary>happy = false: relaxed closed lid following the lower lid (U), lift = fraction of the eye height.
        /// happy = true: arch (^) over the straight corner line, bow = arch height as a fraction of the eye height.</summary>
        void ClosedLine(Cell c, float lift, float bow, float thick, bool happy = false)
        {
            float ox = OX, ix = IX, lw = Lw;
            var line = new List<Vector2>();
            float hMid = TopP(0.5f) - BotP(0.5f);
            for (int k = 0; k <= 32; k++)
            {
                float t = k / 32f;
                float x = Mathf.Lerp(ox + PL(0.5f), ix - PL(0.3f), t);
                float b = BotP(x), top = TopP(x);
                float y = happy
                    ? Mathf.Lerp(BotP(ox + PL(0.5f)), BotP(ix - PL(0.3f)), t) + hMid * (lift + bow * Mathf.Sin(t * Mathf.PI))
                    : Mathf.Lerp(b, top, lift);
                line.Add(new Vector2(x, y));
            }
            Fill(c, p => Stroke(p, line, u => lw * 0.5f * thick * (0.35f + 0.85f * Mathf.Sin(u * Mathf.PI)) * (1.1f - 0.35f * u)), _g.Lash, 1.3f);
            var fl = Curve(line[0], line[0] + new Vector2(-PL(2f), -PL(0.3f)), line[0] + new Vector2(-PL(2.8f), -PL(1.8f)), 10);
            Fill(c, p => Stroke(p, fl, u => lw * 0.3f * (1f - u) + PL(0.1f)), _g.Lash, 1.3f);
        }

        Sd EyeInterior(float shrink)
        {
            float ox = OX, ix = IX, lw = Lw;
            return p =>
            {
                float x = Mathf.Clamp(p.x, ox, ix);
                float d = Mathf.Max(BotP(x) + shrink - p.y, p.y - (TopP(x) - lw - shrink));
                return Mathf.Max(d, Mathf.Max(ox + shrink - p.x, p.x - (ix - shrink)));
            };
        }

        void PaintGlbEye(Cell c, int idx)
        {
            switch (idx)
            {
                case FaceCells.EyeOpen: case FaceCells.EyeWide: case FaceCells.EyeFear: break; // the scanned eye as is
                case FaceCells.EyeHalf: Lid(c, 0.55f); break;
                case FaceCells.EyeHalfLid: Lid(c, 0.34f); break;
                case FaceCells.EyeNarrow: Lid(c, 0.4f, 0f, 0.16f); break;
                case FaceCells.EyeAngry: Lid(c, 0.3f, 0.42f, 0.1f); break;
                case FaceCells.EyeSad: Lid(c, 0.28f, -0.36f); break;
                case FaceCells.EyeClosed: Lid(c, 1f, 0f, 0f, false); ClosedLine(c, 0.3f, 0f, 0.85f); break;
                case FaceCells.EyeHappy: Lid(c, 1f, 0f, 0f, false); ClosedLine(c, 0.05f, 0.5f, 0.9f, true); break;
                case FaceCells.EyeLaugh: Lid(c, 1f, 0f, 0f, false); ClosedLine(c, 0.02f, 0.58f, 1.15f, true); break;
                case FaceCells.EyePain:
                {
                    Lid(c, 1f, 0f, 0f, false);
                    float ox = OX + PL(1.5f), ix = IX - PL(2f);
                    float my = (TopP(ix) + BotP(ix)) * 0.5f, ty = Mathf.Lerp(BotP(ox), TopP(ox), 0.8f), by = Mathf.Lerp(BotP(ox), TopP(ox), 0.15f);
                    var a = Curve(new Vector2(ox, ty), new Vector2((ox + ix) * 0.5f, (ty + my) * 0.5f + PL(0.6f)), new Vector2(ix, my), 16);
                    var b = Curve(new Vector2(ix, my), new Vector2((ox + ix) * 0.5f, (by + my) * 0.5f - PL(0.4f)), new Vector2(ox, by), 16);
                    Fill(c, p => Mathf.Min(Stroke(p, a, u => Lw * 0.45f * (0.5f + 0.5f * u)), Stroke(p, b, u => Lw * 0.42f * (1f - 0.5f * u))), _g.Lash, 1.3f);
                    break;
                }
                case FaceCells.EyeDead:
                    Fill(c, EyeInterior(PL(0.3f)), new Color(0.36f, 0.33f, 0.4f, 0.62f), 2.5f);
                    Lid(c, 0.16f);
                    break;
                case FaceCells.EyeBlank:
                    Fill(c, EyeInterior(PL(0.3f)), new Color(0.72f, 0.71f, 0.78f, 0.42f), 2.5f);
                    break;
                case FaceCells.EyeTeary:
                {
                    float ox = OX, ix = IX;
                    var w = new List<Vector2>();
                    for (int k = 0; k <= 24; k++) { float x = Mathf.Lerp(ox + (ix - ox) * 0.12f, ix - (ix - ox) * 0.1f, k / 24f); w.Add(new Vector2(x, Mathf.Lerp(BotP(x), TopP(x), 0.14f))); }
                    Fill(c, p => Mathf.Max(Stroke(p, w, u => PL(0.9f) * Mathf.Sin(u * Mathf.PI)), EyeInterior(0f)(p)), new Color(0.78f, 0.93f, 1f, 0.8f), 1.5f);
                    FillC(c, p => Mathf.Max(Circle(p, new Vector2(0.5f + PL(1.8f), Mathf.Lerp(BotP(0.5f), TopP(0.5f), 0.3f)), PL(0.9f)), EyeInterior(0f)(p)), new Color(1f, 1f, 1f, 0.9f));
                    Lid(c, 0.12f, -0.2f);
                    break;
                }
                case FaceCells.EyeBreak:
                {
                    var ic = new Vector2(0.5f, 0.5f);
                    float r = (TopP(0.5f) - BotP(0.5f)) * 0.42f;
                    Sd inside = EyeInterior(0f);
                    Fill(c, p => Mathf.Max(Mathf.Abs(Circle(p, ic, r)) - PL(0.45f), inside(p)), new Color(0.78f, 0.08f, 0.22f, 0.95f), 1.3f);
                    Fill(c, p => Mathf.Max(Mathf.Abs(Circle(p, ic, r * 0.58f)) - PL(0.38f), inside(p)), new Color(0.86f, 0.8f, 1f, 0.95f), 1.3f);
                    FillC(c, p => Mathf.Max(Circle(p, ic, r * 0.22f), inside(p)), new Color(0.05f, 0f, 0.05f, 0.95f));
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ mouths
        void PaintGlbMouthCover(Cell c, int idx)
        {
            // cover the scanned line with inpainted skin (skipped for shapes that fully hide it themselves)
            float hwM = _g.L.MouthHalfM.x;
            float rx = _g.L.C.MouthHalf * 1.3f / (2f * hwM), ry = 0.0034f / (2f * hwM);
            var C = new Vector2(0.5f, 0.25f);
            Fill(c, p => Ellipse(p, C, new Vector2(rx, ry)), p =>
            {
                int i = Mathf.Clamp(Mathf.FloorToInt(p.x * 256f), 0, 255), j = Mathf.Clamp(Mathf.FloorToInt(p.y / 0.5f * 128f), 0, 127);
                var col = _g.MouthCover[j * 256 + i];
                float fall = Mathf.Clamp01(-Ellipse(p, C, new Vector2(rx, ry)) / (ry * 0.6f));
                col.a = Mathf.SmoothStep(0f, 1f, fall);
                return col;
            }, 1.2f);
        }

        // ------------------------------------------------------------------ fx (face uv over each 512 cell)
        public Texture2D PaintGlbFx(GlbFaceMask.GlbArt art)
        {
            _g = art;
            var cv = new Canvas(1024, 1024);
            var L = art.L; float mm = 0.001f / L.F.Scale;
            // base (always on): painted form the smoothed scan normals lose - a soft nose-side shadow, the under-nose
            // tick and a faint under-lip shade, in the scan's own shadowed skin tone
            {
                var cell = new Cell { C = cv, X0 = 0, Y0 = 512, W = 512, H = 512 };
                var sk = Skin;
                var shade = new Color(sk.r * 0.8f, sk.g * 0.62f, sk.b * 0.62f, 1f);
                float cx = (L.EyeL.x + L.EyeR.x) * 0.5f;
                float ny = L.Mouth.y + 0.4f * (L.EyeL.y - L.Mouth.y);        // nose tip
                float top = L.EyeL.y - 4f * mm;
                // nose side (character's left, image-right): tapering soft stroke along the ridge
                var ridge = Curve(new Vector2(cx + 2.2f * mm, top), new Vector2(cx + 3.2f * mm, (top + ny) * 0.5f), new Vector2(cx + 2.6f * mm, ny + 1.5f * mm), 12);
                Fill(cell, p => Stroke(p, ridge, u => mm * (0.5f + 1.3f * u)), p => new Color(shade.r, shade.g, shade.b, 0.3f), 3f);
                // under the nose tip
                var un = new Vector2(cx + 0.8f * mm, ny - 1.2f * mm);
                Fill(cell, p => Ellipse(p, un, new Vector2(3.6f * mm, 1.1f * mm)), p => new Color(shade.r, shade.g, shade.b, 0.42f * Mathf.Clamp01(-Ellipse(p, un, new Vector2(3.6f * mm, 1.1f * mm)) / (1.2f * mm))), 2f);
                // under the lower lip
                var ul = new Vector2(L.Mouth.x, L.Mouth.y - L.MouthHalf.y * 1.9f);
                var ur = new Vector2(L.MouthHalf.x * 0.55f, 1.6f * mm);
                Fill(cell, p => Ellipse(p, ul, ur), p => new Color(shade.r, shade.g, shade.b, 0.22f * Mathf.Clamp01(-Ellipse(p, ul, ur) / (1.5f * mm))), 2f);
            }
            // blush: a soft flush on the cheeks under the eyes
            {
                var cell = new Cell { C = cv, X0 = 512, Y0 = 512, W = 512, H = 512 };
                for (int s = 0; s < 2; s++)
                {
                    Vector2 bc = L.EyeUV(s, new Vector2(-2f, L.C.EyeBot.y - 8f));
                    var r = new Vector2(13f * mm, 5.5f * mm);
                    Fill(cell, p => Ellipse(p, bc, r), p => new Color(0.98f, 0.46f, 0.52f, 0.38f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-Ellipse(p, bc, r) / (4.5f * mm)))), 2.5f);
                }
            }
            // gloom: a cold veil over the eye area and soft under-eye shadows (only facial skin receives it)
            {
                var cell = new Cell { C = cv, X0 = 512, Y0 = 0, W = 512, H = 512 };
                float top = L.EyeL.y + L.C.EyeTop.y * mm + 8f * mm, bot = L.EyeL.y + L.C.EyeBot.y * mm - 10f * mm;
                Fill(cell, p => Mathf.Max(p.y - top - 0.05f, bot - p.y), p =>
                {
                    float t = Mathf.Clamp01((p.y - bot) / Mathf.Max(1e-3f, top - bot));
                    return new Color(0.2f, 0.17f, 0.34f, 0.42f * Mathf.SmoothStep(0f, 1f, t));
                }, 2f);
                for (int s = 0; s < 2; s++)
                {
                    Vector2 tc = L.EyeUV(s, new Vector2(-1f, L.C.EyeBot.y - 3.5f));
                    var tr = new Vector2((L.C.EyeIn.x - L.C.EyeOut.x) * 0.5f * mm, 4f * mm);
                    Fill(cell, p => Ellipse(p, tc, tr), p => new Color(0.3f, 0.24f, 0.44f, 0.4f * Mathf.Clamp01(-Ellipse(p, tc, tr) / (3f * mm))), 2f);
                }
            }
            // tears: from the inner-lower lid down the cheek
            {
                var cell = new Cell { C = cv, X0 = 0, Y0 = 0, W = 512, H = 512 };
                for (int s = 0; s < 2; s++)
                {
                    Vector2 a = L.EyeUV(s, new Vector2(3f, L.C.EyeBot.y + 1f));
                    Vector2 b = L.EyeUV(s, new Vector2(1f, L.C.EyeBot.y - 12f));
                    Vector2 d = L.EyeUV(s, new Vector2(3.5f, L.C.EyeBot.y - 24f));
                    var ln = Curve(a, b, d, 16);
                    Fill(cell, p => Stroke(p, ln, u => 1.3f * mm * (0.6f + 0.4f * Mathf.Sin(u * Mathf.PI))), new Color(0.72f, 0.9f, 1f, 0.8f));
                    Fill(cell, p => Stroke(p, ln, u => 0.45f * mm), new Color(1f, 1f, 1f, 0.9f));
                    FillC(cell, p => Circle(p, d + new Vector2(0, -2.5f * mm), 1.5f * mm), new Color(0.72f, 0.9f, 1f, 0.9f));
                }
            }
            return ToTex(cv, "FaceFxGlb");
        }
    }
}
