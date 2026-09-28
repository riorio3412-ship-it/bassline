using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    public sealed partial class ProcBuilder
    {
        Color AccC => Hex(L.AccColor, new Color(0.79f, 0.64f, 0.29f));
        static readonly Color GoldC = new Color(0.92f, 0.7f, 0.25f);
        static readonly Color SilverC = new Color(0.82f, 0.83f, 0.86f);

        Part AccPart(string name, int bone, float res = 0.0016f)
        {
            var p = new Part { Name = name, Res = res, Skin = SkinRule.RigidTo(bone), AOScale = 0.4f };
            M.Parts.Add(p);
            return p;
        }

        Part AccPartBlend(string name, float res, params HBone[] bones)
        {
            var p = new Part { Name = name, Res = res, Skin = SkinRule.Blend(4f, bones.Select(b => Bi(b)).ToArray()), AOScale = 0.4f };
            M.Parts.Add(p);
            return p;
        }

        /// <summary>World position of a face-uv point on the face surface (+ offset along +Z).</summary>
        Vector3 FacePoint(float u, float v, float off)
        {
            float x = (0.5f - u) * 0.9f * hh, y = hc.y + (v - 0.5f) * 0.9f * hh;
            float z = hc.z + 0.6f * hh;
            if (headSkin != null)
                for (int i = 0; i < 40; i++) { float d = headSkin.D(V(x, y, z)); if (Mathf.Abs(d) < 0.0003f) break; z -= d; }
            else z = hc.z + 0.4f * hh;
            return V(x, y, z + off);
        }

        Vector3 EarPos(int s) => hc + V(SX(s) * 0.395f * hh, -0.12f * hh, -0.02f * hh);

        Vector3 HandPt(int side, float along, float inw, float fw)
        {
            float s = P.HandLen / 0.18f;
            float pn = -SX(side);
            Vector3 wrist = J(side == 0 ? HBone.HandL : HBone.HandR);
            Vector3 tip = side == 0 ? P.HandTipL : P.HandTipR;
            Quaternion Q = Quaternion.FromToRotation(Vector3.down, (tip - wrist).normalized);
            return wrist + Q * new Vector3(inw * pn * s, -along * s, fw * s);
        }

        void BuildAccessories()
        {
            float neckR = P.NeckR;
            if (Acc(Accessory.AquariumHead)) BuildAquariumStand();

            if (Acc(Accessory.Tie) || Acc(Accessory.RibbonTie))
            {
                bool ribbon = Acc(Accessory.RibbonTie);
                Color tc = def.Id == "P05" ? AccC : def.Id == "P06" ? new Color(0.45f, 0.33f, 0.14f) : def.Id == "NPC00" ? new Color(0.05f, 0.05f, 0.06f) : ribbon ? new Color(0.1f, 0.12f, 0.2f) : new Color(0.2f, 0.2f, 0.25f);
                int tm = Glossy(tc, "Tie");
                var part = AccPartBlend("Tie", 0.0022f, HBone.Chest, HBone.Neck, HBone.Spine);
                float ky = neckBaseY - 0.012f;
                Vector3 knot = V(0, ky, FrontZ(ky) + 0.012f);
                if (ribbon)
                {
                    knot += V(0, 0, 0.008f);
                    part.Add(Sdf.Ellipsoid(knot, V(0.013f, 0.012f, 0.009f)), tm, 2);
                    for (int s = 0; s < 2; s++)
                    {
                        part.Add(Sdf.Ellipsoid(knot + V(SX(s) * 0.03f, 0.004f, -0.004f), V(0.03f, 0.017f, 0.007f), Quaternion.Euler(0, SX(s) * -12f, SX(s) * 12f)), tm, 1);
                        part.Add(Sdf.Cone(knot + V(SX(s) * 0.004f, -0.008f, 0.0f), knot + V(SX(s) * 0.026f, -0.085f, 0.004f), 0.008f, 0.013f), tm, 1);
                    }
                }
                else
                {
                    part.Add(Sdf.Cone(knot + V(0, 0.008f, -0.004f), knot + V(0, -0.012f, 0.002f), 0.013f, 0.008f), tm, 2);
                    float y1 = waistY - 0.03f;
                    var blade = Sdf.Func(p =>
                    {
                        float t = Mathf.Clamp01((ky - p.y) / (ky - y1));
                        float w = Mathf.Lerp(0.012f, 0.036f, Mathf.Sqrt(t));
                        float zc = FrontZ(Mathf.Clamp(p.y, y1, ky)) + 0.013f;
                        float tip = (y1 + 0.02f - p.y) + Mathf.Abs(p.x) * 0.9f;
                        return Mathf.Max(Mathf.Max(Mathf.Abs(p.x) - w, Mathf.Abs(p.z - zc) - 0.0028f), Mathf.Max(p.y - ky + 0.01f, tip));
                    }, new Bounds(V(0, (ky + y1) * 0.5f, 0.15f), V(0.12f, ky - y1 + 0.08f, 0.3f)));
                    part.Add(blade, tm, 1);
                    if (def.Id == "NPC00" || def.Id == "P05")
                    {
                        float py = ky - 0.09f;
                        part.Add(Sdf.Box(V(0, py, FrontZ(py) + 0.018f), V(0.022f, 0.003f, 0.002f), 0.001f), Metal(GoldC, "TiePin"), 3);
                    }
                }
            }

            if (Acc(Accessory.Armband))
            {
                var part = AccPartBlend("Armband", 0.0022f, HBone.UpperArmL, HBone.LowerArmL);
                var band = Sdf.Inter(Sdf.Inter(ArmShell(0, 0.024f), Sdf.Plane(ArmA(0) + ArmDir(0) * ArmLen(0) * 0.36f, ArmDir(0))), Sdf.Plane(ArmA(0) + ArmDir(0) * ArmLen(0) * 0.24f, -ArmDir(0)));
                part.Add(band, Cloth(AccC, name: "Armband"), 1);
                var stripe = Sdf.Inter(Sdf.Inter(ArmShell(0, 0.0255f), Sdf.Plane(ArmA(0) + ArmDir(0) * ArmLen(0) * 0.305f, ArmDir(0))), Sdf.Plane(ArmA(0) + ArmDir(0) * ArmLen(0) * 0.295f, -ArmDir(0)));
                part.Add(stripe, Metal(GoldC, "ArmbandStripe"), 2);
            }

            if (Acc(Accessory.HairPin))
            {
                var part = AccPart("HairPin", Bi(HBone.Head));
                Vector3 a = Surf(-48f, 18f, 0.024f), b = Surf(-40f, 30f, 0.026f);
                int pm = def.Id == "P17" ? Glossy(new Color(1f, 0.6f, 0.75f), "HairPin") : Metal(GoldC, "HairPin");
                part.Add(Sdf.Capsule(a, b, 0.0032f), pm, 1);
                if (def.Id == "P17")
                {
                    part.Add(Star(Surf(-44f, 24f, 0.03f), (Surf(-44f, 24f, 0.03f) - cc).normalized, 0.012f), Glossy(new Color(1f, 0.85f, 0.35f), "Star"), 2);
                    Vector3 c = Surf(-30f, 34f, 0.028f);
                    part.Add(Sdf.Capsule(c, c + V(0.02f, 0.012f, 0.004f), 0.003f), Glossy(new Color(0.55f, 0.9f, 0.8f), "HairPin2"), 1);
                }
            }

            if (Acc(Accessory.StarPins))
            {
                var part = AccPart("StarPins", Bi(HBone.Head));
                int sm = Glossy(new Color(1f, 0.88f, 0.4f), "Star");
                float[] az = { 52f, 62f, 44f };
                float[] el = { 30f, 18f, 44f };
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = Surf(az[i], el[i], 0.028f);
                    part.Add(Star(p, (p - cc).normalized, 0.011f - i * 0.002f), sm, 1);
                }
            }

            if (Acc(Accessory.Choker))
            {
                var part = AccPartBlend("Choker", 0.0016f, HBone.Neck, HBone.Head);
                float y = neckBaseY + 0.05f;
                part.Add(Sdf.Inter(Sdf.Shell(Sdf.Cyl(V(0, y - 0.01f, -0.012f), V(0, y + 0.01f, -0.012f), neckR + 0.004f, 0.0f), 0.003f), Sdf.Box(V(0, y, 0), V(0.2f, 0.009f, 0.2f), 0f)), Glossy(new Color(0.06f, 0.06f, 0.07f), "Choker"), 1);
                part.Add(Sdf.Sphere(V(0, y - 0.012f, FrontZNeck(y) + 0.008f), 0.005f), Metal(SilverC, "ChokerCharm"), 2);
            }

            if (Acc(Accessory.GoldChain))
            {
                var part = AccPartBlend("GoldChain", 0.0018f, HBone.Chest, HBone.Neck);
                float y0 = neckBaseY - 0.005f, y1 = shY - 0.13f;
                var chain = Sdf.Func(p =>
                {
                    // U-shaped curve around the neck, down the chest
                    float ang = Mathf.Atan2(p.x, p.z + 0.01f);
                    float frontT = Mathf.Clamp01(Mathf.Cos(ang));
                    float yC = Mathf.Lerp(y0, y1, frontT * frontT);
                    float rr = Mathf.Lerp(neckR + 0.018f, 0.07f, frontT);
                    float rad = new Vector2(p.x, (p.z + 0.01f)).magnitude;
                    float zOff = FrontZ(Mathf.Clamp(yC, waistY, neckBaseY)) + 0.012f;
                    float radial = frontT > 0.2f ? Mathf.Abs(p.z - zOff) : Mathf.Abs(rad - rr);
                    float links = Mathf.Sin(ang * 60f) * 0.0012f;
                    return new Vector2(radial, p.y - yC).magnitude - 0.0045f - links;
                }, new Bounds(V(0, (y0 + y1) * 0.5f, 0), V(0.3f, y0 - y1 + 0.06f, 0.3f)));
                var chainC = Sdf.Inter(chain, KeepAbove(Sdf.Offset(torsoSkin, 0.03f), y1 - 0.02f));
                part.Add(chainC, Metal(GoldC, "GoldChain"), 1);
                // pendant
                part.Add(Sdf.Box(V(0, y1 - 0.02f, FrontZ(y1) + 0.016f), V(0.014f, 0.02f, 0.003f), 0.004f), Metal(GoldC, "GoldChain"), 2);
            }

            if (Acc(Accessory.Rings))
            {
                int rm = Metal(def.Id == "P09" ? SilverC : GoldC, "Ring");
                for (int s = 0; s < 2; s++)
                {
                    if (s == 1 && Acc(Accessory.ProstheticHandR)) continue;
                    HBone hb = s == 0 ? HBone.HandL : HBone.HandR;
                    var part = AccPart("Rings" + s, Bi(hb), 0.0012f);
                    int[] fingers = def.Id == "P07" ? new[] { 0, 1, 2, 3 } : new[] { 1, 3 };
                    float[] zs = { 0.026f, 0.0085f, -0.0085f, -0.025f };
                    foreach (int f in fingers)
                    {
                        if (s == 0 && f == 3 && def.Id != "P07") continue;
                        Vector3 c0 = HandPt(s, 0.1f, 0.001f, zs[f]);
                        Vector3 c1 = HandPt(s, 0.112f, 0.004f, zs[f]);
                        Vector3 d = (c1 - c0).normalized;
                        part.Add(Sdf.Torus(c0, Quaternion.FromToRotation(Vector3.up, d), 0.0095f * P.HandLen / 0.18f, 0.0028f), rm, 1);
                    }
                }
            }

            if (Acc(Accessory.Watch) || Acc(Accessory.DigitalWatch))
            {
                bool dig = Acc(Accessory.DigitalWatch);
                var part = AccPart("Watch", Bi(HBone.LowerArmL), 0.0014f);
                Vector3 w = ArmW(0), d = ArmDir(0);
                Vector3 c = w - d * 0.028f;
                var strap = Sdf.Inter(Sdf.Inter(ArmShell(0, 0.012f), Sdf.Plane(c + d * 0.01f, d)), Sdf.Plane(c - d * 0.01f, -d));
                part.Add(strap, dig ? Glossy(new Color(0.1f, 0.1f, 0.11f), "WatchStrap") : Cloth(new Color(0.18f, 0.1f, 0.06f), name: "WatchStrap"), 1);
                // face on the outer (lateral) side of the wrist
                Vector3 outN = V(SX(0), 0, 0.2f).normalized;
                Vector3 fc = c + outN * (P.WristR + 0.014f);
                var face = Sdf.Cyl(fc - outN * 0.004f, fc + outN * 0.004f, dig ? 0.017f : 0.016f, 0.002f);
                if (dig) face = Sdf.Box(fc, V(0.006f, 0.019f, 0.017f), Quaternion.LookRotation(Vector3.forward, Vector3.up), 0.003f);
                part.Add(face, dig ? Glossy(new Color(0.12f, 0.12f, 0.13f), "WatchCase") : Metal(GoldC, "WatchCase"), 2);
                part.Add(Sdf.Cyl(fc + outN * 0.0035f, fc + outN * 0.0055f, dig ? 0.011f : 0.012f, 0.001f), Glossy(dig ? new Color(0.55f, 0.66f, 0.55f) : new Color(0.95f, 0.93f, 0.88f), "WatchDial"), 3);
            }

            if (Acc(Accessory.BassBracelet))
            {
                var part = AccPart("Bracelet", Bi(HBone.LowerArmR), 0.0012f);
                Vector3 w = ArmW(1), d = ArmDir(1);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 c = w - d * (0.03f + i * 0.006f);
                    part.Add(Sdf.Displace(Sdf.Torus(c, Quaternion.FromToRotation(Vector3.up, d), P.WristR + 0.009f, 0.0018f), p => Mathf.Sin(Mathf.Atan2(p.x - c.x, p.z - c.z) * 40f), 0.0006f), Metal(i == 1 ? new Color(0.78f, 0.6f, 0.35f) : SilverC, "BassString"), 1);
                }
            }

            if (Acc(Accessory.Earrings))
            {
                var part = AccPart("Earrings", Bi(HBone.Head), 0.0012f);
                int em = Metal(def.Id == "P07" ? GoldC : def.Id == "P16" ? SilverC : new Color(0.85f, 0.78f, 0.6f), "Earring");
                for (int s = 0; s < 2; s++)
                {
                    Vector3 e = EarPos(s);
                    if (def.Id == "P07" || def.Id == "P09")
                        part.Add(Sdf.Torus(e + V(0, -0.012f, 0.004f), Quaternion.Euler(0, 0, 90f), 0.009f, 0.0018f), em, 1);
                    else
                    {
                        part.Add(Sdf.Sphere(e, 0.0035f), em, 1);
                        part.Add(Sdf.Capsule(e, e + V(0, -0.028f, 0), 0.0012f), em, 1);
                        part.Add(Sdf.Ellipsoid(e + V(0, -0.034f, 0), V(0.004f, 0.008f, 0.004f)), em, 1);
                    }
                }
            }

            if (Acc(Accessory.Sunglasses) || Acc(Accessory.Glasses) || Acc(Accessory.RoundGlasses)) Glasses();
            if (Acc(Accessory.Goggles)) Goggles();
            if (Acc(Accessory.Beanie)) Beanie();
            if (Acc(Accessory.Beret)) Beret();
            if (Acc(Accessory.Headband)) Headband();
            if (Acc(Accessory.BigRibbon)) BigRibbon();
            if (Acc(Accessory.Headset)) Headset();
            if (Acc(Accessory.Earphone)) Earphone();
            if (Acc(Accessory.SilkScarf) || Acc(Accessory.Scarf)) Scarf();
            if (Acc(Accessory.PearlBrooch) || Acc(Accessory.Brooch)) Brooch();
            if (Acc(Accessory.Lanyard)) Lanyard();
            if (Acc(Accessory.TapeMeasure)) TapeMeasure();
            if (Acc(Accessory.ToolBelt)) ToolBelt();
            if (Acc(Accessory.PocketWatch)) PocketWatch();
            if (Acc(Accessory.PenEar) && L.Hair != HairStyle.BunMessy)
            {
                Vector3 e = EarPos(1) + V(0.012f, 0.03f, 0.0f);
                var part = AccPart("PenEar", Bi(HBone.Head), 0.0012f);
                Vector3 a = e + V(0, 0.01f, 0.05f), b = e + V(0.004f, -0.006f, -0.06f);
                part.Add(Sdf.Cyl(a, b, 0.0038f, 0.001f), Glossy(new Color(0.1f, 0.1f, 0.12f), "Pen"), 1);
                part.Add(Sdf.Capsule(b + V(0, 0.004f, 0.01f), b + V(0, 0.004f, 0.03f), 0.0012f), Metal(SilverC, "PenClip"), 2);
            }

            // held props
            if (Acc(Accessory.Clipboard)) HeldClipboard();
            if (Acc(Accessory.Briefcase)) HeldBriefcase();
            if (Acc(Accessory.LilyFlower)) HeldLily();
            if (Acc(Accessory.Notebook)) HeldNotebook();
            if (Acc(Accessory.Thermos)) HeldThermos();
        }

        float FrontZNeck(float y)
        {
            float z = 0.2f;
            for (int i = 0; i < 40; i++) { float d = torsoSkin.D(V(0, y, z)); if (Mathf.Abs(d) < 0.0005f) break; z -= d; }
            return z;
        }

        Sdf Star(Vector3 c, Vector3 n, float r)
        {
            Quaternion q = Quaternion.LookRotation(n, Vector3.up);
            Quaternion inv = Quaternion.Inverse(q);
            return Sdf.Func(p =>
            {
                Vector3 l = inv * (p - c);
                float a = Mathf.Atan2(l.y, l.x);
                float rad = new Vector2(l.x, l.y).magnitude;
                float star = r * (0.55f + 0.45f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f)), 3f));
                return Mathf.Max(rad - star, Mathf.Abs(l.z) - r * 0.18f) * 0.7f;
            }, new Bounds(c, Vector3.one * r * 2.4f));
        }

        void Glasses()
        {
            bool sun = Acc(Accessory.Sunglasses), round = Acc(Accessory.RoundGlasses);
            var part = AccPart("Glasses", Bi(HBone.Head), 0.0011f);
            int frame = sun ? Metal(GoldC, "GlassFrame") : round ? Metal(AccC, "GlassFrame") : Glossy(new Color(0.07f, 0.06f, 0.07f), "GlassFrame");
            float ev = FaceEye.y + (sun ? 0.005f : 0.0f);
            float lensR = (sun ? 0.115f : round ? 0.092f : 0.1f) * 0.9f * hh;
            for (int s = 0; s < 2; s++)
            {
                float u = 0.5f + (s == 0 ? FaceEye.x : -FaceEye.x); // s==0 -> character left (image right)
                Vector3 c = FacePoint(u, ev, sun ? 0.016f : 0.012f);
                Quaternion q = Quaternion.Euler(90f, 0, 0) * Quaternion.identity;
                Quaternion face = Quaternion.LookRotation(Vector3.forward, Vector3.up) * Quaternion.Euler(0, SX(s) * 8f, 0);
                if (round)
                    part.Add(Sdf.Torus(c, face * Quaternion.Euler(90f, 0, 0), lensR, 0.0016f), frame, 2);
                else
                {
                    float w = lensR * (sun ? 1.15f : 1.12f), hgt = lensR * (sun ? 0.85f : 0.72f);
                    var outer = Sdf.Box(c, V(w, hgt, 0.0025f), face, hgt * 0.6f);
                    var inner = Sdf.Box(c, V(w - (sun ? 0.003f : 0.0035f), hgt - (sun ? 0.003f : 0.0035f), 0.006f), face, hgt * 0.5f);
                    part.Add(Sdf.Sub(outer, inner), frame, 2);
                    if (sun) part.Add(Sdf.Box(c + V(0, 0, -0.001f), V(w - 0.002f, hgt - 0.002f, 0.0012f), face, hgt * 0.55f), M.Mat(new MatSpec { Name = "SunLens", Color = new Color(0.03f, 0.025f, 0.04f), Gloss = 0f, GlossThreshold = 0.97f, Rim = 0.25f, UseVertexColor = false, Shade = new Color(0.9f, 0.9f, 0.95f), Shade2 = new Color(0.85f, 0.85f, 0.9f) }), 1);
                }
                // temple arm to the ear
                Vector3 hinge = c + V(SX(s) * lensR * 1.1f, 0.002f, -0.004f);
                Vector3 ear = EarPos(s) + V(-SX(s) * 0.004f, 0.022f, 0.0f);
                part.Add(Sdf.Capsule(hinge, ear, 0.0016f), frame, 2);
            }
            Vector3 l = FacePoint(0.5f - FaceEye.x + lensR / (0.9f * hh) * 0.85f, ev + 0.01f, sun ? 0.017f : 0.013f);
            Vector3 r = FacePoint(0.5f + FaceEye.x - lensR / (0.9f * hh) * 0.85f, ev + 0.01f, sun ? 0.017f : 0.013f);
            Vector3 mid = FacePoint(0.5f, ev + 0.02f, sun ? 0.02f : 0.014f);
            part.Add(Sdf.Tube(new[] { l, mid, r }, new[] { 0.0016f, 0.0018f, 0.0016f }), frame, 2);
        }

        void Goggles()
        {
            var part = AccPart("Goggles", Bi(HBone.Head), 0.0015f);
            int rubber = Cloth(new Color(0.2f, 0.18f, 0.17f), name: "GoggleStrap");
            int lens = M.Mat(new MatSpec { Name = "GoggleLens", Color = AccC, Gloss = 1.2f, GlossThreshold = 0.88f, Rim = 0.7f, Shade = new Color(0.75f, 0.8f, 0.9f), Shade2 = new Color(0.6f, 0.65f, 0.8f) });
            int brass = Metal(new Color(0.72f, 0.56f, 0.3f), "GoggleRim");
            // strap around the head at forehead height
            float el = 52f;
            var strap = Sdf.Func(p =>
            {
                Vector3 q = p - cc; Vector3 qn = new Vector3(q.x / (cr.x + 0.045f), q.y / (cr.y + 0.045f), q.z / (cr.z + 0.045f));
                float r = qn.magnitude;
                float e = Mathf.Asin(Mathf.Clamp(qn.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                float az = Mathf.Atan2(q.x, q.z) * Mathf.Rad2Deg;
                float elC = el - 22f * (1f - Mathf.Cos(az * Mathf.Deg2Rad)) * 0.5f;
                return Mathf.Max(Mathf.Abs(r - 1f) * cr.x - 0.004f, Mathf.Abs(e - elC) * Mathf.Deg2Rad * cr.y - 0.012f);
            }, new Bounds(cc, cr * 2.6f));
            part.Add(strap, rubber, 1);
            for (int s = 0; s < 2; s++)
            {
                Vector3 p = Surf(SX(s) * 20f, el + 3f, 0.05f);
                Vector3 n = (p - cc).normalized;
                part.Add(Sdf.Cyl(p - n * 0.008f, p + n * 0.012f, 0.026f, 0.004f), brass, 2);
                part.Add(Sdf.Cyl(p + n * 0.011f, p + n * 0.014f, 0.021f, 0.002f), lens, 3);
            }
            part.Add(Sdf.Capsule(Surf(-9f, el + 3f, 0.056f), Surf(9f, el + 3f, 0.056f), 0.005f), brass, 2);
        }

        void Beanie()
        {
            var part = AccPart("Beanie", Bi(HBone.Head), 0.0026f);
            int mat = Cloth(Hex("#3A4250"), 8, null, new Vector4(0.01f, 0.1f, 0, 0), name: "Beanie");
            var cap = Sdf.Offset(Sdf.Ellipsoid(cc + V(0, 0.012f, -0.012f), cr), 0.05f);
            cap = Sdf.Inter(cap, Sdf.Plane(cc + V(0, 0.2f * hh, 0.1f * hh), V(0, -1f, 0.35f).normalized));
            part.Add(Sdf.Displace(cap, p => Mathf.Sin(Mathf.Atan2(p.x, p.z) * 40f), 0.0015f), mat, 1);
            var cuff = Sdf.Inter(Sdf.Offset(Sdf.Ellipsoid(cc + V(0, 0.012f, -0.012f), cr), 0.056f), Sdf.Inter(Sdf.Plane(cc + V(0, 0.2f * hh, 0.1f * hh), V(0, 1f, -0.35f).normalized), Sdf.Plane(cc + V(0, 0.2f * hh + 0.04f, 0.1f * hh), V(0, -1f, 0.35f).normalized)));
            part.Add(cuff, mat, 2);
        }

        void Beret()
        {
            var part = AccPart("Beret", Bi(HBone.Head), 0.0024f);
            int mat = Cloth(Hex("#1D1D22"), 0, name: "Beret");
            Vector3 c = cc + V(-0.03f, cr.y + 0.018f, -0.02f);
            part.Add(Sdf.Ellipsoid(c, V(cr.x * 1.18f, 0.036f, cr.z * 1.12f), Quaternion.Euler(-8f, 0, 16f)), mat, 1);
            part.Add(Sdf.Capsule(c + V(0, 0.03f, 0), c + V(0.002f, 0.042f, 0), 0.0035f), mat, 2);
        }

        void Headband()
        {
            var part = AccPart("Headband", Bi(HBone.Head), 0.002f);
            int mat = Cloth(AccC, name: "Headband");
            var band = Sdf.Func(p =>
            {
                Vector3 q = p - cc; Vector3 qn = new Vector3(q.x / (cr.x + 0.02f), q.y / (cr.y + 0.02f), q.z / (cr.z + 0.02f));
                float r = qn.magnitude;
                float e = Mathf.Asin(Mathf.Clamp(qn.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                float az = Mathf.Atan2(q.x, q.z) * Mathf.Rad2Deg;
                float elC = 40f - 30f * (1f - Mathf.Cos(az * Mathf.Deg2Rad)) * 0.5f;
                return Mathf.Max(Mathf.Abs(r - 1f) * cr.x - 0.005f, Mathf.Abs(e - elC) * Mathf.Deg2Rad * cr.y - 0.016f);
            }, new Bounds(cc, cr * 2.4f));
            part.Add(band, mat, 1);
        }

        void BigRibbon()
        {
            var part = AccPart("BigRibbon", Bi(HBone.Head), 0.002f);
            int mat = Cloth(AccC.grayscale < 0.2f ? new Color(0.07f, 0.06f, 0.08f) : AccC, name: "BigRibbon");
            Vector3 c = Surf(180f, 38f, 0.03f);
            part.Add(Sdf.Ellipsoid(c, V(0.022f, 0.02f, 0.016f)), mat, 2);
            for (int s = 0; s < 2; s++)
            {
                Vector3 lc = c + V(SX(s) * 0.075f, 0.03f, -0.012f);
                var loop = Sdf.Shell(Sdf.Ellipsoid(lc, V(0.07f, 0.05f, 0.022f), Quaternion.Euler(0, SX(s) * 12f, SX(s) * -18f)), 0.004f);
                loop = Sdf.Sub(loop, Sdf.Ellipsoid(lc + V(0, 0, -0.02f), V(0.05f, 0.03f, 0.02f)));
                part.Add(Sdf.Union(0.01f, loop, Sdf.Ellipsoid(lc, V(0.068f, 0.048f, 0.006f), Quaternion.Euler(0, SX(s) * 12f, SX(s) * -18f))), mat, 1);
                part.Add(Sdf.Cone(c + V(SX(s) * 0.01f, -0.01f, -0.005f), c + V(SX(s) * 0.05f, -0.16f, -0.03f), 0.012f, 0.02f), mat, 1);
            }
        }

        void Headset()
        {
            var part = AccPartBlend("Headset", 0.0018f, HBone.Neck, HBone.Chest);
            int dark = Glossy(new Color(0.1f, 0.1f, 0.12f), "Headset");
            int red = Cloth(new Color(0.82f, 0.13f, 0.23f), name: "HeadsetAccent");
            float y = neckBaseY - 0.005f;
            // band behind the neck
            var band = Sdf.Func(p =>
            {
                float ang = Mathf.Atan2(p.x, p.z + 0.012f);
                float back = Mathf.Clamp01(-Mathf.Cos(ang));
                float rr = P.NeckR + 0.03f;
                float rad = new Vector2(p.x, p.z + 0.012f).magnitude;
                float yC = y + 0.02f + back * 0.03f;
                return Mathf.Max(new Vector2(rad - rr, p.y - yC).magnitude - 0.006f, Mathf.Cos(ang) - 0.25f);
            }, new Bounds(V(0, y, 0), V(0.3f, 0.12f, 0.3f)));
            part.Add(band, dark, 1);
            for (int s = 0; s < 2; s++)
            {
                Vector3 cup = V(SX(s) * (P.NeckR + 0.045f), y + 0.005f, 0.035f);
                part.Add(Sdf.Cyl(cup - V(SX(s) * 0.012f, 0, 0), cup + V(SX(s) * 0.014f, 0, 0), 0.038f, 0.012f), dark, 2);
                part.Add(Sdf.Cyl(cup + V(SX(s) * 0.012f, 0, 0), cup + V(SX(s) * 0.017f, 0, 0), 0.028f, 0.003f), red, 3);
            }
            Vector3 m0 = V(-(P.NeckR + 0.05f), y, 0.05f);
            part.Add(Sdf.Tube(new[] { m0, m0 + V(0.03f, -0.02f, 0.05f), m0 + V(0.06f, -0.03f, 0.07f) }, new[] { 0.0025f, 0.0025f, 0.004f }), dark, 2);
        }

        void Earphone()
        {
            var part = AccPartBlend("Earphone", 0.0014f, HBone.Head, HBone.Neck, HBone.Chest);
            int white = Glossy(new Color(0.95f, 0.95f, 0.96f), "Earbud");
            Vector3 e = EarPos(1) + V(0.004f, 0.012f, 0.012f);
            part.Add(Sdf.Sphere(e, 0.0075f), white, 1);
            Vector3 a = e + V(0, -0.012f, 0), b = V(P.NeckR + 0.04f, neckBaseY - 0.02f, 0.05f), c = V(0.08f, shY - 0.12f, FrontZ(shY - 0.12f) + 0.03f);
            part.Add(Sdf.Tube(new[] { a, Vector3.Lerp(a, b, 0.5f) + V(0.01f, 0, 0.01f), b, c }, new[] { 0.0014f, 0.0014f, 0.0014f, 0.0014f }), white, 1);
        }

        void Scarf()
        {
            var part = AccPartBlend("Scarf", 0.0026f, HBone.Neck, HBone.Chest);
            int mat = M.Mat(new MatSpec { Name = "SilkScarf", Color = AccC, Gloss = 0.5f, GlossThreshold = 0.95f, Rim = 0.4f, Shade = new Color(0.6f, 0.62f, 0.75f), Shade2 = new Color(0.4f, 0.44f, 0.58f) });
            float y = neckBaseY + 0.02f;
            var loop = Sdf.Displace(Sdf.Torus(V(0, y, -0.008f), Quaternion.Euler(-10f, 0, 0), P.NeckR + 0.02f, 0.017f), p => Mathf.Sin(Mathf.Atan2(p.x, p.z) * 9f + p.y * 80f), 0.003f);
            part.Add(loop, mat, 1);
            Vector3 knot = V(0.018f, y - 0.018f, FrontZNeck(y - 0.02f) + 0.03f);
            part.Add(Sdf.Ellipsoid(knot, V(0.024f, 0.02f, 0.018f)), mat, 2);
            part.Add(Sdf.Cone(knot, knot + V(0.028f, -0.13f, 0.012f), 0.016f, 0.028f), mat, 1);
            part.Add(Sdf.Cone(knot, knot + V(-0.012f, -0.1f, 0.02f), 0.015f, 0.024f), mat, 1);
        }

        void Brooch()
        {
            bool pearl = Acc(Accessory.PearlBrooch);
            var part = AccPartBlend("Brooch", 0.0012f, HBone.Chest, HBone.Neck);
            if (pearl)
            {
                float y = neckBaseY - 0.03f;
                Vector3 c = V(0, y, FrontZ(y) + 0.012f);
                int pm = M.Mat(new MatSpec { Name = "Pearl", Color = new Color(0.97f, 0.95f, 0.9f), Gloss = 0.9f, GlossThreshold = 0.9f, Rim = 0.7f, Shade = new Color(0.8f, 0.78f, 0.88f), Shade2 = new Color(0.66f, 0.64f, 0.76f) });
                part.Add(Sdf.Cyl(c - V(0, 0, 0.003f), c + V(0, 0, 0.002f), 0.019f, 0.002f), Metal(GoldC, "BroochSetting"), 1);
                part.Add(Sdf.Sphere(c + V(0, 0, 0.004f), 0.009f), pm, 2);
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f * Mathf.PI * 2f;
                    part.Add(Sdf.Sphere(c + V(Mathf.Cos(a) * 0.016f, Mathf.Sin(a) * 0.016f, 0.003f), 0.0042f), pm, 2);
                }
                part.Add(Sdf.Sphere(c + V(0, -0.028f, 0.003f), 0.0055f), pm, 2);
                part.Add(Sdf.Capsule(c + V(0, -0.018f, 0.003f), c + V(0, -0.023f, 0.003f), 0.0012f), Metal(GoldC, "BroochSetting"), 1);
            }
            else
            {
                float y = shY - 0.07f;
                Vector3 c = V(-0.1f, y, FrontZ(y) + 0.04f);
                int sm = Metal(AccC, "Brooch");
                part.Add(Star(c, V(0, 0.1f, 1f).normalized, 0.018f), sm, 1);
                part.Add(Sdf.Sphere(c + V(0, 0, 0.004f), 0.006f), Glossy(new Color(0.55f, 0.1f, 0.4f), "BroochGem"), 2);
            }
        }

        void Lanyard()
        {
            var part = AccPartBlend("Lanyard", 0.0018f, HBone.Neck, HBone.Chest, HBone.Spine);
            bool phone = Acc(Accessory.Smartphone);
            int cord = Cloth(phone ? new Color(1f, 0.6f, 0.74f) : new Color(0.12f, 0.12f, 0.14f), name: "Lanyard");
            float y0 = neckBaseY + 0.005f;
            float yEnd = phone ? J(HBone.Chest).y - 0.08f : J(HBone.Chest).y - 0.03f;
            Vector3 end = V(0, yEnd, FrontZ(yEnd) + (phone ? 0.05f : 0.04f));
            for (int s = 0; s < 2; s++)
            {
                Vector3 back = V(0, y0 + 0.01f, BackZ(y0) - 0.005f);
                Vector3 side = V(SX(s) * (P.NeckR + 0.02f), y0, -0.005f);
                Vector3 front = V(SX(s) * 0.035f, (y0 + yEnd) * 0.5f, FrontZ((y0 + yEnd) * 0.5f) + 0.03f);
                part.Add(Sdf.Tube(new[] { back, side, front, end + V(0, 0.02f, 0) }, new[] { 0.003f, 0.003f, 0.003f, 0.003f }), cord, 1);
            }
            if (phone)
            {
                part.Add(Sdf.Box(end + V(0, -0.06f, 0.004f), V(0.036f, 0.074f, 0.0055f), 0.009f), Glossy(new Color(0.97f, 0.62f, 0.75f), "PhoneCase"), 2);
                part.Add(Sdf.Box(end + V(0, -0.06f, 0.0095f), V(0.031f, 0.068f, 0.001f), 0.006f), Glossy(new Color(0.06f, 0.07f, 0.1f), "PhoneScreen"), 3);
                part.Add(Star(end + V(0.018f, -0.11f, 0.012f), Vector3.forward, 0.01f), Glossy(new Color(0.55f, 0.9f, 0.8f), "Charm"), 3);
            }
            else
            {
                part.Add(Sdf.Box(end + V(0, -0.05f, 0.002f), V(0.042f, 0.056f, 0.0025f), 0.004f), Glossy(new Color(0.96f, 0.96f, 0.94f), "PressCard"), 2);
                part.Add(Sdf.Box(end + V(-0.016f, -0.038f, 0.005f), V(0.013f, 0.016f, 0.0008f), 0.001f), Cloth(new Color(0.35f, 0.42f, 0.55f), name: "CardPhoto"), 3);
                part.Add(Sdf.Box(end + V(0, -0.085f, 0.005f), V(0.036f, 0.008f, 0.0008f), 0.001f), Cloth(new Color(0.8f, 0.12f, 0.15f), name: "CardStripe"), 3);
            }
        }

        void TapeMeasure()
        {
            var part = AccPartBlend("TapeMeasure", 0.0018f, HBone.Neck, HBone.Chest);
            int mat = M.Mat(new MatSpec { Name = "Tape", Color = new Color(0.96f, 0.84f, 0.3f), Pattern = 11, PatternColor = new Color(0.1f, 0.1f, 0.1f), PatternParams = new Vector4(0.01f, 0.1f, 0, 0), Rim = 0.3f, Shade = new Color(0.8f, 0.66f, 0.6f), Shade2 = new Color(0.6f, 0.5f, 0.5f) });
            float y0 = neckBaseY + 0.01f;
            for (int s = 0; s < 2; s++)
            {
                float yEnd = s == 0 ? waistY + 0.02f : waistY - 0.04f;
                var pts = new List<Vector3> { V(0, y0 + 0.01f, BackZ(y0) - 0.012f), V(SX(s) * (P.NeckR + 0.022f), y0, -0.01f), V(SX(s) * 0.06f, shY - 0.05f, FrontZ(shY - 0.05f) + 0.03f), V(SX(s) * 0.07f, yEnd, FrontZ(yEnd) + 0.035f) };
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    Vector3 a = pts[i], b = pts[i + 1];
                    Vector3 d = (b - a).normalized;
                    Vector3 n = Vector3.Cross(d, Vector3.up).sqrMagnitude > 0.01f ? Vector3.Cross(d, Vector3.Cross(Vector3.up, d)).normalized : Vector3.forward;
                    Vector3 w = Vector3.Cross(d, n).normalized;
                    var q = Quaternion.LookRotation(d, n);
                    part.Add(Sdf.Box((a + b) * 0.5f, V(0.0095f, 0.0009f, (b - a).magnitude * 0.5f + 0.004f), q, 0.0008f), mat, 1);
                }
            }
        }

        void ToolBelt()
        {
            var part = AccPartBlend("ToolBelt", 0.0024f, HBone.Hips, HBone.Spine);
            int leather = Cloth(new Color(0.36f, 0.24f, 0.15f), name: "ToolBelt");
            int metal = Metal(new Color(0.7f, 0.72f, 0.75f), "Tool");
            float y = hipsY - 0.01f;
            part.Add(KeepBelow(KeepAbove(TorsoShell(0.03f), y - 0.018f), y + 0.018f), leather, 1);
            for (int s = 0; s < 2; s++)
            {
                float x = SideX(y, s) * 0.95f;
                Vector3 pc = V(x + SX(s) * 0.02f, y - 0.05f, 0.03f);
                part.Add(Sdf.Box(pc, V(0.022f, 0.05f, 0.045f), Quaternion.Euler(0, SX(s) * 20f, 0), 0.008f), leather, 2);
                part.Add(Sdf.Cyl(pc + V(0, 0.03f, 0.015f), pc + V(0, 0.09f, 0.02f), 0.006f, 0.002f), s == 0 ? Cloth(new Color(0.85f, 0.2f, 0.15f), name: "ScrewdriverGrip") : metal, 3);
            }
            // wrench hanging at the back-side
            Vector3 w0 = V(SideX(y, 1) * 0.8f, y - 0.02f, BackZ(y) * 0.5f);
            part.Add(Sdf.Box(w0 + V(0, -0.07f, 0), V(0.006f, 0.07f, 0.012f), Quaternion.Euler(0, 30f, 8f), 0.003f), metal, 3);
            part.Add(Sdf.Torus(w0 + V(0, -0.145f, 0), Quaternion.Euler(0, 30f, 90f), 0.016f, 0.006f), metal, 3);
        }

        void PocketWatch()
        {
            var part = AccPartBlend("PocketWatch", 0.0014f, HBone.Spine, HBone.Chest);
            int gold = Metal(GoldC, "WatchChain");
            float y0 = waistY + 0.05f, y1 = waistY - 0.005f;
            Vector3 a = V(0.004f, y0, FrontZ(y0) + 0.017f);
            Vector3 b = V(0.1f, y1, FrontZ(y1) + 0.016f);
            var pts = new List<Vector3>();
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                Vector3 p = Vector3.Lerp(a, b, t) + V(0, -0.035f * Mathf.Sin(t * Mathf.PI), 0);
                p.z = FrontZ(p.y) + 0.018f + 0.004f * Mathf.Sin(t * Mathf.PI);
                pts.Add(p);
            }
            part.Add(Sdf.Displace(Sdf.Tube(pts, pts.Select(_ => 0.0019f).ToList()), p => Mathf.Sin(p.x * 900f), 0.0006f), gold, 1);
            part.Add(Sdf.Box(b + V(0, 0.004f, 0), V(0.016f, 0.004f, 0.004f), 0.002f), Cloth(Color.Lerp(Hex(G(Garment.Vest)?.Color ?? "#222222"), Color.black, 0.4f), name: "WatchPocket"), 2);
        }

        // ------------------------------------------------------------------ held props (anchor space: origin palm, +Y up, +Z forward)
        public sealed class HeldProp { public string Name; public string Anchor; public Part Part; }
        public readonly List<HeldProp> Held = new List<HeldProp>();

        Part Held_(string name, string anchor)
        {
            var p = new Part { Name = name, Res = 0.0022f, Skin = SkinRule.RigidTo(0), AOScale = 0.3f };
            Held.Add(new HeldProp { Name = name, Anchor = anchor, Part = p });
            return p;
        }

        void HeldClipboard()
        {
            var p = Held_("Clipboard", "HandAnchorL");
            int board = Cloth(new Color(0.42f, 0.3f, 0.2f), name: "ClipBoard");
            int paper = Cloth(new Color(0.97f, 0.96f, 0.93f), 1, new Color(0.7f, 0.75f, 0.85f), new Vector4(0.012f, 0.08f, 0, 0), name: "Paper");
            // board hanging vertically along the thigh, held at the top edge
            p.Add(Sdf.Box(V(0, -0.14f, 0.04f), V(0.004f, 0.16f, 0.115f), 0.004f), board, 1);
            p.Add(Sdf.Box(V(0.0045f, -0.15f, 0.04f), V(0.0012f, 0.14f, 0.1f), 0.001f), paper, 2);
            p.Add(Sdf.Box(V(0.006f, 0.0f, 0.04f), V(0.006f, 0.012f, 0.04f), 0.003f), Metal(new Color(0.75f, 0.76f, 0.8f), "Clip"), 3);
        }

        void HeldBriefcase()
        {
            var p = Held_("Briefcase", "HandAnchorR");
            int leather = Glossy(new Color(0.25f, 0.14f, 0.08f), "Briefcase");
            int brass = Metal(AccC, "Clasp");
            p.Add(Sdf.Torus(V(0, 0.0f, 0), Quaternion.Euler(0, 0, 90f), 0.03f, 0.0065f), leather, 2);
            p.Add(Sdf.Box(V(0, -0.2f, 0), V(0.045f, 0.16f, 0.21f), 0.012f), leather, 1);
            for (int s = 0; s < 2; s++)
                p.Add(Sdf.Box(V(SX(s) * 0.046f, -0.07f, 0.08f), V(0.004f, 0.012f, 0.01f), 0.002f), brass, 3);
            p.Add(Sdf.Box(V(0, -0.045f, 0), V(0.048f, 0.006f, 0.212f), 0.003f), brass, 3);
        }

        void HeldLily()
        {
            var p = Held_("Lily", "HandAnchorL");
            int stem = Cloth(new Color(0.3f, 0.5f, 0.25f), name: "Stem");
            int petal = M.Mat(new MatSpec { Name = "LilyPetal", Color = new Color(0.98f, 0.98f, 0.96f), Rim = 0.6f, Shade = new Color(0.82f, 0.85f, 0.92f), Shade2 = new Color(0.7f, 0.72f, 0.82f) });
            Vector3 top = V(0, 0.22f, 0.06f);
            p.Add(Sdf.Tube(new[] { V(0, -0.1f, 0f), V(0, 0.06f, 0.02f), top }, new[] { 0.0035f, 0.0035f, 0.003f }), stem, 1);
            p.Add(Sdf.Ellipsoid(V(0.01f, 0.05f, 0.03f), V(0.012f, 0.035f, 0.004f), Quaternion.Euler(20f, 0, -30f)), stem, 1);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * 360f;
                Quaternion q = Quaternion.AngleAxis(a, Vector3.up) * Quaternion.Euler(55f, 0, 0);
                Vector3 c = top + q * V(0, 0.028f, 0);
                p.Add(Sdf.Ellipsoid(c, V(0.011f, 0.034f, 0.0035f), q), petal, 2);
            }
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * 360f + 30f;
                Vector3 d = Quaternion.AngleAxis(a, Vector3.up) * V(0.012f, 0.035f, 0);
                p.Add(Sdf.Capsule(top, top + d, 0.0012f), Cloth(new Color(0.85f, 0.55f, 0.2f), name: "Stamen"), 3);
            }
        }

        void HeldNotebook()
        {
            var p = Held_("Notebook", "HandAnchorL");
            p.Add(Sdf.Box(V(0.0f, -0.07f, 0.03f), V(0.012f, 0.1f, 0.07f), 0.004f), Cloth(new Color(0.2f, 0.22f, 0.3f), name: "NotebookCover"), 1);
            p.Add(Sdf.Box(V(0.0f, -0.07f, 0.034f), V(0.0105f, 0.096f, 0.066f), 0.002f), Cloth(new Color(0.95f, 0.93f, 0.86f), name: "NotebookPages"), 2);
            p.Add(Sdf.Box(V(0.0f, 0.0f, 0.03f), V(0.0132f, 0.02f, 0.004f), 0.002f), Cloth(new Color(0.75f, 0.2f, 0.2f), name: "NotebookBand"), 3);
        }

        void HeldThermos()
        {
            var p = Held_("Thermos", "HandAnchorL");
            int body = Glossy(AccC, "Thermos");
            p.Add(Sdf.Cyl(V(0, -0.12f, 0.02f), V(0, 0.12f, 0.02f), 0.035f, 0.01f), body, 1);
            p.Add(Sdf.Cyl(V(0, 0.12f, 0.02f), V(0, 0.16f, 0.02f), 0.032f, 0.008f), Glossy(new Color(0.15f, 0.15f, 0.15f), "ThermosCap"), 2);
            p.Add(Sdf.Cyl(V(0, -0.02f, 0.02f), V(0, 0.02f, 0.02f), 0.0365f, 0.002f), Cloth(new Color(0.3f, 0.32f, 0.26f), name: "ThermosGrip"), 3);
        }

        // ------------------------------------------------------------------ aquarium head (NPC00): the brass neck stand is skinned; glass/water/fish are built by AquariumBuilder
        public Vector3 AquariumCenter, AquariumSize;

        void BuildAquariumStand()
        {
            float neckTop = P.ChinPos.y + 0.01f;
            AquariumSize = V(0.30f, 0.205f, 0.23f) * (H / 1.86f);
            AquariumCenter = V(0, neckTop + 0.022f + AquariumSize.y * 0.5f, 0.0f);
            var part = AccPartBlend("AquariumStand", 0.0016f, HBone.Neck, HBone.Head);
            part.Skin = SkinRule.Blend(5f, Bi(HBone.Neck), Bi(HBone.Head));
            int brass = Metal(new Color(0.69f, 0.54f, 0.23f), "Brass");
            float y0 = J(HBone.Neck).y + 0.02f;
            part.Add(Sdf.Cyl(V(0, y0, -0.012f), V(0, neckTop, -0.012f), P.NeckR * 0.95f, 0.004f), brass, 1);
            for (int i = 0; i < 3; i++)
            {
                float y = Mathf.Lerp(y0 + 0.03f, neckTop, i / 2f);
                part.Add(Sdf.Torus(V(0, y, -0.012f), Quaternion.identity, P.NeckR * 0.98f, 0.005f), brass, 2);
            }
            // base plate under the tank
            part.Add(Sdf.Box(V(0, neckTop + 0.01f, AquariumCenter.z), V(AquariumSize.x * 0.28f, 0.01f, AquariumSize.z * 0.3f), 0.006f), brass, 2);
        }
    }
}
