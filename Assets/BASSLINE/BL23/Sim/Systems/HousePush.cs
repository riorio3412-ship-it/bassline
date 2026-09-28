using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// The house's pushes (owner premise, 2026-09-28). Nobody came to kill, so a quiet chapter is the house's problem. While
    /// nobody has died this chapter, it raises the stakes one step at a time, at the morning bell (07:00) or the evening
    /// bell (21:00), never more than one step per half day. Each step wears the wall down (<see cref="Conscience"/>) and
    /// feeds something the murder minds already read:
    ///   1 견본        the wish is real — a token of it in every room (WishDesire; 민혁 gets an empty envelope)
    ///   2 과거의 봉투   somebody's past in somebody else's room: CH06 imposed mid-chapter (secrets, knows-my-secret)
    ///   3 두 번째 소원  the first to get away with it may wish once more, for someone else (love, loyalty)
    ///   4 정기 소등    the lights go out twice today: CH03 imposed (the dark, its "house-dark" moments)
    ///   5 저택의 인내   another room swallowed, less on the plates (Hunger, one level at once)
    /// After the last step, every further quiet morning is 긴 침묵: the house presses on, each morning harder than the last
    /// (strain, and the wall worn past zero into their own restraint, Conscience.Floor) until someone breaks — a quiet chapter
    /// cannot stall the loop, however well 민혁 holds people together.
    /// Hunger keeps its own morning clock beside these. A death ends the ladder for the chapter; the next chapter picks it
    /// up where the house left off (the token is shown once a loop), sooner. No step orders anyone to kill — Yusti says so.
    /// </summary>
    public static class HousePush
    {
        /// <summary>Test hook: off = the house only hungers (to compare runs).</summary>
        public static bool Enabled = true;

        sealed class Step
        {
            public string Id, Name; public bool Evening;
            public double First, Later;               // hours since the chapter began (chapter 1 of a loop / later chapters)
            public Func<Simulation, bool> Can;        // false: this step cannot happen this chapter (skipped)
            public Action<Simulation> Apply;
        }

        static readonly Step[] Ladder =
        {
            new Step { Id = "sample", Name = "소원의 견본", Evening = false, First = 18, Later = 12, Can = CanSample, Apply = Sample },
            new Step { Id = "envelope", Name = "과거의 봉투", Evening = true, First = 30, Later = 12, Can = CanEnvelope, Apply = Envelope },
            new Step { Id = "bonus", Name = "두 번째 소원", Evening = false, First = 44, Later = 20, Can = _ => true, Apply = Bonus },
            new Step { Id = "blackout", Name = "정기 소등", Evening = false, First = 60, Later = 30, Can = CanBlackout, Apply = Blackout },
            new Step { Id = "patience", Name = "저택의 인내", Evening = false, First = 90, Later = 44, Can = s => Hunger.Level(s.S) < 4, Apply = Patience },
        };

        /// <summary>The name of a step (table topics, the notebook), or null.</summary>
        public static string NameOf(string id) => Ladder.FirstOrDefault(s => s.Id == id)?.Name;

        static string Key(GameState S) => $"push:{S.Loop}:{S.Chapter}";
        /// <summary>How far up the ladder the house has gone this chapter (steps taken or skipped).</summary>
        public static int Next(GameState S) => S.Flags.TryGetValue(Key(S), out var v) ? (int)v : 0;
        /// <summary>Has this step been taken this chapter?</summary>
        public static bool Taken(GameState S, string id) => S.Flags.ContainsKey($"pushed:{S.Loop}:{S.Chapter}:{id}");

        /// <summary>The morning bell (07:00, after Hunger) and the evening bell (21:00).</summary>
        public static void Bell(Simulation sim, bool evening)
        {
            var S = sim.S;
            if (!Enabled || S.Phase != Phase.Daily || S.Survivors <= S.FloorLocked) return;
            if (S.Incidents.Values.Any(i => i.Loop == S.Loop && i.Chapter == S.Chapter)) return;   // a death this chapter: the house has what it wanted
            if (S.Flags.TryGetValue($"pushat:{S.Loop}:{S.Chapter}", out var last) && S.Clock - last < 10 * 60) return;
            double h = (S.Clock - S.Ch.ChapterStartClock) / 60.0; bool first = S.Chapter == 1;
            if (Next(S) >= Ladder.Length)
            {
                // 긴 침묵: past the ladder, each quiet morning
                if (evening) return;
                S.Flags[$"pushat:{S.Loop}:{S.Chapter}"] = S.Clock; int n = (int)(S.Flags.TryGetValue($"pushsilence:{S.Loop}:{S.Chapter}", out var sv) ? sv : 0) + 1;
                S.Flags[$"pushsilence:{S.Loop}:{S.Chapter}"] = n;
                S.Log("HousePush", Cast.Butler, data: "silence" + n);
                sim.Announce("y_push_silence", null, "push:silence");
                Conscience.Erode(S, null, 0.04f * n, "silence");   // each silent morning weighs more than the last
                foreach (var a in S.LivingNpcs) { a.Needs.Stress = MathX.Clamp01(a.Needs.Stress + 0.05f); a.Needs.Anger = MathX.Clamp01(a.Needs.Anger + 0.03f); }
                if (n == 1) Confide(sim, "silence");
                return;
            }
            for (int i = Next(S); i < Ladder.Length; i++)
            {
                var st = Ladder[i];
                if (!st.Can(sim)) { S.Flags[Key(S)] = i + 1; S.Log("HousePushSkip", Cast.Butler, data: st.Id); continue; }
                if (st.Evening != evening || h < (first ? st.First : st.Later)) return;   // wait for its bell
                S.Flags[Key(S)] = i + 1; S.Flags[$"pushat:{S.Loop}:{S.Chapter}"] = S.Clock; S.Flags[$"pushed:{S.Loop}:{S.Chapter}:{st.Id}"] = S.Clock;
                S.Log("HousePush", Cast.Butler, data: st.Id);
                S.Dev($"HOUSE PUSH {st.Id} ch{S.Chapter} h{h:0}");
                st.Apply(sim);
                if (st.Id == "sample" || st.Id == "bonus") Confide(sim, st.Id);
                return;
            }
        }

        /// <summary>After a push, the two it shook hardest who trust 민혁 enough come to tell him (LifeDialogue "confide"): what he
        /// says holds them back or loosens them. Those who already handed him their token are past it.</summary>
        static void Confide(Simulation sim, string step)
        {
            var S = sim.S;
            var who = S.LivingNpcs.Where(a => !S.Flags.ContainsKey($"gavesample:{S.Loop}:{a.Id}") && S.HasRel(a.Id, Cast.Player))
                .Select(a => (a, r: S.R(a.Id, Cast.Player)))
                .Where(x => x.r.Like >= 0.08f || x.r.Trust >= 0.15f || x.r.Attach >= 0.08f)
                .OrderByDescending(x => x.a.Def.P.WishDesire + x.r.Like * 0.5f + x.r.Trust * 0.3f - Conscience.Wall(S, x.a.Id))
                .ThenBy(x => x.a.Id, StringComparer.Ordinal).Take(2).Select(x => x.a.Id).ToList();
            foreach (var id in who) { sim.LifePurpose(id, "confide", step); S.Log("Confide", id, Cast.Player, data: step); }
        }

        // ------------------------------------------------------------------ 1 견본: the wish is real
        static bool CanSample(Simulation sim) => !sim.S.Flags.ContainsKey($"pushsample:{sim.S.Loop}");

        static void Sample(Simulation sim)
        {
            var S = sim.S; S.Flags[$"pushsample:{S.Loop}"] = S.Clock;
            sim.Announce("y_push_sample", null, "push:sample");
            foreach (var a in S.LivingNpcs.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                float wd = a.Def.P.WishDesire;
                S.K(a.Id).Facts.Add("wish-sample");
                a.Needs.Stress = MathX.Clamp01(a.Needs.Stress + 0.04f);
                Conscience.Erode(S, a.Id, 0.04f + 0.1f * wd, "sample");
            }
            S.K(Cast.Player).Facts.Add("wish-sample:none");
        }

        // ------------------------------------------------------------------ 2 과거의 봉투 (CH06, imposed)
        static bool CanEnvelope(Simulation sim)
        {
            var S = sim.S;
            if (S.RuleActive("CH06") || S.RuleActive("CH05") || S.RuleActive("CH09") || S.Survivors < 6) return false;
            return S.LivingNpcs.Count(a => !string.IsNullOrEmpty(a.Def.Secret)) >= 2;
        }

        static void Envelope(Simulation sim)
        {
            var S = sim.S;
            if (Rules.Impose(sim, "CH06", "y_push_rule") == null) return;
            Conscience.Erode(S, null, 0.03f, "envelope");
        }

        // ------------------------------------------------------------------ 3 두 번째 소원: one more wish, for someone else
        static void Bonus(Simulation sim)
        {
            var S = sim.S; S.Flags[$"pushbonus:{S.Loop}:{S.Chapter}"] = S.Clock;
            sim.Announce("y_push_bonus", null, "push:bonus");
            foreach (var a in S.LivingNpcs.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                // someone to wish for: the one this resident holds dearest among the living
                float dear = S.LivingNpcs.Where(x => x.Id != a.Id && S.HasRel(a.Id, x.Id)).Select(x => S.R(a.Id, x.Id).Attach).DefaultIfEmpty(0).Max();
                Conscience.Erode(S, a.Id, 0.05f + 0.06f * MathX.Clamp01(dear), "bonus");
            }
        }

        // ------------------------------------------------------------------ 4 정기 소등 (CH03, imposed)
        static bool CanBlackout(Simulation sim) { var S = sim.S; return !S.RuleActive("CH03") && !S.RuleActive("CH23") && !S.RuleActive("CH02") && S.Survivors >= 5; }

        static void Blackout(Simulation sim)
        {
            var S = sim.S;
            if (Rules.Impose(sim, "CH03", "y_push_rule") == null) return;
            Conscience.Erode(S, null, 0.04f, "blackout");
            foreach (var a in S.LivingNpcs) a.Needs.Fear = MathX.Clamp01(a.Needs.Fear + 0.05f);
        }

        // ------------------------------------------------------------------ 5 저택의 인내: the hunger, all at once
        static void Patience(Simulation sim)
        {
            var S = sim.S;
            Hunger.Tighten(sim, "y_push_patience", "push:patience");
            Conscience.Erode(S, null, 0.06f, "patience");
        }
    }
}
