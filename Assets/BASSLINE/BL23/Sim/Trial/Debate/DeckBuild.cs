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
            FeedBody(C, cands); FeedTraces(C, cands); FeedFurniture(C, cands); FeedItems(C, cands); FeedWitness(C, cands); FeedCulpritSeen(C, cands); FeedOffClaim(C, cands); FeedBeats(C, cands); FeedHunt(C, cands); FeedMasque(C, cands); FeedDoor(C, cands); FeedCalls(C, cands); FeedAlibi(C, cands); FeedCoincidence(C, cands); FeedHeld(C, cands);
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
            var S = C.S; var kinds = new HashSet<string>();
            foreach (var t in S.Traces.OrderBy(t => t.Seq))
            {
                if (t.Cleaned || t.Visibility > 2 || t.Type == "SwitchTouched") continue;
                bool atScene = (t.Room == C.KillRoom || t.Room == C.FoundRoom) && t.Clock >= C.W0 && t.Clock <= C.W1 + 5;
                bool route = t.Source != null && t.Source == C.Culprit && t.Clock >= C.W0 - 60 && t.Clock <= C.W1;
                if (!(t.Victim == C.VictimId || atScene || route)) continue;
                // one photograph per kind of mark per room: a trail of six drag marks is one plate, not six
                if (t.Type != "BloodWriting" && !kinds.Add(t.Type + ":" + t.Room)) continue;
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
                // the cup a dose went into (poison, a sedative): left where it was drunk from
                bool cup = !weapon && it.Type == "Cup" && it.Room >= 0 && (it.Room == C.KillRoom || it.Room == C.FoundRoom) && it.Surface.Any(x => x.StartsWith("residue:", StringComparison.Ordinal));
                if (!weapon && !decoy && !plant && !cup) continue;
                if (cup && cands.Any(p => p.Root.StartsWith("item:", StringComparison.Ordinal) && p.Title == "남은 잔")) continue;   // one cup is enough
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
                else if (cup)
                {
                    string smell = it.Surface.Contains("residue:bitter") ? "쓴 냄새" : "달큰한 약 냄새";
                    var p = NewPlate(C, "item:" + it.Id, PlateKind.Object, PlateRole.Link, true, "남은 잔", $"잔. 바닥에 {smell}가 남아 있다. ({S.RoomName(it.Room)})", it.Room, -1, -1, props);
                    p.Back = LineBank.FixParticles($"{G(C.VictimId)}은(는) 쓰러지기 전에 이 잔으로 무언가를 마셨다 — 탄 것은 그보다 앞서다."); cands.Add(p);
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
            // the culprit's false sighting ("I saw the scapegoat heading for the scene") needs someone who had the scapegoat elsewhere
            // around then — not a full alibi for the kill, but enough to break those words (the pack's own breakers for the lie)
            var sawLie = C.Pack?.Lies?.FirstOrDefault(l => l.Topic == "saw");
            if (sawLie != null && C.Scapegoat != null && !cands.Any(p => p.Role == PlateRole.Clear && p.Alibi.Contains(C.Scapegoat)))
                foreach (var w in sawLie.BrokenBy.Where(b => b.StartsWith("witness:")).Select(b => b.Substring(8)).Where(x => C.Npcs.Contains(x) && x != C.Scapegoat && x != C.Culprit).OrderBy(x => x, StringComparer.Ordinal))
                {
                    var s = S.K(w).Sightings.Where(x => x.Target == C.Scapegoat && x.IdConf > 0.5f && x.Room >= 0 && x.Room != C.KillRoom && Math.Abs(x.T0 - C.KillClock) < 20).OrderBy(x => Math.Abs(x.T0 - C.KillClock)).FirstOrDefault();
                    if (s == null) continue;
                    var props = new[] { new Prop { Kind = PropKind.AtPlace, A = C.Scapegoat, Room = s.Room, T0 = s.T0, T1 = s.T1, Value = "elsewhere" } };
                    var p = NewPlate(C, "talk:" + w + ":saw:" + C.Scapegoat, PlateKind.Witness, PlateRole.Seam, true, LineBank.FixParticles($"{G(w)}이(가) 본 것"), $"{ClockFmt.Anchor(s.T0)}, {S.RoomName(s.Room)}. {G(C.Scapegoat)} — {G(w)}", s.Room, s.T0, s.T1, props);
                    p.Witness = w; p.Seen = C.Scapegoat; p.Back = LineBank.FixParticles($"그 무렵 {G(C.Scapegoat)}은(는) 현장 쪽이 아닌 {S.RoomName(s.Room)}에 있었다."); cands.Add(p);
                    break;
                }
        }

        // ---- fair play: what someone saw of the culprit away from the scene that still ties them to it — the weapon in their
        //      hand before (수단), blood on their clothes after (기회). The court must be able to hold the killer with proof.
        static void FeedCulpritSeen(CaseFacts C, List<Plate> cands)
        {
            var S = C.S; if (C.Culprit == null || C.Victim == null) return;
            var weaponType = C.Inc.Weapon != null ? S.I(C.Inc.Weapon)?.Type : null;
            var main = C.Victim.Body.Wounds.Where(w => !w.Postmortem).OrderByDescending(w => w.Sev).FirstOrDefault();
            bool FitsWound(string type) { var def = ItemCatalog.Get(type); return def != null && def.IsWeapon && (type == weaponType || main != null && def.Dmg == main.Type); }
            bool heldShown = cands.Any(p => p.True && p.Props.Any(x => x.Kind == PropKind.Held && x.A == C.Culprit));
            foreach (var w in C.Npcs.Where(x => x != C.Culprit).OrderBy(x => x, StringComparer.Ordinal))
            {
                var k = S.K(w);
                if (!heldShown)
                {
                    var s = k.Sightings.Where(x => x.Target == C.Culprit && x.IdConf >= 0.6f && x.Disguise == null && x.Held != null && FitsWound(x.Held) && x.T1 >= C.KillClock - 300 && x.T0 <= C.KillClock + 5)
                                       .OrderBy(x => Math.Abs(x.T0 - C.KillClock)).FirstOrDefault();
                    if (s != null)
                    {
                        string kor = ItemCatalog.Get(s.Held)?.Kor ?? s.Held;
                        var props = new[] { new Prop { Kind = PropKind.Held, A = C.Culprit, Item = s.Held, Room = s.Room, T0 = s.T0, T1 = s.T1 } };
                        var p = NewPlate(C, "talk:" + w + ":means:" + C.Culprit, PlateKind.Witness, PlateRole.Link, true, $"{G(w)}이(가) 본 것", $"{ClockFmt.Anchor(s.T0)}, {S.RoomName(s.Room)}. {G(C.Culprit)}의 손에 {kor} — {G(w)}", s.Room, s.T0, s.T1, props);
                        p.Witness = w; p.Seen = C.Culprit; p.Title = LineBank.FixParticles(p.Title); p.Face = LineBank.FixParticles(p.Face);
                        p.Back = LineBank.FixParticles($"그 무렵 {G(C.Culprit)}은(는) {kor}을(를) 들고 있었다."); cands.Add(p); heldShown = true;
                    }
                }
                // poison or a sedative: someone saw the culprit's hand near the victim's cup (the dose, not the death — the alibi
                // for the moment they collapsed does not cover it)
                if (!cands.Any(p => p.Root.StartsWith("talk:", StringComparison.Ordinal) && p.Root.EndsWith(":dose:" + C.Culprit, StringComparison.Ordinal)))
                {
                    var e = S.Ledger.Where(x => x.Type == "SawNearCup" && x.Actor == w && x.Target == C.Culprit && x.Data == C.VictimId && x.Clock <= C.FoundClock)
                                    .OrderBy(x => Math.Abs(x.Clock - C.KillClock)).FirstOrDefault();
                    if (e != null)
                    {
                        var props = new[] { new Prop { Kind = PropKind.AtPlace, A = C.Culprit, Room = e.Room, T0 = e.Clock, T1 = e.Clock } };
                        var p = NewPlate(C, "talk:" + w + ":dose:" + C.Culprit, PlateKind.Witness, PlateRole.Link, true, $"{G(w)}이(가) 본 것", $"{ClockFmt.Anchor(e.Clock)}, {S.RoomName(e.Room)}. {G(C.Culprit)}의 손이 {G(C.VictimId)}의 잔 가까이 — {G(w)}", e.Room, e.Clock, e.Clock, props);
                        p.Witness = w; p.Seen = C.Culprit; p.Title = LineBank.FixParticles(p.Title); p.Face = LineBank.FixParticles(p.Face);
                        p.Back = LineBank.FixParticles($"{G(C.Culprit)}은(는) 그때 {G(C.VictimId)}의 잔에 무언가를 탔다."); cands.Add(p);
                    }
                }
                // where the weapon turned up (washed, stashed): the culprit seen in that room right after the deed
                var wit = C.Inc.Weapon != null ? S.I(C.Inc.Weapon) : null;
                if (wit != null && wit.Room >= 0 && wit.Room != C.KillRoom && wit.Room != C.FoundRoom && !cands.Any(p => p.Root.EndsWith(":after:" + C.Culprit, StringComparison.Ordinal)))
                {
                    var s = k.Sightings.Where(x => x.Target == C.Culprit && x.IdConf >= 0.6f && x.Disguise == null && !x.Dead && x.Room == wit.Room && x.T0 >= C.KillClock - 2 && x.T0 <= C.KillClock + 60)
                                       .OrderBy(x => x.T0).FirstOrDefault();
                    if (s != null)
                    {
                        string kor = ItemCatalog.Get(wit.Type)?.Kor ?? "흉기";
                        var props = new[] { new Prop { Kind = PropKind.AtPlace, A = C.Culprit, Room = s.Room, T0 = s.T0, T1 = s.T1 } };
                        var p = NewPlate(C, "talk:" + w + ":after:" + C.Culprit, PlateKind.Witness, PlateRole.Link, true, $"{G(w)}이(가) 본 것", $"{ClockFmt.Anchor(s.T0)}, {S.RoomName(s.Room)}. {G(C.Culprit)} — {G(w)}", s.Room, s.T0, s.T1, props);
                        p.Witness = w; p.Seen = C.Culprit; p.Title = LineBank.FixParticles(p.Title); p.Face = LineBank.FixParticles(p.Face);
                        p.Back = LineBank.FixParticles($"범행 직후 {G(C.Culprit)}은(는) {kor}이(가) 나온 {S.RoomName(s.Room)}에 있었다."); cands.Add(p);
                    }
                }
                if (!cands.Any(p => p.Props.Any(x => x.Kind == PropKind.Bloodied && x.A == C.Culprit)))
                {
                    var s = k.Sightings.Where(x => x.Target == C.Culprit && x.IdConf >= 0.6f && x.Disguise == null && x.Bloody && x.T0 >= C.KillClock - 5 && x.T0 <= C.KillClock + 240)
                                       .OrderBy(x => x.T0).FirstOrDefault();
                    if (s != null)
                    {
                        var props = new[] { new Prop { Kind = PropKind.Bloodied, A = C.Culprit, Room = s.Room, T0 = s.T0, T1 = s.T1 } };
                        var p = NewPlate(C, "talk:" + w + ":blood:" + C.Culprit, PlateKind.Witness, PlateRole.Link, true, $"{G(w)}이(가) 본 것", $"{ClockFmt.Anchor(s.T0)}, {S.RoomName(s.Room)}. {G(C.Culprit)}의 옷에 붉은 얼룩 — {G(w)}", s.Room, s.T0, s.T1, props);
                        p.Witness = w; p.Seen = C.Culprit; p.Title = LineBank.FixParticles(p.Title); p.Face = LineBank.FixParticles(p.Face);
                        p.Back = LineBank.FixParticles($"{G(C.Culprit)}의 옷에 묻은 건 피였다 — 공격한 뒤였다."); cands.Add(p);
                    }
                }
            }
        }

        // ---- what someone saw the culprit do while getting ready (the scheme's beats: the cup they filled themselves, the weapon they
        //      took, the note slipped under a door, the little gift that marks someone in the dark) — innocent then, the proof now
        static void FeedBeats(CaseFacts C, List<Plate> cands)
        {
            var S = C.S; if (C.Culprit == null || S.Mur == null || C.Inc.PlanId == null) return;
            var sc = Initiative.ByPlan(S, C.Inc.PlanId); if (sc == null) return;
            int Rank(string k) => k == "serve" ? 5 : k == "poison" || k == "obtain" ? 4 : k == "note" || k == "mark" || k == "stash" ? 3 : k == "garb" || k == "appoint" ? 2 : 1;
            int n = 0;
            foreach (var b in S.Mur.Beats.Where(b => b.Scheme == sc.Id && b.Actor == C.Culprit && b.Kind != "firstin" && b.Loop == S.Loop && b.Clock <= C.FoundClock).OrderByDescending(b => Rank(b.Kind)).ThenBy(b => Math.Abs(b.Clock - C.KillClock)).ThenBy(b => b.Id, StringComparer.Ordinal))
            {
                var w = b.Observers.Where(o => o != C.Culprit && C.Npcs.Contains(o)).OrderBy(o => o, StringComparer.Ordinal).FirstOrDefault();
                if (w == null || string.IsNullOrEmpty(b.Text)) continue;
                var props = new List<Prop> { new Prop { Kind = PropKind.AtPlace, A = C.Culprit, Room = b.Room, T0 = b.Clock, T1 = b.Clock } };
                var it = b.Item != null ? S.I(b.Item) : null;
                if (it?.Def != null && it.Def.IsWeapon) props.Add(new Prop { Kind = PropKind.Held, A = C.Culprit, Item = it.Type, Room = b.Room, T0 = b.Clock, T1 = b.Clock });
                var p = NewPlate(C, "beat:" + b.Id, PlateKind.Witness, PlateRole.Link, true, $"{G(w)}이(가) 본 것", $"{ClockFmt.Anchor(b.Clock)}, {S.RoomName(b.Room)}. {b.Text} — {G(w)}", b.Room, b.Clock, b.Clock, props);
                p.Witness = w; p.Seen = C.Culprit; p.Title = LineBank.FixParticles(p.Title); p.Face = LineBank.FixParticles(p.Face);
                p.Back = LineBank.FixParticles(string.IsNullOrEmpty(b.Meaning) ? b.Text : $"그때는 {b.Innocent ?? "아무렇지 않아 보였다"} — 사실은 {b.Meaning}.");
                cands.Add(p); if (++n >= 2) return;
            }
        }

        // ---- fair play for the alibi: the pack's own breakers of the culprit's "where" — someone who saw them somewhere other than
        //      the room they claim while the claim runs, or saw them get up and leave the gathering they claim — are witnesses the
        //      court can call. Without a plate of theirs the alibi could only stand (the lie breaks on a plate from its breaker).
        static void FeedOffClaim(CaseFacts C, List<Plate> cands)
        {
            var S = C.S; var st = C.Pack?.Story; var lie = C.Pack?.Lies?.FirstOrDefault(l => l.Topic == "where");
            if (st == null || lie == null || C.Culprit == null || st.ClaimRoom < 0) return;
            string claimRoom = S.RoomName(st.ClaimRoom); int n = 0;
            foreach (var w in lie.BrokenBy.Where(b => b.StartsWith("witness:")).Select(b => b.Substring(8)).Where(x => x != C.Culprit && C.Npcs.Contains(x)).OrderBy(x => x, StringComparer.Ordinal))
            {
                if (cands.Any(p => p.True && p.Kind == PlateKind.Witness && p.Witness == w && p.Seen == C.Culprit && p.Props.Any(x => x.Kind == PropKind.AtPlace && x.Room != st.ClaimRoom))) continue;
                var k = S.K(w);
                var s = k.Sightings.Where(x => x.Target == C.Culprit && x.IdConf >= 0.6f && x.Disguise == null && !x.Dead && x.Room >= 0 && x.Room != st.ClaimRoom && x.T0 <= st.ClaimTo - 1 && x.T1 >= st.ClaimFrom + 1)
                                   .OrderBy(x => Math.Abs(x.T0 - C.KillClock)).ThenBy(x => x.T0).FirstOrDefault();
                if (s != null)
                {
                    var props = new[] { new Prop { Kind = PropKind.AtPlace, A = C.Culprit, Room = s.Room, T0 = s.T0, T1 = s.T1 } };
                    var p = NewPlate(C, "talk:" + w + ":off:" + C.Culprit, PlateKind.Witness, PlateRole.Link, true, LineBank.FixParticles($"{G(w)}이(가) 본 것"), $"{ClockFmt.Anchor(s.T0)}, {S.RoomName(s.Room)}. {G(C.Culprit)} — {G(w)}", s.Room, s.T0, s.T1, props);
                    p.Witness = w; p.Seen = C.Culprit;
                    p.Back = LineBank.FixParticles($"{G(C.Culprit)}이(가) 있었다고 한 곳은 {claimRoom} — 그런데 그 시각 {G(C.Culprit)}은(는) {S.RoomName(s.Room)}에 있었다.");
                    cands.Add(p); if (++n >= 2) return; continue;
                }
                // they saw them get up and go (a gathering, a table) shortly before the attack — the claim's "the whole time" fails
                int left = -1, room = -1;
                foreach (var f in k.Facts.Where(f => f.StartsWith("left-gathering:" + C.Culprit + ":", StringComparison.Ordinal) || f.StartsWith("left-table:" + C.Culprit + ":", StringComparison.Ordinal)))
                {
                    var parts = f.Split(':'); if (parts.Length < 4 || !int.TryParse(parts[3], out var clock)) continue;
                    if (clock > C.KillClock + 5 || C.KillClock - clock > 45 || (left >= 0 && Math.Abs(C.KillClock - clock) >= Math.Abs(C.KillClock - left))) continue;
                    left = clock;
                    room = parts[0] == "left-table" && int.TryParse(parts[2], out var r) ? r : S.Gatherings.FirstOrDefault(g => g.Id == parts[2])?.Cur.Room ?? st.ClaimRoom;
                }
                if (left < 0) continue;
                if (room < 0) room = st.ClaimRoom;
                var q = NewPlate(C, "talk:" + w + ":left:" + C.Culprit, PlateKind.Witness, PlateRole.Link, true, LineBank.FixParticles($"{G(w)}이(가) 본 것"), $"{ClockFmt.Anchor(left)}, {S.RoomName(room)}. 자리를 뜨는 {G(C.Culprit)} — {G(w)}", room, left, left, null);
                q.Witness = w; q.Seen = C.Culprit;
                q.Back = LineBank.FixParticles($"{G(C.Culprit)}은(는) 그 자리에 끝까지 있지 않았다 — {ClockFmt.Vague(left)}에 자리를 떴다.");
                cands.Add(q); if (++n >= 2) return;
            }
        }

        // ---- the treasure hunt (HouseEvents): the chart the house read out that morning — who searched where, alone — is on record;
        //      and anyone who saw the culprit anywhere but their posted zone while the search ran has broken the one alibi a hunt allows
        static void FeedHunt(CaseFacts C, List<Plate> cands)
        {
            var S = C.S; var g = HouseEvents.HuntAt(S, C.KillClock); if (g == null || C.Culprit == null) return;
            double at = g.Cur.Start + 10, end = g.Cur.Start + (HouseEvents.KindOf(g)?.Len ?? 90);
            int vz = HouseEvents.ZoneOf(S, g, C.VictimId), cz = HouseEvents.ZoneOf(S, g, C.Culprit);
            if (vz < 0 && cz < 0) return;
            // the chart: the victim's line, the culprit's among a few others (in the order the house read them), the rest counted
            var posted = C.People.Concat(new[] { C.VictimId }).Where(x => HouseEvents.ZoneOf(S, g, x) >= 0).Distinct().ToList();
            var shown = posted.Where(x => x != C.VictimId).OrderBy(x => x == C.Culprit ? 0 : 1).ThenBy(x => MurderHash.U01(S, "huntchart:" + C.Inc.Id + ":" + x)).ThenBy(x => x, StringComparer.Ordinal).Take(4)
                              .OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (vz >= 0) shown.Insert(0, C.VictimId);
            string Line(string x) => $"{G(x)} — {S.RoomName(HouseEvents.ZoneOf(S, g, x))}";
            int more = posted.Count - shown.Count;
            // and who was not on it: nobody sent them anywhere (the chart's other half)
            var out_ = C.People.Where(x => x != Cast.Player && HouseEvents.ZoneOf(S, g, x) < 0 && x != C.VictimId).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var props = shown.Select(x => new Prop { Kind = PropKind.Invited, A = x, Room = HouseEvents.ZoneOf(S, g, x), T0 = at, T1 = end, Value = "보물찾기 구역표" }).ToList();
            var chart = NewPlate(C, "record:huntchart:" + g.Id, PlateKind.Record, PlateRole.Confirm, true, "보물찾기 구역표",
                $"{ClockFmt.Vague(at)}부터 {ClockFmt.Vague(end)}까지, 각자 혼자. {string.Join(" · ", shown.Select(Line))}{(more > 0 ? $" 외 {TrialSystem.Kor(more)} 명" : "")}{(out_.Count > 0 ? " · 참가하지 않음: " + string.Join("·", out_.Select(G)) : "")}", vz >= 0 ? vz : C.KillRoom, at, end, props);
            chart.Back = (vz >= 0 ? LineBank.FixParticles($"그 시각 {G(C.VictimId)}이(가) {S.RoomName(vz)}에 혼자 있다는 건 구역표를 들은 사람 누구나 알았다. ") : "")
                       + "구역표는 가라는 곳이지, 있었다는 증거는 아니다." + (out_.Count > 0 ? " 구역이 없던 사람은 어디에도 묶여 있지 않았다." : "");
            cands.Add(chart);
            if (cz < 0) return;
            // the culprit off their zone while the claim runs (the scene itself is FeedWitness's)
            var st = C.Pack?.Story; double c0 = st != null && st.ClaimRoom == cz ? st.ClaimFrom : C.KillClock - 25, c1 = st != null && st.ClaimRoom == cz ? st.ClaimTo : C.KillClock + 20;
            foreach (var w in C.Npcs.Where(x => x != C.Culprit).OrderBy(x => x, StringComparer.Ordinal))
            {
                var s = S.K(w).Sightings.Where(x => x.Target == C.Culprit && x.IdConf >= 0.6f && x.Disguise == null && !x.Dead && x.Room != cz && x.Room != C.KillRoom && x.Room != C.FoundRoom
                                                 && x.T1 >= Math.Max(at + 2, c0 + 1) && x.T0 <= Math.Min(end, c1 - 1))
                                   .OrderBy(x => Math.Abs(x.T0 - C.KillClock)).ThenBy(x => x.T0).FirstOrDefault();
                if (s == null) continue;
                var p = NewPlate(C, "talk:" + w + ":zone:" + C.Culprit, PlateKind.Witness, PlateRole.Link, true, $"{G(w)}이(가) 본 것", $"{ClockFmt.Anchor(s.T0)}, {S.RoomName(s.Room)}. 보물찾기 중인 {G(C.Culprit)} — {G(w)}", s.Room, s.T0, s.T1,
                    new[] { new Prop { Kind = PropKind.AtPlace, A = C.Culprit, Room = s.Room, T0 = s.T0, T1 = s.T1 } });
                p.Witness = w; p.Seen = C.Culprit; p.Title = LineBank.FixParticles(p.Title); p.Face = LineBank.FixParticles(p.Face);
                p.Back = LineBank.FixParticles($"구역표가 {G(C.Culprit)}에게 준 곳은 {S.RoomName(cz)} — 그런데 그 시각 {G(C.Culprit)}은(는) {S.RoomName(s.Room)}에 있었다.");
                cands.Add(p); return;
            }
        }

        // ---- the masquerade (HouseEvents): a mask hides a face — not a height, and not what lands on it. The masks the house handed
        //      out by name came back at the end, one with a speck of blood (true); a witness who saw a mask near the scene put a
        //      name to it by its height, and the name was wrong (Mistaken: the one named was still in the hall, and says so)
        static void FeedMasque(CaseFacts C, List<Plate> cands)
        {
            var S = C.S; if (C.Culprit == null || S.Gatherings == null) return;
            var g = S.Gatherings.FirstOrDefault(x => x.Kind == "house:masque" && !x.Cancelled && x.Revs.Count > 0 && C.KillClock >= x.Cur.Start - 5 && C.KillClock <= x.Cur.Start + (HouseEvents.KindOf(x)?.Len ?? 80) + 5);
            if (g == null) return;
            var mask = S.Items.Values.Where(i => i.Type == "TheaterMask" && i.Surface.Contains("blood-speck") && HouseEvents.MaskOf(S, i) == C.Culprit).OrderBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
            if (mask != null)
            {
                var props = new[] { new Prop { Kind = PropKind.Bloodied, A = C.Culprit, Room = C.KillRoom, T0 = C.KillClock, T1 = C.KillClock } };
                var p = NewPlate(C, "record:mask:" + mask.Id, PlateKind.Record, PlateRole.Link, true, "돌려받은 가면",
                    LineBank.FixParticles($"가면의 밤이 끝나고 돌려받은 가면 하나에 작은 핏자국. 저택의 기록 — {G(C.Culprit)} 님께 드린 가면"), g.Cur.Room, g.Cur.Start, g.Cur.Start + 80, props);
                p.Back = LineBank.FixParticles($"{G(C.Culprit)}이(가) 쓴 가면에 피가 튀었다 — 가면을 쓴 채로 {G(C.VictimId)}을(를) 찔렀다.");
                cands.Add(p);
            }
            // who stayed in the hall through the kill (never marked leaving it then)
            bool Stayed(string x) => g.Arrived.ContainsKey(x) && !S.Ledger.Any(e => e.Type == "GatheringLeave" && e.Actor == x && e.Data == g.Id && e.Clock >= C.KillClock - 45 && e.Clock <= C.KillClock + 10);
            var cul = S.A(C.Culprit); if (cul == null) return;
            var near = new HashSet<int> { C.KillRoom, C.FoundRoom }; foreach (var nb in S.Layout.Neighbors(C.KillRoom)) near.Add(nb);
            foreach (var w in C.Npcs.Where(x => x != C.Culprit).OrderBy(x => x, StringComparer.Ordinal))
            {
                var s = S.K(w).Sightings.Where(x => x.Target == C.Culprit && x.Disguise == "TheaterMask" && !x.Dead && near.Contains(x.Room) && x.T1 >= C.KillClock - 30 && x.T0 <= C.KillClock + 10)
                                   .OrderBy(x => Math.Abs(x.T0 - C.KillClock)).FirstOrDefault();
                if (s == null) continue;
                // the name the height suggested to them: someone at the masquerade of about the same height whom they know
                string guess = g.Arrived.Keys.Where(x => x != C.Culprit && x != C.VictimId && x != w && x != Cast.Player && S.A(x)?.Alive == true && C.Npcs.Contains(x) && Stayed(x)
                                                         && Math.Abs((S.A(x).Def.HeightCm) - cul.Def.HeightCm) <= 6)
                                             .OrderByDescending(x => S.HasRel(w, x) ? S.R(w, x).Like + S.R(w, x).Trust : 0f).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault();
                string hw = HouseEvents.HeightWord(cul);
                if (guess == null)
                {
                    // nobody of that height to mistake them for: the sighting is only a mask and a height (true)
                    var tp = NewPlate(C, "talk:" + w + ":mask", PlateKind.Witness, PlateRole.Confirm, true, $"{G(w)}이(가) 본 것", $"{ClockFmt.Anchor(s.T0)}, {S.RoomName(s.Room)}. 가면 쓴 사람, {hw} — {G(w)}", s.Room, s.T0, s.T1,
                        new[] { new Prop { Kind = PropKind.Disguised, A = null, Room = s.Room, T0 = s.T0, T1 = s.T1, Value = hw } });
                    tp.Witness = w; tp.Title = LineBank.FixParticles(tp.Title); tp.Face = LineBank.FixParticles(tp.Face);
                    tp.Back = LineBank.FixParticles($"그 무렵 가면을 쓴 누군가가 {S.RoomName(s.Room)}에 있었다 — {hw}."); cands.Add(tp);
                    return;
                }
                var props = new[] { new Prop { Kind = PropKind.AtPlace, A = guess, Room = s.Room, T0 = s.T0, T1 = s.T1 }, new Prop { Kind = PropKind.Culprit, A = guess, B = C.VictimId, Value = "opportunity" } };
                var p = NewPlate(C, "talk:" + w + ":mask:" + guess, PlateKind.Witness, PlateRole.Mistaken, false, LineBank.FixParticles($"{G(w)}이(가) 본 것"),
                    $"{ClockFmt.Anchor(s.T0)}, {S.RoomName(s.Room)}. 가면을 썼지만 {hw} — {G(guess)} 같았다 — {G(w)}", s.Room, s.T0, s.T1, props);
                p.Witness = w; p.Seen = guess; p.Points = guess; p.Owner = guess; p.Origin = "가면"; p.Routes.Add("ask:" + guess); p.Users.Add("theory:" + w);
                p.Face = LineBank.FixParticles(p.Face);
                p.Back = LineBank.FixParticles($"키가 비슷했을 뿐 — {G(guess)}은(는) 그때 가면을 쓴 채 {S.RoomName(g.Cur.Room)}에 있었다.");
                cands.Add(p); return;
            }
        }

        // ---- the house's own evenings (HouseEvents): the house keeps the door of the room it lit for them — who stepped out, when,
        //      and when they came back. A mask hides a face from the other guests, not from the house that handed it out.
        static void FeedDoor(CaseFacts C, List<Plate> cands)
        {
            var S = C.S; if (S.Gatherings == null) return;
            var g = S.Gatherings.Where(x => x.Kind != null && x.Kind.StartsWith("house:", StringComparison.Ordinal) && x.Kind != "house:hunt" && x.Kind != "house:phone" && !x.Cancelled && x.Revs.Count > 0
                                           && C.KillClock >= x.Cur.Start - 10 && C.KillClock <= x.Cur.Start + (HouseEvents.KindOf(x)?.Len ?? 80) + 20)
                                .OrderBy(x => Math.Abs(x.Cur.Start - C.KillClock)).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
            if (g == null) return;
            int room = g.Cur.Room; double end = g.Cur.Start + (HouseEvents.KindOf(g)?.Len ?? 80);
            var outs = new List<(string who, double left, double back)>();
            foreach (var e in S.Ledger.Where(e => e.Type == "GatheringLeave" && e.Data == g.Id && e.Actor != null && e.Clock >= C.KillClock - 60 && e.Clock <= C.KillClock + 10).OrderBy(e => e.Seq))
            {
                var ret = S.Ledger.FirstOrDefault(r => r.Type == "GatheringReturn" && r.Actor == e.Actor && r.Seq > e.Seq && r.Data != null && r.Data.StartsWith(g.Id + " ", StringComparison.Ordinal));
                outs.Add((e.Actor, e.Clock, ret?.Clock ?? -1));
            }
            if (outs.Count == 0) return;
            // the victim and the culprit first (the court needs those two lines), the rest in the order they went; four lines at most
            var shown = outs.OrderBy(o => o.who == C.VictimId || o.who == C.Culprit ? 0 : 1).ThenBy(o => o.left).Take(4).OrderBy(o => o.left).ToList();
            int more = outs.Select(o => o.who).Distinct().Count() - shown.Select(o => o.who).Distinct().Count();
            string Line((string who, double left, double back) o) => o.back >= 0 ? $"{G(o.who)} {ClockFmt.AnchorRange(o.left, o.back)}" : $"{G(o.who)} {ClockFmt.Anchor(o.left)} 나간 뒤 끝까지 안 돌아옴";
            var props = shown.Select(o => new Prop { Kind = PropKind.NotAtPlace, A = o.who, Room = room, T0 = o.left + 1, T1 = o.back >= 0 ? o.back - 1 : Math.Max(end, C.KillClock + 10) }).ToList();
            var cul = shown.Where(o => o.who == C.Culprit).Select(o => ((string who, double left, double back)?)o).FirstOrDefault();
            var p = NewPlate(C, "record:door:" + g.Id, PlateKind.Record, cul != null ? PlateRole.Link : PlateRole.Seam, true, "저택의 출입 기록",
                $"{g.Label} — {S.RoomName(room)}을(를) 잠시 비운 사람: {string.Join(" · ", shown.Select(Line))}{(more > 0 ? $" 외 {TrialSystem.Kor(more)} 명" : "")}", room, shown[0].left, end, props);
            if (cul != null)
            {
                p.Seen = C.Culprit;
                p.Back = LineBank.FixParticles($"{G(C.Culprit)}은(는) {ClockFmt.Anchor(cul.Value.left)} {S.RoomName(room)}을(를) 나갔다" + (cul.Value.back < 0 || cul.Value.back >= C.KillClock ? $" — {G(C.VictimId)}이(가) 공격당한 건 그 사이다." : "."));
            }
            else p.Back = LineBank.FixParticles($"저택의 기록으로는 그 사이 {S.RoomName(room)}을(를) 비운 사람은 이들뿐이다.");
            cands.Add(p);
        }

        // ---- the telephone night (HouseEvents): the call order the house read out in the morning (everyone knew who would be alone
        //      in the telephone room, and when), and the house's log — a call nobody picked up is a minute by which the victim
        //      was already down, or not where they should have been
        static void FeedCalls(CaseFacts C, List<Plate> cands)
        {
            var S = C.S; var g = HouseEvents.CallsAt(S, C.KillClock); if (g == null) return;
            var order = g.Status.Keys.Select(x => (x, t: HouseEvents.CallSlot(S, g, x))).Where(x => x.t >= 0).OrderBy(x => x.t).ThenBy(x => x.x, StringComparer.Ordinal).ToList();
            if (order.Count == 0) return;
            int pr = S.Layout.Rooms.Where(r => r.Type == RoomType.PhoneRoom).OrderBy(r => r.Id).Select(r => r.Id).DefaultIfEmpty(-1).First();
            double Slot(LedgerEvent e) { int i = e.Data?.IndexOf(':') ?? -1; return i >= 0 && double.TryParse(e.Data.Substring(i + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : e.Clock - 3; }
            var missed = S.Ledger.Where(e => e.Type == "PhoneNoAnswer" && e.Data != null && e.Data.StartsWith(g.Id + ":", StringComparison.Ordinal)).OrderBy(e => e.Clock).ToList();
            int vi = order.FindIndex(x => x.x == C.VictimId);
            var shown = (vi >= 0 ? order.Skip(Math.Max(0, vi - 2)).Take(5) : order.Take(5)).ToList();
            string Line((string x, double t) c) => $"{ClockFmt.Mark(c.t, false)} {G(c.x)}" + (missed.Any(e => e.Target == c.x) ? "(응답 없음)" : "");
            var p = NewPlate(C, "record:calls:" + g.Id, PlateKind.Record, PlateRole.Confirm, true, "전화의 밤 순서표", string.Join(" · ", shown.Select(Line)), pr, order[0].t, order[order.Count - 1].t + HouseEvents.CallLen, null);
            var vm = missed.FirstOrDefault(e => e.Target == C.VictimId);
            p.Back = vm != null ? LineBank.FixParticles($"{ClockFmt.Mark(Slot(vm), true)}, {G(C.VictimId)}의 차례에 전화를 받는 사람이 없었다 — 그때 이미 쓰러져 있었거나, 전화실에 오지 못했다.")
                                : "순서는 아침에 모두가 들었다. 누가 언제 전화실에 혼자 있을지, 저택 안의 모두가 알았다.";
            cands.Add(p);
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
                case Axis.Time: return c.Trick == "Tod" ? $"「{ClockFmt.Vague(c.Presented.T0 + 15)}의 죽음」 — 정말 그 시각이었나" : cands.Any(p => p.True && p.Props.Any(x => x.Kind == PropKind.Heard)) ? $"「{ClockFmt.Vague(C.KillClock)}의 소리」 — 그 시각, 무슨 일이 있었나" : $"「{ClockFmt.Vague(C.KillClock)}」 — 그 시각, 무슨 일이 있었나";
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
            const int WitnessCap = 5, FakeWitnessCap = 3;
            var all = new List<Plate>();
            int Wit() => all.Count(p => p.Kind == PlateKind.Witness);
            bool Take(Plate p, string why)
            {
                if (p == null || all.Contains(p) || all.Any(x => x.Root == p.Root)) return false;
                // fakes never fill the witness stand on their own: the sound that sets the time and the culprit's ties keep a place
                if (p.Kind == PlateKind.Witness && !p.True && all.Count(x => x.Kind == PlateKind.Witness && !x.True) >= FakeWitnessCap) { d.Log.Add("fake witness cap: " + p.Root); return false; }
                if (p.Kind == PlateKind.Witness && Wit() >= WitnessCap && why != "link" && why != "sound" && why != "lie") { d.Log.Add("witness cap: " + p.Root + " (" + why + ")"); return false; }
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
            // fair play: up to two witnessed ties to the culprit (at the scene, the weapon in hand, blood after) — past the witness cap
            // (a sighting off the claimed room only breaks the alibi — it has its own place below, so the ties go first)
            bool OffClaim(Plate p) => p.Root.Contains(":off:") || p.Root.Contains(":left:");
            foreach (var t in trues.Where(p => p.Role == PlateRole.Link && p.Kind == PlateKind.Witness && p.Seen == C.Culprit).OrderBy(p => OffClaim(p) ? 1 : 0).ThenBy(p => p.Props.Any(x => x.Kind == PropKind.AtPlace) ? 0 : 1).ThenBy(p => Math.Abs(p.T0 - C.KillClock)).ThenBy(p => p.Root, StringComparer.Ordinal).Take(2)) Take(t, "link");
            Take(trues.Where(p => p.Role == PlateRole.Clear).OrderBy(p => p.Root, StringComparer.Ordinal).FirstOrDefault(), "clear");
            // the culprit's alibi must be breakable: one plate from a witness on the where-lie's own list, when none is in yet
            var whereLie = C.Pack?.Lies?.FirstOrDefault(l => l.Topic == "where");
            var story = C.Pack?.Story;
            bool BreaksWhere(Plate p) => whereLie != null && p.True
                && ((p.Kind == PlateKind.Witness && p.Seen == C.Culprit && p.Witness != null && whereLie.BrokenBy.Contains("witness:" + p.Witness))
                    || (story != null && story.ClaimRoom >= 0 && p.Props.Any(x => x.Kind == PropKind.NotAtPlace && x.A == C.Culprit && x.Room == story.ClaimRoom && x.T0 <= story.ClaimTo - 1 && x.T1 >= story.ClaimFrom + 1)));
            if (whereLie != null && !all.Any(BreaksWhere))
                Take(trues.Where(BreaksWhere).OrderBy(p => p.Props.Any(x => x.Kind == PropKind.AtPlace) ? 0 : 1).ThenBy(p => Math.Abs(p.T0 - C.KillClock)).ThenBy(p => p.Root, StringComparer.Ordinal).FirstOrDefault(), "lie");
            // whoever had the scapegoat elsewhere while the culprit says they saw them heading for the scene (the lie's breaker)
            Take(trues.Where(p => p.Role == PlateRole.Seam && p.Kind == PlateKind.Witness && p.Seen != null && p.Seen == C.Scapegoat).OrderBy(p => p.Root, StringComparer.Ordinal).FirstOrDefault(), "lie");
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
                // found by 민혁: examined, in his evidence, or told to him by this very witness during the investigation (what they
                // saw of that person around that time) — then it is his to lay, not a plate borrowed in court
                bool heard = p.Kind == PlateKind.Witness && p.Witness != null && p.Seen != null
                    && S.K(Cast.Player).Statements.Any(st => st.Speaker == p.Witness && st.Prop != null && (st.Prop.A == p.Seen || st.Prop.B == p.Seen) && Math.Abs(st.Prop.T0 - p.T0) < 25);
                bool mine = heard || S.K(Cast.Player).Evidence.Any(e => e.Root != null && (e.Root == p.Root || e.Root.EndsWith(p.Root))) || S.K(Cast.Player).Examined.Contains(p.Root) || p.Kind == PlateKind.Body;
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
            if (d.Plates.Count(p => p.Kind == PlateKind.Witness) > 7) d.Log.Add("too many witness plates");
        }
    }
}
