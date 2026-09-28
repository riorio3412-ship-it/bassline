# BL23 살인 시뮬레이션 — 현재 구조 인수인계 (MurderArchitecture_Current)

작성: 2026-09-27 · 살인 시뮬레이션 트랙이 "murder-foundation" 재구축 워크플로에 넘기는 문서.
기준 트리: 이 문서를 쓴 시점의 `Assets/BASSLINE/BL23/Sim`. 줄 번호는 이 시점 기준이며, 파일이 바뀌면 함수 이름으로 찾는다.
짝 문서: `GapReport_Mystery.md`(기획 대비 격차표), `HANDOFF.md`(프로젝트 전체).

> 재구축의 회귀 그물은 **강제 증명 세트**다(§6): `dotnet bin/Release/net10.0/SimTests.dll mystery forced 8` → `forced ok=19/19 faults=0`.
> 각 종류는 계획에 **적힌 것**이 아니라 원장(ledger)에 그 종류의 **서명 이벤트가 실제로 찍혀야** 통과한다.

---

## 0. 한 장 요약

```
Crime.Tick ─(25~45분마다 NPC별)→ Evaluate ─(압력·대안 행동·예산)→ BuildPlan
   BuildPlan: Option 점수표(Crime 105–117 + Methods.Options) → 무작위 흔들기 → 다양성 감점 → 1등 문법
              → 흉기 선택 → 문법별 단계(switch) → 은폐 단계(LockRoom/MoveBody/Hide|Wash/CleanUp/Alibi)
              → Tricks.Augment → SetPieces.Augment → Methods.Augment   (층 = plan.Grammar에 "+X"로 붙음)
Crime.Think(매 판단) : PlanStep → Activity(ActionStep 목록)   · "X_" 단계는 Tricks.PlanThink → Methods / SetPieces
Movement(매 틱)      : ActionStep 실행 → Crime.Exec → (X_) Grammars.Exec → Tricks.Exec → Methods.Exec
결과                 : Combat.Strike/Die → Wound·Trace·Ledger → Cases(사건) → Evidences(카드·Prop) → Logic(재판 판정)
재판                 : TrialSystem(주장·첫인상·재구성) ← TrialTricks/TrialMethods(위장 첫인상) ← Logic.CheckRaw/Staged
전말                 : Replay.BuildSegments/Script ← Methods.Caption/GrammarKor/Verb
```

모든 난수는 `S.R(Stream.X)` 시드 스트림, 모든 열거는 Id 정렬(`StringComparer.Ordinal`) — 세이브 왕복 IDENTICAL의 전제.

---

## 1. 계획 파이프라인

### 1-1. 계획이 생기기까지 — `Sim/Systems/Crime.cs`
| 단계 | 위치 | 내용 |
|---|---|---|
| 주기 | `Crime.Tick` 약 15–30 | 챕터 시작 후 30분(1루프 1챕터)·20분 평온기. NPC마다 `motive:<id>` 플래그로 25–45분 간격 평가. 계획 보유자는 `Review`(감시견: 막힘·대기) |
| 압력 | `Evaluate` 35 | `Relations.Pressure` → (p, target, motive). p<0.1 무시 |
| 대안 | 44–56, `Alternative` 71 | p<0.35 또는 wish 아닌 첫 시도 → 대화·상담·거리두기(실제 행동). 실패 누적 `altfail:` |
| 예산 | 57–59 | `S.Ch.VictimCap`(예약+사망), 동시 계획 2개, 생존자 하한 |
| 수립 | `BuildPlan` 89 | 아래 1-2 |
| 등록 | 61–65 | `PlanFormed` 원장, `Replay.MarkPlanStart` |

### 1-2. `BuildPlan` (Crime.cs 89–240)
1. **Option 점수표** 105–117: Ambush / Lure / NightVisit / Blackout / Press / Drown / Disguise / Trap(`Tricks.ChooseTrap`) / Gathering / Poison(`SetPieces.PoisonSource`) + `Methods.Options`(Strangle/Push/Shock/Smother/Bedtime, Methods.cs 89).
2. 흔들기 +0~0.35(118), **다양성 감점** −0.22×(이번 루프 같은 문법 수)(120), **무기 사건 뒤 감점** −0.15×(이번 루프 칼·둔기 사망 수, 대면 문법만)(122–123), 영리한 계획자 Trap/Gathering +0.3.
3. **강제 보정**(테스트 전용) 126–128: Force=Noise → Lure +5, Burn/Dump/Bury/ColdHide → 흉기 문법 +5.
4. **흉기 선택** 136–141: `Methods.WeaponPref`(인물 친화 + 흔들기 0.6)·Sev·거리. 끈은 Aggression<0.5인 사람만. Press는 둔기 강제. Trap/Poison/Drown/NoWeapon 문법은 흉기 없음(143).
5. **문법별 단계** switch 150–211. 기본(Ambush/Disguise): Stalk alone → Attack(끈이면 Note "strangle").
6. **은폐** 213–227: careful(Infer+Composure>150)이면 LockRoom 35%+, MoveBody 25%. `quick`(Gathering/Trap/Poison/원격)·`staged`(사고·자연사 위장)는 제외. 그 뒤 Hide|Wash, CleanUp, Alibi.
7. **층 덧붙이기** 228–230: `Tricks.Augment`(Tricks.cs 99) → `SetPieces.Augment`(SetPieces.cs 20) → `Methods.Augment`(Methods.cs 196).

