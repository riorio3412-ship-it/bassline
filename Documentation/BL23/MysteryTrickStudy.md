# BL23 Mystery Trick Study — 트릭 연구와 심판 설계

`Documentation/BL23/MysteryTrickStudy.md` · v1 (design study, no code changed) · 2026-09-27

**Inputs:**
- `MurderFoundation.md` v1 (the kernel physics-lite, the act composer, the trick grammar, CaseFile);
- `MurderArchitecture_Current.md`, `GapReport_Mystery.md`, `CharacterBible.md`, `DailyLifeDesign.md`, `HANDOFF.md` §1 and §3.9, `DecisionLog.md` (D-020, D-028, D-039);
- the trial-fun audit (`BL23Lab/trialfun/journal_v1.jsonl`: 12 headless trials, 18 design principles);
- code read for status: `Sim/Systems/Perception.cs`, `Testimony.cs`, `Cases.cs`, `Evidences.cs`, `Sim/Data/CastTraits.cs`, `Sim/Content/Lore.cs`, `BondScenes_P11.cs`;
- genre knowledge, with a few web checks (Appendix A).

`TrialReforge.md` does not exist yet. §3 is written so that it plugs into the running trial-reforge design (a 10-card deck: 5 true, 5 fake, with in-world photos) without changing its premises.

> **오너용 요약**
> 1. 김전일·코난·카·퀸·크리스티·시마다·아야츠지·히가시노·아리스가와·노리즈키·우미네코·단간론파(구조만)에서 트릭 원리 90여 개를 15갈래로 정리했다. 사건을 그대로 가져오지 않는다. **원리만** 우리 저택의 규칙과 인물의 솜씨로 옮긴다.
> 2. 이 트릭들을 손으로 쓰지 않고 **자동으로 만들어 내려면**, 기초 설계(MurderFoundation)의 물리·트릭 문법 위에 **새 시스템 10개**가 더 필요하다. 가장 급한 넷은 다음과 같다.
>    - 목격자가 '무엇을 보고 그렇게 믿었는지'를 남기는 기록(믿음의 출처)
>    - 방 사이 이동 시간 지도와 알리바이 검사
>    - 범인이 꾸미는 가짜 해법과 증인 배치
>    - 발견 순간의 기록(누가 먼저 들어가 무엇을 만졌는지, 그리고 현장 사진)
> 3. 우리 저택과 18명에게 재미 대비 비용이 가장 좋은 트릭 15개를 골랐다. 샹들리에 촛불 타이머, 태엽 새의 목소리, 실로 건 빗장, 밤 잠금 도우미, 시계 조작이 앞자리다.
> 4. 심판의 흐름은 서막 → 1막 첫인상 깨기 → 2막 반전(3D 재현 무대) → 3막 범인의 반격과 결정적 증거 → 4막 되짚기·투표·판결·고백이다.
>    - 단서 10장(진짜 5, 가짜 5)은 **가짜 한 장마다 그것을 뒤집는 진짜가 짝으로** 있다.
>    - 한 장을 뒤집을 때마다 이야기가 한 겹씩 벗겨진다.
> 5. 범인은 가만히 있지 않는다. 다른 설명, 시간 흔들기, 떠넘기기, 부분 인정, 증거 요구로 최대 세 번 반격한다. 플레이어는 두 번째 독립 증거로 답한다.
> 6. 추리가 스트레스가 되지 않게 한다.
>    - 사건마다 새겨진 수수께끼 2~3개
>    - 스스로 눈에 띄는 흔적(픽셀 찾기 없음)
>    - 저절로 떠오르는 기억
>    - 부분 결론을 확인해 주는 추리판과 3단계 힌트
>    - 막다른 길 없음, 되돌릴 수 있는 실수, 투표 전 되짚기
> 7. 구현은 세 갈래로 나눈다.
>    - murder-foundation: 커널 시스템과 새 트릭
>    - trial-reforge: 심판 흐름, 재현 무대, 단서 덱
>    - 새 작업: 수사 편의, 추리판, 힌트

---

## 0. Frame

### 0.1 The request
> "소년탐정 김전일 만화책이나 다른 추리소설들을 보고 그런 트릭들을 우리 게임에 구현해내려면 어떤 시스템들이 필요할 지 그리고 재판은 어떻게 더 흥미롭게 이끌어갈지를 판단하고 구현해줘. 단서 수집이나 추리하는 부분이 플레이어가 스트레스를 받지 않는 디자인이 되었으면 좋겠어"

The target set earlier is "Danganronpa-level trick stories and presentation". That is a quality bar and a structural reference only (D-028, memory `no-danganronpa-copy-gothic-style`).

This document is the *judgement* half of the request: which systems, which trial, which UX. The *implementation* half is the plan in §5, routed to the workflows that own the files.

### 0.2 How to read it
| Tag | Meaning |
|---|---|
| **E** | Exists in code today (file named) |
| **F §x** | Planned in `MurderFoundation.md`, section x |
| **M MS-nn** | Missing; specified here in §2.2 |
| **x** | Small extension of an F item (§2.3), not a new system |
| G-nn | A genre entry in §1 |
| T.Name | A trick template: foundation id, or *new* (proposed here, §2.4) |

### 0.3 Constraints every choice obeys
- **Kernel authority and determinism** (MurderFoundation H1–H4). New state lives in `Phys`/`Mur`, in sorted lists, and is evaluated in closed form. No new per-tick work, and no `string.GetHashCode`.
- **Lethality gate** (H5). Every new device or trick is lethal only through an admitted plan.
- **Fair play** (H6). Every falsification has ≥2 independent exposing paths, and at least one of them needs no single NPC and no power. Nothing is invented after the fact.
- **The truth is a person** (H10). Accident, suicide and natural death exist only as *presented* stories.
- **Roles** (H9). 김민혁 is never the culprit. Yusti is never the culprit and never lies. Unwitting helpers and after-the-fact protectors are allowed; a planned accomplice is OFF.
- **Yusti** handles announcements and the 심판 procedure and verdict only (HANDOFF directive 14). He takes no part in debate or hints.
- **Presentation.**
  - The trial is called **심판**; everyone is an adult; the look is gothic, not neon.
  - Exploration is first person, and every action has a motion.
  - Times are plain ("밤 11시쯤").
  - Few cards. A case files 10 cards, and "looking is not filing".
- **Banned** (D-028): nonstop debate, truth bullets, 논파, rebuttal showdown, anagram/hangman, panic-talk rhythm game, comic-panel reconstruction, slot vote, "학급재판". Also banned: DR names, rooms, mascot and execution spectacle, and Ace Attorney catchphrases.

### 0.4 The originality rule for borrowed tricks
A famous trick is a *principle* plus a *set-piece*. We take the principle and never the set-piece.
1. **Change the carrier.** Re-express the principle with our house's physical rules (MurderFoundation §A4) and furniture.
2. **Root it in the culprit.** The trick must come from the culprit's craft (StyleDef skills), so the aha says *who* as well as *how*.
3. **Our own hinge.** The detail that breaks it is ours: a published house rule, a CastTraits fact, a seam our physics produces.
4. **No recognisable tableau.** For example: no vanishing figurines on a mantel, no Phantom mask at an opera, no card held in a dead hand, no torso count. The watch-list in §2.5 marks the famous ones.

---

## 1. Trick taxonomy from the genre

Every entry gives the source and case, the idea in one or two lines, and where it lands in BL23. Summaries are paraphrased and kept short; they are spoilers by necessity.

### 1.0 Carr's locked-room lecture as the map
*The Hollow Man* (John Dickson Carr, 1935) has Dr Fell classify sealed-room murders. Mapped onto our axes (Time, Place, Identity, Cause, Means, Access, Sequence, Possession, Belief):

| Fell's class | Idea | BL23 landing |
|---|---|---|
| C1 | Not murder: a chain of accidents looks like one | Inverted by H10: murder presented as accident (T.StagedAccident). Accidents occur only as innocent disturbances (MS-07) |
| C2 | Murder, but the victim is driven to his own death (gas, poison, panic) | M.Gas.*, M.Bleed.Flee, T.SelfLocked |
| C3 | A device planted in the room kills later | M.Projectile, M.Shock.Handle, M.Poison.Contact, rigs (MS-06) |
| C4 | Suicide made to look like murder | Disallowed as truth (H10). Only the reverse exists (T.StagedSuicide) |
| C5 | Illusion and impersonation: the victim "alive" after death | T.SeenAlive, T.RecordedVoice, T.BellAnchor |
| C6 | Killed from outside, looks inside | T.ShaftDelivery (new), M.Crush.Drop. Windows do not open, so only vents, transoms, voids and shafts |
| C7 | Time reversed: presumed dead is only drugged, and the "rescuer" kills | T.FirstIn + MS-05 |
| Door methods | Key turned from outside; bolt drawn with pin and thread; hinges lifted; bar dropped by ice or weight; key slid back under the door | T.KeyReturn, T.ThreadBolt, T.HingeLift (new, x), T.IceTimer, T.JudasGap (new, x) |

### 1.1 Locked rooms — mechanical
| # | Source · case | Idea | BL23 |
|---|---|---|---|
| G01 | Japanese honkaku term "針と糸" (needle and thread) | A thread through the door gap draws the bolt and is pulled out. The modern consensus is that pure mechanics are weak unless the mechanism *says who*. | T.ThreadBolt; the thread is 채령's counted spool |
| G02 | Carr, *The Judas Window* (1938) | Every door has an overlooked opening (the knob-spindle hole). The "impossible" room had an ordinary gap. | DoorExt gap class "spindle" (x); T.JudasGap |
| G03 | Detective Conan (recurring) | Fishing line and tape swing a latch or turn a thumb-turn from outside, then are retrieved. | T.ThreadBolt variants; seams: tape residue, line groove |
| G04 | Genre staple | Ice holds up a bar or a weight and melts away. | T.IceTimer; puddle ring, ice count in the cold store |
| G05 | Genre staple | A magnet slides a steel bolt through a thin door. | x: Magnet agent + ferrous bolt profile |
| G06 | Kindaichi, 魔神遺跡殺人事件 | Several tricks layered: a locked room achieved in two stages, plus coded messages. | Tier D3 composition (≤3 tricks, F §B2) |

### 1.2 Locked rooms — timing: before sealing, after sealing, the victim seals it
| # | Source · case | Idea | BL23 |
|---|---|---|---|
| G07 | Zangwill, *The Big Bow Mystery* (1892) | The victim sleeps drugged in a locked room. The man who breaks in first kills him at that moment. | T.FirstIn + MS-05 entry order |
| G08 | Leroux, *The Mystery of the Yellow Room* (1907) | The attack came earlier than believed; what people heard in the sealed room was the victim alone. | T.SelfLocked, M.Bleed.Flee, victim acts (MS-07) |
| G09 | Christie, *Hercule Poirot's Christmas* (1938) | Crash and scream from the locked room are produced after the killing, while the killer stands among those rushing in. | T.LateCrash (new): timer + falling stack + T.RecordedVoice |
| G10 | Genre | Killed before someone else's routine locks the room. | T.NightLockHelper (Yusti's 22:00 lock) |
| G11 | Carr C2 / genre | Poisoned earlier; the victim bolts his own door and dies inside. | T.SelfLocked |

### 1.3 Locked rooms — hidden architecture
| # | Source · case | Idea | BL23 |
|---|---|---|---|
| G12 | Kindaichi, 異人館ホテル殺人事件 | An old "suicide" in a sealed room was murder through a hidden passage used for smuggling. | MS-01 passage with published grammar |
| G13 | Kindaichi, 飛騨からくり屋敷殺人事件 | A trick room on the revolving-door principle lets the killer leave while seeming to come from elsewhere. | MS-01 pivot panel; T.PivotRoom (new) |
| G14 | Ayatsuji, the Yakata (館) series | The reader is *told* that the architect's houses always hide mechanisms, which makes a hidden room fair. | Published house grammar (lore 『저택의 문법』) |
| G15 | Knox, *Decalogue* (1929), rule 3 | Not more than one secret room or passage. | Budget: ≤1 secret passage active per chapter |

### 1.4 Alibi — the time of death
| # | Source · case | Idea | BL23 |
|---|---|---|---|
| G16 | Genre / Conan (recurring) | Cooling slowed or hurried by heat, cold or water to move the time of death. | T.HeatShift / T.ColdShift (F R2, R10) |
| G17 | Christie, *Evil Under the Sun* (1941) | The "corpse" witnesses saw was a living person lying still; the real death came later. | T.PrankCorpse (new): the victim's own prank scheme, hijacked |
| G18 | Christie, *The Mysterious Affair at Styles* (1920) | An additive makes the last dose of a medicine deadly, hours after it was tampered with. | M.Poison.Delayed; cause time ≠ death time (F R7) |
| G19 | Higashino, *Salvation of a Saint* (2008) | Poison set in a household device long before; the alibi is simply the length of the wait. | T.LongFuse (new) + habit knowledge (MS-08) |

