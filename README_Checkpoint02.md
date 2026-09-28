# BASSLINE 구현 체크포인트 02

2026-09-18 · Unity 6000.6.0f1 · **URP 17.6.0** · 기능 검증용 TestOnly / Proxy

FixtureK 6인 4실의 실제 이동·문 통과·생활·물건 전달·저장과 한국어 NOTE를 실행할 수 있습니다. 본편 18인·41화면·사건·재판·최종 아트는 아직 완료하지 않았습니다.

## 바로 실행

`Builds/Windows/BASSLINE_FixtureK.exe`를 실행하세요. 같은 폴더의 `_Data`, `MonoBleedingEdge`, DLL도 필요합니다. Windows와 맑은 고딕이 설치된 환경을 대상으로 합니다. OS font 파일은 재배포하지 않습니다.

| 조작 | 기능 |
| --- | --- |
| WASD / 오른쪽 마우스 드래그 | 1인칭 이동 / 시점 |
| E | 가까운 문 열기, 책·컵 줍기, 소지한 물건을 가까운 NPC에게 전달 |
| L / G | 가까운 문 잠금 전환 / 물건 내려놓기 |
| Tab | NOTE 열기·닫기. 읽는 동안 세계 정지 |
| NOTE에서 Esc | 중첩 정지 시험. NOTE를 닫아도 설정 정지는 유지되며 Esc로 해제 |
| F5 / F9 | 저장 / 불러오기. NPC 경로·활동·물건·문·예약·시점·pause 복구 |
| Shift+F1 | FixtureK를 새 세션으로 시작 |
| Shift+F2 | Main Hall ArtReview로 이동 |

씬 전환은 새 TestOnly 세션입니다. 보존할 생활 진행은 먼저 F5로 저장하세요. 저장 위치는 `Application.persistentDataPath/TestOnly/FixtureK/life-slot.dat`이며 이전 파일은 `.previous`로 남깁니다. 일반 탐색 중에는 작은 시계만 표시합니다.

## Unity에서 열기

이 PC의 `C:/Users/리오/OneDrive/Desktop/BASSLINE`은 이 구현본을 가리키는 디렉터리 연결입니다. 선택한 경로에 기존 폴더가 없음을 확인한 뒤 생성했습니다. 출력 폴더와 별도 복사본이 아닙니다. Hub에는 이 **짧은 경로**로 추가하세요. 긴 출력 경로로 열면 일부 Unity package importer가 Windows 경로 길이 제한에 걸릴 수 있습니다.

다른 PC에서는 소스 ZIP을 짧은 경로에 풀고 Unity 6000.6.0f1로 엽니다. `Packages/manifest.json`과 `packages-lock.json`에 실제 해결한 패키지를 포함했습니다. 최초에는 Unity 라이선스와 패키지 복원이 필요할 수 있습니다.

- 생활 시험: `Assets/BASSLINE/Scenes/Tests/FixtureK_Life.unity`
- 홀 검토: `Assets/BASSLINE/Scenes/Mansion_Main.unity`
- Editor 생성 메뉴: `BASSLINE → Production`. Scene/Prefab/SO는 Unity API로 생성됐으며 이미 포함되어 있습니다.

## 구현 범위

- Built-in에서 URP로 전환. 모든 Quality level, 카메라, 재질 10개를 연결했습니다. 긴 경로에서 실패한 URP 베이크 리소스 import도 복구했습니다.
- FixtureK: 플레이어 1명과 자율 NPC 5명. 4개 방, 북·남 두 물리 문, 통행 대기·양보·잠금 우회, 활동 자리 예약, 도착 후 별도 활동 시간.
- 책·컵의 단일 소지, 실제 거리·시야 확인, 1초 전달 절차와 중간 저장·복구. 모델과 손 위치는 Proxy이며 정식 rig/IK/활동 animation은 미완입니다.
- 한국어 NOTE와 중첩 pause. UI와 NPC assembly는 World/Save/Bootstrap을 참조하지 않고 자기에게 허용된 계약만 사용합니다. 전체 B/C·인지 시스템 완료를 뜻하지 않습니다.
- Main Hall은 URP 실내 조명을 추가한 26×22×11m 기능 Proxy입니다. 최종 아트 승인을 받지 않았습니다.

최신 실제 실행 수치와 실패 수정 이력은 [검수 기록](Verification/TestCoverage.md), 단계별 남은 작업은 [구현 상태](ImplementationStatus.md)를 확인하세요. 이전 checkpoint 01은 별도 ZIP과 Documentation에 보존했습니다.

원자료 `SourcePackage`는 변경하지 않았습니다. 메인 엔딩, R07, 유스티의 비밀, 미승인 외형과 의상, C01–C08을 확정하지 않았습니다. BREAK는 김진우·유스티·차도윤만 허용하며 승인 원화가 없으면 재생을 거부합니다.

## 재현

Editor를 닫고 짧은 프로젝트 경로에서 `RunVerification.ps1`을 실행하거나 Unity Test Runner의 `BASSLINE.Tests.EditMode`와 `BASSLINE.Tests.PlayMode`를 실행합니다. 빌드는 `BASSLINE/Production/6 Build Windows FixtureK checkpoint`입니다.

생성 자산을 수동 수정하면 importer는 덮어쓰지 않고 충돌을 보고합니다. `.meta`나 manifest 해시를 지워 우회하지 마세요. 실제 ID/경로/GUID/local ID는 `Verification/GeneratedBindingManifest.csv`에 있습니다.

검증 완료: Unity EditMode 15/15, PlayMode 11/11, .NET 16/16 PASS. Windows build 오류 0, 독립 실행본 저장/정지/URP/홀 전환 PASS. 숨김 창의 backbuffer screenshot은 실패했으므로 실행본 이미지 검수는 PASS로 표시하지 않습니다.

