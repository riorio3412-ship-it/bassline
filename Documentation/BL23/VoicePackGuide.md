# BASSLINE BL23 — Voice Pack Guide (보이스 팩 작성 가이드)

Written 2026-09-27. How to give a resident their own words for any line key, including the ones that used to be one shared sentence for all 17 residents. This implements DailyLifeDesign §9.1–9.2 (features F1 voice packs and F2 line resolver) and follows CharacterBible §2 (voice matrix) and §6 (rules for line writers).

**Files**
- Voice packs: `Assets/BASSLINE/BL23/Sim/Content/Voice/Voice_Pxx.cs` (P01–P18) and `Voice_NPC00.cs`.
- Resolver and authoring helpers: `Sim/Content/LineContext.cs`.
- Request-text hooks: `Sim/Content/Voice/VoiceHooks.cs`.
- Data the voices come from: `Sim/Data/CastTraits.cs` (tics, laughs, registers, lenses, tells), `Sim/Data/CastWeb.cs` (who has a tie with whom), `Sim/Data/Gifts.cs`.

---

## 1. How a line is chosen

Every kernel line goes through `Simulation.Render(speaker, listener, key, slots)`, which now calls `LineContext.Resolve`. The resolver tries these pools, in order. Each tier is used only when the speaker actually has lines for it.

| Tier | Key form | Meaning | Chance when present |
|---|---|---|---|
| 0 pair | `small_talk@P03` | said **to** this listener | 70% |
| 1 about | `grief#P10` | said **about** the person in `{t}` / `{victim}` / `{t2}` (never the listener) | 70% |
| 2 situation | `small_talk~afterdeath` | the speaker's situation right now (§4) | per tag, 35–85% |
| 3 own | `small_talk` | the speaker's generic lines | always |
| 4 shared | `ANY` `small_talk` | the old shared fallback | last resort |

If a pair, about or situation pool loses its roll and the speaker has **no** generic lines for the key, that pool is still used before the shared line. So one `comfort@P14` line is enough to keep 도윤 off the shared `comfort` when he comforts 은결.

**No repeats.** Each speaker keeps a deck per pool (`S.Flags["lr:<speaker>:<pool>:<P|C>"]`). Every variant is heard once before any repeats, and the same line never plays twice in a row. The memory is part of the save; randomness is `S.R(Stream.Dialogue)` only.

**The listener is never named in the third person.** Outside the pair tier, a variant containing the listener's given name ("재하는…" said *to* 재하) is skipped while any other variant is available.

**Addressed routine keys.** Routines walk up to someone and speak without a listener, passing the person as `{t}`: `comfort`, `offer_tea`, `confront`, `applause`, `stroll_invite`, `stroll_accept`, `courier_deliver`, `argue_*`. For these keys the resolver treats `{t}` as the listener. That enables pair lines (`comfort@P14`), sets 반말/존댓말 from the speaker's relationship with that person, and fills `{you}`.

**Presentation-only barks** (`IntentLines`, and later idle barks) use `LineContext.Presentation` / `LineContext.Intent`. These read only the speaker's own pool, never touch kernel state, and keep their own no-repeat memory. They are safe to call from Unity at any frame.

---

## 2. Writing a pack

A pack is one more `Init_*` method of the partial `LineBank`. It is registered automatically by reflection, and adding lines to a key the resident already has simply extends that pool.

```csharp
namespace BL23.Sim
{
    public static partial class LineBank
    {
        // P10 강준서 — one line on register, tics and ties (see the existing packs)
        static void Init_Voice_P10()
        {
            const string A = "P10";
            Add(A, "small_talk", P("우선 앉아요. 먹고 얘기해요."), C("우선 앉아. 먹고 얘기하자."));   // both registers
            VP(A, "meal", "어이쿠, 숟가락이 하나 비네. 누구 안 왔어요?");                         // polite only
            VC("P13", "busy", "지금 {act} 중. 이 판만 끝내고.");                                    // casual only
            PairP(A, "small_talk", "P16", "{you}, 오늘은 안 따라갈게요. 대신 온실에 있을게요.");   // TO 채령
            AboutP(A, "grief", "P16", "{victim} 도시락… 또 싸 놨어요.");                           // ABOUT 채령
            CtxP(A, "small_talk", "at_Greenhouse", "바질 잎 좀 보세요. 여기서도 이렇게 자라요.");  // in the greenhouse
        }
    }
}
```

