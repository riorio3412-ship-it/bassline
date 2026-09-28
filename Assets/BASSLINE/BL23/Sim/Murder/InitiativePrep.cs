using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    // =====================================================================================================================
    // PREPARATION — visible, deterministic daily-life acts that leave tells. Each is an Activity with motions (walk, reach,
    // search, write, operate, talk) and ends in "S_Prep", which does the deed (pick up and tuck away, stash, set the clock,
    // ask the favour, plant the rumour, send the invitations) and records a SchemeBeat with the people who really saw it
    // (their open sighting of the culprit at that moment). The victim may notice — and fear becomes their own counter-plan.
    // Favours: the unwitting helper's promised act at its hour. Cover: after the deed, being among the first to "find" it.
    // =====================================================================================================================
    public static partial class Initiative
    {
        // ------------------------------------------------------------------ beats (the tells) and who saw them
        static List<string> Observers(Simulation sim, Actor a)
        {
            var S = sim.S; var res = new List<string>();
            foreach (var o in S.Actors.Values)
            {
                if (o == a || !o.Alive || o.IsButler || o.Status != ActorStatus.Active || o.Pose == Pose.Sleep) continue;
                var k = S.K(o.Id); if (k.Open == null || !k.Open.TryGetValue(a.Id, out var s)) continue;
                if (S.Clock - s.T1 > 0.8 || s.IdConf < 0.45f) continue;
                res.Add(o.Id);
            }
            res.Sort(StringComparer.Ordinal);
            return res;
        }

        internal static SchemeBeat Beat(Simulation sim, Scheme sc, Actor a, string kind, string item, int room, string text, string innocent, string meaning)
        {
            var S = sim.S; var M = W(S);
            var b = new SchemeBeat { Id = "bt" + (M.NextId++), Scheme = sc.Id, Plan = sc.Plan, Actor = a.Id, Kind = kind, Beat = kind, Item = item, Room = room, Clock = S.Clock, Text = text, Innocent = innocent, Meaning = meaning, Loop = S.Loop, Chapter = S.Chapter };
            b.Observers.AddRange(Observers(sim, a));
            M.Beats.Add(b); sc.Beats.Add(b.Id);
            foreach (var o in b.Observers) if (o != Cast.Player) S.K(o).Facts.Add("beat:" + b.Id);
            S.Log("SchemeBeat", a.Id, sc.Victim, item: item, room: room, data: $"{b.Id} {kind} seen={b.Observers.Count}", secret: true);
            Noticed(sim, sc, b);
            if (M.Beats.Count > 600) M.Beats.RemoveRange(0, 100);
            return b;
        }

        // ------------------------------------------------------------------ the next preparation act
        static Activity PrepActivity(Simulation sim, Actor a, Scheme sc)
        {
            var S = sim.S;
            if (a.Act != null && a.Act.Id != null && a.Act.Id.StartsWith("scheme:" + sc.Id + ":")) return a.Act;
            if (a.Act != null && a.Act.Id != null && (a.Act.Id.StartsWith("errand:") || a.Act.Id.StartsWith("favour:") || a.Act.Id.StartsWith("case:"))) return null;
            if (S.Phase == Phase.Investigation && sc.Moment != "investigation") return null;
            if (S.Clock < sc.NextPrep) return null;
            double lead = sc.StrikeAt - S.Clock; int m = S.Minute;
            bool night = m >= 22 * 60 + 30 || m < 7 * 60;
            if (night && lead > 150) return null;                                                   // sleep first
            if (sim.MealTime(out int slot) && lead > 70 && a.Needs.Hunger > 0.2f && !S.Flags.ContainsKey($"ate:{a.Id}:{S.Day}:{slot}")) return null;   // eat at the bell (a meal is its own alibi)
            for (int i = 0; i < sc.Prep.Count; i++)
            {
                var t = sc.Prep[i];
                if (t.Done || t.Failed || t.Kind == "firstin" || S.Clock < t.NotBefore) continue;
                if (S.Clock > t.Due + 30 && !t.Essential) { t.Failed = true; continue; }
                var act = PrepAct(sim, a, sc, t, i);
                if (act == null)
                {
                    if (t.Done) continue;
                    t.Tries++; if (t.Tries >= 4) { t.Failed = true; S.Log("SchemePrepFail", a.Id, sc.Victim, data: $"{sc.Id} {t.Kind}", secret: true); }
                    sc.NextPrep = S.Clock + 6; return null;
                }
                sc.NextPrep = S.Clock + 1;
                return act;
            }
            return null;
        }

        static Activity PrepAct(Simulation sim, Actor a, Scheme sc, PrepTask t, int idx)
        {
            var S = sim.S; var rng = Local(S, sc.Id + ":pa:" + idx + ":" + t.Tries);
            var act = new Activity { Id = "scheme:" + sc.Id + ":" + idx, Label = CoverLabel(t.Kind, sc), Priority = 3.2, Interruptible = true, Secret = true };
            var done = new ActionStep { Kind = "S_Prep", Data = sc.Id, Tag = idx.ToString() };
            switch (t.Kind)
            {
                case "obtain": case "poison": case "garb": case "token": case "mark-get":
                    {
                        var it = S.I(t.Item);
                        if (it == null) return null;
                        if (it.Holder == a.Id) { Complete(sim, a, sc, t, null); return null; }
                        if (it.Holder != null)
                        {
                            if (t.Kind == "obtain") { var alt = BestWeapon(sim, sc, a, WeaponsKnown(sim, a).Where(x => x.Id != it.Id).ToList(), sc.Approach == "dark-strike" ? "quiet" : sc.Approach == "slip-out" ? "conceal" : "any"); if (alt != null) { t.Item = alt; sc.Weapon = alt; sc.WeaponType = S.I(alt)?.Type; S.Log("SchemeSwitchWeapon", a.Id, item: alt, data: sc.Id, secret: true); } }
                            return null;
                        }
                        var room = S.Layout.Room(it.Room); if (room != null && !sim.RoomUsable(a, room)) return null;
                        if (it.StashF >= 0) { var f = S.Layout.Furniture[it.StashF]; act.Steps.Add(Simulation.GoTo(sim.FrontOf(f))); act.Steps.Add(new ActionStep { Kind = "C_Retrieve", Item = it.Id }); }
                        else act.Steps.Add(Simulation.GoTo(sim.SnapPublic(it.Pos)));
                        act.Steps.Add(done); return act;
                    }
                case "stash":
                    {
                        var it = S.I(t.Item); var f = t.Furniture >= 0 && t.Furniture < S.Layout.Furniture.Count ? S.Layout.Furniture[t.Furniture] : null;
                        if (it == null || f == null) { t.Failed = true; return null; }
                        if (it.Holder != a.Id) return null;           // wait until it is in hand
                        act.Steps.Add(Simulation.GoTo(sim.FrontOf(f))); act.Steps.Add(new ActionStep { Kind = "C_Stash", Item = it.Id, Furniture = f.Id }); act.Steps.Add(done); return act;
                    }
                case "scout":
                    {
                        var r = S.Layout.Room(t.Room); if (r == null || !sim.RoomUsable(a, r)) return null;
                        act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(r, rng))); act.Steps.Add(Simulation.Do("explore", 3, Anim.Search));
                        act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(r, rng))); act.Steps.Add(Simulation.Do("explore", 2, Anim.Examine)); act.Steps.Add(done); return act;
                    }
                case "shadow":
                    {
                        var v = S.A(t.Target); if (v == null || !v.Alive || v.Room < 0) return null;
                        var r = S.Layout.Room(v.Room); if (r == null || !sim.RoomUsable(a, r) || r.Type == RoomType.Bedroom) return null;
                        act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(r, rng))); act.Steps.Add(Simulation.Do("observe", 10 + rng.R(0, 6), Anim.Idle)); act.Steps.Add(done); return act;
                    }
                case "rehearse":
                    {
                        var kr = S.Layout.Room(t.Room); int.TryParse(t.Note ?? "-1", out var er); var e = S.Layout.Room(er);
                        if (kr == null || !sim.RoomUsable(a, kr)) return null;
                        if (e != null && e.Id != kr.Id && sim.RoomUsable(a, e)) act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(e, rng)));
                        act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(kr, rng))); act.Steps.Add(Simulation.Do("explore", 1, Anim.Search));
                        if (e != null && e.Id != kr.Id && sim.RoomUsable(a, e)) act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(e, rng)));
                        act.Steps.Add(done); return act;
                    }
                case "witness": case "helper": case "appoint":
                    {
                        var x = S.A(t.Target); if (x == null || !x.Alive || x.Pose == Pose.Sleep || x.Status != ActorStatus.Active) return null;
                        if (t.Kind == "appoint" && S.IsNight) return null;
                        act.Steps.Add(new ActionStep { Kind = "S_Reach", Actor = x.Id }); act.Steps.Add(done); return act;
                    }
                case "note": case "summon":
                    {
                        var x = S.A(t.Target); var xb = x != null ? S.Layout.BedroomOf(x.Id) : null;
                        if (x == null || !x.Alive || xb == null || xb.Doors.Count == 0) { t.Failed = true; return null; }
                        var bed = S.Layout.BedroomOf(a.Id);
                        if (bed != null) { act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(bed, rng))); act.Steps.Add(Simulation.Do("write", 3, Anim.Write)); }
                        var d = S.Layout.Doors[xb.Doors[0]]; t.Furniture = d.Id;
                        act.Steps.Add(Simulation.GoTo(sim.SnapPublic(d.Pos))); act.Steps.Add(done); act.Label = "편지 전하기"; return act;
                    }
                case "rumor":
                    {
                        var l = S.LivingNpcs.Where(x => x != a && x.Id != sc.Victim && x.Id != sc.Scapegoat && x.Status == ActorStatus.Active && x.Pose != Pose.Sleep && S.HasRel(x.Id, a.Id) && S.R(x.Id, a.Id).Trust + S.R(x.Id, a.Id).Like > 0)
                                            .OrderBy(x => a.Pos.Dist(x.Pos)).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
                        if (l == null) return null;
                        t.Note = l.Id; act.Steps.Add(new ActionStep { Kind = "S_Reach", Actor = l.Id }); act.Steps.Add(done); return act;
                    }
                case "clock":
                    {
                        var f = t.Furniture >= 0 && t.Furniture < S.Layout.Furniture.Count ? S.Layout.Furniture[t.Furniture] : null; var r = f != null ? S.Layout.Room(f.Room) : null;
                        if (f == null || r == null || !sim.RoomUsable(a, r)) { t.Failed = true; return null; }
                        act.Steps.Add(Simulation.GoTo(sim.FrontOf(f))); act.Steps.Add(Simulation.Do("fix", 1.5, Anim.Operate)); act.Steps.Add(done); return act;
                    }
                case "dress":
                    {
                        var r = S.Layout.Room(t.Room); if (r == null || !sim.RoomUsable(a, r)) return null;
                        act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(r, rng))); act.Steps.Add(Simulation.Do("prepare", 4, Anim.Use)); act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(r, rng))); act.Steps.Add(Simulation.Do("prepare", 3, Anim.Use)); act.Steps.Add(done); return act;
                    }
                case "host":
                    {
                        var g = S.Gatherings.FirstOrDefault(x => x.Id == sc.EventId);
                        if (g == null || g.Cancelled) { t.Failed = true; return null; }
                        act.Label = "모임 초대"; act.Priority = 3.4;
                        // written invitations first (the player's comes under the door); then each guest in person, the victim first
                        var noteFor = g.Channel.Where(kv => kv.Value == "note" && (!g.KnownRev.TryGetValue(kv.Key, out var kr0) || kr0 < 0) && !S.Flags.ContainsKey($"note:{g.Id}:{kv.Key}")).Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal).ToList();
                        var bed = S.Layout.BedroomOf(a.Id);
                        if (noteFor.Count > 0 && bed != null) { act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(bed, rng))); act.Steps.Add(Simulation.Do("write", 3, Anim.Write)); }
                        foreach (var id in noteFor) { var nb = S.Layout.BedroomOf(id); if (nb == null || nb.Doors.Count == 0) continue; var d = S.Layout.Doors[nb.Doors[0]]; act.Steps.Add(Simulation.GoTo(sim.SnapPublic(d.Pos))); act.Steps.Add(new ActionStep { Kind = "X_DropNote", Actor = id, Data = g.Id, Door = d.Id }); S.Flags[$"note:{g.Id}:{id}"] = 1; }
                        var voice = g.Channel.Where(kv => kv.Value == "voice" && (!g.KnownRev.TryGetValue(kv.Key, out var kr1) || kr1 < 0)).Select(kv => kv.Key).OrderBy(x => x == sc.Victim ? 0 : 1).ThenBy(x => x, StringComparer.Ordinal).ToList();
                        foreach (var id in voice.Take(4)) { var x = S.A(id); if (x == null || !x.Alive || x.Pose == Pose.Sleep) continue; act.Steps.Add(new ActionStep { Kind = "Talk", Actor = id }); act.Steps.Add(new ActionStep { Kind = "X_TellInvite", Actor = id, Data = g.Id }); }
                        if (act.Steps.Count == 0) { Complete(sim, a, sc, t, null); return null; }
                        act.Steps.Add(done); return act;
                    }
            }
            t.Failed = true; return null;
        }

        static string CoverLabel(string kind, Scheme sc)
        {
            switch (kind)
            {
                case "obtain": case "poison": case "token": case "mark-get": case "garb": return "물건 챙기기";
                case "stash": return "정리"; case "scout": return "둘러보기"; case "shadow": return "산책"; case "rehearse": return "산책";
                case "witness": case "helper": case "rumor": case "appoint": return "잡담"; case "note": case "summon": return "편지 전하기"; case "clock": return "시계 보기"; case "dress": return (sc.EventLabel ?? "모임") + " 준비";
                case "host": return "모임 초대";
            }
            return "볼일";
        }

        // ------------------------------------------------------------------ the deed at the end of a preparation act
        static void PrepExec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S; var sc = SchemeById(S, st.Data); int idx = int.TryParse(st.Tag, out var ii) ? ii : -1;
            var t = sc != null && idx >= 0 && idx < sc.Prep.Count ? sc.Prep[idx] : null;
            if (sc == null || t == null || t.Done) { sim.NextStepPublic(a); return; }
            switch (t.Kind)
            {
                case "obtain": case "poison": case "garb": case "token": case "mark-get":
                    {
                        var it = S.I(t.Item);
                        if (it != null && it.Holder == null && it.StashF < 0 && it.Pos.DistXZ(a.Pos) <= 2.2f && it.Pos.f == a.Pos.f) sim.PickUp(a, it);
                        if (it == null || it.Holder != a.Id) { t.Tries++; sim.NextStepPublic(a); return; }
                        if (Concealment.SlotOf(S, it) == BodySlot.None && Concealment.BestSlot(S, a, it, out _) != BodySlot.None) Concealment.Conceal(sim, a, it);
                        a.Anim = Anim.PickUp;
                        Complete(sim, a, sc, t, it);
                        break;
                    }
                case "stash":
                    {
                        var it = S.I(t.Item);
                        if (it != null && it.StashF >= 0 && it.Holder == null) { sc.WeaponStashF = it.StashF; Complete(sim, a, sc, t, it); }
                        else t.Tries++;
                        break;
                    }
                case "witness":
                    {
                        var x = S.A(t.Target); var r = S.Layout.Room(t.Room);
                        if (x == null || r == null || a.Pos.Dist(x.Pos) > 3f) { t.Tries++; break; }
                        var slots = new Dictionary<string, string> { { "place", r.Name }, { "time", ClockFmt.Vague(sc.AlibiAt) } };
                        sim.Speak(a, "scheme_witness_ask", x.Id, slots);
                        var rx = S.R(x.Id, a.Id); bool yes = rx.Trust + rx.Like > 0.02f && x.Needs.Fear < 0.7f;
                        sim.Speak(x, yes ? "invite_yes" : "invite_no", a.Id);
                        if (yes)
                        {
                            S.Flags["meet:" + x.Id] = r.Id; S.Flags["meetat:" + x.Id] = sc.AlibiAt; S.Flags[$"meetwith:{x.Id}:{a.Id}"] = 1;
                            S.K(x.Id).Facts.Add($"invited:{a.Id}:{r.Id}:{(int)sc.AlibiAt}");
                            S.Log("Invite", a.Id, x.Id, room: r.Id, data: ClockFmt.HM(sc.AlibiAt) + " accepted alibi");
                            Complete(sim, a, sc, t, null);
                        }
                        else { t.Failed = true; sc.Alibi = "none"; sc.AlibiWitness = null; }
                        break;
                    }
                case "helper":
                    {
                        var x = S.A(t.Target); var ev = S.Layout.Room(t.Room);
                        if (x == null || ev == null || a.Pos.Dist(x.Pos) > 3f) { t.Tries++; break; }
                        var slots = new Dictionary<string, string> { { "time", ClockFmt.Vague(sc.MomentAt + 20) }, { "place", ev.Name }, { "act", sc.EventLabel ?? "모임" } };
                        sim.Speak(a, "scheme_helper_ask", x.Id, slots);
                        var rx = S.R(x.Id, a.Id); bool yes = rx.Trust + rx.Like > 0.02f;
                        sim.Speak(x, yes ? "invite_yes" : "invite_no", a.Id);
                        if (yes && ev.Circuit > 0)
                        {
                            double at = Math.Ceiling((sc.MomentAt + 18 + U(S, sc.Id + ":fa") * 8) / 2) * 2;
                            var f = new Favour { Id = "fv" + (W(S).NextId++), Scheme = sc.Id, Helper = x.Id, Kind = "lights", Room = ev.Id, Circuit = ev.Circuit, At = at, Until = at + 14 + Math.Floor(U(S, sc.Id + ":fu") * 8), Text = K($"{ClockFmt.Vague(at)}에 {ev.Name} 불을 잠깐 내려 달라는 부탁") };
                            W(S).Favours.Add(f);
                            sc.MomentAt = Math.Min(sc.MomentAt, at); sc.MomentEnd = Math.Max(sc.MomentEnd, f.Until + 5);
                            S.Log("FavourAsked", a.Id, x.Id, room: ev.Id, data: $"{f.Id} lights {ClockFmt.HM(at)}-{ClockFmt.HM(f.Until)}", secret: true);
                            Complete(sim, a, sc, t, null);
                        }
                        else { t.Failed = true; sc.DarkBy = "self"; }
                        break;
                    }
                case "rumor":
                    {
                        var l = S.A(t.Note);
                        if (l == null || a.Pos.Dist(l.Pos) > 3f || sc.Scapegoat == null) { t.Tries++; break; }
                        sim.Speak(a, "scheme_rumor", l.Id, new Dictionary<string, string> { { "t", "@" + sc.Scapegoat }, { "v", "@" + sc.Victim } });
                        var kl = S.K(l.Id); kl.Facts.Add($"rumor:{sc.Scapegoat}:{sc.Victim}:{a.Id}");
                        kl.Suspicion[sc.Scapegoat] = (kl.Suspicion.TryGetValue(sc.Scapegoat, out var s0) ? s0 : 0f) + 0.12f;
                        Foreshadow.Rumour(sim, "frames", sc.Scapegoat, sc.Victim, a.Id, a.Room);   // the house's own rumour mill carries it on (daily-life API)
                        Relations.Change(S, l.Id, sc.Scapegoat, trust: -0.04f, memory: K($"{Name(a.Id)}에게서 {Name(sc.Victim)}와(과) 얽힌 얘기를 들었다"));
                        Complete(sim, a, sc, t, null, l.Id);
                        break;
                    }
                case "appoint":
                    {
                        var x = S.A(t.Target); var r = S.Layout.Room(t.Room);
                        if (x == null || r == null || a.Pos.Dist(x.Pos) > 3f) { t.Tries++; break; }
                        sim.Speak(a, "scheme_appoint", x.Id, new Dictionary<string, string> { { "place", r.Name }, { "time", ClockFmt.Vague(sc.MomentAt) } });
                        var rx = S.R(x.Id, a.Id);
                        bool yes = !x.IsPlayer && rx.Trust + rx.Like > 0.0f && x.Needs.Fear < 0.72f && rx.Fear < 0.4f && !(S.K(x.Id).Suspicion.TryGetValue(a.Id, out var su) && su > 0.45f);
                        sim.Speak(x, yes ? "invite_yes" : "invite_no", a.Id);
                        if (yes) { Appoint(sim, x, r.Id, sc.MomentAt, a.Id); S.Log("Invite", a.Id, x.Id, room: r.Id, data: ClockFmt.HM(sc.MomentAt) + " accepted"); Complete(sim, a, sc, t, null); }
                        else { t.Failed = true; S.Log("Invite", a.Id, x.Id, room: r.Id, data: ClockFmt.HM(sc.MomentAt) + " refused"); }
                        break;
                    }
                case "note": case "summon":
                    {
                        var x = S.A(t.Target); var r = S.Layout.Room(t.Room); var d = t.Furniture >= 0 && t.Furniture < S.Layout.Doors.Count ? S.Layout.Doors[t.Furniture] : null;
                        if (x == null || r == null || d == null || a.Pos.DistXZ(d.Pos) > 3f) { t.Tries++; break; }
                        int meet = r.Id; double at = sc.MomentAt; string signer = t.Kind == "note" ? sc.Scapegoat : sc.Victim;
                        if (t.Kind == "summon")
                        {
                            meet = S.Layout.Neighbors(r.Id).Select(S.Layout.Room).Where(q => Plain(q) && sim.RoomUsable(x, q)).OrderBy(q => q.Id).Select(q => q.Id).DefaultIfEmpty(r.Id).First();
                            at = sc.MomentAt + 4;
                        }
                        var note = ForgedNote(sim, a, x, meet, at, signer, d);
                        if (note != null) { sc.Planted.Add(note.Id); Complete(sim, a, sc, t, note); } else t.Tries++;
                        break;
                    }
                case "clock":
                    {
                        var f = S.Layout.Furniture[t.Furniture];
                        S.ClockOffset[f.Id] = sc.ClockShift;
                        S.Log("ClockTamper", a.Id, room: f.Room, data: $"f{f.Id} {sc.ClockShift:0}min", secret: true);
                        Complete(sim, a, sc, t, null);
                        break;
                    }
                default: Complete(sim, a, sc, t, null); break;
            }
            sim.NextStepPublic(a);
        }

        /// <summary>A preparation act done: marked, spaced from the next one, and — if anyone saw — a tell (SchemeBeat).</summary>
        static void Complete(Simulation sim, Actor a, Scheme sc, PrepTask t, Item it, string with = null)
        {
            var S = sim.S; t.Done = true; t.DoneAt = S.Clock;
            double lead = sc.StrikeAt - S.Clock;
            sc.NextPrep = S.Clock + (lead > 240 ? 20 + Math.Floor(U(S, sc.Id + ":gap:" + t.Kind) * 30) : lead > 90 ? 8 + Math.Floor(U(S, sc.Id + ":gap2:" + t.Kind) * 10) : 2);
            string item = it?.Def?.Kor ?? it?.Kor; string room = S.RoomName(a.Room); string me = Name(a.Id);
            string text = null, inn = null, mean = null;
            switch (t.Kind)
            {
                case "obtain": text = $"{me}이(가) {room}에서 {item}을(를) 챙겼다"; inn = InnocentTake(it); mean = "흉기를 미리 손에 넣었다"; break;
                case "poison": text = $"{me}이(가) {room}에서 작은 병을 주머니에 넣었다"; inn = "약을 챙기는 줄 알았다"; mean = "독을 미리 손에 넣었다"; break;
                case "garb": text = $"{me}이(가) {item}을(를) 들고 갔다"; inn = "옷을 갈아입으려는 줄 알았다"; mean = "피가 튈 것에 대비한 겉옷"; break;
                case "token": text = $"{me}이(가) {room}에서 뭔가를 슬쩍 주웠다"; inn = "떨어진 걸 주워 주려는 줄 알았다"; mean = $"{Name(sc.Scapegoat)}의 물건을 현장에 남기려고 챙겼다"; break;
                case "mark-get": text = $"{me}이(가) {room}에서 {item}을(를) 골랐다"; inn = "누구에게 선물하려는 줄 알았다"; mean = "어둠 속 표식으로 쓸 선물"; break;
                case "stash": text = $"{me}이(가) {room}에서 가구 안쪽에 손을 넣었다"; inn = "뭘 정리하는 줄 알았다"; mean = "흉기를 범행 장소에 미리 숨겨 두었다"; break;
                case "scout": text = $"{me}이(가) 평소 잘 안 가던 {room}을(를) 둘러보고 있었다"; inn = "저택 구경을 하는 줄 알았다"; mean = "범행 장소를 미리 살폈다"; break;
                case "shadow": text = $"{me}이(가) {Name(sc.Victim)} 근처를 한참 서성였다"; inn = "말을 걸 타이밍을 보는 줄 알았다"; mean = $"{Name(sc.Victim)}의 습관을 익혔다"; LearnHabit(sim, a, sc); Recon(sim, a, sc); break;
                case "rehearse": text = $"{me}이(가) 같은 복도를 두 번 오갔다"; inn = "길을 헤매는 줄 알았다"; mean = "동선과 걸리는 시간을 재 보았다"; break;
                case "witness": text = $"{me}이(가) {Name(sc.AlibiWitness)}에게 {ClockFmt.Vague(sc.AlibiAt)}에 {S.RoomName(sc.AlibiRoom)}에서 보자고 했다"; inn = "그냥 약속인 줄 알았다"; mean = "범행 직후의 알리바이 증인을 심어 두었다"; break;
                case "clock": text = $"{me}이(가) {room}의 벽시계 앞에 한참 서 있었다"; inn = "시간을 확인하는 줄 알았다"; mean = $"시계를 {Math.Abs(sc.ClockShift):0}분 늦춰 놓았다"; break;
                case "helper": text = $"{me}이(가) {Name(sc.Helper)}에게 뭔가 부탁했다"; inn = "모임 준비를 부탁하는 줄 알았다"; mean = "모르는 조력자에게 불을 끄게 했다"; break;
                case "rumor": text = $"{me}이(가) {Name(with)}에게 {Name(sc.Scapegoat)} 얘기를 했다"; inn = "흔한 뒷말인 줄 알았다"; mean = $"{Name(sc.Scapegoat)}에게 의심이 가도록 미리 말을 흘렸다"; break;
                case "dress": text = $"{me}이(가) {room}에서 {sc.EventLabel ?? "모임"} 자리를 꾸몄다"; inn = "모임 준비인 줄 알았다"; mean = "범행 무대를 직접 꾸몄다"; break;
                case "host": text = $"{me}이(가) 사람들을 {sc.EventLabel ?? "모임"}에 초대하고 다녔다"; inn = "모처럼 즐거운 자리를 만드는 줄 알았다"; mean = "범행에 쓸 자리를 직접 열었다"; break;
                case "appoint": text = $"{me}이(가) {Name(sc.Victim)}에게 {ClockFmt.Vague(sc.MomentAt)}에 {S.RoomName(t.Room)}에서 보자고 했다"; inn = "둘이 할 얘기가 있는 줄 알았다"; mean = "범행 장소로 불러낼 약속"; break;
                case "note": text = $"{me}이(가) {Name(sc.Victim)}의 방문 앞에서 허리를 굽혔다"; inn = "떨어진 걸 줍는 줄 알았다"; mean = $"{Name(sc.Scapegoat)}의 이름으로 쓴 호출 쪽지를 밀어 넣었다"; break;
                case "summon": text = $"{me}이(가) {Name(sc.Scapegoat)}의 방문 앞에 잠깐 서 있었다"; inn = "노크하려다 그만둔 줄 알았다"; mean = $"{Name(sc.Victim)}의 이름으로 {Name(sc.Scapegoat)}을(를) 현장 근처로 불러냈다"; break;
            }
            sc.Log.Add(K($"{ClockFmt.DayHM(S.Clock)} 준비({t.Kind}): {(text ?? t.Kind)}."));
            // the daily-life side renders the tell (a cover bark in character, a watcher who finds it odd, talk): Foreshadow.Tick reads "Prep"
            var ck = CoverKind(t.Kind, it); if (ck != null) S.Log("Prep", a.Id, item: it?.Id, room: a.Room, data: ck + "|" + (it?.Type ?? ""), secret: true);
            if (text != null) t.Beat = Beat(sim, sc, a, t.Kind, it?.Id, a.Room, K(text), K(inn), K(mean)).Id;
            S.Log("SchemePrep", a.Id, sc.Victim, item: it?.Id, room: a.Room, data: $"{sc.Id} {t.Kind}", secret: true);
        }

        /// <summary>An appointment the invitee will keep (Cases.Think walks them there at the hour and they wait).</summary>
        static void Appoint(Simulation sim, Actor x, int room, double at, string with)
        {
            var S = sim.S;
            S.Flags["meet:" + x.Id] = room; S.Flags["meetat:" + x.Id] = at; S.Flags[$"meetwith:{x.Id}:{with}"] = 1;
            S.K(x.Id).Facts.Add($"invited:{with}:{room}:{(int)at}");
        }

        /// <summary>A note slipped under someone's door in another person's name ("밤 9시, 예배당에서 기다릴게 — 채령").
        /// The reader keeps the appointment; the note stays in their room (the forgery is a seam: the hand is not the signer's).</summary>
        static Item ForgedNote(Simulation sim, Actor a, Actor reader, int room, double at, string signer, Door d)
        {
            var S = sim.S; var r = S.Layout.Room(room); var rb = S.Layout.BedroomOf(reader.Id);
            if (r == null || signer == null || reader.IsPlayer) return null;
            var inside = rb != null && d != null ? sim.SnapPublic(new P3(d.Pos.f, d.Pos.x + (d.AlongX ? 0 : (rb.Rect.CX > d.Pos.x ? 0.6f : -0.6f)), d.Pos.z + (d.AlongX ? (rb.Rect.CZ > d.Pos.z ? 0.6f : -0.6f) : 0))) : reader.Pos;
            var note = new Item { Id = S.NewId("it"), Type = "Document", Name = "접힌 쪽지", Owner = reader.Id, Pos = inside, Room = rb?.Id ?? a.Room,
                Note = K($"{ClockFmt.Vague(at)}, {r.Name}에서 기다릴게. 둘이서만 할 얘기가 있어. — {Name(signer)}"), NoteFrom = "framed:" + a.Id + ":" + signer };
            S.Items[note.Id] = note; S.Emit(GameEventType.ItemMoved, a.Id, data: note.Id, text: "spawn", pos: note.Pos);
            Appoint(sim, reader, room, at, signer);
            S.Log("SchemeNote", a.Id, reader.Id, item: note.Id, room: room, data: $"signed {signer} @{ClockFmt.HM(at)}", secret: true);
            return note;
        }

        /// <summary>The daily-life cover vocabulary (Life/Foreshadow.cs: knife cord thread sedative poison saw tea plant trap recorder
        /// note fire noise swap key burn bury swim gathering alibi) for a preparation act; null = no cover bark for it.</summary>
        static string CoverKind(string prep, Item it)
        {
            var d = it?.Def;
            switch (prep)
            {
                case "obtain":
                    if (d == null) return null;
                    if (Methods.IsCord(d) || d.Dmg == DamageType.Choke) return "cord";
                    if (d.Dmg == DamageType.Stab || d.Dmg == DamageType.Cut) return "knife";
                    return "blunt";
                case "poison": return it?.Type == "Sedative" ? "sedative" : "poison";
                case "garb": case "token": return "swap";
                case "mark-get": return it?.Type == "Flower" ? "plant" : "swap";
                case "host": case "helper": case "dress": return "gathering";
                case "witness": case "appoint": return "alibi";
                case "clock": return "key";
                case "note": case "summon": return "note";
                case "stash": return d != null && (d.Dmg == DamageType.Stab || d.Dmg == DamageType.Cut) ? "knife" : "swap";
            }
            return null;
        }

        /// <summary>Shadowing: a question asked of someone nearby about the victim's day ("{t}는 보통 몇 시에 자요?") — the listener remembers who asked.</summary>
        static void Recon(Simulation sim, Actor a, Scheme sc)
        {
            var S = sim.S;
            var l = S.LivingNpcs.Where(x => x != a && x.Id != sc.Victim && x.Room == a.Room && x.Pose != Pose.Sleep && x.Status == ActorStatus.Active && x.Pos.Dist(a.Pos) < 8f)
                                .OrderBy(x => x.Pos.Dist(a.Pos)).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
            if (l == null) return;
            Foreshadow.Recon(sim, a, l, sc.Victim, "routine");
        }

        static string InnocentTake(Item it)
        {
            var t = it?.Type ?? "";
            if (t == "KitchenKnife" || t == "Cleaver" || t == "RollingPin" || t == "FryingPan" || t == "IcePick") return "요리를 하려는 줄 알았다";
            if (t == "Wrench" || t == "Hammer" || t == "Crowbar" || t == "Pliers" || t == "PipeSection" || t == "Chisel") return "뭘 고치려는 줄 알았다";
            if (t == "Scissors" || t == "Scarf" || t == "CurtainCord" || t == "PianoWire" || t == "Rope" || t == "ExtensionCord") return "손볼 게 있는 줄 알았다";
            if (t == "Candlestick" || t == "Decanter" || t == "Statuette" || t == "Bookend" || t == "Trophy") return "제자리로 옮기는 줄 알았다";
            return "필요한 게 있는 줄 알았다";
        }

        /// <summary>Shadowing the victim teaches their habit: where they spend this part of the day (the culprit's own sighting).</summary>
        static void LearnHabit(Simulation sim, Actor a, Scheme sc)
        {
            var S = sim.S; var v = S.A(sc.Victim); if (v == null) return;
            int m = S.Minute; int block = m < 12 * 60 ? 0 : m < 18 * 60 ? 1 : 2;
            S.K(a.Id).Facts.Add($"habit:{v.Id}:{block}:{v.Room}");
        }

        // ------------------------------------------------------------------ favours: the unwitting helper's act at its hour
        static Activity FavourActivity(Simulation sim, Actor a)
        {
            var S = sim.S; var M = W(S);
            foreach (var f in M.Favours)
            {
                if (f.Helper != a.Id || f.Done || f.Failed) continue;
                if (a.Act != null && a.Act.Id == "favour:" + f.Id) return a.Act;
                if (S.Clock < f.At - 14) continue;
                if (S.Clock > f.Until) { f.Failed = true; continue; }
                var pr = S.Layout.First(RoomType.PowerRoom); if (pr == null || !sim.RoomUsable(a, pr)) { f.Failed = true; continue; }
                var sb = pr.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(x => x.Type == "Switchboard");
                var spot = sb != null ? pr.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Furniture == sb.Id) : null;
                var act = new Activity { Id = "favour:" + f.Id, Label = "부탁받은 일", Priority = 35, Interruptible = false };
                act.Steps.Add(Simulation.GoTo(spot != null ? spot.Approach : sim.RandomPointIn(pr, Local(S, f.Id))));
                act.Steps.Add(new ActionStep { Kind = "S_Favour", Data = f.Id, Tag = "off" });
                act.Steps.Add(new ActionStep { Kind = "S_Favour", Data = f.Id, Tag = "on" });
                var back = S.Layout.Room(f.Room); if (back != null) act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(back, Local(S, f.Id + ":b"))));
                f.Started = true;
                return act;
            }
            return null;
        }

        static void FavourExec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S; var f = W(S).Favours.FirstOrDefault(x => x.Id == st.Data);
            if (f == null || f.Failed) { sim.NextStepPublic(a); return; }
            a.Speed = 0; a.Anim = Anim.Operate;
            if (st.Tag == "off")
            {
                bool cue = S.Flags.ContainsKey("cue:" + f.Scheme);
                if (!cue && S.Clock < f.At + 25) return;   // wait by the board for the sign (the hand bell) — or, when it never comes, do as promised a little late
                double dur = Math.Max(10, f.Until - f.At); f.At = S.Clock; f.Until = S.Clock + dur;
                if (S.CircuitOn(f.Circuit)) { sim.SetCircuit(f.Circuit, false, a.Id); S.Log("FavourDone", a.Id, data: $"{f.Id} lights off", secret: true); S.K(a.Id).Facts.Add($"favour:{f.Id}:{f.Scheme}"); }
                sim.NextStepPublic(a); return;
            }
            if (S.Clock < f.Until) return;    // back on after the promised while
            if (!S.CircuitOn(f.Circuit)) sim.SetCircuit(f.Circuit, true, a.Id);
            f.Done = true; f.DoneAt = S.Clock;
            sim.NextStepPublic(a);
        }

        // ------------------------------------------------------------------ cover: among the first to "find" it (y2: the three who see)
        static Activity CoverActivity(Simulation sim, Actor a, Scheme sc)
        {
            var S = sim.S;
            var t = sc.Prep.FirstOrDefault(x => x.Kind == "firstin" && !x.Done && !x.Failed); if (t == null) return null;
            if (a.Act != null && a.Act.Id == "scheme:" + sc.Id + ":cover") return a.Act;
            var v = S.A(sc.Victim); var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == sc.Victim && i.Loop == S.Loop);
            if (v == null || inc == null || inc.Discovered || v.CarriedBy != null) { t.Failed = inc == null || !inc.Discoverers.Contains(a.Id); t.Done = !t.Failed; return null; }
            if (S.Clock < sc.Killed + t.NotBefore) return null;
            if (S.IsNight && S.Minute >= 23 * 60) { t.Failed = true; return null; }
            var room = S.Layout.Room(v.Room); if (room == null || !sim.RoomUsable(a, room)) { t.Failed = true; return null; }
            var comp = S.Living.Where(x => x != a && !x.IsPlayer && x.Status == ActorStatus.Active && x.Pose != Pose.Sleep && x.PlanId == null && (x.Act == null || x.Act.Interruptible) && x.Pos.f == a.Pos.f && x.Pos.Dist(a.Pos) < 14f)
                                .OrderBy(x => x.Pos.Dist(a.Pos)).ThenBy(x => x.Id, StringComparer.Ordinal).Take(2).ToList();
            var act = new Activity { Id = "scheme:" + sc.Id + ":cover", Label = Name(sc.Victim) + " 찾기", Priority = 20, Interruptible = false };
            if (comp.Count > 0) { act.Steps.Add(new ActionStep { Kind = "S_Reach", Actor = comp[0].Id }); act.Steps.Add(new ActionStep { Kind = "S_Cover", Data = sc.Id, Tag = "gather" }); }
            act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(room, Local(S, sc.Id + ":cv"))));
            act.Steps.Add(new ActionStep { Kind = "S_Cover", Data = sc.Id, Tag = "found" });
            return act;
        }

        static void CoverExec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S; var sc = SchemeById(S, st.Data); if (sc == null) { sim.NextStepPublic(a); return; }
            var v = S.A(sc.Victim); var room = v != null ? S.Layout.Room(v.Room) : null;
            if (st.Tag == "gather" && room != null)
            {
                sim.Speak(a, "scheme_search", null, new Dictionary<string, string> { { "t", "@" + sc.Victim } }, loud: true);
                foreach (var x in S.Living.Where(x => x != a && !x.IsPlayer && x.Status == ActorStatus.Active && x.Pose != Pose.Sleep && x.PlanId == null && x.Pos.f == a.Pos.f && x.Pos.Dist(a.Pos) < 9f && (x.Act == null || x.Act.Interruptible)).OrderBy(x => x.Pos.Dist(a.Pos)).ThenBy(x => x.Id, StringComparer.Ordinal).Take(2).ToList())
                {
                    var go = new Activity { Id = "case:search:" + sc.Victim, Label = Name(sc.Victim) + " 찾기", Priority = 18, Interruptible = true };
                    go.Steps.Add(Simulation.GoTo(sim.RandomPointIn(room, Local(S, sc.Id + ":g:" + x.Id))));
                    go.Steps.Add(Simulation.Do("explore", 2, Anim.Search));
                    sim.Assign(x, go);
                    S.Log("Search", x.Id, sc.Victim, data: "asked by " + a.Id);
                }
                Beat(sim, sc, a, "firstin", null, a.Room, K($"{Name(a.Id)}이(가) {Name(sc.Victim)}이(가) 안 보인다며 사람들을 데리고 찾으러 나섰다"), "걱정돼서 그러는 줄 알았다", "스스로 시신 발견자 무리에 끼었다 (세 사람 발견 규칙)");
            }
            if (st.Tag == "found") { var t = sc.Prep.FirstOrDefault(x => x.Kind == "firstin" && !x.Done); if (t != null) { t.Done = true; t.DoneAt = S.Clock; } }
            sim.NextStepPublic(a);
        }

        // ------------------------------------------------------------------ the house distracted: switch to the investigation
        static void InvestigationSwitch(Simulation sim, Scheme sc)
        {
            var S = sim.S; string key = "invsw:" + sc.Id + ":" + S.Chapter + ":" + S.Loop;
            if (S.Flags.ContainsKey(key)) return; S.Flags[key] = 1;
            if (W(S).Schemes.Any(x => x != sc && x.Open && x.Moment == "investigation" && x.Loop == S.Loop && x.Chapter == S.Chapter)) return;   // one killer at a time moves in the confusion
            var a = S.A(sc.Culprit); var st = SchemeStyles.Of(a.Def); var c = a.Def;
            bool firstNotMine = S.Incidents.Values.Any(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Murder && i.Confirmed && i.Culprit != a.Id);
            double bold = (1 - c.P.Fearfulness) * 0.45 + c.P.Pride * 0.25 + c.P.Aggression * 0.2 + st.Bold;
            double p = 0.1 + bold * 0.35 + (firstNotMine && c.Infer >= 60 ? 0.25 : 0) + (st.Style == "Impulsive" ? 0.15 : 0);
            if (U(S, key) >= p || S.Ch.InvestigationEnd - S.Clock < 25) return;
            ClearDesign(sc);
            if (!Design(sim, sc)) { sc.State = "Designing"; sc.NextCheck = S.Clock + 30; return; }
            sc.State = "Preparing"; sc.Designed = S.Clock; sc.Var("during-investigation");
            if (firstNotMine) { sc.Var("second-killer"); sc.Shield("y6", "second-killer"); }
            sc.Log.Add(K($"{ClockFmt.DayHM(S.Clock)} 시신 발견으로 저택이 어수선해지자, 수사 도중에 움직이기로 했다" + (firstNotMine ? " — 심판은 첫 사건만 가린다." : ".")));
            S.Log("SchemeSwitch", a.Id, sc.Victim, data: sc.Id + " investigation", secret: true);
        }
    }
}
