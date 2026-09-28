using System.Collections.Generic;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;
using Pose = BL23.Sim.Pose;

namespace BL23.Game
{
    /// <summary>
    /// Constant small signs of life layered over what the kernel says a person is doing (presentation only: nothing here
    /// changes the simulation). People turn to see who walks into the room: a wave for a friend, a flinch for someone
    /// they fear, folded arms for someone they resent. They glance at whoever they pass and nod to people they like.
    /// They nod along while their partner talks, and while idle they fidget according to mood and temperament: the
    /// grieving wipe their eyes, the frightened look over their shoulder, the proud fold their arms, the curious look around.
    /// </summary>
    public sealed class IdleLife
    {
        float _next = -1f, _scanNext, _glanceNext, _nodNext, _lookUntil;
        Vector3 _look; int _room = int.MinValue;
        readonly HashSet<string> _inRoom = new HashSet<string>();
        readonly Dictionary<string, float> _greeted = new Dictionary<string, float>();

        static float H(string id, int salt) { unchecked { int h = 17 + salt * 31; foreach (var c in id) h = h * 31 + c; return (h & 0xffff) / 65535f; } }

        public Vector3? Tick(ActorView v, Actor a, ActorAnimator an, GameState S, WorldPresenter W, float dt)
        {
            float t = Time.time;
            if (_next < 0f) { _next = t + 2f + H(a.Id, 1) * 6f; _scanNext = t + H(a.Id, 2); }
            bool moving = v.Velocity.sqrMagnitude > 0.09f;
            bool seated = a.Pose == Pose.Sit, lying = a.Pose == Pose.Sleep || a.Pose == Pose.LieBack || a.Pose == Pose.LieFront || a.Pose == Pose.LieSide;
            bool free = (a.Anim == Anim.Idle || a.Anim == Anim.None) && an.CurrentAction == ActionAnim.None && an.CurrentGesture == Gesture.None;
            if (lying) return null;

            // 1) someone new walks into the room
            if (t >= _scanNext)
            {
                _scanNext = t + 0.45f;
                if (a.Room != _room) { _room = a.Room; _inRoom.Clear(); foreach (var o in S.Actors.Values) if (o != a && o.Alive && o.Room == a.Room) _inRoom.Add(o.Id); }
                else
                {
                    string newcomer = null;
                    foreach (var o in S.Actors.Values) { if (o == a || !o.Alive || o.Room != a.Room) continue; if (_inRoom.Add(o.Id) && newcomer == null) newcomer = o.Id; }
                    _inRoom.RemoveWhere(id => { var o = S.A(id); return o == null || !o.Alive || o.Room != a.Room; });
                    if (newcomer != null && a.TalkingTo == null)
                    {
                        var nv = W.ViewOf(newcomer); var o = S.A(newcomer);
                        if (nv != null && o != null && Vector3.Distance(nv.HeadPos, v.HeadPos) < (o.IsPlayer ? 6f : 10f) && H(a.Id + newcomer, (int)t) < (o.IsPlayer ? 0.18f + a.Def.P.Curiosity * 0.2f : 0.55f + a.Def.P.Curiosity * 0.4f))   // the player walking in turns a head or two, not the whole room
                        {
                            Look(nv.HeadPos, 1.8f);
                            if (S.HasRel(a.Id, newcomer))
                            {
                                var r = S.R(a.Id, newcomer);
                                if (free && r.Fear > 0.45f) an.PlayGesture(Gesture.Flinch, 0.9f);
                                else if (free && r.Grudge > 0.4f) an.PlayGesture(Gesture.CrossArms, 3f);
                                else if (free && !seated && r.Like > 0.35f && a.Def.P.Sociability > 0.35f) an.PlayGesture(Gesture.Wave, 1.3f);
                                else if (free && r.Like > 0.2f) an.PlayGesture(Gesture.Nod, 0.8f);
                            }
                        }
                    }
                }
            }

            // 2) listening: nod along, sometimes a small "hm" of thought
            if (a.TalkingTo != null)
            {
                var pv = W.ViewOf(a.TalkingTo);
                if (pv != null && pv.Talking && !v.Talking && t >= _nodNext && an.CurrentGesture == Gesture.None)
                {
                    _nodNext = t + 2.2f + H(a.Id, (int)(t * 3)) * 3.5f;
                    an.PlayGesture(H(a.Id, (int)t) < 0.7f ? Gesture.Nod : Gesture.Think, H(a.Id, 5) < 0.5f ? 0.8f : 1.4f);
                }
                return null;
            }

            // 3) walking: glance at people passing close by; a nod for friends
            if (moving)
            {
                if (t >= _glanceNext)
                {
                    _glanceNext = t + 1.1f + H(a.Id, (int)(t * 7)) * 1.2f;
                    var fwd = v.transform.forward; ActorView best = null; float bestD = 3.8f; string bestId = null;
                    foreach (var o in S.Actors.Values)
                    {
                        if (o == a || !o.Alive || o.Room != a.Room) continue; var ov = W.ViewOf(o.Id); if (ov == null) continue;
                        var d = ov.HeadPos - v.HeadPos; float dist = d.magnitude; if (dist > bestD || Vector3.Dot(fwd, d / Mathf.Max(0.01f, dist)) < 0.25f) continue;
                        best = ov; bestD = dist; bestId = o.Id;
                    }
                    if (best != null)
                    {
                        Look(best.HeadPos, 1.1f);
                        if (S.HasRel(a.Id, bestId) && S.R(a.Id, bestId).Like > 0.3f && (!_greeted.TryGetValue(bestId, out var gt) || t - gt > 25f)) { _greeted[bestId] = t; an.PlayGesture(Gesture.Nod, 0.7f); }
                    }
                }
                return t < _lookUntil ? _look : (Vector3?)null;
            }

            // 4) idle: fidget by mood, then by temperament
            if (free && t >= _next)
            {
                _next = t + (seated ? 9f : 5f) + H(a.Id, (int)t) * (seated ? 12f : 8f);
                var n = a.Needs; var p = a.Def.P; float r = H(a.Id, (int)(t * 13));
                Gesture g = Gesture.None; float dur = 2.2f;
                if (n.Grief > 0.7f) { g = r < 0.55f ? Gesture.Cry : Gesture.HandOnChest; dur = 3.2f; }
                else if (n.Fear > 0.6f) { g = r < 0.6f ? Gesture.LookAround : Gesture.HandOnChest; LookBehind(v); }
                else if (n.Anger > 0.5f) { g = r < 0.7f ? Gesture.CrossArms : Gesture.Angry; dur = 3.5f; }
                else if (n.Stress > 0.6f) { g = r < 0.5f ? Gesture.HandOnChest : Gesture.LookAround; }
                else if (n.Energy < 0.25f) { g = Gesture.Think; dur = 2.8f; }
                else
                {
                    // temperament: a weighted pick, plus a wave or a look for someone they like nearby
                    var friend = seated ? null : FriendNear(a, v, S, W, 7f);
                    if (friend != null && r < 0.18f + p.Sociability * 0.2f) { Look(friend.HeadPos, 2f); g = Gesture.Wave; dur = 1.3f; }
                    else
                    {
                        float wLook = 0.6f + p.Curiosity, wCross = 0.2f + p.Pride * 0.9f + p.Aggression * 0.3f, wThink = 0.3f + p.Curiosity * 0.5f + p.Ambition * 0.3f, wChest = 0.1f + p.Fearfulness * 0.8f, wShrug = 0.15f + (1 - p.Honesty) * 0.2f;
                        float sum = wLook + wCross + wThink + wChest + wShrug, x = r * sum;
                        g = (x -= wLook) < 0 ? Gesture.LookAround : (x -= wCross) < 0 ? Gesture.CrossArms : (x -= wThink) < 0 ? Gesture.Think : (x -= wChest) < 0 ? Gesture.HandOnChest : Gesture.Shrug;
                        dur = g == Gesture.CrossArms ? 4f : g == Gesture.Think ? 3f : 2f;
                        if (g == Gesture.LookAround) LookAway(v, H(a.Id, (int)t + 3));
                    }
                }
                // --- time-on-demand (begin): a crowd never fidgets in unison (one new gesture per frame, not the same one twice in a room within 0.5 s)
                if (g != Gesture.None) { if (FrozenLife.Allow(a.Room, g, a.Id)) an.PlayGesture(g, dur); else _next = t + 0.15f + H(a.Id, (int)(t * 7)) * 0.5f; }
                // --- time-on-demand (end)
            }
            return t < _lookUntil ? _look : (Vector3?)null;
        }

        void Look(Vector3 p, float secs) { _look = p; _lookUntil = Time.time + secs; }
        void LookBehind(ActorView v) => Look(v.HeadPos - v.transform.forward * 3f + v.transform.right * 1.5f, 1.2f);
        void LookAway(ActorView v, float r)
        {
            var dir = Quaternion.Euler(0, Mathf.Lerp(-70f, 70f, r), 0) * v.transform.forward;
            Look(v.HeadPos + dir * 4f + Vector3.up * Mathf.Lerp(-0.4f, 0.8f, r), 1.6f);
        }

        static ActorView FriendNear(Actor a, ActorView v, GameState S, WorldPresenter W, float within)
        {
            ActorView best = null; float bestLike = 0.35f;
            foreach (var o in S.Actors.Values)
            {
                if (o == a || !o.Alive || o.Room != a.Room || !S.HasRel(a.Id, o.Id)) continue;
                float like = S.R(a.Id, o.Id).Like; if (like <= bestLike) continue;
                var ov = W.ViewOf(o.Id); if (ov == null || Vector3.Distance(ov.HeadPos, v.HeadPos) > within) continue;
                best = ov; bestLike = like;
            }
            return best;
        }
    }
}
