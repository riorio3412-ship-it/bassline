using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Restraint (ViolenceMotionContract.md "Restraint"): wrists, ankles and a gag, with rope, a cord, tape or a scarf. It needs
    /// someone who cannot resist (unconscious, asleep, drugged, held in a hold that has worn them down) or a second pair of
    /// hands. A bound person cannot use their hands (no doors, nothing picked up), shuffles or cannot walk at all, cannot cry
    /// out when gagged, and works the knots loose over time — which leaves raw marks on the wrists. The binding itself is
    /// evidence (knots, fibres, a length of cord that belongs somewhere else). NPC culprits use it as a plan layer ("Bind"),
    /// the player with V.
    /// </summary>
    public static partial class Violence
    {
        static readonly string[] BindTypes = { "Rope", "CurtainCord", "ExtensionCord", "Scarf", "Tape", "Thread" };
        public static bool IsBinding(ItemDef d) => d != null && Array.IndexOf(BindTypes, d.Type) >= 0 && d.Type != "Thread";
        /// <summary>Seconds (sim) for an average person to work free of a material pulled tight.</summary>
        static float FreeSeconds(string m) => m == "Tape" ? 220f : m == "Rope" ? 150f : m == "ExtensionCord" ? 130f : m == "CurtainCord" ? 110f : m == "Scarf" ? 70f : 120f;

        public static Binding Bound(GameState S, string id)
        {
            var l = S.Violence?.Bindings; if (l == null || id == null) return null;
            for (int i = l.Count - 1; i >= 0; i--) { var b = l[i]; if (!b.Off && b.Actor == id) return b; }
            return null;
        }
        public static bool HandsBound(GameState S, Actor a) => Bound(S, a.Id) is Binding b && b.Wrists;

        /// <summary>Can `by` tie `v` up right now (with `helper`'s hands, if any)?</summary>
        public static bool CanBind(GameState S, Actor by, Actor v, Actor helper, out string why)
        {
            why = null;
            if (by == null || v == null || !v.Alive || by == v) { why = "묶을 수 없다"; return false; }
            if (Bound(S, v.Id) != null) { why = "이미 묶여 있다"; return false; }
            if (by.Pos.f != v.Pos.f || by.Pos.DistXZ(v.Pos) > 1.8f) { why = "너무 멀다"; return false; }
            bool subdued = v.Status == ActorStatus.Unconscious || v.Pose == Pose.Sleep || Methods.Dozing(v) || S.Flags.ContainsKey("drowsy:" + v.Id)
                           || (HeldIn(S, v.Id) is Assault x && x.Phase >= AssaultPhase.Weaken) || v.Body.Mobility < 0.2f || v.Body.Conscious < 0.3f;
            bool twoPeople = helper != null && helper != v && helper.Alive && helper.Status == ActorStatus.Active && helper.Pos.f == v.Pos.f && helper.Pos.DistXZ(v.Pos) < 1.6f;
            if (!subdued && !twoPeople) { why = "저항하는 사람을 혼자서는 묶을 수 없다"; return false; }
            return true;
        }

        /// <summary>Tie `v` up with `material` (it stays on their body). Hands, legs and voice are taken away until it comes off.</summary>
        public static Binding Bind(Simulation sim, Actor by, Actor v, Item material, bool wrists, bool ankles, bool gag, string why = null)
        {
            var S = sim.S; if (v == null || material == null || !v.Alive) return null;
            var old = Bound(S, v.Id); if (old != null) return old;
            var b = new Binding
            {
                Actor = v.Id, By = by?.Id, Item = material.Id, Material = material.Type, Wrists = wrists, Ankles = ankles, Gag = gag, Tick = S.Tick, Clock = S.Clock,
                Tight = MathX.Clamp(0.55f + (by != null ? Strength(by) * 0.35f + (by.Def.Composure - 50) / 250f : 0.2f), 0.45f, 1.1f),
                HandLWas = v.Body.HandL, HandRWas = v.Body.HandR, MobilityWas = v.Body.Mobility, SpeechWas = v.Body.Speech, Why = why
            };
            // the material leaves the binder's hands and goes onto the body
            if (material.Holder != null) { var h = S.A(material.Holder); if (h != null) { if (h.HandR == material.Id) h.HandR = null; if (h.HandL == material.Id) h.HandL = null; h.Pocket.Remove(material.Id); } }
            material.Holder = v.Id; material.Room = -1; material.Hidden = false; if (!material.Surface.Contains("knotted")) material.Surface.Add("knotted");
            // what is lost while bound
            if (wrists) { if (v.HandR != null) sim.DropItem(v, S.I(v.HandR), v.Pos); if (v.HandL != null) sim.DropItem(v, S.I(v.HandL), v.Pos); v.Body.HandL = Math.Min(v.Body.HandL, 0.05f); v.Body.HandR = Math.Min(v.Body.HandR, 0.05f); }
            if (ankles) v.Body.Mobility = Math.Min(v.Body.Mobility, 0.08f);
            if (gag) v.Body.Speech = Math.Min(v.Body.Speech, 0.1f);
            S.Violence.Bindings.Add(b);
            S.Log("Bind", by?.Id, v.Id, material.Id, v.Room, v.Pos, $"{material.Type}|{(wrists ? "wrists" : "")}{(ankles ? "+ankles" : "")}{(gag ? "+gag" : "")}|tight={b.Tight:0.00}|{why}", by?.PlanId, true);
            S.Emit(GameEventType.Anim, by?.Id, v.Id, text: material.Type, data: "bind|" + (wrists ? "W" : "") + (ankles ? "A" : "") + (gag ? "G" : ""), pos: v.Pos);
            S.Emit(GameEventType.ItemMoved, by?.Id, data: material.Id, text: "bind", pos: v.Pos);
            if (v.Status == ActorStatus.Active && v.Pose != Pose.Sleep) v.NextThink = S.Clock;   // awake: struggle at once
            return b;
        }

        /// <summary>The binding comes off (freed by someone, worked loose, or taken off by the binder). The cord drops at their
        /// feet (or goes with the one who removed it when `keep`).</summary>
        public static void Unbind(Simulation sim, Actor by, Actor v, string why, bool keep = false)
        {
            var S = sim.S; var b = v != null ? Bound(S, v.Id) : null; if (b == null) return;
            MarkBindings(sim, v);
            b.Off = true; b.OffClock = S.Clock; b.OffBy = by?.Id; b.Why = why;
            if (v.Alive)
            {
                if (b.Wrists) { v.Body.HandL = Math.Max(v.Body.HandL, b.HandLWas); v.Body.HandR = Math.Max(v.Body.HandR, b.HandRWas); }
                if (b.Ankles) v.Body.Mobility = Math.Max(v.Body.Mobility, b.MobilityWas);
                if (b.Gag) v.Body.Speech = Math.Max(v.Body.Speech, b.SpeechWas);
            }
            var it = S.I(b.Item);
            if (it != null)
            {
                if (keep && by != null) { it.Holder = by.Id; it.Room = -1; if (!by.Pocket.Contains(it.Id)) by.Pocket.Add(it.Id); }
                else { it.Holder = null; it.Pos = v.Pos; it.Room = v.Room; it.Hidden = false; it.LastMovedTick = S.Tick; }
                S.Emit(GameEventType.ItemMoved, by?.Id, data: it.Id, text: keep ? "pickup" : "drop", pos: v.Pos);
            }
            S.Log("Unbind", by?.Id, v.Id, b.Item, v.Room, v.Pos, why, by?.PlanId, true);
            S.Emit(GameEventType.Anim, by?.Id, v.Id, text: b.Material, data: "unbind", pos: v.Pos);
            if (v.Alive && v.Act != null && v.Act.Id == "bound") sim.Interrupt(v, 0.2);
        }

        /// <summary>Raw rope marks on the wrists and ankles of someone who was bound (once): pulled against, or bound a while.</summary>
        static void MarkBindings(Simulation sim, Actor v)
        {
            var S = sim.S; var b = v != null ? Bound(S, v.Id) : null; if (b == null || b.Marked) return;
            bool worked = b.Loose > 0.05f || S.Clock - b.Clock > 3 || S.Violence.Assaults.Any(x => x.Victim == v.Id && x.Begun >= b.Tick);
            if (!worked) return;
            b.Marked = true; var by = S.A(b.By);
            if (b.Wrists) { Mark(sim, by, v, BodyRegion.HandL, DamageType.Blunt, 1, b.Item, "bound"); Mark(sim, by, v, BodyRegion.HandR, DamageType.Blunt, 1, b.Item, "bound"); }
            if (b.Ankles) Mark(sim, by, v, BodyRegion.FootL, DamageType.Blunt, 1, b.Item, "bound");
            S.Flags["boundmarks:" + v.Id] = S.Clock;
        }

        // ================================================================== bound people do not sit still
        static void TickBindings(Simulation sim)
        {
            var S = sim.S; var L = S.Violence.Bindings; if (L.Count == 0) return;
            if (S.Tick % Hz != 4) return;
            for (int i = 0; i < L.Count; i++)
            {
                var b = L[i]; if (b.Off) continue;
                var v = S.A(b.Actor); if (v == null) { b.Off = true; continue; }
                if (!v.Alive) continue;                                   // a bound body: the cords stay on (evidence)
                if (v.Status != ActorStatus.Active || v.Pose == Pose.Sleep || HeldIn(S, v.Id) != null || v.CarriedBy != null) continue;
                // straining at the knots
                float rate = (0.6f + 0.8f * Strength(v)) / (Math.Max(0.3f, b.Tight) * FreeSeconds(b.Material));
                if (!b.Wrists) rate *= 2.5f;                               // hands free: only the ankles or a gag left to pick at
                b.Loose += rate;
                if (b.Loose >= 1f) { WorkFree(sim, v, b); continue; }
                if (v.Act == null || v.Act.Id != "bound")
                {
                    var act = new Activity { Id = "bound", Label = b.Gag ? "재갈이 물린 채 몸부림치는 중" : "묶인 채 몸부림치는 중", Priority = 300, Interruptible = false };
                    if (!b.Ankles && b.Wrists && S.Flags.TryAdd("boundwalk:" + v.Id + ":" + b.Tick, S.Clock))   // one try (a shut door ends it: no hands for the handle)
                    {
                        // hands tied, legs free: go to where people are
                        var k = S.K(v.Id);
                        var crowd = k.LastSeen.Where(kv => S.Clock - kv.Value.t < 60 && kv.Key != b.By).GroupBy(kv => kv.Value.room).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => g.Key).FirstOrDefault();
                        var room = S.Layout.Room(crowd) ?? S.Layout.Rooms.First(r => r.Type == RoomType.GrandHall);
                        if (room.Id != v.Room) act.Steps.Add(new ActionStep { Kind = "GoTo", Target = sim.RandomPointIn(room, S.R(Stream.Life)), HasTarget = true });
                    }
                    act.Steps.Add(new ActionStep { Kind = "X_Bound", Duration = 999 });
                    v.TalkingTo = null; sim.Assign(v, act);
                }
                // a cry for help (or a muffled one)
                if (S.Clock - b.LastCry > 6)
                {
                    b.LastCry = S.Clock;
                    if (!b.Gag && v.Body.Speech > 0.3f) { S.Emit(GameEventType.Speech, v.Id, text: "살려 줘! 누구 없어요?", pos: v.Pos, key: "scream", value: 1); sim.Sound(SoundKind.Scream, v.Pos, 0.7f, v.Id, v.Id); }
                    else sim.Sound(SoundKind.Struggle, v.Pos, 0.22f, v.Id);
                }
                v.Needs.Fear = 1; v.Emotion = Emotion.Fear;
            }
        }

        static void WorkFree(Simulation sim, Actor v, Binding b)
        {
            var S = sim.S; b.Loose = 1f;
            Unbind(sim, v, v, "스스로 풀었다");
            S.Log("WorkedFree", v.Id, b.By, b.Item, v.Room, v.Pos, b.Material, secret: true);
            // who did it: seen, or only felt
            var binder = S.A(b.By);
            if (binder != null && !v.IsPlayer)
            {
                bool saw = S.K(v.Id).Sightings.Any(s => s.Target == binder.Id && s.IdConf > 0.5f && s.T1 >= b.Clock - 2);
                if (saw) Cases.OnVictimWake(sim, v); else { S.K(v.Id).Facts.Add("attacked-unknown:" + (int)S.Clock); Crime.VictimReact(sim, v, binder); }
            }
        }

        /// <summary>Exec for the bound person's own step: strain, writhe, shuffle. It never ends by itself (the binding does).</summary>
        static bool BoundExec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S; var b = Bound(S, a.Id);
            if (b == null) { sim.NextStepPublic(a); return true; }
            a.Speed = 0; a.Anim = Anim.Struggle;
            if (b.Ankles && a.Pose == Pose.Stand && S.Tick % 40 == 0) a.Pose = Pose.Sit;   // hopping gives out: down on the floor
            return true;
        }

        /// <summary>Someone who comes across a bound person frees them (not the one who tied them, not a schemer mid-plan).</summary>
        public static void OnSighting(Simulation sim, Actor o, Actor t)
        {
            var S = sim.S; if (o.IsPlayer || o.IsButler || o.PlanId != null || o.Status != ActorStatus.Active || o.Pose == Pose.Sleep) return;
            var b = Bound(S, t.Id); if (b == null || !t.Alive || b.By == o.Id || HeldIn(S, t.Id) != null) return;
            if (o.Act != null && (!o.Act.Interruptible || (o.Act.Id?.StartsWith("case:untie") ?? false))) return;
            if (S.Actors.Values.Any(x => x != o && x.Act?.Id == "case:untie:" + t.Id)) return;
            if (o.Pos.f != t.Pos.f || o.Pos.DistXZ(t.Pos) > 14f) return;
            var act = new Activity { Id = "case:untie:" + t.Id, Label = "묶인 사람을 풀어 주는 중", Priority = 140, Interruptible = false };
            act.Steps.Add(new ActionStep { Kind = "GoTo", Target = t.Pos, HasTarget = true, Run = true });
            act.Steps.Add(new ActionStep { Kind = "X_Untie", Actor = t.Id, Duration = 1.2 });
            sim.Assign(o, act);
            S.Log("UntieStart", o.Id, t.Id, room: t.Room, secret: true);
        }

        static bool UntieExec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S; var t = S.A(st.Actor);
            a.Speed = 0; a.Anim = Anim.Use; a.Pose = Pose.Crouch;
            if (t == null || Bound(S, t.Id) == null || t.Pos.DistXZ(a.Pos) > 2.2f) { a.Pose = Pose.Stand; sim.NextStepPublic(a); return true; }
            if (S.Clock < a.Act.StepEnd) return true;
            Unbind(sim, a, t, Cast.GivenOf(a.Id) + "이(가) 풀어 주었다");
            a.Pose = Pose.Stand;
            if (t.Alive) { Relations.Change(S, t.Id, a.Id, trust: 0.35f, like: 0.3f, attach: 0.2f, memory: "묶인 나를 풀어 줬다"); if (!t.IsPlayer && t.Status == ActorStatus.Active) Cases.OnVictimWake(sim, t); }
            sim.NextStepPublic(a); return true;
        }

        // ================================================================== the culprit's side (plan layer "Bind")
        /// <summary>A binding material the planner knows of (not the cord meant for the neck).</summary>
        static Item BindItem(Simulation sim, Actor a, string except)
        {
            var S = sim.S;
            var mine = sim.Carried(a).FirstOrDefault(i => IsBinding(i.Def) && i.Id != except && !i.Surface.Contains("burnt"));
            if (mine != null) return mine;
            var src = SetPieces.Force != null ? S.Items.Values.Where(i => i.Holder == null) : S.K(a.Id).ItemSeen.Keys.Select(S.I).Where(i => i != null && i.Holder == null);
            return src.Where(i => IsBinding(i.Def) && i.Id != except && !i.Surface.Contains("burnt") && !i.Hidden)
                      .Where(i => { var r = S.Layout.Room(i.Room); return r != null && sim.RoomUsable(a, r) && !(r.Type == RoomType.Bedroom && r.Owner != a.Id); })
                      .OrderBy(i => i.Pos.Dist(a.Pos)).ThenBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
        }

        static Activity BindThink(Simulation sim, Actor a, MurderPlan plan, PlanStep st, Activity act)
        {
            var S = sim.S; var t = S.A(plan.Target); var rope = S.I(st.Item);
            if (t == null || !t.Alive) { Crime.Advance(sim, a, plan); return null; }
            if (rope == null || (rope.Holder != null && rope.Holder != a.Id)) { Crime.Advance(sim, a, plan); return null; }   // lost the rope: no binding, go on
            if (st.Kind == "X_Unbind")
            {
                if (Bound(S, t.Id) == null || t.Pos.f != a.Pos.f) { Crime.Advance(sim, a, plan); return null; }
                act.Steps.Add(Simulation.GoTo(sim.SnapPublic(new P3(t.Pos.f, t.Pos.x + 0.6f, t.Pos.z + 0.2f))));
                act.Steps.Add(new ActionStep { Kind = "X_MUnbind", Actor = t.Id, Item = rope.Id });
                act.Interruptible = false; act.Label = "볼일"; return act;
            }
            // X_Bind: only someone who cannot fight back (asleep, drugged, down); an awake target means the layer is dropped
            if (!(t.Status == ActorStatus.Unconscious || t.Pose == Pose.Sleep || Methods.Dozing(t))) { plan.Log.Add($"{ClockFmt.DayHM(S.Clock)} 수정: 깨어 있어서 묶지 못함"); Crime.Advance(sim, a, plan); return null; }
            if (rope.Holder != a.Id) { act.Steps.Add(Simulation.GoTo(sim.SnapPublic(rope.Pos))); act.Steps.Add(new ActionStep { Kind = "PlanPickRope", Item = rope.Id }); }
            act.Steps.Add(Simulation.GoTo(sim.SnapPublic(new P3(t.Pos.f, t.Pos.x + 0.6f, t.Pos.z + 0.2f))));
            act.Steps.Add(new ActionStep { Kind = "X_MBind", Actor = t.Id, Item = rope.Id });
            act.Interruptible = false; act.Label = "볼일"; return act;
        }

        /// <summary>Tying takes time: wrists, then ankles, then the gag. A natural sleeper may wake at the first touch.</summary>
        static bool BindExec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S; var t = S.A(st.Actor); var rope = S.I(st.Item);
            MurderPlan plan = a.PlanId != null && S.Plans.TryGetValue(a.PlanId, out var pp) ? pp : null;
            a.Speed = 0; a.Pose = Pose.Crouch; a.Anim = Anim.Use;
            string key = "binding:" + a.Id;
            if (!S.Flags.TryGetValue(key, out var start))
            {
                S.Flags[key] = start = S.Tick;
                if (t != null && t.Pose == Pose.Sleep && t.Status == ActorStatus.Active && !S.Flags.ContainsKey("drowsy:" + t.Id) && S.R(Stream.Combat).Chance(0.3))
                {
                    S.Flags.Remove(key); a.Pose = Pose.Stand;
                    sim.WakePublic(t); S.Log("BindWoke", a.Id, t.Id, room: t.Room, pos: t.Pos, plan: plan?.Id, secret: true);
                    Crime.VictimReact(sim, t, a);
                    if (plan != null) Crime.Abort(sim, a, plan, "묶으려다 상대가 깨어나서"); else sim.Interrupt(a, 1);
                    return true;
                }
            }
            bool gag = rope != null && (rope.Type == "Tape" || rope.Type == "Scarf" || sim.Carried(a).Any(i => i.Type == "Towel" || i.Type == "Scarf" || i.Type == "Tape"));
            long need = 60 + 45 + (gag ? 25 : 0);
            if (t == null || !t.Alive || rope == null || rope.Holder != a.Id || t.Pos.DistXZ(a.Pos) > 2.2f)
            { S.Flags.Remove(key); a.Pose = Pose.Stand; if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a); return true; }
            if (S.Tick - (long)start < need) return true;
            S.Flags.Remove(key);
            Bind(sim, a, t, rope, true, true, gag, plan != null ? "plan" : null);
            a.Pose = Pose.Stand;
            if (plan != null) { plan.Log.Add($"{ClockFmt.DayHM(S.Clock)} {Cast.GivenOf(t.Id)}을(를) 묶음"); Crime.Advance(sim, a, plan); }
            sim.NextStepPublic(a); return true;
        }

        static bool UnbindExec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S; var t = S.A(st.Actor);
            MurderPlan plan = a.PlanId != null && S.Plans.TryGetValue(a.PlanId, out var pp) ? pp : null;
            a.Speed = 0; a.Pose = Pose.Crouch; a.Anim = Anim.Use;
            string key = "unbinding:" + a.Id;
            if (!S.Flags.TryGetValue(key, out var start)) S.Flags[key] = start = S.Tick;
            if (S.Tick - (long)start < 40) return true;
            S.Flags.Remove(key);
            if (t != null) Unbind(sim, a, t, "범인이 끈을 풀어 챙겼다", keep: true);
            a.Pose = Pose.Stand;
            if (plan != null) Crime.Advance(sim, a, plan);
            sim.NextStepPublic(a); return true;
        }
    }
}
