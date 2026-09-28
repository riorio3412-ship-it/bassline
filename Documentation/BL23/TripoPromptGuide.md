# Tripo 캐릭터 이미지 프롬프트 가이드 (2026-09-28)

Tripo에서 **이미지 생성 → 그 이미지로 3D 생성** 순서로 캐릭터를 만들 때 쓰는 프롬프트다.
- 목표 1: 18명 전원의 **작화(화풍)와 비율을 통일**한다.
- 목표 2: 게임 파이프라인(얼굴 B 방식, 리깅)에 맞는 형태로 뽑는다.

**사용법:** 프롬프트 = **[공통 스타일] + [기술 조건] + [캐릭터 설명]**
- 앞의 두 블록은 **매번 글자 하나 바꾸지 말고 그대로** 붙인다. 이것이 작화 통일의 핵심이다.
- 바꾸는 것은 [캐릭터 설명]뿐이다.
- 영어로 쓴다. 이미지 모델은 영어 프롬프트를 더 정확히 따른다.

---

## 1. 공통 스타일 (매번 그대로)

```
Semi-realistic anime style 3D game character concept, consistent art style for a whole cast.
Soft painterly shading, clean and refined facial features, natural adult face proportions, medium-sized eyes (not oversized).
Believable adult anatomy (joints, hands and neck of an adult); head-to-body ratio as given in the character description.
Muted, rich color palette with dark elegant tones (deep green, oxblood, charcoal, ivory, brass accents), subtle gothic Victorian elegance.
Clean fabric surfaces with simple texture, no noise, no grunge.
```

## 2. 기술 조건 (매번 그대로 — 3D·리깅·얼굴 작업용)

```
Single character, full body from head to shoes, centered, front view, camera at chest height, no perspective distortion.
Standing straight in A-pose: arms relaxed and angled about 40 degrees away from the body, not touching the torso, palms facing the thighs, feet shoulder-width apart.
Hands open, fingers slightly apart, thumbs clearly separated, empty hands, holding nothing.
Neutral expression, eyes open looking straight at the viewer, mouth closed, no teeth visible.
Hair does not cover the eyes or the eyebrows; bangs styled above the eyebrows; hair kept off the face and away from the neck.
Fitted clothing that follows the body, sleeves separate from the torso, no loose sashes or flowing ribbons. If the outfit has a cape or cloak, it is fastened at the collar and hangs straight down BEHIND the back, never covering or touching the arms; both arms fully visible. A scarf stays short and close to the neck. A hat sits on top of the head without shading the eyes.
Flat, even, soft studio lighting from the front; no dramatic shadows, no rim light, no strong highlights.
Plain pure white background, no props, no floor pattern, no text, no watermark.
```

## 3. 캐릭터 설명 (인물마다 바꾸는 부분 — 이 순서로 채운다)

```
[성별], adult, [나이: e.g. in his late twenties], [키 느낌: e.g. very tall (187 cm), slim / short (160 cm), petite adult],
[체형: e.g. broad but not bulky build],
hair: [길이·스타일·색 — 반드시 "bangs above the eyebrows" 유지],
eyes: [눈 색], face: [특징: 점·흉터·주근깨 등 위치까지],
outfit: [상의·하의·신발 — 몸에 맞게, 색 명시],
accessories: [작고 몸에 붙는 것만 — 목걸이, 반지, 머리띠 등],
personality in the look: [e.g. gentle and warm / cold and calculating — 표정은 무표정 유지, 분위기만]
```

**채울 때 규칙:**
- **"adult"와 나이를 꼭 넣는다.** 전원 성인이다. 동안이라도 아이처럼 보이면 안 된다.
- 키는 cm와 단어를 함께 쓴다(very tall / average / short). 이미지 모델은 cm만으로는 잘 반영하지 않는다.
- **망토, 긴 스카프, 큰 모자, 안경, 마스크**가 설정에 있어도 이미지에는 넣지 않는다. 이런 소품은 게임에서 따로 붙인다(3D에서 몸과 붙어 버리기 때문). 권태겸은 안경 없음이 확정이다.
- 흉기나 소지품도 들리지 않는다. 손은 비워 둔다.
- 얼굴의 점 같은 특징은 위치를 정확히 쓴다(예: a small mole under the LEFT eye).

## 4. 네거티브 프롬프트 (입력칸이 있으면)

