using System.Collections.Generic;
using System.Diagnostics;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;
using Pose = BL23.Sim.Pose;

namespace BL23.Game
{
    /// <summary>
    /// The house while time stands still (a stopped clock in daily life, a conversation, a pause): nobody moves through the
    /// kernel, but nobody looks frozen either. Presentation only — no kernel writes, no kernel RNG. Per person:
    /// someone reading or eating keeps at it and now and then looks up and thinks; someone searching, examining, laughing or
    /// washing does it again every few seconds, a little differently; someone stopped on the way somewhere looks toward
    /// where they were going, then around; someone who came to talk to 민혁 faces him and waves or nods; Yusti stands with
    /// his hands together, looks slowly about, bows when 민혁 comes close and nods when looked at. Idle neighbours talk to
    /// each other (AmbientChatter). No two people in a room start the same gesture within half a second, and at most one
    /// new gesture starts per frame, so a crowd never moves in unison.
    /// </summary>
    public sealed class FrozenLife
    {
        // ------------------------------------------------------------------ per frame (shared)
        static int _frame = -1; static bool _on; static int _gestFrame = -1;
        static readonly Dictionary<int, float> _recent = new Dictionary<int, float>();
        static readonly Stopwatch _sw = new Stopwatch(); static double _frameMs; static int _frames;
        static bool _hooked;
        /// <summary>Average cost (ms per frame) of the still-world life (this and the chatter), for the probe log.</summary>
        public static double AvgMs { get; private set; }
        /// <summary>Gestures started by the still-world life (probe: at least two within 20 s in a room with idle people).</summary>
        public static int Started { get; private set; }
        /// <summary>The same gesture started by two people in one room within 0.5 s (must stay 0).</summary>
        public static int Clashes { get; private set; }
        public static readonly List<(float t, int room, string actor, string what)> Recent = new List<(float, int, string, string)>();

        /// <summary>Is the still-world life on this frame? (the first caller of the frame also drives the chatter)</summary>
        public static bool Frame(GameState S, WorldPresenter W)
        {
            if (_frame == Time.frameCount) return _on;
            _frame = Time.frameCount;
            if (_on) { AvgMs = _frames == 0 ? _frameMs : AvgMs * 0.97 + _frameMs * 0.03; _frames++; }   // the cost of a still frame
            _frameMs = 0;
            TimeLink.Poll();
            if (!_hooked) { _hooked = true; TimeLink.SkipEnded += OnSkipEnded; }
            _on = Compute(S);
            if (_on) { _sw.Restart(); try { AmbientChatter.Tick(S, W); } catch (System.Exception e) { UnityEngine.Debug.LogException(e); } _sw.Stop(); _frameMs += _sw.Elapsed.TotalMilliseconds; }
            else AmbientChatter.Idle();
            return _on;
        }

        static bool Compute(GameState S)
        {
            var ses = Session.I; if (ses == null || ses.Sim == null || S == null || S.Player == null) return false;
            var ph = S.Phase; if (ph != Phase.Daily && ph != Phase.Investigation && ph != Phase.Assembly) return false;
            if ((ses.Cine != null && ses.Cine.Busy) || (ses.Trial != null && ses.Trial.Active) || (ses.Reveal != null && ses.Reveal.Active)) return false;
            if (TimeLink.Lapsing) return false;
            return TimeLink.WorldStill;
        }

        /// <summary>After time was passed: every view forgets the activity changes that happened meanwhile (no burst of barks).</summary>
        static void OnSkipEnded()
        {
            var w = Session.I?.World; if (w == null) return;
            foreach (var v in w.Actors.Values) if (v != null) v.ResyncIntent();
        }