### 1.5 Alibi — clocks, witnesses, recordings
| # | Source · case | Idea | BL23 |
|---|---|---|---|
| G20 | Christie, *Murder on the Orient Express* (1934) | A watch smashed at a false hour, plus a trail of planted items pointing to a phantom outsider. | T.StoppedWatch + T.FrameItem. A natural "many fakes" case for the 5-fake deck |
| G21 | Christie, *The Murder of Roger Ackroyd* (1926) | A dictating machine plays the dead man's voice at a set time. | T.RecordedVoice with the gramophone, the recorder, or the house's **clockwork bird** |
| G22 | Ayatsuji, *The Clock House Murders* (1991) | Every clock in the old wing ran fast, so the people shut inside lived a shorter night than they believed. | T.ClockRate (new, x) in ClockMuseum; witnesses without watches |
| G23 | Detective Conan, the "Moonlight Sonata" case | A taped piano piece plays at each killing, and a code hides in the score. | T.RecordedPerformance + mitate (MS-09) |
| G24 | Kindaichi, 悪魔組曲殺人事件 | The solution turns on recognising particular sounds. | MS-02 sound attribution; 서라온 as the ear |
| G25 | Kindaichi, the Opera House stories (the later Phantom case) | A chandelier falls on an actress in front of everyone. A wire run under the stage was cut from a seat in the audience. | M.Crush.Chandelier; alibi by presence; rig (MS-06) |
| G26 | Genre | A photograph "proves" the hour, but the clock in the frame was wrong. | T.PhotoAlibi (Darkroom) |
| G27 | Genre | A witness judges the hour by a bell or broadcast that was rung early. | T.BellAnchor (chapel bell) |

### 1.6 Alibi — travel time and doubles
| # | Source · case | Idea | BL23 |
|---|---|---|---|
| G28 | Matsumoto Seichō, *Points and Lines* (1958) | A four-minute sightline across platforms and an overlooked faster route break a timetable alibi. | MS-03 route atlas; a hidden faster route (service stair, dumbwaiter) |
| G29 | Christie, *Lord Edgware Dies* (1933) | An impersonator attends a dinner as the culprit, giving her an alibi. | T.DoubleAtGathering (new); H9: only an unwitting stand-in (a "role-play" 재하 agrees to) |
| G30 | Christie, *Death on the Nile* (1937) | A staged shooting "wounds" the culprit, whose alibi becomes being unable to walk. | T.FakeInjury (new): a bandage and limp with no wound under them |
| G31 | Kindaichi (series-wide) | Many cases open with every survivor holding an airtight alibi. | Archetype TimeAlibi; L1 must present ≥2 alibis |

### 1.7 Identity
| # | Source · case | Idea | BL23 |
|---|---|---|---|
| G32 | Queen, *The Egyptian Cross Mystery* (1932) | Headless bodies; the supposed victim is the killer, who left another man's body. | Limited: Yusti's roll call always knows who is missing. We use it for *when and where* someone was, not who died |
| G33 | Shimada, *The Tokyo Zodiac Murders* (1981) | Bodies cut and redistributed so the count and identities of the dead mislead; the culprit is among "the dead". | Principle only: parts mislead place and time (D.Dismember.Scatter, PlaceConfusion) |
| G34 | Ayatsuji, *The Mill House Murders* (1988) | A master who always wears a mask; a burned, faceless body lets another take his place. | Faceless bodies identified by marks (MS-02 marks: gold tooth, prosthetic hand) |
| G35 | Kindaichi, 飛騨からくり屋敷殺人事件 | Beheading used to swap identities. | As G32 |
| G36 | Kindaichi, 蝋人形城殺人事件 | The killer hides in plain sight as one of the wax figures. | T.HideAmongFigures (new): dress forms, statues, costume racks |
| G37 | Kindaichi, 異人館ホテル殺人事件 | A twin is exposed by a small habit: how a spoon is held. | MS-02 marks: handedness and gestures (`Cast.LeftHanded`) |
| G38 | Christie, *A Murder Is Announced* (1950) | The hostess has lived for years under her dead sister's name; the murders protect that. | Hidden knots K1–K5 (CharacterBible §3.3) as motives |
| G39 | Christie, *Murder in Mesopotamia* (1936) | The husband is the wife's supposedly dead first husband under a new name. | Identity *reveals* live in knots and bonds, not in mechanics |
| G40 | *Umineko no Naku Koro ni* (2007–10) | Several servants who are never seen in the same place are one person. | Not adoptable (18 fixed people plus roll call). Lesson: "never seen together" is a strong query → 동선 grid |
| G41 | Kindaichi (personas: the Phantom, the Snow Yasha, the Headless Samurai, the Red-Bearded Santa) | The killer wears a legend so witnesses report the legend, not the person. | T.Disguise + house-legend names (MS-09) |

### 1.8 The vanishing weapon or body
| # | Source · case | Idea | BL23 |
|---|---|---|---|
| G42 | Dahl, *Lamb to the Slaughter* (1953) | A frozen joint is the weapon, then cooked and served. | T.EatTheWeapon (F). Famous: supporting trick only, never the aha |
| G43 | Carr's lecture / genre | The icicle weapon that melts. | T.IceWeapon (Icicle in ColdStorage) |
| G44 | Poe, *The Purloined Letter* (1844) | Hidden in plain sight among ordinary things. | D.PlainSight, T.WeaponReturn |
| G45 | Chesterton, *The Sign of the Broken Sword* (1911) | Hide a body in a field of bodies: make many of the thing you must hide. | A weapon among an identical set; mitate hides one target in a pattern |
| G46 | Christie, *4.50 from Paddington* (1957) | A body thrown from a train at a bend lands where the killer can collect it later. | MS-01 laundry chute; D.Drop.Void |
| G47 | Kindaichi, 魔術列車殺人事件 | A stage illusion is the murder method. | Theater machinery (F fixtures) + a magician's cabinet prop (x) |
| G48 | Kindaichi, 墓場島殺人事件 | Overwhelming gore hides the real cause of death. | T.SecondWound; gore tiers keep identical clues (F §D5) |

### 1.9 Remote and delayed mechanisms
| # | Source · case | Idea | BL23 |
|---|---|---|---|
| G49 | Shimada, *Murder in the Crooked Mansion* (1982) | The tilted house was built so a knife slides from the owner's tower along a straight line of stairs and vents into a sealed room, while he talks to a detective. | T.ShaftDelivery (new): MS-01 vent shaft + MS-06 rig + R1 |
| G50 | Christie, *Murder in Mesopotamia* | A heavy stone dropped from the roof when a face lures the victim to lean out. | M.Crush.Drop from the gallery over a Void, lured by a sound or light |
| G51 | Detective Conan, the roller-coaster case | A cord looped on the victim in a dark tunnel; the ride does the killing while the culprit sits in plain view. | Rigs on moving fixtures (fly bar, chandelier winch, dumbwaiter) |
| G52 | Higashino, the Detective Galileo stories | Each case is a physical effect at a distance, solved by an experiment. | The player's 재현 (F A10 `WhatIf`) as a core detective verb |
| G53 | Genre | Candle, incense or ice timers; an alarm or a record's end as the trigger. | T.CandleTimer, T.IceTimer, R8 timers |

### 1.10 Architecture and space
| # | Source · case | Idea | BL23 |
|---|---|---|---|
| G54 | Shimada, *Crooked Mansion* | The house itself is the weapon. Snow knocked off a roof erases footprints. | Fixtures chosen by the culprit; "pristine media" and erasers (x) |
| G55 | Ayatsuji, *The Labyrinth House* (1988) | A maze of look-alike rooms named after myths; whoever knows the maze has power. | Twin rooms and mirror wings (MS-01); T.RoomSwap |
| G56 | Genre (split levels, mirrored wings) | A witness counts floors or turns wrongly; "the room above" is not the one they think. | Mirror wing + place beliefs (MS-02); seams: palette, clock offset |
| G57 | Carr, Shimada, genre | Untracked snow, sand or dust: "no footprints" as the impossibility. | Our media: dust, flour (Kitchen), wet deck (Pool), ash (Incinerator), RainCorridor. Erasers: mop, rain frames |
| G58 | Ayatsuji, *The Clock House Murders* | The building's own clocks are the trick (G22). | ClockMuseum, where "no two clocks agree" is already a published rule |

### 1.11 Psychological tricks
| # | Source · case | Idea | BL23 |
|---|---|---|---|
| G59 | Chesterton, *The Invisible Man* (1911) | The postman is so expected that no witness counts him. | MS-02 salience: chores and routines go unreported unless asked |
| G60 | Queen, *The Chinese Orange Mystery* (1934) | Everything in the room is turned backwards to hide the one thing that was backwards. | T.NoiseOfDisorder (new): wreck a room to bury one telling anomaly |
| G61 | Christie, *The ABC Murders* (1936) | One real target hidden in a serial pattern, with a suggestible patsy. | MS-09 mitate motive "hide one target" |
| G62 | Queen, *Ten Days' Wonder* (1948); Norizuki's essays on the "late-Queen problem" | The culprit feeds the detective planted clues, so correct reasoning names the wrong person. | MS-04 decoy solution; it is the design basis for the 5 fakes |
| G63 | Queen, *The Tragedy of Y* (1932) | The killer followed someone else's written plan; odd details fit the plan's author, not the doer. | T.ScriptFollower (new) + the mousetrap playbill (DailyLife §7.6) |
| G64 | Christie, *Styles* | The culprit invites early suspicion on a flimsy case that will collapse, buying immunity. | T.SelfClear (new): cleared in Act I, and reopening it is a reversal |
| G65 | Christie, *Peril at End House* (1932) | The "target" of repeated attempts is the culprit. | T.FakeAttempt (new) |
| G66 | Higashino, *Malice* (1996) | The culprit's written account manufactures a false motive. | T.Forgery aimed at *why* |
| G67 | Norizuki, *For Yoriko* (1990) | A written confession is itself the deception, shielding someone else. | Protector's false confession → reversal R3 |
| G68 | Kindaichi, 飛騨からくり屋敷殺人事件 | A decoy escape route tailored to a scapegoat's past skill (an ex-acrobat) fails because he fears heights. | MS-04 DecoyDef + CastTraits `CannotDo` breaker |
| G69 | Shimada's "奇想" principle; Carr, *The Hollow Man* | The opening riddle should be spectacular and seem supernatural; the solution is plainly physical. | L1 must be *engraved* as a named mystery (§4 SF1) |

