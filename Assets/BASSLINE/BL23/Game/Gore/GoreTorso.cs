using System.Collections.Generic;
using System.Text;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// The dismembered body (on the victim's ActorView GameObject, added by <see cref="GorePieces"/>): each skinned renderer
    /// with a readable mesh gets a copy without the severed parts' triangles (bind poses and blend shapes kept; the severed
    /// bones' weights handed to the bone each part hung from; cached per mesh and mask; the original is kept for pieces and
    /// replays). Each cut is closed where it really is: the open loops the cut leaves in the drawn mesh are capped
    /// (<see cref="GoreStump.Caps"/>), parented to the bone the part hung from, with a soaked band on the cloth behind the edge.
    /// A severed head also has its bone collapsed (whatever else hangs from it or is skinned to it goes with it), and any
    /// renderer still sitting where the head was is switched off. Every 0.25 s it checks the renderers still carry the copy
    /// (the character agent may rebuild meshes: a new mesh becomes the new original); every frame it keeps renderers inside
    /// severed subtrees, and wound patches at a cut, off (Rig.SetVisible(true) turns everything back on). Unreadable meshes
    /// fall back to collapsing the severed subtree behind a disc. During a replay the body is whole again.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class GoreTorso : MonoBehaviour
    {
        public int Mask { get; private set; }
        public int Level { get; private set; } = -1;
        ActorView _view; ActorRig _rig;
        readonly Dictionary<SkinnedMeshRenderer, Mesh> _orig = new Dictionary<SkinnedMeshRenderer, Mesh>();
        readonly Dictionary<SkinnedMeshRenderer, Mesh> _cut = new Dictionary<SkinnedMeshRenderer, Mesh>();
        readonly List<GameObject> _stumps = new List<GameObject>();
        readonly List<Renderer> _off = new List<Renderer>();
        readonly List<Transform> _offRoot = new List<Transform>();
        float _collectAt;
        readonly List<Transform> _collapse = new List<Transform>();
        readonly List<Vector3> _collapseScale = new List<Vector3>();
        readonly List<Vector3> _cutCentres = new List<Vector3>();
        bool _applied, _replay; float _next;

        public bool Applied => _applied;
        public IReadOnlyList<GameObject> Stumps => _stumps;
        /// <summary>Where the centre of the head was before it was cut off (world; zero when the head is on). Probe.</summary>
        public Vector3 HeadProbePoint { get; private set; }
        /// <summary>The last cut's report (per skin: triangles removed per part, caps, time). Probe / logs.</summary>
        public string LastReport { get; private set; } = "";
        public float LastApplyMs { get; private set; }

        /// <summary>The mesh a renderer had before the cut (null when this component never touched it).</summary>
        public Mesh OriginalOf(SkinnedMeshRenderer smr) => smr != null && _orig.TryGetValue(smr, out var m) ? m : (smr != null ? smr.sharedMesh : null);

        public void Set(int mask, int level)
        {
            if (mask == Mask && level == Level && _applied) return;
            Mask = mask; Level = level;
            Apply();
        }

        /// <summary>After a replay (ActorView.RestoreFromKernel): put the cut back.</summary>
        public void Reapply() { if (Mask != 0) { _applied = false; Apply(); } }

        /// <summary>Give collapsed bones their scale back until the next LateUpdate (a bake of the original mesh needs the
        /// head where it was).</summary>
        public void ReleaseCollapse()
        {
            for (int i = 0; i < _collapse.Count; i++) if (_collapse[i] != null) _collapse[i].localScale = _collapseScale[i];
        }

        void Bind() { if (_view == null) _view = GetComponent<ActorView>(); _rig = _view != null ? _view.Rig : null; }

        void Apply()
        {
            Bind();
            Restore(false);
            if (_rig == null || Mask == 0) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var rep = new StringBuilder();
            rep.Append($"[Gore] torso {(_view != null ? _view.Id : name)} mask={MaskText(Mask)} level={Level}");
            try
            {
                if ((Mask & GoreMeshCut.Bit(SeverPart.Head)) != 0) HeadProbePoint = SafeHeadCentre();
                foreach (var smr in _rig.Skins)
                {
                    if (smr == null) continue;
                    var cur = smr.sharedMesh; if (cur == null) continue;
                    if (!_orig.TryGetValue(smr, out var orig) || (cur != orig && (!_cut.TryGetValue(smr, out var c0) || cur != c0))) { orig = cur; _orig[smr] = orig; }
                    if (!orig.isReadable) { rep.Append($"\n  skin {smr.name}: mesh {orig.name} NOT READABLE (collapse fallback)"); continue; }
                    var cut = GoreMeshCut.Torso(smr, _rig, orig, Mask);
                    if (cut == null) { rep.Append($"\n  skin {smr.name}: cut failed"); continue; }
                    smr.sharedMesh = cut; _cut[smr] = cut;
                    var st = GoreMeshCut.StatsOf(orig, Mask); var cls = GoreMeshCut.Classify(smr, _rig, orig);
                    rep.Append($"\n  skin {smr.name}{(GoreMeshCut.IsTopLod(_rig, smr) ? "" : " (lod)")}: kept {st.Kept} removed {st.Removed} [");
                    for (int p = 0; p < GoreMeshCut.PartCount; p++) if ((Mask & (1 << p)) != 0) rep.Append($" {(SeverPart)p}:{(st.PerPart != null ? st.PerPart[p] : 0)}");
                    rep.Append($" ] skirt-kept {cls?.SkirtKept ?? 0} {st.Ms:0.0}ms");
                }
                BuildStumps(rep);
                CollectOff(rep);
                _applied = true;
            }
            catch (System.Exception e) { Debug.LogWarning("[Gore] torso " + name + ": " + e); }
            LastApplyMs = (float)sw.Elapsed.TotalMilliseconds;
            rep.Append($"\n  apply {LastApplyMs:0.0} ms");
            LastReport = rep.ToString();
            Debug.Log(LastReport);
        }

        static string MaskText(int mask) { var sb = new StringBuilder(); for (int p = 0; p < GoreMeshCut.PartCount; p++) if ((mask & (1 << p)) != 0) sb.Append(sb.Length > 0 ? "+" : "").Append((SeverPart)p); return sb.ToString(); }

        /// <summary>The head's centre, from the neck and head bones (never from anchors under a collapsed head).</summary>
        Vector3 SafeHeadCentre()
        {
            var head = _rig.Bone(HBone.Head); var neck = _rig.Bone(HBone.Neck);
            if (head == null) return transform.position + Vector3.up * 1.5f;
            var up = neck != null ? (head.position - neck.position) : Vector3.up; if (up.sqrMagnitude < 1e-6f) up = Vector3.up;
            float s = _rig.Height > 0.5f ? _rig.Height / 1.75f : 1f;
            return head.position + up.normalized * 0.1f * s;
        }

        /// <summary>Whole again (replay, or before re-cutting).</summary>
        public void Restore(bool forget)
        {
            foreach (var kv in _orig) if (kv.Key != null && kv.Value != null && _cut.TryGetValue(kv.Key, out var c) && kv.Key.sharedMesh == c) kv.Key.sharedMesh = kv.Value;
            _cut.Clear();
            foreach (var s in _stumps) if (s != null) Destroy(s);
            _stumps.Clear(); _cutCentres.Clear();
            for (int i = 0; i < _collapse.Count; i++) if (_collapse[i] != null) _collapse[i].localScale = _collapseScale[i];
            _collapse.Clear(); _collapseScale.Clear();
            // give the subtree back its visibility only when the body itself is drawn
            bool bodyOn = false; if (_rig != null) foreach (var s in _rig.Skins) if (s != null && s.enabled) { bodyOn = true; break; }
            foreach (var r in _off) if (r != null && bodyOn) r.enabled = true;
            _offRoot.Clear();
            _off.Clear();
            _applied = false;
            if (forget) { _orig.Clear(); Mask = 0; HeadProbePoint = Vector3.zero; }
        }

        // ------------------------------------------------------------------ caps
        void BuildStumps(StringBuilder rep)
        {
            var skinSrc = SkinMaterial();
            SkinnedMeshRenderer top = null; foreach (var s in _rig.Skins) if (s != null && GoreMeshCut.IsTopLod(_rig, s) && _cut.ContainsKey(s)) { top = s; break; }
            var baked = new Mesh(); Vector3[] bv = null; Matrix4x4 xf = Matrix4x4.identity;
            try
            {
                if (top != null) { top.BakeMesh(baked, true); bv = baked.vertices; xf = Matrix4x4.TRS(top.transform.position, top.transform.rotation, Vector3.one); }
                for (int p = 0; p <= (int)SeverPart.LegR; p++)
                {
                    if ((Mask & (1 << p)) == 0) continue;
                    var part = (SeverPart)p;
                    // a hand cut off an arm that is itself gone needs no cap
                    if (part == SeverPart.HandL && (Mask & GoreMeshCut.Bit(SeverPart.ArmL)) != 0) continue;
                    if (part == SeverPart.HandR && (Mask & GoreMeshCut.Bit(SeverPart.ArmR)) != 0) continue;
                    var parent = _rig.Bone(GoreMeshCut.ParentBone(part)); if (parent == null) parent = _rig.transform;
                    GameObject go = null; string how = "";
                    if (top != null && bv != null && _orig.TryGetValue(top, out var orig) && orig != null && orig.isReadable)
                    {
                        var cut = LoopsOf(top, orig, part, bv, xf);
                        if (cut != null && cut.Loops.Count > 0)
                        {
                            go = GoreStump.Caps(parent, cut, Level, skinSrc, true, "GoreCap_" + part, out var info);
                            if (go != null) { _cutCentres.Add(info.Centre); how = $"caps: {info.Loops} loop(s) of {cut.Loops.Count}, r {info.Radius:0.000} m, {info.Tris} tris"; }
                        }
                        if (go == null) how = $"no usable loop ({cut?.Loops.Count ?? 0} found): disc";
                    }
                    else how = "unreadable: disc + collapse";
                    if (go == null)
                    {
                        // the disc at the joint (unreadable meshes, or no clean loop)
                        var joint = _rig.Bone(GoreMeshCut.RootBone(part)); var jparent = parent;
                        Vector3 centre = joint != null ? joint.position : parent.position;
                        Vector3 normal = joint != null && jparent != null ? joint.position - jparent.position : Vector3.up;
                        if (part == SeverPart.Head && _rig.Bone(HBone.Neck) != null) { var nk = _rig.Bone(HBone.Neck).position; centre = Vector3.Lerp(nk, centre, 0.75f); normal = centre - nk; }
                        float radius = part == SeverPart.Head ? 0.055f : part == SeverPart.LegL || part == SeverPart.LegR ? 0.08f : part == SeverPart.HandL || part == SeverPart.HandR ? 0.035f : 0.05f;
                        go = GoreStump.Build(parent, centre, normal, radius, Level, skinSrc, true, "GoreStump_" + part);
                        _cutCentres.Add(centre);
                        bool readable = top != null && _orig.TryGetValue(top, out var o2) && o2 != null && o2.isReadable;
                        if (!readable)
                        {
                            var root = _rig.Bone(GoreMeshCut.RootBone(part));
                            if (root != null && part != SeverPart.Head && !_collapse.Contains(root)) { _collapse.Add(root); _collapseScale.Add(root.localScale); }
                        }
                    }
                    if (go != null) _stumps.Add(go);
                    rep.Append($"\n  cut {part}: {how}");
                }
            }
            finally { Destroy(baked); }
            // a severed head: its bone collapses, so nothing parented or skinned to it can stay on the neck (the body copy
            // no longer uses it, so the neck keeps its shape)
            if ((Mask & GoreMeshCut.Bit(SeverPart.Head)) != 0)
            {
                var head = _rig.Bone(HBone.Head);
                if (head != null && !_collapse.Contains(head)) { _collapse.Add(head); _collapseScale.Add(head.localScale); }
            }
        }

        /// <summary>The open loops the cut of one part left in the drawn (top) skin, in world space, and its outward side.</summary>
        GoreStump.CutLoops LoopsOf(SkinnedMeshRenderer smr, Mesh orig, SeverPart part, Vector3[] bakedCut, Matrix4x4 xf)
        {
            var cls = GoreMeshCut.Classify(smr, _rig, orig); if (cls == null) return null;
            var side = GoreMeshCut.Sides(cls, orig, GoreMeshCut.Bit(part), Mask, false);
            var loops = GoreMeshCut.Loops(orig, side);
            var cut = new GoreStump.CutLoops();
            var a = _rig.Bone(GoreMeshCut.ParentBone(part)); var b = _rig.Bone(GoreMeshCut.RootBone(part)); var c = _rig.HasBone(GoreMeshCut.ChildBone(part)) ? _rig.Bone(GoreMeshCut.ChildBone(part)) : null;
            Vector3 dir = part == SeverPart.Head ? (b != null && a != null ? b.position - a.position : Vector3.up) : (c != null && b != null ? c.position - b.position : (b != null && a != null ? b.position - a.position : Vector3.up));
            cut.Outward = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.up;
            foreach (var L in loops)
            {
                var w = new List<Vector3>(L.Length);
                foreach (int v in L) if (v < bakedCut.Length) w.Add(xf.MultiplyPoint3x4(bakedCut[v]));
                if (w.Count >= 4) cut.Loops.Add(w);
            }
            return cut;
        }

        Material SkinMaterial()
        {
            if (_rig == null) return null;
            foreach (var smr in _rig.Skins)
            {
                if (smr == null || !GoreMeshCut.IsTopLod(_rig, smr)) continue;
                var m = GoreStump.SkinSource(smr.sharedMaterials); if (m != null) return m;
            }
            return null;
        }

        // ------------------------------------------------------------------ what must stay off
        /// <summary>Renderers inside a severed subtree (hair cards, hats, gloves, wound patches), wound patches hanging on the
        /// bone a part was cut from (the neck gash of a severed head), never a game item held in the hand (it is detached and
        /// shown again when dropped) and never our own caps.</summary>
        void CollectOff(StringBuilder rep = null)
        {
            _off.Clear(); _offRoot.Clear();
            if (_rig == null || _rig.Bones == null) return;
            HideNearHead(rep);
            for (int p = 0; p <= (int)SeverPart.LegR; p++)
            {
                if ((Mask & (1 << p)) == 0) continue;
                var part = (SeverPart)p;
                var t = _rig.HasBone(GoreMeshCut.RootBone(part)) ? _rig.Bone(GoreMeshCut.RootBone(part)) : null; if (t == null) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is SkinnedMeshRenderer || r.GetComponentInParent<ItemTag>() != null || r.GetComponentInParent<GorePieceTag>() != null || Ours(r)) continue;
                    if (!_off.Contains(r)) { _off.Add(r); _offRoot.Add(t); }
                }
                // wound patches on the parent bone, close to the cut
                var pb = _rig.Bone(GoreMeshCut.ParentBone(part));
                if (pb != null)
                    for (int i = 0; i < pb.childCount; i++)
                    {
                        var ch = pb.GetChild(i); if (!ch.name.StartsWith("Wound_")) continue;
                        var r = ch.GetComponent<Renderer>(); if (r == null || _off.Contains(r)) continue;
                        if (NearCut(r.bounds.center, 0.2f)) { _off.Add(r); _offRoot.Add(null); }
                    }
            }
            _collectAt = Time.unscaledTime + 1f;
        }

        bool NearCut(Vector3 p, float d) { foreach (var c in _cutCentres) if ((c - p).sqrMagnitude < d * d) return true; return false; }

        bool Ours(Renderer r) { foreach (var s in _stumps) if (s != null && r.transform.IsChildOf(s.transform)) return true; return false; }

        /// <summary>A severed head leaves nothing on the neck: any renderer of this body (other than the cut skins and the
        /// caps) whose centre is within 0.2 m of where the head was is switched off, and every one within 0.25 m is logged.</summary>
        void HideNearHead(StringBuilder rep)
        {
            if (HeadProbePoint == Vector3.zero) return;
            if ((Mask & GoreMeshCut.Bit(SeverPart.Head)) == 0 || _rig == null) return;
            var hc = HeadProbePoint; int n = 0; var headBone = _rig.Bone(HBone.Head);
            foreach (var r in _rig.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || Ours(r)) continue;
                bool skin = false; foreach (var s in _rig.Skins) if (s == r) { skin = true; break; }
                if (skin) continue;
                if (r.GetComponentInParent<ItemTag>() != null || r.GetComponentInParent<GorePieceTag>() != null) continue;
                if ((_rig.HandAnchorL != null && r.transform.IsChildOf(_rig.HandAnchorL)) || (_rig.HandAnchorR != null && r.transform.IsChildOf(_rig.HandAnchorR)) || (_rig.PropsRoot != null && r.transform.IsChildOf(_rig.PropsRoot))) continue;
                // a separately skinned head piece (hair, eyes, a mask) has fixed, body-sized bounds: judge it by what it is
                // skinned to, not where its bounds say it is
                if (r is SkinnedMeshRenderer extra)
                {
                    float share = HeadShare(extra, headBone);
                    rep?.Append($"\n  near head: skinned {extra.name} (mesh {(extra.sharedMesh != null ? extra.sharedMesh.name : "-")}) head share {share:0.00}{(share >= 0.5f ? " → off" : "")}");
                    if (share >= 0.5f && !_off.Contains(r)) { _off.Add(r); _offRoot.Add(null); n++; }
                    continue;
                }
                float d = Vector3.Distance(r.bounds.center, hc);
                if (d > 0.25f) continue;
                var mf = r.GetComponent<MeshFilter>(); var mesh = r is SkinnedMeshRenderer sm ? sm.sharedMesh : mf != null ? mf.sharedMesh : null;
                bool off = d <= 0.2f;
                rep?.Append($"\n  near head: {r.name} ({r.GetType().Name}, mesh {(mesh != null ? mesh.name : "-")}) {d:0.00} m enabled={r.enabled}{(off ? " → off" : "")}");
                if (off && !_off.Contains(r)) { _off.Add(r); _offRoot.Add(null); }
                n++;
            }
            if (n == 0) rep?.Append("\n  near head: nothing but the caps");
        }

        /// <summary>How much of a skinned renderer belongs to the head: 1 when its root bone is the head or under it, else the
        /// share of its vertices in the Head part (0 when its mesh cannot be read and it is not rooted there).</summary>
        float HeadShare(SkinnedMeshRenderer smr, Transform headBone)
        {
            if (smr == null) return 0f;
            if (headBone != null && smr.rootBone != null && smr.rootBone.IsChildOf(headBone)) return 1f;
            var mesh = smr.sharedMesh; if (mesh == null || !mesh.isReadable) return 0f;
            var cls = GoreMeshCut.Classify(smr, _rig, mesh); if (cls == null || cls.VertexCount == 0) return 0f;
            int bit = GoreMeshCut.Bit(SeverPart.Head), k = 0;
            for (int v = 0; v < cls.VertexCount; v++) if ((cls.Parts[v] & bit) != 0) k++;
            return k / (float)cls.VertexCount;
        }

        /// <summary>Probe: after a head cut, whatever is still drawn where the head was — enabled renderers of this body within
        /// `radius` of the head's centre now (other than the caps, items and pieces; a skin is judged by its cut, not its
        /// bounds: it must carry its cut copy with head triangles removed). Empty when the neck is clean.</summary>
        public List<string> HeadLeftovers(float radius = 0.12f)
        {
            var list = new List<string>();
            Bind();
            if (_rig == null || (Mask & GoreMeshCut.Bit(SeverPart.Head)) == 0) return list;
            var hc = SafeHeadCentre();
            foreach (var smr in _rig.Skins)
            {
                if (smr == null || !smr.enabled) continue;
                if (!_cut.TryGetValue(smr, out var c) || smr.sharedMesh != c)
                {
                    // an unreadable mesh is never cut: the collapsed head bone takes the head with it (the fallback)
                    var hb = _rig.Bone(HBone.Head);
                    if (smr.sharedMesh != null && !smr.sharedMesh.isReadable && hb != null && hb.lossyScale.sqrMagnitude < 1e-4f) continue;
                    list.Add(smr.name + " (not carrying its cut)"); continue;
                }
                _orig.TryGetValue(smr, out var o);
                var st = GoreMeshCut.StatsOf(o, Mask);
                if (st.PerPart == null || st.PerPart[(int)SeverPart.Head] == 0) list.Add(smr.name + " (no head triangles removed)");
            }
            foreach (var r in _rig.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled || Ours(r)) continue;
                bool skin = false; foreach (var s in _rig.Skins) if (s == r) { skin = true; break; }
                if (skin) continue;
                if (r.GetComponentInParent<ItemTag>() != null || r.GetComponentInParent<GorePieceTag>() != null) continue;
                if (r is SkinnedMeshRenderer sm) { if (HeadShare(sm, _rig.Bone(HBone.Head)) >= 0.5f) list.Add(r.name + " (skinned to the head)"); continue; }
                if (r.bounds.SqrDistance(hc) < radius * radius) list.Add(r.name);
            }
            return list;
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.25f;
            Bind(); if (_view == null || Mask == 0) return;
            bool replay = _view.ReplayDriven;
            if (replay != _replay)
            {
                _replay = replay;
                if (replay) Restore(false); else Apply();
                return;
            }
            if (replay || !_applied || _rig == null) return;
            // the renderers must still carry our copies (the character agent may have rebuilt a mesh)
            foreach (var smr in _rig.Skins)
            {
                if (smr == null) continue;
                var cur = smr.sharedMesh; if (cur == null) continue;
                if (_cut.TryGetValue(smr, out var c) && cur == c) continue;        // still ours
                _orig.TryGetValue(smr, out var o);
                if (cur == o && (o == null || !o.isReadable)) continue;            // an unreadable original we never cut
                Apply(); return;                                                    // restored or rebuilt: cut again
            }
            // wound patches and the like get added under the severed bones later (after a replay): look again now and then
            if (Time.unscaledTime >= _collectAt) CollectOff();
        }

        void LateUpdate()
        {
            if (!_applied || _replay) return;
            for (int i = 0; i < _off.Count; i++)
            {
                var r = _off[i]; if (r == null || !r.enabled) continue;
                if (_offRoot[i] == null || r.transform.IsChildOf(_offRoot[i])) r.enabled = false;
            }
            for (int i = 0; i < _collapse.Count; i++) { var t = _collapse[i]; if (t != null) t.localScale = _collapseScale[i] * 0.0005f; }
        }

        void OnDestroy() { Restore(true); }
    }
}
