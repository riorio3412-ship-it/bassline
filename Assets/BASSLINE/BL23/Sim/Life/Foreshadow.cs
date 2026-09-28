using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// F12 — foreshadowing hooks (DailyLifeDesign §7). The API the murder side calls; daily life renders it. Nothing here
    /// decides a murder, and nothing here becomes an evidence card by itself (case recall does that).
    ///
    ///   HabitShared(sim, subject, where, hearers)  — a habit was said aloud (table, quiz, hangout): who heard it. §7.4
    ///   Knows(S, who, subject[, habit])            — may this planner use the victim's personal-item habit? (the Knows(habit) gate)
    ///   WhoKnows(S, subject[, habit])              — for the one investigation card "who heard about the thermos"
    ///   Cover(sim, actor, kind, item, room)        — an innocent-looking preparation step, in character (cover_&lt;kind&gt; bark
    ///                                                if anyone is near); watchers with the matching lens note an oddity, and
    ///                                                it may become talk. Kinds: knife cord thread sedative poison saw tea plant
    ///                                                trap recorder note fire noise swap key burn bury swim gathering alibi. §7.2
    ///   Recon(sim, asker, listener, target, habit) — "{t}는 보통 몇 시에 자요?" (recon_ask); the listener remembers who asked. §7.3
    ///   Tick(sim)                                  — reads new ledger "Prep" events (Data "kind|item-or-room") and calls Cover.
    /// State: Knowledge.Facts "habit-heard:&lt;subject&gt;[:habit]", "odd:&lt;actor&gt;:&lt;kind&gt;:&lt;day&gt;", "asked-about:&lt;target&gt;:&lt;habit&gt;:&lt;asker&gt;:&lt;day&gt;";
    /// ledger "HabitShared", "Cover", "Recon". Deterministic (S.R(Stream.Dialogue) only).
    /// </summary>
    public static class Foreshadow
    {
        /// <summary>Watchers per preparation kind: who notices this sort of thing (bible §6 witness lenses, DailyLife §7.2).</summary>
        public static readonly Dictionary<string, string[]> Watchers = new Dictionary<string, string[]>
        {
            ["knife"] = new[] { "P10", "P06" }, ["blunt"] = new[] { "P06", "P11" }, ["cord"] = new[] { "P06", "P18" }, ["thread"] = new[] { "P16" },
            ["sedative"] = new[] { "P12", "P14" }, ["poison"] = new[] { "P10", "P14" }, ["saw"] = new[] { "P10" }, ["tea"] = new[] { "P04", "P10" },
            ["plant"] = new[] { "P14", "P15" }, ["trap"] = new[] { "P11", "P08" }, ["recorder"] = new[] { "P08", "P09" }, ["note"] = new[] { "P12" },
            ["fire"] = new[] { "P03", "P18" }, ["tod"] = new[] { "P03", "P18" }, ["noise"] = new[] { "P08" }, ["swap"] = new[] { "P04" }, ["key"] = new[] { "P15", "P11" },
            ["burn"] = new[] { "P18", "P08" }, ["bury"] = new[] { "P10" }, ["swim"] = new[] { "P05", "P13" }, ["gathering"] = new[] { "P17", "P03" }, ["alibi"] = new string[0],
        };

        // ------------------------------------------------------------------ habit knowledge (§7.4)
        public static void HabitShared(Simulation sim, string subject, string where, IEnumerable<string> hearers, string habit = null)
        {
            var S = sim.S; if (subject == null || hearers == null) return;
            var list = hearers.Where(h => h != null && h != subject).Distinct().OrderBy(h => h, StringComparer.Ordinal).ToList();
            foreach (var h in list) { var k = S.K(h); k.Facts.Add("habit-heard:" + subject); if (habit != null) k.Facts.Add("habit-heard:" + subject + ":" + habit); }
            if (list.Count > 0) S.Log("HabitShared", subject, data: (habit ?? "habit") + "|" + (where ?? "") + "|" + string.Join(",", list));
        }

        public static bool Knows(GameState S, string who, string subject, string habit = null)
        {
            if (who == null || subject == null) return false; if (who == subject) return true;
            var f = S.K(who).Facts; return habit == null ? f.Contains("habit-heard:" + subject) : f.Contains("habit-heard:" + subject + ":" + habit) || f.Contains("habit-heard:" + subject);
        }

        public static List<string> WhoKnows(GameState S, string subject, string habit = null)
            => S.Actors.Values.Where(a => !a.IsButler && a.Id != subject && Knows(S, a.Id, subject, habit)).Select(a => a.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

        // ------------------------------------------------------------------ covers (§7.2)
        public static void Cover(Simulation sim, Actor actor, string kind, string item = null, int room = -1)
        {
            var S = sim.S; if (actor == null || !actor.Alive || kind == null) return;
            if (room < 0) room = actor.Room;
            string day = S.Day.ToString(CultureInfo.InvariantCulture);
            S.Log("Cover", actor.Id, room: room, data: kind + "|" + (item ?? ""), secret: true);
            var near = S.Living.Where(x => x.Id != actor.Id && x.Room == room && x.Pose != Pose.Sleep && x.Pos.f == actor.Pos.f && x.Pos.Dist(actor.Pos) < 10).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
            if (near.Count > 0)
            {
                var slots = new Dictionary<string, string> { { "item", item != null ? (ItemCatalog.Get(item)?.Kor ?? item) : "이거" }, { "place", S.RoomName(room) } };
                string key = "cover_" + kind;
                if (LineBank.Has(actor.Id, key) || LineBank.Has(LineBank.Shared, key)) sim.Speak(actor, key, near[0].Id, slots);
            }
            if (!Watchers.TryGetValue(kind, out var lens)) return;
            foreach (var w in near.Where(x => lens.Contains(x.Id)))
            {
                S.K(w.Id).Facts.Add($"odd:{actor.Id}:{kind}:{day}");
                // what the watcher saw may become talk (never an accusation by itself)
                if (sim.S.R(Stream.Dialogue).Chance(0.35)) sim.SeedRumourPublic(kind == "knife" || kind == "cord" || kind == "blunt" || kind == "saw" ? "tool" : kind == "tod" || kind == "fire" || kind == "noise" ? "night" : "room", actor.Id, null, w.Id, room, item);
            }
        }

        // ------------------------------------------------------------------ recon questions (§7.3)
        public static void Recon(Simulation sim, Actor asker, Actor listener, string target, string habit = null)
        {
            var S = sim.S; if (asker == null || listener == null || target == null) return;
            var slots = new Dictionary<string, string> { { "t", "@" + target }, { "place", S.RoomName(asker.Room) } };
            sim.Speak(asker, "recon_ask", listener.Id, slots);
            string fact = $"asked-about:{target}:{habit ?? "habit"}:{asker.Id}:{S.Day}";
            S.K(listener.Id).Facts.Add(fact);
            var me = S.Player;
            if (me != null && me.Room == asker.Room && me.Pose != Pose.Sleep && me.Pos.Dist(asker.Pos) < 10) S.K(Cast.Player).Facts.Add(fact);
            S.Log("Recon", asker.Id, target, room: asker.Room, data: (habit ?? "habit") + "|" + listener.Id, secret: true);
        }

        // ------------------------------------------------------------------ talk (the rumour mill, F11)
        /// <summary>Start a rumour that then spreads through ordinary conversations (a schemer's smear, a watcher's worry).
        /// kind: argued flirt night tool room pry vote frames key secret; subject = who it is about; other = the second person
        /// (argued/flirt/room) or null; teller = its first knower. Returns the rumour id, or null (a duplicate today / not allowed).</summary>
        public static string Rumour(Simulation sim, string kind, string subject, string other, string teller, int room = -1, string itemType = null)
            => sim?.SeedRumourPublic(kind, subject, other, teller, room, itemType);

        // ------------------------------------------------------------------ the murder side's preparation steps
        /// <summary>Reads ledger "Prep" events (Actor = the one preparing, Data = "kind|item-or-room") and renders their covers.</summary>
        public static void Tick(Simulation sim)
        {
            var S = sim.S;
            double seq = S.Flags.TryGetValue("lprepseq", out var v) ? v : 0;
            if (S.Ledger.Count == 0 || S.Ledger[S.Ledger.Count - 1].Seq <= seq) return;
            foreach (var e in sim.LedgerSince(seq).Where(e => e.Type == "Prep").ToList())
            {
                var p = (e.Data ?? "").Split('|');
                Cover(sim, S.A(e.Actor), p.Length > 0 ? p[0] : null, p.Length > 1 && p[1].Length > 0 ? p[1] : e.Item, e.Room);
            }
            S.Flags["lprepseq"] = S.Ledger[S.Ledger.Count - 1].Seq;
        }
    }

    public sealed partial class Simulation
    {
        /// <summary>Foreshadow's door to the rumour mill.</summary>
        internal string SeedRumourPublic(string kind, string a, string b, string origin, int room, string item)
            => SeedRumour(kind, a, b, origin, room, false, item != null ? "item=" + (ItemCatalog.Get(item)?.Kor ?? item) : null);
    }
}
