# 클라우드 작업 기록 — 만든 것과 파일 위치 (BL23)

클라우드 세션(2026-09-26~28)에서 만든 것과 파일 위치를 정리한 문서다. 새 작업을 하면 맨 아래 "기록"에 한 줄씩 덧붙인다.

- **저장소:** `riorio3412-ship-it/bassline`
- **브랜치:** `claude/ecstatic-mccarthy-w08ico`
- **기준선:** `a946694` (오너가 올린 `BASSLINE_1_code.zip`을 그대로 푼 커밋)
  - 그 뒤의 커밋이 전부 클라우드 작업이다: `git log --oneline a946694..HEAD`
- **PC에 옮기는 방법:** [`CLOUD_RESUME.md`](CLOUD_RESUME.md)의 "PC에 반영하기"를 따른다.
  - 바뀐 파일만 복사하고, 덮어쓰기 전에 원본을 백업한다.
- **아래 경로는 모두 저장소 루트 기준이다.**
  - `Sim/…`은 `Assets/BASSLINE/BL23/Sim/…`을 줄인 것이다.
  - `Game/…`은 `Assets/BASSLINE/BL23/Game/…`을 줄인 것이다.

---

## 1. 토론 심판 — 단간론파식 토론을 은판 덱 위에서
심판을 "가설을 세우고 은판으로 무너뜨리는 토론"으로 다시 만들었다. 기존 심판 코드는 그대로 두고, 토론이 켜져 있으면 훅으로 넘어간다.

**흐름**
- **1막 「첫인상」, 2막:** 알리바이 점호 → 수수께끼(현장·흉기·사망 시각·밀실·바꿔치기·원인·범인) → 가설 → 발언권 → 판정 → "그렇다면…" 추론 → 종(좌중 집계)
- **3막 「반격」:** 지목 → 촛불 대결(범인의 거짓말·후퇴·마지막 요구, 매듭 기회·수단·거짓) → 조용한 붕괴
- **4막 「판결」:** 시계와 평면도(재구성) → 민혁의 최종 변론 → 투표 → 정산

**파일**

| 파일 | 내용 |
|---|---|
| `Sim/Trial/Debate/DeckBuild.cs` | 사건의 실제 기록으로 은판 덱과 첫 주장들을 만든다. 공정성 은판(흉기를 든 범인, 옷의 핏자국)은 반드시 싣는다 |
| `Sim/Trial/Debate/DebateModel.cs` | 토론 데이터: 가설·수수께끼·범인의 마음·거짓말·후퇴·촛불·매듭·결정 기록 |
| `Sim/Trial/Debate/DebateEngine.cs` | 진행자: 막·단계, 종과 좌중 집계, 명판, 4막, 투표와 판결, 공개 적합도(`DebatePublicFit`) |
| `Sim/Trial/Debate/DebateTheories.cs` | 가설 생성기(현장/사망 시각/밀실/바꿔치기/원인/범인), 인물별 말투(`InRegister`) |
| `Sim/Trial/Debate/DebatePlayer.cs` | 민혁의 행동: 맞대기·캐묻기·더 듣기·지목, 판정, NPC가 대신 나서기, 추론, 헤드리스 정책(smart/naive/passive) |
| `Sim/Trial/Debate/DebateCulprit.cs` | 범인: 떠넘기기, 흔들림, 촛불 대결(반격·후퇴·같은 은판 재사용 거절·붕괴), 인물별 버릇 |
| `Sim/Trial/Debate/DebateRoom.cs` | 알리바이 점호, 흉기 추리, 최종 변론(확정된 것만 말함) |
| `Sim/Content/Lines_Debate.cs` | 토론 대사 뱅크(가설·반응·대결·투표, 존댓말/반말) |
| `Sim/Trial/TrialSystem.cs`, `Sim/Trial/TrialGames.cs` | 기존 심판에서 토론으로 넘기는 훅 |
| `Sim/Content/LineBank.cs` | 조사 보정(괄호 뒤 은/는 등) |
| `Tests/BL23/SimTests/DebateDump.cs` | `debate`, `deckdump`, `deckgate` 테스트 모드 |

