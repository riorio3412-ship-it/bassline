using System.Collections;
using System.Collections.Generic;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// One blood trace decal (made by <see cref="GoreDecals"/>). In Start it reads the TraceView id WorldPresenter adds right
    /// after creation; a BloodPool whose victim has a kernel Pool mark then slides (≤ 0.6 m) toward the floor under the
    /// wounded body part so the body lies inside its pool, takes its radius from the mark's Power (0.45–1.1 m) and throws
    /// 2–4 satellites at the rim. Colour follows the stain's age (wet under 20 min, the rim browns from 20 to 120 min); it is
    /// re-evaluated only when the age crosses a 10-minute step (no per-frame animation).
    /// </summary>
    public sealed class GoreDecalView : MonoBehaviour
    {
        public string Type, TraceId, Victim; public float Size = 0.5f, Alpha = 1f; public uint Seed; public bool Vertical;
        public int Room = -1;
        Vector2 _off; float _radius = -1f; double _clock = -1; int _ageStep = -1; float _nextAge;
        bool _anchored;

        /// <summary>World centre / radius of the pool after anchoring (for GoreScene hotspots and the film).</summary>
        public Vector3 CentreWorld => transform.TransformPoint(new Vector3(_off.x, 0, _off.y));
        public float Radius => _radius > 0 ? _radius : Size * 0.5f;
        public bool Anchored => _anchored;

        static readonly List<GoreDecalView> _pools = new List<GoreDecalView>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _pools.Clear(); }
        /// <summary>Live BloodPool decals (for hotspots).</summary>
        public static IReadOnlyList<GoreDecalView> Pools { get { _pools.RemoveAll(p => p == null); return _pools; } }

        void OnEnable() { if (Type == "BloodPool" && !_pools.Contains(this)) _pools.Add(this); }
        void OnDisable() { _pools.Remove(this); }

        IEnumerator Start()
        {
            if (Type == "BloodPool" && !_pools.Contains(this)) _pools.Add(this);
            var tv = GetComponent<TraceView>();
            if (tv == null) { yield return null; tv = GetComponent<TraceView>(); }
            TraceId = tv != null ? tv.TraceId : null;
            var S = GoreWorld.S; Trace tr = null;
            if (S != null && TraceId != null) foreach (var t in S.Traces) if (t.Id == TraceId) { tr = t; break; }
            if (tr != null) { Victim = tr.Victim; _clock = tr.Clock; Room = tr.Room; }
            // prints and drag smears follow the way the person was walking (the trace's bearing), toe / drag along +v
            if (tr != null && tr.Dir != 0f && !Vertical && (Type == "FootprintBlood" || Type == "BloodSmear"))
            {
                var n = transform.up; var fwd = Vector3.ProjectOnPlane(GoreWorld.Dir(tr.Dir, 0f), n);
                // a footprint's toe is +v; a smear fades along +u (its v then lies across the drag)
                if (fwd.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(Type == "BloodSmear" ? Vector3.Cross(fwd.normalized, n) : fwd.normalized, n);
            }
            Rebuild();
            if (Type != "BloodPool" || Victim == null) yield break;
            // wait for the body to settle into its dead pose before measuring where the wound lies
            float until = Time.unscaledTime + 3f;
            while (Time.unscaledTime < until)
            {
                var v = GoreWorld.View(Victim);
                if (v != null && v.Rig != null && v.Rig.Anim != null && v.Rig.Anim.IsDead) { yield return new WaitForSecondsRealtime(1.2f); break; }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (this == null) yield break;
            Anchor();
            // the dead now fall as ragdolls and can still be sliding when the pool first anchors: look once more when the
            // body has certainly come to rest, and follow it if the wound moved more than a hand's width
            var at = WoundBonePos();
            yield return new WaitForSecondsRealtime(3f);
            if (this == null || !at.HasValue) yield break;
            var now = WoundBonePos();
            if (now.HasValue && (now.Value - at.Value).sqrMagnitude > 0.15f * 0.15f) Anchor();
        }

        /// <summary>World position of the bone the pool's wound is on (null without a rig or a Pool mark).</summary>
        Vector3? WoundBonePos()
        {
            var a = GoreWorld.S?.A(Victim); if (a == null) return null;
            GoreMark pool = null; foreach (var m in Gore.MarksOf(a)) if (m.Kind == GoreKind.Pool) pool = m;
            var rig = GoreWorld.View(Victim)?.Rig; if (pool == null || rig == null) return null;
            var bone = rig.Bone(rig.RegionBone(GoreWorld.Region(pool.Region)));
            return bone != null ? bone.position : (Vector3?)null;
        }

        void Update()
        {
            if (Time.unscaledTime < _nextAge) return;
            _nextAge = Time.unscaledTime + 5f;
            int step = Mathf.FloorToInt(GoreWorld.AgeMin(_clock) / 10f);
            if (step != _ageStep && _clock > 0) Rebuild();
        }

        void Anchor()
        {
            var S = GoreWorld.S; var a = S?.A(Victim); if (a == null) return;
            GoreMark pool = null;
            foreach (var m in Gore.MarksOf(a)) if (m.Kind == GoreKind.Pool) pool = m;
            if (pool == null) { _anchored = true; return; }
            var v = GoreWorld.View(Victim);
            var rig = v != null ? v.Rig : null;
            if (rig != null)
            {
                var bone = rig.Bone(rig.RegionBone(GoreWorld.Region(pool.Region)));
                if (bone != null)
                {
                    var lp = transform.InverseTransformPoint(bone.position);
                    var off = new Vector2(lp.x, lp.z);
                    if (off.magnitude > 0.6f) off = off.normalized * 0.6f;
                    _off = off;
                }
            }
            _radius = Mathf.Lerp(0.45f, 1.1f, Mathf.Clamp01(pool.Power));
            _anchored = true;
            Rebuild();
            GoreScene.NotifyPool(this);
        }

        /// <summary>Rebuild the mesh for the current age.</summary>
        public void Rebuild()
        {
            float age = GoreWorld.AgeMin(_clock); _ageStep = Mathf.FloorToInt(age / 10f);
            if (Type == "BloodPool") Fit();
            Build(age, _off, _radius);
            if (Type == "BloodPool" && _anchored) { try { Seep(age); } catch (System.Exception e) { Debug.LogWarning("[Gore] seep: " + e.Message); } }
        }

        // ------------------------------------------------------------------ under the door
        GameObject _seep; Mesh _seepMesh;
        /// <summary>The first thing anyone sees from the corridor: where the pool's rim reaches a doorway, a dark tongue of blood
        /// has crept out under the door onto the other side (as wide as the stretch of rim that met the threshold, 20–45 cm
        /// out, heaviest at the door). Its own object beside the pool, so the corridor shows it even while this room is culled.</summary>
        void Seep(float age)
        {
            if (_seep != null) { Destroy(_seep); _seep = null; }
            if (_seepMesh != null) { Destroy(_seepMesh); _seepMesh = null; }
            var S = GoreWorld.S; if (S?.Layout == null || Room < 0 || Vertical) return;
            var room = S.Layout.Room(Room); if (room == null) return;
            var c = CentreWorld; float R = Radius * 1.18f * 0.87f;   // how far the drawn rim reaches
            var b = GoreDecals.Scratch; b.Clear();
            var rnd = new System.Random((int)((Seed ^ 0x5EE9u) & 0x7FFFFFFF));
            foreach (var di in room.Doors)
            {
                if (di < 0 || di >= S.Layout.Doors.Count) continue;
                var d = S.Layout.Doors[di]; var dp = GoreWorld.ToWorld(d.Pos);
                var along = d.AlongX ? Vector3.right : Vector3.forward; var nrm = d.AlongX ? Vector3.forward : Vector3.right;
                float a = Mathf.Clamp(Vector3.Dot(c - dp, along), -d.Width * 0.5f, d.Width * 0.5f);
                var closest = dp + along * a; float dist = new Vector2(c.x - closest.x, c.z - closest.z).magnitude;
                float reach = R + 0.12f; if (dist > reach) continue;
                var outward = Vector3.Dot(c - dp, nrm) >= 0f ? -nrm : nrm;                        // away from the pool, into the next room
                float width = Mathf.Clamp(2f * Mathf.Sqrt(Mathf.Max(0.0004f, reach * reach - dist * dist)), 0.18f, d.Width * 0.8f);
                float len = Mathf.Lerp(0.2f, 0.45f, Mathf.Clamp01((reach - dist) / 0.5f)) * (0.85f + 0.3f * (float)rnd.NextDouble());
                var at = closest + outward * (len * 0.5f + 0.02f); at.y = dp.y;
                var v = Vector3.Cross(outward, Vector3.up);                                           // u = outward: the smear is heavy at −u, the door
                var col = GorePalette.Aged(age, 0.35f, 0.96f);
                b.Quad(transform.InverseTransformPoint(at), transform.InverseTransformDirection(Vector3.up), transform.InverseTransformDirection(v), len * 0.5f + 0.03f, width * 0.5f, GoreAtlas.Smear, col, new Vector3(GorePalette.Gloss(age, 0.3f), 0f, 1.6f), 0.0006f);
            }
            if (b.Empty) return;
            _seepMesh = b.ToMesh("Gore_Seep"); _seepMesh.RecalculateBounds();
            _seep = new GameObject("GoreSeep");
            _seep.transform.SetParent(transform.parent, false); _seep.transform.SetPositionAndRotation(transform.position, transform.rotation); _seep.transform.localScale = transform.localScale;
            _seep.AddComponent<MeshFilter>().sharedMesh = _seepMesh;
            var mr = _seep.AddComponent<MeshRenderer>(); mr.sharedMaterial = GoreAtlas.Mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
        }

        /// <summary>A pool never runs under a wall into the next room: the room is a rectangle (wall faces 0.1 m in from its
        /// edges), so the stain slides inward (≤ 0.35 m) and then shrinks until its outline (≈ 0.87 of the quad half-size)
        /// fits.</summary>
        void Fit()
        {
            var S = GoreWorld.S; var room = S?.Layout?.Room(Room >= 0 ? Room : (GoreWorld.M != null ? GoreWorld.M.RoomAtWorld(transform.position + Vector3.up * 0.05f) : -1));
            if (room == null || Vertical) return;
            var R = room.Rect; float x0 = R.x0 + 0.1f, x1 = R.x1 - 0.1f, z0 = R.z0 + 0.1f, z1 = R.z1 - 0.1f;
            if (x1 - x0 < 0.4f || z1 - z0 < 0.4f) return;
            float half = (_radius > 0 ? _radius : Size * 0.55f) * 1.18f;
            float reach = half * 0.87f;
            var c = transform.TransformPoint(new Vector3(_off.x, 0, _off.y));
            // slide inward
            float sx = 0, sz = 0;
            if (c.x - reach < x0) sx = x0 - (c.x - reach); else if (c.x + reach > x1) sx = x1 - (c.x + reach);
            if (c.z - reach < z0) sz = z0 - (c.z - reach); else if (c.z + reach > z1) sz = z1 - (c.z + reach);
            var shift = new Vector3(Mathf.Clamp(sx, -0.35f, 0.35f), 0, Mathf.Clamp(sz, -0.35f, 0.35f));
            if (shift.sqrMagnitude > 1e-6f)
            {
                var lc = transform.InverseTransformPoint(c + shift); _off = new Vector2(lc.x, lc.z); c += shift;
            }
            // then shrink to what is left
            float room2 = Mathf.Min(Mathf.Min(c.x - x0, x1 - c.x), Mathf.Min(c.z - z0, z1 - c.z));
            if (room2 < reach)
            {
                float fitHalf = Mathf.Max(0.18f, room2 / 0.87f);
                _radius = fitHalf / 1.18f;
            }
        }

        internal void Build(float age, Vector2 off, float radius)
        {
            var mf = GetComponent<MeshFilter>(); if (mf == null) return;
            var b = GoreDecals.Scratch; b.Clear();
            var rnd = new System.Random((int)(Seed & 0x7FFFFFFF));
            var c0 = new Vector3(off.x, 0, off.y);
            switch (Type)
            {
                case "BloodPool":
                    {
                        float R = (radius > 0 ? radius : Size * 0.55f) * 1.18f;
                        Grid(b, c0, R, GoreAtlas.Pool, age, Alpha, 2.2f, false);
                        int sats = radius > 0 ? 2 + rnd.Next(3) : 1;
                        for (int i = 0; i < sats; i++)
                        {
                            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, d = R * (0.78f + 0.2f * (float)rnd.NextDouble());
                            float s = Mathf.Lerp(0.05f, 0.09f, (float)rnd.NextDouble()) * Mathf.Max(0.7f, R);
                            var p = c0 + new Vector3(Mathf.Cos(ang) * d, 0.0005f, Mathf.Sin(ang) * d);
                            b.Quad(p, Vector3.up, new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)), s, s, GoreAtlas.PoolSatellite, GorePalette.Aged(age, 0.6f, Alpha), new Vector3(GorePalette.Gloss(age, 0.4f), 0, 1.8f), 0.0005f);
                        }
                        break;
                    }
                case "DrainBlood":
                    {
                        float R = Mathf.Max(0.35f, Size * 0.9f);
                        var wc = GorePalette.Watery;
                        GridColor(b, new Vector3(-R * 0.72f, 0, 0), R, GoreAtlas.DrainRivulet, wc, wc, 0.95f, 0.8f, 0.6f);
                        b.Quad(new Vector3(0, 0.0004f, 0), Vector3.up, Vector3.forward, 0.14f, 0.14f, GoreAtlas.Pool, new Color(wc.r, wc.g, wc.b, 0.28f * Alpha), new Vector3(0.95f, 0, 0.5f), 0.0004f);
                        break;
                    }
                case "BloodSmear":
                    {
                        float R = Mathf.Max(0.15f, Size * 0.6f);
                        var inner = GorePalette.Thin(age, 0.92f * Alpha); var rim = GorePalette.Aged(age + 40f, 1f, 0.8f * Alpha);
                        GridColor(b, Vector3.zero, R, GoreAtlas.Smear, inner, rim, Mathf.Lerp(0.5f, 0.25f, Mathf.Clamp01(age / 60f)), 0.3f, 1f);
                        break;
                    }
                case "BloodDrip":
                    {
                        if (Vertical) { float L = Mathf.Clamp(Size * 1.6f, 0.15f, 0.45f); b.Quad(new Vector3(0, 0, -L * 0.36f), Vector3.up, Vector3.forward, L * 0.5f, L * 0.5f, GoreAtlas.DripRun, GorePalette.Thin(age, Alpha), new Vector3(Mathf.Lerp(0.8f, 0.35f, Mathf.Clamp01(age / 60f)), 0, 1.6f), 0.0005f); }
                        else { float s = Mathf.Clamp(Size * 0.6f, 0.07f, 0.2f); b.Quad(Vector3.zero, Vector3.up, Vector3.forward, s, s, GoreAtlas.Droplets, GorePalette.Thin(age, Alpha), new Vector3(Mathf.Lerp(0.85f, 0.35f, Mathf.Clamp01(age / 60f)), 0, 1.8f), 0.0005f); }
                        break;
                    }
                case "FootprintBlood":
                    {
                        float s = Mathf.Clamp(Size * 0.78f, 0.17f, 0.24f);
                        b.Quad(Vector3.zero, Vector3.up, Vector3.forward, s, s, GoreAtlas.Footprint, GorePalette.Thin(age + 15f, 0.9f * Alpha), new Vector3(0.3f, 0, 0.6f), 0.0005f);
                        break;
                    }
                case "Handprint":
                    {
                        float s = 0.17f;
                        b.Quad(Vector3.zero, Vector3.up, Vector3.forward, s, s, GoreAtlas.Handprint, GorePalette.Thin(age, 0.95f * Alpha), new Vector3(Mathf.Lerp(0.55f, 0.25f, Mathf.Clamp01(age / 60f)), 0, 0.8f), 0.0005f);
                        break;
                    }
            }
            if (b.Empty) return;
            var old = mf.sharedMesh;
            var mesh = b.ToMesh("Gore_" + Type);
            mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + new Vector3(0.02f, 0.02f, 0.02f));
            mf.sharedMesh = mesh;
            if (old != null && old != mesh) Destroy(old);
        }

        void OnDestroy()
        {
            var mf = GetComponent<MeshFilter>(); if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
            if (_seep != null) Destroy(_seep); if (_seepMesh != null) Destroy(_seepMesh);
        }

        /// <summary>5×5 grid over a square of half-size R (local XZ), wet centre → aged rim.</summary>
        static void Grid(GoreAtlas.Batch b, Vector3 c, float R, int cell, float age, float alpha, float bump, bool thin)
        {
            var r = GoreAtlas.Cell(cell); int b0 = b.V.Count;
            for (int j = 0; j <= 4; j++)
                for (int i = 0; i <= 4; i++)
                {
                    float fu = i / 4f, fv = j / 4f;
                    var p = c + new Vector3((fu - 0.5f) * 2f * R, 0.0003f, (fv - 0.5f) * 2f * R);
                    float rim = Mathf.Clamp01(new Vector2(fu - 0.5f, fv - 0.5f).magnitude * 2.3f);
                    b.V.Add(p); b.N.Add(Vector3.up); b.T.Add(new Vector4(1, 0, 0, -1));
                    b.UV.Add(new Vector2(Mathf.Lerp(r.xMin, r.xMax, fu), Mathf.Lerp(r.yMin, r.yMax, fv)));
                    b.X.Add(new Vector4(GorePalette.Gloss(age, rim), 0, bump, 0));
                    b.C.Add(GorePalette.Aged(age, rim, alpha));
                }
            GridTris(b, b0);
        }

        static void GridColor(GoreAtlas.Batch b, Vector3 c, float R, int cell, Color inner, Color rimCol, float glossIn, float glossRim, float bump)
        {
            var r = GoreAtlas.Cell(cell); int b0 = b.V.Count;
            for (int j = 0; j <= 4; j++)
                for (int i = 0; i <= 4; i++)
                {
                    float fu = i / 4f, fv = j / 4f;
                    var p = c + new Vector3((fu - 0.5f) * 2f * R, 0.0003f, (fv - 0.5f) * 2f * R);
                    float rim = Mathf.Clamp01(new Vector2(fu - 0.5f, fv - 0.5f).magnitude * 2.3f);
                    b.V.Add(p); b.N.Add(Vector3.up); b.T.Add(new Vector4(1, 0, 0, -1));
                    b.UV.Add(new Vector2(Mathf.Lerp(r.xMin, r.xMax, fu), Mathf.Lerp(r.yMin, r.yMax, fv)));
                    b.X.Add(new Vector4(Mathf.Lerp(glossIn, glossRim, rim), 0, bump, 0));
                    b.C.Add(GorePalette.Check(Color.Lerp(inner, rimCol, rim)));
                }
            GridTris(b, b0);
        }

        static void GridTris(GoreAtlas.Batch b, int b0)
        {
            for (int j = 0; j < 4; j++)
                for (int i = 0; i < 4; i++)
                {
                    int a = b0 + j * 5 + i, bb = a + 5, cc = a + 6, d = a + 1;
                    b.I.Add(a); b.I.Add(bb); b.I.Add(cc); b.I.Add(a); b.I.Add(cc); b.I.Add(d);
                }
            b.Quads += 16;
        }
    }
}
