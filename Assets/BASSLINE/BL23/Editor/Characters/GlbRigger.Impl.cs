using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// GLB auto-rigger: normalises the scan (height, feet on y=0, facing +Z), detects body landmarks from
    /// slice/width profiles, lowers T-pose arms with a bend deformer, builds the common skeleton, computes
    /// region-constrained distance skin weights (+ smoothing), decimates, and bakes a toon-shaded prefab.
    /// </summary>
    public static partial class GlbRigger
    {
        static readonly Dictionary<string, string> Files = new Dictionary<string, string>
        {
            { "GLB:minhyuk", "Midnight_Operative_ShortNose_CleanFace.glb" },
            { "GLB:jinwoo", "Midnight_Sentinel_160cm_Nose.glb" },
            { "GLB:doyun", "Midnight_Formal_InnerFringe_Fix.glb" },
        };

        /// <summary>Scans assigned by actor id (models provided after the cast data was written).</summary>
        static readonly Dictionary<string, string> ById = new Dictionary<string, string>
        {
            { "P05", "P05_CrimsonGentleman.glb" },
            // motion track 2026-09-27: capes cut off in Blender (BL23Lab/blender_scripts/cape_fix.py; originals kept next to them)
            { "P06", "P06_Distributor.glb" }, { "P10", "P10_Junseo.glb" },
            // (Blender cape-free versions: BL23Lab/glb_fixed/P06|P10/*_fixed.glb; not used yet: the bake grabs P06 side locks into the
            // arms and leaves P10's uncovered shoulders grey / bulky, see CharacterPipeline_Current.md)
        };
        public static bool HasScan(CastDef def) => def != null && (ById.ContainsKey(def.Id) || (def.Look != null && Files.ContainsKey(def.Look.Model ?? "")));
        static string ScanFile(CastDef def) => ById.TryGetValue(def.Id, out var f) ? f : Files.TryGetValue(def.Look?.Model ?? "", out var g) ? g : null;
        static readonly Dictionary<string, string> GlbIdle = new Dictionary<string, string> { { "P01", "pockets" }, { "P02", "behind" }, { "P04", "stiff" }, { "P05", "politician" }, { "P06", "calm" }, { "P10", "calm" } };

        public sealed class Landmarks
        {
            public float H, CrotchY, HipY, KneeY, AnkleY, NeckY, ChinY, ShoulderY, WaistY, ChestY;
            public float HipX, TorsoHalfW, TorsoHalfD, ShoulderX;
            public Vector3 ShoulderL, ShoulderR, ElbowL, ElbowR, WristL, WristR, TipL, TipR, ToeL, ToeR, AnkleL, AnkleR, KneeL, KneeR, HipL, HipR;
            public Vector3 HeadCenter, EyeCenter;
            public float ArmAngleL, ArmAngleR; // degrees from vertical
            public string Report = "";
        }

        /// <summary>LOD0 triangle budget of a scanned actor (the dense AI-generated source is decimated to this).</summary>
        public const int GlbTriBudget = 48000;

        public sealed class Scan
        {
            public GlbFile Glb;
            public List<Vector3> V, N; public List<Vector2> UV; public List<int> T, TM;
            public List<(Vector3[] v, Vector3[] n, int[] t)> Studs;
            public GlbAnalysis An; public Landmarks LM; public float H;
        }

        /// <summary>Loads a GLB scan, converts to Unity space, normalises height / feet / centre and detects landmarks.</summary>
        public static Scan LoadScan(CastDef def, System.Text.StringBuilder log)
        {
            var file = ScanFile(def); if (file == null) return null;
            string src = CharacterBaker.SourceRoot + "/" + file;
            string full = Path.GetFullPath(src);
            if (!File.Exists(full)) { log.AppendLine($"[{def.Id}] missing {src}"); return null; }
            var glb = GlbFile.Load(full);
            float H = def.HeightCm / 100f;

            // ---------------------------------------------------------------- gather + convert (glTF RH -> Unity LH: x -> -x)
            var mainPrims = glb.Prims.Where(p => !p.MeshName.StartsWith("Stud")).ToList();
            var studPrims = glb.Prims.Where(p => p.MeshName.StartsWith("Stud")).ToList();
            var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>(); var T = new List<int>(); var TM = new List<int>();
            foreach (var p in mainPrims)
            {
                int b = V.Count;
                for (int i = 0; i < p.Pos.Length; i++)
                {
                    V.Add(new Vector3(-p.Pos[i].x, p.Pos[i].y, p.Pos[i].z));
                    N.Add(p.Nrm != null ? new Vector3(-p.Nrm[i].x, p.Nrm[i].y, p.Nrm[i].z) : Vector3.up);
                    UV.Add(p.UV != null ? new Vector2(p.UV[i].x, 1f - p.UV[i].y) : Vector2.zero);
                }
                for (int i = 0; i < p.Idx.Length; i += 3)
                {
                    T.Add(b + p.Idx[i]); T.Add(b + p.Idx[i + 2]); T.Add(b + p.Idx[i + 1]);
                    TM.Add(Math.Max(0, p.Material));
                }
            }
            // normalise: height, feet at 0, centre
            float minY = V.Min(v => v.y), maxY = V.Max(v => v.y);
            float scale = H / (maxY - minY);
            for (int i = 0; i < V.Count; i++) V[i] = new Vector3(V[i].x * scale, (V[i].y - minY) * scale, V[i].z * scale);
            var pel = V.Where(v => v.y > H * 0.45f && v.y < H * 0.56f && Mathf.Abs(v.x) < 0.25f).ToList();
            float cx = pel.Count > 0 ? (pel.Min(v => v.x) + pel.Max(v => v.x)) * 0.5f : 0f;
            float cz = pel.Count > 0 ? (pel.Min(v => v.z) + pel.Max(v => v.z)) * 0.5f : 0f;
            for (int i = 0; i < V.Count; i++) V[i] -= new Vector3(cx, 0, cz);
            var studs = new List<(Vector3[] v, Vector3[] n, int[] t)>();
            foreach (var p in studPrims)
            {
                var sv = p.Pos.Select(q => new Vector3(-q.x * scale - cx, (q.y - minY) * scale, q.z * scale - cz)).ToArray();
                var sn = (p.Nrm ?? p.Pos).Select(q => new Vector3(-q.x, q.y, q.z).normalized).ToArray();
                var st = new int[p.Idx.Length];
                for (int i = 0; i < p.Idx.Length; i += 3) { st[i] = p.Idx[i]; st[i + 1] = p.Idx[i + 2]; st[i + 2] = p.Idx[i + 1]; }
                studs.Add((sv, sn, st));
            }

            // ---------------------------------------------------------------- landmarks
            var an0 = GlbAnalysis.Run(V, T, H);
            GlbFaceCalib.Apply(def.Id, an0.LM, V);
            log.AppendLine($"[{def.Id}] landmarks: {an0.LM.Report}");
            return new Scan { Glb = glb, V = V, N = N, UV = UV, T = T, TM = TM, Studs = studs, An = an0, LM = an0.LM, H = H };
        }

        static bool BakeImpl(CastDef def, System.Text.StringBuilder log)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var scan = LoadScan(def, log);
            if (scan == null) return false;
            string dir = CharacterBaker.BakedRoot + "/" + def.Id;
            CharacterBaker.EnsureFolder(dir);
            var glb = scan.Glb; var V = scan.V; var N = scan.N; var UV = scan.UV; var T = scan.T; var TM = scan.TM; var studs = scan.Studs;
            var an = scan.An; var lm = scan.LM; float H = scan.H;
            // texture fixes are painted on the full-resolution source surface (before decimation / reposing)
            Debug.Log($"[{def.Id}] t={sw.ElapsedMilliseconds} texture fixes");
            var fixedTex = GlbFix.Texture(def, scan, log);
            Debug.Log($"[{def.Id}] t={sw.ElapsedMilliseconds} texture fixes done");
            GlbFix.AddAccessories(def, scan, fixedTex, log);
            string cacheKey = ScanFile(def) + "@" + GlbTriBudget + "c1" + (GlbFix.CutsFringe(def.Id) ? "f" + GlbFix.GeometryVersion : "");

            float S = H / 1.75f;
            // ---------------------------------------------------------------- decimate first (lock UV-seam / open-border vertices), cached per source file
            int nt0 = T.Count / 3;
            int[] triMat;
            if (!GlbCache.TryLoad(cacheKey, nt0, out var cV, out var cN, out var cUV, out var cT, out triMat))
            {
                T = new List<int>(T); var tmCopy = TM.ToArray();
                GlbFix.CutFringe(def, scan, V, UV, T, ref tmCopy, fixedTex, log);   // full resolution: clean strand edges
                Debug.Log($"[{def.Id}] t={sw.ElapsedMilliseconds} cleanup start");
                GlbCleanup.RemoveFloaters(V, T, ref tmCopy, 0.06f * S, 0.004f, log, def.Id);
                Debug.Log($"[{def.Id}] t={sw.ElapsedMilliseconds} cleanup done, decimating {T.Count / 3}");
                TM = tmCopy.ToList();
                var locked = BorderVertices(T, V.Count);
                triMat = TM.ToArray();
                int target = Math.Min(GlbTriBudget, Math.Max(30000, (int)(nt0 * 0.2f)));
                var remap = MeshDecimator.Decimate(V, ref T, ref triMat, target, 40.0 * 0.0005 * 0.0005, locked, 30f);
                V = MeshDecimator.Remap(V, remap); N = MeshDecimator.Remap(N, remap); UV = MeshDecimator.Remap(UV, remap);
                Debug.Log($"[{def.Id}] t={sw.ElapsedMilliseconds} decimated to {T.Count / 3}");
                GlbCache.Save(cacheKey, nt0, V, N, UV, T, triMat);
            }
            else { V = cV; N = cN; UV = cUV; T = cT; }
            log.AppendLine($"[{def.Id}] GLB tris {nt0} -> {T.Count / 3}, verts {V.Count}");
            GlbFix.LiftFringe(def, scan, V, UV, fixedTex, log);

            // ---------------------------------------------------------------- original-pose skeleton (torso / leg weights)

            log.AppendLine($"[{def.Id}]   t={sw.ElapsedMilliseconds} ms before: weld map (UV seams split vertices)");
            // ---------------------------------------------------------------- weld map (UV seams split vertices)
            var weld = Weld(V, 0.0004f, out int weldCount);
            var adj = WeldAdjacency(T, weld, weldCount);

            log.AppendLine($"[{def.Id}]   t={sw.ElapsedMilliseconds} ms before: arms: membership, weights (original pose)");
            // ---------------------------------------------------------------- arms: membership, weights (original pose)
            var arms = GlbArms.Classify(V, weld, adj, weldCount, an, lm, log, def.Id);
            var bones0 = MakeBones(ToProportions(lm, def));
            var W = new BoneWeight[V.Count];
            Parallel.For(0, V.Count, i => W[i] = arms.Side[i] >= 0 ? GlbArms.ArmWeight(arms, i, S, V[i]) : WeighGlb2(bones0, an, V[i], false));
            W = SmoothWeights(W, weld, adj, weldCount, 6);
            {
                var Nn = N;
                System.Func<int, Color> colF = i =>
                {
                    float sh = 0.45f + 0.55f * Mathf.Clamp01(Vector3.Dot(Nn[i].normalized, new Vector3(0.3f, 0.5f, 0.8f).normalized));
                    Color c = arms.Side[i] == 0 ? new Color(1f, 0.3f, 0.3f) : arms.Side[i] == 1 ? new Color(0.3f, 0.5f, 1f) : arms.ShoulderCap[i] > 0 ? new Color(1f, 0.7f, 0.2f) : new Color(0.75f, 0.75f, 0.75f);
                    return c * sh;
                };
                var mk = new List<Vector3>();
                foreach (var a in arms.Arms) { mk.Add(a.S); mk.Add(a.E); mk.Add(a.W); mk.Add(a.T); }
                GlbDebug.Splat("splat_arms_" + def.Id, V, colF, H, mk);
            }

            log.AppendLine($"[{def.Id}]   t={sw.ElapsedMilliseconds} ms before: repose the arms to a relaxed rest pose (DQ blend)");
            // ---------------------------------------------------------------- repose the arms to a relaxed rest pose (DQ blend)
            // fingers: segmentation + weights on the original pose (after smoothing, which must not blur across fingers)
            var hands = GlbHands.Segment(V, weld, adj, weldCount, arms, S);
            foreach (var hd in hands) log.AppendLine($"[{def.Id}] hand {hd.Report}");
            GlbHands.ApplyWeights(W, hands);
            for (int s = 0; s < 2; s++)
            {
                var a = arms.Arms[s]; var hd = hands[s]; var Nn = N;
                Vector3 e1 = (a.W - a.T).normalized, e0 = Vector3.Cross(e1, Vector3.forward).normalized; if (e0.sqrMagnitude < 0.1f) e0 = Vector3.right; Vector3 e2 = Vector3.Cross(e0, e1);
                GlbDebug.SplatRegion("splat_hand_" + def.Id + "_" + s, V, i =>
                {
                    float sh = 0.5f + 0.5f * Mathf.Clamp01(Vector3.Dot(Nn[i].normalized, e2));
                    Color c = arms.Side[i] == s ? new Color(0.9f, 0.4f, 0.4f) : new Color(0.5f, 0.5f, 0.5f);
                    int hl = hd.Ok ? hd.Label[i] : -1;
                    if (hl >= 0) c = new[] { new Color(0.9f, 1f, 0.2f), new Color(0.1f, 1f, 1f), new Color(1f, 0.6f, 0.1f), new Color(0.8f, 0.3f, 1f), Color.white }[Mathf.Min(4, hl)];
                    else if (hd.Ok && hd.Palm[i] > 0.05f) c = Color.green;
                    return c * sh;
                }, (a.W + a.T) * 0.5f, 0.08f * S, e0, e1, e2);
            }
            GlbArms.SolveTargets(arms, V, lm, log, def.Id);
            GlbArms.SolveTwist(arms, V, log, def.Id, hands);
            GlbArms.Apply(arms, V, N, lm, S);
            {
                var Nn = N;
                var mk = new List<Vector3>();
                foreach (var a in arms.Arms) { mk.Add(a.S2); mk.Add(a.E2); mk.Add(a.W2); mk.Add(a.T2); }
                GlbDebug.Splat("splat_repose_" + def.Id, V, i =>
                {
                    float sh = 0.45f + 0.55f * Mathf.Clamp01(Vector3.Dot(Nn[i].normalized, new Vector3(0.3f, 0.5f, 0.8f).normalized));
                    Color c = arms.Side[i] == 0 ? new Color(1f, 0.3f, 0.3f) : arms.Side[i] == 1 ? new Color(0.3f, 0.5f, 1f) : new Color(0.75f, 0.75f, 0.75f);
                    int hl = arms.Side[i] >= 0 && hands[arms.Side[i]].Ok ? hands[arms.Side[i]].Label[i] : -1;
                    if (hl >= 0) c = new[] { new Color(0.9f, 1f, 0.2f), new Color(0.1f, 1f, 1f), new Color(1f, 0.6f, 0.1f), new Color(0.8f, 0.3f, 1f), Color.white }[Mathf.Min(4, hl)];
                    else if (arms.Side[i] >= 0 && hands[arms.Side[i]].Palm[i] > 0.05f) c = Color.green;
                    return c * sh;
                }, H, mk);
            }

            // ---------------------------------------------------------------- final skeleton (reposed arms)
            var P = ToProportions(lm, def);
            GlbHands.ToProportions(P, hands, arms);
            var bones = MakeBones(P);


            var smoothN = SmoothNormals(V, N, T);

            Texture2D skinTex = null;
            log.AppendLine($"[{def.Id}]   t={sw.ElapsedMilliseconds} ms before: textures + materials");
            // ---------------------------------------------------------------- textures + materials
            var mats = new List<Material>();
            for (int m = 0; m < glb.Materials.Count; m++)
            {
                var gm = glb.Materials[m];
                var spec = new MatSpec { Name = "Glb" + m, Color = gm.BaseColorImage >= 0 ? Color.white : gm.BaseColor, UseVertexColor = false, Cavity = 0f, Rim = 0.35f, DoubleSided = true, VertexMasks = true, HairRing = 0.05f, SkinSSS = 0.3f };
                spec.Shade = new Color(0.74f, 0.67f, 0.8f);
                spec.Shade2 = new Color(0.54f, 0.49f, 0.63f);
                if (gm.BaseColorImage >= 0 && m == 0 && fixedTex != null)
                {
                    skinTex = fixedTex;
                    spec.BaseMap = CharacterBaker.SaveTex(fixedTex, dir + "/" + def.Id + "_BaseColor" + m + ".png", 2048);
                }
                else if (gm.BaseColorImage >= 0)
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                    tex.LoadImage(glb.Images[gm.BaseColorImage]);
                    if (m == 0) skinTex = tex;
                    var small = Resize(tex, 2048);
                    if (m != 0) UnityEngine.Object.DestroyImmediate(tex);
                    spec.BaseMap = CharacterBaker.SaveTex(small, dir + "/" + def.Id + "_BaseColor" + m + ".png", 2048);
                    UnityEngine.Object.DestroyImmediate(small);
                }
                if (gm.Name.Contains("Black") || (gm.BaseColorImage < 0 && gm.BaseColor.grayscale < 0.05f)) { spec.Gloss = gm.Name.Contains("Metal") ? 0.8f : 0.2f; spec.GlossThreshold = 0.93f; }
                spec.Outline = new Color(0.07f, 0.05f, 0.07f);
                spec.OutlineWidth = 1.4f;
                mats.Add(CharacterBaker.MakeMat(spec, dir + "/" + def.Id + "_M" + m + ".mat", null, null, null));
            }
            int studMat = 0;
            if (studs.Count > 0)
            {
                var sm = glb.Materials.FindIndex(x => x.Name.Contains("Stud"));
                studMat = sm >= 0 ? sm : 0;
            }

            log.AppendLine($"[{def.Id}]   t={sw.ElapsedMilliseconds} ms before: face: skin mask + clean face normals");
            // ---------------------------------------------------------------- face: skin mask + clean face normals
            var faceImg = GlbFace.BuildImage(def.Id, V, UV, T, triMat, skinTex, lm, GlbFace.MakeFrame(lm, def.Id));
            GlbFaceInpaint.FlattenFeatures(def.Id, V, N, lm, GlbFace.MakeFrame(lm, def.Id), faceImg, log);   // charpolish step 0b hook (face implementer)
            GlbFace.SmoothFaceNormals(V, N, W, lm, GlbFace.MakeFrame(lm, def.Id), faceImg);

            log.AppendLine($"[{def.Id}]   t={sw.ElapsedMilliseconds} ms before: mesh");
            // ---------------------------------------------------------------- mesh
            var mesh = new Mesh { name = def.Id + "_Body" };
            var subTris = new List<int>[mats.Count];
            for (int i = 0; i < mats.Count; i++) subTris[i] = new List<int>();
            for (int t = 0; t < T.Count / 3; t++) { var st = subTris[triMat[t]]; st.Add(T[t * 3]); st.Add(T[t * 3 + 1]); st.Add(T[t * 3 + 2]); }
            var Wl = W.ToList();
            foreach (var sd in studs)
            {
                int b0 = V.Count;
                var accUv = GlbFix.AccUV.TryGetValue(def.Id, out var auv) ? auv : Vector2.zero;
                V.AddRange(sd.v); N.AddRange(sd.n); smoothN.AddRange(sd.n); UV.AddRange(sd.v.Select(_ => accUv));
                Wl.AddRange(sd.v.Select(_ => new BoneWeight { boneIndex0 = (int)HBone.Head, weight0 = 1f }));
                foreach (int ix in sd.t) subTris[studMat].Add(b0 + ix);
            }
            mesh.indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, UV);
            // outline direction: the welded smooth normal, unless it disagrees with the surface (thin double-sided hair
            // cards average to garbage there and the inverted hull then throws black spikes across the face)
            var outN = new List<Vector4>(V.Count);
            int badOut = 0;
            for (int i = 0; i < V.Count; i++)
            {
                var sn = smoothN[i]; var nn = N[i];
                bool ok = sn.sqrMagnitude > 0.04f && Vector3.Dot(sn.normalized, nn.normalized) > 0.35f;
                if (!ok) badOut++;
                var d = ok ? sn.normalized : nn.normalized;
                outN.Add(new Vector4(d.x, d.y, d.z, 1f));
            }
            mesh.SetTangents(outN);
            log.AppendLine($"[{def.Id}] outline normals replaced on {badOut} vertices (thin cards)");
            var faceUv = GlbFace.FaceUV(V, N, Wl, lm, def.Id, out var faceFrame);
            int baseVerts = V.Count - studs.Sum(sd => sd.v.Length);
            for (int i = baseVerts; i < faceUv.Count; i++) faceUv[i] = new Vector4(faceUv[i].x, faceUv[i].y, 0f, 0.55f);   // accessories: no face overlay, finer outline
            mesh.SetUVs(1, faceUv);
            mesh.SetUVs(2, V.ToList());
            var vmasks = GlbFix.VertexMasks(def, V, UV, Wl, skinTex, lm, baseVerts);
            GlbFix.ThinOutlinesNearEyes(def, V, vmasks, faceUv, lm, baseVerts);
            GlbFaceInpaint.OutlineMask(def.Id, V, vmasks, faceUv, lm, baseVerts);   // charpolish step 0b hook (face implementer): no outline over the mouth / on hair crossing the face
            mesh.SetUVs(1, faceUv);
            mesh.SetUVs(3, vmasks);
            mesh.SetColors(V.Select(_ => Color.white).ToList());
            mesh.subMeshCount = mats.Count;
            for (int i = 0; i < mats.Count; i++) mesh.SetTriangles(subTris[i], i, false);
            // charpolish step 0 hook (GlbCloth.cs, physics implementer): cloth / hair chain bones + re-weighting
            var cloth = GlbCloth.Build(def, V, N, Wl, vmasks, faceUv, baseVerts, bones, lm, P, arms, log);
            mesh.boneWeights = Wl.ToArray();
            GlbFace.AddBlendShapes(mesh, V, mesh.boneWeights, lm, faceFrame, faceImg, log, def.Id, baseVerts);
            mesh.RecalculateBounds();
            mesh = CharacterBaker.SaveMesh(mesh, dir + "/" + def.Id + "_Body.asset");

            log.AppendLine($"[{def.Id}]   t={sw.ElapsedMilliseconds} ms before: prefab");
            // ---------------------------------------------------------------- prefab
            var root = new GameObject(def.Id);
            try
            {
                var rig = root.AddComponent<ActorRig>();
                var boneT = CharacterBaker.BuildBones(root.transform, bones);
                var smrGo = new GameObject("Body");
                smrGo.transform.SetParent(root.transform, false);
                var smr = smrGo.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = boneT;
                smr.rootBone = boneT[0];
                smr.sharedMaterials = mats.ToArray();
                smr.localBounds = new Bounds(Vector3.zero, new Vector3(2.2f, 2.6f, 2.2f));
                mesh.bindposes = boneT.Select(b => b.worldToLocalMatrix * root.transform.localToWorldMatrix).ToArray();
                EditorUtility.SetDirty(mesh);
                string idle = GlbIdle.TryGetValue(def.Id, out var gst) ? gst : "stiff";
                CharacterBaker.FillRig(rig, def, boneT, P, idle);
                GlbCloth.Attach(cloth, rig, boneT, bones);   // charpolish step 0 hook (physics implementer)
                GlbFace.Setup(def, rig, smr, mats[0], faceFrame, lm, faceImg, skinTex, dir, log);
                // LOD1 / LOD2 may move UV-seam vertices (sub-millimetre seam gaps are invisible at their distances)
                var lods = CharacterBakerLod.AddLods(root, smr, 0.45f, 0.18f, false, dir + "/" + def.Id + "_Body_LOD1.asset", dir + "/" + def.Id + "_Body_LOD2.asset");
                rig.Skins = new[] { smr, lods[0], lods[1] };
                rig.IsGlb = true;
                rig.KeepArmRestBend = true;
                rig.CanBreak = def.Id == "P02" || def.Id == "P04";
                var anim = root.AddComponent<ActorAnimator>();
                anim.ArmIdleOut = GlbArms.IdleOut(arms);
                rig.Anim = anim;
                PrefabUtility.SaveAsPrefabAsset(root, CharacterBaker.PrefabRoot + "/" + def.Id + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            log.AppendLine($"[{def.Id}] GLB baked in {sw.ElapsedMilliseconds} ms");
            return true;
        }

        // ================================================================ landmark detection
        public static Landmarks Detect(List<Vector3> V, float H)
        {
            var lm = new Landmarks { H = H };
            const int bins = 200;
            float bh = H / bins;
            var slices = new List<float>[bins];
            for (int i = 0; i < bins; i++) slices[i] = new List<float>();
            foreach (var v in V) { int b = Mathf.Clamp((int)(v.y / bh), 0, bins - 1); slices[b].Add(v.x); }
            float[] torsoR = new float[bins], torsoL = new float[bins];
            for (int b = 0; b < bins; b++)
            {
                var xs = slices[b];
                if (xs.Count == 0) continue;
                const float cell = 0.005f;
                int n = 200;
                var occ = new bool[2 * n + 1];
                foreach (float x in xs) { int k = Mathf.Clamp(Mathf.RoundToInt(x / cell) + n, 0, 2 * n); occ[k] = true; }
                int gap = 0; int lastR = n;
                for (int k = n; k <= 2 * n; k++) { if (occ[k]) { lastR = k; gap = 0; } else if (++gap >= 3) break; }
                int lastL = n; gap = 0;
                for (int k = n; k >= 0; k--) { if (occ[k]) { lastL = k; gap = 0; } else if (++gap >= 3) break; }
                torsoR[b] = (lastR - n) * cell; torsoL[b] = (n - lastL) * cell;
            }
            float Y(int b) => (b + 0.5f) * bh;
            float S = H / 1.75f;
            lm.AnkleY = H * 0.045f;
            int crotch = (int)(bins * 0.46f);
            for (int b = (int)(bins * 0.3f); b < (int)(bins * 0.6f); b++)
            {
                bool center = slices[b].Any(x => Mathf.Abs(x) < 0.012f);
                if (center) { crotch = b; break; }
            }
            lm.CrotchY = Y(crotch);
            var legPts = V.Where(v => v.y > lm.CrotchY - 0.12f && v.y < lm.CrotchY - 0.04f && Mathf.Abs(v.x) < 0.2f * S).ToList();
            float lx = legPts.Where(v => v.x < 0).Select(v => v.x).DefaultIfEmpty(-0.09f).Average();
            float rx = legPts.Where(v => v.x > 0).Select(v => v.x).DefaultIfEmpty(0.09f).Average();
            lm.HipX = (Mathf.Abs(lx) + Mathf.Abs(rx)) * 0.5f;
            lm.HipY = lm.CrotchY + 0.035f * S;
            lm.KneeY = lm.AnkleY + (lm.HipY - lm.AnkleY) * 0.49f;
            lm.WaistY = lm.HipY + (H - lm.HipY) * 0.22f;
            int wb = Mathf.Clamp((int)(lm.WaistY / bh), 0, bins - 1);
            lm.TorsoHalfW = Mathf.Max(torsoL[wb], torsoR[wb]);
            int nb = (int)(0.8f * bins); float nw = 9f;
            for (int b = (int)(0.78f * bins); b < (int)(0.92f * bins); b++)
            {
                float w = torsoL[b] + torsoR[b];
                if (w > 0.02f && w < nw) { nw = w; nb = b; }
            }
            lm.NeckY = Y(nb);
            int sb = nb;
            for (int b = nb; b > (int)(bins * 0.6f); b--)
            {
                if (torsoL[b] + torsoR[b] > nw * 2.4f) { sb = b; break; }
            }
            lm.ShoulderY = Y(sb) - 0.035f * S;
            lm.ChestY = lm.WaistY + (lm.ShoulderY - lm.WaistY) * 0.45f;
            float headH = H - lm.NeckY;
            lm.ChinY = lm.NeckY + headH * 0.18f;
            lm.HeadCenter = new Vector3(0, (lm.ChinY + H) * 0.5f, 0);
            float eyeY = lm.ChinY + (H - lm.ChinY) * 0.42f;
            var faceBand = V.Where(v => Mathf.Abs(v.y - eyeY) < 0.01f && Mathf.Abs(v.x) < 0.03f).ToList();
            float faceZ = faceBand.Count > 0 ? faceBand.Max(v => v.z) : 0.1f;
            lm.EyeCenter = new Vector3(0, eyeY, faceZ - 0.01f);
            var headBand = V.Where(v => v.y > lm.ChinY && v.y < H - 0.05f && Mathf.Abs(v.x) < 0.1f).ToList();
            if (headBand.Count > 0) lm.HeadCenter.z = (headBand.Min(v => v.z) + headBand.Max(v => v.z)) * 0.5f;
            var chestBand = V.Where(v => Mathf.Abs(v.y - lm.ChestY) < 0.01f && Mathf.Abs(v.x) < 0.05f).ToList();
            lm.TorsoHalfD = chestBand.Count > 0 ? (chestBand.Max(v => v.z) - chestBand.Min(v => v.z)) * 0.5f : 0.1f;

            for (int s = 0; s < 2; s++)
            {
                float sx = s == 0 ? -1f : 1f;
                var pts = new List<Vector3>();
                int cb = Mathf.Clamp((int)(lm.ChestY / bh), 0, bins - 1);
                float chestHalf = Mathf.Max(s == 0 ? torsoL[cb] : torsoR[cb], 0.1f);
                foreach (var v in V)
                {
                    if (v.x * sx <= 0) continue;
                    if (v.y < lm.HipY * 0.72f || v.y > lm.ShoulderY + 0.1f) continue;
                    int b = Mathf.Clamp((int)(v.y / bh), 0, bins - 1);
                    float run = s == 0 ? torsoL[b] : torsoR[b];
                    bool outside = Mathf.Abs(v.x) > run + 0.004f || (v.y > lm.ChestY && Mathf.Abs(v.x) > chestHalf + 0.02f);
                    if (outside && Mathf.Abs(v.x) > lm.HipX * 0.9f) pts.Add(v);
                }
                if (pts.Count < 50) { lm.Report += $" arm{s}:few({pts.Count})"; pts = V.Where(v => v.x * sx > chestHalf && v.y > lm.HipY * 0.8f && v.y < lm.ShoulderY).ToList(); }
                Vector3 mean = Vector3.zero; foreach (var p in pts) mean += p; mean /= Mathf.Max(1, pts.Count);
                Vector3 dir = PrincipalAxis(pts, mean);
                if (Vector3.Dot(dir, new Vector3(sx, -1f, 0)) < 0) dir = -dir;
                float tMax = float.MinValue;
                foreach (var p in pts) tMax = Mathf.Max(tMax, Vector3.Dot(p - mean, dir));
                Vector3 tip = mean + dir * tMax;
                float armR = 0.045f * S;
                int shb = Mathf.Clamp((int)(lm.ShoulderY / bh), 0, bins - 1);
                float edge = Mathf.Min(s == 0 ? torsoL[Mathf.Clamp(shb - 6, 0, bins - 1)] : torsoR[Mathf.Clamp(shb - 6, 0, bins - 1)], chestHalf + 0.06f);
                Vector3 shoulder = new Vector3(sx * Mathf.Max(0.12f * S, Mathf.Min(edge, chestHalf + 0.03f) - armR * 0.6f), lm.ShoulderY, 0f);
                var shBand = V.Where(v => Mathf.Abs(v.y - lm.ShoulderY) < 0.02f && Mathf.Abs(v.x - shoulder.x) < 0.03f).ToList();
                if (shBand.Count > 0) shoulder.z = (shBand.Min(v => v.z) + shBand.Max(v => v.z)) * 0.5f;
                Vector3 armDir = (tip - shoulder).normalized;
                float armLen = (tip - shoulder).magnitude;
                float handLen = 0.105f * H;
                Vector3 wrist = tip - armDir * handLen;
                Vector3 elbow = shoulder + (wrist - shoulder) * 0.52f;
                float ang = Vector3.Angle(armDir, Vector3.down);
                if (s == 0) { lm.ShoulderL = shoulder; lm.ElbowL = elbow; lm.WristL = wrist; lm.TipL = tip; lm.ArmAngleL = ang; }
                else { lm.ShoulderR = shoulder; lm.ElbowR = elbow; lm.WristR = wrist; lm.TipR = tip; lm.ArmAngleR = ang; }
                lm.Report += $" arm{s}: pts {pts.Count} angle {ang:F0} len {armLen:F2} sh {shoulder} tip {tip}";
            }
            lm.ShoulderX = (Mathf.Abs(lm.ShoulderL.x) + Mathf.Abs(lm.ShoulderR.x)) * 0.5f;
            for (int s = 0; s < 2; s++)
            {
                float sx = s == 0 ? -1f : 1f;
                var knee = V.Where(v => Mathf.Abs(v.y - lm.KneeY) < 0.015f && v.x * sx > 0.01f && Mathf.Abs(v.x) < 0.25f * S).ToList();
                var ank = V.Where(v => Mathf.Abs(v.y - lm.AnkleY - 0.03f) < 0.015f && v.x * sx > 0.01f).ToList();
                var foot = V.Where(v => v.y < 0.06f * S && v.x * sx > 0.0f).ToList();
                Vector3 kc = knee.Count > 0 ? Center(knee) : new Vector3(sx * lm.HipX, lm.KneeY, 0);
                Vector3 ac = ank.Count > 0 ? Center(ank) : new Vector3(sx * lm.HipX, lm.AnkleY, 0);
                kc.y = lm.KneeY; ac.y = lm.AnkleY;
                ac.z -= 0.01f;
                float toeZ = foot.Count > 0 ? foot.Max(v => v.z) : ac.z + 0.2f;
                float toeX = foot.Count > 0 ? foot.Where(v => v.z > toeZ - 0.03f).Select(v => v.x).DefaultIfEmpty(ac.x).Average() : ac.x;
                Vector3 hip = new Vector3(sx * lm.HipX, lm.HipY, kc.z * 0.5f);
                if (s == 0) { lm.HipL = hip; lm.KneeL = kc; lm.AnkleL = ac; lm.ToeL = new Vector3(toeX, 0.01f, toeZ); }
                else { lm.HipR = hip; lm.KneeR = kc; lm.AnkleR = ac; lm.ToeR = new Vector3(toeX, 0.01f, toeZ); }
            }
            lm.Report += $" crotch {lm.CrotchY:F2} hip {lm.HipY:F2} waist {lm.WaistY:F2} chest {lm.ChestY:F2} shoulderY {lm.ShoulderY:F2} neck {lm.NeckY:F2} chin {lm.ChinY:F2} shX {lm.ShoulderX:F2} hipX {lm.HipX:F2}";
            return lm;
        }

        static Vector3 Center(List<Vector3> pts)
        {
            Vector3 mn = pts[0], mx = pts[0];
            foreach (var p in pts) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
            return (mn + mx) * 0.5f;
        }

        static Vector3 PrincipalAxis(List<Vector3> pts, Vector3 mean)
        {
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (var p in pts)
            {
                Vector3 d = p - mean;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z; yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }
            Vector3 v = new Vector3(1, 1, 0.1f).normalized;
            for (int it = 0; it < 40; it++)
            {
                v = new Vector3(xx * v.x + xy * v.y + xz * v.z, xy * v.x + yy * v.y + yz * v.z, xz * v.x + yz * v.y + zz * v.z);
                float m = v.magnitude; if (m < 1e-12f) break; v /= m;
            }
            return v;
        }

        static List<BoneDef> MakeBones(BodyProportions P)
        {
            var bones = new List<BoneDef>();
            for (int i = 0; i < ActorSkeleton.Count; i++) bones.Add(new BoneDef { Name = ActorSkeleton.Names[i], Parent = ActorSkeleton.Parent[i], Pos = P.Joint[i] });
            void Tail(HBone b, Vector3 t) => bones[(int)b].Tail = t;
            Tail(HBone.Hips, P.J(HBone.Spine)); Tail(HBone.Spine, P.J(HBone.Chest)); Tail(HBone.Chest, P.J(HBone.Neck));
            Tail(HBone.Neck, P.J(HBone.Head)); Tail(HBone.Head, P.HeadTop);
            for (int s = 0; s < 2; s++)
            {
                HBone sh = s == 0 ? HBone.ShoulderL : HBone.ShoulderR;
                Tail(sh, P.J(sh + 1)); Tail(sh + 1, P.J(sh + 2)); Tail(sh + 2, P.J(sh + 3)); Tail(sh + 3, s == 0 ? P.HandTipL : P.HandTipR);
                HBone ul = s == 0 ? HBone.UpperLegL : HBone.UpperLegR;
                Tail(ul, P.J(ul + 1)); Tail(ul + 1, P.J(ul + 2)); Tail(ul + 2, P.J(ul + 3)); Tail(ul + 3, s == 0 ? P.ToeTipL : P.ToeTipR);
            }
            CharacterBaker.ExtendedTails(bones, P);
            bones[0].Pos = P.J(HBone.Hips) + new Vector3(0, -0.05f, 0);
            return bones;
        }

        static BodyProportions ToProportions(Landmarks lm, CastDef def)
        {
            var look = def.Look;
            var P = BodyProportions.Make(lm.H, def.Gender == Gender.F, look.Build, look.Shoulders, look.HeadScale);
            float H = lm.H;
            P.Joint[(int)HBone.Hips] = new Vector3(0, lm.HipY + 0.045f * H / 1.75f, lm.HipL.z * 0.5f + lm.HipR.z * 0.5f);
            P.Joint[(int)HBone.Spine] = new Vector3(0, lm.WaistY, -0.005f);
            P.Joint[(int)HBone.Chest] = new Vector3(0, lm.ChestY, -0.005f);
            P.Joint[(int)HBone.Neck] = new Vector3(0, lm.NeckY - 0.02f * H / 1.75f, lm.HeadCenter.z - 0.02f);
            P.Joint[(int)HBone.UpperChest] = new Vector3(0, Mathf.Lerp(lm.ChestY, lm.ShoulderY, 0.55f), -0.01f);
            P.Joint[(int)HBone.Neck] = new Vector3(0, lm.NeckY - 0.02f * H / 1.75f, lm.HeadCenter.z - 0.02f);
            P.Joint[(int)HBone.Head] = new Vector3(0, lm.ChinY + 0.02f, lm.HeadCenter.z - 0.01f);
            P.HeadTop = new Vector3(0, H, lm.HeadCenter.z);
            P.HeadCenter = lm.HeadCenter;
            P.EyeCenter = lm.EyeCenter;
            P.ChinPos = new Vector3(0, lm.ChinY, lm.EyeCenter.z - 0.05f);
            P.HH = H - lm.ChinY;
            for (int s = 0; s < 2; s++)
            {
                bool l = s == 0;
                HBone sh = l ? HBone.ShoulderL : HBone.ShoulderR;
                Vector3 shoulder = l ? lm.ShoulderL : lm.ShoulderR;
                P.Joint[(int)sh] = new Vector3(shoulder.x * 0.18f, lm.ShoulderY + 0.02f, shoulder.z - 0.01f);
                P.Joint[(int)sh + 1] = shoulder;
                P.Joint[(int)sh + 2] = l ? lm.ElbowL : lm.ElbowR;
                P.Joint[(int)sh + 3] = l ? lm.WristL : lm.WristR;
                if (l) P.HandTipL = lm.TipL; else P.HandTipR = lm.TipR;
                HBone ul = l ? HBone.UpperLegL : HBone.UpperLegR;
                P.Joint[(int)ul] = l ? lm.HipL : lm.HipR;
                P.Joint[(int)ul + 1] = l ? lm.KneeL : lm.KneeR;
                P.Joint[(int)ul + 2] = l ? lm.AnkleL : lm.AnkleR;
                Vector3 toe = l ? lm.ToeL : lm.ToeR;
                Vector3 an = l ? lm.AnkleL : lm.AnkleR;
                P.Joint[(int)ul + 3] = new Vector3(Mathf.Lerp(an.x, toe.x, 0.7f), 0.02f, Mathf.Lerp(an.z, toe.z, 0.72f));
                if (l) P.ToeTipL = toe; else P.ToeTipR = toe;
            }
            P.HandLen = (lm.TipL - lm.WristL).magnitude;
            P.ChestHalfD = lm.TorsoHalfD;
            P.ArmRestAngle = (lm.ArmAngleL + lm.ArmAngleR) * 0.5f;
            return P;
        }

        static float EstimateArmClearance(List<Vector3> V, Landmarks lm)
        {
            float hipW = V.Where(v => Mathf.Abs(v.y - (lm.HipY + 0.02f)) < 0.02f && Mathf.Abs(v.x) < lm.ShoulderX + 0.02f).Select(v => Mathf.Abs(v.x)).DefaultIfEmpty(0.17f).Max();
            float drop = lm.ShoulderY - (lm.HipY + 0.02f);
            float needX = hipW + 0.045f - lm.ShoulderX;
            float ang = Mathf.Atan2(Mathf.Max(0f, needX), Mathf.Max(0.1f, drop)) * Mathf.Rad2Deg;
            return Mathf.Clamp(ang + 2f, 5f, 18f);
        }

        // ================================================================ regions & weights
        const int RHead = 0, RTorso = 1, RArmL = 2, RArmR = 3, RLegL = 4, RLegR = 5;

        static int[] Classify(List<Vector3> V, Landmarks lm)
        {
            var r = new int[V.Count];
            Vector3 dL = (lm.TipL - lm.ShoulderL).normalized, dR = (lm.TipR - lm.ShoulderR).normalized;
            float lenL = (lm.TipL - lm.ShoulderL).magnitude, lenR = (lm.TipR - lm.ShoulderR).magnitude;
            for (int i = 0; i < V.Count; i++)
            {
                var v = V[i];
                bool armAssigned = false;
                for (int s = 0; s < 2; s++)
                {
                    Vector3 sh = s == 0 ? lm.ShoulderL : lm.ShoulderR, d = s == 0 ? dL : dR;
                    float len = s == 0 ? lenL : lenR;
                    float sx = s == 0 ? -1f : 1f;
                    if (v.x * sx < Mathf.Abs(sh.x) * 0.72f) continue;
                    float t = Vector3.Dot(v - sh, d);
                    if (t < -0.02f || t > len + 0.03f) continue;
                    float dist = (v - (sh + d * t)).magnitude;
                    float rad = Mathf.Lerp(0.085f, 0.07f, t / len) * lm.H / 1.75f;
                    if (dist < rad && (t > 0.06f || Mathf.Abs(v.x) > Mathf.Abs(sh.x) - 0.01f))
                    {
                        r[i] = s == 0 ? RArmL : RArmR; armAssigned = true; break;
                    }
                }
                if (armAssigned) continue;
                if (v.y > lm.NeckY + (lm.ChinY - lm.NeckY) * 0.3f) { r[i] = RHead; continue; }
                if (v.y < lm.CrotchY + 0.01f) { r[i] = v.x < 0 ? RLegL : RLegR; continue; }
                r[i] = RTorso;
            }
            return r;
        }

        static BoneWeight WeighGlb(List<BoneDef> bones, int region, Vector3 p, Landmarks lm)
        {
            int[] cand;
            switch (region)
            {
                case RHead:
                {
                    float k = Mathf.Clamp01((p.y - lm.NeckY) / Mathf.Max(0.01f, lm.ChinY - lm.NeckY));
                    var l0 = new List<KeyValuePair<int, float>> { new KeyValuePair<int, float>((int)HBone.Head, k * k), new KeyValuePair<int, float>((int)HBone.Neck, 1f - k * k) };
                    return CharMesher.ToBW(l0);
                }
                case RArmL: cand = new[] { (int)HBone.ShoulderL, (int)HBone.UpperArmL, (int)HBone.LowerArmL, (int)HBone.HandL }; break;
                case RArmR: cand = new[] { (int)HBone.ShoulderR, (int)HBone.UpperArmR, (int)HBone.LowerArmR, (int)HBone.HandR }; break;
                case RLegL: cand = new[] { (int)HBone.Hips, (int)HBone.UpperLegL, (int)HBone.LowerLegL, (int)HBone.FootL, (int)HBone.ToeL }; break;
                case RLegR: cand = new[] { (int)HBone.Hips, (int)HBone.UpperLegR, (int)HBone.LowerLegR, (int)HBone.FootR, (int)HBone.ToeR }; break;
                default: cand = new[] { (int)HBone.Hips, (int)HBone.Spine, (int)HBone.Chest, (int)HBone.Neck, (int)HBone.ShoulderL, (int)HBone.ShoulderR, (int)HBone.UpperLegL, (int)HBone.UpperLegR }; break;
            }
            var l = new List<KeyValuePair<int, float>>();
            foreach (int b in cand)
            {
                var bd = bones[b];
                if (region == RTorso && (b == (int)HBone.UpperLegL || b == (int)HBone.UpperLegR) && p.y > lm.HipY) continue;
                if (region == RTorso && (b == (int)HBone.ShoulderL || b == (int)HBone.ShoulderR) && p.y < lm.ChestY) continue;
                float d = CharMesher.SegDist(p, bd.Pos, bd.Tail);
                l.Add(new KeyValuePair<int, float>(b, 1f / Mathf.Pow(d + 0.01f, 4f)));
            }
            return CharMesher.ToBW(l);
        }

        static int[] Weld(List<Vector3> V, float eps, out int count)
        {
            var map = new Dictionary<(int, int, int), int>();
            var w = new int[V.Count];
            count = 0;
            for (int i = 0; i < V.Count; i++)
            {
                var k = (Mathf.RoundToInt(V[i].x / eps), Mathf.RoundToInt(V[i].y / eps), Mathf.RoundToInt(V[i].z / eps));
                if (!map.TryGetValue(k, out int id)) { id = count++; map[k] = id; }
                w[i] = id;
            }
            return w;
        }

        static List<int>[] WeldAdjacency(List<int> T, int[] weld, int count)
        {
            var adj = new HashSet<int>[count];
            for (int i = 0; i < count; i++) adj[i] = new HashSet<int>();
            for (int t = 0; t < T.Count; t += 3)
            {
                int a = weld[T[t]], b = weld[T[t + 1]], c = weld[T[t + 2]];
                adj[a].Add(b); adj[a].Add(c); adj[b].Add(a); adj[b].Add(c); adj[c].Add(a); adj[c].Add(b);
            }
            return adj.Select(h => h.ToList()).ToArray();
        }

        static BoneWeight[] SmoothWeights(BoneWeight[] W, int[] weld, List<int>[] adj, int count, int iters)
        {
            const int NB = ActorSkeleton.Count;
            var cur = new float[count * NB];
            var seen = new bool[count];
            for (int i = 0; i < W.Length; i++)
            {
                int k = weld[i];
                if (seen[k]) continue;
                seen[k] = true;
                var w = W[i];
                cur[k * NB + w.boneIndex0] += w.weight0; cur[k * NB + w.boneIndex1] += w.weight1; cur[k * NB + w.boneIndex2] += w.weight2; cur[k * NB + w.boneIndex3] += w.weight3;
            }
            for (int it = 0; it < iters; it++)
            {
                var next = new float[count * NB];
                Parallel.For(0, count, k =>
                {
                    float tot = 2f;
                    for (int b = 0; b < NB; b++) next[k * NB + b] = cur[k * NB + b] * 2f;
                    foreach (int j in adj[k]) { for (int b = 0; b < NB; b++) next[k * NB + b] += cur[j * NB + b]; tot += 1f; }
                    for (int b = 0; b < NB; b++) next[k * NB + b] /= tot;
                });
                cur = next;
            }
            var res = new BoneWeight[W.Length];
            for (int i = 0; i < W.Length; i++)
            {
                int k = weld[i];
                var l = new List<KeyValuePair<int, float>>();
                for (int b = 0; b < NB; b++) if (cur[k * NB + b] > 0.001f) l.Add(new KeyValuePair<int, float>(b, cur[k * NB + b]));
                res[i] = l.Count > 0 ? CharMesher.ToBW(l) : W[i];
            }
            return res;
        }

        static bool[] BorderVertices(List<int> T, int nv)
        {
            var edges = new Dictionary<long, int>(MeshDecimator.EdgeKeyComparer.Instance);
            for (int t = 0; t < T.Count; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = T[t + k], b = T[t + (k + 1) % 3];
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    edges.TryGetValue(key, out int c); edges[key] = c + 1;
                }
            var locked = new bool[nv];
            foreach (var kv in edges)
                if (kv.Value == 1) { locked[(int)(kv.Key >> 32)] = true; locked[(int)(kv.Key & 0xffffffff)] = true; }
            return locked;
        }

        static List<Vector3> SmoothNormals(List<Vector3> V, List<Vector3> N, List<int> T)
        {
            var weld = Weld(V, 0.0005f, out int count);
            var acc = new Vector3[count];
            for (int t = 0; t < T.Count; t += 3)
            {
                Vector3 n = Vector3.Cross(V[T[t + 1]] - V[T[t]], V[T[t + 2]] - V[T[t]]);
                acc[weld[T[t]]] += n; acc[weld[T[t + 1]]] += n; acc[weld[T[t + 2]]] += n;
            }
            var r = new List<Vector3>(V.Count);
            for (int i = 0; i < V.Count; i++) { var n = acc[weld[i]]; r.Add(n.sqrMagnitude > 1e-12f ? n.normalized : N[i]); }
            return r;
        }

        static Texture2D Resize(Texture2D src, int size)
        {
            if (src.width <= size && src.height <= size) { var c = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false); c.SetPixels(src.GetPixels()); c.Apply(); return c; }
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, size, size), 0, 0); t.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            return t;
        }
    }
}

