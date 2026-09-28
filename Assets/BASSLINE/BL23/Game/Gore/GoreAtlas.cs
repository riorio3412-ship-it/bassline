using System.Collections.Generic;
using BL23.Game.Mansion;
using UnityEngine;
using UnityEngine.Rendering;
using MS = BL23.Game.Mansion.S;

namespace BL23.Game
{
    /// <summary>
    /// The offline-baked blood atlas (Resources/Gore/gore_atlas.png, 2048², 4×4 cells of 512 px, baked by
    /// BL23Lab/goreatlas): A = shape, R = height, read by BL23/MansionDecal. Nothing is generated at runtime (no main-thread
    /// compression stall on the first murder). If the asset is missing, the ProcTex decal atlas stands in.
    /// Mesh convention for every gore decal: vertex colour = tint (alpha = opacity), UV channel 2 = (gloss, emissive, bump, 0),
    /// tangents required. Cell orientation: see the constants.
    /// </summary>
    public static class GoreAtlas
    {
        public const int Pool = 0, PoolSatellite = 1,
            Impact = 2,        // spatter cluster travelling toward +v
            Arterial = 3,      // pulses along +u, runs hanging toward -v
            CastOff = 4,       // line of drops along +u, getting smaller
            DripRun = 5,       // source at the top (+v), running down
            Handprint = 6,     // fingers +v
            Smear = 7,         // heavy at -u, fading toward +u
            NailScratch = 8,   // strokes along v
            Droplets = 9, Footprint = 10 /* toe +v */, Ooze = 11, SawKerf = 12 /* along u */, DrainRivulet = 13 /* flows +u */,
            Ripple = 14, Splat = 15;

        static Texture2D _tex; static Material _mat; static bool _tried, _fallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _tex = null; _mat = null; _tried = false; _fallback = false; }

        public static Texture2D Tex { get { Load(); return _tex; } }
        public static bool IsFallback { get { Load(); return _fallback; } }
        public static bool Loaded { get { Load(); return _tex != null; } }

        static void Load()
        {
            if (_tried) return; _tried = true;
            try { _tex = Resources.Load<Texture2D>("Gore/gore_atlas"); } catch { _tex = null; }
            if (_tex == null)
            {
                _fallback = true;
                try { _tex = MansionMats.Proc("DecalAtlas"); } catch { _tex = null; }
                Debug.LogWarning("[Gore] Resources/Gore/gore_atlas missing: using the ProcTex decal atlas");
            }
            else { _tex.wrapMode = TextureWrapMode.Clamp; }
        }

        /// <summary>UV rect of a cell (inset so mip levels never bleed from the neighbour).</summary>
        public static Rect Cell(int k)
        {
            Load();
            if (_fallback) return ProcTex.DecalRect(FallbackCell(k));
            k = Mathf.Clamp(k, 0, 15);
            const float s = 0.25f, pad = 4f / 2048f;
            int cx = k % 4, cy = k / 4;
            return new Rect(cx * s + pad, cy * s + pad, s - 2 * pad, s - 2 * pad);
        }

        static ProcTex.Decal FallbackCell(int k)
        {
            switch (k)
            {
                case Pool: case Ooze: return ProcTex.Decal.BloodPool;
                case DripRun: return ProcTex.Decal.BloodDrip;
                case Handprint: return ProcTex.Decal.Handprint;
                case Smear: case DrainRivulet: return ProcTex.Decal.BloodSmear;
                case NailScratch: case SawKerf: return ProcTex.Decal.Scratch;
                case Footprint: return ProcTex.Decal.FootShoe;
                case Ripple: return ProcTex.Decal.Water;
            }
            return ProcTex.Decal.BloodSplat;
        }

