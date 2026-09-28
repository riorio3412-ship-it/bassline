using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>Owns disguise pieces parented to bones; destroying the root removes them.</summary>
    public class DisguiseParts : MonoBehaviour
    {
        public List<GameObject> Parts = new List<GameObject>();
        void OnDestroy() { foreach (var p in Parts) if (p != null) Destroy(p); }
    }

    /// <summary>Runtime disguises: "TheaterMask" (porcelain mask on the face), "Cloak" (hooded black cloak), "Raincoat" (hooded clear-yellow raincoat).</summary>
    public static class DisguiseBuilder
    {
        public static GameObject Build(ActorRig rig, string type)
        {
            var root = new GameObject("Disguise_" + type);
            root.transform.SetParent(rig.transform, false);
            _parts = root.AddComponent<DisguiseParts>();
            float s = rig.Height / 1.75f;
            string t = (type ?? "").ToLowerInvariant();
            if (t.Contains("mask")) type = "TheaterMask";
            else if (t.Contains("rain")) type = "Raincoat";
            else if (t.Contains("cloak") || t.Contains("cape") || t.Contains("guise")) type = "Cloak";
            switch (type)
            {
                case "TheaterMask": Mask(rig, root, s); break;
                case "Cloak": Cloak(rig, root, s, new Color(0.06f, 0.05f, 0.07f), new Color(0.35f, 0.05f, 0.1f), 0.06f, false); break;
                case "Raincoat": Cloak(rig, root, s, new Color(0.93f, 0.86f, 0.42f), new Color(0.95f, 0.9f, 0.55f), 0.03f, true); break;
                default: Cloak(rig, root, s, new Color(0.1f, 0.1f, 0.12f), new Color(0.2f, 0.2f, 0.22f), 0.05f, false); break;
            }
            _parts = null;
            return root;
        }

        static DisguiseParts _parts;
        static void Track(GameObject g) { if (_parts != null) _parts.Parts.Add(g); }

        static void Mask(ActorRig rig, GameObject root, float s)
        {
            var head = rig.Bone(HBone.Head);
            var go = new GameObject("TheaterMask");
            Track(go);
            go.transform.SetParent(head, false);
            Vector3 eyeLocal = head.InverseTransformPoint(rig.EyeAnchor.position);
            go.transform.localPosition = eyeLocal + new Vector3(0, -0.012f * s, 0.022f * s);
            go.transform.localRotation = Quaternion.identity;
            var mesh = MaskMesh(0.085f * s, 0.12f * s, 0.05f * s);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mat = ToonRuntime.Mat(new Color(0.97f, 0.95f, 0.92f), 0.6f, 1.4f);
            mat.SetTexture("_BaseMap", MaskTex());
            mat.SetFloat("_GlossThreshold", 0.94f);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static Texture2D _maskTex;
        static Texture2D MaskTex()
        {
            if (_maskTex != null) return _maskTex;
            int W = 256, H = 256;
            _maskTex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "TheaterMask" };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W * 2 - 1, v = y / (float)H * 2 - 1;
                    Color c = new Color(0.98f, 0.97f, 0.95f);
                    // eye holes (dark), smiling mouth, gold trim, tear line
                    for (int e = -1; e <= 1; e += 2)
                    {
                        float ex = u - e * 0.36f, ey = v - 0.18f;
                        float d = Mathf.Sqrt(ex * ex * 1.1f + ey * ey * 3.5f);
                        if (d < 0.2f) c = new Color(0.04f, 0.02f, 0.05f);
                        else if (d < 0.24f) c = new Color(0.85f, 0.65f, 0.25f);
                    }
                    float my = v + 0.45f - u * u * 0.6f;
                    if (Mathf.Abs(my) < 0.035f && Mathf.Abs(u) < 0.42f) c = new Color(0.05f, 0.02f, 0.05f);
                    if (Mathf.Abs(u + 0.36f) < 0.018f && v < 0.0f && v > -0.35f) c = new Color(0.35f, 0.05f, 0.35f);
                    float edge = Mathf.Sqrt(u * u + v * v * 0.85f);
                    if (edge > 0.93f) c = new Color(0.85f, 0.65f, 0.25f);
                    px[y * W + x] = c;
                }
            _maskTex.SetPixels(px); _maskTex.Apply(true);
            return _maskTex;
        }

        static Mesh MaskMesh(float hw, float hh, float depth)
        {
            var m = new Mesh { name = "Mask" };
            int nx = 20, ny = 24;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int y = 0; y <= ny; y++)
                for (int x = 0; x <= nx; x++)
                {
                    float u = x / (float)nx * 2 - 1, w = y / (float)ny * 2 - 1;
                    // oval outline squeezed at the chin
                    float width = Mathf.Sqrt(Mathf.Max(0, 1 - w * w)) * (w < 0 ? Mathf.Lerp(0.6f, 1f, 1 + w) : 1f);
                    float px = u * width * hw, py = w * hh;
                    float pz = depth * (1 - u * u * width * width) * 0.9f - depth * 0.6f * w * w;
                    v.Add(new Vector3(px, py, pz)); uv.Add(new Vector2(u * width * 0.5f + 0.5f, w * 0.5f + 0.5f));
                }
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    int i = y * (nx + 1) + x;
                    tri.Add(i); tri.Add(i + nx + 1); tri.Add(i + 1);
                    tri.Add(i + 1); tri.Add(i + nx + 1); tri.Add(i + nx + 2);
                }
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            var nrm = m.normals; var tan = new Vector4[nrm.Length];
            for (int i = 0; i < nrm.Length; i++) tan[i] = new Vector4(nrm[i].x, nrm[i].y, nrm[i].z, 1);
            m.tangents = tan;
            return m;
        }

        static void Cloak(ActorRig rig, GameObject root, float s, Color c, Color lining, float gloss, bool rain)
        {
            var chest = rig.Bone(HBone.Chest);
            var head = rig.Bone(HBone.Head);
            var hips = rig.Bone(HBone.Hips);
            var mat = ToonRuntime.Mat(c, rain ? 0.6f : 0.05f, 1.6f);
            if (rain) mat.SetFloat("_GlossThreshold", 0.92f);
            var inner = ToonRuntime.Mat(lining, 0f, 0f);
            float H = rig.Height;
            float shoulderY = chest.InverseTransformPoint(rig.Bone(HBone.UpperArmL).position).y;
            float sw = Mathf.Abs(chest.InverseTransformPoint(rig.Bone(HBone.UpperArmL).position).x) + 0.07f * s;
            // upper cape (rigid on chest)
            var up = new GameObject("CloakUpper");
            Track(up);
            up.transform.SetParent(chest, false);
            float chestToHips = chest.position.y - hips.position.y;
            var prof = new List<Vector2> { new Vector2(0.09f * s, shoulderY + 0.07f * s), new Vector2(sw * 0.9f, shoulderY + 0.02f * s), new Vector2(sw + 0.03f * s, shoulderY - 0.1f * s), new Vector2(sw + 0.05f * s, -chestToHips * 0.4f) };
            up.AddComponent<MeshFilter>().sharedMesh = ToonRuntime.Lathe(prof, 28, rain ? 20f : 26f, true);
            up.AddComponent<MeshRenderer>().sharedMaterials = new[] { mat };
            // lower cape (rigid on hips, wide so the legs stay inside)
            var lo = new GameObject("CloakLower");
            Track(lo);
            lo.transform.SetParent(hips, false);
            float hipsH = hips.position.y - rig.transform.position.y;
            float hemY = rain ? -hipsH * 0.45f : -hipsH + 0.08f * s;
            float topY = chest.position.y - hips.position.y - chestToHips * 0.35f;
            var prof2 = new List<Vector2> { new Vector2(sw + 0.05f * s, topY), new Vector2(sw + 0.08f * s, 0f), new Vector2(sw + 0.14f * s, hemY * 0.5f), new Vector2(sw + (rain ? 0.14f : 0.22f) * s, hemY) };
            lo.AddComponent<MeshFilter>().sharedMesh = ToonRuntime.Lathe(prof2, 28, rain ? 24f : 34f, true);
            lo.AddComponent<MeshRenderer>().sharedMaterials = new[] { mat };
            // hood (on head)
            var hood = new GameObject("Hood");
            Track(hood);
            hood.transform.SetParent(head, false);
            Vector3 top = head.InverseTransformPoint(rig.HeadTopAnchor.position);
            float hr = 0.15f * s;
            var hp = new List<Vector2>();
            for (int i = 0; i <= 10; i++)
            {
                float a = i / 10f * Mathf.PI * 0.95f;
                hp.Add(new Vector2(Mathf.Sin(a) * hr * 1.05f + 0.005f, top.y + 0.03f * s - (1 - Mathf.Cos(a)) * hr * 1.25f));
            }
            var hm = ToonRuntime.Lathe(hp, 24, 110f, true);
            hood.AddComponent<MeshFilter>().sharedMesh = hm;
            hood.AddComponent<MeshRenderer>().sharedMaterials = new[] { mat };
            hood.transform.localPosition = new Vector3(0, 0, -0.015f * s);
            if (!rain)
            {
                var clasp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Track(clasp);
                Object.Destroy(clasp.GetComponent<Collider>());
                clasp.transform.SetParent(chest, false);
                clasp.transform.localPosition = new Vector3(0, shoulderY + 0.02f * s, 0.1f * s);
                clasp.transform.localScale = Vector3.one * 0.025f * s;
                clasp.GetComponent<MeshRenderer>().sharedMaterial = ToonRuntime.Mat(new Color(0.8f, 0.62f, 0.25f), 1f, 0f);
            }
        }
    }
}
