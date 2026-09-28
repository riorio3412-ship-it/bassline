# 실제 실행 검증 — checkpoint 03

Unity 6000.6.0f1 / URP 17.6.0 / uGUI 2.6.0 / Test Framework 1.8.0. Seed=0. Map=FixtureK_Life_004 및 M01. Rule=FixtureK_P1_004 / Bootstrap_001_REV10 / Trial_001(컴포넌트 TestOnly). 생활 저장은 BASSLINE_FIXTURE_SESSION/1이며 시계 잔여값을 IEEE-754 hex로 추가 보존합니다.

코드 revision과 장치 환경은 build-revision.txt / engine-environment.txt에 기록합니다. 테스트의 입력·기대·실제와 실행 시간은 Checkpoint03_ExecutedTests.csv 및 각 NUnit 테스트의 assertion/로그를 따릅니다.

| 실행 | 최종 증거 | 해석 |
| --- | --- | --- |
| Unity EditMode | checkpoint03-editmode-final.json | 26/26 PASS · 컴포넌트/도메인/자산 검사 |
| Unity PlayMode | checkpoint03-playmode-final.json | 15/15 PASS · 실제 씬/물리/UI 검사 |
| .NET | checkpoint03-domain-tests.txt | 16/16 PASS · 순수 로직/CSV 검사. Unity 실행과 별도 |
| Windows build | checkpoint03-player-build.json | Succeeded · errors0 · warnings1 |
| 독립 Windows 실행 | checkpoint03-player-smoke.json | PASS · 실제 .exe에서 마우스 명령·줍기·조사·세션/화면 복구·URP·홀 로드 |
| UI 이미지 | Checkpoint03_NOTE_CameraReview.png | Editor Play에서 UI를 camera canvas로 일시 전환해 캡처, 본문/버튼 한국어 육안 확인. standalone 화면 캡처를 뜻하지 않음 |
| 전체 상용 게임 / P7–P13 통합 | NOT RUN | 승인 아트·18인·실제 사건·전말·성장·정산·루프 전체 미완 |

## 이번에 실행한 입력과 기대

| 검수 연결 | 입력 | 기대 / 실제 검사 |
| --- | --- | --- |
| 입력 추가 검사 | 우클릭 상태 없이 mouse delta, NOTE 중 delta, E에 연결된 Primary pickup | 탐색만 yaw/pitch 변화, NOTE 중 회전 없음, 책 단일 소지. Hall도 같은 시점/정지 확인. 실제 OS 기기 사용자 테스트는 아님 |
| AT-INFO-01/02 | 북문 양편에 두 인물, 문 닫음→열음→시선 돌림 | 닫혀 있으면 얼굴 기억 없음, 열린 뒤 실제 관측만 기록, 이후 마지막 확인 tick 유지 |
| AT-INFO-02 | CH01 관측→CH02 전달→CH03 전언, 중복 전달 | 비수신 인물에게 기록 없음, 부모/근원 유지, 사본을 독립 근원으로 집계하지 않음 |
| P3 / AT-NPC subset | 초대→실제 수신→수락→수락 응답, organizer만 revision2 변경 | 수락 전/응답 수신 전 정보가 새지 않음, invitee는 미수신 revision2를 알지 못함, snapshot 동일 |
| P5 / AT-TIME-02 | 실제 대상 검사 120tick 중 60tick에 NOTE와 저장 | NOTE 동안 60 유지, 화면/진행 복원 후 나머지 60tick으로 완료. 완료 UI는 같은 tick의 다른 관측 대신 해당 RecordID 선택 |
| P6 / AT-INFO-03 | 같은 관측으로 범위 안/밖/다른 위치 claim 검토 | Support/LimitScope/해당 span Contradict, 관련 없는 span 유지, 미수신/미등록 규칙 거부. 완성 LR01–10 검수는 아님 |
| P6 | 소유자별 가설·유보, record→record 링크 역방향 | 미수신 근거 거부, cycle 거부, graph/가설 restore 동일 |
| AT-SAVE-02 | 조사·개인 기억·약속·가설·NOTE/설정이 겹친 세션 저장/로드 | 전체 section·선택 stable ID·복귀 화면 동일, 각 pause owner만 해제 |
| AT-SAVE-02 | 독립 실행본에서 관측한 JSON 시간 소수부 1bit 반올림 재현 | 저장한 IEEE-754 bit에서 정확한 world accumulator 복원, NaN encoding 거부 |
| AT-UI-01 | 한국어 1,000자 이상, body font200%, Canvas layout 실행 | 실제 TMP mesh 생성, content 높이>viewport, 스크롤 가능. 모든 버튼/제목/화면의 200% 전수 검수는 아님 |
| AT-UI-02 / P9 component | 한 명 미집결, 미발화 queue, 글자별 발화, 4개 claim | gate 거부, 미발화 전문/claim 비공개, 수신한 완성 발언만 공개, 주요3개/4번째 대기 |
| FOCUS component | 현재 발언 중 FOCUS+NOTE, 저장/복원·각 pause 해제 | CourtTick와 voice cursor 정지·복귀, 마지막 소유자 해제 후만 진행 |
| FOCUS / AT-INFO-03 | Rebut/Support/LimitScope, 출처 확인, 증언 요청, 중복 제출 | LogicResult만 적용, unsupported span만 변경. 요청만으로 답변이 생기지 않음. 다른 인물은 결과 수신 후에만 상태 변경 |
| AT-SAVE-03 component | 4명 중 1명 투표 후 조회, 재선택, 나머지 확정 | 숨은 표 미공개, 중복/변경 방지, 전체 잠금 후 공개, snapshot immutable. 판결이나 P9 실제 게임 흐름 검사가 아님 |

