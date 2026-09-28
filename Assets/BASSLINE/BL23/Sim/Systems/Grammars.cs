using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    [Serializable]
    public sealed class Loan
    {
        public string Id, Item, Owner, Borrower, State = "lent";   // lent → returned | misplaced → noticed → resolved
        public double At, Due; public int LeftRoom = -1; public double LeftAt = -1; public bool Coat; public string Suspect; public bool Complained;
    }

    [Serializable] public sealed class GatheringRev { public int Room; public double Start, End, At; public string Why; }

    [Serializable]
    public sealed class Gathering
    {
        public string Id, Host, Kind, Label; public int Rev; public bool Cancelled, Done;
        public List<GatheringRev> Revs = new List<GatheringRev>();
        public Dictionary<string, string> Status = new Dictionary<string, string>();   // unaware, invited, accepted, declined, attended, late, left, stoodup
        public Dictionary<string, int> KnownRev = new Dictionary<string, int>();        // -1 = not told
        public Dictionary<string, string> Channel = new Dictionary<string, string>();   // "note" / "voice"
        public Dictionary<string, double> Arrived = new Dictionary<string, double>(), Left = new Dictionary<string, double>();
        public GatheringRev Cur => Revs[Rev];
    }

    [Serializable]
    public sealed class Trap
    {
        public string Id, Kind, Owner, Plan, Target, Victim, DisarmedBy; public int Furniture = -1, Spot = -1, Stair = -1, Room = -1, TopFloor;
        public double ArmedAt, FiredAt = -1, DisarmedAt = -1; public bool Active, Found;
    }

    [Serializable] public sealed class DeviceRecord { public int Door; public int Room; public string Actor; public double Clock; public bool Entering; }

    [Serializable]
    public sealed class Repair
    {
        public string Id, Worker, Fault; public int Furniture = -1, Circuit = -1; public int Stage, Stages = 3; public bool Done, Abandoned, ClaimedDone;
        public double Since, NextWork; public List<string> Revs = new List<string>();
    }

    /// <summary>
    /// Everyday grammars from the original IG01–12 set, as real systems NPCs live inside:
    /// lending/returns (IG01, IG06 coat), invitations with revisions and gatherings (IG02 + "모임 안의 사건"),
    /// door loggers with partial coverage (IG04), late privacy locks (IG05), unfinished repairs (IG07),
    /// drifting clocks (IG09), couriers who later stay silent (IG10). Tricks (traps, recorders, powers) live in Tricks.cs.
    /// Every misunderstanding here comes from actual actions and partial observation — nothing is scripted.
    /// </summary>
    public static class Grammars
    {
        // ------------------------------------------------------------------ loop setup
        public static void InitLoop(Simulation sim)
        {
            var S = sim.S; var L = S.Layout; var rng = S.R(Stream.Layout);
            S.Loans.Clear(); S.Gatherings.Clear(); S.Traps.Clear(); S.DeviceLog.Clear(); S.Repairs.Clear(); S.ClockOffset.Clear(); S.DoorLoggers.Clear(); S.Deliveries.Clear();
            // IG09: some clocks drift (the official time is the butler's announcements and the hall clock)
            var hall = L.Rooms.FirstOrDefault(r => r.Type == RoomType.GrandHall && r.Floor == 0);
            foreach (var f in L.Furniture.Where(f => f.Type == "Clock"))
            {
                if (hall != null && f.Room == hall.Id) { S.ClockOffset[f.Id] = 0; continue; }
                S.ClockOffset[f.Id] = rng.Chance(0.45) ? 0 : (rng.Chance(0.5) ? 1 : -1) * rng.Range(6, 24);
            }
            // IG04: a few rooms with two or more entrances get a doorway logger covering only one of them
            var cands = L.Rooms.Where(r => r.Doors.Count >= 2 && !RoomInfo.IsPassage(r.Type) && r.Type != RoomType.Bedroom && r.Type != RoomType.Elevator && r.Type != RoomType.Courtroom && r.Type != RoomType.ButlerRoom).ToList();
            rng.Shuffle(cands);
            foreach (var r in cands.OrderByDescending(r => RoomInfo.IsMystery(r.Type) || r.Type == RoomType.Library || r.Type == RoomType.Gallery || r.Type == RoomType.Study ? 1 : 0).Take(3))
            {
                int door = r.Doors[rng.R(r.Doors.Count)]; var d = L.Doors[door];
                // the logger sits on the room side of the door frame
                var inward = d.AlongX ? new P3(d.Pos.f, d.Pos.x + 0.9f, d.Pos.z + (r.Rect.CZ > d.Pos.z ? 0.35f : -0.35f)) : new P3(d.Pos.f, d.Pos.x + (r.Rect.CX > d.Pos.x ? 0.35f : -0.35f), d.Pos.z + 0.9f);
                var f = new Furniture { Id = L.Furniture.Count, Room = r.Id, Type = "DoorLogger", Pos = inward, Yaw = 0, W = 0.3f, D = 0.15f, H = 0.5f, Blocks = false, Material = Mat.Metal };
                L.Furniture.Add(f); r.Furniture.Add(f.Id);
                S.DoorLoggers[door] = f.Id;
            }
            // personal belongings so lending has real objects (owner-tagged, kept in their room)
            string[] lendable = { "Book", "Camera", "Recorder", "Flashlight", "Notebook", "Thermos", "Raincoat", "Scarf" };
            foreach (var a in S.LivingNpcs)
            {
                var bed = L.BedroomOf(a.Id); if (bed == null) continue;
                string type = PersonalItemFor(a, rng, lendable);
                var it = new Item { Id = S.NewId("it"), Type = type, Name = $"{Cast.GivenOf(a.Id)}의 {ItemCatalog.Get(type)?.Kor ?? type}", Owner = a.Id, Pos = sim.RandomPointIn(bed, rng), Room = bed.Id, HomeRoom = bed.Id };
                it.HomePos = it.Pos; S.Items[it.Id] = it;
            }
        }

        static string PersonalItemFor(Actor a, Rng rng, string[] pool)
        {
            var h = a.Def.Hobbies;
            if (h.Contains("music") || h.Contains("perform") || h.Contains("speech")) return "Recorder";
            if (h.Contains("film") || h.Contains("exhibit") || h.Contains("style")) return "Camera";
            if (h.Contains("read") || h.Contains("puzzle")) return "Book";
            if (h.Contains("investigate") || h.Contains("trade") || h.Contains("organize") || h.Contains("restore")) return "Notebook";
            if (h.Contains("tea")) return "Thermos";
            if (h.Contains("inspect") || h.Contains("repair")) return "Flashlight";
            return pool[rng.R(pool.Length)];
        }

        public static bool HasWatch(string id) => id == Cast.Player || id == Cast.Butler || (id != null && id.Length > 1 && int.TryParse(id.Substring(1), out var hn) ? (hn * 7 + 3) % 5 : 4) < 2;   // deterministic (string hashes differ per process)

        /// <summary>IG09: the time a watchless witness would state for something seen in a room (reads that room's clock).</summary>
        public static double PerceivedTime(GameState S, string witness, int room, double t, out bool byClock)
        {
            byClock = false;
            if (HasWatch(witness)) return t;
            var r = S.Layout.Room(room); if (r == null) return t;
            foreach (var fid in r.Furniture) if (S.ClockOffset.TryGetValue(fid, out var off)) { byClock = off != 0; return t + off; }
            return t;
        }

        // ------------------------------------------------------------------ per tick
        public static void Tick(Simulation sim)
        {
            var S = sim.S;
            Tricks.Tick(sim);
            if (S.Tick % 10 != 0) return;
            foreach (var g in S.Gatherings.Where(x => !x.Done && !x.Cancelled).ToList()) GatheringTick(sim, g);
        }

        /// <summary>Clock-minute schedule hook (called once per in-game hour and at a few fixed times).</summary>
        public static void Hourly(Simulation sim, int mod)
        {
            var S = sim.S; if (S.Phase != Phase.Daily) return;
            var rng = S.R(Stream.Life);
            if (mod >= 9 * 60 && mod <= 17 * 60 && S.Gatherings.Count(g => !g.Done && !g.Cancelled) == 0 && S.Gatherings.Count(g => (int)(g.Revs[0].At / 1440) == S.Day) < 2 && rng.Chance(0.4)) Host(sim, rng);
            LoansHourly(sim, rng);
            if (mod == 9 * 60) ButlerClockRound(sim, rng);
            if (rng.Chance(0.12) && S.Repairs.Count(r => !r.Done && !r.Abandoned) == 0) NewFault(sim, rng);
            PrivacyLocks(sim, rng);
        }

        // ================================================================== IG01 / IG06 lending
        public static void TopicOptions(Simulation sim, Actor a, Actor b, List<(string t, double w, string third)> opts)
        {
            var S = sim.S; var r = S.R(a.Id, b.Id); var rb = S.R(b.Id, a.Id);
            if (b.IsPlayer) return;
            S_IsNight = S.IsNight;
            // borrow something b owns (a real object in b's room or hands)
            if (r.Opinion > -0.05 && !S.Loans.Any(l => l.Borrower == a.Id && l.State == "lent"))
            {
                var it = S.Items.Values.FirstOrDefault(i => i.Owner == b.Id && i.Holder == null && !S.Loans.Any(l => l.Item == i.Id && l.State == "lent") && Wants(a, i));
                if (it != null) opts.Add(("g_borrow", 0.7 + a.Def.P.Sociability * 0.4, it.Id));
            }
            // IG06: lend a coat to someone wet or cold at night
            bool cold = b.Wet || S.IsNight || S.Layout.Room(b.Room)?.Type == RoomType.RainCorridor;
            if (cold && r.Like > 0.25f && a.BorrowedOutfitOf == null && b.BorrowedOutfitOf == null && !S.Loans.Any(l => l.Coat && (l.Owner == a.Id || l.Borrower == b.Id) && l.State == "lent"))
                opts.Add(("g_coat", 0.35 + a.Def.Empathy / 200.0, null));
            // gatherings: tell an invitee in person / explain a mix-up
            foreach (var g in S.Gatherings.Where(x => !x.Done && !x.Cancelled && x.Host == a.Id))
                if (g.Status.TryGetValue(b.Id, out var st) && (st == "unaware" || (g.KnownRev.TryGetValue(b.Id, out var kr) && kr >= 0 && kr < g.Rev))) opts.Add(("g_invite", 3.0, g.Id));
            var mix = S.Gatherings.FirstOrDefault(x => x.Done && x.Host == a.Id && x.Status.TryGetValue(b.Id, out var st2) && st2 == "stoodup" && !S.Flags.ContainsKey($"gexplained:{x.Id}:{b.Id}"));
            if (mix != null) opts.Add(("g_explain", 1.6, mix.Id));
            var mix2 = S.Gatherings.FirstOrDefault(x => x.Done && x.Host == b.Id && x.Status.TryGetValue(a.Id, out var st3) && st3 == "stoodup" && !S.Flags.ContainsKey($"gexplained:{x.Id}:{a.Id}"));
            if (mix2 != null) opts.Add(("g_explain", 1.2, mix2.Id));
            // IG01: complain about something missing
            var loss = S.Loans.FirstOrDefault(l => l.Owner == a.Id && l.State == "noticed" && !l.Complained);
            if (loss != null) opts.Add(("g_missing", 1.4, loss.Id));
            // returning a borrowed thing in person
            var mine = S.Loans.FirstOrDefault(l => l.Borrower == a.Id && l.Owner == b.Id && l.State == "lent" && !l.Coat && S.I(l.Item)?.Holder == a.Id);
            if (mine != null) opts.Add(("g_return", 2.5, mine.Id));
            var coat = S.Loans.FirstOrDefault(l => l.Coat && l.Borrower == a.Id && l.Owner == b.Id && l.State == "lent" && S.Clock > l.Due);
            if (coat != null) opts.Add(("g_return", 2.0, coat.Id));
        }

        static bool S_IsNight;
        static bool Wants(Actor a, Item it)
        {
            var h = a.Def.Hobbies;
            switch (it.Type)
            {
                case "Book": return h.Contains("read") || h.Contains("puzzle") || h.Contains("investigate") || a.Def.P.Curiosity > 0.75f;
                case "Camera": return h.Contains("film") || h.Contains("exhibit") || h.Contains("observe") || a.Def.P.Curiosity > 0.7f;
                case "Recorder": return h.Contains("music") || h.Contains("perform") || h.Contains("speech");
                case "Flashlight": return a.Def.P.Fearfulness > 0.5f || h.Contains("inspect") || S_IsNight;
                case "Notebook": return h.Contains("investigate") || h.Contains("organize") || h.Contains("trade") || h.Contains("craft");
                case "Thermos": return h.Contains("tea") || h.Contains("walk") || a.Def.Likes.Any(l => l.Contains("차") || l.Contains("커피"));
                case "Raincoat": case "Scarf": return h.Contains("walk") || h.Contains("swim") || a.Def.P.Fearfulness > 0.5f || a.Needs.Stress > 0.5f;
            }
            return false;
        }

        public static void ConvoLine(Simulation sim, string topic, string third, Actor sp, Actor li, bool opener)
        {
            var S = sim.S; var slots = new Dictionary<string, string>(); string key = "small_talk"; Prop prop = null;
            switch (topic)
            {
                case "g_borrow": { var it = S.I(third); slots["item"] = it?.Def?.Kor ?? "그거"; key = opener ? "borrow_ask" : (S.R(li.Id, sp.Id).Like > 0.1f ? "borrow_yes" : "borrow_no"); break; }
                case "g_coat": key = opener ? "coat_offer" : "coat_thanks"; break;
                case "g_invite":
                    {
                        var g = S.Gatherings.FirstOrDefault(x => x.Id == third); if (g == null) break;
                        slots["act"] = g.Label; slots["place"] = S.RoomName(g.Cur.Room); slots["time"] = ClockFmt.Vague(g.Cur.Start);
                        bool change = g.KnownRev.TryGetValue(li.Id, out var kr) && kr >= 0 && kr < g.Rev;
                        key = opener ? (change ? "gathering_change" : "gathering_invite") : (WouldAccept(sim, li, g) ? "gathering_yes" : "gathering_no");
                        if (opener) prop = new Prop { Kind = PropKind.Invited, A = li.Id, B = g.Host, Room = g.Cur.Room, T0 = g.Cur.Start, T1 = g.Cur.End, Value = "rev" + g.Rev };
                        break;
                    }
                case "g_explain": key = opener ? "gathering_explain" : "gathering_explain_reply"; break;
                case "g_missing":
                    {
                        var l = S.Loans.FirstOrDefault(x => x.Id == third); var it = S.I(l?.Item); slots["item"] = it?.Def?.Kor ?? "물건";
                        if (opener) { key = l?.Suspect != null ? "item_missing_suspect" : "item_missing"; if (l?.Suspect != null) slots["t"] = "@" + l.Suspect; prop = new Prop { Kind = PropKind.ItemMissing, A = sp.Id, Item = it?.Type, Room = it?.HomeRoom ?? -1, T0 = l?.At ?? S.Clock, T1 = S.Clock, Value = l?.Suspect }; }
                        else key = "small_talk";
                        break;
                    }
                case "g_return": { var l = S.Loans.FirstOrDefault(x => x.Id == third); slots["item"] = l != null && l.Coat ? "외투" : S.I(l?.Item)?.Def?.Kor ?? "이거"; key = opener ? "return_item" : "return_thanks"; break; }
            }
            sim.Speak(sp, key, li.Id, slots, prop);
        }

        public static void ConvoOutcome(Simulation sim, string topic, string third, Actor a, Actor b)
        {
            var S = sim.S; var rng = S.R(Stream.Life);
            switch (topic)
            {
                case "g_borrow":
                    {
                        var it = S.I(third); if (it == null || S.R(b.Id, a.Id).Opinion < -0.02) { Relations.Change(S, a.Id, b.Id, like: -0.01f); break; }
                        // the owner fetches it and hands it over — a real walk, a real handover others can see
                        var act = new Activity { Id = "g:lend:" + it.Id, Label = "물건 빌려주기", Priority = 2.2 };
                        act.Steps.Add(Simulation.GoTo(it.Pos)); act.Steps.Add(new ActionStep { Kind = "X_TakeOwn", Item = it.Id });
                        act.Steps.Add(new ActionStep { Kind = "Talk", Actor = a.Id }); act.Steps.Add(new ActionStep { Kind = "X_Hand", Item = it.Id, Actor = a.Id, Tag = "lend" });
                        sim.Assign(b, act);
                        Relations.Change(S, a.Id, b.Id, like: 0.03f, trust: 0.02f, memory: "물건을 빌려주기로 했다");
                        break;
                    }
                case "g_coat":
                    {
                        // a (owner) lends the outer coat to b
                        b.BorrowedOutfitOf = a.Id; var l = new Loan { Id = S.NewId("loan"), Owner = a.Id, Borrower = b.Id, Coat = true, At = S.Clock, Due = S.Clock + rng.Range(60, 180) };
                        S.Loans.Add(l); S.Log("LendCoat", a.Id, b.Id, room: a.Room, pos: a.Pos, data: l.Id);
                        S.Emit(GameEventType.Disguise, b.Id, a.Id, data: "coat:" + a.Id);
                        foreach (var w in S.Living.Where(x => x != a && x != b && x.Room == a.Room && x.Pose != Pose.Sleep)) S.K(w.Id).Facts.Add($"coat:{b.Id}:{a.Id}:{(int)S.Clock}");
                        Relations.Change(S, b.Id, a.Id, like: 0.06f, trust: 0.04f, memory: "외투를 빌려줬다");
                        break;
                    }
                case "g_invite":
                    {
                        var g = S.Gatherings.FirstOrDefault(x => x.Id == third); if (g == null) break;
                        Tell(sim, g, b, "voice");
                        break;
                    }
                case "g_explain":
                    {
                        var g = S.Gatherings.FirstOrDefault(x => x.Id == third); if (g == null) break;
                        string guest = g.Host == a.Id ? b.Id : a.Id;
                        S.Flags[$"gexplained:{g.Id}:{guest}"] = 1;
                        // both sides learn the invitation was revised — the grudge was a misunderstanding
                        Relations.Change(S, guest, g.Host, grudge: -0.12f, like: 0.05f, memory: "약속이 바뀐 줄 몰랐을 뿐이었다");
                        Relations.Change(S, g.Host, guest, grudge: -0.1f, like: 0.04f, memory: "초대장이 엇갈렸을 뿐이었다");
                        S.Log("GatheringExplained", g.Host, guest, data: g.Id);
                        break;
                    }
                case "g_missing":
                    {
                        var l = S.Loans.FirstOrDefault(x => x.Id == third); if (l == null) break; l.Complained = true;
                        if (l.Suspect == b.Id)
                        {
                            // the accused explains where they left it → a partial fix; the owner still has to find it
                            if (l.LeftRoom >= 0) { sim.Speak(b, "return_where", a.Id, new Dictionary<string, string> { { "place", S.RoomName(l.LeftRoom) } }); S.K(a.Id).Facts.Add($"loanat:{l.Id}:{l.LeftRoom}"); Relations.Change(S, a.Id, b.Id, grudge: -0.05f); }
                        }
                        else if (l.Suspect != null) { var kb = S.K(b.Id); kb.Suspicion[l.Suspect] = (kb.Suspicion.TryGetValue(l.Suspect, out var v) ? v : 0) + 0.05f; Relations.Change(S, b.Id, l.Suspect, trust: -0.04f, memory: "물건을 가져갔다는 말을 들었다"); }
                        break;
                    }
                case "g_return":
                    {
                        var l = S.Loans.FirstOrDefault(x => x.Id == third); if (l == null) break;
                        if (l.Coat) { a.BorrowedOutfitOf = null; S.Emit(GameEventType.Disguise, a.Id, data: null); }
                        else { var it = S.I(l.Item); if (it != null && it.Holder == a.Id) Hand(sim, a, b, it); }
                        l.State = "resolved"; S.Log("Return", a.Id, b.Id, item: l.Item, room: a.Room, data: l.Id);
                        Relations.Change(S, b.Id, a.Id, trust: 0.05f, like: 0.03f, memory: "빌려준 것을 제대로 돌려받았다");
                        break;
                    }
            }
        }

        /// <summary>Physical handover: item leaves giver's hands/pocket and goes into receiver's pocket. Visible to anyone watching.</summary>
        public static void Hand(Simulation sim, Actor giver, Actor recv, Item it)
        {
            var S = sim.S;
            if (giver.HandR == it.Id) giver.HandR = null; if (giver.HandL == it.Id) giver.HandL = null; giver.Pocket.Remove(it.Id);
            it.Holder = recv.Id; recv.Pocket.Add(it.Id); it.Room = -1; it.LastMovedTick = S.Tick;
            S.Log("Handover", giver.Id, recv.Id, item: it.Id, room: giver.Room, pos: giver.Pos, data: it.Type);
            S.Emit(GameEventType.ItemMoved, recv.Id, data: it.Id, text: "pickup");
            foreach (var w in S.Living.Where(x => x != giver && x != recv && x.Room == giver.Room && x.Pose != Pose.Sleep && x.Pos.DistXZ(giver.Pos) < 9))
            {
                var k = S.K(w.Id); k.Facts.Add($"handover:{giver.Id}:{recv.Id}:{it.Type}:{(int)S.Clock}");
                k.ItemSeen[it.Id] = (giver.Room, S.Clock);
                if (w.IsPlayer) Evidences.AddQuiet(sim, w.Id, EvKind.Sighting, $"{Cast.GivenOf(giver.Id)}이(가) {Cast.GivenOf(recv.Id)}에게 건넨 물건", $"{S.RoomName(giver.Room)}에서 {Cast.GivenOf(giver.Id)}이(가) {it.Def?.Kor ?? it.Kor}을(를) {Cast.GivenOf(recv.Id)}에게 건넸다.", "직접 목격", "hand:" + S.Seq, S.Clock, S.Clock, giver.Room, "누가 누구에게 무엇을 건넸는지", "그 물건이 나중에 어디에 쓰였는지", true,
                    new Prop { Kind = PropKind.Loaned, A = recv.Id, B = giver.Id, Item = it.Type, Room = giver.Room, T0 = S.Clock, T1 = S.Clock });
            }
        }

        static void LoansHourly(Simulation sim, Rng rng)
        {
            var S = sim.S;
            foreach (var l in S.Loans.ToList())
            {
                var owner = S.A(l.Owner); var bor = S.A(l.Borrower);
                if (l.Coat)
                {
                    if (l.State == "lent" && (bor == null || !bor.Alive)) { l.State = "resolved"; if (bor != null) bor.BorrowedOutfitOf = null; }
                    continue;
                }
                var it = S.I(l.Item); if (it == null) { l.State = "resolved"; continue; }
                // owner notices the absence when back in their room and the thing isn't where it belongs
                if ((l.State == "lent" || l.State == "misplaced") && owner != null && owner.Alive && owner.Room == it.HomeRoom && it.Room != it.HomeRoom && S.Clock > l.At + 120 && !S.K(owner.Id).Facts.Contains($"loanat:{l.Id}:{it.Room}"))
                {
                    bool knowsLent = l.State == "lent";
                    // the lender remembers lending; after too long it's still "they kept it"; a misplaced return looks like theft
                    l.State = "noticed"; l.Suspect = knowsLent ? l.Borrower : SeenHolding(sim, owner, it) ?? l.Borrower;
                    S.Log("ItemMissingNoticed", owner.Id, l.Suspect, item: it.Id, room: owner.Room, data: l.Id);
                    Relations.Change(S, owner.Id, l.Suspect, trust: -0.08f, grudge: knowsLent ? 0.04f : 0.1f, memory: knowsLent ? "빌려간 것을 돌려주지 않는다" : "내 물건이 없어졌다 — 가져간 것 같다");
                    owner.Needs.Anger = MathX.Clamp01(owner.Needs.Anger + 0.15f);
                }
                // found again (seen in a room by the owner) → resolved, grudge eases if it turns out to be a misplaced return
                if (l.State == "noticed" && owner != null && S.K(owner.Id).ItemSeen.TryGetValue(it.Id, out var seen) && seen.t > l.At && it.Holder == null)
                {
                    l.State = "resolved"; Relations.Change(S, owner.Id, l.Suspect ?? l.Borrower, grudge: -0.06f, memory: "없어진 줄 알았던 물건을 찾았다");
                    S.Log("ItemFound", owner.Id, item: it.Id, room: seen.room, data: l.Id);
                }
            }
        }

        static string SeenHolding(Simulation sim, Actor owner, Item it)
        {
            var k = sim.S.K(owner.Id);
            return k.Sightings.Where(s => s.Held == it.Type && s.Target != owner.Id).OrderByDescending(s => s.T1).Select(s => s.Target).FirstOrDefault();
        }

        /// <summary>Borrower's return: in person if the owner is easy to find, otherwise left somewhere (sometimes the wrong place).</summary>
        public static Activity ReturnActivity(Simulation sim, Actor a)
        {
            var S = sim.S; var rng = S.R(Stream.Life);
            var l = S.Loans.FirstOrDefault(x => x.Borrower == a.Id && x.State == "lent" && !x.Coat && S.Clock > x.Due);
            if (l == null) return null; var it = S.I(l.Item); if (it == null || it.Holder != a.Id) { if (l != null) l.State = "resolved"; return null; }
            var owner = S.A(l.Owner);
            var act = new Activity { Id = "g:return:" + l.Id, Label = "빌린 물건 반납", Priority = 1.6 };
            if (owner != null && owner.Alive && owner.Pos.Dist(a.Pos) < 30 && rng.Chance(0.5 + a.Def.P.Honesty * 0.3))
            {
                act.Steps.Add(new ActionStep { Kind = "Talk", Actor = owner.Id }); act.Steps.Add(new ActionStep { Kind = "X_Hand", Item = it.Id, Actor = owner.Id, Tag = "return", Data = l.Id });
                return act;
            }
            // leave it: owner's room if open, else a "somewhere sensible" room — the classic misplaced return
            var bed = S.Layout.BedroomOf(l.Owner);
            bool doorOpen = bed != null && bed.Doors.Count > 0 && !S.Layout.Doors[bed.Doors[0]].Locked;
            Room room = doorOpen && rng.Chance(0.6) ? bed : S.Layout.Rooms.Where(r => r.Type == RoomType.Lounge || r.Type == RoomType.Library || r.Type == RoomType.TeaRoom).OrderBy(r => r.Floor == a.Pos.f ? 0 : 1).FirstOrDefault() ?? bed;
            if (room == null) return null;
            act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(room, rng))); act.Steps.Add(new ActionStep { Kind = "X_LeaveLoan", Item = it.Id, Data = l.Id });
            return act;
        }

        // ================================================================== IG02 invitations & gatherings
        static readonly (string kind, string label, RoomType[] rooms, string hobby)[] Kinds =
        {
            ("tea", "차 모임", new[] { RoomType.TeaRoom, RoomType.Lounge, RoomType.Greenhouse }, null),
            ("cards", "카드 게임", new[] { RoomType.GameRoom, RoomType.Lounge }, "game"),
            ("music", "작은 연주회", new[] { RoomType.MusicRoom, RoomType.Theater }, "music"),
            ("reading", "낭독 모임", new[] { RoomType.Library, RoomType.Study }, "read"),
            ("party", "작은 파티", new[] { RoomType.Lounge, RoomType.Dining, RoomType.GameRoom }, "party"),
            ("show", "무대 발표회", new[] { RoomType.Theater, RoomType.MusicRoom }, "perform"),
            ("film", "상영회", new[] { RoomType.Theater, RoomType.Lounge }, "film"),
        };

        static void Host(Simulation sim, Rng rng)
        {
            var S = sim.S;
            var hosts = S.LivingNpcs.Where(a => a.Status == ActorStatus.Active && a.Def.P.Sociability > 0.45f && a.Needs.Fear < 0.5f).ToList();
            if (hosts.Count == 0) return;
            var host = rng.Weighted(hosts, a => a.Def.P.Sociability * (1.2f - a.Needs.Stress));
            var kinds = Kinds.Where(k => S.Layout.Rooms.Any(r => k.rooms.Contains(r.Type) && sim.RoomUsable(host, r))).ToList(); if (kinds.Count == 0) return;
            var kind = kinds.FirstOrDefault(k => k.hobby != null && host.Def.Hobbies.Contains(k.hobby)); if (kind.kind == null) kind = kinds[rng.R(kinds.Count)];
            var room = S.Layout.Rooms.Where(r => kind.rooms.Contains(r.Type) && sim.RoomUsable(host, r)).OrderBy(_ => rng.F()).First();
            double start = Math.Ceiling((S.Clock + rng.Range(90, 170)) / 10) * 10; double end = start + rng.Range(60, 110);
            if ((int)Math.Floor(start % 1440) > 22 * 60) return;
            var g = new Gathering { Id = S.NewId("gath"), Host = host.Id, Kind = kind.kind, Label = kind.label };
            g.Revs.Add(new GatheringRev { Room = room.Id, Start = start, End = end, At = S.Clock, Why = "처음 초대" });
            var guests = S.Living.Where(x => x != host && !x.IsButler && S.R(host.Id, x.Id).Opinion > (x.IsPlayer ? 0.08 : 0.12)).OrderByDescending(x => S.R(host.Id, x.Id).Opinion + rng.F() * 0.3).Take(2 + rng.R(4)).ToList();
            if (guests.Count == 0) return;
            foreach (var x in guests) { g.Status[x.Id] = "unaware"; g.KnownRev[x.Id] = -1; g.Channel[x.Id] = x.IsPlayer || S.R(host.Id, x.Id).Like < 0.3f || rng.Chance(0.5) ? "note" : "voice"; }
            g.Status[host.Id] = "host"; g.KnownRev[host.Id] = 0;
            S.Gatherings.Add(g);
            S.Log("GatheringPlanned", host.Id, data: $"{g.Id} {g.Label} @{room.Name} {ClockFmt.Vague(start)}-{ClockFmt.Vague(end)} guests={string.Join(",", guests.Select(x => x.Id))}");
            S.Dev($"GATHER {host.Id} {g.Label} {room.Name} {ClockFmt.HM(start)} guests {guests.Count}");
            // written invitations are delivered by walking to each room; spoken ones by finding each guest
            sim.Assign(host, InviteRound(sim, host, g));
        }

        static Activity InviteRound(Simulation sim, Actor host, Gathering g)
        {
            var act = g.Channel.Any(kv => kv.Value == "note") ? NotesActivity(sim, host, g) : new Activity { Id = "g:invite:" + g.Id, Label = "모임 초대", Priority = 2.4 };
            foreach (var kv in g.Channel.Where(kv => kv.Value == "voice"))
            {
                act.Steps.Add(new ActionStep { Kind = "Talk", Actor = kv.Key });
                act.Steps.Add(new ActionStep { Kind = "X_TellInvite", Actor = kv.Key, Data = g.Id });
            }
            return act;
        }

        static Activity NotesActivity(Simulation sim, Actor host, Gathering g)
        {
            var S = sim.S; var rng = S.R(Stream.Life);
            var act = new Activity { Id = "g:notes:" + g.Id, Label = "초대장 돌리기", Priority = 2.4 };
            var desk = S.Layout.BedroomOf(host.Id);
            if (desk != null) { act.Steps.Add(Simulation.GoTo(sim.RandomPointIn(desk, rng))); act.Steps.Add(Simulation.Do("write", 4, Anim.Write)); }
            foreach (var kv in g.Channel.Where(kv => kv.Value == "note"))
            {
                var bed = S.Layout.BedroomOf(kv.Key); if (bed == null || bed.Doors.Count == 0) continue;
                var d = S.Layout.Doors[bed.Doors[0]];
                act.Steps.Add(Simulation.GoTo(sim.SnapPublic(d.Pos)));
                act.Steps.Add(new ActionStep { Kind = "X_DropNote", Actor = kv.Key, Data = g.Id, Door = d.Id });
            }
            return act;
        }

        public static bool WouldAccept(Simulation sim, Actor x, Gathering g)
        {
            var S = sim.S; if (x.IsPlayer) return true;
            var r = S.R(x.Id, g.Host);
            double score = r.Opinion * 1.4 + x.Def.P.Sociability * 0.5 - x.Needs.Fear * 0.8 - x.Needs.Stress * 0.3 + (S.Chapter > 1 ? -0.1 : 0);
            // a schemer counts heads: a crowd is either a cover or an obstacle
            if (x.PlanId != null && S.Plans.TryGetValue(x.PlanId, out var pl) && pl.Grammar == "Gathering") score += 1;
            return score > 0.15;
        }

        /// <summary>Someone learns (current revision) of a gathering, verbally or by reading a note.</summary>
        public static void Tell(Simulation sim, Gathering g, Actor x, string channel, int rev = -1)
        {
            var S = sim.S; if (rev < 0) rev = g.Rev;
            int before = g.KnownRev.TryGetValue(x.Id, out var kr) ? kr : -1;
            g.KnownRev[x.Id] = Math.Max(before, rev);
            var R = g.Revs[g.KnownRev[x.Id]];
            S.K(x.Id).Facts.Add($"invite:{g.Id}:{g.KnownRev[x.Id]}");
            if (!g.Status.TryGetValue(x.Id, out var st) || st == "unaware" || st == "invited")
            {
                bool yes = WouldAccept(sim, x, g);
                g.Status[x.Id] = x.IsPlayer ? "invited" : (yes ? "accepted" : "declined");
                S.Log("InviteAnswer", x.Id, g.Host, room: R.Room, data: $"{g.Id} rev{g.KnownRev[x.Id]} {g.Status[x.Id]} via {channel}");
                if (!yes) Relations.Change(S, g.Host, x.Id, like: -0.02f, memory: "초대를 거절당했다");
            }
            if (x.IsPlayer)
                Evidences.AddQuiet(sim, Cast.Player, EvKind.Document, $"{Cast.GivenOf(g.Host)}의 {g.Label} 초대", $"{S.RoomName(R.Room)}, {ClockFmt.Vague(R.Start)}~{ClockFmt.Vague(R.End)}. ({(channel == "note" ? "초대장으로 받음" : "직접 들음")}, {g.KnownRev[x.Id] + 1}번째 안내)", channel == "note" ? "초대장" : "직접 들음", $"invite:{g.Id}:{g.KnownRev[x.Id]}", S.Clock, S.Clock, R.Room, "그때 알려 준 모임 장소와 시각", "그 뒤로 바뀌었는지", true,
                    new Prop { Kind = PropKind.Invited, A = x.Id, B = g.Host, Room = R.Room, T0 = R.Start, T1 = R.End, Value = "rev" + g.KnownRev[x.Id] });
        }

        static void GatheringTick(Simulation sim, Gathering g)
        {
            var S = sim.S; var rng = S.R(Stream.Life); var host = S.A(g.Host);
            if (host == null || !host.Alive || S.Phase != Phase.Daily) { if (S.Phase != Phase.Daily && S.Clock < g.Cur.Start) { g.Cancelled = true; S.Log("GatheringCancelled", g.Host, data: g.Id + " phase"); } else if (host == null || !host.Alive) { g.Cancelled = true; } return; }
            var cur = g.Cur;
            // revision (once): room taken / host's whim / a rule — told to some, not all (IG02)
            if (g.Rev == 0 && S.Clock > cur.Start - 60 && S.Clock < cur.Start - 20 && !S.Flags.ContainsKey("grevcheck:" + g.Id))
            {
                S.Flags["grevcheck:" + g.Id] = 1;
                bool occupied = S.Living.Count(x => x.Room == cur.Room && !g.Status.ContainsKey(x.Id)) >= 3;
                if (occupied || rng.Chance(0.3))
                {
                    var alt = S.Layout.Rooms.Where(r => r.Id != cur.Room && (r.Type == RoomType.Lounge || r.Type == RoomType.TeaRoom || r.Type == RoomType.Library || r.Type == RoomType.GameRoom || r.Type == RoomType.Greenhouse) && sim.RoomUsable(host, r)).OrderBy(_ => rng.F()).FirstOrDefault();
                    bool moveRoom = alt != null && (occupied || rng.Chance(0.5));
                    var nr = new GatheringRev { Room = moveRoom ? alt.Id : cur.Room, Start = moveRoom ? cur.Start : cur.Start + (rng.Chance(0.5) ? 30 : -20), End = cur.End + (moveRoom ? 0 : 20), At = S.Clock, Why = occupied ? "장소에 사람이 많음" : "사정이 생김" };
                    g.Revs.Add(nr); g.Rev = g.Revs.Count - 1;
                    S.Log("GatheringRevised", g.Host, data: $"{g.Id} rev{g.Rev} {S.RoomName(nr.Room)} {ClockFmt.Vague(nr.Start)} ({nr.Why})");
                    g.KnownRev[g.Host] = g.Rev;
                    // notes are re-delivered to rooms (read only when the guest goes back there); verbal guests get told when met
                    sim.Assign(host, InviteRound(sim, host, g));
                }
            }
            // arrivals (attendance activities are chosen by LifeActivity using each guest's KNOWN revision)
            foreach (var kv in g.Status.ToList())
            {
                var x = S.A(kv.Key); if (x == null || !x.Alive) continue;
                if (!g.KnownRev.TryGetValue(x.Id, out var kr) || kr < 0) continue;
                var R = g.Revs[kr];
                if (x.Room == R.Room && S.Clock >= R.Start - 15 && S.Clock <= R.End && !g.Arrived.ContainsKey(x.Id))
                {
                    g.Arrived[x.Id] = S.Clock;
                    if (kv.Value == "accepted" || kv.Value == "invited" || kv.Value == "host") g.Status[x.Id] = S.Clock > R.Start + 12 ? "late" : "attended";
                    if (kr != g.Rev && R.Room != g.Cur.Room) S.Flags[$"gstale:{g.Id}:{x.Id}"] = S.Clock;
                    S.Log("GatheringArrive", x.Id, g.Host, room: x.Room, data: $"{g.Id} rev{kr}");
                }
                if (g.Arrived.ContainsKey(x.Id) && !g.Left.ContainsKey(x.Id) && x.Room != R.Room && S.Clock < R.End - 5 && S.Clock > g.Arrived[x.Id] + 1)
                {
                    g.Left[x.Id] = S.Clock; S.Log("GatheringLeave", x.Id, g.Host, room: R.Room, data: g.Id);
                    foreach (var w in S.Living.Where(o => o != x && o.Room == R.Room && o.Pose != Pose.Sleep)) S.K(w.Id).Facts.Add($"left-gathering:{x.Id}:{g.Id}:{(int)S.Clock}");
                }
                else if (g.Left.ContainsKey(x.Id) && x.Room == R.Room && S.Clock <= R.End) { S.Log("GatheringReturn", x.Id, g.Host, room: R.Room, data: $"{g.Id} away {(int)(S.Clock - g.Left[x.Id])}min"); foreach (var w in S.Living.Where(o => o != x && o.Room == R.Room && o.Pose != Pose.Sleep)) S.K(w.Id).Facts.Add($"back-gathering:{x.Id}:{g.Id}:{(int)S.Clock}"); g.Left.Remove(x.Id); }
                // stood up: waited at the old place, nobody came
                if (g.Arrived.ContainsKey(x.Id) && kr != g.Rev && S.Clock > g.Arrived[x.Id] + 15 && g.Status[x.Id] != "stoodup" && x.Room == R.Room && !S.Living.Any(o => o != x && o.Room == R.Room && g.Status.ContainsKey(o.Id)))
                {
                    g.Status[x.Id] = "stoodup";
                    Relations.Change(S, x.Id, g.Host, grudge: 0.1f, trust: -0.08f, like: -0.04f, memory: "약속 장소에 아무도 오지 않았다 — 바람맞았다");
                    sim.Speak(x, "gathering_stoodup", null, new Dictionary<string, string> { { "t", "@" + g.Host } });
                    S.Log("GatheringStoodUp", x.Id, g.Host, room: x.Room, data: g.Id);
                    if (x.Act != null && x.Act.Id == "g:attend:" + g.Id) sim.Interrupt(x, 1);
                }
            }
            // chatter while running (group conversation, heard by everyone present)
            if (S.Clock >= cur.Start && S.Clock <= cur.End && S.Tick % 120 == 0)
            {
                var present = S.Living.Where(x => x.Room == cur.Room && g.Status.ContainsKey(x.Id) && x.Pose != Pose.Sleep && !x.IsPlayer).ToList();
                if (present.Count >= 2)
                {
                    var sp = present[rng.R(present.Count)]; var li = present.Where(p => p != sp).OrderBy(_ => rng.F()).First();
                    sim.Speak(sp, rng.Chance(0.4) ? "gathering_chat" : "small_talk", li.Id, new Dictionary<string, string> { { "act", g.Label } });
                    foreach (var p in present) foreach (var q in present) if (p != q) Relations.Change(S, p.Id, q.Id, like: 0.004f);
                    foreach (var p in present) p.Needs.Social = MathX.Clamp01(p.Needs.Social + 0.02f);
                }
            }
            if (S.Clock > cur.End) Close(sim, g);
        }

        static void Close(Simulation sim, Gathering g)
        {
            var S = sim.S; g.Done = true; var cur = g.Cur;
            foreach (var kv in g.Status.ToList())
            {
                if (kv.Value == "accepted" || kv.Value == "invited" && !g.Arrived.ContainsKey(kv.Key))
                {
                    // didn't come (or went to the old place): from the host's side it looks like a broken promise
                    if (kv.Value == "accepted") { Relations.Change(S, g.Host, kv.Key, like: -0.04f, grudge: 0.05f, memory: "오겠다더니 오지 않았다"); g.Status[kv.Key] = g.KnownRev.TryGetValue(kv.Key, out var kr) && kr != g.Rev && g.Arrived.ContainsKey(kv.Key) ? "stoodup" : "absent"; }
                }
            }
            S.Log("GatheringEnd", g.Host, room: cur.Room, data: $"{g.Id} attended={string.Join(",", g.Arrived.Keys)} left={string.Join(",", g.Left.Keys)}");
            // the player's own record of who was there (only what Minhyuk saw)
            var P = S.Player;
            if (P != null && g.Arrived.ContainsKey(Cast.Player))
            {
                var k = S.K(Cast.Player); var props = new List<Prop>(); var lines = new List<string>();
                foreach (var id in g.Arrived.Keys.Where(i => i != Cast.Player))
                {
                    var seen = k.Sightings.Where(s => s.Target == id && s.Room == cur.Room && s.T1 >= cur.Start - 15 && s.T0 <= cur.End).ToList(); if (seen.Count == 0) continue;
                    double t0 = seen.Min(s => s.T0), t1 = seen.Max(s => s.T1);
                    lines.Add($"{Cast.GivenOf(id)} {ClockFmt.Vague(t0)}~{ClockFmt.Vague(t1)}" + (seen.Count > 1 && seen.Zip(seen.Skip(1), (p, q) => q.T0 - p.T1).Any(gap => gap > 4) ? " (중간에 자리를 비움)" : ""));
                    props.Add(new Prop { Kind = PropKind.AtPlace, A = id, Room = cur.Room, T0 = t0, T1 = t1, Value = "root:gath:" + g.Id });
                }
                if (lines.Count > 0) Evidences.AddQuiet(sim, Cast.Player, EvKind.Sighting, $"{g.Label} 참석 기록", string.Join("\n", lines), "직접 목격", "gath:" + g.Id, cur.Start, cur.End, cur.Room, "내가 보는 동안 그 자리에 있던 사람과 시각", "내가 못 본 사이에 누가 어떻게 움직였는지", true, props.ToArray());
            }
        }

        // ================================================================== life hooks
        public static void LifeCandidates(Simulation sim, Actor a, List<(double score, Func<Activity> make, string id)> cands)
        {
            var S = sim.S;
            foreach (var g in S.Gatherings.Where(x => !x.Done && !x.Cancelled))
            {
                if (!g.Status.TryGetValue(a.Id, out var st) || !(st == "accepted" || st == "host" || st == "late" || st == "attended")) continue;
                int kr = g.KnownRev.TryGetValue(a.Id, out var k) ? k : -1; if (kr < 0) continue;
                var R = g.Revs[kr];
                if (S.Clock < R.Start - 12 || S.Clock > R.End - 5) continue;
                if (g.Left.ContainsKey(a.Id) && a.PlanId != null) continue; // slipping out on purpose
                if (a.Act != null && a.Act.Id == "g:attend:" + g.Id) continue;
                if (g.Status[a.Id] == "stoodup") continue;
                var gg = g; cands.Add((3.2, () => AttendActivity(sim, a, gg, R), "g:attend:" + g.Id));
            }
            var ret = S.Loans.FirstOrDefault(x => x.Borrower == a.Id && x.State == "lent" && !x.Coat && S.Clock > x.Due);
            if (ret != null) cands.Add((1.4 + a.Def.P.Honesty * 0.6, () => ReturnActivity(sim, a), "g:return"));
            var rep = S.Repairs.FirstOrDefault(r => r.Worker == a.Id && !r.Done && !r.Abandoned && S.Clock >= r.NextWork);
            if (rep != null && !S.IsNight) cands.Add((1.9, () => RepairActivity(sim, a, rep), "g:repair"));
            var lockIt = S.K(a.Id).Facts.FirstOrDefault(f => f.StartsWith("privacylock:"));
            if (lockIt != null) cands.Add((2.0, () => PrivacyLockActivity(sim, a, lockIt), "g:lock"));
            var disarm = S.Traps.FirstOrDefault(t => t.Owner == a.Id && t.Active && a.PlanId != t.Plan);
            if (disarm != null) cands.Add((4.0, () => Tricks.DisarmActivity(sim, a, disarm), "g:disarm"));
        }

        static Activity AttendActivity(Simulation sim, Actor a, Gathering g, GatheringRev R)
        {
            var S = sim.S; var rng = S.R(Stream.Life); var room = S.Layout.Room(R.Room); if (room == null) return null;
            var spot = room.Spots.Select(i => S.Layout.Spots[i]).Where(s => s.Occupant == null && (s.Tag == "sit" || s.Tag == "eat" || s.Tag == "read")).OrderBy(_ => rng.F()).FirstOrDefault();
            var act = new Activity { Id = "g:attend:" + g.Id, Label = g.Label, Priority = 3.2 };
            act.Steps.Add(Simulation.GoTo(spot != null ? spot.Approach : sim.RandomPointIn(room, rng)));
            act.Steps.Add(Simulation.Do("tea", Math.Max(5, R.End - Math.Max(S.Clock, R.Start - 10)), rng.Chance(0.5) ? Anim.Talk : Anim.Listen, spot?.Id ?? -1));
            return act;
        }

        // ================================================================== IG05 late privacy locks
        static void PrivacyLocks(Simulation sim, Rng rng)
        {
            var S = sim.S;
            foreach (var a in S.LivingNpcs.Where(x => x.Status == ActorStatus.Active))
            {
                var bed = S.Layout.BedroomOf(a.Id); if (bed == null || bed.Doors.Count == 0) continue;
                var d = S.Layout.Doors[bed.Doors[0]];
                if (d.Locked || a.Room == bed.Id) continue;
                // private people (a secret, pride, anxiety) go back to lock their room after hearing of trouble or at dusk
                bool reason = a.Def.P.Pride > 0.6f || a.Def.P.Fearfulness > 0.55f || S.K(a.Id).Facts.Contains("envelope-about-me");
                if (reason && rng.Chance(a.Needs.Fear > 0.3f || S.Minute >= 20 * 60 ? 0.12 : 0.03)) S.K(a.Id).Facts.Add("privacylock:" + d.Id);
            }
        }

        static Activity PrivacyLockActivity(Simulation sim, Actor a, string fact)
        {
            var S = sim.S; S.K(a.Id).Facts.Remove(fact);
            int door = int.Parse(fact.Substring("privacylock:".Length)); var d = S.Layout.Doors[door];
            if (d.Locked || !sim.HasKey(a, d)) return null;
            var act = new Activity { Id = "g:lock:" + door, Label = "방 문단속", Priority = 2 };
            act.Steps.Add(Simulation.GoTo(sim.SnapPublic(d.Pos)));
            // glance in from the doorway: only what is visible from there is noticed (a body behind furniture may not be)
            act.Steps.Add(new ActionStep { Kind = "X_Glance", Door = door });
            act.Steps.Add(new ActionStep { Kind = "Door", Door = door, Tag = "lock", Data = "사생활" });
            return act;
        }

        // ================================================================== IG07 repairs
        static readonly (string fault, string furn, string kor)[] Faults =
        {
            ("breaker", "Switchboard", "툭하면 내려가는 배전반 차단기"),
            ("pump", "PoolPump", "이상한 소리를 내는 수영장 펌프"),
            ("lock", "DoorLock", "헐거워진 방문 자물쇠"),
            ("clock", "Clock", "제멋대로 가는 괘종시계"),
        };

        static void NewFault(Simulation sim, Rng rng)
        {
            var S = sim.S;
            var f0 = Faults[rng.R(Faults.Length)];
            var furn = S.Layout.Furniture.Where(f => f.Type == f0.furn || (f0.furn == "PoolPump" && f.Type == "Press") || (f0.furn == "DoorLock" && f.Type == "Switchboard")).OrderBy(_ => rng.F()).FirstOrDefault();
            if (furn == null) return;
            var worker = S.LivingNpcs.Where(a => a.Status == ActorStatus.Active && (a.Def.Hobbies.Contains("craft") || a.Def.Hobbies.Contains("repair") || a.Def.Hobbies.Contains("restore") || a.Def.Hobbies.Contains("inspect"))).OrderBy(_ => rng.F()).FirstOrDefault();
            if (worker == null) return;
            var rep = new Repair { Id = S.NewId("rep"), Worker = worker.Id, Fault = f0.kor, Furniture = furn.Id, Since = S.Clock, NextWork = S.Clock + 10, Circuit = furn.Type == "Switchboard" ? 1 + rng.R(Math.Max(1, S.Layout.Circuits.Count - 1)) : -1 };
            S.Repairs.Add(rep);
            S.Log("FaultFound", Cast.Butler, worker.Id, room: furn.Room, data: $"{rep.Id} {f0.kor}");
            sim.Announce("y_repair", new Dictionary<string, string> { { "t", Cast.NameOf(worker.Id) }, { "place", S.RoomName(furn.Room) }, { "act", f0.kor } });
            foreach (var a in S.Living) S.K(a.Id).Facts.Add($"repair:{rep.Id}:{worker.Id}:{furn.Room}");
        }

        static Activity RepairActivity(Simulation sim, Actor a, Repair rep)
        {
            var S = sim.S; var f = S.Layout.Furniture.ElementAtOrDefault(rep.Furniture); if (f == null) { rep.Abandoned = true; return null; }
            var room = S.Layout.Room(f.Room); if (room == null || !sim.RoomUsable(a, room)) return null;
            var sp = room.Spots.Select(i => S.Layout.Spots[i]).FirstOrDefault(s => s.Furniture == f.Id);
            var act = new Activity { Id = "g:repair:" + rep.Id, Label = "수리 작업", Priority = 1.9 };
            act.Steps.Add(Simulation.GoTo(sp != null ? sp.Approach : sim.RandomPointIn(room, S.R(Stream.Life))));
            act.Steps.Add(Simulation.Do("craft", 15, Anim.Operate, -1));
            act.Steps.Add(new ActionStep { Kind = "X_RepairStep", Data = rep.Id });
            return act;
        }

        // ================================================================== butler clock round (IG09 "중간 조정")
        static void ButlerClockRound(Simulation sim, Rng rng)
        {
            var S = sim.S;
            var off = S.ClockOffset.Where(kv => Math.Abs(kv.Value) > 0.5).ToList(); if (off.Count == 0) return;
            var pick = off[rng.R(off.Count)]; var f = S.Layout.Furniture.ElementAtOrDefault(pick.Key); if (f == null) return;
            S.ClockOffset[pick.Key] = 0; f.Marks.Add($"{ClockFmt.Vague(S.Clock)}, 시계 바늘이 저절로 제 시각으로 돌아왔다 (그 전에는 {ClockFmt.VagueSpan(Math.Abs(pick.Value))} {(pick.Value > 0 ? "빨랐다" : "느렸다")})");
            S.Log("ClockAdjust", Cast.Butler, room: f.Room, data: $"f{f.Id} {pick.Value:0}");
        }

        // ================================================================== room change (door loggers, notes, missing items)
        public static void OnRoomChange(Simulation sim, Actor a, int from, int to)
        {
            var S = sim.S;
            // IG04: which door was crossed? only logged doors record, and only that doorway
            if (from >= 0 && to >= 0)
            {
                var r = S.Layout.Room(to); var rf = S.Layout.Room(from);
                var door = r?.Doors.Select(d => S.Layout.Doors[d]).Where(d => (d.RoomA == from && d.RoomB == to) || (d.RoomB == from && d.RoomA == to)).OrderBy(d => d.Pos.DistXZ(a.Pos)).FirstOrDefault();
                if (door != null && S.DoorLoggers.ContainsKey(door.Id))
                {
                    int logged = S.Layout.Furniture[S.DoorLoggers[door.Id]].Room;
                    S.DeviceLog.Add(new DeviceRecord { Door = door.Id, Room = logged, Actor = a.Id, Clock = S.Clock, Entering = to == logged });
                    if (S.DeviceLog.Count > 3000) S.DeviceLog.RemoveRange(0, 500);
                }
            }
            if (to < 0) return;
            // reading notes left in one's own room (invitations, deliveries)
            var room = S.Layout.Room(to);
            if (room != null && room.Owner == a.Id)
            {
                foreach (var it in S.Items.Values.Where(i => i.Room == to && i.Holder == null && i.Type == "Invitation" && i.Owner == a.Id && i.NoteFrom != null).ToList())
                {
                    var g = S.Gatherings.FirstOrDefault(x => x.Id == it.NoteFrom);
                    if (g != null && !a.IsPlayer) Tell(sim, g, a, "note", it.NoteRev);
                }
            }
        }

        public static void ReadInvitation(Simulation sim, Actor reader, Item it)
        {
            var S = sim.S; var g = S.Gatherings.FirstOrDefault(x => x.Id == it.NoteFrom); if (g == null) return;
            Tell(sim, g, reader, "note", it.NoteRev);
        }

        // ================================================================== step execution ("X_" everyday steps)
        public static bool Exec(Simulation sim, Actor a, ActionStep st)
        {
            var S = sim.S;
            switch (st.Kind)
            {
                case "X_TakeOwn":
                    {
                        var it = S.I(st.Item);
                        if (it == null || it.Holder != null || it.Pos.DistXZ(a.Pos) > 2f) { sim.Interrupt(a, 1); return true; }
                        it.Holder = a.Id; a.Pocket.Add(it.Id); it.Room = -1; a.Anim = Anim.PickUp;
                        S.Log("PickUp", a.Id, item: it.Id, room: a.Room, data: it.Type); S.Emit(GameEventType.ItemMoved, a.Id, data: it.Id, text: "pickup");
                        sim.NextStepPublic(a); return true;
                    }
                case "X_Hand":
                    {
                        var it = S.I(st.Item); var t = S.A(st.Actor);
                        if (it == null || t == null || it.Holder != a.Id || a.Pos.Dist(t.Pos) > 3f) { sim.NextStepPublic(a); return true; }
                        Hand(sim, a, t, it);
                        if (st.Tag == "lend") { var l = new Loan { Id = S.NewId("loan"), Item = it.Id, Owner = a.Id, Borrower = t.Id, At = S.Clock, Due = S.Clock + S.R(Stream.Life).Range(90, 300) }; S.Loans.Add(l); S.Log("Lend", a.Id, t.Id, item: it.Id, room: a.Room, data: l.Id); sim.Speak(a, "lend_give", t.Id, new Dictionary<string, string> { { "item", it.Def?.Kor } }); }
                        if (st.Tag == "return") { var l = S.Loans.FirstOrDefault(x => x.Id == st.Data); if (l != null) l.State = "resolved"; sim.Speak(a, "return_item", t.Id, new Dictionary<string, string> { { "item", it.Def?.Kor } }); Relations.Change(S, t.Id, a.Id, trust: 0.05f, memory: "빌려준 것을 돌려받았다"); }
                        if (st.Tag == "courier") Tricks.OnCourierHandover(sim, a, t, it);
                        sim.NextStepPublic(a); return true;
                    }
                case "X_LeaveLoan":
                    {
                        var it = S.I(st.Item); var l = S.Loans.FirstOrDefault(x => x.Id == st.Data);
                        if (it != null && it.Holder == a.Id) { sim.DropItem(a, it, a.Pos); }
                        if (l != null) { l.State = a.Room == it?.HomeRoom ? "resolved" : "misplaced"; l.LeftRoom = a.Room; l.LeftAt = S.Clock; S.Log("LeaveLoan", a.Id, l.Owner, item: l.Item, room: a.Room, data: l.Id + " " + l.State); S.K(a.Id).Facts.Add($"returned:{l.Id}:{a.Room}"); }
                        sim.NextStepPublic(a); return true;
                    }
                case "X_DropNote":
                    {
                        var g = S.Gatherings.FirstOrDefault(x => x.Id == st.Data); var d = S.Layout.Doors[st.Door];
                        if (g != null)
                        {
                            var bed = S.Layout.BedroomOf(st.Actor);
                            var inside = bed != null ? sim.SnapPublic(new P3(d.Pos.f, d.Pos.x + (bed.Rect.CX > d.Pos.x ? 0.6f : -0.6f) * (d.AlongX ? 0 : 1), d.Pos.z + (bed.Rect.CZ > d.Pos.z ? 0.6f : -0.6f) * (d.AlongX ? 1 : 0))) : d.Pos;
                            var R = g.Cur;
                            var note = new Item { Id = S.NewId("it"), Type = "Invitation", Name = $"{Cast.GivenOf(g.Host)}의 초대장" + (g.Rev > 0 ? " (수정본)" : ""), Owner = st.Actor, Pos = inside, Room = bed?.Id ?? a.Room, NoteFrom = g.Id, NoteRev = g.Rev, Note = $"{g.Label}에 초대합니다 — {S.RoomName(R.Room)}, {ClockFmt.Vague(R.Start)}부터. {Cast.GivenOf(g.Host)}" };
                            S.Items[note.Id] = note; S.Emit(GameEventType.ItemMoved, a.Id, data: note.Id, text: "spawn", pos: note.Pos);
                            S.Log("NoteDelivered", a.Id, st.Actor, item: note.Id, room: note.Room, data: $"{g.Id} rev{g.Rev}" + (d.Locked ? " under-door" : ""));
                            if (g.Status.TryGetValue(st.Actor, out var s0) && s0 == "unaware") g.Status[st.Actor] = "invited";
                            // someone already inside reads it now
                            var owner = S.A(st.Actor); if (owner != null && owner.Room == note.Room && owner.Pose != Pose.Sleep && !owner.IsPlayer) Tell(sim, g, owner, "note", g.Rev);
                        }
                        sim.NextStepPublic(a); return true;
                    }
                case "X_Grab":
                    {
                        var t = S.A(st.Actor);
                        if (t != null && t.Status != ActorStatus.Active && t.CarriedBy == null && a.Carrying == null && t.Pos.Dist(a.Pos) < 2.4f)
                        {
                            a.Carrying = t.Id; t.CarriedBy = a.Id; a.Anim = Anim.Carry;
                            // pressure on the wound while carrying: bleeding slows, the clock buys a few minutes
                            if (t.Alive) { t.Body.Bleed *= 0.5f; if (t.Body.DeathAt > 0) t.Body.DeathAt += 6; }
                            S.Log("CarryStart", a.Id, t.Id, room: a.Room, pos: a.Pos, data: "rescue"); S.Emit(GameEventType.Carry, a.Id, t.Id, value: 1);
                        }
                        sim.NextStepPublic(a); return true;
                    }
                case "X_Drop":
                    {
                        var t = S.A(a.Carrying);
                        if (t != null)
                        {
                            t.CarriedBy = null; a.Carrying = null; t.Pos = a.Pos; t.Room = a.Room; t.Pose = Pose.LieBack;
                            S.Log("CarryEnd", a.Id, t.Id, room: a.Room, pos: a.Pos, data: st.Tag ?? "rescue"); S.Emit(GameEventType.Carry, a.Id, t.Id, value: 0);
                            var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == t.Id && i.Loop == S.Loop); if (inc != null) { inc.BodyMoved = true; inc.Notes.Add("구조를 위해 옮겨짐: " + S.RoomName(a.Room)); }
                        }
                        sim.NextStepPublic(a); return true;
                    }
                case "X_LogLoan":
                    {
                        // CH07: the lending sheet gets a line; due in 20 minutes
                        var it = S.I(st.Item); var sheet = S.I(st.Data);
                        if (it != null && it.Holder == a.Id)
                        {
                            var l = new Loan { Id = S.NewId("loan"), Item = it.Id, Owner = it.Owner ?? Cast.Butler, Borrower = a.Id, At = S.Clock, Due = S.Clock + 20 };
                            S.Loans.Add(l); S.Log("Lend", it.Owner ?? Cast.Butler, a.Id, item: it.Id, room: a.Room, data: l.Id + " CH07");
                            if (sheet != null) sheet.Note += $"\n{ClockFmt.Vague(S.Clock)} {Cast.GivenOf(a.Id)} — {it.Kor}";
                        }
                        sim.NextStepPublic(a); return true;
                    }
                case "X_TellInvite":
                    {
                        var g = S.Gatherings.FirstOrDefault(x => x.Id == st.Data); var t = S.A(st.Actor);
                        if (g != null && t != null && t.Alive && t.Pos.Dist(a.Pos) < 4f && (!g.KnownRev.TryGetValue(t.Id, out var kr) || kr < g.Rev))
                        {
                            var R = g.Cur; bool change = kr >= 0 && kr < g.Rev;
                            sim.Speak(a, change ? "gathering_change" : "gathering_invite", t.Id, new Dictionary<string, string> { { "act", g.Label }, { "place", S.RoomName(R.Room) }, { "time", ClockFmt.Vague(R.Start) } },
                                new Prop { Kind = PropKind.Invited, A = t.Id, B = g.Host, Room = R.Room, T0 = R.Start, T1 = R.End, Value = "rev" + g.Rev });
                            Tell(sim, g, t, "voice");
                            if (!t.IsPlayer) sim.Speak(t, g.Status.TryGetValue(t.Id, out var s1) && s1 == "declined" ? "gathering_no" : "gathering_yes", a.Id);
                        }
                        sim.NextStepPublic(a); return true;
                    }
                case "X_Glance":
                    {
                        // look in from the doorway; bodies only register if actually visible (regular perception does that) — pause briefly
                        a.Speed = 0; a.Anim = Anim.Idle;
                        if (S.Clock - a.Act.StepStart > 0.5) sim.NextStepPublic(a);
                        return true;
                    }
                case "X_RepairStep":
                    {
                        var rep = S.Repairs.FirstOrDefault(r => r.Id == st.Data); if (rep == null) { sim.NextStepPublic(a); return true; }
                        var rng = S.R(Stream.Life);
                        rep.Stage++; rep.NextWork = S.Clock + rng.Range(60, 180);
                        string rev = $"{rep.Stage}차 {ClockFmt.Vague(S.Clock)} {Cast.GivenOf(a.Id)}: " + (rep.Stage >= rep.Stages ? "수리 끝" : rep.Stage == 1 ? "덮개를 열고 원인을 찾음" : "임시로 이어 둠 — 아직 불안하다");
                        rep.Revs.Add(rev); var f = S.Layout.Furniture.ElementAtOrDefault(rep.Furniture); f?.Marks.Add(rev);
                        S.Log("RepairWork", a.Id, room: a.Room, data: rep.Id + " " + rev);
                        if (rep.Stage >= rep.Stages) rep.Done = true;
                        // pride: claim it's finished when it isn't (the unfinished state stays in the device)
                        else if (a.Def.P.Pride > 0.6f && rng.Chance(0.4)) { rep.ClaimedDone = true; S.Log("Lie", a.Id, data: $"수리를 끝냈다고 말함 ({rep.Id})", secret: true); sim.Speak(a, "repair_claim", null); }
                        sim.NextStepPublic(a); return true;
                    }
            }
            return Tricks.Exec(sim, a, st);
        }


        /// <summary>Extra detail when anyone examines furniture that belongs to a grammar (clock, logger, repair, trap).</summary>
        public static void ExamineFurniture(Simulation sim, Actor who, Furniture f, ref string desc, List<Prop> props)
        {
            var S = sim.S;
            if (f.Type == "Clock" && S.ClockOffset.TryGetValue(f.Id, out var off))
            {
                desc = Math.Abs(off) < 0.5 ? $"괘종시계가 종소리와 딱 맞는다 — {ClockFmt.Vague(S.Clock)}" : $"괘종시계 바늘이 가리키는 시각은 {ClockFmt.Vague(S.Clock + off)} — 방금 울린 종소리보다 {ClockFmt.VagueSpan(Math.Abs(off))} {(off > 0 ? "빠르다" : "느리다")}";
                props.Add(new Prop { Kind = PropKind.ClockOffset, Room = f.Room, Value = ((int)Math.Round(off)).ToString(), T0 = S.Clock, T1 = S.Clock });
            }
            if (f.Type == "DoorLogger")
            {
                var door = S.DoorLoggers.FirstOrDefault(kv => kv.Value == f.Id).Key; var room = S.Layout.Room(f.Room);
                var recs = S.DeviceLog.Where(r => r.Door == door && S.Clock - r.Clock < 24 * 60).OrderBy(r => r.Clock).ToList();
                int others = room != null ? room.Doors.Count - 1 : 0;
                desc = $"출입 기록기 — 이 문으로 드나든 사람만 적힌다(이 방의 다른 출입구: {others}개).\n" + (recs.Count == 0 ? "최근에 적힌 사람 없음" : string.Join("\n", recs.TakeLast(12).Select(r => $"{ClockFmt.Vague(r.Clock)} {Cast.GivenOf(r.Actor)} {(r.Entering ? "들어옴" : "나감")}")));
                foreach (var r in recs.TakeLast(20)) props.Add(new Prop { Kind = PropKind.DeviceRecord, A = r.Actor, Room = r.Room, T0 = r.Clock, T1 = r.Clock, Value = r.Entering ? "in" : "out", Item = "door" + r.Door });
                props.Add(new Prop { Kind = PropKind.DeviceRecord, Room = f.Room, Value = "coverage-partial", Item = "door" + door, T0 = S.Clock - 1440, T1 = S.Clock });
            }
            var rep = S.Repairs.LastOrDefault(r => r.Furniture == f.Id);
            if (rep != null) { desc += $"\n수리 기록({rep.Fault}): " + (rep.Revs.Count == 0 ? "아직 아무도 손대지 않았다" : string.Join(" / ", rep.Revs)) + (rep.Done ? "" : " — 아직 덜 고쳤다"); props.Add(new Prop { Kind = PropKind.MachineUsed, A = rep.Worker, Value = "수리 " + (rep.Done ? "완료" : "미완료"), T0 = rep.Since, T1 = S.Clock }); }
            Tricks.ExamineFurniture(sim, who, f, ref desc, props);
            SetPieces.FurnitureNotes(sim, f, ref desc, props);
        }
    }
}