### 1-3. 층(layer) 목록과 붙는 곳
| 층 | 붙이는 곳 | 조건 요지 | 삽입 단계 |
|---|---|---|---|
| Recorder | Tricks.Augment ~105 | 녹음기 앎, Deceit | X_Record(자기 방), Alibi→X_ReturnRoom |
| Silence/Guise/Blur/Fix/Echo | Tricks.Augment ~121–139 | 이번 루프 보유 권능 | X_Power(Note), X_EchoRecord |
| Courier(IG10) | Tricks.Augment ~141–148 | Lure의 Invite를 제3자에게 | X_Courier |
| Mutilate | Tricks.Augment 150 | 원한·질투+낮은 Morality 40% 또는 고Deceit 20%, Staged 아님 | X_Mutilate → X_Deface |
| Seal/Tod/Message/Swap | SetPieces.Augment 20–73 | 능력치 문턱, 1~2개, Seal↔Tod 배타 | X_Seal/X_Tod/X_Message/X_Swap |
| KeySlide | Methods.Augment ~203 | NightVisit/Smother, Infer≥65·Composure≥60 50% | X_KeySlide |
| FakeNote | ~210 | Smother/Bedtime, Deceit≥65·Morality<0.6 | X_FakeNote 또는 X_PlantPoison Note "\|note" |
| Noise | ~217 | Lure + 보일러실/기계실 앎 | Lure 장소 교체 + X_Noise |
| Burn/Dump/Bury | ~229 | Hide/Wash 단계를 **대체**(55%) | X_Burn/X_DumpWater/X_Bury (Burn은 CleanUp 제거) |
| Dismember | `AugmentDismember`(MethodsDismember.cs 43), Methods.cs 249에서 호출 | Morality<0.3·Composure≥70 8%+친화, 톱 있는 작업 방 앎, Seal/KeySlide/Tod/Message/Swap 없음 | MoveBody(Note "work") → X_Dismember → X_ScatterParts (+CleanUp) |
| ColdHide | ~251 | Composure≥70 20%+친화, Dismember 아님 | MoveBody Note "cold" |

### 1-4. 실행 — `Crime.Think` 257 / `Crime.Exec` 518
- `Think`: 단계 → Activity. 치명 단계 전이면 대상 사망·데드라인·페이즈 검사(265–279). "X_" 단계는 `Tricks.PlanThink`(Tricks.cs 154) → 처리 못 하면 `Methods.PlanThink`(MethodsActs.cs 18, 해체는 `DismemberThink`) → `SetPieces.PlanThink`(SetPieces.cs 75).
- `Exec`(518): Plan* 스텝(PlanPick, PlanGrab/PlanRelease 624–646, PlanStalk→`Stalk` 735, Attack→`Attack` 798 …). `X_`는 `Grammars.Exec`(Grammars.cs 686) → `Tricks.Exec`(Tricks.cs 262) → `Methods.Exec`(MethodsActs.cs 141, 해체는 `DismemberExec`).
- 진행: `Advance` 437(치명 단계 뒤 Stage=Concealing), `Replan` 454(Tries>4 → Abort, 무기 재선택), `Abort` 470, `Lethal` 451, `Reserve` 785(피해 예산 예약 — Stalk 준비 완료 시점).
- `OnStepFailed` 939: 인터럽트 = Replan. **예외**: Stalk Note "edge"/"asleep"은 인내 대기라 Tries를 쓰지 않음(데드라인이 끝냄).
- `Attack` 798: 계획된 흉기 우선(803–804) → 목격자 있으면 보류 → `Methods.IsMode(tag)`면 `Methods.Attack`(MethodsActs.cs 350) → 일반 타격(명중·부위 `PickRegion`·KO는 끈이면 목) → `Combat.Strike`.
- `Stalk` 735: goal(보임/edgeWait/잠든 방/마지막 목격/습관 방) → ready(tag별: reach, alone-dark, edge=`AtEdge`+`EdgeClear`, asleep=`Dozing`) → Reserve → 다음 단계. 90분 무소득 → Tries.

### 1-5. Force(테스트 스위치)
- `SetPieces.Force`(SetPieces.cs 15, static string). 설정되면 `Methods.Forced`(Methods.cs 36)가 참 → **계획자의 방·물건 지식 제한이 풀린다**(`Knows`, `KnownItems`). 각 층의 확률 대신 점수 9.
- 값: Strangle, Push, Shock, Smother, Bedtime, FakeNote, KeySlide, Burn, Dump, Bury, Noise, ColdHide, Dismember, Drown, Poison, Seal, Tod, Message, Swap.
- 진입: SimTests `campaign <seed> <days> trick <Kind>` / `mystery forced <days> [Kinds]`. Unity 프로브는 `BL23.Sim.SetPieces.Force = "Dismember";`를 `Simulation.NewCampaign` 전에.