기존 6인 FIFO 문 통과(30/120/240 tick batch), renderer 가시/비가시 동일 물리, 잠긴 북문 우회, 5 NPC 생활, 물건 전달 중간 복구, Main Hall 실제 계단/발코니 보행, importer idempotency/수동 수정 충돌과 BREAK 3인 allowlist 검사를 유지했습니다.

## 실패 수정 이력과 제한

- attempt01: 화면 복원에서 기존 pause owner를 다시 acquire. desired state 설정을 idempotent하게 만들어 수정했습니다.
- attempt02: NOTE 탭 교체가 조사 UI_14까지 pop. NOTE 소유 화면 범위에서 UI_14를 제외했습니다. 기존 폰트 mesh 테스트는 가려진 NOTE에 ForceMeshUpdate를 요청해 무시되던 문제여서 inactive 강제 생성 옵션을 적용했습니다. 실제 활성 200% 화면 검사는 별도로 유지합니다.
- 독립 실행본: JSON 파싱 중 world PendingTime 최하위 bit가 반올림돼 엄격 비교 실패. ClockRemainderHex를 저장해 정확한 accumulator를 복구합니다. 허용오차를 늘려 통과시키지 않았습니다. 물리 위치에 대한 기존 1µm 허용오차는 별개입니다.
- standalone screenshot 성공 여부는 smoke JSON의 NoteCapture를 그대로 따릅니다. 숨김/batch 창 backbuffer 캡처 실패를 렌더 검수 PASS로 표시하지 않습니다.
- build warning은 개발용 Unity automation Pipeline 플러그인의 runtime config 부재 안내입니다. URP 렌더 파이프라인과 별개이며 production game 기능에 필요하지 않습니다.
- P9 테스트의 입장/수신 callback과 지식 주입은 TestOnly입니다. 실제 P7 사건, 센서 매체, 실제 집결·UI 연결을 테스트한 것으로 확대하지 않습니다.
- Assembly graph에서 UI/NPC/Knowledge/Investigation/Trial → World/Save/Bootstrap 의존성이 없음을 검사했습니다. 만능 정보 보안 인증이나 전체 인지 검수는 아닙니다.

본편 각 AT 검수의 전체 완료 상태는 AcceptanceExecution.csv에 별도 기록합니다. 컴포넌트 PASS의 합으로 전체 게임 완료를 선언하지 않습니다.

