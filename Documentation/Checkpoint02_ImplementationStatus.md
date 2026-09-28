# ImplementationStatus — checkpoint 02

2026-09-18 · 전체 게임 IN PROGRESS. SourcePackage 원문과 결정 상태는 보존했습니다. checkpoint 01 기록은 같은 Documentation 폴더에 별도 보관했습니다.

## 결정과 프로젝트

- [USER CONFIRMED] 더 많은 실제 구현과 render pipeline 전환 요청을 적용했습니다. 기존 사용자 파일과 원본 패키지는 덮어쓰지 않았습니다.
- 사용자가 선택한 Desktop/BASSLINE은 실제 폴더가 없었으므로 현재 구현본의 디렉터리 연결을 만들었습니다. 긴 출력 경로에서 발생한 Unity Mono importer 오류를 피하는 짧은 경로이며 동일 프로젝트입니다.
- [PRODUCTION PROPOSAL / REV07] Unity 6000.6.0f1 유지, URP/Core/ShaderGraph 17.6.0, uGUI/TMP 2.6.0, Test Framework 1.8.0. PackageManager.Client로 설치하고 manifest/lock을 보존했습니다. Gamma와 Legacy Input은 유지했습니다.
- Built-in은 Unity 6.5부터 deprecated이며 즉시 삭제된 것은 아닙니다. 이 구현은 URP로 옮겼습니다. 공식 근거: https://unity.com/topics/render-pipelines-strategy-for-2026
- 기존 5개 Standard 재질의 GUID·색·텍스처·smoothness·metallic을 대조해 URP Lit로 변환했습니다. 5개 Fixture 재질을 추가했습니다. 모든 quality level, camera data, renderer/pipeline asset을 연결했습니다.
- URP import가 실패한 긴 경로의 캐시는 짧은 경로에서 URT shader를 강제 재가져와 복구했습니다. TMP가 가져온 두 shader의 deprecated debug pragma를 현행 구문으로 바꿨습니다. 원래 라이선스 표기는 유지했습니다.

## 경로와 실제 동작

모든 Scripts 경로는 Assets/BASSLINE 아래입니다.

| 경로 | 추가 구현 | 제한 |
| --- | --- | --- |
| Scripts/Core/FixtureContracts.cs | 물리 port, 자기 생활 view, player command/read port | 본편의 완성 B/C read model 아님 |
| Scripts/World/Fixture | 6인·4실 TestOnly layout, 60Hz clock, 1.4m/s 이동, 문 FIFO, 잠금 인지 후 우회, 자리 예약, 도착/활동/완료 구분, 물건·전달·append event·receipt | 전체 P0–P3 서비스 아님. 일정/약속·관계·장기목표 미구현 |
| Scripts/NPC/Brain | 자신의 완료 활동 수와 available 상태만 받는 생활 순환 | 욕구 utility, 장기 달력, 소문, 추리 미구현 |
| Scripts/Bootstrap/FixtureRuntime.cs | 플레이어와 NPC의 공통 CharacterController·collider·door barrier·시야. 실제 양보, 60Hz world tick과 PhysX 동시 진행. snapshot 적용 전 불법 위치 검사 | NavMesh가 아닌 명시적 경유점 graph. 경로는 4실 fixture 한정 |
| Scripts/Save/Serialization/FixtureSaveStore.cs | checksum·temp flush·atomic replace·previous, 경로 진행·문 대기·소지·전달·예약·정지·시점 저장 | BASSLINE_FIXTURE_SAVE/1 전용. 본편 저장 migration 미구현 |
| Scripts/UI/Views/FixtureHud.cs | 작은 시계, 한국어 NOTE, owner별 nested pause, 저장/로드, 공개 조작 안내 | 41화면 전체가 아닌 검증용 1개 HUD/NOTE. Focus·대화·수사·재판 미구현 |
| Scripts/Editor/Authoring | URP 전환/검증, Fixture builder, Windows builder, render capture | 최종 아트 승인과 별도 |
| Scenes/Tests/FixtureK_Life.unity | Source P1636의 6인·4실 기능 fixture, 물리 문 2개, 생활 자리 8개와 통과시험 출구 6개, 책·컵 | V1/N의 사건 기록·인지·X31 실행은 아직 없음. 출구 표식은 의자가 아님 |
| Scenes/Mansion_Main.unity | URP camera, 실내 점광원 3개, 기존 실제 계단·발코니·수로·허브 | FunctionalProxy. 최종 고품질/전 구역/난간 연속성 승인 없음 |
| Environment/Rendering | RP_BASSLINE_URP / RD_BASSLINE_Forward | baked GI/APV 사용 안 함 |
| UI/Fonts/FONT_Korean_System.asset | 미사용 초기 preview 자산. 실제 HUD는 OS family에서 독립 DynamicOS font/atlas 생성 | Windows OS font 의존. font 바이너리 미배포, 다른 OS 미검증 |
| Builds/Windows | Editor API Windows development build | 본편 배포판 아님. 실행 검증 상태는 player-smoke.json 참조 |

