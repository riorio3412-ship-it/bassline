# BL23 음악 배치표 (MusicMap)

> **2026-09-27 갱신: 상황별 배치는 [MusicCueSheet.md](MusicCueSheet.md)가 기준이다.** 소유자 지시에 따라 탐색·일상·수사에는 잔잔한 곡만, 심판에는 댄스(하우스·EDM·테크노) 곡만 쓴다. 아래 2~3절의 배치는 이전 버전이다. 1절(중복·변형), 5절(기술 메모), 7절(수치)은 지금도 유효하다.

작성: 2026-09-27, 오디오 디렉터(서브에이전트). 코드: `Assets/BASSLINE/BL23/Game/Audio/MusicLibrary.cs` (이 문서와 1:1 대응).
원본: `C:/Users/리오/OneDrive/Desktop/아카이브/` 76개 파일(mp3 70 + wav 6). **원본은 건드리지 않았다.** 선택한 74개를 `Assets/BASSLINE/BL23/Resources/Music/`에 ASCII 소문자 이름으로 복사했다.

## 0. 어떻게 판단했나

나는 음악을 "들을" 수 없으므로 전 곡을 디코딩해 수치로 분석하고(도구: `C:/Users/리오/BL23Lab/MusicAnalysis`, NLayer MP3 디코더 + 자체 DSP), 제목의 의미와 합쳐 판단했다.

| 지표 | 의미 | 쓰임 |
|---|---|---|
| LUFS (BS.1770 K-가중, 게이트) | 체감 음량 | 에너지 판단 + 재생 음량 정규화(-16 LUFS로 **감쇠만**) |
| LRA (3초 단기음량 10–95% 폭) | 곡 안의 다이내믹 | 크게 빌드업되는 곡(Mystery 11.6, murder 14.3) vs 평탄한 루프(open 1.0, bat 0.7) |
| 스펙트럼 중심(centroid), 저역비(<120 Hz 에너지 비율) | 밝기 / 무게감 | 판결곡(judgment 1345 Hz)처럼 밝은 그루브 vs Tired·Faded Ink처럼 어두운 곡 |
| 온셋 밀도(개/초), BPM 추정, 펄스 명확도 | 리듬 활동량 | 대사 밑에 깔 수 있는지(온셋 적고 일정) / 긴박한지 |
| 조성 추정(Krumhansl) | 장·단조 경향 | 참고용(신뢰도 낮음) |
| 앞뒤 무음, 페이드아웃 길이, 끝↔처음 스펙트럼 유사도 | 트림·루프 가능성 | `Start/End/Tail/SeamlessLoop` 메타데이터 |
| 곡간 유사도(초당 크로마 편차 + 온셋 포락선 상호상관) | 중복·변형 탐지 | 아래 1절 |

곡별 수치 전체는 부록(7절). 원자료: `BL23Lab/MusicAnalysis/out/analysis.txt`, `timbre.txt`, `structure.txt`, `ends.txt`.

## 1. 중복 · 변형 검사 결과

- **바이트 동일(해시 일치) 파일은 없다.** 크기가 같은 두 쌍을 따로 검사했다.
- `murder.mp3` ↔ `murder kick ver.mp3` (둘 다 1,556,292 B): 샘플 수까지 같다(77.61 s). 온셋 포락선 상관이 **0.95(시차 0)**라 박자 격자는 같지만, 파형 상관은 대역별로 0.12–0.28로 낮다. `murder`는 120 Hz 아래가 사실상 없고(저역비 0.000) `kick ver`는 에너지의 97%가 저역이다. 결론: **같은 곡의 분리된 스템**(위쪽 악기 / 킥·베이스). 그래서 둘을 **샘플 단위로 동기 재생하는 수직 레이어**로 쓴다. Tension에서는 `murder`만 깔고 `MusicDirector.SetIntensity()`로 킥을 올리며, BodyDiscovery에서는 처음부터 둘 다 나온다.
- `debate.mp3` ↔ `그거였군.mp3` (둘 다 1,991,492 B): 길이(99.37 s)와 비트레이트가 우연히 같을 뿐이다. 파형 상관은 약 0이고 내용도 다르다. 둘 다 쓴다.
- `judgment` ↔ `judgment guitar`: 크로마 유사도 0.54, 온셋 0.42. 같은 작곡을 다르게 편곡한 것이다(길이 105 s / 195 s). 같은 재판 계열 상태에 나눠 넣었다.
- `An ordinary life` (2), `Faded Ink` (1), `Tell me my name` (2)(3), `Midnight in the Ruins` (2), `Beneath the Iron Sky` ver 2: 구조가 일치하지 않는다(시간축 유사도가 무관한 곡 수준). **같은 주제의 다른 버전**이므로 같은 상태나 이웃 상태의 로테이션 파트너로 썼다. 같은 곡이 연달아 나오지 않으면서 분위기는 유지된다.

