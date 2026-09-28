using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>Runtime helpers: toon materials, procedural decal textures, small procedural meshes.</summary>
    public static class ToonRuntime
    {
        static Shader _toon;
        public static Shader Toon
        {
            get
            {
                if (_toon == null) _toon = Shader.Find("BL23/ToonCharacter");
                if (_toon == null) _toon = Shader.Find("Universal Render Pipeline/Lit");
                return _toon;
            }
        }

        public static Material Mat(Color c, float gloss = 0f, float outline = 1.6f)
        {
            var m = new Material(Toon);
            m.SetColor("_BaseColor", c);
            if (m.HasProperty("_GlossStrength")) m.SetFloat("_GlossStrength", gloss);
            if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", outline);
            return m;
        }

        static Material _decalTemplate;
        static bool _decalLoaded;

        public static Material DecalMat(Texture2D tex)
        {
            // a saved template (Resources/Actors/ToonDecal.mat, _ALPHATEST_ON) keeps the alpha-clip variant in player builds
            if (!_decalLoaded) { _decalLoaded = true; _decalTemplate = Resources.Load<Material>("Actors/ToonDecal"); }
            var m = _decalTemplate != null ? new Material(_decalTemplate) : new Material(Toon);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_OutlineWidth", 0f);
            m.SetFloat("_AlphaClip", 1f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cutoff", 0.35f);
            m.SetFloat("_RimStrength", 0.1f);
            m.SetFloat("_GlossStrength", 0.12f);
            m.SetFloat("_GlossThreshold", 0.975f);
            m.SetFloat("_ShadowReceive", 0.6f);
            m.renderQueue = 2460;
            return m;
        }

        // ---------------------------------------------------------------- procedural wound textures (cached)
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();

        static float Hash(int x, int y) { unchecked { int h = x * 374761393 + y * 668265263; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
        static float VNoise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), fx), Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), fx), fy);
        }
        static float Fbm(float x, float y) { float s = 0, a = 0.5f; for (int i = 0; i < 4; i++) { s += a * VNoise(x, y); x *= 2.03f; y *= 2.03f; a *= 0.5f; } return s; }

        public static Texture2D WoundTex(string kind)
        {
            if (_tex.TryGetValue(kind, out var t) && t != null) return t;
            int W = 128, H = 128;
            t = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "Wound_" + kind, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            Color deep = new Color(0.16f, 0.0f, 0.02f), blood = new Color(0.45f, 0.02f, 0.04f), flesh = new Color(0.78f, 0.36f, 0.36f), dry = new Color(0.25f, 0.06f, 0.04f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H * 2f - 1f;
                    float n = Fbm(x * 0.09f, y * 0.09f);
                    Color c = Color.clear;
                    switch (kind)
                    {
                        case "cut":
                        {
                            // long slit along u
                            float w = 0.16f * (1f - u * u) + 0.02f;
                            float d = Mathf.Abs(v + (n - 0.5f) * 0.15f);
                            float slit = Mathf.Clamp01((w - d) / 0.03f);
                            float rim = Mathf.Clamp01((w * 2.2f + 0.06f - d) / 0.04f) * (1f - u * u);
                            float spill = Mathf.Clamp01((0.35f * (1f - Mathf.Abs(u)) - Mathf.Max(0, -v) * 0.6f - d * 0.5f + (n - 0.5f) * 0.4f) / 0.05f) * (v < 0 ? 1f : 0.3f);
                            float a = Mathf.Max(slit, Mathf.Max(rim * 0.95f, spill * 0.9f));
                            Color col = Color.Lerp(blood, flesh, rim * (1f - slit) * 0.7f);
                            col = Color.Lerp(col, deep, slit);
                            c = new Color(col.r, col.g, col.b, a);
                            break;
                        }
                        case "gash":
                        {
                            float w = 0.28f * (1f - u * u * 0.9f) + 0.02f;
                            float d = Mathf.Abs(v + (n - 0.5f) * 0.2f);
                            float slit = Mathf.Clamp01((w - d) / 0.04f);
                            float rim = Mathf.Clamp01((w + 0.14f - d) / 0.05f) * (1f - u * u * 0.8f);
                            float drip = 0f;
                            for (int k = 0; k < 4; k++)
                            {
                                float cx = -0.6f + k * 0.4f + (Hash(k, 3) - 0.5f) * 0.2f;
                                float len = 0.35f + Hash(k, 7) * 0.6f;
                                drip = Mathf.Max(drip, Mathf.Clamp01((0.05f - Mathf.Abs(u - cx)) / 0.02f) * (v < 0 && v > -len ? 1f : 0f));
                            }
                            float a = Mathf.Max(slit, Mathf.Max(rim, drip));
                            Color col = Color.Lerp(blood, flesh, rim * (1f - slit) * 0.6f);
                            col = Color.Lerp(col, deep, slit);
                            c = new Color(col.r, col.g, col.b, a);
                            break;
                        }
                        case "stab":
                        {
                            float r = Mathf.Sqrt(u * u * 2.2f + v * v * 0.8f);
                            float hole = Mathf.Clamp01((0.22f - r) / 0.05f);
                            float ring = Mathf.Clamp01((0.55f + (n - 0.5f) * 0.5f - r) / 0.06f);
                            float drip = Mathf.Clamp01((0.07f - Mathf.Abs(u + (n - 0.5f) * 0.1f)) / 0.03f) * (v < 0 && v > -0.9f ? 1f : 0f);
                            float a = Mathf.Max(hole, Mathf.Max(ring * 0.95f, drip * 0.9f));
                            Color col = Color.Lerp(blood, deep, hole);
                            c = new Color(col.r, col.g, col.b, a);
                            break;
                        }
                        case "burn":
                        {
                            float r = Mathf.Sqrt(u * u + v * v) + (n - 0.5f) * 0.5f;
                            float a = Mathf.Clamp01((0.8f - r) / 0.1f);
                            Color col = Color.Lerp(new Color(0.08f, 0.05f, 0.04f), new Color(0.55f, 0.18f, 0.12f), Mathf.Clamp01(r * 1.4f) * n);
                            c = new Color(col.r, col.g, col.b, a);
                            break;
                        }
                        default: // splash
                        {
                            float r = Mathf.Sqrt(u * u + v * v) + (n - 0.5f) * 0.7f;
                            float a = Mathf.Clamp01((0.6f - r) / 0.06f);
                            c = new Color(dry.r, dry.g, dry.b, a);
                            break;
                        }
                    }
                    px[y * W + x] = c;
                }
            t.SetPixels(px);
            t.Apply(true, false);
            _tex[kind] = t;
            return t;
        }

        /// <summary>Slightly curved quad (bulges along +Z) centered at origin, size in meters, facing +Z.</summary>
        public static Mesh Patch(float w, float h, float bulge = 0.002f)
        {
            var m = new Mesh { name = "Patch" };
            int nx = 6, ny = 4;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var tri = new List<int>();
            for (int y = 0; y <= ny; y++)
                for (int x = 0; x <= nx; x++)
                {
                    float u = x / (float)nx, vv = y / (float)ny;
                    float px = (u - 0.5f) * w, py = (vv - 0.5f) * h;
                    float z = bulge * (1f - (2 * u - 1) * (2 * u - 1)) * (1f - (2 * vv - 1) * (2 * vv - 1));
                    v.Add(new Vector3(px, py, z)); uv.Add(new Vector2(u, vv)); n.Add(Vector3.forward);
                }
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    int i = y * (nx + 1) + x;
                    tri.Add(i); tri.Add(i + nx + 1); tri.Add(i + 1);
                    tri.Add(i + 1); tri.Add(i + nx + 1); tri.Add(i + nx + 2);
                }
            m.SetVertices(v); m.SetUVs(0, uv); m.SetNormals(n); m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Lathe mesh around +Y from a profile (x = radius, y = height); optional front opening angle and hood cap.</summary>
        public static Mesh Lathe(IList<Vector2> profile, int seg, float openFrontDeg, bool doubleSided)
        {
            var m = new Mesh { name = "Lathe" };
            var v = new List<Vector3>(); var tri = new List<int>(); var uv = new List<Vector2>();
            float a0 = openFrontDeg * 0.5f * Mathf.Deg2Rad, a1 = Mathf.PI * 2f - a0;
            for (int i = 0; i < profile.Count; i++)
                for (int s = 0; s <= seg; s++)
                {
                    float a = Mathf.Lerp(a0, a1, s / (float)seg);
                    v.Add(new Vector3(Mathf.Sin(a) * profile[i].x, profile[i].y, Mathf.Cos(a) * profile[i].x * 0.82f));
                    uv.Add(new Vector2(s / (float)seg, i / (float)(profile.Count - 1)));
                }
            for (int i = 0; i + 1 < profile.Count; i++)
                for (int s = 0; s < seg; s++)
                {
                    int a = i * (seg + 1) + s, b = a + 1, c = a + seg + 1, d = c + 1;
                    tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(b); tri.Add(d); tri.Add(c);
                    if (doubleSided) { tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(b); tri.Add(c); tri.Add(d); }
                }
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            // outline normals in tangents
            var nrm = m.normals; var tan = new Vector4[nrm.Length];
            for (int i = 0; i < nrm.Length; i++) tan[i] = new Vector4(nrm[i].x, nrm[i].y, nrm[i].z, 1);
            m.tangents = tan;
            return m;
        }
    }
}
