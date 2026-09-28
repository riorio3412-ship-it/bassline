# 클라우드 세션 재개 안내 (2026-09-28 갱신)

만든 것과 파일 위치는 **[`CloudWorkLog.md`](CloudWorkLog.md)**에 정리되어 있다. 지금 진행 중인 설계는 **[`SocialEventsDesign.md`](SocialEventsDesign.md)**다.

## ★ 사용량이 초기화된 뒤 재개하기 — 복사해서 붙여 넣기만 하면 된다

**가장 쉬운 방법: 클라우드(claude.ai/code)에서 새 세션**
1. claude.ai/code에서 새 세션을 연다.
2. 저장소는 `riorio3412-ship-it/bassline`, 브랜치는 `claude/ecstatic-mccarthy-w08ico`를 고른다.
3. 아래 블록을 그대로 첫 메시지로 보낸다.

**PC의 Claude Code로 할 때**
1. 바탕화면 `BASSLINE` 폴더에서 터미널(PowerShell)을 열고 `claude`를 실행한다.
2. 아래 블록 맨 앞에 이 한 줄을 붙여서 보낸다.
   > 먼저 Documentation/BL23/CLOUD_RESUME.md의 "PC에 반영하기"대로 GitHub 브랜치 claude/ecstatic-mccarthy-w08ico의 작업을 이 폴더에 반영하고, Unity 컴파일 오류가 없는지 확인해 줘. 그다음:

```
BASSLINE(BL23) 작업을 이어서 해 줘. 순서:
1) 읽기: Documentation/BL23/CLOUD_RESUME.md, CloudWorkLog.md, SocialEventsDesign.md,
   HANDOFF.md(§0.5 오너 원문, §1 지시, §3.0000 최신), DecisionLog.md(D-040 이후).
2) 준비: dotnet SDK 10 설치(없으면). Tests/BL23/SimTests에서 dotnet build -c Release 후
   "dotnet run -c Release -- campaign 20260926 6"이 faults=0, roundtrip=IDENTICAL인지 확인.
3) 구현: SocialEventsDesign.md §6의 5단계까지는 끝났다. CloudWorkLog.md §8 "다음에 할 일"과
   "기록"의 마지막 줄들을 보고 이어서 한다.
4) 단계마다 검증: SimTests에서 premise 20260926 9, life, campaign 20260926 6,
   debate 20260926 smart(그리고 4242, 2), "voice lint out.txt"를 돌린다.
   faults=0, 세이브 왕복 IDENTICAL, 보이스 팩 위반 0이어야 한다.
   살인이 바뀌는 작업이면 "firsts 1 40 9 active"로 첫 살인 분포도 본다(한 범인이 35%를 넘지 않게).
5) 단계마다 기록: 통과하면 커밋·푸시(브랜치 claude/ecstatic-mccarthy-w08ico, 기존 커밋 수정 금지).
   CloudWorkLog.md "기록"에 한 줄 추가. 결정은 DecisionLog.md에 D-0xx로 추가.
6) 지킬 것:
   - 커널(Sim)은 결정론(해시·기존 스트림)을 지킨다. 대사는 한 쪽 60자 이하. 인물 말버릇은 CharacterBible §2.
   - 단간론파 고유 명칭·연출은 쓰지 않는다(D-028). 대사 수위 규칙은 HANDOFF §1.4.
   - 기존 코드와 원본 자산을 지우지 않는다. git reset --hard, git clean은 쓰지 않는다.
   - 오너가 보낸 방 그림 48장은 SocialEventsDesign.md §5 카탈로그로 정리되어 있다.
     그림 파일은 오너가 Assets/BASSLINE/BL23/Art/Rooms/에 넣는다.
```

**어디까지 했나 (2026-09-28 기준)**
- §6의 1~3단계(파벌, 저택 행사, 주민 모임 장면)는 끝났다.
- 4단계(심판 연결)도 끝났다: 보물찾기 구역 알리바이·은판, 가면의 밤 은판, 파벌 항의.
- 5단계: 새 방 10종과 방 배경 카드(`Game/UI/RoomCardUI.cs`)까지 했다. 그림 파일 이름표는 SocialEventsDesign §5.
- 덱 개선으로 심판 정답률(40시드) 87.5%.
- 수사·심판 공정성(D-061, D-062): 범인의 거짓말마다 깨는 은판이 덱에 있다(40시드 97.5%). 가만히 있으면 이기지 못한다(16시드 중 3건).
- 첫 살인 다양화(저택의 편지)와 결정성 버그 수정도 했다. 측정 도구는 `firsts`, `staticcheck` 모드다.
- 자세한 진행은 `CloudWorkLog.md` "기록"의 마지막 줄들을 본다.

