using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Bakes all 19 BL23 actors into prefabs under Assets/BASSLINE/BL23/Resources/Actors/&lt;ID&gt;.prefab
    /// (meshes / materials / textures under Assets/BASSLINE/BL23/Art/Characters/Baked/&lt;ID&gt;/).
    /// Batch: Unity -batchmode -projectPath &lt;proj&gt; -executeMethod BL23.EditorTools.Characters.CharacterBaker.BakeAll -quit
    /// </summary>
    public static class CharacterBaker
    {
        public const string BakedRoot = "Assets/BASSLINE/BL23/Art/Characters/Baked";
        public const string SourceRoot = "Assets/BASSLINE/BL23/Art/Characters/Source";
        public const string PrefabRoot = "Assets/BASSLINE/BL23/Resources/Actors";

        static readonly Dictionary<string, string> Idle = new Dictionary<string, string>
        {
            { "P01", "pockets" }, { "P02", "behind" }, { "P03", "clipboard" }, { "P04", "stiff" }, { "P05", "politician" }, { "P06", "briefcase" },
            { "P07", "swagger" }, { "P08", "slouch" }, { "P09", "actor" }, { "P10", "calm" }, { "P11", "curious" }, { "P12", "cute" },
            { "P13", "crossed" }, { "P14", "lily" }, { "P15", "notebook" }, { "P16", "handOnHip" }, { "P17", "cute" }, { "P18", "thermos" }, { "NPC00", "clasp" }
        };

        [MenuItem("BL23/Characters/Bake All Actors")]
        public static void BakeAll()
        {
            EnsureHitboxLayer();
            EnsureFolders();
            EnsureDecalTemplate();
            var log = new System.Text.StringBuilder();
            foreach (var def in Cast.All)
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    Bake(def, log);
                    log.AppendLine($"[{def.Id}] baked in {sw.ElapsedMilliseconds} ms");
                }
                catch (Exception e)
                {
                    log.AppendLine($"[{def.Id}] FAILED: {e}");
                    Debug.LogException(e);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CharacterBaker] " + log);
            File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/bake_report.txt"), log.ToString());
        }

        /// <summary>Batch: -executeMethod BL23.EditorTools.Characters.CharacterBaker.BakeSome -bakeIds P03,P05</summary>
        public static void BakeSome()
        {
            EnsureHitboxLayer();
            EnsureFolders();
            EnsureDecalTemplate();
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-bakeIds");
            var ids = i >= 0 && i + 1 < args.Length ? args[i + 1].Split(',') : new[] { "P03" };
            var log = new System.Text.StringBuilder();
            foreach (var id in ids)
            {
                var def = Cast.Get(id.Trim());
                if (def == null) { log.AppendLine("unknown id " + id); continue; }
                try { var sw = System.Diagnostics.Stopwatch.StartNew(); Bake(def, log); log.AppendLine($"[{def.Id}] baked in {sw.ElapsedMilliseconds} ms"); }
                catch (Exception e) { log.AppendLine($"[{def.Id}] FAILED: {e}"); Debug.LogException(e); }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CharacterBaker] " + log);
            File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/bake_report.txt"), log.ToString());
        }

        public static void Bake(CastDef def, System.Text.StringBuilder log)
        {
            string model = def.Look?.Model ?? "PROC";
            if (model.StartsWith("GLB:") || GlbRigger.HasScan(def))
            {
                if (GlbRigger.Bake(def, log)) return;
                log.AppendLine($"[{def.Id}] GLB bake failed -> procedural fallback");
            }
            BakeProc(def, log);
        }

        /// <summary>Saved alpha-clipped toon material used as the template for runtime wound decals (keeps the shader variant in builds).</summary>
        public static void EnsureDecalTemplate()
        {
            string path = PrefabRoot + "/ToonDecal.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(Shader.Find("BL23/ToonCharacter"));
            m.SetFloat("_OutlineWidth", 0f);
            m.SetFloat("_AlphaClip", 1f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cutoff", 0.35f);
            m.SetFloat("_RimStrength", 0.1f);
            m.SetFloat("_GlossStrength", 0.12f);
            m.SetFloat("_GlossThreshold", 0.975f);
            m.SetFloat("_ShadowReceive", 0.6f);
            m.renderQueue = 2460;
            if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        }

        static void EnsureFolders()
        {
            foreach (var p in new[] { "Assets/BASSLINE/BL23/Art", "Assets/BASSLINE/BL23/Art/Characters", BakedRoot, SourceRoot, "Assets/BASSLINE/BL23/Resources", PrefabRoot })
                EnsureFolder(p);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Adds the "Hitbox" layer (prefers index 8) to the TagManager if it doesn't exist.</summary>
        public static void EnsureHitboxLayer()
        {
            if (LayerMask.NameToLayer("Hitbox") >= 0) return;
            var tm = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tm == null || tm.Length == 0) return;
            var so = new SerializedObject(tm[0]);
            var layers = so.FindProperty("layers");
            if (layers == null) return;
            int slot = -1;
            if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(8).stringValue)) slot = 8;
            else for (int i = 8; i < layers.arraySize; i++) if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) { slot = i; break; }
            if (slot < 0) return;
            layers.GetArrayElementAtIndex(slot).stringValue = "Hitbox";
            so.ApplyModifiedProperties();
        }

        // ================================================================ procedural
        static void BakeProc(CastDef def, System.Text.StringBuilder log)
        {
            string dir = BakedRoot + "/" + def.Id;
            EnsureFolder(dir);
            var pb = new ProcBuilder(def);
            if (def.Look.Acc.Contains(Accessory.AquariumHead)) pb.Aquarium = true;
            pb.IdleStyle = Idle.TryGetValue(def.Id, out var st) ? st : "";
            var M = pb.Build();
            var mesh = M.Build(def.Id + "_Body");
            log.Append(M.Log);

            // textures
            Texture2D atlas = null, fx = null;
            if (!pb.Aquarium)
            {
                atlas = SaveTex(pb.Face.PaintAtlas(), dir + "/" + def.Id + "_FaceAtlas.png", 2048);
                fx = SaveTex(pb.Face.PaintFx(), dir + "/" + def.Id + "_FaceFx.png", 1024);
            }
            Texture2D logo = null;
            if (pb.TeamLogo != null) logo = SaveTex(PaintLogo(pb.TeamLogo), dir + "/" + def.Id + "_Logo.png", 512);

            // materials
            var mats = new Material[M.Mats.Count];
            for (int i = 0; i < M.Mats.Count; i++)
            {
                var spec = M.Mats[i];
                if (logo != null && spec.Name == "Cloth" && Hex(def.Look.Wear.First(w => w.Kind == Garment.Hoodie).Color) == spec.Color)
                {
                    spec.Decal = logo;
                    float cy = pb.P.J(HBone.Chest).y + 0.05f;
                    spec.DecalRect = new Vector4(-0.075f, cy, 0.045f, 0.045f);
                }
                mats[i] = MakeMat(spec, dir + "/" + def.Id + "_M" + i.ToString("00") + "_" + spec.Name + ".mat", atlas, fx, pb);
            }
            mesh = SaveMesh(mesh, dir + "/" + def.Id + "_Body.asset");

            // prefab
            SkinnedMeshRenderer lodPending = null;
            var root = new GameObject(def.Id);
            try
            {
                var rig = root.AddComponent<ActorRig>();
                var boneT = BuildBones(root.transform, M.Bones);
                var smrGo = new GameObject("Body");
                smrGo.transform.SetParent(root.transform, false);
                var smr = smrGo.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = boneT;
                smr.rootBone = boneT[0];
                smr.sharedMaterials = mats;
                smr.updateWhenOffscreen = false;
                smr.localBounds = new Bounds(new Vector3(0, 0, 0), new Vector3(2.2f, 2.6f, 2.2f));
                mesh.bindposes = boneT.Select(b => b.worldToLocalMatrix * root.transform.localToWorldMatrix).ToArray();
                EditorUtility.SetDirty(mesh);

                FillRig(rig, def, boneT, pb.P, pb.IdleStyle);
                rig.Skins = new[] { smr };
                lodPending = smr;
                if (!pb.Aquarium)
                {
                    var face = root.AddComponent<ActorFace>();
                    face.FaceRenderer = smr;
                    face.FaceMaterialIndex = Array.FindIndex(M.Mats.ToArray(), m => m.Face);
                    rig.Face = face;
                }
                var anim = root.AddComponent<ActorAnimator>();
                anim.ArmIdleOut = pb.P.ArmRestAngle + 1.5f + (def.Look.Wear.Any(w => w.Kind == Garment.FurCoat) ? 6f : 0f) + (def.Look.Wear.Any(w => w.Kind == Garment.LongCoat || w.Kind == Garment.TrenchCoat) ? 2f : 0f);
                rig.Anim = anim;
                // spring chains
                foreach (var chainRoot in pb.Chains) AddChain(root.transform, boneT, M.Bones, chainRoot);
                // held props
                var extra = new List<Renderer>();
                foreach (var h in pb.Held)
                {
                    var anchor = h.Anchor == "HandAnchorL" ? rig.HandAnchorL : rig.HandAnchorR;
                    var pm = new CharMesher { Mats = M.Mats.ToList(), Bones = new List<BoneDef> { new BoneDef { Name = "Root", Parent = -1 } } };
                    // props are meshed in anchor space; they may add new materials
                    pm.Parts.Add(h.Part);
                    var propMesh = pm.Build(def.Id + "_Prop_" + h.Name);
                    var propMats = new Material[pm.Mats.Count];
                    for (int i = 0; i < pm.Mats.Count; i++)
                        propMats[i] = i < mats.Length ? mats[i] : MakeMat(pm.Mats[i], dir + "/" + def.Id + "_P" + h.Name + i + ".mat", null, null, pb);
                    // drop empty submeshes
                    propMesh = CompactSubmeshes(propMesh, ref propMats);
                    propMesh.boneWeights = null;
                    propMesh = SaveMesh(propMesh, dir + "/" + def.Id + "_Prop_" + h.Name + ".asset");
                    var go = new GameObject("Prop_" + h.Name);
                    go.transform.SetParent(anchor, false);
                    go.AddComponent<MeshFilter>().sharedMesh = propMesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = propMats;
                    extra.Add(mr);
                }
                // aquarium head
                if (pb.Aquarium)
                {
                    var aq = AquariumBuilder.Build(rig, pb, dir);
                    rig.Aquarium = aq;
                    extra.AddRange(aq.GetComponentsInChildren<Renderer>().Where(r => r.sharedMaterial != null && r.sharedMaterial.shader.name == "BL23/ToonCharacter"));
                }
                rig.ExtraRenderers = extra.ToArray();
                rig.CanBreak = def.Id == "P02" || def.Id == "P04" || def.Id == "NPC00";
                if (lodPending != null)
                {
                    var lod1 = CharacterBakerLod.AddLod(root, lodPending, 0.3f, false, dir + "/" + def.Id + "_Body_LOD1.asset");
                    rig.Skins = new[] { lodPending, lod1 };
                }
                string path = PrefabRoot + "/" + def.Id + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine($"[{def.Id}] prefab {path}: {mesh.vertexCount} verts, {mats.Length} mats");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static Color Hex(string h) => ProcBuilder.Hex(h, Color.gray);

        /// <summary>Tails of the extended bones (upper chest, thumb / index / finger group).</summary>
        public static void ExtendedTails(List<BoneDef> bones, BodyProportions P)
        {
            if (bones.Count < ActorSkeleton.Count) return;
            bones[(int)HBone.UpperChest].Tail = P.J(HBone.Neck);
            bones[(int)HBone.Chest].Tail = P.J(HBone.UpperChest);
            for (int s = 0; s < 2; s++)
                for (int d = 0; d < 5; d++)
                    for (int k = 0; k < 3; k++)
                    {
                        int b = (int)ActorSkeleton.Digit(s == 0, d, k);
                        bones[b].Tail = k < 2 ? P.Joint[b + 1] : P.FingerTip[s * 5 + d];
                    }
        }

        public static Transform[] BuildBones(Transform root, List<BoneDef> bones)
        {
            var arm = new GameObject("Armature").transform;
            arm.SetParent(root, false);
            var t = new Transform[bones.Count];
            for (int i = 0; i < bones.Count; i++) t[i] = new GameObject(bones[i].Name).transform;
            // parents may come after their children in the list (the upper chest was appended to the skeleton)
            var placed = new bool[bones.Count];
            int left = bones.Count;
            while (left > 0)
            {
                int before = left;
                for (int i = 0; i < bones.Count; i++)
                {
                    if (placed[i]) continue;
                    int p = bones[i].Parent;
                    if (p >= 0 && !placed[p]) continue;
                    t[i].SetParent(p < 0 ? arm : t[p], false);
                    t[i].position = root.TransformPoint(bones[i].Pos);
                    t[i].rotation = root.rotation;
                    placed[i] = true; left--;
                }
                if (left == before) throw new Exception("bone hierarchy has a cycle");
            }
            return t;
        }

        public static void FillRig(ActorRig rig, CastDef def, Transform[] b, BodyProportions P, string idle)
        {
            rig.ActorId = def.Id;
            rig.Height = def.HeightCm / 100f;
            rig.Female = def.Gender == Gender.F;
            rig.IdleStyle = idle;
            rig.Bones = new Transform[ActorSkeleton.Count];
            for (int i = 0; i < ActorSkeleton.Count; i++) rig.Bones[i] = b.FirstOrDefault(x => x != null && x.name == ActorSkeleton.Names[i]);
            Transform B(HBone x) => rig.Bones[(int)x];
            rig.Hips = B(HBone.Hips); rig.Spine = B(HBone.Spine); rig.Chest = B(HBone.Chest); rig.Neck = B(HBone.Neck); rig.Head = B(HBone.Head);
            rig.HandL = B(HBone.HandL); rig.HandR = B(HBone.HandR); rig.FootL = B(HBone.FootL); rig.FootR = B(HBone.FootR);
            rig.HandTipRestL = P.HandTipL; rig.HandTipRestR = P.HandTipR;
            rig.FingerTipRest = (Vector3[])P.FingerTip.Clone();
            var root = rig.transform;
            Transform Anchor(string n, Transform parent, Vector3 pos, Quaternion rot)
            {
                var t = new GameObject(n).transform;
                t.SetParent(parent, false);
                t.position = root.TransformPoint(pos);
                t.rotation = root.rotation * rot;
                return t;
            }
            for (int s = 0; s < 2; s++)
            {
                HBone h = s == 0 ? HBone.HandL : HBone.HandR;
                Vector3 w = P.J(h), tip = s == 0 ? P.HandTipL : P.HandTipR;
                Vector3 d = (tip - w).normalized;
                Quaternion O = Quaternion.FromToRotation(Vector3.down, d);
                float pn = s == 0 ? 1f : -1f;
                Vector3 palm = w + O * new Vector3(0.012f * pn, -P.HandLen * 0.42f, 0.004f);
                var a = Anchor(s == 0 ? "HandAnchorL" : "HandAnchorR", B(h), palm, O);
                if (s == 0) rig.HandAnchorL = a; else rig.HandAnchorR = a;
            }
            rig.EyeAnchor = Anchor("EyeAnchor", B(HBone.Head), P.EyeCenter, Quaternion.identity);
            rig.FingerTipAnchors = new Transform[10];
            for (int s = 0; s < 2; s++)
                for (int d = 0; d < 5; d++)
                {
                    var fb = rig.Bones[(int)ActorSkeleton.Digit(s == 0, d, 2)];
                    if (fb != null) rig.FingerTipAnchors[s * 5 + d] = Anchor((s == 0 ? "TipL" : "TipR") + d, fb, P.FingerTip[s * 5 + d], Quaternion.identity);
                }
            rig.ChestAnchor = Anchor("ChestAnchor", B(HBone.Chest), P.J(HBone.Chest) + new Vector3(0, 0.06f, P.ChestHalfD + 0.02f), Quaternion.identity);
            rig.HeadTopAnchor = Anchor("HeadTopAnchor", B(HBone.Head), P.HeadTop + Vector3.up * 0.015f, Quaternion.identity);
        }

        static void AddChain(Transform root, Transform[] boneT, List<BoneDef> bones, string chainRoot)
        {
            int start = bones.FindIndex(x => x.Name == chainRoot);
            if (start < 0) return;
            string prefix = chainRoot.Substring(0, chainRoot.Length - 2);
            var list = new List<Transform>();
            BoneDef last = null;
            for (int i = start; i < bones.Count; i++)
                if (bones[i].Name.StartsWith(prefix + "_")) { list.Add(boneT[i]); last = bones[i]; }
            if (list.Count == 0) return;
            var sc = list[0].gameObject.AddComponent<SpringChain>();
            sc.Bones = list.ToArray();
            sc.TailLocal = last.Tail - last.Pos;
            ChainSetup.ConfigureProc(sc, prefix);   // charpolish step 0: tuning lives in ChainSetup.cs (physics implementer)
        }

        // ================================================================ assets
        public static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = Path.GetFileNameWithoutExtension(path);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            mesh.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static Texture2D SaveTex(Texture2D tex, string path, int maxSize, bool srgb = true, bool normal = false)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = srgb;
            imp.alphaIsTransparency = !normal;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = maxSize;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.anisoLevel = 4;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Material MakeMat(MatSpec s, string path, Texture2D atlas, Texture2D fx, ProcBuilder pb)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(Shader.Find("BL23/ToonCharacter"));
            else m.shader = Shader.Find("BL23/ToonCharacter");
            m.name = Path.GetFileNameWithoutExtension(path);
            m.SetColor("_BaseColor", s.Color);
            m.SetTexture("_BaseMap", s.BaseMap);
            Color shade = s.Shade ?? new Color(0.66f, 0.58f, 0.74f);
            Color shade2 = s.Shade2 ?? new Color(0.46f, 0.39f, 0.55f);
            m.SetColor("_ShadeColor", shade);
            m.SetColor("_Shade2Color", shade2);
            m.SetFloat("_UseVertexColor", s.UseVertexColor ? 1f : 0f);
            m.SetFloat("_CavityStrength", s.Cavity);
            m.SetFloat("_GlossStrength", s.Gloss);
            m.SetFloat("_GlossThreshold", s.GlossThreshold);
            m.SetFloat("_HairRing", s.HairRing);
            m.SetFloat("_SkinSSS", s.SkinSSS >= 0f ? s.SkinSSS : (s.Name == "Skin" || s.Face ? 0.55f : 0f));
            m.SetFloat("_MaskMode", s.VertexMasks ? 1f : 0f);
            m.SetFloat("_ShadeSat", 0.55f);
            m.SetFloat("_OutlineTint", 0.45f);
            m.SetFloat("_RimStrength", s.Rim);
            m.SetFloat("_PatternType", s.Pattern);
            m.SetColor("_PatternColor", s.PatternColor);
            m.SetVector("_PatternParams", s.PatternParams);
            Color baseC = s.Color;
            if (s.Name == "Hair") baseC = pb != null ? Hex(pb.HairColorHex) : baseC;
            Color oc = s.Outline ?? new Color(baseC.r * 0.28f + 0.02f, baseC.g * 0.22f + 0.015f, baseC.b * 0.28f + 0.03f);
            if (s.Face) oc = new Color(0.36f, 0.2f, 0.2f);
            m.SetColor("_OutlineColor", oc);
            m.SetFloat("_OutlineWidth", s.OutlineWidth);
            m.SetFloat("_Emission", s.Emission);
            m.SetColor("_BreakColor", new Color(0.62f, 0.06f, 0.28f));
            m.SetColor("_EmissionColor", s.EmissionColor);
            m.SetFloat("_Cull", s.DoubleSided ? 0f : 2f);
            if (s.Decal != null)
            {
                m.SetTexture("_DecalTex", s.Decal);
                m.SetFloat("_DecalOn", 1f);
                m.SetVector("_DecalRect", s.DecalRect);
            }
            else m.SetFloat("_DecalOn", 0f);
            if (s.Face && atlas != null)
            {
                m.EnableKeyword("_FACE");
                m.SetFloat("_FaceOn", 1f);
                m.SetTexture("_FaceAtlas", atlas);
                m.SetTexture("_FaceFx", fx);
                m.SetVector("_FaceEye", pb.FaceEye);
                m.SetVector("_FaceBrow", pb.FaceBrow);
                m.SetVector("_FaceMouth", pb.FaceMouth);
                m.SetVector("_FaceFxRect", new Vector4(0.5f, 0.5f, 0.5f, 0.5f));
            }
            else { m.DisableKeyword("_FACE"); m.SetFloat("_FaceOn", 0f); }
            if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            return m;
        }

        static Mesh CompactSubmeshes(Mesh mesh, ref Material[] mats)
        {
            var keepTris = new List<int[]>(); var keepMats = new List<Material>();
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                var tri = mesh.GetTriangles(i);
                if (tri.Length == 0) continue;
                keepTris.Add(tri); keepMats.Add(mats[i]);
            }
            mesh.subMeshCount = keepTris.Count;
            for (int i = 0; i < keepTris.Count; i++) mesh.SetTriangles(keepTris[i], i);
            mats = keepMats.ToArray();
            return mesh;
        }

        static Texture2D PaintLogo(Color[] cols)
        {
            int W = 256;
            var t = new Texture2D(W, W, TextureFormat.RGBA32, false);
            var px = new Color[W * W];
            Color red = cols.Length > 1 ? cols[1] : Color.red;
            for (int y = 0; y < W; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W * 2 - 1, v = y / (float)W * 2 - 1;
                    Color c = Color.clear;
                    // shield
                    float shield = Mathf.Max(Mathf.Abs(u) - 0.78f, v - 0.8f);
                    shield = Mathf.Max(shield, (Mathf.Abs(u) * 0.9f - (v + 0.95f) * 0.8f));
                    if (shield < 0) c = new Color(0.1f, 0.1f, 0.12f, 1);
                    if (shield < 0 && shield > -0.06f) c = red;
                    // lightning bolt
                    Vector2 p = new Vector2(u, v);
                    bool bolt = (p.y > -0.1f && p.y < 0.6f && p.x > -0.15f + (0.6f - p.y) * 0.3f - 0.25f && p.x < 0.1f + (0.6f - p.y) * 0.3f - 0.25f) ||
                                (p.y > -0.6f && p.y <= -0.02f && p.x > -0.05f - (p.y + 0.1f) * 0.35f && p.x < 0.2f - (p.y + 0.1f) * 0.35f);
                    if (bolt && shield < -0.06f) c = red;
                    if (v > 0.58f && v < 0.72f && Mathf.Abs(u) < 0.6f && shield < 0) c = Color.white;
                    px[y * W + x] = c;
                }
            t.SetPixels(px); t.Apply();
            return t;
        }
    }
}
