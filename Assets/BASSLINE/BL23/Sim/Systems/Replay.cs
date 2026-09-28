using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>Always-on small recorder. Frames every 0.5 sim-second for every actor; incidents pin the buffer from the
    /// plan start so the read-only 3D reveal can replay preparation → act → concealment → discovery.</summary>
    public static class Replay
    {
        static List<ReplayFrame> Buf(GameState S) { if (S.ReplayBuf == null) S.ReplayBuf = new List<ReplayFrame>(); return S.ReplayBuf; }
        public static readonly string[] Order = Cast.All.Select(c => c.Id).ToArray();
        public const int Stride = 8; // x,z,f,yaw,pose,anim,alive,carry

        public static void Record(Simulation sim)
        {
            var S = sim.S; var b = Buf(S);
            var data = new float[Order.Length * Stride];
            for (int i = 0; i < Order.Length; i++)
            {
                var a = S.A(Order[i]); if (a == null) continue; int o = i * Stride;
                data[o] = a.Pos.x; data[o + 1] = a.Pos.z; data[o + 2] = a.Pos.f; data[o + 3] = a.Yaw; data[o + 4] = (int)a.Pose; data[o + 5] = (int)a.Anim + (a.Speed > 0.1f ? 1000 * (a.Running ? 2 : 1) : 0);
                data[o + 6] = a.Status == ActorStatus.Active ? 1 : a.Status == ActorStatus.Unconscious ? 2 : a.Status == ActorStatus.Dead ? 3 : 0;
                data[o + 7] = a.Carrying != null ? Array.IndexOf(Order, a.Carrying) : -1;
            }
            b.Add(new ReplayFrame { Tick = S.Tick, Clock = S.Clock, Data = data });
            // keep ~6 hours of daily time unless pinned earlier
            if (S.Tick % 50 != 0) return;
            // pin = earliest start among live plans and incidents whose reveal segment hasn't been frozen yet
            long pin = long.MaxValue;
            foreach (var pl in S.Plans.Values) if (pl.Stage != "Aborted" && pl.Stage != "Done") pin = Math.Min(pin, pl.StartTick);
            foreach (var inc in S.Incidents.Values) if (!S.Replays.Any(r => r.Incident == inc.Id)) pin = Math.Min(pin, inc.SegmentStartTick);
            long keepFrom = Math.Min(pin, S.Tick - 4000);
            int cut = b.FindIndex(f => f.Tick >= keepFrom);
            if (cut > 50) b.RemoveRange(0, cut);
            // very long pins (a plan brewing for days) keep full detail only for the last stretch
            if (b.Count > 9000) { var old = b.Take(b.Count - 4000).Where((f, i) => i % 2 == 0).ToList(); b.RemoveRange(0, b.Count - 4000); b.InsertRange(0, old); }
        }

        public static void MarkPlanStart(Simulation sim, MurderPlan plan) { Pin(sim, plan.StartTick); }

        public static void Pin(Simulation sim, long fromTick)
        {
            var S = sim.S; double cur = S.Flags.TryGetValue("replaypin", out var p) ? p : double.MaxValue;
            if (fromTick < cur) S.Flags["replaypin"] = fromTick;
        }

        /// <summary>Freeze segments for closed incidents (called before the reveal). Downsamples long stretches.</summary>
        public static List<ReplaySegment> BuildSegments(Simulation sim)
        {
            var S = sim.S; var b = Buf(S); var res = new List<ReplaySegment>();
            foreach (var inc in S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter).OrderBy(i => i.ResultSeq))
            {
                if (S.Replays.Any(r => r.Incident == inc.Id)) { res.Add(S.Replays.First(r => r.Incident == inc.Id)); continue; }
                long t0 = inc.SegmentStartTick, t1 = S.Ledger.Where(e => e.Type == "BodySeen" && e.Target == inc.Victim).Select(e => e.Tick).DefaultIfEmpty(S.Tick).Min() + 60;
                // someone found alive and dying: the film runs on to the moment they actually die
                long death = S.Ledger.Where(e => e.Type == "Death" && e.Actor == inc.Victim && e.Tick >= t0).Select(e => e.Tick).DefaultIfEmpty(long.MinValue).Min();
                if (death != long.MinValue && death + 40 > t1 && death - t1 < 1200) t1 = Math.Min(S.Tick, death + 40);
                var frames = b.Where(f => f.Tick >= t0 && f.Tick <= t1).ToList();
                if (frames.Count > 2400) { int step = frames.Count / 2400 + 1; frames = frames.Where((f, i) => i % step == 0 || f.Tick >= t1 - 1200).ToList(); }
                var seg = new ReplaySegment { Incident = inc.Id, T0 = t0, T1 = t1, Frames = frames, LayoutHash = S.Layout.Hash };
                var who = new HashSet<string> { inc.Victim }; if (inc.Culprit != null) who.Add(inc.Culprit);
                // the go-between of a courier plan belongs to the story too
                foreach (var e in S.Ledger.Where(e => e.Type == "CourierAsk" && e.Actor == inc.Culprit && e.Data == inc.Victim)) who.Add(e.Target);
                seg.Events = S.Ledger.Where(e => e.Tick >= t0 && e.Tick <= t1 && (who.Contains(e.Actor) || who.Contains(e.Target) || e.Type == "Circuit" || e.Type == "PressArmed" || e.Type == "Death" || e.Type == "Strike" && e.Actor == null || e.Type == "BodySeen" || e.Type == "RecorderPlay" && e.Actor == inc.Culprit || e.Type == "TrapFired" && e.Actor == inc.Culprit)).ToList();
                seg.Actors = Order.ToList();
                S.Replays.Add(seg); res.Add(seg);
            }
            S.Flags.Remove("replaypin");
            return res;
        }

        /// <summary>Human-readable reveal script (the "실제 행동" captions) from the recorded ledger — no invented motives.</summary>
        public static List<(long tick, double clock, string text, string actor)> Script(GameState S, ReplaySegment seg)
        {
            var inc = S.Incidents.TryGetValue(seg.Incident, out var i) ? i : null; var plan = inc?.PlanId != null && S.Plans.TryGetValue(inc.PlanId, out var p) ? p : null;
            var res = new List<(long, double, string, string)>();
            if (plan != null) res.Add((plan.StartTick, plan.Formed, LineBank.FixParticles($"{Cast.NameOf(plan.Actor)}은(는) {MotiveProse(plan.Motive)} 살인을 결심한다. {PlanProse(plan.Grammar)}"), plan.Actor));
            foreach (var e in seg.Events)
            {
                string t = null;
                // --- violence track: a shot is aimed, not "rushed at"; a bullet is not a stab; a body too heavy to lift is dragged ("" = no line)
                { var vt = Violence.ReplayLine(S, inc, e); if (vt != null) { if (vt.Length > 0) res.Add((e.Tick, e.Clock, LineBank.FixParticles(vt), e.Actor)); continue; } }
                switch (e.Type)
                {
                    case "PickUp": if (e.Actor == inc?.Culprit) t = $"{Cast.GivenOf(e.Actor)}, {S.RoomName(e.Room)}에서 {ItemCatalog.Get(e.Data?.Split(' ')[0])?.Kor ?? "물건"}을(를) 손에 넣는다"; break;
                    case "Invite": t = $"{Cast.GivenOf(e.Actor)}, {Cast.GivenOf(e.Target)}에게 {S.RoomName(e.Room)}에서 만나자고 한다"; break;
                    case "Circuit": t = ((e.Data ?? "").EndsWith("off") ? "저택 한쪽의 불이 꺼진다" : "꺼졌던 불이 다시 들어온다") + (e.Actor != null ? $" — {Cast.GivenOf(e.Actor)}이(가) 레버를 당겼다" : ""); break;
                    case "DisguiseOn": t = $"{Cast.GivenOf(e.Actor)}, 다른 사람의 차림으로 꾸민다"; break;
                    case "DisguiseOff": t = $"{Cast.GivenOf(e.Actor)}, {S.RoomName(e.Room)}에서 변장을 벗어 감춘다"; break;
                    case "AttackBegin": t = $"{Cast.GivenOf(e.Actor)}, {S.RoomName(e.Room)}에서 {Cast.GivenOf(e.Target)}에게 달려든다"; break;
                    case "Strike": { var parts = (e.Data ?? "").Split('/'); if (parts.Length >= 3 && Enum.TryParse(parts[0], out BodyRegion r) && Enum.TryParse(parts[1], out DamageType d)) t = r == BodyRegion.Head && d == DamageType.Choke ? "  └ 얼굴이 짓눌려 숨이 막힌다" : $"  └ {WoundText.Region(r)}에 {WoundText.Type(d)}이(가) 남는다"; break; }
                    case "Resist": t = $"{Cast.GivenOf(e.Actor)}, 저항한다"; break;
                    case "Flee": t = $"{Cast.GivenOf(e.Actor)}, 달아난다"; break;
                    case "Death": t = $"{Cast.NameOf(e.Actor)}, 숨을 거둔다 — {e.Data}"; break;
                    case "CarryStart": t = $"{Cast.GivenOf(e.Actor)}, {Cast.GivenOf(e.Target)}을(를) 끌고 간다"; break;
                    case "CarryEnd": t = $"{Cast.GivenOf(e.Actor)}, {Cast.GivenOf(e.Target)}을(를) {S.RoomName(e.Room)}에 내려놓는다"; break;
                    case "PressArmed": t = $"{Cast.GivenOf(e.Actor)}, 프레스가 저절로 내려오도록 타이머를 맞춰 둔다"; break;
                    case "PressContact": t = $"프레스가 내려와 {Cast.GivenOf(e.Target)}을(를) 짓누른다"; break;
                    case "LockedRoomMade": t = $"{Cast.GivenOf(e.Actor)}, 피해자의 열쇠로 밖에서 문을 잠근다"; break;
                    case "SealedRoom": t = $"{Cast.GivenOf(e.Actor)}, 실을 안쪽 잠금쇠에 걸어 문틈으로 당긴다 — 열쇠는 {Cast.GivenOf(e.Target)}의 주머니에 그대로 남는다"; break;
                    case "FakeMessage": { var pd = (e.Data ?? "").Split('|'); t = $"{Cast.GivenOf(e.Actor)}, {Cast.GivenOf(e.Target)}의 손가락으로 바닥에 「{(pd.Length > 1 ? pd[1] : "?")}」 자를 쓴다 — 죽어 가며 남긴 글씨처럼 꾸며 {Cast.GivenOf(pd[0])}에게 죄를 씌우려고"; break; }
                    case "PlantWeapon": t = $"{Cast.GivenOf(e.Actor)}, {ItemCatalog.Get(e.Data)?.Kor ?? "다른 물건"}에 피를 묻혀 시신 곁에 둔다 — 진짜 흉기는 따로 치운다"; break;
                    case "Dose": t = $"{Cast.GivenOf(e.Actor)}, {Cast.GivenOf(e.Target)}의 잔에 디기탈리스를 탄다 — 약효는 30분쯤 뒤에 나타난다"; break;
                    case "SawNearCup": t = $"  └ {Cast.GivenOf(e.Actor)}은(는) {Cast.GivenOf(e.Target)}의 손이 잔 가까이 가는 걸 본다"; break;
                    case "Stoke": t = $"{Cast.GivenOf(e.Actor)}, 벽난로에 장작을 가득 넣어 불을 키운다"; break;
                    case "TodShift": { var pd = (e.Data ?? "").Split('|'); t = pd[0] == "heat" ? $"{Cast.GivenOf(e.Actor)}, 시신을 불 가까이 옮긴다 — 천천히 식게 해서 숨진 시각이 늦어 보이도록" : $"{Cast.GivenOf(e.Actor)}, 시신을 {S.RoomName(e.Room)}의 찬 곳에 둔다 — 빨리 식게 해서 숨진 시각이 일러 보이도록"; break; }
                    case "SealedFound": t = $"문은 안에서 잠겨 있었다 — 겉보기엔 밀실이다"; break;
                    case "Wash": t = $"{Cast.GivenOf(e.Actor)}, {S.RoomName(e.Room)}에서 흉기를 씻는다"; break;
                    case "HideItem": if (e.Actor == inc?.Culprit) t = $"{Cast.GivenOf(e.Actor)}, {S.RoomName(e.Room)}에 {ItemCatalog.Get(e.Data)?.Kor ?? "무언가"}을(를) 숨긴다"; break;
                    case "ChangeClothes": t = $"{Cast.GivenOf(e.Actor)}, 옷을 갈아입는다"; break;
                    case "Lie": t = $"{Cast.GivenOf(e.Actor)}의 거짓말 — {e.Data}"; break;
                    case "BodySeen": t = $"{Cast.GivenOf(e.Actor)}, 쓰러진 {Cast.GivenOf(e.Target)}을(를) 발견한다"; break;
                    case "PlanAbort": t = $"{Cast.GivenOf(e.Actor)}, {WhyKor(e.Data)} 계획을 접는다"; break;
                    case "PlanRevise": t = $"{Cast.GivenOf(e.Actor)}, {WhyKor(e.Data)} 계획을 고친다"; break;
                    case "TrapArmed": if ((e.Data ?? "").EndsWith(" Shock")) break; t = $"{Cast.GivenOf(e.Actor)}, {S.RoomName(e.Room)}에 함정을 설치한다"; break;
                    case "TrapFired": if ((e.Data ?? "").EndsWith(" Shock") || (e.Data ?? "").EndsWith(" Bedtime")) break; t = $"함정이 작동한다 — {Cast.GivenOf(e.Target)}이(가) 걸려든다"; break;
                    case "TrapDisarmed": t = $"{Cast.GivenOf(e.Actor)}, 함정을 치운다"; break;
                    case "RecorderArmed": t = $"{Cast.GivenOf(e.Actor)}, 자기 방에서 목소리를 녹음하고, 나중에 저절로 틀어지도록 맞춰 둔다"; break;
                    case "RecorderPlay": t = $"녹음기가 켜진다 — 방에서 {Cast.GivenOf(e.Target)}의 목소리가 흘러나오지만, 정작 본인은 그곳에 없다"; break;
                    case "EchoRecord": t = $"{Cast.GivenOf(e.Actor)}, 권능 '메아리'로 방 안의 소리를 담아 둔다"; break;
                    case "EchoPlay": t = $"{Cast.GivenOf(e.Actor)}, 담아 둔 소리를 멀리서 울리게 한다 ({S.RoomName(e.Room)})"; break;
                    case "GuiseAs": t = $"{Cast.GivenOf(e.Actor)}, 권능 '외피'로 {Cast.GivenOf(e.Target)}의 차림을 흉내 낸다"; break;
                    case "Ability": if (e.Actor == inc?.Culprit) t = $"{Cast.GivenOf(e.Actor)}, '{Abilities.Get(e.Data)?.Kor ?? e.Data}'의 권능을 쓴다"; break;
                    case "CourierAsk": t = $"{Cast.GivenOf(e.Actor)}, {Cast.GivenOf(e.Target)}에게 쪽지를 전해 달라고 부탁한다 — 보낸 사람은 밝히지 않는다"; break;
                    case "Handover": if (S.I(e.Item)?.Type == "Invitation") t = $"{Cast.GivenOf(e.Actor)}, {Cast.GivenOf(e.Target)}에게 쪽지를 건넨다"; break;
                    case "GatheringLeave": if (e.Actor == inc?.Culprit) t = $"{Cast.GivenOf(e.Actor)}, 모임에서 슬쩍 자리를 비운다"; break;
                    case "GatheringReturn": if (e.Actor == inc?.Culprit) t = $"{Cast.GivenOf(e.Actor)}, 아무 일 없었다는 듯 모임으로 돌아온다"; break;
                    case "PostmortemDamage": t = $"  └ 숨이 멎은 뒤에도 {(Enum.TryParse((e.Data ?? "").Split('/')[0], out BodyRegion pr) ? WoundText.Region(pr) : "몸")}에 상처가 더해진다"; break;
                    default: t = Methods.Caption(S, inc, e); break;   // second-wave methods (MethodsClues.cs)
                }
                if (t != null) res.Add((e.Tick, e.Clock, LineBank.FixParticles(t), e.Actor));
            }
            return res;
        }

        public static string GrammarKor(string g)
        {
            if (string.IsNullOrEmpty(g)) return g;
            // "Lure→Ambush+Recorder+Silence": base grammar, what it turned into, and the layers added to it
            var parts = g.Split('+'); var chain = parts[0].Split('→');
            string s = One(chain[0]); if (chain.Length > 1) s += $" (도중에 바꿈 → {One(chain[1])})";
            for (int i = 1; i < parts.Length; i++) s += " + " + One(parts[i]);
            return s;
        }
        static string One(string g)
        {
            switch (g)
            {
                case "Ambush": return "혼자 있을 때 기습"; case "Lure": return "약속을 잡아 불러냄"; case "NightVisit": return "한밤중에 방으로 찾아감"; case "Blackout": return "정전을 틈탄 습격";
                case "Press": return "기절시킨 뒤 멀리서 프레스 작동"; case "Drown": return "수영장 물에 빠뜨림"; case "Disguise": return "변장하고 습격";
                case "Trap": return "미리 설치해 둔 함정"; case "Gathering": return "모임 도중 슬쩍 빠져나와 범행";
                case "Recorder": return "녹음한 목소리로 만든 알리바이"; case "Courier": return "남을 시켜 전한 쪽지"; case "Silence": return "권능 '정적'"; case "Guise": return "권능 '외피'";
                case "Seal": case "Tod": case "Message": case "Swap": case "Poison": return SetPieces.Kor(g);
                case "Blur": return "권능 '흐림'"; case "Echo": return "권능 '메아리'"; case "Fix": return "권능 '고정'"; case "Weight": return "권능 '무게'";
            }
            return Methods.GrammarKor(g) ?? g;
        }
        /// <summary>The plan in plain sentences for the reveal ("약속으로 불러내고, 시신을 데우거나 식혀 숨진 시각을 속인다."); at most three clauses per sentence.</summary>
        public static string PlanProse(string g)
        {
            if (string.IsNullOrEmpty(g)) return "";
            var parts = g.Split('+'); var chain = parts[0].Split('→');
            var steps = new List<(string conn, string fin)> { Verb(chain[chain.Length - 1]) };
            if (chain.Length > 1) steps[0] = ("처음 계획을 바꿔 " + steps[0].conn, "처음 계획을 바꿔 " + steps[0].fin);
            for (int i = 1; i < parts.Length; i++) steps.Add(Verb(parts[i]));
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < steps.Count; i++) sb.Append(i == steps.Count - 1 ? steps[i].fin + "." : i % 3 == 2 ? steps[i].fin + ". 그리고 " : steps[i].conn + ", ");
            return sb.ToString();
        }
        static (string, string) Verb(string g)
        {
            switch (g)
            {
                case "Ambush": return ("혼자 있는 틈을 노리고", "혼자 있는 틈을 노린다");
                case "Lure": return ("약속으로 불러내고", "약속으로 불러낸다");
                case "NightVisit": return ("밤에 방으로 찾아가고", "밤에 방으로 찾아간다");
                case "Blackout": return ("불을 꺼 주변을 어둡게 만들고", "불을 꺼 주변을 어둡게 만든다");
                case "Press": return ("기절시킨 뒤 멀리서 프레스를 작동시키고", "기절시킨 뒤 멀리서 프레스를 작동시킨다");
                case "Drown": return ("물속에 밀어 넣고", "물속에 밀어 넣는다");
                case "Disguise": return ("다른 사람처럼 꾸미고", "다른 사람처럼 꾸민다");
                case "Trap": return ("미리 함정을 설치해 두고", "미리 함정을 설치해 둔다");
                case "Gathering": return ("모임 도중 잠시 빠져나오고", "모임 도중 잠시 빠져나온다");
                case "Recorder": return ("녹음한 목소리로 알리바이를 만들고", "녹음한 목소리로 알리바이를 만든다");
                case "Courier": return ("남의 손을 빌려 쪽지를 전하고", "남의 손을 빌려 쪽지를 전한다");
                case "Silence": return ("권능 '정적'으로 소리를 지우고", "권능 '정적'으로 소리를 지운다");
                case "Guise": return ("권능 '외피'로 남의 모습을 빌리고", "권능 '외피'로 남의 모습을 빌린다");
                case "Seal": return ("실 한 가닥으로 문을 안에서 잠근 것처럼 꾸미고", "실 한 가닥으로 문을 안에서 잠근 것처럼 꾸민다");
                case "Tod": return ("시신을 데우거나 식혀 숨진 시각을 속이고", "시신을 데우거나 식혀 숨진 시각을 속인다");
                case "Message": return ("죽어 가며 남긴 것처럼 글씨를 꾸며 다른 사람에게 죄를 씌우고", "죽어 가며 남긴 것처럼 글씨를 꾸며 다른 사람에게 죄를 씌운다");
                case "Swap": return ("엉뚱한 흉기를 남겨 두고", "엉뚱한 흉기를 남겨 둔다");
                case "Poison": return ("잔에 독을 타고", "잔에 독을 탄다");
                case "Blur": return ("권능 '흐림'으로 기억을 흐리고", "권능 '흐림'으로 기억을 흐린다");
                case "Echo": return ("권능 '메아리'로 소리를 다른 곳에서 울리게 하고", "권능 '메아리'로 소리를 다른 곳에서 울리게 한다");
                case "Fix": return ("권능 '고정'을 쓰고", "권능 '고정'을 쓴다");
                case "Weight": return ("권능 '무게'를 쓰고", "권능 '무게'를 쓴다");
            }
            var mv = Methods.Verb(g); if (mv.HasValue) return mv.Value;
            var o = One(g); return (o + "을(를) 쓰고", o + "을(를) 쓴다");
        }
        static readonly Dictionary<string, string> _why = new Dictionary<string, string>
        {
            ["interrupted"] = "일이 틀어져서", ["stuck"] = "발이 묶여서", ["locked door"] = "문이 잠겨 있어서", ["item taken"] = "노리던 물건을 누가 가져가서",
            ["partner kept moving"] = "상대가 자꾸 자리를 옮겨서", ["victim didn't come"] = "상대가 약속에 나오지 않아서", ["can't reach target"] = "상대에게 다가갈 수 없어서",
            ["lost target"] = "상대를 놓쳐서", ["follow lost"] = "뒤를 밟다 놓쳐서", ["couldn't catch target"] = "상대를 붙잡지 못해서", ["weapon missing"] = "흉기가 사라져서",
            ["found weapon"] = "다른 흉기를 찾아서", ["no room"] = "마땅한 장소가 없어서", ["invitation refused"] = "초대를 거절당해서", ["door stayed shut"] = "문이 열리지 않아서",
            ["no key"] = "열쇠가 없어서", ["spot taken"] = "자리가 차 있어서", ["partner gone"] = "상대가 사라져서", ["partner busy"] = "상대가 바빠서", ["locked"] = "문이 잠겨 있어서",
            ["item not here"] = "물건이 그 자리에 없어서", ["item gone"] = "물건이 없어져서", ["door fixed"] = "문이 꿈쩍도 하지 않아서", ["door far"] = "문이 너무 멀어서", ["cannot reach partner"] = "상대에게 다가갈 수 없어서",
            ["target gone"] = "상대가 이미 숨져 있어서", ["case opened"] = "수사가 시작돼서", ["phase"] = "수사가 시작돼서", ["deadline"] = "때를 놓쳐서", ["witness"] = "보는 눈이 있어서",
            ["target woke"] = "상대가 깨어나서", ["no weapon source"] = "흉기를 구할 데가 없어서",
        };
        /// <summary>Plan-revision reasons are recorded in short English tags; the reveal reads them as Korean.</summary>
        public static string WhyKor(string why)
        {
            if (string.IsNullOrEmpty(why)) return "뜻대로 되지 않아서";
            var head = why.Split('/')[0].Trim(); if (_why.TryGetValue(head, out var k)) return k;
            if (head.StartsWith("no path")) return "길이 막혀서";
            foreach (var ch in head) if (ch >= 'a' && ch <= 'z') return "뜻대로 되지 않아서";
            return head;
        }
        static string MotiveProse(string m) => m == "wish" ? "소원을 이루려고" : m == "grudge" ? "쌓이고 쌓인 원한 끝에" : m == "fear" ? "입을 막아야 한다는 두려움에" : m == "jealousy" ? "질투에 떠밀려" : "끝내";
        public static string MotiveKor(string m) => m == "wish" ? "소원" : m == "grudge" ? "원한" : m == "fear" ? "두려움(입막음)" : m == "jealousy" ? "질투" : m;
    }
}
