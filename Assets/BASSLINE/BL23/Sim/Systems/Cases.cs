using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    public sealed partial class Simulation
    {
        public void NextStepPublic(Actor a) => NextStep(a);
        public void MoveAlongPublic(Actor a, bool run = false) => MoveAlong(a, new ActionStep { Run = run });
        public void WakePublic(Actor a) => Wake(a);
        public P3 SnapPublic(P3 p) => Snap(p);

        public void SetCircuit(int circuit, bool on, string by)
        {
            if (circuit < 0) return;
            bool was = S.CircuitOn(circuit);
            if (on) S.CircuitMask |= (1 << circuit); else S.CircuitMask &= ~(1 << circuit);
            if (was == on) return;
            S.Log("Circuit", by, data: $"{circuit}:{(on ? "on" : "off")}", secret: true);
            S.Emit(GameEventType.Light, by, id: circuit, value: on ? 1 : 0);
            var pr = S.Layout.First(RoomType.PowerRoom);
            if (pr != null) { Sound(SoundKind.Switch, new P3(pr.Floor, pr.Rect.CX, pr.Rect.CZ), 0.3f, by); if (by != null) AddTrace("SwitchTouched", new P3(pr.Floor, pr.Rect.CX, pr.Rect.CZ), pr.Id, by, null, 0.3f, 1, $"{S.Layout.Circuits[circuit].Name} 회로 레버", $"누군가 최근 {S.Layout.Circuits[circuit].Name} 레버를 건드렸다 (지금은 {(on ? "켜져" : "꺼져")} 있다)", "누가, 정확히 언제 그랬는지"); }
            // everyone in affected rooms notices the lights change (a public, perceivable fact)
            foreach (var a in S.Actors.Values)
            {
                if (!a.Alive) continue; var r = S.Layout.Room(a.Room); if (r == null || r.Circuit != circuit) continue;
                S.K(a.Id).Facts.Add($"lights:{circuit}:{(on ? "on" : "off")}:{(int)S.Clock}");
                if (a.IsPlayer) S.Emit(GameEventType.Notice, a.Id, text: on ? "불이 다시 들어왔다" : "갑자기 불이 꺼졌다!", key: "lights");
            }
        }
    }

    public static class Incidents
    {
        public static void OnAttackBegin(Simulation sim, Actor a, Actor t, MurderPlan plan)
        {
            var S = sim.S;
            S.Log("AttackBegin", a.Id, t.Id, plan: plan?.Id, room: t.Room, pos: t.Pos, secret: true);
            Replay.Pin(sim, plan?.StartTick ?? S.Tick - 600);
        }
    }

    /// <summary>Discovery → report → confirmation → investigation → assembly. Also NPC investigation behaviour.</summary>
    public static class Cases
    {
        // ------------------------------------------------------------------ deaths and discovery
        public static void OnDeath(Simulation sim, Actor v, string by, string cause, Wound w)
        {
            var S = sim.S;
            var plan = S.Plans.Values.FirstOrDefault(p => p.Actor == by && p.Target == v.Id && p.Stage != "Aborted");
            bool murder = by != null && by != v.Id && (plan != null || (S.A(by)?.IsPlayer ?? false) || S.Flags.ContainsKey("attackstart:" + by + ":" + v.Id));
            var inc = new Incident
            {
                Id = S.NewId("inc"), Loop = S.Loop, Chapter = S.Chapter, Victim = v.Id, Culprit = murder ? by : null, PlanId = plan?.Id, Method = plan?.Grammar ?? (murder ? "Direct" : "Accident"),
                Weapon = w?.Weapon, WeaponType = S.I(w?.Weapon)?.Type, Dmg = w?.Type ?? DamageType.None, Region = w?.Region ?? BodyRegion.Chest,
                DeathRoom = v.Room, DeathClock = S.Clock, ResultSeq = v.Body.DeathSeq, Murder = murder, Accident = !murder,
                CauseRoom = S.Ledger.LastOrDefault(e => e.Type == "AttackBegin" && e.Target == v.Id)?.Room ?? v.Room,
                CauseClock = S.Ledger.LastOrDefault(e => e.Type == "AttackBegin" && e.Target == v.Id)?.Clock ?? S.Clock,
                SegmentStartTick = plan?.StartTick ?? Math.Max(0, S.Tick - 3000)
            };
            S.Incidents[inc.Id] = inc; S.Ch.Incidents.Add(inc.Id);
            foreach (var r in S.Ch.Reservations.Where(r => r.Target == v.Id && !r.Released)) r.Consumed = true;
            int deaths = S.Incidents.Values.Count(i => i.Loop == S.Loop && i.Chapter == S.Chapter);
            if (deaths > S.Ch.VictimCap) { S.Ch.UnexpectedCasualty = true; S.Dev("UnexpectedCasualty: deaths exceed cap"); }
            // judgement target: earliest murder death this chapter
            var first = S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Murder).OrderBy(i => i.ResultSeq).FirstOrDefault();
            if (first != null) { S.Ch.TargetIncident = first.Id; S.Ch.TargetCulprit = first.Culprit; }
            S.Dev($"DEATH {v.Id} by {by ?? "-"} cause {cause} room {S.RoomName(v.Room)} murder={murder}");
            Replay.Pin(sim, inc.SegmentStartTick);
            // loved ones grieve only when they learn of it (handled at confirmation)
        }

        public static void OnBodySeen(Simulation sim, Actor o, Actor body, Sighting s)
        {
            var S = sim.S; var k = S.K(o.Id);
            if (!body.Alive && k.KnownDead.Contains(body.Id)) return;
            var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == body.Id && i.Loop == S.Loop);
            // the culprit "notices" their own victim only after the act is wrapped up (costume off, a few minutes later, not mid-plan)
            if (inc != null && inc.Culprit == o.Id && (o.PlanId != null || o.Disguise != null || S.Clock - body.Body.DeathClock < 4)) return;
            string key = "seenbody:" + o.Id + ":" + body.Id; if (S.Flags.ContainsKey(key)) return; S.Flags[key] = S.Clock;
            bool isDead = !body.Alive;
            if (isDead) k.KnownDead.Add(body.Id); // they saw it; confirmation is separate
            S.Log("BodySeen", o.Id, body.Id, room: body.Room, pos: body.Pos, data: isDead ? "dead" : "down");
            if (inc != null && !inc.Discovered && isDead) { inc.Discovered = true; inc.DiscoverClock = S.Clock; inc.FoundRoom = body.Room; }
            if (inc != null && isDead && !inc.Discoverers.Contains(o.Id)) inc.Discoverers.Add(o.Id);
            // HOUSE RULE (user 2026-09-27): the death becomes official — bell, announcement, investigation — only once at least
            // three residents have seen the body with their own eyes. Until then the discoverers call others (Gather).
            if (inc != null && isDead && !inc.Confirmed && Enough(S, inc)) House.Call(sim, body.Room, o.IsPlayer ? 2.0 : 1.5);
            // evidence card: the body as first seen
            Evidences.BodySeenCard(sim, o, body, s);
            if (o.IsButler) return;   // the butler does not count as a witness
            if (o.IsPlayer)
            {
                S.Emit(GameEventType.Discovery, o.Id, body.Id, pos: body.Pos, text: isDead ? "body" : "injured"); sim.RaiseStop(StopKind.Discovery, StopClass.Critical, isDead ? "시신을 발견했다" : "쓰러진 사람을 발견했다", S.RoomName(body.Room), body.Id, body.Room); /* time-on-demand */
                if (isDead && inc != null && !inc.Confirmed && !Enough(S, inc)) { WitnessNotice(sim, inc, body.Room); sim.GatherForPlayer(inc, body.Room); /* time-on-demand: a zero-time look calls on the next tick */ }
                return;
            }
            // NPC reaction
            bool culprit = inc != null && inc.Culprit == o.Id;
            o.Needs.Fear = MathX.Clamp01(o.Needs.Fear + (culprit ? 0.1f : 0.6f)); o.Needs.Stress = MathX.Clamp01(o.Needs.Stress + 0.3f);
            if (culprit)
            {
                // a composed culprit may "discover" it themselves later (witness disguise); otherwise leaves quietly
                if (o.Def.Composure > 80 && S.R(Stream.PlanTie).Chance(0.35)) { sim.Speak(o, "scream_discover", null, loud: true); Report(sim, o, body.Room); }
                return;
            }
            sim.Speak(o, "scream_discover", null, loud: true);
            sim.Sound(SoundKind.Scream, o.Pos, 0.85f, o.Id, o.Id);
            if (!isDead && body.Body.Critical) { Rescue(sim, o, body); return; }
            Report(sim, o, body.Room);
        }

        static void Report(Simulation sim, Actor o, int room, bool keepActivity = false)
        {
            var S = sim.S;
            // the scream carries; the house answers with its bell a moment later (only for the dead: the injured need hands, not bells)
            S.Log("Report", o.Id, room: room, data: "scream");
            foreach (var inc in UnconfirmedIn(S, room)) { if (Enough(S, inc)) House.Call(sim, room); else Gather(sim, inc, room); }
            if (keepActivity) return;   // the reporter is already busy rescuing
            // stay near (shock) or back away
            var act = new Activity { Id = "case:wait", Label = "충격에 얼어붙어 대기", Priority = 40 };
            act.Steps.Add(Simulation.WaitStep(3 + S.R(Stream.Life).Range(0, 3)));
            sim.Assign(o, act);
        }

        public static void PlayerReport(Simulation sim)
        {
            var S = sim.S; var p = S.Player;
            sim.Sound(SoundKind.Bell, p.Pos, 0.6f, p.Id);
            S.Log("Report", p.Id, room: p.Room, data: "bell");
            foreach (var inc in UnconfirmedIn(S, p.Room)) { if (Enough(S, inc)) House.Call(sim, p.Room, 0.5); else { WitnessNotice(sim, inc, p.Room); Gather(sim, inc, p.Room, force: true); } }
        }

        // ------------------------------------------------------------------ the three-witness rule
        /// <summary>House rule: a death is announced and the investigation opens only after at least this many residents have seen
        /// the body (fewer only when fewer residents are alive). The butler never counts.</summary>
        public const int WitnessRule = 3;
        public static int WitnessesNeeded(GameState S) => Math.Max(1, Math.Min(WitnessRule, S.Actors.Values.Count(a => a.Alive && !a.IsButler)));
        public static int Witnesses(GameState S, Incident inc) => inc == null ? 0 : inc.Discoverers.Count(id => { var x = S.A(id); return x != null && !x.IsButler; });
        public static bool Enough(GameState S, Incident inc) => Witnesses(S, inc) >= WitnessesNeeded(S);

        static List<Incident> UnconfirmedIn(GameState S, int room) =>
            S.Incidents.Values.Where(i => i.Loop == S.Loop && !i.Confirmed && S.A(i.Victim) is Actor v && v.Status == ActorStatus.Dead && v.Room == room).OrderBy(i => i.Id).ToList();

        static void WitnessNotice(Simulation sim, Incident inc, int room)
        {
            var S = sim.S; int n = Witnesses(S, inc), need = WitnessesNeeded(S);
            string key = "witnessnote:" + inc.Id + ":" + n; if (S.Flags.ContainsKey(key)) return; S.Flags[key] = S.Clock;
            S.Emit(GameEventType.Notice, Cast.Player, inc.Victim, text: $"시신을 본 사람 {n}/{need}명 — {need}명 이상이 직접 봐야 발견 안내가 울리고 수사가 시작된다. 사람을 불러오자.", key: "witness");
        }

        /// <summary>Call people to the body: the nearest free residents who have not seen it yet come running (deterministic choice).</summary>
        public static void Gather(Simulation sim, Incident inc, int room, bool force = false)
        {
            var S = sim.S; if (inc == null || inc.Confirmed) return;
            string fk = "gather:" + inc.Id;
            if (!force && S.Flags.TryGetValue(fk, out var last) && S.Clock - last < 6) return;
            S.Flags[fk] = S.Clock;
            int missing = WitnessesNeeded(S) - Witnesses(S, inc); if (missing <= 0) return;
            var r = S.Layout.Room(room); if (r == null) return;
            var target = new P3(r.Floor, r.Rect.CX, r.Rect.CZ);
            var pick = S.LivingNpcs.Where(x => !x.IsButler && !inc.Discoverers.Contains(x.Id) && x.PlanId == null && x.Pose != Pose.Sleep && x.Status == ActorStatus.Active && (x.Act == null || x.Act.Interruptible))
                .OrderBy(x => x.Pos.f == r.Floor ? 0 : 1).ThenBy(x => x.Pos.Dist(target)).ThenBy(x => x.Id).Take(missing + 1).ToList();
            var rng = S.R(Stream.Life);
            foreach (var x in pick)
            {
                var act = new Activity { Id = "case:gather:" + inc.Id, Label = "비명을 듣고 달려가는 중", Priority = 60 };
                act.Steps.Add(new ActionStep { Kind = "GoTo", Target = sim.RandomPointIn(r, rng), HasTarget = true, Run = true });
                act.Steps.Add(Simulation.Do("explore", 1.0, Anim.Search));
                sim.Assign(x, act);
            }
            S.Log("Gather", null, inc.Victim, room: room, data: pick.Count.ToString());
        }

        public static void OnAttackSeen(Simulation sim, Actor o, Actor attacker, Sighting s)
        {
            var S = sim.S; if (o.IsPlayer) { S.Emit(GameEventType.Notice, o.Id, attacker.Id, text: "누군가 공격당하고 있다!", key: "attack"); sim.RaiseStop(StopKind.Attack, StopClass.Critical, "누군가 공격당하고 있다", S.RoomName(s.Room), attacker.Id, s.Room); sim.EmergencyTrigger(2, attacker.Id); /* time-on-demand */ return; }
            if (S.Flags.ContainsKey("sawattack:" + o.Id + ":" + attacker.Id)) return; S.Flags["sawattack:" + o.Id + ":" + attacker.Id] = S.Clock;
            S.K(o.Id).Facts.Add($"saw-attack:{s.Target}:{s.Victim}:{(int)S.Clock}");
            sim.Sound(SoundKind.Scream, o.Pos, 0.8f, o.Id, o.Id);
            // brave people intervene, others flee and call for help
            bool brave = o.Def.P.Aggression + o.Def.P.Loyalty > 1.0f && o.Body.Resistance > 0.6f && s.IdConf > 0.2f;
            var act = new Activity { Id = brave ? "case:intervene" : "case:flee", Label = brave ? "싸움 제지" : "도망쳐 도움 요청", Priority = 150, Interruptible = false };
            if (brave) { act.Steps.Add(new ActionStep { Kind = "GoTo", Target = attacker.Pos, HasTarget = true, Run = true }); act.Steps.Add(new ActionStep { Kind = "Say", Tag = "warn", Actor = attacker.Id }); }
            else { var hall = S.Layout.Rooms.First(r => r.Type == RoomType.GrandHall && r.Floor == 0); act.Steps.Add(new ActionStep { Kind = "GoTo", Target = sim.RandomPointIn(hall, S.R(Stream.Life)), HasTarget = true, Run = true }); }
            sim.Assign(o, act);
            Report(sim, o, s.Room);
        }

        public static void OnAlarmHeard(Simulation sim, Actor l, HeardSound h, string source)
        {
            var S = sim.S;
            if (l.PlanId != null || l.Act != null && !l.Act.Interruptible) return;
            string key = "alarm:" + l.Id + ":" + (int)(h.Clock / 3); if (S.Flags.ContainsKey(key)) return; S.Flags[key] = 1;
            var rng = S.R(Stream.Life);
            bool curious = l.Def.P.Curiosity + (1 - l.Def.P.Fearfulness) > 1.0f;
            if (!curious || l.Pose == Pose.Sleep && rng.Chance(0.5)) { l.Needs.Fear = MathX.Clamp01(l.Needs.Fear + 0.25f); return; }
            var room = S.Layout.Room(h.GuessRoom); if (room == null) return;
            var act = new Activity { Id = "case:check", Label = "소리 난 곳 확인", Priority = 25 };
            act.Steps.Add(new ActionStep { Kind = "GoTo", Target = sim.RandomPointIn(room, rng), HasTarget = true, Run = h.Kind == SoundKind.Scream });
            act.Steps.Add(Simulation.Do("explore", 1.5, Anim.Search));
            sim.Assign(l, act);
        }

        static void Rescue(Simulation sim, Actor o, Actor v)
        {
            var S = sim.S; var rng = S.R(Stream.Life);
            var act = new Activity { Id = "case:rescue:" + v.Id, Label = "응급처치", Priority = 120, Interruptible = false };
            bool hasKit = sim.Carried(o).Any(i => i.Def?.Tag == "rescue");
            var inf = S.Layout.First(RoomType.Infirmary);
            float infDist = inf != null ? v.Pos.Dist(new P3(inf.Floor, inf.Rect.CX, inf.Rect.CZ)) : 999f;
            // IG08: with no kit at hand and the infirmary close enough, carrying the person there is faster than fetching a kit
            bool carry = inf != null && v.Room != inf.Id && o.Body.Mobility > 0.6f && o.Body.HandL > 0.3f && o.Body.HandR > 0.3f && (!hasKit ? infDist < 70 : infDist < 25 && rng.Chance(0.4));
            if (carry)
            {
                act.Steps.Add(new ActionStep { Kind = "GoTo", Target = v.Pos, HasTarget = true, Run = true });
                act.Steps.Add(new ActionStep { Kind = "X_Grab", Actor = v.Id });
                act.Steps.Add(new ActionStep { Kind = "GoTo", Target = sim.RandomPointIn(inf, rng), HasTarget = true, Run = false });
                act.Steps.Add(new ActionStep { Kind = "X_Drop", Tag = "rescue" });
                act.Steps.Add(new ActionStep { Kind = "Aid", Actor = v.Id, Duration = 1.5 });
                sim.Assign(o, act);
                Report(sim, o, v.Room, keepActivity: true);
                return;
            }
            if (!hasKit)
            {
                // fetch a kit from where they last saw one, else the infirmary
                var k = S.K(o.Id);
                var kit = k.ItemSeen.Keys.Select(S.I).Where(i => i != null && i.Def?.Tag == "rescue" && i.Holder == null).OrderBy(i => i.Pos.Dist(o.Pos)).FirstOrDefault()
                          ?? S.Items.Values.Where(i => i.Def?.Tag == "rescue" && i.Holder == null && S.Layout.Room(i.Room)?.Type == RoomType.Infirmary).OrderBy(i => i.Pos.Dist(o.Pos)).FirstOrDefault();
                if (kit == null) { Report(sim, o, v.Room); return; }
                act.Steps.Add(new ActionStep { Kind = "GoTo", Target = kit.Pos, HasTarget = true, Run = true });
                act.Steps.Add(new ActionStep { Kind = "PickUp", Item = kit.Id });
            }
            act.Steps.Add(new ActionStep { Kind = "GoTo", Target = v.Pos, HasTarget = true, Run = true });
            act.Steps.Add(new ActionStep { Kind = "Aid", Actor = v.Id, Duration = 1.5 });
            sim.Assign(o, act);
            Report(sim, o, v.Room, keepActivity: true);
        }

        public static void OnRescued(Simulation sim, Actor v, Actor helper)
        {
            var S = sim.S;
            S.Log("Rescued", helper.Id, v.Id, room: v.Room);
            foreach (var r in S.Ch.Reservations.Where(r => r.Target == v.Id && !r.Consumed)) r.Released = true;
            // the attack happened; the victim knows who did it if they saw the face — a rescued victim is a witness
            var atk = v.Body.Wounds.LastOrDefault()?.By;
            if (atk != null && atk != v.Id)
            {
                bool saw = S.K(v.Id).Sightings.Any(s => s.Target == atk && s.IdConf > 0.5f && S.Clock - s.T1 < 20);
                if (saw) { S.K(v.Id).Facts.Add("attacked-by:" + atk); Relations.Change(S, v.Id, atk, fear: 0.6f, grudge: 0.6f, trust: -0.8f, memory: "나를 공격했다"); }
            }
            S.Emit(GameEventType.Notice, helper.Id, v.Id, text: Cast.NameOf(v.Id) + "의 출혈이 멈췄다", key: "rescued");
        }

        /// <summary>A survivor of an attack wakes up: if they saw the attacker, it becomes a public accusation (a real social event, not a trial).</summary>
        public static void OnVictimWake(Simulation sim, Actor v)
        {
            var S = sim.S; var w = v.Body.Wounds.LastOrDefault(x => x.By != null && x.By != v.Id); if (w == null) return;
            var atk = S.A(w.By); if (atk == null) return;
            bool saw = S.K(v.Id).Sightings.Any(s => s.Target == atk.Id && s.IdConf > 0.5f && S.Clock - s.T1 < 40);
            S.Log("AttackSurvived", v.Id, atk.Id, room: v.Room, data: saw ? "saw" : "unseen");
            foreach (var r in S.Ch.Reservations.Where(r => r.Target == v.Id && !r.Consumed)) r.Released = true;
            v.Needs.Fear = 1;
            if (v.IsPlayer) { S.Emit(GameEventType.Notice, v.Id, atk.Id, text: saw ? Cast.NameOf(atk.Id) + "에게 공격당했다" : "누군가에게 공격당했다", key: "attacked"); return; }
            if (!saw) { S.K(v.Id).Facts.Add("attacked-unknown:" + (int)S.Clock); return; }
            S.K(v.Id).Facts.Add("attacked-by:" + atk.Id);
            Relations.Change(S, v.Id, atk.Id, fear: 0.7f, grudge: 0.7f, trust: -1f, like: -0.8f, memory: "나를 공격했다", tag: "enemy");
            // go where people are and say it out loud
            var hall = S.Layout.Rooms.First(r => r.Type == RoomType.GrandHall && r.Floor == 0);
            var act = new Activity { Id = "case:accuse", Label = "도움 요청", Priority = 90, Interruptible = false };
            act.Steps.Add(new ActionStep { Kind = "GoTo", Target = sim.RandomPointIn(hall, S.R(Stream.Life)), HasTarget = true, Run = v.Body.Mobility > 0.5f });
            act.Steps.Add(new ActionStep { Kind = "Accuse", Actor = atk.Id });
            sim.Assign(v, act);
        }

        public static void PublicAccusation(Simulation sim, Actor v, string atkId)
        {
            var S = sim.S; var atk = S.A(atkId); if (atk == null) return;
            var p = new Prop { Kind = PropKind.Injured, A = v.Id, B = atkId, Value = "attacked", T0 = S.Clock, T1 = S.Clock };
            sim.Speak(v, "warn", null, new Dictionary<string, string> { { "t", "@" + atkId } }, p, false, true);
            foreach (var h in S.Actors.Values.Where(h => h.Alive && h != v && h != atk && h.Room == v.Room && h.Pose != Pose.Sleep))
            {
                float believe = MathX.Clamp01(0.4f + S.R(h.Id, v.Id).Trust - S.R(h.Id, atkId).Trust * 0.5f);
                var k = S.K(h.Id); k.Suspicion[atkId] = (k.Suspicion.TryGetValue(atkId, out var x) ? x : 0) + believe;
                Relations.Change(S, h.Id, atkId, fear: 0.3f * believe, trust: -0.4f * believe, like: -0.2f * believe, memory: "공격당했다는 " + Cast.GivenOf(v.Id) + "의 말을 들었다");
                if (believe > 0.6f) Relations.Change(S, h.Id, atkId, grudge: 0.15f);
            }
            atk.Needs.Stress = MathX.Clamp01(atk.Needs.Stress + 0.4f); atk.Needs.Fear = MathX.Clamp01(atk.Needs.Fear + 0.4f);
            S.K(atkId).Facts.Add("exposed-attack:" + v.Id);
            S.Log("PublicAccusation", v.Id, atkId, room: v.Room);
        }

        public static void ButlerConfirm(Simulation sim, Actor b, int room) => HouseConfirm(sim, room);

        /// <summary>The house tolls for the dead in this room: the death becomes public and the investigation opens.</summary>
        public static void HouseConfirm(Simulation sim, int room, bool force = false)
        {
            var S = sim.S;
            var bodies = S.Actors.Values.Where(x => x.Status == ActorStatus.Dead && x.Room == room && !S.Incidents.Values.Any(i => i.Victim == x.Id && i.Confirmed)).ToList();
            foreach (var body in bodies)
            {
                var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == body.Id && i.Loop == S.Loop);
                if (inc == null) continue;
                if (!force && !Enough(S, inc)) { Gather(sim, inc, room); continue; }   // the three-witness rule
                inc.Confirmed = true; inc.ConfirmClock = S.Clock; if (inc.FoundRoom < 0) inc.FoundRoom = room; if (!inc.Discovered) { inc.Discovered = true; inc.DiscoverClock = S.Clock; }
                S.Log("HouseToll", null, body.Id, room: room);
                sim.Announce("y_body", new Dictionary<string, string> { { "victim", Cast.NameOf(body.Id) }, { "place", S.RoomName(room) } });
                SetPieces.OnConfirm(sim, body, room);
                foreach (var a in S.Actors.Values.Where(a => a.Alive))
                {
                    S.K(a.Id).KnownDead.Add(body.Id); Tricks.OnDeathKnown(sim, a, body.Id);
                    float bond = S.HasRel(a.Id, body.Id) ? S.R(a.Id, body.Id).Attach + Math.Max(0, S.R(a.Id, body.Id).Like) : 0;
                    a.Needs.Grief = MathX.Clamp01(a.Needs.Grief + 0.1f + bond); a.Needs.Fear = MathX.Clamp01(a.Needs.Fear + 0.3f);
                    Evidences.OfficialFile(sim, a, inc);
                }
                StartOrExtend(sim);
            }
        }

        static void StartOrExtend(Simulation sim)
        {
            var S = sim.S;
            if (S.Phase == Phase.Daily)
            {
                // the case procedure preempts daily life: active chores stop, investigation begins
                sim.SetPhase(Phase.Investigation);
                S.Ch.FirstAnnounce = S.Clock; S.Ch.InvestigationEnd = S.Clock + 60;
                House.OpenNightRooms(sim, "수사 개방");
                Evidences.RecallRelevant(sim, Cast.Player);
                sim.Announce("y_invest_start", null);
                foreach (var a in S.LivingNpcs) { if (a.Act != null && a.Act.Interruptible) sim.Interrupt(a, S.R(Stream.Life).Range(0.2f, 2f)); }
                // unfinished plans before an attack stop; those mid-concealment continue in secret
                foreach (var p in S.Plans.Values.Where(p => p.Stage != "Done" && p.Stage != "Aborted" && p.Steps.Skip(p.Step).Any(x => x.Kind == "Attack" || x.Kind == "KnockOut" || x.Kind == "Drown")))
                    Crime.Abort(sim, S.A(p.Actor), p, "수사가 시작돼서");
            }
            else if (S.Phase == Phase.Investigation)
            {
                int maxExt = S.RuleActive("CH21") ? 2 : 1;
                if (S.Ch.Extensions < maxExt) { S.Ch.Extensions++; S.Ch.InvestigationEnd += 30; sim.Announce("y_invest_extend", null); }   // a real +30 min (was max(end, now+30): no effect when the 2nd body was found early)
            }
        }

        // ------------------------------------------------------------------ duties chosen by NPC brains (before life)
        public static Activity Think(Simulation sim, Actor a)
        {
            var S = sim.S; var rng = S.R(Stream.Life);
            if (S.Flags.TryGetValue("leave:" + a.Id, out var leave))
            {
                S.Flags.Remove("leave:" + a.Id);
                if (a.Room == (int)leave)
                {
                    var corr = S.Layout.Neighbors(a.Room).Select(S.Layout.Room).Where(r => r != null && RoomInfo.IsPassage(r.Type)).FirstOrDefault();
                    if (corr != null) { var act = new Activity { Id = "case:leave", Label = "퇴실", Priority = 30 }; act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(corr, rng))); return act; }
                }
            }
            // an accepted meeting invitation
            if (S.Flags.TryGetValue("meet:" + a.Id, out var mroom) && mroom >= 0 && S.Phase == Phase.Daily)
            {
                double at = S.Flags.TryGetValue("meetat:" + a.Id, out var t) ? t : S.Clock;
                if (S.Clock >= at - 3)
                {
                    S.Flags["meet:" + a.Id] = -1;
                    var r = S.Layout.Room((int)mroom);
                    var act = new Activity { Id = "life:meeting", Label = "약속 장소에서 대기", Priority = 20 };
                    act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(r, rng))); act.Steps.Add(Simulation.WaitStep(12));
                    return act;
                }
            }
            if (S.Phase == Phase.Investigation) return Investigate(sim, a, rng);
            if (S.Phase == Phase.Assembly) return Assemble(sim, a, rng);
            // somebody critically hurt that I know about?
            var k = S.K(a.Id);
            // a friend nobody has seen for hours: go look (knock on their door, ring for the butler if it stays shut)
            if (!S.IsNight && S.Minute > 9 * 60 && S.Phase == Phase.Daily)
            {
                foreach (var f in S.Living.Where(x => x != a && !x.IsPlayer && (S.R(a.Id, x.Id).Attach > 0.25f || S.R(a.Id, x.Id).Like > 0.35f || S.R(a.Id, x.Id).Tags.Contains("friend"))).Concat(S.Actors.Values.Where(x => x.Status == ActorStatus.Dead && !k.KnownDead.Contains(x.Id) && (S.R(a.Id, x.Id).Attach > 0.25f || S.R(a.Id, x.Id).Like > 0.35f))))
                {
                    if (k.KnownDead.Contains(f.Id)) continue;
                    bool longGone = !(k.LastSeen.TryGetValue(f.Id, out var ls) && S.Clock - ls.t < 6 * 60);
                    string key = $"search:{a.Id}:{f.Id}:{S.Day}";
                    if (!longGone || S.Flags.ContainsKey(key) || !S.R(Stream.Life).Chance(0.3)) continue;
                    S.Flags[key] = 1;
                    var bed = S.Layout.BedroomOf(f.Id); if (bed == null || bed.Doors.Count == 0) continue;
                    var d = S.Layout.Doors[bed.Doors[0]]; var outside = S.Layout.Room(d.RoomA == bed.Id ? d.RoomB : d.RoomA);
                    var act = new Activity { Id = "case:search:" + f.Id, Label = Cast.GivenOf(f.Id) + " 행방 확인", Priority = 8 };
                    act.Steps.Add(Simulation.GoTo(sim.SnapPublic(d.AlongX ? new P3(d.Pos.f, d.Pos.x, d.Pos.z + (outside.Rect.CZ > d.Pos.z ? 0.9f : -0.9f)) : new P3(d.Pos.f, d.Pos.x + (outside.Rect.CX > d.Pos.x ? 0.9f : -0.9f), d.Pos.z))));
                    act.Steps.Add(new ActionStep { Kind = "Door", Door = d.Id, Tag = "knock" });
                    act.Steps.Add(new ActionStep { Kind = "Worry", Actor = f.Id, Door = d.Id });
                    S.Log("Search", a.Id, f.Id, data: "missing");
                    return act;
                }
            }
            foreach (var x in S.Actors.Values)
            {
                if (x.Status != ActorStatus.Unconscious || !x.Body.Critical || x.Body.Stabilized) continue;
                if (!(k.Open != null && k.Open.TryGetValue(x.Id, out var s) && S.Clock - s.T1 < 1)) continue;
                if (S.Plans.Values.Any(p => p.Actor == a.Id && p.Target == x.Id)) continue;
                Rescue(sim, a, x); return a.Act;
            }
            return null;
        }

        static Activity Assemble(Simulation sim, Actor a, Rng rng)
        {
            var S = sim.S; var el = S.Layout.First(RoomType.Elevator); var hall = S.Layout.Rooms.First(r => r.Type == RoomType.GrandHall && r.Floor == 0);
            if (a.Room == hall.Id || a.Room == el.Id) { var w = new Activity { Id = "case:assemble-wait", Label = "심판 대기", Priority = 60 }; w.Steps.Add(Simulation.WaitStep(3)); return w; }
            var act = new Activity { Id = "case:assemble", Label = "심판장으로 이동", Priority = 60 };
            act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(hall, rng))); return act;
        }

        /// <summary>NPC investigation: examine scene and body, check weapon sources, question people. Each costs real time.</summary>
        static Activity Investigate(Simulation sim, Actor a, Rng rng)
        {
            var S = sim.S; var k = S.K(a.Id);
            if (a.Act != null && a.Act.Id != null && a.Act.Id.StartsWith("inv:")) return null;
            var incs = S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Confirmed).ToList();
            if (incs.Count == 0) return null;
            var act = new Activity { Id = "inv:" + S.Tick, Label = "조사", Priority = 10 };
            bool lazy = a.Def.P.Curiosity < 0.45f && rng.Chance(0.4);
            // 1) body — at most two people bend over it at once, each from their own side (one crouching, one on a knee taking
            // notes); anyone else nearby keeps a few steps back and reacts in character, and comes to look when a place frees up
            foreach (var inc in incs)
            {
                var body = S.A(inc.Victim);
                if (body == null || body.CarriedBy != null || k.Examined.Contains("body:" + inc.Victim) || lazy) continue;
                string tag = "inv:body:" + body.Id + ":";
                int examining = S.Actors.Values.Count(x => x != a && x.Alive && x.Act?.Id != null && x.Act.Id.StartsWith(tag));
                if (examining < 2)
                {
                    act.Id = tag + S.Tick; act.Label = "시신 조사";
                    act.Steps.Add(Simulation.GoTo(ExamineSpot(sim, body, a)));
                    act.Steps.Add(new ActionStep { Kind = "Face", Actor = body.Id });
                    act.Steps.Add(new ActionStep { Kind = "Examine", Tag = "body", Actor = body.Id, Duration = 2.5, Data = examining == 0 ? "crouch" : "kneel" });
                    return act;
                }
                if (a.Room == body.Room || a.Pos.Dist(body.Pos) < 10f)
                {
                    var ring = WatchSpot(sim, body, a);
                    if (ring.HasValue)
                    {
                        act.Id = "inv:watch:" + body.Id + ":" + S.Tick; act.Label = "멀찍이서 관망";
                        act.Steps.Add(Simulation.GoTo(ring.Value));
                        act.Steps.Add(new ActionStep { Kind = "Face", Actor = body.Id });
                        act.Steps.Add(Simulation.Do("watch_body", rng.Range(1.2f, 3f), WatchAnim(S, a, body)));
                        return act;
                    }
                }
            }
            // 2) traces in the scene rooms
            var scene = incs.Select(i => S.A(i.Victim).Room).Distinct().ToList();
            var trace = S.Traces.Where(t => scene.Contains(t.Room) && !k.Examined.Contains("trace:" + t.Id) && t.Visibility <= 2 && !t.Cleaned).OrderBy(t => t.Pos.Dist(a.Pos)).FirstOrDefault();
            if (trace != null && a.Def.Obs > 70 && rng.Chance(0.7))
            {
                act.Label = "현장 흔적 조사"; act.Steps.Add(Simulation.GoTo(Near(sim, trace.Pos))); act.Steps.Add(new ActionStep { Kind = "Examine", Tag = "trace", Data = trace.Id, Duration = 1.5 });
                return act;
            }
            // 3) question someone (alibis) — real walking and talking
            var q = S.Living.Where(x => x != a && !x.IsPlayer && x.TalkingTo == null && !k.Facts.Contains($"asked:{x.Id}:{S.Chapter}")).OrderBy(x => x.Pos.Dist(a.Pos) + rng.F() * 10).FirstOrDefault();
            if (q != null && rng.Chance(0.55 + a.Def.P.Sociability * 0.3))
            {
                k.Facts.Add($"asked:{q.Id}:{S.Chapter}");
                act.Label = Cast.GivenOf(q.Id) + "에게 질문"; act.Steps.Add(new ActionStep { Kind = "Interview", Actor = q.Id });
                return act;
            }
            // 4) weapon source rooms
            var src = S.Layout.Rooms.Where(r => (r.Type == RoomType.Kitchen || r.Type == RoomType.Workshop || r.Type == RoomType.Storage || r.Type == RoomType.PowerRoom || r.Type == RoomType.MachineRoom || r.Type == RoomType.Wardrobe) && !k.Examined.Contains("room:" + r.Id)).OrderBy(r => rng.F()).FirstOrDefault();
            if (src != null)
            {
                k.Examined.Add("room:" + src.Id);
                act.Label = S.RoomName(src.Id) + " 조사"; act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(src, rng))); act.Steps.Add(new ActionStep { Kind = "Examine", Tag = "room", Room = src.Id, Duration = 2 });
                return act;
            }
            // 5) think / share with a trusted person
            var friend = S.Living.Where(x => x != a && !x.IsPlayer && S.R(a.Id, x.Id).Trust > 0.25f).OrderBy(x => x.Pos.Dist(a.Pos)).FirstOrDefault();
            if (friend != null && rng.Chance(0.5)) { act.Label = "정보 공유"; act.Steps.Add(new ActionStep { Kind = "Share", Actor = friend.Id }); return act; }
            act.Label = "생각 정리"; act.Steps.Add(Simulation.Do("puzzle", 5, Anim.Think)); return act;
        }

        static P3 Near(Simulation sim, P3 p) => sim.SnapPublic(new P3(p.f, p.x + 0.7f, p.z + 0.4f));

        static int StableHash(string s) { int h = 17; foreach (var ch in s ?? "") h = h * 31 + ch; return h & 0x7fffffff; }

        /// <summary>Distance from p to the nearest other living person (or where they are walking to).</summary>
        static float Crowded(GameState S, P3 p, Actor self, Actor body)
        {
            float d = 99f;
            foreach (var o in S.Actors.Values)
            {
                if (o == self || o == body || !o.Alive || o.Pos.f != p.f) continue;
                float dd = o.Pos.DistXZ(p); var st = o.Act?.Cur; if (st != null && st.Kind == "GoTo" && st.Target.f == p.f) dd = Math.Min(dd, st.Target.DistXZ(p));
                if (dd < d) d = dd;
            }
            return d;
        }

        /// <summary>A free side of the body to kneel at (six places around it, inside its room, the least crowded).</summary>
        static P3 ExamineSpot(Simulation sim, Actor body, Actor a)
        {
            var S = sim.S; P3 best = Near(sim, body.Pos); float bestD = -1f;
            for (int i = 0; i < 6; i++)
            {
                double ang = body.Yaw * Math.PI / 180.0 + i * Math.PI / 3.0;
                var p = sim.SnapPublic(new P3(body.Pos.f, body.Pos.x + (float)Math.Cos(ang) * 0.95f, body.Pos.z + (float)Math.Sin(ang) * 0.95f));
                if (S.Layout.RoomAt(p) != body.Room) continue;
                float d = Crowded(S, p, a, body); if (d > bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>A place a few steps back from the body (2.2–3.4 m), spread around it so onlookers never bunch up; null if the room is too small.</summary>
        static P3? WatchSpot(Simulation sim, Actor body, Actor a)
        {
            var S = sim.S; int h = StableHash(a.Id);
            foreach (float r in new[] { 3.2f, 2.6f, 2.1f })
                for (int i = 0; i < 10; i++)
                {
                    double ang = (h % 360) * Math.PI / 180.0 + i * (Math.PI * 2 / 10);
                    var p = sim.SnapPublic(new P3(body.Pos.f, body.Pos.x + (float)Math.Cos(ang) * r, body.Pos.z + (float)Math.Sin(ang) * r));
                    if (S.Layout.RoomAt(p) != body.Room || p.DistXZ(body.Pos) < 1.8f) continue;
                    if (Crowded(S, p, a, body) >= 1.3f) return p;
                }
            return null;
        }

        /// <summary>How someone stands and looks at a body: grief if they were close, arms folded if hard, a hand to the
        /// chin if curious, hands together if frightened, otherwise a wary look around the room.</summary>
        static Anim WatchAnim(GameState S, Actor a, Actor body)
        {
            var p = a.Def.P; var rel = S.HasRel(a.Id, body.Id) ? S.R(a.Id, body.Id) : null;
            if (rel != null && rel.Like > 0.35f || a.Needs.Grief > 0.45f) return Anim.Cry;
            if (p.Fearfulness > 0.62f) return Anim.Pray;
            if (p.Aggression > 0.6f) return Anim.CrossArms;
            if (p.Curiosity > 0.6f) return Anim.Think;
            return (StableHash(a.Id) & 1) == 0 ? Anim.Search : Anim.CrossArms;
        }

        // ------------------------------------------------------------------ per-tick case logic
        public static void Update(Simulation sim)
        {
            var S = sim.S;
            if (S.Phase == Phase.Prologue) return;
            House.Tick(sim);
            // three-witness rule: a found but unannounced body keeps drawing people until enough have seen it
            if ((S.Phase == Phase.Daily || S.Phase == Phase.Investigation) && S.Tick % 30 == 0)
                foreach (var inc in S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Discovered && !i.Confirmed).OrderBy(i => i.Id).ToList())
                {
                    var v = S.A(inc.Victim); if (v == null || v.Status != ActorStatus.Dead || S.Clock - inc.DiscoverClock < 2) continue;
                    if (Enough(S, inc)) House.Call(sim, v.Room); else Gather(sim, inc, v.Room);
                }
            if (S.Phase == Phase.Investigation && S.Clock >= S.Ch.InvestigationEnd)
            {
                // uniform safety check before assembly: critical injured must be handled first
                bool hazard = S.Actors.Values.Any(x => x.Status == ActorStatus.Unconscious && x.Body.Critical && !x.Body.Stabilized);
                if (hazard && S.Clock < S.Ch.InvestigationEnd + 20) return;
                sim.SetPhase(Phase.Assembly); sim.Announce("y_trial_summon", null);
                S.Flags["assembly_start"] = S.Clock;
                foreach (var a in S.LivingNpcs) sim.Interrupt(a, S.R(Stream.Life).Range(0.1f, 1f));
            }
            if (S.Phase == Phase.Assembly)
            {
                var hall = S.Layout.Rooms.First(r => r.Type == RoomType.GrandHall && r.Floor == 0); var el = S.Layout.First(RoomType.Elevator);
                bool all = S.Living.All(x => x.Room == hall.Id || x.Room == el.Id || x.Status == ActorStatus.Unconscious);
                double started = S.Flags.TryGetValue("assembly_start", out var st) ? st : S.Clock;
                if (all || S.Clock - started > 15) TrialSystem.Begin(sim);
            }
            // NPC-specific investigation steps are executed through Crime.Exec fallthrough → handled here
        }

        /// <summary>Investigation step kinds used by NPCs (Examine / Interview / Share / Aid).</summary>
        public static bool Exec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S;
            switch (st.Kind)
            {
                case "Examine":
                    {
                        a.Speed = 0; a.Anim = Anim.Examine;
                        // the first one crouches at the body; the second goes down on one knee and takes notes if they are the observant kind
                        if (st.Tag == "body") { if (st.Data == "kneel") { a.Pose = Pose.Kneel; if (a.Def.Obs > 60) a.Anim = Anim.Write; } else a.Pose = Pose.Crouch; }
                        if (S.Clock < a.Act.StepEnd) return true;
                        if (st.Tag == "body") { var b = S.A(st.Actor); if (b != null) Evidences.ExamineBody(sim, a, b, true); }
                        else if (st.Tag == "trace") { var t = S.Traces.FirstOrDefault(x => x.Id == st.Data); if (t != null) Evidences.ExamineTrace(sim, a, t); }
                        else if (st.Tag == "room") Evidences.ExamineRoomQuick(sim, a, st.Room);
                        a.Pose = Pose.Stand; sim.NextStepPublic(a); return true;
                    }
                case "Interview":
                    {
                        var t = S.A(st.Actor); if (t == null || !t.Alive) { sim.NextStepPublic(a); return true; }
                        if (a.Pos.Dist(t.Pos) > 2.0f)
                        {
                            if (a.Act.Path == null || S.Tick % 15 == 0) { var pr = Pathfinder.Find(S.Layout, a.Pos, t.Pos, sim.DoorCostFor(a)); if (!pr.Ok) { sim.NextStepPublic(a); return true; } a.Act.Path = pr.Points; a.Act.PathDoors = pr.DoorAtPoint; a.Act.PathStairs = pr.StairAtPoint; a.Act.PathIdx = 1; }
                            sim.MoveAlongPublic(a); if (a.Act != null && S.Clock - a.Act.StepStart > 8) sim.NextStepPublic(a); return true;
                        }
                        a.Speed = 0;
                        Testimony.NpcInterview(sim, a, t);
                        sim.NextStepPublic(a); return true;
                    }
                case "Share":
                    {
                        var t = S.A(st.Actor); if (t == null || !t.Alive) { sim.NextStepPublic(a); return true; }
                        if (a.Pos.Dist(t.Pos) > 2.0f)
                        {
                            if (a.Act.Path == null || S.Tick % 15 == 0) { var pr = Pathfinder.Find(S.Layout, a.Pos, t.Pos, sim.DoorCostFor(a)); if (!pr.Ok) { sim.NextStepPublic(a); return true; } a.Act.Path = pr.Points; a.Act.PathDoors = pr.DoorAtPoint; a.Act.PathStairs = pr.StairAtPoint; a.Act.PathIdx = 1; }
                            sim.MoveAlongPublic(a); if (a.Act != null && S.Clock - a.Act.StepStart > 8) sim.NextStepPublic(a); return true;
                        }
                        Testimony.NpcShare(sim, a, t);
                        sim.NextStepPublic(a); return true;
                    }
                case "Worry":
                    {
                        var d = S.Layout.Doors[st.Door]; var f = S.A(st.Actor);
                        if (!d.Locked) { var bed = S.Layout.BedroomOf(st.Actor); a.Act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(bed, S.R(Stream.Life)))); a.Act.Steps.Add(Simulation.Do("explore", 1, Anim.Search)); sim.NextStepPublic(a); return true; }
                        // no answer from a locked room: if someone lies dead inside, the house lets the door fall open
                        if (f != null && f.Alive && f.Room == S.Layout.BedroomOf(f.Id)?.Id && f.Pose != Pose.Sleep) { sim.NextStepPublic(a); return true; }
                        S.Log("Report", a.Id, st.Actor, room: S.Layout.BedroomOf(st.Actor)?.Id ?? -1, data: "missing-knock");
                        if (f != null && f.Status == ActorStatus.Dead && f.Room == S.Layout.BedroomOf(f.Id)?.Id && !d.Sealed) { sim.SetDoor(null, d, true, false, "저택"); S.Log("HouseOpen", null, f.Id, room: f.Room); }
                        sim.NextStepPublic(a); return true;
                    }
                case "Accuse":
                    PublicAccusation(sim, a, st.Actor); sim.NextStepPublic(a); return true;
                case "Aid":
                    {
                        var v = S.A(st.Actor); if (v == null || !v.Alive) { sim.NextStepPublic(a); return true; }
                        a.Speed = 0; a.Pose = Pose.Kneel; a.Anim = Anim.FirstAid;
                        if (S.Clock < a.Act.StepEnd) return true;
                        sim.FirstAid(a, v); a.Pose = Pose.Stand; sim.NextStepPublic(a); return true;
                    }
            }
            return false;
        }
    }
}
