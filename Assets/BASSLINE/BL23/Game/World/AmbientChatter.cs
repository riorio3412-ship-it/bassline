using System.Collections.Generic;
using System.Text;
using BL23.Game.Audio;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;
using Pose = BL23.Sim.Pose;

namespace BL23.Game
{
    /// <summary>
    /// People talk to each other while the clock stands still: two who were already talking (the kernel's conversations),
    /// idle neighbours within arm's reach who get on, people seated at the same table. A pair takes turns for 20–40 s
    /// (the speaker's lips move and the hands follow what is said; the listener nods), then falls quiet for 15–30 s.
    /// Only the nearest speaker in 민혁's room, within 7 m, is heard — mostly as murmured voice; now and then, close by, a
    /// whole line of small talk is caught over the speaker's head (their own voice; never a line that carries a fact, each
    /// line once a session). Presentation only; nothing reaches the kernel and no kernel dice are drawn.
    /// </summary>
    public static class AmbientChatter
    {
        sealed class Pair
        {
            public string A, B, Speaker; public bool Kernel; public int Room;
            public float TalkUntil, QuietUntil, TurnUntil, NodAt;
            public bool Quiet => Time.time < QuietUntil;
        }
        static readonly List<Pair> _pairs = new List<Pair>();
        static readonly Dictionary<string, Pair> _of = new Dictionary<string, Pair>();
        static float _scanAt;
        /// <summary>Murmured turns heard by the player (probe: the house sounds alive).</summary>
        public static int Heard { get; private set; }
        public static int Pairs => _pairs.Count;

        /// <summary>The person this one is chatting with right now (null when quiet or alone).</summary>
        public static string PartnerOf(string id) => _of.TryGetValue(id, out var p) && !p.Quiet ? (p.A == id ? p.B : p.A) : null;

        /// <summary>The world is moving again: pairs are forgotten (the kernel's own talk takes over).</summary>
        public static void Idle() { if (_pairs.Count == 0) return; _pairs.Clear(); _of.Clear(); }

        public static void Tick(GameState S, WorldPresenter W)
        {
            if (S?.Player == null || W == null) return;
            float t = Time.time;
            if (t >= _scanAt) { _scanAt = t + 0.5f; Scan(S, W); }
            var ses = Session.I;
            bool mayVoice = ses != null && !VoiceBabble.IsSpeaking && !(ses.Dialogue?.Active ?? false) && !(ses.Cine?.Busy ?? false) && !TimeLink.Lapsing;
            Pair loudest = null; float bestD = 7f;
            foreach (var p in _pairs)
            {
                if (p.Quiet) continue;
                if (t >= p.TalkUntil) { p.QuietUntil = t + Random.Range(15f, 30f); p.TalkUntil = p.QuietUntil + Random.Range(20f, 40f); p.Speaker = null; continue; }
                if (t < p.TurnUntil)
                {
                    // the listener nods along now and then
                    if (p.Speaker != null && t >= p.NodAt)
                    {
                        p.NodAt = t + Random.Range(2.2f, 4.5f);
                        var lid = p.Speaker == p.A ? p.B : p.A; var lv = W.ViewOf(lid); var lan = lv?.Rig?.Anim;
                        if (lan != null && lan.CurrentGesture == Gesture.None && !p.Kernel) { var g = Random.value < 0.75f ? Gesture.Nod : Gesture.Think; if (FrozenLife.Allow(p.Room, g, lid)) lan.PlayGesture(g, g == Gesture.Nod ? 0.8f : 1.6f); }
                    }
                    continue;
                }
                // next turn: the other one speaks (a short pause between turns)
                if (p.Speaker != null && t < p.TurnUntil + 0.5f) continue;
                string sp = p.Speaker == null ? (Random.value < 0.5f ? p.A : p.B) : (p.Speaker == p.A ? p.B : p.A);
                var v = W.ViewOf(sp); var an = v?.Rig?.Anim; if (an == null) continue;
                var gest = PickGesture(); if (!FrozenLife.Allow(p.Room, gest, sp)) continue;   // tried again next frame
                float dur = Random.Range(2.2f, 5f);
                p.Speaker = sp; p.TurnUntil = t + dur; p.NodAt = t + Random.Range(0.8f, 1.6f);
                v.Talk(dur); an.PlayGesture(gest, Mathf.Min(dur, gest == Gesture.Nod || gest == Gesture.Shrug ? 1.4f : dur));
                var a = S.A(sp);
                if (mayVoice && a != null && a.Room == S.Player.Room && a.Pos.f == S.Player.Pos.f) { float d = a.Pos.DistXZ(S.Player.Pos); if (d < bestD) { bestD = d; loudest = p; } }
            }
            // the voice is not placed in space: the farther the pair, the fewer of their turns are heard at all
            if (loudest != null && mayVoice && Random.value < Mathf.Lerp(0.85f, 0.25f, bestD / 7f))
            {
                // now and then, close by, a whole line is caught (small talk only: never a fact), shown over the speaker's head
                string partner = loudest.Speaker == loudest.A ? loudest.B : loudest.A;
                string line = bestD < 5.5f && t >= _lineAt && Random.value < 0.4f ? OverheardLine(S, loudest.Speaker, partner, loudest) : null;
                if (line != null)
                {
                    _lineAt = t + Random.Range(16f, 28f);
                    Hud.I?.Overheard(loudest.Speaker, line, bestD);
                    VoiceBabble.Speak(loudest.Speaker, line); Heard++; Lines++;
                }
                else
                {
                    var text = Murmur(loudest.TurnUntil - t);
                    VoiceBabble.Speak(loudest.Speaker, text); Heard++;
                }
            }
        }

