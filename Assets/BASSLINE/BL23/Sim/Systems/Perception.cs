using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>Room graph for sound: neighbours through doors, open passages, shared walls and stairs.</summary>
    public sealed class Acoustics
    {
        public readonly Dictionary<int, List<(int room, int door, bool open, bool wall, bool stair)>> N = new Dictionary<int, List<(int, int, bool, bool, bool)>>();
        public Acoustics(Layout L)
        {
            void Add(int a, int b, int door, bool open, bool wall, bool stair)
            {
                if (!N.TryGetValue(a, out var la)) N[a] = la = new List<(int, int, bool, bool, bool)>();
                if (!la.Any(x => x.room == b && x.door == door && x.wall == wall)) la.Add((b, door, open, wall, stair));
            }
            foreach (var w in L.Walls())
            {
                if (w.RoomB < 0) continue;
                if (w.Open) { Add(w.RoomA, w.RoomB, -1, true, false, false); Add(w.RoomB, w.RoomA, -1, true, false, false); }
                else if (w.DoorId >= 0) { Add(w.RoomA, w.RoomB, w.DoorId, false, false, false); Add(w.RoomB, w.RoomA, w.DoorId, false, false, false); }
                else { Add(w.RoomA, w.RoomB, -1, false, true, false); Add(w.RoomB, w.RoomA, -1, false, true, false); }
            }
            foreach (var s in L.Stairs) { Add(s.RoomA, s.RoomB, -1, true, false, true); Add(s.RoomB, s.RoomA, -1, true, false, true); }
        }
    }

    public sealed partial class Simulation
    {
        [NonSerialized] Acoustics _ac; [NonSerialized] string _acHash;
        // (perf) one delegate for every sight ray (the lambda allocated a new one per ray); reads the door when it is asked
        [NonSerialized] Func<int, bool> _doorOpen;
        bool DoorOpenNow(int door) => S.Layout.Doors[door].Open;
        Acoustics Ac { get { if (_ac == null || _acHash != S.Layout.Hash) { _ac = new Acoustics(S.Layout); _acHash = S.Layout.Hash; } return _ac; } }

        public float RoomLight(int room)
        {
            var r = S.Layout.Room(room); if (r == null) return 0.3f;
            float baseL = r.BaseLight;
            bool on = S.CircuitOn(r.Circuit);
            float l = on ? baseL : (r.Circuit == 0 ? 0.18f : 0.05f);
            if (S.Darkness > 0) l *= (1f - S.Darkness * 0.92f);
            if (S.IsNight && r.Exterior) l = Math.Max(l, 0.12f); // moonlight
            if (r.Type == RoomType.Courtyard) l = Math.Max(l, S.IsNight ? 0.25f : 0.9f);
            // CH03 blackout windows handled by circuit mask; emergency lights on passages
            if (RoomInfo.IsPassage(r.Type)) l = Math.Max(l, 0.1f);
            return MathX.Clamp01(l);
        }

        bool HasLitFlashlight(Actor a)
        {
            var it = Held(a, d => d != null && d.Light); return it != null && S.Flags.TryGetValue("light:" + it.Id, out var on) && on > 0;
        }

        // ------------------------------------------------------------------ vision
        void Perception()
        {
            int idx = 0;
            foreach (var o in S.Actors.Values)
            {
                idx++;
                if ((S.Tick + idx) % 5 != 0) continue;
                if (!o.Alive || o.Status == ActorStatus.Unconscious || o.Pose == Pose.Sleep) continue;
                if (o.StairId >= 0) continue;
                See(o);
            }
        }

        void See(Actor o)
        {
            var k = S.K(o.Id); var g = S.Layout.Nav(o.Pos.f);
            bool flash = HasLitFlashlight(o);
            foreach (var t in S.Actors.Values)
            {
                if (t == o || t.Status == ActorStatus.Executed || t.Status == ActorStatus.Escaped) continue;
                if (t.Pos.f != o.Pos.f) { CloseSighting(k, t.Id); continue; }
                float d = o.Pos.DistXZ(t.Pos); if (d > 26f) { CloseSighting(k, t.Id); continue; }
                float light = Math.Max(RoomLight(t.Room), RoomLight(o.Room) * 0.5f);
                float ang = Math.Abs(MathX.DeltaAngle(o.Yaw, MathX.AngleDeg(t.Pos.x - o.Pos.x, t.Pos.z - o.Pos.z)));
                bool inCone = ang < 78f || d < 1.6f;
                float range = 3.0f + 19f * light;
                if (flash && ang < 22f) range = Math.Max(range, 11f);
                if (t.CarriedBy == null && !t.Alive && t.Pose == Pose.LieFront) range *= 0.9f;
                if (!inCone || d > range) { CloseSighting(k, t.Id); continue; }
                if (!g.Ray(o.Pos.x, o.Pos.z, t.Pos.x, t.Pos.z, _doorOpen ??= DoorOpenNow, false, out _)) { CloseSighting(k, t.Id); continue; }
                // identity confidence
                float idc = MathX.Clamp01((1f - d / (range + 0.01f)) * 0.8f + light * 0.45f);
                if (flash && ang < 22f) idc = Math.Max(idc, 0.75f - d * 0.03f);
                string disg = null;
                if (t.Disguise != null) { var dit = S.I(t.Disguise); disg = dit?.Type; idc *= disg == "TheaterMask" ? 0.12f : disg == "Cloak" ? 0.45f : 0.55f; }
                if (!t.Alive) idc = Math.Max(idc, d < 3f ? 0.95f : idc);
                if (t.Pose == Pose.LieFront && d > 2f) idc *= 0.7f;
                string who = t.Id;
                if (t.BorrowedOutfitOf != null && d > 4.5f && idc < 0.7f) who = t.BorrowedOutfitOf; // IG06: coat = identity (misread)
                Observe(o, k, t, who, idc, disg, d);
            }
            // item glimpses (for weapon/tool knowledge) — cheap subset
            List<Item> pieces = null;
            if ((S.Tick / 5) % 2 == 0)
                foreach (var it in S.Items.Values)
                {
                    if (it.Holder != null || it.Hidden || it.Pos.f != o.Pos.f || it.Room != o.Room) continue;
                    var def = it.Def; if (def == null) continue;
                    if (def.Tag == "part") { if (it.Pos.DistXZ(o.Pos) < 6f) (pieces ??= new List<Item>()).Add(it); continue; }   // a cut-off piece of a body counts as finding the body (handled after the loop)
                    if (!(def.IsWeapon || def.Key || def.Tag == "disguise" || def.Tag == "rescue" || def.Light || def.Tag == "document" || def.Tag == "poison" || def.Tag == "sedate" || def.Tag == "trap" || def.Tag == "thread" || def.Tag == "record" || def.Consumable)) continue;   // + what a schemer would remember seeing (vials, draughts, line, thread, recorders, treats)
                    if (it.Pos.DistXZ(o.Pos) > 7f) continue;
                    k.ItemSeen[it.Id] = (it.Room, S.Clock);
                }
            if (pieces != null) foreach (var it in pieces) Methods.OnPieceSeen(this, o, it);
            if (!o.IsButler) NoticeFurniture(o, k);   // FurnitureChanges.cs: a piece not as they last knew it
        }

        void Observe(Actor o, Knowledge k, Actor t, string who, float idc, string disg, float dist)
        {
            if (k.Open == null) k.Open = new Dictionary<string, Sighting>();
            string held = null; var hit = Held(t); if (hit != null) held = hit.Type;
            bool bloody = t.BloodOnClothes > 0.25f && idc > 0.3f;
            bool attacking = t.Act != null && t.Act.Cur != null && t.Act.Cur.Kind == "Attack" && t.Act.Cur.Actor != null && S.A(t.Act.Cur.Actor)?.Pos.DistXZ(t.Pos) < 2.5f;
            string victim = attacking ? t.Act.Cur.Actor : null;
            bool dead = !t.Alive && (t.Status == ActorStatus.Dead);
            bool uncon = t.Status == ActorStatus.Unconscious;
            string key = t.Id;
            if (k.Open.TryGetValue(key, out var cur) && cur.Room == t.Room && S.Clock - cur.T1 < 1.0 && cur.Dead == dead && cur.Target == who)
            {
                cur.T1 = S.Clock; cur.IdConf = Math.Max(cur.IdConf, idc);
                if (held != null) cur.Held = held; cur.Bloody |= bloody; cur.Carrying |= t.Carrying != null; cur.Running |= t.Running; if (attacking) { cur.Attacking = true; cur.Victim = victim; }
            }
            else
            {
                CloseSighting(k, key);
                var s = new Sighting { Target = who, Room = t.Room, T0 = S.Clock, T1 = S.Clock, IdConf = idc, Dead = dead, Unconscious = uncon, Held = held, Bloody = bloody, Carrying = t.Carrying != null, Disguise = disg, Running = t.Running, Attacking = attacking, Victim = victim, Root = "see:" + o.Id + ":" + S.Seq };
                k.Open[key] = s; k.Sightings.Add(s);
                if (k.Sightings.Count > 2500) k.Sightings.RemoveRange(0, 500);
                if (idc > 0.5f) k.LastSeen[who] = (t.Room, S.Clock);
                OnSighting(o, t, s, dist);
            }
            if (idc > 0.5f) k.LastSeen[who] = (t.Room, S.Clock);
        }

        void CloseSighting(Knowledge k, string target)
        {
            if (k.Open != null && k.Open.TryGetValue(target, out var s) && S.Clock - s.T1 > 0.6) k.Open.Remove(target);
        }

        /// <summary>Hooks for notable sightings: bodies, attacks, weapons, blood.</summary>
        void OnSighting(Actor o, Actor t, Sighting s, float dist)
        {
            if (s.Dead || (s.Unconscious && t.Body.Critical))
            {
                // an attacker still working the plan does not "discover" (scream over, try to rescue) the dying person they just put down
                if (t.Alive && o.PlanId != null && t.Body.Wounds.Any(w => w.By == o.Id && !w.Postmortem)) { }
                else Cases.OnBodySeen(this, o, t, s);
            }
            else if (s.Attacking && s.Victim != o.Id) Cases.OnAttackSeen(this, o, t, s);
            if (o.IsPlayer && (s.Held != null && ItemCatalog.Get(s.Held)?.IsWeapon == true || s.Bloody))
                S.Emit(GameEventType.Notice, o.Id, t.Id, text: s.Bloody ? Cast.GivenOf(t.Id) + "의 옷에 묻은 붉은 얼룩" : Cast.GivenOf(t.Id) + "의 손에 들린 " + ItemCatalog.Get(s.Held)?.Kor);
            Violence.OnSighting(this, o, t);   // --- violence track: someone tied up is freed by whoever finds them
        }

        // ------------------------------------------------------------------ hearing
        public void Sound(SoundKind kind, P3 pos, float loud, string source, string voiceOf = null)
        {
            int room = S.Layout.RoomAt(pos); if (room < 0) return;
            var levels = new Dictionary<int, (float level, int via)>(); var q = new Queue<int>();
            levels[room] = (loud, room); q.Enqueue(room);
            while (q.Count > 0)
            {
                int r = q.Dequeue(); var (lv, via) = levels[r]; if (lv < 0.05f) continue;
                if (!Ac.N.TryGetValue(r, out var ns)) continue;
                foreach (var n in ns)
                {
                    float att;
                    if (n.stair) att = 0.55f;
                    else if (n.open) att = 0.8f;
                    else if (n.wall) att = 0.22f;
                    else { var d = S.Layout.Doors[n.door]; att = d.Open ? 0.75f : 0.35f; }
                    var nr = S.Layout.Room(n.room); var rr = S.Layout.Room(r);
                    if (nr != null && rr != null) { float dist = (float)Math.Sqrt(Math.Pow(nr.Rect.CX - rr.Rect.CX, 2) + Math.Pow(nr.Rect.CZ - rr.Rect.CZ, 2)); att *= (float)Math.Max(0.35, 1 - dist / 60.0); }
                    float nl = lv * att;
                    if (!levels.TryGetValue(n.room, out var old) || old.level < nl) { levels[n.room] = (nl, r == room ? n.room : via); q.Enqueue(n.room); }
                }
            }
            // 권능 '정적': sounds made inside a silenced room barely carry (the sign and residue are still there)
            if (S.Flags.TryGetValue("silence:" + room, out var sil) && S.Clock < sil) { foreach (var key in levels.Keys.ToList()) levels[key] = (levels[key].level * (key == room ? 0.35f : 0.12f), levels[key].via); }
            S.Log("Sound", source, room: room, pos: pos, data: kind.ToString(), secret: true);
            if (!Headless && (kind != SoundKind.Footsteps)) S.Emit(GameEventType.Sound, source, text: kind.ToString(), room: room, pos: pos, value: loud);
            foreach (var l in S.Actors.Values)
            {
                if (!l.Alive || l.Id == source || l.Status == ActorStatus.Unconscious) continue;
                if (!levels.TryGetValue(l.Room, out var lev)) continue;
                float level = lev.level;
                if (l.Room == room) level *= Math.Max(0.35f, 1f - pos.DistXZ(l.Pos) / 30f);
                float mask = 0.06f + S.Noise * 0.22f + (S.Layout.Room(l.Room)?.Type == RoomType.RainCorridor ? 0.2f : 0f);
                bool asleep = l.Pose == Pose.Sleep;
                if (asleep) mask += 0.25f;
                if (level < mask) continue;
                if (kind == SoundKind.Footsteps || kind == SoundKind.Door) { if (level < mask + 0.12f) continue; }
                var k = S.K(l.Id);
                int guess = l.Room == room ? room : (S.Noise > 0.3f ? lev.via : (level > 0.3f ? room : lev.via));
                string voice = null; float vc = 0;
                if (voiceOf != null && level > mask + 0.18f && (l.Room == room || level > 0.35f)) { voice = voiceOf; vc = MathX.Clamp01(level * 1.5f - S.Noise * 0.5f); if (!Met(l.Id, voiceOf)) vc *= 0.3f; }
                var h = new HeardSound { Kind = kind, Room = room, GuessRoom = guess, Clock = S.Clock, Loud = level, Voice = voice, VoiceConf = vc, Root = "snd:" + S.Seq };
                if (kind != SoundKind.Footsteps || level > 0.3f) { k.Heard.Add(h); if (k.Heard.Count > 1500) k.Heard.RemoveRange(0, 300); }
                if (asleep && (kind == SoundKind.Scream || kind == SoundKind.Crash || kind == SoundKind.GlassBreak || kind == SoundKind.Shout || kind == SoundKind.Knock && l.Room == room || kind == SoundKind.Announcement) && level > 0.3f) { if (l.IsPlayer) NoteWakeSound(h); /* time-on-demand: the reason for waking */ Wake(l); }
                OnHeard(l, h, source);
            }
        }

        public bool Met(string a, string b) => S.HasRel(a, b) && S.R(a, b).Talks > 0 || S.K(a).Sightings.Any(s => s.Target == b && s.IdConf > 0.6f);

        void Wake(Actor a)
        {
            if (a.Pose != Pose.Sleep) return;
            a.Pose = Pose.Stand; if (a.Act != null && a.Act.Id == "sleep") EndActivity(a, false); a.NextThink = S.Clock;
            if (a.IsPlayer) OnPlayerWoken();   // --- time-on-demand: a Woken stop with its reason (also the legacy alarm)
        }

        void OnHeard(Actor l, HeardSound h, string source)
        {
            if (l.IsPlayer)
            {
                if (h.Kind == SoundKind.Scream || h.Kind == SoundKind.Crash || h.Kind == SoundKind.GlassBreak || h.Kind == SoundKind.Strike && h.Loud > 0.2f || h.Kind == SoundKind.Press || h.Kind == SoundKind.Gunshot)   // (+ violence track: a gunshot)
                {
                    // --- time-on-demand (begin): an urgent sound the player (awake, or just woken by it) hears stops time and runs it
                    if (l.Pose != Pose.Sleep)
                    {
                        bool woke = _wakeSoundTick == S.Tick && _wakeSound == h;
                        string where = h.GuessRoom == l.Room ? "가까이" : S.RoomName(h.GuessRoom) + " 쪽";
                        RaiseStop(StopKind.Heard, StopClass.Critical, woke ? (h.Kind == SoundKind.Scream ? "비명에 깼다" : SoundText(h.Kind) + " 소리에 깼다") : (h.Kind == SoundKind.Scream ? "비명이 들렸다" : SoundText(h.Kind) + " 소리가 들렸다"), where, source, h.GuessRoom);
                        EmergencyTrigger(1, null, h.Kind);
                    }
                    // --- time-on-demand (end)
                    S.Emit(GameEventType.Notice, l.Id, text: SoundText(h.Kind) + " 소리가 들렸다 (" + (h.GuessRoom == l.Room ? "바로 근처" : S.RoomName(h.GuessRoom) + " 쪽") + ")", key: "heard");
                }
                // --- time-on-demand: someone knocking at a door of the player's room (awake)
                else if (h.Kind == SoundKind.Knock && l.Pose != Pose.Sleep && KnockAtRoom(l.Room, source)) RaiseStop(StopKind.Knock, StopClass.Social, "누군가 문을 두드린다", S.RoomName(l.Room), source, l.Room);
                return;
            }
            if (h.Kind == SoundKind.Scream || h.Kind == SoundKind.Crash || h.Kind == SoundKind.Strike && h.Loud > 0.25f || h.Kind == SoundKind.GlassBreak || h.Kind == SoundKind.Press || h.Kind == SoundKind.Gunshot)   // (+ violence track)
                Cases.OnAlarmHeard(this, l, h, source);
        }

        public static string SoundText(SoundKind k)
        {
            switch (k)
            {
                case SoundKind.Scream: return "비명"; case SoundKind.Strike: return "둔탁한 충격"; case SoundKind.Struggle: return "몸싸움하는";
                case SoundKind.Fall: return "무언가 쓰러지는"; case SoundKind.GlassBreak: return "유리 깨지는"; case SoundKind.Crash: return "요란하게 부서지는";
                case SoundKind.Splash: return "물 튀는"; case SoundKind.Press: return "기계가 내리찍는"; case SoundKind.Machine: return "기계 돌아가는";
                case SoundKind.Door: return "문 여닫는"; case SoundKind.DoorSlam: return "문이 쾅 닫히는"; case SoundKind.Knock: return "노크"; case SoundKind.Running: return "뛰어가는 발";
                case SoundKind.Footsteps: return "발"; case SoundKind.Shout: return "고함"; case SoundKind.Talk: return "말"; case SoundKind.Switch: return "스위치 딸깍하는";
                case SoundKind.Bell: return "호출벨"; case SoundKind.Music: return "음악"; case SoundKind.Laugh: return "웃음"; case SoundKind.Cry: return "울음";
                case SoundKind.Announcement: return "안내 방송"; case SoundKind.Rain: return "비 오는"; case SoundKind.Static: return "지직거리는"; case SoundKind.Clock: return "괘종시계";
                case SoundKind.Gunshot: return "총";   // --- violence track
                case SoundKind.Scrape: return "무언가 바닥에 끌리는";
            }
            return k.ToString();
        }
    }
}