### 1.12 Accidental factors
| # | Source · case | Idea | BL23 |
|---|---|---|---|
| G70 | Christie, *The Hollow* (1946) | A third person takes the gun from the killer's hand and later swaps guns to protect her. | Protector role (F §C4) + MS-07 |
| G71 | Kindaichi, 魔神遺跡殺人事件 | A death that was an accident with a falling object is folded into a murder series. | Truth is always a person: an innocent's capped accident that is misread, or finished by the culprit (T.TwoStage) |
| G72 | Leroux (G08) | The victim's own movements after the attack create the puzzle. | VictimAct records (MS-07) |
| G73 | Genre | An innocent tidies the scene before it is found. | MS-07 (은결's 09:12 tidy route) |
| G74 | Genre | A trap kills the wrong person. | MistakenVictim (F §C4) |

### 1.13 Serial patterns and mitate (見立て)
| # | Source · case | Idea | BL23 |
|---|---|---|---|
| G75 | Christie, *And Then There Were None* (1939) | Deaths follow a nursery rhyme; the one who "died" early is the killer. | MS-09 house verse |
| G76 | Van Dine, *The Bishop Murder Case* (1929) | Killings staged after nursery rhymes and signed with a persona. | Persona name from lore |
| G77 | Yokomizo, *Gokumon-tō* (1947; Kindaichi Kōsuke is the manga hero's grandfather) | Three sisters killed as haiku images. The staging carries a dead man's wish. | Motive for mitate: a wish, a meaning |
| G78 | Ayatsuji, *The Kirigoe Mansion Murders* (1990) | Killings echo a poem. | House verses (MS-09) |
| G79 | Kindaichi: the tarot villa, the opera house, the legend cases | Each case wears a card, an opera or a local legend. | Case "legend names" |
| G80 | Conan, "Moonlight Sonata" (G23) | A piece of music is the pattern and the clock. | Gramophone and music-room staging |
| G81 | *Umineko* | Deaths follow an epitaph; solving the epitaph is a separate goal. | The verse may also hide a lore payoff |

### 1.14 The dying message
| # | Source · case | Idea | BL23 |
|---|---|---|---|
| G82 | Queen, *The Tragedy of X* (1932) | Crossed fingers point to a role (a conductor's X-shaped punch), not a name. | Encodings by role or object (MS-10) |
| G83 | Queen, *The Siamese Twin Mystery* (1933) | Torn playing cards in dead hands are first read the wrong way. | Messages that mislead; real vs forged (T.FakeMessage) |
| G84 | Conan (recurring) | Pun and cipher messages, e.g. a code in a score. | House cipher vocabulary (door emblems, roster colours) |
| G85 | Kindaichi, 黒死蝶殺人事件 (a common reader complaint) | The key needs specialist outside knowledge (a butterfly species). | **Negative example.** Every key must be learnable in the game |
| G86 | Genre | The culprit finds and alters the message. | Seams: two media, wrong hand, written after death |

### 1.15 "The culprit is the one we trusted"
| # | Source · case | Idea | BL23 |
|---|---|---|---|
| G87 | Christie, *Roger Ackroyd* | The confidant who helps the detective. | The trusted helper during the investigation |
| G88 | Christie, *And Then There Were None* | The judge who "dies" early. | T.FakeAttempt / T.SelfClear |
| G89 | Christie, *Curtain* (1975) | The detective himself kills, to stop a manipulator. | Not for 민혁 (H9). Available to the house's "detective" residents (가온, 진우, 예담) |
| G90 | Higashino, *The Devotion of Suspect X* (2005) | The kind neighbour masterminds the cover-up. In the book a second body fixes the wrong date; we reject that part. | Protector as planner of the cover-up (after the fact only, H9) |
| G91 | Zangwill (G07) | The famous detective who breaks the door. | T.FirstIn |
| G92 | Kindaichi, 異人館ホテル殺人事件 | A police officer turns out to be the persona. | The expert everyone relies on (라온's ear, 은결's body exam, 해린's mechanics) |
| G93 | Danganronpa (structure only) | Sometimes the ally closest to the protagonist is the culprit. | Drama bonus for a high-bond culprit, ≤1 per loop |
| G94 | *Umineko* | The game's host is also a player. | The victim's own scheme (SchemeDef) |

### 1.16 What each source teaches the system

| Source | The transferable lesson | Where it lands |
|---|---|---|
| Kindaichi | A named persona or legend; airtight alibis for everyone; the gathering; a tragic backstory; a small personal decisive clue | §3 flow; mysteries; confession |
| Detective Conan | Short physical demonstrations with string and tape; quick pace | 3D re-enactment ≤20 s |
| Carr | A taxonomy of impossibility; every door has a gap | §1.0 map; DoorExt extensions |
| Queen | Elimination logic; dying messages; the planted-clue problem | Name-plate elimination (§3.10); MS-04; MS-10 |
| Christie | Everyone has a secret, and the trial airs several; false solutions come first; recordings and doubles | Side secrets (spare or expose); L1/L2 |
| Shimada | Spectacular first impression; the house as weapon | Mysteries; MS-01/MS-06 |
| Ayatsuji | Published house grammar; the building's own clocks | MS-01 rules; T.ClockRate |
| Higashino | Physics solved by experiment; long fuses; the protector mastermind | 재현; T.LongFuse |
| Arisugawa (the Egami novels: *The Moonlight Game*, *The Solitary Island Puzzle*, *The Double-Headed Devil*) | The culprit is fixed by eliminating everyone who fails small physical conditions (who could carry, reach, know) | Constraint plaques (§3.10) |
| Norizuki | Clues can be planted for the detective; a confession can be a lie | 5 fakes; protector confession |
| Umineko | Some statements are guaranteed true, and the player builds hypotheses against them | House seal on records (SF11) |
| Danganronpa (structure only) | Issue → focused challenge → consensus shift; a mid-trial twist; the culprit's last stand; a recap before the vote | §3.2 (structure only) |

---

## 2. Systems needed to generate these tricks

### 2.0 What we already have (so the gap is small where it matters)
- **E (code today).**
  - `Perception.See`: light-scaled range and identity confidence, disguise factor, and the IG06 borrowed-coat misread.
  - Sightings with Held/Bloody/Carrying.
  - `Testimony`: structured lies (alibi lie, protector omission, fabricated sighting, scapegoat steering) and caught-lie detection.
  - `ClockOffset` for watchless witnesses (D-012); door loggers covering one door (IG04); loans ledger (IG01/IG06).
  - Recorder playback (IG03); night locks (`House`); `RecallRelevant`; witness sheets.
  - The clockwork bird: `BirdCage` furniture plus lore; 해린's bond scene teaches that it mimics speech.
  - `CastTraits` data: lenses, fixed-time habits, quirks, tells.
- **F (MurderFoundation).**
  - Physics rules R1–R12: gravity, heat, fire, water, electricity, gas, toxins, links and timers, contact, body, sound, time sources.
  - Fixtures (chandelier, bell, trapdoor, fly bar, rails, hatch); DoorExt (bolt, key, wedge, gap, keyhole, transom, tape).
  - Traces and seams; `WhatIf` experiments; styles and skills; the opportunity index (victim routine windows).
  - 44 trick templates; CaseFile L1/L2/L3; FairPlay paths; key clues; foreshadow beats; schemes; roles; 16 archetypes.

The genre needs four more things that no plan covers:
1. **What people believe, and why.**
2. **How the house can be moved through.**
3. **How a culprit authors a false story for other people.**
4. **What the scene looked like at the moment of discovery.**

Six smaller systems follow.

### 2.1 Per category: the systems each needs
Columns: need · kind (Prim = physical primitive, Arch = architecture, Know = knowledge and perception, NPC = behaviour, Prop = props with states, Trace = traces) · status.

**2.1.1 Locked rooms (G01–G15)**
| Need | Kind | Status |
|---|---|---|
| Door model: bolt, key side, wedge, gap class, keyhole, transom, tape | Prop | F §A3 DoorExt |
| Thread or line through a gap or keyhole; pull transitions (bolt shoots, key turns) | Prim | F R8 Links |
| Ice melt and candle burn timers | Prim | F R2/R8 |
| Magnet through a thin door; hinge-pin lift; knob-spindle gap | Prim/Prop | x (§2.3) |
| Night lock log; door logger that watches one door | Know | E (`House`, IG04) |
| Who holds which keys (roster keys, loans) | Know | E (items, loans) |
| Victim bolts his own door (fear lock-in, bedtime) | NPC | E (`SafetyActivity`, sleep lock) + F T.SelfLocked |
| Entry order, first touches, state at discovery | Know | **M MS-05** |
| Secret passage, pivot panel, service shaft, published grammar | Arch | **M MS-01** |
| Seams: fibre, pin hole, key scrape, wax drip, puddle ring, splinter, tape residue | Trace | F §A8 |
| "Does the thread fit under this door?" | Know | F A10 `WhatIf` |

**2.1.2 Alibis (G16–G31)**
| Need | Kind | Status |
|---|---|---|
| Cooling, rigor, livor in closed form; heat and cold dwell | Prim | F R2/R10 |
| Dose onset and lethal clocks; delayed and precipitated poison | Prim | F R7 |
| Wall clocks with offsets; watches; electric clocks stop; stopped watches | Prop | E (`ClockOffset`, `HasWatch`) + F R12 |
| A clock whose *rate* is altered; a tower clock that strikes | Prop | x |
| Bells (chapel, departure) | Prop | F fixtures + T.BellAnchor |
| Recorder, gramophone, record end; the clockwork bird as a recorder | Prop | E (IG03, BirdCage) + F T.RecordedVoice; bird rules x |
| Hearing through the acoustic graph; masking | Know | E + F R11 |
| **Which time source each witness used; whose voice; live or replay** | Know | **M MS-02** |
| **Walking times; shortcut knowledge; alibi feasibility** | Know | **M MS-03** |
| Gathering attendance (Arrived/Left) | Know | E |
| Photos with a clock in frame | Prop | DailyLife §7.5 + F T.PhotoAlibi |
| **Culprit arranges an audience** (invites) | NPC | E (`Requests` invite) + **M MS-04** |
| Unwitting stand-in; staged injury | NPC | **M MS-04** + T.DoubleAtGathering, T.FakeInjury (new) |

**2.1.3 Identity (G32–G41)**
| Need | Kind | Status |
|---|---|---|
| Disguise items; borrowed-coat misread | Prop/Know | E (`Perception`) |
| **Cue-based identity: height, build, coat, hair, gait, hand, voice, marks** | Know | **M MS-02** (marks registry inside it) |
| Shoe-size prints, handedness, height tells (low lintels in WhiteDoors) | Trace | F (ActorPhys, T.HeightTell) |
| Roll call: Yusti always knows who is missing | Know | E/canon: limits body-swap tricks |
| Faceless or burnt bodies identified by marks (teeth, prosthetic, rings) | Trace | F R3 remnants + MS-02 marks |
| Hiding among figures (dress forms, statues, costumes) | Prop | x + F T.Dummy/T.HideInside |
| Voice imitation (재하), the Echo power | Know | E + MS-02 attribution |

**2.1.4 Vanishing weapon or body (G42–G48)**
| Need | Kind | Status |
|---|---|---|
| Burn, sink/float plus filter, bury, cold stash, wash, return to display, plain sight, compartments, void drop, eat, dismantle | Prim/Prop | F §B4 disposals |
| Melting ice weapon | Prim | F |
| Container fit and capacity; carts | Prop | F PhysProfile, T.LaundryCart |
| **Laundry chute, dumbwaiter, service shaft as transport** | Arch | **M MS-01** |
| **Innocent cleaning that moves the weapon** | NPC | **M MS-07** |
| Dismemberment with a motive | Prim | F §C4 + Gore |

**2.1.5 Remote and delayed mechanisms (G49–G53)**
| Need | Kind | Status |
|---|---|---|
| Links, pulleys, timers, fixtures; trap lifecycle; wrong victim | Prim | F R8, §0.4 |
| **Device chains with geometric validation** (heights, lengths, straight shafts) | Prim | **M MS-06** |
| Straight vent lines and shafts | Arch | **M MS-01** |
| Delayed dose planted in a routine object | Prim/NPC | F R7 + **M MS-08** |
| Lethality gate | — | F H5 |

**2.1.6 Architecture and space (G54–G58)**
| Need | Kind | Status |
|---|---|---|
| Suite doors, second doors, door loggers | Arch | E/F |
| Chandeliers, stage machinery, gallery rails, bell, elevator and hatch | Arch | F fixtures |
| **Twin rooms, mirror wings, secret routes** | Arch | **M MS-01** (F T.RoomSwap assumes twin rooms exist) |
| Clock tower face as a public time anchor | Arch | x |
| Untracked media and erasers | Trace | F R9 patches/Dusty + x "pristine" |
| Disorientation (dark, mirrored halls) | Know | **M MS-02** place beliefs |

**2.1.7 Psychological (G59–G69)**
| Need | Kind | Status |
|---|---|---|
| Lies, omissions, scapegoat steering | NPC | E (`Testimony`) |
| **Decoy solutions, arranged witnesses, self-clear, fake attempts, script-followers** | NPC | **M MS-04** |
| **Salience and expectation** (routine goes unreported; schema confabulation) | Know | **M MS-02** |
| Hearsay chains, copies counted as one root | Know | E (Copy cards) + F §D3 |
| Reconnaissance questions (who asked about whose habit) | Know | DailyLife §7.3 (planned) |
| Foreshadow beats and watchers | Know | F §C4 + DailyLife §7 |
| Layers L1/L2/L3 | — | F §C4/C5 |

**2.1.8 Accidental factors (G70–G74)**
| Need | Kind | Status |
|---|---|---|
| Non-lethal capped hazards | Prim | F H5 |
| **Innocent disturbances and victim acts, recorded and explainable** | NPC | **M MS-07** |
| Protector, second actor, wrong victim, improvisation | NPC | F §B2/§C4 |

**2.1.9 Mitate and serial patterns (G75–G81)**
| Need | Kind | Status |
|---|---|---|
| Ritual staging | Prop | F G.Ritual |
| **House verses: stanza signatures, next-stanza prediction, persona names** | Know/Prop | **M MS-09** |
| Double events (Ch3+) | NPC | F |

**2.1.10 Dying message (G82–G86)**
| Need | Kind | Status |
|---|---|---|
| Fake message and its seams | Trace | F G.Message, T.FakeMessage (E: Message trick) |
| **Real messages: conscious window, fear of the culprit, a learnable house cipher** | Know/Trace | **M MS-10** |

**2.1.11 The one we trusted (G87–G94)**
| Need | Kind | Status |
|---|---|---|
| First-in killer, discovery choreography | NPC/Know | F T.FirstIn + **M MS-05** |
| Protector's false confession | NPC | F Protector + **M MS-04** |
| Expert testimony that carries extra public weight (and so can lie powerfully) | Know | **M MS-02/MS-04** |
| High-bond culprit (≤1 per loop) | — | x: a Drama term in the composer score (F §B2) |

### 2.2 The ten missing systems

Priorities: **P0** needed for the vertical-slice chapter and the new 심판; **P1** needed for variety across chapters; **P2** for Ch3+ spectacle.
Sizes: **S** <400 lines, about 1 implementer-day; **M** 400–1,500, 2–4 days; **L** 1,500–4,000, 1–2 weeks.

#### MS-01 House Secrets — architecture the generator can hide · P1 · L
Service routes and hidden features that obey a *published* grammar, so the player can reason about them.

**Data**
```csharp
public enum SecretKind { ServiceStair, Passage, Dumbwaiter, LaundryChute, PivotPanel, SpeakingTube, VentShaft, TwinRoom, MirrorWing, TowerClockFace } // append-only
public sealed class HouseSecret {                 // GameState.Phys.Secrets, sorted by Id (Ordinal)
  public string Id; public SecretKind Kind; public bool Public;   // dumbwaiter, chute, service stair = public (on the map)
  public int RoomA = -1, RoomB = -1; public P3 MouthA, MouthB;     // endpoints; TwinRoom/MirrorWing use RoomB as the twin
  public int FitClass;                                             // Pocket..Body (PhysProfile SizeClass)
  public float TravelMin;                                          // closed-form traverse time
  public string TellKey;                                           // LineBank key of the published tell (draft, scrape arc, echo)
  public List<string> KnownBy = new List<string>();                // sorted actor ids
  public double DustDisturbedAt = -1; public string DustBy;        // the seam inside the passage
}
```

**Rules**
- *Placement.* `HouseSecrets.Build(layout, loop)` runs after `LayoutGenerator` and never touches the layout RNG. It uses a counter hash `Hash32("secret", loop, roomId)` (numeric, deterministic).
  - Public features go where geometry already allows them:
    - a dumbwaiter where the Kitchen stacks over a Dining or Landing footprint;
    - a laundry chute where an upper Bedroom corridor stacks over the Laundry;
    - a service stair beside the Stairwell.
  - Secret kinds use shared walls of rooms that have no door between them, on the same floor.
  - Twin rooms are pairs of GuestRooms with mirrored decor.
  - Mirror wings are symmetric wings of Cross or Spine skeletons.
- *Grammar (published house rules, taught by the existing lore book 『저택의 문법』).*
  - "숨은 길은 언제나 벽난로나 책장 뒤에서 시작하고, 같은 층의 두 방만 잇는다."
  - "음식 승강기는 부엌에서만 움직인다."
  - "빨래 통로는 아래로만 간다."
  - Budget: at most **one** secret passage or pivot active per chapter (Knox 3, G15).
- *Movement.* Nav gains edges that only actors in `KnownBy` may use (public features: everyone). `TravelMin` feeds MS-03.
- *Learning a secret.* Read the grammar (you learn *where they can be*); notice the tell (a candle flame leans in a draft, a scrape arc on the floor, a voice from the wall); or see someone use it. Planners use only their own `KnownBy` (H6).
- *Capacity.*
  - Dumbwaiter: Pocket to Arm only.
  - Laundry chute: cloth bundles, Arm-size items, severed parts.
  - Body transport needs a service stair plus a cart.
- *Sound.* Speaking tubes and vents add acoustic edges (R11), so a voice can "come from" a room it was not spoken in.

**Seams**
- Dust and prints inside the passage; a broken cobweb; soot at a fireplace mouth.
- The dumbwaiter car standing at the wrong floor (its position is logged).
- Lint, fibre or a blood smear inside the chute.
- The scrape arc of a pivot panel.

**Tricks enabled:** T.SecretRoute, T.ShaftDelivery, T.PivotRoom, T.RoomSwap (real twins), T.VoiceThroughWall, chute disposal.

**Fair play:** a secret can be a path root only if its tell has visibility ≤2 and the grammar book is readable from day 1.

**Owners:** kernel implementer #1 (Physics/Fixtures), the environment agent (panel, hatch, chute and dumbwaiter visuals), the rooms author.

#### MS-02 Belief provenance — what witnesses think they saw, and why · P0 · L
The heart of alibi, identity and psychological tricks, and the source of most "fake" witness cards.

**Data** (derived at query time; only small cue snapshots are stored)
```csharp
public enum Cue { Height, Build, Coat, Hair, Gait, Hand, Voice, Mark, Scent, Silhouette, Prop, Habit } // append-only
public enum AnchorKind { OwnWatch, WallClock, TowerStrike, Bell, Meal, MusicEnd, Guess }            // append-only
public enum Source { Saw, Heard, Told, Read, Inferred }                                              // append-only
public struct CueSnap { public Cue Cue; public string Value; public float Strength; }                // ≤4 per sighting
public struct TimeAnchor { public AnchorKind Kind; public int Source; public double OffsetMin; public double WidthMin; }
// appended to the existing Sighting (Knowledge.Sightings) and to heard-sound records:
//   CueSnap[] Cues; TimeAnchor Anchor; bool Salient; string VoiceOf; bool Replay;
public sealed class Belief {                      // built by Beliefs.Of(sim, holder, incident) and cached in the CaseFile
  public string Holder; public Prop Claim; public Source Src;   // Saw, Heard, Told(by), Read(record), Inferred
  public List<CueSnap> Cues; public TimeAnchor Anchor; public float Confidence; public string RootKey;
}
```

**Rules**
- *Identity by cues.*
  - When a sighting's `IdConf` is low (dark, far, disguised), the witness infers *who* from cues. Candidates come from `CastMarks`: a static registry of visible marks per resident, each with the range and light it needs. Examples:
    - 세나's white prosthetic hand;
    - 이현's gold tooth (close only);
    - 민서's digital-watch beep;
    - 시온's chain jingle;
    - 진우's candy crunch;
    - 채령's tape measure and left hand;
    - 재하's emerald scarf.
  - The winning candidate is the one whose marks best match the cues. Ties break by the `Habit` cue (MS-08) and then by a counter hash.
  - A trick falsifies some cues (the coat) but not others (height, hand). The mismatch **is the seam**: "코트는 가온 씨 것이었는데, 키가 더 작았다".
- *Expectation.* A sighting that matches a known routine at that place and hour gets its identity pulled toward the routine's owner (confabulation). Example: "그 시간엔 늘 도윤 씨가 액자를 바로잡으니까."
- *Salience.* A routine-consistent act (a chore, a known habit) is stored with `Salient=false`. It is not volunteered in testimony unless the right question is asked ("그때 복도를 닦던 사람은요?"). This is Chesterton's invisible man as a rule. The 동선 grid shows such cells as *unasked* (SF12).
- *Time anchors.*
  - Every statement carries its time source: the witness's own watch (`HasWatch`), a wall clock (existing `ClockOffset`), the hall clock striking, the chapel bell, a meal, a record ending.
  - Tampering a source (T.ClockSet, T.ClockRate, T.BellAnchor) shifts **every** belief anchored on it by the same amount.
  - The exposing path is two witnesses with different anchors disagreeing (민서's digital watch against the hall clock).
- *Sounds.* A heard event records `VoiceOf` as believed, the direction (room, or "through the wall" via MS-01), and `Replay` as perceived.
  - Replays have a tell detectable with the `Sound` skill: 라온 high, 재하 medium. Examples: the clockwork bird's closing click, a gramophone's hiss, a recorder's thin tail.
  - A trusted listener's judgement becomes a Belief with extra public weight. That is what makes an expert's *lie* strong (G92).
- *Hearsay* keeps its chain; copies collapse to one root (existing P0164 rule).
- *Place beliefs.* In the dark or in mirror wings a witness may place an event in the twin room. The belief stores the room they *think*, plus the cue they used (palette, wall clock, smell).

**Cost:** zero per tick. Cue snapshots are written only when a sighting opens (≤4 cues). Beliefs are computed when testimony or the CaseFile asks.

**Owners:** a new implementer (perception) or #3. It needs delimited blocks in `Perception.cs` and `Testimony.cs` (shared files) and a `CastMarks` table in `Sim/Data`.

#### MS-03 Route atlas and alibi windows · P0 · M
The generator must know when an alibi *looks* impossible and how it becomes possible.

**Data**
- `RouteAtlas`: derived, `[JsonIgnore]`, rebuilt at loop start and on load.
  - Public walking minutes between rooms: Dijkstra over the room graph (about 100 nodes), stairs costed.
  - A per-knower overlay for secret edges (MS-01), computed lazily.
- `AlibiWindow {actor, t0, t1, room, beliefs[]}`, drawn from MS-02 beliefs.

**Rules**
- `MinTravel(a, b, knowledge)`.
- `Feasible(actor, act, alibis)` = can the actor get from each alibi point to the act's room and back, plus the act's `StepDef` durations?
- The composer uses it two ways:
  - to find acts that look infeasible under public routes but are feasible through a route or timing the culprit knows (T.SecretRoute, T.SealedGathering);
  - to reject alibis that are trivially broken.

**Player side:**
- the 지도 tab shows walking times on hover ("식당 → 온실 · 걸어서 3분쯤");
- the 동선 grid marks impossible gaps.

**Seams:** route traces (wet prints on a service stair), passage dust, the dumbwaiter's position.

**Owners:** #2 (OpportunityIndex reads it) with #1 (Nav).

#### MS-04 Story authoring — decoys, arranged witnesses and immunity · P0 · M
The genre's best twists come from a culprit who plans what *others will conclude*. This is also the principled source of the deck's fakes.

**Data**
```csharp
public sealed class StoryPlan {                   // Mur.Stories; one per act
  public string Plan, Scapegoat; public List<Prop> TargetBeliefs;  // what the room must believe at L1 and L2
  public List<Arranged> Audience;                                   // {witness, place, time, what they must see or hear, invite LineKey}
  public string Decoy;                                              // DecoyDef id or null
  public string Immunity;                                           // null | SelfClear | FakeAttempt
}
public sealed class DecoyDef {                    // registry: Catalog/Decoys_*.cs
  public string Id; public string[] Requires;                       // room, fixture, item tags
  public SetupStep[] Plant;                                         // ≤3 steps that plant the decoy's own seams (visibility 0–1: meant to be found)
  public ClaimTemplate Points;                                      // "only someone who can X could have done it"
  public string BrokenByTrait;                                      // a CastTraits.CannotDo key the scapegoat has
}
// CastTraits gains: public string[] CannotDo; public string[] CannotDoProof;  // e.g. "grip-fine-thread" : 세나 (prosthetic right hand)
```
Example `CannotDo` entries:
- "climb-rope" (fear of heights, or a body too heavy for the gallery rope);
- "fit-hatch" (민서, 준서);
- "tie-right-hand-knot" (채령, left-handed);
- "taste-bitter" (준서, lost taste);
- "hear-left" (라온, one earphone).

**Rules**
- *Decoys.* The composer may attach one decoy whose `Points` claim singles out the scapegoat's skill. The decoy's seams are deliberately findable, and it becomes the L2 story. The truth breaks it through the scapegoat's public `CannotDo`, which must have been observable in daily life (card night, hangouts, bible gestures).
- *Arranged witnesses.* Through the existing `Requests` invite ("밤 11시에 누룽지 좀 끓여 줄래요?", DailyLife §3.7 alibi request), the culprit places a witness to see or hear a staged fact. The witness becomes an unwitting Helper (allowed by H9).
- *Immunity variants.*
  - **SelfClear:** the culprit leaves a flimsy case against themselves that collapses in Act I.
  - **FakeAttempt:** a staged attempt on the culprit's own life, with Sev ≤1 and a real seam.
- *Script-follower (G63).* The act copies a written plan by someone else (the mousetrap playbill, the victim's own scheme notes). The planner inherits the plan's quirks, so the L2 suspect is the plan's author.

**Scoring:** the Drama term in the composer rewards a StoryPlan whose L2 suspect has a public `CannotDo` breaker. That guarantees a fair reversal.

**Owners:** #3 (tricks) + the case-drama and planner authors. `CannotDo` goes into the cast-voice `CastTraits` file.

#### MS-05 Discovery choreography and scene snapshots · P0 · M
Many tricks act *during* the discovery, and the trial deck needs photos of the scene as found.

**Data**
```csharp
public sealed class Discovery {                   // Mur.Discoveries, one per incident (~3 KB)
  public string Incident, Finder; public double T0;
  public List<Entry> EntryOrder;                  // {actor, clock, door}
  public List<Touch> FirstTouches;                // {actor, thing, clock, what changed}
  public SceneSnap Snap;                          // at the first sighting
  public SceneSnap PlayerSnap;                    // at the player's first view, if different
}
public sealed class SceneSnap { public int Room; public List<ItemPose> Items; public List<DoorPose> Doors;
                                public BodyPose Body; public float Light; public List<ClockHands> Clocks; }
```

**Rules**
- Capture at `Cases.OnBodySeen`, the first sighting: a hook line.
- Record every entry and touch until the House seals the scene. Tricks that act in that window (T.KeyReturn plant, T.FirstIn, T.HideInside, T.HideAmongFigures) show up as touches *after* the snapshot.
- Exposing path: the diff between snapshot and now ("발견 때 열쇠는 문에서 한 걸음 안쪽이었다. 지금은 문턱 바로 앞이다").
- **House scene preservation.** Once confirmed (`HouseConfirm`), items in the incident room stay kinematic and traces stop ageing for investigation purposes. This is already partly in F §A9.

**Game side:** the snapshot is the source for the deck's in-world photos (trial-reforge DECK: `CineShot` 'Thing' framing rendered from the snapshot, not the current state).

**Owners:** #3 + trial-reforge DECK; one delimited line in `Cases.cs`.

#### MS-06 Rig composer — device chains with geometry · P1 · M–L
F R8 has links and timers. The genre's showpieces are *chains*: a candle burns a thread that releases a weight that pulls a bolt; a knife slides down an aligned shaft.

**Data**
```csharp
public enum RigNodeKind { Anchor, Link, Pulley, Gap, Timer, Trigger, Weight, Release, Slide, Effect } // append-only
public sealed class RigNode { public RigNodeKind Kind; public string Where; public string Param; }     // Where = fixture/furniture/door/secret id
public sealed class RigDef { public string Id; public RigNode[] Slots; public string[] Requires; public string EffectMechanism; } // ≤5 nodes
```

**Rules**
- Validation is closed-form via `PhysApi.WhatIf`: support heights, rope lengths against distances, gap classes, straight-line alignment through `VentShaft` and `Void`, timer durations from published constants.
- Each node is a registered `StepDef` with a motion and a seam.
- The chain has ≤5 physical steps (FP08).
- The same nodes drive the 3D re-enactment (§3.10), so what the court sees is what the kernel did.

**Showpieces enabled:**
- the chandelier dropped by a cut wire (G25);
- the shaft knife (G49);
- the stone from the gallery (G50);
- the late crash (G09);
- candle → thread → bolt;
- ice → weight → latch;
- record end → needle arm → bell.

**Owners:** #3 (TrickApi) + #1 (WhatIf geometry).

#### MS-07 Disturbances and victim acts — accidents made fair · P1 · M
Innocent people move things, and victims act after being hurt. Unrecorded, this is noise that breaks fair play. Recorded, it is the best kind of red herring, because it has an honest explanation.

**Data**
- `Disturbance {id, actor, thing, from, to, clock, reason (Tidy|Borrow|Return|Clean|Curious|Fear|Protect), innocent, seenBy[]}`, capped at 64 per loop and rolling.
- `VictimAct {victim, act (LockSelf|Flee|Message|Hide|CallOut|Scheme), clock}`.

**Rules**
- Routines that touch case-relevant things write disturbances. Case-relevant means weapons, keys, thread, dishes, cups, clocks. Sources:
  - 은결's tidy route;
  - 태겸's borrow and return;
  - 준서's dishwashing;
  - 민서's chores;
  - 진우's wrapper trail.
- `FairPlay.Verify` counts a disturbance that erases a path as an unplanned loss (a lab metric, never repaired).
- A disturbance that creates a misleading state is eligible as a deck **fake of origin "우연"**. The actor explains it truthfully when asked.
- At most one innocent disturbance per deck.

**Owners:** #2 (Steps) + the daily-life routines owners.

#### MS-08 Habit registry — what can be exploited and who knew · P1 · S–M
**Data**
- `HabitDef {id, owner, kind, place, window or trigger, object, reliability 0..1, learnedBy (TableTalk|Hangout|Observed|Rumour)}`, seeded from `CastTraits.Fixed` and CharacterBible:
  - 은결's 09:12 walk;
  - 진우 looks at a clock at 23:40;
  - 민서's thermos of barley tea;
  - 이현 floats in the pool at night;
  - 도윤 straightens frames at 23:00;
  - 해린 checks the breakers.
- `HabitKnowledge`: the `HabitShared` ledger (DailyLife §7.4).

**Rules**
- LifeAI performs habits, jittered by reliability through a counter hash, so they really happen.
- The opportunity index (F §B2) uses only habits the planner knows.
- Witness expectation (MS-02) uses known habits.
- One exposing path is always available: "who knew the habit" ("민서 씨 보온병 이야기를 들은 사람: 둘째 날 아침 식탁 · 이현, 수아, 도윤, 시온").

**Owners:** the daily-life workflow (`Sim/Life`) + #2 reads it.

#### MS-09 Mitate engine — the house's verses · P2 · M (plus writing)
**Data**
- `VerseDef {id, title, stanzas[]: {image, placeTags, objectTags, pose, timeHint, LineKey}}`.
- `MitateRun {plan, verse, stanza, placed signature, deviations}`.
- Verses are original house lore, found in the Chapel, the Library and the mousetrap playbill. Example, written for this study:

  > 「손님들의 자장가」
  > 첫째 손님은 물에 들고 — 거울이 대신 숨을 쉰다
  > 둘째 손님은 불 곁에 눕고 — 재가 이름을 부른다
  > 셋째 손님은 종 아래 서고 — 종이 두 번 운다
  > 넷째 손님은 실에 묶이고 — 문이 스스로 잠긴다
  > 다섯째 손님은 시계를 안고 — 바늘이 뒤로 걷는다

**Rules**
- Chosen by Theatrical style or by a Misdirection motive. It needs a reason:
  - **hide one target in a pattern** (G61): Ch3+ double events only;
  - **point at the lore-keeper**: 재하 and 예담 know the verse;
  - **supernatural L1**: "저택이 데려갔다";
  - **the victim's own prank** used the verse.
- *Seam:* the culprit stages from the copy they read, so deviations from the true text say *which copy*. That is a culprit link in the style of G63.
- *Proactive goal.* After two stanzas the player can predict the next image and guard it. Guarding never needs a lethal verb, and H5 still gates every death.

**Owners:** the case-drama author + #3. Writing is heavy; verses are LineBank content.

#### MS-10 Dying message · P2 · M
**Data**
- `MessageDef {medium (blood|wax|ink|lipstick|chalk|tape measure|candy wrapper|the object grabbed), encoding (Initial|RoleSymbol|RoomEmblem|SeatNumber|RosterColour|ObjectPointer), keyLore}`.
- `DyingMessageRun {victim, clock, consciousWindow, encoding, target, alteredBy?}`.

**Rules**
- A victim writes only if R10 gives a conscious window of at least N minutes while alone.
- They encode *indirectly* when they fear the culprit will return (Fear relation), using the **house cipher vocabulary** that daily life teaches:
  - each bedroom door carries an emblem shown on the map legend;
  - 서윤's roster gives everyone a colour;
  - the dining seats are numbered.
- The culprit may alter the message. Seams: two media, a wrong-hand stroke, a line drawn after death (dry edges).
- **No outside knowledge** (G85). `keyLore` must name an in-game source readable by day 2.

**Owners:** #3 + the tricks-cause author.

### 2.3 Small extensions (not systems)
| Extension | Where | Enables |
|---|---|---|
| `Clock.RatePct` for mechanical clocks. Hands = t0 + (t−t0)·rate. A `Regulate` step at the clock needs the WindingKey | F R12 | T.ClockRate (G22) |
| Magnet agent; ferrous bolt profile; pull through a door ≤5 cm thick | F R8, Profiles | G05 |
| `DoorExt.HingePins {in, out}`; lift, remove and replace the leaf | F DoorExt | T.HingeLift (Carr) |
| Gap class "spindle" (removable knob) | F DoorExt | T.JudasGap (G02) |
| `Patch.Pristine` for dust, flour and ash; erasers: a Mop step, RainCorridor frames, ash settling | F R9 | Untracked-media impossibility (G57) |
| `TowerClock` fixture: the GrandHall face strikes the hours and is set from the winding room | F fixtures | Public time anchor (MS-02) |
| **Clockwork bird rules.** It keeps the loudest phrase heard since it was wound, and sings it at the next hour strike. A quiet phrase goes to a second cylinder and plays only when its tail is pressed. Every song ends with a click. Published in a Library manual, 『태엽 새 다루는 법』 | BirdCage (E) + R8/R11 | T.RecordedVoice with a gothic hinge (§3.16) |
| Furniture tag "figure" (dress forms, statues, armour); an actor standing still among figures has `IdConf` ×0.3 | Perception | T.HideAmongFigures (G36) |
| Sightlines through mirror surfaces flip handedness | Perception | T.MirrorWitness (F) |
| Magician's cabinet prop on Stage | Theater | G47 |
| Drama term "trusted culprit": the player's bond stage ≥2; ≤1 per loop | Composer | G93 |

### 2.4 New trick templates proposed (content, S each unless noted)
| Id | Axis | Genre root | Setup, in our house | Seams | Deps |
|---|---|---|---|---|---|
| T.LateCrash | Time | G09 | Candle or ice timer drops a stacked tray or bookend in the locked room after the kill | Wax on the anchor; the stack's dust outline; landing too tidy | MS-06 |
| T.PrankCorpse | Time | G17 | The victim's prank (play dead to scare someone) is hijacked; the real kill is later | Prank props in the victim's room; livor too fresh for the "seen dead" hour | Schemes (F), MS-04 |
| T.LongFuse | Time | G19 | A dose set days earlier in a habitual object (thermos, tea tin) | Residue; who knew the habit | MS-08 |
| T.ClockRate | Time | G22 | A ClockMuseum or hall clock regulated fast over hours; watchless witnesses | Regulator position; anchors disagree | x |
| T.DoubleAtGathering | Identity | G29 | An unwitting stand-in (재하 "rehearsing" the culprit's walk in the culprit's coat) at a dim gathering | Cue mismatch; the stand-in's own account | MS-02, MS-04 |
| T.FakeInjury | Belief | G30 | A bandage and limp as the alibi of "couldn't have climbed" | No bruise under the bandage; the gait changes when unobserved | MS-04 |
| T.HideAmongFigures | Access | G36 | Stand among dress forms or statues during the discovery commotion, leave last | Entry order; a form moved; dust | MS-05, x |
| T.ShaftDelivery | Means/Access | G49 | A weapon slides along an aligned vent or shaft into a sealed room | Scratches along the shaft; alignment only from one room | MS-01, MS-06 · M |
| T.SecretRoute | Place/Time | G12, G28 | A service stair or passage shortens a "15-minute" alibi to 4 | Passage dust; route prints | MS-01, MS-03 · M |
| T.PivotRoom | Access | G13 | A pivot panel between two rooms | Scrape arc; draft | MS-01 · M |
| T.JudasGap | Access | G02 | Thread or needle through the knob-spindle hole | Spindle scratches; the knob refitted crooked | x |
| T.HingeLift | Access | Carr | Lift the hinge pins, remove and refit the door leaf | Fresh cracks at the pins; grease | x |
| T.NoiseOfDisorder | Belief | G60 | Wreck the room so the one telling disorder is lost | Wreck order: dust *under* the fallen items; one thing out of the pattern | — |
| T.ScriptFollower | Belief | G63 | Copy a written plan (playbill, the victim's scheme notes) | Details that fit the author, not the doer | MS-04 |
| T.SelfClear | Sequence | G64 | A flimsy planted case against oneself collapses early | The collapse was *designed*: seams too convenient | MS-04 |
| T.FakeAttempt | Belief | G65 | A staged attempt on one's own life (Sev ≤1) | The attempt's device could not have hit; the timing fits the culprit | MS-04 |
| T.DecoySolution | Identity/Belief | G62, G68 | A planted "only X could have" route or method | The scapegoat's public `CannotDo` | MS-04 · M |
| T.ArrangedWitness | Time/Belief | G17, G21 | Invite someone to see or hear the staged fact | The invite's oddly exact time (Requests log) | MS-04 |

### 2.5 The 15 tricks with the best fun per effort (gothic mansion, 18 residents)
Effort assumes MurderFoundation Phases 1–2 have landed.

| # | Trick | Genre root | Our dressing | Natural culprits (bible) | Hinge | Systems | Effort |
|---|---|---|---|---|---|---|---|
| 1 | Candle timer at the chandelier cleat | G25, G53 | Yusti lowers the chandeliers at dusk (published). A candle at the cleat drops it during a gathering, and the killer sits among the audience | 해린, 도윤 | Wax on a cleat where no candle belongs; burnt thread end | F | S |
| 2 | The clockwork bird's voice | G21, G24 | The house's mimic bird sings its memory at the hour strike | 라온, 재하, 해린 | The closing click; the manual's second cylinder | F + MS-02 + x | S–M |
| 3 | Thread-drawn bolt | G01, Carr | 채령's thread. The lore book 『자물쇠와 그 친구들』 already teaches the principle | 채령, 도윤 | Fibre on the knob; the spool count | F | S |
| 4 | The house locks the room for the killer | G10 | Night lock at 22:00 on the Pool, Dining, Kitchen and Theater | 서윤, 태겸 (they know the lock-up) | Lock log vs a wet trail from before 22:00 | F | S |
| 5 | Clock set or clock rate | G22, G27 | ClockMuseum, "no two clocks agree"; the hall clock strike | 해린, 태겸, 예담 | Anchors disagree: 민서's digital watch | F + MS-02 + x | S–M |
| 6 | Seen alive in the victim's coat | G17, G29, IG06 | A dim gallery at night; the borrowed coat | 재하, 채령, 수아 | "Coat right, height and hand wrong" | E/F + MS-02 | M |
| 7 | Cold or heat shift | G16 | Cold store with a spring door; incinerator warmth; pool water | 은결, 준서 | Condensation, drip trail, livor mismatch | F | S |
| 8 | Weapon washed and returned | G44 | Gallery pedestals, trophy wall | 도윤 (the too-tidy restorer), 태겸 | Dust outline mismatch; still cold | F | S |
| 9 | Self-locked victim, long fuse | G11, G19 | 민서's thermos, 도윤's unscented tea | 은결, 준서, 도윤 | Residue + "who knew the habit" | F + MS-08 | S–M |
| 10 | Staged fall with a second wound | Carr C1, G48 | The gallery rail over the Void; a stair tumble | 이현, 시온 (panic), 민서 | Push bruise vs tumble bruises; bled vs ooze | F | S |
| 11 | First in | G07 | The strongest breaks the door (민서); the first to kneel (은결) | 민서, 은결, 이현 | Entry order; fresh blood on a cuff | F + MS-05 | M |
| 12 | Decoy solution for a scapegoat | G62, G68 | A planted gallery-rope route "only an athlete could climb" | 진우, 이현, 가온 | The scapegoat's `CannotDo` (e.g. 세나's prosthetic grip) | MS-04 | M |
| 13 | The house's hidden way | G12–G15, G28, G49 | Service stair, dumbwaiter, chute on the map; the passage behind the fireplace is not | 태겸 (logistics), 해린, 서윤 | Passage dust; dumbwaiter car on the wrong floor | MS-01, MS-03, MS-06 | L |
| 14 | The victim's scheme turned back | G17 inverted, F example 2 | The victim's prank or trap reversed | 태겸, 진우 | The victim's own packet or props in their room | F schemes + MS-04 | M |
| 15 | The house verse (mitate) | G75–G81 | 「손님들의 자장가」 stanzas; "the house took him" | 예담, 재하, 진우 (Theatrical) | A stanza staged from the wrong copy | MS-09 | M |

**Famous set-pieces never to reproduce as the aha:**
- eating the weapon (G42);
- vanishing figurines;
- a card in a dead hand;
- the torso-count;
- a Phantom mask at an opera;
- the dictaphone behind a moved chair;
- a smashed watch as the only time clue (G20).

Each can appear only re-dressed and as a *supporting* trick.

---

## 3. The 심판 — gripping, not pedantic

### 3.1 How the genre stages the solution
**Kindaichi / Conan / Christie / Queen, as beats:**
1. **The gathering.** Everyone is brought to a meaningful place (often the first scene), and "the culprit is among you" is said aloud. The stakes are social: the culprit must stand there and listen.
2. **"The mystery of X."** Kindaichi lists the riddles, each framed as an impossibility ("the locked tower", "the phantom who vanished"), and solves them one at a time, *how* before *who*. Naming the riddles makes a long solution readable.
3. **Demonstration.** A short physical show with string, tape, a drawing or an assistant acting it out. Conan keeps each under a minute. The demonstration also narrows the field: who *could* do it.
4. **The culprit fights.** "증거 있어?" Alternative explanations, outrage, and alliances with the other suspects. The detective answers each with a second fact.
5. **The decisive evidence.** Usually small and personal: an object the culprit could not dispose of, a trace on their things, or a word only the culprit could know (Columbo's and Kindaichi's favourite).
6. **Confession with backstory.** A flashback to an old wound, usually revenge for someone destroyed by the victims' cruelty. The motive reframes the victim.
7. **Aftermath.** Grief, an attempt at self-destruction stopped (or not), and the detective's line that no reason justifies killing. The last panel is quiet.

Christie adds that **everyone has a secret**: the solution publicly clears (or exposes) each suspect in turn, so every character gets a payoff, and a plausible *false* solution is often presented first. Queen adds the **challenge to the reader** (all facts are on the table) and **elimination** (the culprit is the only one who satisfies every condition).

### 3.2 How interactive games pace deduction
| Game | Device | Lesson | Our adaptation | Not copied |
|---|---|---|---|---|
| Danganronpa (structure only) | Trial = a sequence of issues, each ending in a consensus shift; a mid-trial twist; the culprit steers early and makes a last stand; a full recap before the vote; the vote as a hard gate | Cadence: issue → focused challenge → visible shift. Every trial has a big reversal. The recap turns deduction into story | Acts with bells (§3.5); scheduled reversals from CaseFile layers; recap film | Every mechanic in D-028, the monokuma-style host, execution spectacle, pink/pop look |
| Ace Attorney | Testimony → cross-examination (press or present) → revised testimony; witness breakdowns; a penalty meter; the decisive evidence; partner hints; secrets gated by specific evidence. *The Great Ace Attorney*: jurors' conflicting reasons | Every line is a target, and a contradiction makes the witness *adapt*. Mistakes cost something limited. Breakdown is the catharsis | The culprit's typed counters (§3.8); composure candles; recoverable voice candle; spare or expose secrets | Catchphrases, the "objection" ritual, the penalty bar look |
| Umineko | Guaranteed truths (the red statements) vs the reader's hypotheses; a supernatural first impression the reader must explain | Guaranteed facts lower stress; the player *authors* hypotheses | House-sealed records (SF11); hypothesis re-enactment (§3.10) | Coloured-text duels |
| *Return of the Obra Dinn* / *The Case of the Golden Idol* | Fill-in deductions confirmed only in groups of three | No brute force, and partial progress is confirmed | Clock-chime triples in the recap (trial-fun) | — |
| *Paradise Killer* | Choose the order of questions; accuse anyone at any time | Agency over order | Mystery lanterns: the player picks which to open | — |

### 3.3 Diagnosis of our current 심판 (trial-fun audit, 12 headless trials)
- The same fixed pipeline runs in 12 of 12 trials (Opening > Cause > Time > Place > Suspicious > Culprit > Final > Vote).
- Twist beats: **0** in 12 of 12. The case's real trick is never staged for the player.
- Claims the player could contradict *when spoken*: **0**. Up to 2 rounds per trial are unwinnable by design.
- Decisions every 7–11 readable beats on average, with gaps of up to 32.
- The culprit is passive; the most frequent NPC line is "agree".
- Perfect play may not move the vote (seed 4242: smart and passive tallies are identical).

### 3.4 Design goals
1. A **named goal** at every moment ("열한 시의 외침 — 그 목소리는 누구의 것이었나").
2. A **decision at least every 4 readable beats**; "let it run" counts.
3. **At least one reversal per trial, and two from tier D2**, built from the CaseFile layers.
4. **A culprit who fights back**, with counters built from the true record.
5. **Show, don't recite.** The trick is demonstrated in 3D with the real actors, props and physics.
6. **Perfect play wins.** The vote follows the public case (trial-fun's Scale of Proof).
7. **No pedantry.** Settled facts become plaques and are not repeated. Agreement is a nod, not a line. No jargon, no timers by default.

### 3.5 The flow — acts and bells
Yusti rings the bell for each act and says only procedure. Total 30–45 minutes.

| Act | Purpose | Player decides | The culprit | Reversal | Set piece | Time |
|---|---|---|---|---|---|---|
| **서막 · 개정** | Stakes and the questions | Which mystery to open first | Seeds the L1 consensus, or lets someone else accuse | — | The House **engraves** the 2–3 mysteries on the clock-face floor; the urn and name-stones sit before Yusti | 2–3 min |
| **1막 「첫인상」** (first bell) | Break the supporting tricks; flip 2–3 fakes | Present a card beside a claim; ask the source; let it run; request a small 재현; spare or expose a side secret | Quietly pushes toward the scapegoat (agenda tablet) | **R1:** "It was murder, and it points at ___" (L1→L2) | Optional short re-enactment | 8–12 min |
| **2막 「뒤집힌 이야기」** (second bell) | The aha | When to break the room's consensus; which card is the hinge; drive the Memory Stage | Leads the pile-on on the L2 suspect; defends the decoy | **R2:** the trick exposed (L2→L3); name-plates fall | **The Memory Stage** (main set piece) | 8–12 min |
| **3막 「반격」** (third bell) | Corner the culprit | Answer ≤3 counters; choose the decisive evidence | Typed counterplay (§3.8); composure burns | **R3 (optional):** a protector's confession, a wrong victim, or the victim's own plan | The decisive moment (e.g. the bird sings in court) | 5–8 min |
| **4막 「되짚기와 판결」** (fourth bell) | Recap, vote, verdict, confession, aftermath | Reconstruct the night in triples; vote; one response line in the confession | Breaks; confesses *after* the verdict | — | Recap film; confession memory | 6–10 min |

The confession comes **after** the verdict so the vote keeps its tension. Act III ends in a breakdown and a partial admission, not a full confession.

### 3.6 The 10-card deck (5 true, 5 fake) inside the flow
**Definition.** A *fake* card shows a real thing whose **apparent meaning is false**: the stopped watch's hour, the shout "at 11", the locked bolt, the planted coil, the lying alibi, the misread coat. A *true* card is a seam or a record whose meaning holds.

Fakes have five origins, hidden until the card is flipped:

| Origin | Korean chip | Source |
|---|---|---|
| Culprit's staging | 위장 | TrickRun presented artefacts |
| Protector's cover | 감싸기 | Protector role, Testimony omissions |
| Misperception | 착각 | MS-02 beliefs with a wrong cue or anchor |
| Innocent disturbance | 우연 | MS-07 |
| The victim's own plan | 피해자의 계획 | SchemeDef |

**Generation: `DeckApi.Build(caseFile)` in the kernel, owned by trial-reforge, fed by the foundation.**

- **True, 5 slots:**
  - the hinge;
  - an independent confirmation of the hinge (a disjoint root);
  - one seam per supporting trick (1–2 slots);
  - a culprit link (possession, body, record);
  - when it is a path root, a foreshadow recall.
- **Fake, 5 slots:**
  - the aha's presented artefact;
  - each supporting trick's artefact;
  - the culprit's alibi statement;
  - one misperception or innocent disturbance;
  - a protector's lie, if any.
  - A D1 case with too few falsifications fills from innocent quirks near the scene (DailyLife §7.7), each still with a breaker.
- **Invariants:**
  - every fake has a *breaker*: a true card, or a pair of true cards;
  - every true card breaks at least one fake or proves a constraint (§3.10);
  - House-sealed records are never fakes;
  - every card has one photo, from the MS-05 snapshot or the ledger moment, and a one-line plain face;
  - the count "5 of 10 lie" is public, so the goal is clear.
- **Recommended foundation change.** `CaseApi.KeyClues` (4–7) splits into `TrueClues(≤5)` and `FakeClues(5)`, derived from the same CaseFile.

**Play.** An NPC leans on a fake ("열한 시에 외침이 들렸잖아"). The player lays the breaker beside that claim (제시한다). The fake **flips** like a tarot card:

| Side | Example |
|---|---|
| Front | "밤 11시, 음악실에서 들린 시온의 외침" |
| Back | "종이 칠 때 운 것은 태엽 새였다", plus the origin chip 위장 and the breaker's photo |

Differences from DR's truth bullets:
- no timer;
- no aiming at scrolling text;
- the claims wait in the ledger (발언 되짚기);
- the verb is *pairing a truth to a lie*, not shooting a statement.

**Collection.** The investigation's job is to find the 10 cards. The HUD shows "단서 6/10". Anything else examined is "기타", never required. If the investigation ends short, the catch-up rule applies (SF6).

### 3.7 Where the player decides (cadence ≤4 readable beats)
| Decision | When | Consequence |
|---|---|---|
| Which mystery lantern to open | Each act start | The order of play; NPC petitions pull toward their agendas |
| Present a card beside a claim (제시한다) | Any time | Right pair → the fake flips. Wrong → the claimant answers back (§3.8) |
| Ask the source (출처 묻기) / press | Any time | Draws out cues and anchors (MS-02): "직접 보셨어요, 들으셨어요?" |
| Let it run (지켜본다) | Any time | NPCs continue; someone with information petitions for the floor |
| Grant the floor to a petitioner | When offered | New testimony, sometimes a real lie to catch |
| Request a 재현 | With a card and a question | A public fact plaque ("불가능: 줄이 고리에 닿지 않는다") |
| Spare or expose a side secret | When a protector or innocent's secret is reachable | Spare: verify the alibi without the secret. Expose: a twist that costs that relationship |
| Point suspicion (의심을 건다) | Any time | Moves the public lean; the target answers |
| Choose the decisive evidence | Act III | Ends the culprit's defence |
| Reconstruct in triples | Act IV | Clock-chime confirmations |
| Vote | Act IV | Urn |
| Respond to the confession | After the verdict | Relationship changes (the culprit's friends) |

### 3.8 How the culprit fights back (typed counters from the CaseFile)
At most 3 in Act III (tier-dependent), and earlier as agenda moves. Each counter is generated from facts that are true in the record, so it is never a cheat.

| Counter | Culprit line (example, in the culprit's voice) | Built from | Player's answer | If the player misses |
|---|---|---|---|---|
| Other explanation | "새가 운 건 맞아. 시온이 장난친 거겠지." | An unexposed claim with an innocent reading | The card or 재현 that kills the innocent reading | The culprit gains a black-wax "버텼다" mark (a counterweight on the Scale); hint tier +1 |
| Time attack | "10시 20분? 그때 난 방에서 줄 갈고 있었어." | The culprit's alibi lie (`Testimony`) | Flip the alibi fake with its breaker (a sighting, route time) | As above |
| Redirect using someone's real lie | "재하는 왜 연습했다고 거짓말했는데?" | Another resident's real lie or omission | Spare (verify without the secret) or expose | The room's eyes drift to that person; recoverable next exchange |
| Partial admission | "새를 가져간 건 나야. 소리 좀 들어 보려고." | A non-lethal prep step (Borrow beat) | The step that cannot be innocent (the bird was *wound for the hour* and *hidden in the piano*) | As above |
| Demand for proof | "됐고, 증거 대 봐." (always last) | — | The NPC-independent path root tied to the culprit, or a slip (§3.11) | The culprit reaches the vote with the Scale not yet tipped. The player may retry with another card once |
| Appeal to an ally | An ally speaks: "라온은 그럴 애가 아니야." | Relations Attach | Facts, or simply letting the ally speak (drama, not a test) | No cost |

**Composure candles** (trial-fun) burn with each correct answer. At the threshold the culprit's bible tell fires:
- 라온 touches the earphone that is out;
- 이현 summarises you and touches his jacket button;
- 진우 shows the HollowGrin.

When the candle gutters, the character-specific breaking point plays (CharacterBible §4).

### 3.9 Reversals (guaranteed by construction)
| Reversal | Source | Guarantee |
|---|---|---|
| R1 · L1→L2 | Supporting tricks break; the L2 suspect is the scapegoat or the opportunity suspect | Every case (F §C4) |
| R2 · L2→L3 | The aha breaks. With a decoy (MS-04), the scapegoat's `CannotDo` breaks it on the Memory Stage | Every D2+ case |
| R3 · optional | A protector's false confession (G67), a wrong victim, the victim's own scheme, a trusted culprit (G87–G93), a SelfClear reopened (G64) | ≥1 in D3 cases |

Presentation (trial-fun's stained-glass reversal): the window cracks, the music drops out, the gallery eyes blink shut and turn to the new person, and a brass plaque records the settled point.

### 3.10 The Memory Stage (기억의 무대) — the trick shown in 3D
**Kernel contract.**
```csharp
public static class Reenact {
  ReenactScript Truth(GameState s, string incident, string trickRun);          // from TrickRun setup steps + mechanism + rig nodes
  ReenactScript Hypothesis(GameState s, string incident, List<Prop> claims, string actorOrShadow);
  ReenactVerdict Evaluate(ReenactScript h);   // first failing step: Reach | Time | Strength | Knowledge | Access | Physics, + LineKey
}
public sealed class ReenactStep { double Clock; string Actor; /* or "shadow" */ string StepKind; List<string> Props; WhatIfResult Phys; string CaptionKey; }
```
Physics outcomes come only from `PhysApi.WhatIf` (closed form, H1). The stage replays kernel truth; Unity does not simulate it.

**Game.**
- The courtroom's central floor opens and the crime room rises from below: rebuilt at 1:1 from the MansionView room module, with props placed from the MS-05 snapshot.
- The **real character models** perform the `StepDef` motions.
- A plain-time clock hangs above: "밤 10시 20분쯤". Time-lapse: a candle burns 70 minutes in 7 seconds.
- PhysMirror plays the FX (the fall, the splash, the spark, the bird's song).
- The player drives it with a brass lever on the clock-face: scrub time, pause, and "if ___ did it" (hypothesis).
- ≤20 s per demonstration, repeatable, skippable.

**Hypothesis first, then truth.**
1. The room's L2 story plays with the scapegoat's own model, and fails visibly:
   - the rope does not reach;
   - the prosthetic hand cannot hold the thread;
   - the route takes 15 minutes, not 4.
2. The true mechanism plays with a **shadow figure**: a dark silhouette sized by the constraints the mechanism implies. The culprit's face appears only after the accusation (in the recap film).

**Name-plate elimination (명패가 눕는다).**
- Each proven constraint, in plain words, turns face-down the name plates on the witness stands of residents who fail it:
  - "선반 위 고리에 손이 닿는 키";
  - "오른손으로 묶은 매듭";
  - "기계새를 다룰 줄 아는 사람";
  - "밤 10시 20분쯤 음악실에 갈 수 있었던 사람".
- Constraints come from the mechanism and the trick (reach, strength, hand, skill, knowledge via MS-08 or foreshadow, access via keys or loans, window via MS-03). Each is proved by a card, a 재현 or a testimony.
- Target: ≤3 plates standing after R2. The decisive evidence picks one.
- This is Queen's and Arisugawa's elimination made physical. The whole court sees it, and the Scale follows.

### 3.11 Decisive evidence and the slip (비밀의 폭로)
The decisive evidence is the **NPC-independent path root** (B/S/O/R/X channels) that links to the culprit: possession, a mark on the body, a record, an experiment.

Optional bonus: the **slip detector**.
- A culprit utterance contains a Prop that was not public when spoken, and that only a participant could know. Example: "피아노 안에…" said before the bird was found.
- The kernel compares each culprit utterance with the public-fact timeline.
- Low composure and lower Deceit make a slip more likely (counter hash, tier-gated).
- The player uses 출처 묻기 → "그걸 어떻게 알았죠?". The House's public-fact timeline shows it was not public.
- A slip is **never the only** decisive path (it depends on the culprit's speech).

### 3.12 The confession (after the verdict)
Five beats, 60–120 s, skippable after the first viewing.
1. **Admission** in the character's voice, not a template:
   - 라온: "…아 뭐. 맞아. 내가 감았어."
   - 준서: "…배고프면, 사람은 싸우잖아요."
2. **Memory.** Three stained-glass frames from the ledger, with the real actors in 3D (cinematics' montage): the old wound, the moment of decision, the act.
3. **The victim's face.** `CaseFile.Reframe`: what the two meant to each other, or the victim's own plan. Example: "시온은 매일 밤 '오늘의 단어'에 형 이름을 적었대."
4. **Last request,** character-specific (bible). Example, 준서: "누룽지는… 누가 좀 챙겨 줘요."
5. **민혁's one line** (player choice): stand beside them ("…들었어요."), ask ("왜 저한테는 말 안 했어요?"), or stay silent.

Music drops from the trial's EDM to a solo instrument tied to the culprit (MusicCueSheet: bass for 라온, a cello for 은결).

### 3.13 Recap, vote, verdict, aftermath
- **Recap (되짚기).**
  - The player lays out the night on the clock face in **triples**: who + where + when; means + trick + concealment. A chime rings only when a triple fits the public facts.
  - Then "그날 밤의 재구성" plays *the player's version* with the real actors (existing cinematics).
  - Before the vote, one screen: "이대로 투표할까요?". If any fake is still unflipped, it adds "아직 뒤집지 않은 거짓이 한 장 있다".
- **Vote.** Anonymous stones in the urn. NPC votes read the public Scale first (trial-fun), so a complete public case wins.
- **Verdict (Yusti).**
  - "지목은… 맞았습니다. 정해진 대로 집행하겠습니다." The goldfish stop.
  - A wrong vote gets "틀렸습니다" and the canon consequence.
- **Execution** is brief and symbolic (the House takes them; the eyes close). It is **not** an elaborate spectacle, which is a DR signature.
- **Aftermath** (DailyLife §8): the empty seat, 준서's spoons, the memorial, grudges along the vote lines, and the reframe written into the notebook.

### 3.14 Yusti's lines (procedure only, 하십시오체)
| Beat | Line |
|---|---|
| Opening | "알려 드립니다. 심판을 시작하겠습니다. 정해진 대로, 지목은 한 번입니다." |
| Act bell | "두 번째 종입니다." |
| Accusation call | "지목할 분의 이름을 말씀해 주십시오." |
| Vote | "투표를 받겠습니다. 돌은 한 사람에 하나입니다." |
| Result | "돌을 세었습니다. …가장 많은 이름은 {t}님입니다." |
| Verdict | "지목은… 맞았습니다." / "지목은… 틀렸습니다." |

He never comments on the arguments, never hints, and never jokes.

### 3.15 Anti-pedantry rules (for writers and the director)
- One claim per line, and at most 2 subtitle pages per NPC turn.
- A settled fact becomes a brass plaque and is never restated.
- "Agree" is a nod, an eye turn or a wax dot, never a line.
- Every exchange ends in a visible state change: a card flips, a plate falls, a candle gutters, the eyes turn.
- No round without a real target (no dead rounds).
- No timers by default.
- ≤3 set pieces per trial.
- No forensic jargon (lint: 시반, 사후 경직, 출처 as a noun in dialogue, …); times always plain.
- Ctrl fast-forwards lines; films skip after the first viewing.

### 3.16 Worked example — 「열한 시의 외침」 (D2, original)
**Setup (all from registered steps).**
- **Culprit and motive.** 서라온 learns (knot K2, CH06 envelope) that 유시온 informed on 라온's band leader, 시온's own older brother, who was jailed. Motive: grief and grudge.
- **Foreshadow.** At about 22:00, 수아 sees 라온 carrying the clockwork bird from the 전시실 cage "to check its pitch". Daily life already taught what the bird does (해린's bond scene).
- **22:10.** 라온 invites 시온 to the MusicRoom: "그 곡, 한 번만 같이 맞춰 보자."
- **22:20.** Garrotte with the piano's highest string (M.Garrote.Wire).
  - 시온 shouts "야! 놔!". The bird keeps it: the loudest phrase since winding.
  - 라온 mutters "…형 몫이야." That goes to the second cylinder, which 라온 does not know exists (the cage plate mentions only mimicry; the Library manual mentions both).
  - He winds the bird for the hour and hides it inside the piano.
- **Frame.** At 16:00, 라온 signed out the props-box key on 태겸's loan ledger ("케이블 찾으러") and dropped an old coil of piano wire into 재하's props box.
- **Arranged witness.** At 22:50 라온 asks 준서 for a late 누룽지 in the Lounge.
- **23:00.** The hall clock strikes, and the bird sings "야! 놔!" from inside the piano. 준서 and 수아 hear it. 라온, "the ear", says: "생목이었어. 스피커 소리 아니야." All run to the MusicRoom.

**Layers.**
- **L1:** 시온 was strangled at 23:00, right after shouting. 재하 was alone next door, and a wire turns up in his box.
- **L2** (once the frame breaks): whoever was at the door at 23:00. 진우's candy-wrapper rope is at the door (he passed at 22:55, innocent), so the room turns to 진우.
- **L3:** the voice was the bird replaying 22:20. The killer is whoever placed and wound it.

**Mysteries engraved:** 「열한 시의 외침 — 그 목소리는 누구의 것이었나」 · 「비어 있는 새장 — 누가 새를 음악실로 옮겼나」

**The deck:**
| # | Card (face) | Truth | Breaks / proves |
|---|---|---|---|
| T1 | 기계새 — 피아노 속, 태엽이 감겨 있다 (hinge) | True | F1, F2 |
| T2 | 시신 — 목의 가는 줄 자국, 옷깃이 먼저 식었다 (밤 10시~11시, 앞쪽) | True | F3 (gauge), F4 (time) |
| T3 | 『태엽 새 다루는 법』 — 큰 소리는 종이 칠 때, 작은 소리는 꼬리를 누르면. 노래 끝에 딸깍 | True | F1, F2; enables the court replay |
| T4 | 태겸의 대여표 — 소품 상자 열쇠, 오후 4시쯤 라온 | True | F3 (who could plant it); culprit link |
| T5 | 기억 — 밤 10시쯤, 기계새를 안고 음악실 쪽으로 가던 라온 (수아) | True | F5; "knew the bird" constraint |
| F1 | 열한 시의 외침 — 준서·수아: 음악실에서 시온 목소리 | Fake · 위장 | ← T1 + T3 |
| F2 | 라온의 귀 — "생목이었어" | Fake · 위장 | ← T3 + Memory Stage click |
| F3 | 재하의 소품 상자 속 피아노 줄 | Fake · 위장 | ← T2 (thicker wire) + T4 |
| F4 | 문가의 사탕 껍질 끈 (진우) | Fake · 우연 | ← T2 (death before 진우 passed at 22:55) |
| F5 | 라온의 진술 — 밤 10시~10시 반, 방에서 줄 갈았다 | Fake · 위장 | ← T5 (+ route time, MS-03) |

Fair play for the time falsification: path (a) T1 + T3 (object + record), path (b) T2 (body). Disjoint roots, and neither depends on an NPC.

**Flow (key lines).**
- **서막.** 이현 opens: "자, 자. 정리하죠. 열한 시에 외침이 들렸고, 그때 음악실 옆에 혼자 있던 사람은 재하 씨뿐입니다."
- **1막.** The player flips F3 (T2: the coil is thicker than the wound; T4: who held the props key is noted as a public fact, not yet argued). 재하: "…고마워. 혼자였던 건 맞아." The room turns to 진우 → F4. 진우, HollowGrin: "흐응, 문 앞에 있었던 건 맞아. 왜 있었는지는… 맞혀 볼래?" The player flips F4 with T2. The side secret (why 진우 walks to a clock at 23:40) can be spared.
- **2막.** 가온: "잠깐만요. 직접 보셨어요, 들으셨어요?" The player presents T1 and T3 against F1. **Memory Stage:** the MusicRoom rises. The hall clock strikes, the piano lid trembles, the bird sings "야! 놔!", and the song ends with a *click*. F2 flips. Plates fall for "밤 10시 20분쯤 음악실 근처" (MS-03) and "기계새를 다룰 줄 아는 사람" (T5, 해린's bond knowledge is public). Standing: 라온, 해린.
- **3막.** 라온: "새가 운 건 맞아. 근데 그게 나랑 무슨 상관인데." Time attack → the player flips F5 with T5. Partial admission: "새를 가져간 건 나야. 소리 좀 들어 보려고." → the bird was wound for the hour and hidden. Then "됐고, 증거 대 봐." → the player asks for the bird's tail to be pressed (T3). In the silent court, the second cylinder plays: "…형 몫이야." — in 라온's voice. 라온 reaches for the earphone that is out, and his candle gutters.
- **4막.** Triples chime. Vote. "지목은… 맞았습니다." Confession: 형, the unreleased song, the USB. Reframe: 시온 wrote his brother's name as "오늘의 단어" every night.

---

## 4. Stress-free deduction

### 4.1 Principles
1. **The game shows you where to look.** Key traces draw attention inside the fiction, so there is no pixel hunting.
2. **There is always a named question.** The mystery is written down; the player never wonders what they are solving.
3. **You remember what you saw.** Daily-life moments come back on their own.
4. **Right partial answers are confirmed; contradictions are pointed out gently.** Nothing says which card lies.
5. **Hints come from the case's own fair-play paths**, in tiers, and never decide the logic for you.
6. **The case cannot become unwinnable** during the investigation.
7. **Mistakes in the 심판 cost something recoverable.** The only hard gate is the vote, and the game recaps before it.
8. **Everything is plain Korean with plain times.**

### 4.2 Features, tied to systems
| # | Feature | What the player sees (example) | System tie | Owner | Size |
|---|---|---|---|---|---|
| SF1 | **Engraved mysteries** | At discovery the House writes 2–3 riddles into the investigation card and the 수첩: 「잠긴 서재 — 누가 안에서 빗장을 걸었나」. HUD: `다음 — 서재 문 아래를 살펴본다` | `CaseApi.Mysteries` = L1 claims that are false, as LineKeys; clues-and-qol HUD line | New + foundation | M |
| SF2 | **Traces that call attention** | A drip heard, a glint in the candle light, a smell line ("밀랍 냄새가 난다"). The FP camera eases toward it once. A watcher NPC says "저기, 문 아래 뭐가 있어." | KeyClue visibility (F §D2) + TraceKinds cue + Game `AttentionCue` + NPC lens (CastTraits) | New | M |
| SF3 | **기억 — recall vignettes** | "기억 — 아침, 채령 씨에게 실을 빌려 가던 해린 씨", with a 3 s replay from roughly where the player stood | Foreshadow records (F §C4) + `RecallRelevant` (E) + ReplayStage clip | New + cinematics | M |
| SF4 | **추리판 — deduction board** | Pin two cards: "맞물린다 ✔ — 문은 밖에서 잠겼을 수 있다", or "이 두 장은 서로 맞지 않는다". Confirmed inferences become gold stamps usable in the 심판 | `Insight.Check(cards)` against CaseFile claims and seams. Local meaning only (what a trace means), never *who* | New | L |
| SF5 | **여백의 목소리 — hint tiers** | ① "관찰: 불을 켠 적 없는 고리에 밀랍이…" ② "대현관 샹들리에 고리를 다시 보자." ③ "고리의 밀랍은 초를 매달았던 자국이다." | FairPlay paths → the nearest unfound root of an unexposed path; flavour from player faculties (trial-fun) | New | M |
| SF6 | **No dead ends** | At the investigation's end, a missing card arrives found by someone else: "은결 씨가 찾아 둔 단서 — 기계새". The finder testifies | House scene preservation (MS-05) + NPC investigators (`Cases.Investigate`, E) + FairPlay guarantee | Foundation + new | S |
| SF7 | **Recoverable mistakes** | A wrong card: the claimant answers back and the player's voice candle shortens. It relights on the next right move. No game over before the vote | Trial-reforge Scale and candles (§3.8) | trial-reforge | M |
| SF8 | **되짚기 before the vote** | Clock-chime triples, then the film of "your version", then "이대로 투표할까요?" with any unflipped fake flagged | Reconstruction + RevealPlayer (E) + DeckApi | trial-reforge | M |
| SF9 | **민혁의 메모 — plain notebook** | Per mystery, 2–4 sentences that update: "열한 시에 음악실에서 시온 씨 목소리가 들렸다. 그런데 시신은 그보다 먼저 식기 시작했다. 목소리가 정말 그때 난 걸까?" | ClueTag meaning keys + claim status + Beliefs; lint for jargon | New | M |
| SF10 | **다섯 장의 거짓** | "단서 10장 중 다섯 장은 거짓을 말한다", and a flipped counter | DeckApi | trial-reforge | S |
| SF11 | **저택의 인장 — guaranteed records** | A wax seal on House and machine records (lock log, bell log, door logger, breaker log, roll call): "기록은 정확하다. 읽는 법이 틀릴 수는 있다." | Record provenance; never a fake | trial-reforge + new | S |
| SF12 | **Unasked cells** | The 동선 grid dots cells nobody was asked about: "물어볼 것 — 밤 10시쯤 복도를 닦던 사람" | MS-02 salience + the existing 동선 tab | New | S |
| SF13 | **재현 on demand** | "초가 줄까지 타는 데 걸리는 시간 — 한 시간쯤" | F A10 `WhatIf` | Foundation + new | M |
| SF14 | **Walking times on the map** | "식당 → 온실 · 걸어서 3분쯤" | MS-03 | New | S |

### 4.3 Difficulty presets (the case never changes; only the help does)
| Setting | 쉬움 | 보통 | 어려움 |
|---|---|---|---|
| Hints | All 3 tiers, tier ① offered after 90 s idle | On request; tiers ①–② free, ③ costs wax | Tier ① only |
| 추리판 contradiction flags | On | On | Off (confirmations only) |
| Card shows which mystery it concerns | Always | After first use | Never |
| Culprit counters in Act III | 1 | 2 | 3 |
| Voice-candle cost of a wrong card | None | Light | Normal |
| Investigation extension | +30 min, free | Once | None |
| Timers in the 심판 | Off | Off | Optional |

The 5/5 deck, the FairPlay guarantees and the reversals are identical at every setting.

### 4.4 Failure and recovery
| Situation | What the game does |
|---|---|
| A key card was not found by the end of the investigation | The NPC-found catch-up card, with the finder's testimony (SF6) |
| An innocent tidied a clue away before discovery | FairPlay's second path exists by admission; the tidy becomes a "우연" fake with an honest explanation (MS-07) |
| The player is stuck | The `다음 —` line, then the hint tiers |
| Wrong card in the 심판 | The claimant counters and the candle shortens. After 2 misses on the same claim, the hint tier rises |
| The room is leaning toward an innocent (the player pushed early) | The Scale shows only public facts. The recap flags unconfirmed chimes. The player may reopen one mystery once before the vote |
| Wrong vote | The canon consequence stands (the stakes). The reveal still shows the truth, and the culprit's confession arrives as the night's aftermath |

---

## 5. Prioritised implementation plan

### 5.1 Order and gates
1. **Now (design).** This study feeds the murder-foundation Gate and the trial-reforge synthesis. No code changes until their gates.
2. **Foundation Phase 1** (skeleton) is unchanged.
3. **Foundation Phase 2b** (after Phase 1 exits): the P0 kernel systems MS-02 to MS-05, plus `CaseApi.TrialPack` (A1 below).
4. **trial-reforge TRIAL stage** consumes `TrialPack`: DeckApi, the Memory Stage, counters, the elimination plates.
5. **New `deduction-ux` work** (or a clues-and-qol follow-up) in parallel with step 4: SF1–SF6, SF9, SF12, SF14.
6. **P1 and P2 systems** land chapter by chapter after the vertical slice plays well (memory `commercial-quality-vertical-slice`).

### 5.2 murder-foundation workflow — kernel tricks and systems
| # | Item | Pri | Size | Depends on | Owner (G1) |
|---|---|---|---|---|---|
| A1 | `CaseApi.TrialPack(incident)`: Mysteries (L1 false claims → LineKeys), FakeArtefacts (presented, with origin), TrueClues, Breakers (seam → claim map), Constraints (reach, strength, hand, skill, knowledge, access, window), SlipFacts, PublicFactTimeline. Split KeyClues into TrueClues ≤5 and FakeClues 5 | P0 | M | CaseFile (F §C5) | #3 |
| A2 | MS-02 Belief provenance + `CastMarks` | P0 | L | Perception, Testimony (delimited blocks) | new #6 or #3 |
| A3 | MS-03 Route atlas and alibi windows | P0 | M | Nav | #2 with #1 |
| A4 | MS-04 Story authoring (StoryPlan, DecoyDef, arranged witnesses, SelfClear, FakeAttempt, ScriptFollower) + `CastTraits.CannotDo` | P0 | M | A2, Requests | #3 + case-drama + planner |
| A5 | MS-05 Discovery log, snapshots, scene preservation | P0 | M | `Cases.OnBodySeen` hook | #3 |
| A6 | Extensions §2.3: clock rate, clockwork-bird rules, tower clock, hinge pins, spindle gap, magnet, pristine media, figures, mirror sightlines | P1 | S each (M total) | F R8/R9/R12 | #1 |
| A7 | MS-08 Habit registry | P1 | S–M | CastTraits.Fixed, HabitShared | daily-life + #2 |
| A8 | MS-07 Disturbances and victim acts | P1 | M | Routines | #2 |
| A9 | MS-06 Rig composer | P1 | M–L | WhatIf geometry | #3 + #1 |
| A10 | MS-01 House Secrets (kernel + layout placement), then visuals by the environment agent | P1 | L (+L visuals) | Layout, Nav, A3 | #1 + environment + rooms author |
| A11 | New trick templates (§2.4), 18 entries | P1 | S each | A2–A10 as listed | tricks-* authors |
| A12 | MS-09 Mitate engine + 2 original verses | P2 | M (+ writing) | A4 | case-drama + #3 |
| A13 | MS-10 Dying message + house cipher vocabulary (door emblems, roster colours, seat numbers) | P2 | M | R10, lore | #3 + tricks-cause |
| A14 | Lab metrics (§5.5) in `MurderLab` | P0 | S | A1 | #5 |

### 5.3 trial-reforge workflow — 심판 flow and UI
| # | Item | Pri | Size | Depends on |
|---|---|---|---|---|
| B1 | `DeckApi.Build` (5 true / 5 fake, breakers, origins) + photo capture from MS-05 snapshots (the DECK stage's `CineShot` service) + the flip | P0 | M | A1, A5 |
| B2 | Act director: bells, engraved mysteries as lanterns, decision cadence ≤4 beats, petitions and agendas, the Scale of Proof (trial-fun) | P0 | L | A1 |
| B3 | Memory Stage: kernel `Reenact` scripts + the Game stage (room rise, real actors, shadow figure, clock lever, FX via PhysMirror) | P0 | L | A1, F WhatIf, PhysMirror; cinematics agent |
| B4 | Culprit counterplay engine (typed counters from the CaseFile) + composure candles and tells | P0 | M | A1, A2 |
| B5 | Name-plate elimination from constraints | P0 | M | A1, A3 |
| B6 | Slip detector and "그걸 어떻게 알았죠?" | P1 | M | A1 (PublicFactTimeline), Testimony |
| B7 | Confession scene (5 beats) + response choice | P1 | M | Reframe keys; cinematics |
| B8 | Recap: clock-chime triples, film of the player's version, pre-vote check | P1 | M | RevealPlayer (E) |
| B9 | Yusti procedure lines, urn, verdict | P1 | S | Lines_NPC00 |
| B10 | Spare/expose branch; wax pledges | P2 | M | Relations |
| B11 | Headless `trialfun` gate in SimTests (cadence, dead rounds, reversals, smart-vs-passive delta) | P0 | S | B2 |

### 5.4 New work — deduction UX (propose `deduction-ux`, or extend clues-and-qol)
| # | Item | Pri | Size | Depends on |
|---|---|---|---|---|
| C1 | SF1 engraved mysteries + the `다음 —` line | P0 | M | A1 |
| C2 | SF2 attention cues (Game) | P0 | M | KeyClues, TraceKinds |
| C3 | SF3 recall vignettes | P0 | M | Foreshadow, ReplayStage |
| C4 | SF4 추리판 (`Insight.Check`) | P0 | L | A1 |
| C5 | SF5 hint tiers from FairPlay paths | P0 | M | F FairPlay |
| C6 | SF6 catch-up and scene preservation | P0 | S | A5 |
| C7 | SF9 plain notebook summaries | P1 | M | ClueTags, A2 |
| C8 | SF12 unasked cells; SF14 walking times | P1 | S | A2, A3 |
| C9 | Difficulty presets (§4.3) | P1 | S | C4, C5, B4 |
| C10 | SF13 experiment bench UI in the investigation | P2 | M | F WhatIf |

### 5.5 Lab metrics and acceptance (added to MurderFoundation §E2 and the trial harness)
| Metric | Target |
|---|---|
| Decks valid: 5/5 split, every fake has a breaker in the deck | 100% |
| Fakes with two breaker routes | ≥60% |
| Engraved mysteries per case | 2–3 |
| Plates standing after R2 | ≤3 in ≥90% of cases |
| Counters available per case, by tier | 100% |
| Slip available (never required) | ≥30% of cases |
| Decision gap in headless trials | mean ≤3.5 beats, max ≤6 |
| Dead rounds | 0 |
| Reversals | ≥1 in 100%; ≥2 in ≥80% of D2+ |
| Smart vs passive vote delta >0 | ≥75% of seeds |
| Knowledge-limited investigator solves | ≥70% without hints; ≥85% with tier ① |
| Each of the top-15 tricks fires naturally within 300 cases | 100% |
| House secret passages active per chapter | ≤1; the player's route finds the tell in ≥60% when used |
| Determinism and save growth | unchanged from F §E2 (IDENTICAL; ≤100 KB/day) |

### 5.6 Decisions for the owner
1. **Meaning of "가짜 5".** Cards whose surface meaning is false (planted, covered, misread, accidental, or the victim's plan), with the count public. *Recommended.*
2. **House-sealed records are guaranteed exact** (the "Umineko" idea without its look). This is compatible with D-020 because the seal marks provenance, not the truth of a reading. *Recommended.*
3. **Confession after the verdict** to keep the vote tense. *Recommended.*
4. **Secret passages** allowed at ≤1 per chapter, with the grammar published in 『저택의 문법』. *Recommended from Ch2.*
5. **Mitate and serial patterns** only from Ch3 (double events). *Recommended.*
6. **A trusted-ally culprit** (high bond with 민혁) at most once per loop. *Recommended.*
7. **The Memory Stage shows the culprit only as a shadow** until the accusation. *Recommended.*
8. **Execution** brief and symbolic, never a spectacle sequence. *Recommended (DR-distance).*

**DECIDED by the main session (19:25), per the owner's standing instruction to decide without asking.** All eight are adopted. Two clarifications:
- **(1)** The HUD shows the deck as "단서 10 · 그중 거짓 5". Fakes are never colour-coded.
- **(8)** Executions are brief and gothic, with implied horror (a candle, a shadow, a sound, the aftermath) rather than an elaborate machine spectacle. This keeps the horror the owner wants and the distance from Danganronpa.
- The P0 systems (MS-02 to MS-05) go into the first vertical-slice chapter. P1 and P2 follow.

### 5.7 Risks
- **Scope creep against the vertical slice.** Build only P0 for the first polished chapter; P1 and P2 follow.
- **Layout risk (MS-01).** Placement must not touch the layout RNG or nav connectivity tests. Ship public features first, secrets second.
- **Belief volume (MS-02).** Compute at query time and cache per incident; store ≤4 cues per sighting.
- **Trial length.** Enforce set-piece caps and the anti-pedantry rules. Measure real minutes in probes on 2+ seeds, including `rand`.
- **Originality.** Apply the §0.4 rule and the watch-list in §2.5 at Catalog validation and in critic passes.

---

## Appendix A — Sources checked on the web (for case summaries)
- [The Kindaichi Case Files — Wikipedia](https://en.wikipedia.org/wiki/The_Kindaichi_Case_Files)
- [金田一少年の事件簿FILEシリーズの各事件ごとの感想 (teke teke my life)](https://manga.teketekemylife.com/entry/manga202208)
- [金田一少年の事件簿 Fileシリーズ簡易まとめ後編 (プリズムルート876)](https://imasdsmad.exblog.jp/30586511/)
- [飛騨からくり屋敷殺人事件 (新稀少堂日記)](https://ameblo.jp/s-kishodo/entry-10388131906.html)
- [オペラ座館殺人事件 犯人・トリック (スタ誕MOVIE)](https://statan.jp/operazanokaijin-culprit/) · [dorama9](https://dorama9.com/kindaichisyounen-operazakann3-netabare/)
- [異人館ホテル殺人事件 (text-hack)](https://www.text-hack.com/2024/02/kindaichi-ijinkanhoteru-satsujinjiken.html) · [新稀少堂日記](https://ameblo.jp/s-kishodo/entry-10375521982.html)
- [斜め屋敷の犯罪 解説 (kratter, note)](https://note.com/kratter2/n/ndad9f713ef5f) · [たまあざらし](https://tamaazarashi.com/novel-review-91)
- [『時計館の殺人』トリック解説 (のみやすい読書)](https://nomidoku.com/the-clock-tower-the-trick/)

All other summaries come from general genre knowledge and are paraphrased briefly. No text from any work is reproduced.
