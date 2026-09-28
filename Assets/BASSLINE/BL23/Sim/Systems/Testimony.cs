using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>What a person can say: honest answers from their own memory, and deliberate lies/omissions by a
    /// culprit or a protector. Lies are structured (known fact, motive, audience, alternative account) and can be caught.</summary>
    public static class Testimony
    {
        public sealed class Answer { public string Key; public Dictionary<string, string> Slots = new Dictionary<string, string>(); public Prop Prop; public bool Lie; public string Note; }

        /// <summary>Case-relevant window: from 2h before the earliest confirmed death estimate up to discovery.</summary>
        public static (double t0, double t1, int room) CaseWindow(GameState S)
        {
            var incs = S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Confirmed).ToList();
            if (incs.Count == 0) return (S.Clock - 120, S.Clock, -1);
            // the public doesn't know the exact death time; people talk about "the last couple of hours before it was found"
            double found = incs.Min(i => i.DiscoverClock); int room = incs.OrderBy(i => i.DiscoverClock).First().FoundRoom;
            return (found - 150, found, room);
        }

        // where was I (self knowledge from my own movement record)
        static List<(int room, double t0, double t1)> MyTrack(GameState S, string id, double t0, double t1)
        {
            var enters = S.Ledger.Where(e => e.Type == "Enter" && e.Actor == id && e.Clock <= t1).OrderBy(e => e.Seq).ToList();
            var res = new List<(int, double, double)>();
            for (int i = 0; i < enters.Count; i++)
            {
                double a = enters[i].Clock, b = i + 1 < enters.Count ? enters[i + 1].Clock : t1;
                if (b < t0) continue;
                res.Add((enters[i].Room, Math.Max(a, t0), Math.Min(b, t1)));
            }
            if (res.Count == 0) { var act = S.A(id); res.Add((act?.Room ?? -1, t0, t1)); }
            return res;
        }

        /// <summary>Answer "where were you around the incident?"</summary>
        public static Answer Where(Simulation sim, Actor npc, string asker)
        {
            var S = sim.S; var (t0, t1, _) = CaseWindow(S);
            var track = MyTrack(S, npc.Id, t0, t1).Where(x => !RoomInfo.IsPassage(S.Layout.Room(x.room)?.Type ?? RoomType.Corridor) || x.t1 - x.t0 > 8).OrderByDescending(x => x.t1 - x.t0).ToList();
            var plan = S.Plans.Values.FirstOrDefault(p => p.Actor == npc.Id && (p.Stage == "Done" || p.Step > 0) && p.Formed > t0 - 300);
            var ans = new Answer();
            if (plan != null && plan.Steps.Any(s => s.Done && (s.Kind == "Attack" || s.Kind == "KnockOut" || s.Kind == "Drown" || s.Kind == "ArmPress")))
            {
                // culprit: claims to have been in the alibi room (or a room they visited before) for the key window
                int claim = plan.AlibiRoom >= 0 ? plan.AlibiRoom : track.Where(x => x.room != plan.KillRoom).Select(x => x.room).FirstOrDefault();
                if (claim < 0 || claim == plan.KillRoom) claim = S.Layout.First(RoomType.Lounge).Id;
                var killEv = S.Ledger.FirstOrDefault(e => e.Type == "AttackBegin" && e.Actor == npc.Id);
                double ct = killEv?.Clock ?? t1 - 60;
                ans.Key = Simulation.LifeLieKey(npc.Id, "alibi_where"); ans.Lie = true;   // --- daily-life: lie_alibi_where in their own lying voice (the tell shows)
                ans.Slots["time"] = ClockFmt.Vague(ct - 10); ans.Slots["place"] = PlaceFor(S, npc.Id, claim);
                ans.Prop = new Prop { Kind = PropKind.AtPlace, A = npc.Id, Room = claim, T0 = ct - 25, T1 = ct + 20 };
                plan.AlibiClaimRoom = claim; plan.AlibiClaimRoomName = S.RoomName(claim);
                SealOrCompare(sim, npc, asker, claim, ct - 25, ct + 20);
                S.Log("Lie", npc.Id, asker, room: claim, data: $"alibi {S.RoomName(claim)} {ClockFmt.Vague(ct - 25)}-{ClockFmt.Vague(ct + 20)}", secret: true);
                return ans;
            }
            if (track.Count == 0) { ans.Key = "saw_nothing"; return ans; }
            SealOrCompare(sim, npc, asker, track[0].room, track[0].t0, track[0].t1);
            var main = track[0];
            // honest, but memory is coarse: rounds to 10 minutes
            double drift = Grammars.PerceivedTime(S, npc.Id, main.room, main.t0, out _) - main.t0;   // IG09: watchless people read the room clock
            double m0 = Math.Floor((main.t0 + drift) / 10) * 10, m1 = Math.Ceiling((main.t1 + drift) / 10) * 10;
            // was I with someone? (people I saw in that room during that span)
            var k = S.K(npc.Id);
            var with = k.Sightings.Where(s => s.Room == main.room && s.T0 < main.t1 && s.T1 > main.t0 && s.IdConf > 0.6f && s.Target != npc.Id && !s.Dead).GroupBy(s => s.Target).OrderByDescending(g => g.Sum(s => s.T1 - s.T0)).FirstOrDefault();
            // hiding something unrelated (a private secret) can also produce a small lie — but not about murder
            if (with != null)
            {
                ans.Key = "alibi_with"; ans.Slots["t"] = "@" + with.Key; ans.Slots["place"] = PlaceFor(S, npc.Id, main.room); ans.Slots["time"] = ClockFmt.Vague(m0);
                ans.Prop = new Prop { Kind = PropKind.WithPerson, A = npc.Id, B = with.Key, Room = main.room, T0 = Math.Max(main.t0, with.Min(s => s.T0)), T1 = Math.Min(main.t1, with.Max(s => s.T1)) };
            }
            else
            {
                ans.Key = "alibi_where"; ans.Slots["place"] = PlaceFor(S, npc.Id, main.room); ans.Slots["time"] = ClockFmt.Vague(m0);
                ans.Prop = new Prop { Kind = PropKind.AtPlace, A = npc.Id, Room = main.room, T0 = m0, T1 = m1 };
            }
            return ans;
        }

        /// <summary>How a speaker names a room in their own statement ("my room" for their own bedroom).</summary>
        static string PlaceFor(GameState S, string speaker, int room)
        {
            var r = S.Layout.Room(room);
            return r != null && r.Type == RoomType.Bedroom && r.Owner == speaker ? "내 방" : S.RoomName(room);
        }

        /// <summary>CH04: the first "where were you" answer of each person is sealed; a later different answer is a recorded correction.</summary>
        static void SealOrCompare(Simulation sim, Actor npc, string asker, int room, double t0, double t1)
        {
            var S = sim.S; var r = S.Rule("CH04"); if (r == null) return;
            var sealedLine = r.Targets.FirstOrDefault(x => x.StartsWith(npc.Id + "|"));
            if (sealedLine == null) { r.Targets.Add($"{npc.Id}|{room}|{t0:0}|{t1:0}"); S.Log("StatementSealed", npc.Id, data: S.RoomName(room)); return; }
            var p = sealedLine.Split('|'); int room0 = int.Parse(p[1]);
            if (room0 == room || asker != Cast.Player) return;
            Evidences.Add(sim, Cast.Player, EvKind.Record, $"{Cast.GivenOf(npc.Id)}의 바뀐 진술", $"처음 한 말(봉인됨): {S.RoomName(room0)}에 있었다. 지금 하는 말: {S.RoomName(room)}에 있었다. 말을 바꾼 기록이 따로 남는다.", "최초 진술 봉인 규칙", $"ch04:{npc.Id}:{room}", double.Parse(p[2]), double.Parse(p[3]), room0, "처음 한 말과 지금 하는 말이 다르다는 사실", "어느 쪽이 사실인지", true,
                new Prop { Kind = PropKind.Lie, A = npc.Id, Room = room0, T0 = double.Parse(p[2]), T1 = double.Parse(p[3]), Value = $"진술 정정 {S.RoomName(room0)}→{S.RoomName(room)}" });
        }

        /// <summary>Answer "did you see anyone / anything unusual?" from own sightings in the window.</summary>
        public static List<Answer> Saw(Simulation sim, Actor npc, string asker, int max = 2)
        {
            var S = sim.S; var (t0, t1, room) = CaseWindow(S); var k = S.K(npc.Id); var res = new List<Answer>();
            var plan = S.Plans.Values.FirstOrDefault(p => p.Actor == npc.Id && (p.Stage == "Done" || p.Step > 0));
            var protectees = S.Living.Where(x => x.Id != npc.Id && (S.R(npc.Id, x.Id).Attach > 0.55f || S.R(npc.Id, x.Id).Tags.Contains("lover") || S.R(npc.Id, x.Id).Tags.Contains("family"))).Select(x => x.Id).ToHashSet();
            var notable = k.Sightings.Where(s => s.T1 >= t0 && s.T0 <= t1 && s.Target != npc.Id && !s.Dead && s.IdConf > 0.35f)
                .Select(s => new { s, score = (s.Room == room ? 3 : 0) + (s.Bloody ? 4 : 0) + (s.Held != null && ItemCatalog.Get(s.Held)?.IsWeapon == true ? 3 : 0) + (s.Running ? 1 : 0) + (s.Carrying ? 3 : 0) + (s.Disguise != null ? 3 : 0) + (s.Attacking ? 6 : 0) + (S.Layout.Room(s.Room)?.Type == RoomType.PowerRoom || S.Layout.Room(s.Room)?.Type == RoomType.MachineRoom ? 2 : 0) })
                .Where(x => x.score > 0).OrderByDescending(x => x.score).ThenByDescending(x => x.s.T1).ToList();
            foreach (var n in notable)
            {
                if (res.Count >= max) break;
                var s = n.s;
                // protectors omit what could hurt someone they love; culprits omit sightings of themselves (can't happen) and of nothing else
                if (protectees.Contains(s.Target) && (s.Bloody || s.Held != null || s.Room == room)) { S.Log("Omit", npc.Id, s.Target, data: "protect", secret: true); continue; }
                var a = new Answer();
                // IG09: a watchless witness states the time the room clock showed
                double shift = Grammars.PerceivedTime(S, npc.Id, s.Room, s.T0, out _) - s.T0;
                if (shift != 0) { s = new Sighting { Target = s.Target, Room = s.Room, T0 = s.T0 + shift, T1 = s.T1 + shift, IdConf = s.IdConf, Held = s.Held, Bloody = s.Bloody, Carrying = s.Carrying, Disguise = s.Disguise, Running = s.Running, Attacking = s.Attacking, Victim = s.Victim, Root = s.Root, Direct = s.Direct }; }
                if (s.Disguise != null) { a.Key = "saw_person_unsure"; a.Slots["place"] = S.RoomName(s.Room); a.Slots["time"] = ClockFmt.Vague(s.T0); a.Prop = new Prop { Kind = PropKind.Disguised, Room = s.Room, T0 = s.T0, T1 = s.T1, Item = s.Disguise, Value = "root:" + s.Root }; }
                else if (s.IdConf < 0.55f) { a.Key = "saw_person_unsure"; a.Slots["place"] = S.RoomName(s.Room); a.Slots["time"] = ClockFmt.Vague(s.T0); a.Prop = new Prop { Kind = PropKind.SawActor, A = npc.Id, B = s.Target, Room = s.Room, T0 = s.T0, T1 = s.T1, Value = "root:" + s.Root, Item = "unsure" }; }
                else if (s.Held != null && ItemCatalog.Get(s.Held)?.IsWeapon == true) { a.Key = "saw_item"; a.Slots["t"] = "@" + s.Target; a.Slots["item"] = ItemCatalog.Get(s.Held).Kor; a.Slots["place"] = S.RoomName(s.Room); a.Prop = new Prop { Kind = PropKind.Held, A = s.Target, Item = s.Held, Room = s.Room, T0 = s.T0, T1 = s.T1, Value = "root:" + s.Root }; }
                else { a.Key = "saw_person"; a.Slots["t"] = "@" + s.Target; a.Slots["place"] = S.RoomName(s.Room); a.Slots["time"] = ClockFmt.Vague(s.T0); a.Prop = new Prop { Kind = PropKind.AtPlace, A = s.Target, Room = s.Room, T0 = s.T0, T1 = s.T1, Value = "root:" + s.Root }; }
                res.Add(a);
            }
            // IG10: a go-between who carried the luring note — confesses only to someone they trust (or if very honest)
            foreach (var inc in S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Confirmed))
            {
                var cf = k.Facts.FirstOrDefault(f => f.StartsWith("courier:") && f.Split(':')[2] == inc.Victim);
                if (cf == null || res.Count >= max + 1) continue;
                var parts = cf.Split(':'); string sender = parts[1]; double when = double.Parse(parts[3]);
                bool trusts = asker != null && S.HasRel(npc.Id, asker) && S.R(npc.Id, asker).Trust > 0.45f;
                if (trusts || npc.Def.P.Honesty > 0.8f)
                    res.Add(new Answer { Key = "courier_confess", Slots = { { "t", "@" + sender }, { "victim", Cast.NameOf(inc.Victim) }, { "time", ClockFmt.Vague(when) } }, Prop = new Prop { Kind = PropKind.Loaned, A = npc.Id, B = sender, Item = "Invitation", T0 = when, T1 = when, Value = "courier:" + inc.Victim } });
                else S.Log("Omit", npc.Id, sender, data: "courier-fear", secret: true);
            }
            // handovers they happened to watch (a note passing hands shortly before the case)
            foreach (var f in k.Facts.Where(f => f.StartsWith("handover:")).ToList())
            {
                if (res.Count >= max + 1) break;
                var p = f.Split(':'); if (p.Length < 5 || p[3] != "Invitation") continue; double when = double.Parse(p[4]);
                if (when < t0 - 240 || when > t1) continue;
                if (protectees.Contains(p[1])) continue;
                res.Add(new Answer { Key = "saw_handover", Slots = { { "t", "@" + p[1] }, { "u", "@" + p[2] }, { "time", ClockFmt.Vague(when) } }, Prop = new Prop { Kind = PropKind.Loaned, A = p[2], B = p[1], Item = "Invitation", T0 = when, T1 = when, Value = "handover" } });
            }
            // a deceitful culprit may point at someone else (a fabricated sighting)
            if (plan != null && npc.Def.Deceit >= 75 && res.Count < max && S.R(Stream.Trial).Chance(0.4))
            {
                var scape = Scapegoat(sim, npc);
                if (scape != null)
                {
                    var r = S.Layout.Room(room);
                    res.Add(new Answer { Key = Simulation.LifeLieKey(npc.Id, "saw_person") /* --- daily-life: lie_saw_person */, Lie = true, Slots = { { "t", "@" + scape }, { "place", r?.Name ?? "그쪽" }, { "time", ClockFmt.Vague(t1 - 40) } }, Prop = new Prop { Kind = PropKind.AtPlace, A = scape, Room = room, T0 = t1 - 45, T1 = t1 - 35 } });
                    S.Log("Lie", npc.Id, scape, data: "fabricated sighting", secret: true);
                }
            }
            if (res.Count == 0) res.Add(new Answer { Key = "saw_nothing" });
            return res;
        }

        public static Answer Heard(Simulation sim, Actor npc, string asker)
        {
            var S = sim.S; var (t0, t1, room) = CaseWindow(S); var k = S.K(npc.Id);
            var h = k.Heard.Where(x => x.Clock >= t0 && x.Clock <= t1 && (x.Kind == SoundKind.Scream || x.Kind == SoundKind.Strike || x.Kind == SoundKind.Crash || x.Kind == SoundKind.GlassBreak || x.Kind == SoundKind.Struggle || x.Kind == SoundKind.Press || x.Kind == SoundKind.Splash || x.Kind == SoundKind.Fall || x.Kind == SoundKind.DoorSlam || x.Kind == SoundKind.Running))
                .OrderByDescending(x => x.Loud).FirstOrDefault();
            if (h == null)
            {
                // a voice from a room: who it sounded like — not proof that the person was there (IG03)
                var v = k.Heard.Where(x => x.Clock >= t0 && x.Clock <= t1 && x.Voice != null && x.Voice != npc.Id && x.VoiceConf > 0.4f).OrderByDescending(x => x.VoiceConf).FirstOrDefault();
                if (v == null) return new Answer { Key = "saw_nothing" };
                return new Answer { Key = "heard_voice", Slots = { { "t", "@" + v.Voice }, { "time", ClockFmt.Vague(v.Clock) }, { "place", S.RoomName(v.GuessRoom) } }, Prop = new Prop { Kind = PropKind.Heard, A = npc.Id, B = v.Voice, Room = v.GuessRoom, T0 = v.Clock - 2, T1 = v.Clock + 2, Value = "voice:" + v.Voice } };
            }
            return new Answer { Key = "heard_sound", Slots = { { "time", ClockFmt.Vague(h.Clock) }, { "place", S.RoomName(h.GuessRoom) }, { "sound", Simulation.SoundText(h.Kind) + " 소리" } }, Prop = new Prop { Kind = PropKind.Heard, A = npc.Id, Room = h.GuessRoom, T0 = h.Clock - 2, T1 = h.Clock + 2, Value = h.Kind.ToString() } };
        }

        public static Answer Suspect(Simulation sim, Actor npc)
        {
            var S = sim.S; UpdateSuspicion(sim, npc); var k = S.K(npc.Id);
            var top = k.Suspicion.Where(kv => S.A(kv.Key)?.Alive == true && kv.Key != npc.Id).OrderByDescending(kv => kv.Value).FirstOrDefault();
            if (top.Key == null || top.Value < 0.2f) return new Answer { Key = "saw_nothing" };
            return new Answer { Key = "suspect", Slots = { { "t", "@" + top.Key }, { "reason", Reason(sim, npc, top.Key) } } };
        }

        public static string Scapegoat(Simulation sim, Actor npc)
        {
            var S = sim.S; var rng = S.R(Stream.Trial);
            var c = S.Living.Where(x => x.Id != npc.Id && !(S.R(npc.Id, x.Id).Attach > 0.5f) && !S.R(npc.Id, x.Id).Tags.Contains("family")).ToList();
            if (c.Count == 0) return null;
            return rng.Weighted(c, x => 0.2 + Math.Max(0, S.R(npc.Id, x.Id).Grudge) + Math.Max(0, -S.R(npc.Id, x.Id).Like) + (x.IsPlayer ? 0.3 : 0)).Id;
        }

        // ------------------------------------------------------------------ suspicion: evidence first, bias second
        public static void UpdateSuspicion(Simulation sim, Actor npc)
        {
            var S = sim.S; var k = S.K(npc.Id);
            var incs = S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Confirmed).ToList();
            if (incs.Count == 0) return;
            var (t0, t1, room) = CaseWindow(S);
            var deathWin = k.Evidence.SelectMany(e => e.Props).Where(p => p.Kind == PropKind.DeathWindow && p.Value == "exam").ToList();
            double d0 = deathWin.Count > 0 ? deathWin.Max(p => p.T0) : t0, d1 = deathWin.Count > 0 ? deathWin.Min(p => p.T1) : t1;
            if (d1 < d0) { d0 = t0; d1 = t1; }
            var victims = incs.Select(i => i.Victim).ToHashSet();
            var sceneRooms = incs.Select(i => i.FoundRoom).ToHashSet();
            var scores = new Dictionary<string, float>();
            void Add(string id, float v) { if (id == null || id == npc.Id || victims.Contains(id)) return; scores[id] = (scores.TryGetValue(id, out var o) ? o : 0) + v; }
            // own sightings
            foreach (var s in k.Sightings.Where(s => s.T1 >= d0 - 15 && s.T0 <= d1 + 10 && !s.Dead && s.IdConf > 0.4f))
            {
                if (sceneRooms.Contains(s.Room)) Add(s.Target, 0.35f);
                if (s.Bloody) Add(s.Target, 0.6f); if (s.Carrying) Add(s.Target, 0.5f);
                if (s.Held != null && ItemCatalog.Get(s.Held)?.IsWeapon == true) Add(s.Target, 0.4f);
                if (s.Attacking && victims.Contains(s.Victim)) Add(s.Target, 2.0f);
            }
            // statements received (hearsay counts less; same root counted once)
            foreach (var st in k.Statements.GroupBy(x => x.Root).Select(g => g.First()))
            {
                var p = st.Prop; float w = st.Hearsay ? 0.5f : 0.8f; w *= MathX.Clamp01(0.4f + S.R(npc.Id, st.Speaker).Trust);
                if ((p.Kind == PropKind.AtPlace || p.Kind == PropKind.SawActor) && p.A != st.Speaker && sceneRooms.Contains(p.Room) && p.T1 >= d0 - 15 && p.T0 <= d1 + 10) Add(p.A == npc.Id ? null : (p.Kind == PropKind.SawActor ? p.B : p.A), 0.3f * w);
                if (p.Kind == PropKind.Held && ItemCatalog.Get(p.Item)?.IsWeapon == true) Add(p.A, 0.35f * w);
            }
            // contradictions between what someone said and what I saw (lie detection)
            foreach (var st in k.Statements.Where(x => x.Prop.Kind == PropKind.AtPlace && x.Prop.A == x.Speaker))
                foreach (var s in k.Sightings.Where(s => s.Target == st.Speaker && s.IdConf > 0.6f && s.Room != st.Prop.Room && s.T0 < st.Prop.T1 && s.T1 > st.Prop.T0))
                { Add(st.Speaker, 0.9f); k.Facts.Add($"caught-lie:{st.Speaker}:{st.Id}"); }
            // motives I know about
            foreach (var v in victims) foreach (var id in S.Living.Select(x => x.Id))
                {
                    if (k.Facts.Contains($"argued:{id}:{v}") || k.Facts.Contains($"argued:{v}:{id}")) Add(id, 0.2f);
                    if (k.Facts.Contains("attacked-by:" + id)) Add(id, 3f);
                }
            // bias: dislike raises, affection lowers — but never erases what was seen
            foreach (var id in scores.Keys.ToList())
            {
                var r = S.R(npc.Id, id);
                scores[id] = (float)(scores[id] * (1 + Math.Max(0, -r.Opinion) * 0.5) * (1 - MathX.Clamp01(r.Attach) * 0.35));
            }
            foreach (var kv in scores) k.Suspicion[kv.Key] = Math.Max(k.Suspicion.TryGetValue(kv.Key, out var old) ? old * 0.6f : 0, kv.Value);
        }

        public static string Reason(Simulation sim, Actor npc, string target)
        {
            var S = sim.S; var k = S.K(npc.Id);
            if (k.Facts.Any(f => f.StartsWith("caught-lie:" + target))) return "하는 말이 직접 본 거랑 달라서";
            var (t0, t1, room) = CaseWindow(S);
            var s = k.Sightings.Where(x => x.Target == target && x.T1 >= t0 - 15 && x.T0 <= t1).OrderByDescending(x => (x.Bloody ? 3 : 0) + (x.Held != null ? 2 : 0) + (x.Room == room ? 1 : 0)).FirstOrDefault();
            if (s != null && s.Bloody) return "옷에 피 같은 게 묻어 있어서";
            if (s != null && s.Held != null) return LineBank.FixParticles(ItemCatalog.Get(s.Held)?.Kor + "을(를) 들고 있는 걸 봐서");
            if (s != null && s.Room == room) return "그 시간에 현장 근처에 있어서";
            if (k.Facts.Any(f => f.Contains(target) && f.StartsWith("argued"))) return "피해자랑 다투는 걸 봐서";
            return "왠지 하는 짓이 수상해서";
        }

        // ------------------------------------------------------------------ NPC↔NPC exchanges
        public static void NpcInterview(Simulation sim, Actor asker, Actor t)
        {
            var S = sim.S;
            var trust = S.R(t.Id, asker.Id).Trust + S.R(t.Id, asker.Id).Like;
            if (trust < -0.3f) { sim.Speak(t, "refuse_answer", asker.Id); return; }
            var w = Where(sim, t, asker.Id); var line = sim.Speak(t, w.Key, asker.Id, w.Slots, w.Prop, w.Lie);
            foreach (var s in Saw(sim, t, asker.Id, 1)) sim.Speak(t, s.Key, asker.Id, s.Slots, s.Prop, s.Lie);
            var h = Heard(sim, t, asker.Id); if (h.Prop != null) sim.Speak(t, h.Key, asker.Id, h.Slots, h.Prop);
            UpdateSuspicion(sim, asker);
        }

        public static void NpcShare(Simulation sim, Actor a, Actor t)
        {
            var S = sim.S; var ka = S.K(a.Id);
            UpdateSuspicion(sim, a);
            var top = ka.Suspicion.Where(kv => kv.Key != t.Id && S.A(kv.Key)?.Alive == true).OrderByDescending(kv => kv.Value).FirstOrDefault();
            // the culprit steers the conversation toward their scapegoat
            var plan = S.Plans.Values.FirstOrDefault(p => p.Actor == a.Id && (p.Stage == "Done" || p.Step > 0));
            if (plan != null) { var sc = Scapegoat(sim, a); if (sc != null && sc != t.Id) { sim.Speak(a, "suspect", t.Id, new Dictionary<string, string> { { "t", "@" + sc }, { "reason", "그 시간에 혼자였던 것 같아서" } }); var kt = S.K(t.Id); kt.Suspicion[sc] = (kt.Suspicion.TryGetValue(sc, out var v0) ? v0 : 0) + 0.15f * MathX.Clamp01(S.R(t.Id, a.Id).Trust + 0.3f); return; } }
            if (top.Key != null && top.Value > 0.3f)
            {
                sim.Speak(a, "suspect", t.Id, new Dictionary<string, string> { { "t", "@" + top.Key }, { "reason", Reason(sim, a, top.Key) } });
                var kt = S.K(t.Id); kt.Suspicion[top.Key] = (kt.Suspicion.TryGetValue(top.Key, out var v) ? v : 0) + 0.2f * MathX.Clamp01(S.R(t.Id, a.Id).Trust + 0.3f);
            }
            // share one piece of hard evidence (card copy keeps the same root)
            var ev = ka.Evidence.Where(e => e.Important || e.Kind == EvKind.Body || e.Kind == EvKind.ObjectState).OrderByDescending(e => e.Acquired).FirstOrDefault(e => !e.SharedWith.Contains(t.Id));
            if (ev != null)
            {
                ev.SharedWith.Add(t.Id);
                sim.Speak(a, "share_find", t.Id, new Dictionary<string, string> { { "item", t.IsPlayer ? CaseBoard.Describe(sim, ev).Title : ev.Title }, { "place", S.RoomName(ev.Room) } });
                if (t.IsPlayer) Evidences.Relay(sim, ev, a.Id);   // the player's notebook: one card per thing, hearsay marked
                else
                {
                    var copy = Evidences.Add(sim, t.Id, ev.Kind, ev.Title + " (전해 들음)", ev.Desc, Cast.NameOf(a.Id) + "에게서 들음", ev.Root, ev.T0, ev.T1, ev.Room, ev.CanKnow, "직접 보지 않았다 — " + ev.CannotKnow, false, ev.Props.Select(p => p.Clone()).ToArray());
                    copy.Copy = true;
                }
            }
            Relations.Change(S, t.Id, a.Id, trust: 0.03f);
        }
    }
}
