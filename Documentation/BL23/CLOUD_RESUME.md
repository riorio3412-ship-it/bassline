# 클라우드 세션 재개 안내 (2026-09-28)

## 지금 상태
- **저장소:** `riorio3412-ship-it/bassline`
- **작업 브랜치:** `claude/ecstatic-mccarthy-w08ico`
  - `main`에는 오너가 올린 `BASSLINE_1_code.zip`만 있다.
  - 브랜치에는 그 압축을 푼 기준선 커밋과, 그 위의 작업 커밋이 있다.
- **이번 작업:** 가구 변화 커밋 경로와 사람별 지식. 자세한 내용은 `HANDOFF.md §3.000`에 있다.
- **진행 중일 수 있는 작업:** 오너 모델 보정(`강준서 2차 수정본.glb`)을 백그라운드로 돌리고 있다.
  - 끝나면 `ModelFix/P10_KangJunseo/`에 결과가 커밋된다. 구성: 원본, 보정본 GLB, 전후 비교 이미지, `REPORT.md`.

## 다시 시작하는 방법
1. **같은 클라우드 세션**이면 "이어서 해줘"라고만 보내면 된다.
2. **새 클라우드 세션**이면:
   - 이 저장소의 `claude/ecstatic-mccarthy-w08ico` 브랜치로 연다.
   - 첫 메시지: "Documentation/BL23/CLOUD_RESUME.md와 HANDOFF.md §3.000을 읽고 이어서 해줘"
   - 커널 테스트 준비:
     ```bash
     apt-get install -y dotnet-sdk-10.0
     cd Tests/BL23/SimTests
     dotnet run -c Release -- furnknow 20260926 6   # → ok=33 fail=0
     dotnet run -c Release -- campaign 20260926 6   # → faults=0, roundtrip=IDENTICAL
     ```
   - 클라우드에는 Unity가 없다. GameCompile(Unity DLL 필요), Unity 빌드, 프로브는 오너 PC에서만 된다.
3. **오너 PC에서 Claude Code를 쓸 때**는 아래 "PC에 반영하기"를 먼저 한다.

## PC에 반영하기 (바뀐 파일만, 덮어쓰기 전 백업)
1. 브라우저에서 GitHub에 로그인한 채로 아래 주소를 연다. `bassline-claude-ecstatic-mccarthy-w08ico.zip`이 내려받기 폴더에 받아진다.
   `https://github.com/riorio3412-ship-it/bassline/archive/refs/heads/claude/ecstatic-mccarthy-w08ico.zip`
2. PowerShell에 아래를 붙여 넣는다.
   - 바뀐 파일만 BASSLINE 폴더에 복사한다.
   - 덮어쓰기 전 원본은 `BASSLINE\_cloud_backup_날짜시각\`에 보관한다.
```powershell
& {
  $zip = Join-Path $env:USERPROFILE 'Downloads\bassline-claude-ecstatic-mccarthy-w08ico.zip'
  $proj = @((Join-Path ([Environment]::GetFolderPath('Desktop')) 'BASSLINE'), (Join-Path "$env:OneDrive" 'Desktop\BASSLINE')) | Where-Object { Test-Path $_ } | Select-Object -First 1
  if (-not (Test-Path $zip) -or -not $proj) { Write-Host "zip or BASSLINE folder not found" -ForegroundColor Red; return }
  $files = @(
    'Assets/BASSLINE/BL23/Sim/Systems/FurnitureChanges.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/PlayerPhysics.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/Perception.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/Gore.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/Rules.cs',
    'Assets/BASSLINE/BL23/Sim/Systems/Tricks.cs',
    'Assets/BASSLINE/BL23/Sim/Violence/Assaults.cs',
    'Assets/BASSLINE/BL23/Sim/Violence/Firearms.cs',
    'Assets/BASSLINE/BL23/Sim/Violence/ViolencePlans.cs',
    'Assets/BASSLINE/BL23/Sim/State/State.cs',
    'Assets/BASSLINE/BL23/Sim/State/Enums.cs',
    'Assets/BASSLINE/BL23/Sim/World/WorldTypes.cs',
    'Assets/BASSLINE/BL23/Game/World/WorldPresenter.cs',
    'Assets/BASSLINE/BL23/Game/UI/NoteUI.cs',
    'Tests/BL23/SimTests/Extra.cs',
    'Tests/BL23/SimTests/FurnitureKnowledgeTest.cs',
    'Documentation/BL23/HANDOFF.md',
    'Documentation/BL23/ImplementationStatus.md',
    'Documentation/BL23/CLOUD_RESUME.md',
    'ImplementationStatus.md'
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
3. Unity를 열어 컴파일이 끝나기를 기다린다. 새 파일 `FurnitureChanges.cs`의 `.meta`는 Unity가 자동으로 만든다.
4. PC에서 확인할 것. 규칙대로 시스템 하나를 완성한 뒤 한 번만 한다.
   - `cd Tests/BL23/GameCompile && dotnet build -c Release`에서 오류가 0개인지.
   - 게임을 열어 의자를 밀고 끌어 본다.
     - 끄는 소리가 나는지.
     - 다른 방에 갔다가 돌아왔을 때 "의자가 전에 있던 자리에서 옮겨져 있다" 알림이 뜨는지.
   - 문제가 있으면 `_cloud_backup_...` 폴더의 원본으로 되돌리면 된다.

## 다음 우선순위
1. 가구 지식을 **증언과 심판**에 연결한다.
   - NPC가 "아까 식당에서 뭔가 끄는 소리가 났어", "의자가 옮겨져 있었어"라고 말하게 한다.
   - 은판 근거로 쓴다. 이때 숨은 행위자는 새지 않게 한다.
2. **회상 재생**(Replay)에 가구 이동 궤적과 넘어짐을 기록하고 재생한다.
3. 물리 보고를 **틱 경계로 모아서 확정**한다(지시서 5절 4~5단계). 지금은 보고가 오는 즉시 확정된다.
4. HANDOFF §1.2의 큰 작업: 심판 재구현, 능동적 범인, 폭력과 모션.