---

## 2. 방법(실행 묶음)별 명세

표기: **진입** = 계획/실행 함수, **데이터** = 플래그·아이템·함정, **원장** = Ledger 이벤트(재구성·전말의 근거), **단서** = 조사 Prop 값(Evidences 카드로 감), **재판** = Logic/TrialSystem 훅.

### 2-1. 대면 흉기 공격 (Ambush / Lure / NightVisit / Blackout / Disguise / Gathering)
- 진입: Crime.BuildPlan switch 150–211, `Stalk`, `Attack` 798. Gathering은 `Tricks.FillGathering`(Tricks.cs 87)·X_SlipOut/X_Rejoin.
- 데이터: `attackstart:<a>:<t>`, `blows:`, `strike:`; 무기 Item(Bloody/Washed/Hidden).
- 원장: AttackBegin, Strike(부위/종류/강도), Death, CarryStart/End, PickUp, HideItem.
- 단서: 상처(ExamineBody), BloodDrip/Scuff 흔적(Combat.AddTrace 219, 같은 자리 병합), 피 묻은 옷(BloodOnClothes).
- 약점: 흉기 선택이 "보이는 흉기 중 최선"이라 사람마다 비슷함 → `WeaponPref` 친화표(Methods.cs 43–61)로 보정. 대면 문법의 첫 판정이 목격자 유무 하나라서 긴장감 연출이 얕다.

### 2-2. 교살 Strangle (+ 끈으로 하는 모든 공격)
- 진입: `Methods.Options` 89, `Fill` 137(GetWeapon→Stalk alone→Attack Note strangle), `Methods.Attack` "strangle"(MethodsActs.cs ~386).
- 데이터: 끈 Item Surface "stretched"; `nails:<victim>`, `scratched:<c>:<v>`, `scratch:<c>`(범인 손등 상처, 20시간 보임).
- 원장: Garrote, Scratched(Wound 이벤트), Strike(Neck/Choke).
- 단서: ligature(시신), nail-scrape(시신·기술 0.45+), cord-stretched(끈), Injured 관찰 카드(Methods.Tick 609: 2.6 m 안에서 본 사람).
- 재판: Logic.Staged 152 — ligature/nail-scrape는 모든 "사고·자연사·자살"에 **모순**.

### 2-3. 추락 Push (사고 위장)
- 진입: Options/Fill(Stalk Note "edge", 데드라인 30시간), `AtEdge`(Methods.cs 282: 계단 맨 윗단 <2 m, 빈 공간 난간 <1.3 m), `EdgeClear` 301(같은 층 10 m 안에 깨어 있는 사람 없음), `EdgeWait` 대기 지점, `Methods.Attack` "push", `Fall`(MethodsActs.cs 461).
- 데이터: `pushed:<v>`; ButtonTorn Item(Owner=범인, Surface "torn", 85%/55%); Scuff 흔적 Note "edge=stair|rail".
- 원장: Shove(Data "rail|아래층" 등), ButtonTorn, ShoveFailed(실패 → 피해자가 범인을 앎, `attacked-by:` 사실, 계획 Abort "들킴").
- 단서: push-bruise(모순), fall-injuries, edge-scuff(제한), torn-button(제한 + 범인 지목 지지).
- 재판: 첫인상 `ImpressionAccident`(TrialMethods.cs 35) "accident:fall".
- 약점: 준비 조건이 드물어 비강제 실행률이 낮았음(인내 대기 예외 뒤 8시드 기준 4~5건). 게임층에 `Anim.Shove` 매핑 없음(Struggle로 대체).

### 2-4. 감전 함정 Shock (조건 충족형·원격)
- 진입: `ChooseShock`(Methods.cs 339: Infer≥70 또는 친화, 자른 도구, 대상이 2회 이상 목격된 방의 기계 Machines 334), `Tricks.FillTrap`, Tricks.PlanThink X_ArmTrap(Tricks.cs 159, **야간 잠금 방이면 아침까지 대기**), `Methods.ArmTrap` 495, `TrapCheck` 518(같은 방 같은 종류 기계 어느 자리든), `FireShock` 544, `Disarm` 595.
- 데이터: `Trap{Kind="Shock"}`(S.Traps), 가구 Marks, 물웅덩이 Water 흔적(vis0), 도구 Surface "shavings", `breaker:<circuit>`·`breakerreset:`.
- 원장: ShockRigged, TrapFired, ShockFired, BreakerTrip.
- 단서: electrocuted(시신), scorch, cable-stripped + TrapSet(가구·**모순**), insulation-shavings(도구·제한), 차단기 기록(Switchboard, LightsChanged "trip:").
- 재판: "accident:shock". 엉뚱한 사람이 걸리면 Incident Method "Direct"(계획 id 없음) — 재구성 트릭은 원장 기준이라 Accident로 맞음.
- 약점: 발견 확률 0.15·1.4 m(Tricks.TrapDiscovery 468)는 손으로 맞춘 값. 비강제 실행률 낮음.

