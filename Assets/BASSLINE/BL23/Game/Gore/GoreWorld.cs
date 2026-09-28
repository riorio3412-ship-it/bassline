using System.Collections.Generic;
using BL23.Game.Characters;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>Shared access for Game/Gore: the live session, kernel→world conversion and the "may blood land here"
    /// surface filter (layer masks do not separate people: NPC capsules, items and the player share the default layer).</summary>
    internal static class GoreWorld
    {
        public static Session Ses => Session.I;
        public static GameState S { get { var s = Session.I; return s != null && s.Sim != null ? s.Sim.S : null; } }
        public static WorldPresenter W { get { var s = Session.I; return s != null ? s.World : null; } }
        public static MansionView M { get { var w = W; return w != null ? w.Mansion : null; } }

        public static Vector3 ToWorld(P3 p)
        {
            var w = W; if (w != null && w.Mansion != null) return w.Mansion.ToWorld(p);
            var s = S; return new Vector3(p.x, s?.Layout != null ? s.Layout.FloorY(p.f) : 0f, p.z);
        }

        /// <summary>Kernel bearing (MathX.AngleDeg: 0 = +z, 90 = +x) and elevation to a world direction.</summary>
        public static Vector3 Dir(float bearingDeg, float pitchDeg)
        {
            float b = bearingDeg * Mathf.Deg2Rad, p = pitchDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(b) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Cos(b) * Mathf.Cos(p));
        }

        public static float AgeMin(double clock) { var s = S; return s != null && clock > 0 ? (float)(s.Clock - clock) : 0f; }

        public static ActorView View(string id) { var w = W; return w != null ? w.ViewOf(id) : null; }

        public static Characters.BodyRegion Region(Sim.BodyRegion r) => (Characters.BodyRegion)(int)r;

        // ------------------------------------------------------------------ surfaces
        static readonly Dictionary<Collider, bool> _surf = new Dictionary<Collider, bool>();
        static MansionView _surfFor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _surf.Clear(); _surfFor = null; }

        /// <summary>Mansion geometry (walls, floors, ceilings, fixed furniture), never people, items, pieces or loose props
        /// that physics can still move (a decal on them would float once they are knocked).</summary>
        public static bool IsSurface(Collider c)
        {
            if (c == null) return false;
            var m = M; if (m == null) return false;
            if (_surfFor != m) { _surf.Clear(); _surfFor = m; }
            var id = c;
            if (_surf.TryGetValue(id, out var ok)) return ok;
            ok = c.transform.IsChildOf(m.transform)
                && c.attachedRigidbody == null
                && c.GetComponentInParent<ActorView>() == null && c.GetComponentInParent<ActorRig>() == null
                && c.GetComponentInParent<ItemTag>() == null && c.GetComponentInParent<ItemView>() == null
                && c.GetComponentInParent<GorePieceTag>() == null
                // things that swing or slide (door leaves, cabinet doors and drawers, fans, the press) would carry a decal away
                && c.GetComponentInParent<DoorView>() == null && c.GetComponentInParent<Swinger>() == null && c.GetComponentInParent<Spinner>() == null && c.GetComponentInParent<PressView>() == null
                && !Moving(c);
            if (_surf.Count > 4096) _surf.Clear();
            _surf[id] = ok;
            return ok;
        }

        static bool Moving(Collider c)
        {
            var op = c.GetComponentInParent<OpenableParts>();
            return op != null && op.IsMoving(c.transform);
        }

        /// <summary>A ray that only stops on a mansion surface in the given room (room &lt; 0: any). Passes through rejected
        /// colliders at most twice. budget is decremented per physics query.</summary>
        public static bool Ray(Vector3 o, Vector3 d, float max, int room, out RaycastHit hit, ref int budget)
        {
            hit = default;
            // ceilings have no colliders (the slab above sits higher than the painted ceiling): a rising ray stops at the
            // room's ceiling plane unless something nearer takes it first
            float ceilT = float.MaxValue; var mv = M;
            if (room >= 0 && d.y > 0.05f && mv != null && mv.Rooms != null && room < mv.Rooms.Length && mv.Rooms[room] != null)
            {
                float cy = mv.Rooms[room].CeilY;
                if (cy > o.y + 0.05f) { float t = (cy - o.y) / d.y; if (t < max) { ceilT = t; max = t; } }
            }
            var o0 = o;
            for (int k = 0; k < 3 && budget > 0 && max > 0.01f; k++)
            {
                budget--;
                if (!Physics.Raycast(o, d, out var h, max, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (ceilT < float.MaxValue)
                    {
                        var cp = o0 + d * ceilT;
                        if (mv.RoomAtWorld(cp - Vector3.up * 0.05f) != room) return false;
                        hit.point = cp; hit.normal = Vector3.down; hit.distance = ceilT; return true;
                    }
                    return false;
                }
                if (IsSurface(h.collider))
                {
                    var m = M;
                    if (room < 0 || m == null || m.RoomAtWorld(h.point + h.normal * 0.05f) == room) { hit = h; return true; }
                    // another room's side of a wall (or a doorway): that is where the drop would stop, but it is not ours
                    return false;
                }
                float step = h.distance + 0.02f; o += d * step; max -= step;
            }
            return false;
        }

        /// <summary>The floor (or furniture top) under a point, searched from 0.6 m above.</summary>
        public static bool Floor(Vector3 p, int room, out Vector3 point, out Vector3 normal, ref int budget)
        {
            point = p; normal = Vector3.up;
            if (Ray(p + Vector3.up * 0.6f, Vector3.down, 3.5f, room, out var h, ref budget)) { point = h.point; normal = h.normal; return true; }
            return false;
        }

        /// <summary>Is a room currently drawn (the mansion culls rooms around the view camera).</summary>
        public static bool RoomVisible(int room)
        {
            var m = M; if (m == null || m.Rooms == null || room < 0 || room >= m.Rooms.Length || m.Rooms[room] == null) return true;
            return m.Rooms[room].Visible;
        }

        public static uint Hash(string s) { unchecked { uint h = 2166136261u; if (s != null) foreach (char ch in s) { h ^= ch; h *= 16777619u; } return h; } }
    }

    /// <summary>Marks the root of a Game/Gore visual (a severed piece skin), so blood rays and filters skip it.</summary>
    public sealed class GorePieceTag : MonoBehaviour { public string ItemId; }
}
