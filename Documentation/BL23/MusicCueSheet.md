# BL23 음악 큐 시트 (MusicCueSheet)

작성: 2026-09-27, 오디오 담당(서브에이전트). 코드와 1:1로 대응한다.
- 기본 배치: `Assets/BASSLINE/BL23/Game/Audio/MusicLibrary.cs` (`BuildStates`)
- 소유자용 배치 파일: `Assets/StreamingAssets/music_cues.txt`. 게임이 켜질 때 이 파일이 기본 배치를 덮어쓴다. 두 파일은 지금 같은 내용이다.
- 재생 규칙: `Game/Audio/MusicDirector.cs`. 환경음: `Game/Audio/AmbienceDirector.cs`
- 이전 문서 `MusicMap.md`는 곡 목록, 중복 검사, 트림·루프 메타데이터 자료로 남겨 둔다. **상황별 배치는 이 문서가 기준이다.**

## 00. 최신 기준: 소유자가 정리한 폴더 (2026-09-27 22:40) — 아래 옛 배치보다 우선

> "상황에 맞는 BGM,OST들.zip … 각각 상황에 맞게 내가 정리해놨어. 네가 알아서 상황에 맞게 음악이 나오게 해줘. 일상 음악은 너무 이어서 나오지 않게해줘."

| 소유자 폴더 | 게임 상태 | 곡 |
|---|---|---|
| 평상시 BGM (슬픈 상황에서도 가능) | DailyMorning/Day/Evening, Night, Mystery, Gathering, Aftermath | -P, An ordinary life (2), bat, Echoes of the Marble Hall, Glow, Midnight in the Ruins (1·2), people, piano PL, pianop, SymphonicSuite, Tell me my name (1·2·3), Tired, yeat |
| npc들이랑 놀 때 | Bond (함께 시간 보내기, 유대 대화) | I can't feel it |
| 시신이 발견되고 직후 | BodyDiscovery (발견 영상의 자작 효과음 직후) | murder(+kick 레이어), Meeting the King, Cong |
| 수사 BGM | Investigation, InvestigationLate | inspection, Mystery, ghost, efefef, hope, deep, future / Kill your self, future, inspection |
| 훼손도·참혹성·잔혹성·트릭의 복잡함이 클 때 | InvestigationGrim (훼손, 상처 5개 이상, 트릭 3단계 이상) | eege, Gaze, Give me |
| 범인의 윤곽이 보일 때 | Closing (보드 질문 대부분 확정, 심판 Suspicious/Culprit 단계) | Points of doubt, final, raid, happy |
| 재판장 앞에서 대기할 때 | Assembly | Hey(신규) → An ordinary life |
| 재판 일반 | TrialOpening, TrialDebate, Vote | Saul theme, judgment, korean / judgment 2, My Apple, Game, FU, 그걸 봤어, 그거였군, 모든 걸 알아 … |
| 재판 다른 모드들 | TrialPressure (규칙 게임, 특수 모드) | cyber, pedal, I don't regret it |
| 재판 때 패닉 | TrialPanic (범인 붕괴, break 비트) · Execution | judgment (2) guitar, drift bawl, 진짜, 죽어, WAH, SCRU, gore, 아무 걱정이 필요 없어 |
| 재판 때 npc들 웃긴 추리 할 때 | TrialComic (비트 Key에 comic/joke/tangent…, 또는 `TrialCue("comic")`) | Wolf |
| 범인과 결정적으로 말싸움을 할 때 | TrialClimax (최종 단계) | BOSS, CEO, female |
| 저택에서 예상치 못한 걸 발견했을 때 | Surprise (일상 중 첫 증거 발견 시 한 곡, 쿨다운 6분) | boomba, Cloud |

**일상 음악의 쉼.**
- 곡 사이에 95~205초(밤은 110~230초) 동안 방 환경음만 들린다.
- 쉼은 방이나 시간대가 바뀌어도 이어진다.
- 방을 옮기면 20~45초 쉰 뒤 다음 곡이 나온다.

## 0. 소유자 지시 (2026-09-27 14:35) — (옛 배치, 참고용)

> 탐색이나 수사할 때는 좀 잔잔한 타입의 음악들만 나왔으면 좋겠어. 재판 때만 디스코나 EDM 테크노 이런 장르의 음악이 나왔으면 좋겠네.

