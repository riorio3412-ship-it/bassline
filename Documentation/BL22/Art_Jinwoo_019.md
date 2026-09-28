# 김진우 — 0.19 인물 제작 기록

원전: 통합기획 개발명세 MAIN 40쪽의 P02. 20세 남성, 163cm, 심리학과 대학생. 흰 곱슬머리, 아주 옅은 눈, 버건디 카디건을 기준으로 기존 검은 머리 스탠딩을 교체했다. 속옷 셔츠·바지·운동화의 세부 형태는 제작상의 선택이며 새 서사 설정으로 간주하지 않는다.

## 자산과 연결

- 새 스탠딩: `Assets/BASSLINE/Characters/Standing/Resources/BASSLINE/Standing/CH_02_v019.png`. 1024×1536 RGBA, 내장 imagegen으로 신규 제작. 원본 알파를 그대로 보존했다. 알파 0인 픽셀 약 76.93%, 8 미만 약 77.97%, 240 이상 약 21.14%. 원본 RGB에 배경처럼 보이는 색이 있어도 해당 부분은 투명하다. 배경 제거·재확대·래스터 편집은 하지 않았다.
- 생성 원본: `C:/Users/리오/.codex/generated_images/01a0d8b6-7f77-7d03-9b6d-71587ea5514c/exec-f99ae843-82d6-4ada-901b-fa1b1cc28bf9.png`.
- 사용자 결과: `C:/Users/리오/Documents/Codex/2026-09-25/new-chat-3/outputs/김진우_스탠딩_v0.19.png`.
- 이전 CH_02.png를 보존하고 DialoguePortrait가 김진우에 한해 새 파일을 불러온다. 공유 Resources 텍스처를 대화 창 닫기에서 파괴하지 않는다.
- `CharacterPresentationBuilder.Jinwoo.cs`가 얼굴·눈·머리·열린 카디건·셔츠·깃·소매·손·바지·운동화 메시를 생성한다. 모델은 기존 실제 어깨/팔꿈치/손목 리그를 사용한다. 외형에 충돌체나 Rigidbody를 추가하지 않는다.

## 시각 검토

첫 Unity 실제 렌더에서 앞머리 반복이 지나치게 규칙적이고 평면 신발 앞코가 몸체 안으로 겹쳐 들어갔다. 앞머리의 방향·끝 높이·너비에 변화를 주고, 신발 앞코는 신발 표면을 따라가는 곡면 메시로 바꿨다. 정수리 곡선도 실제 키에 맞게 낮췄다. 카메라 렌더는 콘셉트 그림을 대신 보여주는 것이 아니라 실제 생성한 메시를 사용한다.

## 아직 남은 아트 작업

이는 공통 임시 인형을 개별 외형으로 바꾸는 중간 단계다. 최종 Humanoid Avatar, 표정/얼굴 리그, LOD, 4재질 아틀라스, 원본 .blend 및 교환 파일과 최종 텍스처 규격은 아직 충족하지 않았다. 얼굴 비율, 옷의 부드러운 주름과 관절 이음새, 곱슬머리의 자연스러운 실루엣, 2D와 3D의 그림자/선 일치도는 추가 개선 대상이다. 신규 스탠딩도 기본 자세 1장으로 표정 전체를 완료하지 않는다. 19인 모델과 156개 스탠딩의 요구량 및 미완료 상태를 유지한다.

## 생성 프롬프트

```text
Use case: stylized-concept. Asset type: transparent full-body standing sprite for the Korean mystery game BASSLINE. Create one original adult character Kim Jinwoo: Korean man, age 20, short stature 163 cm, slender natural adult proportions around seven heads tall. His defining design is fluffy WHITE CURLY HAIR, very pale silver-grey eyes, and a muted deep BURGUNDY knitted cardigan. Youthful angular-soft face, narrow attentive almond eyes, a subtle knowing closed-mouth smile, head tilted slightly as if studying the listener's response. Hair is layered into clear loose wavy curls, ivory white with soft cool grey shadow planes, not straight hair and not dark hair. Clothes: open V-neck burgundy cardigan with small dark buttons, ribbed cuffs and hem, over an ivory open-collar shirt without a tie; slim charcoal trousers; worn charcoal canvas lace-up low shoes with ivory rubber soles. No overcoat, no hoodie, no backpack, no bag, no extra accessories. Both hands fully visible and relaxed close to his sides, all fingers anatomically correct; slight natural asymmetry, relaxed standing pose. Expressive mystery visual novel illustration, confident slightly scratchy dark ink contours, restrained hand-painted cel shading, muted colours, visible soft cloth folds, subtle brush texture, clear silhouette and face. Not chibi, not photorealistic, not a 3D render. Full body with complete shoes and hair, centered portrait framing with at least 6 percent transparent margin on every edge. TRUE TRANSPARENT ALPHA background; no scenery, no gradient backdrop, no ground shadow, no text, no watermark, no frames, no additional characters. This is a new original character rather than an existing franchise character.
```