## 2. 상태별 배치 (MusicState → 곡)

재생 규칙은 3절에 있다. "Rotate"는 최근에 덜 들은 곡부터 틀고, 같은 곡은 연속으로 틀지 않는다.

| 상태 | 곡 (리소스 이름) | 모드 · 페이드 |
|---|---|---|
| Silence | — | 1.5 s 페이드아웃 |
| Title | `open` | Hold(무한 루프), 인 3 s |
| Menu | `game`, `cloud` | Rotate, 볼륨 0.65, 인 0.8 s. 메뉴를 닫으면 이전 곡을 **이어서** 재생 |
| Prologue | `echoes_of_the_marble_hall` → `meeting_the_king` → `beneath_the_iron_sky` | Sequence |
| DailyMorning | `happy`, `an_ordinary_life`, `my_apple` | Rotate, 크로스페이드 4 s, 볼륨 0.72 |
| DailyDay | `an_ordinary_life_2`, `ceo`, `korean`, `boomba`, `drift_bawl` | Rotate(가장 오래 듣는 상태라 5곡) |
| DailyEvening | `faded_ink`, `piano_pl`, `efefef` | Rotate |
| Night | `midnight_in_the_ruins`, `midnight_in_the_ruins_2`, `tired` | Rotate, 인 4 s, 볼륨 0.68 |
| Tension | `conspiracy_in_the_dark`, `murder`(+킥 레이어), `deep` | Rotate, 진입 시 intensity 0 |
| Mystery | `ghost`, `bat`, `people`, `yeat` | Rotate, 볼륨 0.7 |
| BodyDiscovery | `geugeol_bwasseo`, `murder`(킥 레이어 100%) | 0.12 s 컷 → 2.2 s 공백(여기서 "discovery" 스팅어) → 인 0.4 s |
| Investigation | `inspection`, `mystery`, `wolf` | Rotate |
| InvestigationLate | `points_of_doubt`, `cyber`, `track_p` | Rotate, 빠른 전환 1 s |
| TrialOpening | `judgment`, `saul_theme`, `beneath_the_iron_sky_v2` | Rotate |
| TrialDebate | `debate`, `judgment_guitar`, `judgment_2`, `judgment_2_guitar` | Rotate, 더킹 -8 dB |
| TrialPressure | `jinjja`, `ai`, `female` | 아웃 0.35 s / 인 0.5 s |
| TrialClimax | `boss`, `final`, `eege` | 0.3 s 컷 + 0.3 s 공백 |
| Vote | `gaze` | Hold |
| VerdictCorrect | `i_dont_regret_it`, `glow` | 0.2 s 컷 → 1.5 s 침묵 → 인 |
| VerdictWrong | `symphonic_suite_aot`, `kill_your_self` | 0.2 s 컷 → 1.8 s 침묵 → 인 |
| Execution | `gore`, `amu_geokjeong_eopseo`, `jugeo`, `wah`, `scru` | 챕터당 1회라 풀을 넓게, 인 0.2 s |
| Escape | `bandit_stroy`, `pedal`, `fu`, `raid`, `give_me` | 챕터당 1회, 인 0.3 s |
| Reveal | `geugeoyeotgun`, `modeun_geol_ara` | Rotate |
| Aftermath | `tell_me_my_name`, `tell_me_my_name_2`, `faded_ink_1`, `pianop`, `hope` | 1 s 공백 → 인 4 s |
| LoopReset | `future` → `tell_me_my_name_3` → `i_cant_feel_it` | **0.05 s 하드컷** → 1.6 s 침묵 → Sequence |
| (스팅어) chapter_clear | `cong` | 음악 파일 스팅어. 재생 동안 음악을 -24 dB로 낮춘다 |

## 3. 곡별 배치 이유

수치는 (길이, LUFS, 추정 BPM, 밝기 centroid Hz, 온셋/초) 순서다.

