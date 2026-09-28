using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// F5 — the Morning and Evening Table (DailyLifeDesign §3.2). Each shared meal has ONE topic drawn from the world
    /// (a death, a verdict, a new rule, the house's hunger, the buddy proposal, yesterday's quarrel, today's event, an empty
    /// chair, a rumour, someone's habit, the food). Its owner opens (tt_&lt;kind&gt;), 2–4 diners react to the opener by name
    /// (tt_&lt;kind&gt;_re, with @opener pair variants from the voice packs), and 민혁 — when he is at the table — may say one thing
    /// (side with, defuse, stay silent). Without him the table talks anyway at meal + 25 minutes: the lines are overheard and
    /// the table's effects happen (who heard whose habit — DailyLife §7.4 — is exactly the knowledge a poisoner needs).
    /// Presentation: CinematicUI.TableTalk → LifeSceneUI.TableTalk → LifeTable.
    /// </summary>
    public sealed partial class Simulation
    {
        static readonly string[] TopicOrder = { "death", "verdict", "rule", "hunger", "buddy", "conflict", "event", "absent", "rumour", "habit", "food", "pact", "faction", "hev" };

        /// <summary>Presentation: 민혁 is at the table with these diners. The staged table scene, or null (already talked this meal).</summary>
        public LifeStage LifeTable(List<string> diners, string meal)
        {
            if (S.Phase != Phase.Daily || diners == null || (_stage != null && !_stage.Over)) return null;
            if (meal != "breakfast" && meal != "dinner" && meal != "lunch") return null;
            if (S.Flags.ContainsKey($"ltt:{S.Day}:{meal}")) return null;
            try
            {
                var run = TableBuild(diners.Where(d => S.A(d)?.Alive == true).Distinct().ToList(), meal, true);
                if (run == null) return null;
                _stage = run;
                var lines = RunFrom(run);
                if (run.Over) _stage = null;
                return ToStage(run, lines);
            }
            catch (Exception e) { Fault("life:table", e); _stage = null; return null; }
        }

        void TableTick(int mod)
        {
            for (int slot = 0; slot < 3; slot += 2)
            {
                if (mod != MealStart[slot] + 25) continue;
                string meal = slot == 0 ? "breakfast" : "dinner";
                if (S.Flags.ContainsKey($"ltt:{S.Day}:{meal}")) continue;
                var din = S.Layout.First(RoomType.Dining); if (din == null) continue;
                if (LifePlayerAround() && S.Player.Room == din.Id) continue;   // 민혁 is there: the presentation stages it
                var diners = S.LivingNpcs.Where(x => x.Room == din.Id && x.Status == ActorStatus.Active && x.Pose != Pose.Sleep && x.PlanId == null).OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => x.Id).ToList();
                if (diners.Count < 3) continue;
                var run = TableBuild(diners.Take(7).ToList(), meal, false); if (run == null) continue;
                var lines = RunFrom(run);
                LifeSayLater(lines, 0.3, 1.0);
                TableNpcOnly(run);
            }
        }

        // ------------------------------------------------------------------ choosing the topic
        (string kind, string subject, string owner) PickTopic(List<string> diners, string meal)
        {
            string Has(string id) => diners.Contains(id) ? id : null;
            string By(Func<string, double> score) => diners.OrderByDescending(score).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault();
            var found = new List<(string kind, string subject, string owner)>();
            // 0 pact: the chapter's first shared table, before anyone has died in it — they came for a wish, nobody came to kill
            // (the premise, owner 2026-09-28). 서윤 says it first ("다시" after a trial); whoever is at the table takes it
            // (Conscience reads pact:<loop>:<chapter>:<id>).
            bool pactDue = !S.Flags.ContainsKey($"ltpact:{S.Loop}:{S.Chapter}") && diners.Count >= 3 && !S.Incidents.Values.Any(i => i.Loop == S.Loop && i.Chapter == S.Chapter);
            var pact = ("pact", S.Chapter.ToString(CultureInfo.InvariantCulture), Has("P03") ?? Has("P05") ?? Has("P10") ?? Has("P12") ?? By(d => S.A(d).Def.P.Morality));
            if (pactDue && S.Chapter <= 1) return pact;
            // 1 death: the first shared meal after a confirmed death
            var dead = S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Confirmed && !S.Flags.ContainsKey("ltdeath:" + i.Victim)).OrderBy(i => i.ConfirmClock).FirstOrDefault();
            if (dead != null) found.Add(("death", dead.Victim, By(d => S.R(d, dead.Victim).Attach + S.R(d, dead.Victim).Like)));
            // 2 verdict: the first meal after a 심판
            var set = S.Settlements.Where(s => s.Loop == S.Loop && s.Applied && !S.Flags.ContainsKey("ltver:" + s.Id)).LastOrDefault();
            if (set != null) found.Add(("verdict", set.Executed ?? set.Accused, By(d => set.Votes.TryGetValue(d, out var v) && v == set.Executed ? 1 : 0)));
            // (a later chapter's pact waits for the table that talks the verdict through)
            if (pactDue && found.Count == 0) return pact;
            // 2b push: the house raised the stakes within half a day (HousePush: the wish's token, the second wish); its hunger
            // step is the hunger topic, its imposed rules the rule / envelope topics
            var pu = S.Announcements.Where(a => a.Rule != null && a.Rule.StartsWith("push:", StringComparison.Ordinal) && a.Rule != "push:patience" && a.Rule != "push:silence" && S.Clock - a.Clock < 12 * 60 && !S.Flags.ContainsKey($"ltpush:{S.Loop}:{a.Rule}")).OrderByDescending(a => a.Clock).FirstOrDefault();
            if (pu != null)
            {
                string step = pu.Rule.Substring(5);
                found.Add(("push", step, step == "sample" ? By(d => S.A(d).Def.P.WishDesire) : Has("P06") ?? Has("P05") ?? Has("P15") ?? Has("P03") ?? By(d => S.A(d).Def.P.Honesty)));
            }
            // 2c the house's evening today (HouseEvents): who is going and who is not — each in their own words, with their reason
            var hev = S.Gatherings.Where(g => HouseEvents.IsHouse(g) && !g.Cancelled && !g.Done && g.Revs.Count > 0 && (int)(g.Cur.Start / 1440) + 1 == S.Day && g.Cur.Start > S.Clock && !S.Flags.ContainsKey("lthev:" + g.Id)).OrderBy(g => g.Cur.Start).FirstOrDefault();
            if (hev != null)
            {
                string go = diners.Where(d => hev.Status.TryGetValue(d, out var st) && st == "accepted").OrderByDescending(d => S.A(d).Def.P.Sociability).ThenBy(d => d, StringComparer.Ordinal).FirstOrDefault();
                if (go != null && diners.Count >= 3) found.Add(("hev", hev.Id, go));
            }
            // 3 rule: a rule announced within half a day
            var an = S.Announcements.Where(a => a.Rule != null && !a.Rule.StartsWith("push:", StringComparison.Ordinal) && !a.Rule.StartsWith("hev:", StringComparison.Ordinal) && S.Clock - a.Clock < 12 * 60 && !S.Flags.ContainsKey("ltrule:" + a.Rule)).OrderByDescending(a => a.Clock).FirstOrDefault();
            if (an != null) { var ri = S.Ch.Rules.FirstOrDefault(r => r.Id == an.Rule || r.Rule == an.Rule); string owner = ri?.Targets.FirstOrDefault(t => diners.Contains(t)) ?? By(d => S.A(d).Needs.Stress); found.Add((ri != null && ri.Rule == "CH06" ? "envelope" : "rule", an.Rule, owner)); }
            // 4 hunger
            if (S.Flags.TryGetValue($"hunger:{S.Loop}:{S.Chapter}", out var hv) && hv >= 1 && !S.Flags.ContainsKey($"lthunger:{S.Loop}:{S.Chapter}:{(int)hv}"))
                found.Add(("hunger", ((int)hv).ToString(CultureInfo.InvariantCulture), Has("P03") ?? Has("P06") ?? By(d => S.A(d).Def.Hobbies.Contains("organize") ? 1 : 0)));
            // 5 buddy: the first morning after a death
            var lastDeath = LifeLastDeath();
            if (meal == "breakfast" && lastDeath != null && !S.Flags.ContainsKey("ltbuddy:" + lastDeath.Victim) && S.Flags.ContainsKey("ltdeath:" + lastDeath.Victim))
                found.Add(("buddy", lastDeath.Victim, Has("P03") ?? Has("P05") ?? diners[0]));
            // 6 conflict: a quarrel yesterday or today, both at the table
            foreach (var f in S.Flags.Keys.Where(k => k.StartsWith("lconf:", StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList())
            {
                var p = f.Split(':'); if (p.Length < 4 || !int.TryParse(p[1], out var d) || S.Day - d > 1) continue;
                if (diners.Contains(p[2]) && diners.Contains(p[3]) && !S.Flags.ContainsKey($"ltconf:{p[1]}:{p[2]}:{p[3]}")) { found.Add(("conflict", p[2] + ":" + p[3], p[3])); break; }
            }
            // 6b faction: a faction formed, grew or lost someone in the last day (Factions.cs) — its people or its outsiders talk
            foreach (var f in S.Factions.Where(f => f.Loop == S.Loop && S.Clock - f.Changed < 24 * 60 && !S.Flags.ContainsKey($"ltfac:{f.Id}:{(int)f.Changed}")).OrderBy(f => f.Id, StringComparer.Ordinal))
            {
                string o = diners.Contains(f.Leader) ? f.Leader : diners.Where(f.Members.Contains).OrderBy(d => d, StringComparer.Ordinal).FirstOrDefault()
                         ?? diners.Where(d => !f.Members.Contains(d)).OrderBy(d => f.Members.Average(m => Factions.Affinity(S, d, m))).ThenBy(d => d, StringComparer.Ordinal).FirstOrDefault();
                if (o != null && diners.Count >= 3) { found.Add(("faction", f.Id, o)); break; }
            }
            // 7 event: a festival today that hasn't started, its host at the table
            var fest = S.Gatherings.Where(g => g.Kind != null && g.Kind.StartsWith("fest:") && !g.Done && !g.Cancelled && g.Revs.Count > 0 && (int)(g.Cur.Start / 1440) + 1 == S.Day && g.Cur.Start > S.Clock).OrderBy(g => g.Cur.Start).FirstOrDefault();
            if (fest != null && diners.Contains(fest.Host) && !S.Flags.ContainsKey("ltevent:" + fest.Id)) found.Add(("event", fest.Id, fest.Host));
            // 8 absent: a living resident who didn't come (breakfast/dinner), 서윤 counts heads
            if (meal != "lunch")
            {
                // not at the table and not on the way to it (asleep, shut in, busy elsewhere)
                var din = S.Layout.First(RoomType.Dining);
                var absent = S.LivingNpcs.Where(x => !diners.Contains(x.Id) && (din == null || x.Room != din.Id) && (x.Act == null || x.Act.Id != "life:eat")).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
                if (absent.Count > 0 && absent.Count <= 6 && diners.Count >= 3)
                { var ab = absent[LR.R(absent.Count)]; found.Add(("absent", ab.Id, Has("P03") ?? By(d => S.R(d, ab.Id).Like))); }
            }
            // 9 rumour: known to two or more at the table
            var rum = RumoursAt(diners);
            if (rum != null) found.Add(("rumour", rum.Value.rid, rum.Value.carrier));
            // 10 habit: someone whose habit the table hasn't heard
            var hab = diners.Where(d => !S.Flags.ContainsKey($"lthabit:{S.Loop}:{d}") && LineBank.Pool(d, "tt_habit", CasualTo(d, null), out _) != null).OrderBy(d => d, StringComparer.Ordinal).ToList();
            if (hab.Count > 0) { var h = hab[LR.R(hab.Count)]; found.Add(("habit", h, h)); }
            // 11 food
            found.Add(("food", null, Has("P10") ?? By(d => S.A(d).Def.Hobbies.Contains("cook") ? 1 : 0)));
            // the strongest (1–6) wins outright; among the everyday ones (7–11) avoid last meal's kind
            bool Strong(string k) => Array.IndexOf(TopicOrder, k) < 6 || k == "envelope" || k == "push" || k == "hev";
            var strong = found.FirstOrDefault(f => Strong(f.kind));
            if (strong.kind != null && Strong(strong.kind)) return strong;
            // everyday topics: not last meal's kind, and the ones this loop hasn't heard yet first (the table keeps changing)
            string lastKind = S.Flags.TryGetValue("ltt:lastkind", out var lk) ? TopicOrder[Math.Max(0, Math.Min(TopicOrder.Length - 1, (int)lk))] : null;
            var everyday = found.Where(f => f.kind != lastKind).ToList(); if (everyday.Count == 0) everyday = found;
            var fresh = everyday.Where(f => !S.Flags.ContainsKey($"lttk:{S.Loop}:{f.kind}")).ToList();
            var pool = fresh.Count > 0 ? fresh : everyday;
            return pool.Count > 1 && LR.Chance(0.4) ? pool[1 + LR.R(pool.Count - 1)] : pool[0];
        }

        // ------------------------------------------------------------------ building the scene
        SceneRun TableBuild(List<string> diners, string meal, bool withPlayer)
        {
            if (diners.Count < 2) return null;
            var (kind, subject, owner) = PickTopic(diners, meal);
            if (owner == null || !diners.Contains(owner)) owner = diners[0];
            var sc = new GenScene { Id = $"gen:table:{S.Day}:{meal}", Kind = "table", Title = kind == "push" ? HousePush.NameOf(subject) ?? "저택의 공지" : kind == "pact" && S.Chapter > 1 ? "다시, 약속" : TopicTitle(kind), Npc = owner };
            var run = new SceneRun { Scene = sc, Kind = "table", Npc = owner, Room = S.A(owner)?.Room ?? -1 };
            run.Ctx["topic"] = kind; run.Ctx["owner"] = owner;
            run.Cast.AddRange(diners); if (withPlayer) run.Cast.Add(Cast.Player);
            string a = null, b = null;
            switch (kind)
            {
                case "death": run.Ctx["victim"] = subject; run.Ctx["t"] = subject; break;
                case "verdict": if (subject != null) { run.Ctx["victim"] = subject; run.Ctx["t"] = subject; } break;
                case "rule": case "envelope": { var ri = S.Ch.Rules.FirstOrDefault(r => r.Id == subject || r.Rule == subject); run.Ctx["rule"] = ri?.Name ?? "새 규칙"; break; }
                case "hunger": run.Ctx["n"] = KorCount(S.Survivors); break;
                case "buddy": run.Ctx["victim"] = subject; break;
                case "conflict": { var p = subject.Split(':'); a = p[0]; b = p[1]; run.Ctx["a"] = a; run.Ctx["b"] = b; run.Ctx["t"] = a; break; }
                case "event": { var g = S.Gatherings.FirstOrDefault(x => x.Id == subject); if (g != null) { run.Ctx["act"] = g.Label; run.Ctx["place"] = g.Cur.Room.ToString(CultureInfo.InvariantCulture); run.Ctx["time"] = g.Cur.Start.ToString(CultureInfo.InvariantCulture); run.Ctx["gid"] = g.Id; } break; }
                case "absent": run.Ctx["t"] = subject; break;
                case "rumour": { var r = RumourOf(owner, subject); if (r != null) { run.Ctx["rumour"] = RumourText(r.Value, owner); run.Ctx["t"] = r.Value.a; run.Ctx["rid"] = subject; } break; }
                case "habit": run.Ctx["t"] = owner; break;
                case "pact": run.Ctx["n"] = KorCount(S.Survivors); break;
                case "push": run.Ctx["push"] = subject; run.Ctx["rule"] = HousePush.NameOf(subject) ?? "새 규칙"; break;
                case "faction": { var f = S.Factions.FirstOrDefault(x => x.Id == subject); if (f != null) { run.Ctx["t"] = f.Leader; run.Ctx["group"] = f.Name; run.Ctx["n"] = KorCount(f.Members.Count); run.Ctx["fac"] = f.Id; } break; }
                case "hev": { var g = S.Gatherings.FirstOrDefault(x => x.Id == subject); if (g != null) { run.Ctx["act"] = g.Label; run.Ctx["gid"] = g.Id; run.Ctx["time"] = g.Cur.Start.ToString(CultureInfo.InvariantCulture); run.Ctx["place"] = g.Cur.Room.ToString(CultureInfo.InvariantCulture); } break; }
            }
            var beat = new LBeat { Id = "start" }; sc.Beats.Add(beat); run.Beat = beat;   // (the run starts here — without it the table said nothing)
            // the opener
            // the house's token: everyone speaks of their own (wish_sample); its other steps have their own pair of keys
            bool again = kind == "pact" && S.Chapter > 1; string openKey0 = null;
            var fac = kind == "faction" ? S.Factions.FirstOrDefault(x => x.Id == subject) : null;
            bool Inside(string who) => fac != null && fac.Members.Contains(who);
            if (kind == "hev") openKey0 = "tt_hev_go";
            string openKey = openKey0 != null ? openKey0 : kind == "conflict" ? "tt_conflict" : kind == "verdict" ? "tt_verdict" : kind == "envelope" ? "env_open" : kind == "push" ? (subject == "sample" ? "wish_sample" : "push_" + subject) : again ? "tt_pact_again" : kind == "faction" ? (Inside(owner) ? "tt_faction" : "tt_faction_out") : "tt_" + kind;
            string reKey = kind == "push" ? (subject == "sample" ? "wish_sample" : "push_" + subject + "_re") : again ? "tt_pact_again_re" : $"tt_{(kind == "envelope" ? "envelope" : kind)}_re";
            string openTo = kind == "conflict" ? a : null;
            beat.Lines.Add(KeyLine(owner, openTo, openKey, kind == "death" || kind == "verdict" ? Emotion.Sad : kind == "conflict" ? Emotion.Angry : Emotion.Neutral));
            // special voices for a death: 준서 counts the spoons, 은결 lays a lily on the chair
            if (kind == "death")
            {
                // the first death of a chapter breaks that chapter's pact (the table often sits after its 심판, in the next chapter):
                // the one who asked for it — or another who took it — says so
                int pc = S.Incidents.Values.Where(i => i.Victim == subject && i.Loop == S.Loop).Select(i => i.Chapter).DefaultIfEmpty(S.Chapter).First();
                if (S.Flags.ContainsKey($"ltpact:{S.Loop}:{pc}") && !S.Flags.ContainsKey($"ltpactbroken:{S.Loop}:{pc}"))
                {
                    S.Flags[$"ltpactbroken:{S.Loop}:{pc}"] = S.Clock;
                    // (준서 and 은결 have their own words at a death table — the spoons, the lily — so they speak for the pact last)
                    string keeper = new[] { "P03", "P05", "P12" }.FirstOrDefault(x => diners.Contains(x) && x != owner && Conscience.TookPact(S, x, pc))
                                 ?? diners.Where(d => d != owner && d != "P10" && d != "P14" && Conscience.TookPact(S, d, pc)).OrderBy(d => d, StringComparer.Ordinal).FirstOrDefault()
                                 ?? new[] { "P10", "P14" }.FirstOrDefault(x => diners.Contains(x) && x != owner && Conscience.TookPact(S, x, pc));
                    if (keeper != null) beat.Lines.Add(KeyLine(keeper, owner, "pact_broken", Emotion.Sad));
                }
                bool spoke(string who) => beat.Lines.Any(l => l != null && l.Who == who);
                if (diners.Contains("P10") && owner != "P10" && !spoke("P10")) beat.Lines.Add(KeyLine("P10", owner, "tt_spoons", Emotion.Sad));
                if (diners.Contains("P14") && owner != "P14" && !spoke("P14")) beat.Lines.Add(KeyLine("P14", owner, "tt_lily", Emotion.Sad));
            }
            // the other party of a quarrel answers first
            if (kind == "conflict" && diners.Contains(a)) beat.Lines.Add(KeyLine(a, owner, "argue_reply", Emotion.Angry));
            if (kind == "rumour" && run.Ctx.TryGetValue("t", out var subj) && diners.Contains(subj) && subj != owner) beat.Lines.Add(KeyLine(subj, owner, "rumour_deny", Emotion.Angry));
            // 2–3 reactors, those with a tie to the opener first (their pair lines land)
            var used = new HashSet<string>(beat.Lines.Select(l => l.Who));
            var reactors = diners.Where(d => !used.Contains(d)).OrderByDescending(d => (CastWeb.TieBetween(d, owner) != null ? 2 : 0) + LineBank.Variants(d, $"{reKey}@{owner}") + LR.F()).ThenBy(d => d, StringComparer.Ordinal).Take(kind == "food" || kind == "habit" ? 2 : kind == "pact" ? 5 : kind == "push" ? 4 : 3).ToList();
            var hg = kind == "hev" ? S.Gatherings.FirstOrDefault(x => x.Id == subject) : null;
            string HevKey(string who)
            {
                if (hg == null || !hg.Status.TryGetValue(who, out var st)) return "tt_hev_yes";
                if (st != "declined") return "tt_hev_yes";
                var why = HouseEvents.NoReason(S, who, hg.Id);
                return why == "fear" ? "tt_hev_no_fear" : why == "enemy" ? "tt_hev_no_enemy" : why == "lead" ? "tt_hev_no_lead" : why == "crowd" ? "tt_hev_no_crowd" : "tt_hev_no";
            }
            if (kind == "hev") reactors = diners.Where(d => d != owner).OrderByDescending(d => hg != null && hg.Status.TryGetValue(d, out var st) && st == "declined" ? 1 : 0).ThenBy(d => MurderHash.U01(S, "hevtab:" + subject + ":" + d)).ThenBy(d => d, StringComparer.Ordinal).Take(4).ToList();
            foreach (var r in reactors) beat.Lines.Add(KeyLine(r, owner, kind == "faction" ? (Inside(r) ? "tt_faction_in" : "tt_faction_out") : kind == "hev" ? HevKey(r) : reKey, kind == "push" ? Emotion.Fear : Emotion.Neutral));
            beat.Lines.RemoveAll(l => l == null);
            if (withPlayer) foreach (var o in TableOptions(kind, owner, a, b, diners, run)) beat.Opts.Add(o);
            // bookkeeping: this meal has had its topic
            S.Flags[$"ltt:{S.Day}:{meal}"] = S.Clock; S.Flags["ltt:lastkind"] = Array.IndexOf(TopicOrder, kind == "envelope" ? "rule" : kind);
            S.Flags[$"lttk:{S.Loop}:{kind}"] = S.Day; LFinc($"ltt:n:{S.Loop}");
            switch (kind)
            {
                case "death": S.Flags["ltdeath:" + subject] = S.Clock; break;
                case "verdict": { var s0 = S.Settlements.Where(s => s.Loop == S.Loop && s.Applied && !S.Flags.ContainsKey("ltver:" + s.Id)).LastOrDefault(); if (s0 != null) S.Flags["ltver:" + s0.Id] = S.Clock; break; }
                case "rule": case "envelope": S.Flags["ltrule:" + subject] = S.Clock; break;
                case "hunger": S.Flags[$"lthunger:{S.Loop}:{S.Chapter}:{subject}"] = S.Clock; break;
                case "buddy": S.Flags["ltbuddy:" + subject] = S.Clock; break;
                case "conflict": { var p = subject.Split(':'); foreach (var f in S.Flags.Keys.Where(k => k.EndsWith(":" + p[0] + ":" + p[1], StringComparison.Ordinal) && k.StartsWith("lconf:", StringComparison.Ordinal)).ToList()) { var q = f.Split(':'); S.Flags[$"ltconf:{q[1]}:{p[0]}:{p[1]}"] = S.Clock; } break; }
                case "event": S.Flags["ltevent:" + subject] = S.Clock; break;
                case "habit":
                    S.Flags[$"lthabit:{S.Loop}:{owner}"] = S.Clock;
                    Foreshadow.HabitShared(this, owner, "table:" + meal, diners.Concat(withPlayer ? new[] { Cast.Player } : new string[0]));
                    if (withPlayer) PK.Facts.Add("habit:" + owner);
                    break;
                case "push": S.Flags[$"ltpush:{S.Loop}:push:{subject}"] = S.Clock; break;
                case "faction": if (fac != null) S.Flags[$"ltfac:{fac.Id}:{(int)fac.Changed}"] = S.Clock; break;
                case "hev": S.Flags["lthev:" + subject] = S.Clock; break;
                case "pact":
                    S.Flags[$"ltpact:{S.Loop}:{S.Chapter}"] = S.Clock;
                    foreach (var d in diners.Concat(withPlayer ? new[] { Cast.Player } : new string[0])) S.Flags[$"pact:{S.Loop}:{S.Chapter}:{d}"] = S.Clock;
                    break;
                case "rumour": if (run.Ctx.TryGetValue("rid", out var rid)) { var rr = RumourOf(owner, rid); if (rr != null) foreach (var d in diners.Concat(withPlayer ? new[] { Cast.Player } : new string[0])) RumourLearn(d, rr.Value, owner); } break;
            }
            S.Log("TableTopic", owner, data: $"{meal}:{kind}:{subject}:{string.Join(",", diners)}:{(withPlayer ? "player" : "npc")}");
            return run;
        }

        /// <summary>A table line from a voice key; the shared ANY line when the speaker has none.</summary>
        LL KeyLine(string who, string to, string key, Emotion e)
        {
            if (who == null) return null;
            // resolved later per run (RunFrom → LifeU): the template is fetched now so the resolver's pair/about tiers apply
            return new LL { Who = who, Text = "\u0001" + key + (to != null ? "\u0002" + to : ""), Emo = e, Gest = e == Emotion.Angry ? Anim.Angry : Anim.Talk };
        }

        static string TopicTitle(string kind)
        {
            switch (kind)
            {
                case "death": return "빈자리"; case "verdict": return "심판 다음 날"; case "rule": return "새 규칙"; case "envelope": return "봉투";
                case "hunger": return "줄어든 식사"; case "buddy": return "짝"; case "conflict": return "어제의 말다툼"; case "event": return "오늘의 모임";
                case "absent": return "빈 의자"; case "rumour": return "소문"; case "habit": return "버릇"; case "pact": return "약속"; case "faction": return "무리"; case "hev": return "오늘 밤의 초대"; default: return "오늘의 식탁";
            }
        }

        /// <summary>민혁's one interjection at the table (±0.03–0.06 on the relevant people, DailyLife §3.2).</summary>
        List<LOpt> TableOptions(string kind, string owner, string a, string b, List<string> diners, SceneRun run)
        {
            var list = new List<LOpt>(); string O = owner;
            LOpt Opt(string label, string labelC = null) => new LOpt { Label = label, LabelC = labelC };
            LFx F(string from, string to, float like = 0, float trust = 0, float attach = 0, float grudge = 0, float respect = 0, string mem = null) => new LFx { From = from, To = to, Like = like, Trust = trust, Attach = attach, Grudge = grudge, Respect = respect, Memory = mem };
            string Name(string id) => CallName(Cast.Player, id);
            switch (kind)
            {
                case "death":
                    list.Add(Opt("{victim} 자리는 오늘 그대로 둬요.").Do(F(O, "me", attach: 0.04f, trust: 0.02f, mem: "빈자리를 그대로 두자고 했다")).Do(diners.Where(d => d != O).Select(d => F(d, "me", like: 0.01f)).ToArray()));
                    list.Add(Opt($"{Name(O)}, 괜찮아요?").Do(F(O, "me", trust: 0.04f, attach: 0.03f, mem: "식탁에서 먼저 내 안부를 물었다")));
                    list.Add(Opt("말없이 수저를 내려놓는다").Act().Do(F(O, "me", respect: 0.02f)));
                    break;
                case "verdict":
                    list.Add(Opt("오늘은 아무 말 안 해도 돼요.").Do(diners.Select(d => F(d, "me", like: 0.015f)).ToArray()));
                    list.Add(Opt($"{Name(O)} 탓이 아니에요.").Do(F(O, "me", trust: 0.05f, attach: 0.02f, mem: "내 탓이 아니라고 해 줬다")));
                    list.Add(Opt("말없이 빵을 반으로 가른다").Act().Do(F(diners.OrderByDescending(d => S.A(d).Needs.Grief).First(), "me", like: 0.03f)));
                    break;
                case "rule": case "envelope":
                    list.Add(Opt($"그건 {Name(O)}이(가) 말할 일이에요.").Do(F(O, "me", trust: 0.05f, mem: "규칙 앞에서 내 편을 들어 줬다")));
                    list.Add(Opt("규칙이 우리한테 뭘 원하는지부터 생각해요.").Do(diners.Select(d => F(d, "me", respect: 0.02f)).ToArray()));
                    list.Add(Opt("말없이 수저를 든다").Act());
                    break;
                case "hunger":
                    list.Add(Opt($"{Name(O)} 말대로 하나씩 나눠요.").Do(F(O, "me", trust: 0.04f, like: 0.02f)).Do(diners.Where(d => S.A(d).Def.P.Pride > 0.6f && d != O).Select(d => F(d, "me", like: -0.02f)).ToArray()));
                    if (diners.Contains("P10")) list.Add(Opt("준서 씨도 드셔야죠. 반은 준서 씨 거예요.").Do(F("P10", "me", like: 0.05f, attach: 0.03f, mem: "내 몫을 챙겨 줬다")).Mem("fed_cook"));
                    else list.Add(Opt("제 몫에서 반 떼 드릴게요.").Do(diners.Select(d => F(d, "me", like: 0.015f)).ToArray()));
                    list.Add(Opt("말없이 빵을 반으로 갈라 옆에 건넨다").Act().Do(F(diners.OrderByDescending(d => S.A(d).Needs.Hunger).ThenBy(d => d, StringComparer.Ordinal).First(), "me", like: 0.04f)));
                    break;
                case "buddy":
                    foreach (var x in diners.Where(d => d != O).OrderByDescending(d => S.R(Cast.Player, d).Like + S.R(d, Cast.Player).Like).ThenBy(d => d, StringComparer.Ordinal).Take(2))
                    { var xo = Opt($"저는 {Name(x)}하고 다닐게요.").Do(F(x, "me", like: 0.03f, trust: 0.04f, mem: "짝이 되자고 했다"), F(O, "me", respect: 0.02f)); xo.FactList = "flag:lwatch:" + x; list.Add(xo); }
                    list.Add(Opt("저는 혼자 다니는 게 편해요.").Do(F(O, "me", like: -0.02f, respect: -0.01f)));
                    break;
                case "conflict":
                    if (a != null && b != null)
                    {
                        list.Add(Opt($"{Name(b)} 말이 맞아요.").Do(F(b, "me", like: 0.05f, trust: 0.03f, mem: "식탁에서 내 편을 들어 줬다"), F(a, "me", like: -0.04f, grudge: 0.02f)));
                        list.Add(Opt($"{Name(a)} 얘기도 끝까지 들어 봐요.").Do(F(a, "me", like: 0.05f, trust: 0.03f, mem: "식탁에서 내 말을 들어 보자고 했다"), F(b, "me", like: -0.03f)));
                        var calm = Opt("밥 먹을 땐 그만해요. 둘 다요.").Do(F(a, "me", respect: 0.03f), F(b, "me", respect: 0.03f), F(a, b, grudge: -0.03f), F(b, a, grudge: -0.03f));
                        calm.TieState = $"{a}:{b}:table-truce"; list.Add(calm);
                    }
                    break;
                case "event":
                    list.Add(Opt("재밌겠네요. 갈게요.").Do(F(O, "me", like: 0.04f, mem: "모임에 오겠다고 했다")).Mem("event_yes"));
                    list.Add(Opt($"{Name(O)}, 뭐 준비할 거 있어요?").Do(F(O, "me", like: 0.05f, attach: 0.02f)));
                    list.Add(Opt("안 가도 되는 거죠?").Do(F(O, "me", like: O == "P17" ? -0.03f : -0.01f)));
                    break;
                case "absent":
                    list.Add(Opt("제가 한번 가 볼게요.").Do(F(O, "me", trust: 0.03f)).Mem("checked_absent"));
                    list.Add(Opt("무슨 일 있는 건 아니겠죠?").Do(F(O, "me", like: 0.01f)));
                    list.Add(Opt("자는 거겠죠. 식기 전에 먹어요.").Do(F(O, "me", respect: -0.02f)));
                    break;
                case "rumour":
                    list.Add(Opt("그거 누가 처음 한 말이에요?").Do(F(O, "me", respect: 0.02f)));
                    list.Add(Opt("직접 본 사람 있어요?").Do(diners.Select(d => F(d, "me", respect: 0.015f)).ToArray()));
                    { string subj = run.Ctx.TryGetValue("t", out var t0) ? t0 : null; var o3 = Opt("없는 사람 얘기는 그만해요.").Do(F(O, "me", like: -0.02f)); if (subj != null) o3.Do(F(subj, "me", trust: 0.04f, like: 0.02f, mem: "소문 앞에서 내 편을 들어 줬다")); list.Add(o3); }
                    break;
                case "push":
                    {
                        // 민혁 can shore the pact up at the table: the ones here who took it hold a little longer (Conscience.Mend)
                        string members = string.Join(",", diners.Where(d => Conscience.TookPact(S, d)).OrderBy(d => d, StringComparer.Ordinal));
                        string step = run.Ctx.TryGetValue("push", out var ps) ? ps : null;
                        if (step == "sample")
                        {
                            list.Add(Opt("저한텐 빈 봉투였어요. 드릴 견본이 없대요.").Do(F(O, "me", trust: 0.02f, mem: "빈 봉투 얘기를 해 줬다")).Do(diners.Where(d => d != O).Select(d => F(d, "me", like: 0.01f)).ToArray()));
                            var keep = Opt("견본은 견본이에요. 첫날 약속, 잊지 마요.").Do(diners.Where(d => Conscience.TookPact(S, d)).Select(d => F(d, "me", respect: 0.02f)).ToArray());
                            if (members.Length > 0) keep.Know("mend:" + members); list.Add(keep);
                            list.Add(Opt("말없이 빈 봉투를 주머니에 넣는다").Act());
                        }
                        else
                        {
                            var keep = Opt("누구를 위해서든, 사람 목숨으로 이루는 소원은 아니에요.").Do(diners.Select(d => F(d, "me", respect: 0.02f)).ToArray());
                            if (members.Length > 0) keep.Know("mend:" + members); list.Add(keep);
                            list.Add(Opt($"{Name(O)}, 그 말 듣고 누가 떠올랐어요?").Do(F(O, "me", respect: 0.02f, trust: 0.01f)));
                            list.Add(Opt("말없이 잔을 내려놓는다").Act());
                        }
                        break;
                    }
                case "hev":
                    {
                        var g = run.Ctx.TryGetValue("gid", out var gid) ? S.Gatherings.FirstOrDefault(x => x.Id == gid) : null; if (g == null) break;
                        var no = diners.Where(d => g.Status.TryGetValue(d, out var st) && st == "declined").OrderBy(d => d, StringComparer.Ordinal).ToList();
                        list.Add(Opt("저도 갈게요. 같이 가요.").Do(diners.Where(d => !no.Contains(d)).Select(d => F(d, "me", like: 0.02f)).ToArray()));
                        if (no.Count > 0) { var bring = Opt($"{Name(no[0])}, 같이 가요. 제가 옆에 있을게요.").Do(F(no[0], "me", trust: 0.04f, attach: 0.02f, mem: "저택 행사에 같이 가자고 했다")); if (S.R(no[0], Cast.Player).Trust + S.R(no[0], Cast.Player).Like >= 0.25f) bring.Know($"hevjoin:{g.Id}:{no[0]}"); list.Add(bring); }
                        list.Add(Opt("오늘 밤은 방에 있을래요.").Do(no.Select(d => F(d, "me", like: 0.01f)).ToArray()));
                        break;
                    }
                case "faction":
                    {
                        var f = run.Ctx.TryGetValue("fac", out var fid) ? S.Factions.FirstOrDefault(x => x.Id == fid) : null; if (f == null) break;
                        var here = diners.Where(f.Members.Contains).ToList(); var outs = diners.Where(d => !f.Members.Contains(d)).ToList();
                        var join = Opt("저도 끼워 줄래요?").Do(here.Select(d => F(d, "me", like: 0.03f, trust: 0.01f)).ToArray()).Do(outs.Select(d => F(d, "me", like: -0.01f)).ToArray());
                        join.Know("flag:facin:" + f.Id); list.Add(join);
                        list.Add(Opt("무리 짓는 건 좀 위험하지 않아요?").Do(outs.Select(d => F(d, "me", respect: 0.02f)).ToArray()).Do(here.Select(d => F(d, "me", like: -0.01f)).ToArray()));
                        list.Add(Opt("말없이 듣는다").Act());
                        break;
                    }
                case "pact":
                    list.Add(Opt("약속해요. 저부터요.").Do(F(O, "me", trust: 0.04f, attach: 0.02f, mem: "약속에 제일 먼저 손을 들었다")).Do(diners.Where(d => d != O).Select(d => F(d, "me", like: 0.01f, trust: 0.01f)).ToArray()));
                    list.Add(Opt("약속만으로는 모자라요. 밤에는 혼자 다니지 마요.").Do(diners.Select(d => F(d, "me", respect: 0.02f)).ToArray()));
                    list.Add(Opt("말없이 잔을 든다").Act().Do(F(O, "me", like: 0.02f)));
                    break;
                case "habit":
                    list.Add(Opt("그거 전혀 몰랐어요.").Do(F(O, "me", like: 0.02f)));
                    list.Add(Opt("저도 비슷한 버릇 있어요.").Do(F(O, "me", like: 0.03f, attach: 0.01f)).Mem("same_habit"));
                    list.Add(Opt("말없이 고개를 끄덕인다").Act());
                    break;
                default:
                    if (O == "P10") { list.Add(Opt("오늘 국 진짜 맛있어요.").Do(F("P10", "me", like: 0.04f))); list.Add(Opt("준서 씨도 앉아서 같이 드세요.").Do(F("P10", "me", like: 0.03f, attach: 0.03f, mem: "같이 앉아서 먹자고 했다")).Mem("sit_with_cook")); }
                    else { list.Add(Opt("오늘 음식 맛있네요.").Do(diners.Select(d => F(d, "me", like: 0.01f)).ToArray())); list.Add(Opt($"{Name(O)}, 이거 좀 드셔 보세요.").Do(F(O, "me", like: 0.03f))); }
                    list.Add(Opt("말없이 한 숟가락 더 뜬다").Act());
                    break;
            }
            return list;
        }

        void TableNpcOnly(SceneRun run)
        {
            var diners = run.Cast.Where(x => x != Cast.Player).ToList();
            foreach (var p in diners) foreach (var q in diners) if (p != q) Relations.Change(S, p, q, like: 0.004f);
            if (run.Ctx.TryGetValue("topic", out var kind))
            {
                if (kind == "conflict" && run.Ctx.TryGetValue("a", out var a) && run.Ctx.TryGetValue("b", out var b)) { Relations.Change(S, a, b, grudge: 0.02f); Relations.Change(S, b, a, grudge: 0.02f); }
                if (kind == "death") foreach (var p in diners) foreach (var q in diners) if (p != q) Relations.Change(S, p, q, attach: 0.01f);
                if (kind == "pact") foreach (var p in diners) foreach (var q in diners) if (p != q) Relations.Change(S, p, q, trust: 0.01f);
                if (kind == "buddy") for (int i = 0; i + 1 < diners.Count; i += 2) { S.R(diners[i], diners[i + 1]).Tags.Add("watch"); S.R(diners[i + 1], diners[i]).Tags.Add("watch"); }
            }
        }

        /// <summary>18 → "열여덟" (seat counts, heads).</summary>
        internal static string KorCount(int n)
        {
            string[] ones = { "", "하나", "둘", "셋", "넷", "다섯", "여섯", "일곱", "여덟", "아홉" };
            if (n <= 0) return "아무도"; if (n < 10) return ones[n]; if (n == 10) return "열"; if (n < 20) return "열" + ones[n - 10]; if (n == 20) return "스물";
            return n.ToString(CultureInfo.InvariantCulture);
        }
    }
}