### 2-5. 수면제 + 베개 Smother (자연사 위장)
- 진입: Fill(GetItem 수면제 → X_Sedate → X_WaitDrowsy → Stalk asleep → Attack smother), `SedateThink` 114(식사·차 자리에서 잔), X_MSedate 147, Methods.Tick 졸음 처리 → X_MDoze 170(자기 침대, Unconscious 140–170분), `Dozing` 138, Attack "smother"(Pillow Item 생성 Surface "pressed", Head/Choke 4).
- 데이터: `sedate:`, `sedated:`, `drowsy:`, `pillow:`; 잔 Surface "residue:sedative"; 수면제 "used"/"empty".
- 원장: Sedate(+SawNearCup 목격), Drowsy, Doze, Smother.
- 단서: smother-marks(모순), sedated, pillow-pressed(모순), sedative-residue/-used(제한).
- 재판: `ImpressionNatural` 58 "natural"(외상 없음 + 자기 침실에서 발견).
- 주의: 의식 잃은 피해자를 범인이 "발견"하던 문제 → Perception.cs 145–150에서 계획 중인 가해자는 자기가 쓰러뜨린 사람을 발견하지 않게 막음.

### 2-6. 시간차 독 Bedtime (장기 지연형)
- 진입: `BedtimeItem` 364(피해자 방 개인 음료·보온병, 없으면 선물 과자/차), X_PlantPoison(낮 8:00–20:30, 주인 부재·문 열림·빈 방), X_MPlant 182(Trap Kind "Bedtime"), TrapCheck → `FireBedtime` 567(잠들 때 섭취, 35–60분 뒤 사망), X_WaitBedtime(데드라인 40시간).
- 데이터: Item Surface "poisoned", ArmedBy; PowderSpill 흔적; `sealed:`(스스로 잠근 방).
- 원장: PoisonPlant, PoisonTaken, TrapFired.
- 단서: poisoned, powder, poisoned-personal(모순: 자연사 / 밀실).
- 재판: DoorLocked 밀실 주장을 poisoned-personal이 **모순**(Logic 61–71), natural은 poisoned가 모순.

### 2-7. 가짜 유서 FakeNote · 열쇠 되돌린 밀실 KeySlide
- FakeNote: `FakeNoteItem`(MethodsActs.cs 339) Document, NoteFrom "forged:<culprit>:<victim>", 빈 수면제 봉지. 단서 farewell-note, handwriting-mismatch(플레이어·Obs≥80·피해자 수첩 조사자만). 재판 `ImpressionSuicide` 88 "suicide" → 필체 불일치 모순.
- KeySlide: X_MKeySlide 214 — 밖에서 잠그고 열쇠를 문틈으로 밀어 넣음(Item Surface "slid"), KeySlide 흔적, `sealed:`/`sealedat:`. 단서 key-slid(모순), key-on-floor(제한). SetPieces.OnConfirm 345가 "방 안, 문 바로 앞 바닥" 세부를 공식 기록에 남김.

### 2-8. 익사 Drown (수영장)
- 진입: Crime.BuildPlan Option 110·switch "Drown", Attack mode "drown" → `Methods.OnDrown`(MethodsActs.cs 672).
- 데이터: `drownclothed:<v>`(수영 중이 아니면 1), `wetsleeve:`/`wettrail:`(범인), 젖은 발자국 두 곳(Trace Note "drown").
- 원장: HeldUnder.
- 단서: clothed-drowning(모순), held-under(모순), wet-trail(**accident:drown일 때만** 제한), Wet 관찰 카드(4 m, 40분).

### 2-9. 독(식사) Poison · 프레스 Press · 가구 전도/계단 줄 Trap (기존)
- Poison: `SetPieces.PoisonSource` 268(Tag "poison" 아이템, 거리→Id 정렬), `FillPoison` 278, `DoseThink/DoseExec` 291/317. 원장 Dose. 단서 poisoned, poison-residue.
- Press: KnockOut → CarryTo(pressbed) → ArmPress. 원장 PressArmed. 원격 사망.
- Trap(Topple/Tripwire): `ChooseTrap` 24, `FillTrap` 69(Furniture는 Tripwire면 계단 id), `TrapCheck/Fire` 412/432, `TrapDiscovery` 468, `DisarmActivity/Disarm` 490/500. 원장 TrapArmed/TrapFired.

### 2-10. 은폐 층 — 방이 돕는 살인
| 층 | 실행 | 데이터·흔적 | 원장 | 단서 |
|---|---|---|---|---|
| Burn(소각실) | X_Burn 71 / X_MBurn 250, `Burn()` 329 | 흉기 Surface "burnt"(금속 등은 남음), 피 묻은 옷 → 타다 남은 Button(Owner=범인, Hidden), `furnace:<f>`, Ash 흔적 | Burn | ash-fresh, burnt, burnt-remnant(범인 지지) · 소각로 FurnitureNotes 152가 가동 시각과 재 속 물건을 드러냄 |
| Dump(수영장) | X_DumpWater 81 / X_MDump 276 | 흉기 → 수질관리실 여과기, "waterlogged" | DumpWater | waterlogged |
| Bury(온실) | X_Bury 90 / X_MBury 295 | 화분대 Marks "dug", Soil 흔적, 흉기 "soil" | Bury | soil-dug, buried |
| Noise(보일러) | X_Noise 99 / X_MNoise 310 | `silence:<room>` 30분, 다이얼 표시 | NoiseMask | 보일러 최대 출력(MachineUsed) |
| ColdHide(저온 보관실) | MoveBody Note "cold" → `ColdDump` 329, PlanRelease → `OnColdHide` 686 | TodShift −70~−120분, `frost:`, Water 흔적 | ColdHide | temp-cold(DeathWindow 모순), cold-water |
| **Dismember**(작업 방) | 아래 2-11 | | Dismember, PartHidden, PieceSeen | postmortem-cut 외 |

