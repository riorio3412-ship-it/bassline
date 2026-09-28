using System.Collections.Generic;
using BL23.Game.Mansion;
using UnityEngine;
using UnityEngine.Rendering;
using MS = BL23.Game.Mansion.S;

namespace BL23.Game
{
    /// <summary>
    /// The cut face. Normally <see cref="Caps"/>: each open loop the cut left in the clothes and skin is closed with a fan
    /// from its centre (so a sleeve, a collar or a trouser leg is never a hollow shell), set 2–3 mm inside the opening, with a
    /// narrow blood-soaked band on the cloth just behind the edge. The cross-section is dark, wet crimson with muscle grain
    /// and only a hint of bone, on a copy of the victim's own toon material (outline off, face painting off) so it takes the
    /// same light as the body. <see cref="Build"/> (a slightly domed disc) remains for bodies whose meshes cannot be read.
    /// Settings.Gore: 2 full, 1 darker and no bone, 0 wrapped in dark linen.
    /// </summary>
    public static class GoreStump
    {
        static readonly Texture2D[] _tex = new Texture2D[3];
        static Mesh _disc;
        static readonly Dictionary<(Material, int), Material> _mats = new Dictionary<(Material, int), Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { for (int i = 0; i < 3; i++) _tex[i] = null; _disc = null; _mats.Clear(); for (int i = 0; i < 3; i++) _skirts[i] = null; }

        /// <summary>The victim material the caps are made from: a body material if there is one, else the only one there
        /// is (a scanned actor's single material also paints the face; the copy turns that off).</summary>
        public static Material SkinSource(IEnumerable<Material> mats)
        {
            Material face = null;
            if (mats == null) return null;
            foreach (var m in mats)
            {
                if (m == null || !m.HasProperty("_BaseMap")) continue;
                if (!m.IsKeywordEnabled("_FACE")) return m;
                if (face == null) face = m;
            }
            return face;
        }

        // ================================================================== caps over real cut loops
        /// <summary>A cut's loops in world space (already in the drawn pose) and which way the missing part ran.</summary>
        public sealed class CutLoops
        {
            public readonly List<List<Vector3>> Loops = new List<List<Vector3>>();
            public Vector3 Outward = Vector3.up;   // from the stump toward where the part was (piece: away from the piece)
        }

        public struct CapInfo { public Vector3 Centre, Normal; public float Radius; public int Loops, Tris; }

