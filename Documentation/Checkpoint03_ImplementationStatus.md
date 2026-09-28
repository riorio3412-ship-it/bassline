# ImplementationStatus — checkpoint 03

2026-09-18 · 전체 게임 IN PROGRESS · 단계 전체 완료 또는 상용 출시 가능 상태가 아님.

기존 checkpoint01/02 문서와 실행 결과, ZIP을 보존했습니다. 원자료 SourcePackage 56개는 원본과 해시로 대조합니다. 시작 전 기존 cs/asmdef/Packages/ProjectSettings를 checkpoint02와 비교했고 사용자 변경 0건을 확인했습니다. 선택된 Desktop/BASSLINE은 이 출력 프로젝트의 기존 디렉터리 연결입니다.

## 적용한 결정

- [USER CONFIRMED] 우클릭 없는 마우스 시점, E 상호작용. 생활 씬과 홀에 적용. WASD/E/R/G/N/M/Esc/F5/F9 및 재판용 F를 사용합니다.
- [USER/SOURCE CONFIRMED] UI·NPCBrain·LogicResolver는 A 비밀에 접근하지 않습니다. UI는 Core read/command 계약만 받습니다. 실제 물리와 인지의 연결은 Bootstrap 어댑터에 있습니다. Domain은 Presentation을 참조하지 않습니다.
- [PRODUCTION PROPOSAL / REV07] 기존 Unity 6000.6.0f1, URP 17.6.0, uGUI/TMP 2.6.0, UTF 1.8.0을 유지했습니다. Built-in 전환은 checkpoint02에서 실제 완료했고 이번에도 URP/Quality 검사를 실행합니다. Legacy Input은 이번 기존 프로젝트의 구현 기본값입니다.
- [PRODUCTION PROPOSAL] 2초 조사, 5분 후 초대, 2분 약속 창, 단순 동의 정책, 관계 변화량, UI 기본 배치·글자 크기는 가역적인 기능 시험값입니다.
- [REVIEW REQUIRED] 최종 아트·18명 정식 외형/의상·폰트 배포·rig·animation·성장 수치·본편 서사는 승인되지 않았습니다. 캡슐과 화면을 Final로 표시하지 않습니다.

## 변경 경로와 동작

경로는 `Assets/BASSLINE/` 기준입니다. ID와 실제 경로/GUID/local ID는 `Verification/GeneratedBindingManifest.csv`, 생성 소유권과 해시는 `Verification/generated-assets.json`에 있습니다.

| 경로 | 구현 | 제한 |
| --- | --- | --- |
| Scripts/UI/Navigation/PlayerControls.cs | 일반적인 키, 감도·Y 반전·E/N 재지정, 충돌/예약 키 거부, 기기 설정 보존 | 모든 행동의 재지정 UI·게임패드·터치 미완 |
| Scripts/UI/Views/FixtureHud.cs | NOTE 탭, 실제 생활 대화·약속, 기록/관측 상세, 조사 진행, 가설, 설정/저장/접근성. 세계 pause와 화면 복귀 stack | 41화면 전체 흐름 아님. 실제 재판/엔드게임 화면은 진입 차단 |
| Scripts/Presentation/Review/ReviewWalkController.cs | Main Hall 자유 마우스 시점과 Esc 커서/정지 | ArtReview 보행 도구이며 본편 플레이어 시스템 아님 |
| Scripts/Knowledge/KnowledgeLedger.cs | 인물별 직접 관측·실제 전달 수신, 원본/부모 출처, 동일 근원 중복 배제, 소유자별 query, Last Confirmed | 광량·청각·주의력·얼굴 식별 성능·기억 변형 등 전체 P4 미완 |
| Scripts/NPC/Schedule/SocialLedger.cs | 약속 revision의 실제 수신/수락/수락 응답, 자기 일정 기준 이동, 실제 만남 경험, 물건 전달 경험 | 장기 달력/욕구 utility/소문/거절 동기/18인 병목 미완 |
| Scripts/Bootstrap/FixtureRuntime.Knowledge.cs | 같은 collider·범위·시야로 얼굴/소지 관측. 조사 동안 실제 시야·거리 검사, 시간 진행·취소·정지, 생활 정보 대화 | 6인 4실 fixture 전용. 인지 tick은 30tick 간격, 반복 위치 관측은 600tick 최소 간격 |
| Scripts/Investigation | 소유자별 가설·cycle 거부 graph, LR01–10의 등록된 범위 규칙, unknown rule/비수신/다른 회차 거부 | 자동 사건 완성/유죄 판정 없음. 모든 LR 상세 의무·route bound·인과 반례의 완성 검수 아님 |
| Scripts/Save/Serialization/SessionSaveStore.cs | World+B+사회+가설+조사+화면/선택 stable ID를 checksum/temp flush/atomic replace/previous로 함께 복구 | FixtureSession schema1. 기존 life-slot 자동 마이그레이션, 전체 본편/프로필 transaction 미완 |
| Scripts/Trial/Topics/TrialDirector.cs | 집결 승인 gate, 단계별 발화·수신, 3개 주요 claim, 미발화 전문 차단, FOCUS/중첩 Court 정지/정확 cursor, 5개 행동 API, 부분 span 상태, 수신별 판정 공개, 중복 제출, 비공개 표→전체 잠금 | 순수 컴포넌트(TestOnly) 검증. 실제 집결 어댑터·P7/P8·UI/발언 오디오와 연결하지 않음. 판결/정산 성공을 반환하지 않음 |
| Scripts/Editor/Authoring/ProductionUiBuilder.cs | UI_01–41의 prefab+definition, theme, Canvas 8개, 한국어 TMP/ScrollRect/키보드 버튼 탐색 | generic FunctionalProxy 공통 구성. 각 화면 최종 전용 디자인·41개 완성 viewmodel 아님 |
| Data/UI/ScreenDefinitions, Data/UI/Themes, UI/Prefabs | Unity Editor API로 생성, stable ID 반복 import, 수동 변경 충돌 보호 | 최종 아트 승인 없음 |
| Tests/EditMode/KnowledgeLogicTests.cs, TrialDirectorTests.cs | 개인 수신·약속 revision·부분 반박·독립 근원·FOCUS·투표 snapshot | 주입된 시험 자료이며 실제 사건 P7 통과로 쓰지 않음 |
| Tests/PlayMode/KnowledgePlayModeTests.cs | 자유 시점/E 명령, 닫힌 문 인지 차단, 실제 조사·NOTE·세션 복구, 장문 한국어 200% | 자동 입력 경로 검사. 실제 키보드/마우스 기기 사용성 평가는 별도 |
| Scripts/Bootstrap/CheckpointSmokeProbe.cs | 새 Windows 실행본의 마우스 명령/줍기/생활 대화/약속/조사/세션/화면 복구/URP/홀 로드 | 명시적 smoke 실행 인수가 있을 때만 생성. 테스트용 시작 위치 설정 |

