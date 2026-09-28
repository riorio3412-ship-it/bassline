using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>The truth of one case, read once when the 심판 opens (never saved). Only the builder and the director read it.</summary>
    public sealed class CaseFacts
    {
        public Simulation Sim; public GameState S; public Incident Inc; public TrialPack Pack;
        public Actor Victim; public string VictimId, Culprit, Trick, Scapegoat;
        public int KillRoom, FoundRoom; public double KillClock, DeathClock, FoundClock, W0, W1;
        public List<string> People = new List<string>();   // everyone at a stand (player included), ordinal
        public List<string> Npcs = new List<string>();     // the residents who speak (no player, no butler), ordinal
    }

    /// <summary>
    /// The 은판 deck (TrialReforge §2), v0 feeders: the case as the kernel recorded it (body, traces, furniture changes, items,
    /// what people saw and heard) plus the culprit's TrialPack (alibi story, planted things). Fakes are true photographs
    /// whose first reading is wrong; a fake with no route to flip it, or nobody who would lean on it, is left out.
    /// </summary>
    public static partial class Debate
    {
        public static CaseFacts Facts(Simulation sim, Incident inc)
        {
            var S = sim.S;
            var C = new CaseFacts { Sim = sim, S = S, Inc = inc, VictimId = inc.Victim, Victim = S.A(inc.Victim), Culprit = inc.Culprit };
            C.Pack = inc.Murder && inc.Culprit != null ? CaseApi.TrialPack(S, inc.Id) : null;
            C.DeathClock = inc.DeathClock >= 0 ? inc.DeathClock : inc.CauseClock >= 0 ? inc.CauseClock : S.Clock;
            C.KillClock = inc.CauseClock >= 0 ? inc.CauseClock : C.DeathClock;
            C.KillRoom = inc.CauseRoom >= 0 ? inc.CauseRoom : inc.DeathRoom >= 0 ? inc.DeathRoom : inc.FoundRoom;
            C.FoundRoom = inc.FoundRoom >= 0 ? inc.FoundRoom : C.KillRoom;
            C.FoundClock = inc.DiscoverClock >= 0 ? inc.DiscoverClock : inc.ConfirmClock >= 0 ? inc.ConfirmClock : S.Clock;
            C.W0 = C.KillClock - 150; C.W1 = C.FoundClock;
            C.Trick = TrialSystem.ExecutedTrick(S, inc);
            C.Scapegoat = C.Pack?.Story?.Scapegoat;
            var present = S.Trial?.Participants ?? S.Living.Select(a => a.Id).ToList();
            C.People = present.Where(id => S.A(id) != null && S.A(id).Alive && !S.A(id).IsButler).OrderBy(id => id, StringComparer.Ordinal).ToList();
            C.Npcs = C.People.Where(id => id != Cast.Player).ToList();
            return C;
        }

        // ------------------------------------------------------------------ build
        public static CaseDeck BuildDeck(CaseFacts C)
        {
            var S = C.S; var inc = C.Inc;
            var d = new CaseDeck { Incident = inc.Id, Loop = S.Loop, Chapter = S.Chapter, Frozen = S.Clock, FrozenSeq = S.Seq, Source = C.Pack != null ? "pack,legacy" : "legacy" };
            double u = MurderHash.U01(S, "deck-fakes:" + inc.Id); d.FakeTarget = u < 0.25 ? 4 : u < 0.75 ? 5 : 6;
            var cands = new List<Plate>();
            FeedBody(C, cands); FeedTraces(C, cands); FeedFurniture(C, cands); FeedItems(C, cands); FeedWitness(C, cands); FeedAlibi(C, cands); FeedCoincidence(C, cands); FeedHeld(C, cands);
            BuildClaims(C, d, cands);
            LinkPlates(C, d, cands);
            Select(C, d, cands);
            Validate(C, d);
            return d;
        }

        static Plate NewPlate(CaseFacts C, string root, PlateKind kind, PlateRole role, bool truth, string title, string face, int room, double t0, double t1, IEnumerable<Prop> props)
        {
            var p = new Plate { Id = "pl:" + C.Inc.Id + ":" + root, Root = root, Kind = kind, Role = role, True = truth, Title = title, Face = LineBank.FixParticles(face), Room = room, T0 = t0, T1 = t1 };
            if (props != null) p.Props.AddRange(props.Where(x => x != null));
            p.Channel = kind == PlateKind.Body ? Channel.Body : kind == PlateKind.Witness ? Channel.Witness : kind == PlateKind.Record ? Channel.Record : kind == PlateKind.Object ? Channel.Object : Channel.Scene;
            return p;
        }

        static string G(string id) => Cast.GivenOf(id) ?? id;
        static string Clean(string line) => line == null ? null : line.TrimStart('·', ' ').Trim();

        // ---- the body (always; Yusti's record gives it to everyone). A thorough look, as the House's discovery photograph shows it.
        static void FeedBody(CaseFacts C, List<Plate> cands)
        {
            var S = C.S; var v = C.Victim; if (v == null) return;
            var main = v.Body.Wounds.Where(w => !w.Postmortem).OrderByDescending(w => w.Sev).ThenBy(w => w.Tick).FirstOrDefault();
            var props = new List<Prop> { new Prop { Kind = PropKind.FoundPlace, A = v.Id, Room = C.FoundRoom, T0 = C.FoundClock, T1 = C.FoundClock } };
            string wound = "눈에 띄는 상처는 없다";
            if (main != null)
            {
                props.Add(new Prop { Kind = PropKind.WeaponType, A = v.Id, Value = main.Type.ToString() });
                props.Add(new Prop { Kind = PropKind.Wound, A = v.Id, Value = WoundText.Region(main.Region) + "에 " + WoundText.Type(main.Type), Item = main.Type.ToString() });
                wound = WoundText.Region(main.Region) + "에 " + (Violence.Phrase(main) ?? WoundText.Type(main.Type));
            }
            // what a careful look shows (temperature, an instant death, the hand that wrote, a ligature …): a local generator, no saved state touched
            var lines = new List<string>(); var rng = new Rng(Rng.Hash("deck-body:" + C.Inc.Id), 11);
            if (S.Player != null) SetPieces.BodyNotes(C.Sim, S.Player, v, 1f, rng, lines, props);
            if (v.Body.PoisonBy != null && main == null) wound = "입술과 손끝이 푸르다";
            string face = $"{S.RoomName(C.FoundRoom)}, {G(v.Id)}. {wound}. {ClockFmt.Anchor(C.FoundClock)} 발견.";
            var p = NewPlate(C, "body:" + v.Id, PlateKind.Body, PlateRole.Body, true, G(v.Id) + "의 시신", face, C.FoundRoom, C.FoundClock, C.FoundClock, props);
            p.Back = lines.Select(Clean).FirstOrDefault(l => l != null && (l.Contains("즉사") || l.Contains("따뜻") || l.Contains("차갑") || l.Contains("손가락") || l.Contains("자국") || l.Contains("멍"))) ?? $"상처는 {wound}.";
            p.Owner = null; cands.Add(p);
        }

        static readonly Dictionary<string, string> TraceTitles = new Dictionary<string, string>
        {
            ["BloodWriting"] = "피로 쓴 글자", ["DragMark"] = "끌린 자국", ["BloodSmear"] = "번진 핏자국", ["Struggle"] = "몸싸움 흔적", ["Debris"] = "쓰러진 가구와 파편",
            ["ThreadFiber"] = "문틈의 실오라기", ["BulletHole"] = "총알 자국", ["Scratch"] = "긁힌 자국", ["Water"] = "물기", ["FootprintWet"] = "젖은 발자국",
            ["Ash"] = "재", ["Soil"] = "흙", ["Scuff"] = "쓸린 자국", ["PowerResidue"] = "그을음", ["Fragment"] = "흩어진 파편", ["BloodPool"] = "고인 피"
        };

        // ---- traces at the scene, on the route, or left by the victim; the fake dying message is a Frame
        static void FeedTraces(CaseFacts C, List<Plate> cands)
        {
            var S = C.S;
            foreach (var t in S.Traces.OrderBy(t => t.Seq))
            {
                if (t.Cleaned || t.Visibility > 2 || t.Type == "SwitchTouched") continue;
                bool atScene = (t.Room == C.KillRoom || t.Room == C.FoundRoom) && t.Clock >= C.W0 && t.Clock <= C.W1 + 5;
                bool route = t.Source != null && t.Source == C.Culprit && t.Clock >= C.W0 - 60 && t.Clock <= C.W1;
                if (!(t.Victim == C.VictimId || atScene || route)) continue;
                var props = new List<Prop> { new Prop { Kind = PropKind.TraceAt, A = t.Victim, Room = t.Room, T0 = t.Clock, T1 = t.Clock, Value = t.Desc, Item = t.Type } };
                var lines = new List<string>(); if (S.Player != null) SetPieces.TraceNotes(C.Sim, S.Player, t, lines, props);
                string title = TraceTitles.TryGetValue(t.Type, out var tt) ? tt : Trim(t.Desc, 12);
                string face = $"{t.Desc}. ({S.RoomName(t.Room)})";
                bool fake = t.Type == "BloodWriting";
                var p = NewPlate(C, "trace:" + t.Id, PlateKind.Trace, fake ? PlateRole.Frame : PlateRole.Seam, !fake, title, face, t.Room, t.Clock, t.Clock, props);
                if (fake)
                {
                    string target = NoteOf(t.Note, "target"); p.Points = target; p.Owner = target; p.Origin = "위장";
                    p.Title = "피로 쓴 글자"; p.Face = LineBank.FixParticles($"피로 쓴 글자 「{NoteOf(t.Note, "glyph")}」. {G(C.VictimId)} 손 옆 바닥.");
                    p.Back = LineBank.FixParticles($"{G(C.VictimId)}이(가) 쓴 글씨가 아니다 — 누군가 {G(target)}에게 뒤집어씌우려 했다.");
                }
                else p.Back = Clean(lines.FirstOrDefault()) ?? t.Know;
                cands.Add(p);
            }
        }

        // ---- furniture knocked over or dragged in the kill/found room (Sim/Systems/FurnitureChanges.cs)
        static void FeedFurniture(CaseFacts C, List<Plate> cands)
        {
            var S = C.S;
            foreach (var f in S.Layout.Furniture)
            {
                if (f.Rev == 0 || (f.Room != C.KillRoom && f.Room != C.FoundRoom)) continue;
                string dis = Simulation.Disorder(f); if (dis == null && !(f.Moved && f.Pos.DistXZ(f.Origin) > 0.6f)) continue;
                string kor = FurnitureCatalog.Get(f.Type)?.Kor ?? "가구";
                string how = dis == "toppled" ? "넘어져 있다" : dis == "rumpled" ? "밀려나 주름져 있다" : "원래 자리에서 밀려나 있다";
                var props = new List<Prop> { new Prop { Kind = PropKind.TraceAt, Room = f.Room, Value = "furniture:" + (dis ?? "moved"), Item = "Furniture" } };
                var p = NewPlate(C, "furn:" + f.Id, PlateKind.Fixture, PlateRole.Seam, true, (dis == "toppled" ? "넘어진 " : "밀려난 ") + kor, $"{kor}. {how}. ({S.RoomName(f.Room)})", f.Room, -1, -1, props);
                p.Back = LineBank.FixParticles($"{S.RoomName(f.Room)}에서 누군가 {kor}을(를) 밀치며 몸싸움을 했다.");
                cands.Add(p);
            }
        }

        // ---- the weapon; a decoy with blood only on its surface (Swap); things planted to point at someone (pack Story.Planted)
        static void FeedItems(CaseFacts C, List<Plate> cands)
        {
            var S = C.S; var planted = new HashSet<string>(C.Pack?.Story?.Planted ?? new List<string>());
            foreach (var it in S.Items.Values.OrderBy(i => i.Id, StringComparer.Ordinal))
            {
                bool weapon = it.Id == C.Inc.Weapon, decoy = it.Surface.Contains("smeared") && it.Room >= 0 && (it.Room == C.FoundRoom || it.Room == C.KillRoom), plant = planted.Contains(it.Id);
                if (!weapon && !decoy && !plant) continue;
                if (it.Holder != null || it.Hidden) continue;   // not where anyone could photograph it
                var def = it.Def; string kor = it.Kor;
                var props = new List<Prop> { new Prop { Kind = PropKind.ItemAt, A = it.Owner, Item = it.Type, Room = it.Room, Value = plant ? "moved" : "found" } };
                var lines = new List<string>(); if (S.Player != null) SetPieces.ItemNotes(C.Sim, S.Player, it, lines, props);
                string state = it.Bloody ? "피가 묻어 있다" : it.Washed ? "물에 씻긴 자국이 있다" : "제자리에 없다";
                if (decoy && !weapon)
                {
                    props.Add(new Prop { Kind = PropKind.WeaponType, A = C.VictimId, Value = def?.Dmg.ToString(), Item = it.Type });
                    var p = NewPlate(C, "item:" + it.Id, PlateKind.Object, PlateRole.Staged, false, "피 묻은 " + kor, $"{kor}. 피가 묻어 있다. ({S.RoomName(it.Room)}, 시신 가까이)", it.Room, -1, -1, props);
                    p.Points = "means"; p.Origin = "위장"; p.Back = LineBank.FixParticles($"피는 겉에만 발려 있다 — {kor}은(는) 흉기가 아니다."); cands.Add(p);
                }
                else if (plant && !weapon)
                {
                    props.Add(new Prop { Kind = PropKind.Culprit, A = it.Owner, B = C.VictimId, Value = "planted" });
                    var p = NewPlate(C, "item:" + it.Id, PlateKind.Object, PlateRole.Frame, false, LineBank.FixParticles($"{G(it.Owner)}의 {kor}"), $"{G(it.Owner)}의 {kor}. {S.RoomName(it.Room)}에 떨어져 있다.", it.Room, -1, -1, props);
                    p.Points = it.Owner; p.Owner = it.Owner; p.Origin = "위장"; p.Back = LineBank.FixParticles($"{G(it.Owner)}은(는) 이걸 잃어버렸을 뿐이다 — 누군가 여기 가져다 놓았다."); cands.Add(p);
                }
                else if (weapon)
                {
                    if (def != null) props.Add(new Prop { Kind = PropKind.WeaponType, A = C.VictimId, Value = def.Dmg.ToString(), Item = it.Type });
                    var p = NewPlate(C, "item:" + it.Id, PlateKind.Object, PlateRole.Link, true, kor, $"{kor}. {state}. ({S.RoomName(it.Room)})", it.Room, -1, -1, props);
                    p.Back = Clean(lines.FirstOrDefault()) ?? LineBank.FixParticles($"{kor}(으)로 {G(C.VictimId)}을(를) 쳤다."); cands.Add(p);
                }
            }
        }

        // ---- what people heard and saw around the kill time: the scream's time (true), the culprit seen near the scene (true Link),
        //      the scapegoat seen elsewhere (true Clear)
        static void FeedWitness(CaseFacts C, List<Plate> cands)
        {
            var S = C.S;
            foreach (var w in C.Npcs.Where(x => x != C.Culprit))
            {
                var k = S.K(w);
                var h = k.Heard.Where(x => IsAlarm(x.Kind) && Math.Abs(x.Clock - C.KillClock) <= 25 && (x.Room == C.KillRoom || x.GuessRoom == C.KillRoom))
                               .OrderByDescending(x => x.Loud).ThenBy(x => x.Clock).FirstOrDefault();
                if (h != null)
                {
                    var props = new[] { new Prop { Kind = PropKind.Heard, A = w, Value = h.Kind.ToString(), Room = h.GuessRoom, T0 = h.Clock, T1 = h.Clock } };
                    var p = NewPlate(C, "talk:" + w + ":heard", PlateKind.Witness, PlateRole.Confirm, true, $"{G(w)}이(가) 들은 것", $"{ClockFmt.Anchor(h.Clock)}, {S.RoomName(h.GuessRoom)} 쪽. {Simulation.SoundText(h.Kind)} 소리 — {G(w)}", h.GuessRoom, h.Clock, h.Clock, props);
                    p.Witness = w; p.Back = LineBank.FixParticles($"그 소리가 난 때가 {G(C.VictimId)}이(가) 공격당한 때다."); p.Title = LineBank.FixParticles(p.Title); cands.Add(p);
                }
                if (C.Culprit != null && w != C.Culprit)
                {
                    var s = k.Sightings.Where(x => x.Target == C.Culprit && x.IdConf >= 0.6f && x.Disguise == null && !x.Dead && (x.Room == C.KillRoom || x.Room == C.FoundRoom) && x.T1 >= C.KillClock - 40 && x.T0 <= C.KillClock + 20)
                                       .OrderBy(x => Math.Abs(x.T0 - C.KillClock)).FirstOrDefault();
                    if (s != null)
                    {
                        var props = new List<Prop> { new Prop { Kind = PropKind.AtPlace, A = C.Culprit, Room = s.Room, T0 = s.T0, T1 = s.T1 } };
                        if (s.Held != null) props.Add(new Prop { Kind = PropKind.Held, A = C.Culprit, Item = s.Held, Room = s.Room, T0 = s.T0, T1 = s.T1 });
                        string held = s.Held != null ? $" 손에 {ItemCatalog.Get(s.Held)?.Kor ?? s.Held}" : "";
                        var p = NewPlate(C, "talk:" + w + ":saw:" + C.Culprit, PlateKind.Witness, PlateRole.Link, true, $"{G(w)}이(가) 본 것", $"{ClockFmt.Anchor(s.T0)}, {S.RoomName(s.Room)}. {G(C.Culprit)}{held} — {G(w)}", s.Room, s.T0, s.T1, props);
                        p.Witness = w; p.Seen = C.Culprit; p.Title = LineBank.FixParticles(p.Title);
                        p.Back = LineBank.FixParticles($"그 무렵 {G(C.Culprit)}은(는) 현장에 있었다."); cands.Add(p);
                    }
                }
            }
            // the scapegoat (or whoever a Frame points at) seen elsewhere at the kill time
            foreach (var target in cands.Where(p => !p.True && p.Points != null && S.A(p.Points) != null).Select(p => p.Points).Concat(new[] { C.Scapegoat }).Where(x => x != null && x != C.Culprit).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList())
                foreach (var w in C.Npcs.Where(x => x != target && x != C.Culprit))
                {
                    var s = S.K(w).Sightings.Where(x => x.Target == target && x.IdConf >= 0.6f && x.Room != C.KillRoom && x.T0 <= C.KillClock + 5 && x.T1 >= C.KillClock - 5).OrderByDescending(x => x.T1 - x.T0).FirstOrDefault();
                    if (s == null) continue;
                    var props = new[] { new Prop { Kind = PropKind.AtPlace, A = target, Room = s.Room, T0 = s.T0, T1 = s.T1, Value = "window-cover" } };
                    var p = NewPlate(C, "talk:" + w + ":saw:" + target, PlateKind.Witness, PlateRole.Clear, true, LineBank.FixParticles($"{G(w)}이(가) 본 것"), $"{ClockFmt.Anchor(s.T0)}, {S.RoomName(s.Room)}. {G(target)} — {G(w)}", s.Room, s.T0, s.T1, props);
                    p.Witness = w; p.Seen = target; p.Alibi.Add(target); p.Back = LineBank.FixParticles($"그 시각 {G(target)}은(는) 현장이 아닌 {S.RoomName(s.Room)}에 있었다."); cands.Add(p);
                    break;
                }
        }

        // ---- the culprit's alibi as recorded (FalseAlibi, the boss fake): a true sighting that reads as "elsewhere at the time"
        static void FeedAlibi(CaseFacts C, List<Plate> cands)
        {
            var S = C.S; var st = C.Pack?.Story; if (st == null || C.Culprit == null) return;
            foreach (var w in C.Npcs.Where(x => x != C.Culprit).OrderBy(x => st.ClaimWith.Contains(x) ? 0 : 1).ThenBy(x => x, StringComparer.Ordinal))
            {
                var s = S.K(w).Sightings.Where(x => x.Target == C.Culprit && x.IdConf >= 0.6f && x.Room != C.KillRoom && (st.ClaimRoom < 0 || x.Room == st.ClaimRoom) && Math.Abs(x.T0 - C.KillClock) <= 90 && !(x.T0 <= C.KillClock && x.T1 >= C.KillClock))
                                  .OrderBy(x => Math.Abs(x.T0 - C.KillClock)).FirstOrDefault();
                if (s == null) continue;
                // worded about the place and the group: whoever else stood there with them
                var group = new List<string> { C.Culprit, w };
                var props = new[] { new Prop { Kind = PropKind.AtPlace, A = C.Culprit, Room = s.Room, T0 = s.T0, T1 = s.T1, Value = "window-cover" }, new Prop { Kind = PropKind.AtPlace, A = w, Room = s.Room, T0 = s.T0, T1 = s.T1, Value = "window-cover" } };
                var p = NewPlate(C, "talk:" + w + ":with:" + C.Culprit, PlateKind.Witness, PlateRole.FalseAlibi, false, LineBank.FixParticles($"{G(w)}이(가) 본 것"),
                    $"{ClockFmt.Anchor(s.T0)}, {S.RoomName(s.Room)}. {G(C.Culprit)}·{G(w)} 함께 — {G(w)}", s.Room, s.T0, s.T1, props);
                p.Witness = w; p.Seen = C.Culprit; p.Alibi.AddRange(group); p.Points = "alibi"; p.Owner = C.Culprit; p.Origin = "위장";
                p.Back = LineBank.FixParticles($"{ClockFmt.Anchor(s.T0)}의 일일 뿐 — {G(C.Victim?.Id)}이(가) 공격당한 때는 따로 있다."); p.Users.Add("story");
                cands.Add(p); return;
            }
        }

        // ---- an innocent near the scene at the wrong moment (Coincidence): seen close by, or seen moving the furniture there
        static void FeedCoincidence(CaseFacts C, List<Plate> cands)
        {
            var S = C.S;
            foreach (var w in C.Npcs)
            {
                var k = S.K(w);
                foreach (var n in k.FurnitureNotes.Where(n => n.Kind == FurnitureNoteKind.Saw && n.Who != null && n.Who != C.Culprit && n.Who != C.VictimId && n.Who != Cast.Player && (n.Room == C.KillRoom || n.Room == C.FoundRoom) && n.Clock >= C.W0 && n.Clock <= C.W1))
                {
                    if (cands.Any(p => p.Root == "furnsaw:" + n.Furniture + ":" + n.Rev)) continue;
                    var f = S.Layout.Furniture[n.Furniture]; string kor = FurnitureCatalog.Get(f.Type)?.Kor ?? "가구";
                    var props = new[] { new Prop { Kind = PropKind.AtPlace, A = n.Who, Room = n.Room, T0 = n.Clock, T1 = n.Clock }, new Prop { Kind = PropKind.Culprit, A = n.Who, B = C.VictimId, Value = "opportunity" } };
                    var p = NewPlate(C, "furnsaw:" + n.Furniture + ":" + n.Rev, PlateKind.Witness, PlateRole.Coincidence, false, LineBank.FixParticles($"{G(w)}이(가) 본 것"),
                        $"{ClockFmt.Anchor(n.Clock)}, {S.RoomName(n.Room)}. {G(n.Who)}이(가) {kor}을(를) 옮기고 있었다 — {G(w)}", n.Room, n.Clock, n.Clock, props);
                    p.Witness = w; p.Seen = n.Who; p.Points = n.Who; p.Owner = n.Who; p.Origin = "우연"; p.Routes.Add("ask:" + n.Who); p.Users.Add("theory:" + w);
                    p.Back = LineBank.FixParticles($"{G(n.Who)}은(는) 다른 일로 {kor}을(를) 옮겼을 뿐이다."); cands.Add(p);
                }
                foreach (var s in k.Sightings.Where(x => x.Target != C.Culprit && x.Target != C.VictimId && x.Target != Cast.Player && x.Target != w && S.A(x.Target) != null && !S.A(x.Target).IsButler && x.IdConf >= 0.6f && !x.Dead
                                                    && (x.Room == C.KillRoom || x.Room == C.FoundRoom) && x.T1 >= C.KillClock - 60 && x.T0 <= C.KillClock - 5).OrderBy(x => x.T0).Take(1))
                {
                    if (cands.Any(p => p.Seen == s.Target && p.Role == PlateRole.Coincidence)) continue;
                    var props = new[] { new Prop { Kind = PropKind.AtPlace, A = s.Target, Room = s.Room, T0 = s.T0, T1 = s.T1 }, new Prop { Kind = PropKind.Culprit, A = s.Target, B = C.VictimId, Value = "opportunity" } };
                    var p = NewPlate(C, "talk:" + w + ":near:" + s.Target, PlateKind.Witness, PlateRole.Coincidence, false, LineBank.FixParticles($"{G(w)}이(가) 본 것"),
                        $"{ClockFmt.Anchor(s.T0)}, {S.RoomName(s.Room)}. {G(s.Target)}{(s.Held != null ? " 손에 " + (ItemCatalog.Get(s.Held)?.Kor ?? s.Held) : "")} — {G(w)}", s.Room, s.T0, s.T1, props);
                    p.Witness = w; p.Seen = s.Target; p.Points = s.Target; p.Owner = s.Target; p.Origin = "우연"; p.Routes.Add("ask:" + s.Target); p.Users.Add("theory:" + w);
                    p.Back = LineBank.FixParticles($"{G(s.Target)}은(는) {G(C.VictimId)}이(가) 공격당하기 전에 그 자리를 떠났다."); cands.Add(p);
                }
            }
        }

        // ---- an innocent seen carrying something that could hurt, in the hours before (Coincidence): the wound says otherwise
        static void FeedHeld(CaseFacts C, List<Plate> cands)
        {
            var S = C.S; var v = C.Victim; if (v == null) return;
            var main = v.Body.Wounds.Where(w => !w.Postmortem).OrderByDescending(w => w.Sev).FirstOrDefault(); if (main == null) return;
            var seen = new HashSet<string>();
            foreach (var w in C.Npcs.Where(x => x != C.Culprit))
                foreach (var s in S.K(w).Sightings.Where(x => x.Held != null && x.IdConf >= 0.6f && x.Target != C.Culprit && x.Target != C.VictimId && x.Target != Cast.Player && x.Target != w && S.A(x.Target) != null && !S.A(x.Target).IsButler
                                                        && x.T1 >= C.KillClock - 150 && x.T0 <= C.KillClock).OrderBy(x => Math.Abs(x.T0 - C.KillClock)).ThenBy(x => x.Target, StringComparer.Ordinal))
                {
                    var def = ItemCatalog.Get(s.Held); if (def == null || !def.IsWeapon || def.Dmg == main.Type || !seen.Add(s.Target)) continue;
                    var props = new[] { new Prop { Kind = PropKind.Held, A = s.Target, Item = s.Held, Room = s.Room, T0 = s.T0, T1 = s.T1 }, new Prop { Kind = PropKind.WeaponType, A = C.VictimId, Value = def.Dmg.ToString(), Item = s.Held } };
                    var p = NewPlate(C, "talk:" + w + ":held:" + s.Target, PlateKind.Witness, PlateRole.Coincidence, false, LineBank.FixParticles($"{G(w)}이(가) 본 것"),
                        $"{ClockFmt.Anchor(s.T0)}, {S.RoomName(s.Room)}. {G(s.Target)}의 손에 {def.Kor} — {G(w)}", s.Room, s.T0, s.T1, props);
                    p.Witness = w; p.Seen = s.Target; p.Points = s.Target; p.Owner = s.Target; p.Origin = "우연"; p.Routes.Add("ask:" + s.Target); p.Users.Add("theory:" + w);
                    p.Back = LineBank.FixParticles($"{def.Kor}(으)로는 저런 상처가 나지 않는다 — {G(s.Target)}은(는) 다른 일로 들고 있었다."); cands.Add(p);
                    if (seen.Count >= 2) return;
                    break;
                }
        }

        internal static bool IsAlarm(SoundKind k) => k == SoundKind.Scream || k == SoundKind.Struggle || k == SoundKind.Strike || k == SoundKind.Crash || k == SoundKind.Fall || k == SoundKind.GlassBreak || k == SoundKind.Gunshot || k == SoundKind.Splash || k == SoundKind.Scrape;
        static string Trim(string s, int n) => string.IsNullOrEmpty(s) ? "흔적" : s.Length <= n ? s : s.Substring(0, n);
        internal static string NoteOf(string note, string key)
        {
            if (note == null) return null;
            foreach (var part in note.Split(';')) { int i = part.IndexOf('='); if (i > 0 && part.Substring(0, i) == key) return part.Substring(i + 1); }
            return null;
        }

        // ------------------------------------------------------------------ the presented story (claims) and the riddles engraved at confirm
        static void BuildClaims(CaseFacts C, CaseDeck d, List<Plate> cands)
        {
            var S = C.S; int n = 0; string V = G(C.VictimId);
            DeckClaim Add(int layer, Axis axis, string text, Prop presented, Prop actual, string holder, string trick)
            { var c = new DeckClaim { Id = "k" + (++n), Layer = layer, Axis = axis, Text = LineBank.FixParticles(text), Presented = presented, Actual = actual, Holder = holder, Trick = trick }; d.Claims.Add(c); return c; }
            // L1: what the scene says at first sight (the trick's false fact)
            var msg = cands.FirstOrDefault(p => p.Role == PlateRole.Frame && p.Root.StartsWith("trace:"));
            if (msg != null)
                Add(1, Axis.Identity, $"{V}이(가) 마지막에 남긴 글씨는 {G(msg.Points)}을(를) 가리킨다", msg.Props.FirstOrDefault(p => p.Kind == PropKind.TraceAt && p.Value != null && p.Value.StartsWith("bloodwriting")),
                    new Prop { Kind = PropKind.Culprit, A = C.Culprit, B = C.VictimId }, null, "Message");
            if (C.Trick == "Tod" && C.Victim != null)
            {
                double shift = C.Victim.Body.TodShift, t = C.DeathClock + shift;
                Add(1, Axis.Time, $"{V}은(는) {ClockFmt.Vague(t)}에 숨졌다", new Prop { Kind = PropKind.DeathWindow, A = C.VictimId, T0 = t - 15, T1 = t + 15, Value = "exam" }, new Prop { Kind = PropKind.DeathWindow, A = C.VictimId, T0 = C.DeathClock - 10, T1 = C.DeathClock + 10 }, null, "Tod");
            }
            if (C.Trick == "Seal")
                Add(1, Axis.Access, $"{S.RoomName(C.FoundRoom)}은(는) 안에서 잠겨 있었다 — 아무도 드나들 수 없었다", new Prop { Kind = PropKind.DoorLocked, A = C.VictimId, Room = C.FoundRoom, Value = "sealed" }, null, null, "Seal");
            var decoy = cands.FirstOrDefault(p => p.Role == PlateRole.Staged && p.Points == "means");
            if (decoy != null)
                Add(1, Axis.Means, $"{V}을(를) 친 건 시신 옆의 {decoy.Title.Replace("피 묻은 ", "")}이다", decoy.Props.FirstOrDefault(p => p.Kind == PropKind.WeaponType), null, null, "Swap");
            if (C.Trick == "Accident" || C.Trick == "Natural" || C.Trick == "Suicide")
            {
                string val = C.Trick == "Natural" ? "natural" : C.Trick == "Suicide" ? "suicide" : "accident:" + (C.Inc.Method ?? "fall");
                string text = C.Trick == "Natural" ? $"{V}은(는) 병으로 숨졌다 — 누가 한 일이 아니다" : C.Trick == "Suicide" ? $"{V}은(는) 스스로 목숨을 끊었다" : $"{V}의 죽음은 사고였다";
                Add(1, Axis.Cause, text, new Prop { Kind = PropKind.Culprit, A = null, B = C.VictimId, Value = val }, null, null, C.Trick);
            }
            // the place: found where they died?
            if (C.KillRoom != C.FoundRoom || C.Inc.BodyMoved)
                Add(1, Axis.Place, $"{V}은(는) 발견된 그 자리, {S.RoomName(C.FoundRoom)}에서 공격당했다", new Prop { Kind = PropKind.DeathPlace, A = C.VictimId, Room = C.FoundRoom, Value = "attack" }, new Prop { Kind = PropKind.DeathPlace, A = C.VictimId, Room = C.KillRoom, Value = "attack" }, null, null);
            // the time, when nothing staged it: the loudest thing anyone heard sets it, truly
            if (!d.Claims.Any(c => c.Axis == Axis.Time))
                Add(1, Axis.Time, $"{V}이(가) 공격당한 건 {ClockFmt.Vague(C.KillClock)}이다", new Prop { Kind = PropKind.DeathWindow, A = C.VictimId, T0 = C.KillClock - 10, T1 = C.KillClock + 10 }, new Prop { Kind = PropKind.DeathWindow, A = C.VictimId, T0 = C.KillClock - 10, T1 = C.KillClock + 10 }, null, null).Truth = "true";
            // L2: the culprit's alibi and their scapegoat
            var st = C.Pack?.Story;
            if (st != null && st.ClaimRoom >= 0 && C.Culprit != null)
                Add(2, Axis.Identity, $"{G(C.Culprit)}은(는) 그때 {S.RoomName(st.ClaimRoom)}에 있었다", new Prop { Kind = PropKind.AtPlace, A = C.Culprit, Room = st.ClaimRoom, T0 = st.ClaimFrom, T1 = st.ClaimTo, Value = "window-cover" }, null, C.Culprit, "Alibi");
            if (C.Scapegoat != null && C.Scapegoat != C.Culprit && S.A(C.Scapegoat)?.Alive == true)
                Add(2, Axis.Identity, $"{G(C.Scapegoat)}이(가) 했다", new Prop { Kind = PropKind.Culprit, A = C.Scapegoat, B = C.VictimId }, new Prop { Kind = PropKind.Culprit, A = C.Culprit, B = C.VictimId }, C.Culprit, "Frame");
            // the riddles: phrased as impossibilities from the L1 claims (the answer never in the words)
            int m = 0;
            foreach (var c in d.Claims.Where(c => c.Layer == 1).OrderBy(c => c.Axis == Axis.Time ? 1 : 0).ThenBy(c => c.Id, StringComparer.Ordinal).Take(3))
                d.Mysteries.Add(new DeckMystery { Id = "m" + (++m), Kind = c.Axis, Claim = c.Id, Text = RiddleText(C, c, cands) });
        }

        static string RiddleText(CaseFacts C, DeckClaim c, List<Plate> cands)
        {
            var S = C.S; string V = G(C.VictimId);
            switch (c.Axis)
            {
                case Axis.Identity: return c.Trick == "Message" ? $"「피로 쓴 글자」 — 정말 {V}이(가) 남긴 말인가" : $"「{V}을(를) 친 사람」 — 누구였나";
                case Axis.Time: return c.Trick == "Tod" ? $"「{ClockFmt.Vague(c.Presented.T0 + 15)}의 죽음」 — 정말 그 시각이었나" : $"「{ClockFmt.Vague(C.KillClock)}의 소리」 — 그 시각, 무슨 일이 있었나";
                case Axis.Access: return $"「잠긴 {S.RoomName(C.FoundRoom)}」 — 누가, 어떻게 드나들었나";
                case Axis.Means: return $"「시신 옆의 흉기」 — 정말 그것으로 쳤나";
                case Axis.Cause: return $"「{V}의 죽음」 — 정말 {(c.Trick == "Natural" ? "병" : c.Trick == "Suicide" ? "스스로" : "사고")}였나";
                case Axis.Place: return $"「{S.RoomName(C.FoundRoom)}의 {V}」 — 어디에서 공격당했나";
            }
            return c.Text;
        }

        // ------------------------------------------------------------------ what each plate breaks, supports, and how fakes flip
        static void LinkPlates(CaseFacts C, CaseDeck d, List<Plate> cands)
        {
            var S = C.S;
            foreach (var p in cands)
                foreach (var c in d.Claims)
                {
                    var (r, _) = Judge(S, c.Presented, p, true);
                    if (p.True && r == LogicResult.Contradict && !IsClaimTrue(C, c)) p.Breaks.Add(c.Id);
                    else if (!p.True && (r == LogicResult.Support || Leans(p, c))) p.Supports.Add(c.Id);
                }
            foreach (var f in cands.Where(p => !p.True))
            {
                foreach (var t in cands.Where(p => p.True))
                    if (f.Props.Any(fp => Judge(S, fp, t, true).r == LogicResult.Contradict) || f.Supports.Any(cid => t.Breaks.Contains(cid)))
                        if (!f.Routes.Contains("pl:" + t.Id)) f.Routes.Add("pl:" + t.Id);
                if (f.Role == PlateRole.FalseAlibi)
                    foreach (var t in cands.Where(p => p.True && (p.Kind == PlateKind.Body || p.Role == PlateRole.Confirm || p.Role == PlateRole.Link)))
                        if (!f.Routes.Contains("pl:" + t.Id)) f.Routes.Add("pl:" + t.Id);
                // who would lean on it: the culprit's story, whoever found or read it, anyone who fears or resents the person it points at
                if (C.Culprit != null && (f.Role == PlateRole.Frame || f.Role == PlateRole.Staged || f.Role == PlateRole.FalseAlibi) && !f.Users.Contains("story")) f.Users.Add("story");
                foreach (var w in C.Npcs)
                {
                    if (w == f.Points || w == C.Culprit) continue;
                    bool found = S.K(w).Evidence.Any(e => e.Root != null && (e.Root == f.Root || e.Root.EndsWith(f.Root)));
                    bool grudge = f.Points != null && S.A(f.Points) != null && S.HasRel(w, f.Points) && (S.R(w, f.Points).Grudge > 0.25f || S.R(w, f.Points).Fear > 0.3f);
                    if ((found || grudge) && !f.Users.Contains("theory:" + w)) f.Users.Add("theory:" + w);
                }
            }
        }

        /// <summary>Does the plate's face lean toward the claim even where Logic has no rule (a planted item toward "X did it").</summary>
        static bool Leans(Plate p, DeckClaim c)
        {
            if (c.Presented == null) return false;
            if (c.Presented.Kind == PropKind.Culprit && p.Points != null && p.Points == c.Presented.A) return true;
            if (c.Presented.Kind == PropKind.AtPlace && p.Role == PlateRole.FalseAlibi) return true;
            return c.Trick != null && ((c.Trick == "Message" && p.Root.StartsWith("trace:") && p.Role == PlateRole.Frame) || (c.Trick == "Swap" && p.Points == "means"));
        }

        static bool IsClaimTrue(CaseFacts C, DeckClaim c) => c.Truth == "true";

        /// <summary>
        /// The debate's one test of a plate against a claim: Logic.Check over the plate's props (the face reading), plus the few
        /// readings the old rules never needed (an alibi covering the kill time clears "X did it"; a struggle in another room
        /// undoes "died where found").
        /// </summary>
        public static (LogicResult r, string why) Judge(GameState S, Prop claim, Plate p, bool direct)
        {
            if (claim == null || p == null) return (LogicResult.Irrelevant, null);
            var best = LogicResult.Irrelevant; string why = null;
            int Rank(LogicResult r) => r == LogicResult.Contradict ? 5 : r == LogicResult.Conditional ? 4 : r == LogicResult.LimitScope ? 3 : r == LogicResult.Support ? 2 : r == LogicResult.NeedPremise ? 1 : 0;
            foreach (var e in p.Props)
            {
                var v = Logic.Check(S, claim, e, direct, p.Root);
                if (Rank(v.Result) > Rank(best)) { best = v.Result; why = v.Why; }
                // extras
                if (claim.Kind == PropKind.Culprit && claim.A != null && e.Kind == PropKind.AtPlace && e.A == claim.A && e.Value == "window-cover" && Rank(LogicResult.Contradict) > Rank(best) && CoversKill(S, e, claim))
                { best = LogicResult.Contradict; why = LineBank.FixParticles($"그 시각 {G(e.A)}은(는) {S.RoomName(e.Room)}에 있었다"); }
                bool struggleSign = e.Kind == PropKind.TraceAt && e.Room >= 0 && ((e.Value != null && e.Value.StartsWith("furniture:")) || StruggleTypes.Contains(e.Item ?? ""));
                if (claim.Kind == PropKind.DeathPlace && struggleSign && e.Room != claim.Room)
                {
                    var r = claim.Value == "attack" ? LogicResult.Contradict : LogicResult.LimitScope;
                    if (Rank(r) > Rank(best)) { best = r; why = LineBank.FixParticles($"{S.RoomName(e.Room)}에도 몸싸움 흔적이 있다 — 공격은 거기서 시작됐다"); }
                }
                if (claim.Kind == PropKind.DeathPlace && e.Kind == PropKind.Heard && e.Room >= 0 && e.Room != claim.Room && Rank(LogicResult.LimitScope) > Rank(best))
                { best = LogicResult.LimitScope; why = LineBank.FixParticles($"그 무렵 소리가 난 곳은 {S.RoomName(e.Room)} 쪽이다"); }
            }
            return (best, why);
        }

        static readonly HashSet<string> StruggleTypes = new HashSet<string> { "Scuff", "Struggle", "Scratch", "Debris" };

        static bool CoversKill(GameState S, Prop alibi, Prop claim)
        {
            var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == claim.B && i.Loop == S.Loop);
            double t = inc == null ? -1 : inc.CauseClock >= 0 ? inc.CauseClock : inc.DeathClock;
            return t >= 0 && alibi.T0 <= t + 3 && alibi.T1 >= t - 3;
        }

        // ------------------------------------------------------------------ selection (target 10; fakes 4–6; honest shrink)
        static void Select(CaseFacts C, CaseDeck d, List<Plate> cands)
        {
            var S = C.S;
            const int WitnessCap = 4;
            var all = new List<Plate>();
            int Wit() => all.Count(p => p.Kind == PlateKind.Witness);
            bool Take(Plate p, string why)
            {
                if (p == null || all.Contains(p) || all.Any(x => x.Root == p.Root)) return false;
                if (p.Kind == PlateKind.Witness && Wit() >= WitnessCap) { d.Log.Add("witness cap: " + p.Root + " (" + why + ")"); return false; }
                all.Add(p); return true;
            }
            // fakes first (the boss first): a route, a user, ≤2 pointing at one person
            foreach (var f in cands.Where(p => !p.True).OrderBy(p => FakeOrder(p.Role)).ThenBy(p => p.Root, StringComparer.Ordinal))
            {
                if (all.Count(x => !x.True) >= d.FakeTarget) break;
                if (f.Routes.Count == 0) { d.Log.Add("fake dropped (no route): " + f.Root); continue; }
                if (f.Users.Count == 0) { d.Log.Add("fake dropped (nobody leans on it): " + f.Root); continue; }
                if (f.Points != null && all.Count(x => !x.True && x.Points == f.Points) >= 2) { d.Log.Add("fake dropped (spread): " + f.Root); continue; }
                Take(f, "fake");
            }
            var trues = cands.Where(p => p.True).ToList();
            int target = 12;
            // body; the L1 breakers (hinge, confirm); plates that turn the admitted fakes
            Take(trues.FirstOrDefault(p => p.Kind == PlateKind.Body), "body");
            var l1 = new HashSet<string>(d.Claims.Where(c => c.Layer == 1).Select(c => c.Id));
            foreach (var t in trues.Where(p => p.Breaks.Any(l1.Contains)).OrderBy(p => p.Channel == Channel.Witness ? 1 : 0).ThenBy(p => p.Root, StringComparer.Ordinal).Take(3)) Take(t, "breaker");
            foreach (var f in all.Where(p => !p.True).ToList())
                if (!f.Routes.Any(r => all.Any(x => "pl:" + x.Id == r)))
                    Take(trues.Where(t => f.Routes.Contains("pl:" + t.Id)).OrderBy(t => t.Kind == PlateKind.Witness ? 1 : 0).ThenBy(t => t.Root, StringComparer.Ordinal).FirstOrDefault(), "route");
            // one culprit link (a witness or a trace the culprit left on the way), one clear, one sound
            Take(trues.Where(p => p.Role == PlateRole.Link && p.Kind == PlateKind.Witness).OrderBy(p => Math.Abs(p.T0 - C.KillClock)).ThenBy(p => p.Root, StringComparer.Ordinal).FirstOrDefault(), "link");
            Take(trues.Where(p => p.Role == PlateRole.Clear).OrderBy(p => p.Root, StringComparer.Ordinal).FirstOrDefault(), "clear");
            Take(trues.Where(p => p.Role == PlateRole.Confirm).OrderBy(p => Math.Abs(p.T0 - C.KillClock)).ThenBy(p => p.Root, StringComparer.Ordinal).FirstOrDefault(), "sound");
            // the scene: struggle marks and blood where it happened, the culprit's own traces on the way, the weapon, the furniture
            foreach (var t in trues.Where(p => p.Kind != PlateKind.Witness && p.Kind != PlateKind.Body).OrderBy(p => SceneOrder(C, p)).ThenBy(p => p.Root, StringComparer.Ordinal))
            { if (all.Count >= target) break; Take(t, "scene"); }
            foreach (var t in trues.Where(p => p.Kind == PlateKind.Witness).OrderBy(p => p.Role == PlateRole.Link ? 0 : 1).ThenBy(p => p.Root, StringComparer.Ordinal))
            { if (all.Count >= target) break; Take(t, "fill"); }
            // hinge: the non-witness true plate that breaks an L1 claim
            var hinge = all.Where(p => p.True && p.Breaks.Any(l1.Contains)).OrderBy(p => p.Channel == Channel.Witness ? 1 : 0).ThenBy(p => p.Root, StringComparer.Ordinal).FirstOrDefault();
            if (hinge != null && hinge.Role != PlateRole.Body) hinge.Role = PlateRole.Hinge;
            // routes left pointing outside the deck are dropped; a fake left with none leaves too
            var ids = new HashSet<string>(all.Select(p => "pl:" + p.Id));
            foreach (var f in all.Where(p => !p.True).ToList())
            {
                f.Routes.RemoveAll(r => r.StartsWith("pl:") && !ids.Contains(r));
                if (f.Routes.Count == 0) { all.Remove(f); d.Log.Add("fake dropped (its route left the deck): " + f.Root); }
            }
            foreach (var p in cands.Where(p => !all.Contains(p))) d.Log.Add("left out: " + p.Root);
            // numbering: body first, the rest by MurderHash (never by truth)
            var body = all.FirstOrDefault(p => p.Kind == PlateKind.Body);
            var rest = all.Where(p => p != body).OrderBy(p => MurderHash.U01(S, "deck:" + C.Inc.Id + ":" + p.Root)).ToList();
            int n = 0;
            if (body != null) { body.N = ++n; d.Plates.Add(body); }
            foreach (var p in rest) { p.N = ++n; d.Plates.Add(p); }
            d.TrueCount = d.Plates.Count(p => p.True); d.FakeCount = d.Plates.Count - d.TrueCount;
            // found / borrowed / late (from the House's discovery photograph)
            foreach (var p in d.Plates)
            {
                bool mine = S.K(Cast.Player).Evidence.Any(e => e.Root != null && (e.Root == p.Root || e.Root.EndsWith(p.Root))) || S.K(Cast.Player).Examined.Contains(p.Root) || p.Kind == PlateKind.Body;
                var finder = mine ? null : C.Npcs.FirstOrDefault(w => S.K(w).Evidence.Any(e => e.Root != null && (e.Root == p.Root || e.Root.EndsWith(p.Root))) || w == p.Witness);
                p.State = mine ? PlateState.Found : finder != null ? PlateState.Borrowed : PlateState.Late;
                p.FoundBy = mine ? Cast.Player : finder;
            }
        }

        /// <summary>Scene plates first where the attack began, then where the body lay, then the culprit's own traces on the way.</summary>
        static int SceneOrder(CaseFacts C, Plate p) => p.Breaks.Count > 0 ? 0 : p.Room == C.KillRoom ? 1 : p.Room == C.FoundRoom ? 2 : p.Role == PlateRole.Link ? 3 : 4;

        static int FakeOrder(PlateRole r) => r == PlateRole.FalseAlibi ? 0 : r == PlateRole.Frame ? 1 : r == PlateRole.Staged ? 2 : r == PlateRole.Mistaken ? 3 : r == PlateRole.Coincidence ? 4 : 5;

        static void Validate(CaseFacts C, CaseDeck d)
        {
            if (d.Plates.Count < 8) d.Log.Add($"thin deck: {d.Plates.Count} plates");
            if (d.FakeCount < 3) d.Log.Add($"thin fakes: {d.FakeCount}");
            if (!d.Plates.Any(p => p.Kind == PlateKind.Body)) d.Log.Add("no body plate");
            var l1 = d.Claims.Where(c => c.Layer == 1 && c.Truth != "true").Select(c => c.Id).ToList();
            foreach (var c in l1) if (!d.Plates.Any(p => p.True && p.Breaks.Contains(c))) d.Log.Add("claim unbreakable by a plate: " + c);
            foreach (var f in d.Plates.Where(p => !p.True)) if (f.Users.Count == 0 || f.Routes.Count == 0) d.Log.Add("fake without user/route: " + f.Root);
            if (d.Plates.Count(p => p.Kind == PlateKind.Witness) > 4) d.Log.Add("too many witness plates");
        }
    }
}