        /// <summary>Close each loop with a fan from its centre, inset along the cap normal; optionally a soaked band on the cloth
        /// behind the edge. Built under 'parent' in its local space (the mesh follows the bone). Returns null when nothing
        /// usable was given.</summary>
        public static GameObject Caps(Transform parent, CutLoops cut, int level, Material skinSrc, bool soakBand, string name, out CapInfo info)
        {
            info = default;
            if (cut == null || cut.Loops.Count == 0) return null;
            level = Mathf.Clamp(level, 0, 2);
            var V = new List<Vector3>(256); var N = new List<Vector3>(256); var UV = new List<Vector2>(256); var T = new List<int>(768); var TG = new List<Vector4>(256);
            var bV = new List<Vector3>(256); var bN = new List<Vector3>(256); var bC = new List<Color>(256); var bT = new List<int>(768); var bX = new List<Vector4>(256); var bTg = new List<Vector4>(256); var bUV = new List<Vector2>(256);
            var cell = GoreAtlas.Cell(GoreAtlas.Pool).center;
            // biggest loop first: it is the outer layer (coat over shirt over skin) and sits nearest the opening
            var order = new List<int>(); for (int i = 0; i < cut.Loops.Count; i++) order.Add(i);
            var perim = new float[cut.Loops.Count];
            for (int i = 0; i < cut.Loops.Count; i++) perim[i] = Perimeter(cut.Loops[i]);
            order.Sort((a, b) => perim[b].CompareTo(perim[a]));
            Vector3 cSum = Vector3.zero, nSum = Vector3.zero; float rMax = 0f; int used = 0;
            for (int oi = 0; oi < order.Count && used < 10; oi++)
            {
                var L = cut.Loops[order[oi]]; if (L == null || L.Count < 4 || perim[order[oi]] < 0.02f) continue;
                // centre, plane (Newell), size
                Vector3 c = Vector3.zero; foreach (var p in L) c += p; c /= L.Count;
                Vector3 n = Vector3.zero;
                for (int i = 0; i < L.Count; i++) { var a = L[i]; var b = L[(i + 1) % L.Count]; n.x += (a.y - b.y) * (a.z + b.z); n.y += (a.z - b.z) * (a.x + b.x); n.z += (a.x - b.x) * (a.y + b.y); }
                if (n.sqrMagnitude < 1e-10f) n = cut.Outward;
                n.Normalize(); if (Vector3.Dot(n, cut.Outward) < 0f) n = -n;
                // a loop whose plane lies along the limb (a torn flap, a hair card) is not an opening facing out: leave it
                if (Vector3.Dot(n, cut.Outward.normalized) < 0.25f && used > 0) continue;
                var e1 = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var e2 = Vector3.Cross(n, e1);
                float R = 0f, Rm = 0f; foreach (var p in L) { float d = Vector3.ProjectOnPlane(p - c, n).magnitude; R = Mathf.Max(R, d); Rm += d; }
                Rm /= L.Count; if (R < 0.004f) continue;
                float inset = 0.0022f + 0.0016f * used;   // inner layers a little deeper: never two caps in one plane
                Vector3 In(Vector3 p) => p - n * inset;
                // fan
                int b0 = V.Count;
                var cc = In(c) + n * (0.07f * Rm);        // the cut face bulges a little
                V.Add(parent.InverseTransformPoint(cc)); N.Add(parent.InverseTransformDirection(n)); UV.Add(new Vector2(0.5f, 0.5f));
                var tan = parent.InverseTransformDirection(e1); TG.Add(new Vector4(tan.x, tan.y, tan.z, 1f));
                for (int i = 0; i < L.Count; i++)
                {
                    var p = In(L[i]); var d = p - cc;
                    V.Add(parent.InverseTransformPoint(p));
                    var radial = Vector3.ProjectOnPlane(L[i] - c, n); var nn = (n + (radial.sqrMagnitude > 1e-8f ? radial.normalized * 0.28f : Vector3.zero)).normalized;
                    N.Add(parent.InverseTransformDirection(nn));
                    UV.Add(new Vector2(0.5f + 0.5f * Vector3.Dot(L[i] - c, e1) / R, 0.5f + 0.5f * Vector3.Dot(L[i] - c, e2) / R));
                    TG.Add(new Vector4(tan.x, tan.y, tan.z, 1f));
                }
                for (int i = 0; i < L.Count; i++)
                {
                    int ia = b0 + 1 + i, ib = b0 + 1 + (i + 1) % L.Count;
                    // front face = clockwise seen from outside: Cross(b − a, c − a) must point along n
                    var fa = In(L[i]); var fb = In(L[(i + 1) % L.Count]);
                    if (Vector3.Dot(Vector3.Cross(fa - cc, fb - cc), n) >= 0f) { T.Add(b0); T.Add(ia); T.Add(ib); }
                    else { T.Add(b0); T.Add(ib); T.Add(ia); }
                }
                // soaked band: the cloth just behind the edge, crimson at the cut fading to nothing 3–4.5 cm back
                if (soakBand && level > 0)
                {
                    int s0 = bV.Count; float back = Mathf.Clamp(Rm * 0.7f, 0.028f, 0.045f);
                    var wet = level == 1 ? new Color(GorePalette.Thick.r, GorePalette.Thick.g, GorePalette.Thick.b, 0.86f) : new Color(GorePalette.Wet.r * 0.85f, GorePalette.Wet.g * 0.85f, GorePalette.Wet.b * 0.85f, 0.9f);
                    for (int i = 0; i <= L.Count; i++)
                    {
                        var p = L[i % L.Count]; var radial = Vector3.ProjectOnPlane(p - c, n); radial = radial.sqrMagnitude > 1e-8f ? radial.normalized : e1;
                        float rag = 0.65f + 0.35f * Mathf.PerlinNoise(i * 0.37f, 3.1f);
                        var p0 = p + radial * 0.0025f - n * 0.001f; var p1 = p + radial * 0.004f - n * back * rag;
                        bV.Add(parent.InverseTransformPoint(p0)); bV.Add(parent.InverseTransformPoint(p1));
                        var rn = parent.InverseTransformDirection(radial); bN.Add(rn); bN.Add(rn);
                        var mid = new Color(wet.r * 0.8f + GorePalette.DryRim.r * 0.2f, wet.g * 0.8f + GorePalette.DryRim.g * 0.2f, wet.b * 0.8f + GorePalette.DryRim.b * 0.2f, 0f);
                        bC.Add(GorePalette.Check(wet)); bC.Add(GorePalette.Check(mid));
                        bUV.Add(cell); bUV.Add(cell);
                        var x = new Vector4(level == 2 ? 0.75f : 0.5f, 0f, 0.3f, 0f); bX.Add(x); bX.Add(x);
                        var bt = parent.InverseTransformDirection(n); bTg.Add(new Vector4(bt.x, bt.y, bt.z, 1f)); bTg.Add(new Vector4(bt.x, bt.y, bt.z, 1f));
                    }
                    for (int i = 0; i < L.Count; i++)
                    {
                        int a = s0 + i * 2, b = a + 1, c2 = a + 2, d2 = a + 3;
                        var pa = bV[a]; var pb = bV[b]; var pc = bV[c2]; var fn = bN[a];
                        if (Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), fn) >= 0f) { bT.Add(a); bT.Add(b); bT.Add(c2); bT.Add(c2); bT.Add(b); bT.Add(d2); }
                        else { bT.Add(a); bT.Add(c2); bT.Add(b); bT.Add(c2); bT.Add(d2); bT.Add(b); }
                    }
                }
                cSum += c; nSum += n; rMax = Mathf.Max(rMax, R); used++;
            }
            if (used == 0) return null;
            var go = new GameObject(name ?? "GoreCap");
            go.transform.SetParent(parent, false); go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            var mesh = new Mesh { name = (name ?? "GoreCap") + "_mesh" };
            mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, UV); mesh.SetTangents(TG); mesh.SetTriangles(T, 0); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = CapMaterial(skinSrc, level);
            mr.shadowCastingMode = ShadowCastingMode.On; mr.lightProbeUsage = LightProbeUsage.BlendProbes;
            if (bV.Count > 0 && GoreAtlas.Mat != null)
            {
                var band = new GameObject("Soak"); band.transform.SetParent(go.transform, false);
                var bm = new Mesh { name = "GoreCapSoak" };
                bm.SetVertices(bV); bm.SetNormals(bN); bm.SetUVs(0, bUV); bm.SetUVs(2, bX); bm.SetColors(bC); bm.SetTangents(bTg); bm.SetTriangles(bT, 0); bm.RecalculateBounds();
                band.AddComponent<MeshFilter>().sharedMesh = bm;
                var br = band.AddComponent<MeshRenderer>(); br.sharedMaterial = GoreAtlas.Mat; br.shadowCastingMode = ShadowCastingMode.Off;
            }
            info = new CapInfo { Centre = cSum / used, Normal = nSum.normalized, Radius = rMax, Loops = used, Tris = T.Count / 3 };
            return go;
        }

        /// <summary>The same fan, written into a piece's own mesh streams (piece space): the cut end of a severed limb is
        /// closed in the limb's mesh as one more submesh.</summary>
        public static int FanInto(List<Vector3> V, List<Vector3> N, List<Vector4>[] UV, List<Color> C, List<int> T, List<Vector3> loop, Vector3 outward, float inset, out Vector3 centre, out float radius)
        {
            centre = Vector3.zero; radius = 0f;
            if (loop == null || loop.Count < 4) return 0;
            Vector3 c = Vector3.zero; foreach (var p in loop) c += p; c /= loop.Count;
            Vector3 n = Vector3.zero;
            for (int i = 0; i < loop.Count; i++) { var a = loop[i]; var b = loop[(i + 1) % loop.Count]; n.x += (a.y - b.y) * (a.z + b.z); n.y += (a.z - b.z) * (a.x + b.x); n.z += (a.x - b.x) * (a.y + b.y); }
            if (n.sqrMagnitude < 1e-10f) n = outward; n.Normalize(); if (Vector3.Dot(n, outward) < 0f) n = -n;
            var e1 = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var e2 = Vector3.Cross(n, e1);
            float R = 0f, Rm = 0f; foreach (var p in loop) { float d = Vector3.ProjectOnPlane(p - c, n).magnitude; R = Mathf.Max(R, d); Rm += d; }
            Rm /= loop.Count; if (R < 0.004f) return 0;
            int b0 = V.Count; var cc = c - n * inset + n * (0.07f * Rm);
            V.Add(cc); N.Add(n); UV[0].Add(new Vector4(0.5f, 0.5f, 0, 0)); for (int ch = 1; ch < UV.Length; ch++) UV[ch].Add(Vector4.zero); C?.Add(Color.white);
            for (int i = 0; i < loop.Count; i++)
            {
                var p = loop[i] - n * inset; V.Add(p);
                var radial = Vector3.ProjectOnPlane(loop[i] - c, n); N.Add((n + (radial.sqrMagnitude > 1e-8f ? radial.normalized * 0.28f : Vector3.zero)).normalized);
                UV[0].Add(new Vector4(0.5f + 0.5f * Vector3.Dot(loop[i] - c, e1) / R, 0.5f + 0.5f * Vector3.Dot(loop[i] - c, e2) / R, 0, 0));
                for (int ch = 1; ch < UV.Length; ch++) UV[ch].Add(Vector4.zero);
                C?.Add(Color.white);
            }
            for (int i = 0; i < loop.Count; i++)
            {
                int ia = b0 + 1 + i, ib = b0 + 1 + (i + 1) % loop.Count;
                if (Vector3.Dot(Vector3.Cross(V[ia] - cc, V[ib] - cc), n) >= 0f) { T.Add(b0); T.Add(ia); T.Add(ib); } else { T.Add(b0); T.Add(ib); T.Add(ia); }
            }
            centre = c; radius = R;
            return loop.Count;
        }

        static float Perimeter(List<Vector3> L) { float s = 0f; if (L == null) return 0f; for (int i = 0; i < L.Count; i++) s += Vector3.Distance(L[i], L[(i + 1) % L.Count]); return s; }

        // ================================================================== the disc (unreadable meshes, fallback limbs)
        /// <summary>Build a disc cap under 'parent' at a world centre facing worldNormal (away from the body it belongs to).</summary>
        public static GameObject Build(Transform parent, Vector3 worldCentre, Vector3 worldNormal, float radius, int level, Material skinSrc, bool skirt, string name = "GoreStump")
        {
            radius = Mathf.Clamp(radius, 0.02f, 0.12f);
            if (worldNormal.sqrMagnitude < 1e-6f) worldNormal = Vector3.up;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldCentre, Quaternion.LookRotation(worldNormal.normalized, Mathf.Abs(worldNormal.normalized.y) > 0.95f ? Vector3.forward : Vector3.up));
            // parent scale must not squash the cap: counter any lossy scale
            var ls = parent != null ? parent.lossyScale : Vector3.one;
            float sx = ls.x != 0 ? 1f / Mathf.Abs(ls.x) : 1f, sy = ls.y != 0 ? 1f / Mathf.Abs(ls.y) : 1f, sz = ls.z != 0 ? 1f / Mathf.Abs(ls.z) : 1f;
            go.transform.localScale = new Vector3(radius * sx, radius * sy, radius * sz);
            go.AddComponent<MeshFilter>().sharedMesh = Disc();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = CapMaterial(skinSrc, Mathf.Clamp(level, 0, 2));
            mr.shadowCastingMode = ShadowCastingMode.On; mr.lightProbeUsage = LightProbeUsage.BlendProbes;
            if (skirt && level > 0 && GoreAtlas.Mat != null)
            {
                var sk = new GameObject("Sleeve"); sk.transform.SetParent(go.transform, false);
                sk.transform.localScale = new Vector3(1.05f, 1.05f, Mathf.Clamp(0.04f / radius, 0.3f, 1.4f));
                sk.AddComponent<MeshFilter>().sharedMesh = Skirt(level);
                var smr = sk.AddComponent<MeshRenderer>(); smr.sharedMaterial = GoreAtlas.Mat;
                smr.shadowCastingMode = ShadowCastingMode.Off;
            }
            return go;
        }

        // ------------------------------------------------------------------ meshes (unit radius, +Z outward)
        static Mesh Disc()
        {
            if (_disc != null) return _disc;
            const int seg = 28, rings = 4;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>(); var tan = new List<Vector4>();
            v.Add(new Vector3(0, 0, 0.1f)); n.Add(Vector3.forward); uv.Add(new Vector2(0.5f, 0.5f)); tan.Add(new Vector4(1, 0, 0, 1));
            for (int r = 1; r <= rings; r++)
            {
                float rr = r / (float)rings, z = 0.1f * (1f - rr * rr);
                for (int s = 0; s < seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2f;
                    // a soft, slightly uneven edge (a cut, not a gear)
                    float jag = r == rings ? 1f + 0.025f * Mathf.Sin(a * 5f + 1.3f) + 0.015f * Mathf.Sin(a * 11f) : 1f;
                    var p = new Vector3(Mathf.Cos(a) * rr * jag, Mathf.Sin(a) * rr * jag, z);
                    v.Add(p);
                    var nn = new Vector3(p.x * 0.3f, p.y * 0.3f, 1f).normalized; n.Add(nn);
                    uv.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(a) * rr, 0.5f + 0.5f * Mathf.Sin(a) * rr));
                    tan.Add(new Vector4(-Mathf.Sin(a), Mathf.Cos(a), 0, 1));
                }
            }
            for (int s = 0; s < seg; s++) { int a = 1 + s, b = 1 + (s + 1) % seg; t.Add(0); t.Add(a); t.Add(b); }
            for (int r = 1; r < rings; r++)
                for (int s = 0; s < seg; s++)
                {
                    int a = 1 + (r - 1) * seg + s, b = 1 + (r - 1) * seg + (s + 1) % seg, c = 1 + r * seg + s, d = 1 + r * seg + (s + 1) % seg;
                    t.Add(a); t.Add(c); t.Add(d); t.Add(a); t.Add(d); t.Add(b);
                }
            _disc = new Mesh { name = "GoreStumpDisc" };
            _disc.SetVertices(v); _disc.SetNormals(n); _disc.SetUVs(0, uv); _disc.SetTangents(tan); _disc.SetTriangles(t, 0);
            _disc.RecalculateBounds();
            return _disc;
        }

        static readonly Mesh[] _skirts = new Mesh[3];
        static Mesh Skirt(int level)
        {
            level = Mathf.Clamp(level, 0, 2);
            if (_skirts[level] != null) return _skirts[level];
            const int seg = 24;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var c = new List<Color>(); var x = new List<Vector4>(); var tan = new List<Vector4>(); var t = new List<int>();
            var cell = GoreAtlas.Cell(GoreAtlas.Pool); var mid = cell.center;
            Color top = level == 1 ? new Color(GorePalette.Thick.r, GorePalette.Thick.g, GorePalette.Thick.b, 0.85f) : new Color(GorePalette.Wet.r * 0.8f, GorePalette.Wet.g * 0.8f, GorePalette.Wet.b * 0.8f, 0.92f);
            for (int ring = 0; ring < 3; ring++)
            {
                float z = -ring / 2f, fade = ring == 0 ? 1f : ring == 1 ? 0.55f : 0f;
                for (int s = 0; s <= seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2f;
                    float zz = ring == 2 ? z * (0.7f + 0.3f * Mathf.Abs(Mathf.Sin(a * 3.7f + 0.4f))) : z;
                    v.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), zz)); n.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0));
                    uv.Add(mid); x.Add(new Vector4(0.7f, 0, 0.2f, 0)); tan.Add(new Vector4(0, 0, 1, 1));
                    var col = top; col.a *= fade; c.Add(GorePalette.Check(col));
                }
            }
            for (int ring = 0; ring < 2; ring++)
                for (int s = 0; s < seg; s++)
                {
                    int a = ring * (seg + 1) + s, b = a + 1, d = a + seg + 1, e = d + 1;
                    t.Add(a); t.Add(d); t.Add(b); t.Add(b); t.Add(d); t.Add(e);
                }
            var m = new Mesh { name = "GoreStumpSleeve" + level };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetUVs(2, x); m.SetColors(c); m.SetTangents(tan); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            _skirts[level] = m;
            return m;
        }

        // ------------------------------------------------------------------ material + texture
        /// <summary>A copy of the victim's toon material wearing the cross-section (outline, face painting, blood spatter,
        /// wound slots and cloth masks off). Without a toon source: a plain lit material (never the mansion's wallpaper
        /// flesh, whose veins and ink read as a mask).</summary>
        public static Material CapMaterial(Material skinSrc, int level)
        {
            if (_mats.TryGetValue((skinSrc, level), out var m) && m != null) return m;
            var tex = CapTex(level);
            if (skinSrc != null && skinSrc.shader != null && skinSrc.HasProperty("_BaseMap") && skinSrc.HasProperty("_OutlineWidth"))
            {
                m = new Material(skinSrc) { name = "GoreStump_" + level };
                m.DisableKeyword("_FACE"); Set(m, "_FaceOn", 0f);
                m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", Vector2.one); m.SetTextureOffset("_BaseMap", Vector2.zero);
                Set(m, "_OutlineWidth", 0f); SetC(m, "_BaseColor", Color.white); Set(m, "_UseVertexColor", 0f); Set(m, "_BloodAmount", 0f); Set(m, "_DecalOn", 0f);
                Set(m, "_PatternType", 0f); Set(m, "_MaskMode", 0f); Set(m, "_Emission", 0f); Set(m, "_RimStrength", 0f); Set(m, "_BreakAmount", 0f); Set(m, "_CavityStrength", 0f);
                Set(m, "_Bruise", 0f); Set(m, "_WoundStrength", 0f); Set(m, "_HairRing", 0f); Set(m, "_Cull", 0f);
                Set(m, "_Wet", level == 0 ? 0f : 0.85f); Set(m, "_GlossStrength", level == 0 ? 0f : 0.55f); Set(m, "_GlossThreshold", 0.955f); SetC(m, "_GlossColor", new Color(0.95f, 0.8f, 0.78f));
                Set(m, "_SkinSSS", 0f); Set(m, "_ShadeSat", 0.8f);
                for (int i = 0; i < 8; i++) if (m.HasProperty("_Wound" + i)) m.SetVector("_Wound" + i, Vector4.zero);
            }
            else
            {
                MansionMats.Init();
                m = MansionMats.NewLit("GoreStumpLit_" + level, null, 1f, level == 0 ? 0.85f : 0.28f, 0f, false, false);
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                m.SetVector("_Tiling", new Vector4(1, 1, 0, 0));
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            }
            _mats[(skinSrc, level)] = m;
            return m;
        }
        static void Set(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }
        static void SetC(Material m, string p, Color v) { if (m.HasProperty(p)) m.SetColor(p, v); }

        /// <summary>128² cross-section (sRGB texture; palette values are linear and converted): dark wet crimson, muscle grain
        /// running round the section, a darker rim where it meets the skin, and at full strength only a small, blood-filmed
        /// hint of bone a little off centre. No painted highlight (the wet shader gives the shine).</summary>
        public static Texture2D CapTex(int level)
        {
            level = Mathf.Clamp(level, 0, 2);
            if (_tex[level] != null) return _tex[level];
            const int N = 128;
            var px = new Color[N * N];
            var rnd = new System.Random(8117 + level);
            float[] fib = new float[64]; for (int i = 0; i < fib.Length; i++) fib[i] = (float)rnd.NextDouble();
            var linen = new Color(0.2f, 0.17f, 0.15f);
            var deep = new Color(0.16f, 0.008f, 0.014f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f; float r = Mathf.Sqrt(dx * dx + dy * dy); float a = Mathf.Atan2(dy, dx);
                    float n1 = Mathf.PerlinNoise(x * 0.09f + level * 13f, y * 0.09f), n2 = Mathf.PerlinNoise(x * 0.33f + 7f, y * 0.33f + 3f);
                    Color c;
                    if (level == 0)
                    {
                        // wrapped: coarse weave, a dark stain seeping through the middle
                        float weave = 0.85f + 0.15f * Mathf.Sin(x * 1.6f) * Mathf.Sin(y * 1.6f);
                        c = linen * weave * (0.85f + 0.3f * n2);
                        float stain = Mathf.Clamp01(1f - r * 1.3f + (n1 - 0.5f) * 0.6f);
                        c = Color.Lerp(c, new Color(0.09f, 0.03f, 0.02f), stain * 0.85f);
                    }
                    else
                    {
                        // muscle: grain in bundles around the section, dark and wet
                        int fi = Mathf.Abs(Mathf.FloorToInt((a / (Mathf.PI * 2f) + 0.5f) * 19f + r * 3f)) % fib.Length;
                        float fas = 0.78f + 0.2f * fib[fi] + 0.1f * (n2 - 0.5f);
                        c = Color.Lerp(deep, GorePalette.Muscle * 0.82f, fas * (level == 1 ? 0.55f : 0.85f));
                        // thin fascia seams between the bundles
                        float seam = Mathf.Abs(Mathf.Sin(a * 9.5f + n1 * 4f)); if (seam < 0.05f && r > 0.25f && r < 0.85f) c *= 0.72f;
                        // the edge, where it meets the skin, darker and clotting
                        c = Color.Lerp(c, GorePalette.Thick * 0.8f, Mathf.SmoothStep(0.7f, 1f, r) * 0.75f);
                        // a hint of bone (full strength only): small, ivory under a film of blood, a little off centre
                        if (level == 2)
                        {
                            float bx = dx + 0.12f, by = dy - 0.06f; float br = Mathf.Sqrt(bx * bx * 1.1f + by * by);
                            float ring = Mathf.Clamp01(1f - Mathf.Abs(br - 0.17f) / 0.045f) * (0.45f + 0.35f * n2);
                            c = Color.Lerp(c, GorePalette.Bone * 0.62f, ring * 0.55f);
                            if (br < 0.13f) c = Color.Lerp(c, new Color(0.3f, 0.02f, 0.03f), 0.7f);   // marrow
                        }
                        // a film of blood pooling in the lower half
                        float film = Mathf.Clamp01(0.35f + (-dy) * 0.45f + (n1 - 0.5f) * 0.5f);
                        c = Color.Lerp(c, GorePalette.Thick, film * 0.35f);
                    }
                    c.a = 1f;
                    px[y * N + x] = c.gamma;
                }
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { name = "GoreStumpCap" + level, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            t.SetPixels(px); t.Apply(true, true);
            _tex[level] = t;
            return t;
        }
    }
}
