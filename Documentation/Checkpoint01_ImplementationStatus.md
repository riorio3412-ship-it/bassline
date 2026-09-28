# ImplementationStatus

상태일 2026-09-18 · checkpoint 01 · 전체 구현 **IN PROGRESS**

## 기준과 보존

- [USER CONFIRMED] 이번 MASTER IMPLEMENTATION PROMPT 및 BREAK 추가 지침을 실행 요청으로 적용. 문서 안의 과거 실행 지시는 자료로만 취급.
- 원자료: `C:/Users/리오/OneDrive/Desktop/BASSLINE_PRODUCTION_PACKAGE`. `SourcePackage/`에 원본 56파일을 내용 변경 없이 보관. 개별 결정 상태와 SOURCE_REGISTER의 REV01–12를 유지.
- 사용자가 선택한 `C:/Users/리오/OneDrive/Desktop/BASSLINE`은 Hub 등록만 존재하고 실제 폴더는 발견되지 않음. 다른 프로젝트에 임의 덮어쓰기하지 않음.
- 이전 실제 BASSLINE: `C:/Users/리오/Documents/Codex/2026-09-17/codex-ai-codex-bassline-1-bassline/outputs/BASSLINE`. 해당 Assets·문서는 `Documentation/LegacySnapshot`에 보존. 기존 방 교체/순간이동 프로토타입 코드는 현재 Assets에서 실행하지 않음.
- 발견 범위의 AGENTS.md 및 Git 저장소 없음. 따라서 Git commit을 만들었다거나 기존 변경 diff가 깨끗하다고 주장하지 않음. 실제 코드 revision은 `Verification/build-revision.txt`의 SHA-256.
- 이전 ProjectSettings/Packages를 복사한 뒤 구현본에서만 변경. 기존 6000.6.0f1 / Built-in / Gamma / Legacy Input 유지. 특정 버전을 최신이라고 주장하지 않음.
- `com.unity.modules.textrendering@1.0.0`은 설치된 Editor에서 해석되지 않아 첫 실행 실패. 별도 임시 Unity 프로젝트의 `UnityEditor.PackageManager.Client`로 유효한 기존 모듈과 Test Framework 1.8.0을 resolve하고 결과 manifest/lock을 계승. 수정 전 파일은 `Documentation/PackageBaseline`에 보존. 수동 JSON 패키지 조작으로 해결하지 않음.

## 경로별 구현

