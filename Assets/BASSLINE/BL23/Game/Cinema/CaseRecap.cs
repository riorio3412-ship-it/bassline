using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Audio;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Cinema
{
    /// <summary>
    /// 그날 밤의 재구성 — the whole case retold once, as a sequence of leaded-glass windows of live shots, from the recording:
    /// the resolve, the preparation, the approach, the act (the hand, the weapon, the face), the concealment, the discovery,
    /// the one thing that gave it away. 민혁 narrates, speaking to the culprit when the court got it right, about them when
    /// it did not. Every pose is the recorded one; the montage only chooses where to look and when.
    /// </summary>
    public sealed class CaseRecap
    {
        sealed class Cut
        {
            public ShotSpec Shot; public double Tick, Span; public float Dur = 3.8f; public string Caption; public int Pane = -1; public bool Keep;
            public string Word; public Vector2 WordAt = new Vector2(0.78f, 0.22f); public string Sfx; public float Tilt;
        }
        sealed class Beat { public string Kicker, Layout; public int Out; public readonly List<Cut> Cuts = new List<Cut>(); }

        readonly Session _s; readonly ReplayStage _st; readonly PaneMontage _m; bool _you; GameState S => _s.S;
        readonly List<Beat> _beats = new List<Beat>();
        string C, V, CG, VG; string _weaponType, _weaponId; long _holdFrom = long.MaxValue, _holdTo = long.MinValue; int _words;
        public int Shots; float _ftSum; int _ftN; long _deathTick = long.MaxValue; bool _probeAlive, _probeDeath; float _deadSeenAt = -1f;
        public bool Skipped { get; private set; }

        static readonly HashSet<string> ActT = new HashSet<string> { "AttackBegin", "Garrote", "Shove", "Smother", "Strike", "PressContact", "TrapFired", "ShockFired", "PoisonTaken", "HeldUnder" };
        static readonly HashSet<string> PrepT = new HashSet<string> { "PickUp", "Invite", "CourierAsk", "Handover", "DisguiseOn", "GuiseAs", "TrapArmed", "RecorderArmed", "PressArmed", "ShockRigged", "Sedate", "Dose", "PoisonPlant", "Stoke", "NoiseMask", "Circuit", "GatheringLeave", "EchoRecord" };
        static readonly HashSet<string> HideT = new HashSet<string> { "CarryStart", "Wash", "HideItem", "PlantWeapon", "SealedRoom", "LockedRoomMade", "KeySlide", "FakeMessage", "FakeNote", "TodShift", "ColdHide", "DisguiseOff", "ChangeClothes", "Burn", "DumpWater", "Bury", "GatheringReturn", "RecorderPlay", "EchoPlay" };
        static readonly HashSet<string> DisposeT = new HashSet<string> { "HideItem", "PlantWeapon", "Burn", "DumpWater", "Bury", "Handover" };

        public CaseRecap(Session s, ReplayStage stage, PaneMontage m, bool secondPerson)
        {
            _s = s; _st = stage; _m = m; C = stage.Culprit; V = stage.Victim; _you = secondPerson && C != null && C != V && V != Cast.Player;
            CG = Cast.GivenOf(C); VG = Cast.GivenOf(V);
            _weaponType = stage.Inc?.WeaponType; _weaponId = stage.Inc?.Weapon;
            if (string.IsNullOrEmpty(_weaponType) && _weaponId != null) _weaponType = S.I(_weaponId)?.Type;
            if (_weaponType != null && ItemCatalog.Get(_weaponType) == null) _weaponType = null;
            Build();
        }

        public int BeatCount => _beats.Count;

        // ================================================================ narration (민혁)
        string Say(string you, string they) => LineBank.FixParticles(_you ? you : they);
        string Room(LedgerEvent e) => e != null && e.Room >= 0 ? S.RoomName(e.Room) : "";
        string Item(LedgerEvent e) => S.I(e?.Item)?.Kor ?? ItemCatalog.Get((e?.Data ?? "").Split(' ', '|')[0])?.Kor ?? ItemCatalog.Get(_weaponType)?.Kor ?? "흉기";
        string When(double tick) => ClockFmt.Vague(_st.ClockAt(tick));
        static string Motive(string m) => m == "wish" ? "소원을 이루기 위해" : m == "grudge" ? "쌓인 원한 끝에" : m == "fear" ? "입을 막아야 한다는 두려움에" : m == "jealousy" ? "질투에 떠밀려" : "끝내";
        string Who(string id) => id == Cast.Player && V != Cast.Player ? "나" : Cast.GivenOf(id);

        string Line(LedgerEvent e)
        {
            // someone other than the one the court named (or no culprit at all: an accident, a fight): third person, by their own
            // name — never "은(는) 도윤에게 달려들었다" with the name missing
            if (e != null && e.Actor != null && e.Actor != C && e.Actor != V && _s.World.ViewOf(e.Actor) != null)
            {
                string cg = CG; bool you = _you; CG = Cast.GivenOf(e.Actor); _you = false;
                try { return LineCore(e); } finally { CG = cg; _you = you; }
            }
            return LineCore(e);
        }
        string LineCore(LedgerEvent e)
        {
            string T = Cast.GivenOf(e.Target), R = Room(e), I = Item(e);
            switch (e.Type)
            {
                case "PickUp": return Say($"넌 {R}에서 {I}을(를) 손에 넣었어.", $"{CG}은(는) {R}에서 {I}을(를) 손에 넣었다.");
                case "Invite": return Say($"{T}에게 {R}에서 만나자고 한 것도 너였지.", $"{CG}은(는) {T}에게 {R}에서 만나자고 했다.");
                case "CourierAsk": return Say($"넌 {T}에게 쪽지 심부름을 부탁했어. 누가 보낸 건지는 숨긴 채로.", $"{CG}은(는) {T}에게 쪽지 심부름을 부탁했다. 누가 보낸 건지는 숨긴 채로.");
                case "Handover": return Say($"넌 {T}에게 쪽지를 건넸어.", $"{CG}은(는) {T}에게 쪽지를 건넸다.");
                case "DisguiseOn": return Say("넌 남의 옷을 걸치고 모습을 감췄어.", $"{CG}은(는) 남의 옷을 걸치고 모습을 감췄다.");
                case "GuiseAs": return Say($"넌 권능 '외피'로 {T}의 모습을 빌렸어.", $"{CG}은(는) 권능 '외피'로 {T}의 모습을 빌렸다.");
                case "TrapArmed": return Say($"넌 {R}에 함정을 설치해 뒀어.", $"{CG}은(는) {R}에 함정을 설치해 두었다.");
                case "RecorderArmed": return Say("넌 네 목소리를 녹음해 두고, 틀어질 시각까지 맞춰 뒀어.", $"{CG}은(는) 자기 목소리를 녹음해 두고, 틀어질 시각까지 맞춰 두었다.");
                case "PressArmed": return Say("넌 프레스 타이머를 맞춰 뒀어.", $"{CG}은(는) 프레스 타이머를 맞춰 두었다.");
                case "ShockRigged": return Say($"넌 {R}의 전선 피복을 벗기고, 바닥에 물을 부었어.", $"{CG}은(는) {R}의 전선 피복을 벗기고 바닥에 물을 부었다.");
                case "Sedate": return Say($"넌 {T}의 잔에 수면제를 녹였어.", $"{CG}은(는) {T}의 잔에 수면제를 녹였다.");
                case "Dose": return Say($"넌 {T}의 잔에 디기탈리스를 탔어. 30분쯤 지나야 듣는 독이지.", $"{CG}은(는) {T}의 잔에 디기탈리스를 탔다. 30분쯤 지나야 듣는 독이다.");
                case "PoisonPlant": return Say($"넌 아무도 없는 {R}에 들어가 독을 섞어 두었어.", $"{CG}은(는) 아무도 없는 {R}에 들어가 독을 섞어 두었다.");
                case "Stoke": return Say("넌 벽난로에 장작을 가득 넣어 불을 키웠어.", $"{CG}은(는) 벽난로에 장작을 가득 넣어 불을 키웠다.");
                case "NoiseMask": return Say("넌 보일러를 끝까지 올렸어. 굉음이 다른 소리를 전부 덮었지.", $"{CG}은(는) 보일러를 끝까지 올렸다. 굉음이 다른 소리를 전부 덮었다.");
                case "Circuit": return Say($"불이 꺼졌어 — 네 손으로.", $"불이 꺼졌다 — {CG}의 손으로.");
                case "GatheringLeave": return Say("넌 모임에서 슬쩍 빠져나왔어.", $"{CG}은(는) 모임에서 슬쩍 빠져나왔다.");
                case "EchoRecord": return Say("넌 권능 '메아리'로 방 안의 소리를 담아 뒀어.", $"{CG}은(는) 권능 '메아리'로 방 안의 소리를 담아 두었다.");
                case "AttackBegin": return Say($"{R}에서, 넌 {VG}에게 달려들었어.", $"{R}에서, {CG}은(는) {VG}에게 달려들었다.");
                case "Garrote": return Say($"넌 {VG}의 등 뒤로 다가가 {I}을(를) 목에 감았어.", $"{CG}은(는) {VG}의 등 뒤로 다가가 {I}을(를) 목에 감았다.");
                case "Shove": return (e.Data ?? "").StartsWith("rail") ? Say($"넌 난간에 기댄 {VG}을(를) 힘껏 밀었어.", $"{CG}은(는) 난간에 기댄 {VG}을(를) 힘껏 밀었다.") : Say($"넌 계단 맨 위에 선 {VG}을(를) 떠밀었어.", $"{CG}은(는) 계단 맨 위에 선 {VG}을(를) 떠밀었다.");
                case "Smother": return Say($"넌 잠든 {VG}의 얼굴을 짓눌렀어.", $"{CG}은(는) 잠든 {VG}의 얼굴을 짓눌렀다.");
                case "Strike":
                    {
                        var p = (e.Data ?? "").Split('/');
                        if (p.Length >= 2 && Enum.TryParse(p[0], out BodyRegion r) && Enum.TryParse(p[1], out DamageType d)) return LineBank.FixParticles($"{Cast.GivenOf(e.Target)}의 {WoundText.Region(r)}에 {WoundText.Type(d)}이(가) " + (_you ? "남았어." : "남았다."));
                        return null;
                    }
                case "PressContact": return Say("프레스가 내려왔어.", "프레스가 내려왔다.");
                case "TrapFired": return Say($"함정이 작동했어. {VG}이(가) 걸려들었지.", $"함정이 작동했다. {VG}이(가) 걸려들었다.");
                case "ShockFired": return Say($"{VG}이(가) 기계에 손을 대는 순간, 불꽃이 튀었어.", $"{VG}이(가) 기계에 손을 대는 순간, 불꽃이 튀었다.");
                case "PoisonTaken": return Say($"{VG}은(는) 잠들기 전 {e.Data}을(를) 입에 댔어. 독이 든 줄도 모르고.", $"{VG}은(는) 잠들기 전 {e.Data}을(를) 입에 댔다. 독이 든 줄도 모르고.");
                case "HeldUnder": return Say("네 소매가 흠뻑 젖었지.", $"{CG}의 소매가 흠뻑 젖었다.");
                case "CarryStart": return Say($"넌 {VG}을(를) 끌고 갔어.", $"{CG}은(는) {VG}을(를) 끌고 갔다.");
                case "Wash": return Say($"넌 {R}에서 흉기를 씻었어. 붉은 물이 흘러내렸지.", $"{CG}은(는) {R}에서 흉기를 씻었다. 붉은 물이 흘러내렸다.");
                case "HideItem": return Say($"넌 {R}에 {I}을(를) 숨겼어.", $"{CG}은(는) {R}에 {I}을(를) 숨겼다.");
                case "PlantWeapon": return Say($"넌 다른 물건에 피를 묻혀 {VG} 곁에 두었어. 진짜 흉기는 따로 치웠지.", $"{CG}은(는) 다른 물건에 피를 묻혀 {VG} 곁에 두었다. 진짜 흉기는 따로 치웠다.");
                case "SealedRoom": return Say("넌 실 한 가닥을 안쪽 잠금쇠에 걸고 문틈으로 당겼어. 방은 안에서 잠긴 것처럼 보였지.", $"{CG}은(는) 실 한 가닥을 안쪽 잠금쇠에 걸고 문틈으로 당겼다. 방은 안에서 잠긴 것처럼 보였다.");
                case "LockedRoomMade": return Say($"넌 {VG}의 열쇠로 밖에서 문을 잠갔어.", $"{CG}은(는) {VG}의 열쇠로 밖에서 문을 잠갔다.");
                case "KeySlide": return Say("넌 밖에서 문을 잠그고, 열쇠를 문 아래 틈으로 밀어 넣었어.", $"{CG}은(는) 밖에서 문을 잠그고, 열쇠를 문 아래 틈으로 밀어 넣었다.");
                case "FakeMessage": { var pd = (e.Data ?? "").Split('|'); string g = pd.Length > 1 ? pd[1] : "?"; string who = pd.Length > 0 ? Cast.GivenOf(pd[0]) : "다른 사람"; return Say($"넌 {VG}의 손가락으로 바닥에 「{g}」라고 썼어. {who}을(를) 가리키는 가짜 다잉 메시지였지.", $"{CG}은(는) {VG}의 손가락으로 바닥에 「{g}」라고 썼다. {who}을(를) 가리키는 가짜 다잉 메시지였다."); }
                case "FakeNote": return Say($"넌 {VG}의 글씨를 흉내 낸 유서를 남겼어.", $"{CG}은(는) {VG}의 글씨를 흉내 낸 유서를 남겼다.");
                case "TodShift": return (e.Data ?? "").StartsWith("heat") ? Say("넌 시신을 불 가까이 옮겼어. 숨진 시각이 더 늦어 보이게.", $"{CG}은(는) 시신을 불 가까이 옮겼다. 숨진 시각이 더 늦어 보이게.") : Say("넌 시신을 차가운 곳에 뒀어. 숨진 시각이 더 일러 보이게.", $"{CG}은(는) 시신을 차가운 곳에 두었다. 숨진 시각이 더 일러 보이게.");
                case "ColdHide": return Say($"넌 {VG}을(를) 저온 보관실에 눕혀 뒀어.", $"{CG}은(는) {VG}을(를) 저온 보관실에 눕혀 두었다.");
                case "DisguiseOff": return Say($"넌 {R}에서 변장을 벗어 숨겼어.", $"{CG}은(는) {R}에서 변장을 벗어 숨겼다.");
                case "ChangeClothes": return Say("넌 옷을 갈아입었어. 아무 일도 없었다는 듯이.", $"{CG}은(는) 옷을 갈아입었다. 아무 일도 없었다는 듯이.");
                case "Burn": return Say($"넌 소각로에 {(string.IsNullOrEmpty(e.Data) ? "증거" : e.Data)}을(를) 던져 넣었어. 불길이 모든 걸 삼켰지.", $"{CG}은(는) 소각로에 {(string.IsNullOrEmpty(e.Data) ? "증거" : e.Data)}을(를) 던져 넣었다. 불길이 모든 걸 삼켰다.");
                case "DumpWater": return Say($"넌 {I}을(를) 수영장 물속에 떨어뜨렸어.", $"{CG}은(는) {I}을(를) 수영장 물속에 떨어뜨렸다.");
                case "Bury": return Say($"넌 온실 흙을 파고 {I}을(를) 묻었어.", $"{CG}은(는) 온실 흙을 파고 {I}을(를) 묻었다.");
                case "GatheringReturn": return Say("그리고 넌 아무 일 없었다는 듯 모임으로 돌아왔어.", $"그리고 {CG}은(는) 아무 일 없었다는 듯 모임으로 돌아왔다.");
                case "RecorderPlay": return Say("그 시각, 네 방에서는 녹음된 네 목소리가 흘러나오고 있었어.", $"그 시각, {CG}의 방에서는 녹음된 목소리가 흘러나오고 있었다.");
                case "EchoPlay": return Say("넌 담아 둔 소리를 먼 곳에서 흘려보냈어.", $"{CG}은(는) 담아 둔 소리를 먼 곳에서 흘려보냈다.");
                case "Death": return Say($"그리고 {VG}은(는) 숨을 거뒀어.", $"그리고 {VG}은(는) 숨을 거두었다.");
            }
            return null;
        }

        // ================================================================ shots per event
        ShotSpec Primary(LedgerEvent e, float side)
        {
            switch (e.Type)
            {
                case "Invite": case "CourierAsk": case "Handover": return new ShotSpec(ShotKind.Over, C, e.Target ?? V, side) { Rack = true };
                case "DisguiseOn": case "GuiseAs": case "ChangeClothes": case "DisguiseOff": return new ShotSpec(ShotKind.Medium, C, null, side);
                case "GatheringLeave": return new ShotSpec(ShotKind.Behind, C, null, side);
                case "GatheringReturn": case "RecorderPlay": case "EchoPlay": return new ShotSpec(ShotKind.Orbit, C, null, side);
                case "CarryStart": return new ShotSpec(ShotKind.Top, C, null, side);
                case "SealedRoom": case "LockedRoomMade": case "KeySlide": return new ShotSpec(ShotKind.Thing, C, null, side) { Point = _s.World.ToWorld(e.Pos) + Vector3.up * 1.0f };
                case "FakeMessage": case "TodShift": case "ColdHide": return new ShotSpec(ShotKind.Top, V, null, side);
                case "Stoke": case "NoiseMask": case "Circuit": return new ShotSpec(ShotKind.Low, C, null, side) { Dutch = true };
            }
            return new ShotSpec(ShotKind.Hand, C, null, side);
        }
        ShotSpec Second(LedgerEvent e, float side)
        {
            switch (e.Type)
            {
                case "Invite": case "CourierAsk": case "Handover": return new ShotSpec(ShotKind.Eyes, C, null, side);
                case "SealedRoom": case "LockedRoomMade": case "KeySlide": return new ShotSpec(ShotKind.Behind, C, null, side);
                case "Wash": case "Dose": case "Sedate": case "PoisonPlant": return new ShotSpec(ShotKind.Eyes, C, null, side);
                case "CarryStart": return new ShotSpec(ShotKind.Feet, C, null, side);
            }
            return new ShotSpec(ShotKind.Face, C, null, side);
        }

        // ================================================================ the story, in windows
        void Build()
        {
            var ev = _st.Seg.Events.OrderBy(e => e.Tick).ToList(); var plan = _st.Plan;
            bool murder = C != null && C != V && _s.World.ViewOf(C) != null;
            var death = ev.FirstOrDefault(e => e.Type == "Death" && e.Actor == V);
            var act = ev.FirstOrDefault(e => ActT.Contains(e.Type) && (e.Actor == C || e.Target == V) && (e.Type != "Strike" || e.Target == V));
            long tAct = act?.Tick ?? death?.Tick ?? _st.T1 - 120;
            long tDeath = death?.Tick ?? tAct + 30; if (death != null) _deathTick = death.Tick;
            // how long the weapon is in the hand
            if (murder && _weaponType != null)
            {
                var pick = ev.FirstOrDefault(e => e.Type == "PickUp" && e.Actor == C && (e.Item == _weaponId || (e.Data ?? "").StartsWith(_weaponType)));
                _holdFrom = pick?.Tick ?? Math.Max(_st.T0, tAct - 400);
                var drop = ev.FirstOrDefault(e => e.Actor == C && DisposeT.Contains(e.Type) && e.Tick >= tAct);
                _holdTo = drop?.Tick ?? tAct + 260;
            }
            int page = 0;
            // I. the resolve
            if (murder && plan != null)
            {
                long t = Math.Max(_st.T0 + 2, plan.StartTick + 2);
                var b = new Beat { Kicker = "결심", Layout = "wide3", Out = 0 };
                b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Establish, C), Tick = t, Span = 18, Pane = 1, Dur = 4.2f,
                    Caption = Say($"{When(t)}, 넌 {Motive(plan.Motive)} 마음을 굳혔어.", $"{When(t)}, {CG}은(는) {Motive(plan.Motive)} 마음을 굳혔다.") });
                b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Low, C, null, -1) { Dutch = true }, Tick = t + 18, Span = 14, Pane = 0, Keep = true, Dur = 3.4f });
                string prose = Replay.PlanProse(plan.Grammar);
                b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Eyes, C, null, 1), Tick = t + 32, Span = 12, Pane = 2, Dur = 4.4f,
                    Caption = string.IsNullOrEmpty(prose) ? null : Say($"계획은 이랬어 — {prose}", $"계획은 이랬다 — {prose}") });
                _beats.Add(b); page++;
            }
            // II. the preparation
            if (murder)
            {
                var prep = ev.Where(e => PrepT.Contains(e.Type) && e.Actor == C && e.Tick < tAct && (e.Type != "PickUp" || e.Item == _weaponId || (_weaponType != null && (e.Data ?? "").StartsWith(_weaponType))) && Line(e) != null)
                    .GroupBy(e => e.Type).Select(g => g.First()).OrderBy(e => e.Tick).Take(3).ToList();
                if (prep.Count > 0)
                {
                    var b = new Beat { Kicker = "준비", Layout = prep.Count >= 3 ? "trio" : "duo", Out = 1 };
                    for (int i = 0; i < prep.Count; i++)
                        b.Cuts.Add(new Cut { Shot = Primary(prep[i], i % 2 == 0 ? 1 : -1), Tick = prep[i].Tick - 4, Span = 14, Caption = Line(prep[i]), Dur = 3.8f });
                    if (prep.Count == 1) b.Cuts.Add(new Cut { Shot = Second(prep[0], -1), Tick = prep[0].Tick + 6, Span = 12, Dur = 3.2f });
                    _beats.Add(b); page++;
                }
            }
            // III. the approach
            {
                var (cp, _, cs, _) = murder ? _st.StateOf(C, tAct - 60) : (Vector3.zero, 0f, 0, false);
                var (vp, _, _, _) = _st.StateOf(V, tAct - 60);
                bool walking = murder && cs == 1 && (cp - vp).magnitude > 2.5f;
                var b = new Beat { Kicker = "접근", Layout = "strip", Out = 2 };
                if (walking)
                {
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Feet, C, null, 1), Tick = tAct - 110, Span = 50, Pane = 1, Dur = 3.2f, Sfx = "heartbeat",
                        Caption = Say($"그리고 {When(tAct)}, 넌 {VG}에게 다가갔어.", $"그리고 {When(tAct)}, {CG}은(는) {VG}에게 다가갔다.") });
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Behind, C, V, -1), Tick = tAct - 50, Span = 30, Pane = 0, Dur = 3.6f });
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Face, V, null, 1), Tick = tAct - 16, Span = 12, Pane = 2, Dur = 3.2f,
                        Caption = LineBank.FixParticles($"{VG}은(는) 아무것도 몰랐" + (_you ? "어." : "다.")) });
                    _beats.Add(b); page++;
                }
                else if (!murder || act == null || act.Type == "PoisonTaken" || act.Type == "TrapFired" || act.Type == "ShockFired" || act.Type == "PressContact")
                {
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Behind, V, null, 1), Tick = tAct - 90, Span = 40, Pane = 0, Dur = 3.6f,
                        Caption = LineBank.FixParticles($"{When(tAct)}, {VG}은(는) 혼자였" + (_you ? "어." : "다.")) });
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Feet, V, null, 1), Tick = tAct - 40, Span = 26, Pane = 1, Dur = 3f });
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Face, V, null, -1), Tick = tAct - 10, Span = 10, Pane = 2, Dur = 3f });
                    _beats.Add(b); page++;
                }
            }
            // IV. the act
            {
                var b = new Beat { Kicker = "그 순간", Layout = "quad", Out = 1 };
                var strike = ev.FirstOrDefault(e => e.Type == "Strike" && e.Target == V && e.Tick >= tAct);
                string actLine = act != null ? Line(act) : null;
                if (murder && act != null && act.Actor == C)
                {
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Two, C, V, 1) { Dutch = true }, Tick = tAct - 6, Span = 22, Pane = 0, Dur = 4.2f, Caption = actLine, Sfx = "organ_sting", Tilt = -1.2f });
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Hand, C, null, -1), Tick = (strike?.Tick ?? tAct + 4) - 5, Span = 12, Pane = 2, Dur = 3.4f, Caption = strike != null ? Line(strike) : null, Tilt = 1f });
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Eyes, V, null, 1), Tick = (strike?.Tick ?? tAct + 6) + 2, Span = 10, Pane = 1, Dur = 3.2f });
                }
                else
                {
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Low, V, null, 1) { Dutch = true }, Tick = tAct - 6, Span = 20, Pane = 0, Dur = 4f, Caption = actLine ?? LineBank.FixParticles($"{When(tAct)}, {VG}에게 그 일이 닥쳤" + (_you ? "어." : "다.")) });
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Eyes, V, null, -1), Tick = tAct + 4, Span = 10, Pane = 1, Dur = 3f });
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Hand, V, null, 1), Tick = tAct + 12, Span = 10, Pane = 2, Dur = 3f });
                }
                bool thud = act != null && (act.Type == "Strike" || act.Type == "AttackBegin" || act.Type == "Shove" || act.Type == "PressContact");
                b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Top, V, null, 1), Tick = tDeath - 4, Span = 16, Pane = 3, Dur = 4.2f, Caption = death != null ? Line(death) : null, Word = thud ? "쿵" : null, WordAt = new Vector2(0.8f, 0.24f) });
                _beats.Add(b); page++;
            }
            // V. what was done afterwards
            if (murder)
            {
                var hide = ev.Where(e => HideT.Contains(e.Type) && (e.Actor == C) && e.Tick >= tAct && Line(e) != null).GroupBy(e => e.Type).Select(g => g.First()).OrderBy(e => e.Tick).Take(3).ToList();
                if (hide.Count > 0)
                {
                    var b = new Beat { Kicker = "감춤", Layout = hide.Count >= 3 ? "trio" : "duo", Out = 2 };
                    for (int i = 0; i < hide.Count; i++)
                    {
                        var e = hide[i]; bool seal = e.Type == "SealedRoom" || e.Type == "LockedRoomMade" || e.Type == "KeySlide";
                        b.Cuts.Add(new Cut { Shot = Primary(e, i % 2 == 0 ? -1 : 1), Tick = e.Tick - 5, Span = 14, Caption = Line(e), Dur = 3.9f, Word = seal ? "철컥" : null, WordAt = new Vector2(0.24f, 0.78f), Sfx = seal ? "door_lock" : null });
                    }
                    if (hide.Count == 1) b.Cuts.Add(new Cut { Shot = Second(hide[0], 1), Tick = hide[0].Tick + 6, Span = 12, Dur = 3.2f });
                    _beats.Add(b); page++;
                }
            }
            // VI. the discovery
            {
                var seen = ev.FirstOrDefault(e => e.Type == "BodySeen" && e.Target == V && e.Actor != C && e.Actor != Cast.Butler) ?? ev.FirstOrDefault(e => e.Type == "BodySeen" && e.Target == V);
                if (seen != null && _s.World.ViewOf(seen.Actor) != null)
                {
                    var sealedF = ev.FirstOrDefault(e => e.Type == "SealedFound");
                    string d = Who(seen.Actor);
                    var b = new Beat { Kicker = "발견", Layout = "lancets", Out = 0 };
                    string cap = d == "나" ? (_you ? $"{When(seen.Tick)}, 쓰러진 {VG}을(를) 발견한 건 나였어." : $"{When(seen.Tick)}, 쓰러진 {VG}을(를) 발견한 것은 나였다.") : (_you ? $"{When(seen.Tick)}, {d}이(가) 쓰러진 {VG}을(를) 발견했어." : $"{When(seen.Tick)}, {d}이(가) 쓰러진 {VG}을(를) 발견했다.");
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Face, seen.Actor, null, 1) { Dutch = true }, Tick = seen.Tick + 1, Span = 12, Pane = 0, Dur = 3.6f, Caption = LineBank.FixParticles(cap), Sfx = "gasp" });
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Establish, V), Tick = seen.Tick + 12, Span = 16, Pane = 1, Dur = 3.8f, Caption = sealedF != null ? Say("문은 안에서 잠겨 있었지. 밀실처럼.", "문은 안에서 잠겨 있었다. 밀실처럼.") : null });
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Top, V, null, -1), Tick = seen.Tick + 26, Span = 12, Pane = 2, Dur = 3.4f });
                    _beats.Add(b); page++;
                }
            }
            // VII. what gave it away
            if (murder)
            {
                var lie = ev.FirstOrDefault(e => e.Type == "Lie" && e.Actor == C && e.Tick >= tAct);
                if (lie != null)
                {
                    string claim = LieText(lie.Data);
                    var b = new Beat { Kicker = "어긋남", Layout = "rose", Out = 1 };
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Medium, C, null, 1), Tick = lie.Tick - 2, Span = 12, Pane = 0, Dur = 3.8f, Keep = true,
                        Caption = Say($"그리고 넌 말했지. {claim}", $"그리고 {CG}은(는) 말했다. {claim}") });
                    if (lie.Target != null && _s.World.ViewOf(lie.Target) != null) b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Over, C, lie.Target, -1), Tick = lie.Tick + 10, Span = 10, Pane = 1, Dur = 3.2f });
                    else b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Behind, C, null, -1), Tick = lie.Tick + 10, Span = 10, Pane = 1, Dur = 3.2f });
                    b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Eyes, C, null, 1), Tick = lie.Tick + 20, Span = 10, Pane = 2, Dur = 4f,
                        Caption = Say("그 한마디가 어긋났어. 거기서부터 전부 풀렸지.", "그 한마디가 어긋났다. 거기서부터 전부 풀렸다.") });
                    _beats.Add(b); page++;
                }
            }
            // VIII. the last window
            if (murder)
            {
                var b = new Beat { Kicker = "그날", Layout = "single", Out = 3 };
                b.Cuts.Add(new Cut { Shot = new ShotSpec(ShotKind.Orbit, C, null, 1), Tick = _st.T1 - 30, Span = 24, Pane = 0, Dur = 5.2f,
                    Caption = Say($"이게 그날 있었던 일의 전부야, {CG}.", $"이것이 그날 있었던 일의 전부다. 그리고 우리는, 그걸 보지 못했다.") });
                _beats.Add(b);
            }
        }

        static string LieText(string data)
        {
            if (string.IsNullOrEmpty(data)) return "그 시각엔 다른 곳에 있었다고.";
            var p = data.Split(new[] { ' ' }, 3);
            if (p[0] == "alibi" && p.Length >= 2) return LineBank.FixParticles($"그 시각 {p[1]}에 있었다고." );
            return "그 시각엔 다른 곳에 있었다고.";
        }

        // ================================================================ playing it
        // (a probe run watches the whole thing: stray keys or clicks on its window must not cut it short)
        static bool Advance() => !AutoProbe.Active && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0));
        static bool Esc() => !AutoProbe.Active && Input.GetKeyDown(KeyCode.Escape);

        public IEnumerator Play(bool fast)
        {
            _m.Show(); _m.CaptionVisible(true); _m.Hint("E  다음      Esc  건너뛰기");
            string sub = LineBank.FixParticles($"{Cast.NameOf(V)}의 죽음" + (C != null && C != V ? "  —  " + (_you ? "지목은 옳았다" : "아무도 보지 못한 것") : ""));
            if (!fast) yield return _m.Title("마지막으로 되짚는다", "그날의 재구성", sub, 1.8f, () => Advance() || Esc());
            if (Esc()) { Skipped = true; _m.Hide(); yield break; }
            int shot = 0;
            for (int bi = 0; bi < _beats.Count && !Skipped; bi++)
            {
                var b = _beats[bi]; var panes = _m.NewPage(b.Layout, bi);
                _m.Focus(null); int next = 0; string kicker = Roman(bi + 1) + "  ·  " + b.Kicker;
                _m.Caption("", kicker);
                for (int ci = 0; ci < b.Cuts.Count && !Skipped; ci++)
                {
                    var c = b.Cuts[ci]; int pi = c.Pane >= 0 && c.Pane < panes.Count ? c.Pane : Math.Min(next, panes.Count - 1); next = Math.Max(next, pi + 1);
                    var pane = panes[pi];
                    // the moment
                    _st.Frozen = false; _st.Sounds = !fast;
                    if (double.IsNaN(_st.T) || Math.Abs(_st.T - c.Tick) > 1.5) _st.Seek(c.Tick);
                    bool hold = _weaponType != null && C != null && c.Tick >= _holdFrom && c.Tick <= _holdTo;
                    if (hold) _st.HoldProp(C, _weaponType, _weaponId); else _st.ClearProp();
                    yield return null; yield return null;   // the animators pose the new moment before we frame it
                    CineShot sh = null;
                    try { sh = CineSolver.Solve(c.Shot); } catch (Exception ex) { Debug.LogException(ex); }
                    if (AutoProbe.Active && sh != null) Debug.Log($"[CINE] recap {b.Kicker}#{ci} {c.Shot} -> {sh.Spec} target {(sh.Target != null ? sh.Target() : sh.Base):F2} off {sh.O0:F2}");
                    if (sh == null) continue;
                    float dur = fast ? 0.35f : c.Dur;
                    _m.Go(pane, sh, dur + 1.6f);
                    if (!fast) { var spec = sh.Spec; var pn = pane; float goAt = Time.unscaledTime; _m.WatchDark(pn, 0.3f, () => { var fb = Wider(spec); if (fb == null) return; CineShot s2 = null; try { s2 = CineSolver.Solve(fb); } catch (Exception ex) { Debug.LogException(ex); } if (s2 != null && pn.Cam != null) _m.Go(pn, s2, Mathf.Max(1f, dur + 1.6f - (Time.unscaledTime - goAt))); }); }
                    _m.Focus(pane, c.Tilt != 0 ? c.Tilt : (ci % 2 == 0 ? -0.6f : 0.6f));
                    if (c.Caption != null) _m.Caption(c.Caption, kicker);
                    if (c.Sfx != null && !fast) Sfx.Play(c.Sfx, null, c.Sfx == "heartbeat" ? 0.5f : 0.35f);
                    var asm = _m.Assemble(pane, fast ? 0.2f : 0.7f); _s.StartCoroutine(asm);
                    if (c.Word != null && _words < 2 && !fast) { _words++; _s.StartCoroutine(DelayedWord(pane, c.Word, c.WordAt, 0.9f)); }
                    double start = _st.T, span = c.Span; float t0 = Time.unscaledTime;
                    _st.Rate = (float)(span / Math.Max(0.3, dur * SimTime.PerSecond));
                    bool shotTaken = false;
                    while (true)
                    {
                        float k = Mathf.Clamp01((Time.unscaledTime - t0) / dur);
                        _st.Advance(start + span * k);
                        if (Esc()) { Skipped = true; break; }
                        if (!fast && Time.unscaledTime - t0 > 0.9f && Advance()) { if (!_m.CaptionDone) _m.CaptionFinish(); else break; }
                        if (AutoProbe.Active && !shotTaken && Time.unscaledTime - t0 > dur * 0.62f && (b.Kicker == "그 순간" && ci == 0)) { shotTaken = true; AutoProbe.Shot("cine_recap_act_live"); }
                        if (AutoProbe.Active && !_probeAlive && _st.T < _deathTick - 40 && _st.T > _deathTick - 400 && Time.unscaledTime - t0 > dur * 0.5f && c.Shot.Subject == V) { _probeAlive = true; AutoProbe.Shot("cine_recap_victim_alive"); Debug.Log($"[CINE] recap victim alive shot at tick {_st.T:0} (death at {_deathTick})"); }
                        if (AutoProbe.Active && !_probeDeath && V != null && _st.StateOf(V).status == 3) { if (_deadSeenAt < 0f) _deadSeenAt = Time.unscaledTime; else if (Time.unscaledTime - _deadSeenAt > 1.25f) { _probeDeath = true; AutoProbe.Shot("cine_recap_death_live"); Debug.Log($"[CINE] recap death shot at tick {_st.T:0} (death at {_deathTick}), {Time.unscaledTime - _deadSeenAt:0.00}s into the collapse"); } }
                        _ftSum += Time.unscaledDeltaTime; _ftN++;
                        if (Time.unscaledTime - t0 >= dur) break;
                        yield return null;
                    }
                    if (!c.Keep) _m.Freeze(pane);
                }
                if (Skipped) break;
                if (AutoProbe.Active && _ftN > 0) { Debug.Log($"[CINE] recap page {bi + 1} {b.Kicker} avg frame {1000f * _ftSum / _ftN:0.0} ms over {_ftN} frames"); } _ftSum = 0; _ftN = 0;
                // the whole window, before it goes
                _m.FreezeAll(); _m.Focus(null); _st.Frozen = true;
                float h0 = Time.unscaledTime; bool shot1 = false;
                while (Time.unscaledTime - h0 < (fast ? 0.1f : 2.2f))
                {
                    if (!shot1 && Time.unscaledTime - h0 > 1.1f) { shot1 = true; if (AutoProbe.Active) AutoProbe.Shot($"cine_recap_{++shot}"); }
                    if (Esc()) { Skipped = true; break; }
                    if (Time.unscaledTime - h0 > 0.5f && Advance()) break;
                    yield return null;
                }
                if (Skipped) break;
                if (bi == _beats.Count - 1) break;
                switch (b.Out) { case 0: yield return _m.Glide(-1f, fast ? 0.2f : 0.65f); break; case 1: yield return _m.Flare(fast ? 0.15f : 0.45f); break; case 2: yield return _m.Fold(fast ? 0.2f : 0.5f); break; default: yield return _m.Fold(fast ? 0.2f : 0.6f); break; }
            }
            Shots = shot;
            _st.ClearProp(); _st.Frozen = false;
            // out: the last window dims away
            if (!Skipped && !fast) yield return _m.Fold(0.9f);
            _m.Hide();
        }

        IEnumerator DelayedWord(PaneMontage.Pane p, string w, Vector2 at, float delay)
        {
            float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < delay) yield return null;
            Sfx.Play(w == "철컥" ? "door_lock" : "body_fall", null, 0.55f);
            yield return _m.SoundWord(p, w, at, w == "철컥" ? 6f : -8f);
        }

        /// <summary>The next wider shot on the same subject (for a pane that came out black).</summary>
        static ShotSpec Wider(ShotSpec s)
        {
            if (s == null) return null;
            switch (s.Kind)
            {
                case ShotKind.Eyes: case ShotKind.Face: case ShotKind.Hand: case ShotKind.Low: case ShotKind.Two: case ShotKind.Over: case ShotKind.Feet: case ShotKind.Behind: case ShotKind.Thing:
                    return s.Subject != null ? new ShotSpec(ShotKind.Medium, s.Subject, null, -s.Side) : new ShotSpec(ShotKind.Establish, null) { Point = s.Point };
                case ShotKind.Medium: case ShotKind.Orbit: case ShotKind.Top: return new ShotSpec(ShotKind.Establish, s.Subject) { Point = s.Point };
            }
            return null;
        }

        static string Roman(int n) { string[] r = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" }; return r[Mathf.Clamp(n - 1, 0, r.Length - 1)]; }
    }
}
