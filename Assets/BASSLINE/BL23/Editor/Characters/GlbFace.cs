using System.Collections.Generic;
using System.Linq;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Expression overlay for the scanned GLB heads. The painted scan face is always the base; the overlay only adds
    /// eyelids (blink / half-lid / closed / happy...), small mouth shapes and cheek FX, all painted from the scan's own
    /// colours and masked per pixel by <see cref="GlbFaceMask"/> so nothing can land on hair.
    /// </summary>
    public static class GlbFace
    {
        public sealed class Frame { public Vector3 Center; public float Scale; public float FaceZ; }

        /// <summary>
        /// Feature placement. Eye centre = iris centre (m, relative to the detected eye centre); the eye opening shape is
        /// given in millimetres relative to the iris centre, x toward the nose, y up (measured on Shots/glbface_ID.png).
        /// </summary>
        public sealed class Calib
        {
            public float EyeCX = 0f, EyeCY = 0f;   // correction of the detected eye centre (m)
            public float ChinDY = -0.08f;          // chin tip relative to the eye centre (m)
            public float EyeDX = 0.03f, EyeDY = 0f, EyeHalf = 0.019f, MouthDX = 0f, MouthDY = -0.058f, MouthHalf = 0.012f;
            public Vector2 EyeOut = new Vector2(-17f, -2f), EyeIn = new Vector2(15f, -4f), EyeTop = new Vector2(-1f, 8.5f), EyeBot = new Vector2(0f, -9f);
            public float LashW = 1.6f;             // upper lash line thickness (mm)
            public Color? Brow;
            public float Sharp = -1f;
        }

        public static readonly Dictionary<string, Calib> Calibration = new Dictionary<string, Calib>
        {
            { "P01", new Calib { EyeCX = 0.0f, EyeCY = -0.055f, EyeDX = 0.034f, EyeHalf = 0.016f, MouthDX = -0.001f, MouthDY = -0.0536f, MouthHalf = 0.0115f, ChinDY = -0.083f,
                                 EyeOut = new Vector2(-18.8f, -2.5f), EyeIn = new Vector2(16f, -5.8f), EyeTop = new Vector2(-1f, 8.9f), EyeBot = new Vector2(0f, -9.2f), LashW = 2.3f } },
            { "P02", new Calib { EyeCX = 0.0005f, EyeCY = -0.059f, EyeDX = 0.0356f, EyeDY = -0.0037f, EyeHalf = 0.021f, MouthDX = 0f, MouthDY = -0.0536f, MouthHalf = 0.018f, ChinDY = -0.085f, Brow = new Color(0.75f, 0.74f, 0.78f),
                                 EyeOut = new Vector2(-26f, 2.4f), EyeIn = new Vector2(18.5f, 0.2f), EyeTop = new Vector2(-5.5f, 13f), EyeBot = new Vector2(-0.5f, -14.3f), LashW = 2.8f } },
            { "P04", new Calib { EyeCX = 0.0055f, EyeCY = -0.047f, EyeDX = 0.031f, EyeHalf = 0.016f, MouthDX = -0.005f, MouthDY = -0.065f, MouthHalf = 0.02f, ChinDY = -0.09f, Sharp = 0.75f,
                                 EyeOut = new Vector2(-17f, 1.1f), EyeIn = new Vector2(14f, -2.1f), EyeTop = new Vector2(-2.4f, 5.5f), EyeBot = new Vector2(0f, -4.4f), LashW = 1.5f } },
            { "P05", new Calib { EyeCX = 0.0075f, EyeCY = -0.0475f, EyeDX = 0.0372f, EyeHalf = 0.018f, MouthDX = -0.005f, MouthDY = -0.059f, MouthHalf = 0.027f, ChinDY = -0.094f } },
            { "P06", new Calib { EyeCX = 0.0051f, EyeCY = -0.059f, EyeDX = 0.0403f, EyeHalf = 0.02f, MouthDX = -0.003f, MouthDY = -0.062f, MouthHalf = 0.016f, ChinDY = -0.085f } },
            { "P10", new Calib { EyeCX = 0.011f, EyeCY = -0.0576f, EyeDX = 0.036f, EyeHalf = 0.018f, MouthDX = -0.005f, MouthDY = -0.061f, MouthHalf = 0.016f, ChinDY = -0.09f } },
        };

        public static Calib Get(string id) => Calibration.TryGetValue(id, out var c) ? c : new Calib();

        /// <summary>Face frame: 21 cm square around the face (eyes ~0.62, mouth ~0.3, chin inside).</summary>
        public static Frame MakeFrame(GlbRigger.Landmarks lm, string id)
        {
            // charpolish-fc F-a: EyeCenter.z = eye skin - 12 mm, so FaceZ = eye skin - 20 mm (the overlay / blend-shape /
            // normal-smoothing depth gate starts 2 cm behind the facial skin, never in front of it)
            float faceZ = GlbFaceCalib.SkinDepth && Calibration.ContainsKey(id) ? lm.EyeCenter.z + 0.012f - 0.02f : lm.EyeCenter.z - 0.045f;
            return new Frame { Center = new Vector3(lm.EyeCenter.x, lm.EyeCenter.y - 0.012f, lm.HeadCenter.z), Scale = 0.21f, FaceZ = faceZ };
        }

        public sealed class Layout
        {
            public Vector2 EyeL, EyeR;      // iris centres in face uv (image-left = character's right eye)
            public float EyeHalf;           // eye cell half size (face uv, square)
            public float EyeHalfM;          // same in metres
            public Vector2 Mouth;           // mouth line centre (face uv)
            public Vector2 MouthHalf;       // mouth cell half size (face uv, 2:1)
            public Vector2 MouthHalfM;
            public Calib C;
            public Frame F;
            /// <summary>Face uv of a point given in eye-local mm (x toward the nose, y up) for eye s (0 image-left, 1 image-right).</summary>
            public Vector2 EyeUV(int s, Vector2 mm)
            {
                Vector2 e = s == 0 ? EyeL : EyeR;
                float dx = mm.x * 0.001f / F.Scale, dy = mm.y * 0.001f / F.Scale;
                return new Vector2(s == 0 ? e.x + dx : e.x - dx, e.y + dy);
            }
        }

        public static Layout MakeLayout(GlbRigger.Landmarks lm, string id, Frame f)
        {
            var c = Get(id);
            var L = new Layout { C = c, F = f };
            float U(float dx) => dx / f.Scale;
            float Vv(float y) => 0.5f + (y - f.Center.y) / f.Scale;
            float ev = Vv(lm.EyeCenter.y + c.EyeDY);
            L.EyeL = new Vector2(0.5f - U(c.EyeDX), ev); L.EyeR = new Vector2(0.5f + U(c.EyeDX), ev);
            float ext = Mathf.Max(Mathf.Max(-c.EyeOut.x, c.EyeIn.x), Mathf.Max(c.EyeTop.y, -c.EyeBot.y)) + 3.5f;
            L.EyeHalfM = ext * 0.001f;
            L.EyeHalf = U(L.EyeHalfM);
            L.Mouth = new Vector2(0.5f - U(c.MouthDX), Vv(lm.EyeCenter.y + c.MouthDY));
            // proc mouth art: a smile line is 0.13 cell-x units long each side -> match the scanned line
            float hw = c.MouthHalf / 0.26f;
            L.MouthHalfM = new Vector2(hw, hw * 0.5f);
            L.MouthHalf = new Vector2(U(hw), U(hw * 0.5f));
            return L;
        }

        /// <summary>uv1: face uv (xy) + head/front gate (z). The per-pixel skin mask does the fine masking.</summary>
        public static List<Vector4> FaceUV(List<Vector3> V, List<Vector3> N, List<BoneWeight> W, GlbRigger.Landmarks lm, string id, out Frame f)
        {
            f = MakeFrame(lm, id);
            var uv = new List<Vector4>(V.Count);
            int head = (int)HBone.Head;
            for (int i = 0; i < V.Count; i++)
            {
                Vector3 p = V[i];
                var fu = GlbFaceMask.ToUV(p, f);
                float hw = 0f;
                if (i < W.Count)
                {
                    var w = W[i];
                    if (w.boneIndex0 == head) hw += w.weight0; if (w.boneIndex1 == head) hw += w.weight1;
                    if (w.boneIndex2 == head) hw += w.weight2; if (w.boneIndex3 == head) hw += w.weight3;
                }
                float front = Mathf.Clamp01((hw - 0.55f) / 0.2f) * Mathf.Clamp01((p.z - f.FaceZ) / 0.01f) * Mathf.Clamp01(N[i].z * 3f + 0.4f);
                uv.Add(new Vector4(fu.x, fu.y, front, 0f));
            }
            return uv;
        }

        /// <summary>Front z-buffer photo of the textured head + skin mask (decimated bind-pose mesh).</summary>
        public static GlbFaceMask.Img BuildImage(string id, List<Vector3> V, List<Vector2> UV, List<int> T, int[] triMat, Texture2D skinTex, GlbRigger.Landmarks lm, Frame f)
        {
            var L = MakeLayout(lm, id, f);
            var img = GlbFaceMask.Rasterize(V, UV, T, triMat, 0, skinTex, f, lm.ChinY - 0.05f);
            GlbFaceMask.Classify(img, L);
            GlbFaceMask.SaveDebug(img, id, L);
            return img;
        }

        /// <summary>Clean anime face shading: facial-skin normals are blended toward a smooth head ellipsoid (the scan's
        /// millimetre bumps otherwise break the toon ramp into blotches); hair strands keep their own normals.</summary>
        public static void SmoothFaceNormals(List<Vector3> V, List<Vector3> N, BoneWeight[] W, GlbRigger.Landmarks lm, Frame f, GlbFaceMask.Img img)
        {
            int R = img.N;
            // wide feather of the skin mask so the normal blend has no visible edge
            var soft = GlbFaceMask.Feather(img.Mask, R, Mathf.RoundToInt(0.004f / GlbFaceMask.PixelSize(img)));
            Vector3 e = lm.EyeCenter;
            Vector3 c = new Vector3(e.x, e.y - 0.02f, e.z - 0.07f);
            Vector3 inv = new Vector3(1f / (0.068f * 0.068f), 1f / (0.085f * 0.085f), 1f / (0.07f * 0.07f));
            int head = (int)HBone.Head, changed = 0;
            for (int i = 0; i < V.Count && i < W.Length; i++)
            {
                var w = W[i];
                float hw = (w.boneIndex0 == head ? w.weight0 : 0) + (w.boneIndex1 == head ? w.weight1 : 0) + (w.boneIndex2 == head ? w.weight2 : 0) + (w.boneIndex3 == head ? w.weight3 : 0);
                if (hw < 0.5f || V[i].z < f.FaceZ - 0.015f) continue;
                var uv = GlbFaceMask.ToUV(V[i], f);
                float m = GlbFaceMask.SampleBilinear(soft, R, uv);
                if (m <= 0.01f) continue;
                Vector3 ns = Vector3.Scale(V[i] - c, inv).normalized;
                if (Vector3.Dot(ns, N[i]) < -0.2f) continue;
                N[i] = Vector3.Lerp(N[i].normalized, ns, 0.85f * m).normalized;
                changed++;
            }
            Debug.Log($"[GlbFace] smoothed {changed} face normals");
        }

        public static readonly string[] ShapeNames = { "SmileL", "SmileR", "JawOpen", "BrowUp", "BrowDown", "CheekUp" };

        /// <summary>
        /// Procedural blend shapes moving the scanned face itself: mouth corners (smile, per side), jaw drop, brow raise,
        /// brow furrow and cheek raise. Displacements are limited to real facial skin (feathered skin mask) so the fringe
        /// never moves or tears.
        /// </summary>
        public static void AddBlendShapes(Mesh mesh, List<Vector3> V, BoneWeight[] W, GlbRigger.Landmarks lm, Frame f, GlbFaceMask.Img img, System.Text.StringBuilder log, string id, int limit = int.MaxValue)
        {
            var L = MakeLayout(lm, id, f);
            var c = L.C;
            int R = img.N;
            var soft = GlbFaceMask.Feather(img.Mask, R, Mathf.RoundToInt(0.003f / GlbFaceMask.PixelSize(img)));
            Vector3 W3(Vector2 uv) => new Vector3(f.Center.x - (uv.x - 0.5f) * f.Scale, f.Center.y + (uv.y - 0.5f) * f.Scale, 0f);
            Vector3 M = W3(L.Mouth);
            Vector3 eL = W3(L.EyeL), eR = W3(L.EyeR);
            float eyeTop = eL.y + c.EyeTop.y * 0.001f, eyeBot = eL.y + c.EyeBot.y * 0.001f;
            Vector3 hinge = new Vector3(0f, M.y + 0.025f, lm.HeadCenter.z - 0.035f);
            int head = (int)HBone.Head;
            int n = V.Count;
            var d = new Vector3[ShapeNames.Length][];
            for (int s = 0; s < d.Length; s++) d[s] = new Vector3[n];
            int moved = 0;
            float G(Vector2 q, Vector2 r) => Mathf.Exp(-(q.x * q.x / (r.x * r.x) + q.y * q.y / (r.y * r.y)));
            for (int i = 0; i < n && i < W.Length && i < limit; i++)
            {
                var w = W[i];
                float hw = (w.boneIndex0 == head ? w.weight0 : 0) + (w.boneIndex1 == head ? w.weight1 : 0) + (w.boneIndex2 == head ? w.weight2 : 0) + (w.boneIndex3 == head ? w.weight3 : 0);
                Vector3 p = V[i];
                if (hw < 0.3f || p.z < f.FaceZ - 0.01f) continue;
                float m = GlbFaceMask.SampleBilinear(soft, R, GlbFaceMask.ToUV(p, f));
                // the jaw also carries the chin / jaw line just outside the mask (still skin, never hair: hair is above)
                float jawSkin = p.y < M.y ? Mathf.Max(m, 0.6f * Mathf.Clamp01((M.y - p.y) / 0.01f)) : m;
                if (m <= 0.01f && jawSkin <= 0.01f) continue;
                moved++;
                float k = Mathf.Min(1f, hw);
                // smile: mouth corners up / out / back, per side
                for (int s = 0; s < 2; s++)
                {
                    float sx = s == 0 ? -1f : 1f;
                    Vector3 corner = new Vector3(M.x + sx * c.MouthHalf, M.y, 0f);
                    float g = G(new Vector2(p.x - corner.x, p.y - corner.y), new Vector2(0.011f, 0.009f));
                    float mid = G(new Vector2(p.x - M.x, p.y - M.y), new Vector2(0.008f, 0.004f)) * 0.35f;
                    d[s][i] = (new Vector3(sx * 0.0014f, 0.0024f, -0.0006f) * g + new Vector3(0f, 0.0006f, 0f) * mid) * m * k;
                }
                // jaw: rotate about a hinge in front of the ears, fading in below the mouth line and toward the jaw angle
                {
                    float below = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(M.y + 0.0015f, M.y - 0.012f, p.y));
                    float lat = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 0.06f, Mathf.Abs(p.x - M.x)));
                    float neck = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lm.ChinY - 0.004f, lm.ChinY - 0.03f, p.y));
                    Vector3 rel = p - hinge;
                    Vector3 rot = Quaternion.AngleAxis(9f, Vector3.right) * rel;
                    d[2][i] = (rot - rel) * below * lat * neck * jawSkin * k;
                }
                // brows (the skin above the eyes, under the fringe): raise / furrow
                for (int s = 0; s < 2; s++)
                {
                    Vector3 e = s == 0 ? eL : eR;
                    float sx = s == 0 ? -1f : 1f;
                    float up = G(new Vector2(p.x - e.x, p.y - (eyeTop + 0.008f)), new Vector2(0.022f, 0.012f));
                    float lid = G(new Vector2(p.x - e.x, p.y - (eyeTop + 0.001f)), new Vector2(0.02f, 0.004f)) * 0.35f;
                    d[3][i] += new Vector3(0f, 0.0026f, 0f) * (up + lid) * m * k;
                    Vector3 inner = new Vector3(e.x - sx * 0.012f, eyeTop + 0.006f, 0f);
                    float fu = G(new Vector2(p.x - inner.x, p.y - inner.y), new Vector2(0.014f, 0.01f));
                    d[4][i] += new Vector3(-sx * 0.0012f, -0.002f, 0.0004f) * fu * m * k;
                    // cheek raise (under the eyes) - pushes the lower lid up a little
                    Vector3 ch = new Vector3(e.x + sx * 0.004f, eyeBot - 0.012f, 0f);
                    float cg = G(new Vector2(p.x - ch.x, p.y - ch.y), new Vector2(0.016f, 0.011f));
                    d[5][i] += new Vector3(0f, 0.0016f, 0.0008f) * cg * m * k;
                }
            }
            var zero = new Vector3[n];
            for (int s = 0; s < ShapeNames.Length; s++) mesh.AddBlendShapeFrame(ShapeNames[s], 100f, d[s], zero, zero);
            log.AppendLine($"[{id}] face blend shapes: {string.Join(", ", ShapeNames)} on {moved} verts");
        }

        /// <summary>Paints the GLB face atlas / fx / mask from the scan and configures the face material.</summary>
        public static void Setup(CastDef def, ActorRig rig, SkinnedMeshRenderer smr, Material mat, Frame f, GlbRigger.Landmarks lm,
                                 GlbFaceMask.Img img, Texture2D skinTex, string dir, System.Text.StringBuilder log)
        {
            var L = MakeLayout(lm, def.Id, f);
            var painter = new FacePainter
            {
                Iris = ProcBuilder.Hex(def.Look.EyeColor, new Color(0.3f, 0.25f, 0.25f)),
                Hair = L.C.Brow ?? ProcBuilder.Hex(def.Look.HairColor, new Color(0.1f, 0.08f, 0.08f)),
                Skin = img.SkinRef,
                Sharp = L.C.Sharp >= 0 ? L.C.Sharp : def.Look.EyeSharp,
                Female = def.Gender == Gender.F,
            };
            var glbArt = GlbFaceMask.Analyze(img, L);
            var atlas = CharacterBaker.SaveTex(painter.PaintGlbAtlas(glbArt), dir + "/" + def.Id + "_FaceAtlas.png", 2048);
            var fx = CharacterBaker.SaveTex(painter.PaintGlbFx(glbArt), dir + "/" + def.Id + "_FaceFx.png", 1024);
            var maskTex = CharacterBaker.SaveTex(GlbFaceMask.MaskTexture(img), dir + "/" + def.Id + "_FaceMask.png", 1024, srgb: false);
            {
                var imp = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(dir + "/" + def.Id + "_FaceMask.png");
                imp.mipmapEnabled = false; imp.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed; imp.alphaIsTransparency = false;
                imp.SaveAndReimport();
                maskTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + def.Id + "_FaceMask.png");
            }
            if (skinTex != null) Object.DestroyImmediate(skinTex);
            mat.EnableKeyword("_FACE");
            mat.SetFloat("_FaceOn", 1f);
            mat.SetTexture("_FaceAtlas", atlas);
            mat.SetTexture("_FaceFx", fx);
            mat.SetTexture("_FaceMask", maskTex);
            // shader cell rects: eye (dx from centre, y, half w, half h), mouth (x, y, half w, half h)
            mat.SetVector("_FaceEye", new Vector4(0.5f - L.EyeL.x, L.EyeL.y, L.EyeHalf, L.EyeHalf));
            mat.SetVector("_FaceBrow", new Vector4(0.2f, L.EyeL.y + 0.1f, 0.05f, 0.02f));
            mat.SetVector("_FaceMouth", new Vector4(L.Mouth.x, L.Mouth.y, L.MouthHalf.x, L.MouthHalf.y));
            mat.SetVector("_FaceFxRect", new Vector4(0.5f, 0.5f, 0.5f, 0.5f));
            mat.SetColor("_FaceCover", new Color(img.SkinRef.r, img.SkinRef.g, img.SkinRef.b, 1f));
            UnityEditor.EditorUtility.SetDirty(mat);
            var face = rig.gameObject.AddComponent<ActorFace>();
            face.FaceRenderer = smr;
            face.FaceMaterialIndex = 0;
            rig.Face = face;
            log.AppendLine($"[{def.Id}] GLB face overlay: eyeL {L.EyeL} half {L.EyeHalf:F3} mouth {L.Mouth} half {L.MouthHalf} skin {img.SkinRef} lash {glbArt.Lash} masked {img.Mask.Count(m => m > 0.5f)} px");
        }

        /// <summary>Batch: renders Shots/glbface_P0x.png (photo | skin | strands | mask) + eye/mouth close-ups straight from the source scans.</summary>
        public static void DebugMasks()
        {
            var log = new System.Text.StringBuilder();
            var ids = new[] { "P01", "P02", "P04" };
            var cl = System.Environment.GetCommandLineArgs();
            for (int a = 0; a + 1 < cl.Length; a++) if (cl[a] == "-ids") ids = cl[a + 1].Split(',');
            foreach (var id in ids)
            {
                var def = Cast.Get(id);
                var scan = GlbRigger.LoadScan(def, log);
                var f = MakeFrame(scan.LM, id);
                var gm = scan.Glb.Materials[0];
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(scan.Glb.Images[gm.BaseColorImage]);
                var img = GlbFaceMask.Rasterize(scan.V, scan.UV, scan.T, scan.TM.ToArray(), 0, tex, f, scan.LM.ChinY - 0.05f);
                var L = MakeLayout(scan.LM, id, f);
                GlbFaceMask.Classify(img, L);
                GlbFaceMask.SaveDebug(img, id, L);
                var art = GlbFaceMask.Analyze(img, L);
                var painter = new FacePainter { Skin = img.SkinRef, Female = def.Gender == Gender.F };
                var atlas = painter.PaintGlbAtlas(art);
                GlbFaceMask.SavePreview(img, L, atlas, id);
                log.AppendLine($"{id}: materials {scan.Glb.Materials.Count} tex {tex.width}x{tex.height} eyeL {L.EyeL} half {L.EyeHalf} mouth {L.Mouth} mh {L.MouthHalf} skinRef {img.SkinRef} lash {art.Lash}");
                Object.DestroyImmediate(tex); Object.DestroyImmediate(atlas);
            }
            Debug.Log("[GlbFace] " + log);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/glbface.txt"), log.ToString());
        }
    }
}

