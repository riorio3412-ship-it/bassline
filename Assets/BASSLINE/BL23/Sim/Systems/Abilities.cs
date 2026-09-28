using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    public sealed class AbilityDef
    {
        public string Id; public string Kor; public string God; public string PublicRule; public string Investigate; public string Crime; public string Sign; public string Limit;
        public int Charges; public double Cooldown; public double Duration; public float Range;
    }

    /// <summary>17 NPC abilities (BL23 design defaults), reassigned every loop without repeating the previous owner. Minhyuk's support is fixed (UI tools).</summary>
    public static class Abilities
    {
        public static readonly List<AbilityDef> Catalog = new List<AbilityDef>
        {
            new AbilityDef { Id = "Reverb", Kor = "잔향", God = "오르페", PublicRule = "지금 있는 방에서 최근 어떤 소리가 났는지, 대략 언제였는지 들린다.", Investigate = "현장의 소리 이력 확인", Crime = "—", Sign = "귀를 기울이는 동안 주위 공기가 떨린다", Limit = "누가 낸 소리인지는 알 수 없다", Charges = 3, Cooldown = 60, Range = 0 },
            new AbilityDef { Id = "Afterglow", Kor = "잔광", God = "루미아", PublicRule = "빛을 비추면 표면에 남은 흔적이 잠시 드러난다. 씻어 낸 흔적도 윤곽이 보인다.", Investigate = "세척된 혈흔 확인", Crime = "빛으로 다른 흔적을 가림", Sign = "손끝에서 창백한 빛이 번진다(멀리서도 보임)", Limit = "없던 흔적을 만들어 내지는 못한다", Charges = 2, Cooldown = 90, Range = 6 },
            new AbilityDef { Id = "Silence", Kor = "정적", God = "녹티스", PublicRule = "10분 동안 좁은 범위 안의 소리가 밖으로 거의 새지 않는다.", Investigate = "비밀 대화 보호", Crime = "공격 소리를 감춤", Sign = "주변 소리가 먹먹해진다", Limit = "범위 밖의 소리나 눈으로 직접 보는 것은 막지 못한다. 쓴 자리에 잔향이 남는다", Charges = 1, Cooldown = 240, Duration = 10 },
            new AbilityDef { Id = "Resonance", Kor = "공명", God = "벨루아", PublicRule = "20분 동안 먼 곳의 소리까지 잘 들린다.", Investigate = "멀리서 난 소리 포착", Crime = "엿듣기", Sign = "눈을 감고 벽에 손을 댄다", Limit = "벽 너머에 누가 있는지는 알 수 없다", Charges = 2, Cooldown = 120, Duration = 20 },
            new AbilityDef { Id = "Mark", Kor = "표식", God = "아스테르", PublicRule = "직접 표식을 붙인 물건 하나가 지금 어느 방에 있는지 안다.", Investigate = "흉기 추적", Crime = "상대 소지품 추적", Sign = "물건에 별 모양 빛이 스민다", Limit = "누가 그 물건을 옮겼는지는 알 수 없다", Charges = 2, Cooldown = 60 },
            new AbilityDef { Id = "Seal", Kor = "봉인", God = "테세라", PublicRule = "문이나 물건 하나를 봉인해, 열리거나 훼손되면 알게 된다.", Investigate = "현장 보존", Crime = "접근 지연", Sign = "봉인된 곳에 은빛 실이 보인다", Limit = "누가 열었는지는 알 수 없다", Charges = 2, Cooldown = 60 },
            new AbilityDef { Id = "Weight", Kor = "무게", God = "테르마", PublicRule = "물건 하나의 무게를 10분간 가볍게 한다.", Investigate = "무거운 증거 운반", Crime = "시신 운반", Sign = "물건 주변이 아지랑이처럼 일렁인다", Limit = "손댄 순간과 무게가 돌아온 시각이 기록으로 남는다", Charges = 1, Cooldown = 240, Duration = 10 },
            new AbilityDef { Id = "Fix", Kor = "고정", God = "모르타", PublicRule = "만지고 있는 문 하나를 10분간 움직이지 않게 한다.", Investigate = "현장 봉쇄", Crime = "피해자를 가둠·추격 차단", Sign = "문틀에 서리 같은 무늬가 핀다", Limit = "해제 뒤 문틀에 흔적이 남는다", Charges = 1, Cooldown = 180, Duration = 10 },
            new AbilityDef { Id = "Lure", Kor = "유인등", God = "에레보", PublicRule = "보이는 곳에 약한 빛을 5분간 띄운다. 사람들의 시선이 끌린다.", Investigate = "어두운 곳 비추기", Crime = "시선·동선 유도", Sign = "허공에 떠 있는 작은 등불", Limit = "누구에게나 보인다", Charges = 2, Cooldown = 90, Duration = 5 },
            new AbilityDef { Id = "Guise", Kor = "외피", God = "실레아", PublicRule = "5분간 다른 참가자의 옷차림으로 보이게 한다. 얼굴·목소리·키는 그대로다.", Investigate = "—", Crime = "목격자 속이기", Sign = "옷의 윤곽이 물결처럼 번진다", Limit = "가까이서 얼굴을 보면 알 수 있다", Charges = 1, Cooldown = 300, Duration = 5 },
            new AbilityDef { Id = "Echo", Kor = "메아리", God = "하르파", PublicRule = "자신이 낸 짧은 소리를 기억했다가 나중에 한 번 다시 울리게 한다.", Investigate = "—", Crime = "소리 시각 조작", Sign = "재생 순간 소리에 금속성 여운이 섞인다", Limit = "처음 소리를 낸 시각과 다시 울린 기록이 남는다", Charges = 1, Cooldown = 300 },
            new AbilityDef { Id = "Tripwire", Kor = "경계감", God = "네메아", PublicRule = "가까운 문 하나에 경계를 두고, 누군가 지나가면 그 시각을 안다.", Investigate = "출입 시각 확인", Crime = "피해자 동선 감시", Sign = "문턱에 가는 빛줄기", Limit = "지나간 사람이 누구인지는 알 수 없다", Charges = 2, Cooldown = 60 },
            new AbilityDef { Id = "Preserve", Kor = "흔적 보존", God = "에이온", PublicRule = "지금 있는 방의 흔적이 이번 사건이 끝날 때까지 변하지 않는다.", Investigate = "현장 보존", Crime = "자신에게 유리한 상태 유지", Sign = "방 안의 먼지가 멈춰 떠 있다", Limit = "새 흔적이 생기는 것은 막지 못한다", Charges = 1, Cooldown = 600 },
            new AbilityDef { Id = "Blur", Kor = "흔적 흐림", God = "오블리", PublicRule = "지금 있는 방의 흔적 일부를 알아보기 어렵게 한다. 지우지는 못한다.", Investigate = "—", Crime = "현장 은폐 보조", Sign = "벽지 무늬가 번져 보인다", Limit = "자세히 조사하면 여전히 찾을 수 있다", Charges = 1, Cooldown = 600 },
            new AbilityDef { Id = "Focus", Kor = "순간 집중", God = "라케시", PublicRule = "최근 30분 동안 본 장면을 또렷하게 떠올린다.", Investigate = "목격 신원 확신", Crime = "목격 내용 선택적 공개", Sign = "눈동자가 잠시 금빛으로 멈춘다", Limit = "보지 않은 과거는 떠올릴 수 없다", Charges = 2, Cooldown = 120 },
            new AbilityDef { Id = "Burden", Kor = "부담 분산", God = "메르카", PublicRule = "함께 나르기로 한 사람과 힘을 나눠, 무거운 것도 빨리 옮긴다.", Investigate = "부상자 이송", Crime = "시신 이동", Sign = "두 사람의 그림자가 겹쳐 보인다", Limit = "실제로 함께 옮길 사람이 있어야 한다", Charges = 2, Cooldown = 120 },
            new AbilityDef { Id = "StateSense", Kor = "상태 감응", God = "베리타", PublicRule = "가까이서 살핀 몸이나 물건의 상태를 어림한다(숨진 시각을 더 좁혀 짐작할 수 있다).", Investigate = "사망 시각 추정", Crime = "부상 연기 간파", Sign = "손바닥이 따뜻하게 빛난다", Limit = "왜 죽었는지, 누가 했는지는 알려 주지 않는다", Charges = 3, Cooldown = 30 },
        };
        public const string MinhyukSupport = "추리 도우미 — 본 것 맞대 보기·누구한테 들은 말인지 잇기·시간 순서 정리·집중·근거 묶기 (고정, 신 없음)";

        public static AbilityDef Get(string id) => Catalog.FirstOrDefault(a => a.Id == id);

        public static void AssignLoop(GameState S)
        {
            var rng = S.R(Stream.AbilityAssign);
            var npcs = Cast.Npcs.Select(c => c.Id).OrderBy(x => x).ToList();
            var abil = Catalog.Select(a => a.Id).ToList();
            // constrained shuffle: avoid giving anyone the same ability as last loop (derangement with retries)
            List<string> best = null;
            for (int t = 0; t < 200; t++)
            {
                var perm = abil.ToList(); rng.Shuffle(perm);
                bool ok = true; for (int i = 0; i < npcs.Count; i++) if (S.PrevAbilityOwner.TryGetValue(perm[i], out var prev) && prev == npcs[i]) { ok = false; break; }
                if (ok) { best = perm; break; }
            }
            best = best ?? abil;
            S.Abilities.Clear();
            for (int i = 0; i < npcs.Count; i++)
            {
                var def = Get(best[i]); var a = S.A(npcs[i]);
                S.Abilities.Add(new AbilityAssignment { Actor = npcs[i], Ability = def.Id, Loop = S.Loop, Charges = def.Charges });
                a.Ability = def.Id; a.AbilityCharges = def.Charges; a.AbilityCooldownUntil = 0;
                S.K(npcs[i]).Facts.Add("ability:" + npcs[i] + ":" + def.Id);
                S.PrevAbilityOwner[def.Id] = npcs[i];
            }
            S.Log("AbilityAssign", null, data: string.Join(",", S.Abilities.Select(x => x.Actor + "=" + x.Ability)), secret: true);
        }

        /// <summary>Use an ability. Real effects, costs, cooldowns, visible signs and traces.</summary>
        public static bool Use(Simulation sim, Actor a, string target = null, int door = -1)
        {
            var S = sim.S; var def = Get(a.Ability); if (def == null) return false;
            if (a.AbilityCharges <= 0 || S.Clock < a.AbilityCooldownUntil) return false;
            a.AbilityCharges--; a.AbilityCooldownUntil = S.Clock + def.Cooldown;
            var asg = S.Abilities.FirstOrDefault(x => x.Actor == a.Id); asg?.Uses.Add($"{ClockFmt.DayHM(S.Clock)} {S.RoomName(a.Room)}");
            S.Log("Ability", a.Id, target, room: a.Room, pos: a.Pos, data: def.Id);
            S.Emit(GameEventType.Ability, a.Id, target, text: def.Sign, data: def.Id, pos: a.Pos);
            // visible sign: anyone who currently sees the user notes that a power was used (not necessarily which)
            foreach (var o in S.Actors.Values.Where(o => o.Alive && o != a && o.Room == a.Room && o.Pose != Pose.Sleep)) S.K(o.Id).Facts.Add($"saw-power:{a.Id}:{def.Id}:{(int)S.Clock}");
            var room = S.Layout.Room(a.Room);
            sim.AddTrace("PowerResidue", a.Pos, a.Room, a.Id, null, 0.4f, 3, "권능의 잔향", $"이곳에서 권능이 쓰였다 — 아마 ‘{def.Kor}’", "누가 썼는지");
            var k = S.K(a.Id);
            switch (def.Id)
            {
                case "Reverb":
                    foreach (var e in S.Ledger.Where(e => e.Type == "Sound" && e.Room == a.Room && S.Clock - e.Clock < 240 && e.Data != "Footsteps").Take(8))
                        Evidences.Add(sim, a.Id, EvKind.Ability, $"잔향으로 들은 {Simulation.SoundText((SoundKind)Enum.Parse(typeof(SoundKind), e.Data))} 소리", $"{S.RoomName(a.Room)}에 {ClockFmt.Vague(e.Clock)} 전후로 {Simulation.SoundText((SoundKind)Enum.Parse(typeof(SoundKind), e.Data))} 소리가 남아 있다.", "권능 잔향", "reverb:" + e.Seq, e.Clock - 3, e.Clock + 3, a.Room, "소리의 종류와 대략의 시각", "누가 낸 소리인지", true, new Prop { Kind = PropKind.Heard, A = a.Id, Room = a.Room, T0 = e.Clock - 3, T1 = e.Clock + 3, Value = e.Data });
                    break;
                case "Afterglow":
                    foreach (var t in S.Traces.Where(t => t.Pos.f == a.Pos.f && t.Pos.DistXZ(a.Pos) < def.Range)) { t.Visibility = Math.Min(t.Visibility, 1); if (t.Cleaned) Evidences.Add(sim, a.Id, EvKind.Ability, "잔광으로 드러난 씻긴 흔적", "씻어 낸 자리에 흐릿한 윤곽이 빛난다 — " + t.Desc, "권능 잔광", "afterglow:" + t.Id, t.Clock - 10, t.Clock + 10, t.Room, "여기 흔적이 있었다", "누가 씻었는지", true, new Prop { Kind = PropKind.TraceAt, Room = t.Room, Value = t.Desc + "(세척됨)", T0 = t.Clock - 10, T1 = t.Clock + 10 }); }
                    foreach (var it in S.Items.Values.Where(i => i.Holder == null && i.Washed && i.Pos.DistXZ(a.Pos) < def.Range)) Evidences.Add(sim, a.Id, EvKind.Ability, "잔광으로 드러난 " + it.Kor, "씻긴 표면에 붉은 윤곽이 떠오른다.", "권능 잔광", "afterglow:" + it.Id, S.Clock, S.Clock, it.Room, "피가 묻었다가 씻겼다", "누구의 피인지", true, new Prop { Kind = PropKind.ItemState, Item = it.Type, Value = "피 묻었다 씻김" });
                    break;
                case "Silence": S.Flags["silence:" + a.Room] = S.Clock + def.Duration; break;
                case "Resonance": S.Flags["resonance:" + a.Id] = S.Clock + def.Duration; break;
                case "Mark": { var it = S.I(target); if (it != null) { it.MarkedBy = a.Id; k.Facts.Add("marked:" + it.Id); } break; }
                case "Seal": { if (door >= 0) { var d = S.Layout.Doors[door]; d.SealedBy = a.Id; } else { var it = S.I(target); if (it != null) it.SealedBy = a.Id; } break; }
                case "Weight": { var it = S.I(target); if (it != null) { it.WeightClassOverride = 1; it.WeightUntil = (long)(S.Clock + def.Duration); } else S.Flags["weight:" + a.Id] = S.Clock + def.Duration; break; }
                case "Fix": { if (door >= 0) { var d = S.Layout.Doors[door]; d.FixedByAbility = true; d.FixedUntilTick = S.Tick + (long)(def.Duration / Math.Max(0.01, S.ClockRate) * SimTime.PerSecond); } break; }
                case "Lure": S.Flags["lure:" + a.Room] = S.Clock + def.Duration; break;
                case "Guise": { if (target != null) { a.BorrowedOutfitOf = target; S.Flags["guise:" + a.Id] = S.Clock + def.Duration; S.Emit(GameEventType.Disguise, a.Id, target, data: "guise"); } break; }
                case "Echo": S.Flags["echo:" + a.Id] = S.Clock; break;
                case "Tripwire": if (door >= 0) S.Flags[$"trip:{door}:{a.Id}"] = S.Clock; break;
                case "Preserve": foreach (var t in S.Traces.Where(t => t.Room == a.Room)) t.Preserved = true; break;
                case "Blur": foreach (var t in S.Traces.Where(t => t.Room == a.Room && !t.Preserved)) { t.Blurred = true; t.Visibility = Math.Min(3, t.Visibility + 1); } break;
                case "Focus": foreach (var s in k.Sightings.Where(s => S.Clock - s.T1 < 30)) s.IdConf = Math.Min(1, s.IdConf * 1.4f + 0.1f); break;
                case "Burden": S.Flags["burden:" + a.Id] = S.Clock + 20; break;
                case "StateSense": break; // used passively by body examination while charges remain
            }
            return true;
        }

        /// <summary>NPC investigators use their ability when it helps (only its real, public effect).</summary>
        public static void NpcInvestigateUse(Simulation sim, Actor a)
        {
            var S = sim.S; if (a.Ability == null || a.AbilityCharges <= 0) return;
            var def = Get(a.Ability);
            bool atScene = S.Incidents.Values.Any(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Confirmed && i.FoundRoom == a.Room);
            if (atScene && (def.Id == "Reverb" || def.Id == "Afterglow" || def.Id == "Preserve")) Use(sim, a);
            if (def.Id == "Focus") Use(sim, a);
        }
    }
}
