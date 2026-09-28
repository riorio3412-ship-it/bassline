# BASSLINE BL23 — Character Bible (캐릭터 바이블)

Written 2026-09-27 as a narrative/systems design pass. **Analysis and design only**: no code or content file was changed.

It covers the 18 residents (P01–P18) and the butler 유스티 (NPC00): an audit of how individual they feel **in play today**, the canonical voice and behaviour of each, the relationship web, and loop arcs.

Companion document: `DailyLifeDesign.md`, which covers what daily life is made of and how to build it.

**Sources**
- `Sim/Data/Cast.cs`
- `Sim/Content/Lines_*.cs`, `BondScenes_*.cs`, `Lines_ANY/Grammar/Routines/House.cs`
- `Sim/Systems/Relations, Social, Bonds, Requests, LifeAI, Goals, Routines, PlayerApi, PlayerActivities`
- `Game/World/IntentLines.cs`, `Game/UI/CinematicUI.TableTalk`
- `SourcePackage/08_REFERENCE/SOURCE_V11_PARAGRAPHS.md` §05, §06, §12 (P0742–P1221), §15
- `03_CHARACTERS_3D/BASSLINE_CHARACTER_MASTER_SHEETS.md`
- The text dump `C:/Users/리오/BL23Lab/textdump` (3 seeds × 3 days: 3,667 spoken barks)
- Three line-by-line audits (P02–P07, P08–P13, P14–P18 + NPC00 + P01)

---

## 0. Canon rules this bible assumes

1. **Order of authority**
   1. The user's directives (HANDOFF §1, memory).
   2. The planning docs' *자료 기준 / 집필 기준* (SOURCE_V11 §12).
   3. This bible.
   4. Existing lines.

   Where existing lines disagree with this bible, the fix is listed in §5.
2. **Everyone is an adult.** The source gives every resident as **20** (P0748–P1139). Past roles (학생회장, 학과) are adult roles. Lore that implies another age is a bug (§5).
3. The trial is **심판**, in dialogue as well. Residents never say 재판 or 학급재판.
4. **Nothing recognisable from Danganronpa**, and nothing copied from Ace Attorney catchphrases. Specifically:
   - Yusti is a procedure, not a host. He never jokes and never teases.
   - No resident has a courtroom catchphrase: no "이의 있음!", no "논파".
   - 예담's "judge's voice" is a referee's (판정합니다 / 반칙), not a lawyer's.
5. **Korean line rules** (LineKeys.md):
   - At most 60 characters per subtitle page; `|` marks a page break.
   - Use slot particles (`{t:이}`).
   - No stage directions in brackets.
   - Adult 반말/존댓말 follows the relationship (`Rel.Casual`), never the relationship value alone.
6. **Secrets are never stated in daily barks.** They surface through bond scenes, rules (CH05/CH06), or the 심판.
   - 도윤 and 은결 never state their crimes in words (HANDOFF §6).
   - 수아's "politician" is never confirmed as 이현.

---

## 1. Audit — how individual the cast feels today

### 1.1 Scores

"Written voice" rates the line files and bond scenes on their own. "In play" rates what the player actually meets: which lines fire and how often, how many generic system barks leak through, whether the character's relationships ever surface, model quality (GLB vs procedural), and whether they behave distinctly.

The scale is 10 = recognisable from any single line, at a commercial mystery-ADV bar.

| ID | 인물 | Written | In play | One-line diagnosis |
|---|---|---|---|---|
| P01 | 김민혁 | 5 | 4.5 | Warm but unspecific. Lines duplicate ANY; no 빵 or co-op-game texture; no "resolved" voice. |
| P02 | 김진우 | 7.5 | 6.5 | Great tics (흐응, 큭, candy tests). Two problems: the sincerity tell ("시험 아니야") is spent 8+ times, and he never names anyone. |
| P03 | 한서윤 | 5.5 | **4.5** | On spec but bland; sounds like 태겸. Her grudge against 이현 is invisible in daily play. |
| P04 | 차도윤 | 7.5 | 6.5 | Superb motifs (제자리, 금, 무향차). Problems: 반말 in panic and "당신" break the spec, and B4 nearly confesses. |
| P05 | 백이현 | 7 | 6 | Polite voice excellent; casual voice generic. He borrows 도윤's words and repeats himself in the 심판. |
| P06 | 권태겸 | 7 | 5.5 | Instantly "money robot". The 민서 tie is absent; his sincerity has almost no outlet. |
| P07 | 유시온 | 7.5 | 6 | Loud and distinct, but 브로 is used as garnish and he names nobody. Beer lines contradict each other. |
| P08 | 서라온 | 7 | 5.5 | Sound vocabulary is good. The "농담 아니야" tic replaces actual jokes; the swearing the doc asks for is missing. |
| P09 | 문재하 | 7 | 5.5 | Name-first address works, but the theatre metaphors are a caricature (the doc forbids them). He shares a lexicon with 수아. |
| P10 | 강준서 | 8 | 7 | Best match to spec (끼니, 숟가락, lost taste). His 채령 tie appears once. |
| P11 | 윤해린 | 6 | **5** | "확인" ×47 is monotone and the spec's swearing is missing. She is interchangeable with 세나, and her signature lie has no line. |
| P12 | 오수아 | 7 | 6 | Polite voice vivid, casual voice generic. She names nobody; the stage lexicon is shared with 재하. |
| P13 | 정세나 | 5 | **4** | Testimony and 심판 lines are interchangeable with 해린's and 라온's. No swearing, no 민서 arc, and her anxiety lines are borrowed. |
| P14 | 차은결 | 7 | 6 | Strong motifs, but "조용" appears 38 times and "고요" 16. Her 어머나/후후 tics never fire, and she speaks 해요체 where the spec wants -습니다. |
| P15 | 남가온 | 7 | 5.5 | Excellent 심판 voice. "확인" ×78; the 이현 exposé never surfaces; her step-counting collides with 민서's. |
| P16 | 신채령 | 7 | 6 | Best bond scenes in the cast. Daily barks drift into tsundere cliché, and she borrows "헐". |
| P17 | 송예담 | 6 | **4.5** | Reads as a generic streamer. The spec's "판정문 어투" (referee's verdict voice) is never used; she names nobody and gamifies death with no pushback. |
| P18 | 임민서 | 8 | 6 | The most consistent voice, but minimal. ANY fallbacks break his -습니다 register, and he names nobody. |
| NPC00 | 유스티 | 6 | **5** | Frequent announcements sound like a hotel concierge. Chapter-rule variants hint at solutions, and the loop leaks. |

**Weakest in play:** 정세나 (4), 한서윤 (4.5), 송예담 (4.5), 윤해린 (5), 유스티 (5). 민혁 (4.5) is the player and is handled separately (§4 P01).

### 1.2 Why the cast feels flatter in play than on paper

These are systemic causes, in order of impact.

**1. Repetition.**
- `small_talk` is **1,000 of 3,667 barks** (27%) in the dump.
- Every resident has exactly **6** small_talk lines. 재하 shows 22 only because his name-prefixed variants count as separate strings.
- Each line is therefore heard 10–15 times in three days. 준서's "사람은 배가 부르면 싸움이 줄어요" appears ×15.
- `busy` and `doing_act` have only 2 variants each.

