using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// 사후 해체 (plan layer "Dismember"). After the kill the culprit carries the body to a work room that has a saw
    /// (cold store, workshop, kitchen, incinerator room, machine room), cuts it up there, and spreads two or three wrapped
    /// pieces over the house — the furnace, the pool, the greenhouse soil, a crate or a locker. The torso stays in the work room.
    /// Few, readable clues: cut surfaces with no vital reaction (cut after death — the saw is not what killed), saw-tooth marks,
    /// washed-down blood in the work room's drain, bone dust between the saw's teeth, and the hidden pieces themselves.
    /// The pieces come from one adapter, <see cref="CutUp"/>: the only place that will call Gore.Dismember once that lands.
    /// </summary>
    public static partial class Methods
    {
        static readonly (RoomType type, double pref)[] WorkRooms = { (RoomType.ColdStorage, 0.5), (RoomType.Workshop, 0.4), (RoomType.Kitchen, 0.3), (RoomType.Incinerator, 0.3), (RoomType.MachineRoom, 0.2), (RoomType.Laundry, 0.1) };
        static readonly string[] BoxTypes = { "Crates", "ColdLocker", "Trunk", "Chest", "Barrel", "Wardrobe" };
        public static bool IsSaw(Item i) => i?.Def != null && (i.Def.Tag == "saw" || i.Type == "Cleaver");
        public static bool Dismembered(GameState S, string victim) => victim != null && S.Flags.ContainsKey("dismembered:" + victim);
        static bool DismemberKind(string k) => k == "X_Dismember" || k == "X_ScatterParts";
        static bool DismemberAct(string k) => k == "X_MSawPick" || k == "X_MDismember" || k == "X_MPartPick" || k == "X_MHidePart";

        // ================================================================== planner
        /// <summary>A known, usable room with a saw (or a cleaver) in it — or any such room when the planner already carries one.</summary>
        public static (Room room, Item saw) WorkRoom(Simulation sim, Actor a)
        {
            var S = sim.S; var held = sim.Carried(a).FirstOrDefault(IsSaw);
            Room best = null; Item bestSaw = null; double bs = double.MinValue;
            foreach (var (type, pref) in WorkRooms)
                foreach (var r in S.Layout.Rooms.Where(r => r.Type == type && !r.Void).OrderBy(r => r.Id))
                {
                    if (!Knows(sim, a, r) || !sim.RoomUsable(a, r)) continue;
                    var saw = held ?? KnownItems(sim, a, i => IsSaw(i) && i.Room == r.Id && !i.Surface.Contains("burnt")).FirstOrDefault();
                    if (saw == null) continue;
                    double s = pref + (saw.Def.Tag == "saw" ? 0.2 : 0) - (RoomInfo.NightLocked(type) ? 0.15 : 0) + (type == RoomType.ColdStorage ? Aff(a.Id, "ColdHide") * 0.5 : 0);
                    if (s > bs) { bs = s; best = r; bestSaw = saw; }
                }
            return (best, bestSaw);
        }

        /// <summary>Planner layer: cold, composed killers (a cook, an undertaker, a trader with a cold store) — or the test switch.</summary>
        static void AugmentDismember(Simulation sim, MurderPlan plan, Actor a, Rng rng, List<string> added)
        {
            var c = a.Def; string head = Head(plan.Grammar);
            int attack = plan.Steps.FindIndex(s => s.Kind == "Attack");
            if (attack < 0 || Staged(plan.Grammar) || Remote(plan.Grammar) || head == "Press" || head == "Drown" || head == "Trap" || head == "Poison") return;
            // these need the body where it fell (a sealed room, a warmed body, a message in blood, a planted weapon)
            if (plan.Steps.Any(s => s.Kind == "X_Seal" || s.Kind == "X_KeySlide" || s.Kind == "X_Tod" || s.Kind == "X_Message" || s.Kind == "X_Swap")) return;
            bool want = F("Dismember") || (c.P.Morality < 0.3f && c.Composure >= 70 && rng.Chance(0.08 + Aff(a.Id, "Dismember")));
            if (!want) return;
            var (room, saw) = WorkRoom(sim, a); if (room == null) return;
            plan.Steps.RemoveAll(s => s.Kind == "LockRoom" || s.Kind == "MoveBody" || s.Kind == "X_Mutilate");
            attack = plan.Steps.FindIndex(s => s.Kind == "Attack");
            plan.Steps.Insert(attack + 1, new PlanStep { Kind = "MoveBody", Target = plan.Target, Note = "work", Room = room.Id });
            plan.Steps.Insert(attack + 2, new PlanStep { Kind = "X_Dismember", Target = plan.Target, Room = room.Id, Item = saw.Id });
            plan.Steps.Insert(attack + 3, new PlanStep { Kind = "X_ScatterParts", Target = plan.Target, Room = room.Id });
            if (!plan.Steps.Any(s => s.Kind == "CleanUp" || s.Kind == "X_Burn")) plan.Steps.Insert(attack + 4, new PlanStep { Kind = "CleanUp" });
            added.Add("Dismember");
        }

        /// <summary>Where the pieces go: distinct places the planner knows, best first — the furnace, the pool, the soil, a box.</summary>
        static List<(string kind, Room room, Furniture f)> PartDestinations(Simulation sim, Actor a, int workRoom, Rng rng)
        {
            var S = sim.S; var res = new List<(string kind, Room room, Furniture f, double s)>();
            Furniture FirstOf(Room r, params string[] types) => r?.Furniture.Select(i => S.Layout.Furniture[i]).Where(x => types.Contains(x.Type)).OrderBy(x => x.Id).FirstOrDefault();
            var inc = KnownRoom(sim, a, RoomType.Incinerator); var fi = FirstOf(inc, "Incinerator"); if (fi != null) res.Add(("burn", inc, fi, 0.6 + Aff(a.Id, "Burn")));
            var pool = KnownRoom(sim, a, RoomType.Pool); var fw = FirstOf(pool, "PoolWater"); if (fw != null && !(S.IsNight && RoomInfo.NightLocked(RoomType.Pool))) res.Add(("water", pool, fw, 0.45 + Aff(a.Id, "Dump")));
            var gh = KnownRoom(sim, a, RoomType.Greenhouse); var fp = FirstOf(gh, "Planter"); if (fp != null) res.Add(("soil", gh, fp, 0.4 + Aff(a.Id, "Bury")));
            int boxes = 0;
            foreach (var rt in new[] { RoomType.ColdStorage, RoomType.Storage, RoomType.WineCellar, RoomType.BoilerRoom, RoomType.Closet })
            {
                var r = KnownRoom(sim, a, rt); var fb = FirstOf(r, BoxTypes); if (fb == null) continue;
                res.Add(("box", r, fb, 0.35 + (r.Id == workRoom ? 0.1 : 0) - boxes * 0.1)); if (++boxes >= 2) break;
            }
            return res.OrderByDescending(x => x.s + rng.F() * 0.3).ThenBy(x => x.f.Id).Select(x => (x.kind, x.room, x.f)).ToList();
        }

        static P3 ApproachOf(Simulation sim, Furniture f, Room room)
        {
            var sp = room.Spots.Select(i => sim.S.Layout.Spots[i]).FirstOrDefault(s => s.Furniture == f.Id);
            if (sp != null) return sp.Approach;
            return sim.SnapPublic(new P3(f.Pos.f, f.Pos.x + (room.Rect.CX - f.Pos.x) * 0.3f, f.Pos.z + (room.Rect.CZ - f.Pos.z) * 0.3f));
        }

        static void SkipDismember(Simulation sim, Actor a, MurderPlan plan)
        {
            while (plan.Step < plan.Steps.Count && DismemberKind(plan.Steps[plan.Step].Kind)) Crime.Advance(sim, a, plan);
        }

        static bool Watched(Simulation sim, Actor a, int room, string victim) =>
            sim.S.Actors.Values.Any(x => x != a && x.Id != victim && x.Alive && x.Room == room && x.Status == ActorStatus.Active && x.Pose != Pose.Sleep);

        // ================================================================== plan step → activity
        static Activity DismemberThink(Simulation sim, Actor a, MurderPlan plan, PlanStep st, Activity act)
        {
            var S = sim.S; var t = S.A(plan.Target); var rng = S.R(Stream.PlanTie);
            if (st.Kind == "X_Dismember")
            {
                if (t == null || t.Alive || t.CarriedBy != null || t.Room != st.Room) { SkipDismember(sim, a, plan); return null; }   // the body never reached the work room
                if (Watched(sim, a, t.Room, t.Id))
                {
                    string wk = "dismwait:" + plan.Id; if (!S.Flags.ContainsKey(wk)) S.Flags[wk] = S.Clock;
                    if (S.Clock - S.Flags[wk] > 60) { S.Flags.Remove(wk); SkipDismember(sim, a, plan); }   // the room never empties: leave the body as it is
                    return null;
                }
                var saw = S.I(st.Item);
                if (saw == null || (saw.Holder != null && saw.Holder != a.Id)) saw = sim.Carried(a).FirstOrDefault(IsSaw) ?? KnownItems(sim, a, i => IsSaw(i) && i.Room == t.Room).FirstOrDefault();
                if (saw == null) { SkipDismember(sim, a, plan); return null; }
                st.Item = saw.Id;
                if (saw.Holder != a.Id) { act.Steps.Add(Simulation.GoTo(sim.SnapPublic(saw.Pos))); act.Steps.Add(new ActionStep { Kind = "X_MSawPick", Item = saw.Id }); }
                act.Steps.Add(Simulation.GoTo(sim.SnapPublic(t.Pos)));
                act.Steps.Add(new ActionStep { Kind = "X_MDismember", Actor = t.Id, Item = saw.Id, Room = st.Room, Duration = 20 });
                act.Label = "볼일"; act.Interruptible = false; return act;
            }
            // X_ScatterParts: pick the wrapped pieces up, then one piece per place
            var parts = (st.Note ?? "").Split(',').Where(x => x.Length > 0).Select(S.I).Where(i => i != null && (i.Holder == a.Id || (i.Holder == null && !i.Hidden && i.Room == st.Room))).ToList();
            if (parts.Count == 0) { Crime.Advance(sim, a, plan); return null; }
            var dests = PartDestinations(sim, a, st.Room, rng);
            if (dests.Count == 0) { Crime.Advance(sim, a, plan); return null; }
            int n = Math.Min(parts.Count, Math.Min(dests.Count, 3));
            foreach (var p in parts.Take(n).Where(p => p.Holder == null)) { act.Steps.Add(Simulation.GoTo(sim.SnapPublic(p.Pos))); act.Steps.Add(new ActionStep { Kind = "X_MPartPick", Item = p.Id }); }
            for (int i = 0; i < n; i++)
            {
                var d = dests[i];
                act.Steps.Add(Simulation.GoTo(ApproachOf(sim, d.f, d.room)));
                act.Steps.Add(new ActionStep { Kind = "X_MHidePart", Item = parts[i].Id, Tag = d.kind, Furniture = d.f.Id, Room = d.room.Id, Duration = d.kind == "soil" ? 4 : 2 });
            }
            act.Steps.Add(new ActionStep { Kind = "PlanAdvance" });
            act.Label = "볼일"; act.Interruptible = false; return act;
        }

        // ================================================================== step execution
        static bool DismemberExec(Simulation sim, Actor a, ActionStep st, MurderPlan plan)
        {
            var S = sim.S;
            switch (st.Kind)
            {
                case "X_MSawPick":
                case "X_MPartPick":
                    {
                        var it = S.I(st.Item);
                        if (it != null && it.Holder == null && it.Pos.DistXZ(a.Pos) < 2.2f)
                        {
                            if (st.Kind == "X_MSawPick") sim.PickUp(a, it);
                            else { a.Pocket.Add(it.Id); it.Holder = a.Id; it.Room = -1; it.LastMovedTick = S.Tick; S.Emit(GameEventType.ItemMoved, a.Id, data: it.Id, text: "pickup", pos: a.Pos); }   // the bundles: no separate ledger line (the reveal tells where each one went)
                        }
                        a.Anim = Anim.PickUp; sim.NextStepPublic(a); return true;
                    }
                case "X_MDismember":
                    {
                        var t = S.A(st.Actor); a.Speed = 0;
                        if (t == null || t.Alive || t.Pos.Dist(a.Pos) > 2.6f || t.CarriedBy != null) { if (plan != null) SkipDismember(sim, a, plan); sim.NextStepPublic(a); return true; }
                        if (Watched(sim, a, a.Room, t.Id)) { a.Anim = Anim.Idle; if (plan != null) SkipDismember(sim, a, plan); sim.NextStepPublic(a); return true; }   // someone walked in: stop, stand, say nothing
                        a.Anim = Anim.Slash; a.Pose = Pose.Crouch;
                        if (S.Tick % 30 == 0) sim.Sound(SoundKind.Machine, a.Pos, 0.3f, a.Id);   // a dull rasp through the door
                        if (S.Clock < a.Act.StepEnd) return true;
                        a.Pose = Pose.Stand;
                        var saw = S.I(st.Item);
                        var parts = CutUp(sim, a, t, saw, a.Room);
                        S.Flags["dismembered:" + t.Id] = S.Clock; S.Flags["sawroom:" + t.Id] = a.Room;
                        var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == t.Id && i.Loop == S.Loop);
                        if (inc != null) { inc.Mutilated = true; inc.BodyMoved = true; inc.Notes.Add("숨진 뒤 시신을 자른 곳: " + S.RoomName(a.Room)); }
                        // the saw is rinsed at the drain and put back where it lay — bone dust stays between the teeth
                        if (saw != null)
                        {
                            saw.Bloody = false; saw.Washed = true; saw.Surface.Remove("blood"); if (!saw.Surface.Contains("bonedust")) saw.Surface.Add("bonedust");
                            if (saw.Holder == a.Id) sim.DropItem(a, saw, saw.HomeRoom == a.Room ? saw.HomePos : a.Pos);
                        }
                        var drainAt = new P3(a.Pos.f, a.Pos.x + 0.6f, a.Pos.z + 0.4f);
                        var tr = sim.AddTrace("DrainBlood", drainAt, a.Room, a.Id, t.Id, 0.45f, 1, "배수구 틈과 바닥 이음새에 남은 묽은 핏물", "이 방에서 누군가 많은 피를 물로 씻어 냈다", "누구의 피인지, 언제 씻었는지");
                        if (tr != null) tr.Note = "dismember";
                        a.BloodOnClothes = Math.Max(a.BloodOnClothes, 0.9f);
                        var stp = plan?.Steps.FirstOrDefault(s => s.Kind == "X_ScatterParts" && !s.Done); if (stp != null) stp.Note = string.Join(",", parts);
                        S.Log("Dismember", a.Id, t.Id, saw?.Id, a.Room, a.Pos, parts.Count.ToString(), plan?.Id, true);
                        if (plan != null) Crime.Advance(sim, a, plan);
                        sim.NextStepPublic(a); return true;
                    }
                case "X_MHidePart":
                    {
                        a.Speed = 0; a.Anim = st.Tag == "soil" ? Anim.Garden : Anim.Hide; if (st.Tag == "soil") a.Pose = Pose.Crouch;
                        if (S.Clock < a.Act.StepEnd) return true;
                        a.Pose = Pose.Stand;
                        var it = S.I(st.Item); var f = S.Layout.Furniture.ElementAtOrDefault(st.Furniture);
                        if (it != null && it.Holder == a.Id && f != null)
                        {
                            var fp = new P3(f.Pos.f, f.Pos.x, f.Pos.z);
                            a.Pocket.Remove(it.Id); if (a.HandR == it.Id) a.HandR = null; if (a.HandL == it.Id) a.HandL = null;
                            it.Holder = null; it.Pos = fp; it.Room = f.Room; it.Hidden = true; it.LastMovedTick = S.Tick;
                            S.Emit(GameEventType.ItemMoved, a.Id, data: it.Id, text: "hide", pos: fp);   // logged once below as PartHidden
                            switch (st.Tag)
                            {
                                case "burn":
                                    if (!it.Surface.Contains("burnt")) it.Surface.Add("burnt"); it.Damage = 3; it.Name = "타다 남은 뼛조각";
                                    S.Flags["furnace:" + f.Id] = S.Clock; if (!f.Marks.Contains("burned")) f.Marks.Add("burned");
                                    sim.AddTrace("Ash", new P3(fp.f, fp.x + 0.5f, fp.z + 0.5f), f.Room, a.Id, it.Owner, 0.35f, 1, "소각로 앞에 새로 떨어진 재", "최근 누군가 소각로에 무언가를 태웠다", "누가 무엇을 태웠는지");
                                    sim.Sound(SoundKind.Machine, fp, 0.5f, a.Id); S.Emit(GameEventType.Furniture, a.Id, text: "stoked", id: f.Id);
                                    break;
                                case "water":
                                    it.Wet = true; if (!it.Surface.Contains("waterlogged")) it.Surface.Add("waterlogged");
                                    sim.Sound(SoundKind.Splash, fp, 0.35f, a.Id);
                                    break;
                                case "soil":
                                    if (!it.Surface.Contains("soil")) it.Surface.Add("soil"); if (!f.Marks.Contains("dug")) f.Marks.Add("dug");
                                    sim.AddTrace("Soil", fp, f.Room, a.Id, it.Owner, 0.35f, 1, "화분대 흙을 깊이 팠다가 다시 덮은 자국", "누군가 이 흙을 깊이 팠다가 다시 덮었다", "무엇을 묻었는지");
                                    break;
                                default:
                                    if (!it.Surface.Contains("stowed")) it.Surface.Add("stowed");
                                    break;
                            }
                            S.Log("PartHidden", a.Id, it.Owner, it.Id, f.Room, fp, st.Tag, plan?.Id, true);
                        }
                        sim.NextStepPublic(a); return true;
                    }
            }
            return false;
        }

        // ==================================================================================================================
        // ADAPTER for Gore.Dismember(sim, victim, by, tool, placement) — corpse-discovery workflow: implement Gore.Dismember
        // with your part model; CutUp will switch to it.
        // Interim representation: Items of type 'SeveredPart' (Owner=victim, Note=part name: Head/ArmL/ArmR/LegL/LegR/Torso),
        // positions and containers as placed. The torso is the victim actor itself (it stays in the work room); CutUp makes
        // the limb pieces next to it, then X_MHidePart moves each piece (Item.Pos/Room, Hidden=true) into a furnace, the pool,
        // planter soil or a crate/locker, with Surface tags burnt / waterlogged / soil / stowed.
        // Discovery of ANY piece goes through Methods.OnPieceSeen (hooked in Perception's item glimpse) → Cases.OnBodySeen.
        // Forcing for probes: set BL23.Sim.SetPieces.Force = "Dismember" before the campaign starts (SimTests: `mystery forced 8 Dismember`).
        // ==================================================================================================================
        /// <summary>The one adapter to the corpse model: cuts the body in the work room and returns the ids of the pieces made (limbs; the torso stays the actor).</summary>
        static List<string> CutUp(Simulation sim, Actor culprit, Actor victim, Item saw, int room)
        {
            var S = sim.S; var rng = S.R(Stream.Combat); var ids = new List<string>();
            int n = 2 + rng.R(3);   // two to four pieces
            for (int i = 0; i < n; i++)
            {
                var p = new P3(victim.Pos.f, victim.Pos.x + (i % 2 == 0 ? 0.5f : -0.5f), victim.Pos.z + (i < 2 ? 0.35f : -0.35f));
                var it = new Item { Id = S.NewId("it"), Type = "SeveredPart", Name = "천에 싼 꾸러미", Note = PieceCodes[i], Owner = victim.Id, Pos = sim.SnapPublic(p), Room = room, HomeRoom = room, HomePos = p, Bloody = true, LastUser = culprit.Id };
                it.Surface.Add("blood"); it.Surface.Add("sawn");
                S.Items[it.Id] = it; S.Emit(GameEventType.ItemMoved, culprit.Id, data: it.Id, text: "spawn", pos: it.Pos);
                ids.Add(it.Id);
            }
            // the cuts on the body: postmortem wounds (no vital reaction), added directly — no blood pool or struggle marks from a dead body
            for (int i = 0; i < n; i++)
                victim.Body.Wounds.Add(new Wound { Region = PieceRegions[i], Type = DamageType.Cut, Sev = 4, Postmortem = true, Tick = S.Tick, Clock = S.Clock, By = culprit.Id, Weapon = saw?.Id, CauseEvent = "dismember" });
            return ids;
        }

        static readonly string[] PieceCodes = { "ArmL", "ArmR", "LegL", "LegR" };
        static readonly BodyRegion[] PieceRegions = { BodyRegion.ArmL, BodyRegion.ArmR, BodyRegion.LegL, BodyRegion.LegR };
        public static string PartKor(string code) => code == "ArmL" ? "왼팔" : code == "ArmR" ? "오른팔" : code == "LegL" ? "왼다리" : code == "LegR" ? "오른다리" : code == "Head" ? "머리" : code == "Torso" ? "몸통" : "신체 일부";

        /// <summary>
        /// Someone sees a (no longer hidden) piece: that counts as discovering the body. Goes through the official discovery path
        /// (scream, bell, discoverers, the case), then the observer's card is corrected to what was actually seen, here.
        /// Redirect to Gore's discovery when it lands.
        /// </summary>
        public static void OnPieceSeen(Simulation sim, Actor o, Item piece)
        {
            var S = sim.S; var v = S.A(piece.Owner); if (v == null || v.Alive || o.Id == piece.LastUser) return;
            string key = "pieceseen:" + o.Id + ":" + piece.Id; if (S.Flags.ContainsKey(key)) return; S.Flags[key] = S.Clock;
            var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == v.Id && i.Loop == S.Loop);
            bool first = inc != null && !inc.Discovered;
            Cases.OnBodySeen(sim, o, v, new Sighting { Target = v.Id, Room = piece.Room, T0 = S.Clock, T1 = S.Clock, IdConf = 0.3f, Dead = true, Direct = true });
            if (first && inc.Discovered) inc.FoundRoom = piece.Room;
            var card = S.K(o.Id).Evidence.LastOrDefault(e => e.Root == "body:" + v.Id + ":" + o.Id);
            if (card != null && card.Room != piece.Room)
            {
                card.Title = "피가 밴 천 꾸러미";
                card.Desc = $"{S.RoomName(piece.Room)}에서 피가 밴 천 꾸러미가 나왔다 — 속에 싸인 것은 사람의 {PartKor(piece.Note)}. 옷자락으로 보아 {Cast.GivenOf(v.Id)}의 것이다.";
                card.Room = piece.Room; foreach (var p in card.Props.Where(p => p.Kind == PropKind.FoundPlace)) p.Room = piece.Room;
            }
            S.Log("PieceSeen", o.Id, v.Id, piece.Id, piece.Room, piece.Pos, piece.Note, secret: true);
        }
    }
}