### 2-11. 사후 해체 Dismember (신규, 인수인계 중인 어댑터)
- 파일: `Sim/Systems/MethodsDismember.cs`.
- 계획: `WorkRoom` 26(ColdStorage 0.5 > Workshop 0.4 > Kitchen 0.3 = Incinerator > MachineRoom 0.2 > Laundry, 그 방에 톱/고기 칼이 있어야 함), `AugmentDismember` 43. 친화 P10 요리사 0.3, P14 장의사 0.3, P06 유통업 0.2, P18 0.1.
- 실행: `DismemberThink` 95 — 시신이 작업 방에 없으면 층 건너뜀, 방에 깨어 있는 사람이 있으면 최대 60분 대기(`dismwait:`). `DismemberExec` 134 — X_MSawPick → X_MDismember(20분, 누가 들어오면 중단) → `CutUp` → 톱 헹궈 제자리(Surface "bonedust"), DrainBlood 흔적(Note "dismember"), BloodOnClothes 0.9 → X_MPartPick ×n → X_MHidePart(burn/water/soil/box).
- **어댑터** `CutUp` 230(주석 블록 219–229): 현재는 `SeveredPart` Item 2–4개(Owner=피해자, Note=ArmL/ArmR/LegL/LegR, Surface blood/sawn) + 시신에 Postmortem Cut 상처 직접 추가. **Gore.Dismember가 들어오면 이 함수만 바꾼다.** 몸통 = 피해자 Actor(작업 방에 남음).
- 발견: `OnPieceSeen` 257 — Perception 아이템 훑기(Perception.cs 95–107)에서 Tag "part"를 보면 호출 → `Cases.OnBodySeen` 공식 경로 → 첫 발견이면 FoundRoom을 조각 위치로, 카드 문구를 "피가 밴 천 꾸러미"로 교정.
- 플래그: `dismembered:<v>`, `sawroom:<v>`, `pieceseen:<o>:<item>`.
- 단서: postmortem-cut(시신), saw-marks(시신, 기술 0.4+), drain-blood(작업 방), bone-dust(톱), part-hidden / burnt-bone(조각, A=피해자). 상자 속 조각은 플레이어의 "뒤지기"(PlayerActivities search)로만 드러남.
- 재판: `ImpressionDismember`(TrialMethods.cs 74) "dismember:alive"("산 채로 톱에 잘렸다") → Logic.Staged 152–159: postmortem-cut 또는 실제 치명상(instant-death/ligature/smother-marks/poisoned) → **모순**, drain-blood → 제한. 재구성 "옮겨졌는가"는 해체면 "옮겨졌다"(TrialSystem 481), 공개 단서 적합성(TrialGames 873), 전말 문장(TrialGames 935).
- 게임층: 임시 메시 "천에 싼 꾸러미"(Game/World/ItemView.cs `MurderProps` "SeveredPart"). 몸통의 팔다리 없는 렌더링은 Gore 렌더러 몫.

---

## 3. 흔적·단서 계층 (적게, 읽히게)

- 시신: `Evidences.ExamineBody` → `SetPieces.BodyNotes`(SetPieces.cs 375) → `Methods.BodyNotes`(MethodsClues.cs 63). Prop Kind TraceAt, A=피해자.
- 흔적: `Evidences.ExamineTrace` → SetPieces.TraceNotes 411 → Methods.TraceNotes 96(Trace.Type별).
- 물건: `Evidences.ExamineItem` → `Tricks.ExamineItem`(Tricks.cs 567) → SetPieces.ItemNotes 427 → Methods.ItemNotes 115. Prop Kind ItemState.
- 가구: `Grammars.ExamineFurniture` 807 → Tricks.ExamineFurniture 552 → SetPieces.FurnitureNotes 436 → Methods.FurnitureNotes 152.
- 방 훑기: `Evidences.ExamineRoomQuick` → Tricks.RoomQuick 606 → Methods.RoomQuick 187(수질관리실·수영장 waterlogged, Soil 흔적 방, 장치, 소각실).
- 관찰 카드: Methods.Tick 609 — 긁힌 손(2.6 m, 20시간), 젖은 소매(4 m, 40분).
- 원칙과 실제 수치: 방법마다 핵심 단서 2~4개. 8시드 무강제 기준 **사건당 핵심 단서 평균 2.2, 최대 9**. 흔적 병합(Combat.AddTrace 222: BloodDrip/FootprintWet/FootprintBlood/Scuff는 한 자리 하나), 비출혈 사인(Choke/Drown/Shock/Burn)엔 혈흔 없음(Combat 98, 107, 174).
- 수영객 발자국은 단서가 아님: FootprintWet는 Note "drown"일 때만 wet-trail(MethodsClues.cs 107).

