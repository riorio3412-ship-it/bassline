using System.Collections.Generic;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Skinned-mesh surgery shared by <see cref="GoreTorso"/> and <see cref="GorePieces"/>. Every vertex is classified once
    /// per mesh by its dominant bone (mapped onto the common HBone skeleton; unmapped helper bones inherit the nearest rig
    /// ancestor) into a per-vertex part mask. Part groups: Head = Head (+ any vertex with head weight ≥ 0.5); ArmX = UpperArmX,
    /// LowerArmX, HandX and the X fingers; HandX = HandX and the X fingers; LegX = UpperLegX, LowerLegX, FootX, ToeX — but only
    /// within 0.1 m (× height/1.75) of the leg's bones in the bind pose: a coat skirt or a dress panel that rides on the thigh
    /// stays with the body instead of leaving with the leg. A triangle belongs to a part when at least two of its vertices do.
    /// The torso copy also hands the severed bones' weights to the bone each part hung from, so nothing left on the body is
    /// pulled by a limb that is no longer there (and the severed head bone can be collapsed safely). The open edges a cut
    /// leaves are found as welded boundary loops (<see cref="Loops"/>) and closed with caps (<see cref="GoreStump.Caps"/>).
    /// </summary>
    internal static class GoreMeshCut
    {
        public const int PartCount = 7;
        public static int Bit(SeverPart p) => 1 << (int)p;

        public sealed class Classified
        {
            public Mesh Src; public int[] Dom; public float[] HeadW; public byte[] Parts; public int VertexCount;
            public Transform[] Bones; public Matrix4x4[] BindPoses;
            public int[] Map;                                        // smr bone index → HBone (−1 unmapped)
            public readonly int[] ParentIndex = new int[PartCount];  // smr bone index of each part's parent bone (−1 unknown)
            public int SkirtKept;                                    // leg-group vertices kept on the body by the distance rule
        }

        /// <summary>What one torso cut removed (for the logs and the probe).</summary>
        public struct CutStats { public int Kept, Removed; public int[] PerPart; public float Ms; }

        static readonly Dictionary<SkinnedMeshRenderer, Classified> _cls = new Dictionary<SkinnedMeshRenderer, Classified>();
        static readonly Dictionary<(Mesh, int), Mesh> _torso = new Dictionary<(Mesh, int), Mesh>();
        static readonly Dictionary<(Mesh, int), CutStats> _stats = new Dictionary<(Mesh, int), CutStats>();
        static readonly Dictionary<Mesh, int[]> _weld = new Dictionary<Mesh, int[]>();
        static readonly Dictionary<Mesh, int[]> _tris = new Dictionary<Mesh, int[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _cls.Clear(); _torso.Clear(); _stats.Clear(); _weld.Clear(); _tris.Clear(); _lower.Clear(); }

        // ------------------------------------------------------------------ classification
        /// <summary>Per-vertex dominant HBone and part mask for one renderer's (original, readable) mesh.</summary>
        public static Classified Classify(SkinnedMeshRenderer smr, ActorRig rig, Mesh mesh)
        {
            if (smr == null || rig == null || mesh == null || !mesh.isReadable) return null;
            if (_cls.TryGetValue(smr, out var c) && c.Src == mesh) return c;
            var bones = smr.bones;
            var map = new int[bones.Length];
            var direct = new int[ActorSkeleton.Count]; for (int k = 0; k < direct.Length; k++) direct[k] = -1;
            var index = new Dictionary<Transform, int>();
            for (int k = 0; k < rig.Bones.Length; k++) if (rig.Bones[k] != null && !index.ContainsKey(rig.Bones[k])) index[rig.Bones[k]] = k;
            for (int i = 0; i < bones.Length; i++)
            {
                map[i] = -1;
                for (var t = bones[i]; t != null && t != rig.transform; t = t.parent)
                    if (index.TryGetValue(t, out int hb)) { map[i] = hb; if (t == bones[i] && hb < direct.Length && direct[hb] < 0) direct[hb] = i; break; }
            }
            var bw = mesh.boneWeights; int n = mesh.vertexCount;
            var bp = mesh.bindposes;
            c = new Classified { Src = mesh, Dom = new int[n], HeadW = new float[n], Parts = new byte[n], VertexCount = n, Bones = bones, BindPoses = bp, Map = map };
            for (int p = 0; p < PartCount; p++) c.ParentIndex[p] = ParentIndexOf(map, direct, ParentBone((SeverPart)p));
            int head = (int)HBone.Head;
            for (int v = 0; v < n; v++)
            {
                if (bw == null || v >= bw.Length) { c.Dom[v] = -1; continue; }
                var w = bw[v];
                int bi = w.boneIndex0; float bmax = w.weight0;
                if (w.weight1 > bmax) { bmax = w.weight1; bi = w.boneIndex1; }
                if (w.weight2 > bmax) { bmax = w.weight2; bi = w.boneIndex2; }
                if (w.weight3 > bmax) { bmax = w.weight3; bi = w.boneIndex3; }
                c.Dom[v] = Map(map, bi);
                float hw = 0f;
                if (Map(map, w.boneIndex0) == head) hw += w.weight0;
                if (Map(map, w.boneIndex1) == head) hw += w.weight1;
                if (Map(map, w.boneIndex2) == head) hw += w.weight2;
                if (Map(map, w.boneIndex3) == head) hw += w.weight3;
                c.HeadW[v] = hw;
                byte m = 0;
                for (int p = 0; p < PartCount; p++) if (InPart((SeverPart)p, c.Dom[v], hw)) m |= (byte)(1 << p);
                c.Parts[v] = m;
            }
            LegDistanceRule(c, mesh, rig, direct);
            _cls[smr] = c;
            return c;
        }
        static int Map(int[] map, int i) => i >= 0 && i < map.Length ? map[i] : -1;

        static int ParentIndexOf(int[] map, int[] direct, HBone want)
        {
            // the bone itself, else its nearest ancestor that the mesh is skinned to
            for (int hb = (int)want, guard = 0; hb >= 0 && guard < 8; hb = ActorSkeleton.Parent[hb], guard++)
                if (hb < direct.Length && direct[hb] >= 0) return direct[hb];
            for (int i = 0; i < map.Length; i++) if (map[i] == (int)HBone.Hips) return i;
            return -1;
        }

        /// <summary>A leg takes only what lies within 0.1 m (scaled) of its bones in the bind pose: the thigh, knee, shin, foot
        /// and shoe; the skirt panels of a long coat or a dress that ride on the thigh stay with the body.</summary>
        static void LegDistanceRule(Classified c, Mesh mesh, ActorRig rig, int[] direct)
        {
            var bp = c.BindPoses; if (bp == null || bp.Length == 0) return;
            float S = rig.Height > 0.5f ? rig.Height / 1.75f : 1f, lim = 0.1f * S, lim2 = lim * lim;
            Vector3? Joint(HBone b)
            {
                int i = (int)b < direct.Length ? direct[(int)b] : -1;
                if (i < 0 || i >= bp.Length) return null;
                return bp[i].inverse.MultiplyPoint3x4(Vector3.zero);
            }
            var V = mesh.vertices;
            for (int side = 0; side < 2; side++)
            {
                bool L = side == 0; int bit = Bit(L ? SeverPart.LegL : SeverPart.LegR);
                var hip = Joint(L ? HBone.UpperLegL : HBone.UpperLegR); var knee = Joint(L ? HBone.LowerLegL : HBone.LowerLegR);
                var ankle = Joint(L ? HBone.FootL : HBone.FootR); var toe = Joint(L ? HBone.ToeL : HBone.ToeR);
                if (hip == null || knee == null || ankle == null) continue;
                Vector3 h = hip.Value, k = knee.Value, a = ankle.Value, t = toe ?? (a + new Vector3(0, -0.04f, 0.12f) * S);
                for (int v = 0; v < c.VertexCount && v < V.Length; v++)
                {
                    if ((c.Parts[v] & bit) == 0) continue;
                    var p = V[v];
                    float d = Mathf.Min(SegD2(p, h, k), Mathf.Min(SegD2(p, k, a), SegD2(p, a, t)));
                    if (d > lim2) { c.Parts[v] = (byte)(c.Parts[v] & ~bit); c.SkirtKept++; }
                }
            }
        }
        static float SegD2(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a; float l2 = ab.sqrMagnitude; float t = l2 > 1e-10f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0f;
            return (p - (a + ab * t)).sqrMagnitude;
        }

        public static bool InPart(SeverPart p, int hb, float headW)
        {
            if (p == SeverPart.Head) return hb == (int)HBone.Head || headW >= 0.5f;
            if (hb < 0) return false;
            var b = (HBone)hb;
            switch (p)
            {
                case SeverPart.ArmL: return b == HBone.UpperArmL || b == HBone.LowerArmL || b == HBone.HandL || ActorSkeleton.IsFingerL(b);
                case SeverPart.ArmR: return b == HBone.UpperArmR || b == HBone.LowerArmR || b == HBone.HandR || (ActorSkeleton.IsFinger(b) && !ActorSkeleton.IsFingerL(b));
                case SeverPart.HandL: return b == HBone.HandL || ActorSkeleton.IsFingerL(b);
                case SeverPart.HandR: return b == HBone.HandR || (ActorSkeleton.IsFinger(b) && !ActorSkeleton.IsFingerL(b));
                case SeverPart.LegL: return b == HBone.UpperLegL || b == HBone.LowerLegL || b == HBone.FootL || b == HBone.ToeL;
                case SeverPart.LegR: return b == HBone.UpperLegR || b == HBone.LowerLegR || b == HBone.FootR || b == HBone.ToeR;
            }
            return false;
        }

        public static bool InMask(Classified c, int v, int mask) => mask != 0 && v >= 0 && v < c.Parts.Length && (c.Parts[v] & mask) != 0;

        /// <summary>Triangles of the mesh, all submeshes concatenated (cached per mesh).</summary>
        public static int[] Triangles(Mesh m)
        {
            if (m == null) return null;
            if (_tris.TryGetValue(m, out var t) && t != null) return t;
            t = m.triangles; _tris[m] = t;
            if (_tris.Count > 48) { _tris.Clear(); _tris[m] = t; }
            return t;
        }

        // ------------------------------------------------------------------ the torso copy
        /// <summary>A copy of the mesh (bind poses and blend shapes kept) without the severed parts' triangles; the weights
        /// of the severed bones handed to the bone each part hung from. Cached per (mesh, mask).</summary>
        public static Mesh Torso(SkinnedMeshRenderer smr, ActorRig rig, Mesh src, int mask)
        {
            if (src == null || !src.isReadable) return null;
            if (_torso.TryGetValue((src, mask), out var m) && m != null) return m;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var c = Classify(smr, rig, src); if (c == null) return null;
            m = Object.Instantiate(src); m.name = src.name + "_gore" + mask;
            var keep = new List<int>(1024);
            var st = new CutStats { PerPart = new int[PartCount] };
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tris = src.GetTriangles(s); keep.Clear();
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    byte a = c.Parts[tris[i]], b = c.Parts[tris[i + 1]], d = c.Parts[tris[i + 2]];
                    int cut = ((a & mask) != 0 ? 1 : 0) + ((b & mask) != 0 ? 1 : 0) + ((d & mask) != 0 ? 1 : 0);
                    if (cut >= 2)
                    {
                        st.Removed++;
                        for (int p = 0; p < PartCount; p++) { int bit = 1 << p; if ((mask & bit) != 0 && ((a & bit) != 0 ? 1 : 0) + ((b & bit) != 0 ? 1 : 0) + ((d & bit) != 0 ? 1 : 0) >= 2) st.PerPart[p]++; }
                        continue;
                    }
                    st.Kept++;
                    keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
                }
                m.SetTriangles(keep, s, false);
            }
            Reweigh(c, src, m, mask);
            st.Ms = (float)sw.Elapsed.TotalMilliseconds;
            _torso[(src, mask)] = m; _stats[(src, mask)] = st;
            return m;
        }

        public static CutStats StatsOf(Mesh src, int mask) => src != null && _stats.TryGetValue((src, mask), out var s) ? s : default;

        /// <summary>What stays on the body stops following the bones of the parts that are gone: their weights go to the bone
        /// each part hung from (head → neck, arm → shoulder, hand → forearm, leg → hips).</summary>
        static void Reweigh(Classified c, Mesh src, Mesh dst, int mask)
        {
            var map = c.Map; if (map == null || map.Length == 0) return;
            var remap = new int[map.Length]; bool any = false;
            for (int i = 0; i < remap.Length; i++)
            {
                remap[i] = i;
                int hb = map[i]; if (hb < 0) continue;
                for (int p = 0; p < PartCount; p++)
                {
                    if ((mask & (1 << p)) == 0) continue;
                    if (!InPart((SeverPart)p, hb, hb == (int)HBone.Head ? 1f : 0f)) continue;
                    int to = c.ParentIndex[p]; if (to >= 0 && to != i) { remap[i] = to; any = true; }
                    break;   // parts in enum order: an arm that is gone claims its hand's bones before the hand does
                }
            }
            if (!any) return;
            var bw = src.boneWeights; if (bw == null || bw.Length != dst.vertexCount) return;
            for (int v = 0; v < bw.Length; v++)
            {
                var w = bw[v];
                w.boneIndex0 = R(remap, w.boneIndex0); w.boneIndex1 = R(remap, w.boneIndex1); w.boneIndex2 = R(remap, w.boneIndex2); w.boneIndex3 = R(remap, w.boneIndex3);
                bw[v] = w;
            }
            dst.boneWeights = bw;
        }
        static int R(int[] remap, int i) => i >= 0 && i < remap.Length ? remap[i] : i;

        // ------------------------------------------------------------------ open edges of a cut
        /// <summary>
        /// Boundary loops between two sets of triangles: the edges of the "A" triangles (side 1) that are shared, by welded
        /// position, with a "B" triangle (side 2) and with no other A triangle. Each loop is the ordered list of the A side's
        /// vertex indices (one per welded corner), following the A triangles' winding. Only the band of triangles that touch
        /// both sides is looked at. Loops shorter than 4 corners are dropped.
        /// </summary>
        public static List<int[]> Loops(Mesh src, sbyte[] side)
        {
            var loops = new List<int[]>();
            var tris = Triangles(src); var weld = Weld(src); if (tris == null || weld == null || side == null) return loops;
            int nw = 0; for (int i = 0; i < weld.Length; i++) if (weld[i] >= nw) nw = weld[i] + 1;
            var usedA = new bool[nw]; var usedB = new bool[nw];
            int nt = tris.Length / 3;
            for (int t = 0; t < nt && t < side.Length; t++)
            {
                if (side[t] == 0) continue;
                var arr = side[t] == 1 ? usedA : usedB;
                arr[weld[tris[t * 3]]] = true; arr[weld[tris[t * 3 + 1]]] = true; arr[weld[tris[t * 3 + 2]]] = true;
            }
            var bEdges = new HashSet<long>(); var aCount = new Dictionary<long, int>();
            for (int t = 0; t < nt && t < side.Length; t++)
            {
                int s = side[t]; if (s == 0) continue;
                int w0 = weld[tris[t * 3]], w1 = weld[tris[t * 3 + 1]], w2 = weld[tris[t * 3 + 2]];
                var other = s == 1 ? usedB : usedA;
                if ((other[w0] ? 1 : 0) + (other[w1] ? 1 : 0) + (other[w2] ? 1 : 0) < 2) continue;
                for (int e = 0; e < 3; e++)
                {
                    int a = e == 0 ? w0 : e == 1 ? w1 : w2, b = e == 0 ? w1 : e == 1 ? w2 : w0;
                    if (a == b || !other[a] || !other[b]) continue;
                    long key = Key(a, b);
                    if (s == 2) bEdges.Add(key);
                    else { aCount.TryGetValue(key, out int k); aCount[key] = k + 1; }
                }
            }
            var ew = new List<(int wa, int wb, int va)>();
            for (int t = 0; t < nt && t < side.Length; t++)
            {
                if (side[t] != 1) continue;
                int i0 = tris[t * 3], i1 = tris[t * 3 + 1], i2 = tris[t * 3 + 2];
                int w0 = weld[i0], w1 = weld[i1], w2 = weld[i2];
                if ((usedB[w0] ? 1 : 0) + (usedB[w1] ? 1 : 0) + (usedB[w2] ? 1 : 0) < 2) continue;
                for (int e = 0; e < 3; e++)
                {
                    int va = e == 0 ? i0 : e == 1 ? i1 : i2;
                    int a = e == 0 ? w0 : e == 1 ? w1 : w2, b = e == 0 ? w1 : e == 1 ? w2 : w0;
                    if (a == b) continue;
                    long key = Key(a, b);
                    if (!bEdges.Contains(key) || !aCount.TryGetValue(key, out int k) || k != 1) continue;
                    ew.Add((a, b, va));
                }
            }
            if (ew.Count < 3) return loops;
            var outOf = new Dictionary<int, List<int>>();
            for (int i = 0; i < ew.Count; i++) { if (!outOf.TryGetValue(ew[i].wa, out var l)) { l = new List<int>(2); outOf[ew[i].wa] = l; } l.Add(i); }
            var used = new bool[ew.Count]; var cur = new List<int>(64);
            for (int i = 0; i < ew.Count; i++)
            {
                if (used[i]) continue;
                cur.Clear(); used[i] = true; cur.Add(ew[i].va);
                int start = ew[i].wa, at = ew[i].wb; bool closed = false;
                for (int guard = 0; guard < ew.Count; guard++)
                {
                    if (at == start) { closed = true; break; }
                    int next = -1;
                    if (outOf.TryGetValue(at, out var cand)) foreach (int e in cand) if (!used[e]) { next = e; break; }
                    if (next < 0) break;
                    used[next] = true; cur.Add(ew[next].va); at = ew[next].wb;
                }
                if (closed && cur.Count >= 4) loops.Add(cur.ToArray());
            }
            return loops;
        }
        static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        /// <summary>Weld ids by bind-pose position (0.5 mm): UV seams and split normals do not break a loop.</summary>
        public static int[] Weld(Mesh m)
        {
            if (m == null) return null;
            if (_weld.TryGetValue(m, out var w) && w != null) return w;
            var V = m.vertices; w = new int[V.Length];
            var map = new Dictionary<(int, int, int), int>(V.Length);
            int count = 0;
            for (int i = 0; i < V.Length; i++)
            {
                var k = (Mathf.RoundToInt(V[i].x * 2000f), Mathf.RoundToInt(V[i].y * 2000f), Mathf.RoundToInt(V[i].z * 2000f));
                if (!map.TryGetValue(k, out int id)) { id = count++; map[k] = id; }
                w[i] = id;
            }
            if (_weld.Count > 24) _weld.Clear();
            _weld[m] = w;
            return w;
        }

        /// <summary>Side of every triangle for the loops of one part. Torso side: A = kept on the body, B = in the part.
        /// Piece side (pieceMask): A = in the piece, B = everything else.</summary>
        public static sbyte[] Sides(Classified c, Mesh src, int partBit, int severedMask, bool pieceSide, int pieceExclude = 0)
        {
            var tris = Triangles(src); if (tris == null || c == null) return null;
            int nt = tris.Length / 3; var side = new sbyte[nt];
            var P = c.Parts;
            for (int t = 0; t < nt; t++)
            {
                byte a = P[tris[t * 3]], b = P[tris[t * 3 + 1]], d = P[tris[t * 3 + 2]];
                bool inPart = ((a & partBit) != 0 ? 1 : 0) + ((b & partBit) != 0 ? 1 : 0) + ((d & partBit) != 0 ? 1 : 0) >= 2;
                if (pieceSide)
                {
                    bool excl = pieceExclude != 0 && ((a & pieceExclude) != 0 ? 1 : 0) + ((b & pieceExclude) != 0 ? 1 : 0) + ((d & pieceExclude) != 0 ? 1 : 0) >= 2;
                    side[t] = (sbyte)(inPart && !excl ? 1 : 2);
                }
                else
                {
                    if (inPart) { side[t] = 2; continue; }
                    bool gone = ((a & severedMask) != 0 ? 1 : 0) + ((b & severedMask) != 0 ? 1 : 0) + ((d & severedMask) != 0 ? 1 : 0) >= 2;
                    side[t] = (sbyte)(gone ? 0 : 1);
                }
            }
            return side;
        }

        // ------------------------------------------------------------------ bones
        /// <summary>The bone a part hangs from (where its stump cap is parented) and the part's root bone.</summary>
        public static HBone ParentBone(SeverPart p)
        {
            switch (p)
            {
                case SeverPart.Head: return HBone.Neck;
                case SeverPart.ArmL: return HBone.ShoulderL;
                case SeverPart.ArmR: return HBone.ShoulderR;
                case SeverPart.HandL: return HBone.LowerArmL;
                case SeverPart.HandR: return HBone.LowerArmR;
                case SeverPart.LegL: case SeverPart.LegR: return HBone.Hips;
            }
            return HBone.Chest;
        }
        public static HBone RootBone(SeverPart p)
        {
            switch (p)
            {
                case SeverPart.Head: return HBone.Head;
                case SeverPart.ArmL: return HBone.UpperArmL;
                case SeverPart.ArmR: return HBone.UpperArmR;
                case SeverPart.HandL: return HBone.HandL;
                case SeverPart.HandR: return HBone.HandR;
                case SeverPart.LegL: return HBone.UpperLegL;
                case SeverPart.LegR: return HBone.UpperLegR;
            }
            return HBone.Chest;
        }
        /// <summary>The bone past the cut that shows which way the part ran (for the cap's outward side).</summary>
        public static HBone ChildBone(SeverPart p)
        {
            switch (p)
            {
                case SeverPart.Head: return HBone.Head;
                case SeverPart.ArmL: return HBone.LowerArmL;
                case SeverPart.ArmR: return HBone.LowerArmR;
                case SeverPart.HandL: return HBone.MiddleL1;
                case SeverPart.HandR: return HBone.MiddleR1;
                case SeverPart.LegL: return HBone.LowerLegL;
                case SeverPart.LegR: return HBone.LowerLegR;
            }
            return HBone.Chest;
        }
        /// <summary>Is a rig bone inside a severed part's subtree (its renderers must stay off).</summary>
        public static bool BoneInPart(SeverPart p, HBone b) => InPart(p, (int)b, b == HBone.Head ? 1f : 0f);

        static readonly Dictionary<ActorRig, HashSet<Renderer>> _lower = new Dictionary<ActorRig, HashSet<Renderer>>();

        /// <summary>False for a skin that is only a lower level of detail (LOD1/LOD2 of a LODGroup): pieces and cut rims are
        /// taken from the top level only (the torso cut still applies to every level).</summary>
        public static bool IsTopLod(ActorRig rig, SkinnedMeshRenderer smr)
        {
            if (rig == null || smr == null) return true;
            if (!_lower.TryGetValue(rig, out var lower))
            {
                lower = new HashSet<Renderer>();
                var lg = rig.GetComponentInChildren<LODGroup>(true);
                if (lg != null)
                {
                    var lods = lg.GetLODs();
                    var top = new HashSet<Renderer>(); if (lods.Length > 0 && lods[0].renderers != null) foreach (var r in lods[0].renderers) if (r != null) top.Add(r);
                    for (int i = 1; i < lods.Length; i++) if (lods[i].renderers != null) foreach (var r in lods[i].renderers) if (r != null && !top.Contains(r)) lower.Add(r);
                }
                if (_lower.Count > 64) _lower.Clear();
                _lower[rig] = lower;
            }
            return !lower.Contains(smr);
        }

        public static bool TryPart(Item it, out SeverPart part)
        {
            part = SeverPart.ArmL;
            if (it == null) return false;
            try { if (Gore.TryPart(it, out part)) return true; } catch { }
            return !string.IsNullOrEmpty(it.Note) && System.Enum.TryParse(it.Note.Trim(), out part) && System.Enum.IsDefined(typeof(SeverPart), part);
        }
    }
}