이 지시를 다음 세 규칙으로 옮겼다.
1. **탐색·일상·수사**(아침, 낮, 저녁, 밤, 기묘한 방, 식사, 유대 대화, 사건 후, 수사, 수사 막바지, 소집)에는 **드럼 비트가 없는 잔잔한 곡만** 쓴다. 곡 사이에는 20~110초를 쉬고, 그동안 방의 환경음(벽난로, 시계, 빗소리 등)이 들린다.
2. **심판**(개정, 토론, 규칙 게임, 최종 변론, 투표)에는 **4박 킥이 계속 이어지는 댄스곡(하우스·디스코·EDM·테크노)만** 쓴다.
3. 탐색 중에 큰 스팅어를 쓰는 경우는 **시신 발견뿐이다.** 증거를 찾을 때 나던 합성 팡파르는 종이와 작은 종으로 된 부드러운 녹음 소리(`evidence`)로 바꿨다.

라이브러리 74곡 안에 조건을 만족하는 댄스곡이 충분했다(아래 2절). 그래서 외부 음악은 받지 않았다.

## 1. 판단 근거: 들을 수 없으므로 측정했다

도구: `C:/Users/리오/BL23Lab/AudioLab/Forge` (`Forge.exe music`). 출력은 `AudioLab/out/music_features.txt`이다. 이전 분석 결과(`BL23Lab/MusicAnalysis/out/analysis.txt`: 조성, 폭, 앞뒤 무음)도 함께 봤다.

| 지표 | 계산 | 쓰임 |
|---|---|---|
| LUFS / LRA | BS.1770 통합 음량 / 3초 단기 음량의 10~95% 폭 | 크기와 기복. 자유 탐색에는 LRA가 작거나 조용하게 시작하는 곡 |
| onset/s, beat | 스펙트럼 플럭스 피크 수 / 온셋 포락선의 자기상관 최대값(60~180 BPM) | 리듬이 얼마나 빽빽하고 규칙적인가 |
| **kickAc** | 35~120 Hz 대역 플럭스의 자기상관(100~175 BPM 한 박 지연). kick4는 한 마디 뒤 | **4박 킥(four-on-the-floor)의 증거.** 댄스곡은 0.6 이상 |
| hatOff, pump | 6.5~10.5 kHz 플럭스의 반 박 자기상관 / 중역 에너지의 한 박 변조(사이드체인) | 디스코·하우스의 엇박 하이햇, EDM의 펌핑 |
| centroid | 파워 가중 스펙트럼 중심(Hz) | 밝기. 대사가 많은 장면에는 목소리 대역을 덜 가리는 어두운 곡 |
| 보컬 검사 | 조화 음높이 추적(가운데 채널, 미세한 음높이 흔들림) | 명확한 리드 보컬이 있는 곡은 없었다. `kill_your_self`만 약하게 의심된다(대사가 적은 오판 장면에만 둠) |

검증: 소유자가 `[Instrumental]`로 표시한 곡(CEO, happy, My Apple, pianop…)의 보컬 지표는 성가 녹음 대비 낮게 나와 기준과 맞았다.

## 2. 두 무리로 나누기

**잔잔한 곡 (킥 규칙성 < 0.55, 온셋이 드물거나 박이 약함):** boomba, piano_pl, pianop, an_ordinary_life(1·2), faded_ink(1·2), hope, tell_me_my_name(1·2·3), midnight_in_the_ruins(1·2), tired, ghost, people, yeat, glow, deep, murder(윗 스템), conspiracy_in_the_dark, echoes_of_the_marble_hall, beneath_the_iron_sky(1·2), meeting_the_king, open, cloud, symphonic_suite_aot

**댄스곡 (킥 규칙성 0.6~0.92, 129~161 BPM):** saul_theme(0.92, 129 BPM, 사이드체인 펌핑 0.67로 가장 뚜렷한 하우스/디스코), ceo(0.84), eege(0.84), cyber(0.82, 반복형 테크노), ai(0.82), give_me(0.80), drift_bawl(0.78), debate(0.78), final(0.74), my_apple(0.73), judgment_2(0.72), modeun_geol_ara(0.72), scru(0.71), geugeol_bwasseo(0.71), happy(0.63), game(0.60), wolf(0.51)

**경계에 있는 곡 (강하고 빽빽하지만 4박 킥 증거는 약함):** jinjja, boss, female, gaze, points_of_doubt, inspection, judgment. 모두 크고 박이 뚜렷하므로 심판 쪽에 두었다. 탐색에는 쓰지 않는다.

## 3. 상황별 배치