        /// <summary>May someone in this room start this gesture now? (one new gesture per frame; not the same one as
        /// someone else in the room within 0.5 s). Recorded when allowed.</summary>
        public static bool Allow(int room, Gesture g, string actor = null) => AllowKey(room, (int)g, actor, g.ToString());
        public static bool Allow(int room, ActionAnim a, string actor = null) => AllowKey(room, 100 + (int)a, actor, a.ToString());
        static bool AllowKey(int room, int kind, string actor, string what)
        {
            if (_gestFrame == Time.frameCount) return false;
            float t = Time.time; int key = room * 256 + kind;
            if (_recent.TryGetValue(key, out var at) && t - at < 0.5f) return false;
            _gestFrame = Time.frameCount; _recent[key] = t; Started++;
            Recent.Add((t, room, actor, what)); if (Recent.Count > 64) Recent.RemoveAt(0);
            return true;
        }
        /// <summary>A gesture that must happen now (a reaction): recorded, and counted as a clash if it repeats one.</summary>
        public static void Note(int room, Gesture g, string actor)
        {
            float t = Time.time; int key = room * 256 + (int)g;
            if (_recent.TryGetValue(key, out var at) && t - at < 0.5f) Clashes++;
            _recent[key] = t;
        }

        static float H(string id, int salt) { unchecked { int h = 17 + salt * 31; foreach (var c in id) h = h * 31 + c; return (h & 0xffff) / 65535f; } }

        // ------------------------------------------------------------------ per person
        float _next = -1f, _lookUntil, _bowAt = -99f, _nodAt = -99f; Vector3 _look; bool _wasOn; Anim _anim = (Anim)(-1); int _phase;

        /// <summary>One person's life in place; returns where the head should look (null: leave it to the rest).</summary>
        public Vector3? Tick(ActorView v, Actor a, ActorAnimator an, GameState S, WorldPresenter W, float dt)
        {
            _sw.Restart();
            try { return Life(v, a, an, S, W); }
            finally { _sw.Stop(); _frameMs += _sw.Elapsed.TotalMilliseconds; }
        }

