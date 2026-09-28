using System;
using System.Collections.Generic;
using BL23.Game.Characters;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using MS = BL23.Game.Mansion.S;

namespace BL23.Game
{
    /// <summary>A severed piece as drawn: which part, its item, the skin root, where it lies, its cut face.</summary>
    public struct GorePiece
    {
        public SeverPart Part; public string ItemId; public Transform T; public Vector3 Center, CapNormal; public float Radius; public int Room; public bool Hidden; public string Surface;
    }

    /// <summary>
    /// Skins the kernel's severed pieces (Items of Type "SeveredPart", Note = part code; murder-sim's interim items work the
    /// same way) and cuts the torso. Every 0.5 s: pieces whose ItemView has no skin are queued; at most one step runs per
    /// frame — first the victim's dead pose is baked once (its own frame), then one piece per frame from that pose
    /// (LastBakeMs = the costlier of the two frames). The limb is the part's triangles of the victim's own skinned meshes in
    /// the dead pose, laid with its long axis on local +z and its flattest side down; every open loop of its cut end(s) is
    /// closed inside the limb's mesh (<see cref="GoreStump.FanInto"/>, one extra submesh), so no piece is a hollow shell. The
    /// skin is parented under ItemView.Visual and the bundle's renderers are switched off; its Rigidbody and ItemTag stay and
    /// its box collider is fitted to the limb, so ItemView still owns physics, carrying, hiding and the write-back of the
    /// resting pose (screen and kernel agree). Linen by Settings.Gore: 2 the sheet opened out under the limb, 1 the sheet
    /// over the cut end (only the hand, foot or hair shows), 0 the original bundle untouched. Surface tags: burnt (charred
    /// bone shards and ash, no flesh), waterlogged (pale, glossy, grey sheet), soil (earth dust), stowed (wrapped).
    /// Unreadable meshes or an unloaded body fall back to a procedural limb.
    /// </summary>
    public sealed class GorePieces : MonoBehaviour
    {
        /// <summary>The costliest single frame spent on the last piece (the pose bake or the piece build), ms.</summary>
        public static float LastBakeMs;
        /// <summary>The last piece's build frame alone and the victim's pose bake alone, ms (probe).</summary>
        public static float LastBuildMs, LastPoseMs;
        /// <summary>Bytes of mesh data made for pieces so far this session (probe).</summary>
        public static long MeshBytes;
        public const int MaxPiecesPerVictim = 6;
        static GorePieces _i;

        sealed class Skin
        {
            public string ItemId, Victim; public SeverPart Part; public GameObject Root, Vis; public int Level; public string SurfKey;
            public Transform Cap; public float Radius = 0.05f; public Renderer Main; public bool Fallback, Failed;
            public BoxCollider Col; public Vector3 ColC0, ColS0; public bool ColSet;
            public readonly List<Mesh> Meshes = new List<Mesh>();
        }
        readonly Dictionary<string, Skin> _skins = new Dictionary<string, Skin>();
        readonly Queue<string> _todo = new Queue<string>();
        readonly HashSet<string> _queued = new HashSet<string>();
        readonly Dictionary<string, float> _deadSince = new Dictionary<string, float>();
        readonly Dictionary<string, int> _masks = new Dictionary<string, int>();
        readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        readonly List<GoreTorso> _torsos = new List<GoreTorso>();
        readonly List<string> _tmp = new List<string>();
        static readonly Dictionary<(Material, int), Material> _pieceMats = new Dictionary<(Material, int), Material>();
        static Material _linen2S;
        GameState _S; Transform _worldRoot; int _level = -1; float _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _i = null; LastBakeMs = LastBuildMs = LastPoseMs = 0; MeshBytes = 0; _pieceMats.Clear(); _linen2S = null; _mdata.Clear(); }

        void Awake() { _i = this; }
        void OnDestroy() { if (_i == this) _i = null; }

        // ------------------------------------------------------------------ API
        public static IReadOnlyList<GorePiece> Of(string victimId)
        {
            var list = new List<GorePiece>();
            if (_i == null || victimId == null) return list;
            var S = GoreWorld.S;
            foreach (var sk in _i._skins.Values)
            {
                if (sk.Victim != victimId || sk.Vis == null) continue;
                var it = S?.I(sk.ItemId); if (it == null) continue;
                var t = sk.Root != null ? sk.Root.transform : sk.Vis.transform;
                Vector3 c = t.position; float r = sk.Radius;
                if (sk.Main != null) c = sk.Main.bounds.center;
                else { var mr = sk.Vis.GetComponentInChildren<Renderer>(); if (mr != null) c = mr.bounds.center; }
                var n = sk.Cap != null ? sk.Cap.forward : -t.forward;
                list.Add(new GorePiece
                {
                    Part = sk.Part, ItemId = sk.ItemId, T = t, Center = c, CapNormal = n, Radius = r, Room = it.Room,
                    Hidden = it.Hidden || it.Holder != null || !sk.Vis.activeInHierarchy, Surface = it.Surface != null ? string.Join(",", it.Surface) : ""
                });
            }
            list.Sort((a, b) => string.CompareOrdinal(a.ItemId, b.ItemId));
            return list;
        }

        // ------------------------------------------------------------------ polling
        void Update()
        {
            if (_todo.Count > 0) { try { BuildNext(); } catch (Exception e) { Debug.LogException(e); } }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            try { Poll(); } catch (Exception e) { Debug.LogException(e); }
        }