| 상황 | 곡 | 이유(한 줄) |
|---|---|---|
| Title | open | 30초 무이음 앰비언트 패드(LRA 1.0, 온셋 0.13/s) |
| Menu | cloud, open | 가장 짧고 여린 두 곡. 메뉴를 닫으면 원래 곡을 이어서 재생한다 |
| Prologue | echoes_of_the_marble_hall → meeting_the_king → beneath_the_iron_sky | 저택 도착 → 유스티(드럼 없는 지속음, 온셋 0.03/s) → 선언. 순서대로 재생한다 |
| **DailyMorning** | boomba, piano_pl, an_ordinary_life_2 | 밝지만 성긴 곡들(온셋 1.9~2.9/s). boomba는 넓고 조용하며(-16 LUFS) piano_pl은 피아노다. 쉼 30~60초 |
| **DailyDay** | an_ordinary_life, faded_ink_1, hope, tell_me_my_name_2 | 가장 오래 머무는 시간대라 목소리 대역을 덜 가리는 어두운 음색(centroid 110~290)을 골랐다. hope는 온셋 0.09/s의 지속음이다. 쉼 35~70초 |
| **DailyEvening** | faded_ink, pianop, tell_me_my_name | 피아노와 어두운 현악. pianop은 16초 여운이 있다. 쉼 30~60초 |
| **Night** | midnight_in_the_ruins, midnight_in_the_ruins_2, tired | 성기고 고역이 없다(tired의 고역은 -79 dB). 음량도 가장 낮다(0.55). 쉼 50~110초라 밤에는 환경음이 주인공이다 |
| Mystery | ghost, people, yeat | 저역이 없고 넓은 곡들(저역비 0.04~0.08). 박이 느껴지는 bat은 이번 지시에 따라 뺐다 |
| **Gathering** (식사) | an_ordinary_life_2, an_ordinary_life, boomba | 따뜻하고 먹먹한 음색이라 테이블 대화가 위에 얹힌다. 대사 더킹 -7 dB. 아침·낮 곡과 겹치므로 식사가 시작돼도 곡이 끊기지 않는다 |
| **Bond** (유대 대화) | pianop, piano_pl, faded_ink_1, glow | 친밀한 피아노. 더킹 -6 dB |
| Aftermath | tell_me_my_name, tell_me_my_name_3, hope | 판결 다음 날: 애도에서 조용한 희망으로 |
| Tension | deep, conspiracy_in_the_dark, murder | 비트 없이 쌓이는 드론. murder는 킥 스템을 빼고 윗 스템만 쓴다 |
| **BodyDiscovery** | disc_theme (이 게임을 위해 새로 작곡한 공포 루프, 비트 없음). 곡이 설치되기 전의 기본값은 murder → deep | 0.12초 컷, 2.2초 정적(여기서 발견 스팅어. `discovery`는 새 스팅 disc_sting으로 바뀐다), 그다음 공포. 기본값에서는 킥 스템을 35%로만 넣어 심장 박동처럼 들리게 했다. **수사가 시작될 때까지(최대 3분) 일상 곡으로 돌아가지 않는다** |
| **Investigation** | echoes_of_the_marble_hall, beneath_the_iron_sky, yeat, people | 드럼이 없는 시네마틱·앰비언트 곡(온셋 0.07~1.5/s). 집중할 수 있고 쉼은 15~35초 |
| InvestigationLate | conspiracy_in_the_dark, beneath_the_iron_sky_v2, deep | 시간 압박은 비트가 아니라 점점 커지는 빌드업(-26 → -11 LUFS)과 드론으로 표현했다 |
| **Assembly** (소집, 새 상태) | meeting_the_king → beneath_the_iron_sky_v2 | 모두 승강기로 모일 때. 프롤로그의 '왕' 주제를 다시 불러 심판의 무게를 예고한다(Session이 InvestigationLate를 요청해도 Assembly 단계에서는 이 곡으로 바꾼다) |
| **TrialOpening** | saul_theme, judgment | 하우스/디스코. saul_theme은 킥 0.92에 사이드체인 펌핑 0.67이다. judgment는 심판 메인 테마다 |
| **TrialDebate** | judgment_2, ceo, my_apple, happy, game, wolf, inspection | 136~148 BPM의 일정한 EDM 그루브(LRA 1.8~4.9)라 글을 읽는 동안 거슬리지 않는다. 가장 오래 듣는 심판 상태라 7곡을 돌린다 |
| **TrialPressure** (규칙 게임·반론) | cyber, ai, debate, drift_bawl, points_of_doubt, jinjja | 테크노(cyber)와 하드 EDM, 157~161 BPM. 가장 크다(-6.8 ~ -10 LUFS) |
| **TrialClimax** | final, eege, female, boss | 가장 큰 드롭. eege는 25초 도입 뒤 폭발하고 LRA가 13.8이다 |
| **Vote** | geugeol_bwasseo, gaze | 어둡게 몰아붙이는 136 BPM 댄스 루프(centroid 201)를 투표가 끝날 때까지 반복한다(Hold) |
| VerdictCorrect | i_dont_regret_it, glow | 자백: 23초의 조용한 도입 뒤 드롭 |
| VerdictWrong | symphonic_suite_aot, kill_your_self | 비극 |
| Execution | gore, amu_geokjeong_eopseo, jugeo, wah, scru | 반어적인 클럽 음악. 챕터당 한 번이라 풀을 넓게 잡았다 |
| Escape | bandit_stroy, pedal, fu, raid, give_me | 범인이 소원을 들고 도망친다 |
| Reveal | geugeoyeotgun, modeun_geol_ara | "그거였군" / "모든 걸 알아" |
| LoopReset | future → tell_me_my_name_3 → i_cant_feel_it | 하드컷, 1.6초 정적, 섬뜩한 연속 재생 |
| (스팅어) chapter_clear | cong | 22초 징글 |