**Title / Menu / Prologue**
- `open` ← open.wav (0:30, -12.5, LRA 1.0, 스테레오 폭 1.11). 30초짜리 평탄한 넓은 패드이고 끝과 처음의 스펙트럼 유사도가 0.91이라 **루프용으로 만든 곡**으로 봤다. WAV라 인코더 패딩이 없어 무이음 루프가 된다. 제목 그대로 타이틀 화면에 쓴다.
- `game` ← Game (1:42, -13.8, LRA 2.0, 밝음 943). 레벨이 일정하고 밝은 그루브라 메뉴를 오래 열어 둬도 거슬리지 않는다.
- `cloud` ← Cloud (0:37, 조용함, C장조 추정). 짧고 가벼운 곡이라 메뉴 로테이션의 쉬어가는 곡으로 쓴다.
- `echoes_of_the_marble_hall` ← Echoes of the Marble Hall (1:51). 제목이 저택의 체크무늬 대리석 홀이고, 폭 0.65의 넓은 공간감이 있다. 저택 도착 장면에 쓴다.
- `meeting_the_king` ← Meeting the King.wav (1:01, -10, 뚜렷한 비트가 없음 = 펄스 1.29). 유스티가 '신들의 게임'을 선포하는 장면이다.
- `beneath_the_iron_sky` ← Beneath the Iron Sky (2:43, 시네마틱). 프롤로그 끝의 큰 서술부에 쓴다.

**일상(Daily)**
- `happy` (3:34, -15.4, 밝고 LRA 3.0), `an_ordinary_life` ("평범한 일상", 어둡지 않은 저역 중심, 서서히 페이드인), `my_apple` (밝고 일정): 아침에 쓴다.
- `an_ordinary_life_2` (같은 주제의 다른 버전, 3:30), `ceo` (-15.8, 밝은 그루브, 레벨이 일정해 대사에 방해되지 않음), `korean` (1:07, 밝음), `boomba` (다이내믹하고 경쾌한 제목), `drift_bawl` (2:25, 12 s 인트로 뒤 밝은 본편): 낮에 쓴다. 가장 오래 머무는 상태라 5곡으로 반복감을 줄였다.
- `faded_ink` (고역이 거의 없는 어두운 톤), `piano_pl` (피아노, 일정한 박), `efefef` (3:44, 펄스 명확도 9.6의 차분한 반복): 저녁에 쓴다.

**밤 · 긴장 · 미스터리**
- `midnight_in_the_ruins` / `_2`: 제목 그대로 밤에 쓴다. 온셋 1.1/초로 조용하다.
- `tired` (3:07, -16.6, 고역이 -79 dB로 거의 없음): 밤이 깊은 시간에 쓴다.
- `conspiracy_in_the_dark` ("어둠 속의 음모", -17 dB에서 시작해 -11 dB로 빌드업): 누군가 살인을 계획하는 시간에 쓴다.
- `murder` + `murder_kick_ver`: 1절의 스템 쌍이다. 긴장이 높아질수록 킥 레이어가 올라오고, 시체 발견 때는 전부 나온다.
- `deep` (-18.2로 가장 조용함, 초저역 드론): 긴장 상태의 바닥음으로 쓴다.
- `ghost` (저역이 없고 넓으며 크레스트 19 dB로 성김), `bat` (1:04, 평탄하고 루프형), `people` (펄스가 없는 루바토, 넓음, 텅 빈 강당에 어울림), `yeat` (조용한 크레셴도): 미스터리 방 앰비언스에 쓴다.

**시체 발견 · 수사**
- `geugeol_bwasseo` ← 그걸 봤어_ ("그걸 봤어"): 첫 타격 뒤 가라앉는 구조로, 발견의 충격 다음 장면에 쓴다.
- `inspection` (제목, 온셋 4.7/초로 계속 움직이지만 -12.8로 과하지 않음), `mystery` ← Mystery (조용하게 시작해 빌드업, LRA 11.6), `wolf` (늑대 게임, 밝고 일정한 그루브): 수사에 쓴다.
- `points_of_doubt` ← Points of doubt (1:01, -7.7, 촘촘함), `cyber` (-8.4, 끝과 처음 유사도 0.99의 반복형 테크노), `track_p` ← -P (고역 없이 어둡고 일정한 그루브): 시간이 촉박한 수사 후반에 쓴다.