## 2. 네 인물의 성격(오너 지정)과 대사 수위
**성격 배정**
- **시온:** 자신만만하고 폭언을 달고 산다. 섹드립이 끝없이 쏟아진다.
- **진우:** 변덕스럽다. 우는 척을 하고, 순진한 웃음으로 사람을 괴롭히는 어그로다.
- **라온:** 어둡고 조용하며 사람들과 어울리지 않는다. 할 말은 하는데 말투가 까칠하다.
- **서윤:** 희망을 믿고 민혁을 챙긴다. 산뜻한 얼굴로 엄한 말을 한다.

**대사 수위**
- 인물별 연애·섹드립 제외를 없앴다. 전원 성인이다.
- 노골적 행위, 성폭력 농담, 실존 집단 비하, 어려 보이는 외모의 성적 묘사는 계속 금지다(HANDOFF §1.4).

**파일**

| 파일 | 내용 |
|---|---|
| `Sim/Data/Cast.cs` | 네 인물의 성격 수치와 말버릇 |
| `Sim/Content/Voice/Voice_Traits.cs` | 새 대사 팩(잡담·섹드립·우는 척·까칠함·조언 등) |
| `Sim/Life/LifeBanter.cs`, `LifeDialogue.cs`, `LifeHearts.cs` | 인물별 제외 해제, 새 반응 키 연결 |
| `Documentation/BL23/CharacterBible.md` | "Owner traits" 문단 |
| `Documentation/BL23/HANDOFF.md` | §1.4 규칙 |

## 3. 새 전제 — 소원 초대장, 약속, 벽, 저택의 부추김
**설정 (오너, 2026-09-28)**
- 모두 "소원을 이루어 드립니다"라는 초대장에 응해서 왔다. 살인 게임인 줄은 아무도 몰랐다.
- 그래서 처음에는 살인할 명분이 없다.
- 저택은 조용한 날이 이어지면 동기와 환경을 조금씩 조여 살인을 부추긴다.

**오프닝:** 유스티가 이렇게 말한다.
- 초대장은 거짓말을 하지 않았다. 방법을 적지 않았을 뿐이다.
- 현관은 잠겼다.
- 이것은 명령이 아니다.
- …그 마음이 언제까지 갈지 지켜보겠다.

민혁에게는 초대장도 계약도 없다.

**약속**
- 챕터의 첫 식탁에서 서윤이 "아무도 안 죽이기로 약속해요"라고 제안한다. 그 자리의 사람들이 약속 멤버가 된다.
- 첫 죽음이 나면 약속이 깨진다. 다음 식탁에서 "…약속했잖아요"라는 말이 나온다.
- 다음 챕터 첫 식탁에서는 "다시, 약속"을 한다. 효과는 절반이다.

**벽 (살인 거부감)**
- 각자의 억제력(도덕·두려움·공감) 위에 루프마다 벽이 하나 더 있다. 약속 멤버는 벽이 조금 더 높다.
- 시간만 흘러서는 깎이지 않는다. 사건이 있어야 깎인다.
  - 저택의 부추김, 굶주림
  - 죽음. 루프의 첫 죽음이 가장 크게 깎는다.
  - 범인의 탈출, 무고한 사람의 추첨 처형
- 새 챕터가 시작되면 벽이 일부 다시 자란다.
  - 정답 판결 뒤: 깎인 양의 절반이 회복된다(억지력).
  - 오답 판결 뒤: 5분의 1만 회복된다.
  - 그래서 민혁의 심판 실력이 다음 챕터의 템포를 정한다.

**저택의 사다리**
- 조용한 챕터에서 아침 종(07:00)이나 저녁 종(21:00)에 한 단계씩 오른다. 반나절에 한 번까지다.

| 단계 | 저택이 하는 일 |
|---|---|
| 소원의 견본 | 각자 방에 소원의 증거가 놓인다. 민혁 방에는 빈 봉투가 놓인다. |
| 과거의 봉투 | CH06 규칙을 챕터 도중에 부과한다. |
| 두 번째 소원 | 먼저 이룬 사람은 다른 한 사람의 소원도 이룰 수 있다. |
| 정기 소등 | CH03 규칙을 챕터 도중에 부과한다. |
| 저택의 인내 | 굶주림이 한 번에 한 단계 오른다. |
| 긴 침묵 | 이후 조용한 아침마다 반복되고, 날마다 더 세진다(0.04 → 0.08 → 0.12…). 누군가 무너질 때까지 벽 너머 억제력까지 누른다. |

- 각 단계는 식탁 주제가 된다. 예를 들어 견본 단계에서는 각자 자기 견본 이야기를 한다.
- 민혁은 식탁에서 약속을 상기시켜 벽을 조금 보강할 수 있다.

