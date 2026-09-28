# BASSLINE BL23 — Daily Life Design (일상 설계)

Written 2026-09-27 as a narrative and systems design pass. This is analysis and design only: no code or content file was changed.

**Companion:** `CharacterBible.md`, referred to below as "the bible". It holds the voices, the relationship web (§3), tells and witness lenses.

**This document covers** what makes the days *between* murders fun, how that fun feeds the mystery, and how to build it on the current kernel (`Sim/**`, deterministic, 10 Hz) and the time-on-demand model now being implemented.

---

## 0. Pillars

1. **Time is the currency.**
   - Under time-on-demand, the clock moves only when 민혁 passes time or spends it: with someone, at an activity, asleep, or at an event.
   - Every block of the day is therefore a choice of *whom to be with*. You cannot be with everyone, and the murder simulation keeps running in the hours you spend elsewhere.
2. **Pairs, not monologues.**
   - Fun comes from residents bouncing off each other: a feud at breakfast, a truce in the library, a jealous glance across the table.
   - Every major system here works on ties (bible §3), not on single characters.
3. **Everything fun also feeds the mystery, while clues stay simple.**
   - Daily life hands out the same things a killer needs: habits, favourites, routes, who borrowed what, who was at the table. Those are exactly the things the player later reasons with.
   - Only case-relevant items become evidence cards (`Evidences.Worthwhile`, user directive: few clues). Everything else stays atmosphere and memory.
4. **The mansion pushes.** Yusti's announcements, the chapter rules, the house's hunger and rumours raise tension on quiet days, so murder never feels random.
5. **Death leaves a visible hole.** The spoons, the seat count, the lily on the chair, the half-finished roster. Grief changes routines and alliances.

**Not Danganronpa, by construction.**
- No tickets, coins or gacha, and no Free-Time-Event menu of portraits.
- Time together happens *in the world*, at a place, doing a real activity. The murder simulation can interrupt it, and your presence can prevent a death.
- The payoff is knowledge (tells, habits, routes), trust in investigation and support in the 심판, not unlockable skills.
- Group events are gothic-mansion rituals, not school events.

---

## 1. What daily life is today (evidence)

Details and numbers are in bible §1.2. In short:

- **Repetition.** `small_talk` is 27% of all speech. Six lines per resident, each heard 10–15 times over three days.
- **Template leakage.**
  - Intent barks, requests, routines (tea, comfort, applause, confrontation, strolls) and daily grammars (borrowing, gatherings, couriers, repairs) are single name-swapped sentences for all 17 residents.
  - Request texts even produce "내 진우의 두꺼운 책 못 봤어?".
- **No interplay.**
  - Table talk is 3–5 disconnected one-liners (`CinematicUI.TableTalkCo`).
  - NPC–NPC replies are drawn independently of the opener (`Social.ConvoLine`).
- **Same verbs for everyone.**
  - The conversation menu (`PlayerApi.Options`) is identical for every NPC.
  - Gifts are keyword matches.
  - Time spent together ends in one `small_talk` line (`PlayerActivities.PlayerUse`).
- **Strong foundations already exist.** These are underused, and every design below builds on them rather than replacing them:
  - Haunts per time block (`LifeAI.HauntRoom`), two goals each (`Goals.cs`).
  - Routines (`Routines.cs`), gatherings with revisions, loans, repairs and couriers (`Grammars.cs`).
  - Requests with appointment cards (`Requests.cs`), 68 bond scenes (`Bonds.cs`).
  - Meal bells, hunger (`Hunger.cs`), 23 chapter rules (`Rules.cs`).

---

## 2. The shape of a day (time on demand)

### 2.1 What costs time

| Action | Time | Notes |
|---|---|---|
| Walking, looking, opening drawers, sitting, short dialogue | 0 (instant) | The world is animated but frozen (`IdleLife` presentation). |
| Bond scene (`Bonds.cs`) | 10 min | At most one per person per day (existing). |
| Furniture activity (`PlayerActivities`) | 5–45 min | Existing table, e.g. read 30, cook 35, chess 35. |
| **함께 시간을 보낸다 (hangout, §4)** | 30–60 min | The main social time sink. |
| Table scene (breakfast/dinner) | 20 min | Once per meal (existing `tabletalk:` flag). |
| Festival or group event (§3.4) | 45–90 min | At most one per calm day. |
| Pass time (T) | Chosen | Stops at an interrupt (§2.4). |
| Sleep | Until 07:00 | — |

**Budget.** The 07:00–22:00 waking window allows roughly **6–8 meaningful time spends** plus two table scenes and at most one event. So the player can deepen **about three relationships a day**. That scarcity is the point (Persona-style craft).

**Day 2 double-booking.** The design deliberately produces *one conflict* on day 2: two invitations at the same hour. 민혁's flaw (bible P01) then becomes a real choice.

### 2.2 The fixed beats

| Time | Beat | Who | Existing hook |
|---|---|---|---|
| 07:00 | Morning announcement (doors open, hunger news, rule reminders) | 유스티 | `Announce("y_morning")`, `Hunger.Morning` |
| 08:00 | Breakfast bell, then the **Morning Table** scene (§3.2) | 유스티, diners | `y_meal_breakfast`, `CheckTableTalk` |
| 09:00 | Clock round (Yusti corrects one drifting clock) | 유스티 | `Grammars.ButlerClockRound` |
| 09:12 | 은결's fixed walk (bible P14) | 은결 | New haunt rule |
| 12:30 | Lunch: free, whoever is hungry, 준서 cooks | 준서 | `MealStart[1]` |
| ~15:00 | Event of the day, if calm (§3.4) | Host resident | `Grammars.Host` + new `Festivals` |
| 18:30 | Dinner bell, then the **Evening Table** scene (conflict, rule and grief topics) | 유스티, diners | `y_meal_dinner` |
| 22:00 | Night announcement; night locks | 유스티 / House | `y_night`, `House` |
| 23:00– | Night: 도윤 straightens frames; 진우 checks a clock at 23:40; 민서 reads in the library | — | New haunt rules |

### 2.3 The day board (오늘의 일정)

This lives in a notebook tab (clues-and-qol owns the notebook; hook it later). It lists:
- Accepted invitations and appointments (existing `Request` cards).
- The event of the day, its host and place.
- Residents' known haunts. Relationship stage 1 unlocks these (§6).
- The quiet-day counter: "조용한 날 2일째 — 저택이 배고파한다".

### 2.4 Interrupt banners while passing time

The time-on-demand workflow owns the banner. This section only proposes its priorities:

1. Scream or death bell. Always stops.
2. A request: someone walks up to you. Stops.
3. An accepted appointment is 8 minutes away (existing notice).
4. A **pair scene** is brewing within earshot, e.g. "라운지에서 목소리가 높아진다 — 가 볼까?". The player chooses.
5. The event of the day is starting.
6. A bond ◆ is available with someone in the same room.

Items 4–6 are offers, not stops.

---

## 3. Daily events

### 3.1 Yusti's announcements

**Role.** Yusti makes every announcement (user directive 14) and nothing else outside the 심판 procedure. The House voice is only the fallback (`Lines_House`).

**Craft rules** (bible NPC00):
- 하십시오체, "~ 님".
- **At most one sensory beat** (water, glass, fish) per announcement.
- No hints and no opinions.
- **Numbers carry the dread.** He states seats, remaining nights and doors.

| Moment | Example |
|---|---|
| Normal morning | 알려 드립니다. 아침입니다. 밤사이 잠가 두었던 문을 모두 열었습니다. |
| Quiet day 2 | 좋은 아침입니다. 조용한 하루였습니다. …어항의 물이 조금 식었습니다. |
| Hunger | 알려 드립니다. 저택이 {place:을} 삼켰습니다. 오늘부터 식사가 조금 줄어듭니다. |
| Breakfast with a seat count | 아침을 차렸습니다. 자리는 열일곱입니다. 따뜻할 때 드십시오. |
| Dinner after a death | 저녁을 차렸습니다. 자리는 열여섯입니다. …정해진 대로, 전부 놓았습니다. |
| Event | 알려 드립니다. 오늘 오후 세 시, 라운지에서 송예담 님이 주최하는 모임이 있습니다. 참석은 자유입니다. |
| CH06 envelopes | 오늘 아침, 몇 분의 방 앞에 봉투를 놓아 두었습니다. 누군가의 지난날이 적혀 있습니다. 열어 보실지는 각자 정하십시오. |
| Night | 밤입니다. 곧 몇 곳의 문을 잠급니다. 무사히 주무시기를 바랍니다. |

**Kernel note.** The seat count needs `{n}` in `y_meal_*`. `LifeAI.Schedule` currently passes `null` slots, so pass `{"n": living participants}` instead. This is a one-line change in an unowned file.

### 3.2 The Morning and Evening Table

This replaces the random one-liners of `TableTalkCo` with a **topic engine**. Each meal has *one* topic, drawn from world state. The topic's *owner* opens, 2–4 diners react to the opener by name (pair variants), and the player may interject once.

**Topic priority** (first that applies):

| # | Topic key | Condition | Owner (opens) | Typical reactors |
|---|---|---|---|---|
| 1 | `tt_death` | A confirmed death in the last 24 h | Closest friend of the victim (highest Attach) | 은결 (memorial), 준서 (spoons), 진우 (silence) |
| 2 | `tt_rule` | A rule announced in the last 12 h | Whoever it hits hardest (CH05 targets, CH06 recipients) | 가온 (asks for the source), 이현 (calms), 진우 (watches) |
| 3 | `tt_hunger` | Hunger ≥ 1 | 서윤 (rations) | 태겸 (calculates), 시온 (complains), 준서 (gives his share) |
| 4 | `tt_conflict` | An argument or pair scene yesterday, and both parties at the table | The aggrieved party | Mediators: 수아 (both sides), 서윤 (procedure), 진우 (stokes) |
| 5 | `tt_event` | An event today | The host | Fans and refusers (예담 → 라온/채령) |
| 6 | `tt_absent` | Someone living skipped the meal | 서윤 (head count) | Whoever last saw them (witness lens) |
| 7 | `tt_rumour` | A rumour known to 2+ diners | Its last carrier | The subject, if present, flares up |
| 8 | `tt_habit` | Otherwise: a resident's habit comes up | Rotating | *Records who heard it* (§7.4) |
| 9 | `tt_food` | Fallback | 준서 | Everyone, one line each |

