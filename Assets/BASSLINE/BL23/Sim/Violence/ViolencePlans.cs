using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// How the violence track plugs into the murder pipeline: plan options ("Shoot"), a plan layer ("Bind"), the X_ steps the
    /// dispatch chain routes here, the weapons' homes in the house, what an examiner reads on bodies / traces / objects, and
    /// the reveal's captions. Also carrying vs dragging a body (strength against weight).
    /// </summary>
    public static partial class Violence
    {
        static bool F(string kind) => SetPieces.Force == kind;
        static readonly Dictionary<string, Dictionary<string, double>> _aff = new Dictionary<string, Dictionary<string, double>>
        {
            ["P05"] = new Dictionary<string, double> { ["Shoot"] = 0.2, ["DuelingPistol"] = 0.3 },
            ["P06"] = new Dictionary<string, double> { ["Shoot"] = 0.15, ["Bind"] = 0.1 },
            ["P08"] = new Dictionary<string, double> { ["Bind"] = 0.15 },
            ["P11"] = new Dictionary<string, double> { ["Crossbow"] = 0.25, ["Bind"] = 0.1 },
            ["P13"] = new Dictionary<string, double> { ["Shoot"] = 0.1 },
            ["P14"] = new Dictionary<string, double> { ["Bind"] = 0.25 },
            ["P16"] = new Dictionary<string, double> { ["Bind"] = 0.2 },
            ["P18"] = new Dictionary<string, double> { ["Shoot"] = 0.25, ["HuntingShotgun"] = 0.3, ["Crossbow"] = 0.2 },
        };
        static double Aff(string a, string k) => a != null && _aff.TryGetValue(a, out var d) && d.TryGetValue(k, out var v) ? v : 0;

        // ================================================================== where the weapons live (every loop, deterministic)
        static readonly (RoomType room, string item, string[] on)[] Arsenal =
        {
            (RoomType.TrophyRoom, "HuntingShotgun", new[] { "DisplayCase", "Cabinet", "Pedestal", "StuffedBeast" }),
            (RoomType.Armory, "Crossbow", new[] { "DisplayCase", "Chest", "Pedestal" }),   // the armory, where the mansion has one (SocialEventsDesign §5)
            (RoomType.TrophyRoom, "Crossbow", new[] { "Pedestal", "DisplayCase", "Cabinet" }),
            (RoomType.Study, "Revolver", new[] { "Desk", "Console", "Cabinet" }),
            (RoomType.Parlor, "DuelingPistol", new[] { "Console", "SideTable", "Cabinet" }),
            (RoomType.Gallery, "Crossbow", new[] { "Pedestal", "DisplayCase" }),
            // ammunition elsewhere: a box of cartridges in the storage, shells by the boiler, powder in the workshop, bolts in the gallery
            (RoomType.Storage, "Cartridges", new[] { "Shelves", "Crates" }),
            (RoomType.BoilerRoom, "ShotShells", new[] { "Crates", "Pipes" }),
            (RoomType.Workshop, "PowderFlask", new[] { "Workbench", "ToolWall" }),
            (RoomType.Gallery, "BoltQuiver", new[] { "Pedestal", "Console", "SideTable" }),
            (RoomType.TrophyRoom, "BoltQuiver", new[] { "Chest", "Cabinet", "Console" }),
            // bindings
            (RoomType.Storage, "Rope", new[] { "Shelves", "Crates" }), (RoomType.Workshop, "Tape", new[] { "Workbench", "ToolWall" }),
            (RoomType.Wardrobe, "Scarf", new[] { "CostumeRack", "VanityDesk", "DressForm" }),
        };

        /// <summary>Methods.SpawnLoop hook: firearms, the crossbow, their ammunition (never in the same room) and bindings.</summary>
        public static void SpawnLoop(Simulation sim, Dictionary<int, int> used)
        {
            var S = sim.S; var rng = new Rng(S.Rng.CampaignSeed ^ Rng.Hash("arsenal#" + S.Loop), 0x76696F6CUL);
            int n = 0; var placed = new HashSet<string>();
            foreach (var (rt, type, on) in Arsenal)
            {
                if (ItemCatalog.Get(type) == null) continue;
                if (type == "Crossbow" && placed.Contains("Crossbow")) continue;   // one crossbow: the armory's, else the trophy room's, else the gallery's
                if (type == "BoltQuiver" && placed.Contains("BoltQuiver")) continue;
                var room = S.Layout.Rooms.Where(r => r.Type == rt && !r.Void).OrderBy(r => r.Id).FirstOrDefault(); if (room == null) continue;
                var near = new P3(room.Floor, room.Rect.CX, room.Rect.CZ);
                P3? p = ItemPlacement.OnSurface(S.Layout, room, on, near, used, rng, out _) ?? ItemPlacement.OnSurface(S.Layout, room, new[] { "Console", "SideTable", "Desk", "Sideboard", "Chest", "Crates", "Pedestal", "CoffeeTable", "RoundTable", "Workbench", "Shelves" }, near, used, rng, out _);
                if (p == null && ItemPlacement.AgainstWall(S.Layout, room, near, rng, out var wp)) p = wp;
                if (p == null) continue;
                var it = new Item { Id = "it_v" + (++n), Type = type, Pos = p.Value, Room = room.Id, Yaw = rng.Range(0, 360), HomeRoom = room.Id, HomePos = p.Value };
                S.Items[it.Id] = it; placed.Add(type);
            }
        }

        // ================================================================== plan options, fill, layers
        /// <summary>Methods.Options hook: "Shoot" when the planner knows of a gun (or the crossbow) and its ammunition.</summary>
        public static void Options(Simulation sim, Actor a, Actor t, float trustTA, Rng rng, List<(string grammar, double score)> res)
        {
            if (t == null || t.IsPlayer || t.IsButler || !t.Alive) return;
            if (F("Bind")) for (int i = 0; i < res.Count; i++) if (res[i].grammar == "Smother") res[i] = (res[i].grammar, res[i].score + 9);   // test hook: a sleeper to tie
            var c = a.Def; var (gun, ammo) = KnownGun(sim, a);
            if (gun == null || ammo == null && Gun(sim.S, gun.Id).Loaded == 0) return;
            if (!F("Shoot") && !F("Crossbow") && c.Composure < 45) return;
            bool bow = gun.Type == "Crossbow";
            double s = 0.55 + c.P.Aggression * 0.25 + (bow ? 0.22 + c.Infer / 400.0 : 0) + Aff(a.Id, "Shoot") + Aff(a.Id, gun.Type) - (bow ? 0 : c.Deceit / 500.0);
            res.Add(("Shoot", F("Shoot") || F("Crossbow") ? 9 : s));
        }

        static (Item gun, Item ammo) KnownGun(Simulation sim, Actor a)
        {
            var S = sim.S;
            IEnumerable<Item> Known(Func<Item, bool> pred)
            {
                foreach (var it in sim.Carried(a)) if (pred(it)) yield return it;
                var src = SetPieces.Force != null ? S.Items.Values.Where(i => i.Holder == null) : S.K(a.Id).ItemSeen.Keys.Select(S.I).Where(i => i != null && i.Holder == null);
                foreach (var it in src.Where(pred).OrderBy(i => i.Pos.Dist(a.Pos)).ThenBy(i => i.Id, StringComparer.Ordinal).ToList())
                { var r = S.Layout.Room(it.Room); if (r != null && sim.RoomUsable(a, r) && !(r.Type == RoomType.Bedroom && r.Owner != a.Id)) yield return it; }
            }
            bool bowFirst = F("Crossbow");
            foreach (var gun in Known(i => IsRanged(i.Def) && !i.Surface.Contains("burnt") && !i.Surface.Contains("waterlogged")).OrderBy(i => bowFirst ? (i.Type == "Crossbow" ? 0 : 1) : 0).ToList())
            {
                string at = AmmoType(gun.Type);
                var ammo = Known(i => i.Type == at).FirstOrDefault();
                if (ammo != null || Gun(S, gun.Id).Loaded > 0) return (gun, ammo);
            }
            return (null, null);
        }

        /// <summary>Crime.BuildPlan "Shoot": fetch the gun and its ammunition, load, find the target alone, fire.</summary>
        public static void Fill(Simulation sim, MurderPlan plan, Actor a, Rng rng)
        {
            var S = sim.S;
            plan.Steps.RemoveAll(s => s.Kind == "GetWeapon" || s.Kind == "FindWeapon");
            var (gun, ammo) = KnownGun(sim, a);
            if (gun == null) { plan.Grammar = "Ambush"; plan.Steps.Add(new PlanStep { Kind = "FindWeapon" }); plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = plan.Target, Note = "alone" }); plan.Steps.Add(new PlanStep { Kind = "Attack", Target = plan.Target }); return; }
            plan.Weapon = gun.Id; plan.WeaponType = gun.Type;
            if (gun.Holder != a.Id) plan.Steps.Add(new PlanStep { Kind = "GetWeapon", Item = gun.Id });
            if (ammo != null && ammo.Holder != a.Id) plan.Steps.Add(new PlanStep { Kind = "GetItem", Item = ammo.Id });
            plan.Steps.Add(new PlanStep { Kind = "X_Load", Item = gun.Id });
            plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = plan.Target, Note = "alone" });
            plan.Steps.Add(new PlanStep { Kind = "Attack", Target = plan.Target, Note = "shoot" });
            plan.Deadline = S.Clock + 60 * 24;
        }

        public static bool NoWeapon(string head) => head == "Shoot";

        /// <summary>Methods.Augment hook: the "Bind" layer — a sleeper (or a drugged one) is tied before the kill, so the hands
        /// cannot claw; a careful culprit takes the cord away afterwards (the wrist marks stay).</summary>
        public static void Augment(Simulation sim, MurderPlan plan, Actor a, Rng rng, List<string> added)
        {
            var S = sim.S; var c = a.Def; string head = (plan.Grammar ?? "").Split('+')[0].Split('→')[0];
            int atk = plan.Steps.FindIndex(s => s.Kind == "Attack"); if (atk < 0) return;
            bool sleeper = head == "Smother" || head == "NightVisit";
            if (!sleeper && !(F("Bind") && (head == "Strangle" || head == "Ambush"))) return;
            var rope = BindItem(sim, a, plan.Weapon); if (rope == null) return;
            double p = F("Bind") ? 1 : c.Composure >= 60 && (c.P.Aggression > 0.4f || c.Infer >= 65) ? 0.28 + Aff(a.Id, "Bind") : 0;
            if (!rng.Chance(p)) return;
            int first = plan.Steps.FindIndex(s => s.Kind == "Stalk" || s.Kind == "WaitNight" || s.Kind == "VisitRoom" || s.Kind == "X_Sedate");
            if (rope.Holder != a.Id) plan.Steps.Insert(Math.Max(0, first), new PlanStep { Kind = "GetItem", Item = rope.Id });
            atk = plan.Steps.FindIndex(s => s.Kind == "Attack");
            plan.Steps.Insert(atk, new PlanStep { Kind = "X_Bind", Target = plan.Target, Item = rope.Id });
            if (c.Composure >= 70 && rng.Chance(0.7)) { atk = plan.Steps.FindIndex(s => s.Kind == "Attack"); plan.Steps.Insert(atk + 1, new PlanStep { Kind = "X_Unbind", Target = plan.Target, Item = rope.Id }); }
            added.Add("Bind");
        }

        // ================================================================== the dispatch chain (Methods.Handles / PlanThink / Exec)
        public static bool Handles(string kind) => kind == "X_Load" || kind == "X_Bind" || kind == "X_Unbind";
        static bool Acts(string kind) => kind == "X_MLoad" || kind == "X_MBind" || kind == "X_MUnbind" || kind == "X_Bound" || kind == "X_Untie";

        public static Activity PlanThink(Simulation sim, Actor a, MurderPlan plan, PlanStep st, Activity act)
        {
            var S = sim.S;
            switch (st.Kind)
            {
                case "X_Load":
                    {
                        var gun = S.I(st.Item);
                        if (gun == null || gun.Holder != a.Id) { Crime.Replan(sim, a, plan, "weapon missing"); return null; }
                        if (Gun(S, gun.Id).Loaded > 0) { Crime.Advance(sim, a, plan); return null; }
                        if (AmmoFor(sim, a, gun) == null) { Crime.Abort(sim, a, plan, "탄약이 없어서"); return null; }
                        // somewhere nobody watches: a quiet corner of a room they know
                        act.Steps.Add(new ActionStep { Kind = "X_MLoad", Item = gun.Id });
                        act.Label = "볼일"; act.Interruptible = false; return act;
                    }
                case "X_Bind": case "X_Unbind": return BindThink(sim, a, plan, st, act);
            }
            Crime.Advance(sim, a, plan); return null;
        }

        /// <summary>Methods.Exec hook: the action steps of this track.</summary>
        public static bool Exec(Simulation sim, Actor a, ActionStep st)
        {
            if (!Acts(st.Kind) && st.Kind != "PlanPickRope") return false;
            var S = sim.S;
            switch (st.Kind)
            {
                case "X_MLoad":
                    {
                        MurderPlan plan = a.PlanId != null && S.Plans.TryGetValue(a.PlanId, out var pp) ? pp : null;
                        var gun = S.I(st.Item); a.Speed = 0; a.Anim = Anim.Use;
                        string key = "loading:" + a.Id; if (!S.Flags.TryGetValue(key, out var t0)) S.Flags[key] = t0 = S.Tick;
                        if (gun == null || gun.Holder != a.Id) { S.Flags.Remove(key); if (plan != null) Crime.Replan(sim, a, plan, "weapon missing"); sim.Interrupt(a); return true; }
                        if (S.Tick - (long)t0 < LoadTicks(gun.Type)) return true;
                        S.Flags.Remove(key);
                        var why = Load(sim, a, gun);
                        if (plan != null) { if (why == null || Gun(S, gun.Id).Loaded > 0) Crime.Advance(sim, a, plan); else Crime.Abort(sim, a, plan, why); }
                        sim.NextStepPublic(a); return true;
                    }
                case "PlanPickRope":
                    {
                        var it = S.I(st.Item);
                        if (it != null && it.Holder == null && it.Pos.DistXZ(a.Pos) < 1.9f) { sim.PickUp(a, it); a.Anim = Anim.PickUp; }
                        sim.NextStepPublic(a); return true;
                    }
                case "X_MBind": return BindExec(sim, a, st);
                case "X_MUnbind": return UnbindExec(sim, a, st);
                case "X_Bound": return BoundExec(sim, a, st);
                case "X_Untie": return UntieExec(sim, a, st);
            }
            return false;
        }

        // ================================================================== carrying or dragging a body (strength vs weight)
        /// <summary>A body's weight (kg) from height and build.</summary>
        public static float BodyMass(Actor t) { var d = t?.Def; if (d == null) return 70f; return 45f + Math.Max(0, d.HeightCm - 150) * 0.9f + d.Look.Build * 25f; }
        /// <summary>What someone can hoist over the shoulder (kg).</summary>
        public static float LiftCapacity(Actor a) => 35f + 70f * Strength(a);
        /// <summary>Too heavy to lift: the body is dragged by the armpits (slower, marks on the floor).</summary>
        public static bool Dragging(GameState S, Actor a) { if (a?.Carrying == null) return false; var t = S.A(a.Carrying); return t != null && BodyMass(t) > LiftCapacity(a); }
        /// <summary>Walking speed factor with a body (carried 0.5, dragged 0.28).</summary>
        public static float CarrySpeed(GameState S, Actor a) => a.Carrying == null ? 1f : Dragging(S, a) ? 0.28f : 0.5f;
        /// <summary>Stair time factor with a body.</summary>
        public static float CarryStairs(GameState S, Actor a) => a.Carrying == null ? 1f : Dragging(S, a) ? 3.2f : 1.8f;
        public static Anim CarryAnim(GameState S, Actor a) => Dragging(S, a) ? Anim.Drag : Anim.Carry;

        /// <summary>Per tick: heel scuffs behind a dragged body (every ~2 m), a smear of blood if it bleeds, a rug pushed into folds.</summary>
        static void DragMarks(Simulation sim)
        {
            var S = sim.S;
            foreach (var a in S.Actors.Values)
            {
                if (a.Carrying == null || !Dragging(S, a)) continue;   // (by distance moved, not Speed: the player's is not kept)
                var t = S.A(a.Carrying); if (t == null) continue;
                if (a.Anim == Anim.Carry || a.Anim == Anim.None) a.Anim = Anim.Drag;
                double yr = a.Yaw * Math.PI / 180; var heel = new P3(a.Pos.f, a.Pos.x - (float)Math.Sin(yr) * 1.0f, a.Pos.z - (float)Math.Cos(yr) * 1.0f);
                string kx = "dragx:" + a.Id, kz = "dragz:" + a.Id, kn = "dragn:" + a.Id;
                bool have = S.Flags.TryGetValue(kx, out var lx) & S.Flags.TryGetValue(kz, out var lz);
                if (have && Math.Abs(heel.x - lx) + Math.Abs(heel.z - lz) < 2.2) continue;
                int n = S.Flags.TryGetValue(kn, out var nn) ? (int)nn : 0; if (n >= 14) continue;
                S.Flags[kx] = heel.x; S.Flags[kz] = heel.z; S.Flags[kn] = n + 1;
                if (n == 0) S.Log("DragStart", a.Id, t.Id, room: a.Room, pos: a.Pos, data: $"{BodyMass(t):0}kg>{LiftCapacity(a):0}kg", plan: a.PlanId, secret: true);
                int room = S.Layout.RoomAt(heel); if (room < 0) room = a.Room;
                bool bleeding = t.Body.Bleed > 0.01f || (!t.Alive && S.Clock - t.Body.DeathClock < 45 && t.Body.Wounds.Any(w => w.Type == DamageType.Cut || w.Type == DamageType.Stab));
                var tr = bleeding
                    ? sim.AddTrace("BloodSmear", heel, room, a.Id, t.Id, 0.55f, 0, "바닥에 길게 끌린 핏자국", "피를 흘리는 사람(혹은 시신)을 이쪽으로 끌고 갔다", "누가, 어디로 끌고 갔는지")
                    : sim.AddTrace("DragMark", heel, room, a.Id, t.Id, 0.45f, 1, "구두 뒤축이 끌린 두 줄의 자국", "무거운 사람을 겨드랑이째 끌고 갔다 — 들어 올리지 못했다", "누가, 어디로 끌고 갔는지");
                if (tr != null) { tr.Dir = a.Yaw; tr.Note = "drag"; }
                var rug = S.Layout.Room(room)?.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(f => f.Type == "Rug" && RimDist(f, heel) < 0.01f);
                if (rug != null && !rug.Marks.Contains("밀려나 주름져 있다"))
                {
                    rug.Marks.Add(Simulation.RumpledMark); rug.Moved = true;
                    S.Emit(GameEventType.Furniture, a.Id, text: "rug", id: rug.Id, pos: rug.Pos, value: a.Yaw);
                    sim.CommitFurnitureChange(rug, a.Id, "rug", rug.Pos, rug.Yaw, rug.Damage);
                }
            }
            // a finished drag forgets its last mark (checked now and then: the flag table is large)
            if (S.Tick % 50 == 17)
                foreach (var a in S.Actors.Values)
                    if (a.Carrying == null && S.Flags.ContainsKey("dragn:" + a.Id)) { S.Flags.Remove("dragn:" + a.Id); S.Flags.Remove("dragx:" + a.Id); S.Flags.Remove("dragz:" + a.Id); }
        }

        // ================================================================== what an examiner reads
        /// <summary>Methods.BodyNotes hook.</summary>
        public static void BodyNotes(Simulation sim, Actor ex, Actor body, float skill, Rng rng, List<string> lines, List<Prop> props)
        {
            var S = sim.S; var W = body.Body.Wounds;
            void P(string v) => props.Add(new Prop { Kind = PropKind.TraceAt, A = body.Id, Room = body.Room, T0 = S.Clock, T1 = S.Clock, Value = v });
            bool Has(string tag) => W.Any(w => w.CauseEvent != null && w.CauseEvent.StartsWith(tag));
            if (Has("manual")) { lines.Add("· 목 양옆에 손가락 끝 모양의 멍이 줄지어 있다 — 끈이 아니라 맨손으로 졸랐다"); P("manual-strangle"); }
            if (Has("hair")) { lines.Add("· 두피 한쪽이 벌겋게 부었고 머리카락이 한 움큼 뽑혀 나갔다 — 머리채를 잡혀 끌려갔다"); P("hair-pulled"); }
            if (Has("shot-in") || Has("shot-pellets"))
            {
                var e = W.First(w => w.CauseEvent != null && (w.CauseEvent.StartsWith("shot-in") || w.CauseEvent.StartsWith("shot-pellets")));
                bool pellets = e.CauseEvent.StartsWith("shot-pellets");
                lines.Add(pellets ? $"· {WoundText.Region(e.Region)}에 작은 구멍이 여럿 흩어져 있다 — 엽총의 산탄에 맞았다" : $"· {WoundText.Region(e.Region)}에 둥글고 가장자리가 오그라든 구멍 — 총에 맞은 자리다");
                P(pellets ? "shotgun-wound" : "gunshot-wound");
                if (Has("shot-out")) { lines.Add("· 반대편에 더 크고 찢긴 구멍이 있다 — 총알이 몸을 뚫고 나갔다(총알은 그 너머 어딘가에 박혀 있을 것이다)"); P("through-and-through"); }
                else if (!pellets && S.Flags.ContainsKey("lodged:" + body.Id)) { lines.Add("· 나간 구멍이 없다 — 총알이 아직 몸 안에 있다"); P("bullet-lodged"); }
                if (S.Flags.TryGetValue("shotclose:" + body.Id, out var cd) && (skill > 0.35f || ex.IsPlayer)) { lines.Add("· 상처 둘레에 검은 화약 알갱이가 문신처럼 박혀 있다 — 한 뼘 거리에서 쐈다"); P("shot-close"); }
                else if (S.Flags.ContainsKey("shotfar:" + body.Id) && (skill > 0.35f || ex.IsPlayer)) { lines.Add("· 상처 둘레가 깨끗하다 — 화약이 닿지 않을 만큼 떨어진 곳에서 쐈다(제 손으로 쏜 총이라면 이럴 수 없다)"); P("shot-distant"); }
            }
            var bolt = S.Violence.Embedded.FirstOrDefault(e => e.Actor == body.Id && S.I(e.Item)?.Holder == body.Id);
            if (bolt != null || Has("bolt"))
            {
                var bw = W.FirstOrDefault(w => w.CauseEvent != null && w.CauseEvent.StartsWith("bolt"));
                lines.Add(bolt != null ? $"· {WoundText.Region(bolt.Region)}에 석궁 화살이 깊이 박혀 있다 — 깃이 달린 짧고 굵은 화살" : $"· {WoundText.Region(bw?.Region ?? BodyRegion.Chest)}에 좁고 깊은 구멍 — 석궁 화살을 뽑아낸 자리다");
                P(bolt != null ? "bolt-embedded" : "bolt-removed");
            }
            if (S.Flags.ContainsKey("boundmarks:" + body.Id) || Bound(S, body.Id) != null)
            {
                var b = Bound(S, body.Id);
                lines.Add(b != null ? $"· 손목과 발목이 {ItemCatalog.Get(b.Material)?.Kor ?? "끈"}(으)로 묶여 있다" : "· 손목과 발목에 끈에 쓸린 붉은 자국이 둘러져 있다 — 죽기 전에 묶여 있었다");
                P("bound-marks");
            }
            if (Has("bite") && (skill > 0.3f || ex.IsPlayer)) { lines.Add("· 입가와 이 사이에 다른 사람의 핏자국 — 죽기 전에 누군가를 물었다"); P("bit-attacker"); }
        }

        /// <summary>Methods.TraceNotes hook.</summary>
        public static void TraceNotes(Simulation sim, Actor ex, Trace t, List<string> lines, List<Prop> props)
        {
            var S = sim.S;
            void P(string v) => props.Add(new Prop { Kind = PropKind.TraceAt, A = t.Victim, Room = t.Room, T0 = t.Clock, T1 = S.Clock, Value = v, Item = t.Type });
            switch (t.Type)
            {
                case "BulletHole":
                    {
                        var sid = Note(t.Note, "shot"); var shot = S.Violence.Shots.FirstOrDefault(s => s.Id == sid);
                        lines.Add("· 구멍의 각도를 거슬러 올라가면 " + (shot != null ? S.RoomName(shot.Room) + " 쪽, 사람 가슴 높이에서 쏜 것이다" : "총을 쏜 자리를 짐작할 수 있다"));
                        P(t.Desc != null && t.Desc.Contains("산탄") ? "shot-pattern" : "bullet-hole"); break;
                    }
                case "DragMark": if (t.Note == "drag") { lines.Add("· 두 줄의 자국이 나란히 이어진다 — 사람을 들지 못하고 끌고 갔다"); P("drag-marks"); } break;
                case "BloodSmear": if (t.Note == "drag") { lines.Add("· 핏자국이 한 방향으로 길게 늘어났다 — 피 흘리는 몸을 끌고 간 방향이다"); P("blood-drag"); } break;
            }
        }
        static string Note(string note, string key)
        {
            if (note == null) return null;
            foreach (var part in note.Split(';')) { int i = part.IndexOf('='); if (i > 0 && part.Substring(0, i) == key) return part.Substring(i + 1); }
            return null;
        }

        /// <summary>Methods.ItemNotes hook.</summary>
        public static void ItemNotes(Simulation sim, Actor ex, Item it, List<string> lines, List<Prop> props)
        {
            var S = sim.S; string owner = it.Owner != null ? Cast.GivenOf(it.Owner) : null;
            void P(string v, string a = null) => props.Add(new Prop { Kind = PropKind.ItemState, A = a, Item = it.Type, Value = v, Room = it.Room, T0 = S.Clock, T1 = S.Clock });
            if (IsRanged(it.Def))
            {
                var g = S.Violence.Guns.FirstOrDefault(x => x.Item == it.Id);
                if (g != null && g.Shots > 0)
                {
                    if (it.Type == "Revolver") lines.Add(g.Spent > 0 ? $"· 실린더에 빈 탄피 {g.Spent}개와 탄 {g.Loaded}발 — 최근에 {g.Spent}발을 쐈다" : $"· 실린더에 탄 {g.Loaded}발 — 빈 탄피는 누가 털어 냈다. 총구에서 화약 냄새가 난다");
                    else if (it.Type == "Crossbow") lines.Add(g.Loaded > 0 ? "· 시위가 당겨진 채 화살이 걸려 있다 — 한 번 쏜 뒤 다시 장전했다" : "· 시위가 풀려 있고 활대에 새 흠집이 있다 — 최근에 쐈다");
                    else lines.Add("· 총열 안이 검게 그을렸고 매캐한 화약 냄새가 난다 — 최근에 쐈다");
                    P(it.Type == "Crossbow" ? "crossbow-fired" : "gun-fired");
                }
                else lines.Add(it.Type == "Crossbow" ? "· 먼지가 앉은 시위 — 오래 쓰지 않았다" : "· 총열이 깨끗하다 — 오래 쏘지 않았다");
            }
            if (it.Type == "Bolt")
            {
                var e = EmbeddedOf(S, it.Id);
                lines.Add(e != null && e.Actor != null ? "· 시신에 박혀 있는 석궁 화살" : it.Surface.Contains("stuck") ? "· 벽에 깊이 박힌 석궁 화살 — 날아온 방향이 곧게 남아 있다" : it.Surface.Contains("pulled-out") ? "· 피가 말라붙은 화살촉 — 무언가에서 뽑아낸 화살이다" : "· 석궁 화살");
                if (it.HomeRoom >= 0) lines.Add($"· 깃의 색과 모양이 {S.RoomName(it.HomeRoom)}에 있던 화살 묶음과 같다");
                P("bolt");
            }
            if (it.Type == "SpentShell") { lines.Add("· 뇌관에 공이가 찍힌 빈 엽총 탄피 — 엽총을 꺾어 다시 장전한 자리다"); P("spent-shell"); }
            if (it.Type == "Wadding") { lines.Add("· 결투용 권총에 화약과 함께 채우는 종이 패치가 타다 남았다 — 여기서 그 권총을 쐈다"); P("wadding"); }
            if (it.Type == "HairStrands" && owner != null) { lines.Add($"· 뿌리째 뽑힌 긴 머리카락 — {owner}의 머리카락이다. 누군가 세게 움켜쥐고 당겼다"); P("hair-torn", it.Owner); }
            if (IsBinding(it.Def) && it.Surface.Contains("knotted"))
            {
                var b = S.Violence.Bindings.LastOrDefault(x => x.Item == it.Id);
                lines.Add(b != null && !b.Off ? $"· {Cast.GivenOf(b.Actor)}의 손목과 발목을 묶고 있다 — 매듭이 단단하다" : "· 단단한 매듭 자국이 남아 있고 가운데가 쓸려 보풀이 일었다 — 사람을 묶었던 끈이다");
                P("binding-cord", b?.Actor);
            }
        }

        // ================================================================== what a wound reads as (Evidences / WoundText hooks)
        static readonly string[] Tags = { "shot-in", "shot-out", "shot-pellets", "bolt", "manual", "hair", "grip", "bite", "bound", "scratch" };
        /// <summary>The violence track's mark on a wound ("shot-in", "bolt", "manual", ...), or null for an ordinary one.</summary>
        public static string Tag(Wound w)
        {
            var c = w?.CauseEvent; if (c == null) return null;
            int i = c.IndexOf('|'); var t = i >= 0 ? c.Substring(0, i) : c;
            return Array.IndexOf(Tags, t) >= 0 ? t : null;
        }
        /// <summary>The short phrase for a grouped wound line ("총알이 들어간 구멍"), or null.</summary>
        public static string Phrase(Wound w)
        {
            switch (Tag(w))
            {
                case "shot-in": return "총알이 들어간 구멍"; case "shot-out": return "총알이 빠져나간 구멍"; case "shot-pellets": return "엽총 산탄에 맞은 상처";
                case "bolt": return "석궁 화살에 꿰뚫린 상처"; case "manual": return "손가락으로 조른 멍"; case "hair": return "머리채를 잡혀 부은 멍";
                case "grip": return "억센 손으로 짓누른 멍"; case "bite": return "이빨에 물린 상처"; case "bound": return "끈에 쓸린 자국"; case "scratch": return "손톱에 긁힌 상처";
            }
            return null;
        }
        /// <summary>The body card's headline for the fatal wound, or null.</summary>
        public static string CauseLine(Wound w)
        {
            switch (Tag(w))
            {
                case "shot-in": case "shot-out": return "총에 맞아 숨진 것으로 보인다.";
                case "shot-pellets": return "엽총 산탄에 맞아 숨진 것으로 보인다.";
                case "bolt": return "석궁 화살에 맞아 숨진 것으로 보인다.";
                case "manual": return "맨손에 목이 졸려 숨진 것으로 보인다.";
            }
            return null;
        }
        /// <summary>A deterministic grouping key for the card's wound lines (-1 = the ordinary rule).</summary>
        public static int GroupKey(Wound w, int family) { var t = Tag(w); return t == null ? -1 : 1000 + Array.IndexOf(Tags, t) * 10 + family; }

        public static string Describe(Wound w)
        {
            var c = w.CauseEvent; if (c == null) return null;
            string R = WoundText.Region(w.Region), sev = WoundText.Sev(w.Sev), pm = w.Postmortem ? " — 숨진 뒤에 생긴 듯하다" : "", tr = w.Treated ? " — 응급처치를 받았다" : "";
            if (c.StartsWith("shot-in")) return $"{R}에 총상 — 총알이 들어간 구멍 ({sev}){pm}{tr}";
            if (c.StartsWith("shot-out")) return $"{R}에 총알이 빠져나간 구멍 ({sev}){pm}{tr}";
            if (c.StartsWith("shot-pellets")) return $"{R}에 엽총 산탄 상처 ({sev}){pm}{tr}";
            if (c.StartsWith("bolt")) return $"{R}에 석궁 화살이 꿰뚫은 상처 ({sev}){pm}{tr}";
            if (c.StartsWith("manual")) return $"{R}에 손가락으로 조른 멍 ({sev}){pm}";
            if (c.StartsWith("hair")) return $"머리채가 잡혀 뽑힌 두피의 멍{pm}";
            if (c.StartsWith("grip")) return $"{R}을(를) 억센 손으로 짓누른 멍{pm}";
            if (c.StartsWith("bite")) return $"{R}에 사람 이빨에 물린 상처{tr}";
            if (c.StartsWith("bound")) return $"{R}에 끈에 쓸린 자국{pm}";
            if (c.StartsWith("scratch")) return $"{R}에 손톱에 긁힌 상처{tr}";
            return null;
        }

        // ================================================================== the reveal (Methods.Caption / GrammarKor / Verb hooks)
        public static string Caption(GameState S, Incident inc, LedgerEvent e)
        {
            string A = Cast.GivenOf(e.Actor), T = e.Target != null ? Cast.GivenOf(e.Target) : "", R = S.RoomName(e.Room), I = S.I(e.Item)?.Kor ?? "물건";
            var p = (e.Data ?? "").Split('|');
            switch (e.Type)
            {
                case "AssaultBegin":
                    switch (p[0])
                    {
                        case "StrangleManual": return $"{A}, {R}에서 {T}의 목을 두 손으로 움켜쥔다";
                        case "Drown": return $"{A}, {T}의 머리채를 움켜쥐고 물속으로 짓누른다";
                        default: return null;   // Garrote / Smother carry their own captions
                    }
                case "Throttle": return null;
                case "AssaultUnconscious": return $"  └ {T}의 몸부림이 잦아들고, 팔이 축 늘어진다 — {A}은(는) 손을 풀지 않는다";
                case "AssaultEscaped": return $"{T}, {A}의 손을 뿌리치고 빠져나간다";
                case "AssaultReleased": return p.Length > 2 && p[2].StartsWith("목격자") ? $"{A}, 누군가의 기척에 {T}을(를) 놓고 달아난다" : $"{A}, {T}을(를) 놓는다";
                case "Bitten": return $"  └ {T}이(가) {A}의 손을 문다";
                case "Bind": return $"{A}, 정신을 잃은 {T}의 손목과 발목을 {ItemCatalog.Get(p[0])?.Kor ?? "끈"}(으)로 묶는다";
                case "Unbind": return e.Actor == e.Target ? $"{A}, 묶인 끈을 겨우 풀어낸다" : p.Length > 0 && p[0].StartsWith("범인") ? $"{A}, {T}을(를) 묶었던 끈을 풀어 챙긴다" : $"{A}, 묶여 있던 {T}을(를) 풀어 준다";
                case "Reload": return $"{A}, 아무도 없는 곳에서 {I}을(를) 장전한다";
                case "Gunshot": return $"{A}, {R}에서 {T}을(를) 향해 {I}을(를) 쏜다 — 총성이 저택 전체에 울린다";
                case "CrossbowShot": return $"{A}, {R}에서 {T}을(를) 향해 석궁을 쏜다 — 소리는 거의 나지 않는다";
                case "ShotHit": return $"  └ {T}의 {WoundText.Region(Enum.TryParse(p[0], out BodyRegion br) ? br : BodyRegion.Chest)}에 맞는다{(p.Length > 2 && p[2] == "through" ? " — 총알이 몸을 뚫고 나간다" : "")}";
            }
            return null;
        }
        /// <summary>Replay.Script hook: lines the generic captions would get wrong (null = the ordinary caption, "" = none).</summary>
        public static string ReplayLine(GameState S, Incident inc, LedgerEvent e)
        {
            switch (e.Type)
            {
                case "AttackBegin":
                    {
                        // the next thing this attacker does to this target: a shot, or a hold
                        var L = S.Ledger; int i = L.IndexOf(e); if (i < 0) return null;
                        for (int j = i + 1; j < L.Count && L[j].Clock <= e.Clock + 8; j++)
                        {
                            var n = L[j]; if (n.Actor != e.Actor) continue;
                            if (n.Type == "Gunshot") return $"{Cast.GivenOf(e.Actor)}, {S.RoomName(e.Room)}에서 {Cast.GivenOf(e.Target)}에게 {S.I(n.Item)?.Kor ?? "총"}을(를) 겨눈다";
                            if (n.Type == "CrossbowShot") return $"{Cast.GivenOf(e.Actor)}, {S.RoomName(e.Room)}에서 {Cast.GivenOf(e.Target)}에게 석궁을 겨눈다";
                            if (n.Type == "Garrote" || n.Type == "Smother" || n.Type == "Throttle" || n.Type == "AssaultBegin") return $"{Cast.GivenOf(e.Actor)}, {S.RoomName(e.Room)}에서 {Cast.GivenOf(e.Target)}에게 소리 없이 다가간다";
                            if (n.Type == "Strike") break;
                        }
                        return null;
                    }
                case "Strike":
                    {
                        var parts = (e.Data ?? "").Split('/');
                        if (parts.Length >= 4 && (parts[3] == "shot" || parts[3] == "bolt")) return "";   // ShotHit says it
                        return null;
                    }
                case "CarryStart":
                    {
                        bool drag = S.Ledger.Any(x => x.Type == "DragStart" && x.Actor == e.Actor && x.Target == e.Target && x.Tick >= e.Tick && x.Tick <= e.Tick + 40);
                        return drag ? $"{Cast.GivenOf(e.Actor)}, {Cast.GivenOf(e.Target)}을(를) 들어 올리지 못하고 겨드랑이를 붙잡아 바닥에 질질 끌고 간다"
                                    : $"{Cast.GivenOf(e.Actor)}, {Cast.GivenOf(e.Target)}을(를) 어깨에 들쳐 멘다";
                    }
            }
            return null;
        }

        public static string GrammarKor(string g) => g == "Shoot" ? "총(혹은 석궁)으로 쏘기" : g == "Bind" ? "잠든 사람을 먼저 묶어 두기" : null;
        public static (string conn, string fin)? Verb(string g)
        {
            switch (g)
            {
                case "Shoot": return ("아무도 없을 때를 노려 총을 쏘고", "아무도 없을 때를 노려 총을 쏜다");
                case "Bind": return ("저항하지 못하게 손발을 묶어 두고", "저항하지 못하게 손발을 묶어 둔다");
            }
            return null;
        }
    }

    public sealed partial class Simulation
    {
        // ================================================================== the player's hands (Interaction)
        /// <summary>Load the held gun / crossbow from carried ammunition. The kernel owns the time it takes (it can't fire until ready).</summary>
        public string PlayerLoad()
        {
            var gun = Held(P, d => Violence.IsRanged(d)); if (gun == null) return "손에 든 총이 없다";
            var g = Violence.Gun(S, gun.Id); if (S.Tick < g.ReadyAt) return "장전하는 중이다";
            var why = Violence.Load(this, P, gun); if (why != null) return why;
            g.ReadyAt = S.Tick + Violence.LoadTicks(gun.Type);
            return gun.Type == "Crossbow" ? "석궁 시위를 천천히 당겨 화살을 걸었다" : gun.Kor + "을(를) 장전했다";
        }
        /// <summary>Fire the held weapon from the camera (eye height above the floor, bearing, elevation in degrees).</summary>
        public ShotRecord PlayerFire(float eyeY, float yaw, float pitch)
        {
            var gun = Held(P, d => Violence.IsRanged(d)); if (gun == null) return null;
            var g = Violence.Gun(S, gun.Id); if (S.Tick < g.ReadyAt) return null;
            var shot = Violence.Fire(this, P, gun, Math.Max(0.4f, eyeY - 0.08f), yaw, pitch, null);
            if (shot != null)
            {
                var hit = shot.Impacts.FirstOrDefault(i => i.Kind == "body")?.Actor;
                if (hit != null) { shot.Intended = hit; var t = S.A(hit); if (t != null && t.Alive && !t.IsButler) Relations.Change(S, t.Id, Cast.Player, fear: 0.8f, grudge: 0.7f, trust: -1f, memory: "민혁이 나를 쐈다", tag: "enemy"); if (!S.Flags.ContainsKey("attackstart:" + Cast.Player + ":" + hit)) { S.Flags["attackstart:" + Cast.Player + ":" + hit] = S.Clock; S.Log("AttackBegin", Cast.Player, hit, room: t?.Room ?? -1, pos: t?.Pos); } }
            }
            return shot;
        }
        public int PlayerRounds() { var gun = Held(P, d => Violence.IsRanged(d)); return gun == null ? -1 : Violence.Gun(S, gun.Id).Loaded; }
        /// <summary>Tie someone up with a carried cord / rope / tape / scarf (they must be unable to resist).</summary>
        public string PlayerBind(Actor t)
        {
            var rope = Carried(P).FirstOrDefault(i => Violence.IsBinding(i.Def)); if (rope == null) return "묶을 끈이 없다";
            if (!Violence.CanBind(S, P, t, null, out var why)) return why;
            bool gag = rope.Type == "Tape" || rope.Type == "Scarf" || Carried(P).Any(i => i.Type == "Scarf" || i.Type == "Tape" || i.Type == "Towel");
            Violence.Bind(this, P, t, rope, true, true, gag, "player");
            if (t.Alive && !t.IsButler) Relations.Change(S, t.Id, Cast.Player, fear: 0.6f, grudge: 0.5f, trust: -0.8f, memory: "민혁이 나를 묶었다", tag: "enemy");
            EmergencyTrigger(4);
            return LineBank.FixParticles(Cast.GivenOf(t.Id) + "의 손목과 발목을 " + rope.Kor + "(으)로 묶었다" + (gag ? " — 입도 막았다" : ""));
        }
        public string PlayerUnbind(Actor t)
        {
            if (Violence.Bound(S, t?.Id) == null) return "묶여 있지 않다";
            if (t.Pos.DistXZ(P.Pos) > 2f) return "너무 멀다";
            Violence.Unbind(this, P, t, "민혁이 풀어 주었다", keep: true);
            if (t.Alive) Relations.Change(S, t.Id, Cast.Player, trust: 0.3f, like: 0.25f, memory: "민혁이 묶인 나를 풀어 줬다");
            return LineBank.FixParticles(Cast.GivenOf(t.Id) + "을(를) 풀어 주었다");
        }
        /// <summary>Seize someone with the held cord (from behind if they face away) or bare hands. Null if it cannot start.</summary>
        public Assault PlayerSeize(Actor t)
        {
            if (t == null || t.Pos.f != P.Pos.f || t.Pos.DistXZ(P.Pos) > 1.3f) return null;
            var cord = Held(P, d => Methods.IsCord(d));
            float bearing = MathX.AngleDeg(P.Pos.x - t.Pos.x, P.Pos.z - t.Pos.z);
            bool behind = Math.Abs(MathX.DeltaAngle(t.Yaw, bearing)) > 115f;
            var water = Violence.WaterNear(S, t.Pos, 1.8f);
            var kind = water != null && cord == null ? AssaultKind.Drown : cord != null ? (behind ? AssaultKind.StrangleRear : AssaultKind.StrangleFront) : t.Pose == Pose.Sleep || t.Status == ActorStatus.Unconscious ? AssaultKind.Smother : AssaultKind.StrangleManual;
            var x = Violence.Begin(this, P, t, kind, cord, null, water);
            if (x != null && t.Alive && !t.IsButler) Relations.Change(S, t.Id, Cast.Player, fear: 0.9f, grudge: 0.8f, trust: -1f, memory: "민혁이 나를 죽이려 했다", tag: "enemy");
            return x;
        }
        public void PlayerLetGo() { var x = Violence.DoingTo(S, Cast.Player); if (x != null) Violence.Release(this, x, "민혁이 손을 놓았다"); }
        /// <summary>Someone (the player) pulled / shoved the attacker off: the hold breaks.</summary>
        public bool ViolenceBreak(string attackerId, string by)
        {
            var x = Violence.DoingTo(S, attackerId); if (x == null) return false;
            Violence.Release(this, x, (by != null ? Cast.GivenOf(by) : "누군가") + "이(가) 떼어 놓았다", witness: true); return true;
        }
    }
}
