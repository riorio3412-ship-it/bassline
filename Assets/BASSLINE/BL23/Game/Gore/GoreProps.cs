using System.Collections;
using System.Collections.Generic;
using BL23.Game.Audio;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Furniture knocked over in a struggle (kernel Topple marks). The kernel never moves the piece (Pos/Yaw/Moved stay);
    /// this lays the visual on its side: rotated 90° about the bottom edge on the fall side (bearing = mark.Dir), lowered
    /// until its lowest point is 2 mm above the floor. If the room is on screen and within 25 m of the camera when the mark
    /// first appears, the piece tips over as a rigid body pivoting on that edge (θ'' = 3g/2h · sin θ from 3°, two bounces at
    /// restitution 0.25, an impact sound by material), then its Rigidbody is let go for a second to settle for real (snapped
    /// back if it drifts more than 0.6 m). Re-applied only when the furniture GameObject is rebuilt or put upright again by the
    /// mansion; never re-applied once the player has handled the piece. Undone (SetFurnitureState to the kernel pose) when
    /// the mark is Righted, gone, or its victim is being replayed.
    /// </summary>
    internal static class GoreProps
    {
        sealed class St
        {
            public int Fid; public GameObject Go; public GoreMark Mark; public string Victim;
            public Vector3 RestPos; public Quaternion RestRot; public bool Animating; public bool PlayerOwned; public Rigidbody Rb;
            public Vector3 Centre;
        }
        static readonly Dictionary<int, St> _st = new Dictionary<int, St>();
        static readonly Dictionary<int, (GoreMark m, string victim)> _want = new Dictionary<int, (GoreMark, string)>();
        static readonly List<int> _drop = new List<int>();
        static readonly HashSet<uint> _fell = new HashSet<uint>();   // marks whose fall was already shown (no re-run after a replay)

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _st.Clear(); _want.Clear(); _drop.Clear(); _fell.Clear(); }

        /// <summary>Forget everything (world rebuilt / new state). Pieces still standing in a live mansion are set upright.</summary>
        public static void ResetAll()
        {
            var M = GoreWorld.M; var S = GoreWorld.S;
            foreach (var st in _st.Values) if (st.Go != null && M != null && S?.Layout != null && st.Fid >= 0 && st.Fid < S.Layout.Furniture.Count && M.FurnitureObject(st.Fid) == st.Go) Upright(M, S.Layout.Furniture[st.Fid]);
            _st.Clear();
        }

        /// <summary>World-space centre of a toppled piece (for hotspots), or false.</summary>
        public static bool Centre(int fid, out Vector3 c) { c = default; if (_st.TryGetValue(fid, out var st) && st.Go != null) { c = st.Centre; return true; } return false; }
        public static int Count => _st.Count;

        public static void Sync(GameState S, MansionView M)
        {
            if (S?.Layout == null || M == null) return;
            _want.Clear();
            foreach (var a in S.Actors.Values)
            {
                var marks = a.Body?.GoreMarks; if (marks == null) continue;
                bool replay = false; var v = GoreWorld.View(a.Id); if (v != null) replay = v.ReplayDriven;
                for (int i = 0; i < marks.Count; i++)
                {
                    var m = marks[i];
                    if (m.Kind != GoreKind.Topple || m.Righted || replay || m.Furniture < 0 || m.Furniture >= S.Layout.Furniture.Count) continue;
                    _want[m.Furniture] = (m, a.Id);
                }
            }
            // undo what is no longer wanted
            _drop.Clear();
            foreach (var kv in _st) if (!_want.ContainsKey(kv.Key)) _drop.Add(kv.Key);
            foreach (var fid in _drop)
            {
                var st = _st[fid]; _st.Remove(fid);
                if (st.Go != null && M.FurnitureObject(fid) == st.Go && !st.PlayerOwned) Upright(M, S.Layout.Furniture[fid]);
            }
            // apply what is wanted
            foreach (var kv in _want)
            {
                int fid = kv.Key; var f = S.Layout.Furniture[fid];
                _st.TryGetValue(fid, out var st);
                var go = M.FurnitureObject(fid);
                if (go == null) continue;
                if (st != null)
                {
                    if (st.Animating) continue;
                    if (st.Rb != null && PhysicsGrab.RecentlyPlayer(st.Rb)) st.PlayerOwned = true;
                    // same object, still lying down (or the player has it): leave it alone
                    if (st.Go == go && (st.PlayerOwned || Vector3.Dot(go.transform.up, Vector3.up) < 0.8f)) { st.Centre = CentreOf(go); continue; }
                    if (st.Go == go && st.PlayerOwned) continue;
                }
                bool first = st == null && !_fell.Contains(kv.Value.m.Seed);
                _fell.Add(kv.Value.m.Seed); if (_fell.Count > 512) _fell.Clear();
                if (st == null) { st = new St { Fid = fid }; _st[fid] = st; }
                st.Mark = kv.Value.m; st.Victim = kv.Value.victim; st.PlayerOwned = false;
                Apply(S, M, f, st, first);
            }
        }

        static void Upright(MansionView M, Furniture f) { try { M.SetFurnitureState(f.Id, f.Damage, f.Pos, f.Yaw); } catch (System.Exception e) { Debug.LogWarning("[Gore] upright " + f.Type + ": " + e.Message); } }

        static void Apply(GameState S, MansionView M, Furniture f, St st, bool first)
        {
            // a statically batched piece cannot move: the mansion swaps in an unbatched copy on its first state change
            if (M.Batched.Contains(f.Id)) { try { M.SetFurnitureState(f.Id, f.Damage, f.Pos, f.Yaw + 1.1f); } catch { } }
            var go = M.FurnitureObject(f.Id); if (go == null) return;
            st.Go = go; st.Rb = go.GetComponent<Rigidbody>();
            // upright reference pose = the kernel's
            Vector3 p0 = M.ToWorld(f.Pos); Quaternion q0 = Quaternion.Euler(0, f.Yaw, 0);
            go.transform.SetPositionAndRotation(p0, q0);
            var fall = GoreWorld.Dir(st.Mark.Dir, 0); fall.y = 0; if (fall.sqrMagnitude < 1e-4f) fall = go.transform.forward; fall.Normalize();
            var axis = Vector3.Cross(Vector3.up, fall).normalized;               // +90° about this tips up → fall
            var dl = Quaternion.Inverse(q0) * fall;
            float W = Mathf.Max(0.1f, f.W), D = Mathf.Max(0.1f, f.D);
            float reach = Mathf.Abs(dl.x) * W * 0.5f + Mathf.Abs(dl.z) * D * 0.5f;
            var pivot = p0 + fall * reach;
            float floorY = p0.y;
            // rest pose: 90° about the edge, then lowered onto the floor
            var qr = Quaternion.AngleAxis(90f, axis);
            go.transform.SetPositionAndRotation(pivot + qr * (p0 - pivot), qr * q0);
            float lower = Bounds(go, out var b) ? (floorY + 0.002f) - b.min.y : 0f;
            st.RestPos = go.transform.position + Vector3.up * lower; st.RestRot = go.transform.rotation;
            st.Centre = st.RestPos + Vector3.up * 0.2f;
            var cam = GoreWorld.M != null && GoreWorld.M.ViewCamera != null ? GoreWorld.M.ViewCamera : Camera.main;
            bool seen = first && GoreWorld.RoomVisible(f.Room) && cam != null && (cam.transform.position - p0).sqrMagnitude < 25f * 25f && GoreScene.I != null;
            if (seen)
            {
                go.transform.SetPositionAndRotation(p0, q0);
                st.Animating = true;
                GoreScene.I.StartCoroutine(Tip(st, f, p0, q0, pivot, axis, lower, Mathf.Max(0.3f, f.H)));
            }
            else
            {
                go.transform.SetPositionAndRotation(st.RestPos, st.RestRot);
                st.Centre = CentreOf(go);
                if (st.Rb != null && GoreScene.I != null) GoreScene.I.StartCoroutine(Settle(st, false));
            }
        }

        static IEnumerator Tip(St st, Furniture f, Vector3 p0, Quaternion q0, Vector3 pivot, Vector3 axis, float lower, float h)
        {
            var go = st.Go; var rb = st.Rb; bool wasKin = rb != null && rb.isKinematic;
            if (rb != null) rb.isKinematic = true;
            float th = 3f * Mathf.Deg2Rad, w = 0f, k = 3f * 9.81f / (2f * h); int bounces = 0; float t0 = Time.time;
            while (go != null && Time.time - t0 < 4f)
            {
                float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
                w += k * Mathf.Sin(th) * dt; th += w * dt;
                if (th >= Mathf.PI * 0.5f)
                {
                    th = Mathf.PI * 0.5f;
                    if (bounces == 0) ImpactSound(f, go.transform.position);
                    if (bounces < 2) { w = -w * 0.25f; bounces++; } else break;
                }
                var q = Quaternion.AngleAxis(th * Mathf.Rad2Deg, axis);
                float s = th / (Mathf.PI * 0.5f);
                go.transform.SetPositionAndRotation(pivot + q * (p0 - pivot) + Vector3.up * lower * s, q * q0);
                if (rb != null) rb.isKinematic = true;
                yield return null;
            }
            if (go != null) go.transform.SetPositionAndRotation(st.RestPos, st.RestRot);
            if (rb != null) rb.isKinematic = wasKin;
            st.Animating = false;
            if (go != null) st.Centre = CentreOf(go);
            if (rb != null && go != null) yield return Settle(st, true);
        }

        /// <summary>Let the body go for a second so it finds its own rest on the floor; snap back if it wanders.</summary>
        static IEnumerator Settle(St st, bool afterTip)
        {
            var rb = st.Rb; var go = st.Go; if (rb == null || go == null) yield break;
            bool wasKin = rb.isKinematic;
            rb.isKinematic = false; rb.WakeUp();
            float t0 = Time.time;
            while (Time.time - t0 < 1f && rb != null) yield return null;
            if (rb == null || go == null) yield break;
            if ((go.transform.position - st.RestPos).sqrMagnitude > 0.36f) { go.transform.SetPositionAndRotation(st.RestPos, st.RestRot); rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            else { st.RestPos = go.transform.position; st.RestRot = go.transform.rotation; }
            rb.isKinematic = wasKin;
            st.Centre = CentreOf(go);
        }

        static void ImpactSound(Furniture f, Vector3 at)
        {
            string id = f.Material == Mat.Metal ? "metal_clang" : f.Material == Mat.Wood ? "wood_crack" : "chair_scrape";
            try { if (Sfx.Has(id)) Sfx.Play(id, at, 0.85f); else if (Sfx.Has("body_fall")) Sfx.Play("body_fall", at, 0.5f); } catch { }
        }

        static bool Bounds(GameObject go, out Bounds b)
        {
            b = default; bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.name == "flame") continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        static Vector3 CentreOf(GameObject go) => Bounds(go, out var b) ? b.center : go.transform.position + Vector3.up * 0.2f;
    }
}