**Line casting.**
- Opener: `tt_<kind>`, preferring `tt_<kind>@<subject>`.
- Reactors: `tt_<kind>_re@<opener>`, else `tt_<kind>_re`.

**Player interjection.** One choice of three, taken from the topic table. Options are "side with A", "defuse" and "stay silent", and each is worth ±0.03–0.06 on the relevant relationships.

**Example — Morning Table, hunger** (after 유스티: "…저택이 온실을 삼켰습니다. 오늘부터 식사가 조금 줄어듭니다.")

> 서윤: 열다섯, 열여섯… 다 계시네요. 정리하면, 오늘부터 빵은 한 사람에 하나예요.
> 태겸: 계산해 보면 하나 반입니다. 반 개는 제 몫에서 빼겠습니다.
> 시온: 사장님, 반 개로 뭘 해? 셰프! 빵 더 없냐?
> 준서: 제 거 드세요. 전 간만 봐도 배불러요. 허허.
> 진우: 흐응. 방금 준서 말에 세 명이 눈을 피했어. 누가 제일 배고픈지 다 보이네.
> ▸ 서윤 씨 말대로 하나씩 나눠요. *(서윤 신뢰+, 시온 호감−)*
> ▸ 준서 씨도 드셔야죠. *(준서 호감+; he takes half — his growth beat)*
> ▸ 진우 씨, 그건 굳이 말 안 해도 되잖아요. *(진우 흥미+, the three who looked away 호감+)*

**Example — Evening Table, CH05 두 계약 공개**

> 유스티: 식사 전에 알려 드립니다. 규칙에 따라 두 분의 계약을 공개합니다. 권태겸 님은 ‘막대한 돈’. 정세나 님은 ‘잃은 손을 되찾는 것’입니다.
> 세나: …야. 밥 먹는 데서 그걸 읽어? *(the prosthetic goes into her pocket)*
> 태겸: 돈이라. 틀린 말은 아닙니다. 다들 계산이 빨라지시겠군요.
> 이현: 자, 자. 계약은 계약일 뿐입니다. 요컨대, 무엇을 빌었든 행동은 별개라는 거죠.
> 가온: 잠깐만요. ‘막대한’은 얼마예요? …적어 둘게요.
> 해린: …손, 이라고 했지.
> ▸ 그건 세나 씨가 말할 일이에요. *(세나 신뢰++)*
> ▸ 태겸 씨, 얼마가 필요한데요? *(태겸 경계, 가온 관심)*
> ▸ 말없이 수저를 든다. *(nothing; 진우 notes it)*

### 3.3 Pushes that raise tension

| Push | Existing | What to add |
|---|---|---|
| Chapter rules (motives) | `Rules.cs` CH01–23, announced by Yusti | Dramatise the **three big ones** as table topics and scenes:<br>• **CH05:** contracts read at dinner (above).<br>• **CH06:** envelopes at the doors; each envelope carries *one fact about someone else*, with no name. For example, to 세나: "작년 겨울 그 승합차를 몰던 사람이 이 집에 있다" (knot K1). Reactions per resident; who opens, who burns, who shows it around.<br>• **CH09:** the deadline notice to one contractor, which changes that person's behaviour visibly (insomnia, pacing the corridor at night, snapping at meals). |
| The house's hunger | `Hunger.cs` (36 h quiet → rooms sealed, food reduced) | Yusti's quiet-day lines escalate (§3.1). Portion fights become a table topic. The swallowed room was someone's haunt, so they are displaced and irritable. |
| Rumours | — | §3.5 |
| Flashpoints | `Routines` confront only | §3.6 |
| The contractless outsider | — | 세나 and later others openly suspect 민혁 ("계약 없는 놈이 제일 수상하지"). Proving himself is part of the player arc. |

**CH06 envelope reactions** (one line each; content is the rule-event key `env_open`):

> 가온: 출처 없는 기록은 기사가 아니에요. …그래도 읽을 거예요.
> 시온: 나한테도? …야, 이거 누가 쓴 거야. 누가 썼냐고.
> 은결: 태우는 편이 낫겠습니다. …제 것은요.
> 진우: 흐응, 드디어 재밌어지네. 다들 봉투 쥔 손 좀 봐.
> 채령: 안 열어. 남이 써 준 내 얘기는 안 읽어.
> 민서: …읽고 접었습니다. 다른 분 얘기라서요.

### 3.4 Group events and small festivals

Rules:
- At most **one per calm day**. The host proposes it and Yusti announces it.
- It runs through the existing gathering system (`Grammars.Host`, revisions, attendance tracking).
- Every event is **fun, reveals someone, and opens a murder window**. Gatherings are already a murder grammar ("모임 안의 사건": slip out and rejoin), so an event is both a party and a risk.

| Event | Host | Place | Mini-game / beat | What it reveals | Murder window |
|---|---|---|---|---|---|
| 저택 퀴즈쇼 | 예담 | Lounge | Quiz about the residents; wrong answers are funny; the player learns trivia (favourites, habits) | Habits are spoken aloud to a known audience (§7.4) | The host is alone before the show; a time change (IG02) confuses alibis |
| 게임 대회 | 세나 | 게임실 | Bracket; the player plays billiards or darts (§4.3) | 세나's left-hand drills; who is a sore loser | Crowd noise masks sound |
| 작은 공연의 밤 | 재하 (+수아, 시온, 라온) | Theater / MusicRoom | Rehearsal, then the show; the player can accompany on piano | Seat 3-7; 시온's verse; 라온 recognises a melody (K2) | Lights down; the audience faces the stage; the play script can plant a trick (§7.6) |
| 교환 장터 (CH11 when active) | 태겸 | Lounge | Trade items at 태겸's "rates"; rare gifts | Who wants what, and who is short of money | Items change hands, so weapon access changes |
| 요리 품평회 | 준서 | Dining | Everyone judges; 준서 asks *you* to taste first | His lost taste (bond II seed) | Food and drink pass through many hands (poison) |
| 지도 공개 | 가온 + 민서 | GrandHall | The map is unveiled and becomes usable in the notebook (a practical payoff) | Routes, doors, loggers | Everyone now knows the routes, the killer included |
| 의상 전시 | 채령 | Wardrobe / Gallery | Style three residents, including the player | Measurements, fibres, buttons | Costumes and cloaks lie around (disguise) |
| 청년 연설회 | 이현 | Theater | His speech; 가온 heckles; the player may ask one question | His A/B sentences; the room's allegiances | — |
| 공방 개방일 | 해린 | 공방 | Fix-your-thing day; everyone brings something broken | Who owns which tool; 도윤's too-clean tools | Tools lent out |
| 촛불 낭독회 | 은결 (+시온) | Chapel | Read-aloud (§4.3); 시온 reads a word from his notebook | Voices; who knows which book | Candle-lit room with one exit |
| 바의 밤 | 시온 | Bar (Lounge/Parlor) | Drinks; confessions after the second glass | Loose lips: secret hints | Drunk people, a late walk home |
| 추모의 밤 | 은결 (+준서) | Chapel | After a death, §8.3 | Grief lines; who stays away | — |

### 3.5 Rumours (소문)

This follows doc §06 4: a rumour has an origin, a chain and a belief distribution, never one global truth value.

**Sources.**
1. **Odd sightings.** Existing `Sightings`, extended with *oddness*:
   - wrong room at the wrong hour;
   - carrying a tool;
   - leaving someone else's bedroom corridor;
   - lit fireplace in an unused room;
   - boiler at full.
2. **CH06 envelopes.**
3. **Pair scenes** witnessed or overheard.
4. **The player's own acts**: searching rooms (already tracked), breaking promises.

**Spread.**
- A new conversation topic, `rumour`, sits beside `gossip` in `Social.ChooseTopic`, and rumours also come up as a table topic.
- **Distortion:** Honesty below 0.5 or Deceit above 70 may escalate the claim one step. For example "만졌다 → 가져갔다 → 훔쳤다" (doc P0448).

**Effects.**
- Suspicion rises and trust falls toward the subject.
- A rumour that touches a secret adds `knows-my-secret:<carrier>` to the subject, which is a real `Relations.Pressure` input and so a real murder motive.
- The subject confronts the carrier (the existing confront routine, now with a reason).

**The player** can:
- hear a rumour, which is noted in the notebook as a rumour, not as evidence;
- trace it ("누가 처음 말했어요?"), which reveals the chain;
- correct it if they know the truth, gaining trust with the subject.

A rumour becomes an evidence card only through `RecallRelevant` when it is case-relevant (clues-and-qol).

| Rumour | Truth |
|---|---|
| 어젯밤에 도윤 씨가 복도 액자를 하나씩 만지고 다니더래요. | True, and innocent: his 1° habit |
| 시온이 옛날에 사람을 경찰에 넘겼다던데. | A CH06 leak, softened from "informed" |
| 민혁 씨가 남의 방 서랍을 열어 봤대요. | The player's own action |
| 태겸 씨가 창고 물건을 빼돌린대. | Distorted from "태겸 씨가 창고 열쇠를 갖고 있었다" |

### 3.6 Flashpoints (pair scenes)

Short authored exchanges of **3–6 lines, one pair each**, triggered by co-location and a condition. With the player present, they end in a mediation choice. Without the player, they resolve NPC-only and leave a rumour ("어제 라운지에서 둘이 싸웠대").

They reuse `BondLine`/`BondChoice` from `BondScenes.cs`, so no new types are needed. Cooldown is two days per scene. At most two scenes per day play *in front of* the player.

