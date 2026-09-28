# 다음 작업 세션 안내 (NextSession)

## 빠르게 상태 파악하기
1. `Documentation/BL23/ImplementationStatus.md` → 무엇이 있고 무엇이 미완인지
2. `Documentation/BL23/RequirementTraceability.csv` → 인수 항목 AT-001~070별 상태
3. `Documentation/BL23/TestReport.md` → 마지막 검증 결과

## 검증 명령
```
# 커널(유니티 없이) — 레이아웃 30개, 캠페인 12일
cd Tests/BL23/SimTests
dotnet build -c Release
dotnet bin/Release/net10.0/SimTests.dll layout 30
dotnet bin/Release/net10.0/SimTests.dll campaign 20260926 12
dotnet bin/Release/net10.0/SimTests.dll campaign 4242 6 force CH20     # 특정 챕터 규칙 강제

# 게임 레이어 컴파일만 빠르게
cd Tests/BL23/GameCompile && dotnet build -c Release

# 유니티 빌드(배치) — 본 프로젝트에서 유니티를 동시에 두 개 열지 말 것
"C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe" -batchmode -projectPath . -executeMethod BL23.EditorTools.Build.BL23Build.CI -bl23BuildOut Builds/BL23_dev -quit -logFile build.log

# 자동 플레이 점검(스크린샷·로그) — 저장은 격리 폴더에 기록됨
Builds/BL23_dev/BASSLINE.exe -screen-fullscreen 0 -bl23probe full -bl23out <폴더>
Builds/BL23_dev/BASSLINE.exe -bl23probe ui -bl23res 1280x720 -bl23uiscale 1.5 -bl23out <폴더>
```

## 남은 일 (우선순위)
1. **사람이 직접 플레이하는 테스트** — 자동 점검은 API로 진행하므로 조작감·난이도·재판의 풀림 여부는 사람 확인이 필요.
2. **절차 생성 인물 16명의 외형 품질** — 머리카락(헬멧형)·손가락·얼굴 디테일을 GLB 3인 수준에 가깝게. 재베이크: `BL23.EditorTools.Characters.CharacterBaker.BakeAll`(약 25분).
3. **성능** — 19명 3D + 저택에서 p95 33 ms. 붐비는 장면(프롤로그·재판) LOD 거리, 그림자 수, 캐릭터 폴리곤(67k–146k) 조정.
4. 해상도·UI 배율 점검 결과 반영(AT-064), 장시간 실행(AT-069), 클린 PC 배포 검증(AT-070).
5. 권능 11종의 범행 쪽 사용 확장(현재 정적·외피·흐림·메아리·고정·무게), CH12·16·17 효과 심화.
6. 36개 문법 조합의 "사람이 풀 수 있는가" 검증과 난이도 조정.
## 주의
- 레거시 v0.31 런타임(`Assets/BASSLINE/Scripts`)은 컴파일만 유지. 빌드 장면은 `Assets/BASSLINE/BL23/Scenes/BL23_Main.unity` 하나.
- 폐기된 2D 스탠딩 스프라이트는 `Characters/Standing/Legacy2D_NotInBuild`로 옮겨 빌드에서 제외(삭제하지 않음).
- 커널 수정 후에는 반드시 헤드리스 캠페인으로 저장 왕복(IDENTICAL)을 확인.
