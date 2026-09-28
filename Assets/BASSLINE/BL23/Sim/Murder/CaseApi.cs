using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BL23.Sim
{
    // =====================================================================================================================
    // CaseApi — the "case truth vs presented story" record for the 심판 (Documentation/BL23/CaseTruthApi.md).
    // Built on demand from saved state (the Scheme, the MurderPlan, the Incident, the ledger, the beats): read-only,
    // deterministic, nothing stored (save round-trips stay IDENTICAL). A murder with no scheme behind it (an impulsive
    // legacy plan) still gets a pack, marked Improvised, whose shields are detected rather than invented.
    // =====================================================================================================================

    public sealed class TrialPack
    {
        public string Incident, Scheme, Plan; public bool Improvised, Judged; public int Order; public string Logline;
        public CaseTruth Truth = new CaseTruth(); public PresentedStory Story = new PresentedStory();
        public List<PackLie> Lies = new List<PackLie>(); public List<RuleShield> Shields = new List<RuleShield>();
        public List<FallbackStory> Fallbacks = new List<FallbackStory>(); public List<ForeshadowBeat> Foreshadow = new List<ForeshadowBeat>();
        public List<PackRole> Roles = new List<PackRole>(); public List<string> Log = new List<string>();
        public int LieBudget;
    }
    public sealed class CaseTruth
    {
        public string Culprit, Victim, Motive, MotiveText, Trigger, Moment, MomentText; public int MomentRoom = -1; public double MomentAt = -1;
        public string Approach, Method, Weapon, WeaponType; public int KillRoom = -1, FoundRoom = -1; public double KillClock = -1, FoundClock = -1;
        public bool Hosted; public string EventId, EventLabel; public List<string> Guests = new List<string>();
        public List<string> Variants = new List<string>(); public List<PrepRecord> Preparations = new List<PrepRecord>(); public List<string> Tricks = new List<string>();
    }
    public sealed class PrepRecord { public string Kind, Item, Text; public int Room = -1; public double Clock = -1; public bool Done; public List<string> SeenBy = new List<string>(); }
    public sealed class PresentedStory
    {
        public string Speaker; public int ClaimRoom = -1; public double ClaimFrom = -1, ClaimTo = -1; public string ClaimText;
        public List<string> ClaimWith = new List<string>(); public string Account, Theory, Scapegoat, ScapegoatCase; public List<string> Planted = new List<string>();
    }
    public sealed class PackLie { public string Id, Topic, Text, Truth; public List<string> BrokenBy = new List<string>(); public int Cost = 1; public bool Prepared; }
    public sealed class RuleShield { public string Rule, Tactic, Argument, When, Counter; public bool Prepared; }
    public sealed class FallbackStory { public int Order; public string Trigger, Story, Concedes, Keeps; }
    public sealed class ForeshadowBeat { public string Id, Kind, Actor, Item, Text, Innocent, Meaning; public int Room = -1; public double Clock = -1; public List<string> Observers = new List<string>(); public bool PlayerSaw; }
    public sealed class PackRole { public string Role, Actor, Did; }

    public static class CaseApi
    {
        static string N(string id) => Cast.GivenOf(id);
        static string K(string s) => LineBank.FixParticles(s);
        static string T(double t) => ClockFmt.Vague(t);

        public static Scheme SchemeOf(GameState S, string culprit)
        {
            if (S?.Mur == null || culprit == null) return null;
            Scheme best = null; foreach (var x in S.Mur.Schemes) if (x.Culprit == culprit && x.Loop == S.Loop) best = x;
            return best;
        }
        public static string PlannedScapegoat(GameState S, string culprit)
        {
            var sc = SchemeOf(S, culprit); if (sc == null || sc.Scapegoat == null) return null;
            if (sc.State == "Abandoned" && sc.Killed < 0) return null;
            return sc.Scapegoat;
        }
        public static List<ForeshadowBeat> BeatsSeenBy(GameState S, string observer, int loop = -1)
        {
            var res = new List<ForeshadowBeat>(); if (S?.Mur == null) return res;
            foreach (var b in S.Mur.Beats) if (b.Observers.Contains(observer) && (loop < 0 ? b.Loop == S.Loop : b.Loop == loop)) res.Add(ToBeat(b));
            return res;
        }
        public static TrialPack ForTarget(GameState S) => S?.Ch?.TargetIncident != null ? TrialPack(S, S.Ch.TargetIncident) : null;
        public static List<TrialPack> Chapter(GameState S)
        {
            var res = new List<TrialPack>();
            foreach (var i in S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Murder).OrderBy(i => i.ResultSeq)) { var p = TrialPack(S, i.Id); if (p != null) res.Add(p); }
            return res;
        }

        static ForeshadowBeat ToBeat(SchemeBeat b) => new ForeshadowBeat { Id = b.Id, Kind = b.Kind, Actor = b.Actor, Item = b.Item, Text = b.Text, Innocent = b.Innocent, Meaning = b.Meaning, Room = b.Room, Clock = b.Clock, Observers = new List<string>(b.Observers), PlayerSaw = b.Observers.Contains(Cast.Player) };

        // ================================================================== the pack
        public static TrialPack TrialPack(GameState S, string incidentId)
        {
            if (S == null || incidentId == null || !S.Incidents.TryGetValue(incidentId, out var inc) || !inc.Murder || inc.Culprit == null) return null;
            var plan = inc.PlanId != null && S.Plans.TryGetValue(inc.PlanId, out var pl) ? pl : null;
            var sc = plan != null ? Initiative.ByPlan(S, plan.Id) : null;
            var p = new TrialPack { Incident = inc.Id, Plan = plan?.Id, Scheme = sc?.Id, Improvised = sc == null };
            p.Order = S.Incidents.Values.Count(i => i.Loop == inc.Loop && i.Chapter == inc.Chapter && i.Murder && i.ResultSeq < inc.ResultSeq) + 1;
            p.Judged = S.Ch.TargetIncident == inc.Id;
            var c = Cast.Get(inc.Culprit);
            p.LieBudget = sc?.LieBudget ?? (2 + ((c?.Deceit ?? 50) >= 70 ? 1 : 0) + ((c?.Composure ?? 50) >= 80 ? 1 : 0));
            FillTruth(S, p, inc, plan, sc);
            FillStory(S, p, inc, plan, sc);
            FillLies(S, p, inc, plan, sc);
            FillShields(S, p, inc, plan, sc);
            FillFallbacks(S, p, inc, plan, sc);
            FillRoles(S, p, inc, plan, sc);
            if (sc != null) { foreach (var b in S.Mur.Beats) if (b.Scheme == sc.Id) p.Foreshadow.Add(ToBeat(b)); p.Log.AddRange(sc.Log); }
            else if (plan != null) p.Log.AddRange(plan.Log);
            p.Logline = Logline(S, p, inc, sc);
            return p;
        }

        static void FillTruth(GameState S, TrialPack p, Incident inc, MurderPlan plan, Scheme sc)
        {
            var t = p.Truth;
            t.Culprit = inc.Culprit; t.Victim = inc.Victim; t.Method = plan?.Grammar ?? inc.Method; t.Weapon = inc.Weapon ?? plan?.Weapon; t.WeaponType = inc.WeaponType ?? plan?.WeaponType;
            t.KillRoom = inc.CauseRoom >= 0 ? inc.CauseRoom : inc.DeathRoom; t.KillClock = inc.CauseClock >= 0 ? inc.CauseClock : inc.DeathClock; t.FoundRoom = inc.FoundRoom; t.FoundClock = inc.DiscoverClock;
            if (sc != null)
            {
                t.Motive = sc.Motive; t.MotiveText = Initiative.MotiveKor(sc.Motive); t.Trigger = sc.Trigger;
                t.Moment = sc.Moment; t.MomentText = Initiative.MomentText(sc); t.MomentRoom = sc.MomentRoom; t.MomentAt = sc.MomentAt; t.Approach = sc.Approach;
                t.Hosted = sc.Has("hosted"); t.EventId = sc.EventId; t.EventLabel = sc.EventLabel; t.Guests.AddRange(sc.Guests);
                t.Variants.AddRange(sc.Variants);
                foreach (var pr in sc.Prep)
                {
                    var b = pr.Beat != null ? S.Mur.Beats.FirstOrDefault(x => x.Id == pr.Beat) : null;
                    var rec = new PrepRecord { Kind = pr.Kind, Item = pr.Item, Done = pr.Done, Clock = pr.DoneAt, Room = b?.Room ?? pr.Room, Text = b?.Text ?? pr.Kind };
                    if (b != null) rec.SeenBy.AddRange(b.Observers);
                    t.Preparations.Add(rec);
                }
            }
            else
            {
                t.Motive = plan?.Motive ?? "unknown"; t.MotiveText = Initiative.MotiveKor(t.Motive); t.Trigger = plan?.Reason;
                t.Moment = "improvised"; t.MomentText = "눈앞의 기회"; t.Approach = plan != null ? Head(plan.Grammar) : "direct";
                t.Variants.Add("improvised");
            }
            if (plan != null) t.Tricks.AddRange(ExecutedLayers(S, plan));
        }

        static string Head(string g) { if (string.IsNullOrEmpty(g)) return g; int i = g.IndexOfAny(new[] { '+', '→' }); return i < 0 ? g : g.Substring(0, i); }

        static readonly Dictionary<string, string> LayerSig = new Dictionary<string, string>
        {
            ["Seal"] = "SealedRoom", ["Tod"] = "TodShift", ["Message"] = "FakeMessage", ["Swap"] = "PlantWeapon", ["KeySlide"] = "KeySlide", ["FakeNote"] = "FakeNote",
            ["Burn"] = "Burn", ["Dump"] = "DumpWater", ["Bury"] = "Bury", ["Noise"] = "NoiseMask", ["ColdHide"] = "ColdHide", ["Dismember"] = "Dismember", ["Recorder"] = "RecorderArmed",
            ["Mutilate"] = "PostmortemDamage", ["Framed"] = "PlantToken", ["ClockAlibi"] = "ClockTamper", ["Helper"] = "FavourDone", ["Marked"] = "DarkStrike",
        };
        static List<string> ExecutedLayers(GameState S, MurderPlan plan)
        {
            var res = new List<string>(); var parts = (plan.Grammar ?? "").Split('+');
            var types = new HashSet<string>(); foreach (var e in S.Ledger) if (e.Plan == plan.Id || (e.Actor == plan.Actor && e.Clock >= plan.Formed - 1)) types.Add(e.Type);
            for (int i = 1; i < parts.Length; i++)
            {
                var layer = parts[i];
                if (LayerSig.TryGetValue(layer, out var sig)) { if (types.Contains(sig)) res.Add(layer); }
                else res.Add(layer);
            }
            return res;
        }

        // ------------------------------------------------------------------ the presented story
        static void FillStory(GameState S, TrialPack p, Incident inc, MurderPlan plan, Scheme sc)
        {
            var st = p.Story; st.Speaker = inc.Culprit; string me = inc.Culprit; string v = N(inc.Victim);
            double kt = p.Truth.KillClock;
            if (sc != null && (sc.Alibi == "crowd" || sc.Alibi == "remote") && sc.MomentRoom >= 0)
            {
                st.ClaimRoom = sc.EventRoom >= 0 ? sc.EventRoom : sc.MomentRoom; st.ClaimFrom = sc.MomentAt; st.ClaimTo = Math.Max(sc.MomentEnd, kt + 20);
                st.ClaimText = sc.Has("hosted") ? K($"{T(sc.MomentAt)}부터 {S.RoomName(st.ClaimRoom)}에서 {sc.EventLabel}을(를) 열고 있었어요. 손님들이 다 봤어요.")
                             : K($"{T(sc.MomentAt)}쯤부터 {S.RoomName(st.ClaimRoom)}에 사람들과 같이 있었어요.");
            }
            else if (sc != null && (sc.Alibi == "witness" || sc.Alibi == "clock") && sc.AlibiRoom >= 0 && MetWitness(S, sc, me))
            {
                st.ClaimRoom = sc.AlibiRoom; st.ClaimFrom = sc.Alibi == "clock" ? sc.AlibiAt + sc.ClockShift : sc.AlibiAt - 15; st.ClaimTo = sc.AlibiAt + 30;
                st.ClaimText = K($"{T(st.ClaimFrom)}부터 {S.RoomName(sc.AlibiRoom)}에서 {N(sc.AlibiWitness)}와(과) 같이 있었어요. {N(sc.AlibiWitness)}한테 물어보세요.");
                st.ClaimWith.Add(sc.AlibiWitness);
            }
            else if (sc != null && sc.Alibi == "zone" && sc.AlibiRoom >= 0)
            {
                st.ClaimRoom = sc.AlibiRoom; st.ClaimFrom = kt - 25; st.ClaimTo = kt + 20; st.ClaimText = ZoneClaim(S, sc.AlibiRoom);
            }
            else
            {
                int room = plan != null && plan.AlibiClaimRoom >= 0 ? plan.AlibiClaimRoom : plan != null && plan.AlibiRoom >= 0 ? plan.AlibiRoom : S.Layout.BedroomOf(me)?.Id ?? -1;
                st.ClaimRoom = room; st.ClaimFrom = kt - 25; st.ClaimTo = kt + 20;
                st.ClaimText = ClaimAt(S, me, room, kt);
            }
            // company the culprit can name (their own sightings in the claimed room around the window)
            List<string> Company(int room) => room >= 0 && S.Know.TryGetValue(me, out var k)
                ? k.Sightings.Where(s => s.Room == room && s.T1 >= st.ClaimFrom && s.T0 <= st.ClaimTo && s.IdConf > 0.5f && s.Target != inc.Victim && s.Target != me && !s.Dead).Select(s => s.Target).Distinct().OrderBy(x => x, StringComparer.Ordinal).Take(3).ToList()
                : new List<string>();
            // never a confession: a claimed room that is the scene itself with no crowd there at the deed (a house blackout's "crowd"
            // that was the victim alone, a moment in the victim's own room, a plan that never reached its alibi step) becomes where
            // they say they were instead — the zone the chart posted them to during a treasure hunt, else the last other room they
            // were in before the deed, else their own room. A meal or an evening where everyone sat together stays as it is.
            int kr = p.Truth.KillRoom, fr = p.Truth.FoundRoom;
            bool gathering = sc != null && (sc.Moment == "hosted" || sc.Moment == "joined" || sc.Moment == "meal");
            int crowdAtKill = S.Know.TryGetValue(me, out var km) ? km.Sightings.Where(s => s.Room == st.ClaimRoom && s.T1 >= kt - 5 && s.T0 <= kt + 5 && s.IdConf > 0.5f && s.Target != inc.Victim && s.Target != me && !s.Dead).Select(s => s.Target).Distinct().Count() : 0;
            if (st.ClaimRoom < 0 || ((st.ClaimRoom == kr || st.ClaimRoom == fr) && !gathering && crowdAtKill < 2))
            {
                st.ClaimWith.Clear(); st.ClaimFrom = kt - 25; st.ClaimTo = kt + 20;
                if (HouseEvents.HuntZoneAt(S, me, kt, out int hz, out _, out _) && hz != kr && hz != fr) { st.ClaimRoom = hz; st.ClaimText = ZoneClaim(S, hz); }
                else { st.ClaimRoom = RoomBefore(S, me, kt, kr, fr); st.ClaimText = ClaimAt(S, me, st.ClaimRoom, kt); }
            }
            foreach (var g in Company(st.ClaimRoom)) if (!st.ClaimWith.Contains(g)) st.ClaimWith.Add(g);
            // the theory they push
            string sg = sc?.Scapegoat ?? ImprovisedScapegoat(S, inc);
            st.Scapegoat = sg;
            if (sc != null && sc.Scapegoat != null) { st.ScapegoatCase = FrameCase(S, sc, inc); st.Planted.AddRange(sc.Planted); }
            else if (sg != null) st.ScapegoatCase = K($"{N(sg)}이(가) {v}와(과) 사이가 안 좋았잖아요.");
            string head = Head(plan?.Grammar ?? inc.Method);
            if (Methods.Staged(head ?? "")) st.Theory = head == "Smother" ? K($"{v}은(는) 자다가 숨이 멎은 거예요. 누가 죽인 게 아니에요.") : head == "Bedtime" ? K($"{v}은(는) 뭘 잘못 먹은 거예요. 사고예요.") : K($"{v}은(는) 사고로 죽은 거예요. 발을 헛디뎠거나, 기계를 잘못 만졌거나.");
            else if (sc != null && sc.Has("copycat")) st.Theory = K("첫 사건의 범인이 또 저지른 거예요. 방식이 똑같잖아요.");
            else if (sc != null && sc.Approach == "dark-strike") st.Theory = K($"불이 꺼져 있었으니 그 방 누구라도 할 수 있었어요. {(sg != null ? N(sg) + "이(가) 어둠 속에서 자리를 옮겼어요." : "저는 제자리에 있었고요.")}");
            else if (sg != null) st.Theory = K($"{N(sg)}이(가) 한 거예요. {st.ScapegoatCase}");
            else st.Theory = K("누가 했는지는 모르겠어요. 하지만 저는 아니에요.");
            st.Account = K(st.ClaimText + " " + (sc != null && sc.Approach == "errand" ? $"{v}이(가) {sc.Pretext}을(를) 가지러 간 뒤로는 못 봤어요. " : "") + (sg != null ? st.Theory : ""));
        }

        /// <summary>Did the arranged meeting happen — did the witness see them in that room around its hour? A witness who never came
        /// (called away, a hunt, a body found) is not named: the story falls back to the room alone.</summary>
        static bool MetWitness(GameState S, Scheme sc, string me)
            => sc.AlibiWitness != null && S.Know.TryGetValue(sc.AlibiWitness, out var kw)
               && kw.Sightings.Any(s => s.Target == me && s.Room == sc.AlibiRoom && s.IdConf > 0.5f && s.T1 >= sc.AlibiAt - 25 && s.T0 <= sc.AlibiAt + 45);

        static string ClaimAt(GameState S, string me, int room, double kt)
            => room >= 0 && S.Layout.Room(room)?.Type == RoomType.Bedroom && S.Layout.Room(room)?.Owner == me ? K($"{T(kt - 25)}쯤엔 제 방에 혼자 있었어요. 증명할 사람은 없지만요.")
             : K($"{T(kt - 25)}쯤엔 {S.RoomName(room)}에 있었어요.");

        /// <summary>The treasure hunt's alibi: the chart posted everyone alone, so it is the one thing anyone can say.</summary>
        static string ZoneClaim(GameState S, int zone) => K($"보물찾기 동안엔 쭉 제 구역, {S.RoomName(zone)}에 있었어요. 구역표대로요. 혼자였으니 증명할 사람은 없지만요.");

        /// <summary>The last room (not a passage, not the scene) the culprit entered in the two hours before the deed, else their own
        /// room, else the lounge.</summary>
        static int RoomBefore(GameState S, string me, double kt, int kr, int fr)
        {
            int best = -1;
            foreach (var e in S.Ledger.Where(e => e.Type == "Enter" && e.Actor == me && e.Clock <= kt && e.Clock >= kt - 120).OrderBy(e => e.Seq))
            {
                var r = S.Layout.Room(e.Room); if (r == null || e.Room == kr || e.Room == fr || RoomInfo.IsPassage(r.Type)) continue;
                best = e.Room;
            }
            if (best >= 0) return best;
            var bed = S.Layout.BedroomOf(me); if (bed != null && bed.Id != kr && bed.Id != fr) return bed.Id;
            return S.Layout.Rooms.Where(r => r.Id != kr && r.Id != fr && (r.Type == RoomType.Lounge || r.Type == RoomType.Library || r.Type == RoomType.Dining)).OrderBy(r => r.Id).Select(r => r.Id).DefaultIfEmpty(-1).First();
        }

        static string ImprovisedScapegoat(GameState S, Incident inc)
        {
            string best = null; double bs = 0.25;
            foreach (var x in S.Actors.Values.Where(x => x.Alive && !x.IsButler && x.Id != inc.Culprit && x.Id != inc.Victim).OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                double s = (S.HasRel(inc.Culprit, x.Id) ? S.R(inc.Culprit, x.Id).Grudge + Math.Max(0, -S.R(inc.Culprit, x.Id).Like) : 0) + (S.HasRel(x.Id, inc.Victim) ? S.R(x.Id, inc.Victim).Grudge : 0) + (x.IsPlayer ? 0.2 : 0);
                if (s > bs) { bs = s; best = x.Id; }
            }
            return best;
        }

        static string FrameCase(GameState S, Scheme sc, Incident inc)
        {
            string sg = N(sc.Scapegoat); var it = S.I(sc.FrameItem);
            switch (sc.Frame)
            {
                case "token": return K($"현장에 {sg}의 {it?.Def?.Kor ?? "물건"}이(가) 떨어져 있었잖아요.");
                case "weapon": return K($"흉기가 {sg}이(가) 늘 쓰던 {ItemCatalog.Get(sc.WeaponType)?.Kor ?? "물건"}이에요.");
                case "summon": return K($"{sg}도 그 시각에 혼자 {S.RoomName(sc.KillRoom)} 근처에 있었어요.");
                case "note": return K($"{N(sc.Victim)}을(를) 불러낸 쪽지에 {sg}의 이름이 적혀 있었어요.");
                case "vial": return K($"독병이 {sg}의 자리 근처에서 나왔어요.");
                case "rumor": return K($"{sg}이(가) {N(sc.Victim)}을(를) 원망했다는 얘기, 다들 들었잖아요.");
            }
            return K($"{sg}이(가) 제일 수상해요.");
        }

        // ------------------------------------------------------------------ prepared lies, each with what breaks it
        static void FillLies(GameState S, TrialPack p, Incident inc, MurderPlan plan, Scheme sc)
        {
            string me = inc.Culprit; string v = N(inc.Victim); int n = 0;
            PackLie L(string topic, string text, string truth, int cost, bool prepared) { var l = new PackLie { Id = "lie" + (++n), Topic = topic, Text = text, Truth = truth, Cost = cost, Prepared = prepared }; p.Lies.Add(l); return l; }
            double kt = p.Truth.KillClock; string kroom = S.RoomName(p.Truth.KillRoom);
            // where
            var w = L("where", p.Story.ClaimText, K($"{T(kt)}에 {kroom}에서 {v}을(를) 죽였다."), 1, sc != null);
            foreach (var e in S.Ledger.Where(e => e.Actor == me && (e.Type == "GatheringLeave" || e.Type == "Excuse") && Math.Abs(e.Clock - kt) < 40)) w.BrokenBy.Add("ledger:" + e.Seq);
            foreach (var x in S.Actors.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                if (!S.Know.TryGetValue(x.Id, out var kx) || x.Id == me) continue;
                if (kx.Sightings.Any(s => s.Target == me && s.Room == p.Truth.KillRoom && Math.Abs(s.T0 - kt) < 25 && s.IdConf > 0.4f)) w.BrokenBy.Add("witness:" + x.Id);
                else if (kx.Facts.Any(f => f.StartsWith($"left-gathering:{me}:") || f.StartsWith($"left-table:{me}:"))) w.BrokenBy.Add("witness:" + x.Id);
                // seen anywhere but the claimed room while the claim runs (a hunter out of their zone, a walk through the hall)
                else if (p.Story.ClaimRoom >= 0 && kx.Sightings.Any(s => s.Target == me && s.Room != p.Story.ClaimRoom && s.IdConf > 0.4f && s.Disguise == null && s.T0 <= p.Story.ClaimTo - 1 && s.T1 >= p.Story.ClaimFrom + 1)) w.BrokenBy.Add("witness:" + x.Id);
            }
            if (sc != null && sc.ClockF >= 0) w.BrokenBy.Add("furniture:" + sc.ClockF);
            if (sc != null && sc.Garb != null) w.BrokenBy.Add("item:" + sc.Garb);
            foreach (var t in S.Traces.Where(t => t.Source == me && Math.Abs(t.Clock - kt) < 30)) { w.BrokenBy.Add("trace:" + t.Id); if (w.BrokenBy.Count > 8) break; }
            // errand
            if (sc != null && sc.Approach == "errand")
            {
                var l = L("errand", K($"{v}한테 {sc.Pretext} 좀 가져다 달라고 한 건 맞아요. 하지만 따라가진 않았어요."), K($"보내 놓고 몇 분 뒤 뒤따라가 {kroom}에서 기다렸다."), 2, true);
                foreach (var e in S.Ledger.Where(e => e.Type == "Errand" && e.Actor == me)) l.BrokenBy.Add("ledger:" + e.Seq);
                if (sc.WeaponStashF >= 0) l.BrokenBy.Add("furniture:" + sc.WeaponStashF);
            }
            // the weapon / the poison
            if (p.Truth.Weapon != null)
            {
                var wi = S.I(p.Truth.Weapon); string wn = wi?.Def?.Kor ?? ItemCatalog.Get(p.Truth.WeaponType)?.Kor ?? "그것";
                var l = L("weapon", K($"{wn}? 저는 손도 대지 않았어요. 어디 있는지도 몰랐어요."), K($"{wn}을(를) 미리 챙겨 두었다."), 1, sc != null);
                if (sc != null) foreach (var pr in sc.Prep.Where(x => (x.Kind == "obtain" || x.Kind == "poison" || x.Kind == "stash") && x.Beat != null)) { var b = S.Mur.Beats.FirstOrDefault(bb => bb.Id == pr.Beat); if (b != null && b.Observers.Count > 0) l.BrokenBy.Add("beat:" + b.Id); }
                if (wi != null) l.BrokenBy.Add("item:" + wi.Id);
            }
            // the relationship (motive)
            {
                var l = L("relation", K($"{v}하고는 아무 일도 없었어요. 오히려 잘 지냈죠."), K($"동기: {p.Truth.MotiveText}" + (p.Truth.Trigger != null ? " — " + p.Truth.Trigger : "")), 1, sc != null);
                if (S.HasRel(me, inc.Victim) && S.R(me, inc.Victim).Grudge > 0.2f) l.BrokenBy.Add("relation:" + me + ">" + inc.Victim);
                foreach (var x in S.Know.Where(kv => kv.Value.Facts.Contains("knows-secret-of:" + me) || kv.Value.Facts.Any(f => f.StartsWith("threat:" + me))).Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal)) l.BrokenBy.Add("witness:" + x);
                if (sc?.Protects != null) l.BrokenBy.Add("relation:" + me + ">" + sc.Protects);
            }
            // the scapegoat (a false sighting that points at them)
            if (p.Story.Scapegoat != null)
            {
                var sgA = S.A(p.Story.Scapegoat);
                var l = L("saw", K($"{T(kt - 10)}쯤 {N(p.Story.Scapegoat)}이(가) {kroom} 쪽으로 가는 걸 봤어요."), K($"{N(p.Story.Scapegoat)}은(는) 범행과 상관없다" + (sc?.Frame == "summon" || sc?.Frame == "note" ? " — 범인이 그 근처로 불러냈을 뿐." : ".")), 2, sc?.Scapegoat != null);
                if (sgA != null) foreach (var x in S.Actors.Values.OrderBy(x => x.Id, StringComparer.Ordinal)) { if (x.Id == me || !S.Know.TryGetValue(x.Id, out var kx)) continue; if (kx.Sightings.Any(s => s.Target == sgA.Id && Math.Abs(s.T0 - kt) < 20 && s.Room != p.Truth.KillRoom && s.IdConf > 0.5f)) l.BrokenBy.Add("witness:" + x.Id); }
                if (sc != null) foreach (var id in sc.Planted) l.BrokenBy.Add("item:" + id);
                if (sc != null) foreach (var pr in sc.Prep.Where(x => (x.Kind == "token" || x.Kind == "rumor") && x.Beat != null)) l.BrokenBy.Add("beat:" + pr.Beat);
            }
            // the time (a clock put wrong in the meeting room)
            if (sc != null && sc.Alibi == "clock")
            {
                var l = L("time", K($"{N(sc.AlibiWitness)}을(를) 만난 건 {T(sc.AlibiAt + sc.ClockShift)}이었어요. 그 방 시계를 똑똑히 봤어요."), K($"그 방 시계는 범인이 {Math.Abs(sc.ClockShift):0}분 늦춰 두었다. 실제로는 {T(sc.AlibiAt)}."), 2, true);
                l.BrokenBy.Add("furniture:" + sc.ClockF); l.BrokenBy.Add("witness:" + sc.AlibiWitness);
                foreach (var pr in sc.Prep.Where(x => x.Kind == "clock" && x.Beat != null)) l.BrokenBy.Add("beat:" + pr.Beat);
            }
            if (sc != null && sc.Approach == "serve")
            {
                var l = L("serve", K($"잔은 다들 알아서 따라 마셨어요. {v}의 잔은 제가 손댄 적 없어요."), K($"{v}의 잔을 직접 채워 주며 독을 탔다."), 2, true);
                foreach (var b in S.Mur.Beats.Where(b => b.Scheme == sc.Id && b.Kind == "serve")) l.BrokenBy.Add("beat:" + b.Id);
                foreach (var e in S.Ledger.Where(e => e.Type == "SawNearCup" && e.Target == me)) l.BrokenBy.Add("ledger:" + e.Seq);
            }
            if (sc != null && sc.MarkItem != null)
            {
                var mi = S.I(sc.MarkItem);
                var l = L("gift", K($"그 {mi?.Def?.Kor ?? "선물"}은(는) 그냥 고마워서 준 거예요. 아무 뜻도 없었어요."), K("어둠 속에서 표적을 찾으려고 건넨 표식이었다."), 1, true);
                foreach (var b in S.Mur.Beats.Where(b => b.Scheme == sc.Id && b.Kind == "mark")) l.BrokenBy.Add("beat:" + b.Id);
                if (mi != null) l.BrokenBy.Add("item:" + mi.Id);
            }
            if (sc != null && sc.Helper != null)
            {
                var l = L("lights", K("불이 왜 나갔는지는 저도 몰라요."), K($"{N(sc.Helper)}에게 그 시각에 불을 내려 달라고 부탁해 두었다."), 2, true);
                l.BrokenBy.Add("witness:" + sc.Helper);
            }
            foreach (var l in p.Lies) { var dist = l.BrokenBy.Distinct().ToList(); l.BrokenBy.Clear(); l.BrokenBy.AddRange(dist); }
        }

        // ------------------------------------------------------------------ rule shields (prepared + detected)
        static void FillShields(GameState S, TrialPack p, Incident inc, MurderPlan plan, Scheme sc)
        {
            var have = new HashSet<string>();
            void Add(string rule, string tactic, bool prepared)
            {
                if (!have.Add(rule + ":" + tactic)) return;
                var r = new RuleShield { Rule = rule, Tactic = tactic, Prepared = prepared };
                string v = N(inc.Victim);
                switch (tactic)
                {
                    case "second-killer":
                        r.Argument = K("규칙 여섯을 떠올려 보세요. 사건이 여럿이면 심판이 가리는 건 가장 먼저 일부러 목숨을 앗은 한 사람이에요. 설령 제가 뒤의 일에 얽혀 있다 해도, 첫 사건과는 아무 관계가 없어요.");
                        r.When = "두 번째 사건의 범인으로 몰릴 때 / 두 사건을 같은 범인으로 묶으려 할 때"; r.Counter = "두 사건의 시각·수법·동기가 다르다는 사실 — 첫 사건의 범인은 따로 있다"; break;
                    case "not-deliberate":
                        r.Argument = K($"규칙은 '일부러' 목숨을 앗은 사람을 가리라고 했어요. {v}의 죽음은 사고였어요. 일부러 죽인 사람이 없는데 누구를 지목하겠다는 거예요?");
                        r.When = "사고·자연사로 보이는 첫인상이 흔들릴 때"; r.Counter = "사고로는 생기지 않는 흔적 (밀친 멍, 눌린 베개, 개인 물건 속의 독)"; break;
                    case "order-swap":
                        r.Argument = K($"{v}이(가) 죽은 건 다른 사건보다 뒤예요. 그러니 이번 심판의 물음은 제 쪽이 아니에요.");
                        r.When = "사건이 둘 이상이고 사망 시각이 다투어질 때"; r.Counter = "조작된 사망 시각을 깨는 흔적 (냉기·물기·온기)"; break;
                    case "dead-scapegoat":
                        { var d = Initiative.DeadScapegoat(S, inc.Culprit, inc.Victim); r.Argument = K($"규칙 일곱 — 돌아가신 분도 지목할 수 있어요. {(d != null ? N(d) + "은(는) " + v + "을(를) 누구보다 미워했어요. 범인은 이미 세상에 없는 사람일 수도 있어요." : "범인은 이미 죽은 사람일 수도 있어요.")}");
                          r.When = "살아 있는 희생양이 무너졌을 때"; r.Counter = "죽은 사람이 그 시각에 이미 숨져 있었거나 움직일 수 없었다는 사실"; break; }
                    case "first-in":
                        r.Argument = K("제 소매에 묻은 건 시신을 처음 발견했을 때 묻은 거예요. 세 사람이 봐야 안내가 울리잖아요. 저도 그 세 사람 중 하나였을 뿐이에요.");
                        r.When = "몸에 남은 흔적(피, 물기, 긁힘)을 추궁당할 때"; r.Counter = "발견 전에 생긴 흔적 — 발견 시각보다 이른 목격, 이미 말라 버린 얼룩"; break;
                    case "delay":
                        r.Argument = K($"{v}이(가) 발견된 건 한참 뒤예요. 그사이 누가 드나들었는지 어떻게 알아요? 사망 시각은 넓게 봐야 해요.");
                        r.When = "사망 시각을 좁히려 할 때"; r.Counter = "시신을 본 뒤 멈춘 시계, 식지 않은 차, 마지막 목격"; break;
                    case "house-dark":
                        r.Argument = K("불은 저택이 끈 거예요. 누가 스위치에 손을 댄 게 아니라고요. 그 어둠 속에서 뭘 봤다는 사람이 있다면, 그게 더 이상하죠.");
                        r.When = "어둠 속의 움직임을 추궁당할 때"; r.Counter = "어둠 속에서도 남는 것 — 소리, 스친 옷감, 향, 불이 켜진 뒤 바뀐 자리"; break;
                    case "noise":
                        r.Argument = K("그 소음 속에서 비명을 들었다는 게 말이 돼요? 아무도 못 들었으니 시각은 아무도 몰라요.");
                        r.When = "들린 소리를 증거로 댈 때"; r.Counter = "소음이 잦아든 순간의 기록"; break;
                    case "wing":
                        r.Argument = K("저는 다른 날개에서 지냈어요. 그쪽엔 갈 수가 없었다고요.");
                        r.When = "동선을 추궁당할 때"; r.Counter = "수사 중에는 양익 제한이 풀린다는 규칙 문구"; break;
                    case "closed-room":
                        r.Argument = K("그 시각 그 방은 정비로 닫혀 있었어요. 아무도 들어갈 수 없었어요.");
                        r.When = "현장 출입을 추궁당할 때"; r.Counter = "정비 시간이 끝난 시각과 문 기록"; break;
                    case "sealed-statement":
                        r.Argument = K("제 첫 진술은 봉인돼 있어요. 한 글자도 바꾼 적 없어요. 말을 바꾼 건 저쪽이에요.");
                        r.When = "진술의 일관성을 공격받을 때"; r.Counter = "봉인된 진술 자체가 사실과 어긋난다는 증거"; break;
                    case "inquiry":
                        r.Argument = K("공개 질의 때 모두 앞에서 말한 그대로예요. 그때 아무도 반박하지 않았잖아요.");
                        r.When = "알리바이를 다시 따질 때"; r.Counter = "공개 질의 뒤에 드러난 흔적"; break;
                    case "night-lock":
                        r.Argument = K("밤 10시가 넘으면 그 방은 저택이 잠가요. 그 뒤로는 아무도 못 들어갔어요. 저는 그 전에 거실에 있었고요.");
                        r.When = "현장 출입 시각을 추궁당할 때"; r.Counter = "잠기기 전, 빈 방이 되기 직전까지 누가 있었는지 — 잠금 기록의 시각"; break;
                }
                p.Shields.Add(r);
            }
            if (sc != null) foreach (var s in sc.Shields) { int i = s.IndexOf(':'); if (i > 0) Add(s.Substring(0, i), s.Substring(i + 1), true); }
            // detected: rule 여섯 for a second killing; staged grammars; a shifted time of death with another incident
            if (p.Order >= 2) Add("y6", "second-killer", false);
            string head = Head(plan?.Grammar ?? inc.Method);
            if (Methods.Staged(head ?? "")) Add("y6", "not-deliberate", false);
            bool tod = plan?.Grammar != null && (plan.Grammar.Contains("+Tod") || plan.Grammar.Contains("+ColdHide"));
            if (tod && S.Incidents.Values.Count(i => i.Loop == inc.Loop && i.Chapter == inc.Chapter && i.Murder) >= 2) Add("y6", "order-swap", false);
            if (Initiative.DeadScapegoat(S, inc.Culprit, inc.Victim) != null) Add("y7", "dead-scapegoat", false);
            if (inc.Discoverers.Contains(inc.Culprit)) Add("y2", "first-in", false);
            if (S.Rule("CH03") != null && S.Rule("CH03").Times.Any(t => p.Truth.KillClock >= t && p.Truth.KillClock <= t + 30)) Add("CH03", "house-dark", false);
        }

        // ------------------------------------------------------------------ fallback stories: bend before breaking
        static void FillFallbacks(GameState S, TrialPack p, Incident inc, MurderPlan plan, Scheme sc)
        {
            string v = N(inc.Victim); string kroom = S.RoomName(p.Truth.KillRoom); int o = 0;
            void F(string trigger, string story, string concedes, string keeps) => p.Fallbacks.Add(new FallbackStory { Order = ++o, Trigger = trigger, Story = K(story), Concedes = concedes, Keeps = keeps });
            if (sc != null && (sc.Approach == "errand" || sc.Approach == "slip-out"))
                F("자리를 비운 사실이 드러날 때", $"잠깐 자리를 뜬 건 맞아요. {(sc.Approach == "errand" ? v + "이(가) 너무 안 와서 찾으러 갔는데, 못 찾고 돌아왔어요." : "바람 좀 쐬러 나갔을 뿐이에요.")}", "자리를 비웠다", "현장에는 가지 않았다");
            F("현장 근처에 있었다는 증언이 나올 때", $"{kroom} 근처에 간 건 맞아요. 하지만 그때 {v}은(는) 거기 없었어요. 아니, 이미 쓰러져 있었어요. 겁이 나서 말을 못 했어요.", "현장에 갔다", "죽이지 않았다");
            if (p.Truth.Weapon != null)
                F("흉기와의 연결이 드러날 때", $"그 {ItemCatalog.Get(p.Truth.WeaponType)?.Kor ?? "물건"}을(를) 만진 적은 있어요. 누구나 만질 수 있는 물건이었어요. {(p.Story.Scapegoat != null ? N(p.Story.Scapegoat) + "도 만지는 걸 봤어요." : "")}", "흉기를 만졌다", "그것으로 죽이지 않았다");
            if (p.Story.Scapegoat != null)
            {
                var d = Initiative.DeadScapegoat(S, inc.Culprit, inc.Victim);
                F($"{N(p.Story.Scapegoat)}의 결백이 드러날 때", d != null ? $"그럼 {N(d)}이에요. 죽은 사람도 지목할 수 있잖아요. {N(d)}은(는) {v}을(를) 미워했어요." : "그럼 제가 잘못 본 거겠죠. 그 어둠 속에서, 그 소란 속에서 누가 정확히 봤겠어요.", "희생양을 내려놓는다", "자신은 아니다");
            }
            // the last line, by who they are
            string m = sc?.Motive ?? inc.Method;
            if (sc != null && (sc.Motive == "defense" || sc.Has("turnabout")))
                F("모든 거짓이 무너질 때", $"먼저 덤빈 건 {v}이에요. {v}이(가) 저를 죽이려고 준비하는 걸 봤어요. 저는 살려고 한 것뿐이에요.", "죽였다", "정당방위였다");
            else if (Methods.Staged(Head(plan?.Grammar ?? "") ?? ""))
                F("사고라는 주장이 무너질 때", "손을 댄 건 맞아요. 하지만 죽이려던 건 아니었어요. 일부러가 아니었다고요. 규칙은 '일부러'라고 했잖아요.", "손을 댔다", "고의가 아니었다");
            else if (sc != null && (sc.Has("second-killer") || sc.Has("copycat") || p.Order >= 2))
                F("두 번째 사건의 범인임이 드러날 때", "그래요, 뒤의 일은 제가 했어요. 하지만 이번 심판이 가리는 건 첫 번째 사건이에요. 첫 사건의 범인은 따로 있어요.", "두 번째 살인을 했다", "첫 사건과는 무관하다 (규칙 여섯)");
            else if (sc != null && (sc.Motive == "protect" || sc.Motive == "love") && sc.Protects != null)
                F("동기가 드러날 때", $"{N(sc.Protects)}을(를) 지키려면 그 방법밖에 없었어요.", "죽였다", "그럴 수밖에 없었다");
            else
                F("모든 거짓이 무너질 때", "…다 아는 척하지 마세요. 당신들은 아무것도 몰라요.", "침묵", "끝까지 인정하지 않는다");
        }

        static void FillRoles(GameState S, TrialPack p, Incident inc, MurderPlan plan, Scheme sc)
        {
            if (p.Story.Scapegoat != null) p.Roles.Add(new PackRole { Role = "scapegoat", Actor = p.Story.Scapegoat, Did = p.Story.ScapegoatCase });
            if (sc == null) return;
            if (sc.Helper != null) p.Roles.Add(new PackRole { Role = "helper", Actor = sc.Helper, Did = K($"{T(sc.MomentAt)} 무렵 부탁받은 대로 불을 내렸다 (무엇에 쓰였는지 모른다)") });
            if (sc.AlibiWitness != null) p.Roles.Add(new PackRole { Role = "witness", Actor = sc.AlibiWitness, Did = K($"{S.RoomName(sc.AlibiRoom)}에서 {T(sc.AlibiAt)}에 만나자는 약속을 받았다" + (sc.Alibi == "clock" ? " — 그 방 시계로 시각을 기억한다" : "")) });
            foreach (var g in sc.Guests) if (g != sc.Victim && g != sc.Helper) p.Roles.Add(new PackRole { Role = "guest", Actor = g, Did = K($"{sc.EventLabel}에 초대받았다") });
            if (sc.CopyOf != null && S.Incidents.TryGetValue(sc.CopyOf, out var ci)) p.Roles.Add(new PackRole { Role = "copied", Actor = ci.Culprit, Did = K($"{N(ci.Victim)} 사건을 흉내 냈다") });
            var vs = S.Mur.Schemes.LastOrDefault(x => x.Culprit == inc.Victim && x.Victim == inc.Culprit && x.Loop == inc.Loop);
            if (vs != null) p.Roles.Add(new PackRole { Role = "victim-plan", Actor = inc.Victim, Did = K($"{N(inc.Victim)} 역시 {N(inc.Culprit)}을(를) 노리고 {Initiative.MomentText(vs)}을(를) 준비하고 있었다") });
            foreach (var x in S.Actors.Values.Where(x => x.Alive && !x.IsButler && x.Id != inc.Culprit && S.HasRel(x.Id, inc.Culprit) && (S.R(x.Id, inc.Culprit).Attach > 0.55f || S.R(x.Id, inc.Culprit).Tags.Contains("family"))).OrderBy(x => x.Id, StringComparer.Ordinal))
                if (sc.Beats.Any(b => S.Mur.Beats.FirstOrDefault(bb => bb.Id == b)?.Observers.Contains(x.Id) == true)) p.Roles.Add(new PackRole { Role = "protector", Actor = x.Id, Did = K($"{N(inc.Culprit)}의 준비를 봤지만 감쌀 것이다") });
        }

        static string Logline(GameState S, TrialPack p, Incident inc, Scheme sc)
        {
            string c = N(inc.Culprit), v = N(inc.Victim);
            if (sc == null) return K($"{c}은(는) {S.RoomName(p.Truth.KillRoom)}에서 {v}을(를) 죽였다 — 준비 없이, 눈앞의 기회에.");
            string frame = sc.Scapegoat != null ? $" 의심은 처음부터 {N(sc.Scapegoat)}에게 가도록 짜 두었다." : "";
            string shield = sc.Shields.Any(x => x.StartsWith("y6:second")) ? " 심판은 첫 사건만 가린다는 규칙을 방패로 삼았다." : sc.Shields.Any(x => x.StartsWith("y2:first")) ? " 시신은 스스로 '발견'했다." : "";
            return K($"{c}은(는) {Initiative.MomentText(sc)}에서 {Initiative.ApproachText(sc, S)} {v}을(를) 죽였다.{frame}{shield}");
        }

        // ================================================================== a readable narrative (lab / reveal / debugging)
        public static string Narrative(GameState S, string incidentId)
        {
            var p = TrialPack(S, incidentId); if (p == null) return null;
            var inc = S.Incidents[incidentId]; var sb = new StringBuilder(); var t = p.Truth;
            sb.AppendLine($"### {N(t.Victim)} 사건 — 범인 {N(t.Culprit)} ({(p.Improvised ? "즉흥" : "계획")}, 챕터 {inc.Chapter}의 {p.Order}번째 살인{(p.Judged ? ", 심판 대상" : ", 심판 대상 아님")})");
            sb.AppendLine();
            sb.AppendLine("> " + p.Logline);
            sb.AppendLine();
            sb.AppendLine($"- **동기**: {t.MotiveText}" + (t.Trigger != null ? $" — {t.Trigger}" : ""));
            sb.AppendLine($"- **기회**: {t.MomentText}" + (t.Hosted ? $" (범인이 직접 연 자리, 손님: {string.Join("·", t.Guests.Select(N))})" : ""));
            sb.AppendLine($"- **수법**: {t.Approach} → 계획 `{t.Method}` · 흉기 {ItemCatalog.Get(t.WeaponType)?.Kor ?? "-"} · 범행 {ClockFmt.DayHM(t.KillClock)} {S.RoomName(t.KillRoom)} · 발견 {(t.FoundClock >= 0 ? ClockFmt.DayHM(t.FoundClock) + " " + S.RoomName(t.FoundRoom) : "아직")}");
            if (t.Variants.Count > 0) sb.AppendLine($"- **변주**: {string.Join(", ", t.Variants)}");
            if (t.Tricks.Count > 0) sb.AppendLine($"- **실행된 층**: {string.Join(", ", t.Tricks)}");
            if (t.Preparations.Count > 0)
            {
                sb.AppendLine("- **준비** (일상 속 복선):");
                foreach (var pr in t.Preparations) sb.AppendLine($"  - [{(pr.Done ? "x" : " ")}] {pr.Kind}: {pr.Text}" + (pr.Clock >= 0 ? $" ({ClockFmt.DayHM(pr.Clock)})" : "") + (pr.SeenBy.Count > 0 ? $" — 본 사람: {string.Join("·", pr.SeenBy.Select(N))}" : " — 아무도 못 봤다"));
            }
            sb.AppendLine($"- **내세울 이야기**: \"{p.Story.Account}\"");
            sb.AppendLine($"- **밀어붙일 가설**: {p.Story.Theory}" + (p.Story.Scapegoat != null ? $" (희생양: {N(p.Story.Scapegoat)})" : ""));
            if (p.Lies.Count > 0) { sb.AppendLine($"- **준비된 거짓말** (예산 {p.LieBudget}):"); foreach (var l in p.Lies) sb.AppendLine($"  - [{l.Topic}] \"{l.Text}\" ↔ 진실: {l.Truth} — 깨는 것: {(l.BrokenBy.Count > 0 ? string.Join(", ", l.BrokenBy.Take(5)) : "(없음)")}"); }
            if (p.Shields.Count > 0) { sb.AppendLine("- **규칙 방패**:"); foreach (var r in p.Shields) sb.AppendLine($"  - {r.Rule}/{r.Tactic}{(r.Prepared ? " (계획 단계부터)" : " (감지)")}: \"{r.Argument}\" — 쓸 때: {r.When} / 깨는 것: {r.Counter}"); }
            if (p.Fallbacks.Count > 0) { sb.AppendLine("- **물러설 자리**:"); foreach (var f in p.Fallbacks) sb.AppendLine($"  {f.Order}. ({f.Trigger}) \"{f.Story}\" — 인정: {f.Concedes} / 지킴: {f.Keeps}"); }
            if (p.Roles.Count > 0) sb.AppendLine("- **역할**: " + string.Join("; ", p.Roles.Where(r => r.Role != "guest").Select(r => $"{r.Role} {N(r.Actor)} — {r.Did}")));
            if (p.Log.Count > 0) { sb.AppendLine("- **범인의 시간표**:"); foreach (var l in p.Log) sb.AppendLine("  - " + l); }
            return sb.ToString();
        }
    }
}