| 경로 | 구현/결정 상태 | 한계 |
| --- | --- | --- |
| Scripts/Core | [PRODUCTION PROPOSAL] stable ID, command port, clock view, knowledge view 계약, world clock, owner pause | B/C query 구현, CourtClock, RNG stream 없음 |
| Scripts/World | [PRODUCTION PROPOSAL] A 전용 snapshot registry, actor pose, door 상태, idempotent RequestUnlock | 소유자·거리·인접방·tick·revision 검증만 구현. 물리 잠금 상호작용과 문 leaf 연결 전. 다른 명령은 NotImplemented |
| Scripts/Save | [PRODUCTION PROPOSAL] Bootstrap 전용 schema 1, checksum, atomic replacement, previous 파일, 참조/receipt 복원 검사 | 본편 BASSLINE_SAVE가 아님. B/C·활동·소지·RNG·재판·보상·루프·마이그레이션 없음 |
| Scripts/Bootstrap | ArtReview 실행, fixed tick 누적 시간, pause, SaveBootstrap/LoadBootstrap | FixtureK/MansionM01 모드는 NotSupportedException. 빈 A 세션이며 홀 geometry 저장과 본편 player pose 저장은 아직 통합 전 |
| Scripts/AuthoringData | raw CSV를 손실 없이 보관한 ImportedDefinition과 identity component | 정식 typed RoomDefinition/CharacterDefinition 등 기능 정의 완료가 아님 |
| Scripts/Editor | CSV reader, validator, importer, MainHallBuilder, verification exporter | model socket, nav reachability, full collision/vision 검증 미완 |
| Scripts/Presentation/Review | WASD/RMB 검토용 capsule. Core clock만 참조 | 본편 player motor/NPC 공통 motor 아님; ArtReview 도구 |
| Scripts/Presentation/Break | 사용자 확정 세 인물 allowlist, 짧은 cue, 중복 재생 거부, pause/화자 변경/접근성 설정 시 복구 | 승인 art가 없으면 거부. 실제 화면 검수 미실행 |
| Scripts/Presentation/BreakUnity | 정상 standing을 바꾸지 않는 승인 overlay adapter | SpriteRenderer adapter만 있음. 대화 Canvas, Focus pause 연결, 30–40% standing 배치 미구현 |
| Data 아래 SO 218개 | 80 room + 79 connection + 18 character + 41 screen, AuthoringProxy | 41개 screen SO는 41개 화면 구현이 아님 |
| Prefabs/Rooms 아래 80개 | AuthoringProxy_NoGeometry. anchors 277/seat 201 local transforms | 가구, approach/IK socket, 18인 착석/소유권·예약 아직 없음 |
| Scenes/Mansion_Main.unity | FunctionalProxy_ArtAndNavNotApproved. Editor API 생성 | 홀만 검토 가능. 다른 79공간은 플레이 공간으로 배치하지 않음. 고품질 Main Hall 기준 미달 |
| Environment/Materials/Proxy | stone/tile/metal/carpet/water sample 5개 | Standard 단순색 재질; 텍스처/조명/유리/물 shader/최종 아트 없음 |
| Tests/EditMode, Tests/PlayMode, Tests/*.csproj | 실행 가능한 검증 | 아래 검사 범위만 증명 |

위 Scripts/Data/Prefabs 경로는 모두 `Assets/BASSLINE/` 아래입니다. 씬의 실제 serialized ID·GUID·local file ID와 경로는 `Verification/GeneratedBindingManifest.csv`, 생성 자산 소유권/해시는 `Verification/generated-assets.json`에 있습니다. 저장 키에 display name이나 hierarchy path를 사용하지 않습니다.

## 단계 상태

| 사용자 순서 | 상태 | 남은 핵심 |
| --- | --- | --- |
| 1 Bootstrap | PARTIAL | 한국어 폰트 fallback 자산, production input/권한 composition, 전체 assembly 역할 연결 |
| 2 Data import/validators | PARTIAL · 실행한 subset PASS | typed runtime definitions, geometry/nav/vision 진단, 실제 missing asset 제작, socket 해결 |
| 3 First Scene | PARTIAL · 홀 보행 subset PASS | 완성 아트, 난간 연속성/전체 접근성, NavMesh·관측 공통 조건, 승인 |
| 4 Authority first | PARTIAL · 저장/명령 subset PASS | objects/ownership/reservations/motion/perception·전체 registry 및 DTO |
| 5 FixtureK/P0–P3 | NOT IMPLEMENTED | Source K 데이터와 별도 맵. 42m·56m를 M01에 적용하지 않음 |
| 6 P4–P8 | NOT IMPLEMENTED | B/C·개인 기억·조사·추리·실제 사건·현장 변경 |
| 7 UI full flow | NOT IMPLEMENTED | 41개 prefab/viewmodel 및 full flow, Focus 5행동, NOTE, 한국어/정보보안 검사 |
| 8 Characters | NOT IMPLEMENTED | CH01–18 정식 모델, rig/IK/animation/bedroom/seat binding |
| 9 Mansion expansion | NOT IMPLEMENTED | ENV02–13, M01 전체 공간, 실제18재판석 |
| 10 P9–P13/integration | NOT IMPLEMENTED | 재판·평가·성장·정산·루프·18인/18신·승인 본편 |
| BREAK 추가 지침 | CONTROLLER PASS / ART NOT PROVIDED | 세 승인 원화 overlay와 실제 dialogue/court pause 연동 |

한국어 폰트는 패키지에 배포 가능한 font asset이 없고 현재 TMP/uGUI도 설치하지 않았습니다. OS 맑은 고딕을 재배포하거나 fallback 완료로 표시하지 않았습니다. UI 단계에서 한국어 글리프를 포함하는 라이선스 확인된 font를 importer로 만들고 장문/혼합문자/atlas overflow를 실제 검사해야 합니다. 현재 ArtReview에는 runtime 문자열 UI가 없습니다.

## 실제 검증 결과와 범위

- `.NET 10.0.12`: CSV 파싱, 데이터 오류 주입, ID, pause, command idempotency, snapshot 복사/복구, atomic save, 손상/참조 오류 거부 16/16 PASS. Unity 실행과 구분.
- Unity EditMode: 9/9 PASS. import된 room reference, 재실행 hash/GUID 보존, 수동 변경 conflict, pause, Unity JsonUtility 저장 복구/쓰기 중단, BREAK 규칙, assembly 권한 경계, receipt 불일치 거부.
- Unity PlayMode: 2/2 PASS. 실제 capsule의 정해진 F1→F2 보행, 중앙 void, 입구·동쪽 통로 raycast, 서쪽 난간 raycast; ArtReview clock pause/중첩 lease/저장 load.
- 이후 importer의 `.meta`/GUID 수동 변경 보호를 추가하고 관련 import/reference/conflict 3개만 재실행해 3/3 PASS. 보고서 revision 대응은 `Verification/TestRunIndex.csv`.
- `AT-SPACE-01`은 import/ID 정적 범위 PASS. AT-SPACE-02/03, AT-TIME-02, AT-SAVE, AT-INFO의 일부만 실행. 이들의 전체 검수는 NOT RUN 상태를 유지. 난간 한 지점 raycast를 전체 추락 방지로 확대 해석하지 않음.
- Main Hall 카메라 실제 렌더 PNG 확인. overbright 단순 재질·부족한 디테일·미완 난간 등이 보이는 기능 Proxy이며 최종 아트 승인 없음. 18인 비교 샷/성능 측정 없음.
- importer의 missing asset 256개와 미해결 socket 277개를 WARNING으로 보고. 개별 목록은 `Verification/unity-manifest-diagnostics.txt`. 소스가 가진 미제작 자산을 importer 오류 0과 혼동하지 않음.
- UI·NPCBrain·LogicResolver 전체 정보 누출 검사는 NOT RUN. 현재 Presentation→A/Save/Bootstrap assembly 의존 금지만 PASS. UI와 B/C 자체가 아직 없으므로 전체 정보보안 완료 주장 금지.

초기 실패도 숨기지 않고 로그에 남겼습니다. 잘못된 패키지, prefab의 ENV_ asset ID와 PF_ prefab ID 혼동, batchmode 빈 장면 additive 제약, exporter field명 컴파일 오류를 수정했습니다. 첫 PlayMode 시간 테스트는 두 headless 프레임이 1/60초보다 짧은데 tick을 요구한 테스트 오류로 실패했고, 실제 0.05초 대기로 고친 뒤 PASS입니다. 최신 결과 XML과 initial failure XML을 구분합니다.

## 보존한 결정 경계

김민혁 1명 + 17 NPC의 manifest를 유지했습니다. 유스티에는 `PRES_YUSTI`라는 presentation ID만 사용하며 CH19를 만들지 않았습니다. 테스트의 actor/door는 TestOnly이고 본편 배역을 배정하지 않습니다. R07·C01–C08·메인 엔딩·저택/유스티의 비밀·세나 의수 좌우·미승인 의상/얼굴을 만들거나 확정하지 않았습니다. Archive→현재 B 복사, truth 색상, 숨은 testimony 표시 기능도 없습니다.

다음 정상 구현 단위는 P0/FixtureK를 위한 권위 room/object/reservation/knowledge 계약과 저장 section 확장입니다. 이후 같은 collider/door/seat/vision 조건으로 P1 이동을 연결해야 합니다. 미승인 모델은 이 시스템 작업을 막는 이유가 아닙니다. 각 단계는 필요한 실제 검수를 추가 통과하기 전 완료로 변경하지 않습니다.
