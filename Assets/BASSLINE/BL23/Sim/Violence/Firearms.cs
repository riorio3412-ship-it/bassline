using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Firearms and the crossbow (ViolenceMotionContract.md "Firearms and crossbow"): a silver revolver (six chambers, the
    /// spent cases stay in the cylinder), a dueling pistol (one shot, a slow muzzle load, a burnt paper patch left on the floor),
    /// a double-barrelled hunting shotgun (a spread of shot, spent shells ejected on reloading) and a hunting crossbow (cocked
    /// slowly, almost silent, the bolt stays where it strikes). Kept in the trophy room, the study and the gallery; ammunition
    /// lies elsewhere. A shot is a deterministic march along the line of fire over the nav grid: walls and closed doors stop
    /// or slow it, furniture and people are hit where the line meets them. A gunshot is heard all over the house.
    /// </summary>
    public static partial class Violence
    {
        public static bool IsRanged(ItemDef d) => d != null && (d.Tag == "firearm" || d.Tag == "crossbow");
        public static bool IsFirearm(ItemDef d) => d != null && d.Tag == "firearm";
        static int Capacity(string type) => type == "Revolver" ? 6 : type == "HuntingShotgun" ? 2 : 1;
        static string AmmoType(string type) => type == "Revolver" ? "Cartridges" : type == "HuntingShotgun" ? "ShotShells" : type == "DuelingPistol" ? "PowderFlask" : type == "Crossbow" ? "BoltQuiver" : null;
        /// <summary>Ticks to make ready: six rounds into a cylinder, two shells, a muzzle-loaded ball, a crossbow cranked back.</summary>
        public static int LoadTicks(string type) => type == "Revolver" ? 55 : type == "HuntingShotgun" ? 35 : type == "DuelingPistol" ? 150 : type == "Crossbow" ? 110 : 40;
        public static int AimTicks(string type) => type == "Revolver" ? 8 : type == "DuelingPistol" ? 12 : type == "HuntingShotgun" ? 9 : 13;
        public static float Range(string type) => type == "Revolver" ? 14f : type == "DuelingPistol" ? 12f : type == "HuntingShotgun" ? 16f : 22f;
        static float SpreadDeg(string type) => type == "Revolver" ? 1.5f : type == "DuelingPistol" ? 1.9f : type == "HuntingShotgun" ? 0.9f : 1.0f;
        static float Energy(string type) => type == "Revolver" ? 1.0f : type == "DuelingPistol" ? 0.95f : type == "HuntingShotgun" ? 0.42f : 0.85f;   // per projectile

        public static GunState Gun(GameState S, string item)
        {
            var l = S.Violence.Guns; for (int i = 0; i < l.Count; i++) if (l[i].Item == item) return l[i];
            var g = new GunState { Item = item }; l.Add(g); return g;
        }

        /// <summary>Ammunition for this weapon on this person (a box of cartridges, a flask and balls, a quiver, a loose bolt).</summary>
        public static Item AmmoFor(Simulation sim, Actor a, Item gun)
        {
            string at = AmmoType(gun?.Type); if (at == null) return null;
            return sim.Carried(a).FirstOrDefault(i => i.Type == at || (gun.Type == "Crossbow" && i.Type == "Bolt" && i.Holder == a.Id));
        }

        // ================================================================== loading
        /// <summary>Load (the caller has spent the time: <see cref="LoadTicks"/>). Returns null on success, else why not.</summary>
        public static string Load(Simulation sim, Actor a, Item gun)
        {
            var S = sim.S; if (gun == null || !IsRanged(gun.Def)) return "장전할 무기가 없다";
            var g = Gun(S, gun.Id); int cap = Capacity(gun.Type);
            if (g.Loaded >= cap) return "이미 장전되어 있다";
            var ammo = AmmoFor(sim, a, gun); if (ammo == null) return gun.Type == "Crossbow" ? "화살이 없다" : "탄약이 없다";
            if (gun.Type == "HuntingShotgun" && g.Spent > 0)
            {
                // breaking the gun open throws the spent shells on the floor
                var r = S.R(Stream.Combat);
                for (int i = 0; i < g.Spent; i++)
                {
                    var sh = new Item { Id = S.NewId("it"), Type = "SpentShell", Name = "빈 엽총 탄피", Pos = new P3(a.Pos.f, a.Pos.x + r.Range(-0.6f, 0.6f), a.Pos.z + r.Range(-0.6f, 0.6f)), Room = a.Room };
                    sh.HomeRoom = sh.Room; sh.HomePos = sh.Pos; sh.Surface.Add("fired"); sh.LastUser = a.Id; S.Items[sh.Id] = sh; S.Emit(GameEventType.ItemMoved, a.Id, data: sh.Id, text: "spawn", pos: sh.Pos);
                }
                g.Spent = 0;
            }
            if (gun.Type == "Revolver") g.Spent = 0;   // the empty cases are shaken out into the hand (and the pocket)
            if (gun.Type == "Crossbow" && ammo.Type == "Bolt") { ammo.Holder = null; a.Pocket.Remove(ammo.Id); if (a.HandL == ammo.Id) a.HandL = null; if (a.HandR == ammo.Id) a.HandR = null; S.Items.Remove(ammo.Id); S.Emit(GameEventType.ItemMoved, a.Id, data: ammo.Id, text: "remove"); }
            g.Loaded = cap; g.ReadyAt = S.Tick; g.LastBy = a.Id;
            if (!ammo.Surface.Contains("used")) ammo.Surface.Add("used");
            S.Log("Reload", a.Id, item: gun.Id, room: a.Room, pos: a.Pos, data: gun.Type + "|" + ammo.Type, plan: a.PlanId, secret: true);
            S.Emit(GameEventType.Anim, a.Id, text: gun.Type, data: "reload|" + gun.Id, pos: a.Pos);
            sim.Sound(SoundKind.Switch, a.Pos, 0.12f, a.Id);
            return null;
        }

        // ================================================================== the NPC's shooting step (Attack mode "shoot")
        static void ShootStep(Simulation sim, Actor a, Actor t, ActionStep st, MurderPlan plan, Item weapon)
        {
            var S = sim.S;
            var gun = weapon != null && IsRanged(weapon.Def) ? weapon : sim.Carried(a).FirstOrDefault(i => IsRanged(i.Def));
            if (gun == null) { st.Tag = "kill"; return; }
            if (a.HandR != gun.Id && a.HandL != gun.Id) { a.Pocket.Remove(gun.Id); if (a.HandR != null) sim.DropItem(a, S.I(a.HandR), a.Pos); a.HandR = gun.Id; }
            var g = Gun(S, gun.Id);
            string rk = "reloading:" + a.Id, ak = "aiming:" + a.Id;
            // reloading takes its time
            if (S.Flags.TryGetValue(rk, out var rStart))
            {
                a.Speed = 0; a.Anim = Anim.Use;
                if (S.Tick - (long)rStart < LoadTicks(gun.Type)) return;
                S.Flags.Remove(rk);
                if (Load(sim, a, gun) != null) { st.Tag = "kill"; return; }
            }
            if (g.Loaded <= 0)
            {
                if (AmmoFor(sim, a, gun) == null || S.Flags.TryGetValue("shots:" + a.Id + ":" + t.Id, out var sh) && sh >= 8) { st.Tag = "kill"; return; }   // nothing left: the butt of the gun
                S.Flags[rk] = S.Tick; a.Speed = 0; a.Anim = Anim.Use; S.Flags.Remove(ak); return;
            }
            float d = a.Pos.Dist(t.Pos);
            // close in to a sure distance first; a target running away is fired at from wherever the shot still carries
            float want = t.Running || t.Speed > 1.8f ? Range(gun.Type) : gun.Type == "DuelingPistol" ? 4.5f : gun.Type == "Revolver" ? 5.5f : gun.Type == "HuntingShotgun" ? 7f : 8f;
            bool clear = a.Pos.f == t.Pos.f && d <= want && LineOfFire(sim, a, t);
            if (!clear)
            {
                S.Flags.Remove(ak);
                if (d > 30f || S.Clock - a.Act.StepStart > 8) { if (plan != null) Crime.Replan(sim, a, plan, "lost target"); sim.Interrupt(a); return; }
                if (a.Act.Path == null || S.Tick % 6 == 0)
                {
                    var pr = Pathfinder.Find(S.Layout, a.Pos, t.Pos, sim.DoorCostFor(a)); if (!pr.Ok) { sim.Interrupt(a); return; }
                    a.Act.Path = pr.Points; a.Act.PathDoors = pr.DoorAtPoint; a.Act.PathStairs = pr.StairAtPoint; a.Act.PathIdx = 1;
                }
                sim.MoveAlongPublic(a, false); return;
            }
            a.Speed = 0; a.Act.Path = null; a.Yaw = MathX.AngleDeg(t.Pos.x - a.Pos.x, t.Pos.z - a.Pos.z); a.Anim = Anim.Point;
            if (!S.Flags.TryGetValue(ak, out var aStart))
            {
                S.Flags[ak] = aStart = S.Tick;
                if (!S.Flags.ContainsKey("attackstart:" + a.Id + ":" + t.Id)) { S.Flags["attackstart:" + a.Id + ":" + t.Id] = S.Clock; Incidents.OnAttackBegin(sim, a, t, plan); }
                S.Emit(GameEventType.Anim, a.Id, t.Id, text: gun.Type, data: "aim|" + gun.Id, pos: a.Pos);
            }
            if (S.Tick - (long)aStart < AimTicks(gun.Type) || S.Tick < g.ReadyAt) return;
            S.Flags.Remove(ak);
            // aim at the chest (a lying body: at the torso on the floor)
            float ty = TargetY(t, 0.72f), ay = MuzzleY(a);
            float dist = a.Pos.DistXZ(t.Pos); float yaw = MathX.AngleDeg(t.Pos.x - a.Pos.x, t.Pos.z - a.Pos.z);
            float pitch = (float)(Math.Atan2(ty - ay, Math.Max(0.1f, dist)) * 180 / Math.PI);
            string sk = "shots:" + a.Id + ":" + t.Id; S.Flags[sk] = (S.Flags.TryGetValue(sk, out var n) ? n : 0) + 1;
            Fire(sim, a, gun, ay, yaw, pitch, t.Id, plan?.Id);
            bool done = !t.Alive || t.Status != ActorStatus.Active && (t.Body.DeathAt >= 0 || t.Body.Bleed > 0.08f) || S.Flags[sk] >= 8;
            if (done)
            {
                S.Flags.Remove(sk); S.Flags.Remove("attackstart:" + a.Id + ":" + t.Id);
                if (plan != null) Crime.Advance(sim, a, plan); sim.NextStepPublic(a);
            }
            else if (g.Loaded > 0) S.Flags[ak] = S.Tick - AimTicks(gun.Type) / 2;   // a follow-up shot comes quicker
        }

        /// <summary>Nothing solid between the muzzle and the target's chest (walls, closed doors, tall furniture).</summary>
        public static bool LineOfFire(Simulation sim, Actor a, Actor t)
        {
            if (a.Pos.f != t.Pos.f) return false;
            var g = sim.S.Layout.Nav(a.Pos.f);
            if (!g.Ray(a.Pos.x, a.Pos.z, t.Pos.x, t.Pos.z, d => sim.S.Layout.Doors[d].Open, false, out _)) return false;
            float ty = TargetY(t, 0.72f), ay = MuzzleY(a), dist = a.Pos.DistXZ(t.Pos);
            float dx = (t.Pos.x - a.Pos.x) / Math.Max(0.01f, dist), dz = (t.Pos.z - a.Pos.z) / Math.Max(0.01f, dist), dy = (ty - ay) / Math.Max(0.01f, dist);
            var h = March(sim, a.Pos, ay, dx, dy, dz, dist + 0.5f, a.Id, null, 1f, true);
            return h == null || h.Kind == "body" && h.Actor == t;
        }

        static float Height(Actor a) => (a.Def?.HeightCm ?? 170) / 100f;
        static bool Lying(Actor a) => !a.Alive || a.Status == ActorStatus.Unconscious || a.Pose == Pose.LieBack || a.Pose == Pose.LieFront || a.Pose == Pose.LieSide || a.Pose == Pose.Sleep || a.Pose == Pose.Slumped;
        static float Stature(Actor a) => a.Pose == Pose.Sit ? 0.72f : a.Pose == Pose.Crouch || a.Pose == Pose.Kneel ? 0.66f : 1f;
        public static float MuzzleY(Actor a) => Height(a) * (a.Pose == Pose.Crouch || a.Pose == Pose.Kneel ? 0.52f : a.Pose == Pose.Sit ? 0.6f : 0.8f);
        /// <summary>A height on the target: frac of standing height (0.72 = chest), squashed for a seated / kneeling / lying body.</summary>
        static float TargetY(Actor t, float frac) => Lying(t) ? BedY(t) + 0.15f : Height(t) * Stature(t) * frac;
        static float BedY(Actor t) => t.Pose == Pose.Sleep || t.Spot >= 0 && t.Pose == Pose.LieBack ? 0.55f : 0f;

        // ================================================================== firing
        /// <summary>Fire `gun` from where `a` stands: muzzle height `y0`, bearing `yaw`, elevation `pitch` (degrees). The aim wanders by
        /// the weapon, the shooter's nerve, the light and the range (deterministic: the combat stream). Returns the record.</summary>
        public static ShotRecord Fire(Simulation sim, Actor a, Item gun, float y0, float yaw, float pitch, string intended, string plan = null)
        {
            var S = sim.S; if (gun == null || !IsRanged(gun.Def)) return null;
            var g = Gun(S, gun.Id); if (g.Loaded <= 0) { sim.Sound(SoundKind.Switch, a.Pos, 0.1f, a.Id); S.Emit(GameEventType.Anim, a.Id, text: gun.Type, data: "dry|" + gun.Id, pos: a.Pos); return null; }
            var r = S.R(Stream.Combat); string type = gun.Type; bool bolt = type == "Crossbow";
            g.Loaded--; g.Shots++; g.LastShot = S.Clock; g.LastBy = a.Id; if (type == "Revolver" || type == "HuntingShotgun") g.Spent++;
            var shot = new ShotRecord { Id = "sh" + (++S.Violence.Next), By = a.Id, Weapon = gun.Id, WeaponType = type, Intended = intended, Y0 = y0, Yaw = yaw, Pitch = pitch, Clock = S.Clock, Tick = S.Tick, Room = a.Room, Silent = bolt };
            double yr = yaw * Math.PI / 180;
            shot.From = new P3(a.Pos.f, a.Pos.x + (float)Math.Sin(yr) * 0.35f, a.Pos.z + (float)Math.Cos(yr) * 0.35f);
            // --- how steady the hand is
            float nerve = a.Def != null ? (a.Def.Composure + a.Def.Obs) / 200f : 0.6f;
            float spread = SpreadDeg(type) * (1.45f - 0.8f * nerve) * (1f + a.Needs.Stress * 0.5f) * (sim.RoomLight(a.Room) < 0.25f ? 1.6f : 1f) * (a.IsPlayer ? 0.55f : 1f);
            var target = S.A(intended); if (target != null && target.Speed > 0.5f) spread *= 1.35f;
            int pellets = type == "HuntingShotgun" ? 7 : 1;
            var hits = new Dictionary<string, List<(BodyRegion region, ShotImpact imp)>>();
            float centreYaw = yaw + Gauss(r) * spread, centrePitch = pitch + Gauss(r) * spread * 0.8f;
            for (int p = 0; p < pellets; p++)
            {
                float py = centreYaw + (pellets > 1 ? Gauss(r) * 2.2f : 0f), pp = centrePitch + (pellets > 1 ? Gauss(r) * 1.8f : 0f);
                Trace(sim, shot, a, gun, py, pp, Energy(type), bolt, pellets > 1, hits);
            }
            // --- wounds: one entry per region hit (a shotgun's pellets add up)
            foreach (var kv in hits.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var v = S.A(kv.Key); if (v == null) continue;
                foreach (var grp in kv.Value.GroupBy(h => h.region).OrderBy(gp => (int)gp.Key))
                {
                    var first = grp.First().imp; int count = grp.Count();
                    Wound(sim, shot, a, gun, v, grp.Key, first, count, bolt, pellets > 1);
                }
            }
            // --- noise, smoke, what is left behind
            if (bolt) sim.Sound(SoundKind.Strike, shot.Impacts.Count > 0 ? shot.Impacts[0].At : a.Pos, 0.12f, a.Id);
            else
            {
                sim.Gunshot(a.Pos, a.Id, type);
                S.Flags["gsr:" + a.Id] = S.Clock;
                if (type == "DuelingPistol")
                {
                    var at = new P3(a.Pos.f, a.Pos.x + (float)Math.Sin(yr) * r.Range(1.2f, 2.6f) + r.Range(-0.3f, 0.3f), a.Pos.z + (float)Math.Cos(yr) * r.Range(1.2f, 2.6f) + r.Range(-0.3f, 0.3f));
                    if (S.Layout.RoomAt(at) < 0) at = a.Pos;
                    var wd = new Item { Id = S.NewId("it"), Type = "Wadding", Name = "그을린 종이 패치", Pos = at, Room = S.Layout.RoomAt(at) };
                    wd.HomeRoom = wd.Room; wd.HomePos = at; wd.Surface.Add("burnt"); wd.LastUser = a.Id; S.Items[wd.Id] = wd; S.Emit(GameEventType.ItemMoved, a.Id, data: wd.Id, text: "spawn", pos: at);
                }
            }
            string summary = string.Join(",", shot.Impacts.Select(i => i.Kind + (i.Actor != null ? ":" + i.Actor + ":" + i.Region : i.Furniture >= 0 ? ":f" + i.Furniture : i.Door >= 0 ? ":d" + i.Door : "")));
            S.Log(bolt ? "CrossbowShot" : "Gunshot", a.Id, intended, gun.Id, a.Room, a.Pos, $"{type}|{summary}", plan ?? a.PlanId, true);
            S.Violence.Shots.Add(shot); if (S.Violence.Shots.Count > 60) S.Violence.Shots.RemoveAt(0);
            S.Emit(GameEventType.Anim, a.Id, intended, text: type, data: "shot|" + shot.Id, pos: a.Pos);
            if (a.IsPlayer) sim.EmergencyTrigger(4);
            return shot;
        }

        static float Gauss(Rng r) { double u1 = Math.Max(1e-7, r.D()), u2 = r.D(); return (float)(Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2)); }

        /// <summary>One projectile: march, hit, maybe pass through (a body, a thin panel, a door) and go on.</summary>
        static void Trace(Simulation sim, ShotRecord shot, Actor a, Item gun, float yawDeg, float pitchDeg, float energy, bool bolt, bool pellet, Dictionary<string, List<(BodyRegion, ShotImpact)>> hits)
        {
            var S = sim.S;
            double yr = yawDeg * Math.PI / 180, pr = pitchDeg * Math.PI / 180;
            float cp = (float)Math.Cos(pr), dx = (float)Math.Sin(yr) * cp, dz = (float)Math.Cos(yr) * cp, dy = (float)Math.Sin(pr);
            var passed = new HashSet<string> { a.Id };
            float s0 = 0.3f, maxD = bolt ? 40f : 60f;
            for (int guard = 0; guard < 6 && energy > 0.08f; guard++)
            {
                var h = March(sim, shot.From, shot.Y0, dx, dy, dz, maxD, a.Id, passed, s0, false);
                if (h == null) { if (guard == 0) shot.Impacts.Add(new ShotImpact { Kind = "miss", At = new P3(shot.From.f, shot.From.x + dx * maxD, shot.From.z + dz * maxD), Y = shot.Y0 + dy * maxD, Dist = maxD }); return; }
                var imp = new ShotImpact { Kind = h.Kind, At = h.At, Y = h.Y, Dist = h.Dist, Furniture = h.F?.Id ?? -1, Door = h.Door };
                s0 = h.Dist + 0.06f;
                switch (h.Kind)
                {
                    case "body":
                        {
                            var v = h.Actor; imp.Actor = v.Id; imp.Region = h.Region;
                            if (!hits.TryGetValue(v.Id, out var l)) hits[v.Id] = l = new List<(BodyRegion, ShotImpact)>();
                            l.Add((h.Region, imp)); shot.Impacts.Add(imp); passed.Add(v.Id);
                            float absorb = h.Region == BodyRegion.Head ? 0.75f : h.Region == BodyRegion.Chest || h.Region == BodyRegion.Abdomen || h.Region == BodyRegion.Back ? 0.66f : h.Region == BodyRegion.Neck ? 0.5f : 0.38f;
                            energy -= absorb;
                            if (bolt || pellet || energy < 0.2f) { if (bolt) Stick(sim, shot, gun, imp, v, null, -1, yawDeg, pitchDeg); return; }
                            imp.Through = true;
                            continue;
                        }
                    case "door":
                        {
                            shot.Impacts.Add(imp); Hole(sim, shot, imp, "door", yawDeg, pitchDeg);
                            if (bolt || pellet || energy < 0.5f) { if (bolt) Stick(sim, shot, gun, imp, null, null, h.Door, yawDeg, pitchDeg); return; }
                            energy *= 0.5f; imp.Through = true; passed.Add("door" + h.Door); continue;
                        }
                    case "furniture":
                        {
                            shot.Impacts.Add(imp); var f = h.F; Hole(sim, shot, imp, "furniture", yawDeg, pitchDeg);
                            var def = FurnitureCatalog.Get(f.Type); var mat = def?.Mat ?? f.Material;
                            bool glass = mat == Mat.Glass || (def?.Fragile ?? false) && mat != Mat.Wood && mat != Mat.Cloth;
                            if (glass) { Shatter(sim, f, a); if (!bolt) { energy *= 0.8f; imp.Through = true; passed.Add("f" + f.Id); continue; } }
                            bool soft = mat == Mat.Cloth || mat == Mat.Leather || mat == Mat.Plant || mat == Mat.Paper;
                            bool thin = mat == Mat.Wood && f.W * f.D < 0.8f && f.H < 1.2f;
                            if (bolt) { Stick(sim, shot, gun, imp, null, f, -1, yawDeg, pitchDeg); return; }
                            if ((soft || thin) && energy > 0.4f && !pellet) { energy *= soft ? 0.7f : 0.5f; imp.Through = true; passed.Add("f" + f.Id); continue; }
                            return;
                        }
                    default:   // wall, floor, ceiling, void
                        shot.Impacts.Add(imp);
                        if (h.Kind == "wall" || h.Kind == "floor") { Hole(sim, shot, imp, h.Kind, yawDeg, pitchDeg); if (bolt) Stick(sim, shot, gun, imp, null, null, -1, yawDeg, pitchDeg); }
                        return;
                }
            }
        }

        sealed class Hit { public string Kind; public Actor Actor; public Furniture F; public int Door = -1; public P3 At; public float Y, Dist; public BodyRegion Region; }

        /// <summary>March along the line in 5 cm steps over the floor's nav grid: walls, closed doors, the edge of the floor,
        /// furniture at the line's height, people (a standing body is a column, a lying body a slab along its yaw).</summary>
        static Hit March(Simulation sim, P3 from, float y0, float dx, float dy, float dz, float maxD, string shooter, HashSet<string> passed, float start, bool probe)
        {
            var S = sim.S; var L = S.Layout; var g = L.Nav(from.f); const float step = 0.05f;
            int prev = g.CellOf(from.x + dx * start, from.z + dz * start); if (prev < 0) return null;
            float ceiling = L.Room(g.Room[prev])?.CeilingH ?? 4f;
            for (float s = start + step; s <= maxD; s += step)
            {
                float x = from.x + dx * s, z = from.z + dz * s, y = y0 + dy * s;
                var at = new P3(from.f, x, z);
                if (y <= 0.02f) return new Hit { Kind = "floor", At = at, Y = 0f, Dist = s };
                if (y >= ceiling - 0.05f) return new Hit { Kind = "ceiling", At = at, Y = ceiling, Dist = s };
                int k = g.CellOf(x, z); if (k < 0) return new Hit { Kind = "wall", At = new P3(from.f, x - dx * step, z - dz * step), Y = y, Dist = s };
                if (k != prev)
                {
                    int e = CrossEdge(g, prev, k);
                    if (e == -2) return new Hit { Kind = "wall", At = new P3(from.f, x - dx * step * 0.5f, z - dz * step * 0.5f), Y = y, Dist = s };
                    if (e >= 0 && !L.Doors[e].Open && (passed == null || !passed.Contains("door" + e))) return new Hit { Kind = "door", Door = e, At = new P3(from.f, x - dx * step * 0.5f, z - dz * step * 0.5f), Y = y, Dist = s };
                    if (g.Room[k] < 0) return new Hit { Kind = "void", At = at, Y = y, Dist = s };
                    prev = k; ceiling = L.Room(g.Room[k])?.CeilingH ?? ceiling;
                }
                // people
                foreach (var v in S.Actors.Values)
                {
                    if (v.Id == shooter || v.Pos.f != from.f || v.Status == ActorStatus.Executed || v.Status == ActorStatus.Escaped || v.CarriedBy != null) continue;
                    if (passed != null && passed.Contains(v.Id)) continue;
                    if (BodyAt(v, x, y, z, dx, dz, out var region)) return new Hit { Kind = "body", Actor = v, Region = region, At = at, Y = y, Dist = s };
                }
                // furniture in this room at this height
                var room = L.Room(g.Room[k]);
                if (room != null)
                    foreach (var fid in room.Furniture)
                    {
                        var f = L.Furniture[fid]; if (f.Pos.f != from.f || f.H <= 0.05f || f.Type == "Rug" || f.Type == "PoolWater" || f.Type == "ShallowWater" || f.Type == "Chandelier" || f.Type == "GrandStair") continue;
                        if (passed != null && passed.Contains("f" + f.Id)) continue;
                        if (y > f.H) continue;
                        NavGrid.GetFootprint(f, 0f, out var fr); if (!fr.Contains(x, z)) continue;
                        if (probe && f.H < 1.0f) continue;   // the line-of-fire check only minds tall things
                        return new Hit { Kind = "furniture", F = f, At = at, Y = y, Dist = s };
                    }
            }
            return null;
        }

        static int CrossEdge(NavGrid g, int a, int b)
        {
            int ai = a % g.NX, aj = a / g.NX, bi = b % g.NX, bj = b / g.NX;
            if (Math.Abs(ai - bi) + Math.Abs(aj - bj) == 1) return g.Edge(a, b);
            if (Math.Abs(ai - bi) <= 1 && Math.Abs(aj - bj) <= 1)
            {
                // diagonal: through either orthogonal neighbour; blocked only if both routes are
                int m1 = bi + aj * g.NX, m2 = ai + bj * g.NX;
                int e1 = Worst(g.Edge(a, m1), g.Edge(m1, b)), e2 = Worst(g.Edge(a, m2), g.Edge(m2, b));
                if (e1 == -1 || e2 == -1) return -1; if (e1 >= 0) return e1; if (e2 >= 0) return e2; return -2;
            }
            return -2;
        }
        static int Worst(int e1, int e2) => e1 == -2 || e2 == -2 ? -2 : e1 >= 0 ? e1 : e2;

        /// <summary>Is the point inside this person's body, and which part? (Standing: a column of radius 0.23 m; arms at the sides.)</summary>
        static bool BodyAt(Actor v, float x, float y, float z, float dx, float dz, out BodyRegion region)
        {
            region = BodyRegion.Chest;
            float ox = x - v.Pos.x, oz = z - v.Pos.z;
            double yr = v.Yaw * Math.PI / 180; float fx = (float)Math.Sin(yr), fz = (float)Math.Cos(yr);   // facing
            float along = ox * fx + oz * fz, side = ox * fz - oz * fx;   // + forward, + right
            if (Lying(v))
            {
                float baseY = BedY(v); if (y < baseY - 0.02f || y > baseY + 0.32f) return false;
                if (Math.Abs(side) > 0.24f || along < -0.95f || along > 0.85f) return false;
                // the head lies toward the facing direction
                float t = (along + 0.95f) / 1.8f;
                region = t > 0.86f ? BodyRegion.Head : t > 0.8f ? BodyRegion.Neck : t > 0.58f ? BodyRegion.Chest : t > 0.44f ? BodyRegion.Abdomen : side < 0 ? BodyRegion.LegL : BodyRegion.LegR;
                return true;
            }
            float H = Height(v) * Stature(v);
            if (y < 0f || y > H) return false;
            float r = (float)Math.Sqrt(ox * ox + oz * oz); if (r > 0.25f) return false;
            float f = y / Math.Max(0.5f, H);
            bool fromBehind = dx * fx + dz * fz > 0.3f;   // travelling the way they face: it came from behind
            float lat = Math.Abs(side);
            if (f > 0.88f) region = BodyRegion.Head;
            else if (f > 0.81f) region = BodyRegion.Neck;
            else if (f > 0.62f) region = lat > 0.17f ? (side < 0 ? BodyRegion.ShoulderL : BodyRegion.ShoulderR) : fromBehind ? BodyRegion.Back : BodyRegion.Chest;
            else if (f > 0.47f) region = lat > 0.18f ? (side < 0 ? BodyRegion.ArmL : BodyRegion.ArmR) : fromBehind ? BodyRegion.Back : BodyRegion.Abdomen;
            else if (f > 0.4f) region = lat > 0.17f ? (side < 0 ? BodyRegion.HandL : BodyRegion.HandR) : (side < 0 ? BodyRegion.LegL : BodyRegion.LegR);
            else if (f > 0.07f) region = side < 0 ? BodyRegion.LegL : BodyRegion.LegR;
            else region = side < 0 ? BodyRegion.FootL : BodyRegion.FootR;
            return true;
        }

        /// <summary>The wound: an entry (and, for a bullet that went through, an exit on the far side). A bolt stays in the body.</summary>
        static void Wound(Simulation sim, ShotRecord shot, Actor a, Item gun, Actor v, BodyRegion region, ShotImpact imp, int count, bool bolt, bool pellets)
        {
            var S = sim.S;
            bool vital = region == BodyRegion.Head || region == BodyRegion.Neck || region == BodyRegion.Chest || region == BodyRegion.Abdomen || region == BodyRegion.Back;
            int sev = bolt ? (vital ? 4 : 3) : pellets ? (count >= 4 ? 4 : count >= 2 ? 3 : 2) : (vital ? 4 : 3);
            if (!pellets && !bolt && imp.Dist > 15f) sev = Math.Max(2, sev - 1);
            if (!v.Alive) sev = Math.Min(sev, 4);
            double yr = shot.Yaw * Math.PI / 180; float dx = (float)Math.Sin(yr), dz = (float)Math.Cos(yr);
            float impulse = bolt ? 7f : pellets ? 6f * count : shot.WeaponType == "Revolver" ? 11f : 10f;
            float energy = bolt ? 90f : pellets ? 45f * count : 320f;
            // local contact point on the victim (actor space, metres) for the wound decal
            float ox = imp.At.x - v.Pos.x, oz = imp.At.z - v.Pos.z; double vy = v.Yaw * Math.PI / 180;
            float lx = (float)(ox * Math.Cos(vy) - oz * Math.Sin(vy)), lz = (float)(ox * Math.Sin(vy) + oz * Math.Cos(vy));
            bool wasAlive = v.Alive;
            var w = sim.Strike(a.Id, v, region, DamageType.Stab, sev, gun.Id, bolt ? "bolt" : "shot", lx, imp.Y, lz, dx, 0f, dz, impulse, energy, -dx, 0f, -dz, false);
            if (w == null) return;
            w.CauseEvent = (bolt ? "bolt|" : pellets ? "shot-pellets|" : "shot-in|") + w.CauseEvent;
            if (!bolt && imp.Dist < 1.2f) S.Flags["shotclose:" + v.Id] = imp.Dist; else if (!bolt) S.Flags["shotfar:" + v.Id] = imp.Dist;
            S.Log("ShotHit", a.Id, v.Id, gun.Id, v.Room, v.Pos, $"{region}|{sev}|{(bolt ? "bolt" : pellets ? "pellets x" + count : imp.Through ? "through" : "lodged")}|{imp.Dist:0.0}m", a.PlanId, true);
            if (imp.Through && !bolt && !pellets)
            {
                var exitRegion = region == BodyRegion.Chest ? BodyRegion.Back : region == BodyRegion.Abdomen ? BodyRegion.Back : region == BodyRegion.Back ? BodyRegion.Chest : region;
                Mark(sim, a, v, exitRegion, DamageType.Stab, Math.Max(2, sev - 1), gun.Id, "shot-out");
                shot.Impacts.Add(new ShotImpact { Kind = "exit", Actor = v.Id, Region = exitRegion, Sev = sev - 1, At = imp.At, Y = imp.Y, Dist = imp.Dist + 0.3f });
            }
            else if (!bolt && !pellets) S.Flags["lodged:" + v.Id] = S.Clock;
            imp.Sev = sev;
            // a shot to the head or throat ends it at once; the torso takes the legs away
            if (wasAlive && v.Alive && sev >= 4 && (region == BodyRegion.Head || region == BodyRegion.Neck && !bolt)) sim.Die(v, a.Id, region == BodyRegion.Head ? "머리에 입은 총상" : "목에 입은 총상", w);
            else if (wasAlive && v.Alive && v.Status == ActorStatus.Active && sev >= 4 && vital) sim.Collapse(v, bolt ? "화살에 맞아 쓰러짐" : "총에 맞아 쓰러짐");
        }

        /// <summary>A bullet hole (or a patch of shot) in a wall, a door, the floor or a piece of furniture: a trace, and a mark.</summary>
        static void Hole(Simulation sim, ShotRecord shot, ShotImpact imp, string where, float yaw, float pitch)
        {
            var S = sim.S; int room = S.Layout.RoomAt(imp.At); if (room < 0) room = shot.Room;
            bool bolt = shot.WeaponType == "Crossbow"; bool shotgun = shot.WeaponType == "HuntingShotgun";
            if (bolt && where != "furniture" && where != "door") return;   // a bolt in plaster is its own evidence (the bolt)
            string kor = where == "door" ? "문짝" : where == "floor" ? "바닥" : where == "furniture" ? (FurnitureCatalog.Get(S.Layout.Furniture[imp.Furniture].Type)?.Kor ?? "가구") : "벽";
            // shotgun pellets: one mark for the patch
            if (shotgun && S.Traces.Any(t => t.Type == "BulletHole" && t.Note != null && t.Note.Contains("shot=" + shot.Id) && t.Pos.DistXZ(imp.At) < 0.6f)) return;
            var tr = sim.AddTrace("BulletHole", imp.At, room, shot.By, shot.Intended, shotgun ? 0.22f : 0.08f, 0,
                shotgun ? kor + "에 흩뿌려진 산탄 자국" : bolt ? kor + "에 박힌 석궁 화살 자국" : kor + "에 난 총알 구멍",
                shotgun ? "이쪽을 향해 엽총을 쐈다 — 자국이 퍼진 넓이로 거리를 가늠할 수 있다" : "이 방향에서 총을 쐈다 — 구멍의 각도가 총을 쏜 자리를 가리킨다",
                "누가, 언제 쐈는지");
            if (tr != null)
            {
                tr.Dir = yaw;
                tr.Note = string.Format(System.Globalization.CultureInfo.InvariantCulture, "shot={0};y={1:0.00};pitch={2:0.0};kind={3};f={4};d={5}", shot.Id, imp.Y, pitch, where, imp.Furniture, imp.Door);
            }
            if (where == "furniture" && imp.Furniture >= 0)
            {
                var f = S.Layout.Furniture[imp.Furniture]; int damageBefore = f.Damage;
                if (!f.Marks.Contains(shotgun ? "산탄 자국" : "총알 구멍")) f.Marks.Add(shotgun ? "산탄 자국" : bolt ? "화살이 박혔던 구멍" : "총알 구멍");
                f.Damage = Math.Max(f.Damage, 1);
                S.Emit(GameEventType.Furniture, shot.By, text: "shot", id: f.Id, pos: f.Pos, value: yaw);
                if (f.Damage > damageBefore) sim.CommitFurnitureChange(f, shot.By, "shot", f.Pos, f.Yaw, damageBefore);
            }
        }

        static void Shatter(Simulation sim, Furniture f, Actor by)
        {
            var S = sim.S; if (f.Damage >= 3) return;
            int damageBefore = f.Damage;
            f.Damage = 3; if (!f.Marks.Contains("산산조각 났다")) f.Marks.Add("산산조각 났다");
            sim.Sound(SoundKind.GlassBreak, new P3(f.Pos.f, f.Pos.x, f.Pos.z), 0.7f, by?.Id);
            S.Emit(GameEventType.Furniture, by?.Id, text: "shatter", id: f.Id, pos: f.Pos);
            S.Log("FurnitureHit", by?.Id, room: f.Room, pos: f.Pos, data: f.Type + ":shot", secret: true);
            sim.CommitFurnitureChange(f, by?.Id, "shot", f.Pos, f.Yaw, damageBefore);
        }

        /// <summary>A crossbow bolt stays where it struck: in a body (it moves with it), a door, a piece of furniture or the wall.</summary>
        static void Stick(Simulation sim, ShotRecord shot, Item bow, ShotImpact imp, Actor v, Furniture f, int door, float yaw, float pitch)
        {
            var S = sim.S; int room = S.Layout.RoomAt(imp.At); if (room < 0) room = shot.Room;
            var b = new Item { Id = S.NewId("it"), Type = "Bolt", Name = "석궁 화살", Pos = imp.At, Room = v != null ? -1 : room, Yaw = yaw, LastUser = shot.By, HomeRoom = bow.HomeRoom };
            b.HomePos = b.Pos; b.Surface.Add(v != null ? "embedded" : "stuck");
            if (v != null) { b.Holder = v.Id; if (v.Body.Bleed > 0 || v.Body.Wounds.Count > 0) { b.Bloody = true; b.Surface.Add("blood"); } }
            S.Items[b.Id] = b; imp.Item = b.Id;
            S.Violence.Embedded.Add(new Embedded { Item = b.Id, Actor = v?.Id, Region = imp.Region, Furniture = f?.Id ?? -1, Door = door, At = imp.At, Y = imp.Y, Yaw = yaw, Pitch = pitch });
            S.Emit(GameEventType.ItemMoved, shot.By, data: b.Id, text: v != null ? "embedded" : "stuck", pos: imp.At);
            S.Log("BoltStuck", shot.By, v?.Id, b.Id, room, imp.At, v != null ? "body:" + imp.Region : f != null ? "furniture:" + f.Type : door >= 0 ? "door" : "wall", secret: true);
        }

        /// <summary>The embedded record for an item stuck somewhere (null = an ordinary loose item).</summary>
        public static Embedded EmbeddedOf(GameState S, string item)
        {
            var l = S.Violence?.Embedded; if (l == null || item == null) return null;
            for (int i = l.Count - 1; i >= 0; i--) if (l[i].Item == item) return l[i];
            return null;
        }
        /// <summary>Picked up / pulled out: it is an ordinary item again.</summary>
        public static void Unstick(GameState S, Item it)
        {
            if (it == null) return; var e = EmbeddedOf(S, it.Id); if (e == null) return;
            S.Violence.Embedded.Remove(e); it.Surface.Remove("stuck"); if (it.Surface.Contains("embedded")) { it.Surface.Remove("embedded"); it.Surface.Add("pulled-out"); }
        }
    }

    public sealed partial class Simulation
    {
        /// <summary>A gunshot: heard in every corner of the house (a closed door or a floor only dulls it). Sleepers wake,
        /// everyone is afraid, the curious come running, and every listener remembers roughly where and when.</summary>
        public void Gunshot(P3 pos, string shooter, string gunType)
        {
            int room = S.Layout.RoomAt(pos); if (room < 0) return;
            S.Log("Sound", shooter, room: room, pos: pos, data: SoundKind.Gunshot.ToString(), secret: true);
            if (!Headless) S.Emit(GameEventType.Sound, shooter, text: SoundKind.Gunshot.ToString(), room: room, pos: pos, value: 1f, data: gunType);
            var near = S.Layout.Room(room);
            foreach (var l in S.Actors.Values)
            {
                if (!l.Alive || l.Id == shooter || l.Status == ActorStatus.Unconscious) continue;
                float d = l.Pos.Dist(pos);
                float level = l.Room == room ? 1f : Math.Max(0.32f, 1f - d / 70f);
                int guess = l.Room == room || (l.Pos.f == pos.f && d < 28f) ? room : (S.Layout.Rooms.FirstOrDefault(r => r.Floor == pos.f && (r.Type == RoomType.Landing || r.Type == RoomType.GrandHall || r.Type == RoomType.Stairwell))?.Id ?? room);
                var h = new HeardSound { Kind = SoundKind.Gunshot, Room = room, GuessRoom = guess, Clock = S.Clock, Loud = level, Root = "snd:" + S.Seq };
                var k = S.K(l.Id); k.Heard.Add(h); if (k.Heard.Count > 1500) k.Heard.RemoveRange(0, 300);
                l.Needs.Fear = MathX.Clamp01(l.Needs.Fear + (level > 0.6f ? 0.45f : 0.25f)); l.Needs.Stress = MathX.Clamp01(l.Needs.Stress + 0.2f);
                if (l.Pose == Pose.Sleep) { if (l.IsPlayer) NoteWakeSound(h); Wake(l); }
                OnHeard(l, h, shooter);
            }
        }

        /// <summary>The violence track's per-tick work (from Bodies): a fault is logged, never allowed to stop the world.</summary>
        void ViolenceTick() { try { Violence.Tick(this); } catch (Exception e) { Fault("violence", e); } }
    }
}
