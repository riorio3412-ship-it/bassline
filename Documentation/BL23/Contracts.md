# BL23 모듈 계약 (Integration Contracts)

작성: 2026-09-26, 디렉터(메인 세션). 병렬 작업자(서브에이전트)는 이 문서를 기준으로 코드를 만든다.
본편 프로젝트: `C:/Users/리오/OneDrive/Desktop/BASSLINE` (Unity 6000.6.0f1, URP 17.6, Linear color space).

## 0. 공통 규칙
- 런타임 코드는 `Assets/BASSLINE/BL23/Game/**` (asmdef `BL23.Game`, 참조: BL23.Sim, Unity.TextMeshPro, URP Runtime, RP Core Runtime, Newtonsoft). 네임스페이스 `BL23.Game.*`.
- 에디터 코드는 `Assets/BASSLINE/BL23/Editor/**` (asmdef `BL23.Editor`, glTFast/glTFast.Editor 참조 가능). 네임스페이스 `BL23.EditorTools.*`.
- 순수 시뮬레이션(커널)은 `Assets/BASSLINE/BL23/Sim/**` (asmdef `BL23.Sim`, noEngineReferences). **서브에이전트는 Sim 코드를 수정하지 않는다.** 필요하면 보고서에 요청으로 적는다.
- C# 9까지 사용(Unity 6). file-scoped namespace, record, global using 금지.
- 좌표: 커널 `P3(f, x, z)` → Unity `Vector3(x, Layout.FloorY(f), z)`. yaw(도) 0 = +Z(북) 바라봄, 90 = +X(동). Unity의 Y 회전과 동일.
- 셰이더는 URP용 HLSL 수작성(`Assets/BASSLINE/BL23/Shaders/`). Shader Graph 사용 금지(배치 빌드 재현성).
- 모든 런타임 생성은 코드로 한다(프리팹 없이도 빌드 가능). 에셋(텍스처/모델)은 `Assets/BASSLINE/BL23/Art/**` 아래.
- 외부 자산은 **CC0/공개 라이선스만**(Poly Haven, ambientCG, Kenney, Google Fonts OFL 등). 출처·라이선스를 `Documentation/BL23/AssetManifest.csv`에 적을 수 있도록 보고서에 목록을 남긴다. 유료/로그인 필요 자산 금지.
- 18명 + 유스티 모두 3D. **2D 스탠딩 스프라이트는 완전 폐기**(사용자 확정). UI 초상도 3D 렌더로만.

## 1. 아트 디렉션 — "다채롭고, 기괴하고, 몽환적인 저택"
- 기본: 고딕/아르누보 저택. 체크무늬 대리석 바닥, 짙은 목재 징두리벽, 다마스크 벽지, 촛불 샹들리에, 달 문양 스테인드글라스.
- 몽환: 방마다 보석톤 팔레트(Amethyst, BloodOpera, TealAbyss, RosePorcelain, GildedRot, MoonMint, Absinthe, Nocturne, CoralFlesh, BoneIvory, CobaltCandle, PeachMold). 채도 높고 명암 대비 강하게.
- 기괴(은근하게, 공포 게임의 점프스케어가 아니라 '어딘가 틀린' 감각): 벽지 무늬 속 눈, 너무 높은 문, 얼굴이 긁혀 지워진 초상화, 벽을 타고 흐르는 자홍 물감 드립(참고 이미지), 살점 같은 유기적 기둥, 공중을 헤엄치는 물고기(유스티의 어항 모티프), 바늘이 틀린 시계, 인형, 이빨 모양 장식 몰딩, 넘쳐 굳은 촛농.
- 조명: 촛불의 따뜻한 빛 웅덩이 + 창으로 드는 차가운 달빛 + 단간론파 V3풍 네온 마젠타/시안 악센트. 가짜 볼류메트릭 광선(가산 반투명 콘 메시), 안개.
- 포스트: Bloom, Vignette, 약한 Chromatic Aberration, Film Grain, Color Adjustments, Split Toning(그림자 마젠타/하이라이트 시안).
- 인물: 셀셰이딩(툰 램프 2~3단, 림 라이트, 잉크 외곽선). 제공 GLB(민혁·진우·도윤)도 같은 툰 셰이더로 통일해 이질감을 줄인다.
- UI: 단간론파 V3 참고(네온 마젠타·시안, 비스듬한 패널, 스테인드글라스 조각), 그러나 BASSLINE 고유 디자인.

