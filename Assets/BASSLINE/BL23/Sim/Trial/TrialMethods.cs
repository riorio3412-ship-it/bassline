using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// The second-wave methods in the trial (Methods*.cs). Each staged scene plants a first impression that someone at the
    /// stand voices from what they saw themselves — the body at the foot of the stairs, the dead machine, a sleeper who never
    /// woke, a farewell note — and the player breaks it with the one clue made for it (Logic.cs):
    ///   사고(추락) ← 가슴의 손바닥 멍 / 뜯긴 단추 · 사고(감전) ← 칼로 벗긴 전선 · 사고(익사) ← 손톱 밑 살갗·물속에 누른 자국
    ///   자연사 ← 얼굴을 누른 자국·눌린 베개 / 중독 증상 · 자살 ← 흉내 낸 필체 · 밀실 ← 문틈의 열쇠 자국 / 미리 넣은 독
    /// Speakers only say what they could know (the body as found, the house's announcement, the note they read).
    /// </summary>
    public static partial class TrialSystem
    {
        /// <summary>Topic hook ("cause" / "place"): the staged first impression of this death, if its scene carries one.</summary>
        static void ImpressionMethods(Simulation sim, TrialState T, List<Actor> npcs, Incident inc, Rng rng, string topic)
        {
            var S = sim.S; var v = S.A(inc?.Victim); if (v == null || npcs.Count == 0) return;
            if (topic == "cause") { ImpressionAccident(sim, T, npcs, inc, v, rng); ImpressionNatural(sim, T, npcs, inc, v, rng); ImpressionDismember(sim, T, npcs, inc, v, rng); }
            if (topic == "place") ImpressionSuicide(sim, T, npcs, inc, v, rng);
        }

        static Actor PickSpeaker(Simulation sim, List<Actor> npcs, Incident inc, Rng rng, Func<Actor, bool> knows)
        {
            var S = sim.S;
            var c = npcs.Where(a => a.Id != inc.Victim && knows(a)).OrderBy(a => a.Id).ToList();
            if (c.Count == 0) c = npcs.Where(a => a.Id != inc.Victim && inc.Discoverers.Contains(a.Id)).OrderBy(a => a.Id).ToList();
            if (c.Count == 0) c = npcs.Where(a => a.Id != inc.Victim).OrderBy(a => a.Id).ToList();
            return c.Count == 0 ? null : rng.Weighted(c, a => IsCulprit(S, a.Id) ? 2.2 : inc.Discoverers.Contains(a.Id) ? 1.5 : 1.0);
        }

        /// <summary>Fall / shock / crushing / drowning: "nobody did it — it was an accident".</summary>
        static void ImpressionAccident(Simulation sim, TrialState T, List<Actor> npcs, Incident inc, Actor v, Rng rng)
        {
            var S = sim.S; if (T.Claims.Any(c => c.Prop?.Kind == PropKind.Culprit && c.Prop.A == null && c.Prop.Value != null && c.Prop.Value.StartsWith("accident"))) return;
            var main = v.Body.Wounds.Where(w => !w.Postmortem).OrderByDescending(w => w.Sev).FirstOrDefault();
            if (main == null) return;
            if (v.Body.Wounds.Any(w => !w.Postmortem && (w.Type == DamageType.Stab || w.Type == DamageType.Cut || (w.Type == DamageType.Blunt && w.CauseEvent != null && !w.CauseEvent.StartsWith("trap"))))) return;   // a blade or a blunt blow reads as an attack
            string kind = main.Type == DamageType.Fall ? "fall" : main.Type == DamageType.Shock ? "shock" : main.Type == DamageType.Crush ? "crush" : main.Type == DamageType.Drown ? "drown" : null;
            if (kind == null) return;
            var sp = PickSpeaker(sim, npcs, inc, rng, a => S.K(a.Id).KnownDead.Contains(v.Id)); if (sp == null) return;
            bool pol = Polite(sp.Id); string V = Cast.GivenOf(v.Id), room = S.RoomName(inc.FoundRoom);
            string text = kind switch
            {
                "fall" => pol ? $"{V} 씨는 발을 헛디딘 거예요. {room}에서 굴러떨어진 사고예요. 누가 죽인 게 아니라고요." : $"{Jc(V, "은", "는", false)} 발을 헛디딘 거야. {room}에서 굴러떨어진 사고라고. 누가 죽인 게 아니야.",
                "shock" => pol ? $"낡은 기계가 누전된 거예요. {V} 씨는 운 나쁘게 거기 손을 댔을 뿐이에요. 사고예요." : $"낡은 기계가 누전된 거야. {Jc(V, "은", "는", false)} 운 나쁘게 거기 손을 댔을 뿐이고. 사고라고.",
                "crush" => pol ? $"무거운 가구가 저절로 넘어진 거예요. 하필 그 밑에 {V} 씨가 있었던 거고요. 사고예요." : $"무거운 가구가 저절로 넘어진 거야. 하필 그 밑에 {Jc(V, "이", "가", false)} 있었던 거고. 사고라고.",
                _ => pol ? $"{V} 씨는 혼자 수영하다 물에 빠진 거예요. 사고예요. 누가 그런 게 아니라고요." : $"{Jc(V, "은", "는", false)} 혼자 수영하다 빠진 거야. 사고라고. 누가 그런 게 아니야.",
            };
            var p = new Prop { Kind = PropKind.Culprit, A = null, B = v.Id, Room = inc.FoundRoom, T0 = inc.DiscoverClock, T1 = inc.DiscoverClock, Value = "accident:" + kind };
            var c = ClaimText(T, sim, sp.Id, p, LineBank.FixParticles(text), "cause", Emotion.Sad);
            NpcReactions(sim, T, c);
        }

        /// <summary>No wound to speak of, found in bed: "they died in their sleep".</summary>
        static void ImpressionNatural(Simulation sim, TrialState T, List<Actor> npcs, Incident inc, Actor v, Rng rng)
        {
            var S = sim.S; if (T.Claims.Any(c => c.Prop?.Kind == PropKind.Culprit && c.Prop.A == null && c.Prop.Value == "natural")) return;
            var visible = v.Body.Wounds.Where(w => !w.Postmortem && !(w.Region == BodyRegion.Head && w.Type == DamageType.Choke)).ToList();
            if (visible.Count > 0) return;
            if (S.Items.Values.Any(i => i.NoteFrom != null && i.NoteFrom.EndsWith(":" + v.Id) && i.NoteFrom.StartsWith("forged:"))) return;   // a note makes it read as suicide instead
            var room = S.Layout.Room(inc.FoundRoom); if (room == null || room.Type != RoomType.Bedroom) return;
            var sp = PickSpeaker(sim, npcs, inc, rng, a => S.K(a.Id).KnownDead.Contains(v.Id)); if (sp == null) return;
            bool pol = Polite(sp.Id); string V = Cast.GivenOf(v.Id);
            string text = pol ? $"다친 데 하나 없이 자기 침대에 누워 있었잖아요. {V} 씨는 자다가 심장이 멎은 거예요. 누가 해친 게 아니에요." : $"다친 데 하나 없이 자기 침대에 누워 있었잖아. {Jc(V, "은", "는", false)} 자다가 심장이 멎은 거야. 누가 해친 게 아니라고.";
            var p = new Prop { Kind = PropKind.Culprit, A = null, B = v.Id, Room = inc.FoundRoom, T0 = inc.DiscoverClock, T1 = inc.DiscoverClock, Value = "natural" };
            var c = ClaimText(T, sim, sp.Id, p, LineBank.FixParticles(text), "cause", Emotion.Sad);
            NpcReactions(sim, T, c);
        }

        /// <summary>A body cut to pieces: "someone sawed them up alive — a madman". Broken by the cut surfaces (no vital reaction) or the real fatal wound.</summary>
        static void ImpressionDismember(Simulation sim, TrialState T, List<Actor> npcs, Incident inc, Actor v, Rng rng)
        {
            var S = sim.S; if (!Methods.Dismembered(S, v.Id)) return;
            if (T.Claims.Any(c => c.Prop?.Kind == PropKind.Culprit && c.Prop.A == null && c.Prop.Value == "dismember:alive")) return;
            var sp = PickSpeaker(sim, npcs, inc, rng, a => S.K(a.Id).KnownDead.Contains(v.Id)); if (sp == null) return;
            bool pol = Polite(sp.Id); string V = Cast.GivenOf(v.Id);
            string text = pol ? $"{V} 씨는… 산 채로 톱에 잘린 거예요. 그런 짓을 한 건 제정신이 아닌 살인마예요. 우리 중에 누가 그럴 수 있겠어요?"
                              : $"{Jc(V, "은", "는", false)}… 산 채로 톱에 잘린 거야. 그런 짓을 한 건 제정신이 아닌 살인마라고. 우리 중에 누가 그럴 수 있겠어?";
            var p = new Prop { Kind = PropKind.Culprit, A = null, B = v.Id, Room = inc.FoundRoom, T0 = inc.DiscoverClock, T1 = inc.DiscoverClock, Value = "dismember:alive" };
            var c = ClaimText(T, sim, sp.Id, p, LineBank.FixParticles(text), "cause", Emotion.Fear);
            NpcReactions(sim, T, c);
        }

        /// <summary>A farewell note by the body: "they took their own life".</summary>
        static void ImpressionSuicide(Simulation sim, TrialState T, List<Actor> npcs, Incident inc, Actor v, Rng rng)
        {
            var S = sim.S; if (T.Claims.Any(c => c.Prop?.Kind == PropKind.Culprit && c.Prop.A == null && c.Prop.Value == "suicide")) return;
            var note = S.Items.Values.Where(i => i.NoteFrom != null && i.NoteFrom.StartsWith("forged:") && i.NoteFrom.EndsWith(":" + v.Id)).OrderBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
            if (note == null) return;
            var sp = PickSpeaker(sim, npcs, inc, rng, a => S.K(a.Id).Examined.Contains("item:" + note.Id) || inc.Discoverers.Contains(a.Id)); if (sp == null) return;
            bool pol = Polite(sp.Id); string V = Cast.GivenOf(v.Id);
            string text = pol ? $"{V} 씨 곁에 유서가 있었어요. “{note.Note}” — 스스로 목숨을 끊은 거예요. 범인을 찾을 일이 아니에요." : $"{V} 곁에 유서가 있었어. “{note.Note}” — 스스로 목숨을 끊은 거야. 범인 찾을 일이 아니라고.";
            var p = new Prop { Kind = PropKind.Culprit, A = null, B = v.Id, Room = inc.FoundRoom, T0 = inc.DiscoverClock, T1 = inc.DiscoverClock, Value = "suicide" };
            var c = ClaimText(T, sim, sp.Id, p, LineBank.FixParticles(text), "place", Emotion.Sad);
            NpcReactions(sim, T, c);
        }

        // ================================================================== reconstruction: the staging, the disposal
        /// <summary>The staging actually carried out around this death (ledger truth — scoring and reveal only; a planned layer that never happened does not count).</summary>
        internal static string ExecutedTrick(GameState S, Incident inc)
        {
            if (inc == null) return null;
            var ev = new HashSet<string>(S.Ledger.Where(e => e.Target == inc.Victim && (inc.Culprit == null || e.Actor == inc.Culprit || e.Actor == null)).Select(e => e.Type));   // the ledger is per loop
            if (ev.Contains("SealedRoom")) return "Seal";
            if (ev.Contains("TodShift")) return "Tod";
            if (ev.Contains("FakeMessage")) return "Message";
            if (ev.Contains("PlantWeapon")) return "Swap";
            if (ev.Contains("KeySlide")) return "KeySlide";
            if (ev.Contains("FakeNote")) return "Suicide";
            if (ev.Contains("PoisonTaken")) return "Delayed";
            if (ev.Contains("Shove") || ev.Contains("ShockFired") || ev.Contains("HeldUnder") || S.Ledger.Any(e => e.Type == "TrapFired" && e.Target == inc.Victim && e.Actor == inc.Culprit && e.Data != null && (e.Data.EndsWith(" Topple") || e.Data.EndsWith(" Tripwire")))) return "Accident";
            if (ev.Contains("Smother")) return "Natural";
            return null;
        }

        /// <summary>Which staging a public fact exposes (reconstruction fit against PUBLIC facts only).</summary>
        internal static string MethodTrickFits(TrialState T)
        {
            bool Any(Func<Prop, bool> f) => T.Public.Any(f);
            if (Any(p => p.Kind == PropKind.TraceAt && p.Value == "key-slid") || Any(p => p.Kind == PropKind.ItemState && p.Value == "key-on-floor")) return "KeySlide";
            if (Any(p => p.Kind == PropKind.ItemState && p.Value == "handwriting-mismatch")) return "Suicide";
            if (Any(p => p.Kind == PropKind.ItemState && p.Value == "poisoned-personal")) return "Delayed";
            if (Any(p => (p.Kind == PropKind.TraceAt && (p.Value == "push-bruise" || p.Value == "cable-stripped" || p.Value == "held-under" || p.Value == "clothed-drowning")) || (p.Kind == PropKind.ItemState && p.Value == "torn-button"))) return "Accident";
            if (Any(p => (p.Kind == PropKind.TraceAt && p.Value == "smother-marks") || (p.Kind == PropKind.ItemState && p.Value == "pillow-pressed"))) return "Natural";
            return null;
        }

        /// <summary>How the culprit got rid of the weapon, if by one of the rooms (ledger truth — scoring only).</summary>
        internal static int MethodConcealAnswer(GameState S, Incident inc, List<string> options)
        {
            var e = S.Ledger.Where(x => x.Actor == inc.Culprit && (x.Type == "Burn" || x.Type == "DumpWater" || x.Type == "Bury") && x.Clock >= inc.DeathClock - 60).OrderBy(x => x.Seq).FirstOrDefault();
            if (e == null) return -1;
            return options.IndexOf(e.Type == "Burn" ? "소각로에 태웠다" : e.Type == "DumpWater" ? "물속에 버렸다" : "흙 속에 묻었다");
        }

        internal static string MethodConcealFit(TrialState T, string opt)
        {
            var st = T.Public.Where(p => p.Kind == PropKind.ItemState).Select(p => p.Value).ToList();
            string fits = st.Contains("burnt") || st.Contains("burnt-remnant") ? "소각로에 태웠다" : st.Contains("waterlogged") ? "물속에 버렸다" : st.Contains("buried") ? "흙 속에 묻었다" : null;
            if (fits == null) return null;
            return opt == fits ? "ok" : "conflict";
        }

        // ================================================================== engraved questions for the new stagings
        /// <summary>When a staged first impression is broken with the right card, the court names the staging (the fact goes public).</summary>
        internal static (string word, string question, Prop fact) MethodQuestion(GameState S, Prop claim, Evidence ev)
        {
            if (claim == null || ev == null) return (null, null, null);
            bool Has(Func<Prop, bool> f) => ev.Props.Any(f);
            if (claim.Kind == PropKind.Culprit && claim.A == null && claim.Value != null)
            {
                if (claim.Value.StartsWith("accident"))
                {
                    if (Has(p => p.Kind == PropKind.TraceAt && p.Value == "push-bruise") || Has(p => p.Kind == PropKind.ItemState && p.Value == "torn-button"))
                        return ("계단·난간에서 밀어 사고로 위장", "그 추락이 사고가 아니라면 — 범인이 한 일은?", ev.Props.First(p => p.Value == "push-bruise" || p.Value == "torn-button").Clone());
                    if (Has(p => p.Value == "cable-stripped" || p.Value == "insulation-shavings"))
                        return ("기계에 걸어 둔 감전 함정", "누전이 우연이 아니라면 — 무엇이 꾸며져 있었나?", ev.Props.First(p => p.Value == "cable-stripped" || p.Value == "insulation-shavings").Clone());
                    if (Has(p => p.Value == "held-under" || p.Value == "nail-scrape" || p.Value == "clothed-drowning"))
                        return ("물속에 짓눌러 익사시킴", "혼자 빠진 게 아니라면 — 무슨 일이 있었나?", ev.Props.First(p => p.Value == "held-under" || p.Value == "nail-scrape" || p.Value == "clothed-drowning").Clone());
                }
                if (claim.Value == "natural" && Has(p => p.Value == "smother-marks" || p.Value == "pillow-pressed"))
                    return ("잠든 사이 베개로 숨을 막음", "자다가 숨이 멎은 게 아니라면 — 범인은 무엇을 했나?", ev.Props.First(p => p.Value == "smother-marks" || p.Value == "pillow-pressed").Clone());
                if ((claim.Value == "natural" || claim.Value == "suicide") && Has(p => p.Value == "poisoned-personal"))
                    return ("미리 타 둔 독", "그 방엔 아무도 없었는데 — 독은 언제 들어갔나?", ev.Props.First(p => p.Value == "poisoned-personal").Clone());
                if (claim.Value == "suicide" && Has(p => p.Value == "handwriting-mismatch"))
                    return ("가짜 유서로 자살 위장", "그 유서를 쓴 사람이 본인이 아니라면?", ev.Props.First(p => p.Value == "handwriting-mismatch").Clone());
            }
            if (claim.Kind == PropKind.DoorLocked && Has(p => p.Value == "key-slid" || p.Value == "key-on-floor"))
                return ("열쇠를 문틈으로 되돌린 밀실", "열쇠가 방 안에 있었는데 — 범인은 어떻게 나갔나?", ev.Props.First(p => p.Value == "key-slid" || p.Value == "key-on-floor").Clone());
            return (null, null, null);
        }

        /// <summary>The reconstruction option label of a staging code ("Accident" → "사고로 위장").</summary>
        internal static string TrickOptionFor(string code) { int i = Array.IndexOf(TrickCodes, code); return i >= 0 ? TrickOptions[i] : null; }

        internal static readonly string[] MethodTrickWords = { "계단·난간에서 밀어 사고로 위장", "기계에 걸어 둔 감전 함정", "물속에 짓눌러 익사시킴", "잠든 사이 베개로 숨을 막음", "미리 타 둔 독", "가짜 유서로 자살 위장", "열쇠를 문틈으로 되돌린 밀실" };
    }
}
