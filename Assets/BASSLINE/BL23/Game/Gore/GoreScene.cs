using System;
using System.Collections.Generic;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game
{
    /// <summary>A place worth looking at in a death scene (for the discovery film's shot planner and probes).</summary>
    public struct GoreHotspot
    {
        public GoreKind Kind; public Vector3 Pos, Normal; public float Size, Weight; public int Room; public string Label;
    }

    /// <summary>
    /// Draws the kernel's gore marks (Body.GoreMarks): spatter traced from the wound along the blow (Impact: a 35° cone,
    /// radial for blunt blows; Arterial: a 40° sweep with the pulse pattern where it met the wall; CastOff: the vertical
    /// arc behind the swing, up to the ceiling), smears down walls, handprints on walls and door frames, nail scratches,
    /// saw kerfs and ooze; toppled furniture through <see cref="GoreProps"/>. Every ray is a real Physics.Raycast that only
    /// stops on mansion surfaces in the mark's room; drops elongate with the angle they strike at (1/sin, 1–5×) and heavy
    /// wall drops run. One victim's quads in one room are merged into one mesh (one draw); renderers follow the mansion's
    /// room culling unless the film asks for everything. Rebuilt only when a body's GoreRev changes (deterministic from
    /// each mark's seed); idle frames allocate nothing.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public sealed class GoreScene : MonoBehaviour
    {
        public static GoreScene I { get; private set; }
        /// <summary>Atlas loaded and the first pass over the live session done.</summary>
        public static bool Ready => I != null && I._builtOnce && GoreAtlas.Loaded;
        /// <summary>Fires once per run, the first time any gore mark is seen (the film warms its clips on it).</summary>
        public static Action OnFirstGore;
        static bool _firstFired;

        public const int MaxQuadsPerVictim = 120, MaxRaysPerRebuild = 200;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; OnFirstGore = null; _firstFired = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("GoreScene");
            DontDestroyOnLoad(go);
            I = go.AddComponent<GoreScene>();
            go.AddComponent<GorePieces>();
            go.AddComponent<GoreForensics>();   // ligature furrows, throat bruises, nails (GoreForensics.cs)
            go.AddComponent<GoreItemBlood>();   // blood on a weapon's blade (GoreItems.cs)
        }

        sealed class Rend { public MeshRenderer R; public int Room; }
        sealed class VictimVis
        {
            public string Id; public int Rev = int.MinValue, Count = -1; public Transform Root; public bool Hidden;
            public readonly List<Rend> Rends = new List<Rend>();
            public readonly List<GoreHotspot> Hot = new List<GoreHotspot>();
        }
        readonly Dictionary<string, VictimVis> _v = new Dictionary<string, VictimVis>();
        GameState _S; int _loop = -1; Transform _worldRoot; MansionView _mansion;
        float _next; bool _film, _builtOnce; int _level = -1;

        void Awake() { if (I == null) I = this; }
        void OnDestroy() { if (I == this) I = null; }

        /// <summary>Show every gore visual regardless of room culling (the film cuts to lenses in rooms the mansion may have
        /// culled) and pause furniture re-checks.</summary>
        public void FilmMode(bool on) { if (_film == on) return; _film = on; UpdateVisibility(); }
        public bool Film => _film;

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.25f;
            var S = GoreWorld.S; var W = GoreWorld.W;
            if (S == null || W == null || W.Root == null || S.Actors == null) { if (_v.Count > 0) ClearAll(); return; }
            int level = Mathf.Clamp(Settings.Gore, 0, 2);
            if (level != _level) { _level = level; foreach (var x in _v.Values) x.Rev = int.MinValue; }   // 완화 ↔ 강함: redraw
            if (S != _S || S.Loop != _loop || W.Root != _worldRoot || W.Mansion != _mansion)
            {
                ClearAll(); GoreProps.ResetAll();
                _S = S; _loop = S.Loop; _worldRoot = W.Root; _mansion = W.Mansion;
            }
            try
            {
                foreach (var a in S.Actors.Values)
                {
                    var body = a.Body; int n = body?.GoreMarks?.Count ?? 0; int rev = body != null ? body.GoreRev : 0;
                    _v.TryGetValue(a.Id, out var vv);
                    if (vv == null) { if (n == 0) continue; vv = new VictimVis { Id = a.Id }; _v[a.Id] = vv; }
                    if (vv.Rev != rev || vv.Count != n) Rebuild(vv, a);
                    var view = W.ViewOf(a.Id); bool hide = view != null && view.ReplayDriven;
                    if (hide != vv.Hidden) vv.Hidden = hide;
                }
                if (!_film) GoreProps.Sync(S, W.Mansion);
            }
            catch (Exception e) { Debug.LogException(e); }
            UpdateVisibility();
            _builtOnce = true;
        }

        void UpdateVisibility()
        {
            foreach (var vv in _v.Values)
                for (int i = 0; i < vv.Rends.Count; i++)
                {
                    var r = vv.Rends[i]; if (r.R == null) continue;
                    bool on = !vv.Hidden && (_film || GoreWorld.RoomVisible(r.Room));
                    if (r.R.enabled != on) r.R.enabled = on;
                }
        }

        void ClearAll()
        {
            foreach (var vv in _v.Values) DestroyVisuals(vv);
            _v.Clear();
        }

        static void DestroyVisuals(VictimVis vv)
        {
            foreach (var r in vv.Rends) if (r.R != null) { var mf = r.R.GetComponent<MeshFilter>(); if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh); }
            vv.Rends.Clear();
            if (vv.Root != null) Destroy(vv.Root.gameObject);
            vv.Root = null;
        }

        /// <summary>Hotspots of one victim's scene: pool centre, wall spray centroid, cast-off midpoint, toppled furniture,
        /// handprints, nail scratches, saw kerfs (heaviest first).</summary>
        public IReadOnlyList<GoreHotspot> Hotspots(string victimId)
        {
            var list = new List<GoreHotspot>();
            if (victimId == null) return list;
            if (_v.TryGetValue(victimId, out var vv)) list.AddRange(vv.Hot);
            foreach (var p in GoreDecalView.Pools)
                if (p != null && p.Victim == victimId)
                    list.Add(new GoreHotspot { Kind = GoreKind.Pool, Pos = p.CentreWorld, Normal = p.transform.up, Size = p.Radius, Weight = 8f, Room = p.Room, Label = "시신 아래 고인 피" });
            var a = GoreWorld.S?.A(victimId);
            if (a?.Body?.GoreMarks != null)
                foreach (var m in a.Body.GoreMarks)
                    if (m.Kind == GoreKind.Topple && !m.Righted && GoreProps.Centre(m.Furniture, out var c))
                        list.Add(new GoreHotspot { Kind = GoreKind.Topple, Pos = c, Normal = Vector3.up, Size = 0.5f, Weight = 7f, Room = m.Room, Label = "넘어진 가구" });
            list.Sort((x, y) => y.Weight.CompareTo(x.Weight));
            return list;
        }

        /// <summary>A BloodPool decal finished anchoring (hook for listeners; hotspots read the decal directly).</summary>
        internal static void NotifyPool(GoreDecalView d) { }

        // ================================================================== building
        readonly Dictionary<int, GoreAtlas.Batch> _byRoom = new Dictionary<int, GoreAtlas.Batch>();
        readonly List<GoreAtlas.Batch> _pool = new List<GoreAtlas.Batch>();
        readonly List<(Vector3 p, Vector3 n)> _wall = new List<(Vector3, Vector3)>(64);
        readonly List<(Vector3 p, Vector3 n)> _markWall = new List<(Vector3, Vector3)>(32);
        readonly List<(Vector3 p, Vector3 n, Vector3 d)> _arc = new List<(Vector3, Vector3, Vector3)>(16);
        int _budget, _quads, _impacts; VictimVis _cur; GameState _curS;

        static readonly GoreKind[] Order = { GoreKind.Arterial, GoreKind.Handprint, GoreKind.Smear, GoreKind.CastOff, GoreKind.Impact, GoreKind.NailScratch, GoreKind.SawMark, GoreKind.Ooze };

        GoreAtlas.Batch Batch(int room)
        {
            if (_byRoom.TryGetValue(room, out var b)) return b;
            if (_pool.Count > 0) { b = _pool[_pool.Count - 1]; _pool.RemoveAt(_pool.Count - 1); } else b = new GoreAtlas.Batch();
            b.Clear(); _byRoom[room] = b; return b;
        }

        void Rebuild(VictimVis vv, Actor a)
        {
            var body = a.Body;
            vv.Rev = body != null ? body.GoreRev : 0; vv.Count = body?.GoreMarks?.Count ?? 0;
            DestroyVisuals(vv); vv.Hot.Clear();
            var marks = body?.GoreMarks; if (marks == null || marks.Count == 0) return;
            if (!_firstFired) { _firstFired = true; try { OnFirstGore?.Invoke(); } catch (Exception e) { Debug.LogException(e); } }
            var W = GoreWorld.W; if (W == null || W.Root == null) return;
            var root = new GameObject("Gore_" + a.Id).transform; root.SetParent(W.Root, false); root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            vv.Root = root;
            _budget = MaxRaysPerRebuild; _quads = 0; _impacts = 0; _cur = vv; _curS = GoreWorld.S; _wall.Clear();
            foreach (var b in _byRoom.Values) _pool.Add(b);
            _byRoom.Clear();
            foreach (var kind in Order)
                for (int i = 0; i < marks.Count; i++)
                {
                    var m = marks[i]; if (m.Kind != kind) continue;
                    if (_quads >= MaxQuadsPerVictim || _budget <= 0) break;
                    try { Draw(m); } catch (Exception e) { Debug.LogWarning("[Gore] mark " + m.Kind + ": " + e.Message); }
                }
            // wall spray centroid hotspot
            if (WallGroup(_wall, out var wc, out var wn, out var ext, out int cnt) && cnt >= 2)
                vv.Hot.Add(new GoreHotspot { Kind = GoreKind.Impact, Pos = wc, Normal = wn, Size = Mathf.Max(0.3f, ext), Weight = 8f, Room = RoomOf(marks[0]), Label = "벽에 튄 핏자국" });
            foreach (var kv in _byRoom)
            {
                var b = kv.Value; if (b.Empty) continue;
                var go = new GameObject("GoreMarks_r" + kv.Key); go.transform.SetParent(root, false);
                var mesh = b.ToMesh("GoreMarks_" + a.Id + "_" + kv.Key);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = GoreAtlas.Mat;
                mr.shadowCastingMode = ShadowCastingMode.Off; mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                vv.Rends.Add(new Rend { R = mr, Room = kv.Key });
            }
            _cur = null;
        }

        int RoomOf(GoreMark m)
        {
            if (m.Room >= 0) return m.Room;
            var S = _curS; return S?.Layout != null ? S.Layout.RoomAt(m.Pos) : -1;
        }

        void Draw(GoreMark m)
        {
            switch (m.Kind)
            {
                case GoreKind.Impact:
                    {
                        _impacts++;
                        int rays = Mathf.Max(4, 12 - 2 * (_impacts - 1));
                        Spray(m, rays, false); break;
                    }
                case GoreKind.Arterial: Spray(m, 20, true); break;
                case GoreKind.CastOff: CastOff(m); break;
                case GoreKind.Smear: Smear(m); break;
                case GoreKind.Handprint: Hand(m); break;
                case GoreKind.NailScratch: Scratch(m); break;
                case GoreKind.SawMark: Kerf(m); break;
                case GoreKind.Ooze: Ooze(m); break;
            }
        }

        static float R01(System.Random r) => (float)r.NextDouble();

        /// <summary>Scale (≤ 1) that keeps a floor stain of the given reach inside its room (walls stand 0.1 m in from the
        /// room rectangle): a stain never runs under a wall into the next room.</summary>
        float FloorFit(Vector3 c, int room, float reach)
        {
            var r = _curS?.Layout?.Room(room); if (r == null || reach <= 0.01f) return 1f;
            var R = r.Rect; float d = Mathf.Min(Mathf.Min(c.x - (R.x0 + 0.1f), (R.x1 - 0.1f) - c.x), Mathf.Min(c.z - (R.z0 + 0.1f), (R.z1 - 0.1f) - c.z));
            if (d >= reach) return 1f;
            return Mathf.Clamp(d / reach, 0.25f, 1f);
        }
        static float Rr(System.Random r, float a, float b) => a + (b - a) * (float)r.NextDouble();

        Vector3 Origin(GoreMark m) => GoreWorld.ToWorld(m.Pos) + Vector3.up * Mathf.Max(0.05f, m.H);

        // ------------------------------------------------------------------ spatter
        void Spray(GoreMark m, int rays, bool arterial)
        {
            var rnd = new System.Random((int)(m.Seed & 0x7FFFFFFF));
            int room = RoomOf(m); var o = Origin(m); float age = GoreWorld.AgeMin(m.Clock);
            bool radial = !arterial && (m.Dmg == DamageType.Blunt || m.Dmg == DamageType.Crush);
            var axis = GoreWorld.Dir(m.Dir, m.Pitch);
            var perp = Vector3.Cross(axis, Vector3.up); if (perp.sqrMagnitude < 1e-4f) perp = Vector3.right; perp.Normalize();
            float power = Mathf.Clamp01(m.Power);
            _markWall.Clear();
            for (int i = 0; i < rays; i++)
            {
                if (_quads >= MaxQuadsPerVictim || _budget <= 0) break;
                Vector3 d;
                if (arterial)
                {
                    float t = rays > 1 ? i / (rays - 1f) : 0.5f;
                    d = GoreWorld.Dir(m.Dir - 20f + 40f * t + Rr(rnd, -2f, 2f), m.Pitch * (1f - 0.45f * t) + Rr(rnd, -4f, 4f));
                }
                else if (radial) d = GoreWorld.Dir(Rr(rnd, 0f, 360f), Rr(rnd, -20f, 50f));
                else
                {
                    float ang = 30f * Mathf.Sqrt(R01(rnd)), rot = Rr(rnd, 0f, 360f);
                    d = Quaternion.AngleAxis(rot, axis) * (Quaternion.AngleAxis(ang, perp) * axis);
                }
                float s = Mathf.Lerp(0.015f, 0.06f, R01(rnd) * 0.6f + power * 0.4f) * (arterial ? 1.15f : 1f);
                if (GoreWorld.Ray(o, d, 3.5f, room, out var hit, ref _budget))
                {
                    Drop(room, hit.point, hit.normal, d, s, age, rnd, arterial);
                    bool wall = Mathf.Abs(hit.normal.y) < 0.5f;
                    if (wall) { _wall.Add((hit.point, hit.normal)); _markWall.Add((hit.point, hit.normal)); }
                    if (wall && s >= 0.04f && power >= 0.6f && _level > 0) Run(room, hit.point, hit.normal, Mathf.Lerp(0.1f, 0.45f, power * Rr(rnd, 0.55f, 1f)), age);
                }
                else Miss(room, o, d, s, power, age, rnd);
            }
            if (arterial && _level > 0 && WallGroup(_markWall, out var c, out var n, out var ext, out int cnt) && cnt >= 3 && _quads < MaxQuadsPerVictim)
            {
                float hu = Mathf.Clamp(ext * 0.5f + 0.12f, 0.22f, 0.6f);
                Batch(room).Quad(c - Vector3.up * 0.28f * hu, n, Vector3.up, hu, hu, GoreAtlas.Arterial, GorePalette.Aged(age, 0.25f), new Vector3(GorePalette.Gloss(age, 0.2f), 0, 1.6f), 0.0025f);
                _quads++;
                _cur.Hot.Add(new GoreHotspot { Kind = GoreKind.Arterial, Pos = c, Normal = n, Size = hu * 2f, Weight = 9f, Room = room, Label = "벽까지 뿜어진 피" });
            }
            // a blunt blow to the head against a nearby wall leaves a dent
            if (!arterial && radial && m.Region == BodyRegion.Head && m.Sev >= 3 && _budget > 0)
            {
                var hd = GoreWorld.Dir(m.Dir, 0f);
                if (GoreWorld.Ray(o, hd, 0.9f, room, out var wh, ref _budget) && Mathf.Abs(wh.normal.y) < 0.5f)
                {
                    try
                    {
                        var dent = TraceFactory.Create("Dent", wh.point, wh.normal, 0.14f, new Color(0.2f, 0.2f, 0.22f, 0.7f));
                        if (dent != null && _cur.Root != null) dent.transform.SetParent(_cur.Root, true);
                    }
                    catch { }
                }
            }
        }

        /// <summary>One drop (cluster cell) where a ray struck: stretched along the ray's projection by 1/sin(incidence).</summary>
        void Drop(int room, Vector3 p, Vector3 n, Vector3 travel, float s, float age, System.Random rnd, bool heavy)
        {
            if (_quads >= MaxQuadsPerVictim) return;
            var proj = Vector3.ProjectOnPlane(travel, n);
            if (proj.sqrMagnitude < 1e-4f) proj = Quaternion.AngleAxis(Rr(rnd, 0, 360), n) * Vector3.ProjectOnPlane(Vector3.forward, n);
            proj.Normalize();
            float sinInc = Mathf.Abs(Vector3.Dot(travel.normalized, n));
            float e = Mathf.Clamp(1f / Mathf.Max(0.2f, sinInc), 1f, 5f);
            bool wall = Mathf.Abs(n.y) < 0.5f;
            float side = Mathf.Clamp(s / 0.13f, 0.06f, 0.25f);
            float hu = side * 0.5f, hv = Mathf.Min(side * 0.5f * e, wall ? 0.2f : 0.3f);
            int cell = e < 1.25f && s > 0.03f ? GoreAtlas.Splat : GoreAtlas.Impact;
            var col = Color.Lerp(GorePalette.Thin(age), GorePalette.Aged(age, 0.1f), heavy ? 0.6f : R01(rnd) * 0.35f);
            if (_level == 0) { col.a *= 0.7f; hu *= 0.8f; hv = Mathf.Min(hv, hu * 1.6f); }   // 완화: fewer, smaller, lighter drops
            var centre = cell == GoreAtlas.Impact ? p + proj * (hv * 2f * 0.13f) : p;
            float gloss = Mathf.Lerp(0.85f, 0.3f, Mathf.Clamp01(age / 60f));
            if (!wall) { float k = FloorFit(centre, room, Mathf.Max(hu, hv) * 0.9f); hu *= k; hv *= k; }   // never under a wall into the next room
            Batch(room).Quad(centre, n, proj, hu, hv, cell, col, new Vector3(gloss, 0, 1.8f), 0.0022f + 0.0001f * (_quads % 7));
            _quads++;
        }

        /// <summary>A heavy wall drop runs down.</summary>
        void Run(int room, Vector3 p, Vector3 n, float L, float age)
        {
            if (_quads >= MaxQuadsPerVictim) return;
            float h = L * 0.5f;
            Batch(room).Quad(p - Vector3.up * (L * 0.36f), n, Vector3.up, h * 0.6f, h, GoreAtlas.DripRun, GorePalette.Thin(age * 0.8f), new Vector3(Mathf.Lerp(0.85f, 0.35f, Mathf.Clamp01(age / 60f)), 0, 1.6f), 0.0018f);
            _quads++;
        }

        /// <summary>A drop that met nothing within reach flies on and lands on the floor.</summary>
        void Miss(int room, Vector3 o, Vector3 d, float s, float power, float age, System.Random rnd)
        {
            if (_quads >= MaxQuadsPerVictim || _budget <= 0) return;
            float v0 = Mathf.Lerp(2.5f, 5f, power); var vel = d * v0;
            float floorY = o.y - 3f; var S = _curS;
            float fy = o.y; if (S?.Layout != null) { var mv = GoreWorld.M; fy = mv != null ? mv.ToWorld(new P3(mv.FloorOfY(o.y), 0, 0)).y : 0f; }
            floorY = fy;
            float hgt = Mathf.Max(0.05f, o.y - floorY);
            float t = (vel.y + Mathf.Sqrt(vel.y * vel.y + 4f * 4.905f * hgt)) / (2f * 4.905f);
            var land = o + new Vector3(vel.x, 0, vel.z) * t; land.y = floorY;
            if (!GoreWorld.Floor(land, room, out var p, out var n, ref _budget)) return;
            var imp = new Vector3(vel.x, vel.y - 9.81f * t, vel.z);
            Drop(room, p, n, imp, s * 0.8f, age, rnd, false);
        }

        // ------------------------------------------------------------------ cast-off: the arc of a swing
        /// <summary>
        /// Drops flung off the weapon as it swings back: a broken trail along the arc behind the attacker, no higher than a
        /// swing can throw them (floor + 3 m, or the ceiling where it is lower), each drop a little smaller than the last and
        /// the gaps between them uneven. A ray that only grazes a surface (a beam's side, a moulding) or lands within 3 cm of
        /// an edge leaves nothing there: a trail never strings itself along an edge like beads.
        /// </summary>
        void CastOff(GoreMark m)
        {
            var rnd = new System.Random((int)(m.Seed & 0x7FFFFFFF));
            int room = RoomOf(m); var o = GoreWorld.ToWorld(m.Pos) + Vector3.up * Mathf.Max(1.2f, m.H); float age = GoreWorld.AgeMin(m.Clock);
            float floorY = GoreWorld.ToWorld(m.Pos).y, capY = floorY + 3.0f;
            var mv = GoreWorld.M; if (mv != null && mv.Rooms != null && room >= 0 && room < mv.Rooms.Length && mv.Rooms[room] != null && mv.Rooms[room].CeilY < capY) capY = mv.Rooms[room].CeilY + 0.02f;
            _arc.Clear();
            float p0 = m.Pitch - 10f, p1 = Mathf.Min(m.Pitch + 45f, 88f);
            const int n = 10;
            for (int i = 0; i < n && _budget > 0; i++)
            {
                float t = Mathf.Clamp01((i + Rr(rnd, -0.38f, 0.38f)) / (n - 1f));   // uneven spacing along the swing
                var d = GoreWorld.Dir(m.Dir + Rr(rnd, -5f, 5f), Mathf.Lerp(p0, p1, t));
                if (!GoreWorld.Ray(o, d, 3.5f, room, out var hit, ref _budget)) continue;
                if (hit.point.y > capY) continue;                                   // out of the swing's reach
                if (Mathf.Abs(Vector3.Dot(d, hit.normal)) < 0.22f) continue;         // grazing: a beam's side, a moulding
                if (!FlatAround(o, hit, d, room)) continue;                         // within 3 cm of an edge
                _arc.Add((hit.point, hit.normal, d));
            }
            for (int i = 0; i < _arc.Count && _quads < MaxQuadsPerVictim; i++)
            {
                var h = _arc[i]; float k = _arc.Count > 1 ? i / (float)(_arc.Count - 1) : 0f;
                bool ceiling = h.n.y < -0.5f;
                // shrinking along the arc (the weapon sheds its heaviest drops first); smaller still overhead
                float s = (ceiling ? Mathf.Lerp(0.03f, 0.015f, k) : Mathf.Lerp(0.038f, 0.016f, k)) * Rr(rnd, 0.72f, 1.18f);
                Drop(room, h.p, h.n, h.d, s, age, rnd, false);
                // between this drop and the next on the same face: none, one or two small ones at random places
                if (i + 1 < _arc.Count && Vector3.Dot(h.n, _arc[i + 1].n) > 0.95f && Vector3.Distance(h.p, _arc[i + 1].p) < 0.9f)
                {
                    int extra = rnd.Next(0, 3);
                    var side = Vector3.Cross(h.n, h.d); if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(h.n, Vector3.up); if (side.sqrMagnitude < 1e-4f) side = Vector3.right; side.Normalize();
                    for (int e = 0; e < extra && _quads < MaxQuadsPerVictim; e++)
                    {
                        var p = Vector3.Lerp(h.p, _arc[i + 1].p, Rr(rnd, 0.2f, 0.8f)) + side * Rr(rnd, -0.025f, 0.025f);
                        Drop(room, p, h.n, h.d, s * Rr(rnd, 0.35f, 0.65f), age, rnd, false);
                    }
                }
            }
            if (_arc.Count > 0)
            {
                var mid = _arc[_arc.Count / 2];
                _cur.Hot.Add(new GoreHotspot { Kind = GoreKind.CastOff, Pos = mid.p, Normal = mid.n, Size = 0.5f, Weight = 6f, Room = room, Label = "휘두른 흉기에서 날아간 핏방울" });
            }
        }

        /// <summary>The face a drop landed on continues 3 cm to either side across the swing (two parallel rays meet the same
        /// plane): not an edge, a beam's corner or a moulding's lip.</summary>
        bool FlatAround(Vector3 o, RaycastHit hit, Vector3 d, int room)
        {
            var across = Vector3.Cross(hit.normal, d); if (across.sqrMagnitude < 1e-4f) across = Vector3.Cross(hit.normal, Vector3.up); if (across.sqrMagnitude < 1e-4f) across = Vector3.right;
            across.Normalize();
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                if (_budget <= 0) return false;
                if (!GoreWorld.Ray(o + across * (0.03f * sgn), d, 3.6f, room, out var h2, ref _budget)) return false;
                if (Vector3.Dot(h2.normal, hit.normal) < 0.95f) return false;
                if (Mathf.Abs(Vector3.Dot(h2.point - hit.point, hit.normal)) > 0.015f) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ smears, prints, scratches, kerfs, ooze
        bool WallAt(GoreMark m, int room, float h, out RaycastHit hit)
        {
            var o = GoreWorld.ToWorld(m.Pos) + Vector3.up * h;
            return GoreWorld.Ray(o, GoreWorld.Dir(m.Dir, 0f), 1.5f, room, out hit, ref _budget) && Mathf.Abs(hit.normal.y) < 0.5f;
        }

        void Smear(GoreMark m)
        {
            int room = RoomOf(m); float age = GoreWorld.AgeMin(m.Clock);
            float floorY = GoreWorld.ToWorld(m.Pos).y;
            if (WallAt(m, room, Mathf.Max(0.5f, m.H), out var hit))
            {
                float top = hit.point.y, L = Mathf.Max(0.25f, top - (floorY + 0.4f));
                var down = -Vector3.up; var v = Vector3.Cross(down, hit.normal);
                float wide = m.H >= 1.0f ? 0.17f : 0.09f;
                Batch(room).Quad(hit.point - Vector3.up * (L * 0.5f - 0.04f), hit.normal, v, L * 0.5f + 0.06f, wide, GoreAtlas.Smear, GorePalette.Thin(age, 0.92f), new Vector3(0.45f, 0, 1f), 0.0026f);
                _quads++;
                _cur.Hot.Add(new GoreHotspot { Kind = GoreKind.Smear, Pos = hit.point - Vector3.up * L * 0.4f, Normal = hit.normal, Size = L, Weight = 6f, Room = room, Label = "벽을 타고 흘러내린 핏자국" });
                return;
            }
            if (GoreWorld.Floor(GoreWorld.ToWorld(m.Pos), room, out var p, out var n, ref _budget))
            {
                float fs = 0.25f * FloorFit(p, room, 0.25f * 0.9f);
                Batch(room).Quad(p, n, GoreWorld.Dir(m.Dir, 0), fs, fs, GoreAtlas.Smear, GorePalette.Thin(age, 0.9f), new Vector3(0.45f, 0, 1f), 0.0026f);
                _quads++;
            }
        }

        void Hand(GoreMark m)
        {
            int room = RoomOf(m); float age = GoreWorld.AgeMin(m.Clock);
            var rnd = new System.Random((int)(m.Seed & 0x7FFFFFFF));
            RaycastHit hit; bool ok;
            if (m.Door >= 0) ok = DoorFrame(m, room, Mathf.Clamp(m.H, 0.9f, 1.3f), rnd, out hit);
            else ok = WallAt(m, room, Mathf.Clamp(m.H, 0.6f, 1.6f), out hit);
            if (!ok) return;
            var v = Quaternion.AngleAxis(Rr(rnd, -20f, 20f), hit.normal) * Vector3.up;
            Batch(room).Quad(hit.point, hit.normal, v, 0.17f, 0.17f, GoreAtlas.Handprint, GorePalette.Thin(age, 0.95f), new Vector3(0.5f, 0, 0.8f), 0.003f);
            _quads++;
            _cur.Hot.Add(new GoreHotspot { Kind = GoreKind.Handprint, Pos = hit.point, Normal = hit.normal, Size = 0.2f, Weight = m.Door >= 0 ? 7f : 6.5f, Room = room, Label = m.Door >= 0 ? "문틀에 찍힌 피 묻은 손자국" : "벽에 남은 피 묻은 손자국" });
        }

        /// <summary>The frame beside a doorway, on the mark's side of the wall, at height h.</summary>
        bool DoorFrame(GoreMark m, int room, float h, System.Random rnd, out RaycastHit hit)
        {
            hit = default;
            var S = _curS; var door = S?.Layout?.Door(m.Door); if (door == null) return false;
            var dp = GoreWorld.ToWorld(door.Pos);
            var along = door.AlongX ? Vector3.right : Vector3.forward; var nrm = door.AlongX ? Vector3.forward : Vector3.right;
            var mp = GoreWorld.ToWorld(m.Pos); float side = Vector3.Dot(mp - dp, nrm) >= 0 ? 1f : -1f; nrm *= side;
            // on the wall right beside the architrave (the trim itself has no collider; 13 cm wide): the hand grabbed at the frame
            float lat = (door.Width * 0.5f + 0.13f + 0.09f) * (R01(rnd) < 0.5f ? -1f : 1f);
            for (int k = 0; k < 2; k++)
            {
                var start = dp + along * lat + nrm * 0.5f + Vector3.up * h;
                if (GoreWorld.Ray(start, -nrm, 1.0f, room, out hit, ref _budget) && Mathf.Abs(hit.normal.y) < 0.5f) return true;
                lat = -lat;
            }
            return false;
        }

        void Scratch(GoreMark m)
        {
            int room = RoomOf(m); var rnd = new System.Random((int)(m.Seed & 0x7FFFFFFF));
            var col = new Color(GorePalette.DryRim.r, GorePalette.DryRim.g, GorePalette.DryRim.b, 0.78f);
            if (m.Door >= 0)
            {
                if (!DoorFrame(m, room, Rr(rnd, 0.45f, 0.9f), rnd, out var dh)) return;
                Batch(room).Quad(dh.point, dh.normal, Vector3.up, 0.08f, 0.13f, GoreAtlas.NailScratch, col, new Vector3(0.2f, 0, 1.4f), 0.003f);
                _quads++;
                _cur.Hot.Add(new GoreHotspot { Kind = GoreKind.NailScratch, Pos = dh.point, Normal = dh.normal, Size = 0.2f, Weight = 6f, Room = room, Label = "문 가까이 긁힌 손톱자국" });
                return;
            }
            if (!GoreWorld.Floor(GoreWorld.ToWorld(m.Pos), room, out var p, out var n, ref _budget)) return;
            var v = Quaternion.AngleAxis(Rr(rnd, 0, 360), n) * Vector3.ProjectOnPlane(Vector3.forward, n);
            Batch(room).Quad(p, n, v, 0.07f, 0.1f, GoreAtlas.NailScratch, col, new Vector3(0.2f, 0, 1.4f), 0.003f);
            _quads++;
            _cur.Hot.Add(new GoreHotspot { Kind = GoreKind.NailScratch, Pos = p, Normal = n, Size = 0.18f, Weight = 5.5f, Room = room, Label = "바닥을 긁은 손톱자국" });
        }

        void Kerf(GoreMark m)
        {
            int room = RoomOf(m); var rnd = new System.Random((int)(m.Seed & 0x7FFFFFFF));
            if (!GoreWorld.Floor(GoreWorld.ToWorld(m.Pos), room, out var p, out var n, ref _budget)) return;
            int k = 2 + rnd.Next(2); float a0 = Rr(rnd, 0, 360);
            var col = new Color(GorePalette.Thick.r, GorePalette.Thick.g, GorePalette.Thick.b, 0.88f);
            for (int i = 0; i < k && _quads < MaxQuadsPerVictim; i++)
            {
                float a = a0 + i * Rr(rnd, 25f, 70f);
                var v = Quaternion.AngleAxis(a, n) * Vector3.ProjectOnPlane(Vector3.forward, n);
                var off = Quaternion.AngleAxis(a + 90f, n) * Vector3.ProjectOnPlane(Vector3.forward, n) * Rr(rnd, -0.05f, 0.05f);
                Batch(room).Quad(p + off, n, v, 0.18f, 0.05f, GoreAtlas.SawKerf, col, new Vector3(0.3f, 0, 1.6f), 0.0028f + i * 0.0002f);
                _quads++;
            }
            _cur.Hot.Add(new GoreHotspot { Kind = GoreKind.SawMark, Pos = p, Normal = n, Size = 0.35f, Weight = 7f, Room = room, Label = "바닥에 남은 톱날 자국" });
        }

        void Ooze(GoreMark m)
        {
            int room = RoomOf(m); float age = GoreWorld.AgeMin(m.Clock); var rnd = new System.Random((int)(m.Seed & 0x7FFFFFFF));
            if (!GoreWorld.Floor(GoreWorld.ToWorld(m.Pos), room, out var p, out var n, ref _budget)) return;
            float h = 0.1f + 0.4f * Mathf.Clamp01(m.Power); h *= FloorFit(p, room, h * 0.9f);
            var v = Quaternion.AngleAxis(Rr(rnd, 0, 360), n) * Vector3.ProjectOnPlane(Vector3.forward, n);
            Batch(room).Quad(p, n, v, h, h, GoreAtlas.Ooze, GorePalette.Aged(age, 0.45f, 0.95f), new Vector3(GorePalette.Gloss(age, 0.2f), 0, 1.8f), 0.0012f);
            _quads++;
        }

        /// <summary>The largest group of wall hits sharing a wall (normal within ~25°, within 1.5 m): centroid, mean normal,
        /// horizontal extent.</summary>
        static bool WallGroup(List<(Vector3 p, Vector3 n)> hits, out Vector3 c, out Vector3 n, out float extent, out int count)
        {
            c = default; n = Vector3.forward; extent = 0; count = 0;
            if (hits.Count == 0) return false;
            int best = -1, bestN = 0;
            for (int i = 0; i < hits.Count; i++)
            {
                int k = 0;
                for (int j = 0; j < hits.Count; j++) if (Vector3.Dot(hits[i].n, hits[j].n) > 0.9f && (hits[i].p - hits[j].p).sqrMagnitude < 2.25f) k++;
                if (k > bestN) { bestN = k; best = i; }
            }
            if (best < 0) return false;
            Vector3 sum = Vector3.zero, ns = Vector3.zero; var h0 = hits[best];
            var t = Vector3.Cross(Vector3.up, h0.n); if (t.sqrMagnitude < 1e-4f) t = Vector3.right; t.Normalize();
            float mn = float.MaxValue, mx = float.MinValue;
            foreach (var h in hits)
            {
                if (Vector3.Dot(h0.n, h.n) <= 0.9f || (h0.p - h.p).sqrMagnitude >= 2.25f) continue;
                sum += h.p; ns += h.n; float x = Vector3.Dot(h.p, t); mn = Mathf.Min(mn, x); mx = Mathf.Max(mx, x); count++;
            }
            c = sum / count; n = ns.normalized; extent = mx - mn;
            return true;
        }
    }
}
