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
        Part torso, skirtPart;
        readonly Part[] arms = new Part[2], legs = new Part[2], hands = new Part[2], feet = new Part[2];
        float shY, neckBaseY, waistY, hipsY, legTop, kneeY, ankleY;
        int prioInner = 2, prioMid = 4, prioOuter = 6, prioOver = 8;
        bool sleevesRolled, bareArms;

        void BuildGarments()
        {
            shY = J(HBone.UpperArmL).y;
            neckBaseY = shY + 0.035f;
            waistY = J(HBone.Spine).y;
            hipsY = J(HBone.Hips).y;
            legTop = J(HBone.UpperLegL).y;
            kneeY = J(HBone.LowerLegL).y;
            ankleY = J(HBone.FootL).y;

            torso = new Part { Name = "Torso", Res = 0.0042f, Skin = SkinRule.Blend(3.2f, Bi(HBone.Hips), Bi(HBone.Spine), Bi(HBone.Chest), Bi(HBone.Neck), Bi(HBone.Head), Bi(HBone.ShoulderL), Bi(HBone.ShoulderR)) };
            float chinY0 = P.ChinPos.y;
            Color neckShade = new Color(0.9f, 0.74f, 0.76f);
            torso.Add(torsoSkin, mSkin, 0, p =>
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(neckBaseY - 0.01f, chinY0 - 0.005f, p.y));
                return Color.Lerp(Color.white, neckShade, k).linear;
            });
            M.Parts.Add(torso);
            for (int s = 0; s < 2; s++)
            {
                HBone sh = s == 0 ? HBone.ShoulderL : HBone.ShoulderR;
                HBone ul = s == 0 ? HBone.UpperLegL : HBone.UpperLegR;
                arms[s] = new Part { Name = "Arm" + (s == 0 ? "L" : "R"), Res = 0.0034f, Skin = SkinRule.Blend(4.5f, Bi(sh), Bi(sh + 1), Bi(sh + 2), Bi(sh + 3)) };
                arms[s].Add(armSkin[s], mSkin, 0);
                legs[s] = new Part { Name = "Leg" + (s == 0 ? "L" : "R"), Res = 0.004f, Skin = SkinRule.Blend(4.5f, Bi(HBone.Hips), Bi(ul), Bi(ul + 1), Bi(ul + 2)) };
                legs[s].Add(legSkin[s], mSkin, 0);
                hands[s] = new Part { Name = "Hand" + (s == 0 ? "L" : "R"), Res = 0.0016f, Skin = HandRule(s), AOScale = 0.35f, Keep = 0.15f, Tolerance = 0.0005f };
                feet[s] = new Part { Name = "Foot" + (s == 0 ? "L" : "R"), Res = 0.0027f, Skin = SkinRule.Blend(6f, Bi(ul + 1), Bi(ul + 2), Bi(ul + 3)), AOScale = 0.5f };
                M.Parts.Add(arms[s]); M.Parts.Add(legs[s]); M.Parts.Add(hands[s]); M.Parts.Add(feet[s]);
            }

            var wear = L.Wear.ToList();
            if (wear.Count == 0)
            {
                wear.Add(new GarmentSpec { Kind = Garment.Shirt, Color = "#E8E4DC" });
                wear.Add(new GarmentSpec { Kind = Garment.Pants, Color = "#2A2A30" });
                wear.Add(new GarmentSpec { Kind = Garment.Sneakers, Color = "#E8E4DC" });
            }
            sleevesRolled = L.Silhouette.Contains("소매 걷음") || L.Silhouette.Contains("걷어 올린 소매") || L.Silhouette.Contains("소매 걷");
            bareArms = wear.Any(w => w.Kind == Garment.TankTop) && !wear.Any(w => IsJacket(w.Kind) || w.Kind == Garment.Shirt);

            // legs first (inner to outer), then torso stack
            foreach (var g in wear) if (IsLegwear(g.Kind)) Legwear(g);
            foreach (var g in wear) if (IsBottom(g.Kind)) Bottom(g);
            foreach (var g in wear) if (IsTop(g.Kind)) Top(g);
            foreach (var g in wear) if (g.Kind == Garment.Vest) Vest(g);
            foreach (var g in wear) if (IsJacket(g.Kind)) Jacket(g);
            foreach (var g in wear) if (g.Kind == Garment.Apron) Apron(g);
            foreach (var g in wear) if (g.Kind == Garment.Capelet) Capelet(g);
            foreach (var g in wear) if (IsShoe(g.Kind)) Shoe(g);
            if (!wear.Any(w => IsShoe(w.Kind))) Shoe(new GarmentSpec { Kind = Garment.Sneakers, Color = "#DDDDDD" });

            // hands (gloves / prosthetic)
            for (int s = 0; s < 2; s++)
            {
                int mat = mSkin;
                if (Acc(Accessory.WhiteGloves)) mat = Cloth(new Color(0.97f, 0.97f, 0.95f), name: "Glove");
                if (Acc(Accessory.LaceGloves)) mat = Cloth(new Color(0.08f, 0.08f, 0.1f), 2, new Color(0.35f, 0.33f, 0.4f), new Vector4(0.008f, 0.1f, 0, 0), name: "LaceGlove");
                if (s == 1 && Acc(Accessory.ProstheticHandR))
                {
                    mat = M.Mat(new MatSpec { Name = "Prosthetic", Color = Hex(L.AccColor, new Color(0.9f, 0.9f, 0.92f)), Gloss = 0.35f, GlossThreshold = 0.9f, Rim = 0.45f, Shade = new Color(0.7f, 0.7f, 0.8f), Shade2 = new Color(0.55f, 0.55f, 0.66f) });
                    // seam rings at the knuckles / wrist
                    hands[s].Add(Sdf.Offset(handSkin[s], 0.0008f), mat, 1);
                    Vector3 w = J(HBone.HandR);
                    Vector3 d = (P.HandTipR - w).normalized;
                    hands[s].Add(Sdf.Cyl(w - d * 0.012f, w + d * 0.004f, P.WristR * 1.12f, 0.004f), Cloth(new Color(0.22f, 0.22f, 0.26f), name: "ProstheticJoint"), 2);
                    continue;
                }
                hands[s].Add(handSkin[s], mat, 0);
            }
        }

        static bool IsLegwear(Garment k) => k == Garment.Tights || k == Garment.KneeSocks || k == Garment.StripedStockings;
        static bool IsBottom(Garment k) => k == Garment.Pants || k == Garment.Jeans || k == Garment.Slacks || k == Garment.CargoPants || k == Garment.Skirt || k == Garment.PleatedSkirt || k == Garment.Shorts || k == Garment.Overalls || k == Garment.WaistTiedOveralls || k == Garment.LongDress || k == Garment.MourningDress;
        static bool IsTop(Garment k) => k == Garment.Shirt || k == Garment.Tshirt || k == Garment.Turtleneck || k == Garment.Knit || k == Garment.Hoodie || k == Garment.TankTop || k == Garment.Blouse || k == Garment.WorkShirt || k == Garment.Cardigan;
        static bool IsJacket(Garment k) => k == Garment.Blazer || k == Garment.SuitJacket || k == Garment.LongCoat || k == Garment.TrenchCoat || k == Garment.FurCoat || k == Garment.CroppedJacket || k == Garment.DenimJacket || k == Garment.Tailcoat;
        static bool IsShoe(Garment k) => k == Garment.Sneakers || k == Garment.HighTops || k == Garment.Boots || k == Garment.Loafers || k == Garment.DressShoes || k == Garment.PlatformShoes;

        // ------------------------------------------------------------------ region helpers
        Vector3 ArmA(int s) => J(s == 0 ? HBone.UpperArmL : HBone.UpperArmR);
        Vector3 ArmW(int s) => J(s == 0 ? HBone.HandL : HBone.HandR);
        Vector3 ArmE(int s) => J(s == 0 ? HBone.LowerArmL : HBone.LowerArmR);
        Vector3 ArmDir(int s) => (ArmW(s) - ArmA(s)).normalized;
        float ArmLen(int s) => (ArmW(s) - ArmA(s)).magnitude;

        /// <summary>Keeps the part of an arm shell between the shoulder and the fraction 'end' (0 shoulder .. 1 wrist).</summary>
        Sdf SleeveClip(int s, Sdf shell, float end)
        {
            Vector3 p = ArmA(s) + ArmDir(s) * ArmLen(s) * end;
            return Sdf.Inter(shell, Sdf.Plane(p, ArmDir(s)));
        }

        Sdf NeckHole(Sdf garment, float r, float yFront, float yBack, float k = 0.006f)
        {
            // vertical cylinder around the neck, cut below by a tilted plane (front lower)
            Vector3 axis = V(0, 0, -0.012f);
            float zSpan = 0.12f;
            Vector3 n = new Vector3(0, -1f, (yFront - yBack) / zSpan).normalized; // plane normal pointing down/front
            var cyl = Sdf.Func(p =>
            {
                float radial = new Vector2(p.x - axis.x, p.z - axis.z).magnitude - r;
                float yCut = Mathf.Lerp(yBack, yFront, Mathf.Clamp01((p.z + 0.06f) / zSpan));
                return Mathf.Max(radial, yCut - p.y);
            }, new Bounds(V(0, (yFront + H) * 0.5f, 0), V(r * 2 + 0.05f, H - yFront + 0.1f, r * 2 + 0.05f)));
            return Sdf.Sub(garment, cyl, k);
        }

        /// <summary>Front V opening from (topY, halfW) narrowing to the apex at apexY.</summary>
        static Sdf VWedge(float topY, float apexY, float halfW, float zMin = 0f)
        {
            Vector3 apex = V(0, apexY, 0);
            Vector3 nL = new Vector3(-(topY - apexY), -halfW, 0).normalized;
            Vector3 nR = new Vector3((topY - apexY), -halfW, 0).normalized;
            return Sdf.Inter(Sdf.Inter(Sdf.Plane(apex, nL), Sdf.Plane(apex, nR)), Sdf.Plane(V(0, 0, zMin), Vector3.back));
        }

        Sdf Armholes(Sdf s, float grow)
        {
            var holes = new List<Sdf>();
            for (int i = 0; i < 2; i++)
                holes.Add(Sdf.Ellipsoid(ArmA(i) + V(SX(i) * 0.012f, -0.035f, 0.0f), V(0.062f + grow, 0.105f + grow, 0.085f + grow)));
            return Sdf.Sub(s, Sdf.Union(0f, holes), 0.008f);
        }

        Sdf TorsoShell(float t) => Sdf.Offset(torsoSkin, t);
        Sdf ArmShell(int s, float t) => Sdf.Offset(armSkin[s], t);

        // ------------------------------------------------------------------ legwear
        void Legwear(GarmentSpec g)
        {
            Color c = Hex(g.Color);
            int mat;
            float top;
            switch (g.Kind)
            {
                case Garment.Tights:
                    mat = M.Mat(new MatSpec { Name = "Tights", Color = c, Rim = 0.45f, Gloss = 0.25f, GlossThreshold = 0.93f, Shade = new Color(0.62f, 0.58f, 0.72f), Shade2 = new Color(0.46f, 0.42f, 0.56f) });
                    top = hipsY + 0.05f; break;
                case Garment.KneeSocks:
                    mat = Cloth(c, 8, null, new Vector4(0.008f, 0.1f, 0, 0), name: "Socks"); top = kneeY - 0.035f; break;
                default: // striped stockings
                    mat = Cloth(c, 7, Hex(g.Color2, Color.white), new Vector4(0.035f, 0.1f, 0, 0), name: "Stockings"); top = kneeY + (legTop - kneeY) * 0.45f; break;
            }
            for (int s = 0; s < 2; s++)
            {
                var shell = KeepBelow(Sdf.Offset(legSkin[s], 0.0018f), top);
                legs[s].Add(shell, mat, 1);
                if (g.Kind == Garment.KneeSocks || g.Kind == Garment.StripedStockings)
                {
                    // elastic top band
                    var band = KeepBelow(KeepAbove(Sdf.Offset(legSkin[s], 0.0045f), top - 0.028f), top);
                    legs[s].Add(band, g.Kind == Garment.StripedStockings ? Cloth(Hex(g.Color2, Color.white), name: "StockingBand") : mat, 2);
                }
            }
            if (g.Kind == Garment.Tights) torso.Add(KeepBelow(TorsoShell(0.0018f), top), mat, 1);
        }

        // ------------------------------------------------------------------ bottoms
        void Bottom(GarmentSpec g)
        {
            switch (g.Kind)
            {
                case Garment.Skirt: case Garment.PleatedSkirt: Skirt(g); return;
                case Garment.MourningDress: case Garment.LongDress: Dress(g); return;
                case Garment.WaistTiedOveralls: case Garment.Overalls: Overalls(g); return;
            }
            // trousers
            bool cargo = g.Kind == Garment.CargoPants;
            bool jeans = g.Kind == Garment.Jeans;
            bool wide = (g.Pattern ?? "").Contains("wide");
            float t = cargo ? 0.016f : 0.0075f;
            int pat = jeans ? 10 : (cargo ? 0 : 0);
            int mat = GarMat(g, pat);
            if (jeans && PatternId(g.Pattern) == 6)
                mat = Cloth(Hex(g.Color), 6, skinCol, new Vector4(0.02f, 0.1f, kneeY + 0.01f, 0), name: "RippedJeans");
            float waistband = waistY - 0.02f;
            // pelvis
            var pel = KeepBelow(TorsoShell(t), waistband);
            torso.Add(pel, mat, 3);
            // waistband + belt
            var band = KeepBelow(KeepAbove(TorsoShell(t + 0.004f), waistband - 0.032f), waistband);
            bool belt = g.Kind == Garment.Slacks || g.Kind == Garment.Pants || jeans || cargo;
            int beltMat = belt ? Glossy(new Color(0.12f, 0.09f, 0.08f), "Belt") : mat;
            torso.Add(band, beltMat, 4);
            if (belt)
            {
                float bz = FrontZ(waistband - 0.016f) + t + 0.006f;
                torso.Add(Sdf.Box(V(0, waistband - 0.016f, bz), V(0.022f, 0.016f, 0.004f), 0.002f), Metal(new Color(0.78f, 0.7f, 0.5f), "Buckle"), 9);
            }
            for (int s = 0; s < 2; s++)
            {
                HBone ul = s == 0 ? HBone.UpperLegL : HBone.UpperLegR;
                Vector3 hj = J(ul), an = J(ul + 2);
                float hem = ankleY + (cargo ? 0.045f : 0.03f);
                Sdf leg = Sdf.Offset(legSkin[s], t);
                if (wide)
                {
                    var flare = Sdf.Cone(hj + V(0, 0.02f, 0), an + V(0, -0.02f, 0.01f), P.ThighR + t + 0.006f, P.ThighR * 1.05f);
                    leg = Sdf.Union(0.03f, leg, flare);
                }
                else if (cargo)
                {
                    var baggy = Sdf.Cone(J(ul + 1) + V(0, 0.05f, 0), an + V(0, 0.06f, 0.005f), P.KneeR + t + 0.008f, P.AnkleR + t + 0.012f);
                    leg = Sdf.Union(0.04f, leg, baggy);
                }
                else
                {
                    // straight-cut trouser leg falls from the knee
                    var straight = Sdf.Cone(J(ul + 1) + V(0, 0.02f, 0), an + V(0, 0.0f, 0.004f), P.KneeR + t + 0.004f, P.AnkleR + t + 0.016f);
                    leg = Sdf.Union(0.03f, leg, straight);
                }
                leg = LegFolds(s, leg);
                leg = KeepAbove(leg, hem);
                legs[s].Add(leg, mat, 3);
                if (cargo)
                {
                    // side pocket
                    Vector3 pc = Vector3.Lerp(hj, J(ul + 1), 0.55f) + V(SX(s) * (P.ThighR + t + 0.004f), 0, 0.008f);
                    legs[s].Add(Sdf.Box(pc, V(0.012f, 0.06f, 0.05f), 0.01f), mat, 4);
                    legs[s].Add(Sdf.Box(pc + V(SX(s) * 0.004f, 0.055f, 0), V(0.012f, 0.012f, 0.053f), 0.006f), mat, 5);
                    // gathered cuff
                    legs[s].Add(KeepAbove(KeepBelow(Sdf.Offset(legSkin[s], t + 0.012f), hem + 0.03f), hem), mat, 4);
                }
            }
        }

        float FrontZ(float y)
        {
            // approximate z of the torso skin surface at x=0 for height y
            float z = 0.25f;
            for (int i = 0; i < 40; i++) { float d = torsoSkin.D(V(0, y, z)); if (Mathf.Abs(d) < 0.0005f) break; z -= d; }
            return z;
        }

        float BackZ(float y)
        {
            float z = -0.25f;
            for (int i = 0; i < 40; i++) { float d = torsoSkin.D(V(0, y, z)); if (Mathf.Abs(d) < 0.0005f) break; z += d; }
            return z;
        }

        float SideX(float y, int s)
        {
            float x = SX(s) * 0.35f;
            for (int i = 0; i < 40; i++) { float d = torsoSkin.D(V(x, y, 0)); if (Mathf.Abs(d) < 0.0005f) break; x -= SX(s) * d; }
            return x;
        }

        /// <summary>Skirt as its own part: cone shell (+ pleats / tiers), weighted to hips + thighs.</summary>
        Sdf _skirtBody;

        /// <summary>Hollow cone skirt whose envelope never dips inside the body (hips / thighs) + clearance.</summary>
        Sdf SkirtShape(float topY, float hemY, float rTop, float rHem, int pleats, float pleatDepth, float thickness, float clearance = 0.015f)
        {
            Vector3 c0 = V(0, topY, -0.008f);
            float zScale = 0.82f;
            if (_skirtBody == null) _skirtBody = Sdf.Union(0.02f, torsoSkin, legSkin[0], legSkin[1]);
            var body = _skirtBody;
            return Sdf.Func(p =>
            {
                float y = p.y;
                float t = Mathf.Clamp01((topY - y) / (topY - hemY));
                float r = Mathf.Lerp(rTop, rHem, Mathf.Pow(t, 1.25f));
                float dx = p.x - c0.x, dz = (p.z - c0.z) / zScale;
                float ang = Mathf.Atan2(dx, dz);
                float rad = Mathf.Sqrt(dx * dx + dz * dz);
                if (pleats > 0)
                {
                    float w = Mathf.Abs(Mathf.Sin(ang * pleats * 0.5f));
                    r += (w * 2f - 1f) * pleatDepth * Mathf.Lerp(0.2f, 1f, t);
                }
                float side = rad - r;                          // outside of the cone surface
                if (side < 0.06f) side = Sdf.SMin(side, body.D(p) - clearance, 0.025f);
                float shell = Mathf.Abs(side + thickness) - thickness; // hollow shell
                float cap = Mathf.Max(y - topY, hemY - y);
                return Mathf.Max(shell * 0.8f, cap);
            }, new Bounds(V(0, (topY + hemY) * 0.5f, 0), V(rHem * 2.6f + 0.1f, topY - hemY + 0.06f, rHem * 2.4f + 0.1f)));
        }

        SkinRule SkirtRule(float topY, float hemY, float legFollow)
        {
            int hips = Bi(HBone.Hips), ulL = Bi(HBone.UpperLegL), ulR = Bi(HBone.UpperLegR);
            return new SkinRule
            {
                Custom = p =>
                {
                    float t = Mathf.Clamp01((topY - p.y) / (topY - hemY));
                    float legW = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.4f)) * legFollow;
                    float side = Mathf.Clamp01(0.5f + p.x / 0.12f); // 0 = left, 1 = right
                    var l = new List<KeyValuePair<int, float>>
                    {
                        new KeyValuePair<int, float>(hips, 1f - legW),
                        new KeyValuePair<int, float>(ulL, legW * (1f - side)),
                        new KeyValuePair<int, float>(ulR, legW * side)
                    };
                    return l;
                }
            };
        }

        void Skirt(GarmentSpec g)
        {
            bool pleated = g.Kind == Garment.PleatedSkirt;
            bool layered = (g.Pattern ?? "").Contains("layered");
            int mat = GarMat(g);
            float topY = waistY + 0.01f;
            float hemY = pleated ? kneeY + (legTop - kneeY) * 0.2f : kneeY + (legTop - kneeY) * 0.42f;
            if (def.Id == "P03") hemY = kneeY - 0.02f; // student council: knee length
            float rTop = Mathf.Max(P.WaistHalfW, P.WaistHalfD) + 0.012f;
            float rHem = P.HipHalfW + 0.07f + (pleated ? 0.02f : 0.04f);
            skirtPart = new Part { Name = "Skirt", Res = 0.0045f, Skin = SkirtRule(topY, hemY, 0.55f), Keep = 0.06f, Tolerance = 0.0014f };
            if (layered)
            {
                // three tiers, alternating tint
                Color c1 = Hex(g.Color), c2 = Color.Lerp(Hex(g.Color), Color.white, 0.45f);
                float span = topY - hemY;
                for (int i = 0; i < 3; i++)
                {
                    float y0 = topY - span * (i * 0.28f);
                    float y1 = hemY - (i == 2 ? 0.0f : 0.0f) + span * (0.3f * (2 - i));
                    float rr = rHem + 0.02f * i;
                    var tier = SkirtShape(topY, y1, rTop + i * 0.004f, rr - 0.02f * (2 - i), 14, 0.008f, 0.004f);
                    skirtPart.Add(tier, Cloth(i % 2 == 0 ? c1 : c2, name: "SkirtTier" + i), 2 + i);
                }
            }
            else
                skirtPart.Add(SkirtShape(topY, hemY, rTop, rHem, pleated ? 26 : 12, pleated ? 0.009f : 0.006f, 0.0035f), mat, 2);
            // waistband on the torso
            torso.Add(KeepBelow(KeepAbove(TorsoShell(0.009f), waistY - 0.012f), topY + 0.012f), mat, 5);
            torso.Add(KeepBelow(TorsoShell(0.004f), topY), mat, 3);
            M.Parts.Add(skirtPart);
        }

        void Dress(GarmentSpec g)
        {
            // fitted bodice with a high collar + long sleeves, flared skirt to mid-calf (lace only on collar / yoke / cuffs / hem)
            int mat = Cloth(Hex(g.Color), name: "Dress");
            int lace = Cloth(Hex(g.Color), 2, new Color(0.3f, 0.29f, 0.34f), new Vector4(0.016f, 0.1f, 0, 0), name: "DressLace");
            var body = TorsoShell(0.006f);
            body = KeepBelow(body, J(HBone.Head).y + 0.01f * hh);
            torso.Add(body, mat, 3);
            // high collar with lace
            var collar = KeepAbove(KeepBelow(Sdf.Cone(V(0, neckBaseY - 0.02f, -0.012f), V(0, P.ChinPos.y - 0.012f, -0.02f), P.NeckR + 0.014f, P.NeckR + 0.012f), P.ChinPos.y - 0.008f), neckBaseY - 0.02f);
            torso.Add(Sdf.Shell(collar, 0.003f), lace, 6);
            // lace yoke over the chest
            var yoke = KeepAbove(Sdf.Offset(torsoSkin, 0.008f), shY - 0.07f);
            yoke = Sdf.Inter(yoke, Sdf.Plane(V(0, 0, 0.0f), Vector3.back));
            torso.Add(KeepBelow(yoke, neckBaseY + 0.005f), lace, 4);
            // buttons
            for (int i = 0; i < 5; i++)
            {
                float y = shY - 0.06f - i * 0.045f;
                torso.Add(Sdf.Sphere(V(0, y, FrontZ(y) + 0.009f), 0.0045f), Glossy(new Color(0.05f, 0.05f, 0.06f), "DressButton"), 9);
            }
            for (int s = 0; s < 2; s++)
            {
                arms[s].Add(SleeveClip(s, ArmShell(s, 0.005f), 1.02f), mat, 3);
                // lace cuffs
                var cuff = Sdf.Inter(SleeveClip(s, ArmShell(s, 0.009f), 1.03f), Sdf.Plane(ArmA(s) + ArmDir(s) * ArmLen(s) * 0.9f, -ArmDir(s)));
                arms[s].Add(cuff, lace, 4);
            }
            float topY = waistY + 0.02f;
            float hemY = kneeY - (kneeY - ankleY) * 0.45f;
            skirtPart = new Part { Name = "DressSkirt", Res = 0.004f, Skin = SkirtRule(topY, hemY, 0.45f) };
            skirtPart.Add(SkirtShape(topY, hemY, Mathf.Max(P.WaistHalfW, P.WaistHalfD) + 0.012f, P.HipHalfW + 0.14f, 16, 0.009f, 0.0035f), mat, 2);
            // lace hem band
            var hemBand = KeepBelow(SkirtShape(topY, hemY - 0.004f, Mathf.Max(P.WaistHalfW, P.WaistHalfD) + 0.016f, P.HipHalfW + 0.146f, 16, 0.009f, 0.0045f), hemY + 0.04f);
            skirtPart.Add(hemBand, lace, 3);
            M.Parts.Add(skirtPart);
        }

        void Overalls(GarmentSpec g)
        {
            // coverall bottoms + sleeves knotted around the waist, bib hanging at the back
            int mat = GarMat(g, 4);
            float t = 0.012f;
            torso.Add(KeepBelow(TorsoShell(t), waistY - 0.01f), mat, 3);
            for (int s = 0; s < 2; s++)
            {
                HBone ul = s == 0 ? HBone.UpperLegL : HBone.UpperLegR;
                var leg = Sdf.Union(0.03f, Sdf.Offset(legSkin[s], t), Sdf.Cone(J(ul + 1) + V(0, 0.04f, 0), J(ul + 2) + V(0, 0.07f, 0.004f), P.KneeR + t + 0.008f, P.AnkleR + t + 0.014f));
                legs[s].Add(KeepAbove(leg, ankleY + 0.08f), mat, 3);
                // knee patch
                legs[s].Add(Sdf.Ellipsoid(J(ul + 1) + V(0, 0.01f, P.KneeR + t - 0.004f), V(0.038f, 0.045f, 0.012f)), Cloth(Color.Lerp(Hex(g.Color), Color.black, 0.2f), 4, new Color(0.16f, 0.12f, 0.08f), new Vector4(0.06f, 0.1f, 0, 0), name: "Patch"), 4);
            }
            if (g.Kind == Garment.WaistTiedOveralls)
            {
                // sleeves tied around the waist: a thick twisted ring + knot + hanging sleeve ends
                float y = waistY - 0.035f;
                var ring = Sdf.Func(p =>
                {
                    Vector2 q = new Vector2(p.x / (P.WaistHalfW + 0.03f), (p.z + 0.004f) / (P.WaistHalfD + 0.03f));
                    float rr = q.magnitude;
                    float dr = (rr - 1f) * Mathf.Min(P.WaistHalfW, P.WaistHalfD);
                    float twist = Mathf.Sin(Mathf.Atan2(q.x, q.y) * 7f + p.y * 60f) * 0.003f;
                    return new Vector2(dr, p.y - y).magnitude - 0.021f - twist;
                }, new Bounds(V(0, y, 0), V(P.WaistHalfW * 2 + 0.16f, 0.08f, P.WaistHalfD * 2 + 0.16f)));
                torso.Add(ring, mat, 7);
                Vector3 knot = V(0.02f, y - 0.005f, FrontZ(y) + 0.03f);
                torso.Add(Sdf.Ellipsoid(knot, V(0.035f, 0.028f, 0.026f)), mat, 8);
                torso.Add(Sdf.Cone(knot + V(-0.01f, -0.01f, 0.004f), knot + V(-0.05f, -0.2f, 0.02f), 0.02f, 0.03f), mat, 8);
                torso.Add(Sdf.Cone(knot + V(0.01f, -0.01f, 0.004f), knot + V(0.06f, -0.17f, 0.012f), 0.019f, 0.028f), mat, 8);
                // folded bib at the back (below waist)
                float bz = BackZ(hipsY) - 0.012f;
                torso.Add(Sdf.Box(V(0, hipsY - 0.03f, bz), V(P.HipHalfW * 0.8f, 0.09f, 0.008f), 0.008f), mat, 6);
            }
            else
            {
                // bib + straps
                var bib = Sdf.Inter(KeepBelow(KeepAbove(TorsoShell(t), waistY - 0.02f), shY - 0.09f), Sdf.Plane(V(0, 0, 0), Vector3.back));
                bib = Sdf.Inter(bib, Sdf.Box(V(0, (shY + waistY) * 0.5f, 0.1f), V(0.11f, 0.3f, 0.2f), 0.02f));
                torso.Add(bib, mat, 5);
            }
        }

        // ------------------------------------------------------------------ tops
        void Top(GarmentSpec g)
        {
            int mat = GarMat(g);
            switch (g.Kind)
            {
                case Garment.Shirt: case Garment.Blouse: case Garment.WorkShirt: Shirt(g, mat); break;
                case Garment.Tshirt: Tee(g, mat); break;
                case Garment.Turtleneck: Turtle(g); break;
                case Garment.Knit: Knit(g); break;
                case Garment.Hoodie: Hoodie(g); break;
                case Garment.TankTop: Tank(g, mat); break;
                default: Shirt(g, mat); break;
            }
        }

        bool HasJacketOver => L.Wear.Any(w => IsJacket(w.Kind));

        void Shirt(GarmentSpec g, int mat)
        {
            bool blouse = g.Kind == Garment.Blouse;
            bool work = g.Kind == Garment.WorkShirt;
            float t = work ? 0.006f : 0.0045f;
            var body = KeepAbove(TorsoShell(t), hipsY - 0.04f);
            body = NeckHole(body, P.NeckR + 0.004f, neckBaseY - 0.012f, neckBaseY + 0.01f);
            torso.Add(body, mat, prioInner);
            // collar: stand + flaps
            float cy0 = neckBaseY - 0.018f, cy1 = neckBaseY + (blouse ? 0.018f : 0.03f);
            var stand = KeepAbove(KeepBelow(Sdf.Cone(V(0, cy0, -0.014f), V(0, cy1, -0.02f), P.NeckR + 0.013f, P.NeckR + 0.009f), cy1), cy0);
            var standShell = Sdf.Shell(stand, 0.003f);
            // open the collar at the front if no tie
            bool tie = Acc(Accessory.Tie) || Acc(Accessory.RibbonTie);
            if (!tie) standShell = Sdf.Sub(standShell, VWedge(cy1 + 0.02f, cy0 - 0.01f, 0.03f, 0.0f));
            torso.Add(standShell, mat, prioInner + 1);
            for (int s = 0; s < 2; s++)
            {
                // soft collar points lying on the upper chest
                float ya = neckBaseY - 0.006f, yb = neckBaseY - (blouse ? 0.045f : 0.058f);
                Vector3 a = V(SX(s) * (P.NeckR * 0.55f), ya, FrontZ(ya) + t + 0.004f);
                Vector3 b = V(SX(s) * (blouse ? 0.048f : 0.058f), yb, FrontZ(yb) + t + 0.004f);
                Vector3 dirC = (b - a).normalized;
                Vector3 nrmC = new Vector3(SX(s) * 0.15f, 0.45f, 1f).normalized;
                var flap = Sdf.Ellipsoid((a + b) * 0.5f, V(blouse ? 0.024f : 0.02f, (b - a).magnitude * 0.62f, 0.0042f), Quaternion.LookRotation(nrmC, dirC));
                torso.Add(flap, mat, prioInner + 2);
            }
            // placket + buttons
            var placket = Sdf.Inter(Sdf.Offset(torsoSkin, t + 0.0022f), Sdf.Box(V(0, (neckBaseY + hipsY) * 0.5f, 0.2f), V(0.011f, (neckBaseY - hipsY) * 0.5f, 0.2f), 0.002f));
            torso.Add(KeepBelow(placket, neckBaseY - 0.03f), mat, prioInner + 1);
            int btn = Glossy(Color.Lerp(Hex(g.Color), Color.gray, 0.3f), "ShirtButton");
            for (float y = neckBaseY - 0.05f; y > hipsY; y -= 0.075f)
                torso.Add(Sdf.Sphere(V(0, y, FrontZ(y) + t + 0.004f), 0.0038f), btn, prioInner + 3);
            if (work)
            {
                for (int s = 0; s < 2; s++)
                {
                    float y = shY - 0.1f;
                    Vector3 pc = V(SX(s) * 0.075f, y, FrontZ(y) + t + 0.003f);
                    torso.Add(Sdf.Box(pc, V(0.04f, 0.048f, 0.006f), 0.008f), mat, prioInner + 2);
                    torso.Add(Sdf.Box(pc + V(0, 0.045f, 0.004f), V(0.043f, 0.014f, 0.006f), 0.006f), mat, prioInner + 3);
                }
            }
            // sleeves
            for (int s = 0; s < 2; s++)
            {
                float end = sleevesRolled && g.Kind != Garment.Blouse ? 0.62f : 1.0f;
                if (HasJacketOver && !sleevesRolled) end = 1.02f;
                var sl = SleeveClip(s, HasJacketOver ? ArmShell(s, t) : SleeveFolds(s, ArmShell(s, t)), end);
                if (blouse) sl = Sdf.Union(0.03f, sl, SleeveClip(s, Sdf.Ellipsoid(Vector3.Lerp(ArmA(s), ArmE(s), 0.25f), V(P.UpperArmR + 0.026f, 0.07f, P.UpperArmR + 0.026f)), 0.4f));
                arms[s].Add(sl, mat, prioInner);
                Vector3 endP = ArmA(s) + ArmDir(s) * ArmLen(s) * end;
                if (sleevesRolled && g.Kind != Garment.Blouse)
                {
                    // rolled cuff bulge
                    var roll = Sdf.Inter(Sdf.Inter(ArmShell(s, t + 0.011f), Sdf.Plane(endP, ArmDir(s))), Sdf.Plane(endP - ArmDir(s) * 0.055f, -ArmDir(s)));
                    arms[s].Add(roll, mat, prioInner + 1);
                }
                else
                {
                    var cuff = Sdf.Inter(Sdf.Inter(ArmShell(s, t + 0.0035f), Sdf.Plane(endP, ArmDir(s))), Sdf.Plane(endP - ArmDir(s) * 0.045f, -ArmDir(s)));
                    arms[s].Add(cuff, mat, prioInner + 1);
                }
            }
        }

        /// <summary>Thin triangular flap (rounded) through three points.</summary>
        static Sdf Tri(Vector3 a, Vector3 b, Vector3 c, float thick, float round)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            var bb = new Bounds(a, Vector3.zero); bb.Encapsulate(b); bb.Encapsulate(c); bb.Expand(thick * 2 + round * 2 + 0.01f);
            return Sdf.Func(p =>
            {
                // distance to triangle (IQ) minus thickness
                Vector3 ba = b - a, pa = p - a, cb = c - b, pb = p - b, ac = a - c, pc = p - c;
                Vector3 nor = Vector3.Cross(ba, ac);
                float sgn = Mathf.Sign(Vector3.Dot(Vector3.Cross(ba, nor), pa)) + Mathf.Sign(Vector3.Dot(Vector3.Cross(cb, nor), pb)) + Mathf.Sign(Vector3.Dot(Vector3.Cross(ac, nor), pc));
                float d2;
                if (sgn < 2f)
                {
                    float d1 = (ba * Mathf.Clamp01(Vector3.Dot(ba, pa) / ba.sqrMagnitude) - pa).sqrMagnitude;
                    float e2 = (cb * Mathf.Clamp01(Vector3.Dot(cb, pb) / cb.sqrMagnitude) - pb).sqrMagnitude;
                    float e3 = (ac * Mathf.Clamp01(Vector3.Dot(ac, pc) / ac.sqrMagnitude) - pc).sqrMagnitude;
                    d2 = Mathf.Min(d1, Mathf.Min(e2, e3));
                }
                else { float dd = Vector3.Dot(nor, pa); d2 = dd * dd / nor.sqrMagnitude; }
                return Mathf.Sqrt(d2) - thick - round * 0f;
            }, bb);
        }

        void Tee(GarmentSpec g, int mat)
        {
            var body = KeepAbove(TorsoShell(0.005f), hipsY - 0.035f);
            body = NeckHole(body, P.NeckR + 0.012f, neckBaseY - 0.03f, neckBaseY + 0.004f);
            torso.Add(body, mat, prioInner);
            // rib collar
            var rib = KeepAbove(KeepBelow(Sdf.Offset(torsoSkin, 0.008f), neckBaseY + 0.008f), neckBaseY - 0.04f);
            rib = Sdf.Inter(rib, Sdf.Func(p => (P.NeckR + 0.03f) - new Vector2(p.x, p.z + 0.012f).magnitude, new Bounds(V(0, neckBaseY, 0), V(0.3f, 0.1f, 0.3f))));
            torso.Add(NeckHole(rib, P.NeckR + 0.012f, neckBaseY - 0.03f, neckBaseY + 0.004f), mat, prioInner + 1);
            for (int s = 0; s < 2; s++)
            {
                var sl = SleeveClip(s, Sdf.Union(0.02f, ArmShell(s, 0.006f), Sdf.Cone(ArmA(s), ArmA(s) + ArmDir(s) * 0.14f, P.UpperArmR + 0.02f, P.UpperArmR + 0.022f)), HasJacketOver ? 0.5f : 0.3f);
                arms[s].Add(sl, mat, prioInner);
            }
        }

        void Turtle(GarmentSpec g)
        {
            int mat = GarMat(g, 8);
            var body = KeepAbove(TorsoShell(0.005f), hipsY - 0.035f);
            body = KeepBelow(body, J(HBone.Head).y);
            torso.Add(body, mat, prioInner);
            // folded neck roll
            var roll = Sdf.Cone(V(0, neckBaseY - 0.02f, -0.012f), V(0, P.ChinPos.y - 0.018f, -0.02f), P.NeckR + 0.016f, P.NeckR + 0.013f);
            roll = KeepBelow(KeepAbove(roll, neckBaseY - 0.02f), P.ChinPos.y - 0.012f);
            torso.Add(roll, mat, prioInner + 1);
            for (int s = 0; s < 2; s++)
                arms[s].Add(SleeveClip(s, ArmShell(s, 0.0055f), HasJacketOver ? 1.02f : 0.98f), mat, prioInner);
        }

        void Knit(GarmentSpec g)
        {
            int mat = GarMat(g, 8);
            var body = KeepAbove(TorsoShell(0.011f), hipsY - 0.07f);
            body = NeckHole(body, P.NeckR + 0.01f, neckBaseY - 0.02f, neckBaseY + 0.008f);
            torso.Add(body, mat, prioMid);
            torso.Add(KeepBelow(KeepAbove(TorsoShell(0.015f), hipsY - 0.075f), hipsY - 0.035f), mat, prioMid + 1);
            for (int s = 0; s < 2; s++)
                arms[s].Add(SleeveClip(s, ArmShell(s, 0.01f), 1.03f), mat, prioMid);
        }

        void Hoodie(GarmentSpec g)
        {
            int mat = GarMat(g, 0);
            bool jacket = HasJacketOver;
            var body = KeepAbove(TorsoShell(0.012f), hipsY - 0.075f);
            body = NeckHole(body, P.NeckR + 0.02f, neckBaseY - 0.035f, neckBaseY + 0.01f);
            torso.Add(body, mat, prioMid - 1);
            torso.Add(KeepBelow(KeepAbove(TorsoShell(0.016f), hipsY - 0.08f), hipsY - 0.045f), mat, prioMid);
            // hood bunched behind the neck
            Vector3 hoodC = V(0, neckBaseY + 0.005f, BackZ(neckBaseY - 0.03f) - 0.03f);
            var hood = Sdf.Union(0.03f,
                Sdf.Ellipsoid(hoodC, V(0.12f, 0.065f, 0.06f)),
                Sdf.Torus(V(0, neckBaseY - 0.005f, -0.02f), Quaternion.Euler(-12f, 0, 0), P.NeckR + 0.045f, 0.026f));
            hood = Sdf.Sub(hood, Sdf.Cyl(V(0, neckBaseY - 0.2f, -0.012f), V(0, neckBaseY + 0.3f, -0.012f), P.NeckR + 0.012f, 0.01f), 0.01f);
            torso.Add(hood, mat, prioOuter + 2);
            // strings
            for (int s = 0; s < 2; s++)
            {
                float x = SX(s) * 0.032f;
                Vector3 a = V(x, neckBaseY - 0.03f, FrontZ(neckBaseY - 0.03f) + 0.018f);
                Vector3 b = V(x * 1.3f, neckBaseY - 0.18f, FrontZ(neckBaseY - 0.18f) + 0.02f);
                torso.Add(Sdf.Capsule(a, b, 0.0035f), Cloth(Color.Lerp(Hex(g.Color), Color.white, 0.6f), name: "HoodString"), prioOver + 2);
                torso.Add(Sdf.Cyl(b, b + (b - a).normalized * 0.018f, 0.0045f, 0.002f), Metal(new Color(0.8f, 0.8f, 0.82f), "Aglet"), prioOver + 2);
            }
            if (!jacket)
            {
                // kangaroo pocket
                float y = hipsY + 0.015f;
                var pocket = Sdf.Inter(Sdf.Offset(torsoSkin, 0.019f), Sdf.Box(V(0, y, 0.2f), V(0.1f, 0.055f, 0.2f), 0.02f));
                torso.Add(pocket, mat, prioMid + 1);
            }
            // team logo decal (P13)
            if ((g.Pattern ?? "") == "teamlogo") TeamLogo = new Color[] { Hex(g.Color), Hex(g.Color2, new Color(0.8f, 0.1f, 0.2f)) };
            for (int s = 0; s < 2; s++)
            {
                arms[s].Add(SleeveClip(s, ArmShell(s, 0.011f), 1.03f), mat, prioMid - 1);
                Vector3 endP = ArmA(s) + ArmDir(s) * ArmLen(s) * 1.03f;
                arms[s].Add(Sdf.Inter(Sdf.Inter(ArmShell(s, 0.008f), Sdf.Plane(endP + ArmDir(s) * 0.015f, ArmDir(s))), Sdf.Plane(endP - ArmDir(s) * 0.03f, -ArmDir(s))), mat, prioMid);
            }
        }

        public Color[] TeamLogo;

        void Tank(GarmentSpec g, int mat)
        {
            var body = KeepAbove(TorsoShell(0.004f), hipsY - 0.04f);
            body = NeckHole(body, P.NeckR + 0.03f, neckBaseY - 0.07f, neckBaseY - 0.005f);
            body = Armholes(body, 0.012f);
            torso.Add(body, mat, prioInner);
        }

        // ------------------------------------------------------------------ vest
        void Vest(GarmentSpec g)
        {
            int mat = GarMat(g);
            float t = 0.011f;
            float hem = hipsY - 0.015f;
            var body = KeepAbove(TorsoShell(t), hem - 0.03f);
            body = NeckHole(body, P.NeckR + 0.02f, neckBaseY, neckBaseY + 0.01f);
            body = Armholes(body, 0.004f);
            float apex = shY - 0.14f;
            body = Sdf.Sub(body, VWedge(neckBaseY + 0.02f, apex, 0.07f, 0.0f), 0.004f);
            // pointed front hem
            var hemCut = Sdf.Func(p => (hem - 0.03f + Mathf.Abs(p.x) * 0.35f) - p.y + (p.z < 0 ? 0.03f : 0f), new Bounds(V(0, hem, 0), V(0.6f, 0.2f, 0.6f)));
            body = Sdf.Sub(body, Sdf.Inter(hemCut, Sdf.Plane(V(0, 0, -0.02f), Vector3.back)));
            torso.Add(body, mat, prioMid);
            // back of the vest is often satin: slightly darker
            int btn = Metal(Hex(L.AccColor, new Color(0.75f, 0.6f, 0.3f)), "VestButton");
            for (int i = 0; i < 4; i++)
            {
                float y = apex - 0.012f - i * 0.045f;
                torso.Add(Sdf.Sphere(V(0.0f, y, FrontZ(y) + t + 0.004f), 0.0048f), btn, prioMid + 3);
            }
            // welt pockets
            for (int s = 0; s < 2; s++)
            {
                float y = waistY - 0.005f;
                torso.Add(Sdf.Inter(TorsoShell(t + 0.0015f), Sdf.Box(V(SX(s) * 0.08f, y, 0.2f), V(0.03f, 0.005f, 0.2f), 0.002f)), Cloth(Color.Lerp(Hex(g.Color), Color.black, 0.35f), name: "Welt"), prioMid + 2);
            }
        }

        // ------------------------------------------------------------------ jackets & coats
        void Jacket(GarmentSpec g)
        {
            var k = g.Kind;
            bool coat = k == Garment.LongCoat || k == Garment.TrenchCoat || k == Garment.FurCoat;
            bool cropped = k == Garment.CroppedJacket;
            bool tail = k == Garment.Tailcoat;
            bool fur = k == Garment.FurCoat;
            bool trench = k == Garment.TrenchCoat;
            bool denim = k == Garment.DenimJacket;
            bool bigShoulder = (g.Pattern ?? "").Contains("bigshoulder");
            int pat = fur ? 9 : denim ? 10 : 0;
            int mat = GarMat(g, pat);
            if (tail) mat = Cloth(Hex(g.Color), 3, Hex(g.Color2, new Color(0.16f, 0.16f, 0.19f)), new Vector4(0.06f, 0.1f, 0, 0), name: "Tailcoat");
            float t = fur ? 0.034f : coat ? 0.02f : 0.017f;
            float hem = cropped ? waistY - 0.01f : tail ? waistY - 0.02f : (k == Garment.SuitJacket || k == Garment.Blazer) ? legTop - 0.045f : denim ? hipsY - 0.03f : legTop - 0.03f;
            Sdf shell = TorsoShell(t);
            // structured jackets: boxier chest / squared shoulders
            if (!fur)
            {
                var box = Sdf.Box(V(0, (shY + hem) * 0.5f - 0.02f, -0.006f), V(P.ChestHalfW + t * 0.6f, (shY - hem) * 0.5f, P.ChestHalfD + t * 0.4f), 0.06f);
                shell = Sdf.Union(0.05f, shell, KeepBelow(box, shY - 0.03f));
            }
            if (fur) shell = Sdf.Displace(shell, p => Noise.Fbm(p * 45f) - 0.45f, 0.012f);
            // shoulder pads
            for (int s = 0; s < 2; s++)
            {
                float pad = bigShoulder ? 0.045f : (coat || k == Garment.SuitJacket || k == Garment.Blazer || tail) ? 0.012f : 0.004f;
                if (pad > 0.005f)
                    shell = Sdf.Union(0.03f, shell, Sdf.Ellipsoid(ArmA(s) + V(SX(s) * (bigShoulder ? 0.025f : 0.0f), 0.02f, 0), V(0.055f + pad, 0.03f + pad * 0.3f, 0.06f + pad * 0.4f)));
            }
            jacketSurf = shell;
            shell = KeepAbove(shell, hem);
            shell = NeckHole(shell, P.NeckR + (fur ? 0.03f : 0.022f), neckBaseY + 0.005f, neckBaseY + 0.03f);
            // front opening
            float apex = cropped ? waistY + 0.02f : tail ? shY - 0.17f : (coat ? shY - 0.2f : shY - 0.2f);
            float openW = fur ? 0.11f : trench ? 0.075f : 0.085f;
            var vcut = VWedge(neckBaseY + 0.03f, apex, openW, -0.01f);
            shell = Sdf.Sub(shell, vcut, 0.003f);
            // below the button: open front (coats, cropped, fur) or small split
            float openBelow = (coat || cropped || denim) ? (fur ? 0.06f : 0.035f) : 0.018f;
            var split = Sdf.Inter(Sdf.Box(V(0, (apex + hem) * 0.5f - 0.05f, 0.25f), V(openBelow, (apex - hem) * 0.5f + 0.1f, 0.25f), 0.0f), Sdf.Plane(V(0, 0, 0.0f), Vector3.back));
            if (!tail) shell = Sdf.Sub(shell, split, 0.01f);
            if (tail)
            {
                // cutaway front above the waist
                var cut = Sdf.Inter(Sdf.Func(p => (apex - 0.02f - (Mathf.Abs(p.x) - 0.02f) * 1.6f) - p.y, new Bounds(V(0, waistY, 0), V(0.6f, 0.4f, 0.6f))), Sdf.Plane(V(0, 0, 0.0f), Vector3.back));
                shell = Sdf.Sub(shell, cut, 0.004f);
            }
            torso.Add(shell, mat, prioOuter);

            // lapels / collar
            int lapelMat = mat;
            if (tail) lapelMat = Cloth(Hex(g.Color2, new Color(0.14f, 0.14f, 0.17f)), 3, new Color(0.3f, 0.28f, 0.33f), new Vector4(0.03f, 0.1f, 0, 0), name: "Lapel");
            if (fur) lapelMat = Cloth(Color.Lerp(Hex(g.Color), Color.white, 0.25f), 9, null, new Vector4(0.02f, 0.1f, 0, 0), name: "FurCollar");
            if (!denim)
                for (int s = 0; s < 2; s++) torso.Add(Lapel(s, t, apex, openW, fur ? 0.075f : trench ? 0.06f : 0.045f, fur ? 0.02f : 0.006f), lapelMat, prioOuter + 2);
            // back collar band
            var collar = KeepAbove(KeepBelow(Sdf.Cone(V(0, neckBaseY - 0.005f, -0.02f), V(0, neckBaseY + (fur ? 0.07f : 0.04f), -0.03f), P.NeckR + t + (fur ? 0.03f : 0.01f), P.NeckR + t + (fur ? 0.04f : 0.012f)), neckBaseY + (fur ? 0.07f : 0.04f)), neckBaseY - 0.01f);
            collar = Sdf.Sub(Sdf.Shell(collar, fur ? 0.012f : 0.004f), VWedge(neckBaseY + 0.2f, neckBaseY - 0.05f, 0.1f, 0.0f));
            if (fur) collar = Sdf.Displace(collar, p => Noise.Fbm(p * 60f) - 0.4f, 0.01f);
            torso.Add(collar, lapelMat, prioOuter + 1);
            if (denim)
            {
                // point collar + chest flap pockets + yellow-ish stitching feel
                for (int s = 0; s < 2; s++)
                {
                    Vector3 a = V(SX(s) * 0.02f, neckBaseY - 0.005f, FrontZ(neckBaseY - 0.02f) + t);
                    Vector3 b = V(SX(s) * 0.075f, neckBaseY - 0.07f, FrontZ(neckBaseY - 0.07f) + t + 0.002f);
                    Vector3 c = V(SX(s) * (P.NeckR + 0.04f), neckBaseY + 0.01f, 0.0f);
                    torso.Add(Tri(a, b, c, 0.003f, 0), mat, prioOuter + 2);
                    float y = shY - 0.1f;
                    Vector3 pc = V(SX(s) * 0.085f, y, FrontZ(y) + t + 0.004f);
                    torso.Add(Sdf.Box(pc + V(0, 0.03f, 0.002f), V(0.045f, 0.018f, 0.006f), 0.004f), mat, prioOuter + 3);
                    torso.Add(Sdf.Sphere(pc + V(0, 0.022f, 0.009f), 0.0045f), Metal(new Color(0.72f, 0.68f, 0.6f), "JeanButton"), prioOuter + 4);
                }
            }
            // buttons
            int btnMat = tail ? Glossy(new Color(0.08f, 0.08f, 0.1f), "JacketButton") : trench ? Glossy(new Color(0.25f, 0.18f, 0.1f), "JacketButton") : denim ? Metal(new Color(0.72f, 0.68f, 0.6f), "JeanButton") : Glossy(Color.Lerp(Hex(g.Color), Color.black, 0.5f), "JacketButton");
            if (trench)
            {
                for (int i = 0; i < 3; i++) for (int s = 0; s < 2; s++)
                {
                    float y = apex - 0.02f - i * 0.075f;
                    Vector3 bp = V(SX(s) * 0.055f + 0.02f, y, FrontZ(y) + t + 0.006f);
                    torso.Add(Sdf.Sphere(bp, 0.0065f), btnMat, prioOuter + 4);
                }
                // belt with buckle
                float by = waistY - 0.005f;
                torso.Add(KeepBelow(KeepAbove(Sdf.Offset(TorsoShell(t), 0.006f), by - 0.022f), by + 0.022f), mat, prioOuter + 3);
                torso.Add(Sdf.Box(V(0.03f, by, FrontZ(by) + t + 0.012f), V(0.022f, 0.026f, 0.004f), 0.004f), Metal(new Color(0.55f, 0.42f, 0.22f), "Buckle"), prioOuter + 5);
                // epaulettes
                for (int s = 0; s < 2; s++)
                    torso.Add(Sdf.Box(ArmA(s) + V(-SX(s) * 0.04f, 0.045f + t, -0.005f), V(0.045f, 0.004f, 0.018f), 0.004f), mat, prioOuter + 4);
            }
            else if (!fur && !denim)
            {
                int n = cropped ? 1 : (tail ? 0 : 2);
                for (int i = 0; i < n; i++)
                {
                    float y = apex - 0.01f - i * 0.07f;
                    torso.Add(Sdf.Sphere(V(0.012f, y, FrontZ(y) + t + 0.006f), 0.0062f), btnMat, prioOuter + 4);
                }
                if (!cropped && !tail && k != Garment.Blazer)
                {
                    // flap pockets
                    for (int s = 0; s < 2; s++)
                    {
                        float y = hipsY + 0.01f;
                        torso.Add(Sdf.Inter(Sdf.Offset(jacketSurf, 0.0035f), Sdf.Box(V(SX(s) * 0.1f, y, 0.2f), V(0.05f, 0.014f, 0.2f), 0.004f)), mat, prioOuter + 2);
                    }
                }
            }
            // gold trim (student council blazer)
            if (!string.IsNullOrEmpty(g.Color2) && (g.Pattern ?? "") == "trim")
            {
                int trimMat = Metal(Hex(g.Color2), "Trim");
                for (int s = 0; s < 2; s++)
                    torso.Add(LapelEdge(s, t, apex, openW), trimMat, prioOuter + 5);
                var hemTrim = KeepBelow(KeepAbove(Sdf.Offset(TorsoShell(t), 0.0025f), hem), hem + 0.007f);
                torso.Add(Sdf.Sub(hemTrim, split, 0.004f), trimMat, prioOuter + 5);
                // emblem
                float ey = shY - 0.1f;
                torso.Add(Sdf.Cyl(V(-0.085f, ey, FrontZ(ey) + t + 0.001f), V(-0.085f, ey, FrontZ(ey) + t + 0.007f), 0.017f, 0.003f), trimMat, prioOuter + 6);
            }

            // sleeves
            for (int s = 0; s < 2; s++)
            {
                float st = fur ? 0.03f : t - 0.004f;
                Sdf sl = ArmShell(s, st);
                if (!fur) sl = Sdf.Union(0.02f, sl, Sdf.Cone(ArmA(s), ArmW(s) - ArmDir(s) * 0.02f, P.UpperArmR + st + 0.004f, P.WristR + st + 0.01f));
                if (!fur) sl = SleeveFolds(s, sl, 0.0028f);
                if (fur) sl = Sdf.Displace(sl, p => Noise.Fbm(p * 45f) - 0.45f, 0.012f);
                float end = sleevesRolled ? 0.6f : (fur ? 1.0f : 0.97f);
                if (bigShoulder) end = 0.95f;
                arms[s].Add(SleeveClip(s, sl, end), mat, prioOuter);
                if (fur)
                {
                    Vector3 e = ArmA(s) + ArmDir(s) * ArmLen(s) * end;
                    arms[s].Add(Sdf.Displace(Sdf.Inter(Sdf.Inter(ArmShell(s, st + 0.014f), Sdf.Plane(e, ArmDir(s))), Sdf.Plane(e - ArmDir(s) * 0.07f, -ArmDir(s))), p => Noise.Fbm(p * 60f) - 0.4f, 0.012f), lapelMat, prioOuter + 1);
                }
                if (trench)
                {
                    Vector3 e = ArmA(s) + ArmDir(s) * ArmLen(s) * end;
                    arms[s].Add(Sdf.Inter(Sdf.Inter(ArmShell(s, st + 0.006f), Sdf.Plane(e - ArmDir(s) * 0.05f, ArmDir(s))), Sdf.Plane(e - ArmDir(s) * 0.075f, -ArmDir(s))), mat, prioOuter + 1);
                }
                if (denim)
                {
                    Vector3 e = ArmA(s) + ArmDir(s) * ArmLen(s) * end;
                    arms[s].Add(Sdf.Inter(Sdf.Inter(ArmShell(s, st + 0.004f), Sdf.Plane(e, ArmDir(s))), Sdf.Plane(e - ArmDir(s) * 0.05f, -ArmDir(s))), mat, prioOuter + 1);
                }
            }

            // long skirts of coats / tails
            if (coat) CoatSkirt(g, mat, t, fur);
            if (tail) Tails(g, mat, t);
            if (denim)
            {
                // waist band
                torso.Add(KeepBelow(KeepAbove(Sdf.Sub(TorsoShell(t + 0.004f), split, 0.004f), hem), hem + 0.04f), mat, prioOuter + 1);
            }
        }

        Sdf jacketSurf;

        Sdf Lapel(int s, float t, float apex, float openW, float width, float thick)
        {
            // strip just outside the V edge, conforming to the jacket surface
            float topY = neckBaseY + 0.03f;
            Vector3 a = V(0, apex, 0), b = V(SX(s) * openW, topY, 0);
            Vector3 dir = (b - a).normalized;
            Vector3 outN = new Vector3(SX(s) * (topY - apex), -openW, 0).normalized; // away from the opening
            var region = Sdf.Func(p =>
            {
                Vector3 q = p - a;
                float along = Vector3.Dot(q, dir);
                float across = Vector3.Dot(q, outN);
                float len = (b - a).magnitude;
                float w = width * Mathf.Clamp01(along / (len * 0.35f)) * (along > len * 0.8f ? Mathf.Lerp(1f, 0.55f, (along - len * 0.8f) / (len * 0.2f)) : 1f);
                float d1 = Mathf.Max(-across, across - w);
                float d2 = Mathf.Max(-along, along - len * 1.02f);
                return Mathf.Max(Mathf.Max(d1, d2), -p.z + 0.02f);
            }, new Bounds(V(SX(s) * 0.1f, (apex + topY) * 0.5f, 0.12f), V(0.3f, topY - apex + 0.1f, 0.3f)));
            return Sdf.Inter(Sdf.Shell(Sdf.Offset(jacketSurf, thick + 0.003f), thick), region);
        }

        Sdf LapelEdge(int s, float t, float apex, float openW)
        {
            float topY = neckBaseY + 0.03f;
            Vector3 a = V(0, apex, 0), b = V(SX(s) * openW, topY, 0);
            Vector3 dir = (b - a).normalized;
            Vector3 outN = new Vector3(SX(s) * (topY - apex), -openW, 0).normalized;
            float len = (b - a).magnitude;
            var region = Sdf.Func(p =>
            {
                Vector3 q = p - a;
                float along = Vector3.Dot(q, dir);
                float across = Vector3.Dot(q, outN);
                return Mathf.Max(Mathf.Max(Mathf.Abs(across) - 0.0035f, Mathf.Max(-along + 0.004f, along - len)), -p.z + 0.02f);
            }, new Bounds(V(SX(s) * 0.1f, (apex + topY) * 0.5f, 0.12f), V(0.3f, topY - apex + 0.1f, 0.3f)));
            return Sdf.Inter(Sdf.Shell(Sdf.Offset(jacketSurf, 0.012f), 0.003f), region);
        }

        void CoatSkirt(GarmentSpec g, int mat, float t, bool fur)
        {
            bool trench = g.Kind == Garment.TrenchCoat;
            float topY = waistY + 0.02f;
            float hemY = fur ? kneeY + 0.03f : kneeY - (kneeY - ankleY) * 0.18f;
            float rTop = Mathf.Max(P.WaistHalfW, P.WaistHalfD) + t + 0.01f;
            float rHem = P.HipHalfW + (fur ? 0.13f : 0.1f);
            var sk = SkirtShape(topY, hemY, rTop, rHem, fur ? 0 : 6, 0.006f, fur ? 0.012f : 0.0045f);
            if (fur) sk = Sdf.Displace(sk, p => Noise.Fbm(p * 40f) - 0.45f, 0.014f);
            // open front
            float gap = fur ? 0.07f : 0.03f;
            var front = Sdf.Func(p => Mathf.Max(Mathf.Abs(p.x) - (gap + (topY - p.y) * (fur ? 0.12f : 0.08f)), -p.z), new Bounds(V(0, (topY + hemY) * 0.5f, 0.2f), V(0.6f, topY - hemY + 0.1f, 0.4f)));
            sk = Sdf.Sub(sk, front, 0.006f);
            // back vent
            if (!fur) sk = Sdf.Sub(sk, Sdf.Box(V(0, hemY + (topY - hemY) * 0.2f, -0.3f), V(0.006f, (topY - hemY) * 0.22f, 0.25f), 0.0f));
            var part = new Part { Name = "CoatSkirt", Res = 0.0042f, Skin = SkirtRule(topY, hemY, 0.5f) };
            part.Add(sk, mat, 2);
            if (fur)
            {
                // fur trim along the front edges and hem
                var trim = Sdf.Inter(Sdf.Offset(sk, 0.006f), Sdf.Func(p => Mathf.Min(Mathf.Abs(Mathf.Abs(p.x) - (gap + (topY - p.y) * 0.12f)) - 0.03f, p.y - (hemY + 0.04f)), sk.B));
                part.Add(Sdf.Displace(trim, p => Noise.Fbm(p * 60f) - 0.4f, 0.012f), Cloth(Color.Lerp(Hex(g.Color), Color.white, 0.25f), 9, null, new Vector4(0.02f, 0.1f, 0, 0), name: "FurCollar"), 3);
            }
            M.Parts.Add(part);
        }

        void Tails(GarmentSpec g, int mat, float t)
        {
            float topY = waistY + 0.01f;
            float hemY = kneeY - 0.02f;
            float rTop = Mathf.Max(P.WaistHalfW, P.WaistHalfD) + t + 0.01f;
            float rHem = P.HipHalfW + 0.03f;
            var sk = SkirtShape(topY, hemY, rTop, rHem, 0, 0f, 0.004f);
            // keep only the back; split into two tails
            sk = Sdf.Inter(sk, Sdf.Func(p => p.z + 0.02f + Mathf.Abs(p.x) * 0.2f, sk.B));
            sk = Sdf.Sub(sk, Sdf.Func(p => Mathf.Abs(p.x) - 0.006f - (topY - p.y) * 0.05f, sk.B), 0.004f);
            // rounded tail tips
            sk = Sdf.Inter(sk, Sdf.Func(p => (hemY + 0.05f - Mathf.Abs(Mathf.Abs(p.x) - 0.08f) * 0.8f) - p.y + 0.05f, sk.B), 0.01f);
            int[] chainL = AddChain("TailL", "Hips", new[] { V(-0.07f, topY - 0.02f, BackZ(topY) - 0.02f), V(-0.08f, (topY + hemY) * 0.5f, BackZ(hipsY) - 0.04f), V(-0.08f, hemY, BackZ(hipsY) - 0.05f) });
            int[] chainR = AddChain("TailR", "Hips", new[] { V(0.07f, topY - 0.02f, BackZ(topY) - 0.02f), V(0.08f, (topY + hemY) * 0.5f, BackZ(hipsY) - 0.04f), V(0.08f, hemY, BackZ(hipsY) - 0.05f) });
            int hips = Bi(HBone.Hips);
            var part = new Part
            {
                Name = "Tails", Res = 0.004f,
                Skin = new SkinRule
                {
                    Custom = p =>
                    {
                        float u = Mathf.Clamp01((topY - p.y) / (topY - hemY));
                        int[] ch = p.x < 0 ? chainL : chainR;
                        var l = new List<KeyValuePair<int, float>> { new KeyValuePair<int, float>(hips, Mathf.Clamp01(1f - u * 3f)) };
                        l.Add(new KeyValuePair<int, float>(ch[0], Mathf.Clamp01(1f - Mathf.Abs(u - 0.3f) * 2.5f)));
                        l.Add(new KeyValuePair<int, float>(ch[1], Mathf.Clamp01(1f - Mathf.Abs(u - 0.85f) * 2.5f)));
                        return l;
                    }
                }
            };
            part.Add(sk, mat, 2);
            // ivory brocade lining peeking out along the inner edges
            var lining = Sdf.Inter(Sdf.Offset(sk, 0.0015f), Sdf.Func(p => Mathf.Abs(p.x) - 0.018f - (topY - p.y) * 0.05f, sk.B));
            part.Add(lining, Cloth(new Color(0.9f, 0.86f, 0.76f), 3, new Color(0.72f, 0.64f, 0.46f), new Vector4(0.04f, 0.1f, 0, 0), name: "TailLining"), 3);
            M.Parts.Add(part);
        }

        // ------------------------------------------------------------------ overlays
        void Apron(GarmentSpec g)
        {
            int mat = GarMat(g);
            int trim = Cloth(Hex(g.Color2, new Color(0.64f, 0.14f, 0.18f)), name: "ApronTrim");
            float top = shY - 0.1f, bottom = kneeY - 0.02f;
            // bib on the torso
            var bib = Sdf.Inter(Sdf.Shell(Sdf.Offset(torsoSkin, 0.02f), 0.0035f), Sdf.Func(p => Mathf.Max(Mathf.Abs(p.x) - (p.y > waistY ? 0.11f : 0.15f + (waistY - p.y) * 0.3f), Mathf.Max(p.y - top, -p.z)), new Bounds(V(0, (top + legTop) * 0.5f, 0.1f), V(0.5f, top - legTop + 0.2f, 0.4f))));
            bib = KeepAbove(bib, legTop - 0.1f);
            torso.Add(bib, mat, prioOver);
            var bibTrim = Sdf.Inter(Sdf.Shell(Sdf.Offset(torsoSkin, 0.0215f), 0.004f), Sdf.Func(p =>
            {
                float hw = p.y > waistY ? 0.11f : 0.15f + (waistY - p.y) * 0.3f;
                float edge = Mathf.Min(Mathf.Abs(Mathf.Abs(p.x) - hw) - 0.007f, Mathf.Abs(p.y - top) - 0.007f);
                return Mathf.Max(Mathf.Max(edge, Mathf.Max(Mathf.Abs(p.x) - hw - 0.007f, p.y - top - 0.007f)), -p.z);
            }, new Bounds(V(0, (top + legTop) * 0.5f, 0.1f), V(0.5f, top - legTop + 0.2f, 0.4f))));
            torso.Add(KeepAbove(bibTrim, legTop - 0.1f), trim, prioOver + 1);
            // neck strap
            torso.Add(Sdf.Capsule(V(-0.1f, top, FrontZ(top) + 0.02f), V(-P.NeckR - 0.01f, neckBaseY + 0.01f, -0.01f), 0.007f), trim, prioOver + 2);
            torso.Add(Sdf.Capsule(V(0.1f, top, FrontZ(top) + 0.02f), V(P.NeckR + 0.01f, neckBaseY + 0.01f, -0.01f), 0.007f), trim, prioOver + 2);
            // waist ties
            var tie = KeepBelow(KeepAbove(TorsoShell(0.017f), waistY - 0.012f), waistY + 0.012f);
            torso.Add(tie, trim, prioOver - 1);
            torso.Add(Sdf.Ellipsoid(V(0, waistY, BackZ(waistY) - 0.015f), V(0.03f, 0.018f, 0.012f)), trim, prioOver);
            torso.Add(Sdf.Cone(V(0.005f, waistY - 0.01f, BackZ(waistY) - 0.015f), V(0.02f, waistY - 0.16f, BackZ(hipsY) - 0.03f), 0.01f, 0.012f), trim, prioOver);
            // lower apron panel as a separate skirt part hanging over the legs
            var skirt = new Part { Name = "ApronSkirt", Res = 0.004f, Skin = SkirtRule(legTop, bottom, 0.65f) };
            var panel = Sdf.Func(p =>
            {
                float t = Mathf.Clamp01((legTop - p.y) / (legTop - bottom));
                float hw = 0.15f + (waistY - legTop) * 0.3f + t * 0.06f;
                float zf = P.HipHalfD * 0.9f + 0.035f + t * 0.03f;
                float curve = zf - (p.x * p.x) * 2.2f;
                float slab = Mathf.Abs(p.z - curve) - 0.0035f;
                return Mathf.Max(Mathf.Max(slab, Mathf.Abs(p.x) - hw), Mathf.Max(p.y - legTop - 0.02f, bottom - p.y));
            }, new Bounds(V(0, (legTop + bottom) * 0.5f, 0.1f), V(0.5f, legTop - bottom + 0.1f, 0.3f)));
            skirt.Add(panel, mat, 2);
            var panelTrim = Sdf.Func(p =>
            {
                float t = Mathf.Clamp01((legTop - p.y) / (legTop - bottom));
                float hw = 0.15f + (waistY - legTop) * 0.3f + t * 0.06f;
                float zf = P.HipHalfD * 0.9f + 0.035f + t * 0.03f + 0.0015f;
                float curve = zf - (p.x * p.x) * 2.2f;
                float slab = Mathf.Abs(p.z - curve) - 0.0045f;
                float edge = Mathf.Min(Mathf.Abs(Mathf.Abs(p.x) - hw) - 0.008f, Mathf.Abs(p.y - bottom) - 0.008f);
                return Mathf.Max(Mathf.Max(slab, edge), Mathf.Max(Mathf.Abs(p.x) - hw - 0.008f, Mathf.Max(p.y - legTop - 0.02f, bottom - 0.008f - p.y)));
            }, panel.B);
            skirt.Add(panelTrim, trim, 3);
            // pocket
            skirt.Add(Sdf.Box(V(0.06f, legTop - 0.1f, P.HipHalfD * 0.9f + 0.045f), V(0.05f, 0.045f, 0.004f), 0.005f), trim, 4);
            M.Parts.Add(skirt);
        }

        void Capelet(GarmentSpec g)
        {
            int mat = GarMat(g);
            float top = neckBaseY + 0.02f, bottom = J(HBone.LowerArmL).y + 0.03f;
            float r0 = P.NeckR + 0.02f, r1 = P.ShoulderHalfW + 0.075f;
            var cape = Sdf.Func(p =>
            {
                float t = Mathf.Clamp01((top - p.y) / (top - bottom));
                float r = Mathf.Lerp(r0, r1, Mathf.Sqrt(t));
                float rad = new Vector2(p.x, (p.z + 0.01f) / 0.85f).magnitude;
                float scallop = Mathf.Abs(Mathf.Sin(Mathf.Atan2(p.x, p.z) * 6f)) * 0.018f;
                float side = rad - r;
                float shell = Mathf.Abs(side + 0.004f) - 0.004f;
                return Mathf.Max(shell * 0.85f, Mathf.Max(p.y - top, bottom + scallop - p.y));
            }, new Bounds(V(0, (top + bottom) * 0.5f, 0), V(r1 * 2.4f + 0.1f, top - bottom + 0.08f, r1 * 2.4f + 0.1f)));
            // must clear the body: union with an offset of the torso+arms near the shoulders
            var drape = Sdf.Union(0.02f, cape, KeepAbove(Sdf.Offset(torsoSkin, 0.02f), shY - 0.03f));
            drape = Sdf.Inter(drape, Sdf.Func(p => p.y - top, cape.B));
            // front opening
            drape = Sdf.Sub(drape, Sdf.Func(p => Mathf.Max(Mathf.Abs(p.x) - 0.012f, -p.z), cape.B));
            var part = new Part { Name = "Capelet", Res = 0.0036f, Skin = SkinRule.Blend(3f, Bi(HBone.Chest), Bi(HBone.Neck), Bi(HBone.ShoulderL), Bi(HBone.ShoulderR)) };
            part.Add(drape, mat, 2);
            // round collar
            part.Add(KeepAbove(KeepBelow(Sdf.Torus(V(0, top - 0.005f, -0.01f), Quaternion.Euler(-8f, 0, 0), r0 + 0.02f, 0.012f), top + 0.02f), top - 0.03f), Cloth(Color.white, name: "CapeCollar"), 3);
            // toy buttons: colorful spheres / stars down the front
            Color[] toy = { new Color(1f, 0.45f, 0.6f), new Color(1f, 0.85f, 0.3f), new Color(0.45f, 0.7f, 1f), new Color(0.6f, 0.9f, 0.5f) };
            for (int i = 0; i < 4; i++)
            {
                float y = top - 0.03f - i * 0.035f;
                float z = r0 + 0.02f + i * 0.012f;
                part.Add(Sdf.Sphere(V(0.018f, y, z), 0.009f), Glossy(toy[i], "ToyButton" + i), 5);
            }
            M.Parts.Add(part);
        }

        // ------------------------------------------------------------------ shoes
        void Shoe(GarmentSpec g)
        {
            Color c = Hex(g.Color);
            bool glossy = g.Kind == Garment.DressShoes || g.Kind == Garment.Loafers || g.Kind == Garment.PlatformShoes || g.Kind == Garment.Boots && c.grayscale < 0.2f;
            int mat = glossy ? Glossy(c, "Shoe") : Cloth(c, name: "Shoe");
            bool sporty = g.Kind == Garment.Sneakers || g.Kind == Garment.HighTops;
            int sole = sporty ? Cloth(new Color(0.95f, 0.94f, 0.9f), name: "Sole") :
                       g.Kind == Garment.PlatformShoes ? Glossy(Color.Lerp(c, Color.white, 0.25f), "Sole") : Cloth(new Color(0.1f, 0.08f, 0.08f), name: "Sole");
            float sc = H / 1.7f;
            for (int s = 0; s < 2; s++)
            {
                HBone ft = s == 0 ? HBone.FootL : HBone.FootR;
                Vector3 an = J(ft), toe = s == 0 ? P.ToeTipL : P.ToeTipR;
                float fw = P.FootHalfW * (sporty ? 1.08f : 1f);
                float soleH = g.Kind == Garment.PlatformShoes ? 0.042f : g.Kind == Garment.Boots ? 0.02f : sporty ? 0.018f : 0.012f;
                float x = an.x;
                float heelZ = an.z - 0.035f * sc, toeZ = toe.z + (g.Kind == Garment.DressShoes ? 0.012f : 0.004f);
                float len = toeZ - heelZ;
                float top = sporty ? 0.05f : 0.042f;
                var parts = new List<Sdf>
                {
                    Sdf.Ellipsoid(V(x, soleH + top * 0.8f, heelZ + 0.045f * sc), V(fw * 0.9f, top, 0.05f * sc)),
                    Sdf.Ellipsoid(V(x, soleH + top * 0.62f, heelZ + len * 0.55f), V(fw, top * 0.85f, len * 0.36f)),
                    Sdf.Ellipsoid(V(x, soleH + top * 0.4f, toeZ - 0.045f * sc), V(fw * 0.94f, top * 0.55f, 0.052f * sc)),
                    Sdf.Cone(V(x, an.y + 0.015f, an.z - 0.008f), V(x, soleH + top * 0.8f, heelZ + len * 0.55f), P.AnkleR * 1.25f, fw * 0.75f),
                };
                Sdf shoe = Sdf.Union(0.022f, parts);
                if (g.Kind == Garment.HighTops || g.Kind == Garment.Boots)
                {
                    float shaftTop = g.Kind == Garment.Boots ? an.y + (kneeY - an.y) * (def.Id == "P15" || def.Id == "P16" ? 0.64f : 0.42f) : an.y + 0.07f;
                    shoe = Sdf.Union(0.02f, shoe, KeepBelow(Sdf.Offset(legSkin[s], g.Kind == Garment.Boots ? 0.008f : 0.011f), shaftTop));
                    if (g.Kind == Garment.HighTops)
                        feet[s].Add(KeepBelow(KeepAbove(Sdf.Offset(legSkin[s], 0.015f), shaftTop - 0.014f), shaftTop), sole, 4);
                }
                else if (g.Kind == Garment.Loafers || g.Kind == Garment.DressShoes || g.Kind == Garment.PlatformShoes)
                {
                    // low cut showing the ankle
                    shoe = Sdf.Sub(shoe, Sdf.Ellipsoid(V(x, an.y + 0.045f, an.z - 0.005f), V(fw * 0.82f, 0.04f, 0.06f * sc)), 0.008f);
                }
                shoe = KeepAbove(shoe, soleH - 0.001f);
                feet[s].Add(shoe, mat, 2);
                // sole: slab following the shoe outline
                var outline = shoe;
                float sy = soleH + 0.012f;
                var soleS = Sdf.Func(p => Mathf.Max(outline.D(new Vector3(p.x, sy, p.z)) - 0.003f, Mathf.Abs(p.y - soleH * 0.5f) - soleH * 0.5f),
                    new Bounds(V(x, soleH * 0.5f, (heelZ + toeZ) * 0.5f), V(fw * 2.6f, soleH + 0.02f, len + 0.06f)));
                if (g.Kind == Garment.DressShoes || g.Kind == Garment.Loafers || g.Kind == Garment.Boots)
                    soleS = Sdf.Union(0.004f, soleS, Sdf.Box(V(x, soleH * 1.2f, heelZ + 0.03f * sc), V(fw * 0.75f, soleH * 1.2f, 0.028f * sc), 0.004f));
                feet[s].Add(soleS, sole, 3);
                if (sporty)
                {
                    int lace = Cloth(new Color(0.96f, 0.96f, 0.96f), name: "Lace");
                    for (int i = 0; i < 4; i++)
                    {
                        float zz = heelZ + len * (0.55f - i * 0.08f);
                        float yy = soleH + top * 0.62f + top * 0.72f + i * 0.006f;
                        feet[s].Add(Sdf.Capsule(V(x - fw * 0.5f, yy, zz), V(x + fw * 0.5f, yy, zz), 0.0032f), lace, 4);
                    }
                    // toe cap
                    feet[s].Add(Sdf.Inter(Sdf.Offset(shoe, 0.0015f), Sdf.Plane(V(0, 0, toeZ - 0.045f * sc), Vector3.back)), sole, 3);
                }
                if (g.Kind == Garment.Loafers)
                    feet[s].Add(Sdf.Inter(Sdf.Offset(shoe, 0.002f), Sdf.Box(V(x, soleH + top, heelZ + len * 0.52f), V(fw * 1.2f, 0.02f, 0.01f), 0.004f)), Glossy(Color.Lerp(c, Color.black, 0.35f), "LoaferStrap"), 4);
            }
        }
    }
}
