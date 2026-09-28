# checkpoint04 — 실제 실행 검수 범위

2026-09-19. 아래는 전체 기획서 검수의 **구현된 부분 시험**입니다. P7/P8 전체, 본편 재판 통합 또는 상용 품질 PASS가 아닙니다. 최종 집계와 revision은 checkpoint04-final-summary.json, 각 입력/기대/실제 결과는 Checkpoint04_ExecutedTests.csv 및 NUnit JSON을 따릅니다.

환경: Unity 6000.6.0f1 / URP17.6.0 / uGUI/TMP2.6.0 / Test Framework1.8.0 / seed0 / FixtureK_Life_004 / FixtureK_P1_004 + FixtureK_Contact_001. 사용 GPU는 실행본 JSON에 기록됩니다. .NET 10.0.12 검사는 Unity 실행과 별도입니다.

## 새 검수의 입력과 기대

| 검수 연결 / 실행 테스트 | 입력 | 기대 결과 |
| --- | --- | --- |
| AT-CASE-01 / PlayerTakesTool | 시험 시작 시 합법적인 작업실 위치에서 플레이어가 실제 Pickup 명령으로 O31을 가져감. 15:06 이후까지 실제 tick 진행 | 원래 행위자는 빈 공간에서 도구를 만들지 못함. Cause/Trace/Result 없음. 숨은 예방 성공 기록 없음 |
| AT-CASE-01 + AT-TIME-02 + AT-SAVE-02 / ContactWindow | 실제 접촉 진행 15/30tick에서 NOTE 정지·저장. 정상 재개와 저장 복원 후 물리 장벽 배치를 비교 | 정지 중 진행 없음. 동일 접촉 cursor 복구. 장벽으로 실제 접촉/시야를 끊으면 진행 0, Cause/Trace 없음 |
| AT-CASE-02 / ActualContactDelayedResult | 원문 배역의 축약 14:59 setup에서 실제 이동/문/관측/줍기/접촉 실행, 15:02:07.5에 저장, 15:02:10까지 재개/복구 비교 | Cause43500, Result43800, 300tick 간격, 도구 소유와 문 통과 실제 이벤트, cap1, 쓰러진 인물은 활동 불가. 서윤/V1 연속 관측. 도서실 플레이어와 바깥 민서에게 원격 원인 지식 없음. 15:02:30 이후 남문으로 실제 퇴장하여 홀 도착 |
| AT-INFO-01 / OccludedHumanWitness | 사람 시야에 실제 BoxCollider 장벽, 사전 설치 V1에는 없는 위치 | 사람은 사건 행위/인과 기록을 얻지 못함. V1은 실제 자기 시야에 들어온 기록 유지. 사람에게 자동 복사하지 않음 |
| AT-CASE-03 + AT-UI-01 + AT-SAVE-02 / ActualReader | 실제 사건 후 검사 위치를 TestOnly setup으로 배치. V1 E 조사 60tick → NOTE → 저장/복구 → 60tick. 재열람, P31와 쓰러진 인물 E 조사, 근접 서윤 대화 | 중첩 pause/조사 복구, 실제 범위/FOV/시간 비용 검사. 같은 V1 반복 열람 중복 없음. 흔적은 행위자 신원 비입증. 늦은 상태 조사와 직접 목격을 구분. 실제 받은 서윤 전언만 추가 |
| AT-INFO-03 / ReacquiredRecordingAndForwardedCopy | 동일 매체를 별개 인물이 열람 후 전언. 별도 사람 목격과 비교 | 같은 ProvenanceKey는 LR09 독립 근원 아님. 별도 직접 목격은 다른 근원. 전달 계보의 출처 변조 snapshot 거부 |
| AT-SAVE-04 / RejectsFabricatedResult | Cause 없는 Result, 사망 플래그, 잘못된 목격자, O31 FK를 변조 | 복원 거부. 부분 성공/가짜 사건 복구 없음 |
| AT-SAVE-02 / IncidentSnapshotsDeepCopy | 반환된 설정 수정, 사건 필드 없는 기존 생활 snapshot | 원래 World 설정 불변, 기존 생활 snapshot 정상 로드. Unity inline-null 왕복은 기존 실제 JSON/file 시험에서 함께 검증 |
| AT-CASE-01 / AppointmentDelaysIntent | 자신이 받은 약속이 도래했다고 planner에 전달 | 접근 요청 유예. 실제 북문을 보지 않고 잠금 지식을 만들어 넣지 않음 |

