using System.Collections.Generic;
using System.IO;
using System.Linq;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEditor;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Eye templates cut from the owner's scanned heads (P01 / P02 / P04): the painted eye of the scan is rasterised
    /// straight from the front at high resolution, separated from the surrounding skin (alpha), and stored per scan.
    /// The procedural cast paints its eye cells from these templates (iris recoloured, lids / blinks painted over with the
    /// character's own skin), so every face in the game shares the scans' anime-semi-real eye style.
    /// </summary>
    public static class GlbEyeTemplate
    {
        public const int Res = 512;

        public sealed class Tpl
        {
            public string Id;
            public Color[] Px;            // Res x Res, straight alpha (cell space: x toward the nose, y up)
            public float HalfM;           // cell half size (m) of the scan
            public GlbFace.Calib C;
            public Color Lash, Skin;
            public float IrisR;           // iris radius (mm)
        }

        static readonly Dictionary<string, Tpl> Cache = new Dictionary<string, Tpl>();

        static string Dir(string id) => CharacterBaker.BakedRoot + "/" + id;

        public static Tpl Get(string id, System.Text.StringBuilder log)
        {
            if (Cache.TryGetValue(id, out var t)) return t;
            string png = Path.GetFullPath(Dir(id) + "/" + id + "_EyeTpl.png"), meta = Path.GetFullPath(Dir(id) + "/" + id + "_EyeTpl.txt");
            var c = GlbFace.Get(id);
            if (File.Exists(png) && File.Exists(meta))
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(png));
                var parts = File.ReadAllText(meta).Split(';');
                t = new Tpl { Id = id, Px = tex.GetPixels(), C = c, HalfM = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) };
                t.Lash = Parse(parts[1]); t.Skin = Parse(parts[2]); t.IrisR = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
                Object.DestroyImmediate(tex);
                Cache[id] = t;
                return t;
            }
            t = Build(id, log);
            if (t != null)
            {
                CharacterBaker.EnsureFolder(Dir(id));
                var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false);
                tex.SetPixels(t.Px); tex.Apply();
                File.WriteAllBytes(png, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                File.WriteAllText(meta, t.HalfM.ToString(ci) + ";" + Fmt(t.Lash) + ";" + Fmt(t.Skin) + ";" + t.IrisR.ToString(ci));
                Cache[id] = t;
            }
            return t;
        }

        static string Fmt(Color c) => string.Join(",", new[] { c.r, c.g, c.b }.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        static Color Parse(string s) { var v = s.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray(); return new Color(v[0], v[1], v[2], 1f); }

        static Tpl Build(string id, System.Text.StringBuilder log)
        {
            var def = Cast.Get(id);
            var scan = GlbRigger.LoadScan(def, log);
            if (scan == null) return null;
            var gm = scan.Glb.Materials[0];
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(scan.Glb.Images[gm.BaseColorImage]);
            var f = GlbFace.MakeFrame(scan.LM, id);
            var L = GlbFace.MakeLayout(scan.LM, id, f);
            // full-face photo for the skin reference / lash colour
            var img = GlbFaceMask.Rasterize(scan.V, scan.UV, scan.T, scan.TM.ToArray(), 0, tex, f, scan.LM.ChinY - 0.05f);
            GlbFaceMask.Classify(img, L);
            var art = GlbFaceMask.Analyze(img, L);
            // high-resolution eye photo (image-left eye = character's right eye; x toward the nose)
            var ef = new GlbFace.Frame
            {
                Center = new Vector3(f.Center.x - (L.EyeL.x - 0.5f) * f.Scale, f.Center.y + (L.EyeL.y - 0.5f) * f.Scale, f.Center.z),
                Scale = 2f * L.EyeHalfM, FaceZ = f.FaceZ
            };
            var eimg = GlbFaceMask.Rasterize(scan.V, scan.UV, scan.T, scan.TM.ToArray(), 0, tex, ef, scan.LM.ChinY, Res);
            Object.DestroyImmediate(tex);
            var t = new Tpl { Id = id, HalfM = L.EyeHalfM, C = c0(id), Lash = art.Lash, Skin = img.SkinRef };
            float eh = L.EyeHalfM * 1000f;
            t.IrisR = 0.44f * (t.C.EyeTop.y - t.C.EyeBot.y);
            var skinLab = GlbFaceMask.Lab(img.SkinRef);
            var px = new Color[Res * Res];
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    int k = y * Res + x;
                    if (float.IsNaN(eimg.Z[k])) { px[k] = Color.clear; continue; }
                    Color col = eimg.Col[k];
                    float mx = ((x + 0.5f) / Res - 0.5f) * 2f * eh, my = ((y + 0.5f) / Res - 0.5f) * 2f * eh;
                    GlbFaceMask.EyeTopBot(t.C, Mathf.Clamp(mx, t.C.EyeOut.x, t.C.EyeIn.x), out float top, out float bot);
                    bool inX = mx >= t.C.EyeOut.x - 0.3f && mx <= t.C.EyeIn.x + 0.3f;
                    float a = 0f;
                    if (inX && my <= top + 0.3f && my >= bot - 0.3f) a = 1f;                 // the eye opening itself
                    else
                    {
                        // lashes (upper + outer flick) and the lower lash line: whatever is not skin coloured
                        bool upper = mx >= t.C.EyeOut.x - 5.5f && mx <= t.C.EyeIn.x + 1.5f && my > top && my < top + t.C.LashW + 2.2f + (mx < t.C.EyeOut.x + 3f ? 2f : 0f);
                        bool lower = inX && my < bot && my > bot - 1.6f;
                        bool crease = inX && my > top && my < top + t.C.LashW + 5f;
                        if (upper || lower || crease)
                        {
                            float de = Dist(GlbFaceMask.Lab(col), skinLab);
                            a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(9f, 24f, de));
                            if (crease && !upper) a *= 0.6f;
                        }
                    }
                    col.a = a;
                    px[k] = col;
                }
            // soften the alpha edge by one texel
            var outp = (Color[])px.Clone();
            for (int y = 1; y < Res - 1; y++)
                for (int x = 1; x < Res - 1; x++)
                {
                    float s = 0f; for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) s += px[(y + dy) * Res + x + dx].a;
                    outp[y * Res + x].a = Mathf.Min(px[y * Res + x].a, s / 9f * 1.3f);
                }
            // bleed colour into transparent texels (mip / bilinear friendliness)
            for (int it = 0; it < 4; it++)
            {
                var src = (Color[])outp.Clone();
                for (int y = 1; y < Res - 1; y++)
                    for (int x = 1; x < Res - 1; x++)
                    {
                        int k = y * Res + x; if (src[k].a > 0.02f) continue;
                        Color acc = Color.clear; float n = 0;
                        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { var q = src[(y + dy) * Res + x + dx]; if (q.a > 0.02f) { acc += q; n++; } }
                        if (n > 0) { var cc = acc / n; cc.a = 0f; outp[k] = cc; }
                    }
            }
            t.Px = outp;
            log?.AppendLine($"[{id}] eye template: half {L.EyeHalfM * 1000f:F1} mm, iris r {t.IrisR:F1} mm, lash {t.Lash}, skin {t.Skin}");
            return t;
        }

        static GlbFace.Calib c0(string id) => GlbFace.Get(id);
        static float Dist(Vector3 a, Vector3 b) { var d = a - b; return Mathf.Sqrt(d.x * d.x * 0.35f + d.y * d.y + d.z * d.z); }

        /// <summary>The template recoloured for a character: iris hue from its eye colour, lashes tinted toward its hair.</summary>
        public static Color[] Recolour(Tpl t, Color iris, Color hair)
        {
            var px = (Color[])t.Px.Clone();
            float eh = t.HalfM * 1000f;
            float irisLum = Mathf.Max(0.05f, iris.r * 0.3f + iris.g * 0.59f + iris.b * 0.11f);
            Color lash = Color.Lerp(new Color(0.08f, 0.06f, 0.07f), hair * 0.55f, 0.35f);
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    int k = y * Res + x;
                    var c = px[k]; if (c.a <= 0.001f) continue;
                    float mx = ((x + 0.5f) / Res - 0.5f) * 2f * eh, my = ((y + 0.5f) / Res - 0.5f) * 2f * eh;
                    float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    float r = Mathf.Sqrt(mx * mx + my * my * 0.9f);
                    GlbFaceMask.EyeTopBot(t.C, Mathf.Clamp(mx, t.C.EyeOut.x, t.C.EyeIn.x), out float top, out float bot);
                    bool inside = mx >= t.C.EyeOut.x && mx <= t.C.EyeIn.x && my <= top && my >= bot;
                    if (inside && r < t.IrisR + 0.6f)
                    {
                        // iris: keep the painted light / dark structure, take the character's hue
                        float mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                        float highlight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.9f, mn));
                        float edge = Mathf.Clamp01((t.IrisR + 0.6f - r) / 1.2f);
                        Color tgt = iris * Mathf.Clamp(lum / irisLum, 0.15f, 2.2f);
                        tgt = Color.Lerp(tgt, Color.Lerp(iris, Color.white, 0.5f), Mathf.Clamp01((lum - irisLum * 1.6f) * 1.5f));
                        tgt.a = c.a;
                        px[k] = Color.Lerp(c, tgt, 0.9f * edge * (1f - highlight));
                    }
                    else if (!inside && lum < 0.35f)
                    {
                        // lash line: tint toward the character's hair colour
                        var l2 = lash * Mathf.Clamp(lum / 0.12f, 0.5f, 1.6f); l2.a = c.a;
                        px[k] = Color.Lerp(c, l2, 0.6f);
                    }
                }
            return px;
        }
    }
}