**속내 (민혁이 사람들을 붙잡는 장면)**
- 견본·두 번째 소원·첫 긴 침묵 뒤에, 가장 흔들린 두 명이 민혁을 찾아와 털어놓는다.
  - 흔들린 정도는 소원에 대한 집착과 민혁과의 관계를 함께 본다.
  - 민혁을 어느 정도 믿는 사람만 온다.
- 민혁이 대답할 수 있는 세 가지와 효과:

| 대답 | 효과 |
|---|---|
| "그 견본, 저한테 맡겨요" | 벽 +0.06. 견본을 넘긴 사람은 다시 오지 않는다. |
| "다른 방법으로 이뤄요" | 벽 +0.04 |
| "그 마음 알 것 같아요" | 신뢰는 크게 오르지만 벽은 −0.02. 공감이 유혹을 정당화하기도 한다. |

- 대사는 17명 각자의 목소리로 쓰였다. 키는 `confide_open`, `confide_give`, `confide_hope`, `confide_understood`다.
- 코드
  - 장면: `Sim/Life/LifeDialogue.cs`의 `confide`
  - 누가 찾아오는지: `HousePush.Confide`
  - 벽 보강·깎기 사실(`mend:`, `erode:`): `Life.cs`

**측정 (8~9일, 시드 5개)**
- 첫 살인이 2~3일차에서 3~6일차로 늦춰졌다.
- 1챕터는 한 사건이다.
- 조용한 챕터는 긴 침묵으로 풀린다.
- 세이브 왕복은 동일하다.

**파일**

| 파일 | 내용 |
|---|---|
| `Sim/Content/Lines_NPC00.cs` | 유스티 오프닝 `y_intro` (새 전제) |
| `Sim/Content/Lines_Premise.cs` | 약속, 다시 약속, 견본(17명 각자), 두 번째 소원, "약속했잖아요", 저택 공지(`y_push_*`), 봉투 대사 보충, 속내(17명 각자) |
| `Sim/Murder/Conscience.cs` | **벽**: `Inhibit`/`Wall`/`Erode`/`Mend`, 죽음·판결·챕터 훅 |
| `Sim/Systems/HousePush.cs` | **저택의 사다리**와 긴 침묵 |
| `Sim/Systems/Rules.cs` | `Rules.Impose`: 챕터 도중 규칙 부과 |
| `Sim/Systems/Hunger.cs` | `Hunger.Tighten`: 굶주림 한 단계 |
| `Sim/Systems/LifeAI.cs` | 07:00과 21:00 종에 사다리 연결 |
| `Sim/Systems/Relations.cs`, `Sim/Murder/InitiativeMotives.cs` | 두 살인 계산의 억제값을 `Conscience.Inhibit` 하나로 통일 |
| `Sim/Systems/Cases.cs`, `Sim/Trial/Settlements.cs`, `Sim/Systems/Simulation.cs` | 죽음 확인, 판결, 챕터 시작 훅 |
| `Sim/Life/LifeTable.cs` | 식탁 주제 `pact`/`push`, "약속했잖아요", 민혁 선택지 |
| `Sim/Life/Life.cs` | 장면 사실 `mend:` (약속 상기 → 벽 보강) |
| `Game/Core/Session.cs` | 식탁 장면 인원 5명 → 7명 |
| `Tests/BL23/SimTests/PremiseTest.cs` | `premise` 테스트: 식탁 대사 전문, 저택 공지, 벽 수치, 첫 살인 시각 |

## 4. 가구 변화와 사람별 지식 (이전 라운드)
- 가구를 밀고 끌고 넘어뜨린 결과를 커널 한 경로로 확정한다.
- 그것을 본 사람, 들은 사람, 나중에 알아챈 사람이 각자 다르게 안다.

**파일**
- `Sim/Systems/FurnitureChanges.cs` (새 파일)
- `Sim/Systems/PlayerPhysics.cs`, `Perception.cs`, `Gore.cs`, `Rules.cs`, `Tricks.cs`
- `Sim/Violence/Assaults.cs`, `Firearms.cs`, `ViolencePlans.cs`
- `Sim/State/State.cs`, `Enums.cs`, `Sim/World/WorldTypes.cs`
- `Game/World/WorldPresenter.cs`, `Game/UI/NoteUI.cs`
- 테스트: `Tests/BL23/SimTests/FurnitureKnowledgeTest.cs` (`furnknow`)

