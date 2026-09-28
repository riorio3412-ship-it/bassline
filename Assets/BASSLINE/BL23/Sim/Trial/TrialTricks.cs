using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Signature tricks (SetPieces: Seal / Tod / Message / Swap) in the class trial: the false first impression each trick was
    /// built to create is voiced by someone who holds it — from their own knowledge (announcements, examinations, what lay at the
    /// scene). Whether it can be broken depends on the clues the player actually gathered (Logic rules LR04/LR07/LR08).
    /// </summary>
    public static partial class TrialSystem
    {
        static string Jc(string w, string polite, string casual, bool pol) => w + LineBank.Josa(w, pol ? polite : casual);

        /// <summary>Swap: a bloodied object lies by the body — someone takes it for the weapon.</summary>
        static void ImpressionSwap(Simulation sim, TrialState T, List<Actor> npcs, Incident inc, Rng rng)
        {
            var S = sim.S;
            var it = S.Items.Values.Where(i => i.Surface != null && i.Surface.Contains("smeared") && i.Room == inc.FoundRoom && i.Holder == null).OrderBy(i => i.Id, System.StringComparer.Ordinal).FirstOrDefault();
            var def = it != null ? ItemCatalog.Get(it.Type) : null; if (def == null) return;
            if (T.Claims.Any(c => c.Prop?.Kind == PropKind.WeaponType && c.Prop.Item == it.Type)) return;
            var sp = npcs.Where(a => !S.K(a.Id).Examined.Contains("body:" + inc.Victim)).OrderBy(_ => rng.F()).FirstOrDefault() ?? npcs.OrderBy(_ => rng.F()).FirstOrDefault(); if (sp == null) return;
            bool pol = Polite(sp.Id); string item = def.Kor;
            var p = new Prop { Kind = PropKind.WeaponType, A = inc.Victim, Value = (def.Dmg == DamageType.None ? DamageType.Blunt : def.Dmg).ToString(), Item = it.Type, Room = inc.FoundRoom };
            var c = ClaimText(T, sim, sp.Id, p, pol ? $"흉기는 시신 옆에 떨어져 있던 {Jc(item, "이에요", "예요", true)}. 피까지 묻어 있었잖아요." : $"흉기는 시신 옆에 떨어져 있던 {Jc(item, "이야", "야", false)}. 피까지 묻어 있었잖아.", "cause", Emotion.Angry);
            NpcReactions(sim, T, c);
        }

        /// <summary>Tod: an examiner states the death window read off the body (shifted if the body was warmed or chilled).</summary>
        static void ImpressionTod(Simulation sim, TrialState T, List<Actor> npcs, Incident inc, Rng rng)
        {
            var S = sim.S; if (T.Claims.Any(c => c.Prop?.Kind == PropKind.DeathWindow)) return;
            foreach (var a in npcs.OrderBy(_ => rng.F()))
            {
                var w = S.K(a.Id).Evidence.Where(e => e.Kind == EvKind.Body && e.Subject == inc.Victim).SelectMany(e => e.Props).FirstOrDefault(p => p.Kind == PropKind.DeathWindow && p.Value == "exam");
                if (w == null) continue;
                bool pol = Polite(a.Id); string V = Cast.GivenOf(inc.Victim), span = ClockFmt.VagueRange(Math.Min(w.T0, w.T1), Math.Max(w.T0, w.T1));
                var c = ClaimText(T, sim, a.Id, w.Clone(), pol ? $"시신을 직접 살펴봤어요. 체온으로 보면 {V} 씨가 숨진 건 {Jc(span, "으로", "로", true)} 보여요." : $"시신을 직접 봤어. 체온으로 보면 {Jc(V, "이", "가", false)} 죽은 건 {Jc(span, "으로", "로", false)} 보여.", "time");
                NpcReactions(sim, T, c);
                return;
            }
        }

        /// <summary>Seal: the butler announced a sealed room — "nobody could have left, so it was suicide".</summary>
        static bool ImpressionSeal(Simulation sim, TrialState T, List<Actor> npcs, Incident inc, Rng rng)
        {
            var S = sim.S;
            var holders = npcs.Where(a => S.K(a.Id).Facts.Contains("sealedroom:" + inc.Victim)).ToList(); if (holders.Count == 0) return false;
            var sp = rng.Weighted(holders, a => IsCulprit(S, a.Id) ? 2.0 : 1.0);
            bool pol = Polite(sp.Id); string V = Cast.GivenOf(inc.Victim), room = S.RoomName(inc.FoundRoom);
            var p = new Prop { Kind = PropKind.DoorLocked, A = inc.Victim, Room = inc.FoundRoom, T0 = inc.DiscoverClock, T1 = inc.DiscoverClock, Value = "sealed" };
            var c = ClaimText(T, sim, sp.Id, p, pol ? $"{Jc(room, "은", "는", true)} 안에서 잠겨 있었어요. 아무도 나갈 수 없었으니까, {V} 씨는 스스로 목숨을 끊은 거예요." : $"{Jc(room, "은", "는", false)} 안에서 잠겨 있었어. 아무도 나갈 수 없었으니까, {Jc(V, "은", "는", false)} 스스로 목숨을 끊은 거야.", "place", Emotion.Sad);
            NpcReactions(sim, T, c);
            return true;
        }

        /// <summary>Message: a blood-written letter by the body is read as the victim naming their killer.</summary>
        static void ImpressionMessage(Simulation sim, TrialState T, List<Actor> npcs, Incident inc, Rng rng)
        {
            var S = sim.S; if (T.Claims.Any(c => c.Prop?.Kind == PropKind.TraceAt && c.Prop.Value != null && c.Prop.Value.StartsWith("bloodwriting"))) return;
            Actor sp = null; Prop msg = null;
            foreach (var a in npcs.OrderBy(_ => rng.F()))
            {
                msg = S.K(a.Id).Evidence.SelectMany(e => e.Props).FirstOrDefault(p => p.Kind == PropKind.TraceAt && p.A == inc.Victim && p.Value != null && p.Value.StartsWith("bloodwriting") && p.B != a.Id);
                if (msg != null) { sp = a; break; }
            }
            if (msg == null)
            {
                // the letter is in plain sight: whoever found the body saw it
                var tr = S.Traces.FirstOrDefault(t => t.Type == "BloodWriting" && t.Victim == inc.Victim && !t.Cleaned);
                sp = tr != null ? npcs.Where(a => inc.Discoverers.Contains(a.Id)).OrderBy(_ => rng.F()).FirstOrDefault() : null;
                if (sp == null || tr?.Note == null) return;
                string target = null, glyph = null; foreach (var kv in tr.Note.Split(';')) { var q = kv.Split('='); if (q.Length == 2 && q[0] == "target") target = q[1]; if (q.Length == 2 && q[0] == "glyph") glyph = q[1]; }
                if (target == null || target == sp.Id) return;
                msg = new Prop { Kind = PropKind.TraceAt, A = inc.Victim, B = target, Room = tr.Room, T0 = tr.Clock, T1 = S.Clock, Value = "bloodwriting:" + glyph };
            }
            bool pol = Polite(sp.Id); string V = Cast.GivenOf(inc.Victim), g = msg.Value.Substring(msg.Value.IndexOf(':') + 1), B = Cast.GivenOf(msg.B);
            var c = ClaimText(T, sim, sp.Id, msg.Clone(), pol ? $"{V} 씨가 마지막 힘을 짜내 남긴 피 글씨 「{g}」… 이건 {B} 씨를 가리키는 거예요!" : $"{Jc(V, "이", "가", false)} 마지막 힘을 짜내 남긴 피 글씨 「{g}」… 이건 {Jc(B, "을", "를", false)} 가리키는 거야!", "suspicious", Emotion.Angry);
            c.Accused = null;
            NpcReactions(sim, T, c);
            // the named one panics — and points somewhere else
            var b = S.A(msg.B);
            if (b != null && b.Alive && !b.IsPlayer && T.Participants.Contains(b.Id))
            {
                Say(T, "line", b.Id, Polite(b.Id) ? "아, 아니에요! 그 글씨가 저라니, 말도 안 돼요!" : "아, 아니야! 그 글씨가 나라니, 말도 안 돼!", emo: Emotion.Fear, gesture: Anim.Cower, key: "panic");
                Testimony.UpdateSuspicion(sim, b);
                var k = S.K(b.Id);
                var other = k.Suspicion.Where(kv => kv.Key != b.Id && kv.Key != inc.Victim && T.Participants.Contains(kv.Key)).OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault() ?? sp.Id;
                var p = new Prop { Kind = PropKind.Culprit, A = other, B = inc.Victim };
                var acc = Claim(T, sim, b.Id, p, other == Cast.Player ? "accuse_player" : "accuse", new Dictionary<string, string> { { "t", "@" + other } }, IsCulprit(S, b.Id), "culprit", other == Cast.Player ? Cast.Player : null, Emotion.Angry);
                acc.Accused = other; acc.Premises = PremisesFor(sim, b, other);
                Sides(sim, T, acc);
            }
        }

        // ================================================================== twists & stakes
        /// <summary>A protector who withheld what they saw breaks when the one they shield is cornered — the relationship comes out.</summary>
        static void Reveals(Simulation sim, TrialState T)
        {
            var S = sim.S; var rng = S.R(Stream.Trial);
            foreach (var w in T.Withheld.ToList())
            {
                var p = w.Split('|'); if (p.Length < 3 || T.GameLog.Contains("reveal:" + p[0])) continue;
                string prot = p[0], shielded = p[1]; var claim = T.Claims.FirstOrDefault(c => c.Id == p[2]);
                var a = S.A(prot); if (a == null || !a.Alive || claim == null) continue;
                bool cornered = T.Claims.Any(c => c.Accused == shielded && c.Status != "refuted" && c.Status != "retracted");
                if (!cornered || !rng.Chance(0.35 + (100 - a.Def.Composure) / 200.0)) continue;
                T.GameLog.Add("reveal:" + prot);
                var r = S.R(prot, shielded); string rel = r.Tags.Contains("lover") ? "연인" : r.Tags.Contains("family") ? "가족" : r.Tags.Contains("friend") ? "오랜 친구" : "가장 가까운 사람";
                T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "twist", Speaker = prot, Text = LineBank.FixParticles($"{Cast.GivenOf(prot)}이(가) 입을 연다"), Data = "reveal", Mode = T.Mode });
                Say(T, "line", prot, Polite(prot) ? $"그만해요…! 말할게요. {Cast.GivenOf(shielded)} 씨는 제 {Jc(rel, "이에요", "예요", true)}. 그래서 알면서도 입을 다물었어요." : $"그만…! 말할게. {Jc(Cast.GivenOf(shielded), "은", "는", false)} 내 {Jc(rel, "이야", "야", false)}. 그래서 알면서도 입 다물고 있었어.", emo: Emotion.Crying, gesture: Anim.Cry, key: "reveal");
                foreach (var id in T.Participants.Where(x => x != Cast.Player && x != prot)) S.K(id).Facts.Add($"bond:{prot}:{shielded}");
                if (claim.Status == "open" || claim.Status == "supported")
                {
                    var (v, ev) = Logic.Best(S, claim.Prop, S.K(prot).Evidence.Concat(SightingsAsEvidence(sim, a)));
                    if (v != null && (v.Result == LogicResult.Contradict || v.Result == LogicResult.Conditional || v.Result == LogicResult.LimitScope))
                    {
                        Line(T, sim, prot, "object", claim.Speaker, new Dictionary<string, string> { { "t", "@" + claim.Speaker } }, emo: Emotion.Angry, gesture: Anim.Point);
                        T.Beats.Add(new TrialBeat { N = T.Beats.Count, Kind = "result", Speaker = prot, Text = v.Why, ClaimId = claim.Id, Data = v.Result.ToString() });
                        ApplyVerdict(sim, T, claim, v, prot, ev);
                    }
                }
                return;   // one twist at a time
            }
        }

        /// <summary>A lapse by the player: the streak breaks, and the loudest voice in the room pulls the jury toward its favourite.</summary>
        internal static (string who, string text) Lapse(Simulation sim, TrialState T)
        {
            var S = sim.S; var rng = S.R(Stream.Trial); T.Streak = 0;
            if (!rng.Chance(0.55)) return (null, null);
            var voices = T.Participants.Where(x => x != Cast.Player && S.A(x)?.Alive == true).Select(S.A).ToList(); if (voices.Count == 0) return (null, null);
            var pick = rng.Weighted(voices, a => { if (IsCulprit(S, a.Id)) return 2.0; var k = S.K(a.Id); var top = k.Suspicion.Where(kv => kv.Key != a.Id).Select(kv => kv.Value).DefaultIfEmpty(0).Max(); return top > 0.4f ? top : 0.05; });
            string target = IsCulprit(S, pick.Id) ? Testimony.Scapegoat(sim, pick) : S.K(pick.Id).Suspicion.Where(kv => kv.Key != pick.Id && T.Participants.Contains(kv.Key)).OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
            if (target == null || target == pick.Id || !T.Participants.Contains(target)) return (null, null);
            foreach (var id in T.Participants.Where(x => x != Cast.Player && x != pick.Id && x != target)) { var k = S.K(id); float trust = MathX.Clamp01(0.5f + S.R(id, pick.Id).Trust); k.Suspicion[target] = (k.Suspicion.TryGetValue(target, out var v) ? v : 0) + 0.06f * trust; }
            string G = target == Cast.Player ? "당신" : Cast.GivenOf(target);
            string text = target == Cast.Player ? (Polite(pick.Id) ? "…헷갈리게 하는 건 당신이잖아요. 왜 자꾸 판을 흔드는 거죠?" : "…헷갈리게 하는 건 너잖아. 왜 자꾸 판을 흔드는 건데?")
                        : Polite(pick.Id) ? $"…그것 보세요. 역시 수상한 건 {G} 씨예요." : $"…봐, 헛다리잖아. 역시 수상한 건 {Jc(G, "이야", "야", false)}.";
            Say(T, "line", pick.Id, text, emo: Emotion.Smirk, gesture: Anim.Point, key: "steer");
            return (pick.Id, text);
        }

        /// <summary>A correct move: two in a row and the court's hourglass turns itself (more candle time / a free press).</summary>
        internal static bool Reward(TrialState T)
        {
            T.Streak++;
            if (T.Streak >= 2) { T.Streak = 0; T.Hourglass = Math.Min(3, T.Hourglass + 1); Say(T, "system", null, "법정의 모래시계가 저절로 뒤집힌다. 촛불이 조금 더 오래 타겠다.", data: "Hourglass", key: "hourglass"); return true; }
            return false;
        }
    }
}
