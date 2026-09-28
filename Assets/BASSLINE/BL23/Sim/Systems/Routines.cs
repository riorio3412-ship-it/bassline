using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Everyday routines between people, the small visible things that make the house feel lived in:
    /// sitting with a friend who is grieving or frightened, drifting over to listen when someone plays, asking a friend
    /// for a walk (they come along side by side), bringing tea to someone who looks worn out, and, when anger has built
    /// up against someone, going to have it out with them. Each is chosen by the ordinary life AI (scored against
    /// meals, hobbies, sleep and so on) and changes needs and relationships only when it actually happens.
    /// Activity ids carry the other person ("life:comfort:P05") so the outcome can be applied when it is done.
    /// </summary>
    public sealed partial class Simulation
    {
        void RoutineCandidates(Actor a, Rng rng, List<(double score, Func<Activity> make, string id)> cands)
        {
            RoutineCandidatesInner(a, rng, cands);
        }

        /// <summary>Called before the "carry on where you are" habit: a strong reason (a grieving friend, music starting next door,
        /// a friend asking for a walk) can pull someone out of what they were settled into.</summary>
        Activity UrgentRoutine(Actor a, Rng rng)
        {
            var list = new List<(double score, Func<Activity> make, string id)>(); RoutineCandidatesInner(a, rng, list);
            foreach (var c in list.OrderByDescending(x => x.score)) { if (c.score < 1.9 || !rng.Chance(0.55)) break; var act = c.make(); if (act != null) return act; }
            return null;
        }
        void RoutineCandidatesInner(Actor a, Rng rng, List<(double score, Func<Activity> make, string id)> cands)
        {
            if (a.IsPlayer || a.IsButler || S.Phase != Phase.Daily) return;
            int minute = S.Minute; bool night = minute >= 22 * 60 || minute < 7 * 60;
            var n = a.Needs; var p = a.Def.P; var k = S.K(a.Id);
            bool Known(Actor x) => x.Room == a.Room || (k.LastSeen.TryGetValue(x.Id, out var ls) && S.Clock - ls.t < 40);
            bool Free(Actor x) => x.Alive && x.Status == ActorStatus.Active && x.Pose != Pose.Sleep && x.StairId < 0 && x.CarriedBy == null && x.TalkingTo == null && (x.Act == null || x.Act.Interruptible) && x.PlanId == null;
            float Like(string to) => S.HasRel(a.Id, to) ? S.R(a.Id, to).Like : 0f;
            string Day(string what, string who) => $"rt:{what}:{a.Id}:{who}:{S.Day}";

            // 1) comfort: a friend who is grieving or badly frightened (once a day per pair)
            if (n.Grief < 0.6f && n.Fear < 0.7f)
            {
                var sad = S.LivingNpcs.Where(x => x != a && !x.IsButler && Free(x) && Known(x) && x.Pos.f == a.Pos.f && (x.Needs.Grief > 0.3f || x.Needs.Fear > 0.6f || x.Emotion == Emotion.Crying || x.Act?.Id == "life:mourn") && Like(x.Id) > 0.12f && !S.Flags.ContainsKey(Day("comfort", x.Id)))
                                      .OrderByDescending(x => Like(x.Id) + x.Needs.Grief).FirstOrDefault();
                if (sad != null) cands.Add((1.4 + Like(sad.Id) * 2.5 + sad.Needs.Grief + p.Loyalty * 0.5, () => ComfortActivity(a, sad, rng), "comfort"));
            }
            // 2) audience: someone is playing or performing close by
            if (!night && n.Fun < 0.75f)
            {
                var player = S.Living.Where(x => x != a && x.Room >= 0 && x.Pos.f == a.Pos.f && x.Act != null && (x.Act.Id == "life:music" && x.Anim == Anim.Play || x.Act.Id == "life:perform" && x.Anim == Anim.Talk) && a.Pos.Dist(x.Pos) < 30f).OrderBy(x => a.Pos.Dist(x.Pos)).FirstOrDefault();
                if (player != null && !S.Flags.ContainsKey(Day("audience", player.Id)))
                    cands.Add((1.2 + (1 - n.Fun) * 1.2 + p.Sociability * 0.6 + Like(player.Id), () => AudienceActivity(a, player, rng), "audience"));
            }
            // 3) a walk with a friend who is right here and free
            if (!night && n.Energy > 0.35f && n.Social < 0.8f && rng.Chance(0.5))
            {
                var friend = S.LivingNpcs.Where(x => x != a && Free(x) && x.Room == a.Room && Like(x.Id) > 0.3f && S.HasRel(x.Id, a.Id) && S.R(x.Id, a.Id).Like > 0.2f && !S.Flags.ContainsKey(Day("stroll", x.Id))).OrderByDescending(x => Like(x.Id)).FirstOrDefault();
                if (friend != null) cands.Add((1.0 + Like(friend.Id) * 1.5 + p.Sociability * 0.5, () => StrollActivity(a, friend, rng), "stroll"));
            }
            // 4) tea for someone who looks worn out (the kind-hearted do this)
            if (!night && p.Loyalty + p.Sociability > 0.85f && n.Energy > 0.3f)
            {
                var tired = S.LivingNpcs.Where(x => x != a && Known(x) && x.Pos.f == a.Pos.f && x.Alive && x.Pose != Pose.Sleep && (x.Needs.Stress > 0.42f || x.Needs.Energy < 0.32f || x.Needs.Grief > 0.35f) && Like(x.Id) > 0.15f && !S.Flags.ContainsKey(Day("tea", x.Id))).OrderByDescending(x => x.Needs.Stress + Like(x.Id)).FirstOrDefault();
                var kitchen = S.Layout.Rooms.FirstOrDefault(r => (r.Type == RoomType.Kitchen || r.Type == RoomType.TeaRoom) && RoomUsable(a, r));
                if (tired != null && kitchen != null) cands.Add((1.5 + Like(tired.Id) * 1.5 + tired.Needs.Stress, () => TeaActivity(a, tired, kitchen, rng), "tea"));
            }
            // 5) have it out: anger built up against someone
            if (n.Anger > 0.42f)
            {
                var foe = S.LivingNpcs.Concat(new[] { S.Player }).Where(x => x != null && x != a && Known(x) && x.Pos.f == a.Pos.f && x.Alive && x.Pose != Pose.Sleep && S.HasRel(a.Id, x.Id) && S.R(a.Id, x.Id).Grudge > 0.28f && !S.Flags.ContainsKey(Day("confront", x.Id)))
                                      .OrderByDescending(x => S.R(a.Id, x.Id).Grudge).FirstOrDefault();
                if (foe != null && !foe.IsPlayer) cands.Add((1.0 + n.Anger * 2 + p.Aggression, () => ConfrontActivity(a, foe), "confront"));
            }
            // --- daily-life (begin)
            LifeCandidates(a, rng, cands);   // Sim/Life/LifeAftermath.cs: a grieving resident's own act the day after a death
            // --- daily-life (end)
        }

        P3 BesidePos(Actor a, Actor t, float dist)
        {
            // a free place about 'dist' from them, on our side, not on top of anybody else
            float dx = a.Pos.x - t.Pos.x, dz = a.Pos.z - t.Pos.z; float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d < 0.05f) { dx = 1; dz = 0; d = 1; }
            var g = S.Layout.Nav(t.Pos.f); P3 best = Snap(t.Pos); float bestScore = -99f;
            for (int i = 0; i < 8; i++)
            {
                double ang = Math.Atan2(dz, dx) + (i % 2 == 0 ? 1 : -1) * (i / 2) * 0.55;
                var p = new P3(t.Pos.f, t.Pos.x + (float)Math.Cos(ang) * dist, t.Pos.z + (float)Math.Sin(ang) * dist);
                int k = g.CellOf(p.x, p.z); if (!g.Walkable(k) || !g.InMain(k)) continue;
                float free = Math.Min(1.2f, NearestOther(p, a)); float score = free - i * 0.05f;
                if (score > bestScore) { bestScore = score; best = p; }
                if (free >= 1.0f) break;
            }
            return best;
        }

        Activity ComfortActivity(Actor a, Actor t, Rng rng)
        {
            S.Flags[$"rt:comfort:{a.Id}:{t.Id}:{S.Day}"] = S.Clock;
            var act = new Activity { Id = "life:comfort:" + t.Id, Label = Cast.GivenOf(t.Id) + " 곁에서 위로", Priority = 3 };
            act.Steps.Add(GoTo(BesidePos(a, t, 0.95f)));
            act.Steps.Add(new ActionStep { Kind = "Face", Actor = t.Id });
            act.Steps.Add(new ActionStep { Kind = "Say", Tag = "comfort", Actor = t.Id });
            act.Steps.Add(Do("company", rng.Range(8, 16), Anim.Listen));
            return act;
        }

        Activity AudienceActivity(Actor a, Actor performer, Rng rng)
        {
            S.Flags[$"rt:audience:{a.Id}:{performer.Id}:{S.Day}"] = S.Clock;
            var act = new Activity { Id = "life:audience:" + performer.Id, Label = Cast.GivenOf(performer.Id) + "의 연주 감상", Priority = 2 };
            var room = S.Layout.Room(performer.Room);
            // a seat facing them if there is one free, else a place to stand at a polite distance
            var seat = room?.Spots.Select(i => S.Layout.Spots[i]).Where(s => s.Occupant == null && (s.Tag == "sit" || s.Tag == "read") && s.Pos.DistXZ(performer.Pos) < 7f && s.Pos.DistXZ(performer.Pos) > 1.5f).OrderBy(s => s.Pos.DistXZ(performer.Pos)).FirstOrDefault();
            if (seat != null) { act.Steps.Add(GoTo(seat.Approach)); act.Steps.Add(Do("listen", rng.Range(12, 24), Anim.Listen, seat.Id)); }
            else { act.Steps.Add(GoTo(BesidePos(a, performer, 2.4f))); act.Steps.Add(new ActionStep { Kind = "Face", Actor = performer.Id }); act.Steps.Add(Do("listen", rng.Range(10, 20), Anim.Listen)); }
            act.Steps.Add(new ActionStep { Kind = "Say", Tag = "applause", Actor = performer.Id });
            return act;
        }

        Activity StrollActivity(Actor a, Actor friend, Rng rng)
        {
            S.Flags[$"rt:stroll:{a.Id}:{friend.Id}:{S.Day}"] = S.Clock;
            var places = S.Layout.Rooms.Where(r => (r.Type == RoomType.Greenhouse || r.Type == RoomType.Gallery || r.Type == RoomType.Courtyard || r.Type == RoomType.RainCorridor || r.Type == RoomType.GrandHall) && RoomUsable(a, r)).ToList();
            if (places.Count == 0) return null;
            var dest = rng.Weighted(places, r => 1.0 / (1 + a.Pos.Dist(new P3(r.Floor, r.Rect.CX, r.Rect.CZ)) / 12.0));
            var act = new Activity { Id = "life:stroll:" + friend.Id, Label = Cast.GivenOf(friend.Id) + "하고 산책", Priority = 2 };
            act.Steps.Add(new ActionStep { Kind = "Face", Actor = friend.Id });
            act.Steps.Add(new ActionStep { Kind = "Say", Tag = "stroll_invite", Data = dest.Name, Actor = friend.Id });
            var mid = RandomPointIn(dest, rng);
            act.Steps.Add(GoTo(mid));
            act.Steps.Add(Do("walk", rng.Range(4, 8), Anim.Idle));
            act.Steps.Add(GoTo(RandomPointIn(dest, rng)));
            act.Steps.Add(Do("walk", rng.Range(6, 12), Anim.Idle));
            // the friend comes along, side by side, for about as long
            var follow = new Activity { Id = "life:strollwith:" + a.Id, Label = Cast.GivenOf(a.Id) + "하고 산책", Priority = 2 };
            follow.Steps.Add(new ActionStep { Kind = "Say", Tag = "stroll_accept", Actor = a.Id });
            follow.Steps.Add(new ActionStep { Kind = "Follow", Actor = a.Id, Tag = "companion", Duration = 26 });
            Assign(friend, follow);
            return act;
        }

        Activity TeaActivity(Actor a, Actor t, Room kitchen, Rng rng)
        {
            S.Flags[$"rt:tea:{a.Id}:{t.Id}:{S.Day}"] = S.Clock;
            var act = new Activity { Id = "life:teafor:" + t.Id, Label = Cast.GivenOf(t.Id) + "에게 차 대접", Priority = 2 };
            var spot = kitchen.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Occupant == null && (s.Tag == "cook" || s.Tag == "stand"));
            if (spot != null) { act.Steps.Add(GoTo(spot.Approach)); act.Steps.Add(Do("brew", rng.Range(4, 7), Anim.Cook, spot.Id)); }
            else { act.Steps.Add(GoTo(RandomPointIn(kitchen, rng))); act.Steps.Add(Do("brew", rng.Range(4, 7), Anim.Cook)); }
            act.Steps.Add(new ActionStep { Kind = "Talk", Actor = t.Id, Duration = 0 });
            act.Steps.Add(new ActionStep { Kind = "Say", Tag = "offer_tea", Actor = t.Id });
            return act;
        }

        Activity ConfrontActivity(Actor a, Actor t)
        {
            S.Flags[$"rt:confront:{a.Id}:{t.Id}:{S.Day}"] = S.Clock;
            var act = new Activity { Id = "life:confront:" + t.Id, Label = Cast.GivenOf(t.Id) + "에게 항의", Priority = 3 };
            act.Steps.Add(GoTo(BesidePos(a, t, 1.2f)));
            act.Steps.Add(new ActionStep { Kind = "Face", Actor = t.Id });
            act.Steps.Add(Do("confront", 0.4, Anim.Angry));
            act.Steps.Add(new ActionStep { Kind = "Say", Tag = "confront", Actor = t.Id });
            act.Steps.Add(Do("confront", 1.5, Anim.CrossArms));
            return act;
        }

        /// <summary>What a routine leaves behind once it has actually been done.</summary>
        void RoutineDone(Actor a, string id)
        {
            string Other(string prefix) => id.Length > prefix.Length ? id.Substring(prefix.Length) : null;
            if (id.StartsWith("life:comfort:"))
            {
                var t = S.A(Other("life:comfort:")); if (t == null || !t.Alive || t.Room != a.Room) return;
                t.Needs.Grief = MathX.Clamp01(t.Needs.Grief - 0.22f); t.Needs.Fear = MathX.Clamp01(t.Needs.Fear - 0.2f); t.Needs.Stress = MathX.Clamp01(t.Needs.Stress - 0.15f); t.Needs.Social = MathX.Clamp01(t.Needs.Social + 0.25f);
                Relations.Change(S, t.Id, a.Id, like: 0.08f, trust: 0.07f, attach: 0.05f, memory: "힘들 때 곁에 있어 줬다");
                Relations.Change(S, a.Id, t.Id, attach: 0.03f);
                Speak(t, "comforted", a.Id); S.Log("Comfort", a.Id, t.Id, room: a.Room);
            }
            else if (id.StartsWith("life:audience:"))
            {
                var t = S.A(Other("life:audience:")); a.Needs.Fun = MathX.Clamp01(a.Needs.Fun + 0.2f); a.Needs.Stress = MathX.Clamp01(a.Needs.Stress - 0.08f);
                if (t != null && t.Alive) { Relations.Change(S, t.Id, a.Id, like: 0.04f, memory: "내 연주를 들어 줬다"); Relations.Change(S, a.Id, t.Id, like: 0.03f); if (t.Room == a.Room && S.R(Stream.Life).Chance(0.5)) Speak(t, "applause_thanks", a.Id); }
            }
            else if (id.StartsWith("life:stroll:"))
            {
                var t = S.A(Other("life:stroll:")); a.Needs.Social = MathX.Clamp01(a.Needs.Social + 0.2f); a.Needs.Stress = MathX.Clamp01(a.Needs.Stress - 0.1f);
                if (t != null && t.Alive) { t.Needs.Social = MathX.Clamp01(t.Needs.Social + 0.2f); Relations.Change(S, t.Id, a.Id, like: 0.04f, attach: 0.03f, memory: "같이 산책했다"); Relations.Change(S, a.Id, t.Id, like: 0.03f, attach: 0.03f); }
            }
            else if (id.StartsWith("life:teafor:"))
            {
                var t = S.A(Other("life:teafor:")); if (t == null || !t.Alive || t.Room != a.Room) return;
                t.Needs.Stress = MathX.Clamp01(t.Needs.Stress - 0.15f); t.Needs.Energy = MathX.Clamp01(t.Needs.Energy + 0.08f);
                Relations.Change(S, t.Id, a.Id, like: 0.06f, trust: 0.03f, memory: "차를 타 줬다"); Speak(t, "thanks_tea", a.Id); S.Log("TeaFor", a.Id, t.Id, room: a.Room);
            }
            else if (id.StartsWith("life:confront:"))
            {
                var t = S.A(Other("life:confront:")); a.Needs.Anger = MathX.Clamp01(a.Needs.Anger - 0.35f);
                if (t != null && t.Alive && t.Room == a.Room)
                {
                    t.Needs.Anger = MathX.Clamp01(t.Needs.Anger + 0.2f); t.Needs.Stress = MathX.Clamp01(t.Needs.Stress + 0.1f);
                    Relations.Change(S, t.Id, a.Id, like: -0.06f, grudge: 0.06f, memory: "사람들 앞에서 나한테 따졌다"); Relations.Change(S, a.Id, t.Id, grudge: -0.08f);
                    Speak(t, "confront_reply", a.Id); S.Log("Confront", a.Id, t.Id, room: a.Room);
                }
            }
        }
    }
}
