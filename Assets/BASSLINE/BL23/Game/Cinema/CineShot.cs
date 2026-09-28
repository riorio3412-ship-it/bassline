using System;
using System.Collections.Generic;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Cinema
{
    /// <summary>The shot vocabulary shared by the montage panes, the reveal film and (by analogy) the court camera.</summary>
    public enum ShotKind
    {
        Establish,  // high corner of the room, the subject small in it; a slow crane down
        Medium,     // waist-up three-quarter, off-centre, a slow push
        Over,       // over Other's shoulder onto Subject's face (rack focus from the shoulder)
        Low,        // hero/threat angle from knee height, dutch roll, push-in
        Top,        // straight down from under the ceiling, slowly turning
        Hand,       // extreme close-up of the weapon hand (shallow focus)
        Eyes,       // extreme close-up of the eyes (a letterbox pane)
        Face,       // reaction close-up
        Feet,       // floor-level tracking of the feet
        Thing,      // extreme close-up of a place/object (a latch, a trace, a basin)
        Behind,     // silhouette from behind, looking where they look
        Orbit,      // slow half-circle around the subject
        Two,        // both of them in one frame (attacker and victim), low and tilted, pushing in
    }

    public sealed class ShotSpec
    {
        public ShotKind Kind; public string Subject, Other; public Vector3? Point;
        public float Side = 1f;        // +1: camera on the subject's right-front, -1: left-front
        public bool Dutch; public float Push = 1f; public bool Rack;
        public ShotSpec(ShotKind k, string subject, string other = null, float side = 1f) { Kind = k; Subject = subject; Other = other; Side = side; }
        public override string ToString() => Kind + ":" + Subject + (Other != null ? ">" + Other : "");
    }

    /// <summary>Live anchors on an actor (bones move every frame).</summary>
    public static class CineAnchors
    {
        public static ActorView View(string id) => id == null ? null : Session.I?.World?.ViewOf(id);
        public static Vector3 Root(string id) { var v = View(id); return v != null ? v.transform.position : Vector3.zero; }
        public static Vector3 Head(string id) { var v = View(id); if (v == null) return Vector3.zero; return v.HeadPos; }
        public static Vector3 Chest(string id) { var v = View(id); if (v == null) return Vector3.zero; return v.Rig?.Chest != null ? v.Rig.Chest.position : v.transform.position + Vector3.up * 1.2f; }
        public static Vector3 Hips(string id) { var v = View(id); if (v == null) return Vector3.zero; return v.Rig?.Hips != null ? v.Rig.Hips.position : v.transform.position + Vector3.up * 0.9f; }
        public static Vector3 Hand(string id)
        {
            var v = View(id); if (v?.Rig == null) return Chest(id);
            bool left = Cast.LeftHanded(id); var t = left ? (v.Rig.HandAnchorL ?? v.Rig.HandL) : (v.Rig.HandAnchorR ?? v.Rig.HandR);
            return t != null ? t.position : Chest(id);
        }
        public static Vector3 Feet(string id)
        {
            var v = View(id); if (v?.Rig == null) return Root(id) + Vector3.up * 0.08f;
            if (v.Rig.FootL != null && v.Rig.FootR != null) return (v.Rig.FootL.position + v.Rig.FootR.position) * 0.5f;
            return v.transform.position + Vector3.up * 0.08f;
        }
        /// <summary>Where the face points (follows the head bone: a bowed head, a body on its back).</summary>
        public static Vector3 FaceFwd(string id)
        {
            // the eyes sit in front of the centre of the head, whatever the rig's bone axes: that offset is the face's direction
            var v = View(id); var ea = v?.Rig?.EyeAnchor; if (ea == null) return Forward(id);
            Vector3 hc; try { hc = v.Rig.HeadCenterWorld(); } catch (System.Exception) { return Forward(id); }
            var f = ea.position - hc; if (f.magnitude < 0.02f) return Forward(id); f.Normalize();
            if (!Lying(id)) { var body = Forward(id); var hz = new Vector3(f.x, 0, f.z); if (hz.sqrMagnitude < 1e-3f || Vector3.Dot(hz.normalized, body) < -0.2f) hz = body; hz = Vector3.Slerp(body, hz.normalized, 0.6f); f = (hz.normalized * Mathf.Sqrt(Mathf.Max(0f, 1f - f.y * f.y)) + Vector3.up * Mathf.Clamp(f.y, -0.5f, 0.5f)).normalized; }
            return f;
        }
        public static Vector3 Forward(string id) { var v = View(id); if (v == null) return Vector3.forward; var f = v.transform.forward; f.y = 0; return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward; }
        public static bool Lying(string id)
        {
            var v = View(id); var an = v?.Rig?.Anim; if (an == null) return false;
            var p = an.CurrentPosture; return an.IsDead || p == Posture.LieBack || p == Posture.LieFront || p == Posture.LieSide || p == Posture.Slumped;
        }
        /// <summary>The middle of the body, standing or lying.</summary>
        public static Vector3 Body(string id) => Lying(id) ? (Head(id) + Hips(id)) * 0.5f : Chest(id);
    }

    /// <summary>A solved shot: where the camera goes over its life, relative to a live anchor, and how it is lensed.</summary>
    public sealed class CineShot
    {
        public ShotSpec Spec; public Func<Vector3> Target; public Vector3 Base; public bool Follow;
        public Vector3 O0, O1, L0, L1; public float Fov0 = 40, Fov1 = 38, Roll0, Roll1;
        public bool Dof; public float Aperture = 4f, Focal = 50f, RackFrom = -1f;
        public Vector3 Up = Vector3.up; public float SpinDeg, OrbitDeg;
        public float FollowTime = 0.35f;
        /// <summary>Close shots are framed by width (metres across at the subject), so a letterbox pane and a lancet pane both hold the eyes, the hand.</summary>
        public float FitW0, FitW1;
        Vector3 _anchor, _vel; bool _init;
        public Vector3 CamPos { get; private set; }

        static float Ease(float u) { u = Mathf.Clamp01(u); float s = u * u * (3f - 2f * u); return Mathf.Lerp(u, s, 0.65f); }

        public void Apply(Camera cam, float u, float dt)
        {
            if (cam == null) return;
            var tgt = Target != null ? Target() : Base;
            if (!_init) { _anchor = Follow ? tgt : Base; _init = true; }
            else if (Follow) _anchor = Vector3.SmoothDamp(_anchor, tgt, ref _vel, FollowTime, 40f, Mathf.Max(1e-4f, dt));
            float e = Ease(u);
            var off = Vector3.Lerp(O0, O1, e);
            if (OrbitDeg != 0) off = Quaternion.AngleAxis(OrbitDeg * (e - 0.5f), Vector3.up) * off;
            var pos = _anchor + off; var look = _anchor + Vector3.Lerp(L0, L1, e);
            var fwd = look - pos; if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward; fwd.Normalize();
            var up = Up; if (SpinDeg != 0) up = Quaternion.AngleAxis(SpinDeg * e, fwd) * Up;
            if (Mathf.Abs(Vector3.Dot(up.normalized, fwd)) > 0.98f) up = Vector3.Cross(fwd, Vector3.right);
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, up) * Quaternion.Euler(0, 0, Mathf.Lerp(Roll0, Roll1, e)));
            float fov = Mathf.Lerp(Fov0, Fov1, e);
            if (FitW0 > 0f) { float w = Mathf.Lerp(FitW0, FitW1 > 0f ? FitW1 : FitW0, e); float d = Mathf.Max(0.1f, Vector3.Distance(pos, tgt)); fov = Mathf.Clamp(2f * Mathf.Atan(w / Mathf.Max(0.2f, cam.aspect) * 0.5f / d) * Mathf.Rad2Deg, 5f, 62f); }
            cam.fieldOfView = fov; CamPos = pos;
            if (Dof)
            {
                float focus = Mathf.Max(0.15f, Vector3.Dot(tgt - pos, fwd));
                if (RackFrom > 0) focus = Mathf.Lerp(RackFrom, focus, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.42f, u)));
                CineDof.Set(cam, true, focus, Aperture, Focal);
            }
            else CineDof.Off(cam);
        }
    }

    /// <summary>
    /// Turns a ShotSpec into a camera move that fits the real geometry: the subject's room (walls, ceiling), whatever stands
    /// between lens and subject (furniture, other people) — a blocked angle turns around the subject until it is clear,
    /// or the lens comes in closer.
    /// </summary>
    public static class CineSolver
    {
        static Session Ses => Session.I;

        public static Room RoomAt(Vector3 p)
        {
            var mv = Ses?.World?.Mansion; if (mv == null) return null; int id = mv.RoomAtWorld(p + Vector3.up * 0.3f);
            return id >= 0 && id < Ses.S.Layout.Rooms.Count ? Ses.S.Layout.Rooms[id] : null;
        }
        static float FloorY(Room r, Vector3 p) => r != null ? Ses.S.Layout.FloorY(r.Floor) : p.y;
        static float CeilY(Room r, Vector3 p) => r != null ? Ses.S.Layout.FloorY(r.Floor) + Mathf.Min(r.CeilingH, 7.5f) : p.y + 2.4f;

        /// <summary>Keep a lens inside the room (a margin off the walls, under the ceiling, above the floor).</summary>
        public static Vector3 KeepIn(Room r, Vector3 c, float margin = 0.35f)
        {
            if (r == null) return c;
            var R = r.Rect; float m = Mathf.Min(margin, Mathf.Min(R.W, R.D) * 0.3f);
            c.x = Mathf.Clamp(c.x, R.x0 + m, R.x1 - m); c.z = Mathf.Clamp(c.z, R.z0 + m, R.z1 - m);
            float fy = FloorY(r, c), cy = CeilY(r, c); c.y = Mathf.Clamp(c.y, fy + 0.08f, cy - 0.25f);
            return c;
        }

        /// <summary>Is the line from a to b free of walls/furniture/other people (the listed actors don't count)?</summary>
        public static bool Clear(Vector3 a, Vector3 b, ICollection<string> ignore, float radius = 0.07f)
        {
            var d = b - a; float len = d.magnitude; if (len < 0.05f) return true; d /= len;
            var hits = Physics.SphereCastAll(a, radius, d, len, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                var av = h.collider.GetComponentInParent<ActorView>(); if (av != null && ignore != null && ignore.Contains(av.Id)) continue;
                if (h.collider.GetComponentInParent<ItemTag>() != null) continue;
                if (h.distance <= 0f) continue;
                return false;
            }
            return true;
        }
        static float FreeDist(Vector3 a, Vector3 dir, float len, ICollection<string> ignore)
        {
            var hits = Physics.SphereCastAll(a, 0.07f, dir, len, ~0, QueryTriggerInteraction.Ignore); float best = len;
            foreach (var h in hits)
            {
                if (h.collider == null || h.distance <= 0f) continue;
                var av = h.collider.GetComponentInParent<ActorView>(); if (av != null && ignore != null && ignore.Contains(av.Id)) continue;
                if (h.collider.GetComponentInParent<ItemTag>() != null) continue;
                best = Mathf.Min(best, h.distance);
            }
            return best;
        }

        /// <summary>
        /// Find a lens position around 'focus' at distance 'dist', preferring horizontal direction 'dir' and elevation 'lift'
        /// (metres above the focus): tries turning left/right in widening steps. Returns false only if every angle is blocked
        /// (then the least bad one, pulled in, is used).
        /// </summary>
        public static Vector3 Place(Vector3 focus, Vector3 dir, float dist, float lift, Room room, ICollection<string> ignore, float minDist, out Vector3 usedDir)
            => Place(focus, dir, dist, lift, room, ignore, minDist, out usedDir, out _);

        /// <summary>As Place; 'ok' is false when no angle was fully clear (the caller may prefer a wider shot then).</summary>
        public static Vector3 Place(Vector3 focus, Vector3 dir, float dist, float lift, Room room, ICollection<string> ignore, float minDist, out Vector3 usedDir, out bool ok)
        {
            dir.y = 0; if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward; dir.Normalize();
            float[] turns = { 0, 18, -18, 36, -36, 55, -55, 75, -75, 100, -100, 130, -130, 160, -160, 180 };
            Vector3 best = focus + dir * minDist + Vector3.up * lift * (minDist / Mathf.Max(0.01f, dist)); float bestScore = -1; usedDir = dir; ok = false;
            foreach (var t in turns)
            {
                var d = Quaternion.AngleAxis(t, Vector3.up) * dir;
                var want = focus + d * dist + Vector3.up * lift;
                var c = KeepIn(room, want);
                var to = c - focus; float len = to.magnitude; if (len < 0.01f) continue;
                float free = FreeDist(focus, to / len, len, ignore);
                // the other way too: a hand touching a shelf starts inside its collider, which a cast from the hand never sees
                float back = FreeDist(c, -to / len, Mathf.Max(0.01f, len - 0.12f), ignore);
                if (back < len - 0.14f) free = Mathf.Min(free, len - back);
                float got = free >= len - 1e-3f ? len : Mathf.Min(len, free - 0.12f);   // nothing in the way = the whole length
                bool props = PropsClear(c, focus) && FurnitureClear(c, focus);   // lenses also respect furniture and decoration without colliders
                if (got >= len - 0.02f && len >= minDist * 0.95f && !Inside(c, ignore) && props) { usedDir = d; ok = true; return c; }
                // partial: remember the best fallback (most room, least turn)
                float score = Mathf.Max(0, got) - Mathf.Abs(t) * 0.004f;
                var pc = focus + to / len * got;
                if (got >= minDist && score > bestScore && !Inside(pc, ignore)) { bestScore = score; best = pc; usedDir = d; }
            }
            return best;
        }
        /// <summary>Is a lens point inside furniture/walls (anything solid but the listed actors and loose items)?</summary>
        /// <summary>
        /// Decoration has no colliders (shelves, lamps, draperies): for a close lens, test the sight line against the bounds of the
        /// room's smaller renderers as well. Conservative (boxes), so a blocked angle simply turns to another.
        /// </summary>
        public static bool PropsClear(Vector3 lens, Vector3 target)
        {
            var mv = Ses?.World?.Mansion; if (mv == null || mv.Rooms == null) return true;
            var d = target - lens; float len = d.magnitude; if (len < 0.1f) return true; var ray = new Ray(lens, d / len);
            int a = mv.RoomAtWorld(lens + Vector3.up * 0.05f), b = mv.RoomAtWorld(target);
            foreach (int id in new[] { a, b })
            {
                if (id < 0 || id >= mv.Rooms.Length || mv.Rooms[id] == null || (id == b && a == b && id != a)) continue;
                foreach (var r in mv.Rooms[id].Renderers)
                {
                    if (r == null || !r.enabled) continue; var bb = r.bounds; if (bb.size.x > 2.6f || bb.size.z > 2.6f || bb.size.y > 3.2f) continue;   // walls, floors, rugs: never in the way of a close lens
                    if (bb.Contains(target)) continue;                                                     // the thing we are looking at (a basin, a desk under a hand)
                    if (bb.Contains(lens)) return false;
                    if (bb.IntersectRay(ray, out float t) && t > 0.04f && t < len - 0.18f) return false;
                }
                if (a == b) break;
            }
            return true;
        }

        /// <summary>
        /// Furniture from the kernel layout (footprint W×D, height H, yaw) as boxes: a lens line through a wardrobe, a plant or a
        /// shelf is blocked even where the mesh has no collider or has been batched. The piece the subject is at does not count.
        /// </summary>
        public static bool FurnitureClear(Vector3 lens, Vector3 target)
        {
            var S = Ses?.S; var mv = Ses?.World?.Mansion; if (S?.Layout == null || mv == null) return true;
            int ra = mv.RoomAtWorld(lens + Vector3.up * 0.05f), rb = mv.RoomAtWorld(target);
            var d = target - lens; float len = d.magnitude; if (len < 0.1f) return true;
            foreach (var f in S.Layout.Furniture)
            {
                if (f == null || (f.Room != ra && f.Room != rb) || f.H < 0.5f) continue;
                var c = Ses.World.ToWorld(f.Pos) + Vector3.up * (f.H * 0.5f);
                var rot = Quaternion.Euler(0, f.Yaw, 0); var inv = Quaternion.Inverse(rot);
                var b = new Bounds(Vector3.zero, new Vector3(Mathf.Max(0.05f, f.W - 0.1f), Mathf.Max(0.05f, f.H - 0.05f), Mathf.Max(0.05f, f.D - 0.1f)));
                var lt = inv * (target - c); var ll = inv * (lens - c);
                if (b.SqrDistance(lt) < 0.25f * 0.25f) continue;              // the subject stands at / sits on / lies by it
                if (b.Contains(ll)) return false;
                var ray = new Ray(ll, (lt - ll).normalized);
                if (b.IntersectRay(ray, out float t) && t > 0.02f && t < len - 0.2f) return false;
            }
            return true;
        }

        /// <summary>Does the person's own body (their hit volumes) stand between a close lens and the part it frames?</summary>
        static bool SelfBlocked(string id, Vector3 cam, Vector3 target)
        {
            var v = CineAnchors.View(id); if (v == null) return false;
            var d = target - cam; float len = d.magnitude; if (len < 0.05f) return false; d /= len;
            foreach (var h in Physics.RaycastAll(cam, d, len, ~0, QueryTriggerInteraction.Collide))
            {
                if (h.collider == null || !h.collider.isTrigger) continue;
                var av = h.collider.GetComponentInParent<ActorView>(); if (av != v) continue;
                if ((h.point - target).magnitude > 0.22f) return true;   // something of theirs well before the framed part
            }
            return false;
        }
        /// <summary>Horizontal distance of a lens point from the person's body axis (a close lens must stay outside their clothes).</summary>
        static float AxisDist(string id, Vector3 p) { var r = CineAnchors.Root(id); return new Vector2(p.x - r.x, p.z - r.z).magnitude; }
        static bool Lying(string id) => CineAnchors.Lying(id);
        /// <summary>Is this point inside solid geometry (anything but people and loose items)?</summary>
        public static bool IsInside(Vector3 p) => Inside(p, null);
        static bool Inside(Vector3 p, ICollection<string> ignore)
        {
            foreach (var c in Physics.OverlapSphere(p, 0.09f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c == null) continue; var av = c.GetComponentInParent<ActorView>(); if (av != null && ignore != null && ignore.Contains(av.Id)) continue;
                if (c.GetComponentInParent<ItemTag>() != null) continue;
                return true;
            }
            return false;
        }

        static float Hash01(string s, int salt) { int h = salt * 7919; foreach (var ch in s ?? "") h = h * 31 + ch; h ^= h >> 13; return (h & 0xffff) / 65535f; }

        /// <summary>Solve a spec against the current pose of the scene (call after actors were posed at least one frame).</summary>
        public static CineShot Solve(ShotSpec s)
        {
            Physics.SyncTransforms();
            // only the subject may overlap the lens line (and the shoulder in an over-the-shoulder); anyone else in the way blocks it
            var ignore = new HashSet<string>(); if (s.Subject != null) ignore.Add(s.Subject); if (s.Other != null && (s.Kind == ShotKind.Over || s.Kind == ShotKind.Behind || s.Kind == ShotKind.Two)) ignore.Add(s.Other);
            string id = s.Subject; float side = s.Side >= 0 ? 1f : -1f; float push = Mathf.Max(0f, s.Push);
            var sh = new CineShot { Spec = s };
            var fwd = CineAnchors.Forward(id);
            Vector3 P(Vector3 v) => v;
            switch (s.Kind)
            {
                case ShotKind.Establish:
                    {
                        var focus = s.Point ?? CineAnchors.Body(id); var room = RoomAt(focus);
                        Vector3 cam0 = focus + new Vector3(4, 2.2f, 4);
                        if (room != null)
                        {
                            var R = room.Rect; float fy = FloorY(room, focus), cy = CeilY(room, focus);
                            float h = Mathf.Min(cy - 0.45f, fy + 3.1f);
                            float best = -1;
                            foreach (var (cx, cz) in new[] { (R.x0, R.z0), (R.x1, R.z0), (R.x0, R.z1), (R.x1, R.z1) })
                            {
                                var corner = KeepIn(room, new Vector3(cx, h, cz), 0.5f);
                                var to = corner - focus; to.y = 0; float dist = to.magnitude;
                                if (dist > 11f) corner = focus + to / dist * 11f + Vector3.up * (h - focus.y);
                                bool clear = Clear(focus, corner, ignore, 0.05f);
                                float front = Vector3.Dot(to.normalized, fwd);
                                float score = (clear ? 10f : 0f) + Mathf.Min(dist, 9f) * 0.5f + front * 1.2f;
                                if (score > best) { best = score; cam0 = corner; }
                            }
                        }
                        var dirIn = focus - cam0; dirIn.y = 0; dirIn.Normalize();
                        sh.Base = focus; sh.Follow = false; sh.Target = () => focus;
                        sh.O0 = cam0 - focus + Vector3.up * 0.35f; sh.O1 = cam0 - focus + dirIn * (0.9f * push) - Vector3.up * 0.15f;
                        sh.L0 = Vector3.up * -0.3f; sh.L1 = Vector3.up * 0.05f;
                        sh.Fov0 = 54; sh.Fov1 = 50; sh.Dof = false;
                        break;
                    }
                case ShotKind.Medium:
                case ShotKind.Orbit:
                    {
                        var head = CineAnchors.Head(id); var focus = head + Vector3.down * 0.28f; var room = RoomAt(focus);
                        var dir = Quaternion.AngleAxis(side * (s.Kind == ShotKind.Orbit ? 55f : 32f), Vector3.up) * fwd;
                        float dist = s.Kind == ShotKind.Orbit ? 2.5f : 2.25f;
                        var c = Place(focus, dir, dist, 0.1f, room, ignore, 1.0f, out var used);
                        var off = c - focus; var right = Vector3.Cross(Vector3.up, -used).normalized;
                        sh.Target = () => CineAnchors.Head(id) + Vector3.down * 0.28f; sh.Base = focus; sh.Follow = true;
                        sh.O0 = off; sh.O1 = s.Kind == ShotKind.Orbit ? off : off * Mathf.Lerp(1f, 0.8f, push);
                        // off-centre: the subject on a third, looking into the open side of the frame
                        var bias = right * (-side * 0.22f * off.magnitude * 0.45f);
                        sh.L0 = bias + Vector3.up * 0.1f; sh.L1 = bias * 0.8f + Vector3.up * 0.12f;
                        sh.Fov0 = 36; sh.Fov1 = 33; sh.Dof = true; sh.Aperture = 3.2f; sh.Focal = 55f;
                        if (s.Kind == ShotKind.Orbit) { sh.OrbitDeg = -side * 42f; sh.Fov0 = 38; sh.Fov1 = 36; }
                        break;
                    }
                case ShotKind.Over:
                    {
                        string other = s.Other ?? id; var head = CineAnchors.Head(id); var oh = CineAnchors.Head(other); var room = RoomAt(head);
                        var d = head - oh; float vsep = Mathf.Abs(d.y); d.y = 0; float sep = d.magnitude;
                        // the other one has to be right there for a shoulder shot; otherwise the face alone, from their side
                        if (other == id || sep < 0.6f || sep > 3.6f || vsep > 1.5f || RoomAt(oh) != room) return Solve(new ShotSpec(ShotKind.Face, id, null, s.Side) { Push = s.Push, Dutch = s.Dutch });
                        d /= sep;
                        var right = Vector3.Cross(Vector3.up, d).normalized;
                        var cam = oh - d * 0.62f + right * (0.36f * side) + Vector3.up * 0.06f;
                        cam = KeepIn(room ?? RoomAt(oh), cam);
                        if (!Clear(head, cam, ignore) || Inside(cam, ignore)) cam = Place(head, (cam - head), Mathf.Max(1.2f, sep + 0.6f), 0.08f, room, ignore, 0.9f, out _);
                        // they are not facing each other (the recording keeps where bodies point): the back of a head says nothing
                        { var tc = cam - head; tc.y = 0; var ffh = CineAnchors.FaceFwd(id); ffh.y = 0; if (tc.sqrMagnitude > 1e-3f && ffh.sqrMagnitude > 1e-3f && Vector3.Dot(tc.normalized, ffh.normalized) < 0.15f) return Solve(new ShotSpec(ShotKind.Face, id, null, s.Side) { Push = s.Push, Dutch = s.Dutch }); }
                        sh.Target = () => CineAnchors.Head(id); sh.Base = head; sh.Follow = true;
                        sh.O0 = cam - head; sh.O1 = (cam - head) * Mathf.Lerp(1f, 0.9f, push) + Vector3.up * -0.02f;
                        var bias = right * (side * 0.12f);
                        sh.L0 = bias - Vector3.up * 0.04f; sh.L1 = bias - Vector3.up * 0.03f;
                        sh.Fov0 = 32; sh.Fov1 = 29; sh.Dof = true; sh.Aperture = 2.2f; sh.Focal = 60f; sh.RackFrom = s.Rack ? 0.55f : -1f;
                        break;
                    }
                case ShotKind.Low:
                    {
                        // from low in front, looking up: placed from the head down to ~0.6 m so the whole sight line is checked
                        var head = CineAnchors.Head(id); var root = CineAnchors.Root(id); var room = RoomAt(head);
                        var dir = Quaternion.AngleAxis(side * 28f, Vector3.up) * fwd;
                        float fy = FloorY(room, root); float lensY = fy + 0.62f; float drop = lensY - head.y;
                        var c = Place(head, dir, 2.0f, drop, room, ignore, 1.1f, out var used, out bool okL);
                        if (!okL) return Solve(new ShotSpec(ShotKind.Medium, id, s.Other, s.Side) { Push = s.Push });
                        sh.Target = () => CineAnchors.Head(id); sh.Base = head; sh.Follow = true; sh.FollowTime = 0.5f;
                        sh.O0 = c - head; sh.O1 = (c - head) * Mathf.Lerp(1f, 0.8f, push); sh.O1.y = sh.O0.y + 0.08f;
                        var right = Vector3.Cross(Vector3.up, -used).normalized;
                        sh.L0 = Vector3.up * 0.02f + right * (-side * 0.1f); sh.L1 = Vector3.down * 0.02f;
                        sh.Fov0 = 42; sh.Fov1 = 36; sh.Dof = true; sh.Aperture = 4f; sh.Focal = 45f;
                        if (s.Dutch) { sh.Roll0 = side * 6f; sh.Roll1 = side * 9f; }
                        break;
                    }
                case ShotKind.Top:
                    {
                        var focus = s.Point ?? CineAnchors.Body(id); var room = RoomAt(focus);
                        float cy = CeilY(room, focus); float h = Mathf.Min(cy - 0.35f, focus.y + 3.4f); h = Mathf.Max(h, focus.y + 1.6f);
                        var c = KeepIn(room, new Vector3(focus.x, h, focus.z) + fwd * 0.25f);
                        if (!Clear(focus, c, ignore)) { float free = FreeDist(focus, (c - focus).normalized, (c - focus).magnitude, ignore); c = focus + (c - focus).normalized * Mathf.Max(1.2f, free - 0.15f); }
                        sh.Target = () => s.Point ?? CineAnchors.Body(id); sh.Base = focus; sh.Follow = s.Point == null;
                        sh.O0 = c - focus; sh.O1 = (c - focus) * Mathf.Lerp(1f, 0.82f, push);
                        sh.L0 = Vector3.zero; sh.L1 = Vector3.zero; sh.Up = fwd; sh.SpinDeg = side * 26f;
                        sh.Fov0 = 50; sh.Fov1 = 45; sh.Dof = true; sh.Aperture = 5.6f; sh.Focal = 40f;
                        break;
                    }
                case ShotKind.Hand:
                    {
                        var hand = CineAnchors.Hand(id); var room = RoomAt(hand);
                        bool left = Cast.LeftHanded(id);
                        var sideDir = CineAnchors.View(id) != null ? CineAnchors.View(id).transform.right * (left ? -1 : 1) : Vector3.right;
                        var dir = (sideDir * 0.8f + fwd * 0.9f); dir = Quaternion.AngleAxis(side * 12f, Vector3.up) * dir;
                        Vector3 c = hand; bool okH = false;
                        foreach (var (hd, hl) in new[] { (0.6f, 0.16f), (0.45f, 0.3f), (0.38f, 0.46f) }) { c = Place(hand, dir, hd, hl, room, ignore, 0.28f, out _, out okH); if (okH) break; }   // closer and from above before giving up
                        if (okH && (AxisDist(id, c) < 0.36f || SelfBlocked(id, c, hand))) okH = false;
                        if (!okH) return Solve(new ShotSpec(ShotKind.Medium, id, s.Other, s.Side) { Push = s.Push });   // no clean angle on the hand: the person instead
                        sh.Target = () => CineAnchors.Hand(id); sh.Base = hand; sh.Follow = true; sh.FollowTime = 0.22f;
                        sh.O0 = c - hand; sh.O1 = (c - hand) * Mathf.Lerp(1f, 0.74f, push);
                        sh.L0 = Vector3.zero; sh.L1 = Vector3.zero;
                        sh.Fov0 = 32; sh.Fov1 = 28; sh.FitW0 = 0.62f; sh.FitW1 = 0.46f; sh.Dof = true; sh.Aperture = 1.6f; sh.Focal = 75f;
                        break;
                    }
                case ShotKind.Eyes:
                case ShotKind.Face:
                    {
                        // along the face itself (the head turns, bows, lies back), a little off-axis
                        // someone lying down (fallen, asleep, dead): the face from close by reads as a smear of cloth — look down on them instead
                        if (Lying(id)) return Solve(new ShotSpec(ShotKind.Top, id, null, s.Side) { Push = 1.5f });
                        bool eyes = s.Kind == ShotKind.Eyes; float dist = eyes ? 0.62f : 1.0f;
                        var head = CineAnchors.Head(id); var room = RoomAt(head); var ff = CineAnchors.FaceFwd(id);
                        var hz = new Vector3(ff.x, 0, ff.z); Vector3 c; bool ok;
                        if (hz.magnitude > 0.4f)
                        {
                            var dir = Quaternion.AngleAxis(side * (eyes ? 20f : 28f), Vector3.up) * hz.normalized;
                            float lift = Mathf.Clamp(ff.y, eyes ? -0.12f : -0.2f, 0.45f) * dist + (eyes ? 0f : -0.05f);   // never from under the chin (inside the collar)
                            c = Place(head, dir, dist, lift, room, ignore, eyes ? 0.35f : 0.5f, out _, out ok);
                        }
                        else
                        {
                            // face up (lying on the back) or down: from above/below along the face, turned a little
                            c = KeepIn(room, head + ff * dist + Quaternion.AngleAxis(side * 30f, Vector3.up) * fwd * 0.18f);
                            ok = Clear(head, c, ignore) && !Inside(c, ignore);
                        }
                        if (ok && ((!Lying(id) && AxisDist(id, c) < 0.4f) || SelfBlocked(id, c, head))) ok = false;   // a lens inside the person's own coat, or their arm in the way
                        if (!ok) return Solve(new ShotSpec(ShotKind.Medium, id, s.Other, s.Side) { Push = s.Push });
                        sh.Target = () => CineAnchors.Head(id); sh.Base = head; sh.Follow = true; sh.FollowTime = 0.3f;
                        sh.O0 = c - head; sh.O1 = (c - head) * Mathf.Lerp(1f, eyes ? 0.86f : 0.82f, push);
                        if (eyes) { sh.L0 = Vector3.up * 0.005f; sh.L1 = sh.L0; sh.Fov0 = 24; sh.Fov1 = 21; sh.FitW0 = 0.5f; sh.FitW1 = 0.4f; sh.Dof = true; sh.Aperture = 2f; sh.Focal = 70f; }
                        else
                        {
                            var right = Vector3.Cross(Vector3.up, -(c - head)).normalized;
                            sh.L0 = right * (-side * 0.07f) - Vector3.up * 0.03f; sh.L1 = right * (-side * 0.06f) - Vector3.up * 0.02f;
                            sh.Fov0 = 30; sh.Fov1 = 27; sh.FitW0 = 0.78f; sh.FitW1 = 0.62f; sh.Dof = true; sh.Aperture = 2.4f; sh.Focal = 65f;
                            if (s.Dutch) { sh.Roll0 = -side * 5f; sh.Roll1 = -side * 6f; }
                        }
                        break;
                    }
                case ShotKind.Feet:
                    {
                        var feet = CineAnchors.Feet(id); var room = RoomAt(feet + Vector3.up * 0.5f);
                        var fy = FloorY(room, feet);
                        var dir = Quaternion.AngleAxis(side * 62f, Vector3.up) * fwd;
                        var ground = new Vector3(feet.x, fy + 0.14f, feet.z);
                        var c = Place(ground, dir, 1.15f, 0f, room, ignore, 0.6f, out _); c.y = fy + 0.13f;
                        sh.Target = () => { var f = CineAnchors.Feet(id); return new Vector3(f.x, fy + 0.14f, f.z); }; sh.Base = ground; sh.Follow = true; sh.FollowTime = 0.25f;
                        sh.O0 = c - ground; sh.O1 = (c - ground) * Mathf.Lerp(1f, 0.85f, push);
                        sh.L0 = fwd * 0.25f + Vector3.up * 0.08f; sh.L1 = fwd * 0.3f + Vector3.up * 0.1f;
                        sh.Fov0 = 40; sh.Fov1 = 36; sh.Dof = true; sh.Aperture = 2.4f; sh.Focal = 50f;
                        break;
                    }
                case ShotKind.Thing:
                    {
                        var p = s.Point ?? CineAnchors.Hand(id); var room = RoomAt(p);
                        var toCentre = room != null ? new Vector3(room.Rect.CX, p.y, room.Rect.CZ) - p : -fwd; toCentre.y = 0;
                        if (toCentre.sqrMagnitude < 0.01f) toCentre = Vector3.forward;
                        var dir = Quaternion.AngleAxis(side * 25f, Vector3.up) * toCentre.normalized;
                        var c = Place(p, dir, 0.75f, 0.42f, room, ignore, 0.35f, out _, out bool okT);
                        if (!okT) { if (id != null && CineAnchors.View(id) != null) return Solve(new ShotSpec(ShotKind.Medium, id, s.Other, s.Side)); return Solve(new ShotSpec(ShotKind.Establish, id) { Point = p }); }
                        sh.Target = () => p; sh.Base = p; sh.Follow = false;
                        sh.O0 = c - p; sh.O1 = (c - p) * Mathf.Lerp(1f, 0.7f, push);
                        sh.Fov0 = 34; sh.Fov1 = 29; sh.FitW0 = 0.8f; sh.FitW1 = 0.55f; sh.Dof = true; sh.Aperture = 1.8f; sh.Focal = 70f;
                        break;
                    }
                case ShotKind.Two:
                    {
                        string other = s.Other; var a = CineAnchors.Head(id); var b = other != null ? CineAnchors.Head(other) : a + fwd;
                        var dab = b - a; float sepY = Mathf.Abs(dab.y); dab.y = 0; float sep = dab.magnitude;
                        if (other == null || sep > 6f || sepY > 2.2f) return Solve(new ShotSpec(ShotKind.Low, id, other, s.Side) { Dutch = s.Dutch, Push = s.Push });
                        var mid = (a + b) * 0.5f + Vector3.down * 0.35f; var room = RoomAt(mid);
                        var n = sep > 0.05f ? Vector3.Cross(Vector3.up, dab / sep) : Vector3.Cross(Vector3.up, fwd);
                        if (Vector3.Dot(n, CineAnchors.FaceFwd(id)) < 0) n = -n;   // the attacker's face side
                        float dist = Mathf.Max(1.7f, sep * 1.1f + 1.3f);
                        var c = Place(mid, n * side, dist, -0.45f, room, ignore, 1.0f, out var used, out bool okT2);
                        if (!okT2) return Solve(new ShotSpec(ShotKind.Low, id, other, s.Side) { Dutch = s.Dutch, Push = s.Push });
                        sh.Target = () => ((CineAnchors.Head(id) + (other != null ? CineAnchors.Head(other) : CineAnchors.Head(id))) * 0.5f + Vector3.down * 0.35f); sh.Base = mid; sh.Follow = true; sh.FollowTime = 0.4f;
                        sh.O0 = c - mid; sh.O1 = (c - mid) * Mathf.Lerp(1f, 0.82f, push);
                        sh.L0 = Vector3.up * 0.08f; sh.L1 = Vector3.up * 0.05f;
                        sh.Fov0 = 44; sh.Fov1 = 40; sh.FitW0 = sep + 1.5f; sh.FitW1 = sep + 1.1f; sh.Dof = true; sh.Aperture = 4f; sh.Focal = 45f;
                        if (s.Dutch) { sh.Roll0 = side * 7f; sh.Roll1 = side * 10f; }
                        break;
                    }
                case ShotKind.Behind:
                    {
                        var head = CineAnchors.Head(id); var room = RoomAt(head);
                        Vector3 look = s.Other != null ? CineAnchors.Head(s.Other) : head + fwd * 4f;
                        var d = look - head; d.y = 0; if (d.sqrMagnitude < 0.01f) d = fwd; d.Normalize();
                        var right = Vector3.Cross(Vector3.up, d).normalized;
                        var want = head - d * 1.25f + right * (0.34f * side) + Vector3.up * 0.12f;
                        var c = KeepIn(room, want); if (!Clear(head, c, ignore)) c = Place(head, -d, 1.2f, 0.12f, room, ignore, 0.7f, out _);
                        sh.Target = () => CineAnchors.Head(id); sh.Base = head; sh.Follow = true; sh.FollowTime = 0.45f;
                        sh.O0 = c - head; sh.O1 = (c - head) * Mathf.Lerp(1f, 0.85f, push);
                        var lk = d * 2.6f + Vector3.down * 0.15f; sh.L0 = lk; sh.L1 = lk;
                        sh.Fov0 = 42; sh.Fov1 = 39; sh.Dof = true; sh.Aperture = 3.2f; sh.Focal = 40f; sh.RackFrom = -1f;
                        break;
                    }
            }
            return sh;
        }

        /// <summary>Where a key light and a rim light should sit for this shot (the subject lit from the lens side, edged from behind).</summary>
        public static (Vector3 key, Vector3 rim, Vector3 subject) LightsFor(CineShot sh, Vector3 camPos)
        {
            var subj = sh.Target != null ? sh.Target() : sh.Base;
            var toCam = camPos - subj; toCam.y = 0; if (toCam.sqrMagnitude < 1e-4f) toCam = Vector3.forward; toCam.Normalize();
            var right = Vector3.Cross(Vector3.up, toCam);
            var key = subj + toCam * 1.6f + right * 0.9f + Vector3.up * 0.8f;
            var rim = subj - toCam * 0.8f - right * 0.4f + Vector3.up * 0.45f;
            return (key, rim, subj);
        }
    }
}