namespace BL23.EditorTools.Characters
{
    public static partial class GlbFaceCalib
    {
        /// <summary>charpolish-fc F-a: true = the eye depth comes from the facial SKIN at the eyes (strands removed), false =
        /// the old front-most-vertex rule (a fringe strand in front of the nose bridge put the eye / face frame up to 5 cm
        /// in front of the face on P01 / P06 / P10 and gated every overlay off).</summary>
        public static bool SkinDepth = true;
        /// <summary>Last measurement per actor (m): skin z of the image-left eye, image-right eye, the value used, the old value.</summary>
        public static readonly System.Collections.Generic.Dictionary<string, Vector4> LastEyeZ = new System.Collections.Generic.Dictionary<string, Vector4>();

        /// <summary>Applies the per-model eye-centre correction to the detected landmarks (before the skeleton is built).</summary>
        public static void Apply(string id, GlbRigger.Landmarks lm, System.Collections.Generic.List<UnityEngine.Vector3> V)
        {
            if (!GlbFace.Calibration.TryGetValue(id, out var c)) return;
            var e = lm.EyeCenter + new UnityEngine.Vector3(c.EyeCX, c.EyeCY, 0);
            float z = float.MinValue;
            foreach (var v in V) if (UnityEngine.Mathf.Abs(v.x - e.x) < 0.01f && UnityEngine.Mathf.Abs(v.y - e.y) < 0.01f && v.z > z) z = v.z;
            float oldZ = z > float.MinValue ? z - 0.012f : e.z;
            e.z = oldZ;
            string rep = "";
            if (SkinDepth)
            {
                float skin = EyeSkinZ(V, e, c, lm.HeadCenter.z, out float zR, out float zL, out rep);
                if (!float.IsNaN(skin)) e.z = skin - 0.012f;
                LastEyeZ[id] = new Vector4(zR, zL, float.IsNaN(skin) ? float.NaN : skin, oldZ + 0.012f);
            }
            lm.EyeCenter = e;
            lm.ChinY = e.y + c.ChinDY;
            lm.NeckY = Mathf.Min(lm.NeckY, lm.ChinY - 0.03f);
            lm.HeadCenter.y = (lm.ChinY + lm.H) * 0.5f;
            lm.Report += $" eye(calibrated) {e} chin {lm.ChinY:F3} eyeZ old {oldZ:F4} new {e.z:F4} {rep}";
        }

