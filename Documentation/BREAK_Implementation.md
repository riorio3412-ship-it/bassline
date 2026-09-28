# BREAK 사용자 지침 적용

이번 대화의 BREAK 지침은 [USER CONFIRMED]. SourcePackage의 과거 IMG01–03과 이번 jpg 세 개를 같은 자료로 대응시키지 않습니다.

| 신규 ReferenceID | 파일 | 용도 |
| --- | --- | --- |
| USR_BREAK_REF01 | Desktop/1789646036.jpg | 정상 실루엣을 유지하면서 국소 표정/명암을 변주하는 방식만 참고 |
| USR_BREAK_REF02 | Desktop/1789646051.jpg | 동일. 캐릭터·의상·포즈·구도·그림체 복제 금지 |
| USR_BREAK_REF03 | Desktop/1789726807.jpg | 동일. BASSLINE 인물 외형 승인 자료로 쓰지 않음 |

참고 그림은 Assets에 가져오지 않았습니다. 최종 sprite/overlay는 미제공이며 **MissingApprovedArt**가 올바른 현재 결과입니다.

| CueID | SpeakerID | 연출 지침 | duration 기본안 |
| --- | --- | --- | --- |
| Jinwoo_Mocking_01 | CH_02 | 친근한 웃음이 불쾌해지는 조롱/거짓말/압박. 국소 입·눈·손 강조, 과도한 광기 금지 | 0.85초 |
| Yusti_Panic_01 | PRES_YUSTI | 짧은 평정심 붕괴·당황. 눈/얼굴의 순간적인 단순화, 약한 블랙 코미디 가능 | 0.35초 |
| Doyoon_Silence_01 | CH_04 | 억제된 감정·정적인 위압. 눈 주변 그림자와 얼굴 정보 축소, 과장된 살인마 금지 | 0.90초 |

시간 수치는 [PRODUCTION PROPOSAL]입니다. 공통 검은 얼굴/흰 눈 효과, VHS/RGB/noise 효과는 자동 생성하지 않습니다. 다른 인물의 BREAK cue는 UnknownCue로 거부합니다. 정상 헤어·의상·체형·실루엣은 유지해야 하며 sprite review에서 인물 식별과 국소 변경을 검수해야 합니다.

## API

`VisualBreakPlayer`는 Unity와 A/Save/Bootstrap에 의존하지 않는 presentation 클래스입니다.

```csharp
var result = player.PlayVisualBreak("Jinwoo_Mocking_01");
```

Started, UnknownCue, WrongSpeaker, MissingApprovedArt, Busy, ReducedEffects, Disabled를 반환합니다. cue 자동 선택을 위해 실제 진위나 숨은 의도를 조회하지 않습니다. 호출은 이미 허가된 현재 public speaker의 연출 이벤트에서만 해야 합니다.

`VisualBreakBehaviour`는 선택적인 SpriteRenderer adapter입니다. NormalStanding과 별도의 FaceOverlay를 지정하고 ApprovedOverlays에 CueId/SpeakerId/BaseArtRevision/ApprovalRecordId/승인 Sprite를 연결합니다. overlay는 정상 sprite와 같은 기준점의 투명한 국소 이미지로 제작해야 합니다. 정상 sprite 자체를 교체하지 않으며 hair/outfit/silhouette 유지 여부는 승인 artwork 검사 대상입니다. approval 문자열이 존재한다는 것이 승인 문서의 진위를 자동 보증하지는 않습니다.

화자 변경, 승인 art 불일치, 비활성화, reduced effects 전환, duration 종료 때 overlay를 지웁니다. pause 동안 duration을 진행하지 않습니다. Busy 때 기존 cue를 덮어쓰지 않습니다. 일반 감정 sprite는 항상 별도 관리합니다. 저장에 presentation overlay를 세계 사실로 추가하지 않습니다.

실제 대화 Canvas, 현재 논쟁 pause token과 `PresentationPaused`, 접근성 설정과 `ReducedEffects`의 연결은 아직 없습니다. Controller EditMode tests에서 fake surface로 allowlist·재생 종료·pause·cleanup을 검사했고, 실제 세 인물 그림에 대한 시각 검수는 **NOT RUN**입니다.