EditMode는 순수 도메인/에디터 검사이며 물리 보행을 입증하지 않습니다. PlayMode 시험은 실제 CharacterController/collider/raycast와 60Hz 물리를 실행합니다. 특정 위치 배치를 사용하는 조사 테스트는 테스트 입력이고 실제 플레이에서 순간이동 기능으로 노출하지 않습니다. 사건의 Cause/Result는 Event를 주입해 만든 것이 아니라 실행 전제와 물리 접촉을 통과해서 생성됩니다.

## 회귀 범위

기존 26개 EditMode와 15개 PlayMode도 함께 실행합니다. ID/FK/단위·문/경계·소유권·atomic save·URP 설정, 실제 문 차폐·잠금 우회·6인 교차·보이는/안 보이는 NPC의 물리 동일성, 생활 활동·전달·이동 복구, 자유 마우스/E·한국어 glyph/200% 스크롤·중첩 NOTE·약속/가설/개인 기억 저장, Trial 순수 컴포넌트의 미발화 감춤/3claim/FOCUS/부분 span/투표를 포함합니다. Trial 컴포넌트의 통과를 사건에서 재판까지 실제 플레이 연결로 확대하지 않습니다.

물리 좌표와 관측 좌표는 1μm 정밀도로 비교합니다. tick/ID/이벤트/소유권/출처/기억/예약/화면은 정확 비교합니다. clock remainder는 IEEE754 hex로 보존합니다. JSON parser의 부동소수점 마지막 자리 차이를 불연속 상태의 차이로 덮어쓰지 않습니다.

## Windows 실행본

- checkpoint04-player-build.json: 실제 Windows64 Development build. 빌드 오류/경고 개수 포함.
- checkpoint04-player-smoke.json: 실제 exe에서 자유 시점·E 줍기·생활 대화/약속·조사·세션/화면 복구·URP·Main Hall 로드.
- checkpoint04-incident-smoke.json: 실제 exe에서 Source 접촉 tick/지연 결과·인원 상태·사람/카메라 관측·원격 누출 없음·중간 저장 복구. Cause/Result 각각 한 번만 생성.
- 숨김 batch 창의 standalone screenshot은 NoteCapture=false이며 이미지 검수 PASS가 아닙니다.
- checkpoint04-incident-record-review.png는 **Editor PlayMode 카메라 렌더 검토**입니다. 실제 생성된 V1 기록을 E로 2초간 열람한 화면입니다. 캡처용으로 Canvas를 임시 ScreenSpaceCamera로 변경하고 플레이어의 검사 위치를 시험 배치했습니다. 이 상태를 씬에 저장하지 않았습니다. 최종 3D 아트 승인이나 standalone 화면 캡처를 대신하지 않습니다.

빌드의 Pipeline runtime-config 경고 1건은 자동화 패키지에서 발생하며 URP 지원 중단 경고가 아닙니다. Unity 6000.6의 일부 FindFirstObjectByType/정렬 API에 대한 기존 obsolete 컴파일 경고는 남아 있습니다. 원자료 검증의 491개 경고에는 승인된 최종 에셋 부재/Proxy 등이 포함되며 에셋 완성으로 계산하지 않습니다.

## NOT RUN / 미완

FixtureK 전체 준비·Orphe·N 출입 로그·신고/확인/공표·보존 잠금, 전체 독립 proof, 일반 사건/현장 변경, 실제 물리 집결부터 재판/판결/전말·정산·루프, 전 M01/18 NPC 장시간 성능, 모든 화면/기기/입력 접근성, 정식 모델/리깅/아트·오디오·사용자 플레이테스트는 미완입니다. C01–C08/미승인 서사와 외형은 확정하지 않았습니다.

실제 최종 검증: Unity EditMode 30/30, PlayMode 20/20, .NET 16/16 PASS. Windows 빌드 오류 0, 경고 1건. 생활/사건 실행본 시험 모두 PASS. 원자료 56개 보존, 생성 자산 399개 무결성 확인. Revision: 0e714e9351379fff4a80fd3a1bacdfce4a0c63abbb94a14cad5426bac48ac69f. 전체 게임/정식 아트/재판 통합은 미완입니다.