---

## 4. 재판 연결

| 기능 | 위치 |
|---|---|
| 판정 엔진 | `Logic.CheckRaw` 24(주장 Kind별 switch: AtPlace/WithPerson 32, AliveAt 57, DoorLocked 61, TraceAt 72, DeathWindow 84, WeaponType 92, DeathPlace 98, DoorState 103, Culprit 107, Held 131, Heard 135) |
| 위장 첫인상 판정 | `Logic.Staged` 144 — Culprit 주장 중 A=null·Value 있음(110): accident:* / natural / suicide / dismember:alive |
| 첫인상 생성 | TrialSystem 166("cause")·204("place") → `ImpressionMethods`(TrialMethods.cs 18) · 기존 4종은 TrialTricks.cs(ImpressionSwap 17, Tod 31, Seal 46, Message 59) |
| 재구성 | `BuildReconstruction`(TrialSystem.cs 471): who / weapon(+Fall·Shock·None) / where / moved / conceal(+소각·물·흙, `MethodConcealAnswer` 485) / when / trick(10지선 499–500) |
| 트릭 정답 | `TrickOf` 503 → `ExecutedTrick`(TrialMethods.cs 103): **원장 기준**(실행 안 된 층은 정답이 아님) |
| 공개 적합성 | `TrialGames.PublicFitOf` 862(+MethodConcealFit 895, MethodTrickFits 908), 새겨진 물음 TrickNames 689 + `MethodQuestion` 706 |
| 전말 서술 | `TrialGames.Narrate` 926 |
| 헤드리스 재판 | `TrialSystem.RunHeadless` 708(smart player가 열린 주장을 보유 증거로 반박) |

게임층 주의: `Game/Trial/ClockReconstruct.cs` 57·277은 "moved"를 **where 선택으로 0/1 자동 계산**한다. 선택지를 늘리려면 게임층도 함께 바꿔야 한다(그래서 해체는 "옮겨졌다"로 흡수함).

---

## 5. 전말(Replay)·동작

- `Replay.BuildSegments` 49: 구간 = 계획 시작~발견+60틱. 이벤트는 범인·피해자·중개자 관련 + 전역(Circuit/PressArmed/Death/BodySeen) + **행위자 없는 Strike만**(65, 다른 사건의 타격이 섞이던 문제 수정).
- `Replay.Script` 74, 기본 분기 128 → `Methods.Caption`(MethodsClues.cs 212, 신규 이벤트 26종). 계획 문장: `One` 155 → `GrammarKor` 246, `Verb` 196 → `Methods.Verb`.
- 동작표: `Movement.StepMotion` 106–112(신규 X_M* 13종). 교살·질식=Strangle, 밀기=Struggle, 해체=Slash, 조각 숨김=Hide.

---

## 6. 테스트 하네스 (Tests/BL23/SimTests)

빌드: `cd Tests/BL23/SimTests && dotnet build -c Release` · 실행: `dotnet bin/Release/net10.0/SimTests.dll <mode>` (백그라운드 실행 중엔 bin을 복사해 따로 돌릴 것 — DLL 잠금).

| 모드 | 파일 | 하는 일 | 합격 기준 |
|---|---|---|---|
| `campaign <seed> <days> [trick <Kind>]` | Program.cs / Extra.cs | 캠페인 + 세이브 JSON·파일 왕복 | `faults=0`, `roundtrip=IDENTICAL` |
| `layout <N>` | Program.cs | N시드 배치 검증(신규 방 3종 포함, 아이템 안착 로그) | `done fails=0` |
| `mystery <seeds,csv> <days>` | Mystery.cs | 무강제 다양성 통계(계획·실행 문법, 흉기, 사인, 층, 발견 방, 첫인상 주장·반박, 사건당 단서 수) + 사건별 CASE 줄·계획·전말 | 모든 시드 `faults=0 roundtrip=IDENTICAL` |
| `mystery forced <days> [Kinds]` | Mystery.cs 40–70, `Sig` 19, `Proves` 20 | 종류마다 시드 6개(20260926, 777, 1234, 90210, 4242, 31337)를 차례로 강제해 **서명 이벤트**가 원장에 찍힌 첫 사례를 증명으로 출력 | `forced ok=19/19 faults=0` |

- 하네스 조사자(`Investigate`, Mystery.cs ~137): 시신·사건 방 흔적·방 훑기·주방·전력실·수질관리실·온실·저온 보관실·소각실·수영장, 창고류 상자 뒤지기. 조사 시작 시 1회 + 45분 뒤 1회(늦은 처분 흔적용).
- 서명 이벤트(`Sig`): Strangle=Garrote, Push=Shove, Shock=ShockFired, Smother=Smother, Bedtime=PoisonTaken, FakeNote=FakeNote, KeySlide=KeySlide, Burn=Burn, Dump=DumpWater, Bury=Bury, Noise=NoiseMask, ColdHide=ColdHide, Dismember=Dismember, Drown=HeldUnder, Seal=SealedRoom, Tod=TodShift, Message=FakeMessage, Swap=PlantWeapon, Poison=Dose.
- 게임층 컴파일: `cd Tests/BL23/GameCompile && dotnet build -c Release` → "오류 0개".

