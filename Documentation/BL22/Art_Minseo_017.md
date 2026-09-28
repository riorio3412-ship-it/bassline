# 임민서 — 0.17 인물 제작 기록

원전: 통합기획 개발명세 P18 임민서(56쪽), 외형 기준과 모델 규격(60–61쪽). 20세 남성, 182cm, 노동자. 큰 체격과 바랜 작업 셔츠·낡은 디지털시계·보온병을 반영한다. 짧은 존댓말과 절제된 자세를 유지한다.

## 자산

- 2D: `Assets/BASSLINE/Characters/Standing/Resources/BASSLINE/Standing/CH_18.png`, 원본 1024×1536 RGBA. 내장 이미지 생성 도구로 신규 제작. 원본 알파를 보존했고 임의 확대나 별도의 배경 제거를 하지 않았다. 대화의 기존 상반신 프레이밍에 연결한다.
- 3D: `CharacterPresentationBuilder.Minseo.cs`가 +Z 정면·+Y 위·미터 단위로 개별 형상을 생성한다. 새 작업복 몸통, 열린 앞섶, 깃·주머니·봉제선, 접은 소매와 전완, 얼굴·머리, 작업 바지·작업화, 시계·보온병을 포함한다. Unity 메시 자산으로 저장해 실제 플레이어 빌드가 사용한다.
- 왼쪽 손목의 시계는 손 리그 아래에 둔다. 몸통에 합치지 않아 손의 자세를 따른다. 보온병은 오른쪽 허리 고리의 외형 소품이며, 아직 독립적인 음용·대여 가능 물체는 아니다. 시계도 현재 시각을 알려주는 기능 도구는 아니다.

## 직접 확인한 수정

첫 실제 3D 렌더에서 닫힌 셔츠 몸통이 티셔츠를 관통했다. 앞이 열린 셔츠 메시로 바꾸고 목 부분도 열었다. 어깨의 평평한 단면을 둥글게 다듬었다. 손바닥과 손가락 마디를 새 메시로 만들어 분리된 구슬처럼 보이던 틈을 줄였다. 정면·사선·측면·후면·얼굴·시계를 각각 실제 Unity 카메라로 렌더한다.

## 현재 한계와 후속 제작

이번 외형은 기존 공통 캡슐 인형에서 개별 인물로 발전시킨 중간 모델이다. 원전의 최종 Humanoid Avatar, 얼굴 표정, 의상 변형, LOD, 4재질 이내 아틀라스, 원본 .blend/교환 파일, 지정 삼각형 예산과 최종 질감을 충족했다고 세지 않는다. 2D의 선·그림자·얼굴 비율과 3D 조형의 일치도도 추가 개선해야 한다. 19인 모델 전체나 156개 스탠딩을 완료 처리하지 않는다.

## 생성 프롬프트

방식: 내장 `image_gen` 도구. 참고 이미지의 방향을 글로 풀어 신규 캐릭터를 생성했으며, 기존 캐릭터 그림을 편집하지 않았다.

```text
Use case: stylized-concept. Asset type: transparent full-body character standing sprite for the Korean mystery game BASSLINE. Create ONE original character, Lim Minseo: 20-year-old Korean man, tall 182 cm, broad shoulders and sturdy athletic working build, quiet alert demeanor, kind but reserved narrow dark grey eyes, straight thick eyebrows, angular youthful jaw, short charcoal brown hair with uneven side swept fringe and visible eyebrows, no glasses, no beard. His grounded pose is relaxed with both arms naturally by his sides, hands fully visible, no gesture, facing camera in a very slight three-quarter view. Clothing: faded dusty slate blue work shirt worn open over a warm ivory crewneck T-shirt, long sleeves rolled to just below elbows, two chest patch pockets, practical charcoal straight work trousers with subtle knee folds, worn dark brown lace-up work boots. A worn black rectangular digital wristwatch on LEFT wrist. A slim brushed steel thermos held in a dark canvas loop at RIGHT hip (no bag). Shirt shoulders broad but no exaggerated bodybuilder muscles. Art direction: expressive Japanese mystery visual novel illustration, confident slightly scratchy dark ink outlines, restrained hand-painted cel shading with a few textured brush marks, angular soft shadow planes, muted cool blue/grey clothing and warm pale tan skin, readable silhouette, modest face proportions, serious thoughtful neutral mouth; adult young man, not chibi, not photorealistic, not 3D render. Full body from top of hair to entire soles with at least 6% transparent margin on all edges, centered vertical portrait composition. True transparent alpha background, no scenery, no ground shadow, no frame, no text, no logo, no symbols, no watermarks, no alternate poses or extra characters. This is a NEW original character design, not a depiction of any existing franchise character.
```

최종 렌더 확인 과정에서 평면 티셔츠 옆으로 보이는 틈을 발견해 내부 티셔츠도 닫힌 입체 메시로 바꿨다. 넓어진 어깨에 맞춰 수거 접근 방향을 실제 오른쪽 어깨에서 계산한다. 팔 길이와 충돌 검사를 유지했으며 최종 관련 34개 PlayMode 검사는 모두 통과했다.
