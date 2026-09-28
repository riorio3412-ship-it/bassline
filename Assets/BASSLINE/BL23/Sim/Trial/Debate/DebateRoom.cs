using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// The room between the floors — what makes a 심판 long and dense rather than a string of quick rounds: the roll call
    /// (everyone says where they were at the hour; pairs confirm each other; name plates fall), the cause-of-death riddle
    /// that usually opens the debate, holders who amend a fallen theory aloud, bystanders who react, and 민혁's closing
    /// argument that rebuilds the night from what the court settled. Every line comes from what that person knows.
    /// </summary>
    public static partial class TrialSystem
    {
        // ================================================================== 각자의 자리 (the roll call)
        /// <summary>Where each resident was at the hour, from their own movements (the ledger's Enter events are their memory
        /// of where they walked) and whom they saw there.</summary>
        static Dictionary<string, (int room, string with, bool near)> Whereabouts(GameState S, DebateState D, List<string> who, double t)
        {
            var last = new Dictionary<string, int>();
            var set = new HashSet<string>(who);
            foreach (var e in S.Ledger)
                if (e.Type == "Enter" && e.Clock <= t && e.Actor != null && set.Contains(e.Actor) && e.Room >= 0) last[e.Actor] = e.Room;
            var res = new Dictionary<string, (int, string, bool)>();
            foreach (var id in who)
            {
                int room = last.TryGetValue(id, out var r) ? r : S.A(id)?.Room ?? -1;
                var k = S.K(id);
                var with = k.Sightings.Where(s => s.Room == room && s.T0 <= t + 5 && s.T1 >= t - 10 && s.IdConf > 0.6f && !s.Dead && s.Target != id && who.Contains(s.Target))
                                      .GroupBy(s => s.Target).OrderByDescending(g => g.Sum(s => s.T1 - s.T0)).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key).FirstOrDefault();
                res[id] = (room, with, room == D.KillRoom || room == D.FoundRoom);
            }
            return res;
        }

        static string PlaceWord(GameState S, string speaker, int room)
        {
            var r = S.Layout.Room(room);
            return r == null ? "어딘가" : r.Type == RoomType.Bedroom && r.Owner == speaker ? Reg(speaker, "제 방", "내 방") : r.Name;
        }

        static void RollCall(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate; var jur = Jurors(S, T);
            double t = D.KillClock;
            DTopic(T, $"각자의 자리 — {ClockFmt.Vague(t)}, 모두 어디에 있었나", "rollcall");
            var where = Whereabouts(S, D, jur, t);
            string chair = jur.Contains("P03") && "P03" != D.Target ? "P03" : PickHolder(sim, T, jur, x => Arg(x) + Obs(x) * 0.3, "chair", D.Target);
            if (chair != null) DSay(sim, T, chair, "rc_open", new Dictionary<string, string> { { "time", ClockFmt.Vague(t) } }, BeatKind.Line, "claim", null, Emotion.Neutral, Anim.Talk, 0.4f);
            // who speaks: pairs first (they confirm each other), then the ones alone, the culprit among them, never more than 9
            var said = new List<string>();
            var order = jur.OrderBy(x => where[x].with != null && where.ContainsKey(where[x].with) && where[where[x].with].with == x ? 0 : 1)
                           .ThenBy(x => DH(S, "rc:" + x)).ToList();
            int lines = 0;
            foreach (var j in order)
            {
                if (said.Contains(j) || lines >= 9) continue;
                var w = where[j];
                if (j == D.Target)
                {
                    int room = D.ClaimRoom >= 0 ? D.ClaimRoom : w.room;
                    string text = D.StoryText ?? DebateLines.Say(S, j, "rc_alone", new Dictionary<string, string> { { "time", ClockFmt.Vague(t) }, { "place", PlaceWord(S, j, room) } }, D.SpokenKeys, D.Seq.ToString());
                    DText(sim, T, j, text, BeatKind.Line, "claim", null, Emotion.Neutral, Anim.Talk, 0.35f);
                    D.Alone.Add(j); said.Add(j); lines++;
                    continue;
                }
                bool mutual = w.with != null && where.ContainsKey(w.with) && where[w.with].with == j && w.with != D.Target;
                if (mutual)
                {
                    DSay(sim, T, j, "rc_with", new Dictionary<string, string> { { "time", ClockFmt.Vague(t) }, { "place", PlaceWord(S, j, w.room) }, { "with", "@" + w.with } }, BeatKind.Line, "claim", null, Emotion.Neutral, Anim.Talk, 0.35f);
                    DSay(sim, T, w.with, "rc_confirm", new Dictionary<string, string> { { "target", "@" + j } }, BeatKind.Line, "claim", null, Emotion.Neutral, Anim.Talk, 0.3f);
                    D.Paired.Add(string.CompareOrdinal(j, w.with) < 0 ? j + "|" + w.with : w.with + "|" + j);
                    said.Add(j); said.Add(w.with); lines += 2;
                    continue;
                }
                if (w.near)
                {
                    DSay(sim, T, j, "rc_near", new Dictionary<string, string> { { "time", ClockFmt.Vague(t) }, { "place", PlaceWord(S, j, w.room) } }, BeatKind.Line, "claim", null, Emotion.Fear, Anim.Talk, 0.45f);
                    D.Alone.Add(j); said.Add(j); lines++;
                    continue;
                }
                if (w.with != null && w.with != D.Target)
                {
                    // one-sided: they saw someone there, but the other did not notice them
                    DSay(sim, T, j, "rc_with", new Dictionary<string, string> { { "time", ClockFmt.Vague(t) }, { "place", PlaceWord(S, j, w.room) }, { "with", "@" + w.with } }, BeatKind.Line, "claim", null, Emotion.Neutral, Anim.Talk, 0.35f);
                    DSay(sim, T, w.with, "rc_didnt_see", new Dictionary<string, string> { { "target", "@" + j } }, BeatKind.Interrupt, "object", null, Emotion.Surprised, Anim.Shrug, 0.45f);
                    D.Alone.Add(j); said.Add(j); lines += 2;
                    continue;
                }
                DSay(sim, T, j, "rc_alone", new Dictionary<string, string> { { "time", ClockFmt.Vague(t) }, { "place", PlaceWord(S, j, w.room) } }, BeatKind.Line, "claim", null, Emotion.Neutral, Anim.Talk, 0.35f);
                D.Alone.Add(j); said.Add(j); lines++;
            }
            int rest = jur.Count(x => !said.Contains(x));
            if (rest > 0) DNarrate(T, $"나머지 {Kor(rest)} 사람도 저마다 있던 곳을 말한다. 혼자였다는 사람이 많다.", BeatKind.Line);
            foreach (var x in jur.Where(x => !said.Contains(x))) D.Alone.Add(x);
            // the chair sums up: whoever two people vouch for can step aside
            var cleared = D.Paired.SelectMany(p => p.Split('|')).Distinct().Where(x => x != D.Target).OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (chair != null && cleared.Count > 0) DSay(sim, T, chair, "rc_sum", null, BeatKind.Line, "claim", null, Emotion.Neutral, Anim.Talk, 0.45f);
            FallPlates(sim, T, cleared, "서로 함께 있었다고 확인된 사람들");
        }

        /// <summary>Name plates turn down for those proven elsewhere; the rest are named (the culprit's never falls: constraints are true).</summary>
        internal static void FallPlates(Simulation sim, TrialState T, List<string> who, string why)
        {
            var S = sim.S; var D = T.Debate;
            var fall = who.Where(x => x != D.Target && x != Cast.Player && D.Standing.Contains(x) && !D.Fallen.Contains(x)).ToList();
            if (fall.Count == 0) return;
            foreach (var x in fall) { D.Fallen.Add(x); D.Standing.Remove(x); if (D.Reading.ContainsKey(x) && D.Reading.Values.Contains(x)) foreach (var j in Jurors(S, T).Where(j => D.Reading[j] == x).ToList()) D.Reading[j] = "?"; }
            var left = D.Standing.Where(x => x != Cast.Player).ToList();
            DResult(T, BeatKind.Stage, null, $"명패가 넘어간다 — {string.Join(", ", fall.Select(Given))} ({why}). 남은 명패 {Kor(left.Count)} 개.", "Named", intensity: 0.55f);
        }

        // ================================================================== 「상처」: what it was done with (the usual first debate)
        static void ThWound(Simulation sim, TrialState T, Mystery m)
        {
            var S = sim.S; var D = T.Debate; var jur = Jurors(S, T);
            var body = D.Deck.Plates.FirstOrDefault(p => p.Kind == PlateKind.Body);
            var wt = body?.Props.FirstOrDefault(p => p.Kind == PropKind.WeaponType); if (wt == null) return;
            var trueDmg = P(wt.Value);
            // the wrong guess: something the room saw someone carry that evening, else the other common weapon
            var coin = D.Deck.Plates.Where(p => p.Role == PlateRole.Coincidence && p.Props.Any(pp => pp.Kind == PropKind.Held && ItemCatalog.Get(pp.Item) != null && ItemCatalog.Get(pp.Item).Dmg != trueDmg)).OrderBy(p => p.N).FirstOrDefault();
            var held = coin?.Props.First(pp => pp.Kind == PropKind.Held);
            var wrong = held != null ? ItemCatalog.Get(held.Item).Dmg : trueDmg == DamageType.Blunt ? DamageType.Stab : DamageType.Blunt;
            string item = held != null ? ItemCatalog.Get(held.Item)?.Kor ?? WeaponWord(wrong) : WeaponWord(wrong);
            string guesser = PickHolder(sim, T, jur.Where(x => !Examined(S, x, D.Victim)), x => (1 - Obs(x)) + Fearful(S, x) * 0.5 + (coin != null && coin.Users.Contains("theory:" + x) ? 1 : 0), "wound:" + m.Id, coin?.Points, D.Target);
            if (guesser == null) guesser = PickHolder(sim, T, jur, x => 1 - Obs(x), "wound2:" + m.Id, coin?.Points, D.Target);
            if (guesser == null) return;
            var t1 = Voice(sim, T, m, guesser, Family.Self, held != null ? Basis.Hearsay : Basis.Guess, new Prop { Kind = PropKind.WeaponType, A = D.Victim, Value = wrong.ToString(), Item = held?.Item }, "th_wound_guess",
                new Dictionary<string, string> { { "victim", "@" + D.Victim }, { "item", item }, { "how", HowWord(wrong) } }, item, null, false, key: "claim", emo: Emotion.Fear, gesture: Anim.Talk, from: coin?.Witness);
            string examiner = PickHolder(sim, T, jur.Where(x => Examined(S, x, D.Victim)), x => Obs(x) + Arg(x) * 0.3, "woundx:" + m.Id, guesser, D.Target);
            var v = S.A(D.Victim); var w = v?.Body.Wounds.Where(x => !x.Postmortem).OrderByDescending(x => x.Sev).FirstOrDefault();
            if (examiner != null && w != null)
                Voice(sim, T, m, examiner, Family.Truth, Basis.Saw, new Prop { Kind = PropKind.WeaponType, A = D.Victim, Value = trueDmg.ToString() }, "th_weapon_wound",
                    new Dictionary<string, string> { { "wound", WoundText.Region(w.Region) + "의 " + WoundText.Type(w.Type) } }, WoundText.Type(w.Type), null, true, answers: "not:" + t1.Id, key: "object", emo: Emotion.Neutral, gesture: Anim.Think);
            if (D.Target != null && D.Target != guesser && jur.Contains(D.Target)) Back(sim, T, t1, D.Target, "th_culprit_wound", new Dictionary<string, string> { { "item", item } }, Emotion.Neutral, Anim.CrossArms);
            else PileOn(sim, T, t1);
        }

        static string HowWord(DamageType d) => d == DamageType.Choke ? "목을 졸랐을" : d == DamageType.Stab ? "찔렀을" : d == DamageType.Cut ? "베었을" : d == DamageType.Blunt ? "머리를 내리쳤을" : d == DamageType.Drown ? "물에 빠뜨렸을" : d == DamageType.Crush ? "짓눌렀을" : "해쳤을";

        // ================================================================== the people around a result
        /// <summary>A holder who amends their fallen theory out loud (§6.2 "evolve"): the reasoning moves forward in their voice.</summary>
        static bool Evolve(Simulation sim, TrialState T, Theory th)
        {
            var S = sim.S; var D = T.Debate; var m = D.Mysteries.FirstOrDefault(x => x.Id == th.Mystery);
            if (m == null || th.Holder == D.Target || !Jurors(S, T).Contains(th.Holder)) return false;
            string key = m.Trick == "Message" ? "ev_message" : m.Trick == "Place" ? "ev_place" : m.Trick == "Wound" ? "ev_wound" : m.Trick == "Tod" ? "ev_tod" : m.Trick == "Seal" ? "ev_seal" : m.Trick == "Swap" ? "ev_swap" : m.Trick == "Accident" || m.Trick == "Natural" || m.Trick == "Suicide" ? "ev_cause" : null;
            if (key == null || (Cast.Get(th.Holder)?.P.Pride ?? 0.5f) >= 0.75f || DH(S, "evolve:" + th.Id) > 0.65) return false;
            DSay(sim, T, th.Holder, key, new Dictionary<string, string> { { "victim", "@" + D.Victim }, { "place", S.RoomName(D.KillRoom) }, { "found", S.RoomName(D.FoundRoom) } }, BeatKind.Line, "recant", th.TrialClaim, Emotion.Surprised, Anim.Think, 0.5f, th.Id);
            th.EvolvedFrom = th.Id;
            return true;
        }

        /// <summary>One bystander says what the room is thinking after a turn (never more than one per result).</summary>
        static void Bystander(Simulation sim, TrialState T, string key, string salt, params string[] exclude)
        {
            var S = sim.S; var D = T.Debate;
            var who = PickHolder(sim, T, Jurors(S, T), x => Fearful(S, x) + (Cast.Get(x)?.Empathy ?? 50) / 100.0, "by:" + key + ":" + salt, exclude.Concat(new[] { D.Target }).ToArray());
            if (who == null) return;
            DSay(sim, T, who, key, new Dictionary<string, string> { { "victim", "@" + D.Victim }, { "target", "@" + D.Target } }, BeatKind.Line, "claim", null, key == "re_fear" ? Emotion.Fear : Emotion.Surprised, key == "re_fear" ? Anim.Cower : Anim.Surprised, 0.4f);
        }

        // ================================================================== 4막: 민혁 rebuilds the night (only what the court settled)
        static void ClosingArgument(Simulation sim, TrialState T)
        {
            var S = sim.S; var D = T.Debate; string V = sim.CallName(Cast.Player, D.Victim);
            string C(string id) => sim.CallName(Cast.Player, id);
            bool Settled(string trick) => D.Mysteries.Any(m => m.Trick == trick && m.State == "settled");
            var lines = new List<string>();
            lines.Add("처음부터 되짚어 볼게요. 그날 밤에 있었던 일을요.");
            string when = D.Plaques.Any(q => q.By == "house") || Settled("Tod") ? ClockFmt.Vague(D.KillClock) : null;
            string site = Settled("Place") ? S.RoomName(D.KillRoom) : S.RoomName(D.FoundRoom);
            lines.Add(when != null ? $"사건은 {when}, {site}에서 일어났어요." : $"사건은 {site}에서 일어났어요.");
            var M = D.Mind; var inc = S.Incidents.TryGetValue(D.Incident, out var i) ? i : null; var weapon = inc?.Weapon != null ? S.I(inc.Weapon) : null;
            bool weaponShown = weapon != null && D.Deck.Plates.Any(p => (p.State == PlateState.Sealed || M.Used.Contains(p.Id)) && p.Props.Any(pp => pp.Kind == PropKind.Held && pp.A == D.Target && pp.Item == weapon.Type));
            if (Settled("Wound") || weaponShown) lines.Add(weaponShown ? $"범인은 거기서 {V}을(를) {weapon.Kor}(으)로 쳤어요." : $"범인은 거기서 {V}을(를) {WeaponWord(inc?.Dmg ?? DamageType.Blunt)}(으)로 쳤어요.");
            if (Settled("Place") && D.KillRoom != D.FoundRoom) lines.Add($"{V}은(는) {S.RoomName(D.FoundRoom)}까지 갔지만, 거기서 쓰러졌어요.");
            if (Settled("Message"))
            {
                var frame = D.Deck.Plates.FirstOrDefault(p => p.Role == PlateRole.Frame && p.Root.StartsWith("trace:"));
                if (frame?.Points != null) lines.Add($"범인은 {V}의 손 옆에 「{Glyph(frame.Face)}」 자를 써서, {C(frame.Points)}에게 죄를 뒤집어씌우려 했어요.");
            }
            if (Settled("Seal")) lines.Add($"그리고 {S.RoomName(D.FoundRoom)} 문을 밖에서 잠가, 밀실처럼 보이게 했어요.");
            if (Settled("Swap")) lines.Add("시신 옆에는 피를 발라 둔 엉뚱한 물건을 흉기처럼 놓아두었고요.");
            if (Settled("Tod")) lines.Add("시신을 데우거나 식혀서, 숨진 시각까지 속이려 했어요.");
            var fa = D.Deck.Plates.FirstOrDefault(p => p.Role == PlateRole.FalseAlibi && p.State == PlateState.Flipped);
            if (fa != null) lines.Add($"그다음 {S.RoomName(fa.Room)}(으)로 가서, {C(fa.Witness)} 눈에 띄는 자리에 있었어요. {ClockFmt.Vague(fa.T0)}에요. 그게 알리바이가 됐죠.");
            if (D.Accused != null && D.Accused == D.Target && M.Broken) lines.Add($"그 모든 걸 할 수 있었던 사람은 한 사람뿐이에요. …그렇죠, {C(D.Target)}?");
            else if (D.Accused != null) { string n = C(D.Accused); lines.Add($"제가 지목한 사람은 {n}{LineBank.Josa(n, "이에요")}."); }
            foreach (var l in lines) DText(sim, T, Cast.Player, LineBank.FixParticles(l), BeatKind.Summary, key: "summary", emo: Emotion.Neutral, gesture: l.StartsWith("그 모든") ? Anim.Point : Anim.Talk, intensity: 0.6f);
            if (D.Accused == D.Target && M.Broken) DNarrate(T, $"{Given(D.Target)}은(는) 대답하지 않는다.", BeatKind.Line, intensity: 0.7f);
        }
    }
}
