using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    public sealed partial class Simulation
    {
        public static readonly int[] MealStart = { 8 * 60, 12 * 60 + 30, 18 * 60 + 30 };
        public const int MealLen = 60;
        public bool MealTime(out int slot)
        {
            int m = S.Minute; for (int i = 0; i < 3; i++) if (m >= MealStart[i] && m < MealStart[i] + MealLen) { slot = i; return true; }
            slot = -1; return false;
        }

        // ------------------------------------------------------------------ brain
        void Think(Actor a)
        {
            var rng = S.R(Stream.Life);
            a.NextThink = S.Clock + rng.Range(0.4f, 1.2f);
            if (a.IsButler) { Butler.Think(this, a); return; }
            if (S.Phase == Phase.Prologue) { if (a.Act == null) { var act = new Activity { Id = "listen", Label = "안내 듣기" }; act.Steps.Add(WaitStep(2)); Assign(a, act); } return; }
            if (a.Act != null && !a.Act.Interruptible) return;
            // 1. crime system gets first say (secret)
            var crime = Crime.Think(this, a);
            if (crime != null) { if (a.Act == null || a.Act.Id != crime.Id) Assign(a, crime); return; }
            // 2. case duties (reporting, rescue, investigation, assembly)
            var duty = Cases.Think(this, a);
            if (duty != null) { if (a.Act == null || a.Act.Id != duty.Id || a.Act.Priority < duty.Priority) Assign(a, duty); return; }
            if (a.Act != null) return; // keep doing what we're doing
            // 3. everyday life
            var life = ChooseLife(a, rng);
            if (life != null) Assign(a, life);
        }

        Activity ChooseLife(Actor a, Rng rng)
        {
            var c = a.Def; var n = a.Needs; var cands = new List<(double score, Func<Activity> make, string id)>();
            int minute = S.Minute; bool night = minute >= 23 * 60 || minute < 7 * 60;
            { var urgent = UrgentRoutine(a, rng); if (urgent != null) return urgent; }
            // settled: once someone is somewhere they like, they carry on there (another book, another round, more tea)
            // unless a meal, sleep, fear or a pressing need pulls them away
            {
                var here = S.Layout.Room(a.Room);
                bool pulled = night || n.Energy < 0.25f || n.Hunger > 0.7f || n.Fear > 0.55f || n.Grief > 0.5f || (MealTime(out int ms) && n.Hunger > 0.25f && !S.Flags.ContainsKey($"ate:{a.Id}:{S.Day}:{ms}"))
                              || (c.Hobbies.Contains("cook") && MealStart.Any(m => m - minute > 0 && m - minute <= 45));
                if (here != null && !pulled && !RoomInfo.IsPassage(here.Type) && here.Type != RoomType.Bedroom && Activities.All.Any(d => d.Rooms.Contains(here.Type) && d.Id != "sleep" && d.Id != "eat" && d.Id != "cook"))
                {
                    double stay = 0.68 + (here.Id == HauntRoom(a) ? 0.22 : 0) + (S.Living.Count(x => x != a && x.Room == here.Id) > 0 ? 0.1 : 0) - n.Stress * 0.2;
                    if (rng.Chance(stay)) { var cont = HauntActivity(a, here, rng); if (cont != null) return cont; }
                }
            }
            // sleep
            double sleepScore = (night ? 3.0 : 0) + (1 - n.Energy) * 2.5 - (minute > 6 * 60 && minute < 22 * 60 ? 1.5 : 0);
            if (a.Needs.Fear > 0.6f && night) sleepScore += 0.5; // hide in room
            cands.Add((sleepScore, () => SleepActivity(a), "sleep"));
            // eat
            if (MealTime(out int slot) && !(S.Flags.TryGetValue($"ate:{a.Id}:{S.Day}:{slot}", out var ate) && ate > 0))
            {
                // after the bell (breakfast, dinner) people come to the table together, hungry or not; lunch is by appetite
                bool bell = slot != 1 && S.Minute - MealStart[slot] < 30 && S.Phase == Phase.Daily;
                if ((bell ? n.Hunger > 0.05f : n.Hunger > 0.25f) && !(slot == 1 && ((a.Id.Length > 1 && int.TryParse(a.Id.Substring(1), out var aidn) ? aidn : 0) + S.Day) % 2 == 0 && n.Hunger < 0.8f))
                    cands.Add((1.5 + n.Hunger * 2.5 + c.P.Sociability * 0.3 + (bell ? 1.3 + c.P.Sociability * 0.9 : 0), () => EatActivity(a, slot), "eat"));
            }
            else if (n.Hunger > 0.7f) cands.Add((n.Hunger * 1.6, () => Simple(a, "snack"), "snack"));
            // cook: 준서 (or anyone with cook hobby) before meals
            if (c.Hobbies.Contains("cook"))
            {
                int next = MealStart.FirstOrDefault(m => m - minute > 0 && m - minute <= 45);
                if (next > 0 && !S.Flags.ContainsKey($"cooked:{S.Day}:{next}")) cands.Add((2.2, () => CookActivity(a, next), "cook"));
            }
            // the place this person usually spends this part of the day (learnable, like free-time locations)
            var haunt = S.Layout.Room(HauntRoom(a));
            if (haunt != null && !night)
            {
                double hs = 1.45 + rng.F() * 0.4 + (a.Room == haunt.Id ? 0.4 : 0) - n.Fear * 0.6;
                cands.Add((hs, () => HauntActivity(a, haunt, rng), "haunt"));
            }
            // hobbies
            foreach (var h in c.Hobbies)
            {
                if (!Activities.HobbyToActivity.TryGetValue(h, out var aid)) continue;
                var def = Activities.Get(aid); if (def == null || aid == "cook") continue;
                double s = 0.6 + (1 - n.Fun) * 1.2 + rng.F() * 0.5 - n.Stress * 0.2;
                if (haunt != null && def.Rooms.Contains(haunt.Type)) s += 0.5;
                if (night) s -= 1.2;
                cands.Add((s, () => Simple(a, aid), "hobby:" + aid));
            }
            // generic pastimes (kept rare: people settle somewhere rather than pace the halls)
            foreach (var aid in new[] { "walk", "read", "tea", "observe", "tour", "explore", "rest" })
            {
                double s = 0.12 + rng.F() * 0.5;
                if (aid == "explore") s += (S.Day <= 1 && S.Minute < 16 * 60 ? 1.0 : -0.2) * c.P.Curiosity;
                if (aid == "tour") s += c.P.Curiosity * 0.3 - n.Fear * 0.8;
                if (aid == "rest") s += (1 - n.Energy) * 1.2 + n.Stress * 0.6;
                if (night) s -= 1.0;
                cands.Add((s, () => aid == "explore" ? ExploreActivity(a, rng) : Simple(a, aid), aid));
            }
            // mourning
            if (n.Grief > 0.3f) cands.Add((n.Grief * 2 + 0.3, () => Simple(a, "mourn"), "mourn"));
            // social
            if (!night || a.Needs.Social > 0.8f)
            {
                double s = (1 - n.Social) * 2.0 * (0.4 + c.P.Sociability) + rng.F() * 0.4;
                if (S.Clock >= a.NextSocial) cands.Add((s, () => SocialActivity(a, rng), "social"));
            }
            // goals
            var g = Goals.NextStep(this, a); if (g != null) cands.Add((g.Priority, () => g, g.Id));
            // chapter rules (events)
            var rule = Rules.LifeActivity(this, a); if (rule != null) cands.Add((rule.Priority, () => rule, rule.Id));
            // fear: stay near others or in own locked room
            if (n.Fear > 0.55f) cands.Add((n.Fear * 2.2, () => SafetyActivity(a), "safety"));

            Grammars.LifeCandidates(this, a, cands);
            RoutineCandidates(a, rng, cands);
            var best = cands.OrderByDescending(x => x.score).Take(3).ToList();
            foreach (var pick in best)
            {
                var act = pick.make(); if (act != null) return act;
            }
            return null;
        }

        public Activity Simple(Actor a, string aid, int roomOverride = -1)
        {
            var def = Activities.Get(aid); if (def == null) return null;
            var rng = S.R(Stream.Life);
            Room room = null;
            if (roomOverride >= 0) room = S.Layout.Room(roomOverride);
            else
            {
                var rooms = S.Layout.Rooms.Where(r => def.Rooms.Contains(r.Type) && RoomUsable(a, r)).ToList();
                if (aid == "rest") { var bed = S.Layout.BedroomOf(a.Id); if (bed != null && (rng.Chance(0.6) || rooms.Count == 0)) room = bed; }
                if (room == null && rooms.Count > 0)
                {
                    var favs = a.Def.FavRooms; int hauntId = HauntRoom(a);
                    // stay where you are when you can; otherwise the haunt, a favourite, then whatever is near
                    room = rng.Weighted(rooms, r => (r.Id == a.Room ? 4 : 1) * (r.Id == hauntId ? 4 : 1) * (favs.Contains(r.Type.ToString()) ? 2.5 : 1) / (1 + a.Pos.Dist(new P3(r.Floor, r.Rect.CX, r.Rect.CZ)) / 10.0));
                }
            }
            if (room == null) return null;
            var spots = room.Spots.Select(i => S.Layout.Spots[i]).Where(s => s.Occupant == null && (def.Spots.Length == 0 || def.Spots.Contains(s.Tag))).ToList();
            if (aid == "rest" && room.Type == RoomType.Bedroom) spots = spots.Where(s => s.Tag == "sleep" || s.Tag == "sit").ToList();
            var act = new Activity { Id = "life:" + aid, Label = def.Kor, Priority = 1 };
            // pastimes last a while: people settle into what they are doing (longer still in their usual place)
            double dur = rng.Range((float)def.Min, (float)def.Max) * (aid == "sleep" || aid == "eat" || aid == "cook" ? 1 : room.Id == HauntRoom(a) ? 3.0 : 2.3);
            if (spots.Count > 0)
            {
                var sp = spots[rng.R(spots.Count)];
                act.Steps.Add(GoTo(sp.Approach)); act.Steps.Add(Do(aid, dur, def.Anim, sp.Id));
            }
            else
            {
                var p = RandomPointIn(room, rng); act.Steps.Add(GoTo(p)); act.Steps.Add(Do(aid, dur, def.Anim));
            }
            return act;
        }

        /// <summary>Where this person tends to be in the current block of the day (morning / afternoon / evening).
        /// Chosen once per block from favourite rooms, hobby rooms and goal rooms — others can learn and rely on it.</summary>
        public int HauntRoom(Actor a)
        {
            if (a == null || a.IsPlayer || a.IsButler) return -1;
            int m = S.Minute; int block = m < 12 * 60 ? 0 : m < 18 * 60 ? 1 : 2;
            string key = $"haunt:{a.Id}:{S.Day}:{block}";
            if (S.Flags.TryGetValue(key, out var hr)) { var r0 = S.Layout.Room((int)hr); if (r0 != null && RoomUsable(a, r0)) return r0.Id; }
            var rng = S.R(Stream.Life); var c = a.Def;
            var w = new Dictionary<int, double>();
            void Add(RoomType t, double v) { foreach (var r in S.Layout.Rooms.Where(r => r.Type == t && RoomUsable(a, r) && !RoomInfo.IsPassage(r.Type))) w[r.Id] = (w.TryGetValue(r.Id, out var o) ? o : 0) + v; }
            foreach (var f in c.FavRooms) if (Enum.TryParse<RoomType>(f, out var ft)) Add(ft, 3);
            foreach (var h in c.Hobbies) if (Activities.HobbyToActivity.TryGetValue(h, out var aid)) { var d = Activities.Get(aid); if (d != null) foreach (var t in d.Rooms) Add(t, 1.2); }
            foreach (var g in S.Goals.Values.Where(g => g.Owner == a.Id && !g.Done && !g.Abandoned)) { var gd = Goals.Catalog.FirstOrDefault(x => x.Id == g.Id); if (gd != null && gd.Activity != null) Add(gd.Room, 1.5); }
            if (block == 2) { Add(RoomType.Lounge, 1.2 * c.P.Sociability); Add(RoomType.TeaRoom, 0.8 * c.P.Sociability); var bed = S.Layout.BedroomOf(a.Id); if (bed != null) w[bed.Id] = (w.TryGetValue(bed.Id, out var ob) ? ob : 0) + 1.5 * (1 - c.P.Sociability); }
            if (w.Count == 0) return -1;
            int pick = rng.Weighted(w.ToList(), kv => kv.Value).Key;
            S.Flags[key] = pick;
            return pick;
        }

        Activity HauntActivity(Actor a, Room room, Rng rng)
        {
            // something this person would do in that room: a hobby first, else anything the room is made for
            var defs = Activities.All.Where(d => d.Rooms.Contains(room.Type) && d.Id != "sleep" && d.Id != "eat" && d.Id != "cook" && d.Id != "mourn").ToList();
            if (defs.Count == 0) { var act0 = new Activity { Id = "life:haunt", Label = "잠시 휴식", Priority = 1 }; act0.Steps.Add(GoTo(RandomPointIn(room, rng))); act0.Steps.Add(Do("rest", rng.Range(40, 90), Anim.Idle)); return act0; }
            var hob = a.Def.Hobbies.Select(h => Activities.HobbyToActivity.TryGetValue(h, out var x) ? x : null).Where(x => x != null).ToHashSet();
            var def = rng.Weighted(defs, d => hob.Contains(d.Id) ? 4 : d.Group ? 1.5 : 1);
            return Simple(a, def.Id, room.Id);
        }

        public P3 RandomPointIn(Room room, Rng rng)
        {
            // somewhere free: not where someone already stands or is walking to (personal space), else the least crowded try
            var g = S.Layout.Nav(room.Floor); P3 best = default; float bestD = -1f;
            for (int t = 0; t < 24; t++)
            {
                var p = new P3(room.Floor, room.Rect.x0 + 0.8f + rng.F() * Math.Max(0.1f, room.Rect.W - 1.6f), room.Rect.z0 + 0.8f + rng.F() * Math.Max(0.1f, room.Rect.D - 1.6f));
                int k = g.CellOf(p.x, p.z); if (!(g.Walkable(k) && g.Room[k] == room.Id && g.InMain(k))) continue;
                var c = g.Center(k); float md = NearestOther(c);
                if (md >= 1.2f) return c;
                if (md > bestD) { bestD = md; best = c; }
            }
            return bestD >= 0 ? best : new P3(room.Floor, room.Rect.CX, room.Rect.CZ);
        }

        public bool RoomUsable(Actor a, Room r)
        {
            if (r.Type == RoomType.Bedroom && r.Owner != a.Id) return false;
            if (r.Type == RoomType.ButlerRoom || r.Type == RoomType.Elevator || r.Type == RoomType.Courtroom || r.Void) return false;
            if (r.Doors.Count > 0 && r.Doors.All(d => S.Layout.Doors[d].Sealed)) return false;
            if (S.IsNight && RoomInfo.NightLocked(r.Type)) return false;
            if (Rules.RoomBlocked(this, a, r)) return false;
            var k = S.K(a.Id);
            foreach (var d in r.Doors) if (k.DoorLocked.TryGetValue(d, out var l) && l && !HasKey(a, S.Layout.Doors[d])) return r.Doors.Count > 1;
            return true;
        }

        Activity SleepActivity(Actor a)
        {
            var bed = S.Layout.BedroomOf(a.Id); if (bed == null) return null;
            var sp = bed.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Tag == "sleep");
            var act = new Activity { Id = "sleep", Label = "수면", Priority = 2 };
            // lock own door at night (most people do; the careless or trusting sometimes don't) — at the door, then to bed
            var door = bed.Doors.Count > 0 ? bed.Doors[0] : -1;
            bool lockIt = a.Def.P.Fearfulness > 0.25f || a.Needs.Fear > 0.2f || S.Chapter > 1 || S.R(Stream.Life).Chance(0.7);
            P3 atDoor = default;
            if (door >= 0) { var d = S.Layout.Doors[door]; atDoor = Snap(d.AlongX ? new P3(d.Pos.f, d.Pos.x, d.Pos.z + (bed.Rect.CZ > d.Pos.z ? 0.8f : -0.8f)) : new P3(d.Pos.f, d.Pos.x + (bed.Rect.CX > d.Pos.x ? 0.8f : -0.8f), d.Pos.z)); }
            if (door >= 0 && lockIt) { act.Steps.Add(GoTo(atDoor)); act.Steps.Add(new ActionStep { Kind = "Door", Door = door, Tag = "lock", Data = "취침" }); }
            act.Steps.Add(GoTo(sp != null ? sp.Approach : new P3(bed.Floor, bed.Rect.CX, bed.Rect.CZ)));
            double until = S.Minute >= 20 * 60 ? (1440 - S.Minute) + 7 * 60 + S.R(Stream.Life).Range(0, 50) : Math.Max(40, (7 * 60 + 20) - S.Minute);
            act.Steps.Add(Do("sleep", until, Anim.Sleep, sp?.Id ?? -1));
            if (door >= 0 && lockIt) { act.Steps.Add(GoTo(atDoor)); act.Steps.Add(new ActionStep { Kind = "Door", Door = door, Tag = "unlock", Data = "기상" }); }
            return act;
        }

        Activity EatActivity(Actor a, int slot)
        {
            var din = S.Layout.First(RoomType.Dining); if (din == null) return null;
            var spots = din.Spots.Select(i => S.Layout.Spots[i]).Where(s => s.Occupant == null && s.Tag == "sit").ToList();
            var rng = S.R(Stream.Life);
            // sit near friends if possible
            Spot sp = null;
            if (spots.Count > 0)
            {
                var friends = S.Living.Where(x => x != a && x.Room == din.Id && S.R(a.Id, x.Id).Opinion > 0.2).ToList();
                // CH15: a suggested dinner partner — accepted or politely refused (both change something)
                var partnerId = slot == 2 ? Rules.TablePartner(S, a.Id) : null; var partner = partnerId != null ? S.A(partnerId) : null;
                if (partner != null && partner.Alive && !S.Flags.ContainsKey($"ch15:{a.Id}:{S.Day}"))
                {
                    S.Flags[$"ch15:{a.Id}:{S.Day}"] = 1;
                    bool accept = S.R(a.Id, partnerId).Opinion > -0.2 && rng.Chance(0.75);
                    if (accept) { friends = new List<Actor> { partner }; Relations.Change(S, a.Id, partnerId, like: 0.04f, memory: "권해 준 자리에 같이 앉아 식사했다"); }
                    else Relations.Change(S, partnerId, a.Id, like: -0.03f, memory: "같이 앉자고 했다가 거절당했다");
                    S.Log("TableSeat", a.Id, partnerId, data: accept ? "accept" : "refuse");
                }
                sp = friends.Count > 0 ? spots.OrderBy(s => friends.Min(f => f.Pos.DistXZ(s.Pos))).First() : spots[rng.R(spots.Count)];
            }
            var act = new Activity { Id = "life:eat", Label = "식사", Priority = 2 };
            act.Steps.Add(GoTo(sp != null ? sp.Approach : new P3(din.Floor, din.Rect.CX, din.Rect.CZ)));
            act.Steps.Add(Do("eat", rng.Range(18, 30), Anim.Eat, sp?.Id ?? -1));
            act.Steps.Add(new ActionStep { Kind = "Flag", Tag = $"ate:{a.Id}:{S.Day}:{slot}" });
            return act;
        }

        Activity CookActivity(Actor a, int meal)
        {
            var k = S.Layout.First(RoomType.Kitchen); if (k == null) return null;
            var sp = k.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Tag == "cook" && s.Occupant == null);
            var act = new Activity { Id = "life:cook", Label = "식사 준비", Priority = 2.5 };
            act.Steps.Add(GoTo(sp != null ? sp.Approach : new P3(k.Floor, k.Rect.CX, k.Rect.CZ)));
            act.Steps.Add(Do("cook", Math.Max(15, meal - S.Minute - 3), Anim.Cook, sp?.Id ?? -1));
            act.Steps.Add(new ActionStep { Kind = "Flag", Tag = $"cooked:{S.Day}:{meal}" });
            return act;
        }

        Activity ExploreActivity(Actor a, Rng rng)
        {
            // visit a room this person hasn't seen yet in this loop — builds real knowledge of the new mansion
            var k = S.K(a.Id);
            var unseen = S.Layout.Rooms.Where(r => !RoomInfo.IsPassage(r.Type) && RoomUsable(a, r) && !k.Facts.Contains("visited:" + r.Id)).ToList();
            if (unseen.Count == 0) return null;
            var r0 = unseen.OrderBy(r => a.Pos.Dist(new P3(r.Floor, r.Rect.CX, r.Rect.CZ)) + rng.F() * 25).First();
            var act = new Activity { Id = "life:explore", Label = "저택 탐방", Priority = 1 };
            act.Steps.Add(GoTo(RandomPointIn(r0, rng))); act.Steps.Add(Do("explore", rng.Range(2, 6), Anim.Search));
            return act;
        }

        Activity SafetyActivity(Actor a)
        {
            // go where people are (or lock yourself in)
            var rng = S.R(Stream.Life);
            var crowd = S.Living.Where(x => x != a && x.Room >= 0).GroupBy(x => x.Room).OrderByDescending(gp => gp.Count()).FirstOrDefault();
            if (crowd != null && crowd.Count() >= 3 && rng.Chance(0.6 + a.Def.P.Sociability * 0.3))
            {
                var r = S.Layout.Room(crowd.Key); var act = new Activity { Id = "life:safety", Label = "사람들 곁으로 피신", Priority = 2 };
                act.Steps.Add(GoTo(RandomPointIn(r, rng))); act.Steps.Add(Do("observe", 20, Anim.Idle)); return act;
            }
            var bed = S.Layout.BedroomOf(a.Id); if (bed == null) return null;
            var act2 = new Activity { Id = "life:hide", Label = "방에서 칩거", Priority = 2 };
            if (bed.Doors.Count > 0)
            {
                var d = S.Layout.Doors[bed.Doors[0]];
                act2.Steps.Add(GoTo(Snap(d.AlongX ? new P3(d.Pos.f, d.Pos.x, d.Pos.z + (bed.Rect.CZ > d.Pos.z ? 0.8f : -0.8f)) : new P3(d.Pos.f, d.Pos.x + (bed.Rect.CX > d.Pos.x ? 0.8f : -0.8f), d.Pos.z))));
                act2.Steps.Add(new ActionStep { Kind = "Door", Door = d.Id, Tag = "lock", Data = "불안" });
            }
            act2.Steps.Add(GoTo(RandomPointIn(bed, rng)));
            act2.Steps.Add(Do("rest", 40, Anim.Idle));
            return act2;
        }

        // per-tick effects of an activity
        void LifeTick(Actor a, ActionStep st)
        {
            var def = Activities.Get(st.Tag); if (def == null) return;
            double span = Math.Max(1, st.Duration); float f = (float)(S.ClockRate * SimTime.Dt / span);
            var n = a.Needs;
            n.Hunger = MathX.Clamp01(n.Hunger + def.Hunger * f * (def.Id == "eat" ? Hunger.MealFactor(S) : 1f)); n.Energy = MathX.Clamp01(n.Energy + def.Energy * f);
            n.Fun = MathX.Clamp01(n.Fun + def.Fun * f * 2); n.Stress = MathX.Clamp01(n.Stress + def.Stress * f);
            if (def.Id == "sleep") a.Pose = Pose.Sleep;
            if (def.Noisy && S.Tick % 30 == 0) { Sound(def.Id == "music" ? SoundKind.Music : SoundKind.Laugh, a.Pos, 0.35f, a.Id); foreach (var x in S.Actors.Values) { if (x == a || !x.Alive || x.IsButler || x.IsPlayer || x.Room != a.Room && x.Pos.Dist(a.Pos) > 8) continue; if (x.Def.Dislikes.Any(d => d.Contains("소음") || d.Contains("소란")) && S.R(Stream.Life).Chance(0.3)) { x.Needs.Anger = MathX.Clamp01(x.Needs.Anger + 0.08f); Relations.Change(S, x.Id, a.Id, like: -0.02f, grudge: 0.01f, memory: "시끄럽게 굴어 거슬렸다"); } } }
        }

        void OnActivityDone(Actor a, string id)
        {
            if (id == null) return;
            if (id == "sleep") { a.Needs.Energy = 1; a.Needs.LastSleep = S.Clock; }
            if (id == "life:eat") { a.Needs.LastMeal = S.Clock; }
            if (id.StartsWith("goal:")) Goals.OnStepDone(this, a, id);
            if (id.StartsWith("life:")) RoutineDone(a, id);
            if (id.StartsWith("social")) a.NextSocial = S.Clock + S.R(Stream.Life).Range(50, 130) * (1.3f - a.Def.P.Sociability);
        }

        void OnEnterRoom(Actor a, int from, int to)
        {
            var k = S.K(a.Id); k.Facts.Add("visited:" + to);
            var r = S.Layout.Room(to);
            if (r != null && (r.Type == RoomType.MirrorWater || r.Type == RoomType.Pool && a.Pose == Pose.Stand && a.Act?.Cur?.Tag == "swim")) { a.Wet = true; a.WetUntil = S.Clock + 45; }
            if (a.Wet && S.Clock < a.WetUntil && S.Tick % 3 == 0) AddTrace("FootprintWet", a.Pos, to, a.Id, null, 0.25f, 1, "젖은 발자국", "누군가 몸이 젖은 채 지나갔다", "누구의 발자국인지, 정확히 언제인지");
            if (a.BloodOnClothes > 0.5f && S.R(Stream.Life).Chance(0.35)) AddTrace("FootprintBlood", a.Pos, to, a.Id, null, 0.25f, 1, "피 묻은 발자국 일부", "피를 밟은 누군가가 지나갔다", "누구인지");
            Rules.OnEnterRoom(this, a, r);
            Goals.OnEnterRoom(this, a, r);
            Grammars.OnRoomChange(this, a, from, to);
        }

        // ------------------------------------------------------------------ schedule (clock driven, public knowledge)
        int _lastMinute = -1;
        void Schedule()
        {
            int m = (int)Math.Floor(S.Clock);
            if (m == _lastMinute) return; _lastMinute = m;
            int mod = m % 1440;
            // needs drift per clock minute
            foreach (var a in S.Actors.Values)
            {
                if (!a.Alive || a.IsButler) continue;
                var n = a.Needs;
                n.Hunger = MathX.Clamp01(n.Hunger + 1f / 360f);
                if (a.Pose != Pose.Sleep) n.Energy = MathX.Clamp01(n.Energy - 1f / 1100f);
                n.Social = MathX.Clamp01(n.Social - 1f / 400f * (0.5f + a.Def.P.Sociability));
                n.Fun = MathX.Clamp01(n.Fun - 1f / 500f);
                n.Fear = MathX.Clamp01(n.Fear - 1f / 600f);
                n.Grief = MathX.Clamp01(n.Grief - 1f / 2400f);
                n.Anger = MathX.Clamp01(n.Anger - 1f / 300f);
                if (a.Wet && S.Clock > a.WetUntil) a.Wet = false;
            }
            if (mod == 7 * 60) { Announce("y_morning", null); Hunger.Morning(this); HousePush.Bell(this, false); }
            if (mod == 21 * 60) HousePush.Bell(this, true);
            if (mod == 22 * 60) Announce("y_night", null);
            // the meal bell: breakfast and dinner are shared at one table (lunch is taken when and where one likes)
            if (S.Phase == Phase.Daily && (mod == MealStart[0] || mod == MealStart[2]) && S.Layout.First(RoomType.Dining) != null)
            {
                Announce(mod < 12 * 60 ? "y_meal_breakfast" : "y_meal_dinner", null);
                // people put down what they are doing (most of them — the sociable first) and come to the table
                var mr = S.R(Stream.Life);
                foreach (var a in S.LivingNpcs.OrderBy(x => x.Id))
                    if (a.Status == ActorStatus.Active && a.Pose != Pose.Sleep && a.PlanId == null && a.TalkingTo == null && a.Act != null && a.Act.Interruptible && a.Act.Id != null && a.Act.Id.StartsWith("life:") && a.Act.Id != "life:eat" && mr.Chance(0.55 + a.Def.P.Sociability * 0.35))
                        Interrupt(a, mr.Range(0.3f, 6f));
            }
            House.Minute(this, mod);
            RequestsTick(mod);
            if (mod == 0) S.Ch.DaysElapsed++;
            if (mod % 60 == 0) { Housekeeping(); Grammars.Hourly(this, mod); }
            Rules.Tick(this, mod);
            // --- daily-life (begin)
            LifeMinute(mod);   // Sim/Life: pair scenes, the table, hearts and invitations, festivals, rumours, aftermath, foreshadow
            // --- daily-life (end)
        }

        static readonly HashSet<string> RoutineLedger = new HashSet<string> { "Enter", "Sound", "Speech", "Converse", "Knock", "DoorRattle", "TrialLine", "Unlock", "Lock" };
        /// <summary>Bound memory growth: routine records age out; anything tied to plans, bodies, weapons, lies or traces stays.</summary>
        void Housekeeping()
        {
            double cut = S.Clock - 36 * 60;
            S.Ledger.RemoveAll(e => e.Clock < cut && RoutineLedger.Contains(e.Type) && e.Plan == null);
            foreach (var key in S.Flags.Keys.Where(x => x.StartsWith("haunt:")).ToList()) { var parts = key.Split(':'); if (parts.Length > 2 && int.TryParse(parts[2], out var hd) && hd < S.Day - 1) S.Flags.Remove(key); }
            foreach (var k in S.Know.Values)
            {
                k.Sightings.RemoveAll(s => s.T1 < cut && !(s.Bloody || s.Carrying || s.Attacking || s.Dead || s.Disguise != null || (s.Held != null && (ItemCatalog.Get(s.Held)?.IsWeapon ?? false))));
                k.Heard.RemoveAll(h => h.Clock < cut && !(h.Kind == SoundKind.Scream || h.Kind == SoundKind.Strike || h.Kind == SoundKind.Crash || h.Kind == SoundKind.GlassBreak || h.Kind == SoundKind.Press || h.Kind == SoundKind.Splash));
                // everyday noise fades faster: plain chatter/steps/doors after 8 h, a recognised voice after 24 h
                double shortCut = S.Clock - 8 * 60, voiceCut = S.Clock - 24 * 60;
                k.Heard.RemoveAll(h => (h.Kind == SoundKind.Talk || h.Kind == SoundKind.Laugh || h.Kind == SoundKind.Music || h.Kind == SoundKind.Footsteps || h.Kind == SoundKind.Door || h.Kind == SoundKind.Clock || h.Kind == SoundKind.Knock) && (h.Voice == null ? h.Clock < shortCut : h.Clock < voiceCut));
                var old = k.ItemSeen.Where(kv => kv.Value.t < cut - 48 * 60).Select(kv => kv.Key).ToList(); foreach (var o in old) k.ItemSeen.Remove(o);
            }
            if (S.Flags.Count > 4000) foreach (var key in S.Flags.Keys.Where(x => x.StartsWith("told:") || x.StartsWith("alarm:") || x.StartsWith("seenbody:") || x.StartsWith("screamed:") || x.StartsWith("sawattack:") || x.StartsWith("blows:") || x.StartsWith("grevcheck:")).ToList()) S.Flags.Remove(key);
        }

        public void Announce(string key, Dictionary<string, string> slots, string rule = null)
        {
            // Yusti makes every announcement over the mansion speakers (he takes no part in the investigation itself); house-only keys fall back to the house voice
            string voice = LineBank.Has(Cast.Butler, key) ? Cast.Butler : LineBank.House;
            string text = YustiVoice.Announcement(this, voice, key, slots) ?? LineBank.Render(LineBank.Raw(voice, key, false, S.R(Stream.Presentation)) ?? key, slots, false);   // voice pack NPC00: seat counts, situations, no repeats (Content/Voice/Voice_NPC00.cs)
            var an = new Announcement { Clock = S.Clock, Key = key, Text = text, Rule = rule };
            // announcements go through the mansion speakers: everyone awake hears it; sleepers are woken by the chime
            foreach (var a in S.Actors.Values) if (a.Alive) { an.Heard.Add(a.Id); if (a.Pose == Pose.Sleep && key != "y_night") Wake(a); }
            S.Announcements.Add(an);
            S.Log("Announce", Cast.Butler, data: key + "|" + text);
            S.Emit(GameEventType.Announcement, Cast.Butler, text: text, key: key, data: rule);
        }
    }
}