**재판**
- `judgment`: 제목이 곧 재판이다. 가장 밝은 곡(centroid 1345)이며 개정 테마로 쓴다.
- `saul_theme` (변호사 Saul): 법정 개정에 쓴다.
- `beneath_the_iron_sky_v2`: 엘리베이터로 법정에 내려가는 서사적 등장에 쓴다.
- `debate` (144 BPM, -6.8, 끝으로 갈수록 커짐): 논쟁이다.
- `judgment_guitar`, `judgment_2`, `judgment_2_guitar`: 같은 테마의 편곡들로, 대사 밑에 깔기 좋게 레벨이 일정하다(LRA 2.0–3.2). 논쟁에 쓴다.
- `jinjja` ← 진짜_ ("진짜?"), `ai` (152 BPM, -7.4), `female` (8.5 s 빌드 뒤 밝고 강함): 모순 지적과 반론 같은 압박 장면에 쓴다.
- `boss` ("보스전" = 범인과의 최종 대결, 단조 추정), `final`, `eege` (26 s 조용한 도입 뒤 폭발, LRA 13.8): 클라이맥스와 최종 재구성에 쓴다.
- `gaze` ("시선", 7 s 빌드업 뒤 -6.7): 모두가 서로를 보며 투표하는 장면에 Hold로 쓴다.