        Vector3? Life(ActorView v, Actor a, ActorAnimator an, GameState S, WorldPresenter W)
        {
            var me = S.Player; float t = Time.time;
            if (!_on || me == null || a.IsPlayer || !a.Alive || a.Status != ActorStatus.Active || a.CarriedBy != null) { _wasOn = false; return null; }
            if (a.Pos.f != me.Pos.f || a.Pos.DistXZ(me.Pos) > 25f || a.StairId >= 0) { _wasOn = false; return null; }
            if (a.Pose == Pose.Sleep || a.Pose == Pose.LieBack || a.Pose == Pose.LieFront || a.Pose == Pose.LieSide || a.Pose == Pose.Slumped) return null;
            if (a.TalkingTo == Cast.Player) return null;   // the conversation directs them
            if (_next < 0f) _next = t + 1.2f + H(a.Id, 11) * 6f;
            if (!_wasOn)
            {
                // the moment the house stops: someone on the way looks to where they were going
                _wasOn = true; _phase = 0;
                if (Walking(a, out var dest)) { _look = W.ToWorld(dest) + Vector3.up * 1.45f; _lookUntil = t + 3f + H(a.Id, 3) * 2.5f; }
            }
            if (a.Anim != _anim) { _anim = a.Anim; _next = Mathf.Min(_next, t + 1.5f + Random.value * 2.5f); }
            bool gestFree = an.CurrentGesture == Gesture.None;
            var pv = W.ViewOf(Cast.Player);

            // Yusti
            if (a.IsButler) return Butler(v, a, an, S, pv, t, gestFree);

            // came to talk to 민혁: faces him, waves or nods now and then
            if (S.Flags.ContainsKey("approach:" + a.Id) && pv != null)
            {
                if (t >= _next && gestFree)
                {
                    var g = Random.value < 0.4f ? Gesture.Wave : Gesture.Nod;
                    if (Allow(a.Room, g, a.Id)) { an.PlayGesture(g, g == Gesture.Wave ? 1.3f : 0.8f); _next = t + Random.Range(6f, 11f); } else _next = t + 0.2f;
                }
                return pv.HeadPos;
            }

            // talking with a neighbour (the chatter takes turns; the listener's head stays with the speaker)
            var partner = AmbientChatter.PartnerOf(a.Id);
            if (partner != null && a.TalkingTo == null) { var ov = W.ViewOf(partner); if (ov != null) return ov.HeadPos; }
            if (a.TalkingTo != null) return null;

            // stopped on the way somewhere: a look ahead, then around; a thought
            if (Walking(a, out var d2))
            {
                if (t >= _next && gestFree)
                {
                    var g = (_phase++ % 2 == 0) ? Gesture.LookAround : Gesture.Think;
                    if (Allow(a.Room, g, a.Id))
                    {
                        an.PlayGesture(g, g == Gesture.Think ? 2.6f : 2.2f);
                        if (g == Gesture.LookAround) LookAside(v, Random.value); else { _look = W.ToWorld(d2) + Vector3.up * 1.45f; _lookUntil = t + 2.5f; }
                        _next = t + Random.Range(8f, 14f);
                    }
                    else _next = t + 0.2f;
                }
                return t < _lookUntil ? _look : (Vector3?)null;
            }

            // "doing" something that needs its place without being at it (playing beside the piano, not at the keys): about to go
            // to it — a look toward it, a thought, a shift of weight; never frozen mid-performance with the hands in the air
            if (Unplaced(S, a, out var uf))
            {
                var dest = uf != null ? W.ToWorld(uf.Pos) + Vector3.up * Mathf.Clamp(uf.H, 0.6f, 1.3f) : (Vector3?)null;
                if (t >= _next && gestFree)
                {
                    var g = UnplacedGestures[_phase % UnplacedGestures.Length];
                    if (Allow(a.Room, g, a.Id))
                    {
                        _phase++;
                        an.PlayGesture(g, g == Gesture.Think ? 2.6f : g == Gesture.CrossArms ? 3.2f : 2.2f);
                        if (g == Gesture.LookAround) LookAside(v, Random.value);
                        else if (dest != null) { _look = dest.Value; _lookUntil = t + 2.8f; }
                        _next = t + Random.Range(6f, 11f);
                    }
                    else _next = t + 0.2f;
                }
                if (t < _lookUntil) return _look;
                return dest;   // otherwise the eyes rest on where they meant to go
            }

            // doing something
            switch (Kind(a.Anim))
            {
                case 1:   // a steady activity (reading, eating, playing): keep at it, look up now and then
                    if (t >= _next && gestFree)
                    {
                        var g = Random.value < 0.55f ? Gesture.Think : Gesture.LookAround;
                        if (Allow(a.Room, g, a.Id)) { an.PlayGesture(g, g == Gesture.Think ? 2.2f : 1.8f); if (g == Gesture.LookAround) LookAside(v, Random.value); _next = t + Random.Range(10f, 20f); }
                        else _next = t + 0.2f;
                    }
                    break;
                case 2:   // a one-off movement: again, a little differently each time
                    if (t >= _next && an.CurrentAction == ActionAnim.None && gestFree)
                    {
                        if (Again(a, an)) _next = t + Random.Range(3f, 8f); else _next = t + 0.2f;
                    }
                    break;
            }
            return t < _lookUntil ? _look : (Vector3?)null;
        }

        static readonly Gesture[] UnplacedGestures = { Gesture.Think, Gesture.LookAround, Gesture.CrossArms, Gesture.LookAround };

        /// <summary>Anims that need their piece of furniture (an instrument or a game, a stove, a bench, a desk).</summary>
        static bool NeedsPlace(Anim an) => an == Anim.Play || an == Anim.Cook || an == Anim.Craft || an == Anim.Operate || an == Anim.Write;
        static bool SeatedInstrument(string type) => type == "Piano" || type == "Piano_Upright" || type == "Organ" || type == "Drums";