## Source FixtureK와 시간

Source P1636의 거리는 통행 가능한 anchor 사이이며 실내 동작·문 대기는 별도입니다. K_GATE_H↔K_N 북문 경로 42m, K_GATE_H↔K_S 남문 경로 56m, H↔L 통로 28m, H↔G 통로 56m를 유지했습니다. 방 내부·문 접근 local leg는 별도입니다. M01의 인접 방에 이 거리를 적용하지 않았습니다.

CSV에는 각 graph edge의 길이와 source 연결을 기록했습니다. NPC의 실제 시간에는 실내 이동·회피·문 대기가 더해집니다. 명목 경로 길이를 실제 모든 통행의 0.2초 오차 검수 PASS로 확대하지 않습니다. 사건·능력·배역은 이 생활 시험에 강제하지 않았습니다.

활동 12종의 시간과 반복 순서는 가역적 시험 기본값입니다. 현재 routine은 읽기·작업·휴식·마시기의 단순 activity timer를 사용합니다. 나머지 template와 ACT_PASSAGE 시험용 1tick 동작은 완성 애니메이션이 아닙니다.

## 단계 상태

| 단계 | 상태 | 다음 필요한 작업 |
| --- | --- | --- |
| Bootstrap / importer | PARTIAL · 실행 subset PASS | typed 본편 definitions, 전체 input/권한 구성, assets/socket 해결 |
| Main Hall | PARTIAL · 보행 subset PASS | 최종 아트, 전체 rail/nav/vision·성능·승인 |
| Authority / P0 | PARTIAL · fixture 저장·물건·문 실행 | 본편 registry/권한/section·migration 통합 |
| P1 이동 생활 | PARTIAL · 실물 이동/문/생활 시험 | 최소 약속·달력, 더 넓은 경합과 장기 테스트 |
| P2 자연 행동 | PARTIAL · 전달·양보 subset | 체형별 정식 rig/IK, 접촉 반응, 그룹 준비, 활동 animation |
| P3 관계 일정 | NOT IMPLEMENTED | 관계·태그·장기목표·예약 시간창·초대·소문 |
| P4–P8 | NOT IMPLEMENTED | 인지/B/C·수사·추리·실제 사건·현장 변경 |
| 41화면 full flow | NOT IMPLEMENTED | Fixture NOTE 이외 화면 연결 |
| 18인 Characters / M01 확장 | NOT IMPLEMENTED | 승인 모델, 18 침실·재판석 실체 binding, ENV02–13 |
| P9–P13 | NOT IMPLEMENTED | 재판·성장·정산·루프·18신·승인 콘텐츠 |
| BREAK | CONTROLLER PASS / ART NOT PROVIDED | 세 인물 승인 원화와 dialogue/court 연결 |

검증 결과는 Verification/TestCoverage.md와 실행 원장을 따릅니다. 테스트 코드의 존재를 실행 PASS로 쓰지 않습니다. 물리 좌표의 저장 복구는 최대 1µm 허용오차, discrete 상태·이벤트·tick은 정확 비교합니다. bit-exact PhysX 재현을 주장하지 않습니다.

유스티는 PRES_YUSTI presentation 대상이며 19번째 참가자가 아닙니다. C01–C08은 OFF, R07·엔딩·저택/유스티 비밀·의수 좌우·미승인 의상/외형은 확정하지 않았습니다. Archive를 현재 NPC 지식에 복사하지 않습니다.


Windows 빌드 오류 0. 재빌드 중 읽기 전용으로 남은 Temp/BurstOutput 폴더 때문에 실패했던 회차는 해당 생성 캐시의 ReadOnly 속성을 해제하고 검증용 Editor를 재시작해 복구했습니다. 원본 Assets와 SourcePackage의 권한/내용은 변경하지 않았습니다.