### 마지막 측정 (이 문서 작성 시점, 최종 바이너리)
- 캠페인 20260926 / 777 / 1234 / 90210 (6일): 모두 faults=0, roundtrip=IDENTICAL.
- layout 30: fails=0.
- forced: 19/19, faults=0.
- 해체 강제 캠페인 777 / 1234 / 90210 / 4242 (5일): 모두 실행됨, faults=0, IDENTICAL.
- 무강제 8시드×8일: 사건 36, 재판 34, 정답 17, faults=0. 실행 문법 10종, 흉기 17종, 사인 7종, 층 13종, 발견 방 15종, 첫인상 주장 7종이 모두 반박·제한됨. 사건당 핵심 단서 평균 2.2(최대 9).
- GameCompile: 이 트랙 파일은 오류 없음. 마지막 확인 때 남은 오류 7개는 모두 `Game/Core/Session.cs`(다른 에이전트가 편집 중, MenuUI.ConfirmOnly/BacklogUI/CaseReport 미정의).

---

## 7. 알려진 약점·특수 처리 (재구축 때 버릴 것 목록)

1. **디스패치가 사슬이다**: Crime.Exec → Grammars.Exec → Tricks.Exec → Methods.Exec, PlanThink도 Tricks → Methods → SetPieces. 새 종류마다 `Handles` 목록·`X_` 접두사·StepMotion 표·Caption 분기에 이름을 흩어 등록해야 한다. 하나라도 빠지면 조용히 Advance된다.
2. **문자열 계약**: `plan.Grammar`는 "Head+Layer+…" 문자열이고, 판정·제외 규칙이 `StartsWith`·`Contains`로 흩어져 있다(Methods.Staged/Remote/NoWeapon, SetPieces.Augment 25, Tricks 150 등). 층끼리의 배타 관계(Seal↔Tod, Seal/KeySlide/Tod/Message/Swap↔Dismember/ColdHide)도 각 Augment 안에 하드코딩돼 있다.
3. **층 순서 의존**: Tricks → SetPieces → Methods 순서라서, 뒤 층이 앞 층의 단계를 지우거나(ColdHide/Dismember가 LockRoom·MoveBody 제거) 끼워 넣을 자리를 `FindIndex("Attack")`로 다시 찾는다.
4. **단서 사슬도 네 겹**(Evidences → Tricks → SetPieces → Methods). 같은 물건에 여러 줄이 붙을 수 있다(예: 조각이 "burnt" 일반 줄 + "burnt-bone" 전용 줄).
5. **Prop 값이 곧 규칙 키**: "postmortem-cut", "cable-stripped" 같은 문자열이 MethodsClues(생성)·Logic(판정)·TrialGames(적합성)·하네스(KeyProps)에 각각 복사돼 있다.
6. **강제 스위치가 지식을 푼다**: Force가 켜지면 계획자가 모르는 방·물건도 쓴다. 강제 증명은 "가능하다"만 보여 줄 뿐 "자연스럽게 일어난다"는 보여 주지 않는다(무강제 통계와 함께 볼 것).
7. **확률 상수가 손맛 값**: 층 확률(0.08~0.55), 함정 발견 0.15, 인내 대기 60분, 해체 20분 등. 인물 친화표 `_aff`(Methods.cs 43)도 손으로 쓴 사전이다.
8. **비강제 실행률 편차**: Push·Shock·Bedtime·Dismember는 형성은 되지만 실행까지 가는 경우가 드물다. Shock은 야간 잠금 방 대기로 개선됐으나 여전히 낮다.
9. **엉뚱한 피해자**: 함정·독이 다른 사람을 죽이면 Incident에 계획 id가 없어 "Direct/Accident"로 기록된다. 재구성은 원장으로 맞추지만 통계·자막은 어색하다.
10. **시신 발견 = 한 Actor**: 해체 조각은 Item이고 몸통이 Actor라, 발견 흐름(Cases.OnBodySeen)은 몸통 위치를 기준으로 카드를 만든다 → OnPieceSeen이 카드를 사후 교정한다. Gore 모델이 들어오면 발견 단위를 "시신 부분"으로 일반화해야 한다.
11. **게임층 매핑 공백**: Anim.Shove 없음, 감전 스파크·굉음 전용 효과음 없음(가구 이벤트 sparks/stoked만 발행), 해체된 몸통 렌더링 없음.
12. **재구성 선택지는 게임층과 묶여 있다**: ClockReconstruct가 "moved"를 자동 계산하는 등 커널 선택지 수를 바꾸면 UI가 깨질 수 있다.

---

## 8. 다시 설계한다면 (권고)