## 2. 캐릭터 모듈 `BL23.Game.Characters` (폴더 `Game/Characters`)
```csharp
public enum Posture { Stand, Sit, Crouch, Kneel, LieBack, LieFront, LieSide, Slumped }
public enum Gesture { None, Talk, TalkEmphatic, Point, Think, CrossArms, Shrug, Nod, ShakeHead, HandOnChest, Wave, Bow,
                      Surprised, Flinch, Cower, Angry, Cry, Laugh, Listen, LookAround, Present, Slam, Clap, Pray }
public enum ActionAnim { None, PickUp, PutDown, Use, Operate, Eat, Drink, Cook, Read, Write, Clean, Wash, Knock, OpenDoor,
                         Stab, Slash, Overhead, Shove, Strangle, Carry, Drag, Struggle, Fall, Stagger, Crawl, Hurt, FirstAid, Play, Sleep }
public enum Expr { Neutral, Smile, Grin, Angry, Sad, Surprised, Fear, Smirk, Disgust, Blank, Dead, Pain, Crying, Laugh, Break }
public enum BodyRegion { Head, Neck, Chest, Abdomen, ShoulderL, ShoulderR, ArmL, ArmR, HandL, HandR, LegL, LegR, FootL, FootR, Back }

public static class ActorFactory { public static ActorRig Create(BL23.Sim.CastDef def, Transform parent); } // 19명 모두
public class ActorRig : MonoBehaviour {
  public string ActorId; public float Height;
  public Transform Hips, Spine, Chest, Neck, Head, HandL, HandR, FootL, FootR;
  public Transform HandAnchorL, HandAnchorR;   // 들고 있는 물건을 붙일 앵커(손바닥, +Z가 도구 방향)
  public Transform EyeAnchor;                  // 카메라가 볼 눈 높이 중앙
  public Transform ChestAnchor, HeadTopAnchor;
  public ActorAnimator Anim;
  public void SetExpression(Expr e, float intensity = 1f);
  public void SetTalking(bool on);             // 입 움직임
  public void SetBlink(bool enabled);
  public void AddWound(BodyRegion r, BL23.Sim.DamageType t, int severity, Vector3 localPoint, bool postmortem); // 부위에 맞는 상처/피/의상 손상
  public void ClearWounds();
  public void SetBloodied(float amount);       // 옷에 튄 피(범인/구조자)
  public void SetWet(bool on);
  public void SetDisguise(string itemType);    // "TheaterMask","Cloak","Raincoat", null=해제
  public void SetVisible(bool on);
  public BodyRegion RegionFromCollider(Collider c); // 부위별 kinematic 콜라이더(레이어 "Hitbox")
}
public class ActorAnimator : MonoBehaviour {     // 코드 기반 절차 애니메이션(공용 스켈레톤)
  public void SetMove(Vector3 worldVelocity, bool running);
  public void SetPosture(Posture p);
  public void SetDeadPose(int variant);          // 시신 자세 0..5
  public void PlayGesture(Gesture g, float duration = 0f);
  public void PlayAction(ActionAnim a, float duration);
  public void SetLookAt(Vector3? worldPoint);
  public void SetInjury(float mobility, bool leftArm, bool rightArm, bool limpL, bool limpR, bool conscious);
  public void SetCarrying(ActorRig carried);     // 시신/사람 들쳐메기
  public void SetBreak(float t);                 // 진우·유스티·도윤 전용 BREAK 연출 강도 0..1
}
```
- 신장은 `CastDef.HeightCm` 그대로(1 unit = 1m). 유스티는 어항 머리(물과 금붕어가 움직이는 유리 상자).
- 제공 GLB 3종: `C:/Users/리오/OneDrive/Desktop/3D모델들/*.zip` → 김민혁(Midnight_Operative…), 김진우(Midnight_Sentinel_160cm…), 차도윤(Midnight_Formal…). 리그 없음 → 에디터 자동 리깅 도구로 공용 스켈레톤에 스키닝.
- 나머지 16명(유스티 포함)은 `LookSpec` 기반 절차 생성(SDF 스컬프트→메시, 얼굴 텍스처로 표정 교체). 유시온·백이현은 기획서 외형 유지, 나머지는 LookSpec의 리디자인을 따른다.