| Helper | Registers | Stored under |
|---|---|---|
| `Add(who, key, P(...), C(...))`, `V(...)` | both | `key` |
| `VP(who, key, ...)` / `VC(who, key, ...)` | polite / casual | `key` |
| `Pair`, `PairP`, `PairC` | both / polite / casual | `key@Pxx` |
| `About`, `AboutP`, `AboutC` | both / polite / casual | `key#Pxx` |
| `Ctx`, `CtxP`, `CtxC` | both / polite / casual | `key~tag` |

**Registers.**
- `casual` is `Rel(speaker → listener).Casual`. With no listener it is the resident's default (`!Speech.PoliteDefault`). Yusti is always polite.
- When a pool has only one register, the other falls back to it.
- Casual-default residents write `C` only: 진우, 시온, 라온, 재하, 해린, 세나, 채령, 예담.
- Polite-default residents write `P`, plus `C` for the keys said to someone they have agreed 반말 with. That means player-facing keys (`req_*`, `greet~stage2/3`) and close-friend lines.
- **도윤 and 은결's `C` lines are 해요체, not 반말.** 반말 exists only between the two of them (`@P14` / `@P04`).

---

## 3. Slots

Use slot particles, never a hard-coded particle after a slot. For example `{t:이}` gives "은결이" / "은결 씨가" / "태겸 씨가".

| Slot | Value | Particle forms |
|---|---|---|
| `{you}` | the listener as the speaker calls them: "민혁 씨" or "민혁" | `{you:아}` vocative ("민혁아", but "민혁 씨" when polite), `{you:은}`, `{you:이}` |
| `{t}` | a resident, rendered as the speaker calls them | `{t:이}`, `{t:을}`, `{t:은}`, `{t:와}`, `{t:이에요}`, `{t:이랑}` |
| `{victim}` | the dead, in grief and death keys | same particles |
| `{u}` | the second person in `saw_handover` | same particles |
| `{place}` | room name ("온실", "대현관 홀") | `{place:으로}`, `{place:이}` |
| `{time}` | spoken time ("오후 4시 반쯤"): it already carries 쯤/조금 넘어, so don't add them | — |
| `{when}` | exact appointment time for requests ("오후 3시 20분") | — |
| `{item}` | item name ("두꺼운 책") | `{item:이}`, `{item:을}`, `{item:이에요}` |
| `{act}` | activity or gathering label ("차 한잔", "독서") | `{act:을}`, `{act:이}`, `{act:이나}` |
| `{topic}`, `{reason}`, `{sound}` | as in the existing files | — |

Write `{t}` for a name, never "{t} 씨": the slot already has the honorific. Old lines that did this are tidied by the renderer.

---

## 4. Situation tags (`key~tag`)

Tags are computed only for speakers who have at least one `~` variant of the key. They are listed in priority order; the chance is the probability that a matching pool beats the generic one.

| Tag | True when | Chance |
|---|---|---|
| `afterdeath` | a death this loop, confirmed less than 24 h ago and known to the speaker | 85% |
| `aftertrial` | within a day after a 심판 (chapter 2+) | 80% |
| `grief` / `afraid` / `angry` / `stressed` / `tired` | the speaker's needs: Grief > .35, Fear > .5, Anger > .5, Stress > .6, Energy < .25 | 50–70% |
| `hunger` | the house is hungry this chapter (`hunger:<loop>:<chapter>` ≥ 1) | 55% |
| `quiet` | no death for over 24 h (daily phase) | 45% |
| `rule_CHxx` | an announced, active chapter rule, e.g. `rule_CH05` | 55% |
| `near_Pxx` | resident Pxx is awake in the same room (not the listener) | 60% |
| `stage1`–`stage4` | listener is 민혁 and the speaker's bond stage this loop | 60% |
| `loop2`, `loop3` | loop ≥ 2 / ≥ 3. **Yusti only** (residents never know the loop) | 60% |
| `at_<RoomType>` | the speaker's room type, e.g. `at_Kitchen`, `at_Library`, `at_Greenhouse`, `at_Workshop`, `at_Pool`, `at_Chapel`, `at_GameRoom`, `at_Storage`, `at_Archive`, `at_Gallery`, `at_Wardrobe`, `at_Theater`, `at_MusicRoom`, `at_DollRoom`, `at_PowerRoom` | 45% |
| `morning` / `day` / `evening` / `night` | 05–11 / 11–17 / 17–21 / 21–05 | 35–50% |

