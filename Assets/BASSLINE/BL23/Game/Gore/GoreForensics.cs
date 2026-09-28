using System.Collections.Generic;
using System.Text;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using BodyRegion = BL23.Sim.BodyRegion;

namespace BL23.Game
{
    /// <summary>
    /// What a strangled body shows that no wound patch draws (so that, from the doorway, the dead do not simply look asleep):
    /// the furrow a cord or a wire leaves round the neck, the thumb and finger bruises of bare hands, and blood and skin under
    /// the nails of someone who clawed at what was choking them. Polls the dead every 0.5 s and gives each such body a
    /// <see cref="GoreBodyMarks"/>, and any body that has lain for a while its moths (<see cref="GoreMoths"/>); bodies that
    /// are no longer dead (a new loop) lose both. Added next to GoreScene.
    /// </summary>
    public sealed class GoreForensics : MonoBehaviour
    {
        float _next;
        readonly List<GoreBodyMarks> _live = new List<GoreBodyMarks>();
        readonly List<GoreMoths> _moths = new List<GoreMoths>();
        /// <summary>Clock-minutes a body lies before the house's moths find it.</summary>
        public const double MothsAfterMin = 10;

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            var S = GoreWorld.S; var W = GoreWorld.W;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var bm = _live[i]; if (bm == null) { _live.RemoveAt(i); continue; }
                var a = S?.A(bm.ActorId);
                if (a == null || a.Status != ActorStatus.Dead) { Destroy(bm); _live.RemoveAt(i); }
            }
            for (int i = _moths.Count - 1; i >= 0; i--)
            {
                var mo = _moths[i]; if (mo == null) { _moths.RemoveAt(i); continue; }
                var a = S?.A(mo.ActorId);
                if (a == null || a.Status != ActorStatus.Dead) { Destroy(mo); _moths.RemoveAt(i); }
            }
            if (S == null || W == null || S.Actors == null) return;
            try
            {
                foreach (var a in S.Actors.Values)
                {
                    if (a.Status != ActorStatus.Dead) continue;
                    var want = GoreBodyMarks.Want(S, a); var view = W.ViewOf(a.Id);
                    if (view == null || view.Rig == null) continue;
                    // the moths come to a body that has lain a while (GoreMoths.cs)
                    if (a.Body != null && a.Body.DeathClock >= 0 && S.Clock - a.Body.DeathClock >= MothsAfterMin && view.GetComponent<GoreMoths>() == null)
                    { var mo = view.gameObject.AddComponent<GoreMoths>(); mo.ActorId = a.Id; _moths.Add(mo); }
                    var bm = view.GetComponent<GoreBodyMarks>();
                    if (want.None) { if (bm != null) { Destroy(bm); _live.Remove(bm); } continue; }
                    if (bm == null) { bm = view.gameObject.AddComponent<GoreBodyMarks>(); bm.ActorId = a.Id; _live.Add(bm); }
                    bm.Set(want);
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Gore] forensics: " + e.Message); }
        }
    }

    /// <summary>Which marks a body should carry (read from the kernel).</summary>
    public struct BodyMarkWant
    {
        public bool Ligature, Manual, Nails; public float LigWidth; public string LigType;
        public bool None => !Ligature && !Manual && !Nails;
        public int Key => (Ligature ? 1 : 0) | (Manual ? 2 : 0) | (Nails ? 4 : 0) | (Mathf.RoundToInt(LigWidth * 1000f) << 3);
    }

    /// <summary>
    /// The marks themselves, on one dead body (on its ActorView): built once the body has lain still for 1.5 s, parented to
    /// the neck and finger bones (they follow the body wherever it is moved), hidden while a replay shows the person alive or
    /// whenever the body itself is not drawn. The ligature furrow is a thin band just proud of the skin (its radius measured
    /// round the neck from the victim's own mesh in the pose it lies in), parchment-brown and pressed-looking, a little higher
    /// and darker at the back where the ends crossed; bare hands leave two thumb bruises at the front and fingertip bruises at
    /// the sides; the nails carry dark blood and skin at their free edges, one or two torn. Blood colours keep the palette rule.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public sealed class GoreBodyMarks : MonoBehaviour
    {
        public string ActorId;
        ActorView _view; BodyMarkWant _want; int _builtKey = int.MinValue; float _deadAt = -1f;
        readonly List<GameObject> _made = new List<GameObject>();
        readonly List<Renderer> _rends = new List<Renderer>();
        readonly List<Mesh> _meshes = new List<Mesh>();
        /// <summary>Where the furrow runs round the neck (world, now; zero when there is none) and what was built. Probe.</summary>
        public Vector3 NeckPoint => _neckRing != null ? _neckRing.position + _neckRing.rotation * _neckLocalCentre : Vector3.zero;
        public string LastReport { get; private set; } = "";
        Transform _neckRing; Vector3 _neckLocalCentre;

        static readonly Color Furrow = new Color(0.19f, 0.055f, 0.035f, 1f);       // abraded, parchment-brown
        static readonly Color Crossing = new Color(0.14f, 0.035f, 0.025f, 1f);     // where the ends crossed
        static readonly Color BruiseCol = new Color(0.26f, 0.07f, 0.06f, 1f);      // dark wine
        static readonly Color NailDirt = new Color(0.16f, 0.012f, 0.015f, 1f);     // blood and skin under the nail
        static readonly Color NailTorn = new Color(0.26f, 0.02f, 0.025f, 1f);

        public void Set(BodyMarkWant w) { _want = w; }

        /// <summary>From the kernel: a Choke wound at the neck made with something (a cord, a wire, a scarf) → a furrow of that
        /// width; without a weapon → bare hands; NailScratch marks or the "nails:" flag → the nails.</summary>
        public static BodyMarkWant Want(GameState S, Actor a)
        {
            var w = new BodyMarkWant();
            var b = a?.Body; if (b == null) return w;
            foreach (var wd in b.Wounds)
            {
                if (wd.Type != DamageType.Choke || wd.Region != BodyRegion.Neck || wd.Postmortem) continue;
                var it = wd.Weapon != null ? S.I(wd.Weapon) : null;
                if (it != null) { w.Ligature = true; w.LigType = it.Type; w.LigWidth = LigatureWidth(it.Type); }
                else w.Manual = true;
            }
            if (w.Ligature) w.Manual = false;
            if (b.GoreMarks != null) foreach (var m in b.GoreMarks) if (m.Kind == GoreKind.NailScratch) { w.Nails = true; break; }
            if (!w.Nails && S.Flags != null && S.Flags.ContainsKey("nails:" + a.Id)) w.Nails = true;
            // nothing to show on a neck that is no longer there
            foreach (var it in Gore.PiecesOf(S, a.Id)) if (Gore.TryPart(it, out var p) && p == SeverPart.Head) { w.Ligature = false; w.Manual = false; break; }
            return w;
        }

        static float LigatureWidth(string type)
        {
            switch (type)
            {
                case "PianoWire": case "Wire": case "Tripwire": case "Thread": case "Fuse": case "FishingLine": return 0.004f;
                case "Scarf": case "Sheet": case "Towel": case "Tie": case "Necktie": case "Cloak": return 0.02f;
                case "Rope": case "CurtainCord": case "ExtensionCord": case "Cord": case "Belt": case "Chain": return 0.011f;
            }
            return 0.01f;
        }

        void LateUpdate()
        {
            if (_view == null) _view = GetComponent<ActorView>();
            var rig = _view != null ? _view.Rig : null; if (rig == null) return;
            int key = _want.Key * 3 + Mathf.Clamp(Settings.Gore, 0, 2);
            if (key != _builtKey)
            {
                bool dead = rig.Anim == null || rig.Anim.IsDead;
                if (!dead || _view.ReplayDriven) _deadAt = -1f;
                else
                {
                    if (_deadAt < 0f) _deadAt = Time.unscaledTime;
                    if (Time.unscaledTime - _deadAt >= 1.5f)
                    {
                        _builtKey = key;
                        try { Build(rig); } catch (System.Exception e) { Debug.LogWarning("[Gore] body marks " + ActorId + ": " + e.Message); Clear(); }
                    }
                }
            }
            bool on = !_view.ReplayDriven && SkinsOn(rig);
            for (int i = 0; i < _rends.Count; i++) { var r = _rends[i]; if (r != null && r.enabled != on) r.enabled = on; }
        }

        static bool SkinsOn(ActorRig rig) { foreach (var s in rig.Skins) if (s != null && s.enabled) return true; return false; }

        void Clear()
        {
            foreach (var go in _made) if (go != null) Destroy(go);
            foreach (var m in _meshes) if (m != null) Destroy(m);
            _made.Clear(); _rends.Clear(); _meshes.Clear(); _neckRing = null;
        }

        void OnDestroy() { Clear(); }

        // ================================================================== building
        void Build(ActorRig rig)
        {
            Clear();
            var rep = new StringBuilder($"[Gore] body marks {ActorId}:");
            uint seed = GoreWorld.Hash(ActorId + "|marks");
            int level = Mathf.Clamp(Settings.Gore, 0, 2);
            if ((_want.Ligature || _want.Manual) && rig.HasBone(HBone.Neck) && rig.HasBone(HBone.Head)) Neck(rig, seed, level, rep);
            if (_want.Nails) Nails(rig, seed, level, rep);
            LastReport = rep.ToString();
            Debug.Log(LastReport);
        }

        // ---- the neck: radius round it, measured from the body's own mesh as it lies
        void Neck(ActorRig rig, uint seed, int level, StringBuilder rep)
        {
            var neck = rig.Bone(HBone.Neck); var head = rig.Bone(HBone.Head);
            var ax = head.position - neck.position; float L = ax.magnitude; if (L < 0.02f) { rep.Append(" neck too short"); return; }
            ax /= L;
            float S = rig.Height > 0.5f ? rig.Height / 1.75f : 1f;
            var c = neck.position + ax * (L * (_want.Ligature ? 0.62f : 0.55f));
            // frame round the neck: e1 = toward the face, e2 = to its side
            var face = rig.EyeWorld() - rig.HeadCenterWorld(); var e1 = Vector3.ProjectOnPlane(face, ax);
            if (e1.sqrMagnitude < 1e-6f) e1 = Vector3.ProjectOnPlane(rig.transform.forward, ax);
            if (e1.sqrMagnitude < 1e-6f) e1 = Vector3.ProjectOnPlane(Vector3.forward, ax);
            e1.Normalize(); var e2 = Vector3.Cross(ax, e1).normalized;
            const int Seg = 32;
            var rad = NeckRadii(rig, c, ax, e1, e2, Seg, S, out int filled);
            rep.Append($" neck r̄={Avg(rad):0.000} ({filled}/{Seg} measured)");
            var root = new GameObject(_want.Ligature ? "GoreLigature" : "GoreThroatBruises"); root.transform.SetParent(neck, false);
            _made.Add(root); _neckRing = root.transform; _neckLocalCentre = neck.InverseTransformPoint(c);
            var b = new GoreAtlas.Batch();
            var cell = GoreAtlas.Cell(GoreAtlas.Pool);
            var solid = cell.center;
            var rnd = new System.Random((int)(seed & 0x7FFFFFFF));
            if (_want.Ligature)
            {
                float w = _want.LigWidth * S, feather = Mathf.Max(0.0025f, w * 0.55f);
                float alpha = level == 0 ? 0.7f : 0.9f;
                float ph = (float)rnd.NextDouble() * 6.283f;
                // rows across the band: outer feather, core, core, outer feather (soft, abraded edges)
                float[] off = { -w * 0.5f - feather, -w * 0.5f, w * 0.5f, w * 0.5f + feather };
                float[] rowA = { 0f, 1f, 1f, 0f };
                int b0 = b.V.Count;
                for (int s = 0; s <= Seg; s++)
                {
                    float th = s / (float)Seg * Mathf.PI * 2f; int k = s % Seg;
                    var dirR = e1 * Mathf.Cos(th) + e2 * Mathf.Sin(th);
                    float back = Mathf.Max(0f, -Mathf.Cos(th));                          // 1 at the back of the neck
                    float cross = Mathf.Pow(back, 6f);                                      // the crossing, right at the back
                    float h = 0.0025f * Mathf.Sin(th * 2f + ph) + 0.006f * S * back * back; // a little higher toward the back
                    float r = rad[k] + 0.0016f;
                    var col = Color.Lerp(Furrow, Crossing, cross);
                    for (int row = 0; row < 4; row++)
                    {
                        float o = off[row] * (1f + 0.45f * cross) + h;
                        var p = c + ax * o + dirR * (r + (row == 1 || row == 2 ? -0.0004f : 0f));
                        b.V.Add(neck.InverseTransformPoint(p)); b.N.Add(neck.InverseTransformDirection(dirR));
                        var tng = neck.InverseTransformDirection(Vector3.Cross(ax, dirR)); b.T.Add(new Vector4(tng.x, tng.y, tng.z, 1f));
                        b.UV.Add(solid); b.X.Add(new Vector4(0.12f, 0f, 0.25f, 0f));
                        var cc = col; cc.a = alpha * rowA[row] * (0.85f + 0.15f * Mathf.PerlinNoise(th * 3f + ph, row * 0.7f)); b.C.Add(GorePalette.Check(cc));
                    }
                }
                for (int s = 0; s < Seg; s++)
                    for (int row = 0; row < 3; row++)
                    {
                        int a0 = b0 + s * 4 + row, a1 = a0 + 1, n0 = a0 + 4, n1 = n0 + 1;
                        b.I.Add(a0); b.I.Add(n0); b.I.Add(a1); b.I.Add(a1); b.I.Add(n0); b.I.Add(n1);
                    }
                b.Quads += Seg * 3;
                rep.Append($" · ligature {_want.LigType} {w * 1000f:0} mm");
            }
            else
            {
                // bare hands: two thumbs at the front, fingertips round the sides and the back
                float alpha = level == 0 ? 0.42f : 0.62f;
                (float angDeg, float up, float size)[] marks =
                {
                    (18f, 0.35f, 0.016f), (-20f, 0.28f, 0.015f),
                    (78f, 0.1f, 0.010f), (98f, -0.15f, 0.009f), (118f, 0.05f, 0.009f),
                    (-80f, 0.12f, 0.010f), (-101f, -0.1f, 0.009f), (-122f, 0.02f, 0.009f),
                };
                foreach (var (angDeg, up, size) in marks)
                {
                    float th = (angDeg + (float)rnd.NextDouble() * 8f - 4f) * Mathf.Deg2Rad; int k = Mathf.RoundToInt(th / (Mathf.PI * 2f) * Seg); k = ((k % Seg) + Seg) % Seg;
                    var dirR = e1 * Mathf.Cos(th) + e2 * Mathf.Sin(th);
                    var p = c + ax * (up * L * 0.35f) + dirR * (rad[k] + 0.0015f);
                    var col = BruiseCol; col.a = alpha * (0.8f + 0.2f * (float)rnd.NextDouble());
                    b.Quad(neck.InverseTransformPoint(p), neck.InverseTransformDirection(dirR), neck.InverseTransformDirection(ax), size * S * 0.9f, size * S * 1.25f, GoreAtlas.PoolSatellite, col, new Vector3(0.1f, 0f, 0.2f), 0f);
                }
                rep.Append($" · throat bruises {marks.Length}");
            }
            Emit(root, b, "GoreNeckMarks_" + ActorId);
        }

        /// <summary>Distance from the neck's axis to its surface in each of `seg` directions round it, in a 3.6 cm slab at c:
        /// the lower quartile of the drawn skin's outward-facing vertices in that direction (gaps interpolated; a plain round
        /// neck if too few).</summary>
        static float[] NeckRadii(ActorRig rig, Vector3 c, Vector3 ax, Vector3 e1, Vector3 e2, int seg, float S, out int filled)
        {
            var res = new float[seg]; filled = 0;
            float fallback = 0.056f * S;
            SkinnedMeshRenderer top = null;
            foreach (var s in rig.Skins) if (s != null && s.sharedMesh != null && GoreMeshCut.IsTopLod(rig, s)) { top = s; break; }
            var buckets = new List<float>[seg];
            if (top != null)
            {
                var baked = new Mesh();
                try
                {
                    top.BakeMesh(baked, true);
                    var V = baked.vertices; var Nm = baked.normals; var xf = Matrix4x4.TRS(top.transform.position, top.transform.rotation, Vector3.one);
                    bool hasN = Nm != null && Nm.Length == V.Length;
                    for (int i = 0; i < V.Length; i++)
                    {
                        var d = xf.MultiplyPoint3x4(V[i]) - c; float h = Vector3.Dot(d, ax);
                        if (h < -0.018f || h > 0.018f) continue;
                        var r = d - ax * h; float rm = r.magnitude;
                        if (rm < 0.02f * S || rm > 0.13f * S) continue;
                        // only surfaces facing outward (not the inside of a collar)
                        if (hasN && Vector3.Dot(xf.MultiplyVector(Nm[i]), r / rm) < 0.2f) continue;
                        float th = Mathf.Atan2(Vector3.Dot(r, e2), Vector3.Dot(r, e1)); if (th < 0f) th += Mathf.PI * 2f;
                        int k = Mathf.Clamp(Mathf.FloorToInt(th / (Mathf.PI * 2f) * seg), 0, seg - 1);
                        (buckets[k] ?? (buckets[k] = new List<float>(16))).Add(rm);
                    }
                }
                finally { Object.Destroy(baked); }
            }
            var has = new bool[seg];
            for (int k = 0; k < seg; k++)
            {
                // the innermost outward-facing surface (the skin, where a collar stands off it): a low percentile, so the
                // furrow never floats between skin and collar — under a collar it is simply hidden, as it would be
                var l = buckets[k]; if (l == null || l.Count == 0) continue;
                l.Sort(); res[k] = l[Mathf.Clamp(l.Count / 4, 0, l.Count - 1)]; has[k] = true; filled++;
            }
            if (filled < seg / 3) { for (int k = 0; k < seg; k++) res[k] = fallback; filled = 0; return res; }
            for (int k = 0; k < seg; k++)
            {
                if (has[k]) continue;
                int a = k, bb = k; int da = 0, db = 0;
                while (!has[a]) { a = (a - 1 + seg) % seg; da++; }
                while (!has[bb]) { bb = (bb + 1) % seg; db++; }
                res[k] = Mathf.Lerp(res[a], res[bb], da / (float)(da + db));
            }
            // a little smoothing: the furrow follows the neck, not every fold of a collar
            var sm = new float[seg];
            for (int k = 0; k < seg; k++) sm[k] = res[(k - 1 + seg) % seg] * 0.25f + res[k] * 0.5f + res[(k + 1) % seg] * 0.25f;
            return sm;
        }

        static float Avg(float[] a) { float s = 0f; foreach (var x in a) s += x; return a.Length > 0 ? s / a.Length : 0f; }

        // ---- the nails
        void Nails(ActorRig rig, uint seed, int level, StringBuilder rep)
        {
            var S = GoreWorld.S; var gone = new HashSet<SeverPart>();
            if (S != null) foreach (var it in Gore.PiecesOf(S, ActorId)) if (Gore.TryPart(it, out var p)) gone.Add(p);
            var rnd = new System.Random((int)((seed * 2654435761u) & 0x7FFFFFFF));
            int n = 0, torn = 0; float alpha = level == 0 ? 0.7f : 0.95f;
            for (int side = 0; side < 2; side++)
            {
                bool L = side == 0;
                if (gone.Contains(L ? SeverPart.HandL : SeverPart.HandR) || gone.Contains(L ? SeverPart.ArmL : SeverPart.ArmR)) continue;
                for (int d = 0; d < 5; d++)
                {
                    var hb3 = (HBone)((int)(L ? HBone.ThumbL1 : HBone.ThumbR1) + d * 3 + 2); var hb2 = (HBone)((int)hb3 - 1);
                    if (!rig.HasBone(hb3)) continue;
                    var b3 = rig.Bone(hb3); var tip = Tip(rig, hb3, hb2, side * 5 + d);
                    var along = tip - b3.position; if (along.sqrMagnitude < 1e-8f) along = b3.forward * 0.01f;
                    var root = new GameObject("GoreNail_" + hb3); root.transform.SetParent(b3, false); _made.Add(root);
                    var bt = new GoreAtlas.Batch();
                    bool isTorn = rnd.NextDouble() < (torn == 0 ? 0.22 : 0.08); if (isTorn) torn++;
                    var col = isTorn ? NailTorn : NailDirt; col.a = alpha;
                    float s = isTorn ? 0.0042f : 0.0032f;
                    var pl = b3.InverseTransformPoint(tip - along.normalized * 0.002f); var al = b3.InverseTransformDirection(along.normalized);
                    // three crossed flecks round the free edge of the nail: dark from whichever side the lens sees it
                    var perp = Vector3.Cross(al, Mathf.Abs(Vector3.Dot(al, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up).normalized;
                    for (int q = 0; q < 3; q++)
                    {
                        var nrm = Quaternion.AngleAxis(q * 60f, al) * perp;
                        bt.Quad(pl, nrm, al, s, s * 0.8f, GoreAtlas.PoolSatellite, col, new Vector3(0.45f, 0f, 0.2f), 0f);
                    }
                    Emit(root, bt, "GoreNail_" + ActorId + "_" + hb3);
                    n++;
                }
            }
            rep.Append($" · nails {n} ({torn} torn)");
        }

        /// <summary>The fingertip now: the rest tip carried along with the distal bone (or its segment extended).</summary>
        static Vector3 Tip(ActorRig rig, HBone b3, HBone b2, int tipIndex)
        {
            var t3 = rig.Bone(b3);
            if (rig.FingerTipRest != null && tipIndex < rig.FingerTipRest.Length && rig.RestBoneToRoot != null && (int)b3 < rig.RestBoneToRoot.Length && rig.FingerTipRest[tipIndex].sqrMagnitude > 1e-8f)
            {
                var local = rig.RestBoneToRoot[(int)b3].inverse.MultiplyPoint3x4(rig.FingerTipRest[tipIndex]);
                if (local.sqrMagnitude < 0.05f * 0.05f) return t3.TransformPoint(local);
            }
            var t2 = rig.HasBone(b2) ? rig.Bone(b2) : null;
            var seg = t2 != null ? t3.position - t2.position : t3.forward * 0.02f;
            return t3.position + seg * 0.85f;
        }

        void Emit(GameObject root, GoreAtlas.Batch b, string name)
        {
            if (b.Empty || GoreAtlas.Mat == null) return;
            var mesh = b.ToMesh(name); mesh.RecalculateBounds();
            _meshes.Add(mesh);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = root.AddComponent<MeshRenderer>(); mr.sharedMaterial = GoreAtlas.Mat;
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.lightProbeUsage = LightProbeUsage.BlendProbes; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _rends.Add(mr);
        }
    }
}
