using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// BL23 murder content, second wave (user: "트릭·흉기·살인 방법이 너무 적다 / 증거가 너무 많아 복잡하다 /
    /// 살인에 도움을 주는 방들도 있으면 좋겠다").
    ///  • New killing methods: a cord from behind (교살), a push from a stair top or the gallery rail (추락 — 사고 위장),
    ///    a rigged machine the victim habitually uses (감전 함정), a sleeping draught and then a pillow (수면제 + 질식),
    ///    poison planted ahead in the victim's own bedtime drink or a gift (시간차 독 — 잠긴 방의 죽음).
    ///  • Rooms that help a crime: the incinerator (증거 소각), the pool and its filter (익사 · 흉기 수장), the cold store
    ///    (시신 은닉 · 사망 시각), the boiler/machine rooms (소음 은폐), the greenhouse (독초 · 흙 속 은닉), the darkroom
    ///    (약품 · 어둠), the stairwell and the gallery rail (추락).
    ///  • Tricks: the key slid back under the door (열쇠 되돌리기 밀실), a forged farewell note (자살 위장).
    /// Every method is a plan of real, interruptible steps chosen by who the culprit is (traits, trade, what they know of
    /// rooms and objects, the chapter rules); each leaves two or three meaningful clues (what · where · when) rather than a
    /// flood, and plants a first impression (accident / natural death / suicide / sealed room) that the trial breaks with
    /// the right clue (Logic.cs, TrialMethods.cs). Game state only — no real-world method detail.
    /// </summary>
    public static partial class Methods
    {
        // ================================================================== vocabulary
        public static readonly string[] Kinds = { "Strangle", "Push", "Shock", "Smother", "Bedtime" };
        static string Head(string g) => string.IsNullOrEmpty(g) ? "" : g.Split('+')[0].Split('→')[0];
        public static bool IsMine(string g) => Kinds.Contains(Head(g));
        /// <summary>The scene is meant to look untouched by anyone (accident / natural death): no locked-room, body move, planted weapon or mutilation layers.</summary>
        public static bool Staged(string g) { var h = Head(g); return h == "Push" || h == "Shock" || h == "Smother" || h == "Bedtime"; }
        /// <summary>The culprit is elsewhere when death comes (the device or the dose does it).</summary>
        public static bool Remote(string g) { var h = Head(g); return h == "Shock" || h == "Bedtime"; }
        /// <summary>No blade to find again when a plan is revised: the plan itself is the weapon.</summary>
        public static bool NoWeapon(string g) { var h = Head(g); return h == "Push" || h == "Shock" || h == "Smother" || h == "Bedtime" || Violence.NoWeapon(h); }   // (+ violence track: Shoot keeps its gun)
        public static bool IsMode(string m) => m == "strangle" || m == "push" || m == "smother" || Violence.IsMode(m);             // (+ violence track: throttle / garrote / drown / shoot)
        static bool F(string kind) => SetPieces.Force == kind;
        static bool Forced => SetPieces.Force != null;

        public static bool IsCord(ItemDef d) => d != null && d.Dmg == DamageType.Choke && d.Sev >= 2;

        // ================================================================== who reaches for what (a trade, a habit, a body)
        // Familiarity, not "murder stats": a cook knows the kitchen and bedtime tea, an engineer knows cables and fixings,
        // an undertaker knows how bodies cool, a bassist knows strings, a stylist knows scarves and scissors.
        static readonly Dictionary<string, Dictionary<string, double>> _aff = new Dictionary<string, Dictionary<string, double>>
        {
            ["P02"] = new Dictionary<string, double> { ["Smother"] = 0.25, ["FakeNote"] = 0.35, ["Bedtime"] = 0.15 },
            ["P04"] = new Dictionary<string, double> { ["Strangle"] = 0.2, ["Bedtime"] = 0.15, ["Burn"] = 0.35, ["PaletteKnife"] = 0.3, ["Chisel"] = 0.2, ["Statuette"] = 0.2 },
            ["P05"] = new Dictionary<string, double> { ["Push"] = 0.25, ["FakeNote"] = 0.1, ["Statuette"] = 0.25, ["Decanter"] = 0.2 },
            ["P06"] = new Dictionary<string, double> { ["Dismember"] = 0.2, ["ColdHide"] = 0.3, ["Push"] = 0.1, ["Shock"] = 0.1, ["Crowbar"] = 0.25 },
            ["P07"] = new Dictionary<string, double> { ["Push"] = 0.3, ["Bottle"] = 0.25, ["Decanter"] = 0.2, ["CueStick"] = 0.2 },
            ["P08"] = new Dictionary<string, double> { ["Strangle"] = 0.35, ["PianoWire"] = 0.45 },
            ["P09"] = new Dictionary<string, double> { ["FakeNote"] = 0.2, ["Candlestick"] = 0.2 },
            ["P10"] = new Dictionary<string, double> { ["Dismember"] = 0.3, ["Bedtime"] = 0.35, ["Smother"] = 0.1, ["KitchenKnife"] = 0.35, ["Cleaver"] = 0.3, ["IcePick"] = 0.3 },
            ["P11"] = new Dictionary<string, double> { ["Shock"] = 0.5, ["Noise"] = 0.3, ["Wrench"] = 0.3, ["Pliers"] = 0.2, ["Crowbar"] = 0.15 },
            ["P12"] = new Dictionary<string, double> { ["Smother"] = 0.15, ["Decanter"] = 0.2, ["Candlestick"] = 0.15 },
            ["P13"] = new Dictionary<string, double> { ["Strangle"] = -0.3, ["Push"] = 0.15, ["CueStick"] = 0.3 },
            ["P14"] = new Dictionary<string, double> { ["Dismember"] = 0.3, ["Smother"] = 0.35, ["ColdHide"] = 0.35, ["Strangle"] = 0.2, ["Scalpel"] = 0.45 },
            ["P15"] = new Dictionary<string, double> { ["FakeNote"] = 0.25, ["LetterOpener"] = 0.45, ["Bedtime"] = 0.1 },
            ["P16"] = new Dictionary<string, double> { ["Strangle"] = 0.2, ["Scissors"] = 0.4, ["Scarf"] = 0.35, ["Bury"] = 0.2 },
            ["P17"] = new Dictionary<string, double> { ["Push"] = 0.15, ["Noise"] = 0.15, ["Dump"] = 0.15 },
            ["P18"] = new Dictionary<string, double> { ["Dismember"] = 0.1, ["Push"] = 0.3, ["Drown"] = 0.25, ["Strangle"] = 0.15, ["ColdHide"] = 0.2, ["Crowbar"] = 0.3, ["PipeSection"] = 0.3, ["Wrench"] = 0.2 },
        };
        public static double Aff(string actor, string key) => actor != null && key != null && _aff.TryGetValue(actor, out var d) && d.TryGetValue(key, out var v) ? v : 0;

        /// <summary>Weapon preference beyond raw harm: one's own trade tools, a little randomness so the same person does not always reach for the same thing.</summary>
        public static double WeaponPref(Actor a, string type, Rng rng) => Aff(a.Id, type) + rng.F() * 0.6;

        // ================================================================== knowledge helpers (the planner only uses what it has seen)
        public static bool Knows(Simulation sim, Actor a, Room r) => r != null && (Forced || sim.S.K(a.Id).Facts.Contains("visited:" + r.Id));
        public static Room KnownRoom(Simulation sim, Actor a, RoomType t) => sim.S.Layout.Rooms.Where(r => r.Type == t).OrderBy(r => r.Id).FirstOrDefault(r => Knows(sim, a, r) && sim.RoomUsable(a, r));

        static IEnumerable<Item> KnownItems(Simulation sim, Actor a, Func<Item, bool> pred)
        {
            var S = sim.S;
            foreach (var it in sim.Carried(a).ToList()) if (pred(it)) yield return it;
            var src = Forced ? S.Items.Values.Where(i => i.Holder == null) : S.K(a.Id).ItemSeen.Keys.Select(S.I).Where(i => i != null && i.Holder == null);
            foreach (var it in src.Where(pred).OrderBy(i => i.Pos.Dist(a.Pos)).ThenBy(i => i.Id, StringComparer.Ordinal).ToList())
            {
                var r = S.Layout.Room(it.Room);
                if (r != null && r.Type == RoomType.Bedroom && r.Owner != a.Id) continue;   // other people's things stay theirs
                if (r == null || sim.RoomUsable(a, r)) yield return it;
            }
        }

        public static Item Cord(Simulation sim, Actor a) => KnownItems(sim, a, i => IsCord(i.Def) && !i.Surface.Contains("burnt")).FirstOrDefault();
        public static Item Sedative(Simulation sim, Actor a) => KnownItems(sim, a, i => i.Type == "Sedative" && !i.Surface.Contains("used")).FirstOrDefault();
        static Item CutTool(Simulation sim, Actor a) => KnownItems(sim, a, i => i.Type == "Pliers" || i.Type == "Scissors" || i.Type == "Chisel" || i.Type == "PaletteKnife" || i.Type == "KitchenKnife" || i.Type == "Scalpel").FirstOrDefault();

        // ================================================================== planner: which of the new methods fit this culprit, this target, this house
        public static List<(string grammar, double score)> Options(Simulation sim, Actor a, Actor t, float trustTA, Rng rng)
        {
            var S = sim.S; var c = a.Def; var res = new List<(string, double)>();
            if (t == null || t.IsButler || !t.Alive) return res;
            bool npc = !t.IsPlayer;
            // 교살: a cord from behind — quiet, bloodless, needs reach and nerve
            var cord = Cord(sim, a);
            if (cord != null && npc && (c.Composure >= 50 || Forced))
            {
                double s = 0.85 + c.Composure / 400.0 + (c.P.Aggression < 0.5f ? 0.15 : 0) + (c.HeightCm + 6 >= t.Def.HeightCm ? 0.1 : -0.35) + Aff(a.Id, "Strangle") + (cord.Type == "PianoWire" ? Aff(a.Id, "PianoWire") : 0) + (cord.Type == "Scarf" ? Aff(a.Id, "Scarf") : 0);
                if (S.RuleActive("CH22") || S.RuleActive("CH23")) s += 0.15;   // noise or darkness swallows a short struggle
                res.Add(("Strangle", F("Strangle") ? 9 : s));
            }
            // 추락: a push where the target passes a stair top or the gallery rail — reads as a slip
            if (npc && (HasEdge(sim, a, t) || F("Push")))
            {
                double s = 0.8 + (c.Deceit >= 55 ? 0.25 : 0) + (c.P.Aggression > 0.45f ? 0.15 : 0) + (c.HeightCm >= t.Def.HeightCm - 5 ? 0.1 : -0.25) + Aff(a.Id, "Push");
                if (S.RuleActive("CH23") || S.RuleActive("CH03")) s += 0.12;
                res.Add(("Push", F("Push") ? 9 : s));
            }
            // 감전 함정: a machine the target habitually uses, a stripped cable and a wet floor — reads as a fault
            var shock = ChooseShock(sim, a, t);
            if (shock != null)
            {
                double s = shock.Score + (S.RuleActive("CH16") ? 0.2 : 0);
                res.Add(("Shock", F("Shock") ? 9 : s));
            }
            // 수면제 + 질식: a draught in the target's cup, then a pillow once they drop off — reads as dying in one's sleep
            var sed = Sedative(sim, a);
            if (sed != null && npc && S.Layout.BedroomOf(t.Id) != null && ((c.Infer >= 60 && c.Deceit >= 55) || Forced || F("FakeNote") || F("KeySlide")))
            {
                double s = 0.85 + c.Deceit / 300.0 + (c.P.Aggression < 0.4f ? 0.2 : 0) + Aff(a.Id, "Smother") + (S.RuleActive("CH15") ? 0.25 : 0);
                res.Add(("Smother", F("Smother") || F("FakeNote") || F("KeySlide") ? 9 : s));
            }
            // 시간차 독: the target's own bedtime drink (or a gift left on the desk) dosed in the afternoon — the culprit is
            // far away with people around when it takes effect, and the door was locked from the inside by the victim
            var vial = SetPieces.PoisonSource(sim, a);
            if (vial != null && npc && S.Layout.BedroomOf(t.Id) != null && (c.Composure >= 60 && c.Infer >= 60 || Forced) && BedtimeItem(sim, a, t, out _) != null)
            {
                double s = 0.8 + c.Composure / 300.0 + Aff(a.Id, "Bedtime");
                res.Add(("Bedtime", F("Bedtime") ? 9 : s));
            }
            Violence.Options(sim, a, t, trustTA, rng, res);   // --- violence track: "Shoot" (a gun or the crossbow, with its ammunition)
            return res;
        }

        public static bool NeedsTarget(string g) => IsMine(g);

        /// <summary>Build the steps of one of the new methods (the generic weapon step is replaced where a method brings its own means).</summary>
        public static void Fill(Simulation sim, MurderPlan plan, Actor a, Rng rng)
        {
            var S = sim.S; var t = S.A(plan.Target);
            plan.Steps.RemoveAll(s => s.Kind == "GetWeapon" || s.Kind == "FindWeapon");
            switch (Head(plan.Grammar))
            {
                case "Strangle":
                    {
                        var cord = Cord(sim, a);
                        if (cord == null) { plan.Grammar = "Ambush"; plan.Steps.Add(new PlanStep { Kind = "FindWeapon" }); plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = plan.Target, Note = "alone" }); plan.Steps.Add(new PlanStep { Kind = "Attack", Target = plan.Target }); return; }
                        plan.Weapon = cord.Id; plan.WeaponType = cord.Type;
                        if (cord.Holder != a.Id) plan.Steps.Add(new PlanStep { Kind = "GetWeapon", Item = cord.Id });
                        plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = plan.Target, Note = "alone" });
                        plan.Steps.Add(new PlanStep { Kind = "Attack", Target = plan.Target, Note = "strangle" });
                        break;
                    }
                case "Push":
                    plan.Weapon = null; plan.WeaponType = null;
                    plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = plan.Target, Note = "edge" });
                    plan.Steps.Add(new PlanStep { Kind = "Attack", Target = plan.Target, Note = "push" });
                    plan.Deadline = S.Clock + 60 * 30;
                    break;
                case "Shock":
                    {
                        var tc = ChooseShock(sim, a, t);
                        if (tc == null) { plan.Grammar = "Ambush"; plan.Steps.Add(new PlanStep { Kind = "FindWeapon" }); plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = plan.Target, Note = "alone" }); plan.Steps.Add(new PlanStep { Kind = "Attack", Target = plan.Target }); return; }
                        Tricks.FillTrap(sim, plan, a, tc);
                        break;
                    }
                case "Smother":
                    {
                        var sed = Sedative(sim, a);
                        plan.Weapon = null; plan.WeaponType = null;
                        if (sed == null) { plan.Grammar = "NightVisit"; plan.Steps.Add(new PlanStep { Kind = "WaitNight" }); plan.Steps.Add(new PlanStep { Kind = "VisitRoom", Target = plan.Target }); plan.Steps.Add(new PlanStep { Kind = "Attack", Target = plan.Target, Note = "smother" }); return; }
                        if (sed.Holder != a.Id) plan.Steps.Add(new PlanStep { Kind = "GetItem", Item = sed.Id });
                        plan.Steps.Add(new PlanStep { Kind = "X_Sedate", Target = plan.Target, Item = sed.Id });
                        plan.Steps.Add(new PlanStep { Kind = "X_WaitDrowsy", Target = plan.Target });
                        plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = plan.Target, Note = "asleep" });
                        plan.Steps.Add(new PlanStep { Kind = "Attack", Target = plan.Target, Note = "smother" });
                        plan.Deadline = S.Clock + 60 * 30;
                        break;
                    }
                case "Bedtime":
                    {
                        var vial = SetPieces.PoisonSource(sim, a); var dose = BedtimeItem(sim, a, t, out bool gift); var bed = S.Layout.BedroomOf(plan.Target);
                        if (vial == null || dose == null || bed == null) { plan.Grammar = "Ambush"; plan.Steps.Add(new PlanStep { Kind = "FindWeapon" }); plan.Steps.Add(new PlanStep { Kind = "Stalk", Target = plan.Target, Note = "alone" }); plan.Steps.Add(new PlanStep { Kind = "Attack", Target = plan.Target }); return; }
                        if (vial.Holder != a.Id) plan.Steps.Add(new PlanStep { Kind = "GetItem", Item = vial.Id });
                        if (gift && dose.Holder != a.Id) plan.Steps.Add(new PlanStep { Kind = "GetItem", Item = dose.Id });
                        plan.Steps.Add(new PlanStep { Kind = "X_PlantPoison", Target = plan.Target, Item = vial.Id, Room = bed.Id, Note = dose.Id + (gift ? "|gift" : "") });
                        plan.Steps.Add(new PlanStep { Kind = "X_WaitBedtime", Target = plan.Target });
                        plan.Weapon = vial.Id; plan.WeaponType = vial.Type; plan.KillRoom = bed.Id;
                        plan.Deadline = S.Clock + 60 * 40;
                        break;
                    }
            }
        }

        // ================================================================== layers: rooms that help, tricks that mislead
        /// <summary>Optional layers chosen by the planner's traits and what they know of the house (applied after the generic concealment).</summary>
        public static void Augment(Simulation sim, MurderPlan plan, Actor a, Rng rng)
        {
            var S = sim.S; var c = a.Def; var t = S.A(plan.Target); if (t == null) return;
            string head = Head(plan.Grammar);
            int attack = plan.Steps.FindIndex(s => s.Kind == "Attack" || s.Kind == "Drown");
            var added = new List<string>();
            // --- 열쇠 되돌리기 밀실: the kill happens in the victim's own bedroom (night visit, or asleep after the draught)
            bool bedroomKill = (head == "NightVisit" || head == "Smother") && attack >= 0;
            if (bedroomKill && !plan.Steps.Any(s => s.Kind == "X_Seal") && (F("KeySlide") || (c.Infer >= 65 && c.Composure >= 60 && rng.Chance(0.5))))
            {
                plan.Steps.RemoveAll(s => s.Kind == "LockRoom" || s.Kind == "MoveBody");
                attack = plan.Steps.FindIndex(s => s.Kind == "Attack"); plan.Steps.Insert(attack + 1, new PlanStep { Kind = "X_KeySlide", Target = plan.Target });
                added.Add("KeySlide");
            }
            // --- 가짜 유서: the draught (and its empty packet) or the bedtime poison, plus a note in the victim's hand
            if ((head == "Smother" || head == "Bedtime") && (F("FakeNote") || (c.Deceit >= 65 && c.P.Morality < 0.6f && rng.Chance(0.45 + Aff(a.Id, "FakeNote")))))
            {
                if (head == "Smother") { int at = plan.Steps.FindIndex(s => s.Kind == "Attack"); plan.Steps.Insert(at + 1, new PlanStep { Kind = "X_FakeNote", Target = plan.Target }); }
                else { var pl = plan.Steps.FirstOrDefault(s => s.Kind == "X_PlantPoison"); if (pl != null) pl.Note += "|note"; }
                added.Add("FakeNote");
            }
            // --- 소음 은폐: a lure into the boiler/machine room, where the boiler turned to full swallows every sound
            if (head == "Lure" && attack >= 0)
            {
                var loud = new[] { KnownRoom(sim, a, RoomType.BoilerRoom), KnownRoom(sim, a, RoomType.MachineRoom) }.Where(r => r != null).FirstOrDefault();
                if (loud != null && (F("Noise") || rng.Chance(0.3 + Aff(a.Id, "Noise") + (c.Infer >= 75 ? 0.1 : 0))))
                {
                    foreach (var st in plan.Steps.Where(s => s.Kind == "Invite" || s.Kind == "GoRoom" || s.Kind == "WaitVictim")) st.Room = loud.Id;
                    plan.KillRoom = loud.Id;
                    int wv = plan.Steps.FindIndex(s => s.Kind == "WaitVictim"); if (wv >= 0) plan.Steps.Insert(wv, new PlanStep { Kind = "X_Noise", Room = loud.Id });
                    added.Add("Noise");
                }
            }
            // --- 흉기 처분: the incinerator, the pool (its filter), the greenhouse soil — instead of a closet or a sink
            int dispose = plan.Steps.FindIndex(s => s.Kind == "HideWeapon" || s.Kind == "WashWeapon");
            if (dispose >= 0 && plan.Weapon != null && !Remote(plan.Grammar))
            {
                var inc = KnownRoom(sim, a, RoomType.Incinerator); var pool = KnownRoom(sim, a, RoomType.Pool); var gh = KnownRoom(sim, a, RoomType.Greenhouse);
                var wdef = ItemCatalog.Get(plan.WeaponType);
                var opts = new List<(string k, double s)>();
                if (inc != null) opts.Add(("Burn", F("Burn") ? 9 : 0.45 + c.Composure / 250.0 + Aff(a.Id, "Burn")));
                if (pool != null && wdef != null && wdef.Mass >= 0.1f) opts.Add(("Dump", F("Dump") ? 9 : 0.4 + (wdef.Mat == Mat.Metal ? 0.1 : 0) + Aff(a.Id, "Dump")));
                if (gh != null && wdef != null && wdef.Size <= 0.5f) opts.Add(("Bury", F("Bury") ? 9 : 0.35 + Aff(a.Id, "Bury")));
                if (opts.Count > 0 && (F("Burn") || F("Dump") || F("Bury") || rng.Chance(0.55)))
                {
                    var pick = opts.OrderByDescending(o => o.s + rng.F() * 0.3).First().k;
                    string kind = pick == "Burn" ? "X_Burn" : pick == "Dump" ? "X_DumpWater" : "X_Bury";
                    plan.Steps[dispose] = new PlanStep { Kind = kind };
                    if (pick == "Burn") plan.Steps.RemoveAll(s => s.Kind == "CleanUp");   // the bloody clothes go into the fire too
                    added.Add(pick);
                }
            }
            // --- 사후 해체: the body to a room with a saw, cut up, pieces spread over the house (MethodsDismember.cs)
            AugmentDismember(sim, plan, a, rng, added);
            // --- 저온 보관실: a careful carrier hides the body in the cold (it reads as dying earlier, and is found late)
            if (!Staged(plan.Grammar) && attack >= 0 && !added.Contains("Dismember") && KnownRoom(sim, a, RoomType.ColdStorage) != null && head != "Press" && head != "Drown" && head != "Trap"
                && !plan.Steps.Any(s => s.Kind == "X_Seal" || s.Kind == "X_KeySlide" || s.Kind == "X_Tod")
                && (F("ColdHide") || (c.Composure >= 70 && rng.Chance(0.2 + Aff(a.Id, "ColdHide")))))
            {
                var mv = plan.Steps.FirstOrDefault(s => s.Kind == "MoveBody");
                if (mv == null) { mv = new PlanStep { Kind = "MoveBody", Target = plan.Target }; plan.Steps.Insert(plan.Steps.FindIndex(s => s.Kind == "Attack" || s.Kind == "Drown") + 1, mv); }
                mv.Note = "cold";
                plan.Steps.RemoveAll(s => s.Kind == "LockRoom");
                added.Add("ColdHide");
            }
            Violence.Augment(sim, plan, a, rng, added);   // --- violence track: "Bind" (tie the sleeper before the kill)
            foreach (var x in added) plan.Grammar += "+" + x;
            if (added.Count > 0) S.Dev($"methods {a.Id} {string.Join("+", added)}");
        }

        // ================================================================== places: edges (stairs, rail), machines, bedtime drinks
        static P3 Top(Stair s) => s.A.f > s.B.f ? s.A : s.B;
        static P3 Bottom(Stair s) => s.A.f > s.B.f ? s.B : s.A;

        /// <summary>The gallery ring over the hall's void on the upper floor (the "rail"), if any.</summary>
        public static Room Void(GameState S) => S.Layout.Rooms.FirstOrDefault(r => r.Void);

        static bool HasEdge(Simulation sim, Actor a, Actor t)
        {
            var S = sim.S; var bed = S.Layout.BedroomOf(t.Id); if (bed == null) return false;
            // someone who sleeps upstairs comes down a stair (or along the gallery rail) every morning
            if (S.Layout.Stairs.Any(s => Top(s).f == bed.Floor && Knows(sim, a, S.Layout.Room(s.A.f > s.B.f ? s.RoomA : s.RoomB)))) return true;
            var v = Void(S); var land = S.Layout.Rooms.FirstOrDefault(r => r.Type == RoomType.Landing);
            return v != null && land != null && v.Floor == bed.Floor && Knows(sim, a, land);
        }

        /// <summary>Is the target standing at a drop right now? (a stair top on its floor, or the rail of the gallery over the hall)</summary>
        public static bool AtEdge(Simulation sim, Actor t, out string kind, out int id)
        {
            var S = sim.S; kind = null; id = -1;
            if (t == null || !t.Alive || t.StairId >= 0 || t.CarriedBy != null) return false;
            foreach (var s in S.Layout.Stairs.OrderBy(s => s.Id))
            {
                var top = Top(s); if (top.f != t.Pos.f || top.f == Bottom(s).f) continue;
                if (t.Pos.DistXZ(top) < 2.0f) { kind = "stair"; id = s.Id; return true; }
            }
            var v = Void(S);
            if (v != null && v.Floor == t.Pos.f && !v.Rect.Contains(t.Pos.x, t.Pos.z))
            {
                var r = v.Rect; float dx = Math.Max(Math.Max(r.x0 - t.Pos.x, 0), t.Pos.x - r.x1), dz = Math.Max(Math.Max(r.z0 - t.Pos.z, 0), t.Pos.z - r.z1);
                if (Math.Sqrt(dx * dx + dz * dz) < 1.3) { kind = "rail"; id = v.Id; return true; }
            }
            return false;
        }

        /// <summary>Nobody else close enough to see a shove at the edge (the gallery is busy, but not every second): no one awake within 10 m on that floor.</summary>
        public static bool EdgeClear(Simulation sim, Actor a, Actor t)
        {
            var S = sim.S;
            foreach (var x in S.Actors.Values)
            {
                if (x == a || x == t || !x.Alive || x.Status != ActorStatus.Active || x.Pose == Pose.Sleep || x.Pos.f != t.Pos.f) continue;
                if (x.Pos.DistXZ(t.Pos) < 10f) return false;
            }
            return t.Following == null && t.FollowedBy == null;
        }

        /// <summary>Where a pusher waits when the target is out of sight: the gallery (or the stair top) on the floor the target sleeps on.</summary>
        public static P3? EdgeWait(Simulation sim, Actor a, Actor t)
        {
            var S = sim.S; var bed = S.Layout.BedroomOf(t.Id); if (bed == null) return null;
            var land = S.Layout.Rooms.Where(r => r.Type == RoomType.Landing && r.Floor == bed.Floor).OrderBy(r => r.Id).FirstOrDefault();
            if (land != null)
            {
                // the stretch of gallery closest to the target's door (everyone from that wing comes this way)
                float x = Math.Max(land.Rect.x0 + 0.8f, Math.Min(land.Rect.x1 - 0.8f, bed.Rect.CX)), z = Math.Max(land.Rect.z0 + 0.8f, Math.Min(land.Rect.z1 - 0.8f, bed.Rect.CZ));
                return sim.SnapPublic(new P3(land.Floor, x, z));
            }
            var st = S.Layout.Stairs.Where(s => Top(s).f == bed.Floor).OrderBy(s => Top(s).DistXZ(new P3(bed.Floor, bed.Rect.CX, bed.Rect.CZ))).ThenBy(s => s.Id).FirstOrDefault();
            if (st == null) return null;
            var tp = Top(st); return sim.SnapPublic(new P3(tp.f, tp.x + 2.2f, tp.z));
        }

        /// <summary>The cold store as a place to leave a body (any floor: the carrier takes the stairs), if the carrier knows it and can get there.</summary>
        public static int ColdDump(Simulation sim, Actor a, Actor victim)
        {
            var S = sim.S; var cold = KnownRoom(sim, a, RoomType.ColdStorage); if (cold == null || victim == null) return -1;
            var pr = Pathfinder.Find(S.Layout, victim.Pos, sim.RandomPointIn(cold, S.R(Stream.PlanTie)), sim.DoorCostFor(a));
            return pr.Ok ? cold.Id : -1;
        }

        static readonly string[] Machines = { "Washer", "Terminal", "Jukebox", "Gramophone", "Arcade", "FilmProjector" };

        /// <summary>감전 함정: a machine with a use-spot in a room where the planner has seen the target more than once, and a blade to strip the cable.</summary>
        public static Tricks.TrapChoice ChooseShock(Simulation sim, Actor a, Actor t)
        {
            var S = sim.S; var k = S.K(a.Id); var c = a.Def;
            if (t == null) return null;
            if (!Forced && (c.Infer < 70 && Aff(a.Id, "Shock") < 0.3 || c.Composure < 55)) return null;
            var tool = CutTool(sim, a); if (tool == null) return null;
            var habit = k.Sightings.Where(s => s.Target == t.Id && s.IdConf > 0.5f).GroupBy(s => s.Room).Where(g => g.Count() >= (Forced ? 1 : 2)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => g.Key).ToList();
            IEnumerable<Room> rooms = habit.Select(S.Layout.Room).Where(r => r != null);
            if (F("Shock")) rooms = rooms.Concat(S.Layout.Rooms.Where(r => r.Furniture.Any(f => Machines.Contains(S.Layout.Furniture[f].Type))).OrderBy(r => r.Id));
            foreach (var room in rooms.Distinct())
            {
                if (RoomInfo.IsPassage(room.Type) || room.Type == RoomType.Bedroom || !sim.RoomUsable(a, room)) continue;
                foreach (var fid in room.Furniture)
                {
                    var f = S.Layout.Furniture[fid]; if (!Machines.Contains(f.Type) || f.Damage > 0 || S.Traps.Any(x => x.Furniture == fid && (x.Active || x.FiredAt >= 0))) continue;
                    var sp = room.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Furniture == fid);
                    if (sp == null) continue;
                    double score = 0.8 + c.Infer / 250.0 + Aff(a.Id, "Shock") + habit.Count * 0.04;
                    return new Tricks.TrapChoice { Kind = "Shock", Furniture = fid, Spot = sp.Id, Room = room.Id, Tool = tool.Id, Score = score };
                }
            }
            return null;
        }

        /// <summary>The thing the victim will drink or eat alone at bedtime: their own tea/thermos/snack in their room, or a gift the planner can leave there.</summary>
        public static Item BedtimeItem(Simulation sim, Actor a, Actor t, out bool gift)
        {
            var S = sim.S; gift = false; var bed = S.Layout.BedroomOf(t.Id); if (bed == null) return null;
            var own = S.Items.Values.Where(i => i.Owner == t.Id && i.Holder == null && i.Room == bed.Id && (i.Def?.Consumable == true || i.Type == "Thermos") && !i.Surface.Contains("consumed") && !i.Surface.Contains("poisoned")).OrderBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
            if (own != null) return own;
            gift = true;
            return KnownItems(sim, a, i => i.Owner == null && (i.Type == "Chocolate" || i.Type == "Candy" || i.Type == "Tea" || i.Type == "Bread") && !i.Surface.Contains("poisoned")).FirstOrDefault();
        }
    }
}