| Pair (bible tie) | Trigger | Opener (Korean) | Choices → outcome |
|---|---|---|---|
| 라온 × 시온 (#27) | Both in Library, 시온 arrives | 시온: 야, 베이스! 여기 있었냐! / 라온: 도서실이야. 네 볼륨, 여기서만 반으로 줄여 줄래? | Listen together (both +) / side with 시온 (라온 −) / side with 라온 (시온 loses face) |
| 준서 × 채령 (#6, doc D01) | 채령 leaving alone | 준서: 전시실 가요? 같이 가요. 혼자 보내기 좀 그래서요. / 채령: 싫다고 했잖아. 호의면 그쯤 알아들어. | Respect the refusal (채령 trust +; 준서 waits in the greenhouse) / silence (he may follow; boundary breach logged) / go with 준서 (채령 grudge +) |
| 세나 × 민서 (#17), day 1 | Storage key missing | 세나: 야, 창고 열쇠 마지막으로 가진 거 너지? 어디 뒀어. / 민서: 제자리에 뒀습니다. 못 봤으면, 없었다는 뜻은 아닙니다. | "태겸 씨 대여표부터 봐요": the slip shows 시온 had it, which unlocks her apology scene. "세나 씨, 증거 없잖아요": 세나 annoyed at 민혁, same apology later. Stay out: 민서 grudge + |
| 진우 × 서윤 (#14) | Roster posted | 진우: 이 표, 칸이 너무 반듯해. 착한 척도 줄 맞춰서 하네. / 서윤: 진우 씨, 그 표로 누가 밥을 굶는지가 정해져요. / 진우: 흐응, 화났어? 목소리가 작아졌는데. / 서윤: 작아진 게 아니라, 정리된 거예요. | Side with 서윤 / side with 진우 / "둘 다 제 칸에 적어 주세요" (defuse: both +, and 서윤 hands 민혁 a chore) |
| 해린 × 태겸 (#25, doc D12) | Same tool | 해린: 지금 쓰잖아. 손부터 빼. / 태겸: 예약 시간은 지났습니다. 끝나는 시각을 말씀해 주시죠. / 해린: …십오 분. / 태겸: 십오 분. 적어 두겠습니다. 늦으면 초콜릿 한 조각. | Usually self-resolving; it plants the K4 pair's texture |
| 태겸 × 민서 (#10) | Chore count | 태겸: 민서 씨, 사흘 연속 설거지입니다. 계산이 안 맞습니다. / 민서: 제가 하는 게 빠릅니다. / 태겸: 빠른 거랑 공평한 건 다릅니다. …전에도 그렇게 말씀하셨죠. / 민서: …처음 뵙습니다. *(checks his watch)* | Notice the lie → debt seed (민서 trust in 민혁 +) / let it go |
| 가온 × 이현 (#5) | Dinner | 가온: 백이현 씨, 어제 ‘모두를 위한 확인’이라고 하셨죠. 누가 확인하는지는 안 정하셨어요. / 이현: 자, 자. 식사 자리에서까지 기사 쓰실 건 아니죠? / 가온: 기사는 제목이 없을 때 제일 정확해요. 지금은 제목 없어요. | Side with either (public alignment matters for later votes) |
| 채령 × 수아 (#11) | Same room | 채령: 그 그룹 활동 중지만 아니었어도. …뭐, 어쨌든. / 수아: 채령 씨… 미안해요. 제가 뭘 할 수 있었을지는 모르지만. / 채령: 사과 받으려던 거 아니야. 날짜만 적어 둔 거지. | Ask 채령 about the shop (her truth hint) / comfort 수아 |
| 시온 × 수아 (#12) | Meal | 시온: 스타! 네 자리 맡아 놨어, 창가. / 수아: 와, 고마워요! 근데 서윤 씨랑 먼저 약속해서… 다음엔 꼭요! | A pure scene. 시온's face falls; it seeds jealousy toward whoever sits with her |
| 도윤 × 세나 (#16) | 공방 / GameRoom | 도윤: 그 손, 선이 곱군요. 이음새가 거의 보이지 않습니다. / 세나: …불쌍하다는 말보단 낫네. 근데 왜 손만 봐? | Say nothing (a red flag the player noticed) / call 도윤 out (도윤 cools toward 민혁) |
| 재하 × 예담 (#28) | 예담 filming | 재하: 예담아, 그거 찍고 있지? 끄자. 지금 이 얼굴은 무대 밖이야. / 예담: ……응. 지울게. 지금. | Watch her delete it (both +) / defend the filming (재하 −) |
| 예담 × 채령 (doc 05 2) | Invitation | 예담: 짜잔! 퀴즈쇼 초대장. 빠지면 벌칙! / 채령: 벌칙 있는 초대는 초대가 아니야. | Suggest an optional invitation. 예담 remakes it ("안 와도 돼") and 채령 comes for ten minutes. Both grow |
| 준서 × 진우 (#18) | Breakfast | 진우: 죽 말고 사탕 없어? / 준서: 죽 먹고 사탕 먹어요. 순서가 있어요. / 진우: 흐응, 명령은 싫은데. / 준서: 명령 아니에요. 부탁이에요. 허허. | Comic; no choice |
| 서윤 × 이현 (#2) | Morning greeting | 이현: 서윤 씨, 좋은 아침입니다! 오늘도 표가 반듯하네요. / 서윤: …네. 좋은 아침이에요. *(her pen stops)* | No choice; the player *sees* the pen stop (context line + gesture) |
| 라온 × 재하 (#8) | 재하 imitating someone | 라온: 그 사람 말투, 네가 하면 반 박자 빨라. / 재하: 라온아, 그래도 단어는 하나도 안 틀렸어. / 라온: 단어가 다가 아니라니까. | Neutral; plants K2 |
| 은결 × 예담 (#31) | After a death, 예담 starts a quiz | 예담: 추리 퀴즈! 흉기는 뭐였을까요? / 은결: 예담 씨. 죽음은 퀴즈가 아닙니다. 정답이 없으니까요. | 예담's silence; later she folds a lily with 은결 (her growth) |

### 3.7 Residents seeking the player out

The existing `Requests` kinds (invite, find, deliver) need **per-character texts through LineBank keys** (`req_invite`, `req_find`, `req_deliver`, `req_accept`, `req_thanks`).

New kinds:
- `advice` — "who's right?": a mediation between two tie parties.
- `mediate` — come with me while I talk to X.
- `alibi` — be at place P at time T. The culprit version is foreshadowing (§7); innocent residents ask the same thing.
- `warn` — beware of X.
- `confide` — the bond ◆, already present.

| Resident | Flavour of their ask (Korean) |
|---|---|
| 진우 | 심심해. 체스 한 판. 지면 네가 질문 하나에 대답하는 거야. |
| 서윤 | 민혁 씨, 내일 아침 식기 당번… 한 칸만 맡아 줄래요? 딱 한 칸만요. |
| 도윤 | 신발 뒤축이 벌어졌군요. 괜찮으시다면 오늘 저녁에 붙여 드리겠습니다. |
| 이현 | 잠깐 수영장에서 얘기 좀 할까요? 둘만요. 어려운 얘기는 아닙니다. |
| 태겸 | 손전등, 빌려드릴 수 있습니다. 반납은 내일 아침 아홉 시까지입니다. |
| 시온 | 야, 새 가사 나왔어. 들어 줄 사람이 너밖에 없어. 딱 여덟 마디만. |
| 라온 | 복도 끝에서 박수 한 번만 쳐 줄래? 울림 좀 재 보게. |
| 재하 | 민혁아, 대사 좀 맞춰 줄래? 상대역이 없어서. 오 분이면 돼. …진짜로. |
| 준서 | 오늘 점심 간 좀 봐 줄래요? 딱 한 숟가락이면 돼요. |
| 해린 | 손전등 좀 들어 줘. 배선 안쪽이 안 보여. 삼 분이면 돼. |
| 수아 | 관객 한 명만 돼 줄래요? 빈 객석은 좀 무서워서요. |
| 세나 | 한 판 해. 저번 거 리매치. 도망가면 부전패야. |
| 은결 | 아홉 시 십이 분에 산책합니다. 괜찮으시다면 도윤이 쪽 복도로 같이 가 주시겠어요? |
| 가온 | 어제 오후 네 시쯤 복도에 계셨죠? 직접 보신 것만 여쭤볼게요. 오 분이요. |
| 채령 | 그 재킷 벗어 봐. 단추 하나가 헐거워. …달아 준다는 거 아니야. 보기 싫어서 그래. |
| 예담 | 짜잔! 초대장이야. 오후 세 시, 라운지, 퀴즈쇼. 안 와도 돼. …와도 되고. |
| 민서 | …한쪽 들어 주실 수 있습니까. 상자가 문보다 넓습니다. |

---

## 4. Spend time together (함께 시간을 보낸다)

This is the content layer for the dialogue option the time-on-demand workflow is adding.

### 4.1 Rules

**Where and what.**
- Available when a resident is free (Active, not talking, not in a plan) and awake.
- It uses **the resident's favourite activities** (§4.4) at a real place. Both of you walk there (instant, animated) and the activity runs with its existing animation (`Activities` Anim).
- **Every action is animated** (memory): tea uses Drink, cooking Cook, reading Read, cards and billiards Play, darts Use, the darkroom Craft.

**Cost and limits.**
- Costs 30–60 minutes of world time. The murder simulation runs meanwhile.
- Your presence protects your partner: the planner's `Stalk` needs the victim alone.
- A hangout can be **interrupted** by the world (§2.4), and the partner reacts in character.
- One hangout per person per time block. Diminishing returns already exist (`Relations.Change`, dim).

**Refusals.** A refusal is respected and has a reason, never "호감 부족" (doc P0403). For example 라온: "지금은 좀. 핫팩 떨어졌어. 한 시간 뒤에."

### 4.2 Structure of a hangout

1. **Open.** 1–2 lines, specific to partner, place and time of day.
2. **Moment 1.** A choice of three, or a mini-interaction (§4.3).
3. **Moment 2.** Something happens: a tell, a habit shown, a third resident walks in, a memory surfaces. There may be a second choice.
4. **Close.** 1–2 lines, then the outcome:
   - relationship change;
   - item: a crafted gift, dish or photo;
   - knowledge fact: `fav:`, `habit:`, `tell:`, `route:`;
   - occasionally the bond ◆ becomes available.

Scale: **each resident needs 2–3 favourite activities × 1 authored hangout** (about 45 scripts), plus a generic per-activity fallback that uses their voice keys. Implementation reuses the `Bonds` pattern: `HangoutOpen`, then `HangoutPick`, reusing `BondLine`/`BondChoice`.

### 4.3 Activities by place, with mini-interactions

| Activity | Place (RoomType) | Mini-interaction | Who shines | Feeds the mystery with |
|---|---|---|---|---|
| **카드 (블러프)** | Lounge, 작은 응접실, GameRoom | Three rounds. The partner bets and says a line each round; the player Calls or Folds. The partner's lies (Deceit, seeded RNG) are performed with **their tell** (bible §2). | 진우 (fakes a tell on purpose), 수아, 해린, 시온, 태겸 | **Tells**: `tell:<id>` in the notebook (§4.5) |
| **피아노 연탄** | MusicRoom | Choose a tempo (slow / steady / fast) and lead or follow. The partner drifts in character (재하 speeds up at the chorus, 라온 drags you back to the beat). Music state `Bond`. | 재하, 라온, 은결 (hymns), 도윤 (precise) | Voices and melodies (K2), who plays at what hour |
| **차** | 다과실, Lounge, Kitchen | Serve or accept a cup: sugar or none, scented or unscented, hot or cooling. The partner has a real preference (§5). | 도윤 (unscented), 은결 (serves others), 민서 (barley tea, thermos), 가온 (instant coffee) | Who drinks what, and whose cup is where: poisoning knowledge (§7.4) |
| **요리** | Kitchen | Chop, then **taste for 준서** ("간 좀 봐 줄래요?"). Honesty matters. | 준서; 시온 eats; 진우 wants dessert | The knife rack order; the pot as a poison vector; his lost taste |
| **원예** | Greenhouse, Courtyard | Water, prune, pot. Choose a pot and a plant. 준서 or 은결 warns: "그건 디기탈리스예요. 손 씻고 오세요." | 준서, 은결, 민서 | Who knows the poisonous plants; freshly dug soil (the `Bury` trick) |
| **낭독** | Library, Chapel | Pick a book (`Lore.Books`). The partner reacts through their lens: 진우 psychoanalyses, 가온 checks sources, 시온 already knows it (the secret reader), 은결 reads it like a eulogy. | 시온, 은결, 가온, 진우 | Mansion lore; voices (재하 can repeat any read passage later) |
| **당구** | GameRoom | Choose a shot (safe / bank / power); the result depends on the partner's style. 태겸 computes angles aloud; 세나 wants a rematch. | 세나, 태겸, 시온, 민서 | Competitiveness; who is a sore loser (flashpoint seeds) |
| **다트** | GameRoom, bar | Aim at the centre or a risky ring. The partner comments; **도윤 hits the bull every time** and says nothing (unsettling precision). | 세나, 도윤, 해린 | A quiet red flag |
| **암실 사진** | 사진 암실 (Darkroom) | Develop photos taken in daily life (by 예담, 채령 or the player with a borrowed camera). A print shows *who was in the background at that time*. | 예담, 채령, 가온 | **Photo evidence** (§7.5), only if case-relevant |
| 체스 | Library, Study, 작은 응접실 | Three moves with a question each; 진우 asks a psychological question per move; 태겸 prices each piece | 진우, 태겸, 도윤 | 진우's reads of others (hints, not facts) |
| 산책 | Greenhouse, Courtyard, RainCorridor, Gallery | Walk a route; the partner stops at *their* spot (은결's third pillar, 민서's 112 steps) | 은결, 민서, 가온 | **Routes and timings** |
| 수영 | Pool | 이현 floats; the talk is a "deal". 세나 does laps. | 이현, 세나 | The pool, the drain, the water room (`Drown`, `Dump`) |
| 공예 | 공방, DollRoom, Lounge | Fold lilies (은결) or paper models (예담); sew a button (채령) | 은결, 예담, 채령 | Thread and cord access (the `Seal` trick) |
| 수리 | 공방 | Hold the flashlight while 해린 fixes something; "문제없어" moments | 해린, 도윤 | Timers, wiring, breakers |
| 대사 맞추기 | Theater | Read opposite 재하; he repeats your line perfectly | 재하, 수아 | Voice imitation, the recorder trick |
| 한잔 | Bar | Two glasses in, a secret hint comes out | 시온, 라온, 이현 | Secret hints (not facts) |

### 4.4 Who loves what

♥ = favourite (authored hangout); ○ = will do it (generic hangout); ✕ = refuses, with an in-character line.

| | Cards | Piano | Tea | Cook | Garden | Read | Billiards | Darts | Darkroom | Chess | Walk | Swim | Craft | Repair | Lines | Bar |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 진우 | ♥ | ○ | ○ | ✕ | ✕ | ♥ | ○ | ○ | ○ | ♥ | ○ | ✕ | ✕ | ✕ | ○ | ○ |
| 서윤 | ○ | ○ | ♥ | ○ | ○ | ♥ | ✕ | ✕ | ○ | ○ | ♥ | ✕ | ○ | ○ | ✕ | ✕ |
| 도윤 | ○ | ♥ | ♥ | ✕ | ○ | ○ | ✕ | ♥ | ○ | ♥ | ○ | ✕ | ♥ | ○ | ✕ | ✕ |
| 이현 | ♥ | ✕ | ○ | ✕ | ✕ | ○ | ○ | ✕ | ✕ | ○ | ○ | ♥ | ✕ | ✕ | ♥ | ♥ |
| 태겸 | ♥ | ✕ | ○ | ✕ | ✕ | ○ | ♥ | ✕ | ✕ | ♥ | ○ | ✕ | ○ | ○ | ✕ | ○ |
| 시온 | ○ | ○ | ✕ | ○ | ✕ | ♥ | ♥ | ○ | ✕ | ✕ | ○ | ○ | ✕ | ✕ | ○ | ♥ |
| 라온 | ○ | ♥ | ○ | ✕ | ✕ | ✕ | ○ | ♥ | ✕ | ✕ | ♥ | ✕ | ✕ | ○ | ✕ | ○ |
| 재하 | ○ | ♥ | ○ | ✕ | ✕ | ♥ | ✕ | ✕ | ✕ | ✕ | ○ | ✕ | ✕ | ✕ | ♥ | ○ |
| 준서 | ✕ | ✕ | ○ | ♥ | ♥ | ✕ | ✕ | ✕ | ✕ | ✕ | ○ | ✕ | ✕ | ✕ | ✕ | ○ |
| 해린 | ♥ | ✕ | ✕ | ✕ | ✕ | ✕ | ○ | ♥ | ○ | ○ | ✕ | ✕ | ○ | ♥ | ✕ | ✕ |
| 수아 | ♥ | ○ | ○ | ○ | ♥ | ✕ | ✕ | ✕ | ○ | ✕ | ○ | ✕ | ○ | ✕ | ♥ | ✕ |
| 세나 | ○ | ✕ | ✕ | ✕ | ✕ | ✕ | ♥ | ♥ | ✕ | ○ | ○ | ♥ | ✕ | ✕ | ✕ | ○ |
| 은결 | ✕ | ♥ | ♥ | ✕ | ♥ | ♥ | ✕ | ✕ | ✕ | ○ | ♥ | ✕ | ♥ | ✕ | ✕ | ✕ |
| 가온 | ○ | ✕ | ♥ | ✕ | ✕ | ♥ | ✕ | ✕ | ♥ | ○ | ♥ | ✕ | ✕ | ✕ | ○ | ✕ |
| 채령 | ○ | ✕ | ○ | ✕ | ○ | ✕ | ✕ | ○ | ♥ | ✕ | ○ | ✕ | ♥ | ✕ | ✕ | ○ |
| 예담 | ♥ | ○ | ✕ | ○ | ○ | ✕ | ○ | ○ | ♥ | ✕ | ○ | ✕ | ♥ | ✕ | ○ | ✕ |
| 민서 | ✕ | ✕ | ♥ | ○ | ♥ | ○ | ○ | ✕ | ✕ | ○ | ♥ | ✕ | ✕ | ○ | ✕ | ✕ |

A refusal is a good line in itself:
- 준서 on cards: "카드는 잘 몰라요. 대신 간식은 제가 가져갈게요."
- 채령 on piano: "피아노는 손을 너무 드러내. 별로."
- 민서 on the bar: "술은 안 합니다. 보리차면 가겠습니다."

### 4.5 Card night teaches tells

- Losing or winning a round *against a bluff* reveals the partner's tell.
- The tell is stored as the fact `tell:<id>`, and the notebook gets one line, e.g. "해린은 거짓말할 때 ‘문제없어’라고 한다."
- From then on, whenever that resident speaks a lie (`Utterance.Lie`), in daily life, investigation or the 심판, their **tell gesture/phrase is performed**. This is fair play: it is performed whether or not the player learned it. The notebook simply makes it legible.
- 진우 is special. He fakes a tell on purpose ("그거? 가짜야. 진짜는 알려 줄 리가 없잖아"). His real one (the candy crunch) comes only at bond III.

### 4.6 Example hangouts

**도윤 — 다과실, 차**

> 도윤: 무향 차입니다. 향이 없으면 잔의 결이 보이지요.
> ▸ 그대로 마실게요. → 도윤: 그렇군요. …더하지 않는 분이시네요. *(호감+, fav:P04:무향차)*
> ▸ 설탕 있어요? → 도윤: 있습니다. 드리지요. 저는 넣지 않습니다만.
> ▸ 잔 가장자리가 좀 빠졌네요. → 도윤: 보셨군요. 붙일 수는 있습니다. 흔적은 남겠지만요. *(존중+; restoration offer unlocked)*
> *(Moment 2: he sets his cup at the saucer's left end, adjusts it by a finger's width)*
> ▸ 그 자리, 정해져 있어요? → 도윤: 1센티미터쯤입니다. 제자리가 있으면 마음이 편하지요. *(habit:P04:cup)*
> 도윤: 차가 식었군요. 오늘은 여기까지 하시지요.

**준서 — 주방, 요리**

> 준서: 오늘은 된장국이에요. 우선… 같이 썰어 줄래요? 칼은 위에서 두 번째 거요. 제일 잘 들어요. *(the player learns the rack order)*
> 준서: 간 좀 봐 줄래요? 제 혀가 요즘 좀 게을러서요.
> ▸ 좀 싱거워요. → 준서: …역시. 짠맛이 제일 먼저 도망갔거든요. *(신뢰+; hint:P10)*
> ▸ 딱 좋아요. → 준서: 다행이다. *(if it was bland, he'll trust your palate; the next table topic is "국이 싱겁다")*
> ▸ 직접 맛보시면 되잖아요. → 준서: …그러게요. 허허. *(호감−)*
> *(outcome: item "된장국 한 그릇")*

**해린 — 공방, 카드**

> 해린: 카드 알아? 세 판. 숫자 큰 쪽이 이기는데, 뒤집기 전에 걸 수 있어. 뻥 쳐도 돼.
> *(round 1)* 해린: 문제없어. 이번 건 무조건이야. *(goggles down: bluff)*
> ▸ 콜. → 해린: 아, 씨… 어떻게 알았어? *(tell learned)*
> ▸ 폴드. → 해린: 헤헤. 겁먹었네.
> *(round 3, honest)* 해린: 음… 애매해. 진짜로.
> 해린: 너 방금 내 표정 봤지? …아니, 말 안 해도 돼.

**재하 — 음악실, 피아노 연탄**

> 재하: 민혁아, 연탄 할래? 넌 낮은 쪽, 난 높은 쪽. 틀려도 돼. 박수는 내가 칠게.
> *(chorus: he speeds up)*
> ▸ 재하야, 너 빨라졌어. → 재하: 아… 그러네. 누가 늘 여기서 빨라졌거든. …아니야. 계속하자. *(hint:P09)*
> ▸ 따라간다. → 재하: 브라보. 너 박자 좋다.
> 재하: 3열 7번 자리 보여? 거기, 비워 두자.

---

## 5. Gifts and favourites

**Rules.**
- Loved = +0.09 like and +attach. Liked = +0.06. Neutral (consumables) = +0.03. Disliked = −0.03 with a line.
- The **signature gift** plays a unique short scene once per loop.
- Gifts come from **activities** (the existing `MakeItem` in `PlayerActivities`: bread, tea, flowers, stickers, paper models, windup toys, buttons) and from the exchange market. There is no shop or gacha.
- **채령 logs every gift** she receives (who and when). She quotes it later, and in the next loop the player can use the date (bond IV).

Existing item types are listed first; proposed new small items are marked *(new)*. Adding items touches `ItemCatalog` in `WorldTypes.cs`, which murder-foundation is extending, so coordinate.

| Resident | Loved | Liked | Disliked | Signature gift → reaction |
|---|---|---|---|---|
| 진우 | 사탕 | 두꺼운 책 | 꽃 ("뻔한 위로 같아") | *투명 사탕 (new)* → "…투명? 네가 이걸 골랐어? 큭. 맛이 안 보이는 걸 줬네. 믿는다는 뜻이야, 시험이야?" |
| 서윤 | *좋은 볼펜 (new)* | 쓴 초콜릿 ("달지 않은 과자"), 스티커 (동물) | 사탕 | *햄스터 스티커* → "…이거 회의록 닮았어요. 후훗. 명단 맨 끝에 붙일게요." |
| 도윤 | 무향 차 | 꽃 (흰색) | 스티커 ("붙이면 흔적이 남지요") | *이 빠진 찻잔 (new)* → "고칠 수 있겠습니다. …아니, 고치게 해 주셔서 감사합니다." |
| 이현 | *금박 동전 초콜릿 (new)* | 탄산수 (after a swim) | 수첩, 녹음기 ("기록은 좀") | Gold-foil chocolate → "금이라. 하하하. 가짜인 걸 알고 주셨죠? 그게 마음에 듭니다." |
| 태겸 | 쓴 초콜릿 | *포장지 (new)*, 무향 차 | — (accepts everything and books it) | *포장지* → "…이 접힘. 삼각으로 두 번. 이건 장부에 적지 않겠습니다." |
| 시온 | 맥주, 두꺼운 책 | 매운 과자 | 무향 차 ("할아버지 음료잖아") | *Used book* → "야, 이거 밑줄 그어진 거잖아. 남이 좋아한 문장… 레알 최고의 선물이다." |
| 라온 | 핫팩 | 탄산수 | 초대장 ("구호 나오는 모임이지?") | *A cooled hot pack* → "…식은 거네. 이거 모으는 거 어떻게 알았어. 크. 3초 감동." |
| 재하 | *낡은 공연 팸플릿 (new)* | 꽃 | 즉석 카메라 ("찍지 마") | *팸플릿* → "세상에… 이 극단, 없어진 데야. 민혁아, 이거 어디서 났어?" |
| 준서 | 꽃, *씨앗 봉투 (new)* | 따뜻한 빵 (made by someone else) | — (never refuses food) | *씨앗 봉투* → "심으면 뭐가 날까요. …누가 심어 준 씨앗은 처음이에요." |
| 해린 | 태엽 장난감, 탄산수 | 손전등 | 꽃 ("관리 못 해") | *Unshaken soda* → "안 흔든 거지? …진짜네. 헤헤. 이건 기억해 둘게." |
| 수아 | 스티커, 매운 과자 | 꽃 | 녹음기 ("몰래 녹음은 싫어요") | *Spicy snack, given discreetly* → "쉿, 이거 비밀이에요. …고마워요. 진짜 나한테 준 거죠?" |
| 세나 | 탄산수 | 매운 과자 | — | *핫팩* → "하나면 되겠네. …농담이야. 웃어도 돼. 진짜로." |
| 은결 | 무향 차, 꽃 (흰 백합) | *색종이 (new)* | 초대장 | *흰 꽃* → "백합이군요. …이건 산 사람한테 받아도 괜찮은 꽃입니다. 후후." |
| 가온 | *인스턴트 커피 (new)* | 수첩 | 스티커 | *Coffee, half sugar* → "반 봉지. …제가 말한 적 있었나요? 적어 둘게요. 아니, 이건 안 적을게요." |
| 채령 | 고풍 단추 (자개) | *유치한 마스코트 인형 (new)* | 스티커 ("촌스러워") | *자개 단추* → "날짜 적어 둘게. 오늘, 너, 자개 단추 하나." |
| 예담 | 종이 모형, 초대장 | 스티커 | 두꺼운 책 ("지루해") | *Paper model of a room she hasn't built yet* → "짜잔은 내 대사인데! …이거 어디 방이야? 모형에 붙일래." |
| 민서 | *보리차 티백 (new)*, *숫자 퍼즐집 (new)* | 따뜻한 빵 | Expensive things ("부담됩니다") | *퍼즐집* → "…이건 괜찮습니다. 칠 쪽부터 풀겠습니다. 허." |

The **player** is gifted bread by 준서 and hot packs by 라온, and receives a lily from 은결 after a death.

---

## 6. Relationship stages and visible payoffs

Thresholds follow `Bonds.BondAvailable`. Each stage has a payoff the player can *see or use*.

| Stage | Reached by | Visible payoff |
|---|---|---|
| 0 낯선 사이 | — | Polite and generic; they keep their distance in idle. |
| 1 아는 사이 | Bond I | They greet you by name with **their gesture**. **They tell you their haunts** ("오후엔 보통 온실에 있어요"), which pins them on the day board. |
| 2 가까운 사이 | Bond II + 반말 agreement | **They save you a seat** at meals: they wave you over, and table scenes include lines addressed to you. They invite you (`Requests` weighting). **They bring rumours to you first.** A gift hint ("요즘 단추가 모자라"). |
| 3 믿는 사이 | Bond III | **Contract** revealed. During an investigation they **share findings unasked**. In the 심판, **once per session they back one of your claims** (a supporting line and a small jury-scale bonus). Their **tell** is noted. |
| 4 루프를 건넌 사이 | Bond IV (earlier loop) | **Secret**. In later loops a unique "loop question" appears in their menu. If they are forming a plan, the question can **prevent it** (a PREVENTED INCIDENT, doc §14 7). |
| ✕ 경계 / 적대 | Grudge > .35, broken promises, searched rooms | They avoid you; testimony shortens (`refuse_answer`); they may confront you (existing routine). |

**Relationship board.** A later notebook tab, "관계", owned by clues-and-qol. Ties *you have discovered* appear as red threads between portraits, labelled with type and source ("라온 ↔ 재하 · 경쟁 · 들은 말"). It is a clue source for motives, and a Persona-social-link-style completion itch without copying its UI.

---

## 7. Foreshadowing: the culprit's innocent-looking preparation

### 7.1 Principles

1. **Observable, not announced.** Every preparation step the murder planner takes has a *cover*: what it looks like from outside. That cover is animated, placed in a real room, and sometimes has a bark.
2. **Character covers.** A cover is most natural when it matches the character's daily life: 준서 near knives, 채령 with thread, 해린 at the breakers. That is both good cover and **good red herring**, because innocent residents do the same things every day (§7.7).
3. **Watchers.** Each resident notices one kind of oddity: their witness lens (bible §6). This is how foreshadowing reaches the player — as a table remark, a rumour, or testimony — without extra UI.
4. **Few clues.** Nothing here becomes a card by itself. At investigation start, `RecallRelevant` (clues-and-qol) pulls only the case-relevant sightings and rumours.
5. **Fair play.** Anything the reveal (`RevealPlayer`) shows as preparation must have had at least one observable moment.

### 7.2 Preparation → cover → tell → watcher

The kinds are those in `MurderArchitecture_Current.md` §1–2.

| Prep step | Cover (what it looks like) | Observable tell | Natural watcher |
|---|---|---|---|
| GetItem: knife / blunt | "Cooking" or "fixing something" | The knife-rack count (`inventory`) is off; rack order changed | 준서 ("칼 하나가 비네요" at dinner), 태겸 (slip) |
| GetItem: cord / rope | "Tying boxes" / "laundry line" | Rope missing from storage or laundry | 태겸, 민서 |
| GetItem: thread (Seal) | "Sewing a button" | A spool is missing from the sewing table | **채령** ("실패가 하나 없어졌어") |
| GetItem: sedative | "Can't sleep" | Infirmary visit, med cabinet opened | 수아 (who talks to whom), 은결 (on her route) |
| GetItem: poison / foxglove | "Gardening" | Gloves at the greenhouse; a cut stem | 준서, 은결 |
| GetItem: saw (dismember) | "Tidying the cold store" | Someone asks 준서 where the bone saw is | 준서 |
| Stalk (habit, edge, asleep) | "Coincidence" | Repeated co-location with the victim; lingering at a railing or stairhead | 진우 (reads the pattern), 서윤 (schedule) |
| Sedate at a meal or tea | "Offering tea" (`offer_tea`) | Tea brought to someone they *dislike*; they don't drink their own | 도윤 (cups out of place), 준서 |
| PlantPoison in a personal item (08:00–20:30) | "Wrong room, sorry" | Seen leaving the victim's bedroom corridor | 은결 (route), 가온 (door logger) |
| ArmTrap (shock, topple, tripwire) | "Loose wiring, checking it" | 해린: "그 배선, 어제는 멀쩡했는데"; a water puddle by a machine | **해린**, 라온 (hum changes) |
| Record (recorder alibi) | "Rehearsing lines" | The `rehearse` line is oddly generic ("오늘은 방에서 좀 쉬어야겠다"); a recorder borrowed | **라온** (knows live from replay), 재하 |
| Courier / lure note | "Just passing a note" | The courier later goes silent (IG10) | 수아 (who talked to whom) |
| Tod (heat or cold) | "It's cold, lighting a fire" | Fireplace lit in an unused room; frost in the cold store | 서윤 (schedule), 민서 (rooms) |
| Noise (boiler) | "Shower's cold" | Boiler dial at max | 라온 |
| Swap weapon | "Returning something" | Objects subtly out of place | **도윤** (things out of place). But **은결 tidies it away** on her 09:12 route. |
| KeySlide / sealed room | — | A key on the floor inside the door | 가온 (door logger), 해린 (the lock) |
| Burn | "Burning rubbish" | Incinerator running at an odd hour; fresh ash | 민서, 라온 (sound) |
| Bury | "Repotting" | Freshly turned soil in a pot nobody tends | 준서 |
| Drown / Dump | "Going for a swim" in street clothes | Wet sleeves; the water room visited | 이현 (the pool is his), 세나 (laps) |
| Gathering slip-out | "Back in a minute" (`gathering_leave`) | The gathering attendance record (existing `Arrived`/`Left`) | 예담 (host), 서윤 (head count) |
| Alibi request | "Come watch me rehearse at 3" (§3.7 `alibi`) | Oddly precise times | — (the player *is* the alibi) |

**Cover barks** are per character (`cover_<kind>`). The same cover sounds different in different mouths:
- 준서 with a knife: "고기 손질해 두려고요. 저녁에 쓸 거라서."
- 해린 at the breaker: "이 차단기, 소리가 이상해. 잠깐만 볼게."
- 채령 with thread: "단추 하나 달 거야. 보지 마."
- 태겸 with rope: "창고 상자 묶을 끈입니다. 대여표에 적었습니다."

### 7.3 Reconnaissance questions

Before acting, planners ask about the victim's habits. **Innocent residents ask the same kinds of question for innocent reasons**, which gives the player real ambiguity (Raging Loop-style social deduction craft).

| Asker | Question (Korean) | Innocent reading |
|---|---|---|
| Any | {t} 씨는 보통 몇 시에 자요? …아니, 그냥요. 밤에 복도가 시끄러워서. | Noise complaint |
| 준서 | {t} 씨는 저녁 몇 시쯤 드세요? 따로 떠 놓으려고요. | Care |
| 태겸 | {place} 열쇠 대여표, 오늘 누구 이름이었습니까? | Inventory |
| 진우 | {t}는 혼자 있을 때 뭐 하는 것 같아? 궁금해서. | Curiosity |
| 은결 | {t} 씨는 밤에 방 문을 잠그시나요? | Worry |
| 가온 | {place}는 밤에도 열려 있어요? 지도에 적으려고요. | The map |

Kernel: the `recon_ask` key plus a `Knowledge.Facts` entry, `asked-about:<t>:<habit>`. The player learning who asked what, and when, is legitimate testimony.

### 7.4 Habit knowledge: "who knew about the thermos?"

- A personal-item poisoning (`Bedtime`: the victim's own drink or thermos) is only possible if the planner **knows the habit**.
- Habits are learned in daily life: table topic `tt_habit`, hangouts, gossip. Each time a habit is spoken, the kernel logs a `HabitShared` ledger event listing who heard it.
- If that method is used, the investigation can produce **one** card: "민서의 보온병 습관을 들은 사람: 식탁 · 둘째 날 아침 · 이현, 수아, 도윤, 시온."
- That is simple, fair and very Danganronpa-quality in *feel*, without any DR device.

The murder planner needs a `Knows(habit)` requirement; that belongs to murder-foundation.

### 7.5 Photos and clips

- 예담 and 채령 own cameras (`Grammars.PersonalItemFor`: film/exhibit/style → Camera). Their `film` activity takes photos.
- The player can **borrow** a camera through the existing loans.
- A photo is a kernel snapshot: `By`, `Room`, `Clock`, and the actors visible from the photographer's position (existing `Perception`), with what they were holding.
- Developing in the **사진 암실** (20 min) prints it. During an investigation, a print showing a case-relevant person at a relevant time becomes a card: "사진: 온실, 오후 3시쯤 — 뒤편에 은결". Otherwise it is a keepsake or gift.

### 7.6 The mousetrap play

- 재하's rehearsal (the small-theatre goal) uses the untitled playbill found in the box office (his bond I lore). The play's plot features **one trick** from the trick grammar, drawn at random from those available this chapter.
- If a would-be culprit attends the rehearsal, their planner gains a small weight toward that trick ("inspired").
- When the crime mirrors the play, someone says it at the table: "그 연극이랑 똑같잖아". That is foreshadowing in plain sight, with no leak, because the play always shows *some* trick.

This needs a weight hook from murder-foundation.

### 7.7 Innocent quirks that look like preparation (red herrings)

| Resident | Quirk | Looks like |
|---|---|---|
| 도윤 | Walks the corridor at 23:00 straightening frames | Night stalking |
| 은결 | 09:12 route; tidies misplaced things | Evidence removal (and sometimes it is) |
| 해린 | Checks breakers and wiring daily | Arming a trap |
| 민서 | Paces corridors counting steps | Route rehearsal |
| 가온 | Reads the door loggers | Checking her own trail |
| 준서 | Sharpens knives every afternoon | Getting a weapon |
| 진우 | Leaves twisted candy-wrapper "ropes" | A signature at scenes |
| 채령 | Carries thread and a tape measure | The sealed-room trick |
| 예담 | Changes gathering times | Alibi manipulation |
| 태겸 | Borrows and returns everything | Weapon access |
| 시온 | Loud night walks to the bar | Being out at night |
| 라온 | Tests acoustics near the boiler room | Noise masking |
| 재하 | Imitates voices | Voice alibi |
| 세나 | Runs in corridors | Fleeing a scene |
| 이현 | Floats in the pool at night | The pool and water room |
| 수아 | Private chats with everyone | Couriering |
| 서윤 | Holds keys for roster tasks | Key access |

### 7.8 Interface contract with murder-foundation

This is a request only; that workflow owns these files.
- Emit a ledger event at each prep step: `LedgerEvent { Type = "Prep", Actor = culprit, Target = victim, Plan = planId, Data = "<kind>|<item-or-room>" }`.
- Require `Knows(habit:<victim>:<item>)` for personal-item poisoning.
- Optional:
  - a weight hook for the mousetrap play;
  - a "buddy system" constraint (§8.5): a planner must separate from its watch partner;
  - read `CastTraits.MethodAffinity` (bible case roles) if useful.

The daily-life side (`Sim/Life/Foreshadow.cs`, new) only *reads* `Prep` events. It renders cover barks, marks odd sightings for rumours, and triggers recon questions.

---

## 8. After a death

### 8.1 Timeline

1. **Discovery.** The corpse-discovery workflow handles the film and gore. Then the bells and Yusti's `y_body`.
2. **Investigation**, then the **심판** and **verdict** (existing / being redesigned).
3. **The same evening: "빈자리"**
   - Yusti's dinner bell states the seat count.
   - **준서 counts spoons**: "…열여덟 개 놓았어요. 버릇이라서요. 하나 치워 줄래요? 저는 못 하겠어요."
   - The dead's chair stays empty (`empty_seat`). 은결 places a paper lily on it.
   - The Evening Table topic is forced to `tt_death`.
4. **The memorial** (§8.3), if 은결 is alive; otherwise 준서 or 서윤 hosts.
5. **Next morning**
   - 서윤's head count: "열다섯… 열다섯이 맞네요."
   - Buddy-system proposal (§8.5).
   - Keepsake requests (§8.4).
   - Inherited goals.
6. **Following days**
   - Grief routines (existing `comfort`).
   - Suspicion and rumours about the votes; guilt lines.
   - Alliances realign (§8.6).

### 8.2 Grief behaviour per resident

This is visible behaviour in the day after a death, keyed off `Needs.Grief` and Attach toward the dead.

| Resident | What they do |
|---|---|
| 진우 | Stops offering candy for a day; leaves one wrapper on the dead's chair. |
| 서윤 | Crosses the name off the roster *with a ruler*. |
| 도윤 | Tries to restore one of the dead's belongings. |
| 이현 | Delivers a eulogy he drafted twice; 가온 notices the drafts. |
| 태겸 | Inventories the dead's things; writes "반납 불가" in his ledger. |
| 시온 | Writes a word for the dead in his notebook and reads it at the memorial. |
| 라온 | Plays one long low note in the music room; nobody else is allowed in. |
| 재하 | Talks without stopping, then stops mid-sentence. |
| 준서 | Sets one spoon too many; cannot clear it himself. |
| 해린 | Tries to fix something the dead broke; can't; swears quietly. |
| 수아 | Takes her star sticker off the dead's door and keeps it. |
| 세나 | "리셋이 안 되잖아." Practises until her left hand cramps. |
| 은결 | Folds the lily; prepares the memorial; her walk skips one stop. |
| 가온 | Writes the date and the weather; no title. |
| 채령 | Looks up the last thing the dead gave her in her log. |
| 예담 | Adds a paper figure of the dead to the mansion model, then takes it off. |
| 민서 | Silently does the dead's chores from the roster. |
| 유스티 | States the seat count. His goldfish are still for one announcement. |

### 8.3 The memorial (추모의 밤, 은결 hosts)

- **Announcement.** Yusti: "차은결 님께서 예배실에 추모 자리를 마련하셨습니다. 참석은 자유입니다."
- **Scene.** Each attendee says one line about the dead: `grief` with the pair variant `grief@<victim>`. The player lays a lily and chooses whom to sit beside.
- **Kernel effects.**
  - Attendees gain Attach toward each other.
  - **Absentees are noticed**: 진우 never comes; 도윤 stands at the back.
  - After a *wrong* verdict, the executed person's friends look at those who voted for them. With CH01 open voting, they know.
- **Examples.**
  > 은결: 이제 아무도 {victim} 씨를 재촉하지 않겠네요. …백합은 한 송이면 충분합니다.
  > 시온: 오늘의 단어는 ‘{victim}’. …그냥 이름이야. 이름으로 적었어.
  > 준서: {victim} 씨 몫을… 또 떠 놨어요. 습관이 무서워요.

### 8.4 Belongings and inherited goals

- **Belongings.** Each resident's personal item (`Grammars.PersonalItemFor`) stays in their room.
  - A close friend asks the player to fetch it ("그 녹음기, 제가 가져도 될까요?").
  - 태겸 wants to inventory first. Two claimants create a small conflict; the player decides.
- **Inherited goals.** `Goals.OnOwnerGone` already chooses an heir. Give it a voice and a visible act: the heir says `goal_inherit` ("서윤 씨 당번표, 제가 이어서 쓸게요.") and actually performs the goal's activity.

### 8.5 Suspicion and the buddy system

- The morning after, 서윤 proposes two-person **watch pairs** (table topic `tt_buddy`).
  - 진우 mocks it ("짝을 지으면 범인한테 알리바이를 선물하는 거야").
  - 세나 volunteers to guard.
  - The player picks a partner or refuses.
- **Kernel.**
  - Pairs get the tag `watch:<id>` both ways.
  - `LifeAI` prefers the partner's room.
  - A planner must *slip away* from its watch partner, which creates an observable absence: the partner's testimony "잠깐 화장실 간다더니 20분".
- Fear-driven lock-ins already exist (`SafetyActivity`). Pair them with rumours about who locks their door.

### 8.6 After the verdict

| Verdict | Effect |
|---|---|
| **Correct** | Relief (`after_trial_relief`) and guilt (`after_trial_guilt`). The culprit's close friends grieve twice and may grudge the accusers. The reveal (`RevealPlayer`) is followed by one quiet night scene: a resident with Attach > .3 toward 민혁 seeks him out ("잠깐 걸을래?"). |
| **Wrong** (culprit escapes; draw execution) | Trust collapses along vote lines. The executed person's friends gain grudge against those who voted for them (known under CH01, else rumoured). Alliances realign; rumours about "who steered the vote" spread (이현 is the usual suspect). |
| **Loop reset** | Nobody remembers; the player does. Stage-IV devices (bible §4) become available. 유스티 alone may pause. |

---

## 9. Systems, data and files

### 9.1 Architecture rules

1. **Kernel first, deterministic.**
   - Randomness comes only from `S.R(Stream.Life / Dialogue / Presentation)`.
   - Iterate over ordinally sorted ids.
   - **Never** `string.GetHashCode`.
2. **No new `GameState` fields in the first phases.** Store state in:
   - `S.Flags` (double): cooldowns, counters, stages;
   - `Knowledge.Facts` (string): `habit:`, `tell:`, `rumour:`, `tie:`, `asked-about:`;
   - `Rel.Tags` / `Rel.Memory`.

   This keeps `SaveStore` round-trips IDENTICAL with no schema work. If a dedicated list is needed later, make `GameState` `partial`: a one-word edit to coordinate with murder-foundation, which also adds state.
3. **New files, partial `Simulation`.** `Simulation` is already partial, so every new system is a new file under `Sim/Life/` or `Sim/Data/` and is inert until a one-line hook calls it.
4. **Reuse types.** `BondLine` and `BondChoice` serve pair scenes and hangouts; `Request` serves seek-outs; `Gathering` serves festivals.
5. **LineBank conventions** (all additive; `LineBank.Add` concatenates, and `Init_*` methods are auto-registered by reflection):
   - `key@Pxx`: pair variant, used when the listener is Pxx.
   - `key#Pxx`: "about" variant, used when the `{t}` slot is Pxx.
   - `key~ctx`: context variant, where ctx ∈ {`morning`, `day`, `evening`, `night`, `afterdeath`, `aftertrial`, `hunger`, `rule_CHxx`, `near_Pxx`, `quiet2`}.
   - Per-character overrides of ANY keys simply use the same key in the resident's own file; `Raw` already prefers the actor over ANY.

### 9.2 The line resolver (the single most valuable small change)

`Simulation.Render` (Social.cs) gains one call: `key = LineContext.Resolve(this, speaker, listener, key, slots, rng)`. It tries, in order and with probabilities:

1. `key@listener` (p .7)
2. `key#third`, when `slots["t"]` is `@Pxx` (p .7)
3. `key~ctx` for the first matching context (p .5)
4. `key`

It then applies **no-repeat**: `S.Flags["lr:<speaker>:<key>"]` holds the last variant index, and the resolver picks a different one when more than one exists. `LineBank.Raw` needs an overload that takes an index or exclusion; that is a small addition in `LineBank.cs` (owned by playtest-and-korean — schedule it after them, or place it in a new partial file, since `LineBank` is partial).

The result: every existing call site gains pairs, context and variety with no other edits.

### 9.3 Feature table

**Fun and effort are rated 1–5.** Owner conflicts refer to the running workflows:
- `CQ`: clues-and-qol
- `TOD`: time-on-demand
- `MF`: murder-foundation
- `PK`: playtest-and-korean
- `CC`: cosmic-courtroom
- `CD`: corpse-discovery

| # | Feature | Data | Logic / API | Hook (one line unless noted) | Files | Conflicts | Fun | Effort |
|---|---|---|---|---|---|---|---|---|
| F1 | **Voice packs**: per-character overrides for every ANY/Routines/Grammar key; +6 `small_talk`, +2 `busy`/`doing_act` each; pair and context variants | LineBank keys | — | None: effective immediately for override keys | `Sim/Content/Voice/Voice_P01..P18.cs`, `Voice_NPC00.cs` (new) | None (new files). PK will see them in the text dump. | 5 | 2 |
| F2 | **Line resolver** + no-repeat | Key conventions | `LineContext.Resolve` | `Social.Render` | `Sim/Content/LineContext.cs` (new); `LineBank` index overload (partial file) | Social.cs unowned (verify); LineBank is PK's → new partial file | 5 | 1 |
| F3 | **Cast web seeding** | Ties table (bible §3.4) | `CastWeb.Apply(S)` | `Relations.InitLoop` | `Sim/Data/CastWeb.cs` (new) | Relations.cs: MF reads `Pressure`; coordinate the timing | 4 | 1 |
| F4 | **Pair scenes / flashpoints** | `PairSceneDef` (A, B, room, when, cond, lines, choices, cooldown, outcome tag) | `PairScenes.Tick`; `PairOpen`/`PairPick` (Bonds pattern); NPC-only resolution + rumour | Routines candidate (NPC side); player-present needs a Game choice UI (reuse `DialogueUI` options) | `Sim/Life/PairScenes.cs`, `Sim/Content/PairScenes_Data.cs` (new) | Game part after TOD (`DialogueUI`) | 5 | 3 |
| F5 | **Table topics** | `tt_*` keys; topic priority | `TableTopics.Scene(diners, meal)` → lines + one choice | `CinematicUI.TableTalkCo` uses the API | `Sim/Life/TableTopics.cs` (new) | CinematicUI may be touched by TOD/CD → hook after them | 5 | 2 |
| F6 | **Aftermath package** | Keys: `tt_death`, `spoons`, `memorial_*`, `goal_inherit`, `grief@victim` | `Aftermath.OnVerdict`, `OnMorning`; seat count | `LifeAI.Schedule` (meal slots); phase-change hook | `Sim/Life/Aftermath.cs` (new) | LifeAI unowned (verify); verdict hook after the 심판 redesign | 4 | 2 |
| F7 | **Hangouts** (함께 시간을 보낸다) | `HangoutDef` per (resident, activity) + generic fallback | `HangoutOptions`/`Open`/`Pick`; outcomes (items, facts) | TOD's dialogue option calls `HangoutOpen` | `Sim/Life/Hangouts.cs`, `Sim/Content/Hangouts_Pxx.cs` (new) | After TOD lands | 5 | 4 (MVP: 2) |
| F8 | **Tells** | `CastTraits.Tell` (gesture + phrase); fact `tell:<id>` | Cards hangout reveals; `Utterance.Lie` → tell | Game: `SpeechGestures` / `ActorView` on Lie; notebook line | `Sim/Data/CastTraits.cs` (new) | Game hooks after TOD (dialogue) and CQ (notebook); 심판 after CC | 4 | 2 |
| F9 | **Gifts and favourites** | `Gifts.Prefs` (§5) | `Gifts.Score(npc, item)`; signature scene | `PlayerApi.GiftScore` → `Gifts.Score` | `Sim/Data/Gifts.cs` (new) | PlayerApi likely TOD's; new items via `ItemCatalog` (MF) | 3 | 1 |
| F10 | **Festivals** | `FestivalDef` (host, room, beats, mini-game); `y_event_*` keys | `Festivals.Hourly` → `Grammars.Host` variant; attend scene | `LifeAI.Schedule` (hourly) | `Sim/Life/Festivals.cs` (new) | Grammars.cs: check MF; prefer a separate hook | 4 | 3 |
| F11 | **Rumours** | Facts `rumour:<rid>|subject|claimKey|origin|step` | `Rumours.Seed/Spread/Correct` | `Social.ChooseTopic` (+topic); PlayerApi "correct" option | `Sim/Life/Rumours.cs` (new) | Correction option after TOD; case recall after CQ | 4 | 3 |
| F12 | **Foreshadow covers** | `Prep` ledger events (MF) | `Foreshadow.Tick`: cover barks, odd sightings, recon asks, habit knowledge | Reads the ledger tail | `Sim/Life/Foreshadow.cs` (new) | Needs MF to emit `Prep` and require `Knows(habit)` | 5 | 3 |
| F13 | **Seek-outs**: per-character request text + new kinds | `req_*` keys; kinds advice/mediate/alibi/warn | `Requests` text via LineBank; new `Make*` | `Requests.cs` (small edits) | Requests.cs | TOD (appointments/time) → after | 3 | 2 |
| F14 | **Relationship board** | Facts `tie:<a>:<b>:<type>:<source>` | Discovery events from pair scenes, bonds, rumours | Notebook tab | Game UI (CQ area) | After CQ | 3 | 3 |
| F15 | **Darkroom photos** | Photo = Item (Type Photo, Note = snapshot) | `Photos.Take` (NPC film + player), `Develop` | `PlayerActivities` (darkroom action) | `Sim/Life/Photos.cs` (new) | PlayerActivities is TOD's → after | 3 | 3 |
| F16 | **Per-character intent barks** | `intent_<activity>` keys | — | `IntentLines.Line`: try LineBank first | `Game/World/IntentLines.cs` | Unclaimed, but PK edits UI strings → after PK | 4 | 1 |
| F17 | **Frozen-time ambient barks** | `idle_<activity>` keys per resident | Non-repeating pick | `IdleLife` (TOD's) | Content now; hook by TOD | TOD | 4 | 1 |
| F18 | **Day board** | Requests, events, haunts, quiet-day counter | — | Notebook | Game UI | After CQ | 3 | 2 |
| F19 | **Mousetrap play and buddy system** | Play trick draw; `watch:` tags | Weights and constraint | MF planner | — | MF | 4 | 2 |

---

## 10. Priorities and implementation order

### 10.1 Top 10 by fun per effort

| Rank | Feature | Why |
|---|---|---|
| 1 | **F2 Line resolver + no-repeat** | One hook makes every existing call site pair-aware and context-aware, and kills repetition. |
| 2 | **F1 Voice packs** | Removes every name-swapped template the player hears hourly, with zero code for the override keys. |
| 3 | **F3 Cast web seeding** | Instantly gives conversations, pressures and motives a history (bible §3). |
| 4 | **F16 + F17 Intent and idle barks** | The lines heard *most* while walking the frozen house. Content only, plus two tiny hooks. |
| 5 | **F5 Table topics** | Turns the two daily group moments into real scenes with interplay and a choice. |
| 6 | **F4 Pair scenes** | Where individuality lives: people bouncing off each other, and the player mediating. |
| 7 | **F7 Hangouts, MVP** | The core time sink of time-on-demand. Start with tea, cooking, cards, piano and walks, and only each resident's ♥ activities. |
| 8 | **F6 Aftermath** | The emotional payoff that makes deaths matter (spoons, seats, lilies, the memorial). Cheap. |
| 9 | **F8 Tells** | Links daily life to the 심판 in a way players will talk about. |
| 10 | **F9 Gifts** | Cheap; signature scenes; 채령's log gives a loop device. |

**Next tier:**
- F12 Foreshadow covers (top fun, but waits on MF).
- F11 Rumours.
- F10 Festivals: concert and quiz first.
- F13 Seek-outs.
- F15 Photos.
- F14 Relationship board.
- F19 Mousetrap and buddies.

### 10.2 Order that avoids the running workflows

There is no git in this project, so two writers on one file can clobber each other. Every phase below touches only files no running workflow owns; later phases wait for the owner to finish.

**Phase 0 — now (new files only)**
- F1 Voice packs, Korean-reviewed. They must obey bible §2 and §6, add no new "재판", and respect the 60-character page limit.
- The data files `CastWeb.cs`, `Gifts.cs` (existing item types only) and `CastTraits.cs` (tells, lenses, haunts by block, covers, method affinities).
- Content files for F2, F4, F5, F6, F7, F16 and F17 keys (inert until hooked).
- System files under `Sim/Life/` compiled but not called: `LineContext`, `PairScenes`, `TableTopics`, `Aftermath`, `Hangouts`, `Rumours`, `Festivals`, `Foreshadow`, `Photos`.
- Verify: GameCompile shows 0 errors; SimTests `campaign` shows `faults=0` and `IDENTICAL`.

**Phase 1 — tiny kernel hooks in unowned files** (check nothing else is editing them at the time)
- F2 in `Social.Render`, and fix `Social.ConvoLine` replies: use `<topic>_reply` keys instead of `small_talk`, and reaction-style `talk_like` lines only as replies.
- F3 in `Relations.InitLoop`, after confirming MF isn't editing `Relations.cs`.
- F4 NPC-only pair scenes via a `Routines` candidate.
- F6 kernel parts: meal seat count in `LifeAI.Schedule` and the `goal_inherit` bark in `Goals.OnOwnerGone`.
- F10 hourly hook in `LifeAI.Schedule`.
- Add a SimTests mode `voice` (§11).

**Phase 2 — after playtest-and-korean releases `Sim/Content` and UI strings**
- Bible §5.1 contradictions and bugs.
- Stage-IV bond devices rewritten (bible §4).
- F16 `IntentLines` hook.
- F13 request texts via LineBank keys.
- `LineBank` index overload, if not already placed in a partial file.

**Phase 3 — after time-on-demand**
- F7 wired to "함께 시간을 보낸다".
- F17 wired into `IdleLife`.
- F4 player-present choices through `DialogueUI`.
- F5 in `CinematicUI.TableTalk`. Also wait for corpse-discovery if it touches `CinematicUI`.
- F9 `GiftScore` hook.
- F13 new request kinds.
- F15 darkroom action in `PlayerActivities`.
- Interrupt-banner priorities (§2.4).

**Phase 4 — after clues-and-qol**
- F11 rumour correction option and case recall.
- F14 relationship board.
- F18 day board.
- F8 tells in the notebook and dialogue.
- The habit-knowledge card (§7.4).

**Phase 5 — after murder-foundation**
- F12 foreshadow covers: the `Prep` events and `Knows(habit)`.
- F19 mousetrap weights and buddy constraint.
- New gift and prop items in `ItemCatalog`.
- `MethodAffinity` consumption.

**Phase 6 — after cosmic-courtroom and the 심판 redesign**
- Tells in the 심판.
- Stage-3 claim support.
- Yusti's seat count at the verdict.

---

## 11. Verification

**New SimTests mode `voice`**, over 3 seeds × 3 days, headless:

| Metric | Definition | Target |
|---|---|---|
| `REPEAT` | Per resident per day, the maximum count of any single rendered ambient line (`small_talk`, `talk_like`, `busy`, `doing_act`, `meal`, `greet*`) | ≤ 3 (today: 15) |
| `ANYRATE` | Share of daily speech rendered from an ANY/Routines/Grammar template | < 5% (today: every routine and grammar line) |
| `NAMED` | Lines per day in which a resident names another resident, with the correct listener | ≥ 25 |
| `PAIR` | Pair scenes per day (NPC-only + player-present) | 3–6, with at most 2 in front of the player |
| `TABLE` | Distinct table topics over 3 days | ≥ 5 |
| `TIES` | Distinct tie tags that changed state during the run | ≥ 8 |
| `GRIEF` | After each death: memorial held, seat count spoken, spoons line, one inherited goal | 100% |

Also:
- Existing `campaign`: `faults=0`, `roundtrip=IDENTICAL`, since all new state lives in Flags, Facts and Tags.
- The text dump (`textdump`) re-run: no "재판" in dialogue, no page over 60 characters, no `…` empty slot.

**Probe shots (Unity)**
- A Morning Table with a topic.
- A pair scene with a mediation choice.
- One hangout per mini-interaction type.
- The memorial.
- A "cover" moment: 준서 at the knife rack with a bark.

Judge them the user's way: screenshots and play feel. Is it more fun, richer, more polished?