        void Poll()
        {
            var S = GoreWorld.S; var W = GoreWorld.W;
            if (S == null || W == null || W.Root == null) { if (_skins.Count > 0) ClearAll(); return; }
            if (S != _S || W.Root != _worldRoot) { ClearAll(); _S = S; _worldRoot = W.Root; }
            int level = Mathf.Clamp(Settings.Gore, 0, 2);
            if (level != _level) { _level = level; foreach (var sk in _skins.Values) DestroySkin(sk, true); _skins.Clear(); }
            _masks.Clear(); _counts.Clear();
            foreach (var it in S.Items.Values)
            {
                if (it.Type != "SeveredPart" || it.Owner == null) continue;
                if (!GoreMeshCut.TryPart(it, out var part)) continue;
                _masks.TryGetValue(it.Owner, out int m); _masks[it.Owner] = m | GoreMeshCut.Bit(part);
                _counts.TryGetValue(it.Owner, out int cnt); if (cnt >= MaxPiecesPerVictim) continue; _counts[it.Owner] = cnt + 1;
                if (!W.Items.TryGetValue(it.Id, out var iv) || iv == null || iv.Visual == null) continue;
                if (_skins.TryGetValue(it.Id, out var sk))
                {
                    if (sk.Vis == iv.Visual && sk.Level == level && sk.SurfKey == SurfKey(it) && (level == 0 || sk.Root != null || sk.Failed)) continue;
                    DestroySkin(sk, true); _skins.Remove(it.Id);
                }
                if (_queued.Add(it.Id)) _todo.Enqueue(it.Id);
            }
            // skins of items that are gone
            _tmp.Clear();
            foreach (var kv in _skins) { var it = S.I(kv.Key); if (it == null || kv.Value.Vis == null || it.Type != "SeveredPart") _tmp.Add(kv.Key); }
            foreach (var id in _tmp) { DestroySkin(_skins[id], true); _skins.Remove(id); }
            // torsos
            foreach (var kv in _masks)
            {
                var view = W.ViewOf(kv.Key); if (view == null || view.Rig == null) continue;
                if (!Settled(view)) continue;
                var torso = view.GetComponent<GoreTorso>();
                if (torso == null) { torso = view.gameObject.AddComponent<GoreTorso>(); _torsos.Add(torso); }
                torso.Set(kv.Value, level);
            }
            for (int i = _torsos.Count - 1; i >= 0; i--)
            {
                var t = _torsos[i]; if (t == null) { _torsos.RemoveAt(i); continue; }
                var v = t.GetComponent<ActorView>();
                if (v == null || !_masks.ContainsKey(v.Id)) { t.Restore(true); Destroy(t); _torsos.RemoveAt(i); }
            }
        }

        bool Settled(ActorView view)
        {
            var S = GoreWorld.S; var a = S?.A(view.Id);
            if (a == null || a.Status != ActorStatus.Dead) { _deadSince.Remove(view.Id); return false; }
            bool dead = view.Rig != null && (view.Rig.Anim == null || view.Rig.Anim.IsDead);
            if (!dead) return false;
            if (!_deadSince.TryGetValue(view.Id, out float t0)) { _deadSince[view.Id] = Time.unscaledTime; return false; }
            return Time.unscaledTime - t0 >= 1.5f;
        }

        static string SurfKey(Item it) => it.Surface == null ? "" : (it.Surface.Contains("burnt") ? "b" : "") + (it.Surface.Contains("waterlogged") ? "w" : "") + (it.Surface.Contains("soil") ? "s" : "") + (it.Surface.Contains("stowed") ? "t" : "");

        void ClearAll()
        {
            foreach (var sk in _skins.Values) DestroySkin(sk, true);
            _skins.Clear(); _todo.Clear(); _queued.Clear(); _deadSince.Clear(); _poses.Clear();
            foreach (var t in _torsos) if (t != null) { t.Restore(true); Destroy(t); }
            _torsos.Clear();
        }

        static void DestroySkin(Skin sk, bool restoreBundle)
        {
            if (sk == null) return;
            if (sk.Root != null) Destroy(sk.Root);
            foreach (var m in sk.Meshes) if (m != null) Destroy(m);
            sk.Meshes.Clear();
            if (sk.ColSet && sk.Col != null) { sk.Col.center = sk.ColC0; sk.Col.size = sk.ColS0; }
            sk.ColSet = false;
            if (restoreBundle && sk.Vis != null) Bundle(sk.Vis, null, true);
        }

        /// <summary>Switch the original bundle's renderers (everything under Visual except the skin).</summary>
        static void Bundle(GameObject vis, GameObject skin, bool on)
        {
            foreach (var r in vis.GetComponentsInChildren<MeshRenderer>(true))
                if (skin == null || !r.transform.IsChildOf(skin.transform)) r.enabled = on;
        }

        // ------------------------------------------------------------------ building
        float _poseMsFor; string _poseVictim;

        void BuildNext()
        {
            var id = _todo.Peek();
            void Drop() { _todo.Dequeue(); _queued.Remove(id); }
            var S = GoreWorld.S; var W = GoreWorld.W; if (S == null || W == null) { Drop(); return; }
            var it = S.I(id); if (it == null || it.Type != "SeveredPart") { Drop(); return; }
            if (!W.Items.TryGetValue(id, out var iv) || iv == null || iv.Visual == null) { Drop(); return; }
            if (!GoreMeshCut.TryPart(it, out var part)) { Drop(); return; }
            var view = W.ViewOf(it.Owner);
            // wait for the body to lie still (the next poll re-queues it)
            if (_level > 0 && view != null && view.Rig != null && !Settled(view)) { Drop(); return; }
            // step 1, a frame of its own: the victim's dead pose, shared by every piece of that body
            if (_level > 0 && view != null && view.Rig != null && NeedsPose(view))
            {
                var sw0 = System.Diagnostics.Stopwatch.StartNew();
                try { CachePose(view); } catch (Exception e) { Debug.LogWarning("[Gore] pose " + view.Id + ": " + e.Message); _poses.Clear(); Drop(); }
                LastPoseMs = (float)sw0.Elapsed.TotalMilliseconds; _poseMsFor = LastPoseMs; _poseVictim = view.Id;
                return;
            }
            Drop();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var sk = new Skin { ItemId = id, Victim = it.Owner, Part = part, Vis = iv.Visual, Level = _level, SurfKey = SurfKey(it) };
            try { Build(sk, it, view); }
            catch (Exception e) { Debug.LogWarning("[Gore] piece " + id + " " + part + ": " + e); DestroySkin(sk, true); sk.Root = null; sk.Failed = true; }
            _skins[id] = sk;
            LastBuildMs = (float)sw.Elapsed.TotalMilliseconds;
            LastBakeMs = Mathf.Max(LastBuildMs, _poseVictim == it.Owner ? _poseMsFor : 0f);
            if (sk.Root != null) Debug.Log($"[Gore] piece {id} {part} of {it.Owner}: {(sk.Fallback ? "procedural" : "baked")} build {LastBuildMs:0.0} ms (pose {(_poseVictim == it.Owner ? _poseMsFor : 0f):0.0} ms), radius {sk.Radius:0.000}");
        }

