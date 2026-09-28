using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>Game damage profiles (not medical models). Channels are combined multiplicatively; bleeding accumulates to death.</summary>
    public static class WoundProfiles
    {
        public sealed class Effect { public float Mob, Res, HandL, HandR, Con, Speech, Bleed; public double DeathDelay = -1; public double KO = 0; public bool Instant; public string Kor; }

        public static Effect Get(BodyRegion r, DamageType t, int sev)
        {
            var e = new Effect(); sev = Math.Max(1, Math.Min(4, sev));
            bool sharp = t == DamageType.Cut || t == DamageType.Stab;
            switch (r)
            {
                case BodyRegion.Neck:
                    if (sharp && sev >= 3) { e.Instant = t == DamageType.Cut; e.DeathDelay = t == DamageType.Cut ? 0 : 3; e.Bleed = 0.8f; e.Con = 0.9f; e.Speech = 1; e.Kor = "목에 입은 깊은 상처"; }
                    else if (t == DamageType.Choke) { e.Con = sev >= 3 ? 1 : 0.5f; e.Speech = 1; e.DeathDelay = sev >= 3 ? 3 : -1; e.KO = sev >= 2 ? 6 : 0; e.Kor = "목이 졸려 질식"; }
                    else { e.Bleed = 0.08f * sev; e.Speech = 0.4f; e.Mob = 0.1f; e.Kor = "목의 상처"; }
                    break;
                case BodyRegion.Head:
                    if (t == DamageType.Blunt || t == DamageType.Crush || t == DamageType.Fall)
                    {
                        if (sev >= 4) { e.Instant = true; e.DeathDelay = 0; e.Kor = "두개골 함몰"; }
                        else if (sev == 3) { e.KO = 25; e.Con = 1; e.Bleed = 0.05f; e.DeathDelay = 30; e.Kor = "머리를 세게 맞은 충격"; }
                        else { e.KO = sev == 2 ? 3 : 0; e.Con = 0.3f; e.Mob = 0.15f; e.Kor = "머리 타박상"; }
                    }
                    else if (sharp) { e.Bleed = 0.06f * sev; e.Con = 0.2f * sev; e.Kor = "머리가 찢어진 상처"; if (sev >= 4) { e.DeathDelay = 2; } }
                    break;
                case BodyRegion.Chest:
                    if (sharp) { e.Bleed = 0.07f * sev; e.Res = 0.25f * sev; e.Mob = 0.15f * sev; e.Speech = 0.15f * sev; e.Kor = "가슴을 찔린 상처"; if (sev >= 4) e.DeathDelay = 6; else if (sev == 3) e.DeathDelay = 16; }
                    else { e.Res = 0.2f; e.Mob = 0.1f; e.Kor = "가슴 타박상"; if (t == DamageType.Crush && sev >= 3) { e.Instant = true; e.DeathDelay = 0; e.Kor = "가슴이 짓눌림"; } }
                    break;
                case BodyRegion.Abdomen:
                case BodyRegion.Back:
                    if (sharp) { e.Bleed = 0.045f * sev; e.Res = 0.2f * sev; e.Mob = 0.18f * sev; e.Kor = r == BodyRegion.Back ? "등을 찔린 상처" : "배를 찔린 상처"; if (sev >= 4) e.DeathDelay = 12; else if (sev == 3) e.DeathDelay = 25; }
                    else { e.Res = 0.25f; e.Mob = 0.15f; e.Kor = r == BodyRegion.Back ? "등 타박상" : "배 타박상"; if (t == DamageType.Crush && sev >= 3) { e.Instant = true; e.DeathDelay = 0; e.Kor = "몸통이 짓눌림"; } }
                    break;
                case BodyRegion.ShoulderL: case BodyRegion.ArmL: case BodyRegion.HandL:
                    e.HandL = r == BodyRegion.HandL ? 0.35f * sev : 0.25f * sev; e.Res = 0.08f * sev; e.Bleed = sharp ? 0.012f * sev : 0; e.Mob = 0.02f; e.Kor = r == BodyRegion.HandL ? "왼손 상처" : r == BodyRegion.ArmL ? "왼팔 상처" : "왼어깨 상처";
                    break;
                case BodyRegion.ShoulderR: case BodyRegion.ArmR: case BodyRegion.HandR:
                    e.HandR = r == BodyRegion.HandR ? 0.35f * sev : 0.25f * sev; e.Res = 0.08f * sev; e.Bleed = sharp ? 0.012f * sev : 0; e.Mob = 0.02f; e.Kor = r == BodyRegion.HandR ? "오른손 상처" : r == BodyRegion.ArmR ? "오른팔 상처" : "오른어깨 상처";
                    break;
                case BodyRegion.LegL: case BodyRegion.LegR: case BodyRegion.FootL: case BodyRegion.FootR:
                    e.Mob = 0.22f * sev; e.Bleed = sharp ? 0.015f * sev : 0; e.Kor = (r == BodyRegion.LegL || r == BodyRegion.FootL ? "왼" : "오른") + (r == BodyRegion.FootL || r == BodyRegion.FootR ? "발 상처" : "다리 상처");
                    break;
            }
            if (t == DamageType.Choke && r == BodyRegion.Head) { e.Con = 1; e.Speech = 1; e.KO = 6; e.DeathDelay = sev >= 3 ? 3 : -1; e.Kor = "얼굴이 짓눌려 질식"; }   // a pillow held over the face (Methods)
            if (t == DamageType.Drown) { e.Con = 1; e.KO = 4; e.DeathDelay = 3; e.Kor = "익사"; }
            if (t == DamageType.Shock) { e.KO = 8; e.Con = 1; e.DeathDelay = sev >= 3 ? 1 : -1; e.Kor = "감전"; }
            if (e.Kor == null) e.Kor = "상처";
            return e;
        }
    }

    public static class WoundText
    {
        public static string Region(BodyRegion r)
        {
            switch (r)
            {
                case BodyRegion.Head: return "머리"; case BodyRegion.Neck: return "목"; case BodyRegion.Chest: return "가슴"; case BodyRegion.Abdomen: return "복부";
                case BodyRegion.ShoulderL: return "왼어깨"; case BodyRegion.ShoulderR: return "오른어깨"; case BodyRegion.ArmL: return "왼팔"; case BodyRegion.ArmR: return "오른팔";
                case BodyRegion.HandL: return "왼손"; case BodyRegion.HandR: return "오른손"; case BodyRegion.LegL: return "왼다리"; case BodyRegion.LegR: return "오른다리";
                case BodyRegion.FootL: return "왼발"; case BodyRegion.FootR: return "오른발"; case BodyRegion.Back: return "등";
            }
            return r.ToString();
        }
        public static string Type(DamageType t)
        {
            switch (t)
            {
                case DamageType.Cut: return "베인 상처"; case DamageType.Stab: return "찔린 상처"; case DamageType.Blunt: return "둔기로 맞은 자국"; case DamageType.Crush: return "짓눌린 자국";
                case DamageType.Drown: return "물에 빠진 흔적"; case DamageType.Choke: return "목 졸린 자국"; case DamageType.Shock: return "전기에 덴 자국"; case DamageType.Fall: return "떨어지며 부딪힌 자국"; case DamageType.Burn: return "화상"; case DamageType.None: return "독이나 약의 흔적";
            }
            return "상처";
        }
        public static string Sev(int s) => s >= 5 ? "잘려 나감" : s >= 4 ? "치명상" : s == 3 ? "심함" : s == 2 ? "조금 심함" : "가벼움";   // 5 = severed (Gore.Dismember)
        public static string Describe(Wound w)
        {
            if (w.Sev >= 5) return $"{Region(w.Region)} 절단" + (w.Postmortem ? " — 숨진 뒤에 잘렸다" : "");
            { var vd = Violence.Describe(w); if (vd != null) return vd; }   // --- violence track: gunshot / bolt / grip / bite / binding marks
            return $"{Region(w.Region)}에 {Type(w.Type)} ({Sev(w.Sev)})" + (w.Postmortem ? " — 숨진 뒤에 생긴 듯하다" : "") + (w.Treated ? " — 응급처치를 받았다" : "");
        }
    }

    public sealed partial class Simulation
    {
        /// <summary>Resolve one strike contact. Used for NPC attacks, the player's real hits (region from the Unity hitbox), traps and machines.</summary>
        public Wound Strike(string attacker, Actor victim, BodyRegion region, DamageType type, int sev, string weaponItem, string cause, float lx = 0, float ly = 0, float lz = 0,
            float dx = 0, float dy = 0, float dz = 0, float contactImpulse = 0, float contactEnergy = 0, float nx = 0, float ny = 0, float nz = 0, bool resolvedByPhysics = false)
        {
            var v = victim; if (v == null) return null;
            bool postmortem = !v.Alive;
            // fatality budget: an attack that could kill needs a reservation made before the cause action
            var w = new Wound { Region = region, Type = type, Sev = sev, lx = lx, ly = ly, lz = lz, Postmortem = postmortem, Tick = S.Tick, Clock = S.Clock, By = attacker, Weapon = weaponItem };
            w.HasContact = contactImpulse > 0 && !float.IsInfinity(contactImpulse) && !float.IsNaN(contactImpulse);
            w.ResolvedByPhysics = resolvedByPhysics;
            if (w.HasContact) { w.dx = dx; w.dy = dy; w.dz = dz; w.nx = nx; w.ny = ny; w.nz = nz; w.ContactImpulse = Math.Min(200, contactImpulse); w.ContactEnergy = Math.Max(0, Math.Min(500, contactEnergy)); }
            var ev = S.Log(postmortem ? "PostmortemDamage" : "Strike", attacker, v.Id, weaponItem, v.Room, v.Pos, $"{region}/{type}/{sev}/{cause}", secret: true);
            w.CauseEvent = ev.Seq.ToString();
            v.Body.Wounds.Add(w);
            var it = S.I(weaponItem);
            bool bleeds = type != DamageType.Choke && type != DamageType.Drown && type != DamageType.Shock && type != DamageType.Burn;   // a cord, water or current draws no blood
            if (it != null && bleeds && (type == DamageType.Cut || type == DamageType.Stab || sev >= 3)) { it.Bloody = true; if (!it.Surface.Contains("blood")) it.Surface.Add("blood"); S.Emit(GameEventType.ItemState, attacker, data: it.Id, text: "blood"); }
            var att = S.A(attacker);
            if (!postmortem) Gore.OnStrike(this, att, v, w, cause, it);   // blood marks only: no ids, no ledger, no shared rng
            if (att != null && !postmortem && bleeds && att.Pos.f == v.Pos.f && att.Pos.DistXZ(v.Pos) < 2.5f && (type == DamageType.Cut || type == DamageType.Stab || sev >= 3))
            {
                att.BloodOnClothes = MathX.Clamp01(att.BloodOnClothes + (region == BodyRegion.Neck ? 0.5f : 0.22f));
                // a mask of the house's evening hides a face, not what lands on it (HouseEvents: the house knows whose mask it was)
                var mask = att.Disguise != null ? S.I(att.Disguise) : null;
                if (mask != null && mask.Type == "TheaterMask" && !mask.Surface.Contains("blood-speck")) mask.Surface.Add("blood-speck");
            }
            S.Emit(GameEventType.Wound, attacker, v.Id, text: WoundText.Describe(w), pos: v.Pos, value: sev,
                data: string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}|{4:R},{5:R},{6:R}|{7:R},{8:R},{9:R}|{10:R}|{11:R}|{12:R},{13:R},{14:R}|{15}",
                    region, type, sev, postmortem ? 1 : 0, lx, ly, lz, w.dx, w.dy, w.dz, w.ContactImpulse, w.ContactEnergy, w.nx, w.ny, w.nz, resolvedByPhysics ? 1 : 0));
            Sound(sev >= 3 ? SoundKind.Strike : SoundKind.Struggle, v.Pos, 0.35f + sev * 0.08f, attacker);
            if (!postmortem)
            {
                ApplyWound(v, w);
                if (type != DamageType.Choke && type != DamageType.Drown && type != DamageType.Shock && type != DamageType.Burn) AddTrace(type == DamageType.Stab || type == DamageType.Cut ? "BloodDrip" : "Scuff", v.Pos, v.Room, attacker, v.Id, 0.25f + sev * 0.08f, 0,
                    type == DamageType.Stab || type == DamageType.Cut ? "혈흔" : "몸싸움 흔적", "이 자리에서 누군가 다쳤다", "누가 다쳤는지, 누가 그랬는지");
            }
            else { Crime.OnPostmortem(this, v, w); Gore.OnPostmortem(this, att, v, w, cause, it); }
            return w;
        }

        void ApplyWound(Actor v, Wound w)
        {
            var e = WoundProfiles.Get(w.Region, w.Type, w.Sev); var b = v.Body;
            b.Mobility = MathX.Clamp01(b.Mobility * (1 - e.Mob)); b.Resistance = MathX.Clamp01(b.Resistance * (1 - e.Res));
            b.HandL = MathX.Clamp01(b.HandL - e.HandL); b.HandR = MathX.Clamp01(b.HandR - e.HandR);
            b.Conscious = MathX.Clamp01(b.Conscious - e.Con); b.Speech = MathX.Clamp01(b.Speech - e.Speech);
            b.Bleed += e.Bleed;
            // combination rule: trauma to vital regions (head/neck/torso) accumulates across blows —
            // many moderate blows can do what one severe blow does (and a body that is already down cannot protect itself)
            int vital = b.Wounds.Where(x => !x.Postmortem && (x.Region == BodyRegion.Head || x.Region == BodyRegion.Neck || x.Region == BodyRegion.Chest || x.Region == BodyRegion.Abdomen || x.Region == BodyRegion.Back)).Sum(x => x.Sev);
            if (!b.Wounds.Contains(w) && !w.Postmortem && (w.Region == BodyRegion.Head || w.Region == BodyRegion.Neck || w.Region == BodyRegion.Chest || w.Region == BodyRegion.Abdomen || w.Region == BodyRegion.Back)) vital += w.Sev;
            if (vital >= 16) { e.Instant = true; e.Kor = "거듭된 공격으로 입은 치명상"; }
            else if (vital >= 9) { double at0 = S.Clock + Math.Max(1.5, 16 - vital); b.DeathAt = b.DeathAt < 0 ? at0 : Math.Min(b.DeathAt, at0); b.Conscious = Math.Min(b.Conscious, 0.1f); }
            if (e.KO > 0) { b.UnconsciousUntil = Math.Max(b.UnconsciousUntil, S.Clock + e.KO); }
            if (e.DeathDelay >= 0)
            {
                double at = S.Clock + e.DeathDelay;
                b.DeathAt = b.DeathAt < 0 ? at : Math.Min(b.DeathAt, at);
            }
            // drop things the hand can't hold
            if (b.HandR < 0.3f && v.HandR != null) DropItem(v, S.I(v.HandR), v.Pos);
            if (b.HandL < 0.3f && v.HandL != null) DropItem(v, S.I(v.HandL), v.Pos);
            if (e.Instant) { Die(v, w.By, e.Kor, w); return; }
            if (b.UnconsciousUntil > S.Clock || b.Conscious <= 0.05f) Collapse(v, "정신을 잃음");
            else if (b.Mobility < 0.12f) Collapse(v, "움직일 수 없음");
            v.Needs.Fear = 1; v.Needs.Stress = 1;
            if (!v.IsPlayer && v.Alive && v.Status == ActorStatus.Active) Crime.VictimReact(this, v, S.A(w.By));
            if (v.IsPlayer) { RaiseStop(StopKind.Wounded, StopClass.Critical, "다쳤다", S.RoomName(v.Room), w.By, v.Room); EmergencyTrigger(3, w.By); }   // --- time-on-demand
        }

        public void Collapse(Actor v, string why)
        {
            if (v.Status != ActorStatus.Active) return;
            v.Status = ActorStatus.Unconscious; v.Pose = S.R(Stream.Combat).Chance(0.5) ? Pose.LieFront : Pose.LieBack; v.Speed = 0;
            if (v.Act != null) EndActivity(v, false); v.TalkingTo = null;
            if (v.HandR != null) DropItem(v, S.I(v.HandR), v.Pos); if (v.HandL != null) DropItem(v, S.I(v.HandL), v.Pos);
            S.Log("Collapse", v.Id, room: v.Room, pos: v.Pos, data: why, secret: true);
            S.Emit(GameEventType.Collapse, v.Id, pos: v.Pos, text: why);
            Sound(SoundKind.Fall, v.Pos, 0.4f, v.Id);
        }

        public void Die(Actor v, string by, string cause, Wound w = null)
        {
            if (!v.Alive) return;
            v.Status = ActorStatus.Dead; v.Body.Dead = true; v.Body.DeathClock = S.Clock; v.Body.DeathCause = cause; v.Body.DeathBy = by; v.Body.DeathRoom = v.Room;
            // died in a rescuer's arms on the way to the infirmary: the rescuer feels it, stops and lays the body down right there
            var carrier = v.CarriedBy != null ? S.A(v.CarriedBy) : null;
            if (carrier != null && carrier.Carrying == v.Id && carrier.Act != null && carrier.Act.Id.StartsWith("case:rescue"))
            {
                carrier.Carrying = null; v.CarriedBy = null; v.Pos = carrier.Pos; v.Room = carrier.Room; v.Body.DeathRoom = v.Room; v.Pose = Pose.LieBack;
                S.Log("CarryEnd", carrier.Id, v.Id, room: carrier.Room, pos: carrier.Pos, data: "died");
                S.Emit(GameEventType.Carry, carrier.Id, v.Id, value: 0);
                EndActivity(carrier, false); carrier.Anim = Anim.Idle; carrier.Emotion = Emotion.Crying; carrier.Needs.Fear = MathX.Clamp01(carrier.Needs.Fear + 0.3f);
            }
            if (v.Pose == Pose.Stand || v.Pose == Pose.Sit || v.Pose == Pose.Crouch) v.Pose = S.R(Stream.Combat).Pick(new[] { Pose.LieBack, Pose.LieFront, Pose.LieSide, Pose.Slumped });
            if (v.Act != null) EndActivity(v, false); v.TalkingTo = null; v.Speed = 0; v.Following = null;
            if (v.HandR != null) DropItem(v, S.I(v.HandR), v.Pos); if (v.HandL != null) DropItem(v, S.I(v.HandL), v.Pos);
            var ev = S.Log("Death", v.Id, by, room: v.Room, pos: v.Pos, data: cause, secret: true);
            v.Body.DeathSeq = ev.Seq;
            S.Emit(GameEventType.Death, v.Id, by, pos: v.Pos, text: cause);
            if (w != null && w.Type != DamageType.Choke && w.Type != DamageType.Drown && w.Type != DamageType.Shock && w.Type != DamageType.Burn)   // no pool of blood under a strangled, drowned, electrocuted or poisoned body
            AddTrace("BloodPool", v.Pos, v.Room, by, v.Id, 0.6f + (w != null && (w.Type == DamageType.Cut || w.Type == DamageType.Stab) ? 0.7f : 0f), 0, "시신 밑에 고인 피", "시신이 이 자리에 쓰러진 뒤 흘러나온 피", "여기서 숨졌는지는 따로 확인해야 한다");
            Gore.OnDeath(this, v, w);   // pool / smear / handprint / nail marks, toppled furniture, at most one scene trace
            Cases.OnDeath(this, v, by, cause, w);
            if (v.IsPlayer) { SetPhaseIfDaily(); }
        }

        void SetPhaseIfDaily() { S.Flags["player_dead"] = S.Clock; S.Emit(GameEventType.Notice, Cast.Player, text: "김민혁이 쓰러졌다", key: "player_dead"); }

        // per-tick body processes: bleeding, delayed death, waking up
        void Bodies()
        {
            ViolenceTick();    // --- violence track (Sim/Violence): prolonged assaults, bindings, drag marks (own fault guard)
            Gore.Tick(this);   // door handprints of the bleeding, furniture set right after a chapter
            double dt = S.ClockRate * SimTime.Dt; // clock minutes
            foreach (var a in S.Actors.Values)
            {
                if (!a.Alive) continue; var b = a.Body;
                if (b.Bleed > 0 && !b.Stabilized)
                {
                    b.BloodLoss += (float)(b.Bleed * dt * 0.12);
                    if (a.Speed > 0.2f && S.Tick % 8 == 0) AddTrace("BloodDrip", a.Pos, a.Room, a.Id, a.Id, 0.18f, 1, "핏방울", "다친 사람이 이 길로 지나갔다", "누구의 피인지");
                    if (b.BloodLoss >= 1f) { Die(a, b.Wounds.LastOrDefault()?.By, "과다 출혈", b.Wounds.LastOrDefault()); continue; }
                    if (b.BloodLoss > 0.6f && a.Status == ActorStatus.Active) Collapse(a, "피를 많이 흘려 쓰러짐");
                }
                if (b.PoisonBy != null && !b.Stabilized && a.Status == ActorStatus.Active && b.DeathAt >= 0 && S.Clock >= b.DeathAt - 8) { Collapse(a, "갑자기 가슴을 움켜쥐고 쓰러짐"); b.Conscious = 0.05f; }
                if (b.DeathAt >= 0 && !b.Stabilized && S.Clock >= b.DeathAt) { var last = b.Wounds.LastOrDefault(); bool poison = last == null && b.PoisonBy != null; Die(a, last?.By ?? b.PoisonBy, poison ? "중독" : WoundProfiles.Get(last?.Region ?? BodyRegion.Chest, last?.Type ?? DamageType.Blunt, last?.Sev ?? 3).Kor, last); continue; }
                if (a.Status == ActorStatus.Unconscious && b.UnconsciousUntil >= 0 && S.Clock >= b.UnconsciousUntil && b.BloodLoss < 0.6f && b.Conscious > 0.05f)
                { a.Status = ActorStatus.Active; a.Pose = Pose.Sit; b.UnconsciousUntil = -1; a.NextThink = S.Clock; S.Log("WakeUp", a.Id, room: a.Room); S.Emit(GameEventType.Pose, a.Id, data: "wake"); Cases.OnVictimWake(this, a); }
            }
        }

        /// <summary>Rescue with a first aid kit (player or NPC). Success depends on time/state, not QTE.</summary>
        public bool FirstAid(Actor helper, Actor v)
        {
            if (!v.Alive || v.Body.Stabilized) return false;
            bool kit = Carried(helper).Any(i => i.Def?.Tag == "rescue");
            if (!kit && !(S.Layout.Room(v.Room)?.Type == RoomType.Infirmary)) return false;
            var b = v.Body;
            b.Stabilized = true; b.Bleed = 0; if (b.DeathAt >= 0 && b.DeathAt - S.Clock > 0.3) b.DeathAt = -1; foreach (var w in b.Wounds) w.Treated = true;
            S.Log("FirstAid", helper.Id, v.Id, room: v.Room, pos: v.Pos);
            S.Emit(GameEventType.Rescue, helper.Id, v.Id, pos: v.Pos);
            Relations.Change(S, v.Id, helper.Id, trust: 0.3f, like: 0.3f, attach: 0.2f, memory: "목숨을 구해 줬다");
            Cases.OnRescued(this, v, helper);
            return b.DeathAt < 0;
        }

        // ------------------------------------------------------------------ traces
        public Trace AddTrace(string type, P3 pos, int room, string source, string victim, float size, int vis, string title, string know, string unknown, string item = null)
        {
            // merge nearby drips to keep counts sane
            if (type == "BloodDrip" || type == "FootprintWet" || type == "FootprintBlood" || type == "Scuff")   // one mark per spot, not one per blow
            {
                var near = S.Traces.FirstOrDefault(t => t.Type == type && t.Pos.f == pos.f && t.Pos.DistXZ(pos) < 0.7f && S.Clock - t.Clock < 20);
                if (near != null) return near;
            }
            var tr = new Trace { Id = S.NewId("tr"), Type = type, Pos = pos, Room = room, Clock = S.Clock, Seq = S.Seq, Size = size, Source = source, Victim = victim, Visibility = vis, Desc = title, Know = know, Unknown = unknown, Item = item };
            S.Traces.Add(tr);
            S.Log("Trace", source, victim, item, room, pos, type, secret: true);
            S.Emit(GameEventType.Trace, source, victim, text: type, pos: pos, room: room, value: size, data: tr.Id);
            return tr;
        }
    }
}
