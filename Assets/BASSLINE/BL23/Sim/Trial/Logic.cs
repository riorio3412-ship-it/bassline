using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    public sealed class LogicVerdict
    {
        public LogicResult Result; public string Rule; public string Why; public bool Direct; public string Root;
        public override string ToString() => $"{Result} {Rule}: {Why}";
    }

    /// <summary>Common logic engine LR01–LR10 over structured propositions. Never reads the A-ledger or the true culprit.</summary>
    public static class Logic
    {
        static bool Overlap(double a0, double a1, double b0, double b1, double tol = 0) => a0 - tol <= b1 && b0 - tol <= a1;
        static bool Within(double t, double a0, double a1, double tol = 0) => t >= a0 - tol && t <= a1 + tol;

        /// <summary>Evaluate evidence e against claim c. e comes from a card (direct/hearsay flag given).</summary>
        public static LogicVerdict Check(GameState S, Prop c, Prop e, bool eDirect, string eRoot)
        {
            var v = CheckRaw(S, c, e, eDirect, eRoot); if (v.Why != null) v.Why = LineBank.FixParticles(v.Why); return v;   // "은(는)" → the right particle before it reaches any screen
        }
        static LogicVerdict CheckRaw(GameState S, Prop c, Prop e, bool eDirect, string eRoot)
        {
            var v = new LogicVerdict { Result = LogicResult.Irrelevant, Direct = eDirect, Root = eRoot };
            if (c == null || e == null) return v;
            string hearsay = eDirect ? "" : " (전해 들은 말이라 확정할 수는 없다)";
            LogicResult Hit(LogicResult r) => !eDirect && r == LogicResult.Contradict ? LogicResult.Conditional : r;
            switch (c.Kind)
            {
                case PropKind.AtPlace:
                case PropKind.WithPerson:
                    {
                        // LR01/LR03: same person seen elsewhere during the claimed span
                        string who = c.A;
                        if ((e.Kind == PropKind.AtPlace || e.Kind == PropKind.Held) && e.A == who && e.Room >= 0 && e.Room != c.Room && Overlap(c.T0, c.T1, e.T0, e.T1, -1))
                        { v.Result = Hit(LogicResult.Contradict); v.Rule = "LR03"; v.Why = $"그 시각 {Cast.GivenOf(who)}은(는) {S.RoomName(e.Room)}에 있었다{hearsay}"; return v; }
                        if (e.Kind == PropKind.SawActor && e.B == who && e.Item != "unsure" && e.Room != c.Room && Overlap(c.T0, c.T1, e.T0, e.T1, -1))
                        { v.Result = Hit(LogicResult.Contradict); v.Rule = "LR01"; v.Why = $"그 시각 {S.RoomName(e.Room)}에서 {Cast.GivenOf(who)}을(를) 봤다는 사람이 있다{hearsay}"; return v; }
                        if (c.Kind == PropKind.WithPerson && (e.Kind == PropKind.AtPlace) && e.A == c.B && e.Room != c.Room && Overlap(c.T0, c.T1, e.T0, e.T1, -1))
                        { v.Result = Hit(LogicResult.Contradict); v.Rule = "LR03"; v.Why = $"함께 있었다는 {Cast.GivenOf(c.B)}은(는) 그때 {S.RoomName(e.Room)}에 있었다{hearsay}"; return v; }
                        if (e.Kind == PropKind.AtPlace && e.A == who && e.Room == c.Room && Overlap(c.T0, c.T1, e.T0, e.T1)) { v.Result = LogicResult.Support; v.Rule = "LR01"; v.Why = "그 시각 그 자리에 있었던 게 맞다"; return v; }
                        if (e.Kind == PropKind.Disguised && e.Room != c.Room && Overlap(c.T0, c.T1, e.T0, e.T1)) { v.Result = LogicResult.NeedPremise; v.Rule = "LR02"; v.Why = "변장한 인물이 누구인지 먼저 밝혀야 한다"; return v; }
                        // device logs: a recorded passage elsewhere contradicts; a partial logger only limits
                        if (e.Kind == PropKind.DeviceRecord && e.A == who && e.Room >= 0 && e.Room != c.Room && Within(e.T0, c.T0, c.T1, -1)) { v.Result = LogicResult.Contradict; v.Rule = "LR03"; v.Why = $"출입 기록기에 {ClockFmt.Vague(e.T0)} {Cast.GivenOf(who)}이(가) {S.RoomName(e.Room)}에 드나든 기록이 남아 있다"; return v; }
                        if (e.Kind == PropKind.DeviceRecord && e.Value == "coverage-partial" && e.Room == c.Room) { v.Result = LogicResult.LimitScope; v.Rule = "LR08"; v.Why = "그 방 기록기는 문 하나만 지켜본다 — 기록이 없다고 해서 드나들지 않았다고 할 수는 없다"; return v; }
                        // IG03: a voice from a room is not the person; a recorder/echo in that room limits it further
                        if (e.Kind == PropKind.Heard && e.Value == "voice:" + who && e.Room == c.Room && Overlap(c.T0, c.T1, e.T0, e.T1, 2)) { v.Result = LogicResult.Support; v.Rule = "LR07"; v.Why = "그 시각 그 방에서 목소리가 들렸다 (목소리가 들렸다고 그 사람이 거기 있었다는 뜻은 아니다)"; return v; }
                        if (e.Kind == PropKind.MachineUsed && e.Value != null && e.Value.Contains("녹음 재생") && e.Room == c.Room && Overlap(c.T0, c.T1, e.T0, e.T1, 5)) { v.Result = LogicResult.LimitScope; v.Rule = "LR02"; v.Why = "그 방에서 녹음이 재생됐다 — 들린 목소리는 그 사람이 거기 있었다는 증거가 못 된다"; return v; }
                        // IG09: times read off a drifting clock
                        if (e.Kind == PropKind.ClockOffset && e.Room == c.Room && e.Value != "0") { v.Result = LogicResult.LimitScope; v.Rule = "LR08"; v.Why = $"그 방 시계는 {e.Value}분 틀어져 있다 — 그 시계를 보고 말한 시각이라면 어긋나 있을 수 있다"; return v; }
                        // IG02: someone waiting at the place of an invitation (possibly an outdated one)
                        if (e.Kind == PropKind.Invited && e.A == who && e.Room == c.Room && Overlap(c.T0, c.T1, e.T0 - 15, e.T1)) { v.Result = LogicResult.Support; v.Rule = "LR01"; v.Why = $"{Cast.GivenOf(who)}은(는) 그 시각 그곳으로 오라는 초대를 받았다 ({e.Value})"; return v; }
                        break;
                    }
                case PropKind.AliveAt:
                    if (e.Kind == PropKind.DeathWindow && e.A == c.A && c.T0 > e.T1 + 5) { v.Result = Hit(LogicResult.Contradict); v.Rule = "LR08"; v.Why = "시신을 보면 그 시각엔 이미 숨진 뒤였다"; return v; }
                    break;
                // ---- signature tricks (SetPieces): the staged first impression vs the clue that undoes it
                case PropKind.DoorLocked:
                    if (c.Value == "sealed" && e.Kind == PropKind.TraceAt && e.Value == "thread-under-door" && (e.A == c.A || e.Room == c.Room)) { v.Result = LogicResult.Contradict; v.Rule = "LR04"; v.Why = "문 아래 틈으로 실이 지나간 자국이 있다 — 밖에서 실로 잠금쇠를 당겨 잠글 수 있었다. 밀실이 아니다"; return v; }
                    // the key slid back under the door (Methods): it only looks as if it never left the room
                    if (c.Value == "sealed" && e.Kind == PropKind.TraceAt && e.Value == "key-slid" && (e.A == c.A || e.Room == c.Room)) { v.Result = LogicResult.Contradict; v.Rule = "LR04"; v.Why = "문턱 안쪽의 긁힌 자국이 문 아래 틈에서 곧게 이어진다 — 밖에서 잠근 뒤 열쇠를 틈으로 밀어 넣을 수 있었다. 밀실이 아니다"; return v; }
                    if (c.Value == "sealed" && e.Kind == PropKind.ItemState && e.Value == "key-on-floor") { v.Result = LogicResult.LimitScope; v.Rule = "LR04"; v.Why = "열쇠가 주머니가 아니라 문 바로 안쪽 바닥에 있었다 — 안에서 잠갔다는 증거가 못 된다"; return v; }
                    // a poison already in the room's drink needs no one inside when it works (cause time ≠ result time)
                    if (c.Value == "sealed" && ((e.Kind == PropKind.ItemState && e.Value == "poisoned-personal") || (e.Kind == PropKind.TraceAt && e.Value == "poisoned" && e.A == c.A))) { v.Result = e.Kind == PropKind.ItemState ? LogicResult.Contradict : LogicResult.LimitScope; v.Rule = "LR08"; v.Why = e.Kind == PropKind.ItemState ? "독은 미리 방 안의 마실 것에 들어 있었다 — 잠긴 방이어도 범인은 그 안에 있을 필요가 없었다" : "독에 당했다 — 독을 먹은 시각과 숨진 시각이 다르다. 방이 잠겨 있었다고 해서 혼자였다고 할 수는 없다"; return v; }
                    if (c.Value == "sealed" && e.Kind == PropKind.ItemState && e.Value == "line-cut") { v.Result = LogicResult.LimitScope; v.Rule = "LR04"; v.Why = "새로 잘린 줄 — 문을 밖에서 잠그는 데 쓰였을 수 있다"; return v; }
                    if (c.Value == "sealed" && e.Kind == PropKind.TraceAt && e.Item == "Scratch" && e.Room == c.Room) { v.Result = LogicResult.LimitScope; v.Rule = "LR04"; v.Why = "안쪽 잠금쇠에 긁힌 자국이 있다 — 손이 아닌 무언가로 돌렸다"; return v; }
                    if (e.Kind == PropKind.ItemAt && e.Value == "moved" && ItemCatalog.Get(e.Item)?.Key == true) { v.Result = LogicResult.Contradict; v.Rule = "LR04"; v.Why = "그 방의 열쇠가 다른 곳에서 발견됐다 — 밖에서 잠갔을 수 있다"; return v; }
                    break;
                case PropKind.TraceAt:
                    if (c.Value != null && c.Value.StartsWith("bloodwriting"))
                    {
                        if (e.Kind == PropKind.TraceAt && e.Value == "instant-death" && e.A == c.A) { v.Result = Hit(LogicResult.Contradict); v.Rule = "LR07"; v.Why = $"{Cast.GivenOf(c.A)}은(는) 거의 즉사였다 — 쓰러진 뒤 글씨를 남길 시간은 없었다{hearsay}"; return v; }
                        if (e.Kind == PropKind.TraceAt && e.Value != null && e.Value.StartsWith("writing-hand:") && e.A == c.A)
                        {
                            var p = e.Value.Split(':'); bool off = p.Length >= 3 && ((p[1] == "L" && p[2] == "right-handed") || (p[1] == "R" && p[2] == "left-handed"));
                            if (off) { v.Result = Hit(LogicResult.Contradict); v.Rule = "LR07"; v.Why = $"피 묻은 손가락은 {Cast.GivenOf(c.A)}이(가) 잘 쓰지 않는 {(p[1] == "L" ? "왼손" : "오른손")}이다 — 본인이 쓴 글씨로 보기 어렵다{hearsay}"; return v; }
                            v.Result = LogicResult.Support; v.Rule = "LR07"; v.Why = "피 묻은 손가락이 평소 쓰는 손과 맞는다"; return v;
                        }
                    }
                    break;
                case PropKind.DeathWindow:
                    if (e.Kind == PropKind.TraceAt && (e.Value == "temp-warm" || e.Value == "temp-cold") && e.A == c.A) { v.Result = Hit(LogicResult.Contradict); v.Rule = "LR08"; v.Why = e.Value == "temp-warm" ? "시신이 이상하게 따뜻했다 — 체온으로 짐작한 숨진 시각이 실제보다 늦게 나온다" : "시신이 이상하게 차갑고 젖어 있었다 — 체온으로 짐작한 숨진 시각이 실제보다 이르게 나온다"; return v; }
                    if (e.Kind == PropKind.TraceAt && e.Value == "fire-stoked") { v.Result = LogicResult.LimitScope; v.Rule = "LR08"; v.Why = "누군가 벽난로 불을 크게 키웠다 — 시신이 데워졌다면 숨진 시각이 실제보다 늦게 짐작된다"; return v; }
                    if (e.Kind == PropKind.TraceAt && e.Item == "Water" && e.Value == "cold-water" && e.A == c.A) { v.Result = LogicResult.LimitScope; v.Rule = "LR08"; v.Why = "시신 아래에 차가운 물기가 있다 — 시신이 식혀졌다면 숨진 시각이 실제보다 이르게 짐작된다"; return v; }
                    if (e.Kind == PropKind.AliveAt && e.A == c.A && e.T0 > c.T0 + 3) { v.Result = LogicResult.LimitScope; v.Rule = "LR01"; v.Why = "그 뒤에도 살아 있는 모습을 본 사람이 있다 — 숨진 시각의 폭이 좁혀진다"; return v; }
                    if (e.Kind == PropKind.Heard && Within(e.T0, c.T0, c.T1, 10)) { v.Result = LogicResult.Support; v.Rule = "LR07"; v.Why = "그 무렵 들린 소리와 들어맞는다 (무슨 소리였는지는 따로 확인해야 한다)"; return v; }
                    if (e.Kind == PropKind.DeathWindow && e.A == c.A && !Overlap(c.T0, c.T1, e.T0, e.T1)) { v.Result = Hit(LogicResult.Contradict); v.Rule = "LR01"; v.Why = "시신을 살펴보고 짐작한 시각과 맞지 않는다"; return v; }
                    break;
                case PropKind.WeaponType:
                    if (c.Item != null && e.Kind == PropKind.ItemState && e.Value == "smeared" && e.Item == c.Item) { v.Result = LogicResult.Contradict; v.Rule = "LR07"; v.Why = $"{ItemCatalog.Get(c.Item)?.Kor ?? "그 물건"}의 피는 겉에만 발려 있다 — 그걸로 내리쳐서 묻은 피가 아니다"; return v; }
                    if (e.Kind == PropKind.WeaponType && e.A == c.A && e.Value != c.Value) { v.Result = LogicResult.Contradict; v.Rule = "LR07"; v.Why = $"시신의 상처는 {WoundText.Type(P(e.Value))}에 가깝다"; return v; }
                    if (e.Kind == PropKind.Wound && e.A == c.A && e.Item != null && e.Item != c.Value && !(Is(e.Item, DamageType.Cut) && Is(c.Value, DamageType.Stab))) { v.Result = LogicResult.Contradict; v.Rule = "LR07"; v.Why = $"실제로 남은 상처는 이렇다 — {e.Value}"; return v; }
                    if (e.Kind == PropKind.WeaponType && e.A == c.A && e.Value == c.Value) { v.Result = LogicResult.Support; v.Rule = "LR07"; v.Why = "상처 모양이 들어맞는다"; return v; }
                    break;
                case PropKind.DeathPlace:
                    if (e.Kind == PropKind.TrapSet && e.Room == c.Room) { v.Result = LogicResult.LimitScope; v.Rule = "LR08"; v.Why = "그 방에는 미리 설치된 함정이 있었다 — 범인이 손을 쓴 때와 곳이, 숨진 때와 곳과 다를 수 있다"; return v; }
                    if (e.Kind == PropKind.BodyMoved && e.A == c.A && e.Value == "likely") { v.Result = LogicResult.LimitScope; v.Rule = "LR08"; v.Why = "시신을 옮긴 흔적이 있다 — 발견된 곳에서 숨졌다고 단정할 수 없다"; return v; }
                    if (e.Kind == PropKind.TraceAt && (e.Item == "DragMark" || e.Item == "BloodSmear")) { v.Result = LogicResult.LimitScope; v.Rule = "LR08"; v.Why = "무언가를 끌고 간 자국이 있다"; return v; }
                    break;
                case PropKind.DoorState:
                    if (e.Kind == PropKind.ItemAt && e.Value == "moved" && ItemCatalog.Get(e.Item)?.Key == true) { v.Result = LogicResult.Contradict; v.Rule = "LR04"; v.Why = "그 방의 열쇠가 다른 곳에서 발견됐다 — 밖에서 잠갔을 수 있다"; return v; }
                    if (e.Kind == PropKind.DoorState) { v.Result = LogicResult.LimitScope; v.Rule = "LR08"; v.Why = "지금 잠겨 있다고 해서 그때도 잠겨 있었다고 할 수는 없다"; return v; }
                    break;
                case PropKind.Culprit:
                    {
                        // "nobody did it": an accident, a natural death, a suicide — staged scenes (Methods) and the clue made to break each
                        if (c.A == null && c.Value != null) { var sv = Staged(S, c, e, hearsay, Hit); if (sv != null) { sv.Direct = eDirect; sv.Root = eRoot; return sv; } break; }
                        string x = c.A;
                        // an independent alibi covering the death window clears (LR01/LR03)
                        if ((e.Kind == PropKind.AtPlace || e.Kind == PropKind.WithPerson) && e.A == x && e.Value == "window-cover") { v.Result = Hit(LogicResult.Contradict); v.Rule = "LR03"; v.Why = $"숨진 것으로 보이는 시간 내내 {Cast.GivenOf(x)}은(는) {S.RoomName(e.Room)}에 있었다{hearsay}"; return v; }
                        if (e.Kind == PropKind.Held && e.A == x && ItemCatalog.Get(e.Item)?.IsWeapon == true) { v.Result = LogicResult.Support; v.Rule = "LR04"; v.Why = "흉기가 될 만한 물건을 갖고 있었다 (갖고 있었다고 해서 썼다는 뜻은 아니다)"; return v; }
                        if (e.Kind == PropKind.Bloodied && e.A == x) { v.Result = LogicResult.Support; v.Rule = "LR07"; v.Why = "옷에 피가 묻어 있었다 (다친 사람을 돕다가 묻었을 수도 있다)"; return v; }
                        if (e.Kind == PropKind.AtPlace && e.A == x && e.Room >= 0) { v.Result = LogicResult.Support; v.Rule = "LR01"; v.Why = "그 무렵 현장 가까이에 있었다"; return v; }
                        if (e.Kind == PropKind.Lie && e.A == x) { v.Result = LogicResult.LimitScope; v.Rule = "LR06"; v.Why = "거짓말은 드러났지만, 그것만으로 죽였다고 할 수는 없다"; return v; }
                        if (e.Kind == PropKind.Injured && e.A == x) { v.Result = LogicResult.Support; v.Rule = "LR07"; v.Why = "피해자가 저항하다 낸 상처일 수 있다"; return v; }
                        if (e.Kind == PropKind.Loaned && e.B == x && e.Value != null && e.Value.StartsWith("courier:")) { v.Result = LogicResult.Support; v.Rule = "LR05"; v.Why = $"{Cast.GivenOf(x)}이(가) 피해자를 불러낸 쪽지를 {Cast.GivenOf(e.A)}에게 맡겼다 (전한 사람은 내용을 몰랐다)"; return v; }
                        if (e.Kind == PropKind.Loaned && e.A == x && e.Value != null && e.Value.StartsWith("courier:")) { v.Result = LogicResult.LimitScope; v.Rule = "LR05"; v.Why = "쪽지를 전한 건 맞지만, 부탁받아 전했을 뿐이다 — 심부름을 했다고 한패인 건 아니다"; return v; }
                        if (e.Kind == PropKind.Heard && e.Value == "voice:" + x) { v.Result = LogicResult.Conditional; v.Rule = "LR02"; v.Why = $"그때 {S.RoomName(e.Room)}에서 {Cast.GivenOf(x)}의 목소리가 들렸다 — 녹음을 튼 게 아니라면 알리바이가 된다"; return v; }
                        if (e.Kind == PropKind.MachineUsed && e.A == x && e.Value != null && e.Value.Contains("녹음 재생")) { v.Result = LogicResult.Support; v.Rule = "LR02"; v.Why = $"누군가 {Cast.GivenOf(x)}의 목소리를 녹음해 사건 무렵에 틀었다 — 목소리로 세운 알리바이는 무너진다"; return v; }
                        if (e.Kind == PropKind.DeviceRecord && e.A == x && e.Value == "in") { v.Result = LogicResult.Support; v.Rule = "LR01"; v.Why = $"기록기에 {ClockFmt.Vague(e.T0)} {S.RoomName(e.Room)}에 들어간 기록이 있다"; return v; }
                        if (e.Kind == PropKind.TraceAt && e.Value != null && e.Value.StartsWith("bloodwriting") && e.B == x) { v.Result = LogicResult.Support; v.Rule = "LR07"; v.Why = $"시신 옆의 피 글씨가 {Cast.GivenOf(x)}을(를) 가리킨다 (누가 썼는지는 따로 확인해야 한다)"; return v; }
                        // second-wave traces that name a person through what they left behind (ownership ≠ act: LR04)
                        if (e.Kind == PropKind.ItemState && e.A == x && e.Value == "torn-button") { v.Result = LogicResult.Support; v.Rule = "LR04"; v.Why = $"떨어진 자리에 있던 뜯긴 단추는 {Cast.GivenOf(x)}의 소매 단추다 — 거기서 누군가와 뒤엉켰다"; return v; }
                        if (e.Kind == PropKind.ItemState && e.A == x && e.Value == "burnt-remnant") { v.Result = LogicResult.Support; v.Rule = "LR04"; v.Why = $"소각로 재 속에서 {Cast.GivenOf(x)}의 셔츠 단추가 나왔다 — 옷을 태워 없앤 사람이 있다"; return v; }
                        if (e.Kind == PropKind.Wet && e.A == x) { v.Result = LogicResult.Support; v.Rule = "LR07"; v.Why = $"그 무렵 {Cast.GivenOf(x)}의 소매와 옷자락이 젖어 있었다 (물가에 있었을 수 있다 — 왜 젖었는지는 따로 확인해야 한다)"; return v; }
                        break;
                    }
                case PropKind.Held:
                    if (e.Kind == PropKind.ItemState && e.Item == c.Item) { v.Result = LogicResult.Support; v.Rule = "LR04"; v.Why = "그 물건이 어떤 상태였는지 남아 있다"; return v; }
                    if (e.Kind == PropKind.AtPlace && e.A == c.A && e.Room != c.Room && Overlap(c.T0, c.T1, e.T0, e.T1, -1)) { v.Result = Hit(LogicResult.Contradict); v.Rule = "LR03"; v.Why = "그 시각엔 다른 곳에 있었다"; return v; }
                    break;
                case PropKind.Heard:
                    if (e.Kind == PropKind.ClockOffset && e.Value != "0") { v.Result = LogicResult.LimitScope; v.Rule = "LR08"; v.Why = $"시계가 {e.Value}분 틀어진 방이 있다 — 어느 시계를 보고 말한 시각인지 확인해야 한다"; return v; }
                    if (e.Kind == PropKind.MachineUsed && e.Value != null && e.Value.Contains("녹음")) { v.Result = LogicResult.LimitScope; v.Rule = "LR02"; v.Why = "녹음기로 소리를 틀었을 수 있다 — 소리가 들린 때가 사건이 벌어진 때라고 장담할 수 없다"; return v; }
                    break;
            }
            return v;
        }

        /// <summary>"Nobody did it" claims (Methods staging): accident:* / natural / suicide, and the clue made to break each.</summary>
        static LogicVerdict Staged(GameState S, Prop c, Prop e, string hearsay, Func<LogicResult, LogicResult> hit)
        {
            bool acc = c.Value.StartsWith("accident"), nat = c.Value == "natural", sui = c.Value == "suicide", dis = c.Value == "dismember:alive";
            if (!acc && !nat && !sui && !dis) return null;
            string victim = c.B; string V = victim != null ? Cast.GivenOf(victim) : "피해자";
            bool onVictim = e.A == null || e.A == victim;
            LogicVerdict R(LogicResult r, string rule, string why) => new LogicVerdict { Result = r, Rule = rule, Why = why };
            // "cut up alive": the cut surfaces carry no vital reaction; the real cause of death is on the body elsewhere
            if (dis)
            {
                if (e.Kind == PropKind.TraceAt && onVictim && e.Value == "postmortem-cut") return R(hit(LogicResult.Contradict), "LR07", $"잘린 자리에 살아 있을 때 생기는 반응이 없다 — {V}은(는) 톱이 닿기 전에 이미 숨져 있었다{hearsay}");
                if (e.Kind == PropKind.TraceAt && onVictim && (e.Value == "instant-death" || e.Value == "ligature" || e.Value == "smother-marks" || e.Value == "poisoned"))
                    return R(hit(LogicResult.Contradict), "LR07", $"진짜 사인은 따로 있다 — {(e.Value == "ligature" ? "목이 졸려" : e.Value == "smother-marks" ? "얼굴이 짓눌려 숨이 막혀" : e.Value == "poisoned" ? "독에 당해" : "치명상 한 번에")} 이미 숨졌고, 톱은 그 뒤의 일이다{hearsay}");
                if (e.Kind == PropKind.TraceAt && e.Value == "drain-blood") return R(LogicResult.LimitScope, "LR07", "해체한 방의 배수구로 피를 씻어 냈다 — 광기가 아니라 치밀하게 계산된 뒷정리다");
                return null;
            }
            // a second person's hands: against every "nobody did it"
            if (e.Kind == PropKind.TraceAt && onVictim && (e.Value == "ligature" || e.Value == "smother-marks" || e.Value == "nail-scrape" || e.Value == "held-under"))
                return R(hit(LogicResult.Contradict), "LR07", (e.Value == "ligature" ? "목에 뒤에서 조른 끈 자국 — 사고나 병으로는 생기지 않는다" : e.Value == "smother-marks" ? "얼굴을 무언가로 짓누른 자국 — 스스로 숨이 멎은 게 아니다" : e.Value == "held-under" ? "목덜미와 어깨에 누른 손가락 자국 — 혼자 빠진 게 아니다" : $"{V}의 손톱 밑에 다른 사람의 살갗 — 누군가와 맞붙었다") + hearsay);
            if (e.Kind == PropKind.ItemState && e.Value == "pillow-pressed" && (nat || sui)) return R(LogicResult.Contradict, "LR07", "베개 한가운데 얼굴이 눌린 자국 — 누군가 베개로 숨을 막았다");
            if (acc)
            {
                if (e.Kind == PropKind.TraceAt && onVictim && e.Value == "push-bruise") return R(hit(LogicResult.Contradict), "LR07", $"{V}의 가슴에 앞에서 밀친 손바닥 멍 — 발을 헛디딘 사고가 아니다{hearsay}");
                if (e.Kind == PropKind.ItemState && e.Value == "torn-button") return R(LogicResult.LimitScope, "LR04", "떨어진 자리 위에 다른 사람의 뜯긴 단추가 있었다 — 그 순간 곁에 누군가 있었다");
                if (e.Kind == PropKind.TraceAt && e.Value == "edge-scuff") return R(LogicResult.LimitScope, "LR07", "가장자리에 난 미끄러진 자국은 뒤로 밀려난 방향이다 — 혼자 헛디뎠다고 단정할 수 없다");
                if (e.Kind == PropKind.TraceAt && e.Value == "cable-stripped") return R(LogicResult.Contradict, "LR07", "전선 피복이 칼로 반듯하게 벗겨져 있었다 — 낡아서 생긴 누전이 아니라 누군가 꾸민 것이다");
                if (e.Kind == PropKind.ItemState && e.Value == "insulation-shavings") return R(LogicResult.LimitScope, "LR04", "공구 날에 전선 피복 부스러기가 끼어 있다 — 누군가 그 전선을 손봤다");
                if (e.Kind == PropKind.TrapSet && (e.Room == c.Room || e.Room < 0)) return R(LogicResult.Contradict, "LR07", "그 자리에 미리 설치된 함정이 있었다 — 사고가 아니라 누군가 꾸민 일이다");
                if (e.Kind == PropKind.TraceAt && onVictim && e.Value == "clothed-drowning") return R(hit(LogicResult.Contradict), "LR07", $"{V}은(는) 겉옷과 신발을 그대로 입은 채 물에 빠졌다 — 수영하다 빠진 사고가 아니다{hearsay}");
                if (c.Value == "accident:drown" && e.Kind == PropKind.TraceAt && e.Value == "wet-trail") return R(LogicResult.LimitScope, "LR07", "수영장에서 다른 곳으로 이어진 젖은 발자국 — 피해자 말고도 물에서 나온 사람이 있었다");
                if (c.Value == "accident:drown" && e.Kind == PropKind.Wet && e.A != null && e.A != victim) return R(LogicResult.LimitScope, "LR07", $"그 무렵 {Cast.GivenOf(e.A)}도 흠뻑 젖어 있었다 — 물가에 다른 사람이 있었다");
            }
            if (nat || sui)
            {
                if (nat && e.Kind == PropKind.TraceAt && onVictim && e.Value == "poisoned") return R(hit(LogicResult.Contradict), "LR07", $"{V}에게 독에 당한 증상이 있다 — 병으로 죽은 게 아니다{hearsay}");
                if (e.Kind == PropKind.ItemState && (e.Value == "poisoned-personal" || e.Value == "poison-residue")) return R(nat ? LogicResult.Contradict : LogicResult.LimitScope, "LR07", nat ? "마신 것에 독이 들어 있었다 — 자연사가 아니다" : "독은 병째 삼킨 게 아니라 마실 것에 미리 섞여 있었다 — 스스로 택한 방법이라 보기 어렵다");
                if ((e.Kind == PropKind.TraceAt && e.Value == "sedated" && onVictim) || (e.Kind == PropKind.ItemState && e.Value == "sedative-residue")) return R(LogicResult.LimitScope, "LR07", e.Kind == PropKind.ItemState ? "잠들게 하는 약은 식탁의 잔에 들어 있었다 — 스스로 방에서 삼킨 게 아니다" : "죽기 전에 수면제를 먹은 상태였다 — 스스로 먹었는지 누가 먹였는지 확인해야 한다");
            }
            if (sui && e.Kind == PropKind.ItemState && e.Value == "handwriting-mismatch") return R(LogicResult.Contradict, "LR07", "유서의 필체가 본인의 것이 아니다 — 누군가 흉내 내 쓴 가짜 유서다");
            if (sui && e.Kind == PropKind.TraceAt && onVictim && e.Value == "poisoned") return R(LogicResult.LimitScope, "LR07", "독에 당한 건 맞지만, 그 독이 어디서 왔는지부터 확인해야 한다");
            return null;
        }

        static DamageType P(string s) { Enum.TryParse(s ?? "None", out DamageType d); return d; }
        static bool Is(string s, DamageType d) => P(s) == d;

        /// <summary>LR09: count independent roots among supporting evidence (same original = one source).</summary>
        public static int IndependentRoots(IEnumerable<Evidence> evs) => evs.Select(e => e.Root).Distinct().Count();

        /// <summary>Best verdict among an owner's evidence for a claim (used by NPCs to decide whether to object).</summary>
        public static (LogicVerdict v, Evidence ev) Best(GameState S, Prop claim, IEnumerable<Evidence> evidence)
        {
            LogicVerdict best = null; Evidence bestEv = null;
            foreach (var ev in evidence)
                foreach (var p in ev.Props)
                {
                    var r = Check(S, claim, p, ev.Direct, ev.Root);
                    if (r.Result == LogicResult.Irrelevant) continue;
                    if (best == null || Rank(r.Result) > Rank(best.Result)) { best = r; bestEv = ev; }
                }
            return (best, bestEv);
        }
        static int Rank(LogicResult r) => r == LogicResult.Contradict ? 5 : r == LogicResult.Conditional ? 4 : r == LogicResult.LimitScope ? 3 : r == LogicResult.Support ? 2 : r == LogicResult.NeedPremise ? 1 : 0;
    }
}