        void Build(Skin sk, Item it, ActorView view)
        {
            if (sk.Level == 0) { Bundle(sk.Vis, null, true); return; }
            var root = new GameObject("GoreSkin_" + sk.Part);
            root.transform.SetParent(sk.Vis.transform, false);
            root.AddComponent<GorePieceTag>().ItemId = sk.ItemId;
            sk.Root = root;
            var surf = it.Surface ?? new List<string>();
            bool burnt = surf.Contains("burnt"), water = surf.Contains("waterlogged"), soil = surf.Contains("soil"), stowed = surf.Contains("stowed");
            var rig = view != null ? view.Rig : null;
            var skinSrc = SkinSource(rig);
            if (burnt) { Burnt(sk, root.transform, rig); Bundle(sk.Vis, root, false); FitCollider(sk, new Bounds(new Vector3(0, 0.03f, 0), new Vector3(0.14f, 0.06f, 0.4f))); return; }
            int variant = water ? 1 : soil ? 2 : 0;
            Bounds lb; Vector3 cap, capN; float radius;
            if (!Bake(sk, root.transform, rig, view, variant, skinSrc, out lb, out cap, out capN, out radius))
            {
                sk.Fallback = true;
                FallbackLimb(sk, root.transform, rig, skinSrc, variant, out lb, out cap, out capN, out radius);
                var capGo = GoreStump.Build(root.transform, root.transform.TransformPoint(cap), root.transform.TransformDirection(capN), radius, sk.Level, skinSrc, false, "Cap");
                sk.Cap = capGo.transform;
            }
            else
            {
                // the cut face is closed inside the limb's own mesh; this marker only says where it looks
                var mk = new GameObject("Cap").transform; mk.SetParent(root.transform, false);
                mk.localPosition = cap; mk.localRotation = Quaternion.LookRotation(capN.sqrMagnitude > 1e-6f ? capN : Vector3.back, Vector3.up);
                sk.Cap = mk;
            }
            sk.Radius = radius;
            Linen(sk, root.transform, lb, cap, capN, sk.Level == 1 || stowed, water, soil);
            Bundle(sk.Vis, root, false);
            FitCollider(sk, lb);
        }

        /// <summary>The bundle's box collider (on the Visual) takes the limb's size, so what the player sees is what collides
        /// and what a grab finds; the original size comes back if the skin is ever removed.</summary>
        static void FitCollider(Skin sk, Bounds lb)
        {
            if (sk.Vis == null) return;
            var bc = sk.Vis.GetComponent<BoxCollider>(); if (bc == null) return;
            if (!sk.ColSet) { sk.Col = bc; sk.ColC0 = bc.center; sk.ColS0 = bc.size; sk.ColSet = true; }
            var size = new Vector3(Mathf.Clamp(lb.size.x, 0.05f, 0.45f), Mathf.Clamp(lb.size.y, 0.035f, 0.3f), Mathf.Clamp(lb.size.z, 0.08f, 1.15f));
            var c = lb.center; c.y = Mathf.Max(c.y, size.y * 0.5f);
            bc.center = c; bc.size = size;
        }

        static Material SkinSource(ActorRig rig)
        {
            if (rig == null) return null;
            foreach (var smr in rig.Skins)
            {
                if (smr == null || !GoreMeshCut.IsTopLod(rig, smr)) continue;
                var m = GoreStump.SkinSource(smr.sharedMaterials); if (m != null) return m;
            }
            return null;
        }

        /// <summary>A still copy of one of the victim's materials for a piece (wound decals are in the body's space, so they
        /// are cleared; the spatter stays light). variant 1 waterlogged, 2 soil.</summary>
        static Material PieceMat(Material src, int variant)
        {
            if (src == null) return null;
            if (_pieceMats.TryGetValue((src, variant), out var m) && m != null) return m;
            m = new Material(src) { name = src.name + "_piece" + variant };
            for (int i = 0; i < 8; i++) if (m.HasProperty("_Wound" + i)) m.SetVector("_Wound" + i, Vector4.zero);
            if (m.HasProperty("_DecalOn")) m.SetFloat("_DecalOn", 0f);
            if (m.HasProperty("_BloodAmount")) m.SetFloat("_BloodAmount", variant == 1 ? 0.1f : 0.34f);
            if (m.HasProperty("_BreakAmount")) m.SetFloat("_BreakAmount", 0f);
            if (m.HasProperty("_BaseColor"))
            {
                var c = m.GetColor("_BaseColor");
                if (variant == 1) c = new Color(c.r * 0.80f, c.g * 0.84f, c.b * 0.84f, c.a);
                if (variant == 2) c = new Color(c.r * 0.74f, c.g * 0.66f, c.b * 0.56f, c.a);
                m.SetColor("_BaseColor", c);
            }
            if (variant == 1) { if (m.HasProperty("_Wet")) m.SetFloat("_Wet", 1f); if (m.HasProperty("_GlossStrength")) m.SetFloat("_GlossStrength", 0.8f); }
            _pieceMats[(src, variant)] = m;
            return m;
        }

        // ---- the dead pose (baked once per body, its own frame)
        sealed class DeadPose { public Vector3[] V, N; public Vector4[] Tg; public Matrix4x4 Xf; public Vector3 RootPos; public Quaternion RootRot; public float At; public Mesh Src; }
        readonly Dictionary<SkinnedMeshRenderer, DeadPose> _poses = new Dictionary<SkinnedMeshRenderer, DeadPose>();

        bool NeedsPose(ActorView view)
        {
            var rig = view.Rig; var torso = view.GetComponent<GoreTorso>();
            foreach (var smr in rig.Skins)
            {
                if (smr == null || !GoreMeshCut.IsTopLod(rig, smr)) continue;
                var orig = torso != null ? torso.OriginalOf(smr) : smr.sharedMesh; if (orig == null || !orig.isReadable) continue;
                if (!_poses.TryGetValue(smr, out var p) || !Fresh(p, rig, orig)) return true;
            }
            return false;
        }
        static bool Fresh(DeadPose p, ActorRig rig, Mesh orig) => p != null && p.Src == orig && Time.unscaledTime - p.At < 30f && (p.RootPos - rig.transform.position).sqrMagnitude < 1e-6f && Quaternion.Angle(p.RootRot, rig.transform.rotation) < 0.1f;