        /// <summary>
        /// Skin surface depth at the eyes (charpolish-fc F-a): the front-most geometry per 1.5 mm cell of the eye band, a
        /// grey-scale opening (strands narrower than ~12 mm disappear), then per eye the 35th percentile inside the eye
        /// ellipse. A wide lock over one eye (P10) makes that eye read far in front: when the two eyes disagree by more than
        /// 6 mm the rear one is the skin. Returns NaN when the band is empty.
        /// </summary>
        public static float EyeSkinZ(System.Collections.Generic.List<Vector3> V, Vector3 e, GlbFace.Calib c, float headCz, out float zImgL, out float zImgR, out string rep)
        {
            const float cs = 0.0015f;
            float x0 = e.x - c.EyeDX - 0.032f, x1 = e.x + c.EyeDX + 0.032f;
            float yc = e.y + c.EyeDY, y0 = yc - 0.022f, y1 = yc + 0.022f;
            int nx = Mathf.CeilToInt((x1 - x0) / cs), ny = Mathf.CeilToInt((y1 - y0) / cs);
            var z = new float[nx * ny];
            for (int i = 0; i < z.Length; i++) z[i] = float.NaN;
            foreach (var v in V)
            {
                if (v.z < headCz || v.x < x0 || v.x >= x1 || v.y < y0 || v.y >= y1) continue;
                int k = Mathf.Min(ny - 1, (int)((v.y - y0) / cs)) * nx + Mathf.Min(nx - 1, (int)((v.x - x0) / cs));
                if (float.IsNaN(z[k]) || v.z > z[k]) z[k] = v.z;
            }
            int r = 4;   // 6 mm
            float[] Filt(float[] src, bool min)
            {
                var tmp = new float[src.Length]; var dst = new float[src.Length];
                for (int pass = 0; pass < 2; pass++)
                {
                    var a = pass == 0 ? src : tmp; var b = pass == 0 ? tmp : dst;
                    for (int y = 0; y < ny; y++)
                        for (int x = 0; x < nx; x++)
                        {
                            float best = float.NaN;
                            for (int d = -r; d <= r; d++)
                            {
                                int xx = pass == 0 ? x + d : x, yy = pass == 0 ? y : y + d;
                                if (xx < 0 || yy < 0 || xx >= nx || yy >= ny) continue;
                                float q = a[yy * nx + xx]; if (float.IsNaN(q)) continue;
                                if (float.IsNaN(best) || (min ? q < best : q > best)) best = q;
                            }
                            b[y * nx + x] = best;
                        }
                }
                return dst;
            }
            var op = Filt(Filt(z, true), false);
            float EyeZ(float cx)
            {
                var l = new System.Collections.Generic.List<float>();
                for (int y = 0; y < ny; y++)
                    for (int x = 0; x < nx; x++)
                    {
                        float px = x0 + (x + 0.5f) * cs, py = y0 + (y + 0.5f) * cs;
                        float u = (px - cx) / 0.014f, w = (py - yc) / 0.007f;
                        if (u * u + w * w > 1f) continue;
                        float q = op[y * nx + x]; if (!float.IsNaN(q)) l.Add(q);
                    }
                if (l.Count < 6) return float.NaN;
                l.Sort(); return l[(int)(l.Count * 0.35f)];
            }
            zImgL = EyeZ(e.x + c.EyeDX); zImgR = EyeZ(e.x - c.EyeDX);   // +X = character's right = image-left
            float res;
            if (float.IsNaN(zImgL)) res = zImgR;
            else if (float.IsNaN(zImgR)) res = zImgL;
            else res = Mathf.Abs(zImgL - zImgR) > 0.006f ? Mathf.Min(zImgL, zImgR) : (zImgL + zImgR) * 0.5f;
            rep = $"eyeSkin imgL {zImgL:F4} imgR {zImgR:F4} -> {res:F4}";
            return res;
        }
    }
}
