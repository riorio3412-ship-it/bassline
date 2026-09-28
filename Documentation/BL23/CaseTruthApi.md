# CaseTruthApi — the "case truth vs presented story" record for the 심판

Owner: murder-simulation track (proactive culprit mind, `Sim/Murder/Initiative*.cs`, `Sim/Murder/CaseApi.cs`).
Consumer: the trial track (`Sim/Trial/**`, `Game/Trial/**`), the reveal (Replay), the lab.
Status: **shape published 2026-09-27 23:00 (v1)**. Fields are append-only; names never change.

> 한 줄 요약: 범인의 머릿속(동기 → 기회 → 준비 → 실행 → 은폐 → 재판 대비)을 사건마다 한 묶음으로 꺼내 준다.
> `CaseApi.TrialPack(S, incidentId)` 하나만 부르면 **진실**(누가·왜·어떻게·무엇을 준비했나)과 **내세울 이야기**(표지 알리바이,
> 준비된 거짓말, 희생양, 규칙 방패, 무너질 때의 대체 이야기), 그리고 **일상 속 복선**(누가 무엇을 봤나)이 나온다.

---

## 1. Entry points (all static, read-only, deterministic, never mutate state)

```csharp
namespace BL23.Sim {
  public static class CaseApi {
    TrialPack TrialPack(GameState S, string incidentId);   // null if the incident is not a murder
    TrialPack ForTarget(GameState S);                      // the chapter's judged incident (rule 여섯: the first deliberate killing)
    List<TrialPack> Chapter(GameState S);                  // every murder of this loop+chapter, in death order
    Scheme SchemeOf(GameState S, string culprit);          // the live or finished scheme of that resident (null if none)
    string PlannedScapegoat(GameState S, string culprit);  // who the culprit planned to push (null if none)
    List<ForeshadowBeat> BeatsSeenBy(GameState S, string observer, int loop = -1); // prep moments a witness saw (testimony input)
    string Narrative(GameState S, string incidentId);      // multi-line Korean case narrative (lab / reveal / debugging)
  }
}
```

- Built **on demand** from saved state (`GameState.Mur.Schemes`, the `MurderPlan`, the `Incident`, the ledger). Nothing extra is stored, so
  save round-trips stay IDENTICAL.
- A murder that did **not** come from a scheme (a legacy impulsive plan) still gets a pack: `Improvised = true`, the story is derived from the
  plan (alibi room, concealment) and the shields are *detected* (e.g. y6 second killing, staged accident), never invented.
- All text is plain Korean with plain times (`ClockFmt.Vague`: "밤 9시 반쯤"), particles fixed through `LineBank.FixParticles`.
  Every text field has typed companions (actor ids, room ids, clocks) so the trial UI can re-render it.

---

## 2. The pack