**배치하지 않은 곡**: judgment_guitar, judgment_2_guitar(기타 편곡이라 킥 증거가 0.24~0.30이고 EDM이 아님), efefef, mystery, korean, track_p, bat(박이 있어 잔잔하지 않고, 댄스곡도 아님). `music_cues.txt`에 이름만 적으면 바로 다시 쓸 수 있다.

## 4. 재생 규칙 (MusicDirector)

- **곡 사이의 쉼(Gap)**: 자유 탐색 상태는 한 곡이 끝나면 `Gap + 0~GapJitter`초를 쉰다. 쉬는 동안에는 방 환경음이 3 dB 올라온다(`AmbienceDirector.MusicRestBoostDb`).
- **요청과 재생 분리**: `Current`는 게임이 마지막으로 요청한 상태이고, `Playing`은 실제로 재생 중인 상태다.
  - 시간대가 바뀌면(아침→낮 등) **지금 곡을 끝까지 들려준 뒤** 새 시간대 곡으로 넘어간다(최대 95초 대기, 곡 중간에 페이드하지 않음).
  - Tension은 2초 동안 요청이 이어져야 들어가고, 10초 동안 요청이 없어야 빠진다. 공포 수치가 0.6 근처에서 흔들려도 음악이 깜빡이지 않는다.
  - Gathering은 4초 뒤 들어가고 8초 뒤 빠진다. Mystery는 3초 뒤 들어가고 5초 뒤 빠진다. 문턱을 스치듯 지나가도 바뀌지 않는다.
  - 사건, 심판, 시네마틱 상태는 즉시 바뀐다.
- **시신 발견 뒤 유지**: 발견 후 3분 동안, 또는 수사가 시작될 때까지 일상 요청을 무시하고 공포 음악을 유지한다. 발견 직후 '행복한' 곡이 나오던 문제를 막는다.
- **소집(Assembly)**: 승강기로 모이는 단계에서는 InvestigationLate 요청을 Assembly 큐로 바꾼다.
- **더킹**: 대화창을 열면 상태별 DuckDb(-6 ~ -9 dB)만큼 낮춘다. 대화창 밖에서 목소리(방송, 테이블 토크)가 나오는 동안에도 -5 dB를 낮춘다(`VoiceDuckDb`). 스팅어가 울릴 때는 -10 dB, 징글이 울릴 때는 -24 dB다.
- **크로스페이드**: 탐색 4~6초, 사건 2~3초, 심판 0.3~1.5초. 발견과 루프 리셋은 하드컷이다.
- **같은 곡 반복 금지**: 한 상태 안에서는 가장 오래전에 들은 곡부터 틀고, 직전 곡은 다시 틀지 않는다. 150초 안에 같은 상태로 돌아오면 끊긴 곳부터 이어서 재생한다.

## 5. 소유자 확인 권장

직접 듣지 않고 측정값으로 판단한 배치다. 특히 아래는 한 번 들어 보길 권한다.
1. `hope`, `tell_me_my_name_2`가 낮 시간대에 충분히 잔잔한지. 둘 다 LUFS가 높은 편이지만 온셋이나 박이 약하다.
2. `inspection`(토론)과 `gaze`(투표)가 원하는 '댄스' 느낌인지. 둘 다 크고 박이 강하지만 4박 킥 증거는 약하다.
3. 기타 편곡 두 곡(judgment_guitar, judgment_2_guitar)을 심판에서 뺀 것. 다시 넣고 싶으면 `music_cues.txt`의 TrialDebate 줄에 추가하면 된다.