        // ------------------------------------------------------------------ overheard small talk (presentation only)
        static float _lineAt;
        static Rng _rng;
        static readonly HashSet<string> _shown = new HashSet<string>();
        static GameState _shownFor;
        /// <summary>Whole lines caught from the still world's chatter (probe).</summary>
        public static int Lines { get; private set; }

        /// <summary>A line of small talk this person might say to the other (their own voice: small talk, the house, what they like, a
        /// morning greeting, the meal). Never a key that carries a fact; each line at most once a session, so nothing repeats — when
        /// someone has said all of theirs, they only murmur. Picked with the presentation's own dice (never the kernel's).</summary>
        static string OverheardLine(GameState S, string speaker, string listener, Pair p)
        {
            var ses = Session.I; var a = S.A(speaker); var b = S.A(listener); if (ses?.Sim == null || a == null || b == null || a.IsButler || a.Body.Speech < 0.2f) return null;
            if (_shownFor != S) { _shownFor = S; _shown.Clear(); }
            if (_rng == null) _rng = new Rng((ulong)(uint)(S.Layout?.Hash ?? "bl23").GetHashCode() ^ 0x7a1cUL, 29UL);
            var room = S.Layout.Room(a.Room);
            var keys = new List<string> { "small_talk", "small_talk", "small_talk", "talk_mansion" };
            if (a.Def?.Likes != null && a.Def.Likes.Length > 0) keys.Add("talk_like");
            if (room != null && room.Type == RoomType.Dining && (a.Anim == Anim.Eat || b.Anim == Anim.Eat)) { keys.Add("meal"); keys.Add("meal"); }
            bool casual = S.HasRel(speaker, listener) ? S.R(speaker, listener).Casual : !(Cast.Get(speaker)?.Speech.PoliteDefault ?? true);
            for (int tries = 0; tries < 6; tries++)
            {
                string key = keys[_rng.R(keys.Count)];
                var raw = LineBank.Raw(speaker, key, casual, _rng); if (string.IsNullOrEmpty(raw)) continue;
                var slots = new Dictionary<string, string> { { "you", ses.Sim.CallName(speaker, listener) } };
                if (key == "talk_like") slots["topic"] = a.Def.Likes[_rng.R(a.Def.Likes.Length)];
                // only the slots filled here (a line that needs a time, a place or another person is not small talk)
                bool other = false;
                foreach (System.Text.RegularExpressions.Match mm in System.Text.RegularExpressions.Regex.Matches(raw, @"\{([a-z!]+)")) if (!slots.ContainsKey(mm.Groups[1].Value)) { other = true; break; }
                if (other) continue;
                var text = LineBank.FixParticles(LineBank.Render(raw, slots, casual));
                if (string.IsNullOrEmpty(text) || !_shown.Add(speaker + "|" + raw)) continue;
                return text;
            }
            return null;
        }

        static Gesture PickGesture()
        {
            float r = Random.value;
            return r < 0.38f ? Gesture.Talk : r < 0.58f ? Gesture.TalkEmphatic : r < 0.68f ? Gesture.Shrug : r < 0.78f ? Gesture.HandOnChest : r < 0.88f ? Gesture.Think : r < 0.95f ? Gesture.Laugh : Gesture.Nod;
        }

