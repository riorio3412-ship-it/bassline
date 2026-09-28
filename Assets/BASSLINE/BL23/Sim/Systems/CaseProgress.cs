using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// How far the player's investigation of the current case has come: the key things there are to find
    /// (the body, marks left at and around the scene, the object that did it, the people who saw something)
    /// and which of them the player has actually examined or heard. Shown as "단서 n/m" — a goal, never a hint
    /// (the UI lists only what has been found; the rest stay unnamed).
    /// </summary>
    public static class CaseProgress
    {
        public sealed class Clue { public string Key, Label; public bool Found; }

        public static Incident Current(GameState S) =>
            S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Confirmed).OrderBy(i => i.ConfirmClock).FirstOrDefault();

        public static List<Clue> Clues(Simulation sim)
        {
            var S = sim.S; var res = new List<Clue>();
            var inc = Current(S); if (inc == null) return res;
            var k = S.K(Cast.Player); var victim = S.A(inc.Victim);
            double death = victim != null && victim.Body.DeathClock > 0 ? victim.Body.DeathClock : inc.ConfirmClock;
            res.Add(new Clue { Key = "body:" + inc.Victim, Label = Cast.NameOf(inc.Victim) + "의 시신", Found = k.Examined.Contains("body:" + inc.Victim) });
            // marks at the scene and on the way to it
            var vpos = victim?.Pos;
            var traces = S.Traces.Where(t => !t.Cleaned && t.Visibility <= 2 && t.Clock <= inc.ConfirmClock + 5 && (t.Victim == inc.Victim || t.Room == inc.FoundRoom && t.Clock >= death - 90))
                                 .OrderBy(t => vpos.HasValue ? t.Pos.DistXZ(vpos.Value) : 0f).ThenBy(t => t.Id).Take(5);
            foreach (var t in traces) res.Add(new Clue { Key = "trace:" + t.Id, Label = string.IsNullOrEmpty(t.Desc) ? "현장의 흔적" : t.Desc, Found = k.Examined.Contains("trace:" + t.Id) });
            // the object that did it, wherever it is now
            var plan = inc.PlanId != null && S.Plans.TryGetValue(inc.PlanId, out var p) ? p : null;
            if (plan?.Weapon != null && S.Items.ContainsKey(plan.Weapon))
                res.Add(new Clue { Key = "item:" + plan.Weapon, Label = S.I(plan.Weapon).Kor ?? "흉기로 쓰였을 물건", Found = k.Examined.Contains("item:" + plan.Weapon) });
            // people who saw the victim (or whoever did it) around the time
            int talks = 0;
            foreach (var w in S.LivingNpcs.OrderBy(x => x.Id))
            {
                if (w.Id == inc.Culprit) continue;
                var kw = S.K(w.Id);
                bool saw = kw.Sightings.Any(s => (s.Target == inc.Victim || s.Target == inc.Culprit) && s.T1 >= death - 60 && s.T0 <= death + 20);
                if (!saw) continue;
                bool heard = k.Statements.Any(st => st.Speaker == w.Id && st.Clock >= S.Ch.FirstAnnounce);
                res.Add(new Clue { Key = "talk:" + w.Id, Label = Cast.GivenOf(w.Id) + "의 증언", Found = heard });
                if (++talks >= 4) break;
            }
            return res;
        }
    }
}
