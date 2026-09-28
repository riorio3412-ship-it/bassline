# 실제 실행 검증 — checkpoint 02

Unity 6000.6.0f1 / URP 17.6.0 / uGUI 2.6.0 / Test Framework 1.8.0. 게임 seed 0, RNG 없음. Fixture Map=FixtureK_Life_004, RuleSet=FixtureK_P1_004, Schema=BASSLINE_FIXTURE_SAVE/1. 하드웨어와 최종 코드 SHA-256은 engine-environment.txt, build-revision.txt를 따릅니다.

## 결과

| 실행 | 실제 결과 | 증거 |
| --- | --- | --- |
| Unity EditMode | 15/15 PASS | checkpoint02-editmode-final.json |
| Unity PlayMode | 11/11 PASS | checkpoint02-playmode-final.json |
| .NET 로직·CSV | 16/16 PASS, Unity와 별도 | checkpoint02-domain-tests.txt |
| Windows build | Succeeded · errors 0 · warnings 1 | checkpoint02-player-build.json |
| 독립 Windows 실행본 | PASS · URP/저장/정지/홀 로드; player 스크린샷 캡처 실패(별도) | checkpoint02-player-smoke.json |
| Main Hall URP render | 실제 PNG 확인, Proxy 품질 | MainHall_URP.png |
| 본편 전체 검수·최종 아트·18인·41화면 | NOT RUN | ImplementationStatus.md |

테스트 코드는 Assets/BASSLINE/Tests/EditMode 및 PlayMode에 있으며, 아래 입력·기대 조건을 실제 NUnit assertion으로 검사했습니다. 초기 실패 결과와 최종 결과를 합산하지 않습니다.

## 이번에 추가한 검사

| 검수 연결 | 입력 | 기대 / 실행 범위 |
| --- | --- | --- |
| URP | quality level 전부·환경 material 검사 | 같은 Universal pipeline와 URP Lit. Camera와 renderer 검사/렌더는 별도 urp-validation.txt |
| AT-NAV-01 | 좁은 북문 양쪽 3명씩 동시에 passage 목표 요청. 1200tick 관측, 프레임당 30/120/240tick 각 실행, 프레임당 30/120/240tick 각 실행 | 실제 여섯 통과, request FIFO, capsule 침투 0, 통과 중 닫힘 0. 통과 후 parking 도착은 별도 추가 10초 검사 |
| P1 생활 | 자율 NPC 5명, 최대 240world초 | 다섯 모두 실제 목적지에서 첫 활동 완료. 한 tick 이동 0.06m 미만, 예약 자리 중복 0 |
| AT-NAV-02 subset | 북문을 처음부터 잠근 뒤 작업실 목표 | 직접 북문까지 가서 잠금을 발견, 남문으로 실제 우회, 북문 통과 없음. 이동 중 잠금/17인 점유 전체는 NOT RUN |
| AT-NAV-03 subset | 같은 시작 상태, 인물 renderer ON/OFF, 프레임당 처리 tick 30/240 | 동일 discrete event 순서·tick과 활동 완료. 전체 M01·18인·플랫폼 간 bit-exact 주장 없음 |
| AT-SAVE-02 / P2 subset | 책 pickup → 60tick 전달 중 30tick에 저장·로드 | 소유권 한 번만 이동, duplicate command는 재지급하지 않음, held collider 비활성·hand proxy 위치 일치 |
| AT-SAVE-02 | 이동 300tick 시점 저장, 다른 상태로 진행 후 로드 | 물리 좌표 최대 1µm 차이, 그 밖의 전체 snapshot 정확 비교, 다음 tick 정상 진행 |
| AT-SAVE-01/04 subset | atomic replace 직전 실패, checksum 손상, 잘못된 FK·이중 소지·NaN·벽/문 내부 위치 | 기존 save 보존 또는 명시적 거부. 실패 load는 현재 world를 변경하지 않음 |
| AT-TIME-02 / UI subset | NOTE+설정 owner, 120tick 시도, 저장·복원 후 owner별 해제 | world tick 정지, 하나를 닫아도 다른 정지 유지. UI panel 상태 복원 |
| 한국어 | 이름·장문 한국어·영문·숫자 500자 이상 | runtime DynamicOS font missing glyph 없음, 실제 TMP mesh 생성 |
| AT-INFO subset | 닫힌/열린 문 사이 시야, assembly 참조 그래프 | 같은 collider가 시야 차단. UI/NPC/Presentation은 World/Save/Bootstrap 미참조. 완성 B/C·LogicResolver 정보보안 검수는 NOT RUN |