## 5. P10 강준서 모델 보정
- `ModelFix/P10_KangJunseo/`에 원본, 보정본 GLB, 전후 비교 이미지, `REPORT.md`가 있다.
- Unity 프로젝트 밖의 결과물이다. 보정본을 쓸 때는 `REPORT.md`를 보고 GLB를 직접 교체한다.

## 6. 찾아서 고친 기존 버그
- **식탁 대화가 한 줄도 나오지 않았다** (`Sim/Life/LifeTable.cs`, 기준선부터 있던 버그).
  - 식탁 장면이 첫 비트에 연결되지 않아서 모든 식탁 주제가 대사 0줄로 끝났다.
  - 고친 뒤 아침과 저녁 식탁이 실제로 말을 한다. 유대가 쌓이는 속도도 달라졌다.
- **최종 변론이 확인되지 않은 장소를 사건 현장이라고 단정했다** (`DebateRoom.cs`).

## 7. 검증

**커널 테스트**
- 클라우드와 PC 어디서나 된다.
- `Tests/BL23/SimTests` 폴더에서 실행한다.
```
dotnet run -c Release -- debate 20260926 smart    # 토론 심판 대본 (smart / naive / passive)
dotnet run -c Release -- premise 20260926 9       # 약속·견본·저택 공지·벽·첫 살인
dotnet run -c Release -- life                     # faults=0, roundtrip=IDENTICAL
dotnet run -c Release -- campaign 20260926 6      # faults=0, roundtrip=IDENTICAL
dotnet run -c Release -- furnknow 20260926 6      # 33/33
dotnet run -c Release -- voice lint out.txt       # 보이스 팩 위반 0
```

**PC에서만 할 수 있는 것 (Unity)**
- Game 레이어 컴파일: `Tests/BL23/GameCompile`에서 `dotnet build -c Release`를 돌려 오류가 0개인지 본다.
- 게임을 열어 1일차를 확인한다.
  - 아침 식탁에서 「약속」 장면이 나오는지.
  - 2일차 아침에 「소원의 견본」 공지와 식탁이 나오는지.

## 8. 다음에 할 일 (우선순위)
1. **벽과 사다리 밸런스 점검.** 시드 여러 개로 챕터마다 "평온 → 부추김 → 살인" 곡선을 본다. 도윤처럼 억제가 낮은 인물이 2일차에 서두르지 않는지도 본다.
2. **토론 타격감.** 기존 화면을 토론에 연결한다(Game 레이어, Unity 필요).
   - 촛불 심문(`Game/Trial/CandleInquiry.cs`) → 논스톱 토론
   - 붉은 실(`Game/Trial/ThreadBoard.cs`) → 최종 결론
3. **사건과 트릭의 다양성, 연출.**
4. **수사의 편의성과 밀도.**
5. **일상의 미연시화.** 식탁이 살아났으니 긴 호흡의 다인 장면을 늘린다.

## 기록 (새 작업은 여기에 한 줄씩)
- 2026-09-26: 가구 변화 커널 경로 + 사람별 지식 (`7ffd2f9`, `a0a2699`)
- 2026-09-27: P10 모델 보정 (`b22d987`, `9ee24d2`, `d0378a5`); 토론 모델·덱·대사 (`bcc2a23`); 토론 심판 (`11a13fd`)
- 2026-09-28: 네 인물 성격, 수위 해제, 토론 다듬기 (`da973de`); .meta (`ba05ac0`); 초대장 전제와 약속 (`c6f7494`); 벽·저택 사다리·식탁 버그 수정 (`c15bd2a`)
- 2026-09-28: 공정성 은판 — 목격자가 본 "범행 전 흉기를 든 범인"과 "범행 뒤 옷에 핏자국이 있는 범인"을 은판으로 올리고, 목격 은판 상한과 무관하게 최대 2장 싣는다 (`DeckBuild.cs` `FeedCulpritSeen`). 도윤 사건의 촛불 대결이 0/4에서 4/4가 됐다.
- 2026-09-28: 1챕터 문턱 — 저택이 첫 두 단계(견본·봉투)를 밟기 전에는 벽 +0.15씩 (`Conscience.Unpushed`). 1챕터 희생자 한도 1명 (`Simulation.BeginChapter`). 측정: 첫 살인 3~6일차(시드 5개), 1챕터 한 사건, 긴 침묵으로 정지 없음.