```csharp
public sealed class TrialPack {
  public string Incident, Scheme, Plan;       // ids; Scheme == null → Improvised
  public bool Improvised;                     // no scheme: reconstructed from the plan
  public bool Judged;                         // this is S.Ch.TargetIncident (the 심판 judges this one)
  public int Order;                           // 1 = first deliberate killing of the chapter, 2 = second …
  public string Logline;                      // one sentence (the reveal's title card)
  public CaseTruth Truth;                     // what really happened
  public PresentedStory Story;                // what the culprit will claim / wants the room to believe
  public List<PackLie> Lies;                  // prepared lies, by topic, each with what breaks it
  public List<RuleShield> Shields;            // rule-shield arguments (유스티의 규칙을 방패로)
  public List<FallbackStory> Fallbacks;       // ordered retreat lines when the story cracks
  public List<ForeshadowBeat> Foreshadow;     // preparation moments in daily life, with who saw them
  public List<PackRole> Roles;                // scapegoat, unwitting helper, arranged witness, guests, protector, copied case
  public List<string> Log;                    // the culprit's timeline in Korean (planning → prep → kill → cover)
}

public sealed class CaseTruth {
  public string Culprit, Victim;
  public string Motive, MotiveText, Trigger;      // Motive ∈ §3.1; Trigger = the event that pushed them ("봉투를 읽고", "처형을 본 뒤")
  public string Moment, MomentText;               // §3.2 — the opportunity the culprit used or made
  public int MomentRoom = -1; public double MomentAt = -1;
  public string Approach;                         // §3.3
  public string Method;                           // the executed MurderPlan grammar (display label, e.g. "Errand+Recorder")
  public string Weapon, WeaponType;               // item id / ItemCatalog type (null for poison-less remote methods)
  public int KillRoom = -1, FoundRoom = -1; public double KillClock = -1, FoundClock = -1;
  public bool Hosted; public string EventId, EventLabel; public List<string> Guests; // the culprit's own event (Gathering id)
  public List<string> Variants;                   // §3.6
  public List<PrepRecord> Preparations;           // every prep task: kind, item, room, clock, done, seen by
  public List<string> Tricks;                     // layers that really ran (ledger-verified, same names as the plan grammar layers)
}

public sealed class PrepRecord { public string Kind, Item, Text; public int Room = -1; public double Clock = -1; public bool Done; public List<string> SeenBy; }

public sealed class PresentedStory {
  public string Speaker;                          // the culprit
  public int ClaimRoom = -1; public double ClaimFrom = -1, ClaimTo = -1;
  public string ClaimText;                        // "밤 9시쯤부터 라운지에서 카드 모임을 열고 있었어요."
  public List<string> ClaimWith;                  // people the culprit will name as company
  public string Account;                          // 1–3 sentence cover account
  public string Theory;                           // the theory the culprit pushes (사고였다 / S가 했다 / 첫 사건 범인이 또 했다 …)
  public string Scapegoat, ScapegoatCase;         // who and why (the planted story pointing at them)
  public List<string> Planted;                    // item ids planted to support the story (frame tokens, moved weapon…)
}

public sealed class PackLie {
  public string Id, Topic;                        // Topic: where | with | item | weapon | relation | saw | time | errand | motive
  public string Text, Truth;                      // what they'll say / what is true
  public List<string> BrokenBy;                   // seam refs: "beat:<id>", "item:<id>", "trace:<id>", "witness:<actorId>", "ledger:<seq>", "rule:<id>"
  public int Cost;                                // lie budget cost 1..3 (a culprit spends ≤ LieBudget before cracking)
  public bool Prepared;                           // decided before the kill (true) or improvised (false)
}

public sealed class RuleShield {
  public string Rule;                             // "y2" "y6" "y7" (y_rules 둘/여섯/일곱), "CH03" "CH23" "CH22" "CH02" "CH16" "CH08" "CH04" "CH10", "house-night"
  public string Tactic;                           // §3.5
  public string Argument;                         // the line the culprit will use in the 심판 (Korean)
  public string When;                             // when they play it ("두 번째 사건 범인으로 몰릴 때")
  public string Counter;                          // what defeats it (fair play: the rule's exact wording / a fact)
  public bool Prepared;                           // chosen at planning time (shaped the plan) vs detected afterwards
}

public sealed class FallbackStory { public int Order; public string Trigger, Story, Concedes, Keeps; }

public sealed class ForeshadowBeat {              // one innocent-looking preparation moment
  public string Id, Kind, Actor, Item;            // Kind = prep kind (§3.4)
  public int Room = -1; public double Clock = -1;
  public string Text;                             // what an observer saw ("도윤이 주방에서 얼음 송곳을 챙겼다")
  public string Innocent;                         // how it read at the time ("얼음을 깨려는 줄 알았다")
  public string Meaning;                          // what it really was (reveal only)
  public List<string> Observers; public bool PlayerSaw;
}

public sealed class PackRole { public string Role, Actor, Did; }  // Role: scapegoat | helper | witness | guest | protector | copied | victim-plan
```

### Lie budget
`Scheme.LieBudget` (2–6) comes from Deceit/Composure. The trial should let the culprit spend lies up to the budget, then retreat along
`Fallbacks` in `Order`. Each fallback `Concedes` a fact and `Keeps` the core claim, so the culprit bends before breaking (vision2 C).