```
chibi, child, kid, teenager, school uniform, childlike face, oversized eyes, big head, tiny body,
T-pose, dynamic pose, action pose, arms touching body, crossed arms, hands in pockets, holding object, weapon,
cape covering the arms, cloak wrapped around the body, long trailing scarf, flowing ribbons, huge shoulder pads, wings,
hair covering eyes, bangs over eyes, hair over face, glasses, mask, hat covering face,
open mouth, teeth, grin, exaggerated expression,
dramatic lighting, strong shadows, rim light, backlight, colored lighting,
background scenery, floor shadow, props, text, watermark, logo,
multiple characters, cropped, cut off feet, extra fingers, fused fingers, missing thumb, deformed hands
```

## 5. 예시 — 강준서 (P10)

```
Semi-realistic anime style 3D game character concept, consistent art style for a whole cast.
Soft painterly shading, clean and refined facial features, natural adult face proportions, medium-sized eyes (not oversized).
Believable adult anatomy (joints, hands and neck of an adult); head-to-body ratio as given in the character description.
Muted, rich color palette with dark elegant tones (deep green, oxblood, charcoal, ivory, brass accents), subtle gothic Victorian elegance.
Clean fabric surfaces with simple texture, no noise, no grunge.
Single character, full body from head to shoes, centered, front view, camera at chest height, no perspective distortion.
Standing straight in A-pose: arms relaxed and angled about 40 degrees away from the body, not touching the torso, palms facing the thighs, feet shoulder-width apart.
Hands open, fingers slightly apart, thumbs clearly separated, empty hands, holding nothing.
Neutral expression, eyes open looking straight at the viewer, mouth closed, no teeth visible.
Hair does not cover the eyes or the eyebrows; bangs styled above the eyebrows; hair kept off the face and away from the neck.
Fitted clothing that follows the body, sleeves separate from the torso, no loose sashes or flowing ribbons. If the outfit has a cape or cloak, it is fastened at the collar and hangs straight down BEHIND the back, never covering or touching the arms; both arms fully visible. A scarf stays short and close to the neck. A hat sits on top of the head without shading the eyes.
Flat, even, soft studio lighting from the front; no dramatic shadows, no rim light, no strong highlights.
Plain pure white background, no props, no floor pattern, no text, no watermark.
Male, adult, in his late twenties, very tall (187 cm), broad but not bulky build,
hair: short tousled blond hair, bangs above the eyebrows, sides tucked behind the ears,
eyes: warm green-grey, face: gentle features, a small mole under his LEFT eye,
outfit: fitted bottle-green chef shirt with rolled sleeves, dark green apron with cream trim, charcoal trousers, brown leather shoes,
accessories: cream headband,
personality in the look: gentle, warm, caring (neutral expression)
```

## 6. 작화 통일 요령

1. **첫 캐릭터 1명을 확정한다.** 여러 번 뽑아서 마음에 드는 것을 기준으로 삼는다.
2. Tripo에 **참고 이미지(스타일 레퍼런스)** 기능이 있으면, 이후 모든 캐릭터에 그 확정 이미지를 넣는다. **시드 고정** 기능이 있으면 같은 시드를 쓴다.
3. [공통 스타일]과 [기술 조건] 블록은 절대 수정하지 않는다. 바꾸고 싶으면 **전원을 다시 뽑는다.**
4. 18명을 **나란히 놓고** 확인한다: 머리 크기, 눈 크기, 채색 방식, 어깨 너비가 같은가. 다른 사람은 다시 뽑는다.
5. 키 차이는 게임이 설정 키(cm)로 맞춘다. 이미지에서는 **비율(7.5등신)만 같으면** 된다.

## 7. 이미지 → 3D 이후 (HANDOFF "오너 3D 모델 얼굴" 참고)

- **3D 생성:** 최신 모델, 텍스처 최고 품질.
- **분할:** Segmentation v2 Detailed + Part Completion으로 머리, 머리카락, 옷, 장신구를 분리한다.
- **정리:** Retopology는 3만~6만 삼각형. 얼굴의 얼룩은 Magic Brush로 정리한다.
- **보낼 것:**
  - 분할한 GLB와 원본 GLB.
  - **생성에 쓴 이미지 원본(최대 해상도)**. 얼굴 크롭용이며, 얼굴 정면 클로즈업이 따로 있으면 함께 보낸다.
  - 사용한 프롬프트.
- **순서:** 한 명(예: 강준서)만 먼저 보내 파이프라인 테스트를 한 뒤, 나머지를 만든다.