**2. Name-swapped templates in the most frequent daily systems.** Zero per-character overrides exist for:
- `IntentLines`: every resident says "차나 한잔해야겠다".
- `Requests`: one invite/find/deliver sentence for all. It also produces the bug "내 진우의 두꺼운 책 못 봤어?".
- Routines keys: `comfort`, `offer_tea`, `applause` ("브라보", which is 재하's tic, now said by everyone), `confront`, `stroll_*`.
- Grammar keys: `borrow_*`, `return_*`, `gathering_*`, `courier_*`, `repair_claim`, `heard_voice`, `saw_handover`.
- ANY keys: `hurt_react`, `startle`. 민서 even says "깜짝이야. 조심 좀 해 주세요" in 해요체.

The planning doc forbids exactly this: "이름만 바꾸는 템플릿으로 인물 차이를 대체하지 않는다" (§15 1).

**3. Conversations do not connect.**
- NPC–NPC talk picks opener and reply keys independently, so replies are non-sequiturs. `small_talk` is the reply to like, tease, confide, flirt and warn.
- `talk_like` fills `{topic}` from the *speaker's own* likes, but many variants are written as reactions ("{topic}? 오, 그건 나도…").
- Table talk is 3–5 unrelated one-liners.
- Lines that name a resident ("재하는…") can be said *to* that resident.

**4. The relationship web is 8 directed pairs.** In daily barks almost nobody names anybody. The doc's pairs (서윤–이현, 가온–이현, 민서–태겸, 준서–채령, 라온–재하) surface only in rare secret or stage-4 content.

**5. The same verbs for every NPC.**
- The conversation menu is identical for everyone.
- Gifts are keyword matches against `Likes`.
- Spending time together ends with one `small_talk` line.

**6. Shared phrasing across files.** Examples:
- final_defense "choosing me is the easy choice": 진우, 도윤, 이현.
- warn "단둘이 있지 마": 진우, 서윤, 시온, 은결, 채령, 예담, 민서.
- verdict_correct "기쁘지 않아": about ten residents.
- casual_yes "좋아요. …아니, 좋아."
- talk_like "{topic} 좋아해? 난 좋아해."
- panic "왜 다 나를 봐?"
- The whole investigation and 심판 skeleton (alibi, saw, heard, claim) differs only in the sentence ending.

**7. Tic collisions.**

| Tic / habit | Who shares it |
|---|---|
| 잠깐 | 서윤, 해린, 가온 |
| 그러니까 | 서윤, 해린 |
| 있잖아 / 음~ | 재하, 수아 |
| 야 | 시온, 세나 |
| 헐 | 라온, 채령, 예담 |
| 흐 (laugh) | 가온, 민서 |
| 확인 | 가온, 해린, 세나 |
| Step-counting | 가온, 민서 |
| Tea as identity | 은결, 민서, 유스티, 도윤 |
| 브라보 | 재하, and ANY |

**8. The stage-4 formula.** Twelve bond files share one skeleton: 민혁 knows something he shouldn't → "오래 본 사람처럼" → "모르는 척해 주세요" → the secret. Played in sequence, the magic dies.

**9. Visuals and behaviour.**
- Twelve procedurally built residents sit next to six GLB models (HANDOFF §3.9).
- Idle behaviour (`IdleLife`) is driven by mood, not by character.
- Signature props (candy, pocket watch, hot pack, sticker sheet, thermos) exist in data, but are never *handled* in play.

---

## 2. Voice matrix (unique tics, resolved collisions)

The rule is **one owner per tic**. Anyone else who needs the meaning says it in their own words.

"Tell" is the physical or verbal cue performed on `Utterance.Lie` (see DailyLifeDesign §4.5 cards and §7).

| ID | 인물 | Register | Signature (owner) | Laugh | Pet words | Never says | Lying tell |
|---|---|---|---|---|---|---|---|
| P01 | 민혁 | 부드러운 해요체 → 합의 후 조심스러운 반말 | "혹시…", "괜찮으면" | 하하 | 빵, 같이, 부탁 | Insults, labels like "살인자", more than the evidence shows | — (player) |
| P02 | 진우 | 반말 only; mock 존댓말 = sarcasm | "흐응", "맞혀 볼까?" | 큭 | 반응, 표정, 손, 사탕 | "괜찮아" as comfort; a light "미안" | Crunches his candy before the lie, then answers with a question |
| P03 | 서윤 | 단정한 해요체 → 담백한 반말 | "정리하면," / head count "열여섯, 열일곱…" | 후훗 | 칸, 몫, 당번, 명단, 빈칸 | "도와줘(요)", "대충" | Says "괜찮아요" twice and aligns her pen |
| P04 | 도윤 | 느린 하십시오체+해요체, "~씨" | "그렇군요.", "괜찮으시다면" | 후 | 제자리, 결, 금, 흠, 붙이다, 허락 | 반말 (except privately to 은결), "당신", "죽이다" | Narrows scope ("들어갔을 때는"), then sets the cup back exactly |
| P05 | 이현 | Public 하십시오체 + rhetoric; private short 반말 | "자, 자", "말씀하신 건 ~라는 거죠?" | 하하하 (public only) | 절차, 질서, 공익, 첫 문장, 정정 | "제 잘못입니다"; the word "돈" in public | Summarises you first, then touches his jacket button |
| P06 | 태겸 | 사무적 하십시오체; conflict → short 반말 | "조건은?", "기한은?", "됐습니다." | 흠 | 대여표, 반납, 미수금, 셈, 포장 | "그냥" — except when truly sincere (his crack) | Glances at his pocket watch; "개인 거래는 없었습니다" |
| P07 | 시온 | Loud 반말; whispers in the library | Nicknames for everyone ("셰프", "회장님", "사장님"…), "레알?" | 크하하 | 의리, 무대, 박자, 단어 | "밀고" said casually; admitting fear | Talks faster and louder, pulls the sunglasses down |
| P08 | 라온 | Dry 반말; curses at pretension | "아 뭐,", "됐고," | 크 | 박자, 템포, 반음, 엇박, 핫팩 | Group chants, "대박", "농담이야" as an apology | "이어폰 끼고 있었어" while touching the earphone that is out |
| P09 | 재하 | Soft 반말, listener's name first | "{이름}아,", "있잖아", "세상에" | 아하하 | 객석, 막, 대사, 기다림 (≤1 stage word per 4 lines) | "내가 할게" (he says "오 분만"); anything about filming | **Drops the name-first**; delivery turns too smooth; hides his hands |
| P10 | 준서 | 해요체 asking consent → 느긋한 반말 | "우선 앉아요", "간 좀 봐 줄래요?" | 허허 | 끼니, 숟가락, 몫, 간, 누룽지 | "싫으면 말고"; wasting food | Minimises ("잠깐 나갔을 뿐이야") and offers food |
| P11 | 해린 | Fast short 반말; swears when scared | "오!", "이거 봐", "됐다!" | 헤헤 | 규격, 공차, 헐겁다, 태엽, 기름칠 | "완벽해", "대충" | **"문제없어. 다 봤어."**, then pulls her goggles down |
| P12 | 수아 | Bright 해요체 → soft 반말; sincere = "나는", lower | "꺄", "대박", "오늘도 반짝!" | 에헤헤 | 카메라, 조명, 반짝, 스티커 | A flat "싫어요"; badmouthing anyone to their face | Brightest smile; eyes flick to the room corner (a camera) |
| P13 | 세나 | Blunt 반말; curses (존나) | "솔직히", "한 판 더", "GG" | 하 | 판, 리플레이, 각, 판정, 반응 속도 | "도와줘", "불쌍", "확인" | "괜찮다고" with the prosthetic hidden in her pocket |
| P14 | 은결 | Low, even -습니다; long waits | "그런가요.", "어머나" (rare), deadpan pun (≤1 per scene) | 후후 | 매듭, 접다, 제자리, 백합 (≤1 "조용/고요" per scene) | "힘내세요", "불쌍해라" | Her pause after a question gets *shorter* |
| P15 | 가온 | Short 해요체; verification questions; silences | **"잠깐만요."**, "직접 보셨어요, 들으셨어요?" | 흐 | 출처, 원문, 정정, 제목, 날씨 (≤1 "확인" per 3 lines) | "확실해요" (unless triple-checked); a source's name | **Closes her notebook** (she never closes it otherwise) |
| P16 | 채령 | Languid 반말, short parallel sentences | "별로.", "풋.", "뭐, 어쨌든." | 풋 | 핏, 선, 단추, 날짜, 원단 | "고마워" said straight (she says "적어 둘게"), "헐", shouting | "기억 안 나" while squeezing 꽥 사장 in her pocket |
| P17 | 예담 | Sing-song 반말 with big pitch swings; doubled words | "짜잔!", "있지있지", **"판정합니다."** | 히히 (rare) | 문제, 초대장, 규칙, 벌칙, 모형 | Her own name as "I"; "구독/좋아요" | Her rhythm stops mid-sentence and she switches to the referee voice |
| P18 | 민서 | Short 하십시오체 → short 반말 | "끝났습니다.", numbers (걸음, 분) | 허 | 몫, 공평, 걸음, 보리차, 사 년 | "힘들어요" | Checks his digital watch; "힘들지 않습니다" |
| NPC00 | 유스티 | 하십시오체 only, "~ 님" | "알려 드립니다.", "정해진 대로" | — | 절차, 기록, 자리, 물결 | Jokes, opinions on guilt, hints, 반말 | Never lies; when a truth is heavy **his goldfish stop swimming** |

**Collision fixes**
- 잠깐만요 → 가온 only.
- 그러니까 → 서윤 only; 해린 loses it.
- 있잖아 → 재하 only; 수아 uses "에이~".
- 야 → both keep it: it is plain Korean. But 세나's is clipped ("야.") and 시온's is shouted ("야!").
- 헐 → 라온 only.
- 흐 → 가온; 민서 → 허.
- Step-counting → 민서 only; 가온 uses *his* counts.
- 확인 → rationed for 가온 and 해린, removed from 세나 ("보지도 않고").
- Tea:
  - 도윤: unscented tea.
  - 은결: warm tea, served to others.
  - 민서: barley tea from a thermos, never "차".
  - 유스티 serves; he never drinks.
- 브라보 → 재하 only; ANY `applause` loses it.

---

## 3. The relationship web

### 3.1 Premise — "매듭"

The eighteen gods did not pick eighteen strangers. The mansion gathered people **whose threads already cross**, and most of them do not know it yet.

This is the web's organising idea. It is also a loop-spanning meta-mystery (Zero Escape / Umineko craft, no copied device). The red-thread board of the 심판 is the visual echo.

Ties come in three levels:
- **A — public.** Both know, and it shows in daily life.
- **B — one-sided.** One knows; the other doesn't.
- **C — hidden knot.** Neither knows at loop start. It surfaces through bond stage 3–4, CH06 "과거의 봉투", or cross-testimony, and every knot is a ready murder motive (`knows-my-secret`).

### 3.2 Ties

`[doc]` = required by SOURCE_V11 P0745 or a §12/§15 example. `[new]` = invented here.

| # | Pair | Type | Lvl | History | Tension now | How it shifts |
|---|---|---|---|---|---|---|
| 1 | 민혁 ↔ 진우 | test / attachment [doc] | A | Met here. 진우 sets out to test him. | Candy tests; "넌 예측이 안 돼". | Trust → "이번엔 무서워서 보는 거야"; betrayal → cold contempt. |
| 2 | 서윤 → 이현 | grudge [doc] | B | His signed invoices sank her family's 30-year print shop. **He does not recognise her.** | She writes his name most neatly; her pen stops when he greets her. | Her bond 4 names him. If she learns he forgot her: fury, and motive. Her growth: she chooses verification over revenge. |
| 3 | 이현 ↔ 태겸 | deal / illegal funds [doc] | A (private) | 태겸 ran 이현's slush money to make payroll. | 태겸 checks his pocket watch whenever 이현 speaks. | CH05/06 exposure → rupture; each becomes the other's motive (the ledger). |
| 4 | 도윤 ↔ 은결 | siblings; crime cover; control [doc] | A (hidden from others) | She cleaned up after him in every city ("상자는 늘 셋"). | She wants him where she can see him; he wants her to stop. | Player closeness to either makes the other jealous; exposure breaks her neutrality. |
| 5 | 가온 ↔ 이현 | exposé [doc] | A | Her triple-checked article on his funds. | He avoids her; she asks him to correct his statements "원문 그대로". | Public clashes at meals; each is the other's best murder motive. |
| 6 | 준서 → 채령 | protection vs boundary [doc] | A | He fed her through the worst month of her shop. | He follows her; she refuses. | Respected refusal → she accepts one lunch box; ignored → grudge. |
| 7 | 세나 ↔ 해린 | hope vs limits; **shared secret** [doc] | A | 세나 told only 해린 the truth about her hand (her team was told "ligament"). | "고칠 수 있냐고 자꾸 물어." | 해린 saying "못 고쳐" honestly deepens the tie; a false "문제없어" breaks it. |
| 8 | 라온 ↔ 재하 | rivals: sound memory vs reproduction [doc] | A | 재하 repeats voices word for word; 라온 hears that the tempo is always wrong. | Needling about tempo. | **Knot K2** (below) turns rivalry into a real clash over the USB. |
| 9 | 은결 → 진우 | old acquaintance (funeral) [new] | B | Her funeral home held his friend's funeral. She remembers the boy who didn't cry and sat eating candy, watching the mourners. He doesn't remember her. | Her deadpan "그날도 사탕을 드시고 계셨죠." freezes him. | Reveal → the only person in the house who saw him that day; he fears her, then confides. |
| 10 | 태겸 ↔ 민서 | **debt** [new] | B | 민서 worked a month of night shifts in 태겸's warehouse. Eleven hours of overtime were never paid. 민서 never complained. | 민서 pretends not to know him. 태겸 knows, and it itches. | Paying the 11 hours is 태겸's growth; exposure in public shames him; 민서's cut-off comes if never acknowledged. |
| 11 | 채령 → 수아 | blame / feud [new] | A | 수아's group's hiatus cancelled 채령's biggest styling contract. It was one of 채령's three excuses; the real cause was her own over-ordering. | Cold sniping; 수아 is over-sweet out of guilt. | 채령 admitting "내 탓이야" ends it; blaming escalates at meals. |
| 12 | 시온 → 수아 | **unrequited feeling** [new] | A | 수아 is kind to everyone; 시온 reads it as meant for him. She never says no. | He saves her seat; she thanks everyone equally. | Her one clear, kind "아니야" is her growth and his heartbreak. Jealousy toward whoever she sits with. |
| 13 | 예담 → 수아 | fan / consent [new] | B | 예담 once ran a fan-edit account cutting 수아's clips without consent. | 예담 films; 수아 flinches. | 수아 recognising the handle → hurt; 예담's apology is her own growth. |
| 14 | 진우 ↔ 서윤 | **feud** [new] | A | Met here. He hates "뼛속 깊은 선인"; she refuses to be read. | He calls her roster "착한 척 표"; she keeps him off it. | Mediation → grudging respect ("너 같은 사람이 제일 안 무너져"); escalation → she excludes him from the buddy system after a death. |
| 15 | 도윤 ↔ 해린 | workshop rivals [new] | A | They share the 공방. She fixes to atone; he restores to control. | She notices his tools are always *too* clean. | She becomes the first to suspect him; or his quiet protector if the player vouches for him. |
| 16 | 도윤 → 세나 | fascination [new] | A | He admires her prosthetic's lines. She first likes that he doesn't pity her. | She mistakes objectification for respect. | Her realisation ("넌 손만 보잖아") → disgust; a red flag the player can notice. |
| 17 | 세나 → 민서 | wrongful accusation [doc example] | A (day 1) | On day 1 she blames him for the missing storage key. 태겸's rental slip shows 시온 had it. | Awkward; he says nothing. | Her apology "민서야, 내가 틀렸어…" is her growth; a repeat accusation → his cut-off. |
| 18 | 준서 ↔ 진우 | the "deep good" he despises [new] | A | — | 준서 feeds him porridge instead of candy and cannot be provoked. | 진우 naps in the kitchen in the afternoons; he'd never admit it. |
| 19 | 준서 ↔ 민서 | the two giants [new] | A | — | Kitchen and carrying partners; barley tea vs feeding. | Warm and stable; grief anchor if either dies. |
| 20 | 서윤 ↔ 예담 | odd couple [new] | A | — | 예담 turns the chore roster into a game; 서윤 pretends to hate it. | Allies; 예담 is the one who makes 서윤 rest. |
| 21 | 서윤 ↔ 태겸 | organizing allies → betrayal [new] | A→B | 서윤 tidies, 태겸 counts: the joint work in doc 05 2. | Mutual respect. | When 서윤 learns he ran 이현's money: "그 장부에 우리 가게 이름도 있었어요?" |
| 22 | 가온 ↔ 민서 | map partners [doc LG01] | A | He paces, she sources. | Quiet warmth. His admiration is never said. | A stable partnership; she's the one he tells about the calculus book. |
| 23 | 이현 → 채령 | **debt** [new] | A | She styled his campaign and chose the teal tie. Her invoices were never paid after the scandal. | She adds a "second excuse" for her shop. | He offers "a deal" instead of payment → she despises him. |
| 24 | 수아 → 이현 | suspicion [doc: unconfirmed] | B | A phone call "from above" froze her career. She freezes when a gray suit approaches. | He is kind to her, which is worse. | **Never confirmed as him.** 태겸's second ledger may name the real caller. |
| 25 | 해린 ↔ 태겸 | tool reservations [doc D12] | A | — | "지금 쓰잖아. 손부터 빼." / "예약 시간은 지났습니다." | Knot K4. |
| 26 | 해린 ↔ 라온 | quiet warmth [new] | A | She fixes his earphone cable; he leaves her hot packs. | None. | Grief anchor. |
| 27 | 라온 ↔ 시온 | noise and banter [doc D02] | A | The library shout. | Music-guy ribbing. | Knot K2. |
| 28 | 재하 ↔ 예담 | filming [new] | A | He hates being filmed; she is secretly a fan of his debut play. | "끄자. 지금 이 얼굴은 무대 밖이야." | She deletes a clip in front of him → trust. |
| 29 | 재하 ↔ 세나 | tempo of turns [doc example] | A | — | He stops her from cutting people off; she calls him slow. | Mutual correction: each makes the other a better juror. |
| 30 | 재하 ↔ 수아 | fellow performers [new] | A | A web drama three years ago. | The only two who can spot each other's fake smiles. | Honest mirror scenes; they share the secret-filming dislike. |
| 31 | 은결 ↔ 예담 | "death is not a game" [new] | A (event) | — | 예담 turns a death into a quiz and 은결 stops her. This fixes the audit gap where nobody reacted. | 예담's growth: she folds a lily with 은결. |
| 32 | 은결 ↔ 준서 | caretakers of the dead and the living [new] | A | — | Memorial meals: he cooks, she arranges. | Their joint memorial is the aftermath event. |
| 33 | 시온 ↔ 준서 | party host and cook [new] | A | — | 준서 calls him "시온" from day one, never "rapper". | Grief anchor. |
| 34 | 시온 → 민서 | "형님" [new] | A | — | 민서 is the only one who hears a whole verse without laughing. | — |
| 35 | 가온 → 태겸 | holding a story [new; fits 태겸 B4] | B | Her article touched the ledger. She knows 태겸 kept it and hasn't published — not yet triple-checked. This is the "아는 척 안 하는 사람" in his B4. | He watches her notebook. | She publishes (CH06-like) → his motive; or she asks consent → his trust. |
| 36 | 세나 → 민혁 | suspicion of the outsider [new] | A | — | "계약 없는 놈이 제일 수상하지." (the doc warns 민혁 may be suspected of privilege). | Evidence-based defence wins her; being wrong about him feeds her apology arc. |

### 3.3 Hidden knots (level C)

These are designed for loop 2+. They are optional but strongly recommended: they multiply motives without adding systems.

| Knot | Who | What neither knows at start | Reveal path |
|---|---|---|---|
| **K1 — 작년 겨울** | 준서, 세나 | The illness that cost 준서 his taste began the night he drove a catering van with a high fever, *because people were waiting for food*. The van hit a taxi, and a young woman lost her right hand. That was 세나. He only knows "손을 잃은 젊은 여자분". She only knows "배달 승합차". | 준서 B4 ("그날 밤 배달") and 세나 B4 ("승합차") heard across loops; or CH06 envelope to 세나. His protective flaw now has a root. Her quick judgement meets the gentlest man in the house. |
| **K2 — USB** | 라온, 시온, 재하 | 라온's band leader "형" — the one who wrote the unreleased song and "couldn't keep the beat" — is 시온's 큰형, jailed after 시온 informed. 라온 doesn't know where 형 is. 재하's departed lover (gender and name never stated) sang the guide vocal on that demo, so 라온's secret USB holds the lover's voice. | 재하 hums the never-performed song → 라온: "그 곡 어디서 들었어?" 시온's notebook line "옥상 관객 다섯 — 지금은 전부 벽 안에". One object, three wants. |
| **K3 — 한 사장님** | 가온, 서윤 | 가온's accurate 이현 article listed the print shop as an invoicing vendor without checking whether the shop knew. The shop collapsed. "한 사장님" keeps recurring in her untitled diary (fits her B3: "맞는 기사도 누군가를 무너뜨려요"). He is 서윤's father. | 가온 B4 plus 서윤 B2 ("잉크 냄새"). This deepens the doc's 가온–이현 theme of accuracy vs the method of disclosure. |
| **K4 — 센서 로트** | 해린, 태겸 | The lift sensor 해린 knowingly passed came from a parallel-import lot that went through 태겸's warehouse. | 해린 is tempted to blame the part (her flaw); truth: she knew. 태겸: "그 계산은 안 끝납니다." |
| **K5 — 장례식** | 은결, 진우 | (Level B, above.) Only she remembers. | Her line; his B4 echoes. |

### 3.4 Seeding values for `Relations.InitLoop`

These are the initial directed values to add after the existing eight pairs. Tags use the existing `Rel.Tags` / `Memory` / `Knowledge.Facts` schema.

| From → To | Values | Tags | Memory (Korean) | Facts |
|---|---|---|---|---|
| P14→P02 | Respect .10 | oldacq | 장례식장에서 울지 않던 학생. 사탕을 먹고 있었다 | oldacq:P02:funeral |
| P06→P18 | Respect .20 | owes | 야간 조에서 불평 한 번 안 하던 사람 | debt:P18:overtime |
| P18→P06 | Grudge .15, Trust −.10 | debt | 초과 근무 열한 시간. 받지 못했다 | knows:P06:employer |
| P16→P12 | Grudge .25, Like −.05 | blame | 저 그룹 활동 중지 때문에 우리 가게 계약이 날아갔다 | — |
| P12→P16 | Like .15, Fear .05 | guilt | 우리 때문에 문 닫은 가게가 있다고 들었다 | — |
| P07→P12 | Like .30, Romance .35 | crush | — | — |
| P17→P12 | Like .30 | fan | 편집하던 직캠 속 그 사람 | secret:P17:fanedit |
| P02→P03 / P03→P02 | Like −.05 / −.10, Trust −.10 | feud | 착한 척하는 표 / 사람을 떠보는 사람 | — |
| P04↔P11 | Respect .20 / .15 (+Fear .05 for P11) | workshop | 같은 작업대, 다른 손 | — |
| P04→P13 / P13→P04 | Like .10 / .15 | fascination / — | 의수의 선이 곱다 / 날 불쌍하게 안 보는 사람 | — |
| P10→P02 / P02→P10 | Like .10 / Like −.05, Respect .10 | feed | — | — |
| P10↔P18, P07↔P10, P03↔P17, P15↔P18, P14↔P10, P11↔P08, P09↔P12 | Like .15–.25 | colleague / party / oddcouple / warmth / stage | — | — |
| P03↔P06 | Trust .15, Respect .15 | ally | — | — |
| P15→P06 | — | holding | — | secret:P06:불법자금 |
| P16→P05 / P05→P16 | Grudge .20 / — | debt / owes | 선거 캠프 스타일링 대금, 아직 못 받았다 / 그 넥타이는 그 사람이 골랐다 | — |
| P12→P05 | Fear .10 | suspect | 회색 양복 | — (never a fact about 이현) |
| P11↔P06 | Grudge .05 | tools | — | — |
| P09→P17 / P17→P09 | Like −.05 / .25 | — / fan | — | — |
| P07→P18 | Respect .20 | — | — | — |
| P11→P13 | — | — | — | secret:P13:hand (shared) |
| P13→P01 | Trust −.10 | outsider | — | — |
| P03→P01 | Like .05 | — | — | — |

Knots K1–K4 start as **no facts at all**. Bonds and rules add them: e.g. `knot:K1` added to both parties' Facts on reveal. Only then does `Relations.Pressure` see them.

---

## 4. Characters

Every entry follows the same order:
1. Hook
2. Canon and audit
3. Personality and contradictions
4. Speech signature, with five lines
5. Visual and gesture signature
6. Secret, fear, desire, contract
7. Daily life
8. Murder and 심판
9. Case roles
10. Key relationships
11. Loop arc and bond stages

**Case-role stars** are *weights* for the murder system (1–5). They are not casting. D-038 still applies: no one is a fixed culprit.

**Bond stages.** The game has four stages; the task asks for three in-loop stages. Here I = surface quirk, II = the crack, III = the wish/contract, and IV = the loop-gated secret. Each IV uses a **different loop-echo device**, which fixes the §1.2-8 formula.

---

### P01 김민혁 — player

**Hook.** 계약 없이 들어온 단 한 사람. 모두의 부탁을 들어주다가, 끝에는 모두를 대신해 "당신이 했다"고 말해야 하는 사람.

**Canon.** 20, 175 cm, 진학 준비생. GLB model. Warm brown and cream; nods before agreeing. Can't refuse requests, so decisions come late. Likes 빵, co-op games, walks; dislikes shouting and being rushed. The only resident without a contract, which the others may read as privilege.

**Audit (5 / 4.5).**
- Pleasant but generic: `p_greet` and `p_agree` are word for word the ANY lines.
- No bread or co-op texture, and no resolved voice.
- Bond stage 1 with 채령 and 예담 slips into 반말 before any agreement.
- His foreknowledge lines ("그 책, 3단원부터 보세요") correctly mark him as the one who remembers loops. Keep that consistent.

**Personality and contradictions.**
- The kindest person in the house, and the one who ends up pronouncing guilt.
- His "yes" is not generosity alone; it is fear of choosing.
- Because the *player* remembers loops, his kindness becomes uncanny to others: he knows the flavour you hate before you say it.

**Speech.** Soft 해요체 ("혹시", "괜찮으면"); careful 반말 once agreed ("이거 맞지?").
- When resolved, the hedges vanish and the sentences go short.
- He never swears and never states more than the evidence shows.
- Growth marker: he learns to say "그건 못 해요".

| # | Line |
|---|---|
| 1 | 혹시 이 빵 하나 남겨 둬도 돼요? 아침 못 먹은 사람이 있어서요. |
| 2 | 괜찮으면 같이 가요. 혼자 가기엔 복도가 너무 길잖아요. |
| 3 | 제가 본 건 거기까지예요. 그다음은 아직 아무도 몰라요. |
| 4 | 아까 부탁한 거 가져왔어. 이거 맞지? 틀리면 다시 찾아볼게. |
| 5 | 다들 멈춰요. 한 사람 버리는 걸로 끝내지 마요. |

**Visual and gesture.** First person only. His body shows in dialogue and the 심판. Crossbag. In dialogue he nods before agreeing and checks his bag for things he owes back (source idle).

**Secret, fear, desire.**
- Secret: he doesn't know why he has no contract; he keeps his loop memory.
- Fear: that his kindness is only an inability to choose, and that someone dies because he said yes to the wrong person.
- Desire: to decide and own it.
- Contract: none ("기록상 계약이 없습니다").

**Daily life.** Player-driven. The design deliberately creates **one double-booking on day 2**: two invitations at the same hour, so his flaw becomes a choice.

**Murder and 심판.** His accusation lines fire only on a complete chain. His break line is line 5 above.

**Relationships.**
- 진우 (test).
- 세나 (she suspects the contractless outsider).
- 서윤 (she gives him his first chore, his first "place").
- 유스티 (the only name Yusti says with an extra pause).

**Arc.**
- Loop 1: yes to everything → learns "no".
- Loop 2: foreknowledge unsettles others.
- Loop 3+: chooses whom to save.

---

### P02 김진우

**Hook.** 사람의 반응을 수집하는 163cm 심리학도. 사탕을 내밀며 당신을 시험하지만, 그가 제일 두려워하는 건 자기 대답이다.

**Canon.** 20, 163 cm, psychology student. GLB model, with dark-face states `HollowGrin`, `CorneredStare` and `VeiledSmirk` (HANDOFF 37). Deceit 97.
- Contract: bring back a dead friend.
- Secret: his reply to the friend's last message.

**Audit (7.5 / 6.5).**
- Voice and objects are strong.
- The sincerity marker ("시험 / 떠보는 거 / 실험 아니야") appears 8+ times. **Ration it to bond III and `confess` only**; elsewhere the cue is physical (the smile drops, his head straightens).
- His investigation lines are generic. He names nobody.
- The counting-heads habit moves to 서윤.
- Hated flavour: B1 says green, B4 says lemon. **Canon: 초록.**

**Personality and contradictions.**
- Reads everyone and trusts no one.
- Despises the deeply good, yet only relaxes near them (준서, 민혁).
- Lies for sport, but the one question he cannot answer honestly is about his friend.
- "이해했어" is his curse: he understood, and only understood.
- Stops arguments before he wins ("더 하면 내가 이기는데, 그건 재미없어").

**Speech.** 반말 always; 존댓말 only as mockery. Short questions and comebacks, name-only address, "흐응", "큭", "맞혀 볼까?". He never offers stock comfort ("괜찮아", "힘내").

| # | Line |
|---|---|
| 1 | 흐응, 대답하기 전에 침 한 번 삼켰지? 뭘 고르다 만 거야? |
| 2 | 빨강, 초록, 투명. 골라 봐. …역시 투명은 안 고르네. 맛이 안 보이면 못 믿거든. |
| 3 | 서윤이 표 봤어? 칸이 너무 반듯해. 저런 사람은 한 번 금 가면 끝까지 가. |
| 4 | {you:아}, 방금 건 안 웃겼어. 진짜로. |
| 5 | 다들 나만 보네. …좋아. 이제야 좀 재밌어졌다. *(cornered → HollowGrin)* |

**Visual and gesture.**
- Tilts his head to listen and straightens it when serious.
- Unwraps candy one-handed and crunches it right before he lies.
- Twists the wrappers into tiny ropes and leaves them where he's been — an innocent trail that can mislead a case.
- Hangouts: morning Dining (who sits by whom), afternoon Library (psychology and politics shelf), evening Lounge. At 11:40 p.m. he looks at a clock, wherever he is.

**Secret, fear, desire.**
- Secret: the night before his friend died, the friend asked "나 없어지면 아무도 모르겠지?" and 진우 answered "그런 말은 관심받고 싶을 때 하는 거야". The friend is never named ("걔").
- Fear: being read the way he reads others; the 11:40 silence.
- Desire: an answer he can't analyse; to say *anything else*.
- Contract: resurrection.

**Daily life.**
- Watches seating at breakfast (doc P0772), then reads in the library.
- Runs candy and "취향 맞히기" tests. Where 예담 wants everyone included, he wants reactions: a natural friction.
- Plays silent chess with 도윤 and 태겸.

**Murder and 심판.**
- At a death: silence, then analysis of the victim's last expression. He investigates people, not objects.
- In the 심판: attacks leaps of logic ("내가 거짓말한 거랑 네 추리가 맞는 건 별개지"), withholds a counterexample to watch reactions, then plays it late.
- Tell: crunches, then answers with a question.
- Breaking point: someone repeats "나 없어지면 아무도 모르겠지?", or says "넌 아무도 안 좋아하잖아". The laughter goes out and he straightens.

**Case roles.**
- Red herring ★★★★★: withholds evidence for fun; wrappers near scenes.
- Culprit ★★★: psychological staging, i.e. courier notes, fake messages, shifting a gathering's time; he chooses a victim "nobody will miss".
- Witness ★★: sees everything, tells selectively.
- Victim ★★: his tests provoke 세나 and 시온, or a culprit fears being read.
- Accomplice ★: he follows no one.

**Relationships.** 민혁 (#1), 서윤 (#14 feud), 도윤 (unreadable rival: silent chess), 은결 (#9 funeral), 준서 (#18).

**Arc.** Growth: he asks one honest question and doesn't watch the reaction. Fall: he stages a "test" that gets someone killed, or tries to make a death "mean something" to justify his wish.

**Bond stages** (existing, keep):
- I 「투명 사탕」
- II 「11시 40분」
- III 「과거형」 (contract)
- IV 「마지막 답장」. Echo device: the player removes the *green* candy without being told. Fix the B4 flavour to 초록.

---

### P03 한서윤 — weak in play: see fix list

**Hook.** 매일 아침 열여덟 명을 세는 학생회장. 자기 이름은 언제나 명단 맨 끝에 쓴다.

**Canon.** 20, 170 cm, university student-council president. Procedural model: navy blazer, one red armband.
- Contract: money for the family.
- Secret: 이현's signature sank the print shop.
- Bond lore: hamster 「회의록」, receipts (her sibling's academy fees, her father's medicine, the shop's electricity), 설거지 당번표, only one decent ballpoint, her own name last.

**Audit (5.5 / 4.5).** On spec but bland.
- Overlaps 태겸 in vocabulary (기록, 약속 시간, 공평, 정리하면 머리도).
- A cat-vs-hamster contradiction.
- `comfort` falls back to ANY.
- Her grudge never shows in daily play.
- "우리 집 가게" is a register slip (should be 저희), and "학생회장이었어요" has the wrong tense.

**Fix.**
1. Give her the morning head count (from 진우) and "정리하면,".
2. Give her the visible **pen-stop** when 이현 is present: a context line and a gesture.
3. Give her the 진우 feud and the 예담 friendship.
4. Write her own `comfort` as concrete help ("물 한 컵, 담요 하나. 그다음에 얘기해요.").
5. De-duplicate her from 태겸: she talks about *people's shares*, he talks about *quantities*.

**Personality and contradictions.**
- The most reliable witness, holding the strongest hidden grudge.
- Referees everyone's fights while fighting a private war.
- Can't accept help (it feels like losing), yet every roster she makes is help for others.
- Gets *quieter and more precise* when angry. Doodles hamsters when stuck.
- Lies only about her own exhaustion.

**Speech.** Neat 해요체, then plain 반말 once agreed. "정리하면," followed by concrete assignments; counting; "후훗".
- Anger names objects: "제가 장소를 바꿨어요. 그 뒤는 못 봤어요."
- She never asks "도와줘요". Her growth line is "한 칸만 맡아 줄래요?".

| # | Line |
|---|---|
| 1 | 정리하면, 설거지는 저, 공지 확인은 같이. 민혁 씨 칸은 여기예요. |
| 2 | 열여섯, 열일곱… 다 계시네요. 아침마다 세는 버릇이 있어요. 신경 쓰지 마세요. |
| 3 | 빈칸은 제가 채울게요. 제 이름은 원래 맨 끝이에요. |
| 4 | 좋은 아침이에요, 백이현 씨. …네, 명단에 제일 반듯하게 적어 뒀어요. |
| 5 | 그때 한 번만 더 봤으면… 그 칸을 비워 둔 건 저예요. |

**Visual and gesture.**
- Upright; sleeves folded exactly; clipboard and one good ballpoint, clicked once before a hard sentence.
- Aligns pens. Hamsters in the margins.
- Her pen hand **stops mid-stroke when 이현 greets her**.
- Hangouts: 07:00 GrandHall notice board, then Dining (table and roster); afternoon Archive/Library; evening Dining clean-up.

**Secret, fear, desire.**
- Secret: the shop's collapse, and (K3) the name "한 사장님" in 가온's diary.
- Fear: being the one who didn't check; pity.
- Desire: "아무한테도 고맙다고 안 해도 되는 돈".

**Daily life.**
- Counts heads at breakfast, so she is the **first to notice an absence** (a clue source).
- Posts the roster; tidies shared supplies with 태겸; logs the day's schedule on the hall board.
- Skips meals while investigating, and 준서 nags her.

**Murder and 심판.**
- At a death: "누가 없어요?". Secures the scene and assigns tasks.
- In the 심판: sorts confirmed from unconfirmed ("확정된 것부터") and refuses to close gaps early.
- Tell: "괜찮아요" twice, then aligns her pen.
- Breaking point: proof that *her* roster left the victim alone (over-responsibility, distinct from confession: "그 선택을 놓친 건 저예요"), or 이현 saying "공익".

**Case roles.**
- Witness ★★★★ (timelines).
- Victim ★★★ (predictable routine; she investigates 이현; she notices absences).
- Red herring ★★★ (prime suspect if 이현 dies).
- Culprit ★★: only against 이현; *the roster trick*, an alibi built into the chore schedule.

**Relationships.** 이현 (#2), 진우 (#14), 태겸 (#21), 예담 (#20), 가온 (K3).

**Arc.** Growth: she gives away one cell, asks once, and chooses verification over revenge. Fall: overwork → the grudge ripens → a roster-built plan.

**Bond stages** (existing, keep):
- I 「여백의 햄스터」
- II 「잉크 냄새」 (seed the name "한 사장님" in one line)
- III 「영수증 뭉치」
- IV 「맨 끝의 이름」 (echo device: the player writes her name last)

---

### P04 차도윤

**Hook.** 무엇이든 제자리로 되돌리는 복원사. 그가 되돌리지 못하는 건, 그가 끝낸 것들뿐이다.

**Canon.** 20, 184 cm, art-restoration assistant. GLB model. Morality .18, Composure 94.
- Contract: erase every record of his crimes.
- Secret: a serial killer the police were chasing; 은결 is his sibling.
- Lore:
  - Corridor frames lean 1° left, and he straightens them nightly.
  - His mentor's rule: "없던 것을 더하지 말 것".
  - He once painted a face onto a face-erased portrait: "제가 아는 가장 조용한 얼굴".
  - Two boxes and one hour to move.
  - His cup sits at the saucer's left end, to 1 cm.

**Audit (7.5 / 6.5).** Motifs are excellent. But:
- 반말 appears in `panic` and `final_defense` C.
- "당신" ×8 reads as translationese.
- B4 all but confesses. Canon forbids that, so make it **oblique**, via 은결's third box.
- The control conflict with 은결 is barely voiced.
- D-038: he is statistically the first culprit.

**Personality and contradictions.**
- The most courteous resident — he asks before touching anything — and the one who objectifies people most.
- Hates waste, yet has wasted lives.
- He regards killing *for a wish* as vulgar. This makes him the house's permanent suspect who is often **innocent**.
- His kindness is restoration: he mends your cup whether you asked or not.

**Speech.** Slow 하십시오체 mixed with 해요체, name + 씨; suggestions rather than assertions. "그렇군요", "괜찮으시다면".
- 반말 only privately, to 은결.
- Never "당신", never "죽이다". Breaking = shorter sentences and polite imperatives.

| # | Line |
|---|---|
| 1 | 그 잔, 이가 빠졌군요. 버리지 마십시오. 괜찮으시다면 제가 붙여 두겠습니다. |
| 2 | 복도 액자가 또 왼쪽으로 1도쯤 기울었습니다. 이 집은 고집이 세군요. |
| 3 | 허락해 주시면 만지겠습니다. …물론, 보는 건 허락이 필요 없지요. |
| 4 | 은결아, 그 얘기는 방에서 하자. 여긴 벽이 얇아. |
| 5 | 그만 만지십시오. 내려놓으세요. 지금, 제자리에. |

**Visual and gesture.**
- Steady breathing. His hand pauses mid-air before touching anything.
- Wipes tools with a soft cloth and resets his cup precisely.
- Night: walks the corridor straightening frames. This makes him a **night witness**, and a night suspect.
- Hangouts: morning Gallery (복원 갤러리), afternoon 공방 (shared with 해린), evening 다과실 (unscented tea), 23:00 corridor.

**Secret, fear, desire.**
- Secret: the killings; 은결's clean-ups; the painted face (a victim's, never said).
- Fear: being corrected or "restored" by someone; the files existing ("서류철 몇 권"); losing 은결's cover.
- Desire: a clean record; a world that stays where he put it.

**Daily life.**
- Restoration goal; tea; mends objects.
- Accepts a refusal outwardly and quietly re-plans access (doc P0818).
- Intervenes only when something is thrown away.

**Murder and 심판.**
- At a death: calm; notes what is out of place, the scene order and the observation gaps (doc).
- In the 심판: splits conditions ("만진 건 맞습니다. 쓴 시점까지 같지는 않지요") and shields 은결 with logic.
- Tell: describes less than he saw, then resets the cup.
- Breaking point: someone defaces an object he restored, in front of him; or 은결 is accused with real evidence.

**Case roles.**
- Red herring ★★★★★: once rumours or CH06 leak his past, every death looks like him.
- Culprit ★★★ (keep him first culprit in ≤1 of 4 loops): staged natural deaths, KeySlide. **His scene is too tidy**, and the tidiness is the clue.
- Victim ★★: someone learns and strikes first, or 은결's control turns.
- Witness ★★ (precise but narrowed). Accomplice ★ (only for 은결).

**Relationships.** 은결 (#4), 해린 (#15), 진우 (rival readers), 세나 (#16).

**Arc.** He does not grow morally. His arc is **restraint vs exposure**: a player he trusts (one who returns things exactly on time) keeps his hands still. Threats to 은결 or his record break it.

**Bond stages.**
- I 「1도」: frames and order.
- II 「없던 것을 더하지 말 것」: the mentor, the painted face (hint).
- III 「서류철」: contract. "기록이 사라지면, 그 일도 없었던 게 될까요?"
- IV echo device: the player straightens the frame **before him**. "누가 먼저 고쳤군요. …저보다 먼저." The secret surfaces only as "상자는 늘 셋이었습니다. 하나는 제 것이 아니었지요."

---

### P05 백이현

**Hook.** 당신의 말을 먼저 요약해 주는 청년대변인. 요약이 끝나면, 당신의 말은 이미 그의 말이 되어 있다.

**Canon.** 20, 178 cm, party youth spokesperson. GLB model: slicked-back hair, light gray suit, teal tie, **gold tooth**.
- Contract: retrieve every illegal ledger.
- Lore:
  - The tooth: a stair fall on his first campaign, headline "청년 정치, 이 빠지다". He turned it into branding.
  - He floats but cannot dive; even his secretary doesn't know.
  - The pool is "최고의 회의실".
  - He drafts two versions of every first sentence.

**Audit (7 / 6).** The polite voice is excellent; the casual one is generic. He repeats himself within the 심판 and borrows 도윤's words (제자리, 통제, 깨끗하게). His casual `final_defense` copies 시온.
- **Age fix:** "스물둘" → "열아홉, 첫 선거 캠프 자원봉사".
- B3 and B4 both say "말 놓을게".

**Personality and contradictions.**
- A mediator who puts his own losses first and wraps self-interest in public-good language.
- Genuinely good at calming a panicking room, and uses the same skill to herd votes.
- Floats and can't dive: a man who can't go deep.
- Displays his humiliation (the gold tooth) as a badge.
- Sometimes his performed warmth is real, and that unsettles him.

**Speech.** Public: 하십시오체/해요체 rhetoric — "자, 자", "요컨대", "말씀하신 건 ~라는 거죠?", "우리 모두를 위해", with a "하하하".
- Private: short, transactional 반말 with no laugh ("그 얘긴 여기까지").
- Never "제 잘못입니다" (only "유감입니다 / 표현을 철회하겠습니다"). Never "돈" in public ("재원", "자금").

| # | Line |
|---|---|
| 1 | 자, 자. 말씀하신 건 결국 '누가 먼저 치우냐'는 거죠? 좋습니다. 순서만 정하면 됩니다. |
| 2 | 요컨대 지금 필요한 건 폭로가 아니라, 모두를 위한 질서 있는 확인입니다. |
| 3 | 그 얘긴 여기까지. 서로 손해 볼 필요 없잖아. |
| 4 | 물 위에선 아무도 받아 적지 못하거든요. 그래서 수영장이 제 회의실입니다. 하하하. |
| 5 | 첫 문장을… 준비 못 했습니다. 이런 적은 처음입니다. |

**Visual and gesture.**
- Adjusts his jacket front before speaking (source). Rings; the trained smile shows the gold.
- In the pool: on his back, arms spread, eyes shut.
- Hangouts: morning Dining (holds court), afternoon Pool, GrandHall (speech practice), evening Lounge (small deals).

**Secret, fear, desire.**
- Secret: the funds, the signed invoices, the scandal.
- Fear: being *written down* (minutes, ledgers, articles).
- Desire: to write the first sentence of every record.

**Daily life.**
- 지지자 모임 and daily-swim goals; speech drafts.
- Avoids 가온; too warm to 서윤 (he doesn't recognise her); poolside chats with 태겸.

**Murder and 심판.**
- At a death: takes the room ("자, 자, 다들 진정하시고") and proposes procedure.
- In the 심판: summarises others' claims to reframe them and **overreaches**. The doc makes him the natural target of "범위 확대" rebuttals (D07).
- Tell: "말씀하신 건…", then the jacket button.
- Breaking point: someone quotes his own earlier words back verbatim (재하 can), or 서윤 names the print shop.

**Case roles.**
- Victim ★★★★ (everyone had a motive: 서윤, 가온, 채령, 태겸, maybe 수아).
- Culprit ★★★ (silence 가온 or 태겸; poolside drowning or a push staged as an accident; FakeNote).
- Accomplice ★★ (with 태겸). Red herring ★★. Witness ★ (he spins).

**Relationships.** 서윤 (#2), 태겸 (#3), 가온 (#5), 채령 (#23), 수아 (#24).

**Arc.** Growth: one undrafted sentence, an apology to 서윤 without "유감". Fall: kills for the ledger.

**Bond stages.** Keep the existing arc: pool meetings → the gold tooth → ledgers → B4.
- Fix the double "말 놓을게".
- IV echo device: the player finishes his A/B first sentence *before he says it*.

---

### P06 권태겸

**Hook.** 모든 호의를 장부에 적는 유통업 대표. 단 한 줄, '그냥'이라고 적힌 칸만 계산이 안 된다.

**Canon.** 20, 181 cm, small distribution business owner. GLB model.
- **No glasses** (user directive 41). Remove `RoundGlasses` from `Cast.cs`.
- Make him as unlike the reference 오마 코키치 as possible: tall, upright, adult business silhouette in muted greens and browns; no scarf, no checks, no purple; a closed-mouth half-smile, never a grin.
- Lore:
  - Rental slip (item, quantity, return time); the late fee is **one piece of bitter chocolate**.
  - The crooked first logo he drew himself, kept on wrapping paper in his wallet.
  - Three staff, payday on the 25th, and the anxiety of "did I press the transfer?"
  - Triangle-fold wrapping (twice, ends tucked in).
  - Two ledgers; 이현 is the first line of the second.

**Audit (7 / 5.5).**
- Saturated vocabulary; the doc warns against the money-robot drift.
- His relationships appear only in B4. 민서 is absent.
- Duplicates with 서윤. `panic` C1 is identical to `final_defense` C1.
- "식량 사흘 치" is a hard world fact.
- B3's logic is muddled.

**Personality and contradictions.**
- Honours every deal and treats favours as debts, so he can't ask for help.
- The late fee is a joke price: his soft core.
- He laundered a politician's money **to make payroll for three people**.
- "그냥" is the one word he can't book.

**Speech.** Businesslike 하십시오체; in conflict, clipped 반말. "조건은?", "기한은?", "계산해 보면", "됐습니다", "흠".
- Never "그냥", except in his sincerest moment.
- A plain thank-you always carries a counter-offer.
- One curse at breaking (doc).

| # | Line |
|---|---|
| 1 | 빌려드릴 수 있습니다. 반납은 몇 시죠? 늦으시면 쓴 초콜릿 한 조각입니다. |
| 2 | 계산해 보면 설거지는 이틀에 한 번이 공평합니다. 민서 씨가 사흘째 혼자 했습니다. |
| 3 | 호의는 빚입니다. 그래서 전 부탁을 안 합니다. 갚을 날짜를 모르니까요. |
| 4 | 이건 견본 아닙니다. 그냥 드리는 겁니다. …장부엔 안 적겠습니다. |
| 5 | 약속은 지켰잖아. 그것까지 없던 일로 하지 마. |

**Visual and gesture.**
- Checks the list first (source) and counts with his pen.
- Always folding wrapping paper when idle.
- **Glances at his pocket watch whenever 이현 speaks.**
- Hangouts: morning Storage (inventory), afternoon Archive/Dining (rental desk), evening Lounge (exchange).

**Secret, fear, desire.**
- Secret: the second ledger; (K4) the sensor lot.
- Fear: a payday he can't meet.
- Desire: never to owe anyone.
- Contract: a vast sum.

**Daily life.**
- Inventory and the rental desk.
- Exchange market (goal; CH11).
- 15- and 30-minute timeboxes; chocolate instead of coffee.
- Negotiates fair chores with 민서 (doc P0864).

**Murder and 심판.**
- At a death: "수량이 안 맞습니다." He counts what is missing: **the weapon inventory**.
- He produces rental times as evidence and separates possession, ownership and handover (doc).
- Tell: pocket watch, then "개인 거래는 없었습니다".
- Breaking point: the 11 unpaid hours said aloud, or his staff mentioned.

**Case roles.**
- Witness ★★★★ ("who borrowed the rope").
- Accomplice ★★★★ (for a price, or unknowingly lends the weapon).
- Victim ★★★ (holds 이현's ledger).
- Culprit ★★ (meticulous, timeboxed plan).
- Red herring ★★ (his slips show him handling everything).

**Relationships.** 이현 (#3), 민서 (#10), 서윤 (#21), 해린 (#25, K4), 가온 (#35).

**Arc.** Growth: pays 민서 the 11 hours, or gives something "그냥" and doesn't book it. Fall: kills for the ledger or the payroll.

**Bond stages** (existing):
- I "견본 → 그냥 드린 거예요"
- II the first logo
- III contract (fix the logic: "금액을 못 적겠어요. 모자라지 않을 만큼, 그게 얼마인지 몰라서.")
- IV 「두 번째 장부」 (echo: triangle fold — keep; it is a *hands* device, distinct from the others)

---

### P07 유시온

**Hook.** 옥상 무대에서 시작한 래퍼. 모두의 이름을 새로 지어 부르지만, 자기 폰 속 스물세 개의 이름에는 연락하지 못한다.

**Canon.** 20, 172 cm, rapper. Procedural model: orange dreadlocks, sunglasses, fur coat, gold chain.
- Contract: every broken relationship mended.
- Secret: he informed on his crew of five 형들 after a detective promised to keep him out; all five were jailed.
- Lore: word notebook (first page: '의리', '빚', '입조심'); 23 blocked contacts including his mother; 민혁 saved as "브로, 연락 가능".

**Audit (7.5 / 6).** Distinct, but 브로 is garnish (~35 uses). He names nobody. The beer lines contradict each other; **canon: the bar serves beer**. `share_find` "{item} 건으로" is awkward.

**Personality and contradictions.**
- Raps about 의리 while living with the betrayal.
- Loud, and a secret library reader who collects words.
- Bottles hurt up, then explodes.
- Hosts everyone because an empty room sounds like a cell.

**Speech.** Loud 반말 with a nickname for everyone: 셰프 (준서), 회장님 (서윤), 사장님 (태겸), 스타 (수아), 베이스 (라온), 형님 (민서).
- "야!", "레알?", "크하하".
- He rhymes rarely and proudly ("라임 됐다").
- 브로 only to real friends. Whisper mode in the library.
- Sincere = quiet, and real names instead of nicknames. He flinches at "밀고".

| # | Line |
|---|---|
| 1 | 야, 자리 있냐? 혼자 먹는 밥은 비트 없는 랩이야. 크하하! |
| 2 | 쉿… 방금 좋은 문장 만났어. 이건 수첩에 적어야 돼. |
| 3 | 오늘의 단어는 '적막'. 저택, 적막, 저녁. …라임 됐다, 인정? |
| 4 | 준서야. 셰프 말고, 준서. …고맙다고. 그냥 그거. |
| 5 | 또 나만 남겨 놓을 거냐. 대답 좀 해 봐. …제발. |

**Visual and gesture.**
- Big gestures; sunglasses pushed up to read.
- Hunches over books and drums on tables.
- Plays with his chain when anxious.
- Hangouts: late breakfast (the loud entrance), afternoon Library, evening bar and Lounge.

**Secret, fear, desire.**
- Secret: the informing; (K2) his 큰형 is 라온's "형".
- Fear: being left alone; the word "밀고자"; 적막.
- Desire: the rooftop audience back.

**Daily life.** Library reading, party goal, bar nights. He drags the quiet ones along (민서, 라온).

**Murder and 심판.**
- At a death: loud panic, then face-saving bravado ("내가 먼저 확인했지"), though he stood at the back (doc).
- Investigation: touches things and gets told off.
- In the 심판: loyal ("친해서 감싸는 거 맞아. 그래도 못 본 걸 봤다고 하진 않아"); his Composure of 29 makes his testimony wobble.
- Tell: faster and louder; sunglasses down.
- Breaking point: "밀고자" said in public (a CH06 envelope), or his friends voting against him.

**Case roles.**
- Victim ★★★ (goes wherever a "브로" calls; always audible).
- Red herring ★★★ (gang past, outbursts).
- Culprit ★★ (a confrontation turns fatal, then a panicked cover-up; motive: exposure).
- Witness ★★.

**Relationships.** 수아 (#12), 라온 (#27, K2), 준서 (#33), 민서 (#34).

**Arc.** Growth: he tells 라온 the truth and unblocks one name. Fall: explodes after exposure.

**Bond stages.**
- I 「오늘의 단어」
- II 「옥상 관객 다섯」
- III contract: 23 names, "연락 가능"
- IV echo device: the player says today's word *before him*. "…너 내 수첩 봤냐? 아니, 못 봤지. 나도 아직 안 적었는데."

---

### P08 서라온

**Hook.** 모든 소리를 박자로 듣는 베이시스트. 한쪽 이어폰을 빼는 순간은, 당신을 듣기로 한 순간이다.

**Canon.** 20, 180 cm, indie bassist. Procedural model: gray-blue shag and beanie, faded teal hoodie under a black denim jacket, one earphone, bass-string bracelet.
- Contract: ownership of the bandmate's unreleased track.
- Lore:
  - Hot packs, 10 in his pocket every morning; he collects cold ones.
  - The music-room fan runs a semitone flat; the old rehearsal fan was an exact A.
  - The song is 형's. He secretly copied it to USB and wants his name first.
  - "3초 상처" gag.

**Audit (7 / 5.5).**
- "농담 아니야" ×17 replaces actual jokes; the doc's swearing is missing.
- His testimony shares a skeleton with 해린's and 세나's.
- The 재하 rivalry appears once.
- **Contradiction:** B3 "우린 백 번쯤 쳤어" vs B4 "형이랑 나 말고 아무도 몰라". Fix B4 to "내가 USB에 옮긴 건 아무도 몰라."

**Personality and contradictions.**
- Hates pretension and chants; deflects insults with jokes.
- The best listener in the house, who lies "이어폰 끼고 있었어" to stay out of things.
- Wants credit for a song he didn't write, while hating uncredited work.
- Generous with the cheapest possible gift.

**Speech.** Dry 반말; curses at pretension (rarely hard). "아 뭐,", "됐고,", "크".
- Musical nouns.
- Never chants, never "대박"; never "농담이야" as an apology.
- Sincere = the jokes stop and the second earphone comes out too.

| # | Line |
|---|---|
| 1 | 끼는 건 괜찮아. 구호만 외치지 마. 그거 시작하면 나 바로 나간다. |
| 2 | 이 집 선풍기, 반음 낮아. 하루 종일 틀린 음 듣는 기분이야. |
| 3 | 핫팩 남았어. 손 차갑다며. 가져가. 생색은 안 낼게. |
| 4 | 재하야, 네가 따라 하는 건 그 사람 목소리가 아니야. 네가 기억하는 방식이지. |
| 5 | 한 번만 조용히 해 줘. 지금은… 아무 소리도 안 들려. |

**Visual and gesture.**
- Taps rhythms on his thigh.
- The earphone comes out in sync with actual listening (source).
- Squeezes a hot pack; hood up when annoyed.
- Hangouts: morning GameRoom (rhythm game), afternoon corridor bench (acoustics), evening Dining corner and MusicRoom.

**Secret, fear, desire.**
- Secret: the USB copy; (K2) where 형 is, and whose voice is on it.
- Fear: being uncredited; silence where music should be.
- Desire: his name on the song.

**Daily life.** Rhythm-game high score (goal), sessions (goal), dodging 예담's group games. He'll join if it's a *rhythm* game.

**Murder and 심판.**
- At a death: he freezes and listens. The **auditory witness**: he separates heard from seen (doc).
- In the 심판: punctures false certainty ("걔는 소리를 들었다고 했지, 얼굴을 봤댔냐?").
- Tell: "이어폰 끼고 있었어", touching the earphone that is out.
- Breaking point: the USB exposed or destroyed; 재하 imitating *that* voice.

**Case roles.**
- Witness ★★★★★: the one person who can tell a *replayed* voice from a live one, the natural counter to recorder and echo tricks.
- Culprit ★★ (noise masking; motive: the USB). Red herring ★★. Victim ★★.

**Relationships.** 재하 (#8, K2), 시온 (#27, K2), 해린 (#26), 예담 (group-chant friction).

**Arc.** Growth: plays the song for everyone with 형's name first. Fall: kills to keep the USB.

**Bond stages.**
- I 「반음」 (hot packs)
- II 「A음 선풍기」 (the band)
- III contract (the USB)
- IV echo device: the player hums the bass line of the unperformed song. He goes white: "그 곡은 무대에 한 번도 안 올라갔어. 어디서 들었어?"

---

### P09 문재하

**Hook.** 누구의 말이든 토씨 하나 안 틀리고 되살리는 배우. 단 한 사람의 목소리만은, 되살릴수록 멀어진다.

**Canon.** 20, 176 cm, actor. Procedural model: copper waves, emerald silk scarf, cream turtleneck, camel coat. Relays speech exactly; lazy; hides his hands when alone; hates being filmed secretly.
- Contract: the departed lover returns. The lover's name and gender are never stated (keep it so: doc 06 5).
- Lore:
  - About 300 playbills in four boxes.
  - An untitled playbill with empty cast slots in the mansion box office.
  - Seat **row 3, no. 7**.
  - The note "기다리지 마".

**Audit (7 / 5.5).**
- Name-first address works.
- **Theatre metaphors everywhere** break the doc rule ("매번 연극 비유를 쓰지 않는다").
- "어머" reads gender-coded; use "세상에".
- Hand-hiding and the filming dislike are absent.
- He shares a lexicon with 수아.
- Contradictions: "front row" vs 3-7 (fix: his debut was front row, 3-7 afterwards); "no word" vs the note (fix: "말 한마디 없이. 쪽지 두 단어만").

**Personality and contradictions.**
- Warm and flashy; delays with charm ("오 분만").
- Can reproduce anyone's words, yet misattributes identity by voice.
- Hates silence since the day the boxes left.
- His hands, hidden, are the only honest part of him.

**Speech.** Soft 반말, listener's name first. "있잖아" (his alone), "세상에", "아하하". At most one stage word per four lines.
- Never "내가 할게"; never films.
- Lying tell: the name-first habit vanishes.

| # | Line |
|---|---|
| 1 | 민혁아, 잠깐 앉아. 있잖아, 얘기부터 듣자. 일은… 오 분만 있다가. |
| 2 | 세상에, 매표소 서랍에 제목 없는 팸플릿이 있더라. 배역 칸이 전부 비어 있었어. |
| 3 | 나 조용한 거 싫어해. 누가 짐 다 빼 간 다음 날부터. |
| 4 | 예담아, 그거 찍고 있지? 끄자. 지금 이 얼굴은 무대 밖이야. |
| 5 | 작업은 끝냈어. 정리만 남았고. *(the lie: no name first, too smooth)* |

**Visual and gesture.**
- Big faces in company; hands in his sleeves when alone.
- Adjusts his scarf; reads playbills.
- Keeps seat 3-7 empty in the theatre.
- Hangouts: late morning in his room, then the Lounge sofa; afternoon Theater; evening Dining.

**Secret, fear, desire.**
- Secret: he reads "기다리지 마" as "wait"; (K2) the voice on the USB.
- Fear: silence; the empty seat.
- Desire: "해피엔딩".

**Daily life.** Small-theatre show (goal); runs lines with anyone willing; postpones chores (the doc's labour-avoidance friction).

**Murder and 심판.**
- At a death: big grief outside, shaking hands hidden.
- In the 심판: the **verbatim relay witness**. He protects people's turn to speak ("세나야, 잠깐만. 그 사람 문장 아직 안 끝났어").
- Tell: drops the name-first; too smooth.
- Breaking point: someone reads the note aloud, or asks him to repeat the lover's words.

**Case roles.**
- Red herring ★★★ (any heard voice points at him).
- Witness ★★★ (exact wording, identity errors).
- Culprit ★★ (voice-imitation alibi). Accomplice ★★ (easy help). Victim ★★.

**Relationships.** 라온 (#8, K2), 예담 (#28), 세나 (#29), 수아 (#30).

**Arc.** Growth: he reads the note as written and stops waiting. Fall: tries to "reproduce" the lover (the USB), or kills for the wish.

**Bond stages.**
- I 「빈 배역」: he casts the player.
- II 「3열 7번」
- III contract: "해피엔딩"
- IV echo device: the player is already sitting in 3-7 when he walks in, and he stops mid-line.

---

### P10 강준서

**Hook.** 열여덟 개의 숟가락을 놓는 요리사. 맛을 반쯤 잃은 혀 대신, 당신에게 "간 좀 봐 줄래요?"라고 묻는다.

**Canon.** 20, 187 cm, cook. GLB model.
- User directive 28: **green clothes, a mole under the left eye**. Update `Cast.cs`.
- Contract: 「백야의 씨앗」.
- Lore:
  - He counts 18 spoons (his grandmother's table, where spoons were always short).
  - Leftover spoons frighten him.
  - He wants to taste her scorched-rice water (누룽지 끓인 물) again.
  - He lost about half his taste (salt and bitter) after last winter's illness. He **would not notice something slipped into his pot**.

**Audit (8 / 7).** The best match to spec. But:
- The polite and casual sets mirror each other mechanically.
- The doc's collapse line is missing.
- `{reason}` breaks grammar in `suspect`.
- The 채령 tie is one line.
- B2 ("남을 때가 있잖아요") presumes a death: gate it or make it the grandmother's table.

**Personality and contradictions.**
- The gentle giant who overrides refusals "for your sake".
- Calm (Composure 82) until his care is refused, and then he drops honorifics.
- A cook who can't taste, who must ask.
- (K1) His care has a root he doesn't know.

**Speech.** Consent-asking 해요체, then relaxed 반말. "우선 앉아요", "밥은요?", **"간 좀 봐 줄래요?"**, "어이쿠", "허허".
- Anger drops the 호칭 and 씨.
- Never "싫으면 말고"; never wastes food.

| # | Line |
|---|---|
| 1 | 우선 앉아요. 먹고 얘기해요. 싫은 건 먼저 말해 주시고요. |
| 2 | 간 좀 봐 줄래요? 제 혀가 요즘 좀 게을러서요. 허허. |
| 3 | 숟가락 열여덟 개. 오늘도 다 놓았어요. …다행이다. |
| 4 | 혼자 가신다는 거 알아요. 그래도 이건 들고 가요. 도시락이에요, 호위 아니고. |
| 5 | 그만해. 사람부터 봐. 지금 네 말 챙길 때야? |

**Visual and gesture.**
- Lowers himself to eye level (source).
- Wipes his hands on his apron; carries a tasting spoon in the apron pocket.
- Counts spoons aloud when laying the table.
- Hangouts: 06:30 Kitchen, Dining (serving), afternoon Greenhouse (basil), evening Kitchen.

**Secret, fear, desire.**
- Secret: the lost taste; (K1) the night drive.
- Fear: a spoon left over; serving something spoiled.
- Desire: one spoonful of 누룽지 water.

**Daily life.**
- Cooks two meals (goal) and greenhouse herbs.
- Takes food to those who skip meals (서윤, 세나).
- Escorts people who didn't ask (채령).

**Murder and 심판.**
- At a death: "사람부터." He carries the injured.
- Investigation: who ate what, and who didn't come to the table.
- In the 심판: shades testimony to protect people ("잠깐 나갔을 뿐이야", doc).
- Tell: offers food; wipes hands that are already clean.
- Breaking point: someone was poisoned *through his food*, or he is told he decided for someone who died.

**Case roles.**
- Victim ★★★ (poisoners use his pot; he'd notice a missing knife).
- Accomplice ★★★ (covers for 채령 or 세나).
- Witness ★★★ (meal attendance).
- Culprit ★ (a protective killing; kitchen tools; the murder track's dismemberment affinity .3).

**Relationships.** 채령 (#6), 세나 (K1), 진우 (#18), 민서 (#19), 시온 (#33), 은결 (#32).

**Arc.** Growth: respects one refusal and waits. Fall: forced protection breeds resentment; or he is the unknowing poison vector.

**Bond stages.**
- I 「바질」
- II 「모자란 숟가락」 (the grandmother's table — no presumed death)
- III contract (누룽지 물)
- IV echo device: the player asks "간 봐 드릴까요?" before he can.

---

### P11 윤해린 — weak in play: see fix list

**Hook.** 무엇이든 뜯어보고 고치는 기계공학자. "문제없어"라고 말할 때만, 그녀는 거짓말을 한다.

**Canon.** 20, 168 cm, mechanical engineer. Procedural model: messy bun with a pencil, goggles, orange overalls tied at the waist, tool belt.
- Contract: undo the accident (a lift controller with a known sensor defect; she stamped the inspection; the client died).
- Lore:
  - The clockwork bird that mimics speech.
  - Twelve tools, of which she only uses the tape-wrapped screwdriver.
  - The soda she shook once.
  - "태엽은 거꾸로 감으면 끊어져".

**Audit (6 / 5).**
- "확인" ×47 is monotone.
- **Zero curses**, although the doc asks for "거친 욕설".
- Her 심판 lines duplicate 세나's (재판 "증명해", "내가 너무 빨랐어").
- Her signature lie ("문제없어") has no line; `repair_claim` falls back to ANY.
- B1's bird says "또 하나 줄었네", which presumes a death.

**Fix.**
1. Own `repair_claim` = "문제없어. 다 봤어." This is her lie and, once learned, her tell.
2. Swearing when scared ("씨발, 만지기 전에 전선부터 봐").
3. Slow speech when admitting failure.
4. Engineering nouns instead of "확인".
5. Remove the 세나 duplicates.
6. The B1 bird mimics something mundane.

**Personality and contradictions.**
- Curious and blunt, and *not* a genius fixer.
- Feels worthless if she can't fix a thing, so she hides her verification gaps (doc).
- Loves windup toys because "감은 만큼만 움직이잖아. 정직해".
- The most honest person in the house about machines, and the least honest about herself.

**Speech.** Fast, clipped 반말: "오!", "이거 봐", "됐다!", "헤헤"; swears when scared.
- Never "완벽해", never "대충" ("사람 죽이는 단어야").
- Lie phrase: "문제없어." Failure: … slow … speech.

| # | Line |
|---|---|
| 1 | 잠깐, 해 보자. 안 되면 안 된다고 말할게. 진짜로. |
| 2 | 이거 봐. 이 집 문고리 전부 규격이 달라. 누가 일부러 이렇게 만든 거야. |
| 3 | 탄산수 하나 남겼어. 이번엔 안 흔들었거든? …아마. |
| 4 | 멈춰. 씨발, 만지기 전에 전선부터 봐. |
| 5 | 확인… 안 했어. 알면서… 넘겼어. 또. |

**Visual and gesture.**
- Snaps toward tools (source); goggles down when hiding something.
- Winds toys; shakes soda by accident.
- Hangouts: morning 공방, afternoon PowerRoom and MachineRoom (inspection goal), Dining (soda), evening 공방 (세나's adjuster).

**Secret, fear, desire.**
- Secret: the lift; (K4) the part's origin.
- Fear: "대충"; 세나 asking "고칠 수 있어?"
- Desire: to undo it.

**Daily life.** Facility inspections (goal). Real repairs through the existing IG07 `Repairs` system. 세나's prosthetic adjuster (goal G11b). The bird.

**Murder and 심판.**
- At a death: "멈춰." She secures hazards and examines mechanisms.
- In the 심판: feasibility testimony; says "모른다" about functions nobody tested (doc).
- Tell: "문제없어", goggles down.
- Breaking point: a device she passed kills someone, or the word "대충".

**Case roles.**
- Red herring ★★★ (handles wiring daily; "문제없어").
- Witness ★★★ (feasibility).
- Culprit ★★ (shock traps, timers).
- Victim ★★ (the engineer who finds the trap).

**Relationships.** 세나 (#7), 도윤 (#15), 태겸 (#25, K4), 라온 (#26).

**Arc.** Growth: says "못 고쳐" to 세나 and stays anyway. Fall: a "temporary" rig kills.

**Bond stages.**
- I 「기계 새」 (the bird mimics a mundane line)
- II 「열두 개 중 하나」
- III contract (the lift)
- IV echo device: the player hands her the tape-wrapped screwdriver unasked.

---

### P12 오수아

**Hook.** 빨간 녹화등이 켜지면 몸이 먼저 웃는 아이돌. 누구에게나 "맞아요"라고 해서, 결국 모두에게 다른 약속을 한다.

**Canon.** 20, 165 cm, idol. Procedural model: long black hair, huge black ribbon, pink cropped jacket, star pins.
- Contract: a glamorous comeback.
- Secret: a call "from above" froze her career; **the caller is never confirmed as 이현**.
- Lore:
  - Trainee since 14.
  - Greets flowerpots as practice.
  - 23 star stickers around the mansion, one on Yusti's glove (his water paused).
  - Fan greeting "오늘도 반짝".

**Audit (7 / 6).** Vivid polite set; generic casual set. She names nobody. She shares "무대보다 떨려" and similar lines with 재하. Her `apology` has no context. "있잖아요/음~" collide with 재하.

**Personality and contradictions.**
- Perceptive and warm, and avoids conflict by reassuring both sides. Her kindness manufactures contradictions (doc P1002).
- Cute tastes are image management: she eats spicy snacks in secret.
- Freezes when a man in a suit approaches.

**Speech.** Bright 해요체, then soft 반말. "꺄", "대박", "에헤헤", "오늘도 반짝!".
- Sincere: lower pitch and "나는".
- Never a flat "싫어요"; never badmouths anyone to their face.

| # | Line |
|---|---|
| 1 | 이거 귀엽죠? 별 스티커예요. 제일 좋은 팬한테만 주는 건데… 하나 골라요. 에헤헤. |
| 2 | 꺄, 깜짝이야! …아, 카메라 없구나. 괜찮아요, 버릇이에요. |
| 3 | 두 분 다 맞아요. 진짜로요. 그러니까 오늘은 그만 싸워요, 네? |
| 4 | 나는… 웃는 거 말고는 할 줄 아는 게 없을까 봐 무서워. |
| 5 | 빨간 불도 안 켜졌는데 계속 웃고 있었어요. 이제 어떻게 멈추는지 모르겠어요. |

**Visual and gesture.**
- Eyes flick to the room's corners; poses reflexively.
- Stickers on things; a spicy-snack pack in her pocket.
- Hangouts: morning bedroom (sorting goods), Dining; afternoon Theater and Greenhouse; evening Lounge.

**Secret, fear, desire.**
- Secret: the call; she doesn't know who made it.
- Fear: silence without a camera; being forgotten.
- Desire: to be loved without performing.

**Daily life.** Small-stage show and sticker-goods goals. Mediates fights, giving each side a different assurance (a real contradiction source).

**Murder and 심판.**
- At a death: "몰래카메라죠?", then collapse.
- In the 심판: "제가 먼저 말할게요" (to spare others). Her split assurances surface as testimony contradictions (doc).
- Tell: the brightest smile, eyes to the corner.
- Breaking point: two people compare what she told each of them; someone tells her to smile.

**Case roles.**
- Accomplice ★★★ (unwitting: the culprit uses her reassurances).
- Red herring ★★★ (contradictions look like lies).
- Witness ★★ (who talked to whom). Victim ★★. Culprit ★ (the one nobody suspects).

**Relationships.** 채령 (#11), 시온 (#12), 예담 (#13), 이현 (#24), 재하 (#30).

**Arc.** Growth: one clear, kind "싫어요", to 시온 or to 예담's camera. Fall: her double assurances light the fuse of a murder.

**Bond stages.**
- I 「별 스물세 개」
- II 「빨간 녹화등」
- III contract (the call from above)
- IV echo device: the player returns a star sticker in a design she hasn't printed yet (she sketched it last night).

---

### P13 정세나 — weakest in play: see fix list

**Hook.** 왼손으로 처음부터 다시 배운 프로게이머. 0.1초 느린 손 대신, 판정은 누구보다 빠르다.

**Canon.** 20, 173 cm, pro gamer; left-handed now (`Cast.LeftHanded`). Procedural model: short black hair with a red streak, black-rim glasses, headset, team hoodie, **matte white prosthetic right hand**.
- Contract: get the lost hand back.
- Lore:
  - Last winter's traffic accident; her team was told "wrist ligament".
  - She relearned left-handed and is still 0.1 s slow.
  - A secret left-hand pad layout she made in rehab.
  - She reviews won replays longer than lost ones.
  - The team notice "정세나 선수 휴식".

**Audit (5 / 4).** Her testimony and 심판 lines are interchangeable with 해린's and 라온's.
- The doc's swearing is missing.
- "확인도 안 하고" is borrowed from 해린.
- Her silence-anxiety line belongs to 재하.
- The `meal` line misses 준서.
- The 민서 arc is absent.

**Fix.**
1. Gamer lens in *every* key: 판, 리플레이, 각, 판정, GG, 트롤.
2. Swearing (존나, 씨).
3. Her own verbs ("보지도 않고 몰았어").
4. The day-1 accusation of 민서 and the apology arc.
5. Hide the prosthetic when lying.
6. `meal` addressed to 준서 by name.

**Personality and contradictions.**
- Action first; defends whoever looks wronged *before checking* (doc).
- Studies wins harder than losses, but judges people in one frame.
- Hates pity, and hides the one thing people would pity.

**Speech.** Blunt 반말, short exclamations, curses. "솔직히", "한 판 더", "GG", "하".
- Apologies start with the person's name, no excuses.
- Never "도와줘", "불쌍", "확인".

| # | Line |
|---|---|
| 1 | 한 판 더. 방금 건 내가 판단 잘못했어. 인정. |
| 2 | 솔직히 걔 편드는 거, 약해 보여서가 아니야. 억울해 보여서야. …그게 그건가? |
| 3 | 난 이긴 판 리플레이를 더 오래 봐. 운빨이었는지 알아야 되니까. |
| 4 | 민서야. 내가 틀렸어. 보지도 않고 몰았어. 미안하다. |
| 5 | 왼손은 아직 0.1초 느려. 그 0.1초 동안… 아무것도 못 했어. |

**Visual and gesture.**
- Leans in to intervene (source).
- Drills left-hand inputs on tables; flexes the prosthetic's fingers.
- **Hides the prosthetic in her hoodie pocket when she lies about needing help.**
- Hangouts: morning GameRoom, noon Dining (fast), afternoon Pool and corridor (exercise), evening GameRoom (replays).

**Secret, fear, desire.**
- Secret: the hand; (K1) the van.
- Fear: pity; being benched forever.
- Desire: the 0.1 second.

**Daily life.** Practice, game-tournament goal, rematches, stepping into other people's arguments.

**Murder and 심판.**
- At a death: first on the scene; accuses fast.
- In the 심판: "들어간 건 맞아. 그래서? 뭘 했는지도 봤냐?"
- Tell: "괜찮다고", hand in pocket.
- Breaking point: proof she accused the wrong person *again*, or pity for the hand.

**Case roles.**
- Red herring ★★★ (aggression, snap accusations).
- Witness ★★★ (fast and direct; conclusions unreliable).
- Culprit ★★ (vigilante: kills someone she judged guilty).
- Victim ★★ (charges in alone).

**Relationships.** 해린 (#7), 민서 (#17), 도윤 (#16), 준서 (K1), 재하 (#29), 민혁 (#36).

**Arc.** Growth: she waits for the whole replay once. Fall: vigilante killing.

**Bond stages.**
- I 「한 판 더」
- II 「선수 휴식」
- III contract (the 0.1 second)
- IV echo device: the player sets up her secret left-hand layout.

---

### P14 차은결

**Hook.** 모든 죽음 앞에서 고요한 장의사. 동생이 어지른 자리를 치울 때도, 그녀의 손길은 똑같이 정성스럽다.

**Canon.** 20, 171 cm, funeral director. Procedural model: knee-length hime-cut black hair, high-collar mourning dress, pearl brooch, lace gloves, white lily.
- Contract: 도윤 moves as she wills.
- Lore:
  - 18 paper lilies, one per resident.
  - The walk "아홉 시 십이 분, 세 번째 기둥 모퉁이", on which she checks "문 아래 흙, 젖은 소매, 제자리에 없는 액자". **"찾으면 치워요."**
  - Three moving boxes, one not hers.

**Audit (7 / 6).** Strong motifs, but "조용 ×38 / 고요 ×16" make her one-note. She speaks 해요체 where the spec gives -습니다. Her tics never fire in barks. The 결 puns are forced. `small_talk` "도윤 씨, 오늘 보셨어요?" can be said to 도윤.

**Personality and contradictions.**
- Respects grief, but neutrality is her hiding place.
- Drawn to the stillness of the dead.
- Deadpan puns.
- **Her tidying habit is evidence tampering**: the most dangerous innocent quirk in the house.
- Her control over 도윤 wears the face of care.

**Speech.** Low, even -습니다, becoming -요 when close; long waits after questions. "그런가요.", "후후", "어머나" (rare); one deadpan pun per scene at most. At most one "조용/고요" per scene.
- Never "힘내세요", never "불쌍해라".
- Her voice rises only about 도윤.

| # | Line |
|---|---|
| 1 | 차가 식기 전에는 돌아오겠습니다. 함께 걸으시겠어요? |
| 2 | 관심은 고맙습니다. 관은 아직 필요 없고요. …후후. |
| 3 | 아홉 시 십이 분, 세 번째 기둥 모퉁이. 그 시간엔 늘 거기 있습니다. |
| 4 | 예담 씨. 죽음은 퀴즈가 아닙니다. 정답이 없으니까요. |
| 5 | 도윤이가 어디 있는지 말씀해 주세요. 지금요. …부탁이 아닙니다. |

**Visual and gesture.**
- Vertical posture; gloves never off.
- Folds lilies; pours tea for others.
- Walks her fixed route at 09:12.
- Hangouts: 09:12 route; morning Chapel (추모 예배실); afternoon Greenhouse and Library; evening 다과실.

**Secret, fear, desire.**
- Secret: the cover-ups; the route's real purpose; (K5) the funeral.
- Fear: 도윤 out of sight; being unneeded.
- Desire: 도윤 at her side, where she can see him.

**Daily life.** The route, the lilies (memorial-chapel goal), tea, crafts, checking on 도윤. After a death she prepares the memorial.

**Murder and 심판.**
- At a death: calm, professional; handling order and missing paperwork (doc). No automatic time-of-death skill.
- In the 심판: neutral until 도윤 is threatened.
- Tell: **her pause gets shorter**.
- Breaking point: 도윤 accused with real evidence, or "상자 세 개".

**Case roles.**
- Accomplice ★★★★★ (the 09:12 clean-up).
- Red herring ★★★ (near bodies; tidies scenes).
- Culprit ★★ (to bind 도윤 or to protect him; natural staging).
- Victim ★★ (도윤 decides she's a liability).
- Witness ★★ (precise; omits).

**Relationships.** 도윤 (#4), 진우 (#9, K5), 예담 (#31), 준서 (#32), and 유스티 (mutual funeral courtesy; she calls him "집사님").

**Arc.** Growth: she lets 도윤 face what he did, choosing truth over control. Fall: kills to keep him.

**Bond stages.**
- I 「열여덟 송이」
- II 「아홉 시 십이 분」
- III contract ("어디로 가는지, 누구 곁에 있는지")
- IV echo device: the player is waiting at the third pillar at 09:12 before she arrives. She is frightened: someone knows the clean-up route.

---

### P15 남가온

**Hook.** 확인은 세 번, 제목은 마지막. 두 번에서 멈췄던 날을 잊지 못하는 기자.

**Canon.** 20, 167 cm, journalist. Procedural model: red bob with bangs, black beret, mustard trench, lanyard, notebook.
- Contract: a genius mind.
- Lore:
  - The false report: checked twice; the correction ran two weeks later in a page-12 corner; the man died first.
  - Her notebook's first page: "확인은 세 번, 제목은 마지막".
  - Instant coffee with half a sugar packet ("70% 비슷").
  - She drew Yusti with four goldfish, one of which changes colour daily.
  - B3: "맞는 기사도 누군가를 무너뜨려요."

**Audit (7 / 5.5).** Superb 심판 voice. But "확인" ×78; the "writes the weather" habit is missing; the 이현 exposé never surfaces in barks; she shares step-counting with 민서.

**Personality and contradictions.**
- Relentless, sceptical of authority, and brave enough to correct herself.
- Believes publishing *is* justice, and so takes other people's choices away (doc).
- Her rule was born from one failure; her accurate article still ruined someone (K3).
- Rests by writing down the weather.

**Speech.** Short 해요체, verification questions, silences. **"잠깐만요."**, "직접 보셨어요, 들으셨어요?", "근거는요?", "적어 둘게요", "흐". At most one "확인" per three lines.
- Never "확실해요" unless triple-checked; never names a source.

| # | Line |
|---|---|
| 1 | 잠깐만요. 직접 보셨어요, 아니면 들으셨어요? 둘은 따로 적어야 해서요. |
| 2 | 오늘 날씨. 창밖은 비, 복도는 맑음. …이 집은 날씨가 두 개예요. |
| 3 | 제목은 마지막에 붙여요. 먼저 붙이면 결론이 따라와요. |
| 4 | 백이현 씨, 방금 표현 정정하실 건가요? 원문 그대로 적어 둘게요. |
| 5 | 두 번 봤어요. 두 번이요. …세 번째를 안 봐서, 사람이 죽었어요. |

**Visual and gesture.**
- Her notebook is always open; **it closes when she lies**.
- Looks from notes to face and back (source). Pen behind her ear; a paper cup.
- Hangouts: morning notice board, then Archive (the map); afternoon Library and corridors (with 민서); evening Dining (coffee, notes).

**Secret, fear, desire.**
- Secret: the false report; (K3) "한 사장님"; 태겸's ledger, held back.
- Fear: another "second check" day; being the story.
- Desire: a mind that never misses.

**Daily life.** The notice board. The mansion map with 민서 (goal LG01). Records. Interviews, asking consent first (her growth). The weather.

**Murder and 심판.**
- At a death: writes the time and the weather, then approaches. Sources, originals, hearsay chains (doc).
- In the 심판: "같은 얘기를 두 번 세셨어요. 근거는 하나예요."
- Tell: the notebook closes; "제보자는 없습니다".
- Breaking point: realising she repeated hearsay as fact.

**Case roles.**
- Victim ★★★★ (she knows 이현, 태겸, and maybe 도윤's files).
- Witness ★★★★ (source chains).
- Culprit ★★ (to protect a source, or to stage a "revelation": fake messages).
- Red herring ★.

**Relationships.** 이현 (#5), 서윤 (K3), 태겸 (#35), 민서 (#22).

**Arc.** Growth: gives 서윤 the choice before telling. Fall: publishes a secret (CH06-like) that gets someone killed.

**Bond stages.**
- I 「백열두 걸음」 (the map by *민서's* counts)
- II 「제목 없는 일기」 (seed: "한 사장님")
- III contract (a genius mind)
- IV echo device: the player hands her instant coffee in a paper cup, half a sugar packet.

---

### P16 신채령

**Hook.** 선물마다 준 사람과 날짜를 적는 스타일리스트. "고맙다"는 말 대신, 장부에 한 줄이 늘어난다.

**Canon.** 20, 169 cm, stylist; left-handed. Procedural model: thick dark-purple side braid, magenta big-shouldered jacket, wide slacks, silver brooch, tape measure.
- Contract: own a successful business.
- Lore:
  - A 편집숍 with the prettiest window in the alley.
  - The real failure: she ordered three times the fabric and picked a rent she couldn't pay. Her excuses: the supplier, the landlord (and now: 수아's hiatus, 이현's unpaid bill).
  - She keeps the shop key.
  - The duck 「꽥 사장」, the shop's unpaid "대표".
  - A mother-of-pearl button.

**Audit (7 / 6).** The best bond scenes in the cast. But her barks lean on tsundere cliché ("딱히 너 때문은 아니고"), the doc's "느긋한" quality is missing, she borrows "헐", and `fear_of` is childish.

**Personality and contradictions.**
- Cynical and self-reliant; looks down on other people's taste.
- Blames everyone for the shop.
- Hurt when people step *out* of the line she drew.
- Adores childish mascots.
- Remembers every gift with its date and says thank you to nobody.

**Speech.** Languid 반말, short parallel sentences, dry cuts. "별로.", "풋.", "뭐, 어쨌든.", "흥".
- Praise comes with her eyes turned away (source).
- Never a plain "고마워", never "헐", never shouts.

| # | Line |
|---|---|
| 1 | 취향은 존중해. 내 물건 만지는 건 별개고. |
| 2 | 핏은 괜찮아. 색은 별로. …입은 사람이 괜찮으니까 봐줄게. |
| 3 | 고맙다는 말은 안 해. 날짜 적어 둘게. 그게 더 오래 가. |
| 4 | 싫다고 했잖아. 호의면 그쯤은 알아들어. …도시락은 두고 가. |
| 5 | 혼자 두라고 했지. 정말 아무도 오지 말라는 뜻은… 아니었어. |

**Visual and gesture.**
- Keeps her distance; sizes people up by eye.
- Sorts buttons.
- **Squeezes 꽥 사장 in her pocket when she lies.**
- Hangouts: morning bedroom (sorting), 의상실 (Wardrobe) (styling goal); afternoon Gallery; evening a Lounge corner, alone.

**Secret, fear, desire.**
- Secret: her own fault.
- Fear: failure; people leaving her line for good.
- Desire: the window.

**Daily life.** Styling and exhibition goals; business notes; logs gifts; refuses 준서's escort; refuses 예담's games until 예담 makes them optional.

**Murder and 심판.**
- Witness to clothes: fibres, missing buttons, the wrong outfit. She does not identify a disguised person automatically (doc).
- In the 심판: protects privacy ("안 보여 주겠다는 거야. 없다는 말은 안 했어").
- Tell: "기억 안 나", squeezing 꽥 사장.
- Breaking point: her own gift log contradicts her; the shop's truth.

**Case roles.**
- Witness ★★★ (clothing: a torn button).
- Culprit ★★ (thread → sealed-room trick; blame-shifting).
- Red herring ★★ (thread and tape-measure access). Victim ★★.

**Relationships.** 준서 (#6), 수아 (#11), 이현 (#23), 예담 (doc 05 2: an optional invitation defuses it).

**Arc.** Growth: "내 탓이야", once. Fall: frames someone after a death.

**Bond stages** (existing, strong; fix 민혁's early 반말):
- I 「자개 단추」
- II 「진열창」
- III contract
- IV echo device: the player hands her a mother-of-pearl button **on the date she wrote last loop**.

---

### P17 송예담 — weak in play: see fix list

**Hook.** 모두를 같은 놀이에 초대해야 안심하는 크리에이터. 단 한 사람, 언니에게만 초대장을 보내지 않았다.

**Canon.** 20, 160 cm, short-form creator. Procedural model: platinum twin tails, mint capelet with toy buttons, layered pastel skirt, striped stockings, phone on a lanyard.
- Contract: her sister's address and one meeting.
- Lore:
  - 「저택 퀴즈쇼 제1회」; invitation #1 (for 민혁) folded into a staircase.
  - The stairs are 13 up and 14 down.
  - A paper model of the mansion, windows unfinished.
  - Her sister designed their models; 예담 glued them.
  - The sister's morning quiz: an animal with three hearts.
  - The truth: the sister sent her new address; 예담 deleted it.

**Audit (6 / 4.5).**
- Generic streamer talk (콘텐츠, 구독, 엔딩 크레딧).
- The spec's defining **"갑자기 판정문 어투" is used zero times**.
- Paper models appear once. She names nobody.
- She gamifies death and nobody reacts.
- She says "재판".

**Fix.**
1. A referee voice ("판정합니다.", "반칙.", "벌칙은…").
2. The paper-mansion model as a living object that gets updated every day.
3. Cut the streamer lexicon.
4. Pair lines with 채령, 수아, 재하, 서윤, 은결.
5. 은결 rebukes the "death quiz".
6. Silence after a refusal.

**Personality and contradictions.**
- Imaginative and loud; believes a conflict ends when everyone plays the same game (doc).
- Hates schedule changes, and never replied to a message.
- The real her shows only in the silence after "싫어".

**Speech.** Sing-song 반말 with big pitch swings and doubled words ("안녕안녕"). "짜잔!", "있지있지", "판정합니다."; "히히" and "에엥" are rare.
- Never uses her own name as "I" (doc).
- After a refusal: "……" and a smaller voice.

| # | Line |
|---|---|
| 1 | 오늘의 문제! 이 종이 한 장이 뭐가 될까? 정답은… 짜잔, 초대장! |
| 2 | 판정합니다. 약속 시간을 몰래 바꾸는 건 반칙. 언제부터 바뀐 건데? |
| 3 | 안 와도 돼. 그래도 네 자리는 만들어 둘게. 이름표까지 붙여서. |
| 4 | ……응. 알았어. 다음 문제는 혼자 풀지 뭐. |
| 5 | 심장이 세 개인 동물, 뭐게? …언니가 매일 아침 내던 문제야. 이제 아무도 안 내. |

**Visual and gesture.**
- Big hand shapes when explaining a game (source); folds paper models.
- Hands out invitations; films.
- Silence after a refusal (source).
- Hangouts: morning DollRoom and 공방 (models); afternoon GrandHall (invitations), Lounge (quiz show); evening GameRoom.

**Secret, fear, desire.**
- Secret: she deleted the address.
- Fear: being left out; the unanswered text.
- Desire: everyone in one room.

**Daily life.**
- Quiz show; gatherings with changing times through the existing IG02 revisions.
- **The paper mansion model**: each day she adds the rooms she has visited. It is a living map the player can consult.

**Murder and 심판.**
- At a death: she tries to make it a quiz and is stopped (은결).
- In the 심판: proposes rules ("한 사람 말 끝날 때까지 끼어들기 금지", doc).
- Tell: the rhythm stops and the referee voice switches on.
- Breaking point: "언니가 안 받은 게 아니라, 네가 안 받은 거잖아."

**Case roles.**
- Witness ★★★ (her phone clips and photos).
- Victim ★★★ (the host, alone before the party).
- Culprit ★★ (gathering trick: she hosts, slips out, and changes the time).
- Red herring ★★ (the time changes).

**Relationships.** 채령 (optional invitations), 수아 (#13), 재하 (#28), 서윤 (#20), 은결 (#31).

**Arc.** Growth: "you may play" instead of "you must play"; she leaves an invitation addressed to her sister on the empty chair. Fall: the forced gathering becomes the murder stage.

**Bond stages.**
- I 「초대장 1호」 (13 up / 14 down)
- II 「종이 저택」
- III contract (the address)
- IV echo device: the player answers the octopus quiz before she asks it.

---

### P18 임민서

**Hook.** 끝낸 일만 짧게 보고하는 노동자. 도서실 맨 아래 칸, 3단원을 접어 둔 미적분 책만 밤마다 펼친다.

**Canon.** 20, 182 cm, labourer. Procedural model: buzz cut, faded olive work shirt, digital watch, thermos.
- Contract: a stable home and four years of living costs.
- Lore:
  - Barley tea; number puzzles (the answer "칠"); the 4:40 a.m. first train; two younger siblings.
  - Accepted to a school; the calculus book folded at chapter 3.
  - He told his family he hated studying.

**Audit (8 / 6).** The most consistent resident, but minimal ("안녕하세요.", "그럼."). ANY fallbacks force 해요체 onto him. The digital watch never appears. He names nobody.
- **Fix:** "a thermos I've used for 12 years" becomes **"아버지가 현장에서 12년 쓰던 걸 물려받은 보온병"**.

**Personality and contradictions.**
- Quiet, fair, finishes every task.
- Never states his own needs, so resentment ends in a total cut-off.
- The most precise witness ("모릅니다" ≠ "못 봤습니다").
- A labourer who is secretly a student.
- Dry jokes where he laughs first (doc).

**Speech.** Short 하십시오체, then short 반말. "끝났습니다.", numbers, "…", "허".
- Never "힘들어요". His lie: "힘들지 않습니다".

| # | Line |
|---|---|
| 1 | 끝났습니다. 다음 건 누가 합니까? …없으면 제가 하겠습니다. |
| 2 | 식당에서 도서실까지 백열두 걸음입니다. 잠겨 있으면 서른 걸음을 더 돌아야 합니다. |
| 3 | 못 봤습니다. 없었다는 뜻은 아닙니다. |
| 4 | 물 있어. 컵은 네가 가져와. …보리차야. 허. |
| 5 | 그만하겠습니다. 당연한 거 아니었습니다. 한 번도. |

**Visual and gesture.**
- Checks his hands and the space to move (source); reports in one line.
- **Counts steps under his breath** (his alone).
- Checks his watch when lying.
- Hangouts: morning Storage, noon Dining (fills the thermos), afternoon Greenhouse walk, night Library (the bottom shelf).

**Secret, fear, desire.**
- Secret: the school, the book.
- Fear: rent going up; being "당연한".
- Desire: to study; a room for each sibling.

**Daily life.** Carrying (goal); the route and step counts for 가온's map; number puzzles; silently takes the worst chore, which becomes a quiet contest with 서윤.

**Murder and 심판.**
- At a death: moves people back and checks the exits.
- Route timing and physical feasibility ("그 시간에는 못 옮깁니다. 같이 해 봤습니다", doc).
- Tell: the watch.
- Breaking point: "넌 원래 이런 거 하잖아", or the book exposed.

**Case roles.**
- Witness ★★★★★ (routes, times, weights).
- Accomplice ★★ (**unwitting carrier**: asked to carry a heavy trunk).
- Red herring ★★ (strong enough to carry a body).
- Culprit ★ (one explosive blow). Victim ★★.

**Relationships.** 태겸 (#10), 세나 (#17), 가온 (#22), 준서 (#19), 시온 (#34).

**Arc.** Growth: says a need aloud ("저도 쉬어야 합니다"). Fall: a total cut-off, which isolates him as a target, or an explosion.

**Bond stages.**
- I 「칠」
- II 「첫차 4시 40분」
- III contract (four years)
- IV echo device: the player opens the calculus book at chapter 3, page 1 (existing; keep)

---

### NPC00 유스티 — weak in play: see fix list

**Hook.** 어항 머리의 집사이자 하급 신. 금붕어가 헤엄을 멈추는 순간, 그는 무언가를 느끼고 있다.

**Canon.** 186 cm, butler and minor god.
- Aquarium head (water and goldfish), black tailcoat, ivory brocade vest, white gloves, pocket watch.
- Role (user directive 14): **announcements, and 심판 procedure and verdict only**. He takes no part in investigation or debate.
- Otherwise he stays in his room, where he can be spoken to, and in the 심판 he sits on the judge's throne.
- Emotion shows *only as the ripples stopping* (Cast.cs).

**Audit (6 / 5).** Precise and non-mascot, but:
- The most frequent announcements (`y_morning`, meal bells, `y_night`) read like a hotel concierge.
- Uncanny lines exist only in rare keys.
- **Several chapter-rule second variants teach the chapter's solution logic** (CH13 "명단에 이름이 있다고 열쇠를 가진 것은 아닙니다", CH14, CH15, CH17). Remove them.
- `y_ability_grant` and `y_loop_reset` reveal the loop to everyone. Keep that only if it is intended.
- `y_intro` "제가 누구인지" must read "그분이 누구인지".
- Ten rule pages exceed 60 characters.

**Personality and contradictions.** Impartial and exact. He is *procedure given a body*: he sees everything and says only what the procedure requires. His one feeling is continuity: the goldfish forget and he doesn't. His mercy is procedural (the soup kept warm, the seat laid), never interference.

**Speech.** 하십시오체 only, "~ 님". "알려 드립니다.", "정해진 대로", "절차에 따라". "…" is the ripple stopping.
- At most one sensory beat (water, glass, fish) per announcement.
- Never jokes, never opines on guilt, never hints, never uses a first name without 님.

| # | Line |
|---|---|
| 1 | 알려 드립니다. 아침입니다. 밤사이 잠가 두었던 문을 모두 열었습니다. |
| 2 | 식당에 저녁을 차렸습니다. 자리는 열여섯입니다. …정해진 대로, 전부 놓았습니다. |
| 3 | 그 질문은 제가 답해 드릴 수 있는 범위 밖입니다. 기록에 있는 사실만 전해 드립니다. |
| 4 | 금붕어는 금방 잊는다지요. …저는 잊지 않습니다. *(existing break line: loop 3+ only)* |
| 5 | 지목은… 맞았습니다. 정해진 대로 집행하겠습니다. |

**Visual and gesture.**
- Perfectly still, gloved hands folded.
- The goldfish swim until something matters; then they stop (the only expression).
- 수아's star sticker stays on his glove.
- Places: his room; the speakers; the throne.
- The 09:00 clock round (D-012, already implemented) is his only walk.

**Secret, fear, desire.**
- Officially none.
- Design: he remembers the loops. This surfaces in loop 3+ only, via `break_line`. He never becomes an ally.

**Daily life.** Announcements at 07:00, 08:00 and 18:30 meals, 22:00, plus hunger, events, rules and bodies. The clock round. Room conversations.

**Murder and 심판.** He announces the body (with the House bells), opens the 심판, calls for accusations and the vote, reads the result, and gives the verdict. Nothing else.

**Case roles.** None.

**Relationships.**
- 민혁: the only name followed by an extra pause.
- 은결: mutual funeral courtesy.
- 진우: tries to provoke him; the fish never stop.
- 수아: the sticker; the fish paused once.
- 해린: wants to open his head ("태엽이 어디 있는지 궁금해서요").

**Arc across loops.** Loop 1: pure procedure. Loop 2: one extra pause for 민혁. Loop 3+: "잊지 않습니다" surfaces.

---

## 5. Consistency ledger

### 5.1 Contradictions and bugs to fix

Apply these after the `playtest-and-korean` workflow releases `Sim/Content`.

| # | Where | Problem | Fix |
|---|---|---|---|
| 1 | Lines_P03 small_talk P | A cat; B1 says hamster 「회의록」 | Hamster |
| 2 | BondScenes_P02 B4 | Hated flavour is lemon; B1 says green | 초록 |
| 3 | Lines_P02 greet_morning | Counting heads (서윤's habit) | Move the line to P03 |
| 4 | Lines_P07 B1 / small_talk / meal | "여기 맥주가 없잖아" vs "맥주 한 캔 하자" | The bar serves beer; rewrite the B1 line |
| 5 | P05 gold tooth | "스물둘 첫 선거" contradicts age 20 | "열아홉, 첫 선거 캠프 자원봉사" |
| 6 | BondScenes_P05 B3/B4 | Double "말 놓을게" | B4 opens in the 반말 B3 established |
| 7 | BondScenes_P04 B4 | Near-confession | Oblique only ("상자는 늘 셋") |
| 8 | Lines_P04 panic / final_defense C, love_*, accuse | 반말 and "당신" | 존댓말 imperatives; name + 씨 |
| 9 | BondScenes_P08 B3/B4 | Who knows the song | "내가 USB에 옮긴 건 아무도 몰라" |
| 10 | P09 | "맨 앞줄" vs 3-7; "아무 말 없이" vs the note | Debut front row, then 3-7; "쪽지 두 단어만" |
| 11 | BondScenes_P10 B2, P11 B1 | Presume a death at stage 1–2 | Grandmother's table; the bird mimics something mundane |
| 12 | P18 thermos "12년" | Contradicts age 20 | Inherited from his father |
| 13 | P15 B1 steps | Step habit belongs to 민서 | She uses 민서's counts |
| 14 | Laughs | 가온 and 민서 both "흐" | 민서 → "허" |
| 15 | P16 / P17 | "헐" | Remove; it is 라온's |
| 16 | P17:350, P17:371, P02:255, P05:422/424/528, P07:272, P09:278 | "재판" in dialogue | 심판 |
| 17 | 12 BondScenes B4 | One formula | Unique echo devices (§4) |
| 18 | P01 in B1 of P16 / P17 | Early 반말 | 해요체 until agreed |
| 19 | Lines_NPC00 y_intro | "제가 누구인지" | "그분이 누구인지" |
| 20 | Lines_NPC00 y_rule_CH13/14/15/17 v2 (+ CH13 v1) | Solution hints | Remove |
| 21 | y_ability_grant, y_loop_reset | Loop leak to all | **Decided 2026-09-27:** residents are never told (SOURCE_V11 P1263, P1561; v2.2 §11.9.3, §11.15.3, REPLAY22-06; archive discovery undecided per P1262/P2075/P2096). `y_loop_reset` fires to all 18 at 07:40 of the new loop's day 1, so it is now a first-morning welcome with no rewind or "new mansion" wording; 민혁 gets only Yusti's extra pause. Only Yusti (silent until `break_line`, loop 3+) and the player know. |
| 22 | Lines_House.cs header | Stale role note | Update |
| 23 | Requests.MakeFind | "내 진우의 두꺼운 책" (the owner's name is inside the item name) | Use the item type name; per-character text via keys (DailyLife §9) |
| 24 | P10 suspect `'{reason}'…`; P07/P11/P13 "{item} 건" | Grammar | Rephrase |
| 25 | P06 small_talk "식량 사흘 치" | Hard world fact | Remove |
| 26 | P12 apology C | No context | Rewrite as a real apology |
| 27 | Cast.cs P06 | `RoundGlasses` | Remove (user directive 41) |
| 28 | Cast.cs P10 | Black shirt, cream apron | Green outfit, mole under the left eye (user directive 28) |
| 29 | Lines_ANY `applause` | "브라보" | Remove; it is 재하's |

### 5.2 Wording bans

**Everyone:** 재판, 학급재판, 학교, 반장.

**Stock phrases.** Each of these is allowed in at most one character file:
- "단둘이 있지 마"
- "기쁘지 않아"
- "왜 다 나를 봐"
- "좋아요. …아니, 좋아."
- "{topic} 좋아해? 난 좋아해"

**Theatre metaphors.** 재하 only, at most one per four lines.

**"조용 / 고요".** 은결: at most one per scene.

**"확인".**
- 가온: at most one per three lines.
- 해린: at most one per five lines.
- 세나: never.

---

## 6. Rules for anyone writing new lines

1. **One line, one person.** Cover the name. If you can't tell who is speaking, rewrite it using their §2 signature, pet words, or witness lens.
2. **Every resident has a witness lens.** Their testimony carries that detail; they never use the ANY skeleton.

   | Resident | Lens |
   |---|---|
   | 태겸 | Rental slip times |
   | 준서 | Meals |
   | 라온 | Sounds and counts |
   | 채령 | Clothes |
   | 해린 | Mechanisms |
   | 민서 | Steps and weights |
   | 가온 | Sources |
   | 서윤 | Head counts and schedules |
   | 은결 | Handling order |
   | 도윤 | Things out of place |
   | 재하 | Exact words |
   | 수아 | Who talked to whom |
   | 세나 | Speed and direction |
   | 예담 | Photos and clips |
   | 진우 | Faces |
   | 시온 | Who was in the library or bar |
   | 이현 | Who stood with whom |

   Example, 라온: "{time}쯤 {place} 쪽에서 {sound}. 박자로 치면 세 번. 얼굴은 못 봤어."
3. **Name people.** Each resident's daily lines mention their ties (§3) regularly, through pair variants (`key@Pxx`) so the line reaches the right listener, never the named person themself.
4. **Ration the tics.** A signature at most once per scene; the sincerity marker only at the arc's peaks.
5. **Secrets never leak through barks.** Hints only (`secret_hint`); the truth only in bond III/IV, a rule event, or the 심판.
6. **Bond IV devices never repeat** (§4 list).
7. Adults, 심판, the 60-character page, slot particles, no brackets.