---

## 3. Vocabularies (strings; append-only)

### 3.1 Motive
`wish` 계약 소원 · `escape` 살아서 나가려고 (처형·기한 뒤) · `grudge` 원한 · `fear` 위협 · `jealousy` 질투 · `secret` 비밀이 드러날까 봐 ·
`protect` 소중한 사람을 지키려고 · `love` 사랑 때문에 (연적·괴롭히는 사람) · `defense` 선수 치기 (상대가 먼저 나를 노린다) ·
`silence` 입막음 (내 범행을 본 사람) · `avenge` 복수 (사랑한 사람을 죽인 자라고 믿는 사람) · `copycat` 모방 (첫 사건과 규칙 여섯을 방패로)

### 3.2 Moment (the opportunity)
`hosted` 범인이 연 모임 · `joined` 초대받은 모임 · `house-dark` 정기 소등(CH03) · `long-dark` 긴 암흑(CH23) · `noise` 잔향(CH22) ·
`meal` 식사 시간 · `habit` 피해자의 습관 시간 · `night-lock` 야간 잠금 직전 · `investigation` 수사 중 · `pair-check` 공동 점검(CH08) ·
`exchange` 교환회(CH11) · `rendezvous` 단둘이 만나는 약속 · `night-visit` 밤의 방문

### 3.3 Approach
`errand` 심부름 보내기 · `slip-out` 자리 비우기 · `dark-strike` 어둠 속 일격 · `serve` 잔에 타기 · `rendezvous` 약속 장소 ·
`ambush` 길목 · `visit` 밤 방문 · `method:<Push|Strangle|Smother|Bedtime|Shock|Drown|Trap>` 기존 수법

### 3.4 Preparation kinds (each is a visible daily-life action with a motion)
`obtain` 흉기·도구 챙기기 · `stash` 숨겨 두기 · `scout` 현장 둘러보기 · `shadow` 피해자 따라다니며 습관 익히기 · `rehearse` 동선 맞춰 보기 ·
`clock` 시계 바늘 조정 · `garb` 비옷·앞치마 같은 겉옷 챙기기 · `mark` 어둠 속 표식 (피해자에게 향 나는 꽃·방울 선물) ·
`token` 희생양의 물건 슬쩍하기 · `rumor` 희생양에 대한 말 흘리기 · `witness` 알리바이 증인 약속 잡기 · `helper` 모르는 조력자에게 부탁 ·
`host` 모임 기획과 초대 돌리기 · `dress` 모임 장소 꾸미기 (초·의자·카드)

### 3.5 Rule-shield tactics
| Rule | Tactic | 요지 |
|---|---|---|
| y6 | `second-killer` | 두 번째 살인은 이번 심판에서 가리지 않는다 — "첫 사건과 나는 무관하다" |
| y6 | `order-swap` | 내 사건이 나중에 일어난 것처럼 보이게 해 다른 사건을 '첫 번째'로 만든다 |
| y6 | `not-deliberate` | "일부러" 앗은 목숨만 가린다 — 사고/자연사로 보이게, 무너지면 "사고였다"로 물러선다 |
| y7 | `dead-scapegoat` | 죽은 사람도 지목할 수 있다 — 이미 죽은 사람에게 덮어씌운다 |
| y2 | `first-in` | 세 사람 발견 규칙: 스스로 발견자 무리에 끼어 현장에 남긴 흔적을 '발견할 때 묻은 것'으로 만든다 |
| y2 | `delay` | 사람 발길이 드문 곳 — 세 명이 볼 때까지 안내가 울리지 않아 사망 추정 폭이 넓어진다 |
| CH03 / CH23 | `house-dark` | 불은 저택이 껐다 — 스위치를 만진 사람은 없다, 어둠 속이라 아무도 못 봤다 |
| CH22 | `noise` | 소음 때문에 비명이 묻혔다 |
| CH02 | `wing` | 나는 다른 날개 사람이라 갈 수 없었다 (수사 때만 풀린다는 예외를 감춤) |
| CH16 | `closed-room` | 정비로 닫힌 방이었다 |
| CH08 | `pair` | 점검 내내 짝과 같이 있었다 |
| CH04 | `sealed-statement` | 첫 진술은 봉인됐고 한 글자도 바꾼 적 없다 |
| CH10 | `inquiry` | 공개 질의에서 모두 앞에서 말한 그대로다 |
| house-night | `night-lock` | 밤 10시 뒤엔 저택이 그 방을 잠갔다 — 그 뒤엔 아무도 못 들어갔다 |