## 지금 상태
- **저장소:** `riorio3412-ship-it/bassline`
- **작업 브랜치:** `claude/ecstatic-mccarthy-w08ico`
  - `main`에는 오너가 올린 `BASSLINE_1_code.zip`만 있다.
  - 브랜치에는 그 압축을 푼 기준선 커밋 `a946694`와, 그 위의 클라우드 작업 커밋이 있다.
- **지금까지 한 일**
  1. 가구 변화와 사람별 지식
  2. P10 모델 보정
  3. 토론 심판(은판 덱)
  4. 네 인물 성격과 대사 수위
  5. 새 전제: 소원 초대장, 약속, 벽, 저택의 부추김
  6. 식탁 대화 버그 수정
- **남은 일:** `CloudWorkLog.md` §8에 있다.

## 다시 시작하는 방법
1. **같은 클라우드 세션**이면 "이어서 해줘"라고만 보내면 된다.
2. **새 클라우드 세션**이면 이 저장소의 `claude/ecstatic-mccarthy-w08ico` 브랜치로 연다.
   - 첫 메시지(복사해서 쓴다):
     > Documentation/BL23/CLOUD_RESUME.md, CloudWorkLog.md, HANDOFF.md를 읽고, CloudWorkLog.md §8 우선순위대로 이어서 작업해 줘. 작업이 끝날 때마다 커밋·푸시하고 CloudWorkLog.md "기록"에 한 줄 추가해 줘.
   - 커널 테스트 준비:
     ```bash
     apt-get install -y dotnet-sdk-10.0      # 없을 때만
     cd Tests/BL23/SimTests
     dotnet run -c Release -- campaign 20260926 6   # → faults=0, roundtrip=IDENTICAL
     dotnet run -c Release -- premise 20260926 9    # → 약속·견본·저택 공지·첫 살인
     ```
   - 클라우드에는 Unity가 없다. GameCompile(Unity DLL 필요), Unity 빌드, 프로브는 오너 PC에서만 된다.
3. **오너 PC의 Claude Code**로 이어 갈 때는 먼저 아래 "PC에 반영하기"를 한다.
   - 첫 메시지 예:
     > Documentation/BL23/CLOUD_RESUME.md의 "PC에 반영하기"대로 GitHub 브랜치 claude/ecstatic-mccarthy-w08ico의 작업을 바탕화면 BASSLINE 폴더에 반영해 줘. 끝나면 Unity 컴파일 오류가 없는지 확인하고, CloudWorkLog.md §8 우선순위대로 이어서 작업해 줘.
   - PC 폴더가 이 저장소의 git 클론이라면 더 간단하다. `git fetch origin` 후 `git checkout claude/ecstatic-mccarthy-w08ico`로 받거나 main에 머지한다.

## PC에 반영하기 (바뀐 파일만, 덮어쓰기 전 백업)
1. 브라우저에서 GitHub에 로그인한 채로 아래 주소를 연다. `bassline-claude-ecstatic-mccarthy-w08ico.zip`이 내려받기 폴더에 받아진다.
   `https://github.com/riorio3412-ship-it/bassline/archive/refs/heads/claude/ecstatic-mccarthy-w08ico.zip`