## 게임에서 열리는 UI와 미완 화면

- 연결: UI_01/02/03, UI_07–13, UI_14–21, UI_38–41. 다만 빈 사건/아카이브는 빈 상태이며 사건 서비스가 완성됐다는 뜻이 아닙니다. UI_20은 자료 목록과 가설 연결 시작점이지 완성된 드래그 그래프 보드가 아닙니다. UI_21은 자료 기반 가설 생성/유보/철회이며 자유 서술 편집은 미완입니다.
- 부분: UI_22 지도 참조·실측 안내만 제공. route reconstruction/stopwatch/typed deductions 미완.
- prefab만 생성: UI_04–06, UI_23–37. 재판·성장·정산으로 성공한 것처럼 진입하지 않습니다.
- 대화 standing 슬롯은 35%이나 승인된 그림이 없어 비워 두었습니다. 그림이 있는 것처럼 표시하지 않습니다.
- 글자 200% 본문 스크롤을 시험했습니다. 개별 화면별 모든 해상도·200% 버튼/제목·contrast·게임패드 전수 검수는 NOT RUN.

## 단계 상태와 다음 의존 관계

| 단계 | 현재 상태 | 남은 필수 작업 |
| --- | --- | --- |
| Bootstrap/import/Main Hall/P0 | PARTIAL, 기존 subset PASS | 최종 Main Hall 아트, 전체 ID/권한/본편 migration |
| P1–P2 | PARTIAL, 동일 물리·문·생활·전달·저장 검증 | 정식 rig/IK/활동·접촉 애니메이션, 전 시설/17 NPC |
| P3 | PARTIAL, 약속 수신·revision·일정 이동·관계 경험 | 욕구/성격 utility, 거절/중단/장기 달력/소문/반복 일정 |
| P4 | PARTIAL, 관측·B/전언 근원·마지막 위치 | 매체/N/V1/청각/광량/주의·공적 게시 실제 수신 |
| P5–P6 | PARTIAL, 실제 조사·가설·Logic subset | Source FixtureK 12 Evidence/2 독립 proof, 4질문·8Lead·5 deduction 등 전체 검수 |
| P7–P8 | NOT IMPLEMENTED | 의도/대안/접근/기회/실행과 지연 결과, 실제 X31·V1·N·현장 변경, 플레이어 개입 분기 |
| P9 | COMPONENT PARTIAL / GAME FLOW NOT RUN | P7/P8 후 실제 집결·발화/UI·투표·Adjudicator·허용 전말 연결 |
| P10–P13 | NOT IMPLEMENTED | 평가·EXP·원자 정산·잔류 인원/18인 루프·18신·승인 콘텐츠 |
| 18인/M01/상용 품질 | NOT COMPLETE | 18 정식 모델·18 침실/재판석·ENV02–13·41화면 full flow·오디오·장기 성능·플레이테스트·승인 |

이 생활 fixture의 인물 배역은 본편 고정 역할이 아닙니다. M01에는 FixtureK의 42m/56m를 강제 적용하지 않았습니다. Yusti는 PRES_YUSTI이며 19번째 참가자가 아닙니다. C01–C08은 OFF, R07/엔딩/유스티·저택 비밀/의수 좌우/미승인 의상을 창작하지 않았습니다. ArchiveMeta를 현재 B로 자동 복사하지 않습니다.

BREAK allowlist 세 인물과 MissingApprovedArt 거부는 유지합니다. 정식 sprite/standing이 없으므로 이번에 일반 감정이나 얼굴 변주 이미지를 복제·확정하지 않았습니다.

실제 최종 실행 수치는 `Verification/TestCoverage.md`, JSON, `Checkpoint03_ExecutedTests.csv`를 따릅니다. 코드 존재나 문서의 예시를 Unity PASS로 계산하지 않습니다.