기존 CSV/import idempotency/manual conflict, Main Hall F1→F2 실물 보행, bootstrap 저장·pause, BREAK allowlist 3명 검사도 유지했습니다. 모서리 경유점과 도어 접근은 실제 CharacterController로 실행하며 test double 물리는 domain 검사에서만 사용합니다.

## 실패를 통해 수정한 사항

- 경유점의 직각 모서리가 벽 경계와 겹쳐 회전이 막힘: turn square를 확보했습니다.
- 여러 NPC가 정확히 같은 점에 모여 정체: 경유점 도착 반경과 불필요한 원점 재방문을 고쳤습니다.
- 문을 통과한 뒤 같은 queue에 재등록됨: 진입 방향과 이탈 조건을 저장했습니다.
- 일반 회피가 door queue/좁은 opening과 충돌함: 대기 자리·문 중앙 접근·이탈 단계를 분리하고 중앙 정렬 완료 상태를 저장했습니다.
- 초기 통과 시험의 parking 지점이 대기열을 막음: 출구 목표를 통로 끝으로 옮겼습니다. 20초 통과 조건은 유지했습니다.
- Korean TMP glyph 없음/원본 hash 변화: 필수 TMP resources를 가져왔고, 실행 font를 OS family 설정에서 독립 생성하도록 바꿨습니다. 초기 font preview 자산은 미사용 상태로 보존했습니다.
- 긴 경로에서 URP URT importer가 실패함: Desktop의 짧은 경로에서 관련 resource를 재import했습니다.
- 실행본 JsonUtility가 double 좌표의 마지막 자릿수를 반올림함: 물리 좌표만 1µm 오차를 허용하고 이벤트·tick·소지·예약 등 discrete 상태의 정확 비교는 유지했습니다.

Main Hall 이미지/정적 장면 검토를 최종 아트 승인으로 쓰지 않습니다. 활동 타이머·hand proxy를 정식 animation/IK라고 부르지 않습니다. 경로의 명목 42/56/28/56m와 실내 이동·대기·회피 시간을 혼동하지 않습니다.


## 화면 검토

FixtureK_NOTE_CameraReview.png는 실제 PlayMode의 한국어 NOTE를 Camera RenderTexture로 렌더한 검토 이미지입니다. headless backbuffer가 검게 나오는 환경이라 캡처할 때만 Canvas를 ScreenSpaceCamera로 임시 전환했습니다. Scene/실행본은 기존 Overlay 설정을 유지합니다. 1280×720에서 문장·조작 안내가 잘리지 않고 한국어가 표시됨을 확인했습니다. 실제 Windows 숨김 창 screenshot은 실패했고, 카메라 검토를 해당 캡처 성공으로 대신 기록하지 않습니다.


- 물리 쿼리 지연 수정: 렌더 프레임과 분리된 60Hz PhysX 수동 시뮬레이션을 world tick에 연결했습니다. 세 batch 모두 6인 최종 통과 tick 956 (15.933초). 원본 컴파일/시험 실패 로그는 이력으로 보존하며 최종 PASS와 구분합니다.
- Windows build의 유일한 warning은 개발 자동화 Pipeline 플러그인의 runtime server config가 없어 player에서 비활성화된다는 안내입니다. URP 렌더 파이프라인과 별개이며 게임 플레이에는 필요하지 않습니다.


