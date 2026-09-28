# 실제 검사 범위

2026-09-18, Unity 6000.6.0f1, Test Framework 1.8.0, NUnit package 2.1.0. OS/CPU/GPU와 code/catalog SHA-256은 `engine-environment.txt`. 게임 seed 0(현재 RNG 없음), Rule `Bootstrap_001_REV10`, Map `M01` 또는 명시된 `TestOnly_P0`, Bootstrap save schema 1. 각 NUnit의 내부 seed는 XML에 따로 있습니다.

## 최신 결과

| 실행 | 결과 | 증거 |
| --- | --- | --- |
| Unity Editor builder | PASS · 실제 저장 | export-final.log, generated-assets.json |
| Unity EditMode | 9/9 PASS | editmode-results.xml, editmode.log |
| 마지막 importer 변경 관련 재검사 | 3/3 PASS | importer-final-results.xml, importer-final.log |
| Unity PlayMode | 2/2 PASS | playmode-results.xml, playmode.log |
| .NET 순수 로직/CSV | 16/16 PASS | domain-test-results.txt |
| Unity camera render | PNG 생성/내용 확인 | MainHall_FunctionalProxy.png, review-capture.log |
| 최종 아트 품질/18인/전체 게임 | NOT RUN | ImplementationStatus의 미완 목록 |

별도 초기 실패 XML, builder/error 로그는 수정 이력입니다. 최신 XML의 결과와 섞어 합산하지 않습니다. CLI가 실패 로그 앞에 라이선스 warning을 출력한 경우에도 실제 failure/compile/stack 기록으로 원인을 판별했습니다. 최종 실제 Unity 실행은 성공했습니다.

EditMode 9개/PlayMode 2개 실행 뒤 importer의 `.meta` hash 보호만 추가했습니다. 관련 import/reference/conflict 3개를 다시 실행해 모두 PASS였으며 runtime은 바꾸지 않았습니다. 각 결과의 code revision은 `TestRunIndex.csv`에 대응합니다. 추가 3개를 새 고유 검사로 중복 집계하지 않습니다.

## Unity EditMode 9개

| 검수 연결 / test | 입력 | 기대 | 실제 |
| --- | --- | --- | --- |
| AT-SPACE-01 / ImportedManifestReferences | M01 전체 CSV 로드, room SO/prefab ID 참조 확인 | 80 room/79 connection, 오류0, 올바른 root ID | PASS. 실제 import 자산 로드 성공 |
| importer repeated run | 동일 source로 builder 재실행 | asset path/hash/GUID 그대로 | PASS. 생성 자산304개 before/after 배열 동일 |
| importer manual conflict | 추적되지 않은 수동 파일에 생성 요청 | 오류, 기존 내용 보존 | PASS. 덮어쓰기 없음 |
| AT-TIME-02 subset | NOTE+SETTINGS acquire, 하나씩 release | 마지막 world owner 해제 전 tick 정지 | PASS |
| AT-SAVE-01/02 subset | TestOnly 두 방, owner unlock commit, NOTE pause, JsonUtility encode, temp write fault, load, 중복 command, checksum 훼손 | 실패쓰기 후 원본 복원, 동일 snapshot, event 중복0, pause 유지, 손상 거부 | PASS |
| BREAK allowlist/assets | 잘못된 화자/미승인 art/정상 세 화자/중복 cue/paused delta/화자 교체 | 정해진 result enum, 짧은 종료, overlay 복구 | PASS · fake surface에서 제어 로직만 |
| BREAK reduced/disable | cue 중 접근성 옵션 ON, dispose 후 호출 | 정상 상태 복구, 새 cue 거부 | PASS |
| AT-INFO-01 subset | 실제 asmdef 전체 의존 그래프 검사 | Presentation→World/Save/Bootstrap 금지, Domain→Presentation 금지 | PASS. 의미상 B/C 권한 전체 검사는 아님 |
| AT-SAVE-04 subset | receipt actor 또는 committed door 상태를 바꾼 snapshot load | 잘못된 state/event/receipt 거부 | PASS. 구버전 migration 구현은 없음 |

## Unity PlayMode 2개

| 검수 연결 | 입력 | 기대 | 실제 |
| --- | --- | --- | --- |
| AT-SPACE-02/03 subset | Mansion_Main 로드. TestOnly capsule을 (-0.15,0.04,2.2)에서 시작. 고정 1/60초 Move로 (6.4,2.2)→(6.4,5.8)→(0.1,5.8)→(0.1,8)→(9.9,8)→(9.9,6.4)→(11.6,6.4) XZ 경유 | 각 경유점에 500 step 미만으로 접근, 최종 y 5.95–6.15m. 입구/동쪽 opening ray clear, 중앙 void clear, 서쪽 rail ray hit | PASS. 실제 CharacterController로 계단 보행. 18 NPC/NavMesh/문 양방향 sweep/전체 난간 추락 검사는 제외 |
| AT-TIME-02/AT-SAVE-02 subset | ArtReview 세션 생성, 실제 0.05초 대기, NOTE+SETTINGS acquire, 0.08초 관찰, 저장, 한 owner씩 release, 0.05초 진행, load | tick 진행→정지→마지막 owner 해제 후 진행→저장 tick와 두 pause owner 복원 | PASS. 대기 시간이 world 누적에 들어가지 않음. 실제 NOTE 화면/Focus 화면은 아직 없음 |

첫 시간 검사 실패는 headless 두 프레임 동안 tick이 0인 상황을 실패로 판단한 테스트 코드 때문입니다. wall time을 명시적으로 기다리는 것으로 수정했고 현재 두 PlayMode 검사 모두 통과했습니다. 물리 bit-exact 결정성을 주장하지 않습니다.

## .NET 16개

CSV-01/02: BOM·한글·쉼표·인용·개행 읽기와 malformed CSV 거부. ID-01: display name/path 거부. AT-TIME-02.Domain: 중첩 pause/restore. AT-SPACE-01.Static: 전체 source manifest 일관성. VAL-DUPLICATE/FK/BOUNDARY/CYCLE/NONFINITE: 의도적 오류 주입에 진단/거부. P0-COMMAND-IDEMPOTENCY/REJECT-ATOMIC/SNAPSHOT-IMMUTABILITY: 재전달 event1, stale/미구현 명령 거부, 반환값 수정의 내부 영향0. AT-SAVE-02.Domain: 동일 JSON 복구. AT-SAVE-01.File: 중단쓰기/previous 파일/손상 감지. AT-SAVE-04.Domain: map mismatch/없는 room FK 거부. 모두 PASS이며 Unity 실행을 대체하지 않습니다.

## 원본 검수와 완료 경계

`AcceptanceExecution.csv`는 원본 61개 항목에 이번 실행 상태를 대응합니다. 일부 코드 테스트를 통과했다고 상위 AT 전체를 PASS로 올리지 않았습니다. AT-SPACE-01의 ID/import 범위 외 전체 검수는 NOT RUN이며, 실행한 하위 검사명은 별도 열에 남깁니다.

미실행: 17 NPC 공통 물리·인지, FixtureK 사건 실제 실행, B/C 전달, unspoken testimony와 partial claim, 전체 UI/한국어, 모든18침실/재판석 실제 사용, save의 객체 소지/예약·사건·재판·정산·루프 section, 장기18인 병목, 기기 성능 예산, 완성 아트와 모델 승인.