**Greetings by relationship stage.** `greet~stage1..3` fire when 민혁 opens a conversation, stage 1 being 아는 사이 after bond I (DailyLifeDesign §6). Write stage 2 and 3 in both registers for polite-default residents: stage 2 usually comes with the 반말 agreement.

---

## 5. Key list

"Fires from" names the code that speaks the key; `lst` means the key has a listener (pair tier available).

### 5.1 Daily talk (the most-heard lines)

| Key | Slots | Fires from | Notes |
|---|---|---|---|
| `small_talk` | `{you}` | NPC–NPC chat (opener and most replies), 민혁's chat, table talk, time together — lst | The biggest pool. Aim for 12+ generic lines plus situations and pairs. |
| `talk_like` | `{topic}` (the speaker's own like) | chat — lst | Write it as the speaker *offering* the topic, not reacting to it. |
| `busy` | `{act}`, `{you}` | brushing someone off — lst | |
| `doing_act` | `{act}` | time together, joined activity — lst (민혁) | |
| `talked_recently`, `sleepy` | — | time together refusals — lst (민혁) | |
| `meal` | — | table talk, meal together — lst | 세나 has `meal@P10` (to 준서, bible fix). |
| `greet`, `greet_morning`, `greet_close`, `greet_cold`, `intro_self` | `{you}` | 민혁 opens a conversation (DialogueUI) — lst | Use `greet~stageN` for relationship stages. |
| `argue_open`, `argue_reply`, `argue_stormoff`, `apology`, `forgive`, `not_forgive` | `{t}` = the other | NPC argument topics — lst | 세나 has `apology@P18` (the 민서 arc). |

### 5.2 Routines (formerly one shared sentence)

| Key | Slots | Said by → to |
|---|---|---|
| `comfort` / `comforted` | `{t}`, `{you}` | a friend → the grieving or frightened / the answer |
| `applause` / `applause_thanks` | `{t}`, `{you}` | the listener → the performer / the performer's thanks |
| `confront` / `confront_reply` | `{t}`, `{you}` | the angry one → the grudge target / the reply |
| `offer_tea` / `thanks_tea` | `{t}`, `{you}` | the kind-hearted (Loyalty + Sociability > .85) → the worn out / the thanks |
| `stroll_invite` / `stroll_accept` | `{place}`, `{you}` | a friend → a friend |

### 5.3 Everyday grammars (IG01–12)

| Key | Slots | Notes |
|---|---|---|
| `borrow_ask` / `borrow_yes` / `borrow_no` | `{item}` | loans |
| `lend_give`, `return_item`, `return_thanks`, `return_where` | `{item}`, `{place}` | |
| `item_missing`, `item_missing_suspect` | `{item}`, `{t}` | the suspect version names who |
| `coat_offer`, `coat_thanks` | — | |
| `gathering_invite` / `_change` / `_yes` / `_no` / `_chat` / `_leave` / `_back` / `_stoodup` / `_explain` | `{time}`, `{place}`, `{act}`, `{t}` = host | `_leave` / `_back` are also the gathering-slip trick's cover: keep them innocent |
| `courier_ask`, `courier_deliver` | `{t}` = addressee | the courier grammar and the lure-note trick |
| `repair_claim` | — | **해린's lie**: "문제없어. 다 봤어." Only proud residents say this key. |
| `rehearse` | — | **Leave it shared on purpose.** It is the recorder-alibi line; its odd, generic wording is the tell (DailyLifeDesign §7.2). |
| `courier_confess` | `{time}`, `{t}` = who asked, `{victim}` | testimony: the courier admits carrying a note |
| `trap_found` | `{place}` | a loud alarm (the trap kind is not in the slots, so say "장치") |
| `saw_handover`, `heard_voice` | `{time}`, `{t}`, `{u}`, `{place}` | **testimony**: colour it with the witness lens (§6.3) and never invent facts beyond the slots |
| `hurt_react`, `startle` | — | bumping into someone |

### 5.4 Walk-off barks (`IntentLines`)

`intent_<activity>` is the line said when someone sets off: `LineContext.Intent(actor, tag)`, which reads the resident's own pool only. When a resident has no line for a tag, the old shared table in `Game/World/IntentLines.cs` is used.

Tags: `eat`, `snack`, `cook`, `read`, `organize`, `restore`, `tea`, `walk`, `swim`, `game`, `music`, `perform`, `garden`, `repair`, `inspect`, `craft`, `film`, `party`, `socialize`, `rest`, `sleep`, `exercise`, `puzzle`, `carry`, `trade`, `style`, `exhibit`, `investigate`, `observe`, `speech`, `pray`, `laundry`, `cleanup`, `explore`, `tour`, `mourn`, `listen`, `chess`, `browse`, `bar`, `company`, `safety`, `meeting`.

Write these in the resident's default register, with no listener.

### 5.5 Requests to 민혁 (`Requests.cs` → `VoiceHooks.cs`)

When the asker has the key, the voiced line replaces the hard-coded sentence; otherwise the old sentence stays.

| Key | Slots | When |
|---|---|---|
| `req_invite_<activity>`, then `req_invite` | `{when}`, `{place}`, `{act}`, `{you}` | an invitation; `<activity>` is tea, walk, swim, game, music, read, garden, perform, … |
| `req_find` | `{item}` (the type name, never "내 진우의 …") | a lost thing of theirs |
| `req_deliver` | `{t}` = addressee | take this note to … |
| `req_accept_invite` / `_find` / `_deliver` | as above | 민혁 said yes |
| `req_refused` | — | 민혁 said no. It must respect the refusal: never "싫으면 말고". |
| `req_thanks_find` | `{item}` | the owner gets it back |
| `req_thanks_deliver` | `{t}` = the sender | the addressee gets the note |

The sentence must fit any activity: use `{act}`, or write a `req_invite_<activity>` variant.

### 5.6 Death and grief

| Key | Slots | Notes |
|---|---|---|
| `grief`, `grief_close` | `{victim}` (also `{t}` at the table) | Write `grief#Pxx` for the resident's closest ties. |
| `discover_shock`, `scream_discover`, `empty_seat`, `victim_named` | `{victim}` | |
| `small_talk~afterdeath` | — | Everyone should have 2–3. It replaces chatter for a day, with an 85% chance. |

### 5.7 Yusti and 민혁

- **Yusti** (`NPC00`): `y_idle` is what he says when 민혁 visits his room (`~afterdeath`, `~night`, `~hunger`, `~loop2`). Announcements (`y_*`) go through `LifeAI.Announce`, not the resolver.
- **민혁** (`P01`): `p_*` keys. `p_greet@Pxx` lets him greet each resident in their own terms. His lines never state more than the evidence shows.

---

## 6. Rules (CharacterBible §0, §2, §5.2, §6)

### 6.1 Natural Korean
- Write how a 20-year-old Korean adult of that background actually talks. Avoid translationese: no "당신" for "you", no stacked "의", no subject pronoun where Korean drops it. Read the line aloud.
- A subtitle page is at most **60 characters**; `|` starts a new page.
- No stage directions in brackets. Show the gesture in the words ("…펜이 잠깐 안 나와서요").
- Say **심판**, never 재판 or 학급재판. No 학교 or 반장: everyone is an adult (20).
- 반말 or 존댓말 follows the relationship (`Rel.Casual`), never the relationship value alone.

### 6.2 One owner per tic

Anyone else who needs the meaning says it in their own words. `SimTests voice lint` enforces this from `CastTraits.TicOwner` / `LaughOwner`.

| Owner | Tics / laugh |
|---|---|
| 진우 P02 | 흐응, 맞혀 볼까? · 큭 |
| 서윤 P03 | 정리하면, 그러니까 (sentence-initial), head counts · 후훗 |
| 도윤 P04 | 그렇군요, 괜찮으시다면 · 후 |
| 이현 P05 | 자, 자 · 요컨대 · 말씀하신 건 ~라는 거죠? · 하하하 |
| 태겸 P06 | 조건은? 기한은? 됐습니다 · 흠. |
| 시온 P07 | 레알, 야! and nicknames (셰프, 회장님, 사장님, 스타, 베이스, 형님) · 크하하 |
| 라온 P08 | 아 뭐, · 됐고, · 헐 · 크 |
| 재하 P09 | {이름}아, · 있잖아 · 세상에 · 브라보 · 아하하 |
| 준서 P10 | 우선 앉아요 · 간 좀 봐 줄래요? · 어이쿠 · 허허 |
| 해린 P11 | 오! · 이거 봐 · 됐다! · "문제없어. 다 봤어." (her lie only) · 헤헤 |
| 수아 P12 | 꺄 · 대박 · 오늘도 반짝! · 에이~ · 에헤헤 |
| 세나 P13 | 솔직히 · 한 판 더 · GG · 하 |
| 은결 P14 | 그런가요. · 어머나 (rare) · 후후 |
| 가온 P15 | 잠깐만요. · 직접 보셨어요, 들으셨어요? · 적어 둘게요 · 흐 |
| 채령 P16 | 별로. · 풋. · 뭐, 어쨌든. · 날짜 적어 둘게 (her "thank you") |
| 예담 P17 | 짜잔! · 있지있지 · 판정합니다. · 히히 (rare) |
| 민서 P18 | 끝났습니다. (as a one-line report) · 걸음 수 · 허 |
| 유스티 | 알려 드립니다 · 정해진 대로 · 절차에 따라 |

**Rationed words.**
- 가온: at most one "확인" per three lines. 해린: at most one per five. 세나: never.
- 은결: at most one "조용/고요" per scene.
- 재하: at most one stage word per four lines.
- Each of these stock phrases may appear in one file only: "단둘이 있지 마", "기쁘지 않아", "왜 다 나를 봐", "좋아요. …아니, 좋아.", "{topic} 좋아해? 난 좋아해".

### 6.3 Voice and witness lens

Every resident's testimony carries their lens; they never use the ANY skeleton.

| Resident | Lens | Resident | Lens |
|---|---|---|---|
| 태겸 | rental-slip times | 은결 | handling order |
| 준서 | meals | 도윤 | things out of place |
| 라온 | sounds and counts | 재하 | exact words (and identity by voice) |
| 채령 | clothes | 수아 | who talked to whom |
| 해린 | mechanisms | 세나 | speed and direction |
| 민서 | steps and weights | 예담 | photos and clips |
| 가온 | sources (heard vs seen) | 진우 | faces |
| 서윤 | head counts and schedules | 시온 | who was in the library or bar |
| 이현 | who stood with whom | | |

**Testimony keys never invent facts** that aren't in the slots: no invented quotes, directions or distances beyond "쯤". The lens colours *how* they say it ("저는 그릇 치우다가 봤어요"), not *what* happened.

### 6.4 Name people, but to the right person
- Give each resident pair lines for their ties (`CastWeb.Partners(id)`, bible §3.2).
- A pair line talks **to** the person: use `{you}`, not their name, so 반말 agreements are respected.
- Lines **about** someone go in `key#Pxx` or name them in a generic line. The resolver keeps such lines away from that person.

### 6.5 Secrets never leak through barks
- Hints only: a slip, a stopped pen, a checked watch.
- The truth comes only in bond III/IV, a rule event, or the 심판.
- 도윤 and 은결 never state their crimes. 수아's caller is never confirmed as 이현.
- **Residents never know about the loop** (§5.1 #21). Only Yusti's `~loop2` pause and his `break_line` exist.
- A grief line may let a mask slip (은결's "도윤이는, 제가 보내겠습니다"), because death is a legitimate breaking point.

### 6.6 Keep the tell rare
- A tell phrase is performed on lies ("문제없어. 다 봤어.", "기억 안 나", "힘들지 않습니다", "괜찮다고"). Don't sprinkle it through ordinary lines, or it stops meaning anything.
- The sincerity markers (진우's "시험 아니야", 태겸's "그냥") belong at stage 3 or a death, once.

---

## 7. Checking your pack

```
cd Tests/BL23/SimTests
dotnet build -c Release
dotnet bin/Release/net10.0/SimTests.dll voice lint [outFile]   # page length, tic owners, banned words, pool sizes ([VOICE] = your lines)
dotnet bin/Release/net10.0/SimTests.dll voice [days] [seeds]   # REPEAT / SHARED / NAMED / TIERS over headless campaigns
dotnet bin/Release/net10.0/SimTests.dll textdump <outDir> 3    # every rendered string, with _lint.txt (particles, empty slots)
dotnet bin/Release/net10.0/SimTests.dll campaign 20260926 3    # must stay faults=0, roundtrip=IDENTICAL
```

| Metric | Definition | Target |
|---|---|---|
| `REPEAT` | per resident per day, the most times one exact ambient line was said | ≤ 3 |
| `SHARED` | share of NPC speech rendered from ANY | < 5% |
| `NAMED` | daily-life lines naming another resident | ≥ 25 per day |

**Before you commit a pack:**
- Every line reads naturally aloud.
- Nobody else's tic.
- No page over 60 characters.
- Slots carry particles.
- Testimony adds no facts.
- No secret stated.
- No loop knowledge.
- 심판, not 재판.

---

## 8. Data for writers

- **`CastTraits.Get(id)`**: register, owned tics, laugh, pet words, never-says, tell (gesture and phrase), lens, haunts by time block, fixed-time habits (은결 09:12, 진우 23:40, 도윤 23:00), innocent quirk and what it looks like, grief behaviour, case-role weights, hangout ♥/○/✕ per activity, and the refusal line.
- **`CastWeb.Ties`**: all 36 ties with type, level, history, tension and shift. `CastWeb.TieBetween(a, b)` and `CastWeb.Partners(id)` return the pairs a resident should have lines for. Knots K1–K5 start dormant; `CastWeb.RevealKnot(S, "K1")` is for bond, CH06 and 심판 writers.
- **`Gifts.Get(id)`**: loved, liked and disliked item types, the refusal line, and the signature gift with its reaction line.

---

## 9. Keys fired by daily life, banter, lies and schemes (added 2026-09-27, daily-life track)

These keys are now **spoken by code** (Sim/Life/*.cs, Sim/Murder/Scheme*). Write them in your pack exactly like the keys above
(`key`, `key@Pxx`, `key#Pxx`, `key~ctx`). A resident without a line falls back to the shared `ANY` line
(`Sim/Content/Life_Shared.cs`, `Sim/Murder/SchemeLines.cs`) or, for table keys, to the nearest everyday key in their own voice.

### 9.1 The rule for openers — never `{you}`
Lines said **to a group or to nobody** have no listener, so `{you}` renders as nothing ("…, 밥 먹었어?" → ", 밥 먹었어?").
Openers must name people through their own slots (`{t}`, `{victim}`, `{a}`/`{b}`) or not at all:
`tt_<kind>` openers (not the `_re` replies), `env_open`, `fest_*_open`, `memorial_open`, `fest_perform`, `grief_act`, `goal_inherit`,
`intent_*`, `idle_*`, `scheme_rumor`. Replies (`*_re`, `*_react`, `*_back`) have a listener and may use `{you}`.

### 9.2 Banter in NPC conversations (Social.ConvoLine → LifeBanter)
| Key | When it fires | Answered by |
|---|---|---|
| `tease` | the "tease" topic; friends on small talk (12%) | `tease_react` |
| `joke` | friends (Like > .3 or a warm tie) on small talk (25%) | `joke_react` |
| `insult` | the "argue" topic with a grudge > .4 or anger > .5; rivals on small talk (10%) | `insult_back` |
| `outburst` | the speaker's anger > .6 toward a rival (40% of openers) | `insult_back` |
| `gloat` | the third line of a quarrel (the one on top rubs it in) | `gloat_react` |
| `scared` | the speaker's fear > .5 (35% of openers) | `comfort` ({t} = the scared one) |
| `flirt`, `love_hint` | the "flirt" topic, only with an attraction tie (Romance > .3, crush, lover); to 민혁 only at stage 2+ | `flirt_react` |
| `dirty_joke` | close casual friends whose voice has it (8% of small talk) | `react_dirty` |
| `react_swear` | answering a line that swore (50%) | — |
**Guardrails in code:** `flirt`, `love_hint`, `flirt_react`, `dirty_joke`, `react_dirty` never fire with 예담 P17, 수아 P12 or 진우 P02 as
speaker, listener or subject (the line becomes plain small talk). A `tease` / `joke` / `insult` / `gloat` / `outburst` line said to or about
세나 P13 that mentions her hand is re-drawn (another variant) or replaced by small talk. Swearing itself is allowed — no filter, no toggle.

### 9.3 Lies (Testimony)
A lying answer uses `lie_<key>` when the liar has it, so the tell is heard: `lie_alibi_where` ({time}, {place}), `lie_saw_person`
({t}, {place}, {time}). `lie_tell` stays the general lying line. Tells are rare everywhere else (§6.6).

### 9.4 The Morning / Evening Table (Sim/Life/LifeTable.cs)
One topic per shared meal. The owner says `tt_<kind>` (no listener), 2–3 diners answer `tt_<kind>_re` to the owner (write `tt_<kind>_re@Pxx`
for your ties), and 민혁 may interject once. Kinds and slots:
`tt_death` / `_re` {victim} {t} · `tt_verdict` / `_re` {victim} (after a 심판) · `tt_rule` / `_re` {rule} · `env_open` / `tt_envelope_re` (CH06) ·
`tt_hunger` / `_re` {n} · `tt_buddy` / `_re` {victim} · `tt_conflict` ({t} = the other party, said to them) / `_re` · `tt_event` / `_re` {act} {place} {time} ·
`tt_absent` / `_re` {t} · `tt_rumour` / `_re` {rumour} {t} · `tt_habit` (a habit of the speaker's own — it becomes known to everyone at the table) / `_re` ·
`tt_food` / `_re`. Death specials: `tt_spoons` (준서), `tt_lily` (은결). The other party of a quarrel answers with `argue_reply`, a rumour's subject with `rumour_deny`.

### 9.5 Daily-life keys (Sim/Life; per-resident lines live in `Sim/Content/Life_Pxx.cs`)
| Key | Slots | When |
|---|---|---|
| `life_give` / `_thanks` / `_tease` / `_refused` | {item} | a resident brings 민혁 a small gift, and his three answers |
| `life_jealous` / `_soothed` / `_teased` / `_hurt` | {t} = who he favoured today | 20:00, someone attached to 민혁 who got nothing today |
| `life_rival` / `_ok` / `_defied` / `_seen` | {t} | a crush / protector / sibling / fan of the favoured one warns him off |
| `rumour_tell` | {rumour} (a whole sentence), {t} = subject | passing a rumour on (in conversations; to 민혁 first at stage 2) |
| `rumour_unsure` · `rumour_scolded` · `rumour_deny` ({t} = teller) · `rumour_correct` ({t} = subject) · `rumour_from` ({t}) · `rumour_mine` | | tracing and correcting |
| `after_trial_walk` / `_yes` / `_no` | — | the night after a 심판 |
| `grief_share` / `_back` / `_silent` | {victim} | coming to 민혁 after a death |
| `grief_act` | {t} = the dead | a resident's own grief act (DailyLife §8.2) |
| `memorial_word` | {victim} | one line at the memorial (write `memorial_word#Pxx` for close ties) |
| `card_bluff` (the tell must show) · `card_true` · `card_caught` · `card_bluff_reveal` · `card_true_win` · `card_true_fold` | — | cards (hangouts, 진우's card night) |
| `fest_quiz_open` (예담) · `fest_cards_open` / `_win` / `_lose` (진우) · `fest_concert_open` (재하) · `fest_cook_open` (준서) · `fest_bar_open` (시온) · `memorial_open` (은결, 준서, 서윤, 민서) | {nick}, {b}, {victim} | festival hosts |
| `fest_perform` · `fest_bar_banter` ({t} = host) · `fest_cook_brag` / `_lost` / `_won` | | festival guests |
| `cover_<kind>` | {item} {place} | a preparation step that looks innocent (DailyLife §7.2): knife cord thread sedative poison saw tea plant trap recorder note fire noise swap key burn bury swim gathering |
| `gift_log` | {item}, {n} = the day | 채령's "thank you": the gift logged with its date |
Hearts, hangouts and pair scenes are authored scenes, not keys: see `Sim/Life/LifeTypes.cs` and `Sim/Content/Life_P10.cs` (the exemplar).

### 9.6 Scheme keys (Sim/Murder/SchemeLines.cs, the killer-initiative track)
`scheme_errand_ask` / `_inv` / `_yes` / `_no` ({place}, {item}) · `scheme_appoint` ({time}, {place}) · `scheme_gift` ({item}) · `scheme_pour` ·
`scheme_witness_ask` ({time}, {place}) · `scheme_helper_ask` ({act}, {time}, {place}) · `scheme_rumor` ({t}, {v}) · `scheme_search` ({t}).
They must sound like ordinary daily talk — that is what makes them foreshadowing. Per-character versions in your pack take precedence.
