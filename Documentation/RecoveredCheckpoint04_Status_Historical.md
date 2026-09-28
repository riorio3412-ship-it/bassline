# ImplementationStatus — checkpoint 04

2026-09-19 · 전체 게임 IN PROGRESS · 상용 출시 가능 상태가 아님.

이번 변경은 기존 생활/조사 구현 위에 Source FixtureK의 **실제 접촉 사건 일부**를 연결합니다. checkpoint03 상태 문서와 검수표는 Documentation/Checkpoint03_*에 보존했습니다. 원자료 56개는 수정하지 않았습니다. 작업 시작 전 checkpoint03 배포본과 코드·asmdef·Packages·ProjectSettings를 비교해 사용자 변경 0건을 확인했습니다. 기존 Desktop/BASSLINE 연결과 Unity/URP 버전을 유지했습니다.

## 결정 상태

- USER/SOURCE CONFIRMED: 마우스 자유 시점, E 상호작용, 1인칭, A/B/C 권한, 마지막 확인 위치, NOTE 정지, 지지/비입증 범위 구분, 출처 보존, 같은 물리/시야 조건을 유지합니다.
- SOURCE CONFIRMED / TestOnly: SRC11 P1636–P1638의 6인 4실, X31 접촉 묶음, O31, 연속 목격과 사전 설치 V1, 치명 사건 cap 1을 이번 시험 범위에서 사용합니다. 본편 역할 확정이 아닙니다.
- PRODUCTION PROPOSAL: 14:59 시작의 축약 setup, NPC/작업대/카메라의 구체 좌표, 30tick 접촉 유지, 15:06까지의 기회 창, 단순한 고정 시험 의도, 쓰러짐 Proxy, 2초 조사 UI는 가역적입니다. 원문의 전체 타임라인 완료로 취급하지 않습니다.
- SOURCE 시간 기준: 기본 접촉 15:02:05, 결과 15:02:10, 퇴장 시도는 15:02:30 이후. 문·사람·물건·약속 개입에 따라 지연/불성립할 수 있습니다. 42m/56m 복도 길이는 유지하며 실제 방 안 이동·문 대기 때문에 전체 이동시간은 추가됩니다.
- REVIEW REQUIRED: 정식 모델/의상/rig/animation, 최종 아트·본편 서사·음성·성장 수치는 확정하지 않았습니다. Yusti는 19번째 인물이 아니며 R07/엔딩/저택 비밀을 발명하지 않았습니다. C01–C08 OFF. BREAK 지정 세 인물 allowlist와 MissingApprovedArt 거부를 유지합니다.

## 생성/변경 경로

Assets/BASSLINE 기준입니다. 실제 경로/GUID/local ID 매핑은 Verification/GeneratedBindingManifest.csv에 있습니다.

| 경로 | 이번 동작 | 범위/제한 |
| --- | --- | --- |
| Scripts/Core/IncidentContracts.cs | stable ID를 가진 X31 시험 설정, 물리적 접촉/관측 어댑터 계약 | 범용 본편 IncidentTemplate 완성 아님 |
| Scripts/World/Fixture/LifeWorld.Incidents.cs | 접근→관측한 도구 확보→접촉 진행→CauseEvent→지연 ResultEvent, cap 예약, 실제 관측자/녹화, 접촉 흔적, 퇴장 | TestOnly 단일 접촉 묶음. 범용 의도 utility, 대안 평가, 사건 5형식 미완 |
| Scripts/World/Fixture/LifeWorld.cs, LifeRecords.cs | 행동 불능 인물의 활동/물건/문 명령 거부, 초기 anchor에서 그래프 진입점까지 실제 이동, 계획상 우회와 관측한 잠금 지식 분리, 몸/시선 방향 저장 | 6인 fixture 도보/단순 회피. 일반 동적 장애물 경로 계획 전체 검수 아님 |
| Scripts/Bootstrap/FixtureRuntime.Incidents.cs | 실제 PhysX 손 주변 접촉·거리·시야, 인물/카메라 FOV·차폐, 연속 목격, V1/P31/쓰러진 인물의 시간 비용 조사, Proxy 충돌체 | 얼굴·손 rig/접촉 애니메이션 및 고급 조명 인지 미완 |
| Scripts/Bootstrap/FixtureRuntime.Knowledge.cs | 실제로 보이는 표면 물건의 B 기록, 서윤 본인이 목격한 사건의 근접 증언, UI용 인물/장치/현장 행동 | 서윤 증언은 FixtureK 허용 대화. 다른 NPC가 자기 비밀을 자동 고백하지 않음 |
| Scripts/Core/KnowledgeContracts.cs, Knowledge/KnowledgeLedger.cs, Investigation/Logic/LogicResolver.cs | provenance key를 실제 매체/목격 근원에 연결, 반복 V1 열람/전언을 독립 출처로 중복 계산하지 않음, 전달 중 출처 변조 거부 | 출처가 서로 다르다는 사실은 진실 확률이나 유죄 판정이 아님 |
| Scripts/Bootstrap/FixtureRuntime.cs, Save/Serialization/SessionSaveStore.cs | 접촉/지연 결과/관측/조사/화면을 함께 저장, 시험 씬별 슬롯과 모드 검사 | Unity inline null 복원을 고려한 명시적 incident Enabled 상태. 전체 본편 migration은 미완 |
| Scripts/UI/Views/FixtureHud.cs | 장치/흔적/인물 상태를 E 조사, NOTE 사건 탭에 수신한 사건 관련 기록만 표시 | 새 사건의 숨은 진행/취소/예방 성공을 표시하지 않음. 재판 UI 연결은 아직 없음 |
| Scripts/AuthoringData/FixtureIncidentDefinition.cs, FixtureCaseReader.cs | 시험 설정 SO, 사전 설치 센서/열람 장치/흔적 바인딩 | FunctionalProxy, 최종 모델 아님 |
| Scripts/Editor/Authoring/IncidentFixtureBuilder.cs | Editor API로 scene/SO/센서/물건/충돌체를 생성하고 stable ID/소유권 해시 등록 | 수동 수정 충돌 보호, 반복 생성 중복 없음 |
| Scenes/Tests/FixtureK_Incident.unity | 6인 4실 접촉 사건 시험 씬 | 본편 production scene과 별도 |
| Data/Incidents/K_INCIDENT_SETUP_01.asset | SO_K_INCIDENT_01 | PRODUCTION_PROPOSAL_TestOnly |
| Scripts/Bootstrap/TestOnlySceneNavigation.cs | Shift+F3 사건 시험 진입, 기존 F1/F2 유지 | 전환은 새 세션; 저장 후 전환 |
| Scripts/Bootstrap/IncidentSmokeProbe.cs | 명시적 실행 인수에서만 Windows 접촉/지연 결과/개인 기록 차단/저장 시험 | 실제 사용자의 일반 플레이에는 생성하지 않음 |
| Tests/EditMode/IncidentDomainTests.cs, Tests/PlayMode/IncidentPlayModeTests.cs | 출처/저장 무결성·약속 지연, 실제 실행·차폐·개입·접촉 중지/복구·매체/흔적/인물 조사 | 아래 검수표의 실행한 subset만 PASS로 계산 |