        void CachePose(ActorView view)
        {
            var rig = view.Rig; var torso = view.GetComponent<GoreTorso>();
            torso?.ReleaseCollapse();   // the original mesh is baked with the head where it was
            if (_poses.Count > 16) _poses.Clear();
            var baked = new Mesh();
            try
            {
                foreach (var smr in rig.Skins)
                {
                    if (smr == null || !GoreMeshCut.IsTopLod(rig, smr)) continue;
                    var orig = torso != null ? torso.OriginalOf(smr) : smr.sharedMesh; if (orig == null || !orig.isReadable) continue;
                    var cur = smr.sharedMesh; smr.sharedMesh = orig; smr.BakeMesh(baked, true); smr.sharedMesh = cur;
                    _poses[smr] = new DeadPose { V = baked.vertices, N = baked.normals, Tg = baked.tangents, Xf = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one), RootPos = rig.transform.position, RootRot = rig.transform.rotation, At = Time.unscaledTime, Src = orig };
                }
            }
            finally { Destroy(baked); }
        }

        DeadPose PoseOf(SkinnedMeshRenderer smr, Mesh orig, ActorView view)
        {
            if (_poses.TryGetValue(smr, out var p) && Fresh(p, view.Rig, orig)) return p;
            CachePose(view);
            return _poses.TryGetValue(smr, out p) ? p : null;
        }

        // ---- per-mesh streams (read once per mesh)
        sealed class MeshData { public readonly List<Vector4>[] UV = new List<Vector4>[4]; public Color[] Col; public int[] SubStart, SubCount; }
        static readonly Dictionary<Mesh, MeshData> _mdata = new Dictionary<Mesh, MeshData>();
        static MeshData Data(Mesh m)
        {
            if (_mdata.TryGetValue(m, out var d) && d != null) return d;
            if (_mdata.Count > 6) _mdata.Clear();
            d = new MeshData();
            for (int ch = 0; ch < 4; ch++) { var l = new List<Vector4>(); m.GetUVs(ch, l); d.UV[ch] = l.Count == m.vertexCount ? l : null; }
            var col = m.colors; d.Col = col != null && col.Length == m.vertexCount ? col : null;
            d.SubStart = new int[m.subMeshCount]; d.SubCount = new int[m.subMeshCount];
            int acc = 0; for (int s = 0; s < m.subMeshCount; s++) { int n = (int)m.GetIndexCount(s); d.SubStart[s] = acc / 3; d.SubCount[s] = n / 3; acc += n; }
            _mdata[m] = d;
            return d;
        }

        // ---- the limb baked from the body
        readonly List<Vector3> _v = new List<Vector3>(4096), _n = new List<Vector3>(4096);
        readonly List<Vector4> _tg = new List<Vector4>(4096);
        readonly List<Vector4>[] _uv = { new List<Vector4>(4096), new List<Vector4>(4096), new List<Vector4>(4096), new List<Vector4>(4096) };
        readonly List<Color> _c = new List<Color>(4096);
        readonly List<List<int>> _subs = new List<List<int>>();
        readonly List<Material> _mats = new List<Material>();

        bool Bake(Skin sk, Transform root, ActorRig rig, ActorView view, int variant, Material skinSrc, out Bounds lb, out Vector3 capLocal, out Vector3 capN, out float radius)
        {
            lb = default; capLocal = default; capN = Vector3.back; radius = 0.05f;
            if (rig == null || rig.Skins == null || view == null) return false;
            var torso = view.GetComponent<GoreTorso>();
            torso?.ReleaseCollapse();
            _v.Clear(); _n.Clear(); _tg.Clear(); _c.Clear(); foreach (var u in _uv) u.Clear(); _subs.Clear(); _mats.Clear();
            bool[] hasUv = new bool[4]; bool hasCol = false, hasTg = false;
            int bit = GoreMeshCut.Bit(sk.Part);
            // an arm whose hand was cut off separately bakes without the hand (the hand is its own piece)
            _masks.TryGetValue(sk.Victim ?? "", out int vmask);
            int excl = sk.Part == SeverPart.ArmL && (vmask & GoreMeshCut.Bit(SeverPart.HandL)) != 0 ? GoreMeshCut.Bit(SeverPart.HandL) : sk.Part == SeverPart.ArmR && (vmask & GoreMeshCut.Bit(SeverPart.HandR)) != 0 ? GoreMeshCut.Bit(SeverPart.HandR) : 0;
            var loopsW = new List<List<Vector3>>();
            foreach (var smr in rig.Skins)
            {
                if (smr == null || !GoreMeshCut.IsTopLod(rig, smr)) continue;   // LOD1/2 would stack copies
                var orig = torso != null ? torso.OriginalOf(smr) : smr.sharedMesh;
                if (orig == null || !orig.isReadable) continue;
                var cls = GoreMeshCut.Classify(smr, rig, orig); if (cls == null) continue;
                var side = GoreMeshCut.Sides(cls, orig, bit, 0, true, excl); if (side == null) continue;
                int ptris = 0; for (int t = 0; t < side.Length; t++) if (side[t] == 1) ptris++;
                if (ptris == 0) continue;
                var pose = PoseOf(smr, orig, view); if (pose == null || pose.V == null) continue;
                var md = Data(orig); var tris = GoreMeshCut.Triangles(orig);
                var bv = pose.V; var bn = pose.N; var bt = pose.Tg; var xf = pose.Xf;
                for (int ch = 0; ch < 4; ch++) if (md.UV[ch] != null) hasUv[ch] = true;
                if (md.Col != null) hasCol = true;
                if (bt != null && bt.Length == bv.Length) hasTg = true;
                var remap = new int[orig.vertexCount]; for (int i = 0; i < remap.Length; i++) remap[i] = -1;
                var mats = smr.sharedMaterials;
                for (int s = 0; s < md.SubStart.Length; s++)
                {
                    List<int> list = null;
                    for (int t = md.SubStart[s], end = md.SubStart[s] + md.SubCount[s]; t < end && t < side.Length; t++)
                    {
                        if (side[t] != 1) continue;
                        if (list == null) list = new List<int>(md.SubCount[s] / 6 + 16);
                        list.Add(Vert(tris[t * 3])); list.Add(Vert(tris[t * 3 + 1])); list.Add(Vert(tris[t * 3 + 2]));
                    }
                    if (list != null && list.Count > 0) { _subs.Add(list); _mats.Add(PieceMat(s < mats.Length ? mats[s] : null, variant) ?? MansionMats.Get(MS.Ceramic)); }
                }
                foreach (var L in GoreMeshCut.Loops(orig, side))
                {
                    var w = new List<Vector3>(L.Length);
                    foreach (int v in L) if (v < bv.Length) w.Add(xf.MultiplyPoint3x4(bv[v]));
                    if (w.Count >= 4) loopsW.Add(w);
                }

                int Vert(int v)
                {
                    if (remap[v] >= 0) return remap[v];
                    remap[v] = _v.Count;
                    _v.Add(xf.MultiplyPoint3x4(v < bv.Length ? bv[v] : Vector3.zero));
                    _n.Add(xf.MultiplyVector(bn != null && v < bn.Length ? bn[v] : Vector3.up));
                    if (bt != null && v < bt.Length) { var tv = xf.MultiplyVector(new Vector3(bt[v].x, bt[v].y, bt[v].z)); _tg.Add(new Vector4(tv.x, tv.y, tv.z, bt[v].w)); } else _tg.Add(new Vector4(1, 0, 0, 1));
                    for (int ch = 0; ch < 4; ch++) _uv[ch].Add(md.UV[ch] != null ? md.UV[ch][v] : Vector4.zero);
                    _c.Add(md.Col != null ? md.Col[v] : Color.white);
                    return remap[v];
                }
            }
            if (_v.Count < 12 || _subs.Count == 0) return false;

            // frame: J the cut (the loops by the part's root joint), long axis J → centroid, flattest side down
            Vector3 C = Vector3.zero; foreach (var p in _v) C += p; C /= _v.Count;
            var jb = rig.Bone(GoreMeshCut.RootBone(sk.Part)); var jpos = jb != null ? jb.position : C;
            Vector3 J; float bestD = float.MaxValue; List<Vector3> prox = null;
            foreach (var L in loopsW) { var lc = Centre(L); float d = (lc - jpos).sqrMagnitude; if (d < bestD) { bestD = d; prox = L; } }
            if (prox != null && bestD < 0.25f * 0.25f)
            {
                J = Vector3.zero; int k = 0;
                foreach (var L in loopsW) { var lc = Centre(L); if ((lc - Centre(prox)).sqrMagnitude < 0.08f * 0.08f) { J += lc * L.Count; k += L.Count; } }
                J = k > 0 ? J / k : Centre(prox);
                float rs = 0; foreach (var p in prox) rs += (p - Centre(prox)).magnitude; radius = Mathf.Clamp(rs / prox.Count, 0.02f, 0.14f);
            }
            else { J = jpos; radius = sk.Part == SeverPart.Head ? 0.06f : 0.05f; }
            var axis = C - J; if (axis.sqrMagnitude < 1e-6f) axis = Vector3.forward; axis.Normalize();
            Vector3 up;
            if (sk.Part == SeverPart.Head)
            {
                // the face turned down to the floor, the back of the head up
                var face = rig.EyeWorld() - rig.HeadCenterWorld();
                up = -Vector3.ProjectOnPlane(face, axis);
                if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.up, axis);
            }
            else up = MinorAxis(axis, C);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.up, axis);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.right, axis);
            up.Normalize();
            var inv = Quaternion.Inverse(Quaternion.LookRotation(axis, up));
            var mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue); var mx = -mn;
            for (int i = 0; i < _v.Count; i++)
            {
                var l = inv * (_v[i] - J); _v[i] = l; mn = Vector3.Min(mn, l); mx = Vector3.Max(mx, l);
                _n[i] = inv * _n[i]; var tv = inv * new Vector3(_tg[i].x, _tg[i].y, _tg[i].z); _tg[i] = new Vector4(tv.x, tv.y, tv.z, _tg[i].w);
            }
            var shift = new Vector3(-(mn.x + mx.x) * 0.5f, 0.004f - mn.y, -(mn.z + mx.z) * 0.5f);
            for (int i = 0; i < _v.Count; i++) _v[i] += shift;
            lb = new Bounds((mn + mx) * 0.5f + shift, mx - mn);
            capLocal = shift; // J sat at the origin before the shift
            var Cl = inv * (C - J) + shift;
            var toC = Cl - capLocal; capN = -(toC.sqrMagnitude > 1e-6f ? toC.normalized : Vector3.forward);
            // every open loop of the cut end(s) closed in the limb's mesh (one more submesh, the cross-section material)
            var capTris = new List<int>(512); int fans = 0;
            foreach (var L in loopsW)
            {
                var loc = new List<Vector3>(L.Count); foreach (var p in L) loc.Add(inv * (p - J) + shift);
                var lcen = Centre(loc); var outw = lcen - Cl; if (outw.sqrMagnitude < 1e-8f) outw = capN;
                int n0 = _v.Count;
                if (GoreStump.FanInto(_v, _n, _uv, _c, capTris, loc, outw.normalized, 0.002f, out _, out _) > 0)
                {
                    fans++;
                    for (int i = n0; i < _v.Count; i++) { var nn = _n[i]; _tg.Add(new Vector4(nn.x, nn.y, nn.z, 1f)); }
                }
            }
            if (capTris.Count > 0) { _subs.Add(capTris); _mats.Add(GoreStump.CapMaterial(skinSrc, sk.Level)); }
            capLocal += capN * 0.004f;

            var mesh = new Mesh { name = "GorePiece_" + sk.Part };
            if (_v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_v); mesh.SetNormals(_n);
            if (hasTg && _tg.Count == _v.Count) mesh.SetTangents(_tg);
            for (int ch = 0; ch < 4; ch++) if (hasUv[ch] || ch == 0) mesh.SetUVs(ch, _uv[ch]);
            if (hasCol) mesh.SetColors(_c);
            mesh.subMeshCount = _subs.Count;
            for (int s = 0; s < _subs.Count; s++) mesh.SetTriangles(_subs[s], s, false);
            mesh.RecalculateBounds();
            sk.Meshes.Add(mesh); MeshBytes += (long)_v.Count * (12 + 12 + 16 + 64 + 16);
            var go = new GameObject("Limb"); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = _mats.ToArray(); mr.shadowCastingMode = ShadowCastingMode.On;
            sk.Main = mr;
            Debug.Log($"[Gore] piece {sk.ItemId} {sk.Part}: {_v.Count} verts, {_subs.Count} submeshes, loops {loopsW.Count} → caps {fans}, radius {radius:0.000}");
            return true;
        }

        static Vector3 Centre(List<Vector3> L) { Vector3 c = Vector3.zero; foreach (var p in L) c += p; return L.Count > 0 ? c / L.Count : c; }

        /// <summary>The perpendicular direction the limb is thinnest along (PCA in the plane normal to the long axis), signed
        /// to keep the side that faced up still up.</summary>
        Vector3 MinorAxis(Vector3 axis, Vector3 C)
        {
            var e1 = Vector3.ProjectOnPlane(Vector3.up, axis); if (e1.sqrMagnitude < 1e-4f) e1 = Vector3.ProjectOnPlane(Vector3.right, axis); e1.Normalize();
            var e2 = Vector3.Cross(axis, e1).normalized;
            double a = 0, b = 0, c = 0;
            for (int i = 0; i < _v.Count; i += 2) { var d = _v[i] - C; double x = Vector3.Dot(d, e1), y = Vector3.Dot(d, e2); a += x * x; b += x * y; c += y * y; }
            double th = 0.5 * Math.Atan2(2 * b, a - c) + Math.PI * 0.5;   // major + 90° = minor
            var up = (float)Math.Cos(th) * e1 + (float)Math.Sin(th) * e2;
            if (Vector3.Dot(up, Vector3.up) < 0) up = -up;
            return up;
        }

        // ---- fallback: a procedural limb with the rig's proportions
        void FallbackLimb(Skin sk, Transform root, ActorRig rig, Material skinSrc, int variant, out Bounds lb, out Vector3 cap, out Vector3 capN, out float radius)
        {
            float L, r;
            switch (sk.Part)
            {
                case SeverPart.Head: L = 0.24f; r = 0.1f; break;
                case SeverPart.HandL: case SeverPart.HandR: L = 0.19f; r = 0.035f; break;
                case SeverPart.LegL: case SeverPart.LegR: L = 0.88f; r = 0.07f; break;
                default: L = 0.62f; r = 0.045f; break;
            }
            if (rig != null)
            {
                Transform A(HBone b) => rig.HasBone(b) ? rig.Bone(b) : null;
                float D(HBone x, HBone y) { var p = A(x); var q = A(y); return p != null && q != null ? Vector3.Distance(p.position, q.position) : 0f; }
                switch (sk.Part)
                {
                    case SeverPart.ArmL: { float d = D(HBone.UpperArmL, HBone.LowerArmL) + D(HBone.LowerArmL, HBone.HandL); if (d > 0.3f) L = d + 0.16f; break; }
                    case SeverPart.ArmR: { float d = D(HBone.UpperArmR, HBone.LowerArmR) + D(HBone.LowerArmR, HBone.HandR); if (d > 0.3f) L = d + 0.16f; break; }
                    case SeverPart.LegL: { float d = D(HBone.UpperLegL, HBone.LowerLegL) + D(HBone.LowerLegL, HBone.FootL); if (d > 0.4f) L = d + 0.08f; break; }
                    case SeverPart.LegR: { float d = D(HBone.UpperLegR, HBone.LowerLegR) + D(HBone.LowerLegR, HBone.FootR); if (d > 0.4f) L = d + 0.08f; break; }
                }
            }
            var skin = new Color(0.85f, 0.72f, 0.62f);
            if (variant == 1) skin = new Color(skin.r * 0.8f, skin.g * 0.84f, skin.b * 0.84f);
            if (variant == 2) skin = new Color(skin.r * 0.74f, skin.g * 0.66f, skin.b * 0.56f);
            var mb = new MeshBuilder(); mb.Set(MS.Ceramic, skin);
            float z0 = -L * 0.5f;
            if (sk.Part == SeverPart.Head)
            {
                mb.Ellipsoid(new Vector3(0, r + 0.004f, 0.03f), new Vector3(r * 0.92f, r, r * 1.1f), 18, 12);
                mb.Set(MS.Cloth, new Color(0.12f, 0.08f, 0.06f)); mb.Ellipsoid(new Vector3(0, r + 0.03f, 0.05f), new Vector3(r * 0.96f, r * 0.9f, r * 1.05f), 18, 10);
                mb.Set(MS.Ceramic, skin); mb.Rod(new Vector3(0, r * 0.8f, z0), new Vector3(0, r * 0.9f, -0.04f), 0.045f, 12, false);
            }
            else if (sk.Part == SeverPart.HandL || sk.Part == SeverPart.HandR)
            {
                mb.BevelBox(new Vector3(0, 0.02f, -0.02f), new Vector3(0.085f, 0.03f, 0.1f), 0.012f);
                for (int f = 0; f < 4; f++) mb.Rod(new Vector3(-0.03f + f * 0.02f, 0.02f, 0.03f), new Vector3(-0.034f + f * 0.023f, 0.015f, 0.09f - Mathf.Abs(f - 1.5f) * 0.01f), 0.009f, 6, true);
                mb.Rod(new Vector3(0.045f, 0.02f, -0.03f), new Vector3(0.07f, 0.018f, 0.02f), 0.011f, 6, true);
            }
            else
            {
                mb.Rod(new Vector3(0, r + 0.004f, z0), new Vector3(0, r * 0.85f + 0.004f, z0 + L * 0.48f), r, 14, true);
                mb.Rod(new Vector3(0, r * 0.85f + 0.004f, z0 + L * 0.48f), new Vector3(0, r * 0.7f + 0.004f, z0 + L * 0.84f), r * 0.78f, 14, true);
                mb.Ellipsoid(new Vector3(0, r * 0.55f + 0.004f, z0 + L * 0.92f), new Vector3(r * 0.9f, r * 0.55f, L * 0.09f), 12, 8);
            }
            var mesh = mb.ToMesh("GorePieceFallback_" + sk.Part, out var slots); sk.Meshes.Add(mesh);
            var go = new GameObject("Limb"); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = MansionMats.Materials(slots); sk.Main = mr;
            lb = mesh.bounds; radius = sk.Part == SeverPart.Head ? 0.05f : r;
            cap = new Vector3(0, (sk.Part == SeverPart.Head ? r * 0.85f : r) + 0.004f, z0 - 0.002f); capN = Vector3.back;
        }

        // ---- burnt: nothing soft left
        void Burnt(Skin sk, Transform root, ActorRig rig)
        {
            var rnd = new System.Random((int)(GoreWorld.Hash(sk.ItemId) & 0x7FFFFFFF));
            float L = sk.Part == SeverPart.LegL || sk.Part == SeverPart.LegR ? 0.5f : sk.Part == SeverPart.Head ? 0.2f : 0.4f;
            var mb = new MeshBuilder();
            int n = 6 + rnd.Next(4);
            for (int i = 0; i < n; i++)
            {
                float z = ((float)rnd.NextDouble() - 0.5f) * L, x = ((float)rnd.NextDouble() - 0.5f) * 0.14f;
                float len = 0.03f + 0.09f * (float)rnd.NextDouble(), w = 0.008f + 0.012f * (float)rnd.NextDouble();
                var tint = (float)rnd.NextDouble() < 0.3f ? new Color(0.36f, 0.32f, 0.26f) : Color.Lerp(GorePalette.Char, new Color(0.14f, 0.12f, 0.1f), (float)rnd.NextDouble());
                mb.Set(MS.Bone, tint);
                mb.Push(new Vector3(x, w * 0.5f + 0.003f, z), Quaternion.Euler((float)rnd.NextDouble() * 20f - 10f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 30f - 15f), Vector3.one);
                mb.BevelBox(Vector3.zero, new Vector3(w, w * 0.8f, len), w * 0.3f);
                mb.Pop();
            }
            var mesh = mb.ToMesh("GoreBurnt_" + sk.Part, out var slots); sk.Meshes.Add(mesh);
            var go = new GameObject("Shards"); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = MansionMats.Materials(slots); sk.Main = mr;
            try
            {
                var ash = TraceFactory.Create("Ash", root.position + Vector3.up * 0.003f, Vector3.up, Mathf.Max(0.35f, L), GorePalette.Ash);
                if (ash != null) ash.transform.SetParent(root, true);
            }
            catch { }
            sk.Radius = 0.04f;
        }

        // ---- the sheet
        /// <summary>The linen material, drawn from both sides (one face, never two coincident ones).</summary>
        static Material Linen2S()
        {
            if (_linen2S != null) return _linen2S;
            MansionMats.Init();
            var src = MansionMats.Get(MS.Linen);
            _linen2S = src != null ? new Material(src) { name = "GoreLinen2S" } : null;
            if (_linen2S != null && _linen2S.HasProperty("_Cull")) _linen2S.SetFloat("_Cull", 0f);
            return _linen2S;
        }

        /// <summary>
        /// Gore 2 (not wrapped): the sheet the part was carried in lies opened out on the floor under it — a smooth grid with
        /// shared vertices, lifted clear of the floorboards, one side still folded up against the limb, the other lying open
        /// with a torn edge and loose folds; the blood soaks through from under the cut end in a smooth gradient (wet crimson
        /// → old brown → the cloth), never in bands. Gore 1 / stowed: the same cloth wrapped round the proximal two thirds and
        /// gathered shut over the cut end, tied with cord.
        /// </summary>
        void Linen(Skin sk, Transform root, Bounds lb, Vector3 cap, Vector3 capN, bool wrapped, bool water, bool soil)
        {
            var rnd = new System.Random((int)(GoreWorld.Hash(sk.ItemId + "linen") & 0x7FFFFFFF));
            var cloth = water ? new Color(0.55f, 0.57f, 0.58f) : soil ? new Color(0.52f, 0.45f, 0.36f) : GorePalette.Linen;
            var brown = new Color(0.16f, 0.07f, 0.04f);
            var V = new List<Vector3>(512); var UV = new List<Vector2>(512); var C = new List<Color>(512); var T = new List<int>(2048);
            float rx = lb.extents.x + 0.03f, ry = lb.extents.y + 0.025f, len = lb.size.z;
            float seedA = (float)rnd.NextDouble() * 10f, seedB = (float)rnd.NextDouble() * 10f;
            // which end is cut: the soak is heaviest there
            float cutZ = cap.z, farZ = Mathf.Abs(cutZ - lb.min.z) < Mathf.Abs(cutZ - lb.max.z) ? lb.max.z : lb.min.z;
            float Soak(Vector3 p, float extra)
            {
                float along = Mathf.Clamp01(Mathf.Abs(p.z - cutZ) / Mathf.Max(0.05f, Mathf.Abs(farZ - cutZ)));
                float under = Mathf.Clamp01(1f - Mathf.Abs(p.x - lb.center.x) / (rx * 1.6f));
                float n = Mathf.PerlinNoise(p.x * 9f + seedA, p.z * 9f + seedB) - 0.5f;
                return Mathf.Clamp01((1f - along) * 1.05f * (0.55f + 0.45f * under) + n * 0.35f + extra);
            }
            if (wrapped)
            {
                // the proximal ~65 % bound up; the cut end hidden, the hand / foot / hair out in the air
                const int seg = 18, rings = 12;
                bool cutAtMin = Mathf.Abs(cutZ - lb.min.z) <= Mathf.Abs(cutZ - lb.max.z);
                float z0 = cutAtMin ? lb.min.z - 0.035f : lb.max.z + 0.035f, z1 = cutAtMin ? lb.min.z + len * 0.65f : lb.max.z - len * 0.65f;
                for (int r = 0; r <= rings; r++)
                    for (int s = 0; s <= seg; s++)
                    {
                        float t = r / (float)rings, a = s / (float)seg * Mathf.PI * 2f, z = Mathf.Lerp(z0, z1, t);
                        // gathered shut over the cut end, loosely gathered where the hand / foot / hair comes out
                        float pinch = t < 0.5f ? 1f - 0.97f * Mathf.Pow(1f - t * 2f, 5f) : 1f - 0.45f * Mathf.Pow(t * 2f - 1f, 6f);
                        float wr = 1f + 0.05f * Mathf.Sin(a * 5f + t * 9f) + 0.03f * (Mathf.PerlinNoise(a * 2f + seedA, t * 6f) - 0.5f);
                        var p = new Vector3(Mathf.Cos(a) * rx * pinch * wr, lb.center.y + Mathf.Sin(a) * ry * pinch * wr, z);
                        p.y = Mathf.Max(p.y, 0.008f);
                        V.Add(p); UV.Add(new Vector2(a * 0.12f, z));
                        float soak = Mathf.Clamp01(1f - t * 1.5f) * 0.95f + (Mathf.Sin(a) < -0.3f ? 0.2f : 0f) + (Mathf.PerlinNoise(a * 3f + seedB, z * 8f) - 0.5f) * 0.3f;
                        C.Add(Soaked(cloth, brown, soak));
                    }
                for (int r = 0; r < rings; r++)
                    for (int s = 0; s < seg; s++)
                    {
                        int a = r * (seg + 1) + s, b = a + 1, c = a + seg + 1, d = c + 1;
                        T.Add(a); T.Add(c); T.Add(b); T.Add(b); T.Add(c); T.Add(d);
                    }
                // cord ties at both ends of the wrap (thin tubes, the same draw)
                foreach (float tt in new[] { 0.14f, 0.86f })
                {
                    float z = Mathf.Lerp(z0, z1, tt), pinch = tt < 0.5f ? 1f - 0.97f * Mathf.Pow(1f - tt * 2f, 5f) : 1f - 0.45f * Mathf.Pow(tt * 2f - 1f, 6f);
                    Ring(V, UV, C, T, new Vector3(0, lb.center.y, z), rx * pinch * 1.02f + 0.004f, ry * pinch * 1.02f + 0.004f, 0.006f, new Color(0.24f, 0.17f, 0.12f));
                }
            }
            else
            {
                // opened out: across x from the open side (lying flat, torn edge) under the limb to the side still folded up
                // against it; along z past both ends. Lifted 1–2 cm off the floor so no floorboard ever shows through.
                float open = rnd.NextDouble() < 0.5 ? -1f : 1f;
                const int nx = 26, nz = 16;
                float x0 = -rx * 1.05f - 0.24f, x1 = rx * 1.1f;             // in "open-side" coordinates (open side negative)
                float za = lb.min.z - 0.07f, zb = lb.max.z + 0.07f;
                for (int iz = 0; iz <= nz; iz++)
                    for (int ix = 0; ix <= nx; ix++)
                    {
                        float u = ix / (float)nx, w = iz / (float)nz;
                        float xo = Mathf.Lerp(x0, x1, u), z = Mathf.Lerp(za, zb, w);
                        // torn outer edge of the open side, ragged ends
                        if (ix == 0) xo -= 0.035f * Mathf.PerlinNoise(z * 11f + seedA, 1.7f);
                        if (iz == 0) z -= 0.03f * Mathf.PerlinNoise(xo * 13f + seedB, 2.3f);
                        if (iz == nz) z += 0.03f * Mathf.PerlinNoise(xo * 13f + seedA, 4.1f);
                        float y = 0.012f + 0.006f * Mathf.PerlinNoise(xo * 7f + seedA, z * 7f + seedB);
                        // loose folds in the open part
                        if (xo < -rx) y += 0.012f * Mathf.Max(0f, Mathf.Sin((z * 6f + seedB) + xo * 3f)) * Mathf.InverseLerp(-rx, x0, xo);
                        // the far side rises and leans against the limb
                        if (xo > rx * 0.55f)
                        {
                            float k = Mathf.InverseLerp(rx * 0.55f, x1, xo);
                            y = Mathf.Lerp(y, ry * 1.25f * (0.8f + 0.2f * Mathf.PerlinNoise(z * 5f + seedA, 3.3f)), Mathf.SmoothStep(0f, 1f, k));
                            xo = Mathf.Lerp(xo, rx * 0.98f, k * k * 0.6f);
                        }
                        var p = new Vector3(xo * open + lb.center.x, y, z);
                        V.Add(p); UV.Add(new Vector2(p.x, p.z));
                        C.Add(Soaked(cloth, brown, Soak(p, xo < -rx * 1.2f ? -0.2f : 0f)));
                    }
                for (int iz = 0; iz < nz; iz++)
                    for (int ix = 0; ix < nx; ix++)
                    {
                        int a = iz * (nx + 1) + ix, b = a + 1, c = a + nx + 1, d = c + 1;
                        if (open > 0) { T.Add(a); T.Add(c); T.Add(b); T.Add(b); T.Add(c); T.Add(d); }
                        else { T.Add(a); T.Add(b); T.Add(c); T.Add(b); T.Add(d); T.Add(c); }
                    }
            }
            if (V.Count == 0) return;
            var mesh = new Mesh { name = "GoreLinen_" + sk.Part };
            mesh.SetVertices(V); mesh.SetUVs(0, UV); mesh.SetColors(C); mesh.SetTriangles(T, 0);
            mesh.RecalculateNormals();
            // the open sheet's normals point up (it is lit from above either way; the material draws both sides)
            if (!wrapped) { var nrm = mesh.normals; for (int i = 0; i < nrm.Length; i++) if (nrm[i].y < 0f) nrm[i] = -nrm[i]; mesh.normals = nrm; }
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            sk.Meshes.Add(mesh); MeshBytes += (long)V.Count * 52;
            var go = new GameObject("Linen"); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Linen2S() ?? MansionMats.Get(MS.Linen); mr.shadowCastingMode = ShadowCastingMode.On;
        }

        /// <summary>A thin elliptical tube (a cord tie) into the same streams.</summary>
        static void Ring(List<Vector3> V, List<Vector2> UV, List<Color> C, List<int> T, Vector3 c, float ax, float ay, float r, Color col)
        {
            const int su = 20, sv = 5; int b0 = V.Count;
            for (int i = 0; i <= su; i++)
            {
                float a = i / (float)su * Mathf.PI * 2f; var ring = c + new Vector3(Mathf.Cos(a) * ax, Mathf.Sin(a) * ay, 0f);
                var outw = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                for (int j = 0; j <= sv; j++)
                {
                    float b = j / (float)sv * Mathf.PI * 2f;
                    var p = ring + (outw * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b)) * r; p.y = Mathf.Max(p.y, 0.006f);
                    V.Add(p); UV.Add(new Vector2(i * 0.05f, j * 0.05f)); C.Add(col);
                }
            }
            for (int i = 0; i < su; i++)
                for (int j = 0; j < sv; j++)
                {
                    int a = b0 + i * (sv + 1) + j, b = a + 1, d = a + sv + 1, e = d + 1;
                    T.Add(a); T.Add(d); T.Add(b); T.Add(b); T.Add(d); T.Add(e);
                }
        }

        /// <summary>Linen darkening through old-blood brown to near-black crimson (never through pink).</summary>
        static Color Soaked(Color cloth, Color brown, float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.6f ? Color.Lerp(cloth, brown, t / 0.6f) : Color.Lerp(brown, GorePalette.Thick, (t - 0.6f) / 0.4f);
        }
    }
}
