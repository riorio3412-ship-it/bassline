using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// F6 — after a death and after the verdict (DailyLifeDesign §8). The table talks of it (tt_death: 준서 counts the spoons,
    /// 은결 lays a lily), a memorial is held (LifeFest), grief shows as each resident's own act for a day (§8.2: 라온's one low
    /// note, 민서 doing the dead's chores, 세나 practising until her hand cramps …), the one who inherits a goal says so and does
    /// it, close residents come to 민혁 (은결 with a lily), and after the 심판: relief and guilt at the table, grudges along the
    /// vote lines when the verdict was wrong, a rumour of who steered it, and one resident who asks him for a walk that night.
    /// Yusti's seat counts are the voice packs' (YustiVoice). Hook for grief acts: Routines → LifeCandidates.
    /// </summary>
    public sealed partial class Simulation
    {
        void AftermathMinute(int mod)
        {
            // deaths the house has confirmed
            foreach (var inc in S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Confirmed).OrderBy(i => i.Id, StringComparer.Ordinal))
            {
                if (S.Flags.ContainsKey("lad:" + inc.Victim)) continue;
                S.Flags["lad:" + inc.Victim] = S.Clock; LFinc("lad:n");
                S.Log("LifeDeath", inc.Victim, data: inc.Id);
            }
            // verdicts
            foreach (var set in S.Settlements.Where(s => s.Loop == S.Loop && s.Applied).ToList())
            {
                if (S.Flags.ContainsKey("lav:" + set.Id)) continue;
                S.Flags["lav:" + set.Id] = S.Clock;
                OnVerdictLife(set);
            }
            // a goal passed on: the heir says so (goal_inherit) and takes it up
            double seq = LF("lgoalseq");
            if (S.Ledger.Count > 0 && S.Ledger[S.Ledger.Count - 1].Seq > seq) S.Flags["lgoalseq"] = S.Ledger[S.Ledger.Count - 1].Seq;
            foreach (var e in LedgerSince(seq).Where(e => e.Type == "GoalHandover").ToList())
            {
                var heir = S.A(e.Actor); if (heir == null || !heir.Alive) continue;
                var u = LifeKeyU(heir.Id, null, "goal_inherit", new SceneRun { Kind = "life", Npc = heir.Id, Ctx = { ["t"] = e.Target, ["victim"] = e.Target, ["act"] = e.Data ?? "그 일" } });
                if (u != null) LifeSayLater(new[] { u }, 1);
                LFinc("lgoal:inherit");
            }
            if (S.Phase != Phase.Daily) return;
            // close residents come to 민혁 with their grief (은결 brings a lily), on the first daily day after a death
            var d = LifeLastDeath();
            if (d != null && mod >= 9 * 60 && mod <= 20 * 60 && mod % 30 == 10 && !S.Flags.ContainsKey("lgriefgo:" + d.Victim))
            {
                S.Flags["lgriefgo:" + d.Victim] = S.Clock;
                if (S.A("P14")?.Alive == true && d.Victim != "P14" && d.Culprit != "P14") LifePurpose("P14", "grief", d.Victim);
                var close = S.LivingNpcs.Where(x => x.Id != "P14" && x.Id != d.Culprit && S.R(x.Id, Cast.Player).Attach >= 0.1f && S.R(x.Id, d.Victim).Like > 0.15f).OrderByDescending(x => S.R(x.Id, d.Victim).Like).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
                if (close != null) LifePurpose(close.Id, "grief", d.Victim);
            }
            // the night after a 심판: one who is attached to 민혁 asks him for a walk
            if (mod == 21 * 60)
                foreach (var set in S.Settlements.Where(s => s.Loop == S.Loop && s.Applied && !S.Flags.ContainsKey("lavwalk:" + s.Id)).ToList())
                {
                    S.Flags["lavwalk:" + set.Id] = S.Clock;
                    var w = S.LivingNpcs.Where(x => S.R(x.Id, Cast.Player).Attach >= 0.12f && x.Id != set.Culprit).OrderByDescending(x => S.R(x.Id, Cast.Player).Attach).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
                    if (w != null) LifePurpose(w.Id, "verdict", set.Id);
                }
        }

        void OnVerdictLife(Settlement set)
        {
            // a wrong verdict: the executed one's friends turn on those who voted for them; talk of who steered the vote
            if (!set.Correct && !set.Exception && set.Executed != null)
            {
                string e = set.Executed;
                var friends = S.LivingNpcs.Where(f => f.Id != e && (S.R(f.Id, e).Like > 0.25f || S.R(f.Id, e).Attach > 0.2f)).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
                var voters = set.Votes.Where(kv => kv.Value == e && kv.Key != e).Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal).ToList();
                foreach (var f in friends) foreach (var v in voters) if (v != f.Id) Relations.Change(S, f.Id, v, grudge: 0.08f, trust: -0.05f, memory: LineBank.FixParticles($"{Cast.GivenOf(e)}에게 표를 던졌다"));
                string steer = voters.Contains("P05") && S.A("P05")?.Alive == true ? "P05" : voters.FirstOrDefault(v => S.A(v)?.Alive == true && v != Cast.Player);
                string origin = friends.Select(f => f.Id).FirstOrDefault();
                if (steer != null && origin != null) SeedRumour("vote", steer, e, origin, -1, false);
                LFinc("lav:wrong");
            }
            S.Log("LifeVerdict", set.Executed, data: set.Id + ":" + (set.Correct ? "correct" : set.Exception ? "exception" : "wrong"));
        }

        // ------------------------------------------------------------------ grief as a visible act (Routines hook)
        /// <summary>Hook: Routines.RoutineCandidatesInner — a grieving resident's own act the day after a death (§8.2).</summary>
        internal void LifeCandidates(Actor a, Rng rng, List<(double score, Func<Activity> make, string id)> cands)
        {
            if (a == null || a.IsPlayer || a.IsButler || S.Phase != Phase.Daily || a.PlanId != null) return;
            var d = LifeLastDeath(); if (d == null || S.Clock - d.ConfirmClock > 36 * 60 || d.Culprit == a.Id) return;
            if (a.Needs.Grief < 0.25f && S.R(a.Id, d.Victim).Like < 0.2f) return;
            if (S.Flags.ContainsKey($"lgriefact:{a.Id}:{d.Victim}")) return;
            int m = S.Minute; if (m < 9 * 60 || m > 21 * 60) return;
            cands.Add((1.55 + a.Needs.Grief * 1.5, () => GriefActivity(a, d.Victim), "life:grief"));
        }

        static readonly Dictionary<string, (string rooms, string act, string label, Anim anim)> GriefActs = new Dictionary<string, (string, string, string, Anim)>
        {
            ["P02"] = ("Dining;Lounge", "observe", "빈 의자를 바라보는 중", Anim.Idle),
            ["P03"] = ("Archive;Library;Dining", "organize", "명단을 고쳐 쓰는 중", Anim.Write),
            ["P04"] = ("Workshop;Gallery", "restore", "고인의 물건을 손보는 중", Anim.Craft),
            ["P05"] = ("GrandHall;Theater;Lounge", "speech", "추도사를 다듬는 중", Anim.Write),
            ["P06"] = ("Storage;Archive", "organize", "고인의 물건을 목록으로 적는 중", Anim.Write),
            ["P07"] = ("Library;Lounge", "read", "수첩에 단어 하나를 적는 중", Anim.Write),
            ["P08"] = ("MusicRoom;Theater", "music", "낮은 음 하나를 오래 치는 중", Anim.Play),
            ["P09"] = ("Lounge;Theater", "perform", "쉬지 않고 말하다 멈춘 중", Anim.Talk),
            ["P10"] = ("Kitchen;Dining", "cook", "한 사람 몫을 더 만드는 중", Anim.Cook),
            ["P11"] = ("Workshop", "repair", "고인이 망가뜨린 걸 고치는 중", Anim.Craft),
            ["P12"] = ("Lounge;Theater;Greenhouse", "rest", "별 스티커 하나를 쥐고 있는 중", Anim.Idle),
            ["P13"] = ("GameRoom;Pool", "exercise", "손에 쥐가 날 때까지 연습하는 중", Anim.Exercise),
            ["P14"] = ("Chapel;Greenhouse", "craft", "종이 백합을 접는 중", Anim.Craft),
            ["P15"] = ("Archive;Library", "investigate", "날짜와 날씨만 적는 중", Anim.Write),
            ["P16"] = ("Wardrobe;Gallery;Lounge", "style", "선물 장부를 넘기는 중", Anim.Read),
            ["P17"] = ("DollRoom;Lounge;Workshop", "craft", "종이 인형을 올렸다 떼는 중", Anim.Craft),
            ["P18"] = ("Kitchen;Dining;Storage", "cleanup", "고인의 당번 몫을 하는 중", Anim.Clean),
        };

        Activity GriefActivity(Actor a, string victim)
        {
            if (!GriefActs.TryGetValue(a.Id, out var g)) return null;
            S.Flags[$"lgriefact:{a.Id}:{victim}"] = S.Clock;
            var types = g.rooms.Split(';');
            var room = S.Layout.Rooms.Where(r => types.Contains(r.Type.ToString()) && RoomUsable(a, r)).OrderBy(r => Array.IndexOf(types, r.Type.ToString())).ThenBy(r => a.Pos.Dist(new P3(r.Floor, r.Rect.CX, r.Rect.CZ))).FirstOrDefault();
            if (room == null) return null;
            var act = new Activity { Id = "life:grief", Label = g.label, Priority = 2 };
            var spot = room.Spots.Select(i => S.Layout.Spots[i]).Where(s => s.Occupant == null && (s.Tag == "sit" || s.Tag == "work" || s.Tag == "play" || s.Tag == "pray" || s.Tag == "cook" || s.Tag == "read")).OrderBy(s => s.Id).FirstOrDefault();
            act.Steps.Add(GoTo(spot != null ? spot.Approach : RandomPointIn(room, LR)));
            act.Steps.Add(new ActionStep { Kind = "Say", Tag = "grief_act", Actor = victim });
            act.Steps.Add(Do(g.act, 30 + LR.R(20), g.anim, spot?.Id ?? -1));
            LFinc("lgrief:acts");
            S.Log("GriefAct", a.Id, victim, room: room.Id, data: g.act);
            return act;
        }
    }
}
