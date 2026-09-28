using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Autonomous crime: perceived pressure → non-violent alternatives → intent → plan (grammar) → preparation → approach →
    /// opportunity recheck → execution/abort → result → escape/concealment/alibi. Plans only use the planner's own knowledge.
    /// </summary>
    public static class Crime
    {
        // ------------------------------------------------------------------ periodic evaluation
        public static void Update(Simulation sim)
        {
            var S = sim.S;
            if (S.Tick % 50 != 0) return; // every 5 sim seconds
            Initiative.Update(sim);   // --- initiative (Sim/Murder/Initiative*.cs): the proactive culprit minds (daily life and the investigation)
            if (S.Phase != Phase.Daily) return;
            // pacing: every chapter opens with a stretch of ordinary life (the very first one longer) before anyone can form lethal intent
            double calm = S.Loop == 1 && S.Chapter == 1 ? 30 * 60 : 20 * 60;
            if (S.Clock - S.Ch.ChapterStartClock < calm && !S.Plans.Values.Any(p => p.Stage != "Done" && p.Stage != "Aborted")) return;
            foreach (var a in S.LivingNpcs.ToList())
            {
                if (a.Status != ActorStatus.Active) continue;
                double next = S.Flags.TryGetValue("motive:" + a.Id, out var t) ? t : 0;
                if (S.Clock < next) continue;
                S.Flags["motive:" + a.Id] = S.Clock + 25 + S.R(Stream.PlanTie).Range(0, 20);
                if (a.PlanId != null) { Review(sim, a); continue; }
                Evaluate(sim, a);
            }
        }

        static int ActivePlans(GameState S) => S.Plans.Values.Count(p => p.Stage != "Done" && p.Stage != "Aborted");

        static void Evaluate(Simulation sim, Actor a)
        {
            var S = sim.S; var rng = S.R(Stream.PlanTie);
            var (p, target, motive) = Relations.Pressure(sim, a);
            if (target == null) return;
            var k = S.K(a.Id);
            S.Dev($"motive {a.Id} p={p:0.00} target={target} {motive}");
            if (p < 0.1f) return;
            // non-violent alternatives first (they are real actions with real outcomes)
            string altKey = $"alt:{a.Id}:{target}:{S.Chapter}";
            int tried = S.Flags.TryGetValue(altKey, out var tv) ? (int)tv : 0;
            // did the last attempt fail to relieve anything? (event-driven frustration)
            if (tried > 0 && S.Flags.TryGetValue("altp:" + a.Id, out var prevP) && p >= prevP - 0.02) { S.Flags["altfail:" + a.Id] = (S.Flags.TryGetValue("altfail:" + a.Id, out var f0) ? f0 : 0) + 1; }
            S.Flags["altp:" + a.Id] = p;
            if (p < 0.35f || (motive != "wish" && tried < 1))
            {
                if (tried < 4 && rng.Chance(0.6))
                {
                    S.Flags[altKey] = tried + 1;
                    var alt = Alternative(sim, a, target, motive, rng);
                    if (alt != null) { sim.Assign(a, alt); S.Log("AltAction", a.Id, target, data: alt.Id + " " + motive, secret: true); }
                }
                return;
            }
            if (S.Ch.Reservations.Count(r => !r.Released && !r.Consumed) + DeadThisChapter(S) >= S.Ch.VictimCap) return; // budget full — no new lethal intent
            if (ActivePlans(S) >= 2) return;
            if (S.Survivors <= S.FloorLocked) return; // the floor is reached; nothing left to gain
            if (Initiative.TakeOver(sim, a, target, motive, p)) return;   // --- initiative: a mind that prepares takes the intent over (the moment, the preparation, the frame)
            var plan = BuildPlan(sim, a, target, motive, p, rng);
            if (plan == null) { S.Dev($"plan none {a.Id}->{target}"); return; }
            S.Plans[plan.Id] = plan; a.PlanId = plan.Id;
            S.Log("PlanFormed", a.Id, target, plan: plan.Id, data: $"{plan.Grammar} motive={motive} p={p:0.00} alts=[{string.Join(",", plan.Alternatives)}]", secret: true);
            S.Dev($"PLAN {a.Id} -> {target} {plan.Grammar} ({motive}) weapon={plan.WeaponType}");
            Replay.MarkPlanStart(sim, plan);
        }

        static int DeadThisChapter(GameState S) => S.Incidents.Values.Count(i => i.Chapter == S.Chapter && i.Loop == S.Loop && !i.ProcedureClosed);

        static Activity Alternative(Simulation sim, Actor a, string target, string motive, Rng rng)
        {
            var S = sim.S; var t = S.A(target);
            var act = new Activity { Id = "alt:" + target, Label = "긴한 얘기", Priority = 1.5 };
            // confront (talk), ask a trusted person for help, or keep distance — personality decides
            var c = a.Def;
            if (c.P.Aggression + c.P.Pride > 1.0f || motive == "grudge") { act.Steps.Add(new ActionStep { Kind = "Talk", Actor = target }); S.Flags[$"topic:{a.Id}:{target}"] = 1; return act; }
            var friend = S.Living.Where(x => x != a && x.Id != target && !x.IsPlayer).OrderByDescending(x => S.R(a.Id, x.Id).Trust).FirstOrDefault();
            if (friend != null && S.R(a.Id, friend.Id).Trust > 0.25f && c.P.Sociability > 0.4f) { act.Steps.Add(new ActionStep { Kind = "Talk", Actor = friend.Id }); act.Label = "고민 상담"; return act; }
            var bed = S.Layout.BedroomOf(a.Id); if (bed == null) return null;
            act.Label = "거리 두기"; act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(bed, rng))); act.Steps.Add(Simulation.Do("rest", 30, Anim.Think));
            return act;
        }

        // ------------------------------------------------------------------ plan grammar
        sealed class Option { public string Grammar; public double Score; public Func<MurderPlan, bool> Fill; }

        static MurderPlan BuildPlan(Simulation sim, Actor a, string target, string motive, float p, Rng rng)
        {
            var S = sim.S; var k = S.K(a.Id); var c = a.Def; var t = S.A(target);
            var plan = new MurderPlan { Id = S.NewId("plan"), Actor = a.Id, Target = target, Motive = motive, Formed = S.Clock, Deadline = S.Clock + 60 * 20, StartTick = S.Tick, Reason = $"p={p:0.00}" };
            // weapon knowledge: what the planner has seen, not what exists
            var weapons = KnownWeapons(sim, a).ToList();
            bool hasWeapon = weapons.Count > 0;
            bool knowsPower = k.Facts.Any(f => f.StartsWith("visited:") && S.Layout.Room(int.Parse(f.Substring(8)))?.Type == RoomType.PowerRoom);
            bool knowsMachine = k.Facts.Any(f => f.StartsWith("visited:") && S.Layout.Room(int.Parse(f.Substring(8)))?.Type == RoomType.MachineRoom);
            bool knowsPool = k.Facts.Any(f => f.StartsWith("visited:") && S.Layout.Room(int.Parse(f.Substring(8)))?.Type == RoomType.Pool);
            bool knowsDisguise = k.ItemSeen.Keys.Any(id => S.I(id)?.Def?.Tag == "disguise");
            float trustTA = S.R(target, a.Id).Trust + S.R(target, a.Id).Like;
            var trap = Tricks.ChooseTrap(sim, a, target);
            var gath = hasWeapon ? Tricks.GatheringFor(sim, a, target) : null;
            var opts = new List<Option>
            {
                new Option { Grammar = "Ambush", Score = hasWeapon ? 1.0 : 0, Fill = pl => true },
                new Option { Grammar = "Lure", Score = hasWeapon && trustTA > 0.05f ? 0.8 + c.Deceit / 150.0 + c.Argue / 200.0 : 0 },
                new Option { Grammar = "NightVisit", Score = hasWeapon ? 0.6 + (trustTA > 0.2f ? 0.4 : 0) : 0 },
                new Option { Grammar = "Blackout", Score = hasWeapon && knowsPower && c.Infer >= 70 ? 0.7 + c.Infer / 200.0 : 0 },
                new Option { Grammar = "Press", Score = knowsMachine && c.Infer >= 80 && c.Composure >= 75 && weapons.Any(w => ItemCatalog.Get(S.I(w.id).Type).Dmg == DamageType.Blunt) ? 0.9 + c.Infer / 150.0 : 0 },
                new Option { Grammar = "Drown", Score = SetPieces.Force == "Drown" && !t.IsPlayer ? 9 : knowsPool && trustTA > 0.05f && !t.IsPlayer ? 0.5 + (t.Def.P.Sociability) * 0.2 + Methods.Aff(a.Id, "Drown") + (c.HeightCm >= t.Def.HeightCm ? 0.2 : 0) : 0 },
                new Option { Grammar = "Disguise", Score = hasWeapon && knowsDisguise && c.Deceit >= 70 ? 0.8 + c.Deceit / 200.0 : 0 },
                new Option { Grammar = "Trap", Score = trap != null ? trap.Score : 0 },
                new Option { Grammar = "Gathering", Score = gath != null ? 0.95 + c.Deceit / 200.0 : 0 },
                new Option { Grammar = "Poison", Score = SetPieces.Force == "Poison" && SetPieces.PoisonSource(sim, a) != null ? 9 : SetPieces.PoisonSource(sim, a) != null && c.Infer >= 60 && c.Deceit >= 50 ? 0.8 + c.Deceit / 250.0 + (c.P.Aggression < 0.4f ? 0.25 : 0) : 0 },
            };
            // second wave (Methods.cs): a cord from behind, a push at an edge, a rigged machine, a draught and a pillow, a bedtime poison
            foreach (var (g, sc) in Methods.Options(sim, a, t, trustTA, rng)) opts.Add(new Option { Grammar = g, Score = sc });
            foreach (var o in opts) if (o.Score > 0) o.Score += rng.F() * 0.35;
            // variety: grammars already used this loop are less attractive (people also learn what gets caught)
            foreach (var o in opts) if (o.Score > 0) o.Score -= 0.22 * S.Plans.Values.Count(pl => pl.Grammar != null && pl.Grammar.StartsWith(o.Grammar) && pl.Stage != "Aborted");
            // …and after a death by blade or bludgeon this loop, the plain weapon attacks lose their appeal (the house is on guard for that)
            { int armed = S.Incidents.Values.Count(i => i.Loop == S.Loop && (i.Dmg == DamageType.Blunt || i.Dmg == DamageType.Stab || i.Dmg == DamageType.Cut));
              foreach (var o in opts) if (o.Score > 0 && armed > 0 && (o.Grammar == "Ambush" || o.Grammar == "Lure" || o.Grammar == "NightVisit" || o.Grammar == "Blackout" || o.Grammar == "Disguise" || o.Grammar == "Gathering")) o.Score -= 0.15 * armed; }
            foreach (var o in opts) if (o.Score > 0 && (o.Grammar == "Trap" || o.Grammar == "Gathering") && c.Infer + c.Composure > 150) o.Score += 0.3;
            // test hook (headless only): a forced room/concealment layer needs a base grammar it can ride on
            if (SetPieces.Force == "Noise") foreach (var o in opts) if (o.Grammar == "Lure" && o.Score > 0) o.Score += 5;
            if (SetPieces.Force == "Burn" || SetPieces.Force == "Dump" || SetPieces.Force == "Bury" || SetPieces.Force == "ColdHide")
                foreach (var o in opts) if (o.Score > 0 && (o.Grammar == "Ambush" || o.Grammar == "Lure" || o.Grammar == "NightVisit" || o.Grammar == "Strangle" || o.Grammar == "Blackout")) o.Score += 5;
            var ranked = opts.Where(o => o.Score > 0).OrderByDescending(o => o.Score).ToList();
            if (ranked.Count == 0)
            {
                // no known weapon yet: first step of any plan is to find one (goes looking in plausible rooms)
                plan.Grammar = "Ambush"; plan.Steps.Add(new PlanStep { Kind = "FindWeapon" });
            }
            else plan.Grammar = ranked[0].Grammar;
            plan.Alternatives = ranked.Skip(1).Select(o => o.Grammar).ToList();
            // weapon choice by grammar
            // weapon choice: harm, distance, concealability — and the culprit's own trade (a cook reaches for a knife, a laborer for a crowbar)
            var wpref = new Dictionary<string, double>();
            foreach (var w in weapons.OrderBy(w => w.id, StringComparer.Ordinal)) if (!wpref.ContainsKey(w.id)) wpref[w.id] = Methods.WeaponPref(a, S.I(w.id).Type, rng);
            var wchoice = plan.Grammar == "Press" ? weapons.Where(w => ItemCatalog.Get(S.I(w.id).Type).Dmg == DamageType.Blunt).OrderBy(w => w.dist).FirstOrDefault()
                        : weapons.Where(w => !Methods.IsCord(ItemCatalog.Get(S.I(w.id).Type)) || c.P.Aggression < 0.5f).OrderByDescending(w => ItemCatalog.Get(S.I(w.id).Type).Sev * 2 - w.dist / 20.0 + (ItemCatalog.Get(S.I(w.id).Type).Size <= 0.35f ? 1 : 0) + wpref[w.id]).ThenBy(w => w.id, StringComparer.Ordinal).FirstOrDefault();
            if (wchoice.id != null) { plan.Weapon = wchoice.id; plan.WeaponType = S.I(wchoice.id).Type; }
            // preparation
            if (plan.Grammar == "Trap" || plan.Grammar == "Poison" || plan.Grammar == "Drown" || Methods.NoWeapon(plan.Grammar)) { plan.Weapon = null; plan.WeaponType = null; }
            if (plan.Weapon != null && S.I(plan.Weapon).Holder != a.Id) plan.Steps.Add(new PlanStep { Kind = "GetWeapon", Item = plan.Weapon });
            if (plan.Grammar == "Disguise")
            {
                var dis = k.ItemSeen.Keys.Select(S.I).Where(i => i != null && i.Def?.Tag == "disguise" && i.Holder == null).OrderBy(i => i.Type == "TheaterMask" ? 0 : 1).FirstOrDefault();
                if (dis != null) { plan.Disguise = dis.Id; plan.Steps.Add(new PlanStep { Kind = "GetItem", Item = dis.Id }); } else plan.Grammar = "Ambush";
            }
            switch (plan.Grammar)
            {
                case "Lure":
                    {
                        var room = SecludedRoom(sim, a, rng); plan.KillRoom = room;
                        plan.Steps.Add(new PlanStep { Kind = "Invite", Target = target, Room = room });
                        plan.Steps.Add(new PlanStep { Kind = "GoRoom", Room = room });
                        plan.Steps.Add(new PlanStep { Kind = "WaitVictim", Target = target, Room = room });
                        plan.Steps.Add(new PlanStep { Kind = "Attack", Target = target });
                        break;
                    }
                case "NightVisit":
                    plan.Steps.Add(new PlanStep { Kind = "WaitNight" });
                    plan.Steps.Add(new PlanStep { Kind = "VisitRoom", Target = target });
                    plan.Steps.Add(new PlanStep { Kind = "Attack", Target = target });
                    break;
                case "Blackout":
                    {
                        var pr = S.Layout.First(RoomType.PowerRoom); plan.AlibiRoom = -1;
                        plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = target, Note = "alone-dark" });
                        plan.Steps.Add(new PlanStep { Kind = "CutPower", Room = pr?.Id ?? -1, Target = target });
                        plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = target, Note = "reach" });
                        plan.Steps.Add(new PlanStep { Kind = "Attack", Target = target });
                        plan.Steps.Add(new PlanStep { Kind = "RestorePower", Room = pr?.Id ?? -1 });
                        break;
                    }
                case "Press":
                    {
                        var mr = S.Layout.First(RoomType.MachineRoom); plan.KillRoom = mr.Id;
                        if (trustTA > 0.1f) plan.Steps.Add(new PlanStep { Kind = "Invite", Target = target, Room = mr.Id });
                        plan.Steps.Add(new PlanStep { Kind = trustTA > 0.1f ? "GoRoom" : "Stalk", Room = mr.Id, Target = target, Note = "alone" });
                        if (trustTA > 0.1f) plan.Steps.Add(new PlanStep { Kind = "WaitVictim", Target = target, Room = mr.Id });
                        plan.Steps.Add(new PlanStep { Kind = "KnockOut", Target = target });
                        plan.Steps.Add(new PlanStep { Kind = "CarryTo", Target = target, Room = mr.Id, Note = "pressbed" });
                        plan.Steps.Add(new PlanStep { Kind = "ArmPress", Target = target, Room = mr.Id });
                        break;
                    }
                case "Drown":
                    {
                        var pool = S.Layout.First(RoomType.Pool); plan.KillRoom = pool.Id;
                        plan.Steps.Add(new PlanStep { Kind = "Invite", Target = target, Room = pool.Id });
                        plan.Steps.Add(new PlanStep { Kind = "GoRoom", Room = pool.Id });
                        plan.Steps.Add(new PlanStep { Kind = "WaitVictim", Target = target, Room = pool.Id });
                        plan.Steps.Add(new PlanStep { Kind = "Drown", Target = target });
                        break;
                    }
                case "Trap": Tricks.FillTrap(sim, plan, a, trap); break;
                case "Poison": SetPieces.FillPoison(sim, plan, a); break;
                case "Gathering": Tricks.FillGathering(sim, plan, a, gath); break;
                case "Strangle": case "Push": case "Shock": case "Smother": case "Bedtime": Methods.Fill(sim, plan, a, rng); break;
                case "Shoot": Violence.Fill(sim, plan, a, rng); break;   // --- violence track: gun / crossbow → load → alone → fire
                default:
                    plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = target, Note = "alone" });
                    if (plan.Grammar == "Disguise") plan.Steps.Add(new PlanStep { Kind = "PutOn", Item = plan.Disguise });
                    // a cord picked up for an ordinary attack is still used the way a cord is used: from behind
                    plan.Steps.Add(new PlanStep { Kind = "Attack", Target = target, Note = Methods.IsCord(ItemCatalog.Get(plan.WeaponType)) ? "strangle" : null });
                    break;
            }
            // concealment chosen by the planner's traits (each is a real action that can leave traces or fail)
            bool careful = c.Infer + c.Composure > 150;
            if (plan.Grammar == "Disguise") plan.Steps.Add(new PlanStep { Kind = "TakeOff", Room = -1 });
            bool quick = plan.Grammar == "Gathering" || plan.Grammar == "Trap" || plan.Grammar == "Poison" || Methods.Remote(plan.Grammar);
            bool staged = Methods.Staged(plan.Grammar);   // an "accident" or a "natural death" must be left as it fell
            if (careful && !quick && !staged && plan.Grammar != "Press" && rng.Chance(0.35 + (c.Infer - 70) / 100.0)) plan.Steps.Add(new PlanStep { Kind = "LockRoom", Target = target });
            if (careful && !quick && !staged && plan.Grammar != "Press" && plan.Grammar != "Drown" && rng.Chance(0.25)) plan.Steps.Add(new PlanStep { Kind = "MoveBody", Target = target });
            if (plan.Grammar == "Gathering")
            {
                // hide the weapon and tidy up on the way back — the gathering itself is the alibi
                int back = plan.Steps.FindIndex(s => s.Kind == "X_Rejoin");
                plan.Steps.Insert(back, new PlanStep { Kind = "CleanUp" }); plan.Steps.Insert(back, new PlanStep { Kind = "HideWeapon" });
            }
            else
            {
                plan.Steps.Add(new PlanStep { Kind = plan.Grammar == "Trap" || rng.Chance(0.5) ? "HideWeapon" : "WashWeapon" });
                plan.Steps.Add(new PlanStep { Kind = "CleanUp" });   // change bloody clothes if needed
                plan.Steps.Add(new PlanStep { Kind = "Alibi" });
            }
            Tricks.Augment(sim, plan, a, rng);
            SetPieces.Augment(sim, plan, a, rng);
            Methods.Augment(sim, plan, a, rng);
            plan.Log.Add($"{ClockFmt.DayHM(S.Clock)} 계획: {plan.Grammar} 대상 {Cast.NameOf(target)} ({motive})");
            return plan;
        }

        public static IEnumerable<(string id, double dist)> KnownWeapons(Simulation sim, Actor a)
        {
            var S = sim.S; var k = S.K(a.Id);
            foreach (var it in sim.Carried(a)) if (it.Def?.IsWeapon == true && it.Def.Sev >= 2 && !Violence.IsRanged(it.Def)) yield return (it.Id, 0);   // (violence track: guns are chosen only by the "Shoot" plan)
            foreach (var kv in k.ItemSeen)
            {
                var it = S.I(kv.Key); if (it == null || it.Def == null || !it.Def.IsWeapon || it.Def.Sev < 2 || Violence.IsRanged(it.Def)) continue;
                var r = S.Layout.Room(kv.Value.room); if (r == null || !sim.RoomUsable(a, r)) continue;
                yield return (it.Id, a.Pos.Dist(new P3(r.Floor, r.Rect.CX, r.Rect.CZ)));
            }
        }

        static int SecludedRoom(Simulation sim, Actor a, Rng rng)
        {
            var S = sim.S; var k = S.K(a.Id);
            var known = S.Layout.Rooms.Where(r => k.Facts.Contains("visited:" + r.Id) && !RoomInfo.IsPassage(r.Type) && r.Type != RoomType.Bedroom && r.Type != RoomType.Elevator && r.Type != RoomType.ButlerRoom && !(RoomInfo.NightLocked(r.Type)) && r.Type != RoomType.Dining && r.Type != RoomType.Lounge).ToList();
            if (known.Count == 0) return S.Layout.Rooms.First(r => r.Type == RoomType.Storage || r.Type == RoomType.Closet).Id;
            // prefer rooms the planner rarely saw people in
            return rng.Weighted(known, r => 1.0 / (1 + k.Sightings.Count(s => s.Room == r.Id && S.Clock - s.T1 < 600)) * (r.Floor == -1 || RoomInfo.IsMystery(r.Type) || r.Type == RoomType.Closet || r.Type == RoomType.WineCellar || r.Type == RoomType.Darkroom ? 2 : 1)).Id;
        }

        // ------------------------------------------------------------------ plan execution as activities
        public static Activity Think(Simulation sim, Actor a)
        {
            var S = sim.S;
            if (a.PlanId == null) { return PostCrime(sim, a) ?? Initiative.Think(sim, a); }   // --- initiative: preparation, favours, cover (Sim/Murder)
            if (!S.Plans.TryGetValue(a.PlanId, out var plan)) { a.PlanId = null; return null; }
            if (S.Phase != Phase.Daily && (plan.Stage == "Forming" || plan.Stage == "Preparing") && !Initiative.PhaseOk(S, plan)) { Abort(sim, a, plan, "phase"); return null; }   // --- initiative: PhaseOk = a plan made for the investigation
            var t = S.A(plan.Target);
            if (plan.Step >= plan.Steps.Count) { Finish(sim, a, plan); return null; }
            var st = plan.Steps[plan.Step];
            bool beforeAttack = plan.Steps.Skip(plan.Step).Any(x => Lethal(x.Kind));
            if (st.Kind.StartsWith("S_")) return Initiative.PlanThink(sim, a, plan, st, beforeAttack);   // --- initiative: scheme steps (errands, the dark strike, the plant, the return)
            if (st.Kind.StartsWith("X_"))
            {
                bool fired = (st.Kind == "X_WaitTrap" || st.Kind == "X_WaitBedtime") && S.Traps.Any(x => x.Plan == plan.Id && x.FiredAt >= 0);
                if (beforeAttack && !fired && (t == null || !t.Alive)) { Abort(sim, a, plan, "target gone"); return null; }
                if (beforeAttack && !fired && (S.Clock > plan.Deadline || S.Phase != Phase.Daily && !Initiative.PhaseOk(S, plan))) { Abort(sim, a, plan, S.Phase != Phase.Daily ? "case opened" : "deadline"); return null; }
                if (a.Act != null && a.Act.Id == "murder:" + plan.Id + ":" + plan.Step) return a.Act;
                var xact = new Activity { Id = "murder:" + plan.Id + ":" + plan.Step, Label = "볼일", Priority = 30, Secret = true, Interruptible = st.Kind == "X_WaitGathering" };
                return Tricks.PlanThink(sim, a, plan, st, xact);
            }
            if (beforeAttack && (t == null || !t.Alive)) { Abort(sim, a, plan, "target gone"); return null; }
            if (beforeAttack && S.Clock > plan.Deadline) { Abort(sim, a, plan, "deadline"); return null; }
            if (beforeAttack && S.Phase != Phase.Daily && !Initiative.PhaseOk(S, plan)) { Abort(sim, a, plan, "case opened"); return null; }   // --- initiative: PhaseOk
            // life needs still matter while scheming (they won't starve waiting)
            if (beforeAttack && st.Kind != "Attack" && st.Kind != "KnockOut" && st.Kind != "Drown" && (a.Needs.Energy < (Initiative.OwnsConscience(S, plan) ? 0.03f : 0.12f))) return null;   // --- initiative: a prepared moment is not missed for tiredness
            if (a.Act != null && a.Act.Id == "murder:" + plan.Id + ":" + plan.Step) return a.Act;
            var act = new Activity { Id = "murder:" + plan.Id + ":" + plan.Step, Label = CoverLabel(st), Priority = 30, Secret = true, Interruptible = st.Kind == "Stalk" || st.Kind == "WaitNight" || st.Kind == "WaitVictim" || st.Kind == "Alibi" };
            var rng = S.R(Stream.PlanTie);
            switch (st.Kind)
            {
                case "FindWeapon":
                    {
                        var rooms = S.Layout.Rooms.Where(r => r.Type == RoomType.Kitchen || r.Type == RoomType.Workshop || r.Type == RoomType.Storage || r.Type == RoomType.Gallery || r.Type == RoomType.Lounge).Where(r => sim.RoomUsable(a, r)).ToList();
                        if (rooms.Count == 0) { Abort(sim, a, plan, "no weapon source"); return null; }
                        var r0 = rooms[rng.R(rooms.Count)];
                        act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(r0, rng))); act.Steps.Add(Simulation.Do("explore", 2, Anim.Search));
                        act.Steps.Add(new ActionStep { Kind = "PlanCheckWeapon" });
                        return act;
                    }
                case "GetWeapon":
                case "GetItem":
                    {
                        var it = S.I(st.Item);
                        if (it == null || (it.Holder != null && it.Holder != a.Id)) { Replan(sim, a, plan, "item taken"); return null; }
                        if (it.Holder == a.Id) { Advance(sim, a, plan); return null; }
                        act.Steps.Add(Simulation.GoTo(sim.SnapPublic(it.Pos))); act.Steps.Add(new ActionStep { Kind = "PlanPick", Item = it.Id });
                        return act;
                    }
                case "Invite":
                    if (t != null && (t.Pose == Pose.Sleep || S.IsNight)) return null;   // nobody knocks on a sleeper's door with an invitation: tomorrow
                    act.Steps.Add(new ActionStep { Kind = "PlanInvite", Actor = st.Target, Room = st.Room });
                    return act;
                case "GoRoom":
                    { var r = S.Layout.Room(st.Room); act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(r, rng))); act.Steps.Add(new ActionStep { Kind = "PlanAdvance" }); return act; }
                case "WaitVictim":
                    act.Steps.Add(new ActionStep { Kind = "PlanWaitVictim", Actor = st.Target, Room = st.Room, Duration = 0 }); act.Interruptible = false; return act;
                case "WaitNight":
                    if (S.Minute >= 60 && S.Minute < 5 * 60) { Advance(sim, a, plan); return null; }
                    return null; // go on living until the small hours
                case "VisitRoom":
                    {
                        var bed = S.Layout.BedroomOf(st.Target); if (bed == null || bed.Doors.Count == 0) { Replan(sim, a, plan, "no room"); return null; }
                        var door = S.Layout.Doors[bed.Doors[0]];
                        var outside = S.Layout.Room(door.RoomA == bed.Id ? door.RoomB : door.RoomA);
                        act.Steps.Add(Simulation.GoTo(Near(sim, door, outside)));
                        act.Steps.Add(new ActionStep { Kind = "PlanVisit", Door = door.Id, Actor = st.Target });
                        act.Interruptible = false; return act;
                    }
                case "Stalk":
                    act.Steps.Add(new ActionStep { Kind = "PlanStalk", Actor = st.Target, Tag = st.Note, Room = st.Room }); return act;
                case "CutPower":
                case "RestorePower":
                    {
                        var r = S.Layout.Room(st.Room); var sb = r?.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(f => f.Type == "Switchboard");
                        if (r == null || sb == null) { Advance(sim, a, plan); return null; }
                        var sp = r.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Furniture == sb.Id);
                        act.Steps.Add(Simulation.GoTo(sp != null ? sp.Approach : sim.RandomPointIn(r, rng), st.Kind == "RestorePower"));
                        act.Steps.Add(new ActionStep { Kind = st.Kind == "CutPower" ? "PlanCut" : "PlanRestore", Actor = st.Target });
                        act.Interruptible = false; return act;
                    }
                case "PutOn": act.Steps.Add(new ActionStep { Kind = "PlanPutOn", Item = st.Item }); return act;
                case "TakeOff": act.Steps.Add(new ActionStep { Kind = "PlanTakeOff" }); act.Interruptible = false; return act;
                case "Attack":
                case "KnockOut":
                    act.Steps.Add(new ActionStep { Kind = "Attack", Actor = st.Target, Tag = st.Kind == "KnockOut" ? "ko" : Methods.IsMode(st.Note) ? st.Note : "kill", Run = st.Note != "smother" });
                    act.Interruptible = false; act.Priority = 100; return act;
                case "Drown":
                    act.Steps.Add(new ActionStep { Kind = "Attack", Actor = st.Target, Tag = "drown", Run = true }); act.Interruptible = false; act.Priority = 100; return act;
                case "CarryTo":
                    {
                        var r = S.Layout.Room(st.Room);
                        var spot = r.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Tag == "pressbed");
                        act.Steps.Add(new ActionStep { Kind = "PlanGrab", Actor = st.Target });
                        act.Steps.Add(Simulation.GoTo(spot != null ? spot.Pos : sim.RandomPointIn(r, rng)));
                        act.Steps.Add(new ActionStep { Kind = "PlanRelease", Tag = st.Note });
                        act.Interruptible = false; return act;
                    }
                case "ArmPress":
                    {
                        var r = S.Layout.Room(st.Room); var con = r.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Tag == "operate");
                        act.Steps.Add(Simulation.GoTo(con != null ? con.Approach : sim.RandomPointIn(r, rng)));
                        act.Steps.Add(new ActionStep { Kind = "PlanArm", Actor = st.Target });
                        act.Interruptible = false; return act;
                    }
                case "LockRoom":
                    act.Steps.Add(new ActionStep { Kind = "PlanLockRoom", Actor = st.Target }); act.Interruptible = false; return act;
                case "MoveBody":
                    {
                        int dump = st.Note == "cold" ? Methods.ColdDump(sim, a, t) : st.Note == "work" && st.Room >= 0 ? st.Room : -1;   // the cold store: found late, reads as an earlier death; "work": the room with the saw
                        if (dump < 0) dump = DumpRoom(sim, a, t, rng); if (dump < 0) { Advance(sim, a, plan); return null; }
                        plan.DumpRoom = dump; var dr = S.Layout.Room(dump);
                        act.Steps.Add(Simulation.GoTo(t.Pos)); act.Steps.Add(new ActionStep { Kind = "PlanGrab", Actor = st.Target });
                        act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(dr, rng))); act.Steps.Add(new ActionStep { Kind = "PlanRelease", Tag = st.Note == "work" ? "work" : "dump" });
                        act.Interruptible = false; return act;
                    }
                case "WashWeapon":
                case "HideWeapon":
                    {
                        var w = S.I(plan.Weapon);
                        if (w == null || w.Holder != a.Id) { Advance(sim, a, plan); return null; }
                        // --- concealment (Systems/Concealment.cs): the careful kind tucks it out of sight before walking off with it
                        if (Concealment.WouldTuck(sim, a, w)) act.Steps.Add(new ActionStep { Kind = "C_Tuck", Item = w.Id });
                        if (st.Kind == "WashWeapon")
                        {
                            var sinkRoom = NearestRoom(sim, a, RoomType.Kitchen, RoomType.Laundry, RoomType.WaterRoom);
                            if (sinkRoom != null && sim.RoomUsable(a, sinkRoom))
                            {
                                var sp = sinkRoom.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Tag == "wash" || s.Tag == "laundry" || s.Tag == "operate");
                                act.Steps.Add(Simulation.GoTo(sp != null ? sp.Approach : sim.RandomPointIn(sinkRoom, rng)));
                                act.Steps.Add(new ActionStep { Kind = "PlanWash" });
                                act.Steps.Add(new ActionStep { Kind = "PlanReturnWeapon" });
                                return act;
                            }
                        }
                        var hide = HideRoom(sim, a, rng);
                        // --- concealment: into a real hiding place of that room when it has one (a drawer, under a cushion, the soil) —
                        // the same act as the player's; the random point is still drawn (the plan's dice stay in step)
                        var hp = sim.RandomPointIn(hide, rng); var hf = Concealment.PlaceIn(S, hide, w, hp);
                        act.Steps.Add(Simulation.GoTo(hf != null ? sim.FrontOf(hf) : hp)); act.Steps.Add(new ActionStep { Kind = "PlanHideWeapon", Furniture = hf?.Id ?? -1 });
                        return act;
                    }
                case "CleanUp":
                    {
                        if (a.BloodOnClothes < 0.2f) { Advance(sim, a, plan); return null; }
                        var bed = S.Layout.BedroomOf(a.Id);
                        act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(bed, rng))); act.Steps.Add(new ActionStep { Kind = "PlanChange" });
                        return act;
                    }
                case "Alibi":
                    {
                        var busy = S.Layout.Rooms.Where(r => sim.RoomUsable(a, r) && (r.Type == RoomType.Lounge || r.Type == RoomType.Dining || r.Type == RoomType.Library || r.Type == RoomType.GameRoom || r.Type == RoomType.TeaRoom)).OrderByDescending(r => S.Actors.Values.Count(x => x.Room == r.Id && x.Alive)).FirstOrDefault();
                        if (busy == null) { Advance(sim, a, plan); return null; }
                        plan.AlibiRoom = busy.Id;
                        act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(busy, rng))); act.Steps.Add(Simulation.Do("observe", 20, Anim.Idle)); act.Steps.Add(new ActionStep { Kind = "PlanAdvance" });
                        act.Label = "사람들 곁에서 휴식"; return act;
                    }
            }
            Advance(sim, a, plan); return null;
        }

        static string CoverLabel(PlanStep st) => st.Kind == "Alibi" ? "사람들 곁에서 휴식" : st.Kind == "GetWeapon" ? "물건 챙기기" : st.Kind == "Stalk" ? "산책" : st.Kind == "WaitNight" ? "휴식" : "볼일";

        static P3 Near(Simulation sim, Door d, Room side)
        {
            float off = 0.9f;
            return d.AlongX ? new P3(d.Pos.f, d.Pos.x, d.Pos.z + (side.Rect.CZ > d.Pos.z ? off : -off)) : new P3(d.Pos.f, d.Pos.x + (side.Rect.CX > d.Pos.x ? off : -off), d.Pos.z);
        }

        static Room NearestRoom(Simulation sim, Actor a, params RoomType[] types)
            => sim.S.Layout.Rooms.Where(r => types.Contains(r.Type)).OrderBy(r => a.Pos.Dist(new P3(r.Floor, r.Rect.CX, r.Rect.CZ))).FirstOrDefault();

        static Room HideRoom(Simulation sim, Actor a, Rng rng)
        {
            var S = sim.S; var k = S.K(a.Id);
            var c = S.Layout.Rooms.Where(r => k.Facts.Contains("visited:" + r.Id) && sim.RoomUsable(a, r) && (r.Type == RoomType.Closet || r.Type == RoomType.Storage || r.Type == RoomType.GuestRoom || r.Type == RoomType.Greenhouse || r.Type == RoomType.WineCellar || RoomInfo.IsMystery(r.Type) || r.Type == RoomType.Laundry)).ToList();
            return c.Count > 0 ? c[rng.R(c.Count)] : S.RoomOf(a);
        }

        static int DumpRoom(Simulation sim, Actor a, Actor victim, Rng rng)
        {
            var S = sim.S; var k = S.K(a.Id);
            var c = S.Layout.Rooms.Where(r => k.Facts.Contains("visited:" + r.Id) && sim.RoomUsable(a, r) && r.Id != victim.Room && r.Floor == victim.Pos.f && (r.Type == RoomType.Pool || r.Type == RoomType.Greenhouse || r.Type == RoomType.Closet || r.Type == RoomType.GuestRoom || RoomInfo.IsMystery(r.Type) || r.Type == RoomType.Chapel)).ToList();
            c = c.Where(r => new P3(r.Floor, r.Rect.CX, r.Rect.CZ).DistXZ(victim.Pos) < 30).ToList();
            return c.Count > 0 ? c[rng.R(c.Count)].Id : -1;
        }

        public static void Advance(Simulation sim, Actor a, MurderPlan plan)
        {
            if (plan.Step < plan.Steps.Count)
            {
                var k = plan.Steps[plan.Step].Kind; plan.Steps[plan.Step].Done = true;
                if (Lethal(k) || k == "ArmPress") plan.Stage = "Concealing";
                else if (plan.Stage == "Forming") plan.Stage = "Preparing";
            }
            plan.Step++;
            sim.S.Log("PlanStep", a.Id, plan.Target, plan: plan.Id, data: plan.Step < plan.Steps.Count ? plan.Steps[plan.Step].Kind : "end", secret: true);
            a.NextThink = sim.S.Clock;
            if (plan.Step >= plan.Steps.Count) Finish(sim, a, plan);
        }

        public static bool Lethal(string k) => k == "Attack" || k == "KnockOut" || k == "Drown" || k == "X_WaitTrap" || k == "X_Dose" || k == "X_WaitBedtime";
        public static void SwitchToStalkPublic(MurderPlan plan) => SwitchToStalk(plan);

        public static void Replan(Simulation sim, Actor a, MurderPlan plan, string why)
        {
            var S = sim.S; plan.Tries++;
            plan.Log.Add($"{ClockFmt.DayHM(S.Clock)} 수정: {why}");
            S.Log("PlanRevise", a.Id, plan.Target, plan: plan.Id, data: why, secret: true);
            if (plan.Tries > 4 || plan.Steps.Skip(plan.Step).All(s => !Lethal(s.Kind) && s.Kind != "X_ArmTrap")) { if (plan.Steps.Skip(plan.Step).Any(s => Lethal(s.Kind) || s.Kind == "X_ArmTrap")) Abort(sim, a, plan, why); else Advance(sim, a, plan); return; }
            if (plan.Grammar != null && (plan.Grammar.StartsWith("Poison") || plan.Grammar.StartsWith("Trap") || Methods.NoWeapon(plan.Grammar))) return;   // no blade needed: the plan itself is the weapon
            // re-derive the weapon from current knowledge; keep the grammar
            var w = KnownWeapons(sim, a).OrderBy(x => x.dist).FirstOrDefault();
            if (w.id == null) { plan.Steps.Insert(plan.Step, new PlanStep { Kind = "FindWeapon" }); return; }
            plan.Weapon = w.id; plan.WeaponType = S.I(w.id).Type;
            var st = plan.Steps[plan.Step];
            if (st.Kind == "GetWeapon" || st.Kind == "GetItem") st.Item = w.id;
            else if (S.I(w.id).Holder != a.Id) plan.Steps.Insert(plan.Step, new PlanStep { Kind = "GetWeapon", Item = w.id });
        }

        public static void Abort(Simulation sim, Actor a, MurderPlan plan, string why)
        {
            var S = sim.S;
            plan.Stage = "Aborted"; plan.Log.Add($"{ClockFmt.DayHM(S.Clock)} 중단: {why}");
            S.Log("PlanAbort", a.Id, plan.Target, plan: plan.Id, data: why, secret: true);
            S.Dev($"abort {a.Id} {plan.Grammar}: {why}");
            foreach (var r in S.Ch.Reservations.Where(r => r.Plan == plan.Id && !r.Consumed)) r.Released = true;
            if (plan.Disguise != null && a.Disguise == plan.Disguise) { a.Disguise = null; S.Emit(GameEventType.Disguise, a.Id, data: null); }
            a.PlanId = null; a.Needs.Stress = MathX.Clamp01(a.Needs.Stress - 0.2f);
            Tricks.OnPlanAbort(sim, a, plan);
            // an aborted intent cools down; a new plan needs a new evaluation later
            S.Flags["motive:" + a.Id] = S.Clock + 180;
            if (a.Act != null && a.Act.Id != null && a.Act.Id.StartsWith("murder")) sim.Interrupt(a);
        }

        static void Finish(Simulation sim, Actor a, MurderPlan plan)
        {
            var S = sim.S; plan.Stage = "Done"; a.PlanId = null;
            S.Log("PlanDone", a.Id, plan.Target, plan: plan.Id, secret: true);
            S.K(a.Id).Facts.Add("culprit-of:" + plan.Target);
        }

        static void Review(Simulation sim, Actor a)
        {
            var S = sim.S; if (!S.Plans.TryGetValue(a.PlanId, out var plan)) { a.PlanId = null; return; }
            // watchdog: a step that should take minutes but has made no progress for an hour is re-thought, not waited out
            if (plan.Step < plan.Steps.Count)
            {
                var st = plan.Steps[plan.Step]; string wk = "stepstart:" + plan.Id + ":" + plan.Step;
                if (!S.Flags.TryGetValue(wk, out var ws)) S.Flags[wk] = ws = S.Clock;
                bool waiting = st.Kind == "WaitNight" || st.Kind == "WaitVictim" || st.Kind == "Stalk" || st.Kind == "Alibi" || st.Kind == "X_WaitTrap" || st.Kind == "X_WaitGathering" || st.Kind == "X_Dose" || st.Kind == "Invite" && S.IsNight || Methods.Waiting(st.Kind) || Initiative.Waiting(st.Kind);   // --- initiative: scheme waits
                if (!waiting && S.Clock - ws > 60)
                {
                    S.Flags.Remove(wk);
                    if (st.Kind == "GetWeapon" || st.Kind == "GetItem") S.K(a.Id).ItemSeen.Remove(st.Item ?? "");
                    if (a.Act != null && a.Act.Id != null && a.Act.Id.StartsWith("murder")) sim.Interrupt(a, 1);
                    Replan(sim, a, plan, "stuck");
                    return;
                }
            }
            // conscience check: strong new bonds or a rescued friend can make someone back out before the act
            bool beforeAttack = plan.Steps.Skip(plan.Step).Any(x => Lethal(x.Kind));
            if (!beforeAttack) return;
            if (Initiative.OwnsConscience(S, plan)) return;   // --- initiative: a scheme weighs its own motive (a secret, a witness, a copycat's arithmetic) while preparing
            var (p, target, motive) = Relations.Pressure(sim, a);
            if (target != plan.Target && p < 0.6f || p < 0.2f) Abort(sim, a, plan, "마음이 바뀌어서");
        }

        // ------------------------------------------------------------------ step execution
        public static void Exec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S;
            if (st.Kind.StartsWith("S_") && Initiative.Exec(sim, a, st)) return;   // --- initiative: scheme action steps (Sim/Murder)
            if (st.Kind.StartsWith("X_") && Grammars.Exec(sim, a, st)) return;
            if (st.Kind.StartsWith("SP_") && SetPieces.Exec(sim, a, st)) return;
            if (a.IsButler && Butler.Exec(sim, a, st)) return;
            if (Cases.Exec(sim, a, st)) return;
            MurderPlan plan = a.PlanId != null && S.Plans.TryGetValue(a.PlanId, out var pp) ? pp : null;
            var rng = S.R(Stream.PlanTie);
            switch (st.Kind)
            {
                case "Flag": S.Flags[st.Tag] = 1; sim.NextStepPublic(a); return;
                case "PlanAdvance": if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return;
                case "PlanCheckWeapon":
                    if (plan != null) { if (KnownWeapons(sim, a).Any()) { plan.Steps[plan.Step].Done = true; plan.Step++; Replan(sim, a, plan, "found weapon"); } }
                    sim.NextStepPublic(a); return;
                case "PlanPick":
                    {
                        var it = S.I(st.Item);
                        if (it == null || it.Holder != null || it.Pos.DistXZ(a.Pos) > 1.8f) { if (plan != null) { S.K(a.Id).ItemSeen.Remove(st.Item); Replan(sim, a, plan, "weapon missing"); } sim.Interrupt(a); return; }
                        // hide small blades in a pocket if you're the careful type
                        // --- concealment (Systems/Concealment.cs): what fits under their clothes goes there — the same act as anyone's
                        // (the clothes decide what fits, and someone may see it done)
                        if (sim.PickUp(a, it) && a.Def.Deceit >= 70 && Concealment.SlotOf(S, it) == BodySlot.None && Concealment.BestSlot(S, a, it, out _) != BodySlot.None) Concealment.Conceal(sim, a, it);
                        a.Anim = Anim.PickUp; if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return;
                    }
                case "PlanInvite":
                    {
                        var t = S.A(st.Actor); if (t == null || !t.Alive) { sim.Interrupt(a); return; }
                        if (a.Pos.Dist(t.Pos) > 2.2f)
                        {
                            // walk to the target
                            if (a.Act.Path == null || S.Tick % 15 == 0) { var pr = Pathfinder.Find(S.Layout, a.Pos, t.Pos, sim.DoorCostFor(a)); if (!pr.Ok) { if (plan != null) Replan(sim, a, plan, "can't reach target"); sim.Interrupt(a); return; } a.Act.Path = pr.Points; a.Act.PathDoors = pr.DoorAtPoint; a.Act.PathStairs = pr.StairAtPoint; a.Act.PathIdx = 1; }
                            sim.MoveAlongPublic(a); if (a.Act != null && S.Clock - a.Act.StepStart > 40) { if (plan != null) Replan(sim, a, plan, "couldn't catch target"); sim.Interrupt(a); }
                            return;
                        }
                        var r = S.Layout.Room(st.Room); var rt = S.R(t.Id, a.Id);
                        double meetAt = S.Clock + 12 + rng.Range(0, 10);
                        var slots = new Dictionary<string, string> { { "act", "잠깐 이야기" }, { "place", r.Name } };
                        sim.Speak(a, "invite_ask", t.Id, slots);
                        bool accept = !t.IsPlayer && (rt.Trust + rt.Like) > 0.05f && t.Needs.Fear < 0.6f && t.Def.P.Fearfulness < 0.8f;
                        if (t.IsPlayer) accept = false; // the player decides by walking there; handled as a normal meeting request
                        sim.Speak(t, accept ? "invite_yes" : "invite_no", a.Id);
                        S.Log("Invite", a.Id, t.Id, room: r.Id, data: ClockFmt.HM(meetAt) + (accept ? " accepted" : " refused"));
                        if (accept) { S.Flags[$"meet:{t.Id}"] = r.Id; S.Flags[$"meetat:{t.Id}"] = meetAt; S.Flags[$"meetwith:{t.Id}:{a.Id}"] = 1; S.K(t.Id).Facts.Add($"invited:{a.Id}:{r.Id}:{(int)meetAt}"); }
                        if (plan != null) { if (accept) Advance(sim, a, plan); else Replan(sim, a, plan, "invitation refused"); if (!accept) SwitchToStalk(plan); }
                        sim.NextStepPublic(a); return;
                    }
                case "PlanWaitVictim":
                    {
                        var t = S.A(st.Actor);
                        if (t == null || !t.Alive) { sim.Interrupt(a); return; }
                        a.Speed = 0; a.Anim = Anim.Idle;
                        if (t.Room == st.Room && a.Room == st.Room) { if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return; }
                        if (S.Clock - a.Act.StepStart > 45) { if (plan != null) { Replan(sim, a, plan, "victim didn't come"); SwitchToStalk(plan); } sim.Interrupt(a); }
                        return;
                    }
                case "PlanVisit":
                    {
                        var d = S.Layout.Doors[st.Door]; var t = S.A(st.Actor);
                        if (t == null || !t.Alive) { sim.Interrupt(a); return; }
                        if (!d.Locked) { if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return; } // unlocked: walk in
                        if (!S.Flags.ContainsKey("knocked:" + plan?.Id))
                        {
                            S.Flags["knocked:" + plan?.Id] = S.Clock; sim.Sound(SoundKind.Knock, d.Pos, 0.45f, a.Id); a.Anim = Anim.Knock;
                            var rt = S.R(t.Id, a.Id);
                            bool opens = t.Pose == Pose.Sleep ? rt.Trust > 0.35f : (rt.Trust + rt.Like > 0.25f && t.Needs.Fear < 0.5f);
                            if (!t.IsPlayer && opens) { sim.WakePublic(t); sim.Interrupt(t, 1); d.Locked = false; d.Open = true; sim.DoorChanged(d, t); S.Log("Unlock", t.Id, data: "door" + d.Id + " opened for " + a.Id); d.LockLog.Add($"{ClockFmt.HM(S.Clock)} unlock {t.Id} (visitor)"); }
                            return;
                        }
                        if (!d.Locked) { if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return; }
                        if (S.Clock - S.Flags["knocked:" + plan?.Id] > 2) { S.Flags.Remove("knocked:" + plan?.Id); if (plan != null) Replan(sim, a, plan, "door stayed shut"); if (plan != null) SwitchToStalk(plan); sim.Interrupt(a); }
                        return;
                    }
                case "PlanStalk":
                    { Stalk(sim, a, st, plan, rng); return; }
                case "PlanCut":
                    {
                        var t = S.A(st.Actor); int circuit = t != null ? S.Layout.Room(t.Room)?.Circuit ?? 1 : 1; if (circuit == 0) circuit = 1;
                        sim.SetCircuit(circuit, false, a.Id);
                        S.Flags["cut:" + plan?.Id] = circuit; if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return;
                    }
                case "PlanRestore":
                    {
                        if (plan != null && S.Flags.TryGetValue("cut:" + plan.Id, out var c)) sim.SetCircuit((int)c, true, a.Id);
                        if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return;
                    }
                case "PlanPutOn":
                    {
                        var it = S.I(st.Item); if (it != null && it.Holder == a.Id) { a.Disguise = it.Id; a.HandL = a.HandL == it.Id ? null : a.HandL; a.HandR = a.HandR == it.Id ? null : a.HandR; a.Pocket.Remove(it.Id); S.Log("DisguiseOn", a.Id, item: it.Id, room: a.Room, secret: true); S.Emit(GameEventType.Disguise, a.Id, data: it.Type); }
                        if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return;
                    }
                case "PlanTakeOff":
                    {
                        if (a.Disguise != null)
                        {
                            var it = S.I(a.Disguise); a.Disguise = null;
                            var hide = HideRoom(sim, a, rng);
                            // drop it where you are (quick) — real trace of the costume
                            it.Holder = null; it.Pos = a.Pos; it.Room = a.Room; it.Hidden = true; if (it.Bloody == false && a.BloodOnClothes > 0.2f) { it.Bloody = true; it.Surface.Add("blood"); }
                            S.Log("DisguiseOff", a.Id, item: it.Id, room: a.Room, pos: a.Pos, secret: true); S.Emit(GameEventType.Disguise, a.Id, data: null);
                            S.Emit(GameEventType.ItemMoved, a.Id, data: it.Id, text: "hide", pos: a.Pos);
                            a.BloodOnClothes *= 0.3f;
                        }
                        if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return;
                    }
                case "Attack": { Attack(sim, a, st, plan, rng); return; }
                case "PlanGrab":
                    {
                        var t = S.A(st.Actor);
                        if (t == null || t.Pos.Dist(a.Pos) > 2.2f || t.Status == ActorStatus.Active) { if (plan != null) Advance(sim, a, plan); sim.Interrupt(a); return; }
                        a.Carrying = t.Id; t.CarriedBy = a.Id; a.Anim = Violence.CarryAnim(S, a);   // (violence track: too heavy to lift → dragged)
                        S.Log("CarryStart", a.Id, t.Id, room: a.Room, pos: a.Pos, secret: true); S.Emit(GameEventType.Carry, a.Id, t.Id, value: 1);
                        if (!t.Alive || t.Body.Bleed > 0) sim.AddTrace("DragMark", a.Pos, a.Room, a.Id, t.Id, 0.5f, 1, "무언가를 끈 자국", "무거운 것이 옮겨졌다", "누가 무엇을 끌었는지");
                        if (plan != null && t.Body.Dead) { var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == t.Id && i.Chapter == S.Chapter); if (inc != null) inc.BodyMoved = true; }
                        sim.NextStepPublic(a); return;
                    }
                case "PlanRelease":
                    {
                        var t = S.A(a.Carrying);
                        if (t != null)
                        {
                            t.CarriedBy = null; a.Carrying = null; t.Pos = a.Pos; t.Room = a.Room;
                            t.Pose = st.Tag == "pressbed" ? Pose.LieBack : S.R(Stream.Combat).Pick(new[] { Pose.LieBack, Pose.LieFront, Pose.LieSide });
                            if (st.Tag == "pressbed") { S.PressVictim = t.Id; }
                            var room = S.Layout.Room(a.Room);
                            if (room?.Type == RoomType.Pool && t.Body.Dead) { t.Pose = Pose.LieFront; t.Wet = true; t.WetUntil = S.Clock + 9999; sim.Sound(SoundKind.Splash, a.Pos, 0.5f, a.Id); }
                            S.Log("CarryEnd", a.Id, t.Id, room: a.Room, pos: a.Pos, data: st.Tag, secret: true); S.Emit(GameEventType.Carry, a.Id, t.Id, value: 0);
                            if (t.Body.Dead) { var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == t.Id && i.Chapter == S.Chapter && i.Loop == S.Loop); if (inc != null) { inc.BodyMoved = true; inc.Notes.Add("시신을 옮겨 둔 곳: " + S.RoomName(a.Room)); } sim.AddTrace("BloodSmear", a.Pos, a.Room, a.Id, t.Id, 0.5f, 0, "옮겨진 시신 주변의 혈흔", "누군가 시신을 이곳에 옮겨 놓았다", "시신이 원래 어디 있었는지"); }
                            if (room?.Type == RoomType.ColdStorage && st.Tag == "dump") Methods.OnColdHide(sim, a, t);
                        }
                        if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return;
                    }
                case "PlanArm":
                    {
                        S.PressFireAt = S.Clock + 4 + rng.Range(0, 5); S.PressArmedBy = a.Id;
                        sim.Sound(SoundKind.Switch, a.Pos, 0.25f, a.Id);
                        S.Log("PressArmed", a.Id, S.PressVictim, room: a.Room, data: "fire@" + ClockFmt.HM(S.PressFireAt), secret: true);
                        S.Emit(GameEventType.Press, a.Id, data: "armed", value: 0);
                        if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return;
                    }
                case "PlanLockRoom":
                    {
                        var t = S.A(st.Actor); var room = t != null ? S.Layout.Room(t.Room) : null;
                        if (room == null || room.Doors.Count == 0) { if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return; }
                        // take the victim's key if they had one, lock from outside, pocket the key
                        var key = t.Pocket.Select(S.I).FirstOrDefault(i => i?.KeyFor != null);
                        if (key != null && room.Type == RoomType.Bedroom && room.Owner == t.Id)
                        {
                            t.Pocket.Remove(key.Id); a.Pocket.Add(key.Id); key.Holder = a.Id;
                            var d = S.Layout.Doors[room.Doors[0]];
                            // step out and lock
                            var outside = S.Layout.Room(d.RoomA == room.Id ? d.RoomB : d.RoomA);
                            a.Pos = sim.SnapPublic(Near(sim, d, outside)); a.Room = outside.Id;
                            sim.SetDoor(a, d, false, true, "밖에서 잠금");
                            S.Log("LockedRoomMade", a.Id, t.Id, key.Id, room.Id, secret: true);
                            var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == t.Id && i.Chapter == S.Chapter); if (inc != null) inc.Notes.Add("밀실: 피해자의 열쇠로 밖에서 잠갔다");
                            S.Flags["droppedkey:" + plan?.Id] = 1;
                        }
                        if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return;
                    }
                case "PlanWash":
                    {
                        var w = S.I(plan?.Weapon);
                        if (w != null && w.Holder == a.Id) { w.Washed = true; w.Bloody = false; w.Surface.Remove("blood"); if (!w.Surface.Contains("residue")) w.Surface.Add("residue"); S.Log("Wash", a.Id, item: w.Id, room: a.Room, pos: a.Pos, secret: true); S.Emit(GameEventType.ItemState, a.Id, data: w.Id, text: "washed"); sim.Sound(SoundKind.Splash, a.Pos, 0.2f, a.Id); sim.AddTrace("Water", a.Pos, a.Room, a.Id, null, 0.35f, 1, "개수대 주변의 물기", "최근 누군가 여기서 물을 썼다", "무엇을 씻었는지"); }
                        a.Anim = Anim.Wash; sim.NextStepPublic(a); return;
                    }
                case "PlanReturnWeapon":
                    {
                        var w = S.I(plan?.Weapon);
                        if (w != null && w.Holder == a.Id)
                        {
                            // put it back where it belongs if that's close, else leave it here
                            var home = S.Layout.Room(w.HomeRoom);
                            if (home != null && home.Id == a.Room) sim.DropItem(a, w, w.HomePos); else sim.DropItem(a, w, a.Pos, false);
                        }
                        if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return;
                    }
                case "PlanHideWeapon":
                    {
                        var w = S.I(plan?.Weapon);
                        if (w != null && w.Holder == a.Id)
                        {
                            // --- concealment: into the hiding place chosen for it (the same act as the player's), else down where they stand
                            var hf = st.Furniture >= 0 && st.Furniture < S.Layout.Furniture.Count ? S.Layout.Furniture[st.Furniture] : null;
                            if (hf == null || !Concealment.Stash(sim, a, w, hf).Ok) sim.DropItem(a, w, a.Pos, true);
                        }
                        foreach (var kid in a.Pocket.ToList()) { var key = S.I(kid); if (key?.KeyFor != null && key.Owner != a.Id && key.Owner != null && S.Flags.ContainsKey("droppedkey:" + plan?.Id)) sim.DropItem(a, key, a.Pos, true); }
                        if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return;
                    }
                case "PlanChange":
                    {
                        if (a.BloodOnClothes > 0.1f) { S.Log("ChangeClothes", a.Id, room: a.Room, data: $"blood={a.BloodOnClothes:0.00}", secret: true); var shirt = new Item { Id = S.NewId("it"), Type = "Sheet", Name = Cast.GivenOf(a.Id) + "의 옷가지", Bloody = true, Surface = new List<string> { "blood" }, Owner = a.Id, Pos = a.Pos, Room = a.Room, Hidden = true }; S.Items[shirt.Id] = shirt; S.Emit(GameEventType.ItemMoved, a.Id, data: shirt.Id, text: "spawn", pos: a.Pos); a.BloodOnClothes = 0; }
                        if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return;
                    }
            }
            sim.NextStepPublic(a);
        }

        static void SwitchToStalk(MurderPlan plan)
        {
            // invitation route failed → fall back to finding them alone
            for (int i = plan.Step; i < plan.Steps.Count; i++)
                if (plan.Steps[i].Kind == "Invite" || plan.Steps[i].Kind == "GoRoom" || plan.Steps[i].Kind == "WaitVictim" || plan.Steps[i].Kind == "VisitRoom" || plan.Steps[i].Kind == "WaitNight")
                { plan.Steps[i].Kind = "Stalk"; plan.Steps[i].Note = "alone"; }
            if (plan.Grammar == "Lure" || plan.Grammar == "NightVisit") plan.Grammar += "→Ambush";
        }

        // who (from the stalker's own current view) could witness
        public static List<Actor> VisibleOthers(Simulation sim, Actor a, Actor t)
        {
            var S = sim.S; var k = S.K(a.Id); var res = new List<Actor>();
            if (k.Open == null) return res;
            foreach (var kv in k.Open)
            {
                var x = S.A(kv.Key); if (x == null || x == a || x == t || !x.Alive || x.Pose == Pose.Sleep) continue;
                if (S.Clock - kv.Value.T1 > 0.6) continue;
                if (x.Pos.f == t.Pos.f && x.Pos.DistXZ(t.Pos) < 22f) res.Add(x);
            }
            // people in the same room the stalker can plainly hear/see
            foreach (var x in S.Actors.Values) if (x != a && x != t && x.Alive && x.Room == t.Room && x.Pose != Pose.Sleep && !res.Contains(x) && x.Pos.DistXZ(a.Pos) < 8f) res.Add(x);
            return res;
        }

        static void Stalk(Simulation sim, Actor a, ActionStep st, MurderPlan plan, Rng rng)
        {
            var S = sim.S; var t = S.A(st.Actor);
            if (t == null || !t.Alive) { sim.Interrupt(a); return; }
            var k = S.K(a.Id);
            // where does the stalker think the target is?
            bool seesTarget = k.Open != null && k.Open.TryGetValue(t.Id, out var sg) && S.Clock - sg.T1 < 0.5;
            P3 goal;
            var edgeWait = !seesTarget && st.Tag == "edge" ? Methods.EdgeWait(sim, a, t) : null;
            var sleepBed = !seesTarget && st.Tag == "asleep" ? S.Layout.BedroomOf(t.Id) : null;
            if (seesTarget) goal = t.Pos;
            else if (edgeWait.HasValue) goal = edgeWait.Value;   // wait at the gallery / stair top the target passes
            else if (sleepBed != null) goal = new P3(sleepBed.Floor, sleepBed.Rect.CX, sleepBed.Rect.CZ);   // the drugged go to bed
            else if (k.LastSeen.TryGetValue(t.Id, out var ls) && S.Clock - ls.t < 60) { var r = S.Layout.Room(ls.room); goal = new P3(r.Floor, r.Rect.CX, r.Rect.CZ); }
            else
            {
                // guess by habit: the target's favourite rooms or bedroom at night
                var tr = S.IsNight ? S.Layout.BedroomOf(t.Id) : S.Layout.Rooms.Where(r => t.Def.FavRooms.Contains(r.Type.ToString())).OrderBy(_ => rng.F()).FirstOrDefault() ?? S.Layout.First(RoomType.Lounge);
                goal = new P3(tr.Floor, tr.Rect.CX, tr.Rect.CZ);
            }
            float d = a.Pos.Dist(t.Pos);
            var wit = VisibleOthers(sim, a, t);
            bool alone = wit.Count == 0 && t.Following == null && t.FollowedBy == null && !t.IsPlayer || (t.IsPlayer && wit.Count == 0);
            bool dark = sim.RoomLight(t.Room) < 0.25f;
            bool ready = st.Tag == "reach" ? seesTarget && d < 6f : st.Tag == "alone-dark" ? seesTarget && alone && d < 14f : seesTarget && alone && d < 12f;
            if (st.Tag == "reach" && dark) ready = seesTarget && d < 8f; // in darkness witnesses can't identify
            if (st.Tag == "edge") ready = seesTarget && d < 10f && Methods.AtEdge(sim, t, out _, out _) && Methods.EdgeClear(sim, a, t);   // at a stair top / the gallery rail, nobody within sight range
            if (st.Tag == "asleep") ready = seesTarget && alone && d < 14f && Methods.Dozing(t);                    // dropped off, nobody else in the room
            if (ready && t.Room >= 0 && S.Layout.Room(t.Room).Type != RoomType.GrandHall)
            {
                // reserve a fatality slot before the dangerous act
                if (!Reserve(sim, a, t, plan)) { Abort(sim, a, plan, "때가 맞지 않아서"); return; }
                Advance(sim, a, plan); sim.NextStepPublic(a); return;
            }
            if (S.Clock - a.Act.StepStart > 90) { plan.Tries++; if (plan.Tries > 5) Abort(sim, a, plan, "끝내 기회가 오지 않아서"); else sim.Interrupt(a, 5); return; }
            // move: keep a discreet distance when target visible
            float keep = seesTarget ? 6f : 1f;
            if (a.Pos.Dist(goal) > keep)
            {
                if (a.Act.Path == null || S.Tick % 25 == 0)
                {
                    var pr = Pathfinder.Find(S.Layout, a.Pos, goal, sim.DoorCostFor(a));
                    if (!pr.Ok) { sim.Interrupt(a, 3); return; }
                    a.Act.Path = pr.Points; a.Act.PathDoors = pr.DoorAtPoint; a.Act.PathStairs = pr.StairAtPoint; a.Act.PathIdx = 1;
                }
                sim.MoveAlongPublic(a);
            }
            else { a.Speed = 0; a.Act.Path = null; if (seesTarget) a.Yaw = MathX.AngleDeg(t.Pos.x - a.Pos.x, t.Pos.z - a.Pos.z); }
        }

        public static bool Reserve(Simulation sim, Actor a, Actor t, MurderPlan plan)
        {
            var S = sim.S;
            if (S.Ch.Reservations.Any(r => r.Plan == plan.Id && !r.Released)) return true;
            int used = S.Ch.Reservations.Count(r => !r.Released && !r.Consumed) + S.Incidents.Values.Count(i => i.Loop == S.Loop && i.Chapter == S.Chapter);
            if (used >= S.Ch.VictimCap) return false;
            var res = new Reservation { Id = S.NewId("rsv"), Actor = a.Id, Target = t.Id, Plan = plan.Id };
            S.Ch.Reservations.Add(res); plan.Reservation = res.Id;
            S.Log("Reserve", a.Id, t.Id, plan: plan.Id, secret: true);
            return true;
        }

        // ------------------------------------------------------------------ the attack itself (contact-based, same rules for NPC and player)
        static void Attack(Simulation sim, Actor a, ActionStep st, MurderPlan plan, Rng rng)
        {
            var S = sim.S; var t = S.A(st.Actor); var crng = S.R(Stream.Combat);
            if (t == null || !t.Alive) { if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return; }
            if (plan != null && !Reserve(sim, a, t, plan)) { Abort(sim, a, plan, "때가 맞지 않아서"); return; }
            // the planned weapon first (a wire in the pocket beats the book that happens to be in hand)
            var weapon = (plan?.Weapon != null ? sim.Carried(a).FirstOrDefault(i => i.Id == plan.Weapon && i.Def?.IsWeapon == true) : null) ?? sim.Held(a, d => d != null && d.IsWeapon) ?? a.Pocket.Select(S.I).FirstOrDefault(i => i?.Def?.IsWeapon == true);
            if (weapon != null && a.HandR != weapon.Id && a.HandL != weapon.Id) { a.Pocket.Remove(weapon.Id); if (a.HandR != null) sim.DropItem(a, S.I(a.HandR), a.Pos); a.HandR = weapon.Id; }
            float d = a.Pos.Dist(t.Pos);
            // witnesses arriving mid-act make the attacker flee (unless the victim is almost gone)
            var wit = VisibleOthers(sim, a, t).Where(x => x.Pos.DistXZ(t.Pos) < 9).ToList();
            if (wit.Count > 0 && t.Status == ActorStatus.Active && a.Disguise == null && sim.RoomLight(t.Room) > 0.25f && (S.Clock - a.Act.StepStart) < 0.4)
            {
                if (plan != null) { plan.Log.Add($"{ClockFmt.DayHM(S.Clock)} 목격자 등장으로 공격 보류"); if (plan.Tries++ > 3) Abort(sim, a, plan, "witness"); else { plan.Step = Math.Max(0, plan.Steps.FindIndex(x => x.Kind == "Stalk" || x.Kind == "WaitVictim")); if (plan.Step < 0) plan.Step = 0; } }
                sim.Interrupt(a, 2); return;
            }
            // second-wave contact modes: a cord from behind, a shove at an edge, a pillow on a sleeper (Methods.Attack)
            Violence.RetagAttack(sim, a, t, st, weapon);   // --- violence track: a gun in hand fires; bare hands strangle
            if (Methods.IsMode(st.Tag)) { Methods.Attack(sim, a, t, st, plan, weapon, crng); return; }
            if (d > (weapon?.Def?.Reach ?? 0.6f) + 0.55f)
            {
                if (d > 25f || S.Clock - a.Act.StepStart > 6) { if (plan != null) { plan.Log.Add("대상을 놓침"); Replan(sim, a, plan, "lost target"); } sim.Interrupt(a); return; }
                if (a.Act.Path == null || S.Tick % 6 == 0)
                {
                    var pr = Pathfinder.Find(S.Layout, a.Pos, t.Pos, sim.DoorCostFor(a)); if (!pr.Ok) { sim.Interrupt(a); return; }
                    a.Act.Path = pr.Points; a.Act.PathDoors = pr.DoorAtPoint; a.Act.PathStairs = pr.StairAtPoint; a.Act.PathIdx = 1;
                }
                sim.MoveAlongPublic(a, true); return;
            }
            a.Speed = 0; a.Yaw = MathX.AngleDeg(t.Pos.x - a.Pos.x, t.Pos.z - a.Pos.z);
            long nextStrike = S.Flags.TryGetValue("strike:" + a.Id, out var ns) ? (long)ns : 0;
            if (S.Tick < nextStrike) return;
            S.Flags["strike:" + a.Id] = S.Tick + 9;
            // nobody keeps hitting forever: after many blows on someone who is down, the attacker stops (they may survive)
            string cntKey = "blows:" + a.Id + ":" + t.Id; double blows = S.Flags.TryGetValue(cntKey, out var bc) ? bc : 0; S.Flags[cntKey] = blows + 1;
            if (blows >= 14 && t.Status != ActorStatus.Active) { S.Flags.Remove(cntKey); S.Flags.Remove("attackstart:" + a.Id + ":" + t.Id); if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); return; }
            if (!S.Flags.ContainsKey("attackstart:" + a.Id + ":" + t.Id)) { S.Flags["attackstart:" + a.Id + ":" + t.Id] = S.Clock; Incidents.OnAttackBegin(sim, a, t, plan); }
            // victim awareness: facing the attacker?
            float face = Math.Abs(MathX.DeltaAngle(t.Yaw, MathX.AngleDeg(a.Pos.x - t.Pos.x, a.Pos.z - t.Pos.z)));
            bool aware = face < 100 && t.Status == ActorStatus.Active && t.Pose != Pose.Sleep;
            string mode = st.Tag;
            var def = weapon?.Def; DamageType dt = def?.Dmg ?? DamageType.Blunt; int sev = def?.Sev ?? 1;
            if (mode == "drown") { dt = DamageType.Drown; sev = 3; }
            BodyRegion region = PickRegion(dt, aware, mode, t, crng);
            if (mode == "ko") { region = dt == DamageType.Choke ? BodyRegion.Neck : BodyRegion.Head; sev = Math.Min(sev, 2); }   // a cord knocks out by the throat, not the face
            float hitP = 0.6f + (aware ? 0f : 0.35f) + (a.Def.Obs - 70) / 200f - (aware ? t.Body.Resistance * 0.3f : 0) + (t.Status != ActorStatus.Active ? 0.5f : 0);
            a.Anim = dt == DamageType.Stab ? Anim.Stab : dt == DamageType.Cut ? Anim.Slash : dt == DamageType.Choke || dt == DamageType.Drown ? Anim.Strangle : Anim.Overhead;
            S.Emit(GameEventType.Strike, a.Id, t.Id, pos: t.Pos, data: a.Anim.ToString());
            if (crng.F() < hitP)
            {
                int s = sev + (aware ? 0 : 1) - (crng.Chance(0.3) ? 1 : 0);
                if (mode == "ko") s = 2;
                sim.Strike(a.Id, t, region, dt, Math.Max(1, Math.Min(4, s)), weapon?.Id, plan?.Id ?? "attack", crng.Range(-0.1f, 0.1f), crng.Range(-0.1f, 0.1f), 0.1f);
                if (mode == "drown") Methods.OnDrown(sim, a, t);   // soaked victim, wet sleeves and a wet trail on the one who held them under
            }
            else
            {
                // miss / block: defensive wound on the arm or nothing
                if (aware && crng.Chance(0.5)) sim.Strike(a.Id, t, crng.Chance(0.5) ? BodyRegion.ArmL : BodyRegion.HandR, dt, 1, weapon?.Id, "defense");
                else sim.Sound(SoundKind.Struggle, t.Pos, 0.35f, a.Id);
                // a struggle can knock things over
                KnockNearby(sim, t, a);
            }
            if (mode == "ko" && t.Status == ActorStatus.Unconscious) { t.Body.UnconsciousUntil = S.Clock + crng.Range(14, 20); if (plan != null) Advance(sim, a, plan); sim.NextStepPublic(a); S.Flags.Remove("attackstart:" + a.Id + ":" + t.Id); return; }
            if (!t.Alive || (t.Status == ActorStatus.Unconscious && (t.Body.DeathAt >= 0 || t.Body.Bleed > 0.1f)))
            {
                // cold killers make sure; others leave in panic
                if (t.Alive && a.Def.P.Morality < 0.35f && crng.Chance(0.7)) return;
                if (plan != null) Advance(sim, a, plan); S.Flags.Remove("attackstart:" + a.Id + ":" + t.Id); S.Flags.Remove("blows:" + a.Id + ":" + t.Id);
                sim.NextStepPublic(a);
            }
        }

        static BodyRegion PickRegion(DamageType dt, bool aware, string mode, Actor t, Rng rng)
        {
            if (dt == DamageType.Drown || dt == DamageType.Choke) return BodyRegion.Neck;
            var w = new List<(BodyRegion r, double w)>();
            if (dt == DamageType.Stab) { w.Add((BodyRegion.Abdomen, 3)); w.Add((BodyRegion.Chest, 2)); w.Add((aware ? BodyRegion.ShoulderL : BodyRegion.Back, 1.5)); w.Add((BodyRegion.Neck, aware ? 0.3 : 1.2)); w.Add((BodyRegion.ArmR, aware ? 1 : 0.2)); }
            else if (dt == DamageType.Cut) { w.Add((BodyRegion.Neck, aware ? 0.6 : 2.5)); w.Add((BodyRegion.ArmL, aware ? 1.5 : 0.4)); w.Add((BodyRegion.Chest, 1)); w.Add((BodyRegion.ShoulderR, 1)); w.Add((BodyRegion.HandL, aware ? 1 : 0.1)); }
            else { w.Add((BodyRegion.Head, aware ? 1.5 : 3.5)); w.Add((BodyRegion.ShoulderL, 1)); w.Add((BodyRegion.ArmR, aware ? 1.2 : 0.3)); w.Add((BodyRegion.Back, 0.6)); }
            if (t.Pose == Pose.LieBack || t.Pose == Pose.LieFront || t.Status != ActorStatus.Active) { w.Add((BodyRegion.Head, 2)); w.Add((BodyRegion.Chest, 1)); }
            return rng.Weighted(w, x => x.w).r;
        }

        static void KnockNearby(Simulation sim, Actor t, Actor a)
        {
            var S = sim.S; var rng = S.R(Stream.Combat);
            var room = S.Layout.Room(t.Room); if (room == null) return;
            var item = S.Items.Values.Where(i => i.Holder == null && i.Room == t.Room && i.Pos.DistXZ(t.Pos) < 2.2f && i.Def != null).OrderBy(i => i.Pos.DistXZ(t.Pos)).FirstOrDefault();
            if (item != null && rng.Chance(0.5))
            {
                var def = item.Def; var old = item.Pos;
                item.Pos = new P3(item.Pos.f, item.Pos.x + rng.Range(-0.8f, 0.8f), item.Pos.z + rng.Range(-0.8f, 0.8f));
                bool breaks = def.Mat == Mat.Glass || def.Mat == Mat.Ceramic;
                if (breaks && item.Damage < 3)
                {
                    item.Damage = 3; sim.Sound(SoundKind.GlassBreak, item.Pos, 0.6f, a.Id);
                    for (int i = 0; i < 2; i++) { var fr = new Item { Id = S.NewId("it"), Type = "Fragment", Name = def.Kor + " 파편", ParentItem = item.Id, Pos = new P3(item.Pos.f, item.Pos.x + rng.Range(-0.5f, 0.5f), item.Pos.z + rng.Range(-0.5f, 0.5f)), Room = item.Room }; S.Items[fr.Id] = fr; S.Emit(GameEventType.ItemMoved, null, data: fr.Id, text: "spawn", pos: fr.Pos); }
                    S.Log("Break", a.Id, item: item.Id, room: item.Room, pos: item.Pos, data: "struggle", secret: true);
                }
                else sim.Sound(SoundKind.Crash, item.Pos, 0.45f, a.Id);
                S.Emit(GameEventType.ItemMoved, null, data: item.Id, text: breaks ? "broken" : "knocked", pos: item.Pos);
                sim.AddTrace("Scratch", old, item.Room, a.Id, t.Id, 0.3f, 1, "바닥에 긁힌 자국과 흐트러진 물건", "이 근처에서 몸싸움이나 충돌이 있었다", "언제, 누가 그랬는지");
            }
        }

        public static void VictimReact(Simulation sim, Actor v, Actor attacker)
        {
            var S = sim.S; var rng = S.R(Stream.Combat);
            if (v.IsPlayer || v.Status != ActorStatus.Active) return;
            if (v.Body.Speech > 0.3f && !S.Flags.ContainsKey("screamed:" + v.Id + ":" + (int)S.Clock)) { S.Flags["screamed:" + v.Id + ":" + (int)S.Clock] = 1; sim.Sound(SoundKind.Scream, v.Pos, 0.9f * v.Body.Speech, v.Id, v.Id); S.Emit(GameEventType.Speech, v.Id, text: "으아악!", pos: v.Pos, key: "scream", value: 1); }
            if (attacker == null) return;
            // resist or flee depending on body channels and personality (the user's examples: shoulder = can fight/flee, abdomen = weakened, neck = dead)
            bool resist = v.Body.Resistance > 0.45f && (v.Def.P.Aggression > 0.45f || v.Body.Mobility < 0.4f) && rng.Chance(0.55);
            if (resist && attacker.Alive)
            {
                var region = rng.Pick(new[] { BodyRegion.ArmR, BodyRegion.HandL, BodyRegion.Head, BodyRegion.Chest });
                sim.Strike(v.Id, attacker, region, DamageType.Blunt, 1, null, "resist");
                if (rng.Chance(0.25 * v.Body.Resistance) && attacker.HandR != null) sim.DropItem(attacker, S.I(attacker.HandR), attacker.Pos); // disarmed
                S.Log("Resist", v.Id, attacker.Id, room: v.Room, secret: true);
            }
            if (v.Body.Mobility > 0.25f)
            {
                // flee toward people (by the victim's own knowledge of where people are)
                var k = S.K(v.Id);
                var crowdRoom = k.LastSeen.Where(kv => S.Clock - kv.Value.t < 30 && kv.Key != attacker.Id).GroupBy(kv => kv.Value.room).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
                var target = crowdRoom != 0 ? S.Layout.Room(crowdRoom) : S.Layout.Rooms.First(r => r.Type == RoomType.GrandHall);
                var act = new Activity { Id = "flee", Label = "도망", Priority = 200, Interruptible = false };
                act.Steps.Add(new ActionStep { Kind = "GoTo", Target = sim.RandomPointIn(target, rng), HasTarget = true, Run = true });
                act.Steps.Add(new ActionStep { Kind = "Say", Tag = "scream_discover" });
                sim.Assign(v, act);
                S.Log("Flee", v.Id, attacker.Id, room: v.Room, secret: true);
            }
        }

        public static void OnPostmortem(Simulation sim, Actor v, Wound w)
        {
            var S = sim.S;
            var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == v.Id && i.Loop == S.Loop);
            if (inc != null) { inc.Mutilated = true; inc.Notes.Add("숨진 뒤 입힌 상처: " + WoundText.Describe(w)); }
        }

        public static void OnStepFailed(Simulation sim, Actor a, string why = null)
        {
            var S = sim.S; if (a.PlanId == null || !S.Plans.TryGetValue(a.PlanId, out var plan)) return;
            var cur = plan.Step < plan.Steps.Count ? plan.Steps[plan.Step] : null;
            if (why == null && cur != null && cur.Kind == "Stalk" && (cur.Note == "edge" || cur.Note == "asleep"))
            { a.Act = null; a.NextThink = S.Clock + 0.3; return; }   // a patient wait (stair top, a drugged sleeper): being called away to dinner is not a failure — the deadline still ends it
            Replan(sim, a, plan, why ?? "interrupted");
            if (a.Act != null) { a.Act = null; } a.NextThink = S.Clock + 0.3;
        }

        // ------------------------------------------------------------------ after the act: keep the story straight
        static Activity PostCrime(Simulation sim, Actor a)
        {
            // an abandoned trap is the first thing its maker goes back for
            var trap = sim.S.Traps.FirstOrDefault(x => x.Owner == a.Id && x.Active);
            if (trap != null && a.Status == ActorStatus.Active) { if (a.Act != null && a.Act.Id == "g:disarm:" + trap.Id) return a.Act; return Tricks.DisarmActivity(sim, a, trap); }
            return null;
        }

        /// <summary>Time-driven facilities: the press, recorders, traps.</summary>
        public static void Facilities(Simulation sim)
        {
            var S = sim.S;
            if (S.PressFireAt >= 0 && S.Clock >= S.PressFireAt)
            {
                S.PressFireAt = -1;
                var mr = S.Layout.First(RoomType.MachineRoom);
                if (!S.PressPowered || !S.CircuitOn(7)) { S.Log("PressFail", null, data: "no power", secret: true); return; }
                S.PressRam = 1; S.Emit(GameEventType.Press, S.PressArmedBy, S.PressVictim, value: 1, data: "fire");
                sim.Sound(SoundKind.Press, new P3(mr.Floor, mr.Rect.CX, mr.Rect.CZ), 1f, null);
                var bedSpot = mr.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Tag == "pressbed");
                foreach (var x in S.Actors.Values.Where(x => x.Room == mr.Id && bedSpot != null && x.Pos.DistXZ(bedSpot.Pos) < 1.3f && x.Status != ActorStatus.Executed))
                {
                    // physical contact decides; the remote command alone kills nobody
                    sim.Strike(S.PressArmedBy, x, BodyRegion.Chest, DamageType.Crush, 4, null, "press");
                    S.Log("PressContact", S.PressArmedBy, x.Id, room: mr.Id, secret: true);
                }
                S.Flags["pressreturn"] = S.Clock + 2;
            }
            if (S.Flags.TryGetValue("pressreturn", out var pr) && S.Clock > pr) { S.PressRam = 0; S.Flags.Remove("pressreturn"); S.Emit(GameEventType.Press, value: 0, data: "return"); }
        }
    }
}
