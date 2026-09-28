using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BL23.Game.Characters;
using BL23.Game.Cinema;
using BL23.Sim;
using UnityEngine;
using Pose = BL23.Sim.Pose;
using BodyRegion = BL23.Sim.BodyRegion;

namespace BL23.Game
{
    /// <summary>
    /// The shot planner: which details 민혁's eyes go to, from what is really in the room. Every detail must be visible from
    /// where he stands; every lens sits on his line of sight to it (a little closer), inside the room, with nothing in the way
    /// and nothing of his own body in frame. Deterministic per victim and loop (presentation only), and every decision and
    /// rejection is written to <see cref="LastPlanLog"/>.
    /// </summary>
    public sealed partial class DiscoveryFilm
    {
        sealed class Shot
        {
            public string Name; public int Score, Cat;     // Cat: 0 extremity (hand, feet) · 1 blood (pool, spray) · 2 weapon / struggle · 3 the worst images
            public Vector3 P, Look, Lens, KeyPos; public Func<Vector3> Target;
            public float Dur = 1.4f, Aperture = 2.2f, Roll, FitW0 = 0.6f, FitW1 = 0.48f;
            public bool Low, Dutch, Moth, PoolKey, Rim, WorstOk;
            public int WorstRank = 9;                        // 0 piece (Gore 2) · 1 veil · 2 pool
            public string ItemId;
            public string Witness;                           // i_witness: whose face (they flinch as the cut lands on them)
            public override string ToString() => $"{Name} s{Score} P{P:F2} lens{Lens:F2} ap{Aperture:0.0}{(Low ? " low" : "")}{(Dutch ? " dutch" : "")}";
        }

        readonly List<Shot> _plan = new List<Shot>();
        readonly StringBuilder _plog = new StringBuilder();
        System.Random _rng;
        readonly HashSet<string> _ignoreItems = new HashSet<string>();
        int _side = 1;