**판결 이후**
- `i_dont_regret_it` ← I don't regret it: 범인의 자백 "후회하지 않아"다. 16초 동안 조용하다가 폭발한다.
- `glow` (2:34, 절반은 조용하고 끝에서 +10 dB 클라이맥스): 담담한 자백에 쓴다. 이 둘이 VerdictCorrect다.
- `symphonic_suite_aot` (A#단조로 추정되는 교향적 비극), `kill_your_self` (단조 추정, 무겁고 모노에 가까움, 8 s 페이드아웃): 오판 뒤 절망에 쓴다.
- 처형(기괴·몽환·아이러니): `gore` (-5.2로 가장 크고 1.8 LU로 눌린 반복형), `amu_geokjeong_eopseo` ← 아무 걱정이 필요 없어 (완전히 평탄한 -6 LUFS, 136 BPM, "걱정 마"라는 반어적 경쾌함이 단간론파 처형곡 문법과 맞음), `jugeo` ← 죽어, `wah` (모노에 가깝고 초저역 괴물, 크레스트 5.9), `scru` ← SCRU_.
- 도주(범인이 소원을 들고 도망침): `bandit_stroy` ("도적 이야기"), `pedal` (148 BPM 질주), `fu` (범인의 조롱, 끝 12 s 무음은 트림함), `raid` (4마디 반복 루프형), `give_me` ("내 소원을 줘").
- 진상 재현(Reveal): `geugeoyeotgun` ← 그거였군 ("그거였군", 3D 리플레이를 보는 플레이어의 대사 그 자체), `modeun_geol_ara` ← 모든 걸 알아 (밝은 계열 중 유일하게 단조로 추정, 일정함).
- 여파(Aftermath): `tell_me_my_name` / `_2` (고역이 거의 없는 여린 곡), `faded_ink_1`, `pianop` ← pianop (1) (피아노, 16 s 여운), `hope` ← hope.wav (슬픔 끝의 희망, 다음 챕터로 넘어감).
- 루프 리셋: `future` (펄스 명확도 21.8로 전곡 중 가장 기계적인 반복, 시계태엽 같은 '되감기' 느낌, 끝 9 s 페이드) → `tell_me_my_name_3` (3.5 s 거의 무음으로 시작, "내 이름을 말해줘": 정체성 상실) → `i_cant_feel_it` ("아무것도 못 느껴"). 루프형이고, "모든 게 너무 정상"인 섬뜩함을 노렸다.
- `cong` ← Cong (0:22): "Congratulations"로 읽었다. 22초짜리 장조(추정) 징글이라 **챕터 클리어 스팅어**로 쓴다(`MusicDirector.I.Stinger("chapter_clear")`).

## 4. 쓰지 않은 곡

| 곡 | 이유 |
|---|---|
| Hey.mp3 (0:33, -8.0) | 33초짜리 저역 비트 훅이다. 상태 BGM으로는 너무 짧아 금방 반복되고, 스팅어로 쓰기에는 길고 강렬한 타격이 없다. 그루브는 gore·raid 계열과 같다. 미니게임 같은 상태가 생기면 1순위 후보다. |
| 523.mp3 (0:26, -15.4) | 26초짜리 스케치 같은 조각이다. 중간에 급격한 레벨 붕괴가 있고(-24 → -31 dB 뒤 복귀) 반복하기 어렵다. |

## 5. 기술 메모

- **음량**: 곡마다 `GainDb = min(0, -16 - LUFS)`로 맞춘다(증폭은 하지 않음). gore는 -10.8 dB, deep은 0 dB. 여기에 상태 볼륨(일상 0.72 … 클라이맥스 1.0)과 플레이어 설정(마스터×음악)을 곱한다.
- **트림**: 앞 무음을 건너뛰고(`Start`), 끝 무음 전에 다음 곡을 시작한다(`End`). 자연 페이드가 있으면 그 길이(`Tail`)의 70%만큼 겹친다(최대 8 s).
- **루프**: `open`·`yeat`는 파일 자체로 무이음 루프(AudioSource.loop)한다. `bat`·`raid`·`i_cant_feel_it`는 0.3 s 짧은 자기 크로스페이드로 루프한다. 나머지는 곡 끝에서 다음 곡으로 크로스페이드한다.
- **이어듣기**: 상태를 떠날 때 곡 위치를 기억해 두고, 150 s 안에 그 상태로 돌아오면 같은 곡을 이어서 재생한다(메뉴 → 게임 복귀 등). 새 상태의 곡 목록에 지금 곡이 있으면 끊지 않고 그대로 이어간다(Tension → BodyDiscovery의 `murder`).
- **WAV 처리**: 24-bit WAV 5개는 TPDF 디더를 넣어 16-bit/48 kHz WAV로 바꿨다(널 테스트 SNR 83 dB, LUFS·스펙트럼 차이 0.00 dB). 용량은 130 → 86 MB다. Glow.wav는 원래 16-bit라 그대로 복사했다(30 MB). OGG 변환은 **하지 않았다**. 설치 없이 쓸 수 있는 관리형 Vorbis 인코더(OggVorbisEncoder)를 널 테스트해 보니 q1.0에서 구간별 SNR이 2–17 dB로 불안정했기 때문이다. 어차피 Unity가 빌드할 때 Vorbis로 인코딩한다.
- **임포트**: `Editor/Audio/BL23AudioImportRules.cs`가 Resources/Music에 Vorbis q0.55, Streaming, 프리로드 없음을 설정한다. 동기 스템 두 곡(murder 쌍)만 CompressedInMemory다. 메뉴 `BASSLINE/BL23/Audio/Reimport Music With BL23 Rules`로 재적용할 수 있다.
- **용량**: Resources/Music은 약 323 MB(mp3 207 MB + wav 116 MB)다. 총 재생시간은 약 2.7시간이고, 빌드에서는 Vorbis 0.55로 대략 150–190 MB로 추정한다. 줄여야 하면 임포트 규칙의 quality를 0.4로 내린다.

## 6. 사용자(작곡가) 확인 권장

소리를 직접 듣지 않고 수치와 제목으로 판단한 배치다. 특히 아래는 한 번 들어보고 확인하길 권한다.
1. 가사나 보컬이 있는 곡이 대사가 많은 상태(Daily, Investigation, TrialDebate)에 들어갔는지. 보컬 여부는 수치로 확정할 수 없었다. 문제가 되면 해당 곡을 Execution이나 Escape로 옮기면 된다(MusicLibrary.BuildStates 한 줄 수정).
2. `murder` + `murder kick ver`를 동시에 재생했을 때 의도한 한 곡처럼 들리는지(스템이라는 추정).
3. `cong`이 정말 축하 징글인지. 아니라면 `_stingerFiles`에서 한 줄을 지우면 합성 팡파르로 대체된다.
4. `kill_your_self`는 VerdictWrong(절망)에 배치했다. 곡 제목은 게임 안에 표시되지 않는다.

## 7. 부록: 전 곡 수치 (분석 도구 출력)

BPM과 조성은 자동 추정이라 2배·절반 오차나 관계조 오차가 있을 수 있다. centroid는 파워 가중 스펙트럼 중심(Hz)이고, 저역비는 120 Hz 미만 에너지 비율이다.

| 원본 | 리소스 | 길이 | LUFS | LRA | BPM | 조성 | centroid | 저역비 | 온셋/s |
|---|---|---|---|---|---|---|---|---|---|
| -P.mp3 | `track_p` | 1:08 | -9.8 | 1.2 | 117 | Fmaj | 397 | 0.412 | 1.60 |
| 523.mp3 | (미사용) | 0:25 | -15.4 | 4.1 | 97 | C#maj | 160 | 0.895 | 2.15 |
| An ordinary life (2).mp3 | `an_ordinary_life_2` | 3:30 | -12.7 | 5.0 | 141 | F#maj | 183 | 0.769 | 1.74 |
| An ordinary life.mp3 | `an_ordinary_life` | 2:43 | -12.6 | 5.2 | 141 | F#maj | 136 | 0.840 | 1.70 |
| BOSS.mp3 | `boss` | 2:06 | -8.5 | 3.7 | 92 | Amin | 518 | 0.724 | 2.98 |
| Bandit stroy.mp3 | `bandit_stroy` | 2:34 | -11.2 | 9.0 | 108 | A#min | 231 | 0.743 | 1.98 |
| Beneath the Iron Sky ver 2.mp3 | `beneath_the_iron_sky_v2` | 2:53 | -11.4 | 5.1 | 94 | F#maj | 370 | 0.709 | 2.66 |
| Beneath the Iron Sky.mp3 | `beneath_the_iron_sky` | 2:43 | -12.2 | 4.9 | 83 | F#maj | 357 | 0.699 | 2.34 |
| CEO.mp3 | `ceo` | 3:12 | -15.8 | 2.5 | 91 | C#maj | 745 | 0.626 | 4.81 |
| Cloud.mp3 | `cloud` | 0:37 | -15.3 | 10.2 | 129 | Cmaj | 200 | 0.931 | 2.52 |
| Cong.mp3 | `cong` | 0:22 | -8.5 | 4.0 | 129 | Fmaj | 264 | 0.710 | 2.29 |
| Echoes of the Marble Hall.mp3 | `echoes_of_the_marble_hall` | 1:51 | -11.9 | 5.8 | 144 | Fmaj | 394 | 0.563 | 1.81 |
| FU.mp3 | `fu` | 1:59 | -9.5 | 9.4 | 152 | Amin | 422 | 0.345 | 1.85 |
| Faded Ink (1).mp3 | `faded_ink_1` | 3:02 | -13.0 | 5.8 | 100 | F#maj | 177 | 0.714 | 1.41 |
| Faded Ink.mp3 | `faded_ink` | 2:18 | -13.0 | 7.6 | 100 | F#maj | 161 | 0.780 | 1.54 |
| Game.mp3 | `game` | 1:42 | -13.8 | 2.0 | 91 | F#maj | 943 | 0.536 | 6.19 |
| Gaze.mp3 | `gaze` | 2:11 | -6.7 | 3.9 | 99 | Fmaj | 522 | 0.639 | 3.92 |
| Give me.mp3 | `give_me` | 3:32 | -8.0 | 7.1 | 70 | Fmaj | 442 | 0.792 | 3.69 |
| Glow.wav | `glow` | 2:34 | -16.9 | 9.1 | 97 | A#min | 304 | 0.797 | 1.52 |
| Hey.mp3 | (미사용) | 0:33 | -8.0 | 2.0 | 129 | Fmaj | 135 | 0.716 | 4.83 |
| I can't feel it.mp3 | `i_cant_feel_it` | 0:59 | -13.9 | 2.1 | 117 | F#maj | 625 | 0.584 | 4.53 |
| I don't regret it.mp3 | `i_dont_regret_it` | 2:52 | -7.2 | 6.4 | 152 | Amin | 652 | 0.727 | 2.75 |
| Kill your self.mp3 | `kill_your_self` | 2:33 | -9.9 | 6.0 | 83 | Amin | 178 | 0.837 | 2.89 |
| Meeting the King.wav | `meeting_the_king` | 1:01 | -10.0 | 3.6 | 88 | F#maj | 411 | 0.441 | 3.58 |
| Midnight in the Ruins (2).mp3 | `midnight_in_the_ruins_2` | 3:18 | -14.1 | 5.2 | 65 | F#maj | 228 | 0.779 | 1.09 |
| Midnight in the Ruins.mp3 | `midnight_in_the_ruins` | 2:29 | -12.3 | 4.3 | 122 | C#maj | 206 | 0.760 | 1.13 |
| My Apple.mp3 | `my_apple` | 2:18 | -15.5 | 2.0 | 97 | F#maj | 981 | 0.595 | 4.64 |
| Mystery.mp3 | `mystery` | 2:20 | -10.7 | 11.6 | 81 | Fmaj | 348 | 0.849 | 2.41 |
| Points of doubt.mp3 | `points_of_doubt` | 1:01 | -7.7 | 3.3 | 108 | Fmaj | 269 | 0.433 | 4.21 |
| SCRU_.mp3 | `scru` | 1:50 | -6.6 | 8.8 | 129 | Fmaj | 669 | 0.814 | 3.59 |
| Saul theme.mp3 | `saul_theme` | 2:22 | -15.6 | 5.8 | 104 | A#min | 685 | 0.688 | 5.13 |
| SymphonicSuite [AoT] Part1-4th7-b@$ (1).mp3 | `symphonic_suite_aot` | 1:44 | -13.6 | 6.8 | 104 | A#min | 195 | 0.754 | 1.26 |
| Tell me my name (2).mp3 | `tell_me_my_name_2` | 1:34 | -14.7 | 8.8 | 97 | F#maj | 164 | 0.845 | 1.08 |
| Tell me my name (3).mp3 | `tell_me_my_name_3` | 1:44 | -12.9 | 9.9 | 69 | F#maj | 222 | 0.431 | 1.32 |
| Tell me my name.mp3 | `tell_me_my_name` | 1:44 | -14.2 | 6.8 | 94 | A#min | 212 | 0.765 | 0.99 |
| Tired.mp3 | `tired` | 3:07 | -16.6 | 11.0 | 134 | F#maj | 365 | 0.293 | 1.79 |
| WAH.mp3 | `wah` | 1:57 | -8.3 | 2.3 | 117 | Fmaj | 200 | 0.942 | 3.30 |
| Wolf.mp3 | `wolf` | 1:56 | -13.6 | 3.0 | 97 | F#maj | 928 | 0.539 | 6.30 |
| ai.mp3 | `ai` | 1:42 | -7.4 | 6.3 | 152 | Fmaj | 544 | 0.717 | 2.29 |
| bat.mp3 | `bat` | 1:04 | -11.6 | 0.7 | 117 | Fmaj | 640 | 0.054 | 2.11 |
| boomba.mp3 | `boomba` | 1:49 | -16.0 | 4.1 | 112 | Cmaj | 596 | 0.370 | 3.49 |
| conspiracy in the dark.mp3 | `conspiracy_in_the_dark` | 1:54 | -12.4 | 8.9 | 76 | F#maj | 394 | 0.537 | 2.25 |
| cyber.mp3 | `cyber` | 2:00 | -8.4 | 4.5 | 108 | Fmaj | 451 | 0.625 | 4.84 |
| debate.mp3 | `debate` | 1:39 | -6.8 | 4.5 | 144 | Fmaj | 635 | 0.802 | 4.62 |
| deep.mp3 | `deep` | 1:38 | -18.2 | 6.3 | 92 | Fmaj | 176 | 0.940 | 2.24 |
| drift bawl.wav | `drift_bawl` | 2:25 | -10.4 | 6.6 | 80 | C#maj | 588 | 0.828 | 4.05 |
| eege.mp3 | `eege` | 3:06 | -8.9 | 13.8 | 144 | Fmaj | 524 | 0.781 | 2.34 |
| efefef.mp3 | `efefef` | 3:44 | -12.7 | 4.9 | 134 | C#maj | 331 | 0.726 | 3.39 |
| female.mp3 | `female` | 2:15 | -7.9 | 6.2 | 112 | Fmaj | 936 | 0.641 | 4.11 |
| final.mp3 | `final` | 1:49 | -7.6 | 4.9 | 112 | Fmaj | 799 | 0.782 | 4.86 |
| future.mp3 | `future` | 2:58 | -10.2 | 1.5 | 70 | Amin | 392 | 0.108 | 1.08 |
| ghost.mp3 | `ghost` | 3:32 | -16.4 | 7.2 | 108 | Amin | 845 | 0.035 | 2.16 |
| gore.mp3 | `gore` | 0:52 | -5.2 | 1.8 | 123 | Fmaj | 101 | 0.754 | 3.88 |
| happy.mp3 | `happy` | 3:34 | -15.4 | 3.0 | 97 | C#maj | 705 | 0.681 | 5.12 |
| hope.wav | `hope` | 2:20 | -9.6 | 3.0 | 88 | F#maj | 295 | 0.458 | 4.76 |
| inspection.mp3 | `inspection` | 2:44 | -12.8 | 4.9 | 94 | C#maj | 278 | 0.805 | 4.72 |
| judgment 2 guitar.mp3 | `judgment_2_guitar` | 3:29 | -15.9 | 2.4 | 148 | F#maj | 1161 | 0.500 | 5.21 |
| judgment 2.mp3 | `judgment_2` | 2:58 | -13.7 | 3.2 | 97 | F#maj | 770 | 0.698 | 5.56 |
| judgment guitar.mp3 | `judgment_guitar` | 3:14 | -16.3 | 2.0 | 97 | F#maj | 1235 | 0.483 | 5.45 |
| judgment.mp3 | `judgment` | 1:44 | -13.2 | 3.7 | 97 | F#maj | 1345 | 0.690 | 5.93 |
| korean.mp3 | `korean` | 1:07 | -14.3 | 3.7 | 97 | F#maj | 804 | 0.530 | 4.30 |
| murder kick ver.mp3 | `murder_kick_ver` | 1:17 | -7.8 | 4.1 | 86 | Cmaj | 65 | 0.975 | 0.57 |
| murder.mp3 | `murder` | 1:17 | -17.4 | 14.3 | 86 | Gmin | 1110 | 0.000 | 0.86 |
| open.wav | `open` | 0:30 | -12.5 | 1.0 | 128 | F#maj | 495 | 0.114 | 0.77 |
| pedal.mp3 | `pedal` | 2:38 | -7.4 | 5.8 | 148 | F#maj | 681 | 0.786 | 3.06 |
| people.mp3 | `people` | 1:36 | -16.2 | 13.1 | 81 | Amin | 959 | 0.073 | 2.49 |
| piano PL.mp3 | `piano_pl` | 3:23 | -15.5 | 4.1 | 122 | F#maj | 363 | 0.563 | 2.77 |
| pianop (1).mp3 | `pianop` | 2:27 | -14.9 | 11.4 | 122 | F#maj | 491 | 0.243 | 2.06 |
| raid.mp3 | `raid` | 1:36 | -8.0 | 4.9 | 60 | Fmaj | 270 | 0.501 | 1.24 |
| yeat.wav | `yeat` | 1:12 | -17.9 | 7.7 | 134 | C#maj | 362 | 0.518 | 2.26 |
| 그거였군.mp3 | `geugeoyeotgun` | 1:39 | -14.0 | 6.6 | 144 | Amin | 267 | 0.759 | 3.53 |
| 그걸 봤어_.mp3 | `geugeol_bwasseo` | 1:22 | -13.2 | 6.3 | 134 | F#maj | 348 | 0.847 | 4.89 |
| 모든 걸 알아.mp3 | `modeun_geol_ara` | 2:56 | -16.4 | 2.0 | 97 | C#min | 1012 | 0.580 | 5.10 |
| 아무 걱정이 필요 없어.mp3 | `amu_geokjeong_eopseo` | 1:43 | -6.0 | 1.1 | 136 | Fmaj | 492 | 0.832 | 4.34 |
| 죽어.mp3 | `jugeo` | 2:01 | -9.5 | 5.9 | 78 | Fmaj | 631 | 0.614 | 2.34 |
| 진짜_.mp3 | `jinjja` | 2:38 | -9.0 | 4.2 | 136 | Cmaj | 520 | 0.798 | 3.30 |

## 8. API 요약

```csharp
using BL23.Game.Audio;
MusicDirector.I.SetState(MusicState.TrialDebate);          // 크로스페이드, 반복 회피, 이어듣기
MusicDirector.I.SetState(MusicState.DailyDay, restart:true); // 같은 상태에서 다른 곡으로
MusicDirector.I.SetIntensity(0.8f, 4f);                     // Tension: murder 킥 스템 올리기
MusicDirector.I.Stinger("discovery");                       // break/objection/verdict/gavel/chime/evidence/loop_reset/chapter_clear
MusicDirector.I.Duck(true); ... MusicDirector.I.Duck(false); // 대사 더킹
MusicDirector.I.SetVolume(master, music, sfx);              // PlayerPrefs 저장
Sfx.Play("glass_shatter", pos); Sfx.Footstep("Wood", pos);
var h = Sfx.Loop("rain_loop", corridorPos); h.Stop(1.5f);
Sfx.PlayEx("scream_muffled", pos, 1f, 1f, -1, lowpassHz: 900f); // 벽 너머
VoiceBabble.Speak("P03", "잠깐, 그건 이상하잖아?");            // 캐릭터별 음성 블립
```
