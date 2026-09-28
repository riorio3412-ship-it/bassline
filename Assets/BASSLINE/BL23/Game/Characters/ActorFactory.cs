using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>
    /// Creates BL23 actors. Loads the baked prefab "Resources/Actors/&lt;Id&gt;" (see CharacterBaker.BakeAll);
    /// if the prefab is missing a primitive-based fallback actor with the same skeleton/API is built (never null).
    /// </summary>
    public static class ActorFactory
    {
        public static ActorRig Create(CastDef def, Transform parent)
        {
            if (def == null) def = new CastDef { Id = "UNKNOWN", Name = "?", HeightCm = 170 };
            GameObject go = null;
            var prefab = Resources.Load<GameObject>("Actors/" + def.Id);
            if (prefab != null)
            {
                go = Object.Instantiate(prefab, parent, false);
            }
            if (go == null) go = FallbackActorBuilder.Build(def, parent);
            go.name = def.Id + "_" + (def.Name ?? "");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var rig = go.GetComponent<ActorRig>();
            if (rig == null) rig = go.AddComponent<ActorRig>();
            rig.ActorId = def.Id;
            if (rig.Height <= 0f) rig.Height = def.HeightCm / 100f;
            if (def.Id == "P02" || def.Id == "P04" || def.Id == "NPC00") rig.CanBreak = true;
            rig.Init();
            if (rig.Anim == null) rig.Anim = go.GetComponent<ActorAnimator>() ?? go.AddComponent<ActorAnimator>();
            rig.Anim.Init();
            return rig;
        }

        /// <summary>Loads (and caches) prefabs for all cast members; call during a loading screen to avoid hitches.</summary>
        public static void Preload()
        {
            foreach (var c in Cast.All) Resources.Load<GameObject>("Actors/" + c.Id);
        }
    }

    /// <summary>Runtime primitive actor (used only when a baked prefab is missing).</summary>
    public static class FallbackActorBuilder
    {
        static Mesh _sphere, _capsule, _cube;

        static Mesh Prim(PrimitiveType t)
        {
            var g = GameObject.CreatePrimitive(t);
            var m = g.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(g);
            return m;
        }

        public static GameObject Build(CastDef def, Transform parent)
        {
            if (_sphere == null) { _sphere = Prim(PrimitiveType.Sphere); _capsule = Prim(PrimitiveType.Capsule); _cube = Prim(PrimitiveType.Cube); }
            var look = def.Look ?? new LookSpec();
            bool f = def.Gender == Gender.F;
            var P = BodyProportions.Make(def.HeightCm / 100f, f, look.Build, look.Shoulders, look.HeadScale);
            var root = new GameObject(def.Id);
            root.transform.SetParent(parent, false);
            var arm = new GameObject("Armature").transform;
            arm.SetParent(root.transform, false);
            var bones = new Transform[ActorSkeleton.Count];
            foreach (int i in ActorSkeleton.Order)
            {
                var t = new GameObject(ActorSkeleton.Names[i]).transform;
                int pi = ActorSkeleton.Parent[i];
                t.SetParent(pi < 0 ? arm : bones[pi], false);
                t.position = root.transform.TransformPoint(P.Joint[i]);
                bones[i] = t;
            }
            Color C(string h, Color d) { return !string.IsNullOrEmpty(h) && ColorUtility.TryParseHtmlString(h, out var c) ? c : d; }
            Color skin = C(look.Skin, new Color(0.95f, 0.84f, 0.77f));
            Color hair = C(look.HairColor, new Color(0.1f, 0.08f, 0.08f));
            Color top = new Color(0.3f, 0.3f, 0.35f), bottom = new Color(0.18f, 0.18f, 0.2f), shoe = new Color(0.1f, 0.1f, 0.1f), outer = top;
            bool hasOuter = false;
            foreach (var w in look.Wear)
            {
                switch (w.Kind)
                {
                    case Garment.Shirt: case Garment.Tshirt: case Garment.Turtleneck: case Garment.Knit: case Garment.Hoodie: case Garment.TankTop: case Garment.Blouse: case Garment.WorkShirt: case Garment.MourningDress:
                        top = C(w.Color, top); if (!hasOuter) outer = top; break;
                    case Garment.Blazer: case Garment.SuitJacket: case Garment.LongCoat: case Garment.TrenchCoat: case Garment.FurCoat: case Garment.CroppedJacket: case Garment.DenimJacket: case Garment.Tailcoat: case Garment.Vest: case Garment.Capelet: case Garment.Apron:
                        outer = C(w.Color, outer); hasOuter = true; break;
                    case Garment.Pants: case Garment.Jeans: case Garment.Slacks: case Garment.CargoPants: case Garment.Skirt: case Garment.PleatedSkirt: case Garment.WaistTiedOveralls: case Garment.Overalls:
                        bottom = C(w.Color, bottom); break;
                    case Garment.Sneakers: case Garment.HighTops: case Garment.Boots: case Garment.Loafers: case Garment.DressShoes: case Garment.PlatformShoes:
                        shoe = C(w.Color, shoe); break;
                }
            }
            var mats = new Dictionary<Color, Material>();
            Material M(Color c) { if (!mats.TryGetValue(c, out var m)) { m = ToonRuntime.Mat(c); mats[c] = m; } return m; }
            var extra = new List<Renderer>();
            void Seg(Transform b, Vector3 a, Vector3 z, float r, Color c)
            {
                var g = new GameObject("Mesh_" + b.name);
                g.transform.SetParent(b, false);
                Vector3 la = b.InverseTransformPoint(root.transform.TransformPoint(a)), lz = b.InverseTransformPoint(root.transform.TransformPoint(z));
                Vector3 d = lz - la;
                g.transform.localPosition = (la + lz) * 0.5f;
                g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.up);
                g.transform.localScale = new Vector3(r * 2f, d.magnitude * 0.5f + r, r * 2f);
                g.AddComponent<MeshFilter>().sharedMesh = _capsule;
                var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = M(c); extra.Add(mr);
            }
            void Ball(Transform b, Vector3 c, Vector3 size, Color col)
            {
                var g = new GameObject("Mesh_" + b.name);
                g.transform.SetParent(b, false);
                g.transform.position = root.transform.TransformPoint(c);
                g.transform.localScale = size;
                g.AddComponent<MeshFilter>().sharedMesh = _sphere;
                var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = M(col); extra.Add(mr);
            }
            Transform B(HBone b) => bones[(int)b];
            Vector3 J(HBone b) => P.J(b);
            Ball(B(HBone.Head), P.HeadCenter, new Vector3(0.78f, 0.95f, 0.86f) * P.HH, skin);
            Ball(B(HBone.Head), P.HeadCenter + new Vector3(0, 0.06f * P.HH, -0.04f * P.HH), new Vector3(0.84f, 0.9f, 0.9f) * P.HH, look.Hair == HairStyle.None ? skin : hair);
            for (int s = -1; s <= 1; s += 2)
                Ball(B(HBone.Head), P.HeadCenter + new Vector3(s * 0.17f * P.HH, -0.06f * P.HH, 0.39f * P.HH), new Vector3(0.09f, 0.12f, 0.03f) * P.HH, C(look.EyeColor, Color.black) * 0.6f);
            Seg(B(HBone.Neck), J(HBone.Neck), J(HBone.Head), P.NeckR, skin);
            Seg(B(HBone.Chest), J(HBone.Chest) + Vector3.down * 0.05f, J(HBone.Neck) + Vector3.down * 0.04f, P.ChestHalfW * 0.95f, outer);
            Seg(B(HBone.Spine), J(HBone.Spine), J(HBone.Chest), P.WaistHalfW * 1.05f, outer);
            Seg(B(HBone.Hips), J(HBone.Hips) + Vector3.down * 0.06f, J(HBone.Spine), P.HipHalfW * 0.95f, bottom);
            for (int s = 0; s < 2; s++)
            {
                HBone ua = s == 0 ? HBone.UpperArmL : HBone.UpperArmR;
                Seg(B(ua), J(ua), J(ua + 1), P.UpperArmR * 1.1f, outer);
                Seg(B(ua + 1), J(ua + 1), J(ua + 2), P.ElbowR * 1.1f, outer);
                Seg(B(ua + 2), J(ua + 2), s == 0 ? P.HandTipL : P.HandTipR, P.WristR * 1.1f, skin);
                HBone ul = s == 0 ? HBone.UpperLegL : HBone.UpperLegR;
                Seg(B(ul), J(ul), J(ul + 1), P.ThighR, bottom);
                Seg(B(ul + 1), J(ul + 1), J(ul + 2), P.CalfR, bottom);
                Seg(B(ul + 2), J(ul + 2) + Vector3.down * 0.02f, s == 0 ? P.ToeTipL : P.ToeTipR, P.FootHalfW * 1.1f, shoe);
            }
            var rig = root.AddComponent<ActorRig>();
            rig.ActorId = def.Id; rig.Height = P.H; rig.Female = f; rig.IsFallback = true;
            rig.Bones = bones;
            rig.Hips = B(HBone.Hips); rig.Spine = B(HBone.Spine); rig.Chest = B(HBone.Chest); rig.Neck = B(HBone.Neck); rig.Head = B(HBone.Head);
            rig.HandL = B(HBone.HandL); rig.HandR = B(HBone.HandR); rig.FootL = B(HBone.FootL); rig.FootR = B(HBone.FootR);
            rig.HandTipRestL = P.HandTipL; rig.HandTipRestR = P.HandTipR;
            rig.FingerTipRest = (Vector3[])P.FingerTip.Clone();
            rig.HandAnchorL = Anchor("HandAnchorL", B(HBone.HandL), Vector3.Lerp(J(HBone.HandL), P.HandTipL, 0.38f), root.transform, Quaternion.FromToRotation(Vector3.down, (P.HandTipL - J(HBone.HandL)).normalized));
            rig.HandAnchorR = Anchor("HandAnchorR", B(HBone.HandR), Vector3.Lerp(J(HBone.HandR), P.HandTipR, 0.38f), root.transform, Quaternion.FromToRotation(Vector3.down, (P.HandTipR - J(HBone.HandR)).normalized));
            rig.EyeAnchor = Anchor("EyeAnchor", B(HBone.Head), P.EyeCenter, root.transform, Quaternion.identity);
            rig.ChestAnchor = Anchor("ChestAnchor", B(HBone.Chest), J(HBone.Chest) + new Vector3(0, 0.08f, P.ChestHalfD), root.transform, Quaternion.identity);
            rig.HeadTopAnchor = Anchor("HeadTopAnchor", B(HBone.Head), P.HeadTop + Vector3.up * 0.01f, root.transform, Quaternion.identity);
            rig.ExtraRenderers = extra.ToArray();
            var anim = root.AddComponent<ActorAnimator>();
            anim.ArmIdleOut = 12f;
            rig.Anim = anim;
            return root;
        }

        static Transform Anchor(string n, Transform parent, Vector3 rootPos, Transform root, Quaternion rot)
        {
            var t = new GameObject(n).transform;
            t.SetParent(parent, false);
            t.position = root.TransformPoint(rootPos);
            t.rotation = root.rotation * rot;
            return t;
        }
    }
}