namespace BL23.EditorTools.Characters
{
    public static partial class GlbRigger
    {
        static KeyValuePair<int, float> KV(HBone b, float w) => new KeyValuePair<int, float>((int)b, w);

        static void DistW(List<KeyValuePair<int, float>> l, List<BoneDef> bones, Vector3 p, float scale, params HBone[] cand)
        {
            var tmp = new List<KeyValuePair<int, float>>();
            float sum = 0f;
            foreach (var b in cand)
            {
                var bd = bones[(int)b];
                float d = CharMesher.SegDist(p, bd.Pos, bd.Tail);
                float w = 1f / Mathf.Pow(d + 0.012f, 4f);
                tmp.Add(KV(b, w)); sum += w;
            }
            foreach (var kv in tmp) l.Add(new KeyValuePair<int, float>(kv.Key, kv.Value / sum * scale));
        }

        /// <summary>Region-constrained weights: arms from the silhouette flood mask with a soft shoulder blend,
        /// legs vs. coat skirt below the crotch, torso chain, head/neck.</summary>
        static BoneWeight WeighGlb2(List<BoneDef> bones, GlbAnalysis an, Vector3 p, bool allowArm = true)
        {
            var lm = an.LM;
            float S = lm.H / 1.75f;
            var l = new List<KeyValuePair<int, float>>();
            // arms
            for (int s = 0; s < 2 && allowArm; s++)
            {
                if (!an.IsArm(p, s, out float along)) continue;
                bool L = s == 0;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.01f, 0.08f * S, along));
                DistW(l, bones, p, k, L ? HBone.UpperArmL : HBone.UpperArmR, L ? HBone.LowerArmL : HBone.LowerArmR, L ? HBone.HandL : HBone.HandR);
                l.Add(KV(L ? HBone.ShoulderL : HBone.ShoulderR, (1f - k) * 0.6f));
                l.Add(KV(HBone.UpperChest, (1f - k) * 0.4f));
                return CharMesher.ToBW(l);
            }
            // head / neck
            if (p.y > lm.ChinY - 0.01f || (p.y > lm.NeckY - 0.01f && Mathf.Abs(p.x) < Mathf.Max(lm.ShoulderX * 0.9f, 0.13f * S)))
            {
                float k = Mathf.Clamp01((p.y - (lm.NeckY - 0.01f)) / Mathf.Max(0.02f, lm.ChinY - lm.NeckY + 0.02f));
                k = k * k * (3f - 2f * k);
                l.Add(KV(HBone.Head, k)); l.Add(KV(HBone.Neck, 1f - k));
                return CharMesher.ToBW(l);
            }
            // below the crotch: legs or coat skirt
            if (p.y < lm.CrotchY + 0.02f)
            {
                int s = p.x < 0 ? 0 : 1;
                bool L = s == 0;
                var th = bones[(int)(L ? HBone.UpperLegL : HBone.UpperLegR)];
                var sh = bones[(int)(L ? HBone.LowerLegL : HBone.LowerLegR)];
                float dLeg = Mathf.Min(CharMesher.SegDist(p, th.Pos, th.Tail), CharMesher.SegDist(p, sh.Pos, sh.Tail));
                float coatT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.085f * S, 0.12f * S, dLeg));
                if (p.y < lm.AnkleY + 0.12f * S) coatT = 0f;
                if (coatT < 1f)
                {
                    float up = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lm.CrotchY - 0.05f, lm.CrotchY + 0.02f, p.y));
                    DistW(l, bones, p, (1f - coatT) * (1f - up * 0.6f), L ? HBone.UpperLegL : HBone.UpperLegR, L ? HBone.LowerLegL : HBone.LowerLegR, L ? HBone.FootL : HBone.FootR, L ? HBone.ToeL : HBone.ToeR);
                    l.Add(KV(HBone.Hips, (1f - coatT) * up * 0.6f));
                }
                if (coatT > 0f)
                {
                    // coat skirt: the front panels ride on the thighs (and drape from the knees with the shins), the back
                    // panel mostly stays with the pelvis - sitting then lays the coat over the lap instead of the legs
                    // punching through it
                    float t = Mathf.Clamp01((lm.CrotchY + 0.05f - p.y) / Mathf.Max(0.1f, lm.CrotchY - lm.KneeY));
                    float legZ = (lm.HipL.z + lm.HipR.z) * 0.5f;
                    float front = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.03f * S, 0.05f * S, p.z - legZ));
                    float legW = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.3f)) * Mathf.Lerp(0.4f, 0.9f, front);
                    float side = Mathf.Clamp01(0.5f + p.x / (0.14f * S));
                    float shin = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lm.KneeY + 0.03f * S, lm.KneeY - 0.06f * S, p.y)) * front;
                    l.Add(KV(HBone.Hips, coatT * (1f - legW)));
                    l.Add(KV(HBone.UpperLegL, coatT * legW * (1f - side) * (1f - shin)));
                    l.Add(KV(HBone.UpperLegR, coatT * legW * side * (1f - shin)));
                    l.Add(KV(HBone.LowerLegL, coatT * legW * (1f - side) * shin));
                    l.Add(KV(HBone.LowerLegR, coatT * legW * side * shin));
                }
                return CharMesher.ToBW(l);
            }
            // torso (+ soft deltoid influence near the shoulders)
            float armShare = 0f; int armSide = -1;
            for (int s = 0; s < 2; s++)
            {
                Vector3 shJ = s == 0 ? lm.ShoulderL : lm.ShoulderR, tip = s == 0 ? lm.TipL : lm.TipR;
                if (p.x * (s == 0 ? -1f : 1f) < lm.ShoulderX * 0.45f) continue;
                float along = Vector3.Dot(p - shJ, (tip - shJ).normalized);
                float dist = (p - shJ).magnitude;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.07f * S, 0f, along)) * Mathf.Clamp01(1f - dist / (0.14f * S)) * 0.5f;
                if (a > armShare) { armShare = a; armSide = s; }
            }
            DistW(l, bones, p, 1f - armShare, HBone.Hips, HBone.Spine, HBone.Chest, HBone.UpperChest, HBone.Neck);
            if (armSide >= 0)
            {
                bool L = armSide == 0;
                l.Add(KV(L ? HBone.UpperArmL : HBone.UpperArmR, armShare * 0.6f));
                l.Add(KV(L ? HBone.ShoulderL : HBone.ShoulderR, armShare * 0.4f));
            }
            // upper thighs pull the pelvis region below the hips
            if (p.y < lm.HipY + 0.04f)
            {
                float k = Mathf.Clamp01((lm.HipY + 0.04f - p.y) / 0.1f) * 0.35f;
                for (int i = 0; i < l.Count; i++) l[i] = new KeyValuePair<int, float>(l[i].Key, l[i].Value * (1f - k));
                l.Add(KV(p.x < 0 ? HBone.UpperLegL : HBone.UpperLegR, k));
            }
            return CharMesher.ToBW(l);
        }
    }
}
