using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>A prop that is attached rigidly to a bone / anchor (separate MeshRenderer).</summary>
    public sealed class PropSpec
    {
        public string Name;
        public string Bone;          // bone or anchor name ("HandAnchorL", "Chest", ...)
        public Part Part;            // meshed in rest root space
    }

    /// <summary>Builds a procedural anime character (body + garments + hair + accessories) from a CastDef's LookSpec.</summary>
    public sealed partial class ProcBuilder
    {
        readonly CastDef def;
        readonly LookSpec L;
        public readonly BodyProportions P;
        public readonly CharMesher M = new CharMesher();
        public readonly List<PropSpec> Props = new List<PropSpec>();
        public FacePainter Face = new FacePainter();
        public Vector4 FaceEye = new Vector4(0.19f, 0.43f, 0.105f, 0.105f);
        public Vector4 FaceBrow = new Vector4(0.19f, 0.575f, 0.105f, 0.0525f);
        public Vector4 FaceMouth = new Vector4(0.5f, 0.17f, 0.085f, 0.0425f);
        public string IdleStyle = "";
        public string HairColorHex => L.HairColor;
        public bool Aquarium;
        public readonly List<string> Chains = new List<string>(); // dynamic chain root bone names

        float H, hh;
        Vector3 hc;                  // head center
        Sdf torsoSkin, headSkin;
        readonly Sdf[] armSkin = new Sdf[2], legSkin = new Sdf[2], handSkin = new Sdf[2];
        int mSkin, mFace;
        Color skinCol;
        bool F;

        public ProcBuilder(CastDef def)
        {
            this.def = def; L = def.Look;
            F = def.Gender == Gender.F;
            P = BodyProportions.Make(def.HeightCm / 100f, F, L.Build, L.Shoulders, L.HeadScale);
            H = P.H; hh = P.HH; hc = P.HeadCenter;
            Aquarium = L.Acc.Contains(Accessory.AquariumHead);
        }

        // ------------------------------------------------------------------ helpers
        public static Color Hex(string h, Color fallback)
        {
            if (!string.IsNullOrEmpty(h) && ColorUtility.TryParseHtmlString(h, out var c)) return c;
            return fallback;
        }
        Color Hex(string h) => Hex(h, Color.gray);
        Vector3 J(HBone b) => P.J(b);
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Sdf KeepAbove(Sdf s, float y, float k = 0f) => Sdf.Inter(s, Sdf.Plane(V(0, y, 0), Vector3.down), k);
        static Sdf KeepBelow(Sdf s, float y, float k = 0f) => Sdf.Inter(s, Sdf.Plane(V(0, y, 0), Vector3.up), k);
        static Sdf KeepSide(Sdf s, Vector3 pt, Vector3 keepDir) => Sdf.Inter(s, Sdf.Plane(pt, -keepDir));
        static float SX(int side) => side == 0 ? -1f : 1f; // 0 = L (-X), 1 = R (+X)
        HBone HB(string name) => (HBone)Array.IndexOf(ActorSkeleton.Names, name);
        int Bi(HBone b) => (int)b;
        bool Has(Garment g) => L.Wear.Any(w => w.Kind == g);
        GarmentSpec G(Garment g) => L.Wear.FirstOrDefault(w => w.Kind == g);
        bool Acc(Accessory a) => L.Acc.Contains(a);

        int Cloth(Color c, int pattern = 0, Color? pc = null, Vector4? pp = null, float gloss = 0f, string name = "Cloth", float rim = 0.3f)
        {
            var m = new MatSpec { Name = name, Color = c, Pattern = pattern, Gloss = gloss, Rim = rim };
            if (pc.HasValue) m.PatternColor = pc.Value;
            if (pp.HasValue) m.PatternParams = pp.Value;
            float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            // darker cloth gets a lighter, more lavender shade so it doesn't crush to black
            m.Shade = lum < 0.18f ? new Color(0.72f, 0.66f, 0.82f) : new Color(0.66f, 0.58f, 0.74f);
            m.Shade2 = lum < 0.18f ? new Color(0.52f, 0.46f, 0.64f) : new Color(0.46f, 0.39f, 0.55f);
            return M.Mat(m);
        }
        int Cloth(string hex, int pattern = 0, Color? pc = null, Vector4? pp = null, float gloss = 0f, string name = "Cloth") => Cloth(Hex(hex), pattern, pc, pp, gloss, name);
        int Metal(Color c, string name = "Metal") => M.Mat(new MatSpec { Name = name, Color = c, Gloss = 1.1f, GlossThreshold = 0.9f, Rim = 0.55f, Shade = new Color(0.55f, 0.48f, 0.5f), Shade2 = new Color(0.35f, 0.3f, 0.34f) });
        int Glossy(Color c, string name = "Glossy") => M.Mat(new MatSpec { Name = name, Color = c, Gloss = 0.8f, GlossThreshold = 0.94f, Rim = 0.4f, Shade = new Color(0.6f, 0.55f, 0.68f), Shade2 = new Color(0.42f, 0.38f, 0.5f) });

        static int PatternId(string p)
        {
            switch ((p ?? "").ToLowerInvariant())
            {
                case "pinstripe": return 1;
                case "lace": return 2;
                case "brocade": case "damask": return 3;
                case "oilstain": return 4;
                case "faded": return 5;
                case "ripped": return 6;
                default: return 0;
            }
        }

        int GarMat(GarmentSpec g, int defPattern = 0)
        {
            if (g == null) return Cloth(Color.gray);
            Color c = Hex(g.Color);
            int pat = PatternId(g.Pattern);
            if (pat == 0) pat = defPattern;
            Color pc = Color.black; Vector4 pp = new Vector4(0.02f, 0.1f, 0, 0);
            switch (pat)
            {
                case 1: pc = Color.Lerp(c, Color.white, 0.45f); pp = new Vector4(0.014f, 0.08f, 0, 0); break;
                case 2: pc = Color.Lerp(c, new Color(0.55f, 0.52f, 0.6f), 0.45f); pp = new Vector4(0.022f, 0.1f, 0, 0); break;
                case 3: pc = string.IsNullOrEmpty(g.Color2) ? Color.Lerp(c, Color.white, 0.18f) : Hex(g.Color2); pp = new Vector4(0.05f, 0.1f, 0, 0); if (c.grayscale > 0.5f) { pc = Color.Lerp(c, new Color(0.6f, 0.5f, 0.32f), 0.72f); c = new Color(c.r * 0.9f, c.g * 0.88f, c.b * 0.84f, 1f); } break;
                case 4: pc = new Color(0.16f, 0.12f, 0.08f); pp = new Vector4(0.09f, 0.1f, 0, 0); break;
                case 5: pp = new Vector4(0.09f, 0.1f, 0, 0); break;
                case 6: pc = skinCol; pp = new Vector4(0.02f, 0.1f, J(HBone.LowerLegL).y + 0.02f, 0); break;
                case 7: pc = string.IsNullOrEmpty(g.Color2) ? Color.white : Hex(g.Color2); pp = new Vector4(0.035f, 0.1f, 0, 0); break;
                case 8: pp = new Vector4(0.012f, 0.1f, 0, 0); break;
                case 9: pp = new Vector4(0.05f, 0.1f, 0, 0); break;
                case 10: pp = new Vector4(0.006f, 0.1f, 0, 0); break;
            }
            return Cloth(c, pat, pc, pp);
        }

        // ------------------------------------------------------------------ build
        public CharMesher Build()
        {
            skinCol = Hex(L.Skin, new Color(0.95f, 0.84f, 0.77f));
            SetupBones();
            SetupFace();
            mSkin = M.Mat(new MatSpec { Name = "Skin", Color = skinCol, Shade = new Color(0.94f, 0.74f, 0.76f), Shade2 = new Color(0.8f, 0.58f, 0.64f), Rim = 0.25f, Cavity = 0.55f });
            BuildBodySkin();
            PlaceFingerBones();
            if (!Aquarium) BuildHead();
            BuildGarments();
            BuildHair();
            BuildAccessories();
            return M;
        }

        void SetupBones()
        {
            for (int i = 0; i < ActorSkeleton.Count; i++)
            {
                var b = new BoneDef { Name = ActorSkeleton.Names[i], Parent = ActorSkeleton.Parent[i], Pos = P.Joint[i] };
                M.Bones.Add(b);
            }
            Tail(HBone.Hips, J(HBone.Spine)); Tail(HBone.Spine, J(HBone.Chest)); Tail(HBone.Chest, J(HBone.Neck));
            Tail(HBone.Neck, J(HBone.Head)); Tail(HBone.Head, P.HeadTop);
            for (int s = 0; s < 2; s++)
            {
                HBone sh = s == 0 ? HBone.ShoulderL : HBone.ShoulderR;
                Tail(sh, J(sh + 1)); Tail(sh + 1, J(sh + 2)); Tail(sh + 2, J(sh + 3)); Tail(sh + 3, s == 0 ? P.HandTipL : P.HandTipR);
                HBone ul = s == 0 ? HBone.UpperLegL : HBone.UpperLegR;
                Tail(ul, J(ul + 1)); Tail(ul + 1, J(ul + 2)); Tail(ul + 2, J(ul + 3)); Tail(ul + 3, s == 0 ? P.ToeTipL : P.ToeTipR);
            }
            CharacterBaker.ExtendedTails(M.Bones, P);
            // torso bones: shorten so the blend happens around the joints
            M.Bones[(int)HBone.Hips].Pos = J(HBone.Hips) + V(0, -0.06f, 0);
        }

        void Tail(HBone b, Vector3 t) => M.Bones[(int)b].Tail = t;

        public int AddChainBone(string name, string parent, Vector3 pos, Vector3 tail)
        {
            int pi = M.BoneIndex(parent);
            M.Bones.Add(new BoneDef { Name = name, Parent = pi, Pos = pos, Tail = tail });
            return M.Bones.Count - 1;
        }

        /// <summary>Adds a chain of bones following the given points; returns bone indices (one per segment).</summary>
        public int[] AddChain(string prefix, string parent, IList<Vector3> pts)
        {
            var ids = new int[pts.Count - 1];
            string par = parent;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                string n = prefix + "_" + i;
                ids[i] = AddChainBone(n, par, pts[i], pts[i + 1]);
                par = n;
            }
            Chains.Add(prefix + "_0");
            return ids;
        }

        /// <summary>Eye template of the owner's scans the procedural face borrows its eyes from (by eye shape).</summary>
        public static string EyeTemplateFor(LookSpec look, bool female) => look.EyeSharp < 0.4f ? "P02" : look.EyeSharp > 0.68f ? "P04" : "P01";
        public static bool UseScanEyes = true;

        // face layout (fractions of the head height above the chin), matched to the scanned heads
        const float EyeK = 0.355f, NoseK = 0.215f, MouthK = 0.13f;

        void SetupFace()
        {
            M.HeadF = new HeadFrame
            {
                Center = hc,
                Scale = 0.9f * hh,
                EllipsoidC = hc + V(0, -0.03f * hh, -0.07f * hh),
                EllipsoidR = V(0.38f * hh, 0.55f * hh, 0.48f * hh),
                FaceZ = hc.z + 0.16f * hh
            };
            Face.Iris = Hex(L.EyeColor, new Color(0.25f, 0.2f, 0.2f));
            Face.Hair = Hex(L.HairColor, new Color(0.1f, 0.08f, 0.08f));
            Face.Skin = Hex(L.Skin, new Color(0.95f, 0.84f, 0.77f));
            Face.Sharp = L.EyeSharp;
            Face.Female = F;
            Face.GoldTooth = Acc(Accessory.GoldTooth);
            float chin = hc.y - 0.5f * hh;
            float Uv(float y) => 0.5f + (y - hc.y) / (0.9f * hh);
            float eyeV = Uv(P.EyeCenter.y);
            Face.NosePos = Uv(chin + NoseK * hh);
            FaceEye = new Vector4(F ? 0.2f : 0.206f, eyeV, F ? 0.142f : 0.13f, F ? 0.142f : 0.13f);
            FaceBrow = new Vector4(F ? 0.198f : 0.204f, eyeV + (F ? 0.148f : 0.14f), 0.12f, 0.06f);
            FaceMouth = new Vector4(0.5f, Uv(chin + MouthK * hh), 0.19f, 0.095f);
            if (UseScanEyes)
            {
                // (charpolish-cc) never pass a null log: GlbEyeTemplate.Build -> GlbRigger.LoadScan writes to it
                var tplLog = new System.Text.StringBuilder();
                var tpl = GlbEyeTemplate.Get(EyeTemplateFor(L, F), tplLog);
                M.Log += tplLog.ToString();
                if (tpl != null)
                {
                    Face.ScanEyes = tpl;
                    Face.ScanEyePx = GlbEyeTemplate.Recolour(tpl, Face.Iris, Face.Hair);
                    float k = hh / 0.25f * (F ? 1.03f : 1f);
                    float U(float m) => m * k / (0.9f * hh);
                    var c = tpl.C;
                    FaceEye = new Vector4(U(c.EyeDX), eyeV, U(tpl.HalfM), U(tpl.HalfM));
                    FaceBrow = new Vector4(U(c.EyeDX + 0.001f), eyeV + U((c.EyeTop.y + 7.5f) * 0.001f), U(0.021f), U(0.0105f));
                    FaceMouth = new Vector4(0.5f, Uv(chin + MouthK * hh), U(0.045f), U(0.0225f));
                }
            }
            Face.EyeV = FaceEye.y; Face.EyeDx = FaceEye.x; Face.MouthV = FaceMouth.y;
            if (F) Face.CuteBlush = 0.35f;
            if (L.EyeSharp < 0.25f) Face.CuteBlush = Mathf.Max(Face.CuteBlush, 0.55f);
        }

        // ------------------------------------------------------------------ body
        void BuildBodySkin()
        {
            float sw = P.ShoulderHalfW;
            float shY = J(HBone.UpperArmL).y;
            float waistY = J(HBone.Spine).y;
            float hipsY = J(HBone.Hips).y;
            float legTop = J(HBone.UpperLegL).y;
            var t = new List<Sdf>();
            // neck
            float neckTop = Aquarium ? shY + 0.06f : J(HBone.Head).y + 0.03f * hh;
            t.Add(Sdf.Cone(V(0, J(HBone.Neck).y - 0.03f, -0.012f), V(0, neckTop, -0.02f * hh), P.NeckR * 1.08f, P.NeckR * 0.97f));
            // shoulder slope (trapezius)
            for (int s = 0; s < 2; s++)
                t.Add(Sdf.Cone(V(0, shY + 0.035f, -0.022f), V(SX(s) * (sw - 0.012f), shY + 0.008f, -0.014f), P.NeckR * 1.25f, P.UpperArmR * 1.0f));
            // upper chest block + ribcage
            float chestCy = (shY + waistY) * 0.5f + 0.035f;
            float chestRy = (shY - waistY) * 0.5f + 0.02f;
            t.Add(Sdf.Box(V(0, shY - 0.055f, -0.01f), V(sw - 0.03f, 0.05f, P.ChestHalfD * 0.78f), 0.045f));
            t.Add(Sdf.Ellipsoid(V(0, chestCy, 0.004f), V(P.ChestHalfW, chestRy, P.ChestHalfD)));
            // waist, pelvis
            t.Add(Sdf.Ellipsoid(V(0, waistY, 0), V(P.WaistHalfW, 0.085f, P.WaistHalfD)));
            t.Add(Sdf.Ellipsoid(V(0, hipsY - 0.015f, -0.006f), V(P.HipHalfW, 0.085f, P.HipHalfD)));
            t.Add(Sdf.Ellipsoid(V(0, legTop - 0.015f, 0.0f), V(P.HipHalfW * 0.72f, 0.055f, P.HipHalfD * 0.8f)));
            // glutes
            for (int s = 0; s < 2; s++)
                t.Add(Sdf.Ellipsoid(V(SX(s) * P.HipHalfW * 0.42f, hipsY - 0.045f, -P.HipHalfD * 0.3f), V(P.HipHalfW * 0.52f, 0.085f, P.HipHalfD * 0.72f)));
            if (F)
                for (int s = 0; s < 2; s++)
                    t.Add(Sdf.Ellipsoid(V(SX(s) * P.ChestHalfW * 0.42f, chestCy - 0.005f, P.ChestHalfD * 0.5f), V(P.BustR, P.BustR * 0.92f, P.BustR * 0.82f)));
            else
                t.Add(Sdf.Ellipsoid(V(0, chestCy + 0.02f, P.ChestHalfD * 0.25f), V(P.ChestHalfW * 0.92f, chestRy * 0.62f, P.ChestHalfD * 0.8f)));
            torsoSkin = Sdf.Union(0.045f, t);

            for (int s = 0; s < 2; s++)
            {
                HBone ua = s == 0 ? HBone.UpperArmL : HBone.UpperArmR;
                Vector3 a = J(ua), e = J(ua + 1), w = J(ua + 2);
                var arm = new List<Sdf>
                {
                    Sdf.Sphere(a + V(0, 0.004f, 0), P.UpperArmR * 1.12f),
                    Sdf.Cone(a, e, P.UpperArmR * 1.02f, P.ElbowR),
                    Sdf.Cone(e, w, P.ElbowR * 1.02f, P.WristR),
                    Sdf.Ellipsoid(Vector3.Lerp(e, w, 0.28f) + V(0, 0, 0.004f), V(P.ElbowR * 1.08f, (w - e).magnitude * 0.3f, P.ElbowR * 1.12f)),
                };
                armSkin[s] = Sdf.Union(0.02f, arm);

                HBone ul = s == 0 ? HBone.UpperLegL : HBone.UpperLegR;
                Vector3 hj = J(ul), kn = J(ul + 1), an = J(ul + 2);
                float shin = (kn - an).magnitude;
                var leg = new List<Sdf>
                {
                    Sdf.Ellipsoid(hj + V(SX(s) * 0.01f, -0.02f, 0), V(P.ThighR * 1.12f, 0.1f, P.ThighR * 1.08f)),
                    Sdf.Cone(hj + V(0, 0.02f, 0), kn, P.ThighR, P.KneeR * 1.05f),
                    Sdf.Cone(kn, an, P.KneeR, P.AnkleR),
                    Sdf.Ellipsoid(kn + V(0, -shin * 0.3f, -0.012f), V(P.CalfR * 1.03f, shin * 0.3f, P.CalfR * 1.08f)),
                    Sdf.Ellipsoid(an + V(0, -0.02f, 0.025f), V(P.FootHalfW * 0.9f, 0.03f, P.FootLen * 0.4f)),
                };
                legSkin[s] = Sdf.Union(0.025f, leg);
                handSkin[s] = HandSdf(s);
            }
        }

        /// <summary>Relaxed anime hand with 4 fingers and a thumb, built in hand space then placed at the wrist.</summary>
        Sdf HandSdf(int side)
        {
            float s = P.HandLen / 0.18f; // scale vs. an 18cm hand
            float pn = -SX(side);        // palm normal x sign (palms face the body)
            HBone hand = side == 0 ? HBone.HandL : HBone.HandR;
            Vector3 wrist = J(hand);
            Vector3 tip = side == 0 ? P.HandTipL : P.HandTipR;
            Quaternion Q = Quaternion.FromToRotation(Vector3.down, (tip - wrist).normalized);
            // canonical hand: fingers -Y, palm normal +X*pn, thumb +Z
            Func<float, float, float, Vector3> Pt = (along, inw, fw) => wrist + Q * new Vector3(inw * pn * s, -along * s, fw * s);
            var parts = new List<Sdf>();
            parts.Add(Sdf.Box(Pt(0.047f, 0f, 0f), new Vector3(0.0118f * s, 0.04f * s, 0.037f * s), Q, 0.0115f * s));
            parts.Add(Sdf.Ellipsoid(Pt(0.014f, 0f, 0f), new Vector3(0.022f, 0.028f, 0.029f) * s, Q));
            float[] zs = { 0.026f, 0.0085f, -0.0085f, -0.025f };
            float[] len = { 0.074f, 0.081f, 0.076f, 0.061f };
            float[] rad = { 0.0079f, 0.0082f, 0.0077f, 0.0069f };
            float[] seg = { 0.43f, 0.32f, 0.25f };
            for (int f = 0; f < 4; f++)
            {
                float curl = 16f + f * 5f;
                Vector3 pc = new Vector3(0, -0.084f, zs[f]);
                Vector3 dirC = new Vector3(0, -1f, zs[f] * 1.4f).normalized;
                var pts = new List<Vector3> { wrist + Q * new Vector3(pc.x * pn * s, pc.y * s, pc.z * s) };
                var rs = new List<float> { rad[f] * s * 1.05f };
                for (int k = 0; k < 3; k++)
                {
                    dirC = Quaternion.AngleAxis((curl + k * 9f) * pn, Vector3.forward) * dirC;
                    Vector3 step = new Vector3(dirC.x, dirC.y, dirC.z) * len[f] * seg[k] * s;
                    pts.Add(pts[pts.Count - 1] + Q * step);
                    rs.Add(rad[f] * s * (1f - 0.11f * (k + 1)));
                }
                parts.Add(Sdf.Tube(pts, rs, 0.0025f * s));
                RecordFinger(side, 1 + f, pts, rad[f] * s);
            }
            {
                Vector3 t0 = Pt(0.024f, 0.012f, 0.024f);
                Vector3 d1 = Q * new Vector3(0.35f * pn, -0.55f, 0.6f).normalized;
                Vector3 d2 = Q * new Vector3(0.45f * pn, -0.8f, 0.3f).normalized;
                Vector3 d3 = Q * new Vector3(0.6f * pn, -0.75f, 0.1f).normalized;
                Vector3 t1 = t0 + d1 * 0.034f * s, t2 = t1 + d2 * 0.029f * s, t3 = t2 + d3 * 0.021f * s;
                parts.Add(Sdf.Tube(new[] { t0, t1, t2, t3 }, new[] { 0.0125f * s, 0.0095f * s, 0.0085f * s, 0.0072f * s }, 0.004f * s));
                RecordFinger(side, 0, new[] { t0, t1, t2, t3 }, 0.009f * s);
            }
            return Sdf.Union(0.0055f * s, parts);
        }

        static Sdf OrientedBox(Vector3 c, Vector3 yAxis, Vector3 xAxis, Vector3 zAxis, Vector3 halfYXZ, float round)
        {
            // local box axes: X = xAxis, Y = yAxis, Z = zAxis; half extents given as (alongY, alongX, alongZ)
            var rot = Quaternion.LookRotation(zAxis, yAxis);
            return Sdf.Box(c, V(halfYXZ.y, halfYXZ.x, halfYXZ.z), rot, round);
        }

        void BuildHead()
        {
            float s = hh;
            float jw = F ? 0.9f : 0.97f;
            var h = new List<Sdf>
            {
                // cranium
                Sdf.Ellipsoid(hc + V(0, 0.075f * s, -0.04f * s), V(0.385f * s, 0.425f * s, 0.445f * s)),
                // cheeks
                Sdf.Ellipsoid(hc + V(0, -0.085f * s, 0.065f * s), V(0.325f * s * jw, 0.28f * s, 0.315f * s)),
                // V jaw: two jaw lines converging on a slim chin
                Sdf.Cone(hc + V(-0.2f * s * jw, -0.1f * s, 0.03f * s), hc + V(-0.045f * s, -0.415f * s, 0.15f * s), 0.11f * s, 0.05f * s),
                Sdf.Cone(hc + V(0.2f * s * jw, -0.1f * s, 0.03f * s), hc + V(0.045f * s, -0.415f * s, 0.15f * s), 0.11f * s, 0.05f * s),
                Sdf.Cone(hc + V(0, -0.12f * s, 0.08f * s), hc + V(0, -0.4f * s, 0.168f * s), 0.22f * s * jw, 0.055f * s),
                Sdf.Sphere(hc + V(0, -0.415f * s, 0.158f * s), (F ? 0.046f : 0.056f) * s),
            };
            Sdf head = Sdf.Union(0.07f * s, h);
            // flatten the face front slightly
            head = Sdf.Inter(head, Sdf.Plane(hc + V(0, 0, 0.395f * s), V(0, -0.06f, 1f)), 0.1f * s);
            // nose: a soft bridge and a small tip (anime-semi-real, like the scans)
            var nose = Sdf.Union(0.02f * s,
                Sdf.Ellipsoid(hc + V(0, -0.19f * s, 0.372f * s), V(0.017f * s, 0.075f * s, 0.022f * s), Quaternion.Euler(-16f, 0, 0)),
                Sdf.Sphere(hc + V(0, -0.272f * s, 0.388f * s), 0.024f * s));
            head = Sdf.Union(0.028f * s, head, nose);
            // ears
            var ears = new List<Sdf>();
            for (int i = 0; i < 2; i++)
                ears.Add(Sdf.Ellipsoid(hc + V(SX(i) * 0.365f * s, -0.06f * s, -0.02f * s), V(0.045f * s, 0.1f * s, 0.066f * s), Quaternion.Euler(0, SX(i) * 18f, SX(i) * -8f)));
            head = Sdf.Union(0.02f * s, head, Sdf.Union(0f, ears));
            headSkin = head;
            mFace = M.Mat(new MatSpec
            {
                Name = "Face", Color = skinCol, Face = true, Shade = new Color(0.97f, 0.82f, 0.82f), Shade2 = new Color(0.88f, 0.68f, 0.72f), Rim = 0.2f, Cavity = 0.15f
            });
            var part = new Part { Name = "Head", Res = 0.0022f, Head = true, Skin = SkinRule.RigidTo(Bi(HBone.Head)), AOScale = 0.6f, Keep = 0.16f, Tolerance = 0.0004f };
            part.Add(head, mFace);
            M.Parts.Add(part);
        }
    }
}