        static readonly string[] Bits = { "그래서", "음", "있잖아", "그러니까", "정말", "아니", "근데", "그렇지", "저기", "맞아", "글쎄", "어제", "그때", "조금", "이상하게", "나는", "너도", "그런가" };
        /// <summary>Sound only: syllables for the voice to murmur for about this long (never shown, never a fact).</summary>
        static string Murmur(float secs)
        {
            var sb = new StringBuilder(); int n = Mathf.Clamp(Mathf.RoundToInt(secs * 2.2f), 2, 12);
            for (int i = 0; i < n; i++) { if (i > 0) sb.Append(' '); sb.Append(Bits[Random.Range(0, Bits.Length)]); }
            sb.Append(Random.value < 0.3f ? "?" : Random.value < 0.5f ? "…" : ".");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ who talks with whom
        static bool Able(Actor a, Actor me)
        {
            if (a == null || a.IsPlayer || a.IsButler || !a.Alive || a.Status != ActorStatus.Active || a.CarriedBy != null || a.StairId >= 0) return false;
            if (a.Pose == Pose.Sleep || a.Pose == Pose.LieBack || a.Pose == Pose.LieFront || a.Pose == Pose.LieSide || a.Pose == Pose.Slumped) return false;
            if (a.TalkingTo == Cast.Player || S_Approach(a)) return false;
            if (a.Pos.f != me.Pos.f || a.Pos.DistXZ(me.Pos) > 25f) return false;
            return true;
        }
        static bool S_Approach(Actor a) => Session.I != null && Session.I.S.Flags.ContainsKey("approach:" + a.Id);

        static bool Idleish(Actor a)
        {
            var st = a.Act?.Cur; if (st != null && st.Kind == "GoTo" && st.HasTarget && a.Pos.DistXZ(st.Target) > 1.2f) return false;   // on the way somewhere
            switch (a.Anim) { case Anim.Idle: case Anim.None: case Anim.Eat: case Anim.Drink: case Anim.Talk: case Anim.Listen: case Anim.Think: return true; }
            return a.Pose == Pose.Sit && (a.Anim == Anim.Read || a.Anim == Anim.Write) ? false : a.Act == null;
        }

        static bool GetOn(GameState S, string x, string y)
        {
            if (!S.HasRel(x, y) || !S.HasRel(y, x)) return true;   // strangers make small talk too
            var r1 = S.R(x, y); var r2 = S.R(y, x);
            return r1.Like > -0.1f && r2.Like > -0.1f && r1.Grudge < 0.3f && r2.Grudge < 0.3f && r1.Fear < 0.5f && r2.Fear < 0.5f;
        }

        static void Scan(GameState S, WorldPresenter W)
        {
            var me = S.Player; float t = Time.time;
            // keep the pairs that still stand together
            for (int i = _pairs.Count - 1; i >= 0; i--)
            {
                var p = _pairs[i]; var a = S.A(p.A); var b = S.A(p.B);
                bool ok = Able(a, me) && Able(b, me) && a.Room == b.Room && a.Pos.DistXZ(b.Pos) <= (p.Kernel ? 4f : 2.6f) && (p.Kernel ? a.TalkingTo == b.Id : (a.TalkingTo == null && b.TalkingTo == null));
                if (!ok) { _of.Remove(p.A); _of.Remove(p.B); _pairs.RemoveAt(i); }
            }
            var cands = new List<Actor>();
            foreach (var a in S.Actors.Values) if (Able(a, me) && !_of.ContainsKey(a.Id)) cands.Add(a);
            cands.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));
            // the kernel's conversations first, then neighbours who get on
            foreach (var a in cands)
            {
                if (_of.ContainsKey(a.Id) || a.TalkingTo == null) continue;
                var b = S.A(a.TalkingTo); if (b == null || b.IsPlayer || _of.ContainsKey(b.Id) || !Able(b, me)) continue;
                Add(a, b, true, t);
            }
            foreach (var a in cands)
            {
                if (_of.ContainsKey(a.Id) || a.TalkingTo != null || !Idleish(a)) continue;
                Actor best = null; float bd = 2.4f;
                foreach (var b in cands)
                {
                    if (b == a || _of.ContainsKey(b.Id) || b.TalkingTo != null || b.Room != a.Room || !Idleish(b)) continue;
                    float d = a.Pos.DistXZ(b.Pos); if (d < bd && GetOn(S, a.Id, b.Id)) { bd = d; best = b; }
                }
                if (best != null) Add(a, best, false, t);
            }
        }

        static void Add(Actor a, Actor b, bool kernel, float t)
        {
            // not everyone starts at once: some pairs begin quiet
            float h = ((a.Id.GetHashCode() ^ b.Id.GetHashCode()) & 0xff) / 255f;
            var p = new Pair { A = a.Id, B = b.Id, Kernel = kernel, Room = a.Room };
            if (h < 0.35f) { p.QuietUntil = t + Random.Range(3f, 12f); p.TalkUntil = p.QuietUntil + Random.Range(20f, 40f); }
            else { p.QuietUntil = 0f; p.TalkUntil = t + Random.Range(20f, 40f); p.TurnUntil = t + Random.Range(0.2f, 2.5f); }
            _pairs.Add(p); _of[a.Id] = p; _of[b.Id] = p;
        }
    }
}
