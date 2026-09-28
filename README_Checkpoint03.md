# BASSLINE 구현 체크포인트 03

2026-09-18 · Unity 6000.6.0f1 · URP 17.6.0 · TestOnly / FunctionalProxy

마우스를 움직여 시점을 바꾸고 E로 상호작용할 수 있습니다. 생활 중 대화·약속·관측한 기록을 NOTE에서 읽고, 실제 물건을 조사해 가설을 만들며, 이 상태를 함께 저장·복구합니다. 이 빌드는 검증 가능한 개발 중간본이며 상용 완성 게임은 아닙니다.

## 실행과 조작

`Builds/Windows/BASSLINE_FixtureK.exe`를 실행합니다. 배포 ZIP은 폴더 전체를 풀어야 합니다. Windows와 맑은 고딕 환경이 필요합니다. OS 글꼴 파일을 재배포하지 않습니다.

| 입력 | 기능 |
| --- | --- |
| 마우스 이동 | 버튼을 누르지 않고 시점 회전. 생활 씬과 Main Hall에 적용 |
| WASD | 이동 |
| E | 바라보는 가까운 물건 줍기·문 열기·NPC 대화. 소지품이 있으면 NPC에게 전달 |
| R | 대상의 자세한 행동 메뉴. 물건 관찰, 문의 잠금 전환 |
| G | 소지품 내려놓기 |
| N | NOTE. 세계와 진행 중 조사를 정지 |
| M | 마지막 확인 위치를 표시하는 지도 |
| Esc | 이전 화면, 탐색에서는 일시정지 메뉴. 커서 잠금 해제 |
| F5 / F9 | 빠른 저장 / 불러오기 |
| Tab / Enter | 열린 화면의 버튼 이동 / 선택 |
| Shift+F1 / Shift+F2 | 생활 시험 씬 새 세션 / Main Hall ArtReview |

F는 재판 FOCUS용 예약 키입니다. 이번 생활 빌드에서 임의로 재판을 시작시키지 않습니다. Main Hall은 보행·아트 검토 씬이며 생활 상호작용은 FixtureK_Life에서 실행합니다. 씬 전환은 새 세션이므로 진행을 보존하려면 먼저 저장하세요.

## 재현할 수 있는 흐름

1. 시작 방의 책·컵 가까이 가서 바라보고 E로 줍습니다. NPC 가까이에서 E를 누르면 1초 전달이 진행됩니다. G로 내려놓을 수 있습니다.
2. 빈손으로 NPC에게 E를 눌러 생활 대화를 합니다. 약속 제안을 선택하면 5분 뒤 홀에서 만날 약속을 수신·수락합니다. NPC는 자신이 받은 약속에 따라 실제로 이동합니다. 현재 동의 정책과 관계 변화량은 FixtureK 시험 기본값입니다.
3. N → 기록·인물·지도·일정에서 자신이 확인하거나 전달받은 내용만 읽습니다. 지도에 다른 NPC의 실시간 위치는 없습니다. 방금 본 위치가 이후 위치를 증명하지 않습니다.
4. 물건이나 문을 바라보고 R → 관찰하기. 2초 동안 실제 시야와 거리를 유지해야 합니다. N을 열면 조사도 멈추며 닫으면 이어집니다.
5. 조사 완료 화면에서 `What this supports`와 `What this does NOT establish`를 따로 읽습니다. 연결에서 관측 구간 안/밖의 주장을 검토하고, 가설을 추가·유보·철회할 수 있습니다.
6. 조사 중이나 NOTE·설정이 겹친 상태에서 저장한 뒤 불러옵니다. 세계뿐 아니라 개인 기억, 수신한 약속 revision, 가설, 조사 진행, 화면 복귀 경로와 선택한 기록 ID를 복구합니다.
7. Esc → 설정에서 감도·Y 반전·E/N 재지정, 접근성에서 글자 100–200%와 모션 감소 설정을 확인합니다. 긴 본문은 스크롤합니다.

저장: `Application.persistentDataPath/TestOnly/FixtureK/session-slot-v1.dat`. 이전 파일은 `.previous`에 보존합니다. checkpoint02의 `life-slot.dat`은 별도로 보존하며 새 세션으로 소급 변환하지 않습니다.

## Unity 프로젝트

이 PC에서는 `C:/Users/리오/OneDrive/Desktop/BASSLINE`으로 엽니다. 이 짧은 경로는 현재 출력 프로젝트를 가리키는 디렉터리 연결입니다. 다른 PC에서는 소스 ZIP을 짧은 경로에 풀고 Unity 6000.6.0f1로 엽니다. `Packages`의 manifest/lock이 포함되어 있습니다.

- 생활 씬: `Assets/BASSLINE/Scenes/Tests/FixtureK_Life.unity`
- 홀: `Assets/BASSLINE/Scenes/Mansion_Main.unity`
- 생성: `BASSLINE → Production`. Scene/Prefab/SO는 Editor API로 생성되어 포함됩니다.
- 시험: Test Runner의 `BASSLINE.Tests.EditMode`, `BASSLINE.Tests.PlayMode` 또는 Editor를 닫고 짧은 경로에서 `RunVerification.ps1`
- UI 정의 41개와 prefab 41개는 생성됐지만 전체 41화면의 게임 흐름 완료를 뜻하지 않습니다.

재판의 발화 공개·3개 주장·FOCUS·부분 반박·출처 확인·증언 요청·투표 잠금은 별도 순수 컴포넌트 시험으로 검증했습니다. 실제 사건 실행(P7), 현장 변경(P8), 집결과 UI를 잇는 P9 플레이는 아직 연결하지 않았습니다. 이 구분은 [ImplementationStatus.md](ImplementationStatus.md)와 [검수 기록](Verification/TestCoverage.md)에 있습니다.

18명 정식 모델·rig/IK·활동 애니메이션, 저택 전체 완성 아트, 18신·성장·정산·루프·승인된 본편 콘텐츠와 장기 플레이테스트는 남아 있습니다. BREAK는 지정된 세 인물만 허용하고 승인 원화가 없으면 재생하지 않습니다. 원본 문서·미승인 외형·엔딩·유스티의 비밀·R07·C01–C08을 임의 확정하지 않았습니다.

검증 결과: Unity EditMode 26/26, PlayMode 15/15, .NET 16/16 PASS. Windows build 오류 0건, 실제 실행본 마우스/E·조사·개인 기록 포함 세션/화면 복구·URP·홀 로드 PASS. 숨김 창의 standalone screenshot은 실패해 이미지 검수 PASS로 계산하지 않았습니다.