1. **방법 = 데이터 정의 + 작은 실행기.** 방법·층마다 `MethodDef { Id, 조건(능력치·지식·방·물건), 단계 템플릿, 배타/필수 태그, 서명 이벤트, 흔적 목록(생성 시점·가시성), 첫인상 주장, 반박 규칙, 재구성 답, 자막, 동작 }` 한 레코드를 두고, 계획·실행·단서·재판·전말·테스트가 모두 이 레코드를 읽게 한다. 지금 흩어진 7~8곳 등록을 한 곳으로 모은다. 강제 증명 세트는 레코드를 자동으로 순회하면 된다.
2. **계획은 문자열이 아니라 구조체**: `Plan { Head, Layers: List<LayerId>, Steps }`, 층 사이 호환성은 태그(`needs-body-in-place`, `moves-body`, `remote`, `staged`)로 선언한다. Augment 순서 의존을 없앤다.
3. **물리 모델을 방법의 바닥으로**: 추락(높이·난간), 감전(회로·물), 익사(수면 높이), 질식(자세·시간), 절단(도구 등급·시간)을 "상태 변화 규칙"으로 두고, 방법은 그 규칙을 **어떻게 일으키는지**만 기술한다. 지금은 `AtEdge` 거리, `FireShock` 즉시 강도 4처럼 방법마다 결과를 직접 쓴다.
4. **단서 = 흔적 인스턴스 하나 → 여러 관찰 경로.** 흔적이 "무엇을 알려 주는가(Prop)"와 "누가 어떤 조건에서 볼 수 있는가"를 함께 들고, ExamineBody/Trace/Item/Furniture 네 겹 사슬을 하나의 질의로 바꾼다. 사건당 핵심 단서 수 상한(5~9, clues-and-qol 목표)을 여기서 강제할 수 있다.
5. **Prop 값은 enum/상수 테이블 하나**(생성·판정·적합성·하네스가 공유).
6. **Incident와 계획의 연결을 원장으로 일반화**: "이 죽음을 일으킨 원인 이벤트(함정 발동·독 섭취·타격)"를 Incident에 원장 참조로 저장하면, 엉뚱한 피해자·원격 사망도 계획·트릭·자막이 일관된다.
7. **발견 단위 일반화**: `BodyPart`(Gore) 또는 "시신 조각"을 Cases가 직접 다루게 하고, 발견 카드·FoundRoom·발견 영상이 그 단위를 기준으로 하게 한다.
8. **자연 발생률을 검증 대상으로**: 무강제 N시드에서 방법별 최소 실행 수(예: 각 종류가 20시드 중 1회 이상)를 테스트 목표로 둔다. 지금은 강제 증명만 있다.

---

## 9. GapReport_Mystery.md의 열린 격차 (요약)

- 미구현: 샹들리에 낙하(커널 가구에 샹들리에 없음), 저온 보관실 가둠에 의한 저체온사(사인 체계에 저체온 없음), 범인의 **의도적** 시계 조작 알리바이(IG09 — 시계 편차·태엽 기록은 있음).
- 부분: 추락 첫 시도 실패 시 피해자가 범인을 알지만 공개 고발(`case:accuse`) 흐름은 부상 없이 발동하지 않음.
- 게임층: `Anim.Shove` 매핑, 감전·보일러 전용 효과, 해체된 몸통·조각 렌더링(Gore 워크플로 담당).
- 검증: 사람이 직접 풀어 보는 난이도 검증 없음(헤드리스 스마트 플레이어만).
- Gore 연동: `Methods.CutUp`을 `Gore.Dismember(sim, victim, by, tool, placement)`로 바꾸고 임시 `SeveredPart` 타입을 정리(§2-11).

---

## 10. 파일 목록 (이 트랙이 만들거나 고친 것)

신규: `Sim/Systems/Methods.cs`, `MethodsActs.cs`, `MethodsClues.cs`, `MethodsDismember.cs`, `Sim/World/ItemPlacement.cs`, `Sim/Trial/TrialMethods.cs`, `Tests/BL23/SimTests/Mystery.cs`, `Documentation/BL23/GapReport_Mystery.md`, 이 문서.
소유(수정): `Crime.cs`, `Tricks.cs`, `SetPieces.cs`, `Trial/Logic.cs`, 아이템 카탈로그(`World/WorldTypes.cs` ItemCatalog).
공유(작은 추가): `WorldTypes.cs`(RoomType Incinerator/ColdStorage/Darkroom, 가구 Incinerator/ColdLocker/DevelopTable), `LayoutGenerator.cs`(방 요구·조명·Settle 호출), `Decorator.cs`(신규 방 3종 Dress), `Simulation.cs`(SpawnLoop), `Perception.cs`(지각 필터·가해자 자기 피해자 발견 억제·조각 발견), `Combat.cs`(질식 부위·비출혈 사인·흔적 병합), `Evidences.cs`(얼굴 질식 문구), `Movement.cs`(StepMotion), `Replay.cs`(자막 분기·Strike 필터·얼굴 질식 자막), `TrialSystem.cs`(첫인상 호출·재구성 선택지·TrickOf), `TrialGames.cs`(트릭 명판·적합성·전말), `Tests/BL23/SimTests/Extra.cs`(mystery 모드 등록), `Game/World/ItemView.cs`(`MurderProps` 절차 메시 21종).