        /// <summary>Someone "doing" a thing that needs its place (playing, cooking, making, writing) without being at it: no place was
        /// free (they do it wherever they stood), or a keyboard played standing beside it. In a still world they read as about to
        /// go to it. f: the piece they would go to (null when the room has none).</summary>
        public static bool Unplaced(GameState S, Actor a, out Furniture f)
        {
            f = null;
            if (S == null || a == null || a.IsPlayer || a.Status != ActorStatus.Active || a.Act == null || !NeedsPlace(a.Anim)) return false;
            var cur = a.Act.Cur; if (cur == null || cur.Kind != "Activity") return false;
            int spId = a.Spot >= 0 ? a.Spot : cur.Spot;
            var sp = spId >= 0 && spId < S.Layout.Spots.Count ? S.Layout.Spots[spId] : null;
            if (sp != null && sp.Furniture >= 0 && sp.Furniture < S.Layout.Furniture.Count) f = S.Layout.Furniture[sp.Furniture];
            if (sp != null && a.Spot == sp.Id)
                return f != null && SeatedInstrument(f.Type) && a.Pose != Pose.Sit && a.Pos.DistXZ(sp.Pos) > 0.35f;
            if (f == null)
            {
                // no place of their own: the nearest piece in the room this is done at
                string aid = a.Act.Id != null && a.Act.Id.StartsWith("life:") ? a.Act.Id.Substring(5) : cur.Tag;
                var def = Activities.Get(aid); var room = S.Layout.Room(a.Room);
                if (def != null && def.Spots != null && def.Spots.Length > 0 && room != null)
                {
                    float best = float.MaxValue;
                    foreach (var sid in room.Spots)
                    {
                        var s2 = S.Layout.Spots[sid]; if (s2.Furniture < 0 || s2.Furniture >= S.Layout.Furniture.Count || System.Array.IndexOf(def.Spots, s2.Tag) < 0) continue;
                        float d = s2.Pos.DistXZ(a.Pos); if (d < best) { best = d; f = S.Layout.Furniture[s2.Furniture]; }
                    }
                }
            }
            return true;
        }

        /// <summary>The label under the name for such a person in a still world: "그랜드 피아노 쪽으로 가려던 참" (null when not unplaced).</summary>
        public static string UnplacedLabel(GameState S, Actor a)
        {
            if (!Unplaced(S, a, out var f)) return null;
            if (f != null) return (FurnitureCatalog.Get(f.Type)?.Kor ?? "그쪽") + " 쪽으로 가려던 참";
            string aid = a.Act?.Id != null && a.Act.Id.StartsWith("life:") ? a.Act.Id.Substring(5) : a.Act?.Cur?.Tag;
            var kor = Activities.Get(aid)?.Kor;
            return kor != null ? LineBank.FixParticles(kor + "을(를) 하려던 참") : "뭔가 하려던 참";
        }

        static bool Walking(Actor a, out P3 dest)
        {
            dest = default; var st = a.Act?.Cur;
            if (st == null || st.Kind != "GoTo" || !st.HasTarget || st.Target.f != a.Pos.f) return false;
            dest = st.Target; return a.Pos.DistXZ(st.Target) > 1.2f;
        }

        /// <summary>0 idle (IdleLife handles it), 1 a steady looping activity, 2 a one-off movement to repeat.</summary>
        static int Kind(Anim a)
        {
            switch (a)
            {
                case Anim.Read: case Anim.Eat: case Anim.Drink: case Anim.Play: case Anim.Pray: case Anim.Cook: case Anim.Garden: case Anim.Craft: case Anim.Use: case Anim.Operate: case Anim.Write: case Anim.Clean: case Anim.FirstAid: return 1;
                case Anim.Search: case Anim.Examine: case Anim.Photo: case Anim.Laugh: case Anim.Think: case Anim.Exercise: case Anim.Wash: case Anim.Swim: case Anim.Cry: case Anim.Point: case Anim.Shrug: case Anim.Present: return 2;
            }
            return 0;
        }

