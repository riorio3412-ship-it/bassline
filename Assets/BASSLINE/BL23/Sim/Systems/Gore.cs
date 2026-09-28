using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    // enums are append-only (saved as ints)
    public enum GoreKind { Impact, CastOff, Arterial, Pool, Smear, Handprint, NailScratch, Topple, SawMark, Ooze }
    public enum SeverPart { Head, ArmL, ArmR, HandL, HandR, LegL, LegR }   // names == Item.Note codes used by MethodsDismember
    public enum SeverPlacement { AtBody, Scatter }

    /// <summary>
    /// One blood / struggle mark on or around a body (kernel side; Game/Gore draws it). Stored on <see cref="Body.GoreMarks"/>.
    /// Conventions: Pos = where the mark starts on the floor plane; H = source height above that floor (m); Dir = travel bearing
    /// (MathX.AngleDeg: 0 = +z, 90 = +x); Pitch = travel elevation (+ up). Per kind:
    ///   Impact / Arterial / CastOff — a spray from (Pos, H) along Dir/Pitch (CastOff: Pos = the attacker's spot, the arc behind them).
    ///   Pool — under the body at Pos; Power = size; Region = the bleeding region (the pool gathers under it).
    ///   Smear — a vertical drag on the wall that Dir points at, from H down to H − Power (m).
    ///   Handprint — on the wall Dir points at (from Pos), at H; Door ≥ 0 → on that door's frame instead.
    ///   NailScratch — floor scratches at Pos; Door ≥ 0 → claw marks on that door at H.
    ///   Topple — Furniture tipped over toward Dir; Chapter = when; Righted = set upright again (the render undoes it).
    ///   SawMark / Ooze — floor marks at a cut joint (Ooze: the thin seep of a postmortem wound).
    /// </summary>
    [Serializable]
    public sealed class GoreMark
    {
        public GoreKind Kind; public P3 Pos; public int Room = -1;
        public float H;        // source height above floor, m
        public float Dir;      // travel bearing, deg (MathX.AngleDeg convention)
        public float Pitch;    // travel elevation, deg (+ up)
        public float Power;    // 0..1
        public BodyRegion Region; public DamageType Dmg; public int Sev; public bool Postmortem;
        public uint Seed; public long Tick; public double Clock; public int Chapter;
        public string By, Item, Trace; public int Furniture = -1, Door = -1; public bool Righted;
    }

    /// <summary>
    /// Kernel gore: blood spray, pools, smears, handprints, nail scratches and toppled furniture from the fight the ledger records,
    /// and the one place bodies are cut into pieces (<see cref="Dismember"/>, the adapter target of MethodsDismember.CutUp).
    /// Pure and deterministic: all randomness comes from a local PCG stream seeded by (campaign, victim, tick, wounds, salt) —
    /// never a shared stream — and strikes never touch ids, the ledger, flags or events, so a fight that kills nobody changes
    /// nothing but these marks. Pieces themselves are ordinary Items (Type "SeveredPart", Owner = victim, Note = part code).
    /// </summary>
    public static class Gore
    {
        public const int MaxMarks = 48;   // per body; when full drop the oldest Impact, never Arterial/Pool/Topple/SawMark/Handprint
        internal static readonly bool EmitSceneTraces = true;    // the TraceFactory hook (gore-render: GoreDecals.Handles/Create) is in the tree, so BloodSpray/Struggle render
        static readonly GoreMark[] NoMarks = new GoreMark[0];
        const string ToppledLine = "넘어져 있다 (몸싸움 중 쓰러진 듯)";
        const string ToppledSpot = "~toppled";
        static readonly SeverPart[] DefaultParts = { SeverPart.ArmL, SeverPart.ArmR, SeverPart.LegL, SeverPart.LegR };

        // ================================================================== helpers
        static Rng LocalRng(GameState S, Actor v, string salt) =>
            new Rng(S.Rng.CampaignSeed ^ Rng.Hash(v.Id + "|" + S.Tick + "|" + v.Body.Wounds.Count + "|" + salt), 0x9E37UL);

        static float Wrap(float a) { a %= 360f; if (a > 180f) a -= 360f; if (a <= -180f) a += 360f; return a; }
        static float Bearing(P3 from, P3 to) { float dx = to.x - from.x, dz = to.z - from.z; return dx * dx + dz * dz < 1e-6f ? 0f : MathX.AngleDeg(dx, dz); }
        static P3 Along(P3 p, float bearing, float d) { double r = bearing * Math.PI / 180.0; return new P3(p.f, p.x + (float)Math.Sin(r) * d, p.z + (float)Math.Cos(r) * d); }
        static bool Sharp(DamageType t) => t == DamageType.Cut || t == DamageType.Stab;
        static bool Bloodless(DamageType t) => t == DamageType.Choke || t == DamageType.Drown || t == DamageType.Shock || t == DamageType.Burn || t == DamageType.None;
        static bool ArmOrHand(BodyRegion r) => r == BodyRegion.ArmL || r == BodyRegion.ArmR || r == BodyRegion.HandL || r == BodyRegion.HandR;
        static bool Limb(BodyRegion r) => ArmOrHand(r) || r == BodyRegion.ShoulderL || r == BodyRegion.ShoulderR;
        static bool Protected(GoreKind k) => k == GoreKind.Arterial || k == GoreKind.Pool || k == GoreKind.Topple || k == GoreKind.SawMark || k == GoreKind.Handprint;
        static bool Lying(Actor v) => v.Status == ActorStatus.Unconscious || v.Pose == Pose.LieBack || v.Pose == Pose.LieFront || v.Pose == Pose.LieSide || v.Pose == Pose.Sleep;

        static float RegionH(BodyRegion r)
        {
            switch (r)
            {
                case BodyRegion.Head: return 1.55f; case BodyRegion.Neck: return 1.45f;
                case BodyRegion.Chest: case BodyRegion.Back: return 1.30f;
                case BodyRegion.ShoulderL: case BodyRegion.ShoulderR: return 1.40f;
                case BodyRegion.Abdomen: return 1.05f;
                case BodyRegion.ArmL: case BodyRegion.ArmR: return 1.15f;
                case BodyRegion.HandL: case BodyRegion.HandR: return 1.00f;
                case BodyRegion.LegL: case BodyRegion.LegR: return 0.60f;
                case BodyRegion.FootL: case BodyRegion.FootR: return 0.10f;
            }
            return 1.1f;
        }
        /// <summary>Height the blood leaves the body from: standing heights by region, 0.2 m for someone lying down, lower when seated or crouched.</summary>
        static float SourceH(Actor v, BodyRegion r)
        {
            if (Lying(v)) return 0.20f;
            float h = RegionH(r);
            if (v.Pose == Pose.Sit || v.Pose == Pose.Kneel || v.Pose == Pose.Crouch || v.Pose == Pose.Slumped) h = Math.Max(0.2f, h * 0.6f);
            return h;
        }
        static float StrikeDir(Actor att, Actor v)
        {
            if (att != null && att != v && att.Pos.f == v.Pos.f && att.Pos.DistXZ(v.Pos) > 0.01f) return Wrap(Bearing(att.Pos, v.Pos));
            return Wrap(v.Yaw + 180f);
        }

        /// <summary>Append a mark (cap: oldest Impact first, then the oldest unprotected mark; a Topple is never dropped). Bumps GoreRev.</summary>
        static GoreMark Add(GameState S, Actor v, GoreMark m)
        {
            var b = v.Body; var list = b.GoreMarks ?? (b.GoreMarks = new List<GoreMark>());
            if (list.Count >= MaxMarks)
            {
                int drop = list.FindIndex(x => x.Kind == GoreKind.Impact);
                if (drop < 0) drop = list.FindIndex(x => !Protected(x.Kind) && x.Kind != GoreKind.Topple);
                if (drop < 0) return null;
                list.RemoveAt(drop);
            }
            b.GoreRev++;
            m.Seed = (uint)Rng.Hash(v.Id + "|" + S.Tick + "|" + b.GoreRev);
            m.Tick = S.Tick; m.Clock = S.Clock; if (m.Kind != GoreKind.Topple) m.Chapter = S.Chapter;
            if (m.Room < 0) m.Room = v.Room;
            list.Add(m);
            return m;
        }

        // ================================================================== strikes
        /// <summary>A live blow landed (Combat.Strike). Marks only: never AddTrace / NewId / Log / Flags / Emit / S.R.</summary>
        public static void OnStrike(Simulation sim, Actor att, Actor v, Wound w, string cause, Item weapon)
        {
            if (sim == null || v == null || w == null || w.Postmortem) return;
            if (cause == "resist" || cause == "defense") return;   // parries and pushes back: scored from the ledger at death
            var t = w.Type; if (Bloodless(t) || t == DamageType.Fall) return;
            var S = sim.S; var rng = LocalRng(S, v, "strike");
            float dir = StrikeDir(att, v), h = SourceH(v, w.Region); int sev = Math.Max(1, Math.Min(4, w.Sev));
            string by = att?.Id ?? w.By; string item = weapon?.Id;
            var marks = v.Body.GoreMarks;
            int liveBy = att == null ? 0 : v.Body.Wounds.Count(x => !x.Postmortem && x.By == att.Id);
            int castoffs = marks == null ? 0 : marks.Count(x => x.Kind == GoreKind.CastOff);
            if (Sharp(t))
            {
                Add(S, v, new GoreMark { Kind = GoreKind.Impact, Pos = v.Pos, Room = v.Room, H = h, Dir = dir, Pitch = rng.Range(-12f, 12f), Power = MathX.Clamp01(0.3f + 0.15f * sev), Region = w.Region, Dmg = t, Sev = w.Sev, By = by, Item = item });
                bool artery = (w.Region == BodyRegion.Neck && w.Sev >= 2) || ((w.Region == BodyRegion.ArmL || w.Region == BodyRegion.ArmR || w.Region == BodyRegion.HandL || w.Region == BodyRegion.HandR || w.Region == BodyRegion.LegL || w.Region == BodyRegion.LegR) && w.Sev >= 3);
                int arteries = marks == null ? 0 : marks.Count(x => x.Kind == GoreKind.Arterial);
                if (artery && arteries < 2)
                {
                    float side = rng.Chance(0.5) ? 1f : -1f;
                    Add(S, v, new GoreMark { Kind = GoreKind.Arterial, Pos = v.Pos, Room = v.Room, H = h, Dir = Wrap(dir + side * rng.Range(60f, 110f)), Pitch = rng.Range(5f, 30f), Power = 0.9f, Region = w.Region, Dmg = t, Sev = w.Sev, By = by, Item = item });
                }
                if (att != null && liveBy >= 2 && castoffs < 6)
                {
                    float pitch = t == DamageType.Cut && w.Sev >= 3 ? rng.Range(60f, 80f) : rng.Range(35f, 70f);
                    var at = att.Pos.f == v.Pos.f ? att.Pos : v.Pos;
                    Add(S, v, new GoreMark { Kind = GoreKind.CastOff, Pos = at, Room = S.Layout.RoomAt(at) >= 0 ? S.Layout.RoomAt(at) : v.Room, H = 1.9f, Dir = Wrap(dir + 180f + rng.Range(-15f, 15f)), Pitch = pitch, Power = MathX.Clamp01(0.45f + 0.1f * sev), Region = w.Region, Dmg = t, Sev = w.Sev, By = by, Item = item });
                }
            }
            else if (t == DamageType.Blunt || t == DamageType.Crush)
            {
                int sameRegion = v.Body.Wounds.Count(x => !x.Postmortem && x.Region == w.Region && (x.Type == DamageType.Blunt || x.Type == DamageType.Crush));
                if (sameRegion >= 2 || (w.Region == BodyRegion.Head && w.Sev >= 3))
                    Add(S, v, new GoreMark { Kind = GoreKind.Impact, Pos = v.Pos, Room = v.Room, H = h, Dir = dir, Pitch = rng.Range(-10f, 20f), Power = 0.4f, Region = w.Region, Dmg = t, Sev = w.Sev, By = by, Item = item });
                int bluntBy = att == null ? 0 : v.Body.Wounds.Count(x => !x.Postmortem && x.By == att.Id && (x.Type == DamageType.Blunt || x.Type == DamageType.Crush));
                if (att != null && bluntBy >= 3 && castoffs < 6)
                {
                    var at = att.Pos.f == v.Pos.f ? att.Pos : v.Pos;
                    Add(S, v, new GoreMark { Kind = GoreKind.CastOff, Pos = at, Room = S.Layout.RoomAt(at) >= 0 ? S.Layout.RoomAt(at) : v.Room, H = 1.9f, Dir = Wrap(dir + 180f + rng.Range(-15f, 15f)), Pitch = 70f, Power = 0.5f, Region = w.Region, Dmg = t, Sev = w.Sev, By = by, Item = item });
                }
            }
        }

        /// <summary>Damage to a body that is already dead: a thin ooze and nothing else — no arterial spray, no cast-off (the
        /// absence is the readable sign of 사후 damage). Cuts made by <see cref="Dismember"/> are marked there instead.</summary>
        public static void OnPostmortem(Simulation sim, Actor att, Actor v, Wound w, string cause, Item weapon)
        {
            if (sim == null || v == null || w == null) return;
            if (cause == "dismember" || cause == "sever") return;
            if (Bloodless(w.Type)) return;
            var S = sim.S; var rng = LocalRng(S, v, "postmortem");
            Add(S, v, new GoreMark { Kind = GoreKind.Ooze, Pos = v.Pos, Room = v.Room, H = 0.1f, Dir = Wrap(rng.Range(-180f, 180f)), Power = MathX.Clamp01(0.15f + 0.05f * Math.Max(1, w.Sev)), Region = w.Region, Dmg = w.Type, Sev = w.Sev, Postmortem = true, By = att?.Id ?? w.By, Item = weapon?.Id });
        }

        // ================================================================== death
        /// <summary>Called by Combat.Die just before Cases.OnDeath (the body is down, its BloodPool trace laid).</summary>
        public static void OnDeath(Simulation sim, Actor v, Wound w)
        {
            if (sim == null || v == null) return;
            var S = sim.S; var rng = LocalRng(S, v, "death");
            var t = w?.Type ?? DamageType.None; int room = v.Room; string by = v.Body.DeathBy ?? w?.By;
            var added = new List<GoreMark>();
            void Put(GoreMark m) { var r = Add(S, v, m); if (r != null) added.Add(r); }
            bool bleeding = w != null && !Bloodless(t) && t != DamageType.Fall;
            if (bleeding)
            {
                float power = MathX.Clamp01(0.35f + v.Body.BloodLoss * 0.8f + (Sharp(t) ? 0.3f : 0f));
                Put(new GoreMark { Kind = GoreKind.Pool, Pos = v.Pos, Room = room, H = 0f, Dir = Wrap(v.Yaw), Power = power, Region = w.Region, Dmg = t, Sev = w.Sev, By = by });
                if (v.Pose == Pose.Slumped)   // sat down against the wall: the back drags a smear down it
                    Put(new GoreMark { Kind = GoreKind.Smear, Pos = v.Pos, Room = room, H = 1.1f, Dir = Wrap(v.Yaw + 180f), Pitch = -90f, Power = 0.8f, Region = BodyRegion.Back, Dmg = t, Sev = w.Sev, By = by });
            }
            else if (t == DamageType.Fall)
            {
                Put(new GoreMark { Kind = GoreKind.Pool, Pos = v.Pos, Room = room, H = 0f, Dir = Wrap(v.Yaw), Power = 0.3f, Region = w.Region, Dmg = t, Sev = w.Sev, By = by });
                Put(new GoreMark { Kind = GoreKind.Impact, Pos = v.Pos, Room = room, H = 0.2f, Dir = Wrap(v.Yaw + rng.Range(-40f, 40f)), Pitch = rng.Range(0f, 15f), Power = 0.45f, Region = w.Region, Dmg = t, Sev = w.Sev, By = by });
            }
            if (t == DamageType.Choke)
            {
                // the hands clawing at the cord leave nail scratches on the floor beside the body, and on a door within reach
                for (int i = 0; i < 2; i++)
                {
                    var hand = Along(v.Pos, v.Yaw + (i == 0 ? -90f : 90f), 0.25f);
                    var at = Along(hand, rng.Range(-180f, 180f), rng.Range(0f, 0.3f));
                    if (S.Layout.RoomAt(at) != room) at = hand;
                    Put(new GoreMark { Kind = GoreKind.NailScratch, Pos = at, Room = room, H = 0f, Dir = Wrap(rng.Range(-180f, 180f)), Power = rng.Range(0.35f, 0.6f), Region = i == 0 ? BodyRegion.HandL : BodyRegion.HandR, Dmg = t, Sev = w.Sev, By = by });
                }
                var d = NearestDoor(S, v.Pos, 1.2f);
                if (d != null)
                    Put(new GoreMark { Kind = GoreKind.NailScratch, Pos = v.Pos, Room = room, H = rng.Range(0.6f, 0.95f), Dir = Wrap(Bearing(v.Pos, d.Pos)), Power = 0.5f, Region = BodyRegion.HandR, Dmg = t, Sev = w.Sev, By = by, Door = d.Id });
            }
            // a bloody hand reached for the wall: live bleeding cut on the hand/arm/shoulder, or heavy blood loss
            bool bloodyHand = v.Body.Wounds.Any(x => !x.Postmortem && Sharp(x.Type) && x.Sev >= 2 && Limb(x.Region)) || v.Body.BloodLoss >= 0.4f;
            if (bloodyHand && FindWall(S, v, 1.2f, rng, out float wallDir, out P3 wallAt))
            {
                float hh = rng.Range(0.9f, 1.3f);
                var src = v.Body.Wounds.LastOrDefault(x => !x.Postmortem && Sharp(x.Type) && Limb(x.Region));
                Put(new GoreMark { Kind = GoreKind.Handprint, Pos = wallAt, Room = room, H = hh, Dir = wallDir, Power = 0.7f, Region = src?.Region ?? BodyRegion.HandR, Dmg = src?.Type ?? t, Sev = src?.Sev ?? 0, By = v.Id });
                Put(new GoreMark { Kind = GoreKind.Smear, Pos = wallAt, Room = room, H = hh, Dir = wallDir, Pitch = -90f, Power = MathX.Clamp01(hh - 0.4f), Region = src?.Region ?? BodyRegion.HandR, Dmg = src?.Type ?? t, Sev = src?.Sev ?? 0, By = v.Id });
            }
            // the fight itself: furniture knocked over around where it happened
            float score = StruggleF(S, v, out P3 centre, out int resists);
            int n = score >= 6 ? 3 : score >= 4 ? 2 : score >= 2 ? 1 : 0;
            if (n > 0)
            {
                var r = S.Layout.Room(room);
                var cands = r == null ? new List<Furniture>() : r.Furniture.Select(i => S.Layout.Furniture[i])
                    .Where(f => f.Pos.f == centre.f && Toppleable(S, f) && f.Pos.DistXZ(centre) <= 2.5f)
                    .OrderBy(f => f.Pos.DistXZ(centre)).ThenBy(f => f.Id).Take(4).ToList();
                rng.Shuffle(cands);
                foreach (var f in cands.Take(n).OrderBy(f => f.Id))
                {
                    var def = FurnitureCatalog.Get(f.Type);
                    Put(new GoreMark { Kind = GoreKind.Topple, Pos = f.Pos, Room = f.Room, H = f.H, Dir = Wrap(Bearing(centre, f.Pos) + rng.Range(-25f, 25f)), Power = MathX.Clamp01(0.4f + score * 0.06f), Furniture = f.Id, Chapter = S.Chapter, By = by, Dmg = t });
                    if (!f.Marks.Contains(ToppledLine)) f.Marks.Add(ToppledLine);
                    if (r != null) foreach (var si in r.Spots) { var sp = S.Layout.Spots[si]; if (sp.Furniture == f.Id && sp.Occupant == null) sp.Occupant = ToppledSpot; }
                }
            }
            // at most one clue trace per death (only once the Game renders these types)
            if (EmitSceneTraces)
            {
                var topples = added.Where(m => m.Kind == GoreKind.Topple).ToList();
                Trace tr = null; IEnumerable<GoreMark> related = null;
                if (topples.Count > 0)
                {
                    var f0 = S.Layout.Furniture[topples[0].Furniture];
                    string kor = FurnitureCatalog.Get(f0.Type)?.Kor ?? "가구";
                    tr = sim.AddTrace("Struggle", centre, room, by, v.Id, 0.5f, 0, $"넘어진 {kor}", "이 자리에서 격한 몸싸움이 있었다" + (resists > 0 ? " — 피해자가 저항했다" : ""), "누구와 싸웠는지");
                    related = topples;
                }
                else
                {
                    var sprays = (v.Body.GoreMarks ?? new List<GoreMark>()).Where(m => (m.Kind == GoreKind.Arterial || m.Kind == GoreKind.CastOff) && !m.Postmortem).ToList();
                    if (sprays.Count > 0)
                    {
                        bool arterial = sprays.Any(m => m.Kind == GoreKind.Arterial);
                        var main = sprays.Where(m => m.Kind == (arterial ? GoreKind.Arterial : GoreKind.CastOff)).OrderByDescending(m => m.Power).ThenBy(m => m.Tick).First();
                        var at = Along(v.Pos, main.Dir, 0.6f); if (S.Layout.RoomAt(at) != room) at = v.Pos;
                        tr = sim.AddTrace("BloodSpray", at, room, by, v.Id, 0.5f, 0, "벽에 튄 핏자국",
                            arterial ? "피해자는 선 채로 공격당했다 — 피가 벽까지 뿜어졌다" : "흉기를 여러 번 크게 휘둘렀다 — 천장 가까이까지 핏방울이 튀었다", "누가 휘둘렀는지");
                        if (tr != null) tr.Dir = main.Dir;
                        related = sprays;
                    }
                }
                if (tr != null && related != null) foreach (var m in related) m.Trace = tr.Id;
            }
            v.Body.GoreRev++;
        }

        static Door NearestDoor(GameState S, P3 p, float within)
        {
            Door best = null; float bd = within;
            foreach (var d in S.Layout.Doors)
            {
                if (d.Pos.f != p.f) continue; float dd = d.Pos.DistXZ(p);
                if (dd <= within && (best == null || dd < bd)) { best = d; bd = dd; }
            }
            return best;
        }

        /// <summary>The nearest wall (or blocking furniture, or a doorway) within reach: 0.1 m steps along 8 bearings on the nav grid.</summary>
        static bool FindWall(GameState S, Actor v, float maxD, Rng rng, out float bearing, out P3 at)
        {
            var g = S.Layout.Nav(v.Pos.f); int room = v.Room; int start = rng.R(8);
            for (int step = 1; step * 0.1f <= maxD + 1e-4f; step++)
            {
                float d = step * 0.1f;
                for (int j = 0; j < 8; j++)
                {
                    float ang = Wrap(((start + j) % 8) * 45f);
                    var p = Along(v.Pos, ang, d); int k = g.CellOf(p.x, p.z);
                    if (k >= 0 && g.Walkable(k) && g.Room[k] == room) continue;
                    bearing = ang; at = Along(v.Pos, ang, Math.Max(0f, d - 0.25f)); return true;
                }
            }
            bearing = 0; at = v.Pos; return false;
        }

        /// <summary>Light, movable furniture a fight can knock over (the same set the Game's physics props use).</summary>
        internal static bool ToppleKind(Furniture f)
        {
            var def = f == null ? null : FurnitureCatalog.Get(f.Type);
            return def != null && def.Movable && def.Mass <= 10f && f.W * f.D <= 0.36f && (def.Blocks || f.Type == "Chair") && f.Type != "Rug" && f.Type != "DoorLogger" && f.H >= 0.3f;
        }
        /// <summary>…and free to fall now: not already down, nobody sitting on it.</summary>
        static bool Toppleable(GameState S, Furniture f)
        {
            if (!ToppleKind(f) || f.Marks.Contains(ToppledLine)) return false;
            var r = S.Layout.Room(f.Room);
            if (r != null) foreach (var si in r.Spots) { var sp = S.Layout.Spots[si]; if (sp.Furniture == f.Id && sp.Occupant != null) return false; }
            return true;
        }

        /// <summary>How hard the fight was, from the ledger (pure): blows landed on the victim by others, the victim's pushes back
        /// (Resist ×1.5, their own resist/defense strikes), and live defensive cuts on the arms and hands. The window is the
        /// first AttackBegin on the victim in the last 60 clock-minutes before death (or those 60 minutes when there is none).</summary>
        public static int StruggleScore(Simulation sim, Actor v) => sim == null || v == null ? 0 : (int)Math.Floor(StruggleF(sim.S, v, out _, out _));

        static float StruggleF(GameState S, Actor v, out P3 centre, out int resists)
        {
            centre = v.Pos; resists = 0;
            double now = v.Alive || v.Body.DeathClock < 0 ? S.Clock : v.Body.DeathClock, from = now - 60;
            var L = S.Ledger;
            // first event at or after `from` (clocks never go down within a loop)
            int lo = 0, hi = L.Count;
            while (lo < hi) { int mid = (lo + hi) >> 1; if (L[mid].Clock < from) lo = mid + 1; else hi = mid; }
            LedgerEvent ab = null;
            for (int i = lo; i < L.Count && L[i].Clock <= now + 1e-9; i++) if (L[i].Type == "AttackBegin" && L[i].Target == v.Id) { ab = L[i]; break; }
            double start = ab?.Clock ?? from;
            float score = 0;
            for (int i = lo; i < L.Count && L[i].Clock <= now + 1e-9; i++)
            {
                var e = L[i]; if (e.Clock < start) continue;
                if (e.Type == "Strike")
                {
                    if (e.Target == v.Id && e.Actor != v.Id) score += 1f;
                    else if (e.Actor == v.Id && e.Data != null && (e.Data.EndsWith("/resist") || e.Data.EndsWith("/defense"))) score += 1f;
                }
                else if (e.Type == "Resist" && e.Actor == v.Id) { score += 1.5f; resists++; }
            }
            foreach (var w in v.Body.Wounds) if (!w.Postmortem && ArmOrHand(w.Region) && w.Clock >= start && w.Clock <= now + 1e-9) score += 1f;
            int deathRoom = v.Body.DeathRoom >= 0 ? v.Body.DeathRoom : v.Room;
            if (ab != null && ab.Room == deathRoom && ab.Pos.f == v.Pos.f) centre = ab.Pos;
            return score;
        }

        // ================================================================== per tick
        /// <summary>Stateless per-tick upkeep (Combat.Bodies): bloody handprints on doors the bleeding pass, and toppled furniture
        /// set upright once the chapter has moved on.</summary>
        public static void Tick(Simulation sim)
        {
            var S = sim?.S; if (S?.Layout == null) return;
            if (S.Tick % 5 == 0) DoorPrints(S);
            if (S.Tick % 50 == 0) Righting(S);
        }

        static void DoorPrints(GameState S)
        {
            List<Actor> bleeders = null;
            foreach (var a in S.Actors.Values)
                if (a.Alive && a.Status == ActorStatus.Active && a.Body.Bleed >= 0.05f && !a.Body.Stabilized && a.CarriedBy == null && a.StairId < 0) (bleeders ?? (bleeders = new List<Actor>())).Add(a);
            if (bleeders == null) return;
            if (bleeders.Count > 1) bleeders.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));
            foreach (var a in bleeders)
            {
                var marks = a.Body.GoreMarks; int prints = 0;
                if (marks != null) foreach (var m in marks) if (m.Kind == GoreKind.Handprint && m.Door >= 0) prints++;
                if (prints >= 2) continue;
                var d = NearestDoor(S, a.Pos, 0.8f); if (d == null) continue;
                if (marks != null && marks.Any(m => m.Kind == GoreKind.Handprint && m.Door == d.Id)) continue;
                var rng = LocalRng(S, a, "door" + d.Id);
                var src = a.Body.Wounds.LastOrDefault(x => !x.Postmortem && !Bloodless(x.Type));
                Add(S, a, new GoreMark { Kind = GoreKind.Handprint, Pos = a.Pos, Room = a.Room, H = rng.Range(1.0f, 1.2f), Dir = Wrap(Bearing(a.Pos, d.Pos)), Power = MathX.Clamp01(0.35f + a.Body.Bleed * 2f), Region = src?.Region ?? BodyRegion.HandR, Dmg = src?.Type ?? DamageType.Cut, Sev = src?.Sev ?? 0, By = a.Id, Door = d.Id });
            }
        }

        static void Righting(GameState S)
        {
            foreach (var a in S.Actors.Values)
            {
                var marks = a.Body?.GoreMarks; if (marks == null) continue;
                bool changed = false;
                foreach (var m in marks)
                {
                    if (m.Kind != GoreKind.Topple || m.Righted || m.Chapter == S.Chapter) continue;
                    var f = m.Furniture >= 0 && m.Furniture < S.Layout.Furniture.Count ? S.Layout.Furniture[m.Furniture] : null;
                    if (f != null)
                    {
                        f.Marks.Remove(ToppledLine);
                        var r = S.Layout.Room(f.Room);
                        if (r != null) foreach (var si in r.Spots) { var sp = S.Layout.Spots[si]; if (sp.Furniture == f.Id && sp.Occupant == ToppledSpot) sp.Occupant = null; }
                    }
                    m.Righted = true; changed = true;
                }
                if (changed) a.Body.GoreRev++;
            }
        }

        // ================================================================== dismemberment
        /// <summary>
        /// Cut a dead body into pieces (the adapter target of MethodsDismember.CutUp). Pieces are Items exactly like CutUp's
        /// (Type "SeveredPart", Name "천에 싼 꾸러미", Note = part code, Owner = victim, Surface blood+sawn), placed next to the joint
        /// (AtBody) or strewn over the floor of the room (Scatter). Each cut adds a postmortem Sev-5 Cut wound (no Strike, no
        /// event) plus a saw-kerf and an ooze mark — little blood, as a dead body gives. The caller owns flags, notes, the ledger,
        /// washing the tool and any DrainBlood. Returns the new item ids in part order (empty, never null, when it cannot cut).
        /// </summary>
        public static List<string> Dismember(Simulation sim, Actor victim, Actor by, Item tool, SeverPlacement placement, SeverPart[] parts = null)
        {
            var ids = new List<string>();
            if (sim == null || victim == null || victim.Alive || victim.CarriedBy != null) return ids;
            if (tool != null && !(Methods.IsSaw(tool) || tool.Def?.Dmg == DamageType.Cut)) return ids;
            var S = sim.S; var rng = LocalRng(S, victim, "dismember");
            var have = new HashSet<SeverPart>();
            foreach (var p in PiecesOf(S, victim.Id)) if (TryPart(p, out var sp0)) have.Add(sp0);
            var list = new List<SeverPart>();
            if (parts == null)
            {
                int n = 2 + rng.R(3);   // two to four pieces, as CutUp
                foreach (var p in DefaultParts) { if (list.Count >= n) break; if (!have.Contains(p)) list.Add(p); }
            }
            else foreach (var p in parts)
                {
                    if (have.Contains(p) || list.Contains(p)) continue;
                    if ((p == SeverPart.HandL && (have.Contains(SeverPart.ArmL))) || (p == SeverPart.HandR && have.Contains(SeverPart.ArmR))) continue;   // the hand went with the arm
                    list.Add(p);
                }
            if (list.Count == 0) return ids;
            var g = S.Layout.Nav(victim.Pos.f); int room = victim.Room;
            var placed = new List<P3>();
            foreach (var part in list)
            {
                Joint(victim, part, out P3 joint, out float limbDir);
                P3 p;
                if (placement == SeverPlacement.AtBody)
                {
                    p = Along(joint, limbDir, rng.Range(0.25f, 0.6f));
                    int k = g.CellOf(p.x, p.z);
                    if (k < 0 || g.Room[k] != room) { p = Along(joint, limbDir, 0.2f); k = g.CellOf(p.x, p.z); if (k < 0 || g.Room[k] != room) p = sim.SnapPublic(p); }
                }
                else p = ScatterPoint(S, sim, g, victim, room, placed, rng);
                placed.Add(p);
                var it = new Item { Id = S.NewId("it"), Type = "SeveredPart", Name = "천에 싼 꾸러미", Note = part.ToString(), Owner = victim.Id, Pos = p, Room = room, HomeRoom = room, HomePos = p, Bloody = true, LastUser = by?.Id,
                    Yaw = placement == SeverPlacement.AtBody ? limbDir : Wrap(rng.Range(-180f, 180f)) };
                it.Surface.Add("blood"); it.Surface.Add("sawn");
                S.Items[it.Id] = it; S.Emit(GameEventType.ItemMoved, by?.Id, data: it.Id, text: "spawn", pos: it.Pos);
                ids.Add(it.Id);
                victim.Body.Wounds.Add(new Wound { Region = RegionOf(part), Type = DamageType.Cut, Sev = 5, Postmortem = true, Tick = S.Tick, Clock = S.Clock, By = by?.Id, Weapon = tool?.Id, CauseEvent = "dismember" });
                Add(S, victim, new GoreMark { Kind = GoreKind.SawMark, Pos = joint, Room = room, H = 0f, Dir = limbDir, Power = 0.5f, Region = RegionOf(part), Dmg = DamageType.Cut, Sev = 5, Postmortem = true, By = by?.Id, Item = it.Id });
                Add(S, victim, new GoreMark { Kind = GoreKind.Ooze, Pos = joint, Room = room, H = 0.05f, Dir = limbDir, Power = 0.25f, Region = RegionOf(part), Dmg = DamageType.Cut, Sev = 5, Postmortem = true, By = by?.Id, Item = it.Id });
            }
            victim.Body.GoreRev++;
            return ids;
        }

        static P3 ScatterPoint(GameState S, Simulation sim, NavGrid g, Actor victim, int room, List<P3> placed, Rng rng)
        {
            for (int pass = 0; pass < 2; pass++)
                for (int tries = 0; tries < 40; tries++)
                {
                    var p = Along(victim.Pos, rng.Range(-180f, 180f), pass == 0 ? rng.Range(1f, 3f) : rng.Range(0.6f, 4f));
                    int k = g.CellOf(p.x, p.z);
                    if (k < 0 || !g.Walkable(k) || g.Room[k] != room) continue;
                    if (placed.Any(q => q.DistXZ(p) < 0.5f)) continue;
                    return p;
                }
            return sim.SnapPublic(Along(victim.Pos, rng.Range(-180f, 180f), 1f));
        }

        public static BodyRegion RegionOf(SeverPart p)
        {
            switch (p)
            {
                case SeverPart.Head: return BodyRegion.Neck;
                case SeverPart.ArmL: return BodyRegion.ShoulderL; case SeverPart.ArmR: return BodyRegion.ShoulderR;
                case SeverPart.HandL: return BodyRegion.HandL; case SeverPart.HandR: return BodyRegion.HandR;
                case SeverPart.LegL: return BodyRegion.LegL; default: return BodyRegion.LegR;
            }
        }

        /// <summary>
        /// Where a part's cut joint lies on the floor plane and which way the limb runs from it, for the body as it lies. Local
        /// frame: +f = the actor's yaw, +r = its right. On the back / side the head lies behind (−f) and the legs in front; face
        /// down the other way round; slumped = seated against a wall behind, legs out in front. Proportions of a 1.75 m person.
        /// </summary>
        static void Joint(Actor v, SeverPart part, out P3 joint, out float limbDir)
        {
            float fr, ff, dr, df;   // joint (right, fwd) and limb direction (right, fwd)
            bool left = part == SeverPart.ArmL || part == SeverPart.HandL || part == SeverPart.LegL;
            float sx = left ? -1f : 1f;
            bool arm = part == SeverPart.ArmL || part == SeverPart.ArmR, hand = part == SeverPart.HandL || part == SeverPart.HandR, head = part == SeverPart.Head;
            switch (v.Pose)
            {
                case Pose.LieFront:
                    if (head) { fr = 0; ff = 0.55f; dr = 0; df = 1; }
                    else if (arm || hand) { fr = sx * 0.19f; ff = 0.45f; dr = sx * 0.35f; df = -0.94f; }
                    else { fr = sx * 0.10f; ff = 0f; dr = sx * 0.1f; df = -1f; }
                    break;
                case Pose.LieSide:
                    if (head) { fr = 0; ff = -0.55f; dr = 0.15f; df = -1; }
                    else if (arm || hand) { fr = sx * 0.06f; ff = -0.42f; dr = 0.5f; df = 0.85f; }
                    else { fr = sx * 0.05f; ff = 0f; dr = 0.45f; df = 0.9f; }
                    break;
                case Pose.Slumped:
                    if (head) { fr = 0; ff = -0.28f; dr = 0.15f; df = -0.4f; }
                    else if (arm || hand) { fr = sx * 0.19f; ff = -0.22f; dr = sx * 0.6f; df = 0.8f; }
                    else { fr = sx * 0.10f; ff = 0.05f; dr = sx * 0.12f; df = 1f; }
                    break;
                default:   // LieBack (and anything else)
                    if (head) { fr = 0; ff = -0.55f; dr = 0; df = -1; }
                    else if (arm || hand) { fr = sx * 0.19f; ff = -0.45f; dr = sx * 0.45f; df = 0.89f; }
                    else { fr = sx * 0.10f; ff = 0f; dr = sx * 0.1f; df = 1f; }
                    break;
            }
            float len = (float)Math.Sqrt(dr * dr + df * df); if (len > 1e-4f) { dr /= len; df /= len; }
            if (hand) { fr += dr * 0.55f; ff += df * 0.55f; }   // the wrist, one forearm-and-upper-arm down from the shoulder
            double y = v.Yaw * Math.PI / 180.0; float sin = (float)Math.Sin(y), cos = (float)Math.Cos(y);
            // world: forward = (sin, cos), right = (cos, -sin)
            joint = new P3(v.Pos.f, v.Pos.x + fr * cos + ff * sin, v.Pos.z - fr * sin + ff * cos);
            float wx = dr * cos + df * sin, wz = -dr * sin + df * cos;
            limbDir = Wrap(MathX.AngleDeg(wx, wz));
        }

        // ================================================================== queries
        /// <summary>The pieces of a victim (items of Type "SeveredPart" owned by them), ordered by id (ordinal).</summary>
        public static IEnumerable<Item> PiecesOf(GameState S, string victimId)
        {
            if (S == null || victimId == null) return Enumerable.Empty<Item>();
            return S.Items.Values.Where(i => i.Type == "SeveredPart" && i.Owner == victimId).OrderBy(i => i.Id, StringComparer.Ordinal);
        }

        /// <summary>The part a piece is (from its Note code: Head, ArmL, ArmR, HandL, HandR, LegL, LegR). False for anything else (e.g. "Torso").</summary>
        public static bool TryPart(Item piece, out SeverPart part)
        {
            switch (piece?.Note)
            {
                case "Head": part = SeverPart.Head; return true;
                case "ArmL": part = SeverPart.ArmL; return true;
                case "ArmR": part = SeverPart.ArmR; return true;
                case "HandL": part = SeverPart.HandL; return true;
                case "HandR": part = SeverPart.HandR; return true;
                case "LegL": part = SeverPart.LegL; return true;
                case "LegR": part = SeverPart.LegR; return true;
            }
            part = SeverPart.ArmL; return false;
        }

        public static bool IsDismembered(GameState S, string victimId) => S != null && victimId != null && (S.Flags.ContainsKey("dismembered:" + victimId) || PiecesOf(S, victimId).Any());

        /// <summary>The marks of a body (empty, never null).</summary>
        public static IReadOnlyList<GoreMark> MarksOf(Actor v) => (IReadOnlyList<GoreMark>)v?.Body?.GoreMarks ?? NoMarks;

        public static string Kor(SeverPart p)
        {
            switch (p)
            {
                case SeverPart.Head: return "머리"; case SeverPart.ArmL: return "왼팔"; case SeverPart.ArmR: return "오른팔";
                case SeverPart.HandL: return "왼손"; case SeverPart.HandR: return "오른손"; case SeverPart.LegL: return "왼다리"; case SeverPart.LegR: return "오른다리";
            }
            return "신체 일부";
        }

        /// <summary>Probe / tests only: stage a killing of one kind (stab | blunt | strangle | dismember) in a quiet ground-floor room
        /// right now and return the victim's id (null when it cannot). Uses the shared streams like any real attack would.</summary>
        public static string DebugStage(Simulation sim, string kind) => sim?.DebugGoreStage(kind);
    }

    public sealed partial class Simulation
    {
        /// <summary>See <see cref="Gore.DebugStage"/>.</summary>
        internal string DebugGoreStage(string kind)
        {
            var S = this.S; if (S?.Layout == null) return null;
            kind = (kind ?? "stab").Trim().ToLowerInvariant();
            if (kind != "stab" && kind != "blunt" && kind != "strangle" && kind != "dismember") kind = "stab";
            // ---- 1. the room: quiet, big enough, with light furniture a struggle can knock over
            var types = new[] { RoomType.Study, RoomType.Library, RoomType.Lounge, RoomType.MusicRoom, RoomType.Gallery, RoomType.GuestRoom, RoomType.Bedroom };
            bool Empty(Room r) => !S.Actors.Values.Any(a => a.Alive && a.Room == r.Id);
            int Tippable(Room r) => r.Furniture.Count(i => Gore.ToppleKind(S.Layout.Furniture[i]));
            var room = S.Layout.Rooms.OrderBy(r => r.Id).FirstOrDefault(r => r.Floor == 0 && !r.Void && types.Contains(r.Type) && r.Rect.Area >= 16 && Tippable(r) >= 2 && r.Doors.Count >= 1 && Empty(r))
                ?? S.Layout.Rooms.OrderBy(r => r.Id).FirstOrDefault(r => r.Floor == 0 && !r.Void && !RoomInfo.IsPassage(r.Type) && r.Doors.Count >= 1 && r.Rect.Area >= 12 && Empty(r))
                ?? S.Layout.Rooms.OrderBy(r => r.Id).FirstOrDefault(r => r.Floor == 0 && !r.Void && !RoomInfo.IsPassage(r.Type) && r.Doors.Count >= 1);
            if (room == null) return null;
            // ---- 2. the two people
            var npcs = S.LivingNpcs.Where(a => a.Status == ActorStatus.Active && a.Carrying == null && a.CarriedBy == null && a.StairId < 0).OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
            if (npcs.Count < 2) return null;
            var v = npcs[0]; var c = npcs[1];
            foreach (var a in new[] { v, c })
            {
                if (a.Act != null) EndActivity(a, false);
                a.Act = null; if (a.Spot >= 0) ReleaseSpot(a);
                a.TalkingTo = null; a.Following = null; a.Speed = 0; a.Running = false; a.Pose = Pose.Stand; a.Anim = Anim.Idle; a.NextThink = S.Clock + 5;
            }
            var g = S.Layout.Nav(room.Floor);
            var centreR = new P3(room.Floor, room.Rect.CX, room.Rect.CZ);
            var light = room.Furniture.Select(i => S.Layout.Furniture[i]).Where(Gore.ToppleKind).ToList();
            P3 vp = default, cp = default; double best = double.MinValue;
            for (int k = 0; k < g.Room.Length; k++)
            {
                if (g.Room[k] != room.Id || !g.Walkable(k) || !g.InMain(k)) continue;
                var p = g.Center(k);
                float wall = Math.Min(Math.Min(p.x - room.Rect.x0, room.Rect.x1 - p.x), Math.Min(p.z - room.Rect.z0, room.Rect.z1 - p.z));
                float yaw = MathX.AngleDeg(centreR.x - p.x, centreR.z - p.z);
                double y = yaw * Math.PI / 180.0; var q = new P3(p.f, p.x + (float)Math.Sin(y) * 0.8f, p.z + (float)Math.Cos(y) * 0.8f);
                int kq = g.CellOf(q.x, q.z); if (kq < 0 || !g.Walkable(kq) || g.Room[kq] != room.Id) continue;
                int near = light.Count(f => f.Pos.DistXZ(p) <= 2.3f);
                double s = Math.Min(near, 3) * 10 - Math.Abs(wall - 1.2f) * 6 - (light.Count > 0 ? light.Min(f => f.Pos.DistXZ(p)) * 0.5 : 0);
                if (s > best) { best = s; vp = p; cp = q; }
            }
            if (best == double.MinValue) { vp = SnapPublic(centreR); cp = SnapPublic(new P3(vp.f, vp.x, vp.z + 0.8f)); }
            v.Pos = vp; v.Room = room.Id; v.Yaw = MathX.AngleDeg(cp.x - vp.x, cp.z - vp.z);
            c.Pos = cp; c.Room = S.Layout.RoomAt(cp) >= 0 ? S.Layout.RoomAt(cp) : room.Id; c.Yaw = MathX.AngleDeg(vp.x - cp.x, vp.z - cp.z);
            // ---- 3. the weapon
            Item Find(string type, P3 near)
            {
                var it = S.Items.Values.Where(i => i.Type == type && i.Holder == null && !i.Hidden && i.Room == room.Id).OrderBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
                if (it != null) return it;
                it = new Item { Id = S.NewId("it"), Type = type, Pos = near, Room = room.Id, HomeRoom = room.Id, HomePos = near };
                S.Items[it.Id] = it; S.Emit(GameEventType.ItemMoved, null, data: it.Id, text: "spawn", pos: near);
                return it;
            }
            var weapon = Find(kind == "blunt" ? "Candlestick" : kind == "strangle" ? "Rope" : "KitchenKnife", cp);
            PickUp(c, weapon);
            S.Flags["attackstart:" + c.Id + ":" + v.Id] = S.Clock;
            Incidents.OnAttackBegin(this, c, v, null);
            // ---- 4. the blows
            Wound last = null;
            void Hit(BodyRegion r, DamageType t, int sev, string cause = "attack") { if (v.Alive) last = Strike(c.Id, v, r, t, sev, weapon.Id, cause) ?? last; }
            void Resist() { if (!v.Alive || !c.Alive) return; Strike(v.Id, c, BodyRegion.ArmR, DamageType.Blunt, 1, null, "resist"); S.Log("Resist", v.Id, c.Id, room: v.Room, secret: true); }
            switch (kind)
            {
                case "blunt":
                    Hit(BodyRegion.Head, DamageType.Blunt, 3); Resist(); Hit(BodyRegion.Head, DamageType.Blunt, 3); Hit(BodyRegion.ShoulderL, DamageType.Blunt, 2); Hit(BodyRegion.Head, DamageType.Blunt, 3);
                    break;
                case "strangle":
                    for (int i = 0; i < 3; i++) Hit(BodyRegion.Neck, DamageType.Choke, 2, "strangle");
                    S.Flags["nails:" + v.Id] = S.Clock;
                    Resist();
                    break;
                default:   // stab, and the killing before a dismemberment
                    Hit(BodyRegion.Abdomen, DamageType.Stab, 3); Resist(); Hit(BodyRegion.Chest, DamageType.Stab, 3); Hit(BodyRegion.Neck, DamageType.Cut, 3);
                    break;
            }
            if (v.Alive) Die(v, c.Id, last != null ? WoundProfiles.Get(last.Region, last.Type, last.Sev).Kor : "외상", last);
            S.Flags.Remove("attackstart:" + c.Id + ":" + v.Id);
            // ---- dismemberment: a cleaver, the pieces strewn over the floor, the bookkeeping X_MDismember does
            var pieces = new List<string>(); Item cleaver = null;
            if (kind == "dismember")
            {
                cleaver = Find("Cleaver", Gore_Along(vp, v.Yaw + 90f, 0.9f, room.Id, vp));
                if (cleaver.Holder == null) PickUp(c, cleaver);
                pieces = Gore.Dismember(this, v, c, cleaver, SeverPlacement.Scatter, new[] { SeverPart.HandR, SeverPart.ArmL, SeverPart.LegR, SeverPart.Head });
                S.Flags["dismembered:" + v.Id] = S.Clock; S.Flags["sawroom:" + v.Id] = room.Id;
                var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == v.Id && i.Loop == S.Loop);
                if (inc != null) { inc.Mutilated = true; inc.Notes.Add("사후 해체: " + S.RoomName(room.Id)); }
                S.Log("Dismember", c.Id, v.Id, cleaver.Id, room.Id, v.Pos, pieces.Count.ToString(), null, true);
            }
            // ---- 5. afterwards: the weapon on the floor, the culprit next door, the doors open
            if (weapon.Holder == c.Id) DropItem(c, weapon, Gore_Along(v.Pos, v.Yaw + 180f, 0.6f, room.Id, v.Pos));
            if (cleaver != null && cleaver.Holder == c.Id) DropItem(c, cleaver, Gore_Along(v.Pos, v.Yaw + 90f, 0.9f, room.Id, v.Pos));
            var next = S.Layout.Neighbors(room.Id).Select(S.Layout.Room).FirstOrDefault(r => r != null && !r.Void && r.Id != room.Id && r.Floor == room.Floor);
            if (next != null) { c.Pos = SnapPublic(new P3(next.Floor, next.Rect.CX, next.Rect.CZ)); c.Room = S.Layout.RoomAt(c.Pos) >= 0 ? S.Layout.RoomAt(c.Pos) : next.Id; }
            c.Act = null; c.NextThink = S.Clock + 1;
            foreach (var di in room.Doors) { var d = S.Layout.Doors[di]; if (d.Locked || !d.Open) SetDoor(null, d, true, d.Locked ? false : (bool?)null, "gore-stage"); }
            S.Dev($"GORE STAGE {kind}: victim {v.Id} by {c.Id} in {room.Name} weapon {weapon.Type}/{weapon.Id} marks {Gore.MarksOf(v).Count} pieces [{string.Join(",", pieces)}]");
            return v.Id;
        }

        /// <summary>A point d metres along a bearing, if it is still inside the room (else the fallback).</summary>
        P3 Gore_Along(P3 p, float bearing, float d, int room, P3 fallback)
        {
            double r = bearing * Math.PI / 180.0; var q = new P3(p.f, p.x + (float)Math.Sin(r) * d, p.z + (float)Math.Cos(r) * d);
            return S.Layout.RoomAt(q) == room ? q : fallback;
        }
    }
}