        static int Fnv(string s) { unchecked { uint h = 2166136261; foreach (char c in s ?? "") { h ^= c; h *= 16777619; } return (int)(h & 0x7fffffff); } }
        float Rnd(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
        void PLog(string s) => _plog.AppendLine(s);

        void Plan()
        {
            _plan.Clear(); _plog.Clear();
            _rng = new System.Random(Fnv(_victim + "|" + S.Loop));
            PLog($"plan victim={_victim} loop={S.Loop} room={_room} ({S.RoomName(_room)}) full={_full} gore={Settings.Gore} eye={_eye0:F2} focus={_focus:F2}");
            var pieces = PiecesOf(_victim);
            _ignoreItems.Clear(); foreach (var p in pieces) if (p.ItemId != null) _ignoreItems.Add(p.ItemId);
            var hot = Hotspots(_victim);
            PLog($"  hotspots {hot.Count}: " + string.Join(", ", hot.Select(h => $"{h.Kind}@{h.Pos:F1} w{h.Weight:0.0}")) + $" · pieces {pieces.Count}: " + string.Join(", ", pieces.Select(p => p.Part + (p.Hidden ? "(hidden)" : ""))));
            double death = _inc != null && _inc.DeathClock >= 0 ? _inc.DeathClock : _v.Body.DeathClock >= 0 ? _v.Body.DeathClock : S.Clock;
            var traces = S.Traces.Where(t => t.Room == _room && !t.Cleaned && t.Clock >= death - 90).ToList();
            PLog($"  traces {traces.Count}: " + string.Join(", ", traces.Select(t => t.Type)));

            var cands = new List<Shot>();
            void Try(Action a, string what) { try { a(); } catch (Exception e) { PLog($"  ! {what}: {e.Message}"); } }
            Try(() => AddPieces(cands, pieces), "pieces");
            Try(() => AddHand(cands, pieces, hot), "hand");
            Try(() => AddVeil(cands, pieces), "veil");
            Try(() => AddFeet(cands, pieces), "feet");
            Try(() => AddPool(cands, hot, traces), "pool");
            Try(() => AddSpray(cands, hot, traces), "spray");
            Try(() => AddStruggle(cands, hot, traces), "struggle");
            Try(() => AddWeapon(cands), "weapon");
            Try(() => AddWitness(cands), "witness");
            if (cands.Count < 2) Try(() => AddGazeFallback(cands), "gaze");
            Select(cands);
            PLog("  plan: " + (_plan.Count == 0 ? "(none)" : string.Join(" → ", _plan.Select(s => $"{s.Name} {s.Dur:0.0}s"))));
            LastPlanLog = _plog.ToString();
        }

        // ------------------------------------------------------------------ candidates
        void AddPieces(List<Shot> c, List<FilmPiece> pieces)
        {
            int gore = Settings.Gore;
            if (gore == 0) { if (pieces.Count > 0) PLog("  i_piece: skipped (Gore 0)"); return; }
            int n = 0;
            // the head first (it is the worst image there is), then the nearest
            foreach (var p in pieces.Where(p => !p.Hidden && p.T != null).OrderBy(p => p.Part == SeverPart.Head ? 0 : 1).ThenBy(p => Vector3.Distance(_eye0, p.Center)))
            {
                if (n >= 2) break;
                bool head = p.Part == SeverPart.Head;
                var s = new Shot { Name = "i_piece_" + p.Part, Score = head ? 11 : 10, Cat = 3, P = p.Center, Look = p.Center, ItemId = p.ItemId, FitW0 = head ? 0.46f : 0.52f, FitW1 = head ? 0.36f : 0.4f, Aperture = head ? 1.8f : Rnd(1.8f, 2.4f), WorstOk = gore == 2, WorstRank = head ? -1 : 0 };
                var t = p.T; var off = p.Center - t.position; s.Target = () => t != null ? t.position + off : s.P;
                if (!SeenFromEye(s.P, s.ItemId)) { PLog($"  reject {s.Name}: not visible from the eye"); continue; }
                float el = head ? 58f : 40f;
                bool cap = p.CapNormal.sqrMagnitude > 0.01f;
                // Gore 2: the lens on the side the cut faces (the head: from behind and above, the hair sharp and the neck's cut
                // at the edge of the frame, never straight into it); Gore 1: the covered side (only the hand, foot or hair shows)
                Func<Vector3, bool> sideOk = lens => !cap || (gore == 2
                    ? (head ? Vector3.Dot(p.CapNormal.normalized, (s.P - lens).normalized) < 0.35f : Vector3.Dot(p.CapNormal.normalized, (s.P - lens).normalized) < -0.2f)
                    : Vector3.Dot(p.CapNormal.normalized, (s.P - lens).normalized) > 0.2f);
                if (SolveAround(s, head ? 0.5f : 0.55f, el, sideOk)) { c.Add(s); n++; }
            }
        }

        void AddHand(List<Shot> c, List<FilmPiece> pieces, List<FilmHotspot> hot)
        {
            var rig = _body?.Rig; if (rig == null) return;
            var severed = new HashSet<SeverPart>(pieces.Select(p => p.Part));
            float Blood(bool left)
            {
                float sum = 0f;
                foreach (var w in _v.Body.Wounds)
                    if (left ? (w.Region == BodyRegion.HandL || w.Region == BodyRegion.ArmL || w.Region == BodyRegion.ShoulderL) : (w.Region == BodyRegion.HandR || w.Region == BodyRegion.ArmR || w.Region == BodyRegion.ShoulderR)) sum += w.Sev;
                return sum;
            }
            // reaching: toward the nearest door or the pool
            var goals = new List<Vector3>();
            foreach (var h in hot) if (h.Kind == GoreKind.Pool) goals.Add(h.Pos);
            foreach (var d in S.Layout.Doors) if (d.RoomA == _room || d.RoomB == _room) goals.Add(_s.World.ToWorld(d.Pos));
            float Reach(Vector3 p) { float best = 99f; foreach (var g in goals) best = Mathf.Min(best, Vector3.Distance(p, g)); return best; }
            Shot best = null; float bestScore = float.MinValue;
            foreach (bool left in new[] { true, false })
            {
                if (severed.Contains(left ? SeverPart.HandL : SeverPart.HandR) || severed.Contains(left ? SeverPart.ArmL : SeverPart.ArmR)) continue;
                var hb = left ? HBone.HandL : HBone.HandR; if (!rig.HasBone(hb)) continue;
                var bone = rig.Bone(hb); var anchor = left ? (rig.HandAnchorL ?? bone) : (rig.HandAnchorR ?? bone);
                var p = Vector3.Lerp(bone.position, anchor.position, 0.5f);
                float score = Blood(left) * 2f - Reach(p) * 0.5f;
                if (!SeenFromEye(p, null)) { PLog($"  hand {(left ? "L" : "R")}: not visible from the eye"); continue; }
                if (score <= bestScore) continue;
                var bt = bone; var at = anchor;
                best = new Shot { Name = "i_hand", Score = 9, Cat = 0, P = p, Look = p, Target = () => bt != null && at != null ? Vector3.Lerp(bt.position, at.position, 0.5f) : p, FitW0 = 0.42f, FitW1 = 0.34f, Aperture = Rnd(1.8f, 2.2f), Moth = true };
                bestScore = score;
            }
            if (best == null) return;
            if (SolveGaze(best, 0.45f, _floorY + 0.12f)) c.Add(best);
        }

        void AddVeil(List<Shot> c, List<FilmPiece> pieces)
        {
            if (pieces.Any(p => p.Part == SeverPart.Head)) return;   // the head piece is filmed as a piece, from the back
            var rig = _body?.Rig; if (rig == null || rig.Head == null) return;
            // the hair over the temple is in focus; the face beyond it, turned a little away, stays soft: a cheek, an ear, an
            // eyelid through the strands — never the whole face
            var hc = rig.HeadCenterWorld(); var face = rig.EyeWorld() - hc; face = face.sqrMagnitude > 1e-6f ? face.normalized : Vector3.forward;
            var side = Vector3.Cross(Vector3.up, face); side = side.sqrMagnitude > 1e-4f ? side.normalized : Vector3.right;
            if (Vector3.Dot(side, _eye0 - hc) < 0f) side = -side;   // his side of the head
            Vector3 P = hc + side * 0.07f + Vector3.up * 0.03f;
            var s = new Shot { Name = "i_veil", Score = 9, Cat = 3, P = P, Look = P, FitW0 = 0.42f, FitW1 = 0.34f, Aperture = 1.8f, Rim = true, WorstOk = true, WorstRank = 1 };
            var hb = rig.Head; var offL = hb.InverseTransformPoint(P); s.Target = () => hb != null ? hb.TransformPoint(offL) : P;
            if (!SeenFromEye(P, null)) { PLog("  reject i_veil: head not visible from the eye"); return; }
            // from the side and a little behind the face, 30–45° up
            foreach (float turn in new[] { 20f, -20f, 45f, -45f, 0f, 70f, -70f })
            {
                var d = (Quaternion.AngleAxis(turn, Vector3.up) * (side * 0.85f - face * 0.35f)).normalized;
                float el = turn == 0f ? 45f : 35f;
                s.Lens = CineSolver.KeepIn(S.Layout.Room(_room), P + (d * Mathf.Cos(el * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(el * Mathf.Deg2Rad)) * 0.48f, 0.2f);
                if (Validate(s)) { Finish(s); c.Add(s); return; }
            }
        }

        void AddFeet(List<Shot> c, List<FilmPiece> pieces)
        {
            if (_v.Pose != Pose.LieFront || _body?.Rig == null) return;
            if (pieces.Count(p => p.Part == SeverPart.LegL || p.Part == SeverPart.LegR) >= 2) return;
            var f = CineAnchors.Feet(_victim); var P = new Vector3(f.x, _floorY + 0.1f, f.z);
            var s = new Shot { Name = "i_feet", Score = 5, Cat = 0, P = P, Look = P, FitW0 = 0.7f, FitW1 = 0.56f, Aperture = 2.4f, Low = true };
            if (!SeenFromEye(P, null)) { PLog("  reject i_feet: not visible"); return; }
            var h = _eye0 - P; h.y = 0; h = h.sqrMagnitude > 1e-4f ? h.normalized : Vector3.forward;
            foreach (float turn in new[] { 0f, 20f, -20f, 40f, -40f })
            {
                var d = Quaternion.AngleAxis(turn, Vector3.up) * h;
                s.Lens = CineSolver.KeepIn(S.Layout.Room(_room), P + d * 0.75f, 0.2f); s.Lens.y = _floorY + 0.14f;
                if (Validate(s)) { Finish(s); c.Add(s); return; }
            }
        }

        void AddPool(List<Shot> c, List<FilmHotspot> hot, List<Trace> traces)
        {
            Vector3 centre; float radius;
            var hp = hot.Where(h => h.Kind == GoreKind.Pool).OrderByDescending(h => h.Size).ToList();
            if (hp.Count > 0) { centre = hp[0].Pos; radius = Mathf.Max(0.2f, hp[0].Size); }
            else
            {
                var t = traces.Where(x => x.Type == "BloodPool").OrderByDescending(x => x.Size).FirstOrDefault(); if (t == null) return;
                centre = _s.World.ToWorld(t.Pos); radius = Mathf.Clamp(t.Size, 0.25f, 1.1f);
            }
            centre.y = _floorY;
            var h = _eye0 - centre; h.y = 0; h = h.sqrMagnitude > 1e-4f ? h.normalized : Vector3.forward;
            var s = new Shot { Name = "i_pool", Score = 8, Cat = 1, Low = true, PoolKey = true, FitW0 = 0.9f, FitW1 = 0.75f, Aperture = 2.4f, WorstOk = true, WorstRank = 2 };
            // the near rim, seen grazing (5–10°) from 0.25 m above the floor: the wet skin of it, the drying edge (then a
            // little higher and closer, before giving up: furniture legs and the body itself crowd the floor)
            float hd = Mathf.Clamp(0.25f / Mathf.Tan(7.5f * Mathf.Deg2Rad), 0.9f, 1.6f);
            foreach (var (lh, dk) in new[] { (0.25f, 1f), (0.25f, 0.7f), (0.38f, 0.8f), (0.5f, 0.6f) })
                foreach (float turn in new[] { 0f, 20f, -20f, 40f, -40f, 70f, -70f })
                {
                    var d = Quaternion.AngleAxis(turn, Vector3.up) * h;
                    s.P = centre + d * radius * 0.85f; s.P.y = _floorY + 0.01f;
                    s.Look = s.P + (centre - s.P) * 0.3f + Vector3.up * (_dropAt.HasValue ? 0.08f : 0.03f);
                    if (!SeenFromEye(s.P, null)) continue;
                    s.Lens = CineSolver.KeepIn(S.Layout.Room(_room), s.P + d * hd * dk, 0.2f); s.Lens.y = _floorY + lh;
                    if (Validate(s)) { Finish(s); c.Add(s); return; }
                }
            PLog("  reject i_pool: no clear grazing angle");
        }

        void AddSpray(List<Shot> c, List<FilmHotspot> hot, List<Trace> traces)
        {
            var walls = hot.Where(h => (h.Kind == GoreKind.Arterial || h.Kind == GoreKind.Impact || h.Kind == GoreKind.CastOff) && Mathf.Abs(h.Normal.y) < 0.7f)
                           .OrderByDescending(h => h.Weight + (h.Kind == GoreKind.Arterial ? 1f : 0f)).ToList();
            var spots = walls.Select(w => (w.Pos, w.Normal, Mathf.Clamp(w.Size * 2.2f, 0.5f, 1.2f))).ToList();
            if (spots.Count == 0)
                foreach (var t in traces.Where(x => x.Type == "BloodSpray"))
                {
                    // the kernel's spray trace: the wall it reached, along its bearing
                    var o = _s.World.ToWorld(t.Pos) + Vector3.up * 1.2f; var dir = new Vector3(Mathf.Sin(t.Dir * Mathf.Deg2Rad), 0f, Mathf.Cos(t.Dir * Mathf.Deg2Rad));
                    foreach (var hit in Physics.RaycastAll(o - dir * 0.6f, dir, 3f, ~0, QueryTriggerInteraction.Ignore).OrderBy(x => x.distance))
                    {
                        if (Ignorable(hit.collider, null)) continue;
                        spots.Add((hit.point, hit.normal, 0.8f)); break;
                    }
                }
            foreach (var (pos, n, w) in spots)
            {
                if (Vector3.Dot(n, _eye0 - pos) <= 0f) { PLog("  reject i_spray: the wall faces away"); continue; }
                var s = new Shot { Name = "i_spray", Score = 8, Cat = 1, P = pos, Look = pos, FitW0 = w, FitW1 = w * 0.82f, Aperture = Rnd(2.2f, 2.8f), Dutch = true, Roll = (_rng.Next(2) == 0 ? -8f : 8f) };
                if (!SeenFromEye(pos, null)) { PLog("  reject i_spray: not visible from the eye"); continue; }
                float dist = Rnd(0.5f, 0.8f);
                var right = Vector3.Cross(Vector3.up, n); right = right.sqrMagnitude > 1e-4f ? right.normalized : Vector3.right;
                foreach (float lat in new[] { 0.1f * _side, -0.1f * _side, 0f })
                {
                    s.Lens = CineSolver.KeepIn(S.Layout.Room(_room), pos + n * dist + right * lat, 0.2f);
                    if (Validate(s)) { _side = -_side; Finish(s); c.Add(s); return; }
                }
            }
        }

        void AddStruggle(List<Shot> c, List<FilmHotspot> hot, List<Trace> traces)
        {
            var list = new List<(string kind, Vector3 p, Vector3 n, float w, float size)>();
            foreach (var h in hot)
            {
                switch (h.Kind)
                {
                    case GoreKind.Topple: list.Add(("topple", h.Pos, h.Normal, h.Weight + 1f, Mathf.Max(0.6f, h.Size))); break;
                    case GoreKind.Handprint: list.Add(("handprint", h.Pos, h.Normal, h.Weight + 0.6f, 0.5f)); break;
                    case GoreKind.NailScratch: list.Add(("nails", h.Pos, h.Normal, h.Weight + 0.4f, 0.4f)); break;
                    case GoreKind.SawMark: list.Add(("saw", h.Pos, h.Normal, h.Weight + 0.3f, 0.45f)); break;
                    case GoreKind.Smear: list.Add(("smear", h.Pos, h.Normal, h.Weight, Mathf.Clamp(h.Size, 0.3f, 0.8f))); break;
                }
            }
            if (list.Count == 0)
                foreach (var t in traces)
                {
                    string k = t.Type == "Struggle" ? "topple" : t.Type == "Handprint" ? "handprint" : t.Type == "Scratch" ? "nails" : t.Type == "Fragment" ? "fragment" : t.Type == "DragMark" ? "drag" : t.Type == "FootprintBlood" ? "footprint" : t.Type == "BloodSmear" ? "smear" : null;
                    if (k == null) continue;
                    list.Add((k, _s.World.ToWorld(t.Pos) + Vector3.up * (k == "topple" ? 0.3f : 0.02f), Vector3.up, k == "topple" ? 1.5f : 0.8f, k == "topple" ? 0.9f : 0.5f));
                }
            int added = 0;
            foreach (var st in list.OrderByDescending(x => x.w))
            {
                if (added >= 2) break;
                var s = new Shot { Name = "i_struggle_" + st.kind, Score = st.kind == "topple" || st.kind == "handprint" || st.kind == "nails" ? 7 : 6, Cat = 2, P = st.p, Look = st.p, FitW0 = Mathf.Clamp(st.size * 1.4f, 0.4f, 1.3f), Aperture = st.kind == "nails" ? 1.4f : Rnd(2f, 2.6f) };
                s.FitW1 = s.FitW0 * 0.82f;
                // a toppled piece is seen if its top is: the sight line may end inside its own bounds
                if (st.kind == "topple") { _ignoreCols = PieceColliders(st.p); if (!SeenFromEye(st.p + Vector3.up * 0.12f, null) && !SeenFromEye(st.p, null)) { PLog($"  reject {s.Name}: not visible from the eye"); _ignoreCols = null; continue; } }
                else if (!SeenFromEye(st.p, null)) { PLog($"  reject {s.Name}: not visible from the eye"); continue; }
                bool ok;
                if (st.kind == "topple")
                {
                    // low and tilted: the fallen chair from the floor, the room leaning
                    s.Low = true; s.Dutch = true; s.Roll = _rng.Next(2) == 0 ? -10f : 10f;
                    ok = SolveGaze(s, 0.9f, _floorY + 0.1f, maxY: _floorY + 0.35f);
                }
                else if (Mathf.Abs(st.n.y) < 0.7f)
                {
                    // on a wall or a door frame: along its face
                    var right = Vector3.Cross(Vector3.up, st.n).normalized; ok = false;
                    foreach (float lat in new[] { 0.08f * _side, -0.08f * _side, 0f })
                    {
                        s.Lens = CineSolver.KeepIn(S.Layout.Room(_room), st.p + st.n * (st.kind == "nails" ? 0.4f : 0.5f) + right * lat, 0.2f);
                        if (Validate(s)) { ok = true; _side = -_side; Finish(s); break; }
                    }
                }
                else { s.Low = true; ok = SolveGaze(s, 0.55f, _floorY + 0.1f, maxY: _floorY + 0.45f); }
                _ignoreCols = null;
                if (ok) { c.Add(s); added++; }
            }
        }

        /// <summary>Colliders a sight line to a toppled piece may end in: the piece's own (found at its centre; never a wall,
        /// a floor or anything bigger than furniture).</summary>
        HashSet<Collider> _ignoreCols;
        static HashSet<Collider> PieceColliders(Vector3 centre)
        {
            var set = new HashSet<Collider>();
            foreach (var c in Physics.OverlapSphere(centre, 0.18f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c == null) continue; var b = c.bounds;
                if (b.size.x > 2.2f || b.size.z > 2.2f || b.size.y > 2.2f) continue;   // walls, floors, big fixtures
                if (c.GetComponentInParent<ActorView>() != null) continue;
                set.Add(c);
                // the rest of the same piece
                var root = c.attachedRigidbody != null ? c.attachedRigidbody.transform : c.transform;
                if (root != null) foreach (var o in root.GetComponentsInChildren<Collider>()) if (o != null && o.bounds.size.magnitude < 2.5f) set.Add(o);
            }
            return set;
        }

        void AddWeapon(List<Shot> c)
        {
            var it = _inc != null ? S.I(_inc.Weapon) : null;
            if (it == null || it.Room != _room || it.Holder != null || it.Hidden) { if (it != null) PLog($"  i_weapon: {it.Type} not lying here (room {it.Room}, holder {it.Holder}, hidden {it.Hidden})"); return; }
            if (!_s.World.Items.TryGetValue(it.Id, out var iv) || iv == null) return;
            var rs = iv.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var s = new Shot { Name = "i_weapon", Score = 7, Cat = 2, P = b.center, Look = b.center, ItemId = it.Id, FitW0 = Mathf.Clamp(b.size.magnitude * 1.5f, 0.35f, 0.8f), Aperture = Rnd(2f, 2.8f) };
            s.FitW1 = s.FitW0 * 0.82f;
            var tr = iv.transform; var off = b.center - tr.position; s.Target = () => tr != null ? tr.position + off : s.P;
            if (!SeenFromEye(s.P, it.Id)) { PLog("  reject i_weapon: not visible from the eye"); return; }
            if (SolveGaze(s, 0.5f, _floorY + 0.1f)) c.Add(s);
        }

        /// <summary>
        /// Someone else is in the room with it and sees it too: for a moment his eyes go to their face (turned toward him,
        /// within 7 m, in plain view) — a close shot from his side, and they recoil as the cut lands on them. Never 민혁's own
        /// face; never someone turned away.
        /// </summary>
        void AddWitness(List<Shot> c)
        {
            ActorView best = null; string bestId = null; float bestD = float.MaxValue;
            foreach (var a in S.Actors.Values)
            {
                if (!a.Alive || a.Status != ActorStatus.Active || a.IsPlayer || a.IsButler || a.Id == _victim || a.CarriedBy != null || a.StairId >= 0) continue;
                var av = _s.World.ViewOf(a.Id); if (av == null || av.Rig == null) continue;
                var head = av.HeadPos; float d = Vector3.Distance(head, _eye0);
                if (d > 7f || d < 0.9f || d >= bestD) continue;
                int hr = _mv != null ? _mv.RoomAtWorld(head - Vector3.up * 0.4f) : a.Room; if (hr != _room) continue;
                var face = CineAnchors.FaceFwd(a.Id); if (Vector3.Dot(face, (_eye0 - head).normalized) < 0.15f) continue;
                _ignoreActor = a.Id; bool seen = SeenFromEye(head, null); _ignoreActor = null;
                if (!seen) continue;
                best = av; bestId = a.Id; bestD = d;
            }
            if (best == null) return;
            var P = best.HeadPos; var bv = best;
            var s = new Shot { Name = "i_witness", Score = 7, Cat = 2, P = P, Look = P, FitW0 = 0.5f, FitW1 = 0.4f, Aperture = 2.0f, Witness = bestId };
            s.Target = () => bv != null ? bv.HeadPos : P;
            _ignoreActor = bestId;
            try { if (SolveGaze(s, 0.95f, _floorY + 0.9f)) { c.Add(s); PLog($"  witness {bestId} ({Cast.NameOf(bestId)}) {bestD:0.0} m"); } }
            finally { _ignoreActor = null; }
        }
        string _ignoreActor;

        /// <summary>
        /// When the room gives fewer than two clear details (a cramped room, furniture everywhere): the body itself, along his
        /// own line of sight — first whole, from a step or two nearer and a little lower than his eyes; then close and low on
        /// the upper body, the way the eyes are dragged back to it. Both are gaze shots; the close one is the last image.
        /// </summary>
        void AddGazeFallback(List<Shot> c)
        {
            var torso = _focus; var d = torso - _eye0; float D = d.magnitude;
            if (D < 0.9f) { PLog("  gaze fallback: he is standing over it (no room for a nearer lens)"); return; }
            var dir = d / D;
            if (!c.Any(x => x.Name == "i_body"))
            {
                var s1 = new Shot { Name = "i_body", Score = 6, Cat = 3, P = torso, Look = torso, FitW0 = 1.5f, FitW1 = 1.25f, Aperture = 2.8f, WorstOk = true, WorstRank = 3 };
                s1.Target = () => _body != null ? CineAnchors.Body(_victim) : torso;
                foreach (float dist in new[] { 1.5f, 1.8f, 1.2f, 2.2f })
                {
                    if (dist >= D - 0.25f) continue;
                    s1.Lens = torso - dir * dist; s1.Lens.y = Mathf.Max(s1.Lens.y - 0.15f, _floorY + 0.5f);
                    if (Validate(s1)) { Finish(s1); c.Add(s1); break; }
                }
            }
            var chestT = _body?.Rig?.Chest; var chest = chestT != null ? chestT.position : torso;
            var d2 = chest - _eye0; d2.y = 0f; d2 = d2.sqrMagnitude > 1e-4f ? d2.normalized : new Vector3(dir.x, 0f, dir.z).normalized;
            var s2 = new Shot { Name = "i_close", Score = 6, Cat = 3, P = chest, Look = chest, FitW0 = 0.7f, FitW1 = 0.56f, Aperture = 2.0f, Low = true, WorstOk = true, WorstRank = 2 };
            s2.Target = () => chestT != null ? chestT.position : chest;
            foreach (float dist in new[] { 0.8f, 0.65f, 1.0f })
                foreach (float side in new[] { 0f, 0.12f, -0.12f })
                {
                    var right = Vector3.Cross(Vector3.up, d2);
                    s2.Lens = chest - d2 * dist + right * side; s2.Lens.y = Mathf.Max(_floorY + 0.35f, chest.y + 0.28f);
                    if (Validate(s2)) { Finish(s2); c.Add(s2); return; }
                }
            PLog("  gaze fallback: no clear lens on the body either");
        }

        // ------------------------------------------------------------------ lens solving
        /// <summary>The lens on his line of sight to P, `dist` from it, a little to one side (alternating), kept in the room;
        /// then the other side, then turned ±25°/±45° and a little closer, until one validates.</summary>
        bool SolveGaze(Shot s, float dist, float minY, float maxY = 99f)
        {
            var dir = _eye0 - s.P; if (dir.sqrMagnitude < 1e-4f) dir = Vector3.up; dir.Normalize();
            var room = S.Layout.Room(_room);
            foreach (float turn in new[] { 0f, 25f, -25f, 45f, -45f })
                foreach (float dk in new[] { 1f, 0.78f })
                    foreach (int sd in new[] { _side, -_side })
                    {
                        var d = Quaternion.AngleAxis(turn, Vector3.up) * dir;
                        var right = Vector3.Cross(Vector3.up, d); right = right.sqrMagnitude > 1e-4f ? right.normalized : Vector3.right;
                        var lens = s.P + d * dist * dk + right * (sd * Rnd(0.05f, 0.12f));
                        lens.y = Mathf.Clamp(lens.y, minY, maxY);
                        s.Lens = CineSolver.KeepIn(room, lens, 0.2f);
                        if (s.Lens.y < minY) s.Lens.y = minY;
                        if (Validate(s)) { _side = -sd; Finish(s); return true; }
                    }
            return false;
        }

        /// <summary>Around P at `el` degrees of elevation, starting from his side and turning until `sideOk` and the lens validate.</summary>
        bool SolveAround(Shot s, float dist, float el, Func<Vector3, bool> sideOk)
        {
            var h = _eye0 - s.P; h.y = 0; h = h.sqrMagnitude > 1e-4f ? h.normalized : Vector3.forward;
            var room = S.Layout.Room(_room);
            foreach (float turn in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f })
            {
                var d = Quaternion.AngleAxis(turn, Vector3.up) * h;
                var lens = s.P + (d * Mathf.Cos(el * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(el * Mathf.Deg2Rad)) * dist;
                s.Lens = CineSolver.KeepIn(room, lens, 0.2f);
                if (s.Lens.y < _floorY + 0.1f) s.Lens.y = _floorY + 0.1f;
                if (!sideOk(s.Lens)) continue;
                if (Validate(s)) { Finish(s); return true; }
            }
            PLog($"  reject {s.Name}: no angle shows the right side");
            return false;
        }

        /// <summary>The lens is in the focus room, not inside anything, sees P, and sees nothing of 민혁.</summary>
        bool Validate(Shot s)
        {
            string why = null;
            int lr = _mv != null ? _mv.RoomAtWorld(s.Lens) : _room;
            if (lr != _room) why = $"lens outside the room ({lr})";
            else if (Vector3.Distance(s.Lens, s.P) < 0.2f) why = "lens too close";
            else if (Inside(s.Lens, s.ItemId)) why = "lens inside geometry";
            else if (!LineClear(s.Lens, s.P, s.ItemId)) why = "line to the detail blocked";
            else if (InsideHim(s.Lens)) why = "lens inside 민혁's body";
            if (why != null) { PLog($"  reject {s.Name} lens {s.Lens:F2}: {why}"); return false; }
            return true;
        }

        void Finish(Shot s)
        {
            // key light: one candle-warm spot on the side away from the lens (the pool: mirrored in the floor toward the lens)
            if (s.PoolKey)
            {
                var d = (s.P - s.Lens).normalized; var r = Vector3.Reflect(d, Vector3.up);
                s.KeyPos = s.P + r * 1.1f;
            }
            else
            {
                var toLens = s.Lens - s.P; toLens.y = 0; toLens = toLens.sqrMagnitude > 1e-4f ? toLens.normalized : Vector3.forward;
                var away = Quaternion.AngleAxis((_rng.Next(2) == 0 ? -1f : 1f) * Rnd(110f, 150f), Vector3.up) * toLens;
                float el = Rnd(35f, 55f) * Mathf.Deg2Rad, dist = Rnd(0.9f, 1.4f);
                s.KeyPos = s.P + (away * Mathf.Cos(el) + Vector3.up * Mathf.Sin(el)) * dist;
            }
            s.KeyPos = CineSolver.KeepIn(S.Layout.Room(_room), s.KeyPos, 0.15f);
            PLog($"  ok {s}");
        }

        // ------------------------------------------------------------------ selection and order
        void Select(List<Shot> cands)
        {
            if (cands.Count < 2) { PLog($"  only {cands.Count} candidate(s)"); return; }
            // ties broken by the planner's seed
            var order = cands.Select(c => (c, r: _rng.NextDouble())).OrderByDescending(x => x.c.Score).ThenBy(x => x.r).Select(x => x.c).ToList();
            Shot worst = order.Where(c => c.WorstOk).OrderBy(c => c.WorstRank).ThenByDescending(c => c.Score).FirstOrDefault();
            int n = _full ? (cands.Count >= 4 ? 4 : cands.Count >= 3 ? 3 : 2) : 2;
            var picks = new List<Shot>();
            bool Far(Shot a) => picks.All(p => Vector3.Distance(p.P, a.P) >= 0.6f) && (worst == null || a == worst || Vector3.Distance(worst.P, a.P) >= 0.6f);
            foreach (var c in order)
            {
                if (c == worst) continue;
                if (picks.Count >= n - (worst != null ? 1 : 0)) break;
                if (picks.Any(p => p.Name == c.Name)) { PLog($"  skip {c.Name}: same kind already chosen"); continue; }
                if (!Far(c)) { PLog($"  skip {c.Name}: within 0.6 m of a chosen detail"); continue; }
                picks.Add(c);
            }
            // never fewer than two if there are two to be had (the distance rule gives way first)
            if (picks.Count + (worst != null ? 1 : 0) < 2)
                foreach (var c in order) { if (c == worst || picks.Contains(c)) continue; picks.Add(c); PLog($"  take {c.Name} despite the distance rule"); if (picks.Count + (worst != null ? 1 : 0) >= 2) break; }
            // at least one low or tilted view
            var all = new List<Shot>(picks); if (worst != null) all.Add(worst);
            if (!all.Any(x => x.Low || x.Dutch))
            {
                var alt = order.FirstOrDefault(c => (c.Low || c.Dutch) && c != worst && !picks.Contains(c));
                if (alt != null && picks.Count > 0) { var drop = picks.OrderBy(p => p.Score).First(); picks.Remove(drop); picks.Add(alt); PLog($"  swap {drop.Name} → {alt.Name} (a low or tilted view)"); }
            }
            // extremity → blood → weapon / struggle → the worst image last
            picks = picks.OrderBy(p => p.Cat).ThenByDescending(p => p.Score).ToList();
            if (worst != null) picks.Add(worst);
            else { var last = picks.OrderBy(p => p.WorstRank).ThenByDescending(p => p.Score).First(); picks.Remove(last); picks.Add(last); }
            // consecutive details at least 0.6 m apart (swap neighbours if needed, never the last)
            for (int i = 1; i < picks.Count - 1; i++)
                if (Vector3.Distance(picks[i].P, picks[i - 1].P) < 0.6f && i + 1 < picks.Count - 1) { var t = picks[i]; picks[i] = picks[i + 1]; picks[i + 1] = t; }
            // lengths: still and long, never accelerating; the last is the longest
            float[] durs = !_full ? new[] { 1.2f, 1.5f } : picks.Count >= 4 ? new[] { 1.1f, 1.2f, 1.3f, 1.8f } : picks.Count == 3 ? new[] { 1.3f, 1.5f, 1.8f } : new[] { 1.5f, 1.8f };
            int k0 = durs.Length - picks.Count;
            for (int i = 0; i < picks.Count; i++) picks[i].Dur = durs[Mathf.Clamp(k0 + i, 0, durs.Length - 1)];
            _plan.AddRange(picks.Take(_full ? 4 : 2));
        }

        // ------------------------------------------------------------------ physics helpers
        /// <summary>Colliders a sight line may pass: the victim, 민혁, severed pieces, the detail's own item, trace decals.</summary>
        bool Ignorable(Collider c, string itemId)
        {
            if (c == null) return true;
            var av = c.GetComponentInParent<ActorView>(); if (av != null && (av.Id == _victim || av.Id == Cast.Player || (_ignoreActor != null && av.Id == _ignoreActor))) return true;
            if (c.GetComponentInParent<PlayerController>() != null) return true;
            // the dead lie as a ragdoll whose proxy bodies live beside the view, not under it: the victim's own limbs never
            // hide the victim (nor 민혁's, if he ever fell)
            var part = c.GetComponentInParent<BL23.Game.Physicality.PhysicalBodyPart>();
            if (part != null) { var ov = part.Owner != null ? part.Owner.GetComponent<ActorView>() : null; if (ov == null || ov.Id == _victim || ov.Id == Cast.Player) return true; }
            var tag = c.GetComponentInParent<ItemTag>(); if (tag != null && (_ignoreItems.Contains(tag.ItemId) || tag.ItemId == itemId)) return true;
            if (c.GetComponentInParent<TraceView>() != null) return true;
            return false;
        }

        bool LineClear(Vector3 a, Vector3 b, string itemId)
        {
            var d = b - a; float len = d.magnitude; if (len < 0.02f) return true; d /= len;
            foreach (var h in Physics.RaycastAll(a, d, len, ~0, QueryTriggerInteraction.Ignore))
            {
                if (len - h.distance < 0.1f) continue;          // the surface the detail lies on
                if (Ignorable(h.collider, itemId)) continue;
                if (_ignoreCols != null && _ignoreCols.Contains(h.collider)) continue;
                return false;
            }
            return true;
        }

        bool Inside(Vector3 p, string itemId)
        {
            foreach (var c in Physics.OverlapSphere(p, 0.05f, ~0, QueryTriggerInteraction.Ignore)) if (!Ignorable(c, itemId)) return true;
            return false;
        }

        /// <summary>Line of sight from where 민혁 stood (before the dolly) to a detail.</summary>
        bool SeenFromEye(Vector3 p, string itemId = null) => LineClear(_eye0, p, itemId);

        /// <summary>
        /// A lens inside 민혁's own body. (He is hidden for every insert — ForceHidden, renderers and shadows off — so nothing
        /// of him can be in frame; the old per-renderer frustum test counted his skins' fixed 2.6 m bounds and the LOD levels
        /// the LODGroup was not even drawing, and threw away almost every close-up.) Tested against his controller capsule
        /// and his bones: within 0.24 m of the head, chest or hips, 0.16 m of a limb.
        /// </summary>
        bool InsideHim(Vector3 lens)
        {
            var cc = _pc != null ? _pc.CC : null;
            if (cc != null && cc.enabled)
            {
                var t = cc.transform; float r = cc.radius, h = Mathf.Max(cc.height, 2f * r);
                var p0 = t.TransformPoint(cc.center + Vector3.up * (h * 0.5f - r)); var p1 = t.TransformPoint(cc.center - Vector3.up * (h * 0.5f - r));
                if (SegDist(lens, p0, p1) < r + 0.03f) return true;
            }
            var rig = _me?.Rig; if (rig == null) return false;
            foreach (var hb in CoreBones) if (rig.HasBone(hb) && (rig.Bone(hb).position - lens).sqrMagnitude < 0.24f * 0.24f) return true;
            foreach (var (x, y) in LimbSegs)
                if (rig.HasBone(x) && rig.HasBone(y) && SegDist(lens, rig.Bone(x).position, rig.Bone(y).position) < 0.16f) return true;
            return false;
        }
        static readonly HBone[] CoreBones = { HBone.Head, HBone.Neck, HBone.Chest, HBone.Spine, HBone.Hips };
        static readonly (HBone, HBone)[] LimbSegs =
        {
            (HBone.UpperArmL, HBone.LowerArmL), (HBone.LowerArmL, HBone.HandL), (HBone.UpperArmR, HBone.LowerArmR), (HBone.LowerArmR, HBone.HandR),
            (HBone.UpperLegL, HBone.LowerLegL), (HBone.LowerLegL, HBone.FootL), (HBone.UpperLegR, HBone.LowerLegR), (HBone.LowerLegR, HBone.FootR),
        };
        static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a; float l2 = ab.sqrMagnitude; float t = l2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0f;
            return (p - (a + ab * t)).magnitude;
        }
    }
}
