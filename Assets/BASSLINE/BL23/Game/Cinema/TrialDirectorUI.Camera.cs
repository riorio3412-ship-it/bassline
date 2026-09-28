using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Cinema;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// The court's camera (the camera half of TrialDirectorUI). Deliberate, never jittery:
    /// · a shot is a move along a path (straight drift, crane curve, orbit arc); a new shot blends in from wherever the lens is,
    ///   no faster than ~80°/s, unless it is a cut;
    /// · the camera changes only when the floor changes hands — the same speaker keeps their shot (a dramatic line pushes in, a
    ///   tense round turns slowly around them);
    /// · grammar: an establishing crane down out of the dark, three kinds of wide (across the ring, from the gallery, from the
    ///   floor up into the height), over-the-shoulder exchanges with each person held on a consistent side of the frame, a
    ///   low-angle push-in on an accusation with a cut to the accused's face, a snap-zoom and a held breath on a contradiction,
    ///   rare cutaways to a listener or to the watching eyes, a low push-in on the judge's seat;
    /// · close shots get depth of field; the speaker has a key and a rim light. No grain, no tint, no shake.
    /// Every lens point stays inside the circle of the court.
    /// </summary>
    [DefaultExecutionOrder(20)]   // after Session.Update (which poses everyone): the eyelines set here are the ones the animators use
    public sealed partial class TrialDirectorUI
    {
        Func<float, (Vector3 p, Vector3 l)> _path; float _dT0 = -1, _dDur = 1; float _roll, _rollTarget, _fovTarget = 40, _fovRate = 2f;
        Vector3 _bFromP, _bFromL; float _bT0 = -1, _bDur = 1; string _subject; float _orbitA; int _wideN;
        Vector3 _floorC; float _courtR = 9f, _standR = 5f, _courtTop = -1f;
        bool _dofOn; float _dofAperture = 2.8f, _dofFocal = 55f; Func<Vector3> _focusOf;
        float _impactT0 = -9f, _impactAmt, _freezeUntil = -1f; bool _frozeTime;
        float _cutAt = -1f; Action _cutAction; float _lastCutaway = -99f; int _dramaN, _changeN;
        bool _probeAccuse, _probeBreak, _probeOts, _probeGallery, _probeCrane, _probeLow; float _craneUntil = -1f;
        readonly List<Vector3> _eyePts = new List<Vector3>();
        bool _revealOpen, _pathLinear, _openingTilt; float _openingT0; string _shotKind = "none"; Func<float, float> _fovFunc;

        static float Ease(float u) { u = Mathf.Clamp01(u); float s = u * u * (3f - 2f * u); return Mathf.Lerp(u, s, 0.7f); }

        /// <summary>Height above the court floor the lens may rise to (the vault, or the dark above it).</summary>
        float CourtTop()
        {
            if (_courtTop > 0) return _courtTop;
            float h = 0f; var cv = _s.World?.Court;
            if (cv != null)
            {
                var t = cv.GetType();
                foreach (var n in new[] { "CameraCeiling", "ShaftTop", "VaultHeight", "CeilingHeight", "Height" })
                {
                    try
                    {
                        var p = t.GetProperty(n); if (p != null && p.PropertyType == typeof(float)) { h = (float)p.GetValue(cv); break; }
                        var f = t.GetField(n); if (f != null && f.FieldType == typeof(float)) { h = (float)f.GetValue(cv); break; }
                    }
                    catch (Exception) { }
                }
            }
            if (h <= 0f) { var court = S.Layout.Rooms.FirstOrDefault(r => r.Type == RoomType.Courtroom); h = court != null ? court.CeilingH : 10.5f; }
            _courtTop = Mathf.Clamp(h - 1.2f, 4f, 60f);   // a shaft into the dark: the lens may climb high for the establishing crane (never to its end)
            return _courtTop;
        }

        Vector3 InCourt(Vector3 p)
        {
            if (_courtR <= 0) return p;
            var d = p - _floorC; float y = d.y; d.y = 0; float lim = Mathf.Max(1.5f, _courtR - 0.8f); if (_pathFree && HallR() > _courtR + 2f) lim = Mathf.Max(lim, HallR() * 0.85f);
            if (d.magnitude > lim) d = d.normalized * lim;
            return _floorC + d + Vector3.up * Mathf.Clamp(y, 0.28f, CourtTop());
        }

        /// <summary>Start a move. cut = jump there; otherwise blend from where the lens is.</summary>
        void Move(Func<float, (Vector3 p, Vector3 l)> path, float dur, float fov, float roll, bool cut, bool dof = false, Func<Vector3> focus = null, float aperture = 2.8f, float focal = 55f, bool free = false)
        {
            _pathFree = free; _pathLinear = false; _fovFunc = null; _path = u => { var (p, l) = path(u); return (InCourt(p), l); };
            var (p0, l0) = _path(0f);
            var curFwd = (_camLook - _camPos); if (curFwd.sqrMagnitude < 1e-6f) curFwd = Vector3.forward;
            float ang = Vector3.Angle(curFwd, l0 - p0), dist = Vector3.Distance(_camPos, p0);
            _dT0 = Time.unscaledTime; _dDur = Mathf.Max(0.5f, dur); _fovTarget = fov; _rollTarget = roll; _fovRate = cut ? 99f : 2f; _fovPathOn = false;
            _dofOn = dof; _focusOf = focus; _dofAperture = aperture; _dofFocal = focal;
            if (cut) { _bT0 = -1; _camPos = p0; _camLook = l0; _fovBase = fov; _roll = roll; }
            else { _bFromP = _camPos; _bFromL = _camLook; _bT0 = Time.unscaledTime; _bDur = Mathf.Clamp(Mathf.Max(ang / 80f, dist / 5f), 1.5f, 3.2f); }
        }
        void Shot(Vector3 p0, Vector3 p1, Vector3 l0, Vector3 l1, float dur, float fov, float roll, bool cut, bool dof = false, Func<Vector3> focus = null, float aperture = 2.8f, float focal = 55f, bool free = false)
            => Move(u => (Vector3.Lerp(p0, p1, u), Vector3.Lerp(l0, l1, u)), dur, fov, roll, cut, dof, focus, aperture, focal, free);

        void CancelCutaway() { _cutAt = -1f; _cutAction = null; }
        void Later(float secs, Action a) { _cutAt = Time.unscaledTime + secs; _cutAction = a; }

        // ---------------------------------------------------------------- framing math: every person-shot is aimed, then checked
        float Aspect => _cam != null && _cam.aspect > 0.2f ? _cam.aspect : 16f / 9f;
        /// <summary>Vertical FOV that shows 'frameH' metres of the scene at distance 'dist' (a medium close-up is ~1 m).</summary>
        static float FovFor(float frameH, float dist) => Mathf.Clamp(2f * Mathf.Atan(frameH * 0.5f / Mathf.Max(0.3f, dist)) * Mathf.Rad2Deg, 12f, 44f);
        /// <summary>The point to look at from 'cam' so that 'target' lands at screen (sx, sy) with this vertical FOV.</summary>
        Vector3 Aim(Vector3 cam, Vector3 target, float sx, float sy, float fovV)
        {
            var d = target - cam; float dist = d.magnitude; if (dist < 1e-3f) return target; d /= dist;
            float tanV = Mathf.Tan(fovV * 0.5f * Mathf.Deg2Rad), tanH = tanV * Aspect;
            float ax = Mathf.Atan((sx - 0.5f) * 2f * tanH) * Mathf.Rad2Deg, ay = Mathf.Atan((sy - 0.5f) * 2f * tanV) * Mathf.Rad2Deg;
            var f = Quaternion.AngleAxis(-ax, Vector3.up) * d;
            var r = Vector3.Cross(Vector3.up, f); if (r.sqrMagnitude < 1e-6f) r = Vector3.right; r.Normalize();
            f = Quaternion.AngleAxis(ay, r) * f;
            return cam + f * dist;
        }
        /// <summary>Where a world point lands on screen (0..1) for a lens at 'cam' looking at 'look' (no roll).</summary>
        Vector2 Proj(Vector3 cam, Vector3 look, float fovV, Vector3 p, out float z)
        {
            var f = (look - cam).normalized; var r = Vector3.Cross(Vector3.up, f); if (r.sqrMagnitude < 1e-6f) r = Vector3.right; r.Normalize(); var u = Vector3.Cross(f, r);
            var d = p - cam; z = Vector3.Dot(d, f); if (z < 0.05f) return new Vector2(-9f, -9f);
            float tanV = Mathf.Tan(fovV * 0.5f * Mathf.Deg2Rad), tanH = tanV * Aspect;
            return new Vector2(0.5f + Vector3.Dot(d, r) / (z * tanH) * 0.5f, 0.5f + Vector3.Dot(d, u) / (z * tanV) * 0.5f);
        }
        /// <summary>Inside [0.15, 0.85] and clear of the dialogue box (bottom), the key hints (bottom right) and the scales (top right).</summary>
        static bool Safe(Vector2 s, bool head)
        {
            if (s.x < 0.15f || s.x > 0.85f || s.y < 0.15f || s.y > 0.85f) return false;
            if (s.y < 0.26f) return false;
            if (s.x > 0.47f && s.y < 0.31f) return false;
            if (head && s.x > 0.71f && s.y > 0.77f) return false;
            return true;
        }
        /// <summary>A clear line from the lens to a point: walls, furniture and other people block it; the listed people do not.</summary>
        bool SightClear(Vector3 cam, Vector3 target, ActorView a, ActorView b = null)
        {
            var d = target - cam; float len = d.magnitude; if (len < 0.05f) return true; d /= len;
            foreach (var h in Physics.SphereCastAll(cam, 0.05f, d, Mathf.Max(0f, len - 0.12f), ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider == null || h.distance <= 0f) continue;
                var av = h.collider.GetComponentInParent<ActorView>(); if (av != null && (av == a || av == b)) continue;
                if (h.collider.GetComponentInParent<ItemTag>() != null) continue;
                return false;
            }
            return !CineSolver.IsInside(cam);
        }
        Vector3 FaceDir(ActorView v, Vector3 head)
        {
            var f = v.transform.forward; f.y = 0; if (f.sqrMagnitude < 1e-3f) { f = _floorC - head; f.y = 0; }
            return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
        }

        /// <summary>
        /// A person-shot. In front of the face, turned a little toward whoever the words are for, the head on a third with room to
        /// look into, 'frameH' metres of the scene top to bottom (1 m ≈ head to the podium rail). Each candidate lens is checked: a clear
        /// line to the head and the chest, and both land inside the safe frame. 'lensY' (metres above the floor) makes it a low angle.
        /// </summary>
        bool Single(string id, string toward, float frameH, bool cut, float roll, float dur = 7f, float push = 0.86f, float sy = 0.64f, float flip = 1f, float lensY = -1f, float aperture = 2.2f)
        {
            var v = _s.World.ViewOf(id); if (v == null || !AtCourt(v.HeadPos)) return false;
            var head = v.HeadPos; var chest = head + Vector3.down * Mathf.Min(0.24f, frameH * 0.26f);
            var face = FaceDir(v, head);
            var tv = toward != null && toward != id ? _s.World.ViewOf(toward) : null;
            var to = tv != null ? tv.HeadPos - head : face; to.y = 0; if (to.sqrMagnitude < 1e-3f) to = face; to.Normalize();
            float lean = Mathf.Clamp(Vector3.SignedAngle(face, to, Vector3.up) * 0.5f, -38f, 38f);
            float sg = (Mathf.Abs(lean) < 4f ? SideOf(id) : Mathf.Sign(lean)) * flip;
            var baseDir = Quaternion.AngleAxis(Mathf.Abs(lean) < 4f ? sg * 18f : lean * flip, Vector3.up) * face;
            float[] turns = { 0f, 14f * sg, -14f * sg, 28f * sg, -28f * sg };
            float d0 = Mathf.Clamp(frameH * 2.1f, 1.1f, 2.7f);
            float[] dists = { d0, d0 * 1.22f, d0 * 0.86f };
            for (int pass = 0; pass < 2; pass++)
                foreach (var dist in dists)
                    foreach (var t in turns)
                    {
                        var dir = Quaternion.AngleAxis(t, Vector3.up) * baseDir;
                        var cam = head + dir * dist + Vector3.down * 0.05f; if (lensY > 0f) cam.y = _floorC.y + lensY;
                        cam = InCourt(cam);
                        float dd = Vector3.Distance(cam, head); if (dd < dist * 0.6f) continue;
                        if (pass == 0 && (!SightClear(cam, head, v) || !SightClear(cam, chest, v))) continue;
                        float fov = FovFor(frameH, dd);
                        var r = Vector3.Cross(Vector3.up, head - cam); r.y = 0; r.Normalize(); float side = Vector3.Dot(to, r);
                        float sx = side > 0.15f ? 0.4f : side < -0.15f ? 0.6f : 0.5f;   // look room on the side the words go
                        var look = Aim(cam, head, sx, sy, fov);
                        if (pass == 0 && (!Safe(Proj(cam, look, fov, head, out _), true) || !Safe(Proj(cam, look, fov, chest, out _), false))) continue;
                        var p1 = head + (cam - head) * push; p1.y = Mathf.Lerp(cam.y, head.y - 0.05f, lensY > 0f ? 0.25f : 0f);
                        var l1 = Aim(p1, head, sx, sy, fov);
                        var vv = v; Func<Vector3> headF = () => vv != null ? vv.HeadPos : head;
                        _shotKind = "single"; Shot(cam, p1, look, l1, dur, fov, roll, cut, true, headF, aperture, 60f);
                        if (AutoProbe.Active && pass == 1) Debug.Log($"[CINE] single {id}: no fully clear angle, best effort");
                        return true;
                    }
            return false;
        }

        /// <summary>
        /// Over the shoulder of the one who spoke before — only when they are actually turned toward the new speaker. The shoulder
        /// stays at the edge (a fifth of the frame at most) and soft; the speaker is sharp on the far third.
        /// </summary>
        bool OverShoulder(string id, string prevId)
        {
            var v = _s.World.ViewOf(id); var pv = prevId != null ? _s.World.ViewOf(prevId) : null; if (v == null || pv == null) return false;
            var head = v.HeadPos; var ph = pv.HeadPos; var chest = head + Vector3.down * 0.24f;
            var dir = head - ph; float vs = Mathf.Abs(dir.y); dir.y = 0; float sep = dir.magnitude; if (sep < 1.3f || sep > 5.5f || vs > 1f) return false; dir /= sep;
            if (Vector3.Dot(FaceDir(pv, ph), dir) < 0.35f) return false;
            var s2 = Vector3.Cross(Vector3.up, dir); if (Vector3.Dot(s2, _floorC - ph) < 0) s2 = -s2;
            foreach (var back in new[] { 0.9f, 1.2f })
                foreach (var off in new[] { 0.55f, 0.75f, 0.95f })
                {
                    var cam = InCourt(ph - dir * back + s2 * off + Vector3.up * 0.05f);
                    if (Vector3.Dot(FaceDir(v, head), (cam - head).normalized) < 0.25f) return false;   // the new speaker would be in profile
                    if (!SightClear(cam, head, v, pv) || !SightClear(cam, chest, v, pv)) continue;
                    float dd = Vector3.Distance(cam, head); float fov = FovFor(1.05f, dd);
                    var fs = Proj(cam, Aim(cam, head, 0.5f, 0.63f, fov), fov, ph, out _);
                    float sx = fs.x < 0.5f ? 0.63f : 0.37f;
                    var look = Aim(cam, head, sx, 0.63f, fov);
                    if (!Safe(Proj(cam, look, fov, head, out _), true) || !Safe(Proj(cam, look, fov, chest, out _), false)) continue;
                    var fg = Proj(cam, look, fov, ph + Vector3.down * 0.12f, out float fz);
                    if (fz > 0.05f && fg.x > 0.1f && fg.x < 0.9f) continue;    // the shoulder must stay at the very edge
                    var p1 = cam + (head - cam) * 0.07f; var l1 = Aim(p1, head, sx, 0.63f, fov);
                    var vv = v; Func<Vector3> headF = () => vv != null ? vv.HeadPos : head;
                    _shotKind = "ots"; Shot(cam, p1, look, l1, 7f, fov, 0, true, true, headF, 1.4f, 75f);
                    return true;
                }
            return false;
        }

        // ---------------------------------------------------------------- cutaways: only once the line has been read
        // A cutaway (a listener, the accused, the eyes) is planned when a line starts and runs only after that line has finished
        // typing and has been on screen for a second; it lasts at most 1.2 s, the page waits for it, and any new page or beat
        // brings the camera straight back to whoever the name plate names.
        Action _cutPlan; TrialBeat _cutPlanBeat, _cutBeat, _camBeat; int _cutPage; bool _inCutaway, _wasWaiting; float _typedAt = -1f, _cutEnd;
        void PlanCutaway(Action a) { if (_s.Headless) return; _cutPlan = a; _cutPlanBeat = _beat; }
        void CancelPlan() { _cutPlan = null; _cutPlanBeat = null; }
        void UpdateCutaways()
        {
            bool typed = _beat != null && _waiting;
            if (typed && !_wasWaiting) _typedAt = Time.unscaledTime;
            _wasWaiting = typed;
            if (_cutPlan != null)
            {
                if (_beat != _cutPlanBeat || _inGame || _busy > 0) CancelPlan();
                else if (typed && Time.unscaledTime - _typedAt >= (ProbeFast ? 0.05f : 1.0f))
                {
                    var a = _cutPlan; CancelPlan(); var keep = _subject;
                    try { a(); } catch (Exception e) { Debug.LogException(e); }
                    _subject = keep; _inCutaway = true; _cutBeat = _beat; _cutPage = _page; _cutEnd = Time.unscaledTime + 1.2f; _lastCutaway = Time.unscaledTime;
                    _autoAt = Mathf.Max(_autoAt, _cutEnd + 0.15f);   // the page waits for the reaction
                }
            }
            if (_inCutaway && (Time.unscaledTime >= _cutEnd || _beat != _cutBeat || _page != _cutPage || !typed)) EndCutaway();
        }
        void EndCutaway() { if (!_inCutaway) return; _inCutaway = false; if (_subject == Cast.Butler) ShotJudge(JudgeHead()); else if (_subject != null) Return(_subject); }

        // ---------------------------------------------------------------- wides, the opening, the neutral shot
        bool _pathFree; float _hallR = -1f;
        /// <summary>The hall around the island of podiums (0 when the court is a closed room): wides and the crane may use it.</summary>
        float HallR()
        {
            if (_hallR >= 0f) return _hallR; _hallR = 0f; var cv = _s.World?.Court; if (cv == null) return _hallR;
            try { var p = cv.GetType().GetProperty("HallRadius"); if (p != null && p.PropertyType == typeof(float)) _hallR = (float)p.GetValue(cv); } catch (Exception) { }
            return _hallR;
        }
        bool Colossal => CourtTop() > 20f && HallR() > _courtR + 4f;

        /// <summary>A lens point that actually sees the floor: if 'want' is inside something or its view of the ring is blocked,
        /// try lower and further in until the line to the centre of the floor is open.</summary>
        Vector3 ClearAbove(Vector3 want, float minRise)
        {
            var target = _floorC + Vector3.up * 1.2f; var empty = new HashSet<string>();
            var off = want - _floorC; float h = off.y; off.y = 0; float r = off.magnitude; var dir = r > 0.01f ? off / r : Vector3.back;
            foreach (var hk in new[] { 1f, 0.75f, 0.55f, 0.4f, 0.28f, 0.18f })
                foreach (var rk in new[] { 1f, 0.7f, 0.45f, 0.25f })
                {
                    var p = _floorC + dir * (r * rk) + Vector3.up * Mathf.Max(minRise * 6f, h * hk);
                    p = InCourtFree(p);
                    if (!CineSolver.IsInside(p) && CineSolver.Clear(p, target, empty, 0.25f)) return p;
                }
            return _floorC + dir * Mathf.Min(r, _courtR * 0.6f) + Vector3.up * 4f;
        }
        Vector3 InCourtFree(Vector3 p) { bool f = _pathFree; _pathFree = true; var q = InCourt(p); _pathFree = f; return q; }

        /// <summary>The wides, in turn: a slow sideways dolly inside the ring past three or four faces at their podiums; out over the
        /// dark around the island (the lit disc small beneath the height); the dolly again the other way; the floor looking up past
        /// the far stands into the dark. Each turns to a new arc of the room.</summary>
        void Wide(float fov, bool cut, float turn = 0.85f, bool cheap = false)
        {
            if (Time.unscaledTime < _craneUntil) return;   // the opening move plays out
            _subject = null; _orbitA += turn; CancelCutaway(); CancelPlan(); _inCutaway = false;
            var dir = new Vector3(Mathf.Sin(_orbitA), 0, Mathf.Cos(_orbitA)); var side = Vector3.Cross(Vector3.up, dir);
            int wn = _wideN++ % 5; if (cheap && (wn == 1 || wn == 4)) wn = wn == 1 ? 3 : 0;   // under a round card: a light wide (cine18: high 29 ms, judge wide 26 ms, floor-up 11, dolly 8.5 — all GPU)
            switch (wn)
            {
                case 4: if (!JudgeWideShot(cut)) DollyFaces(cut, -1f); break;
                case 0: DollyFaces(cut, 1f); break;
                case 2: DollyFaces(cut, -1f); break;
                case 1:
                    {
                        // (the court's request: every wide pitched up 10–20° so the tiers and the height read, no depth of field)
                        if (Colossal)
                        {
                            float r = Mathf.Lerp(_courtR, HallR(), 0.55f), h = Mathf.Min(CourtTop() * 0.16f, 16f);
                            var p0 = ClearAbove(_floorC - dir * r + Vector3.up * h - side * 2f, 0.5f); var p1 = ClearAbove(_floorC - dir * (r * 0.94f) + Vector3.up * (h - 1.2f) + side * 2f, 0.5f);
                            _shotKind = "wide:high";
                            Shot(p0, p1, PitchUp(p0, _floorC + Vector3.up * 0.4f, 16f), PitchUp(p1, _floorC + dir * 1.5f + Vector3.up * 0.8f, 16f), 16f, fov + 6f, 0, cut, false, null, 2.8f, 55f, true); break;
                        }
                        float hh1 = Mathf.Min(CourtTop() * 0.7f, 7.5f);
                        var q0 = _floorC - dir * (_courtR * 0.78f) + Vector3.up * hh1 - side * 0.8f;
                        var q1 = _floorC - dir * (_courtR * 0.72f) + Vector3.up * (hh1 - 0.4f) + side * 0.8f;
                        var m0 = _floorC + dir * (_standR * 0.55f) + Vector3.up * 0.6f; var m1 = _floorC + dir * (_standR * 0.6f) + Vector3.up * 0.8f;
                        _shotKind = "wide:rim";
                        Shot(q0, q1, PitchUp(q0, m0, 14f), PitchUp(q1, m1, 14f), 15f, fov + 4f, 0, cut); break;
                    }
                default:
                    {
                        var p0 = _floorC - dir * (_standR * 0.25f) + Vector3.up * 0.45f + side * 0.4f;
                        var p1 = _floorC - dir * (_standR * 0.12f) + Vector3.up * 0.5f + side * 0.2f;
                        float hh = CourtTop() > 14f ? Mathf.Min(CourtTop() * 0.6f, 36f) : Mathf.Min(CourtTop() * 0.55f, 9f);
                        var reach = Colossal ? Mathf.Lerp(_courtR, HallR(), 0.7f) : _courtR;
                        var l0 = _floorC + dir * reach + Vector3.up * (hh * 0.55f); var l1 = _floorC + dir * reach + Vector3.up * (hh * 0.66f);
                        _shotKind = "wide:floorup";
                        Shot(p0, p1, PitchUp(p0, l0, 12f), PitchUp(p1, l1, 12f), 14f, fov + 8f, side.x > 0 ? 1.5f : -1.5f, cut); break;
                    }
            }
        }

        /// <summary>The court's judge wide, in the rotation: low on the far side of the ring, the lancet, the pier trunks and the chains
        /// filling the frame over the throne (his head lifted a little from the court's framing, clear of any caption); a slow creep.</summary>
        bool JudgeWideShot(bool cut)
        {
            var cv = _s.World?.Court; if (cv == null || !cv.WellShot("judge_wide", out var wp, out _, out var wf)) return false;
            var head = JudgeHead(); var to = head - wp; to.y = 0f;
            var p1 = wp + to * 0.07f + Vector3.up * 0.1f;
            _shotKind = "wide:judge";
            Shot(wp, p1, Aim(wp, head, 0.5f, 0.3f, wf), Aim(p1, head, 0.5f, 0.3f, wf), 14f, wf, 0, cut, false, null, 2.8f, 55f, true);
            return true;
        }

        /// <summary>A slow sideways dolly inside the ring, the lens at face height looking out at the podiums: three or four faces pass.</summary>
        void DollyFaces(bool cut, float way)
        {
            float a0 = _orbitA, arc = 0.55f * way; float rc = Mathf.Max(1.5f, _standR - 2.6f), rl = _standR;
            // a little below the faces and pitched up ~13°: the podiums, and the galleries and piers rising behind them; faces stay
            // above the dialogue box; a wide, so no depth of field
            float y = _floorC.y + 1.15f;
            Func<float, (Vector3, Vector3)> path = u =>
            {
                float a = a0 + arc * u; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                float al = a + 0.22f * way; var dl = new Vector3(Mathf.Sin(al), 0, Mathf.Cos(al));
                var p = _floorC + d * rc; p.y = y; var l = _floorC + dl * rl; l.y = y;   // level, then pitched up (faces land ~0.39)
                return (p, PitchUp(p, l, 13f));
            };
            _shotKind = "wide:dolly";
            Move(path, 13f, 44f, 0f, cut);
            if (AutoProbe.Active && !_probeDolly) { _probeDolly = true; StartCoroutine(ProbeShot("trial_cam_dolly", 2.4f, true)); }
        }
        bool _probeDolly, _probeSpeaker, _probeJudgeLine, _probeResult, _probeLand, _probeReact;

        /// <summary>The opening. Under the title card the lens waits high on the far side of the ring, about 38° down onto the clock
        /// floor (no straight-down view, no wide-angle stretch). When the card lifts it cranes down and around over the ring and lands
        /// on the judge's face before his first word; then the slow push on the judge's seat.</summary>
        void Establish(float turn, bool cut)
        {
            CancelCutaway(); CancelPlan(); _inCutaway = false;
            var cv = _s.World?.Court;
            if (cut)
            {
                _revealOpen = false;
                try { cv?.Beat("open"); } catch (Exception e) { Debug.LogException(e); }
                if (cv != null && cv.WellShot("reveal_start", out var rp, out var rl, out var rf))
                {
                    // under the title card: up the throne side of the well — piers, galleries and the lancet climbing to the Black Sun
                    _subject = null; _shotKind = "opening";
                    Shot(rp, rp + Vector3.up * 0.12f, rl, rl, 9f, rf, 0, true, false, null, 2.8f, 55f, true);
                    _revealOpen = true; return;
                }
            }
            else if (_revealOpen && cv != null && cv.WellShot("reveal_end", out var ep, out var el, out var ef))
            {
                // the card lifts: hold on the Sun (~3 s), then tilt down the lancet and the pier trunks to the throne (reveal_end:
                // Yusti on his seat, the lancet behind) never faster than ~5°/s — ease in, a steady tilt, ease out — then move in
                // across the floor to the landing on the judge, before his first word
                _revealOpen = false;
                var head0 = JudgeHead(); JudgeFrame(head0, out var qp0, out var qp1, out var ql0, out var ql1, out var qfov);
                var sp = _camPos; var d0 = _camLook - _camPos; float len0 = Mathf.Max(0.5f, d0.magnitude); d0 = d0.normalized;
                var dE = el - ep; float lenE = Mathf.Max(0.5f, dE.magnitude); dE = dE.normalized;
                float ang = Vector3.Angle(d0, dE), f0 = _cam != null ? _cam.fieldOfView : 50f;
                const float Vmax = 4.8f, Hold = 3.4f, Dolly = 3.2f; float ta = 1.8f;
                float tilt = ang / Vmax + ta; if (ang < Vmax * ta) { ta = 0f; tilt = Mathf.Max(2f, ang / Vmax * 1.6f); }
                float dur = Hold + tilt + Dolly;
                float Prog(float t)
                {
                    if (t <= 0f) return 0f; if (t >= tilt) return 1f;
                    if (ta <= 0f) { float x = t / tilt; return x * x * (3f - 2f * x); }
                    float v = ang / (tilt - ta), s;
                    if (t <= ta) s = 0.5f * v / ta * t * t;
                    else if (t <= tilt - ta) s = 0.5f * v * ta + v * (t - ta);
                    else { float r = tilt - t; s = ang - 0.5f * v / ta * r * r; }
                    return Mathf.Clamp01(s / Mathf.Max(0.01f, ang));
                }
                float G(float t) { float x = Mathf.Clamp01((t - Hold - tilt) / Dolly); return x * x * (3f - 2f * x); }
                Move(u =>
                {
                    float t = u * dur;
                    if (t < Hold + tilt) { float f = Prog(t - Hold); var p = Vector3.Lerp(sp, ep, f); return (p, p + Vector3.Slerp(d0, dE, f) * Mathf.Lerp(len0, lenE, f)); }
                    float g = G(t); return (Vector3.Lerp(ep, qp0, g), Vector3.Lerp(el, ql0, g));
                }, dur, qfov, 0, false, false, null, 2.8f, 55f, true);
                _pathLinear = true; _bT0 = -1f;   // it starts exactly where the lens is
                _fovFunc = u => { float t = u * dur; return t < Hold + tilt ? Mathf.Lerp(f0, ef, Prog(t - Hold)) : Mathf.Lerp(ef, qfov, G(t)); };
                _subject = Cast.Butler; _shotKind = "opening"; _craneUntil = Time.unscaledTime + dur; _openingTilt = true; _openingT0 = Time.unscaledTime;
                Later(dur, () => { _openingTilt = false; if (_subject == Cast.Butler && !_inCutaway) { _shotKind = "judge"; Shot(qp0, qp1, ql0, ql1, 9f, qfov, 0, false, false, null, 4f, 50f); } });
                if (AutoProbe.Active)
                {
                    Debug.Log($"[CINE] opening: hold {Hold}s, tilt {ang:0.0}° over {tilt:0.0}s (≤{Vmax}°/s), move in {Dolly}s; from {sp:F1} via {ep:F1} to {qp0:F1}");
                    if (!_probeCrane)
                    {
                        _probeCrane = true;
                        StartCoroutine(ProbeShot("trial_well_reveal", Hold - 0.5f, false)); StartCoroutine(ProbeShot("trial_cam_crane", Hold + tilt * 0.45f, false));
                        StartCoroutine(ProbeShot("trial_reveal_end", Hold + tilt + 0.05f, false));
                    }
                    if (!_probeLand) { _probeLand = true; StartCoroutine(ProbeShot("trial_cam_open_land", dur + 0.5f, false)); }
                }
                return;
            }
            var j = JudgeAnchor(); var jd = j != null ? j.position - _floorC : Vector3.forward; jd.y = 0; jd = jd.sqrMagnitude > 1e-3f ? jd.normalized : Vector3.forward;
            var sdir = Quaternion.AngleAxis(32f, Vector3.up) * -jd;
            float rh = Mathf.Max(_standR + 3.2f, 8.5f);
            var start = ClearAbove(_floorC + sdir * rh + Vector3.up * (rh * 0.78f), 0.5f);
            var lookStart = _floorC + jd * 0.6f + Vector3.up * 0.9f;
            if (cut) { _subject = null; Shot(start, start + (lookStart - start).normalized * 0.5f, lookStart, lookStart, 8f, 42, 0, true, false, null, 2.8f, 55f, true); return; }
            var head = JudgeHead(); JudgeFrame(head, out var jp0, out var jp1, out var jl0, out var jl1, out var jfov);
            var mid = InCourtFree(_floorC + Quaternion.AngleAxis(-55f, Vector3.up) * sdir * (_standR * 0.5f) + Vector3.up * 4.4f);
            var from = _camPos; var fromL = _camLook; const float Dur = 3.4f;
            Move(u =>
            {
                float e = Ease(u), a = 1f - e;
                var p = a * a * from + 2f * a * e * mid + e * e * jp0;
                var l = Vector3.Lerp(fromL, jl0, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e * 1.15f)));
                return (p, l);
            }, Dur, jfov, 0, false, false, null, 2.8f, 55f, true);
            _fovPathOn = true; _fovPathFrom = _cam != null ? _cam.fieldOfView : 42f; _fovPathTo = jfov;   // wide over the ring, narrowing only as it lands
            _bDur = 0.3f; _fovRate = 1.2f;
            _subject = Cast.Butler; _craneUntil = Time.unscaledTime + Dur;
            Later(Dur, () => { if (_subject == Cast.Butler && !_inCutaway) Shot(jp0, jp1, jl0, jl1, 7f, jfov, 0, false, true, () => JudgeHead(), 4f, 50f); });
            if (AutoProbe.Active && !_probeCrane) { _probeCrane = true; StartCoroutine(ProbeShot("trial_cam_crane", 1.3f, false)); }
            if (AutoProbe.Active && !_probeLand) { _probeLand = true; StartCoroutine(ProbeShot("trial_cam_open_land", Dur + 0.3f, false)); }
        }
        // the floor changes hands with a cut, never a swing across the room (the user: no whirling camera on every line)
        public void ShotWide() { Wide(48, true); }
        /// <summary>Tense rounds (the reconstruction): a slow half-orbit around the floor, looking in at the ring.</summary>
        public void CamOrbit(float fov = 44)
        {
            _subject = null; _shotKind = "wide:orbit"; CancelCutaway(); CancelPlan(); _inCutaway = false; float a0 = _orbitA; _orbitA += 1.1f; float r = _standR * 0.62f;
            Move(u =>
            {
                float a = a0 + Mathf.Lerp(0f, 1.1f, u); var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                return (_floorC - dir * r + Vector3.up * 2.3f, _floorC + dir * (_standR * 0.4f) + Vector3.up * 1.2f);
            }, 18f, fov, 0, false);
        }

        /// <summary>The house speaking (a result, a ruling, the room's own narration): never over someone's face. A candle on a podium,
        /// close, the court soft behind it; or, every other time, the ring from its rim.</summary>
        void ShotNeutral()
        {
            _subject = null; CancelCutaway(); CancelPlan(); _inCutaway = false;
            if ((_neutralN++ & 1) == 0 && CandleInsert()) { Probe("trial_cam_result"); return; }
            float a = _orbitA + 2.4f; var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); var side = Vector3.Cross(Vector3.up, dir);
            var p0 = _floorC - dir * (_standR * 0.8f) + Vector3.up * 3.0f - side * 0.6f; var p1 = p0 + side * 1.2f + Vector3.down * 0.15f;
            var l0 = _floorC + dir * (_standR * 0.7f) + Vector3.up * 1.2f; var l1 = l0 + side * 0.8f;
            _shotKind = "neutral"; Shot(p0, p1, PitchUp(p0, l0, 10f), PitchUp(p1, l1, 10f), 10f, 40, 0, true);
            Probe("trial_cam_result");
        }
        int _neutralN; bool _fovPathOn; float _fovPathFrom, _fovPathTo;
        bool CandleInsert()
        {
            var mv = _s.World?.Mansion; if (mv == null || T == null) return false;
            string near = _eyeSpeaker ?? _prevSpeaker; int seat = near != null && T.Seat.TryGetValue(near, out var st) ? st : (_neutralN * 7) % 18;
            var f = S.Layout.Furniture.FirstOrDefault(x => x.Type == "TrialStand" && x.Variant == seat); if (f == null) return false;
            var go = mv.FurnitureObject(f.Id); if (go == null) return false;
            var flame = go.transform.TransformPoint(new Vector3(f.W * 0.36f, f.H + 0.17f, -f.D * 0.1f));
            var toC = _floorC - flame; toC.y = 0; if (toC.sqrMagnitude < 1e-3f) return false; toC.Normalize();
            var side = Vector3.Cross(Vector3.up, toC);
            foreach (var s in new[] { 1f, -1f })
            {
                var cam = flame + toC * 0.5f + side * (0.18f * s) + Vector3.up * 0.04f;
                if (CineSolver.IsInside(cam)) continue;
                var look = Aim(cam, flame, 0.5f + 0.12f * s, 0.55f, 26f);
                var p1 = cam + (flame - cam) * 0.15f; var l1 = Aim(p1, flame, 0.5f + 0.12f * s, 0.55f, 26f);
                _shotKind = "candle"; Shot(cam, p1, look, l1, 6f, 26, 0, true, true, () => flame, 1.4f, 80f);
                return true;
            }
            return false;
        }
        void Probe(string name, float after = 0.35f) { if (!AutoProbe.Active || _inFocus || _modal.gameObject.activeSelf || _inGame) return; if (name == "trial_cam_result") { if (_probeResult) return; _probeResult = true; } StartCoroutine(ProbeShot(name, after, true)); }

        // ---------------------------------------------------------------- the speaker
        float SideOf(string id) { int h = 0; foreach (var ch in id ?? "") h = h * 31 + ch; return (h & 2) == 0 ? 1f : -1f; }
        bool Tense => T != null && (T.Mode == TrialMode.TM02_Crossfire || T.Mode == TrialMode.TM04_Chain || T.Mode == TrialMode.TM05_Theory || T.Mode == TrialMode.TM07_Reconstruct || T.Mode == TrialMode.TM08_FinalDefense || T.Stage == "Final");
        bool BeatIsAccusation(string id) => _beat != null && _beat.Speaker == id && (_beat.Key == "accuse" || _beat.Key == "accuse_player" || _beat.Key == "p_accuse");
        string AccusedOf(TrialBeat b) { if (b?.ClaimId == null || T == null) return null; var c = T.Claims.FirstOrDefault(x => x.Id == b.ClaimId); var t = c?.Accused ?? c?.Prop?.A; return t != null && t != b.Speaker && _s.World.ViewOf(t) != null ? t : null; }
        string _addressee;

        /// <summary>Frame a speaker. The same person keeps their shot (a dramatic line pushes in, a tense round turns slowly around them);
        /// a new speaker is a cut: over the shoulder of the one they answer if that one is turned toward them, otherwise a single aimed
        /// from the side the words go; a low push-in on an accusation; a close shot for any other dramatic line.</summary>
        public void ShotSpeaker(string id, bool dramatic)
        {
            if (id == Cast.Butler)
            {
                // Yusti speaks from the judge's seat and every one of his lines frames the seat (a cutaway never lingers over him)
                if (_subject == Cast.Butler && !_inCutaway && _dT0 >= 0) return;
                CancelPlan(); _inCutaway = false; ShotJudge(JudgeHead());
                if (AutoProbe.Active && !_probeJudgeLine && Time.unscaledTime > _craneUntil + 20f) { _probeJudgeLine = true; StartCoroutine(ProbeShot("trial_cam_judge_line", 1.2f, true)); }
                return;
            }
            var v = _s.World.ViewOf(id); if (v == null || !AtCourt(v.HeadPos)) { if (_subject != null) ShotWide(); return; }   // nobody to frame at a podium: hold the room
            if (id == _subject && _dT0 >= 0 && !_inCutaway)
            {
                if (!dramatic)
                {
                    if (Tense && Time.unscaledTime - _dT0 > 3f) { OrbitHold(id); return; }
                    _dDur += 2.5f; return;
                }
                Single(id, _addressee, 0.66f, false, 0f, 3.5f, 0.9f, 0.66f);   // push in, no cut
                return;
            }
            var prev = _subject != Cast.Butler ? _subject : null; if (_inCutaway && prev == id) prev = _addressee;
            _subject = id; CancelCutaway(); CancelPlan(); _inCutaway = false; _changeN++;
            var accused = AccusedOf(_beat);
            _addressee = accused ?? (prev != id ? prev : null);
            if (dramatic && BeatIsAccusation(id)) { ShotAccuse(id, accused); return; }
            if (dramatic)
            {
                _dramaN++;
                float sg = SideOf(id);
                if (!Single(id, _addressee, 0.62f, true, sg * 2.5f, 4f, 0.82f, 0.66f, 1f, -1f, 2f)) ShotWide();
                if (_beat != null && _beat.Kind == "break")
                {
                    _impactAmt = 1f; _impactT0 = Time.unscaledTime; if (!_s.Headless) _freezeUntil = Time.unscaledTime + (ProbeFast ? 0.2f : 0.42f);
                    if (AutoProbe.Active && !_probeBreak) { _probeBreak = true; StartCoroutine(ProbeShot("trial_cam_break", 0.12f, true)); }
                }
                else if (_dramaN % 2 == 0 && Time.unscaledTime - _lastCutaway > 8f) { var hp = v.HeadPos; PlanCutaway(() => ShotGallery(hp)); }
                return;
            }
            if (prev != null && OverShoulder(id, prev))
            {
                if (AutoProbe.Active && !_probeOts) { _probeOts = true; StartCoroutine(ProbeShot("trial_cam_ots", 2.2f, true)); }
            }
            else if (!Single(id, _addressee, 1.0f, true, 0f)) { ShotWide(); return; }
            else if (AutoProbe.Active && !_probeSpeaker && _changeN > 2) { _probeSpeaker = true; StartCoroutine(ProbeShot("trial_cam_speaker", 1.6f, true)); }
            // now and then, once the line is read: the one who was just answered, listening
            if (prev != null && _changeN % 4 == 0 && Time.unscaledTime - _lastCutaway > 18f)
            {
                var who = prev; PlanCutaway(() => { ShotReact(who, false); if (AutoProbe.Active && !_probeReact) { _probeReact = true; StartCoroutine(ProbeShot("trial_cam_react", 0.4f, false)); } });
            }
        }

        /// <summary>An accusation: from low in front of the accuser (the rail in the foreground), pushing in and up; once the line is
        /// read, the accused's face.</summary>
        void ShotAccuse(string id, string accused)
        {
            float sg = SideOf(id);
            bool ok = Single(id, accused, 1.15f, true, sg * 4f, 4.6f, 0.7f, 0.66f, 1f, 0.95f, 3.2f) || Single(id, accused, 1.0f, true, sg * 3f, 4.6f, 0.75f, 0.66f);
            if (!ok) { ShotWide(); return; }
            _rollTarget = sg * 6f; _fovRate = 0.9f;
            if (accused != null) { var who = accused; PlanCutaway(() => ShotReact(who, true)); }
            if (AutoProbe.Active && !_probeAccuse) { _probeAccuse = true; StartCoroutine(ProbeShot("trial_cam_accuse", 1.0f, true)); }
        }

        /// <summary>Is this point really on the court floor (a speaker at a podium, not somewhere upstairs)?</summary>
        bool AtCourt(Vector3 h) { var d = h - _floorC; float y = d.y; d.y = 0; return d.magnitude < _courtR + 1.5f && y > -1f && y < 4.5f; }

        /// <summary>A face, close: the accused flinching, or a listener (looking toward the one speaking).</summary>
        void ShotReact(string id, bool hard)
        {
            var v = _s.World.ViewOf(id); if (v == null || !AtCourt(v.HeadPos)) return;
            Single(id, _subject, 0.72f, true, hard ? -SideOf(id) * 3f : 0f, 1.6f, 0.94f, 0.64f, -1f, -1f, 2f);
        }

        /// <summary>Cut to the gallery: a knot of eyes in the balcony on the speaker's side, near eye level (never straight up), telephoto.</summary>
        void ShotGallery(Vector3 toward)
        {
            if (_eyePts.Count == 0) CollectEyes(); if (_eyePts.Count == 0) return;
            var dir = toward - _floorC; dir.y = 0; if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward; dir.Normalize();
            var from = _floorC + dir * (_standR * 0.2f) + Vector3.up * 1.5f;
            Vector3 best = Vector3.zero; float bs = float.MinValue; bool any = false;
            foreach (var p in _eyePts)
            {
                var d = p - from; float hz = new Vector2(d.x, d.z).magnitude; if (hz < 0.5f) continue;
                if (Mathf.Atan2(d.y, hz) * Mathf.Rad2Deg > 25f) continue;   // no looking straight up into the vault
                var dd = p - _floorC; dd.y = 0; float s = Vector3.Dot(dd.normalized, dir) * 2f - Mathf.Abs(dd.magnitude - _courtR) * 0.05f;
                if (s > bs) { bs = s; best = p; any = true; }
            }
            if (!any) return;
            var to = best - from; float dist = to.magnitude; var pt = best;
            _shotKind = "gallery"; Shot(from, from + to.normalized * Mathf.Min(1.2f, dist * 0.2f), pt, pt + Vector3.up * 0.05f, 2.6f, 20, 0, true, true, () => pt, 2.8f, 85f);
            if (AutoProbe.Active && !_probeGallery) { _probeGallery = true; StartCoroutine(ProbeShot("trial_cam_gallery", 0.5f, false)); }
        }
        void CollectEyes()
        {
            var c = _s.World?.Court; if (c == null || c.Eyes == null) return;
            foreach (var r in c.Eyes.GetComponentsInChildren<Renderer>(true))
            {
                var b = r.bounds; if (b.size.magnitude > 3f || b.center.y < _floorC.y + 2.2f) continue;
                var d = b.center - _floorC; d.y = 0; if (d.magnitude < _courtR * 0.5f) continue;
                _eyePts.Add(b.center); if (_eyePts.Count > 400) break;
            }
        }

        /// <summary>Back to the speaker after a cutaway: a fresh angle from the other side.</summary>
        void Return(string id, bool low = false)
        {
            if (_subject != id) return;
            if (!Single(id, _addressee, low ? 0.9f : 1.0f, true, 0f, 6f, 0.88f, 0.64f, -1f)) Single(id, _addressee, 1.0f, true, 0f);
        }

        /// <summary>A tense round and the same person still talking: turn slowly around them, re-aimed every frame so they stay framed.</summary>
        void OrbitHold(string id)
        {
            var v = _s.World.ViewOf(id); if (v == null) return; var head = v.HeadPos;
            var off = _camPos - head; float h = off.y; off.y = 0; float r = Mathf.Clamp(off.magnitude, 1.3f, 2.8f); if (off.sqrMagnitude < 0.01f) off = FaceDir(v, head); off.Normalize();
            float sg = SideOf(id); float fov = _fovTarget;
            var sp = Proj(_camPos, _camLook, fov, head, out _); float sx = Mathf.Clamp(sp.x, 0.35f, 0.65f), sy = Mathf.Clamp(sp.y, 0.58f, 0.68f);
            var end = head + (Quaternion.AngleAxis(sg * 16f, Vector3.up) * off) * r + Vector3.up * h;
            if (!SightClear(end, head, v)) return;   // the far end of the turn is blocked: hold still instead
            var vv = v;
            _shotKind = "orbit"; Move(u => { var p = head + (Quaternion.AngleAxis(sg * 16f * u, Vector3.up) * off) * r + Vector3.up * h; return (p, Aim(p, vv != null ? vv.HeadPos : head, sx, sy, fov)); }, 9f, fov, _rollTarget, false, true, () => vv != null ? vv.HeadPos : head, 2.6f, 58f);
            _bDur = 0.4f;
        }

        /// <summary>Rounds ask for a speaker; style 4 is the dramatic one. The camera still only moves when the speaker changes.</summary>
        public void CamCut(string id, int style, float dur)
        {
            if (id == null) return; bool drama = ((style % 6) + 6) % 6 == 4;
            if (id == _subject && _dT0 >= 0 && !drama) { _dDur += 2f; return; }
            ShotSpeaker(id, drama);
        }

        /// <summary>A round opens (its title card and teaching card go over this): one of the lighter wides, not the high view over the
        /// whole lit floor or the judge wide (GPU-bound views of the whole island: 26–29 ms in cine18, 36–53 ms in court2).</summary>
        public void CamWide(float fov = 50) { Wide(fov, true, 0.85f, true); }
        public void CamLookCenterFrom(Vector3 offset, float fov) { _subject = null; CancelCutaway(); CancelPlan(); _inCutaway = false; Shot(_center + offset, _center + offset * 0.95f, _center, _center, 8f, fov, 0, false); }
        public Vector3 Center => _center;

        // ---- the judge's seat: the butler sits there, silent and still, for the whole trial; he speaks only the verdict
        Transform JudgeAnchor()
        {
            var c = _s.World?.Court; if (c == null) return null; var t = c.GetType();
            var p = t.GetProperty("JudgeAnchor"); if (p != null) return p.GetValue(c) as Transform;
            var f = t.GetField("JudgeAnchor"); return f != null ? f.GetValue(c) as Transform : null;
        }
        Vector3 JudgeHead()
        {
            var v = _s.World.ViewOf(Cast.Butler); if (v != null && v.gameObject.activeInHierarchy) return v.HeadPos;
            var a = JudgeAnchor(); return a != null ? a.position + Vector3.up * 1.25f : _center + Vector3.up;
        }
        /// <summary>The judge framed from in front of the dais, a little below his eyes, his head on the upper third.</summary>
        void JudgeFrame(Vector3 head, out Vector3 p0, out Vector3 p1, out Vector3 l0, out Vector3 l1, out float fov, float push = 0.1f)
        {
            // on the court's judge-wide line (from low on the far side) but moved in to the middle of the floor: not the flat crop
            // of the window any more — his seat, the throne and the lancet rising behind him, the pier trunks either side, his
            // figure a quarter of the frame and his head above the dialogue box; the lens creeps toward him
            var cv = _s.World?.Court;
            if (cv != null && cv.WellShot("judge_wide", out var wp, out _, out _))
            {
                var to = head - wp; to.y = 0f; var ign0 = new HashSet<string> { Cast.Butler };
                foreach (var (k, lift) in new[] { (0.45f, 0.35f), (0.45f, 0.9f), (0.3f, 0.6f), (0.2f, 0.4f) })
                {
                    var a = wp + to * k; a.y = wp.y + lift; a = InCourt(a);
                    if (!CineSolver.Clear(head, a, ign0, 0.1f)) continue;
                    fov = 40f; p0 = a; p1 = InCourt(wp + to * Mathf.Min(0.8f, k + push) + Vector3.up * (lift + 0.1f));
                    l0 = Aim(p0, head, 0.5f, 0.42f, fov); l1 = Aim(p1, head, 0.5f, 0.42f, fov);
                    return;
                }
            }
            var toC = _floorC - head; toC.y = 0; if (toC.sqrMagnitude < 0.001f) toC = Vector3.forward; toC.Normalize(); var side = Vector3.Cross(Vector3.up, toC);
            var ign = new HashSet<string> { Cast.Butler }; bool found = false; p0 = p1 = Vector3.zero;
            foreach (var d in new[] { 4.6f, 5.6f, 6.8f, 8f })
            {
                var a = head + toC * d + side * 0.5f; a.y = Mathf.Max(_floorC.y + 1.2f, head.y - 0.7f);
                var b = head + toC * (d * 0.62f) + side * 0.25f; b.y = Mathf.Max(_floorC.y + 1.3f, head.y - 0.35f);
                if (CineSolver.Clear(head, InCourt(a), ign, 0.1f) && CineSolver.Clear(head, InCourt(b), ign, 0.1f)) { p0 = InCourt(a); p1 = InCourt(b); found = true; break; }
            }
            if (!found) { p0 = InCourt(head + toC * 6f + side * 0.6f + Vector3.up * 0.6f); p1 = InCourt(head + toC * 4.2f + side * 0.3f + Vector3.up * 0.3f); }
            fov = FovFor(1.5f, Vector3.Distance(p0, head)); fov = Mathf.Clamp(fov, 18f, 34f);
            l0 = Aim(p0, head, 0.5f, 0.62f, fov); l1 = Aim(p1, head, 0.5f, 0.62f, fov);
        }
        /// <summary>A slow push-in on the judge's seat from below: the throne looms.</summary>
        void ShotJudge(Vector3 head)
        {
            _subject = Cast.Butler; CancelCutaway(); CancelPlan(); _inCutaway = false; _shotKind = "judge";
            // the verdict creeps in further; a wide, so no depth of field
            JudgeFrame(head, out var p0, out var p1, out var l0, out var l1, out var fov, _verdictShown ? 0.2f : 0.1f);
            Shot(p0, p1, l0, l1, _verdictShown ? 8f : 9f, fov, 0, true, false, null, 4f, 50f);
        }

        // ---- the gallery of eyes (CourtroomView owns them; bound late so the court can grow them without breaking this file)
        System.Reflection.MethodInfo _eWatch, _eStare; bool _eyesChecked;
        void EyesBind() { if (_eyesChecked) return; _eyesChecked = true; var t = typeof(CourtroomView); _eWatch = t.GetMethod("Watch", new[] { typeof(Vector3) }); _eStare = t.GetMethod("Stare", new[] { typeof(float) }); }
        /// <summary>All the gallery's eyes turn toward a point (the speaker's head).</summary>
        public void EyesWatch(Vector3 p) { EyesBind(); var c = _s.World?.Court; if (c == null || _eWatch == null) return; try { _eWatch.Invoke(c, new object[] { p }); } catch (Exception) { } }
        /// <summary>The eyes widen, pupils tighten (0..1): contradictions, accusations, the vote, the verdict.</summary>
        public void EyesStare(float k) { EyesBind(); var c = _s.World?.Court; if (c == null || _eStare == null) return; try { _eStare.Invoke(c, new object[] { Mathf.Clamp01(k) }); } catch (Exception) { } }

        /// <summary>A hit (a contradiction, a lapse): the lens snaps tighter and the room holds its breath for a moment, then eases
        /// back. (Kept under its old name; there is no random shake any more.)</summary>
        public void ShakeCam(float amt = 0.35f)
        {
            _impactAmt = Mathf.Clamp(amt * 2.6f, 0.35f, 1f); _impactT0 = Time.unscaledTime;
            if (!_s.Headless && !ProbeFast) _freezeUntil = Time.unscaledTime + 0.12f + 0.3f * _impactAmt;
            else if (!_s.Headless) _freezeUntil = Time.unscaledTime + 0.12f;
        }

        /// <summary>The debate itself is on screen (no round, no card, no pick running): camera moments can be staged.</summary>
        public bool CamIdle => Active && !_inGame && _busy == 0 && !_inFocus;

        /// <summary>Probe only: stage a contradiction hit on whoever holds the floor (snap-zoom + held breath) and photograph it.</summary>
        public void ProbeBreak()
        {
            if (!AutoProbe.Active || _probeBreak || T == null) return;
            var id = _subject != null && _subject != Cast.Butler ? _subject : T.Participants.FirstOrDefault(x => x != Cast.Player && _s.World.ViewOf(x) != null && AtCourt(_s.World.ViewOf(x).HeadPos));
            var v = id != null ? _s.World.ViewOf(id) : null; if (v == null) return;
            _probeBreak = true; _subject = id; CancelPlan(); _inCutaway = false;
            if (!Single(id, _addressee, 0.62f, true, SideOf(id) * 2.5f, 4f, 0.82f, 0.66f, 1f, -1f, 2f)) return;
            TrialFx.Sound("break", 0.6f); EyesStare(1f);
            _impactAmt = 1f; _impactT0 = Time.unscaledTime; _freezeUntil = Time.unscaledTime + 0.42f;
            StartCoroutine(ProbeShot("trial_cam_break", 0.2f, true));
        }

        IEnumerator ProbeShot(string name, float after, bool hold)
        {
            if (hold) _busy++;
            float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < after) yield return null;
            AutoProbe.Shot(name); Debug.Log($"[CINE] {name} cam {_cam.transform.position:F2} fwd {_cam.transform.forward:F2} fov {_cam.fieldOfView:F1} floor {_floorC:F2} R {_courtR:F1} standR {_standR:F1} top {CourtTop():F1} subject {_subject} head {(_subject != null && _s.World.ViewOf(_subject) != null ? _s.World.ViewOf(_subject).HeadPos.ToString("F2") : "-")} timeScale {Time.timeScale}");
            yield return new WaitForSecondsRealtime(0.25f);
            if (hold) _busy = Mathf.Max(0, _busy - 1);
        }

        void UpdateCam(float dt)
        {
            _camT += dt;
            // the held breath (and never leave the world stopped)
            bool freeze = Active && Time.unscaledTime < _freezeUntil;
            if (freeze && !_frozeTime) { _frozeTime = true; Time.timeScale = 0f; }
            else if (!freeze && _frozeTime) { _frozeTime = false; Time.timeScale = 1f; }
            // the opening tilt is slow on purpose; a key press hurries the rest of it (1.6 s) instead of cutting it off
            if (_openingTilt && Time.unscaledTime < _craneUntil && Time.unscaledTime - _openingT0 > 0.6f && !AutoProbe.Active && _dT0 >= 0 && _path != null && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)))
            {
                float k0 = Mathf.Clamp01((Time.unscaledTime - _dT0) / _dDur);
                if (k0 < 0.92f) { const float Rest = 1.6f; _dDur = Rest / Mathf.Max(0.05f, 1f - k0); _dT0 = Time.unscaledTime - k0 * _dDur; _craneUntil = Time.unscaledTime + Rest; if (_cutAt > 0) _cutAt = _craneUntil; }
            }
            if (_cutAt > 0 && Time.unscaledTime >= _cutAt) { var a = _cutAction; CancelCutaway(); try { a?.Invoke(); } catch (Exception e) { Debug.LogException(e); } }
            // a line with no speaker (a result, a ruling, the room's narration) never plays over someone's face
            if (_beat != null && _beat != _camBeat)
            {
                _camBeat = _beat;
                if (_beat.Speaker == null && !_inGame && !_verdictShown && Active && (_beat.Kind == "result" || _beat.Kind == "system" || _beat.Kind == "line" || _beat.Kind == "prompt")) ShotNeutral();
            }
            UpdateCutaways();
            Vector3 p, l;
            if (_dT0 >= 0 && _path != null)
            {
                float lin = Mathf.Clamp01((Time.unscaledTime - _dT0) / _dDur), k = _pathLinear ? lin : Ease(lin); (p, l) = _path(k);
                if (_fovFunc != null) { _fovBase = _fovFunc(k); _fovTarget = _fovBase; }
                else if (_fovPathOn) { _fovBase = Mathf.Lerp(_fovPathFrom, _fovPathTo, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, k))); _fovTarget = _fovBase; if (k >= 1f) _fovPathOn = false; }
            }
            else { p = Vector3.Lerp(_camPos, InCourt(_camWantPos), 1f - Mathf.Exp(-2.5f * dt)); l = Vector3.Lerp(_camLook, _camWantLook, 1f - Mathf.Exp(-3f * dt)); _dofOn = false; }
            if (_bT0 >= 0)
            {
                float b = Mathf.Clamp01((Time.unscaledTime - _bT0) / _bDur); b = b * b * b * (b * (b * 6 - 15) + 10);   // smootherstep
                var dFrom = (_bFromL - _bFromP); var dTo = (l - p); float lenF = dFrom.magnitude, lenT = dTo.magnitude;
                var dir = Vector3.Slerp(dFrom / Mathf.Max(0.001f, lenF), dTo / Mathf.Max(0.001f, lenT), b);
                p = Vector3.Lerp(_bFromP, p, b); l = p + dir * Mathf.Lerp(lenF, lenT, b);
                if (b >= 1) _bT0 = -1;
            }
            _camPos = p; _camLook = l;
            if (_fovBase <= 0f) _fovBase = _cam.fieldOfView; _fovBase = Mathf.Lerp(_fovBase, _fovTarget, 1f - Mathf.Exp(-_fovRate * dt)); _roll = Mathf.Lerp(_roll, _rollTarget, 1f - Mathf.Exp(-2f * dt));
            if (_fovRate > 20f && Mathf.Abs(_fovBase - _fovTarget) < 0.05f) _fovRate = 2f;
            // impact: snap tighter within a tenth of a second, hold through the breath, ease back
            float it = Time.unscaledTime - _impactT0, snap = 0f;
            if (it >= 0f && it < 2f) snap = it < 0.09f ? it / 0.09f : Mathf.Max(0f, _freezeUntil - Time.unscaledTime) > 0f ? 1f : Mathf.Exp(-(it - Mathf.Max(0.09f, _freezeUntil - _impactT0)) * 2.6f);
            float fov = _fovBase * (1f - 0.24f * _impactAmt * snap);
            var fwd = (_camLook - _camPos); if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward; fwd.Normalize();
            var pos = _camPos + fwd * (0.1f * _impactAmt * snap);
            _cam.transform.position = pos; _cam.transform.rotation = Quaternion.LookRotation(fwd) * Quaternion.Euler(0, 0, _roll);
            _cam.fieldOfView = fov;
            if (_dofOn)
            {
                // focus on the shot's subject if it really is in frame; otherwise on whichever face is nearest the centre of frame
                Vector3? f = _focusOf != null ? _focusOf() : (Vector3?)null;
                if (f == null || !InFrame(f.Value, 0.4f)) f = HeadNearCentre();
                if (f != null) CineDof.Set(_cam, true, Mathf.Max(0.3f, Vector3.Dot(f.Value - pos, fwd)), Mathf.Max(3.2f, _dofAperture), Mathf.Min(55f, _dofFocal)); else CineDof.Off(_cam);
            }
            else CineDof.Off(_cam);
            SpeakerLights(dt); Eyelines(dt); ProbeFrame(Time.unscaledDeltaTime);
        }
        float _fovBase = -1f;

        bool InFrame(Vector3 p, float half)
        {
            var v = _cam.WorldToViewportPoint(p); return v.z > 0.2f && Mathf.Abs(v.x - 0.5f) < half && Mathf.Abs(v.y - 0.5f) < half + 0.08f;
        }
        Vector3? HeadNearCentre()
        {
            Vector3? best = null; float bd = 0.36f;
            IEnumerable<string> ids = T != null ? T.Participants.Concat(new[] { Cast.Butler }) : new[] { Cast.Butler };
            foreach (var id in ids)
            {
                var v = _s.World.ViewOf(id); if (v == null || !v.gameObject.activeInHierarchy) continue;
                var h = v.HeadPos; var vp = _cam.WorldToViewportPoint(h); if (vp.z < 0.25f) continue;
                float d = new Vector2(vp.x - 0.5f, (vp.y - 0.55f) * 0.8f).magnitude; if (d < bd) { bd = d; best = h; }
            }
            return best;
        }

        /// <summary>The trial is over (or this object goes away): the world never stays holding its breath.</summary>
        void CamEnd()
        {
            ProbePerfReport();
            CancelCutaway(); CancelPlan(); _inCutaway = false; _freezeUntil = -1f; if (_frozeTime) { _frozeTime = false; Time.timeScale = 1f; } if (_cam != null) CineDof.Off(_cam);
            if (_fill != null) _fill.enabled = false; if (_back != null) _back.enabled = false;
            // the court's eyelines end with the court (the reveal poses people itself and never resets them)
            if (_s?.World != null) foreach (var v in _s.World.Actors.Values) v.Rig?.Anim?.SetLookAt(null);
            _eyes.Clear(); _eyeSpeaker = _prevSpeaker = null;
        }
        void OnDisable() { if (_frozeTime) { _frozeTime = false; Time.timeScale = 1f; } }

        /// <summary>The look from p to l, tilted up by 'deg' (never past 'maxPitch' above the horizon).</summary>
        static Vector3 PitchUp(Vector3 p, Vector3 l, float deg, float maxPitch = 72f)
        {
            var d = l - p; float len = d.magnitude; if (len < 1e-3f) return l; d /= len;
            var r = Vector3.Cross(Vector3.up, d); if (r.sqrMagnitude < 1e-6f) return l; r.Normalize();
            float pitch = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg, add = Mathf.Min(deg, maxPitch - pitch);
            return add <= 0f ? l : p + (Quaternion.AngleAxis(-add, r) * d) * len;
        }

        // ---- the well's beats (CourtroomView.Beat): the vote once the urns close, the verdict line, the execution. After each, a
        // few seconds straight up the well with no UI at all, so the eyes waking band by band up the height and the Black Sun's
        // pupil closing are actually seen; then back to whoever the moment belongs to (the caller re-frames).
        float _wellElapsed;
        public void CourtBeat(string key) { try { _s.World?.Court?.Beat(key); } catch (Exception e) { Debug.LogException(e); } }
        public IEnumerator WellBeat(string key, float secs, bool fire = true)
        {
            _wellElapsed = 0f;
            if (fire) CourtBeat(key);
            var cv = _s.World?.Court;
            if (cv == null || _s.Headless || !cv.WellShot("verdict_up", out var p, out var l, out var f)) yield break;
            bool c0 = _c.enabled, g0 = _gc.enabled, h0 = _hc.enabled, x0 = _fc.enabled;
            _c.enabled = false; _gc.enabled = false; _hc.enabled = false; _fc.enabled = false;
            _subject = null; CancelCutaway(); CancelPlan(); _inCutaway = false; _shotKind = "well";
            // held nearly still (a breath of rise), no roll, no depth of field
            Shot(p, p + Vector3.up * 0.2f, l, l, secs + 0.6f, f, 0, true, false, null, 2.8f, 55f, true);
            float t0 = Time.unscaledTime, len = ProbeFast ? Mathf.Min(secs, 4.2f) : secs; bool shot = false;
            while (Time.unscaledTime - t0 < len)
            {
                if (AutoProbe.Active && !shot && Time.unscaledTime - t0 > len * 0.8f) { shot = true; AutoProbe.Shot("trial_well_" + key); Debug.Log($"[CINE] well beat {key}: {cv.WellState()}"); }
                if (!AutoProbe.Active && Time.unscaledTime - t0 > 1f && (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))) break;
                yield return null;
            }
            _wellElapsed = Time.unscaledTime - t0;
            _c.enabled = c0; _gc.enabled = g0; _hc.enabled = h0; _fc.enabled = x0;
        }

        // ---- probe: where the frame time goes, per kind of shot, with and without an overlay card / a round on screen
        // (the court reported 36–53 ms under the round intros and question cards, 7–13 ms for its own well vistas)
        readonly Dictionary<string, double[]> _ftBuckets = new Dictionary<string, double[]>();
        Unity.Profiling.ProfilerRecorder _recGpu, _recMain; bool _recOn;
        void ProbeFrame(float dt)
        {
            if (!AutoProbe.Active || !Active || dt <= 0f || dt > 0.5f) return;
            if (!_recOn)
            {
                _recOn = true;
                try { _recGpu = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "GPU Frame Time", 1); _recMain = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "CPU Main Thread Frame Time", 1); } catch (Exception) { }
            }
            bool card = FxRoot != null && FxRoot.childCount > 0, round = _inGame;
            string key = _shotKind + (_dofOn ? " +dof" : "") + (card ? " +card" : "") + (round ? " +round" : "");
            if (!_ftBuckets.TryGetValue(key, out var b)) _ftBuckets[key] = b = new double[5];
            b[0] += dt; b[1] += 1; if (dt > b[4]) b[4] = dt;
            try { if (_recGpu.Valid && _recGpu.LastValue > 0) b[2] += _recGpu.LastValue * 1e-6; if (_recMain.Valid && _recMain.LastValue > 0) b[3] += _recMain.LastValue * 1e-6; } catch (Exception) { }
        }
        void ProbePerfReport()
        {
            if (!AutoProbe.Active || _ftBuckets.Count == 0) return;
            var sb = new System.Text.StringBuilder("[CINE] trial frame time by shot (avg ms / frames / gpu ms / cpu-main ms / max ms):\n");
            foreach (var kv in _ftBuckets.OrderByDescending(x => x.Value[0] / Math.Max(1, x.Value[1])))
            {
                var b = kv.Value; double n = Math.Max(1, b[1]);
                sb.Append($"  {kv.Key,-32} {1000 * b[0] / n,6:0.0} ms  {b[1],6:0}  gpu {(b[2] > 0 ? (b[2] / n).ToString("0.0") : "n/a"),5}  main {(b[3] > 0 ? (b[3] / n).ToString("0.0") : "n/a"),5}  max {1000 * b[4]:0}\n");
            }
            Debug.Log(sb.ToString()); _ftBuckets.Clear();
            try { if (_recGpu.Valid) _recGpu.Dispose(); if (_recMain.Valid) _recMain.Dispose(); } catch (Exception) { } _recOn = false;
        }

        // ---- blocking: where everyone looks. The one speaking looks at whoever they answer (or accuse); the room turns to the
        // speaker, each head a beat apart; on an accusation a few look at the accused instead. The judge only watches the verdict.
        readonly Dictionary<string, (Vector3? cur, Vector3? next, float at)> _eyes = new Dictionary<string, (Vector3?, Vector3?, float)>();
        string _eyeSpeaker, _prevSpeaker;
        static int H(string s) { int h = 7; foreach (var ch in s ?? "") h = h * 31 + ch; return h & 0x7fffffff; }
        void Eyelines(float dt)
        {
            if (T == null || _s.World == null || !Active) return;
            string speaker = _beat?.Speaker ?? (_subject != Cast.Butler ? _subject : null);
            var sv = speaker != null ? _s.World.ViewOf(speaker) : null; if (sv != null && !AtCourt(sv.HeadPos)) sv = null;
            if (speaker != _eyeSpeaker) { if (_eyeSpeaker != null && _eyeSpeaker != Cast.Butler) _prevSpeaker = _eyeSpeaker; _eyeSpeaker = speaker; }
            string accused = AccusedOf(_beat); var av = accused != null ? _s.World.ViewOf(accused) : null; if (av != null && !AtCourt(av.HeadPos)) av = null;
            var pv = _prevSpeaker != null && _prevSpeaker != speaker ? _s.World.ViewOf(_prevSpeaker) : null; if (pv != null && !AtCourt(pv.HeadPos)) pv = null;
            Vector3 inward = _floorC + Vector3.up * 1.45f;
            foreach (var id in T.Participants)
            {
                var v = _s.World.ViewOf(id); var an = v?.Rig?.Anim; if (an == null || !AtCourt(v.HeadPos)) continue;
                Vector3? want;
                if (sv == null) want = inward;                                                   // nobody holds the floor: into the ring
                else if (id == speaker) want = av != null ? av.HeadPos : pv != null ? pv.HeadPos : inward;
                else if (av != null && id != accused && H(id) % 3 == 0) want = av.HeadPos;      // the room turns to the accused
                else want = sv.HeadPos;
                if (!_eyes.TryGetValue(id, out var e)) e = (want, want, 0f);
                if (!Same(e.next, want)) { e.next = want; e.at = Time.unscaledTime + (id == speaker ? 0.05f : 0.12f + (H(id) % 7) * 0.07f); }
                if (Time.unscaledTime >= e.at) e.cur = e.next;
                _eyes[id] = e;
                an.SetLookAt(e.cur);
            }
            // the judge sits still and faces the court; at the verdict he looks at the accused
            var jv = _s.World.ViewOf(Cast.Butler); if (jv?.Rig?.Anim != null) jv.Rig.Anim.SetLookAt(_verdictShown && av != null ? av.HeadPos : (Vector3?)inward);
        }
        static bool Same(Vector3? a, Vector3? b) => a.HasValue == b.HasValue && (!a.HasValue || (a.Value - b.Value).sqrMagnitude < 0.04f);

        // ---- portrait lighting: a soft warm key from the camera side and a cool rim behind whoever holds the floor
        Light _key, _rim, _fill, _back; float _keyK;
        void SpeakerLights(float dt)
        {
            if (_key == null)
            {
                _key = new GameObject("TrialKeyLight").AddComponent<Light>(); _key.type = LightType.Spot; _key.spotAngle = 44f; _key.innerSpotAngle = 16f; _key.range = 9f; _key.color = new Color(1f, 0.9f, 0.8f); _key.shadows = LightShadows.None; _key.intensity = 0;
                _rim = new GameObject("TrialRimLight").AddComponent<Light>(); _rim.type = LightType.Point; _rim.range = 3.0f; _rim.color = new Color(0.62f, 0.72f, 1f); _rim.shadows = LightShadows.None; _rim.intensity = 0;
                // a soft fill from the lens side (no half-lit faces) and a dim warm glow on whatever stands behind the subject (no black void)
                _fill = new GameObject("TrialFillLight").AddComponent<Light>(); _fill.type = LightType.Point; _fill.range = 3.6f; _fill.color = new Color(0.92f, 0.9f, 0.95f); _fill.shadows = LightShadows.None; _fill.intensity = 0;
                _back = new GameObject("TrialBackLight").AddComponent<Light>(); _back.type = LightType.Point; _back.range = 5.5f; _back.color = new Color(1f, 0.82f, 0.66f); _back.shadows = LightShadows.None; _back.intensity = 0;
                foreach (var l in new[] { _key, _rim, _fill, _back }) DontDestroyOnLoad(l.gameObject);
            }
            var v = _subject != null ? _s.World.ViewOf(_subject) : null;
            _keyK = Mathf.MoveTowards(_keyK, Active && v != null ? 1f : 0f, dt * 1.5f);
            if (v != null)
            {
                var head = v.HeadPos; var toCam = _camPos - head; toCam.y = 0; if (toCam.sqrMagnitude < 1e-4f) toCam = Vector3.forward; toCam.Normalize();
                var right = Vector3.Cross(Vector3.up, toCam); float k = 1f - Mathf.Exp(-4f * dt);
                var kp = head + toCam * 2.2f + right * 1.1f + Vector3.up * 0.9f;
                _key.transform.position = Vector3.Lerp(_key.transform.position, kp, k); _key.transform.rotation = Quaternion.LookRotation(head + Vector3.down * 0.35f - _key.transform.position);
                _rim.transform.position = Vector3.Lerp(_rim.transform.position, head - toCam * 0.75f - right * 0.35f + Vector3.up * 0.35f, k);
                _fill.transform.position = Vector3.Lerp(_fill.transform.position, head + toCam * 1.3f - right * 0.8f + Vector3.up * 0.1f, k);
                _back.transform.position = Vector3.Lerp(_back.transform.position, head - toCam * 2.6f + right * 0.6f + Vector3.up * 0.8f, k);
            }
            _key.intensity = 2.4f * _keyK; _rim.intensity = 2.6f * _keyK; _fill.intensity = 1.1f * _keyK; _back.intensity = 0.9f * _keyK;
            _key.enabled = _rim.enabled = _fill.enabled = _back.enabled = _keyK > 0.01f;
        }
    }
}