        bool Again(Actor a, ActorAnimator an)
        {
            bool alt = Random.value < 0.35f;
            switch (a.Anim)
            {
                case Anim.Search: if (alt) return G(a, an, Gesture.LookAround, 2.4f); return Act(a, an, ActionAnim.Search, 2.2f);
                case Anim.Examine: if (alt) return G(a, an, Gesture.Think, 2.2f); return Act(a, an, ActionAnim.Examine, 2.4f);
                case Anim.Photo: return G(a, an, alt ? Gesture.Point : Gesture.Present, 1.8f);
                case Anim.Laugh: return G(a, an, alt ? Gesture.Nod : Gesture.Laugh, alt ? 0.9f : 1.8f);
                case Anim.Think: return G(a, an, alt ? Gesture.LookAround : Gesture.Think, alt ? 2f : 3.2f);
                case Anim.Exercise: return Act(a, an, ActionAnim.Struggle, 1.8f);
                case Anim.Wash: return Act(a, an, ActionAnim.Wash, 2.6f);
                case Anim.Swim: return G(a, an, Gesture.LookAround, 2.2f);
                case Anim.Cry: return G(a, an, alt ? Gesture.HandOnChest : Gesture.Cry, 3f);
                case Anim.Point: return G(a, an, alt ? Gesture.Think : Gesture.Point, 1.4f);
                case Anim.Shrug: return G(a, an, alt ? Gesture.Think : Gesture.Shrug, 1.5f);
                case Anim.Present: return G(a, an, alt ? Gesture.Nod : Gesture.Present, 1.8f);
            }
            return true;
        }
        static bool G(Actor a, ActorAnimator an, Gesture g, float secs) { if (!Allow(a.Room, g, a.Id)) return false; an.PlayGesture(g, secs * Random.Range(0.85f, 1.2f)); return true; }
        static bool Act(Actor a, ActorAnimator an, ActionAnim x, float secs) { if (!Allow(a.Room, x, a.Id)) return false; an.PlayAction(x, secs * Random.Range(0.85f, 1.2f)); return true; }

        Vector3? Butler(ActorView v, Actor a, ActorAnimator an, GameState S, ActorView pv, float t, bool gestFree)
        {
            var me = S.Player; float dist = me.Pos.DistXZ(a.Pos);
            var ia = Session.I?.Player?.Interact;
            // a bow when 민혁 comes close (once in a while), a nod when he is looked at
            if (gestFree && dist < 2.5f && t - _bowAt > 90f && Allow(a.Room, Gesture.Bow, a.Id)) { _bowAt = t; _nodAt = t; an.PlayGesture(Gesture.Bow, 1.8f); _next = t + 4f; return pv?.HeadPos; }
            if (gestFree && ia != null && ia.LookedActor == a.Id && t - _nodAt > 20f && Allow(a.Room, Gesture.Nod, a.Id)) { _nodAt = t; an.PlayGesture(Gesture.Nod, 0.9f); _next = t + 3f; return pv?.HeadPos; }
            // hands together, a slow look about the room
            if (t >= _next && gestFree)
            {
                if (Allow(a.Room, Gesture.Listen, a.Id)) { an.PlayGesture(Gesture.Listen, Random.Range(6f, 9f)); _next = t + Random.Range(7f, 11f); }
                else _next = t + 0.3f;
            }
            if (dist < 6f && pv != null && (ia != null && ia.LookedActor == a.Id)) return pv.HeadPos;
            float sweep = Mathf.Sin(t * 0.18f + H(a.Id, 7) * 6f) * 55f;
            return v.HeadPos + Quaternion.Euler(0, sweep, 0) * v.transform.forward * 4f + Vector3.up * (Mathf.Sin(t * 0.11f) * 0.3f);
        }

        void LookAside(ActorView v, float r)
        {
            var dir = Quaternion.Euler(0, Mathf.Lerp(-75f, 75f, r), 0) * v.transform.forward;
            _look = v.HeadPos + dir * 4f + Vector3.up * Mathf.Lerp(-0.3f, 0.7f, Random.value); _lookUntil = Time.time + Random.Range(1.4f, 2.6f);
        }
    }
}