### 3.6 Variants
`hosted` · `group-moment` · `during-investigation` · `copycat` · `second-killer` · `silencer` · `avenger` · `turnabout` (피해자 자신의 계획이 되돌아옴) ·
`helper` (모르는 조력자) · `framed` · `rule-shield` · `arranged-witness` · `clock-alibi` · `marked-in-dark`

---

## 4. Guarantees the trial can rely on
1. **The truth is a person's act** (H10). `Truth.Culprit` is never null in a pack.
2. **Fair play.** Every `PackLie.BrokenBy` names at least one seam that exists in the saved world (a beat with an observer, an item, a trace,
   a ledger event, a rule text). Prep beats are recorded only when someone could really see them (their open sighting of the culprit).
3. **Nothing is created after the fact.** Beats and plants happen in the simulation at their own time; the pack only reads them.
4. **Judged vs not judged.** `Judged` mirrors `S.Ch.TargetIncident` (y_rules 여섯). A `second-killer` shield is only offered in packs
   with `Order ≥ 2`, and its `Counter` quotes the rule exactly ("가장 먼저 일부러 목숨을 앗은 한 분").
5. **No Danganronpa set-piece.** The planner never combines party + blackout + glow-marked weapon + body under a table + tablecloth
   (vision2 A). Dark strikes use marks on the victim (scent, bell, pale ribbon), never on the weapon; bodies are never staged under tables.

## 5. Suggested use in the 심판 (non-binding)
- Opening statements: `Story.ClaimText` for the culprit; other residents' honest `Where` answers stay as they are.
- Culprit counters: pick `Lies` by the topic under discussion; when a `BrokenBy` seam is presented, move to the next `Fallbacks[i]`.
- Theory debate: `Story.Theory` is the culprit's pushed hypothesis; `Story.Scapegoat` is their target; `Shields` are the "규칙을 방패로"
  moves — play `When` conditions literally.
- Foreshadow recall: `Foreshadow` with `PlayerSaw` → the player's own memory; others → testimony by those `Observers`.
- Reveal: `Logline` + `Log` + `Truth.Preparations` give the full "how it was prepared" montage.

## 6. Minimal example (seed 20260926, abbreviated)
```
Logline: 한서윤은 자기가 연 카드 모임 도중 백이현을 와인 저장고로 심부름 보내고, 그 틈에 뒤따라가 쇠지렛대로 쳤다.
Truth: motive=grudge moment=hosted approach=errand weapon=Crowbar kill=와인 저장고 variants=[hosted,framed,arranged-witness]
Prep: obtain(쇠지렛대, 창고, 오후 2시쯤, seen by 민서) · stash(와인 저장고 궤짝) · host(카드 모임, 라운지, 밤 8시) · token(채령의 스카프)
Story: "모임 내내 라운지에 있었어요. 이현 씨가 와인을 가지러 가서 안 돌아오길래 한 번 찾으러 갔을 뿐이에요."
Lies: where(라운지에 계속) ← broken by witness:P12 (8시 40분쯤 7분간 자리 비움) · item(스카프는 채령 것) ← beat:b12 (서윤이 채령 방 앞에서 뭔가를 주움)
Shields: y2 first-in ("제 소매의 피는 처음 발견했을 때 묻은 거예요")
Fallbacks: 1) "찾으러 갔다가 이미 쓰러져 있는 걸 봤어요. 겁이 나서 말 못 했어요." (concedes: 현장에 갔다 / keeps: 죽이지 않았다)
```
