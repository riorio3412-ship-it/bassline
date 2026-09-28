using System;
using System.Globalization;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// The wall (owner premise, 2026-09-28). The residents came because an invitation promised to grant a wish; nobody came
    /// to kill, and in the first days nobody has a reason they could live with. So every loop starts with a wall on top of
    /// each resident's own restraint (<see cref="Base"/>: morality, fear, empathy, this loop's state of mind), a little
    /// higher for those who took the chapter's pact at the first table (LifeTable "pact"; again each chapter, "다시").
    ///
    /// Only what happens wears the wall down — never the clock alone (Relations.Pressure's rule): the house's pushes
    /// (<see cref="HousePush"/>), its hunger, a death (the loop's first most: it can happen), a killer who walked out with the
    /// wish, an innocent drawn and executed. A new chapter lets part of it grow back — half of the wear after the court
    /// caught the killer (everyone watched what getting caught costs), a fifth after it failed — so each chapter has its own
    /// calm, push and break, and 민혁's 심판 sets the pace of the next. Both murder minds read it through
    /// <see cref="Inhibit"/> (Relations.Pressure for the old planner, Initiative's event motives), so every threshold,
    /// conscience check and retreat sees the same wall. The wall lives in this loop's flags; a new loop builds it again.
    /// </summary>
    public static class Conscience
    {
        /// <summary>Test hook: off = the old inhibition only (to compare runs).</summary>
        public static bool Enabled = true;
        /// <summary>"Nobody came here to kill."</summary>
        public const float Start = 0.32f;
        /// <summary>The loop's first chapter: each of the house's first two steps not yet taken ("no reason yet").</summary>
        public const float Unpushed = 0.15f;
        /// <summary>Took the pact at the chapter's first table — until the chapter's first death breaks it (the renewed pact
        /// of a later chapter holds half as much).</summary>
        public const float PactBonus = 0.1f;

        /// <summary>The resident's own restraint: morality, fear, empathy, and this loop's state of mind (inh:, D-038).</summary>
        public static float Base(GameState S, Actor a)
        {
            var c = a.Def; float inh = c.P.Morality * 0.85f + c.P.Fearfulness * 0.1f + c.Empathy / 100f * 0.15f;
            // the order is the character's; the spread is narrowed a quarter toward the middle, so the coldest resident is the
            // likeliest to break but not a foregone first killer (FirstsScan: 도윤 was the first in ~45% of games)
            inh = Spread + (inh - Spread) * 0.75f;
            if (S.Flags.TryGetValue("inh:" + a.Id, out var d)) inh = Math.Max(0.05f, inh + (float)d);   // who holds, who cracks, differs each loop
            return inh;
        }

        /// <summary>The middle of the cast's restraint, toward which <see cref="Base"/> narrows the spread.</summary>
        const float Spread = 0.55f;

        /// <summary>Restraint plus what is left of the wall — the inhibition both murder minds subtract.</summary>
        public static float Inhibit(GameState S, Actor a) => Base(S, a) + Wall(S, a.Id);

        /// <summary>What is left of the wall for this resident now: 0 once the house and the deaths have worn it through, below 0
        /// (down to <see cref="Floor"/>) when a silence drags on past the house's last step and it presses on their own restraint.</summary>
        public static float Wall(GameState S, string id)
        {
            if (!Enabled) return 0;
            float w = Start;
            if (TookPact(S, id) && !PactBroken(S)) w += S.Chapter <= 1 ? PactBonus : PactBonus * 0.5f;
            // the loop's first chapter, before anyone has died: until the house has given its first two reasons (the token, the
            // envelopes — taken or passed over), nobody has one of their own
            if (S.Chapter <= 1 && !S.Flags.ContainsKey($"firstdeath:{S.Loop}"))
            {
                int passed = HousePush.Enabled ? HousePush.Next(S) : 2;
                if (passed < 1) w += Unpushed; if (passed < 2) w += Unpushed;
            }
            w -= Worn(S, null) + Worn(S, id);
            return Math.Max(Floor, w);
        }
        /// <summary>How far past the wall the house's pressure can reach into a resident's own restraint (a long silence).</summary>
        public const float Floor = -0.3f;

        public static bool TookPact(GameState S, string id) => TookPact(S, id, S.Chapter);
        public static bool TookPact(GameState S, string id, int chapter) => S.Flags.ContainsKey($"pact:{S.Loop}:{chapter}:{id}");
        public static bool PactBroken(GameState S) => S.Flags.ContainsKey($"pactbroken:{S.Loop}:{S.Chapter}");

        static float Worn(GameState S, string id) => S.Flags.TryGetValue(Key(S, id), out var v) ? (float)v : 0;
        static string Key(GameState S, string id) => id == null ? $"wall:{S.Loop}" : $"wall:{S.Loop}:{id}";

        /// <summary>Wear the wall down (id null: everyone). Logged, so a case can say what wore it.</summary>
        public static void Erode(GameState S, string id, float amount, string why)
        {
            if (amount <= 0) return;
            var k = Key(S, id); S.Flags[k] = (S.Flags.TryGetValue(k, out var v) ? v : 0) + amount;
            S.Log("WallErode", id, data: why + ":" + amount.ToString("0.00", CultureInfo.InvariantCulture));
        }

        /// <summary>Build a little of it back for one resident (민혁 reminding a table of the pact): a personal credit of at
        /// most <see cref="MendCap"/> against the shared wear.</summary>
        public static void Mend(GameState S, string id, float amount, string why)
        {
            if (id == null || amount <= 0) return;
            var k = Key(S, id); double v = S.Flags.TryGetValue(k, out var x) ? x : 0;
            S.Flags[k] = Math.Max(-MendCap, v - amount);
            S.Log("WallMend", id, data: why + ":" + amount.ToString("0.00", CultureInfo.InvariantCulture));
        }
        public const float MendCap = 0.08f;

        /// <summary>A death became public: the chapter's first breaks its pact, and the loop's first shows everyone it can
        /// happen; those who loved the victim have grief looking for someone to blame.</summary>
        public static void OnDeath(Simulation sim, Incident inc)
        {
            var S = sim.S;
            bool loopFirst = !S.Flags.ContainsKey($"firstdeath:{S.Loop}");
            if (loopFirst) S.Flags[$"firstdeath:{S.Loop}"] = S.Clock;
            if (!PactBroken(S)) { S.Flags[$"pactbroken:{S.Loop}:{S.Chapter}"] = S.Clock; Erode(S, null, loopFirst ? 0.16f : 0.08f, loopFirst ? "first-death" : "chapter-death"); }
            else Erode(S, null, 0.05f, "death");
            foreach (var a in S.LivingNpcs.OrderBy(x => x.Id, StringComparer.Ordinal))
                if (S.HasRel(a.Id, inc.Victim) && S.R(a.Id, inc.Victim).Attach > 0.3f) Erode(S, a.Id, 0.04f, "grief");
        }

        /// <summary>A verdict carried out. A wrong one: the killer walked out with the wish — proof it is real and that it
        /// works — and an innocent was drawn and executed.</summary>
        public static void OnSettlement(Simulation sim, Settlement set)
        {
            var S = sim.S;
            if (set.Correct) return;
            if (set.Escaped != null) Erode(S, null, 0.08f, "escape");
            if (set.Executed != null) Erode(S, null, 0.03f, "injustice");
        }

        /// <summary>A new chapter: part of the wall grows back — half of every wear after the court caught the killer (the
        /// deterrent), a fifth after it failed (the wish walked out of the door).</summary>
        public static void OnChapter(Simulation sim)
        {
            var S = sim.S; if (S.Chapter <= 1) return;
            var last = S.Settlements.LastOrDefault(s => s.Loop == S.Loop);
            float keep = last == null ? 0.8f : last.Correct ? 0.5f : 0.8f;
            string pre = $"wall:{S.Loop}";
            foreach (var k in S.Flags.Keys.Where(k => k == pre || k.StartsWith(pre + ":", StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList())
                if (S.Flags[k] > 0) S.Flags[k] *= keep;
            S.Log("WallMend", null, data: (last?.Correct == true ? "deterrent" : "regroup") + ":" + (1 - keep).ToString("0.00", CultureInfo.InvariantCulture));
        }
    }
}