## 단계별 남은 범위

| 단계 | 상태 | 주요 미완 |
| --- | --- | --- |
| Bootstrap/import/Main Hall/P0 | PARTIAL | 최종 Main Hall 아트, 전체 본편 ID/권한/migration |
| P1–P3 | PARTIAL | 전 시설·17 NPC, 정식 rig/IK/활동, 장기 욕구·거절·중단·일정/소문 |
| P4 | PARTIAL | 기본 시야와 매체/목격은 연결. 청각/광량/주의/기억 변형/공적 게시 실제 수신 전체 미완 |
| P5–P6 | PARTIAL | 기록·가설·Logic subset 연결. 전체 FixtureK의 12 Evidence, 독립 입증 경로 2개, 4질문/8Lead/5 deduction 등 미완 |
| P7 | PARTIAL | X31 실제 접촉 subset만 연결. 범용 의도/사건, 전체 준비·Orphe·N 로그·신고/확인/공표 미완 |
| P8 | PARTIAL | 실제 결과로 생긴 쓰러짐 충돌체/접촉 흔적과 조사. 일반 현장 이동/훼손/보존·15:10 잠금 및 로그 미완 |
| P9 | COMPONENT PARTIAL / GAME FLOW NOT RUN | 실제 집결·재판 화면·발화/오디오·판결·전말 연결 |
| P10–P13 | NOT IMPLEMENTED | 평가/성장·정산·잔류 인원·18인 루프·18신·승인된 본편 콘텐츠 |
| 18인/M01/상용 품질 | NOT COMPLETE | 정식 모델18·침실18·재판석18·ENV02–13·41화면 full flow·오디오·기기 성능·플레이테스트·승인 |

Source FixtureK의 전체 14:50 준비와 Orphe 녹음/재생, N 통행 로그, 신고/유스티 확인/공표, 보존 잠금, 실제 재판 집결/투표/판결은 이 접촉 씬에서 자동 생성하지 않습니다. Cause/Result 외 나머지 7 Times/5 Locations 전체 모델은 후속 과제입니다. 인물들이 아직 알지 못하는 사망 확정/정답을 UI에 노출하지 않습니다. Archive를 현재 B로 복사하지 않습니다.

## 검증과 재현

Unity 6000.6.0f1 / URP17.6.0 / uGUI2.6.0 / UTF1.8.0 / seed0. 실제 최종 실행 결과, revision, 입력/기대/실제, 제한은 Verification/Checkpoint04_TestCoverage.md, Checkpoint04_ExecutedTests.csv 및 JSON을 따릅니다. 코드 존재와 문서의 모의 검수는 PASS로 계산하지 않습니다.

물리 좌표의 복구 비교는 최대 1μm 기준이며 tick/ID/이벤트/출처/화면/예약은 정확 비교합니다. Unity JSON의 double 마지막 비트 차이와 WorldClock의 이진 clock remainder 보존을 구분합니다. 엔진 scene/SO는 실제 Editor builder로 생성됐습니다. 열기·조작·재현은 README_Checkpoint04.md에 있습니다.

실제 최종 검증: Unity EditMode 30/30, PlayMode 20/20, .NET 16/16 PASS. Windows 빌드 오류 0, 경고 1건. 생활/사건 실행본 시험 모두 PASS. 원자료 56개 보존, 생성 자산 399개 무결성 확인. Revision: 0e714e9351379fff4a80fd3a1bacdfce4a0c63abbb94a14cad5426bac48ac69f. 전체 게임/정식 아트/재판 통합은 미완입니다.

