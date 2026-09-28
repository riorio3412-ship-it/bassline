using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Prolonged, time-true kills (ViolenceMotionContract.md "Prolonged kills"): strangling (bare hands, a cord from behind, a
    /// cord from the front), smothering, and drowning by the hair. Each is an <see cref="Assault"/> that runs through
    /// Seize → Struggle → Weaken → Unconscious → Dead over tens of seconds, with durations drawn from the victim's strength,
    /// surprise, sleep and sedation. The victim can break free, a witness or an intervener can stop it, a loud sound can make
    /// the attacker let go; the struggle claws, bites, knocks things over and makes noise; water sloshes and hair tears out.
    /// The Game reads Assault.Intensity / Phase every frame (paired motion, faces, splashes); the kernel alone decides.
    /// </summary>
    public static partial class Violence
    {
        const int Hz = SimTime.PerSecond;
        static float C01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        // ================================================================== queries (cheap: called per actor per tick)
        /// <summary>The live assault in which this person is the one being held (null if none).</summary>
        public static Assault HeldIn(GameState S, string id)
        {
            var l = S.Violence?.Assaults; if (l == null || id == null) return null;
            for (int i = l.Count - 1; i >= 0; i--) { var x = l[i]; if (x.Active && x.Victim == id) return x; }
            return null;
        }
        /// <summary>The live assault this person is carrying out (null if none).</summary>
        public static Assault DoingTo(GameState S, string id)
        {
            var l = S.Violence?.Assaults; if (l == null || id == null) return null;
            for (int i = l.Count - 1; i >= 0; i--) { var x = l[i]; if (x.Active && x.Attacker == id) return x; }
            return null;
        }
        public static IEnumerable<Assault> Live(GameState S) => S.Violence?.Assaults.Where(x => x.Active) ?? Enumerable.Empty<Assault>();

        /// <summary>Movement hook (Simulation.Execute): a person held in an assault does nothing of their own.</summary>
        public static bool Holds(Simulation sim, Actor a)
        {
            var x = HeldIn(sim.S, a.Id); if (x == null) return false;
            a.Speed = 0; a.Running = false;
            return true;
        }

        /// <summary>Physical strength 0..1: height, build, shoulders, sex, temper — cut by injuries and limp hands.</summary>
        public static float Strength(Actor a)
        {
            var d = a?.Def; if (d == null) return 0.5f;
            float h = C01((d.HeightCm - 150) / 45f);
            float s = 0.28f + 0.3f * h + 0.18f * d.Look.Build + 0.12f * d.Look.Shoulders + (d.Gender == Gender.M ? 0.08f : 0f) + 0.08f * d.P.Aggression;
            var b = a.Body;
            s *= 0.35f + 0.65f * Math.Min(b.Mobility, b.Resistance);
            s *= 0.5f + 0.5f * Math.Max(b.HandL, b.HandR);
            return MathX.Clamp(s, 0.08f, 1f);
        }

        static void Note(Assault x, GameState S, string what) { x.Log.Add($"{x.Seconds(S.Tick),5:0.0}s {what}"); if (x.Log.Count > 60) x.Log.RemoveAt(0); }

        // ================================================================== water (drowning)
        static readonly string[] WaterTypes = { "PoolWater", "ShallowWater", "Sink", "Bathtub", "Basin", "Well", "Fountain" };
        /// <summary>Water deep enough to hold a head under, nearest the point (same floor, within reach). The courtyard fountain is
        /// dry ("마른 분수") unless the layout marks it wet.</summary>
        public static Furniture WaterNear(GameState S, P3 p, float within)
        {
            Furniture best = null; float bd = within;
            foreach (var f in S.Layout.Furniture)
            {
                if (f.Pos.f != p.f || Array.IndexOf(WaterTypes, f.Type) < 0) continue;
                if (f.Type == "Fountain" && !f.Marks.Contains("water")) continue;
                float d = RimDist(f, p);
                if (d < bd || (best != null && d == bd && f.Id < best.Id)) { bd = d; best = f; }
            }
            return best;
        }
        static float RimDist(Furniture f, P3 p)
        {
            NavGrid.GetFootprint(f, 0f, out var r);
            float dx = Math.Max(Math.Max(r.x0 - p.x, 0), p.x - r.x1), dz = Math.Max(Math.Max(r.z0 - p.z, 0), p.z - r.z1);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
        /// <summary>The nearest point on the water's rim and the outward direction there.</summary>
        static void Rim(Furniture f, P3 p, out P3 rim, out float nx, out float nz)
        {
            NavGrid.GetFootprint(f, 0f, out var r);
            float qx = MathX.Clamp(p.x, r.x0, r.x1), qz = MathX.Clamp(p.z, r.z0, r.z1);
            bool inside = p.x > r.x0 && p.x < r.x1 && p.z > r.z0 && p.z < r.z1;
            if (!inside)
            {
                float dx = p.x - qx, dz = p.z - qz, l = (float)Math.Sqrt(dx * dx + dz * dz);
                if (l < 1e-3f) { dx = 0; dz = -1; l = 1; }
                rim = new P3(p.f, qx, qz); nx = dx / l; nz = dz / l; return;
            }
            // inside (swimming): out through the nearest side
            float l0 = p.x - r.x0, l1 = r.x1 - p.x, l2 = p.z - r.z0, l3 = r.z1 - p.z, m = Math.Min(Math.Min(l0, l1), Math.Min(l2, l3));
            if (m == l0) { rim = new P3(p.f, r.x0, p.z); nx = -1; nz = 0; }
            else if (m == l1) { rim = new P3(p.f, r.x1, p.z); nx = 1; nz = 0; }
            else if (m == l2) { rim = new P3(p.f, p.x, r.z0); nx = 0; nz = -1; }
            else { rim = new P3(p.f, p.x, r.z1); nx = 0; nz = 1; }
        }

        // ================================================================== the attack step (Methods.Attack hook)
        /// <summary>Kernel attack modes owned by the violence track (Crime.Think maps plan notes to them).</summary>
        public static bool IsMode(string m) => m == "throttle" || m == "garrote" || m == "drown" || m == "shoot";
        static bool Takes(string m) => m == "strangle" || m == "smother" || IsMode(m);

        /// <summary>Crime.Attack hook, before the mode dispatch: a firearm in hand is fired (not swung), an unarmed killer
        /// who can overpower the victim strangles with bare hands instead of punching.</summary>
        public static void RetagAttack(Simulation sim, Actor a, Actor t, ActionStep st, Item weapon)
        {
            if (st == null || st.Tag != "kill" || t == null) return;
            if (weapon != null && IsRanged(weapon.Def) && (Gun(sim.S, weapon.Id).Loaded > 0 || AmmoFor(sim, a, weapon) != null)) { st.Tag = "shoot"; return; }
            if (weapon == null && !t.IsPlayer && t.Status == ActorStatus.Active && Strength(a) >= Strength(t) * 0.85f) st.Tag = "throttle";
        }

        /// <summary>Methods.Attack hook. Returns true when the violence track owns this mode (strangle / garrote / throttle /
        /// smother / drown / shoot): approach, seize, and — once the assault has begun — nothing more here (Violence.Tick runs it).</summary>
        public static bool Attack(Simulation sim, Actor a, Actor t, ActionStep st, MurderPlan plan, Item weapon, Rng crng)
        {
            string mode = st.Tag; if (!Takes(mode)) return false;
            if (mode == "shoot") { ShootStep(sim, a, t, st, plan, weapon); return true; }
            var S = sim.S;
            var mine = DoingTo(S, a.Id);
            if (mine != null) { a.Speed = 0; return true; }          // holding on: Violence.Tick drives it
            if (HeldIn(S, t.Id) != null) { a.Speed = 0; return true; }   // someone else has them
            if (t.IsPlayer) { st.Tag = "kill"; return true; }          // the player's body is theirs to move: an ordinary attack
            AssaultKind kind; Item tool = null; Furniture water = null;
            switch (mode)
            {
                case "smother":
                    if (!Methods.Dozing(t) && t.Status == ActorStatus.Active && t.Pose != Pose.Sleep)
                    { if (plan != null) { plan.Log.Add($"{ClockFmt.DayHM(S.Clock)} 수정: 상대가 깨어 있음"); Crime.Replan(sim, a, plan, "target woke"); } sim.Interrupt(a, 3); return true; }
                    kind = AssaultKind.Smother; break;
                case "drown":
                    water = WaterNear(S, t.Pos, 7f);
                    if (water == null) { st.Tag = "throttle"; return true; }   // no water within reach: hands on the throat
                    kind = AssaultKind.Drown; break;
                case "throttle": kind = AssaultKind.StrangleManual; break;
                default:
                    tool = weapon != null && Methods.IsCord(weapon.Def) ? weapon : sim.Carried(a).FirstOrDefault(i => Methods.IsCord(i.Def));
                    kind = tool == null ? AssaultKind.StrangleManual : AssaultKind.StrangleRear; break;
            }
            // --- approach: from behind when a cord is used on someone standing still; to the rim when drowning
            float d = a.Pos.Dist(t.Pos);
            float reach = kind == AssaultKind.Smother ? 1.1f : kind == AssaultKind.Drown ? 1.2f : 0.95f;
            // at the end of the path but the body lies across a bed (or the goal snapped short): lean in over it
            bool arrived = a.Act.Path != null && a.Act.PathIdx >= a.Act.Path.Count;
            if (arrived && d <= reach + (kind == AssaultKind.Smother || t.Spot >= 0 ? 1.0f : 0.5f)) d = reach;
            if (d > reach)
            {
                if (d > 25f || S.Clock - a.Act.StepStart > 6) { if (plan != null) Crime.Replan(sim, a, plan, "lost target"); sim.Interrupt(a); return true; }
                P3 goal = t.Pos;
                if (tool != null && t.Speed < 0.2f) { double yr = t.Yaw * Math.PI / 180; goal = sim.SnapPublic(new P3(t.Pos.f, t.Pos.x - (float)Math.Sin(yr) * 0.55f, t.Pos.z - (float)Math.Cos(yr) * 0.55f)); }
                if (a.Act.Path == null || S.Tick % 6 == 0)
                {
                    var pr = Pathfinder.Find(S.Layout, a.Pos, goal, sim.DoorCostFor(a)); if (!pr.Ok) { sim.Interrupt(a); return true; }
                    a.Act.Path = pr.Points; a.Act.PathDoors = pr.DoorAtPoint; a.Act.PathStairs = pr.StairAtPoint; a.Act.PathIdx = 1;
                }
                if (!S.Flags.ContainsKey("approach:" + a.Id + ":" + t.Id)) S.Flags["approach:" + a.Id + ":" + t.Id] = S.Tick;
                sim.MoveAlongPublic(a, kind != AssaultKind.Smother && d > 3f); return true;
            }
            if (kind == AssaultKind.StrangleRear && !Behind(a, t)) kind = AssaultKind.StrangleFront;   // turned round: the cord goes on from the front
            Begin(sim, a, t, kind, tool, plan, water);
            return true;
        }

        static bool Behind(Actor a, Actor t)
        {
            float bearing = MathX.AngleDeg(a.Pos.x - t.Pos.x, a.Pos.z - t.Pos.z);   // from the victim to the attacker
            return Math.Abs(MathX.DeltaAngle(t.Yaw, bearing)) > 115f || t.Pose == Pose.Sit && Math.Abs(MathX.DeltaAngle(t.Yaw, bearing)) > 90f;
        }

        // ================================================================== begin
        /// <summary>Seize: the hold begins. Works for NPC culprits (from their Attack step), the player (Interaction) and tests.
        /// Returns null if it cannot start (dead / already held / the player as victim).</summary>
        public static Assault Begin(Simulation sim, Actor a, Actor t, AssaultKind kind, Item tool, MurderPlan plan, Furniture water = null)
        {
            var S = sim.S;
            if (a == null || t == null || a == t || !a.Alive || !t.Alive || t.IsPlayer || t.CarriedBy != null) return null;
            if (HeldIn(S, t.Id) != null || DoingTo(S, a.Id) != null || HeldIn(S, a.Id) != null) return null;
            if (kind == AssaultKind.Drown && water == null) water = WaterNear(S, t.Pos, 7f);
            if (kind == AssaultKind.Drown && water == null) kind = AssaultKind.StrangleManual;
            var V = S.Violence;
            var x = new Assault
            {
                Id = "as" + (++V.Next), Kind = kind, Phase = AssaultPhase.Seize, Attacker = a.Id, Victim = t.Id, Plan = plan?.Id, Tool = tool?.Id,
                Begun = S.Tick, PhaseAt = S.Tick, From = t.Pos,
                R = new Rng(S.Rng.CampaignSeed ^ Rng.Hash("assault#" + S.Loop + "#" + V.Next + "#" + t.Id), 0x61737361UL + (ulong)V.Next)
            };
            if (S.Flags.TryGetValue("approach:" + a.Id + ":" + t.Id, out var ap)) { x.Approach = (long)ap; S.Flags.Remove("approach:" + a.Id + ":" + t.Id); }
            var r = x.R;
            x.VStr = Strength(t); x.AStr = Strength(a);
            x.Asleep = t.Pose == Pose.Sleep;
            x.Sedated = t.Status == ActorStatus.Unconscious || S.Flags.ContainsKey("drowsy:" + t.Id) || (S.Flags.TryGetValue("sedated:" + t.Id, out var sd) && S.Clock - sd < 240);
            x.Bound = Bound(S, t.Id) is Binding bd && bd.Wrists;
            float face = Math.Abs(MathX.DeltaAngle(t.Yaw, MathX.AngleDeg(a.Pos.x - t.Pos.x, a.Pos.z - t.Pos.z)));
            x.Surprise = x.Asleep || x.Sedated || face > 100f || kind == AssaultKind.StrangleRear;
            x.Seated = t.Pose == Pose.Sit && (kind == AssaultKind.StrangleRear || kind == AssaultKind.StrangleFront || kind == AssaultKind.StrangleManual);
            if (kind == AssaultKind.Drown) { x.Water = water.Id; x.WaterType = water.Type; }
            Place(sim, x, a, t, water);
            PlanTimes(x);
            // --- the first touch
            if (!S.Flags.ContainsKey("attackstart:" + a.Id + ":" + t.Id)) { S.Flags["attackstart:" + a.Id + ":" + t.Id] = S.Clock; Incidents.OnAttackBegin(sim, a, t, plan); }
            S.Log("AssaultBegin", a.Id, t.Id, tool?.Id, t.Room, t.Pos, $"{kind}|str={x.StruggleT / (float)Hz:0.0}s|v={x.VStr:0.00}|a={x.AStr:0.00}{(x.Surprise ? "|surprise" : "")}{(x.Asleep ? "|asleep" : "")}{(x.Sedated ? "|sedated" : "")}{(x.Bound ? "|bound" : "")}", plan?.Id, true);
            switch (kind)
            {
                case AssaultKind.StrangleRear: case AssaultKind.StrangleFront:
                    S.Log("Garrote", a.Id, t.Id, tool?.Id, t.Room, t.Pos, (tool != null ? S.I(tool.Id)?.Type : "hands") + (kind == AssaultKind.StrangleFront ? "|front" : ""), plan?.Id, true);
                    if (tool != null && !tool.Surface.Contains("stretched")) tool.Surface.Add("stretched");
                    break;
                case AssaultKind.StrangleManual:
                    S.Log("Throttle", a.Id, t.Id, null, t.Room, t.Pos, x.Seated ? "seated" : "standing", plan?.Id, true);
                    break;
                case AssaultKind.Smother:
                    {
                        var room = S.Layout.Room(t.Room); bool bedroom = room?.Type == RoomType.Bedroom && room.Owner == t.Id;
                        var pillow = new Item { Id = S.NewId("it"), Type = "Pillow", Name = bedroom ? Cast.GivenOf(t.Id) + " 침대의 베개" : "소파 쿠션", Pos = new P3(t.Pos.f, t.Pos.x + 0.35f, t.Pos.z + 0.15f), Room = t.Room, HomeRoom = t.Room, HomePos = t.Pos };
                        pillow.Surface.Add("pressed"); S.Items[pillow.Id] = pillow; S.Emit(GameEventType.ItemMoved, a.Id, data: pillow.Id, text: "spawn", pos: pillow.Pos);
                        x.Tool = pillow.Id; S.Flags["pillow:" + t.Id] = S.Clock;
                        S.Log("Smother", a.Id, t.Id, pillow.Id, t.Room, t.Pos, bedroom ? "bed" : "cushion", plan?.Id, true);
                        break;
                    }
                case AssaultKind.Drown:
                    {
                        Methods.OnDrown(sim, a, t);   // soaked clothes, wet sleeves and a trail on the one who holds them under (HeldUnder)
                        // a fist in the hair: a clump tears out at the rim, the scalp is bruised
                        Mark(sim, a, t, BodyRegion.Head, DamageType.Blunt, 1, null, "hair");
                        var hair = new Item { Id = S.NewId("it"), Type = "HairStrands", Name = Cast.GivenOf(t.Id) + "의 머리카락 한 움큼", Owner = t.Id, Pos = new P3(x.At.f, x.At.x + r.Range(-0.3f, 0.3f), x.At.z + r.Range(-0.3f, 0.3f)), Room = S.Layout.RoomAt(x.At) >= 0 ? S.Layout.RoomAt(x.At) : t.Room };
                        hair.HomeRoom = hair.Room; hair.HomePos = hair.Pos; hair.Surface.Add("torn"); S.Items[hair.Id] = hair; S.Emit(GameEventType.ItemMoved, a.Id, data: hair.Id, text: "spawn", pos: hair.Pos);
                        S.Flags["hairhand:" + a.Id] = S.Clock;
                        break;
                    }
            }
            // --- both bodies: the victim stops whatever they were doing (the Movement hook keeps them still)
            t.TalkingTo = null; t.Following = null; t.NextThink = S.Clock + 9999; t.Speed = 0;
            if (t.Pose == Pose.Sleep && kind != AssaultKind.Smother) t.Pose = Pose.LieBack;
            t.Anim = Anim.Struggle; t.Emotion = x.Asleep || x.Sedated ? Emotion.Pain : Emotion.Surprised;
            a.Anim = Anim.Strangle; a.Speed = 0; a.Running = false; if (a.Act != null) a.Act.Path = null;
            if (kind == AssaultKind.Drown) { t.Pose = Pose.Kneel; a.Pose = Pose.Crouch; }
            if (kind == AssaultKind.Smother) a.Pose = Pose.Stand;
            if (x.Asleep && kind == AssaultKind.Smother && !x.Sedated) { t.Pose = Pose.LieBack; Note(x, S, "the sleeper wakes under the pillow"); }
            Note(x, S, $"seize ({kind}{(x.Tool != null ? " with " + (S.I(x.Tool)?.Type ?? "?") : "")}; victim strength {x.VStr:0.00}, attacker {x.AStr:0.00}{(x.Surprise ? ", surprise" : "")}{(x.Asleep ? ", asleep" : "")}{(x.Sedated ? ", sedated" : "")}{(x.Bound ? ", bound" : "")}) — struggle planned {x.StruggleT / (float)Hz:0.0}s, weaken {x.WeakenT / (float)Hz:0.0}s, hold {x.HoldT / (float)Hz:0.0}s");
            if (x.Approach >= 0) x.Log.Insert(0, $"{(x.Approach - x.Begun) / (float)Hz,5:0.0}s approach");
            V.Assaults.Add(x);
            Emit(S, x);
            sim.Sound(SoundKind.Struggle, t.Pos, x.Asleep || x.Sedated ? 0.18f : 0.34f, a.Id);
            if (a.IsPlayer) sim.EmergencyTrigger(4);
            return x;
        }

        /// <summary>Where both bodies are during the hold (the Game's paired animation aligns them further).</summary>
        static void Place(Simulation sim, Assault x, Actor a, Actor t, Furniture water)
        {
            var S = sim.S;
            float bearing = MathX.AngleDeg(t.Pos.x - a.Pos.x, t.Pos.z - a.Pos.z);   // attacker → victim
            switch (x.Kind)
            {
                case AssaultKind.StrangleRear:
                    {
                        x.At = t.Pos; x.Yaw = t.Yaw;
                        double yr = t.Yaw * Math.PI / 180;
                        x.AttAt = new P3(t.Pos.f, t.Pos.x - (float)Math.Sin(yr) * (x.Seated ? 0.5f : 0.38f), t.Pos.z - (float)Math.Cos(yr) * (x.Seated ? 0.5f : 0.38f));
                        x.AttYaw = t.Yaw; break;
                    }
                case AssaultKind.StrangleFront: case AssaultKind.StrangleManual:
                    {
                        x.At = t.Pos; x.Yaw = x.Seated ? t.Yaw : Wrap(bearing + 180f);
                        double yr = (x.Seated ? t.Yaw : bearing + 180f) * Math.PI / 180;
                        float gap = x.Seated ? 0.55f : 0.5f;
                        x.AttAt = new P3(t.Pos.f, t.Pos.x + (float)Math.Sin(yr) * gap, t.Pos.z + (float)Math.Cos(yr) * gap);
                        x.AttYaw = Wrap((float)(yr * 180 / Math.PI) + 180f); break;
                    }
                case AssaultKind.Smother:
                    x.At = t.Pos; x.Yaw = t.Yaw; x.AttAt = a.Pos; x.AttYaw = bearing; break;
                case AssaultKind.Drown:
                    {
                        Rim(water, t.Pos, out var rim, out float nx, out float nz);
                        bool basin = water.Type == "Sink" || water.Type == "Basin";
                        float kneel = basin ? 0.45f : 0.32f, behind = basin ? 0.95f : 0.82f;
                        x.At = new P3(rim.f, rim.x + nx * kneel, rim.z + nz * kneel);
                        x.AttAt = new P3(rim.f, rim.x + nx * behind, rim.z + nz * behind);
                        x.Yaw = MathX.AngleDeg(-nx, -nz); x.AttYaw = x.Yaw; break;
                    }
            }
        }
        static float Wrap(float a) { a %= 360f; if (a > 180f) a -= 360f; if (a < -180f) a += 360f; return a; }

        /// <summary>Phase lengths. Struggle: tens of seconds, longer for the strong, shorter for the surprised, the sleeping,
        /// the drugged and the bound. Then the legs go; then limp; then — only if the hold is kept up — death.</summary>
        static void PlanTimes(Assault x)
        {
            var r = x.R;
            float baseS = x.Kind == AssaultKind.StrangleRear ? 16f : x.Kind == AssaultKind.StrangleFront ? 19f : x.Kind == AssaultKind.StrangleManual ? 24f : x.Kind == AssaultKind.Smother ? 13f : 28f;
            float k = (0.6f + 0.8f * x.VStr) * (x.Surprise ? 0.8f : 1.15f) * (x.Asleep ? 0.85f : 1f) * (x.Sedated ? 0.3f : 1f) * (x.Bound ? 0.6f : 1f) / (0.7f + 0.6f * x.AStr);
            float s = MathX.Clamp(baseS * k * r.Range(0.85f, 1.15f), x.Sedated ? 4f : 8f, 45f);
            x.StruggleT = (int)Math.Round(s * Hz);
            x.WeakenT = (int)Math.Round((x.Kind == AssaultKind.Drown ? r.Range(8f, 12f) : r.Range(5f, 9f)) * (x.Sedated ? 0.6f : 1f) * Hz);
            x.HoldT = (int)Math.Round((x.Kind == AssaultKind.Drown ? r.Range(15f, 25f) : x.Kind == AssaultKind.Smother ? r.Range(20f, 35f) : r.Range(25f, 45f)) * Hz);
            float drag = x.Kind == AssaultKind.Drown ? x.From.DistXZ(x.At) : 0f;
            x.SeizeT = (int)Math.Round((x.Kind == AssaultKind.Drown ? 0.9f + Math.Min(5f, drag / 0.8f) : x.Kind == AssaultKind.Smother ? 0.6f : 0.8f) * Hz);
        }

        // ================================================================== per tick
        public static void Tick(Simulation sim)
        {
            var S = sim.S; var V = S.Violence; if (V == null || S.Layout == null) return;
            for (int i = 0; i < V.Assaults.Count; i++) { var x = V.Assaults[i]; if (x.Active) TickAssault(sim, x); }
            if (V.Assaults.Count > 48) V.Assaults.RemoveAll(x => !x.Active && S.Tick - x.Ended > 6000 && V.Assaults.IndexOf(x) < V.Assaults.Count - 24);
            TickBindings(sim);
            DragMarks(sim);
            if (S.Tick % 30 == 11) Observations(sim);
        }

        static void TickAssault(Simulation sim, Assault x)
        {
            var S = sim.S; var a = S.A(x.Attacker); var v = S.A(x.Victim);
            if (v == null || a == null) { x.Phase = AssaultPhase.Released; x.Ended = S.Tick; x.Outcome = "gone"; return; }
            if (!v.Alive) { Finish(sim, x, a, v, AssaultPhase.Dead, "died"); return; }
            long age = S.Tick - x.PhaseAt;
            // --- the attacker stopped (hit, fled, lost consciousness, moved off)
            if (!a.Alive || a.Status != ActorStatus.Active) { Release(sim, x, "공격자가 쓰러졌다"); return; }
            if (!a.IsPlayer && (a.Act == null || a.Act.Cur == null || a.Act.Cur.Kind != "Attack" || a.Act.Cur.Actor != v.Id)) { Release(sim, x, "공격을 멈췄다"); return; }
            if (a.IsPlayer)
            {
                // the player drags the held body along; letting go is walking off (or the key)
                if (a.Pos.f != x.AttAt.f || a.Pos.DistXZ(x.AttAt) > 1.6f) { Release(sim, x, "손을 놓았다"); return; }
                float dx = a.Pos.x - x.AttAt.x, dz = a.Pos.z - x.AttAt.z;
                if (x.Phase != AssaultPhase.Unconscious && (Math.Abs(dx) > 0.02f || Math.Abs(dz) > 0.02f)) { x.At = new P3(x.At.f, x.At.x + dx, x.At.z + dz); x.AttAt = a.Pos; }
            }
            if (S.Tick % 5 == (x.Begun % 5) && Interrupted(sim, x, a, v)) return;
            Hold(sim, x, a, v);
            switch (x.Phase)
            {
                case AssaultPhase.Seize:
                    x.Intensity = x.Sedated ? 0.35f : 0.85f;
                    if (x.Kind == AssaultKind.Drown && x.SeizeT > 0) { float k = Math.Min(1f, age / (float)x.SeizeT); v.Pos = P3.Lerp(x.From, x.At, k); }
                    if (age >= x.SeizeT) SetPhase(sim, x, AssaultPhase.Struggle, a, v);
                    break;
                case AssaultPhase.Struggle:
                    {
                        float k = Math.Min(1f, age / (float)Math.Max(1, x.StruggleT));
                        x.Air = 1f - 0.72f * k; x.Fight = 1f - 0.45f * k;
                        float pulse = Pulse(x, S.Tick);
                        x.Intensity = (x.Sedated ? 0.25f : x.Bound ? 0.45f : 0.55f) + (x.Sedated ? 0.15f : 0.45f) * pulse * x.Fight;
                        if ((S.Tick - x.Begun) % Hz == 0) Second(sim, x, a, v, true);
                        if (x.Phase == AssaultPhase.Struggle && age >= x.StruggleT) SetPhase(sim, x, AssaultPhase.Weaken, a, v);
                        break;
                    }
                case AssaultPhase.Weaken:
                    {
                        float k = Math.Min(1f, age / (float)Math.Max(1, x.WeakenT));
                        x.Air = 0.28f * (1f - k); x.Fight = 0.55f * (1f - k);
                        x.Intensity = 0.05f + 0.45f * (1f - k) * (0.55f + 0.45f * Pulse(x, S.Tick));
                        if ((S.Tick - x.Begun) % Hz == 0) Second(sim, x, a, v, false);
                        if (age >= x.WeakenT) SetPhase(sim, x, AssaultPhase.Unconscious, a, v);
                        break;
                    }
                case AssaultPhase.Unconscious:
                    x.Air = 0; x.Fight = 0;
                    x.Intensity = age < 25 && (age % 9) < 2 ? 0.08f : 0.01f;   // a last twitch, then limp
                    if (age >= x.HoldT) Kill(sim, x, a, v);
                    break;
            }
        }

        /// <summary>A victim's thrashing is bursts, not a constant: a deterministic sum of beats per assault.</summary>
        static float Pulse(Assault x, long tick)
        {
            double t = (tick - x.Begun) / (double)Hz; uint h = (uint)Rng.Hash(x.Id);
            double p1 = (h & 255) / 40.0, p2 = ((h >> 8) & 255) / 50.0;
            double v = 0.5 + 0.28 * Math.Sin(t * 5.1 + p1) + 0.17 * Math.Sin(t * 11.3 + p2) + 0.12 * Math.Sin(t * 1.7 + p1 * 0.5);
            return (float)Math.Max(0, Math.Min(1, v));
        }

        static void Hold(Simulation sim, Assault x, Actor a, Actor v)
        {
            var S = sim.S;
            if (x.Phase == AssaultPhase.Unconscious) { v.Speed = 0; return; }   // the body lies where it fell
            if (x.Phase != AssaultPhase.Seize || x.Kind != AssaultKind.Drown) v.Pos = x.At;
            v.Yaw = x.Yaw; v.Speed = 0; v.Running = false; v.StairId = -1;
            int vr = S.Layout.RoomAt(v.Pos); if (vr >= 0 && vr != v.Room) sim.UpdateRoom(v);
            if (!a.IsPlayer) { a.Pos = x.AttAt; a.Yaw = x.AttYaw; a.Speed = 0; a.Running = false; int ar = S.Layout.RoomAt(a.Pos); if (ar >= 0 && ar != a.Room) sim.UpdateRoom(a); }
            a.Anim = Anim.Strangle; v.Anim = Anim.Struggle;
            if (a.Act != null) a.Act.Path = null;
            a.BusyUntil = Math.Max(a.BusyUntil, S.Tick + 2);
        }

        static void SetPhase(Simulation sim, Assault x, AssaultPhase p, Actor a, Actor v)
        {
            var S = sim.S; x.Phase = p; x.PhaseAt = S.Tick;
            switch (p)
            {
                case AssaultPhase.Struggle:
                    v.Emotion = Emotion.Fear;
                    Note(x, S, x.Sedated ? "struggle (drugged: feeble)" : x.Bound ? "struggle (bound: can only writhe)" : "struggle — clawing at the " + (x.Kind == AssaultKind.Drown ? "hand in the hair" : x.Kind == AssaultKind.Smother ? "arms on the pillow" : "arms and the throat") + ", kicking");
                    break;
                case AssaultPhase.Weaken:
                    v.Emotion = Emotion.Pain;
                    if (!x.Seated && (x.Kind == AssaultKind.StrangleFront || x.Kind == AssaultKind.StrangleManual || x.Kind == AssaultKind.StrangleRear)) { v.Pose = Pose.Kneel; a.Pose = x.Kind == AssaultKind.StrangleRear ? Pose.Crouch : Pose.Stand; }
                    Note(x, S, "weaken — the legs give, the hands slip");
                    break;
                case AssaultPhase.Unconscious:
                    {
                        v.Emotion = Emotion.Blank;
                        var keep = v.Pose;
                        sim.Collapse(v, x.Kind == AssaultKind.Drown ? "물속에서 정신을 잃음" : "숨이 막혀 정신을 잃음");
                        if (x.Kind == AssaultKind.Smother) v.Pose = keep == Pose.Sleep || keep == Pose.LieBack ? Pose.LieBack : v.Pose;
                        if (x.Kind == AssaultKind.Drown) v.Pose = Pose.LieFront;
                        v.Body.UnconsciousUntil = S.Clock + 9999;
                        a.Pose = x.Kind == AssaultKind.Smother ? Pose.Stand : Pose.Kneel;
                        S.Log("AssaultUnconscious", a.Id, v.Id, x.Tool, v.Room, v.Pos, x.Kind.ToString(), x.Plan, true);
                        Note(x, S, "unconscious — limp; the hold goes on");
                        break;
                    }
            }
            Emit(S, x);
        }

        static void Emit(GameState S, Assault x) => S.Emit(GameEventType.Anim, x.Attacker, x.Victim, text: x.Kind.ToString(), data: "assault|" + x.Id + "|" + x.Phase, pos: S.A(x.Victim)?.Pos, value: x.Intensity);

        /// <summary>Once a second: noise, clawing, biting, knocking things over, water — and the chance to break free.</summary>
        static void Second(Simulation sim, Assault x, Actor a, Actor v, bool struggling)
        {
            var S = sim.S; var r = x.R;
            // --- noise others can hear
            float every = x.Kind == AssaultKind.Drown ? 1.8f : x.Kind == AssaultKind.Smother ? 3f : 2.4f;
            if (!struggling) every *= 2f;
            if (r.F() < 1f / every)
            {
                if (x.Kind == AssaultKind.Drown)
                {
                    x.Splashes++; sim.Sound(SoundKind.Splash, v.Pos, (struggling ? 0.36f : 0.22f) + 0.18f * x.Intensity, a.Id);
                    if (x.Splashes == 2 || x.Splashes == 6) sim.AddTrace("Water", new P3(v.Pos.f, v.Pos.x + r.Range(-0.6f, 0.6f), v.Pos.z + r.Range(-0.6f, 0.6f)), v.Room, a.Id, v.Id, 0.45f + 0.1f * x.Splashes, 0, "물가 바닥에 넓게 튄 물", "이 자리에서 물이 크게 튀었다 — 누군가 물가에서 몸부림쳤다", "누가, 왜");
                    a.Wet = true; a.WetUntil = Math.Max(a.WetUntil, S.Clock + 40); S.Flags["wetsleeve:" + a.Id] = S.Clock;
                }
                else { x.Noises++; sim.Sound(SoundKind.Struggle, v.Pos, x.Kind == AssaultKind.Smother ? 0.16f + 0.1f * x.Intensity : 0.24f + 0.16f * x.Intensity, a.Id); }
            }
            if (!struggling) return;
            bool hands = !x.Bound && !x.Sedated;
            // --- the fight in the ledger (Gore reads it to topple furniture at the scene)
            if (hands && x.Intensity > 0.62f && x.Resists < 4 && r.Chance(0.35)) { x.Resists++; S.Log("Resist", v.Id, a.Id, room: v.Room, pos: v.Pos, data: x.Kind.ToString(), secret: true); }
            // --- nails and teeth
            if (hands && r.Chance(x.Kind == AssaultKind.Drown ? 0.05 : 0.08) && x.Scratches == 0)
            {
                x.Scratches++; var region = x.Kind == AssaultKind.StrangleRear || x.Kind == AssaultKind.Drown ? (r.Chance(0.6) ? BodyRegion.HandL : BodyRegion.ArmR) : (r.Chance(0.5) ? BodyRegion.Neck : BodyRegion.ArmL);
                Scratch(sim, v, a, region); Note(x, S, "nails rake the attacker's " + WoundText.Region(region));
            }
            if (hands && x.Bites == 0 && (x.Kind == AssaultKind.StrangleManual || x.Kind == AssaultKind.Smother || x.Kind == AssaultKind.StrangleFront) && r.Chance(0.03))
            {
                x.Bites++; var region = r.Chance(0.6) ? BodyRegion.HandR : BodyRegion.ArmL;
                Mark(sim, v, a, region, DamageType.Cut, 1, null, "bite");
                S.Flags["bite:" + a.Id] = S.Clock; S.Flags["bitten-by:" + v.Id] = S.Clock;
                S.Log("Bitten", a.Id, v.Id, room: a.Room, pos: a.Pos, data: region.ToString(), secret: true);
                Note(x, S, "teeth sink into the attacker's " + WoundText.Region(region)); x.Grip = Math.Max(0.55f, x.Grip - 0.25f);
            }
            // --- things within reach go flying
            if (hands && x.Knocks < 3 && r.Chance(0.05 + 0.05 * x.VStr * x.Intensity)) { if (Knock(sim, x, a, v)) { x.Knocks++; Note(x, S, "something is knocked over"); } }
            // --- breaking free
            float diff = x.VStr * x.Fight - x.AStr * x.Grip * (x.Kind == AssaultKind.StrangleRear ? 1.25f : x.Kind == AssaultKind.StrangleManual ? 0.85f : x.Kind == AssaultKind.Drown ? 1.1f : 1f);
            double p = 0.004 + 0.06 * Math.Max(0, diff);
            if (x.Surprise) p *= 0.5; if (x.Sedated || x.Bound) p = 0; if (x.Asleep && S.Tick - x.PhaseAt < 20) p *= 0.3; if (a.IsPlayer) p *= 0.6;
            x.Grip = Math.Min(1f, x.Grip + 0.03f);
            if (r.Chance(p)) Release(sim, x, "뿌리치고 빠져나왔다", escaped: true);
        }

        /// <summary>Witnesses, interveners, a startling sound. Returns true if the hold ended.</summary>
        static bool Interrupted(Simulation sim, Assault x, Actor a, Actor v)
        {
            var S = sim.S;
            // someone pulls the attacker off
            foreach (var o in S.Actors.Values)
            {
                if (o == a || o == v || !o.Alive || o.Status != ActorStatus.Active || o.Pose == Pose.Sleep || o.Pos.f != a.Pos.f || o.IsPlayer) continue;
                if (o.Pos.DistXZ(a.Pos) > 1.6f || o.Act == null || o.Act.Id != "case:intervene") continue;
                sim.Strike(o.Id, a, BodyRegion.ArmR, DamageType.Blunt, 1, null, "intervene");
                Release(sim, x, Cast.GivenOf(o.Id) + "이(가) 떼어 놓았다", witness: true); return true;
            }
            if (a.IsPlayer) return false;
            // a witness: early on the attacker lets go and runs; late, a cold one finishes quickly
            var wit = Crime.VisibleOthers(sim, a, v).Where(w => w.Pos.DistXZ(v.Pos) < 12f && w.Status == ActorStatus.Active).ToList();
            if (wit.Count > 0 && sim.RoomLight(v.Room) > 0.2f)
            {
                if (x.Phase <= AssaultPhase.Struggle || wit.Any(w => w.Pos.DistXZ(v.Pos) < 6f) || a.Def.P.Morality >= 0.3f)
                { Release(sim, x, "목격자가 나타났다 — " + Cast.GivenOf(wit[0].Id), witness: true); return true; }
                if (x.Phase == AssaultPhase.Unconscious) x.HoldT = (int)Math.Min(x.HoldT, S.Tick - x.PhaseAt + 30);
            }
            // a loud sound nearby (not their own struggle): a startled attacker may let go
            var k = S.K(a.Id);
            if (k.Heard.Count > 0)
            {
                var h = k.Heard[k.Heard.Count - 1];
                bool fresh = S.Clock - h.Clock < 0.05 && h.Loud > 0.45f && (h.Kind == SoundKind.Scream || h.Kind == SoundKind.Crash || h.Kind == SoundKind.GlassBreak || h.Kind == SoundKind.Knock || h.Kind == SoundKind.DoorSlam || h.Kind == SoundKind.Gunshot || h.Kind == SoundKind.Bell || h.Kind == SoundKind.Announcement);
                string key = "startle-at:" + a.Id; bool once = !S.Flags.TryGetValue(key, out var last) || S.Clock - last > 1;
                if (fresh && once) { S.Flags[key] = S.Clock; if (x.Phase <= AssaultPhase.Weaken && x.R.Chance(Math.Max(0.05, 0.35 - a.Def.Composure / 400.0))) { Release(sim, x, "소리에 놀라 손을 놓았다"); return true; } }
            }
            return false;
        }

        // ================================================================== endings
        static void Kill(Simulation sim, Assault x, Actor a, Actor v)
        {
            var S = sim.S;
            BodyRegion region = x.Kind == AssaultKind.Smother ? BodyRegion.Head : BodyRegion.Neck;
            DamageType type = x.Kind == AssaultKind.Drown ? DamageType.Drown : DamageType.Choke;
            int sev = x.Kind == AssaultKind.Drown ? 3 : 4;
            string cause = x.Kind == AssaultKind.Drown ? "익사" : x.Kind == AssaultKind.Smother ? "얼굴이 짓눌려 질식" : x.Kind == AssaultKind.StrangleManual ? "손에 목이 졸려 질식" : "목이 졸려 질식";
            MarkBindings(sim, v);
            var w = Mark(sim, a, v, region, type, sev, x.Kind == AssaultKind.Drown ? null : x.Tool, x.Kind == AssaultKind.StrangleManual ? "manual" : x.Kind == AssaultKind.Drown ? "drown" : "assault", ledger: true);
            if (x.Kind == AssaultKind.StrangleManual) Mark(sim, a, v, BodyRegion.Neck, DamageType.Blunt, 1, null, "grip");
            if (x.Kind == AssaultKind.Drown) Mark(sim, a, v, BodyRegion.Neck, DamageType.Blunt, 1, null, "grip");
            sim.Die(v, a.Id, cause, w);
            Note(x, S, "dead");
            Finish(sim, x, a, v, AssaultPhase.Dead, "killed");
        }

        /// <summary>The assault is over (death): the attacker's plan step completes.</summary>
        static void Finish(Simulation sim, Assault x, Actor a, Actor v, AssaultPhase p, string why)
        {
            var S = sim.S; x.Phase = p; x.PhaseAt = S.Tick; x.Ended = S.Tick; x.Outcome = why; x.Intensity = 0;
            S.Log("AssaultEnd", x.Attacker, x.Victim, x.Tool, v?.Room ?? -1, v?.Pos, $"{x.Kind}|{p}|{why}|{x.Seconds(S.Tick):0.0}s", x.Plan, true);
            Emit(S, x);
            S.Flags.Remove("blows:" + x.Attacker + ":" + x.Victim); S.Flags.Remove("attackstart:" + x.Attacker + ":" + x.Victim);
            if (a == null || a.IsPlayer) return;
            a.Anim = Anim.Idle; a.Pose = Pose.Stand;
            var plan = x.Plan != null && S.Plans.TryGetValue(x.Plan, out var pl) ? pl : null;
            if (a.Act?.Cur != null && a.Act.Cur.Kind == "Attack" && a.Act.Cur.Actor == x.Victim)
            {
                if (plan != null && plan.Stage != "Aborted" && plan.Stage != "Done") Crime.Advance(sim, a, plan);
                sim.NextStepPublic(a);
            }
        }

        /// <summary>The hold ends early: the victim lives (gasping, or still unconscious), the attacker replans or flees.</summary>
        public static void Release(Simulation sim, Assault x, string why, bool escaped = false, bool witness = false)
        {
            var S = sim.S; if (!x.Active) return;
            var a = S.A(x.Attacker); var v = S.A(x.Victim);
            var was = x.Phase; x.Phase = escaped ? AssaultPhase.Escaped : AssaultPhase.Released; x.PhaseAt = S.Tick; x.Ended = S.Tick; x.Outcome = why; x.Intensity = 0;
            Note(x, S, (escaped ? "escaped — " : "released — ") + why);
            S.Log(escaped ? "AssaultEscaped" : "AssaultReleased", x.Attacker, x.Victim, x.Tool, v?.Room ?? -1, v?.Pos, $"{x.Kind}|{was}|{why}", x.Plan, true);
            Emit(S, x);
            S.Flags.Remove("blows:" + x.Attacker + ":" + x.Victim);
            if (v != null && v.Alive)
            {
                v.NextThink = S.Clock;
                if (was == AssaultPhase.Unconscious || v.Status != ActorStatus.Active)
                {
                    v.Body.UnconsciousUntil = Math.Max(was == AssaultPhase.Unconscious ? -1 : v.Body.UnconsciousUntil, S.Clock + x.R.Range(15f, 40f));
                    Mark(sim, a, v, x.Kind == AssaultKind.Smother ? BodyRegion.Head : BodyRegion.Neck, x.Kind == AssaultKind.Drown ? DamageType.Drown : DamageType.Choke, 2, x.Kind == AssaultKind.Drown ? null : x.Tool, "survived", ledger: true);
                }
                else
                {
                    Mark(sim, a, v, x.Kind == AssaultKind.Smother ? BodyRegion.Head : BodyRegion.Neck, x.Kind == AssaultKind.Drown ? DamageType.Blunt : DamageType.Choke, 1, null, x.Kind == AssaultKind.StrangleManual ? "manual" : "survived");
                    if (x.Kind == AssaultKind.Drown) { v.Wet = true; v.WetUntil = Math.Max(v.WetUntil, S.Clock + 60); }
                    v.Needs.Fear = 1; v.Needs.Stress = 1; v.Emotion = Emotion.Fear; v.Anim = Anim.Idle;
                    if (v.Pose == Pose.Kneel || v.Pose == Pose.Sleep || v.Pose == Pose.LieBack) v.Pose = Pose.Stand;
                    // who did it: seen face to face (or turned round in the struggle); a garrote from behind leaves them guessing
                    bool sawFace = x.Kind != AssaultKind.StrangleRear || x.Seconds(S.Tick) > 6f && x.R.Chance(0.4);
                    if (!sawFace) { double since = S.Clock - x.Seconds(S.Tick) * S.ClockRate - 0.5; S.K(v.Id).Sightings.RemoveAll(s => s.Target == x.Attacker && s.T1 >= since); S.K(v.Id).Open?.Remove(x.Attacker); }
                    if (a != null) { S.K(v.Id).Facts.Add(sawFace ? "attacked-by:" + a.Id : "attacked-unknown:" + (int)S.Clock); if (sawFace) Relations.Change(S, v.Id, a.Id, fear: 0.7f, grudge: 0.7f, trust: -1f, like: -0.8f, memory: "나를 죽이려 했다", tag: "enemy"); }
                    if (!v.IsPlayer)
                    {
                        if (sawFace)
                        {
                            // the scream that could not come out, then straight to the others with a name
                            if (v.Body.Speech > 0.3f) { S.Emit(GameEventType.Speech, v.Id, text: x.Kind == AssaultKind.Drown ? "콜록, 콜록… 사, 살려 줘!" : "커헉… 살려 줘!", pos: v.Pos, key: "scream", value: 1); sim.Sound(SoundKind.Scream, v.Pos, 0.85f, v.Id, v.Id); }
                            Cases.OnVictimWake(sim, v);
                        }
                        else Crime.VictimReact(sim, v, a);   // screams and runs to people: never saw who it was
                    }
                }
            }
            if (a != null && a.Alive && !a.IsPlayer)
            {
                a.Anim = Anim.Idle; a.Pose = Pose.Stand;
                var plan = x.Plan != null && S.Plans.TryGetValue(x.Plan, out var pl) ? pl : null;
                if (plan != null && plan.Stage != "Aborted" && plan.Stage != "Done")
                {
                    if (witness) Crime.Abort(sim, a, plan, "목격자가 나타나서");
                    else { plan.Log.Add($"{ClockFmt.DayHM(S.Clock)} 수정: {why}"); Crime.Replan(sim, a, plan, why); }
                }
                if (a.Act?.Cur != null && a.Act.Cur.Kind == "Attack") sim.Interrupt(a, 1.5);
                if (witness) { a.Needs.Fear = MathX.Clamp01(a.Needs.Fear + 0.5f); }
            }
        }

        // ================================================================== marks, scratches, knocks
        /// <summary>A wound record on a body without the combat damage rules (grip bruises, a scratch, a bite, the fatal mark of
        /// a hold): the Game shows it; examiners read it. ledger=true also writes a Strike event (the reveal links it).</summary>
        public static Wound Mark(Simulation sim, Actor by, Actor v, BodyRegion region, DamageType type, int sev, string tool, string tag, bool ledger = false)
        {
            var S = sim.S;
            var w = new Wound { Region = region, Type = type, Sev = sev, Tick = S.Tick, Clock = S.Clock, By = by?.Id, Weapon = tool, Postmortem = !v.Alive };
            if (ledger) { var ev = S.Log("Strike", by?.Id, v.Id, tool, v.Room, v.Pos, $"{region}/{type}/{sev}/{tag ?? "assault"}", secret: true); w.CauseEvent = (tag != null ? tag + "|" : "") + ev.Seq; }
            else w.CauseEvent = tag;
            v.Body.Wounds.Add(w);
            // a soft contact (no shove): the Game shows the mark and a flinch, not a knock-back
            float dx = 0, dz = 1; if (by != null && by != v) { dx = v.Pos.x - by.Pos.x; dz = v.Pos.z - by.Pos.z; float l = (float)Math.Sqrt(dx * dx + dz * dz); if (l > 1e-3f) { dx /= l; dz /= l; } else { dx = 0; dz = 1; } }
            S.Emit(GameEventType.Wound, by?.Id, v.Id, text: WoundText.Describe(w), pos: v.Pos, value: sev,
                data: string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}|0,0,0|{4:0.###},0,{5:0.###}|0.01|0|0,0,0|1", region, type, sev, w.Postmortem ? 1 : 0, dx, dz));
            return w;
        }

        /// <summary>The victim's nails catch the attacker (skin under the nails ↔ a fresh scratch on someone): the same flags the
        /// murder track's clues and observations read.</summary>
        static void Scratch(Simulation sim, Actor victim, Actor culprit, BodyRegion region)
        {
            var S = sim.S; if (S.Flags.ContainsKey("scratched:" + culprit.Id + ":" + victim.Id)) return;
            Mark(sim, victim, culprit, region, DamageType.Cut, 1, null, "scratch");
            if (region == BodyRegion.HandL) culprit.Body.HandL = MathX.Clamp01(culprit.Body.HandL - 0.1f); else if (region == BodyRegion.HandR || region == BodyRegion.ArmR) culprit.Body.HandR = MathX.Clamp01(culprit.Body.HandR - 0.05f);
            S.Flags["scratched:" + culprit.Id + ":" + victim.Id] = S.Clock; S.Flags["scratch:" + culprit.Id] = S.Clock; S.Flags["nails:" + victim.Id] = S.Clock;
            S.Log("Scratched", culprit.Id, victim.Id, room: culprit.Room, pos: culprit.Pos, data: region.ToString(), secret: true);
        }

        /// <summary>A kicking foot or a flailing arm sends something within reach flying: a loose object (glass breaks), else a
        /// light piece of furniture goes over. A mark on the floor records it.</summary>
        static bool Knock(Simulation sim, Assault x, Actor a, Actor v)
        {
            var S = sim.S; var r = x.R;
            var item = S.Items.Values.Where(i => i.Holder == null && !i.Hidden && i.Room == v.Room && i.Pos.f == v.Pos.f && i.Pos.DistXZ(v.Pos) < 1.8f && i.Def != null && i.Def.Tag != "part" && i.Id != x.Tool)
                .OrderBy(i => i.Pos.DistXZ(v.Pos)).ThenBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
            if (item != null)
            {
                var def = item.Def; var old = item.Pos;
                float ang = r.Range(-180f, 180f) * (float)Math.PI / 180f, dist = r.Range(0.3f, 0.9f);
                var to = new P3(item.Pos.f, item.Pos.x + (float)Math.Sin(ang) * dist, item.Pos.z + (float)Math.Cos(ang) * dist);
                if (S.Layout.RoomAt(to) == item.Room) item.Pos = to;
                bool breaks = (def.Mat == Mat.Glass || def.Mat == Mat.Ceramic) && item.Damage < 3;
                if (breaks)
                {
                    item.Damage = 3; sim.Sound(SoundKind.GlassBreak, item.Pos, 0.6f, a.Id);
                    for (int i = 0; i < 2; i++) { var fr = new Item { Id = S.NewId("it"), Type = "Fragment", Name = def.Kor + " 파편", ParentItem = item.Id, Pos = new P3(item.Pos.f, item.Pos.x + r.Range(-0.5f, 0.5f), item.Pos.z + r.Range(-0.5f, 0.5f)), Room = item.Room }; fr.HomeRoom = fr.Room; fr.HomePos = fr.Pos; S.Items[fr.Id] = fr; S.Emit(GameEventType.ItemMoved, null, data: fr.Id, text: "spawn", pos: fr.Pos); }
                    S.Log("Break", a.Id, item: item.Id, room: item.Room, pos: item.Pos, data: "struggle", secret: true);
                }
                else sim.Sound(SoundKind.Crash, item.Pos, 0.4f, a.Id);
                S.Emit(GameEventType.ItemMoved, null, data: item.Id, text: breaks ? "broken" : "knocked", pos: item.Pos);
                if (x.Knocks == 0) sim.AddTrace("Scratch", old, item.Room, a.Id, v.Id, 0.3f, 1, "바닥에 긁힌 자국과 흐트러진 물건", "이 근처에서 몸싸움이 있었다 — 발길질에 물건이 날아갔다", "언제, 누가 그랬는지");
                return true;
            }
            var room = S.Layout.Room(v.Room); if (room == null) return false;
            var f = room.Furniture.Select(i => S.Layout.Furniture[i]).Where(ff => ff.Pos.f == v.Pos.f && Gore.ToppleKind(ff) && ff.Damage == 0 && !ff.Marks.Contains("넘어져 있다") && ff.Pos.DistXZ(v.Pos) < 1.7f)
                .OrderBy(ff => ff.Pos.DistXZ(v.Pos)).ThenBy(ff => ff.Id).FirstOrDefault();
            if (f == null) return false;
            f.Marks.Add("넘어져 있다"); f.Damage = Math.Max(f.Damage, 1);
            if (!f.Moved) { f.Moved = true; f.Origin = f.Pos; }
            sim.Sound(SoundKind.Crash, new P3(f.Pos.f, f.Pos.x, f.Pos.z), 0.45f, a.Id);
            S.Emit(GameEventType.Furniture, a.Id, v.Id, text: "knocked", id: f.Id, pos: f.Pos, value: MathX.AngleDeg(f.Pos.x - v.Pos.x, f.Pos.z - v.Pos.z));
            S.Log("FurnitureHit", a.Id, room: f.Room, pos: f.Pos, data: f.Type + ":struggle", secret: true);
            if (x.Knocks == 0) sim.AddTrace("Struggle", v.Pos, v.Room, a.Id, v.Id, 0.5f, 0, "넘어진 " + (FurnitureCatalog.Get(f.Type)?.Kor ?? "가구"), "이 자리에서 격한 몸싸움이 있었다", "누구와 싸웠는지");
            return true;
        }

        // ================================================================== things people notice afterwards
        /// <summary>A bite on the hand, hair tangled round the fingers, gunpowder on a sleeve: seen by whoever stands close.</summary>
        static void Observations(Simulation sim)
        {
            var S = sim.S;
            foreach (var x in S.Actors.Values)
            {
                if (!x.Alive || x.IsButler) continue;
                bool bite = S.Flags.TryGetValue("bite:" + x.Id, out var bAt) && S.Clock - bAt < 16 * 60;
                bool hair = S.Flags.TryGetValue("hairhand:" + x.Id, out var hAt) && S.Clock - hAt < 30;
                bool gsr = S.Flags.TryGetValue("gsr:" + x.Id, out var gAt) && S.Clock - gAt < 90;
                if (!bite && !hair && !gsr) continue;
                foreach (var o in S.Actors.Values)
                {
                    if (o == x || !o.Alive || o.Status != ActorStatus.Active || o.Pose == Pose.Sleep || o.IsButler || o.Room != x.Room || o.Pos.f != x.Pos.f) continue;
                    float dist = o.Pos.DistXZ(x.Pos); if (dist > 2.6f || sim.RoomLight(x.Room) < 0.3f) continue;
                    float face = Math.Abs(MathX.DeltaAngle(o.Yaw, MathX.AngleDeg(x.Pos.x - o.Pos.x, x.Pos.z - o.Pos.z))); if (face > 75) continue;
                    string X = Cast.GivenOf(x.Id);
                    if (bite && S.K(o.Id).Facts.Add("saw-bite:" + x.Id + ":" + (int)(bAt / 60)))
                        Evidences.Add(sim, o.Id, EvKind.Sighting, $"{X}의 손에 난 잇자국", $"{ClockFmt.Vague(S.Clock)}, {S.RoomName(x.Room)}에서 {X}의 손에 사람 이빨 모양으로 파인 상처가 보였다.", "직접 목격", "bite:" + x.Id + ":" + o.Id, S.Clock, S.Clock, x.Room,
                            "그 무렵 손에 물린 상처가 있었다", "누구에게, 왜 물렸는지", true, new Prop { Kind = PropKind.Injured, A = x.Id, Room = x.Room, T0 = S.Clock, T1 = S.Clock, Value = "물린 자국" });
                    if (hair && dist < 1.8f && S.K(o.Id).Facts.Add("saw-hair:" + x.Id + ":" + (int)hAt))
                        Evidences.Add(sim, o.Id, EvKind.Sighting, $"{X}의 손가락에 엉킨 머리카락", $"{ClockFmt.Vague(S.Clock)}, {S.RoomName(x.Room)}에서 {X}의 젖은 손가락 사이에 긴 머리카락 몇 가닥이 엉겨 있었다.", "직접 목격", "hair:" + x.Id + ":" + o.Id, S.Clock, S.Clock, x.Room,
                            "그 무렵 손에 남의 머리카락이 엉겨 있었다", "누구의 머리카락인지", true, new Prop { Kind = PropKind.Held, A = x.Id, Room = x.Room, T0 = S.Clock, T1 = S.Clock, Item = "HairStrands", Value = "hair" });
                    if (gsr && dist < 2.0f && S.K(o.Id).Facts.Add("saw-gsr:" + x.Id + ":" + (int)gAt))
                        Evidences.Add(sim, o.Id, EvKind.Sighting, $"{X}의 소매에서 나는 화약 냄새", $"{ClockFmt.Vague(S.Clock)}, {S.RoomName(x.Room)}에서 {X}의 소맷부리에 검은 그을음이 묻어 있었고 매캐한 화약 냄새가 났다.", "직접 목격", "gsr:" + x.Id + ":" + o.Id, S.Clock, S.Clock, x.Room,
                            "그 무렵 누군가 총을 쏜 손이었다", "무엇을 향해 쐈는지", true, new Prop { Kind = PropKind.TraceAt, A = x.Id, Room = x.Room, T0 = S.Clock, T1 = S.Clock, Value = "powder-residue" });
                }
            }
        }
    }
}