## 3. 저택 모듈 `BL23.Game.Mansion` (폴더 `Game/Mansion`)
```csharp
public class MansionView : MonoBehaviour {
  public static MansionView Build(BL23.Sim.Layout L, Transform parent);
  public Vector3 ToWorld(BL23.Sim.P3 p);
  public void SetDoor(int doorId, bool open, bool locked);      // 애니메이션, 콜라이더 갱신
  public void SetCircuit(int circuitId, bool on);               // 회로별 조명(비상등 제외)
  public void SetDarkness(float t);                             // CH23 챕터 암흑 0..1 (일반 조명 억제)
  public void SetNoise(float t);                                // CH22 소음 시각 연출(선택)
  public void SetFurnitureState(int furnitureId, int damage, BL23.Sim.P3 pos, float yaw); // 파손·이동
  public void SetPress(float ram01, bool powered);              // 기계실 프레스
  public Transform RoomAnchor(int roomId);                      // 방 중심(오디오 존)
  public Bounds RoomBounds(int roomId);
  public GameObject FurnitureObject(int furnitureId);
}
public static class PropFactory { public static GameObject CreateItem(BL23.Sim.ItemDef def, string itemId); } // 콜라이더+Rigidbody+PropMaterial
public static class TraceFactory { public static GameObject Create(string traceType, Vector3 pos, Vector3 normal, float size, Color tint); }
// traceType: "BloodPool","BloodDrip","BloodSmear","DragMark","FootprintBlood","FootprintWet","Water","Scratch","Dent","Crack","Fragment","Ash","Soil"
public class CourtroomView : MonoBehaviour { public Transform StandAnchor(int seat); public Transform ButlerAnchor; } // 18석 원형 + 유스티 좌석
```
- 모든 가구 타입(`FurnitureCatalog`)과 아이템 타입(`ItemCatalog`)에 시각 모델을 제공한다. 발자국(footprint W×D×H)을 반드시 지킨다(내비 계약).
- 벽: `Layout.Walls()`의 WallSeg(0.5m 격자, 문 개구부/개방 통로 포함). 두께 0.2m 권장. 외벽 창문, 2층 Void 가장자리는 난간.
- 계단: `Layout.Stairs` A→B 실제 위치/높이에 맞는 모델+경사 콜라이더. 대계단은 홀 안(가구 "GrandStair" 발자국).
- 모든 가구/소품은 물리 콜라이더를 갖고, 가벼운 것은 Rigidbody(수면). 재질별(`Mat`) 충돌 반응: 흠집/찍힘/금/파손(유리·도자기는 파편, 나무는 쪼개짐, 금속은 찌그러짐, 종이는 구겨짐, 천은 찢김).
- 조명: 방마다 Room.BaseLight·Palette 기반. 회로(Circuit)별 on/off. 비상등(회로 0 Emergency)은 어둡게 유지.

## 4. 오디오 모듈 `BL23.Game.Audio` (폴더 `Game/Audio`)
```csharp
public enum MusicState { Silence, Title, Prologue, DailyMorning, DailyDay, DailyEvening, Night, Tension, Mystery, BodyDiscovery,
                         Investigation, InvestigationLate, TrialOpening, TrialDebate, TrialPressure, TrialClimax, Vote, VerdictCorrect,
                         VerdictWrong, Execution, Escape, Reveal, Aftermath, LoopReset, Menu }
public class MusicDirector : MonoBehaviour {
  public static MusicDirector I;
  public void SetState(MusicState s, bool restart = false);   // 크로스페이드, 반복 회피
  public void Stinger(string id);                              // "discovery","break","objection","verdict","gavel","chime"
  public void SetVolume(float master, float music, float sfx);
  public MusicState Current { get; }
}
public static class Sfx { public static void Play(string id, Vector3? pos = null, float vol = 1f); } // 발소리, 문, 비명(합성), 타격, 유리 파손 등
public static class VoiceBabble { public static void Speak(string actorId, string text, float pitch, float rate); } // 단간론파식 짧은 음성 블립
```
- 음악 파일 원본: `C:/Users/리오/OneDrive/Desktop/아카이브/` (사용자 제작). 선택한 곡만 `Assets/BASSLINE/BL23/Resources/Music/`로 복사. 음악 없음(null)에서도 게임은 정상 동작.

## 5. 대사 콘텐츠 `BL23.Sim.Lines` (폴더 `Sim/Content`, 순수 C#)
- `LineBank.Add(actorId, key, politeVariants[], casualVariants[])` 형태의 정적 데이터. 자리표시자: `{you}`(호칭), `{t}`(제3자 이름), `{place}`, `{time}`, `{item}`, `{victim}`, `{act}`, `{topic}`, `{sound}`.
- 키 목록과 규칙은 `Documentation/BL23/LineKeys.md`.