2. PowerShell에 아래를 붙여 넣는다.
   - 기준선 이후 바뀐 파일만 BASSLINE 폴더에 복사한다. 새 `.cs` 파일의 `.meta`도 함께 복사한다.
   - 덮어쓰기 전 원본은 `BASSLINE\_cloud_backup_날짜시각\`에 보관한다.
```powershell
& {
  $zip = Join-Path $env:USERPROFILE 'Downloads\bassline-claude-ecstatic-mccarthy-w08ico.zip'
  $proj = @((Join-Path ([Environment]::GetFolderPath('Desktop')) 'BASSLINE'), (Join-Path "$env:OneDrive" 'Desktop\BASSLINE')) | Where-Object { Test-Path $_ } | Select-Object -First 1
  if (-not (Test-Path $zip) -or -not $proj) { Write-Host "zip or BASSLINE folder not found" -ForegroundColor Red; return }
  $files = @(
    'Assets/BASSLINE/BL23/Game/Core/Session.cs',
    'Assets/BASSLINE/BL23/Game/UI/NoteUI.cs',
    'Assets/BASSLINE/BL23/Game/UI/RoomCardUI.cs',
    'Assets/BASSLINE/BL23/Game/UI/RoomCardUI.cs.meta',
    'Assets/BASSLINE/BL23/Game/World/WorldPresenter.cs',
    'Assets/BASSLINE/BL23/Sim/Content/Activities.cs',
    'Assets/BASSLINE/BL23/Sim/Content/LineBank.cs',
    'Assets/BASSLINE/BL23/Sim/Content/Lines_ANY.cs',
    'Assets/BASSLINE/BL23/Sim/Content/Lines_Debate.cs',
    'Assets/BASSLINE/BL23/Sim/Content/Lines_Debate.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Content/Lines_NPC00.cs',
    'Assets/BASSLINE/BL23/Sim/Content/Lines_Premise.cs',
    'Assets/BASSLINE/BL23/Sim/Content/Lines_Premise.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Content/Lines_Social.cs',
    'Assets/BASSLINE/BL23/Sim/Content/Lines_Social.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Content/Voice/Voice_P06.cs',
    'Assets/BASSLINE/BL23/Sim/Content/Voice/Voice_Traits.cs',
    'Assets/BASSLINE/BL23/Sim/Content/Voice/Voice_Traits.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Data/Cast.cs',
    'Assets/BASSLINE/BL23/Sim/Life/Factions.cs',
    'Assets/BASSLINE/BL23/Sim/Life/Factions.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Life/HouseEvents.cs',
    'Assets/BASSLINE/BL23/Sim/Life/HouseEvents.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Life/Life.cs',
    'Assets/BASSLINE/BL23/Sim/Life/LifeBanter.cs',
    'Assets/BASSLINE/BL23/Sim/Life/LifeData.cs',
    'Assets/BASSLINE/BL23/Sim/Life/LifeDialogue.cs',
    'Assets/BASSLINE/BL23/Sim/Life/LifeFest.cs',
    'Assets/BASSLINE/BL23/Sim/Life/LifeHearts.cs',
    'Assets/BASSLINE/BL23/Sim/Life/LifeTable.cs',
    'Assets/BASSLINE/BL23/Sim/Murder/CaseApi.cs',
    'Assets/BASSLINE/BL23/Sim/Murder/Conscience.cs',
    'Assets/BASSLINE/BL23/Sim/Murder/Conscience.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Murder/Initiative.cs',
    'Assets/BASSLINE/BL23/Sim/Murder/InitiativeDesign.cs',
    'Assets/BASSLINE/BL23/Sim/Murder/InitiativeMotives.cs',
    'Assets/BASSLINE/BL23/Sim/Murder/InitiativeStrike.cs',
    'Assets/BASSLINE/BL23/Sim/State/Enums.cs',
    'Assets/BASSLINE/BL23/Sim/State/GameState.cs',
    'Assets/BASSLINE/BL23/Sim/State/State.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/Cases.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/Combat.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/FurnitureChanges.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/FurnitureChanges.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Systems/Gore.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/Grammars.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/HousePush.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/HousePush.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Systems/Hunger.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/LifeAI.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/Perception.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/PlayerPhysics.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/Relations.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/Rules.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/Simulation.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/Testimony.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/Tricks.cs',
    'Assets/BASSLINE/BL23/Sim/Trial/Debate/DebateCulprit.cs',
    'Assets/BASSLINE/BL23/Sim/Trial/Debate/DebateCulprit.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Trial/Debate/DebateEngine.cs',
    'Assets/BASSLINE/BL23/Sim/Trial/Debate/DebateEngine.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Trial/Debate/DebateModel.cs',
    'Assets/BASSLINE/BL23/Sim/Trial/Debate/DebateModel.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Trial/Debate/DebatePlayer.cs',
    'Assets/BASSLINE/BL23/Sim/Trial/Debate/DebatePlayer.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Trial/Debate/DebateRoom.cs',
    'Assets/BASSLINE/BL23/Sim/Trial/Debate/DebateRoom.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Trial/Debate/DebateTheories.cs',
    'Assets/BASSLINE/BL23/Sim/Trial/Debate/DebateTheories.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Trial/Debate/DeckBuild.cs',
    'Assets/BASSLINE/BL23/Sim/Trial/Debate/DeckBuild.cs.meta',
    'Assets/BASSLINE/BL23/Sim/Trial/Logic.cs',
    'Assets/BASSLINE/BL23/Sim/Trial/Settlements.cs',
    'Assets/BASSLINE/BL23/Sim/Trial/TrialGames.cs',
    'Assets/BASSLINE/BL23/Sim/Trial/TrialSystem.cs',
    'Assets/BASSLINE/BL23/Sim/Violence/Assaults.cs',
    'Assets/BASSLINE/BL23/Sim/Violence/Firearms.cs',
    'Assets/BASSLINE/BL23/Sim/Violence/ViolencePlans.cs',
    'Assets/BASSLINE/BL23/Sim/World/Decorator.cs',
    'Assets/BASSLINE/BL23/Sim/World/LayoutGenerator.cs',
    'Assets/BASSLINE/BL23/Sim/World/WorldTypes.cs',
    'Documentation/BL23/CLOUD_RESUME.md',
    'Documentation/BL23/CharacterBible.md',
    'Documentation/BL23/CloudWorkLog.md',
    'Documentation/BL23/DecisionLog.md',
    'Documentation/BL23/HANDOFF.md',
    'Documentation/BL23/ImplementationStatus.md',
    'Documentation/BL23/SocialEventsDesign.md',
    'ImplementationStatus.md',
    'Tests/BL23/SimTests/DebateDump.cs',
    'Tests/BL23/SimTests/EventCase.cs',
    'Tests/BL23/SimTests/Extra.cs',
    'Tests/BL23/SimTests/FirstsScan.cs',
    'Tests/BL23/SimTests/FurnitureKnowledgeTest.cs',
    'Tests/BL23/SimTests/PremiseTest.cs',
    'Tests/BL23/SimTests/RoomCensus.cs',
    'Tests/BL23/SimTests/StaticCheck.cs'
  )
  $tmp = Join-Path $env:TEMP 'bassline_cloud_apply'
  if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
  Expand-Archive -Path $zip -DestinationPath $tmp
  $root = (Get-ChildItem $tmp -Directory | Select-Object -First 1).FullName
  $backup = Join-Path $proj ('_cloud_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
  foreach ($f in $files) {
    $src = Join-Path $root $f; $dst = Join-Path $proj $f
    if (-not (Test-Path $src)) { Write-Host "missing in zip: $f" -ForegroundColor Yellow; continue }
    if (Test-Path $dst) { $b = Join-Path $backup $f; New-Item -ItemType Directory -Force -Path (Split-Path $b) | Out-Null; Copy-Item $dst $b -Force }
    New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
    Copy-Item $src $dst -Force
    Write-Host "updated $f"
  }
  Write-Host "DONE. backup: $backup" -ForegroundColor Green
}
```
3. Unity를 열어 컴파일이 끝나기를 기다린다.
4. PC에서 확인할 것:
   - `cd Tests/BL23/GameCompile && dotnet build -c Release`에서 오류가 0개인지.
   - 새 파일 `Game/UI/RoomCardUI.cs`: 클라우드에서는 스텁으로만 컴파일했다. Unity에서 오류가 없는지, 방 그림을
     `Assets/BASSLINE/BL23/Resources/Rooms/`에 넣으면 그 방에 처음 들어갈 때 카드가 뜨는지.
   - 새 방 10종(관측실·신탁실 등)이 저택에 보이는지, 가구가 제자리에 놓이는지.
   - 게임 1일차:
     - 아침 식탁 「약속」 장면이 나오는지.
     - 2일차 아침에 「소원의 견본」 공지와 식탁이 나오는지.
     - 심판이 토론(1막~4막)으로 진행되는지.
   - 문제가 있으면 `_cloud_backup_...` 폴더의 원본으로 되돌리면 된다.
5. P10 모델 보정본은 Unity 폴더 밖(`ModelFix/P10_KangJunseo/`)에 있다. `REPORT.md`를 보고 직접 교체한다.

## 파일 목록을 다시 만드는 법 (작업이 늘었을 때)
```bash
git diff --name-only a946694 HEAD | grep -E '^(Assets/|Documentation/|Tests/|ImplementationStatus\.md)' | sort
```
