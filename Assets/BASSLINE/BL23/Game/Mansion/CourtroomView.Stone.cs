using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// The well's masonry. A procedural coursed-ashlar texture (0.6 m courses of dressed blocks 0.85-1.55 m long, fine joints,
    /// worn arrises, grime running down the face) and a world-space mapping for every stone face of the well: the courses run
    /// level all the way round and all the way up (the eye counts them, so they are the scale ruler), nothing stretches along
    /// an arc and nothing repeats bay by bay. The far stone uses a low-contrast copy, so the joints fade with the air.
    /// </summary>
    public sealed partial class CourtroomView
    {
        const float AshW = 3.0f, AshH = 2.4f;     // metres per texture tile: 4 courses of 0.6 m
        static Texture2D _ashlarNear, _ashlarFar;
        static float _ashlarAvg = 0.34f;

        static float VNoise(float x, float y, int px, int py, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int x0 = ((xi % px) + px) % px, x1 = (x0 + 1) % px, y0 = ((yi % py) + py) % py, y1 = (y0 + 1) % py;
            float a = Hash(x0 * 7919 + y0 * 104729, seed), b = Hash(x1 * 7919 + y0 * 104729, seed), c = Hash(x0 * 7919 + y1 * 104729, seed), d = Hash(x1 * 7919 + y1 * 104729, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static void EnsureAshlar()
        {
            if (_ashlarNear != null && _ashlarFar != null) return;
            const int N = 512, C = 4; int ch = N / C;
            var rnd = new System.Random(7717);
            float R() => (float)rnd.NextDouble();
            var cuts = new int[C][]; var vals = new float[C][]; var offs = new int[C];
            for (int k = 0; k < C; k++)
            {
                var cl = new List<int> { 0 }; float x = 0f, full = N;
                while (true)
                {
                    float rem = full - x;
                    if (rem < 2.3f / AshW * N) { if (rem > 1.7f / AshW * N) cl.Add((int)(x + rem * Mathf.Lerp(0.4f, 0.6f, R()))); break; }
                    x += Mathf.Lerp(0.85f, 1.55f, R()) / AshW * N; cl.Add((int)x);
                }
                cl.Add(N); cuts[k] = cl.ToArray();
                vals[k] = new float[cl.Count]; for (int j = 0; j < vals[k].Length; j++) vals[k][j] = Mathf.Lerp(0.29f, 0.41f, R());
                offs[k] = rnd.Next(N);
            }
            var near = new Color32[N * N]; var lin = new float[N * N]; double sum = 0;
            for (int y = 0; y < N; y++)
            {
                int k = y / ch, ly = y % ch; float dyJ = Mathf.Min(ly + 0.5f, ch - 0.5f - ly);
                var cl = cuts[k];
                for (int x = 0; x < N; x++)
                {
                    int xs = (x - offs[k] + N) % N; int j = 0; while (j < cl.Length - 2 && xs >= cl[j + 1]) j++;
                    float dxJ = Mathf.Min(xs - cl[j] + 0.5f, cl[j + 1] - xs - 0.5f);
                    float u = x / (float)N, v = y / (float)N;
                    float n1 = VNoise(u * 16f, v * 16f, 16, 16, 3), n2 = VNoise(u * 48f, v * 48f, 48, 48, 5);
                    float n = n1 * 0.65f + n2 * 0.35f;
                    float val;
                    if (dyJ < 2.4f || dxJ < 2.2f) val = 0.085f * (0.8f + 0.4f * n);                              // mortar joint
                    else
                    {
                        float bv = vals[k][j] * (0.95f + 0.1f * (ly / (float)ch));                                  // blocks catch a little more light at their upper edge
                        val = bv * (0.86f + 0.28f * n);
                        float e = Mathf.Min(dxJ, dyJ);
                        if (e < 7f) val *= Mathf.Lerp(0.7f + 0.2f * VNoise(u * 90f, v * 90f, 90, 90, 9), 1f, e / 7f);   // worn, chipped arrises
                        float streak = VNoise(u * 38f, v * 2.5f, 38, 3, 11);                                         // grime run down the face
                        val *= 0.84f + 0.16f * Mathf.SmoothStep(0.25f, 0.85f, streak);
                    }
                    lin[y * N + x] = val; sum += val;
                }
            }
            _ashlarAvg = (float)(sum / (N * N));
            var far = new Color32[N * N];
            for (int i = 0; i < lin.Length; i++)
            {
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(lin[i] * 255f), 0, 255); near[i] = new Color32(b, b, b, 255);
                float fv = _ashlarAvg + 0.42f * (lin[i] - _ashlarAvg); byte bf = (byte)Mathf.Clamp(Mathf.RoundToInt(fv * 255f), 0, 255); far[i] = new Color32(bf, bf, bf, 255);
            }
            _ashlarNear = MakeTex("court_ashlar", near, N); _ashlarFar = MakeTex("court_ashlar_far", far, N);
        }

        static Texture2D MakeTex(string name, Color32[] px, int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            t.SetPixels32(px); t.Apply(true, true); return t;
        }

        /// <summary>The island's own stone (parapet, the breach's stub piers, the promontory's broken lip) is lit, but cut from the
        /// same dressed ashlar as the well around it, not the house's rubble fieldstone: one material swapped in by reference.</summary>
        static Material _litAshlar;
        void DressIslandStone(MeshRenderer mr)
        {
            if (mr == null) return;
            EnsureAshlar();
            var src = MansionMats.Get(S.StoneWall);
            if (_litAshlar == null)
            {
                _litAshlar = new Material(src) { name = "CourtAshlarLit" };
                _litAshlar.SetTexture("_BaseMap", _ashlarNear);
                _litAshlar.SetVector("_Tiling", new Vector4(1f / AshW, 1f / AshH, 0f, 0f));
                _litAshlar.DisableKeyword("_NORMALMAP"); _litAshlar.SetFloat("_UseNormal", 0f);
                _litAshlar.DisableKeyword("_MASKMAP"); _litAshlar.SetFloat("_UseMask", 0f);
                _litAshlar.SetFloat("_Roughness", 0.85f); _litAshlar.SetFloat("_RoughnessBias", 0f); _litAshlar.SetFloat("_Metallic", 0f);
                _litAshlar.SetColor("_BaseColor", new Color(0.42f, 0.42f, 0.42f, 1f));   // the ashlar texture is stored linear (brighter than the sRGB fieldstone)
            }
            var sm = mr.sharedMaterials; bool any = false;
            for (int i = 0; i < sm.Length; i++) if (sm[i] == src) { sm[i] = _litAshlar; any = true; }
            if (any) mr.sharedMaterials = sm;
        }

        /// <summary>World-space masonry mapping (metres): level faces take (x, z); upright faces run u around the well (arc length
        /// on a circumference rounded to whole tiles, the seam behind the lift) and v straight up, so every course lines up.
        /// A vertex is duplicated only where two faces disagree on its uv.</summary>
        void WorldUV(Mesh m)
        {
            var vs = new List<Vector3>(); m.GetVertices(vs);
            var ns = new List<Vector3>(); m.GetNormals(ns);
            var cs = new List<Color32>(); m.GetColors(cs);
            var xs = new List<Vector4>(); m.GetUVs(2, xs);
            int n0 = vs.Count; if (n0 == 0) return;
            bool hasN = ns.Count == n0, hasC = cs.Count == n0, hasX = xs.Count == n0;
            var uv = new List<Vector2>(n0); var done = new List<bool>(n0);
            for (int i = 0; i < n0; i++) { uv.Add(Vector2.zero); done.Add(false); }
            int subs = m.subMeshCount; var tris = new List<int>[subs];
            for (int s = 0; s < subs; s++) { tris[s] = new List<int>(); m.GetTriangles(tris[s], s); }
            float thL = _liftTh * Mathf.Deg2Rad; const float Tol2 = 0.03f * 0.03f;
            for (int s = 0; s < subs; s++)
            {
                var t = tris[s];
                for (int i = 0; i + 2 < t.Count; i += 3)
                {
                    Vector3 a = vs[t[i]], b = vs[t[i + 1]], c = vs[t[i + 2]];
                    var nf = Vector3.Cross(b - a, c - a); if (nf.sqrMagnitude < 1e-14f) continue; nf.Normalize();
                    bool level = Mathf.Abs(nf.y) > 0.7f;
                    Vector3 tu = Vector3.zero, tv = Vector3.up, cen = (a + b + c) / 3f; float u0 = 0f; bool around = false;
                    if (!level)
                    {
                        var nh = new Vector3(nf.x, 0f, nf.z).normalized;
                        tu = Vector3.Cross(Vector3.up, nh);
                        tv = Vector3.Cross(nf, tu).normalized; if (tv.y < 0f) tv = -tv;
                        var q = cen - _c; q.y = 0f; float rr = q.magnitude;
                        if (rr > 0.5f)
                        {
                            var tan = Vector3.Cross(Vector3.up, q / rr);
                            float d = Vector3.Dot(tu, tan);
                            if (Mathf.Abs(d) > 0.5f)
                            {
                                around = true;
                                float th = Mathf.Atan2(Vector3.Dot(q, _rt), Vector3.Dot(q, _jd));
                                float phi = Mathf.Repeat(th - thL, Mathf.PI * 2f);
                                float rEff = Mathf.Max(1f, Mathf.Round(2f * Mathf.PI * rr / AshW)) * AshW / (2f * Mathf.PI);
                                u0 = (d >= 0f ? 1f : -1f) * phi * rEff;
                            }
                        }
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        int idx = t[i + k]; var p = vs[idx];
                        Vector2 w = level ? new Vector2(p.x - _c.x, p.z - _c.z)
                                  : new Vector2(around ? u0 + Vector3.Dot(p - cen, tu) : Vector3.Dot(p - _c, tu), Vector3.Dot(p - _c, tv));
                        if (!done[idx]) { done[idx] = true; uv[idx] = w; continue; }
                        if ((uv[idx] - w).sqrMagnitude <= Tol2) continue;
                        // this face wants another uv here: its own copy of the vertex
                        vs.Add(p); uv.Add(w); done.Add(true);
                        if (hasN) ns.Add(ns[idx]); if (hasC) cs.Add(cs[idx]); if (hasX) xs.Add(xs[idx]);
                        t[i + k] = vs.Count - 1;
                    }
                }
            }
            if (vs.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(vs); if (hasN) m.SetNormals(ns); if (hasC) m.SetColors(cs); if (hasX) m.SetUVs(2, xs);
            m.SetUVs(0, uv);
            for (int s = 0; s < subs; s++) m.SetTriangles(tris[s], s, false);
            m.RecalculateBounds();
        }
    }
}