        /// <summary>BL23/MansionDecal with the gore atlas (a copy of the mansion decal slot material).</summary>
        public static Material Mat
        {
            get
            {
                if (_mat != null) return _mat;
                Load();
                MansionMats.Init();
                var src = MansionMats.Get(MS.Decal);
                _mat = src != null ? new Material(src) { name = "GoreDecal" } : null;
                if (_mat != null && _tex != null) _mat.SetTexture("_MainTex", _tex);
                if (_mat != null) { _mat.SetFloat("_Gloss", 0.5f); _mat.SetFloat("_Bump", 1.2f); }
                return _mat;
            }
        }

        // ------------------------------------------------------------------ shared quad building
        /// <summary>Reusable vertex streams for building decal meshes (no per-frame allocation; one mesh per build).</summary>
        public sealed class Batch
        {
            public readonly List<Vector3> V = new List<Vector3>(512);
            public readonly List<Vector3> N = new List<Vector3>(512);
            public readonly List<Vector4> T = new List<Vector4>(512);
            public readonly List<Vector2> UV = new List<Vector2>(512);
            public readonly List<Vector4> X = new List<Vector4>(512);
            public readonly List<Color> C = new List<Color>(512);
            public readonly List<int> I = new List<int>(768);
            public int Quads;
            public void Clear() { V.Clear(); N.Clear(); T.Clear(); UV.Clear(); X.Clear(); C.Clear(); I.Clear(); Quads = 0; }
            public bool Empty => V.Count == 0;

            /// <summary>An oriented quad lying on a surface. centre/normal/vAxis in the batch's space; u = cross(normal, vAxis)
            /// ... (right-handed on the surface), halfU/halfV in metres, custom = (gloss, emissive, bump).</summary>
            public void Quad(Vector3 centre, Vector3 normal, Vector3 vAxis, float halfU, float halfV, int cell, Color col, Vector3 custom, float lift = 0.003f)
            {
                normal.Normalize();
                vAxis = Vector3.ProjectOnPlane(vAxis, normal);
                if (vAxis.sqrMagnitude < 1e-8f) vAxis = Vector3.ProjectOnPlane(Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up, normal);
                vAxis.Normalize();
                var uAxis = Vector3.Cross(normal, vAxis).normalized;   // Unity (left-handed): up × forward = right
                var c = centre + normal * lift;
                var r = Cell(cell);
                int b = V.Count;
                V.Add(c - uAxis * halfU - vAxis * halfV); UV.Add(new Vector2(r.xMin, r.yMin));
                V.Add(c - uAxis * halfU + vAxis * halfV); UV.Add(new Vector2(r.xMin, r.yMax));
                V.Add(c + uAxis * halfU + vAxis * halfV); UV.Add(new Vector2(r.xMax, r.yMax));
                V.Add(c + uAxis * halfU - vAxis * halfV); UV.Add(new Vector2(r.xMax, r.yMin));
                var tan = new Vector4(uAxis.x, uAxis.y, uAxis.z, -1f);   // bitangent = cross(n, t) * w must point along +v
                var x = new Vector4(custom.x, custom.y, custom.z, 0f);
                col = GorePalette.Check(col);
                for (int i = 0; i < 4; i++) { N.Add(normal); T.Add(tan); X.Add(x); C.Add(col); }
                I.Add(b); I.Add(b + 1); I.Add(b + 2); I.Add(b); I.Add(b + 2); I.Add(b + 3);
                Quads++;
            }

            /// <summary>A quad whose colour is not blood (scratches on wood, dust): skips the crimson check.</summary>
            public void QuadRaw(Vector3 centre, Vector3 normal, Vector3 vAxis, float halfU, float halfV, int cell, Color col, Vector3 custom, float lift = 0.003f)
            {
                int n0 = C.Count; Quad(centre, normal, vAxis, halfU, halfV, cell, Color.black, custom, lift);
                for (int i = n0; i < C.Count; i++) C[i] = col;
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(V); m.SetNormals(N); m.SetTangents(T); m.SetUVs(0, UV); m.SetUVs(2, X); m.SetColors(C);
                m.SetTriangles(I, 0, true);
                return m;
            }
        }
    }
}
